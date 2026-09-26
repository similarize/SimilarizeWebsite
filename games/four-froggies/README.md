# Four Froggies — 2.5D ranch hub

**Product:** **Four Froggies** (one shared world). Publish: `games/four-froggies/`.

Flagship experience: walkable / drivable **lightweight 2.5D ranch hub** (fixed-angle, y-sorted layers). Areas: **ranch house**, **monster truck track**, **pond** (fishies) + toy prop density. Story path in-world: **phone → Purple Bear → SPS → Optimus kits → bring Jimmy home**.

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
3. **WASD / arrows** or on-screen D-pad to walk. Near the Cybertruck → **INTERACT** to drive.
4. On the **monster truck track**, hit the **jumps** for scrap / stunt combo; scrape while turning for extra scrap.
5. Near **Pond / Fishies** → INTERACT for splash (fishies scatter).
6. Near **Phone** (ranch house) → INTERACT → dial **Purple Bear** → **Open SPS**.
7. On SPS: use **Optimus** kits (**Hover / Rocket / Afterburners / Drone / Map ping**) to raise lock and reach Jimmy’s blip → **Bring Jimmy home**. Soft fail → funny retry.
8. **E / F** or INTERACT for hotspots. Ability button = role skill.

### Party (Host + Join)

Same PeerJS lobby: **Host room** → QR / invite `?room=CODE` → friends claim seats → only Host taps **GO** → everyone enters the hub. Empty seats = AI. Solo still works offline if PeerJS fails.

| Froggy | Ability (temp · TBD Ben) |
|--------|---------------------------|
| James (Wheel) | **DASH** (juice + truck boost) |
| Jimmy (Shield) | **SHIELD** |
| Bubbles (Zap) | **ZAP** |
| Rexy (Bot) | **BOT** (nudge toward SPS / Optimus) |

## Files

| File | Role |
|------|------|
| `index.html` | Lobby + hub HUD + phone/SPS/Optimus panels |
| `world.js` | 2.5D ranch map, parallax, track jumps, pond FX, render |
| `story.js` | Phone (Purple/Blue Bear) + SPS solar map + Optimus kits + Jimmy loop |
| `main.js` | Lobby hooks, hub loop, drive/pond juice, party sync |
| `party.js` | PeerJS Host/Join (hub steerX/Y + strip compat) |
| `ai.js` | Strip-era lane AI (kept; hub wander is in `world.js`) |
| `strip.js` | Legacy 2D ranch-run sandbox (not default entry) |
| `assets/` | Imagine splash / backdrop |

## What’s new (hub2)

- Richer ranch read: parallax hills/clouds, house porch/windows/chimney, track markings + jump ramps, water shimmer, y-sort shadows, Cybertruck stainless wedge, froggies with hats.
- Fun loops under ~1 min: track jumps/scrap/stunt combo; pond splash scare; full phone→SPS→Optimus→bring Jimmy home with soft-fail retry.
- Juice: dash trails/sparks, camera ease + look-ahead, interact pulse, Web Audio beeps.

## Still stubbed / next

- Deeper role abilities beyond juice stubs (Shield/Zap/Bot combat)
- Strip “Ranch Run” as optional track activity wire-up
- Richer Imagine bitmap sprites (procedural OK for now)
- Role↔froggy mapping confirmation (Ben)

## Ben decisions needed

- Confirm temp roles (James Wheel / Jimmy Shield / Bubbles Zap / Rexy Bot)
- Ranch map connectivity + photo refs
- Which Optimus kits feel best first for kids (all five wired; priority TBD)

## Publish

Game Creator → **Webmaster** only. Product path: `games/four-froggies/`. Do not republish SPS as arcade. Cache-bust: `?v=20260925-hub2`.
