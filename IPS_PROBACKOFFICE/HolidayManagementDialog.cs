using IPS_PROBACKOFFICE.Data;

namespace IPS_PROBACKOFFICE;

public sealed class HolidayManagementDialog : Form
{
    private readonly FxValueDateRepository _repository;
    private readonly DateTimePicker _date = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd", Width = 140 };
    private readonly ComboBox _country = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly TextBox _name = new() { Width = 240 };
    private readonly CheckBox _isHoliday = new() { Text = "휴일로 적용", Checked = true, AutoSize = true };
    private readonly TextBox _memo = new() { Width = 240 };

    public HolidayManagementDialog(FxValueDateRepository repository, DateTime selectedDate)
    {
        _repository = repository;
        Text = "한국 · 미국 Banking 휴일 관리";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(430, 285);
        Font = new Font("맑은 고딕", 10F);

        _date.Value = selectedDate.Date;
        _country.Items.AddRange(["KR - 한국", "US - 미국 Banking"]);
        _country.SelectedIndex = 0;

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15),
            ColumnCount = 2,
            RowCount = 7
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(grid, 0, "날짜", _date);
        AddRow(grid, 1, "국가", _country);
        AddRow(grid, 2, "휴일명", _name);
        AddRow(grid, 3, "적용 구분", _isHoliday);
        AddRow(grid, 4, "메모", _memo);
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        var help = new Label
        {
            Text = "체크 해제 후 저장하면 기본 휴일도 영업일로 처리됩니다.",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 6, 0, 0)
        };
        grid.Controls.Add(help, 1, 5);

        var save = new Button { Text = "저장", Width = 85 };
        var delete = new Button { Text = "DB 등록 삭제", Width = 110 };
        var cancel = new Button { Text = "취소", Width = 75, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(delete);
        buttons.Controls.Add(save);
        grid.Controls.Add(buttons, 0, 6);
        grid.SetColumnSpan(buttons, 2);

        save.Click += async (_, _) => await SaveAsync();
        delete.Click += async (_, _) => await DeleteAsync();
        _isHoliday.CheckedChanged += (_, _) =>
        {
            if (!_isHoliday.Checked && string.IsNullOrWhiteSpace(_name.Text)) _name.Text = "영업일 예외";
        };

        AcceptButton = save;
        CancelButton = cancel;
        Controls.Add(grid);
    }

    private static void AddRow(TableLayoutPanel grid, int row, string label, Control control)
    {
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 37));
        grid.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 7, 0, 0) }, 0, row);
        grid.Controls.Add(control, 1, row);
    }

    private string CountryCode => _country.SelectedIndex == 1 ? "US" : "KR";

    private async Task SaveAsync()
    {
        string name = _name.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("휴일명을 입력해주세요.", "입력 확인", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try
        {
            await _repository.UpsertHolidayOverrideAsync(new HolidayOverrideRow(
                _date.Value.Date, CountryCode, name, _isHoliday.Checked, _memo.Text.Trim()));
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "휴일 저장 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task DeleteAsync()
    {
        if (MessageBox.Show(
                $"{_date.Value:yyyy-MM-dd} {CountryCode}의 DB 등록값을 삭제할까요?\n프로그램 기본 휴일은 다시 적용됩니다.",
                "휴일 등록 삭제", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        try
        {
            await _repository.DeleteHolidayOverrideAsync(_date.Value.Date, CountryCode);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "휴일 삭제 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
