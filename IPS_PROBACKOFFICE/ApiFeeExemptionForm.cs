using System.ComponentModel;
using IPS_PROBACKOFFICE.Data;
using IPS_PROBACKOFFICE.Models;

namespace IPS_PROBACKOFFICE;

public sealed class ApiFeeExemptionForm : Form
{
    private readonly ApiFeeExemptionRepository _repository;
    private readonly BindingList<ApiFeeExemptionRow> _rows = [];
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
        BorderStyle = BorderStyle.None
    };
    private readonly Button _refresh = new() { Text = "조회", Width = 90, Height = 30 };
    private readonly Button _add = new() { Text = "신규 등록", Width = 100, Height = 30 };
    private readonly Button _edit = new() { Text = "수정", Width = 90, Height = 30 };
    private readonly Button _delete = new() { Text = "삭제", Width = 90, Height = 30 };
    private readonly ComboBox _filter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    private readonly Label _status = new() { AutoSize = true, Margin = new Padding(14, 8, 0, 0) };
    private IReadOnlyList<ApiFeeExemptionInstitution> _institutions = [];

    public ApiFeeExemptionForm(ApiFeeExemptionRepository repository)
    {
        _repository = repository;
        Text = "API 수수료 면제 관리";
        BuildUi();
        Shown += async (_, _) => await LoadAsync();
        _refresh.Click += async (_, _) => await LoadAsync();
        _filter.SelectedIndexChanged += async (_, _) => await LoadAsync();
        _add.Click += async (_, _) => await AddAsync();
        _edit.Click += async (_, _) => await EditAsync();
        _delete.Click += async (_, _) => await DeleteAsync();
        _grid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await EditAsync(); };
    }

    private void BuildUi()
    {
        var title = new Label
        {
            Text = "API 거래 수수료 면제 정책",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 16F, FontStyle.Bold),
            ForeColor = Color.FromArgb(31, 78, 121),
            Margin = new Padding(0, 4, 20, 0)
        };
        _filter.Items.AddRange(["전체", "사용 중", "종료/미사용"]);
        _filter.SelectedIndex = 0;

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 62,
            Padding = new Padding(14, 12, 14, 8),
            WrapContents = false,
            BackColor = Color.FromArgb(245, 248, 252)
        };
        toolbar.Controls.Add(title);
        toolbar.Controls.Add(_filter);
        toolbar.Controls.Add(_refresh);
        toolbar.Controls.Add(_add);
        toolbar.Controls.Add(_edit);
        toolbar.Controls.Add(_delete);
        toolbar.Controls.Add(_status);

        AddTextColumn(nameof(ApiFeeExemptionRow.Id), "ID", 65, DataGridViewContentAlignment.MiddleCenter);
        AddTextColumn(nameof(ApiFeeExemptionRow.InstitutionCode), "기관코드", 90, DataGridViewContentAlignment.MiddleCenter);
        AddTextColumn(nameof(ApiFeeExemptionRow.InstitutionName), "기관", 180, DataGridViewContentAlignment.MiddleLeft);
        AddTextColumn(nameof(ApiFeeExemptionRow.TraderId), "딜러 ID", 160, DataGridViewContentAlignment.MiddleLeft);
        AddDateColumn(nameof(ApiFeeExemptionRow.StartDate), "시작일", 110);
        AddDateColumn(nameof(ApiFeeExemptionRow.EndDateInclusive), "종료일", 110);
        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(ApiFeeExemptionRow.IsActive),
            HeaderText = "사용",
            Width = 65,
            SortMode = DataGridViewColumnSortMode.Automatic
        });
        AddTextColumn(nameof(ApiFeeExemptionRow.Remarks), "비고", 300,
            DataGridViewContentAlignment.MiddleLeft, DataGridViewAutoSizeColumnMode.Fill);

        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            Alignment = DataGridViewContentAlignment.MiddleCenter,
            BackColor = Color.FromArgb(31, 78, 121),
            ForeColor = Color.White,
            Font = new Font(_grid.Font, FontStyle.Bold),
            SelectionBackColor = Color.FromArgb(31, 78, 121),
            SelectionForeColor = Color.White
        };
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersHeight = 38;
        _grid.RowTemplate.Height = 34;
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(246, 249, 252);
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(214, 228, 242);
        _grid.DefaultCellStyle.SelectionForeColor = Color.Black;
        _grid.DataSource = _rows;

        Controls.Add(_grid);
        Controls.Add(toolbar);
    }

    private void AddTextColumn(string propertyName, string title, int width,
        DataGridViewContentAlignment alignment,
        DataGridViewAutoSizeColumnMode autoSize = DataGridViewAutoSizeColumnMode.None)
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = propertyName,
            HeaderText = title,
            Width = width,
            AutoSizeMode = autoSize,
            SortMode = DataGridViewColumnSortMode.Automatic,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = alignment }
        });
    }

    private void AddDateColumn(string propertyName, string title, int width)
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = propertyName,
            HeaderText = title,
            Width = width,
            SortMode = DataGridViewColumnSortMode.Automatic,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleCenter,
                Format = "yyyy-MM-dd"
            }
        });
    }

    private async Task LoadAsync()
    {
        try
        {
            SetBusy(true, "조회 중...");
            _institutions = await _repository.GetInstitutionsAsync();
            IReadOnlyList<ApiFeeExemptionRow> all = await _repository.GetAllAsync();
            DateTime today = DateTime.Today;
            IEnumerable<ApiFeeExemptionRow> filtered = _filter.SelectedIndex switch
            {
                1 => all.Where(x => x.IsActive && x.EndDateInclusive >= today),
                2 => all.Where(x => !x.IsActive || x.EndDateInclusive < today),
                _ => all
            };

            _rows.RaiseListChangedEvents = false;
            _rows.Clear();
            foreach (ApiFeeExemptionRow row in filtered)
                _rows.Add(row);
            _rows.RaiseListChangedEvents = true;
            _rows.ResetBindings();
            _status.Text = $"{_rows.Count:N0}건 · {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        }
        catch (Exception ex)
        {
            _status.Text = "조회 오류";
            MessageBox.Show("API 수수료 면제 조회 중 오류가 발생했습니다.\n\n" + ex.Message,
                "조회 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false, null);
        }
    }

    private async Task AddAsync()
    {
        using var dialog = new ApiFeeExemptionEditForm(_institutions);
        UiLanguage.Apply(dialog);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result is null)
            return;
        try
        {
            SetBusy(true, "등록 중...");
            await _repository.AddAsync(dialog.Result);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show("등록 중 오류가 발생했습니다.\n\n" + ex.Message,
                "등록 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { SetBusy(false, null); }
    }

    private async Task EditAsync()
    {
        if (_grid.CurrentRow?.DataBoundItem is not ApiFeeExemptionRow selected)
        {
            MessageBox.Show("수정할 정책을 선택해 주세요.");
            return;
        }
        using var dialog = new ApiFeeExemptionEditForm(_institutions, selected);
        UiLanguage.Apply(dialog);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result is null)
            return;
        try
        {
            SetBusy(true, "수정 중...");
            await _repository.UpdateAsync(dialog.Result);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show("수정 중 오류가 발생했습니다.\n\n" + ex.Message,
                "수정 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { SetBusy(false, null); }
    }

    private async Task DeleteAsync()
    {
        if (_grid.CurrentRow?.DataBoundItem is not ApiFeeExemptionRow selected)
        {
            MessageBox.Show("삭제할 정책을 선택해 주세요.");
            return;
        }
        if (MessageBox.Show(
                $"{selected.InstitutionName} / {selected.TraderId}\n" +
                $"{selected.StartDate:yyyy-MM-dd} ~ {selected.EndDateInclusive:yyyy-MM-dd}\n\n삭제하시겠습니까?",
                "삭제 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;
        try
        {
            SetBusy(true, "삭제 중...");
            await _repository.DeleteAsync(selected.Id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show("삭제 중 오류가 발생했습니다.\n\n" + ex.Message,
                "삭제 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { SetBusy(false, null); }
    }

    private void SetBusy(bool busy, string? message)
    {
        UseWaitCursor = busy;
        _refresh.Enabled = !busy;
        _add.Enabled = !busy;
        _edit.Enabled = !busy;
        _delete.Enabled = !busy;
        if (message is not null)
            _status.Text = message;
    }
}
