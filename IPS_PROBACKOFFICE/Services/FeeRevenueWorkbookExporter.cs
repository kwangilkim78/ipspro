using ClosedXML.Excel;
using System.Data;
using System.Globalization;

namespace IPS_PROBACKOFFICE.Services;

internal enum FeeRevenueRowKind
{
    Detail,
    BankTotal,
    GrandTotal
}

internal static class FeeRevenueWorkbookExporter
{
    internal const string DefaultOutputDirectory = @"Y:\전자거래사업본부\Back Office";
    internal const string DetailSheetName = "내용";

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
        IReadOnlyList<FeeRevenueRowKind> rowKinds,
        DateTime from,
        DateTime to,
        decimal exchangeRate,
        string? outputDirectory = null)
    {
        if (table.Rows.Count != rowKinds.Count)
            throw new ArgumentException("엑셀 행 정보와 합계 행 정보가 일치하지 않습니다.", nameof(rowKinds));

        string outputPath = BuildOutputPath(from, to, outputDirectory);
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

    private static void WriteDetailSheet(
        IXLWorksheet sheet,
        DataTable table,
        IReadOnlyList<FeeRevenueRowKind> rowKinds,
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
                sheet.Cell(firstDataRow + row, column + 1).Value = column < 3
                    ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
                    : Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            }

            FeeRevenueRowKind kind = rowKinds[row];
            if (kind == FeeRevenueRowKind.Detail)
                continue;

            IXLRow excelRow = sheet.Row(firstDataRow + row);
            excelRow.Style.Font.Bold = true;
            excelRow.Style.Fill.BackgroundColor = kind == FeeRevenueRowKind.GrandTotal
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
        if (table.Rows.Count > 0 && columnCount >= 4)
            sheet.Range(firstDataRow, 4, lastDataRow, columnCount).Style.NumberFormat.Format = "#,##0.####;[Red]-#,##0.####;-";

        sheet.Column(1).Width = 24;
        sheet.Column(2).Width = 22;
        sheet.Column(3).Width = 12;
        if (columnCount >= 4)
            sheet.Columns(4, columnCount).Width = 14;
        sheet.SheetView.FreezeRows(headerRow);
        sheet.SheetView.FreezeColumns(3);
        sheet.Range(headerRow, 1, Math.Max(lastDataRow, headerRow), columnCount).SetAutoFilter();
    }
}
