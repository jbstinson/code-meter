using CodeMeter.UI;
using FluentAssertions;
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
}
