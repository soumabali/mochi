# Phase Log (APPEND-ONLY — never edit or delete existing lines)

Format, one entry per event:

```
## <ISO timestamp> | phase <n> <NAME> | <event>
- verdict: approved | rejected | report
- by: <human name or Hermes>
- notes: <one to three lines>
```

Events: `enter`, `gate-review`, `progress-report`, `close`.

<!-- entries below line -->

## 2026-07-05T00:00:00+08:00 | phase 0 INIT | enter
- verdict: report
- by: Hermes (Ame)
- notes: Project skeleton created via `new-project mochi-v2 --type desktop`. Ledger initialized. PRD copied from workspace. Assets symlinked.

## 2026-07-05T00:30:00+08:00 | phase 1 PRD | enter
- verdict: report
- by: Hermes (Ame)
- notes: Original PRD v2.0 (398 lines, asset-locked) archived to 01-documents/. Reformatted to dhar-dev template: 15 sections, 28 FRs, 10 ACs, 4 riskiest assumptions, 6 success metrics, 3 open questions. project.json stack fixed to .NET 9.