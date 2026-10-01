using ClosedXML.Excel;
using IPS_PROBACKOFFICE;
using IPS_PROBACKOFFICE.Services;
using System.Data;

if (args.Contains("--base-amount"))
{
    foreach (var (input, expected) in new (string?, string)[]
    {
        ("-1,000,000", "1000000"), ("− 250.50", "250.50"), ("－300", "300"),
        ("-1,000,000 USD", "1000000"), ("1,000,000 USD", "1000000"),
        ("100", "100"), ("0", "0"), ("-", "-"), (null, ""), ("12-34", "12-34")
    })
    {
        string actual = IPS_PROBACKOFFICE.Data.TradeImportRepository.NormalizeBaseAmount(input);
        if (actual != expected) throw new Exception($"Base Amount: {input} => {actual}, expected {expected}");
    }
    var convert = typeof(IPS_PROBACKOFFICE.Data.TradeImportRepository).GetMethod("ConvertValue",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
    if (!Equals(convert.Invoke(null, new object[] { "-1500", "decimal" }), -1500m))
        throw new Exception("Other numeric columns must retain their sign");
    foreach (var (input, expected) in new[] { ("1,000,000 USD", 1000000m),
        ("1,357,250,000 KRW", 1357250000m), ("-1,357,250,000 KRW", -1357250000m) })
        if (!Equals(convert.Invoke(null, new object[] { input, "decimal" }), expected))
            throw new Exception("Currency suffix conversion failed: " + input);
    if (!Equals(convert.Invoke(null, new object[] { "1,000 USD", "bigint" }), 1000L))
        throw new Exception("Integer amount conversion failed");
    if (!Equals(convert.Invoke(null, new object[] { "KB USD", "varchar" }), "KB USD"))
        throw new Exception("Text columns must not be normalized");
    foreach (string invalid in new[] { "12-34 USD", "USD", "100 USD extra" })
    {
        bool rejected = false;
        try { convert.Invoke(null, new object[] { invalid, "decimal" }); }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is FormatException) { rejected = true; }
        if (!rejected) throw new Exception("Malformed input accepted: " + invalid);
    }
    Console.WriteLine("HARNESS PASS: Base Amount signs, separators, empty, malformed values; other amounts unchanged");
    return;
}

if (args.Contains("--contact-email-connection"))
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    Console.WriteLine(await ContactEmailTransport.CheckConnectionAsync(timeout.Token));
    return;
}

if (args.Contains("--contact-email"))
{
    ContactEmailChecks.Run();
    return;
}

if (args.Contains("--emergency-contacts"))
{
    EmergencyContactChecks.Run();
    return;
}

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

if (args.Contains("--holiday-2027"))
{
    DateTime[] koreanDays =
    [
        new(2027, 2, 6), new(2027, 2, 7), new(2027, 2, 8), new(2027, 2, 9),
        new(2027, 5, 1), new(2027, 5, 3), new(2027, 5, 13),
        new(2027, 7, 17), new(2027, 7, 19), new(2027, 8, 16),
        new(2027, 9, 14), new(2027, 9, 15), new(2027, 9, 16),
        new(2027, 10, 4), new(2027, 10, 11), new(2027, 12, 27)
    ];
    foreach (DateTime day in koreanDays)
        Require(HolidayProvider.Get(day, day)[day].Name.Length > 0, $"2027 한국 휴일 누락: {day:yyyy-MM-dd}");
    Require(!HolidayProvider.IsKoreanBusinessDay(new DateTime(2027, 2, 9)), "설날 대체공휴일 영업일 오류");
    Require(HolidayProvider.IsKoreanBusinessDay(new DateTime(2027, 2, 10)), "설날 이후 영업일 오류");
    Require(HolidayProvider.Get(new DateTime(2027, 2, 15), new DateTime(2027, 2, 15))
        [new DateTime(2027, 2, 15)].Name == "Presidents' Day", "미국 2027 Banking 휴일 오류");
    Console.WriteLine("HARNESS PASS: 2027 한국 휴일 및 미국 Banking 휴일");
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
