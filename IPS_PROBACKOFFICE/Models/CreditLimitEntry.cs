namespace IPS_PROBACKOFFICE.Models;

public sealed record CreditLimitEntry(
    long CounterpartyId,
    string CounterpartyName,
    string AssetClass,
    string Tenor,
    decimal LimitAmount,
    bool IsActive);
