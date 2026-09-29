## What's new (ff3d22) — ribbon contact, rocks, camera steer

- **Rocks** on the dirt use `standY`, the same height as feet and tires. A rock on a bank that dips under the ground stays on the ground.
- **Ribbon art:** the driving surface casts shadows and is polygon-offset toward the camera. The shoulder and the buried under-ribbon do not cast, so the stacked ribbons stop flickering.
- **Off a lip:** on foot and in a mech, a drop in `standY` is added to hop, so the body falls. The pond still zeros hop. Truck lip-launch is unchanged.
- **Controls:** walk and space EVA move on `Qn+nudgeYaw`, the yaw the camera is placed from. Drag-look and W agree. Truck A/D still yaws the truck, and the chase cam follows that yaw.
- `Rr` surfY stays on the ff3d18 frame (`-sin(bank)*along + cos*0.05`), the same frame as ribbon mesh `Le`.
- Keeps ff3d21 camera occlusion fade.
- Cache: `?v=20260928-ff3d22` · asset `index-ff3d22.js`
- Cast: James, Jimmy, Bubbles, Rexy only.

## What's new (ff3d21) — camera occlusion fade (local subject)

- **Always see your froggy/robot:** walls (and similar thin occluders) between the camera and **your** controlled subject fade transparent instead of hiding you.
- **Local-only / multiplayer-safe:** each client raycasts camera → **that client's** controlled subject only. Other froggies inside/outside never drive your fade.
- Physics/collision unchanged (materials only). Opacity restores when not occluding.
- Keeps ff3d20: Dad lifestyle + porch/platforms + truck outside house + exit1 Arcade exit.

- Cache: `?v=20260928-ff3d21` · asset `index-ff3d21.js`
