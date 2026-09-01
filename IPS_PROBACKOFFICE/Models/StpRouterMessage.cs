namespace IPS_PROBACKOFFICE.Models;

public sealed record StpRouterMessage(
    int MessageId,
    string ReportId,
    bool Ack,
    int RuleId,
    int ChannelId,
    string OrigReportId,
    string TradeDate,
    string OrigOrderId,
    string Status,
    bool IsTransmitted,
    DateTime Created,
    string Message);
