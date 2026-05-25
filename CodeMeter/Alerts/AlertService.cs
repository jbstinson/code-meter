using CodeMeter.Core;
using CodeMeter.Settings;

namespace CodeMeter.Alerts;

public class AlertService(Action<string, string> showToast)
{
    private DateTime _lastDailyResetsAt;
    private DateTime _lastWeeklyResetsAt;
    private readonly HashSet<int> _firedDaily = [];
    private readonly HashSet<int> _firedWeekly = [];

    public void Check(WindowSummary daily, WindowSummary weekly, AppSettings settings)
    {
        if (daily.ResetsAt != _lastDailyResetsAt)
        {
            _firedDaily.Clear();
            _lastDailyResetsAt = daily.ResetsAt;
        }
        if (weekly.ResetsAt != _lastWeeklyResetsAt)
        {
            _firedWeekly.Clear();
            _lastWeeklyResetsAt = weekly.ResetsAt;
        }

        foreach (var threshold in settings.AlertThresholds.Distinct().OrderBy(t => t))
        {
            if (daily.PercentUsed >= threshold && _firedDaily.Add(threshold))
                showToast(
                    $"Claude Code — Daily limit at {threshold}%",
                    $"Resets at {daily.ResetsAt.ToLocalTime():h:mm tt}");

            if (weekly.PercentUsed >= threshold && _firedWeekly.Add(threshold))
                showToast(
                    $"Claude Code — Weekly limit at {threshold}%",
                    $"Resets {weekly.ResetsAt.ToLocalTime():dddd}");
        }
    }
}
