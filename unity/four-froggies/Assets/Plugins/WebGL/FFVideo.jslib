// ffu18 REAL ROOM TV: a plain HTML <video> (no Unity Video module) uploaded into a Unity texture each frame.
// FFVideoOpen(url, vol) starts it (falls back to muted autoplay if the browser blocks sound), FFVideoUpdate(texId)
// copies the current frame into the GL texture behind Texture2D.GetNativeTexturePtr() (flipped to Unity's bottom-up
// rows, previous binding restored), FFVideoTime() = playback position (s) or -1, FFVideoPlay(0/1), FFVideoVolume(v).
mergeInto(LibraryManager.library, {
  FFVideoOpen: function (urlPtr, vol) {
    try {
      var url = UTF8ToString(urlPtr);
      var v = window.FFRRVideo;
      if (!v) {
        v = document.createElement('video');
        v.crossOrigin = 'anonymous'; v.loop = true; v.playsInline = true; v.preload = 'auto';
        v.setAttribute('playsinline', ''); v.setAttribute('webkit-playsinline', '');
        v.style.display = 'none';
        document.body.appendChild(v);
        window.FFRRVideo = v;
        v.src = url;
      }
      v.volume = Math.max(0, Math.min(1, vol)); v.muted = vol <= 0;
      var p = v.play();
      if (p && p.catch) p.catch(function () { v.muted = true; v.play().catch(function () {}); });
      return 1;
    } catch (e) { console.warn('FFVideoOpen ' + e); return 0; }
  },
  FFVideoUpdate: function (texId) {
    var v = window.FFRRVideo;
    if (!v || v.readyState < 2 || !GL.textures[texId]) return 0;
    try {
      var gl = GLctx;
      var prev = gl.getParameter(gl.TEXTURE_BINDING_2D);
      gl.bindTexture(gl.TEXTURE_2D, GL.textures[texId]);
      gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, true);
      gl.texSubImage2D(gl.TEXTURE_2D, 0, 0, 0, gl.RGBA, gl.UNSIGNED_BYTE, v);
      gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false);
      gl.bindTexture(gl.TEXTURE_2D, prev);
      return 1;
    } catch (e) { if (!window.FFRRVideoErr) { window.FFRRVideoErr = 1; console.warn('FFVideoUpdate ' + e); } return 0; }
  },
  FFVideoTime: function () { var v = window.FFRRVideo; return v && v.readyState >= 2 ? v.currentTime : -1; },
  FFVideoPlay: function (on) { var v = window.FFRRVideo; if (!v) return; if (on) { var p = v.play(); if (p && p.catch) p.catch(function () {}); } else v.pause(); },
  FFVideoVolume: function (vol) { var v = window.FFRRVideo; if (v) { v.volume = Math.max(0, Math.min(1, vol)); v.muted = vol <= 0; } }
});
