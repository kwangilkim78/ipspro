using System.Net;
using System.Net.Mail;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace IPS_PROBACKOFFICE;

public sealed class FeeSummaryEmailDialog : Form
{
    private const string DefaultFrom = "ops_ipspro@ipscorp.co.kr";
    private const string SmtpServer = "mail.ipscorp.co.kr";
    private const int SmtpPort = 25;

    private readonly TextBox _to = new()
    {
        Dock = DockStyle.Fill,
        Text = "jyhan@ipscorp.co.kr",
        ImeMode = ImeMode.Disable
    };
    private readonly TextBox _cc = new()
    {
        Dock = DockStyle.Fill,
        Text = "OPS@ipscorp.co.kr; ipseplatform@ipscorp.co.kr",
        ImeMode = ImeMode.Disable
    };
    private readonly TextBox _subject = new() { Dock = DockStyle.Fill };
    private readonly WebBrowser _bodyPreview = new()
    {
        Dock = DockStyle.Fill,
        AllowWebBrowserDrop = false,
        IsWebBrowserContextMenuEnabled = false,
        WebBrowserShortcutsEnabled = false
    };
    private readonly string _htmlBody;

    public FeeSummaryEmailDialog(DateTime reportDate, string htmlBody)
    {
        _htmlBody = htmlBody;
        _subject.Text = $"전자거래사업본부 거래현황보고 - {reportDate:yyyy.MM.dd}";

        Text = "대표이사 보고 메일 발송";
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("맑은 고딕", 10F);
        BackColor = Color.FromArgb(245, 246, 248);
        ClientSize = new Size(950, 650);
        MinimizeBox = false;
        MaximizeBox = false;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            ColumnCount = 2,
            RowCount = 6,
            BackColor = BackColor
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (int i = 0; i < 4; i++)
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));

        AddRow(layout, 0, "보내는 사람", new Label
        {
            Text = DefaultFrom,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        });
        AddRow(layout, 1, "받는 사람", _to);
        AddRow(layout, 2, "CC", _cc);
        AddRow(layout, 3, "제목", _subject);
        AddRow(layout, 4, "본문", _bodyPreview);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 8, 0, 0),
            BackColor = BackColor
        };
        var cancel = new Button { Text = "취소", AutoSize = true };
        var send = new Button { Text = "메일 발송", AutoSize = true };
        cancel.Click += (_, _) => Close();
        send.Click += async (_, _) => await SendEmailAsync(send);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(send);
        layout.Controls.Add(buttons, 1, 5);
        Controls.Add(layout);

        _to.TextChanged += (_, _) => NormalizeEmailText(_to);
        _cc.TextChanged += (_, _) => NormalizeEmailText(_cc);

        Shown += (_, _) => _bodyPreview.DocumentText = _htmlBody;
    }

    private static void NormalizeEmailText(TextBox textBox)
    {
        string original = textBox.Text;
        int selectionStart = textBox.SelectionStart;
        string normalized = NormalizeEmailCharacters(original);
        if (string.Equals(original, normalized, StringComparison.Ordinal))
            return;

        string normalizedPrefix = NormalizeEmailCharacters(
            original[..Math.Min(selectionStart, original.Length)]);
        textBox.Text = normalized;
        textBox.SelectionStart = Math.Min(normalizedPrefix.Length, normalized.Length);
    }

    private static string NormalizeEmailCharacters(string value)
    {
        var result = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            if (char.IsWhiteSpace(character) || character == '\u3000')
                continue;

            // 전각 ASCII(！~～)를 일반 반각 ASCII(!~~)로 변환한다.
            if (character is >= '\uFF01' and <= '\uFF5E')
                result.Append((char)(character - 0xFEE0));
            else
                result.Append(character);
        }
        return result.ToString();
    }

    private static void AddRow(TableLayoutPanel layout, int row, string title, Control editor)
    {
        layout.Controls.Add(new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent
        }, 0, row);
        layout.Controls.Add(editor, 1, row);
    }

    private async Task SendEmailAsync(Button sendButton)
    {
        if (string.IsNullOrWhiteSpace(_to.Text))
        {
            MessageBox.Show("받는 사람을 입력해주세요.");
            _to.Focus();
            return;
        }

        sendButton.Enabled = false;
        sendButton.Text = "발송 중...";
        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(DefaultFrom),
                Subject = _subject.Text.Trim(),
                SubjectEncoding = Encoding.UTF8,
                Body = _htmlBody,
                BodyEncoding = Encoding.UTF8,
                IsBodyHtml = true
            };
            AddAddresses(message.To, _to.Text);
            AddAddresses(message.CC, _cc.Text);

            using var smtp = new SmtpClient(SmtpServer, SmtpPort)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(DefaultFrom, string.Empty),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 30000
            };

            RemoteCertificateValidationCallback? previousCallback =
                ServicePointManager.ServerCertificateValidationCallback;
            try
            {
                ServicePointManager.ServerCertificateValidationCallback = ValidateInternalMailCertificate;
                await smtp.SendMailAsync(message);
            }
            finally
            {
                ServicePointManager.ServerCertificateValidationCallback = previousCallback;
            }

            MessageBox.Show("메일 서버로 전송했습니다.", "메일 발송 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (FormatException ex)
        {
            MessageBox.Show("메일 주소 형식이 올바르지 않습니다.\n" + ex.Message, "메일 주소 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "메일 발송 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            sendButton.Enabled = true;
            sendButton.Text = "메일 발송";
        }
    }

    private static void AddAddresses(MailAddressCollection collection, string addresses)
    {
        foreach (string address in addresses.Split(
                     [';', ','],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            collection.Add(new MailAddress(address));
    }

    private static bool ValidateInternalMailCertificate(
        object sender,
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
    {
        const SslPolicyErrors allowedErrors =
            SslPolicyErrors.RemoteCertificateNameMismatch |
            SslPolicyErrors.RemoteCertificateChainErrors;
        return (sslPolicyErrors & ~allowedErrors) == SslPolicyErrors.None;
    }
}
