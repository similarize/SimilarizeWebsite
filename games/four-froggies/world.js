/* Four Froggies — 2.5D ranch hub (fixed-angle / y-sorted layers).
   ranchfeel2 + parity1: Cybertruck water; varied track; crisper silhouettes; Starship approach uses shared canon gold guide.
   polish4: house interiors readable; backyard animals present; dramatic mechs; track hills; Starship destination;
   inviting story hotspots; truck bounce + water spray/bubbles; walk dust.
   garage1: garage door spans full front bay; polish5: day ambient pollen/fireflies; pond ripple rings; track race dust; garage door open-near;
   shared pile-in truck obvious; tiny land shake hook; sparkle on hotspot enter.
   polish6: FF-like depth — stronger hill parallax, soft drop shadows, scale-with-depth props,
   walk cam tilt/bob (via main); 1000-story mech wow tip; story/combat visual teases.
   polish7: zone signs fade-in; mini-map; AI idle bounce + follow lag + chat bubbles (existing lines);
   polish8: Cybertruck angular stainless + light bar + arches; ranch 2.5D porch/chimney smoke/path;
   pond whale breach arcs + fish schools; Blue Bear pet bounce near house (place-bound).
   polish9: frog color nameplates; shared truck aboard frog icons; start/finish gate + lap sparkle;
   brief jump air hang; Optimus kit visual punch on ranch. Canvas lead.
   polish10: UI declutter (mini-map / zone signs / quieter nameplates); tighter friction; truck EXIT anytime;
   solid1: shared solid walls/mechs/trucks via FroggiesCanon.resolveSolid;
   tapsteer1: faster walk/drive;
   eyes1: faceAngle — rotate frog face toward walk dir (idle keeps last); AI too;
   truck1: kid-toy Cybertruck scale; smooth yaw steer (face+drive toward aim);
   track elev follow / crest launch / land bounce via trackElevAt;
   truck2: stronger elev follow / crest / ramp ride-up; EXIT tip always while driving;
   view1: track support pillars under elevated ribbon; spawn from canon outdoor yard;
   polish11: truck yaw follows travel; brief EXIT tip (no sticky billboard); ZOOM ability;
   hop1: HOP ability (Y arc + squash); shove toys/animals/pollen;
   hop2: ranch foot ALWAYS hops (continuous arc cycle); ability HOP = bigger jump;
   qa1: on-foot stand/hop uses trackElevAt; human steer is screen-up on the iso map;
   the apron fill and the lane use that same height.
   hop3: faster loco hop + spam HOP + stack height; mech robots;
   track3: banked turns + rock obstacles (hard bounce) + live monster-truck wheels; more cam zoom-out;
   hop4: faster always-hop carry/plant/launch; humanoid frogs (torso+head, spring legs);
   particle caps; sunset sky shift over play time.
   mobile1: phone+desktop shared UI — smaller/toggle-friendly mini-map + harder particle caps on narrow.
   mech3: Tank big missiles — blast wrecks toys/animals/props.
   mech5: tank blows ONLY Rexy 1000-mech (props stay); distinct vehicle speeds; bigger drive ground.
   pond1: swim in pond + docked submarine at south rim (enter/EXIT).
   mech6: tank blast props + Rexy 1000-mech respawn ~7s.
   mech7: swim POSE (stroke + flat body, no hop); single docked sub hull (no clone).
   mech8: (Three) Rexy 1000-mech respawn restores full mesh after tank blast.
   air1: helipad H + heli (4 seats) + drone pad D + passenger drone (1–2) · fly over ranch.
   mechgun1: story-mech omnigun FIRE — permanent session kill of hit targets (not tank).
   mechgun2: omnigun also blows house/garage/trees/rocks/fish/fences/shrubs (session permanent).
   ~10× map: real roam between ranch house / track / pond / Starship.
   James ranch house: big house, backyard (animals), huge garage (toys + 10/100-story mechs);
   1000-story + trillion-story mechs sit out back (won't fit). Four Cybertrucks + shared pile-in.
   yard1: backyard trees/shrubs/creek/rocks/flowers/fence; soft stream slow/splash;
   park1: EXIT leaves mech/truck at exit pos (no snap-home).
   Pond: big fish + whales. Starship pad connected → space episode.
   Ben-named only. No invented cast/zone/toy names. */
(function (global) {
  "use strict";

  /* mech5: bigger playable green/dirt; house geometry unchanged (canon COMPOUND). */
  var MAP_W = 5600;
  var MAP_H = 4200;

  /* mobile1: density helpers — desktop keeps polish10 caps; narrow phones get harder caps */
  function isNarrowView(vw, vh) {
    vw = vw || (typeof window !== "undefined" ? window.innerWidth : 800);
    vh = vh || (typeof window !== "undefined" ? window.innerHeight : 600);
    return vw <= 520 || (vw <= 900 && vh <= 480);
  }
  function particleCaps(vw, vh) {
    if (isNarrowView(vw, vh)) return { dust: 22, sparks: 18, ambient: 12, splash: 6 };
    return { dust: 48, sparks: 40, ambient: 28, splash: 10 };
  }

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
    { x: 2260, y: 1740, r: 160, h: 1.35 },
    { x: 3280, y: 1820, r: 150, h: 1.2 },
    { x: 2460, y: 2480, r: 120, h: 0.7 },
    { x: 3180, y: 2680, r: 130, h: -0.4 },
    { x: 2800, y: 2100, r: 100, h: 0.85 },
  ];
  /* polish9: start/finish gate on west straight (TRACK_MAIN[0] region) */
  var TRACK_GATE = { x: 1870, y: 2225, halfW: 70, halfH: 28 };

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
    { id: "ripsaw", x: 780, y: 1520, vehicleStyle: "ripsaw" },
    { id: "tank", x: 1080, y: 1520, vehicleStyle: "tank" },
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

  /* track3: cam view scale — lower = more world visible (zoom out). Mouse wheel / setViewScale. */
  var VIEW_SCALE_BASE = 0.88;
  var VIEW_SCALE_MIN = 0.66;
  var VIEW_SCALE_MAX = 1.08;
  var viewScaleUser = 1;

  function getViewScale() {
    return clamp(VIEW_SCALE_BASE * viewScaleUser, VIEW_SCALE_MIN, VIEW_SCALE_MAX);
  }
  function setViewScaleUser(u) {
    viewScaleUser = clamp(u, VIEW_SCALE_MIN / VIEW_SCALE_BASE, VIEW_SCALE_MAX / VIEW_SCALE_BASE);
    return getViewScale();
  }
  function adjustViewScale(delta) {
    return setViewScaleUser(viewScaleUser + (delta || 0));
  }

  /* qa1: keyboard / stick / tap are screen axes. steer.y < 0 is screen up.
     Inverse of the isometric ground map (sx = 0.98 dx - 0.52 dy, sy = 0.30 dx + 0.58 dy).
     AI follow writes world axes and must not set steerScreen. */
  function screenSteerToWorld(sx, sy) {
    var det = 0.98 * 0.58 + 0.52 * 0.30;
    return {
      x: (0.58 * sx + 0.52 * sy) / det,
      y: (-0.30 * sx + 0.98 * sy) / det,
    };
  }

  function project(wx, wy, camX, camY, vw, vh) {
    var dx = wx - camX;
    var dy = wy - camY;
    var vs = getViewScale();
    var sx = vw * 0.5 + (dx * 0.98 - dy * 0.52) * vs;
    var sy = vh * 0.46 + (dx * 0.30 + dy * 0.58) * vs;
    /* polish6: wider scale-with-depth so distant props shrink, near ones punch */
    var depth = clamp(0.48 + (wy - camY) / MAP_H * 0.78 + dy * 0.00022, 0.28, 1.52) * (0.92 + vs * 0.08);
    return { x: sx, y: sy, depth: depth, sortY: wy, scale: depth };
  }

  /* polish6: soft layered drop shadow (FF-like ground contact) */
  function drawSoftShadow(ctx, x, y, rx, ry, alpha) {
    alpha = alpha == null ? 0.34 : alpha;
    ctx.fillStyle = "rgba(0,0,0," + (alpha * 0.45) + ")";
    ctx.beginPath();
    ctx.ellipse(x, y + 1, rx * 1.35, ry * 1.25, -0.12, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = "rgba(0,0,0," + alpha + ")";
    ctx.beginPath();
    ctx.ellipse(x, y, rx, ry, -0.12, 0, Math.PI * 2);
    ctx.fill();
  }

  function defaultHotspots() {
    var C = global.FroggiesCanon;
    if (C && C.HOTSPOTS) {
      return C.HOTSPOTS.map(function (h) {
        return {
          id: h.id, label: h.label, x: h.x, y: h.y, r: h.r, tip: h.tip,
          kind: h.kind || null, frogId: h.frogId || null, mode: h.mode || null,
          stories: h.stories || null, solidId: h.solidId || null,
          vehicleStyle: h.vehicleStyle || null,
        };
      });
    }
    return [
      { id: "phone", label: "Phone", x: 380, y: 1880, r: 52, tip: "Call Purple Bear" },
      { id: "sps", label: "SPS", x: 520, y: 1940, r: 48, tip: "Solar Positioning System" },
      { id: "truck-james", label: "Cybertruck · James", x: 1880, y: 1720, r: 54, tip: "Cybertruck · James paint · any frog · solo", kind: "truck", frogId: "james", mode: "solo", vehicleStyle: "cybertruck" },
      { id: "truck-jimmy", label: "Cybertruck · Jimmy", x: 2080, y: 1720, r: 54, tip: "Cybertruck · Jimmy paint · any frog · solo", kind: "truck", frogId: "jimmy", mode: "solo", vehicleStyle: "cybertruck" },
      { id: "truck-bubbles", label: "Cybertruck · Bubbles", x: 2280, y: 1720, r: 54, tip: "Cybertruck · Bubbles paint · any frog · solo", kind: "truck", frogId: "bubbles", mode: "solo", vehicleStyle: "cybertruck" },
      { id: "truck-rexy", label: "Cybertruck · Rexy", x: 2480, y: 1720, r: 54, tip: "Cybertruck · Rexy paint · any frog · solo", kind: "truck", frogId: "rexy", mode: "solo", vehicleStyle: "cybertruck" },
      { id: "truck-shared", label: "★ ALL ABOARD · 4 frogs", x: 2180, y: 1880, r: 78, tip: "Shared Cybertruck · all four pile in", kind: "truck", frogId: null, mode: "shared", vehicleStyle: "cybertruck" },
      { id: "truck-ripsaw", label: "Ripsaw", x: 780, y: 1520, r: 62, tip: "Shared Ripsaw · tracked · any frog", kind: "truck", frogId: null, mode: "solo", vehicleStyle: "ripsaw" },
      { id: "truck-tank", label: "Tank", x: 1080, y: 1520, r: 62, tip: "Shared Tank · FIRE while driving · any frog", kind: "truck", frogId: null, mode: "solo", vehicleStyle: "tank" },
      { id: "mech-10", label: "Bubbles · 10-story mech", x: 820, y: 1680, r: 64, tip: "Bubbles only · 10-story mech", kind: "mech", stories: 10, solidId: "mech10", frogId: "bubbles" },
      { id: "mech-100", label: "Jimmy · 100-story mech", x: 980, y: 1700, r: 78, tip: "Jimmy only · 100-story mech", kind: "mech", stories: 100, solidId: "mech100", frogId: "jimmy" },
      { id: "mech-1000", label: "Rexy · 1000-story mech", x: 340, y: 2420, r: 120, tip: "Rexy only · 1000-story mech", kind: "mech", stories: 1000, solidId: "mech1000", frogId: "rexy" },
      { id: "mech-trillion", label: "James · trillion-story mech", x: 600, y: 2170, r: 150, tip: "James only · trillion-story mech", kind: "mech", stories: 1e12, solidId: "mechTrillion", frogId: "james" },
      { id: "fishies", label: "Fishies", x: 3160, y: 620, r: 70, tip: "Swim the pond · fishies & whales" },
      { id: "submarine", label: "Submarine", x: 2900, y: 1240, r: 72, tip: "Submarine · dive underwater", kind: "submarine", vehicleStyle: "submarine", mode: "solo" },
      { id: "heli", label: "Helipad · H", x: 980, y: 2000, r: 52, tip: "Helicopter · 4 seats · INTERACT board", kind: "heli", vehicleStyle: "heli", mode: "shared", seats: 4 },
      { id: "drone", label: "Drone pad · D", x: 1180, y: 2000, r: 40, tip: "Passenger drone · 1–2 seats · INTERACT board", kind: "drone", vehicleStyle: "drone", mode: "shared", seats: 2 },
      { id: "starship", label: "Starship", x: STARSHIP.x, y: STARSHIP.y, r: 72, tip: "Starship · Spotty · space episode" },
    ];
  }

  function createWorld() {
    var C0 = global.FroggiesCanon;
    if (C0 && C0.resetVehicleParks) C0.resetVehicleParks();
    var world = {
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
      bubbles: [],
      sparks: [],
      ambient: [],
      ripples: [],
      sparkles: [],
      kitFx: [],
      scrap: 0,
      stuntCombo: 0,
      airTime: 0,
      lastJumpT: 0,
      sharedDriverId: null,
      air: null,
      garageOpen: 0,
      ambientT: 0,
      lapCount: 0,
      lapSide: 0,
      lapCooldown: 0,
    };
    var Air0 = global.FroggiesAir;
    if (Air0 && Air0.resetAir) Air0.resetAir(world);
    return world;
  }

  function seedDecor(world) {
    var pond = AREAS[2];
    world.fish = [];
    /* polish8: fish schools — clustered leaders + followers */
    var schoolCenters = [];
    for (var sc = 0; sc < 5; sc++) {
      schoolCenters.push({
        x: pond.x + 80 + Math.random() * (pond.w - 160),
        y: pond.y + 80 + Math.random() * (pond.h - 160),
        phase: Math.random() * Math.PI * 2,
      });
    }
    for (var i = 0; i < 36; i++) {
      var sch = schoolCenters[i % schoolCenters.length];
      world.fish.push({
        x: sch.x + (Math.random() - 0.5) * 70,
        y: sch.y + (Math.random() - 0.5) * 50,
        phase: sch.phase + (Math.random() - 0.5) * 0.8,
        speed: 0.5 + Math.random() * 0.85,
        scare: 0,
        size: 2.0 + Math.random() * 2.6,
        kind: "fish",
        school: i % schoolCenters.length,
        ox: (Math.random() - 0.5) * 55,
        oy: (Math.random() - 0.5) * 40,
      });
    }
    world.schools = schoolCenters;
    world.whales = [];
    for (var w = 0; w < 5; w++) {
      world.whales.push({
        x: pond.x + 120 + Math.random() * (pond.w - 240),
        y: pond.y + 100 + Math.random() * (pond.h - 200),
        phase: Math.random() * Math.PI * 2,
        speed: 0.25 + Math.random() * 0.35,
        scare: 0,
        size: 10 + Math.random() * 7,
        kind: "whale",
        breach: Math.random() * Math.PI * 2,
        breachAmp: 38 + Math.random() * 28,
      });
    }
    /* Big backyard animals — anonymous density only (polish4: larger / more present) */
    world.animals = [];
    var yardX0 = 110, yardY0 = 2080, yardW = 560, yardH = 360;
    for (var a = 0; a < 52; a++) {
      world.animals.push({
        x: yardX0 + Math.random() * yardW,
        y: yardY0 + Math.random() * yardH,
        phase: Math.random() * Math.PI * 2,
        speed: 0.35 + Math.random() * 0.95,
        kind: a % 4,
        tone: a % 3 === 0 ? "#c4a574" : a % 3 === 1 ? "#8b6914" : "#d6d3d1",
        size: 1.05 + Math.random() * 1.15,
        bob: Math.random() * Math.PI * 2,
        vx: 0, vy: 0, r: 12, pushable: true,
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
        vx: 0, vy: 0, r: 9, pushable: true,
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
        vx: 0, vy: 0, r: 10, pushable: true,
      });
    }
    world.dust = [];
    world.splashes = [];
    world.bubbles = [];
    world.sparks = [];
    world.shells = []; /* mech3: tank missiles */
    world.booms = [];
    world.ambient = [];
    world.ripples = [];
    world.sparkles = [];
    world.kitFx = [];
    world.scrap = 0;
    world.stuntCombo = 0;
    world.airTime = 0;
    world.sharedDriverId = null;
    world.garageOpen = 0;
    world.lapCount = 0;
    world.lapSide = 0;
    world.lapCooldown = 0;
    world.ambientT = 0;
    /* polish5/polish10/mobile1: capped ambient pollen / fireflies (perf; fewer on phone) */
    var ambCap = particleCaps().ambient;
    for (var ai = 0; ai < ambCap; ai++) {
      world.ambient.push({
        x: 80 + Math.random() * (MAP_W - 160),
        y: 80 + Math.random() * (MAP_H - 160),
        vx: (Math.random() - 0.5) * 18,
        vy: (Math.random() - 0.5) * 12,
        phase: Math.random() * Math.PI * 2,
        kind: Math.random() < 0.55 ? "pollen" : "firefly",
        r: 1.2 + Math.random() * 2.4,
        pushable: true,
      });
    }
  }

  function makeFrogEntity(id, human, local, laneIndex) {
    var colors = FROG_COLORS[id] || FROG_COLORS.james;
    var C = global.FroggiesCanon;
    var sp = (C && C.COMPOUND && C.COMPOUND.spawn) || { x: 420, y: 1960 };
    var ox = sp.x + (laneIndex % 2) * 48;
    var oy = sp.y + Math.floor(laneIndex / 2) * 48;
    var def = (C && C.FROG_DEFS && C.FROG_DEFS[id]) || null;
    return {
      id: id,
      name: def ? def.name : id,
      human: !!human,
      local: !!local,
      x: ox,
      y: oy,
      vx: 0,
      vy: 0,
      facing: 1,
      faceAngle: -Math.PI / 2, /* eyes1: screen-up default; updated while moving */
      inTruck: false,
      truckMode: null,
      truckId: null,
      vehicleStyle: null,
      inMech: false,
      mechId: null,
      mechStories: 0,
      inSwim: false,
      inSub: false,
      inHeli: false,
      inDrone: false,
      airKind: null,
      airSeat: null,
      climbIn: 0,
      airBoost: false,
      padIndex: null,
      z: 0,
      zVel: 0,
      groundZ: 0,
      waterSub: 0,
      wakePhase: 0,
      truckBounce: 0,
      bouncePhase: 0,
      wheelScale: 1,
      rockCool: 0,
      dustTimer: 0,
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
      hopSquash: 0,
      autoHopCd: 0,
      /* polish7 */
      idleBounce: 0,
      followLag: 0.35 + (laneIndex % 3) * 0.12,
      chatT: 0,
      chatLine: "",
      chatCd: 2 + Math.random() * 4,
    };
  }

  /** True if another frog already occupies this boardable (solo mech / solo truck). Shared truck OK. */
  function hotspotTakenByOther(world, frogs, hot, me) {
    if (!hot || !frogs || !me) return false;
    var C = global.FroggiesCanon;
    var hid = hot.id ? String(hot.id) : "";
    var isMech = hot.kind === "mech" || (C && C.isMechHotspot && C.isMechHotspot(hot)) || hid.indexOf("mech") === 0;
    var isTruck = hot.kind === "truck" || (C && C.isTruckHotspot && C.isTruckHotspot(hot)) || hid.indexOf("truck") === 0;
    var isSub = hot.kind === "submarine" || (C && C.isSubHotspot && C.isSubHotspot(hot)) || hid.indexOf("submarine") === 0;
    var isAir = hot.kind === "heli" || hot.kind === "drone" || (C && C.isAirHotspot && C.isAirHotspot(hot)) || hid === "heli" || hid === "drone";
    if (!isMech && !isTruck && !isSub && !isAir) return false;
    if (isAir) {
      var AirT = global.FroggiesAir;
      if (AirT && world) {
        var kindT = (C && C.airKindOf) ? C.airKindOf(hot) : (hot.kind === "drone" ? "drone" : "heli");
        var craftT = AirT.ensureCraft(world, kindT);
        if (craftT && craftT.seats) {
          var aboardMe = false, openSeat = false;
          for (var si = 0; si < craftT.seats.length; si++) {
            if (craftT.seats[si] === me.id) aboardMe = true;
            if (!craftT.seats[si]) openSeat = true;
          }
          if (aboardMe) return false;
          if (!openSeat) return true;
        }
      }
      return false;
    }
    if (hot.mode === "shared") return false;
    var sid = C && C.mechSolidId ? C.mechSolidId(hid) : null;
    for (var i = 0; i < frogs.length; i++) {
      var f = frogs[i];
      if (!f || f === me) continue;
      if (isMech && f.inMech && f.mechId) {
        var fsid = C && C.mechSolidId ? C.mechSolidId(f.mechId) : String(f.mechId).replace(/^mech-/, "mech");
        if (f.mechId === hid || f.mechId === hot.solidId || (sid && fsid === sid) || (hot.solidId && fsid === hot.solidId))
          return true;
      }
      if (isTruck && f.inTruck && f.truckMode !== "shared" && f.truckId === hid) return true;
      if (isSub && f.inSub) return true;
    }
    return false;
  }

  /**
   * Nearest hotspot. opts.frogs + opts.frog → skip boardables already taken by others
   * so each local can target a DIFFERENT free mech (boardall1).
   */
  function nearestHotspot(world, x, y, maxR, opts) {
    opts = opts || {};
    var frogs = opts.frogs || null;
    var me = opts.frog || null;
    var preferFree = !!(frogs && me);
    var CgoneH = global.FroggiesCanon;
    var baseMax = maxR != null ? maxR : 110;
    /* drivefix3: owner story-mech wins when in generous reach */
    if (me && CgoneH && CgoneH.ownMechInReach && !opts.skipOwnMech) {
      var own = CgoneH.ownMechInReach(x, y, me.id, Math.max(baseMax, 140));
      if (own) {
        /* Prefer live world hotspot copy (park-synced) when present */
        for (var oi = 0; oi < world.hotspots.length; oi++) {
          var oh = world.hotspots[oi];
          if (oh && own && (oh.id === own.id || (oh.solidId && own.solidId && oh.solidId === own.solidId))) {
            if (!preferFree || !hotspotTakenByOther(world, frogs, oh, me)) return oh;
          }
        }
        if (!preferFree || !hotspotTakenByOther(world, frogs, own, me)) return own;
      }
    }
    var best = null;
    var bestD = baseMax;
    var bestFree = null;
    var bestFreeD = baseMax;
    for (var i = 0; i < world.hotspots.length; i++) {
      var h = world.hotspots[i];
      if (h && h.id && CgoneH && CgoneH.isPermaGone && CgoneH.isPermaGone(h.id)) continue;
      if (h && CgoneH && CgoneH.isMechHotspot && CgoneH.isMechHotspot(h) && CgoneH.isMechDestroyed && CgoneH.isMechDestroyed(h)) continue;
      /* drivefix1: skip mechs this frog cannot board so free Cybertruck/Ripsaw win INTERACT */
      if (me && CgoneH && CgoneH.isMechHotspot && CgoneH.isMechHotspot(h) &&
          CgoneH.canBoardMech && !CgoneH.canBoardMech(me.id, h)) continue;
      var d = Math.hypot(h.x - x, h.y - y);
      var reach = (CgoneH && CgoneH.boardReachFor) ? CgoneH.boardReachFor(h, bestD) : Math.max(bestD, (h.r || 60) + 36);
      if (d >= reach) continue;
      if (!best || d < Math.hypot(best.x - x, best.y - y)) {
        best = h;
        bestD = d;
      }
      if (preferFree && !hotspotTakenByOther(world, frogs, h, me)) {
        var freeReach = (CgoneH && CgoneH.boardReachFor) ? CgoneH.boardReachFor(h, bestFreeD) : Math.max(bestFreeD, (h.r || 60) + 36);
        if (d < freeReach && (!bestFree || d < Math.hypot(bestFree.x - x, bestFree.y - y))) {
          bestFree = h;
          bestFreeD = d;
        }
      }
    }
    return preferFree && bestFree ? bestFree : best;
  }

  function spawnDust(world, x, y, n) {
    /* polish10 + mobile1: hard cap dust; tighter on narrow */
    if (!world.dust) world.dust = [];
    var caps = particleCaps();
    var room = Math.max(0, caps.dust - world.dust.length);
    var count = Math.min(n || 4, room);
    for (var i = 0; i < count; i++) {
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
    var caps = particleCaps();
    var maxN = Math.min(n || caps.splash, caps.splash);
    for (var i = 0; i < maxN; i++) {
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

  function spawnBubbles(world, x, y, n) {
    for (var i = 0; i < (n || 4); i++) {
      world.bubbles.push({
        x: x + (Math.random() - 0.5) * 24,
        y: y + (Math.random() - 0.5) * 16,
        life: 0.55 + Math.random() * 0.55,
        vx: (Math.random() - 0.5) * 18,
        vy: -28 - Math.random() * 55,
        r: 2 + Math.random() * 4.5,
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

  /* mech3: big tank missile — forward from turret; blast wrecks props */
  function spawnTankShell(world, x, y, faceAngle, ownerId) {
    if (!world) return null;
    if (!world.shells) world.shells = [];
    var C = global.FroggiesCanon;
    var cfg = (C && C.TANK_FIRE) || { speed: 720, life: 1.55, muzzle: 52, hitR: 36, blastR: 118, size: 2.4 };
    var ang = (faceAngle != null && isFinite(faceAngle)) ? faceAngle : 0;
    var cx = Math.cos(ang), cy = Math.sin(ang);
    var muzzle = cfg.muzzle != null ? cfg.muzzle : 52;
    var spd = cfg.speed != null ? cfg.speed : 720;
    var life = cfg.life != null ? cfg.life : 1.55;
    var shell = {
      x: x + cx * muzzle,
      y: y + cy * muzzle,
      vx: cx * spd,
      vy: cy * spd,
      ang: ang,
      life: life,
      maxLife: life,
      ownerId: ownerId || null,
      r: cfg.hitR != null ? cfg.hitR : 36,
      blastR: cfg.blastR != null ? cfg.blastR : 118,
      blastForce: cfg.blastForce != null ? cfg.blastForce : 520,
      size: cfg.size != null ? cfg.size : 2.4,
      big: true,
    };
    world.shells.push(shell);
    if (world.shells.length > 16) world.shells.splice(0, world.shells.length - 16);
    spawnSparks(world, shell.x, shell.y, 14);
    spawnDust(world, x - cx * 8, y - cy * 8, 6);
    return shell;
  }

  /* mechgun1: story-mech omnigun shell — permanent kill blast on impact */
  function spawnMechGunShell(world, x, y, faceAngle, ownerId, ownerMechId) {
    if (!world) return null;
    if (!world.shells) world.shells = [];
    var C = global.FroggiesCanon;
    var cfg = (C && C.MECH_GUN) || { speed: 780, life: 1.7, muzzle: 64, hitR: 42, blastR: 140, size: 2.8 };
    var ang = (faceAngle != null && isFinite(faceAngle)) ? faceAngle : 0;
    var cx = Math.cos(ang), cy = Math.sin(ang);
    var muzzle = cfg.muzzle != null ? cfg.muzzle : 64;
    var spd = cfg.speed != null ? cfg.speed : 780;
    var life = cfg.life != null ? cfg.life : 1.7;
    var shell = {
      x: x + cx * muzzle,
      y: y + cy * muzzle,
      vx: cx * spd,
      vy: cy * spd,
      ang: ang,
      life: life,
      maxLife: life,
      ownerId: ownerId || null,
      ownerMechId: ownerMechId || null,
      r: cfg.hitR != null ? cfg.hitR : 42,
      blastR: cfg.blastR != null ? cfg.blastR : 140,
      blastForce: cfg.blastForce != null ? cfg.blastForce : 640,
      size: cfg.size != null ? cfg.size : 2.8,
      big: true,
      omnigun: true,
    };
    world.shells.push(shell);
    if (world.shells.length > 18) world.shells.splice(0, world.shells.length - 18);
    spawnSparks(world, shell.x, shell.y, 18);
    spawnDust(world, x - cx * 10, y - cy * 10, 8);
    return shell;
  }

  /* spear1: Rexy 1000-mech melee spear thrust — knocks trillion if in range/arc */
  function tryMechSpear(world, frog) {
    if (!world || !frog) return { ok: false, reason: "no-frog" };
    var C = global.FroggiesCanon;
    if (!C || !C.canSpearPilot || !C.canSpearPilot(frog)) return { ok: false, reason: "no-spear" };
    if (C.isPilotKnocked && C.isPilotKnocked(frog)) return { ok: false, reason: "self-down" };
    var cfg = C.MECH_SPEAR || { range: 280, halfArc: 0.95, thrustLife: 0.28 };
    var ang = (frog.faceAngle != null && isFinite(frog.faceAngle))
      ? frog.faceAngle
      : (frog.facing >= 0 ? 0 : Math.PI);
    var cx = Math.cos(ang), cy = Math.sin(ang);
    var range = cfg.range != null ? cfg.range : 280;
    var halfArc = cfg.halfArc != null ? cfg.halfArc : 0.95;
    /* Target: trillion entity (pilot pos or park) */
    var home = (C.COMPOUND && C.COMPOUND.mechTrillion) || { x: 600, y: 2170 };
    var tp = C.vehiclePos ? C.vehiclePos("mechTrillion", home.x, home.y) : home;
    var frogs = world._frogsRef || null;
    var pilot = null;
    if (frogs) {
      for (var fi = 0; fi < frogs.length; fi++) {
        var f = frogs[fi];
        if (!f || !f.inMech || f.sessionDead) continue;
        var sid = C.mechSolidId ? C.mechSolidId(f.mechId) : null;
        if (sid === "mechTrillion") { pilot = f; break; }
      }
    }
    var tx = pilot ? pilot.x : tp.x;
    var ty = pilot ? pilot.y : tp.y;
    var dx = tx - frog.x, dy = ty - frog.y;
    var dist = Math.hypot(dx, dy);
    var hitR = 125; /* trillion body radius */
    /* Thrust FX always */
    if (!world.spears) world.spears = [];
    world.spears.push({
      x: frog.x, y: frog.y, ang: ang,
      life: cfg.thrustLife != null ? cfg.thrustLife : 0.28,
      maxLife: cfg.thrustLife != null ? cfg.thrustLife : 0.28,
      len: Math.min(range, 200),
      ownerId: frog.id,
    });
    if (world.spears.length > 6) world.spears.splice(0, world.spears.length - 6);
    spawnSparks(world, frog.x + cx * 40, frog.y + cy * 40, 12);
    if (dist > range + hitR) {
      return { ok: true, hit: false, toast: "SPEAR · miss" };
    }
    var aim = Math.atan2(dy, dx);
    var da = aim - ang;
    while (da > Math.PI) da -= Math.PI * 2;
    while (da < -Math.PI) da += Math.PI * 2;
    if (Math.abs(da) > halfArc && dist > hitR * 0.55) {
      return { ok: true, hit: false, toast: "SPEAR · miss" };
    }
    if (C.isMechKnocked && C.isMechKnocked("mechTrillion")) {
      return { ok: true, hit: false, toast: "Already down!" };
    }
    if (!C.knockMechDown || !C.knockMechDown("mechTrillion")) {
      return { ok: true, hit: false, toast: "SPEAR · no effect" };
    }
    spawnBoom(world, tx, ty, 1.6);
    spawnSparks(world, tx, ty, 22);
    spawnDust(world, tx, ty, 10);
    world._spearKnockToast = "SPEAR · trillion DOWN!";
    return { ok: true, hit: true, toast: "SPEAR · trillion DOWN!", target: pilot || null };
  }

  function spawnBoom(world, x, y, power) {
    if (!world) return;
    if (!world.booms) world.booms = [];
    var p = power != null ? power : 1;
    world.booms.push({
      x: x, y: y,
      life: 0.55 + 0.15 * p,
      maxLife: 0.55 + 0.15 * p,
      r: 18,
      maxR: 70 + 50 * p,
      power: p,
    });
    spawnSparks(world, x, y, 18 + Math.floor(10 * p));
    spawnDust(world, x, y, 10);
    spawnSparkle(world, x, y, 14);
  }

  /* mech3 + mech5: radial blast — toys/animals; ONLY Rexy 1000-story mech among mechs */
  function blastWreckProps(world, x, y, radius, force, frogs) {
    if (!world) return 0;
    var R = radius != null ? radius : 118;
    var F = force != null ? force : 520;
    var hitN = 0;
    var C = global.FroggiesCanon;
    function wreckList(arr) {
      if (!arr) return;
      for (var i = 0; i < arr.length; i++) {
        var prop = arr[i];
        if (!prop || prop.wrecked) continue;
        var dx = prop.x - x, dy = prop.y - y;
        var d = Math.hypot(dx, dy);
        var pr = prop.r || 10;
        if (d > R + pr) continue;
        var falloff = 1 - d / (R + pr);
        var nx = d > 0.1 ? dx / d : (Math.random() - 0.5);
        var ny = d > 0.1 ? dy / d : (Math.random() - 0.5);
        prop.vx = (prop.vx || 0) + nx * F * (0.45 + falloff);
        prop.vy = (prop.vy || 0) + ny * F * (0.45 + falloff);
        if (prop.homeX == null) { prop.homeX = prop.x; prop.homeY = prop.y; prop.homeR = prop.r || 10; prop.homeSize = prop.size; }
        prop.wrecked = true;
        prop.wreckT = 0.9 + Math.random() * 0.5;
        prop.respawnT = (C && C.BLAST_RESPAWN_SEC) ? C.BLAST_RESPAWN_SEC : 7;
        prop.spin = (Math.random() - 0.5) * 14;
        prop.r = Math.max(4, (prop.r || 10) * 0.7);
        hitN++;
      }
    }
    wreckList(world.toys);
    wreckList(world.animals);
    /* mech5: ONLY thousand-story (Rexy) mech is destructible by tank */
    if (C && C.isTankBlastableMech && C.markMechDestroyed && !C.isMechDestroyed("mech1000")) {
      var home = (C.COMPOUND && C.COMPOUND.mech1000) || { x: 340, y: 2420 };
      var mp = C.vehiclePos ? C.vehiclePos("mech1000", home.x, home.y) : home;
      var md = Math.hypot(mp.x - x, mp.y - y);
      var mr = 95;
      if (md <= R + mr) {
        if (C.markMechDestroyed("mech1000")) {
          hitN++;
          ejectPilotsFromMech(frogs || world._frogsRef, "mech1000");
          spawnBoom(world, mp.x, mp.y, 3.2);
          spawnSparks(world, mp.x, mp.y, 28);
          spawnDust(world, mp.x, mp.y, 16);
          world._mechBoomToast = "BOOM · Rexy 1000-story mech!";
        }
      }
    }
    return hitN;
  }

  function ejectPilotsFromMech(frogs, solidId) {
    if (!frogs || !solidId) return;
    var C = global.FroggiesCanon;
    for (var i = 0; i < frogs.length; i++) {
      var f = frogs[i];
      if (!f || !f.inMech) continue;
      var sid = C && C.mechSolidId ? C.mechSolidId(f.mechId) : String(f.mechId || "").replace(/^mech-/, "mech");
      if (sid !== solidId && f.mechId !== solidId) continue;
      f.inMech = false;
      f.mechId = null;
      f.mechStories = 0;
      f.z = 0; f.zVel = 0;
    }
  }

  function shellHitsBlastableMech(world, sh) {
    var C = global.FroggiesCanon;
    if (!C || !C.isTankBlastableMech || C.isMechDestroyed("mech1000")) return false;
    var home = (C.COMPOUND && C.COMPOUND.mech1000) || { x: 340, y: 2420 };
    var mp = C.vehiclePos ? C.vehiclePos("mech1000", home.x, home.y) : home;
    var rr = 95 + (sh.r || 28);
    var dx = mp.x - sh.x, dy = mp.y - sh.y;
    return dx * dx + dy * dy < rr * rr;
  }

  function ownerMechSolidId(sh, frogs) {
    var C = global.FroggiesCanon;
    if (sh && sh.ownerMechId && C && C.mechSolidId) return C.mechSolidId(sh.ownerMechId);
    if (sh && sh.ownerMechId) return String(sh.ownerMechId).replace(/^mech-/, "mech");
    if (!frogs || !sh || !sh.ownerId) return null;
    for (var i = 0; i < frogs.length; i++) {
      var f = frogs[i];
      if (f && f.id === sh.ownerId && f.inMech) {
        return C && C.mechSolidId ? C.mechSolidId(f.mechId) : String(f.mechId || "").replace(/^mech-/, "mech");
      }
    }
    return null;
  }

  /* mechgun1: permanent session kill — everything in blast except shooter + own mech */
  function blastOmnigunKill(world, x, y, radius, force, frogs, sh) {
    if (!world) return 0;
    var R = radius != null ? radius : 140;
    var F = force != null ? force : 640;
    var hitN = 0;
    var C = global.FroggiesCanon;
    var ownerId = sh && sh.ownerId;
    var ownSid = ownerMechSolidId(sh, frogs || world._frogsRef);
    frogs = frogs || world._frogsRef || [];

    function inBlast(px, py, pr) {
      var dx = px - x, dy = py - y;
      return Math.hypot(dx, dy) <= R + (pr || 0);
    }

    function wreckForever(prop) {
      if (!prop || prop.goneForever) return false;
      if (prop.homeX == null) { prop.homeX = prop.x; prop.homeY = prop.y; prop.homeR = prop.r || 10; prop.homeSize = prop.size; }
      var dx = prop.x - x, dy = prop.y - y;
      var d = Math.hypot(dx, dy) || 0.1;
      prop.vx = (prop.vx || 0) + (dx / d) * F * 0.7;
      prop.vy = (prop.vy || 0) + (dy / d) * F * 0.7;
      prop.wrecked = true;
      prop.goneForever = true;
      prop.wreckT = 1.0 + Math.random() * 0.5;
      prop.respawnT = 1e12;
      prop.spin = (Math.random() - 0.5) * 16;
      prop.r = Math.max(3, (prop.r || 10) * 0.55);
      return true;
    }

    function killList(arr) {
      if (!arr) return;
      for (var i = 0; i < arr.length; i++) {
        var prop = arr[i];
        if (!prop || prop.goneForever) continue;
        if (!inBlast(prop.x, prop.y, prop.r || 10)) continue;
        if (wreckForever(prop)) hitN++;
      }
    }
    killList(world.toys);
    killList(world.animals);

    /* other story mechs (not shooter's) */
    var mechDefs = [
      { sid: "mech10", home: (C && C.COMPOUND && C.COMPOUND.mech10) || { x: 820, y: 1680 }, r: 64 },
      { sid: "mech100", home: (C && C.COMPOUND && C.COMPOUND.mech100) || { x: 980, y: 1700 }, r: 78 },
      { sid: "mech1000", home: (C && C.COMPOUND && C.COMPOUND.mech1000) || { x: 340, y: 2420 }, r: 95 },
      { sid: "mechTrillion", home: (C && C.COMPOUND && C.COMPOUND.mechTrillion) || { x: 600, y: 2170 }, r: 125 },
    ];
    for (var mi = 0; mi < mechDefs.length; mi++) {
      var md = mechDefs[mi];
      if (ownSid && md.sid === ownSid) continue;
      if (C && C.isMechDestroyed && C.isMechDestroyed(md.sid)) continue;
      var mp = C && C.vehiclePos ? C.vehiclePos(md.sid, md.home.x, md.home.y) : md.home;
      /* if piloted, use pilot position */
      for (var fi = 0; fi < frogs.length; fi++) {
        var pf = frogs[fi];
        if (!pf || !pf.inMech) continue;
        var psid = C && C.mechSolidId ? C.mechSolidId(pf.mechId) : String(pf.mechId || "").replace(/^mech-/, "mech");
        if (psid === md.sid) { mp = { x: pf.x, y: pf.y }; break; }
      }
      if (!inBlast(mp.x, mp.y, md.r)) continue;
      if (C && C.markMechDestroyed && C.markMechDestroyed(md.sid, { permanent: true })) {
        hitN++;
        ejectPilotsFromMech(frogs, md.sid);
        spawnBoom(world, mp.x, mp.y, 3.4);
        spawnSparks(world, mp.x, mp.y, 30);
        world._mechBoomToast = "GONE · story mech!";
      }
    }

    /* trucks / ripsaw / tank (parked or driven) */
    var truckSpots = (C && C.TRUCK_SPOTS) || TRUCK_SPOTS || [];
    for (var ti = 0; ti < truckSpots.length; ti++) {
      var spot = truckSpots[ti];
      var hid = spot.id === "shared" ? "truck-shared" : (String(spot.id).indexOf("truck") === 0 ? spot.id : "truck-" + spot.id);
      if (C && C.isPermaGone && C.isPermaGone(hid)) continue;
      var tp = C && C.vehiclePos ? C.vehiclePos(hid, spot.x, spot.y) : { x: spot.x, y: spot.y };
      var driven = null;
      for (var tfi = 0; tfi < frogs.length; tfi++) {
        var tf = frogs[tfi];
        if (tf && tf.inTruck && (tf.truckId === hid || (spot.id === "shared" && tf.truckMode === "shared"))) {
          driven = tf; tp = { x: tf.x, y: tf.y }; break;
        }
      }
      if (!inBlast(tp.x, tp.y, 50)) continue;
      if (C && C.markPermaGone) C.markPermaGone(hid);
      hitN++;
      for (var efi = 0; efi < frogs.length; efi++) {
        var ef = frogs[efi];
        if (!ef || !ef.inTruck) continue;
        if (ef.truckId !== hid && !(spot.id === "shared" && ef.truckMode === "shared")) continue;
        if (ef.id === ownerId) continue; /* shouldn't happen — shooter in mech */
        ef.inTruck = false; ef.truckId = null; ef.truckMode = null; ef.vehicleStyle = null;
        ef.z = 0; ef.zVel = 0;
      }
      spawnBoom(world, tp.x, tp.y, 2.6);
      spawnSparks(world, tp.x, tp.y, 20);
      world._mechBoomToast = "GONE · vehicle!";
    }

    /* submarine */
    if (!(C && C.isPermaGone && C.isPermaGone("submarine"))) {
      var dock = (C && C.SUB_DOCK) || { x: 2900, y: 1240 };
      var sp = C && C.vehiclePos ? C.vehiclePos("submarine", dock.x, dock.y) : dock;
      for (var sfi = 0; sfi < frogs.length; sfi++) {
        if (frogs[sfi] && frogs[sfi].inSub) { sp = { x: frogs[sfi].x, y: frogs[sfi].y }; break; }
      }
      if (inBlast(sp.x, sp.y, 56)) {
        if (C && C.markPermaGone) C.markPermaGone("submarine");
        hitN++;
        for (var sfe = 0; sfe < frogs.length; sfe++) {
          var sf = frogs[sfe];
          if (!sf || !sf.inSub) continue;
          if (sf.id === ownerId) continue;
          sf.inSub = false; sf.subId = null; sf.vehicleStyle = null;
        }
        spawnBoom(world, sp.x, sp.y, 2.4);
      }
    }

    /* heli + drone craft */
    var Air = global.FroggiesAir;
    if (Air && world.air && world.air.crafts) {
      var kinds = ["heli", "drone"];
      for (var ki = 0; ki < kinds.length; ki++) {
        var kind = kinds[ki];
        if (C && C.isPermaGone && C.isPermaGone(kind)) continue;
        var craft = Air.ensureCraft ? Air.ensureCraft(world, kind) : null;
        if (!craft) continue;
        if (!inBlast(craft.x, craft.y, 48)) continue;
        if (C && C.markPermaGone) C.markPermaGone(kind);
        hitN++;
        for (var afi = 0; afi < frogs.length; afi++) {
          var af = frogs[afi];
          if (!af) continue;
          if ((kind === "heli" && af.inHeli) || (kind === "drone" && af.inDrone)) {
            if (af.id === ownerId) continue;
            af.inHeli = false; af.inDrone = false;
            af.airSeat = null; af._airPilot = false;
            af.z = 0; af.zVel = 0;
          }
        }
        craft.goneForever = true;
        craft.landed = true;
        craft.z = 0; craft.vx = craft.vy = craft.vz = 0;
        if (craft.seats) {
          for (var si = 0; si < craft.seats.length; si++) craft.seats[si] = null;
          craft.pilotId = null;
        }
        spawnBoom(world, craft.x, craft.y, 2.8);
        spawnSparks(world, craft.x, craft.y, 22);
        world._mechBoomToast = "GONE · " + kind + "!";
      }
    }

    /* mechgun2: pond fish + whales — permanent */
    function killSwimmers(arr, pr) {
      if (!arr) return;
      for (var si = 0; si < arr.length; si++) {
        var sw = arr[si];
        if (!sw || sw.goneForever) continue;
        if (!inBlast(sw.x, sw.y, pr || (sw.size || 4))) continue;
        sw.goneForever = true;
        sw.wrecked = true;
        sw.scare = 0;
        hitN++;
        spawnBoom(world, sw.x, sw.y, sw.kind === "whale" ? 2.2 : 1.2);
      }
    }
    killSwimmers(world.fish, 18);
    killSwimmers(world.whales, 36);

    /* mechgun2: static ranch decor from canon — trees/rocks/fence/shrubs/flowers/track rocks */
    function killStaticList(list, idPrefix, radFn, boomPow, toast) {
      if (!list || !C || !C.markPermaGone) return;
      for (var ii = 0; ii < list.length; ii++) {
        var it = list[ii];
        if (!it) continue;
        var id = idPrefix + ii;
        if (C.isPermaGone && C.isPermaGone(id)) continue;
        var rr = radFn ? radFn(it) : (it.r || 14);
        if (!inBlast(it.x, it.y, rr)) continue;
        C.markPermaGone(id);
        hitN++;
        spawnBoom(world, it.x, it.y, boomPow != null ? boomPow : 1.6);
        spawnSparks(world, it.x, it.y, 10);
        if (toast) world._mechBoomToast = toast;
      }
    }
    killStaticList(C.YARD_TREES || [], "yard-tree-", function (tr) { return Math.max(18, (tr.r || 14) * 1.2); }, 2.4, "GONE · tree!");
    killStaticList(C.YARD_ROCKS || [], "yard-rock-", function (rk) { return (rk.r || 8) + 6; }, 1.5, "GONE · rock!");
    killStaticList(C.YARD_SHRUBS || [], "yard-shrub-", function (sb) { return 12 * (sb.s || 0.8); }, 1.3, null);
    killStaticList(C.YARD_FLOWERS || [], "yard-flower-", function () { return 10; }, 0.9, null);
    killStaticList(C.YARD_FENCE || [], "yard-fence-", function () { return 14; }, 1.2, "GONE · fence!");
    killStaticList(C.TRACK_ROCKS || [], "track-rock-", function (rk) { return (rk.r || 10) + 8; }, 1.6, "GONE · rock!");

    /* mechgun2: house + garage buildings — visual wreck + disable wall colliders */
    if (C && C.COMPOUND) {
      var house = C.COMPOUND.house;
      var gar = C.COMPOUND.garage;
      if (house && !(C.isPermaGone && C.isPermaGone("house"))) {
        var hcx = house.x + house.w * 0.5, hcy = house.y + house.h * 0.5;
        var hr = Math.max(house.w, house.h) * 0.42;
        if (inBlast(hcx, hcy, hr) || inBlast(house.x, house.y, 40) || inBlast(house.x + house.w, house.y + house.h, 40)
            || inBlast(house.x + house.w * 0.5, house.y + house.h, 50)) {
          C.markPermaGone("house");
          hitN++;
          spawnBoom(world, hcx, hcy, 4.2);
          spawnSparks(world, hcx, hcy, 36);
          spawnDust(world, hcx, hcy, 22);
          world._mechBoomToast = "GONE · ranch house!";
          if (C.markPermaGone) C.markPermaGone("blue-bear");
        }
      }
      if (gar && !(C.isPermaGone && C.isPermaGone("garage"))) {
        var gcx = gar.x + gar.w * 0.5, gcy = gar.y + gar.h * 0.5;
        var gr = Math.max(gar.w, gar.h) * 0.42;
        if (inBlast(gcx, gcy, gr) || inBlast(gar.x + gar.w * 0.5, gar.y + gar.h, 50)) {
          C.markPermaGone("garage");
          hitN++;
          spawnBoom(world, gcx, gcy, 3.6);
          spawnSparks(world, gcx, gcy, 28);
          spawnDust(world, gcx, gcy, 18);
          world._mechBoomToast = "GONE · garage!";
        }
      }
    }

    /* mechgun2: Blue Bear (place-bound pet) */
    if (C && !(C.isPermaGone && C.isPermaGone("blue-bear"))) {
      if (inBlast(320, 1920, 28)) {
        C.markPermaGone("blue-bear");
        hitN++;
        spawnBoom(world, 320, 1920, 1.8);
        world._mechBoomToast = "GONE · Blue Bear!";
      }
    }

    /* other froggies (AI or players) — soft-disable; never the shooter */
    for (var gi = 0; gi < frogs.length; gi++) {
      var gf = frogs[gi];
      if (!gf || gf.id === ownerId || gf.sessionDead) continue;
      /* shooter stays in mech — skip anyone still piloting shooter's mech */
      if (gf.inMech && ownSid) {
        var gsid = C && C.mechSolidId ? C.mechSolidId(gf.mechId) : String(gf.mechId || "").replace(/^mech-/, "mech");
        if (gsid === ownSid) continue;
      }
      if (!inBlast(gf.x, gf.y, 28)) continue;
      gf.sessionDead = true;
      gf.inTruck = false; gf.truckId = null; gf.truckMode = null; gf.vehicleStyle = null;
      gf.inMech = false; gf.mechId = null; gf.mechStories = 0;
      gf.inSub = false; gf.subId = null;
      gf.inHeli = false; gf.inDrone = false;
      gf.inSwim = false;
      gf.vx = 0; gf.vy = 0; gf.z = 0; gf.zVel = 0;
      hitN++;
      spawnBoom(world, gf.x, gf.y, 2.0);
      spawnSparks(world, gf.x, gf.y, 16);
      if (gf.local) world._mechBoomToast = "OUT · " + (gf.name || "froggy") + " (session)";
      else world._mechBoomToast = "GONE · " + (gf.name || "froggy") + "!";
    }

    return hitN;
  }

  function shellHitsOmnigunTarget(world, sh, frogs) {
    var R = (sh.r || 42);
    var C = global.FroggiesCanon;
    var ownSid = ownerMechSolidId(sh, frogs);
    var ownerId = sh.ownerId;
    frogs = frogs || world._frogsRef || [];
    function near(px, py, pr) {
      var dx = px - sh.x, dy = py - sh.y;
      var rr = R + (pr || 0);
      return dx * dx + dy * dy < rr * rr;
    }
    var lists = [world.toys || [], world.animals || []];
    for (var li = 0; li < lists.length; li++) {
      for (var j = 0; j < lists[li].length; j++) {
        var prop = lists[li][j];
        if (!prop || prop.goneForever || prop.wrecked) continue;
        if (near(prop.x, prop.y, prop.r || 10)) return true;
      }
    }
    var mechDefs = ["mech10", "mech100", "mech1000", "mechTrillion"];
    var homes = {
      mech10: (C && C.COMPOUND && C.COMPOUND.mech10) || { x: 820, y: 1680 },
      mech100: (C && C.COMPOUND && C.COMPOUND.mech100) || { x: 980, y: 1700 },
      mech1000: (C && C.COMPOUND && C.COMPOUND.mech1000) || { x: 340, y: 2420 },
      mechTrillion: (C && C.COMPOUND && C.COMPOUND.mechTrillion) || { x: 600, y: 2170 },
    };
    var mrs = { mech10: 64, mech100: 78, mech1000: 95, mechTrillion: 125 };
    for (var mi = 0; mi < mechDefs.length; mi++) {
      var sid = mechDefs[mi];
      if (ownSid && sid === ownSid) continue;
      if (C && C.isMechDestroyed && C.isMechDestroyed(sid)) continue;
      var mp = C && C.vehiclePos ? C.vehiclePos(sid, homes[sid].x, homes[sid].y) : homes[sid];
      for (var fi = 0; fi < frogs.length; fi++) {
        var pf = frogs[fi];
        if (!pf || !pf.inMech) continue;
        var psid = C && C.mechSolidId ? C.mechSolidId(pf.mechId) : String(pf.mechId || "").replace(/^mech-/, "mech");
        if (psid === sid) { mp = { x: pf.x, y: pf.y }; break; }
      }
      if (near(mp.x, mp.y, mrs[sid])) return true;
    }
    var truckSpots = (C && C.TRUCK_SPOTS) || TRUCK_SPOTS || [];
    for (var ti = 0; ti < truckSpots.length; ti++) {
      var spot = truckSpots[ti];
      var hid = spot.id === "shared" ? "truck-shared" : "truck-" + spot.id;
      if (C && C.isPermaGone && C.isPermaGone(hid)) continue;
      var tp = C && C.vehiclePos ? C.vehiclePos(hid, spot.x, spot.y) : { x: spot.x, y: spot.y };
      for (var tfi = 0; tfi < frogs.length; tfi++) {
        var tf = frogs[tfi];
        if (tf && tf.inTruck && tf.truckId === hid) { tp = { x: tf.x, y: tf.y }; break; }
      }
      if (near(tp.x, tp.y, 44)) return true;
    }
    if (!(C && C.isPermaGone && C.isPermaGone("submarine"))) {
      var dock = (C && C.SUB_DOCK) || { x: 2900, y: 1240 };
      var sp = C && C.vehiclePos ? C.vehiclePos("submarine", dock.x, dock.y) : dock;
      for (var sfi = 0; sfi < frogs.length; sfi++) {
        if (frogs[sfi] && frogs[sfi].inSub) { sp = { x: frogs[sfi].x, y: frogs[sfi].y }; break; }
      }
      if (near(sp.x, sp.y, 50)) return true;
    }
    var Air = global.FroggiesAir;
    if (Air && world.air) {
      for (var ki = 0; ki < 2; ki++) {
        var kind = ki === 0 ? "heli" : "drone";
        if (C && C.isPermaGone && C.isPermaGone(kind)) continue;
        var craft = Air.ensureCraft(world, kind);
        if (craft && !craft.goneForever && near(craft.x, craft.y, 42)) return true;
      }
    }
    for (var gi = 0; gi < frogs.length; gi++) {
      var gf = frogs[gi];
      if (!gf || gf.id === ownerId || gf.sessionDead) continue;
      if (gf.inMech && ownSid) {
        var gsid = C && C.mechSolidId ? C.mechSolidId(gf.mechId) : String(gf.mechId || "").replace(/^mech-/, "mech");
        if (gsid === ownSid) continue;
      }
      if (near(gf.x, gf.y, 26)) return true;
    }
    /* mechgun2: fish / whales / static ranch / buildings count as hits */
    if (world.fish) {
      for (var fi2 = 0; fi2 < world.fish.length; fi2++) {
        var ff = world.fish[fi2];
        if (ff && !ff.goneForever && near(ff.x, ff.y, 16)) return true;
      }
    }
    if (world.whales) {
      for (var wi2 = 0; wi2 < world.whales.length; wi2++) {
        var ww = world.whales[wi2];
        if (ww && !ww.goneForever && near(ww.x, ww.y, 32)) return true;
      }
    }
    function nearStatic(list, idPrefix, radFn) {
      if (!list) return false;
      for (var si = 0; si < list.length; si++) {
        var it = list[si];
        if (!it) continue;
        if (C && C.isPermaGone && C.isPermaGone(idPrefix + si)) continue;
        var rr = radFn ? radFn(it) : (it.r || 12);
        if (near(it.x, it.y, rr)) return true;
      }
      return false;
    }
    if (C) {
      if (nearStatic(C.YARD_TREES, "yard-tree-", function (tr) { return Math.max(16, (tr.r || 14)); })) return true;
      if (nearStatic(C.YARD_ROCKS, "yard-rock-", function (rk) { return (rk.r || 8) + 4; })) return true;
      if (nearStatic(C.YARD_SHRUBS, "yard-shrub-", function (sb) { return 10 * (sb.s || 0.8); })) return true;
      if (nearStatic(C.YARD_FLOWERS, "yard-flower-", function () { return 8; })) return true;
      if (nearStatic(C.YARD_FENCE, "yard-fence-", function () { return 12; })) return true;
      if (nearStatic(C.TRACK_ROCKS, "track-rock-", function (rk) { return (rk.r || 10) + 6; })) return true;
      if (C.COMPOUND) {
        var house = C.COMPOUND.house, gar = C.COMPOUND.garage;
        if (house && !(C.isPermaGone && C.isPermaGone("house"))) {
          var hcx = house.x + house.w * 0.5, hcy = house.y + house.h * 0.5;
          if (near(hcx, hcy, Math.max(house.w, house.h) * 0.38)) return true;
          if (near(house.x + house.w * 0.5, house.y + house.h, 40)) return true;
        }
        if (gar && !(C.isPermaGone && C.isPermaGone("garage"))) {
          var gcx = gar.x + gar.w * 0.5, gcy = gar.y + gar.h * 0.5;
          if (near(gcx, gcy, Math.max(gar.w, gar.h) * 0.38)) return true;
        }
      }
    }
    return false;
  }

  function spawnRipple(world, x, y, maxR) {
    if (!world.ripples) world.ripples = [];
    world.ripples.push({
      x: x, y: y,
      life: 1,
      r: 6 + Math.random() * 4,
      maxR: maxR || 48,
    });
  }

  function spawnSparkle(world, x, y, n) {
    if (!world.sparkles) world.sparkles = [];
    for (var i = 0; i < (n || 10); i++) {
      var ang = Math.random() * Math.PI * 2;
      var sp = 40 + Math.random() * 90;
      world.sparkles.push({
        x: x, y: y,
        life: 0.45 + Math.random() * 0.35,
        vx: Math.cos(ang) * sp,
        vy: Math.sin(ang) * sp * 0.7 - 30,
        r: 2 + Math.random() * 3,
        hue: 45 + Math.random() * 30,
      });
    }
  }

  /* polish9: Optimus kit visual punch on ranch (visual only) */
  function spawnKitFx(world, x, y, kind) {
    if (!world.kitFx) world.kitFx = [];
    world.kitFx.push({
      x: x, y: y, kind: kind || "rocket",
      life: kind === "map" ? 0.7 : kind === "hover" ? 0.85 : 0.55,
      age: 0,
    });
    if (kind === "rocket" || kind === "afterburners") spawnSparks(world, x, y, 14);
    else if (kind === "drone" || kind === "map") spawnSparkle(world, x, y, 16);
    else spawnSparkle(world, x, y, 8);
  }

  function gateSide(x, y) {
    /* West-straight finish line: positive when "past" toward NE circuit */
    var dx = x - TRACK_GATE.x;
    var dy = y - TRACK_GATE.y;
    return dx * 0.55 + dy * (-0.85);
  }

  function nearGate(x, y) {
    return Math.abs(x - TRACK_GATE.x) < TRACK_GATE.halfW * 1.6 &&
      Math.abs(y - TRACK_GATE.y) < TRACK_GATE.halfH * 2.2;
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
    spawnRipple(world, x, y, 70);
    spawnRipple(world, x + 18, y - 10, 42);
  }

  function updateFish(world, dt) {
    var pond = AREAS[2];
    /* polish8: school centers drift; fish keep pack offset */
    if (world.schools) {
      for (var si = 0; si < world.schools.length; si++) {
        var sc = world.schools[si];
        sc.phase += dt * (0.35 + (si % 3) * 0.08);
        sc.x += Math.cos(sc.phase) * 18 * dt;
        sc.y += Math.sin(sc.phase * 0.7) * 12 * dt;
        sc.x = clamp(sc.x, pond.x + 100, pond.x + pond.w - 100);
        sc.y = clamp(sc.y, pond.y + 100, pond.y + pond.h - 100);
      }
    }
    for (var i = 0; i < world.fish.length; i++) {
      var f = world.fish[i];
      if (f.goneForever) continue;
      f.phase += dt * f.speed * (f.scare > 0 ? 3.5 : 1);
      if (f.scare > 0) f.scare -= dt;
      var sch = world.schools && world.schools[f.school];
      if (sch && f.scare <= 0) {
        var tx = sch.x + (f.ox || 0) + Math.cos(f.phase) * 8;
        var ty = sch.y + (f.oy || 0) + Math.sin(f.phase * 0.7) * 6;
        f.x += (tx - f.x) * Math.min(1, 2.4 * dt);
        f.y += (ty - f.y) * Math.min(1, 2.4 * dt);
      } else {
        var sp = f.scare > 0 ? 22 * 2.6 : 22;
        f.x += Math.cos(f.phase) * sp * dt;
        f.y += Math.sin(f.phase * 0.7) * (sp * 0.7) * dt;
      }
      f.x = clamp(f.x, pond.x + 28, pond.x + pond.w - 28);
      f.y = clamp(f.y, pond.y + 28, pond.y + pond.h - 28);
    }
    for (var wi = 0; wi < world.whales.length; wi++) {
      var wh = world.whales[wi];
      if (wh.goneForever) continue;
      wh.phase += dt * wh.speed * (wh.scare > 0 ? 3.5 : 1);
      if (wh.scare > 0) wh.scare -= dt;
      var wsp = wh.scare > 0 ? 12 * 2.6 : 12;
      wh.x += Math.cos(wh.phase) * wsp * dt;
      wh.y += Math.sin(wh.phase * 0.7) * (wsp * 0.7) * dt;
      /* polish8: bigger breach arcs */
      wh.breach = (wh.breach || 0) + dt * (0.55 + wh.speed * 0.4);
      wh.x = clamp(wh.x, pond.x + 80, pond.x + pond.w - 80);
      wh.y = clamp(wh.y, pond.y + 80, pond.y + pond.h - 80);
    }
    for (var a = 0; a < world.animals.length; a++) {
      var an = world.animals[a];
      an.phase += dt * an.speed;
      an.bob = (an.bob || 0) + dt * (1.4 + an.speed);
      an.x += Math.cos(an.phase) * 14 * dt;
      an.y += Math.sin(an.phase * 0.8) * 11 * dt;
      an.x = clamp(an.x, 110, 670);
      an.y = clamp(an.y, 2080, 2450);
    }
  }


  function updatePushables(world, frogs, dt) {
    if (!world) return;
    world._frogsRef = frogs; /* mech5 */
    var C = global.FroggiesCanon;
    var shove = C && C.shoveSmallProp;
    var tick = C && C.tickPushable;
    var yardB = { x0: 110, y0: 2080, x1: 670, y1: 2450 };
    var garB = { x0: 710, y0: 1430, x1: 1160, y1: 1900 };
    var mapB = { x0: 40, y0: 40, x1: MAP_W - 40, y1: MAP_H - 40 };
    var list = [];
    var i, p, f;
    if (world.toys) for (i = 0; i < world.toys.length; i++) { if (world.toys[i].wrecked) continue; list.push({ prop: world.toys[i], bounds: world.toys[i].inGarage ? garB : mapB, r: world.toys[i].r || 9, strength: 1 }); }
    if (world.animals) for (i = 0; i < world.animals.length; i++) { if (world.animals[i].wrecked) continue; list.push({ prop: world.animals[i], bounds: yardB, r: 10 + (world.animals[i].size || 1) * 4, strength: 0.85 }); }
    if (world.ambient) for (i = 0; i < world.ambient.length; i++) {
      if (world.ambient[i].kind === "pollen" || world.ambient[i].pushable)
        list.push({ prop: world.ambient[i], bounds: mapB, r: Math.max(4, (world.ambient[i].r || 2) * 2.2), strength: 0.55, ambient: true });
    }
    for (i = 0; i < list.length; i++) {
      p = list[i].prop;
      p.r = list[i].r;
      if (frogs && shove) {
        for (var fi = 0; fi < frogs.length; fi++) {
          f = frogs[fi];
          if (!f || !f.alive) continue;
          if (f.inTruck) continue; /* truck uses resolveSolid; don't bat props with cab */
          shove(p, f.x, f.y, 20, f.vx || 0, f.vy || 0, { propR: list[i].r, strength: list[i].strength });
        }
      }
      if (list[i].ambient) {
        /* hop1: ambient keeps updateFx drift; just decay shove impulse */
        var asp = Math.hypot(p.vx || 0, p.vy || 0);
        if (asp > 28) { p.vx *= Math.exp(-1.8 * dt); p.vy *= Math.exp(-1.8 * dt); }
      } else if (tick) {
        tick(p, dt, { friction: 4.6, bounce: 0.4, bounds: list[i].bounds });
      }
    }
    /* Keep backyard animals in yard after shove */
    if (world.animals) {
      for (i = 0; i < world.animals.length; i++) {
        var an = world.animals[i];
        an.x = clamp(an.x, 110, 670);
        an.y = clamp(an.y, 2080, 2450);
      }
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
    for (i = world.bubbles.length - 1; i >= 0; i--) {
      var b = world.bubbles[i];
      b.life -= dt; b.x += b.vx * dt; b.y += b.vy * dt; b.vy -= 12 * dt; b.r += dt * 1.2;
      if (b.life <= 0) world.bubbles.splice(i, 1);
    }
    for (i = world.sparks.length - 1; i >= 0; i--) {
      var k = world.sparks[i];
      k.life -= dt; k.x += k.vx * dt; k.y += k.vy * dt; k.vy += 220 * dt;
      if (k.life <= 0) world.sparks.splice(i, 1);
    }
    /* mech3: big missiles fly; on hit / timeout → boom + wreck props
       mechgun1: omnigun shells permanently remove hit targets */
    if (!world.shells) world.shells = [];
    for (i = world.shells.length - 1; i >= 0; i--) {
      var sh = world.shells[i];
      sh.life -= dt;
      sh.x += sh.vx * dt;
      sh.y += sh.vy * dt;
      var expired = sh.life <= 0 || sh.x < -40 || sh.y < -40 || sh.x > MAP_W + 40 || sh.y > MAP_H + 40;
      var hit = false;
      if (!expired) {
        if (sh.omnigun) {
          if (shellHitsOmnigunTarget(world, sh, world._frogsRef)) hit = true;
        } else {
          var lists = [world.toys || [], world.animals || []];
          for (var li = 0; li < lists.length && !hit; li++) {
            var arr = lists[li];
            for (var j = 0; j < arr.length; j++) {
              var prop = arr[j];
              if (!prop || prop.wrecked || prop.goneForever) continue;
              var pr = prop.r || 10;
              var dx = prop.x - sh.x, dy = prop.y - sh.y;
              var rr = pr + (sh.r || 28);
              if (dx * dx + dy * dy < rr * rr) { hit = true; break; }
            }
          }
          if (!hit && shellHitsBlastableMech(world, sh)) hit = true; /* mech5: Rexy 1000 only */
        }
      }
      if (hit || expired) {
        var br = sh.blastR != null ? sh.blastR : 118;
        var bf = sh.blastForce != null ? sh.blastForce : 520;
        if (hit || (expired && sh.life <= 0)) {
          if (sh.omnigun) {
            blastOmnigunKill(world, sh.x, sh.y, br, bf, world._frogsRef, sh);
            spawnBoom(world, sh.x, sh.y, sh.size != null ? sh.size * 0.7 : 1.6);
          } else {
            blastWreckProps(world, sh.x, sh.y, br, bf, world._frogsRef);
            spawnBoom(world, sh.x, sh.y, sh.size != null ? sh.size * 0.55 : 1.2);
          }
        }
        world.shells.splice(i, 1);
      }
    }
    /* mech3: boom rings + wrecked prop decay / remove */
    if (!world.booms) world.booms = [];
    for (i = world.booms.length - 1; i >= 0; i--) {
      var bm = world.booms[i];
      bm.life -= dt;
      var u = 1 - Math.max(0, bm.life) / (bm.maxLife || 0.6);
      bm.r = (bm.maxR || 90) * (0.25 + 0.75 * u);
      if (bm.life <= 0) world.booms.splice(i, 1);
    }
    function tickWreck(arr) {
      if (!arr) return;
      for (var wi = 0; wi < arr.length; wi++) {
        var wp = arr[wi];
        if (!wp || !wp.wrecked) continue;
        if ((wp.wreckT || 0) > 0) {
          wp.wreckT -= dt;
          wp.x += (wp.vx || 0) * dt;
          wp.y += (wp.vy || 0) * dt;
          wp.vx = (wp.vx || 0) * Math.max(0, 1 - 3.2 * dt);
          wp.vy = (wp.vy || 0) * Math.max(0, 1 - 3.2 * dt);
          if (wp.spin) wp.ang = (wp.ang || 0) + wp.spin * dt;
        }
        if (wp.goneForever) {
          /* mechgun1: fade out permanently — never restore */
          if ((wp.wreckT || 0) <= 0) { wp.hidden = true; wp.r = 0; }
          continue;
        }
        wp.respawnT = (wp.respawnT != null ? wp.respawnT : 7) - dt;
        if (wp.respawnT <= 0) {
          wp.wrecked = false;
          wp.wreckT = 0;
          wp.respawnT = 0;
          wp.vx = 0; wp.vy = 0; wp.spin = 0; wp.ang = 0;
          wp.x = wp.homeX != null ? wp.homeX : wp.x;
          wp.y = wp.homeY != null ? wp.homeY : wp.y;
          if (wp.homeR != null) wp.r = wp.homeR;
          if (wp.homeSize != null) wp.size = wp.homeSize;
        }
      }
    }
    tickWreck(world.toys);
    tickWreck(world.animals);
    /* mech6: Rexy 1000-mech comes back after blast timer */
    var Cresp = global.FroggiesCanon;
    if (Cresp && Cresp.tickMechRespawn) {
      var revived = Cresp.tickMechRespawn(dt);
      if (revived && revived.length) world._mechRespawnToast = "Rexy 1000-story mech is back!";
    }
    /* spear1: thrust FX + knockdown timer */
    if (!world.spears) world.spears = [];
    for (i = world.spears.length - 1; i >= 0; i--) {
      var sp = world.spears[i];
      sp.life -= dt;
      if (sp.life <= 0) world.spears.splice(i, 1);
    }
    if (Cresp && Cresp.tickMechKnock) {
      var gotUp = Cresp.tickMechKnock(dt);
      if (gotUp && gotUp.length) world._spearRecoverToast = "Trillion mech is back up!";
    }
    /* polish10: hard caps if arrays ballooned */
    var capsFx = particleCaps();
    if (world.dust && world.dust.length > capsFx.dust) world.dust.length = capsFx.dust;
    if (world.sparks && world.sparks.length > capsFx.sparks) world.sparks.length = capsFx.sparks;
    if (world.ambient && world.ambient.length > capsFx.ambient) world.ambient.length = capsFx.ambient;
    if (world.ambient && world.ambient.length > particleCaps().ambient) world.ambient.length = particleCaps().ambient;
    if (world.ripples && world.ripples.length > 16) world.ripples.length = 16;
    world.ambientT = (world.ambientT || 0) + dt;
    /* polish5: ambient pollen / fireflies drift */
    if (!world.ambient) world.ambient = [];
    for (i = 0; i < world.ambient.length; i++) {
      var a = world.ambient[i];
      a.phase += dt * (a.kind === "firefly" ? 3.2 : 1.4);
      a.x += (a.vx + Math.sin(a.phase) * 8) * dt;
      a.y += (a.vy + Math.cos(a.phase * 0.7) * 6) * dt;
      if (a.x < 40) a.x = MAP_W - 40;
      if (a.x > MAP_W - 40) a.x = 40;
      if (a.y < 40) a.y = MAP_H - 40;
      if (a.y > MAP_H - 40) a.y = 40;
    }
    if (!world.ripples) world.ripples = [];
    for (i = world.ripples.length - 1; i >= 0; i--) {
      var rp = world.ripples[i];
      rp.life -= dt * 0.55;
      rp.r += (rp.maxR || 48) * dt * 0.9;
      if (rp.life <= 0) world.ripples.splice(i, 1);
    }
    if (!world.sparkles) world.sparkles = [];
    for (i = world.sparkles.length - 1; i >= 0; i--) {
      var sk = world.sparkles[i];
      sk.life -= dt;
      sk.x += sk.vx * dt;
      sk.y += sk.vy * dt;
      sk.vy += 60 * dt;
      sk.vx *= 0.96;
      if (sk.life <= 0) world.sparkles.splice(i, 1);
    }
    if (!world.kitFx) world.kitFx = [];
    for (i = world.kitFx.length - 1; i >= 0; i--) {
      var kx = world.kitFx[i];
      kx.age = (kx.age || 0) + dt;
      kx.life -= dt;
      if (kx.life <= 0) world.kitFx.splice(i, 1);
    }
    world.ambientT = (world.ambientT || 0) - dt;
    if (world.ambientT <= 0) {
      world.ambientT = 0.55 + Math.random() * 0.85;
      var pond = AREAS[2];
      spawnRipple(world,
        pond.x + 60 + Math.random() * (pond.w - 120),
        pond.y + 60 + Math.random() * (pond.h - 120),
        36 + Math.random() * 28);
    }
    if ((world.garageOpen || 0) > 0) world.garageOpen = Math.max(0, world.garageOpen - dt * 0.15);
  }

  function tickDrive(world, ent, dt) {
    var result = { jumped: false, landed: false, scrapGain: 0, splashed: false, onWater: false, landShake: false };
    var gWalk = 720; /* hop2: snappier loco hop arcs (~2 hops/sec) */
    var gTruck = 560;
    var wet = inPond(ent.x, ent.y);
    if (ent.inSub) {
      ent.waterSub = Math.min(1.15, Math.max(0.75, (ent.waterSub || 0.85) + dt * 0.15));
      ent.wakePhase = (ent.wakePhase || 0) + dt * (2.2 + Math.hypot(ent.vx || 0, ent.vy || 0) * 0.01);
      if (wet && Math.random() < dt * 4) spawnBubbles(world, ent.x, ent.y, 2);
      result.onWater = true;
      return result;
    }
    if (!ent.inTruck) {
      /* qa1: feet stand on the same track deck trucks use — not the flat map. */
      var gndFoot = (ent.inSwim || ent.inSub) ? 0 : (ent.groundZ || 0);
      var aboveFoot = (ent.z || 0) - gndFoot;
      if (aboveFoot > 0.15 || (ent.zVel || 0) !== 0) {
        ent.zVel -= gWalk * dt;
        ent.z += ent.zVel * dt;
        if (ent.z <= gndFoot) {
          ent.z = gndFoot;
          if (ent.zVel < -40) result.landed = true;
          ent.zVel = 0;
          if (result.landed) {
            ent.hopSquash = 1;
            ent.hopLandT = 0;
          }
          if (wet) {
            spawnSplash(world, ent.x, ent.y, 6);
            ent.waterSub = Math.max(ent.waterSub || 0, 0.35);
            result.splashed = true;
          } else if (result.landed) {
            spawnDust(world, ent.x, ent.y, 4);
          }
        }
      } else {
        ent.z = gndFoot;
        ent.zVel = 0;
      }
      if (wet && (ent.z || 0) <= gndFoot + 0.5) {
        ent.waterSub = Math.max(0.12, (ent.waterSub || 0) * Math.exp(-1.8 * dt));
        if (Math.hypot(ent.vx, ent.vy) > 40 && Math.random() < dt * 3) {
          spawnSplash(world, ent.x, ent.y, 1);
        }
      } else {
        ent.waterSub = Math.max(0, (ent.waterSub || 0) - dt * 1.6);
      }
      /* polish4: footstep dust puffs while walking on dry ground */
      var walkSp = Math.hypot(ent.vx, ent.vy);
      if (!wet && (ent.z || 0) <= (ent.groundZ || 0) + 0.5 && walkSp > 40) {
        ent.dustTimer = (ent.dustTimer || 0) - dt;
        if (ent.dustTimer <= 0) {
          ent.dustTimer = 0.14 + Math.random() * 0.08;
          spawnDust(world, ent.x - ent.facing * 4, ent.y + 6, 2);
        }
      } else {
        ent.dustTimer = 0;
      }
      ent.truckBounce = 0;
      ent.speedBoost = 1;
      if (ent.hopSquash > 0) ent.hopSquash = Math.max(0, ent.hopSquash - dt * 4.2);
      if (ent.hopStretch > 0) ent.hopStretch = Math.max(0, ent.hopStretch - dt * 2.8);
      return result;
    }
    var speed = Math.hypot(ent.vx, ent.vy);
    /* truck2: follow track ribbon/mound/ramp elevation as ground plane */
    var canonH = global.FroggiesCanon;
    var targetGround = 0;
    if (onTrack(ent.x, ent.y) && canonH && canonH.trackElevAt) {
      targetGround = canonH.trackElevAt(ent.x, ent.y) || 0;
    }
    var prevGround = ent.groundZ != null ? ent.groundZ : targetGround;
    if (onTrack(ent.x, ent.y)) {
      /* Snappy contact so hills/ramps read — not smoothed flat */
      ent.groundZ = prevGround + (targetGround - prevGround) * Math.min(1, 22 * dt);
    } else {
      ent.groundZ = (ent.groundZ || 0) * Math.exp(-7 * dt);
      if (Math.abs(ent.groundZ) < 0.4) ent.groundZ = 0;
    }
    var groundZ = ent.groundZ || 0;
    var airAbove = (ent.z || 0) - groundZ;
    result.onWater = wet && airAbove < 4;
    /* track3: wheel scale from entity or shared canon live value */
    var canonW = global.FroggiesCanon;
    var ws = ent.wheelScale != null ? ent.wheelScale : (canonW && canonW.getWheelScale ? canonW.getWheelScale() : 1);
    if (canonW && canonW.getWheelScale) {
      ws = canonW.getWheelScale();
      ent.wheelScale = ws;
    }
    var jumpMul = canonW && canonW.wheelJumpMul ? canonW.wheelJumpMul(ws) : (0.9 + (ws - 1) * 0.55);
    var bounceMul = canonW && canonW.wheelBounceMul ? canonW.wheelBounceMul(ws) : (0.85 + ws * 0.55);
    var clearZ = canonW && canonW.wheelClearanceZ ? canonW.wheelClearanceZ(ws) : (ws - 1) * 18;
    /* Bigger wheels ride a bit higher on contact */
    groundZ = groundZ + clearZ * 0.35;
    airAbove = (ent.z || 0) - groundZ;

    var ramp = rampAt(ent.x, ent.y);
    if (ramp && airAbove <= 6 + clearZ * 0.2 && speed > 55) {
      var boost = ramp.boost * clamp(speed / 190, 0.6, 1.55) * jumpMul;
      ent.zVel = 280 * boost;
      ent.z = Math.max(ent.z, groundZ + 8 + clearZ * 0.15);
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

    /* track3: big rock hit — hard bounce proportional to wheel size + speed */
    ent.rockCool = Math.max(0, (ent.rockCool || 0) - dt);
    if (ent.rockCool <= 0 && airAbove <= 10 + clearZ * 0.25 && canonW && canonW.rockHitAt) {
      var rock = canonW.rockHitAt(ent.x, ent.y, 10 + ws * 6);
      if (rock && speed > 40) {
        var into = Math.hypot(ent.x - rock.x, ent.y - rock.y);
        var nx = (ent.x - rock.x) / Math.max(1, into);
        var ny = (ent.y - rock.y) / Math.max(1, into);
        var rb = (rock.bounce || 1.8) * bounceMul * clamp(speed / 160, 0.55, 1.85);
        ent.zVel = Math.max(ent.zVel || 0, 220 * rb);
        ent.z = Math.max(ent.z || 0, groundZ + 10 + clearZ * 0.2);
        /* Shove off the rock hard */
        var shove = (140 + speed * 0.55) * rb * 0.55;
        ent.vx += nx * shove;
        ent.vy += ny * shove;
        ent.truckBounce = Math.max(ent.truckBounce || 0, 10 + rb * 6 + ws * 3);
        ent.rockCool = 0.28;
        result.jumped = true;
        result.rockHit = true;
        world.stuntCombo += 1;
        var rg = 8 + world.stuntCombo * 4;
        world.scrap += rg;
        result.scrapGain = (result.scrapGain || 0) + rg;
        spawnDust(world, rock.x, rock.y, 8);
        spawnSparks(world, rock.x, rock.y, 8);
        result.landShake = true;
      }
    }
    /* truck2: crest launch — fast over downhill lip after a rise */
    /* dGround from raw track elev (ignore wheel clearance offset) */
    var dGround = (ent.groundZ || 0) - prevGround;
    if (airAbove <= 6 + clearZ * 0.15 && speed > 75 && dGround < -1.4) {
      var crest = Math.min(400, (speed * 0.55 + (-dGround) * 14) * jumpMul);
      if (crest > 60) {
        ent.zVel = Math.max(ent.zVel || 0, crest);
        ent.z = Math.max(ent.z || 0, groundZ + 6);
        result.jumped = true;
        if (!result.scrapGain) {
          world.stuntCombo += 1;
          var cg = 6 + world.stuntCombo * 3;
          world.scrap += cg;
          result.scrapGain = cg;
        }
      }
    }
    airAbove = (ent.z || 0) - groundZ;
    if (airAbove > 0.15 || ent.zVel !== 0) {
      var g = gTruck;
      /* polish9: brief air hang at jump apex so elevation jumps feel */
      if (airAbove > 22 && Math.abs(ent.zVel) < 95) g *= 0.38;
      else if (Math.abs(ent.zVel) < 60) g *= 0.78;
      /* Water drag while airborne over pond softens plunge */
      if (wet) g *= 0.92;
      ent.zVel -= g * dt;
      ent.z += ent.zVel * dt;
      world.airTime += dt;
      if (ent.z <= groundZ) {
        var impact = Math.max(0, -(ent.zVel || 0));
        ent.z = groundZ;
        result.landed = true;
        ent.hopLandT = 0;
        if (ent.hopSquash == null || ent.hopSquash < 0.5) ent.hopSquash = 1;
        if (world.airTime > 0.35) {
          var airBonus = Math.floor(world.airTime * 25);
          world.scrap += airBonus;
          result.scrapGain = (result.scrapGain || 0) + airBonus;
        }
        var plunge = impact + world.airTime * 90;
        var airSnap = world.airTime;
        world.airTime = 0;
        /* truck1 + track3: land bounce from impact — bigger wheels rebound more */
        if (impact > 55 && !wet) {
          ent.zVel = Math.min(155 + ws * 40, impact * 0.3 * bounceMul);
          ent.truckBounce = Math.max(ent.truckBounce || 0, 7 + impact * 0.045 * bounceMul + ws * 2);
        } else {
          ent.zVel = 0;
        }
        if (wet) {
          /* Visual: surface drive default; hard plunge / long air reads as going under */
          var sub = clamp(0.42 + plunge / 320 + airSnap * 0.35, 0.48, 1.2);
          ent.waterSub = Math.max(ent.waterSub || 0, sub);
          spawnSplash(world, ent.x, ent.y, 10 + Math.floor(plunge / 40));
          if (sub > 0.7) spawnBubbles(world, ent.x, ent.y, 6 + Math.floor(plunge / 50));
          scareFishies(world, ent.x, ent.y);
          spawnRipple(world, ent.x, ent.y, 55 + plunge * 0.08);
          result.splashed = true;
        } else {
          spawnDust(world, ent.x, ent.y, 5);
          ent.waterSub = Math.max(0, (ent.waterSub || 0) - 0.5);
        }
        result.landShake = true; /* polish5: tiny screen shake on truck land */
      }
    } else {
      /* truck1: stick to track undulation while rolling */
      ent.z = groundZ;
      ent.zVel = 0;
      if (speed < 40) world.stuntCombo = 0;
    }

    airAbove = (ent.z || 0) - groundZ;
    if (wet && airAbove <= 0.5) {
      /* On the water surface — settle toward shallow waterline; expire dive tint */
      if ((ent.waterSub || 0) > 0.4) {
        ent.waterSub = Math.max(0.26, ent.waterSub - dt * 0.75);
      } else {
        ent.waterSub = 0.22 + Math.min(0.12, speed / 2000);
      }
      ent.wakePhase = (ent.wakePhase || 0) + dt * (2.2 + speed * 0.012);
      /* polish4: spray when skating the surface; bubbles when under */
      if ((ent.waterSub || 0) > 0.7) {
        if (Math.random() < dt * (4 + speed * 0.02)) {
          spawnBubbles(world, ent.x + (Math.random() - 0.5) * 20, ent.y, 2 + (speed > 120 ? 2 : 0));
        }
      } else if (speed > 28 && Math.random() < dt * (3.5 + speed * 0.015)) {
        spawnSplash(world, ent.x - ent.facing * 16, ent.y + 4, 3); /* spray */
      }
      if (speed > 90 && Math.random() < dt * 1.2) {
        world.scrap += 1;
        result.scrapGain += 1;
      }
    } else if (!wet) {
      ent.waterSub = Math.max(0, (ent.waterSub || 0) - dt * 2.2);
    }

    /* polish4: truck bounce — suspension hop while rolling */
    ent.bouncePhase = (ent.bouncePhase || 0) + dt * (3.2 + speed * 0.018);
    airAbove = (ent.z || 0) - groundZ;
    if (airAbove <= 2.5 + clearZ * 0.1) {
      var bounceAmp = clamp(speed / 280, 0, 1.35) * bounceMul;
      if (onTrack(ent.x, ent.y)) bounceAmp *= 1.45;
      if (wet) bounceAmp *= 0.55;
      var baseBounce = Math.sin(ent.bouncePhase * 2.4) * bounceAmp * 3.4
        + Math.sin(ent.bouncePhase * 5.1) * bounceAmp * 1.2;
      /* Add hill chatter from elevation deltas */
      if (onTrack(ent.x, ent.y)) baseBounce += clamp(dGround * 0.35 * bounceMul, -5, 6);
      ent.truckBounce = baseBounce;
      if (!wet && speed > 60 && Math.random() < dt * (1.2 + speed * 0.008)) {
        spawnDust(world, ent.x - ent.facing * 18, ent.y + 8, 1);
      }
    } else {
      ent.truckBounce = (ent.truckBounce || 0) * 0.85;
    }

    if (onTrack(ent.x, ent.y) && speed > 140 && Math.abs(ent.steerX) > 0.6) {
      if (Math.random() < dt * 4) {
        world.scrap += 1;
        result.scrapGain += 1;
        spawnSparks(world, ent.x - ent.facing * 18, ent.y + 6, 2);
      }
    }
    /* polish9: lap sparkle when crossing start/finish gate */
    if (ent.inTruck && onTrack(ent.x, ent.y) && ((ent.z || 0) - (ent.groundZ || 0)) < 8) {
      world.lapCooldown = Math.max(0, (world.lapCooldown || 0) - dt);
      var side = gateSide(ent.x, ent.y);
      if (nearGate(ent.x, ent.y) && world.lapSide !== 0 && side * world.lapSide < 0 && world.lapCooldown <= 0) {
        world.lapCount = (world.lapCount || 0) + 1;
        world.lapCooldown = 2.4;
        result.scrapGain = (result.scrapGain || 0) + 8;
        world.scrap += 8;
        spawnSparkle(world, TRACK_GATE.x, TRACK_GATE.y, 22);
        spawnSparks(world, TRACK_GATE.x, TRACK_GATE.y, 10);
        result.lap = world.lapCount;
      }
      if (nearGate(ent.x, ent.y) || Math.abs(side) > 40) world.lapSide = side >= 0 ? 1 : -1;
    }

    /* polish5: track dust plume when trucks race */
    if (ent.inTruck && onTrack(ent.x, ent.y) && speed > 110 && ((ent.z || 0) - (ent.groundZ || 0)) <= 2.5) {
      if (Math.random() < dt * (2.8 + speed * 0.012)) {
        spawnDust(world, ent.x - ent.facing * 22, ent.y + 10, 2 + (speed > 200 ? 2 : 0));
        spawnDust(world, ent.x - ent.facing * 10, ent.y - 6, 1);
      }
    }
    return result;
  }

  function moveEntity(ent, dt, speed, world) {
    if (ent && ent.sessionDead) return; /* mechgun1 */
    /* air1: flying craft owns motion — never walk + fly same frame */
    var AirM = global.FroggiesAir;
    if (AirM && AirM.isAirborneFrog && AirM.isAirborneFrog(ent) && world) {
      var kindM = AirM.frogAirKind(ent);
      var craftM = AirM.ensureCraft(world, kindM);
      var mx = ent.steerX || 0, my = ent.steerY || 0;
      if (ent.steerScreen) {
        var swAir = screenSteerToWorld(mx, my);
        mx = swAir.x; my = swAir.y;
      }
      var climb = ent.climbIn || 0;
      var boost = !!(ent.airBoost || (ent.speedBoost && ent.speedBoost > 1.05));
      if (AirM.isPilot(craftM, ent)) {
        AirM.tickFlight(world, null, ent, dt, mx, my, climb, boost);
        ent.x = craftM.x; ent.y = craftM.y; ent.z = craftM.z;
        ent.vx = craftM.vx; ent.vy = craftM.vy; ent.zVel = craftM.vz;
        ent.faceAngle = craftM.faceAngle;
        ent.groundZ = 0;
        ent.hopSquash = 0;
        ent.inSwim = false;
        ent._airPilot = true;
      } else if (craftM) {
        ent.x = craftM.x; ent.y = craftM.y; ent.z = craftM.z || 0;
        ent.vx = craftM.vx || 0; ent.vy = craftM.vy || 0;
        ent.faceAngle = craftM.faceAngle;
        ent.groundZ = 0;
        ent._airPilot = false;
      }
      if (craftM) ent._airRotor = craftM.rotor || 0;
      return;
    }
    /* spear1: knocked mech cannot move / steer while down */
    {
      var Ckn = global.FroggiesCanon;
      if (ent.inMech && Ckn && Ckn.isPilotKnocked && Ckn.isPilotKnocked(ent)) {
        ent.vx = 0; ent.vy = 0; ent.steerX = 0; ent.steerY = 0;
        ent.throttle = 0; ent.brake = 0;
        ent.zVel = Math.min(ent.zVel || 0, 0);
        return;
      }
    }
    /* polish3 + polish10: snappier walk/drive — quicker ramp + firmer stop */
    /* tapsteer1: noticeably faster walk + drive */
    /* mech5: per-vehicle / per-mech drive feel (Ripsaw fastest auto) */
    var Cdrv = global.FroggiesCanon;
    var walkMax = 345;
    var truckMax = 420;
    var mechMax = 195;
    var subMax = 280;
    var vStat = null, mStat = null;
    if ((ent.inTruck || ent.inSub) && Cdrv && Cdrv.vehicleDriveStats) {
      vStat = Cdrv.vehicleDriveStats(ent.inSub ? "submarine" : ent);
    }
    if (ent.inMech && Cdrv && Cdrv.mechDriveStats) mStat = Cdrv.mechDriveStats(ent.mechStories || ent);
    if (vStat) {
      if (ent.inSub) subMax *= vStat.maxSp || 1;
      else truckMax *= vStat.maxSp || 1;
    }
    if (mStat) mechMax *= mStat.maxSp || 1;
    var maxSp = (ent.inMech ? mechMax : ent.inSub ? subMax : ent.inTruck ? truckMax : walkMax) * (ent.speedBoost || 1);
    if (ent.inTruck && ent.dashTrail > 0) maxSp *= 1.28;
    if (typeof speed === "number") maxSp = speed * (ent.speedBoost || 1);
    var mx = ent.steerX;
    var my = ent.steerY;
    if (ent.steerScreen) {
      var sws = screenSteerToWorld(mx, my);
      mx = sws.x;
      my = sws.y;
    }
    var mag = Math.hypot(mx, my);
    if (mag > 1) { mx /= mag; my /= mag; }
    var Cwet = global.FroggiesCanon;
    var inStream = Cwet && Cwet.inYardStream ? Cwet.inYardStream(ent.x, ent.y) : false;
    var wetMove = (inPond(ent.x, ent.y) || inStream) && (ent.z || 0) < 3;
    var accel = ent.inMech ? 780 : ent.inSub ? 980 : ent.inTruck ? 1680 : 1520;
    var friction = ent.inMech ? 7.2 : ent.inSub ? 6.4 : ent.inTruck ? 5.6 : 9.6;
    if (vStat) { accel *= vStat.accel || 1; friction *= vStat.fric || 1; }
    if (mStat) { accel *= mStat.accel || 1; friction *= mStat.fric || 1; }
    /* ctrl1: RT accel / LT brake while boarded */
    var thr = (ent.inTruck || ent.inMech || ent.inSub) ? (ent.throttle || 0) : 0;
    var brk = (ent.inTruck || ent.inMech || ent.inSub) ? (ent.brake || 0) : 0;
    if (thr > 0.05) accel *= 1 + Math.min(1, thr) * 0.55;
    if ((ent.fricBoost || 0) > 1 && (ent.inTruck || ent.inMech || ent.inSub)) friction *= ent.fricBoost;
    if (brk > 0.05) {
      friction *= 1 + Math.min(1, brk) * 2.2;
      maxSp *= Math.max(0.32, 1 - Math.min(1, brk) * 0.6);
    }
    if (wetMove && ent.inTruck) {
      accel *= 0.82;
      friction *= 1.15;
      maxSp *= 0.88;
    } else if (wetMove && ent.inSub) {
      accel *= 0.9;
      friction *= 1.05;
      maxSp *= 0.92;
    } else if (wetMove) {
      /* yard1: creek is gentler than pond */
      if (inStream && !inPond(ent.x, ent.y)) {
        accel *= 0.88;
        maxSp *= 0.9;
      } else {
        /* pond1: swim feel — slower stroke in open water */
        accel *= 0.7;
        maxSp *= 0.72;
        friction *= 1.08;
      }
    }
    /* pond1: auto swim when walking into pond; leave swim on shore */
    if (!ent.inTruck && !ent.inMech && !ent.inSub && !ent.inHeli && !ent.inDrone) {
      if (inPond(ent.x, ent.y) && (ent.z || 0) < 8) {
        if (!ent.inSwim) {
          ent.inSwim = true;
          ent.waterSub = Math.max(ent.waterSub || 0, 0.35);
          if (world) spawnSplash(world, ent.x, ent.y, 4);
        }
        /* mech7: no hop height while freestyle swimming */
        ent.z = 0;
        ent.zVel = 0;
        ent.hopSquash = 0;
        ent.hopStretch = 0;
      } else if (ent.inSwim) {
        ent.inSwim = false;
      }
    } else if (ent.inSub) {
      ent.inSwim = false;
      /* keep sub inside / near pond */
      var pondB = AREAS[2];
      var pad = 20;
      if (ent.x < pondB.x + pad) ent.x = pondB.x + pad;
      if (ent.x > pondB.x + pondB.w - pad) ent.x = pondB.x + pondB.w - pad;
      if (ent.y < pondB.y + pad) ent.y = pondB.y + pad;
      if (ent.y > pondB.y + pondB.h - pad) ent.y = pondB.y + pondB.h - pad;
    }
    if (inStream && !ent.inTruck && !ent.inMech && (ent.z || 0) < 3 && world) {
      ent._streamSplash = (ent._streamSplash || 0) - (typeof dt === "number" ? dt : 0.016);
      var spNow = Math.hypot(ent.vx || 0, ent.vy || 0);
      if (ent._streamSplash <= 0 && spNow > 40) {
        spawnSplash(world, ent.x, ent.y, 2);
        ent._streamSplash = 0.22;
      }
    }
    var canon = global.FroggiesCanon;
    var airFoot = !ent.inTruck && !ent.inMech && !ent.inSub && !ent.inHeli && !ent.inDrone && (ent.z || 0) > 1.5;
    if (mag > 0.05) {
      var aim = Math.atan2(my, mx);
      if (ent.inMech) {
        /* Pilot as walking robot — face follows steer, heavier turn */
        var curM = (ent.faceAngle != null && isFinite(ent.faceAngle)) ? ent.faceAngle : aim;
        var turnM = 2.4 * (mStat && mStat.turn != null ? mStat.turn : 1);
        if (canon && canon.approachAngle) ent.faceAngle = canon.approachAngle(curM, aim, turnM * dt);
        else {
          var daM = aim - curM;
          while (daM > Math.PI) daM -= Math.PI * 2;
          while (daM < -Math.PI) daM += Math.PI * 2;
          var stepM = turnM * dt;
          if (daM > stepM) daM = stepM; if (daM < -stepM) daM = -stepM;
          ent.faceAngle = curM + daM;
        }
        ent.facing = Math.cos(ent.faceAngle) >= 0 ? 1 : -1;
        var mfx = Math.cos(ent.faceAngle), mfy = Math.sin(ent.faceAngle);
        ent.vx += (mfx * 0.72 + mx * 0.28) * accel * dt;
        ent.vy += (mfy * 0.72 + my * 0.28) * accel * dt;
      } else if (ent.inTruck || ent.inSub) {
        /* truck1 / pond1 sub: smooth yaw toward aim, then thrust along facing */
        var cur = (ent.faceAngle != null && isFinite(ent.faceAngle)) ? ent.faceAngle : aim;
        var turnRate = (3.6 + Math.min(2.4, Math.hypot(ent.vx, ent.vy) / 160)) * (vStat && vStat.turn != null ? vStat.turn : 1);
        if (canon && canon.approachAngle) ent.faceAngle = canon.approachAngle(cur, aim, turnRate * dt);
        else {
          var da = aim - cur;
          while (da > Math.PI) da -= Math.PI * 2;
          while (da < -Math.PI) da += Math.PI * 2;
          var step = turnRate * dt;
          if (da > step) da = step; if (da < -step) da = -step;
          ent.faceAngle = cur + da;
        }
        ent.facing = Math.cos(ent.faceAngle) >= 0 ? 1 : -1;
        var fx = Math.cos(ent.faceAngle), fy = Math.sin(ent.faceAngle);
        /* Steer into aim a little so turns feel responsive without flip-flip */
        var blend = 0.18;
        var ax = fx * (1 - blend) + mx * blend;
        var ay = fy * (1 - blend) + my * blend;
        var al = Math.hypot(ax, ay) || 1;
        ax /= al; ay /= al;
        var tvx = ax * maxSp;
        var tvy = ay * maxSp;
        ent.vx += (tvx - ent.vx) * Math.min(1, accel * dt / Math.max(60, maxSp));
        ent.vy += (tvy - ent.vy) * Math.min(1, accel * dt / Math.max(60, maxSp));
      } else if (airFoot) {
        /* hop2: mild air steer during hop arc — no ground hover-slide */
        var airA = accel * 0.38;
        var tvx = mx * maxSp;
        var tvy = my * maxSp;
        ent.vx += (tvx - ent.vx) * Math.min(1, airA * dt / Math.max(60, maxSp));
        ent.vy += (tvy - ent.vy) * Math.min(1, airA * dt / Math.max(60, maxSp));
        ent.faceAngle = aim;
        if (Math.abs(mx) > 0.08) ent.facing = mx >= 0 ? 1 : -1;
      } else {
        /* hop2: plant feet on brief ground — damp slide; hop impulse sets travel */
        var plant = Math.exp(-16 * dt);
        ent.vx *= plant;
        ent.vy *= plant;
        if (Math.hypot(ent.vx, ent.vy) < 10) { ent.vx = 0; ent.vy = 0; }
        ent.faceAngle = aim;
        if (Math.abs(mx) > 0.08) ent.facing = mx >= 0 ? 1 : -1;
      }
    } else {
      var damp = Math.exp(-friction * dt);
      ent.vx *= damp;
      ent.vy *= damp;
      if (Math.hypot(ent.vx, ent.vy) < 8) { ent.vx = 0; ent.vy = 0; }
    }
    var spd = Math.hypot(ent.vx, ent.vy);
    if (spd > maxSp) {
      ent.vx = (ent.vx / spd) * maxSp;
      ent.vy = (ent.vy / spd) * maxSp;
    }
    ent.x += ent.vx * dt;
    ent.y += ent.vy * dt;
    /* solid1: house/garage walls, mech pads, parked trucks — doorway gaps stay walkable */
    if (canon && canon.resolveSolid) {
      var ignoreMech = null;
      if (ent.inMech && canon.mechSolidId) ignoreMech = canon.mechSolidId(ent.mechId);
      else if (ent.inMech && ent.mechId) ignoreMech = String(ent.mechId).replace(/^mech-/, "mech");
      var airH = (ent.z || 0) - (ent.groundZ || 0);
      var solid = canon.resolveSolid(ent.x, ent.y, ent.inTruck ? 38 : ent.inSub ? 36 : ent.inMech ? 30 : 22, {
        garageOpen: (world && world.garageOpen) || 0,
        inTruck: !!ent.inTruck,
        inMech: !!ent.inMech,
        inSwim: !!ent.inSwim,
        inSub: !!ent.inSub,
        ignoreMechId: ignoreMech,
        softPond: !ent.inTruck && !ent.inMech && !ent.inSwim && !ent.inSub,
        airHeight: airH,
        airClearHeight: ent.inMech ? 22 : 28,
      });
      if (solid.hit) {
        var pdx = solid.x - ent.x, pdy = solid.y - ent.y;
        var plen = Math.hypot(pdx, pdy) || 1;
        var nx = pdx / plen, ny = pdy / plen;
        var into = ent.vx * nx + ent.vy * ny;
        if (into < 0) { ent.vx -= nx * into; ent.vy -= ny * into; }
        else { ent.vx *= 0.55; ent.vy *= 0.55; }
        ent.x = solid.x; ent.y = solid.y;
      }
    }
    /* mech5: clamps follow expanded MAP_* (more green/dirt; house size unchanged) */
    ent.x = clamp(ent.x, 40, MAP_W - 40);
    ent.y = clamp(ent.y, 40, MAP_H - 40);
    /* drivefix2: hotspot/park rides the occupied rig so a stale mech ring is not left under the frog */
    if (world && ((ent.inTruck && ent.truckId) || (ent.inMech && ent.mechId) || (ent.inSub && ent.subId))) {
      var rideId = ent.inTruck ? ent.truckId : (ent.inMech ? ent.mechId : ent.subId);
      if (canon && canon.setVehiclePark) canon.setVehiclePark(rideId, ent.x, ent.y);
      syncWorldHotspotPos(world, rideId, ent.x, ent.y);
    }
    if ((!ent.inTruck || ent.inMech) && spd > 18) {
      ent.walkPhase = (ent.walkPhase || 0) + dt * ((ent.inMech ? 5.5 : 8) + spd * 0.04);
    } else {
      ent.walkPhase = (ent.walkPhase || 0) * 0.9;
    }
    /* hop2: ANY move input → continuous hop cycle (launch → land → brief ground → next) */
    /* Mechs: no loco hop — piloting a robot feels like a heavy walk */
    /* qa1: deck height before the hop, so the launch leaves the ribbon not the map. */
    if (!ent.inTruck && !ent.inSub && canon && canon.trackElevAt) {
      var gOn = !ent.inSwim && onTrack(ent.x, ent.y);
      var gTarget = gOn ? (canon.trackElevAt(ent.x, ent.y) || 0) : 0;
      var gPrev = ent.groundZ != null ? ent.groundZ : gTarget;
      if (gOn) ent.groundZ = gPrev + (gTarget - gPrev) * Math.min(1, 22 * dt);
      else {
        ent.groundZ = (ent.groundZ || 0) * Math.exp(-7 * dt);
        if (Math.abs(ent.groundZ) < 0.4) ent.groundZ = 0;
      }
      if (ent.inSwim) ent.groundZ = 0;
    }
    if (!ent.inTruck && !ent.inMech && !ent.inSub && !ent.inHeli && !ent.inDrone && !ent.inSwim && canon && canon.tickLocoHop) {
      var wantHop = mag > 0.05;
      var launched = canon.tickLocoHop(ent, dt, {
        moving: wantHop,
        up: 265,
        lift: 10,
        groundHold: 0.011,
        groundEps: 1.2,
      });
      if (launched && mag > 0.05) {
        var hopSp = Math.min(maxSp * 0.98, 400);
        ent.vx = mx * hopSp;
        ent.vy = my * hopSp;
      }
    }
    if (ent.cd > 0) ent.cd -= dt;
    if (ent.invuln > 0) ent.invuln -= dt;
    if (ent.dashTrail > 0) ent.dashTrail -= dt;
  }

  function syncWorldHotspotPos(world, id, x, y) {
    if (!world || !world.hotspots || !id) return;
    var C = global.FroggiesCanon;
    var sid = C && C.mechSolidId ? C.mechSolidId(id) : null;
    for (var i = 0; i < world.hotspots.length; i++) {
      var h = world.hotspots[i];
      if (h.id === id || (sid && (h.solidId === sid || (C && C.mechSolidId && C.mechSolidId(h.id) === sid)))) {
        h.x = x; h.y = y;
      }
    }
    if (world.trucks) {
      var tid = id === "truck-shared" ? "shared" : String(id || "").replace(/^truck-/, "");
      for (var t = 0; t < world.trucks.length; t++) {
        if (world.trucks[t].id === tid || ("truck-" + world.trucks[t].id) === id) {
          world.trucks[t].x = x; world.trucks[t].y = y;
        }
      }
    }
  }

  function parkVehicleHere(world, id, x, y) {
    var C = global.FroggiesCanon;
    if (C && C.setVehiclePark) C.setVehiclePark(id, x, y);
    syncWorldHotspotPos(world, id, x, y);
  }

  function boardTruck(world, frogs, frog, hotspot) {
    /* drivefix1: ANY grounded frog may board free Cybertruck/Ripsaw/Tank (no frogId lock).
       frogId on truck hotspots is paint/label only — mech locks stay in boardMech. */
    if (!hotspot || hotspot.kind !== "truck") return false;
    if (frog.inTruck) {
      /* park1: leave Cybertruck where we EXIT — frog keeps walking from here */
      var parkId = frog.truckId || hotspot.id || "truck";
      var px = frog.x, py = frog.y;
      frog.inTruck = false;
      frog.truckMode = null;
      frog.truckId = null;
      frog.vehicleStyle = null;
      frog.z = 0;
      frog.zVel = 0;
      frog.groundZ = 0;
      if (world.sharedDriverId === frog.id) {
        world.sharedDriverId = null;
        for (var i = 0; i < frogs.length; i++) {
          var f = frogs[i];
          if (f.truckMode === "shared") {
            f.inTruck = false;
            f.truckMode = null;
            f.truckId = null;
            f.vehicleStyle = null;
            f.z = 0;
            f.zVel = 0;
            f.groundZ = 0;
            f.x = px; f.y = py;
          }
        }
      }
      parkVehicleHere(world, parkId, px, py);
      return true;
    }
    frog.x = hotspot.x;
    frog.y = hotspot.y;
    frog.inTruck = true;
    frog.truckId = hotspot.id;
    frog.truckMode = hotspot.mode || "solo";
    var Cstyle = global.FroggiesCanon;
    frog.vehicleStyle = (hotspot.vehicleStyle) || (Cstyle && Cstyle.vehicleStyleOf ? Cstyle.vehicleStyleOf(hotspot) : "cybertruck");
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

  function boardMech(world, frogs, frog, hotspot) {
    if (!frog) return false;
    var C = global.FroggiesCanon;
    /* EXIT anytime while piloting */
    if (frog.inMech) {
      /* park1: leave mech at EXIT pos — no snap back to yard pad */
      var parkMid = frog.mechId || (hotspot && hotspot.id) || "mech";
      var mpx = frog.x, mpy = frog.y;
      frog.inMech = false;
      frog.mechId = null;
      frog.mechStories = 0;
      frog.z = 0;
      frog.zVel = 0;
      frog.groundZ = 0;
      parkVehicleHere(world, parkMid, mpx, mpy);
      return true;
    }
    if (!hotspot || (hotspot.kind !== "mech" && !(C && C.isMechHotspot && C.isMechHotspot(hotspot)))) {
      return false;
    }
    /* mech1: ownership lock — wrong frog cannot enter */
    if (C && C.canBoardMech && !C.canBoardMech(frog.id, hotspot)) {
      return false;
    }
    /* Leave truck if somehow boarding from truck */
    if (frog.inTruck) {
      frog.inTruck = false;
      frog.truckMode = null;
      frog.truckId = null;
      frog.vehicleStyle = null;
    }
    frog.x = hotspot.x;
    frog.y = hotspot.y;
    frog.inMech = true;
    frog.mechId = hotspot.id || hotspot.solidId || "mech";
    frog.mechStories = hotspot.stories || 10;
    frog.z = 0;
    frog.zVel = 0;
    frog.groundZ = 0;
    return true;
  }

  function boardSub(world, frogs, frog, hotspot) {
    if (!frog) return false;
    var C = global.FroggiesCanon;
    /* EXIT anytime while in submarine → swim if still in pond, else shore */
    if (frog.inSub) {
      var parkSid = frog.subId || (hotspot && hotspot.id) || "submarine";
      var sx = frog.x, sy = frog.y;
      frog.inSub = false;
      frog.subId = null;
      frog.vehicleStyle = null;
      frog.z = 0;
      frog.zVel = 0;
      frog.groundZ = 0;
      parkVehicleHere(world, parkSid, sx, sy);
      if (inPond(sx, sy)) {
        frog.inSwim = true;
        frog.waterSub = Math.max(frog.waterSub || 0, 0.4);
        if (world) spawnSplash(world, sx, sy, 5);
      } else {
        frog.inSwim = false;
      }
      return true;
    }
    if (!hotspot || (hotspot.kind !== "submarine" && !(C && C.isSubHotspot && C.isSubHotspot(hotspot)))) {
      return false;
    }
    if (frog.inTruck) {
      frog.inTruck = false; frog.truckMode = null; frog.truckId = null;
    }
    if (frog.inMech) {
      frog.inMech = false; frog.mechId = null; frog.mechStories = 0;
    }
    frog.inSwim = false;
    frog.x = hotspot.x;
    frog.y = hotspot.y;
    frog.inSub = true;
    frog.subId = hotspot.id || "submarine";
    frog.vehicleStyle = "submarine";
    frog.z = 0;
    frog.zVel = 0;
    frog.groundZ = 0;
    frog.waterSub = Math.max(frog.waterSub || 0, 0.85);
    if (world) spawnSplash(world, frog.x, frog.y, 8);
    return true;
  }

  function boardAir(world, frogs, frog, hotspot) {
    var Air = global.FroggiesAir;
    if (!Air || !Air.boardAir) return false;
    var res = Air.boardAir(world, frogs, frog, hotspot);
    return !!(res && res.ok);
  }

  function boardAirResult(world, frogs, frog, hotspot) {
    var Air = global.FroggiesAir;
    if (!Air || !Air.boardAir) return { ok: false };
    return Air.boardAir(world, frogs, frog, hotspot) || { ok: false };
  }

  function pickAiChat(frogId) {
    var C = global.FroggiesCanon;
    var lines = (C && C.AI_CHAT && C.AI_CHAT[frogId]) || null;
    if (!lines || !lines.length) return "";
    return lines[Math.floor(Math.random() * lines.length)];
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
        f.faceAngle = localFrog.faceAngle;
        f.z = localFrog.z;
        f.waterSub = localFrog.waterSub || 0;
        f.wakePhase = localFrog.wakePhase || 0;
        f.idleBounce = (f.idleBounce || 0) + dt * 5;
        if (f.chatT > 0) f.chatT -= dt;
        continue;
      }
      /* air1: AI passengers ride if already seated (no auto-board); sync to craft */
      var AirAI = global.FroggiesAir;
      if (AirAI && AirAI.isAirborneFrog && AirAI.isAirborneFrog(f) && world) {
        var ck = AirAI.frogAirKind(f);
        var cr = AirAI.ensureCraft(world, ck);
        if (cr) {
          f.x = cr.x; f.y = cr.y; f.z = cr.z || 0;
          f.vx = cr.vx || 0; f.vy = cr.vy || 0;
          f.faceAngle = cr.faceAngle;
          f.idleBounce = (f.idleBounce || 0) + dt * 5;
          if (f.chatT > 0) f.chatT -= dt;
        }
        continue;
      }
      if (f.truckMode === "shared" && !(localFrog && localFrog.inTruck && localFrog.truckMode === "shared")) {
        f.inTruck = false;
        f.truckMode = null;
        f.truckId = null;
        f.vehicleStyle = null;
        f.z = 0;
      }
      /* drivefix2: solo truck / mech / sub — do not wander-yank or clear the seat */
      if ((f.inTruck && f.truckMode !== "shared") || f.inMech || f.inSub) {
        f.steerX = 0; f.steerY = 0;
        f.idleBounce = (f.idleBounce || 0) + dt * 4;
        if (f.chatT > 0) f.chatT -= dt;
        continue;
      }
      /* polish7: follow lag — stagger retarget so AI trail behind, not snap */
      f.aiTimer -= dt;
      var lag = f.followLag || 0.45;
      if (f.aiTimer <= 0) {
        f.aiTimer = 0.9 + lag + Math.random() * (1.4 + lag);
        if (localFrog && Math.random() < 0.72) {
          var behind = -localFrog.facing * (40 + lag * 80);
          f.targetX = localFrog.x + behind + (Math.random() - 0.5) * (90 + lag * 60);
          f.targetY = localFrog.y + (Math.random() - 0.5) * (90 + lag * 50);
        } else {
          var a = AREAS[Math.floor(Math.random() * AREAS.length)];
          f.targetX = a.x + a.w * (0.3 + Math.random() * 0.4);
          f.targetY = a.y + a.h * (0.3 + Math.random() * 0.4);
        }
      }
      var dx = f.targetX - f.x;
      var dy = f.targetY - f.y;
      var d = Math.hypot(dx, dy) || 1;
      if (d < 32) { f.steerX = 0; f.steerY = 0; }
      else {
        /* soft follow — damp steer by lag so they trail */
        var soft = 0.55 + (1 - Math.min(1, lag)) * 0.35;
        f.steerX = (dx / d) * soft;
        f.steerY = (dy / d) * soft;
      }
      moveEntity(f, dt, undefined, world);
      var spd = Math.hypot(f.vx || 0, f.vy || 0);
      /* polish7: idle bounce when settled */
      if (spd < 18 && !f.inTruck) {
        f.idleBounce = (f.idleBounce || 0) + dt * 4.2;
      } else {
        f.idleBounce = (f.idleBounce || 0) + dt * (2 + Math.min(4, spd * 0.02));
      }
      if (f.chatT > 0) f.chatT -= dt;
      f.chatCd = (f.chatCd || 3) - dt;
      if (f.chatCd <= 0 && f.chatT <= 0 && Math.random() < 0.55) {
        var line = pickAiChat(f.id);
        if (line) {
          f.chatLine = line;
          f.chatT = 2.2;
        }
        f.chatCd = 5.5 + Math.random() * 7;
      }
    }
  }

  function drawSky(ctx, w, h, t, camX, camY) {
    /* polish10: soft sunset / dusk shift over play time (one visible delight) */
    var day = ((t || 0) % 420) / 420; /* ~7 min full cycle */
    var dusk = day < 0.45 ? 0 : (day < 0.7 ? (day - 0.45) / 0.25 : (day < 0.9 ? 1 : (1 - (day - 0.9) / 0.1)));
    dusk = Math.max(0, Math.min(1, dusk));
    function lerpHex(a, b, u) {
      function parse(h) {
        return [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)];
      }
      var A = parse(a), B = parse(b);
      var r = Math.round(A[0] + (B[0] - A[0]) * u);
      var g0 = Math.round(A[1] + (B[1] - A[1]) * u);
      var bl = Math.round(A[2] + (B[2] - A[2]) * u);
      return "rgb(" + r + "," + g0 + "," + bl + ")";
    }
    var g = ctx.createLinearGradient(0, 0, 0, h);
    g.addColorStop(0, lerpHex("#122448", "#2a1848", dusk));
    g.addColorStop(0.2, lerpHex("#2a628f", "#c45c2a", dusk));
    g.addColorStop(0.45, lerpHex("#6fb3c9", "#f0a060", dusk * 0.85));
    g.addColorStop(0.66, lerpHex("#8ecf6e", "#6a8a4a", dusk * 0.55));
    g.addColorStop(1, lerpHex("#3a6826", "#2a4818", dusk * 0.4));
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, w, h);
    /* polish6: stronger layered ranch-hill parallax (FF depth feel) */
    var pxFar = -camX * 0.12, pyFar = -camY * 0.055;
    var pxMid = -camX * 0.24, pyMid = -camY * 0.11;
    var pxNear = -camX * 0.38, pyNear = -camY * 0.17;
    var pxFG = -camX * 0.52, pyFG = -camY * 0.22;
    function hillBand(fill, y0, amp, freq, phase, px, py, steps, yBot) {
      ctx.fillStyle = fill;
      ctx.beginPath();
      ctx.moveTo(0, y0 + py);
      for (var i = 0; i <= steps; i++) {
        var nx = (i / steps) * w + px;
        var ny = y0 + Math.sin(i * freq + phase + t * 0.035) * amp + py;
        ctx.lineTo(nx, ny);
      }
      ctx.lineTo(w, yBot); ctx.lineTo(0, yBot); ctx.closePath(); ctx.fill();
    }
    hillBand("rgba(22, 42, 72, 0.5)", h * 0.3, 28, 0.7, 0.2, pxFar, pyFar, 14, h * 0.52);
    hillBand("rgba(28, 58, 88, 0.42)", h * 0.33, 20, 0.95, 1.1, pxFar * 1.15, pyFar * 1.1, 16, h * 0.54);
    hillBand("rgba(36, 78, 48, 0.58)", h * 0.38, 18, 1.05, 1.4, pxMid, pyMid, 14, h * 0.58);
    hillBand("rgba(48, 108, 52, 0.55)", h * 0.43, 14, 1.15, 2.0, pxNear, pyNear * 0.85, 16, h * 0.62);
    hillBand("rgba(62, 128, 58, 0.42)", h * 0.48, 10, 1.35, 2.6, pxFG, pyFG * 0.7, 18, h * 0.66);
    /* soft ridge highlights */
    ctx.strokeStyle = "rgba(255,255,255,0.08)";
    ctx.lineWidth = 2;
    ctx.beginPath();
    for (var r = 0; r <= 16; r++) {
      var rx = (r / 16) * w + pxMid * 0.5;
      var ry = h * 0.38 + Math.sin(r * 1.05 + 1.4) * 16 + pyMid;
      if (r === 0) ctx.moveTo(rx, ry); else ctx.lineTo(rx, ry);
    }
    ctx.stroke();
    var sunX = w * (0.8 - dusk * 0.35) - camX * 0.035, sunY = h * (0.09 + dusk * 0.22) - camY * 0.018;
    var sg = ctx.createRadialGradient(sunX, sunY, 3, sunX, sunY, 78 + dusk * 30);
    sg.addColorStop(0, dusk > 0.5 ? "rgba(255, 210, 160, 1)" : "rgba(255, 250, 210, 1)");
    sg.addColorStop(0.25, dusk > 0.5 ? "rgba(255, 140, 70, 0.65)" : "rgba(255, 230, 140, 0.55)");
    sg.addColorStop(1, "rgba(255, 120, 60, 0)");
    ctx.fillStyle = sg;
    ctx.beginPath(); ctx.arc(sunX, sunY, 78 + dusk * 30, 0, Math.PI * 2); ctx.fill();
    for (var c = 0; c < 7; c++) {
      var layer = c < 3 ? 0.28 : 0.55;
      var cx = ((c * 150 + t * (5 + c) + pxMid * layer) % (w + 160)) - 80;
      var cy = h * (0.06 + (c % 3) * 0.032) + pyFar * 0.55;
      ctx.fillStyle = "rgba(255,255,255," + (0.1 + (c % 3) * 0.035) + ")";
      ctx.beginPath();
      ctx.ellipse(cx, cy, 54 - c * 2, 13, 0, 0, Math.PI * 2);
      ctx.ellipse(cx + 30, cy + 3, 40, 11, 0, 0, Math.PI * 2);
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

  function frogPilotsMech(frogs, solidId) {
    if (!frogs || !solidId) return false;
    var C = global.FroggiesCanon;
    for (var i = 0; i < frogs.length; i++) {
      var f = frogs[i];
      if (!f || !f.inMech) continue;
      var sid = C && C.mechSolidId ? C.mechSolidId(f.mechId) : String(f.mechId || "").replace(/^mech-/, "mech");
      if (sid === solidId) return true;
    }
    return false;
  }

  function drawMech(ctx, wx, wy, stories, camX, camY, vw, vh, tint, opts) {
    /* hop3: robot/mech silhouette (head·torso·arms·legs·glow eyes) — not a skyscraper prism
       spear1: opts.tipped = toppled knockdown pose */
    var p = project(wx, wy, camX, camY, vw, vh);
    var Cband = global.FroggiesCanon;
    var band = Cband && Cband.mechBand ? Cband.mechBand(stories) : (stories >= 1e12 ? "trillion" : stories >= 1000 ? "1000" : stories >= 100 ? "100" : "10");
    var hScale = band === "trillion" ? 460 : band === "1000" ? 310 : band === "100" ? 138 : 62;
    var wScale = band === "trillion" ? 108 : band === "1000" ? 72 : band === "100" ? 42 : 26;
    var s = p.depth;
    var H = hScale * s;
    var W = wScale * s;
    var baseY = p.y;
    var cx = p.x;
    var col = tint || "#94a3b8";
    var eyeCol = band === "trillion" ? "#f472b6" : band === "1000" ? "#fbbf24" : band === "100" ? "#67e8f9" : "#a5b4fc";
    var tipped = !!(opts && opts.tipped);
    if (!tipped && Cband && Cband.isMechKnocked) {
      var tipSid = band === "trillion" ? "mechTrillion" : band === "1000" ? "mech1000" : band === "100" ? "mech100" : "mech10";
      tipped = Cband.isMechKnocked(tipSid);
    }
    var _tipSaved = false;
    if (tipped) {
      ctx.save();
      _tipSaved = true;
      ctx.translate(cx, baseY);
      ctx.rotate(Math.PI / 2.15);
      ctx.translate(-cx, -baseY + H * 0.12);
    }
    if (band === "1000" || band === "trillion") {
      var haze = ctx.createRadialGradient(cx, baseY - H * 0.55, W * 0.2, cx, baseY - H * 0.4, W * (band === "trillion" ? 3.2 : 2.6));
      haze.addColorStop(0, band === "trillion" ? "rgba(244, 114, 182, 0.32)" : "rgba(252, 211, 77, 0.26)");
      haze.addColorStop(0.55, band === "trillion" ? "rgba(251, 113, 133, 0.1)" : "rgba(251, 191, 36, 0.08)");
      haze.addColorStop(1, "rgba(0,0,0,0)");
      ctx.fillStyle = haze;
      ctx.beginPath();
      ctx.ellipse(cx, baseY - H * 0.45, W * (band === "trillion" ? 2.6 : 2.2), H * 0.65, 0, 0, Math.PI * 2);
      ctx.fill();
    }
    /* Pad */
    ctx.fillStyle = "rgba(0,0,0,0.32)";
    ctx.beginPath();
    ctx.ellipse(cx, baseY + 5, W * 1.15, W * 0.36, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = "rgba(30,41,59,0.78)";
    ctx.beginPath();
    ctx.ellipse(cx, baseY + 2, W * 0.95, W * 0.3, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.strokeStyle = "#fbbf24";
    ctx.lineWidth = (band === "1000" || band === "trillion") ? (band === "trillion" ? 3.0 : 2.4) : 1.8;
    ctx.beginPath();
    ctx.ellipse(cx, baseY + 2, W * 0.75, W * 0.22, 0, 0, Math.PI * 2);
    ctx.stroke();

    function limb(x0, y0, x1, y1, thick, fill) {
      ctx.strokeStyle = "#020617";
      ctx.lineWidth = thick + 2.2 * s;
      ctx.lineCap = "round";
      ctx.beginPath(); ctx.moveTo(x0, y0); ctx.lineTo(x1, y1); ctx.stroke();
      ctx.strokeStyle = fill;
      ctx.lineWidth = thick;
      ctx.beginPath(); ctx.moveTo(x0, y0); ctx.lineTo(x1, y1); ctx.stroke();
    }
    function block(bx, by, bw, bh, fill) {
      var g = ctx.createLinearGradient(bx, by, bx + bw, by + bh);
      g.addColorStop(0, fill);
      g.addColorStop(0.55, "#64748b");
      g.addColorStop(1, "#1e293b");
      ctx.fillStyle = g;
      ctx.fillRect(bx, by, bw, bh);
      ctx.strokeStyle = "#020617";
      ctx.lineWidth = Math.max(1.2, 1.8 * s);
      ctx.strokeRect(bx, by, bw, bh);
    }

    /* Legs */
    var hipY = baseY - H * 0.38;
    var footY = baseY - 2 * s;
    var legT = Math.max(4, W * 0.18);
    limb(cx - W * 0.22, hipY, cx - W * 0.32, footY, legT, col);
    limb(cx + W * 0.22, hipY, cx + W * 0.32, footY, legT, col);
    /* Feet */
    block(cx - W * 0.48, footY - 3 * s, W * 0.28, 6 * s, col);
    block(cx + W * 0.2, footY - 3 * s, W * 0.28, 6 * s, col);

    /* Torso */
    var torsoH = H * 0.34;
    var torsoW = W * 0.72;
    var torsoY = baseY - H * 0.38 - torsoH;
    block(cx - torsoW * 0.5, torsoY, torsoW, torsoH, col);
    /* Chest plate glow */
    ctx.fillStyle = eyeCol;
    ctx.globalAlpha = 0.55;
    ctx.fillRect(cx - torsoW * 0.18, torsoY + torsoH * 0.28, torsoW * 0.36, torsoH * 0.18);
    ctx.globalAlpha = 1;

    /* Shoulders */
    var shW = W * 0.28, shH = H * 0.08;
    block(cx - torsoW * 0.5 - shW * 0.55, torsoY + torsoH * 0.05, shW, shH, col);
    block(cx + torsoW * 0.5 - shW * 0.45, torsoY + torsoH * 0.05, shW, shH, col);

    /* Arms */
    var armT = Math.max(3.5, W * 0.14);
    var shY = torsoY + shH * 0.5;
    limb(cx - torsoW * 0.5 - shW * 0.2, shY, cx - W * 0.78, torsoY + torsoH * 0.85, armT, col);
    limb(cx + torsoW * 0.5 + shW * 0.2, shY, cx + W * 0.78, torsoY + torsoH * 0.85, armT, col);
    /* Hands / fists */
    block(cx - W * 0.9, torsoY + torsoH * 0.78, W * 0.2, W * 0.16, col);
    block(cx + W * 0.7, torsoY + torsoH * 0.78, W * 0.2, W * 0.16, col);

    /* Head */
    var headH = H * 0.16;
    var headW = W * 0.48;
    var headY = torsoY - headH - 2 * s;
    block(cx - headW * 0.5, headY, headW, headH, col);
    /* Visor + glow eyes */
    ctx.fillStyle = "#0f172a";
    ctx.fillRect(cx - headW * 0.38, headY + headH * 0.28, headW * 0.76, headH * 0.38);
    ctx.fillStyle = eyeCol;
    ctx.shadowColor = eyeCol;
    ctx.shadowBlur = 8 * s;
    ctx.beginPath();
    ctx.arc(cx - headW * 0.16, headY + headH * 0.48, Math.max(1.6, headW * 0.1), 0, Math.PI * 2);
    ctx.arc(cx + headW * 0.16, headY + headH * 0.48, Math.max(1.6, headW * 0.1), 0, Math.PI * 2);
    ctx.fill();
    ctx.shadowBlur = 0;
    /* Antenna */
    ctx.strokeStyle = "#f8fafc";
    ctx.lineWidth = Math.max(1.2, 1.6 * s);
    ctx.beginPath();
    ctx.moveTo(cx, headY);
    var antH = band === "trillion" ? 26 : band === "1000" ? 18 : 10;
    var antR = band === "trillion" ? 4.2 : band === "1000" ? 3.2 : 2.2;
    ctx.lineTo(cx, headY - antH * s);
    ctx.stroke();
    ctx.fillStyle = "#f87171";
    ctx.beginPath();
    ctx.arc(cx, headY - antH * s, antR * s, 0, Math.PI * 2);
    ctx.fill();

    /* Armor band accents (read as mech plating, not windows) */
    var bands = band === "trillion" ? 7 : band === "1000" ? 5 : band === "100" ? 3 : 2;
    for (var bi = 0; bi < bands; bi++) {
      var by = torsoY + torsoH * (0.15 + bi * (0.55 / bands));
      ctx.fillStyle = bi % 2 === 0 ? "rgba(15,23,42,0.45)" : "rgba(248,250,252,0.12)";
      ctx.fillRect(cx - torsoW * 0.42, by, torsoW * 0.84, Math.max(1.5, H * 0.018));
    }

    ctx.fillStyle = "#fff";
    ctx.strokeStyle = "rgba(0,0,0,0.8)";
    ctx.lineWidth = 3.2;
    ctx.font = "bold " + Math.round((band === "trillion" ? 15 : band === "1000" ? 13 : 11) * s) + "px Segoe UI, system-ui, sans-serif";
    ctx.textAlign = "center";
    var label = (Cband && Cband.mechStoriesLabel) ? Cband.mechStoriesLabel(stories) : (stories + "-story mech");
    if (Cband && Cband.mechOwnerName) {
      var onLab = Cband.mechOwnerName(stories);
      if (onLab) label = onLab + " · " + label;
    }
    var labY = headY - (band === "trillion" ? 32 : band === "1000" ? 24 : 14) * s;
    ctx.strokeText(label, cx, labY);
    ctx.fillText(label, cx, labY);
    /* spear1: spear arm cue on Rexy 1000 when not tipped */
    if (band === "1000" && !tipped) {
      ctx.strokeStyle = "#e2e8f0";
      ctx.lineWidth = Math.max(2.2, 3.2 * s);
      ctx.lineCap = "round";
      ctx.beginPath();
      ctx.moveTo(cx + W * 0.55, baseY - H * 0.55);
      ctx.lineTo(cx + W * 1.55, baseY - H * 0.72);
      ctx.stroke();
      ctx.fillStyle = "#fbbf24";
      ctx.beginPath();
      ctx.moveTo(cx + W * 1.55, baseY - H * 0.72);
      ctx.lineTo(cx + W * 1.78, baseY - H * 0.78);
      ctx.lineTo(cx + W * 1.52, baseY - H * 0.62);
      ctx.closePath();
      ctx.fill();
    }
    if (_tipSaved) ctx.restore();
  }


  function drawYardDecor(ctx, camX, camY, vw, vh, t) {
    /* yard1: creek + trees/shrubs/rocks/flowers/fence from shared canon */
    var C = global.FroggiesCanon;
    if (!C) return;
    var stream = C.YARD_STREAM || [];
    var halfW = C.YARD_STREAM_HALF_W || 26;
    var phase = (t || 0) * 1.6;

    /* Soft creek ribbon */
    if (stream.length >= 2) {
      var bank = [];
      for (var si = 0; si < stream.length; si++) {
        bank.push(project(stream[si][0], stream[si][1], camX, camY, vw, vh));
      }
      ctx.beginPath();
      for (var bi = 0; bi < bank.length; bi++) {
        if (bi === 0) ctx.moveTo(bank[bi].x, bank[bi].y);
        else ctx.lineTo(bank[bi].x, bank[bi].y);
      }
      ctx.strokeStyle = "rgba(30, 80, 50, 0.55)";
      ctx.lineWidth = halfW * 2.15 * ((bank[0].depth + bank[bank.length - 1].depth) * 0.5);
      ctx.lineCap = "round";
      ctx.lineJoin = "round";
      ctx.stroke();
      ctx.beginPath();
      for (bi = 0; bi < bank.length; bi++) {
        if (bi === 0) ctx.moveTo(bank[bi].x, bank[bi].y);
        else ctx.lineTo(bank[bi].x, bank[bi].y);
      }
      var g = ctx.createLinearGradient(bank[0].x, bank[0].y, bank[bank.length - 1].x, bank[bank.length - 1].y);
      g.addColorStop(0, "rgba(56, 189, 248, 0.72)");
      g.addColorStop(0.5, "rgba(34, 211, 238, 0.78)");
      g.addColorStop(1, "rgba(14, 165, 233, 0.7)");
      ctx.strokeStyle = g;
      ctx.lineWidth = halfW * 1.55 * ((bank[0].depth + bank[bank.length - 1].depth) * 0.5);
      ctx.stroke();
      /* Sparkle glints */
      for (var gi = 0; gi < stream.length - 1; gi++) {
        var a = stream[gi], b = stream[gi + 1];
        var u = (Math.sin(phase + gi * 1.3) * 0.5 + 0.5);
        var gx = a[0] + (b[0] - a[0]) * u;
        var gy = a[1] + (b[1] - a[1]) * u;
        var gp = project(gx, gy, camX, camY, vw, vh);
        ctx.fillStyle = "rgba(224, 242, 254, " + (0.35 + 0.4 * Math.abs(Math.sin(phase * 2 + gi))) + ")";
        ctx.beginPath();
        ctx.arc(gp.x, gp.y - 2 * gp.depth, 2.2 * gp.depth, 0, Math.PI * 2);
        ctx.fill();
      }
    }

    /* Fence posts + rails — mechgun2 skip blown posts */
    var fence = C.YARD_FENCE || [];
    for (var fi = 0; fi < fence.length; fi++) {
      if (C.isPermaGone && C.isPermaGone("yard-fence-" + fi)) continue;
      var fp = project(fence[fi].x, fence[fi].y, camX, camY, vw, vh);
      var fh = 18 * fp.depth;
      ctx.fillStyle = "#78716c";
      ctx.fillRect(fp.x - 2 * fp.depth, fp.y - fh, 4 * fp.depth, fh);
      ctx.fillStyle = "#a8a29e";
      ctx.fillRect(fp.x - 3 * fp.depth, fp.y - fh - 2, 6 * fp.depth, 3 * fp.depth);
      if (fi > 0 && !(C.isPermaGone && C.isPermaGone("yard-fence-" + (fi - 1)))) {
        var prev = project(fence[fi - 1].x, fence[fi - 1].y, camX, camY, vw, vh);
        var sameRow = Math.abs(fence[fi].y - fence[fi - 1].y) < 8 || Math.abs(fence[fi].x - fence[fi - 1].x) < 8;
        if (sameRow && Math.hypot(fence[fi].x - fence[fi - 1].x, fence[fi].y - fence[fi - 1].y) < 120) {
          ctx.strokeStyle = "#57534e";
          ctx.lineWidth = 2;
          ctx.beginPath();
          ctx.moveTo(prev.x, prev.y - fh * 0.7);
          ctx.lineTo(fp.x, fp.y - fh * 0.7);
          ctx.moveTo(prev.x, prev.y - fh * 0.35);
          ctx.lineTo(fp.x, fp.y - fh * 0.35);
          ctx.stroke();
        }
      }
    }

    /* Rocks — mechgun2 skip blown rocks (leave tiny rubble crumb) */
    var rocks = C.YARD_ROCKS || [];
    for (var ri = 0; ri < rocks.length; ri++) {
      var rk = rocks[ri];
      var rp = project(rk.x, rk.y, camX, camY, vw, vh);
      var rr = (rk.r || 8) * rp.depth;
      if (C.isPermaGone && C.isPermaGone("yard-rock-" + ri)) {
        ctx.fillStyle = "rgba(87, 83, 78, 0.55)";
        ctx.beginPath();
        ctx.ellipse(rp.x, rp.y - 2, rr * 0.35, rr * 0.18, 0, 0, Math.PI * 2);
        ctx.fill();
        continue;
      }
      drawSoftShadow(ctx, rp.x, rp.y + 2, rr * 1.1, rr * 0.4, 0.28);
      ctx.fillStyle = ri % 2 ? "#78716c" : "#57534e";
      ctx.beginPath();
      ctx.ellipse(rp.x, rp.y - rr * 0.25, rr, rr * 0.62, -0.2, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = "rgba(255,255,255,0.12)";
      ctx.beginPath();
      ctx.ellipse(rp.x - rr * 0.25, rp.y - rr * 0.4, rr * 0.35, rr * 0.2, 0, 0, Math.PI * 2);
      ctx.fill();
    }

    /* Flowers */
    var flowers = C.YARD_FLOWERS || [];
    for (var fl = 0; fl < flowers.length; fl++) {
      if (C.isPermaGone && C.isPermaGone("yard-flower-" + fl)) continue;
      var flw = flowers[fl];
      var flp = project(flw.x, flw.y, camX, camY, vw, vh);
      var fs = 3.2 * flp.depth;
      ctx.strokeStyle = "#4d7c0f";
      ctx.lineWidth = 1.4;
      ctx.beginPath();
      ctx.moveTo(flp.x, flp.y);
      ctx.lineTo(flp.x, flp.y - fs * 2.2);
      ctx.stroke();
      ctx.fillStyle = flw.c || "#f472b6";
      for (var petal = 0; petal < 5; petal++) {
        var ang = petal * (Math.PI * 2 / 5) + phase * 0.2;
        ctx.beginPath();
        ctx.arc(flp.x + Math.cos(ang) * fs * 0.7, flp.y - fs * 2.2 + Math.sin(ang) * fs * 0.7, fs * 0.55, 0, Math.PI * 2);
        ctx.fill();
      }
      ctx.fillStyle = "#fde68a";
      ctx.beginPath();
      ctx.arc(flp.x, flp.y - fs * 2.2, fs * 0.4, 0, Math.PI * 2);
      ctx.fill();
    }

    /* Shrubs */
    var shrubs = C.YARD_SHRUBS || [];
    for (var sh = 0; sh < shrubs.length; sh++) {
      if (C.isPermaGone && C.isPermaGone("yard-shrub-" + sh)) continue;
      var sb = shrubs[sh];
      var sp = project(sb.x, sb.y, camX, camY, vw, vh);
      var ss = (sb.s || 0.8) * 14 * sp.depth;
      drawSoftShadow(ctx, sp.x, sp.y + 2, ss * 1.1, ss * 0.35, 0.25);
      ctx.fillStyle = sh % 2 ? "#3f6212" : "#4d7c0f";
      ctx.beginPath();
      ctx.ellipse(sp.x, sp.y - ss * 0.35, ss, ss * 0.7, 0, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = "#65a30d";
      ctx.beginPath();
      ctx.ellipse(sp.x - ss * 0.25, sp.y - ss * 0.55, ss * 0.55, ss * 0.45, 0, 0, Math.PI * 2);
      ctx.fill();
    }

    /* Trees — trunk solid via canon; canopy visual; mechgun2 stump rubble when blown */
    var trees = C.YARD_TREES || [];
    for (var ti = 0; ti < trees.length; ti++) {
      var tr = trees[ti];
      var tp = project(tr.x, tr.y, camX, camY, vw, vh);
      var sc = (tr.s || 1) * tp.depth;
      var trunkH = 28 * sc;
      var canopyR = (tr.r || 14) * 1.35 * sc;
      if (C.isPermaGone && C.isPermaGone("yard-tree-" + ti)) {
        drawSoftShadow(ctx, tp.x, tp.y + 2, canopyR * 0.55, canopyR * 0.18, 0.35);
        ctx.fillStyle = "#5c3a1a";
        ctx.fillRect(tp.x - 5 * sc, tp.y - 8 * sc, 10 * sc, 8 * sc);
        ctx.fillStyle = "rgba(120, 53, 15, 0.75)";
        ctx.beginPath();
        ctx.ellipse(tp.x + 10 * sc, tp.y - 4 * sc, 9 * sc, 4 * sc, 0.4, 0, Math.PI * 2);
        ctx.fill();
        ctx.fillStyle = "rgba(34, 197, 94, 0.35)";
        ctx.beginPath();
        ctx.ellipse(tp.x - 8 * sc, tp.y - 2 * sc, 7 * sc, 3 * sc, -0.3, 0, Math.PI * 2);
        ctx.fill();
        continue;
      }
      drawSoftShadow(ctx, tp.x, tp.y + 3, canopyR * 1.1, canopyR * 0.32, 0.3);
      ctx.fillStyle = "#78350f";
      ctx.fillRect(tp.x - 4 * sc, tp.y - trunkH, 8 * sc, trunkH);
      ctx.fillStyle = "#166534";
      ctx.beginPath();
      ctx.ellipse(tp.x, tp.y - trunkH - canopyR * 0.15, canopyR, canopyR * 0.85, 0, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = "#22c55e";
      ctx.beginPath();
      ctx.ellipse(tp.x - canopyR * 0.2, tp.y - trunkH - canopyR * 0.35, canopyR * 0.55, canopyR * 0.5, 0, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = "#15803d";
      ctx.beginPath();
      ctx.ellipse(tp.x + canopyR * 0.25, tp.y - trunkH - canopyR * 0.2, canopyR * 0.5, canopyR * 0.45, 0, 0, Math.PI * 2);
      ctx.fill();
    }
  }


  /* mechgun2: kid-readable rubble pile where a building blew up */
  function drawBuildingRubble(ctx, cx, cy, w, h, camX, camY, vw, vh, label) {
    var p = project(cx, cy, camX, camY, vw, vh);
    var s = Math.max(w, h) * 0.018 * p.depth;
    drawSoftShadow(ctx, p.x, p.y + 4, s * 2.2, s * 0.7, 0.4);
    var chunks = [
      [-0.9, -0.1, 0.7, 0.35, "#78716c"],
      [0.2, -0.25, 0.85, 0.4, "#57534e"],
      [-0.3, 0.15, 0.6, 0.28, "#a8a29e"],
      [0.7, 0.05, 0.45, 0.22, "#44403c"],
      [-0.55, -0.35, 0.4, 0.2, "#9a3412"],
      [0.05, -0.5, 0.5, 0.25, "#6d4c41"],
    ];
    for (var i = 0; i < chunks.length; i++) {
      var c = chunks[i];
      ctx.fillStyle = c[4];
      ctx.beginPath();
      ctx.ellipse(p.x + c[0] * s * 1.4, p.y + c[1] * s - c[3] * s, c[2] * s, c[3] * s, (i % 3) * 0.3, 0, Math.PI * 2);
      ctx.fill();
    }
    /* smoke wisps */
    ctx.fillStyle = "rgba(200, 200, 210, 0.35)";
    for (var sm = 0; sm < 3; sm++) {
      ctx.beginPath();
      ctx.arc(p.x - 10 + sm * 14, p.y - s * 0.9 - sm * 8, 6 + sm * 2, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.font = "bold " + Math.round(12 * p.depth) + "px Segoe UI, system-ui, sans-serif";
    ctx.textAlign = "center";
    ctx.strokeStyle = "rgba(0,0,0,0.7)";
    ctx.lineWidth = 3;
    ctx.strokeText(label || "WRECKED", p.x, p.y - s * 1.15);
    ctx.fillStyle = "#fda4af";
    ctx.fillText(label || "WRECKED", p.x, p.y - s * 1.15);
  }

  function drawRanchHouse(ctx, camX, camY, vw, vh, world, frogs) {
    /* polish5: raise garage door when any frog near bay */
    var garNear = { x: 700 + 240, y: 1400 + 480, r: 220 };
    var wantOpen = 0;
    if (frogs) {
      for (var gi = 0; gi < frogs.length; gi++) {
        var gf = frogs[gi];
        if (Math.hypot(gf.x - (700 + 240), gf.y - (1400 + 460)) < 260) wantOpen = 1;
      }
    }
    if (world) {
      world.garageOpen = clamp((world.garageOpen || 0) + (wantOpen ? 0.08 : -0.04), 0, 1);
      /* note: render-time lerp uses fixed step; updateFx also decays slowly */
      if (wantOpen) world.garageOpen = Math.min(1, (world.garageOpen || 0) + 0.12);
    }

    var a = AREAS[0];
    /* Compound ground — padfix1: stop south of garage before heli/drone pads
       so the translucent orange house-zone floor does not cover H/D craft. */
    var Cpad = global.FroggiesCanon;
    var heliY = (Cpad && Cpad.HELI_PAD && Cpad.HELI_PAD.y) || 2000;
    var droneY = (Cpad && Cpad.DRONE_PAD && Cpad.DRONE_PAD.y) || 2000;
    var padStopY = Math.min(heliY, droneY) - 70;
    var compoundFloor = {
      x: a.x,
      y: a.y,
      w: a.w,
      h: Math.max(200, Math.min(a.h, padStopY - a.y)),
    };
    drawGroundPoly(ctx, areaCorners(compoundFloor, camX, camY, vw, vh), "rgba(100, 70, 40, 0.28)", "rgba(60,30,10,0.3)");

    /* Big backyard (south) */
    var yard = { x: a.x + 40, y: a.y + a.h - 400, w: 600, h: 360 };
    drawGroundPoly(ctx, areaCorners(yard, camX, camY, vw, vh), "rgba(70, 130, 50, 0.55)", "rgba(40,80,30,0.45)");
    var yl = project(yard.x + yard.w * 0.5, yard.y + 20, camX, camY, vw, vh);
    ctx.fillStyle = "rgba(255,255,255,0.75)";
    ctx.font = "bold 12px Segoe UI, system-ui, sans-serif";
    ctx.textAlign = "center";
    ctx.fillText("Backyard", yl.x, yl.y);

    /* Animals in backyard — polish4: legs, heads, bob — denser presence */
    if (world && world.animals) {
      for (var ai = 0; ai < world.animals.length; ai++) {
        var an = world.animals[ai];
        var ap = project(an.x, an.y, camX, camY, vw, vh);
        if (ap.x < -40 || ap.x > vw + 40 || ap.y < -40 || ap.y > vh + 40) continue;
        var asz = 8.5 * an.size * ap.depth;
        if (an.wrecked) {
          if ((an.wreckT || 0) <= 0) continue; /* mech6: hidden until respawn */
          var awa = Math.max(0.12, Math.min(1, (an.wreckT || 0) * 1.1));
          ctx.save();
          ctx.translate(ap.x, ap.y);
          ctx.rotate(an.ang || 0);
          ctx.globalAlpha = awa;
          ctx.fillStyle = an.tone || "#a8a29e";
          ctx.fillRect(-asz, -asz * 0.35, asz * 2, asz * 0.7);
          ctx.fillStyle = "rgba(248, 113, 113, 0.75)";
          ctx.beginPath(); ctx.arc(0, -asz * 0.6, asz * 0.35, 0, Math.PI * 2); ctx.fill();
          ctx.restore();
          continue;
        }
        var bobY = Math.sin(an.bob || 0) * 1.6 * ap.depth;
        var ay = ap.y - bobY;
        drawSoftShadow(ctx, ap.x, ap.y + 3, asz * 1.1, asz * 0.36, 0.32);
        /* Legs */
        ctx.strokeStyle = "rgba(40, 28, 14, 0.85)";
        ctx.lineWidth = Math.max(1.4, asz * 0.18);
        ctx.beginPath();
        ctx.moveTo(ap.x - asz * 0.45, ay); ctx.lineTo(ap.x - asz * 0.5, ap.y + asz * 0.35);
        ctx.moveTo(ap.x - asz * 0.15, ay); ctx.lineTo(ap.x - asz * 0.1, ap.y + asz * 0.38);
        ctx.moveTo(ap.x + asz * 0.15, ay); ctx.lineTo(ap.x + asz * 0.2, ap.y + asz * 0.35);
        ctx.moveTo(ap.x + asz * 0.45, ay); ctx.lineTo(ap.x + asz * 0.5, ap.y + asz * 0.38);
        ctx.stroke();
        ctx.fillStyle = an.tone;
        ctx.strokeStyle = "rgba(40,28,14,0.55)";
        ctx.lineWidth = 1.2;
        ctx.beginPath();
        ctx.ellipse(ap.x, ay - asz * 0.35, asz * 1.05, asz * 0.7, 0, 0, Math.PI * 2);
        ctx.fill(); ctx.stroke();
        /* Head by kind (anonymous shapes only) */
        var hx = ap.x + (an.kind === 2 ? -asz * 0.75 : asz * 0.75);
        var hy = ay - asz * (an.kind === 3 ? 0.85 : 0.55);
        ctx.beginPath();
        if (an.kind === 3) {
          ctx.ellipse(hx, hy, asz * 0.28, asz * 0.38, 0, 0, Math.PI * 2); /* upright / birdy */
        } else {
          ctx.ellipse(hx, hy, asz * 0.42, asz * 0.36, 0, 0, Math.PI * 2);
        }
        ctx.fill(); ctx.stroke();
        if (an.kind === 0) { /* ears */
          ctx.beginPath();
          ctx.moveTo(hx - asz * 0.2, hy - asz * 0.25);
          ctx.lineTo(hx - asz * 0.05, hy - asz * 0.55);
          ctx.lineTo(hx + asz * 0.05, hy - asz * 0.2);
          ctx.fill();
        }
        if (an.kind === 1) { /* spots */
          ctx.fillStyle = "rgba(60,40,20,0.35)";
          ctx.beginPath(); ctx.arc(ap.x - asz * 0.25, ay - asz * 0.4, asz * 0.18, 0, Math.PI * 2); ctx.fill();
          ctx.beginPath(); ctx.arc(ap.x + asz * 0.2, ay - asz * 0.25, asz * 0.14, 0, Math.PI * 2); ctx.fill();
          ctx.fillStyle = an.tone;
        }
        ctx.fillStyle = "#1c1917";
        ctx.beginPath(); ctx.arc(hx + asz * 0.12, hy - asz * 0.05, Math.max(1.2, asz * 0.08), 0, Math.PI * 2); ctx.fill();
      }
    }

    /* Huge garage (east of house) */
    var gar = { x: a.x + 640, y: a.y + 80, w: 480, h: 520 };
    drawGroundPoly(ctx, areaCorners(gar, camX, camY, vw, vh), "rgba(55, 55, 60, 0.55)", "rgba(20,20,25,0.5)");
    var Cgone = global.FroggiesCanon;
    var garageGone = !!(Cgone && Cgone.isPermaGone && Cgone.isPermaGone("garage"));
    if (garageGone) {
      drawBuildingRubble(ctx, gar.x + gar.w * 0.5, gar.y + gar.h * 0.55, gar.w, gar.h, camX, camY, vw, vh, "WRECKED · garage");
    }
    var g0 = project(gar.x + 30, gar.y + 60, camX, camY, vw, vh);
    var g1 = project(gar.x + gar.w - 30, gar.y + 60, camX, camY, vw, vh);
    var g2 = project(gar.x + gar.w - 30, gar.y + gar.h - 40, camX, camY, vw, vh);
    var g3 = project(gar.x + 30, gar.y + gar.h - 40, camX, camY, vw, vh);
    var gH = 72 * ((g0.depth + g2.depth) * 0.5);
    if (!garageGone) {
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
    /* Garage door bay — polish5 opens when frogs near */
    var door = project(gar.x + gar.w * 0.5, gar.y + gar.h - 50, camX, camY, vw, vh);
    /* garage1: door spans nearly full garage front (wall-to-wall) */
    var ddw = Math.max(118, gar.w * 0.88) * door.depth, ddh = 56 * door.depth;
    var openAmt = clamp(world && world.garageOpen ? world.garageOpen : 0, 0, 1);
    var lift = ddh * openAmt * 0.82;
    /* Dark bay always visible */
    ctx.fillStyle = "#0b1220";
    ctx.fillRect(door.x - ddw * 0.5, door.y - ddh, ddw, ddh);
    if (openAmt > 0.15) {
      /* peek mechs / toys glow from inside */
      ctx.fillStyle = "rgba(103, 232, 249, " + (0.18 + openAmt * 0.35) + ")";
      ctx.fillRect(door.x - ddw * 0.42, door.y - ddh * 0.85, ddw * 0.84, ddh * 0.7);
      ctx.fillStyle = "rgba(253, 224, 71, " + (0.15 * openAmt) + ")";
      ctx.beginPath();
      ctx.ellipse(door.x, door.y - ddh * 0.35, ddw * 0.3, 8 * door.depth, 0, 0, Math.PI * 2);
      ctx.fill();
    }
    /* Roll-up door panel rises with openAmt */
    var panelTop = door.y - ddh + lift;
    var panelH = ddh - lift;
    if (panelH > 2) {
      for (var gd = 0; gd < 6; gd++) {
        var gy = panelTop + 3 + gd * (panelH / 6.2);
        if (gy > door.y - 2) break;
        ctx.fillStyle = gd % 2 ? "#374151" : "#1f2937";
        ctx.fillRect(door.x - ddw * 0.46, gy, ddw * 0.92, Math.min(panelH / 7, door.y - gy));
      }
    }
    ctx.strokeStyle = openAmt > 0.4 ? "#86efac" : "#fbbf24";
    ctx.lineWidth = 2.6;
    ctx.strokeRect(door.x - ddw * 0.5, door.y - ddh, ddw, ddh);
    ctx.fillStyle = openAmt > 0.4 ? "#bbf7d0" : "#fde68a";
    ctx.beginPath();
    ctx.arc(door.x + ddw * 0.38, door.y - ddh * 0.55, 3.2 * door.depth, 0, Math.PI * 2);
    ctx.fill();
    if (openAmt > 0.35) {
      ctx.fillStyle = "#bbf7d0";
      ctx.font = "bold " + Math.round(11 * door.depth) + "px system-ui,sans-serif";
      ctx.textAlign = "center";
      ctx.fillText("OPEN", door.x, door.y - ddh - 8 * door.depth);
    }
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
    } /* !garageGone */

    /* 10-story + 100-story mechs reside in garage (hidden while piloted) */
    var Cmech = global.FroggiesCanon;
    var m10 = (Cmech && Cmech.COMPOUND && Cmech.COMPOUND.mech10) || { x: gar.x + 120, y: gar.y + 280 };
    var m100 = (Cmech && Cmech.COMPOUND && Cmech.COMPOUND.mech100) || { x: gar.x + 280, y: gar.y + 300 };
    var p10 = (Cmech && Cmech.vehiclePos) ? Cmech.vehiclePos("mech10", m10.x, m10.y) : m10;
    var p100 = (Cmech && Cmech.vehiclePos) ? Cmech.vehiclePos("mech100", m100.x, m100.y) : m100;
    if (!(Cmech && Cmech.isMechDestroyed && Cmech.isMechDestroyed("mech10")) && !frogPilotsMech(frogs, "mech10")) drawMech(ctx, p10.x, p10.y, 10, camX, camY, vw, vh, "#a5b4fc");
    if (!(Cmech && Cmech.isMechDestroyed && Cmech.isMechDestroyed("mech100")) && !frogPilotsMech(frogs, "mech100")) drawMech(ctx, p100.x, p100.y, 100, camX, camY, vw, vh, "#67e8f9");

    /* Main house — polish8 stronger 2.5D: porch depth layers, path to door, chimney smoke */
    var hx = a.x + 60, hy = a.y + 100, hw = 520, hh = 420;
    var houseGone = !!(Cgone && Cgone.isPermaGone && Cgone.isPermaGone("house"));
    /* Path to door (walkway) — keep even when wrecked so kids see footprint */
    var pathPts = [
      project(hx + hw * 0.42, hy + hh + 40, camX, camY, vw, vh),
      project(hx + hw * 0.58, hy + hh + 40, camX, camY, vw, vh),
      project(hx + hw * 0.55, hy + hh - 25, camX, camY, vw, vh),
      project(hx + hw * 0.45, hy + hh - 25, camX, camY, vw, vh),
    ];
    drawGroundPoly(ctx, pathPts, "rgba(160, 140, 110, 0.72)", "rgba(70,50,30,0.5)");
    if (houseGone) {
      drawBuildingRubble(ctx, hx + hw * 0.5, hy + hh * 0.55, hw, hh, camX, camY, vw, vh, "WRECKED · ranch house");
    }
    if (!houseGone) {
    /* Path edge stones */
    for (var sti = 0; sti < 5; sti++) {
      var stx = hx + hw * 0.48 + (sti - 2) * 8;
      var sty = hy + hh + 10 - sti * 12;
      var stp = project(stx, sty, camX, camY, vw, vh);
      ctx.fillStyle = "rgba(120,100,80,0.75)";
      ctx.beginPath();
      ctx.ellipse(stp.x, stp.y, 5 * stp.depth, 2.5 * stp.depth, 0, 0, Math.PI * 2);
      ctx.fill();
    }
    /* Porch — back deck (depth) then front step */
    var porchBack = [
      project(hx + 70, hy + hh - 110, camX, camY, vw, vh),
      project(hx + hw - 70, hy + hh - 110, camX, camY, vw, vh),
      project(hx + hw - 55, hy + hh - 55, camX, camY, vw, vh),
      project(hx + 55, hy + hh - 55, camX, camY, vw, vh),
    ];
    drawGroundPoly(ctx, porchBack, "rgba(150, 118, 78, 0.88)", "rgba(70,40,15,0.6)");
    var porch = [
      project(hx + 50, hy + hh - 70, camX, camY, vw, vh),
      project(hx + hw - 50, hy + hh - 70, camX, camY, vw, vh),
      project(hx + hw - 30, hy + hh - 8, camX, camY, vw, vh),
      project(hx + 30, hy + hh - 8, camX, camY, vw, vh),
    ];
    drawGroundPoly(ctx, porch, "rgba(185, 155, 105, 0.9)", "rgba(80,50,20,0.55)");
    /* Porch side walls (depth lips) */
    var pl0 = porch[0], pl3 = porch[3];
    ctx.fillStyle = "rgba(110, 80, 50, 0.75)";
    ctx.beginPath();
    ctx.moveTo(pl0.x, pl0.y);
    ctx.lineTo(pl0.x - 10 * pl0.depth, pl0.y - 8 * pl0.depth);
    ctx.lineTo(pl3.x - 10 * pl3.depth, pl3.y - 6 * pl3.depth);
    ctx.lineTo(pl3.x, pl3.y);
    ctx.closePath();
    ctx.fill();
    /* Porch posts + rail */
    for (var pi = 0; pi < 5; pi++) {
      var px = hx + 80 + pi * ((hw - 160) / 4);
      var pp = project(px, hy + hh - 45, camX, camY, vw, vh);
      ctx.fillStyle = "#5d4037";
      ctx.fillRect(pp.x - 3.5, pp.y - 44 * pp.depth, 7, 44 * pp.depth);
      ctx.fillStyle = "#8d6e63";
      ctx.fillRect(pp.x - 4.5, pp.y - 46 * pp.depth, 9, 4 * pp.depth);
    }
    /* Rail connecting posts */
    var railL = project(hx + 80, hy + hh - 50, camX, camY, vw, vh);
    var railR = project(hx + hw - 80, hy + hh - 50, camX, camY, vw, vh);
    ctx.strokeStyle = "#6d4c41";
    ctx.lineWidth = 2.4;
    ctx.beginPath();
    ctx.moveTo(railL.x, railL.y - 28 * railL.depth);
    ctx.lineTo(railR.x, railR.y - 28 * railR.depth);
    ctx.stroke();
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
    function windowAt(wx, wy, ww, wh, roomKind) {
      var wp = project(wx, wy, camX, camY, vw, vh);
      var wx0 = wp.x - ww * 0.5 * wp.depth, wy0 = wp.y - wh * wp.depth - 10;
      var ww0 = ww * wp.depth, wh0 = wh * wp.depth;
      /* Dark room depth then warm lit interior — furniture readable from outside */
      ctx.fillStyle = "rgba(15, 23, 42, 0.7)";
      ctx.fillRect(wx0 - 2, wy0 - 2, ww0 + 4, wh0 + 5);
      var wg2 = ctx.createLinearGradient(wx0, wy0, wx0, wy0 + wh0);
      wg2.addColorStop(0, "rgba(254, 243, 199, 0.95)");
      wg2.addColorStop(0.45, "rgba(253, 230, 138, 0.75)");
      wg2.addColorStop(1, "rgba(180, 120, 60, 0.55)");
      ctx.fillStyle = wg2;
      ctx.fillRect(wx0, wy0, ww0, wh0);
      /* Room props (sofa / table / lamp / bed) */
      ctx.save();
      ctx.beginPath(); ctx.rect(wx0 + 1, wy0 + 1, ww0 - 2, wh0 - 2); ctx.clip();
      if (roomKind === 0) { /* living — sofa */
        ctx.fillStyle = "rgba(120, 70, 50, 0.9)";
        ctx.fillRect(wx0 + ww0 * 0.12, wy0 + wh0 * 0.55, ww0 * 0.76, wh0 * 0.28);
        ctx.fillStyle = "rgba(90, 50, 35, 0.85)";
        ctx.fillRect(wx0 + ww0 * 0.12, wy0 + wh0 * 0.42, ww0 * 0.12, wh0 * 0.4);
        ctx.fillRect(wx0 + ww0 * 0.76, wy0 + wh0 * 0.42, ww0 * 0.12, wh0 * 0.4);
      } else if (roomKind === 1) { /* kitchen — table + chairs */
        ctx.fillStyle = "rgba(90, 60, 30, 0.9)";
        ctx.fillRect(wx0 + ww0 * 0.28, wy0 + wh0 * 0.5, ww0 * 0.44, wh0 * 0.22);
        ctx.fillStyle = "rgba(60, 40, 20, 0.8)";
        ctx.fillRect(wx0 + ww0 * 0.18, wy0 + wh0 * 0.55, ww0 * 0.1, wh0 * 0.28);
        ctx.fillRect(wx0 + ww0 * 0.72, wy0 + wh0 * 0.55, ww0 * 0.1, wh0 * 0.28);
      } else if (roomKind === 2) { /* lamp room */
        ctx.fillStyle = "rgba(251, 191, 36, 0.55)";
        ctx.beginPath(); ctx.arc(wx0 + ww0 * 0.5, wy0 + wh0 * 0.35, ww0 * 0.18, 0, Math.PI * 2); ctx.fill();
        ctx.strokeStyle = "rgba(80,50,20,0.8)"; ctx.lineWidth = 1.2;
        ctx.beginPath(); ctx.moveTo(wx0 + ww0 * 0.5, wy0 + wh0 * 0.5); ctx.lineTo(wx0 + ww0 * 0.5, wy0 + wh0 * 0.85); ctx.stroke();
      } else { /* bed */
        ctx.fillStyle = "rgba(96, 165, 250, 0.75)";
        ctx.fillRect(wx0 + ww0 * 0.15, wy0 + wh0 * 0.48, ww0 * 0.7, wh0 * 0.35);
        ctx.fillStyle = "rgba(248, 250, 252, 0.85)";
        ctx.fillRect(wx0 + ww0 * 0.15, wy0 + wh0 * 0.42, ww0 * 0.28, wh0 * 0.14);
      }
      ctx.restore();
      ctx.strokeStyle = "#1c1210";
      ctx.lineWidth = 2.2;
      ctx.strokeRect(wx0, wy0, ww0, wh0);
      ctx.strokeStyle = "rgba(255,255,255,0.45)";
      ctx.lineWidth = 1.1;
      ctx.beginPath();
      ctx.moveTo(wp.x, wy0);
      ctx.lineTo(wp.x, wy0 + wh0);
      ctx.moveTo(wx0, wy0 + wh0 * 0.5);
      ctx.lineTo(wx0 + ww0, wy0 + wh0 * 0.5);
      ctx.stroke();
      ctx.fillStyle = "#78350f";
      ctx.fillRect(wx0 - 1, wy0 + wh0, ww0 + 2, 3);
    }
    windowAt(hx + 120, hy + 160, 32, 26, 0);
    windowAt(hx + 220, hy + 160, 32, 26, 1);
    windowAt(hx + 320, hy + 160, 32, 26, 2);
    windowAt(hx + 420, hy + 160, 32, 26, 3);
    windowAt(hx + 140, hy + 250, 30, 24, 1);
    windowAt(hx + 280, hy + 250, 30, 24, 0);
    windowAt(hx + 400, hy + 250, 30, 24, 2);
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
    /* Chimney + polish8 smoke puffs */
    var ch = project(hx + hw * 0.72, hy + 70, camX, camY, vw, vh);
    ctx.fillStyle = "#795548";
    ctx.fillRect(ch.x - 8, ch.y - wallH - 70, 16 * ch.depth, 36 * ch.depth);
    ctx.fillStyle = "#5d4037";
    ctx.fillRect(ch.x - 10, ch.y - wallH - 74, 20 * ch.depth, 5 * ch.depth);
    var smokeT = (world && world.ambientT) ? world.ambientT : (Date.now() / 1000);
    for (var sm = 0; sm < 4; sm++) {
      var sy = ch.y - wallH - 78 - sm * 12 - Math.sin(smokeT * 1.4 + sm) * 3;
      var sx = ch.x + Math.sin(smokeT * 0.9 + sm * 0.7) * 6 * ch.depth;
      var sr = (5 + sm * 2.2) * ch.depth;
      ctx.fillStyle = "rgba(200, 200, 205," + (0.42 - sm * 0.08) + ")";
      ctx.beginPath();
      ctx.ellipse(sx, sy, sr, sr * 0.7, 0, 0, Math.PI * 2);
      ctx.fill();
    }
    /* Front door — polish4: ajar doorway shows hallway / rooms inside */
    var fr = project(hx + hw * 0.48, hy + hh - 80, camX, camY, vw, vh);
    var dw = 26 * fr.depth, dh = 52 * fr.depth;
    /* Interior hallway glow through open doorway */
    ctx.fillStyle = "#1c1410";
    ctx.fillRect(fr.x - dw * 0.55, fr.y - dh, dw * 1.1, dh + 2);
    var hall = ctx.createLinearGradient(fr.x, fr.y - dh, fr.x, fr.y);
    hall.addColorStop(0, "rgba(254, 243, 199, 0.95)");
    hall.addColorStop(0.5, "rgba(251, 191, 36, 0.55)");
    hall.addColorStop(1, "rgba(120, 80, 40, 0.7)");
    ctx.fillStyle = hall;
    ctx.fillRect(fr.x - dw * 0.42, fr.y - dh * 0.95, dw * 0.84, dh * 0.95);
    /* Hall runner + far room doorway */
    ctx.fillStyle = "rgba(180, 60, 50, 0.75)";
    ctx.fillRect(fr.x - dw * 0.18, fr.y - dh * 0.15, dw * 0.36, dh * 0.15);
    ctx.fillStyle = "rgba(56, 189, 248, 0.45)";
    ctx.fillRect(fr.x - dw * 0.22, fr.y - dh * 0.78, dw * 0.44, dh * 0.35);
    ctx.fillStyle = "rgba(120, 70, 50, 0.7)";
    ctx.fillRect(fr.x - dw * 0.3, fr.y - dh * 0.45, dw * 0.6, dh * 0.12);
    /* Door leaf swung open */
    ctx.fillStyle = "#5d4037";
    ctx.beginPath();
    ctx.moveTo(fr.x - dw * 0.5, fr.y - dh);
    ctx.lineTo(fr.x - dw * 0.95, fr.y - dh * 0.92);
    ctx.lineTo(fr.x - dw * 0.95, fr.y + 2);
    ctx.lineTo(fr.x - dw * 0.5, fr.y + 2);
    ctx.closePath();
    ctx.fill();
    ctx.strokeStyle = "#fbbf24";
    ctx.lineWidth = 2.2;
    ctx.strokeRect(fr.x - dw * 0.5, fr.y - dh, dw, dh + 2);
    ctx.fillStyle = "#fde68a";
    ctx.beginPath();
    ctx.arc(fr.x - dw * 0.72, fr.y - dh * 0.4, 2.6 * fr.depth, 0, Math.PI * 2);
    ctx.fill();
    /* Porch light */
    ctx.fillStyle = "rgba(253, 224, 71, 0.65)";
    ctx.beginPath();
    ctx.arc(fr.x, fr.y - dh - 6, 7, 0, Math.PI * 2);
    ctx.fill();
    ctx.font = "bold 14px Segoe UI, system-ui, sans-serif";
    ctx.textAlign = "center";
    ctx.strokeStyle = "rgba(0,0,0,0.7)";
    ctx.lineWidth = 3;
    ctx.strokeText("James · Ranch house", ridge.x, ridge.y - ridgeH - 14);
    ctx.fillStyle = "#fff7ed";
    ctx.fillText("James · Ranch house", ridge.x, ridge.y - ridgeH - 14);
    } /* !houseGone */

    /* polish8: Blue Bear place-bound pet bounce near porch / phone */
    var blueX = 320, blueY = 1920;
    var bp = project(blueX, blueY, camX, camY, vw, vh);
    if (!(Cgone && Cgone.isPermaGone && Cgone.isPermaGone("blue-bear")) && bp.x > -40 && bp.x < vw + 40 && bp.y > -40 && bp.y < vh + 40) {
      var bobT = (world && world.ambientT) ? world.ambientT : (Date.now() / 1000);
      var bounce = Math.abs(Math.sin(bobT * 3.2)) * 7 * bp.depth;
      var bs = 11 * bp.depth;
      drawSoftShadow(ctx, bp.x, bp.y + 4, bs * 1.1, bs * 0.35, 0.3);
      ctx.fillStyle = "#3b82f6";
      ctx.beginPath();
      ctx.ellipse(bp.x, bp.y - bounce - bs * 0.35, bs, bs * 0.85, 0, 0, Math.PI * 2);
      ctx.fill();
      ctx.strokeStyle = "#1e3a8a";
      ctx.lineWidth = 2;
      ctx.stroke();
      /* Ears */
      ctx.fillStyle = "#60a5fa";
      ctx.beginPath(); ctx.ellipse(bp.x - bs * 0.55, bp.y - bounce - bs * 0.95, bs * 0.28, bs * 0.32, -0.3, 0, Math.PI * 2); ctx.fill();
      ctx.beginPath(); ctx.ellipse(bp.x + bs * 0.55, bp.y - bounce - bs * 0.95, bs * 0.28, bs * 0.32, 0.3, 0, Math.PI * 2); ctx.fill();
      ctx.fillStyle = "#fff";
      ctx.beginPath(); ctx.arc(bp.x - bs * 0.28, bp.y - bounce - bs * 0.4, 2.2 * bp.depth, 0, Math.PI * 2); ctx.fill();
      ctx.beginPath(); ctx.arc(bp.x + bs * 0.28, bp.y - bounce - bs * 0.4, 2.2 * bp.depth, 0, Math.PI * 2); ctx.fill();
      ctx.fillStyle = "#0f172a";
      ctx.beginPath(); ctx.arc(bp.x - bs * 0.24, bp.y - bounce - bs * 0.4, 1.1 * bp.depth, 0, Math.PI * 2); ctx.fill();
      ctx.beginPath(); ctx.arc(bp.x + bs * 0.32, bp.y - bounce - bs * 0.4, 1.1 * bp.depth, 0, Math.PI * 2); ctx.fill();
      ctx.font = "bold " + Math.round(10 * bp.depth) + "px Segoe UI, system-ui, sans-serif";
      ctx.textAlign = "center";
      ctx.strokeStyle = "rgba(0,0,0,0.65)";
      ctx.lineWidth = 3;
      ctx.strokeText("Blue Bear", bp.x, bp.y - bounce - bs * 1.55);
      ctx.fillStyle = "#bfdbfe";
      ctx.fillText("Blue Bear", bp.x, bp.y - bounce - bs * 1.55);
      ctx.fillStyle = "rgba(147, 197, 253, 0.9)";
      ctx.font = Math.round(8 * bp.depth) + "px Segoe UI, system-ui, sans-serif";
      ctx.fillText("place-bound", bp.x, bp.y + 14 * bp.depth);
    }

    /* 1000-story + trillion-story mechs — outdoors (won't fit garage); park1 uses exit pos */
    var m1000 = (Cmech && Cmech.COMPOUND && Cmech.COMPOUND.mech1000) || { x: a.x + 280, y: a.y + a.h - 80 };
    var mTri = (Cmech && Cmech.COMPOUND && Cmech.COMPOUND.mechTrillion) || { x: 600, y: 2170, stories: 1e12 };
    var p1000 = (Cmech && Cmech.vehiclePos) ? Cmech.vehiclePos("mech1000", m1000.x, m1000.y) : m1000;
    var pTri = (Cmech && Cmech.vehiclePos) ? Cmech.vehiclePos("mechTrillion", mTri.x, mTri.y) : mTri;
    var Cdest = global.FroggiesCanon;
    if (!(Cdest && Cdest.isMechDestroyed && Cdest.isMechDestroyed("mech1000")) && !frogPilotsMech(frogs, "mech1000"))
      drawMech(ctx, p1000.x, p1000.y, 1000, camX, camY, vw, vh, "#fcd34d");
    if (!(Cmech && Cmech.isMechDestroyed && Cmech.isMechDestroyed("mechTrillion")) && !frogPilotsMech(frogs, "mechTrillion")) drawMech(ctx, pTri.x, pTri.y, 1e12, camX, camY, vw, vh, "#f9a8d4");
  }

  function pathPoint(pt, camX, camY, vw, vh) {
    /* qa1: stroke height matches frog/truck lift (z * 0.58), and z is trackElevAt. */
    var canonE = global.FroggiesCanon;
    var elev = (canonE && canonE.trackElevAt)
      ? (canonE.trackElevAt(pt[0], pt[1]) || 0) * 0.58
      : (pt[2] || 0) * 52;
    var p = project(pt[0], pt[1], camX, camY, vw, vh);
    return { x: p.x, y: p.y - elev * p.depth, depth: p.depth, elev: elev, wx: pt[0], wy: pt[1] };
  }

  function deckLift(wx, wy, depth) {
    var c = global.FroggiesCanon;
    if (!(c && c.trackElevAt)) return 0;
    return (c.trackElevAt(wx, wy) || 0) * 0.58 * (depth || 1);
  }

  function densifyTrackPts(pts, step, close) {
    if (!pts || pts.length < 2) return pts;
    var out = [];
    var nSeg = close ? pts.length : pts.length - 1;
    for (var i = 0; i < nSeg; i++) {
      var a = pts[i], b = pts[(i + 1) % pts.length];
      var dist = Math.hypot((b[0] - a[0]), (b[1] - a[1])) || 1;
      var n = Math.max(1, Math.round(dist / (step || 24)));
      for (var s = 0; s < n; s++) {
        var u = s / n;
        out.push([
          a[0] + (b[0] - a[0]) * u,
          a[1] + (b[1] - a[1]) * u,
          (a[2] || 0) + ((b[2] || 0) - (a[2] || 0)) * u,
        ]);
      }
    }
    if (!close) out.push(pts[pts.length - 1]);
    return out;
  }

  function drawPathRibbon(ctx, pts, camX, camY, vw, vh, stroke, width, dash, close) {
    if (!pts || pts.length < 2) return;
    pts = densifyTrackPts(pts, 24, !!close);
    /* Ground contact shadow so elevated hills float clearly */
    if (!dash && width >= 12) {
      ctx.beginPath();
      for (var si = 0; si < pts.length; si++) {
        var gp = project(pts[si][0], pts[si][1], camX, camY, vw, vh);
        if (si === 0) ctx.moveTo(gp.x, gp.y + 3);
        else ctx.lineTo(gp.x, gp.y + 3);
      }
      if (close) ctx.closePath();
      ctx.strokeStyle = "rgba(0,0,0,0.28)";
      ctx.lineWidth = width + 6;
      ctx.lineJoin = "round";
      ctx.lineCap = "round";
      ctx.stroke();
    }
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
    /* Apron fill is the hill. This is the name on the crown. */
    var p = project(m.x, m.y, camX, camY, vw, vh);
    var y = p.y - deckLift(m.x, m.y, p.depth);
    ctx.fillStyle = "rgba(254, 243, 199, 0.92)";
    ctx.font = "bold " + Math.round(11 * p.depth) + "px Segoe UI, system-ui, sans-serif";
    ctx.textAlign = "center";
    ctx.strokeStyle = "#1c1410";
    ctx.lineWidth = 3;
    var label = m.h >= 1 ? "HILL" : (m.h < 0 ? "DIP" : "RISE");
    ctx.strokeText(label, p.x, y - 8 * p.depth);
    ctx.fillText(label, p.x, y - 8 * p.depth);
  }

  function drawTrackSurface(ctx, camX, camY, vw, vh) {
    var a = AREAS[1];
    var c = global.FroggiesCanon;
    if (!(c && c.trackElevAt) || !a) return;
    var nx = Math.max(2, Math.round(a.w / 44));
    var ny = Math.max(2, Math.round(a.h / 44));
    var cells = [];
    for (var iy = 0; iy < ny; iy++) {
      for (var ix = 0; ix < nx; ix++) {
        var x0 = a.x + (ix / nx) * a.w;
        var y0 = a.y + (iy / ny) * a.h;
        var x1 = a.x + ((ix + 1) / nx) * a.w;
        var y1 = a.y + ((iy + 1) / ny) * a.h;
        var corners = [[x0, y0], [x1, y0], [x1, y1], [x0, y1]];
        var proj = [];
        for (var k = 0; k < 4; k++) {
          var pr = project(corners[k][0], corners[k][1], camX, camY, vw, vh);
          var ez = c.trackElevAt(corners[k][0], corners[k][1]) || 0;
          proj.push({ x: pr.x, y: pr.y - ez * 0.58 * pr.depth, ez: ez });
        }
        cells.push({ y: (y0 + y1) * 0.5, proj: proj });
      }
    }
    cells.sort(function (p, q) { return p.y - q.y; });
    function fillTri(p0, p1, p2) {
      var t = (p0.ez + p1.ez + p2.ez) / 540;
      if (t < -0.35) t = -0.35;
      if (t > 1) t = 1;
      var r = Math.round(58 + 120 * t);
      var g = Math.round(46 + 90 * t);
      var b = Math.round(36 + 40 * t);
      if (r < 18) r = 18;
      if (g < 14) g = 14;
      if (b < 10) b = 10;
      ctx.beginPath();
      ctx.moveTo(p0.x, p0.y);
      ctx.lineTo(p1.x, p1.y);
      ctx.lineTo(p2.x, p2.y);
      ctx.closePath();
      ctx.fillStyle = "rgb(" + r + "," + g + "," + b + ")";
      ctx.fill();
    }
    for (var i = 0; i < cells.length; i++) {
      var prj = cells[i].proj;
      fillTri(prj[0], prj[1], prj[2]);
      fillTri(prj[0], prj[2], prj[3]);
    }
  }

  function drawTrack(ctx, camX, camY, vw, vh, t) {
    var a = AREAS[1];
    drawGroundPoly(ctx, areaCorners(a, camX, camY, vw, vh), "rgba(48, 44, 40, 0.9)", "rgba(12,10,8,0.95)");
    var apron = { x: a.x + 36, y: a.y + 36, w: a.w - 72, h: a.h - 72 };
    drawGroundPoly(ctx, areaCorners(apron, camX, camY, vw, vh), "rgba(92, 72, 48, 0.42)", "rgba(30,22,14,0.7)");
    drawTrackSurface(ctx, camX, camY, vw, vh);

    /* Dirt ribbons: outer squiggle circuit, then branches */
    /* polish3: stronger lane contrast — dirt / amber / chalk */
    drawPathRibbon(ctx, TRACK_MAIN, camX, camY, vw, vh, "rgba(12,10,8,0.98)", 26, null, true);
    drawPathRibbon(ctx, TRACK_MAIN, camX, camY, vw, vh, "#92400e", 5.5, [18, 10], true);
    drawPathRibbon(ctx, TRACK_MAIN, camX, camY, vw, vh, "rgba(41,37,36,0.92)", 14, null, true);
    drawPathRibbon(ctx, TRACK_MAIN, camX, camY, vw, vh, "#fafaf9", 2.6, [8, 10], true);

    drawPathRibbon(ctx, TRACK_BRANCH_A, camX, camY, vw, vh, "rgba(20,16,12,0.94)", 16, null, true);
    drawPathRibbon(ctx, TRACK_BRANCH_A, camX, camY, vw, vh, "#f59e0b", 3.2, [12, 8], true);

    drawPathRibbon(ctx, TRACK_BRANCH_B, camX, camY, vw, vh, "rgba(28,22,16,0.92)", 14, null, true);
    drawPathRibbon(ctx, TRACK_BRANCH_B, camX, camY, vw, vh, "#fde68a", 2.6, [9, 9], true);

    for (var mi = 0; mi < TRACK_MOUNDS.length; mi++) {
      drawTrackMound(ctx, TRACK_MOUNDS[mi], camX, camY, vw, vh);
    }

    /* view1: pillars under elevated ribbon — ground to deck */
    (function drawSupports() {
      var list = (global.FroggiesCanon && global.FroggiesCanon.TRACK_SUPPORTS) || [];
      for (var si = 0; si < list.length; si++) {
        var s = list[si];
        var elev = (s.elev != null ? s.elev : 0.5) * 52;
        if (elev < 16) continue;
        var gp = project(s.x, s.y, camX, camY, vw, vh);
        var top = pathPoint([s.x, s.y, s.elev], camX, camY, vw, vh);
        var half = 10 * gp.depth;
        for (var side = -1; side <= 1; side += 2) {
          var bx = gp.x + side * half;
          var by0 = gp.y + 2;
          var by1 = top.y + 2;
          ctx.strokeStyle = "#57534e";
          ctx.lineWidth = Math.max(2.2, 3.4 * gp.depth);
          ctx.beginPath();
          ctx.moveTo(bx, by0);
          ctx.lineTo(bx, by1);
          ctx.stroke();
          ctx.strokeStyle = "rgba(168,162,158,0.85)";
          ctx.lineWidth = Math.max(1.2, 1.8 * gp.depth);
          ctx.beginPath();
          ctx.moveTo(bx - 1, by0);
          ctx.lineTo(bx - 1, by1);
          ctx.stroke();
        }
        /* Crossbeam under deck */
        ctx.strokeStyle = "#78716c";
        ctx.lineWidth = Math.max(2, 2.8 * gp.depth);
        ctx.beginPath();
        ctx.moveTo(gp.x - half, top.y + 1);
        ctx.lineTo(gp.x + half, top.y + 1);
        ctx.stroke();
      }
    })();

    /* polish9: start/finish gate + checkered line */
    (function drawGate() {
      var gx = TRACK_GATE.x, gy = TRACK_GATE.y;
      var gl = project(gx - TRACK_GATE.halfW, gy, camX, camY, vw, vh);
      var gr = project(gx + TRACK_GATE.halfW, gy, camX, camY, vw, vh);
      var gly = gl.y - deckLift(gx - TRACK_GATE.halfW, gy, gl.depth);
      var gry = gr.y - deckLift(gx + TRACK_GATE.halfW, gy, gr.depth);
      var gTop = Math.min(gly, gry);
      var gh = 52 * ((gl.depth + gr.depth) * 0.5);
      /* Posts stand on the deck */
      ctx.fillStyle = "#f8fafc";
      ctx.strokeStyle = "#0f172a";
      ctx.lineWidth = 2;
      ctx.fillRect(gl.x - 4, gly - gh, 8, gh);
      ctx.strokeRect(gl.x - 4, gly - gh, 8, gh);
      ctx.fillRect(gr.x - 4, gry - gh, 8, gh);
      ctx.strokeRect(gr.x - 4, gry - gh, 8, gh);
      /* Banner */
      var mid = project(gx, gy, camX, camY, vw, vh);
      ctx.fillStyle = "rgba(15, 23, 42, 0.9)";
      ctx.fillRect(gl.x, gTop - gh - 4, gr.x - gl.x, 18);
      for (var bi = 0; bi < 8; bi++) {
        ctx.fillStyle = bi % 2 === 0 ? "#0a0a0a" : "#f8fafc";
        var bx0 = gl.x + (gr.x - gl.x) * (bi / 8);
        var bx1 = gl.x + (gr.x - gl.x) * ((bi + 1) / 8);
        ctx.fillRect(bx0, gTop - gh - 4, bx1 - bx0, 18);
      }
      ctx.strokeStyle = "#fbbf24";
      ctx.lineWidth = 2;
      ctx.strokeRect(gl.x, gTop - gh - 4, gr.x - gl.x, 18);
      ctx.fillStyle = "#fef3c7";
      ctx.font = "bold " + Math.round(11 * mid.depth) + "px Segoe UI, system-ui, sans-serif";
      ctx.textAlign = "center";
      ctx.strokeStyle = "#000";
      ctx.lineWidth = 3;
      ctx.strokeText("START / FINISH", mid.x, gTop - gh + 9);
      ctx.fillText("START / FINISH", mid.x, gTop - gh + 9);
    })();
    /* Checkered start on west straight */
    for (var ci = 0; ci < 10; ci++) {
      var sx = 1780 + ci * 18;
      var sy = 2220 + (ci % 2) * 10;
      var sp = project(sx, sy, camX, camY, vw, vh);
      var spy = sp.y - deckLift(sx, sy, sp.depth);
      ctx.fillStyle = ci % 2 === 0 ? "#0a0a0a" : "#f8fafc";
      ctx.strokeStyle = "#111";
      ctx.lineWidth = 1;
      ctx.fillRect(sp.x - 5, spy - 7, 10, 14);
      ctx.strokeRect(sp.x - 5, spy - 7, 10, 14);
    }

    /* track3: banked turn berms */
    (function drawBanks() {
      var banks = (global.FroggiesCanon && global.FroggiesCanon.TRACK_BANKS) || [];
      for (var bi = 0; bi < banks.length; bi++) {
        var bk = banks[bi];
        var bp = project(bk.x, bk.y, camX, camY, vw, vh);
        var by = bp.y - deckLift(bk.x, bk.y, bp.depth);
        ctx.strokeStyle = "#f59e0b";
        ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.arc(bp.x, by, 14 * bp.depth, -0.5, Math.PI * 0.85);
        ctx.stroke();
        ctx.fillStyle = "#fef3c7";
        ctx.strokeStyle = "#1c1410";
        ctx.lineWidth = 3;
        ctx.font = "bold " + Math.round(10 * bp.depth) + "px Segoe UI, system-ui, sans-serif";
        ctx.textAlign = "center";
        ctx.strokeText(bk.label || "BANK", bp.x, by - 8 * bp.depth);
        ctx.fillText(bk.label || "BANK", bp.x, by - 8 * bp.depth);
      }
    })();

    /* track3: big rock obstacles */
    (function drawRocks() {
      var rocks = (global.FroggiesCanon && global.FroggiesCanon.TRACK_ROCKS) || [];
      for (var ri = 0; ri < rocks.length; ri++) {
        var rk = rocks[ri];
        if (global.FroggiesCanon && global.FroggiesCanon.isPermaGone && global.FroggiesCanon.isPermaGone("track-rock-" + ri)) continue;
        var rp = project(rk.x, rk.y, camX, camY, vw, vh);
        var rr = rk.r * 0.42 * rp.depth;
        var rh = (rk.h || 1) * 22 * rp.depth;
        var deckY = rp.y - deckLift(rk.x, rk.y, rp.depth);
        var by = deckY + rh * 0.45;
        drawSoftShadow(ctx, rp.x, deckY + 4 * rp.depth, rr * 1.1, rr * 0.35, 0.4);
        var rg = ctx.createRadialGradient(rp.x - rr * 0.25, by - rh * 0.6, rr * 0.1, rp.x, by - rh * 0.2, rr * 1.2);
        rg.addColorStop(0, "#a8a29e");
        rg.addColorStop(0.45, "#57534e");
        rg.addColorStop(1, "#1c1917");
        ctx.fillStyle = rg;
        ctx.beginPath();
        ctx.moveTo(rp.x - rr * 0.95, by);
        ctx.quadraticCurveTo(rp.x - rr * 1.05, by - rh * 0.55, rp.x - rr * 0.35, by - rh);
        ctx.quadraticCurveTo(rp.x + rr * 0.15, by - rh * 1.15, rp.x + rr * 0.55, by - rh * 0.7);
        ctx.quadraticCurveTo(rp.x + rr * 1.05, by - rh * 0.25, rp.x + rr * 0.9, by);
        ctx.closePath();
        ctx.fill();
        ctx.strokeStyle = "#0c0a09";
        ctx.lineWidth = 2.4;
        ctx.stroke();
        ctx.strokeStyle = "rgba(214, 211, 209, 0.45)";
        ctx.lineWidth = 1.5;
        ctx.beginPath();
        ctx.moveTo(rp.x - rr * 0.3, by - rh * 0.2);
        ctx.lineTo(rp.x + rr * 0.1, by - rh * 0.75);
        ctx.lineTo(rp.x + rr * 0.45, by - rh * 0.35);
        ctx.stroke();
        ctx.fillStyle = "#fef3c7";
        ctx.font = "bold " + Math.round(9 * rp.depth) + "px Segoe UI, system-ui, sans-serif";
        ctx.textAlign = "center";
        ctx.fillText("ROCK", rp.x, by - rh - 6 * rp.depth);
      }
    })();

    /* Ramps — crisper wedge + edge */
    for (var ri = 0; ri < RAMPS.length; ri++) {
      var r = RAMPS[ri];
      var rp0 = project(r.x - r.w * 0.5, r.y, camX, camY, vw, vh);
      var rp1 = project(r.x + r.w * 0.5, r.y, camX, camY, vw, vh);
      var rp2 = project(r.x, r.y - r.h * 0.4, camX, camY, vw, vh);
      rp0 = { x: rp0.x, y: rp0.y - deckLift(r.x - r.w * 0.5, r.y, rp0.depth), depth: rp0.depth };
      rp1 = { x: rp1.x, y: rp1.y - deckLift(r.x + r.w * 0.5, r.y, rp1.depth), depth: rp1.depth };
      rp2 = { x: rp2.x, y: rp2.y - deckLift(r.x, r.y - r.h * 0.4, rp2.depth), depth: rp2.depth };
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
    var labelY = label.y - deckLift(a.x + a.w * 0.5, a.y + 40, label.depth) - 8;
    ctx.fillStyle = "#fffbeb";
    ctx.strokeStyle = "rgba(0,0,0,0.65)";
    ctx.lineWidth = 3;
    ctx.font = "bold 14px Segoe UI, system-ui, sans-serif";
    ctx.textAlign = "center";
    ctx.strokeText("Monster truck track · banks · rocks", label.x, labelY);
    ctx.fillText("Monster truck track · banks · rocks", label.x, labelY);
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
    ctx.strokeStyle = "rgba(240, 252, 255, 0.92)";
    ctx.lineWidth = 4.2;
    ctx.stroke();
    ctx.strokeStyle = "rgba(4, 28, 48, 0.95)";
    ctx.lineWidth = 1.8;
    ctx.stroke();
    /* Inner sparkle ring */
    ctx.strokeStyle = "rgba(125, 211, 252, 0.35)";
    ctx.lineWidth = 2;
    ctx.setLineDash([8, 6]);
    ctx.stroke();
    ctx.setLineDash([]);
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
      /* polish8: whale breach arcs; fish stay school-readable */
      var breachLift = 0;
      if (isWhale) {
        var br = Math.sin(f.breach || 0);
        breachLift = Math.max(0, br) * (f.breachAmp || 48) * 0.55;
      }
      var fp = project(f.x, f.y, camX, camY, vw, vh);
      if (fp.x < -80 || fp.x > vw + 80 || fp.y < -80 || fp.y > vh + 80) return;
      var fs = (isWhale ? 13 : 6) + f.size * (isWhale ? 2.7 : 2.6);
      fs *= fp.depth * (f.scare > 0 ? 1.14 : 1);
      var ang = f.phase;
      var drawY = fp.y - breachLift * fp.depth;
      /* Breach splash ring when rising */
      if (isWhale && breachLift > 8) {
        var ba = Math.min(0.55, breachLift / 50);
        ctx.strokeStyle = "rgba(186, 230, 253," + ba + ")";
        ctx.lineWidth = 2.4;
        ctx.beginPath();
        ctx.ellipse(fp.x, fp.y + 2, fs * (1.1 + breachLift * 0.02), fs * 0.35, 0, 0, Math.PI * 2);
        ctx.stroke();
        /* Arc trail */
        ctx.strokeStyle = "rgba(125, 211, 252," + (0.25 + ba * 0.4) + ")";
        ctx.lineWidth = 2;
        ctx.beginPath();
        ctx.moveTo(fp.x - fs * 0.9, fp.y + 4);
        ctx.quadraticCurveTo(fp.x - fs * 0.2, drawY - fs * 0.4, fp.x + fs * 0.3, drawY);
        ctx.stroke();
      }
      ctx.fillStyle = "rgba(0,0,0,0.28)";
      ctx.beginPath();
      ctx.ellipse(fp.x, fp.y + 4, fs * 1.05, fs * 0.26, ang, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = f.scare > 0 ? "#fef08a" : (isWhale ? "#7dd3fc" : "#fde68a");
      ctx.beginPath();
      ctx.ellipse(fp.x, drawY, fs * (isWhale ? 1.2 : 1), fs * (isWhale ? 0.44 : 0.48), ang, 0, Math.PI * 2);
      ctx.fill();
      ctx.strokeStyle = isWhale ? "#0c4a6e" : "#78350f";
      ctx.lineWidth = isWhale ? 2.6 : 1.8;
      ctx.stroke();
      /* Tail */
      var tx = fp.x - Math.cos(ang) * fs;
      var ty = drawY - Math.sin(ang) * fs * 0.35;
      ctx.beginPath();
      ctx.moveTo(tx, ty);
      ctx.lineTo(fp.x - Math.cos(ang) * fs * 1.7, drawY);
      ctx.lineTo(fp.x - Math.cos(ang) * fs, drawY + Math.sin(ang) * fs * 0.4);
      ctx.closePath();
      ctx.fillStyle = isWhale ? "#38bdf8" : "#fbbf24";
      ctx.fill();
      ctx.strokeStyle = isWhale ? "#075985" : "#78350f";
      ctx.lineWidth = 1.4;
      ctx.stroke();
      /* Eye */
      var ex = fp.x + Math.cos(ang) * fs * 0.45;
      var ey = drawY + Math.sin(ang) * fs * 0.15 - fs * 0.12;
      ctx.fillStyle = "#fff";
      ctx.beginPath();
      ctx.arc(ex, ey, (isWhale ? 3.4 : 2.1) * fp.depth, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = "#0f172a";
      ctx.beginPath();
      ctx.arc(ex + 0.6 * fp.depth, ey, (isWhale ? 1.7 : 1.05) * fp.depth, 0, Math.PI * 2);
      ctx.fill();
      if (isWhale) {
        ctx.fillStyle = "rgba(255,255,255,0.55)";
        ctx.beginPath();
        ctx.ellipse(fp.x + fs * 0.1, drawY - fs * 0.18, fs * 0.4, fs * 0.14, ang, 0, Math.PI * 2);
        ctx.fill();
        /* Spout taller on breach */
        var spoutH = fs * (0.95 + Math.min(0.8, breachLift / 40));
        ctx.strokeStyle = "rgba(186, 230, 253, 0.9)";
        ctx.lineWidth = 2.4;
        ctx.beginPath();
        ctx.moveTo(fp.x, drawY - fs * 0.35);
        ctx.lineTo(fp.x + 2, drawY - spoutH);
        ctx.lineTo(fp.x + 10 * fp.depth, drawY - spoutH - 4);
        ctx.stroke();
        ctx.fillStyle = "rgba(224, 242, 254, 0.75)";
        ctx.beginPath();
        ctx.arc(fp.x + 10 * fp.depth, drawY - spoutH - 4, 4.2 * fp.depth, 0, Math.PI * 2);
        ctx.fill();
      } else {
        /* Dorsal fin */
        ctx.fillStyle = "#f59e0b";
        ctx.beginPath();
        ctx.moveTo(fp.x - fs * 0.1, drawY - fs * 0.35);
        ctx.lineTo(fp.x + fs * 0.15, drawY - fs * 0.85);
        ctx.lineTo(fp.x + fs * 0.25, drawY - fs * 0.2);
        ctx.closePath();
        ctx.fill();
      }
    }
    for (var i = 0; i < world.fish.length; i++) {
      if (world.fish[i] && world.fish[i].goneForever) continue;
      drawSwimmer(world.fish[i], false);
    }
    for (var wj = 0; wj < world.whales.length; wj++) {
      if (world.whales[wj] && world.whales[wj].goneForever) continue;
      drawSwimmer(world.whales[wj], true);
    }
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
      if (toy.wrecked) {
        if ((toy.wreckT || 0) <= 0) continue; /* mech6: hidden until respawn */
        var wa = clamp((toy.wreckT || 0) * 1.2, 0.15, 1);
        ctx.save();
        ctx.translate(p.x, p.y);
        ctx.rotate(toy.ang || 0);
        ctx.fillStyle = "rgba(120, 53, 15, " + (wa * 0.85) + ")";
        ctx.fillRect(-s, -s * 0.4, s * 2, s * 0.8);
        ctx.fillStyle = "rgba(251, 146, 60, " + (wa * 0.7) + ")";
        ctx.fillRect(-s * 0.5, -s * 1.1, s, s * 0.5);
        ctx.restore();
        continue;
      }
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

  /* mech1: Howe&Howe-style Ripsaw — low tracked wedge + cage (not a wheeled truck) */
  function drawRipsaw(ctx, x, y, faceAngle, depth, driving, z, accent, water) {
    var s = 0.92 * depth;
    var lift = (z || 0) * 0.72 * depth + ((water && water.bounce) ? water.bounce * depth * 0.4 : 0);
    var drawY = y - lift;
    ctx.save();
    ctx.translate(x, drawY);
    var ang = (faceAngle != null && isFinite(faceAngle)) ? faceAngle : 0;
    ctx.rotate(ang);
    /* ground shadow */
    ctx.fillStyle = "rgba(15,23,42,0.35)";
    ctx.beginPath();
    ctx.ellipse(0, 11 * s, 36 * s, 9 * s, 0, 0, Math.PI * 2);
    ctx.fill();
    /* continuous tracks (left/right) with pad nubs */
    function track(side) {
      var zy = side * 16 * s;
      ctx.fillStyle = "#1e293b";
      ctx.fillRect(-28 * s, zy - 5 * s, 56 * s, 10 * s);
      ctx.strokeStyle = "#0f172a";
      ctx.lineWidth = 1.4;
      ctx.strokeRect(-28 * s, zy - 5 * s, 56 * s, 10 * s);
      ctx.fillStyle = "#334155";
      for (var i = 0; i < 8; i++) {
        var px = -24 * s + i * 7 * s;
        ctx.fillRect(px, zy - 6.5 * s, 4.2 * s, 13 * s);
      }
      /* sprocket hubs */
      ctx.fillStyle = "#64748b";
      ctx.beginPath(); ctx.arc(-22 * s, zy, 4.5 * s, 0, Math.PI * 2); ctx.fill();
      ctx.beginPath(); ctx.arc(22 * s, zy, 4.5 * s, 0, Math.PI * 2); ctx.fill();
    }
    track(-1); track(1);
    /* low armored hull / wedge nose */
    ctx.fillStyle = accent || "#78716c";
    ctx.beginPath();
    ctx.moveTo(30 * s, 0);
    ctx.lineTo(14 * s, -10 * s);
    ctx.lineTo(-22 * s, -9 * s);
    ctx.lineTo(-26 * s, 0);
    ctx.lineTo(-22 * s, 9 * s);
    ctx.lineTo(14 * s, 10 * s);
    ctx.closePath();
    ctx.fill();
    ctx.strokeStyle = "#0f172a";
    ctx.lineWidth = 1.8;
    ctx.stroke();
    /* deck plate */
    ctx.fillStyle = "#57534e";
    ctx.fillRect(-18 * s, -6 * s, 28 * s, 12 * s);
    /* roll cage */
    ctx.strokeStyle = "#a8a29e";
    ctx.lineWidth = 2.2;
    ctx.strokeRect(-10 * s, -14 * s, 16 * s, 10 * s);
    ctx.beginPath();
    ctx.moveTo(-10 * s, -14 * s); ctx.lineTo(-6 * s, -20 * s); ctx.lineTo(6 * s, -20 * s); ctx.lineTo(6 * s, -14 * s);
    ctx.stroke();
    /* light bar */
    ctx.fillStyle = "#fde68a";
    ctx.fillRect(12 * s, -4 * s, 4 * s, 8 * s);
    if (driving) {
      ctx.fillStyle = "#fef3c7";
      ctx.font = "bold " + Math.round(8 * depth) + "px system-ui,sans-serif";
      ctx.textAlign = "center";
      ctx.fillText("RIPSAW", 0, 22 * s);
    }
    ctx.restore();
  }

  /* mech1: classic tank — hull + continuous tracks + turret/barrel */
  function drawTank(ctx, x, y, faceAngle, depth, driving, z, accent, water) {
    var s = 0.9 * depth;
    var lift = (z || 0) * 0.72 * depth + ((water && water.bounce) ? water.bounce * depth * 0.35 : 0);
    var drawY = y - lift;
    ctx.save();
    ctx.translate(x, drawY);
    var ang = (faceAngle != null && isFinite(faceAngle)) ? faceAngle : 0;
    ctx.rotate(ang);
    ctx.fillStyle = "rgba(15,23,42,0.35)";
    ctx.beginPath();
    ctx.ellipse(0, 12 * s, 34 * s, 10 * s, 0, 0, Math.PI * 2);
    ctx.fill();
    function track(side) {
      var zy = side * 15 * s;
      ctx.fillStyle = "#111827";
      ctx.fillRect(-26 * s, zy - 5.5 * s, 52 * s, 11 * s);
      ctx.fillStyle = "#374151";
      for (var i = 0; i < 7; i++) {
        ctx.fillRect(-22 * s + i * 7 * s, zy - 7 * s, 4 * s, 14 * s);
      }
      ctx.fillStyle = "#6b7280";
      ctx.beginPath(); ctx.arc(-20 * s, zy, 4 * s, 0, Math.PI * 2); ctx.fill();
      ctx.beginPath(); ctx.arc(18 * s, zy, 4 * s, 0, Math.PI * 2); ctx.fill();
    }
    track(-1); track(1);
    /* hull */
    ctx.fillStyle = accent || "#4b5563";
    ctx.fillRect(-22 * s, -8 * s, 40 * s, 16 * s);
    ctx.strokeStyle = "#0f172a";
    ctx.lineWidth = 1.8;
    ctx.strokeRect(-22 * s, -8 * s, 40 * s, 16 * s);
    /* glacis wedge */
    ctx.fillStyle = "#6b7280";
    ctx.beginPath();
    ctx.moveTo(18 * s, -8 * s);
    ctx.lineTo(28 * s, 0);
    ctx.lineTo(18 * s, 8 * s);
    ctx.closePath();
    ctx.fill();
    ctx.stroke();
    /* turret */
    ctx.fillStyle = "#374151";
    ctx.beginPath();
    ctx.ellipse(0, -2 * s, 12 * s, 9 * s, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();
    /* barrel */
    ctx.fillStyle = "#1f2937";
    ctx.fillRect(10 * s, -4 * s, 26 * s, 4.5 * s);
    ctx.strokeRect(10 * s, -4 * s, 26 * s, 4.5 * s);
    ctx.fillStyle = "#9ca3af";
    ctx.fillRect(34 * s, -4.5 * s, 4 * s, 5.5 * s);
    if (driving) {
      ctx.fillStyle = "#e5e7eb";
      ctx.font = "bold " + Math.round(8 * depth) + "px system-ui,sans-serif";
      ctx.textAlign = "center";
      ctx.fillText("TANK", 0, 24 * s);
    }
    ctx.restore();
  }

  function drawDriveVehicle(ctx, style, x, y, faceAngle, depth, driving, z, accent, water) {
    var st = style || "cybertruck";
    if (st === "ripsaw") return drawRipsaw(ctx, x, y, faceAngle, depth, driving, z, accent || "#a8a29e", water);
    if (st === "tank") return drawTank(ctx, x, y, faceAngle, depth, driving, z, accent || "#6b7280", water);
    if (st === "submarine") return drawSubmarine(ctx, x, y, faceAngle, depth, driving, z, accent || "#0ea5e9", water);
    return drawCybertruck(ctx, x, y, faceAngle, depth, driving, z, accent, water);
  }

  function drawSubmarine(ctx, x, y, faceAngle, depth, driving, z, accent, water) {
    var sc = 0.92 * depth;
    var sub = water && water.sub != null ? water.sub : (driving ? 0.9 : 0.15);
    var dive = Math.min(1.15, Math.max(0.2, sub));
    var lift = (z || 0) * 0.4 * depth;
    var drawY = y - lift + dive * 10 * depth;
    var ang = (faceAngle != null && isFinite(faceAngle)) ? faceAngle : 0;
    ctx.save();
    ctx.translate(x, drawY);
    ctx.rotate(ang);
    if (driving) {
      var wp = (water && water.wakePhase) || 0;
      for (var b = 0; b < 4; b++) {
        ctx.fillStyle = "rgba(186,230,253," + (0.35 - b * 0.06) + ")";
        ctx.beginPath();
        ctx.arc(-28 * sc - b * 8 * sc, (Math.sin(wp + b) * 4) * sc, (3.5 - b * 0.4) * sc, 0, Math.PI * 2);
        ctx.fill();
      }
    }
    drawSoftShadow(ctx, 0, 8 * sc, 40 * sc, 10 * sc, 0.22);
    var hull = ctx.createLinearGradient(-40 * sc, -10 * sc, 42 * sc, 12 * sc);
    hull.addColorStop(0, driving ? "#38bdf8" : "#0284c7");
    hull.addColorStop(0.45, driving ? "#0ea5e9" : "#0369a1");
    hull.addColorStop(1, "#0c4a6e");
    ctx.fillStyle = hull;
    ctx.beginPath();
    ctx.ellipse(0, 0, 42 * sc, 14 * sc, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.strokeStyle = "#082f49";
    ctx.lineWidth = 2.4;
    ctx.stroke();
    ctx.fillStyle = accent || "#7dd3fc";
    ctx.fillRect(-6 * sc, -28 * sc, 14 * sc, 18 * sc);
    ctx.strokeStyle = "#0c4a6e";
    ctx.lineWidth = 1.6;
    ctx.strokeRect(-6 * sc, -28 * sc, 14 * sc, 18 * sc);
    ctx.fillStyle = "#94a3b8";
    ctx.fillRect(2 * sc, -40 * sc, 3.2 * sc, 14 * sc);
    ctx.fillRect(2 * sc, -42 * sc, 10 * sc, 3 * sc);
    ctx.fillStyle = "rgba(224,242,254,0.85)";
    ctx.beginPath(); ctx.arc(-14 * sc, -2 * sc, 4 * sc, 0, Math.PI * 2); ctx.fill();
    ctx.beginPath(); ctx.arc(6 * sc, -2 * sc, 4 * sc, 0, Math.PI * 2); ctx.fill();
    ctx.beginPath(); ctx.arc(24 * sc, -1 * sc, 3.2 * sc, 0, Math.PI * 2); ctx.fill();
    ctx.strokeStyle = "#cbd5e1";
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.moveTo(-42 * sc, 0);
    ctx.lineTo(-52 * sc, -6 * sc);
    ctx.moveTo(-42 * sc, 0);
    ctx.lineTo(-52 * sc, 6 * sc);
    ctx.stroke();
    if (driving) {
      ctx.fillStyle = "rgba(15,23,42,0.75)";
      ctx.fillRect(-36 * sc, 16 * sc, 72 * sc, 14 * sc);
      ctx.fillStyle = "#e0f2fe";
      ctx.font = "bold " + Math.round(9 * depth) + "px Segoe UI, system-ui, sans-serif";
      ctx.textAlign = "center";
      ctx.fillText("UNDERWATER", 0, 26 * sc);
    }
    ctx.restore();
  }

  function drawCybertruck(ctx, x, y, faceAngle, depth, driving, z, accent, water) {
    /* truck1: kid-toy scale vs frog (~16px radius) — bigger than frog, not a building */
    var vis = (global.FroggiesCanon && global.FroggiesCanon.TRUCK_VIS) || {};
    var s = (vis.canvasScale != null ? vis.canvasScale : 0.86) * depth;
    var bounce = (water && water.bounce) ? water.bounce : 0;
    var wsEarly = 1;
    if (water && water.wheelScale != null) wsEarly = water.wheelScale;
    else if (global.FroggiesCanon && global.FroggiesCanon.getWheelScale) wsEarly = global.FroggiesCanon.getWheelScale();
    if (wsEarly < 1) wsEarly = 1;
    if (wsEarly > 2.8) wsEarly = 2.8;
    /* truck2: taller elev draw so hills/jumps read clearly; track3: wheel clearance */
    var lift = (z || 0) * 0.72 * depth + (driving ? bounce * depth * 0.55 : 0) + (wsEarly - 1) * 7 * depth;
    var wet = water && water.inWater;
    var sub = wet ? clamp(water.sub || 0, 0, 1.2) : 0;
    /* Surface drive sits ON the water — tiny lift; dive sinks visually */
    var waterSit = wet && (z || 0) < 5 ? (2.5 - sub * 10) * depth : 0;
    var drawY = y - lift + (wet ? Math.max(-2, -waterSit * 0.15) : 0);
    /* When diving (high sub), truck sits lower through the waterline */
    if (wet && sub > 0.5) drawY += (sub - 0.5) * 14 * depth;

    ctx.save();
    ctx.translate(x, drawY);
    /* truck1: yaw to faceAngle (nose = local +X); was facing flip only */
    var ang = (faceAngle != null && isFinite(faceAngle)) ? faceAngle : 0;
    ctx.rotate(ang);

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

    /* polish6: soft layered drop shadow under Cybertruck */
    var shA = wet ? 0.14 : (0.38 - Math.min(0.22, (z || 0) * 0.005));
    drawSoftShadow(ctx, 0, 10 * s + lift * 0.35, 34 * s, 9 * s, shA);

    /* polish8: more Cybertruck-like — angular stainless, light bar, wheel arches */
    var bodyGrad = ctx.createLinearGradient(-40 * s, -26 * s, 48 * s, 14 * s);
    bodyGrad.addColorStop(0, driving ? "#f8fafc" : "#e8edf2");
    bodyGrad.addColorStop(0.28, driving ? "#d4dce6" : "#b8c0cc");
    bodyGrad.addColorStop(0.55, driving ? "#9aa7b8" : "#8b95a3");
    bodyGrad.addColorStop(1, driving ? "#5b6778" : "#4a5564");
    ctx.fillStyle = bodyGrad;
    ctx.beginPath();
    ctx.moveTo(-38 * s, 4 * s);        /* rear bumper sharp */
    ctx.lineTo(-36 * s, -4 * s);       /* bed rear upright */
    ctx.lineTo(-14 * s, -8 * s);       /* bed flat */
    ctx.lineTo(-6 * s, -12 * s);       /* cab break */
    ctx.lineTo(4 * s, -26 * s);        /* roof peak (steep) */
    ctx.lineTo(40 * s, -8 * s);        /* long nose slope */
    ctx.lineTo(48 * s, 2 * s);         /* front tip */
    ctx.lineTo(44 * s, 8 * s);         /* front bumper */
    ctx.lineTo(-40 * s, 10 * s);
    ctx.closePath();
    ctx.fill();
    ctx.strokeStyle = "#020617";
    ctx.lineWidth = 3;
    ctx.lineJoin = "miter";
    ctx.stroke();
    /* Stainless panel creases */
    ctx.strokeStyle = "rgba(255,255,255,0.72)";
    ctx.lineWidth = 1.6;
    ctx.beginPath();
    ctx.moveTo(-34 * s, -2 * s);
    ctx.lineTo(-12 * s, -6 * s);
    ctx.lineTo(-2 * s, -10 * s);
    ctx.lineTo(6 * s, -22 * s);
    ctx.lineTo(38 * s, -6 * s);
    ctx.stroke();
    ctx.strokeStyle = "rgba(15,23,42,0.55)";
    ctx.lineWidth = 1.4;
    ctx.beginPath();
    ctx.moveTo(-36 * s, 6 * s);
    ctx.lineTo(42 * s, 4 * s);
    ctx.stroke();
    /* Side mirror */
    ctx.fillStyle = "#94a3b8";
    ctx.fillRect(-6 * s, -16 * s, 6 * s, 3.5 * s);
    ctx.strokeStyle = "#0f172a";
    ctx.lineWidth = 1;
    ctx.strokeRect(-6 * s, -16 * s, 6 * s, 3.5 * s);
    if (accent) {
      ctx.strokeStyle = accent;
      ctx.lineWidth = 3.8;
      ctx.beginPath();
      ctx.moveTo(-34 * s, 3 * s);
      ctx.lineTo(38 * s, 1 * s);
      ctx.stroke();
    }
    /* Cabin glass — steep wedge */
    ctx.fillStyle = "rgba(15, 23, 42, 0.97)";
    ctx.beginPath();
    ctx.moveTo(-4 * s, -10 * s);
    ctx.lineTo(6 * s, -24 * s);
    ctx.lineTo(30 * s, -9 * s);
    ctx.lineTo(2 * s, -6 * s);
    ctx.closePath();
    ctx.fill();
    ctx.strokeStyle = "#020617";
    ctx.lineWidth = 1.8;
    ctx.stroke();
    ctx.fillStyle = "rgba(125, 211, 252, 0.45)";
    ctx.beginPath();
    ctx.moveTo(-1 * s, -11 * s);
    ctx.lineTo(8 * s, -20 * s);
    ctx.lineTo(24 * s, -10 * s);
    ctx.lineTo(4 * s, -8 * s);
    ctx.closePath();
    ctx.fill();
    /* Full-width light bar across nose */
    var lbY = -7 * s;
    var lbG = ctx.createLinearGradient(32 * s, lbY, 48 * s, lbY + 5 * s);
    lbG.addColorStop(0, driving ? "#fef9c3" : "#e2e8f0");
    lbG.addColorStop(0.5, driving ? "#fde047" : "#cbd5e1");
    lbG.addColorStop(1, driving ? "#fef08a" : "#94a3b8");
    ctx.fillStyle = lbG;
    ctx.fillRect(34 * s, lbY, 13 * s, 5 * s);
    ctx.strokeStyle = "#0f172a";
    ctx.lineWidth = 1.2;
    ctx.strokeRect(34 * s, lbY, 13 * s, 5 * s);
    if (driving) {
      ctx.fillStyle = "rgba(253, 224, 71, 0.35)";
      ctx.beginPath();
      ctx.ellipse(42 * s, lbY + 2.5 * s, 18 * s, 6 * s, 0, 0, Math.PI * 2);
      ctx.fill();
    }
    /* Tail light bar */
    ctx.fillStyle = driving ? "#f87171" : "#7f1d1d";
    ctx.fillRect(-38 * s, -1 * s, 7 * s, 4 * s);
    /* track3: live monster-truck wheel scale (body already lifted via drawY) */
    var ws = wsEarly;

    function wheel(wx, wy, r) {
      r = (r || 8.5) * ws;
      /* Wheel arch flare — grows with monster wheels */
      ctx.strokeStyle = "rgba(148,163,184,0.85)";
      ctx.lineWidth = 2.2 + (ws - 1) * 1.2;
      ctx.beginPath();
      ctx.arc(wx, wy - 1 * s, (r + 3.2 * ws) * s, Math.PI * 1.05, Math.PI * 1.95);
      ctx.stroke();
      ctx.fillStyle = "#020617";
      ctx.beginPath(); ctx.arc(wx, wy, r * s, 0, Math.PI * 2); ctx.fill();
      ctx.strokeStyle = ws > 1.4 ? "#64748b" : "#94a3b8";
      ctx.lineWidth = 2.4 + (ws - 1) * 0.8;
      ctx.beginPath(); ctx.arc(wx, wy, Math.max(2, r - 2.6 * ws) * s, 0, Math.PI * 2); ctx.stroke();
      ctx.fillStyle = "#334155";
      ctx.beginPath(); ctx.arc(wx, wy, Math.max(1.2, r - 5 * ws) * s, 0, Math.PI * 2); ctx.fill();
      /* Monster tread nubs */
      if (ws > 1.35) {
        ctx.strokeStyle = "rgba(148,163,184,0.55)";
        ctx.lineWidth = 1.5;
        for (var ti = 0; ti < 6; ti++) {
          var ta = (ti / 6) * Math.PI * 2;
          ctx.beginPath();
          ctx.moveTo(wx + Math.cos(ta) * (r - 1.2) * s, wy + Math.sin(ta) * (r - 1.2) * s);
          ctx.lineTo(wx + Math.cos(ta) * r * s, wy + Math.sin(ta) * r * s);
          ctx.stroke();
        }
      }
    }
    var wheelY = (z || 0) < 8 ? 10 + (ws - 1) * 2 : 5 + (ws - 1);
    if (!(wet && sub > 0.75)) {
      wheel(-22 * s, wheelY * s, 9.2);
      wheel(-8 * s, wheelY * s, 8.4);   /* dual rear */
      wheel(22 * s, (wheelY - 1) * s, 9.0);
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
        ctx.moveTo(-38 * s, 4 * s);
        ctx.lineTo(-36 * s, -4 * s);
        ctx.lineTo(4 * s, -26 * s);
        ctx.lineTo(48 * s, 2 * s);
        ctx.lineTo(-40 * s, 10 * s);
        ctx.closePath();
        ctx.fill();
        /* Inline bubble hints on hull */
        ctx.strokeStyle = "rgba(200, 240, 255, 0.65)";
        ctx.lineWidth = 1.4;
        for (var bi = 0; bi < 4; bi++) {
          var bx = (-20 + bi * 14) * s;
          var by = waterlineY + 6 * s + (bi % 2) * 5 * s;
          ctx.beginPath(); ctx.arc(bx, by, (2 + bi % 2) * s, 0, Math.PI * 2); ctx.stroke();
        }
      }
    }

    ctx.restore();
  }

  /* polish9/10: frog-color nameplates — quieter so frogs stay readable */
  function drawNameplate(ctx, text, x, y, color, depth, soft) {
    var d = depth || 1;
    ctx.save();
    var fs = soft ? 9 : 10;
    ctx.font = "bold " + Math.round(fs * Math.min(1.2, 0.85 + d * 0.28)) + "px Segoe UI, system-ui, sans-serif";
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    var tw = Math.min(soft ? 88 : 110, ctx.measureText(text).width + (soft ? 10 : 12));
    var th = soft ? 13 : 14;
    ctx.globalAlpha = soft ? 0.72 : 0.9;
    ctx.fillStyle = "rgba(15, 23, 42, 0.62)";
    ctx.strokeStyle = color || "#fef3c7";
    ctx.lineWidth = soft ? 1.2 : 1.5;
    if (ctx.roundRect) {
      ctx.beginPath();
      ctx.roundRect(x - tw * 0.5, y - th * 0.5, tw, th, 5);
      ctx.fill(); ctx.stroke();
    } else {
      ctx.fillRect(x - tw * 0.5, y - th * 0.5, tw, th);
      ctx.strokeRect(x - tw * 0.5, y - th * 0.5, tw, th);
    }
    ctx.fillStyle = color || "#fff";
    ctx.fillText(text, x, y + 0.5);
    ctx.restore();
  }

  function drawAboardIcons(ctx, frogs, cx, cy, depth, lift) {
    if (!frogs || !frogs.length) return;
    var riders = frogs.slice().sort(function (a, b) { return a.id.localeCompare(b.id); });
    var n = riders.length;
    for (var ri = 0; ri < n; ri++) {
      var rf = riders[ri];
      var ox = (ri - (n - 1) * 0.5) * 14 * depth;
      var iy = cy - 24 * depth - lift;
      ctx.fillStyle = "rgba(15,23,42,0.75)";
      ctx.beginPath();
      ctx.arc(cx + ox, iy, 8.2 * depth, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = rf.color;
      ctx.beginPath();
      ctx.arc(cx + ox, iy, 6.6 * depth, 0, Math.PI * 2);
      ctx.fill();
      ctx.strokeStyle = "#0f172a";
      ctx.lineWidth = 1.4;
      ctx.stroke();
      ctx.fillStyle = rf.hat || "#facc15";
      ctx.beginPath();
      ctx.arc(cx + ox, iy - 5.5 * depth, 3.2 * depth, 0, Math.PI * 2);
      ctx.fill();
      /* tiny readable initial under icon */
      ctx.font = "bold " + Math.round(8 * depth) + "px Segoe UI, system-ui, sans-serif";
      ctx.textAlign = "center";
      ctx.fillStyle = "#fef3c7";
      ctx.strokeStyle = "#000";
      ctx.lineWidth = 2.5;
      var initial = (rf.name || rf.id || "?").charAt(0);
      ctx.strokeText(initial, cx + ox, iy + 14 * depth);
      ctx.fillText(initial, cx + ox, iy + 14 * depth);
    }
    ctx.fillStyle = "rgba(15,23,42,0.82)";
    ctx.fillRect(cx - 52 * depth, cy + 18 * depth, 104 * depth, 16 * depth);
    ctx.strokeStyle = "#fbbf24";
    ctx.lineWidth = 1.6;
    ctx.strokeRect(cx - 52 * depth, cy + 18 * depth, 104 * depth, 16 * depth);
    ctx.fillStyle = "#fef3c7";
    ctx.font = "bold " + Math.round(10 * depth) + "px Segoe UI, system-ui, sans-serif";
    ctx.textAlign = "center";
    ctx.fillText("Aboard · " + n, cx, cy + 29 * depth);
  }

  function drawFroggy(ctx, frog, camX, camY, vw, vh, frogs) {
    if (frog && frog.sessionDead) return null; /* mechgun1: removed for session */
    var p = project(frog.x, frog.y, camX, camY, vw, vh);
    var s = 16.4 * p.depth * (0.92 + 0.08 * p.depth); /* polish3 readable */
    var airOnly = (frog.z || 0) - (frog.groundZ || 0);
    var bob = (!frog.inTruck && !frog.inSub && !frog.inSwim && airOnly < 2 && (frog.walkPhase || 0) > 0.05)
      ? Math.abs(Math.sin(frog.walkPhase)) * 1.2 * p.depth : 0;
    /* polish7: idle bounce for AI companions when standing */
    if (!frog.inTruck && !frog.inSub && !frog.inSwim && bob < 0.4 && (frog.idleBounce || 0) > 0) {
      bob += Math.abs(Math.sin(frog.idleBounce)) * (frog.human ? 0.6 : 2.8) * p.depth;
    }
    if (frog.inSwim && !frog.inSub) {
      /* mech7: sink in water + gentle stroke bob (no hop bounce) */
      bob -= 8 * p.depth * Math.min(1, (frog.waterSub || 0.35) + 0.25);
      bob += Math.sin(frog.swimPhase || 0) * 1.1 * p.depth;
      frog.z = 0;
      frog.zVel = 0;
      frog.hopSquash = 0;
      frog.hopStretch = 0;
    }
    var gLift = (frog.inSwim ? 0 : (frog.groundZ || 0)) * 0.58 * p.depth;
    var lift = (frog.z || 0) * 0.58 * p.depth + bob;

    if (frog.inSub) {
      var yawSub = frog.faceAngle != null ? frog.faceAngle : -Math.PI / 2;
      var spSub = Math.hypot(frog.vx || 0, frog.vy || 0);
      if (spSub > 25) yawSub = Math.atan2(frog.vy, frog.vx);
      drawSubmarine(ctx, p.x, p.y, yawSub, p.depth, true, frog.z || 0, frog.color, {
        inWater: true, sub: Math.max(0.75, frog.waterSub || 0.9), wakePhase: frog.wakePhase || 0
      });
      drawNameplate(ctx, frog.name || "You", p.x, p.y - 48 * p.depth, frog.color || "#fff", p.depth, !frog.local);
      return p;
    }

    if (frog.inHeli || frog.inDrone) {
      var kindD = frog.inDrone ? "drone" : "heli";
      var yawAir = frog.faceAngle != null ? frog.faceAngle : -Math.PI / 2;
      var rotor = frog._airRotor || 0;
      var isPilotSeat = frog.airSeat === 0 || frog._airPilot;
      /* Draw craft once: local frog always; else only pilot seat so AI passengers don't stack meshes */
      if (frog.local || isPilotSeat) {
        if (kindD === "heli") drawHelicopter(ctx, p.x, p.y, yawAir, p.depth, (frog.z || 0) > 4, frog.z || 0, "#94a3b8", rotor);
        else drawPassengerDrone(ctx, p.x, p.y, yawAir, p.depth, (frog.z || 0) > 4, frog.z || 0, "#67e8f9", rotor);
        if (frogs) {
          var airCrew = [];
          for (var ai = 0; ai < frogs.length; ai++) {
            if ((kindD === "heli" && frogs[ai].inHeli) || (kindD === "drone" && frogs[ai].inDrone)) airCrew.push(frogs[ai]);
          }
          if (airCrew.length > 1) {
            drawAboardIcons(ctx, airCrew, p.x, p.y - 18 * p.depth - (frog.z || 0) * 0.35 * p.depth, p.depth, (frog.z || 0) * 0.35 * p.depth);
          }
        }
        drawNameplate(ctx, frog.name || "You", p.x, p.y - 52 * p.depth - (frog.z || 0) * 0.4 * p.depth, frog.color || "#fff", p.depth, !frog.local);
      }
      return p;
    }

    if (frog.inTruck && frog.truckMode === "shared" && !frog.local) {
      return p; /* drawn on shared truck roof by driver */
    }

    if (frog.inTruck && frog.local) {
      /* polish11: yaw from travel velocity when moving so nose matches drive */
      var yawDrive = frog.faceAngle != null ? frog.faceAngle : -Math.PI / 2;
      var spDrive = Math.hypot(frog.vx || 0, frog.vy || 0);
      if (spDrive > 35) yawDrive = Math.atan2(frog.vy, frog.vx);
      var styleDrive = frog.vehicleStyle || (global.FroggiesCanon && global.FroggiesCanon.vehicleStyleOf ? global.FroggiesCanon.vehicleStyleOf(frog.truckId) : "cybertruck");
      drawDriveVehicle(ctx, styleDrive, p.x, p.y, yawDrive, p.depth, true, frog.z || 0, frog.color, { inWater: inPond(frog.x, frog.y), sub: frog.waterSub || 0, wakePhase: frog.wakePhase || 0, bounce: frog.truckBounce || 0, wheelScale: frog.wheelScale || (global.FroggiesCanon && global.FroggiesCanon.getWheelScale ? global.FroggiesCanon.getWheelScale() : 1) });
      if (frog.truckMode === "shared" && frogs) {
        drawAboardIcons(ctx, frogs, p.x, p.y, p.depth, lift);
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
        /* polish11: ZOOM spark trail behind travel nose */
        var tAng = (frog.faceAngle != null) ? frog.faceAngle : (frog.facing >= 0 ? 0 : Math.PI);
        var tx = Math.cos(tAng), ty = Math.sin(tAng);
        ctx.fillStyle = "rgba(251, 191, 36, 0.5)";
        ctx.beginPath();
        ctx.moveTo(p.x - tx * 40 * p.depth, p.y - ty * 40 * p.depth);
        ctx.lineTo(p.x - tx * 14 * p.depth - ty * 10 * p.depth, p.y - ty * 14 * p.depth + tx * 10 * p.depth);
        ctx.lineTo(p.x - tx * 14 * p.depth + ty * 10 * p.depth, p.y - ty * 14 * p.depth - tx * 10 * p.depth);
        ctx.fill();
        ctx.fillStyle = "rgba(125, 211, 252, 0.4)";
        ctx.beginPath();
        ctx.moveTo(p.x - tx * 28 * p.depth, p.y - ty * 28 * p.depth);
        ctx.lineTo(p.x - tx * 8 * p.depth - ty * 6 * p.depth, p.y - ty * 8 * p.depth + tx * 6 * p.depth);
        ctx.lineTo(p.x - tx * 8 * p.depth + ty * 6 * p.depth, p.y - ty * 8 * p.depth - tx * 6 * p.depth);
        ctx.fill();
      }
      if (frog.truckMode !== "shared") {
        drawNameplate(ctx, frog.name || "You", p.x, p.y - 40 * p.depth - lift, frog.color || "#fff", p.depth, !frog.local);
      }
      /* polish11: EXIT tip is brief HUD chip (main.js) — not a world billboard that tracks the truck */
      return p;
    }

    if (frog.inTruck && !frog.local && frog.truckMode === "solo") {
      var yawSolo = frog.faceAngle != null ? frog.faceAngle : -Math.PI / 2;
      var spSolo = Math.hypot(frog.vx || 0, frog.vy || 0);
      if (spSolo > 35) yawSolo = Math.atan2(frog.vy, frog.vx);
      var styleSolo = frog.vehicleStyle || (global.FroggiesCanon && global.FroggiesCanon.vehicleStyleOf ? global.FroggiesCanon.vehicleStyleOf(frog.truckId) : "cybertruck");
      drawDriveVehicle(ctx, styleSolo, p.x, p.y, yawSolo, p.depth, true, frog.z || 0, frog.color, { inWater: inPond(frog.x, frog.y), sub: frog.waterSub || 0, wakePhase: frog.wakePhase || 0, bounce: frog.truckBounce || 0, wheelScale: frog.wheelScale || (global.FroggiesCanon && global.FroggiesCanon.getWheelScale ? global.FroggiesCanon.getWheelScale() : 1) });
      ctx.fillStyle = frog.color;
      ctx.beginPath();
      ctx.arc(p.x, p.y - 22 * p.depth - lift, 6 * p.depth, 0, Math.PI * 2);
      ctx.fill();
      drawNameplate(ctx, frog.name || "Frog", p.x, p.y - 38 * p.depth - lift, frog.color || "#fff", p.depth);
      return p;
    }

    if (frog.inMech) {
      /* interact1/mech-huge: keep full 10/100/1000-story silhouette while piloting (do NOT clamp) */
      var stories = frog.mechStories || 10;
      var CbandP = global.FroggiesCanon;
      var bandP = CbandP && CbandP.mechBand ? CbandP.mechBand(stories) : (stories >= 1e12 ? "trillion" : stories >= 1000 ? "1000" : stories >= 100 ? "100" : "10");
      var tint = bandP === "trillion" ? "#f9a8d4" : bandP === "1000" ? "#fcd34d" : bandP === "100" ? "#67e8f9" : "#a5b4fc";
      var bobM = Math.abs(Math.sin(frog.walkPhase || 0)) * (bandP === "trillion" ? 6.2 : bandP === "1000" ? 4.5 : bandP === "100" ? 3.2 : 2.2) * p.depth;
      drawMech(ctx, frog.x, frog.y, stories, camX, camY, vw, vh, tint);
      /* Pilot hat / nameplate scaled to mech torso height (same hScale bands as drawMech) */
      var hatH = bandP === "trillion" ? 250 : bandP === "1000" ? 168 : bandP === "100" ? 78 : 36;
      var hatR = bandP === "trillion" ? 12 : bandP === "1000" ? 9 : bandP === "100" ? 7 : 5.5;
      ctx.fillStyle = frog.color || "#4ade80";
      ctx.beginPath();
      ctx.arc(p.x, p.y - hatH * p.depth - bobM, hatR * p.depth, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = frog.hat || "#facc15";
      ctx.beginPath();
      ctx.arc(p.x, p.y - (hatH + 8) * p.depth - bobM, hatR * 0.58 * p.depth, 0, Math.PI * 2);
      ctx.fill();
      var plateH = bandP === "trillion" ? 310 : bandP === "1000" ? 210 : bandP === "100" ? 100 : 58;
      drawNameplate(ctx, (frog.name || "Frog") + " · MECH", p.x, p.y - plateH * p.depth - bobM, frog.color || "#fff", p.depth, !frog.local);
      return p;
    }

    /* hop4: humanoid frog — torso + head, big springy legs (extend mid-hop, tuck on land) */
    /* polish6: softer drop shadow under character */
    /* eyes1: rotate body/face toward faceAngle (walk dir); idle keeps last */
    var shA = 0.4 - Math.min(0.24, Math.max(0, airOnly) * 0.005);
    var shW = s * (1.05 - Math.min(0.35, Math.max(0, airOnly) * 0.009));
    drawSoftShadow(ctx, p.x, p.y - gLift + 6, shW, s * 0.32, shA);
    var by = p.y - s * 0.55 - lift;
    var faceA = (frog.faceAngle != null && isFinite(frog.faceAngle)) ? frog.faceAngle : -Math.PI / 2;
    /* hop2/hop4: stretch mid-air (+ hopStretch), squash on land; spring = leg extend amount */
    var swimming = !!(frog.inSwim && !frog.inSub);
    var airZ = swimming ? 0 : (frog.z || 0);
    var sq = swimming ? 0 : (frog.hopSquash || 0);
    var st = swimming ? 0 : (frog.hopStretch || 0);
    var spring = swimming ? 0 : Math.max(0, Math.min(1.15, airZ * 0.028 + st * 0.55 - sq * 0.85));
    var stretchY = swimming ? 0.62 : (1 + Math.min(0.28, airZ * 0.008) + st * 0.12 - sq * 0.22);
    var stretchX = swimming ? 1.28 : (1 - Math.min(0.18, airZ * 0.005) - st * 0.08 + sq * 0.22);
    var d = p.depth;
    var swimPh = frog.swimPhase || 0;
    if (swimming) {
      var spdSwim = Math.hypot(frog.vx || 0, frog.vy || 0);
      frog.swimPhase = swimPh + 0.18 + Math.min(0.35, spdSwim * 0.002);
      swimPh = frog.swimPhase;
    }
    var stroke = swimming ? Math.sin(swimPh) : 0;
    var strokeB = swimming ? Math.sin(swimPh + Math.PI) : 0;
    var hipY = swimming ? s * 0.08 : s * 0.22;
    var thighLen = swimming ? s * (0.42 + Math.abs(stroke) * 0.2) : s * (0.55 + spring * 0.55);
    var shinLen = swimming ? s * (0.38 + Math.abs(strokeB) * 0.18) : s * (0.48 + spring * 0.62);
    var outX = swimming ? s * (0.55 + Math.abs(stroke) * 0.2) : s * (0.22 + spring * 0.55);
    var kick = swimming ? 0 : ((!frog.inTruck && spring < 0.15 && (frog.walkPhase || 0) > 0.05)
      ? Math.sin(frog.walkPhase * 2) * 2.4 * d : 0);
    ctx.save();
    ctx.translate(p.x, by);
    ctx.rotate(faceA + Math.PI / 2); /* canonical face points screen-up */
    if (swimming) ctx.rotate(-1.05); /* mech7: flatter / horizontal in water */
    ctx.scale(stretchX, stretchY);
    /* Big springy hind legs — Z-fold: hip → knee out → foot; spring out mid-hop, tuck on land */
    function drawSpringLeg(side) {
      var hx = side * s * 0.28;
      var hy = hipY;
      var kickAmt = swimming ? ((side < 0 ? stroke : strokeB) * s * 0.35) : (kick * side * 0.15);
      var kx = side * outX + kickAmt;
      var ky = hy + thighLen * (swimming ? (0.35 + (side < 0 ? stroke : strokeB) * 0.2) : (0.55 + spring * 0.15));
      var fx = side * (outX * 0.55 + s * 0.08) + (swimming ? kickAmt * 1.2 : kick * side);
      var fy = hy + thighLen + shinLen * (swimming ? (0.55 - Math.abs(side < 0 ? stroke : strokeB) * 0.15) : (0.75 - spring * 0.12));
      ctx.strokeStyle = frog.accent;
      ctx.lineWidth = Math.max(3.4, s * 0.22);
      ctx.lineCap = "round";
      ctx.lineJoin = "round";
      ctx.beginPath();
      ctx.moveTo(hx, hy);
      ctx.lineTo(kx, ky);
      ctx.lineTo(fx, fy);
      ctx.stroke();
      ctx.strokeStyle = frog.color;
      ctx.lineWidth = Math.max(2.2, s * 0.14);
      ctx.beginPath();
      ctx.moveTo(hx, hy);
      ctx.lineTo(kx, ky);
      ctx.lineTo(fx, fy);
      ctx.stroke();
      /* Foot pad */
      ctx.fillStyle = frog.accent;
      ctx.beginPath();
      ctx.ellipse(fx, fy + 1.5 * d, s * (0.22 + spring * 0.06), s * 0.1, side * 0.25, 0, Math.PI * 2);
      ctx.fill();
    }
    drawSpringLeg(-1);
    drawSpringLeg(1);
    /* Small arms — paddle stroke while swimming */
    ctx.strokeStyle = frog.accent;
    ctx.lineWidth = Math.max(2.2, s * 0.12);
    ctx.lineCap = "round";
    ctx.beginPath();
    if (swimming) {
      ctx.moveTo(-s * 0.38, -s * 0.02);
      ctx.lineTo(-s * (0.75 + strokeB * 0.35), s * (-0.05 + strokeB * 0.35));
      ctx.moveTo(s * 0.38, -s * 0.02);
      ctx.lineTo(s * (0.75 + stroke * 0.35), s * (-0.05 + stroke * 0.35));
    } else {
      ctx.moveTo(-s * 0.38, -s * 0.05);
      ctx.lineTo(-s * (0.55 + spring * 0.08), s * 0.28);
      ctx.moveTo(s * 0.38, -s * 0.05);
      ctx.lineTo(s * (0.55 + spring * 0.08), s * 0.28);
    }
    ctx.stroke();
    /* Torso */
    var torsoGrad = ctx.createRadialGradient(-3, -s * 0.15, 2, 0, 0, s * 0.7);
    torsoGrad.addColorStop(0, frog.color);
    torsoGrad.addColorStop(0.75, frog.color);
    torsoGrad.addColorStop(1, frog.accent);
    ctx.fillStyle = torsoGrad;
    ctx.beginPath();
    ctx.ellipse(0, s * 0.02, s * 0.52, s * 0.58, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.strokeStyle = "#0b1220"; ctx.lineWidth = 2.4; ctx.stroke();
    ctx.strokeStyle = frog.accent; ctx.lineWidth = 1.4; ctx.stroke();
    /* Belly */
    ctx.fillStyle = "rgba(254, 243, 199, 0.88)";
    ctx.beginPath();
    ctx.ellipse(0, s * 0.12, s * 0.28, s * 0.34, 0, 0, Math.PI * 2);
    ctx.fill();
    /* Head */
    var headR = s * 0.42;
    var headY = -s * 0.72;
    var hg = ctx.createRadialGradient(-2, headY - 3, 1, 0, headY, headR);
    hg.addColorStop(0, frog.color);
    hg.addColorStop(1, frog.accent);
    ctx.fillStyle = hg;
    ctx.beginPath(); ctx.arc(0, headY, headR, 0, Math.PI * 2); ctx.fill();
    ctx.strokeStyle = "#0b1220"; ctx.lineWidth = 2.2; ctx.stroke();
    /* Blush on cheeks */
    ctx.fillStyle = "rgba(251, 113, 133, 0.45)";
    ctx.beginPath();
    ctx.ellipse(-headR * 0.55, headY + headR * 0.2, 2.8 * d, 1.9 * d, 0, 0, Math.PI * 2);
    ctx.ellipse(headR * 0.55, headY + headR * 0.2, 2.8 * d, 1.9 * d, 0, 0, Math.PI * 2);
    ctx.fill();
    /* Hat on head */
    ctx.fillStyle = frog.hat || "#facc15";
    ctx.beginPath();
    ctx.ellipse(0, headY - headR * 0.55, headR * 0.95, headR * 0.28, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillRect(-headR * 0.42, headY - headR * 1.35, headR * 0.84, headR * 0.7);
    ctx.strokeStyle = "#0b1220";
    ctx.lineWidth = 1.3;
    ctx.strokeRect(-headR * 0.42, headY - headR * 1.35, headR * 0.84, headR * 0.7);
    /* Eyes — pupils bias forward (local -Y) */
    var eL = -headR * 0.32;
    var eR = headR * 0.36;
    var eY = headY - headR * 0.08;
    var eRsz = headR * 0.28;
    ctx.fillStyle = "#fff";
    ctx.beginPath();
    ctx.arc(eL, eY, eRsz, 0, Math.PI * 2);
    ctx.arc(eR, eY, eRsz, 0, Math.PI * 2);
    ctx.fill();
    ctx.strokeStyle = "#0b1220";
    ctx.lineWidth = 1.15;
    ctx.beginPath();
    ctx.arc(eL, eY, eRsz, 0, Math.PI * 2);
    ctx.arc(eR, eY, eRsz, 0, Math.PI * 2);
    ctx.stroke();
    ctx.fillStyle = "#111";
    ctx.beginPath();
    ctx.arc(eL, eY - 0.7 * d, eRsz * 0.45, 0, Math.PI * 2);
    ctx.arc(eR, eY - 0.7 * d, eRsz * 0.45, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = "#fff";
    ctx.beginPath();
    ctx.arc(eL - 0.15 * d, eY - 1.3 * d, eRsz * 0.18, 0, Math.PI * 2);
    ctx.arc(eR - 0.15 * d, eY - 1.3 * d, eRsz * 0.18, 0, Math.PI * 2);
    ctx.fill();
    /* Smile on head */
    ctx.strokeStyle = "#0b1220";
    ctx.lineWidth = 1.5 * d;
    ctx.beginPath();
    ctx.arc(0, headY + headR * 0.28, headR * 0.32, 0.15, Math.PI - 0.15);
    ctx.stroke();
    ctx.restore();
    var plateTop = by - s * 1.35;
    /* polish9/10: quieter nameplates — name only; local ring; no · AI clutter */
    if (frog.local) {
      ctx.strokeStyle = "rgba(255,255,255,0.75)";
      ctx.lineWidth = 2;
      ctx.beginPath(); ctx.ellipse(p.x, by + s * 0.1, s * 0.7, s * 0.95, 0, 0, Math.PI * 2); ctx.stroke();
    }
    drawNameplate(ctx, frog.name || "Frog", p.x, plateTop, frog.color || "#fff", p.depth, !frog.local);
    /* polish7: chat bubble one-liner (existing canon strings only) */
    if (frog.chatT > 0 && frog.chatLine) {
      var alpha = Math.min(1, frog.chatT * 1.4);
      var msg = frog.chatLine;
      ctx.font = "bold 10px Segoe UI, system-ui, sans-serif";
      var tw = Math.min(160, ctx.measureText(msg).width + 14);
      var bx = p.x;
      var byb = plateTop - 10;
      ctx.fillStyle = "rgba(255,255,255," + (0.92 * alpha) + ")";
      ctx.strokeStyle = "rgba(15,23,42," + (0.85 * alpha) + ")";
      ctx.lineWidth = 1.5;
      ctx.beginPath();
      ctx.roundRect ? ctx.roundRect(bx - tw * 0.5, byb - 14, tw, 20, 6) : ctx.rect(bx - tw * 0.5, byb - 14, tw, 20);
      ctx.fill(); ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(bx - 4, byb + 6); ctx.lineTo(bx, byb + 12); ctx.lineTo(bx + 4, byb + 6);
      ctx.fill();
      ctx.fillStyle = "rgba(15,23,42," + alpha + ")";
      ctx.textAlign = "center";
      ctx.fillText(msg, bx, byb);
    }
    return p;
  }

  function drawAirPad(ctx, wx, wy, kind, camX, camY, vw, vh, near) {
    var p = project(wx, wy, camX, camY, vw, vh);
    var s = p.depth;
    var big = kind === "heli";
    var R = (big ? 52 : 36) * s;
    ctx.save();
    /* Painted circle */
    ctx.beginPath();
    ctx.ellipse(p.x, p.y, R, R * 0.42, -0.12, 0, Math.PI * 2);
    /* padfix1: opaque disc so orange compound tint cannot wash out H/D */
    ctx.fillStyle = near ? "rgba(15, 23, 42, 0.98)" : "rgba(15, 23, 42, 0.94)";
    ctx.fill();
    ctx.strokeStyle = big ? "#fbbf24" : "#67e8f9";
    ctx.lineWidth = (near ? 3.8 : 2.6) * s;
    ctx.stroke();
    ctx.beginPath();
    ctx.ellipse(p.x, p.y, R * 0.72, R * 0.30, -0.12, 0, Math.PI * 2);
    ctx.strokeStyle = "rgba(255,255,255,0.45)";
    ctx.lineWidth = 1.4 * s;
    ctx.stroke();
    ctx.fillStyle = big ? "#fde68a" : "#a5f3fc";
    ctx.font = "bold " + Math.round((big ? 28 : 22) * s) + "px Segoe UI, system-ui, sans-serif";
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    ctx.fillText(big ? "H" : "D", p.x, p.y - 2 * s);
    ctx.textBaseline = "alphabetic";
    ctx.fillStyle = near ? "#fef3c7" : "rgba(255,255,255,0.75)";
    ctx.font = "bold " + Math.round(11 * s) + "px Segoe UI, system-ui, sans-serif";
    ctx.fillText(big ? "HELIPAD" : "DRONE", p.x, p.y + R * 0.55 + 10 * s);
    ctx.restore();
  }

  function drawHelicopter(ctx, x, y, faceAngle, depth, flying, z, accent, rotor) {
    var s = depth;
    var lift = (z || 0) * 0.58 * s;
    var yaw = faceAngle != null ? faceAngle : -Math.PI / 2;
    ctx.save();
    ctx.translate(x, y - lift);
    /* Ground shadow shrinks with height */
    var sh = Math.max(0.25, 1 - (z || 0) / 320);
    ctx.fillStyle = "rgba(0,0,0," + (0.28 * sh) + ")";
    ctx.beginPath();
    ctx.ellipse(0, lift + 6 * s, 38 * s * sh, 12 * s * sh, -0.12, 0, Math.PI * 2);
    ctx.fill();
    ctx.rotate(yaw + Math.PI / 2);
    /* Skids */
    ctx.strokeStyle = "#64748b";
    ctx.lineWidth = 2.4 * s;
    ctx.beginPath();
    ctx.moveTo(-22 * s, 10 * s); ctx.lineTo(22 * s, 10 * s);
    ctx.moveTo(-22 * s, 16 * s); ctx.lineTo(22 * s, 16 * s);
    ctx.moveTo(-14 * s, 4 * s); ctx.lineTo(-14 * s, 16 * s);
    ctx.moveTo(14 * s, 4 * s); ctx.lineTo(14 * s, 16 * s);
    ctx.stroke();
    /* Fuselage */
    ctx.fillStyle = accent || "#94a3b8";
    ctx.beginPath();
    ctx.moveTo(-8 * s, -6 * s);
    ctx.lineTo(18 * s, -4 * s);
    ctx.lineTo(26 * s, 2 * s);
    ctx.lineTo(14 * s, 10 * s);
    ctx.lineTo(-16 * s, 10 * s);
    ctx.lineTo(-22 * s, 2 * s);
    ctx.closePath();
    ctx.fill();
    ctx.strokeStyle = "#1e293b";
    ctx.lineWidth = 1.2 * s;
    ctx.stroke();
    /* Cabin glass */
    ctx.fillStyle = "rgba(125, 211, 252, 0.55)";
    ctx.fillRect(-6 * s, -4 * s, 16 * s, 8 * s);
    /* Tail boom */
    ctx.fillStyle = "#64748b";
    ctx.fillRect(-36 * s, -2 * s, 18 * s, 5 * s);
    ctx.fillStyle = "#fbbf24";
    ctx.fillRect(-38 * s, -8 * s, 4 * s, 14 * s);
    /* Main rotor */
    var spin = (rotor || 0) * (flying ? 1 : 0.15);
    ctx.save();
    ctx.translate(2 * s, -10 * s);
    ctx.rotate(spin);
    ctx.strokeStyle = flying ? "rgba(226,232,240,0.85)" : "rgba(148,163,184,0.7)";
    ctx.lineWidth = 2.2 * s;
    ctx.beginPath();
    ctx.moveTo(-34 * s, 0); ctx.lineTo(34 * s, 0);
    ctx.moveTo(0, -34 * s); ctx.lineTo(0, 34 * s);
    ctx.stroke();
    ctx.fillStyle = "#334155";
    ctx.beginPath(); ctx.arc(0, 0, 3.2 * s, 0, Math.PI * 2); ctx.fill();
    ctx.restore();
    /* H mark on roof */
    ctx.fillStyle = "#fde68a";
    ctx.font = "bold " + Math.round(10 * s) + "px Segoe UI, system-ui, sans-serif";
    ctx.textAlign = "center";
    ctx.fillText("H", 2 * s, 8 * s);
    ctx.restore();
  }

  function drawPassengerDrone(ctx, x, y, faceAngle, depth, flying, z, accent, rotor) {
    var s = depth;
    var lift = (z || 0) * 0.58 * s;
    var yaw = faceAngle != null ? faceAngle : -Math.PI / 2;
    ctx.save();
    ctx.translate(x, y - lift);
    var sh = Math.max(0.25, 1 - (z || 0) / 280);
    ctx.fillStyle = "rgba(0,0,0," + (0.24 * sh) + ")";
    ctx.beginPath();
    ctx.ellipse(0, lift + 4 * s, 22 * s * sh, 8 * s * sh, -0.12, 0, Math.PI * 2);
    ctx.fill();
    ctx.rotate(yaw + Math.PI / 2);
    /* Body */
    ctx.fillStyle = accent || "#67e8f9";
    ctx.beginPath();
    ctx.moveTo(-10 * s, -8 * s);
    ctx.lineTo(10 * s, -8 * s);
    ctx.quadraticCurveTo(14 * s, -8 * s, 14 * s, -4 * s);
    ctx.lineTo(14 * s, 4 * s);
    ctx.quadraticCurveTo(14 * s, 8 * s, 10 * s, 8 * s);
    ctx.lineTo(-10 * s, 8 * s);
    ctx.quadraticCurveTo(-14 * s, 8 * s, -14 * s, 4 * s);
    ctx.lineTo(-14 * s, -4 * s);
    ctx.quadraticCurveTo(-14 * s, -8 * s, -10 * s, -8 * s);
    ctx.closePath();
    ctx.fill();
    ctx.strokeStyle = "#0e7490";
    ctx.lineWidth = 1.4 * s;
    ctx.stroke();
    ctx.fillStyle = "rgba(15, 23, 42, 0.45)";
    ctx.fillRect(-7 * s, -5 * s, 14 * s, 8 * s);
    /* Arms + 4 rotors */
    var arms = [[-16, -12], [16, -12], [-16, 12], [16, 12]];
    var spin = (rotor || 0) * (flying ? 1.4 : 0.2);
    for (var ai = 0; ai < arms.length; ai++) {
      var ax = arms[ai][0] * s, ay = arms[ai][1] * s;
      ctx.strokeStyle = "#475569";
      ctx.lineWidth = 2 * s;
      ctx.beginPath(); ctx.moveTo(0, 0); ctx.lineTo(ax, ay); ctx.stroke();
      ctx.save();
      ctx.translate(ax, ay);
      ctx.rotate(spin + ai * 0.7);
      ctx.strokeStyle = flying ? "rgba(165,243,252,0.9)" : "rgba(148,163,184,0.65)";
      ctx.lineWidth = 1.8 * s;
      ctx.beginPath(); ctx.moveTo(-9 * s, 0); ctx.lineTo(9 * s, 0); ctx.stroke();
      ctx.beginPath(); ctx.moveTo(0, -9 * s); ctx.lineTo(0, 9 * s); ctx.stroke();
      ctx.restore();
    }
    ctx.fillStyle = "#ecfeff";
    ctx.font = "bold " + Math.round(9 * s) + "px Segoe UI, system-ui, sans-serif";
    ctx.textAlign = "center";
    ctx.fillText("D", 0, 4 * s);
    ctx.restore();
  }

  function drawParkedAir(ctx, world, frogs, camX, camY, vw, vh) {
    var Air = global.FroggiesAir;
    var C = global.FroggiesCanon;
    if (!Air || !world) return;
    var kinds = ["heli", "drone"];
    for (var ki = 0; ki < kinds.length; ki++) {
      var kind = kinds[ki];
      if (C && C.isPermaGone && C.isPermaGone(kind)) {
        var homePadGone = kind === "drone" ? ((C && C.DRONE_PAD) || { x: 1180, y: 2000 }) : ((C && C.HELI_PAD) || { x: 980, y: 2000 });
        drawAirPad(ctx, homePadGone.x, homePadGone.y, kind, camX, camY, vw, vh, false);
        continue; /* mechgun1: craft gone for session */
      }
      var craft = Air.ensureCraft(world, kind);
      if (craft && craft.goneForever) continue;
      var homePad = kind === "drone" ? ((C && C.DRONE_PAD) || { x: 1180, y: 2000 }) : ((C && C.HELI_PAD) || { x: 980, y: 2000 });
      /* Painted H/D pad stays at ranch home — craft may fly away */
      drawAirPad(ctx, homePad.x, homePad.y, kind, camX, camY, vw, vh, false);
      var occupied = Air.occupiedCount(craft) > 0;
      /* Occupied craft drawn by pilot frog; empty craft silhouette at park/craft xy */
      if (!occupied && craft) {
        var p = project(craft.x, craft.y, camX, camY, vw, vh);
        if (kind === "heli") drawHelicopter(ctx, p.x, p.y, craft.faceAngle, p.depth, false, 0, "#94a3b8", craft.rotor || 0);
        else drawPassengerDrone(ctx, p.x, p.y, craft.faceAngle, p.depth, false, 0, "#67e8f9", craft.rotor || 0);
      }
    }
  }

  function drawHotspot(ctx, h, camX, camY, vw, vh, near) {
    /* polish4: story props invite — phone / SPS / Starship / trucks */
    var p = project(h.x, h.y, camX, camY, vw, vh);
    var pulse = near ? 1 + Math.sin(Date.now() / 180) * 0.14 : 1;
    var baseR = near ? 28 : 15;
    if (h.id === "phone" || h.id === "sps" || h.id === "starship") baseR += 4;
    var r = baseR * p.depth * pulse;
    var s = p.depth;
    /* Soft ground glow pedestal */
    ctx.fillStyle = near ? "rgba(251, 191, 36, 0.22)" : "rgba(255,255,255,0.08)";
    ctx.beginPath();
    ctx.ellipse(p.x, p.y + 4 * s, r * 1.15, r * 0.38, 0, 0, Math.PI * 2);
    ctx.fill();
    if (near) {
      ctx.beginPath();
      ctx.arc(p.x, p.y, r * 1.4, 0, Math.PI * 2);
      ctx.strokeStyle = "rgba(251, 191, 36, 0.4)";
      ctx.lineWidth = 3.4;
      ctx.stroke();
    }
    /* Prop silhouette by story id */
    if (h.id === "phone") {
      ctx.fillStyle = near ? "#a78bfa" : "#7c3aed";
      ctx.fillRect(p.x - 10 * s, p.y - 28 * s, 20 * s, 30 * s);
      ctx.strokeStyle = "#fbbf24"; ctx.lineWidth = 2;
      ctx.strokeRect(p.x - 10 * s, p.y - 28 * s, 20 * s, 30 * s);
      ctx.fillStyle = "#312e81";
      ctx.fillRect(p.x - 6 * s, p.y - 22 * s, 12 * s, 10 * s);
      ctx.fillStyle = "#fde68a";
      ctx.beginPath(); ctx.arc(p.x + 5 * s, p.y - 8 * s, 2.2 * s, 0, Math.PI * 2); ctx.fill();
      ctx.fillStyle = "#e9d5ff";
      ctx.font = "bold " + Math.round(9 * s) + "px Segoe UI, system-ui, sans-serif";
      ctx.textAlign = "center";
      ctx.fillText("→ Purple Bear", p.x, p.y + 18 * s);
    } else if (h.id === "sps") {
      ctx.strokeStyle = near ? "#38bdf8" : "#0284c7";
      ctx.lineWidth = 2.4;
      ctx.beginPath();
      ctx.ellipse(p.x, p.y - 10 * s, 16 * s, 8 * s, -0.4, 0, Math.PI * 2);
      ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(p.x, p.y - 10 * s); ctx.lineTo(p.x, p.y + 6 * s); ctx.stroke();
      ctx.fillStyle = "#fbbf24";
      ctx.beginPath(); ctx.arc(p.x, p.y - 10 * s, 3.5 * s, 0, Math.PI * 2); ctx.fill();
      ctx.fillStyle = "#bae6fd";
      ctx.font = "bold " + Math.round(9 * s) + "px Segoe UI, system-ui, sans-serif";
      ctx.textAlign = "center";
      ctx.fillText("→ Optimus · Jimmy", p.x, p.y + 18 * s);
    } else if (h.id === "starship") {
      ctx.fillStyle = near ? "#e2e8f0" : "#94a3b8";
      ctx.beginPath();
      ctx.moveTo(p.x, p.y - 26 * s);
      ctx.lineTo(p.x + 10 * s, p.y - 4 * s);
      ctx.lineTo(p.x - 10 * s, p.y - 4 * s);
      ctx.closePath(); ctx.fill();
    } else {
      ctx.beginPath();
      ctx.arc(p.x, p.y, r * 0.85, 0, Math.PI * 2);
      ctx.fillStyle = near ? "rgba(251, 191, 36, 0.55)" : "rgba(255,255,255,0.14)";
      ctx.fill();
      ctx.strokeStyle = near ? "#fde68a" : "rgba(255,255,255,0.4)";
      ctx.lineWidth = near ? 3.2 : 1.2;
      ctx.stroke();
    }
    ctx.textAlign = "center";
    if (near) {
      var tip = h.tip || h.label;
      var badgeY = p.y - 42 * p.depth;
      var bw = Math.max(88, (tip.length * 6.2 + 24)) * Math.min(1.2, p.depth + 0.25);
      ctx.fillStyle = "rgba(15, 23, 42, 0.88)";
      ctx.fillRect(p.x - bw * 0.5, badgeY - 26, bw, 40);
      ctx.strokeStyle = "#fbbf24";
      ctx.lineWidth = 2.2;
      ctx.strokeRect(p.x - bw * 0.5, badgeY - 26, bw, 40);
      ctx.fillStyle = "#fef3c7";
      ctx.font = "bold 12px Segoe UI, system-ui, sans-serif";
      ctx.fillText(h.label, p.x, badgeY - 8);
      ctx.fillStyle = "#fbbf24";
      ctx.font = "bold 11px Segoe UI, system-ui, sans-serif";
      var prompt = (h.kind === "truck" || (h.id && String(h.id).indexOf("truck") === 0) || h.kind === "mech" || (h.id && String(h.id).indexOf("mech") === 0) || h.kind === "submarine" || (h.id && String(h.id).indexOf("submarine") === 0) || h.kind === "heli" || h.kind === "drone" || h.id === "heli" || h.id === "drone")
        ? "BOARD · INTERACT / E" : "INTERACT · E";
      ctx.fillText(prompt, p.x, badgeY + 8);
    } else {
      ctx.fillStyle = "rgba(255,255,255,0.82)";
      ctx.font = "bold 11px Segoe UI, system-ui, sans-serif";
      ctx.fillText(h.label, p.x, p.y - 32 * p.depth);
    }
  }

  function drawParkedTrucks(ctx, world, frogs, camX, camY, vw, vh) {
    var occupied = {};
    for (var i = 0; i < frogs.length; i++) {
      if (frogs[i].inTruck && frogs[i].truckId) occupied[frogs[i].truckId] = true;
    }
    for (var t = 0; t < TRUCK_SPOTS.length; t++) {
      var spot = TRUCK_SPOTS[t];
      var hid = spot.id === "shared" ? "truck-shared" : "truck-" + spot.id;
      var Cgone = global.FroggiesCanon;
      if (Cgone && Cgone.isPermaGone && Cgone.isPermaGone(hid)) continue; /* mechgun1 */
      if (occupied[hid]) continue;
      var anyInShared = frogs.some(function (f) { return f.inTruck && f.truckMode === "shared"; });
      if (spot.id === "shared" && anyInShared) continue;
      var soloTaken = frogs.some(function (f) {
        return f.inTruck && f.truckMode === "solo" && f.truckId === hid;
      });
      if (soloTaken) continue;
      var Cpk = global.FroggiesCanon;
      var hidP = spot.id === "shared" ? "truck-shared" : "truck-" + spot.id;
      var parkT = Cpk && Cpk.vehiclePos ? Cpk.vehiclePos(hidP, spot.x, spot.y) : { x: spot.x, y: spot.y };
      var p = project(parkT.x, parkT.y, camX, camY, vw, vh);
      var styleP = spot.vehicleStyle || (spot.id === "ripsaw" ? "ripsaw" : spot.id === "tank" ? "tank" : "cybertruck");
      var accent = spot.id === "shared" ? "#fbbf24"
        : styleP === "ripsaw" ? "#a8a29e"
        : styleP === "tank" ? "#6b7280"
        : (FROG_COLORS[spot.id] || {}).body;
      if (spot.id === "shared") {
        /* polish5: shared pile-in truck obvious — pad, frog slots, ALL ABOARD */
        var pulse = 1 + Math.sin(Date.now() / 220) * 0.06;
        ctx.fillStyle = "rgba(251, 191, 36, 0.22)";
        ctx.beginPath();
        ctx.ellipse(p.x, p.y + 8 * p.depth, 58 * p.depth * pulse, 22 * p.depth * pulse, 0, 0, Math.PI * 2);
        ctx.fill();
        ctx.strokeStyle = "rgba(253, 224, 71, 0.85)";
        ctx.lineWidth = 3;
        ctx.setLineDash([8, 6]);
        ctx.beginPath();
        ctx.ellipse(p.x, p.y + 8 * p.depth, 52 * p.depth, 18 * p.depth, 0, 0, Math.PI * 2);
        ctx.stroke();
        ctx.setLineDash([]);
      }
      drawDriveVehicle(ctx, styleP, p.x, p.y, -Math.PI / 2, p.depth, false, 0, accent, { inWater: inPond(parkT.x, parkT.y), sub: 0, wakePhase: 0, wheelScale: 1 });
      if (styleP === "ripsaw" || styleP === "tank") {
        ctx.fillStyle = "rgba(15, 23, 42, 0.82)";
        ctx.fillRect(p.x - 36 * p.depth, p.y + 18 * p.depth, 72 * p.depth, 16 * p.depth);
        ctx.strokeStyle = styleP === "ripsaw" ? "#a8a29e" : "#9ca3af";
        ctx.lineWidth = 1.6;
        ctx.strokeRect(p.x - 36 * p.depth, p.y + 18 * p.depth, 72 * p.depth, 16 * p.depth);
        ctx.fillStyle = "#f8fafc";
        ctx.font = "bold " + Math.round(10 * p.depth) + "px system-ui,sans-serif";
        ctx.textAlign = "center";
        ctx.fillText(styleP === "ripsaw" ? "RIPSAW" : "TANK", p.x, p.y + 29 * p.depth);
      }
      if (spot.id === "shared") {
        var ids = ["james", "jimmy", "bubbles", "rexy"];
        for (var si = 0; si < 4; si++) {
          var sx = p.x + (si - 1.5) * 12 * p.depth;
          var sy = p.y - 26 * p.depth;
          ctx.fillStyle = (FROG_COLORS[ids[si]] || {}).body || "#4ade80";
          ctx.beginPath();
          ctx.arc(sx, sy, 5.5 * p.depth, 0, Math.PI * 2);
          ctx.fill();
          ctx.strokeStyle = "#0f172a";
          ctx.lineWidth = 1.2;
          ctx.stroke();
        }
        ctx.fillStyle = "rgba(15, 23, 42, 0.85)";
        ctx.fillRect(p.x - 58 * p.depth, p.y + 22 * p.depth, 116 * p.depth, 22 * p.depth);
        ctx.strokeStyle = "#fbbf24";
        ctx.lineWidth = 2;
        ctx.strokeRect(p.x - 58 * p.depth, p.y + 22 * p.depth, 116 * p.depth, 22 * p.depth);
        ctx.fillStyle = "#fef3c7";
        ctx.font = "bold " + Math.round(12 * p.depth) + "px system-ui,sans-serif";
        ctx.textAlign = "center";
        ctx.fillText("★ ALL ABOARD · 4", p.x, p.y + 37 * p.depth);
      }
    }
    /* pond1: parked submarine at dock / last EXIT */
    var anyInSub = frogs.some(function (f) { return f.inSub; });
    var CsubGone = global.FroggiesCanon;
    if (!anyInSub && !(CsubGone && CsubGone.isPermaGone && CsubGone.isPermaGone("submarine"))) {
      var Csub = global.FroggiesCanon;
      var dock = (Csub && Csub.SUB_DOCK) ? Csub.SUB_DOCK : { x: 2900, y: 1240 };
      var parkS = Csub && Csub.vehiclePos ? Csub.vehiclePos("submarine", dock.x, dock.y) : dock;
      var ps = project(parkS.x, parkS.y, camX, camY, vw, vh);
      drawSubmarine(ctx, ps.x, ps.y, -Math.PI / 2, ps.depth, false, 0, "#38bdf8", { inWater: true, sub: 0.2, wakePhase: 0 });
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
    for (i = 0; i < (world.bubbles || []).length; i++) {
      var b = world.bubbles[i];
      var bp = project(b.x, b.y, camX, camY, vw, vh);
      var ba = clamp(b.life * 1.6, 0, 0.75);
      ctx.strokeStyle = "rgba(220, 245, 255, " + ba + ")";
      ctx.lineWidth = 1.6;
      ctx.beginPath();
      ctx.arc(bp.x, bp.y - (1 - b.life) * 40, b.r * bp.depth, 0, Math.PI * 2);
      ctx.stroke();
      ctx.fillStyle = "rgba(180, 230, 255, " + (ba * 0.25) + ")";
      ctx.fill();
    }
    for (i = 0; i < world.sparks.length; i++) {
      var k = world.sparks[i];
      var kp = project(k.x, k.y, camX, camY, vw, vh);
      ctx.fillStyle = "hsla(" + k.hue + ", 90%, 60%, " + clamp(k.life * 2, 0, 1) + ")";
      ctx.fillRect(kp.x, kp.y - (0.4 - k.life) * 20, 3, 3);
    }
    /* mech3: big tank missiles · mechgun1: cyan omnigun bolts */
    for (i = 0; i < (world.shells || []).length; i++) {
      var shd = world.shells[i];
      var shp = project(shd.x, shd.y, camX, camY, vw, vh);
      var sha = clamp((shd.life / (shd.maxLife || 1.55)) * 1.2, 0.35, 1);
      var sz = (shd.size != null ? shd.size : 2.4) * shp.depth;
      var omni = !!shd.omnigun;
      ctx.save();
      ctx.translate(shp.x, shp.y);
      ctx.rotate(shd.ang || 0);
      /* exhaust / muzzle trail */
      ctx.fillStyle = omni ? ("rgba(34, 211, 238, " + (sha * 0.6) + ")") : ("rgba(251, 146, 60, " + (sha * 0.55) + ")");
      ctx.beginPath();
      ctx.ellipse(-10 * sz, 0, 10 * sz, 3.2 * sz, 0, 0, Math.PI * 2);
      ctx.fill();
      /* fat body */
      ctx.fillStyle = omni ? ("rgba(244, 114, 182, " + sha + ")") : ("rgba(253, 224, 71, " + sha + ")");
      ctx.fillRect(-6 * sz, -3.6 * sz, 22 * sz, 7.2 * sz);
      ctx.fillStyle = omni ? ("rgba(103, 232, 249, " + sha + ")") : ("rgba(248, 113, 113, " + sha + ")");
      ctx.beginPath();
      ctx.moveTo(16 * sz, 0);
      ctx.lineTo(8 * sz, -4.2 * sz);
      ctx.lineTo(8 * sz, 4.2 * sz);
      ctx.closePath();
      ctx.fill();
      ctx.fillStyle = omni ? ("rgba(254, 240, 255, " + (sha * 0.9) + ")") : ("rgba(254, 243, 199, " + (sha * 0.85) + ")");
      ctx.beginPath();
      ctx.arc(-2 * sz, 0, 3.2 * sz, 0, Math.PI * 2);
      ctx.fill();
      ctx.restore();
    }
    /* mech3: explosion rings */
    for (i = 0; i < (world.booms || []).length; i++) {
      var bmd = world.booms[i];
      var bmp = project(bmd.x, bmd.y, camX, camY, vw, vh);
      var bma = clamp(bmd.life / (bmd.maxLife || 0.6), 0, 1);
      ctx.beginPath();
      ctx.ellipse(bmp.x, bmp.y, bmd.r * bmp.depth, bmd.r * 0.42 * bmp.depth, 0, 0, Math.PI * 2);
      ctx.strokeStyle = "rgba(251, 146, 60, " + (bma * 0.95) + ")";
      ctx.lineWidth = 4 * bmp.depth;
      ctx.stroke();
      ctx.beginPath();
      ctx.ellipse(bmp.x, bmp.y, bmd.r * 0.55 * bmp.depth, bmd.r * 0.22 * bmp.depth, 0, 0, Math.PI * 2);
      ctx.fillStyle = "rgba(254, 240, 138, " + (bma * 0.45) + ")";
      ctx.fill();
      ctx.beginPath();
      ctx.arc(bmp.x, bmp.y - 6 * bmp.depth, 5 * bmp.depth * (0.6 + bma), 0, Math.PI * 2);
      ctx.fillStyle = "rgba(248, 113, 113, " + (bma * 0.8) + ")";
      ctx.fill();
    }
    /* polish5: pond ripple rings */
    for (i = 0; i < (world.ripples || []).length; i++) {
      var rp = world.ripples[i];
      var rpp = project(rp.x, rp.y, camX, camY, vw, vh);
      var ra = clamp(rp.life * 0.7, 0, 0.55);
      ctx.beginPath();
      ctx.ellipse(rpp.x, rpp.y, rp.r * rpp.depth, rp.r * 0.38 * rpp.depth, 0, 0, Math.PI * 2);
      ctx.strokeStyle = "rgba(186, 230, 253, " + ra + ")";
      ctx.lineWidth = 2;
      ctx.stroke();
      ctx.beginPath();
      ctx.ellipse(rpp.x, rpp.y, rp.r * 0.65 * rpp.depth, rp.r * 0.24 * rpp.depth, 0, 0, Math.PI * 2);
      ctx.strokeStyle = "rgba(224, 242, 254, " + (ra * 0.7) + ")";
      ctx.lineWidth = 1.2;
      ctx.stroke();
    }
    /* polish5: ambient pollen / fireflies */
    for (i = 0; i < (world.ambient || []).length; i++) {
      var am = world.ambient[i];
      var amp = project(am.x, am.y, camX, camY, vw, vh);
      if (amp.x < -20 || amp.x > vw + 20 || amp.y < -20 || amp.y > vh + 20) continue;
      if (am.kind === "firefly") {
        var glow = 0.35 + 0.65 * Math.abs(Math.sin(am.phase));
        ctx.fillStyle = "rgba(250, 204, 21, " + (glow * 0.85) + ")";
        ctx.beginPath();
        ctx.arc(amp.x, amp.y, (am.r + glow) * amp.depth, 0, Math.PI * 2);
        ctx.fill();
        ctx.fillStyle = "rgba(254, 243, 199, " + (glow * 0.5) + ")";
        ctx.beginPath();
        ctx.arc(amp.x, amp.y, (am.r * 0.5) * amp.depth, 0, Math.PI * 2);
        ctx.fill();
      } else {
        ctx.fillStyle = "rgba(254, 249, 195, 0.55)";
        ctx.beginPath();
        ctx.ellipse(amp.x, amp.y, am.r * amp.depth, am.r * 0.55 * amp.depth, am.phase, 0, Math.PI * 2);
        ctx.fill();
      }
    }
    /* polish5: hotspot enter sparkles */
    for (i = 0; i < (world.sparkles || []).length; i++) {
      var sk = world.sparkles[i];
      var skp = project(sk.x, sk.y, camX, camY, vw, vh);
      ctx.fillStyle = "hsla(" + sk.hue + ", 95%, 70%, " + clamp(sk.life * 2.2, 0, 1) + ")";
      ctx.beginPath();
      ctx.arc(skp.x, skp.y, sk.r * skp.depth, 0, Math.PI * 2);
      ctx.fill();
    }
    /* polish9: Optimus kit visual punch */
    for (i = 0; i < (world.kitFx || []).length; i++) {
      var kf = world.kitFx[i];
      var kfp = project(kf.x, kf.y, camX, camY, vw, vh);
      var ka = clamp(kf.life * 1.6, 0, 1);
      var grow = 1 + (kf.age || 0) * 2.2;
      if (kf.kind === "rocket" || kf.kind === "afterburners") {
        ctx.fillStyle = "rgba(251, 146, 60, " + (ka * 0.55) + ")";
        ctx.beginPath();
        ctx.moveTo(kfp.x, kfp.y - 8 * kfp.depth);
        ctx.lineTo(kfp.x - 10 * kfp.depth * grow, kfp.y + 22 * kfp.depth * grow);
        ctx.lineTo(kfp.x + 10 * kfp.depth * grow, kfp.y + 22 * kfp.depth * grow);
        ctx.closePath(); ctx.fill();
        ctx.fillStyle = "rgba(254, 243, 199, " + (ka * 0.8) + ")";
        ctx.beginPath();
        ctx.arc(kfp.x, kfp.y - 4 * kfp.depth, 6 * kfp.depth, 0, Math.PI * 2);
        ctx.fill();
      } else if (kf.kind === "hover") {
        ctx.strokeStyle = "rgba(125, 211, 252, " + ka + ")";
        ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.ellipse(kfp.x, kfp.y + 6, 28 * kfp.depth * grow, 10 * kfp.depth, 0, 0, Math.PI * 2);
        ctx.stroke();
      } else if (kf.kind === "drone") {
        ctx.fillStyle = "rgba(148, 163, 184, " + ka + ")";
        ctx.fillRect(kfp.x - 8 * kfp.depth, kfp.y - 28 * kfp.depth * grow, 16 * kfp.depth, 8 * kfp.depth);
        ctx.strokeStyle = "rgba(226, 232, 240, " + ka + ")";
        ctx.lineWidth = 2;
        ctx.beginPath();
        ctx.moveTo(kfp.x - 14 * kfp.depth, kfp.y - 24 * kfp.depth * grow);
        ctx.lineTo(kfp.x + 14 * kfp.depth, kfp.y - 24 * kfp.depth * grow);
        ctx.stroke();
      } else { /* map ping */
        ctx.strokeStyle = "rgba(251, 191, 36, " + ka + ")";
        ctx.lineWidth = 2.5;
        ctx.beginPath();
        ctx.arc(kfp.x, kfp.y, (18 + grow * 22) * kfp.depth, 0, Math.PI * 2);
        ctx.stroke();
        ctx.beginPath();
        ctx.arc(kfp.x, kfp.y, (8 + grow * 10) * kfp.depth, 0, Math.PI * 2);
        ctx.stroke();
      }
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
    var sd = p.depth;
    /* polish4: destination pad — chevrons, lights, big rings */
    ctx.fillStyle = near ? "rgba(56,189,248,0.5)" : "rgba(71,85,105,0.62)";
    ctx.beginPath();
    ctx.ellipse(p.x, p.y, STARSHIP.padR * 0.72 * sd, STARSHIP.padR * 0.28 * sd, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.strokeStyle = near ? "#fbbf24" : "#38bdf8";
    ctx.lineWidth = 4;
    ctx.stroke();
    ctx.beginPath();
    ctx.ellipse(p.x, p.y, STARSHIP.padR * 0.48 * sd, STARSHIP.padR * 0.18 * sd, 0, 0, Math.PI * 2);
    ctx.strokeStyle = "rgba(125, 211, 252, 0.9)";
    ctx.lineWidth = 2.4;
    ctx.stroke();
    ctx.beginPath();
    ctx.ellipse(p.x, p.y, STARSHIP.padR * 0.22 * sd, STARSHIP.padR * 0.08 * sd, 0, 0, Math.PI * 2);
    ctx.strokeStyle = "#fef3c7";
    ctx.lineWidth = 1.6;
    ctx.stroke();
    /* Approach chevrons on pad */
    ctx.fillStyle = "rgba(251, 191, 36, 0.75)";
    for (var ch = 0; ch < 3; ch++) {
      var cy = p.y + (18 + ch * 14) * sd;
      ctx.beginPath();
      ctx.moveTo(p.x, cy - 8 * sd);
      ctx.lineTo(p.x + 12 * sd, cy + 4 * sd);
      ctx.lineTo(p.x - 12 * sd, cy + 4 * sd);
      ctx.closePath();
      ctx.fill();
    }
    /* Pad edge lights */
    for (var li = 0; li < 8; li++) {
      var ang = (li / 8) * Math.PI * 2 + Date.now() / 900;
      var lx = p.x + Math.cos(ang) * STARSHIP.padR * 0.62 * sd;
      var ly = p.y + Math.sin(ang) * STARSHIP.padR * 0.24 * sd;
      ctx.fillStyle = li % 2 ? "#fbbf24" : "#38bdf8";
      ctx.beginPath(); ctx.arc(lx, ly, 2.6 * sd, 0, Math.PI * 2); ctx.fill();
    }
    /* Starship silhouette — taller + fins */
    ctx.fillStyle = "#e2e8f0";
    ctx.beginPath();
    ctx.moveTo(p.x, p.y - 68 * sd);
    ctx.lineTo(p.x + 18 * sd, p.y - 8 * sd);
    ctx.lineTo(p.x - 18 * sd, p.y - 8 * sd);
    ctx.closePath();
    ctx.fill();
    ctx.fillStyle = "#64748b";
    ctx.fillRect(p.x - 10 * sd, p.y - 8 * sd, 20 * sd, 22 * sd);
    ctx.fillStyle = "#94a3b8";
    ctx.beginPath();
    ctx.moveTo(p.x - 10 * sd, p.y + 4 * sd);
    ctx.lineTo(p.x - 26 * sd, p.y + 14 * sd);
    ctx.lineTo(p.x - 10 * sd, p.y + 10 * sd);
    ctx.closePath(); ctx.fill();
    ctx.beginPath();
    ctx.moveTo(p.x + 10 * sd, p.y + 4 * sd);
    ctx.lineTo(p.x + 26 * sd, p.y + 14 * sd);
    ctx.lineTo(p.x + 10 * sd, p.y + 10 * sd);
    ctx.closePath(); ctx.fill();
    /* Exhaust glow */
    ctx.fillStyle = near ? "rgba(251, 146, 60, 0.7)" : "rgba(249, 115, 22, 0.45)";
    ctx.beginPath();
    ctx.ellipse(p.x, p.y + 18 * sd, 10 * sd, 6 * sd, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = "#f97316";
    ctx.beginPath();
    ctx.ellipse(p.x + 28 * sd, p.y - 28 * sd, 9 * sd, 8 * sd, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = "#0f172a";
    ctx.font = "bold " + Math.round(10 * sd) + "px system-ui,sans-serif";
    ctx.textAlign = "center";
    ctx.fillText("Spotty", p.x + 28 * sd, p.y - 26 * sd);
    var destY = p.y + 48 * sd;
    ctx.fillStyle = "rgba(15, 23, 42, 0.75)";
    ctx.fillRect(p.x - 70 * sd, destY - 14, 140 * sd, 28);
    ctx.strokeStyle = near ? "#fbbf24" : "#38bdf8";
    ctx.lineWidth = 2;
    ctx.strokeRect(p.x - 70 * sd, destY - 14, 140 * sd, 28);
    ctx.fillStyle = near ? "#fef3c7" : "#7dd3fc";
    ctx.font = "bold " + Math.round(13 * sd) + "px system-ui,sans-serif";
    ctx.fillText("★ STARSHIP · SPACE", p.x, destY + 4);
  }

  function mech1000Pos() {
    var C = global.FroggiesCanon;
    if (C && C.COMPOUND && C.COMPOUND.mech1000) return C.COMPOUND.mech1000;
    return { x: 340, y: 2420, stories: 1000 };
  }

  function nearMech1000(frogs, r) {
    var m = mech1000Pos();
    r = r || 160;
    if (!frogs) return null;
    for (var i = 0; i < frogs.length; i++) {
      var f = frogs[i];
      if (!f.local) continue;
      if (Math.hypot(f.x - m.x, f.y - m.y) < r) return { frog: f, mech: m, d: Math.hypot(f.x - m.x, f.y - m.y) };
    }
    return null;
  }

  function zoneSignsList() {
    var C = global.FroggiesCanon;
    if (C && C.ZONE_SIGNS) return C.ZONE_SIGNS;
    return [
      { id: "house", label: "HOUSE", x: 380, y: 1630, approach: 460, color: "#fbbf24" },
      { id: "track", label: "TRACK", x: 2780, y: 2270, approach: 560, color: "#a8a29e" },
      { id: "pond", label: "POND", x: 3160, y: 670, approach: 500, color: "#67e8f9" },
      { id: "garage", label: "GARAGE", x: 940, y: 1660, approach: 300, color: "#fdba74" },
      { id: "starship", label: "STARSHIP", x: 360, y: 320, approach: 340, color: "#fde68a" },
    ];
  }

  function drawZoneSigns(ctx, frogs, camX, camY, vw, vh) {
    var me = null;
    for (var i = 0; i < (frogs || []).length; i++) {
      if (frogs[i].local) { me = frogs[i]; break; }
    }
    if (!me) return;
    var C = global.FroggiesCanon;
    var signs = zoneSignsList();
    for (var si = 0; si < signs.length; si++) {
      var z = signs[si];
      var a = C && C.zoneSignAlpha ? C.zoneSignAlpha(z, me.x, me.y) : 0;
      if (a <= 0.04) continue;
      var p = project(z.x, z.y, camX, camY, vw, vh);
      /* polish10: lift signs high above frogs; smaller, less opaque */
      var bw = Math.min(160, 88 + a * 40) * Math.min(1.15, p.depth + 0.12);
      var bh = 20 + a * 3;
      var bx = p.x;
      var by = p.y - 110 * p.depth;
      ctx.save();
      ctx.globalAlpha = Math.min(0.72, a * 0.85);
      ctx.fillStyle = "rgba(15, 23, 42, 0.55)";
      ctx.fillRect(bx - bw * 0.5, by - bh * 0.5, bw, bh);
      ctx.strokeStyle = z.color || "#fbbf24";
      ctx.lineWidth = 1.5;
      ctx.strokeRect(bx - bw * 0.5, by - bh * 0.5, bw, bh);
      ctx.fillStyle = z.color || "#fef3c7";
      ctx.font = "bold " + Math.round(11 + a * 2) + "px Segoe UI, system-ui, sans-serif";
      ctx.textAlign = "center";
      ctx.textBaseline = "middle";
      ctx.fillText(z.label, bx, by);
      ctx.restore();
    }
  }

  function drawMiniMap(ctx, frogs, vw, vh) {
    /* polish10 + mobile1: smaller map on narrow; peek-sized on phone so D-pad/HUD stay clear */
    var narrow = isNarrowView(vw, vh);
    var landscapePhone = vw <= 900 && vh <= 480;
    if (landscapePhone) return; /* vertical space too precious — hide */
    var mw = narrow
      ? Math.min(78, Math.max(64, vw * 0.18))
      : Math.min(118, Math.max(96, vw * 0.12));
    var mh = mw * (MAP_H / MAP_W);
    var pad = narrow ? 6 : 10;
    var ox = pad + (narrow ? 4 : 6);
    var oy = pad + (narrow ? 58 : 86);
    ctx.save();
    ctx.globalAlpha = narrow ? 0.58 : 0.72;
    ctx.fillStyle = "rgba(15, 23, 42, 0.72)";
    ctx.strokeStyle = "rgba(251, 191, 36, 0.55)";
    ctx.lineWidth = 1.5;
    ctx.fillRect(ox, oy, mw, mh);
    ctx.strokeRect(ox, oy, mw, mh);
    function mx(x) { return ox + (x / MAP_W) * mw; }
    function my(y) { return oy + (y / MAP_H) * mh; }
    /* landmarks */
    var marks = [
      { x: 380, y: 1630, c: "#fbbf24", r: 3.2 }, /* HOUSE */
      { x: 2780, y: 2270, c: "#a8a29e", r: 3.2 }, /* TRACK */
      { x: 3160, y: 670, c: "#67e8f9", r: 3.2 }, /* POND */
      { x: 940, y: 1660, c: "#fdba74", r: 2.6 }, /* GARAGE */
      { x: 360, y: 320, c: "#fde68a", r: 3.5 }, /* STARSHIP */
    ];
    for (var i = 0; i < marks.length; i++) {
      var m = marks[i];
      ctx.fillStyle = m.c;
      ctx.beginPath();
      ctx.arc(mx(m.x), my(m.y), m.r, 0, Math.PI * 2);
      ctx.fill();
    }
    for (var fi = 0; fi < (frogs || []).length; fi++) {
      var f = frogs[fi];
      if (!f || !f.alive) continue;
      ctx.fillStyle = f.color || "#fff";
      ctx.beginPath();
      ctx.arc(mx(f.x), my(f.y), f.local ? 4.2 : 3, 0, Math.PI * 2);
      ctx.fill();
      if (f.local) {
        ctx.strokeStyle = "#fff";
        ctx.lineWidth = 1.4;
        ctx.beginPath();
        ctx.arc(mx(f.x), my(f.y), 6, 0, Math.PI * 2);
        ctx.stroke();
      }
    }
    ctx.fillStyle = "rgba(254,243,199,0.9)";
    ctx.font = "bold " + (narrow ? 8 : 9) + "px Segoe UI, system-ui, sans-serif";
    ctx.textAlign = "left";
    ctx.fillText("MAP", ox + 5, oy + (narrow ? 10 : 11));
    ctx.restore();
  }

  function drawSpears(ctx, world, camX, camY, vw, vh) {
    if (!world || !world.spears) return;
    for (var si = 0; si < world.spears.length; si++) {
      var sp = world.spears[si];
      var p0 = project(sp.x, sp.y, camX, camY, vw, vh);
      var len = (sp.len || 160) * p0.depth;
      var fade = Math.max(0, sp.life / Math.max(0.01, sp.maxLife || 0.28));
      var ex = p0.x + Math.cos(sp.ang) * len;
      var ey = p0.y + Math.sin(sp.ang) * len * 0.55;
      ctx.save();
      ctx.globalAlpha = 0.35 + 0.65 * fade;
      ctx.strokeStyle = "#fef3c7";
      ctx.lineWidth = Math.max(3, 5 * p0.depth);
      ctx.lineCap = "round";
      ctx.beginPath();
      ctx.moveTo(p0.x, p0.y - 30 * p0.depth);
      ctx.lineTo(ex, ey - 40 * p0.depth);
      ctx.stroke();
      ctx.fillStyle = "#fbbf24";
      ctx.beginPath();
      ctx.arc(ex, ey - 40 * p0.depth, Math.max(3, 6 * p0.depth), 0, Math.PI * 2);
      ctx.fill();
      ctx.restore();
    }
  }

  function drawMechWowTip(ctx, frogs, camX, camY, vw, vh) {
    var hit = nearMech1000(frogs, 170);
    if (!hit) return;
    var m = hit.mech;
    var p = project(m.x, m.y, camX, camY, vw, vh);
    var pulse = 0.85 + 0.15 * Math.sin(Date.now() / 280);
    var bw = Math.min(280, 160 + (1 - hit.d / 170) * 80) * Math.min(1.25, p.depth + 0.2);
    var bx = p.x;
    var by = p.y - 200 * p.depth;
    ctx.fillStyle = "rgba(15, 23, 42, " + (0.82 * pulse) + ")";
    ctx.fillRect(bx - bw * 0.5, by - 28, bw, 44);
    ctx.strokeStyle = "#fbbf24";
    ctx.lineWidth = 2.4;
    ctx.strokeRect(bx - bw * 0.5, by - 28, bw, 44);
    ctx.fillStyle = "#fef3c7";
    ctx.font = "bold " + Math.round(13 * Math.min(1.2, p.depth + 0.25)) + "px Segoe UI, system-ui, sans-serif";
    ctx.textAlign = "center";
    ctx.fillText("★ WOW · Rexy 1000-story mech", bx, by - 6);
    ctx.fillStyle = "#fde68a";
    ctx.font = "bold " + Math.round(11 * Math.min(1.15, p.depth + 0.2)) + "px Segoe UI, system-ui, sans-serif";
    ctx.fillText("scale tease · won't fit in the garage", bx, by + 12);
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
    drawRanchHouse(ctx, camX, camY, vw, vh, world, frogs);
    drawTrack(ctx, camX, camY, vw, vh, t);
    drawToys(ctx, world, camX, camY, vw, vh);
    drawStarshipPad(ctx, camX, camY, vw, vh, nearHot && nearHot.id === "starship");
    drawFx(ctx, world, camX, camY, vw, vh);
    drawParkedTrucks(ctx, world, frogs, camX, camY, vw, vh);
    /* padfix1: pads after trucks / clipped orange compound — before frogs so boardable craft stay visible */
    drawParkedAir(ctx, world, frogs, camX, camY, vw, vh);

    for (var hi = 0; hi < world.hotspots.length; hi++) {
      var h = world.hotspots[hi];
      var hideTruck = h.kind === "truck" && frogs.some(function (f) {
        return f.local && f.inTruck && (f.truckId === h.id || (f.truckMode === "shared" && h.mode === "shared"));
      });
      var hideSub = (h.kind === "submarine" || h.id === "submarine") && frogs.some(function (f) { return f.inSub; });
      var hideAir = h.kind === "heli" || h.kind === "drone" || h.id === "heli" || h.id === "drone";
      if (hideTruck || hideSub || hideAir) continue;
      drawHotspot(ctx, h, camX, camY, vw, vh, nearHot && nearHot.id === h.id);
    }

    var sorted = frogs.slice().sort(function (a, b) { return a.y - b.y; });
    for (var fi = 0; fi < sorted.length; fi++) {
      drawFroggy(ctx, sorted[fi], camX, camY, vw, vh, frogs);
    }

    /* polish6/mech1: Rexy 1000-story mech wow-scale tip when approached */
    drawSpears(ctx, world, camX, camY, vw, vh);
    drawMechWowTip(ctx, frogs, camX, camY, vw, vh);
    /* polish7: zone signs + mini-map */
    drawZoneSigns(ctx, frogs, camX, camY, vw, vh);
    drawMiniMap(ctx, frogs, vw, vh);

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
    hotspotTakenByOther: hotspotTakenByOther,
    updateFish: updateFish,
    updatePushables: updatePushables,
    updateFx: updateFx,
    tickDrive: tickDrive,
    scareFishies: scareFishies,
    spawnDust: spawnDust,
    spawnBubbles: spawnBubbles,
    spawnSplash: spawnSplash,
    spawnSparks: spawnSparks,
    spawnTankShell: spawnTankShell,
    spawnMechGunShell: spawnMechGunShell,
    tryMechSpear: tryMechSpear,
    spawnBoom: spawnBoom,
    blastWreckProps: blastWreckProps,
    blastOmnigunKill: blastOmnigunKill,
    spawnRipple: spawnRipple,
    spawnSparkle: spawnSparkle,
    spawnKitFx: spawnKitFx,
    TRACK_GATE: TRACK_GATE,
    moveEntity: moveEntity,
    tickHubAI: tickHubAI,
    boardTruck: boardTruck,
    boardMech: boardMech,
    boardSub: boardSub,
    boardAir: boardAir,
    boardAirResult: boardAirResult,
    project: project,
    getViewScale: getViewScale,
    setViewScaleUser: setViewScaleUser,
    adjustViewScale: adjustViewScale,
    nearMech1000: nearMech1000,
    mech1000Pos: mech1000Pos,
    drawZoneSigns: drawZoneSigns,
    drawMiniMap: drawMiniMap,
    zoneSignsList: zoneSignsList,
    render: render,
  };
})(typeof window !== "undefined" ? window : globalThis);
