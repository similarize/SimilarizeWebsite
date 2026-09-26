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
  const btnEscape = document.getElementById("btn-escape");
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
  const Space = globalThis.FroggiesSpace;

  let selectedId = "james";
  let phase = "title"; // title | hub | space
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
  let camTX = 400;
  let camTY = 450;
  let steerX = 0;
  let steerY = 0;
  let nearHot = null;
  let prevNearId = null;
  let storyToast = "";
  let storyToastT = 0;
  let lastTs = 0;
  let audioCtx = null;
  let shakeT = 0;

  let story = null;
  let spaceEp = Space ? Space.create() : null;

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

  function sfxJump() {
    beep(180, 0.06, "sawtooth", 0.04);
    beep(320, 0.1, "square", 0.035);
  }
  function sfxLand() {
    beep(90, 0.08, "triangle", 0.05);
  }
  function sfxSplash() {
    beep(520, 0.04, "sine", 0.04);
    beep(280, 0.1, "triangle", 0.045);
    beep(700, 0.05, "sine", 0.03);
  }
  function sfxKit(name) {
    if (name === "rocket" || name === "afterburners") {
      beep(120, 0.08, "sawtooth", 0.05);
      beep(240, 0.12, "sawtooth", 0.04);
    } else if (name === "hover") beep(440, 0.1, "sine", 0.04);
    else if (name === "drone") {
      beep(880, 0.05, "square", 0.03);
      beep(660, 0.08, "square", 0.03);
    } else beep(560, 0.07, "triangle", 0.04);
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
    prevNearId = null;
    storyToast = "Big ranch · Cybertrucks · pond whales · Starship · call Purple Bear";
    storyToastT = 4.5;
    shakeT = 0;
    if (story) story.reset();
    if (spaceEp && Space) {
      Space.exit(spaceEp);
      spaceEp = Space.create();
    }

    const me = frogs.find((f) => f.local);
    if (me) {
      camX = camTX = me.x;
      camY = camTY = me.y;
    }

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
    document.body.classList.remove("in-space");
    if (spaceEp && Space && Space.isActive(spaceEp)) Space.exit(spaceEp);
    const role = party ? party.getRole() : "solo";
    btnStart.textContent = role === "guest" ? "Waiting for host…" : "GO · Ranch Hub";
    btnStart.disabled = role === "guest";
    btnStart.classList.toggle("is-disabled", role === "guest");
    if (showPick) paintLobbySeats();
  }

  function paintHud() {
    const me = localPlayer();
    if (livesEl) {
      if (phase === "space" && spaceEp && spaceEp.inOrbit) {
        livesEl.textContent = "🌍 Orbit";
      } else if (me && me.inTruck) {
        const mode = me.truckMode === "shared" ? "All aboard" : "Drive";
        livesEl.textContent = (me.z || 0) > 4 ? "🚚 AIR!" : "🚚 " + mode;
      } else {
        livesEl.textContent = "🐸 Walk";
      }
    }
    const btnEsc = document.getElementById("btn-escape");
    if (btnEsc) {
      const showEsc = phase === "space" && spaceEp && spaceEp.inOrbit;
      btnEsc.hidden = !showEsc;
      btnEsc.classList.toggle("ready", !!showEsc);
    }
    if (scrapEl) {
      const scrap = world ? world.scrap | 0 : 0;
      const st = story ? story.getState() : {};
      if (st.calledPurple && !st.won) {
        scrapEl.textContent =
          "Scrap " + scrap + " · SPS " + Math.round((st.confidence || 0) * 100) + "%";
      } else if (st.won) {
        scrapEl.textContent = "Scrap " + scrap + " · Jimmy home!";
      } else {
        scrapEl.textContent = "Scrap " + scrap;
      }
    }
    if (progressBar) {
      const scrap = world ? world.scrap : 0;
      const st = story ? story.getState() : {};
      let pct = Math.min(100, scrap);
      if (st.calledPurple) pct = Math.max(pct, (st.confidence || 0) * 100);
      progressBar.style.width = pct + "%";
    }
    if (progressLabel) {
      if (phase === "space" && spaceEp && Space) {
        const hud = Space.getHud(spaceEp);
        progressLabel.textContent = hud ? hud.label : "Space";
      } else {
        progressLabel.textContent = me ? W.areaNameAt(me.x, me.y) : "Ranch hub";
      }
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
      if (phase === "space" && spaceEp && Space) {
        const hud = Space.getHud(spaceEp);
        tipEl.textContent = hud ? hud.tip : "";
        nearHot = hud && hud.near ? hud.near : null;
      } else if (storyToastT > 0) tipEl.textContent = storyToast;
      else if (nearHot) tipEl.textContent = "⚡ " + nearHot.tip + " · INTERACT / E";
      else if (me && me.inTruck && W.onTrack(me.x, me.y))
        tipEl.textContent = "Hit the jumps · scrape for scrap!";
      else tipEl.textContent = "";
    }
    if (btnInteract) {
      btnInteract.classList.toggle("ready", !!nearHot);
      btnInteract.disabled = !nearHot && (phase === "hub" || phase === "space");
    }
    if (livesEl && phase === "space") {
      livesEl.textContent = "🚀 Space";
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
    if (phase === "space" && spaceEp && Space) {
      frog.cd = FROG_DEFS[frog.id].cdMax;
      const res = Space.ability(spaceEp, frog.id);
      beep(660, 0.06, "square", 0.05);
      if (res && res.toast) {
        storyToast = res.toast;
        storyToastT = 2;
      }
      if (res && res.sfx === "blast") shakeT = 0.2;
      if (res && res.sfx === "win") {
        beep(523, 0.1, "triangle", 0.05);
        beep(784, 0.15, "triangle", 0.05);
      }
      updateAbilityButton();
      return;
    }
    const def = FROG_DEFS[frog.id];
    frog.cd = def.cdMax;
    beep(660, 0.06, "square", 0.05);
    if (frog.id === "james") {
      frog.invuln = 1.2;
      frog.dashTrail = 0.45;
      const boost = frog.inTruck ? 70 : 48;
      frog.x += frog.facing * boost;
      if (frog.inTruck) frog.speedBoost = 1.4;
      if (world) {
        W.spawnDust(world, frog.x, frog.y, 8);
        W.spawnSparks(world, frog.x, frog.y, 6);
      }
      shakeT = 0.18;
      storyToast = frog.inTruck ? "DASH · truck boost!" : "DASH!";
      beep(880, 0.05, "sawtooth", 0.035);
    } else if (frog.id === "jimmy") {
      frog.invuln = 2.2;
      storyToast = "SHIELD up!";
      beep(300, 0.12, "triangle", 0.05);
    } else if (frog.id === "bubbles") {
      if (world) W.spawnSparks(world, frog.x, frog.y, 12);
      storyToast = "ZAP!";
      beep(920, 0.04, "square", 0.04);
      beep(1200, 0.05, "square", 0.03);
    } else if (frog.id === "rexy") {
      storyToast = "BOT · open SPS for Optimus kits";
      beep(400, 0.06, "sine", 0.04);
      beep(500, 0.08, "sine", 0.04);
    }
    storyToastT = 1.5;
    updateAbilityButton();
  }

  function enterSpaceEpisode() {
    if (!Space) {
      storyToast = "Space module missing";
      storyToastT = 2;
      return;
    }
    if (!spaceEp) spaceEp = Space.create();
    const me = localPlayer();
    if (me) {
      me.inTruck = false;
      me.truckMode = null;
      me.truckId = null;
    }
    Space.enter(spaceEp, {});
    phase = "space";
    document.body.classList.add("in-space");
    document.body.classList.add("in-hub");
    storyToast = "Spotty welcomes you aboard!";
    storyToastT = 3;
    beep(180, 0.08, "sawtooth", 0.05);
    beep(360, 0.12, "triangle", 0.05);
    beep(540, 0.1, "sine", 0.04);
  }

  function leaveSpaceEpisode(msg) {
    if (spaceEp && Space) Space.exit(spaceEp);
    phase = "hub";
    document.body.classList.remove("in-space");
    const me = localPlayer();
    if (me) {
      me.x = 160;
      me.y = 180;
      camX = camTX = me.x;
      camY = camTY = me.y;
    }
    storyToast = msg || "Back at the ranch hub";
    storyToastT = 3;
    beep(420, 0.08, "triangle", 0.05);
  }

  function doSpaceInteract() {
    if (!spaceEp || !Space) return;
    unlockAudio();
    const res = Space.interact(spaceEp);
    if (!res || !res.ok) return;
    beep(520, 0.07, "triangle", 0.05);
    if (res.exitRanch) {
      leaveSpaceEpisode(res.toast);
      return;
    }
    if (res.sfx === "catch") {
      beep(660, 0.05, "square", 0.04);
      beep(880, 0.06, "square", 0.03);
    } else if (res.sfx === "blast") {
      beep(120, 0.1, "sawtooth", 0.05);
      shakeT = 0.2;
    } else if (res.sfx === "win") {
      beep(523, 0.1, "triangle", 0.05);
      beep(659, 0.12, "triangle", 0.05);
      beep(784, 0.15, "triangle", 0.05);
      shakeT = 0.3;
    } else if (res.sfx === "jet") {
      beep(300, 0.08, "sawtooth", 0.04);
    }
    if (res.toast) {
      storyToast = res.toast;
      storyToastT = 2.5;
    }
    paintHud();
  }

  function doInteract() {
    if (phase === "space") {
      doSpaceInteract();
      return;
    }
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
    } else if (nearHot.kind === "truck" || (nearHot.id && nearHot.id.indexOf("truck") === 0)) {
      const wasIn = !!me.inTruck;
      if (W.boardTruck) W.boardTruck(world, frogs, me, nearHot);
      else {
        me.inTruck = !me.inTruck;
        me.truckMode = me.inTruck ? (nearHot.mode || "solo") : null;
        me.truckId = me.inTruck ? nearHot.id : null;
        if (me.inTruck) { me.x = nearHot.x; me.y = nearHot.y; }
      }
      if (me.inTruck && !wasIn) {
        storyToast = nearHot.mode === "shared"
          ? "All aboard! Four froggies · one Cybertruck · hit the jumps!"
          : "Driving Cybertruck · hit the jumps!";
        beep(200, 0.1, "sawtooth", 0.04);
      } else if (!me.inTruck) {
        storyToast = "Parked · walking";
      }
      storyToastT = 2.5;
    } else if (nearHot.id === "fishies") {
      if (world) W.scareFishies(world, me.x, me.y);
      sfxSplash();
      shakeT = 0.12;
      storyToast = "Splash! Fishies scatter!";
      storyToastT = 2;
      if (world) world.scrap += 3;
    } else if (nearHot.id === "starship") {
      enterSpaceEpisode();
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
        truckMode: f.truckMode || null,
        truckId: f.truckId || null,
        waterSub: f.waterSub || 0,
        wakePhase: f.wakePhase || 0,
        z: f.z || 0,
        cd: f.cd,
        human: f.human,
      })),
      scrap: world ? world.scrap : 0,
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
      f.truckMode = sf.truckMode || null;
      f.truckId = sf.truckId || null;
      f.waterSub = sf.waterSub || 0;
      f.wakePhase = sf.wakePhase || 0;
      f.z = sf.z || 0;
      f.cd = sf.cd;
    }
    if (world && typeof msg.scrap === "number") world.scrap = msg.scrap;
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

  function easeCam(dt) {
    const me = localPlayer();
    if (!me) return;
    // polish3: snappy follow that does not fight — short look-ahead, deadzone settle
    const spd = Math.hypot(me.vx || 0, me.vy || 0);
    const look = spd < 40 ? 0.04 : me.inTruck ? 0.14 : 0.09;
    camTX = me.x + me.vx * look;
    camTY = me.y + me.vy * look - (me.z || 0) * 0.14;
    const follow = me.inTruck ? 7.2 : 6.4;
    const k = 1 - Math.exp(-follow * dt);
    camX += (camTX - camX) * k;
    camY += (camTY - camY) * k;
  }

  function updateHub(dt) {
    if (!world) return;

    if (!isHostSim) {
      const me = localPlayer();
      if (me) {
        me.steerX = steerX;
        me.steerY = steerY;
        W.moveEntity(me, dt);
        const drive = W.tickDrive(world, me, dt);
        if (drive.jumped) {
          sfxJump();
          storyToast = "JUMP! +" + drive.scrapGain + " scrap";
          storyToastT = 1.2;
          shakeT = 0.15;
        } else if (drive.landed) {
          sfxLand();
          if (drive.landShake || drive.splashed) shakeT = Math.max(shakeT, drive.splashed ? 0.12 : 0.08);
          if (drive.splashed) {
            storyToast = (me.waterSub || 0) > 0.7 ? "Under the water!" : "Splash · on the water!";
            storyToastT = 1.1;
          } else if (drive.scrapGain > 0) {
            storyToast = "Landing +" + drive.scrapGain;
            storyToastT = 1;
          }
        } else if (drive.onWater && me.inTruck && Math.hypot(me.vx, me.vy) > 60 && Math.random() < dt * 0.35) {
          storyToast = "Cybertruck · on the water";
          storyToastT = 0.7;
        }
        if (me.speedBoost > 1) me.speedBoost = Math.max(1, me.speedBoost - dt * 0.5);
        easeCam(dt);
        nearHot = W.nearestHotspot(world, me.x, me.y, 70);
        /* polish5: sparkle when entering a hotspot */
        if (nearHot && nearHot.id !== prevNearId) {
          if (W.spawnSparkle) W.spawnSparkle(world, nearHot.x, nearHot.y, 12);
          prevNearId = nearHot.id;
        } else if (!nearHot) {
          prevNearId = null;
        }
      }
      if (storyToastT > 0) storyToastT -= dt;
      if (shakeT > 0) shakeT -= dt;
      W.updateFx(world, dt);
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
            ri.interactQueued = false;
          }
        }
      }
    }
    for (const f of frogs) {
      if (f.human) {
        W.moveEntity(f, dt);
        if (f.local || f.inTruck) {
          const drive = W.tickDrive(world, f, dt);
          if (f.local) {
            if (drive.jumped) {
              sfxJump();
              storyToast =
                "JUMP ×" + world.stuntCombo + " · +" + drive.scrapGain + " scrap";
              storyToastT = 1.3;
              shakeT = 0.16;
            } else if (drive.landed) {
              sfxLand();
              if (drive.landShake || drive.splashed) shakeT = Math.max(shakeT, drive.splashed ? 0.14 : 0.09);
              if (drive.splashed) {
                storyToast = (f.waterSub || 0) > 0.7 ? "Under the water!" : "Splash · on the water!";
                storyToastT = 1.15;
              } else if (drive.scrapGain > 0) {
                storyToast = "Nice air! +" + drive.scrapGain;
                storyToastT = 1.1;
              }
            } else if (drive.onWater && Math.hypot(f.vx, f.vy) > 70 && Math.random() < 0.08) {
              storyToast = "Cybertruck · on the water";
              storyToastT = 0.75;
            } else if (drive.scrapGain > 0 && Math.random() < 0.3) {
              beep(150, 0.03, "sawtooth", 0.025);
            }
          }
        }
        if (f.speedBoost > 1) f.speedBoost = Math.max(1, f.speedBoost - dt * 0.5);
      }
    }
    W.tickHubAI(frogs, me, dt, world);
    W.updateFish(world, dt);
    W.updateFx(world, dt);

    easeCam(dt);
    if (me) nearHot = W.nearestHotspot(world, me.x, me.y, 70);
    if (nearHot && nearHot.id !== prevNearId) {
      if (W.spawnSparkle) W.spawnSparkle(world, nearHot.x, nearHot.y, 12);
      prevNearId = nearHot.id;
    } else if (!nearHot) {
      prevNearId = null;
    }

    if (storyToastT > 0) storyToastT -= dt;
    if (shakeT > 0) shakeT -= dt;

    if (party && party.getRole() === "host") {
      stateSendAcc += dt;
      if (stateSendAcc >= 1 / 12) {
        stateSendAcc = 0;
        if (typeof party.sendState === "function") party.sendState(packHubState());
      }
    }
  }

  function updateSpace(dt) {
    if (!spaceEp || !Space) return;
    if (storyToastT > 0) storyToastT -= dt;
    if (shakeT > 0) shakeT -= dt;
    const me = localPlayer();
    let sx = steerX;
    let sy = steerY;
    if (me && me.cd > 0) me.cd = Math.max(0, me.cd - dt);
    // companion froggies stay on ranch; space is local story path
    Space.update(spaceEp, dt, sx, sy);
    nearHot = Space.nearestHotspot(spaceEp, 75);
  }

  function render(t) {
    const w = window.innerWidth;
    const h = window.innerHeight;
    ctx.clearRect(0, 0, w, h);
    if (phase === "space" && spaceEp && Space) {
      Space.render(ctx, spaceEp, w, h, t);
    } else if (phase === "hub" && world) {
      ctx.save();
      if (shakeT > 0) {
        const mag = shakeT * 10;
        ctx.translate((Math.random() - 0.5) * mag, (Math.random() - 0.5) * mag);
      }
      W.render(ctx, world, frogs, camX, camY, w, h, t, nearHot);
      ctx.restore();
    } else {
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
    } else if (phase === "space") {
      updateSpace(dt);
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
      if (!player || (phase !== "hub" && phase !== "space")) return;
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
      if (phase !== "hub" && phase !== "space") return;
      if (party && party.getRole() === "guest") {
        pushGuestInput({ interact: true });
        doInteract();
        return;
      }
      doInteract();
    });
  }

  if (btnEscape) {
    btnEscape.addEventListener("click", () => {
      if (phase === "space" && spaceEp && Space && spaceEp.inOrbit) {
        const res = Space.leaveOrbit(spaceEp, "escape");
        if (res && res.ok) {
          storyToast = res.toast || "Left orbit";
          storyToastT = 2.2;
          beep(280, 0.08, "sawtooth", 0.04);
          paintHud();
        }
      } else if (globalThis.FroggiesEngines && typeof globalThis.FroggiesEngines.leaveOrbit === "function") {
        globalThis.FroggiesEngines.leaveOrbit();
      }
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
      if (phase === "hub" || phase === "space") {
        const player = localPlayer();
        if (!player) return;
        if (party && party.getRole() === "guest") pushGuestInput({ ability: true });
        else requestAbility(player);
      } else if (phase === "title") tryStartFromUi();
    }
    if ((e.key === "e" || e.key === "E" || e.key === "f" || e.key === "F") && (phase === "hub" || phase === "space")) {
      doInteract();
    }
    if (e.key === "Escape") {
      if (story && story.isOpen()) {
        story.hide();
      } else if (phase === "space" && spaceEp && Space && spaceEp.inOrbit) {
        const res = Space.leaveOrbit(spaceEp, "escape");
        if (res && res.ok) {
          storyToast = res.toast || "Left orbit";
          storyToastT = 2.2;
          beep(280, 0.08, "sawtooth", 0.04);
          paintHud();
        }
      } else if (globalThis.FroggiesEngines && typeof globalThis.FroggiesEngines.leaveOrbit === "function") {
        globalThis.FroggiesEngines.leaveOrbit();
      }
    }
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

  const btnMenu = document.getElementById("btn-menu");
  if (btnMenu) {
    btnMenu.addEventListener("click", () => {
      if (phase !== "hub" && phase !== "space") return;
      if (spaceEp && Space && phase === "space") Space.exit(spaceEp);
      phase = "title";
      if (story) story.hide();
      showOverlay(
        "Four Froggies",
        "Drive the track · splash the pond · call Purple Bear · SPS + Optimus · Starship → space.",
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
        beep(480, 0.08, "sine", 0.05);
      },
      onKit(name) {
        unlockAudio();
        sfxKit(name);
      },
      onBeep(name) {
        unlockAudio();
        sfxKit(name);
      },
      onWin(planet) {
        storyToast = "Jimmy home from " + planet + "!";
        storyToastT = 5;
        if (world) world.scrap += 50;
        beep(523, 0.1, "triangle", 0.05);
        beep(659, 0.12, "triangle", 0.05);
        beep(784, 0.16, "triangle", 0.05);
        paintHud();
      },
      onSoftFail(planet) {
        storyToast = "Jimmy's on " + planet + " again…";
        storyToastT = 4;
        beep(200, 0.15, "sawtooth", 0.04);
        paintHud();
      },
      onRetry(planet) {
        storyToast = "Retry — Jimmy near " + planet;
        storyToastT = 3;
        paintHud();
      },
    });
  }

  window.addEventListener("resize", resize);
  resize();
  initParty();
  ensurePartyBroadcast();
  setTimeout(ensurePartyBroadcast, 100);

  showOverlay(
    "Four Froggies",
    "Drive the track · splash the pond · call Purple Bear · SPS + Optimus · bring Jimmy home · Starship → space episode.",
    "Claim a seat · Host to invite · or GO solo (AI fills)",
    true
  );
  requestAnimationFrame(tick);
})();
