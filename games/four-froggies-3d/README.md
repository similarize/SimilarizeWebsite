# Four Froggies 3D — ff3d8 (playable + start cam)

Cache: `20260928-ff3d8` · bundle `assets/index-ff3d8.js`

## Bugs (ff3d7)
1. After Start: `Cannot access 'M' before initialization` every frame in `Ui` — camera `let M` shadowed track width `M=6.4` used by `halfW=M*.9` → freeze / no controls.
2. Title/start view drifted to display-truck follow near garage wall instead of canon south→garage establish.

## Fixes
- Camera height → `camH` (track `M=6.4` intact). Keeps `BrPush` blank-page fix.
- Title `!Sn`: pin cam `(-12,22,56)` → `(-12,1.2,20)`.

Baseline: ff3d7. Handoff only — do not publish from agent.
