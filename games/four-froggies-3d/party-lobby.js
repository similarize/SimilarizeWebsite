/* Four Froggies 3D — PeerJS Host/Join lobby on the title gate.
   Uses shared FroggiesParty API (party.js). Solo always works if PeerJS fails.
   onelobby2: honors ?host=CODE&frog=name&go=1 handoff from shared entrance. */
(function () {
  "use strict";

  var FROG_ORDER = ["james", "jimmy", "bubbles", "rexy"];
  var party = null;
  var partyMeta = { role: "solo", room: null, status: "idle", error: null, invite: null };
  var lobbySeats = null;
  var injected = false;
  var localPick = "james";
  var started = false;

  function $(id) { return document.getElementById(id); }

  function emptySeats() {
    var s = {};
    for (var i = 0; i < FROG_ORDER.length; i++) {
      s[FROG_ORDER[i]] = { status: "open", peerId: null, label: null, padIndex: null };
    }
    return s;
  }

  function game() { return window.__ff3dGame || null; }

  function ensurePlayNav() {
    if (document.getElementById("ff3d-play-nav")) return;
    var nav = document.createElement("div");
    nav.id = "ff3d-play-nav";
    nav.className = "ff3d-play-nav";
    var lobby = document.createElement("button");
    lobby.type = "button";
    lobby.className = "ff3d-nav-btn";
    lobby.textContent = "\u2190 Lobby";
    lobby.addEventListener("click", function () {
      var g = game();
      if (g && g.toLobby) g.toLobby();
      started = false;
      document.body.classList.remove("ff3d-playing");
      injected = false;
      var tries = 0;
      var timer = setInterval(function () {
        tries++;
        var card = document.querySelector(".gate-card");
        var fresh = card && !card.querySelector("#ff3d-btn-host");
        if (fresh && injectUi()) clearInterval(timer);
        if (tries > 40) clearInterval(timer);
      }, 50);
    });
    var follow = document.createElement("button");
    follow.type = "button";
    follow.className = "ff3d-nav-btn";
    follow.textContent = "Center View";
    follow.addEventListener("click", function () {
      var g = game();
      if (g && g.centerCamera) g.centerCamera();
    });
    var arcade = document.createElement("a");
    arcade.className = "ff3d-nav-btn";
    arcade.href = "/games/";
    arcade.textContent = "Arcade";
    nav.appendChild(lobby);
    nav.appendChild(follow);
    nav.appendChild(arcade);
    document.body.appendChild(nav);
  }

  function injectUi() {
    var card = document.querySelector(".gate-card");
    if (card && card.querySelector("#ff3d-btn-host")) {
      injected = true;
      return true;
    }
    if (!card) return false;
    var startBtn = card.querySelector("button.start");
    if (!startBtn) return false;

    // exit1: clear Arcade exit on 3D gate (do not touch ranch/space bundles)
    if (!document.getElementById("ff3d-exit-arcade")) {
      var exitA = document.createElement("a");
      exitA.id = "ff3d-exit-arcade";
      exitA.className = "exit-arcade ff3d-exit";
      exitA.href = "/games/";
      exitA.textContent = "← Arcade";
      var gate = document.querySelector(".gate") || document.body;
      gate.appendChild(exitA);
    }

    var wrap = document.createElement("div");
    wrap.className = "ff3d-party";
    wrap.innerHTML =
      '<p class="invite-cta">Party: <strong>Host room</strong> → Copy invite / scan QR · friends open link to <strong>Join</strong> · claim seats · Host presses Start. Solo: claim a seat and Start (AI fills open seats).</p>' +
      '<div class="party-bar">' +
      '<button type="button" class="party-btn" id="ff3d-btn-host">Host room</button>' +
      '<button type="button" class="party-btn party-btn-join" id="ff3d-btn-join">Join · enter code</button>' +
      '<input class="join-code" id="ff3d-join-code" maxlength="6" placeholder="CODE" autocomplete="off" spellcheck="false" aria-label="Room code" />' +
      '<button type="button" class="party-btn" id="ff3d-btn-copy" hidden>Copy invite link</button>' +
      '<span class="room-code" id="ff3d-room-code" aria-live="polite"></span>' +
      "</div>" +
      '<img class="invite-qr" id="ff3d-invite-qr" hidden alt="QR code to join this room" width="104" height="104" />' +
      '<p class="party-status" id="ff3d-party-status"></p>' +
      '<p class="party-help">Host = make room · Join = enter code or open invite link · couch pads still claim locally</p>';

    startBtn.parentNode.insertBefore(wrap, startBtn);

    $("ff3d-btn-host").addEventListener("click", function () {
      if (!party) return;
      party.hostRoom();
      paintParty();
    });
    $("ff3d-btn-join").addEventListener("click", function () {
      var input = $("ff3d-join-code");
      if (!party) return;
      if (input) input.hidden = false;
      var code = String((input && input.value) || "").toUpperCase().replace(/[^A-Z0-9]/g, "");
      if (code.length < 3) {
        if (input) input.focus();
        var st = $("ff3d-party-status");
        if (st) {
          st.classList.remove("is-error");
          st.textContent = "Type the host CODE then Join";
        }
        return;
      }
      try {
        var u = new URL(location.href);
        u.searchParams.set("room", code);
        history.replaceState(null, "", u.pathname + u.search + u.hash);
      } catch (e) {}
      party.joinRoom(code);
      paintParty();
    });
    $("ff3d-join-code").addEventListener("keydown", function (e) {
      if (e.key === "Enter") $("ff3d-btn-join").click();
    });
    $("ff3d-btn-copy").addEventListener("click", function () {
      var url = party && party.inviteUrl && party.inviteUrl();
      if (!url) return;
      function ok() {
        var st = $("ff3d-party-status");
        if (st) {
          st.classList.remove("is-error");
          st.textContent = "Invite link copied!";
        }
      }
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(url).then(ok).catch(function () {
          window.prompt("Copy invite link", url);
        });
      } else {
        window.prompt("Copy invite link", url);
        ok();
      }
    });

    // Seat claim: gate seat buttons
    card.querySelectorAll(".seats button.seat, .seats button").forEach(function (btn) {
      btn.addEventListener("click", function () {
        var name = (btn.querySelector("b") && btn.querySelector("b").textContent) || "";
        var id = FROG_ORDER.find(function (f) {
          return name.toLowerCase().indexOf(f) >= 0 || name === cap(f);
        });
        // Match by image src .../james.png etc
        var img = btn.querySelector("img");
        if (img && img.src) {
          for (var i = 0; i < FROG_ORDER.length; i++) {
            if (img.src.indexOf("/" + FROG_ORDER[i] + ".png") >= 0) id = FROG_ORDER[i];
          }
        }
        if (!id) return;
        localPick = id;
        var g = game();
        if (g && g.pick) g.pick(id);
        if (party && party.claimSeat) party.claimSeat(id);
      }, true);
    });

    // Intercept Start
    startBtn.addEventListener("click", function (e) {
      e.preventDefault();
      e.stopPropagation();
      e.stopImmediatePropagation();
      doStart();
    }, true);

    injected = true;
    paintParty();
    return true;
  }

  function cap(s) {
    return s.charAt(0).toUpperCase() + s.slice(1);
  }

  function paintParty() {
    if (!injected) return;
    var role = (party && party.getRole && party.getRole()) || partyMeta.role || "solo";
    var st = $("ff3d-party-status");
    var roomEl = $("ff3d-room-code");
    var qr = $("ff3d-invite-qr");
    var copy = $("ff3d-btn-copy");
    var hostBtn = $("ff3d-btn-host");
    var joinBtn = $("ff3d-btn-join");
    var joinInput = $("ff3d-join-code");

    if (st) {
      st.classList.toggle("is-error", !!partyMeta.error);
      if (partyMeta.error) st.textContent = partyMeta.error;
      else if (role === "host" && partyMeta.room)
        st.textContent = "Hosting · code " + partyMeta.room + " · friends open the invite link";
      else if (role === "guest" && partyMeta.status === "connecting")
        st.textContent = "Joining room " + (partyMeta.room || "") + "…";
      else if (role === "guest" && partyMeta.status === "ready")
        st.textContent = "Joined · claim an Open seat · wait for host Start";
      else st.textContent = "Solo · claim a seat or Start (AI fills the rest)";
    }
    if (roomEl) roomEl.textContent = partyMeta.room || "";
    if (copy) copy.hidden = !(role === "host" && partyMeta.invite);
    if (hostBtn) {
      hostBtn.hidden = role === "guest";
      hostBtn.textContent = role === "host" ? "Hosting…" : "Host room";
      hostBtn.disabled = role === "host" && partyMeta.status === "ready";
    }
    if (joinBtn) {
      joinBtn.hidden = role === "host" || role === "guest";
      joinBtn.disabled = role === "guest" && partyMeta.status === "connecting";
      joinBtn.textContent = role === "guest" ? "Joining…" : "Join · enter code";
    }
    if (joinInput) {
      joinInput.hidden = role === "host" || (role === "guest" && partyMeta.status === "ready");
      joinInput.disabled = role === "guest";
      if (role === "guest" && partyMeta.room && !joinInput.value) joinInput.value = partyMeta.room;
    }
    if (qr) {
      if (role === "host" && partyMeta.invite) {
        qr.hidden = false;
        qr.src =
          "https://api.qrserver.com/v1/create-qr-code/?size=104x104&data=" +
          encodeURIComponent(partyMeta.invite);
      } else {
        qr.hidden = true;
        qr.removeAttribute("src");
      }
    }

    // Reflect seat labels on gate buttons when we have lobby seats
    if (lobbySeats) {
      document.querySelectorAll(".gate-card .seats button").forEach(function (btn) {
        var img = btn.querySelector("img");
        var small = btn.querySelector("small");
        if (!img || !small) return;
        var id = null;
        for (var i = 0; i < FROG_ORDER.length; i++) {
          if (img.src.indexOf("/" + FROG_ORDER[i] + ".png") >= 0) id = FROG_ORDER[i];
        }
        if (!id || !lobbySeats[id]) return;
        var seat = lobbySeats[id];
        if (seat.status === "open") small.textContent = "Open";
        else if (seat.status === "ai") small.textContent = "AI";
        else small.textContent = seat.label || (seat.peerId ? "Joined" : "Taken");
      });
    }
  }

  function myFrogFromSeats(seatMap) {
    if (party && party.getLocalFrogId) {
      var lf = party.getLocalFrogId();
      if (lf) return lf;
    }
    if (!seatMap) return localPick;
    var localId = party && party.getLocalId && party.getLocalId();
    for (var i = 0; i < FROG_ORDER.length; i++) {
      var id = FROG_ORDER[i];
      var s = seatMap[id];
      if (!s) continue;
      if (s.local || (localId && s.peerId === localId)) return id;
    }
    return localPick;
  }

  function doStart() {
    if (started) return;
    var g = game();
    if (!g || !g.start) {
      setTimeout(doStart, 80);
      return;
    }
    var role = party && party.getRole ? party.getRole() : "solo";
    if (role === "guest") {
      var st = $("ff3d-party-status");
      if (st) {
        st.classList.remove("is-error");
        st.textContent = "Waiting for host to press Start…";
      }
      return;
    }
    var seatMap = null;
    if (party && role === "host" && party.startParty) {
      seatMap = party.startParty();
    }
    beginGame(seatMap);
  }

  function beginGame(seatMap) {
    if (started) return;
    var g = game();
    if (!g || !g.start) return;
    var frog = myFrogFromSeats(seatMap);
    if (g.pick) g.pick(frog);
    // Claim locals into gamepad-style roster: pick then start
    g.start(frog);
    started = true;
    document.body.classList.add("ff3d-playing");
    var gate = document.querySelector(".gate");
    if (gate) gate.style.display = "none";
  }

  function initParty() {
    if (typeof FroggiesParty === "undefined") {
      lobbySeats = emptySeats();
      paintParty();
      return;
    }
    party = FroggiesParty.createParty({
      onLobby: function (seats, meta) {
        lobbySeats = seats;
        partyMeta = meta || partyMeta;
        paintParty();
      },
      onStart: function (seatMap) {
        beginGame(seatMap);
      },
      onInput: function () {},
      onState: function () {},
      onEnd: function () {},
      onPeerGone: function () {
        paintParty();
      },
    });
    if (party.resetSoloLobby) party.resetSoloLobby();
    var room = FroggiesParty.parseRoomFromUrl && FroggiesParty.parseRoomFromUrl();
    var hostCode = null;
    var frogQ = null;
    var goQ = false;
    try {
      var u = new URL(location.href);
      hostCode = u.searchParams.get("host");
      frogQ = u.searchParams.get("frog");
      goQ = u.searchParams.get("go") === "1";
    } catch (eQ) {}
    if (hostCode && party.hostRoom) {
      party.hostRoom(String(hostCode).toUpperCase());
    } else if (room) {
      party.joinRoom(room);
    }
    if (frogQ) {
      localPick = String(frogQ).toLowerCase();
      if (FROG_ORDER.indexOf(localPick) < 0) localPick = "james";
      var g0 = game();
      if (g0 && g0.pick) g0.pick(localPick);
      if (party && party.claimSeat) party.claimSeat(localPick);
    }
    paintParty();
    if (goQ) {
      setTimeout(function () {
        if (!started) doStart();
      }, 200);
    }
  }

  function boot() {
    ensurePlayNav();
    if (!injectUi()) {
      requestAnimationFrame(boot);
      return;
    }
    initParty();
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", function () {
      setTimeout(boot, 50);
    });
  } else {
    setTimeout(boot, 50);
  }
})();
