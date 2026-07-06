# Mochi v2 — Manual Test Checklist (T-024)

## Build
- [ ] `dotnet build` succeeds on Windows 11 (0 errors, 0 warnings)
- [ ] MochiV2.exe launches without crash

## AC-1: Transparency
- [ ] Cat sprite visible on desktop, background fully transparent
- [ ] No white/black box around sprite

## AC-2: No Focus Steal
- [ ] Clicking through Mochi doesn't steal window focus
- [ ] Mochi stays on top but doesn't activate

## AC-3: Drag & Release
- [ ] Drag Mochi → follows cursor with elastic lag
- [ ] Release → falls with gravity, squash on landing
- [ ] Screen edge boundaries respected

## AC-4: Feeding Persist
- [ ] Feed via tray menu → food +40, eating animation, hearts
- [ ] Close and reopen → save data persists

## AC-5: 30-min Variety
- [ ] Leave Mochi idle 30 min → observe varied behaviors (walk, blink, scratch, meow)
- [ ] Not stuck in same state

## AC-6: Fullscreen
- [ ] Open fullscreen app → Mochi auto-hides
- [ ] Exit fullscreen → Mochi reappears

## AC-7: Resource Budget
- [ ] RAM < 150MB after 10 min
- [ ] CPU < 5% when idle

## AC-8: Missing Asset
- [ ] Remove a sprite folder → Mochi falls back to Idle, no crash

## AC-9: Sound Cooldowns
- [ ] Rapid clicks → sound not spammed (8s cooldown)

## AC-10: Typo Folder
- [ ] cat_surpised folder loads correctly (Surprised state works)

## Additional
- [ ] Tray menu: all items functional
- [ ] Settings window opens, sliders work, save/cancel
- [ ] Night mode: after 22:00 → cool tint, increased sleep
- [ ] Key rate: type fast 2 min → Mochi sleeps, stop 5 min → wakes + meow
- [ ] Cursor idle 30s → Mochi walks toward cursor