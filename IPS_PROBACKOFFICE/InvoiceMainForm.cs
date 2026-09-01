using System.ComponentModel;
using System.Diagnostics;
using InvoiceReportPrinter.WinForms.Data;
using InvoiceReportPrinter.WinForms.Models;
using InvoiceReportPrinter.WinForms.Services;
using IPS_PROBACKOFFICE;

namespace InvoiceReportPrinter.WinForms;

public sealed class MainForm : Form
{
    private readonly TradeRepository _repository;
    private readonly InvoiceService _invoiceService;
    private readonly BusinessDaySettings _businessDaySettings;
    private readonly ComboBox _cmbDataSource = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 190
    };
    private readonly ComboBox _cmbInstitution = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
    private readonly DateTimePicker _dtpMonth = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM", ShowUpDown = true, Width = 110 };
    private readonly Button _btnSearch = new() { Text = "조회", Width = 90 };
    private readonly Button _btnInvoice = new() { Text = "Invoice 생성", Width = 130, Enabled = false };
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoGenerateColumns = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
    private readonly ToolStripStatusLabel _status = new() { Text = "대기" };
    private readonly BindingList<TradeRow> _rows = [];

    public MainForm(
        TradeRepository repository,
        InvoiceService invoiceService,
        BusinessDaySettings businessDaySettings)
    {
        _repository = repository;
        _invoiceService = invoiceService;
        _businessDaySettings = businessDaySettings;
        Text = "InvoiceReportPrinter";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1100, 650);
        Width = 1350;
        Height = 760;
        BuildUi();
        _cmbDataSource.Items.AddRange(
        [
            "CSV 입력 데이터 (trades)",
            "거래 뷰 직접 조회"
        ]);
        // 운영 기본값은 CSV 적재 테이블이 아니라 실거래 뷰 직접 조회이다.
        _cmbDataSource.SelectedIndex = 1;
        Shown += async (_, _) => await LoadInstitutionsAsync();
        _btnSearch.Click += async (_, _) => await SearchAsync();
        _btnInvoice.Click += (_, _) => CreateInvoice();
    }

    private void BuildUi()
    {
        var filterPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 56, Padding = new Padding(10), WrapContents = false };
        filterPanel.Controls.Add(new Label { Text = "데이터 원본", AutoSize = true, Margin = new Padding(0, 8, 6, 0) });
        filterPanel.Controls.Add(_cmbDataSource);
        filterPanel.Controls.Add(new Label { Text = "기관", AutoSize = true, Margin = new Padding(0, 8, 6, 0) });
        filterPanel.Controls.Add(_cmbInstitution);
        filterPanel.Controls.Add(new Label { Text = "조회월", AutoSize = true, Margin = new Padding(18, 8, 6, 0) });
        filterPanel.Controls.Add(_dtpMonth);
        filterPanel.Controls.Add(_btnSearch);
        filterPanel.Controls.Add(_btnInvoice);

        ConfigureGridColumns();
        _grid.DataSource = _rows;
        var statusStrip = new StatusStrip();
        statusStrip.Items.Add(_status);
        Controls.Add(_grid);
        Controls.Add(filterPanel);
        Controls.Add(statusStrip);
    }

    private void ConfigureGridColumns()
    {
        AddText("TradeDate", "TRADE DATE", 95, "yyyy-MM-dd");
        AddText("RefNumber", "REF NUMBER", 130);
        AddText("Direction", "DIRECTION", 80);
        AddText("Counterparty", "COUNTERPARTY", 140);
        AddText("Tenor", "TENOR", 90);
        AddText("ExchangeRate", "EX.RATE", 95, "N7");
        AddText("Amount", "AMOUNT", 125, "N2");
        AddText("ValueDate", "VALUE DATE", 95, "yyyy-MM-dd");
        AddText("Dealer", "DEALER", 120);
        AddText("Brokerage", "BROKERAGE", 110);
        AddText("Product", "PRODUCT", 110);
        AddText("IsSwitch", "SWITCH", 70);
    }

    private void AddText(string property, string header, int width, string? format = null)
    {
        var column = new DataGridViewTextBoxColumn { DataPropertyName = property, HeaderText = header, Width = width };
        if (!string.IsNullOrWhiteSpace(format)) column.DefaultCellStyle.Format = format;
        _grid.Columns.Add(column);
    }

    private async Task LoadInstitutionsAsync()
    {
        await RunBusyAsync("기관정보 조회 중...", async () =>
        {
            var institutions = await _repository.GetInstitutionsAsync();
            _cmbInstitution.DataSource = institutions.ToList();
            _cmbInstitution.DisplayMember = nameof(Institution.Name);
            _status.Text = $"기관 {institutions.Count}개 로드 완료";
        });
    }

    private async Task SearchAsync()
    {
        if (_cmbInstitution.SelectedItem is not Institution institution)
        {
            MessageBox.Show("기관을 선택하세요.", "확인", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        await RunBusyAsync("거래 조회 중...", async () =>
        {
            bool switchOnly = institution.Name.Contains("KB", StringComparison.OrdinalIgnoreCase)
                           || institution.Name.Contains("HANA", StringComparison.OrdinalIgnoreCase);
            bool useTradingView = _cmbDataSource.SelectedIndex == 1;
            (DateTime from, DateTime to) =
                _businessDaySettings.GetInvoiceMonthRange(_dtpMonth.Value);
            var trades = useTradingView
                ? await _repository.GetTradesFromViewAsync(
                    institution,
                    from,
                    to)
                : await _repository.GetTradesAsync(
                    institution,
                    from,
                    to,
                    switchOnly);
            _rows.RaiseListChangedEvents = false;
            _rows.Clear();
            foreach (var trade in trades) _rows.Add(trade);
            _rows.RaiseListChangedEvents = true;
            _rows.ResetBindings();
            _btnInvoice.Enabled = _rows.Count > 0;
            string sourceName = useTradingView
                ? "v_trading_report"
                : "trades";
            _status.Text = _rows.Count == 0
                ? $"{sourceName}: 조회된 거래가 없습니다."
                : $"{sourceName}: {_rows.Count:N0}건 조회 완료";
            if (_rows.Count == 0) MessageBox.Show("조회된 거래가 없습니다.", "조회", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }

    private void CreateInvoice()
    {
        if (_cmbInstitution.SelectedItem is not Institution institution || _rows.Count == 0)
        {
            MessageBox.Show("먼저 거래를 조회하세요.", "확인", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            string path = _invoiceService.CreateInvoice(institution, _dtpMonth.Value, _rows.ToList());
            _status.Text = $"Invoice 생성 완료: {Path.GetFileName(path)}";
            var result = MessageBox.Show($"Invoice가 생성되었습니다.\n\n{path}\n\n파일을 여시겠습니까?", "완료", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (result == DialogResult.Yes)
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _status.Text = "Invoice 생성 실패";
            MessageBox.Show(ex.Message, "Invoice 생성 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task RunBusyAsync(string status, Func<Task> action)
    {
        try
        {
            UseWaitCursor = true;
            _btnSearch.Enabled = false;
            _btnInvoice.Enabled = false;
            _status.Text = status;
            await action();
        }
        catch (Exception ex)
        {
            _status.Text = "오류";
            MessageBox.Show(ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
            _btnSearch.Enabled = true;
            _btnInvoice.Enabled = _rows.Count > 0;
        }
    }
}
