## What's new (ff3d22) — Cybertruck porch ban + Dad unstuck

- **Truck never parks through porch:** hard ban on porch AABB (x −53.6…−30.4, z 12.05…18.45) expanded by truck half-extents (~2.75×2.4).
- **Valid stops only:** `parkFront` (−42, 22.5) front-lawn pad north of porch; `parkGarage` (−2, 22) garage bay.
- **Drive path retargeted:** waypoints stay at z≥22.5 near house; never aim through porch volume. Porch waypoints skipped at runtime.
- **Force-exit:** each Dad tick, if truck AABB intersects porch (or sits in house interior), snap to nearest valid park immediately.
- **Dad walk (secondary):** after ~2.4s no progress, repath/teleport around porch to yard→door points (stand ON porch OK; clip-through blocked via pushWalls).
- Keeps ff3d21 occlusion fade + ff3d20 lifestyle + exit1 Arcade/Join lobby.

- Cache: `?v=20260928-ff3d22` · asset `index-ff3d22.js`
