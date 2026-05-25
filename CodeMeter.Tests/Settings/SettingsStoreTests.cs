using CodeMeter.Settings;
using FluentAssertions;
using Xunit;

namespace CodeMeter.Tests.Settings;

public class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public SettingsStoreTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public void Load_ReturnsDefaults_WhenFileDoesNotExist()
    {
        var store = new SettingsStore(Path.Combine(_dir, "settings.json"));
        var s = store.Load();
        s.DailyLimitUSD.Should().Be(5.00m);
        s.WeeklyLimitUSD.Should().Be(35.00m);
        s.AlertThresholds.Should().BeEquivalentTo(new[] { 60, 80, 95 });
        s.PollIntervalSeconds.Should().Be(30);
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        var store = new SettingsStore(Path.Combine(_dir, "settings.json"));
        var s = new AppSettings { DailyLimitUSD = 10m, PollIntervalSeconds = 60 };
        store.Save(s);
        var loaded = store.Load();
        loaded.DailyLimitUSD.Should().Be(10m);
        loaded.PollIntervalSeconds.Should().Be(60);
    }

    [Fact]
    public void Exists_ReturnsFalse_WhenFileAbsent()
    {
        var store = new SettingsStore(Path.Combine(_dir, "settings.json"));
        store.Exists().Should().BeFalse();
    }

    [Fact]
    public void Exists_ReturnsTrue_AfterSave()
    {
        var store = new SettingsStore(Path.Combine(_dir, "settings.json"));
        store.Save(new AppSettings());
        store.Exists().Should().BeTrue();
    }

    [Fact]
    public void Save_WritesAtomically_NoPartialFile()
    {
        var path = Path.Combine(_dir, "settings.json");
        var store = new SettingsStore(path);
        store.Save(new AppSettings());
        File.Exists(path + ".tmp").Should().BeFalse();
        File.Exists(path).Should().BeTrue();
    }
}
