# Four Froggies 3D

This folder is the standalone three.js ranch. Do not replace index.html with a redirect to /games/four-froggies/.

## ff3d2 (2026-09-28) — track trestles + longer path

- **Support / trestle fix:** upright timber posts now use the same bank sign as the dirt ribbon (`y - sin(bank)*width*side`). Previously posts used the opposite sign, so they sat on the high/air side of banked turns instead of under the road. Lateral placement also scales by `cos(bank)` so posts sit under the inset banked edge.
- **Longer track (~2×):** expanded loop — bigger east bank, north jump lip + whoops, NW banked hairpin, elevated return bridge, south S-curve whoops back to the join. Approach spur starts farther south. Path length ~421 vs ~225.
- **Rocks / trees:** rocks respaced along the new ribbon; tree scatter Z extended to cover the northern loop.
- Cache: `?v=20260928-ff3d2` · bundle `assets/index-ff3d2.js`

## ff3d1 (2026-09-28)

- Start camera south of driveway; forest in `ranchEnv`; denser trestles; space void; party lobby; arcade 3D badge.

## Controllers

Uses `/games/shared/gamepad.js`. Canon cast: James, Jimmy, Bubbles, Rexy.
