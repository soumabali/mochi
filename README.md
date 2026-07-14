# 🐱 Mochi v2 — Desktop Cat Companion

Mochi adalah aplikasi desktop companion berbentuk kucing yang hidup di layar Anda. Dia berjalan, berlari, tidur, makan, bermain, dan bereaksi terhadap interaksi Anda — seperti kucing sungguhan!

Dibuat dengan **WPF (.NET 9, C#)** dan **SkiaSharp** rendering, Mochi hadir sebagai overlay transparan yang melayang di desktop Anda.

---

## ✨ Fitur

### 🎭 Animasi & Perilaku
| Fitur | Deskripsi | Status |
|------|-----------|--------|
| 🚶 Berjalan | Kiri, kanan, dan maju dengan sprite walking | ✅ |
| 🏃 Berlari | 2 varian animasi run (kiri/kanan) | ✅ |
| ⬆️ Lompat | 2 varian jump dengan arc physics | ✅ |
| 😴 Tidur | Yawn animation → hold sleeping pose, auto-wake setelah 10 menit | ✅ |
| 🐾 Bermain | Chasing wand animation (loop) | ✅ |
| 🐟 Makan | Feed via tray menu → eating animation | ✅ |
| 💧 Minum | Drinking animation via hydration reminder | ✅ |
| 😾 Marah | Saat di-drag dan dilepas → angry + fall physics | ✅ |
| 😲 Surprised | Fast cursor dekat kucing → surprised "!" reaction | ✅ |
| 💕 Petting | Hover cursor 3 detik di dekat kucing → hearts | ✅ |
| 🧹 Scratch | Scratch kiri/kanan (play-once) | ✅ |
| 😸 Meow | Meow kiri/kanan dengan audio | ✅ |
| 🤸 Stretching | Stretch setelah bangun tidur | ✅ |
| 🐰 Happy Hop | Bounce kecil saat mood happy | ✅ |
| 🗡️ Climb | Window-top surface climbing | ✅ |

### 🧠 Sistem Cerdas
| Fitur | Deskripsi | Status |
|------|-----------|--------|
| 🎭 Mood System | Happy, Content, Tired, Sad, Hungry — derived dari needs (food/energy/happiness) | ✅ |
| 📋 Scenario System | 8 data-driven scenarios (JSON) dengan bridging animations | ✅ |
| 🔄 FSM | Custom state machine dengan 27 states, transition graph, bypass validation | ✅ |
| 🧪 Behavior Planner | Mood-aware, personality-shifted weighted random behavior selection | ✅ |
| 🎯 Mood-FSM Sync | Displayed mood selalu compatible dengan active animation state | ✅ |

### 🛠️ Utilitas
| Fitur | Deskripsi | Status |
|------|-----------|--------|
| 💬 Chat | Chat window dengan LLM (OpenAI-compatible API) | ✅ |
| 📊 Stats Dashboard | Needs, mood, level, XP display | ✅ |
| 🍅 Pomodoro | Built-in pomodoro timer | ✅ |
| 💧 Hydration Reminder | Periodic reminder to drink water | ✅ |
| 🌙 Night Mode | Auto-detect night time, sleep bias | ✅ |
| 🎮 Mini Ball Game | Play ball with Mochi | ✅ |
| 🗨️ Speech Bubble | Popup text for mood check-in, quotes | ✅ |
| 🔊 Audio | Per-state sound mapping (21 .ogg files, NAudio) | ✅ |
| 🔄 Wrap-around | Cat exits right edge → re-enters left (configurable) | ✅ |

---

## 🛠️ Tech Stack

| Layer | Technology |
|-------|-----------|
| Framework | WPF (.NET 9, C#) |
| Rendering | SkiaSharp (SKElement, 60fps) |
| Audio | NAudio (OGG playback, mixing) |
| Logging | Serilog (Console + File sinks) |
| Window | Win32 interop (click-through, topmost, no-activate) |
| Animation | Custom AnimationManager (configurable per-animation FPS, sprite-based) |
| State Machine | Custom FSM + FSMBuilder + BehaviorPlanner + ScenarioPlayer |
| Tests | xUnit (325 tests, headless on Linux) |

---

## 📁 Struktur Project

```
mochi/
├── App/                        # WPF application (entry point, UI, overlay)
│   ├── App.xaml                 # WPF app definition
│   ├── App.xaml.cs              # Main app — 60fps render loop, input handling
│   ├── Program.cs               # Service container bootstrap (DI)
│   ├── MochiV2.csproj           # Project file (.NET 9, WPF)
│   ├── UI/                      # Overlay, Chat, Stats, SpeechBubble windows
│   │   ├── Overlay/             # Transparent overlay + SkiaSharp renderer
│   │   ├── Chat/                # LLM chat window
│   │   ├── Stats/               # Needs/mood dashboard
│   │   └── SpeechBubble/        # Popup text bubble
│   └── Infrastructure/           # Win32 interop, DPI, fullscreen detection
│
├── Core/                        # Platform-agnostic core logic (testable on Linux)
│   ├── Animation/               # AnimationManager, AnimationController
│   │   ├── AnimationManager.cs  # Cache, transition cooldown, auto-return
│   │   └── AnimationController.cs # Frame advancement, FPS, minDuration
│   ├── Behavior/                # FSM, BehaviorPlanner, ScenarioPlayer
│   │   ├── FSM.cs                # Custom finite state machine
│   │   ├── FSMBuilder.cs         # State transitions + dead-end validation
│   │   ├── BehaviorPlanner.cs    # Mood-weighted random behavior selection
│   │   ├── ScenarioPlayer.cs     # Data-driven scenario sequences (JSON)
│   │   └── InteractionHandler.cs # Mouse proximity, petting, drag detection
│   ├── Events/                  # EventBus, domain events
│   ├── Models/                  # FSMState (27 states), SaveData, AssetManifest
│   ├── Physics/                  # PhysicsEngine (drag-release fall, gravity)
│   ├── Particles/                # Particle system (hearts, Zzz, dust, surprised)
│   └── Services/                 # MoodResolver, SleepService, FeedingService, etc.
│       ├── MoodResolver.cs       # Needs-based mood resolution with hysteresis
│       ├── MoodFSMCompatibility.cs # Mood-FSM sync (displayed mood never contradicts sprite)
│       ├── SleepService.cs        # Sleep/wake cycle, auto-wake after 10 min
│       ├── FeedingService.cs     # Feed interaction with cooldown
│       └── ...                   # 20+ services
│
├── Infrastructure/              # Cross-cutting infrastructure
│   └── Audio/                   # AudioManager (NAudio, state→sound mapping)
│
├── Assets/                      # Game assets
│   ├── Sprite_optimized/         # 23 sprite folders (24 PNG frames each, ~16MB total)
│   ├── Sound/                    # 21 OGG audio files
│   ├── manifest.json             # Sprite→folder mapping + fps + minDuration + speed
│   └── scenarios.json            # 8 scenario definitions (JSON, data-driven)
│
├── UI/                           # Tray icon, settings window
├── tests/                        # WPF integration tests
├── tests-core/                   # Core-only xUnit tests (325 tests, runs on Linux)
├── deploy/                       # Deployment configs
├── scripts/                      # Build/utility scripts
├── packages/                     # NuGet packages
├── MochiV2.sln                   # Visual Studio solution
└── Makefile                      # Build targets
```

---

## 🎮 Sprite System

Mochi menggunakan sprite-based animation dengan **23 folder sprite** (24 frame per folder, 8-bit PNG with transparency).

### Playback Modes
| Mode | Behavior |
|------|----------|
| `holdFirstFrame` | Display first frame only (Idle) |
| `loop` | Cycle through frames continuously (Walk, Run, Playful, Eating) |
| `playOnce` | Play forward once, then finish (Jump, Meow, Scratch, Surprised) |
| `playOnceReversed` | Play backward once (WakeUp, Fall) |
| `playOnceThenHoldLast` | Play forward, hold last frame (Sleeping) |

### Configurable Parameters (manifest.json)
| Parameter | Description | Default |
|-----------|-------------|---------|
| `fps` | Frames per second for this animation | 10 |
| `minDurationMs` | Minimum duration before animation can finish | 0 |
| `speedMultiplier` | Playback speed multiplier | 1.0 |

### Sprite Folders
| Folder | FSM State | Mode |
|--------|-----------|------|
| cat_blinking_left | Idle, Blink | holdFirstFrame / playOnce |
| cat_blinking_right | IdleRight, BlinkRight | holdFirstFrame / playOnce |
| cat_idle_right | IdleRight | holdFirstFrame |
| cat_walking_left | WalkLeft | loop |
| cat_walking_right | WalkRight | loop |
| cat_walking_forward | WalkForward | loop |
| run_1 | RunVar1 (left) | loop |
| run_2 | RunVar2 (right) | loop |
| jump_1 | JumpVar1, Fall | playOnce / playOnceReversed |
| jump_2 | JumpVar2, FallVar2 | playOnce / playOnceReversed |
| cat_sleepy_yawn | Sleeping, WakeUp | playOnceThenHoldLast / playOnceReversed |
| cat_meowing_left | MeowLeft | playOnce |
| cat_meowing_right | MeowRight | playOnce |
| cat_scratching_left | ScratchLeft | playOnce |
| cat_scratching_right | ScratchRight | playOnce |
| cat_angry_scratch_paw | Angry | loop |
| cat_surpised | Surprised, Drag | playOnce |
| cat_chasing_wand | Playful | loop |
| begging_food | HungryStandard | loop |
| cat_hungry_begging_food | HungryCritical, Eating | loop |
| cat_stretching | Stretching | playOnce |
| cat_drinking | Drinking | playOnce |
| cat_happy_hop | HappyHop | playOnce |

---

## 🎭 Scenario System

Scenario system mendrive perilaku Mochi secara natural dengan data-driven JSON config.

| Scenario | Trigger | Sequence |
|----------|---------|----------|
| S1 Morning Wake | On wake | Sleeping → WakeUp → Stretching → Blink → Walk → Idle |
| S2 Play Session | Random idle (25%) | Idle → Surprised → Run → Playful → Idle → Scratch → Idle |
| S3 Hunger Arc | Food < 40 | Idle → Meow ×2 → WalkForward → Begging → Eating → HappyHop → Stretching → Idle |
| S4 Wander Patrol | Random idle (25%) | Idle → WalkRight → Idle → Blink ×2 → Idle |
| S5 Startle Flee | Cursor near sleeping | Sleeping → Surprised → Jump → Run away → Idle |
| S6 Drink Water | Random idle (15%) | Idle → WalkForward → Drinking → Idle → HappyHop → Idle |
| S7 Emotional Burst | Random idle (30%) | Idle → Meow → Blink → Scratch → Surprised → Idle |
| S8 Stretch & Relax | Random idle (20%) | Idle → Stretching → Blink → Idle |

Bridging animations: illegal direct transitions (e.g., Sleeping → RunVar1) route through intermediate states (WakeUp → Stretching → Idle → RunVar1).

---

## 🚀 Build & Run

### Prerequisites
- .NET 9 SDK
- Windows 10/11 (WPF requires Windows)
- Visual Studio 2022 or VS Code (optional)

### Build
```bash
dotnet build App/MochiV2.csproj
```

### Run Tests (headless, works on Linux)
```bash
dotnet test tests-core/tests-core.csproj
# Expected: 325/325 passed
```

### Build Release Bundle
```bash
dotnet publish App/MochiV2.csproj -c Release -r win-x64 --self-contained false
```

### GitHub Actions
Project includes CI/CD via GitHub Actions — auto-builds on every push to `main`, produces `MochiV2-win-x64-bundle.zip` artifact.

---

## 🎨 Sprite Specifications

| Property | Value |
|----------|-------|
| Style | 2D digital illustration, white/ginger bicolor cat |
| Width | 200px (display 150px × DPI scale) |
| Format | 8-bit PNG with transparency |
| Outline | Thick white outline surrounding figure |
| Frames per animation | 24 frames |
| File naming | `frame_001.png` → `frame_024.png` |
| Total sprite size | ~16MB (optimized from 75MB original) |

---

## 🧪 Testing

| Test Suite | Count | Platform |
|------------|-------|----------|
| tests-core | 325 tests | Linux (headless, no WPF) |
| tests | WPF integration | Windows only |

Test coverage: FSM transitions, animation controller, mood resolver, save/load, asset manifest, scenario player, mood-FSM compatibility, physics engine, behavior planner, particle system, and more.

---

## 📊 Project Stats

| Metric | Value |
|--------|-------|
| C# source files | 101 |
| xUnit tests | 325 |
| Sprite folders | 23 (552 frames total) |
| Audio files | 21 OGG |
| Scenarios | 8 (data-driven JSON) |
| FSM states | 27 |
| NuGet dependencies | SkiaSharp, NAudio, Serilog, DI, H.NotifyIcon |

---

## 📝 License

Private project — © soumabali

---

Made with 💚 by Dhar & Ame