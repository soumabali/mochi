# Post-MVP Phase E: Window-Top Walking (Surface Traversal)

**Date:** 2026-07-07
**Status:** draft (pending Dhar approval)
**Goal:** Mochi walks along the top edge (title bar) of visible desktop windows — not just bottom screen edge. Cat can jump up to a window, walk across its top, and fall back down when the window moves or closes.

**PRD ref:** §5 Out-of-scope → now in-scope: "Window-top walking, window collision (post-MVP; MVP is bottom-edge only)"

## Architecture

```
Current (MVP):
  MovementService → IWorkAreaProvider (bottom-edge only, Y = WorkArea.Bottom - spriteHeight)

Post-MVP Phase E:
  ISurfaceProvider → enumerates WalkableSurface[] (visible window title bars)
  MovementService → uses current surface for Y; falls to next surface / bottom when surface disappears
  Win32Interop + EnumWindows → finds visible top-level windows, filters to those with title bars
  PhysicsEngine → reuse for fall when surface removed
```

### New Types

| Type | File | Purpose |
|------|------|---------|
| `WalkableSurface` | Core/Models/WalkableSurface.cs | readonly struct: Left, Top, Right, SurfaceHandle (HWND) |
| `ISurfaceProvider` | Core/Behavior/ISurfaceProvider.cs | Interface: `WalkableSurface[] GetSurfaces()` + `event Action SurfacesChanged` |
| `Win32SurfaceProvider` | Infrastructure/Window/Win32SurfaceProvider.cs | EnumWindows P/Invoke, filters visible+titled windows, caches list, polls on timer/SystemEvents |
| `SurfaceClimber` | Core/Behavior/SurfaceClimber.cs | Decides when to jump to a nearby surface, animates the climb arc |

### Modified Types

| Type | Changes |
|------|---------|
| `MovementService` | Add `CurrentSurface` (null = bottom-edge). When walking on surface, Y = surface.Top - spriteHeight. Add `TransitionToSurface(WalkableSurface)`. When surface disappears, trigger Fall. |
| `Win32Interop` | Add `EnumWindows` P/Invoke, `IsWindowVisible`, `GetWindowRect` (already exists), `GetWindowTextLength` |
| `FSMState` | Add `ClimbUp`, `ClimbDown` states (optional — or reuse JumpVar1/JumpVar2 for climb animation) |
| `FSMBuilder` | Add transitions: Idle→ClimbUp (when surface nearby), ClimbUp→WalkLeft/WalkRight (on surface), any→Fall (surface gone) |
| `BehaviorPlanner` | Add weight for "climb to surface" behavior (mood/personality gated — chaotic more likely to climb) |
| `App.xaml.cs` | Wire ISurfaceProvider → MovementService. Subscribe to SurfacesChanged. |

## Tasks

| Task | Description | Files | Acceptance | Status |
|------|-------------|-------|------------|--------|
| E-01 | Add `WalkableSurface` model + `ISurfaceProvider` interface | Core/Models/WalkableSurface.cs, Core/Behavior/ISurfaceProvider.cs | Compiles. Surface has Left/Top/Right/HWND. Interface has GetSurfaces() + SurfacesChanged event. | todo |
| E-02 | Add `EnumWindows` + helpers to Win32Interop | Infrastructure/Window/Win32Interop.cs | EnumWindows P/Invoke compiles. IsWindowVisible, GetWindowTextLength added. Returns visible windows with title bars > 0 width. | todo |
| E-03 | Implement `Win32SurfaceProvider` | Infrastructure/Window/Win32SurfaceProvider.cs | Enumerates visible top-level windows. Filters: visible, has title, width > 50px, not overlapped by fullscreen. Caches list, refreshes on SystemEvents.DisplaySettingsChanged + 5s poll. Returns WalkableSurface[]. | todo |
| E-04 | Extend `MovementService` for surface walking | Core/Behavior/MovementService.cs | Position.Y = surface.Top - spriteHeight when on surface. SurfaceLeft/Right clamping. IsAtSurfaceEdge. TurnedAround works on surface. When CurrentSurface null, falls back to bottom-edge. | todo |
| E-05 | Add `SurfaceClimber` service | Core/Behavior/SurfaceClimber.cs | Finds nearest surface within jump range (max 300px vertical). Calculates arc trajectory. Emits ClimbStarted/Completed events. Cat appears to jump from bottom to surface top. | todo |
| E-06 | Extend FSM for surface states | Core/Models/FSMState.cs, Core/Behavior/FSMBuilder.cs | ClimbUp state (playOnce animation, reuses JumpVar1). Transition Idle→ClimbUp→WalkLeft/Right. Surface-gone → Fall → Idle (bottom). | todo |
| E-07 | Wire surface behavior in BehaviorPlanner | Core/Behavior/BehaviorPlanner.cs | 10% chance (chaotic personality > 0.6: 20%) to climb when surface nearby. After walking on surface 5-15s, chance to climb back down. | todo |
| E-08 | Wire ISurfaceProvider into App.xaml.cs | App/App.xaml.cs | DI register Win32SurfaceProvider → ISurfaceProvider. Subscribe SurfacesChanged → check CurrentSurface still exists. MovementService gets surface updates. | todo |
| E-09 | Surface-gone fall handling | Core/Behavior/MovementService.cs, Core/Physics/PhysicsEngine.cs | When current surface disappears (window closed/moved), cat enters Fall state, physics takes over, lands on bottom edge or lower surface. | todo |
| E-10 | Unit tests: surface provider, movement on surface, climb | tests-core/SurfaceTests.cs, tests-core/SurfaceMovementTests.cs | Mock ISurfaceProvider. Test: cat Y = surface.Top - spriteHeight. Surface disappears → Fall triggered. Climb arc calculated correctly. Edge turn-around on surface. ≥15 tests. | todo |
| E-11 | Compile verification + existing tests pass | — | `dotnet build` 0 errors on Linux. All 221+ existing tests + new surface tests pass. | todo |

## Execution Order

1. **E-01** — Model + interface (foundation, no deps)
2. **E-02** — Win32 P/Invoke (independent, compile-only on Linux)
3. **E-03** — Win32SurfaceProvider (depends E-01, E-02)
4. **E-04** — MovementService surface mode (depends E-01)
5. **E-05** — SurfaceClimber (depends E-01, E-04)
6. **E-06** — FSM states + transitions (depends E-04, E-05)
7. **E-07** — BehaviorPlanner weights (depends E-06)
8. **E-08** — App wiring (depends E-03, E-04, E-07)
9. **E-09** — Fall handling (depends E-04, E-08)
10. **E-10** — Tests (depends E-01..E-09)
11. **E-11** — Full compile + test verification

## Risks

- **R-E1: EnumWindows performance.** Mitigation: cache list, refresh every 5s or on display change, not every frame.
- **R-E2: Linux compile — no Win32.** Mitigation: all P/Invoke behind `#if WINDOWS`, interface/mock for tests.
- **R-E3: Surface flicker — windows appearing/disappearing rapidly.** Mitigation: hysteresis — cat stays on current surface for min 3s before re-evaluating.
- **R-E4: Window moves while cat on it.** Mitigation: poll surface position each frame; if surface moved, cat moves with it (sticky). If surface gone, Fall.

## Design Decisions

- **D-E1: Reuse existing Walk states** — WalkLeft/WalkRight already work; only Y coordinate source changes. No new walk states needed.
- **D-E2: ClimbUp reuses JumpVar1** — no new sprite asset needed. Cat jumps up with existing jump animation.
- **D-E3: Surface = title bar top** — cat stands on the top edge of the window rect (title bar). Left/Right = window left/right edges.
- **D-E4: Max 1 surface at a time** — cat walks on one surface; doesn't jump between surfaces mid-walk. Falls first, then climbs new one.