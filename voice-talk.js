/* Similarize Talk — Grok Voice Latest via shared FE mint. Capture → packaged text; sparse speak-back. */
(function (root) {
  var MINT_URL = root.SIMILARIZE_VOICE_MINT_URL || "https://fe-voice-mint.ben-e22.workers.dev/v1/ephemeral";
  var WS_URL = "wss://api.x.ai/v1/realtime?model=grok-voice-latest";
  var CREDIT_KEY = "similarize.sessionCredit";

  function PaymentRequiredError(priceCents, message) {
    var err = new Error(message || "payment_required");
    err.name = "PaymentRequiredError";
    err.code = "payment_required";
    err.error = "payment_required";
    err.session_price_cents = Number(priceCents) || 25;
    return err;
  }

  function readSessionCredit(opts) {
    if (opts && opts.sessionCredit) return String(opts.sessionCredit);
    try {
      var stored = root.localStorage && root.localStorage.getItem(CREDIT_KEY);
      if (stored) return String(stored);
    } catch (e) {}
    return "";
  }

  function TalkController(opts) {
    opts = opts || {};
    this.onStatus = opts.onStatus || function () {};
    this.onTranscript = opts.onTranscript || function () {};
    this.onPackaged = opts.onPackaged || function () {};
    this.onPaymentRequired = opts.onPaymentRequired || function () {};
    this.sessionCredit = opts.sessionCredit || "";
    this.ws = null;
    this.audioCtx = null;
    this.mediaStream = null;
    this.processor = null;
    this.playing = [];
    this.nextPlayTime = 0;
    this.listening = false;
  }

  TalkController.prototype.setStatus = function (msg, warn) {
    this.onStatus(msg || "", !!warn);
  };

  TalkController.prototype.stopAudio = function () {
    if (this.audioCtx) {
      try { this.audioCtx.close(); } catch (e) {}
      this.audioCtx = null;
    }
    this.playing = [];
    this.nextPlayTime = 0;
  };

  TalkController.prototype.disconnect = function () {
    this.listening = false;
    if (this.processor) {
      try { this.processor.disconnect(); } catch (e) {}
      this.processor = null;
    }
    if (this.mediaStream) {
      this.mediaStream.getTracks().forEach(function (t) { t.stop(); });
      this.mediaStream = null;
    }
    if (this.ws) {
      try { this.ws.close(); } catch (e) {}
      this.ws = null;
    }
    this.stopAudio();
  };

  TalkController.prototype.mint = async function () {
    var body = { product: "similarize" };
    var credit = readSessionCredit(this);
    if (credit) body.sessionCredit = credit;

    var res = await fetch(MINT_URL, {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: JSON.stringify(body),
    });

    var data = {};
    try { data = await res.json(); } catch (e) { data = {}; }

    if (res.status === 402 || data.error === "payment_required") {
      throw PaymentRequiredError(
        data.session_price_cents || 25,
        data.message || "payment_required"
      );
    }
    if (!res.ok) {
      var err = new Error((data && data.error) || ("mint_failed_" + res.status));
      err.status = res.status;
      err.data = data;
      throw err;
    }
    return data;
  };

  TalkController.prototype.playPcm16 = function (base64) {
    if (!this.audioCtx) {
      this.audioCtx = new (window.AudioContext || window.webkitAudioContext)({ sampleRate: 24000 });
      this.nextPlayTime = this.audioCtx.currentTime;
    }
    var raw = atob(base64);
    var len = raw.length;
    var bytes = new Uint8Array(len);
    for (var i = 0; i < len; i++) bytes[i] = raw.charCodeAt(i);
    var view = new DataView(bytes.buffer);
    var samples = new Float32Array(len / 2);
    for (var s = 0; s < samples.length; s++) {
      samples[s] = view.getInt16(s * 2, true) / 32768;
    }
    var buf = this.audioCtx.createBuffer(1, samples.length, 24000);
    buf.copyToChannel(samples, 0);
    var src = this.audioCtx.createBufferSource();
    src.buffer = buf;
    src.connect(this.audioCtx.destination);
    var start = Math.max(this.audioCtx.currentTime, this.nextPlayTime);
    src.start(start);
    this.nextPlayTime = start + buf.duration;
  };

  TalkController.prototype.startMic = async function (ws) {
    var self = this;
    this.mediaStream = await navigator.mediaDevices.getUserMedia({
      audio: { echoCancellation: true, noiseSuppression: true, channelCount: 1 },
    });
    var ctx = new (window.AudioContext || window.webkitAudioContext)({ sampleRate: 24000 });
    this.audioCtx = this.audioCtx || ctx;
    var source = ctx.createMediaStreamSource(this.mediaStream);
    var processor = ctx.createScriptProcessor(4096, 1, 1);
    this.processor = processor;
    processor.onaudioprocess = function (ev) {
      if (!self.listening || !ws || ws.readyState !== 1) return;
      var input = ev.inputBuffer.getChannelData(0);
      var pcm = new Int16Array(input.length);
      for (var i = 0; i < input.length; i++) {
        var v = Math.max(-1, Math.min(1, input[i]));
        pcm[i] = v < 0 ? v * 0x8000 : v * 0x7fff;
      }
      var bytes = new Uint8Array(pcm.buffer);
      var bin = "";
      for (var j = 0; j < bytes.length; j++) bin += String.fromCharCode(bytes[j]);
      ws.send(JSON.stringify({
        type: "input_audio_buffer.append",
        audio: btoa(bin),
      }));
    };
    source.connect(processor);
    processor.connect(ctx.destination);
  };

  TalkController.prototype.start = async function () {
    var self = this;
    this.disconnect();
    this.setStatus("Connecting Grok Voice…");
    var minted;
    try {
      minted = await this.mint();
    } catch (e) {
      if (e && (e.code === "payment_required" || e.error === "payment_required")) {
        this.setStatus("Talk needs prepaid credit. Typed Similarize stays free.", true);
        this.onPaymentRequired({
          error: "payment_required",
          session_price_cents: e.session_price_cents || 25,
          message: e.message,
        });
        throw e;
      }
      this.setStatus("Talk isn’t live yet — voice mint not reachable.", true);
      throw e;
    }
    var token = minted.value;
    if (!token) {
      this.setStatus("Talk mint returned no token.", true);
      throw new Error("no_token");
    }
    var wsUrl = minted.ws_url || WS_URL;
    // Browser WebSocket auth via subprotocol (never put secrets in the repo)
    var ws = new WebSocket(wsUrl, ["xai-client-secret." + token]);
    this.ws = ws;
    var assistantText = "";

    ws.onopen = async function () {
      self.listening = true;
      self.setStatus("Listening… say what you mean.");
      try {
        await self.startMic(ws);
      } catch (err) {
        self.setStatus("Microphone permission needed.", true);
        self.disconnect();
      }
    };

    ws.onmessage = function (ev) {
      var msg;
      try { msg = JSON.parse(ev.data); } catch (e) { return; }
      var t = msg.type;
      if (t === "response.output_audio.delta" && msg.delta) {
        self.playPcm16(msg.delta);
      } else if (t === "conversation.item.input_audio_transcription.completed" ||
                 t === "conversation.item.input_audio_transcription.updated") {
        var transcript = msg.transcript || (msg.item && msg.item.content && msg.item.content[0] && msg.item.content[0].transcript);
        if (transcript) self.onTranscript(transcript);
      } else if (t === "response.output_text.delta" && msg.delta) {
        assistantText += msg.delta;
      } else if (t === "response.output_audio_transcript.delta" && msg.delta) {
        assistantText += msg.delta;
      } else if (t === "response.done" || t === "response.completed") {
        if (assistantText.trim()) {
          self.onPackaged(assistantText.trim());
          assistantText = "";
        }
        self.setStatus("Tap Talk again if you want to refine.");
      } else if (t === "error") {
        self.setStatus((msg.error && msg.error.message) || "Voice error", true);
      }
    };

    ws.onerror = function () {
      self.setStatus("Voice connection error.", true);
    };
    ws.onclose = function () {
      self.listening = false;
      self.setStatus("");
    };
  };

  TalkController.prototype.toggle = async function () {
    if (this.listening) {
      this.disconnect();
      this.setStatus("");
      return;
    }
    await this.start();
  };

  root.SimilarizeTalk = {
    TalkController: TalkController,
    MINT_URL: MINT_URL,
    PaymentRequiredError: PaymentRequiredError,
    CREDIT_KEY: CREDIT_KEY,
  };
})(window);
