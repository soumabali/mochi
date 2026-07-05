# DESIGN — mochi-v2 (Neko Desktop Companion)

**Status:** draft → pending approval
**Date:** 2026-07-05
**Source PRD:** .hermes/PRD.md (approved 2026-07-05)
**Mockups:** .hermes/design/mockups/

---

## 0. Design Language (grounded intent, not default)

This is a **desktop pet app**, not a web dashboard. The visual design challenge is unique: the primary "screen" (S-1 Overlay) is a transparent overlay with a sprite character — there is almost no traditional UI surface. The only conventional windows are Settings (S-2) and the tray context menu (S-3). Design tokens here apply to those UI surfaces; the overlay itself uses sprite assets + SkiaSharp vector-drawn particles.

- **What:** A living animated cat that lives on your Windows desktop — non-intrusive companion software, not a productivity tool.
- **Who / when:** Programmers, designers, gamers, anime/cat lovers — at their desk for hours, glancing at Mochi between tasks. Mood: casual, warm, low-stakes delight. The UI should feel like a cozy toy, not enterprise software.
- **Signature (whole product):** Soft rounded corners + warm pastel accent (sakura pink) + cat-paw motif on interactive elements. The Settings window should feel like opening a pet care booklet, not a config panel.
- **Self-critique:** "For any desktop app I'd default to flat Material Design with blue accents; changed to warm pastel (cream + sakura pink + soft brown) because Mochi is a cat companion — cozy pet-care aesthetic, not utility tool."
- **Anti-pattern check:** Tokens below are NOT shadcn/Tailwind-blue, NOT cream+serif+teracotta. Deliberate palette: warm cream surface, sakura pink accent, soft brown text — evokes a Japanese pet café. No exceptions needed.

---

## 1. Design Tokens

Single source of styling truth. BUILD tasks only use these values.

### Colors (semantic roles)

| Token | Value | Use | Reason (ties to §0) |
|-------|-------|-----|---------------------|
| --color-primary | `#E8A0BF` (sakura pink) | primary actions, active states | Cat-paw warmth, signature color |
| --color-primary-hover | `#D88BA5` | hover state | Darker pink for affordance |
| --color-accent | `#FFB4A2` (coral peach) | highlights, badges, emphasis | Secondary warm tone, complements sakura |
| --color-bg | `#FFF8F0` (warm cream) | Settings window background | Cozy paper-like backdrop |
| --color-surface | `#FFFFFF` (pure white) | cards, raised panels in Settings | Clean contrast on cream bg |
| --color-text | `#5D4037` (soft brown) | body text | Warm, not harsh black — pet café feel |
| --color-text-muted | `#8D6E63` (lighter brown) | secondary text | AA 4.5:1 on cream bg |
| --color-danger | `#EF5350` (soft red) | destructive actions (Exit) | Clear but not aggressive |
| --color-success | `#81C784` (soft green) | confirmations, fed status | Gentle positive feedback |
| --color-sleep | `#9575CD` (lavender) | sleep-related UI, Zzz | Dreamy, calming |
| --color-hungry | `#FFB74D` (amber) | hungry warning, food bar | Appetite warmth |

### Typography

| Token | Value | Use | Reason |
|-------|-------|-----|--------|
| --font-body | `'Segoe UI Variable', 'Segoe UI', sans-serif` | all body text, UI labels | Windows-native, clean, no web font needed |
| --font-heading | `'Segoe UI Variable Display', 'Segoe UI', sans-serif` | headings, stats numbers | Same family, heavier weight for hierarchy |
| --font-mono | `'Cascadia Code', 'Consolas', monospace` | debug/stats numbers | Windows-native mono for numeric display |

### Spacing

| Token | Value | Use |
|-------|-------|-----|
| --space-xs | 4px | tight gaps, icon padding |
| --space-sm | 8px | control spacing |
| --space-md | 16px | section padding |
| --space-lg | 24px | window padding |
| --space-xl | 32px | major section gap |

### Radii

| Token | Value | Use | Reason |
|-------|-------|-----|--------|
| --radius-sm | 6px | small controls, badges | Soft but not pill |
| --radius-md | 12px | cards, panels | Cozy rounded |
| --radius-lg | 20px | window corners, major surfaces | Signature soft shape |
| --radius-pill | 999px | toggles, stat pills | Friendly pill shape |

### Copy / Voice Notes

- **Tone:** Warm, casual, pet-owner affectionate. "Mochi is hungry 🍽️" not "Warning: food level critical."
- **EN strings:** Use pet-care language. "Feed Mochi" not "Execute feed action."
- **Settings labels:** Short, friendly. "Calm ↔ Chaotic" not "Personality coefficient."
- **Stats display:** Emoji + number. "🍽️ 70/100" not "Food: 70"
- **Error states:** Gentle. "Mochi can't find that sprite 😿" not "AssetMissing exception."
- **Tray tooltip:** "Mochi is here 💚" not "NekoCompanion running."

### Accessibility Floor

- All text meets AA 4.5:1 contrast on backgrounds
- Settings window: keyboard navigable (Tab order, Enter on sliders, Esc to close)
- Tray menu: standard Windows accessibility (system handles)
- No color-only meaning (icons + text labels always accompany color)
- Settings controls have visible focus indicators (2px sakura pink outline)

---

## 2. Window States (Desktop Domain)

| State | Window style | Size | Position | Visible |
|-------|-------------|------|----------|---------|
| Roam (default) | WS_EX_LAYERED \| WS_EX_TRANSPARENT \| WS_EX_TOOLWINDOW \| WS_EX_NOACTIVATE \| HWND_TOPMOST | Sprite bounding box + particle margin (≈256×256 + 32px margin) | Bottom-edge work area, per-monitor | Yes, click-through |
| Interact | Same but WS_EX_TRANSPARENT cleared | Same | Same | Yes, clickable |
| Drag | Same, WS_EX_TRANSPARENT cleared | Same | Follows cursor with elastic lag | Yes, dragable |
| Fullscreen hidden | Same | Same | Off-screen or hidden | No |
| Settings (S-2) | Normal WPF window | 480×640px, fixed | Centered on Mochi's monitor | Yes, normal focus |
| Tray (S-3) | Standard tray icon | System-sized | System tray | Icon always visible |

---

## 3. Screen Mockups

### S-1: Transparent Overlay (sprite + particles)

No traditional UI. SkiaSharp renders:
- Mochi sprite (1280×720 source → scaled to ~256×144 display at 1.0x scale)
- Particle layer: hearts ❤️, Zzz 💤, "!" ❗, dust puffs — all vector-drawn, no image assets
- Procedural effects: breathing scale, squash & stretch, tint overlays
- No background, no border, no chrome — pure sprite + particles on transparent window

**Mockup:** `design/mockups/S-1-overlay.html` (diagram of sprite bounds, particle zones, hit-test area)

### S-2: Settings Window

WPF window (480×640px), warm cream bg, sections separated by --space-lg:

1. **General** — Start with Windows (toggle), Monitor selection (dropdown)
2. **Personality** — Calum ↔ Chaotic slider (0.0–1.0)
3. **Volume** — Master volume slider (0.0–1.0) + mute toggle
4. **Behavior** — Toggles: typing awareness, night mode, cursor curiosity
5. **Scale** — Slider 75%–150%
6. **Language** — Dropdown: English (EN-only MVP)

**Mockup:** `design/mockups/S-2-settings.html`

### S-3: Tray Icon Context Menu

Standard Windows tray context menu:
- 🍽️ Feed Mochi
- 🎉 Play with Mochi
- 😴 Sleep / ⬆️ Wake up
- 📊 Stats... (mini popup)
- ⚙️ Settings...
- 📍 Bring Mochi here
- ─────────
- ❌ Exit

**Mockup:** `design/mockups/S-3-tray-menu.html`

### S-4: Stats Popup

Small floating panel near Mochi (not a full window):
- 🍽️ Food: 70/100 (progress bar, --color-hungry)
- ⚡ Energy: 80/100 (progress bar, --color-sleep when low)
- 💚 Happiness: 65/100 (progress bar, --color-primary)
- 📊 Level 3 (XP: 120/500)
- Auto-dismiss after 5s or click-away

**Mockup:** `design/mockups/S-4-stats.html`

---

## 4. Architecture Design

### 4.1 Component Diagram

```
Windows Desktop
 │
 ├─ App/ (entry, bootstrap, single-instance mutex)
 │   └─ Program.cs → DI container setup → start overlay
 │
 ├─ UI/
 │   ├─ Overlay/ (transparent host window + SKElement)
 │   │   ├─ OverlayWindow.xaml.cs (Win32 interop: styles, hit-test toggle)
 │   │   └─ MochiRenderer.cs (SkiaSharp draw loop: sprite + particles)
 │   ├─ Settings/ (normal WPF window)
 │   │   └─ SettingsWindow.xaml
 │   └─ Tray/ (tray icon + context menu)
 │       └─ TrayIconController.cs
 │
 ├─ Core/
 │   ├─ Animation/
 │   │   ├─ AnimationManager.cs (playback modes, frame enumeration)
 │   │   └─ PlaybackMode.cs (holdFirstFrame, playOnce, loop, reversed, holdLast)
 │   ├─ Behavior/
 │   │   ├─ FSM.cs (state machine, transitions, interrupts)
 │   │   ├─ BehaviorPlanner.cs (weighted random, chains, mood-based weights)
 │   │   └─ MoodResolver.cs (deterministic mood from needs, 60s hysteresis)
 │   ├─ Events/
 │   │   └─ EventBus.cs (publish/subscribe, all events from PRD §11)
 │   ├─ Models/
 │   │   ├─ FSMState.cs (enum)
 │   │   ├─ AssetManifest.cs (DTO for manifest.json)
 │   │   ├─ SaveData.cs (DTO for save.json)
 │   │   └─ Settings.cs (DTO)
 │   ├─ Particles/
 │   │   ├─ ParticleSystem.cs (single system, 4 emitter types)
 │   │   └─ Emitters.cs (Hearts, Zzz, Exclamation, DustPuff)
 │   ├─ Physics/
 │   │   └─ PhysicsEngine.cs (gravity, screen edges, squash&stretch)
 │   └─ Services/
 │       ├─ NeedsTicker.cs (food/energy/happiness decay)
 │       ├─ LevelXpService.cs (XP gain, level up)
 │       ├─ NightModeService.cs (22:00-06:00 detection)
 │       └─ TypingRateService.cs (keypress rate only, never keys)
 │
 ├─ Infrastructure/
 │   ├─ Audio/
 │   │   └─ AudioManager.cs (NAudio, trim, cooldown, volume)
 │   ├─ Input/
 │   │   ├─ CursorPoller.cs (GetCursorPos ~30Hz)
 │   │   └─ KeyRateHook.cs (global keypress rate, rate only!)
 │   ├─ Storage/
 │   │   └─ SaveManager.cs (JSON, debounced 5s, %APPDATA%)
 │   └─ Window/
 │       └─ Win32Interop.cs (styles, hit-test, fullscreen detect, DPI)
 │
 └─ Assets/ (symlinked)
     ├─ Sprite/ (22 folders, 4081 PNGs)
     ├─ Sound/ (10 WAVs)
     └─ manifest.json (generated from PRD §5)
```

### 4.2 Key Design Decisions

| # | Decision | Rationale |
|---|----------|-----------|
| D-4 | SkiaSharp SKElement over WPF MediaElement | 60fps control, custom particle rendering, transform pipeline |
| D-5 | Event bus (not direct calls) | Decoupled modules, easy post-MVP extension (plugins) |
| D-6 | Manifest-driven assets (not hardcoded) | Runtime frame enumeration, asset lock compliance, fail-loud |
| D-7 | Win32 interop in separate class | Isolation of P/Invoke complexity, testability |
| D-8 | Single particle system with 4 emitters | Lightweight, all vector-drawn, no image assets |
| D-9 | Key-rate hook (not keylogger) | Privacy: only counts rate, never which keys, nothing stored |
| D-10 | Debounced save (5s) | Prevents disk thrash on frequent stat changes |

### 4.3 Data Flow

```
User Input (mouse/keyboard)
 │
 ├─ CursorPoller → EventBus(MouseMoved)
 ├─ KeyRateHook → EventBus(TypingBurstStarted/Ended)
 │
EventBus → FSM → StateChanged → AnimationManager → MochiRenderer → Screen
                    │
                    ├─ BehaviorPlanner (weighted random next state)
                    ├─ MoodResolver (needs → mood → planner weights)
                    ├─ NeedsTicker (periodic decay → NeedsTick)
                    ├─ AudioManager (sound on state enter, with cooldown)
                    ├─ ParticleSystem (hearts/Zzz/!/dust on state)
                    └─ SaveManager (debounced persist)
```

### 4.4 Asset Manifest Schema

```json
{
  "sprites": {
    "<StateName>": {
      "folder": "Sprite/<folder_name>",
      "mode": "holdFirstFrame|playOnce|loop|playOnceReversed|playOnceThenHoldLast",
      "speedMultiplier": 1.0
    }
  },
  "sounds": {
    "<StateName>": "Sound/<file>.wav"
  },
  "statesWithoutSound": ["IdleLeft", "IdleRight", ...],
  "soundSettings": {
    "masterVolumeDefault": 0.35,
    "blinkSoundProbability": 0.1,
    "walkSoundLoop": false,
    "walkSoundIntervalMs": 4000,
    "cooldownPerSoundMs": 8000
  }
}
```

### 4.5 Save Schema

```json
{
  "name": "Mochi",
  "level": 3,
  "xp": 120,
  "food": 70,
  "energy": 80,
  "happiness": 65,
  "mood": "happy",
  "totalFeedings": 12,
  "totalPets": 40,
  "lastSeenUtc": "2026-07-05T10:00:00Z",
  "settings": {
    "personality": 0.5,
    "volume": 0.35,
    "scale": 1.0,
    "typingAwareness": true,
    "nightMode": true,
    "cursorCuriosity": true
  }
}
```

---

## 5. Testing Strategy (Phase 5 preview)

| Layer | What | How |
|-------|------|-----|
| Unit | FSM transitions, mood resolution, needs decay, level/XP | xUnit, deterministic tests |
| Unit | Animation modes (frame enumeration, reverse, hold) | xUnit, mock file system |
| Unit | Manifest loading + AssetMissing fallback | xUnit, temp dirs |
| Integration | Save/load roundtrip, debounced write | xUnit, temp %APPDATA% |
| Manual | Transparency, click-through, focus stealing | Windows desktop (Dhar) |
| Manual | Fullscreen detection, multi-monitor, DPI | Windows desktop (Dhar) |
| Manual | Resource budget (RAM/CPU) | Task Manager |
| E2E | 30-min idle variety (≥8 behaviors) | Automated log + state counter |

---

## 6. Open Design Questions

| # | Question | Status |
|---|----------|--------|
| DQ-1 | SkiaSharp SKElement vs SKGLElement? (software vs hardware rendering) | SKElement for MVP (software, broader compat). SKGLElement if perf issues. |
| DQ-2 | Hardcodet.NotifyIcon.Wpf vs raw Win32 Shell_NotifyIcon? | Hardcodet for MVP (less code). Switch if dependency issues. |
| DQ-3 | Manifest.json generated at build time or runtime? | Runtime on first launch (enumerate folders), cached to %APPDATA%. |