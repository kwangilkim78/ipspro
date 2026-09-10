using InvoiceReportPrinter.WinForms.Configuration;
using InvoiceReportPrinter.WinForms.Models;
using InvoiceReportPrinter.WinForms.Services;
using NPOI.HSSF.UserModel;

internal static class InvoiceSaveChecks
{
    public static void Run(bool includePdf = false)
    {
        string root = Path.Combine(Path.GetTempPath(), "IPS_InvoiceSave_" + Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(root, "2026년8월 인보이스");
        try
        {
            var settings = new InvoiceSettings();
            var service = new InvoiceService(settings, new TemplateService(settings));
            var first = new Institution("1001", "Sample Bank A", null, null);
            var second = new Institution("1002", "Sample Bank B", null, null);
            DateTime month = new(2026, 8, 1);
            TradeRow[] rows = [new()
            {
                TradeDate = new DateTime(2026, 8, 10), RefNumber = "TEST-1", Direction = "BUY",
                Counterparty = "Counterparty", Tenor = "SP", ExchangeRate = 1350m,
                Amount = 1000000m, ValueDate = new DateTime(2026, 8, 12),
                Dealer = "Sample Dealer", Brokerage = "12345", Product = "DF"
            }];
            string firstPath = service.CreateInvoice(first, month, rows, "KRW", folder);
            byte[] original = File.ReadAllBytes(firstPath);
            string secondPath = service.CreateInvoice(second, month, rows, "USD", folder);
            string repeatedPath = service.CreateInvoice(first, month, rows, "KRW", folder);
            Require(Path.GetDirectoryName(firstPath) == folder, "월별 지정 폴더에 저장되지 않았습니다.");
            Require(firstPath != secondPath && firstPath != repeatedPath, "기관별/재저장 파일이 충돌합니다.");
            Require(original.SequenceEqual(File.ReadAllBytes(firstPath)), "기존 파일이 변경되었습니다.");
            foreach (var (path, bank, currency) in new[]
                     { (firstPath, first.Name, "KRW"), (secondPath, second.Name, "USD"), (repeatedPath, first.Name, "KRW") })
            {
                using var stream = File.OpenRead(path);
                using var workbook = new HSSFWorkbook(stream);
                var sheet = workbook.GetSheetAt(0);
                Require(workbook.NumberOfSheets == 1, "기존 인보이스의 단일 시트 구성이 변경되었습니다.");
                var texts = new List<string>();
                for (int r = 0; r <= sheet.LastRowNum; r++)
                    if (sheet.GetRow(r) is { } row)
                        texts.AddRange(row.Cells.Select(cell => cell.ToString() ?? ""));
                Require(texts.Contains(bank), "기관 이름이 잘못 저장되었습니다.");
                Require(texts.Any(text => text.Contains("Aug 2026")), "선택한 월이 잘못 저장되었습니다.");
                Require(texts.Any(text => text.Contains(currency)), "기관 수수료 통화가 누락되었습니다.");
                Require(texts.Contains("TEST-1"), "거래 내역이 누락되었습니다.");
            }
            Require(!Directory.EnumerateFiles(folder, "*.tmp").Any(), "임시 파일이 남았습니다.");
            bool emptyRejected = false;
            try { service.CreateInvoice(first, month, Array.Empty<TradeRow>(), "KRW", folder); }
            catch (InvalidOperationException) { emptyRejected = true; }
            Require(emptyRejected && Directory.GetFiles(folder).Length == 3, "빈 거래의 인보이스가 생성됐습니다.");
            if (includePdf)
            {
                string pdf = InvoicePdfExporter.ExportAsync(firstPath).GetAwaiter().GetResult();
                byte[] pdfBytes = File.ReadAllBytes(pdf);
                Require(System.Text.Encoding.ASCII.GetString(pdfBytes, 0, 5) == "%PDF-", "PDF 형식 오류");
                string duplicatePdf = InvoicePdfExporter.ExportAsync(firstPath).GetAwaiter().GetResult();
                Require(pdf != duplicatePdf && pdfBytes.SequenceEqual(File.ReadAllBytes(pdf)), "기존 PDF가 덮어써졌습니다.");
                Require(original.SequenceEqual(File.ReadAllBytes(firstPath)), "PDF 변환 중 원본 엑셀이 변경됐습니다.");
                Console.WriteLine("PASS: installed Excel PDF export, duplicate preservation, source workbook unchanged");
            }
            Console.WriteLine("PASS: invoice monthly output folder, separate banks, currency/month/trade contents, existing-file preservation, empty rows, temporary cleanup");
        }
        finally
        {
            // 테스트에서 만든 고유 폴더만 삭제한다.
            string fullRoot = Path.GetFullPath(root);
            string tempRoot = Path.GetFullPath(Path.GetTempPath());
            if (fullRoot.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(fullRoot).StartsWith("IPS_InvoiceSave_", StringComparison.Ordinal)
                && Directory.Exists(fullRoot))
                Directory.Delete(fullRoot, recursive: true);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
