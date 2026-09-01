using InvoiceReportPrinter.WinForms.Configuration;
using InvoiceReportPrinter.WinForms.Models;

namespace InvoiceReportPrinter.WinForms.Services;

public sealed class TemplateService
{
    private readonly InvoiceSettings _settings;

    public TemplateService(InvoiceSettings settings)
    {
        _settings = settings;
    }

    public bool IsSwitchInstitution(Institution institution)
    {
        return _settings.SwitchInstitutions.Any(key =>
            institution.Name.Contains(
                key,
                StringComparison.OrdinalIgnoreCase));
    }

    public string ResolveTemplatePath(Institution institution)
    {
        string fileName = string.IsNullOrWhiteSpace(_settings.SummaryTemplate)
            ? "INVOICE_FX_SUMMARY_TEMPLATE.xls"
            : _settings.SummaryTemplate;

        string templateFolder = string.IsNullOrWhiteSpace(_settings.TemplateFolder)
            ? "Templates"
            : _settings.TemplateFolder;

        string templatePath = Path.Combine(
            AppContext.BaseDirectory,
            templateFolder,
            fileName);

        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException(
                $"Template 파일이 없습니다.{Environment.NewLine}" +
                $"파일명: {fileName}{Environment.NewLine}" +
                $"확인 경로: {templatePath}",
                templatePath);
        }

        return templatePath;
    }
}
