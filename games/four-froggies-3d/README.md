## What's new (ff3d19) — ranch HOUSE pass + kid BGM

- **Stairs + 2nd floor:** real step standY + back-half 2F slab (frogs hop/walk up; no float/clip). Camera opens upstairs.
- **Explorable rooms (~20):** foyer, living, kitchen, dining, play, fish gallery, pet parlor, mud room, family, hall + upstairs bedrooms/lofts/library/craft/guest/toy/balcony. Interior walls with doorways.
- **Fish tanks:** 5 big tanks with many animated swimming fish; E to look.
- **Animal pens:** indoor + yard-adjacent dogs, cats, rabbits, lizards, snakes (lots); E to pet.
- **Kid BGM:** upbeat cheerful WebAudio loop (bright majors, bouncy melody) — not melodramatic/low/scary.
- Keeps ff3d18 track physics + ff3d17 Unitree + ff3d16 swim/sub + exit1 lobby. Garage size unchanged. Canon cast only.
- Cache: `?v=20260928-ff3d19` · asset `index-ff3d19.js`

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
