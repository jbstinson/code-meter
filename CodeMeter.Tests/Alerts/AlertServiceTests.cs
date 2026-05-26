using CodeMeter.Alerts;
using CodeMeter.Core;
using CodeMeter.Settings;
using FluentAssertions;
using Xunit;

namespace CodeMeter.Tests.Alerts;

public class AlertServiceTests
{
    private static WindowSummary FiveHour(double pct)
        => new(0m, 22m, pct, DateTime.MinValue);

    private static AppSettings Thresholds(params int[] t)
        => new() { AlertThresholds = t.ToList() };

    [Fact]
    public void Check_FiresToast_WhenFiveHourThresholdFirstCrossed()
    {
        var fired = new List<string>();
        var svc   = new AlertService((title, _) => fired.Add(title));
        svc.Check(FiveHour(81), Thresholds(80));
        fired.Should().ContainSingle(t => t.Contains("5h") && t.Contains("80%"));
    }

    [Fact]
    public void Check_DoesNotFireTwice_ForSameThresholdWithinFiveHours()
    {
        var fired = new List<string>();
        var svc   = new AlertService((title, _) => fired.Add(title));
        svc.Check(FiveHour(85), Thresholds(80));
        svc.Check(FiveHour(85), Thresholds(80));
        fired.Should().HaveCount(1);
    }

    [Fact]
    public void Check_FiresAgain_AfterFiveHourWindowExpires()
    {
        var now   = DateTime.UtcNow;
        var fired = new List<string>();
        var svc   = new AlertService((title, _) => fired.Add(title), () => now);

        svc.Check(FiveHour(85), Thresholds(80));

        now = now.AddHours(5).AddMinutes(1); // advance clock past 5-hour expiry
        svc.Check(FiveHour(85), Thresholds(80));

        fired.Should().HaveCount(2);
    }

    [Fact]
    public void Check_DoesNotFire_WhenBelowThreshold()
    {
        var fired = new List<string>();
        var svc   = new AlertService((title, _) => fired.Add(title));
        svc.Check(FiveHour(70), Thresholds(80));
        fired.Should().BeEmpty();
    }

    [Fact]
    public void Check_FiresToast_WhenPercentageExactlyEqualsThreshold()
    {
        var fired = new List<string>();
        var svc   = new AlertService((title, _) => fired.Add(title));
        svc.Check(FiveHour(80), Thresholds(80));
        fired.Should().ContainSingle(t => t.Contains("5h") && t.Contains("80%"));
    }

    [Fact]
    public void Check_FiresMultipleThresholds_WhenAllCrossed()
    {
        var fired = new List<string>();
        var svc   = new AlertService((title, _) => fired.Add(title));
        svc.Check(FiveHour(96), Thresholds(80, 90, 95));
        fired.Should().HaveCount(3);
        fired.Should().Contain(t => t.Contains("80%"));
        fired.Should().Contain(t => t.Contains("90%"));
        fired.Should().Contain(t => t.Contains("95%"));
    }
}
