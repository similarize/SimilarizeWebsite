/**
 * Soccer RC 3D — Rocket League–lite arcade cabinet.
 * Vanilla Three.js (CDN) + simple custom physics.
 * Controls: phone stick=steer · FWD/REV (dbl-tap FWD=boost, auto-kick) · KB/pad A kick · B boost.
 * Online: same CF Worker rooms as flat Soccer RC (Create/Join two-phone).
 */
import * as THREE from "three";

const CACHE = "20261006-soccerrc3d7";
const HALF_X = 22;
const HALF_Z = 14;
const WALL_H = 5.5;
const GOAL_W = 7.2;   // half-width along Z
const GOAL_H = 4.2;
const GOAL_DEPTH = 3.2;
const BALL_R = 0.55;
const CAR_HALF = { x: 0.85, y: 0.38, z: 0.55 };
const GRAVITY = 28;
const BALL_BOUNCE = 0.62;
const BALL_FRICTION = 0.992;
const CAR_ACCEL = 38;
const CAR_MAX = 18;
const CAR_BOOST_MAX = 32;
const CAR_DRAG = 0.965;
const TURN_RATE = 2.8;
const BOOST_DRAIN = 0.55;   // per second
const BOOST_REGEN = 0.22;
const KICK_RANGE = 2.4;
const KICK_IMPULSE = 16;
const JUMP_VY = 9.5;


// --- Lightweight kid-friendly audio (Web Audio synth; no asset packs) ---
const MUSIC_LS_KEY = "soccerRc3dMusic";
let audioCtx = null;
let musicOn = false;
try {
  musicOn = localStorage.getItem(MUSIC_LS_KEY) === "1";
} catch (_) {}
let musicTimer = null;
let musicOscA = null, musicOscB = null, musicGain = null;
let musicNote = 0;
const musicNotes = [262, 294, 330, 349, 392, 330, 294, 262]; // soft C-major walk
let sfxCrashAt = 0;
let sfxBallAt = 0;

function ensureAudio() {
  if (!audioCtx) {
    const AC = window.AudioContext || window.webkitAudioContext;
    if (!AC) return null;
    audioCtx = new AC();
  }
  if (audioCtx.state === "suspended") {
    try { audioCtx.resume(); } catch (_) {}
  }
  return audioCtx;
}

function playTone(freq, dur, type, vol) {
  const ctx = ensureAudio();
  if (!ctx) return;
  const t0 = ctx.currentTime;
  const osc = ctx.createOscillator();
  const g = ctx.createGain();
  osc.type = type || "sine";
  osc.frequency.value = freq;
  g.gain.setValueAtTime(Math.max(0.001, vol || 0.08), t0);
  g.gain.exponentialRampToValueAtTime(0.001, t0 + dur);
  osc.connect(g);
  g.connect(ctx.destination);
  osc.start(t0);
  osc.stop(t0 + dur + 0.02);
}

function sfxCrash() {
  const now = performance.now();
  if (now - sfxCrashAt < 180) return;
  sfxCrashAt = now;
  // Soft thud + tiny clack — kid-friendly, not harsh
  playTone(140, 0.12, "triangle", 0.11);
  playTone(220, 0.07, "sine", 0.07);
}

function sfxBallHit(strength) {
  const now = performance.now();
  if (now - sfxBallAt < 90) return;
  sfxBallAt = now;
  const s = Math.max(0.4, Math.min(1.4, strength || 1));
  playTone(380 + 80 * s, 0.08, "sine", 0.07 * s);
  playTone(520 + 40 * s, 0.05, "triangle", 0.045 * s);
}


// Continuous RC motor hum — pitch/volume follow speed (not one-shots)
const motors = [null, null]; // per-car soft synth chains

function ensureMotor(i) {
  const ctx = ensureAudio();
  if (!ctx) return null;
  if (motors[i]) return motors[i];
  const gain = ctx.createGain();
  gain.gain.value = 0.001;
  // Soft lowpass-ish stack: triangle fundamental + quiet sine harmonic (kid-friendly)
  const osc = ctx.createOscillator();
  const osc2 = ctx.createOscillator();
  osc.type = "triangle";
  osc2.type = "sine";
  const base = 58 + i * 5;
  osc.frequency.value = base;
  osc2.frequency.value = base * 2.01;
  osc.connect(gain);
  osc2.connect(gain);
  gain.connect(ctx.destination);
  osc.start();
  osc2.start();
  motors[i] = { osc, osc2, gain, base };
  return motors[i];
}

function startMotors() {
  ensureMotor(0);
  ensureMotor(1);
}

function updateMotorAudio() {
  if (!cars || cars.length < 2) return;
  const ctx = ensureAudio();
  if (!ctx) return;
  // Don't start oscillators until context is running (after user gesture)
  if (ctx.state !== "running") return;
  const tNow = ctx.currentTime;
  for (let i = 0; i < 2; i++) {
    const m = ensureMotor(i);
    if (!m) continue;
    const c = cars[i];
    const sp = Math.hypot(c.vx, c.vz);
    const drive = Math.min(1, sp / CAR_BOOST_MAX); // 0 idle → 1 top boost speed
    // Idle hum always present once unlocked; rises with accel, falls when slow/stop
    const freq = m.base + drive * 175;          // ~58 → ~233 Hz
    const freq2 = freq * 2.02;
    const vol = 0.014 + drive * 0.05;            // soft; louder when moving
    try {
      m.osc.frequency.setTargetAtTime(freq, tNow, 0.09);
      m.osc2.frequency.setTargetAtTime(freq2, tNow, 0.09);
      m.gain.gain.setTargetAtTime(vol, tNow, 0.12);
    } catch (_) {}
  }
}

function stopMusic() {
  if (musicTimer) {
    clearInterval(musicTimer);
    musicTimer = null;
  }
  try {
    if (musicOscA) musicOscA.stop();
    if (musicOscB) musicOscB.stop();
  } catch (_) {}
  musicOscA = musicOscB = null;
  musicGain = null;
}

function startMusic() {
  const ctx = ensureAudio();
  if (!ctx || !musicOn || musicOscA) return;
  musicGain = ctx.createGain();
  musicGain.gain.value = 0.045; // quiet bed
  musicGain.connect(ctx.destination);
  musicOscA = ctx.createOscillator();
  musicOscB = ctx.createOscillator();
  musicOscA.type = "triangle";
  musicOscB.type = "sine";
  musicOscA.frequency.value = musicNotes[0];
  musicOscB.frequency.value = musicNotes[0] * 2;
  musicOscA.connect(musicGain);
  musicOscB.connect(musicGain);
  musicOscA.start();
  musicOscB.start();
  musicNote = 0;
  musicTimer = setInterval(() => {
    if (!musicOn || !musicOscA) return;
    musicNote = (musicNote + 1) % musicNotes.length;
    const f = musicNotes[musicNote];
    try {
      musicOscA.frequency.setTargetAtTime(f, audioCtx.currentTime, 0.04);
      musicOscB.frequency.setTargetAtTime(f * 2, audioCtx.currentTime, 0.04);
    } catch (_) {}
  }, 480);
}

function syncMusicBtn() {
  const btn = document.getElementById("musicBtn");
  if (!btn) return;
  btn.textContent = musicOn ? "Music on" : "Music off";
  btn.setAttribute("aria-pressed", musicOn ? "true" : "false");
}

function setMusicEnabled(on) {
  musicOn = !!on;
  try { localStorage.setItem(MUSIC_LS_KEY, musicOn ? "1" : "0"); } catch (_) {}
  syncMusicBtn();
  if (musicOn) {
    ensureAudio();
    startMusic();
  } else {
    stopMusic();
  }
}

function bindAudioUI() {
  syncMusicBtn();
  const btn = document.getElementById("musicBtn");
  if (btn) {
    btn.addEventListener("click", () => {
      ensureAudio();
      setMusicEnabled(!musicOn);
    });
  }
  // Unlock AudioContext on first gesture (browsers block autoplay)
  const unlock = () => {
    ensureAudio();
    startMotors();
    if (musicOn && !musicOscA) startMusic();
  };
  window.addEventListener("pointerdown", unlock, { once: true, capture: true });
  window.addEventListener("keydown", unlock, { once: true, capture: true });
  if (musicOn) {
    // Try start; may no-op until unlock
    try { startMusic(); } catch (_) {}
  }
}


const canvas = document.getElementById("view");
const boot = document.getElementById("boot");
const bannerEl = document.getElementById("banner");
const s1El = document.getElementById("s1");
const s2El = document.getElementById("s2");
const boost1El = document.getElementById("boost1");
const boost2El = document.getElementById("boost2");

const keys = Object.create(null);
const touch = [
  { fwd: 0, rev: 0, steer: 0, boost: 0, boostUntil: 0, kickEdge: false, aimX: 0, aimZ: 0, aimActive: false },
  { fwd: 0, rev: 0, steer: 0, boost: 0, boostUntil: 0, kickEdge: false, aimX: 0, aimZ: 0, aimActive: false },
];
const FWD_DOUBLE_MS = 300;   // double-tap FWD window → boost
const FWD_BOOST_MS = 520;    // boost linger after double-tap
const fwdTapAt = [0, 0];
const AUTO_KICK_CD = 0.45;

// --- Online two-phone multiplayer (same CF Worker rooms as flat Soccer RC) ---
const ROOM_WS_BASE = "wss://similarize-bball-rooms.ben-e22.workers.dev/ws";
const ROOM_HTTP_BASE = "https://similarize-bball-rooms.ben-e22.workers.dev";
let netRole = null; // null | "host" | "guest"
let netWs = null;
let netCode = "";
let netReady = false;
let netRemoteInput = { u: 0, d: 0, l: 0, r: 0, fire: 0, boost: 0, ax: 0, az: 0, aim: 0 };
let netFireArmed = false;
let lastStateSent = 0;
let guestPendingState = null;
let guestInputTimer = 0;
let netLeaveIntent = false;
const onlineStatus = document.getElementById("onlineStatus");
const roomCodeInput = document.getElementById("roomCodeInput");
const padsEl = document.getElementById("pads");

let renderer, scene, camera, clock;
let ballMesh, ball;
let cars = [];
let score = [0, 0];
let freezeT = 0;
let bannerT = 0;
let camTarget = new THREE.Vector3();
let camPos = new THREE.Vector3(0, 18, 28);
let _gp0 = null, _gp1 = null;

function showBanner(text, cls, secs) {
  bannerEl.textContent = text;
  bannerEl.className = "show " + (cls || "");
  bannerT = secs || 1.6;
}

function softResetCarsIfBuried() {
  for (const c of cars) {
    const inOrangeGoal = c.pos.x < -HALF_X + 0.2;
    const inBlueGoal = c.pos.x > HALF_X - 0.2;
    if (inOrangeGoal || inBlueGoal) {
      const side = inOrangeGoal ? -1 : 1;
      c.pos.x = side * (HALF_X - 4);
      c.pos.z = Math.max(-HALF_Z + 2, Math.min(HALF_Z - 2, c.pos.z));
      c.pos.y = CAR_HALF.y;
      c.vx = 0; c.vz = 0; c.vy = 0;
      c.yaw = side > 0 ? Math.PI : 0;
    }
  }
}

function resetBall(toward = 0) {
  ball.pos.set(toward * 2, BALL_R + 0.05, 0);
  ball.vx = 0; ball.vy = 0; ball.vz = 0;
  softResetCarsIfBuried();
}

function resetMatch() {
  score[0] = 0; score[1] = 0;
  s1El.textContent = "0";
  s2El.textContent = "0";
  cars[0].pos.set(-8, CAR_HALF.y, 0);
  cars[0].yaw = 0;
  cars[0].vx = cars[0].vz = cars[0].vy = 0;
  cars[0].boost = 1;
  cars[1].pos.set(8, CAR_HALF.y, 0);
  cars[1].yaw = Math.PI;
  cars[1].vx = cars[1].vz = cars[1].vy = 0;
  cars[1].boost = 1;
  resetBall(0);
  clearTracks();
  if (cars[0]) cars[0]._trackAcc = 0;
  if (cars[1]) cars[1]._trackAcc = 0;
  freezeT = 0.6;
  showBanner("Kickoff", "", 1.2);
}

function makeCarMesh(colorHex) {
  const g = new THREE.Group();
  const bodyMat = new THREE.MeshStandardMaterial({
    color: colorHex, metalness: 0.35, roughness: 0.45,
  });
  const dark = new THREE.MeshStandardMaterial({ color: 0x1a1d24, metalness: 0.2, roughness: 0.7 });
  const accent = new THREE.MeshStandardMaterial({ color: 0xf5d08a, emissive: colorHex, emissiveIntensity: 0.15 });

  const body = new THREE.Mesh(new THREE.BoxGeometry(1.7, 0.45, 1.05), bodyMat);
  body.position.y = 0.35;
  body.castShadow = true;
  g.add(body);

  const cabin = new THREE.Mesh(new THREE.BoxGeometry(0.7, 0.32, 0.9), dark);
  cabin.position.set(-0.15, 0.62, 0);
  cabin.castShadow = true;
  g.add(cabin);

  const nose = new THREE.Mesh(new THREE.BoxGeometry(0.35, 0.22, 0.95), accent);
  nose.position.set(0.75, 0.32, 0);
  g.add(nose);

  const wheelGeo = new THREE.CylinderGeometry(0.28, 0.28, 0.22, 12);
  wheelGeo.rotateZ(Math.PI / 2);
  const spots = [
    [0.55, 0.28, 0.55], [0.55, 0.28, -0.55],
    [-0.55, 0.28, 0.55], [-0.55, 0.28, -0.55],
  ];
  for (const [x, y, z] of spots) {
    const w = new THREE.Mesh(wheelGeo, dark);
    w.position.set(x, y, z);
    w.castShadow = true;
    g.add(w);
  }

  // Boost flame (hidden until boosting)
  const flame = new THREE.Mesh(
    new THREE.ConeGeometry(0.18, 0.7, 8),
    new THREE.MeshBasicMaterial({ color: 0xffaa33, transparent: true, opacity: 0.85 })
  );
  flame.rotation.z = Math.PI / 2;
  flame.position.set(-1.15, 0.35, 0);
  flame.visible = false;
  g.add(flame);
  g.userData.flame = flame;

  return g;
}

function buildArena() {
  // Pitch
  const pitchMat = new THREE.MeshStandardMaterial({ color: 0x2f6b3a, roughness: 0.92, metalness: 0.05 });
  const pitch = new THREE.Mesh(new THREE.BoxGeometry(HALF_X * 2 + 2, 0.25, HALF_Z * 2 + 2), pitchMat);
  pitch.position.y = -0.125;
  pitch.receiveShadow = true;
  scene.add(pitch);

  // Center line / circle
  const lineMat = new THREE.MeshBasicMaterial({ color: 0xf4efe6 });
  const mid = new THREE.Mesh(new THREE.BoxGeometry(0.12, 0.02, HALF_Z * 2 - 1), lineMat);
  mid.position.y = 0.01;
  scene.add(mid);
  const ring = new THREE.Mesh(new THREE.RingGeometry(3.2, 3.4, 48), lineMat);
  ring.rotation.x = -Math.PI / 2;
  ring.position.y = 0.015;
  scene.add(ring);

  // Side boards (soft visual walls)
  const boardMat = new THREE.MeshStandardMaterial({ color: 0x1e2430, roughness: 0.8, metalness: 0.2 });
  const cream = new THREE.MeshStandardMaterial({ color: 0xc9b896, roughness: 0.7, metalness: 0.1 });
  for (const z of [-HALF_Z, HALF_Z]) {
    const board = new THREE.Mesh(new THREE.BoxGeometry(HALF_X * 2, 1.4, 0.35), boardMat);
    board.position.set(0, 0.7, z);
    board.castShadow = true;
    scene.add(board);
    const rail = new THREE.Mesh(new THREE.BoxGeometry(HALF_X * 2, 0.12, 0.4), cream);
    rail.position.set(0, 1.45, z);
    scene.add(rail);
  }
  // End boards beside goals
  for (const x of [-HALF_X, HALF_X]) {
    for (const side of [-1, 1]) {
      const span = (HALF_Z - GOAL_W) / 2;
      const midZ = side * (GOAL_W + span);
      const board = new THREE.Mesh(new THREE.BoxGeometry(0.35, 1.4, span * 2 - 0.2), boardMat);
      board.position.set(x, 0.7, midZ);
      scene.add(board);
    }
  }

  // Goals
  function addGoal(xSign, color) {
    const postMat = new THREE.MeshStandardMaterial({ color, metalness: 0.55, roughness: 0.35 });
    const netMat = new THREE.MeshStandardMaterial({
      color: 0xffffff, transparent: true, opacity: 0.12, roughness: 1, side: THREE.DoubleSide,
    });
    const gx = xSign * HALF_X;
    const posts = [
      [gx, GOAL_H / 2, -GOAL_W],
      [gx, GOAL_H / 2, GOAL_W],
    ];
    for (const [x, y, z] of posts) {
      const p = new THREE.Mesh(new THREE.BoxGeometry(0.22, GOAL_H, 0.22), postMat);
      p.position.set(x, y, z);
      scene.add(p);
    }
    const cross = new THREE.Mesh(new THREE.BoxGeometry(0.22, 0.22, GOAL_W * 2), postMat);
    cross.position.set(gx, GOAL_H, 0);
    scene.add(cross);
    // Back net
    const back = new THREE.Mesh(new THREE.PlaneGeometry(GOAL_W * 2, GOAL_H), netMat);
    back.position.set(gx + xSign * GOAL_DEPTH, GOAL_H / 2, 0);
    back.rotation.y = xSign > 0 ? Math.PI / 2 : -Math.PI / 2;
    scene.add(back);
    // Floor of goal mouth (slightly darker)
    const mouth = new THREE.Mesh(
      new THREE.BoxGeometry(GOAL_DEPTH, 0.08, GOAL_W * 2),
      new THREE.MeshStandardMaterial({ color: 0x1a3a28, roughness: 0.95 })
    );
    mouth.position.set(gx + xSign * (GOAL_DEPTH / 2), 0.04, 0);
    scene.add(mouth);
  }
  addGoal(-1, 0xfb923c);
  addGoal(1, 0x60a5fa);

  // Soft ceiling guide (thin translucent)
  const ceil = new THREE.Mesh(
    new THREE.PlaneGeometry(HALF_X * 2, HALF_Z * 2),
    new THREE.MeshBasicMaterial({ color: 0x4a5568, transparent: true, opacity: 0.06, side: THREE.DoubleSide })
  );
  ceil.rotation.x = Math.PI / 2;
  ceil.position.y = WALL_H;
  scene.add(ceil);
}

function createBallMesh() {
  const geo = new THREE.IcosahedronGeometry(BALL_R, 1);
  const mat = new THREE.MeshStandardMaterial({
    color: 0xf4efe6, roughness: 0.55, metalness: 0.1,
    flatShading: true,
  });
  const m = new THREE.Mesh(geo, mat);
  m.castShadow = true;
  m.receiveShadow = true;
  // Accent panels
  const stripe = new THREE.Mesh(
    new THREE.SphereGeometry(BALL_R * 1.01, 16, 12, 0, Math.PI * 2, 0, Math.PI * 0.35),
    new THREE.MeshStandardMaterial({ color: 0x1a1d24, roughness: 0.6 })
  );
  m.add(stripe);
  return m;
}

function padDrive(g) {
  if (!g || !g.connected) return { steer: 0, fwd: 0, rev: 0, thrusting: 0, kick: false, boost: false };
  let fwd = typeof g.rtValue === "number" ? g.rtValue : g.rt ? 1 : 0;
  let rev = typeof g.ltValue === "number" ? g.ltValue : g.lt ? 1 : 0;
  fwd = Math.max(0, Math.min(1, fwd));
  rev = Math.max(0, Math.min(1, rev));
  if (fwd > 0 && rev > 0) {
    if (fwd >= rev) rev = 0;
    else fwd = 0;
  }
  const thrusting = fwd > 0.04 || rev > 0.04 ? 1 : 0;
  // Tank pivot: stick X / dpad yaw ALWAYS — no thrust required (real RC in-place turn)
  let steer = 0;
  if (g.dpad && g.dpad.l) steer = -1;
  else if (g.dpad && g.dpad.r) steer = 1;
  else steer = Math.max(-1, Math.min(1, g.lx || 0));
  const kick = !!(g.buttonsPressed && g.buttonsPressed.a);
  const boost = !!(g.b || g.rb);
  return { steer, fwd, rev, thrusting, kick, boost };
}

function readInput(playerIndex) {
  // Host online: P2 is driven only by remote guest bits
  if (netRole === "host" && playerIndex === 1) {
    const ri = netRemoteInput;
    let fwd = ri.u ? 1 : 0;
    let rev = ri.d ? 1 : 0;
    let steer = ri.l ? -1 : ri.r ? 1 : 0;
    let kick = !!ri.fire;
    let boost = !!ri.boost;
    let aimX = ri.ax || 0, aimZ = ri.az || 0, aimActive = !!ri.aim;
    if (ri.fire) ri.fire = 0; // consume edge
    if (fwd > 0 && rev > 0) {
      if (fwd >= rev) rev = 0;
      else fwd = 0;
    }
    return { fwd, rev, steer, kick, boost, aimX, aimZ, aimActive };
  }

  const t = touch[playerIndex];
  // Guest phone: local sticks/keys/pad0 drive their car via net bits, not local sim —
  // but host + local couch still use this path for P1 (and P2 when offline).
  const g = playerIndex === 0 ? _gp0 : (netRole === "host" ? null : _gp1);
  const pd = padDrive(g);

  let fwd = pd.fwd, rev = pd.rev, steer = pd.steer;
  // Phone double-tap FWD arms boost for a short window
  if (t.boostUntil && performance.now() < t.boostUntil) t.boost = 1;
  else if (t.boostUntil && performance.now() >= t.boostUntil) {
    t.boost = 0;
    t.boostUntil = 0;
  }

  let kick = pd.kick || t.kickEdge;
  let boost = pd.boost || !!t.boost;
  let aimX = 0, aimZ = 0, aimActive = false;

  // Phone RC: stick → steer only; FWD/REV buttons → throttle (+ dbl-tap boost)
  if (t.fwd > 0.04) fwd = Math.max(fwd, t.fwd);
  if (t.rev > 0.04) rev = Math.max(rev, t.rev);
  if (Math.abs(t.steer) > 0.05) steer = t.steer;

  if (playerIndex === 0) {
    if (keys.ArrowUp) fwd = 1;
    if (keys.ArrowDown) rev = 1;
    const keySteer = keys.ArrowLeft ? -1 : keys.ArrowRight ? 1 : 0;
    if (keySteer) steer = keySteer;
    if (keys[" "] || keys.Space) kick = true;
    if (keys.ShiftLeft || keys.ShiftRight) boost = true;
    // Guest may also use WASD / Q / E locally (packed into guest bits)
    if (netRole === "guest") {
      if (keys.w || keys.W) fwd = 1;
      if (keys.s || keys.S) rev = 1;
      const gs = keys.a || keys.A ? -1 : keys.d || keys.D ? 1 : 0;
      if (gs) steer = gs;
      if (keys.q || keys.Q) kick = true;
      if (keys.e || keys.E) boost = true;
    }
  } else if (netRole !== "host") {
    // Local couch P2 only when not hosting online
    if (keys.w || keys.W) fwd = 1;
    if (keys.s || keys.S) rev = 1;
    const keySteer = keys.a || keys.A ? -1 : keys.d || keys.D ? 1 : 0;
    if (keySteer) steer = keySteer;
    if (keys.q || keys.Q) kick = true;
    if (keys.e || keys.E) boost = true;
  }

  if (fwd > 0 && rev > 0) {
    if (fwd >= rev) rev = 0;
    else fwd = 0;
  }

  t.kickEdge = false;
  return { fwd, rev, steer, kick, boost, aimX, aimZ, aimActive };
}

function kickBall(car, aim) {
  const dx = ball.pos.x - car.pos.x;
  const dy = ball.pos.y - car.pos.y;
  const dz = ball.pos.z - car.pos.z;
  const dist = Math.hypot(dx, dy, dz);
  // Jump always on kick press when grounded
  if (car.onGround && car.jumpCd <= 0) {
    car.vy = JUMP_VY;
    car.onGround = false;
    car.jumpCd = 0.35;
  }
  if (dist > KICK_RANGE) return;
  const nx = dx / (dist || 1);
  const nz = dz / (dist || 1);
  let forwardX = Math.cos(car.yaw);
  let forwardZ = Math.sin(car.yaw);
  // Right-stick aim overrides car facing when active (twin-stick shoot)
  if (aim && aim.aimActive) {
    const alen = Math.hypot(aim.aimX, aim.aimZ);
    if (alen > 0.05) {
      forwardX = aim.aimX / alen;
      forwardZ = aim.aimZ / alen;
    }
  }
  // Bias impulse toward aim/facing + contact normal
  const ix = (forwardX * 0.65 + nx * 0.35) * KICK_IMPULSE + car.vx * 0.35;
  const iz = (forwardZ * 0.65 + nz * 0.35) * KICK_IMPULSE + car.vz * 0.35;
  const iy = 4.5 + Math.max(0, car.vy) * 0.2;
  ball.vx += ix;
  ball.vy += iy;
  ball.vz += iz;
}

function collideCarBall(car) {
  const dx = ball.pos.x - car.pos.x;
  const dy = ball.pos.y - (car.pos.y + 0.15);
  const dz = ball.pos.z - car.pos.z;
  // Approximate car as ellipsoid
  const rx = CAR_HALF.x + BALL_R;
  const ry = CAR_HALF.y + BALL_R + 0.15;
  const rz = CAR_HALF.z + BALL_R;
  const nx = dx / rx, ny = dy / ry, nz = dz / rz;
  const d2 = nx * nx + ny * ny + nz * nz;
  if (d2 >= 1 || d2 < 1e-6) return;
  const d = Math.sqrt(d2);
  const push = (1 - d) * 0.55;
  const px = (nx / d) * push * rx;
  const py = (ny / d) * push * ry;
  const pz = (nz / d) * push * rz;
  ball.pos.x += px;
  ball.pos.y += py * 0.6;
  ball.pos.z += pz;
  const relVx = ball.vx - car.vx;
  const relVy = ball.vy - car.vy;
  const relVz = ball.vz - car.vz;
  const nnx = nx / d, nny = ny / d, nnz = nz / d;
  const vn = relVx * nnx + relVy * nny + relVz * nnz;
  if (vn < 0) {
    const bounce = 1.35;
    ball.vx -= vn * nnx * bounce;
    ball.vy -= vn * nny * bounce;
    ball.vz -= vn * nnz * bounce;
    // Transfer some car speed
    ball.vx += car.vx * 0.25;
    ball.vz += car.vz * 0.25;
    const impact = Math.min(1.5, Math.abs(vn) / 8);
    if (impact > 0.12) sfxBallHit(impact);
  }
  // Phone scheme: auto-kick when bumping/thrusting into ball while holding FWD
  // (KB/pad still have explicit A/Space kick via inp.kick)
  const thrustingIn = car._holdingFwd && (vn < 0.5 || d2 < 0.85);
  if (thrustingIn && (car.autoKickCd || 0) <= 0) {
    car.autoKickCd = AUTO_KICK_CD;
    kickBall(car, null);
  }
}

function collideCars() {
  const a = cars[0], b = cars[1];
  const dx = b.pos.x - a.pos.x;
  const dz = b.pos.z - a.pos.z;
  const minD = CAR_HALF.x * 2 * 0.95;
  const d = Math.hypot(dx, dz);
  if (d < minD && d > 1e-4) {
    const nx = dx / d, nz = dz / d;
    const overlap = (minD - d) * 0.5;
    a.pos.x -= nx * overlap;
    a.pos.z -= nz * overlap;
    b.pos.x += nx * overlap;
    b.pos.z += nz * overlap;
    const rv = (b.vx - a.vx) * nx + (b.vz - a.vz) * nz;
    if (rv < 0) {
      a.vx += rv * nx * 0.5;
      a.vz += rv * nz * 0.5;
      b.vx -= rv * nx * 0.5;
      b.vz -= rv * nz * 0.5;
      if (Math.abs(rv) > 2.2) sfxCrash();
    } else if (d < minD * 0.92) {
      // Soft bump when overlapping with little closing speed
      const rel = Math.hypot(a.vx - b.vx, a.vz - b.vz);
      if (rel > 4) sfxCrash();
    }
  }
}

/** Soft arena bounds — cars never freeze in goals (soccer-goal1 lesson). */
function boundCar(c) {
  const maxX = HALF_X + GOAL_DEPTH - 0.6;
  const maxZ = HALF_Z - 0.7;
  // Soft push from side boards
  if (c.pos.z > maxZ) {
    c.pos.z = maxZ;
    c.vz *= -0.3;
  } else if (c.pos.z < -maxZ) {
    c.pos.z = -maxZ;
    c.vz *= -0.3;
  }
  // End walls: allow into goal mouth but soft-bounce off back net
  const inGoalZ = Math.abs(c.pos.z) < GOAL_W - 0.3;
  if (c.pos.x > maxX) {
    c.pos.x = maxX;
    c.vx *= -0.25;
  } else if (c.pos.x < -maxX) {
    c.pos.x = -maxX;
    c.vx *= -0.25;
  } else if (!inGoalZ) {
    if (c.pos.x > HALF_X - 0.5) {
      c.pos.x = HALF_X - 0.5;
      c.vx *= -0.2;
    } else if (c.pos.x < -HALF_X + 0.5) {
      c.pos.x = -HALF_X + 0.5;
      c.vx *= -0.2;
    }
  }
  // Floor / ceiling
  if (c.pos.y < CAR_HALF.y) {
    c.pos.y = CAR_HALF.y;
    c.vy = 0;
    c.onGround = true;
  }
  if (c.pos.y > WALL_H - 0.5) {
    c.pos.y = WALL_H - 0.5;
    c.vy *= -0.2;
  }
}

function boundBall() {
  // Floor
  if (ball.pos.y < BALL_R) {
    ball.pos.y = BALL_R;
    if (ball.vy < 0) ball.vy *= -BALL_BOUNCE;
    ball.vx *= 0.98;
    ball.vz *= 0.98;
    if (Math.abs(ball.vy) < 0.8) ball.vy = 0;
  }
  // Ceiling
  if (ball.pos.y > WALL_H - BALL_R) {
    ball.pos.y = WALL_H - BALL_R;
    ball.vy *= -BALL_BOUNCE;
  }
  // Side walls
  if (ball.pos.z > HALF_Z - BALL_R) {
    ball.pos.z = HALF_Z - BALL_R;
    ball.vz *= -BALL_BOUNCE;
  } else if (ball.pos.z < -HALF_Z + BALL_R) {
    ball.pos.z = -HALF_Z + BALL_R;
    ball.vz *= -BALL_BOUNCE;
  }
  // End walls / goals
  const inGoalZ = Math.abs(ball.pos.z) < GOAL_W - BALL_R * 0.5;
  const inGoalY = ball.pos.y < GOAL_H - BALL_R * 0.3;
  if (inGoalZ && inGoalY) {
    // Inside goal tunnel — bounce off back
    if (ball.pos.x > HALF_X + GOAL_DEPTH - BALL_R) {
      ball.pos.x = HALF_X + GOAL_DEPTH - BALL_R;
      ball.vx *= -BALL_BOUNCE;
    } else if (ball.pos.x < -HALF_X - GOAL_DEPTH + BALL_R) {
      ball.pos.x = -HALF_X - GOAL_DEPTH + BALL_R;
      ball.vx *= -BALL_BOUNCE;
    }
  } else {
    if (ball.pos.x > HALF_X - BALL_R) {
      ball.pos.x = HALF_X - BALL_R;
      ball.vx *= -BALL_BOUNCE;
    } else if (ball.pos.x < -HALF_X + BALL_R) {
      ball.pos.x = -HALF_X + BALL_R;
      ball.vx *= -BALL_BOUNCE;
    }
  }
}

function checkGoal() {
  // Ball fully enters goal volume past the goal line
  const fullyInY = ball.pos.y < GOAL_H - BALL_R;
  const fullyInZ = Math.abs(ball.pos.z) < GOAL_W - BALL_R;
  if (!fullyInY || !fullyInZ) return;
  if (ball.pos.x > HALF_X + BALL_R * 0.4) {
    // Blue goal scored on → Orange (P1) scores
    score[0]++;
    s1El.textContent = String(score[0]);
    freezeT = 1.8;
    showBanner("ORANGE SCORES!", "orange", 1.6);
    if (netRole === "host") netSend({ type: "banner", text: "ORANGE SCORES!", cls: "orange", secs: 1.6 });
    setTimeout(() => resetBall(0), 900);
  } else if (ball.pos.x < -HALF_X - BALL_R * 0.4) {
    score[1]++;
    s2El.textContent = String(score[1]);
    freezeT = 1.8;
    showBanner("BLUE SCORES!", "blue", 1.6);
    if (netRole === "host") netSend({ type: "banner", text: "BLUE SCORES!", cls: "blue", secs: 1.6 });
    setTimeout(() => resetBall(0), 900);
  }
}


// --- Tire tracks (flat Soccer RC style, lightweight 3D quads on pitch) ---
const TRACK_LIFE = 1.85;       // seconds
const TRACK_MAX = 140;
const TRACK_SPACING = 0.38;    // meters between drops
const tracks = [];
let trackGeo = null;
let trackMat = null;

function ensureTrackAssets() {
  if (trackGeo) return;
  // One tread mark ≈ flat's small rect
  trackGeo = new THREE.BoxGeometry(0.55, 0.02, 0.11);
  trackMat = new THREE.MeshBasicMaterial({
    color: 0x0a0c10,
    transparent: true,
    opacity: 0.45,
    depthWrite: false,
  });
}

function clearTracks() {
  for (const t of tracks) {
    if (t.mesh && t.mesh.parent) t.mesh.parent.remove(t.mesh);
    if (t.mesh) {
      // dispose not needed for shared geo/mat; just drop refs
    }
  }
  tracks.length = 0;
}

function dropTrackMark(x, z, yaw) {
  ensureTrackAssets();
  // Two wheel marks offset along car width (local ±Z when yaw=0 nose +X)
  const group = new THREE.Group();
  const side = 0.42;
  const cos = Math.cos(yaw), sin = Math.sin(yaw);
  // local +Z is left of car when nose +X; world offset via yaw
  for (const s of [-side, side]) {
    const m = new THREE.Mesh(trackGeo, trackMat.clone());
    m.position.set(0, 0, s);
    group.add(m);
  }
  group.position.set(x, 0.03, z);
  group.rotation.y = -yaw;
  scene.add(group);
  tracks.push({ mesh: group, life: TRACK_LIFE, mats: group.children.map((c) => c.material) });
  // Cull oldest if over cap
  while (tracks.length > TRACK_MAX) {
    const old = tracks.shift();
    if (old.mesh && old.mesh.parent) old.mesh.parent.remove(old.mesh);
    for (const mat of old.mats) mat.dispose();
  }
}

function maybeDropTracks(car, dt) {
  if (!car.onGround) {
    car._trackAcc = 0;
    return;
  }
  const sp = Math.hypot(car.vx, car.vz);
  if (sp < 1.4) {
    car._trackAcc = 0;
    return;
  }
  car._trackAcc = (car._trackAcc || 0) + sp * dt;
  while (car._trackAcc >= TRACK_SPACING) {
    car._trackAcc -= TRACK_SPACING;
    dropTrackMark(car.pos.x, car.pos.z, car.yaw);
  }
}

function updateTracks(dt) {
  for (let i = tracks.length - 1; i >= 0; i--) {
    const t = tracks[i];
    t.life -= dt;
    if (t.life <= 0) {
      if (t.mesh && t.mesh.parent) t.mesh.parent.remove(t.mesh);
      for (const mat of t.mats) mat.dispose();
      tracks.splice(i, 1);
      continue;
    }
    const a = Math.max(0, t.life / TRACK_LIFE) * 0.48;
    for (const mat of t.mats) mat.opacity = a;
  }
}

function updateCar(car, inp, dt) {
  if (car.jumpCd > 0) car.jumpCd -= dt;
  if (car.autoKickCd > 0) car.autoKickCd -= dt;
  car._holdingFwd = inp.fwd > 0.04;

  if (Math.abs(inp.steer) > 0.05) {
    // Real RC tank pivot: yaw from steer alone — never gated on fwd/rev (no strafe).
    // Invert L/R while reversing (hold-rev or clearly coasting backward).
    const fx = Math.cos(car.yaw), fz = Math.sin(car.yaw);
    const along = car.vx * fx + car.vz * fz;
    const holdRev = inp.rev > inp.fwd && inp.rev > 0.04;
    const coastRev = along < -2;
    const flip = (holdRev || coastRev) ? -1 : 1;
    car.yaw += inp.steer * TURN_RATE * dt * flip;
  }

  let boostOn = false;
  if (inp.boost && car.boost > 0.02) {
    car.boost = Math.max(0, car.boost - BOOST_DRAIN * dt);
    boostOn = true;
  } else {
    car.boost = Math.min(1, car.boost + BOOST_REGEN * dt);
  }

  const maxSp = boostOn ? CAR_BOOST_MAX : CAR_MAX;
  const ax = Math.cos(car.yaw);
  const az = Math.sin(car.yaw);
  if (inp.fwd > 0.04) {
    const a = CAR_ACCEL * (boostOn ? 1.55 : 1) * inp.fwd;
    car.vx += ax * a * dt;
    car.vz += az * a * dt;
  } else if (inp.rev > 0.04) {
    const a = CAR_ACCEL * 0.55 * inp.rev;
    car.vx -= ax * a * dt;
    car.vz -= az * a * dt;
  }

  // Clamp horizontal speed
  const sp = Math.hypot(car.vx, car.vz);
  if (sp > maxSp) {
    car.vx = (car.vx / sp) * maxSp;
    car.vz = (car.vz / sp) * maxSp;
  }
  car.vx *= CAR_DRAG;
  car.vz *= CAR_DRAG;

  if (!car.onGround) {
    car.vy -= GRAVITY * dt;
  }
  car.pos.x += car.vx * dt;
  car.pos.y += car.vy * dt;
  car.pos.z += car.vz * dt;
  car.onGround = false;
  boundCar(car);

  if (inp.kick) kickBall(car, inp);

  // Sync mesh
  car.mesh.position.copy(car.pos);
  car.mesh.rotation.y = -car.yaw; // mesh nose is +X; yaw 0 → +X
  if (car.mesh.userData.flame) {
    car.mesh.userData.flame.visible = boostOn;
  }
  maybeDropTracks(car, dt);
}

function updateBall(dt) {
  ball.vy -= GRAVITY * dt;
  ball.vx *= BALL_FRICTION;
  ball.vz *= BALL_FRICTION;
  ball.pos.x += ball.vx * dt;
  ball.pos.y += ball.vy * dt;
  ball.pos.z += ball.vz * dt;
  boundBall();
  ballMesh.position.copy(ball.pos);
  const spin = Math.hypot(ball.vx, ball.vz) * dt;
  ballMesh.rotation.x += ball.vz * 0.08 * dt * 60;
  ballMesh.rotation.z -= ball.vx * 0.08 * dt * 60;
  void spin;
}

function updateCamera(dt) {
  // Shared elevated cam: look at blend of ball + cars, sit behind midfield action
  const midX = (cars[0].pos.x + cars[1].pos.x + ball.pos.x * 1.4) / 3.4;
  const midZ = (cars[0].pos.z + cars[1].pos.z + ball.pos.z * 1.4) / 3.4;
  camTarget.lerp(new THREE.Vector3(midX, 0.5, midZ), 1 - Math.pow(0.001, dt));

  const desired = new THREE.Vector3(
    camTarget.x * 0.15,
    16 + Math.min(6, Math.abs(ball.pos.y) * 0.4),
    camTarget.z * 0.2 + 26
  );
  // Slight chase bias toward more active car (higher speed)
  const sp0 = Math.hypot(cars[0].vx, cars[0].vz);
  const sp1 = Math.hypot(cars[1].vx, cars[1].vz);
  if (sp0 + sp1 > 2) {
    const w0 = sp0 / (sp0 + sp1);
    const focus = cars[0].pos.clone().lerp(cars[1].pos, 1 - w0);
    desired.x += (focus.x - desired.x) * 0.15;
  }
  camPos.lerp(desired, 1 - Math.pow(0.02, dt));
  camera.position.copy(camPos);
  camera.lookAt(camTarget.x, 1.2, camTarget.z);
}


function setOnlineStatus(msg, kind) {
  if (!onlineStatus) return;
  onlineStatus.textContent = msg || "";
  onlineStatus.className = "status" + (kind ? " " + kind : "");
}

function applyPadMode() {
  if (!padsEl) return;
  padsEl.classList.remove("solo-guest", "solo-host");
  if (netRole === "guest") padsEl.classList.add("solo-guest");
  else if (netRole === "host") padsEl.classList.add("solo-host");
  const lab = padsEl.querySelector(".cluster.p1 .cluster-label");
  if (lab) lab.textContent = netRole === "guest" ? "BLUE" : "ORANGE";
}

function round3(n) { return Math.round(n * 1000) / 1000; }

function packCar(c) {
  return [
    round3(c.pos.x), round3(c.pos.y), round3(c.pos.z),
    round3(c.yaw), round3(c.vx), round3(c.vy), round3(c.vz),
    round3(c.boost), c.onGround ? 1 : 0,
  ];
}

function unpackCar(c, a) {
  if (!a || a.length < 8) return;
  c.pos.set(a[0], a[1], a[2]);
  c.yaw = a[3];
  c.vx = a[4]; c.vy = a[5]; c.vz = a[6];
  c.boost = a[7];
  c.onGround = !!a[8];
  if (c.mesh) {
    c.mesh.position.copy(c.pos);
    c.mesh.rotation.y = -c.yaw;
    if (c.mesh.userData.flame) c.mesh.userData.flame.visible = false;
  }
}

function buildStateMsg() {
  return {
    type: "state",
    c0: packCar(cars[0]),
    c1: packCar(cars[1]),
    b: [
      round3(ball.pos.x), round3(ball.pos.y), round3(ball.pos.z),
      round3(ball.vx), round3(ball.vy), round3(ball.vz),
    ],
    s: [score[0] | 0, score[1] | 0],
    fz: round3(freezeT),
  };
}

function applyStateMsg(msg) {
  if (!msg || !msg.c0) return;
  unpackCar(cars[0], msg.c0);
  unpackCar(cars[1], msg.c1);
  if (msg.b) {
    ball.pos.set(msg.b[0], msg.b[1], msg.b[2]);
    ball.vx = msg.b[3]; ball.vy = msg.b[4]; ball.vz = msg.b[5];
    if (ballMesh) ballMesh.position.copy(ball.pos);
  }
  if (msg.s) {
    score[0] = msg.s[0] | 0;
    score[1] = msg.s[1] | 0;
    s1El.textContent = String(score[0]);
    s2El.textContent = String(score[1]);
  }
  if (typeof msg.fz === "number") freezeT = msg.fz;
}

function netSend(obj) {
  if (netWs && netWs.readyState === 1) netWs.send(JSON.stringify(obj));
}

function readLocalGuestBits() {
  // Guest: pad0 + phone stick(steer)/FWD/REV + arrows/WASD
  const t = touch[0];
  const d = padDrive(_gp0);
  let fwd = d.fwd > 0.04 || t.fwd > 0.04 || keys.ArrowUp || keys.w || keys.W;
  let rev = d.rev > 0.04 || t.rev > 0.04 || keys.ArrowDown || keys.s || keys.S;
  let steer = 0;
  if (Math.abs(t.steer) > 0.05) steer = t.steer;
  else if (d.steer) steer = d.steer;
  else if (keys.ArrowLeft || keys.a || keys.A) steer = -1;
  else if (keys.ArrowRight || keys.d || keys.D) steer = 1;
  if (t.boostUntil && performance.now() < t.boostUntil) t.boost = 1;
  const fire = !!(netFireArmed || t.kickEdge || d.kick);
  const boost = !!(t.boost || d.boost || keys.ShiftLeft || keys.ShiftRight || keys.e || keys.E);
  const bits = {
    type: "input",
    u: fwd ? 1 : 0,
    d: rev ? 1 : 0,
    l: steer < -0.2 ? 1 : 0,
    r: steer > 0.2 ? 1 : 0,
    fire: fire ? 1 : 0,
    boost: boost ? 1 : 0,
    ax: 0,
    az: 0,
    aim: 0,
  };
  t.kickEdge = false;
  return bits;
}

function disconnectRoom(reason) {
  netLeaveIntent = true;
  const ws = netWs;
  netWs = null;
  if (ws) {
    try { ws.close(); } catch (_) {}
  }
  netRole = null;
  netReady = false;
  netCode = "";
  netRemoteInput = { u: 0, d: 0, l: 0, r: 0, fire: 0, boost: 0, ax: 0, az: 0, aim: 0 };
  guestPendingState = null;
  applyPadMode();
  const leaveBtn = document.getElementById("leaveRoomBtn");
  const createBtn = document.getElementById("createRoomBtn");
  const joinBtn = document.getElementById("joinRoomBtn");
  if (leaveBtn) leaveBtn.hidden = true;
  if (createBtn) createBtn.disabled = false;
  if (joinBtn) joinBtn.disabled = false;
  if (roomCodeInput) roomCodeInput.disabled = false;
  if (reason) setOnlineStatus(reason, "err");
  else setOnlineStatus("");
  netLeaveIntent = false;
}

function connectRoom(code, asCreator) {
  code = String(code || "").toUpperCase().replace(/[^A-Z0-9]/g, "").slice(0, 4);
  if (code.length !== 4) {
    setOnlineStatus("Enter a 4-character code", "err");
    return;
  }
  netLeaveIntent = false;
  if (netWs) disconnectRoom();
  netLeaveIntent = false;
  setOnlineStatus(asCreator ? "Creating…" : "Joining…");
  if (roomCodeInput) roomCodeInput.value = code;
  const ws = new WebSocket(ROOM_WS_BASE + "?room=" + encodeURIComponent(code));
  netWs = ws;
  ws.onopen = () => {
    netCode = code;
    const leaveBtn = document.getElementById("leaveRoomBtn");
    const createBtn = document.getElementById("createRoomBtn");
    const joinBtn = document.getElementById("joinRoomBtn");
    if (leaveBtn) leaveBtn.hidden = false;
    if (createBtn) createBtn.disabled = true;
    if (joinBtn) joinBtn.disabled = true;
    if (roomCodeInput) roomCodeInput.disabled = true;
  };
  ws.onmessage = (ev) => {
    let msg;
    try { msg = JSON.parse(ev.data); } catch { return; }
    if (msg.type === "welcome") {
      netRole = msg.seat; // host | guest
      netReady = !!msg.ready;
      applyPadMode();
      clearTracks();
      const label = netRole === "host" ? "Orange (host)" : "Blue (guest)";
      setOnlineStatus(netReady ? label + " · " + msg.code + " · ready" : label + " · " + msg.code + " · waiting…", "ok");
      if (netRole === "host") {
        ["w", "W", "a", "A", "s", "S", "d", "D", "q", "Q", "e", "E"].forEach((k) => { keys[k] = false; });
      }
      if (netRole === "guest") {
        ["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", " ", "Space"].forEach((k) => { keys[k] = false; });
      }
    } else if (msg.type === "peer") {
      netReady = !!msg.ready;
      const label = netRole === "host" ? "Orange (host)" : "Blue (guest)";
      setOnlineStatus(netReady ? label + " · " + netCode + " · ready" : label + " · " + netCode + " · waiting…", "ok");
    } else if (msg.type === "peer_left") {
      setOnlineStatus("Other phone left", "err");
      netReady = false;
    } else if (msg.type === "input" && netRole === "host") {
      netRemoteInput.u = msg.u | 0;
      netRemoteInput.d = msg.d | 0;
      netRemoteInput.l = msg.l | 0;
      netRemoteInput.r = msg.r | 0;
      netRemoteInput.boost = msg.boost | 0;
      netRemoteInput.ax = +msg.ax || 0;
      netRemoteInput.az = +msg.az || 0;
      netRemoteInput.aim = msg.aim | 0;
      if (msg.fire) netRemoteInput.fire = 1;
    } else if (msg.type === "state" && netRole === "guest") {
      guestPendingState = msg;
    } else if (msg.type === "reset" && netRole === "guest") {
      resetMatch();
    } else if (msg.type === "banner" && netRole === "guest") {
      showBanner(msg.text || "", msg.cls || "", msg.secs || 1.6);
    }
  };
  ws.onclose = () => {
    if (netLeaveIntent) return;
    if (netWs === ws) disconnectRoom("Disconnected");
  };
  ws.onerror = () => setOnlineStatus("Connection failed", "err");
}

async function createRoom() {
  try {
    setOnlineStatus("Creating…");
    const res = await fetch(ROOM_HTTP_BASE + "/create", { method: "POST" });
    const data = await res.json();
    if (!data.code) throw new Error("no code");
    connectRoom(data.code, true);
  } catch (e) {
    setOnlineStatus("Could not create room", "err");
  }
}

function bindOnlineUI() {
  const createBtn = document.getElementById("createRoomBtn");
  const joinBtn = document.getElementById("joinRoomBtn");
  const leaveBtn = document.getElementById("leaveRoomBtn");
  if (createBtn) createBtn.addEventListener("click", createRoom);
  if (joinBtn) joinBtn.addEventListener("click", () => connectRoom(roomCodeInput && roomCodeInput.value, false));
  if (leaveBtn) leaveBtn.addEventListener("click", () => disconnectRoom(""));
  if (roomCodeInput) {
    roomCodeInput.addEventListener("keydown", (e) => {
      if (e.key === "Enter") connectRoom(roomCodeInput.value, false);
    });
  }
}

function refreshGamepads() {
  const GP = window.SimilarizeGamepad;
  if (!GP) {
    _gp0 = _gp1 = null;
    return;
  }
  _gp0 = GP.pollPad(0);
  _gp1 = GP.pollPad(1);
}

function requestReset() {
  if (netRole === "guest") return; // host owns match state
  resetMatch();
  if (netRole === "host") netSend({ type: "reset" });
}

function tick() {
  const dt = Math.min(0.05, clock.getDelta());
  refreshGamepads();

  if (_gp0 && _gp0.connected && (_gp0.buttonsPressed.start || _gp0.buttonsPressed.back)) {
    requestReset();
  }
  if (keys.r || keys.R) {
    keys.r = keys.R = false;
    requestReset();
  }

  if (bannerT > 0) {
    bannerT -= dt;
    if (bannerT <= 0) bannerEl.className = "";
  }

  if (netRole === "guest") {
    // Guest: send inputs ~30Hz; render from host snapshots only
    const now = performance.now();
    if (now - guestInputTimer > 33) {
      guestInputTimer = now;
      // Capture pad A / space edge into netFireArmed
      if (_gp0 && _gp0.connected && _gp0.buttonsPressed && _gp0.buttonsPressed.a) netFireArmed = true;
      if (keys[" "] || keys.Space || keys.q || keys.Q) netFireArmed = true;
      const bits = readLocalGuestBits();
      netSend(bits);
      netFireArmed = false;
    }
    if (guestPendingState) {
      applyStateMsg(guestPendingState);
      guestPendingState = null;
    }
    // Local tire tracks from snapshot velocities (same look as host)
    for (const c of cars) {
      c.onGround = c.pos.y <= CAR_HALF.y + 0.08;
      maybeDropTracks(c, dt);
    }
  } else {
    if (freezeT > 0) {
      freezeT -= dt;
    } else {
      const inp0 = readInput(0);
      const inp1 = readInput(1);
      updateCar(cars[0], edgeKick(0, inp0), dt);
      updateCar(cars[1], edgeKick(1, inp1), dt);
      collideCars();
      for (const c of cars) collideCarBall(c);
      updateBall(dt);
      checkGoal();
    }
    if (netRole === "host" && netReady) {
      const now = performance.now();
      if (now - lastStateSent > 40) {
        lastStateSent = now;
        netSend(buildStateMsg());
      }
    }
  }

  boost1El.style.transform = "scaleX(" + cars[0].boost.toFixed(3) + ")";
  boost2El.style.transform = "scaleX(" + cars[1].boost.toFixed(3) + ")";

  updateTracks(dt);
  updateMotorAudio();
  updateCamera(dt);
  renderer.render(scene, camera);
  requestAnimationFrame(tick);
}

const _prevKick = [false, false];
function edgeKick(i, inp) {
  const out = Object.assign({}, inp);
  const held = !!inp.kick;
  out.kick = held && !_prevKick[i];
  _prevKick[i] = held;
  return out;
}

function onResize() {
  const w = window.innerWidth;
  const h = window.innerHeight;
  camera.aspect = w / Math.max(1, h);
  camera.updateProjectionMatrix();
  renderer.setPixelRatio(Math.min(2, window.devicePixelRatio || 1));
  renderer.setSize(w, h, false);
}

function bindKeys() {
  window.addEventListener("keydown", (e) => {
    keys[e.key] = true;
    if (["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", " ", "Space"].includes(e.key)) e.preventDefault();
  });
  window.addEventListener("keyup", (e) => {
    keys[e.key] = false;
  });
  document.getElementById("resetBtn").addEventListener("click", () => requestReset());
}

function bindSteerStick(root) {
  if (!root) return;
  const knob = root.querySelector(".stick-knob");
  const base = root.querySelector(".stick-base");
  if (!knob || !base) return;
  const p = +root.dataset.p || 0;
  let ptrId = null;
  let maxR = 36;
  const dead = 0.14;

  function setKnob(dx, dy) {
    knob.style.transform = "translate(" + dx + "px," + dy + "px)";
  }

  function applySteer(nx) {
    const t = touch[p];
    const a = Math.abs(nx);
    if (a < dead) {
      t.steer = 0;
      return;
    }
    const s = Math.min(1, (a - dead) / (1 - dead));
    t.steer = Math.sign(nx) * s;
  }

  function fromEvent(e) {
    const rect = base.getBoundingClientRect();
    maxR = Math.max(28, Math.min(rect.width, rect.height) * 0.38);
    const cx = rect.left + rect.width * 0.5;
    const cy = rect.top + rect.height * 0.5;
    let dx = e.clientX - cx;
    let dy = e.clientY - cy;
    // Constrain knob to circle for feel, but only X drives yaw (no throttle from Y)
    let len = Math.hypot(dx, dy);
    if (len > maxR && len > 0) {
      dx = (dx / len) * maxR;
      dy = (dy / len) * maxR;
      len = maxR;
    }
    setKnob(dx, dy);
    const nx = maxR > 0 ? dx / maxR : 0;
    applySteer(nx);
  }

  function endStick() {
    ptrId = null;
    root.classList.remove("is-active");
    setKnob(0, 0);
    touch[p].steer = 0;
  }

  function tryFS() {
    try {
      if (window.SimilarizeViewportFS) window.SimilarizeViewportFS.enter();
    } catch (_) {}
  }

  root.addEventListener("pointerdown", (e) => {
    if (e.button != null && e.button !== 0) return;
    e.preventDefault();
    e.stopPropagation();
    ptrId = e.pointerId;
    root.classList.add("is-active");
    try { root.setPointerCapture(e.pointerId); } catch (_) {}
    fromEvent(e);
    tryFS();
  });
  root.addEventListener("pointermove", (e) => {
    if (ptrId == null || e.pointerId !== ptrId) return;
    e.preventDefault();
    fromEvent(e);
  });
  function up(e) {
    if (ptrId == null || (e && e.pointerId != null && e.pointerId !== ptrId)) return;
    if (e && e.preventDefault) e.preventDefault();
    endStick();
  }
  root.addEventListener("pointerup", up);
  root.addEventListener("pointercancel", up);
  root.addEventListener("lostpointercapture", () => {
    if (ptrId != null) endStick();
  });
}

function bindPadButtons() {
  document.querySelectorAll(".pads button[data-act]").forEach((btn) => {
    const p = +btn.dataset.p || 0;
    const act = btn.dataset.act;
    const set = (v) => {
      const on = v ? 1 : 0;
      if (act === "fwd") {
        touch[p].fwd = on;
        if (!on && !(touch[p].boostUntil && performance.now() < touch[p].boostUntil)) {
          // keep boost flag until boostUntil expires (handled in readInput)
        }
      } else if (act === "rev") touch[p].rev = on;
      btn.classList.toggle("is-held", !!v);
    };
    const down = (e) => {
      e.preventDefault();
      e.stopPropagation();
      if (act === "fwd") {
        const now = performance.now();
        if (now - fwdTapAt[p] < FWD_DOUBLE_MS) {
          touch[p].boostUntil = now + FWD_BOOST_MS;
          touch[p].boost = 1;
          btn.classList.add("is-boosting");
          setTimeout(() => btn.classList.remove("is-boosting"), FWD_BOOST_MS);
        }
        fwdTapAt[p] = now;
      }
      set(1);
      try { btn.setPointerCapture(e.pointerId); } catch (_) {}
      try {
        if (window.SimilarizeViewportFS) window.SimilarizeViewportFS.enter();
      } catch (_) {}
    };
    const up = (e) => {
      e.preventDefault();
      set(0);
    };
    btn.addEventListener("pointerdown", down);
    btn.addEventListener("pointerup", up);
    btn.addEventListener("pointercancel", up);
    btn.addEventListener("lostpointercapture", () => set(0));
  });
}

function bindTouch() {
  document.querySelectorAll(".pads .stick-wrap").forEach(bindSteerStick);
  bindPadButtons();
}

function init() {
  renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: false });
  renderer.setClearColor(0x0c1014);
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;

  scene = new THREE.Scene();
  scene.fog = new THREE.Fog(0x0c1014, 40, 90);

  camera = new THREE.PerspectiveCamera(55, 1, 0.1, 200);
  camera.position.copy(camPos);

  const hemi = new THREE.HemisphereLight(0xb8c8e0, 0x2a3a28, 0.85);
  scene.add(hemi);
  const sun = new THREE.DirectionalLight(0xffe2b0, 1.15);
  sun.position.set(12, 28, 10);
  sun.castShadow = true;
  sun.shadow.mapSize.set(1024, 1024);
  sun.shadow.camera.left = -30;
  sun.shadow.camera.right = 30;
  sun.shadow.camera.top = 30;
  sun.shadow.camera.bottom = -30;
  scene.add(sun);
  const fill = new THREE.DirectionalLight(0x88aaff, 0.25);
  fill.position.set(-10, 12, -8);
  scene.add(fill);

  buildArena();

  ballMesh = createBallMesh();
  scene.add(ballMesh);
  ball = { pos: new THREE.Vector3(0, BALL_R, 0), vx: 0, vy: 0, vz: 0 };

  const mesh0 = makeCarMesh(0xe4572e);
  const mesh1 = makeCarMesh(0x3b82f6);
  scene.add(mesh0, mesh1);
  cars = [
    {
      mesh: mesh0,
      pos: new THREE.Vector3(-8, CAR_HALF.y, 0),
      yaw: 0,
      vx: 0, vy: 0, vz: 0,
      boost: 1,
      onGround: true,
      jumpCd: 0,
      autoKickCd: 0,
      _holdingFwd: false,
      _trackAcc: 0,
    },
    {
      mesh: mesh1,
      pos: new THREE.Vector3(8, CAR_HALF.y, 0),
      yaw: Math.PI,
      vx: 0, vy: 0, vz: 0,
      boost: 1,
      onGround: true,
      jumpCd: 0,
      autoKickCd: 0,
      _holdingFwd: false,
      _trackAcc: 0,
    },
  ];

  clock = new THREE.Clock();
  bindKeys();
  bindTouch();
  bindOnlineUI();
  bindAudioUI();
  applyPadMode();
  onResize();
  window.addEventListener("resize", onResize);

  if (window.SimilarizeViewportFS) {
    try {
      window.SimilarizeViewportFS.ensureCss();
      window.SimilarizeViewportFS.bindGesture(canvas);
    } catch (_) {}
  }

  boot.hidden = true;
  showBanner("Soccer RC 3D", "", 1.4);
  freezeT = 0.8;
  requestAnimationFrame(tick);
  console.info("[Soccer RC 3D]", CACHE, "ready");
}

init();
