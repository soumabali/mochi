# MochiV2 Phase A-D Plan

**Date:** 2026-07-07
**Status:** autonomous execution
**Goal:** Complete ALL remaining MVP features from PRD §5

## Phase A: Visual Polish (A-01..A-08)

| Task | Description | Files | Done |
|------|-------------|-------|------|
| A-01 | Wire particle emit triggers: hearts on feed/pet, Zzz on sleep, dust on landing, "!" on surprised | App.xaml.cs OnStateChanged + OnMouseLeftUp + feeding/sleep handlers | ☐ |
| A-02 | Position particles at cat sprite location (not 0,0) | MochiRenderer.cs Draw() — set particle origin to cat center | ☐ |
| A-03 | Apply micro-motion breathing scale to sprite in renderer | MochiRenderer.cs — use MicroMotion.CurrentBreathingScaleY() as actual scale transform | ☐ |
| A-04 | Apply fidget offsets (head bob, sway) to sprite position | MochiRenderer.cs — apply small XY offset from MicroMotionService | ☐ |
| A-05 | Squash & stretch on landing after Fall state | MochiRenderer.cs — when FSM=Fall→Idle, animate 10% compress + 80ms overshoot | ☐ |
| A-06 | Night mode tint overlay (22:00-06:00 cool blue) | MochiRenderer.cs Draw() — if NightModeService.IsActive, draw semi-transparent blue rect | ☐ |
| A-07 | Verify sound playback works (OGG + NAudio on Windows) | App.xaml.cs OnStateChanged → AudioManager.Play(state) — ensure OGG files load | ☐ |
| A-08 | Sad tint filter (15% desaturation + 0.8x speed when mood=Sad) | MochiRenderer.cs — if mood=Sad, apply SKColorFilter.CreateDesaturate | ☐ |

## Phase B: Interaction Depth (B-01..B-08)

| Task | Description | Files | Done |
|------|-------------|-------|------|
| B-01 | Hover 3s near cat → petting → CatPettedEvent + hearts | App.xaml.cs UpdateInteractionMode — track hover timer, after 3s emit hearts + pet sound | ☐ |
| B-02 | Double-click on cat → Playful state | App.xaml.cs — track click count + timing, double-click → FSM.Playful | ☐ |
| B-03 | Fast cursor >1500px/s → 20% chance Surprised | App.xaml.cs OnMouseMove — calculate velocity, if >1500 → 20% Surprised + "!" particle | ☐ |
| B-04 | Cursor idle 30s → cat walks toward cursor position | App.xaml.cs — track last mouse move time, after 30s idle set wander target to cursor | ☐ |
| B-05 | Typing fast >120 keys/min for 2min → cat sleeps | TypingRateService already runs — wire to FSM: when TypingBurstStartedEvent → FSM.Sleeping | ☐ |
| B-06 | Stop typing 5min → wake + meow | Wire TypingBurstEndedEvent → FSM.WakeUp + AudioManager.Play(MeowLeft) | ☐ |
| B-07 | Fullscreen app detected → hide cat (overlay.Hide()) | App.xaml.cs OnRendering — poll FullscreenDetector.IsForegroundFullscreen() every 2s | ☐ |
| B-08 | Fullscreen exit → show cat again + Surprised | When fullscreen exits → overlay.Show() + FSM.Surprised | ☐ |

## Phase C: UI Completion (C-01..C-08)

| Task | Description | Files | Done |
|------|-------------|-------|------|
| C-01 | Wire Settings window open from tray + context menu | App.xaml.cs — settings menu item → new SettingsWindow().Show() | ☐ |
| C-02 | Settings save/load via SaveManager | SettingsWindow.xaml.cs — save button → SaveManager.NotifyChanged with settings values | ☐ |
| C-03 | Stats popup: food/energy/happiness/level mini display | New StatsPopup.xaml — borderless popup near cat showing needs bars | ☐ |
| C-04 | Stats popup triggered from tray/context menu "Stats" | App.xaml.cs — stats menu → show StatsPopup | ☐ |
| C-05 | Needs/mood visible in stats popup (color-coded bars) | StatsPopup.xaml — 3 progress bars (food=green, energy=blue, happiness=pink) + mood label | ☐ |
| C-06 | Level/XP display in stats popup | StatsPopup.xaml — "Lv. {Level}" + XP progress bar | ☐ |
| C-07 | Tray icon tooltip shows current mood + level | TrayIconController — update tooltip: "Mochi 💚 Lv.{Level} {Mood}" | ☐ |
| C-08 | Bring Mochi from tray menu → teleport to center | Tray feed → teleport cat to screen center + Surprised | ☐ |

## Phase D: Hardening (D-01..D-08)

| Task | Description | Files | Done |
|------|-------------|-------|------|
| D-01 | Multi-monitor: detect monitor count changes | App.xaml.cs — SystemEvents.DisplaySettingsChanged → recompute screen bounds | ☐ |
| D-02 | Monitor disappear → teleport cat to primary + Surprised | App.xaml.cs — if cat position off-screen after monitor change → teleport center | ☐ |
| D-03 | PerMonitorV2 DPI awareness — sprite scales with DPI | App.xaml.cs — use PresentationSource.FromVisual(_overlay).CompositionTarget.TransformToDevice | ☐ |
| D-04 | Resource budget: RAM <100MB check | App.xaml.cs — log GC.GetTotalMemory(false) every 60s, warn if >100MB | ☐ |
| D-05 | Resource budget: CPU <2% idle — throttle render to 10fps when asleep | App.xaml.cs — when FSM=Sleeping + no interaction 5min → reduce CompositionTarget rate | ☐ |
| D-06 | Error handling: crash recovery — try/catch around OnRendering | App.xaml.cs OnRendering — wrap each subsystem in try/catch, log errors, continue | ☐ |
| D-07 | Single-instance mutex already works — verify on Windows | Already implemented in Program.cs | ☐ |
| D-08 | Final: manual test checklist run + bug fixes | Run through all 10 ACs, fix any issues found | ☐ |

## Execution Order

1. **Phase A** (8 tasks) — visual polish, makes cat feel alive
2. **Phase B** (8 tasks) — interaction depth, reactive behaviors
3. **Phase C** (8 tasks) — UI completion, user-facing windows
4. **Phase D** (8 tasks) — hardening, robustness, resource budget

Total: 32 tasks. All executed autonomously, build + test after each phase.