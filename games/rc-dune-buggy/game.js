/**
 * RC Dune Buggy — overhead arcade RC prototype (Three.js CDN).
 * Four Froggie seats only: James, Jimmy, Bubbles, Rexy (overhead name labels).
 * Local pads + Host/Join; elevated dune track (hills/drops/ramps/banks).
 * Sand/hardpack friction + dust. Light WebXR for Quest browser.
 */
import * as THREE from "three";
import { createNet, fillQr } from "./net.js?v=20261007-dune2";

const CACHE = "20261007-dune2";
const LAPS = 3;
const N_CARS = 4;
const COLORS = [0xe85d2a, 0x3b82f6, 0xf2c21b, 0x2fa84f];
/** Exactly the four Froggies — never invent other names. */
const NAMES = ["James", "Jimmy", "Bubbles", "Rexy"];
const WORLD = 72;
const HALF = WORLD * 0.5;
const GRAVITY = 32;
const ACCEL = 42;
const MAX_SPEED = 22;
const TURN = 2.6;
const DRAG_AIR = 0.992;
const SAND_DRAG = 0.955;
const HARD_DRAG = 0.978;
const JUMP_BOOST = 1.15;

const canvas = document.getElementById("view");
const boot = document.getElementById("boot");
const bannerEl = document.getElementById("banner");
const standingsEl = document.getElementById("standings");
const lapLabel = document.getElementById("lapLabel");
const resetBtn = document.getElementById("resetBtn");
const xrBtn = document.getElementById("xrBtn");

let renderer, scene, camera, clock;
let terrainMesh, heightData, heightSeg = 128;
let trackCenter = []; // xz loop samples for AI + progress
let cars = [];
let dust;
let keys = Object.create(null);
let touchSteer = { x: 0, y: 0 };
let touchFwd = false, touchRev = false;
let raceTime = 0;
let finished = false;
let bannerT = 0;
let xrSession = null;

// Host/Join (network) — separate from local 4-pad seating
let net = null;
let netRemote = { 1: { steer: 0, throttle: 0 }, 2: { steer: 0, throttle: 0 }, 3: { steer: 0, throttle: 0 } };
let netGuestPending = null;
const onlineStatus = document.getElementById("onlineStatus");
const roomCodeInput = document.getElementById("roomCodeInput");
const hostPanel = document.getElementById("hostPanel");
const hostCodeEl = document.getElementById("hostCode");
const joinQr = document.getElementById("joinQr");
const leaveBtn = document.getElementById("leaveBtn");
const hostBtn = document.getElementById("hostBtn");
const joinBtn = document.getElementById("joinBtn");

// --- heightfield ---
function noise2(x, z) {
  const n = Math.sin(x * 1.7 + z * 0.9) * 0.5
    + Math.sin(x * 0.45 - z * 1.3) * 0.35
    + Math.sin(x * 2.8 + z * 2.1) * 0.15;
  return n;
}

/** Track elevation profile: hills, valleys, ramps, drops, banked turns (not flat). */
function trackElevation(ang, er) {
  // Rolling hills / downs along the lap
  let h = Math.sin(ang * 2.0) * 2.8 + Math.sin(ang * 3.0 + 0.6) * 1.6;
  // Long climb then drop (quarter-lap)
  const climb = Math.sin(ang + 0.3);
  if (climb > 0.2) h += (climb - 0.2) * 5.5;
  if (climb < -0.35) h += (climb + 0.35) * 3.2; // valley floor
  // Ramp jumps (steep front, drop off back)
  const rampA = Math.max(0, Math.sin(ang * 2 + 0.5));
  h += Math.pow(rampA, 5) * 4.2;
  const rampB = Math.max(0, Math.sin(ang * 2 + 2.9));
  h += Math.pow(rampB, 7) * 5.0;
  // Sharp drop after second ramp peak
  const drop = Math.max(0, Math.sin(ang * 2 + 3.35));
  h -= Math.pow(drop, 4) * 2.2;
  // Banked turns: raise outer edge more in tight oval ends (high curvature)
  const turnBank = 0.55 + 0.45 * Math.abs(Math.sin(ang * 2)); // stronger at ends
  if (er > 0.88) h += (er - 0.88) * 14 * turnBank;
  if (er < 0.78 && er > 0.55) h -= (0.78 - er) * 2.5; // mild inner dip
  return h;
}

function buildHeightData() {
  const n = heightSeg + 1;
  const data = new Float32Array(n * n);
  for (let iz = 0; iz < n; iz++) {
    for (let ix = 0; ix < n; ix++) {
      const u = ix / heightSeg, v = iz / heightSeg;
      const x = (u - 0.5) * WORLD;
      const z = (v - 0.5) * WORLD;
      const ang = Math.atan2(z, x);
      const rx = 26 + Math.cos(ang * 2) * 3;
      const rz = 18 + Math.sin(ang * 3) * 2;
      const er = Math.hypot(x / rx, z / rz);
      let h = 0;
      if (er > 0.52 && er < 1.28) {
        h = trackElevation(ang, er) + noise2(x * 0.07, z * 0.07) * 0.55;
      } else {
        // Off-track dunes — taller hills around the arena
        h = 2.2 + noise2(x * 0.045, z * 0.045) * 4.5 + Math.max(0, er - 1.25) * 5;
        h += Math.sin(x * 0.12) * Math.cos(z * 0.1) * 1.8;
      }
      // Center bowl / mesa
      if (er < 0.48) {
        h = 0.6 + (0.48 - er) * 5.5 + noise2(x * 0.1, z * 0.1) * 0.8;
      }
      data[iz * n + ix] = h;
    }
  }
  return data;
}

function sampleHeight(x, z) {
  const n = heightSeg + 1;
  const u = (x / WORLD + 0.5) * heightSeg;
  const v = (z / WORLD + 0.5) * heightSeg;
  const x0 = Math.max(0, Math.min(heightSeg - 1, Math.floor(u)));
  const z0 = Math.max(0, Math.min(heightSeg - 1, Math.floor(v)));
  const fx = Math.max(0, Math.min(1, u - x0));
  const fz = Math.max(0, Math.min(1, v - z0));
  const i00 = z0 * n + x0;
  const i10 = z0 * n + (x0 + 1);
  const i01 = (z0 + 1) * n + x0;
  const i11 = (z0 + 1) * n + (x0 + 1);
  const h0 = heightData[i00] * (1 - fx) + heightData[i10] * fx;
  const h1 = heightData[i01] * (1 - fx) + heightData[i11] * fx;
  return h0 * (1 - fz) + h1 * fz;
}

function sampleNormal(x, z) {
  const e = 0.6;
  const hx = sampleHeight(x + e, z) - sampleHeight(x - e, z);
  const hz = sampleHeight(x, z + e) - sampleHeight(x, z - e);
  const n = new THREE.Vector3(-hx, 2 * e, -hz).normalize();
  return n;
}

/** 0 = hardpack track, 1 = deep sand */
function surfaceSand(x, z) {
  const ang = Math.atan2(z, x);
  const rx = 26 + Math.cos(ang * 2) * 3;
  const rz = 18 + Math.sin(ang * 3) * 2;
  const er = Math.hypot(x / rx, z / rz);
  if (er > 0.72 && er < 1.05) return 0.15; // hardpack ribbon
  if (er > 0.65 && er < 1.15) return 0.45;
  return 0.95;
}

function buildTrackCenter() {
  const pts = [];
  const steps = 96;
  for (let i = 0; i < steps; i++) {
    const a = (i / steps) * Math.PI * 2;
    const rx = 26 + Math.cos(a * 2) * 3;
    const rz = 18 + Math.sin(a * 3) * 2;
    const x = Math.cos(a) * rx * 0.88;
    const z = Math.sin(a) * rz * 0.88;
    pts.push({ x, z, a });
  }
  trackCenter = pts;
}

function nearestProgress(x, z) {
  let best = 0, bestD = 1e9;
  for (let i = 0; i < trackCenter.length; i++) {
    const p = trackCenter[i];
    const d = (p.x - x) ** 2 + (p.z - z) ** 2;
    if (d < bestD) { bestD = d; best = i; }
  }
  return best / trackCenter.length;
}

function buildTerrain() {
  heightData = buildHeightData();
  buildTrackCenter();
  const geo = new THREE.PlaneGeometry(WORLD, WORLD, heightSeg, heightSeg);
  geo.rotateX(-Math.PI / 2);
  const pos = geo.attributes.position;
  const colors = new Float32Array(pos.count * 3);
  const cSand = new THREE.Color(0xc4a574);
  const cHard = new THREE.Color(0x8b7355);
  const cDune = new THREE.Color(0xd4b896);
  const tmp = new THREE.Color();
  for (let i = 0; i < pos.count; i++) {
    const x = pos.getX(i), z = pos.getZ(i);
    const ix = i % (heightSeg + 1);
    const iz = Math.floor(i / (heightSeg + 1));
    const h = heightData[iz * (heightSeg + 1) + ix];
    pos.setY(i, h);
    const sand = surfaceSand(x, z);
    tmp.copy(cHard).lerp(cSand, sand);
    if (sand > 0.7) tmp.lerp(cDune, 0.4);
    colors[i * 3] = tmp.r; colors[i * 3 + 1] = tmp.g; colors[i * 3 + 2] = tmp.b;
  }
  geo.setAttribute("color", new THREE.BufferAttribute(colors, 3));
  geo.computeVertexNormals();
  const mat = new THREE.MeshStandardMaterial({
    vertexColors: true, roughness: 0.95, metalness: 0.02, flatShading: false,
  });
  terrainMesh = new THREE.Mesh(geo, mat);
  terrainMesh.receiveShadow = true;
  scene.add(terrainMesh);

  // track edge markers + obstacles
  const rockMat = new THREE.MeshStandardMaterial({ color: 0x6b5a4a, roughness: 0.9 });
  for (let i = 0; i < 18; i++) {
    const a = (i / 18) * Math.PI * 2 + 0.2;
    const rx = 26 + Math.cos(a * 2) * 3;
    const rz = 18 + Math.sin(a * 3) * 2;
    // outer rocks
    placeRock(Math.cos(a) * rx * 1.18, Math.sin(a) * rz * 1.18, rockMat, 0.6 + (i % 3) * 0.25);
    if (i % 3 === 0) {
      placeRock(Math.cos(a + 0.15) * rx * 0.62, Math.sin(a + 0.15) * rz * 0.62, rockMat, 0.5);
    }
  }

  // finish gate
  const gateMat = new THREE.MeshStandardMaterial({ color: 0xf5e6d0, roughness: 0.6 });
  const p = trackCenter[0];
  const y = sampleHeight(p.x, p.z);
  for (const side of [-1, 1]) {
    const post = new THREE.Mesh(new THREE.BoxGeometry(0.35, 3.2, 0.35), gateMat);
    post.position.set(p.x + side * 3.2, y + 1.6, p.z);
    post.castShadow = true;
    scene.add(post);
  }
  const bar = new THREE.Mesh(new THREE.BoxGeometry(6.8, 0.25, 0.25), new THREE.MeshStandardMaterial({ color: 0xe85d2a }));
  bar.position.set(p.x, y + 3.1, p.z);
  scene.add(bar);

  // sky / fog
  scene.background = new THREE.Color(0xd4a574);
  scene.fog = new THREE.Fog(0xd4a574, 55, 110);
}

function placeRock(x, z, mat, s) {
  const y = sampleHeight(x, z);
  const m = new THREE.Mesh(new THREE.DodecahedronGeometry(s, 0), mat);
  m.position.set(x, y + s * 0.45, z);
  m.rotation.set(Math.random(), Math.random(), Math.random());
  m.castShadow = true;
  scene.add(m);
}

function makeBuggy(colorHex) {
  const g = new THREE.Group();
  const bodyMat = new THREE.MeshStandardMaterial({ color: colorHex, roughness: 0.55, metalness: 0.25 });
  const dark = new THREE.MeshStandardMaterial({ color: 0x1a1d24, roughness: 0.8 });
  const chrome = new THREE.MeshStandardMaterial({ color: 0xcccccc, metalness: 0.7, roughness: 0.35 });

  const chassis = new THREE.Mesh(new THREE.BoxGeometry(1.9, 0.35, 1.15), bodyMat);
  chassis.position.y = 0.45;
  chassis.castShadow = true;
  g.add(chassis);

  const hood = new THREE.Mesh(new THREE.BoxGeometry(0.7, 0.22, 1.05), bodyMat);
  hood.position.set(0.55, 0.62, 0);
  g.add(hood);

  const cage = new THREE.Mesh(new THREE.BoxGeometry(0.9, 0.55, 0.95), dark);
  cage.position.set(-0.25, 0.78, 0);
  g.add(cage);

  const roll = new THREE.Mesh(new THREE.TorusGeometry(0.55, 0.05, 6, 12, Math.PI), chrome);
  roll.rotation.z = Math.PI / 2;
  roll.position.set(-0.15, 1.05, 0);
  g.add(roll);

  const wheelGeo = new THREE.CylinderGeometry(0.38, 0.38, 0.28, 10);
  const wheels = [];
  [[0.65, 0.55], [0.65, -0.55], [-0.7, 0.55], [-0.7, -0.55]].forEach(([x, z], i) => {
    const w = new THREE.Mesh(wheelGeo, dark);
    w.rotation.x = Math.PI / 2; // axle across the car
    w.position.set(x, 0.38, z);
    w.castShadow = true;
    g.add(w);
    wheels.push(w);
  });
  // model is built nose +X; car drives along +Z, so turn it to face forward
  const outer = new THREE.Group();
  g.rotation.y = -Math.PI / 2;
  outer.add(g);
  outer.userData.wheels = wheels;
  return outer;
}


function makeNameLabel(name, colorHex) {
  const c = document.createElement("canvas");
  c.width = 256; c.height = 64;
  const ctx = c.getContext("2d");
  ctx.clearRect(0, 0, 256, 64);
  // pill background
  ctx.fillStyle = "rgba(0,0,0,0.55)";
  roundRect(ctx, 28, 10, 200, 44, 14);
  ctx.fill();
  ctx.strokeStyle = "#" + colorHex.toString(16).padStart(6, "0");
  ctx.lineWidth = 3;
  roundRect(ctx, 28, 10, 200, 44, 14);
  ctx.stroke();
  ctx.font = "700 28px Outfit, system-ui, sans-serif";
  ctx.textAlign = "center";
  ctx.textBaseline = "middle";
  ctx.fillStyle = "#fff6e8";
  ctx.fillText(name, 128, 33);
  const tex = new THREE.CanvasTexture(c);
  tex.colorSpace = THREE.SRGBColorSpace;
  const mat = new THREE.SpriteMaterial({ map: tex, transparent: true, depthTest: true });
  const spr = new THREE.Sprite(mat);
  spr.scale.set(3.6, 0.9, 1);
  spr.center.set(0.5, 0);
  return spr;
}
function roundRect(ctx, x, y, w, h, r) {
  ctx.beginPath();
  ctx.moveTo(x + r, y);
  ctx.arcTo(x + w, y, x + w, y + h, r);
  ctx.arcTo(x + w, y + h, x, y + h, r);
  ctx.arcTo(x, y + h, x, y, r);
  ctx.arcTo(x, y, x + w, y, r);
  ctx.closePath();
}

function makeCar(i, spawn) {
  const mesh = makeBuggy(COLORS[i]);
  const label = makeNameLabel(NAMES[i], COLORS[i]);
  scene.add(mesh);
  scene.add(label);
  const c = {
    i,
    mesh,
    label,
    name: NAMES[i],
    color: COLORS[i],
    x: spawn.x, y: spawn.y, z: spawn.z,
    yaw: spawn.yaw,
    vx: 0, vz: 0, vy: 0,
    grounded: true,
    pitch: 0, roll: 0,
    susPitch: 0, susRoll: 0,
    progress: 0, lap: 0, lastProg: 0,
    finished: false, finishTime: 0,
    human: false, padSlot: -1,
    aiTarget: 0,
    dustCool: 0,
  };
  syncMesh(c);
  return c;
}

function spawnPos(i) {
  const p0 = trackCenter[0];
  const p1 = trackCenter[2];
  const dx = p1.x - p0.x, dz = p1.z - p0.z;
  const yaw = Math.atan2(dx, dz);
  const side = (i % 2 === 0 ? -1 : 1) * (0.9 + (i >> 1) * 0.15);
  const back = -i * 2.2;
  const fx = Math.sin(yaw), fz = Math.cos(yaw);
  const rx = Math.cos(yaw), rz = -Math.sin(yaw);
  const x = p0.x + fx * back + rx * side;
  const z = p0.z + fz * back + rz * side;
  const y = sampleHeight(x, z) + 0.4;
  return { x, y, z, yaw };
}

function syncMesh(c) {
  c.mesh.position.set(c.x, c.y, c.z);
  c.mesh.rotation.order = "YXZ";
  c.mesh.rotation.y = c.yaw;
  c.mesh.rotation.x = c.pitch + c.susPitch;
  c.mesh.rotation.z = c.roll + c.susRoll;
  // Overhead Froggie name — world-space sprite (billboards; not parented to banked chassis)
  if (c.label) {
    c.label.position.set(c.x, c.y + 2.35, c.z);
  }
}

// --- dust particles ---
function makeDust() {
  const N = 400;
  const geo = new THREE.BufferGeometry();
  const pos = new Float32Array(N * 3);
  const life = new Float32Array(N);
  for (let i = 0; i < N; i++) { pos[i * 3 + 1] = -99; life[i] = 0; }
  geo.setAttribute("position", new THREE.BufferAttribute(pos, 3));
  const mat = new THREE.PointsMaterial({
    color: 0xc9b08a, size: 0.45, transparent: true, opacity: 0.55,
    depthWrite: false, sizeAttenuation: true,
  });
  const pts = new THREE.Points(geo, mat);
  pts.frustumCulled = false;
  scene.add(pts);
  return { pts, pos, life, N, cursor: 0 };
}

function emitDust(x, y, z, n, speed) {
  if (!dust) return;
  const count = Math.min(n, 8);
  for (let k = 0; k < count; k++) {
    const i = dust.cursor++ % dust.N;
    dust.pos[i * 3] = x + (Math.random() - 0.5) * 0.8;
    dust.pos[i * 3 + 1] = y + 0.1;
    dust.pos[i * 3 + 2] = z + (Math.random() - 0.5) * 0.8;
    dust.life[i] = 0.4 + Math.random() * 0.5 + speed * 0.02;
  }
  dust.pts.geometry.attributes.position.needsUpdate = true;
}

function updateDust(dt) {
  if (!dust) return;
  let dirty = false;
  for (let i = 0; i < dust.N; i++) {
    if (dust.life[i] <= 0) continue;
    dust.life[i] -= dt;
    dust.pos[i * 3 + 1] += dt * 0.8;
    if (dust.life[i] <= 0) dust.pos[i * 3 + 1] = -99;
    dirty = true;
  }
  if (dirty) dust.pts.geometry.attributes.position.needsUpdate = true;
}

// --- input ---
function bindInput() {
  addEventListener("keydown", (e) => { keys[e.code] = true; if (["ArrowUp","ArrowDown","ArrowLeft","ArrowRight","Space"].includes(e.code)) e.preventDefault(); });
  addEventListener("keyup", (e) => { keys[e.code] = false; });

  const wrap = document.querySelector(".stick-wrap");
  const knob = wrap && wrap.querySelector(".stick-knob");
  let active = null;
  function setStick(clientX, clientY) {
    const r = wrap.getBoundingClientRect();
    const cx = r.left + r.width / 2, cy = r.top + r.height / 2;
    let dx = (clientX - cx) / (r.width * 0.42);
    let dy = (clientY - cy) / (r.height * 0.42);
    const m = Math.hypot(dx, dy);
    if (m > 1) { dx /= m; dy /= m; }
    touchSteer.x = dx; touchSteer.y = dy;
    if (knob) knob.style.transform = `translate(${dx * 42}%, ${dy * 42}%)`;
  }
  function clearStick() {
    touchSteer.x = 0; touchSteer.y = 0;
    if (knob) knob.style.transform = "translate(0,0)";
  }
  if (wrap) {
    wrap.addEventListener("pointerdown", (e) => { active = e.pointerId; wrap.setPointerCapture(e.pointerId); setStick(e.clientX, e.clientY); });
    wrap.addEventListener("pointermove", (e) => { if (e.pointerId === active) setStick(e.clientX, e.clientY); });
    wrap.addEventListener("pointerup", (e) => { if (e.pointerId === active) { active = null; clearStick(); } });
    wrap.addEventListener("pointercancel", () => { active = null; clearStick(); });
  }
  document.querySelectorAll(".pad-btn").forEach((btn) => {
    const act = btn.dataset.act;
    const set = (on) => {
      btn.classList.toggle("on", on);
      if (act === "fwd") touchFwd = on;
      if (act === "rev") touchRev = on;
    };
    btn.addEventListener("pointerdown", (e) => { e.preventDefault(); btn.setPointerCapture(e.pointerId); set(true); });
    btn.addEventListener("pointerup", () => set(false));
    btn.addEventListener("pointercancel", () => set(false));
  });
  resetBtn.addEventListener("click", resetRace);
}

function readHumanInput(carIndex) {
  const c = cars[carIndex];
  let steer = 0, throttle = 0;
  const GP = window.SimilarizeGamepad;

  if (c.padSlot >= 0 && GP) {
    const gp = GP.pollPad(c.padSlot);
    if (gp && gp.connected) {
      // RC feel: left stick X = steering wheel only (ignore stick Y)
      if (gp.dpad && gp.dpad.l) steer = -1;
      else if (gp.dpad && gp.dpad.r) steer = 1;
      else steer = gp.lx || 0;
      // RT = forward throttle, LT = reverse (Soccer RC pattern)
      let fwd = typeof gp.rtValue === "number" ? gp.rtValue : gp.rt ? 1 : 0;
      let rev = typeof gp.ltValue === "number" ? gp.ltValue : gp.lt ? 1 : 0;
      fwd = Math.max(0, Math.min(1, fwd));
      rev = Math.max(0, Math.min(1, rev));
      if (fwd > 0 && rev > 0) {
        if (fwd >= rev) rev = 0;
        else fwd = 0;
      }
      throttle = fwd - rev;
      return {
        steer: -Math.max(-1, Math.min(1, steer)),
        throttle: Math.max(-1, Math.min(1, throttle)),
      };
    }
  }

  if (carIndex === 0) {
    if (keys.ArrowLeft || keys.KeyA) steer -= 1;
    if (keys.ArrowRight || keys.KeyD) steer += 1;
    if (keys.ArrowUp || keys.KeyW) throttle += 1;
    if (keys.ArrowDown || keys.KeyS) throttle -= 1;
    // Phone: stick X = steer; FWD/REV buttons = throttle (no stick Y drive)
    steer += touchSteer.x;
    if (touchFwd) throttle += 1;
    if (touchRev) throttle -= 1;
  }
  return {
    steer: -Math.max(-1, Math.min(1, steer)),
    throttle: Math.max(-1, Math.min(1, throttle)),
  };
}

function assignSeats() {
  const GP = window.SimilarizeGamepad;
  let pads = [];
  if (GP && typeof GP.connectedIndices === "function") {
    pads = GP.connectedIndices(4) || [];
  }
  document.body.classList.toggle("gp", pads.length > 0 && !(net && net.connected));

  // Network party: host sim; seat i human if in roster (else AI). Guests only drive their seat locally for input send.
  if (net && net.connected && net.role) {
    const seats = new Set((net.roster || []).map((r) => r.seat));
    for (let i = 0; i < N_CARS; i++) {
      if (net.role === "host") {
        cars[i].human = seats.has(i); // host + joined guests; empty = AI
        cars[i].padSlot = (i === 0 && pads[0] != null) ? pads[0] : -1;
        cars[i].netSeat = seats.has(i);
      } else {
        // Guest: local input only for own seat; others shown via state
        cars[i].human = (i === net.seat);
        cars[i].padSlot = (i === net.seat && pads[0] != null) ? pads[0] : -1;
        cars[i].netSeat = seats.has(i);
      }
    }
    // Host local pads beyond seat0: map extra pads to empty human seats if present
    if (net.role === "host" && pads.length > 1) {
      let pi = 1;
      for (let i = 1; i < N_CARS && pi < pads.length; i++) {
        if (!seats.has(i)) {
          cars[i].human = true;
          cars[i].padSlot = pads[pi++];
        }
      }
    }
    return;
  }

  for (let i = 0; i < N_CARS; i++) {
    if (i < pads.length) {
      cars[i].human = true;
      cars[i].padSlot = pads[i];
    } else if (i === 0) {
      cars[i].human = true;
      cars[i].padSlot = -1;
    } else {
      cars[i].human = false;
      cars[i].padSlot = -1;
    }
  }
  if (pads.length > 0) {
    cars[0].human = true;
    cars[0].padSlot = pads[0];
  }
}

function aiInput(c) {
  const look = (c.aiTarget || 0) % trackCenter.length;
  // aim a few waypoints ahead
  const ahead = trackCenter[(look + 4) % trackCenter.length];
  const tx = ahead.x - c.x, tz = ahead.z - c.z;
  const want = Math.atan2(tx, tz);
  let diff = want - c.yaw;
  while (diff > Math.PI) diff -= Math.PI * 2;
  while (diff < -Math.PI) diff += Math.PI * 2;
  const steer = Math.max(-1, Math.min(1, diff * 1.8));
  // slow for sharp turns
  const throttle = Math.abs(diff) > 0.9 ? 0.45 : 0.95;
  // update waypoint when close
  const near = trackCenter[look];
  if ((near.x - c.x) ** 2 + (near.z - c.z) ** 2 < 36) {
    c.aiTarget = (look + 1) % trackCenter.length;
  }
  return { steer, throttle };
}

function updateCar(c, dt) {
  if (c.finished) {
    c.vx *= 0.95; c.vz *= 0.95;
    c.x += c.vx * dt; c.z += c.vz * dt;
    settleGround(c, dt);
    syncMesh(c);
    return;
  }

  let inp;
  if (net && net.connected && net.role === "guest") {
    if (c.i === net.seat) {
      // Reuse P1 keyboard/touch/pad0 for whatever seat we were assigned
      inp = readHumanInput(0);
      if (c.padSlot >= 0) inp = readHumanInput(c.i);
    } else {
      inp = { steer: 0, throttle: 0 };
    }
  } else if (net && net.connected && net.role === "host" && c.i !== 0 && c.human && c.padSlot < 0) {
    inp = netRemote[c.i] || { steer: 0, throttle: 0 };
  } else {
    inp = c.human ? readHumanInput(c.i) : aiInput(c);
  }
  const speed = Math.hypot(c.vx, c.vz);
  const sand = surfaceSand(c.x, c.z);
  const drag = SAND_DRAG + (HARD_DRAG - SAND_DRAG) * (1 - sand);
  const accelMul = 1 - sand * 0.35;

  if (c.grounded) {
    c.yaw += inp.steer * TURN * (0.55 + 0.45 * Math.min(1, speed / 8)) * dt * Math.sign(inp.throttle || speed || 1);
    // when reversing, steer flips feel — keep arcade-simple
    const fx = Math.sin(c.yaw), fz = Math.cos(c.yaw);
    c.vx += fx * inp.throttle * ACCEL * accelMul * dt;
    c.vz += fz * inp.throttle * ACCEL * accelMul * dt;
    c.vx *= Math.pow(drag, dt * 60);
    c.vz *= Math.pow(drag, dt * 60);
    // lateral grip (arcade)
    const forward = c.vx * fx + c.vz * fz;
    const latx = c.vx - fx * forward;
    const latz = c.vz - fz * forward;
    const grip = 0.82 + sand * 0.12; // sand slips a bit more longitudinally already
    c.vx = fx * forward + latx * Math.pow(grip, dt * 60);
    c.vz = fz * forward + latz * Math.pow(grip, dt * 60);
  } else {
    c.vx *= Math.pow(DRAG_AIR, dt * 60);
    c.vz *= Math.pow(DRAG_AIR, dt * 60);
    c.vy -= GRAVITY * dt;
  }

  // clamp speed
  const sp2 = Math.hypot(c.vx, c.vz);
  if (sp2 > MAX_SPEED) {
    c.vx *= MAX_SPEED / sp2;
    c.vz *= MAX_SPEED / sp2;
  }

  c.x += c.vx * dt;
  c.z += c.vz * dt;
  c.y += c.vy * dt;

  // bounds soft wall
  const lim = HALF - 2;
  if (Math.abs(c.x) > lim) { c.x = Math.sign(c.x) * lim; c.vx *= -0.3; }
  if (Math.abs(c.z) > lim) { c.z = Math.sign(c.z) * lim; c.vz *= -0.3; }

  settleGround(c, dt);

  // suspension / weight transfer approx
  const ax = inp.throttle * ACCEL * 0.02;
  c.susPitch += (-inp.throttle * 0.12 - c.susPitch) * Math.min(1, dt * 8);
  c.susRoll += (-inp.steer * 0.14 * Math.min(1, speed / 10) - c.susRoll) * Math.min(1, dt * 8);

  // align to terrain
  if (c.grounded) {
    const n = sampleNormal(c.x, c.z);
    const targetPitch = Math.atan2(n.z, n.y) * 0.85;
    const targetRoll = Math.atan2(-n.x, n.y) * 0.85;
    c.pitch += (targetPitch - c.pitch) * Math.min(1, dt * 6);
    c.roll += (targetRoll - c.roll) * Math.min(1, dt * 6);
  } else {
    c.pitch *= 0.98; c.roll *= 0.98;
  }

  // wheels spin
  const wheels = c.mesh.userData.wheels;
  if (wheels) {
    const spin = speed * dt * 2.2;
    wheels.forEach((w) => { w.rotation.y -= spin; });
  }

  // dust on sand when moving
  c.dustCool -= dt;
  if (c.grounded && sand > 0.35 && speed > 4 && c.dustCool <= 0) {
    emitDust(c.x - Math.sin(c.yaw) * 0.6, c.y, c.z - Math.cos(c.yaw) * 0.6, 3 + (sand > 0.7 ? 2 : 0), speed);
    c.dustCool = 0.04;
  }

  // race progress
  const prog = nearestProgress(c.x, c.z);
  let delta = prog - c.lastProg;
  if (delta < -0.5) { // crossed finish forward
    c.lap += 1;
    if (c.lap >= LAPS && !c.finished) {
      c.finished = true;
      c.finishTime = raceTime;
      showBanner(`${c.name} finished!`);
    }
  } else if (delta > 0.5) {
    // going backward over line — ignore lap
    delta = 0;
  }
  c.lastProg = prog;
  c.progress = c.lap + prog;
  if (!c.aiTarget) c.aiTarget = Math.floor(prog * trackCenter.length);

  syncMesh(c);
}

function settleGround(c, dt) {
  const ground = sampleHeight(c.x, c.z) + 0.38;
  if (c.y <= ground) {
    if (!c.grounded && c.vy < -2) {
      // landing squish
      c.susPitch -= 0.08;
    }
    // jump ramp: if slope ahead steep and fast, launch
    if (c.grounded) {
      const n = sampleNormal(c.x, c.z);
      const fx = Math.sin(c.yaw), fz = Math.cos(c.yaw);
      const slope = n.x * fx + n.z * fz;
      const spd = Math.hypot(c.vx, c.vz);
      if (slope > 0.35 && spd > 12 && n.y < 0.92) {
        c.vy = Math.max(c.vy, slope * spd * 0.55 * JUMP_BOOST);
        c.grounded = false;
        c.y = ground + 0.05;
        return;
      }
    }
    c.y = ground;
    c.vy = 0;
    c.grounded = true;
  } else {
    c.grounded = false;
  }
}

function carCollisions() {
  for (let i = 0; i < cars.length; i++) {
    for (let j = i + 1; j < cars.length; j++) {
      const a = cars[i], b = cars[j];
      const dx = b.x - a.x, dz = b.z - a.z;
      const d = Math.hypot(dx, dz);
      const minD = 1.7;
      if (d < minD && d > 0.01) {
        const nx = dx / d, nz = dz / d;
        const push = (minD - d) * 0.5;
        a.x -= nx * push; a.z -= nz * push;
        b.x += nx * push; b.z += nz * push;
        const dvx = b.vx - a.vx, dvz = b.vz - a.vz;
        const impact = dvx * nx + dvz * nz;
        if (impact < 0) {
          a.vx += nx * impact * 0.55; a.vz += nz * impact * 0.55;
          b.vx -= nx * impact * 0.55; b.vz -= nz * impact * 0.55;
        }
      }
    }
  }
}

function updateCamera() {
  // overhead / elevated spectator framing whole track
  const focus = new THREE.Vector3(0, 2, 0);
  let minX = 1e9, maxX = -1e9, minZ = 1e9, maxZ = -1e9;
  for (const c of cars) {
    minX = Math.min(minX, c.x); maxX = Math.max(maxX, c.x);
    minZ = Math.min(minZ, c.z); maxZ = Math.max(maxZ, c.z);
    focus.x += c.x; focus.z += c.z;
  }
  focus.x /= cars.length; focus.z /= cars.length;
  // prefer arena center slightly so whole track stays framed
  focus.x *= 0.35;
  focus.z *= 0.35;
  const span = Math.max(maxX - minX, maxZ - minZ, 40);
  const height = 52 + span * 0.18;
  const desired = new THREE.Vector3(focus.x, height, focus.z + 28);
  camera.position.lerp(desired, 0.08);
  camera.lookAt(focus.x, 1.5, focus.z);
  camera.fov = 42;
  camera.updateProjectionMatrix();
}

function updateHud() {
  const ranked = [...cars].sort((a, b) => {
    if (a.finished && b.finished) return a.finishTime - b.finishTime;
    if (a.finished) return -1;
    if (b.finished) return 1;
    return b.progress - a.progress;
  });
  standingsEl.innerHTML = ranked.map((c, i) => {
    const tag = c.human ? (c.padSlot >= 0 ? `Pad ${c.padSlot + 1}` : "P1") : "AI";
    const lap = Math.min(LAPS, c.lap + 1);
    return `<div class="row"><span class="dot" style="background:#${c.color.toString(16).padStart(6,"0")}"></span>${i + 1}. ${c.name} <small>${tag} · L${lap}</small></div>`;
  }).join("");
  const p1 = cars[0];
  lapLabel.textContent = p1.finished ? "Finished" : `Lap ${Math.min(LAPS, p1.lap + 1)}/${LAPS}`;

  if (bannerT > 0) {
    bannerT -= 0.016;
    if (bannerT <= 0) bannerEl.classList.remove("on");
  }

  if (!finished && cars.every((c) => c.finished)) {
    finished = true;
    showBanner("Race complete — Reset to go again");
  }
}

function showBanner(msg) {
  bannerEl.textContent = msg;
  bannerEl.classList.add("on");
  bannerT = 2.4;
}

function resetRace() {
  finished = false;
  raceTime = 0;
  for (let i = 0; i < cars.length; i++) {
    const s = spawnPos(i);
    const c = cars[i];
    c.x = s.x; c.y = s.y; c.z = s.z; c.yaw = s.yaw;
    c.vx = c.vz = c.vy = 0;
    c.pitch = c.roll = c.susPitch = c.susRoll = 0;
    c.lap = 0; c.progress = 0; c.lastProg = 0;
    c.finished = false; c.finishTime = 0;
    c.aiTarget = 2;
    c.grounded = true;
    syncMesh(c);
  }
  showBanner("3 · 2 · 1 · GO!");
  if (net && net.connected && net.role === "host") {
    net.sendReset();
    net.sendBanner("3 · 2 · 1 · GO!");
  }
}

function packNetState() {
  return {
    t: raceTime,
    cars: cars.map((c) => ({
      x: round3(c.x), y: round3(c.y), z: round3(c.z),
      yaw: round3(c.yaw), vx: round3(c.vx), vz: round3(c.vz), vy: round3(c.vy),
      lap: c.lap, progress: round3(c.progress), finished: c.finished ? 1 : 0,
      pitch: round3(c.pitch), roll: round3(c.roll),
      susPitch: round3(c.susPitch), susRoll: round3(c.susRoll),
    })),
  };
}
function round3(n) { return Math.round(n * 1000) / 1000; }
function applyNetState(msg) {
  if (!msg || !msg.cars) return;
  msg.cars.forEach((s, i) => {
    const c = cars[i];
    if (!c || !s) return;
    c.x = s.x; c.y = s.y; c.z = s.z; c.yaw = s.yaw;
    c.vx = s.vx; c.vz = s.vz; c.vy = s.vy;
    c.lap = s.lap | 0; c.progress = s.progress; c.finished = !!s.finished;
    c.pitch = s.pitch || 0; c.roll = s.roll || 0;
    c.susPitch = s.susPitch || 0; c.susRoll = s.susRoll || 0;
    syncMesh(c);
  });
  if (typeof msg.t === "number") raceTime = msg.t;
}

function frame() {
  const dt = Math.min(0.05, clock.getDelta());
  raceTime += dt;
  assignSeats();

  if (net && net.connected && net.role === "guest" && netGuestPending) {
    applyNetState(netGuestPending);
    netGuestPending = null;
    // Still send local input to host
    const mine = cars[net.seat];
    if (mine) {
      const inp = readHumanInput(0);
      net.sendInput(inp);
    }
  } else {
    for (const c of cars) updateCar(c, dt);
    carCollisions();
    if (net && net.connected && net.role === "host") {
      net.sendState(packNetState());
    }
  }

  updateDust(dt);
  if (!xrSession) updateCamera();
  updateHud();
  renderer.render(scene, camera);
}

async function setupXR() {
  if (!navigator.xr || !renderer.xr) return;
  try {
    const ok = await navigator.xr.isSessionSupported("immersive-vr");
    if (!ok) {
      // still offer inline / immersive-ar stub path note via button title
      xrBtn.hidden = false;
      xrBtn.title = "WebXR VR not reported — Quest browser may still prompt";
      xrBtn.textContent = "WebXR";
      xrBtn.onclick = () => tryEnterXR();
      return;
    }
    xrBtn.hidden = false;
    xrBtn.onclick = () => tryEnterXR();
  } catch (_) {
    xrBtn.hidden = false;
    xrBtn.title = "WebXR: open in Quest browser";
    xrBtn.onclick = () => tryEnterXR();
  }
}

async function tryEnterXR() {
  if (!navigator.xr) {
    showBanner("WebXR needs Quest browser (or XR device)");
    return;
  }
  try {
    const session = await navigator.xr.requestSession("immersive-vr", {
      optionalFeatures: ["local-floor", "bounded-floor"],
    });
    renderer.xr.enabled = true;
    await renderer.xr.setSession(session);
    xrSession = session;
    // overhead spectator in XR: lift reference
    camera.position.set(0, 42, 22);
    camera.lookAt(0, 0, 0);
    session.addEventListener("end", () => {
      xrSession = null;
      renderer.xr.enabled = false;
    });
    showBanner("VR · look down at the dunes");
  } catch (err) {
    showBanner("WebXR unavailable here — use Quest browser");
    console.warn("XR", err);
  }
}


function setOnlineStatus(msg, kind) {
  if (!onlineStatus) return;
  onlineStatus.textContent = msg || "";
  onlineStatus.className = "status" + (kind ? " " + kind : "");
}

function updateHostPanel() {
  if (!hostPanel) return;
  const show = !!(net && net.connected && net.code);
  hostPanel.hidden = !show;
  if (leaveBtn) leaveBtn.hidden = !show;
  if (hostBtn) hostBtn.disabled = show;
  if (joinBtn) joinBtn.disabled = show;
  if (roomCodeInput) roomCodeInput.disabled = show;
  if (show) {
    if (hostCodeEl) hostCodeEl.textContent = net.code;
    const url = net.joinUrl(net.code);
    fillQr(joinQr, url);
  }
}

function bindNet() {
  net = createNet({
    onStatus: setOnlineStatus,
    onRoster: () => updateHostPanel(),
    onRemoteInput: (seat, inp) => {
      if (seat >= 1 && seat <= 3) netRemote[seat] = {
        steer: Math.max(-1, Math.min(1, +inp.steer || 0)),
        throttle: Math.max(-1, Math.min(1, +inp.throttle || 0)),
      };
    },
    onState: (msg) => { netGuestPending = msg; },
    onReset: () => resetRace(),
    onBanner: (text) => showBanner(text),
    onPeerLeft: () => {},
  });
  if (hostBtn) hostBtn.addEventListener("click", async () => {
    const code = await net.createRoom();
    if (code && roomCodeInput) roomCodeInput.value = code;
    updateHostPanel();
  });
  if (joinBtn) joinBtn.addEventListener("click", () => {
    net.connect(roomCodeInput && roomCodeInput.value);
    updateHostPanel();
  });
  if (leaveBtn) leaveBtn.addEventListener("click", () => {
    net.disconnect("");
    updateHostPanel();
  });
  if (roomCodeInput) {
    roomCodeInput.addEventListener("keydown", (e) => {
      if (e.key === "Enter") { net.connect(roomCodeInput.value); updateHostPanel(); }
    });
  }
  const copyBtn = document.getElementById("copyJoinBtn");
  if (copyBtn) copyBtn.addEventListener("click", async () => {
    if (!net || !net.code) return;
    const url = net.joinUrl(net.code);
    try {
      await navigator.clipboard.writeText(url);
      setOnlineStatus("Join link copied", "ok");
    } catch (_) {
      setOnlineStatus(url, "ok");
    }
  });
  // Deep link ?join=CODE
  try {
    const q = new URLSearchParams(location.search).get("join");
    if (q) {
      if (roomCodeInput) roomCodeInput.value = q;
      net.connect(q);
      updateHostPanel();
    }
  } catch (_) {}
}

function init() {
  renderer = new THREE.WebGLRenderer({ canvas, antialias: true, powerPreference: "high-performance" });
  renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
  renderer.setSize(innerWidth, innerHeight, false);
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;
  renderer.xr.enabled = true;

  scene = new THREE.Scene();
  camera = new THREE.PerspectiveCamera(42, innerWidth / innerHeight, 0.5, 200);
  camera.position.set(0, 52, 30);
  clock = new THREE.Clock();

  const hemi = new THREE.HemisphereLight(0xffe2bd, 0x6b4e32, 0.85);
  scene.add(hemi);
  const sun = new THREE.DirectionalLight(0xfff0d8, 1.15);
  sun.position.set(30, 50, 20);
  sun.castShadow = true;
  sun.shadow.mapSize.set(1024, 1024);
  sun.shadow.camera.left = -40;
  sun.shadow.camera.right = 40;
  sun.shadow.camera.top = 40;
  sun.shadow.camera.bottom = -40;
  scene.add(sun);

  buildTerrain();
  dust = makeDust();

  cars = [];
  for (let i = 0; i < N_CARS; i++) cars.push(makeCar(i, spawnPos(i)));

  bindInput();
  bindNet();
  setupXR();

  addEventListener("resize", () => {
    const w = innerWidth, h = innerHeight;
    camera.aspect = w / h;
    camera.updateProjectionMatrix();
    renderer.setSize(w, h, false);
  });

  boot.classList.add("hide");
  showBanner("Dune heat · 3 laps");
  renderer.setAnimationLoop(frame);
}

init();
