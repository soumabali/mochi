# Decisions (APPEND-ONLY — never edit or delete existing lines)

One entry per technical decision. Supersede by appending, never editing.
This is the required rationale trail for significant changes (any `[XC]`
task, any change picking one viable approach over another) — see
`references/change-management.md` §5. If a significant change has no entry
here, it's not done. Link `D-###` in DISPATCH_LOG and commit body.

```
## D-001 | <ISO date> | <short title>
- decision: <what was chosen>
- reason: <why, one to three lines>
- alternatives: <what was rejected>
- supersedes: <D-### or none>
```

<!-- entries below line -->

## D-001 | 2026-07-05 | Project name & stack
- decision: mochi-v2, WPF .NET 8 C# desktop app
- reason: PRD v2.0 specifies WPF/.NET with Win32 transparency. Mochi v1 was Godot 4.3 — v2 is a full rewrite.
- alternatives: Godot (v1), Tauri, Electron
- supersedes: none

## D-003 | 2026-07-05 | Open questions resolved
- decision: .NET 9 (per PRD §12). GitHub repo: soumabali/mochi (local-first, remote added). EN-only strings for MVP with resource structure ready for ID.
- reason: Dhar approved all three. .NET 9 follows PRD spec. EN-only reduces MVP scope.
- alternatives: .NET 8 LTS (rejected — PRD says 9). Full ID translation (rejected — post-MVP).
- supersedes: none