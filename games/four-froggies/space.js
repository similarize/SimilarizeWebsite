/* Four Froggies — space episode (story path INSIDE the ranch cab).
   Presentation polish: depth-scaled sprites, parallax stars, vignette (original).
   Ben cast only: Spotty, Alex, Fred, Germy, Daisy Dachshund, King Germy;
   ~20 people + hundreds of dogs as anonymous crowds.
   Real moons: Mars Phobos/Deimos; Neptune's 14 named moons (picker stub).
   Ben orbit physics: near planet → gravity pull into orbit; leave via Escape OR hard thruster.
   polish5: clearer moons picker, readable orbit pull rings, Escape/thruster leave banner,
   Spotty/Alex/Fred presence pulse (cast already stubbed).
   polish6: distant invader mech silhouettes when near Mars (visual tease only).
   polish7: Mars cave entrance tease (3-level + back-door labeled hooks); Jimmy jetpack escape visual on ability.
   polish8: clearer Moon / Mars moons / Neptune moons labels; soft destination beacon when heading toward a body.
   polish9: Germy / Daisy / King Germy presence markers; soft hundreds-of-dogs silhouette flock near Mars cave.
   earth1: space scenes show procedural Earth (home planet) — ranch grounds stay on Earth, not floating in vacuum.
   solarsys1: clear Solar System — Sun center; Moon+station orbit Earth; all planets gravity wells;
   compressed scale labeled; asteroid belt beyond Mars; Pluto included (dwarf).
   spacefix1: opaque dark starfield+ground plane (no forest leak); orbit locks cam on planet
   so only froggy orbits; ranch pad grounded on Earth surface; shared blast-off (all 4);
   ship vs spacesuit modes; space zoom; Mars destination travel. */
(function (global) {
  "use strict";

  var MAP = 1600;
  var SUN_X = 800;
  var SUN_Y = 800;

  /** Compressed layout (not to scale — HUD labels say so). Dist = px from Sun. */
  var SOLAR_DEFS = [
    { id: "sun", name: "Sun", dist: 0, baseAngle: 0, period: 0, r: 92, color: "#fbbf24", glow: "#f59e0b", capture: false, kind: "star" },
    { id: "mercury", name: "Mercury", dist: 145, baseAngle: 0.5, period: 22, r: 12, color: "#a8a29e", capture: true, kind: "planet", soft: 95, cap: 52 },
    { id: "venus", name: "Venus", dist: 200, baseAngle: 2.1, period: 34, r: 18, color: "#eab308", capture: true, kind: "planet", soft: 115, cap: 62 },
    { id: "earth", name: "Earth", dist: 270, baseAngle: 0.15, period: 48, r: 30, color: "#0369a1", capture: true, kind: "planet", soft: 150, cap: 80, home: true },
    { id: "mars", name: "Mars", dist: 355, baseAngle: 2.7, period: 68, r: 22, color: "#b45309", capture: true, kind: "planet", soft: 130, cap: 72 },
    { id: "jupiter", name: "Jupiter", dist: 500, baseAngle: 4.1, period: 110, r: 52, color: "#d97706", capture: true, kind: "planet", soft: 200, cap: 110 },
    { id: "saturn", name: "Saturn", dist: 600, baseAngle: 5.3, period: 140, r: 42, color: "#f59e0b", capture: true, kind: "planet", soft: 175, cap: 95, rings: true },
    { id: "uranus", name: "Uranus", dist: 690, baseAngle: 1.0, period: 170, r: 30, color: "#67e8f9", capture: true, kind: "planet", soft: 140, cap: 78 },
    { id: "neptune", name: "Neptune", dist: 770, baseAngle: 3.4, period: 200, r: 28, color: "#3b82f6", capture: true, kind: "planet", soft: 135, cap: 75 },
    { id: "pluto", name: "Pluto", dist: 860, baseAngle: 5.9, period: 240, r: 11, color: "#cbd5e1", capture: true, kind: "dwarf", soft: 75, cap: 42 },
  ];
  var MOON_ORBIT_R = 58;
  var STATION_ORBIT_R = 82;
  var ASTEROID_INNER = 400;
  var ASTEROID_OUTER = 455;

  function orbitCfg() {
    var C = global.FroggiesCanon;
    return (C && C.ORBIT_PHYSICS) || {
      captureRadius: 120,
      softPullRadius: 220,
      orbitAltitude: 78,
      pullAccel: 420,
      hardThrustSpeed: 210,
      hardThrustImpulse: 320,
    };
  }

  function solarTime(ep) {
    return (ep && ep.spaceTime) || 0;
  }

  /** Live body positions for the free-fly solar system scene. */
  function solarBodies(ep) {
    var t = solarTime(ep);
    var list = [];
    var earth = null;
    for (var i = 0; i < SOLAR_DEFS.length; i++) {
      var d = SOLAR_DEFS[i];
      var ang = d.baseAngle + (d.period > 0 ? t / d.period : 0);
      var x = SUN_X + Math.cos(ang) * d.dist;
      var y = SUN_Y + Math.sin(ang) * d.dist;
      var body = {
        id: d.id,
        name: d.name,
        x: x,
        y: y,
        r: d.r,
        color: d.color,
        glow: d.glow,
        capture: !!d.capture,
        kind: d.kind,
        soft: d.soft || 0,
        cap: d.cap || 0,
        rings: !!d.rings,
        home: !!d.home,
        dist: d.dist,
        angle: ang,
      };
      list.push(body);
      if (d.id === "earth") earth = body;
    }
    if (earth) {
      var moonAng = t * 0.55 + 0.8;
      list.push({
        id: "moon",
        name: "Moon",
        x: earth.x + Math.cos(moonAng) * MOON_ORBIT_R,
        y: earth.y + Math.sin(moonAng) * MOON_ORBIT_R,
        r: 14,
        color: "#e2e8f0",
        capture: true,
        kind: "moon",
        soft: 100,
        cap: 55,
        parent: "earth",
        angle: moonAng,
      });
      var stAng = t * 0.32 + 2.4;
      list.push({
        id: "station",
        name: "Space station",
        x: earth.x + Math.cos(stAng) * STATION_ORBIT_R,
        y: earth.y + Math.sin(stAng) * STATION_ORBIT_R,
        r: 16,
        color: "#94a3b8",
        capture: false,
        kind: "station",
        soft: 0,
        cap: 0,
        parent: "earth",
        angle: stAng,
      });
    }
    return list;
  }

  function bodyById(ep, id) {
    var bodies = solarBodies(ep);
    for (var i = 0; i < bodies.length; i++) {
      if (bodies[i].id === id) return bodies[i];
    }
    return null;
  }

  function refreshOrbitPlanet(ep) {
    if (!ep || !ep.inOrbit || !ep.orbitPlanet || !ep.orbitPlanet.id) return;
    var live = bodyById(ep, ep.orbitPlanet.id);
    if (!live) return;
    ep.orbitPlanet.x = live.x;
    ep.orbitPlanet.y = live.y;
    ep.orbitPlanet.r = live.r;
    ep.orbitPlanet.soft = live.soft;
    ep.orbitPlanet.cap = live.cap;
  }

  /** Scene planets/moons that can capture into orbit. */
  function planetsFor(ep) {
    if (!ep) return [];
    if (ep.scene === "space") {
      var out = [];
      var bodies = solarBodies(ep);
      for (var i = 0; i < bodies.length; i++) {
        if (bodies[i].capture) out.push(bodies[i]);
      }
      return out;
    }
    if (ep.scene === "mars") {
      return [{ id: "mars", name: "Mars", x: 450, y: 280, r: 90, soft: 220, cap: 120 }];
    }
    return [];
  }

  function resetOrbit(ep) {
    ep.inOrbit = false;
    ep.orbitPlanet = null;
    ep.orbitAngle = 0;
    ep.orbitRadius = 0;
    ep.orbitEscapeCool = 0;
  }

  function tryCaptureOrbit(ep, dt) {
    var cfg = orbitCfg();
    var planets = planetsFor(ep);
    if (!planets.length) return;
    if (ep.inOrbit) return;
    if (ep.orbitEscapeCool > 0) {
      ep.orbitEscapeCool -= dt;
      return;
    }
    var best = null;
    var bestD = 1e9;
    for (var i = 0; i < planets.length; i++) {
      var pl = planets[i];
      var d = Math.hypot(ep.px - pl.x, ep.py - pl.y);
      if (d < bestD) { bestD = d; best = pl; }
    }
    if (!best) return;
    var cfgR = best.soft || cfg.softPullRadius || 220;
    var capR = best.cap || cfg.captureRadius || 120;
    ep.orbitPull = null;
    if (bestD < cfgR && bestD > 8) {
      var ang = Math.atan2(ep.py - best.y, ep.px - best.x);
      var pull = (cfg.pullAccel || 420) * (1 - bestD / cfgR) * dt;
      ep.vx = (ep.vx || 0) - Math.cos(ang) * pull;
      ep.vy = (ep.vy || 0) - Math.sin(ang) * pull;
      /* polish5: expose pull strength for readable rings */
      ep.orbitPull = { planet: best, dist: bestD, soft: cfgR, cap: capR, strength: 1 - bestD / cfgR };
    }
    if (bestD < capR) {
      ep.inOrbit = true;
      ep.orbitPlanet = best;
      ep.orbitAngle = Math.atan2(ep.py - best.y, ep.px - best.x);
      ep.orbitRadius = Math.max((best.r || 40) + 24, Math.min(cfg.orbitAltitude || 78, cfgR * 0.55));
      ep.vx = 0;
      ep.vy = 0;
      ep.orbitPull = null;
      toast(ep, "Orbit locked · " + best.name + " · ESCAPE or Ability thruster to leave", 3.4);
    }
  }

  function tickOrbit(ep, dt, steerX, steerY) {
    if (!ep.inOrbit || !ep.orbitPlanet) return false;
    refreshOrbitPlanet(ep);
    var pl = ep.orbitPlanet;
    var cfg = orbitCfg();
    var softMax = pl.soft || cfg.softPullRadius || 220;
    // Steer adjusts altitude a little; cannot leave by steering alone
    ep.orbitRadius = clamp(
      (ep.orbitRadius || cfg.orbitAltitude) + (steerY || 0) * 40 * dt,
      (pl.r || 40) + 28,
      softMax * 0.85
    );
    ep.orbitAngle += (0.85 + (steerX || 0) * 0.35) * dt;
    ep.px = pl.x + Math.cos(ep.orbitAngle) * ep.orbitRadius;
    ep.py = pl.y + Math.sin(ep.orbitAngle) * ep.orbitRadius;
    ep.vx = 0;
    ep.vy = 0;
    return true;
  }

  /** Leave orbit: Escape button/key OR hard thruster (speed/impulse threshold). */
  function leaveOrbit(ep, reason) {
    if (!ep || !ep.inOrbit) return { ok: false };
    var cfg = orbitCfg();
    var pl = ep.orbitPlanet;
    var ang = ep.orbitAngle || 0;
    ep.inOrbit = false;
    ep.orbitPlanet = null;
    ep.orbitEscapeCool = 1.4;
    // Kick outward
    var kick = cfg.hardThrustImpulse || 320;
    ep.vx = Math.cos(ang) * kick * 0.55;
    ep.vy = Math.sin(ang) * kick * 0.55;
    var name = pl ? pl.name : "planet";
    var how = reason === "escape" ? "Escape" : "Hard thruster";
    toast(ep, how + " · left " + name + " orbit", 2.4);
    return { ok: true, toast: ep.toast, sfx: "jet" };
  }

  function tryHardThrustEscape(ep) {
    if (!ep || !ep.inOrbit) return { ok: false };
    var cfg = orbitCfg();
    // Ability = hard thruster push while orbit-locked
    ep.jet = 0.55;
    return leaveOrbit(ep, "hard_thrust");
  }


  /** Real moons — design truth, not invented fiction. */
  var MARS_MOONS = [
    { id: "phobos", name: "Phobos", kind: "mars" },
    { id: "deimos", name: "Deimos", kind: "mars" },
  ];
  var NEPTUNE_MOONS = [
    { id: "triton", name: "Triton" },
    { id: "nereid", name: "Nereid" },
    { id: "naiad", name: "Naiad" },
    { id: "thalassa", name: "Thalassa" },
    { id: "despina", name: "Despina" },
    { id: "galatea", name: "Galatea" },
    { id: "larissa", name: "Larissa" },
    { id: "proteus", name: "Proteus" },
    { id: "halimede", name: "Halimede" },
    { id: "psamathe", name: "Psamathe" },
    { id: "sao", name: "Sao" },
    { id: "laomedeia", name: "Laomedeia" },
    { id: "neso", name: "Neso" },
    { id: "hippocamp", name: "Hippocamp" },
  ];

  var SCENES = {
    starship: { name: "Starship", bg: "space" },
    space: { name: "Space · Solar System", bg: "space" },
    station: { name: "Space station", bg: "station" },
    solar: { name: "Solar map", bg: "space" },
    mars: { name: "Mars", bg: "mars" },
    cave1: { name: "Mars cave · level 1", bg: "cave" },
    cave2: { name: "Mars cave · level 2", bg: "cave" },
    cave3: { name: "Dog chamber", bg: "cave" },
    escape: { name: "Back-door escape", bg: "cave" },
    mech: { name: "Mech fight", bg: "space" },
  };

  function clamp(v, a, b) {
    return Math.max(a, Math.min(b, v));
  }

  function rand(a, b) {
    return a + Math.random() * (b - a);
  }

  function makeCrowdPeople(n) {
    var list = [];
    for (var i = 0; i < n; i++) {
      list.push({
        x: 80 + (i % 10) * 70 + rand(-12, 12),
        y: 280 + Math.floor(i / 10) * 55 + rand(-8, 8),
        phase: Math.random() * Math.PI * 2,
        hue: (i * 37) % 360,
      });
    }
    return list;
  }

  function makeDogPack(n) {
    var list = [];
    for (var i = 0; i < n; i++) {
      list.push({
        x: 60 + Math.random() * 780,
        y: 200 + Math.random() * 520,
        vx: rand(-18, 18),
        vy: rand(-14, 14),
        size: 0.55 + Math.random() * 0.55,
        tone: Math.random() < 0.35 ? "#c4a574" : Math.random() < 0.5 ? "#8b6914" : "#d6d3d1",
      });
    }
    return list;
  }


  /** Ranch return pad sits on Earth surface (grounded), never a floating map artifact. */
  function earthRanchPad(ep) {
    var earth = bodyById(ep, "earth");
    if (!earth) return { x: 80, y: 820, r: 42 };
    var ang = 0.85;
    var rad = (earth.r || 30) * 0.78;
    return {
      x: earth.x + Math.cos(ang) * rad,
      y: earth.y + Math.sin(ang) * rad,
      r: 36,
      earth: earth,
    };
  }

  function ensureCrew(ep, opts) {
    opts = opts || {};
    var C = global.FroggiesCanon;
    var order = (C && C.FROG_ORDER) || ["james", "jimmy", "bubbles", "rexy"];
    var defs = (C && C.FROG_DEFS) || {};
    var primary = opts.primaryId || (ep.crew && ep.crew.primary) || "james";
    if (order.indexOf(primary) < 0) primary = order[0];
    var prev = (ep.crew && ep.crew.members) || [];
    function prevOf(id) {
      for (var i = 0; i < prev.length; i++) if (prev[i].id === id) return prev[i];
      return null;
    }
    ep.crew = ep.crew || {};
    ep.crew.primary = primary;
    ep.crew.members = [];
    for (var oi = 0; oi < order.length; oi++) {
      var id = order[oi];
      var d = defs[id] || { id: id, name: id, color: "#4ade80" };
      var ex = prevOf(id);
      ep.crew.members.push({
        id: id,
        name: d.name || id,
        color: d.color || "#4ade80",
        suit: ex ? !!ex.suit : false,
        x: (ep.px || 450) + (oi - 1.5) * 28,
        y: (ep.py || 560) + (oi % 2) * 16,
      });
    }
    var prim = defs[primary];
    ep.frogId = primary;
    ep.frogColor = prim ? prim.color : "#4ade80";
    ep.frogName = prim ? prim.name : "Froggy";
  }

  function create() {
    return {
      active: false,
      scene: "starship",
      px: 450,
      py: 520,
      vx: 0,
      vy: 0,
      facing: 1,
      toast: "",
      toastT: 0,
      jimmyCatches: 0,
      jimmy: { x: 620, y: 280, vx: 40, vy: -20, jet: 0 },
      germy: { x: 520, y: 360 },
      daisy: { x: 560, y: 390 },
      spotty: { x: 450, y: 400 },
      alex: { x: 320, y: 360 },
      fred: { x: 560, y: 360 },
      people: [],
      dogs: [],
      invaders: [],
      mechHp: 100,
      invaderHp: 100,
      solarPick: 0,
      solarTab: "mars", // mars | neptune
      spaceTime: 0,
      asteroids: null,
      caveProgress: 0,
      foundGarage: false,
      foundCompartment: false,
      foundEscape: false,
      mechWon: false,
      stars: [],
      jet: 0,
      shake: 0,
      pickCool: 0,
      returnPad: { x: 80, y: 820, r: 50, tip: "Return to ranch" },
      inOrbit: false,
      orbitPlanet: null,
      orbitAngle: 0,
      orbitRadius: 0,
      orbitEscapeCool: 0,
      orbitPull: null,
      travelMode: "ship",
      zoom: 1,
      frogId: "james",
      frogColor: "#4ade80",
      frogName: "James",
      crew: null,
      shipSilhouette: true,
    };
  }

  function seedStars(ep) {
    ep.stars = [];
    for (var i = 0; i < 110; i++) {
      ep.stars.push({
        x: Math.random() * MAP,
        y: Math.random() * MAP,
        r: Math.random() * 1.6 + 0.4,
        tw: Math.random() * Math.PI * 2,
      });
    }
  }

  function seedAsteroids(ep) {
    ep.asteroids = [];
    for (var i = 0; i < 48; i++) {
      var ang = Math.random() * Math.PI * 2;
      var dist = ASTEROID_INNER + Math.random() * (ASTEROID_OUTER - ASTEROID_INNER);
      ep.asteroids.push({
        ang: ang,
        dist: dist,
        r: 1.5 + Math.random() * 3.5,
        spin: (Math.random() - 0.5) * 0.4,
      });
    }
  }

  function enter(ep, opts) {
    opts = opts || {};
    ep.active = true;
    ep.scene = "starship";
    ep.px = 450;
    ep.py = 560;
    ep.travelMode = "ship";
    ep.zoom = 1;
    ep.shipSilhouette = true;
    ensureCrew(ep, opts);
    if (ep.crew && ep.crew.members) {
      for (var ci = 0; ci < ep.crew.members.length; ci++) {
        ep.crew.members[ci].suit = false;
        ep.crew.members[ci].x = ep.px + (ci - 1.5) * 26;
        ep.crew.members[ci].y = ep.py + (ci % 2) * 14;
      }
    }
    ep.jimmyCatches = 0;
    ep.caveProgress = 0;
    ep.foundGarage = false;
    ep.foundCompartment = false;
    ep.foundEscape = false;
    ep.mechWon = false;
    ep.mechHp = 100;
    ep.invaderHp = 100;
    ep.people = makeCrowdPeople(20);
    ep.dogs = makeDogPack(120);
    ep.invaders = [];
    for (var i = 0; i < 5; i++) {
      ep.invaders.push({
        x: 120 + i * 140,
        y: 120 + (i % 2) * 40,
        hp: 20,
        phase: i,
      });
    }
    seedStars(ep);
    seedAsteroids(ep);
    ep.spaceTime = 0;
    ep.jimmy = { x: 620, y: 280, vx: 55, vy: -15, jet: 0 };
    ep.toast = "Spotty: All four aboard! Blast off together — then suit EVA or stay in ship.";
    ep.toastT = 3.5;
    if (typeof opts.onEnter === "function") opts.onEnter();
  }

  function exit(ep) {
    ep.active = false;
    ep.toast = "";
    ep.toastT = 0;
  }

  function isActive(ep) {
    return !!(ep && ep.active);
  }

  function setScene(ep, id) {
    if (!SCENES[id]) return;
    ep.scene = id;
    ep.px = 450;
    ep.py = 620;
    if (id === "space") {
      resetOrbit(ep);
      var earth0 = bodyById(ep, "earth") || { x: SUN_X + 270, y: SUN_Y };
      ep.px = earth0.x - 90;
      ep.py = earth0.y + 55;
      ep.travelMode = ep.travelMode || "ship";
      ep.jimmy.x = earth0.x + 40;
      ep.jimmy.y = earth0.y - 70;
      ep.germy.x = earth0.x - 40;
      ep.germy.y = earth0.y + 20;
      ep.daisy.x = earth0.x - 10;
      ep.daisy.y = earth0.y + 45;
      if (ep.crew && ep.crew.members) {
        for (var csi = 0; csi < ep.crew.members.length; csi++) {
          ep.crew.members[csi].x = ep.px + (csi - 1.5) * 22;
          ep.crew.members[csi].y = ep.py + (csi % 2) * 12;
        }
      }
      if (!ep.asteroids) seedAsteroids(ep);
      toast(ep, "All four in space · ship rocket or suit jet · Mars ahead!", 3.6);
    } else if (id === "station") {
      ep.px = 450;
      ep.py = 500;
    } else if (id === "solar") {
      ep.px = 450;
      ep.py = 700;
    } else if (id === "mars" || id.indexOf("cave") === 0 || id === "escape") {
      ep.px = 450;
      ep.py = 700;
    } else if (id === "mech") {
      ep.px = 450;
      ep.py = 640;
      ep.invaderHp = 100;
      ep.mechHp = 100;
      for (var i = 0; i < ep.invaders.length; i++) {
        ep.invaders[i].hp = 20;
        ep.invaders[i].x = 100 + i * 150;
        ep.invaders[i].y = 140 + (i % 2) * 50;
      }
    }
  }

  function toast(ep, msg, t) {
    ep.toast = msg;
    ep.toastT = t == null ? 2.5 : t;
  }

  function hotspotsFor(ep) {
    var s = ep.scene;
    var list = [];
    if (s === "starship") {
      list.push({ id: "spotty", label: "Spotty", x: ep.spotty.x, y: ep.spotty.y, r: 55, tip: "Talk to Spotty · blast off (all four)" });
      list.push({ id: "launch", label: "Launch", x: 450, y: 320, r: 60, tip: "Blast off · all four into space" });
      list.push({ id: "to_ranch", label: "Earth · ranch", x: 80, y: 820, r: 55, tip: "Return home · ranch hub" });
    } else if (s === "space") {
      var st = bodyById(ep, "station");
      var earthH = bodyById(ep, "earth");
      var marsH = bodyById(ep, "mars");
      var padH = earthRanchPad(ep);
      list.push({ id: "jimmy", label: "Jimmy", x: ep.jimmy.x, y: ep.jimmy.y, r: 48, tip: "Catch Jimmy · jetpack!" });
      list.push({ id: "germy", label: "Germy", x: ep.germy.x, y: ep.germy.y, r: 40, tip: "Germy the doggy" });
      list.push({ id: "daisy", label: "Daisy", x: ep.daisy.x, y: ep.daisy.y, r: 40, tip: "Daisy Dachshund" });
      if (st) list.push({ id: "to_station", label: "Station", x: st.x, y: st.y, r: 48, tip: "Space station · orbits Earth" });
      list.push({ id: "to_ranch", label: "Ranch pad", x: padH.x, y: padH.y, r: padH.r, tip: "Earth ranch pad · return home" });
      if (ep.travelMode === "ship") {
        list.push({ id: "exit_ship", label: "EVA suit", x: ep.px + 55, y: ep.py - 10, r: 42, tip: "Exit Starship · spacesuit jet (all four)" });
      } else {
        list.push({ id: "board_ship", label: "Board ship", x: ep.px + 55, y: ep.py - 10, r: 42, tip: "Board Starship rocket" });
      }
      if (marsH) {
        list.push({ id: "to_mars", label: "Mars", x: marsH.x, y: marsH.y, r: 52, tip: "Travel to Mars · cave + dogs" });
      }
      if (earthH) {
        list.push({
          id: "to_ship",
          label: "Starship bay",
          x: earthH.x - Math.cos(0.85) * earthH.r * 0.5,
          y: earthH.y - Math.sin(0.85) * earthH.r * 0.5,
          r: 40,
          tip: "Starship bay · Earth home",
        });
      }
    } else if (s === "station") {
      list.push({ id: "alex", label: "Alex", x: ep.alex.x, y: ep.alex.y, r: 48, tip: "Alex · astronaut" });
      list.push({ id: "fred", label: "Fred", x: ep.fred.x, y: ep.fred.y, r: 48, tip: "Fred · astronaut" });
      list.push({ id: "solar_panel", label: "Solar map", x: 450, y: 220, r: 60, tip: "Mars moons · Neptune moons" });
      list.push({ id: "to_mech", label: "Mechs", x: 780, y: 500, r: 55, tip: "Invader mechs vs James 1000-story" });
      list.push({ id: "to_space", label: "Space", x: 80, y: 820, r: 50, tip: "Back to space / moon" });
    } else if (s === "solar") {
      list.push({ id: "pick_go", label: "Go", x: 450, y: 780, r: 55, tip: "Travel to selected moon / Mars" });
      list.push({ id: "tab_mars", label: "Mars", x: 220, y: 140, r: 45, tip: "Two moons of Mars" });
      list.push({ id: "tab_nep", label: "Neptune", x: 680, y: 140, r: 45, tip: "14 moons of Neptune" });
      list.push({ id: "to_station", label: "Station", x: 80, y: 820, r: 50, tip: "Back to station" });
    } else if (s === "mars") {
      list.push({ id: "cave_mouth", label: "Cave", x: 450, y: 360, r: 60, tip: "Secret passageways · King Germy" });
      list.push({ id: "to_solar", label: "Map", x: 80, y: 820, r: 50, tip: "Back to solar map" });
    } else if (s === "cave1") {
      list.push({ id: "deeper", label: "Deeper", x: 450, y: 280, r: 55, tip: "Level 2 passageway" });
      list.push({ id: "to_mars", label: "Out", x: 80, y: 820, r: 50, tip: "Back to Mars surface" });
    } else if (s === "cave2") {
      list.push({ id: "deeper", label: "Deeper", x: 450, y: 260, r: 55, tip: "Level 3 · dog chamber" });
      list.push({ id: "garage", label: "Garage", x: 200, y: 500, r: 50, tip: "Dog garage" });
      list.push({ id: "up", label: "Up", x: 80, y: 820, r: 50, tip: "Back to level 1" });
    } else if (s === "cave3") {
      list.push({ id: "king", label: "King Germy", x: 450, y: 340, r: 60, tip: "King Germy · king of the dogs" });
      list.push({ id: "compartment", label: "Compartment", x: 700, y: 520, r: 50, tip: "Dog compartment" });
      list.push({ id: "secret_door", label: "Secret door", x: 120, y: 600, r: 50, tip: "Secret back-door escape" });
      list.push({ id: "up", label: "Up", x: 820, y: 820, r: 50, tip: "Back to level 2" });
    } else if (s === "escape") {
      list.push({ id: "escape_door", label: "Door", x: 450, y: 300, r: 55, tip: "Escape door · out!" });
      list.push({ id: "to_chamber", label: "Chamber", x: 80, y: 820, r: 50, tip: "Back to dog chamber" });
    } else if (s === "mech") {
      list.push({ id: "blast", label: "Blast", x: 450, y: 400, r: 70, tip: "James 1000-story mech · INTERACT / ability" });
      list.push({ id: "to_station", label: "Station", x: 80, y: 820, r: 50, tip: "Back to station" });
    }
    return list;
  }

  function nearestHotspot(ep, maxR) {
    var best = null;
    var bestD = maxR || 70;
    var list = hotspotsFor(ep);
    for (var i = 0; i < list.length; i++) {
      var h = list[i];
      var d = Math.hypot(h.x - ep.px, h.y - ep.py);
      if (d < bestD) {
        bestD = d;
        best = h;
      }
    }
    return best;
  }

  function selectedMoon(ep) {
    if (ep.solarTab === "mars") {
      var mi = clamp(ep.solarPick, 0, MARS_MOONS.length - 1);
      return MARS_MOONS[mi];
    }
    var ni = clamp(ep.solarPick, 0, NEPTUNE_MOONS.length - 1);
    return NEPTUNE_MOONS[ni];
  }

  function interact(ep) {
    var h = nearestHotspot(ep, 75);
    if (!h) return { ok: false };
    var id = h.id;

    if (id === "to_ranch") {
      exit(ep);
      return { ok: true, exitRanch: true, toast: "Back at the ranch hub" };
    }
    if (id === "spotty" || id === "launch") {
      ep.travelMode = "ship";
      if (ep.crew && ep.crew.members) {
        for (var li = 0; li < ep.crew.members.length; li++) ep.crew.members[li].suit = false;
      }
      setScene(ep, "space");
      toast(ep, "Blast off! James · Jimmy · Bubbles · Rexy — all four in the Starship!");
      return { ok: true, toast: ep.toast, sfx: "jet" };
    }
    if (id === "exit_ship") {
      ep.travelMode = "suit";
      if (ep.crew && ep.crew.members) {
        for (var ei = 0; ei < ep.crew.members.length; ei++) ep.crew.members[ei].suit = true;
      }
      ep.jet = 0.45;
      toast(ep, "EVA! All four in spacesuits — jet free. Board ship to ride rocket.");
      return { ok: true, toast: ep.toast, sfx: "jet" };
    }
    if (id === "board_ship") {
      ep.travelMode = "ship";
      if (ep.crew && ep.crew.members) {
        for (var bi = 0; bi < ep.crew.members.length; bi++) ep.crew.members[bi].suit = false;
      }
      toast(ep, "Back aboard Starship rocket.");
      return { ok: true, toast: ep.toast };
    }
    if (id === "to_mars") {
      setScene(ep, "mars");
      toast(ep, "Mars! Cave mouth + dogs ahead · hang TBD");
      return { ok: true, toast: ep.toast, sfx: "jet" };
    }
    if (id === "jimmy") {
      ep.jimmyCatches++;
      var earthC = bodyById(ep, "earth") || { x: SUN_X + 270, y: SUN_Y };
      ep.jimmy.x = earthC.x + rand(-90, 90);
      ep.jimmy.y = earthC.y + rand(-90, 90);
      ep.jimmy.vx = rand(-90, 90);
      ep.jimmy.vy = rand(-70, 70);
      ep.jimmy.jet = 0.6;
      if (ep.jimmyCatches >= 2) {
        toast(ep, "Almost! He slipped away again — station is open →");
      } else {
        toast(ep, "Got him… nope! Jimmy jetpacks away again!");
      }
      return { ok: true, toast: ep.toast, sfx: "catch" };
    }
    if (id === "germy") {
      toast(ep, "Germy woofs in zero-G!");
      return { ok: true, toast: ep.toast };
    }
    if (id === "daisy") {
      toast(ep, "Daisy Dachshund floats by!");
      return { ok: true, toast: ep.toast };
    }
    if (id === "to_station") {
      setScene(ep, "station");
      toast(ep, "Space station — Alex & Fred + crew");
      return { ok: true, toast: ep.toast };
    }
    if (id === "to_ship" || id === "to_space") {
      setScene(ep, id === "to_ship" ? "starship" : "space");
      return { ok: true, toast: "Heading out…" };
    }
    if (id === "alex") {
      toast(ep, "Alex: Check the solar map — Mars moons & Neptune's 14!");
      return { ok: true, toast: ep.toast };
    }
    if (id === "fred") {
      toast(ep, "Fred: Invader mechs inbound. James's 1000-story mech ready.");
      return { ok: true, toast: ep.toast };
    }
    if (id === "solar_panel") {
      setScene(ep, "solar");
      toast(ep, "Solar map — pick a moon");
      return { ok: true, toast: ep.toast };
    }
    if (id === "to_mech") {
      setScene(ep, "mech");
      toast(ep, "Foreign invader mechs vs James 1000-story mech!");
      return { ok: true, toast: ep.toast };
    }
    if (id === "tab_mars") {
      ep.solarTab = "mars";
      ep.solarPick = 0;
      toast(ep, "Two moons of Mars: Phobos & Deimos");
      return { ok: true, toast: ep.toast };
    }
    if (id === "tab_nep") {
      ep.solarTab = "neptune";
      ep.solarPick = 0;
      toast(ep, "Neptune's 14 moons — pick one (stub stop)");
      return { ok: true, toast: ep.toast };
    }
    if (id === "pick_go") {
      var moon = selectedMoon(ep);
      if (ep.solarTab === "mars") {
        setScene(ep, "mars");
        toast(ep, "Mars via " + moon.name + " — cave ahead!");
      } else {
        toast(ep, moon.name + " (Neptune) — stub stop · return via map");
        // Stay on solar; brief visit toast only
      }
      return { ok: true, toast: ep.toast };
    }
    if (id === "to_solar") {
      setScene(ep, "solar");
      return { ok: true };
    }
    if (id === "cave_mouth") {
      setScene(ep, "cave1");
      ep.caveProgress = Math.max(ep.caveProgress, 1);
      toast(ep, "Secret passageway · level 1 of 3");
      return { ok: true, toast: ep.toast };
    }
    if (id === "deeper") {
      if (ep.scene === "cave1") {
        setScene(ep, "cave2");
        ep.caveProgress = Math.max(ep.caveProgress, 2);
        toast(ep, "Level 2 passageway");
      } else if (ep.scene === "cave2") {
        setScene(ep, "cave3");
        ep.caveProgress = Math.max(ep.caveProgress, 3);
        toast(ep, "Dog chamber — King Germy & the pack!");
      }
      return { ok: true, toast: ep.toast };
    }
    if (id === "garage") {
      ep.foundGarage = true;
      toast(ep, "Dog garage found!");
      return { ok: true, toast: ep.toast };
    }
    if (id === "compartment") {
      ep.foundCompartment = true;
      toast(ep, "Dog compartment unlocked!");
      return { ok: true, toast: ep.toast };
    }
    if (id === "king") {
      toast(ep, "King Germy: King of the dogs!");
      return { ok: true, toast: ep.toast };
    }
    if (id === "secret_door") {
      setScene(ep, "escape");
      ep.foundEscape = true;
      toast(ep, "Secret back-door escape hallway!");
      return { ok: true, toast: ep.toast };
    }
    if (id === "escape_door") {
      setScene(ep, "mars");
      toast(ep, "Escaped through the door · Mars surface!");
      return { ok: true, toast: ep.toast };
    }
    if (id === "to_mars" || id === "to_chamber" || id === "up") {
      if (id === "to_mars") setScene(ep, "mars");
      else if (id === "to_chamber") setScene(ep, "cave3");
      else if (ep.scene === "cave2") setScene(ep, "cave1");
      else if (ep.scene === "cave3") setScene(ep, "cave2");
      return { ok: true };
    }
    if (id === "blast") {
      return blastMech(ep);
    }
    return { ok: false };
  }

  function blastMech(ep) {
    if (ep.scene !== "mech") return { ok: false };
    var hit = 0;
    for (var i = 0; i < ep.invaders.length; i++) {
      var inv = ep.invaders[i];
      if (inv.hp <= 0) continue;
      inv.hp -= 12;
      hit++;
      if (inv.hp <= 0) ep.invaderHp = Math.max(0, ep.invaderHp - 20);
    }
    ep.shake = 0.25;
    if (ep.invaderHp <= 0) {
      ep.mechWon = true;
      toast(ep, "James 1000-story mech wins! Invaders down!");
      return { ok: true, toast: ep.toast, sfx: "win" };
    }
    toast(ep, hit ? "Mech blast! Invaders reeling…" : "Recharge…");
    return { ok: true, toast: ep.toast, sfx: "blast" };
  }

  function ability(ep, frogId) {
    if (ep.scene === "mech") return blastMech(ep);
    if (ep.inOrbit) {
      return tryHardThrustEscape(ep);
    }
    if (ep.scene === "space" || ep.scene === "mars") {
      /* polish7: stronger jetpack visual; Jimmy ability = jetpack escape punch */
      var jimmyPack = frogId === "jimmy";
      ep.jet = jimmyPack ? 0.85 : 0.55;
      ep.jetPackWho = frogId;
      if (ep.jimmy) {
        ep.jimmy.jet = Math.max(ep.jimmy.jet || 0, jimmyPack ? 1.1 : 0.55);
        if (jimmyPack) {
          ep.jimmy.vx = (ep.jimmy.vx || 0) + (Math.random() > 0.5 ? 90 : -90);
          ep.jimmy.vy = (ep.jimmy.vy || 0) - 70;
        }
      }
      var cfg = orbitCfg();
      var impulse = (cfg.hardThrustImpulse || 320) * (jimmyPack ? 0.5 : 0.35);
      ep.vx = (ep.vx || 0) + ep.facing * impulse * 0.4;
      ep.vy = (ep.vy || 0) - impulse * (jimmyPack ? 0.4 : 0.25);
      ep.px += ep.facing * (jimmyPack ? 55 : 40);
      /* hop1: shared HOP thruster nudge in space */
      toast(ep, "HOP · thruster!");
      return { ok: true, toast: ep.toast, sfx: "jet" };
    }
    if (ep.scene === "solar") {
      var moons = ep.solarTab === "mars" ? MARS_MOONS : NEPTUNE_MOONS;
      ep.solarPick = (ep.solarPick + 1) % moons.length;
      toast(ep, "Selected: " + selectedMoon(ep).name);
      return { ok: true, toast: ep.toast };
    }
    return { ok: false };
  }

  function update(ep, dt, steerX, steerY) {
    if (!ep.active) return;
    if (ep.toastT > 0) ep.toastT -= dt;
    if (ep.shake > 0) ep.shake -= dt;
    if (ep.jet > 0) ep.jet -= dt;
    if (ep.scene === "space") ep.spaceTime = (ep.spaceTime || 0) + dt;

    if (ep.vx == null) ep.vx = 0;
    if (ep.vy == null) ep.vy = 0;
    if (tickOrbit(ep, dt, steerX, steerY)) {
      if (steerX !== 0) ep.facing = steerX > 0 ? 1 : -1;
    } else {
      tryCaptureOrbit(ep, dt);
      if (ep.inOrbit) {
        /* captured this frame — orbit tick next */
      } else {
        var suit = ep.travelMode === "suit";
        var maxSp = ep.scene === "space" || ep.scene === "mars" ? (suit ? 210 : 170) : 150;
        var accel = suit ? 1100 : 880;
        var friction = suit ? 4.2 : 5.5;
        var tvx = steerX * maxSp;
        var tvy = steerY * maxSp;
        if (Math.abs(steerX) + Math.abs(steerY) > 0.05) {
          ep.vx += (tvx - ep.vx) * Math.min(1, accel * dt / maxSp);
          ep.vy += (tvy - ep.vy) * Math.min(1, accel * dt / maxSp);
        } else {
          var damp = Math.exp(-friction * dt);
          ep.vx *= damp;
          ep.vy *= damp;
        }
        ep.px = clamp(ep.px + ep.vx * dt, 40, MAP - 40);
        ep.py = clamp(ep.py + ep.vy * dt, 60, MAP - 40);
        if (steerX !== 0) ep.facing = steerX > 0 ? 1 : -1;
      }
    }

    if (ep.crew && ep.crew.members && (ep.scene === "space" || ep.scene === "starship" || ep.scene === "mars")) {
      for (var cfi = 0; cfi < ep.crew.members.length; cfi++) {
        var cm = ep.crew.members[cfi];
        if (cm.id === ep.frogId) {
          cm.x = ep.px; cm.y = ep.py; cm.suit = ep.travelMode === "suit";
        } else {
          var tx = ep.px + Math.cos(cfi * 1.7 + (ep.spaceTime || 0) * 0.4) * (ep.travelMode === "ship" ? 36 : 48);
          var ty = ep.py + Math.sin(cfi * 1.3 + (ep.spaceTime || 0) * 0.35) * (ep.travelMode === "ship" ? 22 : 32);
          cm.x += (tx - cm.x) * Math.min(1, 4 * dt);
          cm.y += (ty - cm.y) * Math.min(1, 4 * dt);
          cm.suit = ep.travelMode === "suit";
        }
      }
    }

    // Jimmy constantly getting away — keep chase near Earth (kids loop)
    if (ep.scene === "space") {
      var earthJ = bodyById(ep, "earth") || { x: SUN_X + 270, y: SUN_Y };
      var j = ep.jimmy;
      j.x += j.vx * dt;
      j.y += j.vy * dt;
      var jMax = 160;
      if (j.x < earthJ.x - jMax || j.x > earthJ.x + jMax) j.vx *= -1;
      if (j.y < earthJ.y - jMax || j.y > earthJ.y + jMax) j.vy *= -1;
      j.x = clamp(j.x, earthJ.x - jMax, earthJ.x + jMax);
      j.y = clamp(j.y, earthJ.y - jMax, earthJ.y + jMax);
      j.vx += Math.sin(performance.now() / 400) * 30 * dt;
      j.vy += Math.cos(performance.now() / 350) * 25 * dt;
      j.vx = clamp(j.vx, -110, 110);
      j.vy = clamp(j.vy, -90, 90);
      if (j.jet > 0) j.jet -= dt;
      // Germy / Daisy drift near Earth
      ep.germy.x = earthJ.x - 35 + Math.sin(performance.now() / 900) * 40;
      ep.germy.y = earthJ.y + 25 + Math.cos(performance.now() / 1100) * 30;
      ep.daisy.x = earthJ.x + 20 + Math.cos(performance.now() / 800) * 50;
      ep.daisy.y = earthJ.y + 40 + Math.sin(performance.now() / 950) * 25;
    }

    if (ep.scene === "cave3" || ep.scene === "cave2") {
      for (var d = 0; d < ep.dogs.length; d++) {
        var dog = ep.dogs[d];
        dog.x += dog.vx * dt;
        dog.y += dog.vy * dt;
        if (dog.x < 40 || dog.x > MAP - 40) dog.vx *= -1;
        if (dog.y < 160 || dog.y > MAP - 60) dog.vy *= -1;
      }
    }

    if (ep.scene === "station") {
      for (var p = 0; p < ep.people.length; p++) {
        ep.people[p].phase += dt * 2;
      }
    }

    if (ep.scene === "mech" && !ep.mechWon) {
      for (var i = 0; i < ep.invaders.length; i++) {
        var inv = ep.invaders[i];
        if (inv.hp <= 0) continue;
        inv.y += 12 * dt;
        if (inv.y > 380) {
          inv.y = 120;
          ep.mechHp = Math.max(0, ep.mechHp - 4);
        }
      }
      if (ep.mechHp <= 0) {
        ep.mechHp = 100;
        toast(ep, "Soft fail — James mech reboots. Blast again!");
      }
    }

    // Solar picker: left/right cycles moons
    if (ep.pickCool > 0) ep.pickCool -= dt;
    if (ep.scene === "solar" && Math.abs(steerX) > 0.5 && ep.pickCool <= 0) {
      var moons = ep.solarTab === "mars" ? MARS_MOONS : NEPTUNE_MOONS;
      ep.solarPick = (ep.solarPick + (steerX > 0 ? 1 : -1) + moons.length) % moons.length;
      ep.pickCool = 0.28;
      toast(ep, "Selected: " + selectedMoon(ep).name, 1.2);
    }
  }

  /** earth1: lightweight procedural Earth (no assets). Ranch lives here — do not draw yard in vacuum. */
  function drawEarth(ctx, w, h, t, opts) {
    opts = opts || {};
    var cx = opts.cx != null ? opts.cx : w * 0.18;
    var cy = opts.cy != null ? opts.cy : h * 0.72;
    var R = opts.r != null ? opts.r : Math.min(w, h) * 0.22;
    /* Atmosphere glow */
    var glow = ctx.createRadialGradient(cx, cy, R * 0.7, cx, cy, R * 1.35);
    glow.addColorStop(0, "rgba(56,189,248,0.0)");
    glow.addColorStop(0.55, "rgba(56,189,248,0.18)");
    glow.addColorStop(1, "rgba(14,165,233,0)");
    ctx.fillStyle = glow;
    ctx.beginPath();
    ctx.arc(cx, cy, R * 1.35, 0, Math.PI * 2);
    ctx.fill();
    /* Ocean */
    var ocean = ctx.createRadialGradient(cx - R * 0.25, cy - R * 0.2, R * 0.1, cx, cy, R);
    ocean.addColorStop(0, "#38bdf8");
    ocean.addColorStop(0.55, "#0369a1");
    ocean.addColorStop(1, "#0c4a6e");
    ctx.fillStyle = ocean;
    ctx.beginPath();
    ctx.arc(cx, cy, R, 0, Math.PI * 2);
    ctx.fill();
    /* Continents (cheap blobs — not real geography) */
    ctx.fillStyle = "rgba(74, 222, 128, 0.88)";
    function land(lx, ly, rx, ry) {
      ctx.beginPath();
      ctx.ellipse(cx + lx * R, cy + ly * R, rx * R, ry * R, (lx + ly) * 0.4, 0, Math.PI * 2);
      ctx.fill();
    }
    land(-0.25, -0.15, 0.28, 0.18);
    land(0.2, 0.05, 0.22, 0.16);
    land(-0.05, 0.35, 0.18, 0.1);
    land(0.35, -0.3, 0.12, 0.08);
    /* Cloud wisps */
    ctx.fillStyle = "rgba(255,255,255," + (0.22 + 0.08 * Math.sin(t * 0.4)) + ")";
    land(-0.1, -0.4, 0.2, 0.05);
    land(0.15, 0.2, 0.16, 0.04);
    /* Terminator shade */
    var shade = ctx.createLinearGradient(cx - R, cy, cx + R, cy);
    shade.addColorStop(0, "rgba(15,23,42,0.45)");
    shade.addColorStop(0.45, "rgba(15,23,42,0)");
    shade.addColorStop(1, "rgba(15,23,42,0.15)");
    ctx.fillStyle = shade;
    ctx.beginPath();
    ctx.arc(cx, cy, R, 0, Math.PI * 2);
    ctx.fill();
    /* Label */
    ctx.font = "bold " + Math.max(11, Math.round(R * 0.14)) + "px system-ui,sans-serif";
    ctx.textAlign = "center";
    ctx.fillStyle = "rgba(0,0,0,0.55)";
    ctx.fillText("Earth · home", cx + 1, cy + R + 16);
    ctx.fillStyle = "#bbf7d0";
    ctx.fillText("Earth · home", cx, cy + R + 15);
    ctx.font = "10px system-ui,sans-serif";
    ctx.fillStyle = "#86efac";
    ctx.fillText("ranch is here", cx, cy + R + 28);
  }

  function drawStars(ctx, ep, w, h, t) {
    /* Opaque void first — never leave ranch/forest peeking through clearRect */
    ctx.fillStyle = "#020617";
    ctx.fillRect(0, 0, w, h);
    var g = ctx.createLinearGradient(0, 0, 0, h);
    g.addColorStop(0, "#020617");
    g.addColorStop(0.55, "#0b1224");
    g.addColorStop(1, "#0a1020");
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, w, h);

    /* Stars screen-stable while orbiting; mild parallax otherwise */
    var camOffX = 0, camOffY = 0;
    if (!ep.inOrbit) {
      camOffX = (spaceCam.x - MAP * 0.5) * 0.03;
      camOffY = (spaceCam.y - MAP * 0.5) * 0.022;
    }
    var stars = ep.stars || [];
    for (var i = 0; i < stars.length; i++) {
      var s = stars[i];
      var layer = (i % 3) + 1;
      var a = 0.35 + 0.65 * Math.abs(Math.sin(t * (1.2 + layer * 0.3) + s.tw));
      ctx.fillStyle = "rgba(255,255,255," + a + ")";
      var sx = (s.x / MAP) * w - camOffX * layer;
      var sy = (s.y / MAP) * h - camOffY * layer;
      ctx.beginPath();
      ctx.arc(sx, sy, s.r * (0.7 + layer * 0.15), 0, Math.PI * 2);
      ctx.fill();
    }
    var neb = ctx.createRadialGradient(w * 0.7, h * 0.25, 10, w * 0.65, h * 0.3, w * 0.45);
    neb.addColorStop(0, "rgba(88, 80, 180, 0.12)");
    neb.addColorStop(0.5, "rgba(30, 64, 120, 0.06)");
    neb.addColorStop(1, "rgba(0,0,0,0)");
    ctx.fillStyle = neb;
    ctx.fillRect(0, 0, w, h);
    drawDarkGroundPlane(ctx, w, h, t);
  }

  function drawDarkGroundPlane(ctx, w, h, t) {
    var gy = h * 0.78;
    var grd = ctx.createLinearGradient(0, gy - h * 0.2, 0, h);
    grd.addColorStop(0, "rgba(2,6,23,0)");
    grd.addColorStop(0.35, "rgba(2,6,23,0.75)");
    grd.addColorStop(1, "rgba(2,6,23,0.96)");
    ctx.fillStyle = grd;
    ctx.fillRect(0, gy - h * 0.22, w, h * 0.45);
    ctx.fillStyle = "rgba(15,23,42,0.92)";
    ctx.beginPath();
    ctx.ellipse(w * 0.5, gy, w * 0.62, h * 0.16, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.strokeStyle = "rgba(51,65,85,0.55)";
    ctx.lineWidth = 1.5;
    ctx.stroke();
    for (var si = 0; si < 28; si++) {
      var px = (si * 97.3) % w;
      var py = gy - h * 0.12 + ((si * 53.1) % (h * 0.28));
      var tw = 0.35 + 0.5 * Math.abs(Math.sin(t * 1.4 + si));
      ctx.fillStyle = "rgba(226,232,240," + tw + ")";
      ctx.beginPath();
      ctx.arc(px, py, 0.8 + (si % 3) * 0.4, 0, Math.PI * 2);
      ctx.fill();
    }
  }

  var spaceCam = { x: MAP * 0.5, y: MAP * 0.55 };

  function worldToScreen(x, y, w, h, camX, camY) {
    // Fixed-angle 2.5D: isometric-ish foreshorten + depth scale + space zoom
    camX = camX == null ? spaceCam.x : camX;
    camY = camY == null ? spaceCam.y : camY;
    var z = (spaceCam.zoom != null ? spaceCam.zoom : 1);
    var dx = (x - camX) * z;
    var dy = (y - camY) * z;
    var sx = w * 0.5 + dx * 0.95 - dy * 0.22;
    var sy = h * 0.42 + dx * 0.18 + dy * 0.62;
    var depth = clamp(0.62 + y / MAP * 0.5 + dy * 0.0002, 0.48, 1.35) * (0.85 + z * 0.15);
    return { x: sx, y: sy, d: depth };
  }

  function drawLabel(ctx, text, x, y, color) {
    ctx.font = "bold 11px system-ui,sans-serif";
    ctx.textAlign = "center";
    ctx.fillStyle = "rgba(0,0,0,0.55)";
    ctx.fillText(text, x + 1, y + 1);
    ctx.fillStyle = color || "#fff";
    ctx.fillText(text, x, y);
  }

  function drawFrog(ctx, x, y, d, facing, jet) {
    ctx.save();
    ctx.translate(x, y);
    ctx.scale(facing < 0 ? -d : d, d);
    if (jet > 0) {
      /* polish7: louder thruster / jetpack plume */
      ctx.fillStyle = "rgba(56,189,248,0.85)";
      ctx.beginPath();
      ctx.moveTo(-7, 10);
      ctx.lineTo(0, 28 + jet * 34);
      ctx.lineTo(7, 10);
      ctx.fill();
      ctx.fillStyle = "rgba(251,191,36,0.8)";
      ctx.beginPath();
      ctx.moveTo(-3.5, 12);
      ctx.lineTo(0, 24 + jet * 22);
      ctx.lineTo(3.5, 12);
      ctx.fill();
    }
    ctx.fillStyle = "#4ade80";
    ctx.beginPath();
    ctx.ellipse(0, 0, 14, 12, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = "#166534";
    ctx.fillRect(-8, -16, 16, 6);
    ctx.fillStyle = "#fff";
    ctx.beginPath();
    ctx.arc(5, -4, 3.5, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = "#111";
    ctx.beginPath();
    ctx.arc(6, -4, 1.6, 0, Math.PI * 2);
    ctx.fill();
    // suit
    ctx.strokeStyle = "rgba(226,232,240,0.85)";
    ctx.lineWidth = 2;
    ctx.strokeRect(-12, -10, 24, 22);
    ctx.restore();
  }

  function drawSpotty(ctx, x, y, d) {
    ctx.save();
    ctx.translate(x, y);
    ctx.scale(d, d);
    ctx.fillStyle = "#f97316";
    ctx.beginPath();
    ctx.ellipse(0, 2, 12, 10, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.beginPath();
    ctx.moveTo(-8, -6);
    ctx.lineTo(-4, -16);
    ctx.lineTo(0, -6);
    ctx.moveTo(8, -6);
    ctx.lineTo(4, -16);
    ctx.lineTo(0, -6);
    ctx.fill();
    ctx.fillStyle = "#fff";
    ctx.fillRect(-3, 4, 3, 3);
    ctx.fillRect(2, 6, 3, 3);
    ctx.fillRect(5, 2, 2, 2);
    ctx.restore();
    drawLabel(ctx, "Spotty", x, y - 22 * d, "#fdba74");
  }

  function drawDog(ctx, x, y, d, color, label) {
    /* polish9: presence marker ring for named cast */
    if (label) {
      var pulse = 0.45 + 0.35 * Math.sin(performance.now() / 420);
      ctx.strokeStyle = "rgba(253, 224, 71, " + pulse + ")";
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.ellipse(x, y + 4 * d, 18 * d, 8 * d, 0, 0, Math.PI * 2);
      ctx.stroke();
      ctx.fillStyle = "rgba(251, 191, 36, " + (pulse * 0.18) + ")";
      ctx.beginPath();
      ctx.ellipse(x, y + 4 * d, 16 * d, 7 * d, 0, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.save();
    ctx.translate(x, y);
    ctx.scale(d, d);
    ctx.fillStyle = color || "#c4a574";
    ctx.beginPath();
    ctx.ellipse(0, 2, 14, 7, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.beginPath();
    ctx.ellipse(12, -2, 6, 5, 0.2, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = "#78350f";
    ctx.fillRect(-2, -1, 16, 3);
    ctx.restore();
    if (label) drawLabel(ctx, label, x, y - 14 * d, "#fde68a");
  }

  /* polish9: soft hundreds-of-dogs silhouette flock (ambient dots) near Mars cave only */
  function drawDogFlockTease(ctx, caveX, caveY, caveD, t) {
    for (var i = 0; i < 48; i++) {
      var ang = (i / 48) * Math.PI * 2 + t * 0.15;
      var rad = 70 + (i % 7) * 9 + Math.sin(t * 0.8 + i) * 6;
      var fx = caveX + Math.cos(ang) * rad * 0.55 * caveD;
      var fy = caveY + Math.sin(ang * 1.1) * rad * 0.28 * caveD + 10;
      var a = 0.12 + 0.1 * Math.sin(t * 2 + i * 0.4);
      ctx.fillStyle = "rgba(28, 25, 23, " + a + ")";
      ctx.beginPath();
      ctx.ellipse(fx, fy, 3.2 * caveD, 1.6 * caveD, ang * 0.2, 0, Math.PI * 2);
      ctx.fill();
    }
    drawLabel(ctx, "hundreds of dogs · tease", caveX, caveY + 78 * caveD, "rgba(214, 211, 209, 0.55)");
  }

  function drawAstronaut(ctx, x, y, d, suit, name) {
    ctx.save();
    ctx.translate(x, y);
    ctx.scale(d, d);
    ctx.fillStyle = suit;
    ctx.beginPath();
    ctx.ellipse(0, 4, 11, 14, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = "#e2e8f0";
    ctx.beginPath();
    ctx.arc(0, -10, 8, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = "#38bdf8";
    ctx.beginPath();
    ctx.arc(0, -10, 5, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();
    drawLabel(ctx, name, x, y - 26 * d, "#e2e8f0");
  }

  function drawPerson(ctx, x, y, d, hue) {
    ctx.save();
    ctx.translate(x, y);
    ctx.scale(d, d);
    ctx.fillStyle = "hsl(" + hue + ",45%,55%)";
    ctx.fillRect(-5, -2, 10, 14);
    ctx.fillStyle = "#fcd34d";
    ctx.beginPath();
    ctx.arc(0, -8, 5, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();
  }

  function drawHot(ctx, h, p, near) {
    ctx.beginPath();
    ctx.ellipse(p.x, p.y + 8, 22 * p.d, 10 * p.d, 0, 0, Math.PI * 2);
    ctx.fillStyle = near ? "rgba(250,204,21,0.35)" : "rgba(148,163,184,0.2)";
    ctx.fill();
    if (near) {
      ctx.strokeStyle = "rgba(250,204,21,0.9)";
      ctx.lineWidth = 2;
      ctx.stroke();
    }
  }

  function drawJimmySuit(ctx, x, y, d, jet) {
    ctx.save();
    ctx.translate(x, y);
    ctx.scale(d, d);
    /* polish7: bigger jetpack plume when escaping / ability-fired */
    var jetOn = jet > 0;
    var plume = jetOn ? 22 + jet * 28 : 8;
    ctx.fillStyle = jetOn ? "rgba(56,189,248,0.9)" : "rgba(251,146,60,0.55)";
    ctx.beginPath();
    ctx.moveTo(-6, 12);
    ctx.lineTo(0, 30 + plume);
    ctx.lineTo(6, 12);
    ctx.fill();
    if (jetOn) {
      ctx.fillStyle = "rgba(251,191,36,0.85)";
      ctx.beginPath();
      ctx.moveTo(-3, 14);
      ctx.lineTo(0, 26 + plume * 0.65);
      ctx.lineTo(3, 14);
      ctx.fill();
    }
    ctx.fillStyle = "#fb923c";
    ctx.beginPath();
    ctx.ellipse(0, 0, 13, 11, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = "#e2e8f0";
    ctx.strokeStyle = "#94a3b8";
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.arc(0, -2, 10, 0, Math.PI * 2);
    ctx.stroke();
    ctx.fillStyle = "#ef4444";
    ctx.fillRect(-7, -15, 14, 5);
    ctx.restore();
    drawLabel(ctx, "Jimmy", x, y - 22 * d, "#fdba74");
  }

  function drawMechJames(ctx, x, y, d) {
    ctx.save();
    ctx.translate(x, y);
    ctx.scale(d, d);
    // 1000-story silhouette — tall stack
    ctx.fillStyle = "rgba(74,222,128,0.9)";
    ctx.fillRect(-18, -180, 36, 200);
    ctx.fillStyle = "rgba(22,101,52,0.95)";
    for (var i = 0; i < 12; i++) {
      ctx.fillRect(-16, -170 + i * 14, 32, 3);
    }
    ctx.fillStyle = "#facc15";
    ctx.fillRect(-10, -190, 20, 12);
    ctx.fillStyle = "#fff";
    ctx.font = "bold 10px system-ui";
    ctx.textAlign = "center";
    ctx.fillText("1000", 0, -195);
    ctx.restore();
    drawLabel(ctx, "James mech", x, y - 210 * d, "#86efac");
  }

  function drawInvader(ctx, x, y, d) {
    ctx.save();
    ctx.translate(x, y);
    ctx.scale(d, d);
    ctx.fillStyle = "#ef4444";
    ctx.fillRect(-16, -20, 32, 28);
    ctx.fillStyle = "#7f1d1d";
    ctx.fillRect(-22, -8, 8, 18);
    ctx.fillRect(14, -8, 8, 18);
    ctx.fillStyle = "#fef08a";
    ctx.fillRect(-6, -12, 12, 8);
    ctx.restore();
  }

  function drawOrbitGuides(ctx, ep, body, scr, t) {
    if (!body || !body.capture) return;
    var cfgVis = orbitCfg();
    var soft = (body.soft || cfgVis.softPullRadius || 220) * scr.d * 0.95;
    var cap = (body.cap || cfgVis.captureRadius || 120) * scr.d * 0.95;
    var locked = ep.inOrbit && ep.orbitPlanet && ep.orbitPlanet.id === body.id;
    var pulling = ep.orbitPull && ep.orbitPull.planet && ep.orbitPull.planet.id === body.id;
    ctx.beginPath();
    ctx.arc(scr.x, scr.y, soft, 0, Math.PI * 2);
    ctx.strokeStyle = pulling ? "rgba(125,211,252,0.55)" : "rgba(125,211,252,0.14)";
    ctx.lineWidth = pulling ? 2.2 : 1;
    ctx.setLineDash([8, 7]);
    ctx.stroke();
    ctx.setLineDash([]);
    ctx.beginPath();
    ctx.arc(scr.x, scr.y, cap, 0, Math.PI * 2);
    ctx.strokeStyle = locked ? "rgba(250,204,21,0.85)" : "rgba(56,189,248,0.28)";
    ctx.lineWidth = locked ? 2.2 : 1.2;
    ctx.stroke();
    if (locked) {
      var orbScr = (ep.orbitRadius || body.r + 28) * scr.d;
      ctx.beginPath();
      ctx.arc(scr.x, scr.y, orbScr, 0, Math.PI * 2);
      ctx.strokeStyle = "rgba(250,204,21,0.95)";
      ctx.lineWidth = 2.6;
      ctx.stroke();
    } else if (pulling) {
      var str = ep.orbitPull.strength || 0;
      var pp = worldToScreen(ep.px, ep.py, scr._w, scr._h);
      ctx.strokeStyle = "rgba(125,211,252," + (0.3 + str * 0.5) + ")";
      ctx.lineWidth = 1.8 + str * 2;
      ctx.beginPath();
      ctx.moveTo(scr.x, scr.y);
      ctx.lineTo(pp.x, pp.y);
      ctx.stroke();
    }
  }

  function drawSolarSystemScene(ctx, ep, w, h, t) {
    var bodies = solarBodies(ep);
    var sun = null;
    for (var si = 0; si < bodies.length; si++) {
      if (bodies[si].id === "sun") { sun = bodies[si]; break; }
    }
    /* Orbit path rings (compressed) */
    if (sun) {
      var sunScr = worldToScreen(sun.x, sun.y, w, h);
      for (var di = 0; di < SOLAR_DEFS.length; di++) {
        var def = SOLAR_DEFS[di];
        if (def.dist <= 0) continue;
        ctx.beginPath();
        ctx.arc(sunScr.x, sunScr.y, def.dist * sunScr.d * 0.95, 0, Math.PI * 2);
        ctx.strokeStyle = "rgba(148,163,184,0.12)";
        ctx.lineWidth = 1;
        ctx.stroke();
      }
      /* Asteroid belt */
      ctx.beginPath();
      ctx.arc(sunScr.x, sunScr.y, ASTEROID_INNER * sunScr.d * 0.95, 0, Math.PI * 2);
      ctx.strokeStyle = "rgba(214,211,209,0.2)";
      ctx.lineWidth = 1;
      ctx.stroke();
      ctx.beginPath();
      ctx.arc(sunScr.x, sunScr.y, ASTEROID_OUTER * sunScr.d * 0.95, 0, Math.PI * 2);
      ctx.strokeStyle = "rgba(214,211,209,0.2)";
      ctx.lineWidth = 1;
      ctx.stroke();
      if (ep.asteroids) {
        for (var ai = 0; ai < ep.asteroids.length; ai++) {
          var a = ep.asteroids[ai];
          var aang = a.ang + (ep.spaceTime || 0) * 0.06;
          var ax = SUN_X + Math.cos(aang) * a.dist;
          var ay = SUN_Y + Math.sin(aang) * a.dist;
          var ascr = worldToScreen(ax, ay, w, h);
          ctx.fillStyle = "rgba(214,211,209,0.75)";
          ctx.beginPath();
          ctx.arc(ascr.x, ascr.y, a.r * ascr.d, 0, Math.PI * 2);
          ctx.fill();
        }
      }
      drawLabel(ctx, "Asteroid belt", sunScr.x, sunScr.y - (ASTEROID_INNER + ASTEROID_OUTER) * 0.5 * sunScr.d * 0.95 - 10, "#a8a29e");
    }

    for (var bi = 0; bi < bodies.length; bi++) {
      var body = bodies[bi];
      var scr = worldToScreen(body.x, body.y, w, h);
      scr._w = w; scr._h = h;
      if (body.capture) drawOrbitGuides(ctx, ep, body, scr, t);

      if (body.id === "sun") {
        var glow = ctx.createRadialGradient(scr.x, scr.y, body.r * scr.d * 0.2, scr.x, scr.y, body.r * scr.d * 1.6);
        glow.addColorStop(0, "rgba(253,224,71,0.95)");
        glow.addColorStop(0.45, "rgba(251,191,36,0.55)");
        glow.addColorStop(1, "rgba(245,158,11,0)");
        ctx.fillStyle = glow;
        ctx.beginPath();
        ctx.arc(scr.x, scr.y, body.r * scr.d * 1.6, 0, Math.PI * 2);
        ctx.fill();
        ctx.fillStyle = body.color;
        ctx.beginPath();
        ctx.arc(scr.x, scr.y, body.r * scr.d, 0, Math.PI * 2);
        ctx.fill();
        drawLabel(ctx, "☉ Sun", scr.x, scr.y + body.r * scr.d + 14, "#fde68a");
        continue;
      }

      if (body.id === "earth") {
        drawEarth(ctx, w, h, t, { cx: scr.x, cy: scr.y, r: body.r * scr.d });
        var padAng = 0.85;
        var padR = body.r * scr.d * 0.78;
        var pdx = scr.x + Math.cos(padAng) * padR;
        var pdy = scr.y + Math.sin(padAng) * padR;
        ctx.fillStyle = "rgba(2,132,199,0.85)";
        ctx.beginPath();
        ctx.ellipse(pdx, pdy, 14 * scr.d, 6 * scr.d, padAng * 0.2, 0, Math.PI * 2);
        ctx.fill();
        ctx.strokeStyle = "#86efac";
        ctx.lineWidth = 1.6;
        ctx.stroke();
        ctx.fillStyle = "rgba(134,239,172,0.9)";
        ctx.beginPath();
        ctx.arc(pdx, pdy, 3.2 * scr.d, 0, Math.PI * 2);
        ctx.fill();
        drawLabel(ctx, "Ranch pad", pdx, pdy + 16 * scr.d, "#bbf7d0");
        continue;
      }

      if (body.rings) {
        ctx.strokeStyle = "rgba(253,230,138,0.55)";
        ctx.lineWidth = 3 * scr.d;
        ctx.beginPath();
        ctx.ellipse(scr.x, scr.y, body.r * scr.d * 1.75, body.r * scr.d * 0.55, 0.4, 0, Math.PI * 2);
        ctx.stroke();
      }

      if (body.id === "station") {
        /* Simple station silhouette — constantly orbits Earth */
        ctx.fillStyle = "#94a3b8";
        ctx.fillRect(scr.x - 10 * scr.d, scr.y - 4 * scr.d, 20 * scr.d, 8 * scr.d);
        ctx.fillStyle = "#38bdf8";
        ctx.fillRect(scr.x - 18 * scr.d, scr.y - 2 * scr.d, 8 * scr.d, 4 * scr.d);
        ctx.fillRect(scr.x + 10 * scr.d, scr.y - 2 * scr.d, 8 * scr.d, 4 * scr.d);
        ctx.fillStyle = "#e2e8f0";
        ctx.beginPath();
        ctx.arc(scr.x, scr.y, 5 * scr.d, 0, Math.PI * 2);
        ctx.fill();
        var pulse = 0.45 + 0.35 * Math.sin(t * 3);
        ctx.strokeStyle = "rgba(125,211,252," + pulse + ")";
        ctx.lineWidth = 1.5;
        ctx.beginPath();
        ctx.arc(scr.x, scr.y, 18 * scr.d + Math.sin(t * 2) * 2, 0, Math.PI * 2);
        ctx.stroke();
        drawLabel(ctx, "🛰 Station · orbits Earth", scr.x, scr.y + 22 * scr.d, "#7dd3fc");
        continue;
      }

      ctx.fillStyle = body.color || "#e2e8f0";
      ctx.beginPath();
      ctx.arc(scr.x, scr.y, body.r * scr.d, 0, Math.PI * 2);
      ctx.fill();
      if (body.id === "moon") {
        ctx.fillStyle = "rgba(100,116,139,0.4)";
        ctx.beginPath();
        ctx.arc(scr.x - 4 * scr.d, scr.y - 2 * scr.d, 3 * scr.d, 0, Math.PI * 2);
        ctx.arc(scr.x + 5 * scr.d, scr.y + 3 * scr.d, 2.2 * scr.d, 0, Math.PI * 2);
        ctx.fill();
        drawLabel(ctx, "☾ Moon · orbits Earth", scr.x, scr.y + body.r * scr.d + 14, "#e2e8f0");
      } else if (body.id === "mars") {
        drawLabel(ctx, "♂ Mars", scr.x, scr.y + body.r * scr.d + 14, "#fed7aa");
      } else if (body.id === "pluto") {
        drawLabel(ctx, "♇ Pluto · dwarf", scr.x, scr.y + body.r * scr.d + 14, "#cbd5e1");
      } else {
        drawLabel(ctx, body.name, scr.x, scr.y + body.r * scr.d + 14, "#e2e8f0");
      }
    }

    /* Scale note */
    ctx.fillStyle = "rgba(15,23,42,0.72)";
    ctx.fillRect(8, 32, 268, 36);
    ctx.strokeStyle = "rgba(148,163,184,0.45)";
    ctx.strokeRect(8, 32, 268, 36);
    ctx.font = "bold 11px system-ui,sans-serif";
    ctx.textAlign = "left";
    ctx.fillStyle = "#fde68a";
    ctx.fillText("Solar System · scale compressed", 16, 48);
    ctx.fillStyle = "#94a3b8";
    ctx.font = "10px system-ui,sans-serif";
    ctx.fillText("not to scale · gravity wells on planets", 16, 62);
  }

  /* polish6: distant invader mech silhouettes (visual tease — no combat system) */
  function drawDistantInvaderSilhouettes(ctx, ep, w, h, t) {
    var nearMars = false;
    if (ep.scene === "mars") nearMars = true;
    else if (ep.scene === "solar" && ep.solarTab === "mars") nearMars = true;
    else if (ep.scene === "space") {
      var marsB = bodyById(ep, "mars");
      if (marsB) nearMars = Math.hypot((ep.px || 0) - marsB.x, (ep.py || 0) - marsB.y) < 220;
    } else if (ep.scene === "station") {
      /* when player near to_mech / Mars-side of station */
      var dMech = Math.hypot((ep.px || 0) - 780, (ep.py || 0) - 500);
      nearMars = dMech < 220;
    }
    if (ep.scene === "mars") {
      var dCave = Math.hypot((ep.px || 450) - 450, (ep.py || 500) - 360);
      nearMars = true;
    }
    if (!nearMars && ep.scene !== "mars" && !(ep.scene === "solar" && ep.solarTab === "mars")) return;
    var alpha = ep.scene === "mars" ? 0.55 : 0.38;
    var n = 5;
    for (var i = 0; i < n; i++) {
      var sx = w * (0.08 + i * 0.2) + Math.sin(t * 0.15 + i) * 8;
      var sy = h * (0.12 + (i % 3) * 0.05) + Math.cos(t * 0.12 + i * 0.7) * 4;
      var sc = 0.45 + (i % 3) * 0.12;
      ctx.save();
      ctx.globalAlpha = alpha * (0.7 + 0.3 * Math.sin(t * 0.4 + i));
      ctx.fillStyle = "#3f0a0a";
      ctx.beginPath();
      ctx.moveTo(sx - 10 * sc, sy + 18 * sc);
      ctx.lineTo(sx - 6 * sc, sy - 28 * sc);
      ctx.lineTo(sx + 6 * sc, sy - 28 * sc);
      ctx.lineTo(sx + 10 * sc, sy + 18 * sc);
      ctx.closePath();
      ctx.fill();
      ctx.fillRect(sx - 16 * sc, sy - 10 * sc, 8 * sc, 14 * sc);
      ctx.fillRect(sx + 8 * sc, sy - 10 * sc, 8 * sc, 14 * sc);
      ctx.fillStyle = "#7f1d1d";
      ctx.fillRect(sx - 4 * sc, sy - 34 * sc, 8 * sc, 6 * sc);
      ctx.restore();
    }
    ctx.globalAlpha = 1;
    ctx.fillStyle = "rgba(254, 202, 202, " + (alpha * 0.85) + ")";
    ctx.font = "bold 11px system-ui,sans-serif";
    ctx.textAlign = "center";
    ctx.fillText("Invader mechs · distant silhouette tease", w * 0.5, h * 0.08);
  }

  /** earth1: lightweight procedural Earth (no assets). Ranch lives here — do not draw yard in vacuum. */
  function drawEarth(ctx, w, h, t, opts) {
    opts = opts || {};
    var cx = opts.cx != null ? opts.cx : w * 0.18;
    var cy = opts.cy != null ? opts.cy : h * 0.72;
    var R = opts.r != null ? opts.r : Math.min(w, h) * 0.22;
    /* Atmosphere glow */
    var glow = ctx.createRadialGradient(cx, cy, R * 0.7, cx, cy, R * 1.35);
    glow.addColorStop(0, "rgba(56,189,248,0.0)");
    glow.addColorStop(0.55, "rgba(56,189,248,0.18)");
    glow.addColorStop(1, "rgba(14,165,233,0)");
    ctx.fillStyle = glow;
    ctx.beginPath();
    ctx.arc(cx, cy, R * 1.35, 0, Math.PI * 2);
    ctx.fill();
    /* Ocean */
    var ocean = ctx.createRadialGradient(cx - R * 0.25, cy - R * 0.2, R * 0.1, cx, cy, R);
    ocean.addColorStop(0, "#38bdf8");
    ocean.addColorStop(0.55, "#0369a1");
    ocean.addColorStop(1, "#0c4a6e");
    ctx.fillStyle = ocean;
    ctx.beginPath();
    ctx.arc(cx, cy, R, 0, Math.PI * 2);
    ctx.fill();
    /* Continents (cheap blobs — not real geography) */
    ctx.fillStyle = "rgba(74, 222, 128, 0.88)";
    function land(lx, ly, rx, ry) {
      ctx.beginPath();
      ctx.ellipse(cx + lx * R, cy + ly * R, rx * R, ry * R, (lx + ly) * 0.4, 0, Math.PI * 2);
      ctx.fill();
    }
    land(-0.25, -0.15, 0.28, 0.18);
    land(0.2, 0.05, 0.22, 0.16);
    land(-0.05, 0.35, 0.18, 0.1);
    land(0.35, -0.3, 0.12, 0.08);
    /* Cloud wisps */
    ctx.fillStyle = "rgba(255,255,255," + (0.22 + 0.08 * Math.sin(t * 0.4)) + ")";
    land(-0.1, -0.4, 0.2, 0.05);
    land(0.15, 0.2, 0.16, 0.04);
    /* Terminator shade */
    var shade = ctx.createLinearGradient(cx - R, cy, cx + R, cy);
    shade.addColorStop(0, "rgba(15,23,42,0.45)");
    shade.addColorStop(0.45, "rgba(15,23,42,0)");
    shade.addColorStop(1, "rgba(15,23,42,0.15)");
    ctx.fillStyle = shade;
    ctx.beginPath();
    ctx.arc(cx, cy, R, 0, Math.PI * 2);
    ctx.fill();
    /* Label */
    ctx.font = "bold " + Math.max(11, Math.round(R * 0.14)) + "px system-ui,sans-serif";
    ctx.textAlign = "center";
    ctx.fillStyle = "rgba(0,0,0,0.55)";
    ctx.fillText("Earth · home", cx + 1, cy + R + 16);
    ctx.fillStyle = "#bbf7d0";
    ctx.fillText("Earth · home", cx, cy + R + 15);
    ctx.font = "10px system-ui,sans-serif";
    ctx.fillStyle = "#86efac";
    ctx.fillText("ranch is here", cx, cy + R + 28);
  }

  function drawStars(ctx, ep, w, h, t) {
    /* Opaque void first — never leave ranch/forest peeking through clearRect */
    ctx.fillStyle = "#020617";
    ctx.fillRect(0, 0, w, h);
    var g = ctx.createLinearGradient(0, 0, 0, h);
    g.addColorStop(0, "#020617");
    g.addColorStop(0.55, "#0b1224");
    g.addColorStop(1, "#0a1020");
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, w, h);

    /* Stars screen-stable while orbiting; mild parallax otherwise */
    var camOffX = 0, camOffY = 0;
    if (!ep.inOrbit) {
      camOffX = (spaceCam.x - MAP * 0.5) * 0.03;
      camOffY = (spaceCam.y - MAP * 0.5) * 0.022;
    }
    var stars = ep.stars || [];
    for (var i = 0; i < stars.length; i++) {
      var s = stars[i];
      var layer = (i % 3) + 1;
      var a = 0.35 + 0.65 * Math.abs(Math.sin(t * (1.2 + layer * 0.3) + s.tw));
      ctx.fillStyle = "rgba(255,255,255," + a + ")";
      var sx = (s.x / MAP) * w - camOffX * layer;
      var sy = (s.y / MAP) * h - camOffY * layer;
      ctx.beginPath();
      ctx.arc(sx, sy, s.r * (0.7 + layer * 0.15), 0, Math.PI * 2);
      ctx.fill();
    }
    var neb = ctx.createRadialGradient(w * 0.7, h * 0.25, 10, w * 0.65, h * 0.3, w * 0.45);
    neb.addColorStop(0, "rgba(88, 80, 180, 0.12)");
    neb.addColorStop(0.5, "rgba(30, 64, 120, 0.06)");
    neb.addColorStop(1, "rgba(0,0,0,0)");
    ctx.fillStyle = neb;
    ctx.fillRect(0, 0, w, h);
    drawDarkGroundPlane(ctx, w, h, t);
  }

  function drawDarkGroundPlane(ctx, w, h, t) {
    var gy = h * 0.78;
    var grd = ctx.createLinearGradient(0, gy - h * 0.2, 0, h);
    grd.addColorStop(0, "rgba(2,6,23,0)");
    grd.addColorStop(0.35, "rgba(2,6,23,0.75)");
    grd.addColorStop(1, "rgba(2,6,23,0.96)");
    ctx.fillStyle = grd;
    ctx.fillRect(0, gy - h * 0.22, w, h * 0.45);
    ctx.fillStyle = "rgba(15,23,42,0.92)";
    ctx.beginPath();
    ctx.ellipse(w * 0.5, gy, w * 0.62, h * 0.16, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.strokeStyle = "rgba(51,65,85,0.55)";
    ctx.lineWidth = 1.5;
    ctx.stroke();
    for (var si = 0; si < 28; si++) {
      var px = (si * 97.3) % w;
      var py = gy - h * 0.12 + ((si * 53.1) % (h * 0.28));
      var tw = 0.35 + 0.5 * Math.abs(Math.sin(t * 1.4 + si));
      ctx.fillStyle = "rgba(226,232,240," + tw + ")";
      ctx.beginPath();
      ctx.arc(px, py, 0.8 + (si % 3) * 0.4, 0, Math.PI * 2);
      ctx.fill();
    }
  }

  var spaceCam = { x: MAP * 0.5, y: MAP * 0.55 };

  function worldToScreen(x, y, w, h, camX, camY) {
    // Fixed-angle 2.5D: isometric-ish foreshorten + depth scale + space zoom
    camX = camX == null ? spaceCam.x : camX;
    camY = camY == null ? spaceCam.y : camY;
    var z = (spaceCam.zoom != null ? spaceCam.zoom : 1);
    var dx = (x - camX) * z;
    var dy = (y - camY) * z;
    var sx = w * 0.5 + dx * 0.95 - dy * 0.22;
    var sy = h * 0.42 + dx * 0.18 + dy * 0.62;
    var depth = clamp(0.62 + y / MAP * 0.5 + dy * 0.0002, 0.48, 1.35) * (0.85 + z * 0.15);
    return { x: sx, y: sy, d: depth };
  }

  function drawLabel(ctx, text, x, y, color) {
    ctx.font = "bold 11px system-ui,sans-serif";
    ctx.textAlign = "center";
    ctx.fillStyle = "rgba(0,0,0,0.55)";
    ctx.fillText(text, x + 1, y + 1);
    ctx.fillStyle = color || "#fff";
    ctx.fillText(text, x, y);
  }

  function drawFrog(ctx, x, y, d, facing, jet) {
    ctx.save();
    ctx.translate(x, y);
    ctx.scale(facing < 0 ? -d : d, d);
    if (jet > 0) {
      /* polish7: louder thruster / jetpack plume */
      ctx.fillStyle = "rgba(56,189,248,0.85)";
      ctx.beginPath();
      ctx.moveTo(-7, 10);
      ctx.lineTo(0, 28 + jet * 34);
      ctx.lineTo(7, 10);
      ctx.fill();
      ctx.fillStyle = "rgba(251,191,36,0.8)";
      ctx.beginPath();
      ctx.moveTo(-3.5, 12);
      ctx.lineTo(0, 24 + jet * 22);
      ctx.lineTo(3.5, 12);
      ctx.fill();
    }
    ctx.fillStyle = "#4ade80";
    ctx.beginPath();
    ctx.ellipse(0, 0, 14, 12, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = "#166534";
    ctx.fillRect(-8, -16, 16, 6);
    ctx.fillStyle = "#fff";
    ctx.beginPath();
    ctx.arc(5, -4, 3.5, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = "#111";
    ctx.beginPath();
    ctx.arc(6, -4, 1.6, 0, Math.PI * 2);
    ctx.fill();
    // suit
    ctx.strokeStyle = "rgba(226,232,240,0.85)";
    ctx.lineWidth = 2;
    ctx.strokeRect(-12, -10, 24, 22);
    ctx.restore();
  }

  function drawSpotty(ctx, x, y, d) {
    ctx.save();
    ctx.translate(x, y);
    ctx.scale(d, d);
    ctx.fillStyle = "#f97316";
    ctx.beginPath();
    ctx.ellipse(0, 2, 12, 10, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.beginPath();
    ctx.moveTo(-8, -6);
    ctx.lineTo(-4, -16);
    ctx.lineTo(0, -6);
    ctx.moveTo(8, -6);
    ctx.lineTo(4, -16);
    ctx.lineTo(0, -6);
    ctx.fill();
    ctx.fillStyle = "#fff";
    ctx.fillRect(-3, 4, 3, 3);
    ctx.fillRect(2, 6, 3, 3);
    ctx.fillRect(5, 2, 2, 2);
    ctx.restore();
    drawLabel(ctx, "Spotty", x, y - 22 * d, "#fdba74");
  }

  function drawDog(ctx, x, y, d, color, label) {
    /* polish9: presence marker ring for named cast */
    if (label) {
      var pulse = 0.45 + 0.35 * Math.sin(performance.now() / 420);
      ctx.strokeStyle = "rgba(253, 224, 71, " + pulse + ")";
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.ellipse(x, y + 4 * d, 18 * d, 8 * d, 0, 0, Math.PI * 2);
      ctx.stroke();
      ctx.fillStyle = "rgba(251, 191, 36, " + (pulse * 0.18) + ")";
      ctx.beginPath();
      ctx.ellipse(x, y + 4 * d, 16 * d, 7 * d, 0, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.save();
    ctx.translate(x, y);
    ctx.scale(d, d);
    ctx.fillStyle = color || "#c4a574";
    ctx.beginPath();
    ctx.ellipse(0, 2, 14, 7, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.beginPath();
    ctx.ellipse(12, -2, 6, 5, 0.2, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = "#78350f";
    ctx.fillRect(-2, -1, 16, 3);
    ctx.restore();
    if (label) drawLabel(ctx, label, x, y - 14 * d, "#fde68a");
  }

  /* polish9: soft hundreds-of-dogs silhouette flock (ambient dots) near Mars cave only */
  function drawDogFlockTease(ctx, caveX, caveY, caveD, t) {
    for (var i = 0; i < 48; i++) {
      var ang = (i / 48) * Math.PI * 2 + t * 0.15;
      var rad = 70 + (i % 7) * 9 + Math.sin(t * 0.8 + i) * 6;
      var fx = caveX + Math.cos(ang) * rad * 0.55 * caveD;
      var fy = caveY + Math.sin(ang * 1.1) * rad * 0.28 * caveD + 10;
      var a = 0.12 + 0.1 * Math.sin(t * 2 + i * 0.4);
      ctx.fillStyle = "rgba(28, 25, 23, " + a + ")";
      ctx.beginPath();
      ctx.ellipse(fx, fy, 3.2 * caveD, 1.6 * caveD, ang * 0.2, 0, Math.PI * 2);
      ctx.fill();
    }
    drawLabel(ctx, "hundreds of dogs · tease", caveX, caveY + 78 * caveD, "rgba(214, 211, 209, 0.55)");
  }

  function drawAstronaut(ctx, x, y, d, suit, name) {
    ctx.save();
    ctx.translate(x, y);
    ctx.scale(d, d);
    ctx.fillStyle = suit;
    ctx.beginPath();
    ctx.ellipse(0, 4, 11, 14, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = "#e2e8f0";
    ctx.beginPath();
    ctx.arc(0, -10, 8, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = "#38bdf8";
    ctx.beginPath();
    ctx.arc(0, -10, 5, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();
    drawLabel(ctx, name, x, y - 26 * d, "#e2e8f0");
  }

  function drawPerson(ctx, x, y, d, hue) {
    ctx.save();
    ctx.translate(x, y);
    ctx.scale(d, d);
    ctx.fillStyle = "hsl(" + hue + ",45%,55%)";
    ctx.fillRect(-5, -2, 10, 14);
    ctx.fillStyle = "#fcd34d";
    ctx.beginPath();
    ctx.arc(0, -8, 5, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();
  }

  function drawHot(ctx, h, p, near) {
    ctx.beginPath();
    ctx.ellipse(p.x, p.y + 8, 22 * p.d, 10 * p.d, 0, 0, Math.PI * 2);
    ctx.fillStyle = near ? "rgba(250,204,21,0.35)" : "rgba(148,163,184,0.2)";
    ctx.fill();
    if (near) {
      ctx.strokeStyle = "rgba(250,204,21,0.9)";
      ctx.lineWidth = 2;
      ctx.stroke();
    }
  }

  function drawJimmySuit(ctx, x, y, d, jet) {
    ctx.save();
    ctx.translate(x, y);
    ctx.scale(d, d);
    /* polish7: bigger jetpack plume when escaping / ability-fired */
    var jetOn = jet > 0;
    var plume = jetOn ? 22 + jet * 28 : 8;
    ctx.fillStyle = jetOn ? "rgba(56,189,248,0.9)" : "rgba(251,146,60,0.55)";
    ctx.beginPath();
    ctx.moveTo(-6, 12);
    ctx.lineTo(0, 30 + plume);
    ctx.lineTo(6, 12);
    ctx.fill();
    if (jetOn) {
      ctx.fillStyle = "rgba(251,191,36,0.85)";
      ctx.beginPath();
      ctx.moveTo(-3, 14);
      ctx.lineTo(0, 26 + plume * 0.65);
      ctx.lineTo(3, 14);
      ctx.fill();
    }
    ctx.fillStyle = "#fb923c";
    ctx.beginPath();
    ctx.ellipse(0, 0, 13, 11, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = "#e2e8f0";
    ctx.strokeStyle = "#94a3b8";
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.arc(0, -2, 10, 0, Math.PI * 2);
    ctx.stroke();
    ctx.fillStyle = "#ef4444";
    ctx.fillRect(-7, -15, 14, 5);
    ctx.restore();
    drawLabel(ctx, "Jimmy", x, y - 22 * d, "#fdba74");
  }

  function drawMechJames(ctx, x, y, d) {
    ctx.save();
    ctx.translate(x, y);
    ctx.scale(d, d);
    // 1000-story silhouette — tall stack
    ctx.fillStyle = "rgba(74,222,128,0.9)";
    ctx.fillRect(-18, -180, 36, 200);
    ctx.fillStyle = "rgba(22,101,52,0.95)";
    for (var i = 0; i < 12; i++) {
      ctx.fillRect(-16, -170 + i * 14, 32, 3);
    }
    ctx.fillStyle = "#facc15";
    ctx.fillRect(-10, -190, 20, 12);
    ctx.fillStyle = "#fff";
    ctx.font = "bold 10px system-ui";
    ctx.textAlign = "center";
    ctx.fillText("1000", 0, -195);
    ctx.restore();
    drawLabel(ctx, "James mech", x, y - 210 * d, "#86efac");
  }

  function drawInvader(ctx, x, y, d) {
    ctx.save();
    ctx.translate(x, y);
    ctx.scale(d, d);
    ctx.fillStyle = "#ef4444";
    ctx.fillRect(-16, -20, 32, 28);
    ctx.fillStyle = "#7f1d1d";
    ctx.fillRect(-22, -8, 8, 18);
    ctx.fillRect(14, -8, 8, 18);
    ctx.fillStyle = "#fef08a";
    ctx.fillRect(-6, -12, 12, 8);
    ctx.restore();
  }

  /* polish6: distant invader mech silhouettes (visual tease — no combat system) */
  function drawDistantInvaderSilhouettes(ctx, ep, w, h, t) {
    var nearMars = false;
    if (ep.scene === "mars") nearMars = true;
    else if (ep.scene === "solar" && ep.solarTab === "mars") nearMars = true;
    else if (ep.scene === "space") {
      var marsB = bodyById(ep, "mars");
      if (marsB) nearMars = Math.hypot((ep.px || 0) - marsB.x, (ep.py || 0) - marsB.y) < 220;
    } else if (ep.scene === "station") {
      /* when player near to_mech / Mars-side of station */
      var dMech = Math.hypot((ep.px || 0) - 780, (ep.py || 0) - 500);
      nearMars = dMech < 220;
    }
    if (ep.scene === "mars") {
      var dCave = Math.hypot((ep.px || 450) - 450, (ep.py || 500) - 360);
      nearMars = true;
    }
    if (!nearMars && ep.scene !== "mars" && !(ep.scene === "solar" && ep.solarTab === "mars")) return;
    var alpha = ep.scene === "mars" ? 0.55 : 0.38;
    var n = 5;
    for (var i = 0; i < n; i++) {
      var sx = w * (0.08 + i * 0.2) + Math.sin(t * 0.15 + i) * 8;
      var sy = h * (0.12 + (i % 3) * 0.05) + Math.cos(t * 0.12 + i * 0.7) * 4;
      var sc = 0.45 + (i % 3) * 0.12;
      ctx.save();
      ctx.globalAlpha = alpha * (0.7 + 0.3 * Math.sin(t * 0.4 + i));
      ctx.fillStyle = "#3f0a0a";
      ctx.beginPath();
      ctx.moveTo(sx - 10 * sc, sy + 18 * sc);
      ctx.lineTo(sx - 6 * sc, sy - 28 * sc);
      ctx.lineTo(sx + 6 * sc, sy - 28 * sc);
      ctx.lineTo(sx + 10 * sc, sy + 18 * sc);
      ctx.closePath();
      ctx.fill();
      ctx.fillRect(sx - 16 * sc, sy - 10 * sc, 8 * sc, 14 * sc);
      ctx.fillRect(sx + 8 * sc, sy - 10 * sc, 8 * sc, 14 * sc);
      ctx.fillStyle = "#7f1d1d";
      ctx.fillRect(sx - 4 * sc, sy - 34 * sc, 8 * sc, 6 * sc);
      ctx.restore();
    }
    ctx.globalAlpha = 1;
    ctx.fillStyle = "rgba(254, 202, 202, " + (alpha * 0.85) + ")";
    ctx.font = "bold 11px system-ui,sans-serif";
    ctx.textAlign = "center";
    ctx.fillText("Invader mechs · distant silhouette tease", w * 0.5, h * 0.08);
  }


  function drawCrewFrog(ctx, x, y, d, color, suit, name) {
    ctx.save();
    ctx.translate(x, y);
    ctx.scale(d, d);
    if (suit) {
      ctx.fillStyle = "#e2e8f0";
      ctx.beginPath();
      ctx.ellipse(0, 0, 15, 13, 0, 0, Math.PI * 2);
      ctx.fill();
      ctx.strokeStyle = "#38bdf8";
      ctx.lineWidth = 1.5;
      ctx.stroke();
      ctx.fillStyle = "rgba(56,189,248,0.55)";
      ctx.beginPath();
      ctx.arc(0, -2, 6, 0, Math.PI * 2);
      ctx.fill();
    } else {
      ctx.fillStyle = color || "#4ade80";
      ctx.beginPath();
      ctx.ellipse(0, 0, 12, 10, 0, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.restore();
    if (name) drawLabel(ctx, name, x, y + 18 * d, "#e2e8f0");
  }

  function drawShipSilhouette(ctx, x, y, d) {
    ctx.save();
    ctx.translate(x, y);
    ctx.scale(d, d);
    ctx.fillStyle = "rgba(226,232,240,0.55)";
    ctx.beginPath();
    ctx.moveTo(0, -36);
    ctx.lineTo(14, 8);
    ctx.lineTo(-14, 8);
    ctx.closePath();
    ctx.fill();
    ctx.fillStyle = "rgba(56,189,248,0.5)";
    ctx.fillRect(-5, -8, 10, 12);
    ctx.restore();
  }

  function adjustZoom(ep, delta) {
    if (!ep) return 1;
    ep.zoom = Math.max(0.55, Math.min(2.2, (ep.zoom || 1) + (delta || 0)));
    return ep.zoom;
  }

  function render(ctx, ep, w, h, t) {
    /* Always opaque-clear first so ranch/forest never leaks under space */
    ctx.fillStyle = "#020617";
    ctx.fillRect(0, 0, w, h);
    if (!ep.active) return;
    /* Orbit: lock cam on planet — only froggy orbits; starfield/plane stay screen-stable */
    if (ep.inOrbit && ep.orbitPlanet) {
      spaceCam.x = ep.orbitPlanet.x;
      spaceCam.y = ep.orbitPlanet.y;
    } else {
      spaceCam.x = ep.px;
      spaceCam.y = ep.py;
    }
    spaceCam.zoom = ep.zoom != null ? ep.zoom : 1;
    var bg = SCENES[ep.scene].bg;
    if (ep.shake > 0) {
      ctx.save();
      ctx.translate((Math.random() - 0.5) * ep.shake * 14, (Math.random() - 0.5) * ep.shake * 14);
    }

    if (bg === "space") {
      drawStars(ctx, ep, w, h, t);
      if (ep.scene !== "space") {
        drawEarth(ctx, w, h, t, {
          cx: w * (ep.scene === "starship" ? 0.78 : 0.16),
          cy: h * (ep.scene === "starship" ? 0.28 : 0.74),
          r: Math.min(w, h) * (ep.scene === "mech" ? 0.14 : 0.2),
        });
      }
    }
    else if (bg === "station") {
      var g = ctx.createLinearGradient(0, 0, 0, h);
      g.addColorStop(0, "#0f172a");
      g.addColorStop(1, "#1e293b");
      ctx.fillStyle = g;
      ctx.fillRect(0, 0, w, h);
      ctx.fillStyle = "rgba(51,65,85,0.85)";
      ctx.fillRect(0, h * 0.55, w, h * 0.45);
      ctx.fillStyle = "rgba(148,163,184,0.25)";
      ctx.fillRect(w * 0.1, h * 0.2, w * 0.8, h * 0.12);
    } else if (bg === "mars") {
      var mg = ctx.createLinearGradient(0, 0, 0, h);
      mg.addColorStop(0, "#1c1917");
      mg.addColorStop(0.4, "#7c2d12");
      mg.addColorStop(1, "#9a3412");
      ctx.fillStyle = mg;
      ctx.fillRect(0, 0, w, h);
      ctx.fillStyle = "rgba(254,215,170,0.15)";
      ctx.beginPath();
      ctx.ellipse(w * 0.5, h * 0.7, w * 0.45, h * 0.18, 0, 0, Math.PI * 2);
      ctx.fill();
    } else if (bg === "cave") {
      ctx.fillStyle = "#1c1917";
      ctx.fillRect(0, 0, w, h);
      ctx.fillStyle = "#292524";
      ctx.beginPath();
      ctx.moveTo(0, h * 0.3);
      ctx.quadraticCurveTo(w * 0.5, h * 0.05, w, h * 0.3);
      ctx.lineTo(w, h);
      ctx.lineTo(0, h);
      ctx.fill();
      ctx.fillStyle = "rgba(120,53,15,0.35)";
      ctx.fillRect(0, h * 0.7, w, h * 0.3);
    }

    var hots = hotspotsFor(ep);
    var near = nearestHotspot(ep, 75);
    for (var hi = 0; hi < hots.length; hi++) {
      var hh = hots[hi];
      // skip dynamic jimmy hotspot ring drawn with jimmy
      if (hh.id === "jimmy") continue;
      var hp = worldToScreen(hh.x, hh.y, w, h);
      drawHot(ctx, hh, hp, near && near.id === hh.id);
    }

    // Scene actors
    if (ep.scene === "starship") {
      // pad
      var pad = worldToScreen(450, 360, w, h);
      ctx.fillStyle = "rgba(100,116,139,0.7)";
      ctx.beginPath();
      ctx.ellipse(pad.x, pad.y, 90 * pad.d, 28 * pad.d, 0, 0, Math.PI * 2);
      ctx.fill();
      ctx.strokeStyle = "#38bdf8";
      ctx.lineWidth = 2;
      ctx.stroke();
      drawLabel(ctx, "STARSHIP", pad.x, pad.y - 36 * pad.d, "#7dd3fc");
      var sp = worldToScreen(ep.spotty.x, ep.spotty.y, w, h);
      ctx.beginPath();
      ctx.arc(sp.x, sp.y, 34 * sp.d * (1 + 0.1 * Math.sin(t * 2.6)), 0, Math.PI * 2);
      ctx.strokeStyle = "rgba(251,146,60,0.65)";
      ctx.lineWidth = 2.4;
      ctx.stroke();
      drawSpotty(ctx, sp.x, sp.y, sp.d);
      drawLabel(ctx, "Commander on deck", sp.x, sp.y + 28 * sp.d, "#fdba74");
      if (ep.crew && ep.crew.members) {
        for (var sci = 0; sci < ep.crew.members.length; sci++) {
          var scm = ep.crew.members[sci];
          var scp = worldToScreen(scm.x, scm.y, w, h);
          drawCrewFrog(ctx, scp.x, scp.y, scp.d * 0.8, scm.color, false, scm.name);
        }
      }
    }

    if (ep.scene === "space") {
      drawSolarSystemScene(ctx, ep, w, h, t);
      drawDistantInvaderSilhouettes(ctx, ep, w, h, t);

      var jp = worldToScreen(ep.jimmy.x, ep.jimmy.y, w, h);
      drawHot(ctx, { x: ep.jimmy.x, y: ep.jimmy.y }, jp, near && near.id === "jimmy");
      drawJimmySuit(ctx, jp.x, jp.y, jp.d, ep.jimmy.jet);

      var gp = worldToScreen(ep.germy.x, ep.germy.y, w, h);
      drawDog(ctx, gp.x, gp.y, gp.d * 0.9, "#b45309", "Germy");
      var dp = worldToScreen(ep.daisy.x, ep.daisy.y, w, h);
      drawDog(ctx, dp.x, dp.y, dp.d * 0.85, "#d6d3d1", "Daisy Dachshund");

      if (ep.crew && ep.crew.members) {
        for (var cri = 0; cri < ep.crew.members.length; cri++) {
          var crm = ep.crew.members[cri];
          if (crm.id === ep.frogId) continue;
          var crp = worldToScreen(crm.x, crm.y, w, h);
          drawCrewFrog(ctx, crp.x, crp.y, crp.d * 0.85, crm.color, crm.suit, crm.name);
        }
      }
      if (ep.travelMode === "ship") {
        var shipP = worldToScreen(ep.px, ep.py - 18, w, h);
        drawShipSilhouette(ctx, shipP.x, shipP.y - 20 * shipP.d, shipP.d);
      }
    }

    if (ep.scene === "station") {
      for (var pi = 0; pi < ep.people.length; pi++) {
        var pe = ep.people[pi];
        var pp = worldToScreen(pe.x, pe.y + Math.sin(pe.phase) * 3, w, h);
        drawPerson(ctx, pp.x, pp.y, pp.d * 0.7, pe.hue);
      }
      drawLabel(ctx, "~20 people", w * 0.5, h * 0.52, "rgba(226,232,240,0.7)");
      var ap = worldToScreen(ep.alex.x, ep.alex.y, w, h);
      /* polish5: Alex / Fred presence pulse rings */
      ctx.beginPath();
      ctx.arc(ap.x, ap.y, 28 * ap.d * (1 + 0.08 * Math.sin(t * 3)), 0, Math.PI * 2);
      ctx.strokeStyle = "rgba(56,189,248,0.55)";
      ctx.lineWidth = 2;
      ctx.stroke();
      drawAstronaut(ctx, ap.x, ap.y, ap.d, "#64748b", "Alex");
      var fp = worldToScreen(ep.fred.x, ep.fred.y, w, h);
      ctx.beginPath();
      ctx.arc(fp.x, fp.y, 28 * fp.d * (1 + 0.08 * Math.sin(t * 3 + 1)), 0, Math.PI * 2);
      ctx.strokeStyle = "rgba(148,163,184,0.55)";
      ctx.lineWidth = 2;
      ctx.stroke();
      drawAstronaut(ctx, fp.x, fp.y, fp.d, "#475569", "Fred");
    }

    if (ep.scene === "solar") {
      /* polish5: clearer planet moons picker — big tabs, selected card, planet discs */
      var panelX = w * 0.06, panelY = h * 0.14, panelW = w * 0.88, panelH = h * 0.62;
      ctx.fillStyle = "rgba(8, 15, 32, 0.88)";
      ctx.fillRect(panelX, panelY, panelW, panelH);
      ctx.strokeStyle = "#38bdf8";
      ctx.lineWidth = 3;
      ctx.strokeRect(panelX, panelY, panelW, panelH);
      /* Planet tab pills */
      var tabMars = { x: w * 0.22, y: h * 0.2, w: 150, h: 36 };
      var tabNep = { x: w * 0.58, y: h * 0.2, w: 170, h: 36 };
      function drawTab(tab, label, on, planetColor) {
        ctx.fillStyle = on ? "rgba(250,204,21,0.28)" : "rgba(30,41,59,0.9)";
        ctx.strokeStyle = on ? "#facc15" : "#64748b";
        ctx.lineWidth = on ? 3 : 1.5;
        ctx.beginPath();
        ctx.rect(tab.x, tab.y, tab.w, tab.h);
        ctx.fill(); ctx.stroke();
        ctx.fillStyle = planetColor;
        ctx.beginPath();
        ctx.arc(tab.x + 22, tab.y + tab.h * 0.5, 10, 0, Math.PI * 2);
        ctx.fill();
        ctx.fillStyle = on ? "#fef3c7" : "#e2e8f0";
        ctx.font = "bold 13px system-ui,sans-serif";
        ctx.textAlign = "left";
        ctx.fillText(label, tab.x + 40, tab.y + tab.h * 0.62);
      }
      drawTab(tabMars, "Mars · 2 moons", ep.solarTab === "mars", "#ef4444");
      drawTab(tabNep, "Neptune · 14 moons", ep.solarTab === "neptune", "#3b82f6");
      drawLabel(ctx, "◀ tabs · Ability cycles moon · INTERACT Go ▶", w * 0.5, h * 0.265, "#94a3b8");
      var moons = ep.solarTab === "mars" ? MARS_MOONS : NEPTUNE_MOONS;
      var cols = ep.solarTab === "mars" ? 2 : 4;
      var cardW = ep.solarTab === "mars" ? 150 : 110;
      var cardH = 44;
      for (var mi = 0; mi < moons.length; mi++) {
        var col = mi % cols;
        var row = Math.floor(mi / cols);
        var mx = w * 0.14 + col * ((panelW - w * 0.1) / cols);
        var my = h * 0.32 + row * (cardH + 10);
        var sel = mi === ep.solarPick;
        ctx.fillStyle = sel ? "rgba(250,204,21,0.42)" : "rgba(51,65,85,0.75)";
        ctx.fillRect(mx - cardW * 0.5, my - cardH * 0.5, cardW, cardH);
        ctx.strokeStyle = sel ? "#fde68a" : "rgba(148,163,184,0.45)";
        ctx.lineWidth = sel ? 3 : 1.2;
        ctx.strokeRect(mx - cardW * 0.5, my - cardH * 0.5, cardW, cardH);
        /* Moon disc */
        ctx.fillStyle = sel ? "#fef3c7" : "#cbd5e1";
        ctx.beginPath();
        ctx.arc(mx - cardW * 0.32, my, sel ? 11 : 8, 0, Math.PI * 2);
        ctx.fill();
        if (sel) {
          ctx.fillStyle = "#fbbf24";
          ctx.font = "bold 14px system-ui,sans-serif";
          ctx.textAlign = "center";
          ctx.fillText("▶", mx - cardW * 0.5 + 12, my + 5);
          ctx.fillText("◀", mx + cardW * 0.5 - 12, my + 5);
        }
        ctx.fillStyle = sel ? "#fffbeb" : "#e2e8f0";
        ctx.font = (sel ? "bold 14px" : "bold 12px") + " system-ui,sans-serif";
        ctx.textAlign = "left";
        ctx.fillText(moons[mi].name, mx - cardW * 0.18, my + 1);
        /* polish8: clearer destination tags */
        ctx.fillStyle = sel ? "#fde68a" : "#94a3b8";
        ctx.font = "10px system-ui,sans-serif";
        var tag = ep.solarTab === "mars" ? "Mars moon" : "Neptune moon";
        ctx.fillText(tag, mx - cardW * 0.18, my + 14);
      }
      var pick = moons[clamp(ep.solarPick, 0, moons.length - 1)];
      ctx.fillStyle = "rgba(15,23,42,0.85)";
      ctx.fillRect(w * 0.18, h * 0.66, w * 0.64, 48);
      ctx.strokeStyle = "#facc15";
      ctx.lineWidth = 2;
      ctx.strokeRect(w * 0.18, h * 0.66, w * 0.64, 48);
      var destLine = ep.solarTab === "mars"
        ? ("☾ " + pick.name + " · Mars moon → Mars cave")
        : ("♆ " + pick.name + " · Neptune moon · stub stop");
      drawLabel(ctx, "Selected destination", w * 0.5, h * 0.682, "#94a3b8");
      drawLabel(ctx, destLine, w * 0.5, h * 0.712, "#fde68a");
      /* Soft beacon above selected card row */
      var pulse = 0.4 + 0.35 * Math.sin(t * 3.2);
      ctx.strokeStyle = "rgba(250, 204, 21," + pulse + ")";
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.arc(w * 0.5, h * 0.3, 18 + Math.sin(t * 2) * 3, 0, Math.PI * 2);
      ctx.stroke();
    }

    if (ep.scene === "mars") {
      drawDistantInvaderSilhouettes(ctx, ep, w, h, t);
      var cave = worldToScreen(450, 360, w, h);
      ctx.fillStyle = "#1c1917";
      ctx.beginPath();
      ctx.ellipse(cave.x, cave.y, 50 * cave.d, 38 * cave.d, 0, 0, Math.PI * 2);
      ctx.fill();
      /* polish7: cave mouth glow + labeled hooks (3-level secret + back-door) — tease only */
      var nearCave = Math.hypot((ep.px || 450) - 450, (ep.py || 500) - 360) < 220;
      var pulse = 0.55 + 0.45 * Math.sin(t * 3.2);
      ctx.strokeStyle = "rgba(251, 146, 60," + (nearCave ? 0.55 + pulse * 0.35 : 0.25) + ")";
      ctx.lineWidth = nearCave ? 3 : 1.5;
      ctx.beginPath();
      ctx.ellipse(cave.x, cave.y, 54 * cave.d, 42 * cave.d, 0, 0, Math.PI * 2);
      ctx.stroke();
      drawLabel(ctx, "Mars cave", cave.x, cave.y - 58 * cave.d, "#fdba74");
      drawDogFlockTease(ctx, cave.x, cave.y, cave.d, t);
      if (nearCave) {
        drawLabel(ctx, "3-level secret · hook", cave.x, cave.y - 40 * cave.d, "#fde68a");
        drawLabel(ctx, "back-door · hook", cave.x, cave.y + 48 * cave.d, "#93c5fd");
        drawLabel(ctx, "Lv1 · Lv2 · Dog chamber", cave.x, cave.y + 62 * cave.d, "#d6d3d1");
      }
      drawLabel(ctx, "☾ Phobos · Deimos · Mars moons overhead", w * 0.5, h * 0.14, "#fed7aa");
      /* polish8: soft destination beacons for Mars moons */
      var phx = w * 0.28, phy = h * 0.2;
      var dex = w * 0.72, dey = h * 0.18;
      var bp = 0.4 + 0.3 * Math.sin(t * 2.8);
      ctx.strokeStyle = "rgba(253, 186, 116," + bp + ")";
      ctx.lineWidth = 2;
      ctx.beginPath(); ctx.arc(phx, phy, 22 + Math.sin(t * 2) * 3, 0, Math.PI * 2); ctx.stroke();
      ctx.beginPath(); ctx.arc(dex, dey, 16 + Math.cos(t * 2.2) * 2, 0, Math.PI * 2); ctx.stroke();
      drawLabel(ctx, "Phobos", phx, phy + 32, "#fdba74");
      drawLabel(ctx, "Deimos", dex, dey + 28, "#fdba74");
    }

    if (ep.scene === "solar" && ep.solarTab === "mars") {
      drawDistantInvaderSilhouettes(ctx, ep, w, h, t);
    }

    if (ep.scene === "station") {
      var dMechPad = Math.hypot(ep.px - 780, ep.py - 500);
      if (dMechPad < 240) drawDistantInvaderSilhouettes(ctx, ep, w, h, t);
    }

    if (ep.scene === "cave1" || ep.scene === "cave2") {
      drawLabel(ctx, ep.scene === "cave1" ? "Passageway · level 1" : "Passageway · level 2", w * 0.5, h * 0.16, "#d6d3d1");
      if (ep.scene === "cave2") {
        for (var d2 = 0; d2 < 40; d2++) {
          var dog2 = ep.dogs[d2];
          var d2p = worldToScreen(dog2.x, dog2.y, w, h);
          drawDog(ctx, d2p.x, d2p.y, d2p.d * dog2.size * 0.55, dog2.tone, null);
        }
        if (ep.foundGarage) drawLabel(ctx, "Garage ✓", w * 0.25, h * 0.55, "#86efac");
      }
    }

    if (ep.scene === "cave3") {
      drawLabel(ctx, "Dog chamber · hundreds of dogs", w * 0.5, h * 0.14, "#fde68a");
      for (var d3 = 0; d3 < ep.dogs.length; d3++) {
        var dog3 = ep.dogs[d3];
        var d3p = worldToScreen(dog3.x, dog3.y, w, h);
        drawDog(ctx, d3p.x, d3p.y, d3p.d * dog3.size * 0.5, dog3.tone, null);
      }
      var kp = worldToScreen(450, 340, w, h);
      drawDog(ctx, kp.x, kp.y, kp.d * 1.35, "#a16207", "King Germy");
      // crown
      ctx.fillStyle = "#facc15";
      ctx.beginPath();
      ctx.moveTo(kp.x - 10, kp.y - 28 * kp.d);
      ctx.lineTo(kp.x, kp.y - 40 * kp.d);
      ctx.lineTo(kp.x + 10, kp.y - 28 * kp.d);
      ctx.fill();
      if (ep.foundCompartment) drawLabel(ctx, "Compartment ✓", w * 0.75, h * 0.58, "#86efac");
      if (ep.foundEscape) drawLabel(ctx, "Escape open", w * 0.2, h * 0.65, "#93c5fd");
    }

    if (ep.scene === "escape") {
      drawLabel(ctx, "Secret back-door escape hallway", w * 0.5, h * 0.2, "#93c5fd");
      var door = worldToScreen(450, 300, w, h);
      ctx.fillStyle = "#44403c";
      ctx.fillRect(door.x - 28 * door.d, door.y - 50 * door.d, 56 * door.d, 70 * door.d);
      ctx.fillStyle = "#facc15";
      ctx.beginPath();
      ctx.arc(door.x + 16 * door.d, door.y - 10 * door.d, 4 * door.d, 0, Math.PI * 2);
      ctx.fill();
      drawLabel(ctx, "Door", door.x, door.y + 40 * door.d, "#e7e5e4");
    }

    if (ep.scene === "mech") {
      drawLabel(ctx, "Foreign invader mechs vs James 1000-story mech", w * 0.5, h * 0.12, "#fca5a5");
      var mp = worldToScreen(450, 620, w, h);
      drawMechJames(ctx, mp.x, mp.y, mp.d * 0.85);
      for (var ii = 0; ii < ep.invaders.length; ii++) {
        var inv = ep.invaders[ii];
        if (inv.hp <= 0) continue;
        var ip = worldToScreen(inv.x, inv.y, w, h);
        drawInvader(ctx, ip.x, ip.y, ip.d);
      }
      // HP bars
      ctx.fillStyle = "rgba(0,0,0,0.5)";
      ctx.fillRect(w * 0.1, h * 0.86, w * 0.35, 10);
      ctx.fillStyle = "#4ade80";
      ctx.fillRect(w * 0.1, h * 0.86, w * 0.35 * (ep.mechHp / 100), 10);
      ctx.fillStyle = "rgba(0,0,0,0.5)";
      ctx.fillRect(w * 0.55, h * 0.86, w * 0.35, 10);
      ctx.fillStyle = "#ef4444";
      ctx.fillRect(w * 0.55, h * 0.86, w * 0.35 * (ep.invaderHp / 100), 10);
      drawLabel(ctx, "James mech", w * 0.27, h * 0.845, "#86efac");
      drawLabel(ctx, "Invaders", w * 0.72, h * 0.845, "#fca5a5");
      if (ep.mechWon) drawLabel(ctx, "WIN!", w * 0.5, h * 0.5, "#fde68a");
    }

    // Player (primary froggy — suit or ship-mode jet)
    var pp2 = worldToScreen(ep.px, ep.py, w, h);
    if (ep.travelMode === "suit") {
      drawCrewFrog(ctx, pp2.x, pp2.y, pp2.d, ep.frogColor || "#4ade80", true, ep.frogName || null);
      if (ep.jet > 0) {
        ctx.fillStyle = "rgba(56,189,248,0.75)";
        ctx.beginPath();
        ctx.moveTo(pp2.x - 6, pp2.y + 10 * pp2.d);
        ctx.lineTo(pp2.x, pp2.y + (22 + ep.jet * 28) * pp2.d);
        ctx.lineTo(pp2.x + 6, pp2.y + 10 * pp2.d);
        ctx.fill();
      }
    } else {
      drawFrog(ctx, pp2.x, pp2.y, pp2.d, ep.facing, ep.jet);
    }

    /* polish5: Escape / thruster leave hint when orbit-locked */
    if (ep.inOrbit) {
      var banW = Math.min(w * 0.86, 420);
      var banX = (w - banW) * 0.5;
      var banY = h * 0.78;
      ctx.fillStyle = "rgba(8, 20, 40, 0.88)";
      ctx.fillRect(banX, banY, banW, 52);
      ctx.strokeStyle = "#38bdf8";
      ctx.lineWidth = 2.5;
      ctx.strokeRect(banX, banY, banW, 52);
      ctx.fillStyle = "#e0f2fe";
      ctx.font = "bold 14px system-ui,sans-serif";
      ctx.textAlign = "center";
      ctx.fillText("ORBIT · " + (ep.orbitPlanet ? ep.orbitPlanet.name : "planet"), w * 0.5, banY + 20);
      ctx.fillStyle = "#fde68a";
      ctx.font = "bold 12px system-ui,sans-serif";
      ctx.fillText("ESCAPE button / Esc key  ·  or Ability = hard thruster", w * 0.5, banY + 40);
    }

    // Soft vignette (presentation)
    var vig = ctx.createRadialGradient(w * 0.5, h * 0.5, h * 0.2, w * 0.5, h * 0.5, h * 0.75);
    vig.addColorStop(0, "rgba(0,0,0,0)");
    vig.addColorStop(1, "rgba(2, 6, 18, 0.32)");
    ctx.fillStyle = vig;
    ctx.fillRect(0, 0, w, h);

    // Scene title strip
    ctx.fillStyle = "rgba(0,0,0,0.45)";
    ctx.fillRect(0, 0, w, 28);
    ctx.font = "bold 13px system-ui,sans-serif";
    ctx.textAlign = "left";
    ctx.fillStyle = "#e2e8f0";
    ctx.fillText("🚀 " + (SCENES[ep.scene] ? SCENES[ep.scene].name : ep.scene), 12, 18);

    if (ep.shake > 0) ctx.restore();
  }

  function getHud(ep) {
    if (!ep || !ep.active) return null;
    var near = nearestHotspot(ep, 75);
    var tip = "";
    if (ep.toastT > 0) tip = ep.toast;
    else if (ep.inOrbit) tip = "ORBIT · only you spin · ESCAPE / Esc · stars stay fixed";
    else if (ep.orbitPull) tip = "Gravity pull · " + (ep.orbitPull.planet ? ep.orbitPull.planet.name : "planet") + " · drift in to lock";
    else if (ep.scene === "space" && ep.travelMode === "ship") tip = "SHIP · Exit ship = suit EVA · wheel zoom · Mars hotspot";
    else if (ep.scene === "space" && ep.travelMode === "suit") tip = "SUIT JET · Board ship = rocket · wheel zoom · Mars";
    else if (near) tip = near.tip + " · INTERACT / E";
    else tip = "Steer · find hotspots · Lobby returns to title";
    return {
      label: SCENES[ep.scene] ? SCENES[ep.scene].name : "Space",
      tip: tip,
      near: near,
      catches: ep.jimmyCatches,
      cave: ep.caveProgress,
      mechWon: ep.mechWon,
      inOrbit: !!ep.inOrbit,
      orbitName: ep.orbitPlanet ? ep.orbitPlanet.name : null,
      travelMode: ep.travelMode || "ship",
      zoom: ep.zoom || 1,
    };
  }

  function packState(ep) {
    if (!ep || !ep.active) return null;
    return {
      mode: "space",
      scene: ep.scene,
      px: ep.px,
      py: ep.py,
      jimmyCatches: ep.jimmyCatches,
      solarTab: ep.solarTab,
      solarPick: ep.solarPick,
      mechHp: ep.mechHp,
      invaderHp: ep.invaderHp,
      mechWon: ep.mechWon,
    };
  }

  global.FroggiesSpace = {
    MARS_MOONS: MARS_MOONS,
    NEPTUNE_MOONS: NEPTUNE_MOONS,
    SOLAR_DEFS: SOLAR_DEFS,
    SCENES: SCENES,
    solarBodies: solarBodies,
    create: create,
    enter: enter,
    exit: exit,
    isActive: isActive,
    setScene: setScene,
    update: update,
    render: render,
    interact: interact,
    ability: ability,
    leaveOrbit: leaveOrbit,
    tryHardThrustEscape: tryHardThrustEscape,
    planetsFor: planetsFor,
    nearestHotspot: nearestHotspot,
    hotspotsFor: hotspotsFor,
    getHud: getHud,
    packState: packState,
    adjustZoom: adjustZoom,
    earthRanchPad: earthRanchPad,
  };
})(typeof window !== "undefined" ? window : globalThis);
