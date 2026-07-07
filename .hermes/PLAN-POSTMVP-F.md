# Post-MVP Phase F: Voice Reminders / Pomodoro Timer

**Date:** 2026-07-07
**Status:** autonomous execution
**Goal:** Mochi acts as a Pomodoro timer with voice-like reminders (meow sounds + speech bubbles + tray notifications). Cat behavior reflects timer state (focused → calm idle, break → playful).

**PRD ref:** §5 Out-of-scope → now in-scope: "Voice reminders / pomodoro"

## Architecture

```
PomodoroService (Core/Services/) — timer state machine: Focus → ShortBreak → Focus → ... → LongBreak
    ↓ EventBus events
App.xaml.cs — subscribes to PomodoroEvent, updates cat behavior, shows notifications
    ↓
TrayIconController — menu items: Start/Pause/Reset Pomodoro, settings
    ↓
NotificationService (Core/Services/) — tray balloon + optional speech bubble overlay
```

### New Types

| Type | File | Purpose |
|------|------|---------|
| `PomodoroService` | Core/Services/PomodoroService.cs | Timer state machine: Focus(25min)→ShortBreak(5min)→repeat 4→LongBreak(15min). Events via EventBus. |
| `PomodoroState` | Core/Models/PomodoroState.cs | Enum: Idle, Focus, ShortBreak, LongBreak, Paused |
| `PomodoroSettings` | Core/Models/PomodoroSettings.cs | Config: focusDuration, shortBreakDuration, longBreakDuration, roundsBeforeLongBreak |
| `PomodoroEvent` | Core/Events/PomodoroEvent.cs | EventBus event: state, elapsed, remaining, round |
| `SpeechBubbleService` | Core/Services/SpeechBubbleService.cs | Shows text bubble above cat for reminders ("Focus time!", "Break time!") |
| `SpeechBubbleWindow` | UI/SpeechBubble/SpeechBubbleWindow.xaml | Transparent WPF window showing text near cat |

### Modified Types

| Type | Changes |
|------|---------|
| `TrayIconController` | Add Pomodoro submenu: Start, Pause, Reset, Settings |
| `App.xaml.cs` | Wire PomodoroService, subscribe events, update cat behavior on state change |
| `Program.cs` | Register PomodoroService + SpeechBubbleService in DI |
| `SaveData` | Add pomodoro settings persistence |

## Tasks

| Task | Description | Files | Acceptance | Status |
|------|-------------|-------|------------|--------|
| F-01 | `PomodoroState` enum + `PomodoroSettings` model | Core/Models/PomodoroState.cs, Core/Models/PomodoroSettings.cs | Enum: Idle, Focus, ShortBreak, LongBreak, Paused. Settings: FocusMin=25, ShortBreakMin=5, LongBreakMin=15, Rounds=4. | todo |
| F-02 | `PomodoroEvent` for EventBus | Core/Events/PomodoroEvent.cs | Event with State, ElapsedSeconds, RemainingSeconds, RoundNumber. | todo |
| F-03 | `PomodoroService` timer state machine | Core/Services/PomodoroService.cs | Start/Pause/Reset. Auto-transitions Focus→Break→Focus. Tracks rounds. Fires PomodoroEvent on state change + tick. Uses ITimeProvider for testability. | todo |
| F-04 | `SpeechBubbleService` text reminders | Core/Services/SpeechBubbleService.cs | Show(text, duration) — displays text above cat position for N seconds. Auto-hides. | todo |
| F-05 | `SpeechBubbleWindow` WPF transparent window | UI/SpeechBubble/SpeechBubbleWindow.xaml + .cs | Transparent tool window, text label, positioned near cat, auto-size, fades out. | todo |
| F-06 | Tray menu Pomodoro items | UI/Tray/TrayIconController.cs | Submenu: Start/Pause/Reset Pomodoro. Shows current state + remaining time in tooltip. | todo |
| F-07 | Wire PomodoroService into App.xaml.cs | App/App.xaml.cs | DI resolve, subscribe PomodoroEvent. On Focus: cat calm idle, suppress playful. On Break: cat playful. On complete: meow + speech bubble + tray notification. | todo |
| F-08 | Register in DI (Program.cs) | App/Program.cs | AddSingleton PomodoroService, SpeechBubbleService. | todo |
| F-09 | Pomodoro settings persistence | Core/Models/SaveData.cs, Core/Infrastructure/Storage/SaveManager.cs | Save/load pomodoro durations in save.json. | todo |
| F-10 | Unit tests | tests-core/PomodoroTests.cs | Timer transitions, pause/resume, round counting, event firing. ≥15 tests. Mock ITimeProvider. | todo |
| F-11 | Compile + all tests pass | — | `dotnet build` 0 errors. All 246+ existing + new tests pass. Commit. | todo |

## Execution Order

1. F-01 — Models (no deps)
2. F-02 — Event (no deps)
3. F-03 — PomodoroService (depends F-01, F-02)
4. F-04 — SpeechBubbleService (depends F-02)
5. F-05 — SpeechBubbleWindow (depends F-04)
6. F-06 — Tray menu (depends F-03)
7. F-08 — DI registration (depends F-03, F-04)
8. F-07 — App wiring (depends F-03, F-04, F-06, F-08)
9. F-09 — Persistence (depends F-01)
10. F-10 — Tests (depends F-01..F-03)
11. F-11 — Compile + test + commit

## Risks

- **R-F1: Speech bubble window on Linux.** Mitigation: WPF window behind #if WINDOWS, service interface in Core.
- **R-F2: Timer drift.** Mitigation: use ITimeProvider (already exists), delta-based timing.
- **R-F3: NAudio for voice.** Mitigation: no actual voice synthesis — use meow sounds + text bubbles. Future: System.Speech.

## Design Decisions

- **D-F1: No actual TTS voice** — MVP uses meow sounds + text speech bubbles. Real TTS is future enhancement.
- **D-F2: Standard Pomodoro** — 25/5/15/4 default, configurable in settings.
- **D-F3: Cat behavior reflects timer** — Focus = calm, Break = playful. Adds immersion.