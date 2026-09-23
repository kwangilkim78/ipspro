using System.Net.Mail;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using MailKit.Security;
using MimeKit;

namespace IPS_PROBACKOFFICE;

public static class ContactEmailTransport
{
    private const string Host = "mail.ipscorp.co.kr";
    // 사내 AntiSpam 서버에서 확인한 인증서. 갱신 시 새 인증서를 확인한 뒤 지문을 변경한다.
    private const string CertificateSha256 = "F532D2DDA89C4374B6B61B7BF180F378304FB0FC119D5DB87C041D186CE6BA57";

    public static bool ValidateCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors)
    {
        if (certificate is null) return false;
        if (errors == SslPolicyErrors.None) return true;
        if ((errors & SslPolicyErrors.RemoteCertificateNotAvailable) != 0) return false;
        using var leaf = X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());
        var now = DateTime.UtcNow;
        return now >= leaf.NotBefore.ToUniversalTime() && now <= leaf.NotAfter.ToUniversalTime()
            && string.Equals(leaf.GetCertHashString(HashAlgorithmName.SHA256), CertificateSha256, StringComparison.OrdinalIgnoreCase);
    }

    private static MailKit.Net.Smtp.SmtpClient CreateClient() => new()
    {
        Timeout = 30000,
        ServerCertificateValidationCallback = ValidateCertificate
    };

    public static async Task SendAsync(MailMessage message, CancellationToken cancellationToken)
    {
        using var client = CreateClient();
        await client.ConnectAsync(Host, 25, SecureSocketOptions.StartTls, cancellationToken);
        // 사내 SMTP는 AUTH를 제공하지 않는 IP 기반 릴레이이며 빈 암호로 인증하지 않는다.
        using var mime = MimeMessage.CreateFromMailMessage(message);
        await client.SendAsync(mime, cancellationToken);
        // 서버가 수락한 뒤 QUIT 실패를 발송 실패로 표시하면 중복 발송될 수 있다.
        try { await client.DisconnectAsync(true, cancellationToken); }
        catch { /* SendAsync가 성공했으므로 서버 수락 상태를 유지한다. */ }
    }

    public static async Task<string> CheckConnectionAsync(CancellationToken cancellationToken)
    {
        using var client = CreateClient();
        await client.ConnectAsync(Host, 25, SecureSocketOptions.StartTls, cancellationToken);
        string result = $"SMTP 연결 성공: {Host}:25; TLS={client.IsSecure}; 인증서 검증 통과 (메일 발송 없음)";
        await client.DisconnectAsync(true, cancellationToken);
        return result;
    }

    public static string DescribeFailure(Exception exception)
    {
        var details = new List<string>();
        for (Exception? current = exception; current is not null; current = current.InnerException)
            details.Add($"{current.GetType().Name}: {current.Message}");
        return $"메일 서버: {Host}:25\n" + string.Join("\n", details);
    }
}
