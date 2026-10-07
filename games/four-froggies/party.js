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
      s[FROG_ORDER[i]] = { status: "open", peerId: null, label: null, padIndex: null };
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
        out[id] = {
          status: s.status,
          peerId: s.peerId,
          label: s.label,
          padIndex: s.padIndex != null ? s.padIndex : null,
        };
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
          seats[id] = { status: "open", peerId: null, label: null, padIndex: null };
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
      enforceSeatInvariant(frogId);
      return { ok: true };
    }

    /**
     * Hard invariant: one peerId ↔ one frog; one padIndex ↔ one frog;
     * keyboard localId never stacks beside local-pad-* seats.
     * preferredFrogId (optional) wins when duplicates must be dropped.
     */
    function enforceSeatInvariant(preferredFrogId) {
      var seenPeer = Object.create(null);
      var seenPad = Object.create(null);
      var hasPadSeat = false;
      var i, id, s, peer, pi;
      /* Prefer keeping preferredFrogId / earlier frogs; drop later dupes */
      var order = FROG_ORDER.slice();
      if (preferredFrogId && order.indexOf(preferredFrogId) >= 0) {
        order.splice(order.indexOf(preferredFrogId), 1);
        order.unshift(preferredFrogId);
      }
      for (i = 0; i < order.length; i++) {
        id = order[i];
        s = seats[id];
        if (!s || !s.peerId) {
          seats[id] = { status: "open", peerId: null, label: null, padIndex: null };
          continue;
        }
        peer = String(s.peerId);
        pi = s.padIndex != null ? (s.padIndex | 0) : padIndexFromPeer(peer);
        if (peer.indexOf("local-pad-") === 0) hasPadSeat = true;
        if (seenPeer[peer]) {
          seats[id] = { status: "open", peerId: null, label: null, padIndex: null };
          continue;
        }
        if (pi != null && seenPad[pi] != null) {
          seats[id] = { status: "open", peerId: null, label: null, padIndex: null };
          continue;
        }
        seenPeer[peer] = id;
        if (pi != null) seenPad[pi] = id;
        s.padIndex = pi;
      }
      /* Drop keyboard-only locals when any pad seat exists */
      if (hasPadSeat) {
        for (i = 0; i < FROG_ORDER.length; i++) {
          id = FROG_ORDER[i];
          s = seats[id];
          if (!s || !s.peerId) continue;
          if (String(s.peerId).indexOf("local-pad-") === 0) continue;
          if (isLocalPeerId(s.peerId) || s.peerId === localId || s.peerId === "local") {
            seats[id] = { status: "open", peerId: null, label: null, padIndex: null };
          }
        }
      }
      /* Re-normalize status/labels after scrub */
      for (i = 0; i < FROG_ORDER.length; i++) {
        id = FROG_ORDER[i];
        s = seats[id];
        if (!s || !s.peerId) {
          seats[id] = { status: "open", peerId: null, label: null, padIndex: null };
          continue;
        }
        if (isLocalPeerId(s.peerId)) {
          s.status = "you";
          pi = s.padIndex != null ? s.padIndex : padIndexFromPeer(s.peerId);
          s.padIndex = pi;
          s.label = pi != null ? ("Pad " + (pi + 1)) : (s.peerId === localId ? "You" : (s.label || "You"));
        } else {
          s.status = "human";
          s.padIndex = null;
          s.label = s.label || "Joined";
        }
      }
    }

    function buildSeatMap() {
      enforceSeatInvariant();
      var map = {};
      var usedPads = Object.create(null);
      for (var i = 0; i < FROG_ORDER.length; i++) {
        var id = FROG_ORDER[i];
        var s = seats[id];
        var isHuman = !!(s.peerId && (s.status === "you" || s.status === "human"));
        var local = isHuman && isLocalPeerId(s.peerId);
        var pIdx = s.padIndex != null ? s.padIndex : padIndexFromPeer(s.peerId);
        if (local && pIdx != null) {
          if (usedPads[pIdx] != null) {
            isHuman = false;
            local = false;
            pIdx = null;
          } else {
            usedPads[pIdx] = id;
          }
        }
        map[id] = {
          human: isHuman,
          local: local,
          peerId: isHuman ? s.peerId : null,
          padIndex: local ? (pIdx != null ? pIdx : null) : null,
        };
      }
      return map;
    }

    function connectedPadIndices() {
      try {
        var GP = global.SimilarizeGamepad;
        if (!GP) return [];
        /* Distinct Gamepad indices keep multiple identical controller models separate. */
        if (typeof GP.uniqueConnectedIndices === "function") {
          return GP.uniqueConnectedIndices(4) || [];
        }
        if (typeof GP.connectedIndices === "function") {
          return GP.connectedIndices(4) || [];
        }
      } catch (e) { /* ignore */ }
      return [];
    }

    function anyPadSeatBound() {
      for (var i = 0; i < FROG_ORDER.length; i++) {
        var s = seats[FROG_ORDER[i]];
        if (s && s.peerId && String(s.peerId).indexOf("local-pad-") === 0) return true;
      }
      return false;
    }

    function padAlreadyBound(padIndex) {
      var peerId = "local-pad-" + (padIndex | 0);
      return !!seatClaimedBy(peerId);
    }

    function firstUnboundConnectedPad() {
      var idxs = connectedPadIndices();
      for (var i = 0; i < idxs.length; i++) {
        if (!padAlreadyBound(idxs[i])) return idxs[i] | 0;
      }
      return null;
    }


    /** padbind1: drop local-pad claims on Steam/USB ghost aliases (still enumerated
     *  in getGamepads but collapsed out of uniqueConnectedIndices). Do NOT wipe on a
     *  transient empty poll — that was wiping real seats mid-frame. */
    function pruneDeadPadClaims() {
      var live = connectedPadIndices();
      var liveSet = Object.create(null);
      var i, id, s, pi, peer;
      for (i = 0; i < live.length; i++) liveSet[live[i] | 0] = true;
      /* padfix2: if ANY unique pad is live, free claims whose index is not in
         that set — covers connected Steam ghosts (padbind1) AND null/disconnect
         (padedge1). Skip wipe only when the unique set is empty (Chrome empty
         frame) so real seats are not cleared mid-poll. */
      if (!live.length) return;
      var changed = false;
      for (i = 0; i < FROG_ORDER.length; i++) {
        id = FROG_ORDER[i];
        s = seats[id];
        if (!s || !s.peerId) continue;
        peer = String(s.peerId);
        if (peer.indexOf("local-pad-") !== 0) continue;
        pi = s.padIndex != null ? (s.padIndex | 0) : padIndexFromPeer(peer);
        if (pi == null || liveSet[pi]) continue;
        seats[id] = { status: "open", peerId: null, label: null, padIndex: null };
        changed = true;
      }
      if (changed) enforceSeatInvariant();
    }

    /** Aggressive prune for Start / disconnect: also drop seats whose slot is gone. */
    function pruneDeadPadClaimsHard() {
      var live = connectedPadIndices();
      var liveSet = Object.create(null);
      var i, id, s, pi, peer;
      for (i = 0; i < live.length; i++) liveSet[live[i] | 0] = true;
      for (i = 0; i < FROG_ORDER.length; i++) {
        id = FROG_ORDER[i];
        s = seats[id];
        if (!s || !s.peerId) continue;
        peer = String(s.peerId);
        if (peer.indexOf("local-pad-") !== 0) continue;
        pi = s.padIndex != null ? (s.padIndex | 0) : padIndexFromPeer(peer);
        if (pi == null || liveSet[pi]) continue;
        seats[id] = { status: "open", peerId: null, label: null, padIndex: null };
      }
      enforceSeatInvariant();
    }


    /** padfix2: local pad→frog claims must not exceed unique connected sticks. */
    function capLocalPadSeats(preferredFrogId) {
      var live = connectedPadIndices();
      var liveSet = Object.create(null);
      var i, id, s, pi, peer, list;
      for (i = 0; i < live.length; i++) liveSet[live[i] | 0] = true;
      list = [];
      for (i = 0; i < FROG_ORDER.length; i++) {
        id = FROG_ORDER[i];
        s = seats[id];
        if (!s || !s.peerId) continue;
        peer = String(s.peerId);
        if (peer.indexOf("local-pad-") !== 0) continue;
        pi = s.padIndex != null ? (s.padIndex | 0) : padIndexFromPeer(peer);
        if (pi == null) continue;
        if (live.length && !liveSet[pi]) {
          seats[id] = { status: "open", peerId: null, label: null, padIndex: null };
          continue;
        }
        list.push({ id: id, pi: pi });
      }
      if (!live.length || list.length <= live.length) {
        enforceSeatInvariant(preferredFrogId);
        return;
      }
      /* Too many claims for the sticks we have — keep preferred + earliest live pads */
      var keep = Object.create(null);
      if (preferredFrogId) {
        for (i = 0; i < list.length; i++) {
          if (list[i].id === preferredFrogId) {
            keep[list[i].id] = true;
            break;
          }
        }
      }
      for (i = 0; i < live.length; i++) {
        var want = live[i] | 0;
        for (var j = 0; j < list.length; j++) {
          if (keep[list[j].id]) continue;
          if ((list[j].pi | 0) === want) {
            keep[list[j].id] = true;
            break;
          }
        }
        var kept = 0;
        for (var k in keep) if (Object.prototype.hasOwnProperty.call(keep, k)) kept++;
        if (kept >= live.length) break;
      }
      for (i = 0; i < list.length; i++) {
        if (!keep[list[i].id]) {
          seats[list[i].id] = { status: "open", peerId: null, label: null, padIndex: null };
        }
      }
      enforceSeatInvariant(preferredFrogId);
    }

    function canonicalizePadIndex(padIndex) {
      var idx = padIndex | 0;
      try {
        var GP = global.SimilarizeGamepad;
        if (GP && typeof GP.canonicalIndex === "function") {
          var c = GP.canonicalIndex(idx, 4);
          if (typeof c === "number" && c >= 0) return c | 0;
          /* padfix2: unproven / ghost slot — do NOT fall back to raw index */
          return -1;
        }
      } catch (e) { /* ignore */ }
      var live = connectedPadIndices();
      return live.indexOf(idx) >= 0 ? idx : -1;
    }

    /** Couch: bind pad N onto a specific frog (focus+A or click). One pad → one seat; no steal. */
    function claimPadOntoFrog(padIndex, frogId) {
      if (role === "guest") return null;
      var idx = canonicalizePadIndex(padIndex);
      if (idx < 0 || idx > 3) return null;
      /* padfix2: only unique live sticks may claim */
      var liveNow = connectedPadIndices();
      if (liveNow.indexOf(idx) < 0) return null;
      if (FROG_ORDER.indexOf(frogId) < 0) return null;
      if (!localId) localId = "local-" + makeCode(6);
      var peerId = "local-pad-" + idx;
      var cur = seats[frogId];
      if (cur && cur.peerId && cur.peerId !== peerId && (cur.status === "human" || cur.status === "you")) {
        return null;
      }
      clearKeyboardOnlyLocals();
      var res = applyClaim(frogId, peerId, "Pad " + (idx + 1), true, idx);
      if (!res.ok) return null;
      enforceSeatInvariant(frogId);
      /* Hard guard: same padIndex must not remain on two frogs (padedge1) */
      var dup = 0, fi, ss, otherPi;
      for (fi = 0; fi < FROG_ORDER.length; fi++) {
        ss = seats[FROG_ORDER[fi]];
        if (ss && ss.padIndex != null && (ss.padIndex | 0) === idx) dup++;
      }
      if (dup > 1) {
        console.warn("[ff-party] dual-claim blocked for pad", idx);
        seats[frogId] = { status: "open", peerId: null, label: null, padIndex: null };
        enforceSeatInvariant(frogId);
        return null;
      }
      /* padfix2: drop ghost-index seats that canonicalize onto this stick */
      for (fi = 0; fi < FROG_ORDER.length; fi++) {
        if (FROG_ORDER[fi] === frogId) continue;
        ss = seats[FROG_ORDER[fi]];
        if (!ss || !ss.peerId || String(ss.peerId).indexOf("local-pad-") !== 0) continue;
        otherPi = ss.padIndex != null ? (ss.padIndex | 0) : padIndexFromPeer(ss.peerId);
        if (otherPi == null || otherPi === idx) continue;
        if (canonicalizePadIndex(otherPi) === idx) {
          seats[FROG_ORDER[fi]] = { status: "open", peerId: null, label: null, padIndex: null };
        }
      }
      enforceSeatInvariant(frogId);
      capLocalPadSeats(frogId);
      if (role === "host") broadcast(lobbyPayload());
      emitLobby(role === "solo" ? "idle" : "ready");
      return frogId;
    }

    /**
     * Couch: pad N claims next open froggy (GO auto-seat / fallback).
     * Does not steal. If already claimed, keeps that seat (no silent reshuffle).
     * Lobby pick UX uses claimPadOntoFrog after D-pad/stick focus — not this.
     */
    function claimLocalPad(padIndex) {
      if (role === "guest") return null;
      var idx = canonicalizePadIndex(padIndex);
      if (idx < 0 || idx > 3) return null;
      if (!localId) localId = "local-" + makeCode(6);
      var peerId = "local-pad-" + idx;
      var existing = seatClaimedBy(peerId);
      if (existing) return existing;
      clearKeyboardOnlyLocals();
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
      enforceSeatInvariant();
      if (role === "host") broadcast(lobbyPayload());
      emitLobby(role === "solo" ? "idle" : "ready");
      return true;
    }

    /** Drop keyboard-only locals so pads cannot share a seat with a padless "You". */
    function clearKeyboardOnlyLocals() {
      for (var i = 0; i < FROG_ORDER.length; i++) {
        var id = FROG_ORDER[i];
        var s = seats[id];
        if (!s || !s.peerId) continue;
        if (String(s.peerId).indexOf("local-pad-") === 0) continue;
        if (isLocalPeerId(s.peerId) || s.peerId === localId || s.peerId === "local") {
          seats[id] = { status: "open", peerId: null, label: null, padIndex: null };
        }
      }
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
          hooks.onStart(map, { engine: msg.engine || "canvas" });
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
      // solo or host: prefer binding a free connected pad (one pad → one frog)
      if (!localId) localId = "local-" + makeCode(6);
      var target = seats[frogId];
      var livePads = connectedPadIndices();
      var padsInPlay = livePads.length > 0 || anyPadSeatBound();
      var freePad = firstUnboundConnectedPad();
      if (freePad != null) {
        if (target && target.peerId && String(target.peerId).indexOf("local-pad-") === 0 &&
            target.peerId !== ("local-pad-" + freePad)) {
          lastError = "Seat taken · pick another froggy";
          emitLobby();
          lastError = null;
          return false;
        }
        var got = claimPadOntoFrog(freePad, frogId);
        return !!got;
      }
      if (target && target.peerId && target.peerId !== localId &&
          (target.status === "human" || target.status === "you")) {
        lastError = "Seat taken · pick another froggy";
        emitLobby();
        lastError = null;
        return false;
      }
      /* Never stack keyboard "You" beside pad seats (one controller was driving two frogs) */
      if (padsInPlay) {
        lastError = "Use stick + A on a pad (or click moves your pad) · one pad = one froggy";
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
      pruneDeadPadClaimsHard();
      /* padexit2: one pad = one froggy.
         If lobby already has pad claims, keep ONLY those — do not auto-seat every
         live index (USB/Steam ghost slots were claiming a second frog on Start).
         Plug-and-go (no claims yet): auto-seat each unique live index once. */
      var livePads = connectedPadIndices();
      var anyPad = anyPadSeatBound();
      var pi;
      if (livePads.length > 0 || anyPad) {
        clearKeyboardOnlyLocals();
        if (!anyPad) {
          /* rstick1: auto-seat ONLY the first live pad. Extra pads must A-claim.
             Steam/USB ghost indices used to seat a second frog on the same stick. */
          if (livePads.length) claimLocalPad(livePads[0]);
        } else {
          /* Refresh existing pad peers only — never invent a second frog for an unbound ghost slot */
          for (pi = 0; pi < livePads.length && pi < 4; pi++) {
            var idxKeep = livePads[pi] | 0;
            if (padAlreadyBound(idxKeep)) claimLocalPad(idxKeep);
          }
        }
      } else if (!seatClaimedBy(localId || "local")) {
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
      enforceSeatInvariant();
      var map = buildSeatMap();
      if (role === "host") {
        var eng = "canvas";
        try {
          if (global.FroggiesCanon && typeof global.FroggiesCanon.getEngine === "function") {
            eng = global.FroggiesCanon.getEngine() || "canvas";
          }
        } catch (eEng) { /* canvas */ }
        broadcast({ t: "start", seatMap: map, engine: eng });
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
      enforceSeatInvariant();
      var byKeyboard = seatClaimedBy(localId);
      if (byKeyboard) return byKeyboard;
      for (var i = 0; i < FROG_ORDER.length; i++) {
        var id = FROG_ORDER[i];
        var s = seats[id];
        if (s && s.status === "you" && s.peerId) return id;
      }
      return null;
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
      claimPadOntoFrog: claimPadOntoFrog,
      releaseLocalPad: releaseLocalPad,
      connectedPadIndices: connectedPadIndices,
      enforceSeatInvariant: enforceSeatInvariant,
      pruneDeadPadClaims: pruneDeadPadClaims,
      pruneDeadPadClaimsHard: pruneDeadPadClaimsHard,
      capLocalPadSeats: capLocalPadSeats,
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
