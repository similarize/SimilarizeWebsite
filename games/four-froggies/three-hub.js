/* Four Froggies — three.js hub (CDN). Fixed-angle 2.5D-ish (orbit locked / isometric-ish).
   NOT free-fly FPS. Canvas-parity landmarks · solo-first.
   Big map · compound · squiggle track · pond whales · 4 trucks+shared ·
   on-water/under · Starship → Escape/hard thruster.
   WASD: camera-relative (Canvas steer.y<0 = screen up) — do not invert. */
(function (global) {
  "use strict";

  var C = global.FroggiesCanon;
  var active = false;
  var hooks = {};
  var steer = { x: 0, y: 0 };
  var wantInteract = false;
  var wantAbility = false;

  var renderer = null;
  var scene = null;
  var camera = null;
  var raf = 0;
  var state = null;
  var clock = null;

  function destroy() {
    active = false;
    steer.x = steer.y = 0;
    wantInteract = wantAbility = false;
    if (raf) {
      cancelAnimationFrame(raf);
      raf = 0;
    }
    if (renderer) {
      try {
        renderer.dispose();
        var canvas = renderer.domElement;
        if (canvas && canvas.parentNode) canvas.parentNode.removeChild(canvas);
      } catch (e) { /* ignore */ }
      renderer = null;
    }
    scene = null;
    camera = null;
    state = null;
    var host = document.getElementById("engine-host");
    if (host) host.innerHTML = "";
  }

  function setSteer(x, y) {
    steer.x = x;
    steer.y = y;
  }
  function pulseInteract() { wantInteract = true; }
  function pulseAbility() { wantAbility = true; }
  function isActive() { return active; }

  function hex(c) {
    return parseInt(String(c).replace("#", ""), 16);
  }

  function makeFrogMesh(def, scale) {
    var g = new THREE.Group();
    var body = new THREE.Mesh(
      new THREE.SphereGeometry(0.45 * (scale || 1), 12, 10),
      new THREE.MeshStandardMaterial({ color: hex(def.color), roughness: 0.55, metalness: 0.1 })
    );
    body.position.y = 0.45 * (scale || 1);
    g.add(body);
    var hat = new THREE.Mesh(
      new THREE.ConeGeometry(0.28 * (scale || 1), 0.35 * (scale || 1), 8),
      new THREE.MeshStandardMaterial({ color: hex(def.hat), roughness: 0.6 })
    );
    hat.position.y = 0.95 * (scale || 1);
    g.add(hat);
    var eyeM = new THREE.MeshStandardMaterial({ color: hex(def.accent) });
    var e1 = new THREE.Mesh(new THREE.SphereGeometry(0.08, 6, 6), eyeM);
    var e2 = e1.clone();
    e1.position.set(-0.15, 0.55, 0.35);
    e2.position.set(0.15, 0.55, 0.35);
    g.add(e1);
    g.add(e2);
    g.castShadow = true;
    return g;
  }

  function labelSprite(text, color) {
    var canvas = document.createElement("canvas");
    canvas.width = 256;
    canvas.height = 64;
    var ctx = canvas.getContext("2d");
    ctx.clearRect(0, 0, 256, 64);
    ctx.font = "bold 28px Segoe UI, system-ui, sans-serif";
    ctx.textAlign = "center";
    ctx.strokeStyle = "#000";
    ctx.lineWidth = 6;
    ctx.strokeText(text, 128, 40);
    ctx.fillStyle = color || "#fff";
    ctx.fillText(text, 128, 40);
    var tex = new THREE.CanvasTexture(canvas);
    var mat = new THREE.SpriteMaterial({ map: tex, transparent: true, depthTest: false });
    var spr = new THREE.Sprite(mat);
    spr.scale.set(2.2, 0.55, 1);
    return spr;
  }

  function worldToThree(x, y) {
    // Map ranch coords → three XZ plane (Y up)
    return { x: (x - C.MAP_W * 0.5) * 0.02, z: (y - C.MAP_H * 0.5) * 0.02 };
  }

  function threeToWorld(tx, tz) {
    return {
      x: tx / 0.02 + C.MAP_W * 0.5,
      y: tz / 0.02 + C.MAP_H * 0.5,
    };
  }

  function makeTruckMesh(accentHex) {
    var g = new THREE.Group();
    var body = new THREE.Mesh(
      new THREE.BoxGeometry(1.55, 0.42, 0.72),
      new THREE.MeshStandardMaterial({ color: 0x9ca3af, metalness: 0.55, roughness: 0.35 })
    );
    body.position.y = 0.28; body.castShadow = true; g.add(body);
    var cab = new THREE.Mesh(
      new THREE.BoxGeometry(0.7, 0.28, 0.62),
      new THREE.MeshStandardMaterial({ color: accentHex, metalness: 0.4, roughness: 0.4 })
    );
    cab.position.set(0.22, 0.52, 0); g.add(cab);
    var edge = new THREE.LineSegments(
      new THREE.EdgesGeometry(new THREE.BoxGeometry(1.55, 0.42, 0.72)),
      new THREE.LineBasicMaterial({ color: 0x111827 })
    );
    edge.position.y = 0.28; g.add(edge);
    g.userData.bodyMat = body.material;
    return g;
  }

  function addPathRibbon(pts, y, color, width) {
    if (!pts || pts.length < 2) return;
    var curvePts = [];
    for (var i = 0; i < pts.length; i++) {
      var p = worldToThree(pts[i][0], pts[i][1]);
      curvePts.push(new THREE.Vector3(p.x, y, p.z));
    }
    var tube = new THREE.Mesh(
      new THREE.TubeGeometry(new THREE.CatmullRomCurve3(curvePts, false), Math.max(24, pts.length * 3), width, 6, false),
      new THREE.MeshStandardMaterial({ color: color, roughness: 0.9 })
    );
    tube.receiveShadow = true; scene.add(tube);
  }

  function addMech(m, color, h) {
    var p = worldToThree(m.x, m.y);
    var body = new THREE.Mesh(
      new THREE.BoxGeometry(h * 0.28, h, h * 0.22),
      new THREE.MeshStandardMaterial({ color: color, metalness: 0.35, roughness: 0.45 })
    );
    body.position.set(p.x, h * 0.5, p.z); body.castShadow = true; scene.add(body);
    var edge = new THREE.LineSegments(
      new THREE.EdgesGeometry(new THREE.BoxGeometry(h * 0.28, h, h * 0.22)),
      new THREE.LineBasicMaterial({ color: 0x0f172a })
    );
    edge.position.copy(body.position); scene.add(edge);
    scene.add(labelSprite(m.stories + "-story mech", "#fff")).position.set(p.x, h + 0.55, p.z);
  }

  function buildCompound() {
    var cp = C.COMPOUND || {};
    var yard = cp.yard || { x: 100, y: 2100, w: 600, h: 360 };
    var yp = worldToThree(yard.x + yard.w / 2, yard.y + yard.h / 2);
    var yardM = new THREE.Mesh(
      new THREE.BoxGeometry(yard.w * 0.02, 0.08, yard.h * 0.02),
      new THREE.MeshStandardMaterial({ color: 0x468232, roughness: 0.95 })
    );
    yardM.position.set(yp.x, 0.1, yp.z); scene.add(yardM);
    scene.add(labelSprite("Backyard", "#ecfccb")).position.set(yp.x, 1.2, yp.z);
    for (var ai = 0; ai < 22; ai++) {
      var ap = worldToThree(yard.x + 30 + Math.random() * (yard.w - 60), yard.y + 40 + Math.random() * (yard.h - 80));
      var animal = new THREE.Mesh(
        new THREE.SphereGeometry(0.14 + Math.random() * 0.08, 8, 6),
        new THREE.MeshStandardMaterial({ color: ai % 2 ? 0xc4a574 : 0x8b6914 })
      );
      animal.position.set(ap.x, 0.18, ap.z); scene.add(animal);
    }
    var gar = cp.garage || { x: 700, y: 1400, w: 480, h: 520 };
    var gp = worldToThree(gar.x + gar.w / 2, gar.y + gar.h / 2);
    var garage = new THREE.Mesh(
      new THREE.BoxGeometry(gar.w * 0.02, 2.4, gar.h * 0.02),
      new THREE.MeshStandardMaterial({ color: 0x6b7280, roughness: 0.75, metalness: 0.15 })
    );
    garage.position.set(gp.x, 1.2, gp.z); garage.castShadow = true; scene.add(garage);
    var door = new THREE.Mesh(new THREE.BoxGeometry(2.2, 1.4, 0.12), new THREE.MeshStandardMaterial({ color: 0x111827 }));
    door.position.set(gp.x, 0.7, gp.z + gar.h * 0.01 - 0.2); scene.add(door);
    scene.add(labelSprite("Garage · James toys", "#fff")).position.set(gp.x, 2.9, gp.z);
    for (var t = 0; t < 32; t++) {
      var tp = worldToThree(gar.x + 40 + (t % 8) * 48, gar.y + 70 + Math.floor(t / 8) * 50);
      var toy = new THREE.Mesh(new THREE.BoxGeometry(0.22, 0.22, 0.22), new THREE.MeshStandardMaterial({ color: 0xfbbf24 }));
      toy.position.set(tp.x, 0.2, tp.z); scene.add(toy);
    }
    var house = cp.house || { x: 120, y: 1420, w: 520, h: 420 };
    var hp = worldToThree(house.x + house.w / 2, house.y + house.h / 2);
    var houseM = new THREE.Mesh(
      new THREE.BoxGeometry(house.w * 0.02, 2.8, house.h * 0.02),
      new THREE.MeshStandardMaterial({ color: 0xd4b896, roughness: 0.7 })
    );
    houseM.position.set(hp.x, 1.4, hp.z); houseM.castShadow = true; scene.add(houseM);
    var roof = new THREE.Mesh(
      new THREE.ConeGeometry(Math.max(house.w, house.h) * 0.012, 1.6, 4),
      new THREE.MeshStandardMaterial({ color: 0x6d4c41 })
    );
    roof.position.set(hp.x, 3.4, hp.z); roof.rotation.y = Math.PI / 4; scene.add(roof);
    scene.add(labelSprite("James · Ranch house", "#fff7ed")).position.set(hp.x, 4.2, hp.z);
    addMech(cp.mech10 || { x: 820, y: 1680, stories: 10 }, 0xa5b4fc, 1.4);
    addMech(cp.mech100 || { x: 980, y: 1700, stories: 100 }, 0x67e8f9, 2.4);
    addMech(cp.mech1000 || { x: 340, y: 2420, stories: 1000 }, 0xfcd34d, 5.5);
  }

  function buildTrack() {
    var mounds = C.TRACK_MOUNDS || [];
    for (var i = 0; i < mounds.length; i++) {
      var m = mounds[i], p = worldToThree(m.x, m.y);
      var geo = new THREE.SphereGeometry(m.r * 0.02, 16, 12);
      geo.scale(1, Math.abs(m.h) * 0.55 + 0.15, 0.55);
      var mesh = new THREE.Mesh(geo, new THREE.MeshStandardMaterial({ color: m.h >= 0 ? 0x78716c : 0x44403c, roughness: 0.95 }));
      mesh.position.set(p.x, Math.abs(m.h) * 0.35, p.z); scene.add(mesh);
    }
    addPathRibbon(C.TRACK_MAIN, 0.12, 0x292524, 0.55);
    addPathRibbon(C.TRACK_MAIN, 0.18, 0xa8a29e, 0.22);
    addPathRibbon(C.TRACK_BRANCH_A, 0.12, 0x292524, 0.35);
    addPathRibbon(C.TRACK_BRANCH_A, 0.17, 0xa8a29e, 0.14);
    addPathRibbon(C.TRACK_BRANCH_B, 0.12, 0x292524, 0.32);
    addPathRibbon(C.TRACK_BRANCH_B, 0.17, 0xa8a29e, 0.13);
    var ramps = C.RAMPS || [];
    for (var r = 0; r < ramps.length; r++) {
      var rp = ramps[r], tp = worldToThree(rp.x, rp.y);
      var ramp = new THREE.Mesh(
        new THREE.BoxGeometry(rp.w * 0.02, 0.25, rp.h * 0.02),
        new THREE.MeshStandardMaterial({ color: 0xf59e0b, metalness: 0.2 })
      );
      ramp.position.set(tp.x, 0.2, tp.z); ramp.rotation.z = -0.25; scene.add(ramp);
    }
  }

  function buildPondLife() {
    var pond = C.AREAS[2];
    state.fish = []; state.whales = [];
    for (var f = 0; f < 20; f++) {
      var fp = worldToThree(pond.x + 60 + Math.random() * (pond.w - 120), pond.y + 60 + Math.random() * (pond.h - 120));
      var fish = new THREE.Mesh(
        new THREE.SphereGeometry(0.16 + Math.random() * 0.08, 8, 6),
        new THREE.MeshStandardMaterial({ color: 0x67e8f9, emissive: 0x164e63, emissiveIntensity: 0.25 })
      );
      fish.position.set(fp.x, 0.12, fp.z);
      fish.userData.phase = Math.random() * Math.PI * 2; fish.userData.bx = fp.x; fish.userData.bz = fp.z;
      scene.add(fish); state.fish.push(fish);
    }
    for (var w = 0; w < 5; w++) {
      var wp = worldToThree(pond.x + 160 + Math.random() * (pond.w - 320), pond.y + 140 + Math.random() * (pond.h - 280));
      var whale = new THREE.Mesh(
        new THREE.SphereGeometry(0.55 + Math.random() * 0.25, 12, 8),
        new THREE.MeshStandardMaterial({ color: 0x38bdf8, roughness: 0.45, metalness: 0.15 })
      );
      whale.scale.set(1.6, 0.55, 1); whale.position.set(wp.x, 0.2, wp.z);
      whale.userData.phase = Math.random() * Math.PI * 2; whale.userData.bx = wp.x; whale.userData.bz = wp.z;
      scene.add(whale); state.whales.push(whale);
      scene.add(labelSprite("whale", "#e0f2fe")).position.set(wp.x, 1.0, wp.z);
    }
    var pl = worldToThree(pond.x + pond.w * 0.5, pond.y + 40);
    scene.add(labelSprite("Pond · fishies & whales", "#ecfeff")).position.set(pl.x, 1.5, pl.z);
  }

  function buildStarshipApproach() {
    var path = C.STARSHIP_APPROACH; if (!path || !path.length) return;
    var pts = path.map(function (pt) {
      var p = worldToThree(pt[0], pt[1]);
      return new THREE.Vector3(p.x, 0.08, p.z);
    });
    scene.add(new THREE.Mesh(
      new THREE.TubeGeometry(new THREE.CatmullRomCurve3(pts, false), 32, 0.12, 6, false),
      new THREE.MeshStandardMaterial({ color: 0xfbbf24, emissive: 0xb45309, emissiveIntensity: 0.25 })
    ));
  }

  function buildTrucks() {
    state.parkedTrucks = [];
    var spots = C.TRUCK_SPOTS || [];
    for (var i = 0; i < spots.length; i++) {
      var s = spots[i];
      var accent = s.id === "shared" ? 0xfbbf24 : hex((C.FROG_DEFS[s.id] || C.FROG_DEFS.james).color);
      var truck = makeTruckMesh(accent);
      var p = worldToThree(s.x, s.y);
      truck.position.set(p.x, 0, p.z); scene.add(truck);
      var label = s.id === "shared" ? "All aboard" : ("Cybertruck · " + (C.FROG_DEFS[s.id] || {}).name);
      var lab = labelSprite(label, "#fde68a"); lab.position.set(p.x, 1.5, p.z); scene.add(lab);
      state.parkedTrucks.push({ spot: s, mesh: truck, label: lab });
    }
  }

  function buildRanch() {
    scene = new THREE.Scene();
    scene.background = new THREE.Color(0x87b5d9);
    scene.fog = new THREE.Fog(0x87b5d9, 40, 110);

    // Fixed-angle isometric-ish camera — orbit LOCKED (no free-fly)
    var aspect = window.innerWidth / Math.max(1, window.innerHeight);
    camera = new THREE.PerspectiveCamera(42, aspect, 0.1, 200);
    camera.position.set(18, 22, 18);
    camera.lookAt(0, 0, 0);
    camera.userData.lockTarget = new THREE.Vector3(0, 0, 0);

    var hemi = new THREE.HemisphereLight(0xfff0d0, 0x3a5a2a, 0.85);
    scene.add(hemi);
    var sun = new THREE.DirectionalLight(0xffe6b0, 0.95);
    sun.position.set(12, 22, 8);
    sun.castShadow = true;
    sun.shadow.mapSize.set(2048, 2048);
    scene.add(sun);

    // Ground
    var ground = new THREE.Mesh(
      new THREE.PlaneGeometry(C.MAP_W * 0.02, C.MAP_H * 0.02, 24, 18),
      new THREE.MeshStandardMaterial({ color: 0x3d7a35, roughness: 0.9 })
    );
    ground.rotation.x = -Math.PI / 2;
    ground.receiveShadow = true;
    scene.add(ground);

    // Soft grid
    var grid = new THREE.GridHelper(Math.max(C.MAP_W, C.MAP_H) * 0.02, 30, 0x2f5e2a, 0x2f5e2a);
    grid.position.y = 0.02;
    grid.material.opacity = 0.35;
    grid.material.transparent = true;
    scene.add(grid);

    state.areaMeshes = [];
    for (var i = 0; i < C.AREAS.length; i++) {
      var a = C.AREAS[i];
      var p = worldToThree(a.x + a.w / 2, a.y + a.h / 2);
      var mesh = new THREE.Mesh(
        new THREE.BoxGeometry(a.w * 0.02, 0.15, a.h * 0.02),
        new THREE.MeshStandardMaterial({
          color: hex(a.color),
          roughness: 0.85,
          transparent: true,
          opacity: 0.92,
        })
      );
      mesh.position.set(p.x, 0.08, p.z);
      mesh.receiveShadow = true;
      scene.add(mesh);
      var lab = labelSprite(a.name, "#ffffff");
      lab.position.set(p.x, 1.2, p.z);
      scene.add(lab);
    }

    buildCompound();
    buildTrack();
    buildPondLife();
    buildStarshipApproach();
    buildTrucks();

    // Hotspots (non-truck rings — trucks drawn as Cybertrucks)
    state.hotMeshes = [];
    for (var h = 0; h < C.HOTSPOTS.length; h++) {
      var hs = C.HOTSPOTS[h];
      if (C.isTruckHotspot && C.isTruckHotspot(hs)) continue;
      var hp = worldToThree(hs.x, hs.y);
      var ring = new THREE.Mesh(
        new THREE.RingGeometry(0.7, 0.95, 28),
        new THREE.MeshBasicMaterial({ color: 0xfbbf24, transparent: true, opacity: 0.45, side: THREE.DoubleSide })
      );
      ring.rotation.x = -Math.PI / 2;
      ring.position.set(hp.x, 0.06, hp.z);
      scene.add(ring);
      var hl = labelSprite(hs.label, "#fde68a");
      hl.position.set(hp.x, 1.7, hp.z);
      scene.add(hl);
      state.hotMeshes.push({ data: hs, ring: ring });
    }

    var ss = C.STARSHIP || { x: 360, y: 320 };
    var sp = worldToThree(ss.x, ss.y);
    var spotty = new THREE.Mesh(
      new THREE.SphereGeometry(0.3, 12, 10),
      new THREE.MeshStandardMaterial({ color: 0xfdba74 })
    );
    spotty.position.set(sp.x, 0.35, sp.z);
    scene.add(spotty);
    scene.add(labelSprite("Spotty", "#fdba74")).position.set(sp.x, 1.35, sp.z);

    var def = C.FROG_DEFS[state.frogId];
    var spawnW = (C.COMPOUND && C.COMPOUND.spawn) || { x: 280, y: 1750 };
    state.player = makeFrogMesh(def, 1);
    var spawn = worldToThree(spawnW.x, spawnW.y);
    state.player.position.set(spawn.x, 0, spawn.z);
    scene.add(state.player);
    state.nameTag = labelSprite(def.name, "#fff");
    state.nameTag.position.set(spawn.x, 1.85, spawn.z);
    scene.add(state.nameTag);

    state.driveTruck = makeTruckMesh(hex(def.color));
    state.driveTruck.visible = false;
    scene.add(state.driveTruck);
    state.waterPlane = new THREE.Mesh(
      new THREE.BoxGeometry(1.7, 0.08, 0.85),
      new THREE.MeshStandardMaterial({ color: 0x0e7490, transparent: true, opacity: 0.55 })
    );
    state.waterPlane.visible = false;
    scene.add(state.waterPlane);

    state.companions = [];
    for (var ci = 0; ci < C.FROG_ORDER.length; ci++) {
      var cid = C.FROG_ORDER[ci];
      if (cid === state.frogId) continue;
      var cmesh = makeFrogMesh(C.FROG_DEFS[cid], 0.85);
      var cp = worldToThree(spawnW.x + 40 + ci * 36, spawnW.y + 20 + (ci % 2) * 16);
      cmesh.position.set(cp.x, 0, cp.z);
      cmesh.userData.tx = cp.x;
      cmesh.userData.tz = cp.z;
      cmesh.userData.timer = 1 + Math.random();
      scene.add(cmesh);
      state.companions.push(cmesh);
    }

    state.vx = 0;
    state.vz = 0;
    state.inTruck = false;
    state.truckMode = null;
    state.truckId = null;
    state.waterSub = 0;
    state.zLift = 0;
    state.zVel = 0;
    state.scrap = 0;
    state.toast = "three.js ranch · compound · squiggle track · whales · Cybertrucks · Starship";
    state.toastT = 3.5;
    state.cd = 0;
    state.near = null;
    state.facing = 1;
    state.mode = "ranch";
    state.bob = 0;
  }

  function buildSpace() {
    // Clear ranch meshes by rebuilding scene
    while (scene.children.length) scene.remove(scene.children[0]);
    scene.background = new THREE.Color(0x030712);
    scene.fog = new THREE.FogExp2(0x030712, 0.012);

    var hemi = new THREE.HemisphereLight(0x93c5fd, 0x1e1b4b, 0.7);
    scene.add(hemi);
    var sun = new THREE.DirectionalLight(0xffffff, 0.6);
    sun.position.set(5, 10, 2);
    scene.add(sun);

    // Stars
    var starGeo = new THREE.BufferGeometry();
    var positions = new Float32Array(240);
    for (var i = 0; i < 80; i++) {
      positions[i * 3] = (Math.random() - 0.5) * 60;
      positions[i * 3 + 1] = (Math.random() - 0.5) * 40;
      positions[i * 3 + 2] = (Math.random() - 0.5) * 60;
    }
    starGeo.setAttribute("position", new THREE.BufferAttribute(positions, 3));
    scene.add(new THREE.Points(starGeo, new THREE.PointsMaterial({ color: 0xffffff, size: 0.12 })));

    // Moon platform
    var moon = new THREE.Mesh(
      new THREE.CircleGeometry(10, 48),
      new THREE.MeshStandardMaterial({ color: 0x94a3b8, roughness: 1 })
    );
    moon.rotation.x = -Math.PI / 2;
    scene.add(moon);

    var def = C.FROG_DEFS[state.frogId];
    state.player = makeFrogMesh(def, 1);
    state.player.position.set(-4, 0, 2);
    scene.add(state.player);
    state.nameTag = labelSprite(def.name, "#fff");
    scene.add(state.nameTag);

    state.jimmy = makeFrogMesh(C.FROG_DEFS.jimmy, 0.9);
    state.jimmy.position.set(4, 0, -2);
    scene.add(state.jimmy);
    state.jimmyLabel = labelSprite("Jimmy", "#fb923c");
    scene.add(state.jimmyLabel);
    state.jimmyVx = 2.2;
    state.jimmyVz = -1.4;
    state.planet = { id: "moon", name: "Moon", x: 5, z: -5, r: 1.2 };
    state.inOrbit = false;
    state.orbitAngle = 0;
    state.orbitRadius = 2.2;
    state.orbitEscapeCool = 0;
    state.orbitCfg = (C && C.ORBIT_PHYSICS) || {};
    state.toast = "Space · near Moon → orbit · Escape / hard thruster to leave";

    var germy = new THREE.Mesh(
      new THREE.SphereGeometry(0.28, 10, 8),
      new THREE.MeshStandardMaterial({ color: 0xb45309 })
    );
    germy.position.set(1, 0.28, 1.5);
    scene.add(germy);
    scene.add(labelSprite("Germy", "#fbbf24")).position.set(1, 1.1, 1.5);

    var daisy = new THREE.Mesh(
      new THREE.SphereGeometry(0.26, 10, 8),
      new THREE.MeshStandardMaterial({ color: 0xd6d3d1 })
    );
    daisy.position.set(2, 0.26, 2);
    scene.add(daisy);
    scene.add(labelSprite("Daisy", "#e7e5e4")).position.set(2, 1.05, 2);

    var spotty = new THREE.Mesh(
      new THREE.SphereGeometry(0.3, 10, 8),
      new THREE.MeshStandardMaterial({ color: 0xfdba74 })
    );
    spotty.position.set(-6, 0.3, -4);
    scene.add(spotty);
    scene.add(labelSprite("Spotty", "#fdba74")).position.set(-6, 1.2, -4);

    var pad = new THREE.Mesh(
      new THREE.CircleGeometry(1.2, 24),
      new THREE.MeshStandardMaterial({ color: 0x22c55e, emissive: 0x14532d, emissiveIntensity: 0.4 })
    );
    pad.rotation.x = -Math.PI / 2;
    pad.position.set(-7, 0.05, 5);
    scene.add(pad);
    scene.add(labelSprite("Ranch", "#bbf7d0")).position.set(-7, 1.2, 5);
    state.returnPad = { x: -7, z: 5 };

    state.vx = 0;
    state.vz = 0;
    state.catches = 0;
    state.toast = "Space · near Moon → orbit · Escape / hard thruster to leave";
    state.toastT = 3;
    state.mode = "space";
    state.near = null;
    state.inTruck = false;
    state.driveTruck = null;
    state.waterPlane = null;
    state.parkedTrucks = [];
    state.fish = [];
    state.whales = [];
    state.hotMeshes = [];
    state.companions = [];
    // Keep same locked isometric angle
    camera.position.set(12, 14, 12);
  }

  function resize() {
    if (!renderer || !camera) return;
    var w = window.innerWidth;
    var h = window.innerHeight;
    renderer.setSize(w, h, false);
    camera.aspect = w / Math.max(1, h);
    camera.updateProjectionMatrix();
  }

  function doInteract() {
    if (!state || !state.near) return;
    var id = state.near.id;
    if (state.mode === "ranch") {
      if (C.isTruckHotspot && C.isTruckHotspot(state.near)) {
        if (state.inTruck) {
          state.inTruck = false; state.truckMode = null; state.truckId = null;
          state.toast = "Hopped out";
        } else {
          state.inTruck = true; state.truckMode = state.near.mode || "solo"; state.truckId = id;
          state.scrap += 1;
          state.toast = state.truckMode === "shared"
            ? "All aboard! Four froggies · one Cybertruck · hit the jumps!"
            : "Driving Cybertruck · hit the jumps!";
        }
      } else if (id === "fishies") {
        state.toast = "Splash! Fishies & whales scatter";
        state.scrap += 2;
        for (var i = 0; i < state.fish.length; i++) {
          state.fish[i].userData.bx += (Math.random() - 0.5) * 2;
          state.fish[i].userData.bz += (Math.random() - 0.5) * 1.6;
        }
        for (var wi = 0; wi < (state.whales || []).length; wi++) {
          state.whales[wi].userData.bx += (Math.random() - 0.5) * 2.5;
          state.whales[wi].userData.bz += (Math.random() - 0.5) * 2;
        }
      } else if (id === "phone") {
        state.toast = "Purple Bear: Check SPS · find Jimmy!";
      } else if (id === "sps") {
        state.toast = "SPS · Solar Positioning System (stub on three.js)";
      } else if (id === "starship") {
        state.toast = "Spotty: Launch!";
        buildSpace();
      }
      state.toastT = 2.5;
    } else {
      if (id === "jimmy") {
        state.catches++;
        state.jimmy.position.set((Math.random() - 0.5) * 8, 0, (Math.random() - 0.5) * 8);
        state.toast = "Caught Jimmy! ×" + state.catches;
        state.toastT = 2.5;
      } else if (id === "return") {
        buildRanch();
        state.toast = "Back at ranch";
        state.toastT = 2;
      }
    }
    if (hooks.onToast) hooks.onToast(state.toast);
  }

  function doAbility() {
    if (!state || state.cd > 0) return;
    state.cd = 5;
    var def = C.FROG_DEFS[state.frogId];
    if (state.frogId === "james") {
      state.vx += state.facing * (state.inTruck ? 8 : 5);
      state.toast = state.inTruck ? "DASH · truck boost!" : "DASH!";
    } else if (state.frogId === "jimmy") {
      state.toast = "SHIELD up!";
    } else if (state.frogId === "bubbles") {
      state.toast = "ZAP!";
      state.scrap += 1;
    } else {
      state.toast = "BOT · nudge toward SPS";
      var target = worldToThree(280, 400);
      state.player.position.x += (target.x - state.player.position.x) * 0.2;
      state.player.position.z += (target.z - state.player.position.z) * 0.2;
    }
    if (state.mode === "space") {
      if (state.inOrbit) {
        state.inOrbit = false;
        state.orbitEscapeCool = 1.4;
        var kick = ((state.orbitCfg && state.orbitCfg.hardThrustImpulse) || 320) * 0.02;
        state.vx = Math.cos(state.orbitAngle || 0) * kick;
        state.vz = Math.sin(state.orbitAngle || 0) * kick;
        state.toast = "Hard thruster · left Moon orbit";
      } else {
        state.vx += 3;
        state.vz -= 2;
        state.toast = def.ability + " · hard thruster";
      }
    }
    state.toastT = 1.8;
    if (hooks.onToast) hooks.onToast(state.toast);
  }

  function tick() {
    if (!active || !state) return;
    raf = requestAnimationFrame(tick);
    var dt = Math.min(0.05, clock.getDelta());
    state.cd = Math.max(0, state.cd - dt);
    state.toastT = Math.max(0, state.toastT - dt);
    state.bob += dt * 10;

    if (state.mode === "space" && state.planet) {
      var cfg = state.orbitCfg || {};
      var capR = (cfg.captureRadius || 120) * 0.03;
      var softR = (cfg.softPullRadius || 220) * 0.03;
      var alt = state.orbitRadius || 2.2;
      var pullA = (cfg.pullAccel || 420) * 0.02;
      if (state.orbitEscapeCool > 0) state.orbitEscapeCool -= dt;
      var dx = state.player.position.x - state.planet.x;
      var dz = state.player.position.z - state.planet.z;
      var dP = Math.hypot(dx, dz);
      if (state.inOrbit) {
        state.orbitRadius = Math.max(1.4, Math.min(softR * 0.9, state.orbitRadius + (steer.y || 0) * 1.2 * dt));
        state.orbitAngle += (0.85 + (steer.x || 0) * 0.35) * dt;
        state.player.position.x = state.planet.x + Math.cos(state.orbitAngle) * state.orbitRadius;
        state.player.position.z = state.planet.z + Math.sin(state.orbitAngle) * state.orbitRadius;
        state.vx = 0; state.vz = 0;
      } else if (state.orbitEscapeCool <= 0) {
        if (dP < softR && dP > 0.2) {
          var ang = Math.atan2(dz, dx);
          var pull = pullA * (1 - dP / softR) * dt;
          state.vx -= Math.cos(ang) * pull;
          state.vz -= Math.sin(ang) * pull;
        }
        if (dP < capR) {
          state.inOrbit = true;
          state.orbitAngle = Math.atan2(dz, dx);
          state.orbitRadius = 2.2;
          state.vx = 0; state.vz = 0;
          state.toast = "Orbit locked · Moon · Escape or hard thruster to leave";
          state.toastT = 3;
        }
      }
    }

    var maxSp = state.mode === "space" ? 7 : state.inTruck ? 9 : 5.5;
    var accel = state.mode === "space" ? 14 : state.inTruck ? 18 : 14;
    var fric = state.mode === "space" ? 2.8 : state.inTruck ? 2.6 : 4.5;

    // Map screen WASD/D-pad → ground plane relative to locked camera
    // Canvas convention: steer.y < 0 = Up/W (screen up). Camera sits at +X+Z offset.
    // Into-scene (screen up) = (-1,-1) on XZ; screen-right = (+1,-1) on XZ.
    // Ben orbit: when locked, position is owned by orbit tick above
    if (!(state.mode === "space" && state.inOrbit)) {
    if (steer.x || steer.y) {
      var len = Math.hypot(steer.x, steer.y) || 1;
      var ix = steer.x / len;
      var iy = steer.y / len; // Up/W is negative
      var inv = 0.70710678;
      var fx = -inv, fz = -inv; // screen up / into scene
      var rx = inv, rz = -inv;  // screen right
      var mx = rx * ix + fx * (-iy);
      var mz = rz * ix + fz * (-iy);
      state.vx += mx * accel * dt;
      state.vz += mz * accel * dt;
      if (Math.abs(mx) + Math.abs(mz) > 0.01) state.facing = mx >= 0 ? 1 : -1;
    }
    state.vx *= Math.max(0, 1 - fric * dt);
    state.vz *= Math.max(0, 1 - fric * dt);
    var sp = Math.hypot(state.vx, state.vz);
    if (sp > maxSp) {
      state.vx = (state.vx / sp) * maxSp;
      state.vz = (state.vz / sp) * maxSp;
    }
    state.player.position.x += state.vx * dt;
    state.player.position.z += state.vz * dt;
    } else {
      var sp = 0;
    }
    state.player.position.y = Math.abs(Math.sin(state.bob)) * (sp > 0.5 ? 0.06 : 0.02);
    state.player.scale.x = state.facing >= 0 ? 1 : -1;

    /* Ranch truck water / ramp / shared pile-in (parity) — after steer, before clamp */
    if (state.mode === "ranch") {
      var wpos0 = threeToWorld(state.player.position.x, state.player.position.z);
      if (state.inTruck && C.rampAt) {
        var ramp = C.rampAt(wpos0.x, wpos0.y);
        if (ramp && sp > 1.2) {
          state.zVel = Math.max(state.zVel || 0, 4.5 * (ramp.boost || 1.3));
          state.scrap += 0.02;
        }
      }
      state.zVel = (state.zVel || 0) - 14 * dt;
      state.zLift = Math.max(0, (state.zLift || 0) + state.zVel * dt);
      if (state.zLift <= 0) { state.zLift = 0; state.zVel = 0; }
      var wet = C.inPond && C.inPond(wpos0.x, wpos0.y);
      if (state.inTruck && wet) {
        var plunge = Math.max(0, -state.zVel) + (state.zLift > 0.4 ? 1 : 0);
        state.waterSub = Math.min(1.15, 0.45 + plunge * 0.2);
        state.vx *= Math.max(0, 1 - 1.5 * dt);
        state.vz *= Math.max(0, 1 - 1.5 * dt);
      } else {
        state.waterSub = Math.max(0, (state.waterSub || 0) - dt * 1.5);
      }
      state.player.visible = !state.inTruck;
      if (state.driveTruck) {
        state.driveTruck.visible = !!state.inTruck;
        if (state.inTruck) {
          state.driveTruck.position.set(state.player.position.x, 0.05 + state.zLift * 0.08, state.player.position.z);
          state.driveTruck.scale.x = state.facing >= 0 ? 1 : -1;
          var dive = state.waterSub > 0.7;
          if (state.driveTruck.userData.bodyMat) {
            state.driveTruck.userData.bodyMat.color.setHex(dive ? 0x64748b : 0x9ca3af);
            state.driveTruck.userData.bodyMat.opacity = dive ? 0.72 : 1;
            state.driveTruck.userData.bodyMat.transparent = dive;
          }
        }
      }
      if (state.waterPlane) {
        state.waterPlane.visible = !!(state.inTruck && wet);
        if (wet && state.inTruck) {
          state.waterPlane.position.set(state.player.position.x, 0.12 + state.waterSub * 0.08, state.player.position.z);
          state.waterPlane.material.opacity = 0.35 + state.waterSub * 0.4;
        }
      }
      for (var pti = 0; pti < (state.parkedTrucks || []).length; pti++) {
        var pt = state.parkedTrucks[pti];
        var hid = pt.spot.id === "shared" ? "truck-shared" : "truck-" + pt.spot.id;
        var taken = state.inTruck && (state.truckId === hid || (state.truckMode === "shared" && pt.spot.id === "shared"));
        pt.mesh.visible = !taken;
        if (pt.label) pt.label.visible = !taken;
      }
      state.player.position.y = (state.zLift || 0) * 0.08 + Math.abs(Math.sin(state.bob)) * (sp > 0.5 ? 0.06 : 0.02);
    }

    // Clamp ranch bounds
    if (state.mode === "ranch") {
      var halfW = C.MAP_W * 0.01;
      var halfH = C.MAP_H * 0.01;
      state.player.position.x = Math.max(-halfW + 0.5, Math.min(halfW - 0.5, state.player.position.x));
      state.player.position.z = Math.max(-halfH + 0.5, Math.min(halfH - 0.5, state.player.position.z));
    } else {
      state.player.position.x = Math.max(-9, Math.min(9, state.player.position.x));
      state.player.position.z = Math.max(-9, Math.min(9, state.player.position.z));
    }

    if (state.nameTag) {
      state.nameTag.position.set(state.player.position.x, 1.85, state.player.position.z);
    }

    // Locked orbit follow — camera offset fixed, no orbit controls / no FPS look
    var target = camera.userData.lockTarget;
    target.x += (state.player.position.x - target.x) * Math.min(1, 3 * dt);
    target.z += (state.player.position.z - target.z) * Math.min(1, 3 * dt);
    target.y = 0;
    var camDist = state.mode === "ranch" ? 20 : 14;
    var camH = state.mode === "ranch" ? 24 : 16;
    camera.position.set(target.x + camDist * 0.85, camH, target.z + camDist * 0.85);
    camera.lookAt(target);

    if (state.mode === "ranch") {
      for (var fi = 0; fi < (state.fish || []).length; fi++) {
        var fish = state.fish[fi];
        fish.userData.phase += dt * 2;
        fish.position.x = fish.userData.bx + Math.sin(fish.userData.phase) * 0.4;
        fish.position.z = fish.userData.bz + Math.cos(fish.userData.phase * 0.7) * 0.28;
      }
      for (var wj = 0; wj < (state.whales || []).length; wj++) {
        var wh = state.whales[wj];
        wh.userData.phase += dt * 0.7;
        wh.position.x = wh.userData.bx + Math.sin(wh.userData.phase) * 0.9;
        wh.position.z = wh.userData.bz + Math.cos(wh.userData.phase * 0.55) * 0.55;
      }
      for (var ci = 0; ci < state.companions.length; ci++) {
        var c = state.companions[ci];
        if (state.inTruck && state.truckMode === "shared") {
          var ox = (ci - 1) * 0.35, oz = -0.25 - (ci % 2) * 0.2;
          c.position.x += (state.player.position.x + ox - c.position.x) * Math.min(1, 8 * dt);
          c.position.z += (state.player.position.z + oz - c.position.z) * Math.min(1, 8 * dt);
          c.position.y = 0.55 + (state.zLift || 0) * 0.08;
        } else {
          c.position.y = Math.abs(Math.sin(state.bob + ci)) * 0.04;
          c.userData.timer -= dt;
          if (c.userData.timer <= 0) {
            c.userData.tx = c.position.x + (Math.random() - 0.5) * 4;
            c.userData.tz = c.position.z + (Math.random() - 0.5) * 4;
            c.userData.timer = 1.5 + Math.random() * 2;
          }
          c.position.x += (c.userData.tx - c.position.x) * Math.min(1, 1.2 * dt);
          c.position.z += (c.userData.tz - c.position.z) * Math.min(1, 1.2 * dt);
        }
      }
      var wpos = threeToWorld(state.player.position.x, state.player.position.z);
      state.near = C.nearestHotspot(wpos.x, wpos.y, 70);
      for (var hi = 0; hi < state.hotMeshes.length; hi++) {
        var hg = state.hotMeshes[hi];
        hg.ring.material.opacity = state.near && state.near.id === hg.data.id ? 0.85 : 0.35;
      }
    } else {
      state.jimmy.position.x += state.jimmyVx * dt;
      state.jimmy.position.z += state.jimmyVz * dt;
      if (Math.abs(state.jimmy.position.x) > 8) state.jimmyVx *= -1;
      if (Math.abs(state.jimmy.position.z) > 8) state.jimmyVz *= -1;
      state.jimmy.position.y = 0.15 + Math.abs(Math.sin(state.bob * 1.4)) * 0.35;
      if (state.jimmyLabel) {
        state.jimmyLabel.position.set(state.jimmy.position.x, 1.8, state.jimmy.position.z);
      }
      var dJ = state.player.position.distanceTo(state.jimmy.position);
      var dR = Math.hypot(state.player.position.x - state.returnPad.x, state.player.position.z - state.returnPad.z);
      state.near = null;
      if (dJ < 1.1) state.near = { id: "jimmy", tip: "Catch Jimmy!" };
      else if (dR < 1.6) state.near = { id: "return", tip: "Return to ranch" };
    }

    if (wantInteract) {
      wantInteract = false;
      doInteract();
    }
    if (wantAbility) {
      wantAbility = false;
      doAbility();
    }

    renderer.render(scene, camera);

    if (hooks.onHud) {
      var def = C.FROG_DEFS[state.frogId];
      var label;
      if (state.mode === "space") label = "Space · Moon · three.js";
      else {
        var wp = threeToWorld(state.player.position.x, state.player.position.z);
        label = C.areaNameAt(wp.x, wp.y) + " · three.js";
      }
      hooks.onHud({
        mode: state.mode,
        label: label,
        scrap: state.mode === "space" ? state.catches : state.scrap,
        tip: state.toastT > 0 ? state.toast : state.inOrbit ? "Orbit locked · Escape or hard thruster" : state.near ? state.near.tip + " · INTERACT" : "",
            inOrbit: !!state.inOrbit,
        near: state.near,
        ability: def.ability,
        cd: state.cd,
        walk: (function () {
          if (state.mode === "space") return state.inOrbit ? "🌍 Orbit" : "🚀 Space";
          if (!state.inTruck) return "🐸 Walk";
          var wp2 = threeToWorld(state.player.position.x, state.player.position.z);
          var wet2 = C.inPond && C.inPond(wp2.x, wp2.y);
          if ((state.zLift || 0) > 0.5) return "🚚 AIR!";
          if (wet2 && state.waterSub > 0.75) return "🚚 Under";
          if (wet2) return "🚚 On water";
          return state.truckMode === "shared" ? "🚚 All aboard" : "🚚 Drive";
        })(),
      });
    }
  }

  function boot(opts) {
    destroy();
    hooks = opts || {};
    if (typeof THREE === "undefined") {
      console.error("three.js CDN not loaded");
      if (hooks.onToast) hooks.onToast("three.js failed to load");
      return;
    }
    var host = document.getElementById("engine-host");
    if (!host) {
      console.error("engine-host missing");
      return;
    }
    host.hidden = false;
    host.innerHTML = "";
    var view = document.getElementById("view");
    if (view) view.style.display = "none";

    state = {
      frogId: (opts && opts.frogId) || "james",
    };

    renderer = new THREE.WebGLRenderer({ antialias: true, alpha: false, powerPreference: "high-performance" });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    renderer.setSize(window.innerWidth, window.innerHeight, false);
    renderer.shadowMap.enabled = true;
    host.appendChild(renderer.domElement);
    renderer.domElement.style.display = "block";
    renderer.domElement.style.width = "100%";
    renderer.domElement.style.height = "100%";
    renderer.domElement.style.touchAction = "none";

    clock = new THREE.Clock();
    buildRanch();
    active = true;
    window.addEventListener("resize", resize);
    if (hooks.onReady) hooks.onReady({ engine: "three", frogId: state.frogId });
    if (hooks.onToast) hooks.onToast(state.toast);
    tick();
  }

  // Cleanup resize on destroy
  var _destroy = destroy;
  destroy = function () {
    window.removeEventListener("resize", resize);
    _destroy();
  };

  function leaveOrbitThree() {
    if (!state || state.mode !== "space" || !state.inOrbit) return false;
    state.inOrbit = false;
    state.orbitEscapeCool = 1.4;
    var kick = ((state.orbitCfg && state.orbitCfg.hardThrustImpulse) || 320) * 0.02;
    state.vx = Math.cos(state.orbitAngle || 0) * kick;
    state.vz = Math.sin(state.orbitAngle || 0) * kick;
    state.toast = "Escape · left Moon orbit";
    state.toastT = 2.2;
    return true;
  }

  global.FroggiesThree = {
    boot: boot,
    destroy: destroy,
    setSteer: setSteer,
    pulseInteract: pulseInteract,
    pulseAbility: pulseAbility,
    isActive: isActive,
    leaveOrbit: leaveOrbitThree,
  };
})(typeof window !== "undefined" ? window : globalThis);
