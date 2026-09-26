/* Four Froggies — thin lobby engine switcher + CDN boot (phaser-hub / three-hub).
   Canvas path stays default (main.js + PeerJS + world.js). Do not touch world.js scale.
   Phaser/Three = solo-first physics/look compare. */
(function (global) {
  "use strict";

  var C = global.FroggiesCanon;
  var CACHE = "20260926-boardall1";
  var CDN = {
    phaser: "https://cdn.jsdelivr.net/npm/phaser@3.87.0/dist/phaser.min.js",
    three: "https://cdnjs.cloudflare.com/ajax/libs/three.js/r134/three.min.js",
  };
  var loading = null;
  var currentEngine = "canvas";
  var engineRunning = false;
  /** Pads claimed by couch locals — never feed these into shared primary setSteer. */
  var claimedPadSet = {};
  var primaryPadIndex = null;
  var lastSeatMap = null;

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
      if (!global.Phaser) throw new Error("Phaser CDN failed to load");
      if (global.FroggiesPhaser) return;
      return loadScript("phaser-hub.js?v=" + CACHE);
    }).then(function () {
      if (!global.FroggiesPhaser || typeof global.FroggiesPhaser.boot !== "function") {
        throw new Error("phaser-hub failed to boot (FroggiesPhaser missing)");
      }
    });
  }

  function ensureThree() {
    var chain = Promise.resolve();
    if (!global.THREE) {
      chain = chain.then(function () { return loadScript(CDN.three); });
    }
    return chain.then(function () {
      if (!global.THREE) throw new Error("three.js CDN failed to load");
      if (global.FroggiesThree) return;
      return loadScript("three-hub.js?v=" + CACHE);
    }).then(function () {
      if (!global.FroggiesThree || typeof global.FroggiesThree.boot !== "function") {
        throw new Error("three-hub failed to boot (FroggiesThree missing)");
      }
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
    claimedPadSet = {};
    primaryPadIndex = null;
    lastSeatMap = null;
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
      var kind = String(abilityName || "HOP").toLowerCase();
      btnAbility.classList.remove("fire-dash", "fire-shield", "fire-zap", "fire-bot", "fire-zoom", "fire-hop", "ability-fired");
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
          /* truck2: EXIT anytime while driving — don't require near parked pad */
          var canAct = !!(h.near) || !!(h.inTruck) || !!(h.inMech);
          btnInteract.classList.toggle("ready", canAct);
          btnInteract.disabled = !canAct;
          if (h.inTruck || h.inMech) btnInteract.textContent = "EXIT";
          else if (h.near && C && ((C.isTruckHotspot && C.isTruckHotspot(h.near)) || (C.isMechHotspot && C.isMechHotspot(h.near))))
            btnInteract.textContent = "BOARD";
          else btnInteract.textContent = "INTERACT";
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

  function ingestSeatMapPads(seatMap, primaryFrogId) {
    claimedPadSet = {};
    primaryPadIndex = null;
    lastSeatMap = seatMap || null;
    if (!seatMap) return;
    var order = (C && C.FROG_ORDER) || ["james", "jimmy", "bubbles", "rexy"];
    for (var i = 0; i < order.length; i++) {
      var id = order[i];
      var s = seatMap[id];
      if (!s || !s.local || s.padIndex == null || s.padIndex === undefined) continue;
      var pi = s.padIndex | 0;
      claimedPadSet[pi] = id;
      if (primaryPadIndex == null && (id === primaryFrogId || (s.human && s.local))) {
        if (id === primaryFrogId) primaryPadIndex = pi;
      }
    }
    if (primaryPadIndex == null && primaryFrogId && seatMap[primaryFrogId] &&
        seatMap[primaryFrogId].padIndex != null) {
      primaryPadIndex = seatMap[primaryFrogId].padIndex | 0;
    }
  }

  function resolveSeatMap(preferredFrogId) {
    var map = null;
    try {
      var P = global.FroggiesParty;
      /* Prefer startParty so couch pads + default seat are finalized (no peer start for three/phaser guests) */
      if (P && P.active && typeof P.active.startParty === "function" && P.active.canStart && P.active.canStart()) {
        map = P.active.startParty();
      } else if (P && P.active && typeof P.active.buildSeatMap === "function") {
        map = P.active.buildSeatMap();
      }
    } catch (e) { /* ignore */ }
    if (!map) {
      var fid = preferredFrogId || selectedFrogId() || "james";
      map = {};
      var order = (C && C.FROG_ORDER) || ["james", "jimmy", "bubbles", "rexy"];
      for (var i = 0; i < order.length; i++) {
        var id = order[i];
        map[id] = { human: id === fid, local: id === fid, peerId: null, padIndex: id === fid ? null : undefined };
      }
    }
    return map;
  }

  function hardFailAlt(mode, err) {
    console.error(err);
    /* Never silently fall back to Canvas — keep selected engine + toast hard fail */
    if (C && C.setEngine) C.setEngine(mode === "phaser" ? "phaser" : "three");
    currentEngine = mode;
    paintPicker();
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
    var msg = (mode === "three" ? "Three.js failed" : "Phaser failed") + ": " + (err && err.message ? err.message : String(err || "boot error"));
    var tipEl = $("hub-tip");
    if (tipEl) tipEl.textContent = msg;
    var partyStatus = $("party-status");
    if (partyStatus) {
      partyStatus.classList.add("is-error");
      partyStatus.textContent = msg + " · Engine stays " + mode + " (not Canvas). Retry GO or pick Canvas.";
    }
    var overlayGo = $("overlay-go");
    if (overlayGo) overlayGo.textContent = msg;
  }

  function startAlt(mode) {
    if (loading) return loading;
    if (mode !== "phaser" && mode !== "three") {
      hardFailAlt("three", new Error("Invalid alt engine: " + mode));
      return Promise.reject(new Error("Invalid alt engine"));
    }
    /* Keep localStorage on the chosen alt engine for the whole attempt */
    if (C && C.setEngine) C.setEngine(mode);
    currentEngine = mode;
    paintPicker();
    showPlayingChrome();
    var frogId = selectedFrogId();
    var seatMap = resolveSeatMap(frogId);
    var primary = frogId;
    var order = (C && C.FROG_ORDER) || ["james", "jimmy", "bubbles", "rexy"];
    for (var si = 0; si < order.length; si++) {
      var s = seatMap[order[si]];
      if (s && s.human && s.local) { primary = order[si]; break; }
    }
    ingestSeatMapPads(seatMap, primary);
    var hud = wireHud();
    var opts = {
      frogId: primary,
      seatMap: seatMap,
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
        var host = $("engine-host");
        if (host) host.hidden = false;
        var view = $("view");
        if (view) view.style.display = "none";

        if (mode === "phaser") {
          global.FroggiesPhaser.boot(opts);
        } else {
          global.FroggiesThree.boot(opts);
        }
        if (mode === "three" && (!global.FroggiesThree || !global.FroggiesThree.isActive || !global.FroggiesThree.isActive())) {
          throw new Error("Three boot did not become active");
        }
        if (mode === "phaser" && (!global.FroggiesPhaser || !global.FroggiesPhaser.isActive || !global.FroggiesPhaser.isActive())) {
          throw new Error("Phaser boot did not become active");
        }
        engineRunning = true;
        currentEngine = mode;
        if (C && C.setEngine) C.setEngine(mode);
        bindAltControls(mode);
        ensurePadLoop();
      })
      .catch(function (err) {
        hardFailAlt(mode, err);
      })
      .finally(function () {
        loading = null;
      });
    return loading;
  }

  /* joy2: shared steer — virtual joystick + WASD/D-pad fan-out to Canvas / Phaser / Three */
  var controlsBound = false;
  var joyBound = false;
  var keys = { left: false, right: false, up: false, down: false };
  var joyActive = false;
  var joyX = 0;
  var joyY = 0;
  var canvasJoyListeners = [];

  function altApi() {
    if (currentEngine === "phaser" && global.FroggiesPhaser && global.FroggiesPhaser.isActive()) {
      return global.FroggiesPhaser;
    }
    if (currentEngine === "three" && global.FroggiesThree && global.FroggiesThree.isActive()) {
      return global.FroggiesThree;
    }
    return null;
  }

  function notifyCanvasJoy(x, y) {
    for (var i = 0; i < canvasJoyListeners.length; i++) {
      try { canvasJoyListeners[i](x, y); } catch (err) { /* ignore */ }
    }
  }

  /** Push combined steer: joy wins while held; else WASD/D-pad/gamepad. Canvas gets joy channel. */
  var _padRaf = 0;
  function applySharedSteer() {
    var x = 0;
    var y = 0;
    /* Gamepad poll only while alt engines run — canvas main.js owns the pad otherwise */
    /* interact2: each pad A/B tagged with padIndex — never anonymous shared pulse.
       Critical: a pad claimed by a non-primary frog must NOT drive shared setSteer
       (that was one-pad-controls-two-frogs). */
    var gPad = null;
    if (engineRunning && global.SimilarizeGamepad) {
      var apiBtn = altApi();
      var snaps = typeof global.SimilarizeGamepad.pollAll === "function"
        ? global.SimilarizeGamepad.pollAll(4)
        : null;
      for (var pi = 0; pi < 4; pi++) {
        var gpN = snaps ? snaps[pi] : global.SimilarizeGamepad.pollPad(pi);
        if (!gpN || !gpN.connected) continue;
        var claimedBy = claimedPadSet[pi];
        var isPrimaryPad = primaryPadIndex != null && (primaryPadIndex | 0) === (pi | 0);
        var padOwnsOtherFrog = !!claimedBy && !isPrimaryPad;
        /* Shared primary steer: only primary's pad, or an unclaimed pad when primary is padless */
        if (!gPad && !padOwnsOtherFrog) {
          if (isPrimaryPad || primaryPadIndex == null) gPad = gpN;
        }
        if (apiBtn) {
          var aEdge = gpN.buttonsPressed || {};
          if (aEdge.a && apiBtn.pulseInteract) apiBtn.pulseInteract(pi);
          if ((aEdge.b || aEdge.x) && apiBtn.pulseAbility) apiBtn.pulseAbility(pi);
        }
      }
    }
    if (joyActive) {
      x = joyX;
      y = joyY;
    } else {
      if (keys.left) x -= 1;
      if (keys.right) x += 1;
      if (keys.up) y -= 1;
      if (keys.down) y += 1;
      /* When primary has its own pad, three/phaser mergedSteer polls that pad —
         avoid also stuffing the same stick into keySteer (double accel). Keyboard still OK. */
      if (gPad && gPad.connected && !x && !y && primaryPadIndex == null) {
        x = gPad.lx || 0;
        y = gPad.ly || 0;
        if (gPad.dpad.l) x = -1;
        if (gPad.dpad.r) x = 1;
        if (gPad.dpad.u) y = -1;
        if (gPad.dpad.d) y = 1;
        var mag = Math.hypot(x, y);
        if (mag > 1) { x /= mag; y /= mag; }
      }
    }
    var a = engineRunning ? altApi() : null;
    if (a && typeof a.setSteer === "function") a.setSteer(x, y);
    notifyCanvasJoy(joyActive ? joyX : 0, joyActive ? joyY : 0);
  }
  function ensurePadLoop() {
    if (_padRaf) return;
    function loop() {
      _padRaf = 0;
      if (!engineRunning) return;
      applySharedSteer();
      _padRaf = requestAnimationFrame(loop);
    }
    _padRaf = requestAnimationFrame(loop);
  }

  function setJoySteer(nx, ny, active) {
    joyActive = !!active;
    if (joyActive) {
      joyX = nx;
      joyY = ny;
    } else {
      joyX = 0;
      joyY = 0;
    }
    applySharedSteer();
  }

  function ensureVjoyBodyMount(root) {
    /* joy2: keep stick a body sibling — never trapped under #controls pointer-events:none */
    if (!root || !document.body) return root;
    if (root.parentElement === document.body) return root;
    try {
      document.body.appendChild(root);
    } catch (err) { /* ignore */ }
    return root;
  }

  var joyHintShown = false;
  function pulseVjoyHint() {
    var root = $("vjoy");
    if (!root || joyHintShown) return;
    joyHintShown = true;
    root.classList.add("vjoy-hint");
    setTimeout(function () {
      root.classList.remove("vjoy-hint");
    }, 1700);
  }

  function bindVirtualJoystick() {
    if (joyBound) return;
    var root = ensureVjoyBodyMount($("vjoy"));
    var knob = $("vjoy-knob");
    if (!root || !knob) return;
    joyBound = true;
    var ptrId = null;
    var maxR = 36;
    var dead = 0.14;

    function playing() {
      return document.body.classList.contains("in-hub") || document.body.classList.contains("in-space");
    }

    function setKnob(dx, dy) {
      knob.style.transform = "translate(" + dx + "px," + dy + "px)";
    }

    function fromEvent(e) {
      var rect = root.getBoundingClientRect();
      maxR = Math.max(28, Math.min(rect.width, rect.height) * 0.38);
      var cx = rect.left + rect.width * 0.5;
      var cy = rect.top + rect.height * 0.5;
      var dx = e.clientX - cx;
      var dy = e.clientY - cy;
      var len = Math.hypot(dx, dy);
      if (len > maxR && len > 0) {
        dx = (dx / len) * maxR;
        dy = (dy / len) * maxR;
        len = maxR;
      }
      setKnob(dx, dy);
      var nx = maxR > 0 ? dx / maxR : 0;
      var ny = maxR > 0 ? dy / maxR : 0;
      var mag = Math.hypot(nx, ny);
      if (mag < dead) {
        setJoySteer(0, 0, true);
        return;
      }
      /* normalize past deadzone so full throw still reaches 1 */
      var scale = (mag - dead) / (1 - dead);
      if (scale > 1) scale = 1;
      setJoySteer((nx / mag) * scale, (ny / mag) * scale, true);
    }

    function endJoy() {
      ptrId = null;
      root.classList.remove("is-active");
      setKnob(0, 0);
      setJoySteer(0, 0, false);
    }

    root.addEventListener("pointerdown", function (e) {
      if (!playing()) return;
      if (e.button != null && e.button !== 0) return;
      e.preventDefault();
      e.stopPropagation();
      ptrId = e.pointerId;
      root.classList.add("is-active");
      try { root.setPointerCapture(e.pointerId); } catch (err) { /* ignore */ }
      fromEvent(e);
    });
    root.addEventListener("pointermove", function (e) {
      if (ptrId == null || e.pointerId !== ptrId) return;
      e.preventDefault();
      fromEvent(e);
    });
    function up(e) {
      if (ptrId == null || (e && e.pointerId != null && e.pointerId !== ptrId)) return;
      e && e.preventDefault && e.preventDefault();
      endJoy();
    }
    root.addEventListener("pointerup", up);
    root.addEventListener("pointercancel", up);
    root.addEventListener("lostpointercapture", function () {
      if (ptrId != null) endJoy();
    });
  }

  function bindAltControls(mode) {
    if (controlsBound) return;
    controlsBound = true;
    bindVirtualJoystick();

    function hold(btn, key, val) {
      if (!btn) return;
      var on = function (e) {
        e.preventDefault();
        if (!engineRunning) return;
        keys[key] = val;
        applySharedSteer();
      };
      var off = function (e) {
        e.preventDefault();
        keys[key] = false;
        applySharedSteer();
      };
      btn.addEventListener("pointerdown", function (e) { on(e); try { btn.setPointerCapture(e.pointerId); } catch (err) {} });
      btn.addEventListener("pointerup", off);
      btn.addEventListener("pointercancel", off);
      btn.addEventListener("pointerleave", off);
    }

    ensurePadLoop();
    hold($("btn-left"), "left", true);
    hold($("btn-right"), "right", true);
    hold($("btn-up"), "up", true);
    hold($("btn-down"), "down", true);

    // Overlay key tracking for alt engines (canvas main.js also listens — both OK)
    window.addEventListener("keydown", function (e) {
      if (!engineRunning) return;
      var a = altApi();
      if (!a) return;
      var k = e.key.toLowerCase();
      if (k === "arrowleft" || k === "a") { keys.left = true; applySharedSteer(); e.preventDefault(); }
      if (k === "arrowright" || k === "d") { keys.right = true; applySharedSteer(); e.preventDefault(); }
      if (k === "arrowup" || k === "w") { keys.up = true; applySharedSteer(); e.preventDefault(); }
      if (k === "arrowdown" || k === "s") { keys.down = true; applySharedSteer(); e.preventDefault(); }
      if (k === "e" || k === "f") {
        a.pulseInteract(); /* keyboard/HUD → primary only */
        e.preventDefault();
      }
      if (k === " " || k === "q" || k === "shift") {
        a.pulseAbility(); /* keyboard HOP → primary only */
        e.preventDefault();
      }
      /* track3: wheel size while driving — [ ] / - = */
      if (k === "[" || k === "-" || k === "]") {
        var C = global.FroggiesCanon;
        if (C && C.adjustWheelScale) {
          var dir = (k === "]") ? 1 : -1;
          var ws = C.adjustWheelScale(dir * ((C.WHEEL_SCALE && C.WHEEL_SCALE.step) || 0.08));
          syncWheelHud(ws);
          e.preventDefault();
        }
      }
      if (k === "=" || k === "+") {
        var C2 = global.FroggiesCanon;
        if (C2 && C2.adjustWheelScale) {
          var ws2 = C2.adjustWheelScale((C2.WHEEL_SCALE && C2.WHEEL_SCALE.step) || 0.08);
          syncWheelHud(ws2);
          e.preventDefault();
        }
      }
    });
    window.addEventListener("keyup", function (e) {
      if (!engineRunning) return;
      var k = e.key.toLowerCase();
      if (k === "arrowleft" || k === "a") keys.left = false;
      if (k === "arrowright" || k === "d") keys.right = false;
      if (k === "arrowup" || k === "w") keys.up = false;
      if (k === "arrowdown" || k === "s") keys.down = false;
      applySharedSteer();
    });

    var btnInteract = $("btn-interact");
    var btnAbility = $("btn-ability");
    if (btnInteract) {
      btnInteract.addEventListener("click", function () {
        if (!engineRunning) return;
        var a = altApi();
        if (a) a.pulseInteract();
      });
    }
    if (btnAbility) {
      btnAbility.addEventListener("click", function () {
        if (!engineRunning) return;
        var a = altApi();
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
        setJoySteer(0, 0, false);
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

  function syncWheelHud(ws) {
    var panel = $("wheel-size");
    var slider = $("wheel-slider");
    var val = $("wheel-size-val");
    if (ws == null && global.FroggiesCanon && global.FroggiesCanon.getWheelScale) {
      ws = global.FroggiesCanon.getWheelScale();
    }
    if (slider && ws != null && document.activeElement !== slider) slider.value = String(Math.round(ws * 100));
    if (val && ws != null) val.textContent = Number(ws).toFixed(1) + "×";
    if (panel) {
      /* Show when alt engine reports in-truck via toast/hooks — toggled by hubs too */
    }
  }

  function bindSharedWheelUi() {
    if (bindSharedWheelUi._done) return;
    bindSharedWheelUi._done = true;
    var down = $("btn-wheel-down");
    var up = $("btn-wheel-up");
    var slider = $("wheel-slider");
    function nudge(dir) {
      var C = global.FroggiesCanon;
      if (!C || !C.adjustWheelScale) return;
      var ws = C.adjustWheelScale(dir * ((C.WHEEL_SCALE && C.WHEEL_SCALE.step) || 0.08));
      syncWheelHud(ws);
    }
    if (down) down.addEventListener("click", function () { nudge(-1); });
    if (up) up.addEventListener("click", function () { nudge(1); });
    if (slider) {
      slider.addEventListener("input", function () {
        var C = global.FroggiesCanon;
        if (!C || !C.setWheelScale) return;
        syncWheelHud(C.setWheelScale(Number(slider.value) / 100));
      });
    }
  }

  global.FroggiesEngines = {
    tryStart: tryStart,
    startAlt: startAlt,
    isAltEngine: function (mode) {
      var m = mode || (C ? C.getEngine() : "canvas");
      return m === "phaser" || m === "three";
    },
    syncWheelHud: syncWheelHud,
    setWheelPanelVisible: function (on) {
      var panel = $("wheel-size");
      if (panel) panel.hidden = !on;
      if (on) syncWheelHud();
    },
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
    /** Canvas (and others) register for joy2 stick values — same HUD path for all engines. */
    onJoySteer: function (fn) {
      if (typeof fn === "function") canvasJoyListeners.push(fn);
    },
    setJoySteer: setJoySteer,
    applySharedSteer: applySharedSteer,
  };

  function applyDirectPlay() {
    var q;
    try { q = new URLSearchParams(location.search); } catch (err) { return; }
    var eng = q.get("engine");
    if ((eng === "three" || eng === "phaser" || eng === "canvas") && C && C.setEngine) {
      C.setEngine(eng);
      paintPicker();
    }
    if (q.get("go") === "1" && C && C.getEngine && C.getEngine() !== "canvas") {
      setTimeout(function () { startAlt(C.getEngine()); }, 80);
    }
  }

  function bootControls() {
    initPicker();
    applyDirectPlay();
    bindVirtualJoystick();
    bindSharedWheelUi();
    /* joy2: notice pulse once when player first enters hub/space */
    try {
      var mo = new MutationObserver(function () {
        if (document.body.classList.contains("in-hub") || document.body.classList.contains("in-space")) {
          pulseVjoyHint();
        }
      });
      mo.observe(document.body, { attributes: true, attributeFilter: ["class"] });
    } catch (err) { /* ignore */ }
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", bootControls);
  } else {
    bootControls();
  }
})(typeof window !== "undefined" ? window : globalThis);
