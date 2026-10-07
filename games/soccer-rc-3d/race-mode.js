/**
 * Race Track mode for Soccer RC 3D — closed loop, checkpoints, laps, 2–4 cars.
 * Shares car mesh/feel with the main game; bounds/camera are race-specific.
 */
import * as THREE from "three";

export const RACE_LAPS_DEFAULT = 3;
export const RACE_COLORS = [0xe4572e, 0x3b82f6, 0x34d399, 0xc084fc];
export const RACE_NAMES = ["Orange", "Blue", "Green", "Purple"];

/** Build oval track with inner island, outer wall, two ramps, one bridge. */
export function buildRaceTrack(scene) {
  const group = new THREE.Group();
  group.name = "raceTrack";

  const asphalt = new THREE.MeshStandardMaterial({ color: 0x2a2e36, roughness: 0.92, metalness: 0.08 });
  const curb = new THREE.MeshStandardMaterial({ color: 0xe4a23a, roughness: 0.55, metalness: 0.1 });
  const wallMat = new THREE.MeshStandardMaterial({ color: 0x1e2430, roughness: 0.85, metalness: 0.15 });
  const grass = new THREE.MeshStandardMaterial({ color: 0x1a3a28, roughness: 0.95, metalness: 0.02 });
  const stripe = new THREE.MeshBasicMaterial({ color: 0xf4efe6 });
  const rampMat = new THREE.MeshStandardMaterial({ color: 0x3d4450, roughness: 0.7, metalness: 0.2 });

  // Ground bowl
  const ground = new THREE.Mesh(new THREE.BoxGeometry(90, 0.4, 60), grass);
  ground.position.y = -0.35;
  ground.receiveShadow = true;
  group.add(ground);

  // Main oval deck (flat)
  const deck = new THREE.Mesh(new THREE.BoxGeometry(70, 0.3, 42), asphalt);
  deck.position.y = -0.05;
  deck.receiveShadow = true;
  group.add(deck);

  // Center island
  const island = new THREE.Mesh(new THREE.BoxGeometry(28, 1.2, 14), grass);
  island.position.y = 0.4;
  island.castShadow = true;
  group.add(island);
  const islandWall = new THREE.Mesh(new THREE.BoxGeometry(30, 1.6, 16), wallMat);
  islandWall.position.y = 0.5;
  group.add(islandWall);

  // Outer walls (segmented rectangle oval-ish)
  function addWall(w, d, x, z, y = 0.9) {
    const m = new THREE.Mesh(new THREE.BoxGeometry(w, 1.8, d), wallMat);
    m.position.set(x, y, z);
    m.castShadow = true;
    group.add(m);
  }
  addWall(74, 1.2, 0, -22); // south
  addWall(74, 1.2, 0, 22);  // north
  addWall(1.2, 46, -36, 0); // west
  addWall(1.2, 46, 36, 0);  // east

  // Curb stripes on start/finish (x≈-20)
  for (let i = 0; i < 10; i++) {
    const s = new THREE.Mesh(new THREE.BoxGeometry(1.4, 0.06, 1.4), i % 2 ? stripe : curb);
    s.position.set(-22, 0.12, -9 + i * 2);
    group.add(s);
  }
  const finish = new THREE.Mesh(new THREE.BoxGeometry(0.2, 0.08, 18), stripe);
  finish.position.set(-22, 0.14, 0);
  group.add(finish);

  // Ramp A (south stretch) — jump toward +X
  const rampA = makeRamp(THREE, rampMat, 10, 3.2, 6);
  rampA.position.set(8, 0, -16);
  group.add(rampA);
  // Landing pad
  const landA = new THREE.Mesh(new THREE.BoxGeometry(8, 0.5, 7), asphalt);
  landA.position.set(22, 0.9, -16);
  landA.receiveShadow = true;
  group.add(landA);
  const landADrop = new THREE.Mesh(new THREE.BoxGeometry(6, 0.4, 7), rampMat);
  landADrop.position.set(28, 0.35, -16);
  landADrop.rotation.z = -0.35;
  group.add(landADrop);

  // Ramp B (north) opposite direction
  const rampB = makeRamp(THREE, rampMat, 10, 3.2, 6);
  rampB.rotation.y = Math.PI;
  rampB.position.set(-8, 0, 16);
  group.add(rampB);
  const landB = new THREE.Mesh(new THREE.BoxGeometry(8, 0.5, 7), asphalt);
  landB.position.set(-22, 0.9, 16);
  group.add(landB);

  // Bridge over center gap (east side)
  const bridge = new THREE.Mesh(new THREE.BoxGeometry(8, 0.45, 18), asphalt);
  bridge.position.set(30, 2.2, 0);
  bridge.castShadow = true;
  bridge.receiveShadow = true;
  group.add(bridge);
  // Bridge approaches
  const br1 = makeRamp(THREE, rampMat, 9, 2.4, 5);
  br1.position.set(30, 0, -12);
  br1.rotation.y = Math.PI / 2;
  group.add(br1);
  const br2 = makeRamp(THREE, rampMat, 9, 2.4, 5);
  br2.position.set(30, 0, 12);
  br2.rotation.y = -Math.PI / 2;
  group.add(br2);
  // Bridge rails
  addWall(0.4, 18, 26.5, 0, 2.8);
  addWall(0.4, 18, 33.5, 0, 2.8);

  // Decorative boost pads (visual only — boost still from input)
  const boostMat = new THREE.MeshBasicMaterial({ color: 0x60a5fa, transparent: true, opacity: 0.35 });
  for (const [x, z] of [[-10, -16], [10, 16], [0, -16]]) {
    const b = new THREE.Mesh(new THREE.BoxGeometry(4, 0.05, 3), boostMat);
    b.position.set(x, 0.12, z);
    group.add(b);
  }

  scene.add(group);

  // Checkpoints clockwise starting just after finish (traveling +Z then around)
  // Track flow: start facing +Z at x=-22, go north, east, south, west, back
  const checkpoints = [
    { x: -22, z: 12, r: 6 },   // 0 after start going north
    { x: -8, z: 18, r: 7 },    // 1 north stretch
    { x: 18, z: 16, r: 7 },    // 2 NE
    { x: 30, z: 0, r: 7 },     // 3 bridge
    { x: 18, z: -16, r: 7 },   // 4 SE / jump
    { x: -8, z: -18, r: 7 },   // 5 south
    { x: -28, z: -8, r: 7 },   // 6 SW
    { x: -22, z: -2, r: 5 },   // 7 toward finish
  ];

  // Grid start spots facing +Z (north), left of finish line
  const startSpots = [
    { x: -24, z: -6, yaw: Math.PI / 2 },  // yaw: cos=0 sin=1 → +Z
    { x: -20, z: -6, yaw: Math.PI / 2 },
    { x: -24, z: -10, yaw: Math.PI / 2 },
    { x: -20, z: -10, yaw: Math.PI / 2 },
  ];

  // Ramp volumes for simple height sampling [minX,maxX,minZ,maxZ, baseY, peakY, dir]
  const ramps = [
    { minX: 3, maxX: 18, minZ: -19.5, maxZ: -12.5, y0: 0, y1: 2.4, axis: "x", a0: 3, a1: 18 },
    { minX: -18, maxX: -3, minZ: 12.5, maxZ: 19.5, y0: 0, y1: 2.4, axis: "x", a0: -3, a1: -18 },
    { minX: 25, maxX: 35, minZ: -16, maxZ: -7, y0: 0, y1: 2.2, axis: "z", a0: -16, a1: -7 },
    { minX: 25, maxX: 35, minZ: 7, maxZ: 16, y0: 0, y1: 2.2, axis: "z", a0: 16, a1: 7 },
    { minX: 26, maxX: 34, minZ: -9, maxZ: 9, y0: 2.0, y1: 2.2, axis: "flat", a0: 0, a1: 1 }, // bridge top
  ];

  return { group, checkpoints, startSpots, ramps };
}

function makeRamp(THREE, mat, len, height, width) {
  // Wedge along +X: low at -len/2, high at +len/2
  const shape = new THREE.Shape();
  shape.moveTo(-len / 2, 0);
  shape.lineTo(len / 2, 0);
  shape.lineTo(len / 2, 0.05);
  shape.lineTo(-len / 2, height);
  shape.closePath();
  const geo = new THREE.ExtrudeGeometry(shape, { depth: width, bevelEnabled: false });
  geo.rotateX(-Math.PI / 2);
  geo.translate(0, 0, -width / 2);
  const m = new THREE.Mesh(geo, mat);
  m.castShadow = true;
  m.receiveShadow = true;
  return m;
}

export function sampleRampY(ramps, x, z, carHalfY) {
  let best = carHalfY;
  for (const r of ramps) {
    if (x < r.minX || x > r.maxX || z < r.minZ || z > r.maxZ) continue;
    let t = 0;
    if (r.axis === "flat") {
      best = Math.max(best, r.y1 + carHalfY);
      continue;
    }
    const a = r.axis === "x" ? x : z;
    const span = r.a1 - r.a0;
    if (Math.abs(span) < 1e-3) continue;
    t = (a - r.a0) / span;
    t = Math.max(0, Math.min(1, t));
    const y = r.y0 + (r.y1 - r.y0) * t + carHalfY;
    if (y > best) best = y;
  }
  return best;
}

export function createRaceState(numCars, laps) {
  const n = Math.max(2, Math.min(4, numCars | 0));
  return {
    numCars: n,
    laps: laps || RACE_LAPS_DEFAULT,
    started: false,
    finished: false,
    countdown: 3.2,
    places: [],
    racers: Array.from({ length: n }, (_, i) => ({
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

/** Advance checkpoints / laps for one car. Returns true if just finished race. */
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

export function rankRacers(raceState, checkpoints) {
  const idx = raceState.racers.map((r, i) => i);
  idx.sort((a, b) => {
    const ra = raceState.racers[a], rb = raceState.racers[b];
    if (ra.finished && rb.finished) return ra.finishOrder - rb.finishOrder;
    if (ra.finished) return -1;
    if (rb.finished) return 1;
    return rb.progress - ra.progress;
  });
  return idx;
}

export function boundRaceCar(car, carHalfY, ramps, sampleY) {
  // Soft outer box
  const maxX = 34.5, maxZ = 20.5;
  if (car.pos.x > maxX) { car.pos.x = maxX; car.vx *= -0.3; }
  if (car.pos.x < -maxX) { car.pos.x = -maxX; car.vx *= -0.3; }
  if (car.pos.z > maxZ) { car.pos.z = maxZ; car.vz *= -0.3; }
  if (car.pos.z < -maxZ) { car.pos.z = -maxZ; car.vz *= -0.3; }

  // Center island soft bounce
  if (Math.abs(car.pos.x) < 15.5 && Math.abs(car.pos.z) < 8.2 && car.pos.y < 2) {
    const ox = Math.abs(car.pos.x) / 15.5;
    const oz = Math.abs(car.pos.z) / 8.2;
    if (ox > oz) {
      car.pos.x = Math.sign(car.pos.x || 1) * 15.5;
      car.vx *= -0.35;
    } else {
      car.pos.z = Math.sign(car.pos.z || 1) * 8.2;
      car.vz *= -0.35;
    }
  }

  const groundY = sampleY(ramps, car.pos.x, car.pos.z, carHalfY);
  if (car.pos.y <= groundY + 0.05) {
    if (car.vy < 0) car.vy = 0;
    car.pos.y = groundY;
    car.onGround = true;
    // Slight launch at ramp crest
    if (groundY > carHalfY + 1.5 && Math.hypot(car.vx, car.vz) > 10) {
      car.vy = Math.max(car.vy, 4.5);
      car.onGround = false;
    }
  }
  if (car.pos.y > 12) {
    car.pos.y = 12;
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
