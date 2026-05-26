using CodeMeter.Settings;

namespace CodeMeter.Core;

public class UsageAggregator
{
    // A gap of ≥30 minutes between consecutive entries signals a new usage "session".
    // Claude Code resets its 5-hour timer at the first entry after such a gap.
    private static readonly TimeSpan SessionGapThreshold = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Sums usage within the rolling 5-hour window ending at <paramref name="now"/> (UTC).
    /// <para><see cref="WindowSummary.ResetsAt"/> is 5 hours after the start of the
    /// <em>current session burst</em> — defined as the first entry after the most recent
    /// gap of ≥ 30 minutes within the window.  This matches Claude Code's "resets Xh"
    /// display behaviour, which starts a fresh timer after inactivity.
    /// Returns <see cref="DateTime.MinValue"/> when the window is empty.</para>
    /// </summary>
    public WindowSummary ComputeFiveHour(
        IEnumerable<UsageEntry> entries, AppSettings settings, DateTime now)
    {
        var windowStart = now.AddHours(-5);
        var inWindow    = entries
            .Where(e => e.Timestamp >= windowStart && e.Timestamp <= now)
            .OrderBy(e => e.Timestamp)
            .ToList();

        var used   = inWindow.Sum(e => e.WeightedTokens);
        var budget = settings.FiveHourTokenBudget;
        var pct    = Math.Min(100.0, (double)used / (double)budget * 100.0);

        var sessionStart = FindSessionStart(inWindow);
        var resetsAt     = sessionStart.HasValue
            ? sessionStart.Value.AddHours(5)
            : DateTime.MinValue;

        return new WindowSummary(used, budget, pct, resetsAt);
    }

    /// <summary>
    /// Returns the timestamp of the first entry in the most recent continuous burst.
    /// Walks backwards through <paramref name="orderedEntries"/> to find the latest gap
    /// ≥ <see cref="SessionGapThreshold"/>; the entry immediately after that gap is the
    /// session start.  Falls back to the oldest entry if no qualifying gap exists.
    /// </summary>
    private static DateTime? FindSessionStart(List<UsageEntry> orderedEntries)
    {
        if (orderedEntries.Count == 0) return null;

        for (int i = orderedEntries.Count - 1; i > 0; i--)
        {
            var gap = orderedEntries[i].Timestamp - orderedEntries[i - 1].Timestamp;
            if (gap >= SessionGapThreshold)
                return orderedEntries[i].Timestamp;
        }

        return orderedEntries[0].Timestamp;
    }
}
