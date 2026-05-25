using CodeMeter.Alerts;
using CodeMeter.Core;
using CodeMeter.Settings;
using FluentAssertions;
using Xunit;

namespace CodeMeter.Tests.Alerts;

public class AlertServiceTests
{
    private static WindowSummary S(double pct, DateTime resetsAt)
        => new(0m, 10m, pct, resetsAt);

    private static AppSettings Thresholds(params int[] t)
        => new() { AlertThresholds = t.ToList() };

    [Fact]
    public void Check_FiresToast_WhenDailyThresholdFirstCrossed()
    {
        var fired = new List<string>();
        var svc = new AlertService((title, _) => fired.Add(title));
        svc.Check(S(81, DateTime.Now.AddDays(1)), S(10, DateTime.Now.AddDays(7)), Thresholds(80));
        fired.Should().ContainSingle(t => t.Contains("Daily") && t.Contains("80%"));
    }

    [Fact]
    public void Check_DoesNotFireTwice_ForSameThresholdSamePeriod()
    {
        var fired = new List<string>();
        var svc = new AlertService((title, _) => fired.Add(title));
        var daily = S(85, DateTime.Now.AddDays(1));
        svc.Check(daily, S(10, DateTime.Now.AddDays(7)), Thresholds(80));
        svc.Check(daily, S(10, DateTime.Now.AddDays(7)), Thresholds(80));
        fired.Should().HaveCount(1);
    }

    [Fact]
    public void Check_FiresAgain_AfterWindowReset()
    {
        var fired = new List<string>();
        var svc = new AlertService((title, _) => fired.Add(title));
        svc.Check(S(85, DateTime.Now.AddDays(1)), S(10, DateTime.Now.AddDays(7)), Thresholds(80));
        // New window: resetsAt has advanced
        svc.Check(S(85, DateTime.Now.AddDays(2)), S(10, DateTime.Now.AddDays(7)), Thresholds(80));
        fired.Should().HaveCount(2);
    }

    [Fact]
    public void Check_DoesNotFire_WhenBelowThreshold()
    {
        var fired = new List<string>();
        var svc = new AlertService((title, _) => fired.Add(title));
        svc.Check(S(70, DateTime.Now.AddDays(1)), S(10, DateTime.Now.AddDays(7)), Thresholds(80));
        fired.Should().BeEmpty();
    }

    [Fact]
    public void Check_FiresForWeekly_Independently()
    {
        var fired = new List<string>();
        var svc = new AlertService((title, _) => fired.Add(title));
        svc.Check(S(10, DateTime.Now.AddDays(1)), S(85, DateTime.Now.AddDays(7)), Thresholds(80));
        fired.Should().ContainSingle(t => t.Contains("Weekly") && t.Contains("80%"));
    }
}
