using ClosedXML.Excel;
using IPS_PROBACKOFFICE.Services;
using System.Data;

string sourceWorkbook = args.Length > 0
    ? args[0]
    : @"Y:\전자거래사업본부\Back Office\2026년 8월 수수료 계산서 내역_전사부.xlsx";
string harnessDirectory = Path.Combine(Path.GetTempPath(), "IPS_PROBACKOFFICE_HARNESS", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(harnessDirectory);

try
{
    DateTime from = new(2026, 8, 1);
    DateTime to = new(2026, 8, 31);
    string target = FeeRevenueWorkbookExporter.BuildOutputPath(from, to, harnessDirectory);
    File.Copy(sourceWorkbook, target);

    DataTable table = CreateSampleTable();
    var rowKinds = new[]
    {
        FeeRevenueRowKind.Detail,
        FeeRevenueRowKind.BankTotal,
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
    table.Columns.Add("DF", typeof(decimal));
    table.Columns.Add("NDF", typeof(decimal));
    table.Columns.Add("NDF MAR", typeof(decimal));
    table.Columns.Add("NDF TOMMAR", typeof(decimal));
    table.Columns.Add("BASIS MAR", typeof(decimal));
    table.Columns.Add("BASIS TOMMAR", typeof(decimal));
    table.Columns.Add("Total", typeof(decimal));
    table.Rows.Add("Hana", "Dealer A", 100m, 200m, 0m, 0m, 0m, 0m, 300m);
    table.Rows.Add("Hana", "Total", 100m, 200m, 0m, 0m, 0m, 0m, 300m);
    table.Rows.Add("Grand Total", "Total", 100m, 200m, 0m, 0m, 0m, 0m, 300m);
    return table;
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException("HARNESS FAIL: " + message);
}
