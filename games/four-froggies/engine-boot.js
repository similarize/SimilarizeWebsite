/* Four Froggies — thin lobby engine switcher + CDN boot (phaser-hub / three-hub).
   Canvas path stays default (main.js + PeerJS + world.js). Do not touch world.js scale.
   Phaser/Three = solo-first physics/look compare. */
(function (global) {
  "use strict";

  var C = global.FroggiesCanon;
  var CACHE = "20260925-polish8";
  var CDN = {
    phaser: "https://cdn.jsdelivr.net/npm/phaser@3.87.0/dist/phaser.min.js",
    three: "https://cdnjs.cloudflare.com/ajax/libs/three.js/r134/three.min.js",
  };
  var loading = null;
  var currentEngine = "canvas";
  var engineRunning = false;

  function $(id) {
    return document.getElementById(id);
  }

  function loadScript(url) {
    return new Promise(function (resolve, reject) {
      var s = document.createElement("script");
      s.src = url;
      s.async = true;
      s.onload = function () { resolve(); };
      s.onerror = function () { reject(new Error("Failed to load " + url)); };
      document.head.appendChild(s);
    });
  }

  function ensurePhaser() {
    var chain = Promise.resolve();
    if (!global.Phaser) {
      chain = chain.then(function () { return loadScript(CDN.phaser); });
    }
    return chain.then(function () {
      if (global.FroggiesPhaser) return;
      return loadScript("phaser-hub.js?v=" + CACHE);
    });
  }

  function ensureThree() {
    var chain = Promise.resolve();
    if (!global.THREE) {
      chain = chain.then(function () { return loadScript(CDN.three); });
    }
    return chain.then(function () {
      if (global.FroggiesThree) return;
      return loadScript("three-hub.js?v=" + CACHE);
    });
  }

  function paintPicker() {
    var root = $("engine-pick");
    if (!root || !C) return;
    var mode = C.getEngine();
    currentEngine = mode;
    var btns = root.querySelectorAll("[data-engine]");
    for (var i = 0; i < btns.length; i++) {
      var b = btns[i];
      var on = b.getAttribute("data-engine") === mode;
      b.classList.toggle("is-on", on);
      b.setAttribute("aria-pressed", on ? "true" : "false");
    }
    var note = $("engine-note");
    if (note) {
      if (mode === "canvas") {
        note.textContent = "Canvas · default · Host/Join (PeerJS) works here";
      } else if (mode === "phaser") {
        note.textContent = "Phaser 3 · solo-first (party sync not wired)";
      } else {
        note.textContent = "three.js · fixed-angle 2.5D · solo-first";
      }
    }
    // Soft-hide party when not canvas (still visible but noted)
    var partyBar = $("party-bar");
    var inviteCta = $("invite-cta");
    if (partyBar) partyBar.style.opacity = mode === "canvas" ? "" : "0.45";
    if (inviteCta && mode !== "canvas") {
      /* leave text; note covers it */
    }
  }

  function stopAltEngines() {
    engineRunning = false;
    if (global.FroggiesPhaser && global.FroggiesPhaser.isActive()) {
      global.FroggiesPhaser.destroy();
    }
    if (global.FroggiesThree && global.FroggiesThree.isActive()) {
      global.FroggiesThree.destroy();
    }
    var host = $("engine-host");
    if (host) {
      host.hidden = true;
      host.innerHTML = "";
    }
    var view = $("view");
    if (view) view.style.display = "";
  }

  function wireHud(api) {
    var livesEl = $("lives");
    var scrapEl = $("scrap");
    var progressBar = $("progress-bar");
    var progressLabel = $("progress-label");
    var tipEl = $("hub-tip");
    var btnAbility = $("btn-ability");
    var btnInteract = $("btn-interact");
    var btnEscape = $("btn-escape");

    function flashAbility(abilityName) {
      if (!btnAbility) return;
      var kind = String(abilityName || "DASH").toLowerCase();
      btnAbility.classList.remove("fire-dash", "fire-shield", "fire-zap", "fire-bot", "ability-fired");
      void btnAbility.offsetWidth;
      btnAbility.classList.add("fire-" + kind, "ability-fired");
      setTimeout(function () {
        btnAbility.classList.remove("fire-" + kind, "ability-fired");
      }, 480);
    }
    return {
      onHud: function (h) {
        if (livesEl) livesEl.textContent = h.walk || "🐸 Walk";
        if (scrapEl) scrapEl.textContent = (h.mode === "space" ? "Catches " : "Scrap ") + (h.scrap | 0);
        if (progressBar) progressBar.style.width = Math.min(100, (h.scrap | 0) * 8) + "%";
        if (progressLabel) progressLabel.textContent = h.label || "";
        if (tipEl) tipEl.textContent = h.tip || "";
        if (btnInteract) {
          btnInteract.classList.toggle("ready", !!h.near);
          btnInteract.disabled = !h.near;
        }
        if (btnAbility) {
          var cd = h.cd || 0;
          btnAbility.textContent = cd > 0 ? h.ability + " " + Math.ceil(cd) + "s" : h.ability;
          btnAbility.classList.toggle("ready", cd <= 0);
          btnAbility.classList.toggle("cd", cd > 0);
          if (h.ability) btnAbility.dataset.roleTbd = h.ability;
        }
        if (btnEscape) {
          var showEsc = !!(h.mode === "space" && h.inOrbit);
          btnEscape.hidden = !showEsc;
          btnEscape.classList.toggle("ready", showEsc);
        }
      },
      onToast: function (t) {
        if (tipEl) tipEl.textContent = t || "";
      },
      onAbilityFire: function (frogId, abilityName) {
        flashAbility(abilityName);
      },
      onReady: function () {
        /* ok */
      },
    };
  }

  function showPlayingChrome() {
    var overlay = $("overlay");
    if (overlay) overlay.hidden = true;
    document.body.classList.add("in-hub");
    document.body.classList.remove("in-title");
    var frogPick = $("frog-pick");
    if (frogPick) frogPick.hidden = true;
    var inviteCta = $("invite-cta");
    if (inviteCta) inviteCta.hidden = true;
    var partyBar = $("party-bar");
    if (partyBar) partyBar.hidden = true;
    var inviteQr = $("invite-qr");
    if (inviteQr) inviteQr.hidden = true;
  }

  function selectedFrogId() {
    var on = document.querySelector(".frog-btn.selected, .frog-btn.seat-you");
    if (on && on.getAttribute("data-id")) return on.getAttribute("data-id");
    // fallback from main's selected buttons
    var pressed = document.querySelector(".frog-btn[data-id].seat-you");
    if (pressed) return pressed.getAttribute("data-id");
    var any = document.querySelector(".frog-btn[data-id]");
    // Check which has selected class from canvas lobby
    var btns = document.querySelectorAll(".frog-btn[data-id]");
    for (var i = 0; i < btns.length; i++) {
      if (btns[i].classList.contains("selected") || btns[i].classList.contains("seat-you")) {
        return btns[i].getAttribute("data-id");
      }
    }
    return "james";
  }

  function startAlt(mode) {
    if (loading) return loading;
    showPlayingChrome();
    var frogId = selectedFrogId();
    var hud = wireHud();
    var opts = {
      frogId: frogId,
      onHud: hud.onHud,
      onToast: hud.onToast,
      onAbilityFire: hud.onAbilityFire,
      onReady: hud.onReady,
    };

    loading = Promise.resolve()
      .then(function () {
        if (mode === "phaser") return ensurePhaser();
        return ensureThree();
      })
      .then(function () {
        stopAltEngines();
        // stopAlt cleared host — re-show
        var host = $("engine-host");
        if (host) host.hidden = false;
        var view = $("view");
        if (view) view.style.display = "none";

        if (mode === "phaser") {
          global.FroggiesPhaser.boot(opts);
        } else {
          global.FroggiesThree.boot(opts);
        }
        engineRunning = true;
        currentEngine = mode;
        bindAltControls(mode);
      })
      .catch(function (err) {
        console.error(err);
        var tipEl = $("hub-tip");
        if (tipEl) tipEl.textContent = String(err.message || err);
        stopAltEngines();
        document.body.classList.add("in-title");
        document.body.classList.remove("in-hub");
        var overlay = $("overlay");
        if (overlay) overlay.hidden = false;
      })
      .finally(function () {
        loading = null;
      });
    return loading;
  }

  var controlsBound = false;
  function bindAltControls(mode) {
    if (controlsBound) return;
    controlsBound = true;

    function api() {
      if (currentEngine === "phaser" && global.FroggiesPhaser && global.FroggiesPhaser.isActive()) {
        return global.FroggiesPhaser;
      }
      if (currentEngine === "three" && global.FroggiesThree && global.FroggiesThree.isActive()) {
        return global.FroggiesThree;
      }
      return null;
    }

    function setAxis() {
      var a = api();
      if (!a) return;
      var x = 0;
      var y = 0;
      if (keys.left) x -= 1;
      if (keys.right) x += 1;
      if (keys.up) y -= 1;
      if (keys.down) y += 1;
      a.setSteer(x, y);
    }

    var keys = { left: false, right: false, up: false, down: false };

    function hold(btn, key, val) {
      if (!btn) return;
      var on = function (e) {
        e.preventDefault();
        if (!engineRunning) return;
        keys[key] = val;
        setAxis();
      };
      var off = function (e) {
        e.preventDefault();
        keys[key] = false;
        setAxis();
      };
      btn.addEventListener("pointerdown", function (e) { on(e); try { btn.setPointerCapture(e.pointerId); } catch (err) {} });
      btn.addEventListener("pointerup", off);
      btn.addEventListener("pointercancel", off);
      btn.addEventListener("pointerleave", off);
    }

    hold($("btn-left"), "left", true);
    hold($("btn-right"), "right", true);
    hold($("btn-up"), "up", true);
    hold($("btn-down"), "down", true);

    // Overlay key tracking for alt engines (canvas main.js also listens — both OK)
    window.addEventListener("keydown", function (e) {
      if (!engineRunning) return;
      var a = api();
      if (!a) return;
      var k = e.key.toLowerCase();
      if (k === "arrowleft" || k === "a") { keys.left = true; setAxis(); e.preventDefault(); }
      if (k === "arrowright" || k === "d") { keys.right = true; setAxis(); e.preventDefault(); }
      if (k === "arrowup" || k === "w") { keys.up = true; setAxis(); e.preventDefault(); }
      if (k === "arrowdown" || k === "s") { keys.down = true; setAxis(); e.preventDefault(); }
      if (k === "e" || k === "f" || k === " ") {
        a.pulseInteract();
        e.preventDefault();
      }
      if (k === "q" || k === "shift") {
        a.pulseAbility();
        e.preventDefault();
      }
    });
    window.addEventListener("keyup", function (e) {
      if (!engineRunning) return;
      var k = e.key.toLowerCase();
      if (k === "arrowleft" || k === "a") keys.left = false;
      if (k === "arrowright" || k === "d") keys.right = false;
      if (k === "arrowup" || k === "w") keys.up = false;
      if (k === "arrowdown" || k === "s") keys.down = false;
      setAxis();
    });

    var btnInteract = $("btn-interact");
    var btnAbility = $("btn-ability");
    if (btnInteract) {
      btnInteract.addEventListener("click", function () {
        if (!engineRunning) return;
        var a = api();
        if (a) a.pulseInteract();
      });
    }
    if (btnAbility) {
      btnAbility.addEventListener("click", function () {
        if (!engineRunning) return;
        var a = api();
        if (a) a.pulseAbility();
      });
    }

    var btnEscAlt = $("btn-escape");
    if (btnEscAlt) {
      btnEscAlt.addEventListener("click", function () {
        if (!engineRunning) return;
        if (global.FroggiesEngines && global.FroggiesEngines.leaveOrbit) {
          global.FroggiesEngines.leaveOrbit();
        }
      });
    }

    var btnMenu = $("btn-menu");
    if (btnMenu) {
      btnMenu.addEventListener("click", function () {
        if (!engineRunning) return;
        // Return to lobby from alt engine
        stopAltEngines();
        document.body.classList.add("in-title");
        document.body.classList.remove("in-hub");
        document.body.classList.remove("in-space");
        var overlay = $("overlay");
        if (overlay) overlay.hidden = false;
        var frogPick = $("frog-pick");
        if (frogPick) frogPick.hidden = false;
        var inviteCta = $("invite-cta");
        if (inviteCta) inviteCta.hidden = false;
        var partyBar = $("party-bar");
        if (partyBar) partyBar.hidden = false;
        paintPicker();
      }, true);
    }
  }

  /** Called by main.js before Canvas startHub — return true if handled. */
  function tryStart() {
    var mode = C ? C.getEngine() : "canvas";
    currentEngine = mode;
    if (mode === "canvas") {
      stopAltEngines();
      return false;
    }
    startAlt(mode);
    return true;
  }

  function initPicker() {
    if (!C) return;
    paintPicker();
    var root = $("engine-pick");
    if (!root) return;
    root.addEventListener("click", function (e) {
      var btn = e.target.closest("[data-engine]");
      if (!btn) return;
      var id = btn.getAttribute("data-engine");
      C.setEngine(id);
      paintPicker();
    });

    // Capture-phase GO: Phaser/Three never enter Canvas main.js startHub
    // (Canvas expansion executor owns main.js / world.js — keep this file thin.)
    var btnStart = $("btn-start");
    if (btnStart) {
      btnStart.addEventListener(
        "click",
        function (e) {
          var mode = C.getEngine();
          if (mode === "canvas") return; // let main.js handle PeerJS + hub
          e.preventDefault();
          e.stopImmediatePropagation();
          if (engineRunning) return;
          startAlt(mode);
        },
        true
      );
    }

    // Space/Enter on title while Phaser/Three selected
    window.addEventListener(
      "keydown",
      function (e) {
        if (engineRunning) {
          // block main.js title-start re-entry
          if (e.key === " " || e.key === "Enter") {
            var overlay = $("overlay");
            if (overlay && !overlay.hidden) return;
            // playing alt — leave ability/interact to bindAltControls
          }
          return;
        }
        var mode = C.getEngine();
        if (mode === "canvas") return;
        if (e.key !== " " && e.key !== "Enter") return;
        var overlay = $("overlay");
        if (!overlay || overlay.hidden) return;
        if (document.body.classList.contains("in-hub")) return;
        e.preventDefault();
        e.stopImmediatePropagation();
        startAlt(mode);
      },
      true
    );
  }

  global.FroggiesEngines = {
    tryStart: tryStart,
    stopAltEngines: stopAltEngines,
    leaveOrbit: function () {
      if (currentEngine === "phaser" && global.FroggiesPhaser && global.FroggiesPhaser.leaveOrbit) {
        return !!global.FroggiesPhaser.leaveOrbit();
      }
      if (currentEngine === "three" && global.FroggiesThree && global.FroggiesThree.leaveOrbit) {
        return !!global.FroggiesThree.leaveOrbit();
      }
      return false;
    },
    paintPicker: paintPicker,
    getMode: function () { return C ? C.getEngine() : "canvas"; },
    isAltRunning: function () { return engineRunning; },
  };

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initPicker);
  } else {
    initPicker();
  }
})(typeof window !== "undefined" ? window : globalThis);
