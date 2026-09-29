# What's new (ff3d71) — spin the submarine stern propellers

- Mount the surface and underwater stern propellers as rotating assemblies and spin them continuously while their respective submarine scene updates.
- Cache: `?v=20260929-ff3d71` · asset `index-ff3d71.js`

# What's new (ff3d70) — complete the submarine shell around the cockpit

- Add the missing opaque upper hull around the existing opaque lower hull and continue the curved form up to a small clear polar cockpit/periscope cap.
- Keep the cockpit canopy aligned to the hull's actual ellipsoid so the seated rider remains visible through the only transparent submarine panel.
- Cache: `?v=20260929-ff3d70` · asset `index-ff3d70.js`

# What's new (ff3d69) — keep the underwater rider in the submarine

- Fixed the final scene-render branch: underwater had been falling through to the space EVA renderer, which reparented the frog out of the sub and put it above the scene every frame after the submarine update.
- Underwater transforms are now owned only by the underwater update, so the rider stays attached to the seat and an outside swimmer stays at their actual swimming position.
- Preserves the solid submarine hull, clear cockpit dome, fitted scuba gear, and E-to-shore behavior.
- Cache: `?v=20260929-ff3d69` · asset `index-ff3d69.js`

# What's new (ff3d68) — fit scuba gear and exit to the nearest shore

- Scale the tank and vest to the frog, place the tank behind its back, and align the mask and regulator to its face.
- Scuba E exits to the nearest safe shore from the diver's current pond location; exiting from inside the submarine still returns to swimming.
- Cache: `?v=20260929-ff3d68` · asset `index-ff3d68.js`

# What's new (ff3d67) — seat the frog in an opaque sub with a clear cockpit dome

- Keep the submarine hull and bow opaque; confine transparency to a small canopy over the cockpit beside the aft-set periscope tower.
- Place the frog inside the open cockpit at a seat-height transform, feet down and facing the bow, consistently on entry, reboarding, and every movement update.
- Pressing E while scuba swimming outside the submarine now exits to a safe nearby shore; pressing E inside the submarine still exits to swimming.
- Use a new versioned bundle/cache URL so browsers cannot reuse the conflicting ff3d66 asset.
- Cache: `?v=20260929-ff3d67` · asset `index-ff3d67.js`

# What's new (ff3d66) — solid open cockpit with visible seated rider

- Keep the submarine hull and bow opaque; remove the translucent cockpit panes so the top is solid/open rather than see-through.
- Raise and resize the frog in all submarine entry, reboarding, and movement paths so the body sits within the cockpit and remains visible above its rim.
- Cache: `?v=20260929-ff3d66` · asset `index-ff3d66.js`

# What's new (ff3d65) — attach the frog to the submarine seat

- Parent the rider under the actual seat group and place its feet at the cabin floor, so submarine motion cannot separate the frog from the seat.
- Restore a translucent hull and bow to keep the seated rider visible through the submarine shell.
- Cache: `?v=20260929-ff3d65` · asset `index-ff3d65.js`

# What's new (ff3d64) — bring the hull up around the seated rider

- Raised the open-top hull so its gunwale surrounds the frog at cockpit height instead of sitting below the rider, and added a dark cabin floor beneath the seat.
- Kept the opaque hull, transparent cockpit glazing, and seated frog pose.
- Cache: `?v=20260929-ff3d64` · asset `index-ff3d64.js`

## What's new (ff3d63) — seat the frog inside an open submarine cockpit

- Replaced the closed see-through bubble/hull with an opaque, open-top lower hull and a clear, framed cockpit, so the frog sits down inside the boat rather than appearing above its shell.
- Lowered the rider's body to the seat and kept the feet bent inside the hull.
- Cache: `?v=20260929-ff3d63` · asset `index-ff3d63.js`

## What's new (ff3d62) — fix the underwater sub entry and rider seat

- Fixed a runtime error on submarine entry: the frog was being attached through an undefined variable, which could abort the game update as the underwater scene loaded.
- Made the hull and bow transparent around the full cockpit, added visible cockpit glass, seat padding, and side rails, and aligned the frog's feet and seated pose with the seat.
- Cache: `?v=20260929-ff3d62` · asset `index-ff3d62.js`

## What's new (ff3d61) — open track entry without barriers

- Removed barriers completely from the entry spur and left the track entry junction open so racers can enter and leave the track freely from the ranch.
- Kept continuous barriers along the racing oval (full inside perimeter to prevent infield shortcuts, and outside perimeter except at the entry).
- Cache: `?v=20260929-ff3d61` · asset `index-ff3d61.js`

## What's new (ff3d60) — seat the rider inside the cockpit

- Made the complete hull and bow highly translucent, and framed the cockpit with glass walls and a visible seat so the frog reads as enclosed inside the submarine rather than above its shell.
- Enlarged the rider and aligned its feet with the cockpit seat in submarine-local coordinates, with a seated leg and arm pose reapplied during movement.
- Cache: `?v=20260929-ff3d61` · asset `index-ff3d61.js`

## What's new (ff3d57) — barriers, Cybertruck details, and submarine swimming

- Added continuous barriers on both sides of the racing track, shaped to follow the banked road and vertical loop. Grounded cars are kept on the track, while a high enough jump can clear the finite barrier to escape.
- Reworked the Cybertruck's dark glazing and added front/rear bumpers, bed rails and floor, side rockers, wheel flares, door seams, and handles while preserving its angular stainless-steel shape.
- E now lets the frog disembark into scuba swimming, then surface into the pond. Boarding again requires swimming back near the sub; pressing E at the water's edge enters the pond instead of pushing the frog back to shore.
- Cache: `?v=20260929-ff3d57` · asset `index-ff3d57.js`

- Retains the ff3d55 landing and stable loop-camera fixes.

## What's new (ff3d55) — land jumps on the track and steady loop view

- Track jumps now keep their height above the road when leaving a raised surface and reset jump height on landing, preventing cars from staying suspended above the track.
- The camera keeps a steady, wider view through the loop and lets the two-racer framing handle both cars.
- Cache: `?v=20260929-ff3d55` · asset `index-ff3d55.js`

## What's new (ff3d53) — proper submarine underwater too
- Replaced the underwater-only tapered cylinder with a streamlined rounded hull, bow, conning tower, periscope, portholes, fins, and propeller, matching the submarine on the surface.
- Cache: `?v=20260929-ff3d53` · asset `index-ff3d53.js`

## What's new (ff3d52) — preserve steering on banked track

- On regular track sections, vehicles keep their steered heading while tilting to match the road surface; the vertical loop still uses the full track frame.
- Cache: `?v=20260929-ff3d52` · asset `index-ff3d52.js`

## What's new (ff3d50) — a proper submarine

- Replaced the tank-shaped cone-like sub with a rounded blue hull, bow, conning tower, periscope, portholes, tail fins, and propeller.
- Cache: `?v=20260929-ff3d50` · asset `index-ff3d50.js`

## What's new (ff3d49) — two lanes for racing
- A repeating dashed center line divides the full track into two lanes, following the bends and vertical loop.
- Cache: `?v=20260929-ff3d49` · asset `index-ff3d49.js`

## What's new (ff3d48) — back down walls and smooth the vertical loop
- While climbing, back away from the wall to descend along it and return to the height where the climb began.
- The car follows the vertical loop with a smooth 3D track frame rather than flipping its heading near the top; denser loop samples smooth the ride further.
- Cache: `?v=20260929-ff3d48` · asset `index-ff3d48.js`

## What's new (ff3d47) — climb pose, solid truck walls, track bank and start

- Holding into a wall gives James an animated climbing pose while the existing climb and camera fade keep him readable.
- Vehicles now keep their full footprint clear of house and garage walls instead of clipping through them.
- The cars roll around the vehicle's forward axis to match the banked track, while the nose follows track pitch.
- The entry spur joins the oval as one road mesh, removing the overlapping track surfaces that flickered at the start.
- Cache: `?v=20260929-ff3d47` · asset `index-ff3d47.js`

## What's new (ff3d46) — camera stays with him

- The view sits behind him and a little above, and it follows him up walls, through the house, and onto the roof. Drag the mouse to look around. Walking sideways does not spin the camera.
- Cache: `?v=20260929-ff3d46` · asset `index-ff3d46.js`

## What's new (ff3d45) — ride with dad

- As James, stand by dad's truck and press E. James sits in the passenger seat. Dad keeps driving, and James goes where the truck goes. E hops him out. The stick does not steer.
- Cache: `?v=20260929-ff3d45` · asset `index-ff3d45.js`

## What's new (ff3d44) — climb any wall

- Hold into a wall and he climbs it. The mansion wall still lands on the roof. The garage wall lands on the garage roof. Let go and he drops.
- A roof or wall between the camera and him turns see-through until it is out of the way.
- Cache: `?v=20260929-ff3d44` · asset `index-ff3d44.js`

## What's new (ff3d43) — climb the trees

- Hop next to a tree and he grabs the trunk. Another hop or two puts him in the crown. Walk out of the leaves and he drops.
- Cache: `?v=20260929-ff3d43` · asset `index-ff3d43.js`

## What's new (ff3d42) — normal frog controls

- W, A, S, and D move him relative to the camera. W goes where you are looking, A and D go left and right, and S backs up straight. He turns to face the way he is running.
- Drag the mouse to look around. The camera stays there instead of swinging behind him when he strafes.
- Cache: `?v=20260929-ff3d42` · asset `index-ff3d42.js`

## What's new (ff3d41) — turn, trees, look, and dad

- A and D turn in place. W still walks, and S still backs up straight.
- Trees are solid. Hop against a trunk to climb it, then walk off to drop.
- Drag the mouse to look around. The view stays where you leave it. Click the ground and he walks to that spot, facing it.
- Lobby and Arcade stay on screen while you play.
- James's dad has legs and faces the way he is walking.
- Cache: `?v=20260929-ff3d41` · asset `index-ff3d41.js`

## What's new (ff3d40) — hop up to the roof

- Stand against the mansion and hop. Each hop climbs the wall and perches there. The third hop lands you on the roof, and you can walk to the helipad. A normal hop everywhere else is unchanged.
- Cache: `?v=20260929-ff3d40` · asset `index-ff3d40.js`

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
