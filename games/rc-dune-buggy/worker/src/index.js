/**
 * RC Dune Buggy rooms — Cloudflare Worker + Durable Object.
 * Up to 4 seats. Signaling for WebRTC + optional WS relay (host authority).
 * Free-tier friendly. No paid TURN.
 *
 * POST /create → { code }
 * WS /ws?room=CODE
 *   welcome: { type, seat:0..3, code, peers, roster, ready }
 *   roster:  { type, roster:[{seat,id}], peers, ready }
 *   signal:  { type:"signal", from, to, payload }  // SDP/ICE — relayed
 *   input:   guest→host { type:"input", seat, steer, throttle }
 *   state:   host→all   { type:"state", ... }
 *   chat/ping passthrough as needed
 */
const DEFAULT_ORIGINS = [
  "https://www.similarize.com",
  "https://similarize.com",
  "http://localhost:3000",
  "http://localhost:8000",
  "http://127.0.0.1:8000",
  "http://127.0.0.1:3000",
];
const MAX_SEATS = 4;

function allowedOrigins(env) {
  const raw = (env.ALLOWED_ORIGINS || "").split(",").map((s) => s.trim()).filter(Boolean);
  return raw.length ? raw : DEFAULT_ORIGINS;
}
function corsHeaders(origin, env) {
  const allowed = allowedOrigins(env);
  const ok = origin && allowed.includes(origin);
  return {
    "Access-Control-Allow-Origin": ok ? origin : allowed[0],
    "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
    "Access-Control-Allow-Headers": "Content-Type",
    "Access-Control-Max-Age": "86400",
    Vary: "Origin",
  };
}
function normalizeCode(code) {
  return String(code || "").toUpperCase().replace(/[^A-Z0-9]/g, "").slice(0, 4);
}
function newCode() {
  const alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
  let out = "";
  const bytes = crypto.getRandomValues(new Uint8Array(4));
  for (const b of bytes) out += alphabet[b % alphabet.length];
  return out;
}

export class DuneRoom {
  constructor(ctx, env) {
    this.ctx = ctx;
    this.env = env;
    this.ctx.setWebSocketAutoResponse(new WebSocketRequestResponsePair("ping", "pong"));
  }

  allSocks() {
    return this.ctx.getWebSockets();
  }

  roster() {
    const out = [];
    for (const ws of this.allSocks()) {
      const m = ws.deserializeAttachment() || {};
      if (typeof m.seat === "number") out.push({ seat: m.seat, id: m.id || ("p" + m.seat) });
    }
    out.sort((a, b) => a.seat - b.seat);
    return out;
  }

  seatTaken(seat) {
    return this.allSocks().some((ws) => (ws.deserializeAttachment() || {}).seat === seat);
  }

  nextSeat() {
    for (let i = 0; i < MAX_SEATS; i++) if (!this.seatTaken(i)) return i;
    return -1;
  }

  send(ws, obj) {
    try { ws.send(JSON.stringify(obj)); } catch (_) {}
  }

  broadcast(obj, except) {
    for (const ws of this.allSocks()) {
      if (ws === except) continue;
      this.send(ws, obj);
    }
  }

  findSeat(seat) {
    for (const ws of this.allSocks()) {
      if ((ws.deserializeAttachment() || {}).seat === seat) return ws;
    }
    return null;
  }

  async fetch(request) {
    if (request.headers.get("Upgrade") !== "websocket") {
      return new Response("Expected WebSocket", { status: 426 });
    }
    const url = new URL(request.url);
    const code = normalizeCode(url.searchParams.get("room") || url.searchParams.get("code"));
    if (code.length !== 4) return new Response("Bad room code", { status: 400 });

    const seat = this.nextSeat();
    if (seat < 0) return new Response("Room full", { status: 409 });

    const pair = new WebSocketPair();
    const [client, server] = Object.values(pair);
    const id = "p" + seat + "-" + Math.random().toString(36).slice(2, 7);
    this.ctx.acceptWebSocket(server, ["seat" + seat]);
    server.serializeAttachment({ seat, code, id, joinedAt: Date.now() });

    const roster = this.roster();
    this.send(server, {
      type: "welcome",
      seat,
      code,
      id,
      peers: roster.length,
      roster,
      ready: roster.length >= 2,
      max: MAX_SEATS,
    });
    this.broadcast({ type: "roster", roster, peers: roster.length, ready: roster.length >= 2 }, server);

    return new Response(null, { status: 101, webSocket: client });
  }

  async webSocketMessage(ws, message) {
    let data;
    try {
      data = typeof message === "string" ? JSON.parse(message) : JSON.parse(new TextDecoder().decode(message));
    } catch { return; }
    const meta = ws.deserializeAttachment() || {};
    const seat = meta.seat;

    // WebRTC signaling — route to target seat or broadcast
    if (data.type === "signal") {
      const payload = { type: "signal", from: seat, to: data.to, payload: data.payload };
      if (typeof data.to === "number") {
        const target = this.findSeat(data.to);
        if (target) this.send(target, payload);
      } else {
        this.broadcast(payload, ws);
      }
      return;
    }

    // Host-authority relay fallback (no TURN needed)
    if (data.type === "input" && seat !== 0) {
      const host = this.findSeat(0);
      if (host) this.send(host, { ...data, seat });
      return;
    }
    if (data.type === "state" && seat === 0) {
      this.broadcast(data, ws);
      return;
    }
    if (data.type === "reset" && seat === 0) {
      this.broadcast(data, ws);
      return;
    }
    if (data.type === "banner" && seat === 0) {
      this.broadcast(data, ws);
      return;
    }
    if (data.type === "ping") {
      this.send(ws, { type: "pong", t: data.t });
    }
  }

  async webSocketClose(ws) {
    const meta = ws.deserializeAttachment() || {};
    try { ws.close(1000, "bye"); } catch (_) {}
    const roster = this.roster().filter((r) => r.seat !== meta.seat);
    this.broadcast({ type: "peer_left", seat: meta.seat, roster, peers: roster.length, ready: roster.length >= 2 });
  }

  async webSocketError(ws) {
    try { ws.close(1011, "error"); } catch (_) {}
  }
}

export default {
  async fetch(request, env) {
    const origin = request.headers.get("Origin") || "";
    const headers = corsHeaders(origin, env);
    const url = new URL(request.url);

    if (request.method === "OPTIONS") return new Response(null, { status: 204, headers });

    if (url.pathname === "/" || url.pathname === "/health") {
      return Response.json(
        { ok: true, service: "similarize-dune-rooms", maxSeats: MAX_SEATS },
        { headers: { ...headers, "Content-Type": "application/json" } },
      );
    }

    if (url.pathname === "/create" && request.method === "POST") {
      return Response.json({ code: newCode() }, { headers: { ...headers, "Content-Type": "application/json" } });
    }

    if (url.pathname === "/ws") {
      const code = normalizeCode(url.searchParams.get("room") || url.searchParams.get("code"));
      if (code.length !== 4) return new Response("Bad room code", { status: 400, headers });
      if (request.headers.get("Upgrade") !== "websocket") {
        return new Response("Expected WebSocket", { status: 426, headers });
      }
      const id = env.ROOM.idFromName(code);
      return env.ROOM.get(id).fetch(request);
    }

    return new Response("Not found", { status: 404, headers });
  },
};
