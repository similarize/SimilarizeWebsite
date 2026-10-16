using System.Runtime.InteropServices;
using UnityEngine;

// lambo7 display density + anti-aliasing + automatic quality fallback (ported from Four Froggies ffu17 / ffu20).
// - Lobby renders at the screen's real density (page cap 2.5x) with 4x MSAA so text and the showroom cars are 1:1 crisp.
// - Racing renders at up to 2x (desktop, phones, Tesla) with 2x MSAA on the post-processed camera targets.
// - Governor: while racing, if the average frame rate over a 6 s window stays under the tier's floor (desktop / Tesla 45,
//   phones 30 fps), drop one level and log "LBQ downgrade": level 1 = MSAA off + 1.5x density, level 2 = 1.0x density + no
//   bloom / grade (cheap post) + hard shadows. It never climbs back during a session (no oscillation).
// ?lbq=0|1|2 forces a level; ?aa=0 turns the canvas MSAA off (page); ?dpr=N forces a density (page).
public static class LBDisplay
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern float LBSetDPR(int mode);
    [DllImport("__Internal")] static extern float LBScreenDPR();
    [DllImport("__Internal")] static extern int LBIsMobileUA();
    [DllImport("__Internal")] static extern int LBIsTeslaUA();
    [DllImport("__Internal")] static extern int LBContextAA();
    [DllImport("__Internal")] static extern float LBSafeInset(int side);
#else
    static float LBSetDPR(int mode) { return 1f; }
    static float LBScreenDPR() { return 1f; }
    static int LBIsMobileUA() { return 0; }
    static int LBIsTeslaUA() { return 0; }
    static int LBContextAA() { return 0; }
    static float LBSafeInset(int side) { return 0f; }
#endif
    static int mode = -1;
    public static int Level { get; private set; }          // 0 full, 1 lighter, 2 lightest
    public static bool Forced { get; private set; }
    public static bool InLobby { get { return mode == 1; } }

    public static bool MobileUA { get { try { return LBIsMobileUA() == 1; } catch { return false; } } }
    public static bool TeslaUA { get { try { return LBIsTeslaUA() == 1; } catch { return false; } } }
    public static float ScreenDpr { get { try { return LBScreenDPR(); } catch { return 1f; } } }
    public static int ContextSamples { get { try { return LBContextAA(); } catch { return -1; } } }

    // u6h: the screen area free of notches / rounded corners / the home bar, in Unity screen pixels (origin bottom-left).
    // WebGL: the page's CSS env(safe-area-inset-*) (viewport-fit=cover) read by LBSafeInset, re-read every 0.5 s; elsewhere
    // Screen.safeArea. Lobby UI (GameLobbyUI) is fitted inside it; the sound button keeps clear of it.
    static Rect safeRect; static float safeT = -10f; static Vector2Int safeScr;
    public static Rect SafeRect
    {
        get
        {
            var scr = new Vector2Int(Screen.width, Screen.height);
            if (Time.unscaledTime - safeT < 0.5f && scr == safeScr) return safeRect;
            safeT = Time.unscaledTime; safeScr = scr;
            Rect r = new Rect(0, 0, Screen.width, Screen.height);
            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                float t = LBSafeInset(0), rt = LBSafeInset(1), b = LBSafeInset(2), l = LBSafeInset(3);
                r = new Rect(l * Screen.width, b * Screen.height, Screen.width * (1f - l - rt), Screen.height * (1f - t - b));
#else
                r = Screen.safeArea;
#endif
            }
            catch { }
            if (r.width < Screen.width * 0.5f || r.height < Screen.height * 0.5f) r = new Rect(0, 0, Screen.width, Screen.height);
            if (r != safeRect) Debug.Log("U6H: safe area " + r.x.ToString("0") + "," + r.y.ToString("0") + " " + r.width.ToString("0") + "x" + r.height.ToString("0") + " of " + Screen.width + "x" + Screen.height);
            safeRect = r;
            return r;
        }
    }

    public static void Init()
    {
        string u = Application.absoluteURL ?? "";
        int i = u.IndexOf("lbq=");
        if (i >= 0 && i + 4 < u.Length && char.IsDigit(u[i + 4])) { Level = Mathf.Clamp(u[i + 4] - '0', 0, 2); Forced = true; }
        if (u.Contains("gfx=low")) { Level = 2; Forced = true; }     // lambo8: ?gfx=low = lambo7 look at the lightest level
        var g = new GameObject("LBDisplayWatch");
        Object.DontDestroyOnLoad(g);
        g.hideFlags = HideFlags.HideInHierarchy;
        g.AddComponent<LBDisplayWatch>();
        Debug.Log("LBQ: start level " + Level + (Forced ? " (forced)" : "") + " screen dpr " + ScreenDpr + (TeslaUA ? " Tesla" : "") + (MobileUA ? " mobileUA" : ""));
    }

    // true = lobby (full density), false = racing
    public static void Lobby(bool on)
    {
        int m = on ? 1 : 0;
        if (m == mode) return;
        mode = m;
        ApplyDpr();
        ApplyAA();
        Look.ApplyLevel();
    }

    static void ApplyDpr()
    {
        try { LBSetDPR(mode == 1 ? 1 : Level == 0 ? 0 : Level == 1 ? 2 : 3); } catch { }
    }

    public static void ApplyAA()
    {
        // lambo8b: race stays 2x MSAA in level 0 (at 1.5-2x density that is 6-8 samples per CSS pixel; 4x cost too much fill rate)
        // lambou6: URP asset MSAA (lobby 4x, race 2x at level 0); WebGPU + desktop + one view at level 0 races with STP instead
        U6.AntiAlias(mode == 1 ? 4 : Level == 0 ? 2 : 1, mode == 0, Level == 0 && !Look.Mobile && Look.Views == 1, Look.Cams);
    }

    static void Downgrade(float fps)
    {
        if (Level >= 2) return;
        Level++;
        Debug.Log("LBQ downgrade -> level " + Level + " (" + fps.ToString("0") + " fps over 6 s, " + Screen.width + "x" + Screen.height + ")");
        ApplyDpr();
        ApplyAA();
        Look.ApplyLevel();
    }

    public static void PerfTick(float dt, ref float t, ref float acc, ref int n)
    {
        bool racing = mode == 0 && Game.I != null && (Game.I.state == Game.State.Race || Game.I.state == Game.State.Countdown);
        if (!racing || Forced || Level >= 2 || Kart.Autopilot && !(Application.absoluteURL ?? "").Contains("lbgov")) { t = 0f; acc = 0f; n = 0; return; }
        t += dt;
        if (t < 4f) return;                 // settle after the lobby -> race switch
        acc += dt; n++;
        if (t < 10f) return;
        float avg = acc / Mathf.Max(1, n), fps = 1f / Mathf.Max(0.0001f, avg);
        float floor = (Look.Mobile || MobileUA) ? 30f : 45f;
        if (fps < floor) Downgrade(fps);
        t = 4f; acc = 0f; n = 0;
    }
}

public class LBDisplayWatch : MonoBehaviour
{
    float t, acc; int n;
    void Update() { LBDisplay.PerfTick(Mathf.Min(Time.unscaledDeltaTime, 0.5f), ref t, ref acc, ref n); }
}
