using System.Data;
using System.Text;
using Microsoft.VisualBasic.FileIO;
using IPS_PROBACKOFFICE.Data;

namespace IPS_PROBACKOFFICE;

public sealed class TradeImportForm : Form
{
    private readonly TradeImportRepository _repository;
    private readonly TextBox _filePath = new() { ReadOnly = true, Dock = DockStyle.Fill, Anchor = AnchorStyles.Left | AnchorStyles.Right };
    private readonly Button _selectButton = new() { Text = "CSV 선택", AutoSize = true, MinimumSize = new Size(110, 36), Anchor = AnchorStyles.None };
    private readonly Button _importButton = new() { Text = "TRADES 입력", AutoSize = true, MinimumSize = new Size(130, 36), Anchor = AnchorStyles.None, Enabled = false };
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells };
    private readonly ToolStripStatusLabel _status = new() { Text = "CSV 파일을 선택하세요." };
    private DataTable? _data;

    public TradeImportForm(TradeImportRepository repository)
    {
        _repository = repository;
        Font = new Font("맑은 고딕", 10F);
        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(10), ColumnCount = 4, RowCount = 1
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        top.Controls.Add(new Label { Text = "거래 CSV", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 3, 8, 3) }, 0, 0);
        top.Controls.Add(_filePath, 1, 0);
        top.Controls.Add(_selectButton, 2, 0);
        top.Controls.Add(_importButton, 3, 0);
        var strip = new StatusStrip();
        strip.Items.Add(_status);
        Controls.Add(_grid);
        Controls.Add(top);
        Controls.Add(strip);
        _selectButton.Click += (_, _) => SelectCsv();
        _importButton.Click += async (_, _) => await ImportAsync();
    }

    private void SelectCsv()
    {
        using var dialog = new OpenFileDialog { Filter = "거래 CSV (*.csv)|*.csv", Title = "매일 내려받은 거래 CSV 선택" };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        try
        {
            _data = ReadCsv(dialog.FileName);
            _filePath.Text = dialog.FileName;
            _grid.DataSource = _data;
            _importButton.Enabled = _data.Rows.Count > 0;
            _status.Text = $"{_data.Rows.Count:N0}건 미리보기 — 확인 후 TRADES 입력을 누르세요.";
        }
        catch (Exception ex)
        {
            _data = null;
            _importButton.Enabled = false;
            MessageBox.Show(ex.Message, "CSV 읽기 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task ImportAsync()
    {
        if (_data is null || _data.Rows.Count == 0) return;
        if (MessageBox.Show($"미리보기 {_data.Rows.Count:N0}건을 trades 테이블에 입력할까요?\n이미 입력된 거래는 자동 제외됩니다.", "입력 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            UseWaitCursor = true;
            _selectButton.Enabled = _importButton.Enabled = false;
            _status.Text = "TRADES 입력 중...";
            ImportResult result = await _repository.ImportAsync(_data);
            _grid.Refresh();
            _status.Text = $"완료: 신규 {result.Inserted:N0}건 / 중복 제외 {result.Duplicates:N0}건 / 오류 {result.Errors:N0}건";
            MessageBox.Show(_status.Text, "TRADES 입력 결과", MessageBoxButtons.OK, result.Errors == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            _status.Text = "입력 실패";
            MessageBox.Show(ex.Message, "TRADES 입력 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
            _selectButton.Enabled = true;
            _importButton.Enabled = _data is { Rows.Count: > 0 };
        }
    }

    private static DataTable ReadCsv(string path)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var parser = new TextFieldParser(path, Encoding.UTF8, true) { TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false };
        parser.SetDelimiters(",");
        string[] headers = parser.ReadFields() ?? throw new InvalidOperationException("CSV 헤더가 없습니다.");
        if (headers.Length == 0) throw new InvalidOperationException("CSV 헤더가 없습니다.");
        var table = new DataTable();
        foreach (string header in headers) table.Columns.Add(header.Trim().TrimStart('\uFEFF'));
        table.Columns.Add("처리결과");
        while (!parser.EndOfData)
        {
            string[] fields = parser.ReadFields() ?? [];
            if (fields.Length != headers.Length) throw new InvalidOperationException($"CSV {parser.LineNumber}행의 컬럼 수가 다릅니다. (기대 {headers.Length}, 실제 {fields.Length})");
            object[] values = new object[headers.Length + 1];
            for (int i = 0; i < fields.Length; i++) values[i] = fields[i].Trim();
            values[^1] = "입력 대기";
            table.Rows.Add(values);
        }
        return table;
    }
}
