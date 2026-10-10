/*!
 * Similarize "Mountain" mark animation (v3)
 * The word "Similarize" -> "imi" is highlighted -> the m between the two i's
 * (two people) grows into the outline of a mountain -> they climb it at full
 * size and shake hands at the summit -> the mountain folds back into the m as
 * they leap home, landing as the Similarize logo.
 *
 * Pure SVG + JS. No dependencies, no images, no fonts.
 *
 * Usage:
 *   <div id="mark" style="width:420px;aspect-ratio:1250/560"></div>
 *   <script src="similarize-mark.js"></script>
 *   <script>
 *     const anim = SimilarizeMark.create(document.getElementById('mark'), {
 *       autoplay: true, pageBg: '#ffffff', speed: 1, clickToReplay: true,
 *       onDone: () => {}
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
  var HILITE = [238, 94, 94];
  var BRAND = [144, 0, 0];

  // ---------- geometry (logo frame: 1536 x 1536) ----------
  var AXIS = 770.5;
  var DISC = { cx: 768, cy: 768, r: 765 };
  var SW = 82;
  var BASE = 1141;            // ground line
  var HOME_X = 192;           // stem x of the left i
  var M_PATH = 'M397 1100 V795 A186.75 186.75 0 0 1 770.5 795 V1100 ' +
               'M770.5 795 A186.75 186.75 0 0 1 1144 795 V1100';
  // left half of the mountain outline (centre line), base -> summit
  var MTN_LEFT = [[-350, 1100], [-60, 700], [50, 770], [320, 430], [420, 500], [AXIS, 60]];
  var DISC_GROW = 1.96;
  var CY_SHIFT = 370;         // the widened disc sits higher so the mountain is centred        // how much the disc (and the view) widens for the mountain

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
    wake: [3.0, 3.5], grow: [3.45, 5.05], stepOut: [3.6, 5.05],
    climb: [5.05, 8.2], five: [8.25, 9.05], jump: [8.95, 9.75],
    fade: [9.8, 10.9], end: 11.6
  };

  // ---------- helpers ----------
  function clamp(x, a, b) { return x < a ? a : x > b ? b : x; }
  function lerp(a, b, t) { return a + (b - a) * t; }
  function prog(t, r) { return clamp((t - r[0]) / (r[1] - r[0]), 0, 1); }
  function ease(x) { return x < 0.5 ? 4 * x * x * x : 1 - Math.pow(-2 * x + 2, 3) / 2; }
  function easeOut(x) { return 1 - Math.pow(1 - x, 3); }
  function easeIO2(x) { return x < 0.5 ? 2 * x * x : 1 - Math.pow(-2 * x + 2, 2) / 2; }
  function smooth(a, b, x) { var t = clamp((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t); }
  function mixC(a, b, t) { return 'rgb(' + Math.round(lerp(a[0], b[0], t)) + ',' + Math.round(lerp(a[1], b[1], t)) + ',' + Math.round(lerp(a[2], b[2], t)) + ')'; }
  function mixA(a, b, t) { return [lerp(a[0], b[0], t), lerp(a[1], b[1], t), lerp(a[2], b[2], t)]; }
  function hex(c) { var m = /^#?([0-9a-f]{6})$/i.exec(c || ''); if (!m) return WHITE; var n = parseInt(m[1], 16); return [n >> 16 & 255, n >> 8 & 255, n & 255]; }
  function el(tag, attrs, parent) { var e = document.createElementNS(NS, tag); for (var k in attrs) e.setAttribute(k, attrs[k]); if (parent) parent.appendChild(e); return e; }
  function f(n) { return Math.round(n * 10) / 10; }
  function add(a, b) { return [a[0] + b[0], a[1] + b[1]]; }
  function sc(a, k) { return [a[0] * k, a[1] * k]; }
  function lp(a, b, t) { return [lerp(a[0], b[0], t), lerp(a[1], b[1], t)]; }
  function rot(p, o, ang) { var s = Math.sin(ang), c = Math.cos(ang), x = p[0] - o[0], y = p[1] - o[1]; return [o[0] + x * c - y * s, o[1] + x * s + y * c]; }
  function norm(v) { var l = Math.hypot(v[0], v[1]) || 1; return [v[0] / l, v[1] / l]; }

  // resample a polyline into n points evenly spaced by arc length
  function resample(pts, n) {
    var L = [0];
    for (var i = 1; i < pts.length; i++) L.push(L[i - 1] + Math.hypot(pts[i][0] - pts[i - 1][0], pts[i][1] - pts[i - 1][1]));
    var tot = L[L.length - 1], out = [], j = 1;
    for (var k = 0; k < n; k++) {
      var d = tot * k / (n - 1);
      while (j < L.length - 1 && L[j] < d) j++;
      var seg = (d - L[j - 1]) / ((L[j] - L[j - 1]) || 1);
      out.push(lp(pts[j - 1], pts[j], clamp(seg, 0, 1)));
    }
    return out;
  }

  // the m as one continuous centre line: left leg, hump, valley, hump, right leg
  function mLine() {
    var p = [], i, R = 186.75;
    for (i = 0; i <= 20; i++) p.push([397, 1100 - 305 * i / 20]);
    for (i = 1; i <= 60; i++) { var a = Math.PI + Math.PI * i / 60; p.push([583.75 + R * Math.cos(a), 795 + R * Math.sin(a)]); }
    for (i = 1; i <= 60; i++) { var b = Math.PI + Math.PI * i / 60; p.push([957.25 + R * Math.cos(b), 795 + R * Math.sin(b)]); }
    for (i = 1; i <= 20; i++) p.push([1144, 795 + 305 * i / 20]);
    return p;
  }
  function mountainLine() {
    var p = MTN_LEFT.slice();
    for (var i = MTN_LEFT.length - 2; i >= 0; i--) p.push([2 * AXIS - MTN_LEFT[i][0], MTN_LEFT[i][1]]);
    return p;
  }
  var NPTS = 241;
  var M_PTS = resample(mLine(), NPTS);
  var MT_PTS = resample(mountainLine(), NPTS);

  // walkable outer edge of the left half of the mountain (u = arc length from the base)
  var EDGE = (function () {
    var c = MTN_LEFT, segN = [], i;
    for (i = 0; i < c.length - 1; i++) { var d = norm([c[i + 1][0] - c[i][0], c[i + 1][1] - c[i][1]]); segN.push([d[1], -d[0]]); }
    var pts = [], ns = [];
    for (i = 0; i < c.length; i++) {
      var n = i === 0 ? segN[0] : i === c.length - 1 ? segN[i - 1] : norm(add(segN[i - 1], segN[i]));
      var dot = i === 0 || i === c.length - 1 ? 1 : Math.max(0.5, n[0] * segN[i][0] + n[1] * segN[i][1]);
      pts.push(add(c[i], sc(n, (SW / 2) / dot))); ns.push(n);
    }
    pts[0] = [pts[0][0], BASE]; // flush with the ground
    var L = [0];
    for (i = 1; i < pts.length; i++) L.push(L[i - 1] + Math.hypot(pts[i][0] - pts[i - 1][0], pts[i][1] - pts[i - 1][1]));
    return { pts: pts, segN: segN, L: L };
  })();
  function mtnSurface(u) {
    var L = EDGE.L, i = 1;
    u = clamp(u, 0, L[L.length - 1]);
    while (i < L.length - 1 && L[i] < u) i++;
    var k = (u - L[i - 1]) / (L[i] - L[i - 1]);
    var P = lp(EDGE.pts[i - 1], EDGE.pts[i], k);
    // smooth the normal across corners
    var N = EDGE.segN[i - 1], W = 70;
    if (u - L[i - 1] < W && i > 1) N = norm(lp(EDGE.segN[i - 2], N, 0.5 + 0.5 * (u - L[i - 1]) / W));
    else if (L[i] - u < W && i < L.length - 1) N = norm(lp(EDGE.segN[i], N, 0.5 + 0.5 * (L[i] - u) / W));
    return { P: P, N: N };
  }
  function ground(u) { return { P: [u, BASE], N: [0, -1] }; }
  // where they stand to shake hands: root ~140 left of the axis
  var U_TOP = (function () { for (var u = 0; u < 4000; u += 2) { if (mtnSurface(u).P[0] > AXIS - 150) return u; } return 0; })();
  var START_X = EDGE.pts[0][0] - 55; // where they stand at the foot of the mountain

  // 2-bone IK
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
    var defs = el('defs', {}, svg);
    var uid = 'sm' + Math.random().toString(36).slice(2, 8);

    var disc = el('circle', { cx: DISC.cx, cy: DISC.cy, r: DISC.r }, svg);
    var gLetters = el('g', { fill: 'none', 'stroke-width': SW, 'stroke-linecap': 'round', 'stroke-linejoin': 'round' }, svg);
    var letterEls = LETTERS.map(function (L) {
      var g = el('g', {}, gLetters);
      el('path', { d: L.d }, g);
      if (L.dot) el('circle', { cx: L.dot[0], cy: L.dot[1], r: L.dot[2], stroke: 'none', fill: 'rgb(0,139,255)' }, g);
      return { L: L, g: g };
    });
    var lineAttrs = { fill: 'none', 'stroke-width': SW, 'stroke-linecap': 'round', 'stroke-linejoin': 'round' };
    var mEl = el('path', Object.assign({ d: M_PATH }, lineAttrs), svg);       // exact m (rest)
    var mtnEl = el('path', Object.assign({}, lineAttrs), svg);                 // morphing outline
    var stemEl = el('path', Object.assign({}, lineAttrs), svg);                // middle stem retracting

    function makeFigure(i, parent) {
      var g = el('g', { 'stroke-linecap': 'round', 'stroke-linejoin': 'round', fill: 'none' }, parent);
      var o = {}, w = {};
      ['torso', 'legA', 'legB', 'armA', 'armB'].forEach(function (k) { o[k] = el('path', {}, g); });
      o.head = el('circle', { stroke: 'none' }, g);
      ['legB', 'armB', 'torso', 'legA', 'armA'].forEach(function (k) { w[k] = el('path', {}, g); });
      w.head = el('circle', { stroke: 'none' }, g);
      var wedge = el('path', { stroke: 'none' }, g);
      var clipId = uid + 'c' + i, cp = el('clipPath', { id: clipId }, defs);
      return { g: g, o: o, w: w, wedge: wedge, clipId: clipId, clipC: el('circle', {}, cp) };
    }
    var figL = makeFigure(0, svg), figR = makeFigure(1, svg);
    // the resting logo, drawn at the widened scale so it can fade in over the mountain scene
    var logoG = el('g', { opacity: 0, transform: 'translate(' + DISC.cx + ' ' + (DISC.cy - CY_SHIFT) + ') scale(' + DISC_GROW + ') translate(' + (-DISC.cx) + ' ' + (-DISC.cy) + ')' }, svg);
    var logoM = el('path', Object.assign({ d: M_PATH }, lineAttrs), logoG);
    var logoL = makeFigure(2, logoG), logoR = makeFigure(3, logoG);
    // little burst where the hands meet
    var burst = el('g', { stroke: 'white', 'stroke-width': 26, 'stroke-linecap': 'round', fill: 'none', opacity: 0 }, svg);
    var rays = [];
    for (var ri = 0; ri < 7; ri++) rays.push(el('path', {}, burst));

    function restState(t) {
      return { s: 1, face: 1, alive: 0, amp: 0, climb: 0, lean: 0, root: [HOME_X, BASE], u: HOME_X, surf: ground, stride: 150, t: t, five: 0, cheer: 0, tuck: 0, slap: 0 };
    }

    // hand-over-hand grip position (arc length on the mountain) for one hand
    var GRIP = 300, LEAD = 640;
    function gripU(u, off) {
      var x = u / GRIP + off, n = Math.floor(x), fr = x - n;
      var mv = smooth(0.55, 1, fr);
      return { u: (n + mv - off) * GRIP + LEAD, lift: Math.sin(Math.PI * mv) };
    }

    // state of the LEFT person; the right one is its mirror image
    function pose(t) {
      var st = restState(t);
      st.alive = easeOut(prog(t, T.wake));
      var hop = Math.sin(Math.PI * prog(t, [3.0, 3.35])) * 60;
      if (t < T.climb[0]) {
        // stand at home, then glide back to the foot of the growing mountain
        var p = easeIO2(prog(t, T.stepOut));
        st.u = lerp(HOME_X, START_X, p);
        st.amp = 0.35 * Math.sin(Math.PI * prog(t, T.stepOut));
        st.stride = 240;
        st.root = [st.u, BASE - hop - Math.abs(Math.sin(st.u / 120 * Math.PI)) * 14 * st.amp];
        return st;
      }
      // climb, then stand at the top
      var pc = easeIO2(prog(t, T.climb));
      var u = lerp(0, U_TOP, pc);
      var q = mtnSurface(u);
      var up = smooth(0.82, 1, pc);                   // straighten up on arrival
      st.surf = mtnSurface; st.u = u;
      st.climb = smooth(0.0, 0.08, pc) * (1 - up);
      // lean the body toward the slope (angle of the wall's normal from vertical)
      st.lean = Math.abs(Math.atan2(q.N[0], -q.N[1])) * 0.45 * st.climb;
      var stand = [START_X, BASE];
      var onM = add(q.P, sc(q.N, 60 * st.climb));
      st.root = lp(stand, onM, smooth(0, 0.06, pc));
      st.amp = t < T.climb[1] ? 1 : 0;
      // high five, then a jump with a cheer
      st.five = smooth(T.five[0], T.five[0] + 0.3, t) * (1 - smooth(T.five[1] - 0.25, T.five[1], t));
      st.slap = Math.sin(Math.PI * prog(t, [T.five[0] + 0.3, T.five[0] + 0.5]));
      var pj = prog(t, T.jump);
      st.cheer = smooth(0, 0.25, pj) * (1 - 0.5 * smooth(0.8, 1, pj));
      var air = Math.sin(Math.PI * smooth(0.15, 0.75, pj));
      var crouch = Math.sin(Math.PI * smooth(0, 0.18, pj)) * 30 + Math.sin(Math.PI * smooth(0.72, 0.92, pj)) * 24;
      st.root = [st.root[0], st.root[1] - air * 150 + crouch];
      st.tuck = air;
      st.groundY = q.P[1];
      return st;
    }

    function drawFigure(fig, st, mirror, bg, wedgeAmt, headR, whiteC, outlineOn) {
      var X = function (p) { return mirror ? [2 * AXIS - p[0], p[1]] : p; };
      var s = st.s, a = st.alive, rootP = st.root;
      var w = SW * s, wl = lerp(SW, 62, a) * s;
      var amp = st.amp * a, climb = st.climb * a;
      var ax = rot([0, -1], [0, 0], st.lean * a);      // body axis
      var at = function (d) { return add(rootP, sc(ax, d * s)); };
      var hip = at(241), shoulder = at(470), neck = at(532);
      var headC = add(at(691), [10 * s * a, 0]);
      var rest = add(rootP, [0, -41 * s]);
      // legs
      function foot(sign) {
        var p;
        if (st.surf === mtnSurface) {
          // on the wall the legs trail and push softly in time with the pulls
          var g = gripU(st.u, sign > 0 ? 0 : 0.5);
          var fu = st.u - 30 + (g.u - LEAD - st.u) * 0.35 * climb + sign * 10;
          var q = mtnSurface(fu);
          p = add(q.P, sc(q.N, wl / 2 + g.lift * 22 * climb));
          if (climb < 1) { var standP = [rootP[0] + sign * 26 * s, (st.groundY || BASE) - wl / 2]; p = lp(standP, p, climb); }
          if (st.tuck > 0) p = lp(p, add(hip, [sign * 40 * s, 170 * s]), st.tuck * 0.7);
        } else {
          var fu2 = st.u + sign * (st.stride / 4) * Math.sin(st.u / st.stride * Math.PI * 2) * amp;
          p = [fu2, BASE - wl / 2 - Math.max(0, sign * Math.cos(st.u / st.stride * Math.PI * 2)) * 24 * amp];
        }
        return lp(rest, p, a);
      }
      var fA = foot(1), fB = foot(-1);
      var legA = ik(hip, fA, 120 * s, 120 * s, -1, a), legB = ik(hip, fB, 120 * s, 120 * s, -1, a);
      // arms
      var restHand = add(rootP, [0, -260 * s]);
      function hand(sign) {
        var h = add(shoulder, [18 * s * a - sign * 20 * s * amp, 238 * s]);
        if (st.surf === mtnSurface) {
          var g = gripU(st.u, sign > 0 ? 0.5 : 0);
          var cq = mtnSurface(g.u);
          var grip = add(cq.P, sc(cq.N, wl / 2 + g.lift * 60));
          h = lp(h, grip, climb);
        }
        if (sign > 0 && st.five > 0) h = lp(h, [AXIS - 8 - st.slap * 10, shoulder[1] - 235 * s], st.five);
        if (st.cheer > 0) h = lp(h, add(shoulder, [sign * 130 * s, -200 * s]), st.cheer);
        var wave = Math.sin(Math.PI * prog(st.t, [3.05, 3.5]));
        if (sign > 0 && wave > 0) h = lp(h, add(shoulder, [70 * s, -170 * s + Math.sin(st.t * 28) * 30 * s]), wave);
        return lp(restHand, h, a);
      }
      var hA = hand(1), hB = hand(-1);
      var armA = ik(shoulder, hA, 125 * s, 125 * s, 1, a), armB = ik(shoulder, hB, 125 * s, 125 * s, 1, a);

      function pth(pts) { return 'M' + pts.map(function (p) { p = X(p); return f(p[0]) + ' ' + f(p[1]); }).join(' L'); }
      var D = {
        torso: pth([neck, hip]),
        legA: pth([hip, legA.j, legA.c]), legB: pth([hip, legB.j, legB.c]),
        armA: pth([shoulder, armA.j, armA.c]), armB: pth([shoulder, armB.j, armB.c])
      };
      var ow = outlineOn * 14 * s;
      var bgC = 'rgb(' + bg.map(Math.round).join(',') + ')';
      for (var k in D) {
        var wk = k === 'torso' ? w : wl;
        fig.o[k].setAttribute('d', D[k]); fig.o[k].setAttribute('stroke', bgC); fig.o[k].setAttribute('stroke-width', f(wk + 2 * ow)); fig.o[k].setAttribute('opacity', ow > 0.2 ? 1 : 0);
        fig.w[k].setAttribute('d', D[k]); fig.w[k].setAttribute('stroke', whiteC); fig.w[k].setAttribute('stroke-width', f(wk));
      }
      var hc = X(headC), r = headR * s;
      fig.o.head.setAttribute('cx', f(hc[0])); fig.o.head.setAttribute('cy', f(hc[1])); fig.o.head.setAttribute('r', f(r + ow)); fig.o.head.setAttribute('fill', bgC); fig.o.head.setAttribute('opacity', ow > 0.2 ? 1 : 0);
      fig.w.head.setAttribute('cx', f(hc[0])); fig.w.head.setAttribute('cy', f(hc[1])); fig.w.head.setAttribute('r', f(r)); fig.w.head.setAttribute('fill', whiteC);
      var fx = mirror ? -1 : 1;
      if (wedgeAmt > 0.01) {
        var apexA = 38 * Math.PI / 180, a1 = 2 * Math.PI / 180, a2 = 30 * Math.PI / 180, mid = (a1 + a2) / 2;
        var A1 = lerp(mid, a1, wedgeAmt), A2 = lerp(mid, a2, wedgeAmt);
        var dd = 32 * s * lerp(2.6, 1, wedgeAmt);
        var ap = [hc[0] + fx * Math.cos(apexA) * dd, hc[1] + Math.sin(apexA) * dd];
        var R = r * 1.5;
        var p1 = [hc[0] + fx * Math.cos(A1) * R, hc[1] + Math.sin(A1) * R];
        var p2 = [hc[0] + fx * Math.cos(A2) * R, hc[1] + Math.sin(A2) * R];
        fig.wedge.setAttribute('d', 'M' + f(ap[0]) + ' ' + f(ap[1]) + ' L' + f(p1[0]) + ' ' + f(p1[1]) + ' L' + f(p2[0]) + ' ' + f(p2[1]) + ' Z');
        fig.wedge.setAttribute('fill', bgC);
        fig.wedge.setAttribute('clip-path', 'url(#' + fig.clipId + ')');
        fig.clipC.setAttribute('cx', f(hc[0])); fig.clipC.setAttribute('cy', f(hc[1])); fig.clipC.setAttribute('r', f(r + 0.5));
        fig.wedge.setAttribute('opacity', 1);
      } else fig.wedge.setAttribute('opacity', 0);
    }

    function render(t) {
      var done = t >= T.fade[1];
      var mo = done ? 0 : ease(prog(t, T.grow));
      var k = lerp(1, DISC_GROW, mo);
      var pc = ease(prog(t, [1.95, 3.0]));
      var cy = DISC.cy - CY_SHIFT * (k - 1) / (DISC_GROW - 1);
      var vbLogo = [DISC.cx - (DISC.cx - VB_LOGO[0]) * k, cy - (DISC.cy - VB_LOGO[1]) * k, VB_LOGO[2] * k, VB_LOGO[3] * k];
      svg.setAttribute('viewBox', VB_WORD.map(function (v, i) { return f(lerp(v, vbLogo[i], pc)); }).join(' '));

      var pIn = easeOut(prog(t, T.wordIn));
      var pHi = ease(prog(t, T.hilite));
      var pDeep = ease(prog(t, [2.0, 3.0]));
      var discCol = mixA(HILITE, BRAND, pDeep);
      disc.setAttribute('fill', 'rgb(' + discCol.map(Math.round).join(',') + ')');
      disc.setAttribute('opacity', f(pHi * 100) / 100);
      disc.setAttribute('r', f(DISC.r * k)); disc.setAttribute('cy', f(cy));
      disc.setAttribute('transform', 'translate(' + DISC.cx + ' ' + f(cy) + ') scale(' + f(lerp(0.55, 1, easeOut(pHi)) * 1000) / 1000 + ') translate(' + (-DISC.cx) + ' ' + f(-cy) + ')');
      var bg = mixA(pageBg, discCol, pHi);

      var pEx = prog(t, T.exit);
      letterEls.forEach(function (o, i) {
        var delay = o.L.side < 0 ? 0 : (i - 1) * 0.04;
        var pe = ease(clamp((pEx - delay) / (1 - 0.3), 0, 1));
        o.g.setAttribute('transform', 'translate(' + f(o.L.side * 900 * pe) + ' ' + f((1 - pIn) * 40) + ')');
        o.g.setAttribute('opacity', f(pIn * (1 - pe) * 1000) / 1000);
        o.g.setAttribute('stroke', 'rgb(0,139,255)');
      });

      var imiC = mixC(BLUE, WHITE, pHi);
      var headR = lerp(66, 90, pHi);
      var dy = 'translate(0 ' + f((1 - pIn) * 40) + ')';
      // crossfade from the mountain scene to the resting logo
      var pf = done ? 0 : easeIO2(prog(t, T.fade));
      var sceneA = 1 - pf;
      if (mo <= 0.0005) {
        mEl.setAttribute('opacity', pIn); mEl.setAttribute('stroke', imiC); mEl.setAttribute('transform', dy);
        mtnEl.setAttribute('opacity', 0); stemEl.setAttribute('opacity', 0);
      } else {
        mEl.setAttribute('opacity', 0);
        var pts = M_PTS.map(function (p, i) { return lp(p, MT_PTS[i], mo); });
        mtnEl.setAttribute('d', 'M' + pts.map(function (p) { return f(p[0]) + ' ' + f(p[1]); }).join(' L'));
        mtnEl.setAttribute('stroke', imiC); mtnEl.setAttribute('opacity', sceneA);
        var v = pts[(NPTS - 1) / 2];
        var stemLen = 305 * (1 - smooth(0, 0.55, mo));
        stemEl.setAttribute('d', 'M' + f(v[0]) + ' ' + f(v[1]) + ' L' + f(v[0]) + ' ' + f(v[1] + stemLen));
        stemEl.setAttribute('stroke', imiC); stemEl.setAttribute('opacity', stemLen > 2 ? sceneA : 0);
      }

      var st = done ? restState(t) : pose(t);
      var outlineOn = done ? 0 : smooth(T.wake[0], T.wake[1], t);
      [figL, figR].forEach(function (fig) { fig.g.setAttribute('opacity', f(pIn * sceneA * 1000) / 1000); fig.g.setAttribute('transform', dy); });
      drawFigure(figL, st, false, bg, pHi, headR, imiC, outlineOn);
      drawFigure(figR, st, true, bg, pHi, headR, imiC, outlineOn);

      // high-five burst
      var pb = prog(t, [T.five[0] + 0.32, T.five[0] + 0.72]);
      if (pb > 0 && pb < 1 && !done) {
        var c = [AXIS, st.root[1] - 470 - 250];
        burst.setAttribute('opacity', f((1 - pb) * 1000) / 1000);
        burst.setAttribute('stroke', imiC);
        rays.forEach(function (r, i) {
          var ang = -Math.PI / 2 + (i - 3) * 0.45;
          var r0 = 70 + 90 * easeOut(pb), r1 = r0 + 70 * (1 - pb) + 10;
          r.setAttribute('d', 'M' + f(c[0] + Math.cos(ang) * r0) + ' ' + f(c[1] + Math.sin(ang) * r0) + ' L' + f(c[0] + Math.cos(ang) * r1) + ' ' + f(c[1] + Math.sin(ang) * r1));
        });
      } else burst.setAttribute('opacity', 0);

      if (pf > 0) {
        logoG.setAttribute('opacity', f(pf * 1000) / 1000);
        logoM.setAttribute('stroke', imiC);
        var rs = restState(t);
        drawFigure(logoL, rs, false, bg, 1, 90, imiC, 0);
        drawFigure(logoR, rs, true, bg, 1, 90, imiC, 0);
      } else logoG.setAttribute('opacity', 0);
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
