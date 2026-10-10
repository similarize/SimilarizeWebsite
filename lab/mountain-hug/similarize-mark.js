/*!
 * Similarize mark animation (v5, "Bridge")
 * "Similarize" draws on -> a disc blooms behind imi -> the camera settles on
 * the mark -> the m rises into a mountain between the two i's (two minds) ->
 * the mountain is removed, the i's lean in and a line of light connects them
 * -> the Similarize logo. About 3.2 s.
 *
 * Pure SVG + JS. No dependencies, no images, no fonts.
 * Usage: SimilarizeMark.create(el, { autoplay, pageBg, speed, clickToReplay, onDone })
 *        -> { play(), stop(), seek(s), duration, svg }
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
  // left half of the mountain (centre line), base -> summit; it fits inside the disc
  var MTN_LEFT = [[397, 1100], [628, 690], [680, 742], [AXIS, 430]];
  var HEAD = { y: 450, r: 90 }, STEM = [609, 1100];

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
    draw: [0.0, 0.75],     // word draws on
    disc: [0.55, 1.05],    // disc blooms behind imi
    focus: [0.85, 1.5],    // other letters part, camera settles on the mark
    rise: [1.3, 1.8],      // the m rises into a mountain between the two i's
    fall: [1.95, 2.55],    // the mountain is removed, the m settles
    bridge: [2.0, 2.75],   // a line of light connects the two minds
    lean: [1.95, 2.9],     // the i's lean in toward each other and settle
    end: 3.2
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

  function easeOutBack(x) { var c1 = 1.4, c3 = c1 + 1; return 1 + c3 * Math.pow(x - 1, 3) + c1 * Math.pow(x - 1, 2); }
  function spring(x) { return x >= 1 ? 1 : 1 - Math.exp(-6 * x) * Math.cos(9 * x); }
  function mountainLine() {
    var p = MTN_LEFT.slice();
    for (var i = MTN_LEFT.length - 2; i >= 0; i--) p.push([2 * AXIS - MTN_LEFT[i][0], MTN_LEFT[i][1]]);
    return p;
  }
  var NPTS = 241;
  var M_PTS = resample(mLine(), NPTS);
  var MT_PTS = resample(mountainLine(), NPTS);
  var X_L = 192, X_R = 2 * AXIS - 192;

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

    var line = { fill: 'none', 'stroke-width': SW, 'stroke-linecap': 'round', 'stroke-linejoin': 'round' };
    var disc = el('circle', { cx: DISC.cx, cy: DISC.cy, r: DISC.r }, svg);
    var gLetters = el('g', line, svg);
    var letterEls = LETTERS.map(function (L, i) {
      var g = el('g', {}, gLetters);
      var p = el('path', { d: L.d, pathLength: 1, stroke: 'rgb(0,139,255)' }, g);
      var dot = L.dot ? el('circle', { cx: L.dot[0], cy: L.dot[1], r: L.dot[2], stroke: 'none', fill: 'rgb(0,139,255)' }, g) : null;
      return { L: L, g: g, p: p, dot: dot, order: L.side < 0 ? 0 : i + 2 };
    });
    // the bridge of light sits behind the mark
    var bridge = el('path', { d: 'M' + X_L + ' ' + (HEAD.y - 110) + ' Q' + AXIS + ' 20 ' + X_R + ' ' + (HEAD.y - 110), pathLength: 1, fill: 'none', 'stroke-width': 34, 'stroke-linecap': 'round', stroke: '#fff', opacity: 0 }, svg);
    var mEl = el('path', Object.assign({ pathLength: 1 }, line), svg);
    var stemEl = el('path', line, svg);
    function makeI(i) {
      var g = el('g', {}, svg);
      var stem = el('path', Object.assign({ pathLength: 1 }, line), g);
      var head = el('circle', { stroke: 'none' }, g);
      var cp = el('clipPath', { id: uid + 'c' + i }, defs);
      var cc = el('circle', {}, cp);
      var wedge = el('path', { stroke: 'none', 'clip-path': 'url(#' + uid + 'c' + i + ')' }, g);
      return { g: g, stem: stem, head: head, cc: cc, wedge: wedge };
    }
    var iL = makeI(0), iR = makeI(1);

    function drawI(o, x, fx, col, bgC, r, notch, draw, dotIn, ang) {
      o.g.setAttribute('transform', 'rotate(' + f(ang) + ' ' + x + ' ' + STEM[1] + ')');
      o.stem.setAttribute('d', 'M' + x + ' ' + STEM[1] + ' V' + STEM[0]);
      o.stem.setAttribute('stroke', col);
      o.stem.setAttribute('stroke-dasharray', f(draw * 1000) / 1000 + ' 2');
      o.stem.setAttribute('opacity', draw > 0.005 ? 1 : 0);
      var rr = r * dotIn;
      o.head.setAttribute('cx', x); o.head.setAttribute('cy', HEAD.y); o.head.setAttribute('r', f(rr)); o.head.setAttribute('fill', col);
      o.cc.setAttribute('cx', x); o.cc.setAttribute('cy', HEAD.y); o.cc.setAttribute('r', f(rr + 0.5));
      if (notch > 0.01 && rr > 1) {
        var apexA = 38 * Math.PI / 180, a1 = 2 * Math.PI / 180, a2 = 30 * Math.PI / 180, mid = (a1 + a2) / 2;
        var A1 = lerp(mid, a1, notch), A2 = lerp(mid, a2, notch), dd = 32 * lerp(2.6, 1, notch), R = rr * 1.5;
        var ap = [x + fx * Math.cos(apexA) * dd, HEAD.y + Math.sin(apexA) * dd];
        var p1 = [x + fx * Math.cos(A1) * R, HEAD.y + Math.sin(A1) * R], p2 = [x + fx * Math.cos(A2) * R, HEAD.y + Math.sin(A2) * R];
        o.wedge.setAttribute('d', 'M' + f(ap[0]) + ' ' + f(ap[1]) + ' L' + f(p1[0]) + ' ' + f(p1[1]) + ' L' + f(p2[0]) + ' ' + f(p2[1]) + ' Z');
        o.wedge.setAttribute('fill', bgC); o.wedge.setAttribute('opacity', 1);
      } else o.wedge.setAttribute('opacity', 0);
    }

    function render(t) {
      // camera: wide word view -> square mark view
      var pc = ease(prog(t, T.focus));
      svg.setAttribute('viewBox', VB_WORD.map(function (v, i) { return f(lerp(v, VB_LOGO[i], pc)); }).join(' '));

      // disc bloom
      var pd = prog(t, T.disc);
      var ds = pd <= 0 ? 0 : easeOutBack(pd);
      var deep = ease(prog(t, [0.9, 1.6]));
      var discCol = mixA(HILITE, BRAND, deep);
      var pulse = 1 + 0.035 * Math.sin(Math.PI * prog(t, [2.25, 2.85]));
      disc.setAttribute('fill', 'rgb(' + discCol.map(Math.round).join(',') + ')');
      disc.setAttribute('r', f(DISC.r * ds * pulse));
      disc.setAttribute('opacity', pd > 0 ? 1 : 0);
      var white = smooth(0.15, 0.6, pd);
      var col = mixC(BLUE, WHITE, white);
      var bg = mixA(pageBg, discCol, white);
      var bgC = 'rgb(' + bg.map(Math.round).join(',') + ')';

      // word draws on, then the outer letters part
      var pdw = prog(t, T.draw), pf = prog(t, T.focus);
      letterEls.forEach(function (o) {
        var lag = o.order * 0.045;
        var dr = easeOut(clamp((pdw - lag) / (1 - 0.4), 0, 1));
        o.p.setAttribute('stroke-dasharray', f(dr * 1000) / 1000 + ' 2');
        o.p.setAttribute('opacity', dr > 0.005 ? 1 : 0);
        if (o.dot) o.dot.setAttribute('r', f(o.L.dot[2] * easeOutBack(smooth(0.55, 1, dr))));
        var lagE = o.L.side < 0 ? 0 : (o.order - 2) * 0.035;
        var pe = ease(clamp((pf - lagE) / 0.75, 0, 1));
        o.g.setAttribute('transform', 'translate(' + f(o.L.side * 700 * pe) + ' 0)');
        o.g.setAttribute('opacity', f((1 - pe) * 1000) / 1000);
      });
      var drawImi = easeOut(clamp((pdw - 0.05) / 0.6, 0, 1));

      // the m: rises into a mountain, then is removed and settles back with a little spring
      var up = ease(prog(t, T.rise));
      var pfall = prog(t, T.fall);
      var mo = pfall > 0 ? Math.max(-0.06, 1 - spring(pfall)) : up;
      if (Math.abs(mo) < 0.0005) {
        mEl.setAttribute('d', M_PATH); stemEl.setAttribute('opacity', 0);
      } else {
        var pts = M_PTS.map(function (p, i) { return lp(p, MT_PTS[i], mo); });
        mEl.setAttribute('d', 'M' + pts.map(function (p) { return f(p[0]) + ' ' + f(p[1]); }).join(' L'));
        var v = pts[(NPTS - 1) / 2];
        var stemLen = 305 * (1 - smooth(0, 0.6, Math.max(0, mo)));
        stemEl.setAttribute('d', 'M' + f(v[0]) + ' ' + f(v[1]) + ' L' + f(v[0]) + ' ' + f(v[1] + stemLen));
        stemEl.setAttribute('stroke', col); stemEl.setAttribute('opacity', stemLen > 2 ? 1 : 0);
      }
      mEl.setAttribute('stroke', col);
      mEl.setAttribute('stroke-dasharray', f(drawImi * 1000) / 1000 + ' 2');
      mEl.setAttribute('opacity', drawImi > 0.005 ? 1 : 0);

      // the two i's: pushed apart by the mountain, then lean in toward each other
      var apart = up * (1 - smooth(0, 0.35, prog(t, T.lean)));
      var pl = prog(t, T.lean);
      var lean = Math.sin(Math.PI * Math.min(1, pl * 1.25)) * Math.exp(-2.2 * pl) * 1.0;
      var ang = -5 * apart + 7 * lean;
      var dotIn = easeOutBack(smooth(0.35, 0.9, pdw));
      var r = lerp(55, HEAD.r, white), notch = white;
      drawI(iL, X_L, 1, col, bgC, r, notch, drawImi, dotIn, ang);
      drawI(iR, X_R, -1, col, bgC, r, notch, drawImi, dotIn, -ang);

      // bridge of light between the two minds
      var pb = prog(t, T.bridge);
      if (pb > 0 && pb < 1) {
        var a = ease(smooth(0.35, 1, pb)), b = ease(smooth(0, 0.65, pb));
        bridge.setAttribute('stroke-dasharray', f((b - a) * 1000) / 1000 + ' 2');
        bridge.setAttribute('stroke-dashoffset', f(-a * 1000) / 1000);
        bridge.setAttribute('opacity', f(Math.sin(Math.PI * pb) * 0.9 * 1000) / 1000);
      } else bridge.setAttribute('opacity', 0);
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
