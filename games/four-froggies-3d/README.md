## What's new (ff3d39) — fly the helicopter and the passenger drone

- A helicopter sits on the H pad. The passenger drone sits on its pad, and the floating sign now fits the whole word, including the P. A cyan P is painted on that pad. Walk up and press E to get in. The stick flies, RT or Hop climbs, LT descends, and it hovers when you let go. E hops you out onto the roof, or into a fall if you bail in the air.
- Cache: `?v=20260929-ff3d39` · asset `index-ff3d39.js`

## What's new (ff3d38) — climb out of the pond

- Press E while swimming and you step onto the nearest shore. Press E in the submarine and you surface on that shore, with the submarine parked beside you.
- Cache: `?v=20260929-ff3d38` · asset `index-ff3d38.js`

## What's new (ff3d37) — a bigger lobed pond

- The ranch pond is about ten times the old circle, and its shore is five ovals grown together: two northern coves, a west bulge, an east bulge, and a south lobe. Wading, swimming, and the blue water are the same shape. The house, porch, garage, and yard stay dry.
- Cache: `?v=20260929-ff3d37` · asset `index-ff3d37.js`

## What's new (ff3d36) — Cybertruck stays off the porch

- The Cybertruck stops at the porch edge. Its nose and corners share the porch rectangle, so it cannot drive up onto the deck.
- Cache: `?v=20260929-ff3d36` · asset `index-ff3d36.js`

## What's new (ff3d35) — frog reverse goes straight back

- Holding back moves the frog straight back along the way it is facing, and it keeps that facing. Forward still turns you toward where you're going.
- Cache: `?v=20260929-ff3d35` · asset `index-ff3d35.js`

## What's new (ff3d34) — see the room you're in

- Inside the house the camera sits in the gap above that floor's walls and under its ceiling, and it looks at the frog. Walls stay visible, so the room reads. The roof overview is unchanged.
- Cache: `?v=20260929-ff3d34` · asset `index-ff3d34.js`

## What's new (ff3d33) — pond matches wading

- Swimming starts on the blue water. The pond disc and the wade circle are the same spot: center (90, -48), radius 30. The old wade circle was radius 52, so you swam on dry ground well outside the water.
- Cache: `?v=20260929-ff3d33` · asset `index-ff3d33.js`

## What's new (ff3d32) — roof, dad, rooms, falls, sub

- Dad stands on the floor he is actually on (porch, ground, loft, roof stair, roof deck) and slides around blockers instead of freezing.
- Roof landing deck sits above the chimneys and spire. Helipad and drone pad are inset on that deck, clear of the upper house.
- On the roof the camera pulls up over the whole house so both pads stay in view. In rooms the camera sits above the walls and the nearby walls fade.
- On foot, Space hops (4.2 m/s) and walking off a roof, loft, or cliff falls with gravity until you land. Holding into a wall climbs over it.
- Underwater submarine is a horizontal hull. The frog rides inside it and stays hidden.

- Cache: `?v=20260929-ff3d32` · asset `index-ff3d32.js`

## What's new (ff3d30) — big oval and a speed loop

- Much larger stadium north of the yard: long straights, a steep east sweeper, a milder west sweeper, a table jump on the way out, whoops and a second jump on the way back, and the driveway spur.
- Vertical loop on the south straight. Full throttle carries the truck over the top and out the other side. A slow entry drops the truck back onto the dirt.
- Keeps the walkable front-door corridor, the bigger pond, rounded lips, outside-high banks, rocks beside the dirt, roof pads, forest, underwater, and the phone cam.
- Cache: `?v=20260928-ff3d30` · asset `index-ff3d30.js`
- Cast: James, Jimmy, Bubbles, Rexy only.

## What's new (ff3d29) — front door → stairs walkable

- **Front-door → stairs corridor:** Removed invisible wall that sealed foyer against the kitchen/stairs. North hall along the inside of the front wall is walkable (same idea as garage→house). Solid room walls kept.
- **Bogus blockers removed:** Invisible collider extensions at x≈−56 / x≈−44 (no matching visual wall) cleared so rooms aren't phantom-walled.
- **Dad NPC:** Uses same `pushWalls`/`oi` — houseTour now walks front→stairs via the north corridor.
- **Kept:** ALL ff3d28 (submarine mesh, frog-in-hull, exit tips, pond r=52, underwater, roof pads, track, phone cam, garage↔house door, distinct froggies/hats, parks, space party, exit1 lobby).

- Cache: `?v=20260928-ff3d29` · asset `index-ff3d29.js`
- Handoff only under `/workspace/ff3d-fix29/` — **not** mirrored to SimilarizeWebsite until formal publish.
