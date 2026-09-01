namespace InvoiceReportPrinter.WinForms.Models;

public sealed record Institution(
    string InstCode,
    string Name,
    string? Emails,
    string? RtnsOrgCode,
    long ExternalInstitutionId = 0,
    string Slug = "",
    bool Enabled = true)
{
    public override string ToString() => Name;
}
