/* Four Froggies — three.js hub (CDN). Fixed-angle 2.5D-ish (orbit locked / isometric-ish).
   NOT free-fly FPS. Canvas-parity landmarks · solo-first.
   Big map · compound · squiggle track · pond whales · 4 trucks+shared ·
   on-water/under · Starship → Escape/hard thruster.
   polish4: compound presence + hills + inviting hotspots + truck bob/spray.
   polish5: ambient pollen/fireflies; pond ripples; track race dust; garage door open-near;
   shared ALL ABOARD; land shake; hotspot sparkle; orbit pull rings + Escape banner.
   polish6: depth shadows + parallax-lite hills + Mars invader silhouette tease + mech wow tip.
   polish7: zone signs + mini-map lite + companion idle bounce / follow lag. Hollow house + frogs kept.
   polish8: truck silhouette + house porch + whale breach + destination beacon. Hollow house + frogs kept.
   polish9: color nameplates; aboard icons; track gate; Optimus punch lite. Hollow house + frogs kept.
   polish10: quieter UI; exit truck anytime; friction/cam; particle caps; dusk sky. Hollow house + frogs + WASD kept.
   solid1: floor z-fight fix; solid walls/mechs/trucks; cast names only on plates.
   tapsteer1: mech pad z-fight fix; faster walk/drive; hold-to-aim tap/click steer + marker.
   eyes1: yaw frog (eyes on +Z) toward walk dir; idle keeps last; AI companions too.
   truck1: kid-toy truck scale; smooth yaw drive toward aim; track elev / crest / land bounce.
   WASD camera-relative — do not invert. */
(function (global) {
  "use strict";

  var C = global.FroggiesCanon;
  var active = false;
  var hooks = {};
  var keySteer = { x: 0, y: 0 };
  var tapSteer = { x: 0, y: 0 };
  var tapHeld = false;
  var tapMarker = null; /* { sx, sy, life, ang } screen px in canvas */
  var wantInteract = false;
  var wantAbility = false;

  function mergedSteer() {
    if (keySteer.x || keySteer.y) return keySteer;
    return tapSteer;
  }

  var renderer = null;
  var scene = null;
  var camera = null;
  var raf = 0;
  var state = null;
  var clock = null;

  function destroy() {
    active = false;
    keySteer.x = keySteer.y = 0; tapSteer.x = tapSteer.y = 0; tapHeld = false; tapMarker = null;
    wantInteract = wantAbility = false;
    var mel = document.getElementById("tap-steer-marker");
    if (mel) mel.classList.remove("is-on");
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
    keySteer.x = x;
    keySteer.y = y;
  }
  function pulseInteract() { wantInteract = true; }
  function pulseAbility() { wantAbility = true; }
  function isActive() { return active; }

  function hex(c) {
    return parseInt(String(c).replace("#", ""), 16);
  }

  function makeFrogMesh(def, scale) {
    var s = (scale == null ? 1.55 : scale); // default bigger — readable from isometric cam
    var g = new THREE.Group();
    var bodyCol = hex(def.color);
    var body = new THREE.Mesh(
      new THREE.SphereGeometry(0.55 * s, 16, 12),
      new THREE.MeshStandardMaterial({
        color: bodyCol, roughness: 0.45, metalness: 0.08,
        emissive: bodyCol, emissiveIntensity: 0.35,
      })
    );
    body.position.y = 0.55 * s;
    body.castShadow = true;
    g.add(body);
    // Belly highlight
    var belly = new THREE.Mesh(
      new THREE.SphereGeometry(0.32 * s, 10, 8),
      new THREE.MeshStandardMaterial({ color: 0xfef3c7, roughness: 0.7 })
    );
    belly.position.set(0, 0.42 * s, 0.28 * s);
    belly.scale.set(1, 0.85, 0.55);
    g.add(belly);
    // Hat (distinct per frog)
    var hat = new THREE.Mesh(
      new THREE.ConeGeometry(0.34 * s, 0.42 * s, 8),
      new THREE.MeshStandardMaterial({
        color: hex(def.hat), roughness: 0.55,
        emissive: hex(def.hat), emissiveIntensity: 0.15,
      })
    );
    hat.position.y = 1.15 * s;
    hat.castShadow = true;
    g.add(hat);
    // Eyes (white + accent pupil)
    var eyeWhite = new THREE.MeshStandardMaterial({ color: 0xffffff, emissive: 0xffffff, emissiveIntensity: 0.1 });
    var eyePupil = new THREE.MeshStandardMaterial({ color: hex(def.accent) });
    function eye(ox) {
      var ew = new THREE.Mesh(new THREE.SphereGeometry(0.14 * s, 8, 8), eyeWhite);
      ew.position.set(ox, 0.68 * s, 0.42 * s);
      g.add(ew);
      var ep = new THREE.Mesh(new THREE.SphereGeometry(0.065 * s, 6, 6), eyePupil);
      ep.position.set(ox, 0.68 * s, 0.52 * s);
      g.add(ep);
    }
    eye(-0.18 * s); eye(0.18 * s);
    // Legs
    var legM = new THREE.MeshStandardMaterial({ color: bodyCol, roughness: 0.55 });
    for (var li = 0; li < 4; li++) {
      var leg = new THREE.Mesh(new THREE.CylinderGeometry(0.07 * s, 0.09 * s, 0.28 * s, 6), legM);
      var side = li < 2 ? -1 : 1;
      var fore = li % 2 === 0 ? 1 : -1;
      leg.position.set(side * 0.28 * s, 0.18 * s, fore * 0.22 * s);
      leg.rotation.z = side * 0.35;
      g.add(leg);
    }
    // Ground selection ring (player/AI readable)
    var ring = new THREE.Mesh(
      new THREE.RingGeometry(0.55 * s, 0.72 * s, 28),
      new THREE.MeshBasicMaterial({
        color: bodyCol, transparent: true, opacity: 0.55, side: THREE.DoubleSide, depthWrite: false,
      })
    );
    ring.rotation.x = -Math.PI / 2;
    ring.position.y = 0.09;
    ring.renderOrder = 2;
    g.add(ring);
    g.userData.ring = ring;
    g.userData.frogId = def.id;
    g.castShadow = true;
    return g;
  }

  function labelSprite(text, color) {
    /* polish9/10: quieter plate nameplates */
    var canvas = document.createElement("canvas");
    canvas.width = 256;
    canvas.height = 64;
    var ctx = canvas.getContext("2d");
    ctx.clearRect(0, 0, 256, 64);
    ctx.font = "bold 22px Segoe UI, system-ui, sans-serif";
    ctx.textAlign = "center";
    var tw = Math.min(200, ctx.measureText(text).width + 22);
    ctx.fillStyle = "rgba(15, 23, 42, 0.62)";
    ctx.strokeStyle = color || "#fef3c7";
    ctx.lineWidth = 2;
    ctx.beginPath();
    if (ctx.roundRect) ctx.roundRect(128 - tw * 0.5, 18, tw, 28, 8);
    else ctx.rect(128 - tw * 0.5, 18, tw, 28);
    ctx.fill(); ctx.stroke();
    ctx.fillStyle = color || "#fff";
    ctx.fillText(text, 128, 38);
    var tex = new THREE.CanvasTexture(canvas);
    var mat = new THREE.SpriteMaterial({ map: tex, transparent: true, depthTest: false, opacity: 0.85 });
    var spr = new THREE.Sprite(mat);
    spr.scale.set(1.9, 0.48, 1);
    return spr;
  }

  /* NEVER chain .position on scene.add() — Object3D.add returns the scene, which
     silently moved scene.position and desynced the camera from all meshes. */
  function addLabel(text, color, x, y, z) {
    var spr = labelSprite(text, color);
    spr.position.set(x, y, z);
    scene.add(spr);
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
    /* polish8: angular stainless Cybertruck — light bar + wheel arches */
    var g = new THREE.Group();
    var bodyMat = new THREE.MeshStandardMaterial({ color: 0xc5ced8, metalness: 0.78, roughness: 0.22 });
    var body = new THREE.Mesh(new THREE.BoxGeometry(1.85, 0.36, 0.82), bodyMat);
    body.position.set(0.02, 0.34, 0); body.castShadow = true; g.add(body);
    /* Steep wedge nose */
    var nose = new THREE.Mesh(
      new THREE.BoxGeometry(0.72, 0.2, 0.78),
      new THREE.MeshStandardMaterial({ color: 0xa8b4c4, metalness: 0.8, roughness: 0.2 })
    );
    nose.position.set(0.95, 0.3, 0); nose.rotation.z = -0.32; g.add(nose);
    /* Cabin glass tint via accent */
    var cab = new THREE.Mesh(
      new THREE.BoxGeometry(0.58, 0.38, 0.68),
      new THREE.MeshStandardMaterial({ color: accentHex, metalness: 0.45, roughness: 0.3, emissive: accentHex, emissiveIntensity: 0.14 })
    );
    cab.position.set(0.12, 0.62, 0); cab.rotation.z = -0.08; g.add(cab);
    /* Bed rails */
    var railM = new THREE.MeshStandardMaterial({ color: 0x64748b, metalness: 0.55, roughness: 0.35 });
    var railL = new THREE.Mesh(new THREE.BoxGeometry(0.75, 0.1, 0.05), railM);
    railL.position.set(-0.5, 0.5, 0.36); g.add(railL);
    var railR = railL.clone(); railR.position.z = -0.36; g.add(railR);
    /* Full-width light bar */
    var hl = new THREE.Mesh(
      new THREE.BoxGeometry(0.1, 0.1, 0.7),
      new THREE.MeshStandardMaterial({ color: 0xfef08a, emissive: 0xfbbf24, emissiveIntensity: 0.65 })
    );
    hl.position.set(1.28, 0.32, 0); g.add(hl);
    /* Wheel arches (half-torus flares) */
    var archM = new THREE.MeshStandardMaterial({ color: 0x94a3b8, metalness: 0.6, roughness: 0.35 });
    function arch(x, z) {
      var a = new THREE.Mesh(new THREE.TorusGeometry(0.22, 0.03, 6, 10, Math.PI), archM);
      a.rotation.y = Math.PI / 2; a.rotation.z = Math.PI; a.position.set(x, 0.22, z); g.add(a);
    }
    arch(-0.5, 0.42); arch(-0.5, -0.42); arch(0.55, 0.42); arch(0.55, -0.42);
    /* Wheels */
    var wheelM = new THREE.MeshStandardMaterial({ color: 0x0f172a, roughness: 0.8 });
    function wheel(x, z) {
      var w = new THREE.Mesh(new THREE.CylinderGeometry(0.18, 0.18, 0.14, 10), wheelM);
      w.rotation.z = Math.PI / 2; w.position.set(x, 0.16, z); g.add(w);
    }
    wheel(-0.5, 0.42); wheel(-0.5, -0.42); wheel(0.55, 0.42); wheel(0.55, -0.42);
    var edge = new THREE.LineSegments(
      new THREE.EdgesGeometry(new THREE.BoxGeometry(1.85, 0.36, 0.82)),
      new THREE.LineBasicMaterial({ color: 0x111827 })
    );
    edge.position.copy(body.position); g.add(edge);
    g.userData.bodyMat = bodyMat;
    /* truck1: kid-toy scale vs frog (~1.7 diam) — bigger than frog, not a building */
    var ts = (C.TRUCK_VIS && C.TRUCK_VIS.threeScale != null) ? C.TRUCK_VIS.threeScale : 2.05;
    g.scale.setScalar(ts);
    g.userData.truckScale = ts;
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
    if (m.stories >= 1000) {
      var haze = new THREE.Mesh(
        new THREE.SphereGeometry(h * 0.55, 12, 10),
        new THREE.MeshBasicMaterial({ color: 0xfbbf24, transparent: true, opacity: 0.12, depthWrite: false })
      );
      haze.position.set(p.x, h * 0.55, p.z); scene.add(haze);
    }
    /* tapsteer1: flat raised pad (not thick cyl) — was z-fighting backyard/ground while walking past */
    var padR = h * 0.34;
    var padY = m.stories >= 1000 ? 0.18 : 0.14;
    var pad = new THREE.Mesh(
      new THREE.CircleGeometry(padR, 28),
      new THREE.MeshStandardMaterial({
        color: 0x1e293b, metalness: 0.45, roughness: 0.65,
        polygonOffset: true, polygonOffsetFactor: -3, polygonOffsetUnits: -3,
        depthWrite: false, transparent: true, opacity: 0.96,
        side: THREE.DoubleSide,
      })
    );
    pad.rotation.x = -Math.PI / 2;
    pad.position.set(p.x, padY, p.z);
    pad.receiveShadow = false;
    pad.renderOrder = 3;
    scene.add(pad);
    var padRing = new THREE.Mesh(
      new THREE.RingGeometry(padR * 0.7, padR * 0.92, 32),
      new THREE.MeshBasicMaterial({
        color: 0xfbbf24, side: THREE.DoubleSide, depthWrite: false,
        transparent: true, opacity: 0.88,
      })
    );
    padRing.rotation.x = -Math.PI / 2;
    padRing.position.set(p.x, padY + 0.02, p.z);
    padRing.renderOrder = 4;
    scene.add(padRing);
    var body = new THREE.Mesh(
      new THREE.BoxGeometry(h * 0.28, h, h * 0.22),
      new THREE.MeshStandardMaterial({ color: color, metalness: 0.35, roughness: 0.45 })
    );
    body.position.set(p.x, h * 0.5, p.z); body.castShadow = true; scene.add(body);
    if (m.stories < 1000) {
      var shoulderMat = new THREE.MeshStandardMaterial({ color: color, metalness: 0.3, roughness: 0.5 });
      var sL = new THREE.Mesh(new THREE.BoxGeometry(h * 0.16, h * 0.18, h * 0.14), shoulderMat);
      sL.position.set(p.x - h * 0.28, h * 0.7, p.z); scene.add(sL);
      var sR = new THREE.Mesh(new THREE.BoxGeometry(h * 0.16, h * 0.18, h * 0.14), shoulderMat);
      sR.position.set(p.x + h * 0.28, h * 0.7, p.z); scene.add(sR);
    }
    var edge = new THREE.LineSegments(
      new THREE.EdgesGeometry(new THREE.BoxGeometry(h * 0.28, h, h * 0.22)),
      new THREE.LineBasicMaterial({ color: 0x0f172a })
    );
    edge.position.copy(body.position); scene.add(edge);
    addLabel(m.stories + "-story mech", "#fff", p.x, h + 0.55, p.z);
  }

  function buildCompound() {
    var cp = C.COMPOUND || {};
    var yard = cp.yard || { x: 100, y: 2100, w: 600, h: 360 };
    var yp = worldToThree(yard.x + yard.w / 2, yard.y + yard.h / 2);
    /* tapsteer1: flat backyard plane (was thick box fighting mech pad / ground) */
    var yardM = new THREE.Mesh(
      new THREE.PlaneGeometry(yard.w * 0.02, yard.h * 0.02),
      new THREE.MeshStandardMaterial({
        color: 0x468232, roughness: 0.95,
        polygonOffset: true, polygonOffsetFactor: -1, polygonOffsetUnits: -2,
      })
    );
    yardM.rotation.x = -Math.PI / 2;
    yardM.position.set(yp.x, 0.05, yp.z);
    yardM.receiveShadow = true;
    yardM.renderOrder = -1;
    scene.add(yardM);
    addLabel("Backyard", "#ecfccb", yp.x, 1.2, yp.z);
    for (var ai = 0; ai < 40; ai++) {
      var ap = worldToThree(yard.x + 30 + Math.random() * (yard.w - 60), yard.y + 40 + Math.random() * (yard.h - 80));
      var ar = 0.22 + Math.random() * 0.16;
      var animal = new THREE.Mesh(
        new THREE.SphereGeometry(ar, 8, 6),
        new THREE.MeshStandardMaterial({ color: ai % 3 === 0 ? 0xc4a574 : ai % 3 === 1 ? 0x8b6914 : 0xd6d3d1 })
      );
      animal.position.set(ap.x, ar, ap.z); animal.castShadow = true; scene.add(animal);
      var head = new THREE.Mesh(
        new THREE.SphereGeometry(ar * 0.45, 6, 5),
        new THREE.MeshStandardMaterial({ color: ai % 2 ? 0xc4a574 : 0x8b6914 })
      );
      head.position.set(ap.x + ar * 0.7, ar * 1.1, ap.z); scene.add(head);
    }
    var gar = cp.garage || { x: 700, y: 1400, w: 480, h: 520 };
    var gp = worldToThree(gar.x + gar.w / 2, gar.y + gar.h / 2);
    var gw = gar.w * 0.02, gd = gar.h * 0.02;
    var gFloor = new THREE.Mesh(
      new THREE.BoxGeometry(gw, 0.08, gd),
      new THREE.MeshStandardMaterial({
        color: 0x57534e, roughness: 0.9,
        polygonOffset: true, polygonOffsetFactor: -1, polygonOffsetUnits: -1,
      })
    );
    gFloor.position.set(gp.x, 0.09, gp.z); gFloor.receiveShadow = true; scene.add(gFloor);
    var gMat = new THREE.MeshStandardMaterial({ color: 0x6b7280, roughness: 0.75, metalness: 0.15 });
    function gWall(wx, wz, ww, wd, wh) {
      var m = new THREE.Mesh(new THREE.BoxGeometry(ww, wh || 2.0, wd), gMat);
      m.position.set(wx, (wh || 2.0) * 0.5, wz); m.castShadow = true; scene.add(m);
    }
    gWall(gp.x, gp.z - gd * 0.5 + 0.1, gw, 0.2);
    gWall(gp.x - gw * 0.5 + 0.1, gp.z, 0.2, gd);
    gWall(gp.x + gw * 0.5 - 0.1, gp.z, 0.2, gd);
    // Open south (door) — dark lintel + polish5 rolling door
    var lintel = new THREE.Mesh(new THREE.BoxGeometry(2.4, 0.35, 0.2), new THREE.MeshStandardMaterial({ color: 0x111827 }));
    lintel.position.set(gp.x, 1.85, gp.z + gd * 0.5 - 0.1); scene.add(lintel);
    state.garageDoor = new THREE.Mesh(
      new THREE.BoxGeometry(2.2, 1.6, 0.12),
      new THREE.MeshStandardMaterial({ color: 0x1f2937, metalness: 0.3, roughness: 0.6 })
    );
    state.garageDoor.position.set(gp.x, 0.9, gp.z + gd * 0.5 - 0.05);
    state.garageDoor.userData.y0 = 0.9;
    state.garageDoor.userData.h0 = 1.6;
    state.garageDoor.userData.cx = gp.x;
    state.garageDoor.userData.cz = gp.z + gd * 0.5;
    scene.add(state.garageDoor);
    state.garageOpen = 0;
    state.garageOpenLabel = labelSprite("OPEN", "#bbf7d0");
    state.garageOpenLabel.position.set(gp.x, 2.2, gp.z + gd * 0.5);
    state.garageOpenLabel.visible = false;
    scene.add(state.garageOpenLabel);
    addLabel("Garage · James toys", "#fff", gp.x, 2.9, gp.z);
    for (var t = 0; t < 32; t++) {
      var tp = worldToThree(gar.x + 40 + (t % 8) * 48, gar.y + 70 + Math.floor(t / 8) * 50);
      var toy = new THREE.Mesh(new THREE.BoxGeometry(0.22, 0.22, 0.22), new THREE.MeshStandardMaterial({ color: 0xfbbf24 }));
      toy.position.set(tp.x, 0.2, tp.z); scene.add(toy);
    }
    var house = cp.house || { x: 120, y: 1420, w: 520, h: 420 };
    var hp = worldToThree(house.x + house.w / 2, house.y + house.h / 2);
    var hw = house.w * 0.02, hd = house.h * 0.02;
    // Floor only + perimeter walls (HOLLOW) — spawn is inside house rect; solid box buried frogs
    var floor = new THREE.Mesh(
      new THREE.BoxGeometry(hw, 0.1, hd),
      new THREE.MeshStandardMaterial({
        color: 0xc4a574, roughness: 0.85,
        polygonOffset: true, polygonOffsetFactor: -1, polygonOffsetUnits: -1,
      })
    );
    floor.position.set(hp.x, 0.1, hp.z); floor.receiveShadow = true; scene.add(floor);
    var wallMat = new THREE.MeshStandardMaterial({ color: 0xd4b896, roughness: 0.7, side: THREE.DoubleSide });
    var wallH = 2.2, thick = 0.18;
    function wall(wx, wz, ww, wd) {
      var m = new THREE.Mesh(new THREE.BoxGeometry(ww, wallH, wd), wallMat);
      m.position.set(wx, wallH * 0.5, wz); m.castShadow = true; scene.add(m);
    }
    // North/South (along X), East/West (along Z) — leave south gap as doorway
    wall(hp.x, hp.z - hd * 0.5 + thick * 0.5, hw, thick); // north
    wall(hp.x - hw * 0.28, hp.z + hd * 0.5 - thick * 0.5, hw * 0.4, thick); // south left
    wall(hp.x + hw * 0.28, hp.z + hd * 0.5 - thick * 0.5, hw * 0.4, thick); // south right (door gap)
    wall(hp.x - hw * 0.5 + thick * 0.5, hp.z, thick, hd); // west
    wall(hp.x + hw * 0.5 - thick * 0.5, hp.z, thick, hd); // east
    /* polish4: interior room props (still hollow — frogs walk through) */
    var sofa = new THREE.Mesh(
      new THREE.BoxGeometry(1.4, 0.45, 0.55),
      new THREE.MeshStandardMaterial({ color: 0x7c4a3a, roughness: 0.9 })
    );
    sofa.position.set(hp.x - 1.2, 0.35, hp.z - 0.8); scene.add(sofa);
    var table = new THREE.Mesh(
      new THREE.BoxGeometry(0.9, 0.35, 0.9),
      new THREE.MeshStandardMaterial({ color: 0x5c4030 })
    );
    table.position.set(hp.x + 1.1, 0.28, hp.z + 0.4); scene.add(table);
    var lamp = new THREE.Mesh(
      new THREE.SphereGeometry(0.22, 8, 6),
      new THREE.MeshStandardMaterial({ color: 0xfbbf24, emissive: 0xf59e0b, emissiveIntensity: 0.6 })
    );
    lamp.position.set(hp.x, 1.1, hp.z - 1.2); scene.add(lamp);
    /* Warm window panes on exterior walls */
    function windowPane(wx, wz, ww, wd) {
      var pane = new THREE.Mesh(
        new THREE.BoxGeometry(ww, 0.55, wd),
        new THREE.MeshStandardMaterial({ color: 0xfde68a, emissive: 0xfbbf24, emissiveIntensity: 0.35, transparent: true, opacity: 0.85 })
      );
      pane.position.set(wx, 1.15, wz); scene.add(pane);
    }
    windowPane(hp.x - 1.6, hp.z - hd * 0.5 + 0.05, 0.7, 0.08);
    windowPane(hp.x + 0.2, hp.z - hd * 0.5 + 0.05, 0.7, 0.08);
    windowPane(hp.x + 1.8, hp.z - hd * 0.5 + 0.05, 0.7, 0.08);
    var roof = new THREE.Mesh(
      new THREE.ConeGeometry(Math.max(hw, hd) * 0.62, 1.5, 4),
      new THREE.MeshStandardMaterial({ color: 0x6d4c41, transparent: true, opacity: 0.55 })
    );
    roof.position.set(hp.x, wallH + 0.85, hp.z); roof.rotation.y = Math.PI / 4; scene.add(roof);
    var chimney = new THREE.Mesh(
      new THREE.BoxGeometry(0.35, 1.1, 0.35),
      new THREE.MeshStandardMaterial({ color: 0x78716c })
    );
    chimney.position.set(hp.x + 1.4, wallH + 1.1, hp.z - 0.8); scene.add(chimney);
    /* polish8: porch depth + path + chimney smoke + Blue Bear (hollow house kept) */
    var porch = new THREE.Mesh(
      new THREE.BoxGeometry(hw * 0.9, 0.14, 1.1),
      new THREE.MeshStandardMaterial({ color: 0xb8956a, roughness: 0.85 })
    );
    porch.position.set(hp.x, 0.12, hp.z + hd * 0.5 + 0.4); porch.receiveShadow = true; scene.add(porch);
    var path = new THREE.Mesh(
      new THREE.BoxGeometry(0.9, 0.06, 1.6),
      new THREE.MeshStandardMaterial({
        color: 0xa09070, roughness: 0.9,
        polygonOffset: true, polygonOffsetFactor: -1, polygonOffsetUnits: -1,
      })
    );
    path.position.set(hp.x, 0.09, hp.z + hd * 0.5 + 1.4); scene.add(path);
    for (var pi = 0; pi < 5; pi++) {
      var post = new THREE.Mesh(
        new THREE.BoxGeometry(0.1, 0.9, 0.1),
        new THREE.MeshStandardMaterial({ color: 0x5d4037 })
      );
      post.position.set(hp.x - hw * 0.35 + pi * (hw * 0.175), 0.55, hp.z + hd * 0.5 + 0.35);
      scene.add(post);
    }
    state.chimneySmoke = [];
    for (var sm = 0; sm < 4; sm++) {
      var puff = new THREE.Mesh(
        new THREE.SphereGeometry(0.12 + sm * 0.04, 6, 6),
        new THREE.MeshBasicMaterial({ color: 0xc8c8d0, transparent: true, opacity: 0.4 })
      );
      puff.position.set(hp.x + 1.4, wallH + 1.7 + sm * 0.25, hp.z - 0.8);
      puff.userData.phase = sm * 0.8; puff.userData.baseY = puff.position.y;
      scene.add(puff); state.chimneySmoke.push(puff);
    }
    var blueG = new THREE.Group();
    var blueBody = new THREE.Mesh(
      new THREE.SphereGeometry(0.28, 10, 8),
      new THREE.MeshStandardMaterial({ color: 0x3b82f6 })
    );
    blueBody.position.y = 0.28; blueG.add(blueBody);
    var earL = new THREE.Mesh(new THREE.SphereGeometry(0.1, 6, 6), new THREE.MeshStandardMaterial({ color: 0x60a5fa }));
    earL.position.set(-0.16, 0.48, 0); blueG.add(earL);
    var earR = earL.clone(); earR.position.x = 0.16; blueG.add(earR);
    var bp = worldToThree(320, 1920);
    blueG.position.set(bp.x, 0, bp.z);
    blueG.userData.bob = 0;
    scene.add(blueG); state.blueBear = blueG;
    addLabel("Blue Bear", "#bfdbfe", bp.x, 1.1, bp.z);
    addLabel("James · Ranch house", "#fff7ed", hp.x, wallH + 2.0, hp.z);
    addMech(cp.mech10 || { x: 820, y: 1680, stories: 10 }, 0xa5b4fc, 1.9);
    addMech(cp.mech100 || { x: 980, y: 1700, stories: 100 }, 0x67e8f9, 3.2);
    addMech(cp.mech1000 || { x: 340, y: 2420, stories: 1000 }, 0xfcd34d, 8.2);
  }

  function buildTrack() {
    var mounds = C.TRACK_MOUNDS || [];
    for (var i = 0; i < mounds.length; i++) {
      var m = mounds[i], p = worldToThree(m.x, m.y);
      var geo = new THREE.SphereGeometry(m.r * 0.022, 16, 12);
      geo.scale(1, Math.abs(m.h) * 0.85 + 0.25, 0.55);
      var mesh = new THREE.Mesh(geo, new THREE.MeshStandardMaterial({ color: m.h >= 0 ? 0x78716c : 0x44403c, roughness: 0.95 }));
      mesh.position.set(p.x, Math.abs(m.h) * 0.55 + 0.15, p.z); scene.add(mesh);
      if (m.h >= 0) addLabel("HILL", "#fef3c7", p.x, Math.abs(m.h) * 0.9 + 0.6, p.z);
    }
    addPathRibbon(C.TRACK_MAIN, 0.12, 0x1c1917, 0.62);
    addPathRibbon(C.TRACK_MAIN, 0.18, 0xfbbf24, 0.18);
    addPathRibbon(C.TRACK_MAIN, 0.22, 0xfafaf9, 0.08);
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
    /* polish9: start/finish gate */
    var gp = worldToThree(1870, 2225);
    var postMat = new THREE.MeshStandardMaterial({ color: 0xf8fafc, roughness: 0.4 });
    var leftPost = new THREE.Mesh(new THREE.BoxGeometry(0.12, 1.4, 0.12), postMat);
    leftPost.position.set(gp.x - 1.4, 0.7, gp.z); scene.add(leftPost);
    var rightPost = new THREE.Mesh(new THREE.BoxGeometry(0.12, 1.4, 0.12), postMat);
    rightPost.position.set(gp.x + 1.4, 0.7, gp.z); scene.add(rightPost);
    var banner = new THREE.Mesh(
      new THREE.BoxGeometry(2.8, 0.28, 0.06),
      new THREE.MeshStandardMaterial({ color: 0x0f172a, emissive: 0xfbbf24, emissiveIntensity: 0.25 })
    );
    banner.position.set(gp.x, 1.35, gp.z); scene.add(banner);
    addLabel("START / FINISH", "#fef3c7", gp.x, 1.7, gp.z);
  }

  function buildPondLife() {
    var pond = C.AREAS[2];
    state.fish = []; state.whales = []; state.schools = [];
    /* polish8: fish school centers */
    for (var sc = 0; sc < 4; sc++) {
      var scp = worldToThree(pond.x + 120 + Math.random() * (pond.w - 240), pond.y + 120 + Math.random() * (pond.h - 240));
      state.schools.push({ x: scp.x, z: scp.z, phase: Math.random() * Math.PI * 2 });
    }
    for (var f = 0; f < 28; f++) {
      var sch = state.schools[f % 4];
      var fp = { x: sch.x + (Math.random() - 0.5) * 1.2, z: sch.z + (Math.random() - 0.5) * 1.0 };
      var fishG = new THREE.Group();
      var fish = new THREE.Mesh(
        new THREE.SphereGeometry(0.18 + Math.random() * 0.08, 8, 6),
        new THREE.MeshStandardMaterial({ color: 0xfde68a, emissive: 0xb45309, emissiveIntensity: 0.3 })
      );
      fish.scale.set(1.5, 0.55, 0.7);
      fishG.add(fish);
      var fin = new THREE.Mesh(
        new THREE.ConeGeometry(0.08, 0.18, 5),
        new THREE.MeshStandardMaterial({ color: 0xf59e0b })
      );
      fin.position.set(-0.22, 0.02, 0); fin.rotation.z = Math.PI / 2; fishG.add(fin);
      fishG.position.set(fp.x, 0.14, fp.z);
      fishG.userData.phase = Math.random() * Math.PI * 2; fishG.userData.bx = fp.x; fishG.userData.bz = fp.z;
      fishG.userData.school = f % 4; fishG.userData.ox = fp.x - sch.x; fishG.userData.oz = fp.z - sch.z;
      scene.add(fishG); state.fish.push(fishG);
    }
    for (var w = 0; w < 5; w++) {
      var wp = worldToThree(pond.x + 160 + Math.random() * (pond.w - 320), pond.y + 140 + Math.random() * (pond.h - 280));
      var whaleG = new THREE.Group();
      var whale = new THREE.Mesh(
        new THREE.SphereGeometry(0.6 + Math.random() * 0.22, 12, 8),
        new THREE.MeshStandardMaterial({ color: 0x7dd3fc, roughness: 0.4, metalness: 0.18, emissive: 0x0c4a6e, emissiveIntensity: 0.2 })
      );
      whale.scale.set(1.85, 0.5, 1); whaleG.add(whale);
      var spout = new THREE.Mesh(
        new THREE.CylinderGeometry(0.04, 0.08, 0.55, 6),
        new THREE.MeshStandardMaterial({ color: 0xe0f2fe, transparent: true, opacity: 0.7 })
      );
      spout.position.set(0.15, 0.45, 0); whaleG.add(spout);
      whaleG.position.set(wp.x, 0.22, wp.z);
      whaleG.userData.phase = Math.random() * Math.PI * 2; whaleG.userData.bx = wp.x; whaleG.userData.bz = wp.z;
      whaleG.userData.breach = Math.random() * Math.PI * 2; whaleG.userData.breachAmp = 0.55 + Math.random() * 0.45;
      scene.add(whaleG); state.whales.push(whaleG);
      addLabel("whale", "#e0f2fe", wp.x, 1.15, wp.z);
    }
    var pl = worldToThree(pond.x + pond.w * 0.5, pond.y + 40);
    var pc = worldToThree(pond.x + pond.w / 2, pond.y + pond.h / 2);
    var shore = new THREE.Mesh(
      new THREE.RingGeometry(Math.min(pond.w, pond.h) * 0.0085, Math.min(pond.w, pond.h) * 0.0112, 64),
      new THREE.MeshBasicMaterial({ color: 0xe0f2fe, transparent: true, opacity: 0.72, side: THREE.DoubleSide })
    );
    shore.rotation.x = -Math.PI / 2;
    shore.position.set(pc.x, 0.16, pc.z);
    shore.scale.set(pond.w / Math.min(pond.w, pond.h), 1, pond.h / Math.min(pond.w, pond.h));
    scene.add(shore);
    addLabel("Pond · fishies & whales", "#ecfeff", pl.x, 1.5, pl.z);
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
      var label = s.id === "shared" ? "★ ALL ABOARD · 4" : ("Cybertruck · " + (C.FROG_DEFS[s.id] || {}).name);
      var lab = labelSprite(label, s.id === "shared" ? "#fef3c7" : "#fde68a");
      lab.position.set(p.x, s.id === "shared" ? 1.85 : 1.5, p.z); scene.add(lab);
      if (s.id === "shared") {
        var pad = new THREE.Mesh(
          new THREE.RingGeometry(1.1, 1.45, 32),
          new THREE.MeshBasicMaterial({ color: 0xfbbf24, transparent: true, opacity: 0.55, side: THREE.DoubleSide })
        );
        pad.rotation.x = -Math.PI / 2; pad.position.set(p.x, 0.06, p.z); scene.add(pad);
        var ids = ["james", "jimmy", "bubbles", "rexy"];
        for (var si = 0; si < 4; si++) {
          var col = hex((C.FROG_DEFS[ids[si]] || C.FROG_DEFS.james).color);
          var slot = new THREE.Mesh(new THREE.SphereGeometry(0.14, 8, 6), new THREE.MeshStandardMaterial({ color: col }));
          slot.position.set(p.x + (si - 1.5) * 0.35, 0.85, p.z); scene.add(slot);
        }
      }
      state.parkedTrucks.push({ spot: s, mesh: truck, label: lab });
    }
  }

  function buildRanch() {
    scene = new THREE.Scene();
    scene.position.set(0, 0, 0);
    scene.background = new THREE.Color(0x87b5d9);
    scene.fog = new THREE.Fog(0x87b5d9, 55, 160);

    // Fixed-angle isometric-ish camera — orbit LOCKED (no free-fly)
    var aspect = window.innerWidth / Math.max(1, window.innerHeight);
    camera = new THREE.PerspectiveCamera(46, aspect, 0.1, 400);
    camera.position.set(18, 22, 18);
    camera.lookAt(0, 0, 0);
    camera.userData.lockTarget = new THREE.Vector3(0, 0, 0);

    var hemi = new THREE.HemisphereLight(0xfff0d0, 0x3a5a2a, 0.85);
    scene.add(hemi);
    var sun = new THREE.DirectionalLight(0xffe6b0, 0.95);
    sun.position.set(12, 22, 8);
    sun.castShadow = true;
    sun.shadow.mapSize.set(2048, 2048);
    /* solid1: reduce shadow acne flicker on ranch floor while walking */
    sun.shadow.bias = -0.00035;
    sun.shadow.normalBias = 0.035;
    scene.add(sun);

    // Ground — flat (no subdiv) + polygonOffset so overlays don't z-fight
    var ground = new THREE.Mesh(
      new THREE.PlaneGeometry(C.MAP_W * 0.02, C.MAP_H * 0.02),
      new THREE.MeshStandardMaterial({
        color: 0x3d7a35, roughness: 0.9,
        polygonOffset: true, polygonOffsetFactor: 1, polygonOffsetUnits: 1,
      })
    );
    ground.rotation.x = -Math.PI / 2;
    ground.position.y = 0;
    ground.receiveShadow = true;
    ground.renderOrder = -2;
    scene.add(ground);

    /* polish6: parallax-lite distant ranch hills (cheap depth bands) */
    state.paraHills = [];
    for (var hi = 0; hi < 3; hi++) {
      var hill = new THREE.Mesh(
        new THREE.BoxGeometry(28 + hi * 6, 2.2 + hi * 0.8, 4 + hi),
        new THREE.MeshStandardMaterial({
          color: hi === 0 ? 0x1e3a5f : hi === 1 ? 0x2f5a3a : 0x3d7a35,
          transparent: true, opacity: 0.55 - hi * 0.08, roughness: 1,
        })
      );
      hill.position.set(-8 + hi * 10, 1.2 + hi * 0.4, -C.MAP_H * 0.009 - hi * 2);
      hill.userData.para = 0.12 + hi * 0.08;
      scene.add(hill);
      state.paraHills.push(hill);
    }

    // Soft grid — lifted + no depth write (was z-fighting ground → floor shudder)
    var grid = new THREE.GridHelper(Math.max(C.MAP_W, C.MAP_H) * 0.02, 30, 0x2f5e2a, 0x2f5e2a);
    grid.position.y = 0.14;
    if (Array.isArray(grid.material)) {
      for (var gi = 0; gi < grid.material.length; gi++) {
        grid.material[gi].opacity = 0.22;
        grid.material[gi].transparent = true;
        grid.material[gi].depthWrite = false;
      }
    } else {
      grid.material.opacity = 0.22;
      grid.material.transparent = true;
      grid.material.depthWrite = false;
    }
    grid.renderOrder = -1;
    scene.add(grid);

    state.areaMeshes = [];
    for (var i = 0; i < C.AREAS.length; i++) {
      var a = C.AREAS[i];
      var p = worldToThree(a.x + a.w / 2, a.y + a.h / 2);
      /* solid1: flat zone pads (not thick boxes) — thick boxes z-fought the ground plane */
      var mesh = new THREE.Mesh(
        new THREE.PlaneGeometry(a.w * 0.02, a.h * 0.02),
        new THREE.MeshStandardMaterial({
          color: hex(a.color),
          roughness: 0.85,
          transparent: true,
          opacity: 0.92,
          polygonOffset: true, polygonOffsetFactor: -1, polygonOffsetUnits: -2,
        })
      );
      mesh.rotation.x = -Math.PI / 2;
      mesh.position.set(p.x, 0.04, p.z);
      mesh.receiveShadow = true;
      scene.add(mesh);
      var edgeA = new THREE.LineSegments(
        new THREE.EdgesGeometry(new THREE.PlaneGeometry(a.w * 0.02, a.h * 0.02)),
        new THREE.LineBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.4, depthWrite: false })
      );
      edgeA.rotation.x = -Math.PI / 2;
      edgeA.position.set(p.x, 0.05, p.z);
      scene.add(edgeA);
      addLabel(a.name, "#ffffff", p.x, 3.2, p.z);
    }

    buildCompound();
    buildTrack();
    buildPondLife();
    buildStarshipApproach();
    buildTrucks();

    // Hotspots (non-truck rings — trucks drawn as Cybertrucks) — polish4 inviting
    state.hotMeshes = [];
    for (var h = 0; h < C.HOTSPOTS.length; h++) {
      var hs = C.HOTSPOTS[h];
      if (C.isTruckHotspot && C.isTruckHotspot(hs)) continue;
      var hp = worldToThree(hs.x, hs.y);
      var ring = new THREE.Mesh(
        new THREE.RingGeometry(0.85, 1.2, 28),
        new THREE.MeshBasicMaterial({ color: 0xfbbf24, transparent: true, opacity: 0.55, side: THREE.DoubleSide })
      );
      ring.rotation.x = -Math.PI / 2;
      ring.position.set(hp.x, 0.06, hp.z);
      scene.add(ring);
      var glow = new THREE.Mesh(
        new THREE.CircleGeometry(1.15, 20),
        new THREE.MeshBasicMaterial({ color: 0xfbbf24, transparent: true, opacity: 0.15, side: THREE.DoubleSide })
      );
      glow.rotation.x = -Math.PI / 2; glow.position.set(hp.x, 0.04, hp.z); scene.add(glow);
      if (hs.id === "phone") {
        var booth = new THREE.Mesh(
          new THREE.BoxGeometry(0.45, 0.9, 0.4),
          new THREE.MeshStandardMaterial({ color: 0x7c3aed, metalness: 0.2 })
        );
        booth.position.set(hp.x, 0.5, hp.z); scene.add(booth);
        addLabel("Phone → Purple Bear", "#e9d5ff", hp.x, 1.9, hp.z);
      } else if (hs.id === "sps") {
        var dish = new THREE.Mesh(
          new THREE.SphereGeometry(0.35, 10, 8, 0, Math.PI * 2, 0, Math.PI * 0.5),
          new THREE.MeshStandardMaterial({ color: 0x38bdf8, metalness: 0.4, side: THREE.DoubleSide })
        );
        dish.position.set(hp.x, 0.35, hp.z); dish.rotation.x = -0.5; scene.add(dish);
        addLabel("SPS → Optimus · Jimmy", "#bae6fd", hp.x, 1.9, hp.z);
      } else {
        addLabel(hs.label, "#fde68a", hp.x, 1.7, hp.z);
      }
      state.hotMeshes.push({ data: hs, ring: ring });
    }

    var ss = C.STARSHIP || { x: 360, y: 320 };
    var sp = worldToThree(ss.x, ss.y);
    var pad = new THREE.Mesh(
      new THREE.CylinderGeometry(2.4, 2.6, 0.12, 32),
      new THREE.MeshStandardMaterial({ color: 0x334155, metalness: 0.5, roughness: 0.4 })
    );
    pad.position.set(sp.x, 0.06, sp.z); scene.add(pad);
    var padRing = new THREE.Mesh(
      new THREE.RingGeometry(1.6, 2.2, 32),
      new THREE.MeshBasicMaterial({ color: 0xfbbf24, transparent: true, opacity: 0.7, side: THREE.DoubleSide })
    );
    padRing.rotation.x = -Math.PI / 2; padRing.position.set(sp.x, 0.14, sp.z); scene.add(padRing);
    var rocket = new THREE.Mesh(
      new THREE.ConeGeometry(0.45, 2.2, 10),
      new THREE.MeshStandardMaterial({ color: 0xe2e8f0, metalness: 0.5 })
    );
    rocket.position.set(sp.x, 1.3, sp.z); scene.add(rocket);
    var spotty = new THREE.Mesh(
      new THREE.SphereGeometry(0.35, 12, 10),
      new THREE.MeshStandardMaterial({ color: 0xfdba74 })
    );
    spotty.position.set(sp.x + 1.1, 0.45, sp.z); scene.add(spotty);
    addLabel("★ STARSHIP · SPACE", "#fef3c7", sp.x, 3.0, sp.z);
    addLabel("Spotty", "#fdba74", sp.x + 1.1, 1.3, sp.z);

    var def = C.FROG_DEFS[state.frogId];
    var spawnW = (C.COMPOUND && C.COMPOUND.spawn) || { x: 280, y: 1750 };
    state.player = makeFrogMesh(def, 1.7);
    var spawn = worldToThree(spawnW.x, spawnW.y);
    state.player.position.set(spawn.x, 0.02, spawn.z);
    if (state.player.userData.ring) state.player.userData.ring.material.opacity = 0.85;
    scene.add(state.player);
    /* polish6 + solid1: soft depth shadow — depthWrite off + lifted so they don't fight the floor */
    state.playerShadow = new THREE.Mesh(
      new THREE.CircleGeometry(0.55, 20),
      new THREE.MeshBasicMaterial({
        color: 0x000000, transparent: true, opacity: 0.32, side: THREE.DoubleSide,
        depthWrite: false,
      })
    );
    state.playerShadow.rotation.x = -Math.PI / 2;
    state.playerShadow.position.set(spawn.x, 0.07, spawn.z);
    state.playerShadow.renderOrder = 1;
    scene.add(state.playerShadow);
    state.playerShadowSoft = new THREE.Mesh(
      new THREE.CircleGeometry(0.85, 20),
      new THREE.MeshBasicMaterial({
        color: 0x000000, transparent: true, opacity: 0.14, side: THREE.DoubleSide,
        depthWrite: false,
      })
    );
    state.playerShadowSoft.rotation.x = -Math.PI / 2;
    state.playerShadowSoft.position.set(spawn.x, 0.065, spawn.z);
    state.playerShadowSoft.renderOrder = 1;
    scene.add(state.playerShadowSoft);
    state.nameTag = labelSprite(def.name, def.color || "#fff");
    state.nameTag.position.set(spawn.x, 2.6, spawn.z);
    state.nameTag.scale.set(2.8, 0.7, 1);
    scene.add(state.nameTag);
    /* polish9: aboard frog icons (shared truck) */
    state.aboardGroup = new THREE.Group();
    state.aboardGroup.visible = false;
    for (var abi = 0; abi < C.FROG_ORDER.length; abi++) {
      var aid = C.FROG_ORDER[abi];
      var adef = C.FROG_DEFS[aid];
      var dot = new THREE.Mesh(
        new THREE.SphereGeometry(0.16, 10, 8),
        new THREE.MeshStandardMaterial({ color: hex(adef.color), emissive: hex(adef.color), emissiveIntensity: 0.25 })
      );
      dot.position.set((abi - 1.5) * 0.38, 0.95, 0);
      state.aboardGroup.add(dot);
    }
    scene.add(state.aboardGroup);
    state.aboardLabel = labelSprite("Aboard", "#fef3c7");
    state.aboardLabel.scale.set(1.6, 0.4, 1);
    state.aboardLabel.visible = false;
    scene.add(state.aboardLabel);
    state.kitFxT = 0; state.kitFxKind = ""; state.lapSide = 0; state.lapCd = 0; state.lapCount = 0;
    // Snap locked camera onto spawn immediately (no multi-second lerp from origin)
    camera.userData.lockTarget.set(spawn.x, 0, spawn.z);
    camera.position.set(spawn.x + 17, 22, spawn.z + 17);
    camera.lookAt(spawn.x, 0.6, spawn.z);

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
      var cdef = C.FROG_DEFS[cid];
      var cmesh = makeFrogMesh(cdef, 1.35);
      if (cmesh.userData.ring) cmesh.userData.ring.material.opacity = 0.35;
      var cp = worldToThree(spawnW.x + 40 + ci * 36, spawnW.y + 20 + (ci % 2) * 16);
      cmesh.position.set(cp.x, 0, cp.z);
      cmesh.userData.tx = cp.x;
      cmesh.userData.tz = cp.z;
      cmesh.userData.timer = 1 + Math.random();
      cmesh.userData.frogId = cid;
      cmesh.userData.followLag = 0.35 + ci * 0.12;
      cmesh.userData.idleBounce = Math.random() * 6;
      cmesh.userData.chatT = 0;
      scene.add(cmesh);
      var ctag = labelSprite(cdef.name, cdef.color || "#fff");
      ctag.scale.set(2.0, 0.5, 1);
      ctag.position.set(cp.x, 2.2, cp.z);
      scene.add(ctag);
      cmesh.userData.nameTag = ctag;
      state.companions.push(cmesh);
    }

    /* polish7: zone signs (world labels) + mini-map overlay canvas */
    var hostEl0 = document.getElementById("engine-host");
    if (hostEl0) {
      var oldMaps = hostEl0.querySelectorAll("canvas");
      /* keep three renderer canvas; drop prior mini-map canvases we tagged */
      for (var omi = 0; omi < oldMaps.length; omi++) {
        if (oldMaps[omi].dataset && oldMaps[omi].dataset.ffMinimap === "1") oldMaps[omi].remove();
      }
    }
    state.zoneSigns = [];
    var zlist = C.ZONE_SIGNS || [];
    for (var zi = 0; zi < zlist.length; zi++) {
      var zs = zlist[zi];
      var zp = worldToThree(zs.x, zs.y);
      var zlab = labelSprite(zs.label, zs.color || "#fef3c7");
      zlab.scale.set(3.2, 0.85, 1);
      zlab.position.set(zp.x, 3.2, zp.z);
      zlab.material.opacity = 0;
      zlab.material.transparent = true;
      zlab.userData.zone = zs;
      scene.add(zlab);
      state.zoneSigns.push(zlab);
    }
    state.miniMapCanvas = document.createElement("canvas");
    var vw3 = window.innerWidth || 800, vh3 = window.innerHeight || 600;
    var narrow3 = vw3 <= 520 || (vw3 <= 900 && vh3 <= 480);
    var land3 = vw3 <= 900 && vh3 <= 480;
    var mmW = narrow3 ? 72 : 108, mmH = narrow3 ? 54 : 82;
    var mmL = narrow3 ? 8 : 12, mmT = narrow3 ? 56 : 88;
    state.miniMapCanvas.width = mmW;
    state.miniMapCanvas.height = mmH;
    state.miniMapCanvas.dataset.ffMinimap = "1";
    state.miniMapCanvas.style.cssText = "position:absolute;left:" + mmL + "px;top:" + mmT + "px;width:" + mmW + "px;height:" + mmH + "px;pointer-events:none;z-index:5;border-radius:8px;opacity:" + (narrow3 ? "0.58" : "0.72") + ";";
    var hostEl = document.getElementById("engine-host");
    if (hostEl) hostEl.appendChild(state.miniMapCanvas);
    state.miniMapCanvas.style.display = land3 ? "none" : "";
    state.miniMapCtx = state.miniMapCanvas.getContext("2d");

    state.vx = 0;
    state.vz = 0;
    state.inTruck = false;
    state.truckMode = null;
    state.truckId = null;
    state.waterSub = 0;
    state.zLift = 0;
    state.zVel = 0;
    state.scrap = 0;
    state.bouncePhase = 0;
    state.dustT = 0;
    state.prevNearId = null;
    state.shakeT = 0;
    state.rippleT = 0;
    state.ambient = [];
    /* polish5 + mobile1: ambient pollen / fireflies (fewer on phone) */
    var ambN3 = (window.innerWidth || 800) <= 520 ? 10 : 22;
    for (var ai = 0; ai < ambN3; ai++) {
      var kind = Math.random() < 0.55 ? "pollen" : "firefly";
      var amb = new THREE.Mesh(
        new THREE.SphereGeometry(kind === "firefly" ? 0.07 : 0.05, 6, 5),
        new THREE.MeshBasicMaterial({
          color: kind === "firefly" ? 0xfacc15 : 0xfef9c3,
          transparent: true,
          opacity: kind === "firefly" ? 0.85 : 0.5,
        })
      );
      amb.position.set((Math.random() - 0.5) * C.MAP_W * 0.018, 0.4 + Math.random() * 1.2, (Math.random() - 0.5) * C.MAP_H * 0.018);
      amb.userData.kind = kind;
      amb.userData.phase = Math.random() * Math.PI * 2;
      amb.userData.vx = (Math.random() - 0.5) * 0.6;
      amb.userData.vz = (Math.random() - 0.5) * 0.6;
      scene.add(amb);
      state.ambient.push(amb);
    }
    state.fx = [];
    state.toast = "three.js ranch · compound · squiggle track · whales · Cybertrucks · Starship";
    state.toastT = 3.5;
    state.cd = 0;
    state.near = null;
    state.facing = 1;
    state.faceYaw = 0; /* eyes1: local +Z is face front */
    state.mode = "ranch";
    state.bob = 0;
  }

  function buildSpace() {
    // Clear ranch meshes by rebuilding scene
    while (scene.children.length) scene.remove(scene.children[0]);
    scene.position.set(0, 0, 0);
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

    /* polish6: Mars marker + distant invader mech silhouettes (visual tease) */
    var mars = new THREE.Mesh(
      new THREE.SphereGeometry(0.9, 16, 12),
      new THREE.MeshStandardMaterial({ color: 0xb45309, emissive: 0x7c2d12, emissiveIntensity: 0.25 })
    );
    mars.position.set(6.5, 0.9, 5.5); scene.add(mars);
    addLabel("Mars", "#fed7aa", 6.5, 2.2, 5.5);
    state.marsPos = { x: 6.5, z: 5.5 };
    state.invSil = [];
    for (var isi = 0; isi < 4; isi++) {
      var inv = new THREE.Mesh(
        new THREE.BoxGeometry(0.35, 0.9, 0.25),
        new THREE.MeshBasicMaterial({ color: 0x7f1d1d, transparent: true, opacity: 0.55 })
      );
      inv.position.set(5.2 + isi * 0.7, 0.5, 4.2 + (isi % 2) * 0.4);
      inv.visible = false;
      scene.add(inv);
      state.invSil.push(inv);
    }
    state.invLabel = labelSprite("Invader mechs · silhouette tease", "#fca5a5");
    state.invLabel.scale.set(4.2, 0.55, 1);
    state.invLabel.position.set(6.5, 2.8, 5.5);
    state.invLabel.visible = false;
    scene.add(state.invLabel);

    var def = C.FROG_DEFS[state.frogId];
    state.player = makeFrogMesh(def, 1.5);
    state.player.position.set(-4, 0, 2);
    scene.add(state.player);
    state.playerShadow = new THREE.Mesh(
      new THREE.CircleGeometry(0.5, 16),
      new THREE.MeshBasicMaterial({ color: 0x000000, transparent: true, opacity: 0.28, side: THREE.DoubleSide })
    );
    state.playerShadow.rotation.x = -Math.PI / 2;
    state.playerShadow.position.set(-4, 0.05, 2);
    scene.add(state.playerShadow);
    state.playerShadowSoft = null;
    state.paraHills = [];
    state.nameTag = labelSprite(def.name, "#fff");
    state.nameTag.scale.set(2.6, 0.65, 1);
    scene.add(state.nameTag);
    camera.userData.lockTarget.set(-4, 0, 2);
    camera.position.set(-4 + 10, 14, 2 + 10);
    camera.lookAt(-4, 0.5, 2);

    state.jimmy = makeFrogMesh(C.FROG_DEFS.jimmy, 1.35);
    state.jimmy.position.set(4, 0, -2);
    scene.add(state.jimmy);
    state.jimmyLabel = labelSprite("Jimmy", "#fb923c");
    scene.add(state.jimmyLabel);
    /* polish7: Jimmy jetpack flame (escape visual) */
    state.jimmyFlame = new THREE.Mesh(
      new THREE.ConeGeometry(0.18, 0.7, 8),
      new THREE.MeshBasicMaterial({ color: 0x38bdf8, transparent: true, opacity: 0.9 })
    );
    state.jimmyFlame.rotation.x = Math.PI;
    state.jimmyFlame.visible = false;
    scene.add(state.jimmyFlame);
    state.jimmyJetT = 0;
    state.jimmyVx = 2.2;
    state.jimmyVz = -1.4;
    state.planet = { id: "moon", name: "Moon", x: 5, z: -5, r: 1.2 };
    addLabel("☾ Moon · Earth", "#e2e8f0", 5, 1.6, -5);
    /* polish8: soft destination beacon */
    state.destBeacon = new THREE.Mesh(
      new THREE.RingGeometry(2.6, 2.85, 48),
      new THREE.MeshBasicMaterial({ color: 0x7dd3fc, transparent: true, opacity: 0.45, side: THREE.DoubleSide })
    );
    state.destBeacon.rotation.x = -Math.PI / 2;
    state.destBeacon.position.set(5, 0.12, -5); scene.add(state.destBeacon);
    state.destBeaconLabel = labelSprite("✦ heading · Moon", "#7dd3fc");
    state.destBeaconLabel.scale.set(3.5, 0.5, 1);
    state.destBeaconLabel.position.set(5, 2.4, -5);
    state.destBeaconLabel.visible = false; scene.add(state.destBeaconLabel);
    state.inOrbit = false;
    state.orbitAngle = 0;
    state.orbitRadius = 2.2;
    state.orbitEscapeCool = 0;
    state.orbitCfg = (C && C.ORBIT_PHYSICS) || {};
    /* polish5: readable pull / orbit rings */
    state.pullRing = new THREE.Mesh(
      new THREE.RingGeometry(3.2, 3.45, 48),
      new THREE.MeshBasicMaterial({ color: 0x7dd3fc, transparent: true, opacity: 0.35, side: THREE.DoubleSide })
    );
    state.pullRing.rotation.x = -Math.PI / 2;
    state.pullRing.position.set(5, 0.08, -5); scene.add(state.pullRing);
    state.capRing = new THREE.Mesh(
      new THREE.RingGeometry(1.9, 2.1, 48),
      new THREE.MeshBasicMaterial({ color: 0x38bdf8, transparent: true, opacity: 0.55, side: THREE.DoubleSide })
    );
    state.capRing.rotation.x = -Math.PI / 2;
    state.capRing.position.set(5, 0.09, -5); scene.add(state.capRing);
    state.orbitRing = new THREE.Mesh(
      new THREE.RingGeometry(2.1, 2.25, 48),
      new THREE.MeshBasicMaterial({ color: 0xfacc15, transparent: true, opacity: 0.9, side: THREE.DoubleSide })
    );
    state.orbitRing.rotation.x = -Math.PI / 2;
    state.orbitRing.position.set(5, 0.1, -5); state.orbitRing.visible = false; scene.add(state.orbitRing);
    state.escapeBanner = labelSprite("ORBIT · ESCAPE / Esc · Ability thruster", "#fde68a");
    state.escapeBanner.scale.set(5.5, 0.7, 1);
    state.escapeBanner.visible = false; scene.add(state.escapeBanner);
    state.toast = "Space · near Moon → orbit · Escape / hard thruster to leave";

    var germy = new THREE.Mesh(
      new THREE.SphereGeometry(0.28, 10, 8),
      new THREE.MeshStandardMaterial({ color: 0xb45309 })
    );
    germy.position.set(1, 0.28, 1.5);
    scene.add(germy);
    addLabel("Germy", "#fbbf24", 1, 1.1, 1.5);

    var daisy = new THREE.Mesh(
      new THREE.SphereGeometry(0.26, 10, 8),
      new THREE.MeshStandardMaterial({ color: 0xd6d3d1 })
    );
    daisy.position.set(2, 0.26, 2);
    scene.add(daisy);
    addLabel("Daisy", "#e7e5e4", 2, 1.05, 2);

    var spotty = new THREE.Mesh(
      new THREE.SphereGeometry(0.3, 10, 8),
      new THREE.MeshStandardMaterial({ color: 0xfdba74 })
    );
    spotty.position.set(-6, 0.3, -4);
    scene.add(spotty);
    addLabel("Spotty", "#fdba74", -6, 1.2, -4);

    var alex = new THREE.Mesh(
      new THREE.SphereGeometry(0.28, 10, 8),
      new THREE.MeshStandardMaterial({ color: 0x64748b })
    );
    alex.position.set(-2, 0.35, 3); scene.add(alex);
    addLabel("Alex", "#e2e8f0", -2, 1.3, 3);
    var fred = new THREE.Mesh(
      new THREE.SphereGeometry(0.28, 10, 8),
      new THREE.MeshStandardMaterial({ color: 0x475569 })
    );
    fred.position.set(-1, 0.35, 3.6); scene.add(fred);
    addLabel("Fred", "#e2e8f0", -1, 1.3, 3.6);

    var pad = new THREE.Mesh(
      new THREE.CircleGeometry(1.2, 24),
      new THREE.MeshStandardMaterial({ color: 0x22c55e, emissive: 0x14532d, emissiveIntensity: 0.4 })
    );
    pad.rotation.x = -Math.PI / 2;
    pad.position.set(-7, 0.05, 5);
    scene.add(pad);
    addLabel("Ranch", "#bbf7d0", -7, 1.2, 5);
    state.returnPad = { x: -7, z: 5 };

    state.vx = 0;
    state.vz = 0;
    state.catches = 0;
    state.toast = "Space · near Moon → orbit · Escape / hard thruster to leave";
    state.toastT = 3;
    state.mode = "space";
    if (state.miniMapCanvas) state.miniMapCanvas.style.display = "none";
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
    var hostEl = document.getElementById("engine-host");
    var w = Math.max((hostEl && hostEl.clientWidth) || 0, window.innerWidth || 320);
    var h = Math.max((hostEl && hostEl.clientHeight) || 0, window.innerHeight || 480);
    renderer.setSize(w, h, false);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    camera.aspect = w / Math.max(1, h);
    camera.updateProjectionMatrix();
    if (state && state.miniMapCanvas) {
      var land = w <= 900 && h <= 480;
      var narrow = w <= 520 || land;
      var mmW = narrow ? 72 : 108, mmH = narrow ? 54 : 82;
      state.miniMapCanvas.width = mmW;
      state.miniMapCanvas.height = mmH;
      state.miniMapCanvas.style.width = mmW + "px";
      state.miniMapCanvas.style.height = mmH + "px";
      state.miniMapCanvas.style.left = (narrow ? 8 : 12) + "px";
      state.miniMapCanvas.style.top = (narrow ? 56 : 88) + "px";
      state.miniMapCanvas.style.opacity = narrow ? "0.58" : "0.72";
      state.miniMapCanvas.style.display = land ? "none" : "";
    }
  }

  function doInteract() {
    if (!state) return;
    /* polish10: EXIT truck anytime while driving */
    if (state.mode === "ranch" && state.inTruck) {
      state.inTruck = false; state.truckMode = null; state.truckId = null;
      state.toast = "Parked · walking"; state.toastT = 1.8;
      if (hooks.onToast) hooks.onToast(state.toast);
      return;
    }
    if (!state.near) return;
    var id = state.near.id;
    if (state.mode === "ranch") {
      if (C.isTruckHotspot && C.isTruckHotspot(state.near)) {
        state.inTruck = true; state.truckMode = state.near.mode || "solo"; state.truckId = id;
        state.scrap += 1;
        state.toast = state.truckMode === "shared"
          ? "All aboard! Four froggies · one Cybertruck · hit the jumps!"
          : "Driving Cybertruck · hit the jumps!";
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
        state.toast = "SPS · Optimus kits (stub on three.js)";
        state.kitFxKind = "rocket"; state.kitFxT = 0.55;
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
      state.toast = "BOT · open SPS for Optimus kits";
      state.kitFxKind = "map"; state.kitFxT = 0.55;
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
        var jimmyPack = state.frogId === "jimmy";
        state.vx += jimmyPack ? 4.2 : 3;
        state.vz -= jimmyPack ? 3.2 : 2;
        state.toast = jimmyPack ? "SHIELD up!" : (def.ability + " · hard thruster");
        /* polish7: Jimmy jetpack escape visual */
        if (jimmyPack && state.jimmy) {
          state.jimmyVx = (Math.random() > 0.5 ? 1 : -1) * 5;
          state.jimmyVz = -4;
          state.jimmyJetT = 0.9;
        }
      }
    }
    state.toastT = 1.8;
    if (hooks.onToast) hooks.onToast(state.toast);
    if (hooks.onAbilityFire) hooks.onAbilityFire(state.frogId, def.ability);
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
      if (state.orbitRing) state.orbitRing.visible = !!state.inOrbit;
      /* polish8: destination beacon when heading toward Moon */
      if (state.destBeacon) {
        var spdB = Math.hypot(state.vx || 0, state.vz || 0);
        var heading = !!state.inOrbit;
        if (!state.inOrbit && dP < softR * 1.6 && spdB > 0.8) {
          var hx = state.planet.x - state.player.position.x;
          var hz = state.planet.z - state.player.position.z;
          var dot = ((state.vx || 0) * hx + (state.vz || 0) * hz) / (spdB * (dP || 1));
          heading = dot > 0.35;
        }
        var pulse = 0.3 + 0.4 * Math.sin(state.bob * 2.5);
        state.destBeacon.material.opacity = heading ? pulse : (dP < softR ? 0.25 : 0.12);
        state.destBeacon.scale.setScalar(1 + Math.sin(state.bob * 2) * 0.04);
        if (state.destBeaconLabel) state.destBeaconLabel.visible = heading;
      }
      if (state.escapeBanner) {
        state.escapeBanner.visible = !!state.inOrbit;
        if (state.inOrbit) {
          state.escapeBanner.position.set(state.player.position.x, 2.2, state.player.position.z);
        }
      }
      if (state.inOrbit) {
        var oSteer = mergedSteer();
        state.orbitRadius = Math.max(1.4, Math.min(softR * 0.9, state.orbitRadius + (oSteer.y || 0) * 1.2 * dt));
        state.orbitAngle += (0.85 + (oSteer.x || 0) * 0.35) * dt;
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

    /* polish3: snappier locomotion (Canvas feel port) */
    /* tapsteer1: noticeably snappier walk + drive */
    var maxSp = state.mode === "space" ? 7.5 : state.inTruck ? 15.8 : 11.2;
    var accel = state.mode === "space" ? 16 : state.inTruck ? 38 : 34;
    var fric = state.mode === "space" ? 3.0 : state.inTruck ? 4.8 : 7.8;

    // Map screen WASD/D-pad → ground plane relative to locked camera
    // Canvas convention: steer.y < 0 = Up/W (screen up). Camera sits at +X+Z offset.
    // Into-scene (screen up) = (-1,-1) on XZ; screen-right = (+1,-1) on XZ.
    // Ben orbit: when locked, position is owned by orbit tick above
    if (!(state.mode === "space" && state.inOrbit)) {
    var steer = mergedSteer();
    if (steer.x || steer.y) {
      var len = Math.hypot(steer.x, steer.y) || 1;
      var ix = steer.x / len;
      var iy = steer.y / len; // Up/W is negative
      var inv = 0.70710678;
      var fx = -inv, fz = -inv; // screen up / into scene
      var rx = inv, rz = -inv;  // screen right
      var mx = rx * ix + fx * (-iy);
      var mz = rz * ix + fz * (-iy);
      if (Math.abs(mx) + Math.abs(mz) > 0.01) {
        var aimYaw = Math.atan2(mx, mz);
        if (state.inTruck) {
          /* truck1: smooth yaw toward aim; thrust along facing */
          var curY = (state.faceYaw != null) ? state.faceYaw : aimYaw;
          var turnRate = 3.8 + Math.min(2.2, Math.hypot(state.vx, state.vz) / 6);
          if (C.approachAngle) state.faceYaw = C.approachAngle(curY, aimYaw, turnRate * dt);
          else {
            var dY = aimYaw - curY;
            while (dY > Math.PI) dY -= Math.PI * 2;
            while (dY < -Math.PI) dY += Math.PI * 2;
            var st = turnRate * dt;
            if (dY > st) dY = st; if (dY < -st) dY = -st;
            state.faceYaw = curY + dY;
          }
          state.facing = Math.sin(state.faceYaw) >= 0 ? 1 : -1;
          var fxx = Math.sin(state.faceYaw), fzz = Math.cos(state.faceYaw);
          var blend = 0.2;
          var ax = fxx * (1 - blend) + mx * blend;
          var az = fzz * (1 - blend) + mz * blend;
          var al = Math.hypot(ax, az) || 1;
          state.vx += (ax / al) * accel * dt;
          state.vz += (az / al) * accel * dt;
        } else {
          state.vx += mx * accel * dt;
          state.vz += mz * accel * dt;
          state.facing = mx >= 0 ? 1 : -1;
          /* eyes1: rotate so eyes (+Z) face walk direction; idle keeps last yaw */
          state.faceYaw = aimYaw;
        }
      }
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
    state.player.scale.x = 1; /* eyes1: yaw instead of flip */
    state.player.rotation.y = (state.faceYaw != null) ? state.faceYaw : 0;

    /* Ranch truck water / ramp / shared pile-in (parity) — after steer, before clamp */
    if (state.mode === "ranch") {
      var wpos0 = threeToWorld(state.player.position.x, state.player.position.z);
      /* truck1: track elev ground plane (world z units → three lift) */
      var elevZ = 0;
      if (state.inTruck && C.onTrack && C.onTrack(wpos0.x, wpos0.y) && C.trackElevAt) {
        elevZ = (C.trackElevAt(wpos0.x, wpos0.y) || 0) * 0.018; /* canvas z → three y */
      }
      var prevG = state.groundLift != null ? state.groundLift : elevZ;
      if (state.inTruck && C.onTrack && C.onTrack(wpos0.x, wpos0.y)) {
        state.groundLift = prevG + (elevZ - prevG) * Math.min(1, 12 * dt);
      } else {
        state.groundLift = (state.groundLift || 0) * Math.exp(-7 * dt);
        if (Math.abs(state.groundLift) < 0.01) state.groundLift = 0;
      }
      var groundLift = state.groundLift || 0;
      var airL = (state.zLift || 0) - groundLift;
      if (state.inTruck && C.rampAt) {
        var ramp = C.rampAt(wpos0.x, wpos0.y);
        if (ramp && sp > 1.2 && airL < 0.15) {
          state.zVel = Math.max(state.zVel || 0, 4.5 * (ramp.boost || 1.3));
          state.zLift = Math.max(state.zLift || 0, groundLift + 0.12);
          state.scrap += 0.02;
        }
      }
      var dG = groundLift - prevG;
      if (state.inTruck && airL < 0.12 && sp > 3.2 && dG < -0.02) {
        var crest = Math.min(6.5, sp * 0.55 + (-dG) * 18);
        if (crest > 1.6) {
          state.zVel = Math.max(state.zVel || 0, crest);
          state.zLift = Math.max(state.zLift || 0, groundLift + 0.08);
        }
      }
      /* polish9 + truck1: air hang + land bounce vs groundLift */
      airL = (state.zLift || 0) - groundLift;
      var gFall = 14;
      if (airL > 0.55 && Math.abs(state.zVel || 0) < 2.2) gFall *= 0.38;
      if (airL > 0.02 || (state.zVel || 0) !== 0) {
        state.zVel = (state.zVel || 0) - gFall * dt;
        state.zLift = (state.zLift || 0) + state.zVel * dt;
        if (state.zLift <= groundLift) {
          var impact = Math.max(0, -(state.zVel || 0));
          state.zLift = groundLift;
          if (impact > 1.4) state.zVel = Math.min(3.2, impact * 0.28);
          else state.zVel = 0;
        }
      } else {
        state.zLift = groundLift;
        state.zVel = 0;
      }
      /* polish9: lap sparkle at gate */
      if (state.inTruck && C.onTrack && C.onTrack(wpos0.x, wpos0.y) && (state.zLift || 0) < 0.4) {
        state.lapCd = Math.max(0, (state.lapCd || 0) - dt);
        var side = (wpos0.x - 1870) * 0.55 + (wpos0.y - 2225) * (-0.85);
        var nearG = Math.abs(wpos0.x - 1870) < 110 && Math.abs(wpos0.y - 2225) < 60;
        if (nearG && state.lapSide && side * state.lapSide < 0 && state.lapCd <= 0) {
          state.lapCount = (state.lapCount || 0) + 1; state.lapCd = 2.4; state.scrap += 8;
          state.toast = "LAP " + state.lapCount + " · sparkle finish!"; state.toastT = 1.6;
          var gp2 = worldToThree(1870, 2225);
          for (var lpi = 0; lpi < 12; lpi++) {
            var spark = new THREE.Mesh(
              new THREE.SphereGeometry(0.06, 6, 5),
              new THREE.MeshBasicMaterial({ color: 0xfde68a, transparent: true, opacity: 0.95 })
            );
            spark.position.set(gp2.x + (Math.random() - 0.5) * 0.8, 0.4 + Math.random() * 0.8, gp2.z + (Math.random() - 0.5) * 0.8);
            scene.add(spark);
            if (!state.fx) state.fx = [];
            state.fx.push({ mesh: spark, life: 0.55, rise: 1.2 });
          }
        }
        if (nearG || Math.abs(side) > 40) state.lapSide = side >= 0 ? 1 : -1;
      }
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
      /* polish4: bounce + spray / bubbles / walk dust */
      state.bouncePhase = (state.bouncePhase || 0) + dt * (3 + sp * 0.4);
      var bounceY = state.inTruck && state.zLift < 0.5
        ? Math.sin(state.bouncePhase * 2.4) * Math.min(1.2, sp / 8) * 0.08 : 0;
      if (!state.fx) state.fx = [];
      if (!state.inTruck && !wet && sp > 1.2) {
        state.dustT = (state.dustT || 0) - dt;
        if (state.dustT <= 0) {
          state.dustT = 0.22;
          var dust = new THREE.Mesh(
            new THREE.SphereGeometry(0.08, 6, 5),
            new THREE.MeshBasicMaterial({ color: 0xb8a070, transparent: true, opacity: 0.5 })
          );
          dust.position.set(state.player.position.x - state.facing * 0.2, 0.08, state.player.position.z);
          scene.add(dust);
          state.fx.push({ mesh: dust, life: 0.35, rise: 0.2 });
        }
      }
      /* polish5: track race dust */
      if (state.inTruck && !wet && sp > 3.5 && state.zLift < 0.4 && C.onTrack && C.onTrack(wpos0.x, wpos0.y)) {
        if (Math.random() < dt * 3) {
          var td = new THREE.Mesh(
            new THREE.SphereGeometry(0.1 + Math.random() * 0.06, 6, 5),
            new THREE.MeshBasicMaterial({ color: 0xb8a070, transparent: true, opacity: 0.55 })
          );
          td.position.set(state.player.position.x - state.facing * 0.5, 0.1, state.player.position.z);
          scene.add(td);
          state.fx.push({ mesh: td, life: 0.4, rise: 0.25 });
        }
      }
      if (state.inTruck && wet) {
        if (state.waterSub > 0.7 && Math.random() < dt * 5) {
          var bub = new THREE.Mesh(
            new THREE.SphereGeometry(0.06 + Math.random() * 0.05, 6, 5),
            new THREE.MeshBasicMaterial({ color: 0xbae6fd, transparent: true, opacity: 0.65 })
          );
          bub.position.set(state.player.position.x + (Math.random() - 0.5) * 0.6, 0.2, state.player.position.z + (Math.random() - 0.5) * 0.4);
          scene.add(bub);
          state.fx.push({ mesh: bub, life: 0.55, rise: 1.2 });
        } else if (state.waterSub <= 0.7 && sp > 1 && Math.random() < dt * 4) {
          var spr = new THREE.Mesh(
            new THREE.SphereGeometry(0.07, 6, 5),
            new THREE.MeshBasicMaterial({ color: 0xe0f2fe, transparent: true, opacity: 0.7 })
          );
          spr.position.set(state.player.position.x - state.facing * 0.4, 0.15, state.player.position.z);
          scene.add(spr);
          state.fx.push({ mesh: spr, life: 0.4, rise: 0.9 });
          var rip = new THREE.Mesh(
            new THREE.RingGeometry(0.15, 0.22, 16),
            new THREE.MeshBasicMaterial({ color: 0xbae6fd, transparent: true, opacity: 0.6, side: THREE.DoubleSide })
          );
          rip.rotation.x = -Math.PI / 2;
          rip.position.set(state.player.position.x, 0.06, state.player.position.z);
          scene.add(rip);
          state.fx.push({ mesh: rip, life: 0.8, rise: 0, grow: 1.8 });
        }
      }
      /* polish5: ambient pond ripples */
      state.rippleT = (state.rippleT || 0) - dt;
      if (state.rippleT <= 0) {
        state.rippleT = 0.7 + Math.random() * 0.9;
        var pondA = (C.AREAS && C.AREAS[2]) || null;
        if (pondA) {
          var rpx = pondA.x + 80 + Math.random() * (pondA.w - 160);
          var rpy = pondA.y + 80 + Math.random() * (pondA.h - 160);
          var rp3 = worldToThree(rpx, rpy);
          var rip2 = new THREE.Mesh(
            new THREE.RingGeometry(0.12, 0.18, 16),
            new THREE.MeshBasicMaterial({ color: 0xe0f2fe, transparent: true, opacity: 0.55, side: THREE.DoubleSide })
          );
          rip2.rotation.x = -Math.PI / 2;
          rip2.position.set(rp3.x, 0.07, rp3.z);
          scene.add(rip2);
          state.fx.push({ mesh: rip2, life: 1.0, rise: 0, grow: 2.0 });
        }
      }
      /* polish5: garage door open-near */
      if (state.garageDoor) {
        var gdx = state.player.position.x - state.garageDoor.userData.cx;
        var gdz = state.player.position.z - state.garageDoor.userData.cz;
        var wantG = Math.hypot(gdx, gdz) < 5.5 ? 1 : 0;
        state.garageOpen = Math.max(0, Math.min(1, (state.garageOpen || 0) + (wantG ? 2.2 : -1.4) * dt));
        var lift = state.garageOpen * 1.35;
        state.garageDoor.position.y = state.garageDoor.userData.y0 + lift * 0.5;
        state.garageDoor.scale.y = Math.max(0.08, 1 - state.garageOpen * 0.9);
        if (state.garageOpenLabel) state.garageOpenLabel.visible = state.garageOpen > 0.35;
      }
      /* polish5: ambient drift */
      for (var ami = 0; ami < (state.ambient || []).length; ami++) {
        var am = state.ambient[ami];
        am.userData.phase += dt * (am.userData.kind === "firefly" ? 3.2 : 1.4);
        am.position.x += (am.userData.vx + Math.sin(am.userData.phase) * 0.25) * dt;
        am.position.z += (am.userData.vz + Math.cos(am.userData.phase * 0.7) * 0.2) * dt;
        am.position.y = 0.35 + Math.abs(Math.sin(am.userData.phase)) * 0.5;
        if (am.userData.kind === "firefly") am.material.opacity = 0.35 + 0.65 * Math.abs(Math.sin(am.userData.phase));
      }
      /* polish5: tiny land shake */
      var wasAir = state._wasAir;
      state._wasAir = state.zLift > 0.4;
      if (wasAir && state.zLift <= 0 && state.inTruck) {
        state.shakeT = Math.max(state.shakeT || 0, 0.1);
      }
      if ((state.shakeT || 0) > 0) {
        state.shakeT -= dt;
        camera.position.x += (Math.random() - 0.5) * 0.08;
        camera.position.y += (Math.random() - 0.5) * 0.05;
      }
      for (var fxi = state.fx.length - 1; fxi >= 0; fxi--) {
        var fx = state.fx[fxi];
        fx.life -= dt;
        fx.mesh.position.y += (fx.rise || 0.3) * dt;
        if (fx.grow) fx.mesh.scale.multiplyScalar(1 + fx.grow * dt);
        if (fx.vx) { fx.mesh.position.x += fx.vx * dt; fx.vx *= 0.96; }
        if (fx.vz) { fx.mesh.position.z += fx.vz * dt; }
        fx.mesh.material.opacity = Math.max(0, fx.life * 1.4);
        if (fx.life <= 0) { scene.remove(fx.mesh); state.fx.splice(fxi, 1); }
      }
      if (state.driveTruck) {
        state.driveTruck.visible = !!state.inTruck;
        if (state.inTruck) {
          state.driveTruck.position.set(state.player.position.x, 0.05 + state.zLift * 0.08 + bounceY, state.player.position.z);
          /* truck1: yaw to faceYaw (mesh nose +X → yaw so +X aligns with travel) */
          var ts0 = state.driveTruck.userData.truckScale || 2.05;
          state.driveTruck.scale.set(ts0, ts0, ts0);
          /* mesh long axis +X; faceYaw is atan2(vx,vz) with +Z front for frogs — truck nose +X needs -PI/2 */
          state.driveTruck.rotation.y = ((state.faceYaw != null) ? state.faceYaw : 0) - Math.PI / 2;
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
        if (!taken) {
          var dTruck = Math.hypot(state.player.position.x - pt.mesh.position.x, state.player.position.z - pt.mesh.position.z);
          var nearT = dTruck < 2.4;
          pt.mesh.position.y = nearT ? 0.06 + Math.abs(Math.sin(state.bob * 1.5)) * 0.08 : 0;
          var pts = pt.mesh.userData.truckScale || ((C.TRUCK_VIS && C.TRUCK_VIS.threeScale) || 2.05);
          pt.mesh.scale.setScalar(pts * (nearT ? 1.06 : 1));
        }
      }
      state.player.position.y = (state.zLift || 0) * 0.08 + Math.abs(Math.sin(state.bob)) * (sp > 0.5 ? 0.06 : 0.02);
    }

    // solid1: solid walls / mech pads / parked trucks (doorways stay walkable)
    if (state.mode === "ranch" && C.resolveSolid) {
      var wHit = threeToWorld(state.player.position.x, state.player.position.z);
      var radW = state.inTruck ? 38 : 22;
      var solidOpts = {
        garageOpen: state.garageOpen || 0,
        inTruck: !!state.inTruck,
        softPond: !state.inTruck,
      };
      var resolved = C.resolveSolid(wHit.x, wHit.y, radW, solidOpts);
      if (resolved.hit) {
        var tHit = worldToThree(resolved.x, resolved.y);
        /* Kill velocity into the blocker so we don't shudder into the wall */
        var pdx = tHit.x - state.player.position.x;
        var pdz = tHit.z - state.player.position.z;
        if (pdx * state.vx + pdz * state.vz < 0) {
          /* incoming — zero component along push */
          var plen = Math.hypot(pdx, pdz) || 1;
          var nx = pdx / plen, nz = pdz / plen;
          var into = state.vx * nx + state.vz * nz;
          if (into < 0) { state.vx -= nx * into; state.vz -= nz * into; }
        } else {
          state.vx *= 0.55; state.vz *= 0.55;
        }
        state.player.position.x = tHit.x;
        state.player.position.z = tHit.z;
      }
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
      state.nameTag.position.set(state.player.position.x, 2.6 + (state.player.position.y || 0) + (state.zLift || 0) * 0.15, state.player.position.z);
      state.nameTag.visible = true;
    }
    /* polish9: aboard icons when shared */
    if (state.aboardGroup) {
      var sharedOn = !!(state.inTruck && state.truckMode === "shared");
      state.aboardGroup.visible = sharedOn;
      if (sharedOn) {
        state.aboardGroup.position.set(state.player.position.x, (state.zLift || 0) * 0.15, state.player.position.z);
      }
      if (state.aboardLabel) {
        state.aboardLabel.visible = sharedOn;
        state.aboardLabel.position.set(state.player.position.x, 1.55 + (state.zLift || 0) * 0.15, state.player.position.z);
      }
    }
    if ((state.kitFxT || 0) > 0) {
      state.kitFxT -= dt;
      if (!state.kitFxMesh) {
        state.kitFxMesh = new THREE.Mesh(
          new THREE.SphereGeometry(0.35, 10, 8),
          new THREE.MeshBasicMaterial({ color: 0xfbbf24, transparent: true, opacity: 0.55 })
        );
        scene.add(state.kitFxMesh);
      }
      state.kitFxMesh.visible = state.kitFxT > 0;
      state.kitFxMesh.position.set(state.player.position.x, 1.2 + (0.55 - state.kitFxT) * 1.5, state.player.position.z);
      state.kitFxMesh.scale.setScalar(1 + (0.55 - state.kitFxT) * 2);
      state.kitFxMesh.material.opacity = Math.min(1, state.kitFxT * 2) * 0.55;
      if (state.kitFxT <= 0) state.kitFxMesh.visible = false;
    }

    // Locked orbit follow — camera offset fixed, no orbit controls / no FPS look
    var target = camera.userData.lockTarget;
    /* polish3: stick to player — no lag fight */
    var followK = Math.min(1, 14 * dt);
    target.x += (state.player.position.x - target.x) * followK;
    target.z += (state.player.position.z - target.z) * followK;
    target.y = 0;
    var camDist = state.mode === "ranch" ? 16 : 12;
    var camH = state.mode === "ranch" ? 18 : 14;
    /* polish6: slight walk bob / tilt */
    var walkBob = 0, walkTilt = 0;
    if (state.mode === "ranch" && !state.inTruck && Math.hypot(state.vx || 0, state.vz || 0) > 1.2) {
      state.walkBobT = (state.walkBobT || 0) + dt * 10;
      walkBob = Math.sin(state.walkBobT) * 0.05;
      walkTilt = Math.sin(state.walkBobT * 0.5) * 0.006;
    }
    camera.position.set(target.x + camDist * 0.85, camH + walkBob, target.z + camDist * 0.85);
    /* polish10: soft dusk sky shift over play time */
    if (state.mode === "ranch" && scene) {
      state.dayT = (state.dayT || 0) + dt;
      var day = (state.dayT % 420) / 420;
      var dusk = day < 0.45 ? 0 : (day < 0.7 ? (day - 0.45) / 0.25 : (day < 0.9 ? 1 : Math.max(0, 1 - (day - 0.9) / 0.1)));
      var col = scene.background && scene.background.isColor ? scene.background : new THREE.Color();
      col.setRGB(0.10 + dusk * 0.18, 0.22 - dusk * 0.06, 0.14 + dusk * 0.04);
      scene.background = col;
    }
    camera.lookAt(target.x, 0.5 + walkTilt, target.z);
    /* polish6: depth shadow under player */
    if (state.playerShadow) {
      var shS = state.inTruck ? 1.7 : 1;
      var shA = (state.zLift || 0) > 0.5 ? 0.12 : 0.32;
      state.playerShadow.position.set(state.player.position.x, 0.07, state.player.position.z);
      state.playerShadow.scale.set(shS, shS, shS);
      state.playerShadow.material.opacity = shA;
      if (state.playerShadowSoft) {
        state.playerShadowSoft.position.set(state.player.position.x, 0.065, state.player.position.z);
        state.playerShadowSoft.scale.set(shS * 1.2, shS * 1.2, shS * 1.2);
        state.playerShadowSoft.material.opacity = shA * 0.45;
      }
    }
    /* polish6: parallax-lite — hills drift slower than camera target */
    if (state.paraHills && state.mode === "ranch") {
      for (var phi = 0; phi < state.paraHills.length; phi++) {
        var ph = state.paraHills[phi];
        var para = ph.userData.para || 0.15;
        ph.position.x = -8 + phi * 10 + target.x * para * 0.15;
      }
    }
    /* polish6: James 1000-story mech wow tip */
    if (state.mode === "ranch") {
      var m1000 = (C.COMPOUND && C.COMPOUND.mech1000) || { x: 340, y: 2420 };
      var mp = worldToThree(m1000.x, m1000.y);
      var dM = Math.hypot(state.player.position.x - mp.x, state.player.position.z - mp.z);
      if (dM < 3.4 && state.toastT <= 0.3) {
        state.toast = "★ WOW · James 1000-story mech · scale tease";
        state.toastT = 1.8;
        if (hooks.onToast) hooks.onToast(state.toast);
      }
    }

    if (state.mode === "ranch") {
      for (var sci = 0; sci < (state.schools || []).length; sci++) {
        var scc = state.schools[sci]; scc.phase += dt * 0.4;
        scc.x += Math.cos(scc.phase) * 0.35 * dt; scc.z += Math.sin(scc.phase * 0.7) * 0.28 * dt;
      }
      for (var fi = 0; fi < (state.fish || []).length; fi++) {
        var fish = state.fish[fi];
        fish.userData.phase += dt * 2;
        var sch2 = state.schools && state.schools[fish.userData.school];
        if (sch2) {
          fish.position.x = sch2.x + (fish.userData.ox || 0) + Math.sin(fish.userData.phase) * 0.15;
          fish.position.z = sch2.z + (fish.userData.oz || 0) + Math.cos(fish.userData.phase * 0.7) * 0.12;
        } else {
          fish.position.x = fish.userData.bx + Math.sin(fish.userData.phase) * 0.4;
          fish.position.z = fish.userData.bz + Math.cos(fish.userData.phase * 0.7) * 0.28;
        }
      }
      for (var wj = 0; wj < (state.whales || []).length; wj++) {
        var wh = state.whales[wj];
        wh.userData.phase += dt * 0.7;
        wh.userData.breach = (wh.userData.breach || 0) + dt * 0.7;
        var bl = Math.max(0, Math.sin(wh.userData.breach)) * (wh.userData.breachAmp || 0.7);
        wh.position.x = wh.userData.bx + Math.sin(wh.userData.phase) * 0.9;
        wh.position.z = wh.userData.bz + Math.cos(wh.userData.phase * 0.55) * 0.55;
        wh.position.y = 0.22 + bl; /* polish8 breach arc */
      }
      /* polish8: Blue Bear pet bounce + chimney smoke */
      if (state.blueBear) {
        state.blueBear.userData.bob = (state.blueBear.userData.bob || 0) + dt * 3.2;
        state.blueBear.position.y = Math.abs(Math.sin(state.blueBear.userData.bob)) * 0.18;
      }
      for (var smi = 0; smi < (state.chimneySmoke || []).length; smi++) {
        var puff = state.chimneySmoke[smi];
        puff.userData.phase += dt * 1.2;
        puff.position.y = puff.userData.baseY + (puff.userData.phase % 2.5) * 0.35;
        puff.position.x += Math.sin(puff.userData.phase) * 0.01;
        puff.material.opacity = 0.45 - (puff.userData.phase % 2.5) * 0.12;
      }
      for (var ci = 0; ci < state.companions.length; ci++) {
        var c = state.companions[ci];
        var lag = c.userData.followLag || 0.4;
        c.userData.idleBounce = (c.userData.idleBounce || 0) + dt * 4.2;
        if (state.inTruck && state.truckMode === "shared") {
          var ox = (ci - 1) * 0.45, oz = -0.35 - (ci % 2) * 0.25;
          c.position.x += (state.player.position.x + ox - c.position.x) * Math.min(1, 8 * dt);
          c.position.z += (state.player.position.z + oz - c.position.z) * Math.min(1, 8 * dt);
          c.position.y = 0.7 + (state.zLift || 0) * 0.08 + Math.abs(Math.sin(c.userData.idleBounce)) * 0.05;
          c.visible = true;
          c.userData.faceYaw = state.faceYaw || 0;
        } else {
          c.visible = true;
          c.position.y = Math.abs(Math.sin(c.userData.idleBounce)) * 0.14;
          c.userData.timer -= dt;
          if (c.userData.timer <= 0) {
            var behind = -(state.facing || 1) * (1.2 + lag * 2.2);
            c.userData.tx = state.player.position.x + behind + (Math.random() - 0.5) * (3 + lag * 2);
            c.userData.tz = state.player.position.z + (Math.random() - 0.5) * (3 + lag * 2);
            c.userData.timer = 0.9 + lag + Math.random() * (1.3 + lag);
          }
          var fk = Math.min(1, (1.05 / (0.7 + lag)) * dt);
          var cdx = c.userData.tx - c.position.x;
          var cdz = c.userData.tz - c.position.z;
          c.position.x += cdx * fk;
          c.position.z += cdz * fk;
          /* eyes1: companions face where they're moving */
          if (Math.hypot(cdx, cdz) > 0.05) c.userData.faceYaw = Math.atan2(cdx, cdz);
        }
        c.scale.x = 1;
        c.rotation.y = (c.userData.faceYaw != null) ? c.userData.faceYaw : 0;
        if (c.userData.nameTag) {
          c.userData.nameTag.position.set(c.position.x, c.position.y + 2.2, c.position.z);
          c.userData.nameTag.visible = c.visible;
        }
      }
      /* polish7: zone signs fade when approaching */
      var wpos2 = threeToWorld(state.player.position.x, state.player.position.z);
      for (var zsi = 0; zsi < (state.zoneSigns || []).length; zsi++) {
        var zl = state.zoneSigns[zsi];
        var za = C.zoneSignAlpha ? C.zoneSignAlpha(zl.userData.zone, wpos2.x, wpos2.y) : 0;
        zl.material.opacity = za;
        zl.visible = za > 0.02;
      }
      /* polish7: mini-map lite */
      if (state.miniMapCtx && state.miniMapCanvas) {
        var mctx = state.miniMapCtx, mc = state.miniMapCanvas;
        var mw = mc.width, mh = mc.height;
        mctx.clearRect(0, 0, mw, mh);
        mctx.fillStyle = "rgba(15,23,42,0.78)";
        mctx.fillRect(0, 0, mw, mh);
        mctx.strokeStyle = "rgba(251,191,36,0.55)";
        mctx.strokeRect(0.5, 0.5, mw - 1, mh - 1);
        function mmx(x) { return (x / C.MAP_W) * mw; }
        function mmy(y) { return (y / C.MAP_H) * mh; }
        var marks = [[380,1630,"#fbbf24"],[2780,2270,"#a8a29e"],[3160,670,"#67e8f9"],[940,1660,"#fdba74"],[360,320,"#fde68a"]];
        for (var mi = 0; mi < marks.length; mi++) {
          mctx.fillStyle = marks[mi][2];
          mctx.beginPath(); mctx.arc(mmx(marks[mi][0]), mmy(marks[mi][1]), 2.8, 0, Math.PI * 2); mctx.fill();
        }
        var meW = threeToWorld(state.player.position.x, state.player.position.z);
        mctx.fillStyle = (C.FROG_DEFS[state.frogId] || {}).color || "#fff";
        mctx.beginPath(); mctx.arc(mmx(meW.x), mmy(meW.y), 4, 0, Math.PI * 2); mctx.fill();
        for (var cmi = 0; cmi < state.companions.length; cmi++) {
          var cm = state.companions[cmi];
          var cw = threeToWorld(cm.position.x, cm.position.z);
          var cdef2 = C.FROG_DEFS[cm.userData.frogId] || {};
          mctx.fillStyle = cdef2.color || "#fff";
          mctx.beginPath(); mctx.arc(mmx(cw.x), mmy(cw.y), 2.6, 0, Math.PI * 2); mctx.fill();
        }
        mctx.fillStyle = "#fef3c7";
        mctx.font = "bold 9px system-ui,sans-serif";
        mctx.fillText("MAP", 6, 11);
      }
      var wpos = threeToWorld(state.player.position.x, state.player.position.z);
      state.near = C.nearestHotspot(wpos.x, wpos.y, 70);
      if (state.mode === "ranch" && state.near && state.near.id !== state.prevNearId) {
        for (var spi = 0; spi < 10; spi++) {
          var ang = Math.random() * Math.PI * 2, ssp = 0.8 + Math.random() * 1.6;
          var spk = new THREE.Mesh(
            new THREE.SphereGeometry(0.06, 6, 5),
            new THREE.MeshBasicMaterial({ color: 0xfde68a, transparent: true, opacity: 0.95 })
          );
          var np = worldToThree(state.near.x, state.near.y);
          spk.position.set(np.x, 0.6, np.z);
          scene.add(spk);
          state.fx.push({ mesh: spk, life: 0.45, rise: 0.6, vx: Math.cos(ang) * ssp, vz: Math.sin(ang) * ssp });
        }
        state.prevNearId = state.near.id;
      } else if (!state.near) state.prevNearId = null;
      for (var hi = 0; hi < state.hotMeshes.length; hi++) {
        var hg = state.hotMeshes[hi];
        hg.ring.material.opacity = state.near && state.near.id === hg.data.id ? 0.85 : 0.35;
      }
    } else {
      state.jimmy.position.x += state.jimmyVx * dt;
      state.jimmy.position.z += state.jimmyVz * dt;
      if (Math.abs(state.jimmy.position.x) > 8) state.jimmyVx *= -1;
      if (Math.abs(state.jimmy.position.z) > 8) state.jimmyVz *= -1;
      if (state.jimmyJetT > 0) state.jimmyJetT -= dt;
      var jetBoost = state.jimmyJetT > 0 ? 0.55 + state.jimmyJetT * 0.8 : 0;
      state.jimmy.position.y = 0.15 + Math.abs(Math.sin(state.bob * 1.4)) * 0.35 + jetBoost;
      if (state.jimmyLabel) {
        state.jimmyLabel.position.set(state.jimmy.position.x, 1.8 + jetBoost, state.jimmy.position.z);
      }
      if (state.jimmyFlame) {
        state.jimmyFlame.visible = state.jimmyJetT > 0;
        if (state.jimmyFlame.visible) {
          state.jimmyFlame.position.set(state.jimmy.position.x, state.jimmy.position.y - 0.4, state.jimmy.position.z);
          state.jimmyFlame.material.opacity = Math.min(1, state.jimmyJetT * 2);
        }
      }
      var dJ = state.player.position.distanceTo(state.jimmy.position);
      var dR = Math.hypot(state.player.position.x - state.returnPad.x, state.player.position.z - state.returnPad.z);
      state.near = null;
      if (dJ < 1.1) state.near = { id: "jimmy", tip: "Catch Jimmy!" };
      else if (dR < 1.6) state.near = { id: "return", tip: "Return to ranch" };
      /* polish6: invader silhouettes when near Mars */
      if (state.marsPos) {
        var dMars = Math.hypot(state.player.position.x - state.marsPos.x, state.player.position.z - state.marsPos.z);
        var nearMars = dMars < 3.2;
        for (var isi2 = 0; isi2 < (state.invSil || []).length; isi2++) {
          state.invSil[isi2].visible = nearMars;
          if (nearMars) state.invSil[isi2].material.opacity = 0.4 + 0.2 * Math.sin(state.bob * 2 + isi2);
        }
        if (state.invLabel) state.invLabel.visible = nearMars;
        if (nearMars && state.toastT <= 0.2) {
          state.toast = "Invader mechs · distant silhouette tease";
          state.toastT = 1.6;
        }
      }
    }

    if (wantInteract) {
      wantInteract = false;
      doInteract();
    }
    if (wantAbility) {
      wantAbility = false;
      doAbility();
    }

    updateTapMarkerVisual(dt);
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
        tip: state.toastT > 0 ? state.toast : state.inOrbit ? "Orbit locked · Escape or hard thruster" : state.inTruck ? "EXIT TRUCK · INTERACT / E" : state.near ? (((C.isTruckHotspot && C.isTruckHotspot(state.near)) ? "BOARD · " : "⚡ ") + state.near.tip + " · INTERACT / E") : (state.invLabel && state.invLabel.visible ? "Mars · invader silhouettes" : ""),
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


  function ensureTapMarkerEl() {
    var el = document.getElementById("tap-steer-marker");
    if (el) return el;
    el = document.createElement("div");
    el.id = "tap-steer-marker";
    el.setAttribute("aria-hidden", "true");
    el.innerHTML = '<span class="tap-ring"></span><span class="tap-arrow"></span>';
    document.body.appendChild(el);
    return el;
  }

  function showTapMarker(clientX, clientY, ang) {
    var el = ensureTapMarkerEl();
    el.classList.add("is-on");
    el.style.left = clientX + "px";
    el.style.top = clientY + "px";
    el.style.setProperty("--tap-ang", ang + "rad");
    tapMarker = { sx: clientX, sy: clientY, life: 0.55, ang: ang };
  }

  function hideTapMarkerSoon() {
    if (tapMarker) tapMarker.life = Math.min(tapMarker.life, 0.28);
  }

  function updateTapMarkerVisual(dt) {
    var el = document.getElementById("tap-steer-marker");
    if (!tapMarker) {
      if (el) el.classList.remove("is-on");
      return;
    }
    tapMarker.life -= dt;
    if (tapMarker.life <= 0) {
      tapMarker = null;
      if (el) el.classList.remove("is-on");
      return;
    }
    if (el) {
      el.classList.add("is-on");
      el.style.opacity = String(Math.min(1, tapMarker.life * 2.2));
    }
  }

  function canvasLocalPointer(e) {
    if (!renderer || !renderer.domElement) return null;
    var rect = renderer.domElement.getBoundingClientRect();
    return { x: e.clientX - rect.left, y: e.clientY - rect.top, cx: e.clientX, cy: e.clientY, w: rect.width, h: rect.height };
  }

  function applyTapAim(e) {
    if (!active || !state || !state.player || !camera || !renderer) return;
    var loc = canvasLocalPointer(e);
    if (!loc) return;
    var v = new THREE.Vector3(state.player.position.x, 0.6 + (state.zLift || 0) * 0.08, state.player.position.z);
    v.project(camera);
    var sx = (v.x * 0.5 + 0.5) * loc.w;
    var sy = (-v.y * 0.5 + 0.5) * loc.h;
    var dx = loc.x - sx;
    var dy = loc.y - sy;
    var len = Math.hypot(dx, dy);
    if (len < 14) return;
    tapSteer.x = dx / len;
    tapSteer.y = dy / len;
    tapHeld = true;
    showTapMarker(loc.cx, loc.cy, Math.atan2(dy, dx));
  }

  function clearTapAim() {
    tapHeld = false;
    tapSteer.x = 0;
    tapSteer.y = 0;
    hideTapMarkerSoon();
  }

  function wireTapSteer(el) {
    if (!el || el.dataset.ffTapSteer === "1") return;
    el.dataset.ffTapSteer = "1";
    el.addEventListener("pointerdown", function (e) {
      if (!active) return;
      if (e.button != null && e.button !== 0) return;
      e.preventDefault();
      try { el.setPointerCapture(e.pointerId); } catch (err) {}
      applyTapAim(e);
    });
    el.addEventListener("pointermove", function (e) {
      if (!active || !tapHeld) return;
      applyTapAim(e);
    });
    function up(e) {
      if (!tapHeld) return;
      clearTapAim();
    }
    el.addEventListener("pointerup", up);
    el.addEventListener("pointercancel", up);
    el.addEventListener("pointerleave", function (e) {
      if (tapHeld && e.pressure === 0) clearTapAim();
    });
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

    host.style.display = "block";
    host.removeAttribute("hidden");
    host.hidden = false;
    var hostW = Math.max(host.clientWidth || 0, window.innerWidth || 320);
    var hostH = Math.max(host.clientHeight || 0, window.innerHeight || 480);
    renderer = new THREE.WebGLRenderer({ antialias: true, alpha: false, powerPreference: "high-performance" });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    renderer.setSize(hostW, hostH, false);
    renderer.shadowMap.enabled = true;
    renderer.setClearColor(0x87b5d9, 1);
    host.appendChild(renderer.domElement);
    renderer.domElement.style.display = "block";
    renderer.domElement.style.width = "100%";
    renderer.domElement.style.height = "100%";
    renderer.domElement.style.touchAction = "none";
    renderer.domElement.setAttribute("aria-label", "Four Froggies three.js ranch");
    wireTapSteer(renderer.domElement);

    clock = new THREE.Clock();
    buildRanch();
    active = true;
    window.addEventListener("resize", resize);
    window.addEventListener("orientationchange", function () { setTimeout(resize, 60); });
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
