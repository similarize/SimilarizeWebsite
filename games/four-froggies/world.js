/* Four Froggies — 2.5D ranch hub (fixed-angle / y-sorted layers).
   ranchfeel2 + parity1: Cybertruck water; varied track; crisper silhouettes; Starship approach uses shared canon gold guide.
   ~10× map: real roam between ranch house / track / pond / Starship.
   James ranch house: big house, backyard (animals), huge garage (toys + 10/100-story mechs);
   1000-story mech sits out back (won't fit). Four Cybertrucks + shared pile-in.
   Pond: big fish + whales. Starship pad connected → space episode.
   Ben-named only. No invented cast/zone/toy names. */
(function (global) {
  "use strict";

  var MAP_W = 4200;
  var MAP_H = 3150;

  var AREAS = [
    { id: "house", name: "Ranch house", x: 60, y: 1320, w: 1180, h: 1180, color: "#8b5a2b" },
    { id: "track", name: "Monster truck track", x: 1680, y: 1580, w: 2200, h: 1380, color: "#57534e" },
    { id: "pond", name: "Pond", x: 2320, y: 80, w: 1680, h: 1180, color: "#0e7490" },
  ];

  /* Varied circuit (not oval): mountain highs, valley lows, squiggles, branch loops.
     Points: [x, y, elev] — elev >0 high berm / mountain, <0 valley dip (visual only). */
  var TRACK_MAIN = [
    [1780, 2220, 0],
    [1850, 2080, 0.15],
    [1940, 1920, 0.45],
    [2060, 1780, 0.85],
    [2180, 1700, 1.15],
    [2320, 1660, 1.25],
    [2480, 1700, 1.05],
    [2600, 1820, 0.55],
    [2680, 1960, 0.25],
    [2780, 2040, 0.2],
    [2920, 1980, 0.35],
    [3080, 1860, 0.7],
    [3220, 1760, 1.0],
    [3380, 1800, 0.95],
    [3520, 1940, 0.55],
    [3620, 2120, 0.2],
    [3660, 2300, 0],
    [3600, 2480, -0.15],
    [3480, 2620, -0.35],
    [3300, 2740, -0.4],
    [3100, 2820, -0.3],
    [2880, 2840, -0.15],
    [2680, 2760, 0.05],
    [2500, 2620, 0.25],
    [2340, 2520, 0.45],
    [2180, 2480, 0.35],
    [2020, 2420, 0.2],
    [1900, 2340, 0.1],
    [1820, 2280, 0],
  ];
  /* Inner squiggle branch */
  var TRACK_BRANCH_A = [
    [2200, 2100, 0.3],
    [2360, 2180, 0.15],
    [2540, 2280, 0],
    [2720, 2360, -0.1],
    [2900, 2420, 0],
    [3080, 2360, 0.2],
    [3200, 2200, 0.45],
    [3180, 2040, 0.55],
    [3020, 1960, 0.4],
    [2820, 2000, 0.25],
    [2620, 2080, 0.2],
    [2420, 2120, 0.25],
    [2260, 2100, 0.3],
  ];
  /* South figure / loop-de-loop cue */
  var TRACK_BRANCH_B = [
    [2400, 2400, 0.1],
    [2560, 2520, -0.1],
    [2760, 2580, -0.2],
    [2960, 2520, -0.1],
    [3080, 2380, 0.15],
    [3000, 2260, 0.35],
    [2800, 2220, 0.4],
    [2600, 2280, 0.25],
    [2460, 2360, 0.15],
  ];
  /* Mountain mound centers inside track apron */
  var TRACK_MOUNDS = [
    { x: 2260, y: 1740, r: 160, h: 1.2 },
    { x: 3280, y: 1820, r: 140, h: 1.05 },
    { x: 2460, y: 2480, r: 110, h: 0.55 },
    { x: 3180, y: 2680, r: 130, h: -0.35 },
  ];
  /* Ramps sit on highs, valley lips, and branch junctions */
  var RAMPS = [
    { x: 2060, y: 1780, w: 72, h: 36, boost: 1.45 },
    { x: 2320, y: 1660, w: 76, h: 38, boost: 1.6 },
    { x: 2480, y: 1700, w: 70, h: 34, boost: 1.4 },
    { x: 3080, y: 1860, w: 72, h: 36, boost: 1.48 },
    { x: 3220, y: 1760, w: 74, h: 36, boost: 1.55 },
    { x: 3380, y: 1800, w: 70, h: 34, boost: 1.42 },
    { x: 3480, y: 2620, w: 72, h: 34, boost: 1.35 },
    { x: 3300, y: 2740, w: 74, h: 36, boost: 1.5 },
    { x: 2340, y: 2520, w: 68, h: 32, boost: 1.32 },
    { x: 2680, y: 1960, w: 66, h: 30, boost: 1.28 },
    { x: 2540, y: 2280, w: 70, h: 34, boost: 1.38 },
    { x: 2900, y: 2420, w: 68, h: 32, boost: 1.36 },
    { x: 3200, y: 2200, w: 72, h: 34, boost: 1.44 },
    { x: 2760, y: 2580, w: 70, h: 34, boost: 1.4 },
    { x: 3000, y: 2260, w: 68, h: 32, boost: 1.33 },
    { x: 1940, y: 1920, w: 64, h: 30, boost: 1.25 },
    { x: 3620, y: 2120, w: 70, h: 34, boost: 1.46 },
    { x: 2180, y: 2480, w: 66, h: 30, boost: 1.3 },
    { x: 3100, y: 2820, w: 72, h: 34, boost: 1.52 },
    { x: 2800, y: 2220, w: 68, h: 32, boost: 1.37 },
  ];

  var FROG_COLORS = {
    james: { body: "#4ade80", accent: "#166534", hat: "#facc15" },
    jimmy: { body: "#fb923c", accent: "#9a3412", hat: "#ef4444" },
    bubbles: { body: "#60a5fa", accent: "#1e3a8a", hat: "#38bdf8" },
    rexy: { body: "#c084fc", accent: "#6b21a8", hat: "#e879f9" },
  };

  var TRUCK_SPOTS = [
    { id: "james", x: 1880, y: 1720 },
    { id: "jimmy", x: 2080, y: 1720 },
    { id: "bubbles", x: 2280, y: 1720 },
    { id: "rexy", x: 2480, y: 1720 },
    { id: "shared", x: 2180, y: 1880 },
  ];

  /* Starship pad + path anchor (connected approach from ranch grounds) */
  var STARSHIP = { x: 360, y: 320, padR: 110 };

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

  function inPond(x, y) {
    var a = AREAS[2];
    var pad = 40;
    return x >= a.x + pad && x <= a.x + a.w - pad &&
           y >= a.y + pad && y <= a.y + a.h - pad;
  }

  function rampAt(x, y) {
    for (var i = 0; i < RAMPS.length; i++) {
      var r = RAMPS[i];
      if (x >= r.x - r.w * 0.5 && x <= r.x + r.w * 0.5 &&
          y >= r.y - r.h * 0.5 && y <= r.y + r.h * 0.5) return r;
    }
    return null;
  }

  function project(wx, wy, camX, camY, vw, vh) {
    var dx = wx - camX;
    var dy = wy - camY;
    var sx = vw * 0.5 + dx * 0.98 - dy * 0.52;
    var sy = vh * 0.46 + dx * 0.30 + dy * 0.58;
    var depth = clamp(0.58 + (wy - camY) / MAP_H * 0.55 + dy * 0.00015, 0.42, 1.38);
    return { x: sx, y: sy, depth: depth, sortY: wy, scale: depth };
  }

  function defaultHotspots() {
    var C = global.FroggiesCanon;
    if (C && C.HOTSPOTS) {
      return C.HOTSPOTS.map(function (h) {
        return {
          id: h.id, label: h.label, x: h.x, y: h.y, r: h.r, tip: h.tip,
          kind: h.kind || null, frogId: h.frogId || null, mode: h.mode || null,
        };
      });
    }
    return [
      { id: "phone", label: "Phone", x: 380, y: 1880, r: 52, tip: "Call Purple Bear" },
      { id: "sps", label: "SPS", x: 520, y: 1940, r: 48, tip: "Solar Positioning System" },
      { id: "truck-james", label: "Cybertruck · James", x: 1880, y: 1720, r: 54, tip: "James Cybertruck · solo drive", kind: "truck", frogId: "james", mode: "solo" },
      { id: "truck-jimmy", label: "Cybertruck · Jimmy", x: 2080, y: 1720, r: 54, tip: "Jimmy Cybertruck · solo drive", kind: "truck", frogId: "jimmy", mode: "solo" },
      { id: "truck-bubbles", label: "Cybertruck · Bubbles", x: 2280, y: 1720, r: 54, tip: "Bubbles Cybertruck · solo drive", kind: "truck", frogId: "bubbles", mode: "solo" },
      { id: "truck-rexy", label: "Cybertruck · Rexy", x: 2480, y: 1720, r: 54, tip: "Rexy Cybertruck · solo drive", kind: "truck", frogId: "rexy", mode: "solo" },
      { id: "truck-shared", label: "Cybertruck · all aboard", x: 2180, y: 1880, r: 62, tip: "All four pile into one Cybertruck", kind: "truck", frogId: null, mode: "shared" },
      { id: "fishies", label: "Fishies", x: 3160, y: 620, r: 70, tip: "Splash the pond" },
      { id: "starship", label: "Starship", x: STARSHIP.x, y: STARSHIP.y, r: 72, tip: "Starship · Spotty · space episode" },
    ];
  }

  function createWorld() {
    return {
      mapW: MAP_W,
      mapH: MAP_H,
      areas: AREAS,
      ramps: RAMPS,
      trucks: TRUCK_SPOTS.map(function (t) {
        return { id: t.id, x: t.x, y: t.y, occupied: false };
      }),
      hotspots: defaultHotspots(),
      fish: [],
      whales: [],
      animals: [],
      toys: [],
      dust: [],
      splashes: [],
      sparks: [],
      scrap: 0,
      stuntCombo: 0,
      airTime: 0,
      lastJumpT: 0,
      sharedDriverId: null,
    };
  }

  function seedDecor(world) {
    var pond = AREAS[2];
    world.fish = [];
    for (var i = 0; i < 28; i++) {
      world.fish.push({
        x: pond.x + 40 + Math.random() * (pond.w - 80),
        y: pond.y + 40 + Math.random() * (pond.h - 80),
        phase: Math.random() * Math.PI * 2,
        speed: 0.45 + Math.random() * 0.9,
        scare: 0,
        size: 2.2 + Math.random() * 2.8,
        kind: "fish",
      });
    }
    world.whales = [];
    for (var w = 0; w < 5; w++) {
      world.whales.push({
        x: pond.x + 120 + Math.random() * (pond.w - 240),
        y: pond.y + 100 + Math.random() * (pond.h - 200),
        phase: Math.random() * Math.PI * 2,
        speed: 0.25 + Math.random() * 0.35,
        scare: 0,
        size: 9 + Math.random() * 6,
        kind: "whale",
      });
    }
    /* Big backyard animals — anonymous density only */
    world.animals = [];
    var yardX0 = 120, yardY0 = 2100, yardW = 520, yardH = 320;
    for (var a = 0; a < 36; a++) {
      world.animals.push({
        x: yardX0 + Math.random() * yardW,
        y: yardY0 + Math.random() * yardH,
        phase: Math.random() * Math.PI * 2,
        speed: 0.4 + Math.random() * 0.8,
        kind: a % 4,
        tone: a % 3 === 0 ? "#c4a574" : a % 3 === 1 ? "#8b6914" : "#d6d3d1",
        size: 0.7 + Math.random() * 0.7,
      });
    }
    world.toys = [];
    /* Garage toy density (James toys — anonymous props) */
    var gx0 = 720, gy0 = 1480;
    for (var t = 0; t < 48; t++) {
      world.toys.push({
        x: gx0 + (t % 8) * 42 + (t % 3) * 6,
        y: gy0 + Math.floor(t / 8) * 38 + (t % 5) * 4,
        kind: t % 4,
        hue: (t * 47) % 360,
        inGarage: true,
      });
    }
    /* Yard / porch scatter */
    var spots = [
      [200, 1950], [260, 2000], [340, 2040], [400, 1980],
      [180, 2200], [300, 2280], [450, 2320], [520, 2180],
      [600, 2050], [160, 1850],
    ];
    for (var s = 0; s < spots.length; s++) {
      world.toys.push({
        x: spots[s][0], y: spots[s][1], kind: s % 3, hue: (s * 61) % 360, inGarage: false,
      });
    }
    world.dust = [];
    world.splashes = [];
    world.sparks = [];
    world.scrap = 0;
    world.stuntCombo = 0;
    world.airTime = 0;
    world.sharedDriverId = null;
  }

  function makeFrogEntity(id, human, local, laneIndex) {
    var colors = FROG_COLORS[id] || FROG_COLORS.james;
    var ox = 420 + (laneIndex % 2) * 48;
    var oy = 1960 + Math.floor(laneIndex / 2) * 48;
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
      truckMode: null,
      truckId: null,
      z: 0,
      zVel: 0,
      waterSub: 0,
      wakePhase: 0,
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
      walkPhase: 0,
    };
  }

  function nearestHotspot(world, x, y, maxR) {
    var best = null;
    var bestD = maxR || 80;
    for (var i = 0; i < world.hotspots.length; i++) {
      var h = world.hotspots[i];
      var d = Math.hypot(h.x - x, h.y - y);
      var reach = Math.max(bestD, (h.r || 60) + 12);
      if (d < reach && (!best || d < Math.hypot(best.x - x, best.y - y))) {
        best = h;
        bestD = d;
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
        x: x, y: y,
        life: 0.3 + Math.random() * 0.35,
        vx: (Math.random() - 0.5) * 120,
        vy: -20 - Math.random() * 100,
        hue: 40 + Math.random() * 40,
      });
    }
  }

  function scareFishies(world, x, y) {
    function scareList(list, radius) {
      for (var i = 0; i < list.length; i++) {
        var f = list[i];
        var d = Math.hypot(f.x - x, f.y - y);
        if (d < radius) {
          f.scare = 1.2 + Math.random() * 0.6;
          var ang = Math.atan2(f.y - y, f.x - x);
          f.x += Math.cos(ang) * 36;
          f.y += Math.sin(ang) * 28;
        }
      }
    }
    scareList(world.fish, 180);
    scareList(world.whales, 260);
    spawnSplash(world, x, y, 18);
  }

  function updateFish(world, dt) {
    var pond = AREAS[2];
    function moveSwim(list, baseSp) {
      for (var i = 0; i < list.length; i++) {
        var f = list[i];
        f.phase += dt * f.speed * (f.scare > 0 ? 3.5 : 1);
        if (f.scare > 0) f.scare -= dt;
        var sp = f.scare > 0 ? baseSp * 2.6 : baseSp;
        f.x += Math.cos(f.phase) * sp * dt;
        f.y += Math.sin(f.phase * 0.7) * (sp * 0.7) * dt;
        var pad = f.kind === "whale" ? 80 : 28;
        f.x = clamp(f.x, pond.x + pad, pond.x + pond.w - pad);
        f.y = clamp(f.y, pond.y + pad, pond.y + pond.h - pad);
      }
    }
    moveSwim(world.fish, 22);
    moveSwim(world.whales, 12);
    for (var a = 0; a < world.animals.length; a++) {
      var an = world.animals[a];
      an.phase += dt * an.speed;
      an.x += Math.cos(an.phase) * 10 * dt;
      an.y += Math.sin(an.phase * 0.8) * 8 * dt;
      an.x = clamp(an.x, 120, 640);
      an.y = clamp(an.y, 2100, 2420);
    }
  }

  function updateFx(world, dt) {
    var i;
    for (i = world.dust.length - 1; i >= 0; i--) {
      var d = world.dust[i];
      d.life -= dt; d.x += d.vx * dt; d.y += d.vy * dt;
      if (d.life <= 0) world.dust.splice(i, 1);
    }
    for (i = world.splashes.length - 1; i >= 0; i--) {
      var s = world.splashes[i];
      s.life -= dt; s.x += s.vx * dt; s.y += s.vy * dt; s.vy += 180 * dt;
      if (s.life <= 0) world.splashes.splice(i, 1);
    }
    for (i = world.sparks.length - 1; i >= 0; i--) {
      var k = world.sparks[i];
      k.life -= dt; k.x += k.vx * dt; k.y += k.vy * dt; k.vy += 220 * dt;
      if (k.life <= 0) world.sparks.splice(i, 1);
    }
  }

  function tickDrive(world, ent, dt) {
    var result = { jumped: false, landed: false, scrapGain: 0, splashed: false, onWater: false };
    var gWalk = 520;
    var gTruck = 560;
    var wet = inPond(ent.x, ent.y);
    if (!ent.inTruck) {
      if (ent.z > 0 || ent.zVel !== 0) {
        ent.zVel -= gWalk * dt;
        ent.z += ent.zVel * dt;
        if (ent.z <= 0) {
          ent.z = 0;
          if (ent.zVel < -40) result.landed = true;
          ent.zVel = 0;
          if (wet) {
            spawnSplash(world, ent.x, ent.y, 6);
            ent.waterSub = Math.max(ent.waterSub || 0, 0.35);
            result.splashed = true;
          }
        }
      }
      if (wet && (ent.z || 0) <= 0) {
        ent.waterSub = Math.max(0.12, (ent.waterSub || 0) * Math.exp(-1.8 * dt));
      } else {
        ent.waterSub = Math.max(0, (ent.waterSub || 0) - dt * 1.6);
      }
      ent.speedBoost = 1;
      return result;
    }
    var speed = Math.hypot(ent.vx, ent.vy);
    result.onWater = wet && (ent.z || 0) < 4;
    var ramp = rampAt(ent.x, ent.y);
    if (ramp && ent.z <= 0.5 && speed > 72) {
      var boost = ramp.boost * clamp(speed / 210, 0.55, 1.45);
      ent.zVel = 235 * boost;
      ent.z = Math.max(ent.z, 3);
      result.jumped = true;
      world.stuntCombo += 1;
      var gain = 10 + world.stuntCombo * 5;
      world.scrap += gain;
      result.scrapGain = gain;
      if (wet) spawnSplash(world, ent.x, ent.y, 4);
      else {
        spawnDust(world, ent.x, ent.y, 6);
        spawnSparks(world, ent.x, ent.y, 6);
      }
    }
    if (ent.z > 0 || ent.zVel !== 0) {
      var g = gTruck;
      if (Math.abs(ent.zVel) < 60) g *= 0.78;
      /* Water drag while airborne over pond softens plunge */
      if (wet) g *= 0.92;
      ent.zVel -= g * dt;
      ent.z += ent.zVel * dt;
      world.airTime += dt;
      if (ent.z <= 0) {
        ent.z = 0;
        result.landed = true;
        if (world.airTime > 0.35) {
          var airBonus = Math.floor(world.airTime * 25);
          world.scrap += airBonus;
          result.scrapGain += airBonus;
        }
        var plunge = Math.max(0, -ent.zVel) + world.airTime * 90;
        var airSnap = world.airTime;
        world.airTime = 0;
        ent.zVel = 0;
        if (wet) {
          /* Visual: surface drive default; hard plunge / long air reads as going under */
          var sub = clamp(0.42 + plunge / 320 + airSnap * 0.35, 0.48, 1.2);
          ent.waterSub = Math.max(ent.waterSub || 0, sub);
          spawnSplash(world, ent.x, ent.y, 10 + Math.floor(plunge / 40));
          scareFishies(world, ent.x, ent.y);
          result.splashed = true;
        } else {
          spawnDust(world, ent.x, ent.y, 5);
          ent.waterSub = Math.max(0, (ent.waterSub || 0) - 0.5);
        }
      }
    } else if (speed < 40) {
      world.stuntCombo = 0;
    }

    if (wet && (ent.z || 0) <= 0.5) {
      /* On the water surface — settle toward shallow waterline; expire dive tint */
      if ((ent.waterSub || 0) > 0.4) {
        ent.waterSub = Math.max(0.26, ent.waterSub - dt * 0.75);
      } else {
        ent.waterSub = 0.22 + Math.min(0.12, speed / 2000);
      }
      ent.wakePhase = (ent.wakePhase || 0) + dt * (2.2 + speed * 0.012);
      if (speed > 35 && Math.random() < dt * (2.5 + speed * 0.01)) {
        spawnSplash(world, ent.x - ent.facing * 14, ent.y + 4, 2);
      }
      if (speed > 90 && Math.random() < dt * 1.2) {
        world.scrap += 1;
        result.scrapGain += 1;
      }
    } else if (!wet) {
      ent.waterSub = Math.max(0, (ent.waterSub || 0) - dt * 2.2);
    }

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
    var walkMax = 148;
    var truckMax = 275;
    var maxSp = (ent.inTruck ? truckMax : walkMax) * (ent.speedBoost || 1);
    if (ent.inTruck && ent.dashTrail > 0) maxSp *= 1.28;
    if (typeof speed === "number") maxSp = speed * (ent.speedBoost || 1);
    var mx = ent.steerX;
    var my = ent.steerY;
    var mag = Math.hypot(mx, my);
    if (mag > 1) { mx /= mag; my /= mag; }
    var wetMove = inPond(ent.x, ent.y) && (ent.z || 0) < 3;
    var accel = ent.inTruck ? 920 : 780;
    var friction = ent.inTruck ? 3.4 : 6.2;
    if (wetMove && ent.inTruck) {
      accel *= 0.82;
      friction *= 1.15;
      maxSp *= 0.88;
    } else if (wetMove) {
      accel *= 0.75;
      maxSp *= 0.8;
    }
    var tvx = mx * maxSp;
    var tvy = my * maxSp;
    if (mag > 0.05) {
      ent.vx += (tvx - ent.vx) * Math.min(1, accel * dt / Math.max(60, maxSp));
      ent.vy += (tvy - ent.vy) * Math.min(1, accel * dt / Math.max(60, maxSp));
      if (Math.abs(mx) > 0.08) ent.facing = mx >= 0 ? 1 : -1;
    } else {
      var damp = Math.exp(-friction * dt);
      ent.vx *= damp;
      ent.vy *= damp;
      if (Math.hypot(ent.vx, ent.vy) < 6) { ent.vx = 0; ent.vy = 0; }
    }
    var spd = Math.hypot(ent.vx, ent.vy);
    if (spd > maxSp) {
      ent.vx = (ent.vx / spd) * maxSp;
      ent.vy = (ent.vy / spd) * maxSp;
    }
    ent.x += ent.vx * dt;
    ent.y += ent.vy * dt;
    ent.x = clamp(ent.x, 40, MAP_W - 40);
    ent.y = clamp(ent.y, 40, MAP_H - 40);
    if (!ent.inTruck && spd > 18) {
      ent.walkPhase = (ent.walkPhase || 0) + dt * (8 + spd * 0.04);
    } else {
      ent.walkPhase = (ent.walkPhase || 0) * 0.9;
    }
    if (ent.cd > 0) ent.cd -= dt;
    if (ent.invuln > 0) ent.invuln -= dt;
    if (ent.dashTrail > 0) ent.dashTrail -= dt;
  }

  function boardTruck(world, frogs, frog, hotspot) {
    if (!hotspot || hotspot.kind !== "truck") return false;
    if (frog.inTruck) {
      frog.inTruck = false;
      frog.truckMode = null;
      frog.truckId = null;
      frog.z = 0;
      frog.zVel = 0;
      if (world.sharedDriverId === frog.id) {
        world.sharedDriverId = null;
        for (var i = 0; i < frogs.length; i++) {
          var f = frogs[i];
          if (f.truckMode === "shared") {
            f.inTruck = false;
            f.truckMode = null;
            f.truckId = null;
            f.z = 0;
            f.zVel = 0;
          }
        }
      }
      return true;
    }
    frog.x = hotspot.x;
    frog.y = hotspot.y;
    frog.inTruck = true;
    frog.truckId = hotspot.id;
    frog.truckMode = hotspot.mode || "solo";
    if (hotspot.mode === "shared") {
      world.sharedDriverId = frog.id;
      for (var j = 0; j < frogs.length; j++) {
        var g = frogs[j];
        g.inTruck = true;
        g.truckMode = "shared";
        g.truckId = hotspot.id;
        g.x = hotspot.x + (j - 1.5) * 10;
        g.y = hotspot.y + (j % 2) * 8;
      }
    }
    return true;
  }

  function tickHubAI(frogs, localFrog, dt, world) {
    for (var i = 0; i < frogs.length; i++) {
      var f = frogs[i];
      if (f.human || !f.alive) continue;
      if (localFrog && localFrog.inTruck && localFrog.truckMode === "shared") {
        f.inTruck = true;
        f.truckMode = "shared";
        f.truckId = localFrog.truckId;
        var ox = ((i % 2) - 0.5) * 14;
        var oy = (Math.floor(i / 2) - 0.5) * 12;
        f.x = localFrog.x + ox;
        f.y = localFrog.y + oy;
        f.vx = localFrog.vx;
        f.vy = localFrog.vy;
        f.facing = localFrog.facing;
        f.z = localFrog.z;
        f.waterSub = localFrog.waterSub || 0;
        f.wakePhase = localFrog.wakePhase || 0;
        continue;
      }
      if (f.truckMode === "shared" && !(localFrog && localFrog.inTruck && localFrog.truckMode === "shared")) {
        f.inTruck = false;
        f.truckMode = null;
        f.truckId = null;
        f.z = 0;
      }
      f.aiTimer -= dt;
      if (f.aiTimer <= 0) {
        f.aiTimer = 1.2 + Math.random() * 1.8;
        if (localFrog && Math.random() < 0.55) {
          f.targetX = localFrog.x + (Math.random() - 0.5) * 160;
          f.targetY = localFrog.y + (Math.random() - 0.5) * 160;
        } else {
          var a = AREAS[Math.floor(Math.random() * AREAS.length)];
          f.targetX = a.x + a.w * (0.3 + Math.random() * 0.4);
          f.targetY = a.y + a.h * (0.3 + Math.random() * 0.4);
        }
      }
      var dx = f.targetX - f.x;
      var dy = f.targetY - f.y;
      var d = Math.hypot(dx, dy) || 1;
      if (d < 28) { f.steerX = 0; f.steerY = 0; }
      else { f.steerX = dx / d; f.steerY = dy / d; }
      moveEntity(f, dt);
    }
  }

  function drawSky(ctx, w, h, t, camX, camY) {
    var g = ctx.createLinearGradient(0, 0, 0, h);
    g.addColorStop(0, "#152a52");
    g.addColorStop(0.22, "#2f6a9e");
    g.addColorStop(0.48, "#6fb3c9");
    g.addColorStop(0.68, "#8ecf6e");
    g.addColorStop(1, "#3a6826");
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, w, h);
    var pxFar = -camX * 0.06, pyFar = -camY * 0.03;
    var pxMid = -camX * 0.14, pyMid = -camY * 0.07;
    var pxNear = -camX * 0.22, pyNear = -camY * 0.1;
    ctx.fillStyle = "rgba(32, 58, 90, 0.45)";
    ctx.beginPath();
    ctx.moveTo(0, h * 0.34 + pyFar);
    for (var i = 0; i <= 10; i++) {
      ctx.lineTo((i / 10) * w + pxFar, h * 0.28 + Math.sin(i * 0.85 + t * 0.04) * 22 + pyFar);
    }
    ctx.lineTo(w, h * 0.5); ctx.lineTo(0, h * 0.5); ctx.closePath(); ctx.fill();
    ctx.fillStyle = "rgba(40, 80, 50, 0.55)";
    ctx.beginPath();
    ctx.moveTo(0, h * 0.4 + pyMid);
    for (var j = 0; j <= 10; j++) {
      ctx.lineTo((j / 10) * w + pxMid * 0.7, h * 0.34 + Math.sin(j * 1.05 + 1.2) * 16 + pyMid);
    }
    ctx.lineTo(w, h * 0.56); ctx.lineTo(0, h * 0.56); ctx.closePath(); ctx.fill();
    ctx.fillStyle = "rgba(55, 115, 58, 0.52)";
    ctx.beginPath();
    ctx.moveTo(0, h * 0.46 + pyNear * 0.7);
    for (var k = 0; k <= 12; k++) {
      ctx.lineTo((k / 12) * w + pxNear, h * 0.42 + Math.sin(k * 0.95 + 2.1) * 12 + pyNear * 0.7);
    }
    ctx.lineTo(w, h * 0.6); ctx.lineTo(0, h * 0.6); ctx.closePath(); ctx.fill();
    var sunX = w * 0.8 - camX * 0.02, sunY = h * 0.09 - camY * 0.012;
    var sg = ctx.createRadialGradient(sunX, sunY, 3, sunX, sunY, 70);
    sg.addColorStop(0, "rgba(255, 250, 210, 1)");
    sg.addColorStop(0.25, "rgba(255, 230, 140, 0.55)");
    sg.addColorStop(1, "rgba(255, 180, 80, 0)");
    ctx.fillStyle = sg;
    ctx.beginPath(); ctx.arc(sunX, sunY, 70, 0, Math.PI * 2); ctx.fill();
    for (var c = 0; c < 6; c++) {
      var layer = c < 3 ? 0.35 : 0.55;
      var cx = ((c * 160 + t * (6 + c) + pxMid * layer) % (w + 140)) - 70;
      var cy = h * (0.07 + (c % 3) * 0.035) + pyFar * 0.4;
      ctx.fillStyle = "rgba(255,255,255," + (0.12 + (c % 3) * 0.04) + ")";
      ctx.beginPath();
      ctx.ellipse(cx, cy, 52 - c * 2, 13, 0, 0, Math.PI * 2);
      ctx.ellipse(cx + 30, cy + 3, 38, 11, 0, 0, Math.PI * 2);
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
      ctx.lineWidth = 2.4;
      ctx.lineJoin = "round";
      ctx.stroke();
    }
  }

  function areaCorners(area, camX, camY, vw, vh) {
    return [
      [area.x, area.y],
      [area.x + area.w, area.y],
      [area.x + area.w, area.y + area.h],
      [area.x, area.y + area.h],
    ].map(function (p) { return project(p[0], p[1], camX, camY, vw, vh); });
  }

  function drawMech(ctx, wx, wy, stories, camX, camY, vw, vh, tint) {
    var p = project(wx, wy, camX, camY, vw, vh);
    /* Visual height scales with stories but capped for screen — 1000-story reads as towering */
    var hScale = stories >= 1000 ? 220 : stories >= 100 ? 110 : 48;
    var wScale = stories >= 1000 ? 48 : stories >= 100 ? 28 : 16;
    var s = p.depth;
    var lift = hScale * s;
    var bw = wScale * s;
    ctx.fillStyle = "rgba(0,0,0,0.25)";
    ctx.beginPath();
    ctx.ellipse(p.x, p.y + 4, bw * 1.1, bw * 0.35, 0, 0, Math.PI * 2);
    ctx.fill();
    var grad = ctx.createLinearGradient(p.x - bw, p.y - lift, p.x + bw, p.y);
    grad.addColorStop(0, tint || "#94a3b8");
    grad.addColorStop(0.5, "#64748b");
    grad.addColorStop(1, "#334155");
    ctx.fillStyle = grad;
    ctx.fillRect(p.x - bw * 0.5, p.y - lift, bw, lift);
    ctx.strokeStyle = "#0f172a";
    ctx.lineWidth = 2.2;
    ctx.strokeRect(p.x - bw * 0.5, p.y - lift, bw, lift);
    /* Side depth plane */
    ctx.fillStyle = "rgba(30,41,59,0.75)";
    ctx.beginPath();
    ctx.moveTo(p.x + bw * 0.5, p.y - lift);
    ctx.lineTo(p.x + bw * 0.5 + 10 * s, p.y - lift - 6 * s);
    ctx.lineTo(p.x + bw * 0.5 + 10 * s, p.y - 4 * s);
    ctx.lineTo(p.x + bw * 0.5, p.y);
    ctx.closePath();
    ctx.fill();
    /* Cockpit glow */
    ctx.fillStyle = stories >= 1000 ? "#fbbf24" : "#38bdf8";
    ctx.fillRect(p.x - bw * 0.2, p.y - lift * 0.85, bw * 0.4, lift * 0.08);
    ctx.fillStyle = "rgba(255,255,255,0.9)";
    ctx.font = "bold " + Math.round(10 * s) + "px Segoe UI, system-ui, sans-serif";
    ctx.textAlign = "center";
    ctx.fillText(stories + "-story mech", p.x, p.y - lift - 8 * s);
  }

  function drawRanchHouse(ctx, camX, camY, vw, vh, world) {
    var a = AREAS[0];
    /* Compound ground */
    drawGroundPoly(ctx, areaCorners(a, camX, camY, vw, vh), "rgba(100, 70, 40, 0.28)", "rgba(60,30,10,0.3)");

    /* Big backyard (south) */
    var yard = { x: a.x + 40, y: a.y + a.h - 400, w: 600, h: 360 };
    drawGroundPoly(ctx, areaCorners(yard, camX, camY, vw, vh), "rgba(70, 130, 50, 0.55)", "rgba(40,80,30,0.45)");
    var yl = project(yard.x + yard.w * 0.5, yard.y + 20, camX, camY, vw, vh);
    ctx.fillStyle = "rgba(255,255,255,0.75)";
    ctx.font = "bold 12px Segoe UI, system-ui, sans-serif";
    ctx.textAlign = "center";
    ctx.fillText("Backyard", yl.x, yl.y);

    /* Animals in backyard */
    if (world && world.animals) {
      for (var ai = 0; ai < world.animals.length; ai++) {
        var an = world.animals[ai];
        var ap = project(an.x, an.y, camX, camY, vw, vh);
        if (ap.x < -30 || ap.x > vw + 30 || ap.y < -30 || ap.y > vh + 30) continue;
        var asz = 6 * an.size * ap.depth;
        ctx.fillStyle = "rgba(0,0,0,0.2)";
        ctx.beginPath();
        ctx.ellipse(ap.x, ap.y + 2, asz * 0.9, asz * 0.3, 0, 0, Math.PI * 2);
        ctx.fill();
        ctx.fillStyle = an.tone;
        ctx.beginPath();
        ctx.ellipse(ap.x, ap.y - asz * 0.3, asz, asz * 0.65, 0, 0, Math.PI * 2);
        ctx.fill();
        if (an.kind === 0) {
          ctx.beginPath();
          ctx.arc(ap.x + asz * 0.7, ap.y - asz * 0.5, asz * 0.35, 0, Math.PI * 2);
          ctx.fill();
        }
      }
    }

    /* Huge garage (east of house) */
    var gar = { x: a.x + 640, y: a.y + 80, w: 480, h: 520 };
    drawGroundPoly(ctx, areaCorners(gar, camX, camY, vw, vh), "rgba(55, 55, 60, 0.55)", "rgba(20,20,25,0.5)");
    var g0 = project(gar.x + 30, gar.y + 60, camX, camY, vw, vh);
    var g1 = project(gar.x + gar.w - 30, gar.y + 60, camX, camY, vw, vh);
    var g2 = project(gar.x + gar.w - 30, gar.y + gar.h - 40, camX, camY, vw, vh);
    var g3 = project(gar.x + 30, gar.y + gar.h - 40, camX, camY, vw, vh);
    var gH = 72 * ((g0.depth + g2.depth) * 0.5);
    /* Garage side wall */
    ctx.fillStyle = "#4b5563";
    ctx.beginPath();
    ctx.moveTo(g1.x, g1.y - gH);
    ctx.lineTo(g1.x + 22 * g1.depth, g1.y - gH - 12);
    ctx.lineTo(g2.x + 22 * g2.depth, g2.y - gH * 0.55 - 8);
    ctx.lineTo(g2.x, g2.y - gH * 0.6);
    ctx.closePath();
    ctx.fill();
    /* Garage front */
    var gg = ctx.createLinearGradient(g0.x, g0.y - gH, g3.x, g3.y);
    gg.addColorStop(0, "#9ca3af");
    gg.addColorStop(1, "#6b7280");
    ctx.fillStyle = gg;
    ctx.beginPath();
    ctx.moveTo(g0.x, g0.y - gH);
    ctx.lineTo(g1.x, g1.y - gH);
    ctx.lineTo(g2.x, g2.y - gH * 0.6);
    ctx.lineTo(g3.x, g3.y - gH * 0.6);
    ctx.closePath();
    ctx.fill();
    ctx.strokeStyle = "#0b1220";
    ctx.lineWidth = 2.8;
    ctx.lineJoin = "round";
    ctx.stroke();
    /* Garage door bay */
    var door = project(gar.x + gar.w * 0.5, gar.y + gar.h - 50, camX, camY, vw, vh);
    ctx.fillStyle = "#111827";
    ctx.fillRect(door.x - 55 * door.depth, door.y - 50 * door.depth, 110 * door.depth, 52 * door.depth);
    ctx.strokeStyle = "#fbbf24";
    ctx.lineWidth = 2;
    ctx.strokeRect(door.x - 55 * door.depth, door.y - 50 * door.depth, 110 * door.depth, 52 * door.depth);
    /* Flat roof */
    ctx.fillStyle = "#374151";
    ctx.beginPath();
    ctx.moveTo(g0.x - 8, g0.y - gH);
    ctx.lineTo(g1.x + 8, g1.y - gH);
    ctx.lineTo(g1.x + 28 * g1.depth, g1.y - gH - 14);
    ctx.lineTo(g0.x + 18 * g0.depth, g0.y - gH - 14);
    ctx.closePath();
    ctx.fill();
    var gl = project(gar.x + gar.w * 0.5, gar.y + 30, camX, camY, vw, vh);
    ctx.fillStyle = "rgba(255,255,255,0.9)";
    ctx.font = "bold 13px Segoe UI, system-ui, sans-serif";
    ctx.textAlign = "center";
    ctx.fillText("Garage · James toys", gl.x, gl.y - gH - 18);

    /* 10-story + 100-story mechs reside in garage */
    drawMech(ctx, gar.x + 120, gar.y + 280, 10, camX, camY, vw, vh, "#a5b4fc");
    drawMech(ctx, gar.x + 280, gar.y + 300, 100, camX, camY, vw, vh, "#67e8f9");

    /* Main house — big 2.5D with depth walls, roof planes, porch */
    var hx = a.x + 60, hy = a.y + 100, hw = 520, hh = 420;
    var porch = [
      project(hx + 60, hy + hh - 90, camX, camY, vw, vh),
      project(hx + hw - 60, hy + hh - 90, camX, camY, vw, vh),
      project(hx + hw - 40, hy + hh - 20, camX, camY, vw, vh),
      project(hx + 40, hy + hh - 20, camX, camY, vw, vh),
    ];
    drawGroundPoly(ctx, porch, "rgba(170, 140, 95, 0.78)", "rgba(80,50,20,0.55)");
    /* Porch posts */
    for (var pi = 0; pi < 5; pi++) {
      var px = hx + 80 + pi * ((hw - 160) / 4);
      var pp = project(px, hy + hh - 55, camX, camY, vw, vh);
      ctx.fillStyle = "#5d4037";
      ctx.fillRect(pp.x - 3, pp.y - 38 * pp.depth, 6, 38 * pp.depth);
    }
    var p0 = project(hx + 50, hy + 50, camX, camY, vw, vh);
    var p1 = project(hx + hw - 50, hy + 50, camX, camY, vw, vh);
    var p2 = project(hx + hw - 50, hy + hh - 70, camX, camY, vw, vh);
    var p3 = project(hx + 50, hy + hh - 70, camX, camY, vw, vh);
    var wallH = 88 * ((p0.depth + p2.depth) * 0.5);
    /* Left depth wall */
    ctx.fillStyle = "#8d7355";
    ctx.beginPath();
    ctx.moveTo(p0.x, p0.y - wallH);
    ctx.lineTo(p0.x - 24 * p0.depth, p0.y - wallH - 14);
    ctx.lineTo(p3.x - 24 * p3.depth, p3.y - wallH * 0.65 - 10);
    ctx.lineTo(p3.x, p3.y - wallH * 0.7);
    ctx.closePath();
    ctx.fill();
    /* Right depth wall */
    ctx.fillStyle = "#a68962";
    ctx.beginPath();
    ctx.moveTo(p1.x, p1.y - wallH);
    ctx.lineTo(p1.x + 26 * p1.depth, p1.y - wallH - 14);
    ctx.lineTo(p2.x + 26 * p2.depth, p2.y - wallH * 0.65 - 10);
    ctx.lineTo(p2.x, p2.y - wallH * 0.7);
    ctx.closePath();
    ctx.fill();
    /* Front wall */
    var wg = ctx.createLinearGradient(p0.x, p0.y - wallH, p3.x, p3.y);
    wg.addColorStop(0, "#e2c9a6");
    wg.addColorStop(0.55, "#d4b896");
    wg.addColorStop(1, "#b8956a");
    ctx.fillStyle = wg;
    ctx.beginPath();
    ctx.moveTo(p0.x, p0.y - wallH);
    ctx.lineTo(p1.x, p1.y - wallH);
    ctx.lineTo(p2.x, p2.y - wallH * 0.7);
    ctx.lineTo(p3.x, p3.y - wallH * 0.7);
    ctx.closePath();
    ctx.fill();
    ctx.strokeStyle = "#3e2723";
    ctx.lineWidth = 3.2;
    ctx.lineJoin = "round";
    ctx.stroke();
    /* Windows rows */
    function windowAt(wx, wy, ww, wh) {
      var wp = project(wx, wy, camX, camY, vw, vh);
      ctx.fillStyle = "rgba(120, 200, 255, 0.62)";
      ctx.fillRect(wp.x - ww * 0.5 * wp.depth, wp.y - wh * wp.depth - 10, ww * wp.depth, wh * wp.depth);
      ctx.strokeStyle = "#1c1210";
      ctx.lineWidth = 2;
      ctx.strokeRect(wp.x - ww * 0.5 * wp.depth, wp.y - wh * wp.depth - 10, ww * wp.depth, wh * wp.depth);
      ctx.strokeStyle = "rgba(255,255,255,0.35)";
      ctx.lineWidth = 1;
      ctx.beginPath();
      ctx.moveTo(wp.x, wp.y - wh * wp.depth - 10);
      ctx.lineTo(wp.x, wp.y - 10);
      ctx.moveTo(wp.x - ww * 0.5 * wp.depth, wp.y - wh * 0.5 * wp.depth - 10);
      ctx.lineTo(wp.x + ww * 0.5 * wp.depth, wp.y - wh * 0.5 * wp.depth - 10);
      ctx.stroke();
    }
    windowAt(hx + 120, hy + 160, 28, 22);
    windowAt(hx + 220, hy + 160, 28, 22);
    windowAt(hx + 320, hy + 160, 28, 22);
    windowAt(hx + 420, hy + 160, 28, 22);
    windowAt(hx + 140, hy + 250, 26, 20);
    windowAt(hx + 280, hy + 250, 26, 20);
    windowAt(hx + 400, hy + 250, 26, 20);
    /* Roof planes (left + right) */
    var ridge = project(hx + hw * 0.5, hy + 20, camX, camY, vw, vh);
    var ridgeH = wallH + 55;
    ctx.fillStyle = "#6d4c41";
    ctx.beginPath();
    ctx.moveTo(p0.x - 18, p0.y - wallH);
    ctx.lineTo(ridge.x, ridge.y - ridgeH);
    ctx.lineTo(p1.x + 18, p1.y - wallH);
    ctx.closePath();
    ctx.fill();
    ctx.fillStyle = "#8d6e63";
    ctx.beginPath();
    ctx.moveTo(p1.x + 18, p1.y - wallH);
    ctx.lineTo(ridge.x, ridge.y - ridgeH);
    ctx.lineTo(p1.x + 40 * p1.depth, p1.y - wallH - 16);
    ctx.lineTo(p1.x + 26 * p1.depth, p1.y - wallH);
    ctx.closePath();
    ctx.fill();
    ctx.strokeStyle = "#3e2723";
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.moveTo(p0.x - 18, p0.y - wallH);
    ctx.lineTo(ridge.x, ridge.y - ridgeH);
    ctx.lineTo(p1.x + 18, p1.y - wallH);
    ctx.stroke();
    /* Chimney */
    var ch = project(hx + hw * 0.72, hy + 70, camX, camY, vw, vh);
    ctx.fillStyle = "#795548";
    ctx.fillRect(ch.x - 8, ch.y - wallH - 70, 16 * ch.depth, 36 * ch.depth);
    /* Front door */
    var fr = project(hx + hw * 0.48, hy + hh - 80, camX, camY, vw, vh);
    ctx.fillStyle = "#3e2723";
    ctx.fillRect(fr.x - 16, fr.y - 42, 32, 44);
    ctx.strokeStyle = "#1a100c";
    ctx.lineWidth = 2;
    ctx.strokeRect(fr.x - 16, fr.y - 42, 32, 44);
    ctx.fillStyle = "#fbbf24";
    ctx.beginPath();
    ctx.arc(fr.x + 8, fr.y - 20, 3, 0, Math.PI * 2);
    ctx.fill();
    ctx.font = "bold 14px Segoe UI, system-ui, sans-serif";
    ctx.textAlign = "center";
    ctx.strokeStyle = "rgba(0,0,0,0.7)";
    ctx.lineWidth = 3;
    ctx.strokeText("James · Ranch house", ridge.x, ridge.y - ridgeH - 14);
    ctx.fillStyle = "#fff7ed";
    ctx.fillText("James · Ranch house", ridge.x, ridge.y - ridgeH - 14);

    /* 1000-story mech — does NOT fit in garage; sits out back */
    drawMech(ctx, a.x + 280, a.y + a.h - 80, 1000, camX, camY, vw, vh, "#fcd34d");
  }

  function pathPoint(pt, camX, camY, vw, vh) {
    var elev = (pt[2] || 0) * 32;
    var p = project(pt[0], pt[1], camX, camY, vw, vh);
    return { x: p.x, y: p.y - elev * p.depth, depth: p.depth, elev: elev, wx: pt[0], wy: pt[1] };
  }

  function drawPathRibbon(ctx, pts, camX, camY, vw, vh, stroke, width, dash, close) {
    if (!pts || pts.length < 2) return;
    ctx.beginPath();
    for (var i = 0; i < pts.length; i++) {
      var p = pathPoint(pts[i], camX, camY, vw, vh);
      if (i === 0) ctx.moveTo(p.x, p.y);
      else ctx.lineTo(p.x, p.y);
    }
    if (close) ctx.closePath();
    ctx.strokeStyle = stroke;
    ctx.lineWidth = width;
    ctx.lineJoin = "round";
    ctx.lineCap = "round";
    if (dash) ctx.setLineDash(dash);
    ctx.stroke();
    ctx.setLineDash([]);
  }

  function drawTrackMound(ctx, m, camX, camY, vw, vh) {
    var p = project(m.x, m.y, camX, camY, vw, vh);
    var h = Math.abs(m.h) * 48 * p.depth;
    var rw = m.r * 0.42 * p.depth;
    var rh = m.r * 0.18 * p.depth;
    if (m.h >= 0) {
      /* Mountain / berm */
      ctx.fillStyle = "rgba(0,0,0,0.22)";
      ctx.beginPath();
      ctx.ellipse(p.x + 4, p.y + 6, rw * 1.05, rh * 1.1, -0.35, 0, Math.PI * 2);
      ctx.fill();
      var mg = ctx.createLinearGradient(p.x - rw, p.y, p.x + rw, p.y - h);
      mg.addColorStop(0, "#5a4634");
      mg.addColorStop(0.45, "#7a6248");
      mg.addColorStop(1, "#c4b59a");
      ctx.fillStyle = mg;
      ctx.beginPath();
      ctx.moveTo(p.x - rw, p.y);
      ctx.quadraticCurveTo(p.x - rw * 0.3, p.y - h * 0.55, p.x, p.y - h);
      ctx.quadraticCurveTo(p.x + rw * 0.35, p.y - h * 0.5, p.x + rw, p.y);
      ctx.closePath();
      ctx.fill();
      ctx.strokeStyle = "#1c1410";
      ctx.lineWidth = 2.2;
      ctx.stroke();
      /* Crisp ridgeline */
      ctx.strokeStyle = "rgba(255,255,255,0.22)";
      ctx.lineWidth = 1.4;
      ctx.beginPath();
      ctx.moveTo(p.x - rw * 0.55, p.y - h * 0.15);
      ctx.quadraticCurveTo(p.x, p.y - h * 0.92, p.x + rw * 0.5, p.y - h * 0.2);
      ctx.stroke();
    } else {
      /* Valley bowl */
      ctx.fillStyle = "rgba(40, 32, 24, 0.55)";
      ctx.beginPath();
      ctx.ellipse(p.x, p.y, rw, rh * 1.15, -0.35, 0, Math.PI * 2);
      ctx.fill();
      ctx.strokeStyle = "#0f0c08";
      ctx.lineWidth = 2;
      ctx.stroke();
      ctx.strokeStyle = "rgba(251, 191, 36, 0.35)";
      ctx.lineWidth = 1.5;
      ctx.setLineDash([6, 5]);
      ctx.beginPath();
      ctx.ellipse(p.x, p.y, rw * 0.7, rh * 0.8, -0.35, 0, Math.PI * 2);
      ctx.stroke();
      ctx.setLineDash([]);
    }
  }

  function drawTrack(ctx, camX, camY, vw, vh, t) {
    var a = AREAS[1];
    drawGroundPoly(ctx, areaCorners(a, camX, camY, vw, vh), "rgba(48, 44, 40, 0.9)", "rgba(12,10,8,0.95)");
    var apron = { x: a.x + 36, y: a.y + 36, w: a.w - 72, h: a.h - 72 };
    drawGroundPoly(ctx, areaCorners(apron, camX, camY, vw, vh), "rgba(92, 72, 48, 0.42)", "rgba(30,22,14,0.7)");

    /* Terrain mounds — high mountain regions + low valley */
    for (var mi = 0; mi < TRACK_MOUNDS.length; mi++) {
      drawTrackMound(ctx, TRACK_MOUNDS[mi], camX, camY, vw, vh);
    }

    /* Dirt ribbons: outer squiggle circuit, then branches */
    drawPathRibbon(ctx, TRACK_MAIN, camX, camY, vw, vh, "rgba(18,16,14,0.96)", 22, null, true);
    drawPathRibbon(ctx, TRACK_MAIN, camX, camY, vw, vh, "#a16207", 4, [16, 12], true);
    drawPathRibbon(ctx, TRACK_MAIN, camX, camY, vw, vh, "rgba(28,24,20,0.9)", 12, null, true);
    drawPathRibbon(ctx, TRACK_MAIN, camX, camY, vw, vh, "#e7e5e4", 2.2, [7, 11], true);

    drawPathRibbon(ctx, TRACK_BRANCH_A, camX, camY, vw, vh, "rgba(22,20,18,0.92)", 14, null, true);
    drawPathRibbon(ctx, TRACK_BRANCH_A, camX, camY, vw, vh, "#fbbf24", 2.5, [10, 9], true);

    drawPathRibbon(ctx, TRACK_BRANCH_B, camX, camY, vw, vh, "rgba(24,22,20,0.9)", 12, null, true);
    drawPathRibbon(ctx, TRACK_BRANCH_B, camX, camY, vw, vh, "#fde68a", 2, [8, 10], true);

    /* Checkered start on west straight */
    for (var ci = 0; ci < 10; ci++) {
      var sx = 1780 + ci * 18;
      var sy = 2220 + (ci % 2) * 10;
      var sp = project(sx, sy, camX, camY, vw, vh);
      ctx.fillStyle = ci % 2 === 0 ? "#0a0a0a" : "#f8fafc";
      ctx.strokeStyle = "#111";
      ctx.lineWidth = 1;
      ctx.fillRect(sp.x - 5, sp.y - 7, 10, 14);
      ctx.strokeRect(sp.x - 5, sp.y - 7, 10, 14);
    }

    /* Ramps — crisper wedge + edge */
    for (var ri = 0; ri < RAMPS.length; ri++) {
      var r = RAMPS[ri];
      var rp0 = project(r.x - r.w * 0.5, r.y, camX, camY, vw, vh);
      var rp1 = project(r.x + r.w * 0.5, r.y, camX, camY, vw, vh);
      var rp2 = project(r.x, r.y - r.h * 0.4, camX, camY, vw, vh);
      var rg = ctx.createLinearGradient(rp0.x, rp0.y, rp2.x, rp2.y - 18);
      rg.addColorStop(0, "#57534e");
      rg.addColorStop(1, "#a8a29e");
      ctx.fillStyle = rg;
      ctx.beginPath();
      ctx.moveTo(rp0.x, rp0.y);
      ctx.lineTo(rp2.x, rp2.y - 18 * rp2.depth);
      ctx.lineTo(rp1.x, rp1.y);
      ctx.closePath();
      ctx.fill();
      ctx.strokeStyle = "#0c0a09";
      ctx.lineWidth = 2.4;
      ctx.stroke();
      ctx.strokeStyle = "#f59e0b";
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.moveTo(rp0.x, rp0.y);
      ctx.lineTo(rp2.x, rp2.y - 18 * rp2.depth);
      ctx.lineTo(rp1.x, rp1.y);
      ctx.stroke();
      ctx.fillStyle = "#fef3c7";
      ctx.font = "bold 11px Segoe UI, system-ui, sans-serif";
      ctx.textAlign = "center";
      ctx.fillText("▲", rp2.x, rp2.y - 22 * rp2.depth);
    }

    var label = project(a.x + a.w * 0.5, a.y + 40, camX, camY, vw, vh);
    ctx.fillStyle = "#fffbeb";
    ctx.strokeStyle = "rgba(0,0,0,0.65)";
    ctx.lineWidth = 3;
    ctx.font = "bold 14px Segoe UI, system-ui, sans-serif";
    ctx.textAlign = "center";
    ctx.strokeText("Monster truck track", label.x, label.y - 8);
    ctx.fillText("Monster truck track", label.x, label.y - 8);
  }

  function drawPond(ctx, camX, camY, vw, vh, world, t) {
    var a = AREAS[2];
    /* Bank / reeds apron — crisp shore ring */
    drawGroundPoly(ctx, areaCorners(a, camX, camY, vw, vh), "rgba(72, 120, 52, 0.55)", "rgba(28, 55, 22, 0.85)");
    var bank = { x: a.x + 18, y: a.y + 18, w: a.w - 36, h: a.h - 36 };
    drawGroundPoly(ctx, areaCorners(bank, camX, camY, vw, vh), "rgba(110, 95, 55, 0.5)", "rgba(40, 30, 12, 0.75)");
    var inset = { x: a.x + 42, y: a.y + 42, w: a.w - 84, h: a.h - 84 };
    var water = areaCorners(inset, camX, camY, vw, vh);
    var wg = ctx.createLinearGradient(water[0].x, water[0].y, water[2].x, water[2].y);
    wg.addColorStop(0, "rgba(8, 70, 100, 0.94)");
    wg.addColorStop(0.4, "rgba(12, 120, 150, 0.9)");
    wg.addColorStop(0.7, "rgba(14, 145, 170, 0.88)");
    wg.addColorStop(1, "rgba(6, 55, 85, 0.95)");
    drawGroundPoly(ctx, water, wg, "rgba(4, 30, 45, 0.95)");
    /* Foam waterline — high-contrast shore edge */
    ctx.beginPath();
    for (var wi = 0; wi < water.length; wi++) {
      if (wi === 0) ctx.moveTo(water[wi].x, water[wi].y);
      else ctx.lineTo(water[wi].x, water[wi].y);
    }
    ctx.closePath();
    ctx.strokeStyle = "rgba(220, 245, 255, 0.75)";
    ctx.lineWidth = 3.2;
    ctx.stroke();
    ctx.strokeStyle = "rgba(6, 40, 60, 0.9)";
    ctx.lineWidth = 1.4;
    ctx.stroke();
    for (var s = 0; s < 10; s++) {
      var sy = a.y + 70 + s * 95 + Math.sin(t * 1.8 + s) * 8;
      var sp0 = project(a.x + 90, sy, camX, camY, vw, vh);
      var sp1 = project(a.x + a.w - 90, sy + 10, camX, camY, vw, vh);
      ctx.strokeStyle = "rgba(190, 240, 255, " + (0.1 + 0.1 * Math.sin(t * 2 + s)) + ")";
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.moveTo(sp0.x, sp0.y);
      ctx.quadraticCurveTo((sp0.x + sp1.x) * 0.5, sp0.y + Math.sin(t * 3 + s) * 8, sp1.x, sp1.y);
      ctx.stroke();
    }
    function drawSwimmer(f, isWhale) {
      var fp = project(f.x, f.y, camX, camY, vw, vh);
      if (fp.x < -80 || fp.x > vw + 80 || fp.y < -80 || fp.y > vh + 80) return;
      var fs = (isWhale ? 10 : 5) + f.size * (isWhale ? 2.2 : 2.4);
      fs *= fp.depth * (f.scare > 0 ? 1.12 : 1);
      ctx.fillStyle = "rgba(0,0,0,0.25)";
      ctx.beginPath();
      ctx.ellipse(fp.x, fp.y + 3, fs * 0.9, fs * 0.22, f.phase, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = f.scare > 0 ? "#fef08a" : (isWhale ? "#7dd3fc" : "#fde68a");
      ctx.beginPath();
      ctx.ellipse(fp.x, fp.y, fs, fs * (isWhale ? 0.38 : 0.45), f.phase, 0, Math.PI * 2);
      ctx.fill();
      ctx.strokeStyle = isWhale ? "#0c4a6e" : "#92400e";
      ctx.lineWidth = 1.6;
      ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(fp.x - Math.cos(f.phase) * fs, fp.y - Math.sin(f.phase) * fs * 0.35);
      ctx.lineTo(fp.x - Math.cos(f.phase) * fs * 1.55, fp.y);
      ctx.lineTo(fp.x - Math.cos(f.phase) * fs, fp.y + Math.sin(f.phase) * fs * 0.35);
      ctx.fillStyle = isWhale ? "#38bdf8" : "#fcd34d";
      ctx.fill();
      ctx.strokeStyle = isWhale ? "#075985" : "#78350f";
      ctx.lineWidth = 1.2;
      ctx.stroke();
      if (isWhale) {
        ctx.fillStyle = "rgba(255,255,255,0.4)";
        ctx.beginPath();
        ctx.ellipse(fp.x + fs * 0.15, fp.y - fs * 0.15, fs * 0.35, fs * 0.12, f.phase, 0, Math.PI * 2);
        ctx.fill();
      }
    }
    for (var i = 0; i < world.fish.length; i++) drawSwimmer(world.fish[i], false);
    for (var wj = 0; wj < world.whales.length; wj++) drawSwimmer(world.whales[wj], true);
    var label = project(a.x + a.w * 0.5, a.y + 28, camX, camY, vw, vh);
    ctx.font = "bold 14px Segoe UI, system-ui, sans-serif";
    ctx.textAlign = "center";
    ctx.strokeStyle = "rgba(0,0,0,0.7)";
    ctx.lineWidth = 3;
    ctx.strokeText("Pond · fishies & whales", label.x, label.y);
    ctx.fillStyle = "#ecfeff";
    ctx.fillText("Pond · fishies & whales", label.x, label.y);
  }

  function drawToys(ctx, world, camX, camY, vw, vh) {
    for (var i = 0; i < world.toys.length; i++) {
      var toy = world.toys[i];
      var p = project(toy.x, toy.y, camX, camY, vw, vh);
      if (p.x < -20 || p.x > vw + 20 || p.y < -20 || p.y > vh + 20) continue;
      var s = (toy.inGarage ? 7 : 8) * p.depth;
      ctx.fillStyle = "rgba(0,0,0,0.2)";
      ctx.beginPath();
      ctx.ellipse(p.x, p.y + 2, s * 0.9, s * 0.3, 0, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = "hsl(" + toy.hue + " 70% 55%)";
      if (toy.kind === 0) {
        ctx.beginPath(); ctx.arc(p.x, p.y - s * 0.3, s, 0, Math.PI * 2); ctx.fill();
      } else if (toy.kind === 1) {
        ctx.fillRect(p.x - s, p.y - s * 1.4, s * 2, s * 1.6);
      } else if (toy.kind === 2) {
        ctx.beginPath();
        ctx.moveTo(p.x, p.y - s * 2); ctx.lineTo(p.x + s, p.y); ctx.lineTo(p.x - s, p.y);
        ctx.closePath(); ctx.fill();
      } else {
        ctx.fillRect(p.x - s * 1.2, p.y - s * 0.5, s * 2.4, s);
      }
    }
  }

  function drawCybertruck(ctx, x, y, facing, depth, driving, z, accent, water) {
    var s = 1.2 * depth;
    var lift = (z || 0) * 0.55 * depth;
    var wet = water && water.inWater;
    var sub = wet ? clamp(water.sub || 0, 0, 1.2) : 0;
    /* Surface drive sits ON the water — tiny lift; dive sinks visually */
    var waterSit = wet && (z || 0) < 5 ? (2.5 - sub * 10) * depth : 0;
    var drawY = y - lift + (wet ? Math.max(-2, -waterSit * 0.15) : 0);
    /* When diving (high sub), truck sits lower through the waterline */
    if (wet && sub > 0.5) drawY += (sub - 0.5) * 14 * depth;

    ctx.save();
    ctx.translate(x, drawY);
    ctx.scale(facing < 0 ? -1 : 1, 1);

    /* Wake / ripples under tires when on surface */
    if (wet && (z || 0) < 6) {
      var wp = water.wakePhase || 0;
      var wakeA = 0.35 + 0.2 * Math.sin(wp * 3);
      for (var wk = 0; wk < 3; wk++) {
        var wr = (18 + wk * 12 + (wp * 20 + wk * 8) % 14) * s;
        ctx.strokeStyle = "rgba(200, 240, 255, " + (wakeA * (1 - wk * 0.28)) + ")";
        ctx.lineWidth = 2.2 - wk * 0.4;
        ctx.beginPath();
        ctx.ellipse(-6 * s, 12 * s + lift * 0.15, wr, wr * 0.28, 0, 0, Math.PI * 2);
        ctx.stroke();
      }
      /* V-wake behind */
      ctx.strokeStyle = "rgba(180, 230, 255, 0.55)";
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.moveTo(-8 * s, 10 * s);
      ctx.lineTo(-42 * s, 6 * s + Math.sin(wp * 4) * 3 * s);
      ctx.moveTo(-8 * s, 10 * s);
      ctx.lineTo(-42 * s, 16 * s + Math.cos(wp * 4) * 3 * s);
      ctx.stroke();
    }

    /* Ground / water contact shadow */
    var shA = wet ? 0.12 : (0.3 - Math.min(0.18, (z || 0) * 0.004));
    ctx.fillStyle = "rgba(0,0,0," + shA + ")";
    ctx.beginPath();
    ctx.ellipse(0, 10 * s + lift * 0.3, 32 * s, 8 * s, 0, 0, Math.PI * 2);
    ctx.fill();

    var bodyGrad = ctx.createLinearGradient(-30 * s, -18 * s, 36 * s, 10 * s);
    bodyGrad.addColorStop(0, driving ? "#f8fafc" : "#d1d5db");
    bodyGrad.addColorStop(0.45, driving ? "#e2e8f0" : "#9ca3af");
    bodyGrad.addColorStop(1, driving ? "#94a3b8" : "#6b7280");
    ctx.fillStyle = bodyGrad;
    ctx.beginPath();
    ctx.moveTo(-30 * s, -4 * s);
    ctx.lineTo(8 * s, -16 * s);
    ctx.lineTo(34 * s, -8 * s);
    ctx.lineTo(38 * s, 6 * s);
    ctx.lineTo(-32 * s, 8 * s);
    ctx.closePath();
    ctx.fill();
    /* Crisp silhouette */
    ctx.strokeStyle = "#020617";
    ctx.lineWidth = 2.4;
    ctx.lineJoin = "round";
    ctx.stroke();
    /* Panel crease / stainless edge */
    ctx.strokeStyle = "rgba(255,255,255,0.55)";
    ctx.lineWidth = 1.3;
    ctx.beginPath();
    ctx.moveTo(-28 * s, -2 * s);
    ctx.lineTo(6 * s, -13 * s);
    ctx.lineTo(32 * s, -6 * s);
    ctx.stroke();
    ctx.strokeStyle = "rgba(15,23,42,0.55)";
    ctx.lineWidth = 1.2;
    ctx.beginPath();
    ctx.moveTo(-30 * s, 4 * s);
    ctx.lineTo(36 * s, 2 * s);
    ctx.stroke();
    if (accent) {
      ctx.strokeStyle = accent;
      ctx.lineWidth = 3.2;
      ctx.beginPath();
      ctx.moveTo(-28 * s, 2 * s);
      ctx.lineTo(30 * s, 0);
      ctx.stroke();
      ctx.strokeStyle = "rgba(0,0,0,0.35)";
      ctx.lineWidth = 1;
      ctx.stroke();
    }
    /* Cabin glass — sharper */
    ctx.fillStyle = "rgba(15, 23, 42, 0.95)";
    ctx.beginPath();
    ctx.moveTo(-6 * s, -5 * s);
    ctx.lineTo(12 * s, -20 * s);
    ctx.lineTo(24 * s, -9 * s);
    ctx.lineTo(-2 * s, -3 * s);
    ctx.closePath();
    ctx.fill();
    ctx.strokeStyle = "#020617";
    ctx.lineWidth = 1.8;
    ctx.stroke();
    ctx.fillStyle = "rgba(125, 211, 252, 0.35)";
    ctx.beginPath();
    ctx.moveTo(-2 * s, -6 * s);
    ctx.lineTo(11 * s, -17 * s);
    ctx.lineTo(18 * s, -10 * s);
    ctx.lineTo(2 * s, -5 * s);
    ctx.closePath();
    ctx.fill();
    ctx.fillStyle = driving ? "#fef08a" : "#94a3b8";
    ctx.fillRect(28 * s, -6 * s, 6 * s, 3.5 * s);
    ctx.strokeStyle = "#0f172a";
    ctx.lineWidth = 1;
    ctx.strokeRect(28 * s, -6 * s, 6 * s, 3.5 * s);
    function wheel(wx, wy) {
      ctx.fillStyle = "#020617";
      ctx.beginPath(); ctx.arc(wx, wy, 8.2 * s, 0, Math.PI * 2); ctx.fill();
      ctx.strokeStyle = "#64748b"; ctx.lineWidth = 2.2;
      ctx.beginPath(); ctx.arc(wx, wy, 5.2 * s, 0, Math.PI * 2); ctx.stroke();
      ctx.strokeStyle = "#94a3b8"; ctx.lineWidth = 1;
      ctx.beginPath(); ctx.arc(wx, wy, 2.5 * s, 0, Math.PI * 2); ctx.stroke();
    }
    var wheelY = (z || 0) < 8 ? 9 : 4;
    if (!(wet && sub > 0.75)) {
      wheel(-16 * s, wheelY * s);
      wheel(18 * s, (wheelY - 1) * s);
    }

    /* Waterline clip + submerged tint — truck reads as ON / under surface */
    if (wet && (z || 0) < 8) {
      var waterlineY = 4 * s + sub * 10 * s;
      /* Foam line across hull */
      ctx.strokeStyle = "rgba(240, 250, 255, 0.85)";
      ctx.lineWidth = 2.4;
      ctx.beginPath();
      ctx.moveTo(-34 * s, waterlineY);
      ctx.lineTo(40 * s, waterlineY - 1 * s);
      ctx.stroke();
      ctx.strokeStyle = "rgba(8, 60, 90, 0.7)";
      ctx.lineWidth = 1.2;
      ctx.beginPath();
      ctx.moveTo(-34 * s, waterlineY + 2);
      ctx.lineTo(40 * s, waterlineY + 1);
      ctx.stroke();
      /* Submerged band — clips lower body into water */
      ctx.save();
      ctx.beginPath();
      ctx.rect(-42 * s, waterlineY, 88 * s, 36 * s);
      ctx.clip();
      var tint = sub > 0.55 ? 0.55 + (sub - 0.55) * 0.35 : 0.32;
      ctx.fillStyle = "rgba(6, 70, 100, " + tint + ")";
      ctx.fillRect(-42 * s, waterlineY, 88 * s, 36 * s);
      ctx.fillStyle = "rgba(34, 180, 200, " + (0.12 + sub * 0.12) + ")";
      ctx.fillRect(-42 * s, waterlineY, 88 * s, 10 * s);
      ctx.restore();
      /* Full-body dive wash when deep */
      if (sub > 0.7) {
        ctx.fillStyle = "rgba(4, 50, 80, " + clamp((sub - 0.7) * 0.85, 0, 0.45) + ")";
        ctx.beginPath();
        ctx.moveTo(-30 * s, -4 * s);
        ctx.lineTo(8 * s, -16 * s);
        ctx.lineTo(34 * s, -8 * s);
        ctx.lineTo(38 * s, 6 * s);
        ctx.lineTo(-32 * s, 8 * s);
        ctx.closePath();
        ctx.fill();
      }
    }

    ctx.restore();
  }

  function drawFroggy(ctx, frog, camX, camY, vw, vh, frogs) {
    var p = project(frog.x, frog.y, camX, camY, vw, vh);
    var s = 15.2 * p.depth * (0.92 + 0.08 * p.depth);
    var bob = (!frog.inTruck && (frog.walkPhase || 0) > 0.05)
      ? Math.abs(Math.sin(frog.walkPhase)) * 2.4 * p.depth : 0;
    var lift = (frog.z || 0) * 0.58 * p.depth + bob;

    if (frog.inTruck && frog.truckMode === "shared" && !frog.local) {
      return p; /* drawn on shared truck roof by driver */
    }

    if (frog.inTruck && frog.local) {
      drawCybertruck(ctx, p.x, p.y, frog.facing, p.depth, true, frog.z || 0, frog.color, { inWater: inPond(frog.x, frog.y), sub: frog.waterSub || 0, wakePhase: frog.wakePhase || 0 });
      if (frog.truckMode === "shared" && frogs) {
        var riders = frogs.slice().sort(function (a, b) { return a.id.localeCompare(b.id); });
        for (var ri = 0; ri < riders.length; ri++) {
          var rf = riders[ri];
          var ox = (ri - 1.5) * 10 * p.depth;
          ctx.fillStyle = rf.color;
          ctx.beginPath();
          ctx.arc(p.x + ox, p.y - 24 * p.depth - lift, 6.5 * p.depth, 0, Math.PI * 2);
          ctx.fill();
          ctx.fillStyle = rf.hat || "#facc15";
          ctx.beginPath();
          ctx.arc(p.x + ox, p.y - 30 * p.depth - lift, 3.5 * p.depth, 0, Math.PI * 2);
          ctx.fill();
        }
      } else {
        ctx.fillStyle = frog.color;
        ctx.beginPath();
        ctx.arc(p.x, p.y - 24 * p.depth - lift, 7 * p.depth, 0, Math.PI * 2);
        ctx.fill();
        ctx.fillStyle = frog.hat || "#facc15";
        ctx.beginPath();
        ctx.arc(p.x, p.y - 30 * p.depth - lift, 4 * p.depth, 0, Math.PI * 2);
        ctx.fill();
      }
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

    if (frog.inTruck && !frog.local && frog.truckMode === "solo") {
      drawCybertruck(ctx, p.x, p.y, frog.facing, p.depth, true, frog.z || 0, frog.color, { inWater: inPond(frog.x, frog.y), sub: frog.waterSub || 0, wakePhase: frog.wakePhase || 0 });
      ctx.fillStyle = frog.color;
      ctx.beginPath();
      ctx.arc(p.x, p.y - 22 * p.depth - lift, 6 * p.depth, 0, Math.PI * 2);
      ctx.fill();
      return p;
    }

    var shA = 0.32 - Math.min(0.2, (frog.z || 0) * 0.004);
    var shW = s * (1.05 - Math.min(0.35, (frog.z || 0) * 0.008));
    ctx.fillStyle = "rgba(0,0,0," + shA + ")";
    ctx.beginPath();
    ctx.ellipse(p.x, p.y + 5, shW, s * 0.32, -0.15, 0, Math.PI * 2);
    ctx.fill();
    var by = p.y - s * 0.4 - lift;
    ctx.strokeStyle = frog.accent;
    ctx.lineWidth = 2.5 * p.depth;
    ctx.beginPath();
    ctx.moveTo(p.x - 6 * p.depth, by + s * 0.5);
    ctx.lineTo(p.x - 10 * p.depth, p.y + 2 - lift * 0.2);
    ctx.moveTo(p.x + 6 * p.depth, by + s * 0.5);
    ctx.lineTo(p.x + 10 * p.depth, p.y + 2 - lift * 0.2);
    ctx.stroke();
    var bg = ctx.createRadialGradient(p.x - 3, by - 4, 2, p.x, by, s);
    bg.addColorStop(0, frog.color);
    bg.addColorStop(1, frog.accent);
    ctx.fillStyle = bg;
    ctx.beginPath(); ctx.arc(p.x, by, s, 0, Math.PI * 2); ctx.fill();
    ctx.strokeStyle = frog.accent; ctx.lineWidth = 2; ctx.stroke();
    ctx.fillStyle = frog.hat || "#facc15";
    ctx.beginPath();
    ctx.ellipse(p.x, by - s * 0.85, s * 0.75, s * 0.28, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillRect(p.x - s * 0.35, by - s * 1.35, s * 0.7, s * 0.5);
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
    if (!frog.human) {
      ctx.fillStyle = "rgba(0,0,0,0.65)";
      ctx.strokeStyle = "#000";
      ctx.lineWidth = 3;
      ctx.font = "bold 10px Segoe UI, system-ui, sans-serif";
      ctx.textAlign = "center";
      var aiLabel = (frog.name || "AI") + " · AI";
      ctx.strokeText(aiLabel, p.x, p.y + s + 12);
      ctx.fillStyle = frog.color || "#fff";
      ctx.fillText(aiLabel, p.x, p.y + s + 12);
    }
    if (frog.local) {
      ctx.strokeStyle = "rgba(255,255,255,0.9)";
      ctx.lineWidth = 2.5;
      ctx.beginPath(); ctx.arc(p.x, by, s + 6, 0, Math.PI * 2); ctx.stroke();
      ctx.fillStyle = "#fff";
      ctx.font = "bold 11px Segoe UI, system-ui, sans-serif";
      ctx.textAlign = "center";
      ctx.strokeStyle = "#000";
      ctx.lineWidth = 3;
      ctx.strokeText(frog.name || "You", p.x, by - s - 8);
      ctx.fillText(frog.name || "You", p.x, by - s - 8);
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
    ctx.fillStyle = near ? "#fef3c7" : "rgba(255,255,255,0.65)";
    ctx.font = "bold " + (near ? 12 : 11) + "px Segoe UI, system-ui, sans-serif";
    ctx.textAlign = "center";
    ctx.fillText(near ? h.label + " · E" : h.label, p.x, p.y - (near ? 24 : 18) * p.depth);
  }

  function drawParkedTrucks(ctx, world, frogs, camX, camY, vw, vh) {
    var occupied = {};
    for (var i = 0; i < frogs.length; i++) {
      if (frogs[i].inTruck && frogs[i].truckId) occupied[frogs[i].truckId] = true;
    }
    for (var t = 0; t < TRUCK_SPOTS.length; t++) {
      var spot = TRUCK_SPOTS[t];
      var hid = spot.id === "shared" ? "truck-shared" : "truck-" + spot.id;
      if (occupied[hid]) continue;
      var anyInShared = frogs.some(function (f) { return f.inTruck && f.truckMode === "shared"; });
      if (spot.id === "shared" && anyInShared) continue;
      var soloTaken = frogs.some(function (f) {
        return f.inTruck && f.truckMode === "solo" && f.truckId === hid;
      });
      if (soloTaken) continue;
      var p = project(spot.x, spot.y, camX, camY, vw, vh);
      var accent = spot.id === "shared" ? "#fbbf24" : (FROG_COLORS[spot.id] || {}).body;
      drawCybertruck(ctx, p.x, p.y, 1, p.depth, false, 0, accent, { inWater: inPond(spot.x, spot.y), sub: 0, wakePhase: 0 });
    }
  }

  function drawFx(ctx, world, camX, camY, vw, vh) {
    var i;
    for (i = 0; i < world.dust.length; i++) {
      var d = world.dust[i];
      var dp = project(d.x, d.y, camX, camY, vw, vh);
      ctx.fillStyle = "rgba(180, 160, 120, " + clamp(d.life * 1.5, 0, 0.5) + ")";
      ctx.beginPath(); ctx.arc(dp.x, dp.y, d.r * dp.depth, 0, Math.PI * 2); ctx.fill();
    }
    for (i = 0; i < world.splashes.length; i++) {
      var s = world.splashes[i];
      var sp = project(s.x, s.y, camX, camY, vw, vh);
      ctx.fillStyle = "rgba(180, 230, 255, " + clamp(s.life * 1.8, 0, 0.7) + ")";
      ctx.beginPath(); ctx.arc(sp.x, sp.y - (0.5 - s.life) * 30, s.r * sp.depth, 0, Math.PI * 2); ctx.fill();
    }
    for (i = 0; i < world.sparks.length; i++) {
      var k = world.sparks[i];
      var kp = project(k.x, k.y, camX, camY, vw, vh);
      ctx.fillStyle = "hsla(" + k.hue + ", 90%, 60%, " + clamp(k.life * 2, 0, 1) + ")";
      ctx.fillRect(kp.x, kp.y - (0.4 - k.life) * 20, 3, 3);
    }
  }

  function drawStarshipPad(ctx, camX, camY, vw, vh, near) {
    /* Connected approach — shared canon gold guide (parity1 Canvas polish) */
    var C0 = global.FroggiesCanon;
    var pathPts = (C0 && C0.STARSHIP_APPROACH) || [
      [520, 1480], [480, 1200], [430, 900], [390, 620], [360, 400],
    ];
    ctx.beginPath();
    for (var i = 0; i < pathPts.length; i++) {
      var pp = project(pathPts[i][0], pathPts[i][1], camX, camY, vw, vh);
      if (i === 0) ctx.moveTo(pp.x, pp.y);
      else ctx.lineTo(pp.x, pp.y);
    }
    ctx.strokeStyle = "rgba(180, 140, 40, 0.5)";
    ctx.lineWidth = 12;
    ctx.lineJoin = "round";
    ctx.lineCap = "round";
    ctx.stroke();
    ctx.strokeStyle = "rgba(251, 191, 36, 0.75)";
    ctx.lineWidth = 3.5;
    ctx.setLineDash([12, 9]);
    ctx.stroke();
    ctx.setLineDash([]);
    ctx.strokeStyle = "rgba(254, 243, 199, 0.55)";
    ctx.lineWidth = 1.5;
    ctx.stroke();

    var p = project(STARSHIP.x, STARSHIP.y, camX, camY, vw, vh);
    /* Wide pad rings */
    ctx.fillStyle = near ? "rgba(56,189,248,0.4)" : "rgba(100,116,139,0.5)";
    ctx.beginPath();
    ctx.ellipse(p.x, p.y, STARSHIP.padR * 0.55 * p.depth, STARSHIP.padR * 0.22 * p.depth, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.strokeStyle = "#38bdf8";
    ctx.lineWidth = 3;
    ctx.stroke();
    ctx.beginPath();
    ctx.ellipse(p.x, p.y, STARSHIP.padR * 0.35 * p.depth, STARSHIP.padR * 0.14 * p.depth, 0, 0, Math.PI * 2);
    ctx.strokeStyle = "rgba(125, 211, 252, 0.8)";
    ctx.lineWidth = 2;
    ctx.stroke();
    /* Starship silhouette */
    ctx.fillStyle = "#e2e8f0";
    ctx.beginPath();
    ctx.moveTo(p.x, p.y - 52 * p.depth);
    ctx.lineTo(p.x + 16 * p.depth, p.y - 6 * p.depth);
    ctx.lineTo(p.x - 16 * p.depth, p.y - 6 * p.depth);
    ctx.closePath();
    ctx.fill();
    ctx.fillStyle = "#94a3b8";
    ctx.fillRect(p.x - 8 * p.depth, p.y - 6 * p.depth, 16 * p.depth, 18 * p.depth);
    ctx.fillStyle = "#f97316";
    ctx.beginPath();
    ctx.ellipse(p.x + 22 * p.depth, p.y - 22 * p.depth, 8 * p.depth, 7 * p.depth, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.font = "bold " + Math.round(12 * p.depth) + "px system-ui,sans-serif";
    ctx.textAlign = "center";
    ctx.fillStyle = "#7dd3fc";
    ctx.fillText("Starship · Spotty", p.x, p.y + 36 * p.depth);
  }

  function render(ctx, world, frogs, camX, camY, vw, vh, t, nearHot) {
    drawSky(ctx, vw, vh, t, camX, camY);
    var g0 = project(0, 0, camX, camY, vw, vh);
    var g1 = project(MAP_W, 0, camX, camY, vw, vh);
    var g2 = project(MAP_W, MAP_H, camX, camY, vw, vh);
    var g3 = project(0, MAP_H, camX, camY, vw, vh);
    drawGroundPoly(ctx, [g0, g1, g2, g3], "rgba(88, 148, 52, 0.72)", null);

    var tileStep = 120;
    for (var ty = 40; ty < MAP_H; ty += tileStep) {
      for (var tx = 40; tx < MAP_W; tx += tileStep) {
        if (areaAt(tx + 20, ty + 20)) continue;
        var tp = project(tx, ty, camX, camY, vw, vh);
        if (tp.x < -40 || tp.x > vw + 40 || tp.y < -40 || tp.y > vh + 40) continue;
        var checker = ((tx / tileStep) + (ty / tileStep)) % 2 === 0;
        ctx.fillStyle = checker ? "rgba(70, 130, 45, 0.14)" : "rgba(110, 170, 70, 0.1)";
        ctx.beginPath();
        ctx.ellipse(tp.x, tp.y, 28 * tp.depth, 12 * tp.depth, -0.4, 0, Math.PI * 2);
        ctx.fill();
      }
    }

    drawPond(ctx, camX, camY, vw, vh, world, t);
    drawRanchHouse(ctx, camX, camY, vw, vh, world);
    drawTrack(ctx, camX, camY, vw, vh, t);
    drawToys(ctx, world, camX, camY, vw, vh);
    drawStarshipPad(ctx, camX, camY, vw, vh, nearHot && nearHot.id === "starship");
    drawFx(ctx, world, camX, camY, vw, vh);
    drawParkedTrucks(ctx, world, frogs, camX, camY, vw, vh);

    for (var hi = 0; hi < world.hotspots.length; hi++) {
      var h = world.hotspots[hi];
      var hideTruck = h.kind === "truck" && frogs.some(function (f) {
        return f.local && f.inTruck && (f.truckId === h.id || (f.truckMode === "shared" && h.mode === "shared"));
      });
      if (hideTruck) continue;
      drawHotspot(ctx, h, camX, camY, vw, vh, nearHot && nearHot.id === h.id);
    }

    var sorted = frogs.slice().sort(function (a, b) { return a.y - b.y; });
    for (var fi = 0; fi < sorted.length; fi++) {
      drawFroggy(ctx, sorted[fi], camX, camY, vw, vh, frogs);
    }

    var rim = ctx.createRadialGradient(vw * 0.55, vh * 0.35, vw * 0.1, vw * 0.5, vh * 0.5, vw * 0.85);
    rim.addColorStop(0, "rgba(255, 230, 170, 0.05)");
    rim.addColorStop(0.55, "rgba(0,0,0,0)");
    rim.addColorStop(1, "rgba(6, 18, 8, 0.28)");
    ctx.fillStyle = rim;
    ctx.fillRect(0, 0, vw, vh);
  }

  global.FroggiesWorld = {
    MAP_W: MAP_W,
    MAP_H: MAP_H,
    AREAS: AREAS,
    RAMPS: RAMPS,
    TRUCK_SPOTS: TRUCK_SPOTS,
    STARSHIP: STARSHIP,
    FROG_COLORS: FROG_COLORS,
    createWorld: createWorld,
    seedDecor: seedDecor,
    makeFrogEntity: makeFrogEntity,
    areaAt: areaAt,
    areaNameAt: areaNameAt,
    onTrack: onTrack,
    inPond: inPond,
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
    boardTruck: boardTruck,
    project: project,
    render: render,
  };
})(typeof window !== "undefined" ? window : globalThis);
