/* Four Froggies — shared Ben-canon constants for all engines.
   WORLD_BIBLE only. No invented cast/zone/toy names. Physics/look engines compare. */
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
    /* James ranch compound: big house + backyard + huge garage (one Ben-named area) */
    { id: "house", name: "Ranch house", x: 60, y: 1320, w: 1180, h: 1180, color: "#8b5a2b" },
    { id: "track", name: "Monster truck track", x: 1680, y: 1580, w: 2200, h: 1380, color: "#57534e" },
    { id: "pond", name: "Pond", x: 2320, y: 80, w: 1680, h: 1180, color: "#0e7490" },
  ];

  var HOTSPOTS = [
    { id: "phone", label: "Phone", x: 380, y: 1880, r: 52, tip: "Call Purple Bear" },
    { id: "sps", label: "SPS", x: 520, y: 1940, r: 48, tip: "Solar Positioning System" },
    { id: "truck-james", label: "Cybertruck · James", x: 1880, y: 1720, r: 54, tip: "James Cybertruck · solo drive", kind: "truck", frogId: "james", mode: "solo" },
    { id: "truck-jimmy", label: "Cybertruck · Jimmy", x: 2080, y: 1720, r: 54, tip: "Jimmy Cybertruck · solo drive", kind: "truck", frogId: "jimmy", mode: "solo" },
    { id: "truck-bubbles", label: "Cybertruck · Bubbles", x: 2280, y: 1720, r: 54, tip: "Bubbles Cybertruck · solo drive", kind: "truck", frogId: "bubbles", mode: "solo" },
    { id: "truck-rexy", label: "Cybertruck · Rexy", x: 2480, y: 1720, r: 54, tip: "Rexy Cybertruck · solo drive", kind: "truck", frogId: "rexy", mode: "solo" },
    { id: "truck-shared", label: "Cybertruck · all aboard", x: 2180, y: 1880, r: 62, tip: "All four pile into one Cybertruck", kind: "truck", frogId: null, mode: "shared" },
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

  /* Ben physics (ALL engines): near a planet → gravity pull into orbit.
     Leave only via Escape key/button OR hard thruster push (ability jet). */
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
    try {
      localStorage.setItem(ENGINE_KEY, id);
    } catch (e) { /* ignore */ }
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

  global.FroggiesCanon = {
    FROG_ORDER: FROG_ORDER,
    FROG_DEFS: FROG_DEFS,
    MAP_W: MAP_W,
    MAP_H: MAP_H,
    AREAS: AREAS,
    HOTSPOTS: HOTSPOTS,
    SPACE_CAST: SPACE_CAST,
    ORBIT_PHYSICS: ORBIT_PHYSICS,
    ENGINE_KEY: ENGINE_KEY,
    ENGINES: ENGINES,
    getEngine: getEngine,
    setEngine: setEngine,
    nearestHotspot: nearestHotspot,
    areaNameAt: areaNameAt,
  };
})(typeof window !== "undefined" ? window : globalThis);
