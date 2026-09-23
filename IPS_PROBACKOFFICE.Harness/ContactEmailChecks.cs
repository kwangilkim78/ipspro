using IPS_PROBACKOFFICE;

internal static class ContactEmailChecks
{
    public static void Run()
    {
        using var key = System.Security.Cryptography.RSA.Create(2048);
        var request = new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=mail.ipscorp.co.kr", key,
            System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        using var unrelated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        if (ContactEmailTransport.ValidateCertificate(new object(), unrelated, null, System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors)
            || ContactEmailTransport.ValidateCertificate(new object(), null, null, System.Net.Security.SslPolicyErrors.RemoteCertificateNotAvailable))
            throw new InvalidOperationException("알 수 없거나 누락된 인증서를 허용했습니다.");
        using var message = ContactEmailMessage.Create(" sender@example.com ", "dealer@example.com", "기관 안내", "안녕하세요.\r\n확인 부탁드립니다.");
        if (message.From?.Address != "sender@example.com" || message.To.Count != 1
            || message.To[0].Address != "dealer@example.com" || message.Subject != "기관 안내"
            || message.BodyEncoding?.WebName != "utf-8" || message.IsBodyHtml)
            throw new InvalidOperationException("메일 작성 결과 오류");
        foreach (string invalid in new[] { "", "invalid", "a@example.com;b@example.com", "a@example.com\r\nBcc: b@example.com", "Someone <a@example.com>" })
        {
            Reject(() => ContactEmailMessage.Create(invalid, "dealer@example.com", "제목", "본문"));
            Reject(() => ContactEmailMessage.Create("sender@example.com", invalid, "제목", "본문"));
        }
        Reject(() => ContactEmailMessage.Create("a@example.com", "b@example.com", " ", "본문"));
        Reject(() => ContactEmailMessage.Create("a@example.com", "b@example.com", "제목\r\nBcc: c@example.com", "본문"));
        Reject(() => ContactEmailMessage.Create("a@example.com", "b@example.com", "제목", " "));
        Console.WriteLine("HARNESS PASS: 발신/수신 주소 검증, 헤더 삽입 차단, 필수 제목/본문, 한글 UTF-8 메일 작성 (실제 발송 없음)");
    }

    private static void Reject(Func<System.Net.Mail.MailMessage> create)
    {
        try { using var unexpected = create(); }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("유효하지 않은 메일을 허용했습니다.");
    }
}
