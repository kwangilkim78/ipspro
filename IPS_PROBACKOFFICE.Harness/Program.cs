using ClosedXML.Excel;
using IPS_PROBACKOFFICE;
using IPS_PROBACKOFFICE.Services;
using System.Data;

if (args.Contains("--invoice-layout"))
{
    InvoiceLayoutChecks.Run();
    return;
}

if (args.Contains("--invoice-save") || args.Contains("--invoice-pdf"))
{
    InvoiceSaveChecks.Run(args.Contains("--invoice-pdf"));
    return;
}

string sourceWorkbook = args.Length > 0
    ? args[0]
    : @"Y:\전자거래사업본부\Back Office\2026년 8월 수수료 계산서 내역_전사부.xlsx";
string harnessDirectory = Path.Combine(Path.GetTempPath(), "IPS_PROBACKOFFICE_HARNESS", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(harnessDirectory);

try
{
    Func<DateTime, bool> koreanDay = day => HolidayProvider.IsKoreanBusinessDay(day);
    Func<DateTime, bool> jointDay = day => HolidayProvider.IsKoreaUsBusinessDay(day);
    DateTime tradeDate = new(2026, 9, 4);
    DateTime spot = FxValueDateForm.CalculateSpotDate(tradeDate, koreanDay, jointDay);
    Require(spot == new DateTime(2026, 9, 8), "중간 미국 휴일 때문에 Spot이 밀렸습니다.");
    Require(FxValueDateForm.CalculateSpotDate(new DateTime(2026, 9, 3), koreanDay, jointDay)
        == new DateTime(2026, 9, 8), "최종 결제일의 미국 휴일을 조정하지 못했습니다.");
    DataTable dates = FxValueDateForm.ToCalculatedRows(tradeDate, new DateTime(2026, 9, 8),
        spot, jointDay, koreanDay, new HashSet<string> { "1M" });
    Require((DateTime)dates.Rows[0]["Settle Date"] == new DateTime(2026, 10, 8), "1M 결제일 오류");
    Require((DateTime)dates.Rows[0]["Fixing Date"] == new DateTime(2026, 10, 6), "1M 픽싱일 오류");
    Console.WriteLine("HARNESS PASS: 중간 미국 휴일 포함, 최종 미국 휴일 조정, 1M 결제일/픽싱일");
    DateTime from = new(2026, 8, 1);
    DateTime to = new(2026, 8, 31);
    string target = FeeRevenueWorkbookExporter.BuildOutputPath(from, to, harnessDirectory);
    if (File.Exists(sourceWorkbook))
    {
        File.Copy(sourceWorkbook, target);
    }
    else
    {
        using var seed = new XLWorkbook();
        seed.Worksheets.Add("수수료 계산서내역");
        seed.SaveAs(target);
    }

    DataTable table = CreateSampleTable();
    var rowKinds = new[]
    {
        FeeRevenueRowKind.Detail,
        FeeRevenueRowKind.BankTotal,
        FeeRevenueRowKind.GrandTotal,
        FeeRevenueRowKind.GrandTotal,
        FeeRevenueRowKind.GrandTotal
    };

    string savedPath = FeeRevenueWorkbookExporter.Save(table, rowKinds, from, to, 1376.50m, harnessDirectory);
    Require(savedPath == target, "월별 파일 경로가 예상과 다릅니다.");

    using (var workbook = new XLWorkbook(savedPath))
    {
        Require(workbook.Worksheets.Contains("수수료 계산서내역"), "기존 계산서 시트가 보존되지 않았습니다.");
        Require(workbook.Worksheets.Contains(FeeRevenueWorkbookExporter.DetailSheetName), "내용 시트가 생성되지 않았습니다.");
        Require(workbook.Worksheet(FeeRevenueWorkbookExporter.DetailSheetName).Cell("A5").GetString() == "Hana",
            "내용 시트의 첫 데이터가 예상과 다릅니다.");
    }

    FeeRevenueWorkbookExporter.Save(table, rowKinds, from, to, 1376.50m, harnessDirectory);
    using (var workbook = new XLWorkbook(savedPath))
    {
        Require(workbook.Worksheets.Count(x => x.Name == FeeRevenueWorkbookExporter.DetailSheetName) == 1,
            "재저장 시 내용 시트가 중복되었습니다.");
    }

    bool rejectedCrossMonth = false;
    try
    {
        FeeRevenueWorkbookExporter.BuildOutputPath(from, new DateTime(2026, 9, 1), harnessDirectory);
    }
    catch (InvalidOperationException)
    {
        rejectedCrossMonth = true;
    }
    Require(rejectedCrossMonth, "서로 다른 달의 조회 범위를 차단하지 못했습니다.");
    Console.WriteLine("HARNESS PASS: 월별 경로, 기존 시트 보존, 내용 시트 생성/교체, 월 경계 검증");
}
finally
{
    if (Directory.Exists(harnessDirectory))
        Directory.Delete(harnessDirectory, recursive: true);
}

static DataTable CreateSampleTable()
{
    var table = new DataTable();
    table.Columns.Add("Bank", typeof(string));
    table.Columns.Add("Dealer Name", typeof(string));
    table.Columns.Add("Currency", typeof(string));
    table.Columns.Add("DF", typeof(decimal));
    table.Columns.Add("NDF", typeof(decimal));
    table.Columns.Add("NDF MAR", typeof(decimal));
    table.Columns.Add("NDF TOMMAR", typeof(decimal));
    table.Columns.Add("BASIS MAR", typeof(decimal));
    table.Columns.Add("BASIS TOMMAR", typeof(decimal));
    table.Columns.Add("Total USD", typeof(decimal));
    table.Columns.Add("Total KRW", typeof(decimal));
    table.Rows.Add("Hana", "Dealer A", "KRW", 100m, 200m, 0m, 0m, 0m, 0m, 0m, 300m);
    table.Rows.Add("Hana", "Total", "KRW", 100m, 200m, 0m, 0m, 0m, 0m, 0m, 300m);
    table.Rows.Add("USD Total", "Total", "USD", 10m, 20m, 0m, 0m, 0m, 0m, 30m, 0m);
    table.Rows.Add("KRW Total", "Total", "KRW", 100m, 200m, 0m, 0m, 0m, 0m, 0m, 300m);
    table.Rows.Add("Grand Total", "Total", "KRW+USD", 13865m, 27730m, 0m, 0m, 0m, 0m, 0m, 41595m);
    return table;
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException("HARNESS FAIL: " + message);
}
