# similarize-dune-rooms

Cloudflare Worker + Durable Object for **RC Dune Buggy** host-and-join (up to 4).

**Do not deploy without Ben / Webmaster approval.** Free `workers.dev` tier only — no paid add-ons.

## Protocol
- `POST /create` → `{ code }` (4-char)
- `WS /ws?room=CODE` — seats `0..3` (0 = host)
- WebRTC signaling: `{ type:"signal", to?, payload }` relayed by Worker
- Relay fallback (no TURN): guests `{ type:"input" }` → host; host `{ type:"state" }` → all

## Deploy (after approval)
```bash
cd games/rc-dune-buggy/worker
npm install
npx wrangler deploy
```
Then set client bases in `../net.js`:
- `ROOM_HTTP_BASE` / `ROOM_WS_BASE` → your `*.workers.dev` URL

## Limits (free tier / party play)
- ~4 players per room; idle rooms hibernate (DO)
- Simultaneous rooms: subject to Workers free-tier request/DO limits — fine for arcade demos, not a mass lobby
- Cellular / symmetric NAT: WebRTC may fail without TURN → client falls back to WS relay through this Worker
- No paid TURN / Meta / Unity APIs
