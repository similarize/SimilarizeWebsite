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
    else setTimeout(bump, 16);
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
    try { return navigator.getGamepads ? navigator.getGamepads() : []; }
    catch (e) { return []; }
  }
  function pollPad(i) {
    var idx = i | 0;
    if (idx < 0) idx = 0;
    while (prev.length <= idx) prev.push({});
    while (last.length <= idx) last.push(null);
    while (frameSlot.length <= idx) frameSlot.push({ token: -1, snap: null });
    scheduleFrameBump();
    var slot = frameSlot[idx];
    if (slot.token === frameToken && slot.snap) return slot.snap;
    var list = pads();
    var gp = list && list[idx];
    var out = snap(gp || null, idx);
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
  /* padreal1: one physical controller → one seat, even when the OS lists extras.
   *
   * Chrome / Steam / Tesla enumerate ghosts that stay "connected" but never
   * drive, or that mirror one real pad under a second index and a different
   * id. Grouping by id missed those (2 sticks → 3 highlighted frogs, one of
   * them dead). Fingerprint-at-rest also merged two idle Xbox pads.
   *
   * A slot earns a seat only after its sticks, face buttons, or d-pad
   * actually change. If that change happens while a higher-ranked slot
   * (standard / XInput / lower index) changes — this poll or the one just
   * before — the extra slot is an echo and does not get a frog. A second
   * real controller gets the next frog the moment it moves on its own, and
   * keeps that seat after it goes idle. A dead endpoint never gets a name.
   */
  var slotPrev = [];
  var slotProven = [];
  var slotSolo = [];
  var slotLastFrame = [];
  var slotId = [];
  var slotAlias = [];
  var ACT_BTNS = [0, 1, 2, 3, 4, 5, 8, 9, 12, 13, 14, 15];

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

  function rankOf(item) {
    var s = 0;
    if (item.info.standard) s += 4;
    if (item.info.xinput) s += 2;
    return s * 10 - item.slot;
  }

  function uniqueConnectedIndices(max) {
    var n = typeof max === "number" ? max : 4;
    if (n < 1) n = 1;
    if (n > 8) n = 8;
    scheduleFrameBump();
    var serial = frameToken;
    var list = pads();
    var live = [];
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

    /* A lower-ranked slot that only changes while a better slot changes
       (same poll, or one frame later) is an echo. It never earns a seat.
       A slot that changes on its own — the other sticks still — does. */
    function wasJust(slot) {
      return slotLastFrame[slot] >= 0 && slotLastFrame[slot] === serial - 1;
    }
    function backer(it) {
      var best = -1;
      var bestR = -1e9;
      var r = rankOf(it);
      var j, o;
      for (j = 0; j < live.length; j++) {
        o = live[j];
        if (o.slot === it.slot) continue;
        if (rankOf(o) <= r) continue;
        if (o.active || wasJust(o.slot)) {
          if (rankOf(o) > bestR) {
            bestR = rankOf(o);
            best = o.slot;
          }
        }
      }
      return best;
    }

    for (i = 0; i < live.length; i++) {
      item = live[i];
      var parent = backer(item);
      if (item.active && parent < 0) {
        slotSolo[item.slot] = true;
        slotProven[item.slot] = true;
        slotAlias[item.slot] = -1;
      } else if (!slotSolo[item.slot]) {
        slotProven[item.slot] = false;
        if (parent >= 0) slotAlias[item.slot] = parent;
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
    return out;
  }

  function pluggedIndices(max) {
    var n = typeof max === "number" ? max : 4;
    if (n < 1) n = 1;
    if (n > 8) n = 8;
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
    pressed: pressed,
    justPressed: justPressed,
    DEADZONE: DZ
  };
})(typeof window !== "undefined" ? window : globalThis);
