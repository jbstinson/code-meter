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
    public void Update_RaisesPropertyChanged_ForAllFourProperties()
    {
        var vm     = new TrayViewModel();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        var fiveHour = new WindowSummary(3m, 22m, 75.0, DateTime.UtcNow.AddHours(2));
        vm.Update(fiveHour);

        raised.Should().Contain(nameof(TrayViewModel.FiveHourPercent));
        raised.Should().Contain(nameof(TrayViewModel.FiveHourResetText));
        raised.Should().Contain(nameof(TrayViewModel.TooltipText));
        raised.Should().Contain(nameof(TrayViewModel.IconColor));
    }

    [Fact]
    public void Update_SetsTooltipText_WithFiveHourPercentage()
    {
        var vm       = new TrayViewModel();
        var fiveHour = new WindowSummary(1m, 22m, 20.0, DateTime.UtcNow.AddHours(3));
        vm.Update(fiveHour);

        vm.TooltipText.Should().Be("5h: 20%");
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
    public void Update_IconColor_ReflectsFiveHourPercentage()
    {
        var vm       = new TrayViewModel();
        var fiveHour = new WindowSummary(20m, 22m, 95.0, DateTime.UtcNow.AddHours(1));
        vm.Update(fiveHour);

        // Red because 5-hour is 95%
        vm.IconColor.R.Should().Be(243);
        vm.IconColor.G.Should().Be(139);
        vm.IconColor.B.Should().Be(168);
    }

    [Fact]
    public void FormatFiveHourReset_ReturnsNoActivity_WhenMinValue()
    {
        var text = TrayViewModel.FormatFiveHourReset(DateTime.MinValue);
        text.Should().Be("No activity in window");
    }

    [Fact]
    public void FormatFiveHourReset_ReturnsCountdown_WhenActiveUsage()
    {
        var resetsAt = DateTime.UtcNow.AddHours(3).AddMinutes(30);
        var text     = TrayViewModel.FormatFiveHourReset(resetsAt);
        text.Should().StartWith("Resets in 3h");
    }
}
