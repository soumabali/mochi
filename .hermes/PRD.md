# PRD Neko Desktop Companion ("Mochi") v2.0

**Status:** draft → pending approval
**Date:** 2026-07-05
**Author:** Dhar (original PRD v2.0) → Ame (Hermes) reformatted to dhar-dev template
**Source:** [01-documents/PRD_mochi_v2_original.md](../01-documents/PRD_mochi_v2_original.md) (398 lines, asset-locked, final)

---

## 1. Background & Goals

A lightweight Windows desktop pet that renders an animated cat (Mochi) on top of the user's desktop. Mochi wanders, naps, plays, gets hungry, reacts to mouse — all without stealing focus, blocking clicks, or appearing in taskbar. Think Shimeji / Desktop Goose / Tamagotchi, but modern, smooth, and completely non-intrusive.

**Hard constraint:** NO new art/sound assets will ever be produced. Everything must be built from the existing asset inventory (§4 of original PRD). Features requiring missing assets use code-only techniques (transforms, particles, procedural effects, asset reuse) or are cut.

**Measurable goals:**
- G-1: Mochi renders with true transparency at 60 fps; user can click through the window rect (outside sprite bounds) to interact with underlying apps.
- G-2: Mochi performs ≥8 distinct behaviors in 30 min with zero user input.
- G-3: < 100 MB RAM, < 2% CPU idle; cold start < 2 s.
- G-4: Ship polished MVP in < 1 week.

---

## 2. User Stories

| Story |
|-------|
| US-1: As a desktop user, I want a cute cat companion on my screen, so that my desktop feels alive and delightful. |
| US-2: As a programmer, I want Mochi to never steal focus or block clicks, so that I can work without interruption. |
| US-3: As a pet lover, I want to feed, pet, and play with Mochi, so that I feel genuine attachment to it. |
| US-4: As a gamer/streamer, I want Mochi to auto-hide during fullscreen apps, so that it doesn't interfere with games or streams. |
| US-5: As a user, I want Mochi to have moods and needs (hunger, sleep, happiness), so that it feels like a living pet not a looping GIF. |
| US-6: As a user, I want Mochi to remember its state across restarts, so that it greets me on return and doesn't guilt-trip me. |
| US-7: As a user, I want a tray icon and context menu, so that I can control Mochi even when it's click-through. |
| US-8: As a user, I want Settings for personality, volume, and behavior toggles, so that I can customize Mochi to my preference. |

---

## 3. Functional Requirements

| Requirement (verifiable) |
|--------------------------|
| FR-1: Transparent click-through window with Win32 extended styles (WS_EX_LAYERED \| WS_EX_TRANSPARENT in roam mode, cleared in interact mode). |
| FR-2: Two-mode hit testing: roam (click-through ON), interact (click-through OFF when cursor enters sprite bounds). Bounds use visible sprite rect, not window rect. |
| FR-3: No taskbar/alt-tab presence (WS_EX_TOOLWINDOW). Never steal focus (WS_EX_NOACTIVATE). |
| FR-4: 60 fps SkiaSharp rendering with frame-skip under load. |
| FR-5: AssetManifest (JSON) is the sole mapping layer between FSM states and file paths. Frame counts enumerated at runtime via Directory.GetFiles, never hardcoded. |
| FR-6: Animation playback modes: holdFirstFrame, playOnce, loop, playOnceReversed, playOnceThenHoldLast. |
| FR-7: FSM with states: Idle, Walking (L/R/Fwd), Running (var1/2), Jumping (var1/2), Sleeping, Playful, Hungry (std/crit), Scratch/Meow/Blink. Interrupts: cursor near, hover, click, drag, fast cursor, double-click, energy floor, fullscreen. |
| FR-8: Weighted random behavior planner with mood-based weights, chained sequences, and personality dial (Calm↔Chaotic). |
| FR-9: Mouse interactions: cursor near → face cursor; hover 3s → petting (hearts); click → meow; drag → angry (follows cursor); release → fall+squash+dust; fast cursor → 20% surprised; double-click → playful. |
| FR-10: Needs system: food (−1/4min), energy (−1/6min awake, +1/30min asleep), happiness (interaction-driven). Mood resolution deterministic with 60s hysteresis. |
| FR-11: Feeding via context menu: food +40, happiness +10, eating animation with hearts. |
| FR-12: Sleep: auto when energy ≤20, manual via menu. Wake = reversed yawn. Zzz particles while sleeping. |
| FR-13: Tray icon with context menu (Feed, Play, Sleep/Wake, Stats, Settings, Exit, Bring Mochi). |
| FR-14: Settings window (normal WPF window): personality slider, volume, behavior toggles (typing awareness, night mode, cursor curiosity), scale (75-150%), language (EN/ID). |
| FR-15: Persistence: %APPDATA%/NekoCompanion/save.json, debounced 5s writes. Offline decay on launch (capped, never critical on return). Welcome-back meow if away >24h. |
| FR-16: Level/XP: feedings, pettings, play sessions grant XP. Level shown in Stats popup. |
| FR-17: Procedural micro-motion: breathing (±1.5% vertical scale, ~0.4Hz), idle fidgets (blink, head bob, sway, glance) every 6-20s. |
| FR-18: Particle system: hearts (feed/pet), Zzz (sleep), "!" (surprised), dust puff (landing). All vector-drawn via SkiaSharp, zero image assets. |
| FR-19: Transform tricks: reverse playback, squash & stretch on landings, playback speed per emotion, horizontal flip FORBIDDEN (asymmetric sprites). |
| FR-20: Tint filters: sad (15% desaturation, downward offset, 0.8x speed), sleepy warning (periodic darkening pulse), night mode (22:00-06:00 cool/dim tint + sleep bias). |
| FR-21: Cursor curiosity: cursor idle 30s near Mochi → walks toward and sits beside. Fast cursor (>1500px/s) → 20% startled. |
| FR-22: Typing awareness: global keypress rate only (never which keys). >120 keys/min for 2min → Mochi sleeps in corner. Typing stops 5min → wakes, meows once. |
| FR-23: Fullscreen app detection: auto-hide Mochi, reappear on exit. |
| FR-24: Multi-monitor + PerMonitorV2 DPI awareness. Monitor disappear → teleport primary + Surprised. |
| FR-25: Single instance enforced (mutex). Portable (no installer required for MVP). |
| FR-26: Fail-loud: missing asset → AssetMissing event + Serilog log + fallback to Idle. Never crash or render empty frame. |
| FR-27: Sound management: NAudio with per-sound trim (1.5-3s), cooldowns (8s per type), master volume default 0.35. No sound for states in statesWithoutSound list. |
| FR-28: PNG alpha verification on load: warn if frame lacks alpha, treat corner color as transparent. |

---

## 4. Non-Functional Requirements

| Area | Requirement (with numbers) |
|------|---------------------------|
| Performance | 60 fps active, 10 fps low-power idle (asleep + no interaction 5min), cold start < 2s |
| Memory | < 100 MB RAM |
| CPU | < 2% idle/asleep |
| Platform | Windows 10/11 x64, no admin rights, portable single-folder |
| Storage | %APPDATA%/NekoCompanion/ (save.json + logs) |
| Offline | Fully offline, no network calls |
| DPI | PerMonitorV2 DPI awareness |
| Logging | Serilog rolling file in %APPDATA%/NekoCompanion/logs |
| Single instance | Mutex enforced |
| Privacy | Key-rate only (never which keys), nothing logged or stored from input |
| Asset lock | NO new art/sound ever; code-only techniques for missing assets |

---

## 5. Scope

**In (MVP — ship in < 1 week):**
- Transparent click-through overlay window (Win32 interop)
- SkiaSharp 60fps rendering with manifest-driven animation
- FSM with weighted random behavior planner + chained sequences
- Mouse interaction (hover, click, drag, release, fast cursor, double-click)
- Needs/mood system (food, energy, happiness) with feeding & sleeping
- Particle system (hearts, Zzz, "!", dust) — all vector-drawn
- Procedural micro-motion (breathing, fidgets, happy hop)
- Transform tricks (reverse playback, squash & stretch, tint filters)
- Cursor curiosity + typing awareness
- Tray icon + context menu
- Settings window (personality, volume, toggles, scale, language)
- JSON persistence with offline decay + welcome-back
- Level/XP (light Tamagotchi hook)
- Night mode (22:00-06:00)
- Fullscreen app detection + auto-hide
- Multi-monitor + DPI awareness
- Single-instance mutex
- Event bus architecture

**Out (explicitly NOT built, do not stub):**
- AI chat / LLM integration
- Voice reminders / pomodoro
- Calendar / weather
- Window-top walking & window collision (post-MVP; MVP is bottom-edge only)
- Plugin system / SDK
- Skin marketplace
- Steam / Discord / OBS integrations
- Multiple pets
- Vision / face tracking

*Architecture (event bus, DI, manifest) must make these addable, but zero MVP time spent on them.*

---

## 6. Screens

| Screen | Purpose |
|--------|---------|
| S-1 | Transparent overlay — Mochi sprite rendered via SkiaSharp, sized to bounding box + particle margin |
| S-2 | Settings window — normal WPF window (the ONLY normal window): personality, volume, toggles, scale, language |
| S-3 | Tray icon context menu — Feed, Play, Sleep/Wake, Stats, Settings, Exit, Bring Mochi |
| S-4 | Stats popup — food/energy/happiness/level mini display |

---

## 7. Entities

| Entity | Key fields |
|--------|-----------|
| E-1: SaveData | name, level, xp, food, energy, happiness, mood, totalFeedings, totalPets, lastSeenUtc, settings |
| E-2: Settings | personality (0-1), volume (0-1), scale (0.75-1.5), typingAwareness (bool), nightMode (bool), cursorCuriosity (bool) |
| E-3: AssetManifest | sprites (state→folder+mode), sounds (state→file), statesWithoutSound, soundSettings |
| E-4: Mood | HungryCritical, Hungry, Sleep, Sleepy, Happy, Sad, Neutral (deterministic resolution, 60s hysteresis) |
| E-5: FSMState | Idle, WalkLeft, WalkRight, WalkForward, RunVar1/2, JumpVar1/2, Sleep, Playful, HungryStd, HungryCrit, Scratch, Meow, Blink, Angry, Surprised, Drag, Fall |

---

## 8. APIs

No external APIs. Internal event bus:

| Event | Trigger |
|-------|---------|
| A-1: MouseMoved | GetCursorPos poll ~30Hz |
| A-2: MouseClicked | Click detected on sprite |
| A-3: MouseDragStart/End | Drag begin/release |
| A-4: CursorNearCat | Cursor within 150px |
| A-5: CatClicked | Click on sprite bounds |
| A-6: CatFed / CatPetted | Menu feed / hover 3s |
| A-7: MoodChanged | Needs tick → mood recompute |
| A-8: NeedsTick | Periodic stat decay |
| A-9: SleepStarted / SleepEnded | Energy floor / manual / wake |
| A-10: StateChanged | FSM transition |
| A-11: AssetMissing | Manifest entry → missing file |
| A-12: FullscreenDetected / Exited | Foreground window fullscreen check |
| A-13: MonitorChanged | DisplaySettingsChanged |
| A-14: TypingBurstStarted / Ended | Key-rate threshold |
| A-15: LevelUp | XP threshold reached |

---

## 9. Acceptance Criteria per Feature (MVP Definition of Done)

| Feature | Criteria (testable, copy-ready for TASKS.md) |
|---------|----------------------------------------------|
| AC-1: Transparency | Mochi renders with true transparency; user can click a link *through* Mochi's window rect (outside sprite bounds) and it works |
| AC-2: Non-intrusive | While user types in another app, Mochi never takes focus and never appears in alt-tab |
| AC-3: Drag-release | Dragging Mochi and releasing produces: angry (with sound) → fall → dust puff → squash landing → recovery → idle, no visual dead-ends |
| AC-4: Feeding | Feeding raises food, plays eating sequence with hearts, persists across restart |
| AC-5: Variety | Left alone 30 min, Mochi performs ≥8 distinct behaviors (walk, run, jump, scratch, meow, blink, sleep, glance) without user input |
| AC-6: Fullscreen | Launching fullscreen game hides Mochi; exiting brings it back |
| AC-7: Resource budget | Task Manager: < 100 MB RAM, < 2% CPU while idle-asleep |
| AC-8: Missing asset | Deleting a sprite folder and launching logs AssetMissing warning and Mochi still runs (degraded, no crash) |
| AC-9: Sound cooldowns | All sounds respect cooldowns; no sound plays > once per 8s per type; master volume defaults 0.35 |
| AC-10: Typo folder | The `cat_surpised` typo folder loads correctly via manifest |

---

## 10. Riskiest Assumptions (validate before BUILD)

| # | Assumption | Type | Impact if wrong | Uncertainty | Rank |
|---|-----------|------|-----------------|-------------|------|
| RA-1 | WPF + Win32 extended styles can achieve reliable click-through + interact-mode toggle on Win10/11 | Feasibility | HIGH (core UX broken without it) | MEDIUM (Win32 interop edge cases) | **1** |
| RA-2 | SkiaSharp SKElement renders 60fps transparent overlay without flickering on integrated GPUs | Feasibility | HIGH (visual quality) | LOW-MEDIUM (SkiaSharp proven) | 2 |
| RA-3 | Users will actually want a desktop pet long-term (not just novelty for 2 days) | Desirability | MEDIUM (retention) | MEDIUM (Shimeji has proven long-term audience) | 3 |
| RA-4 | 4081 PNG frames at 1280x720 can load within 100MB RAM budget | Feasibility | MEDIUM (may need lazy loading) | LOW (lazy-load is standard) | 4 |

**Kill criterion for RA-1:** Build a minimal WPF window with Win32 extended styles (WS_EX_LAYERED \| WS_EX_TRANSPARENT \| WS_EX_TOOLWINDOW \| WS_EX_NOACTIVATE) + SkiaSharp transparent surface on Day 1. If click-through doesn't work OR sprite can't receive clicks when WS_EX_TRANSPARENT is toggled off → pivot to different window strategy before proceeding.

**Cheapest test:** Day 1 spike (already in PRD day-by-day plan). The PRD's own cutline front-loads this exact test.

---

## 11. Success Metrics

| Metric | Target | How to measure |
|--------|--------|----------------|
| SM-1: Behavior variety | ≥8 distinct behaviors in 30 min idle | Automated test: log state transitions for 30 min, count unique states |
| SM-2: Resource budget | < 100 MB RAM, < 2% CPU idle | Task Manager observation after 5 min idle |
| SM-3: Transparency | Click-through works outside sprite bounds | Manual test: click link through window rect |
| SM-4: Non-intrusive | No focus steal, no alt-tab entry | Manual test: type in editor while Mochi active |
| SM-5: Cold start | < 2 seconds | Stopwatch from launch to first frame |
| SM-6: Asset resilience | No crash on missing asset | Delete sprite folder, launch, verify log + fallback |

---

## 12. Open Questions

| # | Question | Status |
|---|----------|--------|
| ~~OQ-1~~ | ~~.NET 8 vs .NET 9?~~ | **RESOLVED:** .NET 9 (per PRD §12) |
| ~~OQ-2~~ | ~~GitHub repo name when ready?~~ | **RESOLVED:** `soumabali/mochi` |
| ~~OQ-3~~ | ~~EN/ID string resources — full ID translation in MVP or EN-only with ID stub?~~ | **RESOLVED:** EN-only for MVP, resource structure ready for ID |

---

## 13. Tech Stack (from original PRD §12)

| Component | Choice | Rationale |
|-----------|--------|-----------|
| Language/Runtime | C# / .NET 9 (see OQ-1) | PRD specifies .NET 9 |
| UI host | WPF | Win32 extended-style interop simpler than WinUI 3 |
| Rendering | SkiaSharp (SKElement/SKGLElement) | 60fps, particles, transforms |
| Audio | NAudio | Per-sound trim + cooldown wrapper |
| Config/Save | System.Text.Json | Standard, lightweight |
| Logging | Serilog | Rolling file in %APPDATA% |
| DI | Microsoft.Extensions.DependencyInjection | Standard |
| Tray | Hardcodet.NotifyIcon.Wpf | WPF tray icon |

---

## 14. Project Structure (from original PRD §13)

```
02-application/
  App/              entry, bootstrap, single-instance mutex
  Core/
    Animation/      AnimationManager, playback modes, frame enumerator
    Behavior/       FSM, planner, chains, moods, needs
    Events/         event bus
    Models/         states, manifest DTOs, save DTOs
    Particles/      hearts, zzz, "!", dust (vector-drawn)
    Physics/        gravity, edges, squash&stretch
    Services/       needs ticker, level/xp, night mode, typing-rate
  Assets/
    Sprite/         (symlinked from workspace)
    Sound/          (symlinked from workspace)
    manifest.json   §5 of original PRD
  Infrastructure/
    Audio/          NAudio wrapper: trim, cooldown, volume
    Input/          cursor polling, key-rate counter (rate only!)
    Storage/        save manager (debounced)
    Window/         Win32 interop: styles, hit-test toggle, fullscreen detect
  UI/
    Overlay/        transparent host window + SkiaSharp surface
    Settings/       normal WPF settings window
    Tray/           tray icon menus
```

---

## 15. Day-by-Day Cutline (suggested)

| Day | Focus |
|-----|-------|
| Day 1 | Transparent click-through window (§9) + manifest loader + sprite renderer 60fps |
| Day 2 | FSM core + walk/idle/blink + screen-edge taskbar-aware movement |
| Day 3 | Mouse interaction (hover, touch, drag, release-fall) + squash/stretch + particles |
| Day 4 | Mood/needs system + feeding + sleeping + save/load JSON |
| Day 5 | Behavior planner (weighted random + chains) + cursor curiosity + sound manager |
| Day 6 | Tray icon + context menu + Settings window + night mode + typing awareness |
| Day 7 | Polish, perf pass (memory/CPU) + edge cases (multi-monitor, DPI, resolution) + packaging |