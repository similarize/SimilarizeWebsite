/* Four Froggies — shared Ben-canon constants for all engines.
   WORLD_BIBLE only. No invented cast/zone/toy names. Physics/look engines compare.
   parity1: landmark layout (compound / track / pond / trucks) shared for Canvas + Phaser + three.
   polish7: zone signs (HOUSE/TRACK/POND/GARAGE/STARSHIP) + AI chat one-liners from existing canon only.
   polish8: shared landmarks unchanged; art punch lives in engine renderers.
   polish9: landmarks unchanged; party/nameplate/gate/kit punch in engines.
   polish10: zone signs fade when close (frogs readable). */
(function (global) {
  "use strict";

  var FROG_ORDER = ["james", "jimmy", "bubbles", "rexy"];

  var FROG_DEFS = {
    james: { id: "james", name: "James", role: "Wheel", color: "#4ade80", accent: "#166534", hat: "#facc15", ability: "DASH" },
    jimmy: { id: "jimmy", name: "Jimmy", role: "Shield", color: "#fb923c", accent: "#9a3412", hat: "#ef4444", ability: "SHIELD" },
    bubbles: { id: "bubbles", name: "Bubbles", role: "Zap", color: "#60a5fa", accent: "#1e3a8a", hat: "#38bdf8", ability: "ZAP" },
    rexy: { id: "rexy", name: "Rexy", role: "Bot", color: "#c084fc", accent: "#6b21a8", hat: "#e879f9", ability: "BOT" },
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
    { x: 2260, y: 1740, r: 160, h: 1.2 },
    { x: 3280, y: 1820, r: 140, h: 1.05 },
    { x: 2460, y: 2480, r: 110, h: 0.55 },
    { x: 3180, y: 2680, r: 130, h: -0.35 },
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
    james: ["DASH!", "DASH · truck boost!"],
    jimmy: ["SHIELD up!", "Catch Jimmy · jetpack!"],
    bubbles: ["ZAP!", "Splash the pond"],
    rexy: ["BOT · open SPS for Optimus kits"],
  };

  var HOTSPOTS = [
    { id: "phone", label: "Phone", x: 380, y: 1880, r: 52, tip: "Call Purple Bear" },
    { id: "sps", label: "SPS", x: 520, y: 1940, r: 48, tip: "Solar Positioning System" },
    { id: "truck-james", label: "Cybertruck · James", x: 1880, y: 1720, r: 54, tip: "James Cybertruck · solo drive", kind: "truck", frogId: "james", mode: "solo" },
    { id: "truck-jimmy", label: "Cybertruck · Jimmy", x: 2080, y: 1720, r: 54, tip: "Jimmy Cybertruck · solo drive", kind: "truck", frogId: "jimmy", mode: "solo" },
    { id: "truck-bubbles", label: "Cybertruck · Bubbles", x: 2280, y: 1720, r: 54, tip: "Bubbles Cybertruck · solo drive", kind: "truck", frogId: "bubbles", mode: "solo" },
    { id: "truck-rexy", label: "Cybertruck · Rexy", x: 2480, y: 1720, r: 54, tip: "Rexy Cybertruck · solo drive", kind: "truck", frogId: "rexy", mode: "solo" },
    { id: "truck-shared", label: "★ ALL ABOARD · 4 frogs", x: 2180, y: 1880, r: 78, tip: "Shared Cybertruck · all four pile in", kind: "truck", frogId: null, mode: "shared" },
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

  function rampAt(x, y) {
    for (var i = 0; i < RAMPS.length; i++) {
      var r = RAMPS[i];
      if (x >= r.x - r.w * 0.5 && x <= r.x + r.w * 0.5 &&
          y >= r.y - r.h * 0.5 && y <= r.y + r.h * 0.5) return r;
    }
    return null;
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
    rampAt: rampAt,
    zoneSignAlpha: zoneSignAlpha,
  };
})(typeof window !== "undefined" ? window : globalThis);
