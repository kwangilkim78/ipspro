namespace IPS_PROBACKOFFICE.Models;

public sealed class ApiFeeExemptionRow
{
    public long Id { get; set; }
    public string InstitutionCode { get; set; } = string.Empty;
    public string InstitutionName { get; set; } = string.Empty;
    public string TraderId { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDateInclusive { get; set; }
    public bool IsActive { get; set; }
    public string? Remarks { get; set; }
}

public sealed record ApiFeeExemptionInstitution(string Code, string Name)
{
    public override string ToString() => $"{Name} ({Code})";
}
