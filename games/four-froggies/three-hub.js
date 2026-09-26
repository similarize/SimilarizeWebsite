/* Four Froggies — three.js hub path (CDN). Fixed-angle 2.5D-ish (orbit locked / isometric-ish).
   NOT free-fly FPS. Same Ben cast/areas; solo-first. Ranch + thin space stub. */
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

  function buildRanch() {
    scene = new THREE.Scene();
    scene.background = new THREE.Color(0x87b5d9);
    scene.fog = new THREE.Fog(0x87b5d9, 28, 70);

    // Fixed-angle isometric-ish camera — orbit LOCKED (no free-fly)
    var aspect = window.innerWidth / Math.max(1, window.innerHeight);
    camera = new THREE.PerspectiveCamera(42, aspect, 0.1, 200);
    camera.position.set(14, 16, 14);
    camera.lookAt(0, 0, 0);
    camera.userData.lockTarget = new THREE.Vector3(0, 0, 0);

    var hemi = new THREE.HemisphereLight(0xfff0d0, 0x3a5a2a, 0.85);
    scene.add(hemi);
    var sun = new THREE.DirectionalLight(0xffe6b0, 0.95);
    sun.position.set(12, 22, 8);
    sun.castShadow = true;
    sun.shadow.mapSize.set(1024, 1024);
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

    // Toys density
    for (var t = 0; t < 10; t++) {
      var tp = worldToThree(120 + (t % 5) * 55, 450 + Math.floor(t / 5) * 50);
      var toy = new THREE.Mesh(
        new THREE.BoxGeometry(0.25, 0.25, 0.25),
        new THREE.MeshStandardMaterial({ color: 0xfbbf24 })
      );
      toy.position.set(tp.x, 0.2, tp.z);
      scene.add(toy);
    }

    // Pond fishies
    state.fish = [];
    for (var f = 0; f < 8; f++) {
      var fp = worldToThree(580 + f * 50, 120 + (f % 3) * 40);
      var fish = new THREE.Mesh(
        new THREE.SphereGeometry(0.18, 8, 6),
        new THREE.MeshStandardMaterial({ color: 0x67e8f9, emissive: 0x164e63, emissiveIntensity: 0.2 })
      );
      fish.position.set(fp.x, 0.15, fp.z);
      fish.userData.phase = Math.random() * Math.PI * 2;
      fish.userData.bx = fp.x;
      fish.userData.bz = fp.z;
      scene.add(fish);
      state.fish.push(fish);
    }

    // Hotspots
    state.hotMeshes = [];
    for (var h = 0; h < C.HOTSPOTS.length; h++) {
      var hs = C.HOTSPOTS[h];
      var hp = worldToThree(hs.x, hs.y);
      var ring = new THREE.Mesh(
        new THREE.RingGeometry(0.55, 0.75, 24),
        new THREE.MeshBasicMaterial({ color: 0xfbbf24, transparent: true, opacity: 0.45, side: THREE.DoubleSide })
      );
      ring.rotation.x = -Math.PI / 2;
      ring.position.set(hp.x, 0.05, hp.z);
      scene.add(ring);
      var hl = labelSprite(hs.label, "#fde68a");
      hl.position.set(hp.x, 1.6, hp.z);
      scene.add(hl);
      state.hotMeshes.push({ data: hs, ring: ring });
    }

    // Spotty at starship
    var sp = worldToThree(140, 110);
    var spotty = new THREE.Mesh(
      new THREE.SphereGeometry(0.28, 10, 8),
      new THREE.MeshStandardMaterial({ color: 0xfdba74 })
    );
    spotty.position.set(sp.x, 0.35, sp.z);
    scene.add(spotty);
    var sl = labelSprite("Spotty", "#fdba74");
    sl.position.set(sp.x, 1.3, sp.z);
    scene.add(sl);

    // Player + AI
    var def = C.FROG_DEFS[state.frogId];
    state.player = makeFrogMesh(def, 1);
    var spawn = worldToThree(220, 420);
    state.player.position.set(spawn.x, 0, spawn.z);
    scene.add(state.player);
    state.nameTag = labelSprite(def.name, "#fff");
    state.nameTag.position.set(spawn.x, 1.8, spawn.z);
    scene.add(state.nameTag);

    state.companions = [];
    for (var ci = 0; ci < C.FROG_ORDER.length; ci++) {
      var cid = C.FROG_ORDER[ci];
      if (cid === state.frogId) continue;
      var cmesh = makeFrogMesh(C.FROG_DEFS[cid], 0.85);
      var cp = worldToThree(240 + ci * 40, 440 + (ci % 2) * 20);
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
    state.scrap = 0;
    state.toast = "three.js ranch · fixed-angle cam · Starship (Spotty)";
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
    state.toast = "Space stub · catch Jimmy · return pad → ranch";
    state.toastT = 3;
    state.mode = "space";
    state.near = null;
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
      if (id === "truck") {
        state.inTruck = !state.inTruck;
        state.toast = state.inTruck ? "Cybertruck · track jumps!" : "Hopped out";
        state.scrap += state.inTruck ? 1 : 0;
      } else if (id === "fishies") {
        state.toast = "Splash! Fishies scatter";
        state.scrap += 2;
        for (var i = 0; i < state.fish.length; i++) {
          state.fish[i].userData.bx += (Math.random() - 0.5) * 1.5;
          state.fish[i].userData.bz += (Math.random() - 0.5) * 1.2;
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

    // Map screen WASD → ground plane relative to locked camera forward
    // Ben orbit: when locked, position is owned by orbit tick above
    if (!(state.mode === "space" && state.inOrbit)) {
    if (steer.x || steer.y) {
      var len = Math.hypot(steer.x, steer.y) || 1;
      var ix = steer.x / len;
      var iy = steer.y / len;
      var mx = (ix - iy) * 0.707;
      var mz = (ix + iy) * 0.707;
      state.vx += mx * accel * dt;
      state.vz += mz * accel * dt;
      if (steer.x) state.facing = steer.x > 0 ? 1 : -1;
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
    camera.position.set(target.x + 14, 16, target.z + 14);
    camera.lookAt(target);

    if (state.mode === "ranch") {
      for (var fi = 0; fi < state.fish.length; fi++) {
        var fish = state.fish[fi];
        fish.userData.phase += dt * 2;
        fish.position.x = fish.userData.bx + Math.sin(fish.userData.phase) * 0.35;
        fish.position.z = fish.userData.bz + Math.cos(fish.userData.phase * 0.7) * 0.25;
      }
      for (var ci = 0; ci < state.companions.length; ci++) {
        var c = state.companions[ci];
        c.userData.timer -= dt;
        if (c.userData.timer <= 0) {
          c.userData.tx = c.position.x + (Math.random() - 0.5) * 3;
          c.userData.tz = c.position.z + (Math.random() - 0.5) * 3;
          c.userData.timer = 1.5 + Math.random() * 2;
        }
        c.position.x += (c.userData.tx - c.position.x) * Math.min(1, 1.2 * dt);
        c.position.z += (c.userData.tz - c.position.z) * Math.min(1, 1.2 * dt);
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
        walk: state.mode === "space" ? "🚀 Space" : state.inTruck ? "🚚 Drive" : "🐸 Walk",
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
  };
})(typeof window !== "undefined" ? window : globalThis);
