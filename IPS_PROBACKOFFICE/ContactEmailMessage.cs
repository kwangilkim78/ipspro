using System.Net.Mail;
using System.Text;

namespace IPS_PROBACKOFFICE;

public static class ContactEmailMessage
{
    public static string ValidateAddress(string value, string label)
    {
        value = value.Trim();
        if (value.Length == 0 || value.Length > 254 || value.Contains('\r') || value.Contains('\n')
            || !MailAddress.TryCreate(value, out var address) || !string.Equals(address.Address, value, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"{label}에 올바른 이메일 주소 하나를 입력하세요.");
        return value;
    }

    public static MailMessage Create(string from, string to, string subject, string body)
    {
        from = ValidateAddress(from, "보내는 사람");
        to = ValidateAddress(to, "받는 사람");
        if (string.IsNullOrWhiteSpace(subject) || subject.Contains('\r') || subject.Contains('\n'))
            throw new ArgumentException("제목을 한 줄로 입력하세요.");
        if (string.IsNullOrWhiteSpace(body)) throw new ArgumentException("본문을 입력하세요.");
        return new MailMessage(from, to)
        {
            Subject = subject.Trim(), Body = body, IsBodyHtml = false,
            SubjectEncoding = Encoding.UTF8, BodyEncoding = Encoding.UTF8
        };
    }
}
