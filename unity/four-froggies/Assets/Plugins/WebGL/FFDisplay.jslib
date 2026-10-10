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
      if (Module.devicePixelRatio !== v) { Module.devicePixelRatio = v; console.log("FFDPR " + (mode === 1 ? "lobby" : mode === 2 ? "game-lite" : "game") + " " + v + " (screen " + raw + ")"); }
      return v;
    } catch (e) { return 1; }
  },
  FFScreenDPR: function () { return window.devicePixelRatio || 1; },
  FFIsMobileUA: function () {
    var ua = (typeof navigator !== "undefined" && navigator.userAgent) ? navigator.userAgent : "";
    return (/iPhone|iPad|iPod|Android/i.test(ua) || (navigator.maxTouchPoints > 1 && /Macintosh/.test(ua))) ? 1 : 0;
  }
});
