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
  const btnAbility = document.getElementById("btn-ability");
  const btnStart = document.getElementById("btn-start");

  const FROG_DEFS = {
    james:  { id: "james",  name: "James",  role: "Wheel", color: "#4ade80", accent: "#166534", ability: "DASH",  cdMax: 6.5 },
    jimmy: { id: "jimmy", name: "Jimmy", role: "Shield", color: "#fb923c", accent: "#9a3412", ability: "SHIELD", cdMax: 7.5 },
    bubbles:  { id: "bubbles",  name: "Bubbles",  role: "Zap", color: "#60a5fa", accent: "#1e3a8a", ability: "ZAP", cdMax: 6.5 },
    rexy:  { id: "rexy",  name: "Rexy",  role: "Bot", color: "#c084fc", accent: "#6b21a8", ability: "BOT", cdMax: 8 },
  };
  const FROG_ORDER = ["james", "jimmy", "bubbles", "rexy"];

  const LANE_COUNT = 4;
  const STRIP_LEN = 4200; // world units to clear
  const SHARED_LIVES = 3;

  let w = 1, h = 1, dpr = 1;
  let last = performance.now();
  let phase = "title"; // title | play | clear | wipe
  let cameraX = 0;
  let worldScroll = 0;
  let lives = SHARED_LIVES;
  let scrap = 0;
  let invadersCleared = 0;
  let selectedId = "james";
  let frogs = [];
  let hazards = [];
  let invaders = [];
  let particles = [];
  let fx = [];
  let landmarks = [];
  let botAssist = null;
  let shieldTimer = 0;
  let hitFlash = 0;
  let cheerTimer = 0;
  let audio = null;
  let steerHeld = 0; // -1 left, 1 right
  let steerRepeat = 0;
  let swipeStartX = null;

  // Grok Imagine art (only 2 images — phone-friendly)
  const art = {
    splash: null,   // assets/splash-forest-roof.png
    backdrop: null, // assets/backdrop-overhead.png
  };
  const BILLBOARD_XS = [620, 1550, 2600, 3500];

  function loadArt() {
    function loadOne(key, src) {
      const img = new Image();
      img.decoding = "async";
      img.onload = () => { art[key] = img; };
      img.onerror = () => { console.warn("Art failed:", src); };
      img.src = src;
    }
    loadOne("splash", "assets/splash-forest-roof.png?v=20260925-names2");
    loadOne("backdrop", "assets/backdrop-overhead.png?v=20260925-names2");
  }

  /** Cover-draw an image into a screen rect (center crop). */
  function drawImageCover(img, dx, dy, dw, dh, srcBiasY) {
    if (!img || !img.complete || !img.naturalWidth) return false;
    const iw = img.naturalWidth;
    const ih = img.naturalHeight;
    const scale = Math.max(dw / iw, dh / ih);
    const sw = dw / scale;
    const sh = dh / scale;
    const bias = srcBiasY == null ? 0.35 : srcBiasY;
    const sx = (iw - sw) * 0.5;
    const sy = Math.max(0, Math.min(ih - sh, (ih - sh) * bias));
    ctx.drawImage(img, sx, sy, sw, sh, dx, dy, dw, dh);
    return true;
  }

  function unlockAudio() {
    if (audio) return;
    try {
      audio = new (window.AudioContext || window.webkitAudioContext)();
      if (audio.state === "suspended") audio.resume();
    } catch (e) {
      audio = null;
    }
  }

  function beep(freq, dur, type, vol) {
    if (!audio) return;
    const t = audio.currentTime;
    const o = audio.createOscillator();
    const g = audio.createGain();
    o.type = type || "square";
    o.frequency.value = freq;
    g.gain.setValueAtTime(vol || 0.05, t);
    g.gain.exponentialRampToValueAtTime(0.001, t + dur);
    o.connect(g);
    g.connect(audio.destination);
    o.start(t);
    o.stop(t + dur);
  }

  function resize() {
    dpr = Math.min(2, window.devicePixelRatio || 1);
    w = Math.max(1, window.innerWidth);
    h = Math.max(1, window.innerHeight);
    canvas.width = Math.floor(w * dpr);
    canvas.height = Math.floor(h * dpr);
    canvas.style.width = w + "px";
    canvas.style.height = h + "px";
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  }

  function laneY(lane) {
    const roadTop = h * 0.32;
    const roadH = h * 0.48;
    const laneH = roadH / LANE_COUNT;
    return roadTop + laneH * (lane + 0.5);
  }

  function roadMetrics() {
    return { top: h * 0.32, height: h * 0.48, bottom: h * 0.32 + h * 0.48 };
  }

  function makeFrog(id, human, slot) {
    const def = FROG_DEFS[id] || FROG_DEFS.james || FROG_DEFS[FROG_ORDER[0]];
    return {
      ...def,
      human,
      slot,
      lane: slot % LANE_COUNT,
      targetLane: slot % LANE_COUNT,
      x: 120 + slot * 70,
      y: 0,
      steer: 0,
      cd: 0,
      invuln: 0,
      dashing: 0,
      aiTimer: Math.random() * 0.3,
      alive: true,
    };
  }

  function buildLandmarks() {
    landmarks = [
      { x: 280, kind: "house", label: "Ranch House" },
      { x: 1100, kind: "track", label: "Monster Truck Track" },
      { x: 2100, kind: "pond", label: "Pond + Fishies" },
      { x: 3200, kind: "house", label: "Barn Shed" },
      { x: 3800, kind: "track", label: "Dirt Loop" },
    ];
  }

  function spawnStripThreats() {
    hazards = [];
    invaders = [];
    // Hazards: rocks / mud / toys clutter on lanes (first ~3–4s clear)
    for (let i = 0; i < 18; i++) {
      const x = 920 + i * 175 + Math.random() * 80;
      if (x > STRIP_LEN - 200) continue;
      hazards.push({
        x,
        lane: Math.floor(Math.random() * LANE_COUNT),
        kind: Math.random() < 0.5 ? "rock" : "mud",
        r: 18 + Math.random() * 8,
        hit: false,
      });
    }
    // Invaders: one type — purple saucers
    for (let i = 0; i < 9; i++) {
      const x = 1100 + i * 340 + Math.random() * 100;
      if (x > STRIP_LEN - 250) continue;
      invaders.push({
        x,
        lane: Math.floor(Math.random() * LANE_COUNT),
        yOff: (Math.random() - 0.5) * 10,
        hp: 1,
        bob: Math.random() * Math.PI * 2,
        r: 22,
      });
    }
  }

  function startRun(playerId) {
    unlockAudio();
    selectedId = playerId || selectedId || "james";
    const others = FROG_ORDER.filter((id) => id !== selectedId);
    frogs = [
      makeFrog(selectedId, true, 1),
      makeFrog(others[0], false, 0),
      makeFrog(others[1], false, 2),
      makeFrog(others[2], false, 3),
    ];
    // Stagger starting lanes + brief convoy invuln so first seconds are playable
    frogs.forEach((f, i) => {
      f.lane = i % LANE_COUNT;
      f.targetLane = f.lane;
      f.x = 90 + i * 55;
      f.invuln = 2;
    });
    lives = SHARED_LIVES;
    scrap = 0;
    invadersCleared = 0;
    worldScroll = 0;
    cameraX = 0;
    particles = [];
    fx = [];
    botAssist = null;
    shieldTimer = 0;
    hitFlash = 0;
    cheerTimer = 0;
    buildLandmarks();
    spawnStripThreats();
    phase = "play";
    overlay.hidden = true;
    frogPick.hidden = true;
    updateAbilityButton();
    paintHud();
    beep(420, 0.08, "triangle", 0.05);
    beep(560, 0.1, "triangle", 0.05);
  }

  function showOverlay(title, text, go, showPick) {
    overlayTitle.textContent = title;
    overlayText.textContent = text;
    overlayGo.textContent = go;
    overlaySub.textContent = "Ranch Grounds · Solo with AI buddies";
    frogPick.hidden = !showPick;
    const splashEl = document.getElementById("splash-art");
    if (splashEl) splashEl.hidden = !showPick; // title only — keep retry overlay light
    overlay.hidden = false;
    btnStart.textContent = showPick ? "GO · Ranch Run" : "RETRY · Instant";
  }

  function paintHud() {
    livesEl.textContent = "♥".repeat(Math.max(0, lives)) + "♡".repeat(Math.max(0, SHARED_LIVES - lives));
    scrapEl.textContent = "Scrap " + scrap;
    const pct = Math.min(100, (worldScroll / STRIP_LEN) * 100);
    progressBar.style.width = pct.toFixed(1) + "%";
    let zone = "Leaving the gate…";
    if (worldScroll > 500) zone = "Past the ranch house";
    if (worldScroll > 1000) zone = "Monster truck track";
    if (worldScroll > 1900) zone = "Pond + fishies";
    if (worldScroll > 3000) zone = "Back stretch";
    if (worldScroll >= STRIP_LEN) zone = "Ranch clear!";
    progressLabel.textContent = zone;

    convoyRow.innerHTML = "";
    for (const f of frogs) {
      const el = document.createElement("span");
      el.className = "badge";
      el.style.borderColor = f.color;
      el.style.color = f.color;
      el.textContent = f.name + (f.human ? " ★" : "");
      if (!f.human) {
        const tag = document.createElement("span");
        tag.className = "bot-tag";
        tag.textContent = "AI";
        el.appendChild(tag);
      }
      convoyRow.appendChild(el);
    }
  }

  function updateAbilityButton() {
    const player = frogs.find((f) => f.human);
    if (!player) return;
    btnAbility.textContent = player.ability + (player.cd > 0 ? "\n" + Math.ceil(player.cd) + "s" : "");
    btnAbility.classList.toggle("ready", player.cd <= 0);
    btnAbility.classList.toggle("cd", player.cd > 0);
    btnAbility.style.borderColor = player.color;
  }

  function burst(x, y, n, color) {
    for (let i = 0; i < n; i++) {
      const a = Math.random() * Math.PI * 2;
      const s = 40 + Math.random() * 140;
      particles.push({
        x, y,
        vx: Math.cos(a) * s,
        vy: Math.sin(a) * s,
        life: 0.4 + Math.random() * 0.3,
        color,
      });
    }
  }

  function loseLife(atX, atY) {
    if (shieldTimer > 0) {
      shieldTimer = 0;
      burst(atX, atY, 12, "#fb923c");
      beep(300, 0.12, "triangle", 0.06);
      fx.push({ kind: "text", text: "SHIELD!", x: atX, y: atY - 30, life: 0.8, color: "#fb923c" });
      return;
    }
    lives -= 1;
    hitFlash = 0.35;
    burst(atX, atY, 18, "#f87171");
    beep(90, 0.28, "sawtooth", 0.08);
    for (const f of frogs) f.invuln = Math.max(f.invuln, 1.2);
    paintHud();
    if (lives <= 0) {
      phase = "wipe";
      showOverlay(
        "Convoy wipe!",
        "Scrap " + scrap + " · invaders " + invadersCleared + ". Funny crash — tap to retry.",
        "Instant retry — no long menus",
        false
      );
      beep(70, 0.4, "sawtooth", 0.09);
    }
  }

  function clearStage() {
    phase = "clear";
    cheerTimer = 2.5;
    const bonus = frogs.every((f) => f.alive) ? 50 : 0;
    scrap += bonus;
    showOverlay(
      "Ranch clear! GO!",
      "Scrap " + scrap + " · invaders " + invadersCleared + (bonus ? " · no frog left behind +50" : ""),
      "Tap to run the strip again",
      false
    );
    beep(520, 0.1, "triangle", 0.06);
    beep(660, 0.12, "triangle", 0.06);
    beep(880, 0.18, "triangle", 0.07);
    paintHud();
  }

  function requestAbility(frog) {
    if (!frog || frog.cd > 0 || phase !== "play") return;
    frog.cd = frog.cdMax;
    const wx = frog.x;
    const wy = laneY(frog.lane);

    if (frog.id === "james") {
      // Lane-swap dash + short invuln
      const dest = (frog.lane + (Math.random() < 0.5 ? 1 : -1) + LANE_COUNT) % LANE_COUNT;
      // Prefer empty-ish lane
      let best = frog.lane;
      let bestD = -1;
      for (let L = 0; L < LANE_COUNT; L++) {
        if (L === frog.lane) continue;
        let clear = 200;
        for (const hz of hazards) {
          if (hz.lane === L && hz.x > frog.x && hz.x < frog.x + 220) clear = Math.min(clear, hz.x - frog.x);
        }
        if (clear > bestD) { bestD = clear; best = L; }
      }
      frog.targetLane = best;
      frog.lane = best;
      frog.dashing = 0.35;
      frog.invuln = Math.max(frog.invuln, 0.9);
      burst(wx, wy, 10, frog.color);
      fx.push({ kind: "text", text: "DASH!", x: wx, y: wy - 28, life: 0.6, color: frog.color });
      beep(700, 0.08, "square", 0.05);
    } else if (frog.id === "jimmy") {
      shieldTimer = 2.8;
      fx.push({ kind: "text", text: "SHIELD!", x: cameraX + w * 0.4, y: h * 0.28, life: 0.9, color: frog.color });
      beep(240, 0.15, "sine", 0.06);
    } else if (frog.id === "bubbles") {
      // Clear nearest invader cluster
      const sorted = invaders
        .map((inv, i) => ({ inv, i, d: Math.abs(inv.x - frog.x) }))
        .sort((a, b) => a.d - b.d);
      let cleared = 0;
      for (const item of sorted) {
        if (item.d > 320) break;
        if (cleared >= 3) break;
        const inv = item.inv;
        burst(inv.x, laneY(inv.lane) + inv.yOff, 14, "#60a5fa");
        scrap += 25;
        invadersCleared += 1;
        invaders.splice(invaders.indexOf(inv), 1);
        cleared++;
      }
      fx.push({ kind: "zap", x: frog.x + 80, y: laneY(frog.lane), life: 0.35 });
      fx.push({ kind: "text", text: "ZAP!", x: wx, y: wy - 28, life: 0.6, color: frog.color });
      beep(880, 0.1, "sawtooth", 0.05);
      paintHud();
    } else if (frog.id === "rexy") {
      botAssist = {
        x: frog.x + 40,
        y: laneY(frog.lane),
        life: 3.2,
        lane: frog.lane,
      };
      fx.push({ kind: "text", text: "BOT!", x: wx, y: wy - 28, life: 0.6, color: frog.color });
      beep(180, 0.12, "triangle", 0.06);
      beep(320, 0.1, "triangle", 0.05);
    }
    updateAbilityButton();
  }

  // Expose for AI module
  function getAIState() {
    return {
      hazards,
      invaders,
      lives,
      laneCount: LANE_COUNT,
      requestAbility,
    };
  }

  function moveFrogs(dt) {
    const speed = 210; // world units / sec
    worldScroll += speed * dt;
    cameraX = worldScroll;
    if (steerRepeat > 0) steerRepeat = Math.max(0, steerRepeat - dt);

    for (const f of frogs) {
      if (f.cd > 0) f.cd = Math.max(0, f.cd - dt);
      if (f.invuln > 0) f.invuln = Math.max(0, f.invuln - dt);
      if (f.dashing > 0) f.dashing = Math.max(0, f.dashing - dt);

      if (f.human) {
        f.steer = steerHeld;
        // Lane change only when settled on a lane (tap or hold-repeat)
        if (f.lane === f.targetLane && Math.abs((f.y || laneY(f.lane)) - laneY(f.lane)) < 5) {
          if (steerHeld !== 0) {
            if (steerRepeat <= 0) {
              f.targetLane = Math.max(0, Math.min(LANE_COUNT - 1, f.lane + steerHeld));
              steerRepeat = 0.28;
            }
          } else {
            steerRepeat = 0;
          }
        }
      }

      // Smooth lane lerp conceptually: snap when close via moving target
      const targetY = laneY(f.targetLane);
      f.y = f.y || laneY(f.lane);
      const dy = targetY - f.y;
      f.y += Math.sign(dy) * Math.min(Math.abs(dy), 280 * dt);
      if (Math.abs(dy) < 4) {
        f.y = targetY;
        f.lane = f.targetLane;
      }

      // Keep formation X relative to camera
      const base = cameraX + 100 + f.slot * 58;
      f.x += (base - f.x) * Math.min(1, 4 * dt);
      if (f.dashing > 0) f.x += 90 * dt;
    }

    if (shieldTimer > 0) shieldTimer = Math.max(0, shieldTimer - dt);

    if (botAssist) {
      botAssist.life -= dt;
      botAssist.x += 260 * dt;
      // Bot clears hazards / damages invaders nearby
      for (let i = hazards.length - 1; i >= 0; i--) {
        const hz = hazards[i];
        if (Math.abs(hz.x - botAssist.x) < 40 && Math.abs(hz.lane - botAssist.lane) <= 1) {
          burst(hz.x, laneY(hz.lane), 8, "#c084fc");
          scrap += 5;
          hazards.splice(i, 1);
        }
      }
      for (let i = invaders.length - 1; i >= 0; i--) {
        const inv = invaders[i];
        if (Math.abs(inv.x - botAssist.x) < 50) {
          burst(inv.x, laneY(inv.lane), 12, "#c084fc");
          scrap += 20;
          invadersCleared += 1;
          invaders.splice(i, 1);
        }
      }
      if (botAssist.life <= 0) botAssist = null;
      paintHud();
    }
  }

  function collide(dt) {
    for (const f of frogs) {
      if (f.invuln > 0 || f.dashing > 0) continue;
      // AI frogs dodge for flavor but do not drain shared lives
      if (!f.human) continue;
      for (const hz of hazards) {
        if (hz.hit) continue;
        if (hz.lane !== f.lane) continue;
        if (Math.abs(hz.x - f.x) < hz.r + 28) {
          hz.hit = true;
          loseLife(f.x, f.y || laneY(f.lane));
          if (phase !== "play") return;
        }
      }
      for (const inv of invaders) {
        if (inv.lane !== f.lane) continue;
        if (Math.abs(inv.x - f.x) < inv.r + 24) {
          // bump — remove invader and hurt (human only)
          burst(inv.x, laneY(inv.lane), 10, "#a78bfa");
          invaders.splice(invaders.indexOf(inv), 1);
          loseLife(f.x, f.y || laneY(f.lane));
          if (phase !== "play") return;
          break;
        }
      }
    }
  }

  function updateParticles(dt) {
    for (let i = particles.length - 1; i >= 0; i--) {
      const p = particles[i];
      p.life -= dt;
      p.x += p.vx * dt;
      p.y += p.vy * dt;
      if (p.life <= 0) particles.splice(i, 1);
    }
    for (let i = fx.length - 1; i >= 0; i--) {
      fx[i].life -= dt;
      if (fx[i].life <= 0) fx.splice(i, 1);
    }
    if (hitFlash > 0) hitFlash = Math.max(0, hitFlash - dt);
  }

  // ——— Drawing ———

  function worldToScreenX(wx) {
    return wx - cameraX;
  }

  function drawSky() {
    const roadTop = roadMetrics().top;
    const skyH = Math.max(1, roadTop);

    // Fallback gradient
    const g = ctx.createLinearGradient(0, 0, 0, h);
    g.addColorStop(0, "#7dd3fc");
    g.addColorStop(0.45, "#bae6fd");
    g.addColorStop(0.55, "#86efac");
    g.addColorStop(1, "#166534");
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, w, h);

    // Parallax Imagine forest backdrop (scrolls slower than road)
    if (art.backdrop) {
      const parallax = (cameraX * 0.22) % w;
      // Two tiles for seamless horizontal scroll
      for (const ox of [-parallax, -parallax + w]) {
        ctx.save();
        ctx.globalAlpha = 0.92;
        drawImageCover(art.backdrop, ox, 0, w, skyH + 8, 0.28);
        ctx.restore();
      }
      // Soft fade into road
      const fade = ctx.createLinearGradient(0, skyH - 28, 0, skyH + 6);
      fade.addColorStop(0, "rgba(34,197,94,0)");
      fade.addColorStop(1, "rgba(22,101,52,0.55)");
      ctx.fillStyle = fade;
      ctx.fillRect(0, skyH - 28, w, 40);
    } else {
      ctx.fillStyle = "#fde047";
      ctx.beginPath();
      ctx.arc(w * 0.82, h * 0.12, 28, 0, Math.PI * 2);
      ctx.fill();
    }
  }

  function drawArtBillboards() {
    if (!art.backdrop) return;
    const { top } = roadMetrics();
    for (const bx of BILLBOARD_XS) {
      const sx = worldToScreenX(bx);
      const bw = Math.min(150, w * 0.28);
      const bh = bw * 1.25;
      if (sx < -bw - 20 || sx > w + 20) continue;
      const x = sx - bw / 2;
      const y = top - bh - 18;
      // Frame
      ctx.fillStyle = "rgba(0,0,0,.35)";
      ctx.fillRect(x - 4, y - 4, bw + 8, bh + 8);
      ctx.strokeStyle = "rgba(255,255,255,.55)";
      ctx.lineWidth = 2;
      ctx.strokeRect(x - 4, y - 4, bw + 8, bh + 8);
      drawImageCover(art.backdrop, x, y, bw, bh, 0.4);
      ctx.font = "700 10px Segoe UI, system-ui, sans-serif";
      ctx.fillStyle = "rgba(0,0,0,.5)";
      ctx.fillRect(x, y + bh + 4, bw, 16);
      ctx.fillStyle = "#e7e5e4";
      ctx.textAlign = "center";
      ctx.fillText("Trail dressing", sx, y + bh + 15);
      ctx.textAlign = "left";
    }
  }

  function drawRoad() {
    const { top, height, bottom } = roadMetrics();
    ctx.fillStyle = "#57534e";
    ctx.fillRect(0, top, w, height);
    ctx.fillStyle = "#78716c";
    ctx.fillRect(0, top, w, 6);
    ctx.fillRect(0, bottom - 6, w, 6);

    // Lane dashes scrolling
    ctx.strokeStyle = "rgba(255,255,255,.35)";
    ctx.lineWidth = 2;
    ctx.setLineDash([18, 16]);
    ctx.lineDashOffset = -(cameraX * 0.4) % 34;
    for (let i = 1; i < LANE_COUNT; i++) {
      const y = top + (height / LANE_COUNT) * i;
      ctx.beginPath();
      ctx.moveTo(0, y);
      ctx.lineTo(w, y);
      ctx.stroke();
    }
    ctx.setLineDash([]);

    // Grass edges
    ctx.fillStyle = "#15803d";
    ctx.fillRect(0, bottom, w, h - bottom);
    ctx.fillStyle = "#22c55e";
    ctx.fillRect(0, top - 18, w, 18);
  }

  function drawLandmark(lm) {
    const sx = worldToScreenX(lm.x);
    if (sx < -200 || sx > w + 200) return;
    const { top } = roadMetrics();
    const baseY = top - 8;

    if (lm.kind === "house") {
      // Simple ranch house
      ctx.fillStyle = "#fef3c7";
      ctx.fillRect(sx - 50, baseY - 70, 100, 70);
      ctx.fillStyle = "#b45309";
      ctx.beginPath();
      ctx.moveTo(sx - 60, baseY - 70);
      ctx.lineTo(sx, baseY - 110);
      ctx.lineTo(sx + 60, baseY - 70);
      ctx.closePath();
      ctx.fill();
      ctx.fillStyle = "#7c2d12";
      ctx.fillRect(sx - 12, baseY - 36, 24, 36);
      ctx.fillStyle = "#38bdf8";
      ctx.fillRect(sx - 38, baseY - 52, 18, 16);
      ctx.fillRect(sx + 20, baseY - 52, 18, 16);
      // Porch
      ctx.fillStyle = "#a8a29e";
      ctx.fillRect(sx - 55, baseY - 8, 110, 8);
    } else if (lm.kind === "track") {
      // Monster truck track — dirt oval cue
      ctx.fillStyle = "#a16207";
      ctx.beginPath();
      ctx.ellipse(sx, baseY - 36, 70, 28, 0, 0, Math.PI * 2);
      ctx.fill();
      ctx.strokeStyle = "#713f12";
      ctx.lineWidth = 4;
      ctx.beginPath();
      ctx.ellipse(sx, baseY - 36, 48, 16, 0, 0, Math.PI * 2);
      ctx.stroke();
      // Ramp
      ctx.fillStyle = "#854d0e";
      ctx.beginPath();
      ctx.moveTo(sx - 90, baseY);
      ctx.lineTo(sx - 40, baseY - 40);
      ctx.lineTo(sx - 20, baseY);
      ctx.closePath();
      ctx.fill();
    } else if (lm.kind === "pond") {
      ctx.fillStyle = "#0ea5e9";
      ctx.beginPath();
      ctx.ellipse(sx, baseY - 30, 75, 32, 0, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = "#38bdf8";
      ctx.beginPath();
      ctx.ellipse(sx - 10, baseY - 36, 40, 14, 0, 0, Math.PI * 2);
      ctx.fill();
      // Fishies
      for (let i = 0; i < 4; i++) {
        const fx = sx - 40 + i * 28;
        const fy = baseY - 28 + Math.sin(performance.now() / 400 + i) * 4;
        ctx.fillStyle = i % 2 ? "#f97316" : "#eab308";
        ctx.beginPath();
        ctx.ellipse(fx, fy, 8, 4, 0, 0, Math.PI * 2);
        ctx.fill();
        ctx.beginPath();
        ctx.moveTo(fx + 8, fy);
        ctx.lineTo(fx + 14, fy - 4);
        ctx.lineTo(fx + 14, fy + 4);
        ctx.closePath();
        ctx.fill();
      }
    }

    // Label
    ctx.font = "700 13px Segoe UI, system-ui, sans-serif";
    ctx.fillStyle = "rgba(0,0,0,.55)";
    const tw = ctx.measureText(lm.label).width;
    ctx.fillRect(sx - tw / 2 - 6, baseY - 128, tw + 12, 20);
    ctx.fillStyle = "#fff";
    ctx.textAlign = "center";
    ctx.fillText(lm.label, sx, baseY - 114);
    ctx.textAlign = "left";
  }

  function drawCybertruck(f) {
    const x = worldToScreenX(f.x);
    const y = f.y || laneY(f.lane);
    if (x < -80 || x > w + 80) return;

    ctx.save();
    if (f.invuln > 0 && Math.floor(f.invuln * 12) % 2 === 0) ctx.globalAlpha = 0.45;
    if (f.dashing > 0) {
      ctx.globalAlpha = 0.85;
      ctx.fillStyle = f.color;
      ctx.fillRect(x - 50, y - 6, 30, 8);
    }

    // Shadow
    ctx.fillStyle = "rgba(0,0,0,.25)";
    ctx.beginPath();
    ctx.ellipse(x, y + 18, 36, 8, 0, 0, Math.PI * 2);
    ctx.fill();

    // Body — angular Cybertruck silhouette
    ctx.fillStyle = "#d6d3d1";
    ctx.beginPath();
    ctx.moveTo(x - 38, y + 10);
    ctx.lineTo(x - 34, y - 6);
    ctx.lineTo(x + 8, y - 16);
    ctx.lineTo(x + 40, y - 8);
    ctx.lineTo(x + 42, y + 10);
    ctx.closePath();
    ctx.fill();
    ctx.strokeStyle = "#44403c";
    ctx.lineWidth = 2;
    ctx.stroke();

    // Cabin glass
    ctx.fillStyle = "#0ea5e9";
    ctx.beginPath();
    ctx.moveTo(x - 8, y - 6);
    ctx.lineTo(x + 6, y - 14);
    ctx.lineTo(x + 22, y - 8);
    ctx.lineTo(x + 18, y - 2);
    ctx.closePath();
    ctx.fill();

    // Wheels
    ctx.fillStyle = "#1c1917";
    ctx.beginPath();
    ctx.arc(x - 22, y + 12, 9, 0, Math.PI * 2);
    ctx.arc(x + 24, y + 12, 9, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = "#a8a29e";
    ctx.beginPath();
    ctx.arc(x - 22, y + 12, 3, 0, Math.PI * 2);
    ctx.arc(x + 24, y + 12, 3, 0, Math.PI * 2);
    ctx.fill();

    // Froggy driver
    ctx.fillStyle = f.color;
    ctx.beginPath();
    ctx.arc(x + 4, y - 20, 11, 0, Math.PI * 2);
    ctx.fill();
    // Eyes
    ctx.fillStyle = "#fff";
    ctx.beginPath();
    ctx.arc(x + 1, y - 22, 3.5, 0, Math.PI * 2);
    ctx.arc(x + 9, y - 22, 3.5, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = "#111";
    ctx.beginPath();
    ctx.arc(x + 2, y - 22, 1.5, 0, Math.PI * 2);
    ctx.arc(x + 10, y - 22, 1.5, 0, Math.PI * 2);
    ctx.fill();

    // Name plate
    ctx.font = "700 11px Segoe UI, system-ui, sans-serif";
    ctx.fillStyle = "rgba(0,0,0,.5)";
    const label = f.name + (f.human ? "" : " AI");
    const tw = ctx.measureText(label).width;
    ctx.fillRect(x - tw / 2 - 4, y + 24, tw + 8, 14);
    ctx.fillStyle = f.color;
    ctx.textAlign = "center";
    ctx.fillText(label, x, y + 35);
    ctx.textAlign = "left";

    ctx.restore();
  }

  function drawHazard(hz) {
    const sx = worldToScreenX(hz.x);
    if (sx < -40 || sx > w + 40) return;
    const y = laneY(hz.lane);
    if (hz.kind === "rock") {
      ctx.fillStyle = "#78716c";
      ctx.beginPath();
      ctx.moveTo(sx - hz.r, y + 8);
      ctx.lineTo(sx - hz.r * 0.4, y - hz.r);
      ctx.lineTo(sx + hz.r * 0.6, y - hz.r * 0.7);
      ctx.lineTo(sx + hz.r, y + 8);
      ctx.closePath();
      ctx.fill();
    } else {
      ctx.fillStyle = "#854d0e";
      ctx.beginPath();
      ctx.ellipse(sx, y + 6, hz.r, hz.r * 0.45, 0, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = "rgba(0,0,0,.2)";
      ctx.beginPath();
      ctx.ellipse(sx, y + 6, hz.r * 0.6, hz.r * 0.25, 0, 0, Math.PI * 2);
      ctx.fill();
    }
  }

  function drawInvader(inv, t) {
    const sx = worldToScreenX(inv.x);
    if (sx < -50 || sx > w + 50) return;
    const y = laneY(inv.lane) + inv.yOff + Math.sin(t * 3 + inv.bob) * 5;
    // Saucer
    ctx.fillStyle = "#7c3aed";
    ctx.beginPath();
    ctx.ellipse(sx, y, 26, 10, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = "#c4b5fd";
    ctx.beginPath();
    ctx.ellipse(sx, y - 6, 14, 10, 0, Math.PI, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = "#f0abfc";
    ctx.beginPath();
    ctx.arc(sx - 10, y + 2, 3, 0, Math.PI * 2);
    ctx.arc(sx, y + 3, 3, 0, Math.PI * 2);
    ctx.arc(sx + 10, y + 2, 3, 0, Math.PI * 2);
    ctx.fill();
    // Label once
    ctx.font = "600 10px Segoe UI, system-ui, sans-serif";
    ctx.fillStyle = "#ede9fe";
    ctx.textAlign = "center";
    ctx.fillText("INVADER", sx, y - 20);
    ctx.textAlign = "left";
  }

  function drawBot() {
    if (!botAssist) return;
    const sx = worldToScreenX(botAssist.x);
    const y = laneY(botAssist.lane);
    // Simple Optimus-ish humanoid
    ctx.fillStyle = "#e5e5e5";
    ctx.fillRect(sx - 8, y - 28, 16, 26);
    ctx.beginPath();
    ctx.arc(sx, y - 34, 8, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = "#404040";
    ctx.fillRect(sx - 12, y - 18, 6, 14);
    ctx.fillRect(sx + 6, y - 18, 6, 14);
    ctx.fillStyle = "#c084fc";
    ctx.font = "700 10px Segoe UI, system-ui, sans-serif";
    ctx.textAlign = "center";
    ctx.fillText("OPTIMUS", sx, y + 16);
    ctx.textAlign = "left";
  }

  function drawShieldBubble() {
    if (shieldTimer <= 0) return;
    ctx.save();
    ctx.strokeStyle = "rgba(251, 146, 60, " + (0.4 + 0.4 * Math.sin(performance.now() / 120)) + ")";
    ctx.lineWidth = 4;
    ctx.beginPath();
    const midX = w * 0.28;
    const midY = roadMetrics().top + roadMetrics().height / 2;
    ctx.ellipse(midX, midY, 110, roadMetrics().height * 0.48, 0, 0, Math.PI * 2);
    ctx.stroke();
    ctx.restore();
  }

  function drawParticles() {
    for (const p of particles) {
      ctx.globalAlpha = Math.max(0, p.life * 2);
      ctx.fillStyle = p.color;
      ctx.fillRect(worldToScreenX(p.x) - 2, p.y - 2, 4, 4);
      ctx.globalAlpha = 1;
    }
    for (const f of fx) {
      if (f.kind === "text") {
        ctx.globalAlpha = Math.min(1, f.life * 2);
        ctx.font = "800 18px Segoe UI, system-ui, sans-serif";
        ctx.fillStyle = f.color;
        ctx.textAlign = "center";
        ctx.fillText(f.text, worldToScreenX(f.x), f.y - (1 - f.life) * 20);
        ctx.textAlign = "left";
        ctx.globalAlpha = 1;
      } else if (f.kind === "zap") {
        ctx.strokeStyle = "rgba(96,165,250," + f.life * 2 + ")";
        ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(worldToScreenX(f.x - 40), f.y);
        for (let i = 0; i < 6; i++) {
          ctx.lineTo(worldToScreenX(f.x - 40 + i * 30), f.y + (i % 2 ? 16 : -16));
        }
        ctx.stroke();
      }
    }
  }

  function drawHitFlash() {
    if (hitFlash <= 0) return;
    ctx.fillStyle = "rgba(248,113,113," + hitFlash * 0.45 + ")";
    ctx.fillRect(0, 0, w, h);
  }

  function drawTitleBackdrop() {
    // Full-bleed Imagine splash behind the translucent overlay
    if (art.splash) {
      drawImageCover(art.splash, 0, 0, w, h, 0.32);
      ctx.fillStyle = "rgba(6, 20, 8, 0.28)";
      ctx.fillRect(0, 0, w, h);
    } else {
      drawSky();
      drawRoad();
    }
    // Small convoy preview at bottom so roles stay readable
    const preview = FROG_ORDER.map((id, i) => {
      const f = makeFrog(id, id === selectedId, i);
      f.x = cameraX + 140 + i * 70;
      f.y = laneY(i % LANE_COUNT);
      f.lane = i % LANE_COUNT;
      return f;
    });
    if (!art.splash) {
      for (const f of preview) drawCybertruck(f);
    } else {
      // Dim road strip so convoy still peeks under overlay on tall phones
      drawRoad();
      for (const f of preview) drawCybertruck(f);
    }
  }

  function render(t) {
    if (phase === "title") {
      drawTitleBackdrop();
      return;
    }

    drawSky();
    drawRoad();
    drawArtBillboards();
    for (const lm of landmarks) drawLandmark(lm);
    for (const hz of hazards) drawHazard(hz);
    for (const inv of invaders) drawInvader(inv, t);
    drawShieldBubble();
    drawBot();
    // Draw frogs back-to-front by lane
    const sorted = frogs.slice().sort((a, b) => a.lane - b.lane);
    for (const f of sorted) drawCybertruck(f);
    drawParticles();
    drawHitFlash();

    // Finish line
    const fin = worldToScreenX(STRIP_LEN);
    if (fin > -20 && fin < w + 20) {
      ctx.fillStyle = "#fbbf24";
      ctx.fillRect(fin, roadMetrics().top, 6, roadMetrics().height);
      ctx.font = "800 14px Segoe UI, system-ui, sans-serif";
      ctx.fillStyle = "#fbbf24";
      ctx.fillText("FINISH", fin + 10, roadMetrics().top + 20);
    }
  }

  function tick(now) {
    const dt = Math.min(0.05, (now - last) / 1000);
    last = now;

    if (phase === "play") {
      if (typeof FroggiesAI !== "undefined") {
        FroggiesAI.tickAI(frogs, getAIState(), dt);
      }
      moveFrogs(dt);
      collide(dt);
      updateParticles(dt);
      updateAbilityButton();
      paintHud();

      // Bob invaders slightly in world
      for (const inv of invaders) inv.bob += dt;

      if (worldScroll >= STRIP_LEN && phase === "play") {
        clearStage();
      }
    } else {
      updateParticles(dt);
    }

    render(now / 1000);
    requestAnimationFrame(tick);
  }

  // ——— Input ———

  function bindHold(btn, dir) {
    const down = (e) => {
      e.preventDefault();
      steerHeld = dir;
      unlockAudio();
    };
    const up = (e) => {
      e.preventDefault();
      if (steerHeld === dir) steerHeld = 0;
    };
    btn.addEventListener("pointerdown", down);
    btn.addEventListener("pointerup", up);
    btn.addEventListener("pointerleave", up);
    btn.addEventListener("pointercancel", up);
  }

  bindHold(btnLeft, -1);
  bindHold(btnRight, 1);

  btnAbility.addEventListener("pointerdown", (e) => {
    e.preventDefault();
    unlockAudio();
    const player = frogs.find((f) => f.human);
    if (player) requestAbility(player);
  });

  // Swipe on canvas
  canvas.addEventListener("pointerdown", (e) => {
    swipeStartX = e.clientX;
    unlockAudio();
  });
  canvas.addEventListener("pointerup", (e) => {
    if (swipeStartX == null) return;
    const dx = e.clientX - swipeStartX;
    swipeStartX = null;
    if (phase !== "play") return;
    const player = frogs.find((f) => f.human);
    if (!player) return;
    if (dx < -40) player.targetLane = Math.max(0, player.lane - 1);
    if (dx > 40) player.targetLane = Math.min(LANE_COUNT - 1, player.lane + 1);
  });

  window.addEventListener("keydown", (e) => {
    if (e.key === "ArrowLeft" || e.key === "a" || e.key === "A") {
      steerHeld = -1;
      e.preventDefault();
    }
    if (e.key === "ArrowRight" || e.key === "d" || e.key === "D") {
      steerHeld = 1;
      e.preventDefault();
    }
    if (e.key === " " || e.key === "Enter") {
      e.preventDefault();
      if (phase === "play") {
        const player = frogs.find((f) => f.human);
        if (player) requestAbility(player);
      } else if (phase === "wipe" || phase === "clear") {
        startRun(selectedId);
      } else if (phase === "title") {
        startRun(selectedId);
      }
    }
  });
  window.addEventListener("keyup", (e) => {
    if (["ArrowLeft", "a", "A", "ArrowRight", "d", "D"].includes(e.key)) {
      steerHeld = 0;
    }
  });

  document.querySelectorAll(".frog-btn").forEach((btn) => {
    btn.addEventListener("click", () => {
      selectedId = btn.dataset.id;
      document.querySelectorAll(".frog-btn").forEach((b) => b.classList.toggle("selected", b.dataset.id === selectedId));
      unlockAudio();
      startRun(selectedId);
    });
  });

  btnStart.addEventListener("click", () => {
    unlockAudio();
    if (phase === "title" || phase === "wipe" || phase === "clear") {
      startRun(selectedId);
    }
  });

  overlay.addEventListener("pointerdown", (e) => {
    if (e.target.closest(".frog-btn") || e.target.closest(".start-btn")) return;
    if (phase === "wipe" || phase === "clear") {
      unlockAudio();
      startRun(selectedId);
    }
  });

  window.addEventListener("resize", resize);
  resize();
  loadArt();
  const selectedBtn = document.querySelector('.frog-btn[data-id="james"]');
  if (selectedBtn) selectedBtn.classList.add("selected");
  showOverlay(
    "Froggies Cybertruck Odyssey",
    "Steer left/right · tap ability · keep the convoy alive past the ranch house, track, and pond.",
    "Tap a froggy to start · or tap GO for James",
    true
  );
  requestAnimationFrame(tick);
})();
