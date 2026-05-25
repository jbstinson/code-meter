namespace CodeMeter.Core;

public record WindowSummary(
    decimal AmountUsed,
    decimal Limit,
    double PercentUsed,
    DateTime ResetsAt
);
