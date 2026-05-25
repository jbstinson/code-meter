using CodeMeter.Core;
using FluentAssertions;
using Xunit;

namespace CodeMeter.Tests.Core;

public class UsageReaderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public UsageReaderTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, true);

    private void WriteJsonl(string subPath, params string[] lines)
    {
        var path = Path.Combine(_dir, subPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllLines(path, lines);
    }

    [Fact]
    public void ReadAll_ParsesValidAssistantEntries()
    {
        WriteJsonl("proj1/conv1.jsonl",
            """{"type":"assistant","timestamp":"2026-05-25T10:00:00Z","costUSD":0.05}""");

        var entries = new UsageReader().ReadAll(_dir).ToList();

        entries.Should().HaveCount(1);
        entries[0].CostUSD.Should().Be(0.05m);
        entries[0].Timestamp.Should().BeCloseTo(
            new DateTime(2026, 5, 25, 10, 0, 0, DateTimeKind.Utc), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ReadAll_SkipsNonAssistantEntries()
    {
        WriteJsonl("proj1/conv1.jsonl",
            """{"type":"human","timestamp":"2026-05-25T10:00:00Z","costUSD":0.05}""",
            """{"type":"assistant","timestamp":"2026-05-25T10:01:00Z","costUSD":0.10}""");

        var entries = new UsageReader().ReadAll(_dir).ToList();

        entries.Should().HaveCount(1);
        entries[0].CostUSD.Should().Be(0.10m);
    }

    [Fact]
    public void ReadAll_SkipsZeroCostEntries()
    {
        WriteJsonl("proj1/conv1.jsonl",
            """{"type":"assistant","timestamp":"2026-05-25T10:00:00Z","costUSD":0.0}""",
            """{"type":"assistant","timestamp":"2026-05-25T10:01:00Z","costUSD":0.05}""");

        var entries = new UsageReader().ReadAll(_dir).ToList();

        entries.Should().HaveCount(1);
        entries[0].CostUSD.Should().Be(0.05m);
    }

    [Fact]
    public void ReadAll_SkipsMalformedLines()
    {
        WriteJsonl("proj1/conv1.jsonl",
            "not valid json",
            """{"type":"assistant","timestamp":"2026-05-25T10:00:00Z","costUSD":0.05}""");

        var entries = new UsageReader().ReadAll(_dir).ToList();

        entries.Should().HaveCount(1);
    }

    [Fact]
    public void ReadAll_ReturnsEmpty_WhenDirectoryDoesNotExist()
    {
        var entries = new UsageReader().ReadAll(Path.Combine(_dir, "nonexistent")).ToList();
        entries.Should().BeEmpty();
    }

    [Fact]
    public void ReadAll_AggregatesAcrossMultipleFiles()
    {
        WriteJsonl("proj1/conv1.jsonl",
            """{"type":"assistant","timestamp":"2026-05-25T10:00:00Z","costUSD":0.05}""");
        WriteJsonl("proj2/conv2.jsonl",
            """{"type":"assistant","timestamp":"2026-05-25T11:00:00Z","costUSD":0.10}""");

        var entries = new UsageReader().ReadAll(_dir).ToList();

        entries.Should().HaveCount(2);
        entries.Sum(e => e.CostUSD).Should().Be(0.15m);
    }
}
