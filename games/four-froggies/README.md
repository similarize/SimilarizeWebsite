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
3. **WASD / arrows** or on-screen D-pad to walk. Near the Cybertruck → **INTERACT** to drive.
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


## What's new (polish3) — Canvas ranch clarity + Phaser/Three parity

Ben: lots more polish. Canvas lead; cheap clarity wins ported to Phaser + three.js. threefix2 frog move kept (hollow house, addLabel, visible frogs).

**Canvas (world.js / main.js):**
- Sharper house (glow windows, lit door), garage roll-up door, mechs with window bands + antenna/pad
- Track lanes higher contrast (dirt / amber / chalk); pond foam + fish eyes/fins + whale spout
- Cybertrucks more truck-like (wedge nose, bed, dual rear, mirrors, lights)
- Froggies charming (blush, smile, belly, kick legs) + clearer INTERACT hotspot badges
- Snappier walk/drive; camera sticks without fighting look-ahead

**Phaser / three.js:** truck wedge meshes, brighter fish/whales, snappier move + camera follow, clearer INTERACT tips. Three house stays hollow; frogs stay visible/moving.

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

Game Creator → **Webmaster** only. Product path: `games/four-froggies/`. Do not republish SPS as arcade. Cache-bust: `?v=20260925-threefix2`.
