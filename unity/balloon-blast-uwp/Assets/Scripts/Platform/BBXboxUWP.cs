using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

// lbuwp (Oct 20, 2026): Balloon Blast Airsoft (Unity 2022.3) UWP / Xbox One S Dev Mode only - the same Xbox layer as Four Froggies ff-uwp-ffu67x
// (unity/four-froggies-uwp FFXboxUWP.cs) without the Four Froggies specifics. Copied into the project by
// .github/workflows/unity-balloon-blast-uwp.yml from unity/balloon-blast-uwp/, so the WebGL build never contains it. Everything below compiles to
// nothing outside a UWP player (UNITY_WSA && !UNITY_EDITOR).
//   * One S profile:  1280x720 render (the console upscales),
//     30 fps vsync cap, MSAA off, HDR off, LBPost off, one short hard shadow cascade, half-resolution textures, lower LOD bias.
//   * Sound: the audio output is re-opened (AudioSettings.Reset, Xbox-native 48 kHz stereo) at boot and again once the
//     window is active, looping sounds are restarted after any reset / device change / resume, a music watchdog restarts
//     the game's music / ambience loops if Unity stopped them, the sound is unlocked without waiting for a first press,
//     and a diagnostics panel shows what the mixer is doing (hold View + Menu 2 s; D-pad Right there tries the next output mode).
//   * FPS counter: hold View 2 s (cycles: FPS at 30 cap -> FPS at 60 -> off). A "BBXBOX perf" log line every 10 s.
//   * B = back: the B button raises the system Back request on Xbox; left unhandled, the shell takes the app back to Home.
//     We mark it handled so B stays the game's own back / leave button (Input System still sees it as buttonEast).
public static class BBXboxUWP
{
    public const string BuildTag = "bb-uwp-1";
    public const int TargetW = 1280, TargetH = 720;

#if UNITY_WSA && !UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        try
        {
#if UNITY_2022_2_OR_NEWER
            QualitySettings.globalTextureMipmapLimit = 1;   // half-resolution textures (mipmapped ones)
#else
            QualitySettings.masterTextureLimit = 1;
#endif
        }
        catch (System.Exception e) { Debug.LogWarning("BBXBOX: texture limit " + e.Message); }
        QualitySettings.antiAliasing = 0;
        QualitySettings.vSyncCount = 2;   // 30 fps on a 60 Hz TV
        var go = new GameObject("BBXboxUWP");
        Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideInHierarchy;
        go.AddComponent<BBXboxUWPRunner>();
        BBXboxAudio.Boot(go);
        Debug.Log("BBXBOX: UWP player " + BuildTag + " " + SystemInfo.deviceModel + " / " + SystemInfo.operatingSystem + " / " + SystemInfo.graphicsDeviceName
                  + " " + SystemInfo.graphicsDeviceVersion + " mem " + SystemInfo.systemMemorySize + " MB gfx mem " + SystemInfo.graphicsMemorySize + " MB screen "
                  + Screen.width + "x" + Screen.height + " cpu " + SystemInfo.processorCount + " | profile " + "generic (no game tier switch)");
#if ENABLE_WINMD_SUPPORT
        UnityEngine.WSA.Application.InvokeOnUIThread(() =>
        {
            try
            {
                Windows.UI.Core.SystemNavigationManager.GetForCurrentView().BackRequested += (sender, args) => { args.Handled = true; };
                Debug.Log("BBXBOX: Back request handled in-game (B stays the game's back button)");
            }
            catch (System.Exception e) { Debug.LogWarning("BBXBOX: back handler " + e.Message); }
        }, false);
#endif
    }
#endif
}

// ---------------------------------------------------------------- performance profile + FPS counter + gestures
public class BBXboxUWPRunner : MonoBehaviour
{
    public static int FpsMode;            // 0 off (30 cap), 1 FPS shown (30 cap), 2 FPS shown (60, vsync 1)
    float t, enforceT, logT = 10f, resTries, toastT = -1f;
    float fAcc, fWorst; int fN; float fps = 0f, ms = 0f, worst = 0f;
    float viewHeld, comboHeld; bool viewFired, comboFired;
    GUIStyle style;
    bool resLogged;

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        t += dt;
        fAcc += dt; fN++; fWorst = Mathf.Max(fWorst, dt);
        if (fAcc >= 1f) { fps = fN / fAcc; ms = 1000f * fAcc / fN; worst = 1000f * fWorst; fAcc = 0f; fN = 0; fWorst = 0f; }

        Gestures(dt);

        enforceT -= dt;
        if (enforceT <= 0f) { enforceT = 0.5f; Enforce(); }

        logT -= dt;
        if (logT <= 0f)
        {
            logT = 10f;
            Debug.Log("BBXBOX perf: " + fps.ToString("0.0") + " fps " + ms.ToString("0.0") + " ms (worst " + worst.ToString("0") + " ms) " + Status());
        }
        if (toastT < 0f && (Look.Views > 0 || t > 6f)) toastT = 14f;
        if (toastT > 0f) toastT -= dt;
    }

    public static string Status()
    {
        return "res " + Screen.width + "x" + Screen.height + " vsync " + QualitySettings.vSyncCount + " fpsTarget " + Application.targetFrameRate + " " + "look " + (Look.Mobile ? "mobile" : "desktop")
             + " views " + Look.Views + " aa " + QualitySettings.antiAliasing + " shadows " + QualitySettings.shadows + " " + QualitySettings.shadowDistance.ToString("0") + "m x" + QualitySettings.shadowCascades
             + " lod " + QualitySettings.lodBias.ToString("0.0") + " mem " + (System.GC.GetTotalMemory(false) >> 20) + " MB managed";
    }

    void Enforce()
    {
        if (Look.Views <= 0 && t < 6f) return;   // wait for the game's first camera setup (Lambo's lobby never sets Views)
        if (QualitySettings.antiAliasing != 0) QualitySettings.antiAliasing = 0;
        if (QualitySettings.shadows != ShadowQuality.Disable)
        {
            if (QualitySettings.shadows != ShadowQuality.HardOnly) QualitySettings.shadows = ShadowQuality.HardOnly;
            if (QualitySettings.shadowResolution != ShadowResolution.Low) QualitySettings.shadowResolution = ShadowResolution.Low;
            if (QualitySettings.shadowCascades != 1) QualitySettings.shadowCascades = 1;
            if (QualitySettings.shadowDistance > 20f) QualitySettings.shadowDistance = 20f;
        }
        if (QualitySettings.lodBias > 0.6f) QualitySettings.lodBias = 0.6f;
        if (QualitySettings.pixelLightCount > 1) QualitySettings.pixelLightCount = 1;
        if (QualitySettings.realtimeReflectionProbes) QualitySettings.realtimeReflectionProbes = false;
        foreach (Camera c in Camera.allCameras)
        {
            if (c == null) continue;
            var lp = c.GetComponent<LBPost>(); if (lp != null && lp.enabled) lp.enabled = false;
            if (c.allowHDR) c.allowHDR = false;
            if (c.allowMSAA) c.allowMSAA = false;
        }
        int vs = FpsMode == 2 ? 1 : 2;
        if (QualitySettings.vSyncCount != vs) QualitySettings.vSyncCount = vs;
        if (Application.targetFrameRate != -1) Application.targetFrameRate = -1;   // the game asks for 60 with vsync off; vsync paces it here
        if (Screen.width > BBXboxUWP.TargetW + 8 && resTries < 4f)
        {
            resTries += 1f;
            Debug.Log("BBXBOX: render " + Screen.width + "x" + Screen.height + " -> " + BBXboxUWP.TargetW + "x" + BBXboxUWP.TargetH);
            Screen.SetResolution(BBXboxUWP.TargetW, BBXboxUWP.TargetH, FullScreenMode.FullScreenWindow);
        }
        else if (!resLogged && t > 8f) { resLogged = true; Debug.Log("BBXBOX: profile on - " + Status()); }
    }

    void Gestures(float dt)
    {
        bool view = false, combo = false;
        foreach (Gamepad p in Gamepad.all)
        {
            if (p == null) continue;
            if (p.selectButton.isPressed && p.startButton.isPressed) combo = true;
            else if (p.selectButton.isPressed) view = true;
        }
        if (combo) { comboHeld += dt; viewHeld = 0f; viewFired = true; }
        else { comboHeld = 0f; comboFired = false; }
        if (view && !combo) viewHeld += dt;
        else if (!combo) { viewHeld = 0f; viewFired = false; }

        if (comboHeld >= 2f && !comboFired) { comboFired = true; BBXboxAudio.TogglePanel(); }
        if (viewHeld >= 2f && !viewFired)
        {
            viewFired = true;
            FpsMode = (FpsMode + 1) % 3;
            Debug.Log("BBXBOX: FPS counter " + (FpsMode == 0 ? "off (30 fps cap)" : FpsMode == 1 ? "on (30 fps cap)" : "on (60 fps, vsync 1)"));
            enforceT = 0f;
        }
    }

    void OnGUI()
    {
        bool toast = toastT > 0f;
        if (FpsMode == 0 && !toast && !BBXboxAudio.PanelOpen) return;
        float k = Mathf.Max(1f, Screen.height / 720f);
        if (style == null) { style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, wordWrap = true }; style.normal.textColor = Color.white; }
        style.fontSize = Mathf.RoundToInt(15 * k);
        float m = Screen.height * 0.05f;   // inside the TV title-safe area
        if (FpsMode > 0)
        {
            string s = fps.ToString("0") + " fps  " + ms.ToString("0.0") + " ms  (worst " + worst.ToString("0") + " ms)\n"
                     + (FpsMode == 1 ? "cap 30 fps" : "cap 60 fps") + "   hold View 2 s: " + (FpsMode == 1 ? "try 60 fps" : "hide") + "\n" + Status();
            GUI.Box(new Rect(m, m, 560f * k, 128f * k), s, style);
        }
        else if (toast)
        {
            GUI.Box(new Rect(m, Screen.height - m - 52f * k, 600f * k, 52f * k),
                    "Xbox build " + BBXboxUWP.BuildTag + "  -  hold View 2 s: FPS counter\nhold View + Menu 2 s: sound check", style);
        }
        if (BBXboxAudio.PanelOpen) BBXboxAudio.DrawPanel(style, k, m);
    }
}

// ---------------------------------------------------------------- sound: re-open the output, restart loops, watchdog, diagnostics
public class BBXboxAudio : MonoBehaviour
{
    struct Opt { public string name; public AudioSpeakerMode mode; public int rate, dsp; }
    static readonly Opt[] Opts =
    {
        new Opt { name = "stereo 48 kHz", mode = AudioSpeakerMode.Stereo, rate = 48000, dsp = 1024 },
        new Opt { name = "7.1 48 kHz", mode = AudioSpeakerMode.Mode7point1, rate = 48000, dsp = 1024 },
        new Opt { name = "5.1 48 kHz", mode = AudioSpeakerMode.Mode5point1, rate = 48000, dsp = 1024 },
        new Opt { name = "stereo 44.1 kHz", mode = AudioSpeakerMode.Stereo, rate = 44100, dsp = 1024 },
        new Opt { name = "stereo 48 kHz big buffer", mode = AudioSpeakerMode.Stereo, rate = 48000, dsp = 2048 },
    };
    static BBXboxAudio I;
    public static bool PanelOpen;
    static int opt, resets, cfgChanges, restartedTotal;
    static string lastReset = "-", fmod = "no FMOD complaint since launch", bootCfg = "", unlockInfo = "not yet";
    static bool fmodFail, inReset, pendingDevice, rearmed, unlockTried;
    static float since, snapT, peak, dspRate = -1f;
    static double lastDsp = -1; static float lastDspReal;
    static readonly List<AudioSource> snap = new List<AudioSource>();
    static readonly float[] buf = new float[512];
    AudioSource tone; AudioClip toneClip;
    static float musT;
    static int musRestarts;
    static string musLast = "-";
    const BindingFlags SB = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;

    public static void Boot(GameObject host)
    {
        Application.logMessageReceived += OnLog;
        bootCfg = Describe();
        opt = Mathf.Clamp(PlayerPrefs.GetInt("bbx.audioopt", 0), 0, Opts.Length - 1);
        Debug.Log("BBXBOX AUDIO boot: " + bootCfg + " driver caps " + AudioSettings.driverCapabilities + " saved mode " + Opts[opt].name);
        AudioSettings.OnAudioConfigurationChanged += OnCfgChanged;
        I = host.AddComponent<BBXboxAudio>();
        Apply(opt, "boot");   // before the game creates its sounds
    }

    static string Describe()
    {
        AudioConfiguration c = AudioSettings.GetConfiguration();
        return c.sampleRate + " Hz " + c.speakerMode + " dsp " + c.dspBufferSize + " voices " + c.numRealVoices + "/" + c.numVirtualVoices
             + " out " + AudioSettings.outputSampleRate + " Hz";
    }

    static void OnLog(string msg, string stack, LogType type)
    {
        if (msg == null || msg.StartsWith("BBXBOX")) return;
        if (msg.IndexOf("FMOD", System.StringComparison.OrdinalIgnoreCase) >= 0 || msg.IndexOf("audio device", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            fmod = msg.Length > 140 ? msg.Substring(0, 140) : msg;
            if (msg.IndexOf("failed", System.StringComparison.OrdinalIgnoreCase) >= 0 || msg.IndexOf("no sound", System.StringComparison.OrdinalIgnoreCase) >= 0) fmodFail = true;
        }
    }

    static void OnCfgChanged(bool deviceWasChanged)
    {
        cfgChanges++;
        if (inReset) return;
        Debug.Log("BBXBOX AUDIO configuration changed (device changed " + deviceWasChanged + "): " + Describe());
        pendingDevice = true;   // re-open + restart the loops on the next frame (main thread, outside the callback)
    }

    static void Snapshot(List<AudioSource> into)
    {
        into.Clear();
        foreach (AudioSource s in Object.FindObjectsOfType<AudioSource>())
            if (s != null && s.clip != null && s.isPlaying && s.loop) into.Add(s);
    }

    static int Restart(List<AudioSource> list)
    {
        int n = 0;
        foreach (AudioSource s in list)
            if (s != null && s.clip != null && s.enabled && s.gameObject.activeInHierarchy && !s.isPlaying) { s.Play(); n++; }
        restartedTotal += n;
        return n;
    }

    static void Apply(int idx, string why, bool useSnapshot = false)
    {
        Opt o = Opts[idx];
        var playing = new List<AudioSource>();
        if (useSnapshot) playing.AddRange(snap); else Snapshot(playing);
        AudioConfiguration c = AudioSettings.GetConfiguration();
        c.speakerMode = o.mode; c.sampleRate = o.rate; c.dspBufferSize = o.dsp;
        fmodFail = false;
        inReset = true;
        bool ok = false;
        try { ok = AudioSettings.Reset(c); } catch (System.Exception e) { Debug.LogWarning("BBXBOX AUDIO reset threw " + e.Message); }
        inReset = false;
        resets++;
        int n = Restart(playing);
        string clips = "";
        foreach (AudioSource s in playing) if (s != null && s.clip != null) clips += " " + s.clip.name + "/" + s.clip.loadState + (s.isPlaying ? "" : "(stopped)");
        lastReset = why + ": " + o.name + (ok ? " ok" : " FAILED") + (fmodFail ? " (FMOD: no audio device)" : "") + ", restarted " + n + " loops" + clips + " -> " + Describe();
        Debug.Log("BBXBOX AUDIO reset " + lastReset);
        lastDsp = -1;
        musT = 0.2f;
    }

    public static void TogglePanel()
    {
        PanelOpen = !PanelOpen;
        Debug.Log("BBXBOX AUDIO panel " + (PanelOpen ? "open" : "closed"));
        if (PanelOpen && I != null) { Apply(opt, "manual check"); I.Tone(); }
    }

    static object SG(string name) { FieldInfo f = typeof(Sfx).GetField(name, SB); return f != null ? f.GetValue(null) : null; }

    // the web build waits for a first press (browsers block audio until a gesture); a console app may play at once
    static void TryUnlock()
    {
        if (Sfx.Unlocked) { if (!unlockTried) { unlockTried = true; unlockInfo = "already unlocked by the game"; } return; }
        if (SG("host") == null) return;   // Sfx.Init has not run yet - unlocking now would skip the game's music start
        unlockTried = true;
        try { Sfx.Unlock(); unlockInfo = "unlocked at launch (" + since.ToString("0.0") + " s)"; }
        catch (System.Exception e) { unlockInfo = "unlock threw " + e.Message; }
        Debug.Log("BBXBOX AUDIO " + unlockInfo);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        since += dt;
        if (!unlockTried || !Sfx.Unlocked) TryUnlock();
        snapT -= dt;
        if (snapT <= 0f) { snapT = 2f; Snapshot(snap); }
        if (pendingDevice) { pendingDevice = false; Apply(opt, "device change", true); }
        musT -= dt;
        if (musT <= 0f) { musT = 1f; MusicWatch(); }
        // once the window is active: open the output again (the console's audio endpoint may not exist at engine start)
        if (!rearmed && since > 4f && Application.isFocused && (Look.Views > 0 || since > 6f))
        {
            rearmed = true;
            Apply(opt, "re-arm after window active");
            Debug.Log("BBXBOX AUDIO status: " + Line());
        }
        double d = AudioSettings.dspTime; float now = Time.realtimeSinceStartup;
        if (lastDsp < 0) { lastDsp = d; lastDspReal = now; }
        else if (now - lastDspReal >= 1f) { dspRate = (float)((d - lastDsp) / (now - lastDspReal)); lastDsp = d; lastDspReal = now; }
        if (PanelOpen)
        {
            try { AudioListener.GetOutputData(buf, 0); float p = 0f; for (int i = 0; i < buf.Length; i++) p = Mathf.Max(p, Mathf.Abs(buf[i])); peak = Mathf.Lerp(peak, p, 0.3f); } catch { }
            foreach (Gamepad g in Gamepad.all)
                if (g != null && g.dpad.right.wasPressedThisFrame)
                {
                    opt = (opt + 1) % Opts.Length;
                    PlayerPrefs.SetInt("bbx.audioopt", opt); PlayerPrefs.Save();
                    Apply(opt, "D-pad try");
                    Tone();
                    break;
                }
        }
    }

    void OnApplicationFocus(bool f) { if (f) { musT = 0.3f; Debug.Log("BBXBOX AUDIO focus regained - music check"); } }
    void OnApplicationPause(bool p) { if (!p) { musT = 0.3f; Debug.Log("BBXBOX AUDIO resumed - music check"); } }

    static void Revive(AudioSource s, string what)
    {
        if (s == null || s.clip == null || s.isPlaying || !s.enabled || !s.gameObject.activeInHierarchy) return;
        AudioClip c = s.clip;
        if (c.loadState == AudioDataLoadState.Unloaded || c.loadState == AudioDataLoadState.Failed) c.LoadAudioData();
        s.Play(); musRestarts++;
        musLast = "restarted " + what + " " + c.name + " (" + c.loadState + ", vol " + s.volume.ToString("0.00") + ")";
        Debug.Log("BBXBOX AUDIO music " + musLast);
    }

    // the game's own music / ambience loops: they only call Play when the track changes, so a source Unity stopped
    // (device change, AudioSettings.Reset, resume) would stay silent until then
    static void MusicWatch()
    {
        try
        {
            if (!Sfx.Unlocked) return;
            Revive(SG("musA") as AudioSource, "music");
            Revive(SG("ambSrc") as AudioSource, "ambience");
            Revive(SG("windSrc") as AudioSource, "wind");
        }
        catch (System.Exception e) { musLast = "watchdog error " + e.Message; }
    }

    static string MusicLine()
    {
        string r = "";
        foreach (string n in new[] { "musA", "ambSrc", "windSrc" })
        {
            var s = SG(n) as AudioSource;
            if (s != null && s.clip != null) r += n + " " + s.clip.name + (s.isPlaying ? " playing " : " stopped ") + s.volume.ToString("0.00") + " " + s.clip.loadState + "; ";
        }
        return (r.Length == 0 ? "no music yet; " : r) + "restarts " + musRestarts + " | last " + musLast;
    }

    void Tone()
    {
        if (toneClip == null)
        {
            int rate = 48000, n = rate * 2 / 5;
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float env = Mathf.Min(1f, i / 960f) * Mathf.Min(1f, (n - i) / 2400f);
                float f = i < n / 2 ? 660f : 880f;
                data[i] = 0.45f * env * Mathf.Sin(2f * Mathf.PI * f * i / rate);
            }
            toneClip = AudioClip.Create("x_tone", n, 1, rate, false);
            toneClip.SetData(data, 0);
        }
        if (tone == null)
        {
            tone = gameObject.AddComponent<AudioSource>();
            tone.playOnAwake = false; tone.spatialBlend = 0f; tone.ignoreListenerVolume = true; tone.ignoreListenerPause = true; tone.priority = 0;
        }
        tone.Stop(); tone.clip = toneClip; tone.volume = 1f; tone.Play();
    }

    static string Line()
    {
        int listeners = Object.FindObjectsOfType<AudioListener>().Length;
        int playing = 0, total = 0;
        foreach (AudioSource s in Object.FindObjectsOfType<AudioSource>()) { total++; if (s.isPlaying) playing++; }
        return "out " + Describe() + " | driver " + AudioSettings.driverCapabilities + " | mixer clock " + (dspRate < 0 ? "-" : dspRate.ToString("0.00") + "x")
             + " | listeners " + listeners + " vol " + AudioListener.volume.ToString("0.00") + (AudioListener.pause ? " PAUSED" : "") + " | game SOUND " + Sfx.LevelName
             + " | " + unlockInfo + " | sources playing " + playing + "/" + total + " | focus " + Application.isFocused + " | resets " + resets + " device changes " + cfgChanges + " | FMOD: " + fmod + " | music: " + MusicLine();
    }

    public static void DrawPanel(GUIStyle style, float k, float m)
    {
        string s = "SOUND CHECK (Xbox)  " + BBXboxUWP.BuildTag + "   - a test tone played just now (660 then 880 Hz)\n"
                 + "output now: " + Describe() + "   driver: " + AudioSettings.driverCapabilities + "\n"
                 + "at launch: " + bootCfg + "\n"
                 + "mixer clock " + (dspRate < 0 ? "-" : dspRate.ToString("0.00") + "x") + "   output peak " + peak.ToString("0.000") + "   (peak > 0 while silent = Unity mixes, console gets nothing)\n"
                 + "game SOUND " + Sfx.LevelName + " (" + unlockInfo + ")   listener vol " + AudioListener.volume.ToString("0.00") + (AudioListener.pause ? " PAUSED" : "") + "   focus " + Application.isFocused + "\n"
                 + "last reset: " + lastReset + "\n"
                 + "resets " + resets + "   device changes " + cfgChanges + "   loops restarted " + restartedTotal + "\n"
                 + "FMOD: " + fmod + "\n"
                 + "music: " + MusicLine() + "\n"
                 + "D-pad Right = try next output mode (" + Opts[(opt + 1) % Opts.Length].name + ") - kept for next launch\n"
                 + "hold View + Menu 2 s = close";
        float w = Screen.width - 2f * m, h = 290f * k;
        GUI.Box(new Rect(m, Screen.height - m - h, w, h), s, style);
    }
}
