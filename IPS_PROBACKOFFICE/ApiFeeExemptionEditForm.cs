using IPS_PROBACKOFFICE.Models;

namespace IPS_PROBACKOFFICE;

public sealed class ApiFeeExemptionEditForm : Form
{
    private readonly ComboBox _institution = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    private readonly TextBox _traderId = new() { Width = 300 };
    private readonly DateTimePicker _startDate = new() { Format = DateTimePickerFormat.Short, Width = 140 };
    private readonly DateTimePicker _endDate = new() { Format = DateTimePickerFormat.Short, Width = 140 };
    private readonly CheckBox _isActive = new() { Text = "사용", Checked = true, AutoSize = true };
    private readonly TextBox _remarks = new() { Width = 420, Height = 70, Multiline = true, ScrollBars = ScrollBars.Vertical };

    public ApiFeeExemptionRow? Result { get; private set; }

    public ApiFeeExemptionEditForm(
        IReadOnlyList<ApiFeeExemptionInstitution> institutions,
        ApiFeeExemptionRow? source = null)
    {
        Text = source is null ? "API 수수료 면제 등록" : "API 수수료 면제 수정";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(570, 365);
        Font = new Font("맑은 고딕", 10F);

        foreach (ApiFeeExemptionInstitution item in institutions)
            _institution.Items.Add(item);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(22, 20, 22, 8)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(table, "기관", _institution);
        AddRow(table, "딜러 ID", _traderId);
        AddRow(table, "적용 시작일", _startDate);
        AddRow(table, "적용 종료일", _endDate);
        AddRow(table, "사용 여부", _isActive);
        AddRow(table, "비고", _remarks);

        var save = new Button { Text = "저장", Width = 90, Height = 32 };
        var cancel = new Button { Text = "취소", Width = 90, Height = 32, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => Save(source?.Id ?? 0);
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 58,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12)
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);
        Controls.Add(buttons);
        Controls.Add(table);
        CancelButton = cancel;

        DateTime today = DateTime.Today;
        _startDate.Value = source?.StartDate ?? today;
        _endDate.Value = source?.EndDateInclusive ?? today;
        _traderId.Text = source?.TraderId ?? string.Empty;
        _isActive.Checked = source?.IsActive ?? true;
        _remarks.Text = source?.Remarks ?? string.Empty;

        if (source is not null)
        {
            int index = institutions.ToList().FindIndex(x =>
                string.Equals(x.Code, source.InstitutionCode, StringComparison.OrdinalIgnoreCase));
            _institution.SelectedIndex = index;
        }
        else if (_institution.Items.Count > 0)
        {
            _institution.SelectedIndex = 0;
        }
    }

    private static void AddRow(TableLayoutPanel table, string label, Control control)
    {
        int row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Margin = new Padding(0, 9, 8, 12)
        }, 0, row);
        control.Margin = new Padding(0, 4, 0, 12);
        table.Controls.Add(control, 1, row);
    }

    private void Save(long id)
    {
        if (_institution.SelectedItem is not ApiFeeExemptionInstitution institution)
        {
            MessageBox.Show("기관을 선택해 주세요.", "입력 확인",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(_traderId.Text))
        {
            MessageBox.Show("딜러 ID를 입력해 주세요.", "입력 확인",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _traderId.Focus();
            return;
        }
        if (_endDate.Value.Date < _startDate.Value.Date)
        {
            MessageBox.Show("적용 종료일은 시작일보다 빠를 수 없습니다.", "입력 확인",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Result = new ApiFeeExemptionRow
        {
            Id = id,
            InstitutionCode = institution.Code,
            InstitutionName = institution.Name,
            TraderId = _traderId.Text.Trim(),
            StartDate = _startDate.Value.Date,
            EndDateInclusive = _endDate.Value.Date,
            IsActive = _isActive.Checked,
            Remarks = string.IsNullOrWhiteSpace(_remarks.Text) ? null : _remarks.Text.Trim()
        };
        DialogResult = DialogResult.OK;
        Close();
    }
}
