namespace InvoiceReportPrinter.WinForms.Configuration;

public sealed class InvoiceSettings
{
    public string TemplateFolder { get; set; } = "Templates";
    public string OutputFolder { get; set; } = "Output";
    public string SummaryTemplate { get; set; } = "INVOICE_FX_SUMMARY_TEMPLATE.xls";
    public string SwitchTemplate { get; set; } = "SWITCH.xlsx.xls";
    public string NonSwitchTemplate { get; set; } = "NON SWITCH.xlsx.xls";
    public string FrontSheetName { get; set; } = "front";
    public string FrontInstitutionCell { get; set; } = "B2";
    public string FrontInvoiceMonthCell { get; set; } = "B3";
    public string FrontGeneratedAtCell { get; set; } = "B4";
    public string[] SwitchInstitutions { get; set; } = ["KB", "HANA"];
}
