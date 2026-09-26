/* Four Froggies — Phaser 3 hub (CDN). Canvas-parity ranch + thin space stub.
   Solo-first. Big map · compound · squiggle track · pond whales · 4 trucks+shared ·
   on-water/under tint · Starship → orbit Escape/hard thruster.
   polish4: compound presence + track hills + inviting hotspots + drive bob/spray.
   polish5: ambient pollen/fireflies; pond ripples; track race dust; garage door open-near;
   shared ALL ABOARD; land shake; hotspot sparkle; orbit pull rings + Escape banner.
   polish6: depth shadows + parallax-lite hills + Mars invader silhouette tease + mech wow tip.
   polish7: zone signs + mini-map lite + companion idle bounce / follow lag.
   polish8: truck silhouette + house porch + whale breach + destination beacon (cheap Canvas ports).
   polish9: color nameplates; aboard icons; track start/finish gate; Optimus visual punch lite.
   polish10: quieter UI; exit truck anytime; friction/cam tighten; particle caps; dusk sky shift.
   tapsteer1: faster walk/drive; hold-to-aim tap/click steer + marker.
   eyes1: rotate frog sprite toward walk dir; idle keeps last; AI companions too. */
(function (global) {
  "use strict";
  var C = global.FroggiesCanon;
  var game = null, active = false, hooks = {};
  var keySteer = { x: 0, y: 0 }, tapSteer = { x: 0, y: 0 }, tapHeld = false;
  var tapMarker = null;
  var wantInteract = false, wantAbility = false;

  function mergedSteer() {
    if (keySteer.x || keySteer.y) return keySteer;
    return tapSteer;
  }
  function ensureTapMarkerEl() {
    var el = document.getElementById("tap-steer-marker");
    if (el) return el;
    el = document.createElement("div");
    el.id = "tap-steer-marker";
    el.setAttribute("aria-hidden", "true");
    el.innerHTML = '<span class="tap-ring"></span><span class="tap-arrow"></span>';
    document.body.appendChild(el);
    return el;
  }
  function showTapMarker(cx, cy, ang) {
    var el = ensureTapMarkerEl();
    el.classList.add("is-on");
    el.style.left = cx + "px";
    el.style.top = cy + "px";
    el.style.setProperty("--tap-ang", ang + "rad");
    el.style.opacity = "1";
    tapMarker = { life: 0.55, ang: ang };
  }
  function clearTapAim() {
    tapHeld = false; tapSteer.x = 0; tapSteer.y = 0;
    if (tapMarker) tapMarker.life = Math.min(tapMarker.life || 0.55, 0.28);
  }
  function tickTapMarker(dt) {
    var el = document.getElementById("tap-steer-marker");
    if (!tapMarker) { if (el) el.classList.remove("is-on"); return; }
    tapMarker.life -= dt;
    if (tapMarker.life <= 0) { tapMarker = null; if (el) el.classList.remove("is-on"); return; }
    if (el) { el.classList.add("is-on"); el.style.opacity = String(Math.min(1, tapMarker.life * 2.2)); }
  }

  function destroy() {
    active = false; keySteer.x = keySteer.y = 0; tapSteer.x = tapSteer.y = 0; tapHeld = false; tapMarker = null;
    wantInteract = wantAbility = false;
    var mel = document.getElementById("tap-steer-marker");
    if (mel) mel.classList.remove("is-on");
    if (game) { try { game.destroy(true); } catch (e) {} game = null; }
    var host = document.getElementById("engine-host");
    if (host) host.innerHTML = "";
  }
  function setSteer(x, y) { keySteer.x = x; keySteer.y = y; }
  function pulseInteract() { wantInteract = true; }
  function pulseAbility() { wantAbility = true; }
  function isActive() { return active; }
  function hx(hex) { return Phaser.Display.Color.HexStringToColor(hex).color; }
  function polyLine(g, pts, color, width, alpha, close) {
    if (!pts || pts.length < 2) return;
    g.lineStyle(width || 8, color, alpha == null ? 1 : alpha);
    g.beginPath(); g.moveTo(pts[0][0], pts[0][1]);
    for (var i = 1; i < pts.length; i++) g.lineTo(pts[i][0], pts[i][1]);
    if (close) g.closePath(); g.strokePath();
  }

  function boot(opts) {
    destroy(); hooks = opts || {};
    var host = document.getElementById("engine-host");
    if (!host) { console.error("engine-host missing"); return; }
    if (typeof Phaser === "undefined") {
      console.error("Phaser CDN not loaded");
      if (hooks.onToast) hooks.onToast("Phaser failed to load");
      return;
    }
    host.hidden = false; host.innerHTML = "";
    var view = document.getElementById("view");
    if (view) view.style.display = "none";
    var frogId = (opts && opts.frogId) || "james";
    var def = C.FROG_DEFS[frogId] || C.FROG_DEFS.james;
    var spawn = (C.COMPOUND && C.COMPOUND.spawn) || { x: 280, y: 1750 };

    var RanchScene = new Phaser.Class({
      Extends: Phaser.Scene,
      initialize: function () { Phaser.Scene.call(this, { key: "ranch" }); },
      create: function () {
        this.cameras.main.setBackgroundColor("#1a3a1a");
        this.physics.world.setBounds(0, 0, C.MAP_W, C.MAP_H);
        var g = this.add.graphics();
        g.fillStyle(0x2d5a27, 1); g.fillRect(0, 0, C.MAP_W, C.MAP_H);
        g.lineStyle(1, 0x3f6b38, 0.28);
        for (var tx = 0; tx < C.MAP_W; tx += 80) g.lineBetween(tx, 0, tx, C.MAP_H);
        for (var ty = 0; ty < C.MAP_H; ty += 80) g.lineBetween(0, ty, C.MAP_W, ty);
        for (var i = 0; i < C.AREAS.length; i++) {
          var a = C.AREAS[i];
          this.add.rectangle(a.x + a.w / 2, a.y + a.h / 2, a.w, a.h, hx(a.color), 0.88)
            .setStrokeStyle(4, 0xffffff, 0.4);
          this.add.text(a.x + 16, a.y + 12, a.name, {
            fontFamily: "Segoe UI, system-ui, sans-serif", fontSize: "18px", fontStyle: "bold",
            color: "#fff", stroke: "#000", strokeThickness: 4,
          });
        }
        this.drawCompound(); this.drawTrack(); this.drawPondLife();
        this.drawStarshipApproach(); this.drawTrucks();
        this.hotGfx = [];
        for (var h = 0; h < C.HOTSPOTS.length; h++) {
          var hs = C.HOTSPOTS[h];
          if (C.isTruckHotspot && C.isTruckHotspot(hs)) continue;
          var ring = this.add.circle(hs.x, hs.y, hs.r + 6, 0xfbbf24, 0.16).setStrokeStyle(3, 0xfbbf24, 0.85);
          var sub = hs.id === "phone" ? "→ Purple Bear" : hs.id === "sps" ? "→ Optimus · Jimmy" : hs.label;
          this.add.ellipse(hs.x, hs.y + 6, hs.r * 1.4, hs.r * 0.45, 0xfbbf24, 0.18);
          if (hs.id === "phone") {
            this.add.rectangle(hs.x, hs.y - 8, 22, 32, 0x7c3aed, 1).setStrokeStyle(2, 0xfbbf24, 1);
          } else if (hs.id === "sps") {
            this.add.ellipse(hs.x, hs.y - 10, 28, 14, 0x0284c7, 0.35).setStrokeStyle(2, 0x38bdf8, 1);
            this.add.circle(hs.x, hs.y - 10, 5, 0xfbbf24, 1);
          }
          this.add.text(hs.x, hs.y - hs.r - 18, hs.label, {
            fontFamily: "Segoe UI, system-ui, sans-serif", fontSize: "13px", fontStyle: "bold",
            color: "#fef3c7", stroke: "#000", strokeThickness: 3,
          }).setOrigin(0.5, 1);
          this.add.text(hs.x, hs.y - hs.r - 4, sub, {
            fontFamily: "Segoe UI, system-ui, sans-serif", fontSize: "10px", fontStyle: "bold",
            color: "#c4b5fd", stroke: "#000", strokeThickness: 2,
          }).setOrigin(0.5, 1);
          this.hotGfx.push({ data: hs, ring: ring });
        }
        var ss = C.STARSHIP || { x: 360, y: 320 };
        this.add.ellipse(ss.x, ss.y + 8, 150, 56, 0x38bdf8, 0.28).setStrokeStyle(3, 0xfbbf24, 0.9);
        this.add.ellipse(ss.x, ss.y + 8, 90, 34, 0x7dd3fc, 0.2).setStrokeStyle(2, 0xe0f2fe, 0.8);
        this.add.triangle(ss.x, ss.y - 26, 0, -22, -16, 22, 16, 22, 0xe2e8f0, 1);
        this.add.rectangle(ss.x, ss.y + 6, 20, 22, 0x64748b, 1);
        this.add.circle(ss.x + 26, ss.y - 20, 10, 0xf97316, 1);
        this.add.text(ss.x, ss.y + 44, "★ STARSHIP · SPACE", {
          fontFamily: "Segoe UI, system-ui, sans-serif", fontSize: "13px", fontStyle: "bold",
          color: "#fef3c7", stroke: "#000", strokeThickness: 4,
        }).setOrigin(0.5);
        this.add.text(ss.x + 26, ss.y - 34, "Spotty", {
          fontFamily: "Segoe UI, system-ui, sans-serif", fontSize: "11px", color: "#fdba74",
          stroke: "#000", strokeThickness: 3,
        }).setOrigin(0.5);
        this.ensureFrogTexture(frogId, def, 64);
        this.player = this.physics.add.image(spawn.x, spawn.y, "frog_" + frogId);
        this.player.setCollideWorldBounds(true).setDepth(20).setScale(1.15);
        this.player.body.setSize(34, 34);
        this.companions = [];
        for (var ci = 0; ci < C.FROG_ORDER.length; ci++) {
          var cid = C.FROG_ORDER[ci]; if (cid === frogId) continue;
          var cdef = C.FROG_DEFS[cid];
          this.ensureFrogTexture(cid, cdef, 48);
          var companion = this.add.image(spawn.x + 40 + ci * 36, spawn.y + 20 + (ci % 2) * 16, "frog_" + cid);
          companion.setAlpha(0.95).setDepth(19).setScale(1.05);
          companion.tx = companion.x; companion.ty = companion.y; companion.timer = 1 + Math.random() * 2;
          companion.frogId = cid;
          companion.followLag = 0.35 + ci * 0.12;
          companion.idleBounce = Math.random() * 6;
          companion.faceAngle = 0;
          companion.chatT = 0;
          companion.nameTag = this.add.text(companion.x, companion.y - 26, cdef.name, {
            fontFamily: "Segoe UI, system-ui, sans-serif", fontSize: "11px", fontStyle: "bold",
            color: cdef.color || "#fff", stroke: "#000", strokeThickness: 3,
            backgroundColor: "#0f172ae6", padding: { x: 6, y: 2 },
          }).setOrigin(0.5, 1).setDepth(21);
          companion.chatBubble = this.add.text(companion.x, companion.y - 44, "", {
            fontFamily: "Segoe UI, system-ui, sans-serif", fontSize: "10px", fontStyle: "bold",
            color: "#0f172a", backgroundColor: "#f8fafc", padding: { x: 6, y: 3 },
          }).setOrigin(0.5, 1).setDepth(22).setVisible(false);
          this.companions.push(companion);
        }
        this.nameTag = this.add.text(0, 0, def.name, {
          fontFamily: "Segoe UI, system-ui, sans-serif", fontSize: "13px", fontStyle: "bold",
          color: def.color || "#fff", stroke: "#000", strokeThickness: 4,
          backgroundColor: "#0f172ae6", padding: { x: 7, y: 3 },
        }).setOrigin(0.5, 1).setDepth(21);
        /* polish9: shared truck aboard frog icons */
        this.aboardIcons = [];
        for (var ai = 0; ai < C.FROG_ORDER.length; ai++) {
          var aid = C.FROG_ORDER[ai];
          var adef = C.FROG_DEFS[aid];
          var ic = this.add.circle(0, 0, 7, hx(adef.color), 1).setDepth(22).setVisible(false).setStrokeStyle(1.5, 0x0f172a, 1);
          var il = this.add.text(0, 0, adef.name.charAt(0), {
            fontSize: "9px", fontStyle: "bold", color: "#0f172a",
          }).setOrigin(0.5).setDepth(23).setVisible(false);
          this.aboardIcons.push({ id: aid, dot: ic, letter: il });
        }
        this.aboardLabel = this.add.text(0, 0, "Aboard", {
          fontSize: "11px", fontStyle: "bold", color: "#fef3c7", stroke: "#000", strokeThickness: 3,
          backgroundColor: "#0f172acc", padding: { x: 6, y: 2 },
        }).setOrigin(0.5).setDepth(22).setVisible(false);
        this.kitFxT = 0; this.kitFxKind = ""; this.lapSide = 0; this.lapCd = 0; this.lapCount = 0;
        this.truckBody = this.add.rectangle(0, 0, 78, 36, 0x9ca3af, 1).setDepth(18).setVisible(false).setStrokeStyle(3, 0x111827, 1);
        this.truckAccent = this.add.rectangle(0, -2, 50, 14, hx(def.color), 0.85).setDepth(18).setVisible(false);
        this.waterClip = this.add.rectangle(0, 10, 84, 22, 0x0e7490, 0.55).setDepth(19).setVisible(false);
        this.cameras.main.startFollow(this.player, true, 0.28, 0.28); /* polish3/10 less lag fight */

        /* tapsteer1: hold-to-aim on playfield (direction, not go-to) */
        var self = this;
        function aimFromPointer(pointer) {
          if (!active || !self.player) return;
          var cam = self.cameras.main;
          var sx = (self.player.x - cam.worldView.x) * cam.zoom;
          var sy = (self.player.y - cam.worldView.y) * cam.zoom;
          var dx = pointer.x - sx;
          var dy = pointer.y - sy;
          var len = Math.hypot(dx, dy);
          if (len < 14) return;
          tapSteer.x = dx / len;
          tapSteer.y = dy / len;
          tapHeld = true;
          showTapMarker(pointer.event ? pointer.event.clientX : pointer.x, pointer.event ? pointer.event.clientY : pointer.y, Math.atan2(dy, dx));
        }
        this.input.on("pointerdown", function (pointer) {
          aimFromPointer(pointer);
        });
        this.input.on("pointermove", function (pointer) {
          if (!tapHeld || !pointer.isDown) return;
          aimFromPointer(pointer);
        });
        this.input.on("pointerup", function () { clearTapAim(); });

        this.cameras.main.setBounds(0, 0, C.MAP_W, C.MAP_H);
        /* mobile1: phone gets closer zoom so frogs aren't mush; desktop unchanged formula */
        (function () {
          var iw = window.innerWidth || 800;
          var z = iw <= 520
            ? Math.min(0.88, Math.max(0.62, iw / 600))
            : Math.min(0.95, Math.max(0.42, iw / 1400));
          this.cameras.main.setZoom(z);
        }).call(this);
        /* polish6: soft depth shadow under player / truck */
        this.playerShadow = this.add.ellipse(spawn.x, spawn.y + 14, 40, 16, 0x000000, 0.32).setDepth(18);
        this.playerShadowSoft = this.add.ellipse(spawn.x, spawn.y + 15, 56, 22, 0x000000, 0.14).setDepth(17);
        /* polish6: parallax-lite hill bands (screen-space, scrollFactor 0) */
        this.paraGfx = this.add.graphics().setScrollFactor(0).setDepth(-20);
        this.inTruck = false; this.truckMode = null; this.truckId = null;
        this.waterSub = 0; this.scrap = 0; this.toastT = 3.5; this.cd = 0; this.near = null; this.bouncePhase = 0; this.dustT = 0; this.fx = [];
        this.prevNearId = null; this.shakeT = 0; this.rippleT = 0; this.ambient = [];
        this.mechWowT = 0; this.walkBobT = 0;
        /* polish7: zone signs + mini-map lite (screen-space) */
        this.zoneSignTexts = [];
        var zlist = (C.ZONE_SIGNS || []);
        for (var zi = 0; zi < zlist.length; zi++) {
          var zs = zlist[zi];
          var zt = this.add.text(zs.x, zs.y - 90, zs.label, {
            fontFamily: "Segoe UI, system-ui, sans-serif", fontSize: "12px", fontStyle: "bold",
            color: zs.color || "#fef3c7", backgroundColor: "#0f172a99",
            padding: { x: 8, y: 3 }, stroke: "#000", strokeThickness: 1,
          }).setOrigin(0.5).setDepth(50).setAlpha(0);
          zt.zone = zs;
          this.zoneSignTexts.push(zt);
        }
        this.miniMapGfx = this.add.graphics().setScrollFactor(0).setDepth(60);
        /* polish5 + mobile1: ambient pollen / fireflies (fewer on phone) */
        var ambN = (window.innerWidth || 800) <= 520 ? 10 : 22;
        for (var ai = 0; ai < ambN; ai++) {
          var kind = Math.random() < 0.55 ? "pollen" : "firefly";
          var amb = this.add.circle(
            80 + Math.random() * (C.MAP_W - 160),
            80 + Math.random() * (C.MAP_H - 160),
            kind === "firefly" ? 3 : 2,
            kind === "firefly" ? 0xfacc15 : 0xfef9c3,
            kind === "firefly" ? 0.8 : 0.45
          ).setDepth(30);
          amb.kind = kind; amb.phase = Math.random() * Math.PI * 2;
          amb.vx = (Math.random() - 0.5) * 18; amb.vy = (Math.random() - 0.5) * 12;
          this.ambient.push(amb);
        }
        this.facing = 1; this.faceAngle = 0; /* eyes1: texture faces up */ this.bob = 0; this.zLift = 0; this.zVel = 0;
        this.toast = "Phaser ranch · compound · squiggle track · pond whales · Cybertrucks · Starship";
        if (hooks.onReady) hooks.onReady({ engine: "phaser", frogId: frogId });
        if (hooks.onToast) hooks.onToast(this.toast);
      },
      ensureFrogTexture: function (id, d, size) {
        /* polish3: charming readable frog (Canvas clarity port) */
        var key = "frog_" + id; if (this.textures.exists(key)) return;
        var rt = this.make.graphics({ x: 0, y: 0, add: false });
        var s = size || 56, cx = s * 0.5, cy = s * 0.55, r = s * 0.42;
        rt.fillStyle(hx(d.color), 0.4); rt.fillCircle(cx, cy + 3, r * 1.2);
        rt.fillStyle(hx(d.color), 1); rt.fillCircle(cx, cy, r);
        rt.lineStyle(3.5, 0x0b1220, 1); rt.strokeCircle(cx, cy, r);
        rt.fillStyle(0xfef3c7, 0.95); rt.fillEllipse(cx, cy + r * 0.18, r * 0.7, r * 0.55);
        rt.fillStyle(0xfb7185, 0.45);
        rt.fillEllipse(cx - r * 0.55, cy + r * 0.15, r * 0.28, r * 0.18);
        rt.fillEllipse(cx + r * 0.55, cy + r * 0.15, r * 0.28, r * 0.18);
        rt.fillStyle(0xffffff, 1);
        rt.fillCircle(cx - r * 0.34, cy - r * 0.2, r * 0.24);
        rt.fillCircle(cx + r * 0.34, cy - r * 0.2, r * 0.24);
        rt.lineStyle(1.5, 0x0b1220, 0.9);
        rt.strokeCircle(cx - r * 0.34, cy - r * 0.2, r * 0.24);
        rt.strokeCircle(cx + r * 0.34, cy - r * 0.2, r * 0.24);
        rt.fillStyle(hx(d.accent), 1);
        rt.fillCircle(cx - r * 0.28, cy - r * 0.2, r * 0.12);
        rt.fillCircle(cx + r * 0.4, cy - r * 0.2, r * 0.12);
        rt.fillStyle(0xffffff, 1);
        rt.fillCircle(cx - r * 0.34, cy - r * 0.28, r * 0.06);
        rt.fillCircle(cx + r * 0.34, cy - r * 0.28, r * 0.06);
        rt.lineStyle(2.5, 0x0b1220, 1);
        rt.strokeEllipse(cx, cy + r * 0.35, r * 0.55, r * 0.28);
        rt.fillStyle(hx(d.color), 1); rt.fillEllipse(cx, cy + r * 0.28, r * 0.55, r * 0.22);
        rt.fillStyle(hx(d.hat), 1);
        rt.fillTriangle(cx, cy - r * 1.2, cx - r * 0.75, cy - r * 0.15, cx + r * 0.75, cy - r * 0.15);
        rt.generateTexture(key, s, s); rt.destroy();
      },
      drawCompound: function () {
        var cp = C.COMPOUND || {};
        var yard = cp.yard || { x: 100, y: 2100, w: 600, h: 360 };
        var gar = cp.garage || { x: 700, y: 1400, w: 480, h: 520 };
        var house = cp.house || { x: 120, y: 1420, w: 520, h: 420 };
        this.add.rectangle(yard.x + yard.w / 2, yard.y + yard.h / 2, yard.w, yard.h, 0x468232, 0.7)
          .setStrokeStyle(3, 0x1a3a12, 0.8);
        this.add.text(yard.x + yard.w / 2, yard.y + 14, "Backyard", {
          fontSize: "14px", fontStyle: "bold", color: "#ecfccb", stroke: "#000", strokeThickness: 3,
        }).setOrigin(0.5, 0);
        for (var ai = 0; ai < 44; ai++) {
          var ax = yard.x + 20 + Math.random() * (yard.w - 40);
          var ay = yard.y + 40 + Math.random() * (yard.h - 60);
          var aw = 14 + (ai % 5) * 2, ah = 10 + (ai % 4);
          this.add.ellipse(ax, ay, aw, ah,
            ai % 3 === 0 ? 0xc4a574 : ai % 3 === 1 ? 0x8b6914 : 0xd6d3d1, 0.95
          ).setStrokeStyle(1, 0x292016, 0.6);
          this.add.circle(ax + aw * 0.45, ay - 4, 5 + (ai % 3), ai % 3 === 0 ? 0xc4a574 : 0x8b6914, 0.95);
        }
        this.add.rectangle(gar.x + gar.w / 2, gar.y + gar.h / 2, gar.w, gar.h, 0x6b7280, 0.92)
          .setStrokeStyle(4, 0x0b1220, 1);
        this.garageBay = this.add.rectangle(gar.x + gar.w / 2, gar.y + gar.h - 40, 140, 70, 0x38bdf8, 0.25);
        this.garageDoor = this.add.rectangle(gar.x + gar.w / 2, gar.y + gar.h - 40, 140, 70, 0x111827, 1)
          .setStrokeStyle(2, 0xfbbf24, 1);
        this.garageOpenLabel = this.add.text(gar.x + gar.w / 2, gar.y + gar.h - 90, "", {
          fontSize: "12px", fontStyle: "bold", color: "#bbf7d0", stroke: "#000", strokeThickness: 3,
        }).setOrigin(0.5).setAlpha(0);
        this.garageOpen = 0;
        this.garageDoorY0 = gar.y + gar.h - 40;
        this.garageDoorH0 = 70;
        this.garageCx = gar.x + gar.w / 2;
        this.garageCy = gar.y + gar.h - 40;
        this.add.text(gar.x + gar.w / 2, gar.y + 16, "Garage · James toys", {
          fontSize: "14px", fontStyle: "bold", color: "#fff", stroke: "#000", strokeThickness: 3,
        }).setOrigin(0.5, 0);
        for (var t = 0; t < 40; t++) {
          this.add.rectangle(gar.x + 30 + (t % 8) * 48, gar.y + 60 + Math.floor(t / 8) * 42, 14, 14, 0xfbbf24, 0.85)
            .setStrokeStyle(1, 0x78350f, 0.8);
        }
        this.add.rectangle(house.x + house.w / 2, house.y + house.h / 2, house.w, house.h, 0xd4b896, 0.95)
          .setStrokeStyle(4, 0x3e2723, 1);
        /* polish4: warm windows with room hints */
        var winCols = [0xfef3c7, 0xfde68a, 0xfbbf24, 0xfef9c3];
        for (var wi = 0; wi < 4; wi++) {
          this.add.rectangle(house.x + 80 + wi * 110, house.y + 120, 36, 28, winCols[wi], 0.95)
            .setStrokeStyle(2, 0x1c1210, 1);
          this.add.rectangle(house.x + 80 + wi * 110, house.y + 132, 22, 8, 0x78350f, 0.8);
        }
        for (var wj = 0; wj < 3; wj++) {
          this.add.rectangle(house.x + 110 + wj * 140, house.y + 220, 32, 24, 0xfde68a, 0.9)
            .setStrokeStyle(2, 0x1c1210, 1);
        }
        /* Open doorway glow */
        this.add.rectangle(house.x + house.w * 0.48, house.y + house.h - 36, 36, 52, 0xfbbf24, 0.55)
          .setStrokeStyle(2, 0xfef3c7, 1);
        /* polish8: porch depth + path to door + chimney smoke */
        this.add.rectangle(house.x + house.w / 2, house.y + house.h - 8, house.w - 60, 28, 0xb8956a, 0.9)
          .setStrokeStyle(2, 0x5d4037, 0.8);
        this.add.rectangle(house.x + house.w / 2, house.y + house.h + 18, 70, 50, 0xa09070, 0.75)
          .setStrokeStyle(1, 0x5d4037, 0.6);
        for (var pi = 0; pi < 5; pi++) {
          this.add.rectangle(house.x + 70 + pi * ((house.w - 140) / 4), house.y + house.h - 20, 8, 36, 0x5d4037, 1);
        }
        this.add.rectangle(house.x + house.w * 0.72, house.y - 20, 18, 40, 0x78716c, 1);
        this.chimneySmoke = [];
        for (var sm = 0; sm < 3; sm++) {
          var puff = this.add.circle(house.x + house.w * 0.72, house.y - 40 - sm * 14, 6 + sm * 2, 0xc8c8d0, 0.4);
          puff.smPhase = sm * 0.9; this.chimneySmoke.push(puff);
        }
        /* Blue Bear place-bound pet bounce near porch */
        this.blueBear = this.add.ellipse(house.x + 80, house.y + house.h + 50, 28, 24, 0x3b82f6, 1)
          .setStrokeStyle(2, 0x1e3a8a, 1);
        this.blueBear.baseY = house.y + house.h + 50;
        this.blueBearLabel = this.add.text(house.x + 80, house.y + house.h + 28, "Blue Bear", {
          fontSize: "11px", fontStyle: "bold", color: "#bfdbfe", stroke: "#000", strokeThickness: 3,
        }).setOrigin(0.5, 1);
        this.blueBearBob = 0;
        var roofG = this.add.graphics();
        roofG.fillStyle(0x6d4c41, 1); roofG.lineStyle(3, 0x3e2723, 1);
        roofG.beginPath();
        roofG.moveTo(house.x + house.w / 2, house.y - 48);
        roofG.lineTo(house.x + 12, house.y + 36);
        roofG.lineTo(house.x + house.w - 12, house.y + 36);
        roofG.closePath(); roofG.fillPath(); roofG.strokePath();
        this.add.text(house.x + house.w / 2, house.y + 20, "James · Ranch house", {
          fontSize: "15px", fontStyle: "bold", color: "#fff7ed", stroke: "#000", strokeThickness: 4,
        }).setOrigin(0.5, 0);
        this.drawMech(cp.mech10 || { x: 820, y: 1680, stories: 10 }, 0xa5b4fc, 68);
        this.drawMech(cp.mech100 || { x: 980, y: 1700, stories: 100 }, 0x67e8f9, 110);
        this.drawMech(cp.mech1000 || { x: 340, y: 2420, stories: 1000 }, 0xfcd34d, 220);
      },
      drawMech: function (m, color, h) {
        if (m.stories >= 1000) {
          this.add.ellipse(m.x, m.y - h * 0.5, h * 0.9, h * 0.7, 0xfbbf24, 0.12);
        }
        this.add.ellipse(m.x, m.y + 6, Math.max(36, h * 0.4), 16, 0x0f172a, 0.45);
        this.add.rectangle(m.x, m.y - h * 0.45, Math.max(30, h * 0.3), h, color, 0.95)
          .setStrokeStyle(3, 0x0f172a, 1);
        if (m.stories < 1000) {
          this.add.rectangle(m.x - h * 0.28, m.y - h * 0.65, h * 0.16, h * 0.18, color, 0.9)
            .setStrokeStyle(2, 0x0f172a, 1);
          this.add.rectangle(m.x + h * 0.28, m.y - h * 0.65, h * 0.16, h * 0.18, color, 0.9)
            .setStrokeStyle(2, 0x0f172a, 1);
        }
        this.add.rectangle(m.x, m.y - h * 0.82, Math.max(18, h * 0.18), h * 0.08, 0xfbbf24, 1);
        this.add.text(m.x, m.y - h - 10, m.stories + "-story mech", {
          fontSize: m.stories >= 1000 ? "14px" : "11px", fontStyle: "bold", color: "#fff",
          stroke: "#000", strokeThickness: 3,
        }).setOrigin(0.5, 1);
      },
      drawTrack: function () {
        var tg = this.add.graphics();
        var mounds = C.TRACK_MOUNDS || [];
        for (var mi = 0; mi < mounds.length; mi++) {
          var m = mounds[mi];
          var mh = Math.abs(m.h) * 40 + 18;
          tg.fillStyle(0x000000, 0.25);
          tg.fillEllipse(m.x + 4, m.y + 6, m.r * 1.15, m.r * 0.5);
          tg.fillStyle(m.h >= 0 ? 0x78716c : 0x44403c, 0.9);
          tg.fillEllipse(m.x, m.y - (m.h >= 0 ? mh * 0.15 : 0), m.r * 1.2, m.r * 0.6);
          tg.lineStyle(3, 0x1c1917, 0.8);
          tg.strokeEllipse(m.x, m.y - (m.h >= 0 ? mh * 0.15 : 0), m.r * 1.2, m.r * 0.6);
          if (m.h >= 0) {
            tg.lineStyle(2, 0xfbbf24, 0.45);
            tg.strokeEllipse(m.x, m.y - mh * 0.25, m.r * 0.7, m.r * 0.35);
            tg.strokeEllipse(m.x, m.y - mh * 0.4, m.r * 0.45, m.r * 0.22);
            this.add.text(m.x, m.y - mh * 0.55 - 8, "HILL", {
              fontSize: "10px", fontStyle: "bold", color: "#fef3c7", stroke: "#000", strokeThickness: 2,
            }).setOrigin(0.5);
          }
        }
        polyLine(tg, C.TRACK_MAIN, 0x292524, 46, 0.95, true);
        polyLine(tg, C.TRACK_MAIN, 0xfbbf24, 14, 0.85, true);
        polyLine(tg, C.TRACK_MAIN, 0xfafaf9, 5, 0.9, true);
        polyLine(tg, C.TRACK_BRANCH_A, 0x292524, 28, 0.9, true);
        polyLine(tg, C.TRACK_BRANCH_A, 0xa8a29e, 12, 0.85, true);
        polyLine(tg, C.TRACK_BRANCH_B, 0x292524, 26, 0.88, true);
        polyLine(tg, C.TRACK_BRANCH_B, 0xa8a29e, 11, 0.82, true);
        var ramps = C.RAMPS || [];
        for (var ri = 0; ri < ramps.length; ri++) {
          var r = ramps[ri];
          this.add.rectangle(r.x, r.y, r.w, r.h, 0xf59e0b, 0.85).setStrokeStyle(2, 0x78350f, 1).setAngle(-18);
        }
        var track = C.AREAS[1];
        this.add.text(track.x + track.w * 0.5, track.y + 18, "Monster truck track", {
          fontSize: "16px", fontStyle: "bold", color: "#f5f5f4", stroke: "#000", strokeThickness: 4,
        }).setOrigin(0.5, 0);
        /* polish9: start/finish gate */
        var gx = 1870, gy = 2225;
        var gateG = this.add.graphics();
        gateG.fillStyle(0xf8fafc, 1); gateG.lineStyle(2, 0x0f172a, 1);
        gateG.fillRect(gx - 74, gy - 56, 10, 56); gateG.strokeRect(gx - 74, gy - 56, 10, 56);
        gateG.fillRect(gx + 64, gy - 56, 10, 56); gateG.strokeRect(gx + 64, gy - 56, 10, 56);
        for (var gi = 0; gi < 8; gi++) {
          gateG.fillStyle(gi % 2 === 0 ? 0x0a0a0a : 0xf8fafc, 1);
          gateG.fillRect(gx - 64 + gi * 16, gy - 56, 16, 16);
        }
        gateG.lineStyle(2, 0xfbbf24, 1); gateG.strokeRect(gx - 64, gy - 56, 128, 16);
        this.add.text(gx, gy - 48, "START / FINISH", {
          fontSize: "11px", fontStyle: "bold", color: "#fef3c7", stroke: "#000", strokeThickness: 3,
        }).setOrigin(0.5);
      },
      drawPondLife: function () {
        var pond = C.AREAS[2]; this.fish = []; this.whales = []; this.schools = [];
        /* polish8: fish schools */
        for (var sc = 0; sc < 5; sc++) {
          this.schools.push({
            x: pond.x + 100 + Math.random() * (pond.w - 200),
            y: pond.y + 100 + Math.random() * (pond.h - 200),
            phase: Math.random() * Math.PI * 2,
          });
        }
        for (var f = 0; f < 30; f++) {
          var sch = this.schools[f % 5];
          var fx = sch.x + (Math.random() - 0.5) * 60;
          var fy = sch.y + (Math.random() - 0.5) * 40;
          var fish = this.add.ellipse(fx, fy, 18 + Math.random() * 10, 8 + Math.random() * 4, 0xfde68a, 0.95)
            .setStrokeStyle(2, 0x92400e, 1);
          fish.phase = Math.random() * Math.PI * 2; fish.bx = fx; fish.by = fy;
          fish.school = f % 5; fish.ox = fx - sch.x; fish.oy = fy - sch.y;
          this.fish.push(fish);
        }
        for (var w = 0; w < 5; w++) {
          var wx = pond.x + 140 + Math.random() * (pond.w - 280);
          var wy = pond.y + 120 + Math.random() * (pond.h - 240);
          var whale = this.add.ellipse(wx, wy, 96 + Math.random() * 48, 36 + Math.random() * 16, 0x7dd3fc, 0.95)
            .setStrokeStyle(3.5, 0x0c4a6e, 1);
          whale.phase = Math.random() * Math.PI * 2; whale.bx = wx; whale.by = wy;
          whale.breach = Math.random() * Math.PI * 2; whale.breachAmp = 42 + Math.random() * 30;
          this.whales.push(whale);
          this.add.text(wx, wy - 28, "whale", { fontSize: "10px", color: "#e0f2fe", stroke: "#000", strokeThickness: 2 }).setOrigin(0.5);
        }
        this.add.text(pond.x + pond.w * 0.5, pond.y + 16, "Pond · fishies & whales", {
          fontSize: "16px", fontStyle: "bold", color: "#ecfeff", stroke: "#000", strokeThickness: 4,
        }).setOrigin(0.5, 0);
      },
      drawStarshipApproach: function () {
        var path = C.STARSHIP_APPROACH; if (!path || !path.length) return;
        var g = this.add.graphics();
        g.lineStyle(6, 0xfbbf24, 0.45); g.beginPath(); g.moveTo(path[0][0], path[0][1]);
        for (var i = 1; i < path.length; i++) g.lineTo(path[i][0], path[i][1]); g.strokePath();
        g.lineStyle(2, 0xfef3c7, 0.7); g.beginPath(); g.moveTo(path[0][0], path[0][1]);
        for (var j = 1; j < path.length; j++) g.lineTo(path[j][0], path[j][1]); g.strokePath();
      },
      drawTrucks: function () {
        /* polish3: wedge Cybertruck silhouette + wheels */
        this.parkedTrucks = [];
        var spots = C.TRUCK_SPOTS || [];
        for (var i = 0; i < spots.length; i++) {
          var s = spots[i];
          var accent = s.id === "shared" ? 0xfbbf24 : hx((C.FROG_DEFS[s.id] || def).color);
          var g = this.add.graphics();
          /* polish8: angular stainless + light bar + wheel arches */
          g.fillStyle(0xc5ced8, 1); g.lineStyle(3, 0x111827, 1);
          g.beginPath();
          g.moveTo(s.x - 46, s.y + 10);
          g.lineTo(s.x - 44, s.y - 4);
          g.lineTo(s.x - 16, s.y - 8);
          g.lineTo(s.x - 4, s.y - 14);
          g.lineTo(s.x + 8, s.y - 28);
          g.lineTo(s.x + 48, s.y - 8);
          g.lineTo(s.x + 54, s.y + 4);
          g.lineTo(s.x + 50, s.y + 12);
          g.closePath(); g.fillPath(); g.strokePath();
          g.lineStyle(1.5, 0xffffff, 0.55);
          g.beginPath(); g.moveTo(s.x - 40, s.y); g.lineTo(s.x + 6, s.y - 22); g.lineTo(s.x + 44, s.y - 6); g.strokePath();
          g.fillStyle(accent, 0.95);
          g.fillRect(s.x - 2, s.y - 20, 30, 14);
          g.lineStyle(2, 0x0b1220, 0.9); g.strokeRect(s.x - 2, s.y - 20, 30, 14);
          g.fillStyle(0xfef08a, 1); g.fillRect(s.x + 42, s.y - 8, 14, 6); /* light bar */
          g.lineStyle(2, 0x94a3b8, 0.8);
          g.beginPath(); g.arc(s.x - 24, s.y + 10, 12, Math.PI * 1.1, Math.PI * 1.9); g.strokePath();
          g.beginPath(); g.arc(s.x + 26, s.y + 9, 12, Math.PI * 1.1, Math.PI * 1.9); g.strokePath();
          g.fillStyle(0x0f172a, 1);
          g.fillCircle(s.x - 24, s.y + 12, 9);
          g.fillCircle(s.x - 8, s.y + 12, 7);
          g.fillCircle(s.x + 26, s.y + 11, 9);
          var body = this.add.rectangle(s.x, s.y, 78, 34, 0x9ca3af, 0.01); // hit proxy for pulse
          var cab = this.add.rectangle(s.x + 8, s.y - 4, 36, 16, accent, 0.01);
          var label = s.id === "shared" ? "★ ALL ABOARD · 4" : ("Cybertruck · " + (C.FROG_DEFS[s.id] || {}).name);
          if (s.id === "shared") {
            g.lineStyle(3, 0xfbbf24, 0.85);
            g.strokeEllipse(s.x, s.y + 8, 100, 36);
            var ids = ["james", "jimmy", "bubbles", "rexy"];
            for (var si = 0; si < 4; si++) {
              var col = hx((C.FROG_DEFS[ids[si]] || def).color);
              g.fillStyle(col, 1);
              g.fillCircle(s.x + (si - 1.5) * 14, s.y - 26, 6);
            }
          }
          var lbl = this.add.text(s.x, s.y - 32, label, {
            fontSize: s.id === "shared" ? "13px" : "11px", fontStyle: "bold",
            color: s.id === "shared" ? "#fef3c7" : "#fde68a", stroke: "#000", strokeThickness: 3,
          }).setOrigin(0.5, 1);
          this.parkedTrucks.push({ spot: s, body: body, cab: cab, gfx: g, label: lbl });
        }
      },
      update: function (time, delta) {
        var dt = Math.min(0.05, delta / 1000);
        this.cd = Math.max(0, this.cd - dt); this.toastT = Math.max(0, this.toastT - dt);
        this.bob += dt * (this.inTruck ? 14 : 10);
        /* polish10: soft dusk sky shift over play time */
        this.dayT = (this.dayT || 0) + dt;
        var day = (this.dayT % 420) / 420;
        var dusk = day < 0.45 ? 0 : (day < 0.7 ? (day - 0.45) / 0.25 : (day < 0.9 ? 1 : Math.max(0, 1 - (day - 0.9) / 0.1)));
        var bgR = Math.round(26 + dusk * 40), bgG = Math.round(58 - dusk * 20), bgB = Math.round(26 + dusk * 10);
        this.cameras.main.setBackgroundColor(Phaser.Display.Color.GetColor(bgR, bgG, bgB));
        /* polish3: snappier locomotion */
        /* tapsteer1: faster walk + drive */
        var maxSp = this.inTruck ? 440 : 290, accel = this.inTruck ? 1050 : 920, fric = this.inTruck ? 5.2 : 8.8;
        var body = this.player.body;
        var steer = mergedSteer();
        if (steer.x || steer.y) {
          var len = Math.hypot(steer.x, steer.y) || 1;
          body.velocity.x += (steer.x / len) * accel * dt;
          body.velocity.y += (steer.y / len) * accel * dt;
          /* eyes1: face walk direction; idle keeps last faceAngle */
          this.faceAngle = Math.atan2(steer.y / len, steer.x / len) + Math.PI / 2;
          if (steer.x) this.facing = steer.x > 0 ? 1 : -1;
        }
        tickTapMarker(dt);
        body.velocity.x *= Math.max(0, 1 - fric * dt);
        body.velocity.y *= Math.max(0, 1 - fric * dt);
        var sp = Math.hypot(body.velocity.x, body.velocity.y);
        if (sp > maxSp) { body.velocity.x = (body.velocity.x / sp) * maxSp; body.velocity.y = (body.velocity.y / sp) * maxSp; }
        /* solid1: solid walls / mechs / parked trucks (doorway open) */
        if (C.resolveSolid) {
          var solid = C.resolveSolid(this.player.x, this.player.y, this.inTruck ? 38 : 22, {
            garageOpen: this.garageOpen || 0,
            inTruck: !!this.inTruck,
            softPond: !this.inTruck,
          });
          if (solid.hit) {
            var pdx = solid.x - this.player.x, pdy = solid.y - this.player.y;
            var plen = Math.hypot(pdx, pdy) || 1;
            var nx = pdx / plen, ny = pdy / plen;
            var into = body.velocity.x * nx + body.velocity.y * ny;
            if (into < 0) { body.velocity.x -= nx * into; body.velocity.y -= ny * into; }
            else { body.velocity.x *= 0.55; body.velocity.y *= 0.55; }
            this.player.setPosition(solid.x, solid.y);
          }
        }
        if (this.inTruck && C.rampAt) {
          var ramp = C.rampAt(this.player.x, this.player.y);
          if (ramp && sp > 40) { this.zVel = Math.max(this.zVel, 180 * (ramp.boost || 1.3)); this.scrap += 0.02; }
        }
        /* polish9: brief air hang at apex */
        var gFall = 420;
        if (this.zLift > 18 && Math.abs(this.zVel) < 80) gFall *= 0.38;
        this.zVel -= gFall * dt; this.zLift = Math.max(0, this.zLift + this.zVel * dt);
        if (this.zLift <= 0) { this.zLift = 0; this.zVel = 0; }
        /* polish9: lap sparkle at gate */
        if (this.inTruck && C.onTrack && C.onTrack(this.player.x, this.player.y) && this.zLift < 8) {
          this.lapCd = Math.max(0, (this.lapCd || 0) - dt);
          var side = (this.player.x - 1870) * 0.55 + (this.player.y - 2225) * (-0.85);
          var nearG = Math.abs(this.player.x - 1870) < 110 && Math.abs(this.player.y - 2225) < 60;
          if (nearG && this.lapSide && side * this.lapSide < 0 && this.lapCd <= 0) {
            this.lapCount = (this.lapCount || 0) + 1; this.lapCd = 2.4; this.scrap += 8;
            this.toast = "LAP " + this.lapCount + " · sparkle finish!"; this.toastT = 1.6;
            for (var lpi = 0; lpi < 16; lpi++) {
              var lang = Math.random() * Math.PI * 2, lsp = 40 + Math.random() * 80;
              var lp = this.add.circle(1870, 2225, 2 + Math.random() * 3, 0xfde68a, 0.95).setDepth(40);
              this.fx.push({ g: lp, life: 0.5, vx: Math.cos(lang) * lsp, vy: Math.sin(lang) * lsp });
            }
          }
          if (nearG || Math.abs(side) > 40) this.lapSide = side >= 0 ? 1 : -1;
        }
        var wet = C.inPond && C.inPond(this.player.x, this.player.y);
        if (this.inTruck && wet) {
          var plunge = Math.max(0, -this.zVel) + (this.zLift > 2 ? 40 : 0);
          this.waterSub = Math.min(1.15, 0.45 + plunge / 400);
          body.velocity.x *= Math.max(0, 1 - 1.8 * dt); body.velocity.y *= Math.max(0, 1 - 1.8 * dt);
        } else this.waterSub = Math.max(0, this.waterSub - dt * 1.6);
        this.player.setFlipX(false).setRotation(this.faceAngle || 0).setVisible(!this.inTruck);
        this.nameTag.setPosition(this.player.x, this.player.y - 28 - this.zLift * 0.08);
        this.nameTag.setVisible(true);
        /* polish9: aboard frog icons when shared truck */
        var sharedOn = this.inTruck && this.truckMode === "shared";
        if (this.aboardLabel) {
          this.aboardLabel.setVisible(!!sharedOn).setPosition(this.player.x, this.player.y + 28);
        }
        if (this.aboardIcons) {
          for (var abi = 0; abi < this.aboardIcons.length; abi++) {
            var ab = this.aboardIcons[abi];
            var show = !!sharedOn;
            ab.dot.setVisible(show); ab.letter.setVisible(show);
            if (show) {
              var ax = this.player.x + (abi - 1.5) * 16;
              var ay = this.player.y - 22 - this.zLift * 0.08;
              ab.dot.setPosition(ax, ay); ab.letter.setPosition(ax, ay);
            }
          }
        }
        if (this.kitFxT > 0) {
          this.kitFxT -= dt;
          var kfg = this.kitFxGfx;
          if (!kfg) {
            kfg = this.add.graphics().setDepth(35); this.kitFxGfx = kfg;
          }
          kfg.clear();
          var ka = Math.min(1, this.kitFxT * 2);
          if (this.kitFxKind === "rocket" || this.kitFxKind === "afterburners") {
            kfg.fillStyle(0xfb923c, ka * 0.7);
            kfg.fillTriangle(this.player.x, this.player.y - 6, this.player.x - 12, this.player.y + 28, this.player.x + 12, this.player.y + 28);
          } else if (this.kitFxKind === "hover") {
            kfg.lineStyle(3, 0x7dd3fc, ka); kfg.strokeEllipse(this.player.x, this.player.y + 10, 56, 18);
          } else if (this.kitFxKind === "drone") {
            kfg.fillStyle(0xe2e8f0, ka); kfg.fillRect(this.player.x - 10, this.player.y - 36, 20, 8);
          } else {
            kfg.lineStyle(2, 0xfbbf24, ka); kfg.strokeCircle(this.player.x, this.player.y, 28 + (0.55 - this.kitFxT) * 40);
          }
          if (this.kitFxT <= 0) kfg.clear();
        }
        /* polish4: bounce + spray / bubbles / walk dust */
        this.bouncePhase = (this.bouncePhase || 0) + dt * (3 + sp * 0.02);
        var bounce = this.inTruck && this.zLift < 2 ? Math.sin(this.bouncePhase * 2.4) * Math.min(1.2, sp / 260) * 3.2 : 0;
        if (!this.inTruck && !wet && sp > 45) {
          this.dustT = (this.dustT || 0) - dt;
          if (this.dustT <= 0) {
            this.dustT = 0.22;
            var d = this.add.circle(this.player.x - this.facing * 6, this.player.y + 10, 4, 0xb8a070, 0.45).setDepth(15);
            this.fx.push({ g: d, life: 0.35 });
          }
        }
        /* polish5: track race dust */
        if (this.inTruck && !wet && sp > 110 && this.zLift < 2 && C.onTrack && C.onTrack(this.player.x, this.player.y)) {
          if (Math.random() < dt * (2.5 + sp * 0.01)) {
            var td = this.add.circle(this.player.x - this.facing * 20, this.player.y + 10, 5 + Math.random() * 4, 0xb8a070, 0.5).setDepth(15);
            this.fx.push({ g: td, life: 0.4 });
          }
        }
        if (this.inTruck && wet) {
          if (this.waterSub > 0.7 && Math.random() < dt * 5) {
            var bub = this.add.circle(this.player.x + (Math.random() - 0.5) * 24, this.player.y, 3 + Math.random() * 3, 0xbae6fd, 0.55).setDepth(21);
            this.fx.push({ g: bub, life: 0.55, rise: true });
          } else if (this.waterSub <= 0.7 && sp > 30 && Math.random() < dt * 4) {
            var spr = this.add.circle(this.player.x - this.facing * 14, this.player.y + 4, 3, 0xe0f2fe, 0.7).setDepth(21);
            this.fx.push({ g: spr, life: 0.4, rise: true });
            var rip = this.add.ellipse(this.player.x, this.player.y + 4, 12, 5, 0xbae6fd, 0.01).setStrokeStyle(2, 0xbae6fd, 0.7).setDepth(14);
            this.fx.push({ g: rip, life: 0.7, ripple: true });
          }
        }
        /* polish5: ambient pond ripples */
        this.rippleT = (this.rippleT || 0) - dt;
        if (this.rippleT <= 0 && C.AREAS) {
          this.rippleT = 0.7 + Math.random() * 0.8;
          var pondA = (C.AREAS && C.AREAS[2]) || null;
          if (pondA) {
            var rx = pondA.x + 80 + Math.random() * (pondA.w - 160);
            var ry = pondA.y + 80 + Math.random() * (pondA.h - 160);
            var rip2 = this.add.ellipse(rx, ry, 10, 4, 0xbae6fd, 0.01).setStrokeStyle(2, 0xe0f2fe, 0.65).setDepth(8);
            this.fx.push({ g: rip2, life: 1.0, ripple: true });
          }
        }
        /* polish5: garage door open when near */
        if (this.garageDoor) {
          var dGar = Phaser.Math.Distance.Between(this.player.x, this.player.y, this.garageCx, this.garageCy);
          var want = dGar < 260 ? 1 : 0;
          this.garageOpen = Phaser.Math.Clamp(this.garageOpen + (want ? 2.2 : -1.4) * dt, 0, 1);
          var lift = this.garageOpen * 52;
          this.garageDoor.setDisplaySize(140, Math.max(4, this.garageDoorH0 - lift));
          this.garageDoor.y = this.garageDoorY0 - lift * 0.5;
          this.garageDoor.setStrokeStyle(2, this.garageOpen > 0.4 ? 0x86efac : 0xfbbf24, 1);
          if (this.garageOpenLabel) {
            this.garageOpenLabel.setAlpha(this.garageOpen > 0.35 ? this.garageOpen : 0);
            this.garageOpenLabel.setText(this.garageOpen > 0.35 ? "OPEN" : "");
          }
        }
        /* polish5: ambient drift */
        for (var ami = 0; ami < (this.ambient || []).length; ami++) {
          var am = this.ambient[ami];
          am.phase += dt * (am.kind === "firefly" ? 3.2 : 1.4);
          am.x += (am.vx + Math.sin(am.phase) * 8) * dt;
          am.y += (am.vy + Math.cos(am.phase * 0.7) * 6) * dt;
          if (am.kind === "firefly") am.setAlpha(0.35 + 0.65 * Math.abs(Math.sin(am.phase)));
          if (am.x < 40) am.x = C.MAP_W - 40;
          if (am.x > C.MAP_W - 40) am.x = 40;
          if (am.y < 40) am.y = C.MAP_H - 40;
          if (am.y > C.MAP_H - 40) am.y = 40;
        }
        /* polish5: tiny land shake */
        var wasAir = this._wasAir;
        this._wasAir = this.zLift > 2;
        if (wasAir && this.zLift <= 0 && this.inTruck) {
          this.shakeT = Math.max(this.shakeT || 0, 0.1);
          this.cameras.main.shake(90, 0.0035);
        }
        if (this.shakeT > 0) this.shakeT -= dt;
        /* polish6: depth shadows under frog / truck */
        if (this.playerShadow) {
          var shY = this.player.y + 12 + (this.inTruck ? 4 : 0);
          var shScale = 1 + Math.min(0.35, this.zLift * 0.004);
          this.playerShadow.setPosition(this.player.x, shY);
          this.playerShadow.setDisplaySize(40 * shScale * (this.inTruck ? 1.6 : 1), 14 * shScale);
          this.playerShadow.setAlpha(this.zLift > 4 ? 0.14 : 0.32);
          if (this.playerShadowSoft) {
            this.playerShadowSoft.setPosition(this.player.x, shY + 1);
            this.playerShadowSoft.setDisplaySize(56 * shScale * (this.inTruck ? 1.5 : 1), 20 * shScale);
            this.playerShadowSoft.setAlpha(this.zLift > 4 ? 0.06 : 0.14);
          }
        }
        /* polish6: walk cam bob (tilt-lite) */
        if (!this.inTruck && sp > 40) {
          this.walkBobT = (this.walkBobT || 0) + dt * 10;
          this.cameras.main.setAngle(Math.sin(this.walkBobT) * 0.12);
        } else {
          this.cameras.main.setAngle(0);
        }
        /* polish6: parallax-lite hills */
        if (this.paraGfx) {
          var cam = this.cameras.main;
          var vw = cam.width, vh = cam.height;
          var ox = -cam.scrollX * 0.08, oy = -cam.scrollY * 0.04;
          this.paraGfx.clear();
          this.paraGfx.fillStyle(0x1e3a5f, 0.35);
          this.paraGfx.beginPath();
          this.paraGfx.moveTo(0, vh * 0.18 + oy);
          for (var pi = 0; pi <= 12; pi++) {
            this.paraGfx.lineTo((pi / 12) * vw + ox, vh * 0.14 + Math.sin(pi * 0.9 + time * 0.0004) * 18 + oy);
          }
          this.paraGfx.lineTo(vw, vh * 0.32); this.paraGfx.lineTo(0, vh * 0.32); this.paraGfx.closePath(); this.paraGfx.fillPath();
          this.paraGfx.fillStyle(0x2f6a3a, 0.4);
          this.paraGfx.beginPath();
          this.paraGfx.moveTo(0, vh * 0.24 + oy * 1.4);
          for (var pj = 0; pj <= 12; pj++) {
            this.paraGfx.lineTo((pj / 12) * vw + ox * 1.6, vh * 0.2 + Math.sin(pj * 1.1 + 1.2) * 12 + oy * 1.4);
          }
          this.paraGfx.lineTo(vw, vh * 0.38); this.paraGfx.lineTo(0, vh * 0.38); this.paraGfx.closePath(); this.paraGfx.fillPath();
        }
        /* polish6: James 1000-story mech wow tip */
        var m1000 = (C.COMPOUND && C.COMPOUND.mech1000) || { x: 340, y: 2420 };
        var dMech = Phaser.Math.Distance.Between(this.player.x, this.player.y, m1000.x, m1000.y);
        if (dMech < 170 && this.toastT <= 0.3) {
          this.toast = "★ WOW · James 1000-story mech · scale tease";
          this.toastT = 1.8;
          if (hooks.onToast) hooks.onToast(this.toast);
        }
        for (var fi = this.fx.length - 1; fi >= 0; fi--) {
          var fx = this.fx[fi];
          fx.life -= dt;
          if (fx.rise) fx.g.y -= 40 * dt;
          if (fx.vx) { fx.g.x += fx.vx * dt; fx.vx *= 0.96; }
          if (fx.vy) { fx.g.y += fx.vy * dt; fx.vy += 40 * dt; }
          if (fx.ripple && fx.g.scaleX !== undefined) {
            fx.g.scaleX = (fx.g.scaleX || 1) + dt * 2.2;
            fx.g.scaleY = (fx.g.scaleY || 1) + dt * 2.2;
          }
          fx.g.setAlpha(Math.max(0, fx.life * 1.5));
          if (fx.life <= 0) { fx.g.destroy(); this.fx.splice(fi, 1); }
        }
        if (this.inTruck) {
          this.truckBody.setVisible(true); this.truckAccent.setVisible(true);
          this.truckBody.setPosition(this.player.x, this.player.y - this.zLift * 0.06 - bounce);
          this.truckAccent.setPosition(this.player.x + 6 * this.facing, this.player.y - 4 - this.zLift * 0.06 - bounce);
          this.truckBody.setScale(this.facing < 0 ? -1 : 1, 1);
          var dive = this.waterSub > 0.7;
          this.truckBody.setFillStyle(dive ? 0x64748b : 0x9ca3af, dive ? 0.7 : 1);
          this.waterClip.setVisible(wet);
          if (wet) { this.waterClip.setPosition(this.player.x, this.player.y + 8 + this.waterSub * 6); this.waterClip.setAlpha(0.35 + this.waterSub * 0.4); }
        } else { this.truckBody.setVisible(false); this.truckAccent.setVisible(false); this.waterClip.setVisible(false); }
        /* polish8: school drift + whale breach arcs */
        for (var sci = 0; sci < (this.schools || []).length; sci++) {
          var scc = this.schools[sci]; scc.phase += dt * 0.4;
          scc.x += Math.cos(scc.phase) * 18 * dt; scc.y += Math.sin(scc.phase * 0.7) * 12 * dt;
        }
        for (var fi = 0; fi < this.fish.length; fi++) {
          var fish = this.fish[fi]; fish.phase += dt * 2;
          var sch2 = this.schools && this.schools[fish.school];
          if (sch2) {
            fish.x = sch2.x + (fish.ox || 0) + Math.sin(fish.phase) * 8;
            fish.y = sch2.y + (fish.oy || 0) + Math.cos(fish.phase * 0.7) * 6;
          } else {
            fish.x = fish.bx + Math.sin(fish.phase) * 18; fish.y = fish.by + Math.cos(fish.phase * 0.7) * 10;
          }
        }
        for (var wi = 0; wi < this.whales.length; wi++) {
          var wh = this.whales[wi]; wh.phase += dt * 0.7; wh.breach = (wh.breach || 0) + dt * 0.7;
          var bl = Math.max(0, Math.sin(wh.breach)) * (wh.breachAmp || 48);
          wh.x = wh.bx + Math.sin(wh.phase) * 40;
          wh.y = wh.by + Math.cos(wh.phase * 0.55) * 22 - bl * 0.55;
        }
        if (this.blueBear) {
          this.blueBearBob = (this.blueBearBob || 0) + dt * 3.2;
          var bb = Math.abs(Math.sin(this.blueBearBob)) * 8;
          this.blueBear.y = this.blueBear.baseY - bb;
          if (this.blueBearLabel) this.blueBearLabel.y = this.blueBear.baseY - bb - 22;
        }
        for (var smi = 0; smi < (this.chimneySmoke || []).length; smi++) {
          var puff = this.chimneySmoke[smi];
          puff.smPhase = (puff.smPhase || 0) + dt * 1.2;
          puff.y -= dt * 12; puff.x += Math.sin(puff.smPhase) * 8 * dt;
          puff.setAlpha(0.35 + 0.15 * Math.sin(puff.smPhase));
          if (puff.y < (puff.resetY || puff.y) - 50) { if (puff.resetY == null) puff.resetY = puff.y + 50; puff.y = puff.resetY; }
        }
        for (var ci = 0; ci < this.companions.length; ci++) {
          var c = this.companions[ci];
          var lag = c.followLag || 0.4;
          if (this.inTruck && this.truckMode === "shared") {
            var ox = (ci - 1) * 14, oy = -10 - (ci % 2) * 8;
            c.x += (this.player.x + ox - c.x) * Math.min(1, 8 * dt);
            c.y += (this.player.y + oy - this.zLift * 0.06 - c.y) * Math.min(1, 8 * dt);
            c.idleBounce = (c.idleBounce || 0) + dt * 5;
            c.faceAngle = this.faceAngle || 0;
          } else {
            c.timer -= dt;
            if (c.timer <= 0) {
              var behind = -this.facing * (36 + lag * 70);
              c.tx = Phaser.Math.Clamp(this.player.x + behind + (Math.random() - 0.5) * (80 + lag * 50), 80, C.MAP_W - 80);
              c.ty = Phaser.Math.Clamp(this.player.y + (Math.random() - 0.5) * (80 + lag * 40), 80, C.MAP_H - 80);
              c.timer = 0.9 + lag + Math.random() * (1.3 + lag);
            }
            var followK = Math.min(1, (1.05 / (0.7 + lag)) * dt);
            var cdx = c.tx - c.x, cdy = c.ty - c.y;
            c.x += cdx * followK; c.y += cdy * followK;
            /* eyes1: companions face where they're moving */
            if (Math.hypot(cdx, cdy) > 4) c.faceAngle = Math.atan2(cdy, cdx) + Math.PI / 2;
            c.idleBounce = (c.idleBounce || 0) + dt * 4.2;
          }
          var bobY = Math.abs(Math.sin(c.idleBounce)) * 3.2;
          c.setY(c.y); /* position already includes follow; visual bob via nameTag offset */
          c.setFlipX(false).setRotation(c.faceAngle || 0);
          if (c.nameTag) c.nameTag.setPosition(c.x, c.y - 28 - bobY).setVisible(true);
          if (c.chatT > 0) c.chatT -= dt;
          else if (Math.random() < dt * 0.08) {
            var lines = (C.AI_CHAT && C.AI_CHAT[c.frogId]) || [];
            if (lines.length) { c.chatLine = lines[Math.floor(Math.random() * lines.length)]; c.chatT = 2.1; }
          }
          if (c.chatBubble) {
            if (c.chatT > 0 && c.chatLine) {
              c.chatBubble.setText(c.chatLine).setPosition(c.x, c.y - 44 - bobY).setVisible(true).setAlpha(Math.min(1, c.chatT));
            } else c.chatBubble.setVisible(false);
          }
          c.setScale(1.05, 1.05 + Math.abs(Math.sin(c.idleBounce)) * 0.04);
        }
        /* polish7: zone signs fade */
        for (var zsi = 0; zsi < (this.zoneSignTexts || []).length; zsi++) {
          var zt = this.zoneSignTexts[zsi];
          var za = C.zoneSignAlpha ? C.zoneSignAlpha(zt.zone, this.player.x, this.player.y) : 0;
          zt.setAlpha(za);
        }
        /* polish7 + mobile1: mini-map lite — shrink/hide on phone */
        if (this.miniMapGfx) {
          var g = this.miniMapGfx; g.clear();
          var vwP = window.innerWidth || 800, vhP = window.innerHeight || 600;
          var narrowP = vwP <= 520 || (vwP <= 900 && vhP <= 480);
          if (vwP <= 900 && vhP <= 480) { /* landscape phone: hide */ }
          else {
          var mw = narrowP ? 72 : 108, mh = narrowP ? 54 : 82;
          var ox = narrowP ? 8 : 12, oy = narrowP ? 56 : 88; /* polish10/mobile1 */
          g.fillStyle(0x0f172a, 0.72); g.fillRect(ox, oy, mw, mh);
          g.lineStyle(1.5, 0xfbbf24, 0.55); g.strokeRect(ox, oy, mw, mh);
          function mmx(x) { return ox + (x / C.MAP_W) * mw; }
          function mmy(y) { return oy + (y / C.MAP_H) * mh; }
          var marks = [[380,1630,0xfbbf24],[2780,2270,0xa8a29e],[3160,670,0x67e8f9],[940,1660,0xfdba74],[360,320,0xfde68a]];
          for (var mi = 0; mi < marks.length; mi++) {
            g.fillStyle(marks[mi][2], 1); g.fillCircle(mmx(marks[mi][0]), mmy(marks[mi][1]), 2.8);
          }
          g.fillStyle(hx(def.color), 1); g.fillCircle(mmx(this.player.x), mmy(this.player.y), 4);
          for (var cmi = 0; cmi < this.companions.length; cmi++) {
            var cm = this.companions[cmi];
            var cdef2 = C.FROG_DEFS[cm.frogId] || def;
            g.fillStyle(hx(cdef2.color || "#fff"), 1);
            g.fillCircle(mmx(cm.x), mmy(cm.y), 2.6);
          }
          } /* end narrow/landscape gate */
        }
        for (var pi = 0; pi < this.parkedTrucks.length; pi++) {
          var pt = this.parkedTrucks[pi];
          var hid = pt.spot.id === "shared" ? "truck-shared" : "truck-" + pt.spot.id;
          var taken = this.inTruck && (this.truckId === hid || (this.truckMode === "shared" && pt.spot.id === "shared"));
          pt.body.setVisible(!taken); pt.cab.setVisible(!taken);
          if (pt.gfx) pt.gfx.setVisible(!taken);
          if (!taken) {
            var dT = Phaser.Math.Distance.Between(this.player.x, this.player.y, pt.spot.x, pt.spot.y);
            var nearT = dT < 90;
            var pulse = nearT ? 1 + Math.abs(Math.sin(this.bob * 1.4)) * 0.06 : 1;
            pt.body.setScale(pulse); pt.cab.setScale(pulse);
            if (pt.gfx) { pt.gfx.setScale(pulse); pt.gfx.setAlpha(nearT ? 1 : 0.92); }
            if (nearT) pt.body.setStrokeStyle(3, 0xfbbf24, 1);
            else pt.body.setStrokeStyle(3, 0x111827, 1);
          }
        }
        this.near = C.nearestHotspot(this.player.x, this.player.y, 80);
        if (this.near && this.near.id !== this.prevNearId) {
          for (var spi = 0; spi < 10; spi++) {
            var ang = Math.random() * Math.PI * 2, ssp = 30 + Math.random() * 70;
            var spk = this.add.circle(this.near.x, this.near.y, 2 + Math.random() * 2, 0xfde68a, 0.95).setDepth(40);
            this.fx.push({ g: spk, life: 0.45, vx: Math.cos(ang) * ssp, vy: Math.sin(ang) * ssp });
          }
          this.prevNearId = this.near.id;
        } else if (!this.near) this.prevNearId = null;
        for (var hi = 0; hi < this.hotGfx.length; hi++) {
          var hg = this.hotGfx[hi];
          hg.ring.setAlpha(this.near && this.near.id === hg.data.id ? 0.65 : 0.14);
          if (hg.ring.setScale) hg.ring.setScale(this.near && this.near.id === hg.data.id ? 1.25 : 1);
        }
        if (wantInteract) { wantInteract = false; this.doInteract(); }
        if (wantAbility) { wantAbility = false; this.doAbility(); }
        if (hooks.onHud) {
          var walk = "🐸 Walk";
          if (this.inTruck) {
            if (this.zLift > 8) walk = "🚚 AIR!";
            else if (wet && this.waterSub > 0.75) walk = "🚚 Under";
            else if (wet) walk = "🚚 On water";
            else walk = this.truckMode === "shared" ? "🚚 All aboard" : "🚚 Drive";
          }
          hooks.onHud({
            mode: "ranch", label: C.areaNameAt(this.player.x, this.player.y) + " · Phaser",
            scrap: Math.floor(this.scrap),
            tip: this.toastT > 0 ? this.toast : (this.inTruck ? "EXIT TRUCK · INTERACT / E" : (this.near ? ((C.isTruckHotspot(this.near) ? "BOARD · " : "⚡ ") + this.near.tip + " · INTERACT / E") : "")),
            near: this.near, ability: def.ability, cd: this.cd, walk: walk,
          });
        }
      },
      doInteract: function () {
        /* polish10: EXIT truck anytime */
        if (this.inTruck) {
          this.inTruck = false; this.truckMode = null; this.truckId = null;
          this.toast = "Parked · walking"; this.toastT = 1.8;
          if (hooks.onToast) hooks.onToast(this.toast);
          return;
        }
        if (!this.near) return;
        var id = this.near.id;
        if (C.isTruckHotspot(this.near)) {
          this.inTruck = true; this.truckMode = this.near.mode || "solo"; this.truckId = id; this.scrap += 1;
          this.toast = this.truckMode === "shared"
            ? "All aboard! Four froggies · one Cybertruck · hit the jumps!"
            : "Driving Cybertruck · hit the jumps!";
          this.toastT = 2.4;
        } else if (id === "fishies") {
          this.toast = "Splash! Fishies & whales scatter"; this.toastT = 2; this.scrap += 2;
          for (var i = 0; i < this.fish.length; i++) { this.fish[i].bx += (Math.random() - 0.5) * 100; this.fish[i].by += (Math.random() - 0.5) * 60; }
          for (var w = 0; w < this.whales.length; w++) { this.whales[w].bx += (Math.random() - 0.5) * 140; this.whales[w].by += (Math.random() - 0.5) * 80; }
        } else if (id === "phone") { this.toast = "Purple Bear: Check SPS · find Jimmy!"; this.toastT = 3; this.scrap += 1; }
        else if (id === "sps") {
          this.toast = "SPS · Optimus kits (stub on Phaser)"; this.toastT = 2.5;
          this.kitFxKind = "rocket"; this.kitFxT = 0.55;
        }
        else if (id === "starship") { this.toast = "Spotty: Welcome aboard!"; this.toastT = 1.5; this.scene.start("space"); }
        if (hooks.onToast) hooks.onToast(this.toast);
      },
      doAbility: function () {
        if (this.cd > 0) return; this.cd = 5;
        var body = this.player.body;
        if (frogId === "james") { body.velocity.x += this.facing * 300; this.toast = this.inTruck ? "DASH · truck boost!" : "DASH!"; }
        else if (frogId === "jimmy") this.toast = "SHIELD up!";
        else if (frogId === "bubbles") { this.toast = "ZAP!"; this.scrap += 1; }
        else {
          this.toast = "BOT · open SPS for Optimus kits";
          this.player.x += (520 - this.player.x) * 0.12; this.player.y += (1940 - this.player.y) * 0.12;
          this.kitFxKind = "map"; this.kitFxT = 0.55; /* polish9 Optimus punch lite */
        }
        this.toastT = 1.8; if (hooks.onToast) hooks.onToast(this.toast);
        if (hooks.onAbilityFire) hooks.onAbilityFire(frogId, def.ability);
      },
    });

    var SpaceScene = new Phaser.Class({
      Extends: Phaser.Scene,
      initialize: function () { Phaser.Scene.call(this, { key: "space" }); },
      create: function () {
        this.cameras.main.setBackgroundColor("#030712");
        var stars = this.add.graphics();
        for (var i = 0; i < 120; i++) {
          stars.fillStyle(0xffffff, 0.35 + Math.random() * 0.65);
          stars.fillCircle(Math.random() * 1100, Math.random() * 800, Math.random() * 2.2);
        }
        this.add.text(550, 36, "Space · Moon · Spotty launched", {
          fontFamily: "Segoe UI, system-ui, sans-serif", fontSize: "18px", fontStyle: "bold",
          color: "#e0f2fe", stroke: "#000", strokeThickness: 4,
        }).setOrigin(0.5);
        this.physics.world.setBounds(0, 0, 1100, 800);
        this.player = this.physics.add.image(220, 420, "frog_" + frogId);
        if (!this.textures.exists("frog_" + frogId)) this.player.setTint(hx(def.color));
        this.player.setCollideWorldBounds(true);
        this.jimmy = this.add.circle(720, 300, 16, 0xfb923c, 1);
        this.jimmyFlame = this.add.triangle(720, 330, 0, 0, 8, 28, -8, 28, 0x38bdf8, 0.9).setVisible(false);
        this.jimmyJetT = 0;
        this.jimmyLabel = this.add.text(720, 270, "Jimmy", { fontSize: "12px", color: "#fb923c", stroke: "#000", strokeThickness: 3 }).setOrigin(0.5);
        this.add.circle(560, 380, 12, 0xb45309, 1);
        this.add.text(560, 358, "Germy", { fontSize: "11px", color: "#fbbf24", stroke: "#000", strokeThickness: 3 }).setOrigin(0.5);
        this.add.circle(600, 410, 11, 0xd6d3d1, 1);
        this.add.text(600, 388, "Daisy", { fontSize: "11px", color: "#e7e5e4", stroke: "#000", strokeThickness: 3 }).setOrigin(0.5);
        this.add.circle(140, 120, 14, 0xfdba74, 1);
        this.add.text(140, 96, "Spotty", { fontSize: "11px", color: "#fdba74", stroke: "#000", strokeThickness: 3 }).setOrigin(0.5);
        this.add.circle(90, 700, 44, 0x22c55e, 0.35).setStrokeStyle(2, 0x86efac, 0.9);
        this.add.text(90, 700, "Ranch", { fontSize: "12px", color: "#bbf7d0", stroke: "#000", strokeThickness: 3 }).setOrigin(0.5);
        this.jimmyVx = 80; this.jimmyVy = -45; this.catches = 0; this.toastT = 3; this.cd = 0;
        this.toast = "Catch Jimmy · near Moon → orbit · Escape / hard thruster to leave";
        this.planet = { id: "moon", name: "Moon", x: 820, y: 160, r: 54 };
        this.add.circle(820, 160, 54, 0xcbd5e1, 0.95).setStrokeStyle(3, 0x64748b, 1);
        this.add.text(820, 220, "☾ Moon · Earth", { fontSize: "14px", color: "#e2e8f0", fontStyle: "bold", stroke: "#000", strokeThickness: 3 }).setOrigin(0.5);
        this.destBeacon = this.add.circle(820, 160, 70, 0x7dd3fc, 0.01).setStrokeStyle(2, 0x7dd3fc, 0.45);
        this.destBeaconLabel = this.add.text(820, 90, "", { fontSize: "12px", color: "#7dd3fc", fontStyle: "bold", stroke: "#000", strokeThickness: 3 }).setOrigin(0.5);
        this.orbitCfg = (C && C.ORBIT_PHYSICS) || {};
        var softR = (this.orbitCfg.softPullRadius || 220);
        var capR = (this.orbitCfg.captureRadius || 120);
        this.pullRing = this.add.circle(820, 160, softR, 0x7dd3fc, 0.01).setStrokeStyle(1.5, 0x7dd3fc, 0.35);
        this.capRing = this.add.circle(820, 160, capR, 0x38bdf8, 0.01).setStrokeStyle(2, 0x38bdf8, 0.55);
        this.orbitRing = this.add.circle(820, 160, 78, 0xfacc15, 0.01).setStrokeStyle(3, 0xfacc15, 0.95).setVisible(false);
        this.pullLine = this.add.graphics();
        this.escapeBanner = this.add.text(550, 720, "", {
          fontFamily: "Segoe UI, system-ui, sans-serif", fontSize: "14px", fontStyle: "bold",
          color: "#fde68a", stroke: "#000", strokeThickness: 4, align: "center",
        }).setOrigin(0.5).setScrollFactor(0).setDepth(50);
        /* Alex + Fred presence (Ben cast) */
        this.add.circle(300, 520, 14, 0x64748b, 1).setStrokeStyle(2, 0x38bdf8, 0.8);
        this.add.text(300, 496, "Alex", { fontSize: "11px", color: "#e2e8f0", stroke: "#000", strokeThickness: 3 }).setOrigin(0.5);
        this.add.circle(380, 540, 14, 0x475569, 1).setStrokeStyle(2, 0x94a3b8, 0.8);
        this.add.text(380, 516, "Fred", { fontSize: "11px", color: "#e2e8f0", stroke: "#000", strokeThickness: 3 }).setOrigin(0.5);
        this.inOrbit = false; this.orbitAngle = 0; this.orbitRadius = 78; this.orbitEscapeCool = 0;
        /* polish6: distant Mars + invader mech silhouette tease */
        this.marsMark = this.add.circle(980, 620, 36, 0xb45309, 0.85).setStrokeStyle(2, 0xfed7aa, 0.8);
        this.add.text(980, 660, "Mars", { fontSize: "12px", color: "#fed7aa", stroke: "#000", strokeThickness: 3 }).setOrigin(0.5);
        this.invSil = [];
        for (var isi = 0; isi < 4; isi++) {
          var inv = this.add.rectangle(920 + isi * 28, 560 - (isi % 2) * 16, 14, 28, 0x7f1d1d, 0.55).setVisible(false);
          this.invSil.push(inv);
        }
        this.invLabel = this.add.text(980, 520, "Invader mechs · silhouette tease", {
          fontSize: "11px", color: "#fca5a5", stroke: "#000", strokeThickness: 3,
        }).setOrigin(0.5).setVisible(false);
        this.cameras.main.startFollow(this.player, true, 0.28, 0.28); /* polish3/10 less lag fight */

        /* tapsteer1: hold-to-aim on playfield (direction, not go-to) */
        var self = this;
        function aimFromPointer(pointer) {
          if (!active || !self.player) return;
          var cam = self.cameras.main;
          var sx = (self.player.x - cam.worldView.x) * cam.zoom;
          var sy = (self.player.y - cam.worldView.y) * cam.zoom;
          var dx = pointer.x - sx;
          var dy = pointer.y - sy;
          var len = Math.hypot(dx, dy);
          if (len < 14) return;
          tapSteer.x = dx / len;
          tapSteer.y = dy / len;
          tapHeld = true;
          var cx = pointer.event ? pointer.event.clientX : pointer.x;
          var cy = pointer.event ? pointer.event.clientY : pointer.y;
          showTapMarker(cx, cy, Math.atan2(dy, dx));
        }
        this.input.on("pointerdown", function (pointer) { aimFromPointer(pointer); });
        this.input.on("pointermove", function (pointer) {
          if (!tapHeld || !pointer.isDown) return;
          aimFromPointer(pointer);
        });
        this.input.on("pointerup", function () { clearTapAim(); });

        this.cameras.main.setBounds(0, 0, 1100, 800);
      },
      update: function (time, delta) {
        var dt = Math.min(0.05, delta / 1000);
        this.cd = Math.max(0, this.cd - dt); this.toastT = Math.max(0, this.toastT - dt);
        var body = this.player.body, cfg = this.orbitCfg || {};
        var capR = cfg.captureRadius || 120, softR = cfg.softPullRadius || 220;
        var alt = cfg.orbitAltitude || 78, pullA = cfg.pullAccel || 420;
        if (this.orbitEscapeCool > 0) this.orbitEscapeCool -= dt;
        if (this.pullLine) this.pullLine.clear();
        /* polish8: soft destination beacon when heading toward Moon */
        if (this.destBeacon) {
          var dB = Phaser.Math.Distance.Between(this.player.x, this.player.y, this.planet.x, this.planet.y);
          var spdB = Math.hypot(body.velocity.x, body.velocity.y);
          var heading = false;
          if (!this.inOrbit && dB < 520 && spdB > 20) {
            var hx = this.planet.x - this.player.x, hy = this.planet.y - this.player.y;
            var dot = (body.velocity.x * hx + body.velocity.y * hy) / (spdB * (dB || 1));
            heading = dot > 0.35;
          }
          if (this.inOrbit) heading = true;
          var pulse = 0.35 + 0.4 * Math.sin(time / 280);
          this.destBeacon.setVisible(heading || dB < softR);
          this.destBeacon.setStrokeStyle(2.5, 0x7dd3fc, heading ? pulse : 0.25);
          this.destBeacon.setRadius(68 + Math.sin(time / 400) * 4);
          if (this.destBeaconLabel) {
            this.destBeaconLabel.setText(heading ? "✦ heading · Moon" : "");
            this.destBeaconLabel.setAlpha(heading ? pulse : 0);
          }
        }
        var steer = mergedSteer();
        tickTapMarker(dt);
        if (this.inOrbit) {
          this.orbitRadius = Phaser.Math.Clamp(this.orbitRadius + (steer.y || 0) * 40 * dt, 55, softR * 0.85);
          this.orbitAngle += (0.85 + (steer.x || 0) * 0.35) * dt;
          this.player.x = this.planet.x + Math.cos(this.orbitAngle) * this.orbitRadius;
          this.player.y = this.planet.y + Math.sin(this.orbitAngle) * this.orbitRadius;
          body.velocity.x = 0; body.velocity.y = 0;
          if (this.orbitRing) { this.orbitRing.setVisible(true); this.orbitRing.setRadius(this.orbitRadius); }
          if (this.escapeBanner) this.escapeBanner.setText("ORBIT · Moon · ESCAPE / Esc  ·  Ability = hard thruster");
          if (this.capRing) this.capRing.setStrokeStyle(2, 0xfacc15, 0.9);
        } else {
          if (this.orbitRing) this.orbitRing.setVisible(false);
          if (this.escapeBanner) this.escapeBanner.setText("");
          if (this.capRing) this.capRing.setStrokeStyle(2, 0x38bdf8, 0.55);
          var dP = Phaser.Math.Distance.Between(this.player.x, this.player.y, this.planet.x, this.planet.y);
          if (this.orbitEscapeCool <= 0 && dP < softR && dP > 8) {
            var ang = Math.atan2(this.player.y - this.planet.y, this.player.x - this.planet.x);
            var pull = pullA * (1 - dP / softR) * dt;
            body.velocity.x -= Math.cos(ang) * pull; body.velocity.y -= Math.sin(ang) * pull;
            if (this.pullLine) {
              this.pullLine.lineStyle(2, 0x7dd3fc, 0.35 + (1 - dP / softR) * 0.5);
              this.pullLine.lineBetween(this.planet.x, this.planet.y, this.player.x, this.player.y);
            }
            if (this.pullRing) this.pullRing.setStrokeStyle(2, 0x7dd3fc, 0.55);
          } else if (this.pullRing) this.pullRing.setStrokeStyle(1.5, 0x7dd3fc, 0.25);
          if (this.orbitEscapeCool <= 0 && dP < capR) {
            this.inOrbit = true; this.orbitAngle = Math.atan2(this.player.y - this.planet.y, this.player.x - this.planet.x);
            this.orbitRadius = alt; body.velocity.x = 0; body.velocity.y = 0;
            this.toast = "Orbit locked · Moon · Escape or hard thruster to leave"; this.toastT = 3;
          }
          if (steer.x || steer.y) {
            var len = Math.hypot(steer.x, steer.y) || 1;
            body.velocity.x += (steer.x / len) * 420 * dt; body.velocity.y += (steer.y / len) * 420 * dt;
          }
          body.velocity.x *= Math.max(0, 1 - 3.5 * dt); body.velocity.y *= Math.max(0, 1 - 3.5 * dt);
          var sp = Math.hypot(body.velocity.x, body.velocity.y);
          if (sp > 210) { body.velocity.x = (body.velocity.x / sp) * 210; body.velocity.y = (body.velocity.y / sp) * 210; }
        }
        this.jimmy.x += this.jimmyVx * dt; this.jimmy.y += this.jimmyVy * dt;
        if (this.jimmy.x < 60 || this.jimmy.x > 1040) this.jimmyVx *= -1;
        if (this.jimmy.y < 60 || this.jimmy.y > 740) this.jimmyVy *= -1;
        if (this.jimmyLabel) this.jimmyLabel.setPosition(this.jimmy.x, this.jimmy.y - 22);
        var dJ = Phaser.Math.Distance.Between(this.player.x, this.player.y, this.jimmy.x, this.jimmy.y);
        var nearReturn = Phaser.Math.Distance.Between(this.player.x, this.player.y, 90, 700) < 55;
        this.near = null;
        if (dJ < 42) this.near = { id: "jimmy", tip: "Catch Jimmy!" };
        else if (nearReturn) this.near = { id: "return", tip: "Return to ranch" };
        if (wantInteract) {
          wantInteract = false;
          if (this.near && this.near.id === "jimmy") {
            this.catches++; this.jimmy.x = 100 + Math.random() * 900; this.jimmy.y = 100 + Math.random() * 600;
            this.toast = "Caught Jimmy! ×" + this.catches + " · he jetpacks away again"; this.toastT = 2.5;
          } else if (this.near && this.near.id === "return") { this.scene.start("ranch"); return; }
        }
        if (wantAbility) {
          wantAbility = false;
          if (this.cd <= 0) {
            this.cd = 4;
            if (this.inOrbit) {
              this.inOrbit = false; this.orbitEscapeCool = 1.4;
              var kick = (cfg.hardThrustImpulse || 320);
              body.velocity.x = Math.cos(this.orbitAngle) * kick * 0.55;
              body.velocity.y = Math.sin(this.orbitAngle) * kick * 0.55;
              this.toast = "Hard thruster · left Moon orbit";
            } else {
              body.velocity.x += frogId === "jimmy" ? 280 : 200;
              body.velocity.y -= frogId === "jimmy" ? 180 : 120;
              this.toast = frogId === "jimmy" ? "SHIELD up!" : (def.ability + " · hard thruster");
              /* polish7: Jimmy jetpack escape visual */
              if (frogId === "jimmy" && this.jimmy) {
                this.jimmyVx = (Math.random() > 0.5 ? 1 : -1) * 140;
                this.jimmyVy = -110;
                this.jimmyJetT = 0.9;
              }
            }
            this.toastT = 1.5;
            if (hooks.onAbilityFire) hooks.onAbilityFire(frogId, def.ability);
          }
        }
        if (this.jimmyJetT > 0) {
          this.jimmyJetT -= dt;
          if (this.jimmyFlame) {
            this.jimmyFlame.setVisible(true);
            this.jimmyFlame.setPosition(this.jimmy.x, this.jimmy.y + 18);
            this.jimmyFlame.setAlpha(Math.min(1, this.jimmyJetT * 2));
          }
        } else if (this.jimmyFlame) this.jimmyFlame.setVisible(false);
        /* polish6: show invader silhouettes when near Mars marker */
        var nearMars = Phaser.Math.Distance.Between(this.player.x, this.player.y, 980, 620) < 160;
        for (var isi2 = 0; isi2 < (this.invSil || []).length; isi2++) {
          this.invSil[isi2].setVisible(nearMars);
          if (nearMars) this.invSil[isi2].setAlpha(0.4 + 0.2 * Math.sin(time * 0.004 + isi2));
        }
        if (this.invLabel) this.invLabel.setVisible(nearMars);
        if (nearMars && this.toastT <= 0.2) {
          this.toast = "Invader mechs · distant silhouette tease";
          this.toastT = 1.6;
        }
        if (hooks.onHud) {
          hooks.onHud({
            mode: "space", label: "Space · Moon · Phaser", scrap: this.catches,
            tip: this.toastT > 0 ? this.toast : this.inOrbit ? "Orbit locked · Escape or hard thruster" : this.near ? this.near.tip + " · INTERACT" : nearMars ? "Mars · invader silhouettes" : "Chase Jimmy · Spotty / Germy / Daisy nearby",
            inOrbit: !!this.inOrbit, near: this.near, ability: def.ability, cd: this.cd,
            walk: this.inOrbit ? "🌍 Orbit" : "🚀 Space",
          });
        }
      },
    });

    game = new Phaser.Game({
      type: Phaser.AUTO, parent: "engine-host",
      width: window.innerWidth, height: window.innerHeight, backgroundColor: "#0c1a0c",
      physics: { default: "arcade", arcade: { gravity: { y: 0 }, debug: false } },
      scale: { mode: Phaser.Scale.RESIZE, autoCenter: Phaser.Scale.CENTER_BOTH },
      scene: [RanchScene, SpaceScene], audio: { noAudio: true },
    });
    active = true;
  }

  function leaveOrbitPhaser() {
    if (!game) return false;
    var sc = game.scene.getScene("space");
    if (!sc || !sc.scene.isActive() || !sc.inOrbit) return false;
    sc.inOrbit = false; sc.orbitEscapeCool = 1.4;
    var kick = ((sc.orbitCfg && sc.orbitCfg.hardThrustImpulse) || 320);
    if (sc.player && sc.player.body) {
      sc.player.body.velocity.x = Math.cos(sc.orbitAngle || 0) * kick * 0.55;
      sc.player.body.velocity.y = Math.sin(sc.orbitAngle || 0) * kick * 0.55;
    }
    sc.toast = "Escape · left Moon orbit"; sc.toastT = 2.2;
    return true;
  }

  global.FroggiesPhaser = {
    boot: boot, destroy: destroy, setSteer: setSteer,
    pulseInteract: pulseInteract, pulseAbility: pulseAbility,
    isActive: isActive, leaveOrbit: leaveOrbitPhaser,
  };
})(typeof window !== "undefined" ? window : globalThis);
