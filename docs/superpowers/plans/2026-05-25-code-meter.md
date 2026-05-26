# Code Meter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Windows system tray WPF app that reads Claude Code's local JSONL usage logs and displays daily/weekly usage meters with a color-coded tray icon and configurable Windows toast alerts.

**Architecture:** Seven focused units (UsageReader, UsageAggregator, SettingsStore, TrayViewModel, TrayPopup, AlertService, PollingService) wired together in App.xaml.cs. No main window — the app lives entirely in the system tray. A `System.Threading.Timer` drives the read → aggregate → update → alert cycle on a configurable interval.

**Tech Stack:** C# / .NET 8 WPF (`net8.0-windows10.0.17763.0`), H.NotifyIcon.Wpf 2.1.0 (tray icon + popup), Microsoft.Toolkit.Uwp.Notifications 7.1.3 (Windows toasts), System.Text.Json (inbox), xUnit 2.9.0 + FluentAssertions 6.12.0 (tests)

---

## File Map

| File | Responsibility |
|---|---|
| `CodeMeter/App.xaml` | ShutdownMode=OnExplicitShutdown, no StartupUri |
| `CodeMeter/App.xaml.cs` | Wires all services, creates tray icon, handles first-run |
| `CodeMeter/Core/UsageEntry.cs` | Record: Timestamp + CostUSD |
| `CodeMeter/Core/WindowSummary.cs` | Record: AmountUsed, Limit, PercentUsed, ResetsAt |
| `CodeMeter/Core/UsageReader.cs` | Reads `~/.claude/projects/**/*.jsonl` |
| `CodeMeter/Core/UsageAggregator.cs` | Computes daily/weekly WindowSummary |
| `CodeMeter/Core/PollingService.cs` | Timer-driven polling loop |
| `CodeMeter/Alerts/AlertService.cs` | Per-window threshold tracking + toast dispatch |
| `CodeMeter/Settings/AppSettings.cs` | Settings POCO with defaults |
| `CodeMeter/Settings/SettingsStore.cs` | Atomic JSON read/write |
| `CodeMeter/UI/TrayViewModel.cs` | INotifyPropertyChanged VM |
| `CodeMeter/UI/TrayPopup.xaml(.cs)` | Popup panel with gradient bars |
| `CodeMeter/UI/SettingsWindow.xaml(.cs)` | Settings window |
| `CodeMeter.Tests/Core/UsageReaderTests.cs` | JSONL parsing tests |
| `CodeMeter.Tests/Core/UsageAggregatorTests.cs` | Window calculation tests |
| `CodeMeter.Tests/Alerts/AlertServiceTests.cs` | Threshold crossing tests |
| `CodeMeter.Tests/Settings/SettingsStoreTests.cs` | Read/write/defaults tests |
| `CodeMeter.Tests/UI/TrayViewModelTests.cs` | Icon color logic tests |

---

## Task 1: Project Scaffold

**Files:**
- Create: `CodeMeter.sln`
- Create: `CodeMeter/CodeMeter.csproj`
- Create: `CodeMeter.Tests/CodeMeter.Tests.csproj`
- Modify: `CodeMeter/App.xaml`
- Modify: `CodeMeter/App.xaml.cs`

- [ ] **Step 1: Create solution and projects**

```bash
cd C:\git\code-meter
dotnet new sln -n CodeMeter
dotnet new wpf -n CodeMeter -o CodeMeter
dotnet new xunit -n CodeMeter.Tests -o CodeMeter.Tests
dotnet sln add CodeMeter/CodeMeter.csproj
dotnet sln add CodeMeter.Tests/CodeMeter.Tests.csproj
dotnet add CodeMeter.Tests/CodeMeter.Tests.csproj reference CodeMeter/CodeMeter.csproj
```

- [ ] **Step 2: Add NuGet packages**

```bash
dotnet add CodeMeter/CodeMeter.csproj package H.NotifyIcon.Wpf --version 2.1.0
dotnet add CodeMeter/CodeMeter.csproj package Microsoft.Toolkit.Uwp.Notifications --version 7.1.3
dotnet add CodeMeter.Tests/CodeMeter.Tests.csproj package FluentAssertions --version 6.12.0
dotnet add CodeMeter.Tests/CodeMeter.Tests.csproj package Microsoft.NET.Test.Sdk --version 17.11.1
dotnet add CodeMeter.Tests/CodeMeter.Tests.csproj package xunit --version 2.9.0
dotnet add CodeMeter.Tests/CodeMeter.Tests.csproj package xunit.runner.visualstudio --version 2.8.2
dotnet add CodeMeter.Tests/CodeMeter.Tests.csproj package coverlet.collector --version 6.0.2
```

- [ ] **Step 3: Set target frameworks**

Replace `CodeMeter/CodeMeter.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows10.0.17763.0</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>CodeMeter</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="H.NotifyIcon.Wpf" Version="2.1.0" />
    <PackageReference Include="Microsoft.Toolkit.Uwp.Notifications" Version="7.1.3" />
  </ItemGroup>
</Project>
```

Replace `CodeMeter.Tests/CodeMeter.Tests.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows10.0.17763.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="FluentAssertions" Version="6.12.0" />
    <PackageReference Include="coverlet.collector" Version="6.0.2" />
    <ProjectReference Include="..\CodeMeter\CodeMeter.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Configure App.xaml for no main window**

Replace `CodeMeter/App.xaml` with:

```xml
<Application x:Class="CodeMeter.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnExplicitShutdown">
  <Application.Resources/>
</Application>
```

Replace `CodeMeter/App.xaml.cs` with:

```csharp
using System.Windows;

namespace CodeMeter;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Wiring added in Task 11
    }
}
```

- [ ] **Step 5: Verify build**

```bash
dotnet build CodeMeter.sln
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git init
git add .
git commit -m "chore: scaffold WPF app and test projects"
```

---

## Task 2: Core Models

**Files:**
- Create: `CodeMeter/Core/UsageEntry.cs`
- Create: `CodeMeter/Core/WindowSummary.cs`
- Create: `CodeMeter/Settings/AppSettings.cs`

- [ ] **Step 1: Create UsageEntry**

Create `CodeMeter/Core/UsageEntry.cs`:

```csharp
namespace CodeMeter.Core;

public record UsageEntry(DateTime Timestamp, decimal CostUSD);
```

- [ ] **Step 2: Create WindowSummary**

Create `CodeMeter/Core/WindowSummary.cs`:

```csharp
namespace CodeMeter.Core;

public record WindowSummary(
    decimal AmountUsed,
    decimal Limit,
    double PercentUsed,
    DateTime ResetsAt
);
```

- [ ] **Step 3: Create AppSettings**

Create `CodeMeter/Settings/AppSettings.cs`:

```csharp
namespace CodeMeter.Settings;

public class AppSettings
{
    public decimal DailyLimitUSD { get; set; } = 5.00m;
    public decimal WeeklyLimitUSD { get; set; } = 35.00m;
    public int DailyResetHour { get; set; } = 0;
    public DayOfWeek WeeklyResetDay { get; set; } = DayOfWeek.Monday;
    public int WeeklyResetHour { get; set; } = 0;
    public List<int> AlertThresholds { get; set; } = [60, 80, 95];
    public int PollIntervalSeconds { get; set; } = 30;
}
```

- [ ] **Step 4: Build to verify**

```bash
dotnet build CodeMeter/CodeMeter.csproj
```
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add CodeMeter/Core/UsageEntry.cs CodeMeter/Core/WindowSummary.cs CodeMeter/Settings/AppSettings.cs
git commit -m "feat: add core model records and AppSettings"
```

---

## Task 3: SettingsStore

**Files:**
- Create: `CodeMeter/Settings/SettingsStore.cs`
- Create: `CodeMeter.Tests/Settings/SettingsStoreTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `CodeMeter.Tests/Settings/SettingsStoreTests.cs`:

```csharp
using CodeMeter.Settings;
using FluentAssertions;

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
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test CodeMeter.Tests/CodeMeter.Tests.csproj --filter "FullyQualifiedName~SettingsStoreTests" -v minimal
```
Expected: FAIL — `SettingsStore` not defined.

- [ ] **Step 3: Implement SettingsStore**

Create `CodeMeter/Settings/SettingsStore.cs`:

```csharp
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
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test CodeMeter.Tests/CodeMeter.Tests.csproj --filter "FullyQualifiedName~SettingsStoreTests" -v minimal
```
Expected: 5 passed.

- [ ] **Step 5: Commit**

```bash
git add CodeMeter/Settings/SettingsStore.cs CodeMeter.Tests/Settings/SettingsStoreTests.cs
git commit -m "feat: add SettingsStore with atomic JSON read/write"
```

---

## Task 4: UsageReader

**Files:**
- Create: `CodeMeter/Core/UsageReader.cs`
- Create: `CodeMeter.Tests/Core/UsageReaderTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `CodeMeter.Tests/Core/UsageReaderTests.cs`:

```csharp
using CodeMeter.Core;
using FluentAssertions;

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
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test CodeMeter.Tests/CodeMeter.Tests.csproj --filter "FullyQualifiedName~UsageReaderTests" -v minimal
```
Expected: FAIL — `UsageReader` not defined.

- [ ] **Step 3: Implement UsageReader**

Create `CodeMeter/Core/UsageReader.cs`:

```csharp
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
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test CodeMeter.Tests/CodeMeter.Tests.csproj --filter "FullyQualifiedName~UsageReaderTests" -v minimal
```
Expected: 6 passed.

- [ ] **Step 5: Commit**

```bash
git add CodeMeter/Core/UsageReader.cs CodeMeter.Tests/Core/UsageReaderTests.cs
git commit -m "feat: add UsageReader to parse Claude Code JSONL usage logs"
```

---

## Task 5: UsageAggregator

**Files:**
- Create: `CodeMeter/Core/UsageAggregator.cs`
- Create: `CodeMeter.Tests/Core/UsageAggregatorTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `CodeMeter.Tests/Core/UsageAggregatorTests.cs`:

```csharp
using CodeMeter.Core;
using CodeMeter.Settings;
using FluentAssertions;

namespace CodeMeter.Tests.Core;

public class UsageAggregatorTests
{
    private static AppSettings DefaultSettings() => new()
    {
        DailyLimitUSD = 5.00m,
        WeeklyLimitUSD = 35.00m,
        DailyResetHour = 0,
        WeeklyResetDay = DayOfWeek.Monday,
        WeeklyResetHour = 0
    };

    private static UsageEntry E(DateTime ts, decimal cost) => new(ts, cost);

    [Fact]
    public void ComputeDaily_IncludesEntriesAfterTodaysResetHour()
    {
        var now = new DateTime(2026, 5, 25, 14, 0, 0);
        var entries = new[] { E(new DateTime(2026, 5, 25, 8, 0, 0), 1.00m) };

        var summary = new UsageAggregator().ComputeDaily(entries, DefaultSettings(), now);

        summary.AmountUsed.Should().Be(1.00m);
        summary.PercentUsed.Should().BeApproximately(20.0, 0.01);
    }

    [Fact]
    public void ComputeDaily_ExcludesEntriesBeforeWindowStart()
    {
        var now = new DateTime(2026, 5, 25, 14, 0, 0);
        var entries = new[] { E(new DateTime(2026, 5, 24, 8, 0, 0), 1.00m) };

        var summary = new UsageAggregator().ComputeDaily(entries, DefaultSettings(), now);

        summary.AmountUsed.Should().Be(0m);
    }

    [Fact]
    public void ComputeDaily_UsesYesterdayReset_WhenBeforeResetHour()
    {
        var settings = DefaultSettings();
        settings.DailyResetHour = 8;
        var now = new DateTime(2026, 5, 25, 6, 0, 0);
        var entries = new[] { E(new DateTime(2026, 5, 24, 9, 0, 0), 2.00m) };

        var summary = new UsageAggregator().ComputeDaily(entries, settings, now);

        summary.AmountUsed.Should().Be(2.00m);
    }

    [Fact]
    public void ComputeDaily_ResetsAt_IsOneDayAfterWindowStart()
    {
        var now = new DateTime(2026, 5, 25, 14, 0, 0);

        var summary = new UsageAggregator().ComputeDaily([], DefaultSettings(), now);

        summary.ResetsAt.Should().Be(new DateTime(2026, 5, 26, 0, 0, 0));
    }

    [Fact]
    public void ComputeWeekly_IncludesEntriesAfterWeeklyReset()
    {
        var now = new DateTime(2026, 5, 27, 14, 0, 0); // Wednesday
        var entries = new[] { E(new DateTime(2026, 5, 26, 8, 0, 0), 5.00m) }; // Tuesday

        var summary = new UsageAggregator().ComputeWeekly(entries, DefaultSettings(), now);

        summary.AmountUsed.Should().Be(5.00m);
    }

    [Fact]
    public void ComputeWeekly_ExcludesEntriesFromPreviousWeek()
    {
        var now = new DateTime(2026, 5, 27, 14, 0, 0);
        var entries = new[] { E(new DateTime(2026, 5, 18, 8, 0, 0), 5.00m) };

        var summary = new UsageAggregator().ComputeWeekly(entries, DefaultSettings(), now);

        summary.AmountUsed.Should().Be(0m);
    }

    [Fact]
    public void ComputeWeekly_ResetsAt_IsSevenDaysAfterWindowStart()
    {
        var now = new DateTime(2026, 5, 27, 14, 0, 0); // Wednesday

        var summary = new UsageAggregator().ComputeWeekly([], DefaultSettings(), now);

        // Window started Monday 2026-05-25 00:00, resets Monday 2026-06-01 00:00
        summary.ResetsAt.Should().Be(new DateTime(2026, 6, 1, 0, 0, 0));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test CodeMeter.Tests/CodeMeter.Tests.csproj --filter "FullyQualifiedName~UsageAggregatorTests" -v minimal
```
Expected: FAIL — `UsageAggregator` not defined.

- [ ] **Step 3: Implement UsageAggregator**

Create `CodeMeter/Core/UsageAggregator.cs`:

```csharp
using CodeMeter.Settings;

namespace CodeMeter.Core;

public class UsageAggregator
{
    public WindowSummary ComputeDaily(IEnumerable<UsageEntry> entries, AppSettings settings, DateTime now)
    {
        var start = GetDailyWindowStart(settings, now);
        var end = start.AddDays(1);
        var used = entries.Where(e => e.Timestamp >= start && e.Timestamp < end).Sum(e => e.CostUSD);
        var pct = settings.DailyLimitUSD > 0
            ? Math.Min(100.0, (double)(used / settings.DailyLimitUSD) * 100.0)
            : 0.0;
        return new WindowSummary(used, settings.DailyLimitUSD, pct, end);
    }

    public WindowSummary ComputeWeekly(IEnumerable<UsageEntry> entries, AppSettings settings, DateTime now)
    {
        var start = GetWeeklyWindowStart(settings, now);
        var end = start.AddDays(7);
        var used = entries.Where(e => e.Timestamp >= start && e.Timestamp < end).Sum(e => e.CostUSD);
        var pct = settings.WeeklyLimitUSD > 0
            ? Math.Min(100.0, (double)(used / settings.WeeklyLimitUSD) * 100.0)
            : 0.0;
        return new WindowSummary(used, settings.WeeklyLimitUSD, pct, end);
    }

    internal static DateTime GetDailyWindowStart(AppSettings settings, DateTime now)
    {
        var todayReset = new DateTime(now.Year, now.Month, now.Day, settings.DailyResetHour, 0, 0);
        return now >= todayReset ? todayReset : todayReset.AddDays(-1);
    }

    internal static DateTime GetWeeklyWindowStart(AppSettings settings, DateTime now)
    {
        var daysBack = ((int)now.DayOfWeek - (int)settings.WeeklyResetDay + 7) % 7;
        var candidate = new DateTime(now.Year, now.Month, now.Day, settings.WeeklyResetHour, 0, 0)
                            .AddDays(-daysBack);
        if (candidate > now) candidate = candidate.AddDays(-7);
        return candidate;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test CodeMeter.Tests/CodeMeter.Tests.csproj --filter "FullyQualifiedName~UsageAggregatorTests" -v minimal
```
Expected: 7 passed.

- [ ] **Step 5: Commit**

```bash
git add CodeMeter/Core/UsageAggregator.cs CodeMeter.Tests/Core/UsageAggregatorTests.cs
git commit -m "feat: add UsageAggregator with daily/weekly windowing"
```

---

## Task 6: TrayViewModel

**Files:**
- Create: `CodeMeter/UI/TrayViewModel.cs`
- Create: `CodeMeter.Tests/UI/TrayViewModelTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `CodeMeter.Tests/UI/TrayViewModelTests.cs`:

```csharp
using CodeMeter.UI;
using FluentAssertions;
using System.Drawing;

namespace CodeMeter.Tests.UI;

public class TrayViewModelTests
{
    [Theory]
    [InlineData(0,   166, 227, 161)]  // green
    [InlineData(60,  166, 227, 161)]  // green upper edge
    [InlineData(61,  249, 226, 175)]  // yellow lower edge
    [InlineData(89,  249, 226, 175)]  // yellow upper edge
    [InlineData(90,  243, 139, 168)]  // red lower edge
    [InlineData(99,  243, 139, 168)]  // red upper edge
    [InlineData(100, 139, 0,   0  )]  // dark red
    public void ResolveIconColor_ReturnsCorrectColor(double percent, int r, int g, int b)
    {
        var color = TrayViewModel.ResolveIconColor(percent);
        color.R.Should().Be((byte)r);
        color.G.Should().Be((byte)g);
        color.B.Should().Be((byte)b);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test CodeMeter.Tests/CodeMeter.Tests.csproj --filter "FullyQualifiedName~TrayViewModelTests" -v minimal
```
Expected: FAIL — `TrayViewModel` not defined.

- [ ] **Step 3: Implement TrayViewModel**

Create `CodeMeter/UI/TrayViewModel.cs`:

```csharp
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using CodeMeter.Core;

namespace CodeMeter.UI;

public class TrayViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private double _dailyPercent;
    private double _weeklyPercent;
    private string _dailyResetText = "";
    private string _weeklyResetText = "";
    private string _tooltipText = "Claude Code Usage";
    private Color _iconColor = Color.FromArgb(166, 227, 161);

    public double DailyPercent  { get => _dailyPercent;  private set { _dailyPercent = value;  Notify(); } }
    public double WeeklyPercent { get => _weeklyPercent; private set { _weeklyPercent = value; Notify(); } }
    public string DailyResetText  { get => _dailyResetText;  private set { _dailyResetText = value;  Notify(); } }
    public string WeeklyResetText { get => _weeklyResetText; private set { _weeklyResetText = value; Notify(); } }
    public string TooltipText { get => _tooltipText; private set { _tooltipText = value; Notify(); } }
    public Color IconColor    { get => _iconColor;   private set { _iconColor = value;   Notify(); } }

    public void Update(WindowSummary daily, WindowSummary weekly)
    {
        DailyPercent    = daily.PercentUsed;
        WeeklyPercent   = weekly.PercentUsed;
        DailyResetText  = FormatReset(daily.ResetsAt);
        WeeklyResetText = FormatReset(weekly.ResetsAt);
        TooltipText     = $"Daily: {daily.PercentUsed:F0}% · Weekly: {weekly.PercentUsed:F0}%";
        IconColor       = ResolveIconColor(Math.Max(daily.PercentUsed, weekly.PercentUsed));
    }

    internal static Color ResolveIconColor(double worst) => worst switch
    {
        >= 100 => Color.FromArgb(139, 0, 0),
        >= 90  => Color.FromArgb(243, 139, 168),
        >= 61  => Color.FromArgb(249, 226, 175),
        _      => Color.FromArgb(166, 227, 161)
    };

    private static string FormatReset(DateTime resetsAt)
    {
        var local = resetsAt.Kind == DateTimeKind.Utc ? resetsAt.ToLocalTime() : resetsAt;
        return local.Date == DateTime.Today
            ? $"Resets at {local:h:mm tt}"
            : $"Resets {local:dddd}";
    }

    private void Notify([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test CodeMeter.Tests/CodeMeter.Tests.csproj --filter "FullyQualifiedName~TrayViewModelTests" -v minimal
```
Expected: 7 passed.

- [ ] **Step 5: Commit**

```bash
git add CodeMeter/UI/TrayViewModel.cs CodeMeter.Tests/UI/TrayViewModelTests.cs
git commit -m "feat: add TrayViewModel with icon color logic and bindings"
```

---

## Task 7: AlertService

**Files:**
- Create: `CodeMeter/Alerts/AlertService.cs`
- Create: `CodeMeter.Tests/Alerts/AlertServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `CodeMeter.Tests/Alerts/AlertServiceTests.cs`:

```csharp
using CodeMeter.Alerts;
using CodeMeter.Core;
using CodeMeter.Settings;
using FluentAssertions;

namespace CodeMeter.Tests.Alerts;

public class AlertServiceTests
{
    private static WindowSummary S(double pct, DateTime resetsAt)
        => new(0m, 10m, pct, resetsAt);

    private static AppSettings Thresholds(params int[] t)
        => new() { AlertThresholds = t.ToList() };

    [Fact]
    public void Check_FiresToast_WhenDailyThresholdFirstCrossed()
    {
        var fired = new List<string>();
        var svc = new AlertService((title, _) => fired.Add(title));
        svc.Check(S(81, DateTime.Now.AddDays(1)), S(10, DateTime.Now.AddDays(7)), Thresholds(80));
        fired.Should().ContainSingle(t => t.Contains("Daily") && t.Contains("80%"));
    }

    [Fact]
    public void Check_DoesNotFireTwice_ForSameThresholdSamePeriod()
    {
        var fired = new List<string>();
        var svc = new AlertService((title, _) => fired.Add(title));
        var daily = S(85, DateTime.Now.AddDays(1));
        svc.Check(daily, S(10, DateTime.Now.AddDays(7)), Thresholds(80));
        svc.Check(daily, S(10, DateTime.Now.AddDays(7)), Thresholds(80));
        fired.Should().HaveCount(1);
    }

    [Fact]
    public void Check_FiresAgain_AfterWindowReset()
    {
        var fired = new List<string>();
        var svc = new AlertService((title, _) => fired.Add(title));
        svc.Check(S(85, DateTime.Now.AddDays(1)), S(10, DateTime.Now.AddDays(7)), Thresholds(80));
        // New window: resetsAt has advanced
        svc.Check(S(85, DateTime.Now.AddDays(2)), S(10, DateTime.Now.AddDays(7)), Thresholds(80));
        fired.Should().HaveCount(2);
    }

    [Fact]
    public void Check_DoesNotFire_WhenBelowThreshold()
    {
        var fired = new List<string>();
        var svc = new AlertService((title, _) => fired.Add(title));
        svc.Check(S(70, DateTime.Now.AddDays(1)), S(10, DateTime.Now.AddDays(7)), Thresholds(80));
        fired.Should().BeEmpty();
    }

    [Fact]
    public void Check_FiresForWeekly_Independently()
    {
        var fired = new List<string>();
        var svc = new AlertService((title, _) => fired.Add(title));
        svc.Check(S(10, DateTime.Now.AddDays(1)), S(85, DateTime.Now.AddDays(7)), Thresholds(80));
        fired.Should().ContainSingle(t => t.Contains("Weekly") && t.Contains("80%"));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test CodeMeter.Tests/CodeMeter.Tests.csproj --filter "FullyQualifiedName~AlertServiceTests" -v minimal
```
Expected: FAIL — `AlertService` not defined.

- [ ] **Step 3: Implement AlertService**

Create `CodeMeter/Alerts/AlertService.cs`:

```csharp
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
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test CodeMeter.Tests/CodeMeter.Tests.csproj --filter "FullyQualifiedName~AlertServiceTests" -v minimal
```
Expected: 5 passed.

- [ ] **Step 5: Commit**

```bash
git add CodeMeter/Alerts/AlertService.cs CodeMeter.Tests/Alerts/AlertServiceTests.cs
git commit -m "feat: add AlertService with per-window threshold tracking"
```

---

## Task 8: PollingService

**Files:**
- Create: `CodeMeter/Core/PollingService.cs`

- [ ] **Step 1: Implement PollingService**

Create `CodeMeter/Core/PollingService.cs`:

```csharp
namespace CodeMeter.Core;

public class PollingService : IDisposable
{
    private readonly Timer _timer;
    internal readonly Func<Task> Callback;

    public PollingService(int intervalSeconds, Func<Task> callback)
    {
        Callback = callback;
        _timer = new Timer(OnTick, null, TimeSpan.Zero, TimeSpan.FromSeconds(intervalSeconds));
    }

    public void ForceRun() => Callback().GetAwaiter().GetResult();

    private void OnTick(object? _) => Callback().GetAwaiter().GetResult();

    public void Dispose() => _timer.Dispose();
}
```

- [ ] **Step 2: Build to verify**

```bash
dotnet build CodeMeter/CodeMeter.csproj
```
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add CodeMeter/Core/PollingService.cs
git commit -m "feat: add PollingService timer wrapper with ForceRun"
```

---

## Task 9: TrayPopup UI

**Files:**
- Create: `CodeMeter/UI/TrayPopup.xaml`
- Create: `CodeMeter/UI/TrayPopup.xaml.cs`

- [ ] **Step 1: Create TrayPopup.xaml**

Create `CodeMeter/UI/TrayPopup.xaml`:

```xml
<UserControl x:Class="CodeMeter.UI.TrayPopup"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Width="260">
  <UserControl.Resources>
    <Style x:Key="GradientBar" TargetType="ProgressBar">
      <Setter Property="Height" Value="12"/>
      <Setter Property="Maximum" Value="100"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="ProgressBar">
            <Grid>
              <Border Background="#313244" CornerRadius="6"/>
              <Border x:Name="PART_Indicator" HorizontalAlignment="Left" CornerRadius="6">
                <Border.Background>
                  <LinearGradientBrush StartPoint="0,0" EndPoint="1,0">
                    <GradientStop Color="#A6E3A1" Offset="0"/>
                    <GradientStop Color="#F9E2AF" Offset="0.6"/>
                    <GradientStop Color="#F38BA8" Offset="1"/>
                  </LinearGradientBrush>
                </Border.Background>
              </Border>
            </Grid>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
  </UserControl.Resources>

  <Border Background="#1E1E2E" CornerRadius="8" Padding="16">
    <StackPanel>

      <TextBlock Text="CLAUDE CODE USAGE" Foreground="#CDD6F4"
                 FontSize="11" FontWeight="SemiBold" CharacterSpacing="100"
                 Margin="0,0,0,14"/>

      <!-- Daily row -->
      <Grid Margin="0,0,0,4">
        <Grid.ColumnDefinitions>
          <ColumnDefinition Width="*"/>
          <ColumnDefinition Width="Auto"/>
        </Grid.ColumnDefinitions>
        <TextBlock Text="Daily" Foreground="#A6ADC8" FontSize="11" VerticalAlignment="Bottom"/>
        <TextBlock Grid.Column="1"
                   Text="{Binding DailyPercent, StringFormat={}{0:F0}%}"
                   Foreground="#F38BA8" FontSize="18" FontWeight="Bold"/>
      </Grid>
      <ProgressBar Style="{StaticResource GradientBar}"
                   Value="{Binding DailyPercent}" Margin="0,0,0,4"/>
      <TextBlock Text="{Binding DailyResetText}"
                 Foreground="#585B70" FontSize="10" Margin="0,0,0,14"/>

      <!-- Weekly row -->
      <Grid Margin="0,0,0,4">
        <Grid.ColumnDefinitions>
          <ColumnDefinition Width="*"/>
          <ColumnDefinition Width="Auto"/>
        </Grid.ColumnDefinitions>
        <TextBlock Text="Weekly" Foreground="#A6ADC8" FontSize="11" VerticalAlignment="Bottom"/>
        <TextBlock Grid.Column="1"
                   Text="{Binding WeeklyPercent, StringFormat={}{0:F0}%}"
                   Foreground="#A6E3A1" FontSize="18" FontWeight="Bold"/>
      </Grid>
      <ProgressBar Style="{StaticResource GradientBar}"
                   Value="{Binding WeeklyPercent}" Margin="0,0,0,4"/>
      <TextBlock Text="{Binding WeeklyResetText}"
                 Foreground="#585B70" FontSize="10" Margin="0,0,0,14"/>

      <!-- Footer -->
      <Separator Background="#313244" Margin="0,0,0,10"/>
      <Grid>
        <Grid.ColumnDefinitions>
          <ColumnDefinition Width="*"/>
          <ColumnDefinition Width="Auto"/>
        </Grid.ColumnDefinitions>
        <TextBlock x:Name="LastUpdatedText" Foreground="#585B70" FontSize="10"/>
        <TextBlock Grid.Column="1" Foreground="#89B4FA" FontSize="10"
                   Cursor="Hand" MouseLeftButtonUp="Settings_Click">⚙ Settings</TextBlock>
      </Grid>

    </StackPanel>
  </Border>
</UserControl>
```

- [ ] **Step 2: Create TrayPopup.xaml.cs**

Create `CodeMeter/UI/TrayPopup.xaml.cs`:

```csharp
using System.Windows.Controls;
using System.Windows.Input;

namespace CodeMeter.UI;

public partial class TrayPopup : UserControl
{
    public static event EventHandler? SettingsRequested;

    private DateTime _lastUpdated = DateTime.Now;

    public TrayPopup()
    {
        InitializeComponent();
    }

    public void MarkUpdated()
    {
        _lastUpdated = DateTime.Now;
        LastUpdatedText.Text = "Updated just now";
    }

    private void Settings_Click(object sender, MouseButtonEventArgs e)
        => SettingsRequested?.Invoke(this, EventArgs.Empty);
}
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build CodeMeter/CodeMeter.csproj
```
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add CodeMeter/UI/TrayPopup.xaml CodeMeter/UI/TrayPopup.xaml.cs
git commit -m "feat: add TrayPopup UI with gradient progress bars"
```

---

## Task 10: SettingsWindow UI

**Files:**
- Create: `CodeMeter/UI/SettingsWindow.xaml`
- Create: `CodeMeter/UI/SettingsWindow.xaml.cs`

- [ ] **Step 1: Create SettingsWindow.xaml**

Create `CodeMeter/UI/SettingsWindow.xaml`:

```xml
<Window x:Class="CodeMeter.UI.SettingsWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Code Meter — Settings" Height="500" Width="360"
        ResizeMode="NoResize" WindowStartupLocation="CenterScreen"
        Background="#1E1E2E" Foreground="#CDD6F4">
  <Window.Resources>
    <Style TargetType="Label">
      <Setter Property="Foreground" Value="#A6ADC8"/>
      <Setter Property="FontSize" Value="11"/>
      <Setter Property="Padding" Value="0,0,0,2"/>
    </Style>
    <Style TargetType="TextBox">
      <Setter Property="Background" Value="#313244"/>
      <Setter Property="Foreground" Value="#CDD6F4"/>
      <Setter Property="BorderBrush" Value="#45475A"/>
      <Setter Property="Padding" Value="4,3"/>
    </Style>
    <Style TargetType="ComboBox">
      <Setter Property="Background" Value="#313244"/>
      <Setter Property="Foreground" Value="#CDD6F4"/>
    </Style>
    <Style TargetType="Button">
      <Setter Property="Background" Value="#313244"/>
      <Setter Property="Foreground" Value="#CDD6F4"/>
      <Setter Property="BorderBrush" Value="#45475A"/>
      <Setter Property="Padding" Value="10,4"/>
      <Setter Property="Cursor" Value="Hand"/>
    </Style>
  </Window.Resources>

  <StackPanel Margin="16">

    <TextBlock Text="LIMITS" FontWeight="SemiBold" FontSize="11"
               CharacterSpacing="100" Foreground="#CDD6F4" Margin="0,0,0,8"/>
    <Grid Margin="0,0,0,12">
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="8"/>
        <ColumnDefinition Width="*"/>
      </Grid.ColumnDefinitions>
      <StackPanel Grid.Column="0">
        <Label Content="Daily limit (USD)"/>
        <TextBox x:Name="DailyLimitBox"/>
      </StackPanel>
      <StackPanel Grid.Column="2">
        <Label Content="Weekly limit (USD)"/>
        <TextBox x:Name="WeeklyLimitBox"/>
      </StackPanel>
    </Grid>

    <TextBlock Text="RESET SCHEDULE" FontWeight="SemiBold" FontSize="11"
               CharacterSpacing="100" Foreground="#CDD6F4" Margin="0,0,0,8"/>
    <Grid Margin="0,0,0,8">
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="8"/>
        <ColumnDefinition Width="*"/>
      </Grid.ColumnDefinitions>
      <StackPanel Grid.Column="0">
        <Label Content="Daily reset hour"/>
        <ComboBox x:Name="DailyResetHourBox"/>
      </StackPanel>
      <StackPanel Grid.Column="2">
        <Label Content="Poll interval (sec, min 10)"/>
        <TextBox x:Name="PollIntervalBox"/>
      </StackPanel>
    </Grid>
    <Grid Margin="0,0,0,12">
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="8"/>
        <ColumnDefinition Width="*"/>
      </Grid.ColumnDefinitions>
      <StackPanel Grid.Column="0">
        <Label Content="Weekly reset day"/>
        <ComboBox x:Name="WeeklyResetDayBox"/>
      </StackPanel>
      <StackPanel Grid.Column="2">
        <Label Content="Weekly reset hour"/>
        <ComboBox x:Name="WeeklyResetHourBox"/>
      </StackPanel>
    </Grid>

    <TextBlock Text="ALERT THRESHOLDS (%)" FontWeight="SemiBold" FontSize="11"
               CharacterSpacing="100" Foreground="#CDD6F4" Margin="0,0,0,8"/>
    <Border Background="#313244" CornerRadius="4" Padding="8" Margin="0,0,0,8" MaxHeight="140">
      <ScrollViewer VerticalScrollBarVisibility="Auto">
        <StackPanel x:Name="ThresholdsPanel"/>
      </ScrollViewer>
    </Border>
    <Button Content="+ Add Threshold" HorizontalAlignment="Left"
            Click="AddThreshold_Click" Margin="0,0,0,16"/>

    <Separator Background="#45475A" Margin="0,0,0,12"/>
    <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
      <Button Content="Cancel" Click="Cancel_Click" Margin="0,0,8,0"/>
      <Button Content="Save" Click="Save_Click"
              Background="#89B4FA" Foreground="#1E1E2E" FontWeight="SemiBold"/>
    </StackPanel>

  </StackPanel>
</Window>
```

- [ ] **Step 2: Create SettingsWindow.xaml.cs**

Create `CodeMeter/UI/SettingsWindow.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CodeMeter.Settings;

namespace CodeMeter.UI;

public partial class SettingsWindow : Window
{
    private readonly SettingsStore _store;

    public SettingsWindow(SettingsStore store)
    {
        InitializeComponent();
        _store = store;
        PopulateHours(DailyResetHourBox);
        PopulateHours(WeeklyResetHourBox);
        foreach (var day in Enum.GetValues<DayOfWeek>())
            WeeklyResetDayBox.Items.Add(day.ToString());

        var s = store.Load();
        DailyLimitBox.Text = s.DailyLimitUSD.ToString("F2");
        WeeklyLimitBox.Text = s.WeeklyLimitUSD.ToString("F2");
        PollIntervalBox.Text = s.PollIntervalSeconds.ToString();
        DailyResetHourBox.SelectedIndex = s.DailyResetHour;
        WeeklyResetHourBox.SelectedIndex = s.WeeklyResetHour;
        WeeklyResetDayBox.SelectedIndex = (int)s.WeeklyResetDay;
        foreach (var t in s.AlertThresholds)
            AddThresholdRow(t);
    }

    private void AddThresholdRow(int value = 75)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var box = new TextBox
        {
            Text = value.ToString(),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = new SolidColorBrush(Color.FromRgb(205, 214, 244))
        };
        var btn = new Button
        {
            Content = "✕",
            Padding = new Thickness(4, 0, 4, 0),
            Margin = new Thickness(4, 0, 0, 0)
        };
        btn.Click += (_, _) => ThresholdsPanel.Children.Remove(row);

        Grid.SetColumn(btn, 1);
        row.Children.Add(box);
        row.Children.Add(btn);
        ThresholdsPanel.Children.Add(row);
    }

    private void AddThreshold_Click(object sender, RoutedEventArgs e) => AddThresholdRow();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(DailyLimitBox.Text, out var daily) || daily <= 0)
            { Err("Daily limit must be a positive number."); return; }
        if (!decimal.TryParse(WeeklyLimitBox.Text, out var weekly) || weekly <= 0)
            { Err("Weekly limit must be a positive number."); return; }
        if (!int.TryParse(PollIntervalBox.Text, out var poll) || poll < 10)
            { Err("Poll interval must be at least 10 seconds."); return; }

        var thresholds = new List<int>();
        foreach (Grid row in ThresholdsPanel.Children)
        {
            var box = (TextBox)row.Children[0];
            if (!int.TryParse(box.Text, out var t) || t < 1 || t > 100)
                { Err($"Threshold '{box.Text}' must be 1–100."); return; }
            thresholds.Add(t);
        }

        _store.Save(new AppSettings
        {
            DailyLimitUSD     = daily,
            WeeklyLimitUSD    = weekly,
            DailyResetHour    = DailyResetHourBox.SelectedIndex,
            WeeklyResetHour   = WeeklyResetHourBox.SelectedIndex,
            WeeklyResetDay    = (DayOfWeek)WeeklyResetDayBox.SelectedIndex,
            PollIntervalSeconds = poll,
            AlertThresholds   = thresholds.Distinct().OrderBy(t => t).ToList()
        });

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

    private void Err(string msg) =>
        MessageBox.Show(msg, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);

    private static void PopulateHours(ComboBox cb)
    {
        for (int h = 0; h < 24; h++)
            cb.Items.Add($"{h:D2}:00");
    }
}
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build CodeMeter/CodeMeter.csproj
```
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add CodeMeter/UI/SettingsWindow.xaml CodeMeter/UI/SettingsWindow.xaml.cs
git commit -m "feat: add SettingsWindow with dynamic threshold list"
```

---

## Task 11: App Wiring

**Files:**
- Modify: `CodeMeter/App.xaml`
- Modify: `CodeMeter/App.xaml.cs`

- [ ] **Step 1: Update App.xaml**

Replace `CodeMeter/App.xaml`:

```xml
<Application x:Class="CodeMeter.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnExplicitShutdown">
  <Application.Resources/>
</Application>
```

- [ ] **Step 2: Wire all services in App.xaml.cs**

Replace `CodeMeter/App.xaml.cs`:

```csharp
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows;
using System.Windows.Threading;
using CodeMeter.Alerts;
using CodeMeter.Core;
using CodeMeter.Settings;
using CodeMeter.UI;
using H.NotifyIcon;
using Microsoft.Toolkit.Uwp.Notifications;

namespace CodeMeter;

public partial class App : Application
{
    private TaskbarIcon? _tray;
    private TrayViewModel? _vm;
    private TrayPopup? _popup;
    private PollingService? _poller;
    private Func<Task>? _pollCallback;
    private SettingsStore? _store;
    private AlertService? _alerts;
    private DispatcherTimer? _pulseTimer;
    private bool _pulseOn;

    private static string SettingsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "CodeMeter", "settings.json");

    private static string ClaudeDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                     ".claude", "projects");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _store = new SettingsStore(SettingsPath);
        if (!_store.Exists())
            new SettingsWindow(_store).ShowDialog();

        var reader = new UsageReader();
        var agg    = new UsageAggregator();
        _vm        = new TrayViewModel();
        _popup     = new TrayPopup { DataContext = _vm };
        _alerts    = new AlertService(ShowToast);

        _tray = new TaskbarIcon
        {
            ToolTipText      = "Claude Code Usage",
            PopupActivation  = PopupActivationMode.LeftClick,
            MenuActivation   = PopupActivationMode.RightClick,
            TrayPopup        = _popup,
            ContextMenu      = BuildContextMenu()
        };

        TrayPopup.SettingsRequested += (_, _) => OpenSettings();

        _pollCallback = async () =>
        {
            var settings = _store.Load();
            var entries  = reader.ReadAll(ClaudeDir).ToList();
            var now      = DateTime.Now;
            var daily    = agg.ComputeDaily(entries, settings, now);
            var weekly   = agg.ComputeWeekly(entries, settings, now);

            await Dispatcher.InvokeAsync(() =>
            {
                _vm.Update(daily, weekly);
                _popup.MarkUpdated();
                RefreshIcon(_vm.IconColor);
            });

            _alerts.Check(daily, weekly, settings);
        };

        var settings = _store.Load();
        _poller = new PollingService(settings.PollIntervalSeconds, _pollCallback);
    }

    private void RefreshIcon(Color color)
    {
        if (color == Color.FromArgb(139, 0, 0))
        {
            EnsurePulse();
            return;
        }
        StopPulse();
        SetIcon(color);
    }

    private void EnsurePulse()
    {
        if (_pulseTimer is { IsEnabled: true }) return;
        _pulseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _pulseTimer.Tick += (_, _) =>
        {
            _pulseOn = !_pulseOn;
            SetIcon(_pulseOn ? Color.FromArgb(139, 0, 0) : Color.FromArgb(40, 0, 0));
        };
        _pulseTimer.Start();
    }

    private void StopPulse()
    {
        _pulseTimer?.Stop();
        _pulseTimer = null;
    }

    private void SetIcon(Color color)
    {
        using var bmp = new Bitmap(16, 16);
        using var g   = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        using var brush = new SolidBrush(color);
        g.FillEllipse(brush, 1, 1, 14, 14);
        _tray!.Icon = Icon.FromHandle(bmp.GetHicon());
    }

    private ContextMenu BuildContextMenu()
    {
        var cm       = new System.Windows.Controls.ContextMenu();
        var settings = new System.Windows.Controls.MenuItem { Header = "Settings" };
        var refresh  = new System.Windows.Controls.MenuItem { Header = "Refresh Now" };
        var exit     = new System.Windows.Controls.MenuItem { Header = "Exit" };

        settings.Click += (_, _) => OpenSettings();
        refresh.Click  += (_, _) => _poller?.ForceRun();
        exit.Click     += (_, _) => { StopPulse(); _poller?.Dispose(); _tray?.Dispose(); Shutdown(); };

        cm.Items.Add(settings);
        cm.Items.Add(refresh);
        cm.Items.Add(new System.Windows.Controls.Separator());
        cm.Items.Add(exit);
        return cm;
    }

    private void OpenSettings()
    {
        new SettingsWindow(_store!).ShowDialog();
        _poller?.Dispose();
        var settings = _store!.Load();
        _poller = new PollingService(settings.PollIntervalSeconds, _pollCallback!);
    }

    private static void ShowToast(string title, string body)
    {
        try
        {
            new ToastContentBuilder()
                .AddText(title)
                .AddText(body)
                .Show();
        }
        catch { /* toasts not available in this environment */ }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        StopPulse();
        _poller?.Dispose();
        _tray?.Dispose();
        base.OnExit(e);
    }
}
```

- [ ] **Step 3: Build the full solution**

```bash
dotnet build CodeMeter.sln
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Run all tests**

```bash
dotnet test CodeMeter.Tests/CodeMeter.Tests.csproj -v minimal
```
Expected: All tests pass (UsageReaderTests ×6, UsageAggregatorTests ×7, AlertServiceTests ×5, SettingsStoreTests ×5, TrayViewModelTests ×7 = 30 tests).

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat: wire all services in App — tray app complete"
```
