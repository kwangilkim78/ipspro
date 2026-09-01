namespace IPS_PROBACKOFFICE;

public sealed class BusinessDaySettings
{
    public static readonly TimeSpan StartTime = TimeSpan.FromHours(8);
    private TimeSpan _endTime = TimeSpan.FromHours(6);

    public TimeSpan EndTime
    {
        get => _endTime;
        set
        {
            if (_endTime == value) return;
            _endTime = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? Changed;

    public (DateTime Start, DateTime End) GetDailyRange(DateTime businessDate)
    {
        DateTime start = businessDate.Date.Add(StartTime);
        DateTime end = businessDate.Date.AddDays(1).Add(EndTime);
        return (start, end);
    }

    public (DateTime Start, DateTime End) GetMonthToDateRange(DateTime businessDate)
    {
        DateTime start = new DateTime(businessDate.Year, businessDate.Month, 1).Add(StartTime);
        DateTime end = businessDate.Date.AddDays(1).Add(EndTime);
        return (start, end);
    }

    public (DateTime Start, DateTime End) GetInvoiceMonthRange(DateTime month)
    {
        DateTime monthStart = new DateTime(month.Year, month.Month, 1);
        DateTime start = monthStart.Add(StartTime);
        DateTime end = monthStart.AddMonths(1).Add(EndTime);
        return (start, end);
    }
}
