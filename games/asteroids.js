(() => {
  const canvas = document.getElementById("view");
  const ctx = canvas.getContext("2d");
  const hud = document.getElementById("hud");
  const overlay = document.getElementById("overlay");
  const overlayTitle = document.getElementById("overlay-title");
  const overlayText = document.getElementById("overlay-text");
  const phone = window.matchMedia("(pointer: coarse), (max-width: 800px)").matches;

  const JOIN_SECS = 10;
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
    if (!audio) return;
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

  function spawnField() {
    rocks = [];
    const count = Math.max(4, Math.min(8, Math.round((w * h) / 140000)));
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

  function start() {
    unlock();
    score = 0;
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
      life: 0.85,
      color: ship.color,
    });
    beep(640, 0.06, "square", 0.04);
  }

  function hitShip(ship) {
    if (!ship.active || ship.invuln > 0 || phase !== "play") return;
    lives -= 1;
    burst(ship.x, ship.y, 16, ship.color);
    beep(90, 0.3, "sawtooth", 0.08);
    if (lives <= 0) {
      phase = "over";
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
    burst(rock.x, rock.y, 8, "#ddd");
    beep(180 + rock.tier * 40, 0.08, "triangle", 0.05);
    if (rock.tier > 1) {
      rocks.push(makeRock(rock.x, rock.y, rock.tier - 1));
      rocks.push(makeRock(rock.x, rock.y, rock.tier - 1));
    }
    if (!rocks.length) {
      for (const s of ships) {
        if (s.active) s.invuln = Math.max(s.invuln, 1.2);
      }
      spawnField();
    }
    paintHud();
  }

  function paintHud() {
    const n = activeCount();
    const joinHint = phase === "play" && !joinClosed
      ? "    Join " + Math.max(0, Math.ceil(JOIN_SECS - matchTime)) + "s"
      : "";
    hud.textContent = "Score " + score + "    Lives " + lives + "    Best " + best + "    " + n + "P" + joinHint;
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
    ctx.strokeStyle = ship.color;
    ctx.lineWidth = 2;
    ctx.stroke();
    if (ship.thrusting && ship.active) {
      ctx.beginPath();
      ctx.moveTo(-8, 4);
      ctx.lineTo(-18 - Math.random() * 8, 0);
      ctx.lineTo(-8, -4);
      ctx.strokeStyle = "#fbbf24";
      ctx.stroke();
    }
    ctx.restore();
  }

  function draw() {
    ctx.fillStyle = "#000";
    ctx.fillRect(0, 0, w, h);
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
      ctx.strokeStyle = "#fff";
      ctx.lineWidth = 2;
      ctx.stroke();
      ctx.restore();
    }
    for (const s of shots) {
      ctx.fillStyle = s.color || "#fff";
      ctx.fillRect(s.x - 1.5, s.y - 1.5, 3, 3);
    }
    for (const p of particles) {
      ctx.globalAlpha = Math.max(0, p.life / 0.45);
      ctx.fillStyle = p.color;
      ctx.fillRect(p.x, p.y, 2, 2);
      ctx.globalAlpha = 1;
    }
    for (const ship of ships) drawShip(ship);
  }

  function frame(now) {
    const dt = (now - last) / 1000;
    last = now;
    step(dt);
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
})();
