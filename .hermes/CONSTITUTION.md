# Project Constitution: mochi-v2

Non-negotiable rules for this project. Every BUILD/FIX task spec
inherits this file verbatim as `constraints` (phase-4 rule 0).
Hermes edits this file only; change = append dated amendment at bottom.

## Standards in effect (pluggable — pick per project)

- language(s): `standards/languages/csharp.md` (to be created if needed)
- framework(s): WPF .NET 8
- testing: `standards/testing/functional.md`
- code quality: `references/code-quality.md` (cross-language anti-debt gate, injected every BUILD/FIX task)
- test quality: `references/test-quality.md` (anti-test-theater gate)

## Tech invariants

- Language/runtime: C# / .NET 8 / WPF
- Stack pins: .NET 8 LTS, WPF, Serilog
- New dependency: must be approved by Hermes (D-### entry in DECISIONS.md)
- Asset lock: ABSOLUTE — no new art/sound assets ever produced. Everything from existing inventory in `Assets/`.

## Asset rules (from PRD §0)

1. **Asset lock absolute.** Folders/files in `Assets/` are the complete final asset set. No placeholder art, no TODOs for "future assets."
2. **Never hardcode frame counts.** Enumerate each folder with `Directory.GetFiles(dir, "*.png")` sorted by filename at startup.
3. **AssetManifest (`Assets/manifest.json`) is the only mapping layer** between FSM states and file paths. Do NOT derive folder names from enum names (known typo: `cat_surpised`).
4. **Excluded from build:** `flip_folders.py`, `video/` folder.
5. **Fail loud, not silent.** Missing asset → publish `AssetMissing` event, log to Serilog, fall back to Idle state. Never crash, never render empty frame silently.
6. **Transparency spec (§9) is non-negotiable.** `Background="Transparent"` alone is NOT acceptable. Win32 extended style work required.

## Workflow

```
Plan → Dispatch → Execute → Verify → Commit/Push → Document
```

- **Hermes (Ame):** plan, dispatch, verify, commit/push, document
- **Claude Code / executor:** execute code inside `02-application/` only
- No agent may: skip verification gates, edit root context files unless instructed

## Git rules

- Clean git history, no generated files committed
- No `.git` inside `02-application/` (subtree pattern)
- Pre-push checklist required before any push

## Amendment log

(none yet)