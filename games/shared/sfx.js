/* Similarize Arcade — shared procedural audio (games/shared/sfx.js)
 * No audio files: music loops + SFX are synthesized with WebAudio.
 *
 *   ArcadeAudio.init({ mood: "rally", button: "bottom-left" });
 *   ArcadeAudio.play("jump");            // see SFX list below
 *   ArcadeAudio.engine(throttle, speed)  // 0..1 each, call every frame; engine(0,0) idles
 *   ArcadeAudio.engineStop();
 *   ArcadeAudio.setMood("space");  ArcadeAudio.music(false);
 *   ArcadeAudio.duck(0.3, 1.2);  ArcadeAudio.toggleMute();
 *
 * Starts on the first user gesture (pointer / touch / key / gamepad button).
 * M key or the small on-screen button mutes; saved in localStorage("arcadeAudioMuted").
 */
(function () {
  "use strict";
  if (window.ArcadeAudio) return;

  var KEY = "arcadeAudioMuted";
  var ctx = null, master = null, musicBus = null, sfxBus = null, comp = null;
  var muted = false, unlocked = false, inited = false, btn = null;
  var cfg = { mood: "party", music: true, button: "bottom-left", muteKey: true, musicVol: 0.16, sfxVol: 0.55 };
  var noiseBuf = null, lastPlay = {}, counts = { played: 0, music: 0 };
  try { muted = localStorage.getItem(KEY) === "1"; } catch (e) { /* private mode */ }

  function mtof(m) { return 440 * Math.pow(2, (m - 69) / 12); }

  function ensureCtx() {
    if (ctx) return ctx;
    var AC = window.AudioContext || window.webkitAudioContext;
    if (!AC) return null;
    try { ctx = new AC(); } catch (e) { return null; }
    comp = ctx.createDynamicsCompressor();
    comp.threshold.value = -14; comp.knee.value = 10; comp.ratio.value = 4;
    comp.attack.value = 0.004; comp.release.value = 0.2;
    master = ctx.createGain(); master.gain.value = muted ? 0 : 1;
    musicBus = ctx.createGain(); musicBus.gain.value = cfg.musicVol;
    sfxBus = ctx.createGain(); sfxBus.gain.value = cfg.sfxVol;
    musicBus.connect(master); sfxBus.connect(master); master.connect(comp); comp.connect(ctx.destination);
    var len = Math.floor(ctx.sampleRate * 1.0);
    noiseBuf = ctx.createBuffer(1, len, ctx.sampleRate);
    var d = noiseBuf.getChannelData(0);
    for (var i = 0; i < len; i++) d[i] = Math.random() * 2 - 1;
    return ctx;
  }

  /* ---------- primitive voices ---------- */
  function env(g, t, a, peak, dur) {
    g.gain.setValueAtTime(0.0001, t);
    g.gain.linearRampToValueAtTime(peak, t + a);
    g.gain.exponentialRampToValueAtTime(0.0001, t + Math.max(a + 0.01, dur));
  }
  function tone(bus, f, dur, type, vol, opt) {
    opt = opt || {};
    var t = (opt.t != null ? opt.t : ctx.currentTime) + (opt.delay || 0);
    var o = ctx.createOscillator(), g = ctx.createGain();
    o.type = type || "square";
    o.frequency.setValueAtTime(f, t);
    if (opt.to) o.frequency.exponentialRampToValueAtTime(Math.max(20, opt.to), t + dur);
    if (opt.detune) o.detune.value = opt.detune;
    env(g, t, opt.a || 0.005, vol, dur);
    var out = g;
    if (opt.lp) { var f2 = ctx.createBiquadFilter(); f2.type = "lowpass"; f2.frequency.value = opt.lp; g.connect(f2); out = f2; }
    o.connect(g); out.connect(bus);
    o.start(t); o.stop(t + dur + 0.05);
  }
  function noise(bus, dur, vol, type, f, fTo, opt) {
    opt = opt || {};
    var t = (opt.t != null ? opt.t : ctx.currentTime) + (opt.delay || 0);
    var s = ctx.createBufferSource(); s.buffer = noiseBuf; s.loop = true;
    var bq = ctx.createBiquadFilter(); bq.type = type || "lowpass"; bq.frequency.setValueAtTime(f || 1200, t);
    if (opt.q) bq.Q.value = opt.q;
    if (fTo) bq.frequency.exponentialRampToValueAtTime(Math.max(30, fTo), t + dur);
    var g = ctx.createGain(); env(g, t, opt.a || 0.004, vol, dur);
    s.connect(bq); bq.connect(g); g.connect(bus);
    s.start(t, Math.random() * 0.5); s.stop(t + dur + 0.05);
  }

  /* ---------- SFX bank ---------- */
  var B = function () { return sfxBus; };
  var SFX = {
    click: function () { tone(B(), 1250, 0.05, "square", 0.12); },
    jump: function () { tone(B(), 320, 0.18, "square", 0.16, { to: 760, lp: 2600 }); },
    land: function (s) { tone(B(), 150, 0.14, "triangle", 0.3 * s, { to: 60 }); noise(B(), 0.1, 0.18 * s, "lowpass", 700, 200); },
    bump: function (s) { tone(B(), 120, 0.12, "triangle", 0.3 * s, { to: 70 }); noise(B(), 0.08, 0.2 * s, "lowpass", 1400, 300); },
    crash: function (s) {
      noise(B(), 0.5, 0.5 * s, "lowpass", 2400, 160); tone(B(), 95, 0.4, "sawtooth", 0.2 * s, { to: 38, lp: 900 });
      noise(B(), 0.18, 0.18 * s, "highpass", 3000, 1500, { delay: 0.05 }); duck(0.45, 0.6);
    },
    boost: function () { tone(B(), 180, 0.45, "sawtooth", 0.14, { to: 900, lp: 2200 }); noise(B(), 0.45, 0.16, "bandpass", 600, 3500, { q: 1.5 }); },
    whoosh: function (s) { noise(B(), 0.35, 0.2 * s, "bandpass", 400, 2400, { q: 2, a: 0.08 }); },
    pickup: function () { tone(B(), 988, 0.08, "square", 0.12); tone(B(), 1319, 0.2, "square", 0.12, { delay: 0.07 }); },
    coin: function () { SFX.pickup(); },
    pop: function (s) { noise(B(), 0.09, 0.42 * s, "bandpass", 2000, 700, { q: 0.8 }); tone(B(), 520, 0.09, "sine", 0.22 * s, { to: 1400 }); },
    shot: function () { noise(B(), 0.07, 0.3, "highpass", 2500, 900); tone(B(), 950, 0.07, "square", 0.08, { to: 220 }); },
    laser: function () { tone(B(), 1500, 0.14, "square", 0.1, { to: 260, lp: 3000 }); },
    explode: function (s) { noise(B(), 0.9, 0.5 * s, "lowpass", 1800, 90); tone(B(), 70, 0.6, "sine", 0.3 * s, { to: 30 }); duck(0.5, 0.8); },
    splash: function (s) {
      noise(B(), 0.4, 0.35 * s, "bandpass", 1600, 300, { q: 0.7 });
      tone(B(), 600, 0.08, "sine", 0.1 * s, { to: 1200, delay: 0.1 }); tone(B(), 800, 0.07, "sine", 0.08 * s, { to: 1500, delay: 0.2 });
    },
    bounce: function (s) { tone(B(), 220, 0.13, "sine", 0.3 * s, { to: 380 }); },
    hit: function (s) { tone(B(), 660, 0.08, "triangle", 0.25 * s, { to: 330 }); noise(B(), 0.05, 0.15 * s, "highpass", 2000); },
    kick: function (s) { tone(B(), 160, 0.12, "sine", 0.4 * s, { to: 55 }); noise(B(), 0.04, 0.2 * s, "bandpass", 1800); },
    horn: function () { tone(B(), 392, 0.35, "square", 0.08, { lp: 1600 }); tone(B(), 494, 0.35, "square", 0.08, { lp: 1600 }); },
    countdown: function () { tone(B(), 660, 0.16, "square", 0.14, { lp: 3000 }); },
    go: function () { tone(B(), 990, 0.4, "square", 0.16, { lp: 3500 }); tone(B(), 1320, 0.4, "square", 0.08, { lp: 3500 }); },
    lap: function () { [784, 988, 1175].forEach(function (f, i) { tone(B(), f, 0.12, "square", 0.12, { delay: i * 0.08, lp: 3200 }); }); },
    checkpoint: function () { tone(B(), 880, 0.1, "triangle", 0.18); tone(B(), 1320, 0.16, "triangle", 0.15, { delay: 0.08 }); },
    win: function () {
      duck(0.15, 2.2);
      [523, 659, 784, 1047].forEach(function (f, i) { tone(B(), f, 0.16, "square", 0.13, { delay: i * 0.11, lp: 3200 }); });
      [523, 659, 784].forEach(function (f) { tone(B(), f, 0.9, "triangle", 0.12, { delay: 0.48 }); });
      tone(B(), 1047, 0.9, "square", 0.07, { delay: 0.48, lp: 2600 });
    },
    lose: function () {
      duck(0.15, 2);
      [392, 370, 349, 311].forEach(function (f, i) { tone(B(), f, i === 3 ? 0.7 : 0.26, "sawtooth", 0.1, { delay: i * 0.28, lp: 1400 }); });
    },
    goal: function () { SFX.win(); noise(B(), 1.4, 0.12, "bandpass", 1200, 2200, { a: 0.25, q: 0.4 }); },
    powerup: function () { [523, 659, 784, 1047, 1319].forEach(function (f, i) { tone(B(), f, 0.08, "square", 0.1, { delay: i * 0.045 }); }); },
    hurt: function () { tone(B(), 300, 0.25, "sawtooth", 0.14, { to: 90, lp: 1600 }); },
    reload: function () { noise(B(), 0.05, 0.2, "bandpass", 3000); noise(B(), 0.06, 0.2, "bandpass", 1800, null, { delay: 0.12 }); },
    thud: function (s) { tone(B(), 110, 0.1, "sine", 0.35 * s, { to: 50 }); noise(B(), 0.06, 0.12 * s, "lowpass", 900); }
  };

  function play(name, strength) {
    counts.played++; counts.last = name;
    if (!ctx || !unlocked || muted) return false;
    var fn = SFX[name]; if (!fn) return false;
    var now = ctx.currentTime;
    if (lastPlay[name] && now - lastPlay[name] < 0.035) return false; // de-spam
    lastPlay[name] = now;
    var s = strength == null ? 1 : Math.max(0.1, Math.min(1.5, strength));
    try { fn(s); } catch (e) { /* ignore */ }
    return true;
  }

  function duck(level, secs) {
    if (!ctx || !musicBus) return;
    var t = ctx.currentTime, v = cfg.musicVol;
    musicBus.gain.cancelScheduledValues(t);
    musicBus.gain.setTargetAtTime(v * (level == null ? 0.35 : level), t, 0.04);
    musicBus.gain.setTargetAtTime(v, t + (secs || 1), 0.35);
  }

  /* ---------- engine hum ---------- */
  var eng = null;
  var ENG = {
    rc: { base: 95, range: 420, lp: 1800, vol: 0.09 },
    buggy: { base: 60, range: 230, lp: 1100, vol: 0.12 },
    truck: { base: 42, range: 140, lp: 800, vol: 0.13 },
    jet: { base: 120, range: 260, lp: 2400, vol: 0.06 }
  };
  function engine(throttle, speed, kind) {
    if (!ctx || !unlocked) return;
    var p = ENG[kind || cfg.engine || "rc"] || ENG.rc;
    if (!eng || eng.kind !== (kind || cfg.engine || "rc")) {
      engineStop(true);
      var o1 = ctx.createOscillator(), o2 = ctx.createOscillator(), lp = ctx.createBiquadFilter(), g = ctx.createGain();
      var lfo = ctx.createOscillator(), lg = ctx.createGain();
      o1.type = "sawtooth"; o2.type = "square"; o2.detune.value = -1200 + 7;
      lp.type = "lowpass"; lp.frequency.value = p.lp; lp.Q.value = 3;
      lfo.frequency.value = 18; lg.gain.value = 6; lfo.connect(lg); lg.connect(o1.frequency); lg.connect(o2.frequency);
      g.gain.value = 0.0001;
      o1.connect(lp); o2.connect(lp); lp.connect(g); g.connect(sfxBus);
      o1.start(); o2.start(); lfo.start();
      eng = { o1: o1, o2: o2, lp: lp, g: g, lfo: lfo, kind: kind || cfg.engine || "rc" };
    }
    var th = Math.max(0, Math.min(1, Math.abs(throttle || 0))), sp = Math.max(0, Math.min(1, Math.abs(speed || 0)));
    var t = ctx.currentTime, f = p.base + p.range * (0.75 * sp + 0.25 * th);
    eng.o1.frequency.setTargetAtTime(f, t, 0.08);
    eng.o2.frequency.setTargetAtTime(f, t, 0.08);
    eng.lfo.frequency.setTargetAtTime(14 + 30 * sp, t, 0.1);
    eng.lp.frequency.setTargetAtTime(p.lp * (0.6 + 0.8 * th), t, 0.1);
    eng.g.gain.setTargetAtTime(p.vol * (0.35 + 0.45 * th + 0.2 * sp), t, 0.06);
  }
  function engineStop(now) {
    if (!eng) return;
    var e = eng; eng = null;
    try {
      e.g.gain.setTargetAtTime(0.0001, ctx.currentTime, now ? 0.01 : 0.15);
      var st = ctx.currentTime + (now ? 0.1 : 0.8);
      e.o1.stop(st); e.o2.stop(st); e.lfo.stop(st);
    } catch (x) { /* ignore */ }
  }

  /* ---------- music: step sequencer ---------- */
  // chords: [rootOffset, quality(0 maj,1 min)]; drums: 16-step strings (x hit, . rest)
  var MOODS = {
    rally: { bpm: 138, root: 45, chords: [[0, 0], [10, 0], [5, 0], [0, 0]], lead: "square", bass: "sawtooth",
      k: "x...x...x...x...", s: "....x.......x...", h: "x.x.x.x.x.x.x.xx", mel: [0, 2, 1, 2, 3, 2, 1, -1, 0, 2, 1, 2, 4, 3, 2, 1] },
    space: { bpm: 120, root: 43, chords: [[0, 1], [8, 0], [3, 0], [10, 0]], lead: "triangle", bass: "square",
      k: "x.....x...x.....", s: "....x.......x...", h: "..x...x...x...x.", mel: [0, -1, 2, 1, 3, -1, 2, -1, 4, 3, 2, -1, 1, -1, 0, -1] },
    party: { bpm: 116, root: 48, chords: [[0, 0], [9, 1], [5, 0], [7, 0]], lead: "square", bass: "triangle",
      k: "x.......x.......", s: "....x.......x...", h: "x.x.x.x.x.x.x.x.", mel: [0, -1, 1, 2, -1, 2, 3, -1, 2, 1, -1, 0, 1, -1, 2, -1] },
    sport: { bpm: 128, root: 47, chords: [[0, 0], [5, 0], [7, 0], [5, 0]], lead: "square", bass: "sawtooth",
      k: "x...x...x...x...", s: "....x.......x..x", h: "xxx.xxx.xxx.xxx.", mel: [0, 1, 2, -1, 2, 1, 0, -1, 3, 2, 1, -1, 2, -1, 1, -1] },
    chill: { bpm: 92, root: 50, chords: [[0, 0], [5, 0], [9, 1], [7, 0]], lead: "triangle", bass: "sine",
      k: "x.........x.....", s: "........x.......", h: "..x...x...x...x.", mel: [2, -1, -1, 1, -1, -1, 0, -1, 3, -1, -1, 2, -1, 1, -1, -1] },
    pond: { bpm: 108, root: 52, chords: [[0, 0], [7, 0], [9, 1], [5, 0]], lead: "triangle", bass: "triangle",
      k: "x.....x.x.......", s: "....x.......x...", h: "x...x.x.x...x.x.", mel: [0, 2, -1, 1, 2, -1, 3, 2, -1, 1, 0, -1, 2, -1, 1, -1] },
    arcade: { bpm: 126, root: 45, chords: [[0, 1], [5, 1], [10, 0], [7, 0]], lead: "square", bass: "square",
      k: "x...x...x...x...", s: "....x.......x...", h: "x.x.x.x.x.x.x.x.", mel: [0, 1, 2, 1, 3, 2, 1, 0, 2, -1, 4, 3, 2, -1, 1, -1] }
  };
  var seq = { on: false, step: 0, next: 0, timer: null };
  function chordTones(root, q) { return [root, root + (q ? 3 : 4), root + 7, root + 12, root + (q ? 15 : 16)]; }
  function schedStep(m, step, t) {
    var sx = step % 16, bar = Math.floor(step / 16) % m.chords.length, cyc = Math.floor(step / (16 * m.chords.length));
    var ch = m.chords[bar], root = m.root + ch[0], tones = chordTones(root, ch[1]);
    var st = 60 / m.bpm / 4, mb = musicBus;
    if (m.k[sx] === "x") { tone(mb, 150, 0.16, "sine", 0.9, { to: 48, t: t }); }
    if (m.s[sx] === "x") { noise(mb, 0.14, 0.32, "bandpass", 1800, 900, { t: t, q: 0.6 }); tone(mb, 210, 0.08, "triangle", 0.25, { t: t }); }
    if (m.h[sx] === "x") noise(mb, 0.035, 0.12, "highpass", 7000, null, { t: t });
    if (sx % 4 === 0 || (sx % 4 === 3 && m.bpm > 110)) tone(mb, mtof(root - 12 + (sx === 8 ? 7 : 0)), st * 1.8, m.bass, 0.42, { t: t, lp: 700 });
    if (sx === 0) tones.slice(0, 3).forEach(function (n) { tone(mb, mtof(n + 12), st * 14, "triangle", 0.07, { t: t, a: 0.06 }); });
    var mi = m.mel[sx];
    if (cyc % 2 === 1 && sx >= 12) mi = m.mel[(sx + 5) % 16]; // small variation every other pass
    if (mi != null && mi >= 0) tone(mb, mtof(tones[mi % tones.length] + 24), st * 1.6, m.lead, 0.13, { t: t, lp: 2800 });
  }
  function tick() {
    if (!seq.on || !ctx) return;
    var m = MOODS[cfg.mood] || MOODS.party, st = 60 / m.bpm / 4;
    if (seq.next < ctx.currentTime) seq.next = ctx.currentTime + 0.05;
    while (seq.next < ctx.currentTime + 0.15) {
      if (!muted) { try { schedStep(m, seq.step, seq.next); } catch (e) { /* ignore */ } }
      seq.step++; seq.next += st; counts.music++;
    }
  }
  function startMusic() {
    if (!ctx || seq.on || !cfg.music) return;
    seq.on = true; seq.step = 0; seq.next = ctx.currentTime + 0.1;
    seq.timer = setInterval(tick, 40);
  }
  function stopMusic() { seq.on = false; if (seq.timer) clearInterval(seq.timer); seq.timer = null; }

  /* ---------- unlock + mute ---------- */
  function unlock() {
    if (!inited) return;
    if (!ensureCtx()) return;
    if (ctx.state === "suspended") { try { ctx.resume(); } catch (e) { /* ignore */ } }
    if (!unlocked) {
      unlocked = true;
      startMusic();
      ["pointerdown", "touchstart", "keydown", "mousedown"].forEach(function (ev) { window.removeEventListener(ev, unlock, true); });
    }
  }
  function setMuted(v) {
    muted = !!v;
    try { localStorage.setItem(KEY, muted ? "1" : "0"); } catch (e) { /* ignore */ }
    if (master) master.gain.setTargetAtTime(muted ? 0 : 1, ctx.currentTime, 0.03);
    if (muted) engineStop(true);
    paintBtn();
  }
  function paintBtn() {
    if (!btn) return;
    btn.textContent = muted ? "\uD83D\uDD07" : "\uD83D\uDD0A";
    btn.setAttribute("aria-label", muted ? "Unmute sound (M)" : "Mute sound (M)");
    btn.title = btn.getAttribute("aria-label");
  }
  function makeBtn() {
    if (!cfg.button || btn || !document.body) return;
    btn = document.createElement("button");
    btn.type = "button"; btn.id = "arcade-mute";
    var pos = String(cfg.button), css = "position:fixed;z-index:2147483000;width:38px;height:38px;border-radius:50%;" +
      "border:1px solid rgba(255,255,255,.45);background:rgba(0,0,0,.45);color:#fff;font-size:18px;line-height:1;padding:0;" +
      "cursor:pointer;opacity:.75;touch-action:manipulation;-webkit-tap-highlight-color:transparent;";
    if (pos.indexOf(":") >= 0) css += pos; // raw CSS placement
    else {
      css += pos.indexOf("top") >= 0 ? "top:calc(10px + env(safe-area-inset-top));" : "bottom:calc(10px + env(safe-area-inset-bottom));";
      css += pos.indexOf("center") >= 0 ? "left:50%;transform:translateX(-50%);" : pos.indexOf("right") >= 0 ? "right:10px;" : "left:10px;";
    }
    btn.style.cssText = css;
    var stop = function (e) { e.stopPropagation(); };
    ["pointerdown", "pointerup", "touchstart", "touchend", "mousedown", "mouseup"].forEach(function (ev) { btn.addEventListener(ev, stop); });
    btn.addEventListener("click", function (e) { e.stopPropagation(); e.preventDefault(); unlock(); setMuted(!muted); btn.blur(); });
    document.body.appendChild(btn);
    paintBtn();
  }
  function onKey(e) {
    if (!cfg.muteKey || e.repeat) return;
    if (e.code !== "KeyM" && e.key !== "m" && e.key !== "M") return;
    var t = e.target, tag = t && t.tagName;
    if (tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT" || (t && t.isContentEditable)) return;
    unlock(); setMuted(!muted);
  }
  function padPoll() {
    if (unlocked) return;
    try {
      var pads = navigator.getGamepads ? navigator.getGamepads() : [];
      for (var i = 0; i < pads.length; i++) {
        var p = pads[i]; if (!p) continue;
        for (var b = 0; b < p.buttons.length; b++) if (p.buttons[b] && p.buttons[b].pressed) { unlock(); break; }
      }
    } catch (e) { /* ignore */ }
    if (!unlocked) setTimeout(padPoll, 250);
  }

  function init(o) {
    o = o || {};
    for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) cfg[k] = o[k];
    if (inited) { if (musicBus) musicBus.gain.value = cfg.musicVol; return api; }
    inited = true;
    ["pointerdown", "touchstart", "keydown", "mousedown"].forEach(function (ev) { window.addEventListener(ev, unlock, true); });
    window.addEventListener("keydown", onKey);
    document.addEventListener("visibilitychange", function () {
      if (!ctx) return;
      try { if (document.hidden) ctx.suspend(); else if (unlocked) ctx.resume(); } catch (e) { /* ignore */ }
    });
    if (document.body) makeBtn(); else document.addEventListener("DOMContentLoaded", makeBtn);
    padPoll();
    return api;
  }

  var api = {
    init: init,
    play: play,
    engine: engine,
    engineStop: function () { engineStop(false); },
    duck: duck,
    setMood: function (m) { if (MOODS[m]) cfg.mood = m; },
    music: function (on) { cfg.music = !!on; if (on) { if (unlocked) startMusic(); } else stopMusic(); },
    toggleMute: function () { setMuted(!muted); return muted; },
    setMuted: setMuted,
    isMuted: function () { return muted; },
    isUnlocked: function () { return unlocked; },
    unlock: unlock,
    sounds: Object.keys(SFX),
    moods: Object.keys(MOODS),
    _debug: function () { return { unlocked: unlocked, muted: muted, mood: cfg.mood, music: seq.on, steps: counts.music, played: counts.played, last: counts.last, engine: !!eng }; }
  };
  window.ArcadeAudio = api;
})();
