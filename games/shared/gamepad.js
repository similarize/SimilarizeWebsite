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
 * Bind by slot index once claimed; never feed one pad into two players.
 *
 * ctrl1: Frame-cached pollPad/pollAll — first snap of a slot in a browser frame is
 * reused for later callers. Re-polling used to recompute rising edges against an
 * already-updated prev → buttonsPressed.x/b eaten (flaky HOP). The cache token
 * bumps in a microtask so the NEXT animation frame is a new snap (pad3d1:
 * a cached A edge must not still be true on the following frame). Also exposes
 * analog ltValue/rtValue 0–1 from buttons[6]/[7].value.
 *
 * API (window.SimilarizeGamepad):
 *   start()                 — optional; auto-runs on load
 *   poll() / pollPad(i)     — snapshot { connected, lx,ly,rx,ry, a,b,x,y, lb,rb,lt,rt,
 *                             ltValue,rtValue, start,back, dpad:{u,d,l,r}, buttonsPressed:{…edges} }
 *   pollAll(max?)           — poll slots 0..max-1 once (avoids double-poll eating edges)
 *   connectedIndices(max?)  — pads that have actually moved on their own (one physical stick → one seat)
 *   pluggedIndices(max?)    — raw connected endpoints (deduped by Gamepad.index only). Not for seating.
 *   uniqueConnectedIndices(max?) — alias of connectedIndices
 *   connectedCount(max?)    — length of connectedIndices
 *   canonicalIndex(slot?)   — map a raw/ghost slot to the unique representative
 *   padNumber(slot?)        — 1-based "Pad N" label from the deduped list (fallback slot+1)
 *   debug()                 — one line per raw entry: kept / alias→#n / ignored
 *
 * padmerge1 (ported from Dirt Track RC Rally, confirmed on Bill's 2× Xbox setup):
 *   1) if any connected pad reports mapping==="standard", every non-standard entry is
 *      ignored outright (never counted, pollPad returns a disconnected snapshot);
 *   2) press-coincidence aliasing, independent of id / mapping / button count: two raw
 *      entries whose button presses land within 80 ms twice (or whose A presses land
 *      within 120 ms once while either is still unproven = a join) are one device —
 *      the later presser (tie: lower index) becomes an alias. Aliases are never counted
 *      and pollPad(alias) returns a disconnected snapshot, so a ghost can't drive a frog.
 *      Pairs that have each pressed alone are known-distinct and never merged; an alias
 *      that presses alone twice is released (false merge of two real pads self-heals).
 *   3) on every merge window fires "similarize-gamepad-merge" {detail:{alias, root, handled}}
 *      so games can move/free a seat on the alias (set detail.handled = true when they did);
 *      a "Merged duplicate controller" toast shows if the alias was counted or a seat moved.
 *   pressed(name, i?)       — held (uses last pollPad cache, or polls once)
 *   justPressed(name, i?)   — rising edge (uses last pollPad cache, or polls once)
 * Typical frame: const gp = SimilarizeGamepad.poll(); then read gp.* / buttonsPressed.
 */
(function (global) {
  "use strict";
  /* ff3dmp1 (Oct 7 2026): same-model Xbox pads also become "known distinct" after two
     independent-activity events (solo press OR stick moved while the other pad was idle),
     so pad 2 no longer drops out while both players hold the same stick pose. */
  var DZ = 0.25;
  var NAMES = ["a", "b", "x", "y", "lb", "rb", "lt", "rt", "back", "start", "du", "dd", "dl", "dr"];
  var prev = [{}, {}, {}, {}];
  var last = [null, null, null, null];
  var hinted = false;
  /* ctrl1: one snap per slot per browser frame — share edges across callers */
  var frameToken = 0;
  var frameBumpScheduled = false;
  var frameSlot = []; /* { token, snap } per pad index */

  function scheduleFrameBump() {
    if (frameBumpScheduled) return;
    frameBumpScheduled = true;
    var bump = function () {
      frameToken++;
      frameBumpScheduled = false;
    };
    /* pad3d1: bump at the end of this turn, not on a later rAF.
       The game queues its next frame before this bump, so an rAF bump
       left the same buttonsPressed edge true for a second animation frame
       (board, then immediate EXIT). Microtasks run after this frame's
       rAF callbacks and before the next frame, so same-frame callers still
       share one snap. */
    if (typeof queueMicrotask === "function") queueMicrotask(bump);
    else if (typeof requestAnimationFrame === "function") requestAnimationFrame(bump);
    else setTimeout(bump, 0);
  }

  function axis(v) {
    return Math.abs(v) < DZ ? 0 : v;
  }
  function btn(gp, i) {
    var b = gp.buttons && gp.buttons[i];
    if (!b) return false;
    return !!(b.pressed || (typeof b.value === "number" && b.value > 0.45));
  }
  function btnVal(gp, i) {
    var b = gp.buttons && gp.buttons[i];
    if (!b) return 0;
    if (typeof b.value === "number" && isFinite(b.value)) return Math.max(0, Math.min(1, b.value));
    return b.pressed ? 1 : 0;
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
      index: idx,
      lx: 0, ly: 0, rx: 0, ry: 0,
      a: false, b: false, x: false, y: false,
      lb: false, rb: false, lt: false, rt: false,
      ltValue: 0, rtValue: 0,
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
    out.ltValue = btnVal(gp, 6); out.rtValue = btnVal(gp, 7);
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
    try {
      var gps = navigator.getGamepads ? navigator.getGamepads() : [];
      return gps || [];
    }
    catch (e) { return []; }
  }

  /* ---------- padmerge1: standard-mapping preference + press-coincidence aliasing ---------- */
  var MAXP = 8;
  var COIN_MS = 80;      /* two presses this close = same device (needs 2 hits) */
  var JOIN_MS = 120;     /* A presses this close = same device on a join (1 hit) */
  var SOLO_MS = 120;     /* a press with no partner press this close = pressed alone */
  var pressScanToken = -1;
  var pressId = [];
  var rawPrevBtn = [];
  var lastPressT = [];
  var lastAT = [];
  var pressAll = [];     /* recent press times, any button */
  var pressPend = [];    /* digital presses awaiting solo evaluation */
  var pressAlias = [];   /* -1 or representative raw index */
  var ignoredSlot = [];  /* non-standard while a standard pad exists */
  var pairCoin = Object.create(null);
  var pairSolo = Object.create(null);
  var pairDistinct = Object.create(null);
  /* ff3dmp1: stick-only "moved while the other pad sat fully idle" evidence.
     A mirror/ghost can never move while its twin is idle, so two such events
     (or one plus a solo press) prove two real pads — even same-model Xbox pads. */
  var moveSolo = Object.create(null);
  var busyHist = [];
  var movePend = [];
  var MOVE_WIN = 200;
  var mergeListeners = [];

  function nowMs() {
    return (typeof performance !== "undefined" && performance.now) ? performance.now() : Date.now();
  }
  function pairKey(a, b) { return a < b ? a + "|" + b : b + "|" + a; }
  function rootOf(i) {
    var n = 0;
    while (pressAlias[i] >= 0 && n < MAXP) { i = pressAlias[i]; n++; }
    return i;
  }
  function isAliasSlot(i) { return typeof pressAlias[i] === "number" && pressAlias[i] >= 0; }
  function isDeadSlot(i) { return !!ignoredSlot[i] || isAliasSlot(i); }
  function resetPress(i) {
    var k;
    pressId[i] = "";
    rawPrevBtn[i] = null;
    lastPressT[i] = null;
    lastAT[i] = null;
    pressAll[i] = [];
    pressPend[i] = [];
    pressAlias[i] = -1;
    ignoredSlot[i] = false;
    for (k = 0; k < MAXP; k++) if (pressAlias[k] === i) pressAlias[k] = -1;
    var pre = i + "|", post = "|" + i;
    for (k in pairCoin) if (k.indexOf(pre) === 0 || k.slice(-post.length) === post) delete pairCoin[k];
    for (k in pairDistinct) if (k.indexOf(pre) === 0 || k.slice(-post.length) === post) delete pairDistinct[k];
    for (k in pairSolo) {
      var ab = k.split(">");
      if ((ab[0] | 0) === i || (ab[1] | 0) === i) delete pairSolo[k];
    }
    for (k in moveSolo) {
      var mb = k.split(">");
      if ((mb[0] | 0) === i || (mb[1] | 0) === i) delete moveSolo[k];
    }
    busyHist[i] = [];
    movePend[i] = [];
  }
  /* ff3dmp1: any two independent-activity events between a pair (solo press or
     solo stick move, either direction) = two real controllers. Undo a false
     merge right away so a same-model pad that joined in sync gets its seat back. */
  function noteEvidence(x, y) {
    var ck = pairKey(x, y);
    if (pairDistinct[ck]) return;
    var ev = (pairSolo[x + ">" + y] | 0) + (pairSolo[y + ">" + x] | 0) +
      (moveSolo[x + ">" + y] | 0) + (moveSolo[y + ">" + x] | 0);
    if (ev < 2) return;
    pairDistinct[ck] = true;
    if (pairCoin[ck]) pairCoin[ck].n = 0;
    if (pressAlias[x] === y || (isAliasSlot(x) && rootOf(x) === y)) { pressAlias[x] = -1; uniqCache = null; }
    if (pressAlias[y] === x || (isAliasSlot(y) && rootOf(y) === x)) { pressAlias[y] = -1; uniqCache = null; }
  }
  function pressedNear(y, t, w) {
    var h = pressAll[y] || [];
    for (var i = 0; i < h.length; i++) if (Math.abs(h[i] - t) <= w) return true;
    return false;
  }
  function toast(msg) {
    if (typeof document === "undefined" || !document.body) return;
    var el = document.createElement("div");
    el.setAttribute("aria-live", "polite");
    el.textContent = msg;
    el.style.cssText = "position:fixed;left:50%;bottom:12px;transform:translateX(-50%);z-index:9999;padding:6px 12px;border-radius:8px;background:rgba(0,0,0,.72);color:#f3e2c4;font:12px/1.3 system-ui,sans-serif;pointer-events:none;opacity:1;transition:opacity .4s";
    document.body.appendChild(el);
    setTimeout(function () { el.style.opacity = "0"; setTimeout(function () { el.remove(); }, 500); }, 2200);
  }
  function rankSlot(i, list) {
    var gp = list && list[i];
    var s = 0;
    if (gp && gp.mapping === "standard") s += 4;
    if (gp && String(gp.id || "").toLowerCase().indexOf("xinput") >= 0) s += 2;
    return s;
  }
  function mergeSlots(a, b, list) {
    var ra = rootOf(a), rb = rootOf(b);
    if (ra === rb) return;
    var keep, drop;
    var pa = !!slotProven[ra], pb = !!slotProven[rb];
    if (pa !== pb) keep = pa ? ra : rb;              /* the already-counted one keeps its seat */
    else {
      var ta = lastPressT[ra], tb = lastPressT[rb];
      if (ta != null && tb != null && Math.abs(ta - tb) > 8) keep = ta < tb ? ra : rb; /* earlier presser = real */
      else {
        var ka = rankSlot(ra, list), kb = rankSlot(rb, list);
        keep = ka !== kb ? (ka > kb ? ra : rb) : (ra < rb ? ra : rb);
      }
    }
    drop = keep === ra ? rb : ra;
    var wasCounted = !!slotProven[drop];
    pressAlias[drop] = keep;
    for (var k = 0; k < MAXP; k++) if (pressAlias[k] === drop) pressAlias[k] = keep;
    delete pairCoin[pairKey(a, b)];
    slotProven[drop] = false;
    slotSolo[drop] = false;
    slotAlias[drop] = keep;
    frameSlot[drop] = { token: -1, snap: null };
    uniqCache = null;
    /* Always tell the game (it may hold a seat on the alias even if the echo
       filter had already stopped counting it); toast when a seat/pad merged. */
    var detail = { alias: drop, root: keep, handled: false };
    try {
      if (typeof CustomEvent === "function" && typeof window !== "undefined" && window.dispatchEvent) {
        window.dispatchEvent(new CustomEvent("similarize-gamepad-merge", { detail: detail }));
      }
    } catch (e) { /* ignore */ }
    for (var m = 0; m < mergeListeners.length; m++) {
      try { if (mergeListeners[m](detail) === true) detail.handled = true; } catch (e2) { /* ignore */ }
    }
    if (wasCounted || detail.handled) toast("Merged duplicate controller");
  }
  /* Runs once per frame token, before any snapshot or seat list of that frame. */
  function scanPresses() {
    if (pressScanToken === frameToken) return;
    pressScanToken = frameToken;
    var list = pads();
    var now = nowMs();
    var i, j, gp, id, k;
    var live = [];
    var liveSet = Object.create(null);
    var seenIdx = Object.create(null);
    var hasStd = false;
    for (i = 0; i < MAXP; i++) {
      gp = list && list[i];
      if (!gp || gp.connected === false) {
        if (pressId[i] !== "" && pressId[i] != null) resetPress(i);
        else ignoredSlot[i] = false;
        continue;
      }
      var px = typeof gp.index === "number" && isFinite(gp.index) && gp.index >= 0 ? gp.index | 0 : i;
      if (seenIdx[px]) continue;
      seenIdx[px] = true;
      id = "#" + String(gp.id || "") + "/" + String(gp.mapping || "");
      if (pressId[i] !== id) { resetPress(i); pressId[i] = id; }
      live.push(i);
      liveSet[i] = true;
      if (gp.mapping === "standard") hasStd = true;
    }
    /* drop aliases whose representative vanished */
    for (i = 0; i < MAXP; i++) if (isAliasSlot(i) && !liveSet[pressAlias[i]]) pressAlias[i] = -1;
    var elig = [];
    for (i = 0; i < live.length; i++) {
      j = live[i];
      ignoredSlot[j] = !!(hasStd && list[j].mapping !== "standard");
      if (ignoredSlot[j]) {
        if (slotProven[j]) uniqCache = null;
        pressAlias[j] = -1;
        continue;
      }
      elig.push(j);
    }
    /* 1) every eligible entry's press transitions first */
    var hit = Object.create(null), hitA = Object.create(null);
    for (i = 0; i < elig.length; i++) {
      j = elig[i];
      gp = list[j];
      var bt = gp.buttons || [];
      var cur = [];
      for (k = 0; k < bt.length; k++) {
        var b = bt[k];
        cur.push(!!b && !!(b.pressed || (typeof b.value === "number" && b.value > 0.5)));
      }
      var pv = rawPrevBtn[j];
      rawPrevBtn[j] = cur;
      if (!pv) continue; /* first sight: no edges */
      var n = cur.length < pv.length ? cur.length : pv.length;
      var t = false, a = false, dig = false;
      for (k = 0; k < n; k++) {
        if (cur[k] && !pv[k]) {
          t = true;
          if (k === 0) a = true;
          if (k !== 6 && k !== 7) dig = true;
        }
      }
      if (!t) continue;
      hit[j] = true;
      lastPressT[j] = now;
      if (a) { lastAT[j] = now; hitA[j] = true; }
      (pressAll[j] = pressAll[j] || []).push(now);
      if (dig) (pressPend[j] = pressPend[j] || []).push(now);
    }
    /* 2) coincidences → alias */
    for (i = 0; i < elig.length; i++) {
      for (j = i + 1; j < elig.length; j++) {
        var x = elig[i], y = elig[j];
        if (!hit[x] && !hit[y]) continue;
        if (rootOf(x) === rootOf(y)) continue;
        var key = pairKey(x, y);
        if (pairDistinct[key]) continue;
        var tx = lastPressT[x], ty = lastPressT[y];
        if (tx == null || ty == null) continue;
        var joinA = (hitA[x] || hitA[y]) && lastAT[x] != null && lastAT[y] != null &&
          Math.abs(lastAT[x] - lastAT[y]) <= JOIN_MS && now - lastAT[x] <= JOIN_MS && now - lastAT[y] <= JOIN_MS &&
          (!slotProven[rootOf(x)] || !slotProven[rootOf(y)]);
        if (Math.abs(tx - ty) > COIN_MS && !joinA) continue;
        var c = pairCoin[key] || (pairCoin[key] = { n: 0, last: -1e9 });
        if (now - c.last > COIN_MS) { c.n++; c.last = now; }
        if (c.n >= 2 || joinA) mergeSlots(x, y, list);
      }
    }
    /* 3) solo presses (evaluated once the partner window has passed) */
    for (i = 0; i < elig.length; i++) {
      x = elig[i];
      var pend = pressPend[x] || [];
      var keepPend = [];
      for (k = 0; k < pend.length; k++) {
        var pt = pend[k];
        if (now - pt <= SOLO_MS + 10) { keepPend.push(pt); continue; }
        for (j = 0; j < elig.length; j++) {
          y = elig[j];
          if (y === x || pressedNear(y, pt, SOLO_MS)) continue;
          var sk = x + ">" + y;
          pairSolo[sk] = (pairSolo[sk] || 0) + 1;
          var ck = pairKey(x, y);
          if (pairCoin[ck]) pairCoin[ck].n = 0;
          if (pairSolo[y + ">" + x] >= 1) pairDistinct[ck] = true;
          noteEvidence(x, y);
          if (isAliasSlot(x) && rootOf(x) === rootOf(y) && pairSolo[sk] >= 2) {
            /* alias keeps pressing on its own: two real pads, undo the merge */
            pressAlias[x] = -1;
            pairSolo[sk] = 0;
            uniqCache = null;
          }
        }
      }
      pressPend[x] = keepPend;
      var all = pressAll[x] || [];
      while (all.length && now - all[0] > 1500) all.shift();
    }
    for (i = 0; i < elig.length; i++) {
      x = elig[i];
      if (pressAlias[x] >= 0) slotAlias[x] = rootOf(x);
    }
  }
  function emptySnap(idx) {
    return snap(null, idx);
  }
  function pollPad(i) {
    var idx = i | 0;
    if (idx < 0) idx = 0;
    while (prev.length <= idx) prev.push({});
    while (last.length <= idx) last.push(null);
    while (frameSlot.length <= idx) frameSlot.push({ token: -1, snap: null });
    scheduleFrameBump();
    scanPresses();
    var slot = frameSlot[idx];
    if (slot.token === frameToken && slot.snap) return slot.snap;
    var out;
    if (idx < MAXP && isDeadSlot(idx)) {
      /* padmerge1: ignored non-standard twin / pressed-in-lockstep alias never drives anything */
      out = emptySnap(idx);
    } else {
      var list = pads();
      var gp = list && list[idx];
      out = snap(gp || null, idx);
    }
    slot.token = frameToken;
    slot.snap = out;
    return out;
  }
  function poll() { return pollPad(0); }
  function pollAll(max) {
    var n = typeof max === "number" ? max : 4;
    if (n < 1) n = 1;
    if (n > 8) n = 8;
    var out = [];
    for (var i = 0; i < n; i++) out.push(pollPad(i));
    return out;
  }
  /* padreal1 + padfix2: one physical controller → one seat, even when the OS
   * lists extras (Chrome / Steam / Tesla ghosts under other indices + ids).
   *
   * A slot earns a seat only after sticks / face / d-pad change on their own.
   * Echoes that move with a higher-ranked slot (same poll, or within a few
   * frames into the same non-idle pose) never earn a frog — and a once-sticky
   * solo that later locksteps is demoted (padfix2). Idle matching is ignored
   * so two real same-model pads stay two after either is wiggled.
   */
  var slotPrev = [];
  var slotProven = [];
  var slotSolo = [];
  var slotLastFrame = [];
  var slotId = [];
  var slotAlias = [];
  var ACT_BTNS = [0, 1, 2, 3, 4, 5, 8, 9, 12, 13, 14, 15];
  /* Steam mirrors often lag 2–3 frames behind the real stick. */
  var ECHO_FRAMES = 4;

  function clearSlot(i) {
    slotPrev[i] = null;
    slotProven[i] = false;
    slotSolo[i] = false;
    slotLastFrame[i] = -1;
    slotId[i] = "";
    slotAlias[i] = -1;
  }

  function idInfo(gp) {
    var id = String(gp && gp.id || "").toLowerCase();
    return {
      id: id,
      xinput: id.indexOf("xinput") >= 0,
      microsoft: id.indexOf("045e") >= 0 || id.indexOf("xbox") >= 0 || id.indexOf("microsoft") >= 0,
      standard: !!(gp && gp.mapping === "standard")
    };
  }

  function controlVector(gp) {
    var v = [];
    var ax = (gp && gp.axes) || [];
    var a, x, b, btn, on;
    for (a = 0; a < 4; a++) {
      x = ax[a] || 0;
      if (x > 0.55) v.push(1);
      else if (x < -0.55) v.push(-1);
      else v.push(0);
    }
    var bt = (gp && gp.buttons) || [];
    for (b = 0; b < ACT_BTNS.length; b++) {
      btn = bt[ACT_BTNS[b]];
      on = !!(btn && (btn.pressed || (typeof btn.value === "number" && btn.value > 0.55)));
      v.push(on ? 1 : 0);
    }
    return v;
  }

  function deltaKey(prev, now) {
    if (!prev || !now) return "";
    var p = [];
    var i, n = now.length < prev.length ? now.length : prev.length;
    for (i = 0; i < n; i++) if (now[i] !== prev[i]) p.push(i + "=" + now[i]);
    return p.join(",");
  }

  function vecKey(v) {
    return v && v.length ? v.join(",") : "";
  }

  function vecBusy(v) {
    var i;
    if (!v) return false;
    for (i = 0; i < v.length; i++) if (v[i]) return true;
    return false;
  }

  function rankOf(item) {
    var s = 0;
    if (item.info.standard) s += 4;
    if (item.info.xinput) s += 2;
    return s * 10 - item.slot;
  }

  function rawBusy(gp) {
    var ax = (gp && gp.axes) || [];
    var a, b, btn;
    for (a = 0; a < 4; a++) if (Math.abs(ax[a] || 0) > 0.3) return true;
    var bt = (gp && gp.buttons) || [];
    for (b = 0; b < ACT_BTNS.length; b++) {
      btn = bt[ACT_BTNS[b]];
      if (btn && (btn.pressed || (typeof btn.value === "number" && btn.value > 0.3))) return true;
    }
    return false;
  }
  function busyNear(o, t) {
    var h = busyHist[o] || [];
    for (var k = 0; k < h.length; k++) if (Math.abs(h[k] - t) <= MOVE_WIN) return true;
    return false;
  }
  /* ff3dmp1: a move into a non-idle pose while another pad stayed fully idle for
     MOVE_WIN ms before AND after it. Judged only once the after-window passed. */
  function scoreSoloMoves(slots, now) {
    var i, j, x, y, k, pend, keep, pt;
    for (i = 0; i < slots.length; i++) {
      x = slots[i];
      pend = movePend[x] || [];
      keep = [];
      for (k = 0; k < pend.length; k++) {
        pt = pend[k];
        if (now - pt <= MOVE_WIN) { keep.push(pt); continue; }
        for (j = 0; j < slots.length; j++) {
          y = slots[j];
          if (y === x || busyNear(y, pt)) continue;
          moveSolo[x + ">" + y] = (moveSolo[x + ">" + y] | 0) + 1;
          noteEvidence(x, y);
        }
      }
      movePend[x] = keep.length > 6 ? keep.slice(-6) : keep;
    }
    for (i = 0; i < slots.length; i++) {
      var h = busyHist[slots[i]];
      while (h && h.length && now - h[0] > 1500) h.shift();
    }
  }

  var uniqCache = null; /* { token, n, out } — one decision per frame token */
  function uniqueConnectedIndices(max) {
    var n = typeof max === "number" ? max : 4;
    if (n < 1) n = 1;
    if (n > 8) n = 8;
    scheduleFrameBump();
    scanPresses();
    if (uniqCache && uniqCache.token === frameToken && uniqCache.n === n) return uniqCache.out.slice();
    var serial = frameToken;
    var list = pads();
    var live = [];
    var evNow = nowMs();
    var evSlots = [];
    var seenIndex = Object.create(null);
    var i, gp, id, vec, prev, dkey, px, item;
    for (i = 0; i < n; i++) {
      gp = list && list[i];
      if (!gp || gp.connected === false) {
        clearSlot(i);
        continue;
      }
      px = typeof gp.index === "number" && isFinite(gp.index) && gp.index >= 0 ? gp.index | 0 : i;
      if (seenIndex[px]) {
        clearSlot(i);
        continue;
      }
      seenIndex[px] = true;
      id = gp.id ? String(gp.id) : "";
      if (slotId[i] !== id) {
        clearSlot(i);
        slotId[i] = id;
      }
      vec = controlVector(gp);
      if (!ignoredSlot[i]) {
        /* ff3dmp1: busy history + "moved into a pose" moments for solo-move evidence */
        evSlots.push(i);
        if (rawBusy(gp)) (busyHist[i] = busyHist[i] || []).push(evNow);
        if (slotPrev[i] && vecBusy(vec) && deltaKey(slotPrev[i], vec)) (movePend[i] = movePend[i] || []).push(evNow);
      }
      if (ignoredSlot[i]) {
        /* padmerge1: a standard pad exists — non-standard entries never seat */
        clearSlot(i);
        slotId[i] = id;
        continue;
      }
      if (isAliasSlot(i)) {
        slotProven[i] = false;
        slotSolo[i] = false;
        slotAlias[i] = rootOf(i);
        slotPrev[i] = vec;
        continue;
      }
      prev = slotPrev[i];
      dkey = prev ? deltaKey(prev, vec) : "";
      live.push({
        slot: i,
        gp: gp,
        vec: vec,
        delta: dkey,
        active: !!(prev && dkey),
        info: idInfo(gp)
      });
    }

    scoreSoloMoves(evSlots, evNow);

    /* Echo only when the lower slot's change/pose matches a better slot —
       never because an unrelated higher pad twitched (that hid real pad 2). */
    function recentlyActive(slot) {
      return slotLastFrame[slot] >= 0 && (serial - slotLastFrame[slot]) <= ECHO_FRAMES;
    }
    function backer(it) {
      var best = -1;
      var bestR = -1e9;
      var r = rankOf(it);
      var myPose = vecKey(it.vec);
      var busy = vecBusy(it.vec);
      var j, o, match;
      for (j = 0; j < live.length; j++) {
        o = live[j];
        if (o.slot === it.slot) continue;
        if (rankOf(o) <= r) continue;
        /* padmerge1: both have pressed alone before — two real pads, never an echo */
        if (pairDistinct[pairKey(it.slot, o.slot)]) continue;
        match = false;
        if (it.active && o.active && it.delta && o.delta && it.delta === o.delta) {
          /* Same-frame lockstep. Shared release-to-idle from DIFFERENT poses
             (two sticks centering) must NOT count — that demoted real pad 2. */
          var prevSame = vecKey(slotPrev[it.slot]) === vecKey(slotPrev[o.slot]);
          if (busy || prevSame) match = true;
        } else if (it.active && busy && recentlyActive(o.slot) && myPose === vecKey(o.vec)) {
          match = true; /* lagged mirror into the same non-idle pose */
        } else if (!it.active && busy && slotProven[o.slot] && myPose === vecKey(o.vec)) {
          match = true; /* held pose twin of a proven better pad */
        }
        if (!match) continue;
        if (rankOf(o) > bestR) {
          bestR = rankOf(o);
          best = o.slot;
        }
      }
      return best;
    }

    /* Decide parents against a frozen slotPrev, then mutate — updating
       slotPrev mid-loop made release-to-idle miss prevSame for later slots. */
    var parents = [];
    for (i = 0; i < live.length; i++) parents[i] = backer(live[i]);
    for (i = 0; i < live.length; i++) {
      item = live[i];
      var parent = parents[i];
      if (item.active && parent < 0) {
        slotSolo[item.slot] = true;
        slotProven[item.slot] = true;
        slotAlias[item.slot] = -1;
      } else if (parent >= 0) {
        /* padfix2: demote sticky solo that is now echoing a better pad */
        slotAlias[item.slot] = parent;
        slotSolo[item.slot] = false;
        slotProven[item.slot] = false;
      } else if (!slotSolo[item.slot]) {
        slotProven[item.slot] = false;
        slotAlias[item.slot] = -1;
      } else {
        slotAlias[item.slot] = -1;
      }
      if (item.active) slotLastFrame[item.slot] = serial;
      slotPrev[item.slot] = item.vec;
    }

    var out = [];
    for (i = 0; i < live.length; i++) {
      if (slotProven[live[i].slot]) out.push(live[i].slot);
    }
    out.sort(function (a, b) { return a - b; });
    uniqCache = { token: frameToken, n: n, out: out.slice() };
    return out;
  }

  function pluggedIndices(max) {
    var n = typeof max === "number" ? max : 4;
    if (n < 1) n = 1;
    if (n > 8) n = 8;
    scanPresses();
    var list = pads();
    var out = [];
    var seenIndex = Object.create(null);
    var i, gp, px;
    for (i = 0; i < n; i++) {
      gp = list && list[i];
      if (!gp || gp.connected === false) continue;
      px = typeof gp.index === "number" && isFinite(gp.index) && gp.index >= 0 ? gp.index | 0 : i;
      if (seenIndex[px]) continue;
      seenIndex[px] = true;
      if (isDeadSlot(i)) continue; /* padmerge1: ignored twin / alias */
      out.push(i);
    }
    return out;
  }

  function connectedIndices(max) {
    return uniqueConnectedIndices(max);
  }
  function connectedCount(max) {
    return connectedIndices(max).length;
  }
  /** Map a raw slot to the live representative, or -1 if it is not a real seat. */
  function canonicalIndex(slot, max) {
    var want = slot | 0;
    var xs = uniqueConnectedIndices(max);
    if (xs.indexOf(want) >= 0) return want;
    var alias = slotAlias[want];
    if (typeof alias === "number" && alias >= 0 && xs.indexOf(alias) >= 0) return alias;
    return -1;
  }
  /** 1-based label number from the deduped list ("Pad 1".."Pad N"). */
  function padNumber(slot, max) {
    var want = slot | 0;
    var xs = uniqueConnectedIndices(max);
    var c = xs.indexOf(want);
    if (c < 0 && isAliasSlot(want)) c = xs.indexOf(rootOf(want));
    return c >= 0 ? c + 1 : want + 1;
  }
  function debug() {
    var list = pads();
    var xs = uniqueConnectedIndices(MAXP);
    var out = [];
    for (var i = 0; i < MAXP; i++) {
      var gp = list && list[i];
      if (!gp) continue;
      var st = xs.indexOf(i) >= 0 ? "kept" : ignoredSlot[i] ? "ignored (non-standard)" :
        isAliasSlot(i) ? "alias→#" + rootOf(i) : "unproven";
      out.push("#" + i + " " + (gp.mapping || "(none)") + " " + st + " · " + String(gp.id || ""));
    }
    return out;
  }
  function onMerge(fn) {
    if (typeof fn === "function") mergeListeners.push(fn);
  }
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
    el.textContent = "Controller connected · press a button · Xbox / standard layout";
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
    pollAll: pollAll,
    connectedIndices: connectedIndices,
    uniqueConnectedIndices: uniqueConnectedIndices,
    pluggedIndices: pluggedIndices,
    connectedCount: connectedCount,
    canonicalIndex: canonicalIndex,
    padNumber: padNumber,
    debug: debug,
    onMerge: onMerge,
    pressed: pressed,
    justPressed: justPressed,
    DEADZONE: DZ
  };
})(typeof window !== "undefined" ? window : globalThis);
