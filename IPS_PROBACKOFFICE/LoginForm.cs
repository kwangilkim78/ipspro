using IPS_PROBACKOFFICE.Data;
using IPS_PROBACKOFFICE.Security;

namespace IPS_PROBACKOFFICE;

public sealed class LoginForm : Form
{
    private readonly AuthRepository _repository;
    private readonly TextBox _loginId = new() { Width = 240 };
    private readonly TextBox _password = new() { Width = 240, UseSystemPasswordChar = true };
    private readonly Button _login = new() { Text = "로그인", Width = 100, Height = 32 };
    private readonly Label _status = new() { AutoSize = true, ForeColor = Color.Firebrick };
    private readonly CheckBox _remember = new() { Text = "아이디/비밀번호 저장", AutoSize = true };
    private readonly CheckBox _autoLogin = new() { Text = "자동 로그인", AutoSize = true, Enabled = false };
    private readonly bool _suppressAutoLogin;

    public AppUser? AuthenticatedUser { get; private set; }

    public LoginForm(AuthRepository repository, bool suppressAutoLogin = false)
    {
        _repository = repository;
        _suppressAutoLogin = suppressAutoLogin;
        Text = "IPS PRO BACKOFFICE 로그인";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(445, 310);
        Font = new Font("맑은 고딕", 10F);

        var title = new Label
        {
            Text = "IPS PRO BACKOFFICE",
            Font = new Font("맑은 고딕", 17F, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 14)
        };
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(45, 28, 45, 22),
            ColumnCount = 2,
            RowCount = 6
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.Controls.Add(title, 0, 0);
        table.SetColumnSpan(title, 2);
        AddField(table, 1, "아이디", _loginId);
        AddField(table, 2, "비밀번호", _password);
        var options = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        options.Controls.Add(_remember);
        options.Controls.Add(_autoLogin);
        table.Controls.Add(options, 1, 3);
        table.Controls.Add(_status, 1, 4);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(_login);
        buttons.Controls.Add(new Button { Text = "종료", Width = 90, Height = 32, DialogResult = DialogResult.Cancel });
        table.Controls.Add(buttons, 0, 5);
        table.SetColumnSpan(buttons, 2);
        Controls.Add(table);

        AcceptButton = _login;
        CancelButton = (Button)buttons.Controls[1];
        _remember.CheckedChanged += (_, _) =>
        {
            _autoLogin.Enabled = _remember.Checked;
            if (!_remember.Checked) _autoLogin.Checked = false;
        };
        _autoLogin.CheckedChanged += (_, _) =>
        {
            if (_autoLogin.Checked) _remember.Checked = true;
        };
        LoadSavedCredential();
        Shown += async (_, _) =>
        {
            if (_autoLogin.Checked && !_suppressAutoLogin)
                await LoginAsync(automatic: true);
            else if (string.IsNullOrWhiteSpace(_loginId.Text))
                _loginId.Focus();
            else
                _password.Focus();
        };
        _login.Click += async (_, _) => await LoginAsync(automatic: false);
    }

    private void LoadSavedCredential()
    {
        SavedLoginCredential? saved = LoginCredentialStore.Load();
        if (saved is null) return;
        _loginId.Text = saved.LoginId;
        _password.Text = saved.Password;
        _remember.Checked = saved.RememberPassword;
        _autoLogin.Enabled = saved.RememberPassword;
        _autoLogin.Checked = saved.RememberPassword && saved.AutoLogin;
    }

    private static void AddField(TableLayoutPanel table, int row, string text, Control control)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        table.Controls.Add(new Label { Text = text, AutoSize = true, Margin = new Padding(0, 8, 4, 0) }, 0, row);
        control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        table.Controls.Add(control, 1, row);
    }

    private async Task LoginAsync(bool automatic)
    {
        if (string.IsNullOrWhiteSpace(_loginId.Text) || string.IsNullOrEmpty(_password.Text))
        {
            _status.Text = "아이디와 비밀번호를 입력하세요.";
            return;
        }

        try
        {
            _login.Enabled = false;
            _status.ForeColor = Color.DimGray;
            _status.Text = "로그인 확인 중...";
            AuthenticatedUser = await _repository.AuthenticateAsync(_loginId.Text, _password.Text);
            if (AuthenticatedUser is null)
            {
                _status.ForeColor = Color.Firebrick;
                _status.Text = "아이디 또는 비밀번호가 올바르지 않습니다.";
                _password.Clear();
                _password.Focus();
                if (automatic)
                {
                    _autoLogin.Checked = false;
                    LoginCredentialStore.DisableAutoLogin();
                }
                return;
            }

            if (_remember.Checked)
            {
                bool saved = LoginCredentialStore.Save(new SavedLoginCredential(
                    _loginId.Text.Trim(),
                    _password.Text,
                    RememberPassword: true,
                    AutoLogin: _autoLogin.Checked));
                if (!saved)
                    MessageBox.Show("로그인은 성공했지만 로그인 정보를 저장하지 못했습니다.", "알림");
            }
            else
            {
                LoginCredentialStore.Clear();
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            _status.ForeColor = Color.Firebrick;
            _status.Text = "로그인 오류: " + ex.Message;
        }
        finally
        {
            _login.Enabled = true;
        }
    }
}
