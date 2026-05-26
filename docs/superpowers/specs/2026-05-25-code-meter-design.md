# Code Meter — Design Spec
_Date: 2026-05-25_

## Overview

A Windows system tray app built in C# / .NET 8 WPF that tracks Claude Code subscription usage against user-configured daily and weekly limits. The app reads Claude Code's local JSONL usage logs — no API key or network calls required — and shows live usage meters with color-coded alerts.

---

## Goals

- Show current daily and weekly Claude Code usage (in USD) against configurable limits
- Warn proactively as limits approach via tray icon color changes and Windows toast notifications
- Stay out of the way: lives in the system tray, opens a popup on click, never steals focus

## Non-Goals

- No cloud sync, no account login, no API key
- Not a replacement for the Claude Console billing page — a quick glance tool only
- No usage history charts or export features (not in scope)

---

## Architecture

Seven focused units with single responsibilities:

| Unit | Responsibility |
|---|---|
| `UsageReader` | Walks `~/.claude/projects/**/*.jsonl`, parses and emits `UsageEntry` records |
| `UsageAggregator` | Filters entries into daily and weekly windows, returns `WindowSummary` per window |
| `SettingsStore` | Reads/writes `%APPDATA%\CodeMeter\settings.json` |
| `TrayViewModel` | `INotifyPropertyChanged` VM — drives tray icon color, tooltip, and popup data binding |
| `TrayPopup.xaml` | WPF `UserControl` rendered as the tray popup via Hardcodet.NotifyIcon.Wpf |
| `AlertService` | Tracks threshold crossings per window period, fires Windows toast notifications |
| `PollingService` | `System.Threading.Timer` — fires every N seconds (configurable), triggers read → aggregate → update cycle |

**App entry point:** `App.xaml.cs` with `ShutdownMode="OnExplicitShutdown"`. No main window. The tray icon is the entire UI surface.

**Data flow:**
1. `PollingService` fires on interval
2. `UsageReader` reads all JSONL files
3. `UsageAggregator` computes daily and weekly `WindowSummary`
4. `TrayViewModel` updates — icon color, tooltip, popup bindings
5. `AlertService` checks thresholds against new summaries, fires toasts for any newly crossed thresholds

---

## Data Layer

### JSONL Source

Claude Code writes one JSONL file per conversation to:
```
~/.claude/projects/<project-hash>/<conversation-id>.jsonl
```

Each line is a usage event. Only lines where `type == "assistant"` and `costUSD > 0` are used:

```json
{
  "type": "assistant",
  "timestamp": "2026-05-25T14:32:00.000Z",
  "costUSD": 0.0523,
  "message": {
    "usage": {
      "input_tokens": 1234,
      "output_tokens": 567,
      "cache_read_input_tokens": 890,
      "cache_creation_input_tokens": 123
    }
  }
}
```

`UsageReader` emits `UsageEntry { DateTime Timestamp, decimal CostUSD }` records. Token counts are parsed but not surfaced in the UI in v1.

### Aggregation Windows

`UsageAggregator` computes the start of each window from settings, then sums `CostUSD` for all entries within that window:

- **Daily window start:** today at `settings.DailyResetHour:00` (or yesterday if current time is before that hour)
- **Weekly window start:** most recent occurrence of `settings.WeeklyResetDay` at `settings.WeeklyResetHour:00`

Returns `WindowSummary { decimal AmountUsed, decimal Limit, double PercentUsed, DateTime ResetsAt }`.

---

## UI

### Tray Icon

Icon is a small circular meter rendered as a bitmap, colored based on the **worst** of the two window percentages:

| Usage | Color |
|---|---|
| 0–60% | Green |
| 61–89% | Yellow |
| 90–99% | Red |
| 100% | Dark red (pulsing animation) |

Tooltip (on hover): `"Daily: 64% · Weekly: 35%"`

Right-click context menu: **Settings**, **Refresh Now**, **Exit**.

### Popup Panel

Opened on left-click of the tray icon. WPF `UserControl` hosted via `Hardcodet.NotifyIcon.Wpf`'s popup mechanism. Auto-dismisses on click-away.

Layout (Option C — Rich Bars):

```
┌─────────────────────────────┐
│  CLAUDE CODE USAGE          │
│                             │
│  Daily                  64% │
│  ████████████░░░░░░░░░░     │  ← gradient bar (green→yellow→red)
│  Resets at 11:00 PM         │
│                             │
│  Weekly                 35% │
│  ██████░░░░░░░░░░░░░░░░░░   │
│  Resets Monday              │
│                             │
│  Updated 12s ago      ⚙ Settings │
└─────────────────────────────┘
```

Gradient bar fills left-to-right: green → yellow → red, clipped to `PercentUsed`. The fill endpoint color matches the current usage band. Dollar amounts are not displayed — only percentage and next reset time.

### Settings Window

Standard WPF window, opens from tray context menu or popup ⚙ link. **Auto-opens on first run** (no settings file found).

Fields:
- Daily limit (USD, decimal input)
- Weekly limit (USD, decimal input)
- Daily reset hour (0–23, combo box)
- Weekly reset day (Mon–Sun, combo box) + hour (0–23)
- Poll interval (seconds, integer input, min 10)
- Alert thresholds: variable-length list of percentage values
  - Each row: numeric input (1–100) + ✕ remove button
  - **+ Add threshold** button appends a new row
  - Duplicates are silently deduplicated on save

Default values on first run:
```json
{
  "dailyLimitUSD": 5.00,
  "weeklyLimitUSD": 35.00,
  "dailyResetHour": 0,
  "weeklyResetDay": "Monday",
  "weeklyResetHour": 0,
  "alertThresholds": [60, 80, 95],
  "pollIntervalSeconds": 30
}
```

---

## Alert System

`AlertService` maintains a set of already-fired thresholds per window per period. On each poll cycle:

1. For each window (daily, weekly): compute `PercentUsed`
2. For each configured threshold: if `PercentUsed >= threshold` and not yet fired this period → fire toast, mark as fired
3. On window reset (detected by comparing `ResetsAt` to previous cycle): clear fired-threshold set for that window

**Toast format:**
> **Claude Code — Daily limit at 80%**
> Resets at 12:00 AM

Toast notifications use `Microsoft.Toolkit.Uwp.Notifications` (Windows 10+).

---

## Settings Persistence

File: `%APPDATA%\CodeMeter\settings.json`

Serialized with `System.Text.Json`. Written atomically (write to `.tmp`, then rename) to avoid corruption on crash. Read on startup; re-read if file modification timestamp changes between polls (supports external edits).

---

## Dependencies

| Package | Purpose |
|---|---|
| `Hardcodet.NotifyIcon.Wpf` | System tray icon + popup hosting |
| `Microsoft.Toolkit.Uwp.Notifications` | Windows toast notifications |
| `System.Text.Json` | Settings serialization (inbox in .NET 8) |

---

## Project Structure

```
CodeMeter/
├── App.xaml / App.xaml.cs
├── Core/
│   ├── UsageEntry.cs
│   ├── WindowSummary.cs
│   ├── UsageReader.cs
│   ├── UsageAggregator.cs
│   └── PollingService.cs
├── Alerts/
│   └── AlertService.cs
├── Settings/
│   ├── AppSettings.cs
│   └── SettingsStore.cs
├── UI/
│   ├── TrayViewModel.cs
│   ├── TrayPopup.xaml / .cs
│   └── SettingsWindow.xaml / .cs
└── Resources/
    └── icons/  (green/yellow/red/darkred tray icons)
```

---

## Out of Scope (v1)

- Usage history / charts
- Multiple Claude accounts
- macOS / Linux support
- Auto-start on Windows login (user can add manually via Task Scheduler or Startup folder)
