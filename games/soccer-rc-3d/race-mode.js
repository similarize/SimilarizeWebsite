/**
 * Race Track v2 — "Similarize RC Raceway".
 * A ~0.5 km RC circuit that fills the whole yard: long start/finish straight,
 * fast sweeper, whoops (rhythm bumps), top straight with a plywood kicker jump,
 * chicane, infield esses, hairpin, and a climb onto a bridge that crosses over
 * the left back-straight before dropping down to the grid.
 *
 * Road is a custom flat-framed ribbon (no ExtrudeGeometry Frenet twist), sampled
 * every 0.5 m into a table that physics, AI, camera and props all share.
 * Height/rail lookups track each car's own track index so the bridge and the
 * road beneath it never get confused.
 */
import * as THREE from "three";

export const RACE_LAPS_DEFAULT = 3;
export const RACE_COLORS = [0xe4572e, 0x3b82f6, 0x34d399, 0xc084fc];
export const RACE_NAMES = ["Orange", "Blue", "Green", "Purple"];

const HW = 4.0;                    // painted road half-width (edge lines)
const BARRIER_OFF = HW + 0.85;     // inner face of barriers / runoff edge
const CAR_SIDE = 0.55;
const RAIL_LIMIT = BARRIER_OFF - CAR_SIDE; // car-centre lateral limit
const ROAD_LIFT = 0.05;            // at-grade road surface above grass
const STEP = 0.5;                  // sample spacing (m)
const DECK_T = 0.6;                // bridge deck thickness
const BRIDGE_Y = 2.2;              // above this the road is a bridge (open underneath)

/** Control points [x, z, y] in driving order (closed). +x right, +z toward camera. */
const TRACK_PTS = [
  // Start / finish straight (bottom), heading +x
  [-30, 38, 0], [0, 38, 0], [30, 38, 0],
  // T1 fast sweeper
  [50, 34, 0], [62, 20, 0],
  // Right side (whoops)
  [65, 0, 0], [64, -18, 0],
  // T2 into top straight
  [58, -34, 0], [44, -41, 0],
  // Top straight (kicker jump) heading -x
  [20, -42, 0], [-2, -42, 0],
  // Chicane
  [-11, -42, 0], [-24, -36.5, 0], [-37, -42, 0],
  // T3 top-left
  [-50, -40, 0], [-58, -28, 0],
  // Left back-straight (runs UNDER the bridge)
  [-58, -12, 0], [-56, 6, 0],
  // T4 hairpin into infield
  [-50, 18, 0], [-38, 22, 0], [-28, 16, 0],
  // Infield esses
  [-14, 20, 0], [0, 14, 0], [14, 22, 0], [30, 18, 0],
  // Infield sweeper + hairpin
  [40, 6, 0], [38, -10, 0], [30, -22, 0], [18, -26, 0], [8, -18, 0],
  // Climb to the bridge heading -x
  [-6, -10, 0.2], [-22, -6, 1.8], [-38, -4, 4.0], [-50, -4, 5.0],
  [-58, -4, 5.2], [-66, -4, 5.0],
  // Bridge exit, descend outside the left side
  [-74, 4, 3.6], [-76, 18, 1.8], [-72, 32, 0.3],
  // Final corner onto the grid
  [-60, 40, 0], [-45, 40, 0],
];

// Feature anchors (nearest sample to these XZ points)
const START_XZ = [-2, 38];          // start/finish line
const JUMP_XZ = [14, -42];          // kicker begins here (heading -x)
const JUMP_LEN = 6.5, JUMP_H = 1.35;
const WHOOPS_XZ = [65, 6];          // whoops begin
const WHOOPS_N = 4, WHOOPS_P = 5.5, WHOOPS_A = 0.32;

const SX = 0.9; // horizontal squeeze → ~0.6 km lap (AI ≈ 40 s)
function wrapI(i, n) { return ((i % n) + n) % n; }

/* ------------------------------------------------------------------ */
/* Sample table                                                        */
/* ------------------------------------------------------------------ */
function buildSamples() {
  const pts = TRACK_PTS.map(([x, z, y]) => new THREE.Vector3(x * SX, y, z));
  const curve = new THREE.CatmullRomCurve3(pts, true, "centripetal");
  curve.arcLengthDivisions = 4000;
  // Dense sample, then Gaussian-smooth XZ so control-point kinks never make a
  // corner tighter than the road is wide (prevents inner-edge fold/self-overlap).
  const L0 = curve.getLength();
  const M0 = Math.round(L0 / 0.25);
  let ax = new Float32Array(M0), az = new Float32Array(M0), ay = new Float32Array(M0);
  const p = new THREE.Vector3(), t = new THREE.Vector3();
  for (let i = 0; i < M0; i++) {
    curve.getPointAt(i / M0, p);
    ax[i] = p.x; az[i] = p.z; ay[i] = p.y;
  }
  const sigma = 3.2 / 0.25, R = Math.ceil(sigma * 2.5);
  const wts = [];
  let wsum = 0;
  for (let j = -R; j <= R; j++) { const w = Math.exp(-(j * j) / (2 * sigma * sigma)); wts.push(w); wsum += w; }
  const bx = new Float32Array(M0), bz = new Float32Array(M0), by = new Float32Array(M0);
  for (let i = 0; i < M0; i++) {
    let sx = 0, sz = 0, sy = 0;
    for (let j = -R; j <= R; j++) {
      const q = wrapI(i + j, M0), w = wts[j + R];
      sx += ax[q] * w; sz += az[q] * w; sy += ay[q] * w;
    }
    bx[i] = sx / wsum; bz[i] = sz / wsum; by[i] = sy / wsum;
  }
  // arc-length resample
  const cum = new Float32Array(M0 + 1);
  for (let i = 0; i < M0; i++) {
    const q = wrapI(i + 1, M0);
    cum[i + 1] = cum[i] + Math.hypot(bx[q] - bx[i], bz[q] - bz[i], by[q] - by[i]);
  }
  const L = cum[M0];
  const N = Math.round(L / STEP);
  const px = new Float32Array(N), py = new Float32Array(N), pz = new Float32Array(N);
  const tx = new Float32Array(N), tz = new Float32Array(N);
  const nx = new Float32Array(N), nz = new Float32Array(N);
  const base = new Float32Array(N), k = new Float32Array(N), slope = new Float32Array(N);
  const ramp = new Float32Array(N);
  let seg = 0;
  for (let i = 0; i < N; i++) {
    const s = (i / N) * L;
    while (cum[seg + 1] < s) seg++;
    const f = (s - cum[seg]) / Math.max(1e-6, cum[seg + 1] - cum[seg]);
    const q = wrapI(seg + 1, M0);
    px[i] = bx[seg] + (bx[q] - bx[seg]) * f;
    pz[i] = bz[seg] + (bz[q] - bz[seg]) * f;
    base[i] = Math.max(0, by[seg] + (by[q] - by[seg]) * f);
  }
  for (let i = 0; i < N; i++) {
    const a = wrapI(i - 1, N), b = wrapI(i + 1, N);
    const dx = px[b] - px[a], dz = pz[b] - pz[a];
    const l = Math.hypot(dx, dz) || 1;
    tx[i] = dx / l; tz[i] = dz / l;
    nx[i] = -tz[i]; nz[i] = tx[i];
  }
  const ds = L / N;
  // Smooth base height (Catmull overshoot) with a short box filter
  const b2 = new Float32Array(N);
  for (let i = 0; i < N; i++) {
    let s = 0;
    for (let j = -6; j <= 6; j++) s += base[wrapI(i + j, N)];
    b2[i] = s / 13;
  }
  for (let i = 0; i < N; i++) base[i] = b2[i] < 0.03 ? 0 : b2[i];

  const S = { N, L, ds, px, py, pz, tx, tz, nx, nz, base, k, slope, ramp, curve };
  const nearestIdx = (x, z) => {
    let best = 0, bd = Infinity;
    for (let i = 0; i < N; i++) {
      const d = (px[i] - x) ** 2 + (pz[i] - z) ** 2;
      if (d < bd) { bd = d; best = i; }
    }
    return best;
  };
  S.nearestIdx = nearestIdx;

  // Whoops (rhythm section)
  const w0 = nearestIdx(WHOOPS_XZ[0] * SX, WHOOPS_XZ[1]);
  const wLen = WHOOPS_N * WHOOPS_P;
  for (let j = 0; j * ds <= wLen; j++) {
    const s = j * ds;
    ramp[wrapI(w0 + j, N)] += WHOOPS_A * (0.5 - 0.5 * Math.cos((2 * Math.PI * s) / WHOOPS_P));
  }
  S.whoops = { i0: w0, n: Math.round(wLen / ds) };

  // Kicker jump (plywood wedge, vertical back face)
  const j0 = nearestIdx(JUMP_XZ[0] * SX, JUMP_XZ[1]);
  const jn = Math.round(JUMP_LEN / ds);
  for (let j = 0; j <= jn; j++) {
    const f = j / jn;
    // slight curve-up "kicker" profile
    ramp[wrapI(j0 + j, N)] += JUMP_H * (0.75 * f + 0.25 * f * f);
  }
  S.jump = { i0: j0, n: jn };

  for (let i = 0; i < N; i++) py[i] = base[i] + ramp[i] + ROAD_LIFT;
  for (let i = 0; i < N; i++) {
    const a = wrapI(i - 1, N), b = wrapI(i + 1, N);
    const cross = tx[a] * tz[b] - tz[a] * tx[b];
    const dot = tx[a] * tx[b] + tz[a] * tz[b];
    k[i] = Math.atan2(cross, dot) / (2 * ds); // signed curvature (+ = turning right)
    // backward difference: the kicker lip keeps its ramp slope (launch vy), the
    // vertical drop after it never pulls a grounded car down
    slope[i] = (py[i] - py[a]) / ds;
  }
  // Smoothed curvature for décor decisions
  const ks = new Float32Array(N);
  for (let i = 0; i < N; i++) {
    let s = 0;
    for (let j = -8; j <= 8; j++) s += k[wrapI(i + j, N)];
    ks[i] = s / 17;
  }
  S.ks = ks;
  S.startIdx = nearestIdx(START_XZ[0] * SX, START_XZ[1]);
  return S;
}

/* ------------------------------------------------------------------ */
/* Canvas textures                                                     */
/* ------------------------------------------------------------------ */
function canvasTex(w, h, draw, opts = {}) {
  const c = document.createElement("canvas");
  c.width = w; c.height = h;
  const g = c.getContext("2d");
  draw(g, w, h);
  const tex = new THREE.CanvasTexture(c);
  tex.colorSpace = THREE.SRGBColorSpace;
  tex.wrapS = tex.wrapT = THREE.RepeatWrapping;
  if (opts.nearest) { tex.magFilter = THREE.NearestFilter; tex.minFilter = THREE.NearestFilter; tex.generateMipmaps = false; }
  else tex.anisotropy = 4;
  if (opts.repeat) tex.repeat.set(opts.repeat[0], opts.repeat[1]);
  return tex;
}
function rng(seed) { let s = seed >>> 0; return () => ((s = (s * 1664525 + 1013904223) >>> 0) / 4294967296); }
function noiseFill(g, w, h, base, spread, n, seed) {
  g.fillStyle = base; g.fillRect(0, 0, w, h);
  const r = rng(seed);
  for (let i = 0; i < n; i++) {
    const v = (r() - 0.5) * spread;
    g.fillStyle = v > 0 ? `rgba(255,255,255,${v})` : `rgba(0,0,0,${-v})`;
    g.fillRect(r() * w, r() * h, 1 + r() * 2, 1 + r() * 2);
  }
}

function makeMaterials() {
  const asphaltTex = canvasTex(128, 128, (g, w, h) => noiseFill(g, w, h, "#5d6168", 0.22, 2600, 7));
  const dirtTex = canvasTex(128, 128, (g, w, h) => noiseFill(g, w, h, "#8a6a48", 0.25, 2600, 11));
  const grassTex = canvasTex(256, 256, (g, w, h) => {
    noiseFill(g, w, h, "#4f8a3c", 0.12, 5000, 3);
    // mowing stripes
    g.fillStyle = "rgba(255,255,255,0.05)";
    g.fillRect(0, 0, w / 2, h);
  });
  const curbTex = canvasTex(4, 2, (g) => {
    g.fillStyle = "#d8352a"; g.fillRect(0, 0, 4, 1);
    g.fillStyle = "#f4f1ea"; g.fillRect(0, 1, 4, 1);
  }, { nearest: true });
  const boardTex = canvasTex(4, 2, (g) => {
    g.fillStyle = "#2f6fd6"; g.fillRect(0, 0, 4, 1);
    g.fillStyle = "#f2f4f7"; g.fillRect(0, 1, 4, 1);
  }, { nearest: true });
  const checkerTex = canvasTex(8, 2, (g) => {
    for (let x = 0; x < 8; x++) for (let y = 0; y < 2; y++) {
      g.fillStyle = (x + y) % 2 ? "#111" : "#f5f5f5"; g.fillRect(x, y, 1, 1);
    }
  }, { nearest: true });
  const concreteTex = canvasTex(64, 64, (g, w, h) => noiseFill(g, w, h, "#a7a49c", 0.16, 700, 5));
  const plyTex = canvasTex(64, 64, (g, w, h) => {
    noiseFill(g, w, h, "#c9955a", 0.12, 600, 9);
    g.fillStyle = "rgba(80,45,15,.35)";
    for (let y = 0; y < h; y += 16) g.fillRect(0, y, w, 1);
  });
  const tireCapTex = canvasTex(64, 64, (g, w, h) => {
    g.fillStyle = "#000"; g.fillRect(0, 0, w, h);
    g.fillStyle = "#3a3a3a"; g.beginPath(); g.arc(32, 32, 31, 0, Math.PI * 2); g.fill();
    g.fillStyle = "#151515"; g.beginPath(); g.arc(32, 32, 18, 0, Math.PI * 2); g.fill();
    g.fillStyle = "#050505"; g.beginPath(); g.arc(32, 32, 15, 0, Math.PI * 2); g.fill();
  });
  const lit = (o) => lam(o);
  const overlay = (o) => lit(Object.assign({ polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -2 }, o));
  return {
    asphaltTex, dirtTex, grassTex,
    asphalt: lit({ map: asphaltTex, roughness: 0.92 }),
    dirt: lit({ map: dirtTex, roughness: 1 }),
    grass: lit({ map: grassTex, roughness: 1, polygonOffset: true, polygonOffsetFactor: 2, polygonOffsetUnits: 4 }),
    curb: overlay({ map: curbTex, roughness: 0.7 }),
    line: overlay({ color: 0xf2f2ee, roughness: 0.6 }),
    checker: overlay({ map: checkerTex, roughness: 0.7, polygonOffsetFactor: -3, polygonOffsetUnits: -3 }),
    board: lit({ map: boardTex, roughness: 0.6 }),
    concrete: lit({ map: concreteTex, color: 0xd8d5cc }),
    concreteDark: lit({ map: concreteTex, color: 0x8a877f }),
    ply: lit({ map: plyTex, roughness: 0.8 }),
    tireSide: lit({ color: 0xffffff, roughness: 0.95 }),
    tireCap: lit({ map: tireCapTex, roughness: 0.95 }),
    steel: lit({ color: 0x9aa3ad, roughness: 0.45, metalness: 0.6 }),
    darkSteel: lit({ color: 0x3b4048, roughness: 0.6, metalness: 0.4 }),
    wood: lit({ color: 0x8b6a45, roughness: 0.9 }),
    white: lit({ color: 0xf1f1ec, roughness: 0.7 }),
    hay: lit({ color: 0xd9b45a, roughness: 1 }),
    cone: lit({ color: 0xff6a1a, roughness: 0.6 }),
    leaf: lit({ color: 0x2f6b34, roughness: 1, flatShading: true }),
    trunk: lit({ color: 0x5a4030, roughness: 1 }),
  };
}

/* ------------------------------------------------------------------ */
/* Sweep builder: extrude 2D profile segments along sample runs        */
/* Profile point: [offset (along +n = right), dy (above road y)] or     */
/* [offset, null, absY]. Segments traversed so CCW-rotated dir = normal */
/* ------------------------------------------------------------------ */
function sweep(S, runs, segs, vScale = 4, uScale = 1) {
  const pos = [], uv = [], idx = [];
  for (const [i0, cnt] of runs) {
    for (const seg of segs) {
      const start = pos.length / 3;
      let s = 0;
      for (let j = 0; j <= cnt; j++) {
        const i = wrapI(i0 + j, S.N);
        for (let q = 0; q < 2; q++) {
          const pt = seg[q];
          const o = pt[0];
          const y = pt.length > 2 ? pt[2] : S.py[i] + pt[1];
          pos.push(S.px[i] + S.nx[i] * o, y, S.pz[i] + S.nz[i] * o);
          uv.push(q * uScale, s / vScale);
        }
        s += S.ds;
        if (j < cnt) {
          const a = start + j * 2;
          // (a0, b0, a1) (b0, b1, a1)
          idx.push(a, a + 1, a + 2, a + 1, a + 3, a + 2);
        }
      }
    }
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute("position", new THREE.Float32BufferAttribute(pos, 3));
  g.setAttribute("uv", new THREE.Float32BufferAttribute(uv, 2));
  g.setIndex(idx);
  g.computeVertexNormals();
  return g;
}

/** Contiguous runs [i0, count] where pred(i) holds (wrap-aware), min length. */
function runsWhere(S, pred, minLen = 1) {
  const N = S.N, on = new Uint8Array(N);
  for (let i = 0; i < N; i++) on[i] = pred(i) ? 1 : 0;
  let all = true;
  for (let i = 0; i < N; i++) if (!on[i]) { all = false; break; }
  if (all) return [[0, N]];
  let first = 0;
  while (on[first]) first++; // first off
  const runs = [];
  let i = first + 1, cnt = 0, st = -1;
  for (let c = 0; c < N; c++, i++) {
    const ii = wrapI(i, N);
    if (on[ii]) { if (st < 0) { st = ii; cnt = 0; } cnt++; }
    else if (st >= 0) { if (cnt >= minLen) runs.push([st, cnt]); st = -1; }
  }
  if (st >= 0 && cnt >= minLen) runs.push([st, cnt]);
  return runs;
}

/** Lambert everywhere on the track: far cheaper per pixel than PBR on phones/Tesla. */
function lam(o) {
  const c = Object.assign({}, o);
  delete c.roughness; delete c.metalness;
  return new THREE.MeshLambertMaterial(c);
}

/**
 * Merge plain static meshes that share material + shadow flags + attribute
 * layout into one draw call each (props were ~300 calls → a few dozen).
 */
function mergeStatic(group) {
  const buckets = new Map();
  for (const ch of group.children.slice()) {
    if (!ch.isMesh || ch.isInstancedMesh || Array.isArray(ch.material) || ch.name === "raceSky") continue;
    const g = ch.geometry;
    const attrs = Object.keys(g.attributes).sort().join(",");
    if (attrs !== "normal,position,uv" && attrs !== "normal,position") continue;
    const key = ch.material.uuid + "|" + ch.castShadow + "|" + ch.receiveShadow + "|" + attrs + "|" + ch.renderOrder;
    if (!buckets.has(key)) buckets.set(key, []);
    buckets.get(key).push(ch);
  }
  for (const list of buckets.values()) {
    if (list.length < 2) continue;
    let nv = 0, ni = 0;
    for (const m of list) {
      nv += m.geometry.attributes.position.count;
      ni += m.geometry.index ? m.geometry.index.count : m.geometry.attributes.position.count;
    }
    const hasUv = !!list[0].geometry.attributes.uv;
    const pos = new Float32Array(nv * 3), nor = new Float32Array(nv * 3), uv = hasUv ? new Float32Array(nv * 2) : null;
    const idx = new Uint32Array(ni);
    let vo = 0, io = 0;
    const nm = new THREE.Matrix3(), v = new THREE.Vector3();
    for (const m of list) {
      m.updateMatrix();
      nm.getNormalMatrix(m.matrix);
      const g = m.geometry, P = g.attributes.position, Nn = g.attributes.normal;
      for (let i = 0; i < P.count; i++) {
        v.fromBufferAttribute(P, i).applyMatrix4(m.matrix);
        pos[(vo + i) * 3] = v.x; pos[(vo + i) * 3 + 1] = v.y; pos[(vo + i) * 3 + 2] = v.z;
        v.fromBufferAttribute(Nn, i).applyMatrix3(nm).normalize();
        nor[(vo + i) * 3] = v.x; nor[(vo + i) * 3 + 1] = v.y; nor[(vo + i) * 3 + 2] = v.z;
        if (uv) { uv[(vo + i) * 2] = g.attributes.uv.getX(i); uv[(vo + i) * 2 + 1] = g.attributes.uv.getY(i); }
      }
      if (g.index) for (let i = 0; i < g.index.count; i++) idx[io++] = g.index.getX(i) + vo;
      else for (let i = 0; i < P.count; i++) idx[io++] = i + vo;
      vo += P.count;
      group.remove(m);
    }
    const mg = new THREE.BufferGeometry();
    mg.setAttribute("position", new THREE.BufferAttribute(pos, 3));
    mg.setAttribute("normal", new THREE.BufferAttribute(nor, 3));
    if (uv) mg.setAttribute("uv", new THREE.BufferAttribute(uv, 2));
    mg.setIndex(new THREE.BufferAttribute(idx, 1));
    mg.computeBoundingSphere();
    const out = new THREE.Mesh(mg, list[0].material);
    out.castShadow = list[0].castShadow; out.receiveShadow = list[0].receiveShadow;
    out.renderOrder = list[0].renderOrder;
    group.add(out);
  }
}

function mesh(geo, mat, cast = false, recv = true) {
  const m = new THREE.Mesh(geo, mat);
  m.castShadow = cast; m.receiveShadow = recv;
  return m;
}

/* ------------------------------------------------------------------ */
/* Track build                                                         */
/* ------------------------------------------------------------------ */
let _cached = null;

export function buildRaceTrack(scene) {
  if (_cached) {
    if (!_cached.group.parent) scene.add(_cached.group);
    _cached.group.visible = true;
    return _cached;
  }
  const S = buildSamples();
  const M = makeMaterials();
  const group = new THREE.Group();
  group.name = "raceTrack";
  const N = S.N;
  const elev = (i) => S.base[i] > 0.08;
  const isBridge = (i) => S.base[i] > BRIDGE_Y;
  // Lower road under a bridge? (needed so embankments never block it)
  // ---------- Ground ----------
  const gw = 640, gd = 640; // runs past the fog so no edge is ever visible
  const ground = mesh(new THREE.PlaneGeometry(gw, gd), M.grass, false, true);
  ground.rotation.x = -Math.PI / 2;
  ground.position.set(-5, 0, 2);
  M.grassTex.repeat.set(gw / 8, gd / 8);
  group.add(ground);

  // ---------- Road surface (asphalt + dirt whoops) ----------
  const W = BARRIER_OFF;
  const roadSeg = [[[-W, 0], [W, 0]]];
  const inWhoops = (i) => wrapI(i - S.whoops.i0 + 6, N) < S.whoops.n + 12;
  const asphaltRuns = runsWhere(S, (i) => !inWhoops(i));
  const dirtRuns = runsWhere(S, inWhoops);
  group.add(mesh(sweep(S, asphaltRuns, roadSeg, 6, 2), M.asphalt));
  group.add(mesh(sweep(S, dirtRuns, roadSeg, 6, 2), M.dirt));
  M.asphaltTex.repeat.set(1, 1);

  // ---------- Embankments / bridge deck sides ----------
  const embRuns = runsWhere(S, (i) => elev(i) && !isBridge(i));
  const W2 = W + 0.42;
  group.add(mesh(sweep(S, embRuns, [
    [[W2, 0.02], [W2, null, 0]],          // right face, down to ground
    [[-W2, null, 0], [-W2, 0.02]],        // left face
  ], 3), M.concrete, true, true));
  // Abutment end-walls where an embankment meets an open bridge span
  {
    const capMat = lam({ map: M.concrete.map, color: 0xc9c6bd, side: THREE.DoubleSide });
    const capPos = [];
    for (const [i0, cnt] of embRuns) {
      for (const [ii, nb] of [[i0, wrapI(i0 - 1, N)], [wrapI(i0 + cnt, N), wrapI(i0 + cnt + 1, N)]]) {
        if (!isBridge(nb)) continue;
        const y = S.py[ii] + 0.02;
        const ax = S.px[ii] - S.nx[ii] * W2, az = S.pz[ii] - S.nz[ii] * W2;
        const bx = S.px[ii] + S.nx[ii] * W2, bz = S.pz[ii] + S.nz[ii] * W2;
        capPos.push(ax, 0, az, bx, 0, bz, bx, y, bz, ax, 0, az, bx, y, bz, ax, y, az);
      }
    }
    if (capPos.length) {
      const cg = new THREE.BufferGeometry();
      cg.setAttribute("position", new THREE.Float32BufferAttribute(capPos, 3));
      cg.computeVertexNormals();
      group.add(mesh(cg, capMat, true, true));
    }
  }
  const deckRuns = runsWhere(S, isBridge);
  group.add(mesh(sweep(S, deckRuns, [
    [[W2, 0.02], [W2, -DECK_T]],
    [[-W2, -DECK_T], [-W2, 0.02]],
    [[W2, -DECK_T], [-W2, -DECK_T]],      // underside
  ], 3), M.concreteDark, true, true));
  // road surface extends under the parapets on elevated parts
  group.add(mesh(sweep(S, runsWhere(S, elev), [[[W, 0], [W2, 0]], [[-W2, 0], [-W, 0]]], 3), M.concrete, false, true));

  // ---------- Curbs (corners) + edge lines (elsewhere) ----------
  const curbR = (i) => Math.abs(S.ks[i]) > 1 / 26;
  const curbRuns = runsWhere(S, (i) => curbR(i) && !inWhoops(i), 8);
  const lineRuns = runsWhere(S, (i) => !(curbR(i) && !inWhoops(i)), 1);
  const CY = 0.012;
  group.add(mesh(sweep(S, curbRuns, [[[HW - 0.45, CY], [HW + 0.45, CY]], [[-HW - 0.45, CY], [-HW + 0.45, CY]]], 2.4, 1), M.curb));
  group.add(mesh(sweep(S, lineRuns, [[[HW - 0.28, 0.008], [HW - 0.08, 0.008]], [[-HW + 0.08, 0.008], [-HW + 0.28, 0.008]]], 4), M.line));
  // Start/finish checker band
  const sfRun = [[wrapI(S.startIdx - 2, N), 4]];
  group.add(mesh(sweep(S, sfRun, [[[-HW, 0.016], [HW, 0.016]]], 2, 8), M.checker));

  // ---------- Barriers ----------
  // side: +1 right, -1 left. Outer side of a bend = opposite of turn direction.
  const outerTire = (i, side) => Math.abs(S.ks[i]) > 1 / 34 && Math.sign(S.ks[i]) === -side;
  const boardH = 0.5, boardT = 0.22;
  for (const side of [1, -1]) {
    const o0 = side * W, o1 = side * (W + boardT);
    const prof = side > 0
      ? [[[o0, 0], [o0, boardH]], [[o0, boardH], [o1, boardH]], [[o1, boardH], [o1, 0]]]
      : [[[o1, 0], [o1, boardH]], [[o1, boardH], [o0, boardH]], [[o0, boardH], [o0, 0]]];
    // boards on at-grade, non-tire stretches
    const bRuns = runsWhere(S, (i) => !elev(i) && !outerTire(i, side), 2);
    group.add(mesh(sweep(S, bRuns, prof, 2.0, 1), M.board, true, true));
    // concrete parapet on elevated stretches
    const pH = 0.85, pT = 0.4;
    const q0 = side * W, q1 = side * (W + pT);
    const pprof = side > 0
      ? [[[q0, 0], [q0, pH]], [[q0, pH], [q1, pH]], [[q1, pH], [q1, -0.02]]]
      : [[[q1, -0.02], [q1, pH]], [[q1, pH], [q0, pH]], [[q0, pH], [q0, 0]]];
    group.add(mesh(sweep(S, runsWhere(S, elev, 2), pprof, 3), M.concrete, true, true));
  }
  // Tire walls (instanced): outer side of real corners, at grade
  const tirePos = [];
  for (const side of [1, -1]) {
    for (const [i0, cnt] of runsWhere(S, (i) => !elev(i) && outerTire(i, side), 3)) {
      let acc = 0;
      for (let j = 0; j <= cnt; j++) {
        acc += S.ds;
        if (acc < 0.9 && j > 0) continue;
        acc = 0;
        const i = wrapI(i0 + j, N);
        const o = side * (W + 0.44);
        tirePos.push([S.px[i] + S.nx[i] * o, S.py[i] - ROAD_LIFT, S.pz[i] + S.nz[i] * o, tirePos.length]);
      }
    }
  }
  const tireGeo = new THREE.CylinderGeometry(0.45, 0.45, 0.3, 12, 1, false);
  const tires = new THREE.InstancedMesh(tireGeo, [M.tireSide, M.tireCap, M.tireCap], tirePos.length * 2);
  const dm = new THREE.Object3D(), col = new THREE.Color();
  let ti = 0;
  for (const [x, y, z, n] of tirePos) {
    for (let layer = 0; layer < 2; layer++) {
      dm.position.set(x, y + 0.15 + layer * 0.3, z);
      dm.rotation.set(0, n * 0.7, 0);
      dm.updateMatrix();
      tires.setMatrixAt(ti, dm.matrix);
      const painted = (n >> 1) % 3 === 0;
      col.setHex(painted ? 0xe9e6de : 0x2a2b2e);
      tires.setColorAt(ti, col);
      ti++;
    }
  }
  tires.castShadow = true; tires.receiveShadow = true;
  group.add(tires);

  // ---------- Kicker jump (plywood wedge) ----------
  {
    const { i0, n } = S.jump;
    const top = [[-HW + 0.2, 0.004], [HW - 0.2, 0.004]];
    const g1 = sweep(S, [[i0, n]], [top], 2, 2);
    group.add(mesh(g1, M.ply, true, true));
    // sides: vertical faces from base road to ramp top
    const sidePos = [], sideIdx = [];
    for (const side of [1, -1]) {
      const o = side * (HW - 0.2);
      const st = sidePos.length / 3;
      for (let j = 0; j <= n; j++) {
        const i = wrapI(i0 + j, N);
        const bx = S.px[i] + S.nx[i] * o, bz = S.pz[i] + S.nz[i] * o;
        const yb = S.base[i] + ROAD_LIFT;
        sidePos.push(bx, yb, bz, bx, S.py[i] + 0.004, bz);
        if (j < n) {
          const a = st + j * 2;
          if (side > 0) sideIdx.push(a, a + 2, a + 1, a + 1, a + 2, a + 3);
          else sideIdx.push(a, a + 1, a + 2, a + 1, a + 3, a + 2);
        }
      }
    }
    // back face (vertical drop)
    const ie = wrapI(i0 + n, N);
    const st = sidePos.length / 3;
    for (const o of [-(HW - 0.2), HW - 0.2]) {
      const bx = S.px[ie] + S.nx[ie] * o, bz = S.pz[ie] + S.nz[ie] * o;
      sidePos.push(bx, S.base[ie] + ROAD_LIFT, bz, bx, S.py[ie] + 0.004, bz);
    }
    sideIdx.push(st, st + 2, st + 1, st + 1, st + 2, st + 3);
    const sg = new THREE.BufferGeometry();
    sg.setAttribute("position", new THREE.Float32BufferAttribute(sidePos, 3));
    sg.setIndex(sideIdx);
    sg.computeVertexNormals();
    group.add(mesh(sg, lam({ color: 0x9a6c3c, roughness: 0.9, side: THREE.DoubleSide }), true, true));
    // yellow/black lip stripe
    const lip = sweep(S, [[wrapI(i0 + n - 1, N), 1]], [[[-HW + 0.2, 0.012], [HW - 0.2, 0.012]]], 1, 6);
    group.add(mesh(lip, lam({ map: M.curb.map, color: 0xffd23a, polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -2 })));
  }

  // ---------- Bridge piers ----------
  {
    const pierGeo = new THREE.BoxGeometry(0.9, 1, 0.9);
    const capGeo = new THREE.BoxGeometry(1, 0.5, 1);
    const piers = [];
    let acc = 99;
    for (let i = 0; i < N; i++) {
      acc += S.ds;
      if (!isBridge(i) || acc < 7) continue;
      // keep clear of any OTHER road section below
      let blocked = false;
      for (let j = 0; j < N; j += 2) {
        if (Math.abs(wrapI(j - i + N / 2, N) - N / 2) < 40) continue;
        const d = Math.hypot(S.px[j] - S.px[i], S.pz[j] - S.pz[i]);
        if (d < W + 4.5) { blocked = true; break; }
      }
      if (blocked) continue;
      acc = 0;
      piers.push(i);
    }
    for (const i of piers) {
      const h = S.py[i] - DECK_T;
      for (const side of [-1, 1]) {
        const o = side * (W - 0.6);
        const p = mesh(pierGeo, M.concrete, true, true);
        p.scale.y = h;
        p.position.set(S.px[i] + S.nx[i] * o, h / 2, S.pz[i] + S.nz[i] * o);
        group.add(p);
      }
      const cap = mesh(capGeo, M.concreteDark, true, true);
      cap.scale.set(1, 1, 2 * W);
      cap.position.set(S.px[i], h - 0.25, S.pz[i]);
      cap.rotation.y = Math.atan2(S.tx[i], S.tz[i]) + Math.PI / 2;
      group.add(cap);
    }
    // Abutment pillars flanking the lower road (big, readable "bridge" cue)
    S.bridgePiers = piers.length;
  }

  // ---------- Props ----------
  addProps(group, S, M);

  mergeStatic(group);

  // ---------- Sky dome (follows camera) ----------
  const sky = makeSky();
  group.add(sky);

  scene.add(group);

  // ---------- Race bookkeeping ----------
  const cpCount = 24;
  const checkpoints = [];
  for (let c = 0; c < cpCount; c++) {
    const i = wrapI(S.startIdx + Math.round(((c + 1) / cpCount) * N), N);
    checkpoints.push({ x: S.px[i], z: S.pz[i], y: S.py[i], r: W + 2, i });
  }
  // Grid: 2 x 2 staggered, behind the line
  const startSpots = [];
  for (let c = 0; c < 4; c++) {
    const back = 5 + c * 3.2;
    const i = wrapI(S.startIdx - Math.round(back / S.ds), N);
    const lane = c % 2 === 0 ? -1.8 : 1.8;
    startSpots.push({
      x: S.px[i] + S.nx[i] * lane,
      z: S.pz[i] + S.nz[i] * lane,
      y: S.py[i] - 0.38, // game adds CAR_HALF.y; boundRaceCar snaps to surface
      yaw: Math.atan2(S.tz[i], S.tx[i]),
      i,
    });
  }

  _cached = {
    group,
    samples: S,
    sky,
    curve: S.curve,
    checkpoints,
    startSpots,
    roadHalfW: HW,
    length: S.L,
    railMask: null,
    ramps: [],
  };
  return _cached;
}

function makeSky() {
  const g = new THREE.SphereGeometry(290, 24, 12); // inside camera.far (320), outside all ground
  const colors = [];
  const top = new THREE.Color(0x4f8fd6), hor = new THREE.Color(0xcfe3f2), c = new THREE.Color();
  const p = g.attributes.position;
  for (let i = 0; i < p.count; i++) {
    const t = Math.max(0, p.getY(i) / 290);
    c.copy(hor).lerp(top, Math.pow(t, 0.6));
    colors.push(c.r, c.g, c.b);
  }
  g.setAttribute("color", new THREE.Float32BufferAttribute(colors, 3));
  const m = new THREE.Mesh(g, new THREE.MeshBasicMaterial({ vertexColors: true, side: THREE.BackSide, fog: false, depthWrite: false }));
  m.renderOrder = 50; // after opaque → early-z skips everything already covered
  m.frustumCulled = false;
  m.name = "raceSky";
  return m;
}

/* ------------------------------------------------------------------ */
/* Props: gantry, driver stand, pits, fence, banners, cones, hay, trees */
/* ------------------------------------------------------------------ */
function bannerTex(text, bg, fg, w = 512, h = 96) {
  return canvasTex(w, h, (g) => {
    g.fillStyle = bg; g.fillRect(0, 0, w, h);
    let size = Math.round(h * 0.6);
    g.font = `800 ${size}px Outfit, Arial, sans-serif`;
    const maxW = w * 0.9;
    const tw = g.measureText(text).width;
    if (tw > maxW) { size = Math.floor((size * maxW) / tw); g.font = `800 ${size}px Outfit, Arial, sans-serif`; }
    g.fillStyle = fg;
    g.textAlign = "center"; g.textBaseline = "middle";
    g.fillText(text, w / 2, h / 2 + 3);
  });
}

function addProps(group, S, M) {
  const N = S.N;
  const box = new THREE.BoxGeometry(1, 1, 1);
  const place = (mat, sx, sy, sz, x, y, z, ry = 0, cast = true) => {
    const m = mesh(box, mat, cast, true);
    m.scale.set(sx, sy, sz); m.position.set(x, y, z); m.rotation.y = ry;
    group.add(m);
    return m;
  };
  const W = BARRIER_OFF;

  // --- Start/finish gantry ---
  {
    const i = S.startIdx;
    const ry = -Math.atan2(S.tz[i], S.tx[i]);
    const cx = S.px[i], cz = S.pz[i];
    const off = W + 0.9, H = 4.6;
    for (const side of [-1, 1]) {
      const x = cx + S.nx[i] * off * side, z = cz + S.nz[i] * off * side;
      place(M.darkSteel, 0.45, H, 0.45, x, H / 2, z, ry);
    }
    const span = off * 2 + 0.6;
    // Box faces: +x/-x are the big faces (beam runs along local z = across the road)
    const beam = new THREE.Mesh(new THREE.BoxGeometry(0.5, 1.15, span), [
      lam({ map: bannerTex("START  ·  FINISH", "#101418", "#ffffff", 1024, 100) }),
      lam({ map: bannerTex("SIMILARIZE RC RACEWAY", "#101418", "#ffd23a", 1024, 100) }),
      M.darkSteel, M.darkSteel, M.darkSteel, M.darkSteel,
    ]);
    beam.position.set(cx, H, cz);
    beam.rotation.y = ry;
    beam.castShadow = true;
    group.add(beam);
    // start lights
    const lightMat = lam({ color: 0x330000, emissive: 0xff2a1a, emissiveIntensity: 0.9 });
    for (let k = -2; k <= 2; k++) {
      const l = mesh(new THREE.SphereGeometry(0.16, 10, 8), lightMat, false, false);
      l.position.set(cx + S.nx[i] * k * 0.55 - S.tx[i] * 0.3, H - 0.8, cz + S.nz[i] * k * 0.55 - S.tz[i] * 0.3);
      group.add(l);
    }
  }

  // --- Driver stand (outside the main straight, facing the track) ---
  {
    const cx = -2, cz = 49.5, len = 16, dep = 3.4, ph = 2.3;
    place(M.wood, len, 0.25, dep, cx, ph, cz);                       // deck
    for (const dx of [-len / 2 + 0.3, -len / 4, 0, len / 4, len / 2 - 0.3])
      for (const dz of [-dep / 2 + 0.2, dep / 2 - 0.2]) place(M.darkSteel, 0.18, ph, 0.18, cx + dx, ph / 2, cz + dz);
    place(M.steel, len, 0.07, 0.07, cx, ph + 1.05, cz - dep / 2 + 0.1, 0, false); // railing
    place(M.steel, len, 0.07, 0.07, cx, ph + 0.6, cz - dep / 2 + 0.1, 0, false);
    for (let dx = -len / 2 + 0.2; dx <= len / 2; dx += 2) place(M.steel, 0.06, 1.05, 0.06, cx + dx, ph + 0.52, cz - dep / 2 + 0.1, 0, false);
    // roof
    for (const dx of [-len / 2 + 0.3, len / 2 - 0.3]) place(M.darkSteel, 0.14, 2.6, 0.14, cx + dx, ph + 1.3, cz + dep / 2 - 0.2);
    const roof = place(M.white, len + 0.8, 0.15, dep + 1.0, cx, ph + 2.65, cz + 0.2);
    roof.rotation.x = -0.12;
    // stairs
    for (let s = 0; s < 6; s++) place(M.wood, 1.4, 0.12, 0.45, cx + len / 2 + 0.9, 0.2 + s * 0.38, cz + 1.2 - s * 0.42);
    // drivers with transmitters
    const skin = lam({ color: 0xe0b48c, roughness: 0.8 });
    const shirts = [0xe4572e, 0x3b82f6, 0x34d399, 0xc084fc, 0xf1c40f];
    const body = new THREE.CapsuleGeometry(0.28, 0.75, 4, 8);
    const head = new THREE.SphereGeometry(0.2, 10, 8);
    for (let p = 0; p < 5; p++) {
      const x = cx - 6 + p * 3;
      const b = mesh(body, lam({ color: shirts[p], roughness: 0.8 }), true, true);
      b.position.set(x, ph + 0.13 + 0.65, cz - 0.6);
      group.add(b);
      const h = mesh(head, skin, true, false);
      h.position.set(x, ph + 0.13 + 1.35, cz - 0.6);
      group.add(h);
      place(M.darkSteel, 0.35, 0.22, 0.12, x, ph + 1.05, cz - 0.95, 0, false);
    }
  }

  // --- Pit tents + tables (behind the stand, right) ---
  {
    const tentCols = [0xe4572e, 0x2f6fd6, 0xf1c40f];
    const roofGeo = new THREE.ConeGeometry(2.4, 1.1, 4, 1);
    for (let t = 0; t < 3; t++) {
      const x = 16 + t * 6.2, z = 51;
      for (const dx of [-1.6, 1.6]) for (const dz of [-1.6, 1.6]) place(M.steel, 0.08, 2.2, 0.08, x + dx, 1.1, z + dz, 0, false);
      const r = mesh(roofGeo, lam({ color: tentCols[t], roughness: 0.7 }), true, false);
      r.position.set(x, 2.75, z); r.rotation.y = Math.PI / 4;
      group.add(r);
      place(M.white, 2.2, 0.08, 0.9, x, 0.85, z);
      for (const dx of [-0.95, 0.95]) place(M.darkSteel, 0.06, 0.85, 0.6, x + dx, 0.42, z, 0, false);
    }
    // pit sign banner
    const sign = new THREE.Mesh(new THREE.BoxGeometry(6, 1, 0.1), lam({ map: bannerTex("PITS", "#2f6fd6", "#ffffff") }));
    sign.position.set(28.6, 1.6, 47.6);
    group.add(sign);
  }

  // --- Perimeter fence + banners ---
  {
    const x0 = -80, x1 = 69, z0 = -54, z1 = 58;
    const posts = [];
    const edges = [[x0, z0, x1, z0], [x1, z0, x1, z1], [x1, z1, x0, z1], [x0, z1, x0, z0]];
    for (const [ax, az, bx, bz] of edges) {
      const len = Math.hypot(bx - ax, bz - az), n = Math.round(len / 4);
      for (let k = 0; k < n; k++) posts.push([ax + ((bx - ax) * k) / n, az + ((bz - az) * k) / n]);
      const ry = -Math.atan2(bz - az, bx - ax);
      for (const y of [0.55, 1.15]) place(M.steel, len, 0.06, 0.06, (ax + bx) / 2, y, (az + bz) / 2, ry, false);
    }
    const postGeo = new THREE.BoxGeometry(0.12, 1.3, 0.12);
    const pm = new THREE.InstancedMesh(postGeo, M.darkSteel, posts.length);
    const d = new THREE.Object3D();
    posts.forEach(([x, z], k) => { d.position.set(x, 0.65, z); d.updateMatrix(); pm.setMatrixAt(k, d.matrix); });
    group.add(pm);
    const ads = [
      ["SIMILARIZE", "#e4572e", "#ffffff"], ["RC ARCADE", "#101418", "#ffd23a"],
      ["SIMILARIZE.COM", "#2f6fd6", "#ffffff"], ["FULL THROTTLE", "#ffd23a", "#101418"],
    ];
    const adMats = ads.map(([t, b, f]) => lam({ map: bannerTex(t, b, f), roughness: 0.7 }));
    const adGeo = new THREE.BoxGeometry(7, 1.0, 0.06);
    let a = 0;
    const putAd = (x, z, ry) => {
      const m = mesh(adGeo, adMats[a++ % adMats.length], false, true);
      m.position.set(x, 0.8, z); m.rotation.y = ry; group.add(m);
    };
    for (let x = -64; x <= 56; x += 24) { putAd(x, z0 + 0.1, 0); }
    for (let z = -36; z <= 36; z += 24) { putAd(x1 - 0.1, z, -Math.PI / 2); putAd(x0 + 0.1, z, Math.PI / 2); }
    for (const x of [-56, -34, 42, 60]) putAd(x, z1 - 0.1, Math.PI);
  }

  // --- Cones at apexes (on the grass, inside of corners) ---
  {
    const conePos = [];
    let acc = 0;
    for (let i = 0; i < N; i++) {
      acc += S.ds;
      if (Math.abs(S.ks[i]) < 1 / 18 || acc < 4 || S.base[i] > 0.05) continue;
      acc = 0;
      const side = Math.sign(S.ks[i]); // inside of the bend
      const o = side * (BARRIER_OFF + 1.6);
      const x = S.px[i] + S.nx[i] * o, z = S.pz[i] + S.nz[i] * o;
      if (clearOfRoad(S, x, z, BARRIER_OFF + 1.0)) conePos.push([x, z]);
    }
    const coneGeo = new THREE.ConeGeometry(0.3, 0.8, 10);
    const cm = new THREE.InstancedMesh(coneGeo, M.cone, conePos.length);
    const d = new THREE.Object3D();
    conePos.forEach(([x, z], k) => { d.position.set(x, 0.4, z); d.updateMatrix(); cm.setMatrixAt(k, d.matrix); });
    cm.castShadow = true;
    group.add(cm);
  }

  // --- Hay bales behind tire walls of the fast corners + a few loose ---
  {
    const hayPos = [];
    const spots = [[60, 30], [64, 12], [56, -45], [-52, -46], [-60, -35], [-76, 24], [-63, 46], [44, 46]];
    for (const [x, z] of spots) {
      for (let k = 0; k < 3; k++) {
        const hx = x + k * 1.25, hz = z;
        if (clearOfRoad(S, hx, hz, BARRIER_OFF + 1.4)) hayPos.push([hx, hz]);
      }
    }
    const hayGeo = new THREE.CylinderGeometry(0.6, 0.6, 1.2, 12);
    hayGeo.rotateZ(Math.PI / 2);
    const hm = new THREE.InstancedMesh(hayGeo, M.hay, hayPos.length);
    const d = new THREE.Object3D();
    hayPos.forEach(([x, z], k) => { d.position.set(x, 0.6, z); d.rotation.y = (k * 1.3) % 0.6; d.updateMatrix(); hm.setMatrixAt(k, d.matrix); });
    hm.castShadow = true; hm.receiveShadow = true;
    group.add(hm);
  }

  // --- Trees outside the fence ---
  {
    const r = rng(42);
    const pts = [];
    for (let k = 0; k < 70; k++) {
      const side = k % 4;
      let x, z;
      if (side === 0) { x = -88 + r() * 165; z = -58 - 3 - r() * 14; }
      else if (side === 1) { x = -88 + r() * 165; z = 62 + r() * 14; }
      else if (side === 2) { x = -85 - r() * 14; z = -60 + r() * 120; }
      else { x = 74 + r() * 14; z = -60 + r() * 120; }
      pts.push([x, z, 0.8 + r() * 0.7]);
    }
    const leafGeo = new THREE.ConeGeometry(2.2, 6, 7);
    const trunkGeo = new THREE.CylinderGeometry(0.25, 0.3, 1.6, 6);
    const lm = new THREE.InstancedMesh(leafGeo, M.leaf, pts.length);
    const tm = new THREE.InstancedMesh(trunkGeo, M.trunk, pts.length);
    const d = new THREE.Object3D();
    pts.forEach(([x, z, s], k) => {
      d.scale.set(s, s, s); d.position.set(x, 1.6 * s + 3 * s - 0.2, z); d.updateMatrix(); lm.setMatrixAt(k, d.matrix);
      d.position.set(x, 0.8 * s, z); d.updateMatrix(); tm.setMatrixAt(k, d.matrix);
    });
    group.add(lm, tm);
  }
}

function clearOfRoad(S, x, z, r) {
  const r2 = r * r;
  for (let i = 0; i < S.N; i += 2) {
    if ((S.px[i] - x) ** 2 + (S.pz[i] - z) ** 2 < r2) return false;
  }
  return true;
}

/* ------------------------------------------------------------------ */
/* Track queries                                                       */
/* ------------------------------------------------------------------ */
function localSearch(S, x, z, hint) {
  let best = hint, bd = Infinity;
  let lo = hint - 24, hi = hint + 24;
  for (let pass = 0; pass < 6; pass++) {
    for (let j = lo; j <= hi; j++) {
      const i = wrapI(j, S.N);
      const d = (S.px[i] - x) ** 2 + (S.pz[i] - z) ** 2;
      if (d < bd) { bd = d; best = j; }
    }
    if (best - lo < 3) { hi = lo; lo -= 40; }
    else if (hi - best < 3) { lo = hi; hi += 40; }
    else break;
  }
  return { i: wrapI(best, S.N), d2: bd };
}

function globalSearch(S, x, y, z) {
  let best = 0, bc = Infinity;
  for (let i = 0; i < S.N; i++) {
    const c = (S.px[i] - x) ** 2 + (S.pz[i] - z) ** 2 + 6 * (S.py[i] - y) ** 2;
    if (c < bc) { bc = c; best = i; }
  }
  return best;
}

/** Locate car on track: index, lateral offset, surface height (bridge-safe). */
export function locateOnTrack(meta, car) {
  const S = meta.samples;
  let i;
  if (typeof car._ti === "number") {
    const r = localSearch(S, car.pos.x, car.pos.z, car._ti);
    i = r.d2 > 64 ? globalSearch(S, car.pos.x, car.pos.y, car.pos.z) : r.i;
  } else {
    i = globalSearch(S, car.pos.x, car.pos.y, car.pos.z);
  }
  car._ti = i;
  // interpolate toward the neighbour on the car's side of the sample
  const dx = car.pos.x - S.px[i], dz = car.pos.z - S.pz[i];
  const along = dx * S.tx[i] + dz * S.tz[i];
  const j = wrapI(i + (along >= 0 ? 1 : -1), S.N);
  const f = Math.min(1, Math.abs(along) / S.ds);
  const lat = dx * S.nx[i] + dz * S.nz[i];
  const y = S.py[i] + (S.py[j] - S.py[i]) * f;
  const slope = S.slope[i] + (S.slope[j] - S.slope[i]) * f;
  return { i, lat, y, slope, along };
}

/** Physics hook (race only): rails, height, jumps. Replaces arena bounds. */
export function boundRaceCar(car, carHalfY, raceMeta) {
  const S = raceMeta.samples;
  const loc = locateOnTrack(raceMeta, car);
  const i = loc.i;
  const tx = S.tx[i], tz = S.tz[i], nx = S.nx[i], nz = S.nz[i];

  // Barriers: clamp lateral offset, kill outward velocity, keep tangential speed
  if (Math.abs(loc.lat) > RAIL_LIMIT) {
    const sign = loc.lat > 0 ? 1 : -1;
    const over = Math.abs(loc.lat) - RAIL_LIMIT;
    car.pos.x -= nx * sign * over;
    car.pos.z -= nz * sign * over;
    const vn = (car.vx * nx + car.vz * nz) * sign; // outward speed
    if (vn > 0) {
      car.vx -= nx * sign * vn * 1.25;
      car.vz -= nz * sign * vn * 1.25;
      car._wallHit = vn;
      if (vn > 3) car._wallHits = (car._wallHits || 0) + 1;
    }
  }

  // Height: follow the surface, launch off lips, land with gravity
  const vAlong = car.vx * tx + car.vz * tz;
  const slopeVy = loc.slope * vAlong;
  const ground = loc.y;
  const wasAir = car._air;
  if (car.pos.y <= ground + 0.03) {
    car.pos.y = ground;
    car.vy = slopeVy;
    car.onGround = true;
    if (wasAir && car._airT > 0.25) car._landed = true;
    car._air = false;
    car._airT = 0;
  } else {
    car.onGround = false;
    if (!wasAir && car.pos.y - ground > 0.3) car._launched = true;
    car._air = true;
  }
  car._slope = car.onGround ? loc.slope : 0;
  if (car.pos.y > 20) { car.pos.y = 20; car.vy = Math.min(0, car.vy); }
  // Fell through / escaped somehow → respawn on centreline
  if (car.pos.y < ground - 2.5 || Math.abs(loc.lat) > BARRIER_OFF + 3) respawnOnTrack(raceMeta, car, i);
}

export function respawnOnTrack(meta, car, i) {
  const S = meta.samples;
  if (typeof i !== "number") i = typeof car._ti === "number" ? car._ti : 0;
  // back up a little so we don't drop onto a jump lip
  i = wrapI(i - 6, S.N);
  car.pos.set(S.px[i], S.py[i] + 0.4, S.pz[i]);
  car.yaw = Math.atan2(S.tz[i], S.tx[i]);
  car.vx = S.tx[i] * 3; car.vz = S.tz[i] * 3; car.vy = 0;
  car._ti = i;
  car._air = true; car._airT = 0;
  car._stuckT = 0;
  car._respawns = (car._respawns || 0) + 1;
}

/** Visual pose: yaw + pitch (track slope projected on heading, or flight path). */
export function poseRaceCar(car, dt) {
  const m = car.mesh;
  if (!m) return;
  if (m.rotation.order !== "YXZ") m.rotation.order = "YXZ";
  if (car._air) car._airT = (car._airT || 0) + dt;
  const fx = Math.cos(car.yaw), fz = Math.sin(car.yaw);
  let want = 0;
  if (car.onGround || !car._air) {
    const S = car._meta && car._meta.samples;
    if (S && typeof car._ti === "number") {
      const c = fx * S.tx[car._ti] + fz * S.tz[car._ti];
      want = Math.atan((car._slope || 0) * c);
    }
  } else {
    const vh = Math.max(4, Math.abs(car.vx * fx + car.vz * fz));
    want = Math.max(-0.5, Math.min(0.5, Math.atan2(car.vy, vh) * 0.7));
  }
  const p = car._pitch || 0;
  car._pitch = p + (want - p) * Math.min(1, dt * (car._air ? 5 : 16));
  m.position.copy(car.pos);
  m.rotation.set(0, -car.yaw, car._pitch);
}

/** Pure-pursuit plan for AI. laneBias in metres (+ = right). */
export function raceAiPlan(meta, car, laneBias = 0) {
  const S = meta.samples;
  if (typeof car._ti !== "number") locateOnTrack(meta, car);
  const sp = Math.hypot(car.vx, car.vz);
  const look = 5.5 + sp * 0.42;
  const ia = wrapI(car._ti + Math.round(look / S.ds), S.N);
  // heading change over the next stretch → corner severity
  const far = Math.round((10 + sp * 1.25) / S.ds);
  let turn = 0, maxK = 0;
  for (let j = 0; j < far; j += 2) {
    const ii = wrapI(car._ti + j, S.N);
    const kk = Math.abs(S.ks[ii]);
    if (kk > maxK) maxK = kk;
  }
  const i2 = wrapI(car._ti + far, S.N);
  turn = Math.abs(Math.atan2(S.tx[car._ti] * S.tz[i2] - S.tz[car._ti] * S.tx[i2], S.tx[car._ti] * S.tx[i2] + S.tz[car._ti] * S.tz[i2]));
  // tighter line on the inside of bends
  const bias = laneBias + Math.sign(S.ks[ia]) * Math.min(1.6, Math.abs(S.ks[ia]) * 40);
  const lb = Math.max(-HW + 1, Math.min(HW - 1, bias));
  return {
    tx: S.px[ia] + S.nx[ia] * lb,
    tz: S.pz[ia] + S.nz[ia] * lb,
    turn,
    maxK,
    jumpAhead: wrapI(S.jump.i0 - car._ti, S.N) < Math.round(30 / S.ds),
  };
}

/** Fractional lap progress in [0,1) measured from the start line. */
export function trackFrac(meta, car) {
  const S = meta.samples;
  if (typeof car._ti !== "number") return 0;
  return wrapI(car._ti - S.startIdx, S.N) / S.N;
}

/* ------------------------------------------------------------------ */
/* Race state / laps / ranking                                         */
/* ------------------------------------------------------------------ */
export function createRaceState(numCars, laps) {
  const n = Math.max(2, Math.min(4, numCars | 0));
  return {
    numCars: n,
    laps: laps || RACE_LAPS_DEFAULT,
    started: false,
    finished: false,
    countdown: 3.2,
    finishCount: 0,
    places: [],
    racers: Array.from({ length: n }, () => ({
      cp: 0, lap: 0, finished: false, finishOrder: -1, progress: 0,
    })),
  };
}

export function raceProgress(racer, checkpoints) {
  return racer.lap * checkpoints.length + racer.cp + (racer._frac || 0);
}

/** Checkpoints are passed by track index (bridge-safe), in order only. */
export function updateRaceProgress(car, racer, checkpoints, totalLaps) {
  if (racer.finished) return false;
  const cp = checkpoints[racer.cp];
  let passed;
  if (typeof car._ti === "number" && typeof cp.i === "number" && car._meta) {
    const N = car._meta.samples.N;
    const ahead = wrapI(car._ti - cp.i, N);
    passed = ahead < 30 && Math.abs(car.pos.y - cp.y) < 3;
    const prev = checkpoints[(racer.cp + checkpoints.length - 1) % checkpoints.length];
    const segLen = wrapI(cp.i - prev.i, N) || 1;
    racer._frac = Math.max(0, Math.min(0.99, 1 - wrapI(cp.i - car._ti, N) / segLen));
    if (racer._frac > 0.99 || wrapI(cp.i - car._ti, N) > segLen) racer._frac = 0;
  } else {
    passed = Math.hypot(car.pos.x - cp.x, car.pos.z - cp.z) < cp.r;
  }
  if (passed) {
    racer.cp++;
    racer._frac = 0;
    if (racer.cp >= checkpoints.length) {
      racer.cp = 0;
      racer.lap++;
      if (racer.lap >= totalLaps) {
        racer.finished = true;
        racer.progress = raceProgress(racer, checkpoints);
        return true;
      }
    }
  }
  racer.progress = raceProgress(racer, checkpoints);
  return false;
}

export function rankRacers(raceState) {
  const idx = raceState.racers.map((_, i) => i);
  idx.sort((a, b) => {
    const ra = raceState.racers[a], rb = raceState.racers[b];
    if (ra.finished && rb.finished) return ra.finishOrder - rb.finishOrder;
    if (ra.finished) return -1;
    if (rb.finished) return 1;
    return rb.progress - ra.progress;
  });
  return idx;
}

/* ------------------------------------------------------------------ */
/* Camera                                                              */
/* ------------------------------------------------------------------ */
/**
 * Race camera rig. mode "chase": low three-quarter behind one car, looking
 * ahead along the track. mode "overview": fixed-azimuth tilted view that frames
 * the focus cars, zooms to fit, and clamps so cars never get tiny.
 */
export function createRaceCamRig() {
  return {
    mode: "overview",
    pos: new THREE.Vector3(0, 60, 60),
    look: new THREE.Vector3(),
    yaw: 0,
    dist: 40,
    init: false,
  };
}

const _v = new THREE.Vector3();
export function updateRaceCam(rig, camera, cars, focusIdx, chaseIdx, meta, dt) {
  const k = (rate) => 1 - Math.exp(-rate * dt);
  if (rig.mode === "chase" && cars[chaseIdx]) {
    const c = cars[chaseIdx];
    const S = meta.samples;
    // heading: blend car velocity dir with track tangent so spins don't whip the camera
    const sp = Math.hypot(c.vx, c.vz);
    let hx = Math.cos(c.yaw), hz = Math.sin(c.yaw);
    if (sp > 3) { hx = hx * 0.5 + (c.vx / sp) * 0.5; hz = hz * 0.5 + (c.vz / sp) * 0.5; }
    if (typeof c._ti === "number") { hx = hx * 0.6 + S.tx[c._ti] * 0.4; hz = hz * 0.6 + S.tz[c._ti] * 0.4; }
    const wantYaw = Math.atan2(hz, hx);
    if (!rig.init) { rig.yaw = wantYaw; }
    let dy = wantYaw - rig.yaw;
    while (dy > Math.PI) dy -= Math.PI * 2;
    while (dy < -Math.PI) dy += Math.PI * 2;
    rig.yaw += dy * k(3.2);
    const fx = Math.cos(rig.yaw), fz = Math.sin(rig.yaw);
    const back = 8.5 + Math.min(3, sp * 0.08), up = 4.3;
    const groundY = c.onGround ? c.pos.y : Math.min(c.pos.y, rig.look.y + 1);
    _v.set(c.pos.x - fx * back, groundY + up, c.pos.z - fz * back);
    if (!rig.init) { rig.pos.copy(_v); rig.look.set(c.pos.x + fx * 7, groundY + 0.6, c.pos.z + fz * 7); rig.init = true; }
    rig.pos.lerp(_v, k(6));
    _v.set(c.pos.x + fx * 7, groundY + 0.6, c.pos.z + fz * 7);
    rig.look.lerp(_v, k(8));
    camera.position.copy(rig.pos);
    camera.lookAt(rig.look);
    const wantFov = 62;
    camera.fov += (wantFov - camera.fov) * k(4);
    camera.updateProjectionMatrix();
    return rig.look;
  }

  // Overview: frame focus cars (+ velocity lead), fixed azimuth, tilt ~52°
  let minX = Infinity, maxX = -Infinity, minZ = Infinity, maxZ = -Infinity, sy = 0, n = 0;
  for (const i of focusIdx) {
    const c = cars[i];
    if (!c) continue;
    const lx = c.pos.x + c.vx * 0.45, lz = c.pos.z + c.vz * 0.45;
    minX = Math.min(minX, c.pos.x, lx); maxX = Math.max(maxX, c.pos.x, lx);
    minZ = Math.min(minZ, c.pos.z, lz); maxZ = Math.max(maxZ, c.pos.z, lz);
    sy += c.pos.y; n++;
  }
  if (!n) { minX = -20; maxX = 20; minZ = -20; maxZ = 20; n = 1; }
  const pad = 9;
  const cx = (minX + maxX) / 2, cz = (minZ + maxZ) / 2;
  const spanX = maxX - minX + pad * 2, spanZ = maxZ - minZ + pad * 2;
  const pitch = 0.92; // ~53° down
  const vfov = THREE.MathUtils.degToRad(camera.fov);
  const hfov = 2 * Math.atan(Math.tan(vfov / 2) * camera.aspect);
  // distance needed to fit: x across, z foreshortened by sin(pitch)
  const needX = spanX / 2 / Math.tan(hfov / 2);
  const needZ = (spanZ * Math.sin(pitch)) / 2 / Math.tan(vfov / 2);
  const MIN_D = 24, MAX_D = 62; // MAX_D = cars never shrink below ~readable size
  const want = Math.max(MIN_D, Math.min(MAX_D, Math.max(needX, needZ)));
  if (!rig.init) { rig.dist = want; rig.look.set(cx, sy / n, cz); }
  rig.dist += (want - rig.dist) * k(want > rig.dist ? 2.5 : 1.2);
  _v.set(cx, Math.max(0, sy / n) * 0.6, cz);
  rig.look.lerp(_v, k(3));
  rig.pos.set(rig.look.x, rig.look.y + Math.sin(pitch) * rig.dist, rig.look.z + Math.cos(pitch) * rig.dist);
  rig.init = true;
  camera.position.copy(rig.pos);
  camera.lookAt(rig.look);
  camera.fov += (50 - camera.fov) * k(4);
  camera.updateProjectionMatrix();
  return rig.look;
}

/**
 * Race tyre marks: ONE instanced mesh ring buffer with per-instance fade
 * (the soccer path makes 2 meshes + 2 materials per mark → ~280 draw calls).
 */
export function createSkidMarks(parent, max = 900, life = 2.2) {
  const geo = new THREE.PlaneGeometry(0.55, 0.11);
  geo.rotateX(-Math.PI / 2);
  const alpha = new Float32Array(max);
  const born = new Float32Array(max).fill(-1e9);
  const aAttr = new THREE.InstancedBufferAttribute(alpha, 1);
  aAttr.setUsage(THREE.DynamicDrawUsage);
  geo.setAttribute("aAlpha", aAttr);
  const mat = new THREE.MeshBasicMaterial({
    color: 0x0a0c10, transparent: true, opacity: 0.5, depthWrite: false,
    polygonOffset: true, polygonOffsetFactor: -4, polygonOffsetUnits: -4,
  });
  mat.onBeforeCompile = (sh) => {
    sh.vertexShader = "attribute float aAlpha;\nvarying float vAlpha;\n" +
      sh.vertexShader.replace("#include <begin_vertex>", "#include <begin_vertex>\nvAlpha = aAlpha;");
    sh.fragmentShader = "varying float vAlpha;\n" +
      sh.fragmentShader.replace("#include <color_fragment>", "#include <color_fragment>\ndiffuseColor.a *= vAlpha;");
  };
  const m = new THREE.InstancedMesh(geo, mat, max);
  m.frustumCulled = false;
  m.renderOrder = 2;
  const hide = new THREE.Matrix4().makeScale(0, 0, 0);
  for (let i = 0; i < max; i++) m.setMatrixAt(i, hide);
  m.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
  parent.add(m);
  let head = 0, now = 0;
  const d = new THREE.Object3D();
  return {
    mesh: m,
    drop(x, y, z, yaw) {
      const c = Math.cos(yaw), s = Math.sin(yaw);
      for (const side of [-0.42, 0.42]) {
        // local +Z (left of nose) → world (-sin, cos)
        d.position.set(x - s * side, y, z + c * side);
        d.rotation.set(0, -yaw, 0);
        d.updateMatrix();
        m.setMatrixAt(head, d.matrix);
        born[head] = now;
        head = (head + 1) % max;
      }
      m.instanceMatrix.needsUpdate = true;
    },
    update(dt) {
      now += dt;
      for (let i = 0; i < max; i++) alpha[i] = Math.max(0, 1 - (now - born[i]) / life);
      aAttr.needsUpdate = true;
    },
    clear() {
      born.fill(-1e9);
      alpha.fill(0);
      aAttr.needsUpdate = true;
    },
  };
}

/** Floating screen-size name tag for a car (race only). */
const _tagCache = new Map();
export function makeCarTag(text, colorHex) {
  const key = text + "|" + colorHex;
  if (_tagCache.has(key)) return _tagCache.get(key).clone(); // shares material/texture
  const c = document.createElement("canvas");
  c.width = 128; c.height = 64;
  const g = c.getContext("2d");
  const col = "#" + colorHex.toString(16).padStart(6, "0");
  g.fillStyle = "rgba(10,12,16,.82)";
  g.beginPath();
  g.roundRect ? g.roundRect(8, 6, 112, 40, 12) : g.rect(8, 6, 112, 40);
  g.fill();
  g.lineWidth = 5; g.strokeStyle = col; g.stroke();
  g.beginPath(); g.moveTo(52, 46); g.lineTo(64, 60); g.lineTo(76, 46); g.closePath();
  g.fillStyle = col; g.fill();
  g.fillStyle = "#fff"; g.font = "800 26px Outfit, Arial, sans-serif";
  g.textAlign = "center"; g.textBaseline = "middle";
  g.fillText(text, 64, 27);
  const tex = new THREE.CanvasTexture(c);
  tex.colorSpace = THREE.SRGBColorSpace;
  const mat = new THREE.SpriteMaterial({ map: tex, depthTest: false, depthWrite: false, transparent: true, sizeAttenuation: false });
  const s = new THREE.Sprite(mat);
  s.center.set(0.5, 0);
  s.scale.set(0.075, 0.0375, 1);
  s.renderOrder = 20;
  _tagCache.set(key, s);
  return s.clone();
}

/* Back-compat exports (older callers) */
export function raceArenaFocus(cars) {
  let x = 0, y = 0, z = 0;
  for (const c of cars) { x += c.pos.x; y += c.pos.y; z += c.pos.z; }
  const n = cars.length || 1;
  return { x: x / n, y: y / n, z: z / n, span: 40 };
}
export function raceChaseFocus(cars) { return raceArenaFocus(cars); }
export function raceCameraTarget(cars) { return raceArenaFocus(cars); }
export function sampleRampY(ramps, x, z, carHalfY) { return carHalfY; }
/** Test hook: sample table without building meshes. */
export function buildTrackSamplesForTest() { return buildSamples(); }
