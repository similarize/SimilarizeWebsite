/**
 * Froggies · SPS Track — first playable (Jimmy's Gone Rogue)
 * Vanilla JS, no bundler, phone-first. Canon: James/Jimmy/Bubbles/Rexy;
 * Purple Bear / Blue Bear; Optimus advanced kit. No Zip/Tinker/etc.
 */
(function () {
  'use strict';

  var PLANETS = [
    'Mercury', 'Venus', 'Earth', 'Mars',
    'Jupiter', 'Saturn', 'Uranus', 'Neptune'
  ];

  // Orbit radii (canvas units, center at 180) — schematic, not to scale
  var ORBIT_R = [40, 55, 72, 90, 115, 135, 152, 168];
  var SUN = { x: 180, y: 180 };

  var ROUND_SECONDS = 60;
  var COOLDOWNS = { hover: 2.5, rocket: 4, drone: 5, map: 3.5 };
  var MOVE = { hover: 18, rocket: 38 };

  var state = {
    screen: 'title',
    timeLeft: ROUND_SECONDS,
    running: false,
    planet: null,
    planetIndex: 0,
    jimmyAngle: 0,
    jimmyR: 90,
    jimmyVisible: false,
    jimmyRevealed: false,
    seeker: { x: 180, y: 180 },
    confidence: 0, // 0..1
    calledPurple: false,
    peekNote: '',
    cds: { hover: 0, rocket: 0, drone: 0, map: 0 },
    lastAbility: '',
    raf: 0,
    lastTs: 0,
    optimusReturn: 'hub'
  };

  var els = {};

  function $(id) { return document.getElementById(id); }

  function cacheEls() {
    els.app = $('app');
    els.screens = {
      title: $('screen-title'),
      hub: $('screen-hub'),
      phone: $('screen-phone'),
      sps: $('screen-sps'),
      optimus: $('screen-optimus'),
      win: $('screen-win'),
      lose: $('screen-lose')
    };
    els.alertBanner = $('alert-banner');
    els.alertText = $('alert-text');
    els.callPanel = $('call-panel');
    els.callStatus = $('call-status');
    els.speech = $('speech-bubble');
    els.speechText = $('speech-text');
    els.btnHangup = $('btn-hangup');
    els.canvas = $('sps-canvas');
    els.ctx = els.canvas.getContext('2d');
    els.confidence = $('confidence');
    els.targetPlanet = $('target-planet');
    els.spsHint = $('sps-hint');
    els.btnLock = $('btn-lock');
    els.abilityFeedback = $('ability-feedback');
    els.winDetail = $('win-detail');
    els.loseDetail = $('lose-detail');
    els.timers = {
      hub: $('hub-timer'),
      phone: $('phone-timer'),
      sps: $('sps-timer'),
      opt: $('opt-timer')
    };
    els.btnOpenPhone = $('btn-open-phone');
    els.btnOpenSps = $('btn-open-sps');
    els.btnOpenOptimus = $('btn-open-optimus');
  }

  function showScreen(name) {
    state.screen = name;
    Object.keys(els.screens).forEach(function (k) {
      els.screens[k].classList.toggle('active', k === name);
    });
    if (name === 'sps') drawSps();
  }

  function formatTime(t) {
    var s = Math.max(0, Math.ceil(t));
    var m = Math.floor(s / 60);
    var r = s % 60;
    return m + ':' + (r < 10 ? '0' : '') + r;
  }

  function updateTimers() {
    var label = formatTime(state.timeLeft);
    var urgent = state.timeLeft <= 10;
    Object.keys(els.timers).forEach(function (k) {
      var el = els.timers[k];
      if (!el) return;
      el.textContent = label;
      el.classList.toggle('urgent', urgent);
    });
  }

  function updateConfidenceUI() {
    var pct = Math.round(state.confidence * 100);
    var label = pct < 30 ? 'low' : pct < 70 ? 'medium' : 'high';
    els.confidence.textContent = 'Confidence: ' + label + ' (' + pct + '%)';
    els.targetPlanet.textContent = state.jimmyRevealed
      ? 'Target: ' + state.planet
      : 'Target: scanning…';
    var canLock = state.confidence >= 0.72 || distance(state.seeker, jimmyPos()) < 22;
    els.btnLock.disabled = !canLock;
    if (canLock) {
      els.spsHint.textContent = 'SPS lock ready — bring Jimmy home!';
    } else if (state.jimmyRevealed) {
      els.spsHint.textContent = 'Jimmy near ' + state.planet + '. Close the gap with Optimus.';
    } else {
      els.spsHint.textContent = 'Track Jimmy’s blip. Use Optimus to close in.';
    }
  }

  function updateAbilityCds() {
    Object.keys(COOLDOWNS).forEach(function (key) {
      var nodes = document.querySelectorAll('[data-cd="' + key + '"]');
      var left = state.cds[key];
      var btns = document.querySelectorAll('[data-ability="' + key + '"]');
      var i;
      for (i = 0; i < nodes.length; i++) {
        nodes[i].textContent = left > 0 ? left.toFixed(1) + 's' : '';
      }
      for (i = 0; i < btns.length; i++) {
        btns[i].disabled = left > 0 || !state.running;
      }
    });
  }

  function unlockHubButtons() {
    els.btnOpenPhone.disabled = false;
    // SPS + Optimus unlock after Purple Bear call (story beat)
    els.btnOpenSps.disabled = !state.calledPurple;
    els.btnOpenOptimus.disabled = !state.calledPurple;
  }

  function pickPlanet() {
    var i = Math.floor(Math.random() * PLANETS.length);
    state.planetIndex = i;
    state.planet = PLANETS[i];
    state.jimmyR = ORBIT_R[i];
    state.jimmyAngle = Math.random() * Math.PI * 2;
  }

  function jimmyPos() {
    return {
      x: SUN.x + Math.cos(state.jimmyAngle) * state.jimmyR,
      y: SUN.y + Math.sin(state.jimmyAngle) * state.jimmyR
    };
  }

  function distance(a, b) {
    var dx = a.x - b.x;
    var dy = a.y - b.y;
    return Math.sqrt(dx * dx + dy * dy);
  }

  function startRound() {
    pickPlanet();
    state.timeLeft = ROUND_SECONDS;
    state.running = true;
    state.jimmyVisible = false;
    state.jimmyRevealed = false;
    state.seeker = { x: SUN.x, y: SUN.y };
    state.confidence = 0.05;
    state.calledPurple = false;
    state.peekNote = '';
    state.cds = { hover: 0, rocket: 0, drone: 0, map: 0 };
    state.lastAbility = '';
    state.lastTs = 0;
    state.optimusReturn = 'hub';

    els.alertBanner.classList.remove('hidden');
    els.alertText.textContent = 'Jimmy went rogue toward the solar system!';
    els.callPanel.classList.add('hidden');
    els.speech.hidden = true;
    els.btnHangup.hidden = true;
    els.callStatus.textContent = 'Calling Purple Bear…';
    els.abilityFeedback.textContent = 'Ready.';
    unlockHubButtons();
    updateTimers();
    updateConfidenceUI();
    updateAbilityCds();
    showScreen('hub');

    // Brief beat then nudge toward phone
    setTimeout(function () {
      if (!state.running) return;
      els.alertText.textContent =
        'Jimmy went rogue! Call Purple Bear — then track him on SPS.';
    }, 900);

    if (state.raf) cancelAnimationFrame(state.raf);
    state.raf = requestAnimationFrame(tick);
  }

  function endWin() {
    state.running = false;
    els.winDetail.textContent =
      'SPS lock on ' + state.planet + '. Optimus escorted Jimmy home to the ranch.';
    showScreen('win');
  }

  function endLose() {
    state.running = false;
    els.loseDetail.textContent =
      'Jimmy’s on ' + state.planet + ' again… tap Retry!';
    showScreen('lose');
  }

  function tick(ts) {
    if (!state.running) return;
    if (!state.lastTs) state.lastTs = ts;
    var dt = Math.min(0.05, (ts - state.lastTs) / 1000);
    state.lastTs = ts;

    state.timeLeft -= dt;
    if (state.timeLeft <= 0) {
      state.timeLeft = 0;
      updateTimers();
      endLose();
      return;
    }

    // Jimmy drifts on his orbit
    state.jimmyAngle += dt * 0.35;

    // Cooldowns
    Object.keys(state.cds).forEach(function (k) {
      if (state.cds[k] > 0) {
        state.cds[k] = Math.max(0, state.cds[k] - dt);
      }
    });

    // Soft seeker drift if very close — auto win check
    if (distance(state.seeker, jimmyPos()) < 16 && state.confidence >= 0.55) {
      endWin();
      return;
    }

    updateTimers();
    updateAbilityCds();
    if (state.screen === 'sps') {
      updateConfidenceUI();
      drawSps();
    }

    state.raf = requestAnimationFrame(tick);
  }

  function drawSps() {
    var ctx = els.ctx;
    var w = els.canvas.width;
    var h = els.canvas.height;
    ctx.clearRect(0, 0, w, h);

    // stars
    ctx.fillStyle = '#061018';
    ctx.fillRect(0, 0, w, h);
    ctx.fillStyle = '#ffffff';
    var si;
    for (si = 0; si < 40; si++) {
      var sx = (si * 97 + 13) % w;
      var sy = (si * 53 + 29) % h;
      ctx.globalAlpha = 0.25 + (si % 5) * 0.1;
      ctx.fillRect(sx, sy, 1.5, 1.5);
    }
    ctx.globalAlpha = 1;

    // orbits
    var i;
    for (i = 0; i < ORBIT_R.length; i++) {
      ctx.beginPath();
      ctx.arc(SUN.x, SUN.y, ORBIT_R[i], 0, Math.PI * 2);
      ctx.strokeStyle = i === state.planetIndex && state.jimmyRevealed
        ? 'rgba(61,255,154,0.45)'
        : 'rgba(94,184,255,0.18)';
      ctx.lineWidth = i === state.planetIndex && state.jimmyRevealed ? 2 : 1;
      ctx.stroke();
    }

    // sun
    var grd = ctx.createRadialGradient(SUN.x, SUN.y, 2, SUN.x, SUN.y, 18);
    grd.addColorStop(0, '#fff6a8');
    grd.addColorStop(1, '#ff9a2e');
    ctx.fillStyle = grd;
    ctx.beginPath();
    ctx.arc(SUN.x, SUN.y, 14, 0, Math.PI * 2);
    ctx.fill();

    // planet dots
    for (i = 0; i < PLANETS.length; i++) {
      var ang = (i / PLANETS.length) * Math.PI * 2 - Math.PI / 2;
      // park planets at fixed schematic angles for readability
      ang = -Math.PI / 2 + i * (Math.PI * 2 / PLANETS.length);
      var px = SUN.x + Math.cos(ang) * ORBIT_R[i];
      var py = SUN.y + Math.sin(ang) * ORBIT_R[i];
      ctx.beginPath();
      ctx.arc(px, py, i === 2 ? 5 : 3.5, 0, Math.PI * 2);
      ctx.fillStyle = i === state.planetIndex && state.jimmyRevealed
        ? '#3dff9a'
        : '#8aa0bc';
      ctx.fill();
    }

    // seeker (James / Optimus track)
    ctx.beginPath();
    ctx.arc(state.seeker.x, state.seeker.y, 7, 0, Math.PI * 2);
    ctx.fillStyle = '#5eb8ff';
    ctx.fill();
    ctx.strokeStyle = '#fff';
    ctx.lineWidth = 1.5;
    ctx.stroke();
    ctx.fillStyle = '#5eb8ff';
    ctx.font = '10px sans-serif';
    ctx.fillText('YOU', state.seeker.x - 10, state.seeker.y - 12);

    // Jimmy blip — fuzzy until revealed / high confidence
    var jp = jimmyPos();
    var showBlip = state.jimmyVisible || state.jimmyRevealed || state.confidence > 0.25;
    if (showBlip) {
      var jitter = state.confidence < 0.5 ? (1 - state.confidence) * 14 : 0;
      var bx = jp.x + Math.sin(state.jimmyAngle * 3) * jitter;
      var by = jp.y + Math.cos(state.jimmyAngle * 2) * jitter;
      ctx.beginPath();
      ctx.arc(bx, by, 6 + (1 - state.confidence) * 4, 0, Math.PI * 2);
      ctx.fillStyle = 'rgba(255, 94, 122, ' + (0.35 + state.confidence * 0.55) + ')';
      ctx.fill();
      if (state.confidence > 0.4) {
        ctx.fillStyle = '#ff5e7a';
        ctx.font = '10px sans-serif';
        ctx.fillText('JIMMY', bx - 16, by - 12);
      }
    }

    // confidence ring around seeker
    ctx.beginPath();
    ctx.arc(state.seeker.x, state.seeker.y, 12 + state.confidence * 28, 0, Math.PI * 2);
    ctx.strokeStyle = 'rgba(61,255,154,' + (0.15 + state.confidence * 0.5) + ')';
    ctx.lineWidth = 2;
    ctx.stroke();
  }

  function moveSeekerTowardJimmy(amount) {
    var jp = jimmyPos();
    var dx = jp.x - state.seeker.x;
    var dy = jp.y - state.seeker.y;
    var d = Math.sqrt(dx * dx + dy * dy) || 1;
    var step = Math.min(amount, d);
    state.seeker.x += (dx / d) * step;
    state.seeker.y += (dy / d) * step;
  }

  function useAbility(name) {
    if (!state.running || state.cds[name] > 0) return;
    state.cds[name] = COOLDOWNS[name];
    state.lastAbility = name;

    if (name === 'hover') {
      moveSeekerTowardJimmy(MOVE.hover);
      state.confidence = Math.min(1, state.confidence + 0.08);
      els.abilityFeedback.textContent = 'Hover — seeker nudged toward Jimmy.';
    } else if (name === 'rocket') {
      moveSeekerTowardJimmy(MOVE.rocket);
      state.confidence = Math.min(1, state.confidence + 0.12);
      els.abilityFeedback.textContent = 'Rocket / afterburner — big boost!';
    } else if (name === 'drone') {
      state.jimmyVisible = true;
      state.jimmyRevealed = true;
      state.confidence = Math.min(1, state.confidence + 0.22);
      els.abilityFeedback.textContent =
        'Drone scout — Jimmy’s near ' + state.planet + '!';
    } else if (name === 'map') {
      state.jimmyVisible = true;
      state.confidence = Math.min(1, state.confidence + 0.28);
      if (state.confidence >= 0.45) state.jimmyRevealed = true;
      moveSeekerTowardJimmy(10);
      els.abilityFeedback.textContent = 'Map ping — SPS coordinate refined.';
    }

    updateAbilityCds();
    updateConfidenceUI();

    // After ability, show SPS so kids see the effect
    state.optimusReturn = 'sps';
    showScreen('sps');
    drawSps();

    if (distance(state.seeker, jimmyPos()) < 18 && state.confidence >= 0.5) {
      endWin();
    }
  }

  function dialPurple() {
    if (!state.running) return;
    els.callPanel.classList.remove('hidden');
    els.speech.hidden = true;
    els.btnHangup.hidden = true;
    els.callStatus.textContent = 'Calling Purple Bear…';

    setTimeout(function () {
      if (!state.running) return;
      els.callStatus.textContent = 'Connected · Purple Bear';
      els.speech.hidden = false;
      els.speechText.textContent =
        'Hey James — he’s near ' + state.planet + '. Check SPS.';
      els.btnHangup.hidden = false;
      state.calledPurple = true;
      state.jimmyVisible = true;
      state.confidence = Math.max(state.confidence, 0.2);
      unlockHubButtons();
    }, 700);
  }

  function peekContact(who) {
    if (who === 'blue') {
      // Soft pet-like line only — no invented Blue Bear lore
      els.callPanel.classList.remove('hidden');
      els.callStatus.textContent = 'Blue Bear (nearby, place-bound)';
      els.speech.hidden = false;
      els.speechText.textContent = '…meow.';
      els.btnHangup.hidden = true;
    }
  }

  function onAction(action, el) {
    switch (action) {
      case 'start':
      case 'retry':
        startRound();
        break;
      case 'open-phone':
        showScreen('phone');
        break;
      case 'open-sps':
        if (!state.calledPurple) return;
        showScreen('sps');
        drawSps();
        updateConfidenceUI();
        break;
      case 'open-optimus':
        if (!state.calledPurple) return;
        state.optimusReturn = state.screen === 'sps' ? 'sps' : 'hub';
        showScreen('optimus');
        updateAbilityCds();
        break;
      case 'to-hub':
        showScreen('hub');
        break;
      case 'back-from-optimus':
        var backTo = state.optimusReturn === 'sps' ? 'sps' : 'hub';
        showScreen(backTo);
        if (backTo === 'sps') {
          drawSps();
          updateConfidenceUI();
        }
        break;
      case 'dial':
        if (el && el.getAttribute('data-who') === 'purple') dialPurple();
        break;
      case 'peek':
        if (el) peekContact(el.getAttribute('data-who'));
        break;
      case 'hangup':
        showScreen('sps');
        drawSps();
        updateConfidenceUI();
        break;
      case 'ability':
        if (el) useAbility(el.getAttribute('data-ability'));
        break;
      case 'lock':
        if (!els.btnLock.disabled) endWin();
        break;
      default:
        break;
    }
  }

  function onTap(e) {
    var t = e.target.closest('[data-action]');
    if (!t) return;
    e.preventDefault();
    onAction(t.getAttribute('data-action'), t);
  }

  function init() {
    cacheEls();
    document.getElementById('app').addEventListener('click', onTap);
    // Prevent double-tap zoom delay feel on iOS for buttons
    document.getElementById('app').addEventListener('touchend', function (e) {
      var t = e.target.closest('[data-action]');
      if (!t) return;
      // let click fire; no extra prevent unless needed
    }, { passive: true });
    showScreen('title');
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
