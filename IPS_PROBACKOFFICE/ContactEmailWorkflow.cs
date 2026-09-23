using IPS_PROBACKOFFICE.Data;
using IPS_PROBACKOFFICE.Security;

namespace IPS_PROBACKOFFICE;

public sealed class ContactEmailWorkflow(AuthRepository repository, AppUser user, AuditService audit)
{
    public async Task EditOwnEmailAsync(IWin32Window owner)
    {
        try
        {
            using var dialog = new OwnEmailDialog(await repository.GetOwnEmailAsync(user.UserId), async email =>
            {
                await repository.SaveOwnEmailAsync(user.UserId, email);
                await audit.WriteAsync("OWN_EMAIL_UPDATE", user.LoginId);
            });
            BackOfficeTheme.Apply(dialog);
            UiLanguage.Apply(dialog);
            dialog.ShowDialog(owner);
        }
        catch (Exception ex) { MessageBox.Show(owner, ex.Message, "본인 이메일 설정", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    public async Task ComposeAsync(IWin32Window owner, string recipient, string name)
    {
        try
        {
            recipient = ContactEmailMessage.ValidateAddress(recipient, "선택한 담당자의 이메일");
            string from = await repository.GetOwnEmailAsync(user.UserId);
            using var dialog = new ContactEmailDialog(from, recipient, name, audit, async email =>
            {
                await repository.SaveOwnEmailAsync(user.UserId, email);
                await audit.WriteAsync("OWN_EMAIL_UPDATE", user.LoginId);
            });
            BackOfficeTheme.Apply(dialog);
            UiLanguage.Apply(dialog);
            dialog.ShowDialog(owner);
        }
        catch (Exception ex) { MessageBox.Show(owner, ex.Message, "이메일 보내기", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
}

internal sealed class OwnEmailDialog : Form
{
    public OwnEmailDialog(string current, Func<string, Task> save)
    {
        Text = "본인 이메일 설정";
        ClientSize = new Size(530, 210);
        Padding = new Padding(16);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowOnly;
        AutoScaleMode = AutoScaleMode.Font;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 3 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 3; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = "메일 발송에 사용할 본인 회사 이메일을 저장하세요.", AutoSize = true, Margin = new Padding(3, 3, 3, 16) }, 0, 0);
        var email = new TextBox { Text = current, MaxLength = 150, Dock = DockStyle.Fill, ImeMode = ImeMode.Disable };
        layout.Controls.Add(email, 0, 1);
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false, FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0, 16, 0, 0)
        };
        var saveButton = new Button { Text = "저장", AutoSize = true };
        var cancel = new Button { Text = "취소", DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.AddRange([saveButton, cancel]);
        layout.Controls.Add(buttons, 0, 2);
        Controls.Add(layout);
        bool saving = false;
        FormClosing += (_, e) => { if (saving) e.Cancel = true; };
        saveButton.Click += async (_, _) =>
        {
            if (saving) return;
            saving = true; layout.Enabled = false;
            try
            {
                await save(email.Text);
                saving = false;
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "저장 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            finally { saving = false; layout.Enabled = true; }
        };
        AcceptButton = saveButton; CancelButton = cancel;
    }
}

internal sealed class ContactEmailDialog : Form
{
    public ContactEmailDialog(string from, string recipient, string recipientName, AuditService audit, Func<string, Task> saveSender)
    {
        Text = $"이메일 보내기 · {recipientName}";
        ClientSize = new Size(720, 530);
        MinimumSize = new Size(580, 430);
        StartPosition = FormStartPosition.CenterParent;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 5 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 3; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var subject = new TextBox { Dock = DockStyle.Fill, MaxLength = 255 };
        var body = new TextBox { Dock = DockStyle.Fill, Multiline = true, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical };
        var senderEmail = new TextBox { Text = from, MaxLength = 150, Dock = DockStyle.Fill, ImeMode = ImeMode.Disable, PlaceholderText = "보내는 사람 이메일 입력" };
        var senderRow = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        senderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        senderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var saveSenderButton = new Button { Text = "주소 저장", AutoSize = true };
        senderRow.Controls.Add(senderEmail, 0, 0);
        senderRow.Controls.Add(saveSenderButton, 1, 0);
        Add(0, "보내는 사람", senderRow);
        Add(1, "받는 사람", new TextBox { Text = recipient, ReadOnly = true, Dock = DockStyle.Fill });
        Add(2, "제목", subject);
        Add(3, "본문", body);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(3, 12, 3, 3) };
        var send = new Button { Text = "메일 발송", AutoSize = true };
        var cancel = new Button { Text = "취소", DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.AddRange([send, cancel]);
        layout.Controls.Add(buttons, 1, 4);
        Controls.Add(layout);
        CancelButton = cancel;
        bool sending = false;
        bool saving = false;
        FormClosing += (_, e) => { if (sending || saving) e.Cancel = true; };
        saveSenderButton.Click += async (_, _) =>
        {
            if (sending || saving) return;
            saving = true; layout.Enabled = false;
            try
            {
                await saveSender(senderEmail.Text);
                MessageBox.Show(this, "보내는 사람 이메일을 저장했습니다.", "주소 저장", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "저장 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            finally { saving = false; layout.Enabled = true; }
        };
        send.Click += async (_, _) =>
        {
            if (sending || saving) return;
            try
            {
                using var message = ContactEmailMessage.Create(senderEmail.Text, recipient, subject.Text, body.Text);
                sending = true; layout.Enabled = false; send.Text = "발송 중...";
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await ContactEmailTransport.SendAsync(message, timeout.Token);
                await audit.WriteAsync("CONTACT_EMAIL_SEND", recipient);
                MessageBox.Show(this, "메일 서버로 전송했습니다.", "발송 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                sending = false;
                DialogResult = DialogResult.OK;
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show(this, "메일 서버 응답 시간이 초과되었습니다. 중복 발송을 피하려면 수신 여부를 확인한 후 다시 시도하세요.", "발송 결과 확인 필요");
            }
            catch (Exception ex) { MessageBox.Show(this, ContactEmailTransport.DescribeFailure(ex), "메일 발송 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            finally { sending = false; layout.Enabled = true; send.Text = "메일 발송"; }
        };

        void Add(int row, string title, Control control)
        {
            layout.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            layout.Controls.Add(control, 1, row);
        }
    }
}
