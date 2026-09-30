/* Four Froggies — version lobby. Canvas, 2.5D Three, and full 3D.
   Does not rebuild the ranch. Cache: 20260929-onelobby1 */
(function () {
  "use strict";

  var lobby = document.getElementById("version-lobby");
  if (!lobby) return;

  function drawPane(canvas, kind, t) {
    var ctx = canvas.getContext("2d");
    var dpr = Math.min(2, window.devicePixelRatio || 1);
    var w = canvas.clientWidth || 240;
    var h = canvas.clientHeight || 140;
    var pw = Math.max(1, Math.floor(w * dpr));
    var ph = Math.max(1, Math.floor(h * dpr));
    if (canvas.width !== pw || canvas.height !== ph) {
      canvas.width = pw;
      canvas.height = ph;
    }
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    if (kind === "canvas") drawCanvas(ctx, w, h, t);
    else if (kind === "three") drawThree(ctx, w, h, t);
    else draw3d(ctx, w, h, t);
  }

  function drawCanvas(ctx, w, h, t) {
    ctx.fillStyle = "#8ecae6";
    ctx.fillRect(0, 0, w, h);
    ctx.fillStyle = "#4ade80";
    ctx.fillRect(0, h * 0.55, w, h * 0.45);
    ctx.fillStyle = "#166534";
    ctx.fillRect(0, h * 0.62, w, h * 0.08);
    ctx.fillStyle = "#a16207";
    ctx.beginPath();
    ctx.moveTo(0, h * 0.78);
    ctx.lineTo(w, h * 0.7);
    ctx.lineTo(w, h);
    ctx.lineTo(0, h);
    ctx.fill();
    ctx.fillStyle = "#f8fafc";
    ctx.font = "700 11px Segoe UI, sans-serif";
    ctx.fillText("2D", 8, 16);
    ctx.fillRect(w * 0.18, h * 0.42, w * 0.22, h * 0.16);
    ctx.fillStyle = "#b91c1c";
    ctx.beginPath();
    ctx.moveTo(w * 0.16, h * 0.42);
    ctx.lineTo(w * 0.29, h * 0.3);
    ctx.lineTo(w * 0.42, h * 0.42);
    ctx.fill();
    truck(ctx, w * 0.58, h * 0.66, w * 0.28, h * 0.16, "#14532d", t);
    frogs(ctx, w * 0.62, h * 0.58, 4, t);
  }

  function drawThree(ctx, w, h, t) {
    var bands = ["#0e7490", "#155e75", "#166534", "#15803d", "#65a30d"];
    for (var i = 0; i < bands.length; i++) {
      ctx.fillStyle = bands[i];
      var y = h * (0.18 + i * 0.12) + Math.sin(t * 0.6 + i) * 2;
      ctx.fillRect(0, y, w, h);
    }
    ctx.fillStyle = "#fef3c7";
    ctx.fillRect(w * 0.08, h * 0.34, w * 0.2, h * 0.22);
    ctx.fillStyle = "#92400e";
    ctx.beginPath();
    ctx.moveTo(w * 0.06, h * 0.34);
    ctx.lineTo(w * 0.18, h * 0.2);
    ctx.lineTo(w * 0.3, h * 0.34);
    ctx.fill();
    ctx.fillStyle = "#1e293b";
    ctx.fillRect(w * 0.22, h * 0.62, w * 0.7, h * 0.06);
    truck(ctx, w * 0.46 + Math.sin(t) * 6, h * 0.5, w * 0.34, h * 0.18, "#334155", t);
    frogs(ctx, w * 0.5, h * 0.42, 4, t);
    ctx.fillStyle = "rgba(255,255,255,.55)";
    ctx.font = "700 11px Segoe UI, sans-serif";
    ctx.fillText("2.5D", 8, 16);
  }

  function draw3d(ctx, w, h, t) {
    var sky = ctx.createLinearGradient(0, 0, 0, h);
    sky.addColorStop(0, "#042f2e");
    sky.addColorStop(0.5, "#115e59");
    sky.addColorStop(1, "#14532d");
    ctx.fillStyle = sky;
    ctx.fillRect(0, 0, w, h);
    ctx.fillStyle = "#064e3b";
    for (var i = 0; i < 5; i++) {
      var x = (i / 5) * w + 8;
      ctx.beginPath();
      ctx.moveTo(x, h * 0.72);
      ctx.lineTo(x + 10, h * 0.38 + (i % 2) * 8);
      ctx.lineTo(x + 20, h * 0.72);
      ctx.fill();
    }
    ctx.fillStyle = "#0f172a";
    ctx.beginPath();
    ctx.moveTo(w * 0.08, h * 0.86);
    ctx.lineTo(w * 0.92, h * 0.86);
    ctx.lineTo(w * 0.78, h * 0.62);
    ctx.lineTo(w * 0.22, h * 0.62);
    ctx.fill();
    ctx.fillStyle = "#cbd5e1";
    ctx.beginPath();
    ctx.moveTo(w * 0.28, h * 0.6);
    ctx.lineTo(w * 0.72, h * 0.6);
    ctx.lineTo(w * 0.66, h * 0.42);
    ctx.lineTo(w * 0.34, h * 0.42);
    ctx.fill();
    ctx.fillStyle = "#0f172a";
    ctx.fillRect(w * 0.4, h * 0.46, w * 0.2, h * 0.08);
    frogs(ctx, w * 0.38, h * 0.36 + Math.sin(t * 2) * 1.5, 4, t * 1.4);
    ctx.fillStyle = "#5eead4";
    ctx.font = "700 11px Segoe UI, sans-serif";
    ctx.fillText("3D", 8, 16);
  }

  function truck(ctx, x, y, bw, bh, color) {
    ctx.fillStyle = color;
    ctx.fillRect(x, y, bw, bh * 0.7);
    ctx.fillStyle = "#e2e8f0";
    ctx.fillRect(x + bw * 0.15, y + bh * 0.12, bw * 0.7, bh * 0.28);
    ctx.fillStyle = "#111";
    ctx.beginPath();
    ctx.arc(x + bw * 0.22, y + bh * 0.72, bh * 0.28, 0, Math.PI * 2);
    ctx.arc(x + bw * 0.78, y + bh * 0.72, bh * 0.28, 0, Math.PI * 2);
    ctx.fill();
  }

  function frogs(ctx, x, y, n, t) {
    var colors = ["#4ade80", "#fb923c", "#60a5fa", "#c084fc"];
    for (var i = 0; i < n; i++) {
      ctx.fillStyle = colors[i];
      ctx.beginPath();
      ctx.arc(x + i * 14, y + Math.sin(t * 3 + i) * 2, 5, 0, Math.PI * 2);
      ctx.fill();
    }
  }

  function paint(t) {
    var shots = lobby.querySelectorAll("canvas[data-shot]");
    for (var i = 0; i < shots.length; i++) {
      drawPane(shots[i], shots[i].getAttribute("data-shot"), t || 0);
    }
  }

  var started = performance.now();
  function frame(now) {
    if (lobby.hidden) return;
    paint((now - started) / 1000);
    requestAnimationFrame(frame);
  }

  function showLobby() {
    lobby.hidden = false;
    requestAnimationFrame(frame);
  }

  function enterRanch(engine) {
    if (window.FroggiesEngines && FroggiesEngines.stopAltEngines) FroggiesEngines.stopAltEngines();
    if (window.FroggiesCanon && FroggiesCanon.setEngine) FroggiesCanon.setEngine(engine);
    if (window.FroggiesEngines && FroggiesEngines.paintPicker) FroggiesEngines.paintPicker();
    lobby.hidden = true;
    var sub = document.getElementById("overlay-sub");
    if (sub) {
      sub.textContent = engine === "three"
        ? "2.5D · fixed angle · claim a seat · GO"
        : "2D · ranch · party · claim a seat · GO";
    }
  }

  lobby.addEventListener("click", function (e) {
    var btn = e.target.closest("[data-version]");
    if (!btn) return;
    var id = btn.getAttribute("data-version");
    if (id === "3d") {
      location.href = "/games/four-froggies-3d/?v=20260929-onelobby1";
      return;
    }
    enterRanch(id === "three" ? "three" : "canvas");
  });

  var back = document.getElementById("btn-versions");
  if (back) {
    back.addEventListener("click", function () {
      if (window.FroggiesEngines && FroggiesEngines.stopAltEngines) FroggiesEngines.stopAltEngines();
      var overlay = document.getElementById("overlay");
      if (overlay) overlay.hidden = false;
      document.body.classList.add("in-title");
      document.body.classList.remove("in-hub");
      document.body.classList.remove("in-space");
      showLobby();
    });
  }

  var skip = false;
  try {
    var q = new URLSearchParams(location.search);
    if (q.get("engine") === "3d") {
      location.replace("/games/four-froggies-3d/?v=20260929-onelobby1");
      return;
    }
    if (q.get("engine") || q.get("go") === "1") skip = true;
  } catch (err) { /* keep lobby */ }

  if (skip) lobby.hidden = true;
  else showLobby();
  paint(0);
  window.addEventListener("resize", function () { paint(0); });
})();
