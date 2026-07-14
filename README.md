# 🐱 Mochi v2 — Desktop Cat Companion

Mochi adalah aplikasi desktop companion berbentuk kucing yang hidup di layar Anda. Dia berjalan, berlari, tidur, makan, bermain, dan bereaksi terhadap interaksi Anda —seperti kucing sungguhan!

## ✨ Fitur

| Fitur | Status |
|-------|--------|
| 🚶 Berjalan (kiri/kanan/maju) | ✅ |
| 🏃 Berlari (2 varian) | ✅ |
| ⬆️ Lompat (2 varian) | ✅ |
| 😴 Tidur & auto-wake (10 menit) | ✅ |
| 🐾 Bermain (chasing wand) | ✅ |
| 🐟 Makan (feed menu) | ✅ |
| 💧 Minum (hydration reminder) | ✅ |
| 😾 Marah saat di-drag | ✅ |
| 😲 Surprised saat cursor cepat | ✅ |
| 💕 Petting (hover 3 detik → hearts) | ✅ |
| 🧹 Scratch (kiri/kanan) | ✅ |
| 😸 Meow (kiri/kanan) | ✅ |
| 🤸 Stretching setelah bangun | ✅ |
| 🐰 Happy Hop | ✅ |
| 🗡️ Window-top climbing | ✅ |
| 💬 Chat dengan LLM | ✅ |
| 📊 Stats dashboard | ✅ |
| 🍅 Pomodoro timer | ✅ |
| 💧 Hydration reminder | ✅ |
| 🌙 Night mode | ✅ |
| 🎮 Mini ball game | ✅ |
| 🗨️ Speech bubble | ✅ |
| 🔊 Audio per-state (NAudio) | ✅ |
| 🔄 Wrap-around screen edge | ✅ |
| 🎭 Mood system (happy/content/tired/sad/hungry) | ✅ |
| 📋 Scenario system (6+ scenarios) | ✅ |

## 🛠️ Tech Stack

| Layer | Technology |
|-------|-----------|
| Framework | WPF (.NET 9, C#) |
| Rendering | SkiaSharp (SKElement) |
| Audio | NAudio |
| Logging | Serilog |
| Window | Win32 interop (click-through, topmost, no-activate) |
| Animation | Custom AnimationManager (configurable FPS, sprite-based) |
| State Machine | Custom FSM + BehaviorPlanner + ScenarioPlayer |
| Tests | xUnit (325 tests) |

## 📁 Struktur Project

```
mochi/
├── App/                    # WPF application (entry point, UI, overlay)
│   ├── App.xaml.cs          # Main app — render loop, input handling
│   ├── Program.cs           # Service container bootstrap
│   ├── UI/                  # Overlay, Chat, Stats, SpeechBubble windows
│   └── Infrastructure/      # Win32 interop, DPI, fullscreen detection
├── Core/                   # Platform-agnostic core logic
│   ├── Animation/          # AnimationManager, AnimationController
│   ├── Behavior/           # FSM, FSMBuilder, BehaviorPlanner, ScenarioPlayer
│   ├── Events/              # EventBus, domain events
│   ├── Models/              # FSMState, SaveData, AssetManifest
│   ├── Physics/             # PhysicsEngine (drag-release fall)
│   ├── Particles/           # Particle system (hearts, Zzz, dust)
│   └── Services/            # MoodResolver, SleepService, FeedingService, etc.
├── Assets/                  # Sprites, audio, manifest, scenarios
│   ├── Sprite_optimized/    # 23 sprite folders (24 frames each)
│   ├── Sound/               # 21 .ogg audio files
│   ├── manifest.json        # Sprite → folder mapping + fps + duration
│   └── scenarios.json       # Data-driven scenario definitions
├── Infrastructure/          # Audio (AudioManager, NAudio)
├── UI/                      # Tray icon, settings
├── tests/                   # WPF integration tests
├── tests-core/              # Core-only xUnit tests (325 tests)
├── deploy/                  # Deployment configs
├── scripts/                 # Build/utility scripts
├── packages/                # NuGet packages
├── MochiV2.sln              # Visual Studio solution
└── Makefile                 # Build targets
```

## 🎮 Sprite System

Mochi menggunakan sprite-based animation dengan 23 folder sprite (24 frame per folder):

- **Loop animations**: Walk (kiri/kanan/maju), Run (2 varian), Playful, Angry, Hungry (standard/critical), Eating
- **Play-once animations**: Jump (2 varian), Meow (kiri/kanan), Scratch (kiri/kanan), Surprised, Blink, Stretching, Drinking, HappyHop, ClimbUp
- **Special modes**: Sleeping (playOnceThenHoldLast), WakeUp (playOnceReversed), Fall (playOnceReversed)
- **Configurable**: Per-animation FPS, minimum duration, speed multiplier (via `manifest.json`)

## 🎭 Scenario System

Scenario system mendrive perilaku Mochi secara natural:

| Scenario | Trigger | Sequence |
|----------|---------|----------|
| S1 Morning Wake | On wake | Sleeping → WakeUp → Stretching → Blink → Walk → Idle |
| S2 Play Session | Random idle | Idle → Surprised → Run → Playful → Idle → Scratch → Idle |
| S3 Hunger Arc | Food < 40 | Idle → Meow ×2 → WalkForward → Begging → Eating → HappyHop → Stretching → Idle |
| S4 Wander Patrol | Random idle | Idle → WalkRight → Idle → Blink ×2 → Idle |
| S5 Startle Flee | Cursor near sleeping | Sleeping → Surprised → Jump → Run away → Idle |
| S6 Drink Water | Random idle | Idle → WalkForward → Drinking → Idle → HappyHop → Idle |
| S7 Emotional Burst | Random idle | Idle → Meow → Blink → Scratch → Surprised → Idle |
| S8 Stretch & Relax | Random idle | Idle → Stretching → Blink → Idle |

## 🚀 Build

### Prerequisites
- .NET 9 SDK
- Windows (WPF requires Windows)

### Build
```bash
cd App
dotnet build MochiV2.csproj
```

### Run Tests
```bash
cd tests-core
dotnet test
```

### Build Bundle
```bash
dotnet publish App/MochiV2.csproj -c Release -r win-x64 --self-contained
```

## 🎨 Sprite Specifications

- **Style**: 2D digital illustration, white/ginger bicolor cat
- **Size**: 200px wide, 8-bit PNG with transparency
- **Outline**: Thick white outline
- **Frames**: 24 frames per animation folder
- **Naming**: `frame_001.png` through `frame_024.png`

## 📝 License

Private project — © soumabali

---

Made with 💚 by Dhar & Ame