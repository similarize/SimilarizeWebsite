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
3. **WASD / arrows** or tap/hold on playfield to walk (D-pad hidden). Near the Cybertruck → **INTERACT** to drive.
4. On the **monster truck track**, hit the **jumps** for scrap / stunt combo.
5. Near **Pond / Fishies** → INTERACT for splash.
6. Near **Phone** (ranch house) → INTERACT → dial **Purple Bear** → **Open SPS**.
7. On SPS: **Optimus** kits → **Bring Jimmy home**.
8. **Starship** (Spotty) → INTERACT → **space episode**.
9. **E / F** or INTERACT for hotspots. Ability = role skill.

### Space episode (Canvas — full path)

Starship (Spotty) → Space/Moon (Jimmy jetpack chase; Germy + Daisy) → station (Alex + Fred + ~20 people) → solar map (Mars moons + Neptune’s 14) → Mars cave / King Germy → mech set-piece. Soft stubs as before.

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
