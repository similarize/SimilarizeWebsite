// Four Froggies (ffu13): online Host / Join over PeerJS (free 0.peerjs.com signalling, WebRTC data channels), modelled on
// the three.js 3D version's party.js. The host opens peer id "froggiesu-<CODE>" (4 chars from CODE_CHARS); guests open a
// random "froggiesu-p..." id and connect to it. Star topology: the host relays guest states to the other guests (Net.cs).
// PeerJS itself is loaded on demand by the page (window.FFNetLoadPeer in web/index.html, pinned CDN version), so offline
// play never waits on the network. Unity polls an inbox once per frame (FFNet_Poll), one event per line:
//   open|<CODE>      host room is live          me|<peerId>     guest's own id
//   conn|<peerId>    a guest connected (host) / "conn|host" (guest connected to the host)
//   msg|<from>|<payload>    gone|<peerId>     err|<kind>|<detail>
mergeInto(LibraryManager.library, {
  FFNet_Init: function () {
    if (window.FFN) return;
    var N = window.FFN = { peer: null, role: '', conns: {}, host: null, inbox: [], code: '', tries: 0, timer: 0 };
    var CH = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
    N.makeCode = function () { var s = ''; for (var i = 0; i < 4; i++) s += CH.charAt(Math.floor(Math.random() * CH.length)); return s; };
    N.push = function (s) { N.inbox.push(String(s).replace(/[\r\n]/g, ' ')); };
    N.load = function (cb) {
      if (window.Peer) { cb(null); return; }
      if (typeof window.FFNetLoadPeer === 'function') { window.FFNetLoadPeer(cb); return; }
      var s = document.createElement('script');
      s.src = 'https://cdn.jsdelivr.net/npm/peerjs@1.5.4/dist/peerjs.min.js';
      s.onload = function () { cb(null); }; s.onerror = function () { cb('load'); };
      document.head.appendChild(s);
    };
    N.stop = function () {
      if (N.timer) { clearTimeout(N.timer); N.timer = 0; }
      var p = N.peer;
      N.peer = null; N.conns = {}; N.host = null; N.role = '';   // first, so close events of the old session stay quiet
      try { if (p) p.destroy(); } catch (e) { }
      N.inbox.length = 0;
    };
    N.wireGuest = function (c) {   // host side: one connected guest
      c.on('open', function () { N.conns[c.peer] = c; N.push('conn|' + c.peer); });
      c.on('data', function (d) { N.push('msg|' + c.peer + '|' + d); });
      c.on('close', function () { if (N.conns[c.peer] === c) { delete N.conns[c.peer]; N.push('gone|' + c.peer); } });
      c.on('error', function () { });
    };
    N.startHost = function (code) {
      N.stop();
      N.role = 'host'; N.code = code;
      var p;
      try { p = N.peer = new window.Peer('froggiesu-' + code, { debug: 0 }); } catch (e) { N.push('err|create|' + e); return; }
      p.on('open', function () { if (N.peer !== p) return; N.tries = 0; N.push('open|' + code); console.log('FFNet: hosting room ' + code); });
      p.on('connection', function (c) { N.wireGuest(c); });
      p.on('error', function (e) {
        if (N.peer !== p) return;
        var t = (e && e.type) || 'peer';
        if (t === 'unavailable-id' && N.tries < 4) { N.tries++; N.startHost(N.makeCode()); return; }   // code taken: new code
        N.push('err|' + t + '|' + (e && e.message ? e.message : ''));
      });
      p.on('disconnected', function () { try { if (N.peer === p && !p.destroyed) p.reconnect(); } catch (e) { } });
    };
    N.startJoin = function (code) {
      N.stop();
      N.role = 'guest'; N.code = code;
      var id = 'froggiesu-p' + Math.random().toString(36).slice(2, 10);
      var p;
      try { p = N.peer = new window.Peer(id, { debug: 0 }); } catch (e) { N.push('err|create|' + e); return; }
      N.timer = setTimeout(function () { if (N.peer === p && !N.host) N.push('err|timeout|'); }, 15000);
      p.on('open', function (myId) {
        if (N.peer !== p) return;
        N.push('me|' + myId);
        var c = p.connect('froggiesu-' + code, { reliable: true });
        c.on('open', function () { N.host = c; if (N.timer) { clearTimeout(N.timer); N.timer = 0; } N.push('conn|host'); console.log('FFNet: joined room ' + code); });
        c.on('data', function (d) { if (N.host === c) N.push('msg|host|' + d); });
        c.on('close', function () { if (N.host === c) { N.host = null; N.push('gone|host'); } });
        c.on('error', function () { });
      });
      p.on('error', function (e) {
        if (N.peer !== p) return;
        var t = (e && e.type) || 'peer';
        N.push('err|' + (t === 'peer-unavailable' ? 'noroom' : t) + '|' + (e && e.message ? e.message : ''));
      });
    };
    window.addEventListener('pagehide', function () { N.stop(); });
    window.addEventListener('beforeunload', function () { N.stop(); });
  },

  FFNet_Host: function (codePtr) {
    var N = window.FFN; var code = UTF8ToString(codePtr);
    N.load(function (err) { if (err) { N.push('err|load|PeerJS'); return; } N.startHost(code || N.makeCode()); });
  },

  FFNet_Join: function (codePtr) {
    var N = window.FFN; var code = UTF8ToString(codePtr);
    N.load(function (err) { if (err) { N.push('err|load|PeerJS'); return; } N.startJoin(code); });
  },

  // to: "host" (guest), a guest peer id, "*" = every guest, "*!<id>" = every guest except that one
  FFNet_Send: function (toPtr, msgPtr) {
    var N = window.FFN; if (!N) return;
    var to = UTF8ToString(toPtr), msg = UTF8ToString(msgPtr);
    try {
      if (N.role === 'guest') { if (N.host && N.host.open) N.host.send(msg); return; }
      if (to.charAt(0) === '*') {
        var skip = to.length > 2 ? to.substring(2) : '';
        for (var k in N.conns) if (k !== skip && N.conns[k].open) N.conns[k].send(msg);
      } else if (N.conns[to] && N.conns[to].open) N.conns[to].send(msg);
    } catch (e) { }
  },

  FFNet_Drop: function (idPtr) {
    var N = window.FFN; if (!N) return;
    var id = UTF8ToString(idPtr);
    var c = N.conns[id]; if (c) { delete N.conns[id]; try { c.close(); } catch (e) { } }
  },

  FFNet_Leave: function () { if (window.FFN) window.FFN.stop(); },

  FFNet_Poll: function () {
    var N = window.FFN;
    if (!N || N.inbox.length === 0) return 0;
    var s = N.inbox.join('\n'); N.inbox.length = 0;
    var n = lengthBytesUTF8(s) + 1, b = _malloc(n);
    stringToUTF8(s, b, n);
    return b;
  }
});
