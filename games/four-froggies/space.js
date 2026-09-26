/* Four Froggies — space episode (story path INSIDE the ranch cab).
   Presentation polish: depth-scaled sprites, parallax stars, vignette (original).
   Ben cast only: Spotty, Alex, Fred, Germy, Daisy Dachshund, King Germy;
   ~20 people + hundreds of dogs as anonymous crowds.
   Real moons: Mars Phobos/Deimos; Neptune's 14 named moons (picker stub).
   Ben orbit physics: near planet → gravity pull into orbit; leave via Escape OR hard thruster.
   polish5: clearer moons picker, readable orbit pull rings, Escape/thruster leave banner,
   Spotty/Alex/Fred presence pulse (cast already stubbed).
   polish6: distant invader mech silhouettes when near Mars (visual tease only).
   polish7: Mars cave entrance tease (3-level + back-door labeled hooks); Jimmy jetpack escape visual on ability. */
(function (global) {
  "use strict";

  var MAP = 900;

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

  /** Scene planets/moons that can capture into orbit (Ben-named bodies only). */
  function planetsFor(ep) {
    if (!ep) return [];
    if (ep.scene === "space") {
      return [{ id: "moon", name: "Moon", x: 700, y: 140, r: 55 }];
    }
    if (ep.scene === "mars") {
      return [{ id: "mars", name: "Mars", x: 450, y: 280, r: 90 }];
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
    var cfgR = cfg.softPullRadius || 220;
    ep.orbitPull = null;
    if (bestD < cfgR && bestD > 8) {
      var ang = Math.atan2(ep.py - best.y, ep.px - best.x);
      var pull = (cfg.pullAccel || 420) * (1 - bestD / cfgR) * dt;
      ep.vx = (ep.vx || 0) - Math.cos(ang) * pull;
      ep.vy = (ep.vy || 0) - Math.sin(ang) * pull;
      /* polish5: expose pull strength for readable rings */
      ep.orbitPull = { planet: best, dist: bestD, soft: cfgR, cap: cfg.captureRadius || 120, strength: 1 - bestD / cfgR };
    }
    if (bestD < (cfg.captureRadius || 120)) {
      ep.inOrbit = true;
      ep.orbitPlanet = best;
      ep.orbitAngle = Math.atan2(ep.py - best.y, ep.px - best.x);
      ep.orbitRadius = cfg.orbitAltitude || 78;
      ep.vx = 0;
      ep.vy = 0;
      ep.orbitPull = null;
      toast(ep, "Orbit locked · " + best.name + " · ESCAPE or Ability thruster to leave", 3.4);
    }
  }

  function tickOrbit(ep, dt, steerX, steerY) {
    if (!ep.inOrbit || !ep.orbitPlanet) return false;
    var pl = ep.orbitPlanet;
    var cfg = orbitCfg();
    // Steer adjusts altitude a little; cannot leave by steering alone
    ep.orbitRadius = clamp(
      (ep.orbitRadius || cfg.orbitAltitude) + (steerY || 0) * 40 * dt,
      (pl.r || 40) + 28,
      (cfg.softPullRadius || 220) * 0.85
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
    space: { name: "Space · Moon", bg: "space" },
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
    };
  }

  function seedStars(ep) {
    ep.stars = [];
    for (var i = 0; i < 80; i++) {
      ep.stars.push({
        x: Math.random() * MAP,
        y: Math.random() * MAP,
        r: Math.random() * 1.6 + 0.4,
        tw: Math.random() * Math.PI * 2,
      });
    }
  }

  function enter(ep, opts) {
    opts = opts || {};
    ep.active = true;
    ep.scene = "starship";
    ep.px = 450;
    ep.py = 560;
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
    ep.jimmy = { x: 620, y: 280, vx: 55, vy: -15, jet: 0 };
    ep.toast = "Spotty: Welcome aboard, commander on deck!";
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
      ep.px = 280;
      ep.py = 500;
      ep.jimmy.x = 640;
      ep.jimmy.y = 260;
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
      list.push({ id: "spotty", label: "Spotty", x: ep.spotty.x, y: ep.spotty.y, r: 55, tip: "Talk to Spotty · launch" });
      list.push({ id: "launch", label: "Launch", x: 450, y: 320, r: 60, tip: "Launch into space" });
      list.push({ id: "to_ranch", label: "Ranch", x: 80, y: 820, r: 55, tip: "Return to ranch hub" });
    } else if (s === "space") {
      list.push({ id: "jimmy", label: "Jimmy", x: ep.jimmy.x, y: ep.jimmy.y, r: 48, tip: "Catch Jimmy · jetpack!" });
      list.push({ id: "germy", label: "Germy", x: ep.germy.x, y: ep.germy.y, r: 40, tip: "Germy the doggy" });
      list.push({ id: "daisy", label: "Daisy", x: ep.daisy.x, y: ep.daisy.y, r: 40, tip: "Daisy Dachshund" });
      list.push({ id: "to_station", label: "Station", x: 820, y: 180, r: 55, tip: "Space station ahead" });
      list.push({ id: "to_ship", label: "Starship", x: 80, y: 820, r: 50, tip: "Back to Starship" });
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
      setScene(ep, "space");
      toast(ep, "Spotty: Launch! Jimmy's jetpacking away — catch him!");
      return { ok: true, toast: ep.toast };
    }
    if (id === "jimmy") {
      ep.jimmyCatches++;
      ep.jimmy.x = rand(120, 780);
      ep.jimmy.y = rand(120, 420);
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
      if (jimmyPack) {
        /* existing canon line — no new dialogue */
        toast(ep, "SHIELD up!");
      } else {
        toast(ep, (frogId === "james" ? "DASH" : "Boost") + " · hard thruster!");
      }
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

    if (ep.vx == null) ep.vx = 0;
    if (ep.vy == null) ep.vy = 0;
    if (tickOrbit(ep, dt, steerX, steerY)) {
      if (steerX !== 0) ep.facing = steerX > 0 ? 1 : -1;
    } else {
      tryCaptureOrbit(ep, dt);
      if (ep.inOrbit) {
        /* captured this frame — orbit tick next */
      } else {
        var maxSp = ep.scene === "space" || ep.scene === "mars" ? 170 : 150;
        var accel = 880;
        var friction = 5.5;
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

    // Jimmy constantly getting away
    if (ep.scene === "space") {
      var j = ep.jimmy;
      j.x += j.vx * dt;
      j.y += j.vy * dt;
      if (j.x < 60 || j.x > MAP - 60) j.vx *= -1;
      if (j.y < 80 || j.y > 500) j.vy *= -1;
      j.vx += Math.sin(performance.now() / 400) * 30 * dt;
      j.vy += Math.cos(performance.now() / 350) * 25 * dt;
      j.vx = clamp(j.vx, -110, 110);
      j.vy = clamp(j.vy, -90, 90);
      if (j.jet > 0) j.jet -= dt;
      // Germy / Daisy drift
      ep.germy.x = 480 + Math.sin(performance.now() / 900) * 40;
      ep.germy.y = 380 + Math.cos(performance.now() / 1100) * 30;
      ep.daisy.x = 540 + Math.cos(performance.now() / 800) * 50;
      ep.daisy.y = 410 + Math.sin(performance.now() / 950) * 25;
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

  function drawStars(ctx, ep, w, h, t) {
    var g = ctx.createLinearGradient(0, 0, 0, h);
    g.addColorStop(0, "#020617");
    g.addColorStop(0.55, "#0b1224");
    g.addColorStop(1, "#111827");
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, w, h);

    // Parallax star layers vs player (overworld depth feel)
    var camOffX = (ep.px - MAP * 0.5) * 0.04;
    var camOffY = (ep.py - MAP * 0.5) * 0.03;
    for (var i = 0; i < ep.stars.length; i++) {
      var s = ep.stars[i];
      var layer = (i % 3) + 1;
      var a = 0.35 + 0.65 * Math.abs(Math.sin(t * (1.2 + layer * 0.3) + s.tw));
      ctx.fillStyle = "rgba(255,255,255," + a + ")";
      var sx = (s.x / MAP) * w - camOffX * layer;
      var sy = (s.y / MAP) * h - camOffY * layer;
      ctx.beginPath();
      ctx.arc(sx, sy, s.r * (0.7 + layer * 0.15), 0, Math.PI * 2);
      ctx.fill();
    }
    // Soft nebula wash (original procedural color — not licensed art)
    var neb = ctx.createRadialGradient(w * 0.7, h * 0.25, 10, w * 0.65, h * 0.3, w * 0.45);
    neb.addColorStop(0, "rgba(88, 80, 180, 0.12)");
    neb.addColorStop(0.5, "rgba(30, 64, 120, 0.06)");
    neb.addColorStop(1, "rgba(0,0,0,0)");
    ctx.fillStyle = neb;
    ctx.fillRect(0, 0, w, h);
  }

  var spaceCam = { x: MAP * 0.5, y: MAP * 0.55 };

  function worldToScreen(x, y, w, h, camX, camY) {
    // Fixed-angle 2.5D: isometric-ish foreshorten + depth scale (presentation)
    camX = camX == null ? spaceCam.x : camX;
    camY = camY == null ? spaceCam.y : camY;
    var dx = x - camX;
    var dy = y - camY;
    var sx = w * 0.5 + dx * 0.95 - dy * 0.22;
    var sy = h * 0.42 + dx * 0.18 + dy * 0.62;
    var depth = clamp(0.62 + y / MAP * 0.5 + dy * 0.0002, 0.48, 1.35);
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
      /* approach Mars marker if present in solar path — soft tease from station→mars */
      nearMars = false;
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

  function render(ctx, ep, w, h, t) {
    spaceCam.x = ep.px;
    spaceCam.y = ep.py;
    if (!ep.active) return;
    var bg = SCENES[ep.scene].bg;
    if (ep.shake > 0) {
      ctx.save();
      ctx.translate((Math.random() - 0.5) * ep.shake * 14, (Math.random() - 0.5) * ep.shake * 14);
    }

    if (bg === "space") drawStars(ctx, ep, w, h, t);
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
    }

    if (ep.scene === "space") {
      // moon + polish5 readable pull / orbit rings
      var moon = worldToScreen(700, 140, w, h);
      var cfgVis = orbitCfg();
      var softScr = (cfgVis.softPullRadius || 220) * moon.d * 0.95;
      var capScr = (cfgVis.captureRadius || 120) * moon.d * 0.95;
      var orbScr = (ep.inOrbit ? (ep.orbitRadius || cfgVis.orbitAltitude || 78) : (cfgVis.orbitAltitude || 78)) * moon.d;
      /* Soft pull halo */
      ctx.beginPath();
      ctx.arc(moon.x, moon.y, softScr, 0, Math.PI * 2);
      ctx.strokeStyle = ep.orbitPull ? "rgba(125,211,252,0.55)" : "rgba(125,211,252,0.2)";
      ctx.lineWidth = ep.orbitPull ? 2.4 : 1.2;
      ctx.setLineDash([10, 8]);
      ctx.stroke();
      ctx.setLineDash([]);
      /* Capture ring */
      ctx.beginPath();
      ctx.arc(moon.x, moon.y, capScr, 0, Math.PI * 2);
      ctx.strokeStyle = ep.inOrbit ? "rgba(250,204,21,0.85)" : "rgba(56,189,248,0.45)";
      ctx.lineWidth = 2;
      ctx.stroke();
      if (ep.inOrbit) {
        ctx.beginPath();
        ctx.arc(moon.x, moon.y, orbScr, 0, Math.PI * 2);
        ctx.strokeStyle = "rgba(250,204,21,0.95)";
        ctx.lineWidth = 3;
        ctx.stroke();
        /* orbit chevron at player angle */
        var oa = ep.orbitAngle || 0;
        var ox = moon.x + Math.cos(oa) * orbScr;
        var oy = moon.y + Math.sin(oa) * orbScr;
        ctx.fillStyle = "#fde68a";
        ctx.beginPath();
        ctx.arc(ox, oy, 5, 0, Math.PI * 2);
        ctx.fill();
      } else if (ep.orbitPull) {
        var str = ep.orbitPull.strength || 0;
        ctx.strokeStyle = "rgba(125,211,252," + (0.35 + str * 0.5) + ")";
        ctx.lineWidth = 2 + str * 2;
        ctx.beginPath();
        ctx.moveTo(moon.x, moon.y);
        var pp = worldToScreen(ep.px, ep.py, w, h);
        ctx.lineTo(pp.x, pp.y);
        ctx.stroke();
        drawLabel(ctx, "Gravity pull · " + Math.round(str * 100) + "%", moon.x, moon.y + softScr + 14, "#7dd3fc");
      }
      ctx.fillStyle = "#e2e8f0";
      ctx.beginPath();
      ctx.arc(moon.x, moon.y, 55 * moon.d, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = "rgba(100,116,139,0.35)";
      ctx.beginPath();
      ctx.arc(moon.x - 15, moon.y - 8, 12 * moon.d, 0, Math.PI * 2);
      ctx.arc(moon.x + 18, moon.y + 10, 8 * moon.d, 0, Math.PI * 2);
      ctx.fill();
      drawLabel(ctx, "Moon", moon.x, moon.y + 70 * moon.d, "#cbd5e1");

      var jp = worldToScreen(ep.jimmy.x, ep.jimmy.y, w, h);
      drawHot(ctx, { x: ep.jimmy.x, y: ep.jimmy.y }, jp, near && near.id === "jimmy");
      drawJimmySuit(ctx, jp.x, jp.y, jp.d, ep.jimmy.jet);

      var gp = worldToScreen(ep.germy.x, ep.germy.y, w, h);
      drawDog(ctx, gp.x, gp.y, gp.d * 0.9, "#b45309", "Germy");
      var dp = worldToScreen(ep.daisy.x, ep.daisy.y, w, h);
      drawDog(ctx, dp.x, dp.y, dp.d * 0.85, "#d6d3d1", "Daisy Dachshund");
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
        ctx.fillText(moons[mi].name, mx - cardW * 0.18, my + 5);
      }
      var pick = moons[clamp(ep.solarPick, 0, moons.length - 1)];
      ctx.fillStyle = "rgba(15,23,42,0.85)";
      ctx.fillRect(w * 0.18, h * 0.68, w * 0.64, 36);
      ctx.strokeStyle = "#facc15";
      ctx.lineWidth = 2;
      ctx.strokeRect(w * 0.18, h * 0.68, w * 0.64, 36);
      drawLabel(ctx, "Selected · " + pick.name + (ep.solarTab === "mars" ? " → Mars cave" : " · Neptune stub stop"), w * 0.5, h * 0.705, "#fde68a");
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
      if (nearCave) {
        drawLabel(ctx, "3-level secret · hook", cave.x, cave.y - 40 * cave.d, "#fde68a");
        drawLabel(ctx, "back-door · hook", cave.x, cave.y + 48 * cave.d, "#93c5fd");
        drawLabel(ctx, "Lv1 · Lv2 · Dog chamber", cave.x, cave.y + 62 * cave.d, "#d6d3d1");
      }
      drawLabel(ctx, "Phobos · Deimos overhead", w * 0.5, h * 0.14, "#fed7aa");
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

    // Player
    var pp2 = worldToScreen(ep.px, ep.py, w, h);
    drawFrog(ctx, pp2.x, pp2.y, pp2.d, ep.facing, ep.jet);

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
    else if (ep.inOrbit) tip = "ORBIT · ESCAPE / Esc  ·  Ability = hard thruster leave";
    else if (ep.orbitPull) tip = "Gravity pull · " + (ep.orbitPull.planet ? ep.orbitPull.planet.name : "planet") + " · drift in to lock";
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
    SCENES: SCENES,
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
  };
})(typeof window !== "undefined" ? window : globalThis);
