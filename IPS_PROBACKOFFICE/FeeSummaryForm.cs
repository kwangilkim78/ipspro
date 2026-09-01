using ClosedXML.Excel;
using InvoiceReportPrinter.WinForms.Data;
using InvoiceReportPrinter.WinForms.Models;
using IPS_PROBACKOFFICE.Data;
using IPS_PROBACKOFFICE.Models;
using System.Globalization;
using System.Net;
using System.Text;

namespace IPS_PROBACKOFFICE;

public sealed class FeeSummaryForm : Form
{
    private const int BankColumnWidth = 180;
    private const int FeeColumnWidth = 105;
    private const int ReportTableWidth = BankColumnWidth + (FeeColumnWidth * 4);

    private static readonly (string Display, string Match)[] ProductRows =
    [
        ("DF 1M", "DF 1M"),
        ("NDF 1M", "NDF 1M"),
        ("NDF MAR", "NDF MAR"),
        ("NDF TOMMAR", "NDF TOMMAR"),
        ("BASIS_MAR", "BASIS MAR"),
        ("BASIS_TOMMAR", "BASIS TOMMAR")
    ];

    private readonly TradeRepository _tradeRepository;
    private readonly BrokerageFeeRepository _feeRepository;
    private readonly BusinessDaySettings _businessDaySettings;
    private readonly DateTimePicker _date = new() { Format = DateTimePickerFormat.Short, Width = 130 };
    private readonly NumericUpDown _exchangeRate = new()
    {
        Width = 110,
        DecimalPlaces = 2,
        Minimum = 0,
        Maximum = 10000,
        Increment = 0.10m,
        ThousandsSeparator = true,
        Value = 0
    };
    private readonly Button _search = new() { Text = "조회", AutoSize = true };
    private readonly Button _excel = new() { Text = "엑셀 저장", AutoSize = true, Enabled = false };
    private readonly Button _email = new() { Text = "메일 전송", AutoSize = true, Enabled = false };
    private readonly Label _status = new() { AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(12, 8, 3, 0) };
    private readonly TableLayoutPanel _sheet = new();
    private IReadOnlyList<TradeRow> _dailyRows = Array.Empty<TradeRow>();
    private IReadOnlyList<TradeRow> _monthToDateRows = Array.Empty<TradeRow>();
    private IReadOnlyDictionary<string, string> _currencies = new Dictionary<string, string>();

    public FeeSummaryForm(
        TradeRepository tradeRepository,
        BrokerageFeeRepository feeRepository,
        BusinessDaySettings businessDaySettings)
    {
        _tradeRepository = tradeRepository;
        _feeRepository = feeRepository;
        _businessDaySettings = businessDaySettings;

        Text = "대표이사 보고";
        Font = new Font("맑은 고딕", 10F);
        BackColor = Color.White;
        Padding = new Padding(18);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 14)
        };
        toolbar.Controls.Add(new Label { Text = "거래일", AutoSize = true, Margin = new Padding(3, 8, 3, 0) });
        toolbar.Controls.Add(_date);
        toolbar.Controls.Add(new Label { Text = "환율(USD/KRW)", AutoSize = true, Margin = new Padding(12, 8, 3, 0) });
        toolbar.Controls.Add(_exchangeRate);
        toolbar.Controls.Add(_search);
        toolbar.Controls.Add(_excel);
        toolbar.Controls.Add(_email);
        toolbar.Controls.Add(_status);

        _sheet.AutoSize = true;
        _sheet.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _sheet.ColumnCount = 5;
        _sheet.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, BankColumnWidth));
        for (int i = 0; i < 4; i++)
            _sheet.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, FeeColumnWidth));

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
        scroll.Controls.Add(_sheet);

        Controls.Add(scroll);
        Controls.Add(toolbar);

        _search.Click += async (_, _) => await LoadSummaryAsync();
        _excel.Click += (_, _) => ExportToExcel();
        _email.Click += (_, _) => ShowEmailDialog();
        _exchangeRate.ValueChanged += (_, _) =>
        {
            if (_dailyRows.Count > 0 || _monthToDateRows.Count > 0)
                DrawSheet(_dailyRows, _monthToDateRows, _currencies);
        };
        _date.ValueChanged += async (_, _) => await LoadSummaryAsync();
        _businessDaySettings.Changed += async (_, _) => await LoadSummaryAsync();
        Shown += async (_, _) => await LoadSummaryAsync();
    }

    private async Task LoadSummaryAsync()
    {
        if (!_search.Enabled)
            return;

        _search.Enabled = false;
        _status.Text = "조회 중...";

        try
        {
            DateTime selectedDate = _date.Value.Date;
            (DateTime dailyStart, DateTime dailyEnd) =
                _businessDaySettings.GetDailyRange(selectedDate);
            (DateTime monthStart, DateTime monthEnd) =
                _businessDaySettings.GetMonthToDateRange(selectedDate);
            IReadOnlyList<Institution> institutions = await _tradeRepository.GetInstitutionsAsync();
            IReadOnlyList<BrokerageFeeRow> feeSchedules = await _feeRepository.GetAllAsync();
            var monthlyRows = new List<TradeRow>();

            foreach (Institution institution in institutions)
            {
                IReadOnlyList<TradeRow> rows =
                    await _tradeRepository.GetTradesFromViewAsync(
                        institution,
                        monthStart,
                        monthEnd);
                monthlyRows.AddRange(rows);
            }

            List<TradeRow> dailyRows = monthlyRows
                .Where(x => x.TradeDate >= dailyStart && x.TradeDate < dailyEnd)
                .ToList();

            // Monthly Total은 해당 월 전체가 아니라 월초부터 선택일까지의 누적이다.
            List<TradeRow> monthToDateRows = monthlyRows
                .Where(x => x.TradeDate >= monthStart && x.TradeDate < monthEnd)
                .ToList();

            var currencies = feeSchedules
                .GroupBy(x => x.InstCode.ToString())
                .ToDictionary(
                    x => x.Key,
                    x => x.First().CurrencyCode?.Trim().ToUpperInvariant() ?? "KRW",
                    StringComparer.OrdinalIgnoreCase);

            DrawSheet(dailyRows, monthToDateRows, currencies);
            _dailyRows = dailyRows;
            _monthToDateRows = monthToDateRows;
            _currencies = currencies;
            _excel.Enabled = true;
            _email.Enabled = true;
            _status.Text =
                $"{selectedDate:yyyy-MM-dd} · {dailyStart:HH:mm}~익일 {dailyEnd:HH:mm} · 전체 상품 {dailyRows.Count:N0}건";
        }
        catch (Exception ex)
        {
            _status.Text = "조회 실패";
            MessageBox.Show(ex.Message, "대표이사 보고 조회 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _search.Enabled = true;
        }
    }

    private void ExportToExcel()
    {
        try
        {
            using var dialog = new SaveFileDialog
            {
                Filter = "Excel 통합 문서 (*.xlsx)|*.xlsx",
                FileName = $"중개수수료현황_{_date.Value:yyyyMMdd}.xlsx",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            using XLWorkbook workbook = CreateReportWorkbook();
            workbook.SaveAs(dialog.FileName);
            MessageBox.Show($"엑셀 저장이 완료되었습니다.\n{dialog.FileName}", "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "엑셀 저장 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private XLWorkbook CreateReportWorkbook()
    {
        var workbook = new XLWorkbook();
        IXLWorksheet ws = workbook.Worksheets.Add("Fee Summary");
        ws.ShowGridLines = false;
        int row = 2;

        ws.Range(row, 2, row + 1, 2).Merge().Value = UiLanguage.Text("Bank");
        ws.Range(row, 3, row, 4).Merge().Value = UiLanguage.Text("Brokerage");
        ws.Range(row, 5, row, 6).Merge().Value = UiLanguage.Text("Switch Fee");
        row++;
        ws.Cell(row, 3).Value = "USD";
        ws.Cell(row, 4).Value = "KRW";
        ws.Cell(row, 5).Value = "USD";
        ws.Cell(row, 6).Value = "KRW";
        row++;

        foreach (IGrouping<string, TradeRow> institution in _dailyRows
                     .GroupBy(x => x.InstitutionName, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(x => x.Key))
        {
            FeeValues values = Calculate(institution, _currencies);
            WriteFeeLine(ws, row++, institution.Key, values);
        }

        decimal exchangeRate = _exchangeRate.Value;
        WriteTotalBlock(ws, ref row, UiLanguage.Text("Daily Total"), Calculate(_dailyRows, _currencies), exchangeRate);
        FeeValues monthly = Calculate(_monthToDateRows, _currencies);
        WriteTotalBlock(ws, ref row, UiLanguage.Text("Monthly Total"), monthly, exchangeRate);

        ws.Cell(row, 2).Value = UiLanguage.Text("Net Fee");
        ws.Cell(row, 2).Style.Font.Bold = true;
        ws.Range(row, 3, row, 6).Merge();
        ws.Cell(row, 3).Value = ToKrw(monthly, exchangeRate);
        int borderedLastRow = row;
        row++;

        IReadOnlyList<(string Display, int Count)> productCounts = GetTradedProductCounts(_dailyRows);
        int productStartRow = row;
        ws.Range(row, 2, row + productCounts.Count - 1, 2).Merge().Value = UiLanguage.Text("Product");
        foreach ((string display, int count) in productCounts)
        {
            ws.Range(row, 3, row, 4).Merge().Value = display;
            ws.Range(row, 5, row, 6).Merge();
            ws.Cell(row, 5).Value = count;
            row++;
        }

        IXLRange table = ws.Range(2, 2, borderedLastRow, 6);
        table.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        table.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        table.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.Range(2, 2, 3, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range(2, 2, 3, 6).Style.Font.Bold = false;
        ws.Column(2).Width = 18;
        ws.Columns(3, 6).Width = 12;
        ws.Rows(2, row - 1).Height = 22;
        ws.Range(4, 3, borderedLastRow, 6).Style.NumberFormat.Format = "#,##0;[Red]-#,##0;-";
        ws.Range(4, 5, borderedLastRow, 6).Style.Font.FontColor = XLColor.Red;
        ws.Range(2, 2, row - 1, 6).Style.Font.FontName = "맑은 고딕";
        ws.Range(2, 2, row - 1, 6).Style.Font.FontSize = 11;
        ws.Range(productStartRow, 2, row - 1, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Cell(2, 8).Value = "USD/KRW 환율";
        ws.Cell(2, 9).Value = exchangeRate;
        ws.Cell(2, 9).Style.NumberFormat.Format = "#,##0.00";

        return workbook;
    }

    private void ShowEmailDialog()
    {
        if (_dailyRows.Count == 0 && _monthToDateRows.Count == 0)
        {
            MessageBox.Show("먼저 대표이사 보고를 조회해주세요.");
            return;
        }

        try
        {
            string htmlBody = CreateEmailHtml();
            using var dialog = new FeeSummaryEmailDialog(_date.Value, htmlBody);
            dialog.ShowDialog(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "메일 전송창 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private string CreateEmailHtml()
    {
        var html = new StringBuilder();
        html.Append("<html><body style=\"margin:16px;background:#fff;font-family:'Malgun Gothic','맑은 고딕',Arial,sans-serif;font-size:10pt;color:#111;\">");
        html.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"border-collapse:collapse;mso-table-lspace:0pt;mso-table-rspace:0pt;\"><tr>");
        html.Append("<td valign=\"top\" style=\"vertical-align:top;\">");
        html.Append($"<table width=\"{ReportTableWidth}\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"border-collapse:collapse;width:{ReportTableWidth}px;table-layout:fixed;mso-table-lspace:0pt;mso-table-rspace:0pt;\">");
        html.Append($"<colgroup><col style=\"width:{BankColumnWidth}px;\">");
        for (int i = 0; i < 4; i++)
            html.Append($"<col style=\"width:{FeeColumnWidth}px;\">");
        html.Append("</colgroup>");
        html.Append("<tr>")
            .Append(HtmlCell(UiLanguage.Text("Bank"), header: true, width: BankColumnWidth, height: 44))
            .Append(HtmlHeaderGroup(UiLanguage.Text("Brokerage")))
            .Append(HtmlHeaderGroup(UiLanguage.Text("Switch Fee")))
            .Append("</tr>");

        foreach (IGrouping<string, TradeRow> institution in _dailyRows
                     .GroupBy(x => x.InstitutionName, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(x => x.Key))
        {
            FeeValues values = Calculate(institution, _currencies);
            html.Append("<tr>")
                .Append(HtmlCell(institution.Key, center: true, width: BankColumnWidth))
                .Append(HtmlCell(Format(values.BrokerageUsd)))
                .Append(HtmlCell(Format(values.BrokerageKrw)))
                .Append(HtmlCell(Format(values.SwitchUsd), red: true))
                .Append(HtmlCell(Format(values.SwitchKrw), red: true))
                .Append("</tr>");
        }

        decimal exchangeRate = _exchangeRate.Value;
        FeeValues daily = Calculate(_dailyRows, _currencies);
        FeeValues monthly = Calculate(_monthToDateRows, _currencies);
        AppendHtmlTotalBlock(html, UiLanguage.Text("Daily Total"), daily, exchangeRate);
        AppendHtmlTotalBlock(html, UiLanguage.Text("Monthly Total"), monthly, exchangeRate);

        html.Append("<tr>")
            .Append(HtmlCell(UiLanguage.Text("Net Fee"), bold: true, center: true, width: BankColumnWidth, thickBottom: true))
            .Append(HtmlCell(Format(ToKrw(monthly, exchangeRate)), colSpan: 4, bold: true, thickBottom: true))
            .Append("</tr>");

        IReadOnlyList<(string Display, int Count)> products = GetTradedProductCounts(_dailyRows);
        for (int i = 0; i < products.Count; i++)
        {
            html.Append("<tr>");
            if (i == 0)
                html.Append(HtmlCell(
                    UiLanguage.Text("Product"),
                    center: true,
                    rowSpan: products.Count,
                    width: BankColumnWidth,
                    productBorder: true));
            html.Append(HtmlCell(
                    products[i].Display,
                    colSpan: 2,
                    center: true,
                    green: false,
                    productBorder: true))
                .Append(HtmlCell(
                    products[i].Count.ToString("N0", CultureInfo.InvariantCulture),
                    colSpan: 2,
                    productBorder: true))
                .Append("</tr>");
        }

        html.Append("</table></td>");
        html.Append("<td valign=\"top\" style=\"vertical-align:top;padding-left:45px;padding-top:8px;\">");
        html.Append("<table cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"border-collapse:collapse;white-space:nowrap;mso-table-lspace:0pt;mso-table-rspace:0pt;\"><tr>")
            .Append("<td style=\"border:none;padding:0 16px 0 0;text-align:left;font-weight:normal;vertical-align:middle;\">USD/KRW</td>")
            .Append("<td style=\"border:none;padding:0;text-align:right;font-weight:normal;vertical-align:middle;\">")
            .Append(_exchangeRate.Value.ToString("N2", CultureInfo.InvariantCulture))
            .Append("</td></tr></table>");
        html.Append("</td></tr></table></body></html>");
        return html.ToString();
    }

    private static void AppendHtmlTotalBlock(
        StringBuilder html,
        string title,
        FeeValues values,
        decimal exchangeRate)
    {
        decimal brokerageKrw = decimal.Round(values.BrokerageUsd * exchangeRate + values.BrokerageKrw, 0, MidpointRounding.AwayFromZero);
        decimal switchKrw = decimal.Round(values.SwitchUsd * exchangeRate + values.SwitchKrw, 0, MidpointRounding.AwayFromZero);
        bool thickTop = title.Equals(UiLanguage.Text("Daily Total"), StringComparison.Ordinal);
        html.Append("<tr>")
            .Append(HtmlCell(title, rowSpan: 2, bold: true, center: true, width: BankColumnWidth, thickTop: thickTop, outlookBorderFix: true, height: 44))
            .Append(HtmlCell(Format(values.BrokerageUsd), thickTop: thickTop))
            .Append(HtmlCell(Format(values.BrokerageKrw), thickTop: thickTop))
            .Append(HtmlCell(Format(values.SwitchUsd), red: true, thickTop: thickTop))
            .Append(HtmlCell(Format(values.SwitchKrw), red: true, thickTop: thickTop))
            .Append("</tr><tr>")
            .Append(HtmlCell("\u00A0"))
            .Append(HtmlCell(Format(brokerageKrw)))
            .Append(HtmlCell("\u00A0", red: true))
            .Append(HtmlCell(Format(switchKrw), red: true))
            .Append("</tr>");
    }

    private static string HtmlHeaderGroup(string title)
    {
        int groupWidth = FeeColumnWidth * 2;
        return
            $"<td colspan=\"2\" width=\"{groupWidth}\" style=\"border:1px solid #666;padding:0;vertical-align:middle;width:{groupWidth}px;\">" +
            $"<table role=\"presentation\" width=\"{groupWidth}\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"border-collapse:collapse;width:{groupWidth}px;table-layout:fixed;mso-table-lspace:0pt;mso-table-rspace:0pt;\"><tbody>" +
            "<tr>" +
            $"<td colspan=\"2\" width=\"{groupWidth}\" style=\"border:none;border-bottom:1px solid #666;padding:3px 6px;height:22px;line-height:18px;vertical-align:middle;text-align:center;white-space:nowrap;mso-line-height-rule:exactly;\">{WebUtility.HtmlEncode(title)}</td>" +
            "</tr><tr>" +
            $"<td width=\"{FeeColumnWidth}\" style=\"border:none;border-right:1px solid #666;padding:3px 6px;height:22px;line-height:18px;vertical-align:middle;text-align:center;white-space:nowrap;mso-line-height-rule:exactly;\">USD</td>" +
            $"<td width=\"{FeeColumnWidth}\" style=\"border:none;padding:3px 6px;height:22px;line-height:18px;vertical-align:middle;text-align:center;white-space:nowrap;mso-line-height-rule:exactly;\">KRW</td>" +
            "</tr></tbody></table></td>";
    }

    private static string HtmlCell(
        string text,
        int colSpan = 1,
        int rowSpan = 1,
        bool header = false,
        bool bold = false,
        bool center = false,
        bool red = false,
        bool green = false,
        int? width = null,
        bool productBorder = false,
        bool thickTop = false,
        bool thickBottom = false,
        bool suppressTop = false,
        bool suppressBottom = false,
        bool verticalBottom = false,
        bool outlookBorderFix = false,
        int height = 22)
    {
        string tag = header ? "th" : "td";
        int cellWidth = width ?? (FeeColumnWidth * colSpan);
        string attributes =
            (colSpan > 1 ? $" colspan=\"{colSpan}\"" : string.Empty) +
            (rowSpan > 1 ? $" rowspan=\"{rowSpan}\"" : string.Empty) +
            $" width=\"{cellWidth}\"";
        string borderStyle = productBorder
            ? "border-top:1px solid #666;border-left:none;border-right:none;border-bottom:none;"
            : "border:1px solid #666;";
        if (thickTop)
            borderStyle += "border-top:2px solid #111;";
        if (thickBottom)
            borderStyle += "border-bottom:2px solid #111;";
        if (suppressTop)
            borderStyle += "border-top:none;";
        if (suppressBottom)
            borderStyle += "border-bottom:none;";
        if (outlookBorderFix)
            borderStyle += "mso-border-alt:solid #666 0.75pt;mso-border-left-alt:solid #666 0.75pt;";
        string style =
            borderStyle +
            (verticalBottom ? "padding:3px 6px 0 6px;" : "padding:3px 6px;") +
            $"height:{height}px;" +
            $"line-height:18px;vertical-align:{(verticalBottom ? "bottom" : "middle")};mso-line-height-rule:exactly;" +
            "white-space:nowrap;" +
            $"text-align:{(center || header ? "center" : "right")};" +
            (header ? "background:#fff;font-weight:normal;" : string.Empty) +
            (bold ? "font-weight:bold;" : string.Empty) +
            (red ? "color:#d00000;" : string.Empty) +
            (green ? "color:#138a13;" : string.Empty) +
            $"width:{cellWidth}px;min-width:{cellWidth}px;max-width:{cellWidth}px;";
        return $"<{tag}{attributes} style=\"{style}\">{WebUtility.HtmlEncode(text)}</{tag}>";
    }

    private static void WriteFeeLine(IXLWorksheet ws, int row, string title, FeeValues values)
    {
        ws.Cell(row, 2).Value = title;
        ws.Cell(row, 3).Value = values.BrokerageUsd;
        ws.Cell(row, 4).Value = values.BrokerageKrw;
        ws.Cell(row, 5).Value = values.SwitchUsd;
        ws.Cell(row, 6).Value = values.SwitchKrw;
    }

    private static void WriteTotalBlock(
        IXLWorksheet ws,
        ref int row,
        string title,
        FeeValues values,
        decimal exchangeRate)
    {
        ws.Range(row, 2, row + 1, 2).Merge().Value = title;
        ws.Cell(row, 2).Style.Font.Bold = true;
        ws.Cell(row, 3).Value = values.BrokerageUsd;
        ws.Cell(row, 4).Value = values.BrokerageKrw;
        ws.Cell(row, 5).Value = values.SwitchUsd;
        ws.Cell(row, 6).Value = values.SwitchKrw;
        row++;
        ws.Range(row, 3, row, 4).Merge();
        ws.Cell(row, 3).Value = decimal.Round(values.BrokerageUsd * exchangeRate + values.BrokerageKrw, 0, MidpointRounding.AwayFromZero);
        ws.Range(row, 5, row, 6).Merge();
        ws.Cell(row, 5).Value = decimal.Round(values.SwitchUsd * exchangeRate + values.SwitchKrw, 0, MidpointRounding.AwayFromZero);
        row++;
    }

    private void DrawSheet(
        IReadOnlyList<TradeRow> dailyRows,
        IReadOnlyList<TradeRow> monthlyRows,
        IReadOnlyDictionary<string, string> currencies)
    {
        _sheet.SuspendLayout();
        _sheet.Controls.Clear();
        _sheet.RowStyles.Clear();
        _sheet.RowCount = 0;

        int row = 0;
        AddCell(UiLanguage.Text("Bank"), 0, row, bold: false, align: ContentAlignment.MiddleCenter, rowSpan: 2);
        AddCell(UiLanguage.Text("Brokerage"), 1, row, align: ContentAlignment.MiddleCenter, columnSpan: 2);
        AddCell(UiLanguage.Text("Switch Fee"), 3, row, align: ContentAlignment.MiddleCenter, columnSpan: 2);
        row++;
        AddCell("USD", 1, row, align: ContentAlignment.MiddleCenter);
        AddCell("KRW", 2, row, align: ContentAlignment.MiddleCenter);
        AddCell("USD", 3, row, align: ContentAlignment.MiddleCenter);
        AddCell("KRW", 4, row, align: ContentAlignment.MiddleCenter);
        row++;

        foreach (IGrouping<string, TradeRow> institution in dailyRows
                     .GroupBy(x => x.InstitutionName, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(x => x.Key))
        {
            FeeValues values = Calculate(institution, currencies);
            AddFeeLine(institution.Key, values, ref row);
        }

        FeeValues daily = Calculate(dailyRows, currencies);
        decimal exchangeRate = _exchangeRate.Value;
        AddTotalBlock(UiLanguage.Text("Daily Total"), daily, ref row, exchangeRate);

        FeeValues monthly = Calculate(monthlyRows, currencies);
        AddTotalBlock(UiLanguage.Text("Monthly Total"), monthly, ref row, exchangeRate);

        decimal netFee = ToKrw(monthly, exchangeRate);
        AddCell(UiLanguage.Text("Net Fee"), 0, row, bold: true, align: ContentAlignment.MiddleCenter);
        AddCell(Format(netFee), 1, row, bold: false, align: ContentAlignment.MiddleRight, columnSpan: 4);
        row++;

        IReadOnlyList<(string Display, int Count)> productCounts = GetTradedProductCounts(dailyRows);
        AddCell(UiLanguage.Text("Product"), 0, row, align: ContentAlignment.MiddleCenter, rowSpan: productCounts.Count, border: false);
        foreach ((string display, int count) in productCounts)
        {
            AddCell(display, 1, row, color: Color.Black, align: ContentAlignment.MiddleCenter, columnSpan: 2, border: false);
            AddCell(count.ToString("N0", CultureInfo.InvariantCulture), 3, row, align: ContentAlignment.MiddleRight, columnSpan: 2, border: false);
            row++;
        }

        _sheet.ResumeLayout(true);
    }

    private void AddFeeLine(string title, FeeValues values, ref int row, bool bold = false)
    {
        AddCell(title, 0, row, bold: bold, align: ContentAlignment.MiddleCenter);
        AddCell(Format(values.BrokerageUsd), 1, row);
        AddCell(Format(values.BrokerageKrw), 2, row);
        AddCell(Format(values.SwitchUsd), 3, row, color: Color.Red);
        AddCell(Format(values.SwitchKrw), 4, row, color: Color.Red);
        row++;
    }

    private void AddTotalBlock(string title, FeeValues values, ref int row, decimal exchangeRate)
    {
        AddCell(title, 0, row, bold: true, align: ContentAlignment.MiddleCenter, rowSpan: 2);
        AddCell(Format(values.BrokerageUsd), 1, row);
        AddCell(Format(values.BrokerageKrw), 2, row);
        AddCell(Format(values.SwitchUsd), 3, row, color: Color.Red);
        AddCell(Format(values.SwitchKrw), 4, row, color: Color.Red);
        row++;
        decimal brokerageKrw = decimal.Round(values.BrokerageUsd * exchangeRate + values.BrokerageKrw, 0, MidpointRounding.AwayFromZero);
        decimal switchKrw = decimal.Round(values.SwitchUsd * exchangeRate + values.SwitchKrw, 0, MidpointRounding.AwayFromZero);
        AddCell(Format(brokerageKrw), 1, row, columnSpan: 2);
        AddCell(Format(switchKrw), 3, row, color: Color.Red, columnSpan: 2);
        row++;
    }

    private static FeeValues Calculate(
        IEnumerable<TradeRow> rows,
        IReadOnlyDictionary<string, string> currencies)
    {
        decimal brokerageUsd = 0m;
        decimal brokerageKrw = 0m;
        decimal switchUsd = 0m;
        decimal switchKrw = 0m;

        foreach (TradeRow trade in rows)
        {
            string currency = currencies.TryGetValue(trade.InstitutionCode, out string? value)
                ? value
                : "KRW";
            bool usd = currency.Equals("USD", StringComparison.OrdinalIgnoreCase);

            if (trade.IsSwitch)
            {
                if (usd) switchUsd += trade.BrokerageAmount;
                else switchKrw += trade.BrokerageAmount;
            }
            else
            {
                if (usd) brokerageUsd += trade.BrokerageAmount;
                else brokerageKrw += trade.BrokerageAmount;
            }
        }

        return new FeeValues(brokerageUsd, brokerageKrw, switchUsd, switchKrw);
    }

    private void AddCell(
        string text,
        int column,
        int row,
        bool bold = false,
        Color? color = null,
        ContentAlignment align = ContentAlignment.MiddleRight,
        int columnSpan = 1,
        int rowSpan = 1,
        bool border = true)
    {
        while (_sheet.RowCount <= row)
        {
            _sheet.RowCount++;
            _sheet.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        }

        var label = new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            BorderStyle = border ? BorderStyle.FixedSingle : BorderStyle.None,
            TextAlign = align,
            Padding = new Padding(6, 0, 6, 0),
            BackColor = Color.White,
            ForeColor = color ?? Color.Black,
            Font = bold ? new Font(Font, FontStyle.Bold) : Font
        };
        _sheet.Controls.Add(label, column, row);
        if (columnSpan > 1) _sheet.SetColumnSpan(label, columnSpan);
        if (rowSpan > 1) _sheet.SetRowSpan(label, rowSpan);
    }

    private static string Format(decimal value) =>
        value == 0m ? "-" : value.ToString("N0", CultureInfo.InvariantCulture);

    private static decimal ToKrw(FeeValues values, decimal exchangeRate) =>
        decimal.Round(
            (values.BrokerageUsd + values.SwitchUsd) * exchangeRate
            + values.BrokerageKrw
            + values.SwitchKrw,
            0,
            MidpointRounding.AwayFromZero);

    private static int CountProduct(IEnumerable<TradeRow> rows, string product)
    {
        string normalizedProduct = NormalizeProduct(product);
        int rowCount = rows.Count(
            x => NormalizeProduct(x.Product) == normalizedProduct);

        // DF/NDF는 BUY/SELL 두 행이 한 거래이므로 실제 건수는 2로 나눈다.
        if (normalizedProduct.StartsWith("DF ", StringComparison.Ordinal)
            || normalizedProduct.StartsWith("NDF ", StringComparison.Ordinal))
            return rowCount / 2;

        return rowCount;
    }

    private static IReadOnlyList<(string Display, int Count)> GetTradedProductCounts(
        IEnumerable<TradeRow> rows)
    {
        var result = ProductRows
            .Select(x => (x.Display, Count: CountProduct(rows, x.Match)))
            .Where(x => x.Count > 0)
            .ToList();

        if (result.Count == 0)
            result.Add(("거래 없음", 0));

        return result;
    }

    private static string NormalizeProduct(string value) =>
        string.Join(
            ' ',
            value.Trim()
                .ToUpperInvariant()
                .Replace('_', ' ')
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private sealed record FeeValues(
        decimal BrokerageUsd,
        decimal BrokerageKrw,
        decimal SwitchUsd,
        decimal SwitchKrw);
}
