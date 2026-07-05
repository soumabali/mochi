# AGENTS.md — Hard Rules for All Agents on mochi-v2

This file applies to **every agent, subagent, coding assistant, automated tool** that operates on project mochi-v2. No agent is exempt. Read this before doing anything.

---

## 1. You Must Read Context First

Before executing any task, read:
1. `AGENTS.md` (this file)
2. `.hermes/CONSTITUTION.md` (project rules — ASSET LOCK is there)
3. `.hermes/PRD.md` (requirements — source of truth)
4. `.hermes/DESIGN.md` (architecture + tokens)
5. `.hermes/TASKS.md` (your task ID + acceptance criteria)
6. The task spec JSON if dispatched via dispatch-claude.sh

If a task conflicts with these files, **stop and report back to Hermes (Ame)** before proceeding.

---

## 2. Mandatory Workflow

The only allowed workflow for non-trivial changes:

```
Plan → Dispatch → Execute → Verify → Commit → Document
```

- **Hermes (Ame):** plan, dispatch, verify, commit, document
- **Claude Code / executor:** execute code inside `02-application/` only

No agent may:
- Skip verification gates
- Edit `.hermes/` ledger files (Hermes only)
- Edit root context files unless explicitly instructed
- Push to any remote without Hermes instruction

---

## 3. Asset Lock (NON-NEGOTIABLE)

From PRD §0 and CONSTITUTION.md:

1. **NO new art/sound assets ever.** Use only what's in `02-application/Assets/`
2. **Never hardcode frame counts.** Enumerate with `Directory.GetFiles(dir, "*.png")` at runtime
3. **AssetManifest is the only mapping layer.** Don't derive folder names from enum names (typo: `cat_surpised`)
4. **Fail loud:** missing asset → `AssetMissing` event + Serilog log + fallback to Idle
5. **Horizontal flip FORBIDDEN** for sprites (asymmetric markings). Flip only for particles.
6. **Excluded:** `flip_folders.py`, `video/` folder

---

## 4. Tech Stack

- C# / ..NET 9 / WPF
- SkiaSharp (SKElement) for rendering
- NAudio for audio
- Serilog for logging
- Microsoft.Extensions.DependencyInjection
- Hardcodet.NotifyIcon.Wpf for tray
- System.Text.Json for save/config

---

## 5. Git Rules

- Clean git history, no generated files committed
- Assets are symlinked (gitignored) — do NOT stage Assets/Sprite or Assets/Sound
- Commit messages: `T-XXX: <description>` format
- No `.git` inside `02-application/` (subtree pattern)

---

## 6. Code Quality

- All public methods documented with XML comments
- All classes have unit tests (where logic permits — Win32/UI tests are manual)
- No TODO comments that suggest "future assets" (asset lock)
- No silent catches — log everything via Serilog
- Use `var` only when type is obvious; prefer explicit types for clarity