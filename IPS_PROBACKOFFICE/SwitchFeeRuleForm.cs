using System.ComponentModel;
using IPS_PROBACKOFFICE.Data;
using IPS_PROBACKOFFICE.Models;

namespace IPS_PROBACKOFFICE;

public sealed class SwitchFeeRuleForm : Form
{
    private readonly SwitchFeeRuleRepository _repository;
    private readonly BindingList<SwitchFeeRuleRow> _rows = [];
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoGenerateColumns = false,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        BackgroundColor = Color.White,
        BorderStyle = BorderStyle.None,
        EnableHeadersVisualStyles = false
    };
    private readonly Button _refresh = new() { Text = "새로고침", Width = 100, Height = 30 };
    private readonly Button _edit = new() { Text = "수정", Width = 90, Height = 30 };
    private readonly Button _save = new() { Text = "저장", Width = 90, Height = 30, Enabled = false };
    private readonly Button _cancel = new() { Text = "취소", Width = 90, Height = 30, Enabled = false };
    private readonly CheckBox _activeOnly = new()
    {
        Text = "사용 중 규칙만",
        Checked = true,
        AutoSize = true,
        Margin = new Padding(14, 7, 0, 0)
    };
    private readonly Label _status = new() { AutoSize = true, Margin = new Padding(14, 8, 0, 0) };
    private bool _editing;

    public SwitchFeeRuleForm(SwitchFeeRuleRepository repository)
    {
        _repository = repository;
        Text = "스위치 수수료";
        BuildUi();
        Shown += async (_, _) => await LoadAsync();
        _refresh.Click += async (_, _) => await LoadAsync();
        _activeOnly.CheckedChanged += (_, _) => ApplyFilter();
        _edit.Click += (_, _) => SetEditMode(true);
        _cancel.Click += async (_, _) => await CancelAsync();
        _save.Click += async (_, _) => await SaveAsync();
        _grid.DataError += (_, e) =>
        {
            e.ThrowException = false;
            MessageBox.Show("수수료와 적용 종료일의 입력값을 확인해 주세요.", "입력 오류",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        };
    }

    private void BuildUi()
    {
        var title = new Label
        {
            Text = "스위치 수수료 조회 및 수정",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 16F, FontStyle.Bold),
            ForeColor = Color.FromArgb(31, 78, 121),
            Margin = new Padding(0, 4, 24, 0)
        };
        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 62,
            Padding = new Padding(14, 12, 14, 8),
            WrapContents = false,
            BackColor = Color.FromArgb(245, 248, 252)
        };
        top.Controls.Add(title);
        top.Controls.Add(_refresh);
        top.Controls.Add(_activeOnly);
        top.Controls.Add(_edit);
        top.Controls.Add(_save);
        top.Controls.Add(_cancel);
        top.Controls.Add(_status);

        AddText(nameof(SwitchFeeRuleRow.InstCode), "기관코드", 90, DataGridViewContentAlignment.MiddleCenter);
        AddText(nameof(SwitchFeeRuleRow.InstitutionName), "기관", 160, DataGridViewContentAlignment.MiddleLeft);
        AddText(nameof(SwitchFeeRuleRow.ProductCode), "상품", 125, DataGridViewContentAlignment.MiddleCenter);
        AddText(nameof(SwitchFeeRuleRow.Direction), "방향", 80, DataGridViewContentAlignment.MiddleCenter);
        AddText(nameof(SwitchFeeRuleRow.LegType), "LEG", 80, DataGridViewContentAlignment.MiddleCenter);
        AddText(nameof(SwitchFeeRuleRow.FeePerMillion), "USD 1M당 수수료", 145, DataGridViewContentAlignment.MiddleRight, "N0");
        AddText(nameof(SwitchFeeRuleRow.EffectiveFrom), "적용 시작일", 115, DataGridViewContentAlignment.MiddleCenter, "yyyy-MM-dd");
        AddText(nameof(SwitchFeeRuleRow.EffectiveTo), "적용 종료일", 115, DataGridViewContentAlignment.MiddleCenter, "yyyy-MM-dd");
        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = nameof(SwitchFeeRuleRow.IsActive),
            DataPropertyName = nameof(SwitchFeeRuleRow.IsActive),
            HeaderText = "사용",
            Width = 65,
            SortMode = DataGridViewColumnSortMode.Automatic
        });

        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            Alignment = DataGridViewContentAlignment.MiddleCenter,
            BackColor = Color.FromArgb(31, 78, 121),
            ForeColor = Color.White,
            Font = new Font(_grid.Font, FontStyle.Bold),
            SelectionBackColor = Color.FromArgb(31, 78, 121),
            SelectionForeColor = Color.White
        };
        _grid.ColumnHeadersHeight = 38;
        _grid.RowTemplate.Height = 34;
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(246, 249, 252);
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(214, 228, 242);
        _grid.DefaultCellStyle.SelectionForeColor = Color.Black;
        _grid.DataSource = _rows;

        Controls.Add(_grid);
        Controls.Add(top);
    }

    private void AddText(string property, string header, int width,
        DataGridViewContentAlignment alignment, string? format = null)
    {
        var column = new DataGridViewTextBoxColumn
        {
            Name = property,
            DataPropertyName = property,
            HeaderText = header,
            Width = width,
            SortMode = DataGridViewColumnSortMode.Automatic,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = alignment }
        };
        if (!string.IsNullOrWhiteSpace(format))
            column.DefaultCellStyle.Format = format;
        _grid.Columns.Add(column);
    }

    private async Task LoadAsync()
    {
        try
        {
            UseWaitCursor = true;
            _refresh.Enabled = false;
            _status.Text = "조회 중...";
            IReadOnlyList<SwitchFeeRuleRow> rules = await _repository.GetAllAsync();
            _allRows = rules.ToList();
            ApplyFilter();
            SetEditMode(false);
            _status.Text = $"규칙 {_rows.Count:N0}건 · 최종 조회 {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        }
        catch (Exception ex)
        {
            _status.Text = "조회 오류";
            MessageBox.Show("스위치 수수료 조회 중 오류가 발생했습니다.\n\n" + ex.Message,
                "조회 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
            _refresh.Enabled = !_editing;
        }
    }

    private List<SwitchFeeRuleRow> _allRows = [];

    private void ApplyFilter()
    {
        if (_editing)
            return;
        IEnumerable<SwitchFeeRuleRow> source = _activeOnly.Checked
            ? _allRows.Where(x => x.IsActive)
            : _allRows;
        _rows.RaiseListChangedEvents = false;
        _rows.Clear();
        foreach (SwitchFeeRuleRow row in source)
            _rows.Add(row);
        _rows.RaiseListChangedEvents = true;
        _rows.ResetBindings();
    }

    private void SetEditMode(bool editing)
    {
        _editing = editing;
        _grid.ReadOnly = !_editing;
        foreach (string key in new[]
                 {
                     nameof(SwitchFeeRuleRow.InstCode),
                     nameof(SwitchFeeRuleRow.InstitutionName),
                     nameof(SwitchFeeRuleRow.ProductCode),
                     nameof(SwitchFeeRuleRow.Direction),
                     nameof(SwitchFeeRuleRow.LegType),
                     nameof(SwitchFeeRuleRow.EffectiveFrom)
                 })
            _grid.Columns[key].ReadOnly = true;
        _edit.Enabled = !_editing && _rows.Count > 0;
        _save.Enabled = _editing;
        _cancel.Enabled = _editing;
        _refresh.Enabled = !_editing;
        _activeOnly.Enabled = !_editing;
        if (_editing)
            _status.Text = "수수료, 적용 종료일, 사용 여부를 수정한 뒤 저장하세요.";
    }

    private async Task SaveAsync()
    {
        _grid.EndEdit();
        SwitchFeeRuleRow? invalid = _rows.FirstOrDefault(x => x.FeePerMillion < 0);
        if (invalid is not null)
        {
            MessageBox.Show($"{invalid.InstitutionName}/{invalid.ProductCode}: 수수료는 0 이상이어야 합니다.",
                "입력값 확인", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        invalid = _rows.FirstOrDefault(x => x.EffectiveTo.HasValue && x.EffectiveTo.Value.Date < x.EffectiveFrom.Date);
        if (invalid is not null)
        {
            MessageBox.Show($"{invalid.InstitutionName}/{invalid.ProductCode}: 종료일은 시작일보다 빠를 수 없습니다.",
                "입력값 확인", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (MessageBox.Show("수정한 스위치 수수료 규칙을 저장하시겠습니까?", "저장 확인",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        try
        {
            UseWaitCursor = true;
            _save.Enabled = false;
            _cancel.Enabled = false;
            _status.Text = "저장 중...";
            await _repository.UpdateAllAsync(_rows.ToList());
            await LoadAsync();
            MessageBox.Show("스위치 수수료 규칙이 저장되었습니다.", "저장 완료",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _status.Text = "저장 오류";
            _save.Enabled = true;
            _cancel.Enabled = true;
            MessageBox.Show("스위치 수수료 저장 중 오류가 발생했습니다.\n\n" + ex.Message,
                "저장 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task CancelAsync()
    {
        if (MessageBox.Show("수정 내용을 취소하고 DB 값을 다시 불러오시겠습니까?", "수정 취소",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            await LoadAsync();
    }
}
