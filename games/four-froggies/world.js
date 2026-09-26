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

  var FROG_COLORS = {
    james: { body: "#4ade80", accent: "#166534" },
    jimmy: { body: "#fb923c", accent: "#9a3412" },
    bubbles: { body: "#60a5fa", accent: "#1e3a8a" },
    rexy: { body: "#c084fc", accent: "#6b21a8" },
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

  /** Fixed-angle 2.5D: world (x,y) → screen with depth foreshortening. */
  function project(wx, wy, camX, camY, vw, vh) {
    var dx = wx - camX;
    var dy = wy - camY;
    // ~30° isometric-ish: x skew + y as depth
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
      // Hotspots for story / interact (Ben-named only)
      hotspots: [
        { id: "phone", label: "Phone", x: 200, y: 360, r: 48, tip: "Call Purple Bear" },
        { id: "sps", label: "SPS", x: 280, y: 400, r: 44, tip: "Solar Positioning System" },
        { id: "truck", label: "Cybertruck", x: 700, y: 620, r: 56, tip: "Drive the track" },
        { id: "fishies", label: "Fishies", x: 760, y: 180, r: 50, tip: "Pond fishies" },
      ],
      fish: [],
      toys: [], // unnamed prop dots — density only, no invented item names
      dust: [],
    };
  }

  function seedDecor(world) {
    world.fish = [];
    for (var i = 0; i < 8; i++) {
      world.fish.push({
        x: 560 + Math.random() * 440,
        y: 100 + Math.random() * 200,
        phase: Math.random() * Math.PI * 2,
        speed: 0.6 + Math.random() * 0.8,
      });
    }
    world.toys = [];
    // Prop clutter near ranch house — no names
    var spots = [
      [120, 480], [160, 520], [340, 500], [380, 450],
      [100, 420], [360, 540], [420, 380], [250, 550],
    ];
    for (var t = 0; t < spots.length; t++) {
      world.toys.push({
        x: spots[t][0],
        y: spots[t][1],
        kind: t % 3, // 0 ball-ish, 1 block, 2 cone — visual only
        hue: (t * 47) % 360,
      });
    }
  }

  function makeFrogEntity(id, human, local, laneIndex) {
    var colors = FROG_COLORS[id] || FROG_COLORS.james;
    // Start near ranch house, staggered
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
      color: colors.body,
      accent: colors.accent,
      cd: 0,
      invuln: 0,
      alive: true,
      aiTimer: 0,
      targetX: ox,
      targetY: oy,
      steerX: 0,
      steerY: 0,
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

  function updateFish(world, dt) {
    var pond = AREAS[2];
    for (var i = 0; i < world.fish.length; i++) {
      var f = world.fish[i];
      f.phase += dt * f.speed;
      f.x += Math.cos(f.phase) * 18 * dt;
      f.y += Math.sin(f.phase * 0.7) * 12 * dt;
      f.x = clamp(f.x, pond.x + 20, pond.x + pond.w - 20);
      f.y = clamp(f.y, pond.y + 20, pond.y + pond.h - 20);
    }
  }

  function moveEntity(ent, dt, speed) {
    var sp = speed || (ent.inTruck ? 220 : 140);
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
      ent.vx *= 0.8;
      ent.vy *= 0.8;
    }
    ent.x = clamp(ent.x, 40, MAP_W - 40);
    ent.y = clamp(ent.y, 40, MAP_H - 40);
    if (ent.cd > 0) ent.cd -= dt;
    if (ent.invuln > 0) ent.invuln -= dt;
  }

  /** Simple AI: wander toward area centers + follow local human loosely. */
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
      // Match truck mode of local player loosely
      if (localFrog) f.inTruck = localFrog.inTruck;
      moveEntity(f, dt);
    }
  }

  function drawSky(ctx, w, h, t) {
    var g = ctx.createLinearGradient(0, 0, 0, h);
    g.addColorStop(0, "#1e3a5f");
    g.addColorStop(0.45, "#3d7ea6");
    g.addColorStop(0.7, "#7cb342");
    g.addColorStop(1, "#33691e");
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, w, h);
    // Soft sun
    ctx.beginPath();
    ctx.arc(w * 0.82, h * 0.12, 36, 0, Math.PI * 2);
    ctx.fillStyle = "rgba(255,236,160,0.85)";
    ctx.fill();
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
    drawGroundPoly(ctx, base, "rgba(120, 72, 40, 0.55)", "rgba(60,30,10,0.5)");
    // Building block (extruded feel)
    var p0 = project(a.x + 40, a.y + 60, camX, camY, vw, vh);
    var p1 = project(a.x + a.w - 40, a.y + 60, camX, camY, vw, vh);
    var p2 = project(a.x + a.w - 40, a.y + a.h - 40, camX, camY, vw, vh);
    var p3 = project(a.x + 40, a.y + a.h - 40, camX, camY, vw, vh);
    var wallH = 48 * ((p0.depth + p2.depth) * 0.5);
    ctx.fillStyle = "#c4a574";
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
    // Roof
    var ridge = project(a.x + a.w * 0.5, a.y + 40, camX, camY, vw, vh);
    ctx.fillStyle = "#8d6e63";
    ctx.beginPath();
    ctx.moveTo(p0.x - 8, p0.y - wallH);
    ctx.lineTo(ridge.x, ridge.y - wallH - 36);
    ctx.lineTo(p1.x + 8, p1.y - wallH);
    ctx.closePath();
    ctx.fill();
    // Door
    var door = project(a.x + a.w * 0.45, a.y + a.h - 50, camX, camY, vw, vh);
    ctx.fillStyle = "#5d4037";
    ctx.fillRect(door.x - 10, door.y - 28, 20, 32);
    // Label
    ctx.fillStyle = "rgba(255,255,255,0.9)";
    ctx.font = "bold 13px Segoe UI, system-ui, sans-serif";
    ctx.textAlign = "center";
    ctx.fillText("Ranch house", ridge.x, ridge.y - wallH - 48);
  }

  function drawTrack(ctx, camX, camY, vw, vh) {
    var a = AREAS[1];
    var base = areaCorners(a, camX, camY, vw, vh);
    drawGroundPoly(ctx, base, "rgba(66, 66, 66, 0.7)", "rgba(30,30,30,0.6)");
    // Oval lane
    var cx = a.x + a.w * 0.5;
    var cy = a.y + a.h * 0.5;
    var steps = 24;
    ctx.beginPath();
    for (var i = 0; i <= steps; i++) {
      var ang = (i / steps) * Math.PI * 2;
      var wx = cx + Math.cos(ang) * (a.w * 0.38);
      var wy = cy + Math.sin(ang) * (a.h * 0.32);
      var p = project(wx, wy, camX, camY, vw, vh);
      if (i === 0) ctx.moveTo(p.x, p.y);
      else ctx.lineTo(p.x, p.y);
    }
    ctx.strokeStyle = "#fbbf24";
    ctx.lineWidth = 3;
    ctx.setLineDash([10, 8]);
    ctx.stroke();
    ctx.setLineDash([]);
    var label = project(cx, a.y + 30, camX, camY, vw, vh);
    ctx.fillStyle = "rgba(255,255,255,0.9)";
    ctx.font = "bold 13px Segoe UI, system-ui, sans-serif";
    ctx.textAlign = "center";
    ctx.fillText("Monster truck track", label.x, label.y - 8);
  }

  function drawPond(ctx, camX, camY, vw, vh, world, t) {
    var a = AREAS[2];
    var base = areaCorners(a, camX, camY, vw, vh);
    drawGroundPoly(ctx, base, "rgba(14, 116, 144, 0.75)", "rgba(8, 60, 80, 0.7)");
    // Soft highlight
    var mid = project(a.x + a.w * 0.4, a.y + a.h * 0.4, camX, camY, vw, vh);
    ctx.beginPath();
    ctx.ellipse(mid.x, mid.y, 70 * mid.depth, 28 * mid.depth, -0.4, 0, Math.PI * 2);
    ctx.fillStyle = "rgba(125, 211, 252, 0.25)";
    ctx.fill();
    for (var i = 0; i < world.fish.length; i++) {
      var f = world.fish[i];
      var fp = project(f.x, f.y, camX, camY, vw, vh);
      ctx.fillStyle = "#fde68a";
      ctx.beginPath();
      ctx.ellipse(fp.x, fp.y, 6 * fp.depth, 3 * fp.depth, f.phase, 0, Math.PI * 2);
      ctx.fill();
    }
    var label = project(a.x + a.w * 0.5, a.y + 24, camX, camY, vw, vh);
    ctx.fillStyle = "rgba(255,255,255,0.9)";
    ctx.font = "bold 13px Segoe UI, system-ui, sans-serif";
    ctx.textAlign = "center";
    ctx.fillText("Pond · fishies", label.x, label.y);
  }

  function drawToys(ctx, world, camX, camY, vw, vh) {
    for (var i = 0; i < world.toys.length; i++) {
      var toy = world.toys[i];
      var p = project(toy.x, toy.y, camX, camY, vw, vh);
      var s = 8 * p.depth;
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

  function drawCybertruck(ctx, x, y, facing, depth, driving) {
    var s = 1.15 * depth;
    ctx.save();
    ctx.translate(x, y);
    ctx.scale(facing < 0 ? -1 : 1, 1);
    // Body
    ctx.fillStyle = driving ? "#e5e7eb" : "#9ca3af";
    ctx.beginPath();
    ctx.moveTo(-28 * s, -6 * s);
    ctx.lineTo(32 * s, -10 * s);
    ctx.lineTo(36 * s, 6 * s);
    ctx.lineTo(-30 * s, 8 * s);
    ctx.closePath();
    ctx.fill();
    ctx.strokeStyle = "#111";
    ctx.lineWidth = 1.5;
    ctx.stroke();
    // Cabin wedge
    ctx.fillStyle = "#1f2937";
    ctx.beginPath();
    ctx.moveTo(-8 * s, -6 * s);
    ctx.lineTo(14 * s, -18 * s);
    ctx.lineTo(22 * s, -8 * s);
    ctx.lineTo(-4 * s, -4 * s);
    ctx.closePath();
    ctx.fill();
    // Wheels
    ctx.fillStyle = "#111";
    ctx.beginPath();
    ctx.arc(-16 * s, 8 * s, 7 * s, 0, Math.PI * 2);
    ctx.arc(18 * s, 7 * s, 7 * s, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();
  }

  function drawFroggy(ctx, frog, camX, camY, vw, vh) {
    var p = project(frog.x, frog.y, camX, camY, vw, vh);
    var s = 14 * p.depth;
    if (frog.inTruck && frog.local) {
      drawCybertruck(ctx, p.x, p.y, frog.facing, p.depth, true);
      // Tiny frog on roof
      ctx.fillStyle = frog.color;
      ctx.beginPath();
      ctx.arc(p.x, p.y - 22 * p.depth, 6 * p.depth, 0, Math.PI * 2);
      ctx.fill();
      return p;
    }
    // Shadow
    ctx.fillStyle = "rgba(0,0,0,0.25)";
    ctx.beginPath();
    ctx.ellipse(p.x, p.y + 4, s * 0.9, s * 0.35, 0, 0, Math.PI * 2);
    ctx.fill();
    // Body
    ctx.fillStyle = frog.color;
    ctx.beginPath();
    ctx.arc(p.x, p.y - s * 0.4, s, 0, Math.PI * 2);
    ctx.fill();
    ctx.strokeStyle = frog.accent;
    ctx.lineWidth = 2;
    ctx.stroke();
    // Eyes
    ctx.fillStyle = "#fff";
    ctx.beginPath();
    ctx.arc(p.x - 4 * p.depth * frog.facing, p.y - s * 0.7, 3.2 * p.depth, 0, Math.PI * 2);
    ctx.arc(p.x + 5 * p.depth * frog.facing, p.y - s * 0.7, 3.2 * p.depth, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = "#111";
    ctx.beginPath();
    ctx.arc(p.x - 3.5 * p.depth * frog.facing, p.y - s * 0.7, 1.4 * p.depth, 0, Math.PI * 2);
    ctx.arc(p.x + 5.5 * p.depth * frog.facing, p.y - s * 0.7, 1.4 * p.depth, 0, Math.PI * 2);
    ctx.fill();
    // Bot badge
    if (!frog.human) {
      ctx.fillStyle = "rgba(0,0,0,0.55)";
      ctx.font = "bold 9px Segoe UI, system-ui, sans-serif";
      ctx.textAlign = "center";
      ctx.fillText("AI", p.x, p.y + s + 10);
    }
    if (frog.local) {
      ctx.strokeStyle = "rgba(255,255,255,0.7)";
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.arc(p.x, p.y - s * 0.4, s + 4, 0, Math.PI * 2);
      ctx.stroke();
    }
    return p;
  }

  function drawHotspot(ctx, h, camX, camY, vw, vh, near) {
    var p = project(h.x, h.y, camX, camY, vw, vh);
    ctx.beginPath();
    ctx.arc(p.x, p.y, (near ? 18 : 12) * p.depth, 0, Math.PI * 2);
    ctx.fillStyle = near ? "rgba(251, 191, 36, 0.45)" : "rgba(255,255,255,0.15)";
    ctx.fill();
    ctx.strokeStyle = near ? "#fbbf24" : "rgba(255,255,255,0.35)";
    ctx.lineWidth = near ? 2.5 : 1;
    ctx.stroke();
    ctx.fillStyle = near ? "#fef3c7" : "rgba(255,255,255,0.7)";
    ctx.font = "bold 11px Segoe UI, system-ui, sans-serif";
    ctx.textAlign = "center";
    ctx.fillText(h.label, p.x, p.y - 20 * p.depth);
  }

  function drawParkedTruck(ctx, camX, camY, vw, vh) {
    var p = project(700, 620, camX, camY, vw, vh);
    drawCybertruck(ctx, p.x, p.y, 1, p.depth, false);
  }

  function render(ctx, world, frogs, camX, camY, vw, vh, t, nearHot) {
    drawSky(ctx, vw, vh, t);
    // Ground plane tint
    var g0 = project(0, 0, camX, camY, vw, vh);
    var g1 = project(MAP_W, 0, camX, camY, vw, vh);
    var g2 = project(MAP_W, MAP_H, camX, camY, vw, vh);
    var g3 = project(0, MAP_H, camX, camY, vw, vh);
    drawGroundPoly(ctx, [g0, g1, g2, g3], "rgba(85, 139, 47, 0.55)", null);

    drawPond(ctx, camX, camY, vw, vh, world, t);
    drawRanchHouse(ctx, camX, camY, vw, vh);
    drawTrack(ctx, camX, camY, vw, vh);
    drawToys(ctx, world, camX, camY, vw, vh);

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
    FROG_COLORS: FROG_COLORS,
    createWorld: createWorld,
    seedDecor: seedDecor,
    makeFrogEntity: makeFrogEntity,
    areaAt: areaAt,
    areaNameAt: areaNameAt,
    nearestHotspot: nearestHotspot,
    updateFish: updateFish,
    moveEntity: moveEntity,
    tickHubAI: tickHubAI,
    project: project,
    render: render,
  };
})(typeof window !== "undefined" ? window : globalThis);
