/* Four Froggies — space episode (story path INSIDE the ranch cab).
   Ben cast only: Spotty, Alex, Fred, Germy, Daisy Dachshund, King Germy;
   ~20 people + hundreds of dogs as anonymous crowds.
   Real moons: Mars Phobos/Deimos; Neptune's 14 named moons (picker stub). */
(function (global) {
  "use strict";

  var MAP = 900;

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
    if (ep.scene === "space") {
      ep.jet = 0.45;
      ep.px += ep.facing * 40;
      toast(ep, (frogId === "james" ? "DASH" : "Boost") + " · jet!");
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

    var speed = ep.scene === "space" ? 160 : 140;
    ep.px = clamp(ep.px + steerX * speed * dt, 40, MAP - 40);
    ep.py = clamp(ep.py + steerY * speed * dt, 60, MAP - 40);
    if (steerX !== 0) ep.facing = steerX > 0 ? 1 : -1;

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
    ctx.fillStyle = "#030712";
    ctx.fillRect(0, 0, w, h);
    for (var i = 0; i < ep.stars.length; i++) {
      var s = ep.stars[i];
      var a = 0.4 + 0.6 * Math.abs(Math.sin(t * 2 + s.tw));
      ctx.fillStyle = "rgba(255,255,255," + a + ")";
      var sx = (s.x / MAP) * w;
      var sy = (s.y / MAP) * h;
      ctx.beginPath();
      ctx.arc(sx, sy, s.r, 0, Math.PI * 2);
      ctx.fill();
    }
  }

  function worldToScreen(x, y, w, h) {
    // Fixed-angle 2.5D-ish: foreshorten Y a bit for phone
    var sx = (x / MAP) * w;
    var sy = h * 0.12 + (y / MAP) * h * 0.78;
    var depth = clamp(0.75 + y / MAP * 0.4, 0.6, 1.2);
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
      ctx.fillStyle = "rgba(56,189,248,0.7)";
      ctx.beginPath();
      ctx.moveTo(-6, 10);
      ctx.lineTo(0, 28 + jet * 20);
      ctx.lineTo(6, 10);
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
    if (jet > 0 || true) {
      ctx.fillStyle = "rgba(251,146,60,0.85)";
      ctx.beginPath();
      ctx.moveTo(-5, 12);
      ctx.lineTo(0, 30 + (jet > 0 ? 18 : 8));
      ctx.lineTo(5, 12);
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

  function render(ctx, ep, w, h, t) {
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
      drawSpotty(ctx, sp.x, sp.y, sp.d);
    }

    if (ep.scene === "space") {
      // moon
      var moon = worldToScreen(700, 140, w, h);
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
      drawAstronaut(ctx, ap.x, ap.y, ap.d, "#64748b", "Alex");
      var fp = worldToScreen(ep.fred.x, ep.fred.y, w, h);
      drawAstronaut(ctx, fp.x, fp.y, fp.d, "#475569", "Fred");
    }

    if (ep.scene === "solar") {
      ctx.fillStyle = "rgba(15,23,42,0.72)";
      ctx.fillRect(w * 0.08, h * 0.18, w * 0.84, h * 0.55);
      ctx.strokeStyle = "#38bdf8";
      ctx.lineWidth = 2;
      ctx.strokeRect(w * 0.08, h * 0.18, w * 0.84, h * 0.55);
      drawLabel(ctx, ep.solarTab === "mars" ? "Mars · 2 moons" : "Neptune · 14 moons", w * 0.5, h * 0.22, "#7dd3fc");
      var moons = ep.solarTab === "mars" ? MARS_MOONS : NEPTUNE_MOONS;
      var cols = ep.solarTab === "mars" ? 2 : 4;
      for (var mi = 0; mi < moons.length; mi++) {
        var col = mi % cols;
        var row = Math.floor(mi / cols);
        var mx = w * 0.18 + col * (w * 0.7 / cols);
        var my = h * 0.3 + row * 28;
        var sel = mi === ep.solarPick;
        ctx.fillStyle = sel ? "rgba(250,204,21,0.35)" : "rgba(51,65,85,0.6)";
        ctx.fillRect(mx - 50, my - 12, 100, 22);
        drawLabel(ctx, moons[mi].name, mx, my + 4, sel ? "#fde68a" : "#e2e8f0");
      }
      drawLabel(ctx, "Ability cycles pick · INTERACT Go / tabs", w * 0.5, h * 0.7, "#94a3b8");
    }

    if (ep.scene === "mars") {
      var cave = worldToScreen(450, 360, w, h);
      ctx.fillStyle = "#1c1917";
      ctx.beginPath();
      ctx.ellipse(cave.x, cave.y, 50 * cave.d, 38 * cave.d, 0, 0, Math.PI * 2);
      ctx.fill();
      drawLabel(ctx, "Mars cave", cave.x, cave.y - 50 * cave.d, "#fdba74");
      drawLabel(ctx, "Phobos · Deimos overhead", w * 0.5, h * 0.14, "#fed7aa");
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
    else if (near) tip = near.tip + " · INTERACT / E";
    else tip = "Steer · find hotspots · Lobby returns to title";
    return {
      label: SCENES[ep.scene] ? SCENES[ep.scene].name : "Space",
      tip: tip,
      near: near,
      catches: ep.jimmyCatches,
      cave: ep.caveProgress,
      mechWon: ep.mechWon,
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
    nearestHotspot: nearestHotspot,
    hotspotsFor: hotspotsFor,
    getHud: getHud,
    packState: packState,
  };
})(typeof window !== "undefined" ? window : globalThis);
