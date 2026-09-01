namespace InvoiceReportPrinter.WinForms.Helpers;

public static class ProductClassifier
{
    private static readonly string[] Order =
    [
        "DF 1M", "NDF 1M", "NDF MAR", "NDF TOMMAR", "BASIS MAR", "BASIS TOMMAR"
    ];

    public static string Classify(string? type, string? tenor)
    {
        string t = (type ?? string.Empty).Trim().ToUpperInvariant();
        string n = (tenor ?? string.Empty).Trim().ToUpperInvariant().Replace("_", string.Empty).Replace(" ", string.Empty);

        if (t.Contains("BASIS"))
            return t.Contains("TOMMAR") || n.Contains("TOMMAR")
                ? "BASIS TOMMAR"
                : "BASIS MAR";
        if (t.Contains("NDF"))
        {
            if (t.Contains("TOMMAR") || n.Contains("TOMMAR")) return "NDF TOMMAR";
            if (t.Contains("MAR") || n.Contains("MAR")) return "NDF MAR";
            return "NDF 1M";
        }
        return "DF 1M";
    }

    public static int SortIndex(string product)
    {
        int index = Array.IndexOf(Order, product);
        return index < 0 ? int.MaxValue : index;
    }
}
