# Four Froggies — 2.5D ranch hub + space episode

**Product:** **Four Froggies** (one shared world). Publish: `games/four-froggies/`.

Flagship experience: walkable / drivable **lightweight 2.5D ranch hub** (fixed-angle isometric-ish, depth-scaled sprites, y-sorted layers). Areas: **ranch house**, **monster truck track**, **pond** (fishies) + toy prop density. Story paths in-world: **phone → Purple Bear → SPS → Optimus kits → bring Jimmy home**, and **Starship (Spotty) → space episode**.

The old Canvas 2D strip lives in `strip.js` as a party/sandbox leftover — **not** a second product. Not loaded by default. Space is **not** a second arcade cab — it is a story path inside this build.

## How to open locally

```bash
cd /workspace/SimilarizeWebsite/games/four-froggies
python3 -m http.server 8765
```

Open **http://localhost:8765/** (same Wi‑Fi URL on phones).

- `file://` works for **solo hub / space**.
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
8. **Starship** (NW grounds, Spotty pad) → INTERACT → **space episode** (see below).
9. **E / F** or INTERACT for hotspots. Ability button = role skill (also mech blast / moon cycle in space).

### Space episode (from ranch Starship)

Playable stub path (fun under ~1 min if you rush):

1. **Starship** — **Spotty** (cat commander) · Launch into space · Return to ranch hotspot.
2. **Space / Moon** — **Jimmy** in space-suit jetpack constantly getting away (chase / catch loop). **Germy** + **Daisy Dachshund** float nearby.
3. **Space station** — **Alex** + **Fred** astronauts + **~20 people** crowd sprites. Talk for tips; open **Solar map**; path to **mech fight**.
4. **Solar map** — **two moons of Mars** (Phobos, Deimos) + **Neptune’s 14 moons** list/picker (real names; Ability cycles pick; Go travels). Mars → playable cave; Neptune moons = stub stops (toast).
5. **Mars cave + King Germy** — 3-level-deep passageways → dog chamber / garage / compartment; **hundreds of dogs** as pack density (no invented dog names); secret **back-door escape hallway** with a door.
6. **Mech set-piece** — foreign invader mechs vs **James 1000-story mech** (INTERACT / Ability blast; soft-fail reboot).

**King Germy** vs **Germy**: both labels kept visible (cave title vs doggy in space) — no lore merge until Ben confirms.

### Party (Host + Join)

Same PeerJS lobby: **Host room** → QR / invite `?room=CODE` → friends claim seats → only Host taps **GO** → everyone enters the hub. Empty seats = AI. Solo still works offline if PeerJS fails. Space episode is a **local story path** from the hub (ranch loops + party sync remain for hub).

| Froggy | Ability (temp · TBD Ben) |
|--------|---------------------------|
| James (Wheel) | **DASH** (juice + truck boost; space jet / mech) |
| Jimmy (Shield) | **SHIELD** |
| Bubbles (Zap) | **ZAP** |
| Rexy (Bot) | **BOT** (nudge toward SPS / Optimus) |

## Files

| File | Role |
|------|------|
| `index.html` | Lobby + hub HUD + phone/SPS/Optimus panels |
| `world.js` | 2.5D ranch map, parallax, track jumps, pond FX, **Starship pad**, render |
| `story.js` | Phone (Purple/Blue Bear) + SPS solar map + Optimus kits + Jimmy loop |
| `space.js` | Space episode scenes (Starship → moon chase → station → solar → Mars cave → mech) |
| `main.js` | Lobby hooks, hub + space loop, drive/pond juice, party sync |
| `party.js` | PeerJS Host/Join (hub steerX/Y + strip compat) |
| `ai.js` | Strip-era lane AI (kept; hub wander is in `world.js`) |
| `strip.js` | Legacy 2D ranch-run sandbox (not default entry) |
| `assets/` | Imagine splash / backdrop |

## What's new (fflook1) — physics & look only

Ben ask: Final Fantasy–**like makeup** = **physics + presentation**, not story/party systems.
**No Square Enix engine, assets, music, names, or IP.** Original 2.5D canvas only.

- **Ranch hub camera:** stronger fixed-angle isometric-ish projection, depth-scaled sprites, y-sorted layers, soft ground tiles, warm rim light + vignette.
- **Walk / drive physics:** acceleration + friction (not instant velocity), walk bob, smoother truck jump arcs (parabolic hang near apex).
- **Parallax / lighting:** multi-layer hills + clouds; space parallax starfield + nebula wash + vignette.
- **Camera follow:** critically-damped-ish town-hub settle in `main.js`.
- Narrative ranch + space episode content **unchanged** (no party HP menus, encounters, quest saves, or cast rewrites).

Prior space1 path still there: Starship → Jimmy chase → station → solar → Mars cave → mech.

## Still stubbed / next

- Neptune moon landings as unique playable stops (picker lists all 14)
- Party sync for space positions (hub sync remains; space is local path for now)
- Strip "Ranch Run" as optional track activity wire-up
- Richer Imagine bitmap sprites (procedural OK for now)
- Role↔froggy mapping confirmation (Ben)
- Confirm King Germy ↔ Germy relation (Ben)

## Ben decisions needed

- Confirm temp roles (James Wheel / Jimmy Shield / Bubbles Zap / Rexy Bot)
- Ranch map connectivity + photo refs
- Which Optimus kits feel best first for kids (all five wired; priority TBD)
- King Germy same dog as Germy or title only?
- Which Neptune moons get first playable landings

## Publish

Game Creator → **Webmaster** only. Product path: `games/four-froggies/`. Do not republish SPS as arcade. Cache-bust: `?v=20260925-fflook1`.
