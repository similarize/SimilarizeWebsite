using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// ffu34 (Windows / console groundwork): the few things that differ between the WebGL arcade build and a native player.
// The WebGL build behaves exactly as before: PageUrl = Application.absoluteURL, assets stream from the page folder
// (web/real, web/realroom, web/audio next to index.html) with their ?v= cache-busters, music through FFAudio.jslib.
// A native player (Windows x64 now, Xbox later) has no page:
//   * PageUrl is built from the command line: FourFroggies.exe -ffurl "?ffdemo=1&ffshot=frog" (the same test params)
//   * the streamed folders ship in <Game>_Data/StreamingAssets/ (copied in by the Windows workflow), read as file:// URLs
//     without query strings
//   * music + ambience come from Resources/FFMusic/*.ogg (copied in only for the Windows build) via FFLocalMusic
//   * the game opens fullscreen at the monitor's native resolution (F11 / Alt+Enter toggle); online play is web-only
public static class FFPlatform
{
#if UNITY_WEBGL && !UNITY_EDITOR
    public static readonly bool WebPlayer = true;
#else
    public static readonly bool WebPlayer = false;
#endif
    // a built native player (not the editor, not WebGL)
    public static bool Native { get { return !WebPlayer && !Application.isEditor; } }
    public static bool OnlineAvailable { get { return !Native; } }

    static string page;
    public static string PageUrl
    {
        get
        {
            if (WebPlayer || Application.isEditor) return Application.absoluteURL ?? "";
            if (page != null) return page;
            page = "";
            try
            {
                string[] a = System.Environment.GetCommandLineArgs();
                for (int i = 0; i + 1 < a.Length; i++)
                    if (a[i] == "-ffurl" || a[i] == "--ffurl")
                    {
                        string q = a[i + 1];
                        int k = q.IndexOf('?');
                        q = k >= 0 ? q.Substring(k) : "?" + q;
                        page = "app://four-froggies/index.html" + q;
                        Debug.Log("FFPlatform: native player, test params " + q);
                    }
            }
            catch (System.Exception e) { Debug.LogWarning("FFPlatform args: " + e.Message); }
            return page;
        }
    }

    public static bool HasArg(string name)
    {
        try { foreach (string s in System.Environment.GetCommandLineArgs()) if (s == name) return true; } catch { }
        return false;
    }

    // base URL (ending in '/') of a streamed asset folder: "real/", "realroom/", "audio/"
    public static string AssetBase(string sub)
    {
        if (Native)
        {
            string p = System.IO.Path.Combine(Application.streamingAssetsPath, sub.TrimEnd('/'));
            try { return new System.Uri(p).AbsoluteUri + "/"; } catch { return "file:///" + p.Replace('\\', '/') + "/"; }
        }
        string u = Application.absoluteURL ?? "";
        int q = u.IndexOfAny(new[] { '?', '#' }); if (q >= 0) u = u.Substring(0, q);
        int s = u.LastIndexOf('/'); if (s >= 0) u = u.Substring(0, s + 1);
        return u + sub;
    }

    // cache-buster query ("?v=r3") for web URLs; file:// URLs in a native player take none
    public static string Bust(string q) { return Native ? "" : q; }

    // ---------------- native display ----------------
    public static void NativeDisplayInit()
    {
        if (!Native) return;
        bool windowed = HasArg("-windowed") || HasArg("-screen-fullscreen") || HasArg("-popupwindow");
        Resolution r = Screen.currentResolution;
        if (!windowed && r.width > 0 && r.height > 0) Screen.SetResolution(r.width, r.height, FullScreenMode.FullScreenWindow);
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = -1;
        Debug.Log("FFPlatform: native display " + (windowed ? "windowed " + Screen.width + "x" + Screen.height : "fullscreen " + r.width + "x" + r.height));
    }

    static float mouseIdle;
    static Vector2 lastMouse;
    // F11 toggles fullscreen (Alt+Enter is built into the Windows player); the cursor hides after 2.5 s without mouse
    // movement so a controller player never sees it over the game
    public static void NativeDisplayTick(float dt)
    {
        if (!Native) return;
        Keyboard k = Keyboard.current;
        if (k != null && k.f11Key.wasPressedThisFrame)
        {
            if (Screen.fullScreenMode == FullScreenMode.Windowed) { Resolution r = Screen.currentResolution; Screen.SetResolution(r.width, r.height, FullScreenMode.FullScreenWindow); }
            else Screen.SetResolution(Mathf.Max(960, Screen.currentResolution.width * 2 / 3), Mathf.Max(540, Screen.currentResolution.height * 2 / 3), FullScreenMode.Windowed);
        }
        Mouse m = Mouse.current;
        Vector2 mp = m != null ? m.position.ReadValue() : lastMouse;
        bool moved = (mp - lastMouse).sqrMagnitude > 4f || (m != null && (m.leftButton.isPressed || m.rightButton.isPressed));
        lastMouse = mp;
        mouseIdle = moved ? 0f : mouseIdle + dt;
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            bool vis = mouseIdle < 2.5f;
            if (Cursor.visible != vis) Cursor.visible = vis;
        }
    }
}

// ffu34: music + ambience for native players (stand-in for Plugins/WebGL/FFAudio.jslib). Two channels (0 music,
// 1 ambience bed), each a pair of looping AudioSources that crossfade. Clips: Resources/FFMusic/<name>.ogg, which only
// exist in the Windows build (the workflow copies web/audio in), so WebGL and the editor simply stay silent here.
// Volume follows AudioListener.volume (the SOUND level), the underwater low-pass is an AudioLowPassFilter.
public class FFLocalMusic : MonoBehaviour
{
    static FFLocalMusic I;
    readonly AudioSource[] src = new AudioSource[4];      // chan * 2 + side
    readonly int[] side = new int[2];
    readonly float[] goal = new float[4], rate = new float[4];
    readonly string[] playing = { "", "" };
    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    AudioLowPassFilter lp;
    float lpGoal = 22000f;

    public static void Init()
    {
        if (I != null) return;
        var g = new GameObject("FFLocalMusic");
        Object.DontDestroyOnLoad(g);
        I = g.AddComponent<FFLocalMusic>();
        for (int i = 0; i < 4; i++)
        {
            var s = g.AddComponent<AudioSource>();
            s.playOnAwake = false; s.loop = true; s.spatialBlend = 0f; s.volume = 0f; s.priority = 0;
            I.src[i] = s;
        }
        I.lp = g.AddComponent<AudioLowPassFilter>();
        I.lp.cutoffFrequency = 22000f;
        Debug.Log("FFLocalMusic: native music player ready");
    }

    AudioClip Clip(string name)
    {
        AudioClip c;
        if (clips.TryGetValue(name, out c)) return c;
        c = Resources.Load<AudioClip>("FFMusic/" + name);
        clips[name] = c;
        if (c == null) Debug.Log("FFLocalMusic: no clip " + name);
        return c;
    }

    // url as the WebGL bus gets it: "audio/mus_lobby.ogg?v=a1"
    public static void Play(int chan, string url, float vol, float fade)
    {
        if (I == null || chan < 0 || chan > 1 || string.IsNullOrEmpty(url)) return;
        string n = url;
        int q = n.IndexOf('?'); if (q >= 0) n = n.Substring(0, q);
        int sl = n.LastIndexOf('/'); if (sl >= 0) n = n.Substring(sl + 1);
        if (n.EndsWith(".ogg")) n = n.Substring(0, n.Length - 4);
        if (n == I.playing[chan]) { I.goal[chan * 2 + I.side[chan]] = vol; return; }
        I.playing[chan] = n;
        float f = Mathf.Max(0.05f, fade);
        int old = chan * 2 + I.side[chan];
        I.goal[old] = 0f; I.rate[old] = Mathf.Max(0.01f, I.src[old].volume) / f;
        AudioClip c = I.Clip(n);
        if (c == null) return;
        I.side[chan] ^= 1;
        int nw = chan * 2 + I.side[chan];
        AudioSource s = I.src[nw];
        s.Stop(); s.clip = c; s.volume = 0f; s.Play();
        I.goal[nw] = vol; I.rate[nw] = Mathf.Max(0.01f, vol) / f;
    }

    public static void Lowpass(float hz) { if (I != null) I.lpGoal = Mathf.Clamp(hz, 200f, 22000f); }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < 4; i++)
        {
            AudioSource s = src[i];
            if (s.clip == null) continue;
            s.volume = Mathf.MoveTowards(s.volume, goal[i], Mathf.Max(rate[i], 0.05f) * dt);
            if (s.volume <= 0.0005f && goal[i] <= 0f && s.isPlaying) s.Stop();
        }
        if (lp != null) lp.cutoffFrequency = Mathf.Lerp(lp.cutoffFrequency, lpGoal, Mathf.Min(1f, dt * 3f));
    }
}
