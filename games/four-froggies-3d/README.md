## What's new (ff3d21) — camera occlusion fade (local subject)

- **Always see your froggy/robot:** walls (and similar thin occluders) between the camera and **your** controlled subject fade transparent instead of hiding you.
- **Local-only / multiplayer-safe:** each client raycasts camera → **that client's** controlled subject only. Other froggies inside/outside never drive your fade.
- Physics/collision unchanged (materials only). Opacity restores when not occluding.
- Keeps ff3d20: Dad lifestyle + porch/platforms + truck outside house + exit1 Arcade exit.

- Cache: `?v=20260928-ff3d21` · asset `index-ff3d21.js`
