// Page helpers for the Unity side: CSS safe-area insets and the size of the page's own top-right toolbar
// (fullscreen + "Arcade" buttons), both converted to canvas pixels so the game UI can stay clear of them.
mergeInto(LibraryManager.library, {
  BBPage_Metric: function (which) {
    try {
      var c = (typeof Module !== "undefined" && Module.canvas) ? Module.canvas : document.getElementById("unity-canvas");
      if (!c) return 0;
      var cr = c.getBoundingClientRect();
      var k = c.width / Math.max(1, cr.width);
      if (which < 4) {
        var p = document.getElementById("bb-safe-probe");
        if (!p) {
          p = document.createElement("div");
          p.id = "bb-safe-probe";
          p.style.cssText = "position:fixed;left:0;top:0;width:0;height:0;visibility:hidden;pointer-events:none;" +
            "padding-left:env(safe-area-inset-left,0px);padding-right:env(safe-area-inset-right,0px);" +
            "padding-top:env(safe-area-inset-top,0px);padding-bottom:env(safe-area-inset-bottom,0px);";
          document.body.appendChild(p);
        }
        var cs = window.getComputedStyle(p);
        var v = [cs.paddingLeft, cs.paddingRight, cs.paddingTop, cs.paddingBottom][which];
        return Math.round((parseFloat(v) || 0) * k);
      }
      var fs = document.fullscreenElement || document.webkitFullscreenElement;
      var t = document.querySelector(".tb");
      if (fs || !t || t.offsetParent === null) return 0;
      var r = t.getBoundingClientRect();
      if (which === 4) return Math.max(0, Math.round((cr.right - r.left) * k));   // toolbar width incl. its right margin
      if (which === 5) return Math.max(0, Math.round((r.bottom - cr.top) * k));   // toolbar bottom incl. its top margin
      return 0;
    } catch (e) { return 0; }
  }
});
