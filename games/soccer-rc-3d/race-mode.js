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

  const asphalt = new THREE.MeshStandardMaterial({
    color: 0x2c313a, roughness: 0.88, metalness: 0.12,
  });
  const asphaltEdge = new THREE.MeshStandardMaterial({
    color: 0xe4a23a, roughness: 0.55, metalness: 0.15,
  });
  const grass = new THREE.MeshStandardMaterial({ color: 0x143224, roughness: 0.95, metalness: 0.02 });
  const wallMat = new THREE.MeshStandardMaterial({ color: 0x1a1f28, roughness: 0.85, metalness: 0.2 });
  const accent = new THREE.MeshStandardMaterial({
    color: 0x60a5fa, roughness: 0.45, metalness: 0.3, emissive: 0x1e40af, emissiveIntensity: 0.2,
  });
  const stripe = new THREE.MeshBasicMaterial({ color: 0xf4efe6 });

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
  // Banked turn rails (outer walls along high-curvature samples)
  addBankRails(group, curve, wallMat);

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
    new THREE.MeshStandardMaterial({ color: 0x07090c, roughness: 1 })
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

/** Chase cam focus: prefer P1, blend pack if close. */
export function raceChaseFocus(cars, preferIndex = 0) {
  if (!cars.length) return { x: 0, y: 0, z: 0, sp: 0, yaw: 0 };
  const main = cars[Math.min(preferIndex, cars.length - 1)];
  const sp = Math.hypot(main.vx, main.vz);
  // Mild blend toward nearby cars so pack stays framed
  let bx = main.pos.x, by = main.pos.y, bz = main.pos.z;
  let w = 1;
  for (let i = 0; i < cars.length; i++) {
    if (i === preferIndex) continue;
    const d = main.pos.distanceTo(cars[i].pos);
    if (d < 18) {
      const k = (1 - d / 18) * 0.35;
      bx += cars[i].pos.x * k;
      by += cars[i].pos.y * k;
      bz += cars[i].pos.z * k;
      w += k;
    }
  }
  return {
    x: bx / w,
    y: by / w,
    z: bz / w,
    sp,
    yaw: main.yaw,
    vx: main.vx,
    vz: main.vz,
  };
}
