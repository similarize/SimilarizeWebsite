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
 *   connectedIndices(max?)  — physical pads (index dedupe + same-id lockstep/sticky-split)
 *   uniqueConnectedIndices(max?) — alias of connectedIndices (1 physical stick → 1 seat)
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
  /* padbind1: one physical stick → one lobby/index seat.
   *
   * History: onefrog1 collapsed same-id + matching fingerprint (fixed Steam dual-slot
   * ghosts, but also hid a second idle same-model pad). padpick1 then deduped ONLY
   * repeated Gamepad.index — two real Xbox pads stayed two seats, but Steam/USB
   * mirrors under DIFFERENT indices each claimed a frog (Ben: 2 pads → 3 UI, one
   * stick lights Pad1/Pad2/Pad4).
   *
   * Fix: per frame, group by gamepad.id. Matching fingerprints collapse to the
   * lowest slot (ghosts stay one). Diverged ACTIVE fingerprints prove a split and
   * stick until disconnect — two real same-model pads stay two after either is
   * touched, even when both return to idle. Idle-only mismatch is ignored (noise).
   */
  var provenSplit = Object.create(null); /* id -> { slot: true, ... } */

  function padFingerprint(gp) {
    if (!gp) return { key: "", active: false };
    var parts = [];
    var active = false;
    var ax = gp.axes || [];
    var a, v, bt, b, btn, on;
    for (a = 0; a < ax.length; a++) {
      v = ax[a] || 0;
      if (Math.abs(v) < DZ) v = 0;
      else active = true;
      parts.push(Math.round(v * 10));
    }
    bt = gp.buttons || [];
    for (b = 0; b < bt.length; b++) {
      btn = bt[b];
      on = !!(btn && (btn.pressed || (typeof btn.value === "number" && btn.value > 0.45)));
      if (on) active = true;
      parts.push(on ? 1 : 0);
    }
    return { key: parts.join(","), active: active };
  }

  function uniqueConnectedIndices(max) {
    var n = typeof max === "number" ? max : 4;
    if (n < 1) n = 1;
    if (n > 8) n = 8;
    var list = pads();
    var idxs = [];
    var seenIndex = Object.create(null);
    var byId = Object.create(null);
    var liveSlots = Object.create(null);
    var anon = 0;
    var i, gp, id, physicalIndex, group, g, fp, key, cluster, reps, proven, keep;
    var slot, empty, liveInGroup, outSlots, anyActiveRep, rep, k;
    for (i = 0; i < n; i++) {
      gp = list && list[i];
      if (!gp) continue;
      physicalIndex = typeof gp.index === "number" && isFinite(gp.index) && gp.index >= 0
        ? gp.index | 0
        : i;
      if (seenIndex[physicalIndex]) continue;
      seenIndex[physicalIndex] = true;
      liveSlots[i] = true;
      id = gp.id ? String(gp.id) : ("__anon_" + (anon++));
      if (!byId[id]) byId[id] = [];
      fp = padFingerprint(gp);
      byId[id].push({ slot: i, index: physicalIndex, key: fp.key, active: fp.active });
    }
    for (id in provenSplit) {
      if (!Object.prototype.hasOwnProperty.call(provenSplit, id)) continue;
      proven = provenSplit[id];
      keep = Object.create(null);
      empty = true;
      for (slot in proven) {
        if (!Object.prototype.hasOwnProperty.call(proven, slot)) continue;
        if (liveSlots[slot | 0]) {
          keep[slot] = true;
          empty = false;
        }
      }
      if (empty) delete provenSplit[id];
      else provenSplit[id] = keep;
    }
    for (id in byId) {
      if (!Object.prototype.hasOwnProperty.call(byId, id)) continue;
      group = byId[id];
      if (group.length === 1) {
        idxs.push(group[0].slot);
        continue;
      }
      /* Collapse lockstep mirrors (same fingerprint) to lowest slot per key */
      cluster = Object.create(null);
      for (g = 0; g < group.length; g++) {
        key = group[g].key;
        if (!cluster[key]) cluster[key] = [];
        cluster[key].push(group[g]);
      }
      reps = [];
      for (key in cluster) {
        if (!Object.prototype.hasOwnProperty.call(cluster, key)) continue;
        cluster[key].sort(function (a, b) { return a.slot - b.slot; });
        reps.push(cluster[key][0]);
      }
      reps.sort(function (a, b) { return a.slot - b.slot; });

      liveInGroup = Object.create(null);
      for (g = 0; g < group.length; g++) liveInGroup[group[g].slot] = true;

      proven = provenSplit[id];
      if (proven) {
        outSlots = [];
        for (slot in proven) {
          if (!Object.prototype.hasOwnProperty.call(proven, slot)) continue;
          if (liveInGroup[slot | 0]) outSlots.push(slot | 0);
        }
        if (outSlots.length) {
          /* New active fingerprint cluster → prove its representative too */
          anyActiveRep = false;
          for (k = 0; k < reps.length; k++) if (reps[k].active) anyActiveRep = true;
          if (anyActiveRep && reps.length > 1) {
            for (k = 0; k < reps.length; k++) {
              proven[reps[k].slot] = true;
              if (outSlots.indexOf(reps[k].slot) < 0) outSlots.push(reps[k].slot);
            }
          }
          outSlots.sort(function (a, b) { return a - b; });
          for (k = 0; k < outSlots.length; k++) idxs.push(outSlots[k]);
          continue;
        }
      }

      if (reps.length === 1) {
        idxs.push(reps[0].slot);
        continue;
      }
      anyActiveRep = false;
      for (k = 0; k < reps.length; k++) if (reps[k].active) anyActiveRep = true;
      if (anyActiveRep) {
        if (!provenSplit[id]) provenSplit[id] = Object.create(null);
        for (k = 0; k < reps.length; k++) {
          provenSplit[id][reps[k].slot] = true;
          idxs.push(reps[k].slot);
        }
        continue;
      }
      /* Never-proven idle same-model set (or idle noise) → one canonical seat */
      idxs.push(reps[0].slot);
    }
    idxs.sort(function (a, b) { return a - b; });
    return idxs;
  }

  function connectedIndices(max) {
    return uniqueConnectedIndices(max);
  }
  function connectedCount(max) {
    return connectedIndices(max).length;
  }
  /** Map a raw slot to the canonical uniqueConnectedIndices representative, or -1. */
  function canonicalIndex(slot, max) {
    var want = slot | 0;
    var xs = uniqueConnectedIndices(max);
    var list = pads();
    var i, gp, idWant, id;
    if (xs.indexOf(want) >= 0) return want;
    gp = list && list[want];
    if (!gp) return xs.length ? xs[0] : -1;
    idWant = gp.id ? String(gp.id) : null;
    if (!idWant) return xs.length ? xs[0] : -1;
    for (i = 0; i < xs.length; i++) {
      gp = list && list[xs[i]];
      id = gp && gp.id ? String(gp.id) : null;
      if (id && id === idWant) return xs[i] | 0;
    }
    return xs.length ? xs[0] : -1;
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
    connectedCount: connectedCount,
    canonicalIndex: canonicalIndex,
    pressed: pressed,
    justPressed: justPressed,
    DEADZONE: DZ
  };
})(typeof window !== "undefined" ? window : globalThis);
