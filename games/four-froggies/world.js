/* Four Froggies — 2.5D ranch hub (fixed-angle / y-sorted layers).
   Areas (Ben-named only): ranch house, monster truck track, pond (+ toys prop density).
   No invented cast/zone/toy names. */
(function (global) {
  "use strict";

  var MAP_W = 1200;
  var MAP_H = 900;

  /** @type {{ id: string, name: string, x: number, y: number, w: number, h: number, color: string }[]} */
  var AREAS = [
    { id: "house", name: "Ranch house", x: 80, y: 280, w: 320, h: 240, color: "#8b5a2b" },
    { id: "track", name: "Monster truck track", x: 480, y: 480, w: 620, h: 340, color: "#57534e" },
    { id: "pond", name: "Pond", x: 520, y: 60, w: 520, h: 280, color: "#0e7490" },
  ];

  /** Ramp jumps on monster truck track (world coords). */
  var RAMPS = [
    { x: 620, y: 580, w: 56, h: 28, boost: 1.35 },
    { x: 880, y: 640, w: 56, h: 28, boost: 1.4 },
    { x: 760, y: 740, w: 60, h: 28, boost: 1.3 },
    { x: 980, y: 560, w: 52, h: 26, boost: 1.25 },
  ];

  var FROG_COLORS = {
    james: { body: "#4ade80", accent: "#166534", hat: "#facc15" },
    jimmy: { body: "#fb923c", accent: "#9a3412", hat: "#ef4444" },
    bubbles: { body: "#60a5fa", accent: "#1e3a8a", hat: "#38bdf8" },
    rexy: { body: "#c084fc", accent: "#6b21a8", hat: "#e879f9" },
  };

  function clamp(v, a, b) {
    return Math.max(a, Math.min(b, v));
  }

  function areaAt(x, y) {
    for (var i = 0; i < AREAS.length; i++) {
      var a = AREAS[i];
      if (x >= a.x && x <= a.x + a.w && y >= a.y && y <= a.y + a.h) return a;
    }
    return null;
  }

  function areaNameAt(x, y) {
    var a = areaAt(x, y);
    return a ? a.name : "Ranch grounds";
  }

  function onTrack(x, y) {
    var a = AREAS[1];
    return x >= a.x && x <= a.x + a.w && y >= a.y && y <= a.y + a.h;
  }

  function rampAt(x, y) {
    for (var i = 0; i < RAMPS.length; i++) {
      var r = RAMPS[i];
      if (x >= r.x - r.w * 0.5 && x <= r.x + r.w * 0.5 &&
          y >= r.y - r.h * 0.5 && y <= r.y + r.h * 0.5) return r;
    }
    return null;
  }

  /** Fixed-angle 2.5D: world (x,y) → screen with depth foreshortening. */
  function project(wx, wy, camX, camY, vw, vh) {
    var dx = wx - camX;
    var dy = wy - camY;
    var sx = vw * 0.5 + dx * 1.05 - dy * 0.35;
    var sy = vh * 0.42 + dx * 0.22 + dy * 0.72;
    var depth = clamp(0.72 + (wy - camY) / MAP_H * 0.35, 0.55, 1.15);
    return { x: sx, y: sy, depth: depth, sortY: wy };
  }

  function createWorld() {
    return {
      mapW: MAP_W,
      mapH: MAP_H,
      areas: AREAS,
      ramps: RAMPS,
      hotspots: [
        { id: "phone", label: "Phone", x: 200, y: 360, r: 48, tip: "Call Purple Bear" },
        { id: "sps", label: "SPS", x: 280, y: 400, r: 44, tip: "Solar Positioning System" },
        { id: "truck", label: "Cybertruck", x: 700, y: 620, r: 56, tip: "Drive the track · jumps!" },
        { id: "fishies", label: "Fishies", x: 760, y: 180, r: 50, tip: "Splash the pond" },
      ],
      fish: [],
      toys: [],
      dust: [],
      splashes: [],
      sparks: [],
      scrap: 0,
      stuntCombo: 0,
      airTime: 0,
      lastJumpT: 0,
    };
  }

  function seedDecor(world) {
    world.fish = [];
    for (var i = 0; i < 10; i++) {
      world.fish.push({
        x: 560 + Math.random() * 440,
        y: 100 + Math.random() * 200,
        phase: Math.random() * Math.PI * 2,
        speed: 0.6 + Math.random() * 0.8,
        scare: 0,
        size: 0.8 + Math.random() * 0.5,
      });
    }
    world.toys = [];
    var spots = [
      [120, 480], [160, 520], [340, 500], [380, 450],
      [100, 420], [360, 540], [420, 380], [250, 550],
      [190, 500], [300, 470],
    ];
    for (var t = 0; t < spots.length; t++) {
      world.toys.push({
        x: spots[t][0],
        y: spots[t][1],
        kind: t % 3,
        hue: (t * 47) % 360,
      });
    }
    world.dust = [];
    world.splashes = [];
    world.sparks = [];
    world.scrap = 0;
    world.stuntCombo = 0;
    world.airTime = 0;
  }

  function makeFrogEntity(id, human, local, laneIndex) {
    var colors = FROG_COLORS[id] || FROG_COLORS.james;
    var ox = 220 + (laneIndex % 2) * 36;
    var oy = 420 + Math.floor(laneIndex / 2) * 40;
    return {
      id: id,
      human: !!human,
      local: !!local,
      x: ox,
      y: oy,
      vx: 0,
      vy: 0,
      facing: 1,
      inTruck: false,
      z: 0,
      zVel: 0,
      color: colors.body,
      accent: colors.accent,
      hat: colors.hat,
      cd: 0,
      invuln: 0,
      dashTrail: 0,
      alive: true,
      aiTimer: 0,
      targetX: ox,
      targetY: oy,
      steerX: 0,
      steerY: 0,
      speedBoost: 1,
    };
  }

  function nearestHotspot(world, x, y, maxR) {
    var best = null;
    var bestD = maxR || 70;
    for (var i = 0; i < world.hotspots.length; i++) {
      var h = world.hotspots[i];
      var d = Math.hypot(h.x - x, h.y - y);
      if (d < bestD) {
        bestD = d;
        best = h;
      }
    }
    return best;
  }

  function spawnDust(world, x, y, n) {
    for (var i = 0; i < (n || 4); i++) {
      world.dust.push({
        x: x + (Math.random() - 0.5) * 20,
        y: y + (Math.random() - 0.5) * 12,
        life: 0.35 + Math.random() * 0.35,
        vx: (Math.random() - 0.5) * 40,
        vy: (Math.random() - 0.5) * 30,
        r: 3 + Math.random() * 5,
      });
    }
  }

  function spawnSplash(world, x, y, n) {
    for (var i = 0; i < (n || 10); i++) {
      world.splashes.push({
        x: x + (Math.random() - 0.5) * 30,
        y: y + (Math.random() - 0.5) * 20,
        life: 0.5 + Math.random() * 0.4,
        vx: (Math.random() - 0.5) * 90,
        vy: -40 - Math.random() * 80,
        r: 2 + Math.random() * 4,
      });
    }
  }

  function spawnSparks(world, x, y, n) {
    for (var i = 0; i < (n || 8); i++) {
      world.sparks.push({
        x: x,
        y: y,
        life: 0.3 + Math.random() * 0.35,
        vx: (Math.random() - 0.5) * 120,
        vy: -20 - Math.random() * 100,
        hue: 40 + Math.random() * 40,
      });
    }
  }

  function scareFishies(world, x, y) {
    for (var i = 0; i < world.fish.length; i++) {
      var f = world.fish[i];
      var d = Math.hypot(f.x - x, f.y - y);
      if (d < 140) {
        f.scare = 1.2 + Math.random() * 0.6;
        var ang = Math.atan2(f.y - y, f.x - x);
        f.x += Math.cos(ang) * 28;
        f.y += Math.sin(ang) * 22;
      }
    }
    spawnSplash(world, x, y, 14);
  }

  function updateFish(world, dt) {
    var pond = AREAS[2];
    for (var i = 0; i < world.fish.length; i++) {
      var f = world.fish[i];
      f.phase += dt * f.speed * (f.scare > 0 ? 3.5 : 1);
      if (f.scare > 0) f.scare -= dt;
      var sp = f.scare > 0 ? 55 : 18;
      f.x += Math.cos(f.phase) * sp * dt;
      f.y += Math.sin(f.phase * 0.7) * (sp * 0.7) * dt;
      f.x = clamp(f.x, pond.x + 20, pond.x + pond.w - 20);
      f.y = clamp(f.y, pond.y + 20, pond.y + pond.h - 20);
    }
  }

  function updateFx(world, dt) {
    var i;
    for (i = world.dust.length - 1; i >= 0; i--) {
      var d = world.dust[i];
      d.life -= dt;
      d.x += d.vx * dt;
      d.y += d.vy * dt;
      if (d.life <= 0) world.dust.splice(i, 1);
    }
    for (i = world.splashes.length - 1; i >= 0; i--) {
      var s = world.splashes[i];
      s.life -= dt;
      s.x += s.vx * dt;
      s.y += s.vy * dt;
      s.vy += 180 * dt;
      if (s.life <= 0) world.splashes.splice(i, 1);
    }
    for (i = world.sparks.length - 1; i >= 0; i--) {
      var k = world.sparks[i];
      k.life -= dt;
      k.x += k.vx * dt;
      k.y += k.vy * dt;
      k.vy += 220 * dt;
      if (k.life <= 0) world.sparks.splice(i, 1);
    }
  }

  /**
   * Drive physics: jumps on ramps while in truck on track.
   * Returns { jumped, landed, scrapGain } for SFX/toast.
   */
  function tickDrive(world, ent, dt) {
    var result = { jumped: false, landed: false, scrapGain: 0 };
    if (!ent.inTruck) {
      if (ent.z > 0) {
        ent.zVel -= 420 * dt;
        ent.z += ent.zVel * dt;
        if (ent.z <= 0) {
          ent.z = 0;
          ent.zVel = 0;
          result.landed = true;
        }
      }
      ent.speedBoost = 1;
      return result;
    }

    var speed = Math.hypot(ent.vx, ent.vy);
    var ramp = rampAt(ent.x, ent.y);
    if (ramp && ent.z <= 0 && speed > 80) {
      ent.zVel = 220 * ramp.boost * clamp(speed / 200, 0.6, 1.4);
      ent.z = 2;
      result.jumped = true;
      world.stuntCombo += 1;
      var gain = 10 + world.stuntCombo * 5;
      world.scrap += gain;
      result.scrapGain = gain;
      spawnDust(world, ent.x, ent.y, 6);
      spawnSparks(world, ent.x, ent.y, 6);
    }

    if (ent.z > 0) {
      ent.zVel -= 480 * dt;
      ent.z += ent.zVel * dt;
      world.airTime += dt;
      if (ent.z <= 0) {
        ent.z = 0;
        ent.zVel = 0;
        result.landed = true;
        if (world.airTime > 0.35) {
          var airBonus = Math.floor(world.airTime * 25);
          world.scrap += airBonus;
          result.scrapGain += airBonus;
        }
        world.airTime = 0;
        spawnDust(world, ent.x, ent.y, 5);
      }
    } else if (speed < 40) {
      world.stuntCombo = 0;
    }

    // Scrap scrape when drifting hard on track
    if (onTrack(ent.x, ent.y) && speed > 140 && Math.abs(ent.steerX) > 0.6) {
      if (Math.random() < dt * 4) {
        world.scrap += 1;
        result.scrapGain += 1;
        spawnSparks(world, ent.x - ent.facing * 18, ent.y + 6, 2);
      }
    }

    return result;
  }

  function moveEntity(ent, dt, speed) {
    var base = ent.inTruck ? 260 : 140;
    if (ent.inTruck && ent.dashTrail > 0) base *= 1.35;
    var sp = (speed || base) * (ent.speedBoost || 1);
    var mx = ent.steerX;
    var my = ent.steerY;
    var mag = Math.hypot(mx, my);
    if (mag > 1) {
      mx /= mag;
      my /= mag;
    }
    ent.vx = mx * sp;
    ent.vy = my * sp;
    if (mag > 0.05) {
      ent.facing = mx >= 0 ? 1 : -1;
      ent.x += ent.vx * dt;
      ent.y += ent.vy * dt;
    } else {
      ent.vx *= 0.82;
      ent.vy *= 0.82;
    }
    ent.x = clamp(ent.x, 40, MAP_W - 40);
    ent.y = clamp(ent.y, 40, MAP_H - 40);
    if (ent.cd > 0) ent.cd -= dt;
    if (ent.invuln > 0) ent.invuln -= dt;
    if (ent.dashTrail > 0) ent.dashTrail -= dt;
  }

  function tickHubAI(frogs, localFrog, dt) {
    for (var i = 0; i < frogs.length; i++) {
      var f = frogs[i];
      if (f.human || !f.alive) continue;
      f.aiTimer -= dt;
      if (f.aiTimer <= 0) {
        f.aiTimer = 1.2 + Math.random() * 1.8;
        if (localFrog && Math.random() < 0.55) {
          f.targetX = localFrog.x + (Math.random() - 0.5) * 120;
          f.targetY = localFrog.y + (Math.random() - 0.5) * 120;
        } else {
          var a = AREAS[Math.floor(Math.random() * AREAS.length)];
          f.targetX = a.x + a.w * (0.3 + Math.random() * 0.4);
          f.targetY = a.y + a.h * (0.3 + Math.random() * 0.4);
        }
      }
      var dx = f.targetX - f.x;
      var dy = f.targetY - f.y;
      var d = Math.hypot(dx, dy) || 1;
      if (d < 28) {
        f.steerX = 0;
        f.steerY = 0;
      } else {
        f.steerX = dx / d;
        f.steerY = dy / d;
      }
      if (localFrog) f.inTruck = localFrog.inTruck;
      moveEntity(f, dt);
    }
  }

  function drawSky(ctx, w, h, t, camX, camY) {
    var g = ctx.createLinearGradient(0, 0, 0, h);
    g.addColorStop(0, "#1a3a6e");
    g.addColorStop(0.35, "#4a90b8");
    g.addColorStop(0.62, "#87ce6a");
    g.addColorStop(1, "#3d6b28");
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, w, h);

    // Parallax distant hills (slow vs cam)
    var px = -camX * 0.12;
    var py = -camY * 0.06;
    ctx.fillStyle = "rgba(40, 80, 50, 0.55)";
    ctx.beginPath();
    ctx.moveTo(0, h * 0.38 + py);
    for (var i = 0; i <= 8; i++) {
      var hx = (i / 8) * w + px * 0.5;
      var hy = h * 0.32 + Math.sin(i * 1.1 + t * 0.05) * 18 + py;
      ctx.lineTo(hx, hy);
    }
    ctx.lineTo(w, h * 0.55);
    ctx.lineTo(0, h * 0.55);
    ctx.closePath();
    ctx.fill();

    ctx.fillStyle = "rgba(55, 110, 60, 0.5)";
    ctx.beginPath();
    ctx.moveTo(0, h * 0.44 + py * 0.7);
    for (var j = 0; j <= 10; j++) {
      var hx2 = (j / 10) * w + px;
      var hy2 = h * 0.4 + Math.sin(j * 0.9 + 2) * 14 + py * 0.7;
      ctx.lineTo(hx2, hy2);
    }
    ctx.lineTo(w, h * 0.58);
    ctx.lineTo(0, h * 0.58);
    ctx.closePath();
    ctx.fill();

    // Soft sun
    var sunX = w * 0.82 - camX * 0.02;
    var sunY = h * 0.1 - camY * 0.015;
    var sg = ctx.createRadialGradient(sunX, sunY, 4, sunX, sunY, 55);
    sg.addColorStop(0, "rgba(255, 245, 180, 0.95)");
    sg.addColorStop(0.4, "rgba(255, 220, 120, 0.45)");
    sg.addColorStop(1, "rgba(255, 200, 80, 0)");
    ctx.fillStyle = sg;
    ctx.beginPath();
    ctx.arc(sunX, sunY, 55, 0, Math.PI * 2);
    ctx.fill();

    // Soft clouds (parallax)
    ctx.fillStyle = "rgba(255,255,255,0.18)";
    for (var c = 0; c < 4; c++) {
      var cx = ((c * 180 + t * 8 + px * 0.4) % (w + 120)) - 60;
      var cy = h * 0.1 + c * 18 + py * 0.3;
      ctx.beginPath();
      ctx.ellipse(cx, cy, 48, 14, 0, 0, Math.PI * 2);
      ctx.ellipse(cx + 28, cy + 4, 36, 12, 0, 0, Math.PI * 2);
      ctx.fill();
    }
  }

  function drawGroundPoly(ctx, corners, fill, stroke) {
    if (!corners.length) return;
    ctx.beginPath();
    ctx.moveTo(corners[0].x, corners[0].y);
    for (var i = 1; i < corners.length; i++) ctx.lineTo(corners[i].x, corners[i].y);
    ctx.closePath();
    ctx.fillStyle = fill;
    ctx.fill();
    if (stroke) {
      ctx.strokeStyle = stroke;
      ctx.lineWidth = 2;
      ctx.stroke();
    }
  }

  function areaCorners(area, camX, camY, vw, vh) {
    var pts = [
      [area.x, area.y],
      [area.x + area.w, area.y],
      [area.x + area.w, area.y + area.h],
      [area.x, area.y + area.h],
    ];
    return pts.map(function (p) {
      return project(p[0], p[1], camX, camY, vw, vh);
    });
  }

  function drawRanchHouse(ctx, camX, camY, vw, vh) {
    var a = AREAS[0];
    var base = areaCorners(a, camX, camY, vw, vh);
    drawGroundPoly(ctx, base, "rgba(100, 70, 40, 0.4)", "rgba(60,30,10,0.35)");

    // Porch slab
    var porch = [
      project(a.x + 50, a.y + a.h - 70, camX, camY, vw, vh),
      project(a.x + a.w - 50, a.y + a.h - 70, camX, camY, vw, vh),
      project(a.x + a.w - 40, a.y + a.h - 20, camX, camY, vw, vh),
      project(a.x + 40, a.y + a.h - 20, camX, camY, vw, vh),
    ];
    drawGroundPoly(ctx, porch, "rgba(160, 130, 90, 0.7)", "rgba(80,50,20,0.5)");

    var p0 = project(a.x + 40, a.y + 60, camX, camY, vw, vh);
    var p1 = project(a.x + a.w - 40, a.y + 60, camX, camY, vw, vh);
    var p2 = project(a.x + a.w - 40, a.y + a.h - 40, camX, camY, vw, vh);
    var p3 = project(a.x + 40, a.y + a.h - 40, camX, camY, vw, vh);
    var wallH = 56 * ((p0.depth + p2.depth) * 0.5);

    // Side wall (depth face)
    ctx.fillStyle = "#a68962";
    ctx.beginPath();
    ctx.moveTo(p1.x, p1.y - wallH);
    ctx.lineTo(p1.x + 18 * p1.depth, p1.y - wallH - 10);
    ctx.lineTo(p2.x + 18 * p2.depth, p2.y - wallH * 0.65 - 8);
    ctx.lineTo(p2.x, p2.y - wallH * 0.7);
    ctx.closePath();
    ctx.fill();

    // Front wall
    var wg = ctx.createLinearGradient(p0.x, p0.y - wallH, p3.x, p3.y);
    wg.addColorStop(0, "#d4b896");
    wg.addColorStop(1, "#b8956a");
    ctx.fillStyle = wg;
    ctx.beginPath();
    ctx.moveTo(p0.x, p0.y - wallH);
    ctx.lineTo(p1.x, p1.y - wallH);
    ctx.lineTo(p2.x, p2.y - wallH * 0.7);
    ctx.lineTo(p3.x, p3.y - wallH * 0.7);
    ctx.closePath();
    ctx.fill();
    ctx.strokeStyle = "#5d4037";
    ctx.lineWidth = 2;
    ctx.stroke();

    // Windows
    function windowAt(wx, wy, ww, wh) {
      var wp = project(wx, wy, camX, camY, vw, vh);
      ctx.fillStyle = "rgba(120, 200, 255, 0.55)";
      ctx.fillRect(wp.x - ww * 0.5 * wp.depth, wp.y - wh * wp.depth - 8, ww * wp.depth, wh * wp.depth);
      ctx.strokeStyle = "#5d4037";
      ctx.lineWidth = 1.5;
      ctx.strokeRect(wp.x - ww * 0.5 * wp.depth, wp.y - wh * wp.depth - 8, ww * wp.depth, wh * wp.depth);
    }
    windowAt(a.x + 90, a.y + 140, 22, 18);
    windowAt(a.x + 230, a.y + 140, 22, 18);

    // Roof
    var ridge = project(a.x + a.w * 0.5, a.y + 36, camX, camY, vw, vh);
    var rg = ctx.createLinearGradient(p0.x, p0.y - wallH, ridge.x, ridge.y - wallH - 40);
    rg.addColorStop(0, "#8d6e63");
    rg.addColorStop(0.5, "#6d4c41");
    rg.addColorStop(1, "#a1887f");
    ctx.fillStyle = rg;
    ctx.beginPath();
    ctx.moveTo(p0.x - 12, p0.y - wallH);
    ctx.lineTo(ridge.x, ridge.y - wallH - 42);
    ctx.lineTo(p1.x + 12, p1.y - wallH);
    ctx.closePath();
    ctx.fill();
    ctx.strokeStyle = "#3e2723";
    ctx.lineWidth = 2;
    ctx.stroke();

    // Chimney
    var ch = project(a.x + a.w * 0.72, a.y + 70, camX, camY, vw, vh);
    ctx.fillStyle = "#795548";
    ctx.fillRect(ch.x - 6, ch.y - wallH - 52, 12 * ch.depth, 28 * ch.depth);

    // Door
    var door = project(a.x + a.w * 0.48, a.y + a.h - 48, camX, camY, vw, vh);
    ctx.fillStyle = "#4e342e";
    ctx.fillRect(door.x - 12, door.y - 34, 24, 36);
    ctx.fillStyle = "#fbbf24";
    ctx.beginPath();
    ctx.arc(door.x + 6, door.y - 16, 2.5, 0, Math.PI * 2);
    ctx.fill();

    ctx.fillStyle = "rgba(255,255,255,0.92)";
    ctx.font = "bold 13px Segoe UI, system-ui, sans-serif";
    ctx.textAlign = "center";
    ctx.fillText("Ranch house", ridge.x, ridge.y - wallH - 56);
  }

  function drawTrack(ctx, camX, camY, vw, vh, t) {
    var a = AREAS[1];
    var base = areaCorners(a, camX, camY, vw, vh);
    drawGroundPoly(ctx, base, "rgba(55, 52, 48, 0.82)", "rgba(25,25,25,0.7)");

    // Dirt apron
    var apron = [
      project(a.x + 20, a.y + 20, camX, camY, vw, vh),
      project(a.x + a.w - 20, a.y + 20, camX, camY, vw, vh),
      project(a.x + a.w - 20, a.y + a.h - 20, camX, camY, vw, vh),
      project(a.x + 20, a.y + a.h - 20, camX, camY, vw, vh),
    ];
    drawGroundPoly(ctx, apron, "rgba(90, 70, 45, 0.35)", null);

    var cx = a.x + a.w * 0.5;
    var cy = a.y + a.h * 0.5;
    var steps = 36;

    // Outer lane
    ctx.beginPath();
    for (var i = 0; i <= steps; i++) {
      var ang = (i / steps) * Math.PI * 2;
      var wx = cx + Math.cos(ang) * (a.w * 0.42);
      var wy = cy + Math.sin(ang) * (a.h * 0.36);
      var p = project(wx, wy, camX, camY, vw, vh);
      if (i === 0) ctx.moveTo(p.x, p.y);
      else ctx.lineTo(p.x, p.y);
    }
    ctx.strokeStyle = "rgba(30,30,30,0.9)";
    ctx.lineWidth = 14;
    ctx.stroke();

    // Center dashed markings
    ctx.beginPath();
    for (var j = 0; j <= steps; j++) {
      var ang2 = (j / steps) * Math.PI * 2;
      var wx2 = cx + Math.cos(ang2) * (a.w * 0.38);
      var wy2 = cy + Math.sin(ang2) * (a.h * 0.32);
      var p2 = project(wx2, wy2, camX, camY, vw, vh);
      if (j === 0) ctx.moveTo(p2.x, p2.y);
      else ctx.lineTo(p2.x, p2.y);
    }
    ctx.strokeStyle = "#fbbf24";
    ctx.lineWidth = 3;
    ctx.setLineDash([12, 10]);
    ctx.stroke();
    ctx.setLineDash([]);

    // Inner lane
    ctx.beginPath();
    for (var k = 0; k <= steps; k++) {
      var ang3 = (k / steps) * Math.PI * 2;
      var wx3 = cx + Math.cos(ang3) * (a.w * 0.22);
      var wy3 = cy + Math.sin(ang3) * (a.h * 0.18);
      var p3 = project(wx3, wy3, camX, camY, vw, vh);
      if (k === 0) ctx.moveTo(p3.x, p3.y);
      else ctx.lineTo(p3.x, p3.y);
    }
    ctx.strokeStyle = "rgba(40,40,40,0.7)";
    ctx.lineWidth = 8;
    ctx.stroke();

    // Ramps (jumps)
    for (var ri = 0; ri < RAMPS.length; ri++) {
      var r = RAMPS[ri];
      var rp0 = project(r.x - r.w * 0.5, r.y, camX, camY, vw, vh);
      var rp1 = project(r.x + r.w * 0.5, r.y, camX, camY, vw, vh);
      var rp2 = project(r.x, r.y - r.h * 0.4, camX, camY, vw, vh);
      ctx.fillStyle = "#78716c";
      ctx.beginPath();
      ctx.moveTo(rp0.x, rp0.y);
      ctx.lineTo(rp2.x, rp2.y - 14 * rp2.depth);
      ctx.lineTo(rp1.x, rp1.y);
      ctx.closePath();
      ctx.fill();
      ctx.strokeStyle = "#f59e0b";
      ctx.lineWidth = 2;
      ctx.stroke();
      // Chevron
      ctx.fillStyle = "#fbbf24";
      ctx.font = "bold 10px Segoe UI, system-ui, sans-serif";
      ctx.textAlign = "center";
      ctx.fillText("▲", rp2.x, rp2.y - 18 * rp2.depth);
    }

    var label = project(cx, a.y + 28, camX, camY, vw, vh);
    ctx.fillStyle = "rgba(255,255,255,0.92)";
    ctx.font = "bold 13px Segoe UI, system-ui, sans-serif";
    ctx.textAlign = "center";
    ctx.fillText("Monster truck track", label.x, label.y - 8);
  }

  function drawPond(ctx, camX, camY, vw, vh, world, t) {
    var a = AREAS[2];
    var base = areaCorners(a, camX, camY, vw, vh);
    // Shore ring
    drawGroundPoly(ctx, base, "rgba(90, 140, 70, 0.45)", "rgba(50, 90, 40, 0.5)");

    // Water body inset
    var inset = {
      x: a.x + 24,
      y: a.y + 24,
      w: a.w - 48,
      h: a.h - 48,
    };
    var water = areaCorners(inset, camX, camY, vw, vh);
    var wg = ctx.createLinearGradient(water[0].x, water[0].y, water[2].x, water[2].y);
    wg.addColorStop(0, "rgba(12, 90, 120, 0.88)");
    wg.addColorStop(0.45, "rgba(14, 130, 160, 0.85)");
    wg.addColorStop(1, "rgba(8, 70, 100, 0.9)");
    drawGroundPoly(ctx, water, wg, "rgba(6, 50, 70, 0.8)");

    // Water shimmer bands
    for (var s = 0; s < 5; s++) {
      var sy = a.y + 50 + s * 40 + Math.sin(t * 1.8 + s) * 6;
      var sp0 = project(a.x + 60, sy, camX, camY, vw, vh);
      var sp1 = project(a.x + a.w - 60, sy + 8, camX, camY, vw, vh);
      ctx.strokeStyle = "rgba(180, 230, 255, " + (0.08 + 0.08 * Math.sin(t * 2 + s)) + ")";
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.moveTo(sp0.x, sp0.y);
      ctx.quadraticCurveTo(
        (sp0.x + sp1.x) * 0.5,
        sp0.y + Math.sin(t * 3 + s) * 6,
        sp1.x,
        sp1.y
      );
      ctx.stroke();
    }

    // Soft highlight
    var mid = project(a.x + a.w * 0.38, a.y + a.h * 0.38, camX, camY, vw, vh);
    ctx.beginPath();
    ctx.ellipse(mid.x, mid.y, 75 * mid.depth, 30 * mid.depth, -0.4, 0, Math.PI * 2);
    ctx.fillStyle = "rgba(160, 230, 255, 0.18)";
    ctx.fill();

    for (var i = 0; i < world.fish.length; i++) {
      var f = world.fish[i];
      var fp = project(f.x, f.y, camX, camY, vw, vh);
      var fs = (5 + f.size * 3) * fp.depth * (f.scare > 0 ? 1.15 : 1);
      ctx.fillStyle = f.scare > 0 ? "#fef08a" : "#fde68a";
      ctx.beginPath();
      ctx.ellipse(fp.x, fp.y, fs, fs * 0.45, f.phase, 0, Math.PI * 2);
      ctx.fill();
      // Tiny tail
      ctx.beginPath();
      ctx.moveTo(fp.x - Math.cos(f.phase) * fs, fp.y - Math.sin(f.phase) * fs * 0.4);
      ctx.lineTo(fp.x - Math.cos(f.phase) * fs * 1.6, fp.y);
      ctx.lineTo(fp.x - Math.cos(f.phase) * fs, fp.y + Math.sin(f.phase) * fs * 0.4);
      ctx.fillStyle = "#fcd34d";
      ctx.fill();
    }

    var label = project(a.x + a.w * 0.5, a.y + 22, camX, camY, vw, vh);
    ctx.fillStyle = "rgba(255,255,255,0.92)";
    ctx.font = "bold 13px Segoe UI, system-ui, sans-serif";
    ctx.textAlign = "center";
    ctx.fillText("Pond · fishies", label.x, label.y);
  }

  function drawToys(ctx, world, camX, camY, vw, vh) {
    for (var i = 0; i < world.toys.length; i++) {
      var toy = world.toys[i];
      var p = project(toy.x, toy.y, camX, camY, vw, vh);
      var s = 8 * p.depth;
      // Shadow
      ctx.fillStyle = "rgba(0,0,0,0.2)";
      ctx.beginPath();
      ctx.ellipse(p.x, p.y + 2, s * 0.9, s * 0.3, 0, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = "hsl(" + toy.hue + " 70% 55%)";
      if (toy.kind === 0) {
        ctx.beginPath();
        ctx.arc(p.x, p.y - s * 0.3, s, 0, Math.PI * 2);
        ctx.fill();
      } else if (toy.kind === 1) {
        ctx.fillRect(p.x - s, p.y - s * 1.4, s * 2, s * 1.6);
      } else {
        ctx.beginPath();
        ctx.moveTo(p.x, p.y - s * 2);
        ctx.lineTo(p.x + s, p.y);
        ctx.lineTo(p.x - s, p.y);
        ctx.closePath();
        ctx.fill();
      }
    }
  }

  function drawCybertruck(ctx, x, y, facing, depth, driving, z) {
    var s = 1.2 * depth;
    var lift = (z || 0) * 0.55 * depth;
    ctx.save();
    ctx.translate(x, y - lift);
    ctx.scale(facing < 0 ? -1 : 1, 1);

    // Ground shadow
    ctx.fillStyle = "rgba(0,0,0," + (0.28 - Math.min(0.18, (z || 0) * 0.004)) + ")";
    ctx.beginPath();
    ctx.ellipse(0, 10 * s + lift * 0.3, 32 * s, 8 * s, 0, 0, Math.PI * 2);
    ctx.fill();

    // Body — stainless wedge (Imagine still vibe)
    var bodyGrad = ctx.createLinearGradient(-30 * s, -18 * s, 36 * s, 10 * s);
    bodyGrad.addColorStop(0, driving ? "#f3f4f6" : "#d1d5db");
    bodyGrad.addColorStop(0.45, driving ? "#e5e7eb" : "#9ca3af");
    bodyGrad.addColorStop(1, driving ? "#cbd5e1" : "#6b7280");
    ctx.fillStyle = bodyGrad;
    ctx.beginPath();
    ctx.moveTo(-30 * s, -4 * s);
    ctx.lineTo(8 * s, -16 * s);
    ctx.lineTo(34 * s, -8 * s);
    ctx.lineTo(38 * s, 6 * s);
    ctx.lineTo(-32 * s, 8 * s);
    ctx.closePath();
    ctx.fill();
    ctx.strokeStyle = "#111827";
    ctx.lineWidth = 1.8;
    ctx.stroke();

    // Cabin glass
    ctx.fillStyle = "rgba(30, 41, 59, 0.92)";
    ctx.beginPath();
    ctx.moveTo(-6 * s, -5 * s);
    ctx.lineTo(12 * s, -20 * s);
    ctx.lineTo(24 * s, -9 * s);
    ctx.lineTo(-2 * s, -3 * s);
    ctx.closePath();
    ctx.fill();
    // Glass highlight
    ctx.strokeStyle = "rgba(148, 163, 184, 0.5)";
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(0, -8 * s);
    ctx.lineTo(14 * s, -16 * s);
    ctx.stroke();

    // Light bar
    ctx.fillStyle = driving ? "#fef08a" : "#94a3b8";
    ctx.fillRect(28 * s, -6 * s, 6 * s, 3 * s);

    // Wheels — chunky
    function wheel(wx, wy) {
      ctx.fillStyle = "#111";
      ctx.beginPath();
      ctx.arc(wx, wy, 8 * s, 0, Math.PI * 2);
      ctx.fill();
      ctx.strokeStyle = "#4b5563";
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.arc(wx, wy, 5 * s, 0, Math.PI * 2);
      ctx.stroke();
    }
    if ((z || 0) < 8) {
      wheel(-16 * s, 9 * s);
      wheel(18 * s, 8 * s);
    } else {
      // Airborne — wheels tucked hint
      wheel(-16 * s, 4 * s);
      wheel(18 * s, 3 * s);
    }

    // Exhaust / afterburner hint when dashing
    if (driving && depth) {
      /* optional trail drawn by caller via dashTrail */
    }

    ctx.restore();
  }

  function drawFroggy(ctx, frog, camX, camY, vw, vh) {
    var p = project(frog.x, frog.y, camX, camY, vw, vh);
    var s = 14 * p.depth;
    var lift = (frog.z || 0) * 0.55 * p.depth;

    if (frog.inTruck && frog.local) {
      drawCybertruck(ctx, p.x, p.y, frog.facing, p.depth, true, frog.z || 0);
      // Froggies on roof (Imagine still)
      ctx.fillStyle = frog.color;
      ctx.beginPath();
      ctx.arc(p.x, p.y - 24 * p.depth - lift, 7 * p.depth, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = frog.hat || "#facc15";
      ctx.beginPath();
      ctx.arc(p.x, p.y - 30 * p.depth - lift, 4 * p.depth, 0, Math.PI * 2);
      ctx.fill();
      if (frog.dashTrail > 0) {
        ctx.fillStyle = "rgba(251, 191, 36, 0.45)";
        ctx.beginPath();
        ctx.moveTo(p.x - frog.facing * 36 * p.depth, p.y);
        ctx.lineTo(p.x - frog.facing * 18 * p.depth, p.y - 8 * p.depth);
        ctx.lineTo(p.x - frog.facing * 18 * p.depth, p.y + 6 * p.depth);
        ctx.fill();
      }
      return p;
    }

    // Shadow
    ctx.fillStyle = "rgba(0,0,0," + (0.28 - Math.min(0.15, (frog.z || 0) * 0.003)) + ")";
    ctx.beginPath();
    ctx.ellipse(p.x, p.y + 4, s * 0.95, s * 0.35, 0, 0, Math.PI * 2);
    ctx.fill();

    var by = p.y - s * 0.4 - lift;

    // Legs hint
    ctx.strokeStyle = frog.accent;
    ctx.lineWidth = 2.5 * p.depth;
    ctx.beginPath();
    ctx.moveTo(p.x - 6 * p.depth, by + s * 0.5);
    ctx.lineTo(p.x - 10 * p.depth, p.y + 2 - lift * 0.2);
    ctx.moveTo(p.x + 6 * p.depth, by + s * 0.5);
    ctx.lineTo(p.x + 10 * p.depth, p.y + 2 - lift * 0.2);
    ctx.stroke();

    // Body
    var bg = ctx.createRadialGradient(p.x - 3, by - 4, 2, p.x, by, s);
    bg.addColorStop(0, frog.color);
    bg.addColorStop(1, frog.accent);
    ctx.fillStyle = bg;
    ctx.beginPath();
    ctx.arc(p.x, by, s, 0, Math.PI * 2);
    ctx.fill();
    ctx.strokeStyle = frog.accent;
    ctx.lineWidth = 2;
    ctx.stroke();

    // Hat (Imagine hats vibe)
    ctx.fillStyle = frog.hat || "#facc15";
    ctx.beginPath();
    ctx.ellipse(p.x, by - s * 0.85, s * 0.75, s * 0.28, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillRect(p.x - s * 0.35, by - s * 1.35, s * 0.7, s * 0.5);

    // Eyes
    ctx.fillStyle = "#fff";
    ctx.beginPath();
    ctx.arc(p.x - 4 * p.depth * frog.facing, by - s * 0.25, 3.4 * p.depth, 0, Math.PI * 2);
    ctx.arc(p.x + 5 * p.depth * frog.facing, by - s * 0.25, 3.4 * p.depth, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = "#111";
    ctx.beginPath();
    ctx.arc(p.x - 3.2 * p.depth * frog.facing, by - s * 0.25, 1.5 * p.depth, 0, Math.PI * 2);
    ctx.arc(p.x + 5.6 * p.depth * frog.facing, by - s * 0.25, 1.5 * p.depth, 0, Math.PI * 2);
    ctx.fill();

    if (frog.dashTrail > 0) {
      ctx.globalAlpha = 0.45;
      ctx.fillStyle = frog.color;
      ctx.beginPath();
      ctx.arc(p.x - frog.facing * 16 * p.depth, by + 4, s * 0.7, 0, Math.PI * 2);
      ctx.fill();
      ctx.globalAlpha = 1;
    }

    if (!frog.human) {
      ctx.fillStyle = "rgba(0,0,0,0.55)";
      ctx.font = "bold 9px Segoe UI, system-ui, sans-serif";
      ctx.textAlign = "center";
      ctx.fillText("AI", p.x, p.y + s + 10);
    }
    if (frog.local) {
      ctx.strokeStyle = "rgba(255,255,255,0.75)";
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.arc(p.x, by, s + 5, 0, Math.PI * 2);
      ctx.stroke();
    }
    if (frog.invuln > 0) {
      ctx.strokeStyle = "rgba(251, 191, 36, 0.8)";
      ctx.lineWidth = 2.5;
      ctx.beginPath();
      ctx.arc(p.x, by, s + 8, 0, Math.PI * 2);
      ctx.stroke();
    }
    return p;
  }

  function drawHotspot(ctx, h, camX, camY, vw, vh, near) {
    var p = project(h.x, h.y, camX, camY, vw, vh);
    var pulse = near ? 1 + Math.sin(Date.now() / 200) * 0.08 : 1;
    ctx.beginPath();
    ctx.arc(p.x, p.y, (near ? 20 : 12) * p.depth * pulse, 0, Math.PI * 2);
    ctx.fillStyle = near ? "rgba(251, 191, 36, 0.5)" : "rgba(255,255,255,0.14)";
    ctx.fill();
    ctx.strokeStyle = near ? "#fbbf24" : "rgba(255,255,255,0.35)";
    ctx.lineWidth = near ? 2.5 : 1;
    ctx.stroke();
    if (near) {
      ctx.fillStyle = "#fef3c7";
      ctx.font = "bold 12px Segoe UI, system-ui, sans-serif";
      ctx.textAlign = "center";
      ctx.fillText(h.label + " · E", p.x, p.y - 24 * p.depth);
    } else {
      ctx.fillStyle = "rgba(255,255,255,0.65)";
      ctx.font = "bold 11px Segoe UI, system-ui, sans-serif";
      ctx.textAlign = "center";
      ctx.fillText(h.label, p.x, p.y - 18 * p.depth);
    }
  }

  function drawParkedTruck(ctx, camX, camY, vw, vh) {
    var p = project(700, 620, camX, camY, vw, vh);
    drawCybertruck(ctx, p.x, p.y, 1, p.depth, false, 0);
  }

  function drawFx(ctx, world, camX, camY, vw, vh) {
    var i;
    for (i = 0; i < world.dust.length; i++) {
      var d = world.dust[i];
      var dp = project(d.x, d.y, camX, camY, vw, vh);
      ctx.fillStyle = "rgba(180, 160, 120, " + clamp(d.life * 1.5, 0, 0.5) + ")";
      ctx.beginPath();
      ctx.arc(dp.x, dp.y, d.r * dp.depth, 0, Math.PI * 2);
      ctx.fill();
    }
    for (i = 0; i < world.splashes.length; i++) {
      var s = world.splashes[i];
      var sp = project(s.x, s.y, camX, camY, vw, vh);
      ctx.fillStyle = "rgba(180, 230, 255, " + clamp(s.life * 1.8, 0, 0.7) + ")";
      ctx.beginPath();
      ctx.arc(sp.x, sp.y - (0.5 - s.life) * 30, s.r * sp.depth, 0, Math.PI * 2);
      ctx.fill();
    }
    for (i = 0; i < world.sparks.length; i++) {
      var k = world.sparks[i];
      var kp = project(k.x, k.y, camX, camY, vw, vh);
      ctx.fillStyle = "hsla(" + k.hue + ", 90%, 60%, " + clamp(k.life * 2, 0, 1) + ")";
      ctx.fillRect(kp.x, kp.y - (0.4 - k.life) * 20, 3, 3);
    }
  }

  function render(ctx, world, frogs, camX, camY, vw, vh, t, nearHot) {
    drawSky(ctx, vw, vh, t, camX, camY);

    // Ground plane
    var g0 = project(0, 0, camX, camY, vw, vh);
    var g1 = project(MAP_W, 0, camX, camY, vw, vh);
    var g2 = project(MAP_W, MAP_H, camX, camY, vw, vh);
    var g3 = project(0, MAP_H, camX, camY, vw, vh);
    drawGroundPoly(ctx, [g0, g1, g2, g3], "rgba(90, 150, 55, 0.6)", null);

    // Soft grass patches
    for (var gi = 0; gi < 12; gi++) {
      var gx = 80 + (gi * 97) % (MAP_W - 100);
      var gy = 80 + (gi * 131) % (MAP_H - 100);
      if (areaAt(gx, gy)) continue;
      var gp = project(gx, gy, camX, camY, vw, vh);
      ctx.fillStyle = "rgba(60, 120, 40, 0.25)";
      ctx.beginPath();
      ctx.ellipse(gp.x, gp.y, 28 * gp.depth, 12 * gp.depth, 0, 0, Math.PI * 2);
      ctx.fill();
    }

    drawPond(ctx, camX, camY, vw, vh, world, t);
    drawRanchHouse(ctx, camX, camY, vw, vh);
    drawTrack(ctx, camX, camY, vw, vh, t);
    drawToys(ctx, world, camX, camY, vw, vh);
    drawFx(ctx, world, camX, camY, vw, vh);

    var localInTruck = frogs.some(function (f) {
      return f.local && f.inTruck;
    });
    if (!localInTruck) drawParkedTruck(ctx, camX, camY, vw, vh);

    for (var hi = 0; hi < world.hotspots.length; hi++) {
      var h = world.hotspots[hi];
      if (h.id === "truck" && localInTruck) continue;
      drawHotspot(ctx, h, camX, camY, vw, vh, nearHot && nearHot.id === h.id);
    }

    // Y-sort froggies
    var sorted = frogs.slice().sort(function (a, b) {
      return a.y - b.y;
    });
    for (var fi = 0; fi < sorted.length; fi++) {
      drawFroggy(ctx, sorted[fi], camX, camY, vw, vh);
    }
  }

  global.FroggiesWorld = {
    MAP_W: MAP_W,
    MAP_H: MAP_H,
    AREAS: AREAS,
    RAMPS: RAMPS,
    FROG_COLORS: FROG_COLORS,
    createWorld: createWorld,
    seedDecor: seedDecor,
    makeFrogEntity: makeFrogEntity,
    areaAt: areaAt,
    areaNameAt: areaNameAt,
    onTrack: onTrack,
    rampAt: rampAt,
    nearestHotspot: nearestHotspot,
    updateFish: updateFish,
    updateFx: updateFx,
    tickDrive: tickDrive,
    scareFishies: scareFishies,
    spawnDust: spawnDust,
    spawnSplash: spawnSplash,
    spawnSparks: spawnSparks,
    moveEntity: moveEntity,
    tickHubAI: tickHubAI,
    project: project,
    render: render,
  };
})(typeof window !== "undefined" ? window : globalThis);
