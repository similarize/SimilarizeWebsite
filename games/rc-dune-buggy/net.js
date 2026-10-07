/**
 * RC Dune Buggy — Host/Join net (separate from local 4-pad).
 * Worker: room create + WebRTC signaling + roster + WS relay fallback.
 * Host runs physics; guests send input; host broadcasts state.
 *
 * After Worker deploy, set ROOM_* bases below. Until then Host/Join shows
 * "Worker not deployed" and local/AI play still works.
 */
export const ROOM_HTTP_BASE = "https://similarize-dune-rooms.ben-e22.workers.dev";
export const ROOM_WS_BASE = "wss://similarize-dune-rooms.ben-e22.workers.dev/ws";
/** Join path on similarize.com (QR / share). */
export const JOIN_PATH = "/games/rc-dune-buggy/";

const ICE = { iceServers: [{ urls: "stun:stun.l.google.com:19302" }] }; // free STUN only

export function createNet(hooks) {
  const {
    onStatus,
    onRoster,
    onRemoteInput, // host: (seat, {steer,throttle})
    onState,       // guest: (msg)
    onReset,
    onBanner,
    onPeerLeft,
  } = hooks;

  let ws = null;
  let role = null; // "host" | "guest" | null
  let seat = -1;
  let code = "";
  let leaveIntent = false;
  let roster = [];
  let useRelay = true; // start with WS relay; WebRTC upgrades when available
  /** @type {Map<number, RTCPeerConnection>} */
  const pcs = new Map();
  /** @type {Map<number, RTCDataChannel>} */
  const dcs = new Map();

  function status(msg, kind) { if (onStatus) onStatus(msg, kind); }

  function sendWs(obj) {
    if (ws && ws.readyState === 1) ws.send(JSON.stringify(obj));
  }

  function sendInput(inp) {
    const msg = { type: "input", seat, steer: inp.steer, throttle: inp.throttle };
    // Prefer DC to host if up
    const dc = dcs.get(0);
    if (dc && dc.readyState === "open") {
      try { dc.send(JSON.stringify(msg)); return; } catch (_) {}
    }
    sendWs(msg);
  }

  function sendState(msg) {
    const packed = { type: "state", ...msg };
    let viaDc = 0;
    for (const [s, dc] of dcs) {
      if (s === 0) continue;
      if (dc.readyState === "open") {
        try { dc.send(JSON.stringify(packed)); viaDc++; } catch (_) {}
      }
    }
    // Always also WS-broadcast so guests without DC still sync
    sendWs(packed);
  }

  function sendReset() { sendWs({ type: "reset" }); }
  function sendBanner(text) { sendWs({ type: "banner", text }); }

  function closePc(s) {
    const dc = dcs.get(s); if (dc) try { dc.close(); } catch (_) {}
    dcs.delete(s);
    const pc = pcs.get(s); if (pc) try { pc.close(); } catch (_) {}
    pcs.delete(s);
  }

  function handleDcMessage(ev) {
    let msg; try { msg = JSON.parse(ev.data); } catch { return; }
    if (msg.type === "input" && role === "host" && onRemoteInput) {
      onRemoteInput(msg.seat | 0, { steer: +msg.steer || 0, throttle: +msg.throttle || 0 });
    } else if (msg.type === "state" && role === "guest" && onState) {
      onState(msg);
    }
  }

  async function ensurePc(remoteSeat, asOfferer) {
    if (pcs.has(remoteSeat)) return pcs.get(remoteSeat);
    const pc = new RTCPeerConnection(ICE);
    pcs.set(remoteSeat, pc);
    pc.onicecandidate = (ev) => {
      if (ev.candidate) {
        sendWs({ type: "signal", to: remoteSeat, payload: { kind: "ice", candidate: ev.candidate } });
      }
    };
    pc.onconnectionstatechange = () => {
      if (pc.connectionState === "failed" || pc.connectionState === "closed") {
        closePc(remoteSeat);
        useRelay = true;
      }
    };
    if (asOfferer) {
      const dc = pc.createDataChannel("dune", { ordered: true });
      dcs.set(remoteSeat, dc);
      dc.onmessage = handleDcMessage;
      dc.onopen = () => { useRelay = false; status(`P2P · seat ${remoteSeat}`, "ok"); };
      const offer = await pc.createOffer();
      await pc.setLocalDescription(offer);
      sendWs({ type: "signal", to: remoteSeat, payload: { kind: "offer", sdp: pc.localDescription } });
    } else {
      pc.ondatachannel = (ev) => {
        dcs.set(remoteSeat, ev.channel);
        ev.channel.onmessage = handleDcMessage;
        ev.channel.onopen = () => { useRelay = false; status(`P2P · seat ${remoteSeat}`, "ok"); };
      };
    }
    return pc;
  }

  async function onSignal(from, payload) {
    if (!payload || from === seat) return;
    try {
      if (payload.kind === "offer") {
        const pc = await ensurePc(from, false);
        await pc.setRemoteDescription(payload.sdp);
        const answer = await pc.createAnswer();
        await pc.setLocalDescription(answer);
        sendWs({ type: "signal", to: from, payload: { kind: "answer", sdp: pc.localDescription } });
      } else if (payload.kind === "answer") {
        const pc = pcs.get(from) || (await ensurePc(from, true));
        await pc.setRemoteDescription(payload.sdp);
      } else if (payload.kind === "ice") {
        const pc = pcs.get(from);
        if (pc && payload.candidate) await pc.addIceCandidate(payload.candidate);
      }
    } catch (err) {
      console.warn("signal", err);
      useRelay = true;
    }
  }

  async function tryHostOffers(list) {
    if (role !== "host" || typeof RTCPeerConnection === "undefined") return;
    for (const r of list) {
      if (r.seat === 0) continue;
      try { await ensurePc(r.seat, true); } catch (_) { useRelay = true; }
    }
  }

  function disconnect(reason) {
    leaveIntent = true;
    const w = ws; ws = null;
    if (w) try { w.close(); } catch (_) {}
    for (const s of [...pcs.keys()]) closePc(s);
    role = null; seat = -1; code = ""; roster = [];
    if (onRoster) onRoster([]);
    if (reason) status(reason, "err");
    else status("");
    leaveIntent = false;
  }

  function connect(roomCode) {
    roomCode = String(roomCode || "").toUpperCase().replace(/[^A-Z0-9]/g, "").slice(0, 4);
    if (roomCode.length !== 4) {
      status("Enter a 4-character code", "err");
      return;
    }
    leaveIntent = false;
    if (ws) disconnect();
    leaveIntent = false;
    status("Connecting…");
    let sock;
    try {
      sock = new WebSocket(ROOM_WS_BASE + "?room=" + encodeURIComponent(roomCode));
    } catch (e) {
      status("Worker unreachable — deploy worker/ first", "err");
      return;
    }
    ws = sock;
    sock.onopen = () => { code = roomCode; };
    sock.onmessage = (ev) => {
      let msg; try { msg = JSON.parse(ev.data); } catch { return; }
      if (msg.type === "welcome") {
        seat = msg.seat | 0;
        role = seat === 0 ? "host" : "guest";
        code = msg.code || roomCode;
        roster = msg.roster || [];
        if (onRoster) onRoster(roster);
        const label = role === "host" ? "Host" : "Seat " + (seat + 1);
        status(label + " · " + code + (msg.ready ? " · ready" : " · waiting…"), "ok");
        if (role === "host") tryHostOffers(roster);
      } else if (msg.type === "roster") {
        roster = msg.roster || [];
        if (onRoster) onRoster(roster);
        status((role === "host" ? "Host" : "Seat " + (seat + 1)) + " · " + code + " · " + roster.length + "/4", "ok");
        if (role === "host") tryHostOffers(roster);
      } else if (msg.type === "peer_left") {
        closePc(msg.seat | 0);
        roster = msg.roster || roster.filter((r) => r.seat !== (msg.seat | 0));
        if (onRoster) onRoster(roster);
        if (onPeerLeft) onPeerLeft(msg.seat | 0);
        status("Seat " + ((msg.seat | 0) + 1) + " left", "err");
      } else if (msg.type === "signal") {
        onSignal(msg.from | 0, msg.payload);
      } else if (msg.type === "input" && role === "host" && onRemoteInput) {
        onRemoteInput(msg.seat | 0, { steer: +msg.steer || 0, throttle: +msg.throttle || 0 });
      } else if (msg.type === "state" && role === "guest" && onState) {
        onState(msg);
      } else if (msg.type === "reset" && role === "guest" && onReset) {
        onReset();
      } else if (msg.type === "banner" && role === "guest" && onBanner) {
        onBanner(msg.text || "");
      }
    };
    sock.onclose = () => {
      if (leaveIntent) return;
      if (ws === sock) disconnect("Disconnected");
    };
    sock.onerror = () => status("Connection failed — is Worker deployed?", "err");
  }

  async function createRoom() {
    try {
      status("Creating…");
      const res = await fetch(ROOM_HTTP_BASE + "/create", { method: "POST" });
      if (!res.ok) throw new Error("bad status");
      const data = await res.json();
      if (!data.code) throw new Error("no code");
      connect(data.code);
      return data.code;
    } catch (e) {
      status("Could not create room — deploy worker/ (see README)", "err");
      return null;
    }
  }

  function joinUrl(c) {
    const origin = typeof location !== "undefined" ? location.origin : "https://www.similarize.com";
    return origin + JOIN_PATH + "?v=20261007-dune2&join=" + encodeURIComponent(c || code);
  }

  return {
    createRoom,
    connect,
    disconnect,
    sendInput,
    sendState,
    sendReset,
    sendBanner,
    joinUrl,
    get role() { return role; },
    get seat() { return seat; },
    get code() { return code; },
    get roster() { return roster; },
    get connected() { return !!(ws && ws.readyState === 1); },
    get usingRelay() { return useRelay; },
  };
}

/** QR as data-URL via free esm CDN (no paid API). Falls back to hiding img. */
export async function fillQr(imgEl, url) {
  if (!imgEl) return;
  imgEl.alt = url;
  try {
    const mod = await import("https://esm.sh/qrcode@1.5.4");
    const QR = mod.default || mod;
    imgEl.src = await QR.toDataURL(url, { width: 140, margin: 1, color: { dark: "#1a120c", light: "#ffffff" } });
  } catch (_) {
    imgEl.removeAttribute("src");
  }
}
