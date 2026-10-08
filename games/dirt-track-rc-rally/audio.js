/* Dirt Track RC Rally — audio glue (reads the game's top-level state; no game logic changes). */
(function () {
  "use strict";
  var A = window.ArcadeAudio;
  if (!A) return;
  A.init({ mood: "rally", engine: "rc", button: "bottom-center" });
  var prev = new Map(), lastState = null, lastCount = null, raceOn = false;
  // top-level let/const of the game's classic script are visible to Function bodies (global lexical scope)
  var snap = new Function("try { return { state: state, countdown: countdown, paused: paused, players: players, standings: standings," +
    " MAXS: MAXS, creek: creek, inBox: inBox, muds: muds }; } catch (e) { return {}; }");
  var S = {};
  function g(name) { return S[name]; }
  function myCars() {
    var ps = g("players") || [];
    return ps.map(function (p) { return p && p.car; }).filter(Boolean);
  }
  function step() {
    requestAnimationFrame(step);
    S = snap() || {};
    var state = g("state"), countdown = g("countdown") || 0, paused = g("paused");
    // countdown beeps + GO
    if (state === "count" || (state === "race" && countdown > 0)) {
      var n = Math.ceil(countdown - 0.9);
      if (n !== lastCount) {
        if (n > 0 && n <= 3) A.play("countdown");
        else if (n <= 0 && lastCount > 0) A.play("go");
        lastCount = n;
      }
    } else lastCount = null;
    // finish fanfare
    if (state === "finish" && lastState !== "finish") {
      var st = (g("standings") || function () { return []; })(), mine = myCars(), best = 99;
      mine.forEach(function (c) { var i = st.indexOf(c); if (i >= 0 && i < best) best = i; });
      if (best === 0) A.play("win"); else if (best === 1) A.play("lap"); else A.play("lose");
    }
    lastState = state;
    raceOn = (state === "count" || state === "race" || state === "finish") && !paused;
    var cars = myCars();
    if (!raceOn || !cars.length) { A.engineStop(); prev.clear(); return; }
    var c0 = cars[0], maxs = g("MAXS") || 340;
    A.engine(c0.input ? Math.max(c0.input.gas || 0, (c0.input.brake || 0) * 0.5) : 0, (c0.speed || 0) / maxs, "rc");
    var creek = g("creek"), inBox = g("inBox"), muds = g("muds") || [];
    cars.forEach(function (c) {
      var p = prev.get(c);
      var inCreek = !!(creek && inBox && c.air <= 0 && inBox(creek, c.x, c.y, 6));
      var inMud = muds.some(function (m) { return c.air <= 0 && Math.hypot(m.x - c.x, m.y - c.y) < m.r; });
      var cur = { vx: c.vx, vy: c.vy, air: c.air, boost: c.boost, lap: c.lap, spin: c.spin, creek: inCreek, mud: inMud, finish: c.finish };
      if (p) {
        var sp = Math.hypot(c.vx, c.vy);
        if (c.boost > p.boost + 0.5) A.play("boost");
        if (p.air <= 0 && c.air > 0) A.play("jump");
        if (p.air > 0 && c.air <= 0) A.play("land", 0.9);
        if (c.spin > 0.5 && p.spin <= 0.5) A.play("whoosh", 1);
        if (inCreek && !p.creek && sp > 40) A.play("splash", 0.9);
        if (inMud && !p.mud && sp > 60) A.play("splash", 0.4);
        if (c.lap > p.lap) A.play("lap");
        if (p.finish == null && c.finish != null) A.play("checkpoint");
        var dv = Math.hypot(c.vx - p.vx, c.vy - p.vy);
        if (dv > 80 && c.air <= 0 && !(c.boost > p.boost + 0.5)) A.play("bump", Math.min(1.3, dv / 220));
      }
      prev.set(c, cur);
    });
  }
  // UI button clicks
  document.addEventListener("click", function (e) { var t = e.target; if (t && t.closest && t.closest("button") && t.id !== "arcade-mute") A.play("click"); }, true);
  requestAnimationFrame(step);
  window.__dtrcAudio = { step: step };
})();
