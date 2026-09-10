using System.Globalization;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using IPS_PROBACKOFFICE.Data;
using IPS_PROBACKOFFICE.Models;
using Microsoft.VisualBasic.FileIO;

namespace IPS_PROBACKOFFICE;

public sealed partial class FxValueDateForm : Form
{
    private static readonly Regex ValuePattern = new(
        @"^(?<tenor>S/N|1M)\s+(?<start>\d{2}/\d{2})\s*~\s*(?<end>\d{2}/\d{2})\s*\((?<days>\d+)일(?:,\s*(?<adjust>[+-]?\d+))?\s*,?\)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly FxValueDateRepository _repository;
    private readonly DateTimePicker _searchDate = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd", Width = 115 };
    private readonly Button _holidayManageButton = new()
    {
        Text = "휴일 관리",
        Width = 100,
        Height = 29,
        BackColor = Color.FromArgb(38, 116, 181),
        ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat
    };
    private readonly DataGridView _searchGrid = CreateGrid();
    private readonly Dictionary<string, CheckBox> _tenorChecks = new(StringComparer.OrdinalIgnoreCase);
    private bool _updatingTenorChecks;
    private readonly HolidayCalendarPanel _holidayCalendar;
    private readonly TextBox _filePath = new() { ReadOnly = true, Width = 600 };
    private readonly Button _selectButton = new() { Text = "파일 선택", Width = 105 };
    private readonly Button _importButton = new() { Text = "DB 저장", Width = 105, Enabled = false };
    private readonly DataGridView _previewGrid = CreateGrid();
    private readonly ToolStripStatusLabel _status = new() { Text = "기준일을 선택하세요." };
    private List<FxValueDateRow> _previewRows = [];
    private int _calculationVersion;

    public FxValueDateForm(FxValueDateRepository repository)
    {
        _repository = repository;
        _holidayCalendar = new HolidayCalendarPanel(repository);
        Font = new Font("맑은 고딕", 10F);
        _searchDate.Value = DateTime.Today;

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildSearchTab());
        var strip = new StatusStrip();
        strip.Items.Add(_status);
        Controls.Add(tabs);
        Controls.Add(strip);

        _searchDate.ValueChanged += async (_, _) => await SearchAsync();
        _holidayManageButton.Click += async (_, _) => await _holidayCalendar.ShowManagementAsync(this);
        _holidayCalendar.HolidaysChanged += async (_, _) => await SearchAsync();
        Shown += async (_, _) => await SearchAsync();
    }

    private TabPage BuildSearchTab()
    {
        var tab = new TabPage("Settle Date 자동 계산");
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 56, Padding = new Padding(10), WrapContents = false };
        top.Controls.Add(new Label { Text = "기준일", AutoSize = true, Margin = new Padding(0, 8, 6, 0) });
        top.Controls.Add(_searchDate);
        top.Controls.Add(_holidayManageButton);
        top.Controls.Add(new Label
        {
            Text = "날짜를 선택하면 자동 계산됩니다.",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(10, 8, 0, 0)
        });
        var left = new Panel { Dock = DockStyle.Fill };
        left.Controls.Add(_searchGrid);
        left.Controls.Add(BuildTenorFilter());
        left.Controls.Add(top);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 350F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.Controls.Add(left, 0, 0);
        layout.Controls.Add(_holidayCalendar, 1, 0);
        tab.Controls.Add(layout);
        return tab;
    }

    private Control BuildTenorFilter()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 68,
            Padding = new Padding(10, 5, 10, 5),
            AutoScroll = true,
            WrapContents = true
        };
        panel.Controls.Add(new Label
        {
            Text = "Tenor",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 5, 8, 0)
        });
        var allOn = new Button { Text = "ALL ON", AutoSize = true, Height = 27, Margin = new Padding(0, 0, 5, 0) };
        var allOff = new Button { Text = "ALL OFF", AutoSize = true, Height = 27, Margin = new Padding(0, 0, 10, 0) };
        allOn.Click += async (_, _) => await SetAllTenorsAsync(true);
        allOff.Click += async (_, _) => await SetAllTenorsAsync(false);
        panel.Controls.Add(allOn);
        panel.Controls.Add(allOff);

        HashSet<string>? savedTenors = LoadSavedTenors();
        string[] tenors = ["O/N", "T/N", "S/N", "1W", "2W", "3W",
            "1M", "2M", "3M", "4M", "5M", "6M", "7M", "8M", "9M", "10M", "11M", "12M"];
        foreach (string tenor in tenors)
        {
            var check = new CheckBox
            {
                Text = tenor,
                Checked = savedTenors is null || savedTenors.Contains(tenor),
                AutoSize = true,
                Margin = new Padding(3, 4, 8, 0)
            };
            check.CheckedChanged += async (_, _) =>
            {
                if (_updatingTenorChecks) return;
                SaveTenorSelection();
                await SearchAsync();
            };
            _tenorChecks[tenor] = check;
            panel.Controls.Add(check);
        }
        return panel;
    }

    private async Task SetAllTenorsAsync(bool isChecked)
    {
        _updatingTenorChecks = true;
        try
        {
            foreach (CheckBox check in _tenorChecks.Values) check.Checked = isChecked;
        }
        finally
        {
            _updatingTenorChecks = false;
        }
        SaveTenorSelection();
        await SearchAsync();
    }

    private static string TenorSettingsPath
    {
        get
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IPS", "IPS_PROBACKOFFICE");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "settle_date_tenors.txt");
        }
    }

    private static HashSet<string>? LoadSavedTenors()
    {
        try
        {
            if (!File.Exists(TenorSettingsPath)) return null;
            return File.ReadAllLines(TenorSettingsPath)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return null;
        }
    }

    private void SaveTenorSelection()
    {
        try
        {
            File.WriteAllLines(TenorSettingsPath,
                _tenorChecks.Where(x => x.Value.Checked).Select(x => x.Key));
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "테너 선택 저장 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private TabPage BuildImportTab()
    {
        var tab = new TabPage("CSV/엑셀 임포트");
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 56, Padding = new Padding(10), WrapContents = false };
        top.Controls.Add(new Label { Text = "Settle Date 파일", AutoSize = true, Margin = new Padding(0, 8, 6, 0) });
        top.Controls.Add(_filePath);
        top.Controls.Add(_selectButton);
        top.Controls.Add(_importButton);
        tab.Controls.Add(_previewGrid);
        tab.Controls.Add(top);
        return tab;
    }

    private async Task SearchAsync()
    {
        int version = ++_calculationVersion;
        try
        {
            UseWaitCursor = true;
            _status.Text = "계산 중...";
            DateTime selectedDate = _searchDate.Value.Date;
            IReadOnlyList<HolidayOverrideRow> overrides = await _repository.GetHolidayOverridesAsync(
                selectedDate.AddMonths(-1), selectedDate.AddMonths(14));
            if (version != _calculationVersion) return;
            Func<DateTime, bool> jointBusinessDay =
                day => HolidayProvider.IsKoreaUsBusinessDay(day, overrides);
            Func<DateTime, bool> koreanBusinessDay =
                day => HolidayProvider.IsKoreanBusinessDay(day, overrides);
            DateTime tomorrowDate = AddBusinessDays(selectedDate, 1, jointBusinessDay);
            DateTime spotDate = CalculateSpotDate(selectedDate, koreanBusinessDay, jointBusinessDay);
            HashSet<string> selectedTenors = _tenorChecks
                .Where(x => x.Value.Checked)
                .Select(x => x.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _searchGrid.DataSource = ToCalculatedRows(
                selectedDate, tomorrowDate, spotDate, jointBusinessDay, koreanBusinessDay, selectedTenors);
            FormatGrid(_searchGrid);
            _status.Text = $"계산 완료: Spot {spotDate:yyyy-MM-dd} · 선택 테너 {selectedTenors.Count}개";
        }
        catch (Exception ex)
        {
            _status.Text = "조회 실패";
            MessageBox.Show(ex.Message, "Settle Date 조회 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    internal static DateTime CalculateSpotDate(
        DateTime tradeDate,
        Func<DateTime, bool> koreanBusinessDay,
        Func<DateTime, bool> jointBusinessDay)
    {
        // 중간 미국 휴일은 T+2 계산에서 제외하지 않는다.
        // 한국 영업일로 이틀을 센 뒤 최종 결제일만 한·미 영업일로 조정한다.
        DateTime target = AddBusinessDays(tradeDate, 2, koreanBusinessDay);
        return RollFollowing(target, jointBusinessDay);
    }

    private static DateTime AddBusinessDays(DateTime start, int days, Func<DateTime, bool> isBusinessDay)
    {
        int direction = Math.Sign(days);
        int remaining = Math.Abs(days);
        DateTime result = start.Date;
        while (remaining > 0)
        {
            result = result.AddDays(direction);
            if (isBusinessDay(result)) remaining--;
        }
        return result;
    }

    internal static DataTable ToCalculatedRows(
        DateTime tradeDate,
        DateTime tomorrowDate,
        DateTime spotDate,
        Func<DateTime, bool> jointBusinessDay,
        Func<DateTime, bool> koreanBusinessDay,
        IReadOnlySet<string> selectedTenors)
    {
        var table = new DataTable();
        table.Columns.Add("기준일", typeof(DateTime));
        table.Columns.Add("Tenor", typeof(string));
        table.Columns.Add("Start Date", typeof(DateTime));
        table.Columns.Add("Settle Date", typeof(DateTime));
        table.Columns.Add("Fixing Date", typeof(DateTime));
        table.Columns.Add("Days", typeof(int));

        if (selectedTenors.Contains("O/N"))
            AddCalculatedRow(table, tradeDate, "O/N", tradeDate, tomorrowDate, null,
                (tomorrowDate - tradeDate).Days);
        if (selectedTenors.Contains("T/N"))
            AddCalculatedRow(table, tradeDate, "T/N", tomorrowDate, spotDate, null,
                (spotDate - tomorrowDate).Days);
        DateTime snEnd = AddBusinessDays(spotDate, 1, jointBusinessDay);
        if (selectedTenors.Contains("S/N"))
            AddCalculatedRow(table, tradeDate, "S/N", spotDate, snEnd, null,
                (snEnd - spotDate).Days);

        for (int week = 1; week <= 3; week++)
        {
            string tenor = $"{week}W";
            if (!selectedTenors.Contains(tenor)) continue;
            DateTime target = spotDate.AddDays(week * 7);
            DateTime settleDate = RollFollowing(target, jointBusinessDay);
            DateTime fixingDate = AddBusinessDays(settleDate, -2, koreanBusinessDay);
            AddCalculatedRow(table, tradeDate, tenor, spotDate, settleDate, fixingDate,
                (settleDate - spotDate).Days);
        }

        for (int month = 1; month <= 12; month++)
        {
            string tenor = $"{month}M";
            if (!selectedTenors.Contains(tenor)) continue;
            bool isEndOfMonth = IsLastBusinessDayOfMonth(spotDate, jointBusinessDay);
            DateTime target = isEndOfMonth
                ? new DateTime(spotDate.Year, spotDate.Month, 1).AddMonths(month + 1).AddDays(-1)
                : spotDate.AddMonths(month);
            DateTime settleDate = isEndOfMonth
                ? RollPreceding(target, jointBusinessDay)
                : RollFollowing(target, jointBusinessDay);
            DateTime fixingDate = AddBusinessDays(settleDate, -2, koreanBusinessDay);
            AddCalculatedRow(table, tradeDate, tenor, spotDate, settleDate, fixingDate,
                (settleDate - spotDate).Days);
        }
        return table;
    }

    private static void AddCalculatedRow(
        DataTable table,
        DateTime tradeDate,
        string tenor,
        DateTime startDate,
        DateTime settleDate,
        DateTime? fixingDate,
        int days) =>
        table.Rows.Add(tradeDate, tenor, startDate, settleDate,
            (object?)fixingDate ?? DBNull.Value, days);

    private static DateTime RollFollowing(DateTime date, Func<DateTime, bool> isBusinessDay)
    {
        DateTime result = date.Date;
        while (!isBusinessDay(result)) result = result.AddDays(1);
        return result;
    }

    private static DateTime RollPreceding(DateTime date, Func<DateTime, bool> isBusinessDay)
    {
        DateTime result = date.Date;
        while (!isBusinessDay(result)) result = result.AddDays(-1);
        return result;
    }

    private static bool IsLastBusinessDayOfMonth(DateTime date, Func<DateTime, bool> isBusinessDay)
    {
        DateTime next = date.Date.AddDays(1);
        while (next.Month == date.Month)
        {
            if (isBusinessDay(next)) return false;
            next = next.AddDays(1);
        }
        return isBusinessDay(date);
    }

    private void SelectImportFile()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Settle Date 파일 (*.csv;*.xlsx;*.xlsm)|*.csv;*.xlsx;*.xlsm|CSV 파일 (*.csv)|*.csv|Excel 통합 문서 (*.xlsx;*.xlsm)|*.xlsx;*.xlsm",
            Title = "FX 테너별 Settle Date 파일 선택"
        };
        if (dialog.ShowDialog() != DialogResult.OK) return;

        try
        {
            _previewRows = Path.GetExtension(dialog.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase)
                ? ReadCsv(dialog.FileName)
                : ReadExcel(dialog.FileName);
            _filePath.Text = dialog.FileName;
            _previewGrid.DataSource = ToDisplayRows(_previewRows);
            FormatGrid(_previewGrid);
            _importButton.Enabled = _previewRows.Count > 0;
            _status.Text = $"S/N · 1M {_previewRows.Count:N0}건 미리보기 — 확인 후 DB 저장을 누르세요.";
        }
        catch (Exception ex)
        {
            _previewRows = [];
            _previewGrid.DataSource = null;
            _importButton.Enabled = false;
            _status.Text = "파일 읽기 실패";
            MessageBox.Show(ex.Message, "Settle Date 파일 읽기 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task ImportAsync()
    {
        if (_previewRows.Count == 0) return;
        if (MessageBox.Show(
                $"미리보기 {_previewRows.Count:N0}건을 ips_ew.fx_value_date에 저장할까요?\n같은 기준일·테너 자료는 새 값으로 갱신됩니다.",
                "DB 저장 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        try
        {
            UseWaitCursor = true;
            _selectButton.Enabled = _importButton.Enabled = false;
            _status.Text = "DB 저장 중...";
            FxValueDateImportResult result = await _repository.ImportAsync(_previewRows);
            _status.Text = $"저장 완료: 신규 {result.Inserted:N0}건 / 갱신 {result.Updated:N0}건";
            MessageBox.Show(_status.Text, "Settle Date 저장 결과", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _status.Text = "DB 저장 실패";
            MessageBox.Show(ex.Message, "Settle Date 저장 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
            _selectButton.Enabled = true;
            _importButton.Enabled = _previewRows.Count > 0;
        }
    }

    private static List<FxValueDateRow> ReadExcel(string path)
    {
        using var workbook = new XLWorkbook(path);
        IXLWorksheet sheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidOperationException("엑셀 시트를 찾을 수 없습니다.");
        var byKey = new Dictionary<(DateTime TradeDate, string Tenor), FxValueDateRow>();
        var errors = new List<string>();

        foreach (IXLRow row in sheet.RowsUsed())
        {
            string tradeDateText = row.Cell(1).GetFormattedString().Trim();
            string rawText = row.Cell(2).GetString().Trim();
            if (string.IsNullOrWhiteSpace(tradeDateText) && string.IsNullOrWhiteSpace(rawText)) continue;

            string tenor = rawText.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToUpperInvariant() ?? string.Empty;
            if (tenor is not ("S/N" or "1M")) continue;

            try
            {
                DateTime tradeDate = ParseTradeDate(row.Cell(1), tradeDateText);
                Match match = ValuePattern.Match(rawText);
                if (!match.Success) throw new FormatException("B열 형식을 인식할 수 없습니다.");
                DateTime startDate = ResolveMonthDay(tradeDate, match.Groups["start"].Value, null);
                DateTime endDate = ResolveMonthDay(tradeDate, match.Groups["end"].Value, startDate);
                int days = int.Parse(match.Groups["days"].Value, CultureInfo.InvariantCulture);
                int adjustment = match.Groups["adjust"].Success
                    ? int.Parse(match.Groups["adjust"].Value, CultureInfo.InvariantCulture)
                    : 0;
                byKey[(tradeDate.Date, tenor)] = new FxValueDateRow(
                    tradeDate.Date, tenor, startDate.Date, endDate.Date, days, adjustment, rawText);
            }
            catch (Exception ex)
            {
                errors.Add($"{row.RowNumber()}행: {ex.Message}");
            }
        }

        if (errors.Count > 0)
            throw new InvalidOperationException("엑셀 자료를 확인해주세요.\n" + string.Join("\n", errors.Take(10)) + (errors.Count > 10 ? $"\n외 {errors.Count - 10}건" : string.Empty));
        if (byKey.Count == 0)
            throw new InvalidOperationException("S/N 또는 1M 자료를 찾을 수 없습니다.");

        return byKey.Values.OrderBy(x => x.TradeDate).ThenBy(x => x.Tenor == "S/N" ? 0 : 1).ToList();
    }

    private static List<FxValueDateRow> ReadCsv(string path)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Encoding encoding = DetectCsvEncoding(path);
        using var parser = new TextFieldParser(path, encoding, true)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = true
        };
        parser.SetDelimiters(",");

        string[] headers = parser.ReadFields()
            ?? throw new InvalidOperationException("CSV 헤더가 없습니다.");
        int tradeDateIndex = Array.FindIndex(headers,
            x => x.Trim().TrimStart('\uFEFF').Equals("TRADE_DT", StringComparison.OrdinalIgnoreCase));
        int datesIndex = Array.FindIndex(headers,
            x => x.Trim().Equals("DATES", StringComparison.OrdinalIgnoreCase));
        if (tradeDateIndex < 0 || datesIndex < 0)
            throw new InvalidOperationException("CSV에 TRADE_DT와 DATES 컬럼이 필요합니다.");

        var byKey = new Dictionary<(DateTime TradeDate, string Tenor), FxValueDateRow>();
        var errors = new List<string>();
        int rowNumber = 1;

        while (!parser.EndOfData)
        {
            rowNumber++;
            string[] fields = parser.ReadFields() ?? [];
            if (fields.Length <= Math.Max(tradeDateIndex, datesIndex))
            {
                errors.Add($"{rowNumber}행: CSV 컬럼 수가 부족합니다.");
                continue;
            }

            string tradeDateText = fields[tradeDateIndex].Trim();
            string rawText = fields[datesIndex].Trim();
            string tenor = rawText.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()?.ToUpperInvariant() ?? string.Empty;
            if (tenor is not ("S/N" or "1M")) continue;

            try
            {
                if (!DateTime.TryParseExact(tradeDateText, "yyyyMMdd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out DateTime tradeDate))
                    throw new FormatException($"기준일자 형식이 올바르지 않습니다: {tradeDateText}");

                Match match = ValuePattern.Match(rawText);
                if (!match.Success) throw new FormatException("DATES 형식을 인식할 수 없습니다.");
                DateTime startDate = ResolveMonthDay(tradeDate, match.Groups["start"].Value, null);
                DateTime endDate = ResolveMonthDay(tradeDate, match.Groups["end"].Value, startDate);
                int days = int.Parse(match.Groups["days"].Value, CultureInfo.InvariantCulture);
                int adjustment = match.Groups["adjust"].Success
                    ? int.Parse(match.Groups["adjust"].Value, CultureInfo.InvariantCulture)
                    : 0;
                byKey[(tradeDate.Date, tenor)] = new FxValueDateRow(
                    tradeDate.Date, tenor, startDate.Date, endDate.Date, days, adjustment, rawText);
            }
            catch (Exception ex)
            {
                errors.Add($"{rowNumber}행: {ex.Message}");
            }
        }

        if (errors.Count > 0)
            throw new InvalidOperationException("CSV 자료를 확인해주세요.\n" +
                string.Join("\n", errors.Take(10)) +
                (errors.Count > 10 ? $"\n외 {errors.Count - 10}건" : string.Empty));
        if (byKey.Count == 0)
            throw new InvalidOperationException("CSV에서 S/N 또는 1M 자료를 찾을 수 없습니다.");

        return byKey.Values.OrderBy(x => x.TradeDate)
            .ThenBy(x => x.Tenor == "S/N" ? 0 : 1).ToList();
    }

    private static Encoding DetectCsvEncoding(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        try
        {
            var strictUtf8 = new UTF8Encoding(false, true);
            _ = strictUtf8.GetString(bytes);
            return new UTF8Encoding(false);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(949);
        }
    }

    private static DateTime ParseTradeDate(IXLCell cell, string text)
    {
        if (cell.TryGetValue(out DateTime date)) return date.Date;
        string digits = new(text.Where(char.IsDigit).ToArray());
        if (DateTime.TryParseExact(digits, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return date.Date;
        throw new FormatException($"기준일자 형식이 올바르지 않습니다: {text}");
    }

    private static DateTime ResolveMonthDay(DateTime tradeDate, string monthDay, DateTime? notBefore)
    {
        string[] parts = monthDay.Split('/');
        int month = int.Parse(parts[0], CultureInfo.InvariantCulture);
        int day = int.Parse(parts[1], CultureInfo.InvariantCulture);
        int year = tradeDate.Year;
        var value = new DateTime(year, month, day);
        DateTime lowerBound = notBefore ?? tradeDate.AddDays(-7);
        while (value < lowerBound) value = value.AddYears(1);
        return value;
    }

    private static DataTable ToDisplayRows(IEnumerable<FxValueDateRow> rows)
    {
        var table = new DataTable();
        table.Columns.Add("기준일", typeof(DateTime));
        table.Columns.Add("테너", typeof(string));
        table.Columns.Add("시작일", typeof(DateTime));
        table.Columns.Add("종료일", typeof(DateTime));
        table.Columns.Add("일수", typeof(int));
        table.Columns.Add("조정일수", typeof(int));
        table.Columns.Add("원본문자열", typeof(string));

        foreach (FxValueDateRow row in rows)
        {
            table.Rows.Add(
                row.TradeDate,
                row.Tenor,
                row.StartDate,
                row.EndDate,
                row.DayCount,
                row.DayAdjustment,
                row.RawText);
        }

        return table;
    }

    private static DataGridView CreateGrid() => new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoGenerateColumns = true,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        RowHeadersVisible = false
    };

    private static void FormatGrid(DataGridView grid)
    {
        foreach (string name in new[] { "기준일", "시작일", "종료일", "Spot Date", "1M Settle Date",
                     "Start Date", "Settle Date", "Fixing Date" })
            if (grid.Columns[name] is DataGridViewColumn column)
                column.DefaultCellStyle.Format = "yyyy-MM-dd";
        if (grid.Columns["원본문자열"] is DataGridViewColumn raw) raw.FillWeight = 180;
        if (grid.Columns["테너"] is DataGridViewColumn tenor) tenor.FillWeight = 55;
        if (grid.Columns["일수"] is DataGridViewColumn days) days.FillWeight = 55;
        if (grid.Columns["조정일수"] is DataGridViewColumn adjustment) adjustment.FillWeight = 65;
        if (grid.Columns["Tenor"] is DataGridViewColumn tenorEn) tenorEn.FillWeight = 55;
        if (grid.Columns["Days"] is DataGridViewColumn daysEn) daysEn.FillWeight = 55;
        if (grid.Columns["Adjustment"] is DataGridViewColumn adjustmentEn) adjustmentEn.FillWeight = 65;
    }
}
