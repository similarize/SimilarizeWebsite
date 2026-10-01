/* Four Froggies — onelobby2 entrance.
   One screen: claim James/Jimmy/Bubbles/Rexy + Host/Join + pick 2D/2.5D/3D → play.
   Skips the old overlay frog-pick / party lobby. Cache: 20260930-onelobby2 */
(function () {
  "use strict";

  var CACHE = "20260930-onelobby2";
  var FROG_ORDER = ["james", "jimmy", "bubbles", "rexy"];
  var FROG_NAME = { james: "James", jimmy: "Jimmy", bubbles: "Bubbles", rexy: "Rexy" };

  var lobby = document.getElementById("version-lobby");
  if (!lobby) return;

  var COUNT_LINE = [
    "No controllers are connected",
    "One controller is connected",
    "Two controllers are connected",
    "Three controllers are connected",
    "Four controllers are connected"
  ];
  var countEl = document.getElementById("pad-count");
  var hintEl = document.getElementById("pad-dock-hint");
  var youEl = document.getElementById("pad-you");
  var tiles = lobby.querySelectorAll(".pad-tile.frog-btn");
  var padFocus = { 0: null, 1: null, 2: null, 3: null };
  var padAxisLatch = { 0: { x: 0, y: 0 }, 1: { x: 0, y: 0 }, 2: { x: 0, y: 0 }, 3: { x: 0, y: 0 } };
  var lastPad = -1;
  var starting = false;

  function partyApi() {
    return (window.FroggiesParty && window.FroggiesParty.active) || null;
  }

  function connectedIdxs() {
    var GP = window.SimilarizeGamepad;
    if (!GP) return [];
    if (typeof GP.uniqueConnectedIndices === "function") return GP.uniqueConnectedIndices(4) || [];
    if (typeof GP.connectedIndices === "function") return GP.connectedIndices(4) || [];
    return [];
  }

  function openFrogs(seats) {
    var out = [];
    for (var i = 0; i < FROG_ORDER.length; i++) {
      var id = FROG_ORDER[i];
      var s = seats && seats[id];
      if (!s || s.status === "open" || !s.peerId) out.push(id);
    }
    return out;
  }

  function ensureFocus(pi, seats) {
    var cur = padFocus[pi];
    var open = openFrogs(seats);
    if (cur && open.indexOf(cur) >= 0) return cur;
    if (cur) {
      var s = seats && seats[cur];
      if (s && s.padIndex === pi) return cur;
    }
    padFocus[pi] = open.length ? open[0] : FROG_ORDER[pi % 4];
    return padFocus[pi];
  }

  function cycleFocus(pi, dir, seats) {
    var open = openFrogs(seats);
    if (!open.length) return ensureFocus(pi, seats);
    var cur = ensureFocus(pi, seats);
    var idx = open.indexOf(cur);
    if (idx < 0) idx = 0;
    idx = (idx + (dir > 0 ? 1 : -1) + open.length) % open.length;
    padFocus[pi] = open[idx];
    return padFocus[pi];
  }

  function axisEdge(pi, snap) {
    var latch = padAxisLatch[pi] || (padAxisLatch[pi] = { x: 0, y: 0 });
    var lx = snap && snap.lx ? snap.lx : 0;
    var ly = snap && snap.ly ? snap.ly : 0;
    var dx = 0;
    var dy = 0;
    if (snap && snap.dpad) {
      if (snap.dpad.r) dx = 1;
      else if (snap.dpad.l) dx = -1;
      if (snap.dpad.d) dy = 1;
      else if (snap.dpad.u) dy = -1;
    }
    if (!dx) {
      if (lx > 0.55) dx = 1;
      else if (lx < -0.55) dx = -1;
    }
    if (!dy) {
      if (ly > 0.55) dy = 1;
      else if (ly < -0.55) dy = -1;
    }
    var out = 0;
    if (dx && dx !== latch.x) out = dx;
    else if (dy && dy !== latch.y) out = dy;
    latch.x = dx;
    latch.y = dy;
    return out;
  }

  function paintPads() {
    if (!countEl || !tiles.length) return;
    var GP = window.SimilarizeGamepad;
    var P = partyApi();
    var seats = P && P.getSeats ? P.getSeats() : null;
    var idxs = connectedIdxs();
    var connected = {};
    for (var i = 0; i < idxs.length; i++) connected[idxs[i] | 0] = true;
    var snaps = GP && typeof GP.pollAll === "function" ? GP.pollAll(4) : [];
    var n = idxs.length;
    var line = COUNT_LINE[n] || (n + " controllers are connected");
    if (countEl.textContent !== line) countEl.textContent = line;
    if (hintEl) {
      var hint = n === 0
        ? "Tap James / Jimmy / Bubbles / Rexy (or plug a pad). Host/Join optional. Then pick 2D / 2.5D / 3D."
        : "Stick/D-pad cycles open froggies · A claims · B releases · one pad = one froggy";
      if (hintEl.textContent !== hint) hintEl.textContent = hint;
    }

    /* Pad input while entrance is up */
    if (!lobby.hidden && P && snaps && snaps.length) {
      for (var pi = 0; pi < 4; pi++) {
        if (!connected[pi]) {
          padFocus[pi] = null;
          continue;
        }
        var snap = snaps[pi];
        if (!snap || !snap.connected) continue;
        ensureFocus(pi, seats);
        var dir = axisEdge(pi, snap);
        if (dir) {
          lastPad = pi;
          cycleFocus(pi, dir, seats);
          seats = P.getSeats ? P.getSeats() : seats;
        }
        var bp = snap.buttonsPressed || {};
        if (bp.a || bp.start) {
          lastPad = pi;
          var focus = ensureFocus(pi, seats);
          if (P.claimPadOntoFrog) P.claimPadOntoFrog(pi, focus);
          seats = P.getSeats ? P.getSeats() : seats;
        } else if (bp.b) {
          lastPad = pi;
          if (P.releaseLocalPad) P.releaseLocalPad(pi);
          ensureFocus(pi, seats);
          seats = P.getSeats ? P.getSeats() : seats;
        }
        if (bp.y) {
          lastPad = pi;
          /* Y = start default 2D from entrance when allowed */
          tryStartMode("canvas");
        }
      }
    }

    seats = P && P.getSeats ? P.getSeats() : seats;
    var focusByFrog = {};
    for (var pj = 0; pj < 4; pj++) {
      if (!connected[pj]) continue;
      var fid = padFocus[pj];
      if (!fid) continue;
      if (!focusByFrog[fid]) focusByFrog[fid] = [];
      focusByFrog[fid].push(pj + 1);
    }

    var youFrog = null;
    for (var t = 0; t < tiles.length; t++) {
      var tile = tiles[t];
      var id = tile.getAttribute("data-id");
      var seat = seats && seats[id] ? seats[id] : { status: "open" };
      var status = seat.status || "open";
      tile.classList.remove("is-on", "is-hot", "is-you", "seat-you", "seat-human", "seat-open", "pad-focus", "seat-pad");
      tile.removeAttribute("data-pad");
      tile.removeAttribute("data-focus-pad");
      var em = tile.querySelector(".seat-state, em");
      var fname = FROG_NAME[id] || id;

      if (status === "you") {
        tile.classList.add("is-on", "is-you", "seat-you");
        var pIdx = seat.padIndex != null ? (seat.padIndex | 0) : null;
        if (pIdx != null) {
          tile.classList.add("seat-pad", "is-hot");
          tile.setAttribute("data-pad", String(pIdx + 1));
          if (em) em.textContent = "Pad " + (pIdx + 1) + " · " + fname;
          if (!youFrog) youFrog = { frog: fname, pad: pIdx };
        } else {
          if (em) em.textContent = seat.label || "You · " + fname;
          if (!youFrog) youFrog = { frog: fname, pad: -1 };
        }
      } else if (status === "human") {
        tile.classList.add("is-on", "seat-human");
        if (em) em.textContent = seat.label || "Joined";
      } else {
        tile.classList.add("seat-open");
        var focusPads = focusByFrog[id] || [];
        if (focusPads.length) {
          tile.classList.add("pad-focus", "is-on");
          tile.setAttribute("data-focus-pad", String(focusPads[0]));
          if (em) {
            em.textContent = focusPads.length === 1
              ? "Pad " + focusPads[0] + " · pick " + fname
              : "Pads " + focusPads.join(",") + " · pick";
          }
        } else if (em) {
          em.textContent = "Open · AI";
        }
      }
    }

    if (youEl) {
      if (!youFrog) {
        youEl.hidden = true;
      } else {
        youEl.hidden = false;
        youEl.classList.remove("is-multi");
        var msg = youFrog.pad >= 0
          ? ("You are " + youFrog.frog + " · Pad " + (youFrog.pad + 1))
          : ("You are " + youFrog.frog);
        if (youEl.textContent !== msg) youEl.textContent = msg;
        if (youFrog.pad >= 0) youEl.setAttribute("data-pad", String(youFrog.pad));
        else youEl.removeAttribute("data-pad");
      }
    }
  }

  function claimedFrogId() {
    var P = partyApi();
    if (P && P.getLocalFrogId) {
      var id = P.getLocalFrogId();
      if (id) return id;
    }
    var seats = P && P.getSeats ? P.getSeats() : null;
    if (seats) {
      for (var i = 0; i < FROG_ORDER.length; i++) {
        var fid = FROG_ORDER[i];
        if (seats[fid] && seats[fid].status === "you") return fid;
      }
    }
    return "james";
  }

  function hideEntrance() {
    lobby.hidden = true;
    document.body.classList.remove("in-entrance");
  }

  function showEntrance() {
    lobby.hidden = false;
    document.body.classList.add("in-entrance");
    document.body.classList.add("in-title");
    document.body.classList.remove("in-hub");
    document.body.classList.remove("in-space");
    var overlay = document.getElementById("overlay");
    if (overlay) overlay.hidden = true;
    starting = false;
    requestAnimationFrame(frame);
  }

  function tryStartMode(engine) {
    if (starting || lobby.hidden) return;
    var P = partyApi();
    var role = P && P.getRole ? P.getRole() : "solo";
    if (role === "guest") {
      var st = document.getElementById("party-status");
      if (st) {
        st.classList.remove("is-error");
        st.textContent = "Joined · wait for Host to pick 2D / 2.5D / 3D";
      }
      return;
    }

    if (engine === "3d") {
      starting = true;
      var frog = claimedFrogId();
      var room = P && P.getRoom ? P.getRoom() : null;
      var q = "?v=" + CACHE + "&frog=" + encodeURIComponent(frog) + "&go=1";
      if (role === "host" && room) q += "&host=" + encodeURIComponent(room);
      else if (room) q += "&room=" + encodeURIComponent(room);
      location.href = "/games/four-froggies-3d/" + q;
      return;
    }

    starting = true;
    hideEntrance();
    if (window.FroggiesCanon && FroggiesCanon.setEngine) FroggiesCanon.setEngine(engine);
    if (window.FroggiesEngines && FroggiesEngines.paintPicker) FroggiesEngines.paintPicker();

    var overlay = document.getElementById("overlay");
    if (overlay) overlay.hidden = true;

    /* Prefer the shared one-lobby start hook from main.js */
    if (window.FroggiesOneLobby && typeof window.FroggiesOneLobby.startNow === "function") {
      window.FroggiesOneLobby.startNow(engine);
      return;
    }

    /* Fallback */
    if (engine === "three" && window.FroggiesEngines && FroggiesEngines.startAlt) {
      FroggiesEngines.startAlt("three");
      return;
    }
    if (P && P.startParty) {
      var map = P.startParty();
      if (window.FroggiesOneLobby && FroggiesOneLobby.startHub) {
        FroggiesOneLobby.startHub(map);
      }
    }
  }

  function frame() {
    if (lobby.hidden) return;
    paintPads();
    requestAnimationFrame(frame);
  }

  lobby.addEventListener("click", function (e) {
    var frogBtn = e.target.closest(".frog-btn[data-id]");
    if (frogBtn && lobby.contains(frogBtn)) {
      var id = frogBtn.getAttribute("data-id");
      var P = partyApi();
      if (P) {
        if (lastPad >= 0 && P.claimPadOntoFrog) {
          var moved = P.claimPadOntoFrog(lastPad, id);
          if (moved) {
            padFocus[lastPad] = moved;
            paintPads();
            return;
          }
        }
        if (P.claimPadOntoFrog && P.connectedPadIndices) {
          var live = P.connectedPadIndices() || [];
          var padToMove = null;
          for (var i = 0; i < live.length; i++) {
            var pi = live[i] | 0;
            var seats = P.getSeats() || {};
            var claimed = FROG_ORDER.some(function (fid) {
              var s = seats[fid];
              return s && s.peerId === ("local-pad-" + pi);
            });
            if (!claimed) { padToMove = pi; break; }
          }
          if (padToMove == null && live.length) padToMove = live[0] | 0;
          if (padToMove != null && P.claimPadOntoFrog(padToMove, id)) {
            lastPad = padToMove;
            padFocus[padToMove] = id;
            paintPads();
            return;
          }
        }
        if (P.claimSeat) P.claimSeat(id);
      }
      paintPads();
      return;
    }

    var btn = e.target.closest("[data-version]");
    if (!btn || !lobby.contains(btn)) return;
    var ver = btn.getAttribute("data-version");
    if (ver === "3d") tryStartMode("3d");
    else if (ver === "three") tryStartMode("three");
    else tryStartMode("canvas");
  });

  var back = document.getElementById("btn-versions");
  if (back) {
    back.addEventListener("click", function () {
      if (window.FroggiesEngines && FroggiesEngines.stopAltEngines) FroggiesEngines.stopAltEngines();
      showEntrance();
    });
  }

  var skip = false;
  var autoEngine = null;
  try {
    var q = new URLSearchParams(location.search);
    if (q.get("engine") === "3d") {
      location.replace("/games/four-froggies-3d/?v=" + CACHE + "&go=1");
      return;
    }
    if (q.get("engine") === "three" || q.get("engine") === "canvas") {
      autoEngine = q.get("engine");
      skip = true;
    }
    if (q.get("go") === "1" || q.get("autogo") === "1") {
      skip = true;
      if (!autoEngine) autoEngine = "canvas";
    }
  } catch (err) { /* keep lobby */ }

  document.body.classList.add("in-entrance");
  if (skip) {
    lobby.hidden = true;
    document.body.classList.remove("in-entrance");
    var overlaySkip = document.getElementById("overlay");
    if (overlaySkip) overlaySkip.hidden = true;
    if (autoEngine) {
      setTimeout(function () {
        if (window.FroggiesOneLobby && FroggiesOneLobby.startNow) {
          FroggiesOneLobby.startNow(autoEngine);
        } else {
          tryStartMode(autoEngine);
        }
      }, 320);
    }
  } else {
    var overlay = document.getElementById("overlay");
    if (overlay) overlay.hidden = true;
    showEntrance();
  }

  window.FroggiesVersionLobby = {
    show: showEntrance,
    hide: hideEntrance,
    startMode: tryStartMode,
    paint: paintPads,
    cache: CACHE,
  };
})();
