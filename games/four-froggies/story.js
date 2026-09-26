/* Four Froggies — in-world story: phone → Purple Bear → SPS → Optimus → bring Jimmy home.
   Folded from game-sps reference HUD. Not a separate product.
   polish6: playful Purple Bear phone UI; SPS map flash on Map ping (dish);
   Jimmy rogue hint when Optimus kits active; hang TBD hooks (no new systems). */
(function (global) {
  "use strict";

  var PLANETS = [
    "Mercury", "Venus", "Earth", "Mars",
    "Jupiter", "Saturn", "Uranus", "Neptune",
  ];

  var ORBIT_R = [28, 40, 52, 66, 84, 100, 114, 126];
  var KIT_CD = { hover: 2.2, rocket: 3.5, afterburners: 3.2, drone: 4.5, map: 3.8 };
  var KIT_MOVE = { hover: 16, rocket: 36, afterburners: 28, drone: 0, map: 0 };
  var ROUND_SECONDS = 55;

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
    var kitRow = $("optimus-kits");
    var btnBring = $("story-bring-home");
    var btnRetry = $("story-retry");
    var confEl = $("sps-confidence");
    var timerEl = $("sps-timer");
    var feedbackEl = $("sps-feedback");

    var state = {
      open: false,
      mode: null,
      planet: null,
      planetIndex: 0,
      calledPurple: false,
      jimmyAngle: 0,
      jimmyR: 66,
      confidence: 0,
      jimmyVisible: false,
      jimmyRevealed: false,
      seeker: { x: 140, y: 140 },
      cds: { hover: 0, rocket: 0, afterburners: 0, drone: 0, map: 0 },
      timeLeft: ROUND_SECONDS,
      running: false,
      won: false,
      failed: false,
      lastAbility: "",
      raf: 0,
      lastTs: 0,
      mapFlash: 0,
      rogueHintShown: false,
      hangTbd: null, /* polish6: TBD hang hook slot — do not invent hang systems */
    };

    function pickPlanet() {
      state.planetIndex = Math.floor(Math.random() * PLANETS.length);
      state.planet = PLANETS[state.planetIndex];
      state.jimmyAngle = Math.random() * Math.PI * 2;
      state.jimmyR = ORBIT_R[state.planetIndex];
      state.confidence = 0.12;
      state.jimmyVisible = false;
      state.jimmyRevealed = false;
      state.calledPurple = false;
      state.seeker = { x: 140, y: 140 };
      state.cds = { hover: 0, rocket: 0, afterburners: 0, drone: 0, map: 0 };
      state.timeLeft = ROUND_SECONDS;
      state.running = false;
      state.won = false;
      state.failed = false;
      state.lastAbility = "";
    }

    function jimmyPos() {
      var w = spsCanvas ? spsCanvas.width : 280;
      var h = spsCanvas ? spsCanvas.height : 280;
      var cx = w / 2;
      var cy = h / 2;
      return {
        x: cx + Math.cos(state.jimmyAngle) * state.jimmyR,
        y: cy + Math.sin(state.jimmyAngle) * state.jimmyR,
      };
    }

    function dist(a, b) {
      return Math.hypot(a.x - b.x, a.y - b.y);
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
        if (statusEl) statusEl.textContent = "Contacts · tap Purple Bear!";
        if (speechEl) {
          speechEl.hidden = true;
          speechEl.textContent = "";
          speechEl.classList.remove("speech-bounce");
        }
        if (btnHangup) {
          btnHangup.hidden = true;
          btnHangup.textContent = "Got it";
        }
        if (btnOpenSps) btnOpenSps.hidden = true;
        setPhonePlayful(true); /* playful while phone open near Purple Bear path */
        stopLoop();
      } else {
        setPhonePlayful(false);
      }
      if (mode === "sps") {
        if (state.calledPurple && !state.won && !state.failed) {
          state.running = true;
          startLoop();
        }
        drawSps();
        updateSpsMeta();
        paintKits();
      }
      if (typeof hooks.onOpen === "function") hooks.onOpen(mode);
    }

    function hide() {
      state.open = false;
      state.mode = null;
      state.running = false;
      stopLoop();
      if (panel) panel.hidden = true;
      setPhonePlayful(false);
      if (speechEl) speechEl.classList.remove("speech-bounce");
      if (typeof hooks.onClose === "function") hooks.onClose();
    }

    function startLoop() {
      stopLoop();
      state.lastTs = 0;
      function frame(ts) {
        if (!state.open || state.mode !== "sps") return;
        var dt = state.lastTs ? Math.min(0.05, (ts - state.lastTs) / 1000) : 0;
        state.lastTs = ts;
        tick(dt);
        drawSps();
        updateSpsMeta();
        paintKits();
        state.raf = requestAnimationFrame(frame);
      }
      state.raf = requestAnimationFrame(frame);
    }

    function stopLoop() {
      if (state.raf) {
        cancelAnimationFrame(state.raf);
        state.raf = 0;
      }
    }

    function tick(dt) {
      if (state.mapFlash > 0) state.mapFlash = Math.max(0, state.mapFlash - dt * 1.6);
      if (!state.running || state.won || state.failed) return;
      state.jimmyAngle += dt * 0.55;
      var keys = Object.keys(state.cds);
      for (var i = 0; i < keys.length; i++) {
        if (state.cds[keys[i]] > 0) state.cds[keys[i]] = Math.max(0, state.cds[keys[i]] - dt);
      }
      state.timeLeft -= dt;
      if (state.timeLeft <= 0) {
        softFail();
        return;
      }
      var jp = jimmyPos();
      if (dist(state.seeker, jp) < 18 && state.confidence >= 0.5) {
        win();
      }
    }

    function softFail() {
      state.failed = true;
      state.running = false;
      if (feedbackEl) {
        feedbackEl.textContent =
          "Jimmy's on " + state.planet + " again… soft fail — retry!";
      }
      if (btnRetry) btnRetry.hidden = false;
      if (btnBring) btnBring.hidden = true;
      if (typeof hooks.onSoftFail === "function") hooks.onSoftFail(state.planet);
    }

    function win() {
      state.won = true;
      state.running = false;
      state.confidence = 1;
      if (feedbackEl) {
        feedbackEl.textContent = "Got him! Jimmy's coming home from " + state.planet + ".";
      }
      if (btnBring) btnBring.hidden = true;
      if (btnRetry) btnRetry.hidden = false;
      if (typeof hooks.onWin === "function") hooks.onWin(state.planet);
    }

    function setPhonePlayful(on) {
      if (panel) panel.classList.toggle("playful-phone", !!on);
      if (phoneView) phoneView.classList.toggle("playful-phone", !!on);
      var purpleBtn = document.querySelector('[data-story-dial="purple"]');
      if (purpleBtn) purpleBtn.classList.toggle("purple-pulse", !!on);
    }

    function dialPurple() {
      if (!state.planet) pickPlanet();
      if (statusEl) statusEl.textContent = "Calling Purple Bear… ✦";
      if (speechEl) speechEl.hidden = true;
      if (btnHangup) btnHangup.hidden = true;
      if (btnOpenSps) btnOpenSps.hidden = true;
      setPhonePlayful(true);
      if (typeof hooks.onPhonePlayful === "function") hooks.onPhonePlayful(true);
      setTimeout(function () {
        if (!state.open || state.mode !== "phone") return;
        if (statusEl) statusEl.textContent = "Connected · Purple Bear · ʕ·ᴥ·ʔ";
        if (speechEl) {
          speechEl.hidden = false;
          speechEl.textContent =
            "Hey James — he's near " + state.planet + "! Check SPS. Call Optimus kits! ✨";
          speechEl.classList.add("speech-bounce");
        }
        state.calledPurple = true;
        state.jimmyVisible = true;
        state.confidence = Math.max(state.confidence, 0.32);
        if (btnHangup) {
          btnHangup.hidden = false;
          btnHangup.textContent = "Got it · hang (TBD)";
        }
        if (btnOpenSps) btnOpenSps.hidden = false;
        if (typeof hooks.onCalledPurple === "function") {
          hooks.onCalledPurple(state.planet);
        }
        /* polish6: hang TBD hook — reserved, no new hang system */
        if (typeof hooks.onHangTbd === "function") {
          hooks.onHangTbd({ ready: true, who: "purple", planet: state.planet });
        }
        state.hangTbd = { ready: true, who: "purple", planet: state.planet };
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

    function moveSeekerTowardJimmy(amount) {
      var jp = jimmyPos();
      var dx = jp.x - state.seeker.x;
      var dy = jp.y - state.seeker.y;
      var d = Math.hypot(dx, dy) || 1;
      state.seeker.x += (dx / d) * amount;
      state.seeker.y += (dy / d) * amount;
    }

    function useKit(name) {
      if (!state.calledPurple || state.won || state.failed) return;
      if (state.cds[name] > 0) return;
      if (!state.running) {
        state.running = true;
        startLoop();
      }
      state.cds[name] = KIT_CD[name] || 3;
      state.lastAbility = name;

      if (name === "hover") {
        moveSeekerTowardJimmy(KIT_MOVE.hover);
        state.confidence = Math.min(1, state.confidence + 0.09);
        if (feedbackEl) feedbackEl.textContent = "Hover — Optimus glides closer!";
      } else if (name === "rocket") {
        moveSeekerTowardJimmy(KIT_MOVE.rocket);
        state.confidence = Math.min(1, state.confidence + 0.14);
        if (feedbackEl) feedbackEl.textContent = "Rocket boost!";
      } else if (name === "afterburners") {
        moveSeekerTowardJimmy(KIT_MOVE.afterburners);
        state.confidence = Math.min(1, state.confidence + 0.11);
        if (feedbackEl) feedbackEl.textContent = "Afterburners — whoosh!";
      } else if (name === "drone") {
        state.jimmyRevealed = true;
        state.jimmyVisible = true;
        state.confidence = Math.min(1, state.confidence + 0.22);
        // Snap a little toward Jimmy
        moveSeekerTowardJimmy(10);
        if (feedbackEl) feedbackEl.textContent = "Drone scout — Jimmy spotted!";
      } else if (name === "map") {
        state.jimmyRevealed = true;
        state.jimmyVisible = true;
        state.confidence = Math.min(1, state.confidence + 0.28);
        if (state.confidence >= 0.45) state.jimmyRevealed = true;
        if (feedbackEl) feedbackEl.textContent = "Map ping · dish flash — SPS lock rising!";
        /* polish6: SPS map flash when dish / Map ping used */
        state.mapFlash = 0.55;
        if (spsCanvas) {
          spsCanvas.classList.remove("map-flash");
          void spsCanvas.offsetWidth;
          spsCanvas.classList.add("map-flash");
        }
        if (typeof hooks.onMapFlash === "function") hooks.onMapFlash(state.confidence);
      }

      if (typeof hooks.onKit === "function") hooks.onKit(name, state.confidence);

      var jp = jimmyPos();
      if (dist(state.seeker, jp) < 20 && state.confidence >= 0.5) {
        win();
      }
      drawSps();
      updateSpsMeta();
      paintKits();
    }

    function tryBringHome() {
      if (state.confidence >= 0.72 || dist(state.seeker, jimmyPos()) < 22) {
        win();
      } else if (feedbackEl) {
        feedbackEl.textContent = "Need a stronger SPS lock — use Optimus kits!";
      }
    }

    function retry() {
      var oldPlanet = state.planet;
      pickPlanet();
      // Keep the rogue loop going — new planet, funny soft retry
      state.calledPurple = true;
      state.jimmyVisible = true;
      state.confidence = 0.28;
      state.running = true;
      if (feedbackEl) {
        feedbackEl.textContent =
          "Jimmy hopped again — now near " + state.planet + "! (was " + oldPlanet + ")";
      }
      if (btnRetry) btnRetry.hidden = true;
      if (btnBring) btnBring.hidden = false;
      if (typeof hooks.onRetry === "function") hooks.onRetry(state.planet);
      startLoop();
      drawSps();
      updateSpsMeta();
      paintKits();
    }

    function paintKits() {
      if (!kitRow) return;
      var show = state.calledPurple && state.mode === "sps";
      kitRow.hidden = !show;
      kitRow.classList.toggle("kits-active", !!show);
      if (btnBring) btnBring.hidden = !(show && !state.won && !state.failed);
      if (btnRetry) {
        btnRetry.hidden = !(state.won || state.failed);
      }
      /* polish6: Jimmy rogue hint when Optimus kit area active */
      if (show && !state.won && !state.failed) {
        var hint = document.getElementById("jimmy-rogue-hint");
        if (!hint) {
          hint = document.createElement("p");
          hint.id = "jimmy-rogue-hint";
          hint.className = "jimmy-rogue-hint";
          kitRow.insertBefore(hint, kitRow.firstChild);
        }
        hint.hidden = false;
        hint.textContent = "Jimmy's gone rogue near " + (state.planet || "…") + " — Optimus kits ready!";
        if (!state.rogueHintShown) {
          state.rogueHintShown = true;
          if (typeof hooks.onRogueHint === "function") {
            hooks.onRogueHint(state.planet);
          }
        }
      } else {
        var hintOff = document.getElementById("jimmy-rogue-hint");
        if (hintOff) hintOff.hidden = true;
      }
      kitRow.querySelectorAll("[data-kit]").forEach(function (btn) {
        var name = btn.getAttribute("data-kit");
        var cd = state.cds[name] || 0;
        btn.disabled = cd > 0 || state.won || state.failed || !state.calledPurple;
        var label = btn.getAttribute("data-label") || name;
        btn.textContent = cd > 0 ? label + " " + Math.ceil(cd) + "s" : label;
        btn.classList.toggle("ready", cd <= 0 && !state.won && !state.failed);
      });
    }

    function drawSps() {
      if (!spsCanvas) return;
      var ctx = spsCanvas.getContext("2d");
      var w = spsCanvas.width;
      var h = spsCanvas.height;
      var cx = w / 2;
      var cy = h / 2;
      ctx.clearRect(0, 0, w, h);

      // Space bg
      var bg = ctx.createRadialGradient(cx, cy, 10, cx, cy, w * 0.7);
      bg.addColorStop(0, "#122038");
      bg.addColorStop(1, "#060d18");
      ctx.fillStyle = bg;
      ctx.fillRect(0, 0, w, h);
      /* polish6: dish / map-ping flash wash */
      if (state.mapFlash > 0) {
        ctx.fillStyle = "rgba(251, 191, 36, " + (state.mapFlash * 0.55) + ")";
        ctx.fillRect(0, 0, w, h);
        ctx.strokeStyle = "rgba(254, 243, 199, " + (state.mapFlash * 0.9) + ")";
        ctx.lineWidth = 4;
        ctx.strokeRect(4, 4, w - 8, h - 8);
      }

      // Stars
      ctx.fillStyle = "rgba(255,255,255,0.5)";
      for (var s = 0; s < 28; s++) {
        var sx = (s * 47 + 13) % w;
        var sy = (s * 73 + 29) % h;
        ctx.fillRect(sx, sy, 1.5, 1.5);
      }

      // Sun
      var sunG = ctx.createRadialGradient(cx, cy, 2, cx, cy, 16);
      sunG.addColorStop(0, "#fde68a");
      sunG.addColorStop(1, "#f59e0b");
      ctx.beginPath();
      ctx.arc(cx, cy, 11, 0, Math.PI * 2);
      ctx.fillStyle = sunG;
      ctx.fill();

      for (var i = 0; i < PLANETS.length; i++) {
        ctx.beginPath();
        ctx.arc(cx, cy, ORBIT_R[i], 0, Math.PI * 2);
        ctx.strokeStyle =
          i === state.planetIndex && state.jimmyVisible
            ? "rgba(251, 146, 60, 0.55)"
            : "rgba(148,163,184,0.28)";
        ctx.lineWidth = i === state.planetIndex && state.jimmyVisible ? 1.8 : 1;
        ctx.stroke();

        var ang = -Math.PI / 2 + i * ((Math.PI * 2) / PLANETS.length);
        var px = cx + Math.cos(ang) * ORBIT_R[i];
        var py = cy + Math.sin(ang) * ORBIT_R[i];
        ctx.beginPath();
        ctx.arc(px, py, i === 2 ? 4.5 : 3, 0, Math.PI * 2);
        ctx.fillStyle =
          i === state.planetIndex && state.jimmyVisible ? "#fb923c" : "#94a3b8";
        ctx.fill();
      }

      // Seeker (Optimus)
      if (state.calledPurple) {
        ctx.beginPath();
        ctx.arc(
          state.seeker.x,
          state.seeker.y,
          12 + state.confidence * 22,
          0,
          Math.PI * 2
        );
        ctx.strokeStyle =
          "rgba(61,255,154," + (0.15 + state.confidence * 0.5) + ")";
        ctx.lineWidth = 2;
        ctx.stroke();
        ctx.beginPath();
        ctx.arc(state.seeker.x, state.seeker.y, 7, 0, Math.PI * 2);
        ctx.fillStyle = "#94a3b8";
        ctx.fill();
        ctx.fillStyle = "#e2e8f0";
        ctx.font = "9px Segoe UI, system-ui, sans-serif";
        ctx.textAlign = "center";
        ctx.fillText("Optimus", state.seeker.x, state.seeker.y - 12);
      }

      // Jimmy blip
      var showBlip =
        state.jimmyVisible || state.jimmyRevealed || state.confidence > 0.25;
      if (showBlip && state.planet) {
        var jp = jimmyPos();
        var jitter =
          state.confidence < 0.5 && !state.jimmyRevealed
            ? (1 - state.confidence) * 12
            : 0;
        var bx = jp.x + Math.sin(state.jimmyAngle * 3) * jitter;
        var by = jp.y + Math.cos(state.jimmyAngle * 2) * jitter;
        ctx.beginPath();
        ctx.arc(bx, by, 6 + (1 - state.confidence) * 3, 0, Math.PI * 2);
        ctx.fillStyle =
          "rgba(74, 222, 128, " + (0.4 + state.confidence * 0.55) + ")";
        ctx.fill();
        ctx.strokeStyle = "#fff";
        ctx.lineWidth = 1.5;
        ctx.stroke();
        if (state.confidence > 0.35 || state.jimmyRevealed) {
          ctx.fillStyle = "#e2e8f0";
          ctx.font = "10px Segoe UI, system-ui, sans-serif";
          ctx.textAlign = "center";
          ctx.fillText("Jimmy", bx, by - 11);
        }
      } else {
        ctx.fillStyle = "#94a3b8";
        ctx.font = "12px Segoe UI, system-ui, sans-serif";
        ctx.textAlign = "center";
        ctx.fillText("Call Purple Bear for a tip", cx, h - 14);
      }
    }

    function updateSpsMeta() {
      if (timerEl) {
        if (state.calledPurple && !state.won && !state.failed) {
          timerEl.textContent = Math.max(0, Math.ceil(state.timeLeft)) + "s";
        } else if (state.won) timerEl.textContent = "Home!";
        else if (state.failed) timerEl.textContent = "Again…";
        else timerEl.textContent = "";
      }
      if (confEl) {
        var pct = Math.round(state.confidence * 100);
        confEl.textContent = state.calledPurple ? "Lock " + pct + "%" : "No lock";
        confEl.style.width = Math.max(8, pct) + "%";
      }
      if (!spsMeta) return;
      if (!state.calledPurple) {
        spsMeta.textContent = "No lock yet — call Purple Bear from the ranch house.";
        return;
      }
      if (state.won) {
        spsMeta.textContent = "Jimmy home from " + state.planet + "!";
        return;
      }
      if (state.failed) {
        spsMeta.textContent = "Jimmy's on " + state.planet + " again…";
        return;
      }
      spsMeta.textContent =
        "Jimmy near " + state.planet + " · Optimus kits ready";
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
    if (btnBring) {
      btnBring.addEventListener("click", function () {
        tryBringHome();
      });
    }
    if (btnRetry) {
      btnRetry.addEventListener("click", function () {
        retry();
      });
    }
    document.querySelectorAll("[data-story-dial]").forEach(function (btn) {
      btn.addEventListener("click", function () {
        var who = btn.getAttribute("data-story-dial");
        if (who === "purple") dialPurple();
        else if (who === "blue") peekBlue();
      });
    });
    document.querySelectorAll("[data-kit]").forEach(function (btn) {
      btn.addEventListener("click", function () {
        useKit(btn.getAttribute("data-kit"));
        if (typeof hooks.onBeep === "function") hooks.onBeep(btn.getAttribute("data-kit"));
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
          won: state.won,
          failed: state.failed,
          timeLeft: state.timeLeft,
          mapFlash: state.mapFlash,
          hangTbd: state.hangTbd,
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
