# Post-MVP Phase G: Simple Feature Batch (G-1 to G-3)

**Date:** 2026-07-07
**Status:** autonomous execution
**Goal:** Add simple/medium features to Mochi in 3 phases. Each phase builds on previous.

## Phase G-1: Productivity Reminders (code-only, no new sprites)

Features that use existing sprites + speech bubble system from Phase F.

| Task | Description | Files | New Sprites? |
|------|-------------|-------|-------------|
| G-01 | Hydration reminder — every 60min cat shows speech bubble "Minum yuk! 💧" + meow | Core/Services/HydrationReminderService.cs, App.xaml.cs | No — uses Meow + SpeechBubble |
| G-02 | Daily quote/fact — morning (8-10am) cat shows inspirational quote | Core/Services/DailyQuoteService.cs | No — uses Idle + SpeechBubble |
| G-03 | Mood check-in — every 2h cat asks "Gimana mood?" via speech bubble, tray notification | Core/Services/MoodCheckInService.cs | No — uses Idle + SpeechBubble |
| G-04 | Quick launcher — tray menu shortcuts for VS Code, Browser, Terminal | UI/Tray/TrayIconController.cs | No — tray menu only |
| G-05 | Hotkey shortcuts — Ctrl+Shift+M teleport cat, Ctrl+Shift+F feed | App.xaml.cs, Infrastructure/Input | No — keyboard hook only |
| G-06 | Wire all G-1 services in DI + App.xaml.cs | Program.cs, App.xaml.cs | No |
| G-07 | Unit tests for G-1 services | tests-core/ | No |
| G-08 | Compile + test + commit | — | No |

## Phase G-2: Cat Behavior Expansion (needs minimal sprites)

Features that need some new sprite frames but can reuse existing as fallback.

| Task | Description | New Sprites Needed? |
|------|-------------|-------------------|
| G-09 | Screen-edge peek — cat peeks from screen edge when idle long | Reuse IdleLeft/IdleRight (no new sprite) |
| G-10 | Drag throw physics — cat can be thrown with mouse momentum | Reuse Fall + JumpVar1 (no new sprite) |
| G-11 | Item drops — cat occasionally drops items (fish, coin) as particles | No sprite — particle vector drawing (hearts/Zzz pattern) |
| G-12 | Ambient purring — cat purrs (looping sound) when petted 3s+ | No sprite — sound only |
| G-13 | Stats dashboard — popup window with mood/needs bars + pomodoro info | No sprite — WPF window |
| G-14 | Unit tests | — |
| G-15 | Compile + test + commit | — |

## Phase G-3: Enhanced Interaction (needs some sprites)

| Task | Description | New Sprites Needed? |
|------|-------------|-------------------|
| G-16 | Keyboard reaction — cat looks at keyboard when typing | Reuse existing — flip Idle/Walk based on typing side |
| G-17 | Weather display — tray tooltip shows weather, cat mood adjusts | No sprite — API + tray |
| G-18 | Mini ball game — cat chases a ball particle, user clicks to throw | No sprite — particle vector |
| G-19 | Night mode enhancement — cat dreams during sleep (Zzz particles + random twitch) | No sprite — particle + micro-motion |
| G-20 | Unit tests | — |
| G-21 | Compile + test + commit | — |

## Sprite Requirements Analysis

### Existing Sprites (20 animations, 200px wide, 240 frames each):
- cat_blinking_left/right (Idle, Blink)
- cat_walking_left/right/forward (Walk)
- run_1, run_2 (Run)
- jump_1, jump_2 (Jump, Fall)
- cat_sleepy_yawn (Sleep, WakeUp)
- cat_meowing_left/right (Meow)
- cat_scratching_left/right (Scratch)
- cat_angry_scratch_paw (Angry)
- cat_surpised (Surprised)
- cat_chasing_wand (Playful)
- begging_food, cat_hungry_begging_food (Hungry, Eating)

### New Sprites Needed for Future Features (NOT in G-1 to G-3):

**If Dhar wants to add these later, here are the specs:**

1. **Climbing/Jumping Up** (for window-top walking Phase E):
   - Folder: `cat_climbing_up`
   - 10-15 frames, 200×150px
   - Cat jumping upward from bottom, reaching up with paws
   - Side view, facing right
   - Same art style: white+orange cat, thick white outline, cel-shaded

2. **Looking Up** (for keyboard reaction):
   - Folder: `cat_looking_up`
   - 10 frames, 200×250px
   - Cat sitting, head tilted up, looking at screen/keyboard
   - Front view, eyes upward

3. **Drinking Water** (for hydration reminder):
   - Folder: `cat_drinking`
   - 15 frames, 200×200px
   - Cat licking water from bowl
   - Side view, tongue out

4. **Happy Hop** (for mood check-in positive response):
   - Folder: `cat_happy_hop`
   - 10 frames, 200×200px
   - Cat doing a small happy bounce/jump in place
   - Front view

5. **Sad Head Down** (for mood check-in negative response):
   - Folder: `cat_sad`
   - 10 frames, 200×200px
   - Cat sitting with head down, ears flat
   - Front view

6. **Stretching** (for after sleep wake-up):
   - Folder: `cat_stretching`
   - 15 frames, 200×200px
   - Cat doing the classic cat arch stretch
   - Side view

### Sprite Generation Options for Dhar:

**Option A: Manual (Dhar draws):**
- Use the existing cat_blinking_left frame_001 as reference
- Art style: 2D digital illustration, cel-shaded, thick white outline
- Colors: white body, orange/ginger patches, pink nose/ears, amber eyes
- Size: 200px wide, transparent background (PNG with alpha)
- 10-15 frames per animation at 24fps

**Option B: AI-assisted (Stable Diffusion + ControlNet):**
1. Train a LoRA on the existing 2400+ cat frames (20 animations × 240 frames)
2. Use ControlNet pose to generate new poses
3. Use img2img with existing frames as base for consistency
4. Post-process: remove background, add white outline, resize to 200px

**Option C: Code-based (no new sprites needed):**
- Use existing sprites with transforms (flip, rotate, scale, tint)
- Particle effects for new behaviors
- Speech bubbles for communication
- This is what G-1 to G-3 uses!

## Execution Order

Phase G-1: G-01 → G-02 → G-03 → G-04 → G-05 → G-06 → G-07 → G-08
Phase G-2: G-09 → G-10 → G-11 → G-12 → G-13 → G-14 → G-15
Phase G-3: G-16 → G-17 → G-18 → G-19 → G-20 → G-21