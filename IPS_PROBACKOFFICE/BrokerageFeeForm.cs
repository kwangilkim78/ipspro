using System.ComponentModel;
using IPS_PROBACKOFFICE.Data;
using IPS_PROBACKOFFICE.Models;

namespace IPS_PROBACKOFFICE;

public sealed class BrokerageFeeForm : Form
{
    private readonly BrokerageFeeRepository _repository;
    private readonly Button _btnRefresh = new() { Text = "새로고침", Width = 100, Height = 30 };
    private readonly Button _btnAdd = new() { Text = "신규 기관 추가", Width = 120, Height = 30 };
    private readonly Button _btnDelete = new() { Text = "삭제", Width = 90, Height = 30 };
    private readonly Button _btnEdit = new() { Text = "수정", Width = 90, Height = 30 };
    private readonly Button _btnSave = new() { Text = "저장", Width = 90, Height = 30, Enabled = false };
    private readonly Button _btnCancel = new() { Text = "취소", Width = 90, Height = 30, Enabled = false };
    private readonly Label _status = new()
    {
        AutoSize = true,
        Text = "수수료 정보를 조회합니다.",
        Margin = new Padding(14, 8, 0, 0)
    };
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
        AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells,
        BackgroundColor = Color.White,
        BorderStyle = BorderStyle.None,
        EnableHeadersVisualStyles = false
    };
    private readonly BindingList<BrokerageFeeRow> _rows = [];
    private bool _isEditing;

    public BrokerageFeeForm(BrokerageFeeRepository repository)
    {
        _repository = repository;
        Text = "기관별 중개 수수료";
        BuildUi();

        Shown += async (_, _) => await LoadFeesAsync();
        _btnRefresh.Click += async (_, _) => await LoadFeesAsync();
        _btnAdd.Click += async (_, _) => await AddInstitutionAsync();
        _btnDelete.Click += async (_, _) => await DeleteSelectedAsync();
        _btnEdit.Click += (_, _) => BeginEdit();
        _btnSave.Click += async (_, _) => await SaveAsync();
        _btnCancel.Click += async (_, _) => await CancelEditAsync();
        _grid.CellFormatting += Grid_CellFormatting;
        _grid.DataError += (_, e) =>
        {
            e.ThrowException = false;
            MessageBox.Show("수수료는 숫자로 입력해 주세요.", "입력 오류",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        };
    }

    private void BuildUi()
    {
        var title = new Label
        {
            Text = "기관별 중개 수수료 현황",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 16F, FontStyle.Bold),
            ForeColor = Color.FromArgb(31, 78, 121),
            Margin = new Padding(0, 4, 24, 0)
        };

        var topPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 62,
            Padding = new Padding(14, 12, 14, 8),
            WrapContents = false,
            BackColor = Color.FromArgb(245, 248, 252)
        };
        topPanel.Controls.Add(title);
        topPanel.Controls.Add(_btnRefresh);
        topPanel.Controls.Add(_btnAdd);
        topPanel.Controls.Add(_btnDelete);
        topPanel.Controls.Add(_btnEdit);
        topPanel.Controls.Add(_btnSave);
        topPanel.Controls.Add(_btnCancel);
        topPanel.Controls.Add(_status);

        AddColumn(nameof(BrokerageFeeRow.InstCode), "기관코드", 90,
            DataGridViewContentAlignment.MiddleCenter);
        AddColumn(nameof(BrokerageFeeRow.InstitutionName), "기관", 180,
            DataGridViewContentAlignment.MiddleLeft);
        AddColumn(nameof(BrokerageFeeRow.Df1mFee), "DF 1M", 130,
            DataGridViewContentAlignment.MiddleRight);
        AddColumn(nameof(BrokerageFeeRow.Ndf1mFee), "NDF 1M", 130,
            DataGridViewContentAlignment.MiddleRight);
        AddColumn(nameof(BrokerageFeeRow.NdMarFee), "ND/NDF MAR", 140,
            DataGridViewContentAlignment.MiddleRight);
        AddColumn(nameof(BrokerageFeeRow.BasisMarFee), "BASIS MAR", 140,
            DataGridViewContentAlignment.MiddleRight);
        AddColumn(nameof(BrokerageFeeRow.CurrencyCode), "통화", 80,
            DataGridViewContentAlignment.MiddleCenter);
        AddColumn(nameof(BrokerageFeeRow.Remarks), "비고", 300,
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
        _grid.ColumnHeadersHeight = 38;
        _grid.RowTemplate.Height = 34;
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(246, 249, 252);
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(214, 228, 242);
        _grid.DefaultCellStyle.SelectionForeColor = Color.Black;
        _grid.DataSource = _rows;

        Controls.Add(_grid);
        Controls.Add(topPanel);
    }

    private void AddColumn(
        string propertyName,
        string headerText,
        int width,
        DataGridViewContentAlignment alignment,
        DataGridViewAutoSizeColumnMode autoSizeMode = DataGridViewAutoSizeColumnMode.None)
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = propertyName,
            DataPropertyName = propertyName,
            HeaderText = headerText,
            Width = width,
            AutoSizeMode = autoSizeMode,
            SortMode = DataGridViewColumnSortMode.Automatic,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = alignment }
        });
    }

    private async Task LoadFeesAsync()
    {
        try
        {
            UseWaitCursor = true;
            _btnRefresh.Enabled = false;
            _status.Text = "조회 중...";

            var fees = await _repository.GetAllAsync();
            _rows.RaiseListChangedEvents = false;
            _rows.Clear();
            foreach (var fee in fees)
                _rows.Add(fee);
            _rows.RaiseListChangedEvents = true;
            _rows.ResetBindings();

            SetEditMode(false);

            _status.Text = $"기관 {_rows.Count:N0}개 · 최종 조회 {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        }
        catch (Exception ex)
        {
            _status.Text = "조회 오류";
            MessageBox.Show(
                "기관별 수수료 조회 중 오류가 발생했습니다.\n\n" + ex.Message,
                "수수료 조회 오류",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
            _btnRefresh.Enabled = true;
        }
    }

    private void BeginEdit()
    {
        if (_rows.Count == 0)
            return;

        SetEditMode(true);
        _status.Text = "수정할 값을 입력한 뒤 저장을 누르세요.";
    }

    private async Task AddInstitutionAsync()
    {
        using var dialog = new AddBrokerageFeeForm();
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.FeeRow is null)
            return;

        try
        {
            UseWaitCursor = true;
            _btnAdd.Enabled = false;
            _status.Text = "신규 기관 저장 중...";

            await _repository.AddAsync(dialog.FeeRow);
            await LoadFeesAsync();

            int rowIndex = _rows.ToList().FindIndex(x => x.InstCode == dialog.FeeRow.InstCode);
            if (rowIndex >= 0)
            {
                _grid.ClearSelection();
                _grid.Rows[rowIndex].Selected = true;
                _grid.FirstDisplayedScrollingRowIndex = rowIndex;
            }

            MessageBox.Show("신규 기관의 중개 수수료가 등록되었습니다.", "등록 완료",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (MySqlConnector.MySqlException ex) when (ex.Number == 1062)
        {
            _status.Text = "등록 오류";
            MessageBox.Show("이미 등록된 기관코드입니다.", "중복 기관",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            _status.Text = "등록 오류";
            MessageBox.Show("신규 기관 등록 중 오류가 발생했습니다.\n\n" + ex.Message,
                "등록 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
            _btnAdd.Enabled = !_isEditing;
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (_grid.CurrentRow?.DataBoundItem is not BrokerageFeeRow row)
        {
            MessageBox.Show("삭제할 기관을 먼저 선택해 주세요.", "기관 선택",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        DialogResult answer = MessageBox.Show(
            $"'{row.InstitutionName}' 기관의 중개 수수료 정보를 삭제하시겠습니까?\n\n" +
            "삭제한 항목은 화면과 수수료 계산 대상에서 제외됩니다.",
            "기관 삭제 확인",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes)
            return;

        try
        {
            UseWaitCursor = true;
            _btnDelete.Enabled = false;
            _status.Text = "기관 삭제 중...";

            bool deleted = await _repository.DeleteAsync(row.InstCode);
            if (!deleted)
            {
                MessageBox.Show("이미 삭제되었거나 존재하지 않는 기관입니다.", "삭제 결과",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            await LoadFeesAsync();
            if (deleted)
            {
                MessageBox.Show("기관의 중개 수수료 정보가 삭제되었습니다.", "삭제 완료",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            _status.Text = "삭제 오류";
            MessageBox.Show("기관 삭제 중 오류가 발생했습니다.\n\n" + ex.Message,
                "삭제 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
            _btnDelete.Enabled = !_isEditing && _rows.Count > 0;
        }
    }

    private async Task SaveAsync()
    {
        _grid.EndEdit();

        string? validationError = ValidateRows();
        if (validationError is not null)
        {
            MessageBox.Show(validationError, "입력값 확인",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult answer = MessageBox.Show(
            "수정한 기관별 수수료를 DB에 저장하시겠습니까?",
            "수수료 저장 확인",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (answer != DialogResult.Yes)
            return;

        try
        {
            UseWaitCursor = true;
            _btnSave.Enabled = false;
            _btnCancel.Enabled = false;
            _status.Text = "저장 중...";

            await _repository.UpdateAllAsync(_rows.ToList());
            await LoadFeesAsync();
            MessageBox.Show("기관별 수수료가 저장되었습니다.", "저장 완료",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _status.Text = "저장 오류";
            _btnSave.Enabled = true;
            _btnCancel.Enabled = true;
            MessageBox.Show("수수료 저장 중 오류가 발생했습니다.\n\n" + ex.Message,
                "저장 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task CancelEditAsync()
    {
        DialogResult answer = MessageBox.Show(
            "수정 중인 내용을 취소하고 DB 값을 다시 불러오시겠습니까?",
            "수정 취소",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (answer == DialogResult.Yes)
            await LoadFeesAsync();
    }

    private void SetEditMode(bool editing)
    {
        _isEditing = editing;
        _grid.ReadOnly = !editing;
        _grid.Columns[nameof(BrokerageFeeRow.InstCode)].ReadOnly = true;
        _grid.Columns[nameof(BrokerageFeeRow.InstitutionName)].ReadOnly = true;
        _btnEdit.Enabled = !editing;
        _btnAdd.Enabled = !editing;
        _btnDelete.Enabled = !editing && _rows.Count > 0;
        _btnRefresh.Enabled = !editing;
        _btnSave.Enabled = editing;
        _btnCancel.Enabled = editing;
        _grid.Refresh();
    }

    private string? ValidateRows()
    {
        foreach (var row in _rows)
        {
            decimal?[] fees = [row.Df1mFee, row.Ndf1mFee, row.NdMarFee, row.BasisMarFee];
            if (fees.Any(x => x < 0))
                return $"{row.InstitutionName}: 수수료는 0 이상이어야 합니다.";

            string currency = row.CurrencyCode?.Trim().ToUpperInvariant() ?? string.Empty;
            bool hasFee = fees.Any(x => x.HasValue);
            if (hasFee && currency is not ("KRW" or "USD"))
                return $"{row.InstitutionName}: 통화는 KRW 또는 USD로 입력해 주세요.";

            row.CurrencyCode = string.IsNullOrWhiteSpace(currency) ? null : currency;
            row.Remarks = string.IsNullOrWhiteSpace(row.Remarks) ? null : row.Remarks.Trim();
        }

        return null;
    }

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || _grid.Rows[e.RowIndex].DataBoundItem is not BrokerageFeeRow row)
            return;

        string? propertyName = _grid.Columns[e.ColumnIndex].DataPropertyName;
        bool isFeeColumn = propertyName is nameof(BrokerageFeeRow.Df1mFee)
            or nameof(BrokerageFeeRow.Ndf1mFee)
            or nameof(BrokerageFeeRow.NdMarFee)
            or nameof(BrokerageFeeRow.BasisMarFee);

        if (isFeeColumn)
        {
            if (e.Value is null)
            {
                e.Value = "미등록";
                e.CellStyle.ForeColor = Color.Firebrick;
            }
            else if (e.Value is decimal amount)
            {
                e.Value = _isEditing
                    ? $"{amount:0.##}"
                    : string.Equals(row.CurrencyCode, "KRW", StringComparison.OrdinalIgnoreCase)
                        ? $"{amount:N0}원"
                        : string.Equals(row.CurrencyCode, "USD", StringComparison.OrdinalIgnoreCase)
                            ? $"${amount:N2}"
                            : $"{amount:N2}";
            }
            e.FormattingApplied = true;
        }

        if (propertyName == nameof(BrokerageFeeRow.CurrencyCode))
        {
            e.CellStyle.Font = new Font(_grid.Font, FontStyle.Bold);
            e.CellStyle.ForeColor = string.Equals(row.CurrencyCode, "USD", StringComparison.OrdinalIgnoreCase)
                ? Color.FromArgb(0, 102, 51)
                : Color.FromArgb(31, 78, 121);
        }
    }
}
