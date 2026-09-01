using System.Data;
using System.Net;
using System.Net.Mail;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace IPSEBO;

public sealed class KbEodEmailDialog : Form
{
    private const string DefaultTo = "kb1955.kbg@kbfg.com";
    private const string DefaultCc =
        "ipseplatform@ipscorp.co.kr; OPS@ipscorp.co.kr";
    private const string DefaultFrom = "ops_ipspro@ipscorp.co.kr";
    private const string SmtpServer = "mail.ipscorp.co.kr";
    private const int SmtpPort = 25;

    private readonly TextBox _to = new()
    {
        Dock = DockStyle.Fill,
        Text = DefaultTo
    };
    private readonly TextBox _cc = new()
    {
        Dock = DockStyle.Fill,
        Text = DefaultCc
    };
    private readonly TextBox _subject = new() { Dock = DockStyle.Fill };
    private readonly DataGridView _bodyPreview = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        RowHeadersVisible = false,
        BackgroundColor = Color.White,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells
    };
    private readonly DataTable _reportData;

    public KbEodEmailDialog(DateTime reportDate, DataTable reportData)
    {
        _reportData = reportData.Copy();

        Text = "KB EOD 메일 발송";
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("맑은 고딕", 10F);
        BackColor = Color.FromArgb(245, 246, 248);
        ClientSize = new Size(1000, 600);
        MinimizeBox = false;
        MaximizeBox = false;

        _subject.Text = $"KB_DEAL SUMMARY{reportDate:yyyy-MM-dd}";
        _bodyPreview.DataSource = _reportData;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            ColumnCount = 2,
            RowCount = 5,
            BackColor = BackColor
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));

        AddRow(layout, 0, "받는 사람", _to);
        AddRow(layout, 1, "CC", _cc);
        AddRow(layout, 2, "제목", _subject);
        AddRow(layout, 3, "본문", _bodyPreview);

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
        layout.Controls.Add(buttons, 1, 4);
        Controls.Add(layout);
    }

    public string ToAddresses => _to.Text.Trim();
    public string CcAddresses => _cc.Text.Trim();
    public string MailSubject => _subject.Text.Trim();
    public string HtmlBody => BuildHtmlTable(_reportData);

    private static void AddRow(
        TableLayoutPanel layout,
        int row,
        string title,
        Control editor)
    {
        layout.Controls.Add(
            new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            },
            0,
            row);
        layout.Controls.Add(editor, 1, row);
    }

    private static string BuildHtmlTable(DataTable table)
    {
        var html = new StringBuilder();
        html.Append("<table style=\"border-collapse:collapse;font-family:Arial,sans-serif;font-size:10pt;\">");
        html.Append("<thead><tr>");
        foreach (DataColumn column in table.Columns)
        {
            html.Append("<th style=\"border:1px solid #666;padding:5px 8px;background:#f5f6f8;white-space:nowrap;\">")
                .Append(WebUtility.HtmlEncode(column.ColumnName))
                .Append("</th>");
        }
        html.Append("</tr></thead><tbody>");

        foreach (DataRow row in table.Rows)
        {
            html.Append("<tr>");
            foreach (object? value in row.ItemArray)
            {
                html.Append("<td style=\"border:1px solid #666;padding:5px 8px;white-space:nowrap;\">")
                    .Append(WebUtility.HtmlEncode(
                        value == null || value == DBNull.Value
                            ? string.Empty
                            : Convert.ToString(value)))
                    .Append("</td>");
            }
            html.Append("</tr>");
        }

        html.Append("</tbody></table>");
        return html.ToString();
    }

    private async Task SendEmailAsync(Button sendButton)
    {
        if (string.IsNullOrWhiteSpace(ToAddresses))
        {
            MessageBox.Show("받는 사람을 입력해주세요.");
            _to.Focus();
            return;
        }

        sendButton.Enabled = false;
        sendButton.Text = "발송 중...";
        string smtpDiagnostics = "SMTP 사전 진단을 실행하지 못했습니다.";

        try
        {
            smtpDiagnostics = await CaptureSmtpDiagnosticsAsync();

            using var message = new MailMessage
            {
                From = new MailAddress(DefaultFrom),
                Subject = MailSubject,
                SubjectEncoding = Encoding.UTF8,
                Body = HtmlBody,
                BodyEncoding = Encoding.UTF8,
                IsBodyHtml = true
            };

            AddAddresses(message.To, ToAddresses);
            AddAddresses(message.CC, CcAddresses);

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
                ServicePointManager.ServerCertificateValidationCallback =
                    ValidateInternalMailCertificate;
                await smtp.SendMailAsync(message);
            }
            finally
            {
                ServicePointManager.ServerCertificateValidationCallback =
                    previousCallback;
            }

            MessageBox.Show(
                "메일 서버로 전송했습니다.",
                "메일 발송 완료",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (FormatException ex)
        {
            string logPath = WriteFailureLog(ex, smtpDiagnostics);
            MessageBox.Show(
                "메일 주소 형식이 올바르지 않습니다.\n" + ex.Message +
                $"\n\n상세 로그: {logPath}",
                "메일 주소 오류",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        catch (SmtpException ex)
        {
            string logPath = WriteFailureLog(ex, smtpDiagnostics);
            MessageBox.Show(
                $"송신 메일 서버({SmtpServer})로 메일 전송에 실패했습니다.\n\n" +
                $"StatusCode: {ex.StatusCode}\n" +
                $"Message: {ex.Message}\n" +
                $"InnerException: {ex.InnerException?.Message ?? "없음"}\n\n" +
                $"상세 로그: {logPath}",
                "메일 발송 실패",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            string logPath = WriteFailureLog(ex, smtpDiagnostics);
            MessageBox.Show(
                "메일 전송 중 오류가 발생했습니다.\n\n" + ex.Message +
                $"\n\n상세 로그: {logPath}",
                "메일 발송 오류",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            sendButton.Enabled = true;
            sendButton.Text = "메일 발송";
        }
    }

    private static void AddAddresses(
        MailAddressCollection collection,
        string addresses)
    {
        foreach (string address in addresses.Split(
                     [';', ','],
                     StringSplitOptions.RemoveEmptyEntries
                     | StringSplitOptions.TrimEntries))
        {
            collection.Add(new MailAddress(address));
        }
    }

    private static async Task<string> CaptureSmtpDiagnosticsAsync()
    {
        var result = new StringBuilder();
        result.AppendLine($"진단 시각: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        result.AppendLine($"SMTP 서버: {SmtpServer}:{SmtpPort}");

        IPAddress[] addresses = await Dns.GetHostAddressesAsync(SmtpServer);
        result.AppendLine(
            "DNS 결과: " +
            string.Join(", ", addresses.Select(x => x.ToString())));

        using var tcp = new TcpClient();
        await tcp.ConnectAsync(SmtpServer, SmtpPort)
            .WaitAsync(TimeSpan.FromSeconds(5));

        result.AppendLine($"LocalEndPoint: {tcp.Client.LocalEndPoint}");
        result.AppendLine($"RemoteEndPoint: {tcp.Client.RemoteEndPoint}");

        using NetworkStream stream = tcp.GetStream();
        using var reader = new StreamReader(
            stream,
            Encoding.ASCII,
            false,
            1024,
            leaveOpen: true);
        using var writer = new StreamWriter(
            stream,
            Encoding.ASCII,
            1024,
            leaveOpen: true)
        {
            NewLine = "\r\n",
            AutoFlush = true
        };

        string banner = await ReadSmtpResponseAsync(reader);
        result.AppendLine("서버 배너:");
        result.AppendLine(banner);

        await writer.WriteLineAsync($"EHLO {Dns.GetHostName()}");
        string ehlo = await ReadSmtpResponseAsync(reader);
        result.AppendLine("EHLO 응답:");
        result.AppendLine(ehlo);
        result.AppendLine(
            "STARTTLS 광고 여부: " +
            (ehlo.Contains("STARTTLS", StringComparison.OrdinalIgnoreCase)
                ? "YES"
                : "NO"));

        await writer.WriteLineAsync("QUIT");
        return result.ToString();
    }

    private static async Task<string> ReadSmtpResponseAsync(
        StreamReader reader)
    {
        var response = new StringBuilder();
        string? firstLine = await reader.ReadLineAsync()
            .WaitAsync(TimeSpan.FromSeconds(5));
        if (firstLine == null)
            return "응답 없음";

        response.AppendLine(firstLine);
        bool more = firstLine.Length >= 4 && firstLine[3] == '-';
        while (more)
        {
            string? line = await reader.ReadLineAsync()
                .WaitAsync(TimeSpan.FromSeconds(5));
            if (line == null)
                break;

            response.AppendLine(line);
            more = line.Length >= 4 && line[3] == '-';
        }

        return response.ToString().TrimEnd();
    }

    private string WriteFailureLog(
        Exception exception,
        string smtpDiagnostics)
    {
        string desktop = Environment.GetFolderPath(
            Environment.SpecialFolder.DesktopDirectory);
        string path = Path.Combine(
            desktop,
            $"IPS_PROBO_MAIL_ERROR_{DateTime.Now:yyyyMMdd_HHmmss}.log");

        var log = new StringBuilder();
        log.AppendLine("=== IPS PROBO KB EOD MAIL ERROR ===");
        log.AppendLine($"발생 시각: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        log.AppendLine($"PC 이름: {Environment.MachineName}");
        log.AppendLine($"Windows 사용자: {Environment.UserName}");
        log.AppendLine($"발신자: {DefaultFrom}");
        log.AppendLine($"받는 사람: {ToAddresses}");
        log.AppendLine($"CC: {CcAddresses}");
        log.AppendLine($"제목: {MailSubject}");
        log.AppendLine($"TLS 사용: True");
        log.AppendLine("TLS 인증서 정책: 내부 서버의 이름 불일치/인증서 체인 오류 허용");
        log.AppendLine($"SMTP 인증: {DefaultFrom} / 빈 비밀번호");
        log.AppendLine();
        log.AppendLine("=== SMTP 사전 진단 ===");
        log.AppendLine(smtpDiagnostics);
        log.AppendLine();
        log.AppendLine("=== Exception.ToString() ===");
        log.AppendLine(exception.ToString());

        File.WriteAllText(path, log.ToString(), new UTF8Encoding(true));
        return path;
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

        // 내부 SMTP 서버에서 확인된 인증서 이름/체인 오류만 허용한다.
        // 인증서를 제시하지 않은 경우(RemoteCertificateNotAvailable)는 계속 차단한다.
        return (sslPolicyErrors & ~allowedErrors) == SslPolicyErrors.None;
    }
}
