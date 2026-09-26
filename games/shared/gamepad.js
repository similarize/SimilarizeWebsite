/*! SimilarizeGamepad — reusable Web Gamepad helper (Xbox / W3C standard mapping).
 *
 * Drop in with one script tag, then OR poll()/justPressed() into your input loop.
 * Do not fork this file per-game — keep mappings in the cab.
 *
 * Xbox / standard mapping:
 *   buttons: 0=A 1=B 2=X 3=Y 4=LB 5=RB 6=LT 7=RT 8=Back 9=Start
 *            12=D-pad Up 13=Down 14=Left 15=Right
 *   axes:    0/1 left stick X/Y, 2/3 right stick X/Y
 * Deadzone ~0.25 on sticks; LT/RT treat value>0.45 as pressed.
 * Poll every frame — Chrome needs a button press after connect before getGamepads is live.
 *
 * API (window.SimilarizeGamepad):
 *   start()                 — optional; auto-runs on load
 *   poll() / pollPad(i)     — snapshot { connected, lx,ly,rx,ry, a,b,x,y, lb,rb,lt,rt,
 *                             start,back, dpad:{u,d,l,r}, buttonsPressed:{…edges} }
 *   pressed(name, i?)       — held (uses last pollPad cache, or polls once)
 *   justPressed(name, i?)   — rising edge (uses last pollPad cache, or polls once)
 * Typical frame: const gp = SimilarizeGamepad.poll(); then read gp.* / buttonsPressed.
 */
(function (global) {
  "use strict";
  var DZ = 0.25;
  var NAMES = ["a", "b", "x", "y", "lb", "rb", "lt", "rt", "back", "start", "du", "dd", "dl", "dr"];
  var prev = [{}, {}, {}, {}];
  var last = [null, null, null, null];
  var hinted = false;

  function axis(v) {
    return Math.abs(v) < DZ ? 0 : v;
  }
  function btn(gp, i) {
    var b = gp.buttons && gp.buttons[i];
    if (!b) return false;
    return !!(b.pressed || (typeof b.value === "number" && b.value > 0.45));
  }
  function nowVal(out, n) {
    if (n === "du") return out.dpad.u;
    if (n === "dd") return out.dpad.d;
    if (n === "dl") return out.dpad.l;
    if (n === "dr") return out.dpad.r;
    return !!out[n];
  }
  function snap(gp, idx) {
    var out = {
      connected: !!gp,
      lx: 0, ly: 0, rx: 0, ry: 0,
      a: false, b: false, x: false, y: false,
      lb: false, rb: false, lt: false, rt: false,
      start: false, back: false,
      dpad: { u: false, d: false, l: false, r: false },
      buttonsPressed: {}
    };
    if (!gp) {
      prev[idx] = {};
      last[idx] = out;
      return out;
    }
    out.lx = axis(gp.axes[0] || 0);
    out.ly = axis(gp.axes[1] || 0);
    out.rx = axis(gp.axes[2] || 0);
    out.ry = axis(gp.axes[3] || 0);
    out.a = btn(gp, 0); out.b = btn(gp, 1); out.x = btn(gp, 2); out.y = btn(gp, 3);
    out.lb = btn(gp, 4); out.rb = btn(gp, 5); out.lt = btn(gp, 6); out.rt = btn(gp, 7);
    out.back = btn(gp, 8); out.start = btn(gp, 9);
    out.dpad.u = btn(gp, 12); out.dpad.d = btn(gp, 13);
    out.dpad.l = btn(gp, 14); out.dpad.r = btn(gp, 15);
    var p = prev[idx] || {};
    var edge = {};
    for (var i = 0; i < NAMES.length; i++) {
      var n = NAMES[i];
      var now = nowVal(out, n);
      edge[n] = !!(now && !p[n]);
    }
    out.buttonsPressed = edge;
    prev[idx] = {
      a: out.a, b: out.b, x: out.x, y: out.y, lb: out.lb, rb: out.rb,
      lt: out.lt, rt: out.rt, back: out.back, start: out.start,
      du: out.dpad.u, dd: out.dpad.d, dl: out.dpad.l, dr: out.dpad.r
    };
    last[idx] = out;
    return out;
  }
  function pads() {
    try { return navigator.getGamepads ? navigator.getGamepads() : []; }
    catch (e) { return []; }
  }
  function pollPad(i) {
    var idx = i | 0;
    if (idx < 0) idx = 0;
    while (prev.length <= idx) prev.push({});
    while (last.length <= idx) last.push(null);
    var list = pads();
    var gp = list && list[idx];
    return snap(gp || null, idx);
  }
  function poll() { return pollPad(0); }
  function cached(i) {
    var idx = i | 0;
    return last[idx] || pollPad(idx);
  }
  function pressed(name, i) {
    var s = cached(i);
    if (name === "u" || name === "du") return s.dpad.u;
    if (name === "d" || name === "dd") return s.dpad.d;
    if (name === "l" || name === "dl") return s.dpad.l;
    if (name === "r" || name === "dr") return s.dpad.r;
    return !!s[name];
  }
  function justPressed(name, i) {
    var s = cached(i);
    return !!(s.buttonsPressed && s.buttonsPressed[name]);
  }
  function showHint() {
    if (hinted || !document.body) return;
    hinted = true;
    var el = document.createElement("div");
    el.setAttribute("aria-live", "polite");
    el.textContent = "Controller connected · Xbox / standard layout";
    el.style.cssText = "position:fixed;left:50%;bottom:12px;transform:translateX(-50%);z-index:9999;padding:6px 12px;border-radius:8px;background:rgba(0,0,0,.72);color:#f3e2c4;font:12px/1.3 system-ui,sans-serif;pointer-events:none;opacity:1;transition:opacity .4s";
    document.body.appendChild(el);
    setTimeout(function () { el.style.opacity = "0"; setTimeout(function () { el.remove(); }, 500); }, 2800);
  }
  function start() {
    window.addEventListener("gamepadconnected", showHint);
  }
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", start);
  else start();

  global.SimilarizeGamepad = {
    start: start,
    poll: poll,
    pollPad: pollPad,
    pressed: pressed,
    justPressed: justPressed,
    DEADZONE: DZ
  };
})(typeof window !== "undefined" ? window : globalThis);
