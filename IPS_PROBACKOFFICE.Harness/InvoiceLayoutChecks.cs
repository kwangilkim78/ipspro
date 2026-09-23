using InvoiceReportPrinter.WinForms.Configuration;
using InvoiceReportPrinter.WinForms.Models;
using InvoiceReportPrinter.WinForms.Services;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;

internal static class InvoiceLayoutChecks
{
    public static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "IPS_InvoiceLayout_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            VerifyInvoicePagination(root, false);
            VerifyInvoicePagination(root, true);
            Console.WriteLine("PASS: d971a6e invoice layout for individual and bulk: blank page removed, repeated titles/logos, 25-trade pagination, page numbers, totals, borders, institution sorting and USD currency");
        }
        finally
        {
            string fullRoot = Path.GetFullPath(root);
            if (fullRoot.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(fullRoot).StartsWith("IPS_InvoiceLayout_", StringComparison.Ordinal)
                && Directory.Exists(fullRoot))
                Directory.Delete(fullRoot, recursive: true);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    // d971a6e의 인보이스 회귀 검증을 개별 생성/전체저장에 동일하게 실행한다.
static void VerifyInvoicePagination(string harnessDirectory, bool bulk)
{
    string templateFolder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "IPS_PROBACKOFFICE", "Templates"));
    if (!Directory.Exists(templateFolder))
        templateFolder = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "IPS_PROBACKOFFICE", "Templates"));

    var settings = new InvoiceSettings
    {
        TemplateFolder = templateFolder,
        OutputFolder = harnessDirectory,
        SummaryTemplate = "INVOICE_FX_SUMMARY_TEMPLATE.xls"
    };
    var service = new InvoiceService(settings, new TemplateService(settings));
    string CreateInvoice(Institution bank, DateTime month, IReadOnlyList<TradeRow> rows, string currency = "KRW") =>
        service.CreateInvoice(bank, month, rows, currency, bulk ? Path.Combine(harnessDirectory, "bulk") : null);
    var institution = new Institution("1", "Harness Bank", null, null);
    IReadOnlyList<TradeRow> trades = Enumerable.Range(1, 4).Select(index => new TradeRow
    {
        TradeDate = new DateTime(2026, 8, index),
        RefNumber = $"HARNESS-{index}",
        Direction = index % 2 == 0 ? "B" : "S",
        Counterparty = "KB SEOUL",
        Tenor = "1M",
        ExchangeRate = 1376.5m,
        Amount = 1_000_000m,
        ValueDate = new DateTime(2026, 9, index + 1),
        Dealer = index is 1 or 3 ? "Dealer A" : "Dealer B",
        Brokerage = "10000",
        Product = index <= 2 ? "DF" : "NDF 1M",
        InstitutionCode = "1",
        InstitutionName = institution.Name
    }).ToArray();

    string invoicePath = CreateInvoice(institution, new DateTime(2026, 8, 1), trades);
    using var input = File.OpenRead(invoicePath);
    var workbook = new HSSFWorkbook(input);
    ISheet sheet = workbook.GetSheetAt(0);
    var ndfTenorInstitution = new Institution("4", "NDF Tenor Harness", null, null);
    var ndfTenorTrade = new TradeRow
    {
        TradeDate = new DateTime(2026, 8, 14), RefNumber = "HARNESS-NDF-TENOR",
        Direction = "B", Counterparty = "SCB LONDON", Tenor = "NDF 1M",
        Product = "NDF 1M", ExchangeRate = 1376.5m, Amount = 1_000_000m,
        ValueDate = new DateTime(2026, 9, 14), Dealer = "Dealer A",
        Brokerage = "10000", InstitutionCode = "4", InstitutionName = ndfTenorInstitution.Name
    };
    string ndfTenorPath = CreateInvoice(ndfTenorInstitution, new DateTime(2026, 8, 1), [ndfTenorTrade]);
    using var ndfTenorInput = File.OpenRead(ndfTenorPath);
    var ndfTenorWorkbook = new HSSFWorkbook(ndfTenorInput);
    ISheet ndfTenorSheet = ndfTenorWorkbook.GetSheetAt(0);
    int ndfTradeRow = FindText(ndfTenorSheet, "HARNESS-NDF-TENOR")!.Value;
    Require(CellText(ndfTenorSheet, ndfTradeRow, 33) == "1M", "NDF 인보이스 TENOR가 1M이 아닙니다.");
    Require(sheet.RowBreaks.Contains(27), "첫 상세 페이지 앞에 정확한 페이지 나누기가 없습니다.");
    Require(FindText(sheet, "INVOICE - Aug 2026", 28) is int detailTitleRow && detailTitleRow < 35,
        "빈 2페이지 없이 상세 제목이 이어지지 않았습니다.");
    Require(FindText(sheet, "INVOICE SUMMARY - Aug 2026") is not null, "마지막 Summary 제목이 없습니다.");
    int summaryTitleRow = FindText(sheet, "INVOICE SUMMARY - Aug 2026")!.Value;
    Require(FindText(sheet, "SUM", summaryTitleRow) is not null &&
            FindText(sheet, "TOTAL", summaryTitleRow) is not null,
        "Summary 페이지에 SUM 또는 TOTAL이 없습니다.");
    HSSFPicture[] pictures = (sheet.DrawingPatriarch as HSSFPatriarch)?.Children
        .OfType<HSSFPicture>()
        .ToArray() ?? Array.Empty<HSSFPicture>();
    int pictureCount = pictures.Length;
    Require(pictureCount >= 3, "표지·상세·Summary 페이지의 로고가 모두 복사되지 않았습니다.");
    Require(pictures.Any(picture => picture.ClientAnchor is HSSFClientAnchor anchor &&
                                    anchor.Row1 >= 28 &&
                                    anchor.Row1 < summaryTitleRow &&
                                    anchor.Dy1 >= 128),
        "상세 페이지 로고가 반 행 아래로 이동되지 않았습니다.");
    int detailTotalRow = FindText(sheet, "TOTAL -", 28)!.Value;
    int summaryTotalRow = FindText(sheet, "TOTAL", summaryTitleRow)!.Value;
    Require(sheet.GetRow(detailTotalRow).GetCell(71).CellStyle.BorderBottom == BorderStyle.Medium,
        "상세 표 마지막 행에 하단 테두리가 없습니다.");
    Require(sheet.GetRow(summaryTotalRow).GetCell(1).CellStyle.BorderBottom == BorderStyle.Medium &&
            sheet.GetRow(summaryTotalRow).GetCell(66).CellStyle.BorderBottom == BorderStyle.Medium,
        "Summary 표 마지막 행에 하단 테두리가 없습니다.");

    int defaultFirstDataRow = summaryTitleRow + 4;
    Require(CellText(sheet, defaultFirstDataRow, 29) == "Dealer A" &&
            CellText(sheet, defaultFirstDataRow + 1, 29) == "Dealer B" &&
            CellText(sheet, defaultFirstDataRow, 44) == "DF" &&
            CellText(sheet, defaultFirstDataRow + 1, 44) == "DF",
        "기본 인보이스가 상품별/딜러별로 정렬되지 않았습니다.");

    var dealerFirstInstitution = new Institution("2", "IBK Seoul", null, null);
    string dealerFirstPath = CreateInvoice(
        dealerFirstInstitution,
        new DateTime(2026, 8, 1),
        trades);
    using var dealerFirstInput = File.OpenRead(dealerFirstPath);
    var dealerFirstWorkbook = new HSSFWorkbook(dealerFirstInput);
    ISheet dealerFirstSheet = dealerFirstWorkbook.GetSheetAt(0);
    int dealerFirstSummaryTitleRow = FindText(
        dealerFirstSheet,
        "INVOICE SUMMARY - Aug 2026")!.Value;
    int dealerFirstDataRow = dealerFirstSummaryTitleRow + 4;
    Require(CellText(dealerFirstSheet, dealerFirstDataRow, 29) == "Dealer A" &&
            CellText(dealerFirstSheet, dealerFirstDataRow + 1, 29) == "Dealer A" &&
            CellText(dealerFirstSheet, dealerFirstDataRow, 44) == "DF" &&
            CellText(dealerFirstSheet, dealerFirstDataRow + 1, 44) == "NDF 1M",
        "IBK Seoul 인보이스가 딜러별/상품별로 정렬되지 않았습니다.");

    IReadOnlyList<TradeRow> usdTrades = trades
        .Select(trade => new TradeRow
        {
            TradeDate = trade.TradeDate,
            RefNumber = trade.RefNumber,
            Direction = trade.Direction,
            Counterparty = trade.Counterparty,
            Tenor = trade.Tenor,
            ExchangeRate = trade.ExchangeRate,
            Amount = trade.Amount,
            ValueDate = trade.ValueDate,
            Dealer = trade.Dealer,
            Brokerage = trade.Brokerage,
            Product = trade.Product,
            InstitutionCode = "3",
            InstitutionName = "Macquarie Sydney"
        })
        .ToArray();
    var usdInstitution = new Institution("3", "Macquarie Sydney", null, null);
    string usdPath = CreateInvoice(usdInstitution, new DateTime(2026, 8, 1), usdTrades, "USD");
    using var usdInput = File.OpenRead(usdPath);
    var usdWorkbook = new HSSFWorkbook(usdInput);
    ISheet usdSheet = usdWorkbook.GetSheetAt(0);
    int usdSummaryTitleRow = FindText(usdSheet, "INVOICE SUMMARY - Aug 2026")!.Value;
    int usdSumRow = FindExactText(usdSheet, "SUM", usdSummaryTitleRow)!.Value;
    int usdTotalRow = FindExactText(usdSheet, "TOTAL", usdSummaryTitleRow)!.Value;
    int usdDetailTotalRow = FindText(usdSheet, "TOTAL -", 28)!.Value;
    Require(CellText(usdSheet, usdSumRow, 58) == "USD" &&
            CellText(usdSheet, usdTotalRow, 58) == "USD" &&
            CellText(usdSheet, usdDetailTotalRow, 59).EndsWith(" USD", StringComparison.Ordinal),
        "USD 기관의 상세 및 Summary 통화가 USD로 표시되지 않았습니다.");

    IReadOnlyList<TradeRow> manyTrades = Enumerable.Range(1, 25)
        .Select(index => new TradeRow
        {
            TradeDate = new DateTime(2026, 8, Math.Min(index, 25)),
            RefNumber = $"MANY-{index:D2}",
            Direction = "BUY",
            Counterparty = "KB SEOUL",
            Tenor = "1M",
            ExchangeRate = 1376.5m,
            Amount = 1_000_000m,
            ValueDate = new DateTime(2026, 9, 1).AddDays(index),
            Dealer = "Dealer A",
            Brokerage = "10000",
            Product = "DF",
            InstitutionCode = "1",
            InstitutionName = institution.Name
        })
        .ToArray();
    string manyPath = CreateInvoice(
        institution with { Name = "Many Trades Bank" },
        new DateTime(2026, 8, 1),
        manyTrades);
    using var manyInput = File.OpenRead(manyPath);
    var manyWorkbook = new HSSFWorkbook(manyInput);
    ISheet manySheet = manyWorkbook.GetSheetAt(0);
    int firstDetailTitleRow = FindText(manySheet, "INVOICE - Aug 2026", 28)!.Value;
    int secondDetailTitleRow = FindText(
        manySheet,
        "INVOICE - Aug 2026",
        firstDetailTitleRow + 1)!.Value;
    int manySummaryTitleRow = FindText(
        manySheet,
        "INVOICE SUMMARY - Aug 2026",
        secondDetailTitleRow + 1)!.Value;
    Require(CellText(manySheet, firstDetailTitleRow + 3, 73) == "PAGE : 1" &&
            CellText(manySheet, secondDetailTitleRow + 3, 73) == "PAGE : 2",
        "다건 상세 페이지 번호가 순서대로 표시되지 않았습니다.");
    Require(FindText(manySheet, "MANY-01", firstDetailTitleRow) is int firstTradeRow &&
            firstTradeRow < secondDetailTitleRow &&
            FindText(manySheet, "MANY-21", secondDetailTitleRow) is int continuedTradeRow &&
            continuedTradeRow < manySummaryTitleRow,
        "20건을 초과한 거래가 다음 제목 페이지로 분할되지 않았습니다.");
    Require(FindText(manySheet, "TOTAL -", firstDetailTitleRow) is int manyTotalRow &&
            manyTotalRow > secondDetailTitleRow &&
            manyTotalRow < manySummaryTitleRow &&
            Math.Abs(manySheet.GetRow(manyTotalRow).GetCell(71).NumericCellValue - 250000d) < 0.001d,
        "분할 상세의 마지막 페이지에 전체 합계가 표시되지 않았습니다.");
    HSSFPicture[] manyPictures = (manySheet.DrawingPatriarch as HSSFPatriarch)?.Children
        .OfType<HSSFPicture>()
        .ToArray() ?? Array.Empty<HSSFPicture>();
    Require(manyPictures.Count(picture => picture.ClientAnchor is HSSFClientAnchor anchor &&
                                          anchor.Row1 >= firstDetailTitleRow - 2 &&
                                          anchor.Row1 < manySummaryTitleRow) >= 2,
        "분할된 각 상세 페이지에 로고가 반복되지 않았습니다.");
}

static string CellText(ISheet sheet, int rowIndex, int columnIndex) =>
    sheet.GetRow(rowIndex)?.GetCell(columnIndex)?.ToString() ?? string.Empty;

static int? FindExactText(ISheet sheet, string text, int startRow = 0)
{
    for (int rowIndex = Math.Max(sheet.FirstRowNum, startRow); rowIndex <= sheet.LastRowNum; rowIndex++)
    {
        IRow? row = sheet.GetRow(rowIndex);
        if (row?.Cells.Any(cell => string.Equals(
                cell.ToString()?.Trim(),
                text,
                StringComparison.OrdinalIgnoreCase)) == true)
            return rowIndex;
    }
    return null;
}

static int? FindText(ISheet sheet, string text, int startRow = 0)
{
    for (int rowIndex = Math.Max(sheet.FirstRowNum, startRow); rowIndex <= sheet.LastRowNum; rowIndex++)
    {
        IRow? row = sheet.GetRow(rowIndex);
        if (row?.Cells.Any(cell => (cell.ToString() ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase)) == true)
            return rowIndex;
    }
    return null;
}
}
