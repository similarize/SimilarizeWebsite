/* Four Froggies — Phaser 3 hub path (CDN). Same Ben cast/areas; physics/look compare.
   Solo-first (no PeerJS). Ranch hub + thin space stub (Spotty → Jimmy chase). */
(function (global) {
  "use strict";

  var C = global.FroggiesCanon;
  var game = null;
  var active = false;
  var hooks = {};
  var steer = { x: 0, y: 0 };
  var wantInteract = false;
  var wantAbility = false;

  function destroy() {
    active = false;
    steer.x = steer.y = 0;
    wantInteract = wantAbility = false;
    if (game) {
      try { game.destroy(true); } catch (e) { /* ignore */ }
      game = null;
    }
    var host = document.getElementById("engine-host");
    if (host) host.innerHTML = "";
  }

  function setSteer(x, y) {
    steer.x = x;
    steer.y = y;
  }

  function pulseInteract() { wantInteract = true; }
  function pulseAbility() { wantAbility = true; }

  function isActive() { return active; }

  function boot(opts) {
    destroy();
    hooks = opts || {};
    var host = document.getElementById("engine-host");
    if (!host) {
      console.error("engine-host missing");
      return;
    }
    if (typeof Phaser === "undefined") {
      console.error("Phaser CDN not loaded");
      if (hooks.onToast) hooks.onToast("Phaser failed to load");
      return;
    }
    host.hidden = false;
    host.innerHTML = "";
    var view = document.getElementById("view");
    if (view) view.style.display = "none";

    var frogId = (opts && opts.frogId) || "james";
    var def = C.FROG_DEFS[frogId] || C.FROG_DEFS.james;

    var RanchScene = new Phaser.Class({
      Extends: Phaser.Scene,
      initialize: function RanchScene() {
        Phaser.Scene.call(this, { key: "ranch" });
      },
      create: function () {
        this.cameras.main.setBackgroundColor("#1a3a1a");
        this.physics.world.setBounds(0, 0, C.MAP_W, C.MAP_H);

        // Ground feel
        var g = this.add.graphics();
        g.fillStyle(0x2d5a27, 1);
        g.fillRect(0, 0, C.MAP_W, C.MAP_H);
        // Soft tile lines
        g.lineStyle(1, 0x3f6b38, 0.35);
        for (var tx = 0; tx < C.MAP_W; tx += 40) g.lineBetween(tx, 0, tx, C.MAP_H);
        for (var ty = 0; ty < C.MAP_H; ty += 40) g.lineBetween(0, ty, C.MAP_W, ty);

        for (var i = 0; i < C.AREAS.length; i++) {
          var a = C.AREAS[i];
          var col = Phaser.Display.Color.HexStringToColor(a.color).color;
          var zone = this.add.rectangle(a.x + a.w / 2, a.y + a.h / 2, a.w, a.h, col, 0.85);
          zone.setStrokeStyle(3, 0xffffff, 0.35);
          this.add.text(a.x + 12, a.y + 10, a.name, {
            fontFamily: "Segoe UI, system-ui, sans-serif",
            fontSize: "16px",
            fontStyle: "bold",
            color: "#fff",
            stroke: "#000",
            strokeThickness: 3,
          });
        }

        // Toys prop density (anonymous shapes — no invented names)
        for (var t = 0; t < 10; t++) {
          var px = 120 + (t % 5) * 55;
          var py = 450 + Math.floor(t / 5) * 50;
          this.add.circle(px, py, 8 + (t % 3) * 3, 0xfbbf24, 0.7);
        }

        // Fishies in pond
        this.fish = [];
        for (var f = 0; f < 8; f++) {
          var fish = this.add.ellipse(580 + f * 50, 120 + (f % 3) * 40, 18, 10, 0x67e8f9, 0.9);
          fish.phase = Math.random() * Math.PI * 2;
          this.fish.push(fish);
        }

        this.hotGfx = [];
        for (var h = 0; h < C.HOTSPOTS.length; h++) {
          var hs = C.HOTSPOTS[h];
          var ring = this.add.circle(hs.x, hs.y, hs.r, 0xfbbf24, 0.12);
          ring.setStrokeStyle(2, 0xfbbf24, 0.7);
          this.add.text(hs.x, hs.y - hs.r - 8, hs.label, {
            fontFamily: "Segoe UI, system-ui, sans-serif",
            fontSize: "12px",
            fontStyle: "bold",
            color: "#fde68a",
            stroke: "#000",
            strokeThickness: 3,
          }).setOrigin(0.5, 1);
          this.hotGfx.push({ data: hs, ring: ring });
        }

        // Spotty marker at starship (cat commander — labeled)
        this.add.circle(140, 110, 14, 0xfdba74, 1);
        this.add.text(140, 88, "Spotty", {
          fontFamily: "Segoe UI, system-ui, sans-serif",
          fontSize: "11px",
          color: "#fdba74",
          stroke: "#000",
          strokeThickness: 3,
        }).setOrigin(0.5);

        // Player froggy (procedural texture first)
        var key = "frog_" + frogId;
        if (!this.textures.exists(key)) {
          var rt = this.make.graphics({ x: 0, y: 0, add: false });
          var body = Phaser.Display.Color.HexStringToColor(def.color).color;
          var accent = Phaser.Display.Color.HexStringToColor(def.accent).color;
          var hat = Phaser.Display.Color.HexStringToColor(def.hat).color;
          rt.fillStyle(body, 1);
          rt.fillCircle(20, 22, 16);
          rt.fillStyle(accent, 1);
          rt.fillCircle(14, 18, 4);
          rt.fillCircle(26, 18, 4);
          rt.fillStyle(hat, 1);
          rt.fillTriangle(20, 4, 10, 16, 30, 16);
          rt.generateTexture(key, 40, 40);
          rt.destroy();
        }
        this.player = this.physics.add.image(220, 420, key);
        this.player.setCollideWorldBounds(true);
        this.player.setDepth(10);
        this.player.body.setSize(28, 28);

        // AI companions (visual wander)
        this.companions = [];
        for (var ci = 0; ci < C.FROG_ORDER.length; ci++) {
          var cid = C.FROG_ORDER[ci];
          if (cid === frogId) continue;
          var cdef = C.FROG_DEFS[cid];
          var ckey = "frog_" + cid;
          if (!this.textures.exists(ckey)) {
            var cg = this.make.graphics({ x: 0, y: 0, add: false });
            var cb = Phaser.Display.Color.HexStringToColor(cdef.color).color;
            var ca = Phaser.Display.Color.HexStringToColor(cdef.accent).color;
            var ch = Phaser.Display.Color.HexStringToColor(cdef.hat).color;
            cg.fillStyle(cb, 1);
            cg.fillCircle(16, 18, 13);
            cg.fillStyle(ca, 1);
            cg.fillCircle(11, 15, 3);
            cg.fillCircle(21, 15, 3);
            cg.fillStyle(ch, 1);
            cg.fillTriangle(16, 2, 8, 14, 24, 14);
            cg.generateTexture(ckey, 32, 32);
            cg.destroy();
          }
          var companion = this.add.image(240 + ci * 40, 440 + (ci % 2) * 20, ckey);
          companion.setAlpha(0.85);
          companion.tx = companion.x;
          companion.ty = companion.y;
          companion.timer = 1 + Math.random() * 2;
          this.companions.push(companion);
        }

        this.nameTag = this.add.text(0, 0, def.name, {
          fontFamily: "Segoe UI, system-ui, sans-serif",
          fontSize: "12px",
          fontStyle: "bold",
          color: "#fff",
          stroke: "#000",
          strokeThickness: 3,
        }).setOrigin(0.5, 1).setDepth(11);

        this.cameras.main.startFollow(this.player, true, 0.12, 0.12);
        this.cameras.main.setZoom(Math.min(1.15, Math.max(0.7, window.innerWidth / 700)));

        this.inTruck = false;
        this.scrap = 0;
        this.toast = "Phaser ranch · drive · pond · Starship (Spotty)";
        this.toastT = 3.5;
        this.cd = 0;
        this.near = null;
        this.facing = 1;
        this.bob = 0;

        if (hooks.onReady) hooks.onReady({ engine: "phaser", frogId: frogId });
        if (hooks.onToast) hooks.onToast(this.toast);
      },
      update: function (time, delta) {
        var dt = Math.min(0.05, delta / 1000);
        this.cd = Math.max(0, this.cd - dt);
        this.toastT = Math.max(0, this.toastT - dt);
        this.bob += dt * (this.inTruck ? 14 : 10);

        // Accel / friction feel (compare vs Canvas)
        var maxSp = this.inTruck ? 260 : 160;
        var accel = this.inTruck ? 520 : 380;
        var fric = this.inTruck ? 3.2 : 5.5;
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
        if (sp > maxSp) {
          body.velocity.x = (body.velocity.x / sp) * maxSp;
          body.velocity.y = (body.velocity.y / sp) * maxSp;
        }
        this.player.setFlipX(this.facing < 0);
        this.player.y += Math.sin(this.bob) * (sp > 20 ? 0.35 : 0);

        this.nameTag.setPosition(this.player.x, this.player.y - 28);

        // Fish swim
        for (var fi = 0; fi < this.fish.length; fi++) {
          var fish = this.fish[fi];
          fish.phase += dt * 2;
          fish.x += Math.sin(fish.phase) * 12 * dt;
          fish.y += Math.cos(fish.phase * 0.7) * 8 * dt;
        }

        // AI wander
        for (var ci = 0; ci < this.companions.length; ci++) {
          var c = this.companions[ci];
          c.timer -= dt;
          if (c.timer <= 0) {
            c.tx = Phaser.Math.Clamp(c.x + (Math.random() - 0.5) * 120, 80, C.MAP_W - 80);
            c.ty = Phaser.Math.Clamp(c.y + (Math.random() - 0.5) * 120, 80, C.MAP_H - 80);
            c.timer = 1.5 + Math.random() * 2.5;
          }
          c.x += (c.tx - c.x) * Math.min(1, 1.4 * dt);
          c.y += (c.ty - c.y) * Math.min(1, 1.4 * dt);
        }

        this.near = C.nearestHotspot(this.player.x, this.player.y, 70);
        for (var hi = 0; hi < this.hotGfx.length; hi++) {
          var hg = this.hotGfx[hi];
          hg.ring.setAlpha(this.near && this.near.id === hg.data.id ? 0.35 : 0.12);
        }

        if (wantInteract) {
          wantInteract = false;
          this.doInteract();
        }
        if (wantAbility) {
          wantAbility = false;
          this.doAbility();
        }

        if (hooks.onHud) {
          hooks.onHud({
            mode: "ranch",
            label: C.areaNameAt(this.player.x, this.player.y) + " · Phaser",
            scrap: this.scrap,
            tip: this.toastT > 0 ? this.toast : this.near ? this.near.tip + " · INTERACT" : "",
            near: this.near,
            ability: def.ability,
            cd: this.cd,
            walk: this.inTruck ? "🚚 Drive" : "🐸 Walk",
          });
        }
      },
      doInteract: function () {
        if (!this.near) return;
        var id = this.near.id;
        if (id === "truck") {
          this.inTruck = !this.inTruck;
          this.toast = this.inTruck ? "Cybertruck · hit the jumps!" : "Hopped out";
          this.toastT = 2;
          this.scrap += this.inTruck ? 1 : 0;
        } else if (id === "fishies") {
          this.toast = "Splash! Fishies scatter";
          this.toastT = 2;
          this.scrap += 2;
          for (var i = 0; i < this.fish.length; i++) {
            this.fish[i].x += (Math.random() - 0.5) * 80;
            this.fish[i].y += (Math.random() - 0.5) * 40;
          }
        } else if (id === "phone") {
          this.toast = "Purple Bear: Check SPS · find Jimmy!";
          this.toastT = 3;
          this.scrap += 1;
        } else if (id === "sps") {
          this.toast = "SPS · Solar Positioning System (stub on Phaser)";
          this.toastT = 2.5;
        } else if (id === "starship") {
          this.toast = "Spotty: Welcome aboard!";
          this.toastT = 1.5;
          this.scene.start("space");
        }
        if (hooks.onToast) hooks.onToast(this.toast);
      },
      doAbility: function () {
        if (this.cd > 0) return;
        this.cd = 5;
        var body = this.player.body;
        if (frogId === "james") {
          body.velocity.x += this.facing * 280;
          this.toast = this.inTruck ? "DASH · truck boost!" : "DASH!";
        } else if (frogId === "jimmy") {
          this.toast = "SHIELD up!";
        } else if (frogId === "bubbles") {
          this.toast = "ZAP!";
          this.scrap += 1;
        } else {
          this.toast = "BOT · nudge toward SPS";
          this.player.x += (280 - this.player.x) * 0.15;
          this.player.y += (400 - this.player.y) * 0.15;
        }
        this.toastT = 1.8;
        if (hooks.onToast) hooks.onToast(this.toast);
      },
    });

    var SpaceScene = new Phaser.Class({
      Extends: Phaser.Scene,
      initialize: function SpaceScene() {
        Phaser.Scene.call(this, { key: "space" });
      },
      create: function () {
        this.cameras.main.setBackgroundColor("#030712");
        var stars = this.add.graphics();
        for (var i = 0; i < 80; i++) {
          stars.fillStyle(0xffffff, 0.4 + Math.random() * 0.6);
          stars.fillCircle(Math.random() * 900, Math.random() * 700, Math.random() * 2);
        }
        this.add.text(450, 40, "Space · Moon · Spotty launched", {
          fontFamily: "Segoe UI, system-ui, sans-serif",
          fontSize: "18px",
          fontStyle: "bold",
          color: "#e0f2fe",
          stroke: "#000",
          strokeThickness: 4,
        }).setOrigin(0.5);

        this.player = this.physics.add.image(200, 400, "frog_" + frogId);
        if (!this.textures.exists("frog_" + frogId)) {
          // fallback circle
          this.player.setTint(Phaser.Display.Color.HexStringToColor(def.color).color);
        }
        this.player.setCollideWorldBounds(true);
        this.physics.world.setBounds(0, 0, 900, 700);

        this.jimmy = this.add.circle(620, 280, 16, 0xfb923c, 1);
        this.add.text(620, 250, "Jimmy", {
          fontSize: "12px", color: "#fb923c", stroke: "#000", strokeThickness: 3,
        }).setOrigin(0.5).setName("jimmyLabel");
        this.jimmyLabel = this.children.getByName("jimmyLabel");

        this.germy = this.add.circle(520, 360, 12, 0xb45309, 1);
        this.add.text(520, 338, "Germy", { fontSize: "11px", color: "#fbbf24", stroke: "#000", strokeThickness: 3 }).setOrigin(0.5);
        this.daisy = this.add.circle(560, 390, 11, 0xd6d3d1, 1);
        this.add.text(560, 368, "Daisy", { fontSize: "11px", color: "#e7e5e4", stroke: "#000", strokeThickness: 3 }).setOrigin(0.5);
        this.spotty = this.add.circle(120, 120, 14, 0xfdba74, 1);
        this.add.text(120, 96, "Spotty", { fontSize: "11px", color: "#fdba74", stroke: "#000", strokeThickness: 3 }).setOrigin(0.5);

        this.returnPad = this.add.circle(80, 620, 40, 0x22c55e, 0.35);
        this.returnPad.setStrokeStyle(2, 0x86efac, 0.9);
        this.add.text(80, 620, "Ranch", { fontSize: "12px", color: "#bbf7d0", stroke: "#000", strokeThickness: 3 }).setOrigin(0.5);

        this.jimmyVx = 70;
        this.jimmyVy = -40;
        this.catches = 0;
        this.toast = "Catch Jimmy · INTERACT near return pad for ranch";
        this.toastT = 3;
        this.cd = 0;
        this.cameras.main.startFollow(this.player, true, 0.1, 0.1);
      },
      update: function (time, delta) {
        var dt = Math.min(0.05, delta / 1000);
        this.cd = Math.max(0, this.cd - dt);
        this.toastT = Math.max(0, this.toastT - dt);
        var body = this.player.body;
        var maxSp = 200;
        if (steer.x || steer.y) {
          var len = Math.hypot(steer.x, steer.y) || 1;
          body.velocity.x += (steer.x / len) * 400 * dt;
          body.velocity.y += (steer.y / len) * 400 * dt;
        }
        body.velocity.x *= Math.max(0, 1 - 3.5 * dt);
        body.velocity.y *= Math.max(0, 1 - 3.5 * dt);
        var sp = Math.hypot(body.velocity.x, body.velocity.y);
        if (sp > maxSp) {
          body.velocity.x = (body.velocity.x / sp) * maxSp;
          body.velocity.y = (body.velocity.y / sp) * maxSp;
        }

        this.jimmy.x += this.jimmyVx * dt;
        this.jimmy.y += this.jimmyVy * dt;
        if (this.jimmy.x < 60 || this.jimmy.x > 840) this.jimmyVx *= -1;
        if (this.jimmy.y < 60 || this.jimmy.y > 640) this.jimmyVy *= -1;
        if (this.jimmyLabel) this.jimmyLabel.setPosition(this.jimmy.x, this.jimmy.y - 22);

        var dJ = Phaser.Math.Distance.Between(this.player.x, this.player.y, this.jimmy.x, this.jimmy.y);
        var nearReturn = Phaser.Math.Distance.Between(this.player.x, this.player.y, 80, 620) < 55;
        this.near = null;
        if (dJ < 42) this.near = { id: "jimmy", tip: "Catch Jimmy!" };
        else if (nearReturn) this.near = { id: "return", tip: "Return to ranch" };

        if (wantInteract) {
          wantInteract = false;
          if (this.near && this.near.id === "jimmy") {
            this.catches++;
            this.jimmy.x = 100 + Math.random() * 700;
            this.jimmy.y = 100 + Math.random() * 500;
            this.toast = "Caught Jimmy! ×" + this.catches + " · he jetpacks away again";
            this.toastT = 2.5;
          } else if (this.near && this.near.id === "return") {
            this.scene.start("ranch");
            return;
          }
        }
        if (wantAbility) {
          wantAbility = false;
          if (this.cd <= 0) {
            this.cd = 4;
            body.velocity.x += 200;
            body.velocity.y -= 120;
            this.toast = def.ability + " · jet boost";
            this.toastT = 1.5;
          }
        }

        if (hooks.onHud) {
          hooks.onHud({
            mode: "space",
            label: "Space · Moon · Phaser",
            scrap: this.catches,
            tip: this.toastT > 0 ? this.toast : this.near ? this.near.tip + " · INTERACT" : "Chase Jimmy · Spotty / Germy / Daisy nearby",
            near: this.near,
            ability: def.ability,
            cd: this.cd,
            walk: "🚀 Space",
          });
        }
      },
    });

    game = new Phaser.Game({
      type: Phaser.AUTO,
      parent: "engine-host",
      width: window.innerWidth,
      height: window.innerHeight,
      backgroundColor: "#0c1a0c",
      physics: {
        default: "arcade",
        arcade: { gravity: { y: 0 }, debug: false },
      },
      scale: {
        mode: Phaser.Scale.RESIZE,
        autoCenter: Phaser.Scale.CENTER_BOTH,
      },
      scene: [RanchScene, SpaceScene],
      audio: { noAudio: true },
    });
    active = true;
  }

  global.FroggiesPhaser = {
    boot: boot,
    destroy: destroy,
    setSteer: setSteer,
    pulseInteract: pulseInteract,
    pulseAbility: pulseAbility,
    isActive: isActive,
  };
})(typeof window !== "undefined" ? window : globalThis);
