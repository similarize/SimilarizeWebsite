/*!
 * Similarize "Mountain Hug" mark animation
 * The word "Similarize" -> "imi" is highlighted -> the two i's (people) climb the
 * m (the mountain between them), meet at the top and shake hands, then
 * dissolve back into the Similarize logo.
 *
 * Pure SVG + JS. No dependencies, no images, no fonts.
 *
 * Usage:
 *   <div id="mark" style="width:420px;aspect-ratio:1250/560"></div>
 *   <script src="similarize-mark.js"></script>
 *   <script>
 *     const anim = SimilarizeMark.create(document.getElementById('mark'), {
 *       autoplay: true,        // start right away
 *       pageBg: '#ffffff',     // page colour behind the word
 *       speed: 1,              // 1 = normal (~11.5 s); 1.5 = faster
 *       clickToReplay: true,
 *       onDone: () => {}       // fires when the logo has settled
 *     });
 *     anim.play(); anim.seek(seconds); anim.duration;
 *   </script>
 */
(function (root) {
  'use strict';
  var NS = 'http://www.w3.org/2000/svg';

  // ---------- palette ----------
  var BLUE = [0, 139, 255];
  var WHITE = [255, 255, 255];
  var HILITE = [238, 94, 94];   // light red highlight disc (word phase)
  var BRAND = [144, 0, 0];      // final logo disc (#900000)
  var HEART = [255, 214, 222];

  // ---------- geometry (logo frame: 1536 x 1536) ----------
  var AXIS = 770.5;             // mirror line between the two people
  var DISC = { cx: 768, cy: 768, r: 765 };
  var SW = 82;                  // stroke weight of the letters
  var BASE = 1141;              // ground line (bottom of the letters)
  var HOME_X = 192;             // stem x of the left i
  var WALL_X = 356;             // outer face of the m's left leg
  var HUMP = { cx: 583, cy: 795, r: 227 }; // outer top surface of left hump
  var M_PATH = 'M397 1100 V795 A186.75 186.75 0 0 1 770.5 795 V1100 ' +
               'M770.5 795 A186.75 186.75 0 0 1 1144 795 V1100';

  // word letters (centre-line strokes, same weight as the logo)
  var LETTERS = [
    { id: 'S', side: -1, d: 'M-45 410 C-80 350 -140 322 -205 322 C-300 322 -370 380 -370 465 C-370 560 -290 600 -205 630 C-100 668 -20 720 -20 865 C-20 1010 -110 1100 -215 1100 C-300 1100 -365 1060 -395 1000' },
    { id: 'l', side: 1, d: 'M1557 330 V1100' },
    { id: 'a', side: 1, d: 'M1755 855 a245 245 0 1 0 490 0 a245 245 0 1 0 -490 0 M2245 610 V1100' },
    { id: 'r', side: 1, d: 'M2423 610 V1100 M2423 800 C2440 660 2560 600 2720 620' },
    { id: 'i3', side: 1, d: 'M2970 610 V1100', dot: [2970, 455, 55] },
    { id: 'z', side: 1, d: 'M3160 610 H3600 L3160 1100 H3610' },
    { id: 'e', side: 1, d: 'M3742 855 H4258 A258 258 0 1 0 4190 1030' }
  ];

  var VB_WORD = [-640, -40, 5100, 1616];
  var VB_LOGO = [-50, -50, 1636, 1636];

  // ---------- timeline (seconds) ----------
  var T = {
    wordIn: [0.0, 0.7], hilite: [0.7, 1.5], exit: [1.9, 3.0],
    wake: [3.0, 3.5], toWall: [3.5, 4.1], climb: [4.1, 6.4],
    shake: [6.3, 8.0], fade: [7.85, 8.85], end: 9.5
  };
  var U_HUG = 772;   // arc-length position where they meet
  var U_HOME = -164;
  var MS = 0.58;     // size of the people while on the mountain

  // ---------- helpers ----------
  function clamp(x, a, b) { return x < a ? a : x > b ? b : x; }
  function lerp(a, b, t) { return a + (b - a) * t; }
  function prog(t, r) { return clamp((t - r[0]) / (r[1] - r[0]), 0, 1); }
  function ease(x) { return x < 0.5 ? 4 * x * x * x : 1 - Math.pow(-2 * x + 2, 3) / 2; }
  function easeOut(x) { return 1 - Math.pow(1 - x, 3); }
  function smooth(a, b, x) { var t = clamp((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t); }
  function mixC(a, b, t) { return 'rgb(' + Math.round(lerp(a[0], b[0], t)) + ',' + Math.round(lerp(a[1], b[1], t)) + ',' + Math.round(lerp(a[2], b[2], t)) + ')'; }
  function mixA(a, b, t) { return [lerp(a[0], b[0], t), lerp(a[1], b[1], t), lerp(a[2], b[2], t)]; }
  function hex(c) { var m = /^#?([0-9a-f]{6})$/i.exec(c || ''); if (!m) return WHITE; var n = parseInt(m[1], 16); return [n >> 16 & 255, n >> 8 & 255, n & 255]; }
  function el(tag, attrs, parent) { var e = document.createElementNS(NS, tag); for (var k in attrs) e.setAttribute(k, attrs[k]); if (parent) parent.appendChild(e); return e; }
  function f(n) { return Math.round(n * 10) / 10; }
  function add(a, b) { return [a[0] + b[0], a[1] + b[1]]; }
  function sc(a, k) { return [a[0] * k, a[1] * k]; }
  function lp(a, b, t) { return [lerp(a[0], b[0], t), lerp(a[1], b[1], t)]; }

  // Surface of the mountain for the LEFT person (u = arc length).
  // u<0: ground in front of the m; 0..346: the m's outer wall; then the hump.
  function surface(u) {
    var P, N;
    var wallLen = BASE - HUMP.cy; // 346
    if (u <= 0) { P = [WALL_X + u, BASE]; N = [0, -1]; }
    else if (u <= wallLen) { P = [WALL_X, BASE - u]; N = [-1, 0]; }
    else {
      var th = Math.PI + (u - wallLen) / HUMP.r;
      N = [Math.cos(th), Math.sin(th)];
      P = [HUMP.cx + HUMP.r * N[0], HUMP.cy + HUMP.r * N[1]];
    }
    // soften the ground->wall corner
    if (u > -36 && u < 36) { var k = smooth(-36, 36, u); var n = [lerp(0, -1, k), lerp(-1, 0, k)]; var l = Math.hypot(n[0], n[1]); N = [n[0] / l, n[1] / l]; }
    return { P: P, N: N };
  }

  // 2-bone IK; bend: +1/-1 picks which side the joint pops to
  function ik(a, c, L1, L2, bend, amt) {
    var dx = c[0] - a[0], dy = c[1] - a[1], d = Math.hypot(dx, dy) || 1e-6;
    var maxd = L1 + L2 - 0.01;
    if (d > maxd) { c = [a[0] + dx / d * maxd, a[1] + dy / d * maxd]; dx = c[0] - a[0]; dy = c[1] - a[1]; d = maxd; }
    var x = (L1 * L1 - L2 * L2 + d * d) / (2 * d);
    var h = Math.sqrt(Math.max(0, L1 * L1 - x * x));
    var ux = dx / d, uy = dy / d;
    var j = [a[0] + ux * x - uy * h * bend, a[1] + uy * x + ux * h * bend];
    var mid = lp(a, c, L1 / (L1 + L2));
    return { j: lp(mid, j, amt), c: c };
  }

  function rot(p, o, ang) { var s = Math.sin(ang), c = Math.cos(ang), x = p[0] - o[0], y = p[1] - o[1]; return [o[0] + x * c - y * s, o[1] + x * s + y * c]; }

  // ---------- the animation ----------
  function create(container, opts) {
    opts = opts || {};
    var pageBg = hex(opts.pageBg || '#ffffff');
    var speed = opts.speed || 1;
    var reduce = opts.respectReducedMotion !== false && root.matchMedia && root.matchMedia('(prefers-reduced-motion: reduce)').matches;

    var svg = el('svg', { xmlns: NS, viewBox: VB_LOGO.join(' '), preserveAspectRatio: 'xMidYMid meet', role: 'img', 'aria-label': 'Similarize' });
    if (getComputedStyle(container).position === 'static') container.style.position = 'relative';
    svg.style.position = 'absolute'; svg.style.left = '0'; svg.style.top = '0'; svg.style.display = 'block'; svg.style.width = '100%'; svg.style.height = '100%'; svg.style.overflow = 'visible';
    container.appendChild(svg);

    var disc = el('circle', { cx: DISC.cx, cy: DISC.cy, r: DISC.r }, svg);
    var gLetters = el('g', { fill: 'none', 'stroke-width': SW, 'stroke-linecap': 'round', 'stroke-linejoin': 'round' }, svg);
    var letterEls = LETTERS.map(function (L) {
      var g = el('g', {}, gLetters);
      el('path', { d: L.d }, g);
      if (L.dot) el('circle', { cx: L.dot[0], cy: L.dot[1], r: L.dot[2], stroke: 'none', fill: 'rgb(0,139,255)' }, g);
      return { L: L, g: g };
    });
    var mEl = el('path', { d: M_PATH, fill: 'none', 'stroke-width': SW, 'stroke-linecap': 'round', 'stroke-linejoin': 'round' }, svg);

    function makeFigure() {
      var g = el('g', { 'stroke-linecap': 'round', 'stroke-linejoin': 'round', fill: 'none' }, svg);
      var o = {}, w = {};
      ['torso', 'legA', 'legB', 'armA', 'armB'].forEach(function (k) { o[k] = el('path', {}, g); });
      o.head = el('circle', { stroke: 'none' }, g);
      ['legB', 'armB', 'torso', 'legA', 'armA'].forEach(function (k) { w[k] = el('path', {}, g); });
      w.head = el('circle', { stroke: 'none' }, g);
      var wedge = el('path', { stroke: 'none' }, g);
      return { g: g, o: o, w: w, wedge: wedge };
    }
    // the logo's resting i's (fade in at the end) and the travelling people
    var homeL = makeFigure(), homeR = makeFigure();
    var figL = makeFigure(), figR = makeFigure();

    // pose for the LEFT person in the left frame; mirrored for the right one
    function pose(t) {
      var alive = easeOut(prog(t, T.wake));
      var u, moving = 0, s;
      var pGround = ease(prog(t, T.toWall)), pClimb = ease(prog(t, T.climb));
      if (t < T.toWall[1]) { u = lerp(U_HOME, 0, pGround); moving = t > T.toWall[0] ? 1 : 0; }
      else if (t < T.climb[1]) { u = lerp(0, U_HUG, pClimb); moving = 1; }
      else u = U_HUG;
      s = lerp(1, MS, ease(prog(t, T.toWall)));
      // handshake: reach in, pump twice, hold
      var ps = prog(t, T.shake);
      var shake = smooth(0, 0.22, ps);
      var pump = Math.sin(Math.PI * 2 * 2 * smooth(0.25, 0.75, ps)) * (ps > 0.25 && ps < 0.75 ? 1 : 0);
      var hop = Math.sin(Math.PI * prog(t, [3.0, 3.35])) * 60;
      return { u: u, s: s, alive: alive, moving: moving, face: 1, shake: shake, pump: pump, hop: hop };
    }
    var REST = { u: U_HOME, s: 1, alive: 0, moving: 0, face: 1, shake: 0, pump: 0, hop: 0, t: 0 };

    function drawFigure(fig, st, mirror, bg, wedgeAmt, headR, whiteC, outlineOn) {
      var X = function (p) { return mirror ? [2 * AXIS - p[0], p[1]] : p; };
      var s = st.s, a = st.alive;
      var sp = surface(st.u);
      var climb = smooth(0.55, 0.9, Math.abs(sp.N[0])) * smooth(-10, 40, st.u);
      var off = 56 * s * climb;
      var rootP = add(sp.P, sc(sp.N, off));
      rootP[1] -= st.hop * s * (st.u < -100 ? 1 : 0);
      var w = SW * s, wl = lerp(SW, 62, a) * s;
      var stride = 150 * s;
      var phase = (st.u / stride) * Math.PI * 2;
      var amp = st.moving * a;
      var dirF = st.face; // knees/elbows follow facing
      // body
      var hip = add(rootP, [0, -241 * s]);
      var neck = add(rootP, [0, -532 * s]);
      var shoulder = add(rootP, [0, -470 * s]);
      var lean = (0.06 * amp * (1 - climb) * dirF) + 0 * st.shake;
      neck = rot(neck, hip, lean); shoulder = rot(shoulder, hip, lean);
      var headC = rot(add(rootP, [10 * s * st.face, -691 * s]), hip, lean);
      // feet
      function foot(sign, ph) {
        var fu = st.u + sign * (stride / 4) * Math.sin(ph) * amp;
        var q = surface(fu);
        var lift = Math.max(0, sign * Math.cos(ph)) * 34 * s * amp;
        var p = add(q.P, sc(q.N, wl / 2 + lift));
        var rest = add(rootP, [0, -41 * s]);
        return lp(rest, p, a);
      }
      var fA = foot(1, phase), fB = foot(-1, phase);
      var legA = ik(hip, fA, 120 * s, 120 * s, -dirF, a), legB = ik(hip, fB, 120 * s, 120 * s, -dirF, a);
      // hands
      var restHand = add(rootP, [0, -260 * s]);
      function hand(sign) {
        var walkH = add(shoulder, [-sign * Math.sin(phase) * 90 * s * amp * dirF + 18 * s * dirF * a, 238 * s]);
        var cu = st.u + 600 * s + sign * Math.sin(phase) * 70 * s;
        var cq = surface(cu);
        var climbH = add(cq.P, sc(cq.N, wl / 2));
        var h = lp(walkH, climbH, climb * amp);
        // handshake: right hands meet in the middle, pump
        if (sign > 0) h = lp(h, [AXIS + 8, shoulder[1] + 150 * s + st.pump * 26 * s], st.shake);
        // waving on wake
        var wave = Math.sin(Math.PI * prog(st.t, [3.05, 3.5]));
        if (sign > 0 && wave > 0) h = lp(h, add(shoulder, [70 * s, -170 * s + Math.sin(st.t * 28) * 30 * s]), wave);
        return lp(restHand, h, a);
      }
      var hA = hand(1), hB = hand(-1);
      var armA = ik(shoulder, hA, 125 * s, 125 * s, dirF, a), armB = ik(shoulder, hB, 125 * s, 125 * s, dirF, a);

      function pth(pts) { return 'M' + pts.map(function (p) { p = X(p); return f(p[0]) + ' ' + f(p[1]); }).join(' L'); }
      var D = {
        torso: pth([neck, hip]),
        legA: pth([hip, legA.j, legA.c]), legB: pth([hip, legB.j, legB.c]),
        armA: pth([shoulder, armA.j, armA.c]), armB: pth([shoulder, armB.j, armB.c])
      };
      var ow = outlineOn * 12 * s;
      var bgC = 'rgb(' + bg.map(Math.round).join(',') + ')';
      for (var k in D) {
        var wk = k === 'torso' ? w : wl;
        fig.o[k].setAttribute('d', D[k]); fig.o[k].setAttribute('stroke', bgC); fig.o[k].setAttribute('stroke-width', f(wk + 2 * ow)); fig.o[k].setAttribute('opacity', ow > 0.2 ? 1 : 0);
        fig.w[k].setAttribute('d', D[k]); fig.w[k].setAttribute('stroke', whiteC); fig.w[k].setAttribute('stroke-width', f(wk));
      }
      var hc = X(headC), r = headR * s;
      fig.o.head.setAttribute('cx', f(hc[0])); fig.o.head.setAttribute('cy', f(hc[1])); fig.o.head.setAttribute('r', f(r + ow)); fig.o.head.setAttribute('fill', bgC); fig.o.head.setAttribute('opacity', ow > 0.2 ? 1 : 0);
      fig.w.head.setAttribute('cx', f(hc[0])); fig.w.head.setAttribute('cy', f(hc[1])); fig.w.head.setAttribute('r', f(r)); fig.w.head.setAttribute('fill', whiteC);
      // the notch "face" from the logo, pointing the way the person faces
      var fx = (mirror ? -1 : 1) * st.face;
      if (wedgeAmt > 0.01 && Math.abs(fx) > 0.05) {
        var apexA = 38 * Math.PI / 180, a1 = 2 * Math.PI / 180, a2 = 30 * Math.PI / 180;
        var spread = wedgeAmt;
        var mid = (a1 + a2) / 2;
        var A1 = lerp(mid, a1, spread), A2 = lerp(mid, a2, spread);
        var ap = [hc[0] + fx * Math.cos(apexA) * 32 * s * lerp(2.6, 1, spread), hc[1] + Math.sin(apexA) * 32 * s * lerp(2.6, 1, spread)];
        var R = r * 1.5;
        var p1 = [hc[0] + fx * Math.cos(A1) * R, hc[1] + Math.sin(A1) * R];
        var p2 = [hc[0] + fx * Math.cos(A2) * R, hc[1] + Math.sin(A2) * R];
        // clip the wedge to the head so it never cuts the partner
        fig.wedge.setAttribute('d', 'M' + f(ap[0]) + ' ' + f(ap[1]) + ' L' + f(p1[0]) + ' ' + f(p1[1]) + ' L' + f(p2[0]) + ' ' + f(p2[1]) + ' Z');
        fig.wedge.setAttribute('fill', bgC);
        fig.wedge.setAttribute('clip-path', 'url(#' + fig.clipId + ')');
        fig.clipC.setAttribute('cx', f(hc[0])); fig.clipC.setAttribute('cy', f(hc[1])); fig.clipC.setAttribute('r', f(r + 0.5));
        fig.wedge.setAttribute('opacity', 1);
      } else fig.wedge.setAttribute('opacity', 0);
      return { headC: hc, s: s };
    }

    // clip paths for the wedges
    var defs = el('defs', {}, svg);
    var uid = 'sm' + Math.random().toString(36).slice(2, 8);
    [figL, figR, homeL, homeR].forEach(function (fig, i) {
      fig.clipId = uid + 'c' + i;
      var cp = el('clipPath', { id: fig.clipId }, defs);
      fig.clipC = el('circle', {}, cp);
    });

    function render(t) {
      // camera
      var pc = ease(prog(t, [1.95, 3.0]));
      var vb = VB_WORD.map(function (v, i) { return f(lerp(v, VB_LOGO[i], pc)); });
      svg.setAttribute('viewBox', vb.join(' '));

      var pIn = easeOut(prog(t, T.wordIn));
      var pHi = ease(prog(t, T.hilite));
      var pDeep = ease(prog(t, [2.0, 3.0]));
      // disc
      var discCol = mixA(HILITE, BRAND, pDeep);
      disc.setAttribute('fill', 'rgb(' + discCol.map(Math.round).join(',') + ')');
      disc.setAttribute('opacity', f(pHi * 100) / 100);
      disc.setAttribute('transform', 'translate(' + DISC.cx + ' ' + DISC.cy + ') scale(' + f(lerp(0.55, 1, easeOut(pHi)) * 1000) / 1000 + ') translate(' + (-DISC.cx) + ' ' + (-DISC.cy) + ')');
      var bg = mixA(pageBg, discCol, pHi);

      // outer letters
      var pEx = prog(t, T.exit);
      letterEls.forEach(function (o, i) {
        var delay = o.L.side < 0 ? 0 : (i - 1) * 0.04;
        var pe = ease(clamp((pEx - delay) / (1 - 0.3), 0, 1));
        var dx = o.L.side * 900 * pe, dy = (1 - pIn) * 40;
        o.g.setAttribute('transform', 'translate(' + f(dx) + ' ' + f(dy) + ')');
        o.g.setAttribute('opacity', f(pIn * (1 - pe) * 1000) / 1000);
        o.g.setAttribute('stroke', mixC(BLUE, BLUE, 0));
      });

      // imi
      var imiC = mixC(BLUE, WHITE, pHi);
      mEl.setAttribute('stroke', imiC);
      mEl.setAttribute('opacity', pIn);
      mEl.setAttribute('transform', 'translate(0 ' + f((1 - pIn) * 40) + ')');
      var st = pose(t); st.t = t;
      var headR = lerp(66, 90, pHi);
      var outlineOn = smooth(T.wake[0], T.wake[1], t);
      // closing dissolve: the people fade on the summit as the logo's i's fade in at home
      var pf = ease(prog(t, T.fade));
      var started = t >= T.wake[0];
      [figL, figR].forEach(function (fig) { fig.g.setAttribute('opacity', f((started ? 1 - pf : pIn) * 1000) / 1000); fig.g.setAttribute('transform', 'translate(0 ' + f((1 - pIn) * 40 - pf * 30) + ')'); });
      [homeL, homeR].forEach(function (fig) { fig.g.setAttribute('opacity', f(pf * 1000) / 1000); });
      drawFigure(figL, st, false, bg, pHi, headR, imiC, outlineOn);
      drawFigure(figR, st, true, bg, pHi, headR, imiC, outlineOn);
      if (pf > 0) { REST.t = t; drawFigure(homeL, REST, false, bg, 1, 90, imiC, 0); drawFigure(homeR, REST, true, bg, 1, 90, imiC, 0); }

    }

    var api = { duration: T.end / speed, svg: svg };
    var raf = null, t0 = 0;
    api.seek = function (sec) { render(clamp(sec * speed, 0, T.end)); };
    api.stop = function () { if (raf) cancelAnimationFrame(raf); raf = null; };
    api.play = function () {
      api.stop();
      if (reduce) { render(T.end); if (opts.onDone) opts.onDone(); return; }
      t0 = performance.now();
      var tick = function (now) {
        var tt = (now - t0) / 1000 * speed;
        render(Math.min(tt, T.end));
        if (tt < T.end) raf = requestAnimationFrame(tick);
        else { raf = null; if (opts.onDone) opts.onDone(); if (opts.loop) setTimeout(api.play, 1200); }
      };
      raf = requestAnimationFrame(tick);
    };
    render(opts.autoplay === false ? T.end : 0);
    if (opts.clickToReplay !== false) { svg.style.cursor = 'pointer'; svg.addEventListener('click', function (e) { e.preventDefault(); api.play(); }); }
    if (opts.autoplay !== false) api.play();
    return api;
  }

  root.SimilarizeMark = { create: create };
})(typeof window !== 'undefined' ? window : this);
