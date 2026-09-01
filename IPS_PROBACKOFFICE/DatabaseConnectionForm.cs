using IPS_PROBACKOFFICE.Security;
using MySqlConnector;

namespace IPS_PROBACKOFFICE;

public sealed class DatabaseConnectionForm : Form
{
    private readonly string _baseConnectionString;
    private readonly TextBox _userName = new() { Width = 250, Text = "ips_backoffice" };
    private readonly TextBox _password = new() { Width = 250, UseSystemPasswordChar = true };
    private readonly Button _connect = new() { Text = "연결 및 저장", Width = 120, Height = 32 };
    private readonly Label _status = new() { AutoSize = true, ForeColor = Color.Firebrick };

    public string? ConnectionString { get; private set; }

    public DatabaseConnectionForm(
        string baseConnectionString,
        SavedDatabaseCredential? saved,
        string? initialError = null)
    {
        _baseConnectionString = baseConnectionString;
        var builder = new MySqlConnectionStringBuilder(baseConnectionString);
        Text = "백오피스 DB 접속 설정";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(500, 300);
        Font = new Font("맑은 고딕", 10F);

        var title = new Label
        {
            Text = "백오피스 전용 DB 계정",
            Font = new Font(Font.FontFamily, 16F, FontStyle.Bold),
            AutoSize = true
        };
        var server = new Label
        {
            Text = $"서버: {builder.Server}:{builder.Port} / DB: {builder.Database}",
            AutoSize = true,
            ForeColor = Color.DimGray
        };
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(38, 25, 38, 20),
            ColumnCount = 2,
            RowCount = 6
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.Controls.Add(title, 0, 0);
        table.SetColumnSpan(title, 2);
        table.Controls.Add(server, 0, 1);
        table.SetColumnSpan(server, 2);
        AddField(table, 2, "DB 사용자", _userName);
        AddField(table, 3, "DB 비밀번호", _password);
        table.Controls.Add(_status, 1, 4);

        var cancel = new Button { Text = "종료", Width = 90, Height = 32, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(_connect);
        buttons.Controls.Add(cancel);
        table.Controls.Add(buttons, 0, 5);
        table.SetColumnSpan(buttons, 2);
        Controls.Add(table);

        if (saved is not null)
        {
            _userName.Text = saved.UserName;
            _password.Text = saved.Password;
        }
        if (!string.IsNullOrWhiteSpace(initialError))
            _status.Text = "기존 DB 접속정보로 연결할 수 없습니다. 다시 입력해 주세요.";

        AcceptButton = _connect;
        CancelButton = cancel;
        _connect.Click += async (_, _) => await ConnectAsync();
        Shown += (_, _) => _password.Focus();
    }

    private static void AddField(TableLayoutPanel table, int row, string title, Control control)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        table.Controls.Add(new Label { Text = title, AutoSize = true, Margin = new Padding(0, 9, 5, 0) }, 0, row);
        control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        table.Controls.Add(control, 1, row);
    }

    private async Task ConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(_userName.Text) || string.IsNullOrEmpty(_password.Text))
        {
            _status.Text = "DB 사용자와 비밀번호를 입력해 주세요.";
            return;
        }

        try
        {
            _connect.Enabled = false;
            _status.ForeColor = Color.DimGray;
            _status.Text = "DB 연결 확인 중...";
            string connectionString = DatabaseConnectionManager.BuildConnectionString(
                _baseConnectionString,
                _userName.Text.Trim(),
                _password.Text);
            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            if (!DatabaseCredentialStore.Save(new SavedDatabaseCredential(
                    _userName.Text.Trim(),
                    _password.Text)))
            {
                MessageBox.Show("DB 연결은 성공했지만 접속정보를 암호화 저장하지 못했습니다.",
                    "저장 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ConnectionString = connectionString;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            _status.ForeColor = Color.Firebrick;
            _status.Text = "DB 연결 실패: " + ex.Message;
        }
        finally
        {
            _connect.Enabled = true;
        }
    }
}
