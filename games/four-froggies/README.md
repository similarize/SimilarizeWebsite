# Four Froggies — temporary 2D strip / party lobby sandbox

**Product:** **Four Froggies** (one shared world). Publish path: `games/four-froggies/`.  
**This folder (`game/`):** temporary **2D ranch strip / party lobby sandbox** — useful for party + AI tests. **Not** a second product. Flagship is the **2.5D ranch hub** (Grok Imagine look).

Phone-first ranch strip: up to **4 human players** on their own phones/computers in the **same world**. Empty seats fill with **AI buddies** when you press GO (never replaces a human who could join). Solo still works fully offline.

## How to open locally

```bash
cd /workspace/froggies-cybertruck/game
python3 -m http.server 8765
```

Then open **http://localhost:8765/** (use the same Wi‑Fi / reachable URL on phones).

`file://` works for **solo**, but **Host / Join** needs a real `http(s)://` origin (PeerJS + clipboard/QR).

## How to play

### Solo (default)

1. Claim a froggy seat (or skip — GO defaults to **James**).
2. Tap **GO · Ranch Run** → you are human; the other **3 seats become AI**.
3. Steer left/right · tap ability · keep shared lives until the finish.

### Party (Host + Join)

1. **Host:** tap **Host room** → short room code appears + **Copy invite link** + QR.
2. Share the link (`?room=CODE`) or code with friends.
3. **Join:** open the invite on another phone/computer → auto-joins that lobby → claim an **Open** froggy (can’t steal a human seat).
4. Lobby shows **Open · AI** / **You** / **Joined** on the four seats.
5. Only the **Host** taps **GO**. Unfilled seats become AI. Never a 5th froggy.
6. Shared run: **host is authoritative**; guests send steer/ability; host simulates and streams compact state (~12 Hz).

Invite CTA (on splash): *“Invite friends — open seats stay open until someone joins (AI fills the rest when you GO).”*

| Froggy | Ability |
|--------|---------|
| James (Wheel) | **DASH** — lane swap + short invuln |
| Jimmy (Shield) | **SHIELD** — convoy bubble (blocks one hit) |
| Bubbles (Zap) | **ZAP** — clear nearest invader cluster |
| Rexy (Bot) | **BOT** — brief Optimus assist clears path |

## Networking (v1)

**Picked: PeerJS cloud (free)** via `cdn.jsdelivr.net/npm/peerjs` + public broker `0.peerjs.com`.

- No Cloudflare Worker required for this pass.
- Host Peer id = `froggies-{CODE}`; guests `connect()` to that id.
- Logic lives in `party.js` (lobby + input relay + state broadcast). `main.js` only hooks lobby UI, `startRun(seatMap)`, and remote inputs.
- **Day-one fallback:** if PeerJS fails or you’re offline, solo + AI still works — just tap GO.

Future option (design canon): thin Cloudflare Worker WebSocket relay — swap inside `party.js` without redesigning gameplay.

## Files

- `index.html` — entry + splash lobby
- `style.css` — phone HUD + seat / party chrome
- `main.js` — canvas loop, ranch strip, hazards/invaders, lobby hooks
- `party.js` — Host/Join, seat map, PeerJS relay
- `ai.js` — local companion heuristics (unfilled seats only)
- `assets/` — Imagine art (2 PNGs)

## Art sources (Grok Imagine)

| File | Use |
|------|-----|
| `assets/splash-forest-roof.png` | Title splash |
| `assets/backdrop-overhead.png` | Parallax / billboards |

## How to test with 2 browsers

1. Serve over http (`python3 -m http.server 8765`).
2. Browser A: open `/` → **Host room** → **Copy invite link**.
3. Browser B: paste invite (or `?room=CODE`) → claim a different froggy.
4. Confirm seats: A = **You**, B = **Joined**, others **Open · AI**.
5. Only A can **GO**. Both should see the same ranch run; B steers their froggy.
6. Optional: close B before GO → seat returns to Open → AI fills on GO.

## Similarize publish handoff

**Product:** Four Froggies → `games/four-froggies/` (only live arcade entry).  
**Publish loop:** Game Creator builds → **Webmaster** surfaces on similarize.com.  
See `../SIMILARIZE_LINK_STUB.md`: Webmaster renames cab/path; **DELETE / unpublish `froggies-sps` entirely** (take down https://www.similarize.com/games/froggies-sps/ completely).

Cache-bust: `?v=20260925-four1` on css/js in `index.html`.

## Deferred (not in this build)

- Cloudflare Worker relay (PeerJS is the v1 stand-in)
- Starship / Space / Mars / Raid stages
- Full robot roster + more Imagine stills
- Tilt, haptics, BGM, TV host layout
- Folding Jimmy-rogue / SPS story path into the **2.5D** world (see `../game-sps/` as reference HUD only)
