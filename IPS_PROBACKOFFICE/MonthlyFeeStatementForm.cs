using InvoiceReportPrinter.WinForms.Data;
using InvoiceReportPrinter.WinForms.Models;
using IPS_PROBACKOFFICE.Data;
using IPS_PROBACKOFFICE.Models;
using IPS_PROBACKOFFICE.Services;
using System.Data;
using System.Globalization;

namespace IPS_PROBACKOFFICE;

public sealed class MonthlyFeeStatementForm : Form
{
    private readonly TradeRepository _tradeRepository;
    private readonly BrokerageFeeRepository _feeRepository;
    private readonly BusinessDaySettings _businessDaySettings;
    private readonly DateTimePicker _month = CreateMonthPicker();
    private readonly NumericUpDown _exchangeRate = new()
    {
        Width = 110,
        DecimalPlaces = 2,
        Minimum = 0,
        Maximum = 10000,
        Increment = 0.10m,
        ThousandsSeparator = true
    };
    private readonly Button _search = new() { Text = "조회", AutoSize = true };
    private readonly Button _excel = new() { Text = "월별 계산서 저장", AutoSize = true, Enabled = false };
    private readonly Label _status = new()
    {
        Text = "기간 선택 후 조회 버튼을 눌러주세요.",
        AutoSize = true,
        ForeColor = Color.DimGray,
        Margin = new Padding(12, 8, 3, 0)
    };
    private readonly DataGridView _statementGrid = new();
    private readonly DataGridView _issueGrid = new();
    private readonly Font _boldGridFont;
    private DataTable? _statementTable;
    private DataTable? _issueTable;
    private IReadOnlyList<TradeRow> _currentTrades = Array.Empty<TradeRow>();
    private IReadOnlyDictionary<string, string> _currentCurrencies =
        new Dictionary<string, string>();

    public MonthlyFeeStatementForm(
        TradeRepository tradeRepository,
        BrokerageFeeRepository feeRepository,
        BusinessDaySettings businessDaySettings)
    {
        _tradeRepository = tradeRepository;
        _feeRepository = feeRepository;
        _businessDaySettings = businessDaySettings;

        Text = "수수료 계산서 내역";
        Font = new Font("맑은 고딕", 10F);
        _boldGridFont = new Font(Font, FontStyle.Bold);
        BackColor = Color.White;
        Padding = new Padding(18);

        DateTime today = DateTime.Today;
        _month.Value = new DateTime(today.Year, today.Month, 1);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 12)
        };
        toolbar.Controls.Add(LabelFor("조회월"));
        toolbar.Controls.Add(_month);
        toolbar.Controls.Add(LabelFor("환율(USD/KRW)", 14));
        toolbar.Controls.Add(_exchangeRate);
        toolbar.Controls.Add(_search);
        toolbar.Controls.Add(_excel);
        toolbar.Controls.Add(_status);

        var outputGuide = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(4, 0, 0, 10),
            ForeColor = Color.FromArgb(48, 84, 120),
            Text = $"기본 저장 위치: {MonthlyFeeStatementWorkbookExporter.DefaultOutputDirectory}\\{{연도}}년 {{월}}월 수수료 계산서 내역_전사부.xlsx"
        };

        ConfigureGrids();
        Controls.Add(CreateStatementLayout());
        Controls.Add(outputGuide);
        Controls.Add(toolbar);

        _search.Click += async (_, _) => await LoadAsync();
        _excel.Click += (_, _) => ExportExcel();
        _exchangeRate.ValueChanged += (_, _) =>
        {
            if (_currentTrades.Count > 0)
                BuildStatementTables(_currentTrades, _currentCurrencies, _exchangeRate.Value);
        };
    }

    private static Label LabelFor(string text, int left = 3) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(left, 8, 3, 0)
    };

    private static DateTimePicker CreateMonthPicker() => new()
    {
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "yyyy년 M월",
        Width = 130,
        ShowUpDown = true
    };

    private (DateTime From, DateTime To) SelectedMonthRange()
    {
        DateTime from = new(_month.Value.Year, _month.Value.Month, 1);
        return (from, from.AddMonths(1).AddDays(-1));
    }

    private Control CreateStatementLayout()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(0, 8, 0, 0) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        layout.Controls.Add(CreateGridPanel(_statementGrid, () => $"{_month.Value:yyyy년 M월} 수수료 내역"), 0, 0);
        layout.Controls.Add(CreateGridPanel(_issueGrid, () => $"{_month.Value:yyyy년 M월} 계산서 발행내역_FX와 합산처리"), 1, 0);
        return layout;
    }

    private Panel CreateGridPanel(DataGridView grid, Func<string> title)
    {
        var label = new Label
        {
            Dock = DockStyle.Top,
            Height = 36,
            Text = title(),
            Font = _boldGridFont,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 0, 0),
            Tag = title
        };
        _month.ValueChanged += (_, _) => label.Text = title();
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
        panel.Controls.Add(grid);
        panel.Controls.Add(label);
        return panel;
    }

    private void ConfigureGrids()
    {
        ConfigureGrid(_statementGrid, Color.FromArgb(252, 228, 209));
        AddTextColumn(_statementGrid, "Bank", "Bank", 150);
        AddNumberColumn(_statementGrid, "Brokerage", "Brokerage\n(수취 수수료)", 125);
        AddNumberColumn(_statementGrid, "Switch Fee", "Switch Fee\n(지급 수수료)", 125);
        AddNumberColumn(_statementGrid, "E-platform_Total", "E-platform_Total", 130);
        AddNumberColumn(_statementGrid, "계산서_KRW", "계산서_KRW", 115);
        AddNumberColumn(_statementGrid, "계산서_USD", "계산서_USD", 105, "#,##0.00;[Red]-#,##0.00;-");

        ConfigureGrid(_issueGrid, Color.FromArgb(218, 238, 243));
        AddTextColumn(_issueGrid, "No.", "No.", 60);
        AddTextColumn(_issueGrid, "은행", "은행", 145);
        AddNumberColumn(_issueGrid, "E-platform", "E-platform", 120);
    }

    private static void ConfigureGrid(DataGridView grid, Color headerColor)
    {
        grid.Dock = DockStyle.Fill;
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.RowHeadersVisible = false;
        grid.AutoGenerateColumns = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersHeight = 52;
        grid.RowTemplate.Height = 29;
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        grid.ColumnHeadersDefaultCellStyle.BackColor = headerColor;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(220, 235, 252);
        grid.DefaultCellStyle.SelectionForeColor = Color.Black;
    }

    private static void AddTextColumn(DataGridView grid, string name, string header, int width)
    {
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = name,
            DataPropertyName = name,
            HeaderText = header,
            Width = width,
            MinimumWidth = 55,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
        });
    }

    private static void AddNumberColumn(DataGridView grid, string name, string header, int width, string format = "#,##0;[Red]-#,##0;-")
    {
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = name, DataPropertyName = name, HeaderText = header, Width = width, MinimumWidth = 80,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = format, NullValue = "-" }
        });
    }

    private async Task LoadAsync()
    {
        if (!_search.Enabled)
            return;

        (DateTime selectedFrom, DateTime selectedTo) = SelectedMonthRange();

        _search.Enabled = false;
        _excel.Enabled = false;
        _statementTable = null;
        _issueTable = null;
        _currentTrades = Array.Empty<TradeRow>();
        _currentCurrencies = new Dictionary<string, string>();
        _statementGrid.DataSource = null;
        _issueGrid.DataSource = null;
        _status.Text = "조회 중...";
        string finalStatus = "조회 실패";

        try
        {
            DateTime from = _businessDaySettings.GetDailyRange(selectedFrom).Start;
            DateTime to = _businessDaySettings.GetDailyRange(selectedTo).End;
            IReadOnlyList<Institution> institutions = await _tradeRepository.GetInstitutionsAsync();
            IReadOnlyList<BrokerageFeeRow> schedules = await _feeRepository.GetAllAsync();
            Dictionary<string, string> currencies = schedules
                .GroupBy(x => x.InstCode.ToString(CultureInfo.InvariantCulture))
                .ToDictionary(
                    x => x.Key,
                    x => x.First().CurrencyCode?.Trim().ToUpperInvariant() ?? "KRW",
                    StringComparer.OrdinalIgnoreCase);

            using var queryGate = new SemaphoreSlim(4);
            Task<IReadOnlyList<TradeRow>>[] queryTasks = institutions
                .Select(async institution =>
                {
                    await queryGate.WaitAsync();
                    try
                    {
                        return await _tradeRepository.GetTradesFromViewAsync(
                            institution,
                            from,
                            to);
                    }
                    finally
                    {
                        queryGate.Release();
                    }
                })
                .ToArray();
            IReadOnlyList<TradeRow>[] results = await Task.WhenAll(queryTasks);
            List<TradeRow> trades = results.SelectMany(x => x).ToList();

            _currentTrades = trades;
            _currentCurrencies = currencies;
            BuildStatementTables(_currentTrades, _currentCurrencies, _exchangeRate.Value);
            _excel.Enabled = true;
            finalStatus = $"조회 완료 · {selectedFrom:yyyy-MM-dd}~{selectedTo:yyyy-MM-dd} · {trades.Count:N0}건 · KRW 환산";
        }
        catch (Exception ex)
        {
            finalStatus = "조회 실패";
            MessageBox.Show(ex.Message, "매출 수수료 조회 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _status.Text = finalStatus;
            _search.Enabled = true;
        }
    }

    private void BuildStatementTables(
        IReadOnlyList<TradeRow> trades,
        IReadOnlyDictionary<string, string> currencies,
        decimal exchangeRate)
    {
        DataTable statement = CreateStatementTable();
        DataTable issues = CreateIssueTable();
        decimal totalBrokerage = 0m;
        decimal totalSwitch = 0m;
        decimal totalPlatform = 0m;
        decimal totalInvoiceKrw = 0m;
        decimal totalInvoiceUsd = 0m;
        int issueNo = 1;

        foreach (IGrouping<string, TradeRow> bank in trades
                     .GroupBy(x => x.InstitutionName, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(x => x.Key))
        {
            TradeRow first = bank.First();
            bool usd = currencies.TryGetValue(first.InstitutionCode, out string? currency)
                && currency.Equals("USD", StringComparison.OrdinalIgnoreCase);
            decimal brokerageRaw = bank.Where(x => !x.IsSwitch).Sum(x => x.BrokerageAmount);
            decimal switchRaw = bank.Where(x => x.IsSwitch).Sum(x => x.BrokerageAmount);
            decimal brokerageKrw = ConvertToKrw(brokerageRaw, usd, exchangeRate);
            decimal switchKrw = ConvertToKrw(switchRaw, usd, exchangeRate);
            decimal platformKrw = brokerageKrw - switchKrw;
            decimal invoiceKrw = usd ? 0m : platformKrw;
            decimal invoiceUsd = usd ? brokerageRaw - switchRaw : 0m;

            statement.Rows.Add(bank.Key, brokerageKrw, switchKrw, platformKrw, invoiceKrw, invoiceUsd);
            if (invoiceKrw != 0m)
                issues.Rows.Add(issueNo++.ToString(CultureInfo.InvariantCulture), bank.Key, invoiceKrw);

            totalBrokerage += brokerageKrw;
            totalSwitch += switchKrw;
            totalPlatform += platformKrw;
            totalInvoiceKrw += invoiceKrw;
            totalInvoiceUsd += invoiceUsd;
        }

        statement.Rows.Add("TOTAL", totalBrokerage, totalSwitch, totalPlatform, totalInvoiceKrw, totalInvoiceUsd);
        issues.Rows.Add(string.Empty, "Total", totalInvoiceKrw);
        _statementTable = statement;
        _issueTable = issues;
        _statementGrid.DataSource = statement;
        _issueGrid.DataSource = issues;
        ApplyTotalStyle(_statementGrid, Color.FromArgb(248, 203, 173));
        ApplyTotalStyle(_issueGrid, Color.FromArgb(218, 238, 243));
        _statementGrid.ClearSelection();
        _issueGrid.ClearSelection();
    }

    private static decimal ConvertToKrw(decimal value, bool usd, decimal exchangeRate) =>
        decimal.Round(usd ? value * exchangeRate : value, 0, MidpointRounding.AwayFromZero);

    private static DataTable CreateStatementTable()
    {
        var table = new DataTable();
        table.Columns.Add("Bank", typeof(string));
        table.Columns.Add("Brokerage", typeof(decimal));
        table.Columns.Add("Switch Fee", typeof(decimal));
        table.Columns.Add("E-platform_Total", typeof(decimal));
        table.Columns.Add("계산서_KRW", typeof(decimal));
        table.Columns.Add("계산서_USD", typeof(decimal));
        return table;
    }

    private static DataTable CreateIssueTable()
    {
        var table = new DataTable();
        table.Columns.Add("No.", typeof(string));
        table.Columns.Add("은행", typeof(string));
        table.Columns.Add("E-platform", typeof(decimal));
        return table;
    }

    private void ApplyTotalStyle(DataGridView grid, Color color)
    {
        if (grid.Rows.Count == 0) return;
        DataGridViewRow row = grid.Rows[^1];
        row.DefaultCellStyle.Font = _boldGridFont;
        row.DefaultCellStyle.BackColor = color;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _boldGridFont.Dispose();
        base.Dispose(disposing);
    }

    private void ExportExcel()
    {
        if (_statementTable is null || _issueTable is null)
            return;

        try
        {
            (DateTime selectedFrom, DateTime selectedTo) = SelectedMonthRange();
            string defaultPath = MonthlyFeeStatementWorkbookExporter.BuildOutputPath(
                selectedFrom,
                selectedTo);
            string defaultDirectory = Path.GetDirectoryName(defaultPath)
                ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (!Directory.Exists(defaultDirectory))
                defaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            using var dialog = new SaveFileDialog
            {
                Title = "월별 수수료 계산서 저장 위치 선택",
                Filter = "Excel 통합 문서 (*.xlsx)|*.xlsx",
                DefaultExt = "xlsx",
                AddExtension = true,
                OverwritePrompt = true,
                InitialDirectory = defaultDirectory,
                FileName = Path.GetFileName(defaultPath)
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            string outputPath = MonthlyFeeStatementWorkbookExporter.SaveStatementToPath(
                _statementTable,
                _issueTable,
                selectedFrom,
                _exchangeRate.Value,
                dialog.FileName);
            MessageBox.Show(
                $"월별 수수료 계산서 저장이 완료되었습니다.\n{outputPath}",
                "완료");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "엑셀 저장 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

}
