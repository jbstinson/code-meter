using CodeMeter.Core;
using CodeMeter.Settings;

namespace CodeMeter.Alerts;

public class AlertService
{
    private readonly Action<string, string> _showToast;
    private readonly Func<DateTime> _clock;   // injectable for tests

    // 5-hour window: keyed by threshold, value = UTC time the alert fired.
    // Clears automatically after 5 hours so the alert can re-fire next window.
    private readonly Dictionary<int, DateTime> _firedFiveHour = [];

    public AlertService(Action<string, string> showToast, Func<DateTime>? clock = null)
    {
        ArgumentNullException.ThrowIfNull(showToast);
        _showToast = showToast;
        _clock = clock ?? (() => DateTime.UtcNow);
    }

    public void Check(WindowSummary fiveHour, AppSettings settings)
    {
        var now = _clock();

        // Expire 5-hour alerts that are older than the window length.
        foreach (var key in _firedFiveHour.Keys.ToList())
            if ((now - _firedFiveHour[key]).TotalHours >= 5)
                _firedFiveHour.Remove(key);

        foreach (var threshold in settings.AlertThresholds.Distinct().OrderBy(t => t))
        {
            if (fiveHour.PercentUsed >= threshold && !_firedFiveHour.ContainsKey(threshold))
            {
                _firedFiveHour[threshold] = now;
                var remaining = fiveHour.ResetsAt != DateTime.MinValue
                    ? fiveHour.ResetsAt.ToUniversalTime() - now
                    : TimeSpan.Zero;
                var body = remaining > TimeSpan.Zero
                    ? $"Resets in {(int)remaining.TotalHours}h {remaining.Minutes}m"
                    : "Window clearing now";
                _showToast($"Claude Code — 5h limit at {threshold}%", body);
            }
        }
    }
}
