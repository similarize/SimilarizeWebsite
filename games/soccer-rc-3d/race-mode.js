/**
 * Race Track mode — figure-eight adventure circuit (not a flat oval).
 * Five set-piece features: banked turns, jumps, loop-like ramp, figure-eight
 * crossing (bridge over tunnel), and elevation twists.
 */
import * as THREE from "three";

export const RACE_LAPS_DEFAULT = 3;
export const RACE_COLORS = [0xe4572e, 0x3b82f6, 0x34d399, 0xc084fc];
export const RACE_NAMES = ["Orange", "Blue", "Green", "Purple"];

const asphalt = () => new THREE.MeshStandardMaterial({ color: 0x2a2e36, roughness: 0.9, metalness: 0.08 });
const asphaltLite = () => new THREE.MeshStandardMaterial({ color: 0x353b46, roughness: 0.85, metalness: 0.1 });
const curb = () => new THREE.MeshStandardMaterial({ color: 0xe4a23a, roughness: 0.55, metalness: 0.1 });
const wallMat = () => new THREE.MeshStandardMaterial({ color: 0x1e2430, roughness: 0.85, metalness: 0.15 });
const grass = () => new THREE.MeshStandardMaterial({ color: 0x163528, roughness: 0.95, metalness: 0.02 });
const stripe = () => new THREE.MeshBasicMaterial({ color: 0xf4efe6 });
const rampMat = () => new THREE.MeshStandardMaterial({ color: 0x3d4450, roughness: 0.7, metalness: 0.2 });
const accentBlue = () => new THREE.MeshStandardMaterial({ color: 0x3b82f6, roughness: 0.5, metalness: 0.25, emissive: 0x1e3a8a, emissiveIntensity: 0.15 });

function addBox(group, mat, w, h, d, x, y, z, rx = 0, ry = 0, rz = 0) {
  const m = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), mat);
  m.position.set(x, y, z);
  m.rotation.set(rx, ry, rz);
  m.castShadow = true;
  m.receiveShadow = true;
  group.add(m);
  return m;
}

/** Road plank centered on (x,z), facing yaw (0 = +X). */
function addRoad(group, mat, len, width, x, y, z, yaw, thick = 0.35) {
  const m = addBox(group, mat, len, thick, width, x, y, z, 0, yaw, 0);
  return m;
}

/**
 * Build adventure figure-eight.
 * Layout (top-down): left lobe ← crossing → right lobe
 * Drive CCW on left, through tunnel, CW on right, over bridge, home.
 */
export function buildRaceTrack(scene) {
  const group = new THREE.Group();
  group.name = "raceTrack";
  const matA = asphalt();
  const matA2 = asphaltLite();
  const matCurb = curb();
  const matWall = wallMat();
  const matGrass = grass();
  const matStripe = stripe();
  const matRamp = rampMat();
  const matAccent = accentBlue();

  // Big grass bowl
  addBox(group, matGrass, 120, 0.5, 80, 0, -0.6, 0);

  // ---- Figure-eight road ribbon (segment list) ----
  // Each: [x, z, yaw, len, width, y]
  const roads = [
    // Start/finish straight (west of left lobe), facing +Z into north bank
    [-26, -4, Math.PI / 2, 14, 8, 0.1],
    [-26, 8, Math.PI / 2, 12, 8, 0.15],
    // FEATURE 1 — banked north turn (left lobe) climbing
    [-20, 18, Math.PI / 4, 12, 9, 0.5],
    [-10, 22, 0, 12, 9, 1.0],
    [0, 20, -Math.PI / 6, 12, 8.5, 1.4],
    // Drop toward crossing
    [8, 14, -Math.PI / 2.5, 12, 8, 0.9],
    [12, 6, -Math.PI / 2, 10, 8, 0.35],
    // FEATURE 5 twist + FEATURE 4 lower path into TUNNEL under crossing
    [12, -2, -Math.PI / 2, 8, 7.5, 0.15],
    // east of crossing (still low)
    [12, -10, -Math.PI / 2, 10, 8, 0.2],
    // FEATURE 2 — jump setup (south-east)
    [18, -16, -Math.PI / 8, 10, 8, 0.4],
    // Right lobe south banked
    [28, -18, Math.PI / 8, 12, 9, 0.8],
    [36, -10, Math.PI / 2, 12, 9, 1.2],
    // FEATURE 3 — loop-like ramp climb (east)
    [36, 2, Math.PI / 2, 10, 8, 1.6],
    // Right lobe north
    [30, 16, Math.PI, 12, 9, 1.0],
    [18, 18, -Math.PI * 0.75, 12, 8.5, 1.5],
    // Climb to BRIDGE over crossing (figure-eight upper)
    [10, 10, -Math.PI / 2, 10, 8, 2.2],
    [10, 2, -Math.PI / 2, 8, 8, 3.0],
    // Bridge deck across center
    [10, -4, -Math.PI / 2, 10, 8, 3.2],
    // Descend west back toward start
    [2, -10, -Math.PI * 0.6, 12, 8, 2.0],
    [-10, -14, -Math.PI * 0.85, 12, 8, 1.0],
    [-20, -12, Math.PI, 10, 8, 0.4],
    [-26, -10, Math.PI / 2, 8, 8, 0.15],
  ];

  for (const [x, z, yaw, len, width, y] of roads) {
    addRoad(group, matA, len, width, x, y, z, yaw);
  }

  // ---- FEATURE 1: Banked turns (tilted outer walls + raised outer curb) ----
  // Left lobe north bank
  addBox(group, matWall, 14, 2.4, 1.2, -14, 1.6, 24, 0, 0.2, 0.45);
  addBox(group, matCurb, 14, 0.5, 1.5, -14, 0.9, 23.2, 0, 0.15, 0.55);
  // Right lobe SE bank
  addBox(group, matWall, 1.2, 2.6, 14, 40, 1.8, -12, 0, 0, -0.5);
  addBox(group, matCurb, 1.5, 0.55, 14, 38.8, 1.0, -12, 0, 0, -0.55);
  // Right lobe north bank
  addBox(group, matWall, 14, 2.2, 1.2, 26, 1.7, 22, 0, -0.25, -0.4);

  // ---- FEATURE 2: Jumps (gap with launch + landing) ----
  // Launch wedge (visual)
  const jumpLaunch = makeWedge(matRamp, 9, 2.8, 7);
  jumpLaunch.position.set(22, 0.05, -16);
  jumpLaunch.rotation.y = -0.15;
  group.add(jumpLaunch);
  // Landing pad
  addBox(group, matA2, 9, 0.5, 8, 34, 1.4, -14);
  const jumpDown = makeWedge(matRamp, 7, 1.6, 7);
  jumpDown.rotation.y = Math.PI;
  jumpDown.position.set(40, 0.05, -12);
  group.add(jumpDown);
  // Chevrons
  for (let i = 0; i < 4; i++) {
    addBox(group, matStripe, 1.2, 0.08, 0.5, 18 + i * 1.1, 0.55 + i * 0.35, -16.5);
  }

  // ---- FEATURE 3: Loop-like ramp (tall arch / half-pipe portal) ----
  // Climb into a tall curved arch then drop — looks like a loop entrance
  const loopClimb = makeWedge(matAccent, 12, 5.5, 8);
  loopClimb.position.set(36, 0.1, 6);
  loopClimb.rotation.y = Math.PI / 2;
  group.add(loopClimb);
  // Arch (visual torus segment)
  const arch = new THREE.Mesh(
    new THREE.TorusGeometry(5.5, 1.1, 10, 28, Math.PI),
    matAccent
  );
  arch.rotation.z = Math.PI / 2;
  arch.rotation.y = Math.PI / 2;
  arch.position.set(36, 6.2, 12);
  arch.castShadow = true;
  group.add(arch);
  // High landing shelf after arch
  addBox(group, matA2, 8, 0.5, 10, 36, 4.2, 18);
  const loopDrop = makeWedge(matRamp, 10, 4.0, 8);
  loopDrop.rotation.y = Math.PI;
  loopDrop.position.set(36, 0.1, 26);
  group.add(loopDrop);
  // Glow rings
  const ringGeo = new THREE.TorusGeometry(4.2, 0.15, 8, 24);
  const ringMat = new THREE.MeshBasicMaterial({ color: 0x93c5fd });
  const ring = new THREE.Mesh(ringGeo, ringMat);
  ring.position.set(36, 5.5, 12);
  ring.rotation.y = Math.PI / 2;
  group.add(ring);

  // ---- FEATURE 4: Figure-eight crossing — bridge OVER + tunnel UNDER ----
  // Raised bridge deck (cars coming from east lobe go over)
  addBox(group, matA2, 10, 0.55, 18, 10, 3.35, -2);
  addBox(group, matWall, 0.5, 1.4, 18, 5.2, 4.0, -2);
  addBox(group, matWall, 0.5, 1.4, 18, 14.8, 4.0, -2);
  // Bridge posts
  for (const z of [-8, 0, 6]) {
    addBox(group, matWall, 1.2, 3.4, 1.2, 5.5, 1.5, z);
    addBox(group, matWall, 1.2, 3.4, 1.2, 14.5, 1.5, z);
  }
  // Tunnel under (dark box + opening) for lower lane
  addBox(group, matWall, 12, 2.8, 10, 10, 1.2, -2);
  // Carve visual openings (darker interior slabs)
  const tunMat = new THREE.MeshStandardMaterial({ color: 0x0a0c10, roughness: 1 });
  addBox(group, tunMat, 8, 2.2, 9.2, 10, 1.0, -2);
  // Tunnel mouth frames
  addBox(group, matCurb, 9, 0.4, 0.6, 10, 2.3, 3.2);
  addBox(group, matCurb, 9, 0.4, 0.6, 10, 2.3, -7.2);

  // ---- FEATURE 5: Elevation twists / hills (S-rise before banks) ----
  addBox(group, matA, 10, 0.4, 8, -26, 0.55, 2, 0, Math.PI / 2, 0.12);
  addBox(group, matA, 10, 0.4, 8, 0, 1.6, 18, 0, 0.1, -0.08);
  addBox(group, matA, 10, 0.4, 8, 22, 0.7, 8, 0, Math.PI / 2, 0.15);
  // Twisty berm snakes
  addBox(group, matCurb, 8, 0.7, 1.2, -8, 1.1, 20, 0, 0.4, 0.3);
  addBox(group, matCurb, 8, 0.7, 1.2, 24, 1.3, -6, 0, -0.35, -0.25);

  // Outer soft walls (containment)
  addBox(group, matWall, 100, 2.2, 1.4, 0, 0.9, -32);
  addBox(group, matWall, 100, 2.2, 1.4, 0, 0.9, 36);
  addBox(group, matWall, 1.4, 2.2, 70, -48, 0.9, 2);
  addBox(group, matWall, 1.4, 2.2, 70, 50, 0.9, 2);

  // Start/finish checkers
  for (let i = 0; i < 8; i++) {
    addBox(group, i % 2 ? matStripe : matCurb, 1.5, 0.08, 1.5, -26, 0.32, -8 + i * 1.6);
  }
  addBox(group, matStripe, 0.25, 0.12, 12, -26, 0.35, 0);

  // Center decoration under crossing
  addBox(group, matGrass, 6, 0.8, 6, 10, 0.2, 10);

  scene.add(group);

  // Height volumes for physics (ramps / bridge / loop / hills)
  const ramps = [
    // Start twist hill
    { minX: -30, maxX: -22, minZ: -2, maxZ: 8, y0: 0.15, y1: 0.7, axis: "z", a0: -2, a1: 8 },
    // North bank elevation (left lobe)
    { minX: -24, maxX: -4, minZ: 16, maxZ: 26, y0: 0.4, y1: 1.5, axis: "x", a0: -24, a1: -4 },
    // Approach to tunnel
    { minX: 6, maxX: 16, minZ: -6, maxZ: 8, y0: 0.9, y1: 0.15, axis: "z", a0: 8, a1: -6 },
    // Tunnel floor (low)
    { minX: 5, maxX: 15, minZ: -8, maxZ: 4, y0: 0.15, y1: 0.15, axis: "flat", a0: 0, a1: 1 },
    // Jump launch
    { minX: 16, maxX: 28, minZ: -20, maxZ: -12, y0: 0.2, y1: 2.6, axis: "x", a0: 16, a1: 28 },
    // Jump landing
    { minX: 30, maxX: 38, minZ: -18, maxZ: -10, y0: 1.4, y1: 1.5, axis: "flat", a0: 0, a1: 1 },
    // SE bank climb
    { minX: 32, maxX: 42, minZ: -18, maxZ: -4, y0: 0.8, y1: 1.5, axis: "z", a0: -18, a1: -4 },
    // Loop-like climb
    { minX: 32, maxX: 40, minZ: 2, maxZ: 14, y0: 1.4, y1: 5.5, axis: "z", a0: 2, a1: 14 },
    // Loop shelf
    { minX: 32, maxX: 40, minZ: 14, maxZ: 22, y0: 4.2, y1: 4.3, axis: "flat", a0: 0, a1: 1 },
    // Loop drop
    { minX: 32, maxX: 40, minZ: 22, maxZ: 32, y0: 4.0, y1: 0.3, axis: "z", a0: 22, a1: 32 },
    // Climb to bridge
    { minX: 6, maxX: 16, minZ: 4, maxZ: 14, y0: 1.2, y1: 3.2, axis: "z", a0: 14, a1: 4 },
    // Bridge deck (figure-eight upper)
    { minX: 5, maxX: 15, minZ: -10, maxZ: 6, y0: 3.2, y1: 3.35, axis: "flat", a0: 0, a1: 1 },
    // Descend from bridge toward start
    { minX: -4, maxX: 8, minZ: -16, maxZ: -6, y0: 3.0, y1: 1.0, axis: "x", a0: 8, a1: -4 },
    { minX: -22, maxX: -8, minZ: -16, maxZ: -8, y0: 1.0, y1: 0.25, axis: "x", a0: -8, a1: -22 },
  ];

  // Checkpoints along drive order (figure-eight)
  const checkpoints = [
    { x: -26, z: 6, r: 7 },     // 0 leave start
    { x: -14, z: 20, r: 8 },    // 1 banked north
    { x: 6, z: 16, r: 7 },      // 2
    { x: 12, z: 2, r: 6 },      // 3 into tunnel
    { x: 12, z: -8, r: 6 },     // 4 tunnel exit
    { x: 24, z: -16, r: 7 },    // 5 jump
    { x: 36, z: -8, r: 7 },     // 6 SE bank
    { x: 36, z: 10, r: 7 },     // 7 loop climb
    { x: 36, z: 22, r: 7 },     // 8 loop shelf/drop
    { x: 22, z: 16, r: 7 },     // 9 north right lobe
    { x: 10, z: 6, r: 6 },      // 10 climb bridge
    { x: 10, z: -4, r: 6 },     // 11 on bridge
    { x: -8, z: -12, r: 7 },    // 12 descend
    { x: -26, z: -6, r: 6 },    // 13 toward finish
  ];

  // Grid at start facing +Z
  const startSpots = [
    { x: -28, z: -6, yaw: Math.PI / 2 },
    { x: -24, z: -6, yaw: Math.PI / 2 },
    { x: -28, z: -10, yaw: Math.PI / 2 },
    { x: -24, z: -10, yaw: Math.PI / 2 },
  ];

  return { group, checkpoints, startSpots, ramps };
}

function makeWedge(mat, len, height, width) {
  const shape = new THREE.Shape();
  shape.moveTo(-len / 2, 0);
  shape.lineTo(len / 2, 0);
  shape.lineTo(len / 2, 0.08);
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
    if (r.axis === "flat") {
      best = Math.max(best, r.y1 + carHalfY * 0.15);
      continue;
    }
    const a = r.axis === "x" ? x : z;
    const span = r.a1 - r.a0;
    if (Math.abs(span) < 1e-3) continue;
    let t = (a - r.a0) / span;
    t = Math.max(0, Math.min(1, t));
    const y = r.y0 + (r.y1 - r.y0) * t + carHalfY * 0.2;
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

export function rankRacers(raceState, checkpoints) {
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

export function boundRaceCar(car, carHalfY, ramps, sampleY) {
  const maxX = 48, maxZ = 34;
  if (car.pos.x > maxX) { car.pos.x = maxX; car.vx *= -0.35; }
  if (car.pos.x < -maxX) { car.pos.x = -maxX; car.vx *= -0.35; }
  if (car.pos.z > maxZ) { car.pos.z = maxZ; car.vz *= -0.35; }
  if (car.pos.z < -maxZ) { car.pos.z = -maxZ; car.vz *= -0.35; }

  const groundY = sampleY(ramps, car.pos.x, car.pos.z, carHalfY);
  if (car.pos.y <= groundY + 0.08) {
    const wasAir = !car.onGround;
    if (car.vy < 0) car.vy = 0;
    car.pos.y = Math.max(carHalfY * 0.9, groundY);
    car.onGround = true;
    // Launch off steep rises / jump faces
    if (groundY > carHalfY + 1.8 && Math.hypot(car.vx, car.vz) > 9) {
      car.vy = Math.max(car.vy, 5.5 + (groundY - carHalfY) * 0.35);
      car.onGround = false;
    } else if (wasAir && Math.hypot(car.vx, car.vz) > 12 && groundY > carHalfY + 0.8) {
      car.vy = Math.max(car.vy, 2.5);
    }
  }
  if (car.pos.y > 16) {
    car.pos.y = 16;
    car.vy *= -0.25;
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
