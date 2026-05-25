namespace CodeMeter.Settings;

public class AppSettings
{
    public decimal DailyLimitUSD { get; set; } = 5.00m;
    public decimal WeeklyLimitUSD { get; set; } = 35.00m;
    public int DailyResetHour { get; set; } = 0;
    public DayOfWeek WeeklyResetDay { get; set; } = DayOfWeek.Monday;
    public int WeeklyResetHour { get; set; } = 0;
    public List<int> AlertThresholds { get; set; } = [60, 80, 95];
    public int PollIntervalSeconds { get; set; } = 30;
}
