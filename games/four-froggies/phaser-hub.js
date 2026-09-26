/* Four Froggies — Phaser 3 hub (CDN). Canvas-parity ranch + thin space stub.
   Solo-first. Big map · compound · squiggle track · pond whales · 4 trucks+shared ·
   on-water/under tint · Starship → orbit Escape/hard thruster. */
(function (global) {
  "use strict";
  var C = global.FroggiesCanon;
  var game = null, active = false, hooks = {};
  var steer = { x: 0, y: 0 }, wantInteract = false, wantAbility = false;

  function destroy() {
    active = false; steer.x = steer.y = 0; wantInteract = wantAbility = false;
    if (game) { try { game.destroy(true); } catch (e) {} game = null; }
    var host = document.getElementById("engine-host");
    if (host) host.innerHTML = "";
  }
  function setSteer(x, y) { steer.x = x; steer.y = y; }
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
          var ring = this.add.circle(hs.x, hs.y, hs.r, 0xfbbf24, 0.12).setStrokeStyle(2, 0xfbbf24, 0.75);
          this.add.text(hs.x, hs.y - hs.r - 8, hs.label, {
            fontFamily: "Segoe UI, system-ui, sans-serif", fontSize: "12px", fontStyle: "bold",
            color: "#fde68a", stroke: "#000", strokeThickness: 3,
          }).setOrigin(0.5, 1);
          this.hotGfx.push({ data: hs, ring: ring });
        }
        var ss = C.STARSHIP || { x: 360, y: 320 };
        this.add.circle(ss.x, ss.y, 14, 0xfdba74, 1);
        this.add.text(ss.x, ss.y - 22, "Spotty", {
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
          companion.nameTag = this.add.text(companion.x, companion.y - 26, cdef.name + " · AI", {
            fontFamily: "Segoe UI, system-ui, sans-serif", fontSize: "11px", fontStyle: "bold",
            color: cdef.color || "#fff", stroke: "#000", strokeThickness: 3,
          }).setOrigin(0.5, 1).setDepth(21);
          this.companions.push(companion);
        }
        this.nameTag = this.add.text(0, 0, def.name, {
          fontFamily: "Segoe UI, system-ui, sans-serif", fontSize: "14px", fontStyle: "bold",
          color: "#fff", stroke: "#000", strokeThickness: 4,
        }).setOrigin(0.5, 1).setDepth(21);
        this.truckBody = this.add.rectangle(0, 0, 78, 36, 0x9ca3af, 1).setDepth(18).setVisible(false).setStrokeStyle(3, 0x111827, 1);
        this.truckAccent = this.add.rectangle(0, -2, 50, 14, hx(def.color), 0.85).setDepth(18).setVisible(false);
        this.waterClip = this.add.rectangle(0, 10, 84, 22, 0x0e7490, 0.55).setDepth(19).setVisible(false);
        this.cameras.main.startFollow(this.player, true, 0.18, 0.18); /* polish3 less lag */
        this.cameras.main.setBounds(0, 0, C.MAP_W, C.MAP_H);
        this.cameras.main.setZoom(Math.min(0.95, Math.max(0.42, window.innerWidth / 1400)));
        this.inTruck = false; this.truckMode = null; this.truckId = null;
        this.waterSub = 0; this.scrap = 0; this.toastT = 3.5; this.cd = 0; this.near = null;
        this.facing = 1; this.bob = 0; this.zLift = 0; this.zVel = 0;
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
        for (var ai = 0; ai < 28; ai++) {
          this.add.ellipse(
            yard.x + 20 + Math.random() * (yard.w - 40),
            yard.y + 40 + Math.random() * (yard.h - 60),
            10 + (ai % 4), 7 + (ai % 3),
            ai % 3 === 0 ? 0xc4a574 : ai % 3 === 1 ? 0x8b6914 : 0xd6d3d1, 0.95
          );
        }
        this.add.rectangle(gar.x + gar.w / 2, gar.y + gar.h / 2, gar.w, gar.h, 0x6b7280, 0.92)
          .setStrokeStyle(4, 0x0b1220, 1);
        this.add.rectangle(gar.x + gar.w / 2, gar.y + gar.h - 40, 140, 70, 0x111827, 1)
          .setStrokeStyle(2, 0xfbbf24, 1);
        this.add.text(gar.x + gar.w / 2, gar.y + 16, "Garage · James toys", {
          fontSize: "14px", fontStyle: "bold", color: "#fff", stroke: "#000", strokeThickness: 3,
        }).setOrigin(0.5, 0);
        for (var t = 0; t < 40; t++) {
          this.add.rectangle(gar.x + 30 + (t % 8) * 48, gar.y + 60 + Math.floor(t / 8) * 42, 14, 14, 0xfbbf24, 0.85)
            .setStrokeStyle(1, 0x78350f, 0.8);
        }
        this.add.rectangle(house.x + house.w / 2, house.y + house.h / 2, house.w, house.h, 0xd4b896, 0.95)
          .setStrokeStyle(4, 0x3e2723, 1);
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
        this.drawMech(cp.mech10 || { x: 820, y: 1680, stories: 10 }, 0xa5b4fc, 56);
        this.drawMech(cp.mech100 || { x: 980, y: 1700, stories: 100 }, 0x67e8f9, 90);
        this.drawMech(cp.mech1000 || { x: 340, y: 2420, stories: 1000 }, 0xfcd34d, 160);
      },
      drawMech: function (m, color, h) {
        this.add.rectangle(m.x, m.y - h * 0.45, Math.max(28, h * 0.28), h, color, 0.95)
          .setStrokeStyle(3, 0x0f172a, 1);
        this.add.rectangle(m.x, m.y - h * 0.78, Math.max(16, h * 0.16), h * 0.08, 0xfbbf24, 1);
        this.add.text(m.x, m.y - h - 8, m.stories + "-story mech", {
          fontSize: m.stories >= 1000 ? "13px" : "11px", fontStyle: "bold", color: "#fff",
          stroke: "#000", strokeThickness: 3,
        }).setOrigin(0.5, 1);
      },
      drawTrack: function () {
        var tg = this.add.graphics();
        var mounds = C.TRACK_MOUNDS || [];
        for (var mi = 0; mi < mounds.length; mi++) {
          var m = mounds[mi];
          tg.fillStyle(m.h >= 0 ? 0x78716c : 0x44403c, 0.85);
          tg.fillEllipse(m.x, m.y, m.r * 1.1, m.r * 0.55);
          tg.lineStyle(3, 0x1c1917, 0.7);
          tg.strokeEllipse(m.x, m.y, m.r * 1.1, m.r * 0.55);
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
      },
      drawPondLife: function () {
        var pond = C.AREAS[2]; this.fish = []; this.whales = [];
        for (var f = 0; f < 22; f++) {
          var fx = pond.x + 50 + Math.random() * (pond.w - 100);
          var fy = pond.y + 50 + Math.random() * (pond.h - 100);
          var fish = this.add.ellipse(fx, fy, 20 + Math.random() * 12, 9 + Math.random() * 5, 0xfde68a, 0.95)
            .setStrokeStyle(2, 0x92400e, 1);
          fish.phase = Math.random() * Math.PI * 2; fish.bx = fx; fish.by = fy; this.fish.push(fish);
        }
        for (var w = 0; w < 5; w++) {
          var wx = pond.x + 140 + Math.random() * (pond.w - 280);
          var wy = pond.y + 120 + Math.random() * (pond.h - 240);
          var whale = this.add.ellipse(wx, wy, 78 + Math.random() * 42, 30 + Math.random() * 14, 0x7dd3fc, 0.95)
            .setStrokeStyle(3.5, 0x0c4a6e, 1);
          whale.phase = Math.random() * Math.PI * 2; whale.bx = wx; whale.by = wy; this.whales.push(whale);
          this.add.text(wx, wy - 22, "whale", { fontSize: "10px", color: "#e0f2fe", stroke: "#000", strokeThickness: 2 }).setOrigin(0.5);
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
          g.fillStyle(0xb0b8c4, 1); g.lineStyle(3, 0x111827, 1);
          g.beginPath();
          g.moveTo(s.x - 42, s.y + 10);
          g.lineTo(s.x - 38, s.y - 6);
          g.lineTo(s.x - 8, s.y - 10);
          g.lineTo(s.x + 10, s.y - 22);
          g.lineTo(s.x + 44, s.y - 8);
          g.lineTo(s.x + 48, s.y + 8);
          g.closePath(); g.fillPath(); g.strokePath();
          g.fillStyle(accent, 0.95);
          g.fillRect(s.x - 2, s.y - 18, 28, 14);
          g.lineStyle(2, 0x0b1220, 0.9); g.strokeRect(s.x - 2, s.y - 18, 28, 14);
          g.fillStyle(0xfef08a, 1); g.fillRect(s.x + 40, s.y - 6, 8, 5);
          g.fillStyle(0x0f172a, 1);
          g.fillCircle(s.x - 22, s.y + 12, 8);
          g.fillCircle(s.x - 8, s.y + 12, 7);
          g.fillCircle(s.x + 24, s.y + 11, 8);
          var body = this.add.rectangle(s.x, s.y, 78, 34, 0x9ca3af, 0.01); // hit proxy for pulse
          var cab = this.add.rectangle(s.x + 8, s.y - 4, 36, 16, accent, 0.01);
          var label = s.id === "shared" ? "All aboard" : ("Cybertruck · " + (C.FROG_DEFS[s.id] || {}).name);
          this.add.text(s.x, s.y - 32, label, {
            fontSize: "11px", fontStyle: "bold", color: "#fde68a", stroke: "#000", strokeThickness: 3,
          }).setOrigin(0.5, 1);
          this.parkedTrucks.push({ spot: s, body: body, cab: cab, gfx: g });
        }
      },
      update: function (time, delta) {
        var dt = Math.min(0.05, delta / 1000);
        this.cd = Math.max(0, this.cd - dt); this.toastT = Math.max(0, this.toastT - dt);
        this.bob += dt * (this.inTruck ? 14 : 10);
        /* polish3: snappier locomotion */
        var maxSp = this.inTruck ? 330 : 195, accel = this.inTruck ? 720 : 560, fric = this.inTruck ? 3.8 : 6.6;
        var body = this.player.body;
        if (steer.x || steer.y) {
          var len = Math.hypot(steer.x, steer.y) || 1;
          body.velocity.x += (steer.x / len) * accel * dt;
          body.velocity.y += (steer.y / len) * accel * dt;
          if (steer.x) this.facing = steer.x > 0 ? 1 : -1;
        }
        body.velocity.x *= Math.max(0, 1 - fric * dt);
        body.velocity.y *= Math.max(0, 1 - fric * dt);
        var sp = Math.hypot(body.velocity.x, body.velocity.y);
        if (sp > maxSp) { body.velocity.x = (body.velocity.x / sp) * maxSp; body.velocity.y = (body.velocity.y / sp) * maxSp; }
        if (this.inTruck && C.rampAt) {
          var ramp = C.rampAt(this.player.x, this.player.y);
          if (ramp && sp > 40) { this.zVel = Math.max(this.zVel, 180 * (ramp.boost || 1.3)); this.scrap += 0.02; }
        }
        this.zVel -= 420 * dt; this.zLift = Math.max(0, this.zLift + this.zVel * dt);
        if (this.zLift <= 0) { this.zLift = 0; this.zVel = 0; }
        var wet = C.inPond && C.inPond(this.player.x, this.player.y);
        if (this.inTruck && wet) {
          var plunge = Math.max(0, -this.zVel) + (this.zLift > 2 ? 40 : 0);
          this.waterSub = Math.min(1.15, 0.45 + plunge / 400);
          body.velocity.x *= Math.max(0, 1 - 1.8 * dt); body.velocity.y *= Math.max(0, 1 - 1.8 * dt);
        } else this.waterSub = Math.max(0, this.waterSub - dt * 1.6);
        this.player.setFlipX(this.facing < 0).setVisible(!this.inTruck);
        this.nameTag.setPosition(this.player.x, this.player.y - 28 - this.zLift * 0.08);
        this.nameTag.setVisible(!this.inTruck || this.truckMode === "solo");
        if (this.inTruck) {
          this.truckBody.setVisible(true); this.truckAccent.setVisible(true);
          this.truckBody.setPosition(this.player.x, this.player.y - this.zLift * 0.06);
          this.truckAccent.setPosition(this.player.x + 6 * this.facing, this.player.y - 4 - this.zLift * 0.06);
          this.truckBody.setScale(this.facing < 0 ? -1 : 1, 1);
          var dive = this.waterSub > 0.7;
          this.truckBody.setFillStyle(dive ? 0x64748b : 0x9ca3af, dive ? 0.7 : 1);
          this.waterClip.setVisible(wet);
          if (wet) { this.waterClip.setPosition(this.player.x, this.player.y + 8 + this.waterSub * 6); this.waterClip.setAlpha(0.35 + this.waterSub * 0.4); }
        } else { this.truckBody.setVisible(false); this.truckAccent.setVisible(false); this.waterClip.setVisible(false); }
        for (var fi = 0; fi < this.fish.length; fi++) {
          var fish = this.fish[fi]; fish.phase += dt * 2;
          fish.x = fish.bx + Math.sin(fish.phase) * 18; fish.y = fish.by + Math.cos(fish.phase * 0.7) * 10;
        }
        for (var wi = 0; wi < this.whales.length; wi++) {
          var wh = this.whales[wi]; wh.phase += dt * 0.7;
          wh.x = wh.bx + Math.sin(wh.phase) * 40; wh.y = wh.by + Math.cos(wh.phase * 0.55) * 22;
        }
        for (var ci = 0; ci < this.companions.length; ci++) {
          var c = this.companions[ci];
          if (this.inTruck && this.truckMode === "shared") {
            var ox = (ci - 1) * 14, oy = -10 - (ci % 2) * 8;
            c.x += (this.player.x + ox - c.x) * Math.min(1, 8 * dt);
            c.y += (this.player.y + oy - this.zLift * 0.06 - c.y) * Math.min(1, 8 * dt);
          } else {
            c.timer -= dt;
            if (c.timer <= 0) {
              c.tx = Phaser.Math.Clamp(this.player.x + (Math.random() - 0.5) * 140, 80, C.MAP_W - 80);
              c.ty = Phaser.Math.Clamp(this.player.y + (Math.random() - 0.5) * 140, 80, C.MAP_H - 80);
              c.timer = 1.2 + Math.random() * 1.8;
            }
            c.x += (c.tx - c.x) * Math.min(1, 1.6 * dt); c.y += (c.ty - c.y) * Math.min(1, 1.6 * dt);
          }
          if (c.nameTag) c.nameTag.setPosition(c.x, c.y - 28).setVisible(true);
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
            tip: this.toastT > 0 ? this.toast : this.near ? ("⚡ " + this.near.tip + " · INTERACT / E") : "",
            near: this.near, ability: def.ability, cd: this.cd, walk: walk,
          });
        }
      },
      doInteract: function () {
        if (!this.near) return;
        var id = this.near.id;
        if (C.isTruckHotspot(this.near)) {
          if (this.inTruck) { this.inTruck = false; this.truckMode = null; this.truckId = null; this.toast = "Hopped out"; }
          else {
            this.inTruck = true; this.truckMode = this.near.mode || "solo"; this.truckId = id; this.scrap += 1;
            this.toast = this.truckMode === "shared"
              ? "All aboard! Four froggies · one Cybertruck · hit the jumps!"
              : "Driving Cybertruck · hit the jumps!";
          }
          this.toastT = 2.4;
        } else if (id === "fishies") {
          this.toast = "Splash! Fishies & whales scatter"; this.toastT = 2; this.scrap += 2;
          for (var i = 0; i < this.fish.length; i++) { this.fish[i].bx += (Math.random() - 0.5) * 100; this.fish[i].by += (Math.random() - 0.5) * 60; }
          for (var w = 0; w < this.whales.length; w++) { this.whales[w].bx += (Math.random() - 0.5) * 140; this.whales[w].by += (Math.random() - 0.5) * 80; }
        } else if (id === "phone") { this.toast = "Purple Bear: Check SPS · find Jimmy!"; this.toastT = 3; this.scrap += 1; }
        else if (id === "sps") { this.toast = "SPS · Solar Positioning System (stub on Phaser)"; this.toastT = 2.5; }
        else if (id === "starship") { this.toast = "Spotty: Welcome aboard!"; this.toastT = 1.5; this.scene.start("space"); }
        if (hooks.onToast) hooks.onToast(this.toast);
      },
      doAbility: function () {
        if (this.cd > 0) return; this.cd = 5;
        var body = this.player.body;
        if (frogId === "james") { body.velocity.x += this.facing * 300; this.toast = this.inTruck ? "DASH · truck boost!" : "DASH!"; }
        else if (frogId === "jimmy") this.toast = "SHIELD up!";
        else if (frogId === "bubbles") { this.toast = "ZAP!"; this.scrap += 1; }
        else { this.toast = "BOT · nudge toward SPS"; this.player.x += (520 - this.player.x) * 0.12; this.player.y += (1940 - this.player.y) * 0.12; }
        this.toastT = 1.8; if (hooks.onToast) hooks.onToast(this.toast);
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
        this.add.text(820, 160, "Moon", { fontSize: "13px", color: "#0f172a", fontStyle: "bold" }).setOrigin(0.5);
        this.inOrbit = false; this.orbitAngle = 0; this.orbitRadius = 78; this.orbitEscapeCool = 0;
        this.orbitCfg = (C && C.ORBIT_PHYSICS) || {};
        this.cameras.main.startFollow(this.player, true, 0.18, 0.18); /* polish3 less lag */
        this.cameras.main.setBounds(0, 0, 1100, 800);
      },
      update: function (time, delta) {
        var dt = Math.min(0.05, delta / 1000);
        this.cd = Math.max(0, this.cd - dt); this.toastT = Math.max(0, this.toastT - dt);
        var body = this.player.body, cfg = this.orbitCfg || {};
        var capR = cfg.captureRadius || 120, softR = cfg.softPullRadius || 220;
        var alt = cfg.orbitAltitude || 78, pullA = cfg.pullAccel || 420;
        if (this.orbitEscapeCool > 0) this.orbitEscapeCool -= dt;
        if (this.inOrbit) {
          this.orbitRadius = Phaser.Math.Clamp(this.orbitRadius + (steer.y || 0) * 40 * dt, 55, softR * 0.85);
          this.orbitAngle += (0.85 + (steer.x || 0) * 0.35) * dt;
          this.player.x = this.planet.x + Math.cos(this.orbitAngle) * this.orbitRadius;
          this.player.y = this.planet.y + Math.sin(this.orbitAngle) * this.orbitRadius;
          body.velocity.x = 0; body.velocity.y = 0;
        } else {
          var dP = Phaser.Math.Distance.Between(this.player.x, this.player.y, this.planet.x, this.planet.y);
          if (this.orbitEscapeCool <= 0 && dP < softR && dP > 8) {
            var ang = Math.atan2(this.player.y - this.planet.y, this.player.x - this.planet.x);
            var pull = pullA * (1 - dP / softR) * dt;
            body.velocity.x -= Math.cos(ang) * pull; body.velocity.y -= Math.sin(ang) * pull;
          }
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
            } else { body.velocity.x += 200; body.velocity.y -= 120; this.toast = def.ability + " · hard thruster"; }
            this.toastT = 1.5;
          }
        }
        if (hooks.onHud) {
          hooks.onHud({
            mode: "space", label: "Space · Moon · Phaser", scrap: this.catches,
            tip: this.toastT > 0 ? this.toast : this.inOrbit ? "Orbit locked · Escape or hard thruster" : this.near ? this.near.tip + " · INTERACT" : "Chase Jimmy · Spotty / Germy / Daisy nearby",
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
