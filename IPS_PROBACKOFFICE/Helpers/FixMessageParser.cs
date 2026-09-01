namespace IPS_PROBACKOFFICE.Helpers;

public sealed record FixField(int Sequence, string Tag, string Name, string Value);

public static class FixMessageParser
{
    private static readonly IReadOnlyDictionary<string, string> Names =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["8"] = "BeginString", ["9"] = "BodyLength", ["10"] = "CheckSum",
            ["11"] = "ClOrdID", ["14"] = "CumQty", ["15"] = "Currency",
            ["17"] = "ExecID", ["21"] = "HandlInst", ["22"] = "SecurityIDSource",
            ["31"] = "LastPx", ["32"] = "LastQty", ["34"] = "MsgSeqNum",
            ["35"] = "MsgType", ["37"] = "OrderID", ["38"] = "OrderQty",
            ["39"] = "OrdStatus", ["40"] = "OrdType", ["41"] = "OrigClOrdID",
            ["44"] = "Price", ["49"] = "SenderCompID", ["52"] = "SendingTime",
            ["54"] = "Side", ["55"] = "Symbol", ["56"] = "TargetCompID",
            ["58"] = "Text", ["59"] = "TimeInForce", ["60"] = "TransactTime",
            ["63"] = "SettlType", ["64"] = "SettlDate", ["75"] = "TradeDate",
            ["150"] = "ExecType", ["151"] = "LeavesQty", ["167"] = "SecurityType",
            ["207"] = "SecurityExchange", ["453"] = "NoPartyIDs",
            ["448"] = "PartyID", ["447"] = "PartyIDSource", ["452"] = "PartyRole",
            ["461"] = "CFICode", ["541"] = "MaturityDate"
        };

    public static IReadOnlyList<FixField> Parse(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return Array.Empty<FixField>();

        string normalized = message.Replace("^A", "\u0001", StringComparison.OrdinalIgnoreCase);
        char separator = normalized.Contains('\u0001') ? '\u0001' : '|';
        string[] parts = normalized.Split(separator, StringSplitOptions.RemoveEmptyEntries);
        var fields = new List<FixField>(parts.Length);

        foreach (string rawPart in parts)
        {
            string part = rawPart.Trim();
            int equals = part.IndexOf('=');
            if (equals <= 0)
                continue;

            string tag = part[..equals].Trim();
            string value = part[(equals + 1)..].Trim();
            fields.Add(new FixField(
                fields.Count + 1,
                tag,
                Names.TryGetValue(tag, out string? name) ? name : "Unknown",
                value));
        }

        return fields;
    }

    public static string ToDisplayText(string? message) =>
        (message ?? string.Empty)
            .Replace("^A", "|", StringComparison.OrdinalIgnoreCase)
            .Replace('\u0001', '|');
}
