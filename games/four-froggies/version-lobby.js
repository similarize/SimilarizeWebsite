/* Four Froggies — onefrog1 entrance.
   One screen: claim James/Jimmy/Bubbles/Rexy + Host/Join + pick 2D/2.5D/3D → play.
   Skips the old overlay frog-pick / party lobby. Cache: 20261006-rstick1 */
(function () {
  "use strict";

  var CACHE = "20261006-rstick1";
  var FF3D_CACHE = "20261006-rstick1";
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

  function seatOwnedByPad(seat, pi) {
    if (!seat || !seat.peerId) return false;
    if (seat.padIndex != null && (seat.padIndex | 0) === pi) return true;
    return seat.peerId === ("local-pad-" + pi);
  }

  /* One pad, one cursor. Lowest connected index gets the first open frog
     (James when free); the next pad gets the next open frog (Jimmy, …).
     A cursor already on an open frog stays there so B does not jump to Jimmy. */
  function ensureFocus(pi, seats) {
    var i, id, s, cur, open, j;
    for (i = 0; i < FROG_ORDER.length; i++) {
      id = FROG_ORDER[i];
      s = seats && seats[id];
      if (seatOwnedByPad(s, pi)) {
        padFocus[pi] = id;
        return id;
      }
    }
    /* Only lower indices count. A higher slot still sitting on James must
       not push pad 0 onto Jimmy; that higher slot moves off on its own turn. */
    function heldByOther(fid) {
      for (j = 0; j < pi; j++) {
        if (padFocus[j] === fid) return true;
      }
      return false;
    }
    cur = padFocus[pi];
    open = openFrogs(seats);
    if (cur && open.indexOf(cur) >= 0) return cur;
    for (i = 0; i < open.length; i++) {
      id = open[i];
      if (heldByOther(id)) continue;
      padFocus[pi] = id;
      return id;
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
    var latch = padAxisLatch[pi] || (padAxisLatch[pi] = { x: 0, y: 0, armed: false });
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
    /* First sample only arms the latch. A resting stick must not cycle
       James → Jimmy the moment the entrance opens. */
    if (!latch.armed) {
      latch.x = dx;
      latch.y = dy;
      latch.armed = true;
      return 0;
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
          if (padAxisLatch[pi]) padAxisLatch[pi].armed = false;
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
          if (P.enforceSeatInvariant) P.enforceSeatInvariant(focus);
          seats = P.getSeats ? P.getSeats() : seats;
        } else if (bp.b) {
          lastPad = pi;
          if (P.releaseLocalPad) P.releaseLocalPad(pi);
          if (P.enforceSeatInvariant) P.enforceSeatInvariant();
          /* Fresh seats: the pre-release copy still showed James claimed,
             so a cleared cursor skipped him and stuck on "pick Jimmy". */
          seats = P.getSeats ? P.getSeats() : seats;
          ensureFocus(pi, seats);
        }
        if (bp.y) {
          lastPad = pi;
          /* Y = start default 2D from entrance when allowed */
          tryStartMode("canvas");
        }
      }
    }

    seats = P && P.getSeats ? P.getSeats() : seats;
    /* Claimed pads paint on their seat only — never also as a second "pick" tile. */
    var claimedPadFrog = {};
    var fk;
    for (fk = 0; fk < FROG_ORDER.length; fk++) {
      var fs = seats && seats[FROG_ORDER[fk]];
      if (!fs || fs.padIndex == null || !fs.peerId) continue;
      if (fs.status !== "you" && fs.status !== "human") continue;
      claimedPadFrog[fs.padIndex | 0] = FROG_ORDER[fk];
    }
    var focusByFrog = {};
    var paintedPad = {};
    for (var pj = 0; pj < 4; pj++) {
      if (!connected[pj]) continue;
      var fid = padFocus[pj];
      if (claimedPadFrog[pj] != null) fid = claimedPadFrog[pj];
      if (!fid) continue;
      if (paintedPad[pj]) continue;
      paintedPad[pj] = fid;
      if (!focusByFrog[fid]) focusByFrog[fid] = [];
      if (focusByFrog[fid].indexOf(pj + 1) < 0) focusByFrog[fid].push(pj + 1);
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
              : "Pads " + focusPads.join(",") + " · pick " + fname;
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
    if (P && P.enforceSeatInvariant) P.enforceSeatInvariant();
    if (P && P.getLocalFrogId) {
      var id = P.getLocalFrogId();
      if (id) return id;
    }
    var seats = P && P.getSeats ? P.getSeats() : null;
    if (seats) {
      var padYou = null;
      var anyYou = null;
      for (var i = 0; i < FROG_ORDER.length; i++) {
        var fid = FROG_ORDER[i];
        var s = seats[fid];
        if (!s || s.status !== "you") continue;
        if (!anyYou) anyYou = fid;
        if (s.padIndex != null || (s.peerId && String(s.peerId).indexOf("local-pad-") === 0)) {
          if (!padYou) padYou = fid;
        }
      }
      if (padYou) return padYou;
      if (anyYou) return anyYou;
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
      var q = "?v=" + FF3D_CACHE + "&frog=" + encodeURIComponent(frog) + "&go=1";
      /* rstick1: carry pad claim so 3D page doesn't ghost-seat a second frog */
      try {
        var seats3 = P && P.getSeats ? P.getSeats() : null;
        var st3 = seats3 && seats3[frog];
        if (st3 && st3.padIndex != null) q += "&pad=" + (st3.padIndex | 0);
      } catch (ePad) {}
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
        if (P.enforceSeatInvariant) P.enforceSeatInvariant();
        var live = (P.connectedPadIndices && P.connectedPadIndices()) || [];
        var seatsNow = (P.getSeats && P.getSeats()) || {};
        /* Prefer last-active unique pad; else first unbound; else first live — never a second frog for same pad */
        if (P.claimPadOntoFrog && live.length) {
          var padToMove = null;
          if (lastPad >= 0 && live.indexOf(lastPad) >= 0) padToMove = lastPad;
          if (padToMove == null) {
            for (var i = 0; i < live.length; i++) {
              var pi = live[i] | 0;
              var claimed = FROG_ORDER.some(function (fid) {
                var s = seatsNow[fid];
                return s && s.peerId === ("local-pad-" + pi);
              });
              if (!claimed) { padToMove = pi; break; }
            }
          }
          if (padToMove == null) padToMove = live[0] | 0;
          var moved = P.claimPadOntoFrog(padToMove, id);
          if (moved) {
            lastPad = padToMove;
            padFocus[padToMove] = moved;
            if (P.enforceSeatInvariant) P.enforceSeatInvariant(moved);
            paintPads();
            return;
          }
          /* Seat taken or claim failed — do NOT claimSeat (would stack keyboard You) */
          paintPads();
          return;
        }
        if (P.claimSeat) P.claimSeat(id);
        if (P.enforceSeatInvariant) P.enforceSeatInvariant(id);
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
      location.replace("/games/four-froggies-3d/?v=" + FF3D_CACHE + "&go=1");
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
