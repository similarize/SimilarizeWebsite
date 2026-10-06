/**
 * Soccer RC 3D — Rocket League–lite arcade cabinet.
 * Vanilla Three.js (CDN) + simple custom physics. No backend.
 * Controls match flat Soccer RC: RT fwd · LT rev · stick steer while thrusting · A kick · B boost.
 */
import * as THREE from "three";

const CACHE = "20261006-soccerrc3d1";
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

const canvas = document.getElementById("view");
const boot = document.getElementById("boot");
const bannerEl = document.getElementById("banner");
const s1El = document.getElementById("s1");
const s2El = document.getElementById("s2");
const boost1El = document.getElementById("boost1");
const boost2El = document.getElementById("boost2");

const keys = Object.create(null);
const touch = [
  { fwd: 0, rev: 0, left: 0, right: 0, kick: 0, boost: 0, kickEdge: false },
  { fwd: 0, rev: 0, left: 0, right: 0, kick: 0, boost: 0, kickEdge: false },
];

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
  let steer = 0;
  if (thrusting) {
    if (g.dpad && g.dpad.l) steer = -1;
    else if (g.dpad && g.dpad.r) steer = 1;
    else steer = Math.max(-1, Math.min(1, g.lx || 0));
  }
  const kick = !!(g.buttonsPressed && g.buttonsPressed.a);
  const boost = !!(g.b || g.rb);
  return { steer, fwd, rev, thrusting, kick, boost };
}

function readInput(playerIndex) {
  const t = touch[playerIndex];
  const g = playerIndex === 0 ? _gp0 : _gp1;
  const pd = padDrive(g);

  let fwd = pd.fwd, rev = pd.rev, steer = pd.steer;
  let kick = pd.kick || t.kickEdge;
  let boost = pd.boost || !!t.boost;

  if (playerIndex === 0) {
    if (keys.ArrowUp || t.fwd) fwd = 1;
    if (keys.ArrowDown || t.rev) rev = 1;
    // Keyboard/touch always steer (flat Soccer RC); pad stick only while thrusting (padDrive).
    const keySteer = keys.ArrowLeft || t.left ? -1 : keys.ArrowRight || t.right ? 1 : 0;
    if (keySteer) steer = keySteer;
    if (keys[" "] || keys.Space) kick = true;
    if (keys.ShiftLeft || keys.ShiftRight) boost = true;
  } else {
    if (keys.w || keys.W || t.fwd) fwd = 1;
    if (keys.s || keys.S || t.rev) rev = 1;
    const keySteer = keys.a || keys.A || t.left ? -1 : keys.d || keys.D || t.right ? 1 : 0;
    if (keySteer) steer = keySteer;
    if (keys.q || keys.Q) kick = true;
    if (keys.e || keys.E) boost = true;
  }

  if (fwd > 0 && rev > 0) {
    if (fwd >= rev) rev = 0;
    else fwd = 0;
  }

  t.kickEdge = false;
  return { fwd, rev, steer, kick, boost };
}

function kickBall(car) {
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
  const forwardX = Math.cos(car.yaw);
  const forwardZ = Math.sin(car.yaw);
  // Bias impulse toward car facing + contact normal
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
    setTimeout(() => resetBall(0), 900);
  } else if (ball.pos.x < -HALF_X - BALL_R * 0.4) {
    score[1]++;
    s2El.textContent = String(score[1]);
    freezeT = 1.8;
    showBanner("BLUE SCORES!", "blue", 1.6);
    setTimeout(() => resetBall(0), 900);
  }
}

function updateCar(car, inp, dt) {
  if (car.jumpCd > 0) car.jumpCd -= dt;

  if (Math.abs(inp.steer) > 0.05) {
    // Reverse flips steer feel so backing up matches wheel direction
    const flip = inp.rev > inp.fwd && inp.rev > 0.04 ? -1 : 1;
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

  if (inp.kick) kickBall(car);

  // Sync mesh
  car.mesh.position.copy(car.pos);
  car.mesh.rotation.y = -car.yaw; // mesh nose is +X; yaw 0 → +X
  if (car.mesh.userData.flame) {
    car.mesh.userData.flame.visible = boostOn;
  }
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

function refreshGamepads() {
  const GP = window.SimilarizeGamepad;
  if (!GP) {
    _gp0 = _gp1 = null;
    return;
  }
  _gp0 = GP.pollPad(0);
  _gp1 = GP.pollPad(1);
}

function tick() {
  const dt = Math.min(0.05, clock.getDelta());
  refreshGamepads();

  if (_gp0 && _gp0.connected && (_gp0.buttonsPressed.start || _gp0.buttonsPressed.back)) {
    resetMatch();
  }
  if (keys.r || keys.R) {
    keys.r = keys.R = false;
    resetMatch();
  }

  if (bannerT > 0) {
    bannerT -= dt;
    if (bannerT <= 0) bannerEl.className = "";
  }

  if (freezeT > 0) {
    freezeT -= dt;
  } else {
    const inp0 = readInput(0);
    const inp1 = readInput(1);
    // rising-edge kick for keyboard space/q held — convert hold to edge via prev
    updateCar(cars[0], edgeKick(0, inp0), dt);
    updateCar(cars[1], edgeKick(1, inp1), dt);
    collideCars();
    for (const c of cars) collideCarBall(c);
    updateBall(dt);
    checkGoal();
  }

  boost1El.style.transform = "scaleX(" + cars[0].boost.toFixed(3) + ")";
  boost2El.style.transform = "scaleX(" + cars[1].boost.toFixed(3) + ")";

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
  document.getElementById("resetBtn").addEventListener("click", () => resetMatch());
}

function bindTouch() {
  document.querySelectorAll(".pads button").forEach((btn) => {
    const p = +btn.dataset.p;
    const act = btn.dataset.act;
    const set = (v) => {
      if (act === "kick") {
        if (v) touch[p].kickEdge = true;
        touch[p].kick = v ? 1 : 0;
      } else if (act === "boost") touch[p].boost = v ? 1 : 0;
      else if (act === "fwd") touch[p].fwd = v ? 1 : 0;
      else if (act === "rev") touch[p].rev = v ? 1 : 0;
      else if (act === "left") touch[p].left = v ? 1 : 0;
      else if (act === "right") touch[p].right = v ? 1 : 0;
    };
    const down = (e) => {
      e.preventDefault();
      set(1);
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
    btn.addEventListener("pointerleave", up);
    btn.addEventListener("pointercancel", up);
  });
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
    },
    {
      mesh: mesh1,
      pos: new THREE.Vector3(8, CAR_HALF.y, 0),
      yaw: Math.PI,
      vx: 0, vy: 0, vz: 0,
      boost: 1,
      onGround: true,
      jumpCd: 0,
    },
  ];

  clock = new THREE.Clock();
  bindKeys();
  bindTouch();
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
