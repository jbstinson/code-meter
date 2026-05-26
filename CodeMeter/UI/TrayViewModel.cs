using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using CodeMeter.Core;

namespace CodeMeter.UI;

public class TrayViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private double _fiveHourPercent;
    private string _fiveHourResetText = "";
    private string _tooltipText       = "Claude Code Usage";
    private Color  _iconColor         = Color.FromArgb(166, 227, 161);

    public double FiveHourPercent
    {
        get => _fiveHourPercent;
        private set { if (_fiveHourPercent == value) return; _fiveHourPercent = value; Notify(); }
    }

    public string FiveHourResetText
    {
        get => _fiveHourResetText;
        private set { if (_fiveHourResetText == value) return; _fiveHourResetText = value; Notify(); }
    }

    public string TooltipText
    {
        get => _tooltipText;
        private set { if (_tooltipText == value) return; _tooltipText = value; Notify(); }
    }

    public Color IconColor
    {
        get => _iconColor;
        private set { if (_iconColor == value) return; _iconColor = value; Notify(); }
    }

    public void Update(WindowSummary fiveHour)
    {
        FiveHourPercent   = fiveHour.PercentUsed;
        FiveHourResetText = FormatFiveHourReset(fiveHour.ResetsAt);
        TooltipText       = $"5h: {fiveHour.PercentUsed:F0}%";
        IconColor         = ResolveIconColor(fiveHour.PercentUsed);
    }

    internal static Color ResolveIconColor(double worst) => worst switch
    {
        >= 100 => Color.FromArgb(139, 0, 0),
        >= 90  => Color.FromArgb(243, 139, 168),
        >= 61  => Color.FromArgb(249, 226, 175),
        _      => Color.FromArgb(166, 227, 161)
    };

    /// <summary>
    /// Returns a countdown string for the rolling 5-hour window, e.g. "Resets in 3h 42m".
    /// </summary>
    internal static string FormatFiveHourReset(DateTime resetsAt)
    {
        if (resetsAt == DateTime.MinValue)
            return "No activity in window";

        var remaining = resetsAt.ToUniversalTime() - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
            return "Window clearing";

        var h = (int)remaining.TotalHours;
        var m = remaining.Minutes;
        return h > 0 ? $"Resets in {h}h {m}m" : $"Resets in {m}m";
    }

    private void Notify([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
