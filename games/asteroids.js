(() => {
  const canvas = document.getElementById("view");
  const ctx = canvas.getContext("2d");
  const hud = document.getElementById("hud");
  const overlay = document.getElementById("overlay");
  const overlayTitle = document.getElementById("overlay-title");
  const overlayText = document.getElementById("overlay-text");
  const phone = window.matchMedia("(pointer: coarse), (max-width: 800px)").matches;

  /* audio1: shared arcade audio (shared/sfx.js); legacy beep() is silenced when it is present */
  const AUD = window.ArcadeAudio || null;
  if (AUD) AUD.init({ mood: "space", engine: "jet", button: "bottom-center" });
  function sfx(name, s) { if (AUD) AUD.play(name, s); }
  const JOIN_SECS = 2;
  const FADE_SECS = 1.2;

  const keys = new Set();
  // Touch cluster drives P1 only
  const held = { left: false, right: false, thrust: false, shoot: false };
  let pads = [null, null, null, null];
  let w = 1;
  let h = 1;
  let last = performance.now();
  let score = 0;
  let lives = 3;
  let rocks = [];
  let shots = [];
  let particles = [];
  let phase = "title";
  let audio = null;
  let best = Number(localStorage.getItem("asteroids-best") || 0) || 0;
  let matchTime = 0;
  let joinClosed = false;

  const COLORS = ["#fff", "#60a5fa", "#fbbf24", "#34d399"];
  const ships = [0, 1, 2, 3].map((i) => ({
    id: i,
    x: 0,
    y: 0,
    a: -Math.PI / 2,
    vx: 0,
    vy: 0,
    r: 14,
    invuln: 0,
    shootCd: 0,
    color: COLORS[i],
    thrusting: false,
    everMoved: false,
    active: true,
    alpha: 1,
  }));

  // Bindings: left, right, thrust, fire (codes). P4: numpad preferred; T/G/Y/R if no numpad.
  const BINDS = [
    { left: ["ArrowLeft"], right: ["ArrowRight"], thrust: ["ArrowUp"], fire: ["Space"] },
    { left: ["KeyA"], right: ["KeyD"], thrust: ["KeyW"], fire: ["KeyF"] },
    { left: ["KeyJ"], right: ["KeyL"], thrust: ["KeyI"], fire: ["KeyH"] },
    { left: ["Numpad4", "KeyG"], right: ["Numpad6", "KeyY"], thrust: ["Numpad8", "KeyT"], fire: ["Numpad0", "KeyR"] },
  ];

  function anyHeld(codes) {
    for (const c of codes) if (keys.has(c)) return true;
    return false;
  }

  function padInput(i) {
    const GP = window.SimilarizeGamepad;
    if (!GP) return null;
    if (typeof GP.pollPad === "function") return GP.pollPad(i);
    return i === 0 ? GP.poll() : null;
  }

  function readCtrl(i) {
    const b = BINDS[i];
    const pad = pads[i];
    const gpLeft = !!(pad && pad.connected && (pad.lx < -0.25 || pad.dpad.l));
    const gpRight = !!(pad && pad.connected && (pad.lx > 0.25 || pad.dpad.r));
    const gpThrust = !!(pad && pad.connected && (pad.ly < -0.25 || pad.a || pad.lt));
    const gpShoot = !!(pad && pad.connected && (pad.b || pad.rb || pad.rt || pad.x));
    const touch = i === 0;
    return {
      left: (touch && held.left) || anyHeld(b.left) || gpLeft,
      right: (touch && held.right) || anyHeld(b.right) || gpRight,
      thrust: (touch && held.thrust) || anyHeld(b.thrust) || gpThrust,
      shoot: (touch && held.shoot) || anyHeld(b.fire) || gpShoot,
    };
  }

  function ctrlActive(ctrl) {
    return !!(ctrl.left || ctrl.right || ctrl.thrust || ctrl.shoot);
  }

  function activeCount() {
    let n = 0;
    for (const s of ships) if (s.active) n++;
    return n;
  }

  function resize() {
    const dpr = Math.min(2, window.devicePixelRatio || 1);
    w = Math.max(1, window.innerWidth);
    h = Math.max(1, window.innerHeight);
    canvas.width = Math.floor(w * dpr);
    canvas.height = Math.floor(h * dpr);
    canvas.style.width = w + "px";
    canvas.style.height = h + "px";
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    if (phase === "title") placeShipsTitle();
    else {
      for (const ship of ships) {
        if (!ship.active && ship.alpha <= 0) continue;
        ship.x = Math.max(ship.r, Math.min(w - ship.r, ship.x));
        ship.y = Math.max(ship.r, Math.min(h - ship.r, ship.y));
      }
    }
  }

  function placeShipsTitle() {
    const cx = w / 2;
    const cy = h / 2;
    const offsets = [
      [-28, -20],
      [28, -20],
      [-28, 28],
      [28, 28],
    ];
    ships.forEach((ship, i) => {
      ship.x = cx + offsets[i][0];
      ship.y = cy + offsets[i][1];
      ship.vx = 0;
      ship.vy = 0;
      ship.a = -Math.PI / 2;
      ship.invuln = 0;
      ship.shootCd = 0;
      ship.thrusting = false;
      ship.everMoved = false;
      ship.active = true;
      ship.alpha = 1;
    });
  }

  function respawnShip(ship) {
    const cx = w / 2;
    const cy = h / 2;
    const ang = (ship.id / 4) * Math.PI * 2 - Math.PI / 2;
    ship.x = cx + Math.cos(ang) * 50;
    ship.y = cy + Math.sin(ang) * 50;
    ship.vx = 0;
    ship.vy = 0;
    ship.a = -Math.PI / 2;
    ship.invuln = 2;
    ship.shootCd = 0;
  }

  function unlock() {
    if (audio) return;
    try {
      const ctxA = new AudioContext();
      audio = ctxA;
      if (ctxA.state === "suspended") ctxA.resume();
    } catch (e) {
      audio = null;
    }
  }

  function beep(freq, dur, type, vol) {
    if (!audio || AUD) return;
    const t = audio.currentTime;
    const o = audio.createOscillator();
    const g = audio.createGain();
    o.type = type;
    o.frequency.value = freq;
    g.gain.setValueAtTime(vol, t);
    g.gain.exponentialRampToValueAtTime(0.001, t + dur);
    o.connect(g);
    g.connect(audio.destination);
    o.start(t);
    o.stop(t + dur);
  }

  function wrap(obj) {
    if (obj.x < 0) obj.x += w;
    if (obj.x > w) obj.x -= w;
    if (obj.y < 0) obj.y += h;
    if (obj.y > h) obj.y -= h;
  }

  function rockShape(seed, n) {
    const pts = [];
    for (let i = 0; i < n; i++) {
      const ang = (i / n) * Math.PI * 2;
      const j = Math.abs(Math.sin(seed * 12.9898 + i * 78.233) * 43758.5453);
      const rad = 0.72 + (j - Math.floor(j)) * 0.5;
      pts.push([Math.cos(ang) * rad, Math.sin(ang) * rad]);
    }
    return pts;
  }

  function makeRock(x, y, tier) {
    const radius = tier === 3 ? 46 : tier === 2 ? 28 : 16;
    const speed = (70 + Math.random() * 50) * (tier === 1 ? 1.35 : 1);
    const ang = Math.random() * Math.PI * 2;
    return {
      x, y, tier, radius,
      vx: Math.cos(ang) * speed,
      vy: Math.sin(ang) * speed,
      rot: Math.random() * Math.PI * 2,
      spin: (Math.random() - 0.5) * 1.4,
      shape: rockShape(Math.random() * 100, 9 + tier),
    };
  }

  function farFromShips(x, y, minD) {
    for (const s of ships) {
      if (!s.active) continue;
      if (Math.hypot(x - s.x, y - s.y) < minD) return false;
    }
    return true;
  }

  // ast3 co-op waves: the whole crew clears a wave together; each wave adds a rock (cap 12) and a team banner
  let wave = 0, waveT = 0;
  function spawnField() {
    rocks = [];
    wave++; waveT = 2.2;
    const count = Math.max(4, Math.min(8, Math.round((w * h) / 140000))) + Math.min(4, wave - 1);
    for (let i = 0; i < count; i++) {
      let x = 0;
      let y = 0;
      for (let t = 0; t < 12; t++) {
        x = Math.random() * w;
        y = Math.random() * h;
        if (farFromShips(x, y, 180)) break;
      }
      rocks.push(makeRock(x, y, 3));
    }
  }

  function drawWave() {
    if (waveT <= 0 || phase !== "play") return;
    waveT -= 1 / 60;
    ctx.save(); ctx.globalAlpha = Math.min(1, waveT); ctx.textAlign = "center"; ctx.fillStyle = "#9fe7ff";
    ctx.font = "700 28px system-ui, sans-serif"; ctx.fillText(wave === 1 ? "WAVE 1 \u00b7 TEAM UP!" : "WAVE " + wave + " \u00b7 NICE TEAMWORK!", w / 2, h * 0.32);
    ctx.restore();
  }
  function start() {
    try { if (window.SimilarizeViewportFS) window.SimilarizeViewportFS.enter(); } catch (_) {}
    unlock();
    score = 0; wave = 0;
    lives = 3;
    shots = [];
    particles = [];
    matchTime = 0;
    joinClosed = false;
    placeShipsTitle();
    for (const s of ships) {
      s.invuln = 2;
      s.shootCd = 0;
      s.everMoved = false;
      s.active = true;
      s.alpha = 1;
    }
    phase = "play";
    spawnField();
    sfx("go");
    overlay.hidden = true;
    paintHud();
  }

  function burst(x, y, n, color) {
    for (let i = 0; i < n; i++) {
      const a = Math.random() * Math.PI * 2;
      const s = 30 + Math.random() * 120;
      particles.push({ x, y, vx: Math.cos(a) * s, vy: Math.sin(a) * s, life: 0.45, color });
    }
  }

  function shoot(ship) {
    if (phase !== "play" || !ship.active || ship.shootCd > 0) return;
    ship.shootCd = 0.18;
    shots.push({
      x: ship.x + Math.cos(ship.a) * (ship.r + 4),
      y: ship.y + Math.sin(ship.a) * (ship.r + 4),
      vx: Math.cos(ship.a) * 460 + ship.vx,
      vy: Math.sin(ship.a) * 460 + ship.vy,
      life: 3.4, // 4x range before bullets despawn
      color: ship.color,
    });
    beep(640, 0.06, "square", 0.04);
    sfx("laser");
  }

  function hitShip(ship) {
    if (!ship.active || ship.invuln > 0 || phase !== "play") return;
    lives -= 1;
    burst(ship.x, ship.y, 16, ship.color);
    beep(90, 0.3, "sawtooth", 0.08);
    sfx("explode", 1.2);
    if (lives <= 0) {
      phase = "over";
      if (AUD) { AUD.engineStop(); setTimeout(() => sfx("lose"), 500); }
      if (score > best) {
        best = score;
        try { localStorage.setItem("asteroids-best", String(best)); } catch (e) {}
      }
      overlayTitle.textContent = "Ships lost";
      overlayText.textContent = "Score " + score + " · best " + best;
      overlay.hidden = false;
      paintHud();
      return;
    }
    respawnShip(ship);
    paintHud();
  }

  function splitRock(rock, index) {
    rocks.splice(index, 1);
    score += rock.tier === 3 ? 20 : rock.tier === 2 ? 50 : 100;
    burst(rock.x, rock.y, 8 + rock.tier * 6, "#ffd9a8");
    burst(rock.x, rock.y, 4 + rock.tier * 2, "#9fe7ff");
    rings.push({ x: rock.x, y: rock.y, r: rock.radius * 0.4, max: rock.radius * 2.2, life: 0.5 });
    shake = Math.min(14, shake + rock.tier * 3);
    beep(180 + rock.tier * 40, 0.08, "triangle", 0.05);
    sfx("explode", rock.tier === 3 ? 0.9 : rock.tier === 2 ? 0.6 : 0.35);
    if (rock.tier > 1) {
      rocks.push(makeRock(rock.x, rock.y, rock.tier - 1));
      rocks.push(makeRock(rock.x, rock.y, rock.tier - 1));
    }
    if (!rocks.length) {
      for (const s of ships) {
        if (s.active) s.invuln = Math.max(s.invuln, 1.2);
      }
      spawnField();
      sfx("powerup");
    }
    paintHud();
  }

  function paintHud() {
    const n = activeCount();
    const joinHint = phase === "play" && !joinClosed
      ? "    Join " + Math.max(0, Math.ceil(JOIN_SECS - matchTime)) + "s"
      : "";
    hud.textContent = "Team score " + score + "    Wave " + Math.max(1, wave) + "    Lives " + lives + "    Best " + best + "    " + n + "P" + joinHint;
  }

  function closeJoinWindow() {
    if (joinClosed) return;
    joinClosed = true;
    for (const ship of ships) {
      if (!ship.everMoved) {
        // Idle: drop from collision/lives immediately; fade visually. Not a death.
        ship.active = false;
        ship.thrusting = false;
        ship.vx = 0;
        ship.vy = 0;
      }
    }
    paintHud();
  }

  function stepShip(ship, ctrl, dt) {
    ship.invuln = Math.max(0, ship.invuln - dt);
    ship.shootCd = Math.max(0, ship.shootCd - dt);
    ship.thrusting = !!ctrl.thrust;
    if (ctrl.left) ship.a -= 3.1 * dt;
    if (ctrl.right) ship.a += 3.1 * dt;
    if (ctrl.thrust) {
      ship.vx += Math.cos(ship.a) * 170 * dt;
      ship.vy += Math.sin(ship.a) * 170 * dt;
    }
    if (ctrl.shoot) shoot(ship);
    const sp = Math.hypot(ship.vx, ship.vy);
    if (sp > 280) {
      ship.vx = (ship.vx / sp) * 280;
      ship.vy = (ship.vy / sp) * 280;
    }
    ship.x += ship.vx * dt;
    ship.y += ship.vy * dt;
    wrap(ship);
  }

  function step(dt) {
    dt = Math.min(dt, 0.05);
    for (let i = 0; i < 4; i++) pads[i] = padInput(i);
    for (let i = 0; i < 4; i++) {
      const pad = pads[i];
      if (pad && pad.connected && phase !== "play" && (pad.buttonsPressed.a || pad.buttonsPressed.start || pad.buttonsPressed.b)) {
        start();
        break;
      }
    }
    if (phase !== "play") {
      particles.forEach((p) => { p.x += p.vx * dt; p.y += p.vy * dt; p.life -= dt; });
      particles = particles.filter((p) => p.life > 0);
      return;
    }

    matchTime += dt;
    if (!joinClosed && matchTime >= JOIN_SECS) closeJoinWindow();

    for (const ship of ships) {
      if (!ship.active) {
        if (ship.alpha > 0) ship.alpha = Math.max(0, ship.alpha - dt / FADE_SECS);
        continue;
      }
      const ctrl = readCtrl(ship.id);
      if (!joinClosed && ctrlActive(ctrl)) ship.everMoved = true;
      stepShip(ship, ctrl, dt);
    }

    if (phase === "play" && !joinClosed) paintHud();

    for (const r of rocks) {
      r.x += r.vx * dt;
      r.y += r.vy * dt;
      r.rot += r.spin * dt;
      wrap(r);
      for (const ship of ships) {
        if (!ship.active) continue;
        if (ship.invuln <= 0 && Math.hypot(r.x - ship.x, r.y - ship.y) < r.radius + ship.r * 0.7) {
          hitShip(ship);
          if (phase !== "play") return;
        }
      }
    }
    for (let i = shots.length - 1; i >= 0; i--) {
      const s = shots[i];
      s.x += s.vx * dt;
      s.y += s.vy * dt;
      s.life -= dt;
      wrap(s);
      if (s.life <= 0) {
        shots.splice(i, 1);
        continue;
      }
      for (let j = rocks.length - 1; j >= 0; j--) {
        const r = rocks[j];
        if (Math.hypot(s.x - r.x, s.y - r.y) < r.radius) {
          shots.splice(i, 1);
          splitRock(r, j);
          break;
        }
      }
    }
    particles.forEach((p) => { p.x += p.vx * dt; p.y += p.vy * dt; p.life -= dt; });
    particles = particles.filter((p) => p.life > 0);
  }

  function drawShip(ship) {
    if (ship.alpha <= 0) return;
    ctx.save();
    ctx.globalAlpha = ship.alpha * (ship.invuln > 0 && Math.floor(ship.invuln * 10) % 2 === 0 ? 0.35 : 1);
    ctx.translate(ship.x, ship.y);
    ctx.rotate(ship.a);
    ctx.beginPath();
    ctx.moveTo(18, 0);
    ctx.lineTo(-12, 10);
    ctx.lineTo(-7, 0);
    ctx.lineTo(-12, -10);
    ctx.closePath();
    ctx.fillStyle = "rgba(10,14,30,0.85)"; ctx.fill();
    neon(ship.color, 2);
    if (ship.thrusting && ship.active) {
      ctx.beginPath();
      ctx.moveTo(-8, 4);
      ctx.lineTo(-18 - Math.random() * 8, 0);
      ctx.lineTo(-8, -4);
      neon("#fbbf24", 2);
    }
    ctx.restore();
  }

  // ast2: deep-space backdrop (parallax stars + soft nebula), neon vectors, shockwaves, shake
  const rings = [];
  let shake = 0, stars = null, nebula = null, starW = 0;
  function makeSky() {
    starW = w;
    stars = [];
    for (let i = 0; i < Math.round((w * h) / 2600); i++) stars.push({ x: Math.random() * w, y: Math.random() * h, z: 0.2 + Math.random() * 0.8, t: Math.random() * 6 });
    nebula = document.createElement("canvas"); nebula.width = Math.max(1, w >> 2); nebula.height = Math.max(1, h >> 2);
    const n = nebula.getContext("2d");
    const blobs = [["rgba(80,40,160,0.35)", 0.25, 0.3], ["rgba(0,120,200,0.28)", 0.75, 0.65], ["rgba(144,0,0,0.22)", 0.55, 0.15]];
    for (const [c, bx, by] of blobs) { const g = n.createRadialGradient(bx * nebula.width, by * nebula.height, 0, bx * nebula.width, by * nebula.height, nebula.width * 0.45); g.addColorStop(0, c); g.addColorStop(1, "rgba(0,0,0,0)"); n.fillStyle = g; n.fillRect(0, 0, nebula.width, nebula.height); }
  }
  function neon(color, width) {
    ctx.strokeStyle = color; ctx.globalAlpha = 0.22; ctx.lineWidth = width * 4; ctx.stroke();
    ctx.globalAlpha = 1; ctx.lineWidth = width; ctx.stroke();
  }
  function draw() {
    if (!stars || starW !== w) makeSky();
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.fillStyle = "#03040a";
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    { const k = canvas.width / (w || 1); ctx.setTransform(k, 0, 0, k, 0, 0); }
    ctx.drawImage(nebula, 0, 0, w, h);
    const tnow = performance.now() / 1000, lead = ships.find((sh) => sh.active) || { vx: 0, vy: 0 };
    for (const st of stars) {
      st.x -= (lead.vx || 0) * st.z * 0.004; st.y -= (lead.vy || 0) * st.z * 0.004;
      if (st.x < 0) st.x += w; if (st.x > w) st.x -= w; if (st.y < 0) st.y += h; if (st.y > h) st.y -= h;
      ctx.globalAlpha = 0.35 + st.z * 0.5 + Math.sin(tnow * 2 + st.t) * 0.12;
      ctx.fillStyle = st.z > 0.85 ? "#cfe8ff" : "#fff";
      ctx.fillRect(st.x, st.y, st.z * 1.8, st.z * 1.8);
    }
    ctx.globalAlpha = 1;
    ctx.save();
    if (shake > 0.2) { ctx.translate((Math.random() - 0.5) * shake, (Math.random() - 0.5) * shake); shake *= 0.88; } else shake = 0;
    for (let i = rings.length - 1; i >= 0; i--) {
      const g = rings[i]; g.life -= 1 / 60; g.r += (g.max - g.r) * 0.18;
      if (g.life <= 0) { rings.splice(i, 1); continue; }
      ctx.beginPath(); ctx.arc(g.x, g.y, g.r, 0, Math.PI * 2);
      ctx.globalAlpha = g.life * 1.6; ctx.strokeStyle = "#ffcf8a"; ctx.lineWidth = 2; ctx.stroke(); ctx.globalAlpha = 1;
    }
    for (const r of rocks) {
      ctx.save();
      ctx.translate(r.x, r.y);
      ctx.rotate(r.rot);
      ctx.beginPath();
      r.shape.forEach(([px, py], i) => {
        const x = px * r.radius;
        const y = py * r.radius;
        if (i === 0) ctx.moveTo(x, y);
        else ctx.lineTo(x, y);
      });
      ctx.closePath();
      const rg = ctx.createRadialGradient(-r.radius * 0.35, -r.radius * 0.35, r.radius * 0.1, 0, 0, r.radius * 1.1);
      rg.addColorStop(0, "rgba(120,110,130,0.55)"); rg.addColorStop(1, "rgba(20,18,30,0.75)");
      ctx.fillStyle = rg; ctx.fill();
      neon("#e8e4ff", 1.6);
      ctx.globalAlpha = 0.35; ctx.fillStyle = "#0a0a14";
      for (let k = 0; k < r.tier; k++) { const cx = r.shape[k * 3 % r.shape.length]; ctx.beginPath(); ctx.arc(cx[0] * r.radius * 0.45, cx[1] * r.radius * 0.45, r.radius * 0.14, 0, Math.PI * 2); ctx.fill(); }
      ctx.globalAlpha = 1;
      ctx.restore();
    }
    for (const s of shots) {
      const sp = Math.hypot(s.vx || 0, s.vy || 0) || 1;
      ctx.beginPath(); ctx.moveTo(s.x, s.y); ctx.lineTo(s.x - (s.vx || 0) / sp * 12, s.y - (s.vy || 0) / sp * 12);
      ctx.lineCap = "round"; neon(s.color || "#fff", 2.5); ctx.lineCap = "butt";
    }
    for (const p of particles) {
      ctx.globalAlpha = Math.max(0, p.life / 0.45);
      ctx.fillStyle = p.color;
      ctx.fillRect(p.x, p.y, 2, 2);
      ctx.globalAlpha = 1;
    }
    for (const ship of ships) drawShip(ship);
    ctx.restore();
    drawWave();
  }

  function frame(now) {
    const dt = (now - last) / 1000;
    last = now;
    step(dt);
    if (AUD) {
      const th = phase === "play" && ships.some((sh) => sh.active && sh.thrusting);
      if (th) AUD.engine(1, 0.6, "jet"); else AUD.engineStop();
    }
    draw();
    requestAnimationFrame(frame);
  }

  function bindHold(id, name) {
    const el = document.getElementById(id);
    const down = (e) => {
      e.preventDefault();
      unlock();
      held[name] = true;
      if (phase !== "play") start();
      if (name === "shoot") shoot(ships[0]);
      el.setPointerCapture?.(e.pointerId);
    };
    const up = (e) => {
      held[name] = false;
      e.preventDefault();
    };
    el.addEventListener("pointerdown", down);
    el.addEventListener("pointerup", up);
    el.addEventListener("pointercancel", up);
    el.addEventListener("lostpointercapture", () => { held[name] = false; });
  }

  const PREVENT = new Set([
    "ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown", "Space",
    "KeyW", "KeyA", "KeyS", "KeyD", "KeyF",
    "KeyI", "KeyJ", "KeyK", "KeyL", "KeyH",
    "KeyT", "KeyG", "KeyY", "KeyR",
    "Numpad4", "Numpad6", "Numpad8", "Numpad0", "Numpad5",
  ]);

  window.addEventListener("keydown", (e) => {
    if (PREVENT.has(e.code)) e.preventDefault();
    if (e.repeat && e.code !== "Space" && !BINDS.some((b) => b.thrust.includes(e.code) || b.fire.includes(e.code))) return;
    keys.add(e.code);
    unlock();
    if (phase !== "play" && (e.code === "Enter" || e.code === "Space" || e.code === "ArrowUp")) start();
  });
  window.addEventListener("keyup", (e) => keys.delete(e.code));
  window.addEventListener("resize", resize);
  window.visualViewport?.addEventListener("resize", resize);
  document.addEventListener("visibilitychange", () => {
    if (document.hidden) {
      held.left = held.right = held.thrust = held.shoot = false;
      keys.clear();
    }
  });
  overlay.addEventListener("pointerdown", (e) => {
    e.preventDefault();
    start();
  });
  bindHold("left", "left");
  bindHold("right", "right");
  bindHold("thrust", "thrust");
  bindHold("shoot", "shoot");

  document.body.classList.toggle("phone", phone);
  resize();
  paintHud();
  overlayTitle.textContent = "Asteroids · 4P local";
  overlayText.textContent = (phone
    ? "Touch = P1. Pads 0–3 = P1–P4. "
    : "P1 arrows+Space · P2 WASD+F · P3 IJKL+H · P4 numpad 8460 (or T/G/Y/R). Pads 0–3 → P1–P4. Shared score/lives; each ship respawns. ")
    + "Move in first 10s to join · idle ships fade";
  requestAnimationFrame(frame);
  if (new URLSearchParams(location.search).get("demo") === "play") setTimeout(() => { try { start(); } catch (e) { console.warn(e); } }, 300);
})();
