# Four Froggies 3D — ff3d5 (track vehicle physics)

Cache: `20260928-ff3d5` · bundle `assets/index-ff3d5.js`

## Physics (this build)
- Sample ribbon **height + bank + tangent/yaw** under the vehicle (`Rr` → `surfY`, `yaw`)
- Align pitch/roll/yaw so trucks **lean with banked turns** and follow the path
- Keep wheels/body on dirt mesh (ribbon half-width clamp + bank-aware floor)
- Trestle/mast **push-out** collision (`tS` / `Br`) — stop nosing into posts
- Applies to all track rides in `V`: James Cybertruck, Ripsaw, Monster Truck, Tank

## Roster (unchanged from ff3d4)
- Vehicles: 2 CTs (James playable + Dad NPC), Ripsaw, MT, Tank
- Robots / phone remote / Dad / space rocket+EVA kept

Baseline: ff3d4. Do not publish from agent — handoff only.
