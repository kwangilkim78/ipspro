using InvoiceReportPrinter.WinForms.Data;
using InvoiceReportPrinter.WinForms.Models;
using IPS_PROBACKOFFICE.Data;
using IPS_PROBACKOFFICE.Models;
using IPS_PROBACKOFFICE.Services;
using System.Data;
using System.Globalization;

namespace IPS_PROBACKOFFICE;

public sealed class FeeRevenueByBankForm : Form
{
    private static readonly (string Header, string Key)[] Products =
    [
        ("DF", "DF 1M"),
        ("NDF", "NDF 1M"),
        ("NDF MAR", "NDF MAR"),
        ("NDF TOMMAR", "NDF TOMMAR"),
        ("BASIS MAR", "BASIS MAR"),
        ("BASIS TOMMAR", "BASIS TOMMAR")
    ];

    private readonly TradeRepository _tradeRepository;
    private readonly BrokerageFeeRepository _feeRepository;
    private readonly BusinessDaySettings _businessDaySettings;
    private readonly DateTimePicker _from = CreateDatePicker();
    private readonly DateTimePicker _to = CreateDatePicker();
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
    private readonly Button _excel = new() { Text = "엑셀 저장", AutoSize = true, Enabled = false };
    private readonly Label _status = new()
    {
        Text = "기간 선택 후 조회 버튼을 눌러주세요.",
        AutoSize = true,
        ForeColor = Color.DimGray,
        Margin = new Padding(12, 8, 3, 0)
    };
    private readonly DataGridView _grid = new();
    private readonly List<FeeRevenueRowKind> _rowKinds = [];
    private readonly Font _boldGridFont;
    private DataTable? _currentTable;
    private IReadOnlyList<TradeRow> _currentTrades = Array.Empty<TradeRow>();
    private IReadOnlyDictionary<string, string> _currentCurrencies =
        new Dictionary<string, string>();

    public FeeRevenueByBankForm(
        TradeRepository tradeRepository,
        BrokerageFeeRepository feeRepository,
        BusinessDaySettings businessDaySettings)
    {
        _tradeRepository = tradeRepository;
        _feeRepository = feeRepository;
        _businessDaySettings = businessDaySettings;

        Text = "매출 수수료 조회";
        Font = new Font("맑은 고딕", 10F);
        _boldGridFont = new Font(Font, FontStyle.Bold);
        BackColor = Color.White;
        Padding = new Padding(18);

        DateTime today = DateTime.Today;
        _from.Value = new DateTime(today.Year, today.Month, 1);
        _to.Value = today;

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 12)
        };
        toolbar.Controls.Add(LabelFor("조회기간"));
        toolbar.Controls.Add(_from);
        toolbar.Controls.Add(LabelFor("~", 4));
        toolbar.Controls.Add(_to);
        toolbar.Controls.Add(LabelFor("환율(USD/KRW)", 14));
        toolbar.Controls.Add(_exchangeRate);
        toolbar.Controls.Add(_search);
        toolbar.Controls.Add(_excel);
        toolbar.Controls.Add(_status);

        ConfigureGrid();
        Controls.Add(_grid);
        Controls.Add(toolbar);

        _search.Click += async (_, _) => await LoadAsync();
        _excel.Click += (_, _) => ExportExcel();
        _exchangeRate.ValueChanged += (_, _) =>
        {
            if (_currentTrades.Count > 0)
                BuildTable(_currentTrades, _currentCurrencies, _exchangeRate.Value);
        };
    }

    private static Label LabelFor(string text, int left = 3) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(left, 8, 3, 0)
    };

    private static DateTimePicker CreateDatePicker() => new()
    {
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "yyyy-MM-dd",
        Width = 130,
        ShowUpDown = false,
        DropDownAlign = LeftRightAlignment.Left
    };

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.BackgroundColor = Color.White;
        _grid.BorderStyle = BorderStyle.FixedSingle;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.RowHeadersVisible = false;
        _grid.AutoGenerateColumns = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.ColumnHeadersHeight = 51;
        _grid.RowTemplate.Height = 29;
        _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.WhiteSmoke;
        _grid.DefaultCellStyle.BackColor = Color.White;
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(220, 235, 252);
        _grid.DefaultCellStyle.SelectionForeColor = Color.Black;

        AddTextColumn("Bank", "Bank", 180, DataGridViewContentAlignment.MiddleLeft, frozen: true);
        AddTextColumn("Dealer Name", "Dealer Name", 160, DataGridViewContentAlignment.MiddleLeft, frozen: true);
        foreach ((string header, _) in Products)
            AddNumberColumn(header, header, 100);
        AddNumberColumn("Total", "Total", 115);
    }

    private void AddTextColumn(
        string name,
        string header,
        int width,
        DataGridViewContentAlignment alignment,
        bool frozen)
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = name,
            DataPropertyName = name,
            HeaderText = header,
            Width = width,
            MinimumWidth = width,
            Frozen = frozen,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = alignment }
        });
    }

    private void AddNumberColumn(string name, string header, int width)
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = name,
            DataPropertyName = name,
            HeaderText = header,
            Width = width,
            MinimumWidth = 80,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleRight,
                Format = "#,##0;[Red]-#,##0;-",
                NullValue = "-"
            }
        });
    }

    private async Task LoadAsync()
    {
        if (!_search.Enabled)
            return;

        DateTime selectedFrom = _from.Value.Date;
        DateTime selectedTo = _to.Value.Date;

        if (selectedFrom > selectedTo)
        {
            MessageBox.Show("조회 시작일은 종료일보다 늦을 수 없습니다.");
            return;
        }

        _search.Enabled = false;
        _excel.Enabled = false;
        _currentTable = null;
        _currentTrades = Array.Empty<TradeRow>();
        _currentCurrencies = new Dictionary<string, string>();
        _rowKinds.Clear();
        _grid.DataSource = null;
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
            BuildTable(_currentTrades, _currentCurrencies, _exchangeRate.Value);
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

    private void BuildTable(
        IReadOnlyList<TradeRow> trades,
        IReadOnlyDictionary<string, string> currencies,
        decimal exchangeRate)
    {
        DataTable table = CreateTable();
        _rowKinds.Clear();
        decimal[] grand = new decimal[Products.Length];

        foreach (IGrouping<string, TradeRow> bank in trades
                     .GroupBy(x => x.InstitutionName, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(x => x.Key))
        {
            decimal[] bankTotal = new decimal[Products.Length];
            foreach (IGrouping<string, TradeRow> dealer in bank
                         .GroupBy(x => string.IsNullOrWhiteSpace(x.Dealer) ? "(Unknown)" : x.Dealer,
                             StringComparer.OrdinalIgnoreCase)
                         .OrderBy(x => x.Key))
            {
                decimal[] values = SumProducts(dealer, currencies, exchangeRate);
                AddRow(table, bank.Key, dealer.Key, values);
                _rowKinds.Add(FeeRevenueRowKind.Detail);
                Add(values, bankTotal);
            }

            AddRow(table, bank.Key, "Total", bankTotal);
            _rowKinds.Add(FeeRevenueRowKind.BankTotal);
            Add(bankTotal, grand);
        }

        AddRow(table, "Grand Total", "Total", grand);
        _rowKinds.Add(FeeRevenueRowKind.GrandTotal);
        _currentTable = table;
        _grid.DataSource = table;
        ApplyRowStyles();
        _grid.ClearSelection();
    }

    private static DataTable CreateTable()
    {
        var table = new DataTable();
        table.Columns.Add("Bank", typeof(string));
        table.Columns.Add("Dealer Name", typeof(string));
        foreach ((string header, _) in Products)
            table.Columns.Add(header, typeof(decimal));
        table.Columns.Add("Total", typeof(decimal));
        return table;
    }

    private static decimal[] SumProducts(
        IEnumerable<TradeRow> trades,
        IReadOnlyDictionary<string, string> currencies,
        decimal exchangeRate)
    {
        decimal[] values = new decimal[Products.Length];
        foreach (TradeRow trade in trades)
        {
            int index = ProductIndex(trade.Product);
            if (index < 0)
                continue;

            bool usd = currencies.TryGetValue(trade.InstitutionCode, out string? currency)
                && currency.Equals("USD", StringComparison.OrdinalIgnoreCase);
            decimal fee = usd ? trade.BrokerageAmount * exchangeRate : trade.BrokerageAmount;
            values[index] += decimal.Round(fee, 0, MidpointRounding.AwayFromZero);
        }
        return values;
    }

    private static int ProductIndex(string product)
    {
        string normalized = Normalize(product);
        for (int i = 0; i < Products.Length; i++)
        {
            if (normalized == Products[i].Key)
                return i;
        }
        return -1;
    }

    private static string Normalize(string value) =>
        string.Join(' ', value.Trim().ToUpperInvariant().Replace('_', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static void Add(decimal[] source, decimal[] target)
    {
        for (int i = 0; i < source.Length; i++)
            target[i] += source[i];
    }

    private static void AddRow(DataTable table, string bank, string dealer, decimal[] values)
    {
        object[] cells = new object[Products.Length + 3];
        cells[0] = bank;
        cells[1] = dealer;
        decimal total = 0m;
        for (int i = 0; i < values.Length; i++)
        {
            cells[i + 2] = values[i];
            total += values[i];
        }
        cells[^1] = total;
        table.Rows.Add(cells);
    }

    private void ApplyRowStyles()
    {
        for (int i = 0; i < _grid.Rows.Count && i < _rowKinds.Count; i++)
        {
            DataGridViewRow row = _grid.Rows[i];
            FeeRevenueRowKind kind = _rowKinds[i];
            row.DefaultCellStyle.Font = kind == FeeRevenueRowKind.Detail ? Font : _boldGridFont;
            row.DefaultCellStyle.BackColor = kind switch
            {
                FeeRevenueRowKind.BankTotal => Color.FromArgb(238, 243, 249),
                FeeRevenueRowKind.GrandTotal => Color.FromArgb(217, 228, 241),
                _ => Color.White
            };
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _boldGridFont.Dispose();
        base.Dispose(disposing);
    }

    private void ExportExcel()
    {
        if (_currentTable is null)
            return;

        try
        {
            string outputPath = FeeRevenueWorkbookExporter.Save(
                _currentTable,
                _rowKinds,
                _from.Value.Date,
                _to.Value.Date,
                _exchangeRate.Value);
            MessageBox.Show(
                $"월별 계산서 문서의 '{FeeRevenueWorkbookExporter.DetailSheetName}' 시트 저장이 완료되었습니다.\n{outputPath}",
                "완료");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "엑셀 저장 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

}
