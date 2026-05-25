using CodeMeter.Core;
using CodeMeter.Settings;
using FluentAssertions;
using Xunit;

namespace CodeMeter.Tests.Core;

public class UsageAggregatorTests
{
    private static AppSettings DefaultSettings() => new()
    {
        DailyLimitUSD = 5.00m,
        WeeklyLimitUSD = 35.00m,
        DailyResetHour = 0,
        WeeklyResetDay = DayOfWeek.Monday,
        WeeklyResetHour = 0
    };

    private static UsageEntry E(DateTime ts, decimal cost) => new(ts, cost);

    [Fact]
    public void ComputeDaily_IncludesEntriesAfterTodaysResetHour()
    {
        var now = new DateTime(2026, 5, 25, 14, 0, 0);
        var entries = new[] { E(new DateTime(2026, 5, 25, 8, 0, 0), 1.00m) };

        var summary = new UsageAggregator().ComputeDaily(entries, DefaultSettings(), now);

        summary.AmountUsed.Should().Be(1.00m);
        summary.PercentUsed.Should().BeApproximately(20.0, 0.01);
    }

    [Fact]
    public void ComputeDaily_ExcludesEntriesBeforeWindowStart()
    {
        var now = new DateTime(2026, 5, 25, 14, 0, 0);
        var entries = new[] { E(new DateTime(2026, 5, 24, 8, 0, 0), 1.00m) };

        var summary = new UsageAggregator().ComputeDaily(entries, DefaultSettings(), now);

        summary.AmountUsed.Should().Be(0m);
    }

    [Fact]
    public void ComputeDaily_UsesYesterdayReset_WhenBeforeResetHour()
    {
        var settings = DefaultSettings();
        settings.DailyResetHour = 8;
        var now = new DateTime(2026, 5, 25, 6, 0, 0);
        var entries = new[] { E(new DateTime(2026, 5, 24, 9, 0, 0), 2.00m) };

        var summary = new UsageAggregator().ComputeDaily(entries, settings, now);

        summary.AmountUsed.Should().Be(2.00m);
    }

    [Fact]
    public void ComputeDaily_ResetsAt_IsOneDayAfterWindowStart()
    {
        var now = new DateTime(2026, 5, 25, 14, 0, 0);

        var summary = new UsageAggregator().ComputeDaily([], DefaultSettings(), now);

        summary.ResetsAt.Should().Be(new DateTime(2026, 5, 26, 0, 0, 0));
    }

    [Fact]
    public void ComputeWeekly_IncludesEntriesAfterWeeklyReset()
    {
        var now = new DateTime(2026, 5, 27, 14, 0, 0); // Wednesday
        var entries = new[] { E(new DateTime(2026, 5, 26, 8, 0, 0), 5.00m) }; // Tuesday

        var summary = new UsageAggregator().ComputeWeekly(entries, DefaultSettings(), now);

        summary.AmountUsed.Should().Be(5.00m);
    }

    [Fact]
    public void ComputeWeekly_ExcludesEntriesFromPreviousWeek()
    {
        var now = new DateTime(2026, 5, 27, 14, 0, 0);
        var entries = new[] { E(new DateTime(2026, 5, 18, 8, 0, 0), 5.00m) };

        var summary = new UsageAggregator().ComputeWeekly(entries, DefaultSettings(), now);

        summary.AmountUsed.Should().Be(0m);
    }

    [Fact]
    public void ComputeWeekly_ResetsAt_IsSevenDaysAfterWindowStart()
    {
        var now = new DateTime(2026, 5, 27, 14, 0, 0); // Wednesday

        var summary = new UsageAggregator().ComputeWeekly([], DefaultSettings(), now);

        // Window started Monday 2026-05-25 00:00, resets Monday 2026-06-01 00:00
        summary.ResetsAt.Should().Be(new DateTime(2026, 6, 1, 0, 0, 0));
    }
}
