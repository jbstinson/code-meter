using CodeMeter.Settings;

namespace CodeMeter.Core;

public class UsageAggregator
{
    public WindowSummary ComputeDaily(IEnumerable<UsageEntry> entries, AppSettings settings, DateTime now)
    {
        var start = GetDailyWindowStart(settings, now);
        var end = start.AddDays(1);
        var used = entries.Where(e => e.Timestamp >= start && e.Timestamp < end).Sum(e => e.CostUSD);
        var pct = settings.DailyLimitUSD > 0
            ? Math.Min(100.0, (double)(used / settings.DailyLimitUSD) * 100.0)
            : 0.0;
        return new WindowSummary(used, settings.DailyLimitUSD, pct, end);
    }

    public WindowSummary ComputeWeekly(IEnumerable<UsageEntry> entries, AppSettings settings, DateTime now)
    {
        var start = GetWeeklyWindowStart(settings, now);
        var end = start.AddDays(7);
        var used = entries.Where(e => e.Timestamp >= start && e.Timestamp < end).Sum(e => e.CostUSD);
        var pct = settings.WeeklyLimitUSD > 0
            ? Math.Min(100.0, (double)(used / settings.WeeklyLimitUSD) * 100.0)
            : 0.0;
        return new WindowSummary(used, settings.WeeklyLimitUSD, pct, end);
    }

    internal static DateTime GetDailyWindowStart(AppSettings settings, DateTime now)
    {
        var todayReset = new DateTime(now.Year, now.Month, now.Day, settings.DailyResetHour, 0, 0);
        return now >= todayReset ? todayReset : todayReset.AddDays(-1);
    }

    internal static DateTime GetWeeklyWindowStart(AppSettings settings, DateTime now)
    {
        var daysBack = ((int)now.DayOfWeek - (int)settings.WeeklyResetDay + 7) % 7;
        var candidate = new DateTime(now.Year, now.Month, now.Day, settings.WeeklyResetHour, 0, 0)
                            .AddDays(-daysBack);
        if (candidate > now) candidate = candidate.AddDays(-7);
        return candidate;
    }
}
