namespace IPS_PROBACKOFFICE.Models;

public sealed record CreditMatrixEntry(
    long InstitutionId,
    string InstitutionName,
    long CounterpartyId,
    string CounterpartyName,
    string AssetClass,
    string Tenor,
    decimal LimitAmount,
    decimal AvailableAmount,
    bool IsActive);
