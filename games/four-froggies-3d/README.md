# Four Froggies 3D

This folder is the standalone three.js ranch. Do not replace index.html with a redirect to /games/four-froggies/.

## ff3d1 (2026-09-28)

- **Start camera:** south of the driveway, elevated, looking north toward the garage — frames all four froggies + four Cybertrucks (was north/garage-side looking the wrong way).
- **Forest backdrop:** `forest.jpg` cylinder scaled to playable tree height; ground+forest+trees live in `ranchEnv` so they hide in space.
- **Track trestles:** denser timber bents under elevated ribbon; upright braces (not skewed).
- **Space:** full leave-ranch — ranchEnv hidden, dark void + denser starfield + dark play plane (no forest/ground leak).
- **Party lobby:** Host room / Join / invite QR (PeerJS, `froggies3d-` prefix) on the title gate — same pattern as `/games/four-froggies/`.
- Cache: `?v=20260928-ff3d1`

## Controllers (same map as the other cabinets)

Uses `/games/shared/gamepad.js` (`window.SimilarizeGamepad`). Do not fork that file.

Xbox / Cybertruck pads, up to 4:

- Title: A or Start claims the next open frog for that pad. B releases it. Y starts.
- In game: left stick and d-pad move that frog. A gets into the one Cybertruck or gets out. B or X hops. LB / RB change tire size, driver only.
- Four Cybertrucks (one per frog). Keyboard / on-screen frog starts driving. Other claimed frogs walk. Unclaimed frogs are AI.
- Camera follows the truck while someone is driving, otherwise whoever is walking.

## Party

`party.js` + `party-lobby.js` — Host QR / Join code / Copy invite. Rooms use peer id prefix `froggies3d-` so they do not collide with the Canvas lobby. Solo always works if the net fails.
