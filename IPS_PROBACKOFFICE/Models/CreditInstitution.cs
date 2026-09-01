namespace IPS_PROBACKOFFICE.Models;

public sealed record CreditInstitution(long CreditorId, string Name, bool IsActive)
{
    public string DisplayName => IsActive
        ? $"{Name} ({CreditorId})"
        : $"{Name} ({CreditorId}) - 비활성";
}
