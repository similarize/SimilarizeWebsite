using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

// ffu73 (Oct 22, 2026): FOUR FROGGIES 6.6 (Unity 6000.6.5f1 + URP) UWP / Xbox One S Dev Mode, adapted from the 2022.3 overlay
// (ffuwp + ffu67x sound / One S profile + the Lambo / Balloon Blast backoff + ready fixes). Copied into unity/four-froggies-u6
// on the runner by .github/workflows/unity-four-froggies-u6-uwp.yml from unity/four-froggies-u6-uwp/, so the 6.6 WebGL build
// (and its Xbox Edge lite path, 512 MB cap, xbox-compat tag) never contains it.
// URP: MSAA 1, HDR off, one 15 m main-light cascade at 1024, camera post-processing off, set every 0.5 s by reflection. Everything below compiles to nothing outside a UWP player (UNITY_WSA && !UNITY_EDITOR).
//   * One S profile: the game's own ?quality=low (Perf60 LOW tier, governor locked - it never steps up) and ?realism=lite
//     (the phone / Tesla tier: LDR, lite grass radius + density) are injected as test params before the game reads them;
//     on top: 1280x720 render (the console upscales), 30 fps vsync cap, MSAA off, post-processing off, one short hard
//     shadow cascade, half-resolution textures, lower LOD bias.
//   * Sound: the audio output is re-opened (AudioSettings.Reset, Xbox-native 48 kHz stereo) at boot and again once the
//     window is active, looping sounds are restarted after any reset / device change, and a diagnostics panel shows what
//     the mixer is doing (hold View + Menu 2 s; D-pad Right there tries the next output mode, each reset plays a test tone).
//   * FPS counter: hold View 2 s (cycles: FPS at 30 cap -> FPS at 60 -> off). A "FFXBOX perf" log line every 10 s.
//   * The B button raises the system Back request on Xbox; left unhandled, the shell takes the app back to Home. We mark
//     it handled so B stays a game button (Input System still sees it as gamepad buttonEast).
public static class FFXboxUWP
{
    public const string BuildTag = "ff-u6-uwp-ffu73b";
    public const int TargetW = 1280, TargetH = 720;
#if UNITY_WSA && !UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        InjectParams();
        try
        {
#if UNITY_2022_2_OR_NEWER
            QualitySettings.globalTextureMipmapLimit = 1;   // half-resolution textures (mipmapped ones)
#else
            QualitySettings.masterTextureLimit = 1;
#endif
        }
        catch (System.Exception e) { Debug.LogWarning("FFXBOX: texture limit " + e.Message); }
        QualitySettings.antiAliasing = 0;
        QualitySettings.vSyncCount = 2;   // 30 fps on a 60 Hz TV
        var go = new GameObject("FFXboxUWP");
        Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideInHierarchy;
        go.AddComponent<FFXboxUWPRunner>();
        FFXboxAudio.Boot(go);
        Debug.Log("FFXBOX: UWP player " + BuildTag + " " + SystemInfo.deviceModel + " / " + SystemInfo.operatingSystem + " / " + SystemInfo.graphicsDeviceName
                  + " " + SystemInfo.graphicsDeviceVersion + " mem " + SystemInfo.systemMemorySize + " MB gfx mem " + SystemInfo.graphicsMemorySize + " MB screen "
                  + Screen.width + "x" + Screen.height + " cpu " + SystemInfo.processorCount);
#if ENABLE_WINMD_SUPPORT
        UnityEngine.WSA.Application.InvokeOnUIThread(() =>
        {
            try
            {
                Windows.UI.Core.SystemNavigationManager.GetForCurrentView().BackRequested += (sender, args) => { args.Handled = true; };
                Debug.Log("FFXBOX: Back request handled in-game (B stays a game button)");
            }
            catch (System.Exception e) { Debug.LogWarning("FFXBOX: back handler " + e.Message); }
        }, false);
#endif
    }

    // the same test params the web build takes from its page URL (FFPlatform.PageUrl); added only when not given already
    static void InjectParams()
    {
        try
        {
            string u = FFPlatform.PageUrl ?? "";   // parses -ffurl first, so real test params are kept
            string add = "";
            if (Ffu33.Url("quality") == null) add += "&quality=low";
            if (Ffu33.Url("realism") == null) add += "&realism=lite";
            if (add.Length == 0) { Debug.Log("FFXBOX: params kept " + u); return; }
            if (u.IndexOf('?') < 0) u = "app://four-froggies-u6/index.html?" + add.Substring(1);
            else u += add;
            FieldInfo f = typeof(FFPlatform).GetField("page", BindingFlags.NonPublic | BindingFlags.Static);
            if (f == null) { Debug.LogWarning("FFXBOX: FFPlatform.page not found - One S params not set"); return; }
            f.SetValue(null, u);
            Debug.Log("FFXBOX: One S params -> " + FFPlatform.PageUrl);
        }
        catch (System.Exception e) { Debug.LogWarning("FFXBOX: params " + e.Message); }
    }
#endif
}

// ---------------------------------------------------------------- performance profile + FPS counter + gestures
public class FFXboxUWPRunner : MonoBehaviour
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
        // frame-time window (1 s)
        fAcc += dt; fN++; fWorst = Mathf.Max(fWorst, dt);
        if (fAcc >= 1f) { fps = fN / fAcc; ms = 1000f * fAcc / fN; worst = 1000f * fWorst; fAcc = 0f; fN = 0; fWorst = 0f; }

        Gestures(dt);

        enforceT -= dt;
        if (enforceT <= 0f) { enforceT = 0.5f; Enforce(); }

        logT -= dt;
        if (logT <= 0f)
        {
            logT = 10f;
            Debug.Log("FFXBOX perf: " + fps.ToString("0.0") + " fps " + ms.ToString("0.0") + " ms (worst " + worst.ToString("0") + " ms) " + Status());
        }
        if (toastT < 0f && Look.Views > 0) toastT = 14f;
        if (toastT > 0f) toastT -= dt;
    }

    string Status()
    {
        return "res " + Screen.width + "x" + Screen.height + " vsync " + QualitySettings.vSyncCount + " tier " + Perf60.TierName + (Perf60.Forced ? " (locked)" : "")
             + " views " + Look.Views + " aa " + QualitySettings.antiAliasing + " shadows " + QualitySettings.shadows + " " + QualitySettings.shadowDistance.ToString("0") + "m x" + QualitySettings.shadowCascades
             + (Grass56.I != null ? " grass " + Grass56.I.DrawnInfo : "") + " culled " + Cull60.Culled + "/" + Cull60.Tracked
             + " realism " + (Realism.On ? (Realism.Lite ? "lite" : "full") : "off") + " urp " + UrpInfo + " gfx " + SystemInfo.graphicsDeviceType + " mem " + (System.GC.GetTotalMemory(false) >> 20) + " MB managed";
    }

    void Enforce()
    {
        if (Look.Views <= 0 && t < 6f) return;   // wait for the first ApplyViews (lobby camera), at most 6 s (LB / BB ready fix)
        UrpLow();
        // Perf60 low tier: locked by ?quality=low; if the param did not take, pin it again (governor may have stepped up)
        if (Perf60.On && !Perf60.Forced && Perf60.Tier != 0) Perf60.SetTier(0, "Xbox One S profile");
        if (QualitySettings.antiAliasing != 0) QualitySettings.antiAliasing = 0;
        if (QualitySettings.shadows != ShadowQuality.Disable)
        {
            if (QualitySettings.shadows != ShadowQuality.HardOnly) QualitySettings.shadows = ShadowQuality.HardOnly;
            if (QualitySettings.shadowResolution != ShadowResolution.Low) QualitySettings.shadowResolution = ShadowResolution.Low;
            if (QualitySettings.shadowCascades != 1) QualitySettings.shadowCascades = 1;
            if (QualitySettings.shadowDistance > 15f) QualitySettings.shadowDistance = 15f;
        }
        if (QualitySettings.lodBias > 0.6f) QualitySettings.lodBias = 0.6f;
        if (QualitySettings.pixelLightCount > 1) QualitySettings.pixelLightCount = 1;
        foreach (Camera c in Camera.allCameras)
        {
            if (c == null) continue;
            var lp = c.GetComponent<LBPost>(); if (lp != null && lp.enabled) lp.enabled = false;
            var rp = c.GetComponent<RealPost>(); if (rp != null && rp.enabled) rp.enabled = false;
            if (c.allowHDR) c.allowHDR = false;
            if (c.allowMSAA) c.allowMSAA = false;
            var ad = c.GetComponent("UniversalAdditionalCameraData");   // URP: no post-processing on the console
            if (ad != null) SetP(ad, "renderPostProcessing", false);
        }
        int vs = FpsMode == 2 ? 1 : 2;
        if (QualitySettings.vSyncCount != vs) QualitySettings.vSyncCount = vs;
        if (Application.targetFrameRate != -1) Application.targetFrameRate = -1;
        // 720p render (the console scales it to the TV); a few tries in case the window is still settling
        if (Screen.width > FFXboxUWP.TargetW + 8 && resTries < 4f)
        {
            resTries += 1f;
            Debug.Log("FFXBOX: render " + Screen.width + "x" + Screen.height + " -> " + FFXboxUWP.TargetW + "x" + FFXboxUWP.TargetH);
            Screen.SetResolution(FFXboxUWP.TargetW, FFXboxUWP.TargetH, FullScreenMode.FullScreenWindow);
        }
        else if (!resLogged && t > 8f) { resLogged = true; Debug.Log("FFXBOX: profile on - " + Status()); }
    }

    // URP asset knobs by reflection (some setters are internal in URP 17; a missing one only logs once)
    static readonly HashSet<string> warned = new HashSet<string>();
    static void SetP(object o, string name, object v)
    {
        if (o == null) return;
        try
        {
            PropertyInfo p = o.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo set = p != null ? p.GetSetMethod(true) : null;
            if (set == null) { FieldInfo f = o.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance); if (f != null) { if (!Equals(f.GetValue(o), v)) f.SetValue(o, v); return; } if (warned.Add(name)) Debug.LogWarning("FFXBOX: URP " + name + " not settable"); return; }
            if (!Equals(p.GetValue(o, null), v)) set.Invoke(o, new[] { v });
        }
        catch (System.Exception e) { if (warned.Add(name)) Debug.LogWarning("FFXBOX: URP " + name + " " + e.Message); }
    }
    static object GetP(object o, string name) { try { var p = o.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance); return p != null ? p.GetValue(o, null) : null; } catch { return null; } }
    public static string UrpInfo = "-";
    static void UrpLow()
    {
        object a = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        if (a == null) { UrpInfo = "no URP asset"; return; }
        SetP(a, "msaaSampleCount", 1);
        SetP(a, "supportsHDR", false);
        object sd = GetP(a, "shadowDistance"); if (sd is float && (float)sd > 15f) SetP(a, "shadowDistance", 15f);
        SetP(a, "shadowCascadeCount", 1);
        object res = GetP(a, "mainLightShadowmapResolution"); if (res is int && (int)res > 1024) SetP(a, "mainLightShadowmapResolution", 1024);
        UrpInfo = a.GetType().Name + " msaa " + GetP(a, "msaaSampleCount") + " hdr " + GetP(a, "supportsHDR") + " shadow " + GetP(a, "shadowDistance") + "m x" + GetP(a, "shadowCascadeCount") + " @" + GetP(a, "mainLightShadowmapResolution") + " scale " + GetP(a, "renderScale");
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
        if (combo) { comboHeld += dt; viewHeld = 0f; viewFired = true; }   // a combo never counts as a View hold
        else { comboHeld = 0f; comboFired = false; }
        if (view && !combo) viewHeld += dt;
        else if (!combo) { viewHeld = 0f; viewFired = false; }

        if (comboHeld >= 2f && !comboFired) { comboFired = true; FFXboxAudio.TogglePanel(); }
        if (viewHeld >= 2f && !viewFired)
        {
            viewFired = true;
            FpsMode = (FpsMode + 1) % 3;
            Debug.Log("FFXBOX: FPS counter " + (FpsMode == 0 ? "off (30 fps cap)" : FpsMode == 1 ? "on (30 fps cap)" : "on (60 fps, vsync 1)"));
            enforceT = 0f;
        }
    }

    void OnGUI()
    {
        bool toast = toastT > 0f;
        if (FpsMode == 0 && !toast && !FFXboxAudio.PanelOpen) return;
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
                    "Four Froggies 6.6 Xbox build " + FFXboxUWP.BuildTag + "  -  hold View 2 s: FPS counter\nhold View + Menu 2 s: sound check", style);
        }
        if (FFXboxAudio.PanelOpen) FFXboxAudio.DrawPanel(style, k, m);
    }
}

// ---------------------------------------------------------------- sound: re-open the output, restart loops, diagnostics
public class FFXboxAudio : MonoBehaviour
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
    static FFXboxAudio I;
    public static bool PanelOpen;
    static int opt, resets, cfgChanges, restartedTotal;
    static string lastReset = "-", fmod = "no FMOD complaint since launch", bootCfg = "";
    static bool fmodFail, inReset, pendingDevice, rearmed;
    static float since, snapT, peak, dspRate = -1f;
    static double lastDsp = -1; static float lastDspReal;
    static readonly List<AudioSource> snap = new List<AudioSource>();
    static readonly float[] buf = new float[512];
    AudioSource tone; AudioClip toneClip;
    // music watchdog (ffu67x): FFLocalMusic only calls Play when the track CHANGES; a source Unity stopped (device change,
    // AudioSettings.Reset, resume) or a clip that failed to load stays silent until the next world -> restart / reload here
    static float musT;
    static int musRestarts, musReloads, musRequests;
    static string musLast = "-";
    static readonly Dictionary<string, int> reqN = new Dictionary<string, int>();
    static FieldInfo fSrc, fGoal, fPlaying, fSide, fClips;
    static readonly Dictionary<AudioSource, int> miss = new Dictionary<AudioSource, int>();
    static readonly Dictionary<AudioSource, float> nextTry = new Dictionary<AudioSource, float>();

    public static void Boot(GameObject host)
    {
        Application.logMessageReceived += OnLog;
        bootCfg = Describe();
        opt = Mathf.Clamp(PlayerPrefs.GetInt("ffx.audioopt", 0), 0, Opts.Length - 1);
        Debug.Log("FFXBOX AUDIO boot: " + bootCfg + " driver caps " + AudioSettings.driverCapabilities + " saved mode " + Opts[opt].name);
        AudioSettings.OnAudioConfigurationChanged += OnCfgChanged;
        I = host.AddComponent<FFXboxAudio>();
        Apply(opt, "boot");   // before any sound plays
    }

    static string Describe()
    {
        AudioConfiguration c = AudioSettings.GetConfiguration();
        return c.sampleRate + " Hz " + c.speakerMode + " dsp " + c.dspBufferSize + " voices " + c.numRealVoices + "/" + c.numVirtualVoices
             + " out " + AudioSettings.outputSampleRate + " Hz";
    }

    static void OnLog(string msg, string stack, LogType type)
    {
        if (msg == null || msg.StartsWith("FFXBOX")) return;
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
        Debug.Log("FFXBOX AUDIO configuration changed (device changed " + deviceWasChanged + "): " + Describe());
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
        try { ok = AudioSettings.Reset(c); } catch (System.Exception e) { Debug.LogWarning("FFXBOX AUDIO reset threw " + e.Message); }
        inReset = false;
        resets++;
        int n = Restart(playing);
        lastReset = why + ": " + o.name + (ok ? " ok" : " FAILED") + (fmodFail ? " (FMOD: no audio device)" : "") + ", restarted " + n + " loops -> " + Describe();
        Debug.Log("FFXBOX AUDIO reset " + lastReset);
        lastDsp = -1;
    }

    public static void TogglePanel()
    {
        PanelOpen = !PanelOpen;
        Debug.Log("FFXBOX AUDIO panel " + (PanelOpen ? "open" : "closed"));
        if (PanelOpen && I != null) { Apply(opt, "manual check"); I.Tone(); }
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        since += dt;
        // a periodic list of the looping sounds, so a device change (which stops every source) can restart them
        snapT -= dt;
        if (snapT <= 0f) { snapT = 2f; Snapshot(snap); }
        if (pendingDevice) { pendingDevice = false; Apply(opt, "device change", true); musT = 0f; }
        musT -= dt;
        if (musT <= 0f) { musT = 1f; MusicWatch(); }
        // once the window is active: open the output again (the console's audio endpoint may not exist at engine start)
        if (!rearmed && since > 4f && Application.isFocused && (Look.Views > 0 || since > 6f))   // ready = views up OR 6 s (LB / BB fix)
        {
            rearmed = true;
            Apply(opt, "re-arm after window active");
            Debug.Log("FFXBOX AUDIO status: " + Line());
        }
        // mixer clock + output level
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
                    PlayerPrefs.SetInt("ffx.audioopt", opt); PlayerPrefs.Save();
                    Apply(opt, "D-pad try");
                    Tone();
                    break;
                }
        }
    }

    void OnApplicationFocus(bool f) { if (f) { musT = 0.3f; Debug.Log("FFXBOX AUDIO focus regained - music check"); } }
    void OnApplicationPause(bool p) { if (!p) { musT = 0.3f; Debug.Log("FFXBOX AUDIO resumed - music check"); } }

    static void MusicWatch()
    {
        try
        {
            var lm = Object.FindObjectOfType<FFLocalMusic>();
            if (lm == null) return;
            System.Type t = typeof(FFLocalMusic);
            const BindingFlags B = BindingFlags.NonPublic | BindingFlags.Instance;
            if (fSrc == null) { fSrc = t.GetField("src", B); fGoal = t.GetField("goal", B); fPlaying = t.GetField("playing", B); fSide = t.GetField("side", B); fClips = t.GetField("clips", B); }
            if (fSrc == null || fGoal == null || fPlaying == null || fSide == null) { musLast = "watchdog: FFLocalMusic fields not found"; return; }
            var src = (AudioSource[])fSrc.GetValue(lm); var goal = (float[])fGoal.GetValue(lm);
            var playing = (string[])fPlaying.GetValue(lm); var side = (int[])fSide.GetValue(lm);
            for (int ch = 0; ch < 2; ch++)
            {
                string want = playing[ch];
                if (string.IsNullOrEmpty(want)) continue;
                int i = ch * 2 + side[ch];
                AudioSource s = src[i];
                if (s == null) continue;
                if (s.clip == null || s.clip.name != want)
                {
                    // the clip never loaded (cached null) -> forget it and ask FFLocalMusic again (3 tries per track)
                    int rq; reqN.TryGetValue(want, out rq); if (rq >= 3) continue; reqN[want] = rq + 1;
                    var clips = fClips != null ? fClips.GetValue(lm) as Dictionary<string, AudioClip> : null;
                    if (clips != null) clips.Remove(want);
                    playing[ch] = "";
                    FFLocalMusic.Play(ch, "audio/" + want + ".ogg", ch == 0 ? 0.4f : 0.3f, 0.5f);
                    musRequests++;
                    musLast = "re-requested " + want + " (clip " + (s.clip != null ? s.clip.name : "missing") + ")";
                    Debug.Log("FFXBOX AUDIO music " + musLast);
                    continue;
                }
                if (s.isPlaying) miss[s] = 0;
                if (goal[i] > 0.001f && !s.isPlaying)
                {
                    AudioClip c = s.clip;
                    if (c.loadState == AudioDataLoadState.Failed || c.loadState == AudioDataLoadState.Unloaded)
                    {
                        string st0 = c.loadState.ToString();
                        c.UnloadAudioData(); bool ok = c.LoadAudioData(); musReloads++;
                        musLast = "reload " + c.name + " (" + st0 + ") -> " + ok;
                        Debug.Log("FFXBOX AUDIO music " + musLast);
                        if (c.loadState == AudioDataLoadState.Loading) continue;   // play next tick, when it is in
                    }
                    // backoff (LB / BB fix): restarts in a row that don't take -> one try per 15 s after 3, log only every 10th
                    // (a box with no audio output never reports isPlaying)
                    int m; miss.TryGetValue(s, out m);
                    float nt; if (nextTry.TryGetValue(s, out nt) && since < nt) continue;
                    s.Play(); musRestarts++;
                    m++; miss[s] = m; nextTry[s] = m >= 3 ? since + 15f : 0f;
                    musLast = "restarted " + c.name + " (was stopped, goal " + goal[i].ToString("0.00") + ", " + c.loadState + ", try " + m + (m >= 3 ? ", backing off 15 s" : "") + ")";
                    if (m <= 3 || m % 10 == 0) Debug.Log("FFXBOX AUDIO music " + musLast);
                }
            }
        }
        catch (System.Exception e) { musLast = "watchdog error " + e.Message; }
    }

    static string MusicLine()
    {
        string r = "";
        var lm = Object.FindObjectOfType<FFLocalMusic>();
        if (lm == null) return "FFLocalMusic missing";
        foreach (AudioSource s in lm.GetComponents<AudioSource>())
            if (s.clip != null) r += s.clip.name + (s.isPlaying ? " playing " : " stopped ") + s.volume.ToString("0.00") + " " + s.clip.loadState + "; ";
        return (r.Length == 0 ? "no track yet; " : r) + "restarts " + musRestarts + " reloads " + musReloads + " re-requests " + musRequests + " | last " + musLast;
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
            toneClip = AudioClip.Create("ffx_tone", n, 1, rate, false);
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
             + " | sources playing " + playing + "/" + total + " | focus " + Application.isFocused + " | resets " + resets + " device changes " + cfgChanges + " | FMOD: " + fmod + " | music: " + MusicLine();
    }

    public static void DrawPanel(GUIStyle style, float k, float m)
    {
        string s = "SOUND CHECK (Xbox)  " + FFXboxUWP.BuildTag + "   - a test tone played just now (660 then 880 Hz)\n"
                 + "output now: " + Describe() + "   driver: " + AudioSettings.driverCapabilities + "\n"
                 + "at launch: " + bootCfg + "\n"
                 + "mixer clock " + (dspRate < 0 ? "-" : dspRate.ToString("0.00") + "x") + "   output peak " + peak.ToString("0.000") + "   (peak > 0 while silent = Unity mixes, console gets nothing)\n"
                 + "game SOUND " + Sfx.LevelName + "   listener vol " + AudioListener.volume.ToString("0.00") + (AudioListener.pause ? " PAUSED" : "") + "   focus " + Application.isFocused + "\n"
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
