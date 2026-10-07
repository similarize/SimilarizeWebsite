/**
 * Race Track — ONE continuous extruded road along a figure-eight spline.
 * Features via elevation/banking décor: banks, jump crest, loop-like rise,
 * figure-eight over/under crossing, elevation twists.
 */
import * as THREE from "three";

export const RACE_LAPS_DEFAULT = 3;
export const RACE_COLORS = [0xe4572e, 0x3b82f6, 0x34d399, 0xc084fc];
export const RACE_NAMES = ["Orange", "Blue", "Green", "Purple"];

const ROAD_HALF_W = 4.2;
const SAMPLE_N = 220;
const RAIL_OFFSET = ROAD_HALF_W + 0.45;

/**
 * Intentional rail gaps (normalized u along closed curve).
 * both: omit L+R. outerOnly: omit outer-of-bend side only.
 * jump crest ~u 0.83, loop ~u 0.02, banks, scenic overlook.
 */
const RAIL_GAPS = [
  { u0: 0.78, u1: 0.88, both: true },       // jump crest
  { u0: 0.97, u1: 1.0, both: true },        // loop approach (wrap)
  { u0: 0.0, u1: 0.09, both: true },        // loop crest / landing
  { u0: 0.40, u1: 0.48, both: true },       // scenic overlook stretch
  { u0: 0.18, u1: 0.30, outerOnly: true },  // outer bank (bridge high)
  { u0: 0.55, u1: 0.66, outerOnly: true },  // outer bank (SE lobe)
];

function uInRange(u, a, b) {
  u = ((u % 1) + 1) % 1;
  if (a <= b) return u >= a && u <= b;
  return u >= a || u <= b;
}

/** @returns {{ left: boolean, right: boolean }} side = +1 is "right" of tangent (nx,nz)=(-tz,tx) */
export function railPresence(u, outerSign) {
  let left = true, right = true;
  for (const g of RAIL_GAPS) {
    if (!uInRange(u, g.u0, g.u1)) continue;
    if (g.both) return { left: false, right: false };
    if (g.outerOnly && typeof outerSign === "number") {
      if (outerSign > 0) right = false;
      else left = false;
    }
  }
  return { left, right };
}

function bendOuterSign(curve, u) {
  const u0 = ((u - 0.008) % 1 + 1) % 1;
  const u1 = ((u + 0.008) % 1 + 1) % 1;
  const t0 = curve.getTangentAt(u0);
  const t1 = curve.getTangentAt(u1);
  const cross = t0.x * t1.z - t0.z * t1.x;
  return cross >= 0 ? 1 : -1;
}

/** Lemniscate figure-eight centerline with adventure elevation. */
export function buildRaceCurve() {
  const pts = [];
  const a = 26; // scale
  const steps = 160;
  for (let i = 0; i < steps; i++) {
    const t = (i / steps) * Math.PI * 2;
    const den = Math.sin(t) * Math.sin(t) + 1;
    const x = (a * Math.SQRT2 * Math.cos(t)) / den;
    const z = (a * Math.SQRT2 * Math.cos(t) * Math.sin(t)) / den;

    // Base over/under at the two origin crossings (t≈π/2 bridge, t≈3π/2 tunnel)
    let y = 1.6 + 2.4 * Math.sin(t); // ~ -0.8 .. 4.0

    // FEATURE: jump crest (south-east lobe)
    const jump = Math.exp(-Math.pow((t - 5.2) / 0.28, 2));
    y += 3.2 * jump;

    // FEATURE: loop-like tall rise (east tip)
    const loop = Math.exp(-Math.pow((t - 0.15) / 0.35, 2));
    y += 4.5 * loop;

    // FEATURE: elevation twists (gentle undulation)
    y += 0.55 * Math.sin(3 * t) + 0.35 * Math.cos(5 * t);

    // Keep tunnel side from going underground too far
    if (y < 0.15) y = 0.15;
    pts.push(new THREE.Vector3(x, y, z));
  }
  const curve = new THREE.CatmullRomCurve3(pts, true, "catmullrom", 0.35);
  return curve;
}

export function buildRaceTrack(scene) {
  const group = new THREE.Group();
  group.name = "raceTrack";

  const curve = buildRaceCurve();

  // Brighter race palette — readable asphalt/curbs without blowing out whites
  const asphalt = new THREE.MeshStandardMaterial({
    color: 0x5a6574, roughness: 0.82, metalness: 0.1,
    emissive: 0x1a2030, emissiveIntensity: 0.22,
  });
  const asphaltEdge = new THREE.MeshStandardMaterial({
    color: 0xf0b45a, roughness: 0.5, metalness: 0.12,
    emissive: 0xa86a18, emissiveIntensity: 0.28,
  });
  const grass = new THREE.MeshStandardMaterial({
    color: 0x2a5a3e, roughness: 0.92, metalness: 0.02,
    emissive: 0x0a2014, emissiveIntensity: 0.12,
  });
  const wallMat = new THREE.MeshStandardMaterial({
    color: 0x3a4558, roughness: 0.78, metalness: 0.18,
    emissive: 0x121820, emissiveIntensity: 0.15,
  });
  const accent = new THREE.MeshStandardMaterial({
    color: 0x7eb8ff, roughness: 0.42, metalness: 0.28,
    emissive: 0x2563eb, emissiveIntensity: 0.35,
  });
  const stripe = new THREE.MeshBasicMaterial({ color: 0xfff8ec });

  // Ground
  const ground = new THREE.Mesh(new THREE.CylinderGeometry(55, 55, 0.5, 48), grass);
  ground.position.y = -0.4;
  ground.receiveShadow = true;
  group.add(ground);

  // —— Continuous road (single extrusion) ——
  const roadShape = new THREE.Shape();
  const hw = ROAD_HALF_W;
  roadShape.moveTo(-hw, 0);
  roadShape.lineTo(hw, 0);
  roadShape.lineTo(hw, 0.32);
  roadShape.lineTo(-hw, 0.32);
  roadShape.closePath();

  const roadGeo = new THREE.ExtrudeGeometry(roadShape, {
    steps: 280,
    bevelEnabled: false,
    extrudePath: curve,
  });
  const road = new THREE.Mesh(roadGeo, asphalt);
  road.castShadow = true;
  road.receiveShadow = true;
  group.add(road);

  // Thin edge curbs (slightly wider / thinner strip) — continuous look
  const curbShape = new THREE.Shape();
  curbShape.moveTo(-hw - 0.35, 0.28);
  curbShape.lineTo(-hw + 0.15, 0.28);
  curbShape.lineTo(-hw + 0.15, 0.48);
  curbShape.lineTo(-hw - 0.35, 0.48);
  curbShape.closePath();
  const curbL = new THREE.Mesh(
    new THREE.ExtrudeGeometry(curbShape, { steps: 200, bevelEnabled: false, extrudePath: curve }),
    asphaltEdge
  );
  group.add(curbL);
  const curbShapeR = new THREE.Shape();
  curbShapeR.moveTo(hw - 0.15, 0.28);
  curbShapeR.lineTo(hw + 0.35, 0.28);
  curbShapeR.lineTo(hw + 0.35, 0.48);
  curbShapeR.lineTo(hw - 0.15, 0.48);
  curbShapeR.closePath();
  group.add(new THREE.Mesh(
    new THREE.ExtrudeGeometry(curbShapeR, { steps: 200, bevelEnabled: false, extrudePath: curve }),
    asphaltEdge
  ));

  // Center dashed line (narrow extrusion)
  const dashShape = new THREE.Shape();
  dashShape.moveTo(-0.12, 0.33);
  dashShape.lineTo(0.12, 0.33);
  dashShape.lineTo(0.12, 0.38);
  dashShape.lineTo(-0.12, 0.38);
  dashShape.closePath();
  const dashes = new THREE.Mesh(
    new THREE.ExtrudeGeometry(dashShape, { steps: 120, bevelEnabled: false, extrudePath: curve }),
    stripe
  );
  group.add(dashes);

  // —— FEATURE décor (not the driving surface) ——
  // Banked turn décor (outer walls along high-curvature samples)
  addBankRails(group, curve, wallMat);
  // Continuous side rails with intentional fall-off gaps
  const railMask = addSideRails(group, curve);

  // Loop-like arch at east tip (visual)
  const east = curve.getPoint(0.02);
  const arch = new THREE.Mesh(new THREE.TorusGeometry(5.2, 0.55, 12, 32, Math.PI * 1.15), accent);
  arch.position.set(east.x, east.y + 2.2, east.z);
  arch.rotation.z = Math.PI / 2;
  arch.castShadow = true;
  group.add(arch);

  // Figure-eight crossing: tunnel shell under low pass + bridge rails on high pass
  const crossLow = curve.getPointAt(0.75); // ~3π/2 region
  const crossHigh = curve.getPointAt(0.25); // ~π/2 region
  // Tunnel portal
  const tun = new THREE.Mesh(new THREE.BoxGeometry(10, 3.2, 12), wallMat);
  tun.position.set(crossLow.x, 1.2, crossLow.z);
  group.add(tun);
  const tunHole = new THREE.Mesh(
    new THREE.BoxGeometry(8, 2.4, 13),
    new THREE.MeshStandardMaterial({ color: 0x1a1e28, roughness: 1, emissive: 0x0a0c12, emissiveIntensity: 0.1 })
  );
  tunHole.position.set(crossLow.x, 0.9, crossLow.z);
  group.add(tunHole);

  // Bridge side rails near high crossing
  for (const side of [-1, 1]) {
    const rail = new THREE.Mesh(new THREE.BoxGeometry(1.0, 1.2, 14), wallMat);
    rail.position.set(crossHigh.x + side * 5.2, crossHigh.y + 0.9, crossHigh.z);
    group.add(rail);
  }

  // Soft outer containment ring
  const ring = new THREE.Mesh(
    new THREE.TorusGeometry(48, 0.7, 8, 64),
    wallMat
  );
  ring.rotation.x = Math.PI / 2;
  ring.position.y = 0.6;
  group.add(ring);

  // Start/finish markers near t≈0.92 (west approach)
  const sf = curve.getPointAt(0.92);
  const st = curve.getTangentAt(0.92);
  for (let i = 0; i < 8; i++) {
    const c = new THREE.Mesh(
      new THREE.BoxGeometry(1.3, 0.1, 1.3),
      i % 2 ? stripe : asphaltEdge
    );
    const side = (i - 3.5) * 1.35;
    // perpendicular in XZ
    const px = -st.z, pz = st.x;
    const plen = Math.hypot(px, pz) || 1;
    c.position.set(sf.x + (px / plen) * side, sf.y + 0.4, sf.z + (pz / plen) * side);
    group.add(c);
  }

  scene.add(group);

  // Checkpoints evenly along curve
  const checkpoints = [];
  const cpCount = 16;
  for (let i = 0; i < cpCount; i++) {
    const u = (i + 0.5) / cpCount;
    const p = curve.getPointAt(u);
    checkpoints.push({ x: p.x, z: p.z, y: p.y, r: 7.5, u });
  }

  // Start grid just before finish (u≈0.90–0.94), facing along tangent
  const startSpots = [];
  for (let i = 0; i < 4; i++) {
    const u = 0.905 + (i % 2) * 0.012;
    const p = curve.getPointAt(u);
    const tan = curve.getTangentAt(u).normalize();
    const yaw = Math.atan2(tan.z, tan.x);
    const nx = -tan.z, nz = tan.x;
    const lane = (i < 2 ? -1.6 : 1.6) + (i % 2) * 0.15;
    startSpots.push({
      x: p.x + nx * lane,
      z: p.z + nz * lane,
      y: p.y,
      yaw,
    });
  }

  return {
    group,
    curve,
    checkpoints,
    startSpots,
    roadHalfW: ROAD_HALF_W,
    railMask,
    ramps: [], // height from curve now
  };
}

function addBankRails(group, curve, wallMat) {
  // Sample curve; place short wall segments on the outside of bends
  const n = 64;
  for (let i = 0; i < n; i++) {
    const u0 = i / n;
    const u1 = (i + 1) / n;
    const p0 = curve.getPointAt(u0);
    const p1 = curve.getPointAt(u1);
    const t0 = curve.getTangentAt(u0);
    const t1 = curve.getTangentAt(u1);
    // Curvature proxy: tangent change
    const bend = 1 - Math.max(-1, Math.min(1, t0.dot(t1)));
    if (bend < 0.04) continue;
    const mid = p0.clone().lerp(p1, 0.5);
    const tan = t0.clone().add(t1).normalize();
    const nx = -tan.z, nz = tan.x;
    const len = p0.distanceTo(p1);
    // Outer side (sign from approx curvature using 2D cross)
    const cross = t0.x * t1.z - t0.z * t1.x;
    const sign = cross >= 0 ? 1 : -1;
    const wall = new THREE.Mesh(
      new THREE.BoxGeometry(Math.max(1.2, len * 1.05), 1.6 + bend * 4, 0.55),
      wallMat
    );
    wall.position.set(
      mid.x + nx * sign * (ROAD_HALF_W + 0.6),
      mid.y + 0.9 + bend * 1.2,
      mid.z + nz * sign * (ROAD_HALF_W + 0.6)
    );
    wall.rotation.y = Math.atan2(tan.x, tan.z);
    // Bank tilt
    wall.rotation.z = sign * -0.35 * Math.min(1, bend * 3);
    wall.castShadow = true;
    group.add(wall);
  }
}

/** Glow-metal side rails along curb edges; gaps leave fall-off danger. */
function addSideRails(group, curve) {
  const railMat = new THREE.MeshStandardMaterial({
    color: 0xd0dae8, roughness: 0.32, metalness: 0.8,
    emissive: 0x60a5fa, emissiveIntensity: 0.32,
  });
  const n = 120;
  const mask = new Array(n);
  for (let i = 0; i < n; i++) {
    const u = i / n;
    const outer = bendOuterSign(curve, u);
    const pres = railPresence(u, outer);
    mask[i] = pres;
    const p = curve.getPointAt(u);
    const tan = curve.getTangentAt(u).normalize();
    const nx = -tan.z, nz = tan.x;
    const len = curve.getPointAt((i + 1) / n).distanceTo(p) * 1.05;
    for (const side of [-1, 1]) {
      const on = side < 0 ? pres.left : pres.right;
      if (!on) continue;
      const tube = new THREE.Mesh(
        new THREE.CylinderGeometry(0.12, 0.12, Math.max(0.8, len), 6),
        railMat
      );
      // Cylinder default along Y — lay along tangent in XZ
      tube.rotation.z = Math.PI / 2;
      tube.rotation.y = Math.atan2(tan.z, tan.x);
      tube.position.set(
        p.x + nx * side * RAIL_OFFSET,
        p.y + 0.55,
        p.z + nz * side * RAIL_OFFSET
      );
      tube.castShadow = true;
      group.add(tube);
      // Low glow wall under tube for readability
      const wall = new THREE.Mesh(
        new THREE.BoxGeometry(Math.max(0.8, len), 0.55, 0.18),
        railMat
      );
      wall.position.set(
        p.x + nx * side * RAIL_OFFSET,
        p.y + 0.28,
        p.z + nz * side * RAIL_OFFSET
      );
      wall.rotation.y = Math.atan2(tan.x, tan.z);
      group.add(wall);
    }
  }
  return { n, samples: mask };
}

/** Nearest curve sample: lateral offset (+ = right of tangent), u, point, tangent. */
export function nearestOnTrack(curve, x, z) {
  let bestD = Infinity;
  let bestU = 0;
  let bestP = null;
  const coarse = 90;
  for (let i = 0; i <= coarse; i++) {
    const u = i / coarse;
    const p = curve.getPointAt(u);
    const d = Math.hypot(p.x - x, p.z - z);
    if (d < bestD) { bestD = d; bestU = u; bestP = p; }
  }
  const refine = 14;
  for (let i = -refine; i <= refine; i++) {
    let u = bestU + i / (coarse * refine);
    u = ((u % 1) + 1) % 1;
    const p = curve.getPointAt(u);
    const d = Math.hypot(p.x - x, p.z - z);
    if (d < bestD) { bestD = d; bestU = u; bestP = p; }
  }
  const tan = curve.getTangentAt(bestU).normalize();
  const nx = -tan.z, nz = tan.x;
  const lat = (x - bestP.x) * nx + (z - bestP.z) * nz;
  return { u: bestU, p: bestP, tan, nx, nz, lat, dist: bestD };
}

/** Height from nearest point on continuous road curve. */
export function sampleTrackHeight(curve, x, z, roadHalfW, carHalfY) {
  if (!curve) return carHalfY;
  let bestD = Infinity;
  let bestY = 0;
  let bestU = 0;
  // Coarse then refine
  const coarse = 80;
  for (let i = 0; i <= coarse; i++) {
    const u = i / coarse;
    const p = curve.getPointAt(u);
    const d = Math.hypot(p.x - x, p.z - z);
    if (d < bestD) {
      bestD = d;
      bestY = p.y;
      bestU = u;
    }
  }
  const refine = 12;
  for (let i = -refine; i <= refine; i++) {
    let u = bestU + i / (coarse * refine);
    u = ((u % 1) + 1) % 1;
    const p = curve.getPointAt(u);
    const d = Math.hypot(p.x - x, p.z - z);
    if (d < bestD) {
      bestD = d;
      bestY = p.y;
    }
  }
  if (bestD > roadHalfW + 1.8) {
    // Off track — soft grass
    return carHalfY;
  }
  return bestY + carHalfY * 0.35;
}

/** @deprecated kept for API compat — unused when curve present */
export function sampleRampY(ramps, x, z, carHalfY) {
  return carHalfY;
}

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
      cp: 0,
      lap: 0,
      finished: false,
      finishOrder: -1,
      progress: 0,
    })),
  };
}

export function raceProgress(racer, checkpoints) {
  return racer.lap * checkpoints.length + racer.cp + 0.01;
}

export function updateRaceProgress(car, racer, checkpoints, totalLaps) {
  if (racer.finished) return false;
  const cp = checkpoints[racer.cp];
  const d = Math.hypot(car.pos.x - cp.x, car.pos.z - cp.z);
  if (d < cp.r) {
    racer.cp++;
    if (racer.cp >= checkpoints.length) {
      racer.cp = 0;
      racer.lap++;
      if (racer.lap >= totalLaps) {
        racer.finished = true;
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

export function boundRaceCar(car, carHalfY, raceMeta) {
  const maxR = 50;
  if (Math.hypot(car.pos.x, car.pos.z) > maxR) {
    const ang = Math.atan2(car.pos.z, car.pos.x);
    car.pos.x = Math.cos(ang) * maxR;
    car.pos.z = Math.sin(ang) * maxR;
    car.vx *= -0.3;
    car.vz *= -0.3;
  }

  const curve = raceMeta && raceMeta.curve;
  const hw = (raceMeta && raceMeta.roadHalfW) || ROAD_HALF_W;

  // Side-rail redirect: bounce inward, preserve speed (no friction drain)
  if (curve) {
    const hit = nearestOnTrack(curve, car.pos.x, car.pos.z);
    const outer = bendOuterSign(curve, hit.u);
    const pres = railPresence(hit.u, outer);
    const limit = hw + 0.15;
    const onRight = hit.lat > 0;
    const blocked = onRight ? pres.right : pres.left;
    if (blocked && Math.abs(hit.lat) > limit && hit.dist < hw + 3.5) {
      const sign = onRight ? 1 : -1;
      // Push back onto road
      const over = Math.abs(hit.lat) - limit;
      car.pos.x -= hit.nx * sign * over;
      car.pos.z -= hit.nz * sign * over;
      // Inward normal (toward centerline)
      const inx = -hit.nx * sign;
      const inz = -hit.nz * sign;
      const sp = Math.hypot(car.vx, car.vz);
      const vn = car.vx * inx + car.vz * inz;
      if (vn < 0) {
        // Reflect against inward normal, keep magnitude
        car.vx -= 2 * vn * inx;
        car.vz -= 2 * vn * inz;
        const sp2 = Math.hypot(car.vx, car.vz);
        if (sp2 > 1e-4 && sp > 1e-4) {
          car.vx = (car.vx / sp2) * sp;
          car.vz = (car.vz / sp2) * sp;
        }
      } else if (sp > 0.5) {
        // Sliding along rail — nudge inward without slowdown
        car.vx += inx * 0.8;
        car.vz += inz * 0.8;
        const sp3 = Math.hypot(car.vx, car.vz);
        if (sp3 > 1e-4) {
          car.vx = (car.vx / sp3) * sp;
          car.vz = (car.vz / sp3) * sp;
        }
      }
    }
  }

  const groundY = curve
    ? sampleTrackHeight(curve, car.pos.x, car.pos.z, hw, carHalfY)
    : carHalfY;

  if (car.pos.y <= groundY + 0.1) {
    const prevY = car.pos.y;
    if (car.vy < 0) car.vy = 0;
    car.pos.y = Math.max(carHalfY * 0.85, groundY);
    car.onGround = true;
    // Launch off steep crests (jump / loop)
    const rise = groundY - prevY;
    const sp = Math.hypot(car.vx, car.vz);
    if (rise > 0.15 && sp > 8 && groundY > carHalfY + 2) {
      car.vy = Math.max(car.vy, 4 + rise * 8);
      car.onGround = false;
    } else if (groundY > carHalfY + 3.5 && sp > 10) {
      car.vy = Math.max(car.vy, 6);
      car.onGround = false;
    }
  }
  if (car.pos.y > 18) {
    car.pos.y = 18;
    car.vy *= -0.2;
  }
  // Soft floor if somehow underground off-track
  if (car.pos.y < carHalfY * 0.5) {
    car.pos.y = carHalfY;
    if (car.vy < 0) car.vy = 0;
  }
}

export function raceCameraTarget(cars) {
  const n = cars.length || 1;
  let x = 0, z = 0, y = 0;
  for (const c of cars) {
    x += c.pos.x;
    z += c.pos.z;
    y += c.pos.y;
  }
  return { x: x / n, y: y / n, z: z / n };
}

/** @deprecated chase cam — race uses raceArenaFocus (overview). Kept for import compat. */
export function raceChaseFocus(cars, preferIndex = 0) {
  return raceArenaFocus(cars);
}

/**
 * Elevated arena overview: pack centroid + span so EVERY car stays framed.
 * Caller places camera high and pulls out with `span`.
 */
export function raceArenaFocus(cars) {
  if (!cars || !cars.length) {
    return { x: 0, y: 2, z: 0, span: 42, minX: -30, maxX: 30, minZ: -24, maxZ: 24 };
  }
  let sx = 0, sy = 0, sz = 0;
  let minX = Infinity, maxX = -Infinity, minZ = Infinity, maxZ = -Infinity;
  let maxY = 0;
  for (const c of cars) {
    sx += c.pos.x; sy += c.pos.y; sz += c.pos.z;
    if (c.pos.x < minX) minX = c.pos.x;
    if (c.pos.x > maxX) maxX = c.pos.x;
    if (c.pos.z < minZ) minZ = c.pos.z;
    if (c.pos.z > maxZ) maxZ = c.pos.z;
    if (c.pos.y > maxY) maxY = c.pos.y;
  }
  const n = cars.length;
  // Pad so cars near edges stay on-screen; also keep a minimum track footprint
  const pad = 14;
  minX = Math.min(minX - pad, -36);
  maxX = Math.max(maxX + pad, 36);
  minZ = Math.min(minZ - pad, -28);
  maxZ = Math.max(maxZ + pad, 28);
  const spanX = maxX - minX;
  const spanZ = maxZ - minZ;
  const span = Math.max(spanX, spanZ, 40);
  return {
    x: sx / n,
    y: sy / n,
    z: sz / n,
    span,
    maxY,
    minX, maxX, minZ, maxZ,
  };
}
