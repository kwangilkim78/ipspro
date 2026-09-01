using IPS_PROBACKOFFICE.Data;
using IPS_PROBACKOFFICE.Security;

namespace IPS_PROBACKOFFICE;

public sealed class PasswordChangeForm : Form
{
    private readonly AuthRepository _repository;
    private readonly AppUser _user;
    private readonly TextBox _current = new() { Width = 250, UseSystemPasswordChar = true };
    private readonly TextBox _newPassword = new() { Width = 250, UseSystemPasswordChar = true };
    private readonly TextBox _confirm = new() { Width = 250, UseSystemPasswordChar = true };
    private readonly Button _save = new() { Text = "비밀번호 변경", Width = 120, Height = 32 };
    private readonly Label _status = new() { AutoSize = true, ForeColor = Color.Firebrick };

    public string? ChangedPassword { get; private set; }

    public PasswordChangeForm(AuthRepository repository, AppUser user, bool required)
    {
        _repository = repository;
        _user = user;
        Text = required ? "최초 로그인 비밀번호 변경" : "내 비밀번호 변경";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(465, 310);
        Font = new Font("맑은 고딕", 10F);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(32, 24, 32, 20),
            ColumnCount = 2
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var notice = new Label
        {
            Text = required
                ? "관리자가 발급한 초기 비밀번호를 새 비밀번호로 변경해야 합니다."
                : $"{user.LoginId} 계정의 비밀번호를 변경합니다.",
            AutoSize = true,
            ForeColor = required ? Color.Firebrick : Color.DimGray,
            Margin = new Padding(0, 0, 0, 14)
        };
        table.Controls.Add(notice, 0, 0);
        table.SetColumnSpan(notice, 2);
        AddField(table, 1, "현재 비밀번호", _current);
        AddField(table, 2, "새 비밀번호", _newPassword);
        AddField(table, 3, "새 비밀번호 확인", _confirm);
        table.Controls.Add(_status, 1, 4);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(_save);
        var cancel = new Button { Text = required ? "로그인 취소" : "취소", Width = 100, Height = 32, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(cancel);
        table.Controls.Add(buttons, 0, 5);
        table.SetColumnSpan(buttons, 2);
        Controls.Add(table);

        AcceptButton = _save;
        CancelButton = cancel;
        _save.Click += async (_, _) => await ChangeAsync();
        Shown += (_, _) => _current.Focus();
    }

    private static void AddField(TableLayoutPanel table, int row, string label, Control control)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        table.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 8, 4, 0) }, 0, row);
        table.Controls.Add(control, 1, row);
    }

    private async Task ChangeAsync()
    {
        if (string.IsNullOrEmpty(_current.Text) || _newPassword.Text.Length < 4)
        {
            _status.Text = "현재 비밀번호를 입력하고 새 비밀번호는 4자 이상으로 지정하세요.";
            return;
        }
        if (_newPassword.Text != _confirm.Text)
        {
            _status.Text = "새 비밀번호 확인이 일치하지 않습니다.";
            return;
        }
        if (_current.Text == _newPassword.Text)
        {
            _status.Text = "현재 비밀번호와 다른 비밀번호를 입력하세요.";
            return;
        }

        try
        {
            _save.Enabled = false;
            _status.ForeColor = Color.DimGray;
            _status.Text = "변경 중...";
            bool changed = await _repository.ChangeOwnPasswordAsync(_user.UserId, _current.Text, _newPassword.Text);
            if (!changed)
            {
                _status.ForeColor = Color.Firebrick;
                _status.Text = "현재 비밀번호가 올바르지 않습니다.";
                _current.Clear();
                _current.Focus();
                return;
            }
            ChangedPassword = _newPassword.Text;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            _status.ForeColor = Color.Firebrick;
            _status.Text = "변경 오류: " + ex.Message;
        }
        finally
        {
            _save.Enabled = true;
        }
    }
}
