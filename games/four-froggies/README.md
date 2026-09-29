## What's new (lobbypick1) — gamepad alone picks James / Jimmy / Bubbles / Rexy

- **Bug:** After padfix1/drivefix2, A claimed the first open froggy then stuck — `claimLocalPad` returned the existing seat and the title lobby never read D-pad/stick, so Ben could not choose another froggy with the pad alone.
- **Fix (shared HTML lobby → Canvas + Three):** Per-pad focus cursor. D-pad or left stick L/R (or U/D) cycles open froggies for that pad; **A** claims / confirms; **B** releases. Badge `Pad N · FrogName` with Pad 1–4 colors. Cannot steal an occupied seat. Click/keyboard still work; click moves last-active pad. drivefix2 board latch, mechs, air, omnigun, spear untouched.
- Cache: `20260929-lobbypick1`. Lobby exit1 kept.

## What's new (drivefix2) — board Ripsaw / Cybertruck / Tank and STAY

- **Bug:** Jimmy (and the other froggies) could board a free Ripsaw, then the same INTERACT edge fired again — Three's pad loop re-read the cached A press after the tick already boarded, so the next frame EXITed. Standing in the vehicle solid then popped them out. AI follow could also yank a seated companion.
- **Fix (Canvas + Three):** One press boards and latches until that button is released. A later INTERACT is EXIT. Occupied hotspot rides with the rig. Mech locks unchanged (James=trillion, Rexy=1000, Bubbles=10, Jimmy=100). padfix1 / air / omnigun / spear untouched.
- Cache: `20260929-drivefix2`.

## What's new (drivefix1) — any frog boards free Cybertrucks + Ripsaw

- **Bug:** Couch companions (Rexy / Jimmy / Bubbles) could not board/drive the other Cybertruck(s) or Ripsaw the way James (primary) could — Three blocked companion truck INTERACT; near locked mechs also stole INTERACT from free trucks.
- **Fix (Canvas + Three):** Any grounded froggy boards a **free** Cybertruck / Ripsaw / Tank (same INTERACT). `frogId` on truck spots is paint/label only. **Mech locks stay exclusive** (James=trillion, Rexy=1000, Bubbles=10, Jimmy=100). Nearest-hotspot skips mechs the acting frog cannot board so free trucks win.
- Cache: `20260929-drivefix1`. Lobby exit1 / padfix1 claim / air heli-drone / omnigun / spear unchanged.

## What's new (padfix1) — one pad → one froggy · heli/drone above orange zone

- **Bug A:** Dual gamepads were multi-claiming froggies (keyboard "You" stacked on top of pad seats; click rebound poorly). Each pad now claims exactly one froggy — A cycles to the next open seat, B releases, click moves that pad. Clear Pad 1–4 seat badges.
- **Bug B:** Translucent orange house-zone floor covered helipad / drone pad / craft (Three y=0.03 under zone y=0.04; Canvas compound poly over pads). Zone floor clipped short of pads; Three pads raised & enlarged; craft sit on pad deck.
- Cache-bust: `?v=20260929-padfix1`. Lobby exit1 kept. Cast: James, Jimmy, Bubbles, Rexy. mechgun2 omnigun + air1 flight untouched.

## What's new (mechwalk1) — articulated, faster mechs with stable height

- Animate the mech's upper legs, bent knees, shins, and alternating feet while it moves.
- Increase Canvas mech acceleration and top speed for a quicker, more responsive walk.
- Keep the mech body and pilot marker at a fixed screen scale so camera depth/player position no longer changes mech height.
- Cache-bust: `?v=20260929-mechwalk1`.

## What's new (air4) — raise and enlarge the Three.js landing pads

- Raise the heli and drone pad graphics above the translucent orange house-zone floor that covered them.
- Enlarge both pad discs, accent rings, and labels so they are easy to see beneath the aircraft.
- Cache-bust: `?v=20260929-air4`.

## What's new (air3) — point aircraft along their flight path

- Remove the 90-degree nose offset from helicopter and drone heading in both Canvas and Three.js; the aircraft now point along the same world-space direction used by flight steering.
- Cache-bust: `?v=20260929-air3`.

## What's new (air2) — takeoff and scale aircraft to the froggies

- Fix takeoff in both Canvas and Three engines: actively climbing aircraft are no longer forced back onto the ground by the landing assist.
- Scale the Three.js helicopter and passenger drone up to better fit the froggies: helicopter 2.2×, drone 3.2×.
- Cache-bust: `?v=20260929-air2`.

## What's new (qa1) — feet on the track, screen steer, flat ribbon

**Feet were walking through the elevated track.** Canvas landed hops at `z = 0`. Three.js applied `trackElevAt` only while driving, so frogs, mechs, and followers passed through the ribbon. Both engines now stand and hop from the shared deck. Swim and the submarine still use the water line.

**Three.js steer no longer assumes the old corner camera.** The follow camera sits mostly south (view3). WASD still used a 45° basis, so W walked diagonal to the screen. Movement now reads the camera’s flattened view each frame (`forward × up = right`). W is into the picture, D is screen-right. Canvas keyboard, stick, and click-to-aim use the same screen-up rule against the isometric projection. AI follow stays in world axes.

**Track art uses the same height as the tires.** Canvas and Three.js build the apron from `trackElevAt` (path, hills, banks, ramps, and rocks together), sampled closely enough that the road follows the hills instead of spiking at the old corner list. Lane stripes sit a few centimeters above that apron. Pillars, the start gate, parked trucks, rocks, and ramp lips stand on it. The ranch floor has a hole under the apron so the dips stay visible. Shadows and dust sit on the deck.

- Cache-bust: `?v=20260928-qa1`. Lobby exit1 UX kept. Cast unchanged: James, Jimmy, Bubbles, Rexy.
- Standalone cabinet `games/four-froggies-3d/` ff3d30 is a much larger oval with jumps, whoops, banked sweepers, and a vertical loop that drops the truck unless it carries speed.

## What's new (mech8) — Rexy 1000-mech respawn restores full mesh

**Bug (mech6/mech7 Three):** After Tank destroys **Rexy's thousand-story mech**, the ~7s respawn came back as a **solid yellow ball / pad** instead of the articulated robot. Root cause: blast replaced every child material (including the large faint **haze SphereGeometry**) with a wreck mat; revive only forced `opacity=1` + yellow on those wreck mats — so the glow sphere became an opaque ball hiding the body boxes.

**Fix (`three-hub.js`):** save `userData.homeMat` per mech mesh at build/destroy; on `tickMechRespawn` restore those materials and clear wreck transform. Canvas draw path was already fine (hides while destroyed, `drawMech` on revive).

- Cache-bust: `?v=20260928-mech8`. Lobby exit1 UX kept (`← Arcade` + `Join · enter code`). Cast unchanged: James, Jimmy, Bubbles, Rexy.
- Includes mech7 (swim LOOK + single sub hull) if not yet live.

## What's new (mech7) — swim LOOK + single sub hull

- **Swim pose:** in the pond, froggies look like they are swimming (flat/horizontal body, arm/leg stroke) — not the land hop/walk cycle.
- **Submarine:** board the **existing** docked hull in place — no second/cloned sub. EXIT leaves that same hull at shore/exit.
- Land hop/walk unchanged. Sub boarding ride pose unchanged. Lobby exit1 UX kept.
- Cache-bust: `?v=20260928-mech7`.

## What's new (mech6) — pond swim + docked submarine · tank blast respawn ~7s

- **Swim:** walk into the pond — soft rim no longer blocks; frog enters swim (slower stroke, splash).
- **Submarine:** docked at the **south pond perimeter** (near ranch approach). INTERACT / E to board · dive underwater · EXIT back to swim (if still in pond) or shore.
- Phone + desktop: same INTERACT / E / virtual joystick patterns as trucks/mechs.
- **mech6 blast respawn:** tank missiles that wreck toys/animals/props **and** Rexy's thousand-story mech bring them back after **~7 seconds**.
- House/garage size unchanged (mech5 ground scale kept).
- Cache-bust: `?v=20260928-mech6`. Lobby UX kept (`← Arcade` + `Join · enter code`). Cast: James, Jimmy, Bubbles, Rexy.

## What's new (mech5) — tank vs Rexy 1000-mech · vehicle feel · bigger drive ground

**Tank missiles (Canvas + Three lobby):** still blow up **toys / animals / props**. Among **mechs**, only **Rexy's thousand-story (1000)** can be destroyed — **not** trillion / hundred / ten. Boom ejects a pilot if aboard; mech fades out. World reset / `resetVehicleParks` respawns it.

**Vehicle drive feel (autos):** distinct accel / top speed / turn —
- **Ripsaw** = fastest automobile-type
- **Cybertruck** = balanced baseline
- **Monster** = Cybertruck with big live wheels (wheel scale ≥ 1.35) — punchy, slower turn
- **Tank** = heaviest / slowest (FIRE unchanged)
Mech bands also differ slightly (10 snappy → trillion lumber).

**World size:** `MAP_W×MAP_H` **5600×4200** (was 4200×3150). More green/dirt to drive; clamps + perimeter forest push out. **Ranch house / garage sizes unchanged.**

- Cache-bust: `?v=20260928-mech5`. Lobby UX kept (`← Arcade` + `Join · enter code`). Cast: James, Jimmy, Bubbles, Rexy.
- Not touched: standalone `games/four-froggies-3d/` (parent may ship parallel ff3d expand).

## What's new (mech4) — Tank FIRE no longer resets the world

**Bug (live mech3):** While driving Tank in Three, **Space / X / FIRE** often **rebuilt the ranch** (brown dirt spawn / “reset”) instead of shooting. Root cause: `main.js` stayed on `phase === "title"` during Three play, so Space/Enter hit `tryStartFromUi` → `startAlt` → `stopAltEngines` + fresh boot.

**Fixes (`/games/four-froggies/` Canvas + Three lobby):**
1. **While alt/Three is running:** Space / Enter / X are swallowed (preventDefault + stopImmediatePropagation) and only `pulseAbility` — never title-restart.
2. **`startAlt` / `tryStart`:** refuse to reboot if `engineRunning` already.
3. **Tank:** Space / X / ability / FIRE **only** fires big missiles (no hop buffer / no world reset). Keep boom + destroy toys/animals/props.
4. Lobby UX kept: **`← Arcade`** + **`Join · enter code`** + CODE field (exit1 / current gh-pages). Do **not** regress mech2 lobby overwrite.

- Cache-bust: `?v=20260928-mech4`. Cast: James, Jimmy, Bubbles, Rexy.
- Not touched: `games/four-froggies-3d/`.

## What's new (mech3) — FIRE actually works + big boom missiles

**Root cause (mech2 FIRE no-op):** Canvas `main.js` used bare `global.FroggiesCanon` inside a strict IIFE. Browsers have no `global` → `ReferenceError` on every ability press / ability HUD update. Button label could show FIRE (Three) or hang; **Space / click / X did nothing on Canvas**.

**Fixes (Canvas + Three lobby — `/games/four-froggies/` only):**
1. All `global.` → `globalThis.` in `main.js` (ability + tips + mech ownership).
2. **Space**, **X**, and on-screen **FIRE** all shoot (Canvas keydown + Three `engine-boot` binds Space/X/q/shift).
3. **Big missiles** with blast radius — dramatic boom FX; toys/animals/destructible props wreck then remove (Canvas `world.js` + Three `three-hub.js`).
4. Lobby UX kept from **exit1 / current gh-pages**: `← Arcade` (overlay + HUD) + **Join · enter code** + visible CODE field. Do **not** regress mech2 handoff lobby overwrite.

- Cache-bust: `?v=20260928-mech3`. Cast: James, Jimmy, Bubbles, Rexy.
- Not touched: `games/four-froggies-3d/`.

## What's new (mech2) — Tank FIRE while driving

- **Tank shoot (Canvas + Three lobby):** while driving the shared **Tank**, ability button / gamepad **X·B** / Space fires a **forward shell from the turret** (hull facing). Short CD (~0.28s). Ability HUD shows **FIRE**.
- Tip on board + while driving: **FIRE · ability / X · EXIT INTERACT**. Toys/animals take a shove on Canvas hit.
- Ripsaw / Cybertrucks / mechs unchanged (still HOP). **Not** standalone `four-froggies-3d`.
- Cache-bust: `?v=20260928-mech2`. Cast: James, Jimmy, Bubbles, Rexy.

## What's new (mech1) — mech ownership locks + Ripsaw + Tank

- **Mech ownership (Canvas + Three lobby engines):** only the named froggy may board each mech — **James → trillion-story**, **Rexy → thousand-story (1000)**, **Bubbles → ten-story**, **Jimmy → hundred-story**. Wrong froggy gets a clear tip (near + on INTERACT); cannot enter/drive.
- **Ripsaw + Tank:** shared driveable vehicles in the garage (any frog, one at a time) in **both** Canvas and Three lobby modes. Silhouettes read as real **tracked Ripsaw** (wedge + cage + track pads) and **tank** (hull + tracks + turret/barrel) — not Cybertruck clones.
- Cast names only: James, Jimmy, Bubbles, Rexy. **Not** standalone `four-froggies-3d`.
- Cache-bust: `?v=20260928-mech1`. Do not invent cast/zone names.

## What's new (view3) — Three.js start cam + yard-scale forest
- **Three.js cold start camera:** establishing shot moved south/higher and aimed at garage mouth + Cybertruck apron so all four froggies and four trucks are in frame (was west/close → looked through the garage bay). Follow cam stays south-biased looking north toward the yard/garage.
- **Ranch forest backdrop:** removed oversized perimeter trees (s=1.35–3.0) and green cylinder berms; rings now use the **same** trunk/canopy recipe and scale as playable yard trees (s≈0.88–1.3). Sky stays blue (dusk no longer paints a flat forest-green void). Softer fog so the woods read continuous.
- Cache-bust: `?v=20260928-view3`. Canvas forest untouched. Do not publish house2 / basketball / froggies-sps.

## What's new (view2) — leave-ranch space + continuous forest
- **Three.js Starship→space:** fresh scene (not clear-in-place), `setClearColor` + `#engine-host` go void `#020617`, dense starfield + starfield **play plane** (no ranch grass / forest leak; planets no longer sit on the woods).
- **Ranch perimeter:** mismatched box “hills” replaced with the same trunk/canopy trees as the yard so the forest reads continuous.
- Keeps view1: Phaser out of lobby, outdoor start cam, track support pillars.
- Cache-bust: `?v=20260928-view2`. Cast unchanged.

## What's new (view1) — Phaser out · start camera · track supports
- **Phaser removed** from lobby/engine picker (Canvas + Three.js only). `phaser-hub.js` left on disk but unwired; saved `ff-engine=phaser` migrates to Canvas.
- **Three.js start camera:** spawn aligned to outdoor yard / garage-mouth lineup (was inside house → garage blocked view). ~2.6s establishing shot from south frames all four froggies + four Cybertrucks, then blends to south-biased follow.
- **Track supports:** paired pillars + crossbeam under elevated ribbon points (Canvas + Three). Ramps footed on ground.
- Cache-bust: `?v=20260928-view2`. Cast unchanged. Do not publish froggies-sps.

## What's new (ctrl2) — Phaser black-screen fix
- **Phaser ranch black screen:** hop4 `ensureFrogTexture` nested `springLeg` declared `var hx` (hip X) which shadowed the module `hx()` hex→color helper → `TypeError: hx is not a function` in `create` → scene abort → black canvas. Renamed to `hipX`.
- Keep: ctrl1 gamepad hop buffer / RT·LT / pinch zoom, hop4 silhouette, track3, spacefix1. Cast: James, Jimmy, Bubbles, Rexy.
- Cache-bust: `?v=20260926-ctrl2`. Cast unchanged. Do not publish from agent.

## What's new (ctrl1) — reliable HOP + RT/LT + pinch zoom
- **Flaky gamepad X fixed:** `SimilarizeGamepad.pollPad` now frame-caches snapshots — a second poll same frame used to recompute rising edges against an already-updated `prev`, eating `buttonsPressed.x` (HOP intermittent). One snap per slot per browser frame; edges shared.
- **Hop buffer ~150ms:** If X/B/Space hits during the short ~0.1s anti-tap CD, hop queues and fires when ready (coyote/air stack still via `applyHop`).
- **Mech hop:** Three.js toast-only "Mech stomp" replaced with real `zVel`/`zLift` jump; Canvas/Phaser toast `HOP · mech jump!`. Truck hop unchanged.
- **Wall clear:** Airborne above clear height soft-passes solids (`resolveSolid` airHeight) so hop vaults low walls / obstacles.
- **RT accel / LT brake:** Triggers 6/7 (+ analog `ltValue`/`rtValue`) speed up / brake truck and mech (Canvas + Phaser + Three).
- **Pinch zoom:** Two-finger **spread = zoom OUT**, pinch-in = zoom IN on ranch (Canvas `adjustViewScale`; Phaser camera zoom; Three `camDist`). Does not steal 1-finger vjoy.
- Cache-bust: `?v=20260926-ctrl1`. Cast unchanged. No inventing places/toys. Do not publish from agent.

## Tesla Cybertruck fullscreen
Live entry includes `../shared/tesla-fullscreen.js` (YouTube-redirect chromeless trick). Desktop no-op; party `room=` query preserved. Full notes: [`games/shared/TESLA_FULLSCREEN.md`](../shared/TESLA_FULLSCREEN.md).

## What's new (spacefix1) — space env fix + shared blast-off slice
- **Forest leak fixed:** Starship/space episode always opaque-clears to a dark starfield + dark ground plane. Ranch trees/props/splash-forest never remain under the episode (Canvas lead; Three dark ground plane; Phaser screen-fixed stars/ground).
- **Orbit env spin fixed:** Camera locks on the planet while orbiting — only the froggy (and ship silhouette) orbits. Starfield + dark plane stay screen-stable (Canvas / Three / Phaser).
- **Ranch pad grounded:** Earth-layer ranch return pad sits on the Earth surface/terrain (not a floating detached artifact).
- **Shared blast-off:** Any froggy entering Starship boards **all four** (James, Jimmy, Bubbles, Rexy) together for launch.
- **Ship vs suit:** Exit Starship → EVA spacesuit jet (all four suit up); board ship to ride rocket again. Short loops.
- **Space zoom:** Mouse wheel zooms in/out in the space episode (ship + suit views).
- **Mars destination:** Hotspot on Mars in free-fly space → existing Mars cave/dogs scene (hang TBD; no new place names).
- Cache-bust: `?v=20260926-spacefix1`. Cast unchanged. No paid APIs. Do not message Webmaster.

## What's new (hop4) — faster hop + humanoid spring-leg frogs
- **Faster always-hop loco:** Higher launch + shorter ground plant (~0.011s) + stronger hop carry. Walk max nudged up. Snappier than hop3 — frogs cover ground faster while still always-hopping.
- **Humanoid frogs (not squish spheres):** Torso + head silhouette; **big springy legs** fold/extend on hop (spring out mid-air, tuck on land). Canvas lead draw; Three articulated thigh/shin; Phaser texture matches silhouette. Colors + hats per frog kept. Names-only plates.
- Keep: hop3 combo/cooldown (~0.1s spam HOP + stack height), robot mechs, shove props, joystick, garage1 door, polish11 truck yaw / EXIT tip, **track3 banks/rocks/monster wheels**. Cast: James, Jimmy, Bubbles, Rexy.
- Cache-bust: `?v=20260926-hop4`. Cast unchanged. No SPS arcade republish. (Parent may merge after track3 publish.)

## What's new (track3) — real Cybertruck track + monster wheels
- **Monster track:** Banked turn berms + jump ramps/mounds + **big rock obstacles** that bounce the truck hard (jolt ∝ wheel size × speed). Canvas lead visual + physics; Three + Phaser parity (banks/rocks elev + rock hit).
- **Live wheel size:** While driving, HUD **WHEELS** slider / **− +** (or hold **[ ]** / **- =**). Scale from stock Cybertruck (~1×) up to huge monster-truck wheels (~2.65×). Bigger wheels = more clearance, stronger suspension bounce, better ramp/rock jump physics.
- **Cam zoom-out:** Slightly more world visible by default; Canvas mouse-wheel zoom range widened; Three ranch cam farther; Phaser zoom min lowered.
- Keep: hop3 always-hop + combo HOP + robot mechs, shove props, joystick, garage1 door, polish11 truck yaw / brief EXIT tip, names-only plates. Cast: James, Jimmy, Bubbles, Rexy.
- Note: frog torso / spring-leg silhouette (hop4) left for parallel pass — this build is truck/track/wheels only.
- Cache-bust: `?v=20260926-track3`. Cast unchanged. No SPS arcade republish.

## What's new (hop3) — faster hop + spam HOP + stack height + mech robots
- **Faster always-hop loco:** Quicker cadence (shorter ground plant ~0.022s) + higher launch + longer hop carry. Walk max nudged up. Same always-hop-when-moving from hop2 — just snappier.
- **HOP ability cooldown ~0.1s:** Near-zero anti-double-tap only (was ~5.5–6s). Spam Space / HOP button immediately.
- **Combo stack height:** Ability HOP while airborne (or within ~0.15s land window) stacks extra upward lift (bunny-hop climb). Soft ceiling at ~10 stacks with diminishing returns — 5–12 hops feel rewarding; gravity brings you down. Toast shows `HOP ×N!`.
- **Mech art (robots, not skyscrapers):** 10 / 100 / 1000-story mechs redrawn with head, torso, shoulders, arms, legs, glow eyes — Canvas `world.js`, Three `three-hub.js`, Phaser `phaser-hub.js`. Labels stay anonymous (`N-story mech`). Garage toys tiny form pass (Three).
- Keep: always-hop when moving, shove props, joystick, garage1 door, polish11 truck yaw/EXIT tip, names-only plates. Cast: James, Jimmy, Bubbles, Rexy.
- Cache-bust: `?v=20260926-hop3`. Cast unchanged. No SPS arcade republish.

## What's new (hop2) — frogs ALWAYS hop when moving
- **Ranch foot locomotion:** Any non-zero move input (WASD / stick / tap-steer) drives a continuous hop cycle: launch arc → land → brief ground plant → next hop. No hover-slide / walk-glide between hops — frogs get around by hopping.
- **Cadence:** ~2 hops/sec with clear vertical lift (z / Y), squash on land, stretch mid-air. Tuned for kids — not a micro-bob.
- **Ability HOP (Space):** Still the bigger deliberate jump (higher/farther). Baseline movement already hops; ability is the power hop. Truck hop jump + space thruster unchanged.
- **Engines:** Canvas (`world.js` + `FroggiesCanon.tickLocoHop`), Phaser, Three — same always-hop plant/launch pattern. Cast: James, Jimmy, Bubbles, Rexy.
- Cache-bust: `?v=20260926-hop2`. Cast unchanged. No SPS arcade republish.

## What's new (hop1) — HOP frogs + shove small props
- **HOP ability (shared):** Replaces ZOOM on the ranch ability button. Vertical jump arc + forward carry (Space / ability button). Landing squash + mid-air stretch on Canvas; Phaser + Three show Y lift + scale squash/stretch. Fast walk auto-hops so frogs don't hover-glide; low speed keeps leg-bob walk. Truck = hop jump; space = thruster nudge ("HOP · thruster!").
- **Push small things:** Toys, backyard animals, pollen blobs get physics-ish knockback via `FroggiesCanon.shoveSmallProp` / `tickPushable` (Canvas world + Phaser/Three mesh sync). Walls / trucks / mechs stay solid via `resolveSolid`.
- Joystick (joy2), garage1 full-width door, polish11 truck yaw + brief EXIT tip kept. Names-only plates. Cast: James, Jimmy, Bubbles, Rexy.
- Cache-bust: `?v=20260926-hop1`. Cast unchanged. No SPS arcade republish.

## What's new (polish11) — truck yaw + brief EXIT tip + ZOOM
- **Cybertruck orientation:** Canvas / Phaser / Three — truck nose follows travel (velocity when moving; face yaw when slow). Parked trucks face the same default as idle frogs (screen-up / +Z), not sideways vs the cast.
- **EXIT tip:** Removed sticky world-space "EXIT · INTERACT / E" billboard that tracked the truck. Brief tip (~2.4s) after board via hub tip; INTERACT button stays labeled EXIT while driving. No giant HUD that follows you across the ranch.
- **ZOOM ability (shared):** Replaces per-frog DASH/SHIELD/ZAP/BOT on the ranch ability button. Short speed burst along face/travel with dust/spark juice (Canvas + Phaser + Three); space = thruster nudge. INTERACT still boards/exits/phone/etc.
- Joystick (joy2), garage1 full-width door, names-only plates, Canvas party path kept.
- Cache-bust: `?v=20260926-polish11`. Cast unchanged. No SPS arcade republish.

## What's new (garage1) — garage door wall-to-wall
- **Three.js:** South bay door + lintel span between side walls (was a ~2.2-unit floating slab in a ~9.6-wide bay); left/right jambs frame the entrance. Open/close lift unchanged.
- **Collision / Canvas / Phaser:** Closed door blocks nearly the full garage front (`GARAGE_DOOR_W` ~440); Canvas + Phaser door draw match.
- Cache-bust: `?v=20260926-garage1`. Cast unchanged. No SPS arcade republish.

## What's new (joy2) — joystick visible + touchable on phone
- **Visibility:** Idle opacity ~0.92 (was 0.48 glass-ghost on ranch); stronger border / knob / cyan glow; still stylish glass-neon, not a solid blob.
- **Touch target:** Phone stick ~118px; z-index 32 above engine-host/canvas.
- **Pointer events:** `#vjoy` moved to **body sibling** (not under `#controls` `pointer-events:none`); `#controls .vjoy { pointer-events: auto !important; }` kept as defense.
- **Bind:** `bindVirtualJoystick` still runs on boot for Canvas + Phaser + Three; `setSteer` / `onJoySteer` unchanged.
- **Hint:** Brief scale/glow pulse once on first hub/space enter.
- Cache-bust: `?v=20260926-joy2`. Cast unchanged. No SPS arcade republish.

## What's new (joy1) — universal virtual joystick (Canvas / Phaser / Three)
- **Stick:** Bottom-left glass-neon virtual joystick (base + knob, thumb-sized, safe-area, low opacity). Drag → `setSteer(x,y)` via **engine-boot**; release → center + stop. Walk + truck.
- **Shared path:** Same HUD stick for Canvas, Phaser, and Three (`FroggiesEngines.onJoySteer` / `applySharedSteer`). INTERACT + ability stay on the right.
- **Touch:** Full-screen playfield hold-to-aim no longer primary on touch (mouse aim still optional on desktop). Desktop **WASD** unchanged.
- Cache-bust: `?v=20260926-joy1`. Cast unchanged. No SPS arcade republish.

## What's new (truck2) — EXIT anytime + real track hills/jumps
- **EXIT:** Root cause — Three/Phaser HUD set `btnInteract.disabled = !near`, so once you drove off the parked pad INTERACT was dead (mobile trapped; tip lied). Fix: `inTruck` keeps button ready + labeled EXIT; E/INTERACT exits Canvas + Three + Phaser anytime. Tip always shows EXIT while driving.
- **Track 3D:** Root cause — `trackElevAt` too soft; Three applied elev then `* 0.08` on truck Y (invisible); Phaser drew `zLift * 0.06` (~3px). Ramps were jump triggers only (no ride-up). Fix: stronger `trackElevAt` (path + mounds + **ramp wedges**); snappy ground follow; crest/ramp launch; Three ribbon elev + taller ramps; full Y lift (no damp); air/land vs ground plane.
- Cache-bust: `?v=20260926-truck2`. Cast unchanged. No SPS arcade republish.

## What's new (truck1) — Cybertruck proportion + steer yaw + track reaction
- **Proportion:** Kid-toy Cybertruck vs frogs (bigger than a frog, not a building). `FroggiesCanon.TRUCK_VIS` — Canvas scale 0.86 (was 1.2), Three mesh ×2.05, Phaser ×1.28. Parked + driven.
- **Steering:** Was facing flip only (180° left/right). Now smooth yaw toward tap-steer / WASD aim; thrust along facing with light aim blend (Canvas `moveEntity`, Three, Phaser).
- **Track:** `trackElevAt` samples ribbon elev + mounds. Trucks follow undulation, crest-launch over downhill lips, keep hang, land bounce. Uses existing ramps/TRACK_MAIN/MOUNDS.
- Cache-bust: `?v=20260926-truck1`. Cast unchanged. No SPS arcade republish.

## What's new (eyes1) — frog eyes face walk direction + hide D-pad
- **Eyes / face:** Canvas, Three, Phaser — frog (or face) rotates toward walk direction; idle keeps last facing. Includes AI companions.
- **D-pad:** bottom-left btn-up/down/left/right hidden via CSS (`display:none`) on phone and desktop. Tap/finger steer primary; INTERACT + ability stay on the right; WASD remains for desktop. No empty gaps (absolute controls).
- Cache-bust: `?v=20260926-eyes1`. Cast unchanged. No SPS arcade republish.

## What's new (tapsteer1) — floor pad stable + faster + tap-to-steer
- **Floor flicker:** 1000-story mech pad was a thick cylinder coplanar with backyard box → z-fight while walking past. Now flat raised CircleGeometry pad + ring (`depthWrite: false`, polygonOffset); backyard is a PlaneGeometry.
- **Speed:** walk + drive raised noticeably (Canvas world.js, three-hub, phaser-hub).
- **Tap / click steer:** hold on playfield aims **direction** from player toward pointer (not go-to destination). WASD still wins when held (D-pad hidden as of eyes1). Gold ring+arrow marker at tap. Release stops (brief coast). Wired Canvas + Three + Phaser.
- Cache-bust: `?v=20260926-tapsteer1`. Cast unchanged. No SPS arcade republish.

## What's new (solid1) — Three floor stable + cast-only names + solid walls
- **Floor artifact:** z-fighting — GridHelper + thick AREA boxes + soft shadow discs coplanar with ground; fixed by flat zone pads, lifted grid (`depthWrite: false`), lifted shadow discs (`depthWrite: false`), shadow bias.
- **Role tags:** removed Wheel/Shield/Zap/Bot under frog seat names (cast names only; roles TBD).
- **Collision:** `FroggiesCanon.resolveSolid` — house/garage walls (south doorway open), mech pads, parked trucks; soft pond rim optional. Wired in three-hub + world.js + phaser-hub.
- Cache-bust: `?v=20260926-solid1`. No SPS arcade republish.

## What's new (mapfix1) — Three mini-map not fullscreen
- Root cause: `#engine-host canvas { width/height: 100% !important }` stretched the Three mini-map overlay canvas to the whole screen → giant blurry "MAP" box on phone.
- Fix: only the renderer canvas fills the host; `canvas[data-ff-minimap]` keeps its corner size.
- Cache-bust: `?v=20260926-mapfix1`.

# Four Froggies — ranch hub + space episode (multi-engine)

**Product:** **Four Froggies** (one shared world). Publish: `games/four-froggies/`.

Flagship experience: walkable / drivable **ranch hub** with the same Ben-canon cast and areas. Story paths in-world: **phone → Purple Bear → SPS → Optimus kits → bring Jimmy home**, and **Starship (Spotty) → space episode**.

**Physics / look engines (production compare):** pick on the lobby — **Canvas** (default) · **Phaser 3** · **three.js**. Same story/cast; engines are presentation/physics only. **No Square Enix IP**, assets, music, or names. Canon: `WORLD_BIBLE` (Ben-named only — no invented cast).

## How to open locally

```bash
cd /workspace/SimilarizeWebsite/games/four-froggies
python3 -m http.server 8765
```

Open **http://localhost:8765/** (same Wi‑Fi URL on phones).

- `file://` works for **solo** (all three engines).
- **Host / Join** needs `http(s)://` (PeerJS + clipboard/QR) — **Canvas path only**.

## How to switch engines

1. On the lobby, tap **Canvas | Phaser | Three**.
2. Choice is saved in `localStorage` key `ff-engine`.
3. Tap **GO · Ranch Hub**.
4. **Lobby** returns to the title screen (you can switch engine again).

| Engine | Entry | Party (PeerJS) | Notes |
|--------|-------|----------------|--------|
| **Canvas** (default) | Current `fflook1` hub + full space episode | **Yes** — Host/Join | Default production path |
| **Phaser 3** (CDN) | Ranch + thin space stub | **Solo-first** | Arcade physics + y-follow camera |
| **three.js** (CDN) | Ranch + thin space stub | **Solo-first** | Fixed-angle 2.5D-ish (orbit **locked** / isometric-ish) — **not** free-fly FPS |

Phaser and three.js load their CDNs on first GO (no paid APIs). Party sync is intentionally not wired on those paths yet — note for Webmaster / next pass.

## How to play (Canvas — full)

### Solo

1. Claim a froggy (or GO defaults to **James**).
2. **GO · Ranch Hub** → you + **3 AI** companions.
3. **WASD / arrows** (desktop) or **bottom-left joystick** (touch) to walk — D-pad hidden. Near the Cybertruck → **INTERACT** to drive.
4. On the **monster truck track**, hit the **jumps** for scrap / stunt combo.
5. Near **Pond / Fishies** → INTERACT for splash.
6. Near **Phone** (ranch house) → INTERACT → dial **Purple Bear** → **Open SPS**.
7. On SPS: **Optimus** kits → **Bring Jimmy home**.
8. **Starship** (Spotty) → INTERACT → **space episode**.
9. **E / F** or INTERACT for hotspots. Ability = role skill.

### Space episode (Canvas — full path)

Starship (Spotty) → Solar System free-fly (Sun center; Earth home; Moon + station orbit Earth; all planets gravity wells; asteroid belt; Pluto) → Jimmy chase near Earth → station (Alex + Fred + ~20 people) → solar map (Mars moons + Neptune’s 14) → Mars cave / King Germy → mech set-piece. Scale compressed (labeled). Soft stubs as before.

### Phaser / three.js (solo — landmark parity)

Same big-map landmarks as Canvas (house compound + mechs, squiggle track, pond fish+whales, 4 Cybertrucks + shared pile-in, Starship). Drive onto pond for on-water / underwater look. INTERACT at **Starship** → thin **space** stub: chase **Jimmy**, Moon orbit pull, **Escape** / hard thruster leave, see **Spotty / Germy / Daisy**, return pad → ranch. Phone/SPS stay toast stubs (full HUD on Canvas). Party sync not wired. three.js WASD is camera-relative (matches Canvas).

### Party (Host + Join) — Canvas only

Same PeerJS lobby: **Host room** → QR / invite `?room=CODE` → claim seats → Host taps **GO**. Empty seats = AI. Phaser/Three ignore Host/Join for now (solo).

| Froggy | Ability (temp · TBD Ben) |
|--------|---------------------------|
| James (Wheel) | **DASH** |
| Jimmy (Shield) | **SHIELD** |
| Bubbles (Zap) | **ZAP** |
| Rexy (Bot) | **BOT** |

## Files

| File | Role |
|------|------|
| `index.html` | Lobby + engine picker + hub HUD + phone/SPS panels |
| `canon.js` | Shared Ben-canon: map, compound, track, trucks, pond helpers (all engines) |
| `engine-boot.js` | **Thin lobby switcher** + CDN load + boot / HUD bridge (capture-phase GO) |
| `phaser-hub.js` | Phaser 3 ranch (Canvas-parity landmarks) + thin space stub (CDN) |
| `three-hub.js` | three.js fixed-angle ranch (Canvas-parity + camera-relative WASD) + thin space stub |
| `world.js` | Canvas ranch map / physics / render — **Canvas executor owns scale** |
| `story.js` | Phone (Purple/Blue Bear) + SPS + Optimus (Canvas) |
| `space.js` | Canvas space episode scenes |
| `main.js` | Canvas lobby / hub / space / PeerJS — **not patched by engine switcher** |
| `party.js` | PeerJS Host/Join (**Canvas path**) |
| `ai.js` / `strip.js` | Strip-era leftovers (not default entry) |
| `assets/` | Imagine splash / backdrop |

**Collision rule:** Shared landmark layout lives in `canon.js`. Canvas `world.js` remains the richest renderer; Phaser/Three hubs consume the same canon landmarks for parity. Do not invent cast/zone names. Preserve three.js camera-relative WASD.



## What's new (mobile1) — phone + desktop both playable
- Phone lobby scrolls; verbose invite wall hidden on ≤520px; Host/engine/GO thumb-sized.
- D-pad/ability no longer balloon on coarse/≤800 (that crushed the playfield); compact on phone, desktop sizes unchanged.
- `100dvh` + safe-area; ESCAPE button actually positioned; DPR/visualViewport resize + orientation.
- Mini-map smaller on narrow / hidden in landscape phone; harder particle caps on narrow.
- Phaser closer zoom on phone; Three minimap/ambient match.
- Cache-bust: `?v=20260926-mobile1`. Cast unchanged. No SPS arcade republish.

## What's new (polish10) — feel / bug polish for playtest

Ben: LOTS more improvement but may playtest soon — prioritize feel/bug polish over garnish. Canvas lead; Phaser/Three cheap ports. Three frogs + hollow house + camera-relative WASD kept. No SPS arcade republish.

**UI declutter (all engines):**
- Mini-map moved **top-left**, smaller, lower opacity — clear of right INTERACT / ability
- Zone signs fade when standing on them, lifted higher, quieter — frogs stay visible
- Nameplates toned down (name only, no · you / · AI clutter)

**Movement / truck:**
- Tighter friction + snappier accel; less camera look-ahead / walk-tilt fight
- **EXIT truck anytime** while driving (was stuck until back at pad) — BOARD / EXIT prompts + button label

**Perf + delight:**
- Cap fireflies / pollen / dust plumes
- Soft **sunset / dusk sky shift** over play time (~7 min cycle)

- Cache-bust: `?v=20260925-polish10`.

## What's new (polish9) — party clarity + track gate + Optimus punch

Ben: LOTS more improvement. Canvas lead; Phaser/Three parity on nameplates, aboard icons, track gate, Optimus visual punch lite. Three frogs + hollow house kept.

**Canvas party / solo (`world.js` + `main.js` + `index.html` + `style.css`):**
- Frog **color nameplates** that stay readable (every frog: you / AI / joined)
- Clearer **Host / Join / QR** lobby instructions (no new party systems)
- Shared Cybertruck: **aboard frog icons** on the truck + HUD convoy strip

**Optimus kits (`story.js` + ranch FX):**
- Visual punch for rocket / afterburners / drone / hover / map ping (SPS seeker + ranch burst)

**Monster truck track (`world.js`):**
- **START / FINISH** gate on the west straight
- Lap sparkle + scrap when crossing; brief **air hang** at jump apex

**Space (`space.js`):**
- Germy / Daisy / King Germy **presence markers** (glow rings)
- Soft **hundreds of dogs** silhouette flock tease near Mars cave only (ambient dots)

**Phaser / three.js:** nameplates + aboard icons + track gate + Optimus punch lite. Three frogs + hollow house kept.

- Cache-bust: `?v=20260925-polish9`.

## What's new (polish8) — art punch + destination clarity

Ben: LOTS more improvement. Canvas lead; Phaser/Three cheap ports of truck silhouette + house porch + whale breach + destination beacon. Three frogs + hollow house kept.

**Canvas ranch (`world.js` + `story.js` + `style.css`):**
- Cybertrucks: more Cybertruck-like angular stainless silhouette, full-width light bar, wheel arches (on-water / underwater modes kept)
- Ranch house: stronger 2.5D porch depth layers, chimney smoke, path to door
- Pond: bigger whale breach arcs; fish schools
- Purple Bear phone: warmer call panel; Blue Bear place-bound pet bounce near house

**Space (`space.js`):**
- Clearer labels for Moon / Mars moons / Neptune moons in picker + overhead
- Soft destination beacon when heading toward a body

**Phaser / three.js:** truck silhouette + house porch + whale breach + destination beacon. Three frogs + hollow house kept.

- Cache-bust: `?v=20260925-polish9`.

## What's new (polish7) — play feel + readability

Ben: LOTS more improvement. Canvas lead; Phaser/Three parity on zone signs, mini-map lite, companion bounce.

**Canvas ranch (`world.js` + `main.js` + `style.css`):**
- Clearer zone signs **HOUSE / TRACK / POND / GARAGE / STARSHIP** that fade in when approaching
- Corner mini-map: player + landmarks + frogs
- Ability button feedback flash for **DASH / SHIELD / ZAP / BOT** (roles hang as TBD — not locked)

**Party / solo AI (`world.js`):**
- Idle bounce when settled
- Follow lag so companions trail (not snap)
- Chat bubble one-liners from **existing** toast/tip strings only (no new dialogue scripts)

**Space (`space.js`):**
- Mars cave entrance tease — **3-level secret** + **back-door** labeled hooks only
- Jimmy jetpack escape visual when ability used in space (stronger plume / NPC punch)

**Phaser / three.js:** zone signs + mini-map lite + companion bounce/follow lag. Three frogs + hollow house kept.

- Cache-bust: `?v=20260925-polish8`.

## What's new (polish6) — FF-like depth + story/combat teases

Ben: LOTS more improvement. Canvas lead; Phaser/Three parity on depth shadows, parallax-lite, space silhouette tease. Three frogs + hollow house kept.

**Canvas depth feel (`world.js` + `main.js`):**
- Stronger layered ranch-hill parallax (far/mid/near/FG bands)
- Soft layered drop shadows under frogs, trucks, backyard animals
- Wider scale-with-depth so distant props shrink / near punch
- Slight camera tilt/bob when walking (truck bounce lite when driving)

**Story loop juice (`story.js` + `style.css`):**
- Playful Purple Bear phone UI (pulse, wiggle, speech bounce)
- SPS map flash when Map ping / dish used
- Jimmy rogue hint when Optimus kits active
- Hang TBD hooks reserved (`onHangTbd`) — no new hang system invented

**Combat fantasy tease (visual only):**
- Distant invader mech silhouettes when near Mars (Canvas space + Phaser/Three stubs)
- James 1000-story mech approach → ★ WOW scale tip on ranch

**Phaser / three.js:** depth shadows under player, parallax-lite hills, Mars silhouette tease, mech wow tip. Three hollow house + frogs unchanged.

- Cache-bust: `?v=20260925-polish6`.

## What's new (polish5) — space clarity + ranch juice

Ben: LOTS more improvement. Canvas lead; Phaser/Three cheap ports of most visible juice. Three frogs + hollow house kept.

**Space episode (Canvas `space.js`):**
- Moons picker: Mars/Neptune tab pills with planet discs, big selected cards + ▶◀, selected summary bar
- Orbit pull readable: soft-pull dashed halo, capture ring, lock orbit ring, gravity pull line + % toast
- Escape / thruster leave: on-screen ORBIT banner (ESCAPE button / Esc · Ability = hard thruster)
- Spotty / Alex / Fred presence pulses (cast already stubbed — no new names)

**Ranch juice (Canvas `world.js`):**
- Day ambient pollen + fireflies across the map
- Pond ripple rings (ambient + splash/land)
- Track dust plumes when trucks race
- Mech garage door rolls open when frogs near (OPEN label)
- Shared pile-in Cybertruck: gold pad, 4 frog slots, ★ ALL ABOARD · 4 badge

**Audio-free juice:**
- Tiny screen shake on truck land (dry or wet)
- Sparkle burst when entering a hotspot

**Phaser / three.js:** ambient particles, pond ripples, track race dust, garage door open-near, ALL ABOARD shared truck, land shake, hotspot sparkle, orbit pull rings + Escape banner, Alex/Fred in space stub. Three hollow house + frogs unchanged.

- Cache-bust: `?v=20260925-polish5`.

## What's new (polish4) — ranch presence + drive feel

Ben: LOTS more improvement. Canvas lead; Phaser/Three get the most visible cheap ports. threefix2 hollow house + frogs kept.

**Canvas (world.js):**
- Ranch house: rooms readable through windows (sofa/table/lamp/bed) + ajar doorway shows hallway
- Backyard animals denser/larger with legs + heads; 10/100 mechs punchier in garage; 1000-story silhouette dramatic out back
- Monster-truck track elevation/hills more readable (stronger ribbon lift, contour rings, HILL labels)
- Starship pad clearer destination (lights, chevrons, ★ STARSHIP · SPACE); phone → Purple Bear / SPS → Optimus · Jimmy inviting
- Drive feel: truck bounce, spray on water, bubbles underwater, footstep dust on walk

**Phaser / three.js:** bigger animals/mechs, warm house windows + doorway, taller hills, clearer Starship + story hotspot props, bounce/spray/dust FX. Three house stays hollow with interior props frogs can walk through.

- Cache-bust: `?v=20260925-polish4`.

## Prior (polish3) — Canvas ranch clarity + Phaser/Three parity

**Canvas:** sharper house/garage/mechs, track contrast, pond foam, truck wedge, charming frogs, snappier move/camera.
**Phaser / three.js:** truck wedge, brighter fish/whales, snappier move + follow. Three hollow house kept.

- Cache-bust: `?v=20260925-polish3`.

## What's new (threefix2) — three.js frogs + move visible

**Root cause (Three blank frogs / “move does nothing”):**
1. `scene.add(labelSprite(...)).position.set(...)` — `Object3D.add` returns the **scene**, so every label call silently moved `scene.position`. Camera tracked local player coords while meshes rendered offset → froggies off-camera / “input does nothing.”
2. Ranch house (and garage) were **solid boxes**; `COMPOUND.spawn` is inside the house rect, so frog meshes were buried inside opaque geometry (labels still showed — sprites use `depthTest: false`).

**Fixes:** `addLabel()` helper (never chain `.position` on `scene.add`); hollow house/garage (floor + walls); larger colored frog meshes (James/Jimmy/Bubbles/Rexy) + AI name tags; camera snap to spawn + snappier follow; host sizing; truck near-pulse; Phaser/Canvas frog clarity polish.

- Cache-bust: `?v=20260925-threefix2`.

## Prior (parity1) — Phaser/Three toward Canvas landmarks

Ben: keep pouring polish into Canvas, **and** remember everything done on Canvas for Phaser + three.js.

- Shared `canon.js` landmark bible: ~10× map, house compound (backyard / garage / 10·100·1000 mechs), squiggle track + mounds/ramps, 4 Cybertrucks + shared pile-in, pond helpers, Starship approach, orbit physics.
- **Phaser / three.js** hubs: big map, compound, varied track (not oval), big pond with fish+whales, Cybertruck on-water / underwater look, 4 trucks + shared pile-in, Starship → space with planet orbit pull + Escape / hard thruster leave. Solo-first PeerJS OK.
- **three.js WASD** camera-relative (Canvas `steer.y < 0` = screen up) — do not invert (`three-dir1` fix kept).
- **Canvas** not regressed; small polish: Starship approach uses shared gold guide from canon.

## Still stubbed / next

- Phaser/Three: fold full phone/SPS/Optimus HUD + richer space scenes
- Party sync for Phaser/Three (or keep Canvas-only party)
- Neptune moon landings as unique playable stops
- Party sync for Canvas space positions
- Role↔froggy mapping confirmation (Ben)
- Confirm King Germy ↔ Germy relation (Ben)

## Ben decisions needed

- Confirm temp roles (James Wheel / Jimmy Shield / Bubbles Zap / Rexy Bot)
- Which engine becomes long-term default after playtest
- King Germy same dog as Germy or title only?
- Which Neptune moons get first playable landings

## Publish

Game Creator → **Webmaster** only. Product path: `games/four-froggies/`. Do not republish SPS as arcade. Cache-bust: `?v=20260925-polish9`.
