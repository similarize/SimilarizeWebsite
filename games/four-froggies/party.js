/* Froggies party lobby + host-authoritative PeerJS relay (free 0.peerjs.com).
   Solo never needs this — day-one fallback if net fails. */
(function (global) {
  "use strict";

  var FROG_ORDER = ["james", "jimmy", "bubbles", "rexy"];
  var CODE_CHARS = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
  var PEER_PREFIX = "froggies-";

  function emptySeats() {
    var s = {};
    for (var i = 0; i < FROG_ORDER.length; i++) {
      s[FROG_ORDER[i]] = { status: "open", peerId: null, label: null };
    }
    return s;
  }

  function makeCode(len) {
    var out = "";
    var n = len || 4;
    for (var i = 0; i < n; i++) {
      out += CODE_CHARS.charAt(Math.floor(Math.random() * CODE_CHARS.length));
    }
    return out;
  }

  function peerIdForRoom(code) {
    return PEER_PREFIX + String(code || "").toUpperCase();
  }

  function roomFromPeerId(pid) {
    if (!pid || pid.indexOf(PEER_PREFIX) !== 0) return null;
    return pid.slice(PEER_PREFIX.length).toUpperCase();
  }

  function inviteUrl(code) {
    var u = new URL(global.location.href);
    u.searchParams.set("room", String(code || "").toUpperCase());
    return u.toString();
  }

  function parseRoomFromUrl() {
    try {
      var u = new URL(global.location.href);
      var r = u.searchParams.get("room");
      if (!r) return null;
      r = String(r).toUpperCase().replace(/[^A-Z0-9]/g, "");
      return r.length >= 3 ? r : null;
    } catch (e) {
      return null;
    }
  }

  /**
   * @param {object} hooks
   * onLobby(seats, meta) — seats map + { role, room, status, error }
   * onStart(seatMap) — { id: { human, local, peerId } }
   * onInput(frogId, payload) — host only
   * onState(state) — guests
   * onEnd(payload) — wipe/clear from host
   * onPeerGone(peerId) — host
   */
  function createParty(hooks) {
    hooks = hooks || {};
    var role = "solo"; // solo | host | guest
    var room = null;
    var peer = null;
    var localId = null;
    var seats = emptySeats();
    var conns = {}; // peerId -> DataConnection (host)
    var hostConn = null; // guest's connection to host
    var status = "idle";
    var lastError = null;
    var destroyed = false;

    function emitLobby(extraStatus) {
      if (extraStatus) status = extraStatus;
      if (typeof hooks.onLobby === "function") {
        hooks.onLobby(cloneSeats(), {
          role: role,
          room: room,
          status: status,
          error: lastError,
          localId: localId,
          invite: room ? inviteUrl(room) : null,
        });
      }
    }

    function cloneSeats() {
      var out = {};
      for (var i = 0; i < FROG_ORDER.length; i++) {
        var id = FROG_ORDER[i];
        var s = seats[id];
        out[id] = { status: s.status, peerId: s.peerId, label: s.label };
      }
      return out;
    }

    function send(conn, msg) {
      if (!conn || !conn.open) return;
      try {
        conn.send(msg);
      } catch (e) {
        /* ignore */
      }
    }

    function broadcast(msg, exceptPeerId) {
      var ids = Object.keys(conns);
      for (var i = 0; i < ids.length; i++) {
        if (exceptPeerId && ids[i] === exceptPeerId) continue;
        send(conns[ids[i]], msg);
      }
    }

    function lobbyPayload() {
      return { t: "lobby", seats: cloneSeats(), room: room, hostId: localId };
    }

    function seatClaimedBy(peerId) {
      for (var i = 0; i < FROG_ORDER.length; i++) {
        var id = FROG_ORDER[i];
        if (seats[id].peerId === peerId) return id;
      }
      return null;
    }

    function clearPeerSeat(peerId) {
      for (var i = 0; i < FROG_ORDER.length; i++) {
        var id = FROG_ORDER[i];
        if (seats[id].peerId === peerId) {
          seats[id] = { status: "open", peerId: null, label: null };
        }
      }
    }

    function isLocalPeerId(peerId) {
      if (!peerId) return false;
      if (peerId === localId) return true;
      return String(peerId).indexOf("local-pad-") === 0;
    }

    function padIndexFromPeer(peerId) {
      if (!peerId) return null;
      var m = String(peerId).match(/^local-pad-(\d+)$/);
      return m ? (parseInt(m[1], 10)) : null;
    }

    function applyClaim(frogId, peerId, label, isLocal, padIndex) {
      if (FROG_ORDER.indexOf(frogId) < 0) return { ok: false, reason: "bad-frog" };
      var cur = seats[frogId];
      if (cur.status === "human" && cur.peerId && cur.peerId !== peerId && cur.status !== "you") {
        /* taken by another human (remote or other pad) */
      }
      if (cur.peerId && cur.peerId !== peerId && (cur.status === "human" || cur.status === "you")) {
        return { ok: false, reason: "taken" };
      }
      // Release previous seat for this peer
      clearPeerSeat(peerId);
      var pIdx = typeof padIndex === "number" ? padIndex : padIndexFromPeer(peerId);
      seats[frogId] = {
        status: isLocalPeerId(peerId) ? "you" : (isLocal ? "you" : "human"),
        peerId: peerId,
        label: label || (pIdx != null ? ("Pad " + (pIdx + 1)) : (isLocal ? "You" : "Joined")),
        padIndex: pIdx,
      };
      // Normalize: keyboard localId + local-pad-* are couch locals; remotes are human
      for (var i = 0; i < FROG_ORDER.length; i++) {
        var id = FROG_ORDER[i];
        var s = seats[id];
        if (!s.peerId) {
          seats[id] = { status: "open", peerId: null, label: null, padIndex: null };
        } else if (isLocalPeerId(s.peerId)) {
          seats[id].status = "you";
          var pi = s.padIndex != null ? s.padIndex : padIndexFromPeer(s.peerId);
          seats[id].padIndex = pi;
          seats[id].label = pi != null ? ("Pad " + (pi + 1)) : (s.peerId === localId ? "You" : (s.label || "You"));
        } else {
          seats[id].status = "human";
          seats[id].label = s.label || "Joined";
          seats[id].padIndex = null;
        }
      }
      return { ok: true };
    }

    function buildSeatMap() {
      var map = {};
      for (var i = 0; i < FROG_ORDER.length; i++) {
        var id = FROG_ORDER[i];
        var s = seats[id];
        var isHuman = !!(s.peerId && (s.status === "you" || s.status === "human"));
        var local = isHuman && isLocalPeerId(s.peerId);
        var pIdx = s.padIndex != null ? s.padIndex : padIndexFromPeer(s.peerId);
        map[id] = {
          human: isHuman,
          local: local,
          peerId: isHuman ? s.peerId : null,
          padIndex: local ? (pIdx != null ? pIdx : null) : null,
        };
      }
      return map;
    }

    /** Couch: pad N claims next open froggy (does not steal). Returns frogId or null. */
    function claimLocalPad(padIndex) {
      if (role === "guest") return null;
      var idx = padIndex | 0;
      if (idx < 0 || idx > 3) return null;
      if (!localId) localId = "local-" + makeCode(6);
      var peerId = "local-pad-" + idx;
      var existing = seatClaimedBy(peerId);
      if (existing) return existing;
      for (var i = 0; i < FROG_ORDER.length; i++) {
        var fid = FROG_ORDER[i];
        if (!seats[fid].peerId || seats[fid].status === "open") {
          var res = applyClaim(fid, peerId, "Pad " + (idx + 1), true, idx);
          if (res.ok) {
            if (role === "host") broadcast(lobbyPayload());
            emitLobby(role === "solo" ? "idle" : "ready");
            return fid;
          }
        }
      }
      return null;
    }

    function releaseLocalPad(padIndex) {
      var peerId = "local-pad-" + (padIndex | 0);
      if (!seatClaimedBy(peerId)) return false;
      clearPeerSeat(peerId);
      if (role === "host") broadcast(lobbyPayload());
      emitLobby(role === "solo" ? "idle" : "ready");
      return true;
    }

    function ensurePeerScript(cb) {
      if (typeof global.Peer === "function") {
        cb(null);
        return;
      }
      var existing = document.querySelector("script[data-froggies-peerjs]");
      if (existing) {
        existing.addEventListener("load", function () { cb(null); });
        existing.addEventListener("error", function () { cb(new Error("PeerJS load failed")); });
        return;
      }
      var s = document.createElement("script");
      s.src = "https://cdn.jsdelivr.net/npm/peerjs@1.5.4/dist/peerjs.min.js";
      s.async = true;
      s.dataset.froggiesPeerjs = "1";
      s.onload = function () { cb(null); };
      s.onerror = function () { cb(new Error("PeerJS load failed")); };
      document.head.appendChild(s);
    }

    function wireHostConn(conn) {
      var remoteId = conn.peer;
      conns[remoteId] = conn;
      conn.on("data", function (msg) {
        handleHostMessage(remoteId, msg);
      });
      conn.on("close", function () {
        delete conns[remoteId];
        clearPeerSeat(remoteId);
        broadcast(lobbyPayload());
        emitLobby("ready");
        if (typeof hooks.onPeerGone === "function") hooks.onPeerGone(remoteId);
      });
      conn.on("error", function () {
        /* ignore; close will fire */
      });
      // Push current lobby
      send(conn, lobbyPayload());
      emitLobby("ready");
    }

    function handleHostMessage(fromId, msg) {
      if (!msg || !msg.t) return;
      if (msg.t === "hello") {
        send(conns[fromId], lobbyPayload());
        return;
      }
      if (msg.t === "claim") {
        var res = applyClaim(msg.frogId, fromId, msg.label || "Joined", false);
        if (!res.ok) {
          send(conns[fromId], { t: "claimFail", reason: res.reason, frogId: msg.frogId });
        } else {
          broadcast(lobbyPayload());
          emitLobby("ready");
        }
        return;
      }
      if (msg.t === "input") {
        if (typeof hooks.onInput === "function") {
          hooks.onInput(msg.frogId, {
            steer: msg.steer || 0,
            steerX: typeof msg.steerX === "number" ? msg.steerX : 0,
            steerY: typeof msg.steerY === "number" ? msg.steerY : 0,
            ability: !!msg.ability,
            interact: !!msg.interact,
            targetLane: typeof msg.targetLane === "number" ? msg.targetLane : null,
            peerId: fromId,
          });
        }
        return;
      }
    }

    function handleGuestMessage(msg) {
      if (!msg || !msg.t) return;
      if (msg.t === "lobby") {
        seats = emptySeats();
        var remote = msg.seats || {};
        for (var i = 0; i < FROG_ORDER.length; i++) {
          var id = FROG_ORDER[i];
          var s = remote[id];
          if (!s) continue;
          if (s.peerId === localId) {
            seats[id] = { status: "you", peerId: localId, label: "You" };
          } else if (s.peerId) {
            seats[id] = { status: "human", peerId: s.peerId, label: s.label || "Joined" };
          } else {
            seats[id] = { status: "open", peerId: null, label: null };
          }
        }
        emitLobby("ready");
        return;
      }
      if (msg.t === "claimFail") {
        lastError = "Seat taken — pick another froggy";
        emitLobby("ready");
        lastError = null;
        return;
      }
      if (msg.t === "start") {
        if (typeof hooks.onStart === "function") {
          var map = msg.seatMap || {};
          // Mark local
          for (var j = 0; j < FROG_ORDER.length; j++) {
            var fid = FROG_ORDER[j];
            if (!map[fid]) continue;
            map[fid].local = !!(map[fid].human && map[fid].peerId === localId);
          }
          hooks.onStart(map);
        }
        return;
      }
      if (msg.t === "state" || msg.t === "hub") {
        if (typeof hooks.onState === "function") hooks.onState(msg);
        return;
      }
      if (msg.t === "end") {
        if (typeof hooks.onEnd === "function") hooks.onEnd(msg);
        return;
      }
      if (msg.t === "hostGone") {
        lastError = "Host left — scan / open invite again";
        role = "solo";
        status = "error";
        emitLobby();
      }
    }

    function hostRoom(preferredCode) {
      role = "host";
      lastError = null;
      status = "connecting";
      // Keep the local player's existing claim (if any); clear remote seats
      var kept = null;
      for (var ki = 0; ki < FROG_ORDER.length; ki++) {
        var kid = FROG_ORDER[ki];
        if (seats[kid].peerId === localId || seats[kid].status === "you") {
          kept = { id: kid, peerId: localId };
          break;
        }
      }
      seats = emptySeats();
      if (kept) {
        seats[kept.id] = { status: "you", peerId: kept.peerId, label: "You" };
      }
      conns = {};
      emitLobby();
      ensurePeerScript(function (err) {
        if (err) {
          lastError = "Network unavailable — solo still works";
          role = "solo";
          status = "error";
          emitLobby();
          return;
        }
        var code = (preferredCode || makeCode(4)).toUpperCase();
        room = code;
        try {
          if (peer) {
            try { peer.destroy(); } catch (e1) { /* */ }
          }
          peer = new global.Peer(peerIdForRoom(code), {
            debug: 0,
          });
        } catch (e) {
          lastError = "Could not create room — try solo";
          role = "solo";
          status = "error";
          emitLobby();
          return;
        }
        peer.on("open", function (id) {
          var prevId = localId;
          localId = id;
          room = roomFromPeerId(id) || code;
          // Keep any seat already claimed before PeerJS finished opening
          var hadClaim = false;
          for (var i = 0; i < FROG_ORDER.length; i++) {
            var fid = FROG_ORDER[i];
            if (seats[fid].peerId === prevId) {
              seats[fid].peerId = localId;
              seats[fid].status = "you";
              seats[fid].label = "You";
              hadClaim = true;
            }
          }
          if (!hadClaim) {
            // Fresh host lobby — leave seats open (don't wipe a migrated claim)
            // seats already emptySeats from hostRoom start unless claimed
          }
          status = "ready";
          emitLobby();
        });
        peer.on("connection", function (conn) {
          conn.on("open", function () {
            wireHostConn(conn);
          });
        });
        peer.on("error", function (e) {
          var msg = (e && e.type) || "peer-error";
          if (msg === "unavailable-id") {
            // Retry with new code
            hostRoom(makeCode(4));
            return;
          }
          lastError = "Host error (" + msg + ") — solo still works";
          status = "error";
          emitLobby();
        });
        peer.on("disconnected", function () {
          if (destroyed) return;
          try { peer.reconnect(); } catch (e2) { /* */ }
        });
      });
    }

    function joinRoom(code) {
      code = String(code || "").toUpperCase().replace(/[^A-Z0-9]/g, "");
      if (!code) {
        lastError = "Missing room code";
        status = "error";
        emitLobby();
        return;
      }
      role = "guest";
      room = code;
      lastError = null;
      status = "connecting";
      seats = emptySeats();
      emitLobby();
      ensurePeerScript(function (err) {
        if (err) {
          lastError = "Network unavailable — ask host to share again, or play solo";
          role = "solo";
          status = "error";
          emitLobby();
          return;
        }
        try {
          if (peer) {
            try { peer.destroy(); } catch (e1) { /* */ }
          }
          peer = new global.Peer({ debug: 0 });
        } catch (e) {
          lastError = "Could not join — try solo";
          role = "solo";
          status = "error";
          emitLobby();
          return;
        }
        peer.on("open", function (id) {
          localId = id;
          hostConn = peer.connect(peerIdForRoom(code), { reliable: true });
          hostConn.on("open", function () {
            status = "ready";
            send(hostConn, { t: "hello", peerId: localId });
            emitLobby();
          });
          hostConn.on("data", handleGuestMessage);
          hostConn.on("close", function () {
            lastError = "Host left — open invite again";
            status = "error";
            emitLobby();
          });
          hostConn.on("error", function () {
            lastError = "Could not reach host — check code / wifi";
            status = "error";
            emitLobby();
          });
        });
        peer.on("error", function (e) {
          lastError = "Join error — solo still works";
          status = "error";
          emitLobby();
        });
      });
    }

    function claimSeat(frogId) {
      if (role === "guest") {
        if (!hostConn || !hostConn.open) {
          lastError = "Not connected yet";
          emitLobby();
          lastError = null;
          return false;
        }
        send(hostConn, { t: "claim", frogId: frogId, label: "Joined" });
        return true;
      }
      // solo or host: claim locally
      if (!localId) localId = "local-" + makeCode(6);
      var target = seats[frogId];
      if (target && target.status === "human" && target.peerId && target.peerId !== localId) {
        lastError = "Seat taken";
        emitLobby();
        lastError = null;
        return false;
      }
      applyClaim(frogId, localId, "You", true);
      if (role === "host") broadcast(lobbyPayload());
      emitLobby(role === "solo" ? "idle" : "ready");
      return true;
    }

    function canStart() {
      return role === "solo" || role === "host";
    }

    function startParty() {
      if (!canStart()) return null;
      // Ensure at least one local seat: keyboard localId only if no couch pad claimed yet
      var anyPad = false;
      for (var pi = 0; pi < FROG_ORDER.length; pi++) {
        var ps = seats[FROG_ORDER[pi]];
        if (ps && ps.peerId && String(ps.peerId).indexOf("local-pad-") === 0) { anyPad = true; break; }
      }
      if (!anyPad && !seatClaimedBy(localId || "local")) {
        if (!localId) localId = "local-" + makeCode(6);
        if (seats.james.status === "open") {
          applyClaim("james", localId, "You", true);
        } else {
          for (var i = 0; i < FROG_ORDER.length; i++) {
            if (seats[FROG_ORDER[i]].status === "open") {
              applyClaim(FROG_ORDER[i], localId, "You", true);
              break;
            }
          }
        }
      }
      var map = buildSeatMap();
      if (role === "host") {
        broadcast({ t: "start", seatMap: map });
      }
      return map;
    }

    function sendInput(frogId, payload) {
      if (role !== "guest" || !hostConn) return;
      payload = payload || {};
      send(hostConn, {
        t: "input",
        frogId: frogId,
        steer: payload.steer || 0,
        steerX: typeof payload.steerX === "number" ? payload.steerX : 0,
        steerY: typeof payload.steerY === "number" ? payload.steerY : 0,
        ability: !!payload.ability,
        interact: !!payload.interact,
        targetLane: typeof payload.targetLane === "number" ? payload.targetLane : null,
      });
    }

    function sendState(stateObj) {
      if (role !== "host") return;
      var msg = stateObj || {};
      if (!msg.t || msg.t === "state") {
        msg.t = msg.mode === "hub" ? "hub" : "state";
      }
      broadcast(msg);
    }

    function sendEnd(payload) {
      if (role !== "host") return;
      var msg = payload || {};
      msg.t = "end";
      broadcast(msg);
    }

    function getLocalFrogId() {
      return seatClaimedBy(localId);
    }

    function resetSoloLobby() {
      role = "solo";
      room = null;
      seats = emptySeats();
      status = "idle";
      lastError = null;
      localId = localId || "local-" + makeCode(6);
      emitLobby();
    }

    function destroy() {
      destroyed = true;
      try {
        if (peer) peer.destroy();
      } catch (e) { /* */ }
      peer = null;
      conns = {};
      hostConn = null;
      if (global.FroggiesParty && global.FroggiesParty.active === api) {
        global.FroggiesParty.active = null;
      }
    }

    // Bootstrap local id for solo claims
    localId = "local-" + makeCode(6);

    var api = {
      FROG_ORDER: FROG_ORDER,
      hostRoom: hostRoom,
      joinRoom: joinRoom,
      claimSeat: claimSeat,
      claimLocalPad: claimLocalPad,
      releaseLocalPad: releaseLocalPad,
      startParty: startParty,
      canStart: canStart,
      sendInput: sendInput,
      sendState: sendState,
      sendEnd: sendEnd,
      getSeats: cloneSeats,
      getRole: function () { return role; },
      getRoom: function () { return room; },
      getLocalId: function () { return localId; },
      getLocalFrogId: getLocalFrogId,
      inviteUrl: function () { return room ? inviteUrl(room) : null; },
      resetSoloLobby: resetSoloLobby,
      buildSeatMap: buildSeatMap,
      destroy: destroy,
      parseRoomFromUrl: parseRoomFromUrl,
      makeCode: makeCode,
      emptySeats: emptySeats,
      emitLobby: emitLobby,
    };
    global.FroggiesParty.active = api;
    return api;
  }

  global.FroggiesParty = {
    active: null,
    createParty: createParty,
    parseRoomFromUrl: parseRoomFromUrl,
    inviteUrl: inviteUrl,
    makeCode: makeCode,
    FROG_ORDER: FROG_ORDER,
    emptySeats: emptySeats,
  };
})(typeof window !== "undefined" ? window : globalThis);
