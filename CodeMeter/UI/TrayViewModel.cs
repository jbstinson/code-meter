using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using CodeMeter.Core;

namespace CodeMeter.UI;

public class TrayViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private double _dailyPercent;
    private double _weeklyPercent;
    private string _dailyResetText = "";
    private string _weeklyResetText = "";
    private string _tooltipText = "Claude Code Usage";
    private Color _iconColor = Color.FromArgb(166, 227, 161); // GDI+ color for tray icon bitmap

    public double DailyPercent
    {
        get => _dailyPercent;
        private set
        {
            if (_dailyPercent == value) return;
            _dailyPercent = value;
            Notify();
        }
    }

    public double WeeklyPercent
    {
        get => _weeklyPercent;
        private set
        {
            if (_weeklyPercent == value) return;
            _weeklyPercent = value;
            Notify();
        }
    }

    public string DailyResetText
    {
        get => _dailyResetText;
        private set
        {
            if (_dailyResetText == value) return;
            _dailyResetText = value;
            Notify();
        }
    }

    public string WeeklyResetText
    {
        get => _weeklyResetText;
        private set
        {
            if (_weeklyResetText == value) return;
            _weeklyResetText = value;
            Notify();
        }
    }

    public string TooltipText
    {
        get => _tooltipText;
        private set
        {
            if (_tooltipText == value) return;
            _tooltipText = value;
            Notify();
        }
    }

    public Color IconColor // System.Drawing.Color — intentional, used for GDI+ icon rendering
    {
        get => _iconColor;
        private set
        {
            if (_iconColor == value) return;
            _iconColor = value;
            Notify();
        }
    }

    public void Update(WindowSummary daily, WindowSummary weekly)
    {
        DailyPercent    = daily.PercentUsed;
        WeeklyPercent   = weekly.PercentUsed;
        DailyResetText  = FormatReset(daily.ResetsAt);
        WeeklyResetText = FormatReset(weekly.ResetsAt);
        TooltipText     = $"Daily: {daily.PercentUsed:F0}% · Weekly: {weekly.PercentUsed:F0}%";
        IconColor       = ResolveIconColor(Math.Max(daily.PercentUsed, weekly.PercentUsed));
    }

    internal static Color ResolveIconColor(double worst) => worst switch
    {
        >= 100 => Color.FromArgb(139, 0, 0),
        >= 90  => Color.FromArgb(243, 139, 168),
        >= 61  => Color.FromArgb(249, 226, 175),
        _      => Color.FromArgb(166, 227, 161)
    };

    private static string FormatReset(DateTime resetsAt)
    {
        var local = resetsAt.Kind == DateTimeKind.Utc ? resetsAt.ToLocalTime() : resetsAt;
        return local.Date == DateTime.Today
            ? $"Resets at {local:h:mm tt}"
            : $"Resets {local:dddd}";
    }

    private void Notify([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
