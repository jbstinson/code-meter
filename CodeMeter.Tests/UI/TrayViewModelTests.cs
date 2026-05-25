using CodeMeter.Core;
using CodeMeter.UI;
using FluentAssertions;
using System.Collections.Generic;
using System.Drawing;
using Xunit;

namespace CodeMeter.Tests.UI;

public class TrayViewModelTests
{
    [Theory]
    [InlineData(0,   166, 227, 161)]  // green
    [InlineData(60,  166, 227, 161)]  // green upper edge
    [InlineData(61,  249, 226, 175)]  // yellow lower edge
    [InlineData(89,  249, 226, 175)]  // yellow upper edge
    [InlineData(90,  243, 139, 168)]  // red lower edge
    [InlineData(99,  243, 139, 168)]  // red upper edge
    [InlineData(100, 139, 0,   0  )]  // dark red
    public void ResolveIconColor_ReturnsCorrectColor(double percent, int r, int g, int b)
    {
        var color = TrayViewModel.ResolveIconColor(percent);
        color.R.Should().Be((byte)r);
        color.G.Should().Be((byte)g);
        color.B.Should().Be((byte)b);
    }

    [Fact]
    public void Update_RaisesPropertyChanged_ForAllSixProperties()
    {
        var vm = new TrayViewModel();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        // Use 75% daily so IconColor changes from the green default
        var daily = new WindowSummary(3m, 4m, 75.0, DateTime.Now.AddDays(1));
        var weekly = new WindowSummary(5m, 35m, 14.0, DateTime.Now.AddDays(7));
        vm.Update(daily, weekly);

        raised.Should().Contain(nameof(TrayViewModel.DailyPercent));
        raised.Should().Contain(nameof(TrayViewModel.WeeklyPercent));
        raised.Should().Contain(nameof(TrayViewModel.DailyResetText));
        raised.Should().Contain(nameof(TrayViewModel.WeeklyResetText));
        raised.Should().Contain(nameof(TrayViewModel.TooltipText));
        raised.Should().Contain(nameof(TrayViewModel.IconColor));
    }

    [Fact]
    public void Update_SetsTooltipText_WithBothPercentages()
    {
        var vm = new TrayViewModel();
        var daily = new WindowSummary(1m, 5m, 20.0, DateTime.Now.AddDays(1));
        var weekly = new WindowSummary(5m, 35m, 14.0, DateTime.Now.AddDays(7));
        vm.Update(daily, weekly);

        vm.TooltipText.Should().Be("Daily: 20% · Weekly: 14%");
    }

    [Fact]
    public void ResolveIconColor_Above100_ReturnsDarkRed()
    {
        var color = TrayViewModel.ResolveIconColor(150.0);
        color.R.Should().Be(139);
        color.G.Should().Be(0);
        color.B.Should().Be(0);
    }

    [Fact]
    public void Update_IconColor_ReflectsWorstPercentage()
    {
        var vm = new TrayViewModel();
        // Daily = 20% (green), Weekly = 95% (red)
        var daily = new WindowSummary(1m, 5m, 20.0, DateTime.Now.AddDays(1));
        var weekly = new WindowSummary(33m, 35m, 95.0, DateTime.Now.AddDays(7));
        vm.Update(daily, weekly);

        // Should be red (243, 139, 168) because weekly is 95%
        vm.IconColor.R.Should().Be(243);
        vm.IconColor.G.Should().Be(139);
        vm.IconColor.B.Should().Be(168);
    }
}
