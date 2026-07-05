# CLAUDE.md — Orchestrator Context for mochi-v2

## Role Split

- **Hermes (Ame):** Orchestrator. Plans tasks, dispatches to executor, verifies results, manages ledger, commits.
- **Claude Code (executor):** Writes code inside `02-application/` only. Receives task specs, implements, reports back.

## Project Layout

```
mochi-v2/
├── .hermes/          ← State ledger (Hermes only)
│   ├── PRD.md        ← Requirements (28 FRs, 10 ACs)
│   ├── DESIGN.md     ← Architecture + design tokens
│   ├── PLAN.md       ← 7 milestones
│   ├── TASKS.md      ← 24 tasks (T-001..T-024)
│   ├── CONSTITUTION.md ← Project rules (asset lock!)
│   └── project.json  ← Phase tracking
├── 02-application/   ← ALL code lives here (executor scope)
│   ├── App/          ← Entry, bootstrap, mutex
│   ├── Core/         ← Animation, Behavior, Events, Models, Particles, Physics, Services
│   ├── Infrastructure/ ← Audio, Input, Storage, Window
│   ├── UI/           ← Overlay, Settings, Tray
│   ├── Assets/       ← Symlinked (Sprite + Sound + manifest.json)
│   └── tests/        ← xUnit tests
├── AGENTS.md         ← Hard rules (read first!)
└── CLAUDE.md         ← This file
```

## Key Constraints for Executor

1. **Asset lock absolute** — see AGENTS.md §3 + CONSTITUTION.md
2. **Target: .NET 9 WPF** — but compile on Linux server (no Win32 runtime). Logic must compile cross-platform; Win32 P/Invoke guarded with `[SupportedOSPlatform("windows")]`.
3. **Tasks ≤300 lines each** — one module per task, tight file scope
4. **Test what's testable on Linux** — FSM logic, mood resolution, manifest loading, save/load, event bus. Win32/UI/visual tests are manual (Dhar's Windows 11).
5. **Use design tokens from DESIGN.md** for all UI (Settings window, tray menu strings)
6. **Serilog everywhere** — no silent exceptions
7. **Event bus for decoupling** — modules communicate via EventBus, not direct calls

## Build Commands

```bash
export PATH="$HOME/.dotnet:$PATH"
cd 02-application
dotnet build
dotnet test
```

## Verify Before Reporting Done

1. `dotnet build` — zero errors, zero warnings
2. `dotnet test` — all tests pass
3. Code matches DESIGN.md architecture (correct namespaces, file locations)
4. No TODOs for "future assets"
5. Acceptance criteria from TASKS.md verified