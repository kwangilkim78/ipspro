namespace IPS_PROBACKOFFICE.Models;

public sealed class SwitchFeeRuleRow
{
    public string InstCode { get; init; } = string.Empty;
    public string InstitutionName { get; init; } = string.Empty;
    public string ProductCode { get; init; } = string.Empty;
    public string Direction { get; init; } = string.Empty;
    public string? LegType { get; init; }
    public decimal FeePerMillion { get; set; }
    public DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
}
