using CodeMeter.Core;
using CodeMeter.Settings;
using FluentAssertions;
using Xunit;

namespace CodeMeter.Tests.Core;

public class UsageAggregatorTests
{
    private static UsageEntry E(DateTime ts, decimal tokens) => new(ts, tokens);
    private static AppSettings DefaultSettings() => new();

    // ── ComputeFiveHour ────────────────────────────────────────────────────

    [Fact]
    public void ComputeFiveHour_IncludesEntriesWithinWindow()
    {
        var now     = new DateTime(2026, 5, 25, 14, 0, 0, DateTimeKind.Utc);
        var entries = new[] { E(now.AddHours(-2), 1.00m) };

        var summary = new UsageAggregator().ComputeFiveHour(entries, DefaultSettings(), now);

        summary.AmountUsed.Should().Be(1.00m);
    }

    [Fact]
    public void ComputeFiveHour_ExcludesEntriesOlderThanFiveHours()
    {
        var now     = new DateTime(2026, 5, 25, 14, 0, 0, DateTimeKind.Utc);
        var entries = new[] { E(now.AddHours(-5).AddSeconds(-1), 1.00m) };

        var summary = new UsageAggregator().ComputeFiveHour(entries, DefaultSettings(), now);

        summary.AmountUsed.Should().Be(0m);
    }

    [Fact]
    public void ComputeFiveHour_IncludesEntryExactlyAtWindowStart()
    {
        var now     = new DateTime(2026, 5, 25, 14, 0, 0, DateTimeKind.Utc);
        var entries = new[] { E(now.AddHours(-5), 1.00m) };

        var summary = new UsageAggregator().ComputeFiveHour(entries, DefaultSettings(), now);

        summary.AmountUsed.Should().Be(1.00m);
    }

    [Fact]
    public void ComputeFiveHour_PercentUsed_CapsAt100WhenOverLimit()
    {
        var now      = new DateTime(2026, 5, 25, 14, 0, 0, DateTimeKind.Utc);
        var settings = DefaultSettings();
        // way over budget
        var entries  = new[] { E(now.AddHours(-1), settings.FiveHourTokenBudget * 2) };

        var summary = new UsageAggregator().ComputeFiveHour(entries, settings, now);

        summary.PercentUsed.Should().Be(100.0);
    }

    [Fact]
    public void ComputeFiveHour_ResetsAt_IsSessionStartPlusFiveHours_NoGap()
    {
        // Two entries 30 seconds apart — no gap ≥ 30 min → session start = oldest.
        var now    = new DateTime(2026, 5, 25, 14, 0, 0, DateTimeKind.Utc);
        var oldest = now.AddHours(-3);
        var entries = new[]
        {
            E(oldest,                    0.50m),
            E(oldest.AddSeconds(30), 0.50m),
        };

        var summary = new UsageAggregator().ComputeFiveHour(entries, DefaultSettings(), now);

        summary.ResetsAt.Should().Be(oldest.AddHours(5));
    }

    [Fact]
    public void ComputeFiveHour_ResetsAt_IsPostGapSessionStartPlusFiveHours()
    {
        // Gap of 2 hours between entries → session restarts at the later entry.
        var now      = new DateTime(2026, 5, 25, 14, 0, 0, DateTimeKind.Utc);
        var before   = now.AddHours(-4);
        var after    = now.AddHours(-1); // 3-hour gap from 'before'
        var entries  = new[] { E(before, 0.50m), E(after, 0.50m) };

        var summary = new UsageAggregator().ComputeFiveHour(entries, DefaultSettings(), now);

        // Session start = 'after' (first entry after the gap)
        summary.ResetsAt.Should().Be(after.AddHours(5));
    }

    [Fact]
    public void ComputeFiveHour_ResetsAt_IsMinValue_WhenWindowEmpty()
    {
        var now = new DateTime(2026, 5, 25, 14, 0, 0, DateTimeKind.Utc);

        var summary = new UsageAggregator().ComputeFiveHour([], DefaultSettings(), now);

        summary.ResetsAt.Should().Be(DateTime.MinValue);
        summary.AmountUsed.Should().Be(0m);
    }

    [Fact]
    public void ComputeFiveHour_PercentUsed_Correct()
    {
        // Exactly half the default budget → 50%
        var now      = new DateTime(2026, 5, 25, 14, 0, 0, DateTimeKind.Utc);
        var settings = DefaultSettings();
        var entries  = new[] { E(now.AddHours(-1), settings.FiveHourTokenBudget / 2m) };

        var summary = new UsageAggregator().ComputeFiveHour(entries, settings, now);

        summary.PercentUsed.Should().BeApproximately(50.0, 0.01);
    }
}
