// ffu17 display: Unity WebGL reads Module.devicePixelRatio every frame to size the canvas backbuffer
// (JS_SystemInfo_GetPreferredDevicePixelRatio), so the game can render the lobby at the screen's real pixel density and
// drop to the lighter gameplay density on phones. The page (web/index.html) publishes its caps in window.FFDPR.
mergeInto(LibraryManager.library, {
  FFSetDPR: function (mode) {
    try {
      var c = window.FFDPR || {};
      var raw = window.devicePixelRatio || 1;
      var v = mode === 1 ? (c.lobby || Math.min(raw, 2.5)) : mode === 2 ? (c.gameLite || Math.min(raw, 1.5)) : (c.game || Math.min(raw, 2));
      if (c.force) v = c.force;
      else if (window.FFDPRcap > 0) v = Math.min(v, window.FFDPRcap);   // ffu60 quality tier cap (Perf60)
      if (Module.devicePixelRatio !== v) { Module.devicePixelRatio = v; console.log("FFDPR " + (mode === 1 ? "lobby" : mode === 2 ? "game-lite" : "game") + " " + v + " (screen " + raw + ")"); }
      return v;
    } catch (e) { return 1; }
  },
  FFScreenDPR: function () { return window.devicePixelRatio || 1; },
  // ffu60: quality tier pixel-density cap (0 = none); re-applies the current mode's density right away
  FFSetDPRCap__deps: ['FFSetDPR'],
  FFSetDPRCap: function (cap, mode) {
    try { window.FFDPRcap = cap > 0 ? cap : 0; return _FFSetDPR(mode); } catch (e) { return 1; }
  },
  FFCurDPR: function () { try { return Module.devicePixelRatio || window.devicePixelRatio || 1; } catch (e) { return 1; } },
  // ffu60: the governor's last tier for this browser (the page reads it before the next load: low = no canvas MSAA)
  FFPerfSave: function (tier) { try { localStorage.setItem("ffq60", String(tier)); } catch (e) { } },
  FFPerfLoad: function () { try { var v = localStorage.getItem("ffq60"); return v === null ? -1 : (parseInt(v, 10) | 0); } catch (e) { return -1; } },
  FFIsMobileUA: function () {
    var ua = (typeof navigator !== "undefined" && navigator.userAgent) ? navigator.userAgent : "";
    return (/iPhone|iPad|iPod|Android/i.test(ua) || (navigator.maxTouchPoints > 1 && /Macintosh/.test(ua))) ? 1 : 0;
  }
});
