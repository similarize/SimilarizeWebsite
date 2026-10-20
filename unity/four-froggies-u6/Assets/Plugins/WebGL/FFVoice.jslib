// Four Froggies 6.6 (ffu6q): VOICE TALK with the robots (Game/RobotVoice66.cs).
// Speech in = the browser's Web Speech API (SpeechRecognition / webkitSpeechRecognition), one shot per press, interim
// results; speech out = speechSynthesis with a per-robot voice / pitch / rate. No network AI, no API keys: the browser's
// own recogniser is used as-is (Chrome / Edge / Safari; Firefox + the Tesla browser have none -> the game shows quick
// replies instead). C# polls events with FFVoice_Poll (one "K:text" event per call, "" = none):
//   S: listening started   I:<interim text>   F:<final text>   E:<error>   X: ended
// Probe hook: ?voicetest=1 (or window.__ffVoiceFake = true before load) replaces the recogniser with a fake that "hears"
// the phrase C# queued with FFVoice_SetFake (else the next |-separated phrase of ?voicetest=), emitting the same
// onresult events (interim then final) as the real one. Every speak() is logged "FFVOICE tts ..." + kept in window.__ffTts.
mergeInto(LibraryManager.library, {
  FFVoice_Init: function () {
    if (window.FFV) return;
    var V = window.FFV = { q: [], rec: null, on: false, fake: null, fakeList: [], voices: [], tts: !!window.speechSynthesis };
    try {
      var m = /[?&]voicetest=([^&#]*)/.exec(location.search || '');
      var raw0 = m ? decodeURIComponent(m[1].replace(/\+/g, ' ')) : '';
      if (raw0 === 'off') {
        // probe: a browser without speech recognition (Firefox / the Tesla browser)
        try { window.SpeechRecognition = undefined; window.webkitSpeechRecognition = undefined; } catch (e) {}
        try { Object.defineProperty(window, 'webkitSpeechRecognition', { value: undefined, configurable: true }); Object.defineProperty(window, 'SpeechRecognition', { value: undefined, configurable: true }); } catch (e) {}
        console.log('FFVOICE voicetest=off: recognition removed');
      } else if (m || window.__ffVoiceFake) {
        var raw = raw0;
        V.fakeList = raw && raw !== '1' ? raw.split('|') : [];
        var Fake = function () { this.lang = 'en-US'; this.interimResults = true; this.continuous = false; this.maxAlternatives = 1; };
        Fake.prototype.start = function () {
          var self = this, phrase = V.fake != null ? V.fake : (V.fakeList.length ? V.fakeList.shift() : 'hello');
          V.fake = null;
          var res = function (txt, fin) {
            var alt = { transcript: txt, confidence: 0.9 }, r = [alt]; r.isFinal = fin; r.length = 1;
            var list = [r]; list.length = 1;
            return { resultIndex: 0, results: list };
          };
          console.log('FFVOICE fake recogniser hears "' + phrase + '"');
          setTimeout(function () { if (self.onstart) self.onstart(); }, 50);
          var w = phrase.split(' '), half = w.slice(0, Math.max(1, Math.ceil(w.length / 2))).join(' ');
          setTimeout(function () { if (self.onresult) self.onresult(res(half, false)); }, 400);
          setTimeout(function () { if (self.onresult) self.onresult(res(phrase, true)); }, 900);
          setTimeout(function () { if (self.onend) self.onend(); }, 1000);
        };
        Fake.prototype.stop = function () {};
        Fake.prototype.abort = function () { if (this.onend) this.onend(); };
        window.SpeechRecognition = Fake; window.webkitSpeechRecognition = Fake;
        console.log('FFVOICE fake recogniser installed (' + V.fakeList.length + ' url phrases)');
      }
    } catch (e) { console.warn('FFVOICE fake install failed ' + e); }
    V.load = function () { try { V.voices = (window.speechSynthesis.getVoices() || []).filter(function (v) { return /^en/i.test(v.lang || ''); }); } catch (e) { V.voices = []; } };
    // ffu65: robots speak with MALE voices only (by name; none found -> the en list, with the lowest pitch doing the work)
    V.male = function () {
      var re = /\bmale\b|google uk english male|david|daniel|alex\b|fred|george|james|mark|ryan|guy|arthur|aaron|tom\b|rishi|lee\b/i, fe = /female|woman|girl|samantha|zira|susan|karen|moira|tessa|victoria|fiona|hazel|serena|kate|libby|sonia|aria|jenny/i;
      var m = V.voices.filter(function (v) { return re.test(v.name || '') && !fe.test(v.name || ''); });
      return m;
    };
    V.beep = function (vol) {
      try {
        var C = V.ac || (V.ac = new (window.AudioContext || window.webkitAudioContext)()); if (C.state === 'suspended') C.resume();
        var t = C.currentTime, o = C.createOscillator(), g = C.createGain(); o.type = 'square'; o.frequency.setValueAtTime(880, t); o.frequency.setValueAtTime(660, t + 0.06);
        g.gain.setValueAtTime(0.0001, t); g.gain.linearRampToValueAtTime(0.06 * vol, t + 0.01); g.gain.linearRampToValueAtTime(0.0001, t + 0.13);
        o.connect(g); g.connect(C.destination); o.start(t); o.stop(t + 0.14);
      } catch (e) {}
    };
    if (V.tts) { V.load(); try { window.speechSynthesis.onvoiceschanged = V.load; } catch (e) {} }
    window.__ffTts = window.__ffTts || [];
    console.log('FFVOICE init: recognition ' + (!!(window.SpeechRecognition || window.webkitSpeechRecognition)) + ', tts ' + V.tts);
  },
  FFVoice_Supported: function () { return (window.SpeechRecognition || window.webkitSpeechRecognition) ? 1 : 0; },
  FFVoice_TtsSupported: function () { return window.speechSynthesis ? 1 : 0; },
  FFVoice_SetFake: function (p) { if (window.FFV) window.FFV.fake = UTF8ToString(p); },
  FFVoice_Start: function (lang) {
    var V = window.FFV; if (!V) return 0;
    var R = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!R) { V.q.push('E:unsupported'); return 0; }
    try { if (V.rec) { V.rec.onend = null; V.rec.abort(); } } catch (e) {}
    try { if (window.speechSynthesis) window.speechSynthesis.cancel(); } catch (e) {}
    var r = new R(); V.rec = r; V.on = true;
    r.lang = UTF8ToString(lang) || 'en-US'; r.interimResults = true; r.continuous = false; r.maxAlternatives = 1;
    r.onstart = function () { V.q.push('S:'); };
    r.onresult = function (ev) {
      var fin = '', mid = '';
      for (var i = ev.resultIndex; i < ev.results.length; i++) { var t = ev.results[i][0].transcript; if (ev.results[i].isFinal) fin += t; else mid += t; }
      if (fin) V.q.push('F:' + fin); else if (mid) V.q.push('I:' + mid);
    };
    r.onerror = function (ev) { V.q.push('E:' + (ev && ev.error ? ev.error : 'error')); };
    r.onend = function () { V.on = false; V.q.push('X:'); };
    try { r.start(); } catch (e) { V.on = false; V.q.push('E:' + e); V.q.push('X:'); return 0; }
    return 1;
  },
  FFVoice_Stop: function () { var V = window.FFV; try { if (V && V.rec && V.on) V.rec.stop(); } catch (e) {} },
  FFVoice_Poll: function () {
    var V = window.FFV, s = V && V.q.length ? V.q.shift() : '';
    var n = lengthBytesUTF8(s) + 1, b = _malloc(n); stringToUTF8(s, b, n); return b;
  },
  FFVoice_Speak: function (text, pitch, rate, vidx, vol) {
    var s = UTF8ToString(text), V = window.FFV;
    var rec = { text: s, pitch: pitch, rate: rate, voice: vidx, vol: vol, t: Date.now() };
    window.__ffTts = window.__ffTts || []; window.__ffTts.push(rec);
    console.log('FFVOICE tts pitch ' + pitch.toFixed(2) + ' rate ' + rate.toFixed(2) + ' voice ' + vidx + ' vol ' + vol.toFixed(2) + ': ' + s);
    if (!window.speechSynthesis || vol <= 0) return 0;
    try {
      var u = new SpeechSynthesisUtterance(s);
      if (V && !V.voices.length) V.load();
      var ml = V ? V.male() : [], vl = ml.length ? ml : (V ? V.voices : []);
      if (vl.length) u.voice = vl[((vidx % vl.length) + vl.length) % vl.length];
      rec.voiceName = u.voice ? u.voice.name : ''; rec.male = ml.length > 0;
      console.log('FFVOICE robot voice ' + (rec.voiceName || 'default') + (rec.male ? ' (male)' : ' (no male voice found, low pitch)'));
      if (V) V.beep(vol);
      u.lang = 'en-US'; u.pitch = Math.max(0, Math.min(0.45, pitch)); u.rate = Math.max(0.5, Math.min(2, rate)); u.volume = Math.max(0, Math.min(1, vol));
      window.speechSynthesis.speak(u);
      return 1;
    } catch (e) { console.warn('FFVOICE tts failed ' + e); return 0; }
  },
  FFVoice_Cancel: function () { try { if (window.speechSynthesis) window.speechSynthesis.cancel(); } catch (e) {} },
  FFVoice_Speaking: function () { try { return window.speechSynthesis && window.speechSynthesis.speaking ? 1 : 0; } catch (e) { return 0; } }
});
