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
  var MTN_LEFT = [[-350, 1100], [-60, 700], [50, 770], [320, 360], [420, 430], [AXIS, -150]];
  var DISC_GROW = 2.2;        // how much the disc (and the view) widens for the mountain

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
    climb: [5.05, 8.0], shake: [8.0, 9.5], ret: [9.55, 10.95],
    settle: [10.85, 11.35], end: 12.0
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

    function makeFigure(i) {
      var g = el('g', { 'stroke-linecap': 'round', 'stroke-linejoin': 'round', fill: 'none' }, svg);
      var o = {}, w = {};
      ['torso', 'legA', 'legB', 'armA', 'armB'].forEach(function (k) { o[k] = el('path', {}, g); });
      o.head = el('circle', { stroke: 'none' }, g);
      ['legB', 'armB', 'torso', 'legA', 'armA'].forEach(function (k) { w[k] = el('path', {}, g); });
      w.head = el('circle', { stroke: 'none' }, g);
      var wedge = el('path', { stroke: 'none' }, g);
      var clipId = uid + 'c' + i, cp = el('clipPath', { id: clipId }, defs);
      return { g: g, o: o, w: w, wedge: wedge, clipId: clipId, clipC: el('circle', {}, cp) };
    }
    var figL = makeFigure(0), figR = makeFigure(1);

    // state of the LEFT person; the right one is its mirror image
    function pose(t) {
      var st = { s: 1, face: 1, shake: 0, pump: 0, climb: 0, amp: 0, surf: null, u: 0, stride: 150, free: 0, armsUp: 0, t: t };
      st.alive = t < T.settle[0] ? easeOut(prog(t, T.wake)) : 1 - ease(prog(t, T.settle));
      var hop = Math.sin(Math.PI * prog(t, [3.0, 3.35])) * 60;
      if (t < T.climb[0]) {
        // stand at home, then back away as the mountain grows
        var p = easeIO2(prog(t, T.stepOut));
        st.surf = ground; st.u = lerp(HOME_X, START_X, p); st.amp = t > T.stepOut[0] && t < T.stepOut[1] ? 1 : 0;
        st.stride = 170;
        st.root = [st.u, BASE - hop];
      } else if (t < T.ret[0]) {
        // climb to the top
        var pc = easeIO2(prog(t, T.climb));
        // first a short walk from the stand point onto the slope
        var startU = -1;
        if (pc < 0.0001) startU = -1;
        var u = lerp(0, U_TOP, pc);
        st.surf = mtnSurface; st.u = u; st.stride = 300;
        st.amp = t < T.climb[1] ? 1 : 0;
        var q = mtnSurface(u);
        st.climb = smooth(0.5, 0.8, Math.abs(q.N[0])) * (1 - smooth(0.85, 1, pc));
        var stand = [START_X, BASE];
        var onM = add(q.P, sc(q.N, 46 * st.climb));
        st.root = lp(stand, onM, smooth(0, 0.06, pc));
        var ps = prog(t, T.shake);
        st.shake = smooth(0, 0.22, ps) * (1 - smooth(0.92, 1, ps));
        st.pump = Math.sin(Math.PI * 2 * 2 * smooth(0.25, 0.75, ps)) * (ps > 0.25 && ps < 0.75 ? 1 : 0);
      } else {
        // leap home while the mountain folds back into the m
        var pr = prog(t, T.ret), e = easeIO2(pr);
        // ride the shrinking mountain down (scaled with the disc), then land at home
        var top = mtnSurface(U_TOP).P, home = [HOME_X, BASE];
        var kk = lerp(1, DISC_GROW, 1 - ease(prog(t, [T.ret[0], T.ret[1] - 0.3]))) / DISC_GROW;
        var ride = [DISC.cx + (top[0] - DISC.cx) * kk, DISC.cy + (top[1] - DISC.cy) * kk];
        st.root = lp(ride, home, easeIO2(smooth(0, 0.8, pr)));
        st.root[1] -= Math.sin(Math.PI * smooth(0, 0.8, pr)) * 40;
        st.free = 1; st.armsUp = Math.sin(Math.PI * smooth(0, 0.8, pr));
        // land with a soft squash
        st.root[1] += Math.sin(Math.PI * prog(t, [10.9, 11.25])) * 18;
      }
      return st;
    }

    function drawFigure(fig, st, mirror, bg, wedgeAmt, headR, whiteC, outlineOn) {
      var X = function (p) { return mirror ? [2 * AXIS - p[0], p[1]] : p; };
      var s = st.s, a = st.alive, rootP = st.root;
      var w = SW * s, wl = lerp(SW, 62, a) * s;
      var stride = st.stride * s;
      var phase = (st.u / stride) * Math.PI * 2;
      var amp = st.amp * a, climb = st.climb, dirF = st.face;
      var hip = add(rootP, [0, -241 * s]);
      var neck = add(rootP, [0, -532 * s]);
      var shoulder = add(rootP, [0, -470 * s]);
      var lean = 0.06 * amp * (1 - climb) * Math.sign(st.amp) * (st.surf === ground ? -1 : 1) + 0.12 * climb * amp;
      neck = rot(neck, hip, lean); shoulder = rot(shoulder, hip, lean);
      var headC = rot(add(rootP, [10 * s * st.face, -691 * s]), hip, lean);
      var rest = add(rootP, [0, -41 * s]);
      function foot(sign) {
        var p;
        if (st.surf && !st.free) {
          var fu = st.u + sign * (stride / 4) * Math.sin(phase) * amp;
          var q = st.surf(fu);
          var lift = Math.max(0, sign * Math.cos(phase)) * 40 * s * amp;
          p = add(q.P, sc(q.N, wl / 2 + lift));
        } else {
          // mid-air: knees tucked, legs a little apart
          p = add(hip, [sign * 55 * s, 150 * s]);
        }
        return lp(rest, p, a);
      }
      var fA = foot(1), fB = foot(-1);
      var legA = ik(hip, fA, 120 * s, 120 * s, -dirF, a), legB = ik(hip, fB, 120 * s, 120 * s, -dirF, a);
      var restHand = add(rootP, [0, -260 * s]);
      function hand(sign) {
        var h = add(shoulder, [-sign * Math.sin(phase) * 90 * s * amp + 18 * s * dirF * a, 238 * s]);
        if (st.surf === mtnSurface && climb > 0) {
          var cq = mtnSurface(st.u + 560 * s + sign * Math.sin(phase) * 90 * s);
          h = lp(h, add(cq.P, sc(cq.N, wl / 2)), climb * clamp(amp * 2, 0, 1));
        }
        if (sign > 0) h = lp(h, [AXIS + 8, shoulder[1] + 150 * s + st.pump * 26 * s], st.shake);
        if (st.armsUp > 0) h = lp(h, add(shoulder, [sign * 150 * s, -170 * s]), st.armsUp);
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
      var fx = (mirror ? -1 : 1) * st.face;
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

    // how far the m has turned into the mountain (0 = m, 1 = mountain)
    function morphAmt(t) {
      if (t < T.ret[0]) return ease(prog(t, T.grow));
      return 1 - ease(prog(t, [T.ret[0], T.ret[1] - 0.3]));
    }

    function render(t) {
      var mo = morphAmt(t);
      // the disc and the view widen together with the mountain
      var k = lerp(1, DISC_GROW, mo);
      var pc = ease(prog(t, [1.95, 3.0]));
      var vbLogo = [DISC.cx - (DISC.cx - VB_LOGO[0]) * k, DISC.cy - (DISC.cy - VB_LOGO[1]) * k, VB_LOGO[2] * k, VB_LOGO[3] * k];
      var vb = VB_WORD.map(function (v, i) { return f(lerp(v, vbLogo[i], pc)); });
      svg.setAttribute('viewBox', vb.join(' '));

      var pIn = easeOut(prog(t, T.wordIn));
      var pHi = ease(prog(t, T.hilite));
      var pDeep = ease(prog(t, [2.0, 3.0]));
      var discCol = mixA(HILITE, BRAND, pDeep);
      disc.setAttribute('fill', 'rgb(' + discCol.map(Math.round).join(',') + ')');
      disc.setAttribute('opacity', f(pHi * 100) / 100);
      disc.setAttribute('r', f(DISC.r * k));
      disc.setAttribute('transform', 'translate(' + DISC.cx + ' ' + DISC.cy + ') scale(' + f(lerp(0.55, 1, easeOut(pHi)) * 1000) / 1000 + ') translate(' + (-DISC.cx) + ' ' + (-DISC.cy) + ')');
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
      var dy = 'translate(0 ' + f((1 - pIn) * 40) + ')';
      if (mo <= 0.0005) {
        mEl.setAttribute('opacity', pIn); mEl.setAttribute('stroke', imiC); mEl.setAttribute('transform', dy);
        mtnEl.setAttribute('opacity', 0); stemEl.setAttribute('opacity', 0);
      } else {
        mEl.setAttribute('opacity', 0);
        var pts = M_PTS.map(function (p, i) { return lp(p, MT_PTS[i], mo); });
        mtnEl.setAttribute('d', 'M' + pts.map(function (p) { return f(p[0]) + ' ' + f(p[1]); }).join(' L'));
        mtnEl.setAttribute('stroke', imiC); mtnEl.setAttribute('opacity', 1);
        var v = pts[(NPTS - 1) / 2];
        var stemLen = 305 * (1 - smooth(0, 0.55, mo));
        stemEl.setAttribute('d', 'M' + f(v[0]) + ' ' + f(v[1]) + ' L' + f(v[0]) + ' ' + f(v[1] + stemLen));
        stemEl.setAttribute('stroke', imiC); stemEl.setAttribute('opacity', stemLen > 2 ? 1 : 0);
      }

      var st = pose(t);
      var headR = lerp(66, 90, pHi);
      var outlineOn = smooth(T.wake[0], T.wake[1], t) * (1 - smooth(T.settle[0], T.settle[1], t));
      [figL, figR].forEach(function (fig) { fig.g.setAttribute('opacity', pIn); fig.g.setAttribute('transform', dy); });
      drawFigure(figL, st, false, bg, pHi, headR, imiC, outlineOn);
      drawFigure(figR, st, true, bg, pHi, headR, imiC, outlineOn);
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
