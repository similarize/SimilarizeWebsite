/* Four Froggies — shared Ben-canon constants for all engines.
   WORLD_BIBLE only. No invented cast/zone/toy names. Physics/look engines compare.
   parity1: landmark layout (compound / track / pond / trucks) shared for Canvas + Phaser + three.
   polish7: zone signs (HOUSE/TRACK/POND/GARAGE/STARSHIP) + AI chat one-liners from existing canon only.
   polish8: shared landmarks unchanged; art punch lives in engine renderers.
   polish9: landmarks unchanged; party/nameplate/gate/kit punch in engines.
   polish10: zone signs fade when close (frogs readable).
   solid1: shared solid walls / mech pads / parked trucks; house doorway open.
   truck1: kid-toy truck scale constants; trackElevAt for path/mound undulation.
   truck2: stronger trackElevAt + ramp wedges; exit anytime HUD.
   hop1: HOP ability (replaces ZOOM); shoveSmallProp for toys/animals/pollen.
   hop2: tickLocoHop — continuous ranch foot hop cycle (any move input).
   hop3: faster loco hop; near-zero ability CD; airborne stack height (combo hops).
   track3: banked turns + rock obstacles + live monster-truck wheel scale.
   hop4: snappier always-hop (higher carry / shorter plant / higher launch); humanoid frog silhouette. */
(function (global) {
  "use strict";

  var FROG_ORDER = ["james", "jimmy", "bubbles", "rexy"];

  var FROG_DEFS = {
    james: { id: "james", name: "James", role: "Wheel", color: "#4ade80", accent: "#166534", hat: "#facc15", ability: "HOP" },
    jimmy: { id: "jimmy", name: "Jimmy", role: "Shield", color: "#fb923c", accent: "#9a3412", hat: "#ef4444", ability: "HOP" },
    bubbles: { id: "bubbles", name: "Bubbles", role: "Zap", color: "#60a5fa", accent: "#1e3a8a", hat: "#38bdf8", ability: "HOP" },
    rexy: { id: "rexy", name: "Rexy", role: "Bot", color: "#c084fc", accent: "#6b21a8", hat: "#e879f9", ability: "HOP" },
  };

  /* ~10× area vs old 1200×900 — real roam between house / track / pond / Starship */
  var MAP_W = 4200;
  var MAP_H = 3150;

  var AREAS = [
    { id: "house", name: "Ranch house", x: 60, y: 1320, w: 1180, h: 1180, color: "#8b5a2b" },
    { id: "track", name: "Monster truck track", x: 1680, y: 1580, w: 2200, h: 1380, color: "#57534e" },
    { id: "pond", name: "Pond", x: 2320, y: 80, w: 1680, h: 1180, color: "#0e7490" },
  ];

  var COMPOUND = {
    yard: { x: 100, y: 2100, w: 600, h: 360 },
    garage: { x: 700, y: 1400, w: 480, h: 520 },
    house: { x: 120, y: 1420, w: 520, h: 420 },
    mech10: { x: 820, y: 1680, stories: 10 },
    mech100: { x: 980, y: 1700, stories: 100 },
    mech1000: { x: 340, y: 2420, stories: 1000 },
    spawn: { x: 280, y: 1750 },
  };

  var TRACK_MAIN = [
    [1780, 2220, 0], [1850, 2080, 0.15], [1940, 1920, 0.45], [2060, 1780, 0.85],
    [2180, 1700, 1.15], [2320, 1660, 1.25], [2480, 1700, 1.05], [2600, 1820, 0.55],
    [2680, 1960, 0.25], [2780, 2040, 0.2], [2920, 1980, 0.35], [3080, 1860, 0.7],
    [3220, 1760, 1.0], [3380, 1800, 0.95], [3520, 1940, 0.55], [3620, 2120, 0.2],
    [3660, 2300, 0], [3600, 2480, -0.15], [3480, 2620, -0.35], [3300, 2740, -0.4],
    [3100, 2820, -0.3], [2880, 2840, -0.15], [2680, 2760, 0.05], [2500, 2620, 0.25],
    [2340, 2520, 0.45], [2180, 2480, 0.35], [2020, 2420, 0.2], [1900, 2340, 0.1],
    [1820, 2280, 0],
  ];
  var TRACK_BRANCH_A = [
    [2200, 2100, 0.3], [2360, 2180, 0.15], [2540, 2280, 0], [2720, 2360, -0.1],
    [2900, 2420, 0], [3080, 2360, 0.2], [3200, 2200, 0.45], [3180, 2040, 0.55],
    [3020, 1960, 0.4], [2820, 2000, 0.25], [2620, 2080, 0.2], [2420, 2120, 0.25],
    [2260, 2100, 0.3],
  ];
  var TRACK_BRANCH_B = [
    [2400, 2400, 0.1], [2560, 2520, -0.1], [2760, 2580, -0.2], [2960, 2520, -0.1],
    [3080, 2380, 0.15], [3000, 2260, 0.35], [2800, 2220, 0.4], [2600, 2280, 0.25],
    [2460, 2360, 0.15],
  ];
  var TRACK_MOUNDS = [
    { x: 2260, y: 1740, r: 160, h: 1.35 },
    { x: 3280, y: 1820, r: 150, h: 1.2 },
    { x: 2460, y: 2480, r: 120, h: 0.7 },
    { x: 3180, y: 2680, r: 130, h: -0.4 },
    { x: 2800, y: 2100, r: 100, h: 0.85 },
  ];
  /* track3: banked turn berms — outer lip elev for monster-track feel */
  var TRACK_BANKS = [
    { x: 2180, y: 1700, r: 200, tilt: 1.05, label: "BANK" },
    { x: 3380, y: 1800, r: 180, tilt: 0.95, label: "BANK" },
    { x: 3600, y: 2300, r: 190, tilt: 1.0, label: "BANK" },
    { x: 3300, y: 2740, r: 175, tilt: 0.9, label: "BANK" },
    { x: 2340, y: 2520, r: 165, tilt: 0.85, label: "BANK" },
    { x: 1900, y: 2200, r: 150, tilt: 0.75, label: "BANK" },
    { x: 2780, y: 2040, r: 140, tilt: 0.7, label: "BANK" },
  ];
  /* track3: big rock obstacles — hard bounce when hit at speed */
  var TRACK_ROCKS = [
    { x: 2080, y: 1980, r: 52, h: 1.05, bounce: 1.85 },
    { x: 2420, y: 1820, r: 60, h: 1.2, bounce: 2.1 },
    { x: 2720, y: 1920, r: 48, h: 0.95, bounce: 1.7 },
    { x: 3140, y: 1980, r: 58, h: 1.15, bounce: 2.0 },
    { x: 3500, y: 2200, r: 54, h: 1.05, bounce: 1.9 },
    { x: 3400, y: 2560, r: 62, h: 1.25, bounce: 2.2 },
    { x: 2920, y: 2680, r: 50, h: 0.9, bounce: 1.65 },
    { x: 2520, y: 2580, r: 56, h: 1.1, bounce: 1.95 },
    { x: 2200, y: 2360, r: 46, h: 0.85, bounce: 1.55 },
    { x: 2680, y: 2240, r: 64, h: 1.3, bounce: 2.25 },
    { x: 3000, y: 2140, r: 44, h: 0.8, bounce: 1.5 },
    { x: 1980, y: 2280, r: 50, h: 0.95, bounce: 1.75 },
  ];
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

  var TRUCK_SPOTS = [
    { id: "james", x: 1880, y: 1720 },
    { id: "jimmy", x: 2080, y: 1720 },
    { id: "bubbles", x: 2280, y: 1720 },
    { id: "rexy", x: 2480, y: 1720 },
    { id: "shared", x: 2180, y: 1880 },
  ];

  var STARSHIP = { x: 360, y: 320, padR: 110 };
  var STARSHIP_APPROACH = [
    [520, 1480], [480, 1200], [430, 900], [390, 620], [360, 400],
  ];

  /* polish7: big zone signs — fade in when approaching (shared labels) */
  var ZONE_SIGNS = [
    { id: "house", label: "HOUSE", x: 380, y: 1630, approach: 460, color: "#fbbf24" },
    { id: "track", label: "TRACK", x: 2780, y: 2270, approach: 560, color: "#a8a29e" },
    { id: "pond", label: "POND", x: 3160, y: 670, approach: 500, color: "#67e8f9" },
    { id: "garage", label: "GARAGE", x: 940, y: 1660, approach: 300, color: "#fdba74" },
    { id: "starship", label: "STARSHIP", x: 360, y: 320, approach: 340, color: "#fde68a" },
  ];

  /* polish7: companion chat one-liners — ONLY existing toast/tip strings (no new scripts) */
  var AI_CHAT = {
    james: ["HOP!", "HOP · truck jump!"],
    jimmy: ["HOP!", "Catch Jimmy · jetpack!"],
    bubbles: ["HOP!", "Splash the pond"],
    rexy: ["HOP!", "Open SPS for Optimus kits"],
  };

  var HOTSPOTS = [
    { id: "phone", label: "Phone", x: 380, y: 1880, r: 52, tip: "Call Purple Bear" },
    { id: "sps", label: "SPS", x: 520, y: 1940, r: 48, tip: "Solar Positioning System" },
    { id: "truck-james", label: "Cybertruck · James", x: 1880, y: 1720, r: 54, tip: "James Cybertruck · solo drive", kind: "truck", frogId: "james", mode: "solo" },
    { id: "truck-jimmy", label: "Cybertruck · Jimmy", x: 2080, y: 1720, r: 54, tip: "Jimmy Cybertruck · solo drive", kind: "truck", frogId: "jimmy", mode: "solo" },
    { id: "truck-bubbles", label: "Cybertruck · Bubbles", x: 2280, y: 1720, r: 54, tip: "Bubbles Cybertruck · solo drive", kind: "truck", frogId: "bubbles", mode: "solo" },
    { id: "truck-rexy", label: "Cybertruck · Rexy", x: 2480, y: 1720, r: 54, tip: "Rexy Cybertruck · solo drive", kind: "truck", frogId: "rexy", mode: "solo" },
    { id: "truck-shared", label: "★ ALL ABOARD · 4 frogs", x: 2180, y: 1880, r: 78, tip: "Shared Cybertruck · all four pile in", kind: "truck", frogId: null, mode: "shared" },
    { id: "mech-10", label: "Board 10-story mech", x: COMPOUND.mech10.x, y: COMPOUND.mech10.y, r: 64, tip: "10-story mech · INTERACT / BOARD", kind: "mech", stories: 10, solidId: "mech10" },
    { id: "mech-100", label: "Board 100-story mech", x: COMPOUND.mech100.x, y: COMPOUND.mech100.y, r: 78, tip: "100-story mech · INTERACT / BOARD", kind: "mech", stories: 100, solidId: "mech100" },
    { id: "mech-1000", label: "Board 1000-story mech", x: COMPOUND.mech1000.x, y: COMPOUND.mech1000.y, r: 120, tip: "1000-story mech · INTERACT / BOARD", kind: "mech", stories: 1000, solidId: "mech1000" },
    { id: "fishies", label: "Fishies", x: 3160, y: 620, r: 70, tip: "Splash the pond" },
    { id: "starship", label: "Starship", x: 360, y: 320, r: 72, tip: "Starship · Spotty · space episode" },
  ];

  var SPACE_CAST = {
    spotty: { name: "Spotty", tip: "Starship commander" },
    jimmy: { name: "Jimmy", tip: "Space-suit jetpack · catch him!" },
    germy: { name: "Germy", tip: "Germy the doggy" },
    daisy: { name: "Daisy Dachshund", tip: "Daisy Dachshund" },
    alex: { name: "Alex", tip: "Alex · astronaut" },
    fred: { name: "Fred", tip: "Fred · astronaut" },
  };

  var ORBIT_PHYSICS = {
    captureRadius: 120,
    softPullRadius: 220,
    orbitAltitude: 78,
    pullAccel: 420,
    hardThrustSpeed: 210,
    hardThrustImpulse: 320,
    leaveModes: ["escape", "hard_thrust"],
  };

  var ENGINE_KEY = "ff-engine";
  var ENGINES = ["canvas", "phaser", "three"];

  function getEngine() {
    try {
      var v = localStorage.getItem(ENGINE_KEY) || "canvas";
      return ENGINES.indexOf(v) >= 0 ? v : "canvas";
    } catch (e) {
      return "canvas";
    }
  }

  function setEngine(id) {
    if (ENGINES.indexOf(id) < 0) id = "canvas";
    try { localStorage.setItem(ENGINE_KEY, id); } catch (e) {}
    return id;
  }

  function nearestHotspot(x, y, maxR) {
    var best = null;
    var bestD = maxR || 70;
    for (var i = 0; i < HOTSPOTS.length; i++) {
      var h = HOTSPOTS[i];
      var d = Math.hypot(h.x - x, h.y - y);
      var reach = Math.max(bestD, h.r || 70);
      if (d < reach && d < (best ? Math.hypot(best.x - x, best.y - y) : reach)) {
        bestD = d;
        best = h;
      }
    }
    return best;
  }

  function areaNameAt(x, y) {
    for (var i = 0; i < AREAS.length; i++) {
      var a = AREAS[i];
      if (x >= a.x && x <= a.x + a.w && y >= a.y && y <= a.y + a.h) return a.name;
    }
    return "Ranch grounds";
  }

  function inPond(x, y) {
    var a = AREAS[2];
    var pad = 40;
    return x >= a.x + pad && x <= a.x + a.w - pad &&
           y >= a.y + pad && y <= a.y + a.h - pad;
  }

  function onTrack(x, y) {
    var a = AREAS[1];
    return x >= a.x && x <= a.x + a.w && y >= a.y && y <= a.y + a.h;
  }

  function isTruckHotspot(h) {
    return !!(h && (h.kind === "truck" || (h.id && String(h.id).indexOf("truck") === 0)));
  }

  function isMechHotspot(h) {
    return !!(h && (h.kind === "mech" || (h.id && String(h.id).indexOf("mech") === 0)));
  }

  function mechSolidId(idOrHot) {
    var id = idOrHot && typeof idOrHot === "object" ? idOrHot.solidId || idOrHot.id : idOrHot;
    if (!id) return null;
    id = String(id);
    if (id === "mech-10" || id === "mech10") return "mech10";
    if (id === "mech-100" || id === "mech100") return "mech100";
    if (id === "mech-1000" || id === "mech1000") return "mech1000";
    return id.indexOf("mech") === 0 ? id.replace(/^mech-/, "mech") : null;
  }

  function rampAt(x, y) {
    for (var i = 0; i < RAMPS.length; i++) {
      var r = RAMPS[i];
      if (x >= r.x - r.w * 0.5 && x <= r.x + r.w * 0.5 &&
          y >= r.y - r.h * 0.5 && y <= r.y + r.h * 0.5) return r;
    }
    return null;
  }

  /* truck1: kid-toy Cybertruck vs frog — bigger than a frog, not a building.
     Applied by each engine renderer (parked + driven). */
  var TRUCK_VIS = {
    canvasScale: 0.86,
    threeScale: 2.05,
    phaserScale: 1.28,
  };

  /* track3: live monster-truck wheel scale (1 = stock Cybertruck, max = huge). */
  var WHEEL_SCALE = { min: 1, max: 2.65, default: 1, step: 0.08 };
  var wheelScaleLive = WHEEL_SCALE.default;

  function clampWheel(v) {
    if (v < WHEEL_SCALE.min) return WHEEL_SCALE.min;
    if (v > WHEEL_SCALE.max) return WHEEL_SCALE.max;
    return v;
  }
  function getWheelScale() { return wheelScaleLive; }
  function setWheelScale(v) {
    wheelScaleLive = clampWheel(typeof v === "number" ? v : WHEEL_SCALE.default);
    return wheelScaleLive;
  }
  function adjustWheelScale(delta) {
    return setWheelScale(wheelScaleLive + (delta || 0));
  }
  /* Clearance / bounce helpers for engines */
  function wheelClearanceZ(ws) {
    ws = ws != null ? ws : wheelScaleLive;
    return (ws - 1) * 18;
  }
  function wheelBounceMul(ws) {
    ws = ws != null ? ws : wheelScaleLive;
    return 0.85 + ws * 0.55;
  }
  function wheelJumpMul(ws) {
    ws = ws != null ? ws : wheelScaleLive;
    return 0.9 + (ws - 1) * 0.55;
  }

  /* truck2: sample ribbon elev (pt[2]) + mound bells + ramp wedges → frog.z units.
     Stronger contact so crest → airtime → land bounce is obvious on all engines.
     track3: + bank berms + rock bumps. */
  var TRACK_ELEV_Z = 78;

  function samplePathElev(pts, x, y) {
    var bestD = 1e12, bestE = 0;
    if (!pts || pts.length < 2) return { elev: 0, dist: bestD };
    for (var i = 0; i < pts.length - 1; i++) {
      var ax = pts[i][0], ay = pts[i][1], ae = pts[i][2] || 0;
      var bx = pts[i + 1][0], by = pts[i + 1][1], be = pts[i + 1][2] || 0;
      var abx = bx - ax, aby = by - ay;
      var len2 = abx * abx + aby * aby || 1;
      var u = ((x - ax) * abx + (y - ay) * aby) / len2;
      if (u < 0) u = 0; else if (u > 1) u = 1;
      var px = ax + abx * u, py = ay + aby * u;
      var d = Math.hypot(x - px, y - py);
      if (d < bestD) {
        bestD = d;
        bestE = ae + (be - ae) * u;
      }
    }
    return { elev: bestE, dist: bestD };
  }

  /* Ramp wedge height in elev units (0..~1.4) — ride up geometry, not clip through. */
  function rampElevAt(x, y) {
    var best = 0;
    for (var i = 0; i < RAMPS.length; i++) {
      var r = RAMPS[i];
      var hw = r.w * 0.55, hh = r.h * 0.55;
      var dx = Math.abs(x - r.x), dy = Math.abs(y - r.y);
      if (dx > hw || dy > hh) continue;
      var nx = 1 - dx / hw, ny = 1 - dy / hh;
      /* Wedge: low at south edge, crest at north — matches track jump lips */
      var along = (r.y + hh - y) / (hh * 2);
      if (along < 0) along = 0; else if (along > 1) along = 1;
      var tent = nx * ny;
      var peak = 0.75 + (r.boost || 1.3) * 0.45;
      var h = tent * (0.25 + along * peak);
      if (h > best) best = h;
    }
    return best;
  }

  /* track3: bank berm elev — crown toward center of bank pad */
  function bankElevAt(x, y) {
    var best = 0;
    for (var i = 0; i < TRACK_BANKS.length; i++) {
      var b = TRACK_BANKS[i];
      var d = Math.hypot(x - b.x, y - b.y);
      if (d >= b.r) continue;
      var w = 1 - d / b.r;
      /* Outer lip higher — ring-ish berm (peak ~0.55–0.85 radius) */
      var ring = Math.sin(Math.min(1, d / b.r) * Math.PI);
      var h = (b.tilt || 0.8) * (0.35 * w + 0.75 * ring * w);
      if (h > best) best = h;
    }
    return best;
  }

  function rockElevAt(x, y) {
    var best = 0;
    for (var i = 0; i < TRACK_ROCKS.length; i++) {
      var rk = TRACK_ROCKS[i];
      var d = Math.hypot(x - rk.x, y - rk.y);
      var rr = rk.r * 1.15;
      if (d >= rr) continue;
      var w = 1 - d / rr;
      var h = (rk.h || 1) * (0.2 * w + 0.95 * w * w * w);
      if (h > best) best = h;
    }
    return best;
  }

  /* Nearest rock under truck footprint — for hard bounce jolts */
  function rockHitAt(x, y, reach) {
    reach = reach != null ? reach : 8;
    var best = null, bestD = 1e12;
    for (var i = 0; i < TRACK_ROCKS.length; i++) {
      var rk = TRACK_ROCKS[i];
      var d = Math.hypot(x - rk.x, y - rk.y);
      var hitR = rk.r + reach;
      if (d < hitR && d < bestD) {
        bestD = d;
        best = rk;
      }
    }
    return best;
  }

  function trackElevAt(x, y) {
    if (!onTrack(x, y)) return 0;
    var a = samplePathElev(TRACK_MAIN, x, y);
    var b = samplePathElev(TRACK_BRANCH_A, x, y);
    var c = samplePathElev(TRACK_BRANCH_B, x, y);
    var best = a;
    if (b.dist < best.dist) best = b;
    if (c.dist < best.dist) best = c;
    var pathE = 0;
    /* Wider ribbon influence so truck stays on hills across apron */
    if (best.dist < 210) {
      pathE = best.elev;
      if (best.dist > 28) pathE *= Math.max(0, 1 - (best.dist - 28) / 182);
    }
    var moundE = 0;
    for (var i = 0; i < TRACK_MOUNDS.length; i++) {
      var m = TRACK_MOUNDS[i];
      var d = Math.hypot(x - m.x, y - m.y);
      if (d < m.r) {
        var w = 1 - d / m.r;
        /* Bell + soft skirt — obvious rise approaching the hill */
        moundE += m.h * (0.25 * w + 0.75 * w * w);
      }
    }
    var rampE = rampElevAt(x, y);
    var bankE = bankElevAt(x, y);
    var rockE = rockElevAt(x, y);
    return (pathE * 1.05 + moundE * 0.95 + rampE * 1.15 + bankE * 1.05 + rockE * 1.25) * TRACK_ELEV_Z;
  }

  function wrapAngle(a) {
    while (a > Math.PI) a -= Math.PI * 2;
    while (a < -Math.PI) a += Math.PI * 2;
    return a;
  }

  function approachAngle(cur, aim, maxStep) {
    var d = wrapAngle(aim - cur);
    if (d > maxStep) d = maxStep;
    if (d < -maxStep) d = -maxStep;
    return cur + d;
  }

  /* solid1: cheap walk blockers (world XY). Doorways stay open. Trucks block unless boarding/in-truck. */
  var WALL_THICK = 20;
  var HOUSE_DOOR_W = 96;
  var GARAGE_DOOR_W = 440; /* garage1: nearly full bay (gar.w=480) */

  function solidRects(opts) {
    opts = opts || {};
    var garageOpen = opts.garageOpen || 0;
    var out = [];
    var house = COMPOUND.house;
    var t = WALL_THICK;
    var midX = house.x + house.w * 0.5;
    var gap = HOUSE_DOOR_W * 0.5;
    /* North / West / East full; South split around doorway (walkable gap) */
    out.push({ id: "house-n", x: house.x, y: house.y - t * 0.5, w: house.w, h: t });
    out.push({ id: "house-w", x: house.x - t * 0.5, y: house.y, w: t, h: house.h });
    out.push({ id: "house-e", x: house.x + house.w - t * 0.5, y: house.y, w: t, h: house.h });
    out.push({ id: "house-sl", x: house.x, y: house.y + house.h - t * 0.5, w: Math.max(8, midX - gap - house.x), h: t });
    out.push({ id: "house-sr", x: midX + gap, y: house.y + house.h - t * 0.5, w: Math.max(8, house.x + house.w - (midX + gap)), h: t });

    var gar = COMPOUND.garage;
    out.push({ id: "gar-n", x: gar.x, y: gar.y - t * 0.5, w: gar.w, h: t });
    out.push({ id: "gar-w", x: gar.x - t * 0.5, y: gar.y, w: t, h: gar.h });
    out.push({ id: "gar-e", x: gar.x + gar.w - t * 0.5, y: gar.y, w: t, h: gar.h });
    /* Rolling door blocks south bay until open enough to walk under */
    if (garageOpen < 0.45) {
      /* garage1: door fills south bay wall-to-wall (tiny side margins for jambs) */
      var margin = Math.max(8, (gar.w - GARAGE_DOOR_W) * 0.5);
      out.push({ id: "gar-door", x: gar.x + margin, y: gar.y + gar.h - t * 0.5, w: gar.w - margin * 2, h: t });
    }
    return out;
  }

  function solidCircles(opts) {
    opts = opts || {};
    var out = [];
    var ignoreMech = opts.ignoreMechId || null;
    var mechs = [
      { id: "mech10", x: COMPOUND.mech10.x, y: COMPOUND.mech10.y, r: 38 },
      { id: "mech100", x: COMPOUND.mech100.x, y: COMPOUND.mech100.y, r: 52 },
      { id: "mech1000", x: COMPOUND.mech1000.x, y: COMPOUND.mech1000.y, r: 95 },
    ];
    for (var mi = 0; mi < mechs.length; mi++) {
      if (ignoreMech && mechs[mi].id === ignoreMech) continue;
      out.push(mechs[mi]);
    }
    if (!opts.inTruck && !opts.ignoreTrucks) {
      for (var i = 0; i < TRUCK_SPOTS.length; i++) {
        var s = TRUCK_SPOTS[i];
        /* r under hotspot radius so BOARD shell stays reachable (solo ~54, shared ~78) */
        out.push({ id: "truck-" + s.id, x: s.x, y: s.y, r: s.id === "shared" ? 40 : 28 });
      }
    }
    return out;
  }

  function _pushCircleOut(pos, cx, cy, minDist) {
    var dx = pos.x - cx, dy = pos.y - cy;
    var d = Math.hypot(dx, dy);
    if (d < 1e-4) { pos.x = cx + minDist; return true; }
    if (d < minDist) {
      var s = minDist / d;
      pos.x = cx + dx * s;
      pos.y = cy + dy * s;
      return true;
    }
    return false;
  }

  function _pushRectOut(pos, r, rad) {
    var cx = Math.max(r.x, Math.min(r.x + r.w, pos.x));
    var cy = Math.max(r.y, Math.min(r.y + r.h, pos.y));
    var dx = pos.x - cx, dy = pos.y - cy;
    var d2 = dx * dx + dy * dy;
    if (d2 >= rad * rad) return false;
    if (d2 < 1e-6) {
      /* Deep inside: push via nearest edge */
      var left = pos.x - r.x, right = r.x + r.w - pos.x;
      var top = pos.y - r.y, bot = r.y + r.h - pos.y;
      var m = Math.min(left, right, top, bot);
      if (m === left) pos.x = r.x - rad;
      else if (m === right) pos.x = r.x + r.w + rad;
      else if (m === top) pos.y = r.y - rad;
      else pos.y = r.y + r.h + rad;
      return true;
    }
    var d = Math.sqrt(d2);
    var s = rad / d;
    pos.x = cx + dx * s;
    pos.y = cy + dy * s;
    return true;
  }

  function resolveSolid(x, y, radius, opts) {
    opts = opts || {};
    var rad = radius || 22;
    var pos = { x: x, y: y };
    var hit = false;
    var rects = solidRects(opts);
    for (var i = 0; i < rects.length; i++) {
      if (_pushRectOut(pos, rects[i], rad)) hit = true;
    }
    var circs = solidCircles(opts);
    for (var j = 0; j < circs.length; j++) {
      if (_pushCircleOut(pos, circs[j].x, circs[j].y, circs[j].r + rad)) hit = true;
    }
    /* Optional soft pond rim — gentle slide, not a hard wall */
    if (opts.softPond && !opts.inTruck && !opts.inMech) {
      var pond = AREAS[2];
      var margin = 36;
      var inside =
        pos.x > pond.x + margin && pos.x < pond.x + pond.w - margin &&
        pos.y > pond.y + margin && pos.y < pond.y + pond.h - margin;
      if (inside) {
        var dl = pos.x - (pond.x + margin);
        var dr = (pond.x + pond.w - margin) - pos.x;
        var dt = pos.y - (pond.y + margin);
        var db = (pond.y + pond.h - margin) - pos.y;
        var m = Math.min(dl, dr, dt, db);
        var push = 28 * (opts.softPondStrength || 0.35);
        if (m === dl) pos.x -= push;
        else if (m === dr) pos.x += push;
        else if (m === dt) pos.y -= push;
        else pos.y += push;
        hit = true;
      }
    }
    return { x: pos.x, y: pos.y, hit: hit };
  }

  /* polish10: peak mid-approach; fade when standing on the sign so frogs stay visible */
  function zoneSignAlpha(sign, x, y) {
    if (!sign) return 0;
    var d = Math.hypot((sign.x || 0) - x, (sign.y || 0) - y);
    var R = sign.approach || 400;
    if (d >= R) return 0;
    if (d <= R * 0.16) return 0.18 * (d / Math.max(1, R * 0.16));
    if (d <= R * 0.42) return 0.55 + 0.2 * ((d - R * 0.16) / (R * 0.26));
    return 0.75 * (1 - (d - R * 0.42) / (R * 0.58));
  }


  /* hop4: continuous ranch foot hop — higher launch + shorter plant (engines pass carry).
     opts: moving, zKey, zvKey, gndKey|ground, up, lift, groundHold, groundEps */
  function tickLocoHop(ent, dt, opts) {
    opts = opts || {};
    if (!ent || ent.inTruck || ent.inMech) return false;
    var moving = !!opts.moving;
    var zKey = opts.zKey || "z";
    var zvKey = opts.zvKey || "zVel";
    var gnd = opts.ground != null ? opts.ground
      : (opts.gndKey && ent[opts.gndKey] != null ? ent[opts.gndKey]
        : (ent.groundZ != null ? ent.groundZ : 0));
    var up = opts.up != null ? opts.up : 250;
    var lift = opts.lift != null ? opts.lift : 8;
    var hold = opts.groundHold != null ? opts.groundHold : 0.012;
    var eps = opts.groundEps != null ? opts.groundEps : 1.2;
    var z = ent[zKey] || 0;
    var zv = ent[zvKey] || 0;
    var onGround = z <= gnd + eps && zv <= Math.max(12, up * 0.08);
    if (onGround) {
      ent.hopLandT = (ent.hopLandT != null ? ent.hopLandT : 0) + dt;
      if (ent.hopLandT > 0.16) ent.hopCombo = 0;
    }
    if (!moving) {
      ent.hopGroundT = 0;
      return false;
    }
    if (!onGround) {
      ent.hopGroundT = 0;
      return false;
    }
    ent.hopGroundT = (ent.hopGroundT || 0) + dt;
    if (ent.hopGroundT < hold) return false;
    ent[zvKey] = Math.max(zv, up);
    ent[zKey] = Math.max(z, gnd + lift);
    ent.hopStretch = Math.max(ent.hopStretch || 0, 0.9);
    ent.hopGroundT = 0;
    return true;
  }

  /* hop3: ability HOP — forward carry + airborne/land-window combo stack height.
     Stack: each air (or ≤landWindow after land) ability hop adds height; soft-cap ~10.
     opts: up, fwd, truckUp, truckFwd, ang, zKey, zvKey, landWindow, maxCombo, airEps, gndKey */
  function applyHop(ent, opts) {
    opts = opts || {};
    var ang = (ent.faceAngle != null && isFinite(ent.faceAngle))
      ? ent.faceAngle
      : (ent.facing >= 0 ? 0 : Math.PI);
    if (opts.ang != null && isFinite(opts.ang)) ang = opts.ang;
    var cx = Math.cos(ang), cy = Math.sin(ang);
    var inTruck = !!ent.inTruck;
    var up = inTruck ? (opts.truckUp != null ? opts.truckUp : 260) : (opts.up != null ? opts.up : 320);
    var fwd = inTruck ? (opts.truckFwd != null ? opts.truckFwd : 220) : (opts.fwd != null ? opts.fwd : 175);
    var zKey = opts.zKey || "z";
    var zvKey = opts.zvKey || "zVel";
    var gndKey = opts.gndKey || "groundZ";
    var gnd = ent[gndKey] != null ? ent[gndKey] : (ent.groundZ != null ? ent.groundZ : 0);
    var z = ent[zKey] || 0;
    var zv = ent[zvKey] || 0;
    var airEps = opts.airEps != null ? opts.airEps : 3;
    var landWin = opts.landWindow != null ? opts.landWindow : 0.15;
    var maxCombo = opts.maxCombo != null ? opts.maxCombo : 10;
    var airborne = z > gnd + airEps;
    var landAge = ent.hopLandT != null ? ent.hopLandT : 999;
    var canStack = airborne || landAge <= landWin;
    if (canStack) {
      ent.hopCombo = Math.min(maxCombo, (ent.hopCombo || 0) + 1);
    } else {
      ent.hopCombo = 1;
    }
    var combo = ent.hopCombo || 1;
    /* Diminishing stack bonus — ~5–10 hops climb big; soft ceiling after maxCombo */
    var stackBonus = 0;
    for (var si = 1; si < combo; si++) {
      stackBonus += up * (0.30 * Math.pow(0.86, si - 1));
    }
    var totalUp = up + stackBonus;
    if (airborne) {
      /* Bunny-hop: cancel fall into upward, then add stacked lift */
      ent[zvKey] = Math.max(0, zv) + totalUp * (0.68 + 0.02 * Math.min(combo, 8));
    } else {
      ent[zvKey] = Math.max(zv, totalUp);
      ent[zKey] = Math.max(z, gnd + 5);
    }
    ent.hopLandT = 999;
    ent.vx = (ent.vx || 0) + cx * fwd;
    ent.vy = (ent.vy || 0) + cy * fwd;
    ent.hopStretch = 1;
    ent.dashTrail = Math.max(ent.dashTrail || 0, 0.4);
    ent.invuln = Math.max(ent.invuln || 0, 0.18);
    return { ang: ang, cx: cx, cy: cy, up: totalUp, fwd: fwd, combo: combo };
  }

  /* hop3: call on land to open the short combo land-window */
  function noteHopLand(ent) {
    if (!ent) return;
    ent.hopLandT = 0;
  }

  /* hop1: shove small movable props (toys / little animals / pollen) — not walls/trucks/mechs */
  function shoveSmallProp(prop, frogX, frogY, frogR, frogVx, frogVy, opts) {
    if (!prop) return false;
    opts = opts || {};
    var pr = prop.r != null ? prop.r : (opts.propR != null ? opts.propR : 10);
    var fr = frogR != null ? frogR : 22;
    var dx = (prop.x || 0) - frogX;
    var dy = (prop.y || 0) - frogY;
    var dist = Math.hypot(dx, dy);
    var minD = pr + fr;
    if (dist >= minD) return false;
    if (dist < 1e-4) {
      var a = Math.random() * Math.PI * 2;
      dx = Math.cos(a); dy = Math.sin(a); dist = 1;
    }
    var nx = dx / dist, ny = dy / dist;
    var overlap = minD - dist;
    prop.x = (prop.x || 0) + nx * overlap * 0.9;
    prop.y = (prop.y || 0) + ny * overlap * 0.9;
    var strength = opts.strength != null ? opts.strength : 1;
    var base = 95 + Math.hypot(frogVx || 0, frogVy || 0) * 0.6;
    var impulse = base * strength;
    prop.vx = (prop.vx || 0) + nx * impulse + (frogVx || 0) * 0.38;
    prop.vy = (prop.vy || 0) + ny * impulse + (frogVy || 0) * 0.38;
    var maxV = opts.maxV != null ? opts.maxV : 340;
    var sp = Math.hypot(prop.vx, prop.vy);
    if (sp > maxV) { prop.vx = (prop.vx / sp) * maxV; prop.vy = (prop.vy / sp) * maxV; }
    return true;
  }

  function tickPushable(prop, dt, opts) {
    if (!prop) return;
    opts = opts || {};
    var fric = opts.friction != null ? opts.friction : 4.8;
    prop.vx = prop.vx || 0;
    prop.vy = prop.vy || 0;
    prop.x = (prop.x || 0) + prop.vx * dt;
    prop.y = (prop.y || 0) + prop.vy * dt;
    var damp = Math.exp(-fric * dt);
    prop.vx *= damp;
    prop.vy *= damp;
    var bounce = opts.bounce != null ? opts.bounce : 0.38;
    if (opts.bounds) {
      var b = opts.bounds;
      if (prop.x < b.x0) { prop.x = b.x0; prop.vx = Math.abs(prop.vx) * bounce; }
      if (prop.x > b.x1) { prop.x = b.x1; prop.vx = -Math.abs(prop.vx) * bounce; }
      if (prop.y < b.y0) { prop.y = b.y0; prop.vy = Math.abs(prop.vy) * bounce; }
      if (prop.y > b.y1) { prop.y = b.y1; prop.vy = -Math.abs(prop.vy) * bounce; }
    }
    if (Math.hypot(prop.vx, prop.vy) < 5) { prop.vx = 0; prop.vy = 0; }
  }

  global.FroggiesCanon = {
    FROG_ORDER: FROG_ORDER,
    FROG_DEFS: FROG_DEFS,
    MAP_W: MAP_W,
    MAP_H: MAP_H,
    AREAS: AREAS,
    COMPOUND: COMPOUND,
    TRACK_MAIN: TRACK_MAIN,
    TRACK_BRANCH_A: TRACK_BRANCH_A,
    TRACK_BRANCH_B: TRACK_BRANCH_B,
    TRACK_MOUNDS: TRACK_MOUNDS,
    TRACK_BANKS: TRACK_BANKS,
    TRACK_ROCKS: TRACK_ROCKS,
    RAMPS: RAMPS,
    TRUCK_SPOTS: TRUCK_SPOTS,
    STARSHIP: STARSHIP,
    STARSHIP_APPROACH: STARSHIP_APPROACH,
    ZONE_SIGNS: ZONE_SIGNS,
    AI_CHAT: AI_CHAT,
    HOTSPOTS: HOTSPOTS,
    SPACE_CAST: SPACE_CAST,
    ORBIT_PHYSICS: ORBIT_PHYSICS,
    ENGINE_KEY: ENGINE_KEY,
    ENGINES: ENGINES,
    getEngine: getEngine,
    setEngine: setEngine,
    nearestHotspot: nearestHotspot,
    areaNameAt: areaNameAt,
    inPond: inPond,
    onTrack: onTrack,
    isTruckHotspot: isTruckHotspot,
    isMechHotspot: isMechHotspot,
    mechSolidId: mechSolidId,
    rampAt: rampAt,
    rampElevAt: rampElevAt,
    bankElevAt: bankElevAt,
    rockElevAt: rockElevAt,
    rockHitAt: rockHitAt,
    trackElevAt: trackElevAt,
    TRUCK_VIS: TRUCK_VIS,
    WHEEL_SCALE: WHEEL_SCALE,
    getWheelScale: getWheelScale,
    setWheelScale: setWheelScale,
    adjustWheelScale: adjustWheelScale,
    wheelClearanceZ: wheelClearanceZ,
    wheelBounceMul: wheelBounceMul,
    wheelJumpMul: wheelJumpMul,
    wrapAngle: wrapAngle,
    approachAngle: approachAngle,
    zoneSignAlpha: zoneSignAlpha,
    solidRects: solidRects,
    solidCircles: solidCircles,
    resolveSolid: resolveSolid,
    applyHop: applyHop,
    tickLocoHop: tickLocoHop,
    noteHopLand: noteHopLand,
    shoveSmallProp: shoveSmallProp,
    tickPushable: tickPushable,
  };
})(typeof window !== "undefined" ? window : globalThis);
