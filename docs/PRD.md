# PRD — Neko Desktop Companion ("Mochi")

**Version:** 2.0 (final, asset-locked)
**Status:** Ready for `hermes` agent consumption — this document is the single source of truth
**Target build time:** MVP deliverable in **under 1 week** of focused agent-assisted development
**Hard constraint:** **NO new art or sound assets will ever be produced.** Everything must be built from the existing asset inventory (§4). Any feature requiring a missing asset must be implemented through code-only techniques (transforms, particles, procedural effects, asset reuse) or cut.

---

## 0. Instructions for the Hermes Agent (READ FIRST)

1. **Asset lock is absolute.** The folders and files listed in §4 are the complete and final asset set. Do not generate placeholder art, do not reference sprites/sounds that don't exist, do not leave TODOs for "future assets." If a state has no asset, it is either aliased (§4.3), synthesized in code (§6), or does not exist in this product.
2. **Never hardcode frame counts.** Sprite folders contain per-frame PNG files whose counts vary between states (folder sizes range 16.3 kB to 98.3 kB). At startup, enumerate each folder (`Directory.GetFiles(dir, "*.png")`, sorted by filename) and build the animation timeline from what is actually on disk.
3. **Use the Asset Manifest (§5) as the only mapping layer** between FSM states and file paths. Never derive folder names from enum names — there is a known typo (`cat_surpised`) that would silently break automatic name matching.
4. **Excluded from the build entirely:** `flip_folders.py`, `video/` folder.
5. **Fail loud, not silent.** If a manifest entry points to a missing folder/file at runtime, publish an `AssetMissing` event, log via Serilog, and fall back to the Idle state — never render an empty frame or crash.
6. **The transparency spec (§9) is non-negotiable.** A WPF window with `Background="Transparent"` alone is NOT acceptable. The Win32 extended style work described there is required for the app to be usable during real work.
7. Follow the MVP cutline in §7 strictly. Everything in §7 must ship in week one. §8 items are explicitly out of MVP scope regardless of how easy they look.

---

## 1. Overview

**Product name:** Neko Desktop Companion
**Character name:** Mochi

A lightweight Windows application that renders a living, animated cat on top of the user's desktop. Mochi wanders along the bottom of the screen, naps, plays, gets hungry, reacts to the mouse, and occasionally does something surprising — all without ever stealing focus, blocking clicks, or appearing in the taskbar. Think Shimeji / Desktop Goose / Tamagotchi, but modern, smooth, and completely non-intrusive.

**Inspiration:** Microsoft Clippy, Desktop Goose, Shimeji, Bonzi Buddy (minus the spyware), Tamagotchi.

## 2. Goals

- Make the desktop feel alive and delightful without ever interfering with real work.
- Create genuine attachment: Mochi should feel like a pet with moods, needs, and personality — not a looping GIF.
- Maximize perceived variety and charm **from a fixed asset set** through behavioral depth, procedural effects, and smart asset reuse.
- Ship a polished, complete-feeling MVP in under one week.
- Lay a clean architectural foundation (event bus, manifest-driven assets, FSM) for post-MVP growth (AI chat, reminders, plugins).

## 3. Target Users & Platform

**Users:** Programmers, designers, gamers, office workers, streamers, anime lovers, cat lovers.
**Platform:** Windows 10 and Windows 11. x64. No admin rights required. Portable (single folder, no installer required for MVP).

---

## 4. Asset Inventory (LOCKED — the complete and final set)

### 4.1 Sounds (`Assets/Sound/`, WAV, ~1.9 MB each)

| File | Used for |
|---|---|
| `cat_angry_scratch_paw.wav` | Angry (drag protest) |
| `cat_begging.wav` | Hungry / begging / eating (reused) |
| `cat_blinking.wav` | Idle blink (occasional, low volume) |
| `cat_chasing_wand.wav` | Playful |
| `cat_meowing.wav` | Meow (both directions) |
| `cat_scratching.wav` | Scratching (both directions) |
| `cat_sleepy.wav` | Sleepy / falling asleep |
| `cat_surprised.wav` | Surprised |
| `cat_walking.wav` | Walking left/right |
| `cat_walking_forward.wav` | Walking toward camera |

### 4.2 Sprites (`Assets/Sprite/`, one folder per state, frame PNGs inside)

| Folder | Notes |
|---|---|
| `begging_food` | Begging pose — standard hunger |
| `cat_angry_scratch_paw` | Angry paw swipe |
| `cat_blinking_left` | Blink facing left |
| `cat_blinking_right` | Blink facing right |
| `cat_chasing_wand` | Playful wand chase |
| `cat_hungry_begging_food` | Begging pose — critical hunger variant |
| `cat_meowing_left` / `cat_meowing_right` | Meow, both facings |
| `cat_scratching_left` / `cat_scratching_right` | Scratch, both facings |
| `cat_sleepy_yawn` | Yawn → sleep |
| `cat_surpised` | ⚠️ TYPO in folder name (missing "r") — map explicitly, never by string derivation |
| `cat_walking_forward` | Walk toward camera |
| `cat_walking_left` / `cat_walking_right` | Walk, both facings |
| `jump_1`, `jump_2` | Two jump variants (smaller folders — fewer frames) |
| `run_1`, `run_2` | Two run variants |

### 4.3 Alias & Reuse Table (permanent decisions — asset lock makes these final)

| Logical state | Physical asset | Technique |
|---|---|---|
| **Idle** (home state) | First frame of `cat_blinking_left` (or `_right` per current facing) held as a still | Hold-frame + procedural breathing (§6.1) |
| **Sit** | Same as Idle | Alias |
| **Look Left / Right** | `cat_blinking_left` / `cat_blinking_right` | The blink reads as a glance |
| **Eating** | `begging_food` looped 2–3× at slightly faster playback + heart/crumb particles + `cat_begging.wav` | Reuse + particles (§6.2) |
| **Fall / landing** (after drag release) | `jump_1` or `jump_2` played **in reverse** for the fall, forward for the recovery hop | Reverse playback (§6.3) |
| **Wake up** | `cat_sleepy_yawn` played **in reverse** (eyes open, body rises) | Reverse playback |
| **Happy** | Idle + faster blink cadence + small procedural hop (translate-Y bounce) + heart particles | Code-only synthesis (§6.1, §6.2) |
| **Sad** (low food + low sleep) | Idle with slowed playback, slight downward sprite offset, desaturation shader tint at ~15% | Code-only synthesis (§6.4) |
| **Attack / pounce** | `cat_chasing_wand` (it already reads as lunging/swiping) | Direct reuse |
| **Bounce** (physics) | Squash & stretch transform applied to whatever frame is current | Transform-only (§6.3) |
| **Run / Jump SFX** | None — intentionally silent | Declared in manifest `statesWithoutSound` |

**These aliases are product decisions, not temporary workarounds.** Do not add TODO comments suggesting future asset replacement.

---

## 5. Asset Manifest (generate as `Assets/manifest.json`)

The manifest is the single mapping layer. All paths relative to `Assets/`. The FSM never touches file paths directly.

```json
{
  "sprites": {
    "IdleLeft":        { "folder": "Sprite/cat_blinking_left",  "mode": "holdFirstFrame" },
    "IdleRight":       { "folder": "Sprite/cat_blinking_right", "mode": "holdFirstFrame" },
    "BlinkLeft":       { "folder": "Sprite/cat_blinking_left",  "mode": "playOnce" },
    "BlinkRight":      { "folder": "Sprite/cat_blinking_right", "mode": "playOnce" },
    "WalkLeft":        { "folder": "Sprite/cat_walking_left",   "mode": "loop" },
    "WalkRight":       { "folder": "Sprite/cat_walking_right",  "mode": "loop" },
    "WalkForward":     { "folder": "Sprite/cat_walking_forward","mode": "loop" },
    "RunVar1":         { "folder": "Sprite/run_1",              "mode": "loop" },
    "RunVar2":         { "folder": "Sprite/run_2",              "mode": "loop" },
    "JumpVar1":        { "folder": "Sprite/jump_1",             "mode": "playOnce" },
    "JumpVar2":        { "folder": "Sprite/jump_2",             "mode": "playOnce" },
    "FallVar1":        { "folder": "Sprite/jump_1",             "mode": "playOnceReversed" },
    "FallVar2":        { "folder": "Sprite/jump_2",             "mode": "playOnceReversed" },
    "SleepYawn":       { "folder": "Sprite/cat_sleepy_yawn",    "mode": "playOnceThenHoldLast" },
    "WakeUp":          { "folder": "Sprite/cat_sleepy_yawn",    "mode": "playOnceReversed" },
    "MeowLeft":        { "folder": "Sprite/cat_meowing_left",   "mode": "playOnce" },
    "MeowRight":       { "folder": "Sprite/cat_meowing_right",  "mode": "playOnce" },
    "ScratchLeft":     { "folder": "Sprite/cat_scratching_left","mode": "playOnce" },
    "ScratchRight":    { "folder": "Sprite/cat_scratching_right","mode": "playOnce" },
    "Angry":           { "folder": "Sprite/cat_angry_scratch_paw","mode": "loop" },
    "Surprised":       { "folder": "Sprite/cat_surpised",       "mode": "playOnce" },
    "Playful":         { "folder": "Sprite/cat_chasing_wand",   "mode": "loop" },
    "HungryStandard":  { "folder": "Sprite/begging_food",       "mode": "loop" },
    "HungryCritical":  { "folder": "Sprite/cat_hungry_begging_food", "mode": "loop" },
    "Eating":          { "folder": "Sprite/begging_food",       "mode": "loop", "speedMultiplier": 1.3 }
  },
  "sounds": {
    "Angry":       "Sound/cat_angry_scratch_paw.wav",
    "Begging":     "Sound/cat_begging.wav",
    "Eating":      "Sound/cat_begging.wav",
    "Blink":       "Sound/cat_blinking.wav",
    "Playful":     "Sound/cat_chasing_wand.wav",
    "Meow":        "Sound/cat_meowing.wav",
    "Scratch":     "Sound/cat_scratching.wav",
    "Sleep":       "Sound/cat_sleepy.wav",
    "Surprised":   "Sound/cat_surprised.wav",
    "Walk":        "Sound/cat_walking.wav",
    "WalkForward": "Sound/cat_walking_forward.wav"
  },
  "statesWithoutSound": ["IdleLeft","IdleRight","RunVar1","RunVar2","JumpVar1","JumpVar2","FallVar1","FallVar2","WakeUp"],
  "soundSettings": { "masterVolumeDefault": 0.35, "blinkSoundProbability": 0.1, "walkSoundLoop": false, "walkSoundIntervalMs": 4000, "cooldownPerSoundMs": 8000 }
}
```

**Sound design rules (important for likability):** the WAV files are long (~1.9 MB ≈ 10+ seconds). Do **not** loop them raw with walk cycles. Play at most the first 1.5–3 seconds (configurable trim), respect per-sound cooldowns, and keep default master volume low (0.35). A pet that meows constantly gets uninstalled by day two.

---

## 6. Code-Only Enhancement Techniques (replaces missing assets — MVP scope)

Because no new assets will exist, perceived richness must come from code. All of the following are cheap, high-impact, and required in MVP:

### 6.1 Procedural micro-motion ("it's alive" layer)
- **Breathing:** on any held frame (Idle, sleep hold), apply a subtle sinusoidal vertical scale (±1.5%, ~0.4 Hz) so Mochi never looks frozen.
- **Idle fidgets:** random micro-events every 6–20 s from the pool: blink, ear-implied head bob (2 px vertical nudge), tail-implied sway (1° rotation oscillation for 2 s), quick glance (flip facing, hold 1 s, flip back).
- **Happy hop:** 2–3 quick sine bounces (translate-Y) with slight squash & stretch on landing.

### 6.2 Particle system (single lightweight system, four emitters)
- **Hearts:** rise + fade, on feed and on petting (hover 3 s+).
- **Zzz:** floating "z" glyphs drawn as text/vector (no sprite needed), while sleeping.
- **Sweat drop / exclamation:** vector-drawn "!" above head on Surprised — sells the reaction hard.
- **Dust puff:** small expanding fading circles at feet on landing after fall/jump.
All particles are vector/procedurally drawn via SkiaSharp — zero image assets required.

### 6.3 Transform tricks
- **Reverse playback** as a first-class animation mode (`playOnceReversed`) — turns `jump` into `fall` and `sleepy_yawn` into `wake_up` for free.
- **Squash & stretch:** on landings, compress Y / expand X 10% for 80 ms, then overshoot back. This one trick makes physics feel professional.
- **Playback speed as emotion:** faster = excited/panicked, slower = tired/sad. Exposed per-state in the manifest (`speedMultiplier`).
- **Horizontal flip is FORBIDDEN as a facing substitute** — dedicated left/right sprites exist and flipping would look mirrored-wrong (asymmetric markings). Flip is only allowed for particles.

### 6.4 Tint & filter states
- **Sad:** ~15% desaturation + 3 px downward offset + 0.8× playback speed on Idle.
- **Sleepy warning:** slow periodic eyelid-like darkening pulse (subtle alpha overlay), signaling sleep is imminent.
- **Night mode (bonus, trivial):** between 22:00–06:00 local time, apply a slight cool/dim tint and bias the behavior planner heavily toward sleeping. Mochi keeps your hours.

### 6.5 Behavioral depth (the real variety engine)
- **Weighted random behavior planner:** state selection weights shift with mood, needs, time of day, and time-since-last-interaction. Two users' Mochis behave differently within an hour.
- **Personality dial (Settings):** one slider "Calm ↔ Chaotic" scaling event frequency and run/jump probability.
- **Chained sequences:** planner composes multi-step routines instead of single states, e.g. *walk right → stop → scratch → glance at cursor → walk forward → sit*. Sequences make the fixed asset set feel 5× larger.
- **Cursor curiosity:** if the cursor is idle for 30 s near Mochi, he slowly walks toward it and sits beside it. If the cursor starts moving fast (>1500 px/s sustained), 20% chance he gets startled (Surprised) — a beloved Desktop Goose-style moment, achieved with existing assets only.
- **Typing awareness (lightweight, no keylogging):** using only a global keypress *rate* counter (never which keys), if the user is typing intensely (>120 keys/min for 2 min), Mochi settles into sleep at the screen corner — "do not disturb" empathy. When typing stops for 5 min, he wakes and meows once. This is a signature charm feature and must be clearly documented in Settings as rate-only, nothing logged or stored.

---

## 7. MVP Scope (ship in < 1 week)

### Day-by-day cutline (suggested)
- **Day 1:** Transparent click-through window (§9) + manifest loader + sprite renderer at 60 fps.
- **Day 2:** FSM core + walk/idle/blink + screen-edge & taskbar-aware movement.
- **Day 3:** Mouse interaction (hover, touch, drag, release-fall) + squash & stretch + particles.
- **Day 4:** Mood & needs system + feeding + sleeping + save/load JSON.
- **Day 5:** Behavior planner (weighted random + chained sequences) + cursor curiosity + sound manager with trims/cooldowns.
- **Day 6:** Tray icon + context menu + Settings window + night mode + typing awareness.
- **Day 7:** Polish, perf pass (memory/CPU budget), edge cases (multi-monitor, DPI, resolution change), packaging.

### 7.1 Window & rendering
- Transparent, click-through, always-on-top, no taskbar/alt-tab presence, never steals focus (full spec §9).
- Window sized to Mochi's bounding box only (+small particle margin), never a fullscreen overlay.
- 60 fps SkiaSharp rendering; frame-skip gracefully under load.

### 7.2 Movement & physics (lightweight)
- Mochi walks along the work-area bottom edge (per-monitor `SystemParameters.WorkArea`, taskbar-aware).
- Gravity applies when dragged and released: fall (reversed jump) → dust puff → squash landing → recover → shake-off (Angry loop 1×, no sound) → Idle.
- Screen edges are hard boundaries; on reaching one: turn around, or 15% chance to sit and blink first.
- Occasional random jump (`jump_1`/`jump_2`) and short run bursts (`run_1`/`run_2`) per personality dial.

### 7.3 Mouse interaction
| Trigger | Response |
|---|---|
| Cursor within 150 px | Face the cursor (Idle facing swap / blink-glance) |
| Cursor hover over Mochi 3 s+ | "Petting": hearts + happiness up (cooldown 60 s) |
| Click on Mochi | Meow (facing-correct) + `cat_meowing.wav` |
| Drag | Angry loop + `cat_angry_scratch_paw.wav`; sprite follows cursor with slight lag (elastic) |
| Release mid-air | Gravity fall → landing sequence (§7.2) |
| Fast cursor movement nearby | 20% Surprised + "!" particle + `cat_surprised.wav` |
| Double-click on Mochi | Playful (chasing wand) 3 s — he plays with your cursor |

### 7.4 Needs, mood & feeding
- Stats 0–100: `food` (−1/4 min), `energy` (−1/6 min while awake, +1/30 s asleep), `happiness` (derived + interaction-driven).
- **Mood resolution (deterministic, in priority order):** food < 20 → **HungryCritical**; food < 45 → **Hungry**; energy < 20 → auto-**Sleep**; energy < 40 → **Sleepy** (slow, yawns often); happiness > 75 → **Happy**; food < 35 AND happiness < 30 → **Sad**; otherwise **Neutral**. First match wins; no oscillation — apply 60 s hysteresis on mood transitions.
- **Feeding:** tray/context menu "Feed" → Eating alias sequence (§4.3) → food +40, happiness +10, hearts.
- **Sleep:** context menu Sleep/Wake, plus automatic per energy. Sleeping = held last yawn frame + breathing scale + Zzz particles. Wake = reversed yawn.

### 7.5 Context menu (right-click on Mochi) & tray icon
Context menu: Feed · Play (trigger Playful) · Sleep/Wake · Stats (food/energy/happiness mini popup) · Settings · Exit.
Tray icon (needed because click-through makes Mochi occasionally hard to catch): same menu + "Bring Mochi to this monitor".

### 7.6 Settings (simple WPF window — the ONLY normal window in the app)
General (start with Windows, monitor selection) · Personality slider (Calm↔Chaotic) · Volume (master + mute) · Behavior toggles (typing awareness on/off, night mode on/off, cursor curiosity on/off) · Scale (75%–150% sprite size) · Language (EN/ID strings via resource files).

### 7.7 Persistence
`%APPDATA%/NekoCompanion/save.json`, written on change (debounced 5 s) and on exit:
```json
{
  "name": "Mochi", "level": 3, "xp": 120,
  "food": 70, "energy": 80, "happiness": 65,
  "mood": "happy", "totalFeedings": 12, "totalPets": 40,
  "lastSeenUtc": "2026-07-05T10:00:00Z",
  "settings": { "personality": 0.5, "volume": 0.35, "scale": 1.0, "typingAwareness": true, "nightMode": true, "cursorCuriosity": true }
}
```
- **Offline decay on launch:** compute elapsed time since `lastSeenUtc`, decay food/energy accordingly (capped so Mochi is never *critical* on return — greet the user, don't guilt them). If away > 24 h, Mochi's first action is WalkForward toward screen center + one meow: "welcome back."
- **Level/XP (light Tamagotchi hook):** XP from feedings, pettings, play sessions. Level shown in Stats popup. No gameplay gates in MVP — purely a retention/attachment number.

### 7.8 Non-functional budget
Memory < 100 MB · CPU idle < 2% (animations pause to 10 fps "low-power idle" when Mochi is asleep AND no interaction for 5 min) · 60 fps active · cold start < 2 s · fully offline · no admin · portable · single instance enforced (mutex).

---

## 8. Explicitly OUT of MVP (do not build, do not stub UIs for)
AI chat / LLM integration · voice · reminders/pomodoro · calendar/weather · window-top walking & window collision (post-MVP; MVP is bottom-edge only) · plugin system/SDK · skin marketplace · Steam/Discord/OBS integrations · multiple pets · Vision/face tracking.

The architecture (event bus, DI, manifest) must make these *addable*, but zero MVP time is spent on them.

---

## 9. Transparency & Non-Intrusive Overlay — REQUIRED technical spec

This is where desktop-pet apps typically fail and become annoying. All items mandatory:

1. **Layered click-through window:** after `HwndSource` is available, set `WS_EX_LAYERED | WS_EX_TRANSPARENT` via `SetWindowLong(GWL_EXSTYLE)`. In this default "roam mode," all mouse input passes through to whatever is underneath — the user can click "through" Mochi at their real work.
2. **Two-mode hit testing:**
   - **Roam mode (default):** `WS_EX_TRANSPARENT` ON.
   - **Interact mode:** when the cursor enters Mochi's *actual sprite bounds* (poll `GetCursorPos` at ~30 Hz against the character rect — do NOT rely on WPF hit testing, it's dead under click-through), temporarily clear `WS_EX_TRANSPARENT` so click/drag/right-click land on Mochi. Restore immediately when the cursor leaves or the drag ends.
   - Bounds check uses the *visible sprite* rect, not the full window rect, so near-misses still click through.
3. **No taskbar / alt-tab:** add `WS_EX_TOOLWINDOW`, remove `WS_EX_APPWINDOW`.
4. **Never steal focus:** `WS_EX_NOACTIVATE`. Mochi must never become the foreground window; the user's typing must never be interrupted. (The Settings window is a normal window and exempt.)
5. **Topmost but tiny:** `HWND_TOPMOST` with the window sized to the character bounding box + particle margin. Never a screen-sized transparent sheet — a hit-testing bug on a fullscreen overlay would brick the user's desktop.
6. **Fullscreen app respect:** detect when the foreground window is fullscreen (game/video: foreground rect equals monitor rect and it isn't the shell); auto-hide Mochi and reappear when fullscreen exits. Non-negotiable for the gamer/streamer audience.
7. **PNG alpha verification at load:** every frame must carry a real alpha channel; if any frame lacks one, log a warning and pre-process (treat pure-white or corner-sampled color as transparent) rather than rendering an opaque box.
8. **Multi-monitor & DPI:** `PerMonitorV2` DPI awareness in the manifest; recompute position/scale on `DisplaySettingsChanged`; walk area = work area of the monitor Mochi currently occupies; if that monitor disappears, teleport to primary with a Surprised reaction (a bug turned into charm).

---

## 10. State Machine

```
                          ┌─────────┐
              ┌──────────►│  IDLE   │◄──────────┐
              │           └────┬────┘           │
              │      (behavior planner tick)    │
              │                │                │
   ┌──────────┼───────┬───────┼───────┬────────┼──────────┐
   ▼          ▼       ▼       ▼       ▼        ▼          ▼
 Walking   Running  Jumping Sleeping Playful  Hungry   Scratch/
 (L/R/Fwd) (var1/2) (var1/2) (yawn→  (wand)  (std/crit) Meow/Blink
   │          │       │      hold)     │        │       (fidgets)
   └──────────┴───────┴───────┴────────┴────────┴──────────┘
                               │
                               ▼
                             IDLE

INTERRUPTS (from any state; return to previous state or Idle):
  CursorNear      → face cursor (glance)
  CursorHover3s   → Petting (hearts)
  ClickOnCat      → Meow
  DragStart       → Angry (follows cursor, elastic lag)
  DragRelease     → Fall(reversed jump) → land(squash+dust) → shake-off → Idle
  FastCursor      → 20% Surprised("!")
  DoubleClick     → Playful 3s
  EnergyFloor     → forced Sleep
  FullscreenApp   → Hidden (auto-resume on exit)

RULES:
  - Interrupt states remember and restore the interrupted state where sensible.
  - Mood (from needs, §7.4) reweights planner probabilities; it does not force states except HungryCritical and forced Sleep.
  - Every playOnce state MUST declare a terminal transition — no dead ends.
  - 60 s hysteresis on mood transitions to prevent flip-flopping.
```

## 11. Event Bus (decoupled modules)

`MouseMoved` · `MouseClicked` · `MouseDragStart` · `MouseDragEnd` · `CursorNearCat` · `CatClicked` · `CatFed` · `CatPetted` · `MoodChanged` · `NeedsTick` · `SleepStarted` · `SleepEnded` · `StateChanged` · `AssetMissing` · `FullscreenDetected` · `FullscreenExited` · `MonitorChanged` · `TypingBurstStarted` · `TypingBurstEnded` · `LevelUp`

## 12. Architecture & Tech Stack

```
Windows Desktop
      │
Transparent Click-through Window  ← Win32 interop (§9), WPF host
      │
Rendering Engine                  ← SkiaSharp, 60fps, particles, transforms (§6)
      │
Animation Manager                 ← manifest-driven (§5), playback modes incl. reversed
      │
Behavior Planner + FSM            ← weighted random, chains, moods (§7.4, §10)
      │
Event Bus                         ← §11
      │
Services: Input(GetCursorPos poll + key-rate hook) · Audio(NAudio, trims/cooldowns)
          · Needs/Mood · Save(JSON, debounced) · Tray · Settings
```

- **Language/Runtime:** C# / .NET 9
- **UI host:** **WPF** (chosen over WinUI 3 — the §9 extended-window-style interop is dramatically simpler in WPF)
- **Rendering:** SkiaSharp (`SKElement`/`SKGLElement`)
- **Audio:** NAudio (with per-sound trim + cooldown wrapper)
- **Config/Save:** System.Text.Json
- **Logging:** Serilog (rolling file in `%APPDATA%/NekoCompanion/logs`)
- **DI:** Microsoft.Extensions.DependencyInjection
- **Tray:** Hardcodet.NotifyIcon.Wpf (or minimal Win32 Shell_NotifyIcon wrapper)

## 13. Project Structure

```
DesktopCat/
    App/                      # entry, DI bootstrap, single-instance mutex
    Core/
        Animation/            # AnimationManager, playback modes, frame enumerator
        Behavior/             # FSM, planner, chains, moods, needs
        Events/               # event bus
        Models/               # states, manifest DTOs, save DTOs
        Particles/            # hearts, zzz, "!", dust (vector-drawn)
        Physics/              # gravity, edges, squash&stretch
        Services/             # needs ticker, level/xp, night mode, typing-rate
    Assets/
        Sprite/               # copied as-is from source sprites/ (incl. cat_surpised typo)
        Sound/                # copied as-is from source sounds/
        manifest.json         # §5
    Infrastructure/
        Audio/                # NAudio wrapper: trim, cooldown, volume
        Input/                # cursor polling, key-rate counter (rate only!)
        Storage/              # save manager (debounced)
        Window/               # Win32 interop: styles, hit-test toggle, fullscreen detect
    UI/
        Overlay/              # the transparent host window + SkiaSharp surface
        Settings/             # normal WPF settings window
        Tray/                 # tray icon + menus
```

## 14. Acceptance Criteria (MVP definition of done)

1. Mochi renders with true transparency; a user can click a link *through* Mochi's window rect (outside sprite bounds) and it works.
2. While a user types in another app, Mochi never takes focus and never appears in alt-tab.
3. Dragging Mochi and releasing produces: angry (with sound) → fall → dust puff → squash landing → recovery → idle, with no visual dead-ends.
4. Feeding raises `food`, plays the eating sequence with hearts, and persists across restart.
5. Left alone for 30 minutes, Mochi visibly performs ≥ 8 distinct behaviors (walk, run, jump, scratch, meow, blink, sleep, glance) without user input.
6. Launching a fullscreen game hides Mochi; exiting brings him back.
7. Task Manager: < 100 MB RAM, < 2% CPU while idle-asleep.
8. Deleting a sprite folder and launching logs an `AssetMissing` warning and Mochi still runs (degraded, no crash).
9. All sounds respect cooldowns; no sound plays more than once per 8 s per type; master volume defaults to 0.35.
10. The `cat_surpised` typo folder loads correctly via the manifest.

---

## 15. Post-MVP Roadmap (unchanged in spirit, out of week-one scope)

**Phase 2:** Window-top walking & window-edge collision · richer chained routines · achievements.
**Phase 3:** AI chat bubble (local Ollama or cloud) · voice · reminders/pomodoro.
**Phase 4:** Plugin SDK (`plugin.json` + sprites/ + sounds/ + behavior.dll) · community skins · marketplace.
