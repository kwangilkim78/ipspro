using IPS_PROBACKOFFICE.Data;
using IPS_PROBACKOFFICE.Security;

namespace IPS_PROBACKOFFICE;

public sealed class UserManagementForm : Form
{
    private readonly AuthRepository _repository;
    private readonly AppUser _currentUser;
    private readonly AuditService _audit;
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoGenerateColumns = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        RowHeadersVisible = false
    };
    private readonly Label _status = new() { AutoSize = true, Text = "준비" };
    private readonly CheckedListBox _screenPermissions = new()
    {
        Dock = DockStyle.Fill,
        CheckOnClick = true,
        IntegralHeight = false,
        DisplayMember = nameof(ScreenPermissionDefinition.DisplayName)
    };
    private readonly Button _savePermissionsButton;
    private readonly Label _permissionTarget = new()
    {
        Text = "사용자를 선택하세요.",
        AutoSize = true,
        Font = new Font("맑은 고딕", 10F, FontStyle.Bold),
        Margin = new Padding(4, 8, 4, 4)
    };
    private List<BackOfficeUserRow> _rows = new();
    private bool _loadingPermissions;

    public UserManagementForm(AuthRepository repository, AppUser currentUser, AuditService audit)
    {
        _repository = repository;
        _currentUser = currentUser;
        _audit = audit;
        Text = "백오피스 사용자 관리";
        Font = new Font("맑은 고딕", 10F);
        ConfigureGrid();
        _savePermissionsButton = Button("화면 권한 저장", async (_, _) => await SavePermissionsAsync());
        _savePermissionsButton.Enabled = false;

        foreach (ScreenPermissionDefinition screen in ScreenPermissionCatalog.Screens)
            _screenPermissions.Items.Add(screen);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(8, 6, 8, 4)
        };
        toolbar.Controls.Add(Button("신규 사용자", async (_, _) => await AddAsync()));
        toolbar.Controls.Add(Button("선택 사용자 수정", async (_, _) => await EditAsync()));
        toolbar.Controls.Add(Button("비밀번호 재설정", async (_, _) => await ResetPasswordAsync()));
        toolbar.Controls.Add(Button("사용/중지 전환", async (_, _) => await ToggleActiveAsync()));
        toolbar.Controls.Add(Button("새로고침", async (_, _) => await ReloadAsync()));
        toolbar.Controls.Add(_status);
        var permissionToolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(8, 4, 8, 4)
        };
        permissionToolbar.Controls.Add(_permissionTarget);
        permissionToolbar.Controls.Add(_savePermissionsButton);

        var permissionPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        permissionPanel.Controls.Add(_screenPermissions);
        permissionPanel.Controls.Add(permissionToolbar);

        var content = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical
        };
        content.Panel1.Controls.Add(_grid);
        content.Panel2.Controls.Add(permissionPanel);

        Controls.Add(content);
        Controls.Add(toolbar);
        Shown += async (_, _) =>
        {
            SetSafeSplitterDistance(content, 0.72);
            await ReloadAsync();
        };
        _grid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await EditAsync(); };
        _grid.SelectionChanged += async (_, _) => await LoadPermissionsAsync();
    }

    private void ConfigureGrid()
    {
        AddColumn(nameof(BackOfficeUserRow.LoginId), "아이디", 130);
        AddColumn(nameof(BackOfficeUserRow.UserName), "사용자명", 150);
        AddColumn(nameof(BackOfficeUserRow.Email), "이메일", 220);
        AddColumn(nameof(BackOfficeUserRow.IsActive), "사용", 70);
        AddColumn(nameof(BackOfficeUserRow.MustChangePassword), "변경 필요", 90);
        AddColumn(nameof(BackOfficeUserRow.LastLoginAt), "마지막 로그인", 160, "yyyy-MM-dd HH:mm:ss");
        AddColumn(nameof(BackOfficeUserRow.PasswordChangedAt), "비밀번호 변경일", 160, "yyyy-MM-dd HH:mm:ss");
        AddColumn(nameof(BackOfficeUserRow.CreatedAt), "등록일", 160, "yyyy-MM-dd HH:mm:ss");
    }

    private void AddColumn(string property, string title, int width, string? format = null)
    {
        var column = new DataGridViewTextBoxColumn
        {
            DataPropertyName = property,
            HeaderText = title,
            Width = width
        };
        if (format is not null) column.DefaultCellStyle.Format = format;
        _grid.Columns.Add(column);
    }

    private async Task ReloadAsync()
    {
        try
        {
            _rows = (await _repository.GetUsersAsync()).ToList();
            _grid.DataSource = null;
            _grid.DataSource = _rows;
            _status.Text = $"사용자 {_rows.Count:N0}명";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "사용자 조회 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private BackOfficeUserRow? Selected() => _grid.CurrentRow?.DataBoundItem as BackOfficeUserRow;

    private async Task LoadPermissionsAsync()
    {
        if (_loadingPermissions)
            return;

        BackOfficeUserRow? row = Selected();
        _permissionTarget.Text = row is null
            ? "사용자를 선택하세요."
            : $"{row.LoginId} 화면 권한";
        bool editable = row is not null;
        _savePermissionsButton.Enabled = editable;
        _screenPermissions.Enabled = editable;

        _loadingPermissions = true;
        try
        {
            for (int i = 0; i < _screenPermissions.Items.Count; i++)
                _screenPermissions.SetItemChecked(i, false);

            if (row is null)
                return;

            IReadOnlySet<string> allowed = await _repository.GetScreenPermissionsAsync(row.LoginId);

            if (Selected()?.LoginId != row.LoginId)
                return;

            for (int i = 0; i < _screenPermissions.Items.Count; i++)
            {
                if (_screenPermissions.Items[i] is ScreenPermissionDefinition screen)
                    _screenPermissions.SetItemChecked(i, allowed.Contains(screen.Key));
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "화면 권한 조회 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _loadingPermissions = false;
        }
    }

    private async Task SavePermissionsAsync()
    {
        BackOfficeUserRow? row = Selected();
        if (row is null)
            return;

        string[] checkedKeys = _screenPermissions.CheckedItems
            .OfType<ScreenPermissionDefinition>()
            .Select(x => x.Key)
            .ToArray();

        if (row.UserId == _currentUser.UserId &&
            !checkedKeys.Contains(ScreenPermissionCatalog.UserManagement, StringComparer.OrdinalIgnoreCase))
        {
            MessageBox.Show(
                "현재 로그인한 본인의 '백오피스 사용자 관리' 권한은 해제할 수 없습니다.",
                "권한 확인",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        try
        {
            _savePermissionsButton.Enabled = false;
            await _repository.SaveScreenPermissionsAsync(row.LoginId, checkedKeys);
            await _audit.WriteAsync(
                "SCREEN_PERMISSION_UPDATE",
                row.LoginId,
                string.Join(",", checkedKeys));
            _status.Text = $"{row.LoginId}: 화면 권한 {checkedKeys.Length:N0}개 저장 완료 · 다음 로그인부터 적용";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "화면 권한 저장 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _savePermissionsButton.Enabled = true;
        }
    }

    private async Task AddAsync()
    {
        using var dialog = new UserEditDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            await _repository.AddUserAsync(dialog.LoginId, dialog.UserName, dialog.Password!, "USER", dialog.Email);
            await _audit.WriteAsync("USER_CREATE", dialog.LoginId);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "사용자 등록 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task EditAsync()
    {
        BackOfficeUserRow? row = Selected();
        if (row is null) return;
        using var dialog = new UserEditDialog(row);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            await _repository.UpdateUserAsync(row.UserId, dialog.UserName, "USER", dialog.Email);
            await _audit.WriteAsync("USER_UPDATE", row.LoginId);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "사용자 수정 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task ResetPasswordAsync()
    {
        BackOfficeUserRow? row = Selected();
        if (row is null) return;
        using var dialog = new PasswordResetDialog(row.LoginId);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        await _repository.ResetPasswordAsync(row.UserId, dialog.Password);
        await _audit.WriteAsync("PASSWORD_RESET", row.LoginId);
        MessageBox.Show("비밀번호가 재설정되었습니다.", "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task ToggleActiveAsync()
    {
        BackOfficeUserRow? row = Selected();
        if (row is null) return;
        if (row.UserId == _currentUser.UserId && row.IsActive)
        {
            MessageBox.Show("현재 로그인한 관리자 본인 계정은 중지할 수 없습니다.");
            return;
        }
        await _repository.SetActiveAsync(row.UserId, !row.IsActive);
        await _audit.WriteAsync("USER_ACTIVE_CHANGE", row.LoginId, $"is_active={!row.IsActive}");
        await ReloadAsync();
    }

    private static Button Button(string text, EventHandler handler)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 30 };
        button.Click += handler;
        return button;
    }

    private static void SetSafeSplitterDistance(SplitContainer split, double ratio)
    {
        int available = split.ClientSize.Width - split.SplitterWidth;
        if (available <= 2)
            return;

        split.SplitterDistance = Math.Clamp(
            (int)Math.Round(available * ratio),
            1,
            available - 1);
    }
}

internal sealed class UserEditDialog : Form
{
    private readonly TextBox _loginId = new() { Width = 250 };
    private readonly TextBox _userName = new() { Width = 250 };
    private readonly TextBox _password = new() { Width = 250, UseSystemPasswordChar = true };
    private readonly TextBox _email = new() { Width = 250 };
    private readonly bool _isNew;

    public string LoginId => _loginId.Text.Trim();
    public string UserName => _userName.Text.Trim();
    public string? Password => _isNew ? _password.Text : null;
    public string? Email => string.IsNullOrWhiteSpace(_email.Text) ? null : _email.Text.Trim();

    public UserEditDialog(BackOfficeUserRow? row = null)
    {
        _isNew = row is null;
        Text = _isNew ? "신규 사용자" : "사용자 수정";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(440, _isNew ? 300 : 260);
        Font = new Font("맑은 고딕", 10F);
        _loginId.Text = row?.LoginId ?? "";
        _loginId.ReadOnly = !_isNew;
        _userName.Text = row?.UserName ?? "";
        _email.Text = row?.Email ?? "";

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 2 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        int r = 0;
        Add(table, r++, "아이디", _loginId);
        Add(table, r++, "사용자명", _userName);
        if (_isNew) Add(table, r++, "초기 비밀번호", _password);
        Add(table, r++, "이메일", _email);
        var save = new Button { Text = "저장", Width = 90 };
        var cancel = new Button { Text = "취소", Width = 90, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(save); buttons.Controls.Add(cancel);
        table.Controls.Add(buttons, 0, r); table.SetColumnSpan(buttons, 2);
        Controls.Add(table);
        save.Click += (_, _) => Save();
        CancelButton = cancel;
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(LoginId) || string.IsNullOrWhiteSpace(UserName) ||
            (_isNew && (_password.Text.Length < 4)))
        {
            MessageBox.Show("아이디·사용자명을 입력하고 비밀번호는 4자 이상으로 지정하세요.");
            return;
        }
        DialogResult = DialogResult.OK;
        Close();
    }

    private static void Add(TableLayoutPanel table, int row, string title, Control control)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        table.Controls.Add(new Label { Text = title, AutoSize = true, Margin = new Padding(0, 8, 4, 0) }, 0, row);
        table.Controls.Add(control, 1, row);
    }
}

internal sealed class PasswordResetDialog : Form
{
    private readonly TextBox _password = new() { Width = 230, UseSystemPasswordChar = true };
    public string Password => _password.Text;

    public PasswordResetDialog(string loginId)
    {
        Text = $"{loginId} 비밀번호 재설정";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ClientSize = new Size(390, 145);
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), FlowDirection = FlowDirection.TopDown };
        panel.Controls.Add(new Label { Text = "새 비밀번호(4자 이상)", AutoSize = true });
        panel.Controls.Add(_password);
        var save = new Button { Text = "변경", Width = 90 };
        panel.Controls.Add(save);
        Controls.Add(panel);
        save.Click += (_, _) =>
        {
            if (_password.Text.Length < 4) { MessageBox.Show("4자 이상 입력하세요."); return; }
            DialogResult = DialogResult.OK; Close();
        };
    }
}
