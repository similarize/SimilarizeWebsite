// src/game/standalone.ts
import {
  VERSION,
  VIEW_H,
  VIEW_W,
  createSim,
  draw,
  metersOf,
  scoreOf,
  shakeOffset,
  startRun,
  step,
  stageOf,
  wheelPace,
  MISSILE_MAX,
  MISSILE_RELOAD
} from "./engine.js?v=20260925ragdoll";
var BEST_KEY = "rc-rally-jump-best";
var RIG_KEY = "rc-rally-rig";
var TUNE_KEY = "rc-rally-tune";
function loadBest() {
  try {
    return Number(localStorage.getItem(BEST_KEY)) || 0;
  } catch {
    return 0;
  }
}
function loadRig() {
  try {
    const v = localStorage.getItem(RIG_KEY);
    return v === "crawler" || v === "pixel" ? v : "rally";
  } catch {
    return "rally";
  }
}
function saveRig(rig) {
  try {
    localStorage.setItem(RIG_KEY, rig);
  } catch {
  }
}
function loadTune() {
  const tune = { wheel: 1, chassis: 1, squish: 0, gravity: 1, trailer: 0 };
  try {
    const raw = JSON.parse(localStorage.getItem(TUNE_KEY) || "null");
    if (!raw) return tune;
    for (const key of Object.keys(tune)) {
      const n = Number(raw[key]);
      if (Number.isFinite(n)) tune[key] = n;
    }
  } catch {
  }
  return tune;
}
function saveTune(tune) {
  try {
    localStorage.setItem(TUNE_KEY, JSON.stringify(tune));
  } catch {
  }
}
function saveBest(n) {
  try {
    localStorage.setItem(BEST_KEY, String(n));
  } catch {
  }
}
var AudioBus = class {
  ctx = null;
  motor = null;
  motorGain = null;
  filter = null;
  unlock() {
    try {
      if (!this.ctx) {
        const ctx = new AudioContext();
        const osc = ctx.createOscillator();
        const filter = ctx.createBiquadFilter();
        const gain = ctx.createGain();
        osc.type = "sawtooth";
        osc.frequency.value = 72;
        filter.type = "lowpass";
        filter.frequency.value = 280;
        gain.gain.value = 0;
        osc.connect(filter);
        filter.connect(gain);
        gain.connect(ctx.destination);
        osc.start();
        this.ctx = ctx;
        this.motor = osc;
        this.filter = filter;
        this.motorGain = gain;
      }
      if (this.ctx.state === "suspended") void this.ctx.resume();
    } catch {
      this.ctx = null;
    }
  }
  motorDrive(playing, muted, boosting, speed) {
    if (!this.ctx || !this.motorGain || !this.motor || !this.filter) return;
    const now = this.ctx.currentTime;
    const vol = !playing || muted ? 0 : boosting ? 0.04 : 0.018;
    this.motorGain.gain.setTargetAtTime(vol, now, 0.05);
    this.motor.frequency.setTargetAtTime(64 + speed * 0.08 + (boosting ? 36 : 0), now, 0.08);
    this.filter.frequency.setTargetAtTime(boosting ? 520 : 240, now, 0.08);
  }
  tone(freq, dur, type, vol, muted) {
    if (!this.ctx || muted) return;
    const now = this.ctx.currentTime;
    const osc = this.ctx.createOscillator();
    const gain = this.ctx.createGain();
    osc.type = type;
    osc.frequency.setValueAtTime(freq, now);
    osc.frequency.exponentialRampToValueAtTime(Math.max(40, freq * 0.6), now + dur);
    gain.gain.setValueAtTime(vol, now);
    gain.gain.exponentialRampToValueAtTime(1e-3, now + dur);
    osc.connect(gain);
    gain.connect(this.ctx.destination);
    osc.start(now);
    osc.stop(now + dur + 0.02);
  }
  crash(muted) {
    if (!this.ctx || muted) return;
    const now = this.ctx.currentTime;
    const frames = Math.floor(this.ctx.sampleRate * 0.28);
    const buffer = this.ctx.createBuffer(1, frames, this.ctx.sampleRate);
    const data = buffer.getChannelData(0);
    for (let i = 0; i < frames; i++) data[i] = (Math.random() * 2 - 1) * (1 - i / frames);
    const src = this.ctx.createBufferSource();
    src.buffer = buffer;
    const filter = this.ctx.createBiquadFilter();
    filter.type = "lowpass";
    filter.frequency.value = 700;
    const gain = this.ctx.createGain();
    gain.gain.setValueAtTime(0.35, now);
    gain.gain.exponentialRampToValueAtTime(1e-3, now + 0.28);
    src.connect(filter);
    filter.connect(gain);
    gain.connect(this.ctx.destination);
    src.start(now);
  }
};
function el(id) {
  const node = document.getElementById(id);
  if (!node) throw new Error(`missing #${id}`);
  return node;
}
function artUrl(file) {
  return new URL(`./${file}`, import.meta.url).href;
}
function boot() {
  const canvas = el("view");
  const ctx = canvas.getContext("2d");
  if (!ctx) return;
  const sim = createSim(loadBest());
  sim.reduced = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  sim.rig = loadRig();
  sim.tune = loadTune();
  const audio = new AudioBus();
  const art = {
    buggy: new Image(),
    body: new Image(),
    wheelRear: new Image(),
    wheelFront: new Image(),
    drone: new Image(),
    sky: new Image(),
    crawler: new Image(),
    crawlerWheel: new Image(),
    pixel: new Image(),
    pixelWheel: new Image()
  };
  art.buggy.src = artUrl("buggy.png");
  art.body.src = artUrl("buggy-body.png");
  art.wheelRear.src = artUrl("wheel-rear.png");
  art.wheelFront.src = artUrl("wheel-front.png");
  art.drone.src = artUrl("drone.png");
  art.sky.src = artUrl("sky.jpg");
  art.crawler.src = artUrl("crawler-body.png") + "?v=20260925render";
  art.crawlerWheel.src = artUrl("crawler-wheel.png") + "?v=20260925render";
  art.pixel.src = artUrl("pixel-body.png") + "?v=20260925pixel";
  art.pixelWheel.src = artUrl("pixel-wheel.png") + "?v=20260925pixel";
  const rigBtns = [...document.querySelectorAll("#rigs .rig")];
  const picked = el("picked");
  const paintRigs = () => {
    rigBtns.forEach((btn) => {
      const on = btn.dataset.rig === sim.rig;
      btn.classList.toggle("on", on);
      btn.setAttribute("aria-pressed", on ? "true" : "false");
    });
    const label = sim.rig === "crawler" ? "Selected: red crawler" : sim.rig === "pixel" ? "Selected: pixel crawler" : "Selected: rally buggy";
    picked.textContent = label;
  };
  paintRigs();
  const chooseRig = (btn) => {
    sim.rig = btn.dataset.rig === "crawler" || btn.dataset.rig === "pixel" ? btn.dataset.rig : "rally";
    saveRig(sim.rig);
    paintRigs();
  };
  rigBtns.forEach((btn) => {
    btn.addEventListener("pointerdown", (e) => {
      e.preventDefault();
      e.stopPropagation();
      chooseRig(btn);
    });
    btn.addEventListener("click", (e) => {
      e.preventDefault();
      e.stopPropagation();
      chooseRig(btn);
    });
  });
  const scoreEl = el("score");
  const bestEl = el("best");
  const gatesEl = el("gates");
  const metersEl = el("meters");
  const threatEl = el("threat");
  const packEl = el("pack");
  const packFill = el("pack-fill");
  const packLow = el("pack-low");
  const bodyRow = el("body-row");
  const bodyDmg = el("body-dmg");
  const hudScore = el("hud-score");
  const hudPack = el("hud-pack");
  const title = el("title");
  const version = el("version");
  const pause = el("pause");
  const over = el("over");
  const reason = el("reason");
  const overScore = el("over-score");
  const overMeta = el("over-meta");
  const muteBtn = el("mute");
  const pauseBtn = el("pause-btn");
  const fireBtn = el("fire");
  const noseBtn = el("nose");
  const pips = [...el("pips").children];
  version.textContent = `DESERT CIRCUIT \xB7 ${VERSION}`;
  let pointer = false;
  let fireEdge = false;
  const keys = /* @__PURE__ */ new Set();
  let prevPhase = "title";
  let prevGates = 0;
  let bestSaved = sim.best;
  let hudAcc = 0;
  const paintHud = (force = false) => {
    const phaseChanged = sim.phase !== prevPhase;
    if (!force && !phaseChanged && hudAcc < 0.08) return;
    hudAcc = 0;
    const score = scoreOf(sim);
    const low = sim.battery < 25;
    scoreEl.textContent = score.toLocaleString();
    bestEl.textContent = `Best ${sim.best.toLocaleString()}`;
    gatesEl.textContent = String(sim.gatesCleared);
    metersEl.textContent = String(metersOf(sim));
    const stageName = { calm: "GATES", pattern: "WEAVE", hunt: "HUNT", lock: "LOCK" };
    threatEl.textContent = stageName[stageOf(sim.scroll)] || "GATES";
    packEl.textContent = `${Math.round(sim.battery)}%`;
    packEl.classList.toggle("low", low);
    packFill.style.width = `${Math.max(0, Math.min(100, sim.battery))}%`;
    packFill.classList.toggle("low", low);
    packLow.hidden = !(sim.phase === "play" && low);
    bodyRow.hidden = !(sim.phase === "play" && sim.damage > 0);
    bodyDmg.textContent = sim.damage < 34 ? "scuffed" : sim.damage < 68 ? "dented" : "beat up";
    fireBtn.hidden = sim.phase !== "play";
    noseBtn.hidden = sim.phase !== "play";
    pips.forEach((pip, i) => {
      const on = i < sim.missiles;
      const charging = !on && i === sim.missiles && sim.missiles < MISSILE_MAX;
      pip.classList.toggle("on", on);
      pip.classList.toggle("charge", charging);
      pip.style.setProperty("--fill", charging ? `${Math.round(sim.missileT / MISSILE_RELOAD * 100)}%` : "0%");
    });
    hudScore.hidden = sim.phase === "title";
    hudPack.hidden = sim.phase === "title";
    pauseBtn.hidden = sim.phase === "title";
    title.hidden = sim.phase !== "title";
    pause.hidden = sim.phase !== "pause";
    over.hidden = sim.phase !== "over";
    muteBtn.textContent = sim.muted ? "Unmute" : "Mute";
    muteBtn.setAttribute("aria-label", sim.muted ? "Unmute" : "Mute");
    pauseBtn.textContent = sim.phase === "pause" ? "Resume" : "Pause";
    pauseBtn.setAttribute("aria-label", sim.phase === "pause" ? "Resume" : "Pause");
    if (sim.phase === "over") {
      reason.textContent = sim.crash === "flip" ? "UPSIDE DOWN" : sim.crash === "drone" ? "DRONE HIT" : "CLIPPED A GATE";
      overScore.textContent = score.toLocaleString();
      overMeta.textContent = `${sim.gatesCleared} gates \xB7 ${metersOf(sim)} m \xB7 best ${sim.best.toLocaleString()}`;
    }
  };
  const begin = (hold) => {
    audio.unlock();
    startRun(sim);
    pointer = hold;
    fireEdge = false;
    prevGates = 0;
  };
  let noseDown = false;
  const boostHeld = () => pointer || keys.has("Space") || keys.has("ArrowUp") || keys.has("KeyW");
  const leanHeld = () => noseDown || keys.has("ArrowDown") || keys.has("KeyS");
  const resize = () => {
    const parent = canvas.parentElement;
    if (!parent) return;
    const rect = parent.getBoundingClientRect();
    const dpr = Math.min(2, window.devicePixelRatio || 1);
    canvas.width = Math.max(1, Math.floor(rect.width * dpr));
    canvas.height = Math.max(1, Math.floor(rect.height * dpr));
  };
  resize();
  window.addEventListener("resize", resize);
  window.addEventListener("keydown", (e) => {
    if (e.repeat) return;
    if (["Space", "ArrowUp", "ArrowDown", "KeyW"].includes(e.code)) e.preventDefault();
    keys.add(e.code);
    if (e.code === "KeyF" && sim.phase === "play") {
      e.preventDefault();
      fireEdge = true;
      return;
    }
    if (e.code === "KeyM") {
      sim.muted = !sim.muted;
      paintHud(true);
      return;
    }
    if (e.code === "KeyP" || e.code === "Escape") {
      if (sim.phase === "play") sim.phase = "pause";
      else if (sim.phase === "pause") sim.phase = "play";
      return;
    }
    if (e.code === "KeyR" && (sim.phase === "play" || sim.phase === "over" || sim.phase === "pause")) {
      audio.unlock();
      begin(false);
      return;
    }
    if ((e.code === "Space" || e.code === "ArrowUp" || e.code === "KeyW") && sim.phase === "title") {
      begin(true);
    }
    if ((e.code === "Space" || e.code === "KeyR") && sim.phase === "over" && sim.sinceOver > 0.35) {
      begin(false);
    }
  });
  window.addEventListener("keyup", (e) => {
    keys.delete(e.code);
  });
  const shell = el("shell");
  shell.addEventListener("pointerdown", (e) => {
    if (e.button === 2) {
      e.preventDefault();
      if (sim.phase === "play") fireEdge = true;
      return;
    }
    if (e.target.closest("[data-ui]")) return;
    audio.unlock();
    if (sim.phase === "title") begin(true);
    else if (sim.phase === "over" && sim.sinceOver > 0.4) begin(true);
    else if (sim.phase === "play") pointer = true;
  });
  const release = () => {
    pointer = false;
  };
  window.addEventListener("pointerup", release);
  window.addEventListener("pointercancel", release);
  document.addEventListener("visibilitychange", () => {
    if (document.hidden && sim.phase === "play") sim.phase = "pause";
  });
  shell.addEventListener("contextmenu", (e) => e.preventDefault());
  muteBtn.addEventListener("click", () => {
    sim.muted = !sim.muted;
    paintHud(true);
  });
  pauseBtn.addEventListener("click", () => {
    if (sim.phase === "play") sim.phase = "pause";
    else if (sim.phase === "pause") sim.phase = "play";
  });
  for (const id of ["start", "retry", "again"]) {
    el(id).addEventListener("click", () => begin(false));
  }
  el("resume").addEventListener("click", () => {
    if (sim.phase === "pause") sim.phase = "play";
  });
  const shoot = (e) => {
    e.preventDefault();
    e.stopPropagation();
    audio.unlock();
    if (sim.phase === "play") fireEdge = true;
  };
  fireBtn.addEventListener("pointerdown", shoot);
  const noseOn = (e) => {
    e.preventDefault();
    e.stopPropagation();
    noseDown = true;
  };
  const noseOff = () => {
    noseDown = false;
  };
  noseBtn.addEventListener("pointerdown", noseOn);
  noseBtn.addEventListener("pointerup", noseOff);
  noseBtn.addEventListener("pointercancel", noseOff);
  noseBtn.addEventListener("pointerleave", noseOff);
  el("pips").addEventListener("pointerdown", shoot);
  const tunePanel = el("tune");
  const tuneFields = ["wheel", "chassis", "squish", "gravity", "trailer"];
  const tuneLabel = (key, value) => {
    if (key === "squish") return `${Math.round(value * 100)}%`;
    if (key === "trailer") {
      if (value < 0.04) return "off";
      if (value < 0.75) return "light";
      if (value < 1.45) return "loaded";
      return "heavy";
    }
    return `${value.toFixed(2)}\u00d7`;
  };
  const paintTune = () => {
    for (const key of tuneFields) {
      const value = sim.tune[key];
      el(`tune-${key}`).value = String(value);
      el(`lbl-${key}`).textContent = tuneLabel(key, value);
    }
  };
  paintTune();
  for (const key of tuneFields) {
    el(`tune-${key}`).addEventListener("input", (e) => {
      sim.tune[key] = Number(e.target.value);
      el(`lbl-${key}`).textContent = tuneLabel(key, sim.tune[key]);
      saveTune(sim.tune);
    });
  }
  el("tune-btn").addEventListener("click", () => {
    tunePanel.hidden = !tunePanel.hidden;
  });
  el("tune-close").addEventListener("click", () => {
    tunePanel.hidden = true;
  });
  el("tune-reset").addEventListener("click", () => {
    sim.tune = { wheel: 1, chassis: 1, squish: 0, gravity: 1, trailer: 0 };
    saveTune(sim.tune);
    paintTune();
  });
  tunePanel.addEventListener("pointerdown", (e) => e.stopPropagation());
  let last = performance.now();
  let prevBooms = 0;
  let prevScrapes = 0;
  const frame = (now) => {
    const dt = Math.min(0.05, (now - last) / 1e3);
    last = now;
    const fire = fireEdge;
    fireEdge = false;
    const before = sim.missiles;
    step(sim, dt, { boost: boostHeld() && sim.phase === "play", fire, lean: leanHeld() && sim.phase === "play" });
    const phaseChanged = sim.phase !== prevPhase;
    if (sim.phase === "over" && phaseChanged) {
      audio.crash(sim.muted);
      saveBest(sim.best);
    }
    if (sim.gatesCleared > prevGates && sim.phase === "play") {
      audio.tone(620, 0.12, "triangle", 0.08, sim.muted);
    }
    if (sim.phase === "play" && sim.missiles < before) {
      audio.tone(880, 0.08, "square", 0.05, sim.muted);
    }
    if (sim.booms > prevBooms) {
      audio.tone(160, 0.16, "sawtooth", 0.1, sim.muted);
    }
    if (sim.scrapes > prevScrapes) {
      audio.tone(110, 0.12, "square", 0.06, sim.muted);
    }
    prevBooms = sim.booms;
    prevScrapes = sim.scrapes;
    prevGates = sim.gatesCleared;
    audio.motorDrive(sim.phase === "play", sim.muted, sim.boosting, wheelPace(sim));
    const portrait = canvas.height > canvas.width * 1.05;
    const scale = portrait ? Math.min(canvas.width / 700, canvas.height / VIEW_H) : Math.min(canvas.width / VIEW_W, canvas.height / VIEW_H);
    const visW = canvas.width / scale;
    const windowStart = portrait ? Math.max(0, Math.min(72, VIEW_W - visW)) : 0;
    const shake = shakeOffset(sim);
    const worldH = VIEW_H * scale;
    const oy = (canvas.height - worldH) / 2 + shake.y * scale;
    const ox = (portrait ? 0 : (canvas.width - VIEW_W * scale) / 2) + shake.x * scale;
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    if (art.sky && art.sky.complete && art.sky.naturalWidth > 0) {
      ctx.drawImage(art.sky, 0, 0, canvas.width, canvas.height);
    } else {
      ctx.fillStyle = "#1c2438";
      ctx.fillRect(0, 0, canvas.width, canvas.height);
    }
    ctx.setTransform(scale, 0, 0, scale, ox - windowStart * scale, oy);
    draw(ctx, sim, art);
    if (sim.best > bestSaved) {
      bestSaved = sim.best;
      saveBest(sim.best);
    }
    hudAcc += dt;
    paintHud(phaseChanged);
    prevPhase = sim.phase;
    requestAnimationFrame(frame);
  };
  paintHud(true);
  requestAnimationFrame(frame);
}
boot();
