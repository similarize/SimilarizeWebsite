// The release-specific filename prevents stale CDN copies of the simulation.
const VERSION = "3.21";
const VIEW_W = 960;
const VIEW_H = 540;
const PLAYER_X = 168;
const CAR_W = 118;
const CAR_H = 52;
const GATE_W = 78;
const GRAVITY = 1500;
const HOLD_NET = -900;
const MAX_UP = -420;
const MAX_DOWN = 780;
const IMPULSE = -380;
const TAP_COST = 3;
const HOLD_DRAIN = 22;
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
const FRONT = wheelSpec(600, 234, 82);
const WHEELS = [REAR, FRONT];
const CRAWLER_W = 1440;
const CRAWLER_H = 630;
function wheelSpecBox(ix, iy, imgR, imgW, imgH) {
  const sx = CAR_W / imgW;
  const sy = CAR_H / imgH;
  return {
    lx: (ix - imgW / 2) * sx,
    ly: (iy - imgH / 2) * sy,
    r: imgR * sy,
    imgR,
    sx,
    sy
  };
}
const CRAWLER_WHEELS = [
  wheelSpecBox(322.6, 464.4, 147.8, CRAWLER_W, CRAWLER_H),
  wheelSpecBox(1119.5, 460.5, 151, CRAWLER_W, CRAWLER_H)
];
const PIXEL_W = 1440;
const PIXEL_H = 630;
const PIXEL_WHEEL_X = (329.1 + 1070.2) / 2;
const PIXEL_WHEEL_SPACING = (1070.2 - 329.1) / 2;
const PIXEL_WHEEL_Y = (460.5 + 469.6) / 2;
const PIXEL_WHEEL_R = (158.2 + 154.9) / 2;
const PIXEL_WHEELS = [
  wheelSpecBox(PIXEL_WHEEL_X - PIXEL_WHEEL_SPACING, PIXEL_WHEEL_Y, PIXEL_WHEEL_R, PIXEL_W, PIXEL_H),
  wheelSpecBox(PIXEL_WHEEL_X + PIXEL_WHEEL_SPACING, PIXEL_WHEEL_Y, PIXEL_WHEEL_R, PIXEL_W, PIXEL_H)
];
function keepRig(rig) {
  return rig === "crawler" || rig === "pixel" ? rig : "rally";
}
function defaultTune() {
  return { wheel: 1, chassis: 1, squish: 0, gravity: 1, trailer: 0 };
}
function rigWheels(sim) {
  if (sim.rig === "pixel") return PIXEL_WHEELS;
  if (sim.rig === "crawler") return CRAWLER_WHEELS;
  return WHEELS;
}
function wheelGeom(sim, i) {
  const w = rigWheels(sim)[i];
  const chassis = sim.tune.chassis;
  const drawR = w.r * sim.tune.wheel;
  const squash = sim.squash ? sim.squash[i] : 0;
  return {
    lx: w.lx * chassis,
    ly: w.ly * chassis,
    r: Math.max(3.5, drawR),
    drawR,
    squash,
    imgR: w.imgR,
    sx: w.sx || SX,
    sy: w.sy || SY
  };
}
function rideLow(sim) {
  let low = CAR_H * 0.5 * sim.tune.chassis;
  for (let i = 0; i < 2; i++) {
    const g = wheelGeom(sim, i);
    const bottom = g.ly + g.r;
    if (bottom > low) low = bottom;
  }
  return low;
}
function plantY(sim, worldX) {
  return surfaceY(sim, worldX) - CAR_H * 0.5 - rideLow(sim);
}
function hitchOf(sim) {
  const chassis = sim.tune.chassis;
  const rot = sim.rot * Math.PI / 180;
  const c = Math.cos(rot);
  const s = Math.sin(rot);
  const lx = -CAR_W * 0.48 * chassis;
  const ly = CAR_H * 0.1 * chassis;
  const sx = PLAYER_X + CAR_W / 2 + c * lx - s * ly;
  const sy = sim.y + CAR_H / 2 + s * lx + c * ly;
  return { sx, sy, x: sim.scroll + sx };
}
function stepSquash(sim, dt) {
  for (let i = 0; i < 2; i++) {
    const g = wheelGeom(sim, i);
    const p = wheelWorld(sim, g);
    const sink = p.y + g.r - surfaceY(sim, p.x);
    const planted = !!sim.grounded && sink > -5;
    const give = sim.tune.squish;
    const rest = planted ? clamp(0.012 + give * 0.12 + Math.max(0, sink) / 60, 0, 0.16) : 0;
    const frequency = planted ? 18 : 12;
    const damping = planted ? 0.82 : 1.05;
    const acceleration = (rest - sim.squash[i]) * frequency * frequency
      - 2 * damping * frequency * sim.squashV[i];
    sim.squashV[i] += acceleration * dt;
    sim.squash[i] += sim.squashV[i] * dt;
    if (sim.squash[i] < 0) {
      sim.squash[i] = 0;
      sim.squashV[i] = Math.max(0, sim.squashV[i]);
    } else if (sim.squash[i] > 0.32) {
      sim.squash[i] = 0.32;
      sim.squashV[i] = Math.min(0, sim.squashV[i]);
    }
  }
}
function stepTrailer(sim, dt) {
  const weight = sim.tune.trailer;
  if (weight < 0.04) {
    sim.trailAng += (0 - sim.trailAng) * Math.min(1, dt * 3);
    sim.trailW *= Math.exp(-dt * 4);
    return;
  }
  const hitch = hitchOf(sim);
  const size = trailerScale(weight);
  const axleReach = trailerLength(weight) - 22;
  const mass = 0.45 + weight * 1.7;
  const axleR = 9 * sim.tune.wheel * size;
  const ang = sim.trailAng;
  const axleX = hitch.x - Math.cos(ang) * axleReach;
  const gy = surfaceY(sim, axleX) - axleR;
  const want = clamp((gy - hitch.sy) / axleReach, -0.82, 0.92);
  const target = Math.asin(want);
  const ay = dt > 0 ? (sim.vy - sim.prevVy) / dt : 0;
  const omega = 8.2 / Math.sqrt(mass);
  const damp = 0.22 + 0.12 / mass;
  let w = sim.trailW + clamp(-ay / 900, -7, 7) * dt * (1.4 + mass);
  const acc = omega * omega * (target - ang) - 2 * damp * omega * w;
  w = clamp(w + acc * dt, -7.5, 7.5);
  sim.trailW = w;
  sim.trailAng = clamp(ang + w * dt, -1.05, 1.2);
  const slam = clamp(sim.trailAng - target, 0, 0.7);
  if (slam > 0.08 && sim.trailW > 0.35) {
    sim.vy -= slam * mass * 22;
    sim.rot -= slam * mass * dt * 40;
  } else if (!sim.grounded) {
    sim.vy += weight * 70 * sim.tune.gravity * dt;
  }
}
function trailerScale(weight) {
  return 0.8 + weight * 0.12;
}
function trailerLength(weight) {
  return 102 + weight * 28;
}
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
    if (worldX >= r.x0 && worldX <= r.lip + RAMP_RUN) return r;
  }
  return null;
}
function smoothstep(t) {
  const x = clamp(t, 0, 1);
  return x * x * (3 - 2 * x);
}
function rampLift(sim, worldX) {
  const r = rampAt(sim, worldX);
  if (!r) return 0;
  if (worldX <= r.lip) {
    return r.rise * smoothstep((worldX - r.x0) / (r.lip - r.x0));
  }
  return r.rise * (1 - smoothstep((worldX - r.lip) / RAMP_RUN));
}
function surfaceY(sim, worldX) {
  return groundY(worldX) - rampLift(sim, worldX);
}
function surfacePitchDeg(sim) {
  // Line Rider-style: pitch of dirt under the two contact patches (degrees, nose-up negative in canvas Y-down).
  const g0 = wheelGeom(sim, 0);
  const g1 = wheelGeom(sim, 1);
  const x0 = sim.scroll + PLAYER_X + CAR_W / 2 + g0.lx;
  const x1 = sim.scroll + PLAYER_X + CAR_W / 2 + g1.lx;
  const y0 = surfaceY(sim, x0);
  const y1 = surfaceY(sim, x1);
  return Math.atan2(y1 - y0, Math.max(8, x1 - x0)) * 180 / Math.PI;
}
function createSim(best = 0) {
  const sim = {
    phase: "title",
    scroll: 0,
    y: 0,
    vy: 0,
    rot: 0,
    av: 0,
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
    nextGate: 520,
    nextDrone: 5200,
    stage: "calm",
    gatesCleared: 0,
    best,
    shake: 0,
    crash: null,
    sinceOver: 0,
    time: 0,
    accumulator: 0,
    pendingFire: false,
    muted: false,
    reduced: false,
    squash: [0, 0],
    squashV: [0, 0],
    trailAng: 0.15,
    trailW: 0,
    prevVy: 0,
    damage: 0,
    wasGrounded: true,
    recover: 0,
    dents: [],
    scrapes: 0,
    scraped: 0,
    tune: defaultTune()
  };
  plant(sim);
  return sim;
}
function startRun(sim) {
  const best = sim.best;
  const muted = sim.muted;
  const reduced = sim.reduced;
  const rig = keepRig(sim.rig);
  const tune = sim.tune || defaultTune();
  const fresh = createSim(best);
  fresh.phase = "play";
  fresh.muted = muted;
  fresh.reduced = reduced;
  fresh.rig = rig;
  fresh.tune = tune;
  Object.assign(sim, fresh);
}
function metersOf(sim) {
  if (sim.phase === "title") return 0;
  return Math.max(0, Math.floor(sim.scroll / 20));
}
function scoreOf(sim) {
  return metersOf(sim) + sim.gatesCleared * 100 + sim.scraped * 25;
}
function wheelPace(sim) {
  let sum = 0;
  for (let i = 0; i < 2; i++) sum += sim.wheelOmega[i] * wheelGeom(sim, i).r;
  return sum * 0.5;
}
function stageAt(t) {
  // Retuned 3.12: calm short, weave by ~45s, chase by ~90s, lock/serious by ~180s (~3 min).
  if (t < 45) return "calm";
  if (t < 90) return "pattern";
  if (t < 180) return "hunt";
  return "lock";
}
function eta(sim, worldX) {
  return sim.time + Math.max(0, worldX - sim.scroll) / Math.max(160, cruiseOf(sim));
}
function stageOf(sim) {
  return stageAt(sim.time || 0);
}
function heat(sim, worldX) {
  return clamp(eta(sim, worldX) / 180, 0, 1.8);
}
function dronePos(sim, d) {
  if (d.kind === "pattern") {
    const y = d.y + Math.sin(sim.time * d.freq + d.bob) * d.amp;
    return { x: d.x, y: clamp(y, 42, surfaceY(sim, d.x) - 40) };
  }
  if (d.kind === "lock") return { x: d.x, y: d.y };
  const bob = d.kind === "seek" ? 0 : Math.sin(sim.time * 3 + d.bob) * 10;
  return { x: d.x, y: d.y + bob };
}
function stepDrones(sim, dt) {
  const playerX = sim.scroll + PLAYER_X + CAR_W * 0.45;
  const playerY = sim.y + CAR_H * 0.45;
  for (const d of sim.drones) {
    if (d.dead) continue;
    if (d.kind === "drift") d.x -= sim.speed * 0.12 * dt;
    else if (d.kind === "pattern") d.x -= sim.speed * 0.03 * dt;
    else if (d.kind === "seek") {
      const chase = sim.time < 90 ? 0.35 : sim.time < 180 ? 0.35 + (sim.time - 90) / 90 * 0.75 : 1.35;
      d.x += (playerX + 260 - d.x) * Math.min(1, dt * (0.7 + chase));
      d.y += clamp(playerY - d.y, -140, 140) * dt * chase;
      d.y = clamp(d.y, 40, surfaceY(sim, d.x) - 56);
    }
    d.spin += dt * (d.kind === "seek" ? 26 : 16);
  }
}
function announceStage(sim) {
  const stage = stageOf(sim);
  if (stage === sim.stage) return;
  sim.stage = stage;
  const text = stage === "pattern" ? "WEAVE" : stage === "hunt" ? "THEY CHASE" : stage === "lock" ? "SHOOT THE BEAM" : "";
  if (!text) return;
  sim.popups.push({ x: sim.scroll + 480, y: 78, text, life: 1.5, max: 1.5 });
}
function spawnDrones(sim) {
  let guard = 0;
  while (sim.nextDrone < sim.scroll + VIEW_W + 60 && guard < 6) {
    guard += 1;
    const x = sim.nextDrone;
    const stage = stageAt(eta(sim, x));
    const gyD = groundY(x);
    const nearGate = sim.gates.some((g) => Math.abs(g.x - x) < 200);
    if (stage === "calm") {
      if (!nearGate && gyD > 180 && eta(sim, x) > 22) {
        sim.drones.push({
          kind: "drift",
          x,
          y: 58 + hash(x + 11) * 90,
          bob: hash(x) * Math.PI * 2,
          spin: 0,
          dead: false
        });
      }
      sim.nextDrone += 1700 + hash(x + 3) * 650;
    } else if (stage === "pattern") {
      if (!nearGate) {
        const late = clamp((eta(sim, x) - 45) / 60, 0, 1.4);
        const amp = 64 + late * 36;
        const freq = 0.8 + late * 0.85;
        const y = clamp(gyD * 0.42, 130, 240);
        sim.drones.push({ kind: "pattern", x, y, bob: 0, amp, freq, spin: 0, dead: false });
        sim.drones.push({ kind: "pattern", x, y, bob: Math.PI, amp, freq, spin: 0, dead: false });
      }
      sim.nextDrone += 1550 - Math.min(650, Math.max(0, eta(sim, x) - 45));
    } else if (stage === "hunt") {
      const cap = sim.time < 120 ? 1 : sim.time < 180 ? 2 : 3;
      const hunting = sim.drones.filter((d) => d.kind === "seek" && !d.dead).length;
      if (hunting < cap && !nearGate) {
        sim.drones.push({
          kind: "seek",
          x: x + 60,
          y: 90 + hash(x) * 150,
          bob: 0,
          spin: 0,
          dead: false
        });
      }
      sim.nextDrone += sim.time < 180 ? 1180 : 820;
    } else {
      const blocking = sim.drones.some((d) => d.kind === "lock" && !d.dead && d.x > sim.scroll + 40);
      if (!blocking && !nearGate) {
        sim.drones.push({
          kind: "lock",
          x,
          y: VIEW_H * 0.4,
          bob: 0,
          spin: 0,
          dead: false,
          bumped: false
        });
        sim.nextDrone += sim.time < 240 ? 1850 : 1350;
      } else {
        const cap = sim.time < 240 ? 2 : 3;
        const hunting = sim.drones.filter((d) => d.kind === "seek" && !d.dead).length;
        if (hunting < cap) {
          sim.drones.push({
            kind: "seek",
            x: x + 40,
            y: 80 + hash(x + 6) * 140,
            bob: 0,
            spin: 0,
            dead: false
          });
        }
        sim.nextDrone += 900;
      }
    }
  }
}
function plant(sim) {
  sim.y = groundY(PLAYER_X + CAR_W * 0.5) - CAR_H;
}
function cruiseOf(sim) {
  const t = sim.time || 0;
  if (t < 180) return 215 + t * 0.38;
  return Math.min(430, 283 + (t - 180) * 0.72);
}
function randKind(sim, worldX) {
  const t = eta(sim, worldX);
  const r = hash(worldX);
  // Brief flat spawn, then hops in the first minute; climbs meaningfully by ~1–2 min.
  if (t < 14) return "drive";
  if (t < 45) return r < 0.26 ? "drive" : "hop";
  if (t < 110) {
    if (r < 0.16) return "drive";
    if (r < 0.66) return "hop";
    return "climb";
  }
  if (r < 0.1) return "drive";
  if (r < 0.46) return "hop";
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
  const kind = kicker ? "hop" : randKind(sim, worldX);
  const t = eta(sim, worldX);
  const opening = t < 180 ? 208 - t * 0.3 : Math.max(108, 154 - (t - 180) * 0.24);
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
    const lift = 16 + heat(sim, worldX) * 42;
    gapBot = hi - lift;
    gapTop = gapBot - opening;
  } else {
    const lift = 70 + heat(sim, worldX) * 48;
    gapBot = hi - lift;
    gapTop = gapBot - opening;
  }
  if (gapTop < 22) {
    const shift = 22 - gapTop;
    gapTop += shift;
    gapBot += shift;
  }
  sim.gates.push({ x: worldX, gapTop, gapBot, w: GATE_W, scored: false, kind, kicker, marks: [], bumped: false });
}
function wantKicker(sim, worldX) {
  const t = eta(sim, worldX);
  // Fun amber ramps in the opening minute; still off for the first ~12s so spawn stays fair.
  if (t < 12) return false;
  if (hash(worldX + 8.5) > (t < 90 ? 0.5 : t < 180 ? 0.38 : 0.24)) return false;
  const last = sim.ramps[sim.ramps.length - 1];
  if (last && worldX - last.lip < (t < 60 ? 700 : t < 120 ? 560 : 440)) return false;
  return true;
}
function wrapDeg(a) {
  let x = a % 360;
  if (x > 180) x -= 360;
  if (x < -180) x += 360;
  return x;
}
function stepChassis(sim, dt, input) {
  const impactVy = sim.vy;
  if (input && input.aimPoint && !Number.isFinite(input.aim)) {
    const tx = PLAYER_X + CAR_W / 2;
    const ty = sim.y + CAR_H / 2;
    const dx = input.aimPoint.x - tx;
    const dy = input.aimPoint.y - ty;
    if (dx * dx + dy * dy > 28 * 28) {
      input.aim = dx < -24 && Math.abs(dy) < Math.abs(dx) * 0.55 ? -150 : Math.atan2(dy, dx) * 180 / Math.PI;
    } else {
      input.aimPoint = null;
      sim.aimPoint = null;
    }
  }
  const aiming = input && Number.isFinite(input.aim);
  const rot = sim.rot * Math.PI / 180;
  const c = Math.cos(rot);
  const s = Math.sin(rot);
  let contacts = 0;
  let lift = 0;
  const sinks = [0, 0];
  for (let i = 0; i < 2; i++) {
    const g = wheelGeom(sim, i);
    const p = wheelWorld(sim, g);
    sinks[i] = p.y + g.r - surfaceY(sim, p.x);
    if (sinks[i] > -1.2) contacts += 1;
    if (sinks[i] > 0) {
      lift = Math.max(lift, sinks[i]);
      const rx = c * g.lx - s * g.ly;
      sim.av -= rx * Math.min(sinks[i], 10) * 0.05;
    }
  }
  if (lift > 0) {
    sim.y -= Math.min(lift * 0.8, 9);
    if (sim.vy > 0) sim.vy = Math.min(sim.vy * 0.2, 80);
    sim.vy -= Math.min(lift, 6) * 22;
  }
  // Terrain pitch (Line Rider): when wheels kiss dirt, lean with the surface; aim still steers in air / on dirt.
  const pitch = contacts > 0 ? surfacePitchDeg(sim) : 0;
  const aimTarget = aiming ? (contacts > 0 ? clamp(input.aim, -70, 70) : input.aim) : 0;
  if (aiming) {
    const err = wrapDeg(aimTarget - sim.rot);
    sim.av += clamp(err * 16 - sim.av * 4.2, -560, 560) * dt;
    if (contacts > 0) {
      const camber = wrapDeg(pitch - aimTarget);
      sim.av += clamp(camber * 6, -120, 120) * dt;
    }
  } else if (contacts === 0) {
    const ang = wrapDeg(sim.rot);
    if (Math.abs(ang) < 85) sim.av += (-ang * 48 - sim.av * 13) * dt;
    else sim.av *= Math.exp(-dt * 1.4);
  } else {
    // Follow the dirt curve / off-camber (Line Rider) — spring + direct blend so slopes show lean.
    const err = wrapDeg(pitch - sim.rot);
    const grip = contacts === 2 ? 1 : 0.55;
    sim.av += clamp(err * (26 * grip) - sim.av * (2.6 * grip), -480, 480) * dt;
    sim.rot += err * Math.min(1, dt * (10 * grip));
    // Soft settle — keep lean authority (was exp*8 which killed tilt).
    sim.av *= Math.exp(-dt * (contacts === 2 ? 2.4 : 1.4));
  }
  sim.av = clamp(sim.av, -360, 360);
  sim.rot = wrapDeg(sim.rot + sim.av * dt);
  if (sim.phase === "play" && contacts > 0) {
    const lean = wrapDeg(sim.rot);
    const landed = !sim.wasGrounded;
    if (landed && Math.abs(lean) > 100) {
      sim.wasGrounded = true;
      crash(sim, "flip");
      return true;
    }
    if (landed) {
      const impact = clamp((impactVy - 80) / 900, 0, 0.32) * (0.35 + sim.tune.squish * 0.65);
      for (let i = 0; i < 2; i++) {
        if (sinks[i] > -1.2) {
          sim.squash[i] = clamp(sim.squash[i] + impact, 0, 0.32);
          sim.squashV[i] = 0;
        }
      }
    }
    sim.rot = clamp(lean, -70, 70);
    if (sim.rot >= 70 && sim.av > 0) sim.av = 0;
    if (sim.rot <= -70 && sim.av < 0) sim.av = 0;
    sim.wasGrounded = true;
  } else if (contacts === 0) {
    sim.wasGrounded = false;
  }
  sim.grounded = contacts > 0;
  if (contacts === 2 && !aiming) {
    // Settle chassis onto both contact patches so it rides the dirt instead of skating flat.
    const g0 = wheelGeom(sim, 0);
    const g1 = wheelGeom(sim, 1);
    const p0 = wheelWorld(sim, g0);
    const p1 = wheelWorld(sim, g1);
    const want0 = surfaceY(sim, p0.x) - g0.r;
    const want1 = surfaceY(sim, p1.x) - g1.r;
    const wantY = (want0 + want1) * 0.5 - CAR_H / 2;
    sim.y += (wantY - sim.y) * Math.min(1, dt * 14);
    if (sim.vy > 0) sim.vy *= 0.35;
  }
  return false;
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
  const loads = [0, 0];
  let loaded = false;
  for (let i = 0; i < 2; i++) {
    const g = wheelGeom(sim, i);
    const p = wheelWorld(sim, g);
    const sink = p.y + g.r - surfaceY(sim, p.x);
    // Soft contact band — brief lifts should not freefall-spin one tire while the other rolls.
    loads[i] = sink <= -2.4 ? 0 : clamp((sink + 2.4) / 2.4, 0.18, 1);
    if (loads[i] > 0) loaded = true;
  }
  const shared = loaded ? Math.max(loads[0], loads[1], 0.4) : 0;
  for (let i = 0; i < 2; i++) {
    const g = wheelGeom(sim, i);
    const p = wheelWorld(sim, g);
    const R = Math.max(4, g.r);
    const targetOmega = sim.speed / R;
    if (!loaded) {
      sim.wheelOmega[i] *= Math.exp(-dt / SPIN_AIR);
      sim.wheelAng[i] += sim.wheelOmega[i] * dt;
      continue;
    }
    const load = loads[i] > 0 ? loads[i] : shared * 0.85;
    const I = 0.28 * R * R;
    const slip = sim.speed - sim.wheelOmega[i] * R;
    const force = clamp(slip * load / 0.72, -260, 260);
    sim.speed -= force * dt * 0.2;
    // Strong rolling sync so front/rear track road speed together (no stuck / reverse look).
    sim.wheelOmega[i] += force * R / I * dt;
    sim.wheelOmega[i] += (targetOmega - sim.wheelOmega[i]) * Math.min(1, dt * (16 * load));
    if (i === 0) sim.speed += (cruise - sim.speed) * dt * (sim.recover > 0 ? 6.5 : 1.35) * load;
    if (Math.abs(slip) > 55 && load > 0.3 && sim.dust.length < 80 && hash(sim.time * 900 + i * 19) > 0.62) {
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
  if (!loaded && sim.recover > 0) sim.speed += (cruise - sim.speed) * dt * 6;
  sim.speed = clamp(sim.speed, sim.recover > 0 ? -40 : 36, 420);
  if (loaded) {
    for (let i = 0; i < 2; i++) {
      const R = Math.max(4, wheelGeom(sim, i).r);
      sim.wheelOmega[i] = sim.speed / R;
      sim.wheelAng[i] += sim.wheelOmega[i] * dt;
    }
  }
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
  sim.aimPoint = input && input.aimPoint ? input.aimPoint : null;
  const want = input.boost && sim.phase === "play";
  const can = want && sim.battery > 0.4;
  if (can) {
    if (!sim.wasBoost) {
      sim.vy = Math.min(sim.vy, IMPULSE);
      sim.battery -= TAP_COST;
    }
    sim.vy += HOLD_NET / (1 + sim.tune.trailer * 0.28) * dt;
    sim.battery -= HOLD_DRAIN * dt;
    sim.boosting = true;
  } else {
    if (sim.grounded) sim.battery = Math.min(100, sim.battery + RECHARGE * dt);
    else sim.vy += GRAVITY * sim.tune.gravity * dt;
    sim.boosting = false;
  }
  sim.vy = clamp(sim.vy, sim.loft > 0 ? -780 : MAX_UP, MAX_DOWN);
  if (sim.loft > 0) sim.loft = Math.max(0, sim.loft - dt);
  sim.y += sim.vy * dt;
  sim.battery = clamp(sim.battery, 0, 100);
  sim.wasBoost = want;
  const footX = sim.scroll + PLAYER_X + CAR_W * 0.55;
  const gy = surfaceY(sim, footX);
  if (stepChassis(sim, dt, input)) return;
  const launched = kickRamp(sim, footX, dt);
  if (launched) sim.grounded = false;
  if (sim.y < 14) {
    sim.y = 14;
    if (sim.vy < 0) sim.vy = 0;
  }
  coupleWheels(sim, dt);
  stepSquash(sim, dt);
  stepTrailer(sim, dt);
  sim.prevVy = sim.vy;
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
      if (d.kind === "lock") {
        if (Math.abs(shot.x - d.x) < 22) {
          d.dead = true;
          shot.life = 0;
          sim.booms += 1;
          burst(sim, d.x, shot.y, 16);
          sim.popups.push({ x: d.x, y: shot.y, text: "DOWN", life: 0.7, max: 0.7 });
          break;
        }
        continue;
      }
      const p = dronePos(sim, d);
      if (Math.hypot(shot.x - p.x, shot.y - p.y) < 26) {
        d.dead = true;
        shot.life = 0;
        sim.booms += 1;
        burst(sim, p.x, p.y, 12);
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
    const pace = sim.time < 180 ? 2.05 - sim.time * 0.0032 : Math.max(1.18, 1.55 - (sim.time - 180) * 0.0024);
    const spacing = cruiseOf(sim) * pace;
    sim.nextGate += spacing;
  }
  announceStage(sim);
  spawnDrones(sim);
  stepDrones(sim, dt);
  const s = sim.tune.chassis;
  const halfW = (CAR_W - 28) * s / 2;
  const halfH = (CAR_H - 12) * s / 2;
  const angle = sim.rot * Math.PI / 180;
  const hw = Math.abs(Math.cos(angle)) * halfW + Math.abs(Math.sin(angle)) * halfH;
  const hh = Math.abs(Math.sin(angle)) * halfW + Math.abs(Math.cos(angle)) * halfH;
  const hx = PLAYER_X + CAR_W / 2 - hw;
  const hy = sim.y + CAR_H / 2 - hh;
  for (const g of sim.gates) {
    const sx = g.x - sim.scroll;
    if (!g.blown && hx + hw > sx && hx < sx + g.w && gateBlocks(g, hy, hh)) {
      const overlap = hx + hw - sx;
      if (overlap > 0) sim.scroll -= overlap;
      if (!g.bumped) bumpGate(sim, g, hy, hh, g.gapTop > 4 && hy < g.gapTop);
    }
    if (!g.scored && hx > sx + g.w) {
      g.scored = true;
      const scraped = g.bumped && !g.blown;
      if (scraped) sim.scraped += 1;
      else sim.gatesCleared += 1;
      sim.popups.push({
        x: g.x + g.w,
        y: (g.gapTop + Math.min(g.gapBot, VIEW_H - 20)) * 0.5,
        text: g.blown ? "CLEAR" : scraped ? "SCRAPE" : g.kicker && !sim.grounded ? "CLEAN" : "+100",
        life: 0.8,
        max: 0.8
      });
    }
  }
  for (const d of sim.drones) {
    if (d.dead) continue;
    const p = dronePos(sim, d);
    const sx = p.x - sim.scroll;
    if (!aabb(hx, hy, hw, hh, sx - 16, p.y - 10, 32, 20)) continue;
    const overlap = hx + hw - (sx - 16);
    if (overlap > 0) sim.scroll -= overlap;
    if (!d.bumped) {
      d.bumped = true;
      sim.speed = Math.min(sim.speed, 64);
      sim.recover = 0.16;
      sim.shake = 0.28;
      const hit = d.kind === "lock" ? 16 : 28;
      hurt(sim, hit);
      sim.popups.push({
        x: d.x,
        y: hy,
        text: sim.phase === "over" ? "WRECK" : d.kind === "lock" ? "SHOOT" : "HIT",
        life: 0.7,
        max: 0.7
      });
      if (sim.phase !== "play") return;
    }
  }
  if (sim.grounded && sim.speed < cruiseOf(sim) - 6) {
    const cruise = cruiseOf(sim);
    sim.speed += (cruise - sim.speed) * dt * (sim.recover > 0 ? 8 : 0.7);
  }
  if (sim.recover > 0) sim.recover = Math.max(0, sim.recover - dt);
  sim.gates = sim.gates.filter((g) => g.x - sim.scroll > -200);
  sim.ramps = sim.ramps.filter((r) => r.lip - sim.scroll > -240);
  sim.drones = sim.drones.filter((d) => !d.dead && d.x - sim.scroll > -120);
  ageBits(sim, dt);
  const score = scoreOf(sim);
  if (score > sim.best) sim.best = score;
}
function gateBlocks(g, hy, hh) {
  const hitsCeiling = g.gapTop > 4 && hy < g.gapTop;
  const hitsFloor = g.gapBot < VIEW_H && hy + hh > g.gapBot;
  return hitsCeiling || hitsFloor;
}
function hurt(sim, amount) {
  sim.damage = Math.min(100, sim.damage + amount);
  if (sim.damage >= 100 && sim.phase === "play") crash(sim, "wreck");
}
function bumpGate(sim, g, hy, hh, topHit) {
  const impact = Math.max(0, sim.speed);
  g.bumped = true;
  if (g.marks.length < 5) {
    const y = topHit
      ? clamp(hy + hh * 0.35, 6, Math.max(10, g.gapTop - 6))
      : clamp(hy + hh * 0.55, g.gapBot + 2, VIEW_H - 8);
    g.marks.push({
      y,
      h: 3 + hash(sim.time * 9 + g.marks.length) * 6,
      w: 6 + hash(sim.time * 4) * 12
    });
  }
  if (sim.dents.length < 8) {
    sim.dents.push({
      x: (hash(sim.time * 17 + sim.dents.length) - 0.25) * CAR_W * 0.42,
      y: (hash(sim.time * 29 + sim.dents.length) - 0.5) * CAR_H * 0.32,
      n: 0.55 + hash(sim.time * 5) * 0.7
    });
  }
  sim.speed = Math.min(sim.speed, 70);
  if (topHit) sim.vy = Math.max(sim.vy, 80);
  else sim.vy = Math.min(sim.vy, -110);
  sim.rot += topHit ? 2 : -2;
  sim.recover = 0.16;
  sim.shake = Math.min(0.45, 0.12 + impact / 1400);
  sim.scrapes += 1;
  burst(sim, g.x + 6, hy + hh * 0.5, 7);
  const add = 7 + impact * 0.045 + (impact > 280 ? 14 : 0);
  hurt(sim, add);
  sim.popups.push({
    x: g.x + g.w * 0.5,
    y: hy,
    text: sim.phase === "over" ? "WRECK" : "BUMP",
    life: 0.5,
    max: 0.5
  });
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
  const capped = clamp(Number.isFinite(dt) ? dt : 0, 0, 0.05);
  if (sim.phase === "title") {
    sim.speed = 48;
    sim.scroll += sim.speed * capped;
    sim.time += capped;
    sim.grounded = true;
    sim.boosting = false;
    // Title parade: lean with dirt (Line Rider pitch) — do not flatten to 0.
    const pitchTitle = surfacePitchDeg(sim);
    sim.rot += wrapDeg(pitchTitle - sim.rot) * Math.min(1, capped * 10);
    sim.av = 0;
    // Plant both contact patches after pitch so wheels kiss the slope.
    {
      const g0 = wheelGeom(sim, 0);
      const g1 = wheelGeom(sim, 1);
      const p0 = wheelWorld(sim, g0);
      const p1 = wheelWorld(sim, g1);
      const want0 = surfaceY(sim, p0.x) - g0.r;
      const want1 = surfaceY(sim, p1.x) - g1.r;
      const wantY = (want0 + want1) * 0.5 - CAR_H / 2;
      sim.y = wantY;
    }
    sim.vy = 0;
    for (let i = 0; i < 2; i++) {
      const rad = Math.max(4, wheelGeom(sim, i).drawR);
      sim.wheelOmega[i] = sim.speed / rad;
      sim.wheelAng[i] += sim.wheelOmega[i] * capped;
    }
    stepSquash(sim, capped);
    stepTrailer(sim, capped);
    sim.prevVy = 0;
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
  sim.accumulator += capped;
  sim.pendingFire = sim.pendingFire || !!input.fire;
  const h = 1 / 120;
  let guard = 0;
  while (sim.accumulator + 1e-10 >= h && guard < 8) {
    if (sim.phase !== "play") break;
    const shot = sim.pendingFire;
    sim.pendingFire = false;
    tick(sim, h, { boost: input.boost, fire: shot, aim: input.aim, aimPoint: input.aimPoint });
    sim.accumulator -= h;
    guard += 1;
  }
  if (sim.accumulator < 1e-10) sim.accumulator = 0;
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
  drawTrailer(ctx, sim);
  drawCar(ctx, sim, art);
  drawAim(ctx, sim);
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
  for (let x = 0; x <= VIEW_W; x += 4) {
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
  for (let x = 0; x <= VIEW_W; x += 4) {
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
    const exit = r.lip + RAMP_RUN;
    const end = exit - sim.scroll;
    if (end < -40 || x0 > VIEW_W + 40) continue;
    ctx.beginPath();
    ctx.moveTo(x0, groundY(r.x0));
    for (let x = r.x0; x <= exit; x += 4) ctx.lineTo(x - sim.scroll, surfaceY(sim, x));
    ctx.lineTo(end, groundY(exit));
    ctx.closePath();
    const dirt = ctx.createLinearGradient(0, VIEW_H * 0.62, 0, VIEW_H);
    dirt.addColorStop(0, "rgba(228, 87, 46, 0.42)");
    dirt.addColorStop(1, "rgba(105, 55, 29, 0.12)");
    ctx.fillStyle = dirt;
    ctx.fill();
    ctx.beginPath();
    for (let x = r.x0; x <= exit; x += 4) {
      const sx = x - sim.scroll;
      const sy = surfaceY(sim, x);
      if (x === r.x0) ctx.moveTo(sx, sy);
      else ctx.lineTo(sx, sy);
    }
    ctx.strokeStyle = "rgba(240, 180, 41, 0.85)";
    ctx.lineWidth = 2;
    ctx.stroke();
    ctx.strokeStyle = "#f0b429";
    ctx.lineWidth = 3;
    ctx.beginPath();
    const lipY = surfaceY(sim, r.lip);
    ctx.moveTo(lip, surfaceY(sim, r.lip));
    ctx.lineTo(lip, lipY - 16);
    ctx.lineTo(lip + 10, lipY - 10);
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
function gateLabel(gate) {
  if (gate.kicker) return "KICK";
  if (gate.kind === "drive") return "DRIVE";
  if (gate.kind === "climb") return "CLIMB";
  return "HOP";
}
function drawGate(ctx, sim, gate) {
  const sx = gate.x - sim.scroll;
  if (sx > VIEW_W + 20 || sx + gate.w < -20) return;
  if (gate.blown) return;
  hazard(ctx, sx, 0, gate.w, Math.max(0, gate.gapTop));
  if (!gate.kicker && gate.gapBot < VIEW_H) hazard(ctx, sx, gate.gapBot, gate.w, VIEW_H - gate.gapBot);
  if (gate.marks) {
    for (const m of gate.marks) {
      ctx.fillStyle = "rgba(28, 14, 10, 0.82)";
      ctx.fillRect(sx + 5, m.y, gate.w - 10, m.h);
      ctx.fillStyle = "rgba(243, 226, 196, 0.55)";
      ctx.fillRect(sx + 7, m.y + 1, Math.min(m.w, gate.w - 14), 2);
    }
  }
  const openTop = Math.max(0, gate.gapTop);
  const openBot = Math.min(gate.gapBot, VIEW_H);
  const openH = openBot - openTop;
  ctx.strokeStyle = "#f0b429";
  ctx.lineWidth = 3;
  if (openH > 8) ctx.strokeRect(sx + 1.5, openTop + 1.5, gate.w - 3, openH - 3);
  ctx.strokeRect(sx + 1.5, Math.max(0, gate.gapTop - 2), gate.w - 3, 4);
  if (gate.gapBot < groundY(gate.x) - 10) {
    ctx.strokeRect(sx + 1.5, gate.gapBot - 2, gate.w - 3, 4);
  }
  ctx.font = "700 18px 'Barlow Condensed', sans-serif";
  ctx.textAlign = "center";
  ctx.fillStyle = "#f3e2c4";
  ctx.fillText(gateLabel(gate), sx + gate.w * 0.5, Math.max(18, openTop - 8));
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
  const p = dronePos(sim, d);
  const sx = p.x - sim.scroll;
  if (sx < -80 || sx > VIEW_W + 80) return;
  // Lock drones used to paint a full-height red/amber beam (looked like a rogue floating rectangle).
  // Keep the drone body only; aiming stays via drone position / shots.
  ctx.save();
  ctx.translate(sx, p.y);
  ctx.rotate(Math.sin(d.spin) * 0.05);
  const img = art.drone;
  if (img && img.complete && img.naturalWidth > 0) {
    ctx.drawImage(img, -28, -12, 56, 22);
  } else {
    ctx.fillStyle = d.kind === "seek" ? "#e4572e" : d.kind === "lock" ? "#f0b429" : "#1a120c";
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
  const pixel = sim.rig === "pixel" && ready(art.pixel);
  ctx.save();
  ctx.translate(PLAYER_X + CAR_W / 2, sim.y + CAR_H / 2);
  ctx.rotate(sim.rot * Math.PI / 180);
  const s = sim.tune.chassis;
  if (pixel) ctx.imageSmoothingEnabled = false;
  if (sim.boosting) {
    const tail = -CAR_W * 0.42 * s;
    ctx.fillStyle = "rgba(228, 87, 46, 0.9)";
    ctx.beginPath();
    ctx.moveTo(tail, 4);
    ctx.lineTo(tail - 16 - hash(sim.time * 40) * 14, 10);
    ctx.lineTo(tail, 16);
    ctx.fill();
    ctx.fillStyle = "rgba(240, 180, 41, 0.85)";
    ctx.beginPath();
    ctx.moveTo(tail + 2, 7);
    ctx.lineTo(tail - 8, 11);
    ctx.lineTo(tail + 2, 14);
    ctx.fill();
  }
  const wheelRear = pixel && ready(art.pixelWheel) ? art.pixelWheel : crawler && ready(art.crawlerWheel) ? art.crawlerWheel : art.wheelRear;
  const wheelFront = pixel && ready(art.pixelWheel) ? art.pixelWheel : crawler && ready(art.crawlerWheel) ? art.crawlerWheel : art.wheelFront;
  const body = pixel ? art.pixel : crawler ? art.crawler : art.body;
  const atlasReady = ready(art.wheelAtlas);
  const wheelsReady = ready(body) && (atlasReady || ready(wheelRear) && ready(wheelFront));
  if (wheelsReady) {
    if (atlasReady) {
      const atlasRow = pixel ? 3 : crawler ? 2 : -1;
      spinWheel(ctx, sim, 0, art.wheelAtlas, atlasRow);
      spinWheel(ctx, sim, 1, art.wheelAtlas, atlasRow);
    } else {
      spinWheel(ctx, sim, 0, wheelRear);
      spinWheel(ctx, sim, 1, wheelFront);
    }
  }
  const img = wheelsReady ? body : art.buggy;
  if (img && ready(img)) {
    ctx.drawImage(img, -CAR_W / 2 * s, -CAR_H / 2 * s, CAR_W * s, CAR_H * s);
  } else {
    ctx.fillStyle = pixel ? "#d42828" : crawler ? "#9a1b24" : "#e4572e";
    ctx.fillRect(-CAR_W / 2 * s, -CAR_H / 2 * s, CAR_W * s, CAR_H * s);
  }
  // Damage used to paint a translucent rear fillRect (read as a see-through square that followed
  // every skin). Dents alone carry the beat-up look without a rogue panel.
  if (sim.dents.length) {
    ctx.strokeStyle = "rgba(48, 22, 14, 0.9)";
    ctx.lineWidth = 1.4;
    ctx.lineCap = "round";
    for (const d of sim.dents) {
      ctx.beginPath();
      ctx.moveTo(d.x - 5 * d.n, d.y);
      ctx.lineTo(d.x + 3 * d.n, d.y + 1.5);
      ctx.lineTo(d.x + 7 * d.n, d.y - 1);
      ctx.stroke();
    }
  }
  ctx.restore();
}
function ready(img) {
  return !!img && img.complete && img.naturalWidth > 0;
}
function spinWheel(ctx, sim, index, img, atlasRow) {
  const g = wheelGeom(sim, index);
  const squash = clamp(g.squash, 0, 0.32);
  const bulge = 1 + squash * 0.2;
  const flat = 1 - squash * 0.42;
  const ang = sim.wheelAng[index];
  ctx.save();
  ctx.translate(g.lx, g.ly + g.drawR * (1 - flat));
  ctx.scale(bulge, flat);
  const atlas = atlasRow !== undefined;
  if (atlas) {
    const row = atlasRow < 0 ? index : atlasRow;
    const frames = 180;
    const frameSize = 48;
    const angle = ((ang % (Math.PI * 2)) + Math.PI * 2) % (Math.PI * 2);
    const frame = Math.round(angle / (Math.PI * 2 / frames)) % frames;
    ctx.imageSmoothingEnabled = sim.rig !== "pixel";
    ctx.drawImage(img, frame * frameSize, row * frameSize, frameSize, frameSize, -g.drawR, -g.drawR, g.drawR * 2, g.drawR * 2);
  } else {
    ctx.rotate(ang);
    ctx.beginPath();
    ctx.arc(0, 0, Math.max(0.5, g.drawR * 0.98), 0, Math.PI * 2);
    ctx.clip();
    ctx.drawImage(img, -g.drawR, -g.drawR, g.drawR * 2, g.drawR * 2);
  }
  ctx.restore();
}
function drawTrailer(ctx, sim) {
  if (sim.tune.trailer < 0.04) return;
  const hitch = hitchOf(sim);
  const size = trailerScale(sim.tune.trailer);
  const L = trailerLength(sim.tune.trailer);
  const ang = sim.trailAng;
  const wheelR = 8.5 * sim.tune.wheel * size;
  const squash = clamp(Math.max(0, Math.sin(ang)) * sim.tune.squish * 0.4, 0, 0.2);
  ctx.save();
  ctx.translate(hitch.sx, hitch.sy);
  ctx.rotate(-ang);
  ctx.scale(1, size);
  ctx.fillStyle = "#2c241c";
  ctx.fillRect(-28, -3, 26, 5);
  const nose = -34;
  const tail = -L + 6;
  const top = -36;
  const bot = 7;
  const bodyW = nose - tail;
  ctx.fillStyle = "#27313b";
  ctx.fillRect(tail - 3, top - 5, bodyW + 7, 9);
  ctx.fillStyle = "#eee9dc";
  ctx.fillRect(tail, top + 4, bodyW, bot - top - 4);
  ctx.strokeStyle = "#59636a";
  ctx.lineWidth = 1.5;
  ctx.strokeRect(tail, top + 4, bodyW, bot - top - 4);
  ctx.fillStyle = "#c64c34";
  ctx.fillRect(tail, top + 11, bodyW, 4);
  ctx.fillStyle = "#e1a43a";
  ctx.fillRect(tail, top + 16, bodyW, 2);
  ctx.fillStyle = "#77858b";
  ctx.fillRect(tail + 4, top + 6, bodyW - 8, 1);

  function camperWindow(x, y, w, h) {
    ctx.fillStyle = "#34434b";
    ctx.fillRect(x - 1.5, y - 1.5, w + 3, h + 3);
    ctx.fillStyle = "#78b8c4";
    ctx.fillRect(x, y, w, h);
    ctx.fillStyle = "rgba(239, 250, 246, 0.72)";
    ctx.fillRect(x + 2, y + 2, Math.max(2, w * 0.22), h - 4);
    ctx.fillStyle = "#526e75";
    ctx.fillRect(x + w * 0.48, y, 1, h);
  }

  if (bodyW > 65) camperWindow(tail + 16, top + 21, 18, 11);
  if (bodyW > 100) camperWindow(tail + 40, top + 21, 18, 11);
  if (bodyW > 128) camperWindow(tail + 64, top + 21, 18, 11);

  const doorX = nose - 32;
  ctx.fillStyle = "#c8c4b9";
  ctx.fillRect(doorX, top + 19, 23, 23);
  ctx.strokeStyle = "#59636a";
  ctx.lineWidth = 1;
  ctx.strokeRect(doorX, top + 19, 23, 23);
  camperWindow(doorX + 4, top + 22, 15, 8);
  ctx.fillStyle = "#758087";
  ctx.fillRect(doorX + 3, top + 32, 17, 1.5);
  ctx.fillStyle = "#303940";
  ctx.fillRect(doorX + 19, top + 34, 1.5, 2);
  ctx.fillStyle = "#566168";
  ctx.fillRect(doorX + 1, bot + 1, 26, 3);
  ctx.fillStyle = "#272d31";
  ctx.fillRect(tail - 2, top + 4, 3, bot - top - 4);
  ctx.fillRect(tail + 4, top + 4, 1, bot - top - 4);
  ctx.fillStyle = "#e34335";
  ctx.fillRect(tail - 4, bot - 4, 4, 5);
  ctx.fillStyle = "#f2c34d";
  ctx.fillRect(tail - 4, bot - 10, 4, 4);
  ctx.fillStyle = "#aeb6b6";
  ctx.fillRect(tail + bodyW * 0.46, top - 9, 24, 4);
  ctx.fillStyle = "#59636a";
  ctx.fillRect(tail + bodyW * 0.46 + 3, top - 11, 18, 2);
  ctx.save();
  ctx.translate(-(L - 22), 8);
  ctx.scale(1, 1 / size);
  ctx.rotate(sim.wheelAng[0]);
  ctx.scale(1 + squash * 0.05, 1 - squash * 0.08);
  ctx.beginPath();
  ctx.arc(0, 0, wheelR, 0, Math.PI * 2);
  ctx.fillStyle = "#16110e";
  ctx.fill();
  ctx.beginPath();
  ctx.arc(0, 0, wheelR * 0.42, 0, Math.PI * 2);
  ctx.fillStyle = "#d5dbe3";
  ctx.fill();
  ctx.strokeStyle = "#69747c";
  ctx.lineWidth = Math.max(1, wheelR * 0.1);
  ctx.beginPath();
  ctx.moveTo(-wheelR * 0.29, 0);
  ctx.lineTo(wheelR * 0.29, 0);
  ctx.moveTo(0, -wheelR * 0.29);
  ctx.lineTo(0, wheelR * 0.29);
  ctx.stroke();
  ctx.beginPath();
  ctx.arc(0, 0, wheelR * 0.12, 0, Math.PI * 2);
  ctx.fillStyle = "#313b42";
  ctx.fill();
  ctx.restore();
  ctx.restore();
}
function drawAim(ctx, sim) {
  const p = sim.aimPoint;
  if (!p || sim.phase !== "play") return;
  const tx = PLAYER_X + CAR_W / 2;
  const ty = sim.y + CAR_H / 2;
  ctx.save();
  ctx.strokeStyle = "rgba(240, 180, 41, 0.95)";
  ctx.fillStyle = "rgba(240, 180, 41, 0.9)";
  ctx.lineWidth = 3;
  ctx.beginPath();
  ctx.moveTo(tx, ty);
  ctx.lineTo(p.x, p.y);
  ctx.stroke();
  ctx.beginPath();
  ctx.arc(p.x, p.y, 11, 0, Math.PI * 2);
  ctx.stroke();
  ctx.beginPath();
  ctx.arc(p.x, p.y, 4, 0, Math.PI * 2);
  ctx.fill();
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
  stageOf,
  wheelPace,
  MISSILE_MAX,
  MISSILE_RELOAD
};
