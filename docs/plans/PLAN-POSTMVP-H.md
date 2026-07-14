# Post-MVP Phase H: Deep Integration + New Animations

**Date:** 2026-07-07
**Status:** autonomous execution
**Goal:** Wire AI-generated sprites into manifest, add new behaviors that use them, create stats dashboard window, and integrate weather/keyboard reactions visually.

## Phase H-1: Sprite Integration + New Behaviors

| Task | Description | Files | Sprites |
|------|-------------|-------|---------|
| H-01 | Add cat_stretching to manifest.json as Stretching state | manifest.json, FSMState.cs, FSMBuilder.cs | cat_stretching (4 frames, done) |
| H-02 | Wire Stretching into behavior planner (after WakeUp → Stretching → Idle) | BehaviorPlanner.cs | — |
| H-03 | Generate more AI sprites: cat_drinking (4 frames), cat_happy_hop (4 frames) | HuggingFace SDXL-Turbo | NEW |
| H-04 | Post-process new sprites (bg removal, resize 200px) | Python PIL | — |
| H-05 | Add Drinking + HappyHop states to manifest + FSM | manifest.json, FSMState.cs, FSMBuilder.cs | cat_drinking, cat_happy_hop |
| H-06 | Wire Drinking to hydration reminder (when user clicks "I drank water" → cat drinks) | App.xaml.cs | — |
| H-07 | Wire HappyHop to mood check-in positive response | App.xaml.cs | — |
| H-08 | Unit tests for new states + transitions | tests-core/ | — |
| H-09 | Compile + test + commit | — | — |

## Phase H-2: Stats Dashboard Window

| Task | Description | Files |
|------|-------------|-------|
| H-10 | Create StatsWindow WPF (transparent popup, shows needs bars + mood + level) | UI/Stats/StatsWindow.xaml + .cs |
| H-11 | Wire StatsWindow to tray "Stats" menu item | TrayIconController.cs, App.xaml.cs |
| H-12 | Show pomodoro status in stats window | StatsWindow.xaml |
| H-13 | Show weather info in stats window | StatsWindow.xaml |
| H-14 | Compile + test + commit | — |

## Phase H-3: Visual Behavior Integration

| Task | Description | Files |
|------|-------------|-------|
| H-15 | Weather mood integration — cat mood shifts based on weather | App.xaml.cs, WeatherService |
| H-16 | Keyboard reaction visual — cat faces toward typing direction | App.xaml.cs |
| H-17 | Night mode dreams — Zzz particles + random ear twitch during sleep | App.xaml.cs, ParticleSystem |
| H-18 | Ball game rendering — draw ball as red circle particle near cat | MochiRenderer.cs |
| H-19 | Item drop rendering — draw fish/coin/heart/star as colored shapes | MochiRenderer.cs |
| H-20 | Compile + test + commit | — |

## Execution Order

H-1: H-01 → H-02 → H-03 → H-04 → H-05 → H-06 → H-07 → H-08 → H-09
H-2: H-10 → H-11 → H-12 → H-13 → H-14
H-3: H-15 → H-16 → H-17 → H-18 → H-19 → H-20