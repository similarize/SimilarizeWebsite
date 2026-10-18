// Four Froggies (ffu10): streamed music + ambience beds. Each bed is a seamless Ogg Vorbis loop under audio/ next to
// index.html, fetched + decoded only when its stage is entered (so the first load stays small), looped sample-exactly
// with AudioBufferSourceNode.loop, and cross-faded on two channels (0 = music, 1 = ambience). Unity's own AudioListener
// volume (M / SOUND button) is mirrored with FFAudio_Master. The context resumes on the first tap / click / key.
mergeInto(LibraryManager.library, {
  FFAudio_Init: function () {
    if (window.FFA) return;
    var A = window.FFA = { ctx: null, master: null, lp: null, ch: [], cache: {}, order: [], vol: 1, want: [null, null] };
    A.make = function () {
      if (A.ctx) return true;
      var C = window.AudioContext || window.webkitAudioContext;
      if (!C) return false;
      A.ctx = new C();
      A.master = A.ctx.createGain(); A.master.gain.value = A.vol;
      A.lp = A.ctx.createBiquadFilter(); A.lp.type = 'lowpass'; A.lp.frequency.value = 20000; A.lp.Q.value = 0.5;
      A.lp.connect(A.master); A.master.connect(A.ctx.destination);
      for (var i = 0; i < 2; i++) { var g = A.ctx.createGain(); g.gain.value = 1; g.connect(i === 0 ? A.lp : A.master); A.ch.push({ bus: g, cur: null }); }
      return true;
    };
    var resume = function () {
      if (!A.make()) return;
      if (A.ctx.state !== 'running') A.ctx.resume().then(function () { console.log('FFAudio: context ' + A.ctx.state); });
      for (var i = 0; i < 2; i++) if (A.want[i] && !A.ch[i].cur) A.start(i, A.want[i].url, A.want[i].vol, A.want[i].fade);
    };
    ['pointerdown', 'touchend', 'keydown', 'mousedown'].forEach(function (e) { window.addEventListener(e, resume, true); });
    A.decode = function (url, done) {
      if (A.cache[url]) { done(A.cache[url]); return; }
      fetch(url).then(function (r) { if (!r.ok) throw new Error('HTTP ' + r.status); return r.arrayBuffer(); }).then(function (ab) {
        return new Promise(function (ok, bad) { A.ctx.decodeAudioData(ab, ok, bad); });
      }).then(function (buf) {
        A.cache[url] = buf; A.order.push(url);
        while (A.order.length > 4) { var old = A.order.shift(); if (A.cache[old]) delete A.cache[old]; }   // keep memory bounded
        console.log('FFAudio: decoded ' + url + ' ' + buf.duration.toFixed(1) + ' s ' + buf.numberOfChannels + 'ch ' + buf.sampleRate + ' Hz');
        done(buf);
      }).catch(function (e) { console.warn('FFAudio: failed ' + url + ' ' + e); });
    };
    A.start = function (i, url, vol, fade) {
      var c = A.ch[i];
      A.decode(url, function (buf) {
        if (!A.want[i] || A.want[i].url !== url) return;          // the stage changed while decoding
        if (c.cur && c.cur.url === url) return;
        var t = A.ctx.currentTime, f = Math.max(0.05, fade);
        if (c.cur) { var o = c.cur; o.g.gain.cancelScheduledValues(t); o.g.gain.setValueAtTime(o.g.gain.value, t); o.g.gain.linearRampToValueAtTime(0, t + f); try { o.src.stop(t + f + 0.05); } catch (e) {} }
        var src = A.ctx.createBufferSource(); src.buffer = buf; src.loop = true; src.loopStart = 0; src.loopEnd = buf.duration;
        var g = A.ctx.createGain(); g.gain.setValueAtTime(0, t); g.gain.linearRampToValueAtTime(vol, t + f);
        src.connect(g); g.connect(c.bus); src.start(t);
        c.cur = { src: src, g: g, url: url, vol: vol };
        console.log('FFAudio: playing ' + url + ' on ' + (i === 0 ? 'music' : 'ambience') + ' (ctx ' + A.ctx.state + ')');
      });
    };
  },

  // chan 0 music / 1 ambience; empty url = fade out
  FFAudio_Play: function (chan, urlPtr, vol, fade) {
    var A = window.FFA; if (!A) return;
    var url = UTF8ToString(urlPtr);
    if (!url) {
      A.want[chan] = null;
      if (A.ctx && A.ch[chan] && A.ch[chan].cur) { var o = A.ch[chan].cur, t = A.ctx.currentTime; o.g.gain.cancelScheduledValues(t); o.g.gain.setValueAtTime(o.g.gain.value, t); o.g.gain.linearRampToValueAtTime(0, t + fade); try { o.src.stop(t + fade + 0.05); } catch (e) {} A.ch[chan].cur = null; }
      return;
    }
    A.want[chan] = { url: url, vol: vol, fade: fade };
    if (!A.ctx) { try { A.make(); } catch (e) { return; } }
    if (A.ch[chan].cur && A.ch[chan].cur.url === url) {     // same bed: just retarget the volume
      var cur = A.ch[chan].cur, tt = A.ctx.currentTime; cur.g.gain.cancelScheduledValues(tt); cur.g.gain.setValueAtTime(cur.g.gain.value, tt); cur.g.gain.linearRampToValueAtTime(vol, tt + 0.5); cur.vol = vol;
      return;
    }
    A.start(chan, url, vol, fade);
  },

  FFAudio_Master: function (v) {
    var A = window.FFA; if (!A) return;
    A.vol = v;
    if (A.master) { var t = A.ctx.currentTime; A.master.gain.cancelScheduledValues(t); A.master.gain.setValueAtTime(A.master.gain.value, t); A.master.gain.linearRampToValueAtTime(v, t + 0.15); }
  },

  // music low-pass (underwater muffle): hz >= 20000 = open
  FFAudio_Lowpass: function (hz) {
    var A = window.FFA; if (!A || !A.lp) return;
    var t = A.ctx.currentTime; A.lp.frequency.cancelScheduledValues(t); A.lp.frequency.setValueAtTime(A.lp.frequency.value, t); A.lp.frequency.exponentialRampToValueAtTime(Math.max(200, hz), t + 0.6);
  }
});
