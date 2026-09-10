using InvoiceReportPrinter.WinForms.Data;
using InvoiceReportPrinter.WinForms.Models;
using IPS_PROBACKOFFICE.Data;
using IPS_PROBACKOFFICE.Models;
using System.ComponentModel;
using System.Globalization;

namespace IPS_PROBACKOFFICE;

public sealed class DailyFeeTotalForm : Form
{
    private readonly TradeRepository _tradeRepository;
    private readonly BrokerageFeeRepository _feeRepository;
    private readonly BusinessDaySettings _businessDaySettings;
    private readonly BindingList<DailyFeeTotalRow> _rows = [];
    private readonly RadioButton _monthly = new() { Text = "월별", AutoSize = true };
    private readonly RadioButton _daily = new() { Text = "일별", AutoSize = true, Checked = true };
    private readonly Label _periodLabel = LabelFor("조회월");
    private readonly DateTimePicker _month = new()
    {
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "yyyy-MM",
        ShowUpDown = true,
        Width = 110
    };
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
    private readonly CheckBox _showTrend = new() { Text = "추세 표 보기", AutoSize = true };
    private readonly Label _status = new()
    {
        Text = "월을 선택해 주세요.",
        AutoSize = true,
        ForeColor = Color.DimGray,
        Margin = new Padding(12, 8, 3, 0)
    };
    private readonly DataGridView _grid = new();
    private readonly FeeTrendChart _chart = new();
    private readonly ComboBox _chartCurrency = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 125 };
    private readonly Font _totalFont;
    private List<TradeRow> _trades = [];
    private IReadOnlyDictionary<string, string> _currencies = new Dictionary<string, string>();

    public DailyFeeTotalForm(
        TradeRepository tradeRepository,
        BrokerageFeeRepository feeRepository,
        BusinessDaySettings businessDaySettings)
    {
        _tradeRepository = tradeRepository;
        _feeRepository = feeRepository;
        _businessDaySettings = businessDaySettings;

        Text = "월/일별 수수료";
        Font = new Font("맑은 고딕", 10F);
        _totalFont = new Font(Font, FontStyle.Bold);
        BackColor = Color.White;
        Padding = new Padding(18);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 12)
        };
        toolbar.Controls.Add(_monthly);
        toolbar.Controls.Add(_daily);
        toolbar.Controls.Add(_periodLabel);
        toolbar.Controls.Add(_month);
        toolbar.Controls.Add(LabelFor("환율(USD/KRW)", 14));
        toolbar.Controls.Add(_exchangeRate);
        toolbar.Controls.Add(_search);
        toolbar.Controls.Add(_showTrend);
        toolbar.Controls.Add(_status);

        ConfigureGrid();
        _grid.DataSource = _rows;
        var split = new SplitContainer
        {
            Size = new Size(1200, 600), Dock = DockStyle.Fill,
            SplitterDistance = 720, SplitterWidth = 10,
            Panel1MinSize = 300, Panel2MinSize = 240,
            Panel2Collapsed = !_showTrend.Checked
        };
        var chartToolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        chartToolbar.Controls.Add(LabelFor("수수료 추세"));
        _chartCurrency.Items.AddRange(new object[] { "KRW", "USD", "KRW 환산" });
        _chartCurrency.SelectedIndex = 0;
        chartToolbar.Controls.Add(_chartCurrency);
        split.Panel1.Controls.Add(_grid);
        split.Panel2.Controls.Add(_chart);
        split.Panel2.Controls.Add(chartToolbar);
        Controls.Add(split);
        Controls.Add(toolbar);
        _showTrend.CheckedChanged += (_, _) => split.Panel2Collapsed = !_showTrend.Checked;
        _chartCurrency.SelectedIndexChanged += (_, _) => UpdateChart();
        Shown += (_, _) =>
        {
            int available = split.ClientSize.Width - split.SplitterWidth;
            if (available >= split.Panel1MinSize + split.Panel2MinSize)
                split.SplitterDistance = Math.Clamp((int)(available * .6), split.Panel1MinSize, available - split.Panel2MinSize);
        };

        _search.Click += async (_, _) => await LoadAsync();
        _monthly.CheckedChanged += async (_, _) =>
        {
            _month.CustomFormat = _monthly.Checked ? "yyyy" : "yyyy-MM";
            _periodLabel.Text = UiLanguage.Text(_monthly.Checked ? "조회연도" : "조회월");
            UpdateColumns();
            await LoadAsync();
        };
        _month.ValueChanged += async (_, _) => await LoadAsync();
        _exchangeRate.ValueChanged += (_, _) => BuildRows();
        _businessDaySettings.Changed += async (_, _) => await LoadAsync();
        Shown += async (_, _) => await LoadAsync();
    }

    private static Label LabelFor(string text, int left = 3) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(left, 8, 3, 0)
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
        _grid.ColumnHeadersHeight = 42;
        _grid.RowTemplate.Height = 30;
        _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.WhiteSmoke;
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(220, 235, 252);
        _grid.DefaultCellStyle.SelectionForeColor = Color.Black;

        AddTextColumn(nameof(DailyFeeTotalRow.Date), "거래일", 150, DataGridViewContentAlignment.MiddleCenter);
        AddNumberColumn(nameof(DailyFeeTotalRow.BrokerageUsd), "중개 수수료 USD", 140);
        AddNumberColumn(nameof(DailyFeeTotalRow.BrokerageKrw), "중개 수수료 KRW", 145);
        AddNumberColumn(nameof(DailyFeeTotalRow.SwitchUsd), "스위치 수수료 USD", 145);
        AddNumberColumn(nameof(DailyFeeTotalRow.SwitchKrw), "스위치 수수료 KRW", 150);
        AddNumberColumn(nameof(DailyFeeTotalRow.TotalUsd), "총 수수료 USD", 150);
        AddNumberColumn(nameof(DailyFeeTotalRow.NativeTotalKrw), "총 수수료 KRW", 150);
        AddNumberColumn(nameof(DailyFeeTotalRow.TotalKrw), "총 수수료(KRW 환산)", 175);
        UpdateColumns();
    }

    private void UpdateColumns()
    {
        bool monthly = _monthly.Checked;
        _grid.Columns[nameof(DailyFeeTotalRow.Date)]!.HeaderText = UiLanguage.Text(monthly ? "월" : "거래일");
        foreach (string name in new[] { nameof(DailyFeeTotalRow.BrokerageUsd), nameof(DailyFeeTotalRow.BrokerageKrw),
                     nameof(DailyFeeTotalRow.SwitchUsd), nameof(DailyFeeTotalRow.SwitchKrw) })
            _grid.Columns[name]!.Visible = !monthly;
        _grid.Columns[nameof(DailyFeeTotalRow.TotalUsd)]!.Visible = monthly;
        _grid.Columns[nameof(DailyFeeTotalRow.NativeTotalKrw)]!.Visible = monthly;
    }

    private void AddTextColumn(string name, string title, int width, DataGridViewContentAlignment alignment) =>
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = name,
            DataPropertyName = name,
            HeaderText = title,
            Width = width,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = alignment }
        });

    private void AddNumberColumn(string name, string title, int width) =>
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = name,
            DataPropertyName = name,
            HeaderText = title,
            Width = width,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleRight,
                Format = "#,##0;[Red]-#,##0;-"
            }
        });

    private async Task LoadAsync()
    {
        if (!_search.Enabled)
            return;

        _search.Enabled = false;
        _monthly.Enabled = _daily.Enabled = _month.Enabled = _exchangeRate.Enabled = false;
        _status.Text = "조회 중...";
        _chart.SetData([], "", "조회 중...");
        try
        {
            DateTime selectedMonth = new(_month.Value.Year, _monthly.Checked ? 1 : _month.Value.Month, 1);
            DateTime lastDate = selectedMonth.AddMonths(_monthly.Checked ? 12 : 1).AddDays(-1);
            DateTime from = _businessDaySettings.GetDailyRange(selectedMonth).Start;
            DateTime to = _businessDaySettings.GetDailyRange(lastDate).End;
            IReadOnlyList<Institution> institutions = await _tradeRepository.GetInstitutionsAsync();
            IReadOnlyList<BrokerageFeeRow> feeSchedules = await _feeRepository.GetAllAsync();

            using var queryGate = new SemaphoreSlim(4);
            Task<IReadOnlyList<TradeRow>>[] tasks = institutions.Select(async institution =>
            {
                await queryGate.WaitAsync();
                try
                {
                    return await _tradeRepository.GetTradesFromViewAsync(institution, from, to);
                }
                finally
                {
                    queryGate.Release();
                }
            }).ToArray();

            IReadOnlyList<TradeRow>[] results = await Task.WhenAll(tasks);
            _trades = results.SelectMany(x => x).ToList();
            _currencies = feeSchedules
                .GroupBy(x => x.InstCode.ToString(CultureInfo.InvariantCulture))
                .ToDictionary(
                    x => x.Key,
                    x => x.First().CurrencyCode?.Trim().ToUpperInvariant() ?? "KRW",
                    StringComparer.OrdinalIgnoreCase);

            BuildRows();
            _status.Text = _monthly.Checked
                ? $"{selectedMonth:yyyy} · 1~12월 월별 합계 · {_trades.Count:N0}건"
                : $"{selectedMonth:yyyy-MM} · 일별 {from:HH:mm}~익일 {to:HH:mm} · {_trades.Count:N0}건";
        }
        catch (Exception ex)
        {
            _trades.Clear();
            _rows.Clear();
            _chart.SetData([], "", "조회 실패 — 다시 조회해 주세요.");
            _status.Text = "조회 실패";
            MessageBox.Show(ex.Message, "월/일별 수수료 조회 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _search.Enabled = true;
            _monthly.Enabled = _daily.Enabled = _month.Enabled = _exchangeRate.Enabled = true;
        }
    }

    private void BuildRows()
    {
        _rows.RaiseListChangedEvents = false;
        _rows.Clear();

        DateTime firstDate = new(_month.Value.Year, _monthly.Checked ? 1 : _month.Value.Month, 1);
        int count = _monthly.Checked ? 12 : DateTime.DaysInMonth(firstDate.Year, firstDate.Month);
        for (int index = 0; index < count; index++)
        {
            DateTime date = _monthly.Checked ? firstDate.AddMonths(index) : firstDate.AddDays(index);
            if (!_monthly.Checked && date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                continue;
            DateTime lastDate = _monthly.Checked ? date.AddMonths(1).AddDays(-1) : date;
            DateTime from = _businessDaySettings.GetDailyRange(date).Start;
            DateTime to = _businessDaySettings.GetDailyRange(lastDate).End;
            IEnumerable<TradeRow> dailyTrades = _trades.Where(
                x => x.TradeDate >= from && x.TradeDate < to);
            _rows.Add(CreateRow(date.ToString(_monthly.Checked ? "yyyy-MM" : "yyyy-MM-dd (ddd)"), dailyTrades)
                with { PeriodDate = date });
        }

        _rows.Add(CreateRow("전체 합계", _trades));
        _rows.RaiseListChangedEvents = true;
        _rows.ResetBindings();

        if (_grid.Rows.Count > 0)
        {
            DataGridViewRow totalRow = _grid.Rows[^1];
            totalRow.DefaultCellStyle.Font = _totalFont;
            totalRow.DefaultCellStyle.BackColor = Color.FromArgb(217, 228, 241);
        }
        _grid.ClearSelection();
        UpdateChart();
    }

    private void UpdateChart()
    {
        string unit = _chartCurrency.SelectedIndex == 1 ? "USD" : "KRW";
        if (_chartCurrency.SelectedIndex == 2 && _exchangeRate.Value <= 0)
        {
            _chart.SetData([], unit, "KRW 환산 추세를 보려면 환율을 입력해 주세요.");
            return;
        }
        DateTime today = DateTime.Today;
        DateTime firstDate = new(_month.Value.Year, _monthly.Checked ? 1 : _month.Value.Month, 1);
        var data = _rows.Take(Math.Max(0, _rows.Count - 1))
            .Where(row => row.PeriodDate <= today)
            .Select(row => (row.Date, _chartCurrency.SelectedIndex switch
            {
                1 => row.TotalUsd,
                2 => row.TotalKrw,
                _ => row.NativeTotalKrw
            })).ToArray();
        _chart.SetData(data, unit, data.Length == 0
            ? (firstDate > today ? "미래 기간은 차트에 표시하지 않습니다." : "조회 후 추세가 표시됩니다.")
            : null);
    }

    private DailyFeeTotalRow CreateRow(string date, IEnumerable<TradeRow> trades)
    {
        decimal brokerageUsd = 0m;
        decimal brokerageKrw = 0m;
        decimal switchUsd = 0m;
        decimal switchKrw = 0m;
        foreach (TradeRow trade in trades)
        {
            bool usd = CurrencyOf(trade).Equals("USD", StringComparison.OrdinalIgnoreCase);
            if (trade.IsSwitch)
            {
                if (usd) switchUsd += trade.BrokerageAmount;
                else switchKrw += trade.BrokerageAmount;
            }
            else
            {
                if (usd) brokerageUsd += trade.BrokerageAmount;
                else brokerageKrw += trade.BrokerageAmount;
            }
        }

        decimal totalKrw = decimal.Round(
            (brokerageUsd + switchUsd) * _exchangeRate.Value + brokerageKrw + switchKrw,
            0,
            MidpointRounding.AwayFromZero);
        return new DailyFeeTotalRow(date, brokerageUsd, brokerageKrw, switchUsd, switchKrw, totalKrw);
    }

    private string CurrencyOf(TradeRow trade) =>
        _currencies.TryGetValue(trade.InstitutionCode, out string? currency)
            && !string.IsNullOrWhiteSpace(currency)
                ? currency
                : "KRW";

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _totalFont.Dispose();
        base.Dispose(disposing);
    }

    private sealed record DailyFeeTotalRow(
        string Date,
        decimal BrokerageUsd,
        decimal BrokerageKrw,
        decimal SwitchUsd,
        decimal SwitchKrw,
        decimal TotalKrw)
    {
        public DateTime PeriodDate { get; init; }
        public decimal TotalUsd => BrokerageUsd + SwitchUsd;
        public decimal NativeTotalKrw => BrokerageKrw + SwitchKrw;
    }
}
