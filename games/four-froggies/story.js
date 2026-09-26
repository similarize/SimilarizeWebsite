/* Four Froggies — in-world story stubs: phone → Purple Bear, SPS solar map.
   Folded from game-sps reference HUD. Not a separate product. */
(function (global) {
  "use strict";

  var PLANETS = [
    "Mercury", "Venus", "Earth", "Mars",
    "Jupiter", "Saturn", "Uranus", "Neptune",
  ];

  function $(id) {
    return document.getElementById(id);
  }

  function createStory(hooks) {
    hooks = hooks || {};
    var panel = $("story-panel");
    var phoneView = $("story-phone");
    var spsView = $("story-sps");
    var titleEl = $("story-title");
    var speechEl = $("story-speech");
    var statusEl = $("story-status");
    var spsCanvas = $("sps-mini");
    var spsMeta = $("sps-meta");
    var btnClose = $("story-close");
    var btnHangup = $("story-hangup");
    var btnOpenSps = $("story-open-sps");

    var state = {
      open: false,
      mode: null, // phone | sps
      planet: null,
      planetIndex: 0,
      calledPurple: false,
      jimmyAngle: 0,
      confidence: 0,
      jimmyVisible: false,
    };

    function pickPlanet() {
      state.planetIndex = Math.floor(Math.random() * PLANETS.length);
      state.planet = PLANETS[state.planetIndex];
      state.jimmyAngle = Math.random() * Math.PI * 2;
      state.confidence = 0.15;
      state.jimmyVisible = false;
      state.calledPurple = false;
    }

    function show(mode) {
      if (!panel) return;
      state.open = true;
      state.mode = mode;
      panel.hidden = false;
      if (phoneView) phoneView.hidden = mode !== "phone";
      if (spsView) spsView.hidden = mode !== "sps";
      if (titleEl) {
        titleEl.textContent = mode === "phone" ? "Phone" : "SPS · Solar Positioning";
      }
      if (mode === "phone") {
        if (statusEl) statusEl.textContent = "Contacts";
        if (speechEl) {
          speechEl.hidden = true;
          speechEl.textContent = "";
        }
        if (btnHangup) btnHangup.hidden = true;
        if (btnOpenSps) btnOpenSps.hidden = true;
      }
      if (mode === "sps") {
        drawSps();
        updateSpsMeta();
      }
      if (typeof hooks.onOpen === "function") hooks.onOpen(mode);
    }

    function hide() {
      state.open = false;
      state.mode = null;
      if (panel) panel.hidden = true;
      if (typeof hooks.onClose === "function") hooks.onClose();
    }

    function dialPurple() {
      if (!state.planet) pickPlanet();
      if (statusEl) statusEl.textContent = "Calling Purple Bear…";
      if (speechEl) speechEl.hidden = true;
      if (btnHangup) btnHangup.hidden = true;
      if (btnOpenSps) btnOpenSps.hidden = true;
      setTimeout(function () {
        if (!state.open || state.mode !== "phone") return;
        if (statusEl) statusEl.textContent = "Connected · Purple Bear";
        if (speechEl) {
          speechEl.hidden = false;
          speechEl.textContent =
            "Hey James — he's near " + state.planet + ". Check SPS.";
        }
        state.calledPurple = true;
        state.jimmyVisible = true;
        state.confidence = Math.max(state.confidence, 0.35);
        if (btnHangup) btnHangup.hidden = false;
        if (btnOpenSps) btnOpenSps.hidden = false;
        if (typeof hooks.onCalledPurple === "function") {
          hooks.onCalledPurple(state.planet);
        }
      }, 650);
    }

    function peekBlue() {
      if (statusEl) statusEl.textContent = "Blue Bear (nearby, place-bound)";
      if (speechEl) {
        speechEl.hidden = false;
        speechEl.textContent = "…meow.";
      }
      if (btnHangup) btnHangup.hidden = true;
      if (btnOpenSps) btnOpenSps.hidden = true;
    }

    function drawSps() {
      if (!spsCanvas) return;
      var ctx = spsCanvas.getContext("2d");
      var w = spsCanvas.width;
      var h = spsCanvas.height;
      var cx = w / 2;
      var cy = h / 2;
      ctx.clearRect(0, 0, w, h);
      ctx.fillStyle = "#0a1628";
      ctx.fillRect(0, 0, w, h);
      // Sun
      ctx.beginPath();
      ctx.arc(cx, cy, 10, 0, Math.PI * 2);
      ctx.fillStyle = "#fbbf24";
      ctx.fill();
      var orbitR = [28, 40, 52, 66, 84, 100, 114, 126];
      for (var i = 0; i < PLANETS.length; i++) {
        ctx.beginPath();
        ctx.arc(cx, cy, orbitR[i], 0, Math.PI * 2);
        ctx.strokeStyle = "rgba(148,163,184,0.35)";
        ctx.lineWidth = 1;
        ctx.stroke();
        var ang = -Math.PI / 2 + i * ((Math.PI * 2) / PLANETS.length);
        var px = cx + Math.cos(ang) * orbitR[i];
        var py = cy + Math.sin(ang) * orbitR[i];
        ctx.beginPath();
        ctx.arc(px, py, i === 2 ? 4 : 3, 0, Math.PI * 2);
        ctx.fillStyle = i === state.planetIndex && state.jimmyVisible ? "#fb923c" : "#94a3b8";
        ctx.fill();
      }
      if (state.jimmyVisible && state.planet) {
        var jr = orbitR[state.planetIndex];
        var jx = cx + Math.cos(state.jimmyAngle) * jr;
        var jy = cy + Math.sin(state.jimmyAngle) * jr;
        ctx.beginPath();
        ctx.arc(jx, jy, 6, 0, Math.PI * 2);
        ctx.fillStyle = "#4ade80";
        ctx.fill();
        ctx.strokeStyle = "#fff";
        ctx.lineWidth = 1.5;
        ctx.stroke();
        ctx.fillStyle = "#e2e8f0";
        ctx.font = "10px Segoe UI, system-ui, sans-serif";
        ctx.textAlign = "center";
        ctx.fillText("Jimmy", jx, jy - 10);
      } else {
        ctx.fillStyle = "#94a3b8";
        ctx.font = "12px Segoe UI, system-ui, sans-serif";
        ctx.textAlign = "center";
        ctx.fillText("Call Purple Bear for a tip", cx, h - 14);
      }
    }

    function updateSpsMeta() {
      if (!spsMeta) return;
      if (!state.calledPurple) {
        spsMeta.textContent = "No lock yet — call Purple Bear from the ranch house.";
        return;
      }
      var pct = Math.round(state.confidence * 100);
      spsMeta.textContent =
        "Jimmy near " + state.planet + " · confidence " + pct + "% (Optimus kits TBD)";
    }

    function openPhone() {
      if (!state.planet) pickPlanet();
      show("phone");
    }

    function openSps() {
      if (!state.planet) pickPlanet();
      show("sps");
    }

    // Wire DOM once
    if (btnClose) btnClose.addEventListener("click", hide);
    if (btnHangup) {
      btnHangup.addEventListener("click", function () {
        hide();
      });
    }
    if (btnOpenSps) {
      btnOpenSps.addEventListener("click", function () {
        show("sps");
      });
    }
    document.querySelectorAll("[data-story-dial]").forEach(function (btn) {
      btn.addEventListener("click", function () {
        var who = btn.getAttribute("data-story-dial");
        if (who === "purple") dialPurple();
        else if (who === "blue") peekBlue();
      });
    });

    return {
      openPhone: openPhone,
      openSps: openSps,
      hide: hide,
      isOpen: function () {
        return state.open;
      },
      getState: function () {
        return {
          calledPurple: state.calledPurple,
          planet: state.planet,
          confidence: state.confidence,
        };
      },
      ensurePlanet: function () {
        if (!state.planet) pickPlanet();
        return state.planet;
      },
      reset: function () {
        pickPlanet();
        hide();
      },
    };
  }

  global.FroggiesStory = { createStory: createStory, PLANETS: PLANETS };
})(typeof window !== "undefined" ? window : globalThis);
