namespace IPS_PROBACKOFFICE.Models;

public sealed record FxValueDateRow(
    DateTime TradeDate,
    string Tenor,
    DateTime StartDate,
    DateTime EndDate,
    int DayCount,
    int DayAdjustment,
    string RawText);

