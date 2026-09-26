/* Four Froggies — lobby + 2.5D ranch hub (flagship).
   Party: PeerJS via party.js. Solo+AI offline. Strip kept in strip.js as sandbox activity. */
(() => {
  "use strict";

  const canvas = document.getElementById("view");
  const ctx = canvas.getContext("2d");
  const overlay = document.getElementById("overlay");
  const overlayTitle = document.getElementById("overlay-title");
  const overlayText = document.getElementById("overlay-text");
  const overlayGo = document.getElementById("overlay-go");
  const overlaySub = document.getElementById("overlay-sub");
  const frogPick = document.getElementById("frog-pick");
  const livesEl = document.getElementById("lives");
  const scrapEl = document.getElementById("scrap");
  const progressBar = document.getElementById("progress-bar");
  const progressLabel = document.getElementById("progress-label");
  const convoyRow = document.getElementById("convoy-row");
  const btnLeft = document.getElementById("btn-left");
  const btnRight = document.getElementById("btn-right");
  const btnUp = document.getElementById("btn-up");
  const btnDown = document.getElementById("btn-down");
  const btnAbility = document.getElementById("btn-ability");
  const btnInteract = document.getElementById("btn-interact");
  const btnStart = document.getElementById("btn-start");
  const btnHost = document.getElementById("btn-host");
  const btnCopy = document.getElementById("btn-copy");
  const roomCodeEl = document.getElementById("room-code");
  const inviteCta = document.getElementById("invite-cta");
  const partyStatus = document.getElementById("party-status");
  const inviteQr = document.getElementById("invite-qr");
  const tipEl = document.getElementById("hub-tip");

  const FROG_DEFS = {
    james: { id: "james", name: "James", role: "Wheel", color: "#4ade80", ability: "DASH", cdMax: 6.5 },
    jimmy: { id: "jimmy", name: "Jimmy", role: "Shield", color: "#fb923c", ability: "SHIELD", cdMax: 7.5 },
    bubbles: { id: "bubbles", name: "Bubbles", role: "Zap", color: "#60a5fa", ability: "ZAP", cdMax: 6.5 },
    rexy: { id: "rexy", name: "Rexy", role: "Bot", color: "#c084fc", ability: "BOT", cdMax: 8 },
  };
  const FROG_ORDER = ["james", "jimmy", "bubbles", "rexy"];

  const W = globalThis.FroggiesWorld;

  let selectedId = "james";
  let phase = "title"; // title | hub
  let party = null;
  let partyMeta = { role: "solo", room: null, status: "idle", error: null };
  let lobbySeats = typeof FroggiesParty !== "undefined" ? FroggiesParty.emptySeats() : {};
  let pendingSeatMap = null;
  let isHostSim = true;
  let remoteInputs = {};
  let stateSendAcc = 0;

  let world = null;
  let frogs = [];
  let camX = 400;
  let camY = 450;
  let steerX = 0;
  let steerY = 0;
  let nearHot = null;
  let storyToast = "";
  let storyToastT = 0;
  let lastTs = 0;
  let audioCtx = null;

  let story = null;

  function unlockAudio() {
    if (!audioCtx) {
      try {
        audioCtx = new (window.AudioContext || window.webkitAudioContext)();
      } catch (e) {
        /* ignore */
      }
    }
    if (audioCtx && audioCtx.state === "suspended") audioCtx.resume();
  }

  function beep(freq, dur, type, vol) {
    if (!audioCtx) return;
    try {
      const o = audioCtx.createOscillator();
      const g = audioCtx.createGain();
      o.type = type || "square";
      o.frequency.value = freq;
      g.gain.value = vol || 0.04;
      o.connect(g);
      g.connect(audioCtx.destination);
      o.start();
      g.gain.exponentialRampToValueAtTime(0.001, audioCtx.currentTime + (dur || 0.08));
      o.stop(audioCtx.currentTime + (dur || 0.08) + 0.02);
    } catch (e) {
      /* ignore */
    }
  }

  function resize() {
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    const w = window.innerWidth;
    const h = window.innerHeight;
    canvas.width = Math.floor(w * dpr);
    canvas.height = Math.floor(h * dpr);
    canvas.style.width = w + "px";
    canvas.style.height = h + "px";
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  }

  function localPlayer() {
    return frogs.find((f) => f.local) || frogs.find((f) => f.human) || null;
  }

  function normalizeSeatMap(seatMapOrPlayerId) {
    if (seatMapOrPlayerId && typeof seatMapOrPlayerId === "object") return seatMapOrPlayerId;
    const pid = seatMapOrPlayerId || selectedId || "james";
    selectedId = pid;
    const map = {};
    for (const id of FROG_ORDER) {
      map[id] = { human: id === pid, local: id === pid, peerId: null };
    }
    return map;
  }

  function startHub(seatMapOrPlayerId) {
    unlockAudio();
    if (!W) {
      console.error("FroggiesWorld missing");
      return;
    }
    const seatMap = normalizeSeatMap(seatMapOrPlayerId || pendingSeatMap);
    pendingSeatMap = seatMap;
    const localId = FROG_ORDER.find((id) => seatMap[id] && seatMap[id].human && seatMap[id].local);
    if (localId) selectedId = localId;

    world = W.createWorld();
    W.seedDecor(world);
    frogs = FROG_ORDER.map((id, i) => {
      const seat = seatMap[id] || { human: false, local: false };
      return W.makeFrogEntity(id, !!seat.human, !!seat.local, i);
    });

    const role = party ? party.getRole() : "solo";
    isHostSim = role !== "guest";
    remoteInputs = {};
    stateSendAcc = 0;
    nearHot = null;
    storyToast = "Walk the ranch · find Phone near the house";
    storyToastT = 4;
    if (story) story.reset();

    phase = "hub";
    overlay.hidden = true;
    frogPick.hidden = true;
    if (inviteCta) inviteCta.hidden = true;
    const partyBar = document.getElementById("party-bar");
    if (partyBar) partyBar.hidden = true;
    if (inviteQr) inviteQr.hidden = true;
    document.body.classList.add("in-hub");
    document.body.classList.remove("in-title");
    updateAbilityButton();
    paintHud();
    beep(420, 0.08, "triangle", 0.05);
    beep(560, 0.1, "triangle", 0.05);
  }

  // Party onStart / GO still call this name
  function startRun(seatMapOrPlayerId) {
    startHub(seatMapOrPlayerId);
  }

  function showOverlay(title, text, go, showPick) {
    overlayTitle.textContent = title;
    overlayText.textContent = text;
    overlayGo.textContent = go;
    overlaySub.textContent = "2.5D ranch hub · Up to 4 players · AI fills empty seats";
    frogPick.hidden = !showPick;
    if (inviteCta) inviteCta.hidden = !showPick;
    const partyBar = document.getElementById("party-bar");
    if (partyBar) partyBar.hidden = !showPick;
    const splashEl = document.getElementById("splash-art");
    if (splashEl) splashEl.hidden = !showPick;
    overlay.hidden = false;
    document.body.classList.add("in-title");
    document.body.classList.remove("in-hub");
    const role = party ? party.getRole() : "solo";
    btnStart.textContent = role === "guest" ? "Waiting for host…" : "GO · Ranch Hub";
    btnStart.disabled = role === "guest";
    btnStart.classList.toggle("is-disabled", role === "guest");
    if (showPick) paintLobbySeats();
  }

  function paintHud() {
    const me = localPlayer();
    if (livesEl) livesEl.textContent = me && me.inTruck ? "🚚 Drive" : "🐸 Walk";
    if (scrapEl) {
      const st = story ? story.getState() : {};
      scrapEl.textContent = st.calledPurple ? "SPS: " + (st.planet || "?") : "Explore";
    }
    if (progressBar) progressBar.style.width = me ? "100%" : "0%";
    if (progressLabel) {
      progressLabel.textContent = me ? W.areaNameAt(me.x, me.y) : "Ranch hub";
    }
    if (convoyRow) {
      convoyRow.innerHTML = "";
      for (const f of frogs) {
        const def = FROG_DEFS[f.id];
        const el = document.createElement("span");
        el.className = "badge";
        el.style.borderColor = def.color;
        el.innerHTML =
          def.name +
          (f.human ? "" : '<span class="bot-tag">AI</span>') +
          (f.local ? " · you" : "");
        convoyRow.appendChild(el);
      }
    }
    if (tipEl) {
      if (storyToastT > 0) tipEl.textContent = storyToast;
      else if (nearHot) tipEl.textContent = nearHot.tip + " · tap INTERACT";
      else tipEl.textContent = "";
    }
    if (btnInteract) {
      btnInteract.classList.toggle("ready", !!nearHot);
      btnInteract.disabled = !nearHot && phase === "hub";
    }
  }

  function updateAbilityButton() {
    const me = localPlayer();
    if (!me || !btnAbility) return;
    const def = FROG_DEFS[me.id];
    btnAbility.textContent = me.cd > 0 ? def.ability + " " + Math.ceil(me.cd) + "s" : def.ability;
    btnAbility.classList.toggle("ready", me.cd <= 0);
    btnAbility.classList.toggle("cd", me.cd > 0);
  }

  function requestAbility(frog) {
    if (!frog || frog.cd > 0) return;
    const def = FROG_DEFS[frog.id];
    frog.cd = def.cdMax;
    beep(660, 0.06, "square", 0.05);
    if (frog.id === "james") {
      frog.invuln = 1.2;
      frog.x += frog.facing * 40;
      storyToast = "DASH!";
    } else if (frog.id === "jimmy") {
      storyToast = "SHIELD up (hub stub)";
    } else if (frog.id === "bubbles") {
      storyToast = "ZAP! (hub stub)";
    } else if (frog.id === "rexy") {
      storyToast = "BOT · Optimus assist (hub stub)";
    }
    storyToastT = 1.5;
    updateAbilityButton();
  }

  function doInteract() {
    const me = localPlayer();
    if (!me || !nearHot) return;
    unlockAudio();
    beep(520, 0.07, "triangle", 0.05);
    if (nearHot.id === "phone") {
      if (story) story.openPhone();
      storyToast = "Phone open";
      storyToastT = 2;
    } else if (nearHot.id === "sps") {
      if (story) story.openSps();
      storyToast = "SPS open";
      storyToastT = 2;
    } else if (nearHot.id === "truck") {
      me.inTruck = !me.inTruck;
      storyToast = me.inTruck ? "Driving Cybertruck" : "Parked · walking";
      storyToastT = 2;
    } else if (nearHot.id === "fishies") {
      storyToast = "Fishies splash! (pond play TBD)";
      storyToastT = 2;
      beep(880, 0.05, "sine", 0.04);
    }
    paintHud();
  }

  function packHubState() {
    return {
      mode: "hub",
      frogs: frogs.map((f) => ({
        id: f.id,
        x: f.x,
        y: f.y,
        facing: f.facing,
        inTruck: f.inTruck,
        cd: f.cd,
        human: f.human,
      })),
      phase: phase,
    };
  }

  function applyHubState(msg) {
    if (!msg || !Array.isArray(msg.frogs)) return;
    const byId = {};
    for (const f of frogs) byId[f.id] = f;
    for (const sf of msg.frogs) {
      const f = byId[sf.id];
      if (!f || f.local) continue;
      f.x = sf.x;
      f.y = sf.y;
      f.facing = sf.facing;
      f.inTruck = sf.inTruck;
      f.cd = sf.cd;
    }
  }

  function pushGuestInput(extra) {
    if (!party || party.getRole() !== "guest") return;
    const me = localPlayer();
    if (!me) return;
    party.sendInput(me.id, {
      steerX: steerX,
      steerY: steerY,
      ability: !!(extra && extra.ability),
      interact: !!(extra && extra.interact),
    });
  }

  function updateHub(dt) {
    if (!world || !isHostSim) {
      // Guests still move local frog predictively; host corrects via state
      if (!isHostSim) {
        const me = localPlayer();
        if (me) {
          me.steerX = steerX;
          me.steerY = steerY;
          W.moveEntity(me, dt);
          camX += (me.x - camX) * Math.min(1, dt * 4);
          camY += (me.y - camY) * Math.min(1, dt * 4);
          nearHot = W.nearestHotspot(world, me.x, me.y, 70);
        }
      }
      return;
    }

    const me = localPlayer();
    for (const f of frogs) {
      if (f.local) {
        f.steerX = steerX;
        f.steerY = steerY;
      } else if (f.human) {
        const ri = remoteInputs[f.id];
        if (ri) {
          f.steerX = ri.steerX || 0;
          f.steerY = ri.steerY || 0;
          if (ri.abilityQueued) {
            requestAbility(f);
            ri.abilityQueued = false;
          }
          if (ri.interactQueued) {
            nearHot = W.nearestHotspot(world, f.x, f.y, 70);
            // Only local opens story UI; remote interact is movement-adjacent for now
            ri.interactQueued = false;
          }
        }
      }
    }
    for (const f of frogs) {
      if (f.human) W.moveEntity(f, dt);
    }
    W.tickHubAI(frogs, me, dt);
    W.updateFish(world, dt);

    if (me) {
      camX += (me.x - camX) * Math.min(1, dt * 4);
      camY += (me.y - camY) * Math.min(1, dt * 4);
      nearHot = W.nearestHotspot(world, me.x, me.y, 70);
    }

    if (storyToastT > 0) storyToastT -= dt;

    if (party && party.getRole() === "host") {
      stateSendAcc += dt;
      if (stateSendAcc >= 1 / 12) {
        stateSendAcc = 0;
        if (typeof party.sendState === "function") party.sendState(packHubState());
      }
    }
  }

  function render(t) {
    const w = window.innerWidth;
    const h = window.innerHeight;
    ctx.clearRect(0, 0, w, h);
    if (phase === "hub" && world) {
      W.render(ctx, world, frogs, camX, camY, w, h, t, nearHot);
    } else {
      // Soft title backdrop
      const g = ctx.createLinearGradient(0, 0, 0, h);
      g.addColorStop(0, "#0c1a0c");
      g.addColorStop(1, "#14532d");
      ctx.fillStyle = g;
      ctx.fillRect(0, 0, w, h);
    }
  }

  function tick(now) {
    const dt = Math.min(0.05, (now - (lastTs || now)) / 1000);
    lastTs = now;
    if (phase === "hub") {
      updateHub(dt);
      updateAbilityButton();
      paintHud();
    }
    render(now / 1000);
    requestAnimationFrame(tick);
  }

  // ——— Party lobby UI ———

  function paintLobbySeats() {
    const seats = lobbySeats || {};
    const role = partyMeta.role || "solo";
    document.querySelectorAll(".frog-btn").forEach((btn) => {
      const id = btn.dataset.id;
      const seat = seats[id] || { status: "open" };
      let status = seat.status || "open";
      btn.classList.remove("seat-open", "seat-you", "seat-human", "seat-ai", "seat-taken", "selected");
      const stateEl = btn.querySelector(".seat-state");
      if (status === "you") {
        btn.classList.add("seat-you", "selected");
        if (stateEl) stateEl.textContent = "You";
      } else if (status === "human") {
        btn.classList.add("seat-human", "seat-taken");
        if (stateEl) stateEl.textContent = "Joined";
      } else {
        btn.classList.add("seat-open", "seat-ai");
        if (stateEl) stateEl.textContent = "Open · AI";
      }
    });

    if (partyStatus) {
      partyStatus.classList.toggle("is-error", !!partyMeta.error);
      if (partyMeta.error) partyStatus.textContent = partyMeta.error;
      else if (role === "host" && partyMeta.room)
        partyStatus.textContent = "Hosting · code " + partyMeta.room + " · friends open the invite link";
      else if (role === "guest" && partyMeta.status === "connecting")
        partyStatus.textContent = "Joining room " + (partyMeta.room || "") + "…";
      else if (role === "guest" && partyMeta.status === "ready")
        partyStatus.textContent = "Joined · claim an Open seat · wait for host GO";
      else if (role === "solo") partyStatus.textContent = "Solo · claim a seat or GO (AI fills the rest)";
      else partyStatus.textContent = "";
    }
    if (roomCodeEl) {
      if ((role === "host" || role === "guest") && partyMeta.room) {
        roomCodeEl.hidden = false;
        roomCodeEl.textContent = partyMeta.room;
      } else roomCodeEl.hidden = true;
    }
    if (btnCopy) btnCopy.hidden = !(role === "host" && partyMeta.invite);
    if (btnHost) {
      btnHost.hidden = role === "guest";
      btnHost.textContent = role === "host" ? "Hosting…" : "Host room";
      btnHost.disabled = role === "host" && partyMeta.status === "ready";
    }
    if (inviteQr) {
      if (role === "host" && partyMeta.invite) {
        inviteQr.hidden = false;
        inviteQr.src =
          "https://api.qrserver.com/v1/create-qr-code/?size=112x112&margin=8&data=" +
          encodeURIComponent(partyMeta.invite);
      } else {
        inviteQr.hidden = true;
        inviteQr.removeAttribute("src");
      }
    }
    const canGo = role !== "guest";
    btnStart.disabled = !canGo;
    btnStart.classList.toggle("is-disabled", !canGo);
    btnStart.textContent = role === "guest" ? "Waiting for host…" : "GO · Ranch Hub";
    if (overlayGo && phase === "title") {
      if (role === "guest") overlayGo.textContent = "Claim an Open froggy · host starts the hub";
      else if (role === "host") overlayGo.textContent = "Friends join via link · claim seats · you press GO";
      else overlayGo.textContent = "Claim a seat · Host to invite · or GO solo (AI fills)";
    }
  }

  function bindAxis(btn, axis, val) {
    if (!btn) return;
    const down = (e) => {
      e.preventDefault();
      if (axis === "x") steerX = val;
      else steerY = val;
      unlockAudio();
      pushGuestInput(null);
    };
    const up = (e) => {
      e.preventDefault();
      if (axis === "x" && steerX === val) steerX = 0;
      if (axis === "y" && steerY === val) steerY = 0;
      pushGuestInput(null);
    };
    btn.addEventListener("pointerdown", down);
    btn.addEventListener("pointerup", up);
    btn.addEventListener("pointerleave", up);
    btn.addEventListener("pointercancel", up);
  }

  bindAxis(btnLeft, "x", -1);
  bindAxis(btnRight, "x", 1);
  bindAxis(btnUp, "y", -1);
  bindAxis(btnDown, "y", 1);

  if (btnAbility) {
    btnAbility.addEventListener("pointerdown", (e) => {
      e.preventDefault();
      unlockAudio();
      const player = localPlayer();
      if (!player || phase !== "hub") return;
      if (party && party.getRole() === "guest") {
        pushGuestInput({ ability: true });
        return;
      }
      requestAbility(player);
    });
  }

  if (btnInteract) {
    btnInteract.addEventListener("pointerdown", (e) => {
      e.preventDefault();
      if (phase !== "hub") return;
      if (party && party.getRole() === "guest") {
        pushGuestInput({ interact: true });
        doInteract(); // local UI for guest phone/sps is OK
        return;
      }
      doInteract();
    });
  }

  window.addEventListener("keydown", (e) => {
    if (["ArrowLeft", "a", "A"].includes(e.key)) {
      steerX = -1;
      e.preventDefault();
      pushGuestInput(null);
    }
    if (["ArrowRight", "d", "D"].includes(e.key)) {
      steerX = 1;
      e.preventDefault();
      pushGuestInput(null);
    }
    if (["ArrowUp", "w", "W"].includes(e.key)) {
      steerY = -1;
      e.preventDefault();
      pushGuestInput(null);
    }
    if (["ArrowDown", "s", "S"].includes(e.key)) {
      steerY = 1;
      e.preventDefault();
      pushGuestInput(null);
    }
    if (e.key === " " || e.key === "Enter") {
      e.preventDefault();
      if (phase === "hub") {
        const player = localPlayer();
        if (!player) return;
        if (party && party.getRole() === "guest") pushGuestInput({ ability: true });
        else requestAbility(player);
      } else if (phase === "title") tryStartFromUi();
    }
    if ((e.key === "e" || e.key === "E" || e.key === "f" || e.key === "F") && phase === "hub") {
      doInteract();
    }
    if (e.key === "Escape" && story && story.isOpen()) story.hide();
  });
  window.addEventListener("keyup", (e) => {
    if (["ArrowLeft", "a", "A"].includes(e.key) && steerX < 0) steerX = 0;
    if (["ArrowRight", "d", "D"].includes(e.key) && steerX > 0) steerX = 0;
    if (["ArrowUp", "w", "W"].includes(e.key) && steerY < 0) steerY = 0;
    if (["ArrowDown", "s", "S"].includes(e.key) && steerY > 0) steerY = 0;
    pushGuestInput(null);
  });

  function tryStartFromUi() {
    const role = party ? party.getRole() : "solo";
    if (role === "guest") return;
    if (party) {
      const map = party.startParty();
      if (map) {
        startHub(map);
        return;
      }
    }
    startHub(selectedId);
  }

  document.querySelectorAll(".frog-btn").forEach((btn) => {
    btn.addEventListener("click", () => {
      unlockAudio();
      const id = btn.dataset.id;
      const seat = lobbySeats[id];
      if (seat && seat.status === "human" && seat.peerId && party && seat.peerId !== party.getLocalId()) {
        if (partyStatus) {
          partyStatus.textContent = "That seat is taken";
          partyStatus.classList.add("is-error");
        }
        return;
      }
      selectedId = id;
      if (party) party.claimSeat(id);
      else {
        lobbySeats = typeof FroggiesParty !== "undefined" ? FroggiesParty.emptySeats() : lobbySeats;
        for (const fid of FROG_ORDER) lobbySeats[fid] = { status: "open", peerId: null, label: null };
        lobbySeats[id] = { status: "you", peerId: "local", label: "You" };
        paintLobbySeats();
      }
    });
  });

  btnStart.addEventListener("click", () => {
    unlockAudio();
    if (phase === "title") tryStartFromUi();
  });

  if (btnHost) {
    btnHost.addEventListener("click", () => {
      unlockAudio();
      if (party) party.hostRoom();
    });
  }
  if (btnCopy) {
    btnCopy.addEventListener("click", async () => {
      unlockAudio();
      const url = party && party.inviteUrl();
      if (!url) return;
      try {
        if (navigator.clipboard && navigator.clipboard.writeText) await navigator.clipboard.writeText(url);
        else {
          const ta = document.createElement("textarea");
          ta.value = url;
          document.body.appendChild(ta);
          ta.select();
          document.execCommand("copy");
          ta.remove();
        }
        if (partyStatus) {
          partyStatus.classList.remove("is-error");
          partyStatus.textContent = "Invite link copied!";
        }
      } catch (err) {
        if (partyStatus) {
          partyStatus.textContent = url;
          partyStatus.classList.remove("is-error");
        }
      }
    });
  }

  // Menu / leave hub
  const btnMenu = document.getElementById("btn-menu");
  if (btnMenu) {
    btnMenu.addEventListener("click", () => {
      if (phase !== "hub") return;
      phase = "title";
      if (story) story.hide();
      showOverlay(
        "Four Froggies",
        "Walk / drive the ranch hub · call Purple Bear · open SPS. Party Host QR still works from this lobby.",
        "Claim a seat · Host to invite · or GO solo (AI fills)",
        true
      );
      paintLobbySeats();
    });
  }

  function initParty() {
    if (typeof FroggiesParty === "undefined") {
      lobbySeats = {};
      for (const id of FROG_ORDER) lobbySeats[id] = { status: "open", peerId: null, label: null };
      paintLobbySeats();
      return;
    }
    party = FroggiesParty.createParty({
      onLobby(seats, meta) {
        lobbySeats = seats;
        partyMeta = meta || partyMeta;
        if (phase === "title") paintLobbySeats();
      },
      onStart(seatMap) {
        startHub(seatMap);
      },
      onInput(frogId, payload) {
        if (!isHostSim) return;
        const cur = remoteInputs[frogId] || {
          steerX: 0,
          steerY: 0,
          abilityQueued: false,
          interactQueued: false,
        };
        cur.steerX = payload.steerX || 0;
        cur.steerY = payload.steerY || 0;
        // Back-compat if strip-style steer sent
        if (payload.steer && !payload.steerX) cur.steerX = payload.steer;
        if (payload.ability) cur.abilityQueued = true;
        if (payload.interact) cur.interactQueued = true;
        remoteInputs[frogId] = cur;
      },
      onState(msg) {
        if (isHostSim) return;
        if (msg && (msg.t === "hub" || msg.mode === "hub" || Array.isArray(msg.frogs))) applyHubState(msg);
      },
      onEnd() {
        /* hub has no wipe/clear yet */
      },
      onPeerGone() {
        if (phase === "title") paintLobbySeats();
      },
    });
    party.resetSoloLobby();
    const room = FroggiesParty.parseRoomFromUrl();
    if (room) party.joinRoom(room);
    else paintLobbySeats();
  }

  // Check party.broadcastState exists
  function ensurePartyBroadcast() {
    if (!party) return;
    if (typeof party.broadcastState !== "function" && typeof party.sendState === "function") {
      party.broadcastState = function (s) {
        party.sendState(s);
      };
    }
  }

  if (typeof FroggiesStory !== "undefined") {
    story = FroggiesStory.createStory({
      onCalledPurple(planet) {
        storyToast = "Purple Bear: Jimmy near " + planet;
        storyToastT = 4;
        paintHud();
      },
    });
  }

  window.addEventListener("resize", resize);
  resize();
  initParty();
  ensurePartyBroadcast();
  // Re-hook after party ready
  setTimeout(ensurePartyBroadcast, 100);

  showOverlay(
    "Four Froggies",
    "Walk / drive James's ranch · house, monster truck track, pond. Call Purple Bear · check SPS.",
    "Claim a seat · Host to invite · or GO solo (AI fills)",
    true
  );
  requestAnimationFrame(tick);
})();
