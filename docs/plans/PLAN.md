# PLAN — mochi-v2 (Neko Desktop Companion)

**Status:** draft → auto-approved (autonomous mode)
**Date:** 2026-07-05
**Source:** .hermes/PRD.md + .hermes/DESIGN.md (both approved)

## Milestones

| Milestone | Pipeline phase | Task IDs | Definition of done |
|-----------|---------------|----------|-------------------|
| M1: Foundation | 4 (BUILD) | T-001..T-004 | Transparent window + manifest loader + sprite renderer compiles, renders test sprite at 60fps |
| M2: Animation + FSM | 4 (BUILD) | T-005..T-008 | FSM core + animation playback modes + walk/idle/blink + screen-edge movement |
| M3: Interaction + Physics | 4 (BUILD) | T-009..T-012 | Mouse interaction + drag/release + squash/stretch + particles + sound manager |
| M4: Needs + Behavior | 4 (BUILD) | T-013..T-016 | Needs/mood system + feeding/sleeping + behavior planner + cursor curiosity + typing awareness |
| M5: UI + Persistence | 4 (BUILD) | T-017..T-020 | Tray icon + context menu + settings window + JSON save/load + night mode + fullscreen detect |
| M6: Test pass | 5 (TEST) | T-021..T-024 | Unit tests (FSM, mood, manifest, save) + integration + manual checklist + resource budget |
| M7: Fix + Close | 6-7 (FIX/CLOSE) | T-025+ | Zero open bugs; final report; ready for Windows desktop testing |

## Estimation

Derived from PRD counts: 4 screens, 5 entities, 15 event bus events, 28 FRs, 10 ACs → **20 build tasks + 4 test tasks + fix tasks** = ~26 tasks.

Day-by-day alignment with PRD §15:
- M1 = Day 1 | M2 = Day 2 | M3 = Day 3 | M4 = Day 4-5 | M5 = Day 5-6 | M6-M7 = Day 7

## Task table

Lives in `.hermes/TASKS.md` (ledger). See below.

## Risks

- **R1: .NET 9 SDK not on server.** Mitigation: install via dotnet-install script. Build/compile on server, visual QA on Dhar's Windows 11.
- **R2: Win32 interop untestable on Linux.** Mitigation: compile-only verification on server. All Win32/visual tests are manual on Windows.
- **R3: 2.1GB assets not in git.** Mitigation: symlinked from workspace. Claude Code dispatches reference `02-application/Assets/` symlink.
- **R4: SkiaSharp Linux rendering differs from Windows.** Mitigation: unit tests for logic-only (FSM, manifest, mood). Visual tests deferred to Dhar.
- **R5: Claude Code executor scope.** Mitigation: tasks sized ≤300 lines, one module each, tight file scope.