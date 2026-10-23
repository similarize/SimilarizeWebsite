/**
 * RC Dune Buggy — overhead arcade RC prototype (Three.js CDN).
 * Four Froggie seats only: James, Jimmy, Bubbles, Rexy (overhead name labels).
 * Local pads + Host/Join; elevated dune track (hills/drops/ramps/banks).
 * Sand/hardpack friction + dust. Light WebXR for Quest browser.
 */
import * as THREE from "three";
import { createNet, fillQr } from "./net.js?v=20261007-dune2";

const CACHE = "20261023-dune6";
const LAPS = 3;
const N_CARS = 4;
const COLORS = [0xe85d2a, 0x3b82f6, 0xf2c21b, 0x2fa84f];
/** Exactly the four Froggies — never invent other names. */
const NAMES = ["James", "Jimmy", "Bubbles", "Rexy"];
const WORLD = 116;
const HALF = WORLD * 0.5;
const GRAVITY = 32;
const ACCEL = 42;
const MAX_SPEED = 22;
const TURN = 2.6;
const DRAG_AIR = 0.992;
const SAND_DRAG = 0.955;
const HARD_DRAG = 0.978;
const JUMP_BOOST = 1.15;

/* audio1: shared arcade audio (games/shared/sfx.js) */
const AUD = window.ArcadeAudio || null;
if (AUD) AUD.init({ mood: "sport", engine: "buggy", button: "bottom-right" });
function sfx(name, s) { if (AUD) AUD.play(name, s); }
function isLocalCar(c) { return net && net.connected && net.role === "guest" ? c.i === net.seat : !!c.human; }
const canvas = document.getElementById("view");
const boot = document.getElementById("boot");
const bannerEl = document.getElementById("banner");
const standingsEl = document.getElementById("standings");
const lapLabel = document.getElementById("lapLabel");
const resetBtn = document.getElementById("resetBtn");
const xrBtn = document.getElementById("xrBtn");

let renderer, scene, camera, clock;
let terrainMesh, heightData, heightSeg = 176;
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

/* dune5: spline circuit - sweepers, a hairpin, an infield whoops section, a cliff jump and kickers.
   Track = closed Catmull-Rom loop; terrain height/banking/berms/sand are all derived from distance-to-track. */
const TRACK_WP = [[-36,-32],[-8,-36],[22,-34],[42,-24],[46,-4],[36,10],[18,6],[2,-4],[-12,-12],[-26,-8],[-24,8],[-6,16],[12,26],[26,38],[4,46],[-24,42],[-44,28],[-48,2],[-46,-20]];
const TRACK_SUB = 14;
const TRACK_HW = 4.2;           // half width of the hardpack ribbon
let trackLen = 1, trackSWP = [], trackFeatures = [];
let distGrid = null, sGrid = null, offGrid = null;
const smooth01 = (a, b, x) => { const t = Math.max(0, Math.min(1, (x - a) / (b - a))); return t * t * (3 - 2 * t); };

function buildTrackCenter() {
  const W = TRACK_WP, n = W.length, pts = [];
  const cr = (a, b, c, d, t) => {
    const t2 = t * t, t3 = t2 * t;
    const f = (k) => 0.5 * ((2 * b[k]) + (-a[k] + c[k]) * t + (2 * a[k] - 5 * b[k] + 4 * c[k] - d[k]) * t2 + (-a[k] + 3 * b[k] - 3 * c[k] + d[k]) * t3);
    return [f(0), f(1)];
  };
  for (let i = 0; i < n; i++) for (let k = 0; k < TRACK_SUB; k++) {
    const q = cr(W[(i - 1 + n) % n], W[i], W[(i + 1) % n], W[(i + 2) % n], k / TRACK_SUB);
    pts.push({ x: q[0], z: q[1], wp: i });
  }
  const N = pts.length; let s = 0;
  for (let i = 0; i < N; i++) {
    const a = pts[i], b = pts[(i + 1) % N], c = pts[(i - 1 + N) % N];
    a.yaw = Math.atan2(b.x - c.x, b.z - c.z);
    a.s = s; a.len = Math.hypot(b.x - a.x, b.z - a.z); s += a.len;
  }
  trackLen = s;
  // curvature (dyaw/ds, + = turning right), smoothed
  const raw = pts.map((p, i) => { const b = pts[(i + 1) % N], c = pts[(i - 1 + N) % N]; let d = b.yaw - c.yaw; while (d > Math.PI) d -= 2 * Math.PI; while (d < -Math.PI) d += 2 * Math.PI; return d / (b.len + c.len); });
  pts.forEach((p, i) => { let a = 0; for (let k = -3; k <= 3; k++) a += raw[(i + k + N) % N]; p.curv = a / 7; });
  trackSWP = []; for (let i = 0; i < n; i++) trackSWP.push(pts[i * TRACK_SUB].s);
  trackCenter = pts;
}

const kicker = (s, s0, rise, H, flat, fall) => {
  if (s < s0) return 0;
  if (s < s0 + rise) return H * Math.pow((s - s0) / rise, 1.25);
  if (s < s0 + rise + flat) return H;
  return H * (1 - smooth01(0, fall, s - s0 - rise - flat));
};
/** Elevation along the lap at arc length s (units). */
function trackH(s) {
  const sw = (i, o = 0) => trackSWP[i % trackSWP.length] + o;
  let h = Math.sin(s * 0.062 + 0.5) * 0.7 + Math.sin(s * 0.17 + 1.3) * 0.28;
  h += kicker(s, sw(0, 34), 7, 2.7, 4, 2.2);                    // kicker A on the start straight
  h += 1.9 * smooth01(sw(15), sw(17), s) * (1 - smooth01(sw(17), sw(18, 8), s)); // west dune hump
  // infield whoops (WP7 -> WP9)
  const w0 = sw(7, 3), w1 = w0 + 40;
  if (s > w0 && s < w1) h += Math.sin((s - w0) * 2 * Math.PI / 6.6) * 0.5 * smooth01(w0, w0 + 5, s) * (1 - smooth01(w1 - 5, w1, s));
  // big climb (WP10 -> WP12) and cliff jump off the crest
  h += kicker(s, sw(10, 2), sw(11, 12) - sw(10, 2), 3.8, 5, 5.5);
  h += kicker(s, sw(14, 5), 6, 2.3, 3, 2.2);                    // kicker B on the bottom straight
  return h;
}

function buildHeightData() {
  if (!trackCenter.length) buildTrackCenter();
  const n = heightSeg + 1, data = new Float32Array(n * n);
  distGrid = new Float32Array(n * n); sGrid = new Float32Array(n * n); offGrid = new Float32Array(n * n);
  const P = trackCenter, N = P.length, cell = WORLD / heightSeg;
  const hs = TRACK_HW, edge = hs + 3.2, far = hs + 13;
  const profile = new Float32Array(N); for (let i = 0; i < N; i++) profile[i] = trackH(P[i].s);
  for (let iz = 0; iz < n; iz++) for (let ix = 0; ix < n; ix++) {
    const x = ix * cell - WORLD / 2, z = iz * cell - WORLD / 2;
    let bd = 1e9, bi = 0, bt = 0;
    for (let i = 0; i < N; i++) {
      const a = P[i], b = P[(i + 1) % N], dx = b.x - a.x, dz = b.z - a.z;
      let t = ((x - a.x) * dx + (z - a.z) * dz) / (dx * dx + dz * dz); t = t < 0 ? 0 : t > 1 ? 1 : t;
      const ex = a.x + dx * t - x, ez = a.z + dz * t - z, d = ex * ex + ez * ez;
      if (d < bd) { bd = d; bi = i; bt = t; }
    }
    bd = Math.sqrt(bd);
    const a = P[bi], b = P[(bi + 1) % N];
    const cxp = a.x + (b.x - a.x) * bt, czp = a.z + (b.z - a.z) * bt;
    const yaw = a.yaw + (((b.yaw - a.yaw + 3 * Math.PI) % (2 * Math.PI)) - Math.PI) * bt;
    const off = (x - cxp) * Math.cos(yaw) + (z - czp) * -Math.sin(yaw); // + = right of travel
    const s = a.s + a.len * bt, curv = a.curv + (b.curv - a.curv) * bt;
    const idx = iz * n + ix; distGrid[idx] = bd; sGrid[idx] = s; offGrid[idx] = off;
    const th = profile[bi] + (profile[(bi + 1) % N] - profile[bi]) * bt;
    // wind-blown dunes everywhere; rising rim around the arena
    const m = Math.max(Math.abs(x), Math.abs(z)) / (WORLD / 2);
    let dune = 2.4 + noise2(x * 0.045, z * 0.045) * 3.6 + Math.sin(x * 0.12) * Math.cos(z * 0.1) * 1.6 + smooth01(0.78, 1, m) * 14;
    // banking: the outside of a turn is raised (clamped), fading out at the ribbon edge
    const bank = Math.max(-0.42, Math.min(0.42, -3.4 * curv)) * off * (1 - smooth01(hs, edge, bd));
    let ribbon = th + bank + noise2(x * 0.3, z * 0.3) * 0.05;
    // berm ridge just outside the ribbon keeps cars on track
    const berm = 1.25 * Math.exp(-Math.pow((bd - (hs + 1.4)) / 1.1, 2));
    const w = 1 - smooth01(edge, far, bd);
    data[idx] = bd < edge ? ribbon + berm * smooth01(hs - 0.4, hs + 0.6, bd) : th * w + dune * (1 - w) + berm;
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
function gridAt(arr, x, z) {
  const n = heightSeg + 1;
  const ix = Math.max(0, Math.min(heightSeg, Math.round((x / WORLD + 0.5) * heightSeg)));
  const iz = Math.max(0, Math.min(heightSeg, Math.round((z / WORLD + 0.5) * heightSeg)));
  return arr[iz * n + ix];
}
function surfaceSand(x, z) {
  const d = gridAt(distGrid, x, z);
  if (d < TRACK_HW) return 0.12;
  if (d < TRACK_HW + 2.2) return 0.4;
  if (d < TRACK_HW + 6) return 0.7;
  return 0.95;
}

/** progress 0..1 along the lap; hint (previous progress) limits the search so parallel legs never swap places */
function nearestProgress(x, z, hint) {
  const N = trackCenter.length;
  let best = 0, bestD = 1e9;
  const test = (i) => { const p = trackCenter[i]; const d = (p.x - x) ** 2 + (p.z - z) ** 2; if (d < bestD) { bestD = d; best = i; } };
  if (hint != null) {
    const h = Math.round(hint * N);
    for (let k = -14; k <= 14; k++) test(((h + k) % N + N) % N);
    if (bestD < 14 * 14) return best / N;
    bestD = 1e9;
  }
  for (let i = 0; i < N; i++) test(i);
  return best / N;
}

/** dune6: crisp track markings as thin geometry draped on the heightfield (replaces the blurry per-vertex colour stripes). */
function buildTrackMarkings() {
  const P = trackCenter, N = P.length, L = trackLen, HW = TRACK_HW;
  const R = P.map((p) => [Math.cos(p.yaw), -Math.sin(p.yaw)]);
  const at = (s0) => {
    const s = ((s0 % L) + L) % L;
    let lo = 0, hi = N - 1;
    while (lo < hi) { const m = (lo + hi + 1) >> 1; if (P[m].s <= s) lo = m; else hi = m - 1; }
    const a = P[lo], b = P[(lo + 1) % N], t = a.len > 0 ? (s - a.s) / a.len : 0, r0 = R[lo], r1 = R[(lo + 1) % N];
    let rx = r0[0] + (r1[0] - r0[0]) * t, rz = r0[1] + (r1[1] - r0[1]) * t; const l = Math.hypot(rx, rz) || 1; rx /= l; rz /= l;
    return { x: a.x + (b.x - a.x) * t, z: a.z + (b.z - a.z) * t, rx, rz };
  };
  const pos = [], col = [], tc = new THREE.Color();
  const quad = (s0, s1, o0, o1, hex, lift) => {
    const p0 = at(s0), p1 = at(s1); tc.set(hex);
    const V = [[p0, o0], [p0, o1], [p1, o1], [p1, o0]].map(([p, o]) => { const x = p.x + p.rx * o, z = p.z + p.rz * o; return [x, sampleHeight(x, z) + lift, z]; });
    for (const tri of [[0, 1, 2], [0, 2, 3]]) {
      let [a, b, c] = tri.map((i) => V[i]);
      if ((b[2] - a[2]) * (c[0] - a[0]) - (b[0] - a[0]) * (c[2] - a[2]) < 0) { const t = b; b = c; c = t; }
      pos.push(...a, ...b, ...c); for (let k = 0; k < 3; k++) col.push(tc.r, tc.g, tc.b);
    }
  };
  const STEP = 0.275, RED = 0xd8261c, WHITE = 0xf7f3ea;
  for (let s = 0; s < L; s += STEP) {
    const s1 = Math.min(L, s + STEP), blk = Math.floor(s / 1.1) % 2;
    for (const sd of [-1, 1]) {
      quad(s, s1, sd * (HW - 0.8), sd * (HW - 0.08), blk ? RED : WHITE, 0.07);          // rumble strip, 1.1-long red/white blocks
      quad(s, s1, sd * (HW - 1.06), sd * (HW - 0.92), WHITE, 0.065);                     // solid white edge line
    }
    if (Math.floor(s / 1.65) % 2 === 0) quad(s, s1, -0.08, 0.08, 0xf3e7c6, 0.06);        // dashed centre line
  }
  // chequered start/finish line: 2 rows of 0.55 x 0.53 squares across the ribbon
  const cw = (HW - 1.2) * 2 / 12;
  for (let row = 0; row < 2; row++) for (let c = 0; c < 12; c++) {
    const o0 = -(HW - 1.2) + c * cw;
    quad(-0.55 + row * 0.55, -0.55 + (row + 1) * 0.55, o0, o0 + cw, (c + row) % 2 ? 0x161616 : 0xfafafa, 0.1);
  }
  // checkpoint lines under the hoops
  for (let k = 1; k <= 5; k++) { const sc = P[Math.floor(N * k / 6)].s; quad(sc - 0.16, sc + 0.16, -(HW - 1.0), HW - 1.0, 0x7fe9ff, 0.085); }
  const geo = new THREE.BufferGeometry();
  geo.setAttribute("position", new THREE.Float32BufferAttribute(pos, 3));
  geo.setAttribute("color", new THREE.Float32BufferAttribute(col, 3));
  geo.computeVertexNormals();
  const mat = new THREE.MeshLambertMaterial({ vertexColors: true, polygonOffset: true, polygonOffsetFactor: -3, polygonOffsetUnits: -3 });
  const mesh = new THREE.Mesh(geo, mat); mesh.receiveShadow = true; mesh.renderOrder = 1; scene.add(mesh);
}

function buildTerrain() {
  buildTrackCenter();
  heightData = buildHeightData();
  const geo = new THREE.PlaneGeometry(WORLD, WORLD, heightSeg, heightSeg);
  geo.rotateX(-Math.PI / 2);
  const pos = geo.attributes.position;
  const colors = new Float32Array(pos.count * 3);
  const cSand = new THREE.Color(0xdcbd86);
  const cHard = new THREE.Color(0x8e5f3e);
  const cDune = new THREE.Color(0xe6cf9f);
  const tmp = new THREE.Color();
  for (let i = 0; i < pos.count; i++) {
    const x = pos.getX(i), z = pos.getZ(i);
    const ix = i % (heightSeg + 1);
    const iz = Math.floor(i / (heightSeg + 1));
    const h = heightData[iz * (heightSeg + 1) + ix];
    pos.setY(i, h);
    const sand = surfaceSand(x, z);
    tmp.copy(cHard).lerp(cSand, sand);
    // dune6: hardpack tint only - all painted markings are real geometry now (buildTrackMarkings), never vertex colours
    { const gi = iz * (heightSeg + 1) + ix; if (distGrid[gi] < TRACK_HW - 0.8) tmp.multiplyScalar(0.92); }
    if (sand > 0.7) tmp.lerp(cDune, 0.4);
    // dune3: wind ripples + low-frequency tone variation for realistic sand
    const rip = Math.sin(x * 1.9 + z * 0.7 + Math.sin(z * 0.35) * 2.2) * 0.5 + 0.5;
    const tone = Math.sin(x * 0.13 + 1.1) * Math.cos(z * 0.11) * 0.5 + 0.5;
    tmp.multiplyScalar(0.9 + rip * 0.07 * (0.4 + sand) + tone * 0.06);
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

  buildTrackMarkings();
  // dune5 scenery: boulders on the dune side of the berm, mesas, start arch, checkpoint hoops, jump warning signs
  const rockMat = new THREE.MeshStandardMaterial({ color: 0x6b5a4a, roughness: 0.9, flatShading: true });
  let seed0 = 11; const r0 = () => ((seed0 = (seed0 * 16807) % 2147483647) / 2147483647);
  const dTrack = (x, z) => gridAt(distGrid, x, z);
  for (let k = 0, made = 0; k < 900 && made < 46; k++) {
    const x = (r0() - 0.5) * WORLD * 0.9, z = (r0() - 0.5) * WORLD * 0.9, d = dTrack(x, z);
    if (d < TRACK_HW + 3 || d > TRACK_HW + 16) continue;
    made++; placeRock(x, z, rockMat, 0.5 + r0() * 0.9);
  }
  const mesaMat = [0xb5764a, 0xc58a5a, 0xa9683f].map((c) => new THREE.MeshStandardMaterial({ color: c, roughness: 1, flatShading: true }));
  for (let k = 0, made = 0; k < 900 && made < 12; k++) {
    const x = (r0() - 0.5) * WORLD * 0.96, z = (r0() - 0.5) * WORLD * 0.96, d = dTrack(x, z);
    if (d < 14) continue;
    made++;
    const r = 3.2 + r0() * 3.8, h = 5 + r0() * 7, y = sampleHeight(x, z), mg = new THREE.Group();
    for (let l = 0; l < 3; l++) { const seg = new THREE.Mesh(new THREE.CylinderGeometry(r * (0.8 - l * 0.12), r * (0.95 - l * 0.12), h / 3, 8), mesaMat[(l + made) % 3]); seg.position.y = (l + 0.5) * h / 3 - 0.4; seg.castShadow = seg.receiveShadow = true; mg.add(seg); }
    mg.position.set(x, y, z); mg.rotation.y = r0() * 6; scene.add(mg);
  }
  // start/finish arch with banner
  {
    const p = trackCenter[0], y = sampleHeight(p.x, p.z), g = new THREE.Group();
    g.position.set(p.x, y, p.z); g.rotation.y = p.yaw;
    const gateMat = new THREE.MeshStandardMaterial({ color: 0xf5e6d0, roughness: 0.6 });
    const bcv = document.createElement("canvas"); bcv.width = 2048; bcv.height = 308; const bg = bcv.getContext("2d");
    bg.fillStyle = "#e85d2a"; bg.fillRect(0, 0, 2048, 308); bg.fillStyle = "#fff"; bg.font = "900 190px sans-serif"; bg.textAlign = "center"; bg.textBaseline = "middle"; bg.fillText("RC DUNE BUGGY", 1024, 166);
    for (let i = 0; i < 64; i++) for (let r = 0; r < 2; r++) { bg.fillStyle = (i + r) % 2 ? "#111" : "#fff"; bg.fillRect(i * 32, r * 16, 32, 16); bg.fillRect(i * 32, 276 + r * 16, 32, 16); }
    const bannerTex = new THREE.CanvasTexture(bcv);
    bannerTex.colorSpace = THREE.SRGBColorSpace; bannerTex.anisotropy = renderer.capabilities.getMaxAnisotropy(); bannerTex.generateMipmaps = true; bannerTex.minFilter = THREE.LinearMipmapLinearFilter;
    for (const side of [-1, 1]) { const post = new THREE.Mesh(new THREE.CylinderGeometry(0.22, 0.28, 5, 8), gateMat); post.position.set(side * (TRACK_HW + 0.8), 2.5, 0); post.castShadow = true; g.add(post); }
    const bm = new THREE.MeshStandardMaterial({ map: bannerTex, roughness: 0.7 }), dm = new THREE.MeshStandardMaterial({ color: 0x333333 });
    const banner = new THREE.Mesh(new THREE.BoxGeometry((TRACK_HW + 0.8) * 2, 1.5, 0.12), [dm, dm, dm, dm, bm, bm]); banner.position.set(0, 4.9, 0); banner.castShadow = true; g.add(banner);
    scene.add(g);
  }
  // checkpoint hoops (glowing, drive-through): purely visual landmarks for the lap
  const hoopMat = new THREE.MeshStandardMaterial({ color: 0x59e0ff, emissive: 0x1aa6d8, emissiveIntensity: 0.9, roughness: 0.4 });
  for (let k = 1; k <= 5; k++) {
    const p = trackCenter[Math.floor(trackCenter.length * k / 6)], y = sampleHeight(p.x, p.z), g = new THREE.Group();
    g.position.set(p.x, y, p.z); g.rotation.y = p.yaw;
    const hoop = new THREE.Mesh(new THREE.TorusGeometry(TRACK_HW - 0.2, 0.14, 14, 64, Math.PI), hoopMat); hoop.position.y = 0.02; g.add(hoop);
    for (const sd of [-1, 1]) { const foot = new THREE.Mesh(new THREE.CylinderGeometry(0.3, 0.34, 0.16, 16), hoopMat); foot.position.set(sd * (TRACK_HW - 0.2), 0.06, 0); g.add(foot); }
    scene.add(g);
  }
  // kicker / cliff warning signs (yellow chevron boards just before each launch)
  const signMat = new THREE.MeshStandardMaterial({ color: 0xf5c518, roughness: 0.6 }), postMat = new THREE.MeshStandardMaterial({ color: 0x555555 });
  for (const [wi, o] of [[0, 28], [10, -4], [14, -1]]) {
    const sIdx = Math.floor(((trackSWP[wi] + o + trackLen) % trackLen) / trackLen * trackCenter.length), p = trackCenter[sIdx];
    for (const sd of [-1, 1]) {
      const x = p.x + Math.cos(p.yaw) * sd * (TRACK_HW + 2.2), z = p.z - Math.sin(p.yaw) * sd * (TRACK_HW + 2.2), y = sampleHeight(x, z);
      const pl = new THREE.Mesh(new THREE.BoxGeometry(0.12, 1.5, 0.12), postMat); pl.position.set(x, y + 0.75, z);
      const bd = new THREE.Mesh(new THREE.BoxGeometry(1.1, 0.9, 0.08), signMat); bd.position.set(x, y + 1.6, z); bd.rotation.y = p.yaw; bd.castShadow = true;
      scene.add(pl, bd);
    }
  }

  // dune3: desert sky dome (gradient + sun glow) instead of a flat tan backdrop
  const skyMat = new THREE.ShaderMaterial({ side: THREE.BackSide, depthWrite: false, fog: false,
    vertexShader: "varying vec3 vD; void main(){ vD = normalize(position); gl_Position = projectionMatrix*modelViewMatrix*vec4(position,1.0);}",
    fragmentShader: "varying vec3 vD; void main(){ float h=clamp(vD.y,0.0,1.0); vec3 c=mix(vec3(0.93,0.78,0.6), vec3(0.36,0.6,0.86), pow(h,0.55)); float s=max(dot(vD,normalize(vec3(0.5,0.6,0.35))),0.0); c+=vec3(1.0,0.85,0.6)*pow(s,24.0)*0.8; gl_FragColor=vec4(c,1.0);}" });
  const sky = new THREE.Mesh(new THREE.SphereGeometry(330, 32, 16), skyMat);
  sky.renderOrder = -1; scene.add(sky);
  scene.background = new THREE.Color(0xedc79a);
  scene.fog = new THREE.Fog(0xedc79a, 110, 270);
  // dune3: saguaro cacti + dry shrubs off the racing line (instanced, cheap)
  const cacMat = new THREE.MeshStandardMaterial({ color: 0x5c7a3a, roughness: 0.8 });
  const shrubMat = new THREE.MeshStandardMaterial({ color: 0x8a7a4a, roughness: 1, flatShading: true });
  let seed = 7; const rnd = () => ((seed = (seed * 16807) % 2147483647) / 2147483647);
  const far = (x, z) => gridAt(distGrid, x, z);
  for (let k = 0, made = 0; k < 900 && made < 34; k++) {
    const x = (rnd() - 0.5) * WORLD * 0.95, z = (rnd() - 0.5) * WORLD * 0.95;
    if (far(x, z) < TRACK_HW + 6) continue;
    made++;
    const y = sampleHeight(x, z), hgt = 1.6 + rnd() * 1.6, cg = new THREE.Group();
    const trunk = new THREE.Mesh(new THREE.CapsuleGeometry(0.28, hgt, 4, 8), cacMat); trunk.position.y = hgt / 2 + 0.2; trunk.castShadow = true; cg.add(trunk);
    for (const sd of [-1, 1]) if (rnd() > 0.3) {
      const ah = 0.5 + rnd() * 0.7, arm = new THREE.Mesh(new THREE.CapsuleGeometry(0.18, ah, 4, 8), cacMat);
      arm.position.set(sd * 0.5, hgt * (0.45 + rnd() * 0.25) + ah / 2, 0); arm.castShadow = true;
      const elbow = new THREE.Mesh(new THREE.CapsuleGeometry(0.18, 0.3, 4, 8), cacMat); elbow.rotation.z = Math.PI / 2; elbow.position.set(sd * 0.3, arm.position.y - ah / 2, 0);
      cg.add(arm, elbow);
    }
    cg.position.set(x, y, z); cg.rotation.y = rnd() * 6; cg.scale.setScalar(1.7); scene.add(cg);
  }
  for (let k = 0, made = 0; k < 900 && made < 60; k++) {
    const x = (rnd() - 0.5) * WORLD * 0.95, z = (rnd() - 0.5) * WORLD * 0.95;
    if (far(x, z) < TRACK_HW + 3.5) continue; made++;
    const m = new THREE.Mesh(new THREE.IcosahedronGeometry(0.35 + rnd() * 0.35, 0), shrubMat);
    m.position.set(x, sampleHeight(x, z) + 0.2, z); m.scale.y = 0.6; m.castShadow = true; scene.add(m);
  }
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

  // dune3: detail kit — driver helmet, spoiler wing, headlights, exhaust, bumper, knobby tyres + rims
  const helmet = new THREE.Mesh(new THREE.SphereGeometry(0.2, 14, 10), new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.3, metalness: 0.1 }));
  helmet.position.set(-0.25, 0.98, 0); helmet.castShadow = true; g.add(helmet);
  const visor = new THREE.Mesh(new THREE.SphereGeometry(0.205, 14, 10, -0.9, 1.8, 1.1, 0.7), new THREE.MeshStandardMaterial({ color: 0x111820, roughness: 0.1, metalness: 0.6 }));
  visor.position.copy(helmet.position); g.add(visor);
  const wing = new THREE.Mesh(new THREE.BoxGeometry(0.32, 0.05, 1.3), bodyMat); wing.position.set(-0.95, 1.08, 0); wing.castShadow = true; g.add(wing);
  for (const sz of [-0.4, 0.4]) { const st = new THREE.Mesh(new THREE.BoxGeometry(0.06, 0.42, 0.05), dark); st.position.set(-0.9, 0.85, sz); g.add(st); }
  const lampMat = new THREE.MeshStandardMaterial({ color: 0xfff6d0, emissive: 0xfff0b0, emissiveIntensity: 1.6 });
  for (const sz of [-0.32, 0.32]) { const l = new THREE.Mesh(new THREE.CylinderGeometry(0.09, 0.09, 0.06, 12), lampMat); l.rotation.z = Math.PI / 2; l.position.set(0.93, 0.7, sz); g.add(l); }
  const bump = new THREE.Mesh(new THREE.CylinderGeometry(0.05, 0.05, 1.2, 8), chrome); bump.rotation.x = Math.PI / 2; bump.position.set(1.0, 0.42, 0); g.add(bump);
  const pipe = new THREE.Mesh(new THREE.CylinderGeometry(0.06, 0.07, 0.4, 8), chrome); pipe.rotation.z = Math.PI / 2 - 0.3; pipe.position.set(-1.05, 0.6, 0.3); g.add(pipe);
  const wheelGeo = new THREE.CylinderGeometry(0.4, 0.4, 0.3, 14);
  { const p = wheelGeo.attributes.position; for (let i = 0; i < p.count; i++) { const ang = Math.atan2(p.getZ(i), p.getX(i)), r = Math.hypot(p.getX(i), p.getZ(i)); if (r > 0.39) { const k = (Math.round(ang / (Math.PI / 7)) % 2 ? 1 : 0.92); p.setX(i, p.getX(i) * k); p.setZ(i, p.getZ(i) * k); } } wheelGeo.computeVertexNormals(); }
  const rimGeo = new THREE.CylinderGeometry(0.2, 0.2, 0.32, 10);
  const wheels = [];
  [[0.65, 0.55], [0.65, -0.55], [-0.7, 0.55], [-0.7, -0.55]].forEach(([x, z], i) => {
    const w = new THREE.Mesh(wheelGeo, dark);
    w.rotation.x = Math.PI / 2; // axle across the car
    w.position.set(x, 0.38, z);
    w.castShadow = true;
    w.add(new THREE.Mesh(rimGeo, chrome));
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
  const p0 = trackCenter[0], p1 = trackCenter[2];
  const yaw = Math.atan2(p1.x - p0.x, p1.z - p0.z);
  const side = (i % 2 === 0 ? -1 : 1) * 1.5;
  const back = -3.5 - (i >> 1) * 3.4 - (i % 2) * 1.6;
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
  const N = 700;
  const geo = new THREE.BufferGeometry();
  const pos = new Float32Array(N * 3);
  const life = new Float32Array(N);
  for (let i = 0; i < N; i++) { pos[i * 3 + 1] = -99; life[i] = 0; }
  geo.setAttribute("position", new THREE.BufferAttribute(pos, 3));
  const mat = new THREE.PointsMaterial({
    color: 0xd9c09a, size: 1.4, transparent: true, opacity: 0.5, map: (() => { const cv = document.createElement("canvas"); cv.width = cv.height = 32; const g = cv.getContext("2d"), r = g.createRadialGradient(16, 16, 0, 16, 16, 16); r.addColorStop(0, "#fff"); r.addColorStop(1, "rgba(255,255,255,0)"); g.fillStyle = r; g.fillRect(0, 0, 32, 32); return new THREE.CanvasTexture(cv); })(),
    depthWrite: false, sizeAttenuation: true,
  });
  const pts = new THREE.Points(geo, mat);
  pts.frustumCulled = false;
  scene.add(pts);
  return { pts, pos, life, N, cursor: 0 };
}

function emitDust(x, y, z, n, speed) {
  if (!dust) return;
  const count = Math.min(n, 10);
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

const DEMO = new URLSearchParams(location.search).get("demo") === "race"; // all-AI attract mode (arcade live preview)
function assignSeats() {
  if (DEMO) { for (const c of cars) { c.human = false; c.padSlot = -1; } return; }
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

function aiInput(c, dt = 1 / 60) {
  const N = trackCenter.length;
  const look = (c.aiTarget || 0) % N;
  const sp = Math.hypot(c.vx, c.vz);
  const lead = 4 + Math.round(sp / 7);
  const ahead = trackCenter[(look + lead) % N];
  // lane offset per car so the pack spreads out; a little extra when a car is right in front
  const off = (c.i - 1.5) * 0.9;
  const tx = ahead.x + Math.cos(ahead.yaw) * off - c.x, tz = ahead.z - Math.sin(ahead.yaw) * off - c.z;
  const want = Math.atan2(tx, tz);
  let diff = want - c.yaw;
  while (diff > Math.PI) diff -= Math.PI * 2;
  while (diff < -Math.PI) diff += Math.PI * 2;
  const steer = Math.max(-1, Math.min(1, diff * 2.0));
  // brake for the sharpest bend in the next ~25 units
  let curv = 0; for (let k = 3; k < 18; k += 3) curv = Math.max(curv, Math.abs(trackCenter[(look + k) % N].curv));
  const vmax = Math.max(9, 24 - curv * 150) * (0.93 + c.i * 0.025);
  let throttle = sp > vmax ? (sp > vmax + 4 ? -0.35 : 0) : (Math.abs(diff) > 1.0 ? 0.5 : 1);
  // unstick: back out when pinned
  c._stuck = sp < 1.2 && c.grounded ? (c._stuck || 0) + dt : Math.max(0, (c._stuck || 0) - dt);
  if (c._stuck > 1.4) { c._rev = 0.9; c._stuck = 0; }
  if (c._rev > 0) { c._rev -= dt; return { steer: -Math.sign(diff || 1), throttle: -0.9 }; }
  const near = trackCenter[look];
  if ((near.x - c.x) ** 2 + (near.z - c.z) ** 2 < 49) c.aiTarget = (look + 1) % N;
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
    inp = c.human ? readHumanInput(c.i) : aiInput(c, dt);
  }
  c._thr = inp.throttle || 0;
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
  const prog = nearestProgress(c.x, c.z, c.lastProg);
  let delta = prog - c.lastProg;
  // dune5 lap logic: furthest point reached this lap (no skipping ahead more than ~12% at once); a lap counts only past 88%
  if (c.maxP == null) c.maxP = 0;
  if (delta > -0.5 && prog > c.maxP && prog - c.maxP < 0.12) c.maxP = prog;
  if (delta < -0.5 && c.maxP < 0.88) delta = 0; // crossed the line without driving the lap: ignore
  else if (delta < -0.5) { // crossed finish forward
    c.maxP = 0;
    c.lap += 1;
    if (c.lap >= LAPS && !c.finished) {
      c.finished = true;
      c.finishTime = raceTime;
      showBanner(`${c.name} finished!`);
      if (isLocalCar(c)) {
        const place = cars.filter((o) => o.finished).length;
        sfx(place === 1 ? "win" : place === 2 ? "lap" : "lose");
      }
    } else if (isLocalCar(c)) sfx("lap");
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
      if (isLocalCar(c)) sfx("land", Math.min(1.3, -c.vy / 12));
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
        if (isLocalCar(c)) sfx("jump");
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
        if (impact < -3 && (isLocalCar(a) || isLocalCar(b))) sfx("bump", Math.min(1.3, -impact / 14));
        if (impact < 0) {
          a.vx += nx * impact * 0.55; a.vz += nz * impact * 0.55;
          b.vx -= nx * impact * 0.55; b.vz -= nz * impact * 0.55;
        }
      }
    }
  }
}

function updateCamera() {
  // spectator framing: follow the pack's centre, pull back as it spreads out
  let minX = 1e9, maxX = -1e9, minZ = 1e9, maxZ = -1e9, fx = 0, fz = 0;
  for (const c of cars) {
    minX = Math.min(minX, c.x); maxX = Math.max(maxX, c.x);
    minZ = Math.min(minZ, c.z); maxZ = Math.max(maxZ, c.z);
    fx += c.x; fz += c.z;
  }
  fx /= cars.length; fz /= cars.length;
  const span = Math.max(maxX - minX, (maxZ - minZ) * 1.5, 16);
  const height = Math.min(118, 34 + span * 0.8);
  const desired = new THREE.Vector3(fx * 0.9, height, fz * 0.9 + height * 0.55);
  camera.position.lerp(desired, 0.06);
  camera.lookAt(fx * 0.9, 0.5, fz * 0.9);
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
    c.lap = 0; c.progress = 0; c.lastProg = 0; c.maxP = 0; c._rev = 0; c._stuck = 0;
    c.finished = false; c.finishTime = 0;
    c.aiTarget = 3;
    c.grounded = true;
    syncMesh(c);
  }
  showBanner("3 · 2 · 1 · GO!");
  if (AUD) { sfx("countdown"); setTimeout(() => sfx("countdown"), 450); setTimeout(() => sfx("countdown"), 900); setTimeout(() => sfx("go"), 1350); }
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

  if (AUD) {
    const me = cars.find(isLocalCar);
    if (me) AUD.engine(Math.abs(me._thr || 0), Math.hypot(me.vx, me.vz) / MAX_SPEED, "buggy");
    else AUD.engineStop();
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
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.2;
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
  camera = new THREE.PerspectiveCamera(42, innerWidth / innerHeight, 0.5, 520);
  camera.position.set(0, 80, 40);
  clock = new THREE.Clock();

  const hemi = new THREE.HemisphereLight(0xffe9c8, 0x7d5a38, 1.0);
  scene.add(hemi);
  const sun = new THREE.DirectionalLight(0xfff0d8, 1.15);
  sun.position.set(48, 90, 32);
  sun.castShadow = true;
  sun.shadow.mapSize.set(innerWidth < 700 ? 1024 : 2048, innerWidth < 700 ? 1024 : 2048);
  sun.shadow.bias = -0.0004; sun.shadow.normalBias = 0.03;
  sun.shadow.camera.left = -62; sun.shadow.camera.right = 62; sun.shadow.camera.top = 62; sun.shadow.camera.bottom = -62;
  sun.shadow.camera.near = 5; sun.shadow.camera.far = 220;
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

  window.__dune = { get cars() { return cars; }, get len() { return trackLen; }, get pts() { return trackCenter; } }; // test hook
  boot.classList.add("hide");
  showBanner("Dune heat · 3 laps");
  renderer.setAnimationLoop(frame);
}

init();
