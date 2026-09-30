/**
 * Drop Lab — Similarize Arcade workshop physics toy.
 * Vanilla Three.js + @dimforge/rapier3d-compat. No score. Cache: 20260929-droplab1
 */
import * as THREE from "three";
import RAPIER from "@dimforge/rapier3d-compat";

const MAX_BODIES = 48;
const FLOOR_HALF = 7;
const RIM_H = 0.58;
const THROW_MAX = 12;
const STORAGE_KEY = "droplab-settings-v1";
const KILL_Y = -16;
const BG = 0x101114;

const canvas = document.getElementById("view");
const bootEl = document.getElementById("boot");
const countEl = document.getElementById("shape-count");
const gravityInput = document.getElementById("gravity");
const bounceInput = document.getElementById("bounce");
const gravityVal = document.getElementById("gravity-val");
const bounceVal = document.getElementById("bounce-val");

let gravity = 9.8;
let bounce = 0.32;

try {
  const saved = JSON.parse(localStorage.getItem(STORAGE_KEY) || "null");
  if (saved && typeof saved === "object") {
    if (typeof saved.gravity === "number") gravity = clamp(saved.gravity, 0, 25);
    if (typeof saved.bounce === "number") bounce = clamp(saved.bounce, 0, 0.92);
  }
} catch (_) {}

gravityInput.value = String(gravity);
bounceInput.value = String(bounce);
gravityVal.textContent = gravity.toFixed(1);
bounceVal.textContent = bounce.toFixed(2);

await RAPIER.init();

let renderer;
try {
  renderer = new THREE.WebGLRenderer({
    canvas,
    antialias: true,
    alpha: false,
    powerPreference: "high-performance",
  });
} catch (err) {
  bootEl.hidden = false;
  bootEl.textContent = "WebGL is required for Drop Lab.";
  throw err;
}
renderer.setPixelRatio(Math.min(devicePixelRatio || 1, 2));
renderer.setClearColor(BG, 1);
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFSoftShadowMap;
renderer.outputColorSpace = THREE.SRGBColorSpace;

const scene = new THREE.Scene();
scene.background = new THREE.Color(BG);
scene.fog = new THREE.FogExp2(BG, 0.018);

const camera = new THREE.PerspectiveCamera(40, 1, 0.1, 120);
const lookTarget = new THREE.Vector3(0.15, 1.05, 0.15);
const orbit = {
  theta: Math.atan2(7.1 - 0.15, 5.8 - 0.15),
  phi: 0,
  radius: 0,
  minR: 3.4,
  maxR: 22,
  minY: 0.35,
};
{
  const start = new THREE.Vector3(5.8, 4.15, 7.1);
  const offset = start.clone().sub(lookTarget);
  orbit.radius = offset.length();
  orbit.theta = Math.atan2(offset.x, offset.z);
  orbit.phi = Math.asin(clamp(offset.y / orbit.radius, -0.99, 0.99));
  applyOrbit();
}

const keyLight = new THREE.DirectionalLight(0xffd7a0, 1.35);
keyLight.position.set(6.5, 10.5, 4.5);
keyLight.castShadow = true;
keyLight.shadow.mapSize.set(2048, 2048);
keyLight.shadow.camera.near = 1;
keyLight.shadow.camera.far = 40;
keyLight.shadow.camera.left = -12;
keyLight.shadow.camera.right = 12;
keyLight.shadow.camera.top = 12;
keyLight.shadow.camera.bottom = -12;
keyLight.shadow.bias = -0.00025;
scene.add(keyLight);
scene.add(new THREE.AmbientLight(0x6a645c, 0.38));
const fill = new THREE.DirectionalLight(0x9bb0c8, 0.45);
fill.position.set(-5, 4, -3);
scene.add(fill);
const rimLight = new THREE.PointLight(0xe4a23a, 0.55, 28, 2);
rimLight.position.set(0, 3.2, 0);
scene.add(rimLight);

const world = new RAPIER.World({ x: 0, y: -gravity, z: 0 });
world.integrationParameters.numSolverIterations = 8;

const entities = []; // { id, kind, body, collider, mesh, born }
let nextId = 1;
const raycaster = new THREE.Raycaster();
const pointerNdc = new THREE.Vector2();
const scratch = new THREE.Vector3();
const scratch2 = new THREE.Vector3();
const dragPlane = new THREE.Plane();
const dragHit = new THREE.Vector3();

const interaction = {
  mode: null, // 'throw' | 'orbit' | null
  pointerId: null,
  entity: null,
  last: null,
  samples: [],
  orbitLastX: 0,
  orbitLastY: 0,
};
let hoverEntity = null;

buildWorkshop();
spawnOpeningScene();
resize();
bindUi();
bootEl.hidden = true;
requestAnimationFrame(loop);

function clamp(v, a, b) {
  return Math.max(a, Math.min(b, v));
}

function rand(a, b) {
  return a + Math.random() * (b - a);
}

function saveSettings() {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify({ gravity, bounce }));
  } catch (_) {}
}

function applyOrbit() {
  const r = orbit.radius;
  const y = lookTarget.y + Math.sin(orbit.phi) * r;
  const flat = Math.cos(orbit.phi) * r;
  camera.position.set(
    lookTarget.x + Math.sin(orbit.theta) * flat,
    Math.max(orbit.minY, y),
    lookTarget.z + Math.cos(orbit.theta) * flat
  );
  // If clamped under floor, pull radius/phi so cam never sinks.
  if (camera.position.y <= orbit.minY + 1e-4) {
    camera.position.y = orbit.minY;
    const dy = camera.position.y - lookTarget.y;
    const horizontal = Math.sqrt(Math.max(1e-6, r * r - dy * dy));
    camera.position.x = lookTarget.x + Math.sin(orbit.theta) * horizontal;
    camera.position.z = lookTarget.z + Math.cos(orbit.theta) * horizontal;
    orbit.phi = Math.asin(clamp(dy / r, -0.99, 0.99));
  }
  camera.lookAt(lookTarget);
}

function buildWorkshop() {
  // Floor tray 14×14
  const floorMat = new THREE.MeshStandardMaterial({
    color: 0x14161c,
    roughness: 0.92,
    metalness: 0.08,
  });
  const floor = new THREE.Mesh(new THREE.BoxGeometry(FLOOR_HALF * 2, 0.22, FLOOR_HALF * 2), floorMat);
  floor.position.y = -0.11;
  floor.receiveShadow = true;
  floor.name = "floor";
  scene.add(floor);

  // Faint grid
  const grid = new THREE.GridHelper(FLOOR_HALF * 2, 28, 0x2a2e38, 0x1c1f28);
  grid.position.y = 0.002;
  grid.material.transparent = true;
  grid.material.opacity = 0.55;
  scene.add(grid);

  // Amber ring near edge
  const ringGeo = new THREE.RingGeometry(FLOOR_HALF - 0.55, FLOOR_HALF - 0.28, 96);
  const ringMat = new THREE.MeshBasicMaterial({
    color: 0xe4a23a,
    transparent: true,
    opacity: 0.55,
    side: THREE.DoubleSide,
  });
  const ring = new THREE.Mesh(ringGeo, ringMat);
  ring.rotation.x = -Math.PI / 2;
  ring.position.y = 0.01;
  scene.add(ring);

  // Low rim walls with lighter top lip
  const wallMat = new THREE.MeshStandardMaterial({
    color: 0x2c2a26,
    roughness: 0.85,
    metalness: 0.12,
  });
  const lipMat = new THREE.MeshStandardMaterial({
    color: 0x4a4640,
    roughness: 0.7,
    metalness: 0.18,
  });
  const wallT = 0.22;
  const specs = [
    { x: 0, z: FLOOR_HALF + wallT / 2, w: FLOOR_HALF * 2 + wallT * 2, d: wallT },
    { x: 0, z: -(FLOOR_HALF + wallT / 2), w: FLOOR_HALF * 2 + wallT * 2, d: wallT },
    { x: FLOOR_HALF + wallT / 2, z: 0, w: wallT, d: FLOOR_HALF * 2 },
    { x: -(FLOOR_HALF + wallT / 2), z: 0, w: wallT, d: FLOOR_HALF * 2 },
  ];
  for (const s of specs) {
    const wall = new THREE.Mesh(new THREE.BoxGeometry(s.w, RIM_H, s.d), wallMat);
    wall.position.set(s.x, RIM_H / 2, s.z);
    wall.castShadow = true;
    wall.receiveShadow = true;
    scene.add(wall);
    const lip = new THREE.Mesh(new THREE.BoxGeometry(s.w + 0.04, 0.06, s.d + 0.04), lipMat);
    lip.position.set(s.x, RIM_H + 0.01, s.z);
    lip.castShadow = true;
    scene.add(lip);
  }

  // Physics floor + walls
  const floorBody = world.createRigidBody(RAPIER.RigidBodyDesc.fixed().setTranslation(0, -0.11, 0));
  world.createCollider(
    RAPIER.ColliderDesc.cuboid(FLOOR_HALF, 0.11, FLOOR_HALF)
      .setFriction(0.9)
      .setRestitution(0)
      .setRestitutionCombineRule(RAPIER.CoefficientCombineRule.Max),
    floorBody
  );

  for (const s of specs) {
    const b = world.createRigidBody(
      RAPIER.RigidBodyDesc.fixed().setTranslation(s.x, RIM_H / 2, s.z)
    );
    world.createCollider(
      RAPIER.ColliderDesc.cuboid(s.w / 2, RIM_H / 2, s.d / 2)
        .setFriction(0.7)
        .setRestitution(0)
        .setRestitutionCombineRule(RAPIER.CoefficientCombineRule.Max),
      b
    );
  }
}

function shadeColor(hex, delta) {
  const c = new THREE.Color(hex);
  const hsl = { h: 0, s: 0, l: 0 };
  c.getHSL(hsl);
  hsl.l = clamp(hsl.l + delta, 0.05, 0.92);
  c.setHSL(hsl.h, hsl.s, hsl.l);
  return c;
}

function makeMaterial(kind, id) {
  const lightJitter = ((id * 17) % 9) / 9 * 0.1 - 0.05;
  if (kind === "sphere") {
    return new THREE.MeshPhysicalMaterial({
      color: shadeColor(0xe4a23a, lightJitter),
      roughness: 0.18,
      metalness: 0.05,
      transmission: 0.42,
      thickness: 0.55,
      transparent: true,
      opacity: 0.92,
      clearcoat: 0.65,
      clearcoatRoughness: 0.2,
      ior: 1.45,
    });
  }
  if (kind === "box") {
    return new THREE.MeshStandardMaterial({
      color: shadeColor(0xd7d2c8, lightJitter * 0.8),
      roughness: 0.88,
      metalness: 0.04,
    });
  }
  return new THREE.MeshStandardMaterial({
    color: shadeColor(0x3d6b66, lightJitter),
    roughness: 0.35,
    metalness: 0.72,
  });
}

function setHover(entity, on) {
  if (!entity || !entity.mesh) return;
  const mats = Array.isArray(entity.mesh.material) ? entity.mesh.material : [entity.mesh.material];
  for (const m of mats) {
    if (!m) continue;
    if (on) {
      if (m.userData._emissive == null && m.emissive) {
        m.userData._emissive = m.emissive.getHex();
        m.userData._emissiveIntensity = m.emissiveIntensity || 0;
      }
      if (m.emissive) {
        m.emissive.setHex(0xe4a23a);
        m.emissiveIntensity = 0.45;
      }
    } else if (m.emissive && m.userData._emissive != null) {
      m.emissive.setHex(m.userData._emissive);
      m.emissiveIntensity = m.userData._emissiveIntensity || 0;
    }
  }
}

function enforceCap() {
  while (entities.length > MAX_BODIES) {
    removeEntity(entities[0]);
  }
  updateCount();
}

function updateCount() {
  countEl.textContent = String(entities.length);
}

function removeEntity(ent) {
  if (!ent) return;
  const i = entities.indexOf(ent);
  if (i >= 0) entities.splice(i, 1);
  if (hoverEntity === ent) {
    setHover(ent, false);
    hoverEntity = null;
  }
  if (interaction.entity === ent) {
    endThrow(false);
  }
  if (ent.mesh) {
    scene.remove(ent.mesh);
    ent.mesh.geometry?.dispose?.();
    const mats = Array.isArray(ent.mesh.material) ? ent.mesh.material : [ent.mesh.material];
    mats.forEach((m) => m?.dispose?.());
  }
  if (ent.body) {
    world.removeRigidBody(ent.body);
  }
  ent.collider = null;
  ent.body = null;
  updateCount();
}

function clearAll() {
  while (entities.length) removeEntity(entities[entities.length - 1]);
}

function applyBounceToAll() {
  // Restitution Max combine: shape restitution drives bounce vs floor.
  for (const ent of entities) {
    if (ent.collider) {
      ent.collider.setRestitution(bounce);
      ent.collider.setRestitutionCombineRule(RAPIER.CoefficientCombineRule.Max);
    }
  }
}

function spawnShape(kind, opts = {}) {
  const id = nextId++;
  let mesh;
  let colliderDesc;
  let friction = 0.5;
  let pos = opts.position || spawnDropPos();
  let rot = opts.rotation || { x: 0, y: 0, z: 0, w: 1 };
  let linvel = opts.linvel || null;

  if (kind === "sphere") {
    const r = opts.radius != null ? opts.radius : rand(0.36, 0.56);
    mesh = new THREE.Mesh(new THREE.SphereGeometry(r, 32, 24), makeMaterial("sphere", id));
    colliderDesc = RAPIER.ColliderDesc.ball(r);
    friction = 0.42;
  } else if (kind === "box") {
    const hx = opts.half != null ? opts.half : rand(0.3, 0.46);
    const hy = opts.halfY != null ? opts.halfY : hx;
    const hz = opts.halfZ != null ? opts.halfZ : hx;
    mesh = new THREE.Mesh(new THREE.BoxGeometry(hx * 2, hy * 2, hz * 2), makeMaterial("box", id));
    colliderDesc = RAPIER.ColliderDesc.cuboid(hx, hy, hz);
    friction = 0.82;
    if (!opts.rotation && opts.randomTilt !== false && opts.opening !== true) {
      rot = randomTilt();
    }
  } else {
    const r = opts.radius != null ? opts.radius : rand(0.26, 0.38);
    const hh = opts.halfHeight != null ? opts.halfHeight : rand(0.36, 0.58);
    mesh = new THREE.Mesh(new THREE.CylinderGeometry(r, r, hh * 2, 28), makeMaterial("cylinder", id));
    colliderDesc = RAPIER.ColliderDesc.cylinder(hh, r);
    friction = 0.55;
    if (!opts.rotation && opts.randomTilt !== false && opts.opening !== true) {
      rot = randomTilt();
    }
  }

  mesh.castShadow = true;
  mesh.receiveShadow = true;
  mesh.position.set(pos.x, pos.y, pos.z);
  mesh.quaternion.set(rot.x, rot.y, rot.z, rot.w);
  mesh.userData.entityId = id;
  scene.add(mesh);

  // Rapier requires unit quaternions — normalize before create.
  {
    const q = new THREE.Quaternion(rot.x, rot.y, rot.z, rot.w).normalize();
    rot = { x: q.x, y: q.y, z: q.z, w: q.w };
  }
  const bodyDesc = RAPIER.RigidBodyDesc.dynamic()
    .setTranslation(pos.x, pos.y, pos.z)
    .setRotation(rot)
    .setLinearDamping(0.06)
    .setAngularDamping(0.2)
    .setCcdEnabled(true)
    .setCanSleep(true);
  const body = world.createRigidBody(bodyDesc);
  if (linvel) body.setLinvel(linvel, true);

  colliderDesc
    .setFriction(friction)
    .setRestitution(bounce)
    .setRestitutionCombineRule(RAPIER.CoefficientCombineRule.Max)
    .setDensity(1.1);
  const collider = world.createCollider(colliderDesc, body);

  const ent = { id, kind, body, collider, mesh, born: performance.now() };
  entities.push(ent);
  enforceCap();
  return ent;
}

function spawnDropPos() {
  const a = Math.random() * Math.PI * 2;
  const r = Math.random() * 1.15;
  return {
    x: Math.cos(a) * r,
    y: rand(5.4, 6.8),
    z: Math.sin(a) * r,
  };
}

function randomTilt() {
  const e = new THREE.Euler(rand(-0.55, 0.55), rand(0, Math.PI * 2), rand(-0.55, 0.55));
  const q = new THREE.Quaternion().setFromEuler(e);
  return { x: q.x, y: q.y, z: q.z, w: q.w };
}

function quatFromEuler(x, y, z) {
  const q = new THREE.Quaternion().setFromEuler(new THREE.Euler(x, y, z));
  return { x: q.x, y: q.y, z: q.z, w: q.w };
}

function spawnOpeningScene() {
  const half = 0.34;
  // 3-2-1 pyramid centered, bases at y≈0.34 / 1.02 / 1.7
  const stack = [
    { y: 0.34, xz: [[-0.7, 0], [0, 0], [0.7, 0]] },
    { y: 1.02, xz: [[-0.35, 0], [0.35, 0]] },
    { y: 1.7, xz: [[0, 0]] },
  ];
  for (const row of stack) {
    for (const [x, z] of row.xz) {
      spawnShape("box", {
        half,
        position: { x, y: row.y, z },
        rotation: { x: 0, y: 0, z: 0, w: 1 },
        opening: true,
        randomTilt: false,
      });
    }
  }

  // small 2-box stack left-back
  spawnShape("box", {
    half: 0.3,
    position: { x: -3.2, y: 0.3, z: -3.0 },
    rotation: quatFromEuler(0, 0.35, 0),
    opening: true,
    randomTilt: false,
  });
  spawnShape("box", {
    half: 0.3,
    position: { x: -3.2, y: 0.92, z: -3.0 },
    rotation: quatFromEuler(0, -0.25, 0),
    opening: true,
    randomTilt: false,
  });

  // one sphere + one upright cylinder on the right
  spawnShape("sphere", {
    radius: 0.42,
    position: { x: 3.1, y: 0.42, z: 1.4 },
    opening: true,
  });
  spawnShape("cylinder", {
    radius: 0.3,
    halfHeight: 0.42,
    position: { x: 3.4, y: 0.42, z: -0.6 },
    rotation: { x: 0, y: 0, z: 0, w: 1 },
    opening: true,
    randomTilt: false,
  });

  // one sphere falling from ~y=4.7 onto the pyramid
  spawnShape("sphere", {
    radius: 0.4,
    position: { x: 0.05, y: 4.7, z: 0.05 },
    opening: true,
  });
}

function spawnPile() {
  // APPEND 3-2-1 stack of stone boxes starting near y=6.4, then 2 spheres + 1 tilted cylinder
  const half = 0.34;
  const baseY = 6.4;
  const ox = rand(-0.4, 0.4);
  const oz = rand(-0.4, 0.4);
  const rows = [
    { y: baseY, xz: [[-0.7, 0], [0, 0], [0.7, 0]] },
    { y: baseY + 0.68, xz: [[-0.35, 0], [0.35, 0]] },
    { y: baseY + 1.36, xz: [[0, 0]] },
  ];
  for (const row of rows) {
    for (const [x, z] of row.xz) {
      spawnShape("box", {
        half,
        position: { x: x + ox, y: row.y, z: z + oz },
        rotation: { x: 0, y: 0, z: 0, w: 1 },
        randomTilt: false,
      });
    }
  }
  const topY = baseY + 1.36 + 0.34 + 0.45;
  spawnShape("sphere", {
    radius: 0.4,
    position: { x: ox - 0.35, y: topY, z: oz },
  });
  spawnShape("sphere", {
    radius: 0.38,
    position: { x: ox + 0.35, y: topY + 0.1, z: oz + 0.1 },
  });
  spawnShape("cylinder", {
    radius: 0.3,
    halfHeight: 0.4,
    position: { x: ox, y: topY + 0.85, z: oz },
    rotation: quatFromEuler(0.55, 0.4, 0.2),
    randomTilt: false,
  });
}

function focusInField() {
  const el = document.activeElement;
  if (!el) return false;
  const tag = (el.tagName || "").toLowerCase();
  return tag === "input" || tag === "textarea" || tag === "select" || el.isContentEditable;
}

function bindUi() {
  document.getElementById("btn-sphere").addEventListener("click", () => spawnShape("sphere"));
  document.getElementById("btn-box").addEventListener("click", () => spawnShape("box"));
  document.getElementById("btn-cylinder").addEventListener("click", () => spawnShape("cylinder"));
  document.getElementById("btn-pile").addEventListener("click", () => spawnPile());
  document.getElementById("btn-clear").addEventListener("click", () => clearAll());

  gravityInput.addEventListener("input", () => {
    gravity = clamp(parseFloat(gravityInput.value) || 0, 0, 25);
    gravityVal.textContent = gravity.toFixed(1);
    world.gravity = { x: 0, y: -gravity, z: 0 };
    saveSettings();
  });
  bounceInput.addEventListener("input", () => {
    bounce = clamp(parseFloat(bounceInput.value) || 0, 0, 0.92);
    bounceVal.textContent = bounce.toFixed(2);
    applyBounceToAll();
    saveSettings();
  });

  addEventListener("keydown", (e) => {
    if (focusInField()) return;
    const k = e.key.toLowerCase();
    if (k === "1") spawnShape("sphere");
    else if (k === "2") spawnShape("box");
    else if (k === "3") spawnShape("cylinder");
    else if (k === "4") spawnPile();
    else if (k === "c") clearAll();
  });

  canvas.addEventListener("pointerdown", onPointerDown);
  canvas.addEventListener("pointermove", onPointerMove);
  canvas.addEventListener("pointerup", onPointerUp);
  canvas.addEventListener("pointercancel", onPointerUp);
  canvas.addEventListener("pointerleave", onPointerLeave);
  canvas.addEventListener("wheel", onWheel, { passive: false });
  canvas.addEventListener("contextmenu", (e) => e.preventDefault());

  // Pinch zoom
  let pinchDist = 0;
  canvas.addEventListener(
    "touchstart",
    (e) => {
      if (e.touches.length === 2) {
        const dx = e.touches[0].clientX - e.touches[1].clientX;
        const dy = e.touches[0].clientY - e.touches[1].clientY;
        pinchDist = Math.hypot(dx, dy);
      }
    },
    { passive: true }
  );
  canvas.addEventListener(
    "touchmove",
    (e) => {
      if (e.touches.length === 2 && interaction.mode !== "throw") {
        e.preventDefault();
        const dx = e.touches[0].clientX - e.touches[1].clientX;
        const dy = e.touches[0].clientY - e.touches[1].clientY;
        const d = Math.hypot(dx, dy);
        if (pinchDist > 0) {
          const scale = pinchDist / d;
          orbit.radius = clamp(orbit.radius * scale, orbit.minR, orbit.maxR);
          applyOrbit();
        }
        pinchDist = d;
      }
    },
    { passive: false }
  );
  canvas.addEventListener(
    "touchend",
    () => {
      pinchDist = 0;
    },
    { passive: true }
  );

  addEventListener("resize", resize);
}

function resize() {
  const w = Math.max(1, window.innerWidth);
  const h = Math.max(1, window.innerHeight);
  camera.aspect = w / h;
  camera.updateProjectionMatrix();
  renderer.setSize(w, h, false);
}

function setPointerNdc(event) {
  const rect = canvas.getBoundingClientRect();
  pointerNdc.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
  pointerNdc.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;
}

function pickEntity(event) {
  setPointerNdc(event);
  raycaster.setFromCamera(pointerNdc, camera);
  const meshes = entities.map((e) => e.mesh);
  const hits = raycaster.intersectObjects(meshes, false);
  if (!hits.length) return null;
  const id = hits[0].object.userData.entityId;
  return entities.find((e) => e.id === id) || null;
}

function hitFloor(event) {
  setPointerNdc(event);
  raycaster.setFromCamera(pointerNdc, camera);
  const plane = new THREE.Plane(new THREE.Vector3(0, 1, 0), 0);
  return raycaster.ray.intersectPlane(plane, scratch2) ? scratch2.clone() : null;
}

function onPointerDown(event) {
  if (event.button !== 0 && event.pointerType === "mouse") {
    // right / middle: orbit
    if (event.button === 2 || event.button === 1) {
      beginOrbit(event);
    }
    return;
  }
  if (event.button !== 0 && event.pointerType === "mouse") return;

  const ent = pickEntity(event);
  if (ent) {
    beginThrow(event, ent);
    return;
  }
  // left-drag empty floor → orbit (also right-drag)
  const floorPt = hitFloor(event);
  if (floorPt || event.button === 2) {
    beginOrbit(event);
  }
}

function beginThrow(event, ent) {
  interaction.mode = "throw";
  interaction.pointerId = event.pointerId;
  interaction.entity = ent;
  interaction.samples = [];
  canvas.setPointerCapture(event.pointerId);
  canvas.classList.add("grabbing");

  ent.body.setBodyType(RAPIER.RigidBodyType.KinematicPositionBased, true);
  ent.body.setLinvel({ x: 0, y: 0, z: 0 }, true);
  ent.body.setAngvel({ x: 0, y: 0, z: 0 }, true);

  const t = ent.body.translation();
  dragPlane.setFromNormalAndCoplanarPoint(
    camera.getWorldDirection(scratch).negate().normalize(),
    new THREE.Vector3(t.x, t.y, t.z)
  );
  moveThrown(event);
}

function beginOrbit(event) {
  if (interaction.mode === "throw") return;
  interaction.mode = "orbit";
  interaction.pointerId = event.pointerId;
  interaction.orbitLastX = event.clientX;
  interaction.orbitLastY = event.clientY;
  canvas.setPointerCapture(event.pointerId);
  canvas.classList.add("grabbing");
}

function moveThrown(event) {
  const ent = interaction.entity;
  if (!ent) return;
  setPointerNdc(event);
  raycaster.setFromCamera(pointerNdc, camera);
  // Refresh plane facing camera through current grab height
  const t = ent.body.translation();
  const facing = camera.getWorldDirection(scratch).multiplyScalar(-1).normalize();
  // Prefer near-vertical plane facing camera but keep Y free on that plane
  dragPlane.setFromNormalAndCoplanarPoint(facing, new THREE.Vector3(t.x, t.y, t.z));
  if (!raycaster.ray.intersectPlane(dragPlane, dragHit)) return;

  // Don't sink through floor — keep a small clearance based on approx radius
  const minY = 0.28;
  dragHit.y = Math.max(minY, dragHit.y);
  // Keep inside tray roughly
  dragHit.x = clamp(dragHit.x, -FLOOR_HALF + 0.4, FLOOR_HALF - 0.4);
  dragHit.z = clamp(dragHit.z, -FLOOR_HALF + 0.4, FLOOR_HALF - 0.4);

  ent.body.setNextKinematicTranslation({ x: dragHit.x, y: dragHit.y, z: dragHit.z });
  ent.mesh.position.copy(dragHit);

  const now = performance.now();
  interaction.samples.push({ t: now, x: dragHit.x, y: dragHit.y, z: dragHit.z });
  if (interaction.samples.length > 8) interaction.samples.shift();
}

function onPointerMove(event) {
  if (interaction.mode === "throw" && event.pointerId === interaction.pointerId) {
    moveThrown(event);
    return;
  }
  if (interaction.mode === "orbit" && event.pointerId === interaction.pointerId) {
    const dx = event.clientX - interaction.orbitLastX;
    const dy = event.clientY - interaction.orbitLastY;
    interaction.orbitLastX = event.clientX;
    interaction.orbitLastY = event.clientY;
    orbit.theta -= dx * 0.0055;
    orbit.phi += dy * 0.0045;
    orbit.phi = clamp(orbit.phi, 0.08, 1.35);
    applyOrbit();
    return;
  }

  // Hover glow
  const ent = pickEntity(event);
  if (ent !== hoverEntity) {
    setHover(hoverEntity, false);
    hoverEntity = ent;
    setHover(hoverEntity, true);
    canvas.classList.toggle("hover-shape", !!ent);
  }
}

function endThrow(applyVel) {
  const ent = interaction.entity;
  if (!ent) {
    interaction.mode = null;
    interaction.pointerId = null;
    return;
  }
  ent.body.setBodyType(RAPIER.RigidBodyType.Dynamic, true);
  if (applyVel) {
    const samples = interaction.samples;
    let vx = 0, vy = 0, vz = 0;
    if (samples.length >= 2) {
      const a = samples[samples.length - 1];
      // look back ~60–100ms
      let b = samples[0];
      for (let i = samples.length - 2; i >= 0; i--) {
        if (a.t - samples[i].t >= 60) {
          b = samples[i];
          break;
        }
        b = samples[i];
      }
      const dt = Math.max(0.016, (a.t - b.t) / 1000);
      vx = (a.x - b.x) / dt;
      vy = (a.y - b.y) / dt;
      vz = (a.z - b.z) / dt;
      const speed = Math.hypot(vx, vy, vz);
      if (speed > THROW_MAX) {
        const s = THROW_MAX / speed;
        vx *= s; vy *= s; vz *= s;
      }
    }
    ent.body.setLinvel({ x: vx, y: vy, z: vz }, true);
  }
  interaction.entity = null;
  interaction.mode = null;
  interaction.pointerId = null;
  interaction.samples = [];
  canvas.classList.remove("grabbing");
}

function onPointerUp(event) {
  if (event.pointerId !== interaction.pointerId) return;
  try {
    canvas.releasePointerCapture(event.pointerId);
  } catch (_) {}
  if (interaction.mode === "throw") {
    endThrow(true);
  } else {
    interaction.mode = null;
    interaction.pointerId = null;
    canvas.classList.remove("grabbing");
  }
}

function onPointerLeave() {
  if (hoverEntity && interaction.mode !== "throw") {
    setHover(hoverEntity, false);
    hoverEntity = null;
    canvas.classList.remove("hover-shape");
  }
}

function onWheel(event) {
  event.preventDefault();
  if (interaction.mode === "throw") return;
  const factor = Math.exp(event.deltaY * 0.0012);
  orbit.radius = clamp(orbit.radius * factor, orbit.minR, orbit.maxR);
  applyOrbit();
}

let lastT = performance.now();
function loop(now) {
  requestAnimationFrame(loop);
  const dt = Math.min(0.05, (now - lastT) / 1000);
  lastT = now;

  world.timestep = dt;
  try {
    world.step();
  } catch (err) {
    console.warn("physics step", err);
  }

  const doomed = [];
  for (let i = 0; i < entities.length; i++) {
    const ent = entities[i];
    if (!ent.body) continue;
    if (interaction.mode === "throw" && interaction.entity === ent) {
      const t = ent.body.translation();
      const r = ent.body.rotation();
      ent.mesh.position.set(t.x, t.y, t.z);
      ent.mesh.quaternion.set(r.x, r.y, r.z, r.w);
      continue;
    }
    const t = ent.body.translation();
    if (t.y < KILL_Y) {
      doomed.push(ent);
      continue;
    }
    const r = ent.body.rotation();
    ent.mesh.position.set(t.x, t.y, t.z);
    ent.mesh.quaternion.set(r.x, r.y, r.z, r.w);
  }
  for (const ent of doomed) removeEntity(ent);

  renderer.render(scene, camera);
}

// Apply initial gravity from settings
world.gravity = { x: 0, y: -gravity, z: 0 };
updateCount();
