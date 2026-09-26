# Froggies · SPS Track (first playable)

Working titles: **Froggies · SPS Track** / **Jimmy’s Gone Rogue**  
Phone-first vanilla HTML/JS/CSS prototype from Ben’s **2026-09-25** world-bible call.  
Does **not** replace the existing 2D ranch strip under `../game/` — this is a new screen-flow slice.

## How to run

```bash
cd /workspace/froggies-cybertruck/game-sps
python3 -m http.server 8766
```

Open **http://localhost:8766/** on a phone or desktop browser.  
No bundler, no modules, no paid APIs. `file://` also works in most browsers.

Syntax check:

```bash
node --check main.js
```

## One-minute loop

1. **Title** → Play as **James** at the futuristic ranch hub.  
2. **Alert:** Jimmy went rogue into the solar system.  
3. **Call** → dial **Purple Bear** → people-talk beat: *“Hey James — he’s near [planet]. Check SPS.”*  
   - Optional peek: **Blue Bear** contact → soft *“…meow.”* (no invented lore).  
4. Open **SPS** (Solar Positioning System) — schematic solar map (Mercury→Neptune). Jimmy’s blip moves; you track him.  
5. Call **Optimus** abilities (playable): **Hover**, **Rocket/Afterburner**, **Drone scout**, **Map ping**. Short cooldowns; seeker moves / confidence rises. Badge: *usable in ranch · space · planets*.  
6. **Win:** seeker reaches Jimmy’s blip or tap **Bring Jimmy home** at high SPS confidence.  
7. **Soft fail:** timer (60s) → “Jimmy’s on [planet] again…” → **Retry**.

Clear goal under a minute: **find Jimmy**.

## UX

- Touch-first big buttons: Call, SPS, Optimus abilities, Retry / Bring Jimmy home.  
- Futuristic ranch UI (new screen flow) — not the old 2D strip as flagship.  
- Other robots (Unitree / Atlas / Figure) are **stubs** in the Optimus screen only.

## Canon locked in this build

| Keep | Notes |
|------|--------|
| Froggies | James, Jimmy, Bubbles, Rexy only |
| Bears | Purple Bear (phone answer); Blue Bear (peek / meow only) |
| Robot focus | Optimus advanced: hover, rocket/afterburners, drone, map |
| Planets | Full solar set Mercury→Neptune |
| Forbidden | Zip, Tinker, Moss, Ember, Bolt, Gear, Sunny — never |

## Files

| File | Role |
|------|------|
| `index.html` | Screens: title, hub, phone, SPS, Optimus, win/lose |
| `style.css` | Phone-first futuristic HUD |
| `main.js` | Loop, SPS canvas, abilities, timer |
| `ASSETS_BRIEF.md` | Brief for Grok Imaginer (no generated art required for play) |
| `README.md` | This file |

## Similarize publish handoff

**Publish loop:** Game Creator builds → hands to **Webmaster** → Webmaster surfaces on similarize.com. **Do not** route *routine* publishes through Similarize SME (company SME only; engage SME only for company-facing exceptions).

**Do not publish until Ben says go.** When ready:

- Game Creator prepares `game-sps/` for `SimilarizeWebsite/games/froggies-sps/` (or the live `games/` path).  
- Hand directly to **Webmaster** to copy + add a cab card on the arcade index → `/games/froggies-sps/`.  
- Keep the existing `froggies-cybertruck` 2D strip cab until Ben chooses replace vs. dual listing.  
- See `../SIMILARIZE_LINK_STUB.md`.

## Deferred (not in this MVP)

- Second call flavors (Bubbles / Rexy) beyond stubs  
- Real 3D ranch hub / Three.js world  
- Full robot roster playable kits  
- Portal / travel toy names (TBD with Ben)  
- 4P / shared-world networking  
- Grok Imagine art wiring (placeholders are CSS/emoji only)  
- Blue / Purple Bear looks, home, abilities beyond phone personality split  

## What’s next (suggested)

1. Wire Imagine stills per `ASSETS_BRIEF.md`.  
2. Ben pass on Optimus kit feel + Purple Bear line variants.  
3. Hand to **Webmaster** to surface on similarize.com when Ben approves.  
4. Fold SPS as a ranch-hub activity into the future 3D world.
