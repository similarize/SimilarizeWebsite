## What's new (ff3d24) — Dad exit-after-park unstuck

- **Root cause:** `dadForceTruckOffPorch` applied truck AABB to Dad's *walk* position and teleported him back to park every time he stepped toward the porch house-tour — so after parking he never completed exit → walk → remount.
- **Fix:** force-exit only relocates Dad while driving; on foot it only snaps the *truck* off the porch. After park arrives, `dadExitParkToWalk` forces exit → house walk loop → yard remount. Drive/walk unstuck if no progress ~2s.
- **Kept:** porch ban, parkFront (−42, 22.5), parkGarage (−2, 22), force-exit, ALL ff3d23 features, exit1 lobby.

- Cache: `?v=20260928-ff3d24` · asset `index-ff3d24.js`
