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

### Phaser / three.js (solo stubs)

Same ranch areas & hotspots (house, track, pond, phone, SPS, Cybertruck, Starship/Spotty). INTERACT at **Starship** → thin **space** stub: chase **Jimmy**, see **Spotty / Germy / Daisy**, return pad → ranch. Phone/SPS are toast stubs on these engines (full HUD remains on Canvas).

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
| `canon.js` | Ben-canon constants for Phaser/Three only (independent map scale) |
| `engine-boot.js` | **Thin lobby switcher** + CDN load + boot / HUD bridge (capture-phase GO) |
| `phaser-hub.js` | Phaser 3 ranch + thin space stub (CDN, on demand) |
| `three-hub.js` | three.js fixed-angle ranch + thin space stub (CDN, on demand) |
| `world.js` | Canvas ranch map / physics / render — **Canvas executor owns scale** |
| `story.js` | Phone (Purple/Blue Bear) + SPS + Optimus (Canvas) |
| `space.js` | Canvas space episode scenes |
| `main.js` | Canvas lobby / hub / space / PeerJS — **not patched by engine switcher** |
| `party.js` | PeerJS Host/Join (**Canvas path**) |
| `ai.js` / `strip.js` | Strip-era leftovers (not default entry) |
| `assets/` | Imagine splash / backdrop |

**Collision rule:** Phaser/Three live in `phaser-hub.js` / `three-hub.js` + `engine-boot.js`. They do **not** rewrite `world.js` ranch scale. Canvas expansion (10× map, house, track, trucks, etc.) stays on the Canvas files.

## What's new (engines1) — physics & look only

Ben: “let’s do all three” = keep Canvas **and** add Phaser 3 **and** three.js for production physics/look comparison. Same story/cast.

- Lobby **engine picker** + `localStorage` remember (`ff-engine`).
- Canvas path unchanged as **default** (PeerJS intact); switcher does not patch `main.js` / `world.js`.
- Phaser 3 via jsDelivr CDN — `phaser-hub.js` ranch + space stub.
- three.js (r134 UMD CDN) — `three-hub.js`, **locked** isometric-ish camera (no orbit drag / no FPS free-look).
- Cache-bust: `?v=20260925-ranchbig1`.

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

Game Creator → **Webmaster** only. Product path: `games/four-froggies/`. Do not republish SPS as arcade. Cache-bust: `?v=20260925-ranchbig1`.
