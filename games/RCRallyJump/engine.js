const VERSION = "2.5";
const VIEW_W = 960;
const VIEW_H = 540;
const PLAYER_X = 168;
const CAR_W = 118;
const CAR_H = 52;
const GATE_W = 56;
const GRAVITY = 1500;
const HOLD_NET = -900;
const MAX_UP = -420;
const MAX_DOWN = 780;
const IMPULSE = -380;
const TAP_COST = 3;
const HOLD_DRAIN = 36;
const RECHARGE = 48;
const OPENING = 168;
const IMG_W = 720;
const IMG_H = 315;
const SX = CAR_W / IMG_W;
const SY = CAR_H / IMG_H;
const SPIN_AIR = 0.85;
function wheelSpec(ix, iy, imgR) {
  return {
    lx: (ix - IMG_W / 2) * SX,
    ly: (iy - IMG_H / 2) * SY,
    r: imgR * SY,
    imgR
  };
}
const REAR = wheelSpec(100, 232, 82);
const FRONT = wheelSpec(600, 234, 80);
const WHEELS = [REAR, FRONT];
function groundY(worldX) {
  const n = Math.sin(worldX * 41e-4) * 26 + Math.sin(worldX * 0.0105 + 1.4) * 12 + Math.sin(worldX * 22e-4 + 0.6) * 34;
  return clamp(VIEW_H * 0.78 + n, VIEW_H * 0.64, VIEW_H * 0.9);
}
const RAMP_RUN = 168;
const RAMP_RISE = 84;
const LIP_LEAD = 142;
const PACE = 255;
const MISSILE_MAX = 3;
const MISSILE_RELOAD = 9;
function rampAt(sim, worldX) {
  for (let i = sim.ramps.length - 1; i >= 0; i--) {
    const r = sim.ramps[i];
    if (worldX >= r.x0 && worldX <= r.lip) return r;
  }
  return null;
}
function rampLift(sim, worldX) {
  const r = rampAt(sim, worldX);
  if (!r) return 0;
  const t = (worldX - r.x0) / (r.lip - r.x0);
  return r.rise * t * t;
}
function surfaceY(sim, worldX) {
  return groundY(worldX) - rampLift(sim, worldX);
}
function createSim(best = 0) {
  const sim = {
    phase: "title",
    scroll: 0,
    y: 0,
    vy: 0,
    rot: 0,
    speed: 230,
    wheelAng: [0.4, 1.7],
    wheelOmega: [230 / REAR.r, 230 / FRONT.r],
    battery: 100,
    grounded: true,
    boosting: false,
    wasBoost: false,
    gates: [],
    ramps: [],
    loft: 0,
    drones: [],
    dust: [],
    popups: [],
    shots: [],
    missiles: MISSILE_MAX,
    missileT: 0,
    rig: "rally",
    booms: 0,
    nextGate: 780,
    nextDrone: 2600,
    gatesCleared: 0,
    best,
    shake: 0,
    crash: null,
    sinceOver: 0,
    time: 0,
    muted: false,
    reduced: false
  };
  plant(sim);
  return sim;
}
function startRun(sim) {
  const best = sim.best;
  const muted = sim.muted;
  const reduced = sim.reduced;
  const rig = sim.rig === "crawler" ? "crawler" : "rally";
  const fresh = createSim(best);
  fresh.phase = "play";
  fresh.muted = muted;
  fresh.reduced = reduced;
  fresh.rig = rig;
  Object.assign(sim, fresh);
}
function metersOf(sim) {
  if (sim.phase === "title") return 0;
  return Math.floor(sim.scroll / 20);
}
function scoreOf(sim) {
  return metersOf(sim) + sim.gatesCleared * 100;
}
function wheelPace(sim) {
  const r = (REAR.r + FRONT.r) * 0.5;
  return (sim.wheelOmega[0] + sim.wheelOmega[1]) * 0.5 * r;
}
function plant(sim) {
  sim.y = groundY(PLAYER_X + CAR_W * 0.5) - CAR_H;
}
function cruiseOf(sim) {
  return Math.min(390, 230 + sim.scroll * 0.01 + sim.gatesCleared * 3);
}
function randKind(worldX) {
  if (worldX < 1700) return hash(worldX) < 0.5 ? "drive" : "hop";
  const r = hash(worldX);
  if (r < 0.34) return "drive";
  if (r < 0.7) return "hop";
  return "climb";
}
function groundSpan(worldX) {
  let hi = -Infinity;
  let lo = Infinity;
  for (let i = -96; i <= GATE_W + 24; i += 12) {
    const y = groundY(worldX + i);
    hi = Math.max(hi, y);
    lo = Math.min(lo, y);
  }
  return { hi, lo };
}
function spawnGate(sim, worldX) {
  const { hi, lo } = groundSpan(worldX);
  const kicker = wantKicker(sim, worldX);
  const kind = kicker ? "hop" : randKind(worldX);
  let gapTop;
  let gapBot;
  if (kicker) {
    const base = groundY(worldX);
    gapBot = VIEW_H + 40;
    gapTop = Math.max(64, base - 308);
    sim.ramps.push({
      x0: worldX - LIP_LEAD - RAMP_RUN,
      lip: worldX - LIP_LEAD,
      rise: RAMP_RISE,
      grip: 0,
      taken: false
    });
  } else if (kind === "drive") {
    gapBot = hi + 48;
    gapTop = lo - CAR_H - 156;
  } else if (kind === "hop") {
    const lift = 40 + hash(worldX + 3) * 18;
    gapBot = hi - lift;
    gapTop = gapBot - OPENING;
  } else {
    const lift = 96 + hash(worldX + 7) * 36;
    gapBot = hi - lift;
    gapTop = gapBot - (42 + 130);
  }
  if (gapTop < 22) {
    const shift = 22 - gapTop;
    gapTop += shift;
    gapBot += shift;
  }
  sim.gates.push({ x: worldX, gapTop, gapBot, w: GATE_W, scored: false, kind, kicker });
}
function wantKicker(sim, worldX) {
  if (worldX < 1500) return false;
  if (hash(worldX + 8.5) > 0.4) return false;
  const last = sim.ramps[sim.ramps.length - 1];
  if (last && worldX - last.lip < 780) return false;
  return true;
}
function wheelWorld(sim, w) {
  const rot = sim.rot * Math.PI / 180;
  const c = Math.cos(rot);
  const s = Math.sin(rot);
  return {
    x: sim.scroll + PLAYER_X + CAR_W / 2 + c * w.lx - s * w.ly,
    y: sim.y + CAR_H / 2 + s * w.lx + c * w.ly
  };
}
function coupleWheels(sim, dt) {
  const cruise = cruiseOf(sim);
  for (let i = 0; i < 2; i++) {
    const w = WHEELS[i];
    const p = wheelWorld(sim, w);
    const sink = p.y + w.r - surfaceY(sim, p.x);
    if (sink <= -1.25) {
      sim.wheelOmega[i] *= Math.exp(-dt / SPIN_AIR);
      sim.wheelAng[i] += sim.wheelOmega[i] * dt;
      continue;
    }
    const load = clamp((sink + 2.4) / 2.4, 0.22, 1);
    const R = w.r;
    const I = 0.05 * R * R;
    const slip = sim.speed - sim.wheelOmega[i] * R;
    const inv = 1 + R * R / I;
    const force = clamp(slip / (0.12 * inv) * load, -420, 420);
    sim.speed -= force * dt;
    sim.wheelOmega[i] += force * R / I * dt;
    if (i === 0) sim.speed += (cruise - sim.speed) * dt * 1.35 * load;
    sim.wheelAng[i] += sim.wheelOmega[i] * dt;
    if (Math.abs(slip) > 130 && load > 0.3 && sim.dust.length < 80 && hash(sim.time * 900 + i * 19) > 0.72) {
      sim.dust.push({
        x: p.x,
        y: surfaceY(sim, p.x) - 1,
        vx: -50 - Math.abs(slip) * 0.12,
        vy: -24 - hash(sim.time * 13 + i) * 36,
        life: 0.28,
        max: 0.28,
        s: 2 + hash(sim.time * 5) * 2
      });
    }
  }
  sim.speed = clamp(sim.speed, 36, 420);
}
function kickRamp(sim, footX, dt) {
  let launched = false;
  for (const r of sim.ramps) {
    if (r.taken) continue;
    if (sim.grounded && footX >= r.x0 && footX <= r.lip) r.grip += sim.speed * dt;
    const prev = footX - sim.speed * dt;
    if (sim.grounded && prev < r.lip && footX >= r.lip) {
      r.taken = true;
      const coverage = clamp(r.grip / (RAMP_RUN * 0.85), 0, 1);
      const juice = coverage * clamp(sim.speed / PACE, 0, 1.2);
      sim.vy = juice >= 0.8 ? -720 * clamp(sim.speed / PACE, 0.96, 1.05) : -150 * clamp(juice / 0.8, 0.3, 1);
      sim.loft = 1.15;
      sim.y -= 4;
      launched = true;
      burst(sim, r.lip, surfaceY(sim, r.lip - 2), 12);
    } else if (footX > r.lip + 36) {
      r.taken = true;
    }
  }
  return launched;
}
function tick(sim, dt, input) {
  sim.time += dt;
  const want = input.boost && sim.phase === "play";
  const can = want && sim.battery > 0.4;
  if (can) {
    if (!sim.wasBoost) {
      sim.vy = Math.min(sim.vy, IMPULSE);
      sim.battery -= TAP_COST;
    }
    sim.vy += HOLD_NET * dt;
    sim.battery -= HOLD_DRAIN * dt;
    sim.boosting = true;
  } else if (sim.grounded) {
    sim.vy = 0;
    sim.battery = Math.min(100, sim.battery + RECHARGE * dt);
    sim.boosting = false;
  } else {
    sim.vy += GRAVITY * dt;
    sim.boosting = false;
  }
  sim.vy = clamp(sim.vy, sim.loft > 0 ? -780 : MAX_UP, MAX_DOWN);
  if (sim.loft > 0) sim.loft = Math.max(0, sim.loft - dt);
  sim.y += sim.vy * dt;
  sim.battery = clamp(sim.battery, 0, 100);
  sim.wasBoost = want;
  const footX = sim.scroll + PLAYER_X + CAR_W * 0.55;
  const gy = surfaceY(sim, footX);
  const launched = kickRamp(sim, footX, dt);
  if (launched) {
    sim.grounded = false;
  } else if (sim.y + CAR_H >= gy && sim.vy >= 0) {
    const impact = sim.vy;
    sim.y = gy - CAR_H;
    sim.vy = 0;
    if (!sim.grounded && impact > 180) burst(sim, footX, gy, 8);
    sim.grounded = true;
  } else {
    sim.grounded = false;
  }
  if (sim.y < 14) {
    sim.y = 14;
    if (sim.vy < 0) sim.vy = 0;
  }
  const ahead = surfaceY(sim, footX + 26);
  const behind = surfaceY(sim, footX - 26);
  const slope = behind - ahead;
  const target = sim.grounded ? clamp(slope * 0.55, -14, 14) : clamp(-sim.vy * 0.045, -26, 34);
  sim.rot += (target - sim.rot) * Math.min(1, dt * 10);
  coupleWheels(sim, dt);
  sim.scroll += sim.speed * dt;
  if (input.fire) {
    if (sim.missiles > 0) {
      sim.missiles -= 1;
      sim.shots.push({
        x: sim.scroll + PLAYER_X + CAR_W * 0.78,
        y: sim.y + CAR_H * 0.42,
        vx: sim.speed + 560,
        life: 1.25
      });
    } else {
      sim.popups.push({
        x: sim.scroll + PLAYER_X + CAR_W * 0.5,
        y: sim.y - 8,
        text: "EMPTY",
        life: 0.45,
        max: 0.45
      });
    }
  }
  if (sim.missiles < MISSILE_MAX) {
    sim.missileT += dt;
    if (sim.missileT >= MISSILE_RELOAD) {
      sim.missileT -= MISSILE_RELOAD;
      sim.missiles += 1;
    }
  } else {
    sim.missileT = 0;
  }
  for (const shot of sim.shots) {
    shot.x += shot.vx * dt;
    shot.life -= dt;
    if (shot.life <= 0) continue;
    for (const g of sim.gates) {
      if (g.blown) continue;
      if (shot.x < g.x - 10 || shot.x > g.x + g.w + 10) continue;
      const hitTop = g.gapTop > 4 && shot.y <= g.gapTop + 8;
      const hitBot = g.gapBot < VIEW_H && shot.y >= g.gapBot - 8;
      if (hitTop || hitBot) {
        g.blown = true;
        shot.life = 0;
        sim.booms += 1;
        burst(sim, shot.x, shot.y, 16);
        sim.popups.push({ x: g.x + g.w * 0.5, y: shot.y, text: "BOOM", life: 0.55, max: 0.55 });
        break;
      }
    }
    if (shot.life <= 0) continue;
    for (const d of sim.drones) {
      if (d.dead) continue;
      const dy = d.y + Math.sin(sim.time * 3 + d.bob) * 10;
      if (Math.hypot(shot.x - d.x, shot.y - dy) < 24) {
        d.dead = true;
        shot.life = 0;
        sim.booms += 1;
        burst(sim, d.x, dy, 12);
        break;
      }
    }
  }
  sim.shots = sim.shots.filter((s) => s.life > 0 && s.x - sim.scroll < VIEW_W + 120);
  if (sim.grounded && sim.phase === "play" && sim.dust.length < 70 && hash(sim.time * 1e3) > 0.35) {
    sim.dust.push({
      x: footX - 10,
      y: gy - 2,
      vx: -30 - hash(sim.time * 17) * 70,
      vy: -20 - hash(sim.time * 29) * 40,
      life: 0.45,
      max: 0.45,
      s: 2 + hash(sim.time * 13) * 3
    });
  }
  if (sim.boosting && sim.dust.length < 80) {
    sim.dust.push({
      x: sim.scroll + PLAYER_X + 8,
      y: sim.y + CAR_H * 0.55,
      vx: -80 - hash(sim.time * 41) * 40,
      vy: 40 + hash(sim.time * 53) * 50,
      life: 0.28,
      max: 0.28,
      s: 3
    });
  }
  while (sim.nextGate < sim.scroll + VIEW_W + 80) {
    spawnGate(sim, sim.nextGate);
    const spacing = cruiseOf(sim) * 1.85 + 150;
    sim.nextGate += spacing;
  }
  while (sim.nextDrone < sim.scroll + VIEW_W + 40) {
    const x = sim.nextDrone;
    const nearGate = sim.gates.some((g) => Math.abs(g.x - x) < 160);
    const gyD = groundY(x);
    const onRamp = sim.ramps.some((r) => x > r.x0 - 30 && x < r.lip + 120);
    if (!nearGate && !onRamp && gyD > 180) {
      sim.drones.push({
        x,
        y: 70 + hash(x + 11) * (gyD - 190),
        bob: hash(x) * Math.PI * 2,
        spin: hash(x + 5) * Math.PI * 2
      });
    }
    sim.nextDrone += 820 + hash(x + 9) * 520;
  }
  const hx = PLAYER_X + 16;
  const hy = sim.y + 10;
  const hw = CAR_W - 32;
  const hh = CAR_H - 14;
  for (const g of sim.gates) {
    const sx = g.x - sim.scroll;
    if (!g.blown) {
      const topHit = g.gapTop > 4 && aabb(hx, hy, hw, hh, sx, 0, g.w, g.gapTop);
      const botHit = g.gapBot < VIEW_H && aabb(hx, hy, hw, hh, sx, g.gapBot, g.w, VIEW_H - g.gapBot);
      if (topHit || botHit) {
        crash(sim, "gate");
        return;
      }
    }
    if (!g.scored && hx > sx + g.w) {
      g.scored = true;
      sim.gatesCleared += 1;
      sim.popups.push({
        x: g.x + g.w,
        y: (g.gapTop + Math.min(g.gapBot, VIEW_H - 20)) * 0.5,
        text: g.blown ? "CLEAR" : g.kicker && !sim.grounded ? "CLEAN" : "+100",
        life: 0.8,
        max: 0.8
      });
    }
  }
  for (const d of sim.drones) {
    if (d.dead) continue;
    d.x -= sim.speed * 0.12 * dt;
    d.spin += dt * 18;
    const sx = d.x - sim.scroll;
    const sy = d.y + Math.sin(sim.time * 3 + d.bob) * 10;
    if (aabb(hx, hy, hw, hh, sx - 16, sy - 10, 32, 20)) {
      crash(sim, "drone");
      return;
    }
  }
  sim.gates = sim.gates.filter((g) => g.x - sim.scroll > -200);
  sim.ramps = sim.ramps.filter((r) => r.lip - sim.scroll > -240);
  sim.drones = sim.drones.filter((d) => !d.dead && d.x - sim.scroll > -80);
  ageBits(sim, dt);
  const score = scoreOf(sim);
  if (score > sim.best) sim.best = score;
}
function crash(sim, kind) {
  sim.phase = "over";
  sim.crash = kind;
  sim.shake = 1;
  sim.boosting = false;
  sim.sinceOver = 0;
  burst(sim, sim.scroll + PLAYER_X + CAR_W * 0.5, sim.y + CAR_H * 0.5, 16);
}
function burst(sim, x, y, n) {
  for (let i = 0; i < n; i++) {
    const a = hash(x + i * 13 + sim.time) * Math.PI * 2;
    const sp = 40 + hash(y + i * 9) * 140;
    sim.dust.push({
      x,
      y,
      vx: Math.cos(a) * sp,
      vy: Math.sin(a) * sp,
      life: 0.4 + hash(i + 2) * 0.3,
      max: 0.7,
      s: 2 + hash(i + 4) * 3
    });
  }
}
function ageBits(sim, dt) {
  for (const d of sim.dust) {
    d.x += d.vx * dt;
    d.y += d.vy * dt;
    d.life -= dt;
  }
  sim.dust = sim.dust.filter((d) => d.life > 0);
  for (const p of sim.popups) p.life -= dt;
  sim.popups = sim.popups.filter((p) => p.life > 0);
  sim.shake = Math.max(0, sim.shake - dt * 1.8);
}
function step(sim, dt, input) {
  const capped = Math.min(dt, 0.05);
  if (sim.phase === "title") {
    sim.speed = 48;
    sim.scroll += sim.speed * capped;
    sim.time += capped;
    sim.y = groundY(sim.scroll + PLAYER_X + CAR_W * 0.55) - CAR_H;
    sim.vy = 0;
    sim.grounded = true;
    sim.boosting = false;
    sim.rot += (0 - sim.rot) * Math.min(1, capped * 4);
    for (let i = 0; i < 2; i++) {
      sim.wheelOmega[i] = sim.speed / WHEELS[i].r;
      sim.wheelAng[i] += sim.wheelOmega[i] * capped;
    }
    ageBits(sim, capped);
    return;
  }
  if (sim.phase === "pause") return;
  if (sim.phase === "over") {
    sim.sinceOver += capped;
    for (let i = 0; i < 2; i++) {
      sim.wheelOmega[i] *= Math.exp(-capped / SPIN_AIR);
      sim.wheelAng[i] += sim.wheelOmega[i] * capped;
    }
    ageBits(sim, capped);
    return;
  }
  let acc = capped;
  const h = 1 / 120;
  let guard = 0;
  let shot = !!input.fire;
  while (acc >= h && guard < 8) {
    if (sim.phase !== "play") break;
    tick(sim, h, { boost: input.boost, fire: shot });
    shot = false;
    acc -= h;
    guard += 1;
  }
}
function draw(ctx, sim, art) {
  const W = VIEW_W;
  const H = VIEW_H;
  ctx.clearRect(0, 0, W, H);
  const sky = art.sky;
  if (sky && sky.complete && sky.naturalWidth > 0) {
    const extra = 0.22;
    const dw = W * (1 + extra);
    const dh = dw * (sky.naturalHeight / sky.naturalWidth);
    const maxPan = dw - W;
    const cycle = sim.scroll * 0.06 % (maxPan * 2);
    const pan = cycle > maxPan ? maxPan * 2 - cycle : cycle;
    const dhUse = Math.max(dh, H * 1.05);
    ctx.drawImage(sky, -pan, Math.min(0, H * 0.42 - dhUse * 0.55), dw, dhUse);
  } else {
    const g = ctx.createLinearGradient(0, 0, 0, H);
    g.addColorStop(0, "#1c2438");
    g.addColorStop(0.55, "#e39a62");
    g.addColorStop(1, "#c4844a");
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, W, H);
  }
  ctx.fillStyle = "rgba(90, 52, 42, 0.55)";
  ctx.beginPath();
  const far = sim.scroll * 0.32;
  ctx.moveTo(0, H);
  for (let x = 0; x <= W; x += 12) {
    const y = H * 0.62 + Math.sin((x + far) * 8e-3) * 16 + Math.sin((x + far) * 27e-4) * 22;
    ctx.lineTo(x, y);
  }
  ctx.lineTo(W, H);
  ctx.fill();
  drawTrees(ctx, sim);
  drawGround(ctx, sim);
  for (const gate of sim.gates) drawGate(ctx, sim, gate);
  for (const d of sim.drones) drawDrone(ctx, sim, d, art);
  drawShots(ctx, sim);
  drawDust(ctx, sim);
  drawCar(ctx, sim, art);
  drawPopups(ctx, sim);
  if (sim.phase === "over") {
    ctx.fillStyle = "rgba(120, 24, 12, 0.22)";
    ctx.fillRect(0, 0, W, H);
  }
  const vig = ctx.createRadialGradient(W / 2, H / 2, W * 0.28, W / 2, H / 2, W * 0.72);
  vig.addColorStop(0, "rgba(0,0,0,0)");
  vig.addColorStop(1, "rgba(16, 10, 8, 0.38)");
  ctx.fillStyle = vig;
  ctx.fillRect(0, 0, W, H);
}
function drawTrees(ctx, sim) {
  const start = Math.floor(sim.scroll / 180) - 1;
  for (let i = start; i < start + 10; i++) {
    if (hash(i * 4.1) < 0.42) continue;
    const wx = i * 180 + hash(i + 2) * 40;
    const sx = wx - sim.scroll;
    if (sx < -40 || sx > VIEW_W + 40) continue;
    if (rampLift(sim, wx) > 8) continue;
    const gy = groundY(wx);
    ctx.fillStyle = "#5a3828";
    ctx.fillRect(sx - 3, gy - 26, 6, 26);
    ctx.fillStyle = "#245536";
    ctx.beginPath();
    ctx.moveTo(sx, gy - 72);
    ctx.lineTo(sx - 20, gy - 22);
    ctx.lineTo(sx + 20, gy - 22);
    ctx.fill();
    ctx.fillStyle = "#3d7a4c";
    ctx.beginPath();
    ctx.moveTo(sx, gy - 90);
    ctx.lineTo(sx - 14, gy - 46);
    ctx.lineTo(sx + 14, gy - 46);
    ctx.fill();
  }
}
function drawGround(ctx, sim) {
  ctx.beginPath();
  ctx.moveTo(0, VIEW_H);
  for (let x = 0; x <= VIEW_W; x += 8) {
    ctx.lineTo(x, surfaceY(sim, sim.scroll + x));
  }
  ctx.lineTo(VIEW_W, VIEW_H);
  ctx.closePath();
  const g = ctx.createLinearGradient(0, VIEW_H * 0.6, 0, VIEW_H);
  g.addColorStop(0, "#e0b27a");
  g.addColorStop(0.35, "#c4844a");
  g.addColorStop(1, "#6a3a22");
  ctx.fillStyle = g;
  ctx.fill();
  ctx.beginPath();
  for (let x = 0; x <= VIEW_W; x += 8) {
    const y = surfaceY(sim, sim.scroll + x);
    if (x === 0) ctx.moveTo(x, y);
    else ctx.lineTo(x, y);
  }
  ctx.strokeStyle = "rgba(74, 40, 22, 0.65)";
  ctx.lineWidth = 3;
  ctx.stroke();
  drawKickers(ctx, sim);
  const i0 = Math.floor(sim.scroll / 28);
  for (let i = i0; i < i0 + 40; i++) {
    if (hash(i) < 0.72) continue;
    const wx = i * 28;
    const sx = wx - sim.scroll;
    const gy = surfaceY(sim, wx);
    ctx.fillStyle = hash(i + 1) > 0.5 ? "#8a5a32" : "#d7b48a";
    ctx.fillRect(sx, gy + 6 + hash(i + 2) * 18, 3 + hash(i + 3) * 5, 2);
  }
}
function drawKickers(ctx, sim) {
  for (const r of sim.ramps) {
    const x0 = r.x0 - sim.scroll;
    const lip = r.lip - sim.scroll;
    if (lip < -40 || x0 > VIEW_W + 40) continue;
    ctx.beginPath();
    ctx.moveTo(x0, groundY(r.x0));
    for (let x = r.x0; x <= r.lip; x += 8) ctx.lineTo(x - sim.scroll, surfaceY(sim, x));
    ctx.lineTo(lip, groundY(r.lip));
    ctx.closePath();
    ctx.fillStyle = "rgba(228, 87, 46, 0.35)";
    ctx.fill();
    ctx.strokeStyle = "#f0b429";
    ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.moveTo(lip, surfaceY(sim, r.lip));
    ctx.lineTo(lip + 16, groundY(r.lip + 12));
    ctx.stroke();
    for (let k = 1; k <= 3; k++) {
      const wx = r.x0 + (r.lip - r.x0) * k / 4;
      const sx = wx - sim.scroll;
      const y = surfaceY(sim, wx) - 10;
      ctx.beginPath();
      ctx.moveTo(sx - 9, y + 7);
      ctx.lineTo(sx + 5, y);
      ctx.lineTo(sx - 9, y - 7);
      ctx.stroke();
    }
  }
}
function drawGate(ctx, sim, gate) {
  const sx = gate.x - sim.scroll;
  if (sx > VIEW_W + 20 || sx + gate.w < -20) return;
  if (gate.blown) return;
  hazard(ctx, sx, 0, gate.w, Math.max(0, gate.gapTop));
  if (!gate.kicker && gate.gapBot < VIEW_H) hazard(ctx, sx, gate.gapBot, gate.w, VIEW_H - gate.gapBot);
  ctx.strokeStyle = "#f0b429";
  ctx.lineWidth = 3;
  ctx.strokeRect(sx + 1.5, Math.max(0, gate.gapTop - 2), gate.w - 3, 4);
  if (gate.gapBot < groundY(gate.x) - 10) {
    ctx.strokeRect(sx + 1.5, gate.gapBot - 2, gate.w - 3, 4);
  }
  const mid = gate.kicker ? gate.gapTop + 110 : (gate.gapTop + Math.min(gate.gapBot, VIEW_H - 8)) / 2;
  ctx.fillStyle = "rgba(240, 180, 41, 0.85)";
  const bob = Math.sin(sim.time * 6) * 3;
  chevron(ctx, sx + gate.w * 0.5, mid + bob);
}
function hazard(ctx, x, y, w, h) {
  if (h <= 0 || w <= 0) return;
  ctx.save();
  ctx.beginPath();
  ctx.rect(x, y, w, h);
  ctx.clip();
  ctx.fillStyle = "rgba(22, 14, 10, 0.78)";
  ctx.fillRect(x, y, w, h);
  ctx.fillStyle = "rgba(240, 180, 41, 0.92)";
  const step2 = 16;
  for (let i = -h; i < w + h; i += step2) {
    ctx.beginPath();
    ctx.moveTo(x + i, y);
    ctx.lineTo(x + i + 8, y);
    ctx.lineTo(x + i + 8 + h, y + h);
    ctx.lineTo(x + i + h, y + h);
    ctx.closePath();
    ctx.fill();
  }
  ctx.restore();
}
function chevron(ctx, x, y) {
  ctx.beginPath();
  ctx.moveTo(x - 10, y - 8);
  ctx.lineTo(x + 2, y);
  ctx.lineTo(x - 10, y + 8);
  ctx.lineWidth = 3;
  ctx.strokeStyle = "#f0b429";
  ctx.stroke();
}
function drawDrone(ctx, sim, d, art) {
  if (d.dead) return;
  const sx = d.x - sim.scroll;
  const sy = d.y + Math.sin(sim.time * 3 + d.bob) * 10;
  if (sx < -60 || sx > VIEW_W + 60) return;
  const img = art.drone;
  ctx.save();
  ctx.translate(sx, sy);
  ctx.rotate(Math.sin(d.spin) * 0.05);
  if (img && img.complete && img.naturalWidth > 0) {
    ctx.drawImage(img, -28, -12, 56, 22);
  } else {
    ctx.fillStyle = "#1a120c";
    ctx.fillRect(-16, -6, 32, 12);
  }
  ctx.restore();
}
function drawShots(ctx, sim) {
  for (const s of sim.shots) {
    const sx = s.x - sim.scroll;
    if (sx < -30 || sx > VIEW_W + 30) continue;
    ctx.save();
    ctx.translate(sx, s.y);
    ctx.fillStyle = "rgba(228, 87, 46, 0.85)";
    ctx.fillRect(-16, -2, 8, 4);
    ctx.fillStyle = "#f3e2c4";
    ctx.beginPath();
    ctx.moveTo(14, 0);
    ctx.lineTo(-8, -4.5);
    ctx.lineTo(-4, 0);
    ctx.lineTo(-8, 4.5);
    ctx.closePath();
    ctx.fill();
    ctx.restore();
  }
}
function drawDust(ctx, sim) {
  for (const d of sim.dust) {
    const a = Math.max(0, d.life / d.max);
    ctx.fillStyle = `rgba(232, 196, 140, ${a * 0.7})`;
    ctx.beginPath();
    ctx.arc(d.x - sim.scroll, d.y, d.s, 0, Math.PI * 2);
    ctx.fill();
  }
}
function drawCar(ctx, sim, art) {
  const crawler = sim.rig === "crawler" && ready(art.crawler);
  ctx.save();
  ctx.translate(PLAYER_X + CAR_W / 2, sim.y + CAR_H / 2);
  ctx.rotate(sim.rot * Math.PI / 180);
  if (sim.boosting) {
    ctx.fillStyle = "rgba(228, 87, 46, 0.9)";
    ctx.beginPath();
    ctx.moveTo(-CAR_W * 0.42, 4);
    ctx.lineTo(-CAR_W * 0.42 - 16 - hash(sim.time * 40) * 14, 10);
    ctx.lineTo(-CAR_W * 0.42, 16);
    ctx.fill();
    ctx.fillStyle = "rgba(240, 180, 41, 0.85)";
    ctx.beginPath();
    ctx.moveTo(-CAR_W * 0.4, 7);
    ctx.lineTo(-CAR_W * 0.4 - 10, 11);
    ctx.lineTo(-CAR_W * 0.4, 14);
    ctx.fill();
  }
  const wheelRear = crawler && ready(art.crawlerWheel) ? art.crawlerWheel : art.wheelRear;
  const wheelFront = crawler && ready(art.crawlerWheel) ? art.crawlerWheel : art.wheelFront;
  const body = crawler ? art.crawler : art.body;
  const wheelsReady = ready(wheelRear) && ready(wheelFront) && ready(body);
  if (wheelsReady) {
    spinWheel(ctx, sim, 0, wheelRear);
    spinWheel(ctx, sim, 1, wheelFront);
  }
  const img = wheelsReady ? body : art.buggy;
  if (img && ready(img)) {
    ctx.drawImage(img, -CAR_W / 2, -CAR_H / 2, CAR_W, CAR_H);
  } else {
    ctx.fillStyle = crawler ? "#9a1b24" : "#e4572e";
    ctx.fillRect(-CAR_W / 2, -CAR_H / 2, CAR_W, CAR_H);
  }
  ctx.restore();
}
function ready(img) {
  return !!img && img.complete && img.naturalWidth > 0;
}
function spinWheel(ctx, sim, index, img) {
  const w = WHEELS[index];
  const sideW = w.imgR * 2 * SX;
  const sideH = w.imgR * 2 * SY;
  ctx.save();
  ctx.translate(w.lx, w.ly);
  ctx.rotate(sim.wheelAng[index]);
  ctx.drawImage(img, -sideW / 2, -sideH / 2, sideW, sideH);
  ctx.restore();
}
function drawPopups(ctx, sim) {
  ctx.save();
  ctx.font = "700 22px 'Barlow Condensed', sans-serif";
  ctx.textAlign = "center";
  for (const p of sim.popups) {
    const a = Math.max(0, p.life / p.max);
    ctx.fillStyle = `rgba(243, 226, 196, ${a})`;
    ctx.fillText(p.text, p.x - sim.scroll, p.y - (1 - a) * 18);
  }
  ctx.restore();
}
function shakeOffset(sim) {
  if (sim.reduced || sim.shake <= 0) return { x: 0, y: 0 };
  const m = sim.shake * 7;
  return { x: (hash(sim.time * 90) - 0.5) * m, y: (hash(sim.time * 130) - 0.5) * m };
}
function aabb(ax, ay, aw, ah, bx, by, bw, bh) {
  return ax < bx + bw && ax + aw > bx && ay < by + bh && ay + ah > by;
}
function clamp(n, a, b) {
  return Math.max(a, Math.min(b, n));
}
function hash(n) {
  const x = Math.sin(n * 127.1 + 311.7) * 43758.5453;
  return x - Math.floor(x);
}
export {
  VERSION,
  VIEW_H,
  VIEW_W,
  createSim,
  draw,
  groundY,
  metersOf,
  scoreOf,
  shakeOffset,
  startRun,
  step,
  wheelPace,
  MISSILE_MAX,
  MISSILE_RELOAD
};
