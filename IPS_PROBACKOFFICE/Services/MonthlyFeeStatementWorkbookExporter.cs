using ClosedXML.Excel;
using System.Data;
using System.Globalization;

namespace IPS_PROBACKOFFICE.Services;

internal enum MonthlyFeeStatementRowKind
{
    Detail,
    BankTotal,
    GrandTotal
}

internal static class MonthlyFeeStatementWorkbookExporter
{
    internal const string DefaultOutputDirectory =
        @"Y:\전자거래사업본부\Back Office\월간수수료보고";
    internal const string DetailSheetName = "내용";
    internal const string StatementSheetName = "수수료 계산서내역";

    internal static string BuildOutputPath(DateTime from, DateTime to, string? outputDirectory = null)
    {
        if (from.Year != to.Year || from.Month != to.Month)
            throw new InvalidOperationException("월별 계산서 파일은 조회 시작일과 종료일이 같은 달이어야 합니다.");

        string directory = string.IsNullOrWhiteSpace(outputDirectory)
            ? DefaultOutputDirectory
            : outputDirectory;
        string fileName = $"{from.Year}년 {from.Month}월 수수료 계산서 내역_전사부.xlsx";
        return Path.Combine(directory, fileName);
    }

    internal static string Save(
        DataTable table,
        IReadOnlyList<MonthlyFeeStatementRowKind> rowKinds,
        DateTime from,
        DateTime to,
        decimal exchangeRate,
        string? outputDirectory = null)
    {
        string outputPath = BuildOutputPath(from, to, outputDirectory);
        return SaveToPath(table, rowKinds, from, to, exchangeRate, outputPath);
    }

    internal static string SaveToPath(
        DataTable table,
        IReadOnlyList<MonthlyFeeStatementRowKind> rowKinds,
        DateTime from,
        DateTime to,
        decimal exchangeRate,
        string outputPath)
    {
        if (from.Year != to.Year || from.Month != to.Month)
            throw new InvalidOperationException("월별 계산서 파일은 조회 시작일과 종료일이 같은 달이어야 합니다.");
        if (table.Rows.Count != rowKinds.Count)
            throw new ArgumentException("엑셀 행 정보와 합계 행 정보가 일치하지 않습니다.", nameof(rowKinds));
        if (!Path.GetExtension(outputPath).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Excel 통합 문서(.xlsx) 형식으로 저장해 주세요.");

        string directory = Path.GetDirectoryName(outputPath)
            ?? throw new InvalidOperationException("엑셀 저장 경로를 확인할 수 없습니다.");
        Directory.CreateDirectory(directory);

        using var workbook = File.Exists(outputPath) ? new XLWorkbook(outputPath) : new XLWorkbook();
        IXLWorksheet? oldDetail = workbook.Worksheets
            .FirstOrDefault(x => x.Name.Equals(DetailSheetName, StringComparison.OrdinalIgnoreCase));
        oldDetail?.Delete();

        IXLWorksheet sheet = workbook.Worksheets.Add(DetailSheetName);
        WriteDetailSheet(sheet, table, rowKinds, from, to, exchangeRate);

        string tempPath = Path.Combine(
            directory,
            $".{Path.GetFileNameWithoutExtension(outputPath)}.{Guid.NewGuid():N}.tmp.xlsx");
        try
        {
            workbook.SaveAs(tempPath);
            File.Move(tempPath, outputPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }

        return outputPath;
    }

    internal static string SaveStatementToPath(
        DataTable statement,
        DataTable issues,
        DateTime month,
        decimal exchangeRate,
        string outputPath)
    {
        if (!Path.GetExtension(outputPath).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Excel 통합 문서(.xlsx) 형식으로 저장해 주세요.");
        string directory = Path.GetDirectoryName(outputPath)
            ?? throw new InvalidOperationException("엑셀 저장 경로를 확인할 수 없습니다.");
        Directory.CreateDirectory(directory);

        using var workbook = File.Exists(outputPath) ? new XLWorkbook(outputPath) : new XLWorkbook();
        workbook.Worksheets.FirstOrDefault(x => x.Name.Equals(StatementSheetName, StringComparison.OrdinalIgnoreCase))?.Delete();
        IXLWorksheet sheet = workbook.Worksheets.Add(StatementSheetName);
        WriteStatementSheet(sheet, statement, issues, month, exchangeRate);

        string tempPath = Path.Combine(directory, $".{Path.GetFileNameWithoutExtension(outputPath)}.{Guid.NewGuid():N}.tmp.xlsx");
        try
        {
            workbook.SaveAs(tempPath);
            File.Move(tempPath, outputPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
        return outputPath;
    }

    private static void WriteStatementSheet(
        IXLWorksheet sheet,
        DataTable statement,
        DataTable issues,
        DateTime month,
        decimal exchangeRate)
    {
        sheet.Cell("A1").Value = $"{month:yyyy년 M월} 수수료 내역";
        sheet.Range("A1:C1").Merge();
        sheet.Cell("D1").Value = $"기준환율: {exchangeRate:#,##0.00}";
        sheet.Range("D1:F1").Merge();
        sheet.Cell("H1").Value = $"{month:yyyy년 M월} 계산서 발행내역_FX와 합산처리";
        sheet.Range("H1:J1").Merge();

        string[] leftHeaders = ["Bank", "Brokerage\n(수취 수수료)", "Switch Fee\n(지급 수수료)", "E-platform_Total", "계산서_KRW", "계산서_USD"];
        for (int c = 0; c < leftHeaders.Length; c++) sheet.Cell(2, c + 1).Value = leftHeaders[c];
        for (int r = 0; r < statement.Rows.Count; r++)
            for (int c = 0; c < statement.Columns.Count; c++)
                sheet.Cell(r + 3, c + 1).Value = c == 0
                    ? Convert.ToString(statement.Rows[r][c], CultureInfo.InvariantCulture) ?? string.Empty
                    : Convert.ToDecimal(statement.Rows[r][c], CultureInfo.InvariantCulture);

        string[] rightHeaders = ["No.", "은행", "E-platform"];
        for (int c = 0; c < rightHeaders.Length; c++) sheet.Cell(2, c + 8).Value = rightHeaders[c];
        for (int r = 0; r < issues.Rows.Count; r++)
            for (int c = 0; c < issues.Columns.Count; c++)
                sheet.Cell(r + 3, c + 8).Value = c < 2
                    ? Convert.ToString(issues.Rows[r][c], CultureInfo.InvariantCulture) ?? string.Empty
                    : Convert.ToDecimal(issues.Rows[r][c], CultureInfo.InvariantCulture);

        int leftLast = statement.Rows.Count + 2;
        int rightLast = issues.Rows.Count + 2;
        IXLRange left = sheet.Range(2, 1, leftLast, 6);
        IXLRange right = sheet.Range(2, 8, rightLast, 10);
        foreach (IXLRange range in new[] { left, right })
        {
            range.Style.Font.FontName = "맑은 고딕";
            range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
            range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }
        sheet.Range("A2:F2").Style.Fill.BackgroundColor = XLColor.FromArgb(252, 228, 209);
        sheet.Range("H2:J2").Style.Fill.BackgroundColor = XLColor.FromArgb(218, 238, 243);
        sheet.Range("A2:F2").Style.Font.Bold = true;
        sheet.Range("H2:J2").Style.Font.Bold = true;
        sheet.Range("A2:F2").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Range("H2:J2").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Range(leftLast, 1, leftLast, 6).Style.Fill.BackgroundColor = XLColor.FromArgb(248, 203, 173);
        sheet.Range(rightLast, 8, rightLast, 10).Style.Fill.BackgroundColor = XLColor.FromArgb(218, 238, 243);
        sheet.Range(leftLast, 1, leftLast, 6).Style.Font.Bold = true;
        sheet.Range(rightLast, 8, rightLast, 10).Style.Font.Bold = true;
        sheet.Range(3, 2, leftLast, 5).Style.NumberFormat.Format = "#,##0;[Red]-#,##0;-";
        sheet.Range(3, 6, leftLast, 6).Style.NumberFormat.Format = "#,##0.00;[Red]-#,##0.00;-";
        sheet.Range(3, 10, rightLast, 10).Style.NumberFormat.Format = "#,##0;[Red]-#,##0;-";
        sheet.Columns("A:J").Width = 14;
        sheet.Column("A").Width = 19;
        sheet.Column("G").Width = 4;
        sheet.Column("I").Width = 19;
        sheet.Row(2).Height = 38;
        sheet.SheetView.FreezeRows(2);
    }

    private static void WriteDetailSheet(
        IXLWorksheet sheet,
        DataTable table,
        IReadOnlyList<MonthlyFeeStatementRowKind> rowKinds,
        DateTime from,
        DateTime to,
        decimal exchangeRate)
    {
        int columnCount = table.Columns.Count;
        int headerRow = 4;
        int firstDataRow = headerRow + 1;
        int lastDataRow = headerRow + table.Rows.Count;

        sheet.Cell(1, 1).Value = $"{from:yyyy년 M월} 매출 수수료 조회 내용";
        sheet.Range(1, 1, 1, columnCount).Merge();
        sheet.Cell(2, 1).Value = "조회기간";
        sheet.Cell(2, 2).Value = from.Date;
        sheet.Cell(2, 3).Value = "~";
        sheet.Cell(2, 4).Value = to.Date;
        sheet.Cell(2, 6).Value = "환율(USD/KRW)";
        sheet.Cell(2, 7).Value = exchangeRate;

        for (int column = 0; column < columnCount; column++)
            sheet.Cell(headerRow, column + 1).Value = table.Columns[column].ColumnName;

        for (int row = 0; row < table.Rows.Count; row++)
        {
            for (int column = 0; column < columnCount; column++)
            {
                object value = table.Rows[row][column];
                sheet.Cell(firstDataRow + row, column + 1).Value = column < 2
                    ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
                    : Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            }

            MonthlyFeeStatementRowKind kind = rowKinds[row];
            if (kind == MonthlyFeeStatementRowKind.Detail)
                continue;

            IXLRow excelRow = sheet.Row(firstDataRow + row);
            excelRow.Style.Font.Bold = true;
            excelRow.Style.Fill.BackgroundColor = kind == MonthlyFeeStatementRowKind.GrandTotal
                ? XLColor.FromArgb(217, 228, 241)
                : XLColor.FromArgb(238, 243, 249);
        }

        IXLRange dataRange = sheet.Range(headerRow, 1, Math.Max(lastDataRow, headerRow), columnCount);
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        dataRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Range(1, 1, Math.Max(lastDataRow, headerRow), columnCount).Style.Font.FontName = "맑은 고딕";

        sheet.Row(1).Style.Font.Bold = true;
        sheet.Row(1).Style.Font.FontSize = 14;
        sheet.Row(1).Height = 24;
        sheet.Row(headerRow).Style.Font.Bold = true;
        sheet.Row(headerRow).Style.Fill.BackgroundColor = XLColor.FromArgb(221, 235, 247);
        sheet.Row(headerRow).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Range(2, 2, 2, 4).Style.DateFormat.Format = "yyyy-mm-dd";
        sheet.Cell(2, 7).Style.NumberFormat.Format = "#,##0.00";
        if (table.Rows.Count > 0 && columnCount >= 3)
            sheet.Range(firstDataRow, 3, lastDataRow, columnCount).Style.NumberFormat.Format = "#,##0;[Red]-#,##0;-";

        sheet.Column(1).Width = 24;
        sheet.Column(2).Width = 22;
        if (columnCount >= 3)
            sheet.Columns(3, columnCount).Width = 14;
        sheet.SheetView.FreezeRows(headerRow);
        sheet.SheetView.FreezeColumns(2);
        sheet.Range(headerRow, 1, Math.Max(lastDataRow, headerRow), columnCount).SetAutoFilter();
    }
}
