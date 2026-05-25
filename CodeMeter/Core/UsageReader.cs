using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeMeter.Core;

public class UsageReader
{
    private record JsonlRow(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("timestamp")] DateTime? Timestamp,
        [property: JsonPropertyName("costUSD")] decimal? CostUSD
    );

    private static readonly JsonSerializerOptions _opts = new() { PropertyNameCaseInsensitive = true };

    public IEnumerable<UsageEntry> ReadAll(string claudeProjectsDir)
    {
        if (!Directory.Exists(claudeProjectsDir))
            yield break;

        foreach (var file in Directory.EnumerateFiles(claudeProjectsDir, "*.jsonl", SearchOption.AllDirectories))
        {
            foreach (var line in File.ReadLines(file))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                JsonlRow? row;
                try { row = JsonSerializer.Deserialize<JsonlRow>(line, _opts); }
                catch { continue; }
                if (row?.Type != "assistant") continue;
                if (row.Timestamp is null || row.CostUSD is null || row.CostUSD <= 0) continue;
                yield return new UsageEntry(row.Timestamp.Value, row.CostUSD.Value);
            }
        }
    }
}
