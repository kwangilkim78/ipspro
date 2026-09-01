namespace InvoiceReportPrinter.WinForms.Models;

public sealed class TradeRow
{
    public DateTime? TradeDate { get; init; }
    public string RefNumber { get; init; } = string.Empty;
    public string Direction { get; init; } = string.Empty;
    public string Counterparty { get; init; } = string.Empty;
    public string Tenor { get; init; } = string.Empty;
    public decimal? ExchangeRate { get; init; }
    public decimal? Amount { get; init; }
    public DateTime? ValueDate { get; init; }
    public string Dealer { get; init; } = string.Empty;
    public string Brokerage { get; init; } = string.Empty;
    public decimal BrokerageAmount =>
        decimal.TryParse(
            Brokerage,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out decimal value)
            ? value
            : decimal.TryParse(Brokerage, out value)
                ? value
                : 0m;
    public string Product { get; init; } = string.Empty;
    public string InstitutionCode { get; init; } = string.Empty;
    public string InstitutionName { get; init; } = string.Empty;
    public string? DealerEmail { get; init; }
    public string? SwitchClient { get; init; }
    public string? SwitchBank { get; init; }
    public bool IsSwitch =>
        !string.IsNullOrWhiteSpace(SwitchClient)
        || !string.IsNullOrWhiteSpace(SwitchBank);
}
