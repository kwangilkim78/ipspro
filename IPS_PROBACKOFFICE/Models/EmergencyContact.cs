namespace IPS_PROBACKOFFICE.Models;

public sealed record EmergencyContact
{
    public long Id { get; init; }
    public long InstitutionId { get; init; }
    public string InstitutionName { get; init; } = "";
    public string Category { get; init; } = "딜러";
    public string ContactName { get; init; } = "";
    public string WorkPhone { get; init; } = "";
    public string MobilePhone { get; init; } = "";
    public string Email { get; init; } = "";
    public int Priority { get; init; } = 1;
    public string Remarks { get; init; } = "";
    public string UpdatedBy { get; init; } = "";
    public DateTime UpdatedAt { get; init; }

    public void Validate()
    {
        if (InstitutionId <= 0) throw new ArgumentException("기관을 선택하세요.");
        if (Category is not ("딜러" or "IT" or "백오피스")) throw new ArgumentException("업무 구분을 선택하세요.");
        if (string.IsNullOrWhiteSpace(ContactName)) throw new ArgumentException("담당자명을 입력하세요.");
        if (string.IsNullOrWhiteSpace(WorkPhone) && string.IsNullOrWhiteSpace(MobilePhone))
            throw new ArgumentException("회사전화 또는 휴대전화를 하나 이상 입력하세요.");
        if (ContactName.Length > 100 || WorkPhone.Length > 50 || MobilePhone.Length > 50 || Email.Length > 254 || Remarks.Length > 1000)
            throw new ArgumentException("입력 가능한 글자 수를 초과했습니다.");
        if (Priority is < 1 or > 99) throw new ArgumentException("연락 순서는 1~99 사이로 입력하세요.");
        if (Email.Length > 0 && !System.Net.Mail.MailAddress.TryCreate(Email, out _))
            throw new ArgumentException("이메일 형식을 확인하세요.");
    }
}
