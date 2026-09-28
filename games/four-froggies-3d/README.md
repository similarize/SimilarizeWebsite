# Four Froggies 3D — ff3d7 (boot TDZ fix)

Cache: `20260928-ff3d7` · bundle `assets/index-ff3d7.js`

## Bug (ff3d6 blank page)
`Uncaught ReferenceError: Cannot access 'J' before initialization`

**Cause:** Trestle push-out was named `function Br`, which hoisted inside `od()` and **shadowed** Three.js `Sprite` (`Br=class`). Phone-bot labels (`Xt` → `new Br(...)`) therefore constructed the collider instead of a Sprite. That ran during `let H=[vt(...)]`, **before** `let J=0`, hitting the TDZ on player `J`.

**Fix:** Rename collider to `BrPush` (def + drive-loop call). Sprite `new Br(...)` unchanged. All ff3d6 features kept.

Baseline: ff3d6. Do not publish from agent — handoff only.
