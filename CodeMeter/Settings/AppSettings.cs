namespace CodeMeter.Settings;

public class AppSettings
{
    public List<int> AlertThresholds { get; set; } = [60, 80, 95];
    public int PollIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// 5-hour rolling window capacity expressed as output-token-equivalents
    /// (Sonnet output token = 1.0 unit).  Default 1 466 667 corresponds to the
    /// empirically-observed Claude Code Pro/Max session limit.
    /// </summary>
    public decimal FiveHourTokenBudget { get; set; } = 1_466_667m;
}
