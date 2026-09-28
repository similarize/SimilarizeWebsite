# Four Froggies 3D — ff3d6 (vehicles + track + controls)

Cache: `20260928-ff3d6` · bundle `assets/index-ff3d6.js`

## This build
1. **Track rebuild** — clean CCW loop, no self-intersecting ribbon; approach ramp lands into loop; banks = outer-high on turns.
2. **Bank lean** — `sr = ribbon bank` (no travel-sign flip); wheels sit on `surfY`; forward-only soft path-align (reverse never flips yaw/camera).
3. **Tank/truck controls** — L/R steer yaw only (no strafe); hold reverse stays reverse; forward/back + steer = arcade drive. Applies to James CT + Ripsaw + Monster + Tank.
4. **Roster** — exactly **2 Cybertrucks** (James playable + Dad NPC). Shared **Ripsaw** (tracked Howe&Howe-style), **Monster Truck**, **Tank** parked **inside garage**.
5. Keeps: phone robots, Dad NPC, Monster shared, ff3d5 trestle collision.

Baseline: ff3d5. Do not publish from agent — handoff only.
