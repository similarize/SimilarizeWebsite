# Four Froggies — 2.5D ranch hub

**Product:** **Four Froggies** (one shared world). Publish: `games/four-froggies/`.

Flagship experience: walkable / drivable **lightweight 2.5D ranch hub** (fixed-angle, y-sorted layers). Areas you can go between: **ranch house**, **monster truck track**, **pond** (fishies) + toy prop density. Story stubs in-world: **phone → Purple Bear**, **SPS** (Solar Positioning System).

The old Canvas 2D strip lives in `strip.js` as a party/sandbox leftover — **not** a second product. Not loaded by default.

## How to open locally

```bash
cd /workspace/SimilarizeWebsite/games/four-froggies
python3 -m http.server 8765
```

Open **http://localhost:8765/** (same Wi‑Fi URL on phones).

- `file://` works for **solo hub**.
- **Host / Join** needs `http(s)://` (PeerJS + clipboard/QR).

## How to play

### Solo

1. Claim a froggy (or GO defaults to **James**).
2. **GO · Ranch Hub** → you + **3 AI** companions.
3. **WASD / arrows** or on-screen D-pad to walk. Near the Cybertruck hotspot → **INTERACT** to drive.
4. Near **Phone** (ranch house) → INTERACT → dial **Purple Bear** → open **SPS**.
5. **E / F** or INTERACT for hotspots. Ability button = role skill (hub stubs OK).

### Party (Host + Join)

Same PeerJS lobby as before: **Host room** → QR / invite `?room=CODE` → friends claim seats → only Host taps **GO** → everyone enters the hub. Empty seats = AI. Solo still works offline if PeerJS fails.

| Froggy | Ability (temp · TBD Ben) |
|--------|---------------------------|
| James (Wheel) | **DASH** |
| Jimmy (Shield) | **SHIELD** |
| Bubbles (Zap) | **ZAP** |
| Rexy (Bot) | **BOT** |

## Files

| File | Role |
|------|------|
| `index.html` | Lobby + hub HUD + phone/SPS panels |
| `world.js` | 2.5D ranch map, areas, render, hub AI wander |
| `story.js` | Phone (Purple/Blue Bear) + SPS solar map stub |
| `main.js` | Lobby hooks, hub loop, input, party sync |
| `party.js` | PeerJS Host/Join (hub steerX/Y + strip compat) |
| `ai.js` | Strip-era lane AI (kept; hub wander is in `world.js`) |
| `strip.js` | Legacy 2D ranch-run sandbox (not default entry) |
| `assets/` | Imagine splash / backdrop |

## Still stubbed / next

- Optimus advanced kits in hub (rocket, afterburners, drone, hover, map ping)
- Full Jimmy-rogue bring-home loop (SPS track + win beat)
- Richer Imagine art / Cybertruck models
- Strip “Ranch Run” as optional track activity wire-up
- Role↔froggy mapping confirmation (Ben)

## Ben decisions needed

- Confirm temp roles (James Wheel / Jimmy Shield / Bubbles Zap / Rexy Bot)
- Ranch map connectivity + photo refs
- Pond interact depth; Optimus kit priority in hub

## Publish

Game Creator → **Webmaster** only. Product path: `games/four-froggies/`. Do not republish SPS as arcade. Cache-bust: `?v=20260925-hub1`.
