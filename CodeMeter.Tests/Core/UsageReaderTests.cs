using CodeMeter.Core;
using FluentAssertions;
using Xunit;

namespace CodeMeter.Tests.Core;

public class UsageReaderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public UsageReaderTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, true);

    // ── Helpers ────────────────────────────────────────────────────────────

    private void WriteJsonl(string subPath, params string[] lines)
    {
        var path = Path.Combine(_dir, subPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllLines(path, lines);
    }

    /// <summary>Build an assistant JSONL line in the old format (root type:"assistant").</summary>
    private static string AssistantLine(
        string timestamp,
        string model = "claude-sonnet-4-6",
        long inputTokens = 10_000,
        long outputTokens = 1_000,
        long cacheWrite = 0,
        long cacheRead = 0)
    {
        var obj = new
        {
            type      = "assistant",
            timestamp = timestamp,
            message   = new
            {
                model = model,
                usage = new
                {
                    input_tokens                = inputTokens,
                    output_tokens               = outputTokens,
                    cache_creation_input_tokens = cacheWrite,
                    cache_read_input_tokens     = cacheRead,
                }
            }
        };
        return System.Text.Json.JsonSerializer.Serialize(obj);
    }

    /// <summary>Build an assistant JSONL line in the new format (message.role:"assistant").</summary>
    private static string NewFormatAssistantLine(
        string timestamp,
        string model = "claude-sonnet-4-6",
        long inputTokens = 10_000,
        long outputTokens = 1_000,
        long cacheWrite = 0,
        long cacheRead = 0)
    {
        var obj = new
        {
            parentUuid  = "48d20b7c-0000-0000-0000-000000000000",
            isSidechain = false,
            timestamp   = timestamp,
            message     = new
            {
                model = model,
                role  = "assistant",
                usage = new
                {
                    input_tokens                = inputTokens,
                    output_tokens               = outputTokens,
                    cache_creation_input_tokens = cacheWrite,
                    cache_read_input_tokens     = cacheRead,
                }
            }
        };
        return System.Text.Json.JsonSerializer.Serialize(obj);
    }

    // Sonnet 4 weights (per million tokens):
    //   input=200 000, output=1 000 000, cache_write=250 000, cache_read=9 500
    // Default test: 10 000 input + 1 000 output (no cache tokens)
    // WeightedTokens = (10 000 × 200 000 + 1 000 × 1 000 000) / 1 000 000 = 3 000
    private const decimal SonnetWeighted =
        (10_000m * 200_000m + 1_000m * 1_000_000m) / 1_000_000m; // 3 000

    // ── Tests ──────────────────────────────────────────────────────────────

    [Fact]
    public void ReadAll_ParsesValidAssistantEntries()
    {
        WriteJsonl("proj1/conv1.jsonl",
            AssistantLine("2026-05-25T10:00:00Z"));

        var entries = new UsageReader().ReadAll(_dir).ToList();

        entries.Should().HaveCount(1);
        entries[0].WeightedTokens.Should().Be(SonnetWeighted);
        entries[0].Timestamp.Should().BeCloseTo(
            new DateTime(2026, 5, 25, 10, 0, 0, DateTimeKind.Utc), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ReadAll_SkipsNonAssistantEntries()
    {
        // Old-format "human" row (root type not "assistant") should be skipped
        var humanLine = System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "human", timestamp = "2026-05-25T10:00:00Z",
            message = new { model = "claude-sonnet-4-6",
                usage = new { input_tokens = 10000, output_tokens = 1000,
                    cache_creation_input_tokens = 0, cache_read_input_tokens = 0 } }
        });
        // New-format "queue-operation" row (no role) should also be skipped
        var queueLine = System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "queue-operation", timestamp = "2026-05-25T10:00:30Z",
            message = (object?)null
        });
        WriteJsonl("proj1/conv1.jsonl", humanLine, queueLine, AssistantLine("2026-05-25T10:01:00Z"));

        var entries = new UsageReader().ReadAll(_dir).ToList();

        entries.Should().HaveCount(1);
    }

    [Fact]
    public void ReadAll_ParsesNewFormatAssistantEntries()
    {
        // New format: no root "type":"assistant", instead message.role:"assistant"
        WriteJsonl("proj1/conv1.jsonl",
            NewFormatAssistantLine("2026-05-25T10:00:00Z"));

        var entries = new UsageReader().ReadAll(_dir).ToList();

        entries.Should().HaveCount(1);
        entries[0].WeightedTokens.Should().Be(SonnetWeighted);
    }

    [Fact]
    public void ReadAll_ParsesBothFormatsInSameFile()
    {
        // Old and new format lines can coexist in the same file
        WriteJsonl("proj1/conv1.jsonl",
            AssistantLine("2026-05-25T10:00:00Z"),
            NewFormatAssistantLine("2026-05-25T10:01:00Z"));

        var entries = new UsageReader().ReadAll(_dir).ToList();

        entries.Should().HaveCount(2);
        entries.Sum(e => e.WeightedTokens).Should().Be(SonnetWeighted * 2);
    }

    [Fact]
    public void ReadAll_SkipsZeroWeightEntries()
    {
        WriteJsonl("proj1/conv1.jsonl",
            // all-zero tokens → weight 0 → skipped
            AssistantLine("2026-05-25T10:00:00Z", inputTokens: 0, outputTokens: 0),
            AssistantLine("2026-05-25T10:01:00Z"));

        var entries = new UsageReader().ReadAll(_dir).ToList();

        entries.Should().HaveCount(1);
        entries[0].WeightedTokens.Should().Be(SonnetWeighted);
    }

    [Fact]
    public void ReadAll_SkipsMalformedLines()
    {
        WriteJsonl("proj1/conv1.jsonl",
            "not valid json",
            AssistantLine("2026-05-25T10:00:00Z"));

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
        WriteJsonl("proj1/conv1.jsonl", AssistantLine("2026-05-25T10:00:00Z"));
        WriteJsonl("proj2/conv2.jsonl", AssistantLine("2026-05-25T11:00:00Z"));

        var entries = new UsageReader().ReadAll(_dir).ToList();

        entries.Should().HaveCount(2);
        entries.Sum(e => e.WeightedTokens).Should().Be(SonnetWeighted * 2);
    }

    [Fact]
    public void ReadAll_ComputesWeight_ForHaikuModel()
    {
        // Haiku 4 weights: input=53 333 / output=266 667 (no cache tokens in this test)
        // 10 000 input + 1 000 output → (10 000×53 333 + 1 000×266 667) / 1 000 000
        const decimal expected =
            (10_000m * 53_333m + 1_000m * 266_667m) / 1_000_000m; // ≈ 799.997

        WriteJsonl("proj1/conv1.jsonl",
            AssistantLine("2026-05-25T10:00:00Z", model: "claude-haiku-4-5-20251001"));

        var entries = new UsageReader().ReadAll(_dir).ToList();

        entries.Should().HaveCount(1);
        entries[0].WeightedTokens.Should().Be(expected);
    }

    [Fact]
    public void ReadAll_IncludesCacheWriteTokensInWeight()
    {
        // cache_creation_input_tokens are counted at CacheWritePerM = 250 000/M (1.25x input).
        // 10 000 input + 500 000 cache_write + 1 000 output (Sonnet):
        // (10 000×200 000 + 500 000×250 000 + 1 000×1 000 000) / 1 000 000 = 128 000
        const decimal expected =
            (10_000m * 200_000m + 500_000m * 250_000m + 1_000m * 1_000_000m) / 1_000_000m;

        WriteJsonl("proj1/conv1.jsonl",
            AssistantLine("2026-05-25T10:00:00Z",
                inputTokens: 10_000, outputTokens: 1_000, cacheWrite: 500_000));

        var entries = new UsageReader().ReadAll(_dir).ToList();

        entries.Should().HaveCount(1);
        entries[0].WeightedTokens.Should().Be(expected);
    }

    [Fact]
    public void ReadAll_IncludesCacheReadTokensAtCalibratedWeight()
    {
        // cache_read_input_tokens weighted at 9 500 per million for Sonnet
        // (empirically ~47.5% of API cache-read rate, calibrated 2026-05-26).
        // 50 000 000 cache reads → 50 000 000 × 9 500 / 1 000 000 = 475 000
        // Plus baseline (10K input + 1K output) = 3 000
        // Total = 478 000
        const decimal expected =
            (10_000m * 200_000m + 1_000m * 1_000_000m + 50_000_000m * 9_500m) / 1_000_000m;

        WriteJsonl("proj1/conv1.jsonl",
            AssistantLine("2026-05-25T10:00:00Z",
                inputTokens: 10_000, outputTokens: 1_000,
                cacheWrite: 0, cacheRead: 50_000_000));

        var entries = new UsageReader().ReadAll(_dir).ToList();

        entries.Should().HaveCount(1);
        entries[0].WeightedTokens.Should().Be(expected);
    }

    [Fact]
    public void ReadAll_UsesVersionedModelNamePrefix()
    {
        // "claude-sonnet-4-5-20250929" should match the "claude-sonnet-4" prefix
        WriteJsonl("proj1/conv1.jsonl",
            AssistantLine("2026-05-25T10:00:00Z", model: "claude-sonnet-4-5-20250929"));

        var entries = new UsageReader().ReadAll(_dir).ToList();

        entries.Should().HaveCount(1);
        entries[0].WeightedTokens.Should().Be(SonnetWeighted);
    }
}
