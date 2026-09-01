using System.Globalization;
using IPS_PROBACKOFFICE.Data;

namespace IPS_PROBACKOFFICE;

/// <summary>날짜 제한 없이 한국/미국 휴일을 함께 보여주는 달력 패널.</summary>
public sealed class HolidayCalendarPanel : Panel
{
    private readonly MonthCalendar _calendar = new();
    private readonly ListView _holidays = new();
    private readonly Label _periodLabel = new();
    private readonly FxValueDateRepository _repository;
    private int _loadedYear;
    private int _loadedMonth;

    public event EventHandler? HolidaysChanged;

    public HolidayCalendarPanel(FxValueDateRepository repository)
    {
        _repository = repository;
        Dock = DockStyle.Fill;
        Padding = new Padding(10);
        BackColor = Color.White;

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 30,
            Text = "한국 · 미국 Banking 휴일 달력",
            Font = new Font("맑은 고딕", 11F, FontStyle.Bold)
        };
        _periodLabel.Dock = DockStyle.Top;
        _periodLabel.Height = 28;
        _periodLabel.ForeColor = Color.DimGray;

        _calendar.Dock = DockStyle.Top;
        _calendar.Height = 185;
        _calendar.MaxSelectionCount = 1;
        _calendar.SetDate(DateTime.Today);

        var legend = new Label
        {
            Dock = DockStyle.Top,
            Height = 30,
            Padding = new Padding(0, 7, 0, 0),
            Text = "● 한국   ● 미국   ● 양국 공통",
            ForeColor = Color.FromArgb(55, 82, 125)
        };

        _holidays.Dock = DockStyle.Fill;
        _holidays.View = View.Details;
        _holidays.FullRowSelect = true;
        _holidays.GridLines = true;
        _holidays.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _holidays.Columns.Add("날짜", 120);
        _holidays.Columns.Add("구분", 55);
        _holidays.Columns.Add("휴일명", 165);

        _calendar.DateChanged += async (_, e) => await LoadMonthAsync(e.Start.Year, e.Start.Month);
        HandleCreated += async (_, _) => await LoadMonthAsync(DateTime.Today.Year, DateTime.Today.Month);

        Controls.Add(_holidays);
        Controls.Add(legend);
        Controls.Add(_calendar);
        Controls.Add(_periodLabel);
        Controls.Add(title);
    }

    public async Task ShowManagementAsync(IWin32Window? owner = null)
    {
        using var dialog = new HolidayManagementDialog(_repository, _calendar.SelectionStart);
        DialogResult result = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        if (result != DialogResult.OK) return;
        _loadedYear = _loadedMonth = 0;
        await LoadMonthAsync(_calendar.SelectionStart.Year, _calendar.SelectionStart.Month);
        HolidaysChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task LoadMonthAsync(int year, int month)
    {
        if (_loadedYear == year && _loadedMonth == month) return;
        _loadedYear = year;
        _loadedMonth = month;
        _periodLabel.Text = $"{year}년 {month}월 휴일 · 이전/다음 달 자유 이동";
        DateTime firstDay = new(year, month, 1);
        DateTime lastDay = firstDay.AddMonths(1).AddDays(-1);
        IReadOnlyList<HolidayOverrideRow> overrides;
        try
        {
            overrides = await _repository.GetHolidayOverridesAsync(firstDay, lastDay);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "휴일 DB 조회 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            overrides = [];
        }
        Dictionary<DateTime, HolidayInfo> dates = HolidayProvider.Get(firstDay, lastDay, overrides);
        _calendar.BoldedDates = dates.Keys.ToArray();
        _holidays.BeginUpdate();
        _holidays.Items.Clear();
        foreach ((DateTime day, HolidayInfo info) in dates.OrderBy(x => x.Key))
        {
            var item = new ListViewItem(day.ToString("yyyy-MM-dd (ddd)", CultureInfo.GetCultureInfo("ko-KR")));
            item.SubItems.Add(info.Country);
            item.SubItems.Add(info.Name);
            item.ForeColor = info.Country == "한국" ? Color.Firebrick
                : info.Country == "미국" ? Color.RoyalBlue : Color.Purple;
            _holidays.Items.Add(item);
        }
        if (_holidays.Items.Count == 0)
        {
            var item = new ListViewItem("-");
            item.SubItems.Add("-");
            item.SubItems.Add($"{year}년 {month}월 휴일 없음");
            item.ForeColor = Color.DimGray;
            _holidays.Items.Add(item);
        }
        _holidays.EndUpdate();
    }
}

internal sealed record HolidayInfo(string Country, string Name);

internal static class HolidayProvider
{
    public static bool IsKoreanBusinessDay(DateTime day, IReadOnlyList<HolidayOverrideRow>? overrides = null) =>
        IsCountryBusinessDay(day, "KR", overrides);

    public static bool IsKoreaUsBusinessDay(DateTime day, IReadOnlyList<HolidayOverrideRow>? overrides = null) =>
        IsCountryBusinessDay(day, "KR", overrides) && IsCountryBusinessDay(day, "US", overrides);

    public static Dictionary<DateTime, HolidayInfo> Get(
        DateTime from,
        DateTime to,
        IReadOnlyList<HolidayOverrideRow>? overrides = null)
    {
        var result = new Dictionary<DateTime, HolidayInfo>();
        for (DateTime day = from.Date; day <= to.Date; day = day.AddDays(1))
        {
            string? kr = GetHolidayName(day, "KR", overrides);
            string? us = GetHolidayName(day, "US", overrides);
            if (kr is not null && us is not null)
                result[day] = new HolidayInfo("공통", $"{kr} / {us}");
            else if (kr is not null)
                result[day] = new HolidayInfo("한국", kr);
            else if (us is not null)
                result[day] = new HolidayInfo("미국", us);
        }
        return result;
    }

    private static bool IsCountryBusinessDay(
        DateTime day,
        string countryCode,
        IReadOnlyList<HolidayOverrideRow>? overrides)
    {
        if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return false;
        return GetHolidayName(day, countryCode, overrides) is null;
    }

    private static string? GetHolidayName(
        DateTime day,
        string countryCode,
        IReadOnlyList<HolidayOverrideRow>? overrides)
    {
        HolidayOverrideRow? custom = overrides?.FirstOrDefault(x =>
            x.HolidayDate.Date == day.Date && x.CountryCode.Equals(countryCode, StringComparison.OrdinalIgnoreCase));
        if (custom is not null) return custom.IsHoliday ? custom.HolidayName : null;
        IEnumerable<(DateTime, string)> source = countryCode == "KR"
            ? KoreanHolidays(day.Year)
            : UnitedStatesHolidays(day.Year);
        return source.FirstOrDefault(x => x.Item1.Date == day.Date).Item2;
    }

    private static IEnumerable<(DateTime, string)> KoreanHolidays(int year)
    {
        yield return (new DateTime(year, 1, 1), "신정");
        yield return (new DateTime(year, 3, 1), "삼일절");
        yield return (new DateTime(year, 5, 5), "어린이날");
        yield return (new DateTime(year, 6, 6), "현충일");
        yield return (new DateTime(year, 8, 15), "광복절");
        yield return (new DateTime(year, 10, 3), "개천절");
        yield return (new DateTime(year, 10, 9), "한글날");
        yield return (new DateTime(year, 12, 25), "성탄절");

        // 음력 공휴일과 대체공휴일은 고시된 날짜를 연도별로 관리한다.
        if (year == 2026)
        {
            yield return (new DateTime(2026, 2, 16), "설날 연휴");
            yield return (new DateTime(2026, 2, 17), "설날");
            yield return (new DateTime(2026, 2, 18), "설날 연휴");
            yield return (new DateTime(2026, 3, 2), "삼일절 대체공휴일");
            yield return (new DateTime(2026, 5, 24), "부처님오신날");
            yield return (new DateTime(2026, 5, 25), "부처님오신날 대체공휴일");
            yield return (new DateTime(2026, 8, 17), "광복절 대체공휴일");
            yield return (new DateTime(2026, 9, 24), "추석 연휴");
            yield return (new DateTime(2026, 9, 25), "추석");
            yield return (new DateTime(2026, 9, 26), "추석 연휴");
            yield return (new DateTime(2026, 10, 5), "개천절 대체공휴일");
        }
    }

    private static IEnumerable<(DateTime, string)> UnitedStatesHolidays(int year)
    {
        yield return (BankingObserved(new DateTime(year, 1, 1)), "New Year's Day");
        yield return (NthWeekday(year, 1, DayOfWeek.Monday, 3), "Martin Luther King Jr. Day");
        yield return (NthWeekday(year, 2, DayOfWeek.Monday, 3), "Presidents' Day");
        yield return (LastWeekday(year, 5, DayOfWeek.Monday), "Memorial Day");
        yield return (BankingObserved(new DateTime(year, 6, 19)), "Juneteenth");
        yield return (BankingObserved(new DateTime(year, 7, 4)), "Independence Day");
        yield return (NthWeekday(year, 9, DayOfWeek.Monday, 1), "Labor Day");
        yield return (NthWeekday(year, 10, DayOfWeek.Monday, 2), "Columbus Day");
        yield return (BankingObserved(new DateTime(year, 11, 11)), "Veterans Day");
        yield return (NthWeekday(year, 11, DayOfWeek.Thursday, 4), "Thanksgiving Day");
        yield return (BankingObserved(new DateTime(year, 12, 25)), "Christmas Day");
    }

    // Federal Reserve/Fedwire 기준: 토요일 공휴일은 금요일로 당기지 않으며,
    // 일요일 공휴일만 다음 월요일에 휴무한다.
    private static DateTime BankingObserved(DateTime day) => day.DayOfWeek switch
    {
        DayOfWeek.Sunday => day.AddDays(1),
        _ => day
    };

    private static DateTime NthWeekday(int year, int month, DayOfWeek weekday, int nth)
    {
        DateTime first = new(year, month, 1);
        int offset = ((int)weekday - (int)first.DayOfWeek + 7) % 7;
        return first.AddDays(offset + (nth - 1) * 7);
    }

    private static DateTime LastWeekday(int year, int month, DayOfWeek weekday)
    {
        DateTime last = new DateTime(year, month, 1).AddMonths(1).AddDays(-1);
        int offset = ((int)last.DayOfWeek - (int)weekday + 7) % 7;
        return last.AddDays(-offset);
    }
}
