using System.IO;
using System.Text.Json;

namespace CodeMeter.Settings;

public class SettingsStore(string filePath)
{
    private static readonly JsonSerializerOptions _opts = new() { WriteIndented = true };

    public bool Exists() => File.Exists(filePath);

    public AppSettings Load()
    {
        if (!File.Exists(filePath))
            return new AppSettings();
        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<AppSettings>(json, _opts) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var tmp = filePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, _opts));
        File.Move(tmp, filePath, overwrite: true);
    }
}
