namespace IPS_PROBACKOFFICE.Models;

public sealed class DealerInfo
{
    public long ExternalUserId { get; init; }
    public long ExternalInstitutionId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string InstCode { get; init; } = string.Empty;
    public string NameKor { get; init; } = string.Empty;
    public string NameEng { get; init; } = string.Empty;
    public string MobilePhone { get; init; } = string.Empty;
    public string WorkPhone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string JobTitle { get; init; } = string.Empty;
    public string RtnsTraderCode { get; init; } = string.Empty;
    public string RtnsTraderEmail { get; init; } = string.Empty;
    public string PermittedIp { get; init; } = string.Empty;
    public string UseYn { get; init; } = "Y";
}
