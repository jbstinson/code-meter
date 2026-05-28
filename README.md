# Code Meter

> A lightweight Windows system tray app that tracks your [Claude Code](https://claude.ai/code) subscription usage in real time — no API key required.

![Claude Code Usage popup showing 5-hour usage at 37%](https://raw.githubusercontent.com/jbstinson/code-meter/trunk/docs/screenshots/popup.png)

---

## What is it?

Claude Code enforces a **5-hour rolling window** spending cap. Once you hit it, you're rate-limited until the window clears. Code Meter reads Claude Code's local usage logs directly from your machine and shows you exactly where you stand — before you hit the wall mid-task.

- Lives in the system tray: zero clutter, always one double-click away
- Reads `~/.claude/projects/**/*.jsonl` — no network calls, no API key
- Color-coded tray icon changes as you approach your limit
- Configurable toast notifications at thresholds you set

---

## Features

- **5-hour rolling window meter** — gradient progress bar updates on a configurable poll interval
- **Reset countdown** — shows exactly how long until the current session window clears
- **Color-coded tray icon** — green → yellow → red → pulsing dark red as usage climbs
- **Toast alerts** — Windows notifications fire the first time you cross each threshold
- **Configurable token budget** — adjust the 5-hour budget to match your plan if the default drifts
- **First-run setup** — settings window opens automatically when no config is found
- **Offline & private** — reads local log files only, never touches the network

---

## Screenshots

### Tray Popup

Double-click the tray icon to see your current 5-hour usage at a glance.

![Popup with gradient bar for 5-hour usage and reset countdown](https://raw.githubusercontent.com/jbstinson/code-meter/trunk/docs/screenshots/popup.png)

### Settings Window

Right-click the tray icon → **Settings**, or click **⚙ Settings** in the popup.

![Settings window showing token budget, poll interval, and alert thresholds](https://raw.githubusercontent.com/jbstinson/code-meter/trunk/docs/screenshots/settings.png)

### Tray Icon States

The tray icon color reflects your 5-hour window usage.

![Four tray icon states: green 0-60%, yellow 61-89%, red 90-99%, pulsing dark red 100%](https://raw.githubusercontent.com/jbstinson/code-meter/trunk/docs/screenshots/tray-states.png)

### Toast Notifications

Windows toast notifications fire once per threshold crossing per window period.

![Toast notification showing Claude Code 5-hour limit at 80%](https://raw.githubusercontent.com/jbstinson/code-meter/trunk/docs/screenshots/toast.png)

---

## Prerequisites

| Requirement | Version |
|---|---|
| Windows | 10 or 11 |
| [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | 8.0+ |
| [Claude Code](https://claude.ai/code) | Any (Max or Pro subscription) |

> **Note:** Code Meter reads Claude Code's local usage logs from `~/.claude/projects/`. You must have Claude Code installed and have used it at least once for any usage to appear.

---

## Installation

### Option A — Clone and run

```bash
git clone https://github.com/jbstinson/code-meter.git
cd code-meter
dotnet run --project CodeMeter/CodeMeter.csproj
```

### Option B — Build a standalone executable

```bash
git clone https://github.com/jbstinson/code-meter.git
cd code-meter
dotnet publish CodeMeter/CodeMeter.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

The output will be a single `CodeMeter.exe` in the `publish/` folder. Double-click to run.

---

## First Run

On first launch, the **Settings** window opens automatically so you can configure things before polling starts.

Fill in:
- **5-hour token budget** — the capacity of your rolling window (default: 1,466,667 output-token-equivalents). Adjust this if Code Meter's percentage drifts from what Claude Code shows.
- **Poll interval** — how often to re-read the logs (default: 30 seconds, minimum: 10)
- **Alert thresholds** — percentages at which you want a toast notification

Click **Save**. The app minimizes to the system tray and starts polling immediately.

---

## Usage

| Action | Result |
|---|---|
| **Double-click** tray icon | Opens the usage popup |
| **Right-click** tray icon | Opens context menu |
| Context menu → **Settings** | Opens the settings window |
| Context menu → **Refresh Now** | Forces an immediate log re-read |
| Context menu → **Exit** | Quits the app |
| Click **⚙ Settings** in popup | Opens the settings window |

---

## How It Works

```
~/.claude/projects/**/*.jsonl
          │
          ▼
    UsageReader          reads every JSONL file, filters assistant messages,
          │              computes weighted token score per entry
          ▼
   UsageAggregator       sums entries within the 5-hour rolling window,
          │              detects session gaps to compute the reset timer
          ▼
    TrayViewModel        updates the progress bar, reset countdown, and icon color
          │
       ┌──┴──┐
       ▼     ▼
  UsageWindow TrayIcon   renders the popup + colors the 16×16 GDI+ icon
              │
              ▼
       AlertService      fires Windows toasts on first threshold crossing per window
```

Claude Code writes one JSONL file per conversation to `~/.claude/projects/<project-hash>/<conversation-id>.jsonl`. Each assistant message includes token counts broken down by type (input, output, cache write, cache read). Code Meter scores each entry using empirically-calibrated token weights — no dollar amounts, no API key.

### Token Scoring

Rather than dollar costs, Code Meter uses **output-token-equivalents**: every token type is weighted relative to one Sonnet output token = 1.0. The weights are derived from Anthropic's API price ratios, with cache_read empirically calibrated against Claude Code's own percentage display:

| Token type | Sonnet weight (per 1M tokens) |
|---|---|
| Output | 1,000,000 |
| Input | 200,000 |
| Cache write | 250,000 |
| Cache read | 9,500 |

The default budget of **1,466,667** output-token-equivalents corresponds to the observed Claude Code Pro/Max 5-hour session limit. If your percentage consistently differs from what Claude Code shows, adjust the budget in Settings.

---

## Configuration

Settings are stored at `%APPDATA%\CodeMeter\settings.json` and can be edited directly:

```json
{
  "fiveHourTokenBudget": 1466667,
  "alertThresholds": [60, 80, 95],
  "pollIntervalSeconds": 30
}
```

---

## Tray Icon Color Reference

| Color | Usage band | Meaning |
|---|---|---|
| 🟢 Green | 0 – 60% | Well within your limit |
| 🟡 Yellow | 61 – 89% | Getting close — keep an eye on it |
| 🔴 Red | 90 – 99% | Almost at your limit |
| ◉ Pulsing dark red | 100% | Limit reached |

---

## Alert Thresholds

Thresholds fire **once per window period** — you won't get spammed on every poll. The fired state resets automatically when the 5-hour window clears.

You can add as many thresholds as you like from the Settings window. Duplicates are silently ignored. The default set is `[60, 80, 95]`.

---

## Auto-Start on Login (optional)

Code Meter doesn't register itself to run on startup. To add it manually:

1. Press `Win + R` → type `shell:startup` → press Enter
2. Create a shortcut to `CodeMeter.exe` in the folder that opens

---

## Project Structure

```
CodeMeter/
├── App.xaml / App.xaml.cs          Entry point — wires all services, owns the tray icon
├── Core/
│   ├── UsageEntry.cs               Record: Timestamp + WeightedTokens
│   ├── WindowSummary.cs            Record: AmountUsed, Limit, PercentUsed, ResetsAt
│   ├── UsageReader.cs              Reads ~/.claude/projects/**/*.jsonl; scores tokens
│   ├── UsageAggregator.cs          Computes 5-hour rolling WindowSummary
│   └── PollingService.cs           System.Threading.Timer wrapper
├── Alerts/
│   └── AlertService.cs             Per-threshold tracking + toast dispatch
├── Settings/
│   ├── AppSettings.cs              Settings POCO — budget, thresholds, poll interval
│   └── SettingsStore.cs            Atomic JSON read/write to %APPDATA%\CodeMeter\
└── UI/
    ├── TrayViewModel.cs            INotifyPropertyChanged VM — percent, reset text, icon color
    ├── UsageWindow.xaml / .cs      Double-click popup with gradient progress bar
    └── SettingsWindow.xaml / .cs   Settings window with budget field and threshold list

CodeMeter.Tests/
├── Core/                           UsageReader, UsageAggregator tests
├── Alerts/                         AlertService tests
├── Settings/                       SettingsStore tests
└── UI/                             TrayViewModel tests
```

---

## Running Tests

```bash
dotnet test CodeMeter.Tests/CodeMeter.Tests.csproj -v minimal
```

Expected output: **44 tests, 0 failures**.

---

## Built With

| Package | Purpose |
|---|---|
| [H.NotifyIcon.Wpf](https://github.com/HavenDV/H.NotifyIcon) | System tray icon + popup hosting |
| [Microsoft.Toolkit.Uwp.Notifications](https://github.com/CommunityToolkit/WindowsCommunityToolkit) | Windows toast notifications |
| System.Text.Json | Settings serialization (inbox in .NET 8) |
| xUnit + FluentAssertions | Tests |

---

## License

MIT
