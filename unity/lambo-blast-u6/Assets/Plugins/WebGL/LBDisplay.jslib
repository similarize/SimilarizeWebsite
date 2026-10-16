// lambo7 display (ported from Four Froggies ffu17 FFDisplay.jslib): Unity WebGL reads Module.devicePixelRatio every frame
// to size the canvas backbuffer, so the game renders the lobby at the screen's real pixel density and switches to the
// gameplay density (and a lighter fallback) when racing. The page (web/index.html) publishes its caps in window.LBDPR.
mergeInto(LibraryManager.library, {
  LBSetDPR: function (mode) {
    try {
      var c = window.LBDPR || {};
      var raw = window.devicePixelRatio || 1;
      var v = mode === 1 ? (c.lobby || Math.min(raw, 2.5)) : mode === 2 ? (c.gameLite || Math.min(raw, 1.5)) : mode === 3 ? (c.gameLow || Math.min(raw, 1.0)) : (c.game || Math.min(raw, 2));
      if (c.force) v = c.force;
      if (Module.devicePixelRatio !== v) { Module.devicePixelRatio = v; console.log("LBDPR " + (["game", "lobby", "game-lite", "game-low"][mode] || mode) + " " + v + " (screen " + raw + ")"); }
      return v;
    } catch (e) { return 1; }
  },
  LBScreenDPR: function () { return window.devicePixelRatio || 1; },
  LBIsMobileUA: function () {
    var ua = (typeof navigator !== "undefined" && navigator.userAgent) ? navigator.userAgent : "";
    return (/iPhone|iPad|iPod|Android/i.test(ua) || (navigator.maxTouchPoints > 1 && /Macintosh/.test(ua))) ? 1 : 0;
  },
  LBIsTeslaUA: function () {
    var ua = (typeof navigator !== "undefined" && navigator.userAgent) ? navigator.userAgent : "";
    return /Tesla|QtCarBrowser/i.test(ua) ? 1 : 0;
  },
  // u6h: safe-area insets (notch / home bar; the page uses viewport-fit=cover) as a fraction of the canvas edge
  // (side 0 top, 1 right, 2 bottom, 3 left). ?lbsafe=T,R,B,L (CSS px) simulates insets for probes.
  LBSafeInset: function (side) {
    try {
      var d = window.__lbSafeDiv;
      if (!d) {
        d = document.createElement("div");
        d.style.cssText = "position:fixed;left:0;top:0;width:0;height:0;visibility:hidden;pointer-events:none;" +
          "padding:env(safe-area-inset-top,0px) env(safe-area-inset-right,0px) env(safe-area-inset-bottom,0px) env(safe-area-inset-left,0px)";
        document.body.appendChild(d); window.__lbSafeDiv = d;
      }
      var cs = getComputedStyle(d);
      var px = parseFloat([cs.paddingTop, cs.paddingRight, cs.paddingBottom, cs.paddingLeft][side]) || 0;
      var q = /[?&]lbsafe=([0-9.,]+)/.exec(location.search);
      if (q) { var a = q[1].split(","); px = Math.max(px, parseFloat(a[side]) || 0); }
      var c = (typeof Module !== "undefined" && Module.canvas) ? Module.canvas : document.getElementById("unity-canvas");
      var r = c.getBoundingClientRect(), dim = (side % 2 === 0) ? r.height : r.width;
      return dim > 0 ? Math.min(0.3, px / dim) : 0;
    } catch (e) { return 0; }
  },
  LBContextAA: function () {
    // lambou6: never call getContext here (on WebGPU that would steal the canvas); only read an existing WebGL context
    try { var g = (typeof GLctx !== "undefined" && GLctx) ? GLctx : (Module.ctx && Module.ctx.getContextAttributes ? Module.ctx : null); if (!g) return -2; var a = g.getContextAttributes(); return a && a.antialias ? (g.getParameter(g.SAMPLES) || 1) : 0; } catch (e) { return -1; }
  }
});
