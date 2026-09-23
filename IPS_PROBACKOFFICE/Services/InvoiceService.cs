using System.Globalization;
using InvoiceReportPrinter.WinForms.Configuration;
using InvoiceReportPrinter.WinForms.Models;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.SS.Util;

namespace InvoiceReportPrinter.WinForms.Services;

public sealed class InvoiceService
{
    private const int SwapDataStartRow = 6;      // Excel 7행
    private const int SwapTemplateLastRow = 25; // Excel 26행
    private const int SwapTemplateCapacity =
        SwapTemplateLastRow - SwapDataStartRow + 1;
    private const int FrontPageRowCount = 28;
    private const int HalfRowAnchorOffset = 128;

    private readonly InvoiceSettings _settings;
    private readonly TemplateService _templateService;

    public InvoiceService(
        InvoiceSettings settings,
        TemplateService templateService)
    {
        _settings = settings;
        _templateService = templateService;
    }

    public string CreateInvoice(
        Institution institution,
        DateTime month,
        IReadOnlyList<TradeRow> rows,
        string feeCurrency = "KRW",
        string? outputDirectory = null)
    {
        if (rows.Count == 0)
            throw new InvalidOperationException("조회된 거래가 없습니다.");

        string templatePath = _templateService.ResolveTemplatePath(institution);
        string outputFolder = outputDirectory ?? Path.Combine(
            AppContext.BaseDirectory,
            _settings.OutputFolder);
        Directory.CreateDirectory(outputFolder);

        string invoiceMonth =
            month.ToString("MMM yyyy", CultureInfo.InvariantCulture);
        string referenceMonth =
            month.ToString("yyyyMM", CultureInfo.InvariantCulture);
        string invoiceTitle =
            $"INVOICE - {invoiceMonth}(Ref.{referenceMonth})";
        string summaryTitle =
            $"INVOICE SUMMARY - {invoiceMonth}(Ref.{referenceMonth})";
        string safeName = string.Join(
            "_",
            institution.Name.Split(
                Path.GetInvalidFileNameChars(),
                StringSplitOptions.RemoveEmptyEntries));
        string outputPath = Path.Combine(
            outputFolder,
            $"IPS PRO_{safeName}_Brokerage Invoice__" +
            $"{invoiceMonth.ToUpperInvariant()}.xls");

        using var input = File.OpenRead(templatePath);
        var workbook = new HSSFWorkbook(input);

        ISheet front = FindSheet(workbook, "front")
            ?? throw MissingSheet("front");
        ISheet swap = FindSheet(workbook, "swap")
            ?? throw MissingSheet("swap");
        ISheet summaryProduct = FindSheet(workbook, "summary_p")
            ?? throw MissingSheet("summary_p");
        ISheet summaryDealer = FindSheet(workbook, "summary_d")
            ?? throw MissingSheet("summary_d");

        bool dealerFirst = UsesDealerFirstOrdering(institution.Name);
        IOrderedEnumerable<TradeRow> primaryOrdering = dealerFirst
            ? rows.OrderBy(x => x.Dealer, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => NormalizeDisplayProduct(x.Product), StringComparer.OrdinalIgnoreCase)
            : rows.OrderBy(x => NormalizeDisplayProduct(x.Product), StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Dealer, StringComparer.OrdinalIgnoreCase);
        List<TradeRow> orderedRows = primaryOrdering
            .ThenBy(x => x.TradeDate ?? DateTime.MinValue)
            .ThenBy(x => x.RefNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();

        WriteFront(front, institution, invoiceTitle);

        // 한 딜러가 여러 상품을 거래한 경우 상품별 상세 페이지로 분리한다.
        // 그래야 상세 헤더가 MULTIPLE이 되지 않고 실제 상품명이 표시된다.
        List<List<TradeRow>> detailGroups = orderedRows
            .GroupBy(
                x => new
                {
                    Dealer = x.Dealer.Trim().ToUpperInvariant(),
                    Product = NormalizeDisplayProduct(x.Product).Trim().ToUpperInvariant()
                })
            .Select(x => x.ToList())
            .ToList();

        int swapTemplateIndex = workbook.GetSheetIndex(swap.SheetName);
        var detailSections = new List<(ISheet Sheet, int LastRow)>();
        int detailPageNumber = 0;

        foreach (List<TradeRow> detailGroup in detailGroups)
        {
            int pageCount = (detailGroup.Count + SwapTemplateCapacity - 1)
                / SwapTemplateCapacity;

            for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
            {
                List<TradeRow> pageRows = detailGroup
                    .Skip(pageIndex * SwapTemplateCapacity)
                    .Take(SwapTemplateCapacity)
                    .ToList();
                bool includeTotal = pageIndex == pageCount - 1;

                // 상세 페이지마다 원본 템플릿을 복제해 로고·제목·컬럼 헤더를 반복한다.
                ISheet detailSheet = workbook.CloneSheet(swapTemplateIndex);
                int detailLastRow = WriteSwap(
                    detailSheet,
                    institution,
                    invoiceTitle,
                    pageRows,
                    ++detailPageNumber,
                    includeTotal,
                    detailGroup,
                    feeCurrency);

                detailSections.Add((detailSheet, detailLastRow));
            }
        }

        int dealerSummaryLastRow = WriteDealerSummary(
            summaryDealer,
            institution,
            summaryTitle,
            orderedRows,
            detailPageNumber + 1,
            dealerFirst,
            feeCurrency);

        ComposeSingleSheet(
            workbook,
            front,
            swap,
            detailSections,
            summaryDealer,
            dealerSummaryLastRow);

        // 전체저장은 완성된 파일만 이동하고 기존 인보이스는 번호를 붙여 보존한다.
        string temporaryPath = Path.Combine(outputFolder, $".{Guid.NewGuid():N}.tmp");
        try
        {
            using (var output = File.Create(temporaryPath))
                workbook.Write(output, leaveOpen: false);
            string basePath = outputPath;
            for (int suffix = 2; ; suffix++)
            {
                try
                {
                    File.Move(temporaryPath, outputPath, overwrite: outputDirectory is null);
                    break;
                }
                catch (IOException) when (outputDirectory is not null && File.Exists(outputPath))
                {
                    outputPath = Path.Combine(outputFolder,
                        $"{Path.GetFileNameWithoutExtension(basePath)} ({suffix}){Path.GetExtension(basePath)}");
                }
            }
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
        return outputPath;
    }

    private static void WriteFront(
        ISheet sheet,
        Institution institution,
        string invoiceTitle)
    {
        Set(sheet, "AH3", invoiceTitle);
        Set(sheet, "AP25", institution.Name);
    }

    private static int WriteSwap(
        ISheet sheet,
        Institution institution,
        string invoiceTitle,
        IReadOnlyList<TradeRow> rows,
        int pageNumber,
        bool includeTotal,
        IReadOnlyList<TradeRow> totalRows,
        string feeCurrency)
    {
        int extraRows = Math.Max(0, rows.Count - SwapTemplateCapacity);
        if (extraRows > 0)
        {
            InsertStyledMergedRows(
                sheet,
                SwapTemplateLastRow + 1,
                extraRows,
                SwapTemplateLastRow);
        }

        int templateTotalRow = SwapTemplateLastRow + 1 + extraRows;
        int totalRow = SwapDataStartRow + rows.Count;

        if (totalRow != templateTotalRow)
            CopyTotalRowLayout(sheet, templateTotalRow, totalRow);

        string dealer = SingleOrMultiple(rows.Select(x => x.Dealer));
        string product = SingleOrMultiple(
            rows.Select(x => NormalizeDisplayProduct(x.Product)));
        string brokerageCurrency = feeCurrency.Trim().ToUpperInvariant();

        Set(sheet, "K2", invoiceTitle);
        Set(sheet, "B5", institution.Name);
        Set(sheet, "V5", $"DEALER: {dealer}");
        Set(sheet, "AN5", $"PRODUCT - {product}");
        Set(sheet, "BD5", "CCY PAIR : USD/KRW");
        Set(sheet, "BV5", $"PAGE : {pageNumber}");

        for (int index = 0; index < rows.Count; index++)
        {
            int rowIndex = SwapDataStartRow + index;
            TradeRow trade = rows[index];
            bool nearLeg = IsNearLeg(trade);
            bool farLeg = IsFarLeg(trade);

            Set(sheet, Cell(rowIndex, 1), FormatDate(trade.TradeDate)); // B
            Set(sheet, Cell(rowIndex, 7), trade.RefNumber);             // H
            Set(sheet, Cell(rowIndex, 20), NormalizeDirection(trade.Direction)); // U
            Set(sheet, Cell(rowIndex, 25), trade.Counterparty);         // Z
            Set(sheet, Cell(rowIndex, 33), NormalizeDisplayTenor(trade)); // AH

            if (!trade.IsSwitch)
            {
                // 일반 거래용 새 템플릿 열 배치
                // EX.RATE(AK:AQ) / AMOUNT(AR:AY) / VALUE DATE(AZ:BI)
                // / DEALER(BJ:BZ) / BROKERAGE(CA:CG)
                Set(sheet, Cell(rowIndex, 36), trade.ExchangeRate);          // AK
                Set(sheet, Cell(rowIndex, 43), trade.Amount);                // AR
                Set(sheet, Cell(rowIndex, 51), FormatValueDate(trade.ValueDate)); // AZ
                Set(sheet, Cell(rowIndex, 61), trade.Dealer);                // BJ
            }
            else if (nearLeg)
            {
                Set(sheet, Cell(rowIndex, 36), FormatDate(trade.ValueDate)); // AK
                Set(sheet, Cell(rowIndex, 59), trade.ExchangeRate);          // BH
            }
            else if (farLeg)
            {
                Set(sheet, Cell(rowIndex, 42), FormatDate(trade.ValueDate)); // AQ
                Set(sheet, Cell(rowIndex, 64), trade.ExchangeRate);          // BM
            }
            else
            {
                // Leg 구분이 없는 스위치 거래는 Near 열을 사용한다.
                Set(sheet, Cell(rowIndex, 36), FormatDate(trade.ValueDate)); // AK
                Set(sheet, Cell(rowIndex, 59), trade.ExchangeRate);          // BH
            }

            if (trade.IsSwitch)
            {
                Set(sheet, Cell(rowIndex, 47), trade.Amount);            // AV
                Set(sheet, Cell(rowIndex, 69), trade.Dealer);            // BR
            }

            Set(sheet, Cell(rowIndex, 71), trade.BrokerageAmount);      // BT
        }

        if (!includeTotal)
            return SwapDataStartRow + rows.Count - 1;

        Set(sheet, Cell(totalRow, 59), $"TOTAL - {product} {brokerageCurrency}"); // BH
        Set(
            sheet,
            Cell(totalRow, 71),
            totalRows.Sum(x => x.BrokerageAmount));              // BT
        ApplyTotalBorders(sheet, totalRow, 1, 77);

        return totalRow;
    }

    private static int WriteProductSummary(
        ISheet sheet,
        Institution institution,
        string summaryTitle,
        IReadOnlyList<TradeRow> rows,
        int pageNumber)
    {
        var summaries = rows
            .GroupBy(
                x => NormalizeDisplayProduct(x.Product),
                StringComparer.OrdinalIgnoreCase)
            .Select(x => new
            {
                Product = x.Key,
                Brokerage = x.Sum(y => y.BrokerageAmount)
            })
            .OrderBy(x => x.Product, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Set(sheet, "K2", summaryTitle);
        Set(sheet, "AV4", $"PAGE : {pageNumber}");
        EnsureStyledRows(sheet, 5, summaries.Count, 5);

        for (int index = 0; index < summaries.Count; index++)
        {
            int rowIndex = 5 + index;
            Set(sheet, Cell(rowIndex, 1), institution.Name);           // B
            Set(sheet, Cell(rowIndex, 16), summaries[index].Product);  // Q
            MergeIfNeeded(sheet, rowIndex, 40, 51);                    // AO:AZ
            Set(sheet, Cell(rowIndex, 40), summaries[index].Brokerage);// AO
        }

        return Math.Max(5, 5 + summaries.Count - 1);
    }

    private static int WriteDealerSummary(
        ISheet sheet,
        Institution institution,
        string summaryTitle,
        IReadOnlyList<TradeRow> rows,
        int pageNumber,
        bool dealerFirst,
        string feeCurrency)
    {
        var groupedSummaries = rows
            .GroupBy(
                x => new
                {
                    Dealer = x.Dealer.Trim(),
                    Product = NormalizeDisplayProduct(x.Product)
                })
            .Select(x => new
            {
                x.Key.Dealer,
                x.Key.Product,
                Brokerage = x.Sum(y => y.BrokerageAmount)
            })
            .ToList();
        var summaries = dealerFirst
            ? groupedSummaries
                .OrderBy(x => x.Dealer, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Product, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : groupedSummaries
                .OrderBy(x => x.Product, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Dealer, StringComparer.OrdinalIgnoreCase)
                .ToList();

        Set(sheet, "K2", summaryTitle);
        Set(sheet, "BV4", $"PAGE : {pageNumber}");
        EnsureStyledRows(sheet, 5, summaries.Count + 2, 5);

        for (int index = 0; index < summaries.Count; index++)
        {
            int rowIndex = 5 + index;
            Set(sheet, Cell(rowIndex, 1), institution.Name);           // B
            Set(sheet, Cell(rowIndex, 29), summaries[index].Dealer);   // AD
            Set(sheet, Cell(rowIndex, 44), summaries[index].Product);  // AS
            MergeIfNeeded(sheet, rowIndex, 66, 77);                    // BO:BZ
            Set(sheet, Cell(rowIndex, 66), summaries[index].Brokerage);// BO
        }

        decimal totalBrokerage = summaries.Sum(x => x.Brokerage);
        string brokerageCurrency = feeCurrency.Trim().ToUpperInvariant();
        int sumRow = 5 + summaries.Count;
        int totalRow = sumRow + 1;

        Set(sheet, Cell(sumRow, 29), "SUM");                         // AD
        Set(sheet, Cell(sumRow, 58), brokerageCurrency);              // BG
        MergeIfNeeded(sheet, sumRow, 66, 77);                         // BO:BZ
        Set(sheet, Cell(sumRow, 66), totalBrokerage);                  // BO

        Set(sheet, Cell(totalRow, 1), "TOTAL");                       // B
        Set(sheet, Cell(totalRow, 58), brokerageCurrency);            // BG
        MergeIfNeeded(sheet, totalRow, 66, 77);                       // BO:BZ
        Set(sheet, Cell(totalRow, 66), totalBrokerage);                // BO
        ApplyTotalBorders(sheet, totalRow, 1, 77);

        return totalRow;
    }

    private static bool UsesDealerFirstOrdering(string institutionName)
    {
        string normalizedName = string.Join(
            " ",
            institutionName.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();

        return normalizedName is
            "IBK SEOUL" or
            "IKB SEOUL" or
            "KB SEOUL" or
            "MIRAE ASSET SECURITIES" or
            "NH INV";
    }

    private static void ComposeSingleSheet(
        HSSFWorkbook workbook,
        ISheet front,
        ISheet swapTemplate,
        IReadOnlyList<(ISheet Sheet, int LastRow)> detailSections,
        ISheet summaryDealer,
        int dealerSummaryLastRow)
    {
        // front 템플릿에는 사용하지 않는 서식 행이 남아 있으므로 LastRowNum을 쓰면
        // 첫 상세 페이지 앞에 빈 인쇄 페이지가 생긴다. 첫 장은 Excel 1~28행으로 고정한다.
        int nextRow = FrontPageRowCount;

        foreach ((ISheet detailSheet, int detailLastRow) in detailSections)
        {
            int detailStartRow = nextRow;
            CopySheetBlock(
                detailSheet,
                front,
                0,
                detailLastRow,
                detailStartRow);
            CopyPictures(
                detailSheet,
                front,
                0,
                detailLastRow,
                detailStartRow,
                HalfRowAnchorOffset);
            front.SetRowBreak(detailStartRow - 1);
            nextRow += detailLastRow + 2;
        }

        int dealerSummaryStartRow = nextRow;
        CopySheetBlock(
            summaryDealer,
            front,
            0,
            dealerSummaryLastRow,
            dealerSummaryStartRow);
        CopyPictures(
            summaryDealer,
            front,
            0,
            dealerSummaryLastRow,
            dealerSummaryStartRow);
        int finalRow = dealerSummaryStartRow + dealerSummaryLastRow;

        front.SetRowBreak(dealerSummaryStartRow - 1);
        front.FitToPage = true;
        front.PrintSetup.FitWidth = 1;
        front.PrintSetup.FitHeight = 0;

        int frontIndex = workbook.GetSheetIndex(front.SheetName);
        workbook.SetPrintArea(
            frontIndex,
            0,
            77,
            0,
            finalRow);

        foreach ((ISheet detailSheet, _) in detailSections)
            RemoveSheet(workbook, detailSheet.SheetName);

        RemoveSheet(workbook, "summary_d");
        RemoveSheet(workbook, "summary_p");
        RemoveSheet(workbook, swapTemplate.SheetName);
    }

    private static void CopySheetBlock(
        ISheet source,
        ISheet target,
        int sourceStartRow,
        int sourceEndRow,
        int targetStartRow)
    {
        int rowOffset = targetStartRow - sourceStartRow;

        for (int sourceRowIndex = sourceStartRow;
             sourceRowIndex <= sourceEndRow;
             sourceRowIndex++)
        {
            IRow? sourceRow = source.GetRow(sourceRowIndex);
            if (sourceRow is null)
                continue;

            int targetRowIndex = sourceRowIndex + rowOffset;
            IRow targetRow =
                target.GetRow(targetRowIndex)
                ?? target.CreateRow(targetRowIndex);
            targetRow.Height = sourceRow.Height;

            foreach (ICell sourceCell in sourceRow.Cells)
            {
                ICell targetCell =
                    targetRow.GetCell(sourceCell.ColumnIndex)
                    ?? targetRow.CreateCell(sourceCell.ColumnIndex);
                CopyCell(sourceCell, targetCell);
            }
        }

        for (int index = 0; index < source.NumMergedRegions; index++)
        {
            CellRangeAddress region = source.GetMergedRegion(index);
            if (region.FirstRow < sourceStartRow ||
                region.LastRow > sourceEndRow)
                continue;

            target.AddMergedRegion(
                new CellRangeAddress(
                    region.FirstRow + rowOffset,
                    region.LastRow + rowOffset,
                    region.FirstColumn,
                    region.LastColumn));
        }
    }

    private static void CopyCell(ICell source, ICell target)
    {
        target.CellStyle = source.CellStyle;

        switch (source.CellType)
        {
            case CellType.String:
                target.SetCellValue(source.StringCellValue);
                break;
            case CellType.Numeric:
                target.SetCellValue(source.NumericCellValue);
                break;
            case CellType.Boolean:
                target.SetCellValue(source.BooleanCellValue);
                break;
            case CellType.Formula:
                target.CellFormula = source.CellFormula;
                break;
            case CellType.Error:
                target.SetCellErrorValue(source.ErrorCellValue);
                break;
            default:
                target.SetBlank();
                break;
        }
    }

    private static void CopyPictures(
        ISheet source,
        ISheet target,
        int sourceStartRow,
        int sourceEndRow,
        int targetStartRow,
        int verticalAnchorOffset = 0)
    {
        if (source is not HSSFSheet sourceSheet || target is not HSSFSheet targetSheet)
            return;

        HSSFPatriarch? sourceDrawing = sourceSheet.DrawingPatriarch as HSSFPatriarch;
        if (sourceDrawing is null)
            return;

        HSSFPatriarch targetDrawing = targetSheet.DrawingPatriarch as HSSFPatriarch
            ?? (HSSFPatriarch)targetSheet.CreateDrawingPatriarch();
        int rowOffset = targetStartRow - sourceStartRow;

        foreach (HSSFShape shape in sourceDrawing.Children)
        {
            if (shape is not HSSFPicture picture ||
                picture.ClientAnchor is not HSSFClientAnchor sourceAnchor ||
                sourceAnchor.Row1 < sourceStartRow ||
                sourceAnchor.Row2 > sourceEndRow + 1)
                continue;

            (int firstRow, int firstDy) = ShiftAnchorVertically(
                sourceAnchor.Row1 + rowOffset,
                sourceAnchor.Dy1,
                verticalAnchorOffset);
            (int lastRow, int lastDy) = ShiftAnchorVertically(
                sourceAnchor.Row2 + rowOffset,
                sourceAnchor.Dy2,
                verticalAnchorOffset);

            var targetAnchor = new HSSFClientAnchor(
                sourceAnchor.Dx1,
                firstDy,
                sourceAnchor.Dx2,
                lastDy,
                sourceAnchor.Col1,
                firstRow,
                sourceAnchor.Col2,
                lastRow)
            {
                AnchorType = sourceAnchor.AnchorType
            };
            targetDrawing.CreatePicture(targetAnchor, picture.PictureIndex);
        }
    }

    private static (int Row, int Dy) ShiftAnchorVertically(
        int row,
        int dy,
        int offset)
    {
        int shifted = dy + offset;
        return (row + shifted / 256, shifted % 256);
    }

    private static void RemoveSheet(
        HSSFWorkbook workbook,
        string sheetName)
    {
        int sheetIndex = workbook.GetSheetIndex(sheetName);
        if (sheetIndex >= 0)
            workbook.RemoveSheetAt(sheetIndex);
    }

    private static void EnsureStyledRows(
        ISheet sheet,
        int startRowIndex,
        int requiredCount,
        int templateRowIndex)
    {
        if (requiredCount <= 0)
            return;

        IRow templateRow = sheet.GetRow(templateRowIndex)
            ?? throw new InvalidDataException(
                $"Template 데이터 행 {templateRowIndex + 1}이 없습니다.");

        for (int index = 0; index < requiredCount; index++)
        {
            int rowIndex = startRowIndex + index;
            IRow row = sheet.GetRow(rowIndex) ?? sheet.CreateRow(rowIndex);
            if (rowIndex == templateRowIndex)
                continue;

            CopyRowStyle(templateRow, row);
        }
    }

    private static void MergeIfNeeded(
        ISheet sheet,
        int rowIndex,
        int firstColumn,
        int lastColumn)
    {
        for (int index = 0; index < sheet.NumMergedRegions; index++)
        {
            CellRangeAddress region = sheet.GetMergedRegion(index);
            if (region.FirstRow == rowIndex &&
                region.LastRow == rowIndex &&
                region.FirstColumn == firstColumn &&
                region.LastColumn == lastColumn)
                return;
        }

        sheet.AddMergedRegion(
            new CellRangeAddress(
                rowIndex,
                rowIndex,
                firstColumn,
                lastColumn));
    }

    private static void ApplyTotalBorders(
        ISheet sheet,
        int rowIndex,
        int firstColumn,
        int lastColumn)
    {
        IRow row = sheet.GetRow(rowIndex) ?? sheet.CreateRow(rowIndex);
        for (int columnIndex = firstColumn;
             columnIndex <= lastColumn;
             columnIndex++)
        {
            ICell cell = row.GetCell(columnIndex) ?? row.CreateCell(columnIndex);
            ICellStyle borderedStyle = sheet.Workbook.CreateCellStyle();
            borderedStyle.CloneStyleFrom(cell.CellStyle);
            borderedStyle.BorderTop = NPOI.SS.UserModel.BorderStyle.Thin;
            borderedStyle.BorderBottom = NPOI.SS.UserModel.BorderStyle.Medium;
            cell.CellStyle = borderedStyle;
        }
    }

    private static void InsertStyledMergedRows(
        ISheet sheet,
        int insertionRowIndex,
        int count,
        int templateRowIndex)
    {
        IRow templateRow = sheet.GetRow(templateRowIndex)
            ?? throw new InvalidDataException(
                $"Template 데이터 행 {templateRowIndex + 1}이 없습니다.");

        var mergedColumns = new List<(int FirstColumn, int LastColumn)>();
        for (int index = 0; index < sheet.NumMergedRegions; index++)
        {
            CellRangeAddress region = sheet.GetMergedRegion(index);
            if (region.FirstRow == templateRowIndex &&
                region.LastRow == templateRowIndex)
            {
                mergedColumns.Add(
                    (region.FirstColumn, region.LastColumn));
            }
        }

        sheet.ShiftRows(
            insertionRowIndex,
            sheet.LastRowNum,
            count,
            true,
            false);

        for (int offset = 0; offset < count; offset++)
        {
            int rowIndex = insertionRowIndex + offset;
            IRow row = sheet.CreateRow(rowIndex);
            CopyRowStyle(templateRow, row);

            foreach ((int firstColumn, int lastColumn) in mergedColumns)
            {
                sheet.AddMergedRegion(
                    new CellRangeAddress(
                        rowIndex,
                        rowIndex,
                        firstColumn,
                        lastColumn));
            }
        }
    }

    private static void CopyRowStyle(IRow source, IRow target)
    {
        target.Height = source.Height;
        for (int column = 0; column < source.LastCellNum; column++)
        {
            ICell? sourceCell = source.GetCell(column);
            if (sourceCell is null)
                continue;

            ICell targetCell =
                target.GetCell(column) ?? target.CreateCell(column);
            targetCell.CellStyle = sourceCell.CellStyle;
        }
    }

    private static void CopyTotalRowLayout(
        ISheet sheet,
        int sourceRowIndex,
        int targetRowIndex)
    {
        IRow sourceRow = sheet.GetRow(sourceRowIndex)
            ?? throw new InvalidDataException(
                $"Template 합계 행 {sourceRowIndex + 1}이 없습니다.");
        IRow targetRow = sheet.GetRow(targetRowIndex)
            ?? sheet.CreateRow(targetRowIndex);

        var totalMergedColumns = new List<(int FirstColumn, int LastColumn)>();
        for (int index = 0; index < sheet.NumMergedRegions; index++)
        {
            CellRangeAddress region = sheet.GetMergedRegion(index);
            if (region.FirstRow == sourceRowIndex &&
                region.LastRow == sourceRowIndex)
            {
                totalMergedColumns.Add(
                    (region.FirstColumn, region.LastColumn));
            }
        }

        // 합계 행이 들어갈 위치의 기존 거래행 병합을 제거한다.
        for (int index = sheet.NumMergedRegions - 1; index >= 0; index--)
        {
            CellRangeAddress region = sheet.GetMergedRegion(index);
            if (region.FirstRow == targetRowIndex &&
                region.LastRow == targetRowIndex)
            {
                sheet.RemoveMergedRegion(index);
            }
        }

        CopyRowStyle(sourceRow, targetRow);

        foreach ((int firstColumn, int lastColumn) in totalMergedColumns)
        {
            sheet.AddMergedRegion(
                new CellRangeAddress(
                    targetRowIndex,
                    targetRowIndex,
                    firstColumn,
                    lastColumn));
        }
    }

    private static bool IsNearLeg(TradeRow trade)
    {
        string tenor = NormalizeText(trade.Tenor);
        return trade.IsSwitch
               && tenor == "NDF SN";
    }

    private static bool IsFarLeg(TradeRow trade)
    {
        string tenor = NormalizeText(trade.Tenor);
        return trade.IsSwitch
               && (tenor == "NDF SN1M" || tenor == "DF SN");
    }

    private static string NormalizeText(string? value) =>
        string.Join(
            ' ',
            (value ?? string.Empty)
                .Trim()
                .ToUpperInvariant()
                .Replace('_', ' ')
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static string SingleOrMultiple(IEnumerable<string> values)
    {
        string[] distinct = values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return distinct.Length switch
        {
            0 => string.Empty,
            1 => distinct[0],
            _ => "MULTIPLE"
        };
    }

    private static ISheet? FindSheet(IWorkbook workbook, string sheetName)
    {
        for (int index = 0; index < workbook.NumberOfSheets; index++)
        {
            ISheet sheet = workbook.GetSheetAt(index);
            if (sheet.SheetName.Equals(
                    sheetName,
                    StringComparison.OrdinalIgnoreCase))
                return sheet;
        }

        return null;
    }

    private static InvalidDataException MissingSheet(string sheetName) =>
        new($"Template Sheet '{sheetName}'가 없습니다.");

    private static string NormalizeDisplayProduct(string? product) =>
        string.Equals(product, "DF 1M", StringComparison.OrdinalIgnoreCase)
            ? "DF"
            : product ?? string.Empty;

    private static string NormalizeDisplayTenor(TradeRow trade)
    {
        string tenor = NormalizeText(trade.Tenor);
        string product = NormalizeText(trade.Product);

        if (!trade.IsSwitch &&
            ((product == "DF 1M" && tenor == "DF 1M") ||
             (product == "NDF 1M" && tenor == "NDF 1M")))
            return "1M";

        return trade.Tenor?.Trim() ?? string.Empty;
    }

    private static string NormalizeDirection(string? direction) =>
        direction?.Trim().ToUpperInvariant() switch
        {
            "BUY" => "B",
            "SELL" => "S",
            var value => value ?? string.Empty
        };

    private static string FormatDate(DateTime? date) =>
        date?.ToString("ddMMMyyyy", CultureInfo.InvariantCulture)
        ?? string.Empty;

    private static string FormatValueDate(DateTime? date) =>
        date?.ToString("dd-MMM-yy", CultureInfo.InvariantCulture)
        ?? string.Empty;

    private static string Cell(int zeroBasedRow, int zeroBasedColumn) =>
        new CellReference(
            zeroBasedRow,
            zeroBasedColumn).FormatAsString();

    private static void Set(ISheet sheet, string address, string value) =>
        GetCell(sheet, address).SetCellValue(value);

    private static void Set(ISheet sheet, string address, decimal value) =>
        GetCell(sheet, address).SetCellValue((double)value);

    private static void Set(ISheet sheet, string address, decimal? value)
    {
        ICell cell = GetCell(sheet, address);
        if (value.HasValue)
            cell.SetCellValue((double)value.Value);
        else
            cell.SetBlank();
    }

    private static ICell GetCell(ISheet sheet, string address)
    {
        var reference = new CellReference(address);
        IRow row =
            sheet.GetRow(reference.Row) ?? sheet.CreateRow(reference.Row);
        return row.GetCell(reference.Col) ?? row.CreateCell(reference.Col);
    }
}
