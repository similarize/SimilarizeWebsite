## What's new (ff3d18) — dirt track surface physics (serious fix)

- **Root cause:** `Rr` surfY used `+sin(bank)*lat/cos` while ribbon mesh `Le` uses `-sin(bank)*along + cos*0.05` — up to ~8m error on banked lips → vehicles clipped through the ribbon. Frogs used `standY` that ignored the track entirely.
- **Approach (researched):** Catmull/polyline ribbon height+normal sample — same Frenet-ish frame as mesh build (Nordschleife-racer / Nebula-Rush / KartRacer pattern). Sample Y + bank from curve; constrain vehicle/frog when laterally on-ribbon; drive-off edges still allowed.
- Vehicles (Cybertruck / Ripsaw / Monster / Tank) stay on ribbon, bank with surface; frogs can hop/stand on ribbon top.
- Keeps ff3d17 Unitree humanoid + ff3d16 swim/sub + exit1 lobby.
- Cache: `?v=20260928-ff3d18` · asset `index-ff3d18.js`
- Cast: James, Jimmy, Bubbles, Rexy only.

## What's new (ff3d17) — Unitree humanoid silhouette

- **Unitree** is bipedal humanoid (Unitree G1-style), not a dog/quadruped.
- Phone POV/control unchanged (default bot still Unitree).
- Keeps ff3d16 swim look + single sub hull. Mansion/garage same. Lobby exit1 kept.
- Cache: `?v=20260928-ff3d17` · asset `index-ff3d17.js`
- Cast: James, Jimmy, Bubbles, Rexy only.

## What's new (ff3d16) — swim LOOK + single sub hull

- **Swim pose:** in the pond, froggies look like they are swimming (flat body, arm/leg stroke) — not land hop/walk.
- **Submarine:** board the existing docked hull — no second/cloned sub. EXIT leaves that same hull parked.
- Keeps ff3d15 pond swim + docked sub. Mansion/garage unchanged. Lobby exit1 UX kept.
- Cache: `?v=20260928-ff3d16` · asset `index-ff3d16.js`

## What's new (ff3d15) — pond swim + docked submarine

- **Swim** in the pond (deeper foot sink; splash tip).
- **Submarine** docked at the pond south rim — E / Interact to board, dive/drive underwater feel, EXIT parks at exit.
- Keeps ff3d14 bigger ranch floor / forest / clamps. Mansion/garage unchanged.
- Cache: `?v=20260928-ff3d15` · asset `index-ff3d15.js`
- Cast: James, Jimmy, Bubbles, Rexy only.

# Four Froggies 3D — ff3d14 (bigger ranch floor)

Cache: `20260928-ff3d14`

- Expand playable ranch **ground** + push **forest ring** and **world clamps** out so drivers are not stuck at the old tree/clamp rectangle
- Mansion / garage **same size** (standY + indoor cam clamps unchanged)
- Keeps floor standY / phone (Unitree) / bank / pond / space / BrPush / camH from ff3d13
- Includes **exit1** `party-lobby.js` / `party-lobby.css` (Arcade exit + Join CTA)
