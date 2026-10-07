# RC Dune Buggy (working title)

Browser-playable **Three.js / WebGL** arcade prototype for similarize.com.
Overhead RC spectator camera, **elevated** dunes (hills/drops/ramps/banked turns),
four Froggie seats (**James, Jimmy, Bubbles, Rexy** only) with overhead name labels,
up to 4 local players
(pads via `/games/shared/gamepad.js`), empty seats = AI, sand vs hardpack feel,
suspension bounce approx, dust on sand. Light **WebXR** entry for Quest browser.

**Host/Join** (Android party style) is scaffolded separately from local 4-pad:
room code + QR, Cloudflare Worker signaling + optional WS relay, WebRTC datachannels
when ICE works. **Worker not deployed yet** — needs Ben/Webmaster approval.

Play: `/games/rc-dune-buggy/?v=20261007-dune2`

## Why Three.js this pass
Unity 6 Editor install is a separate track (not blocking this prototype).
`grok plugin install Unity-Technologies/unity-agent-plugin` needs Grok Build CLI
(unavailable here earlier). Static CDN prototype ships the gameplay brief. No paid APIs.

## Controls (local)
- **P1 keyboard:** arrows or WASD (W/Up throttle, S/Down reverse, A/D steer)
- **P1 phone:** on-screen stick X = steer + FWD/REV buttons (no stick Y drive)
- **Gamepads (Xbox):** one pad = one buggy; left stick X = steer; RT = forward, LT = reverse (no stick Y throttle); empty seats = AI
- **Reset** — 3-lap heat
- **WebXR** — Quest browser when `navigator.xr` present

## Host / Join (network)
- **Host** → creates room via Worker `POST /create`, shows 4-char code + QR + copy link
- **Join** → enter code (or `?join=CODE` deep link / QR)
- Up to **4** network seats; host runs physics; guests send input; host broadcasts state
- Transport: WebRTC datachannels (STUN only) when possible; **WS relay fallback** through Worker (no paid TURN)
- Local pads on the host machine still work alongside network guests
- Separate from local-only 4-pad mode (no room = classic local+AI)

### Worker deploy (approval required — do not spend paid)
```bash
cd games/rc-dune-buggy/worker
npm install
npx wrangler deploy
```
Then confirm `ROOM_HTTP_BASE` / `ROOM_WS_BASE` in `net.js` match the `*.workers.dev` URL.

Scaffold lives in `worker/` (Durable Object `DuneRoom`, free `workers.dev`).

### Limits
- ~4 players / room; demo-scale simultaneous rooms (Workers free-tier DO/request caps)
- Cellular / symmetric NAT: WebRTC may fail → automatic WS relay (higher latency, still playable)
- No paid TURN, Meta, or Unity online services

## Files
| Path | Role |
|------|------|
| `index.html` | Shell, HUD, touch, Host/Join UI |
| `style.css` | Desert HUD |
| `game.js` | Terrain, buggies, AI, dust, WebXR, net hooks |
| `net.js` | Host/Join client (WebRTC + WS) |
| `worker/` | CF Worker + DO (signaling + relay) — undeployed |
| `README.md` | This note |

## Later: Unity 6 + URP + native Quest
When Unity 6 Editor is on a machine:
1. URP 3D project, Input System, WebGL module
2. Port track / buggy / seat rules
3. WebGL build for similarize.com (beside this prototype)
4. Quest: OpenXR + Meta XR SDK / XRI; Store needs Meta developer account — no paid Meta APIs for basic sideload
5. Keep Three.js as lightweight arcade-floor fallback

## Out of scope
Unity project files, paid assets, Four Froggies, Soccer RC changes, deploying Worker without approval.
