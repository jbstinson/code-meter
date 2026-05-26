using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeMeter.Core;

public class UsageReader
{
    // ── Token-weight table (output-token-equivalents per million tokens) ───
    // All token types are weighted relative to one Sonnet output token = 1.0
    // (i.e., 1 000 000 units per million output tokens).
    //
    // Calibration methodology (exact token counts, 2026-05-26 session):
    //   Tokens:  274 input | 223 480 output | 556 636 cache_write | 18 943 305 cache_read
    //   Claude Code UI: 37%   Code Meter pre-fix: 30.6%
    //   Weights that reproduce 37%:
    //     cache_write = 250 000/M  (= $3.75/$15 = API 1.25x-input rate, exact)
    //     cache_read  =   9 500/M  (= ~$0.1425/M effective, ~47.5% of $0.30/M API rate)
    //
    //   input/output ratios derive from Anthropic API list prices:
    //     Sonnet: input=$3/M  output=$15/M   -> input_weight = 200 000/M
    //     Haiku:  input=$0.80/M  output=$4/M -> input_weight =  53 333/M
    //     Opus:   input=$15/M  output=$75/M  -> input_weight = 1 000 000/M
    //   cache_write = 1.25x input (Anthropic API charges 1.25x for cache writes vs input)
    //   cache_read is empirically calibrated; scale across models by API-rate ratio.
    private sealed record ModelWeights(
        decimal InputPerM,
        decimal OutputPerM,
        decimal CacheWritePerM,
        decimal CacheReadPerM = 0m);

    private static readonly (string Prefix, ModelWeights Weights)[] _weightTable =
    [
        // ── Sonnet 4.x ─────────────────────────────────────────────────────
        ("claude-sonnet-4",   new(200_000m, 1_000_000m,  250_000m,  9_500m)),
        // ── Haiku 4.x / 3.x ─────────────────────────────────────────────────
        // cache_write = 53333 × 1.25 = 66667; cache_read scales by $0.03/$0.30 of Sonnet
        ("claude-haiku-4",    new( 53_333m,   266_667m,   66_667m,    950m)),
        ("claude-haiku-3",    new( 53_333m,   266_667m,   66_667m,    950m)),
        // ── Opus 4.x / Opus 3 ────────────────────────────────────────────────
        // cache_write = 1M × 1.25 = 1.25M; cache_read scales by $1.50/$0.30 of Sonnet
        ("claude-opus-4",     new(1_000_000m, 5_000_000m, 1_250_000m, 47_500m)),
        ("claude-3-opus",     new(1_000_000m, 5_000_000m, 1_250_000m, 47_500m)),
        // ── Sonnet 3.x ──────────────────────────────────────────────────────
        ("claude-3-5-sonnet", new(200_000m, 1_000_000m,  250_000m,  9_500m)),
        ("claude-3-sonnet",   new(200_000m, 1_000_000m,  250_000m,  9_500m)),
    ];

    // Fallback for unrecognised models — use Sonnet 4 weights
    private static readonly ModelWeights _fallback =
        new(200_000m, 1_000_000m, 250_000m, 9_500m);

    private static ModelWeights GetWeights(string? model)
    {
        if (model is null) return _fallback;
        foreach (var (prefix, weights) in _weightTable)
            if (model.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return weights;
        return _fallback;
    }

    // ── JSON shape ─────────────────────────────────────────────────────────
    // Claude Code JSONL uses two formats depending on CLI version:
    //
    //   Old format:  {"type":"assistant","timestamp":"...","message":{...,"usage":{...}}}
    //   New format:  {"parentUuid":"...","message":{...,"role":"assistant",...,"usage":{...}},"timestamp":"..."}
    //
    // Both formats have the timestamp at the root and usage inside message.
    // We detect assistant rows by checking EITHER root "type"=="assistant"
    // OR message.role=="assistant".
    private record TokenUsage(
        [property: JsonPropertyName("input_tokens")]                long InputTokens,
        [property: JsonPropertyName("output_tokens")]               long OutputTokens,
        [property: JsonPropertyName("cache_creation_input_tokens")] long CacheCreationTokens,
        [property: JsonPropertyName("cache_read_input_tokens")]     long CacheReadTokens
    );

    private record MessageInfo(
        [property: JsonPropertyName("model")]  string?     Model,
        [property: JsonPropertyName("role")]   string?     Role,
        [property: JsonPropertyName("usage")]  TokenUsage? Usage
    );

    private record JsonlRow(
        [property: JsonPropertyName("type")]      string?      Type,
        [property: JsonPropertyName("timestamp")] DateTime?    Timestamp,
        [property: JsonPropertyName("message")]   MessageInfo? Message
    );

    private static readonly JsonSerializerOptions _opts = new() { PropertyNameCaseInsensitive = true };

    // ── Public API ─────────────────────────────────────────────────────────

    public IEnumerable<UsageEntry> ReadAll(string claudeProjectsDir)
    {
        if (!Directory.Exists(claudeProjectsDir))
            yield break;

        foreach (var file in Directory.EnumerateFiles(
                     claudeProjectsDir, "*.jsonl", SearchOption.AllDirectories))
        {
            foreach (var entry in ReadFile(file))
                yield return entry;
        }
    }

    internal IEnumerable<UsageEntry> ReadFile(string path)
    {
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            JsonlRow? row;
            try { row = JsonSerializer.Deserialize<JsonlRow>(line, _opts); }
            catch { continue; }

            // Accept old format (root "type":"assistant") OR new format (message.role:"assistant")
            var isAssistant = row?.Type == "assistant"
                           || row?.Message?.Role?.Equals("assistant", StringComparison.OrdinalIgnoreCase) == true;
            if (!isAssistant) continue;
            if (row!.Timestamp is null || row.Message?.Usage is null) continue;

            var weighted = ComputeWeightedTokens(row.Message.Usage, row.Message.Model);
            if (weighted <= 0) continue;

            yield return new UsageEntry(row.Timestamp.Value, weighted);
        }
    }

    private static decimal ComputeWeightedTokens(TokenUsage u, string? model)
    {
        var w = GetWeights(model);
        // cache_creation_input_tokens: counted at CacheWritePerM (API: 1.25× input rate).
        // cache_read_input_tokens: counted at CacheReadPerM (empirically ~47.5% of API rate).
        return (u.InputTokens         * w.InputPerM
              + u.CacheCreationTokens * w.CacheWritePerM
              + u.CacheReadTokens     * w.CacheReadPerM
              + u.OutputTokens        * w.OutputPerM)
               / 1_000_000m;
    }
}
