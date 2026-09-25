# Froggies Cybertruck Odyssey — Ranch Grounds MVP

Phone-first solo prototype: you drive one froggy; three AI companions steer and use abilities. Stage 1 only — James’s ranch strip (house → monster truck track → pond + fishies).

## How to open locally

From this folder:

```bash
cd /workspace/froggies-cybertruck/game
python3 -m http.server 8765
```

Then open **http://localhost:8765/** on your phone or desktop browser.

You can also open `index.html` directly via `file://` in most browsers (no modules; plain script tags).

## How to play

1. Pick a froggy (or tap **GO** for default **Moss**).
2. **Steer:** left/right buttons, swipe on the canvas, or ←/→ / A/D.
3. **Ability:** big button (or Space / Enter).
4. Keep shared lives above 0 until the finish line. Wipe → tap for **instant retry**.

| Froggy | Ability |
|--------|---------|
| Moss (Wheel) | **DASH** — lane swap + short invuln |
| Ember (Shield) | **SHIELD** — convoy bubble (blocks one hit) |
| Bolt (Zap) | **ZAP** — clear nearest invader cluster |
| Gear (Bot) | **BOT** — brief Optimus assist clears path |

## Files

- `index.html` — entry
- `style.css` — phone HUD + touch pads
- `main.js` — canvas loop, ranch strip, hazards/invaders
- `ai.js` — local companion heuristics (no network)
- `assets/` — Imagine art used in-game (2 PNGs only)

## Art sources (Grok Imagine)

Copied from `../imagine/` (saved Cybertruck + froggies History). Only **two** loads for phones:

| File | Source | Use |
|------|--------|-----|
| `assets/splash-forest-roof.png` | `froggy-cybertruck-02-forest-roof.png` | Title splash + start-screen backdrop |
| `assets/backdrop-overhead.png` | `froggy-cybertruck-11-overhead-forest.png` | Parallax sky strip + trail billboards |

Other numbered Imagine stills stay in `../imagine/` for later stages.

## Similarize handoff (later)

Do **not** copy into `SimilarizeWebsite` until Ben says go. Stub note:

- Target path when ready: `SimilarizeWebsite/games/froggies-cybertruck/` (or a single `froggies.html` cab card).
- See sibling file `../SIMILARIZE_LINK_STUB.md`.

## Deferred (not in this build)

- 4P Host QR + WebSocket relay
- Starship / Space / Mars / Raid stages
- Full robot roster + more Imagine stills as convoy sprites
- Tilt, haptics, BGM, TV host layout
