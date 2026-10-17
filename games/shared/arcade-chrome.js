/*! Similarize Arcade chrome (ac1) — consistent "← Arcade" Exit + fullscreen toggle for every cabinet.
 * <script src="/games/shared/arcade-chrome.js?v=ac2" data-pos="top:8px;left:8px"></script>
 * Skips the Exit pill if the page already has a link back to /games/. Esc-free: B/Select on a pad is left to the game.
 */
(function () {
  "use strict";
  var me = document.currentScript, pos = (me && me.getAttribute("data-pos")) || "top:8px;left:8px";
  function css() {
    if (document.getElementById("ac1-css")) return;
    var s = document.createElement("style"); s.id = "ac1-css";
    s.textContent = ".ac1-bar{position:fixed;" + pos + ";z-index:2147483000;display:flex;gap:6px;font:600 13px Outfit,system-ui,sans-serif}" +
      ".ac1-bar a,.ac1-bar button{all:unset;cursor:pointer;padding:6px 12px;border-radius:999px;background:rgba(16,14,12,.72);color:#fff;border:1.5px solid rgba(255,255,255,.55);backdrop-filter:blur(4px);-webkit-tap-highlight-color:transparent}" +
      ".ac1-bar a:hover,.ac1-bar button:hover{border-color:#008bff}.ac1-bar a:focus-visible,.ac1-bar button:focus-visible{outline:2px solid #008bff}" +
      "@media (display-mode:fullscreen){.ac1-fs{display:none}}";
    document.head.appendChild(s);
  }
  function hasExit() {
    var as = document.querySelectorAll("a[href]");
    for (var i = 0; i < as.length; i++) { var h = as[i].getAttribute("href") || ""; if (/^(\/games\/?(index\.html)?(\?.*)?|\.\.\/(index\.html)?)$/.test(h) && as[i].offsetParent !== null) return true; }
    return false;
  }
  function hasFs() {
    var els = document.querySelectorAll("button,a,[role=button],[data-fullscreen],[id*=full i],[class*=full i]");
    for (var i = 0; i < els.length; i++) { var e = els[i];
      if (e.closest && e.closest(".ac1-bar")) continue;
      var t = [e.id, e.className && e.className.baseVal === undefined ? e.className : "", e.title, e.getAttribute("aria-label"), e.textContent].join(" ");
      if (/full ?screen|\u26F6|\u26f6|⛶/i.test(t)) return true; }
    return false;
  }
  function build() {
    css();
    var bar = document.createElement("div"); bar.className = "ac1-bar";
    if (!hasExit()) { var a = document.createElement("a"); a.href = "/games/"; a.textContent = "\u2190 Arcade"; a.setAttribute("aria-label", "Exit to the Similarize Arcade"); bar.appendChild(a); }
    var de = document.documentElement;
    if ((de.requestFullscreen || de.webkitRequestFullscreen) && !hasFs()) {
      var b = document.createElement("button"); b.className = "ac1-fs"; b.type = "button"; b.textContent = "\u26F6"; b.title = "Fullscreen"; b.setAttribute("aria-label", "Toggle fullscreen");
      b.onclick = function () {
        var fs = document.fullscreenElement || document.webkitFullscreenElement;
        if (fs) (document.exitFullscreen || document.webkitExitFullscreen).call(document);
        else (de.requestFullscreen || de.webkitRequestFullscreen).call(de);
      };
      bar.appendChild(b);
    }
    if (bar.children.length) document.body.appendChild(bar);
  }
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", function () { setTimeout(build, 50); }); else setTimeout(build, 50);
})();
