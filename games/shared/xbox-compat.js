/*! SimXbox: Xbox / console Edge compatibility for Unity WebGL/WebGPU pages (xb1, 2026-10-20). No dependencies.
 * Include BEFORE the Unity loader script:  <script src="../shared/xbox-compat.js?v=xbp4"></script>
 * See XBOX_COMPAT.md. Query flags: ?lowmem=1|0  ?webgpu=0  ?padlog=1  ?padgate=1|0  ?ctxmenu=1|0  ?fs=1|0  ?lock=1|0  ?hud=1|0
 */
(function () {
  "use strict";
  var qs = location.search, ua = navigator.userAgent || "";
  function flag(n) { var m = new RegExp("[?&]" + n + "=([01])").exec(qs); return m ? m[1] === "1" : null; }
  var isXbox = /Xbox/i.test(ua);
  var lm = flag("lowmem"), lowmem = lm === null ? isXbox : lm;
  var forceWebGL2 = lowmem || flag("webgpu") === false;
  var opts = window.SimXboxOptions || {};
  var canvasSel = opts.canvas || "#unity-canvas";

  // 1) WebGPU off -> Unity's loader (which calls navigator.gpu.requestAdapter() before downloading) and engine take
  //    their WebGL 2 path. Must run before the loader script.
  if (forceWebGL2) { try { Object.defineProperty(navigator, "gpu", { value: undefined, configurable: true }); } catch (e) { } }

  // 2) Legacy EdgeHTML opt-out of mouse emulation. Current Chromium Edge for Xbox most likely ignores it (the user
  //    switches with HOLD Menu -> "Use game controls"); harmless everywhere else.
  function emuOptOut() { try { if ("gamepadInputEmulation" in navigator && navigator.gamepadInputEmulation !== "gamepad") navigator.gamepadInputEmulation = "gamepad"; } catch (e) { } }
  emuOptOut();

  // 2b) xbp1: on Xbox Edge, HOLD Menu in browsing (cursor) mode opens Edge's context menu, whose first item is "Use game
  //     controls". Our pages (and many web games) call preventDefault() on "contextmenu", which hides that menu over
  //     the page, so the toggle can't be reached (matches reports that it only works with the cursor outside the game
  //     frame). Window capture runs before any document/canvas listener: stop the event there so nothing can cancel
  //     it. Xbox only by default; ?ctxmenu=1 forces it on (testing), ?ctxmenu=0 off.
  var cf = flag("ctxmenu"), keepCtx = cf === null ? isXbox : cf;
  if (keepCtx) window.addEventListener("contextmenu", function (e) { e.stopImmediatePropagation(); }, true);

  // 2c) xbp2: on Xbox, element fullscreen of the Unity canvas (the page's fullscreen button calls Unity SetFullscreen)
  //     turns the screen black with only the cursor left, and with the canvas filling the whole viewport the cursor
  //     can't get "outside the game" for HOLD Menu -> "Use game controls". So on Xbox: no fullscreen requests from
  //     the page at all (Edge's own View-button full screen still works), hide #fs, and keep a 56px non-canvas strip
  //     at the top. ?fs=1 = old behavior; ?fs=0 = this behavior on any browser (testing).
  var VER = "xbp4", ff = flag("fs"), noFs = ff === null ? isXbox : !ff;
  if (noFs) {
    var fsNoop = function () { console.log("SimXbox: fullscreen request blocked (Xbox; ?fs=1 to allow)"); return Promise.resolve(); };
    ["requestFullscreen", "webkitRequestFullscreen", "webkitRequestFullScreen", "mozRequestFullScreen", "msRequestFullscreen"].forEach(function (k) {
      if (Element.prototype[k]) try { Element.prototype[k] = fsNoop; } catch (e) { } });
    var st = document.createElement("style"); st.id = "simxbox-nofs";
    st.textContent = "#fs{display:none!important}" + canvasSel + "{inset:auto 0 0 0!important;top:56px!important;height:calc(100% - 56px)!important}" +
      "html::before{content:'Controller: put the cursor up here, HOLD the Menu button, choose Use game controls';position:fixed;left:0;right:0;top:0;height:56px;" +
      "display:flex;align-items:center;justify-content:center;font:600 18px system-ui,sans-serif;color:#cfe;background:#0b0f16;border-bottom:1px solid #345;z-index:1}";
    (document.head || document.documentElement).appendChild(st);
  }
  var hf = flag("hud"), hudOn = hf === null ? isXbox : hf;

  // 2d) xbp3: Unity Cursor.lockState = Locked calls requestPointerLock. On Xbox, A is a click in cursor mode, so the page
  //     grabs pointer lock and HOLD Menu -> "Use game controls" never opens. Xbox only by default; ?lock=1 allows it.
  var lk = flag("lock"), noLock = lk === null ? isXbox : !lk;
  if (noLock) ["requestPointerLock", "webkitRequestPointerLock", "mozRequestPointerLock"].forEach(function (k) {
    if (Element.prototype[k]) try { Element.prototype[k] = function () { console.log("SimXbox: pointer lock blocked (Xbox; ?lock=1 to allow)"); return Promise.resolve(); }; } catch (e) { } });

  function canvas() { return document.querySelector(canvasSel) || document.querySelector("canvas"); }
  function refocus() { emuOptOut(); setTimeout(function () { try { window.focus(); var c = canvas(); if (c) { if (!c.hasAttribute("tabindex")) c.setAttribute("tabindex", "-1"); c.focus(); } } catch (e) { } }, 50); }

  var SX = window.SimXbox = {
    isXbox: isXbox, lowmem: lowmem, forceWebGL2: forceWebGL2, dprCap: 1, maxCanvas: [1280, 720],
    padSeen: false, canvas: canvas, refocus: refocus,
    // DPR to use: in low-memory mode <= dprCap and small enough that the backbuffer fits maxCanvas
    capDpr: function (raw) {
      raw = raw || window.devicePixelRatio || 1; if (!lowmem) return raw;
      var w = window.innerWidth || screen.width || 1280, h = window.innerHeight || screen.height || 720;
      var mx = SX.maxCanvas, long = Math.max(w, h), short = Math.min(w, h);
      return Math.max(0.5, Math.min(SX.dprCap, raw, mx[0] / long, mx[1] / short));
    },
    // createUnityInstance config: cap devicePixelRatio, and skip the IndexedDB second copy of the data file
    applyConfig: function (cfg) {
      if (!lowmem || !cfg) return cfg;
      cfg.devicePixelRatio = SX.capDpr(cfg.devicePixelRatio || window.devicePixelRatio || 1);
      cfg.cacheControl = function () { return "no-store"; };
      return cfg;
    }
  };
  if (lowmem || forceWebGL2) console.log("SimXbox: " + (isXbox ? "Xbox" : "flag") + " lowmem=" + lowmem + " webgl2=" + forceWebGL2 + " navigator.gpu " + (navigator.gpu ? "present" : "hidden"));

  // 3) Give the canvas focus back after fullscreen exit, tab return, window focus, and a pad connecting
  document.addEventListener("fullscreenchange", refocus);
  document.addEventListener("webkitfullscreenchange", refocus);
  document.addEventListener("visibilitychange", function () { if (!document.hidden) refocus(); });
  window.addEventListener("focus", refocus);
  window.addEventListener("gamepadconnected", function (e) { console.log("SimXbox: gamepad connected #" + e.gamepad.index + " " + e.gamepad.id); refocus(); });

  // 4) Poll getGamepads(): Chromium only fills it after a button press; note when a pad is actually live
  function pads() { try { return navigator.getGamepads ? navigator.getGamepads() : []; } catch (e) { return []; } }
  function livePad() { var g = pads(); for (var i = 0; i < g.length; i++) if (g[i] && g[i].connected) {
      for (var j = 0; j < g[i].buttons.length; j++) if (g[i].buttons[j].pressed) return g[i];
      for (var k = 0; k < g[i].axes.length; k++) if (Math.abs(g[i].axes[k]) > 0.5) return g[i]; } return null; }

  // 5) Xbox gate: Edge for Xbox starts in "browsing controls" (pad = cursor); the user must HOLD Menu -> "Use game
  //    controls", and that toggle reportedly only works with the cursor outside the game area. So until the first pad
  //    button arrives, show a strip across the top (not the canvas) for the cursor to sit on. Clicking it (A is a
  //    click in cursor mode) focuses the canvas and asks for fullscreen; the first pad button hides it.
  var gf = flag("padgate"), gateOn = gf === null ? isXbox : gf, gate = null;
  function hideGate() { if (gate) { gate.remove(); gate = null; } }
  function showGate() {
    if (gate || SX.padSeen || !document.body) return;
    gate = document.createElement("div"); gate.id = "simxbox-gate";
    gate.style.cssText = "position:fixed;left:0;right:0;top:0;min-height:56px;z-index:9;display:block;" +
      "background:#000;border-bottom:2px solid rgba(255,255,255,.35);cursor:pointer;font:600 20px system-ui,sans-serif;color:#fff;text-align:center;padding:6px 120px;box-sizing:border-box;line-height:1.3";
    if (hudOn) gate.style.background = "transparent", gate.style.borderBottom = "0", gate.style.minHeight = "56px";
    else gate.innerHTML = 'Move the cursor here, <b>hold</b> the &#8801; Menu button, choose <b>Use game controls</b>, then press <b>A</b>.' +
      '<br><span style="font-size:15px;opacity:.8">Still no controller? Open and close the Xbox Guide, then press A.</span>';
    gate.addEventListener("click", function () { refocus(); }); // xbp2: no fullscreen request from the gate
    document.body.appendChild(gate);
  }
  var t0 = Date.now(), readySeen = false;
  setInterval(function () {
    var p = livePad();
    if (p && !SX.padSeen) { SX.padSeen = true; console.log("SimXbox: pad live #" + p.index + " " + p.id); hideGate(); refocus(); }
    if (!readySeen && (window.unityInstance || opts.ready)) { readySeen = true; if (gateOn) showGate(); }
  }, 250);

  // 5b) xbp4: always-on 2-line readout in the 56px strip on Xbox (?hud=0 hides, ?hud=1 shows anywhere). Updated every
  //     frame from navigator.getGamepads(); "mode" is a GUESS: pad data changing = game controls; mousemove with no pad
  //     data = the stick is driving Edge's cursor (browsing controls). Pages can set SimXboxOptions.build = "tag".
  if (hudOn) {
    var hud = null, hudTxt = "", lastPadT = -1e9, lastMoveT = -1e9, prevSig = "";
    window.addEventListener("mousemove", function () { lastMoveT = performance.now(); }, true);
    var hudTick = function () {
      requestAnimationFrame(hudTick);
      if (!hud) { if (!document.body) return; hud = document.createElement("div"); hud.id = "simxbox-hud";
        hud.style.cssText = "position:fixed;left:0;right:0;top:0;height:56px;z-index:2147483646;pointer-events:none;box-sizing:border-box;padding:3px 10px;" +
          "background:#0b0f16;border-bottom:1px solid #345;color:#cfe;font:600 17px/25px ui-monospace,Consolas,monospace;white-space:pre;overflow:hidden";
        document.body.appendChild(hud); }
      var now = performance.now(), g = pads(), list = [];
      for (var i = 0; i < g.length; i++) if (g[i] && g[i].connected) list.push(g[i]);
      var p = list[0], sig = "", vals = "pad: none (press a button)";
      if (p) { var b = [];
        for (var j = 0; j < p.buttons.length; j++) if (p.buttons[j].pressed || p.buttons[j].value > 0.5) b.push(j);
        var ax = Array.prototype.map.call(p.axes, function (a) { return (a >= 0 ? "+" : "") + a.toFixed(2); }).join(" ");
        sig = b.join(",") + "|" + ax; vals = "axes " + ax + "  btn [" + b.join(",") + "]  " + p.id.slice(0, 34) + " map=" + (p.mapping || "NONE"); }
      if (sig !== prevSig) { if (prevSig) lastPadT = now; prevSig = sig; }
      var mode = now - lastPadT < 2000 ? "GAME (pad data arriving)" : now - lastMoveT < 2000 ? "CURSOR? (stick moves mouse)" :
        list.length ? "idle, move a stick" : "CURSOR? (no pad seen)";
      var t = "HOLD \u2261 here: Use game controls | " + (opts.build || VER) + " | pads " + list.length + " | focus " + (document.hasFocus() ? "Y" : "N") +
        " " + document.visibilityState + " | lock " + (document.pointerLockElement ? "ON" : "off") + " | guess: " + mode + "\n" + vals;
      if (t !== hudTxt) { hudTxt = t; hud.textContent = t; }
    };
    requestAnimationFrame(hudTick);
  }

  // 6) ?padlog=1 diagnostics every second: console + an on-screen box (consoles have no devtools). Unity's Input
  //    System only lists mapping "standard" pads in Gamepad.all; anything else arrives as a Joystick.
  if (flag("padlog")) {
    var box = null;
    setInterval(function () {
      var out = [], g = pads();
      for (var i = 0; i < g.length; i++) { var x = g[i]; if (!x) continue; var b = [];
        for (var j = 0; j < x.buttons.length; j++) if (x.buttons[j].pressed) b.push(j);
        out.push("#" + i + " " + x.id.slice(0, 48) + " mapping=" + (x.mapping || "(none: Unity sees a Joystick, not a Gamepad)") +
          " btn[" + b.join(",") + "] axes[" + Array.prototype.map.call(x.axes, function (a) { return a.toFixed(2); }).join(",") + "]"); }
      var a = document.activeElement;
      var line = "SimXbox " + VER + " xbox=" + isXbox + " lowmem=" + lowmem + " t=" + ((Date.now() - t0) / 1000).toFixed(0) + "s emu=" + ("gamepadInputEmulation" in navigator ? navigator.gamepadInputEmulation : "n/a") +
        " focus=" + document.hasFocus() + " active=" + (a && (a.id || a.tagName)) + " fs=" + !!(document.fullscreenElement || document.webkitFullscreenElement) +
        " lock=" + !!document.pointerLockElement + " gate=" + !!gate + " ctx=" + keepCtx + " nofs=" + noFs + " nolock=" + noLock + " padSeen=" + SX.padSeen + " webgpu=" + (navigator.gpu ? "on" : "off");
      console.log("SimXbox padlog: " + line + " pads=" + (out.length ? out.join(" | ") : "none"));
      if (!box && document.body) { box = document.createElement("pre"); box.id = "simxbox-padlog";
        box.style.cssText = "position:fixed;left:2vw;right:2vw;bottom:2vh;z-index:2147483647;margin:0;white-space:pre-wrap;font:bold 24px/1.35 monospace;color:#ff0;background:rgba(0,0,0,.88);border:4px solid #ff0;padding:12px 16px;border-radius:10px;pointer-events:none";
        document.body.appendChild(box); }
      if (box) box.textContent = "PADLOG " + line + "\n" + (out.length ? out.join("\n") : "pads: none (press a button)");
    }, 1000);
  }
})();
