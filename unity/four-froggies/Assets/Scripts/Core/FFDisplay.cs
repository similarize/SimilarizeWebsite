using System.Runtime.InteropServices;
using UnityEngine;

// ffu17: pixel density + anti-aliasing per screen. The lobby renders at the screen's real density (capped 2.5x) so the
// UI text and the character turntables are 1:1 crisp on a Pixel 9 (2.625x) or a high-DPI desktop; gameplay goes back
// to the previous caps (phones 1.5x, desktop 2x) to keep the frame rate. MSAA: the page (web/index.html) forces
// antialias on the canvas context (Unity left it off even with every quality level at 4x); cameras with image effects
// render into an MSAA intermediate sized by QualitySettings.antiAliasing.
public static class FFDisplay
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern float FFSetDPR(int mode);
    [DllImport("__Internal")] static extern float FFScreenDPR();
    [DllImport("__Internal")] static extern int FFIsMobileUA();
#else
    static float FFSetDPR(int mode) { return 1f; }
    static float FFScreenDPR() { return 1f; }
    static int FFIsMobileUA() { return 0; }
#endif
    static int mode = -1;

    public static bool MobileUA { get { try { return FFIsMobileUA() == 1; } catch { return false; } } }
    public static float ScreenDpr { get { try { return FFScreenDPR(); } catch { return 1f; } } }

    // true = lobby (full density), false = gameplay (lighter density)
    public static void Lobby(bool on)
    {
        int m = on ? 1 : 0;
        if (m == mode) return;
        mode = m;
        try { FFSetDPR(m == 0 && lite ? 2 : m); } catch { }
        ApplyAA();
    }

    // ffu20: gameplay on phones now renders at 2x (was 1.5x) so the HUD / phone text is close to 1:1 on a Pixel 9;
    // if the frame rate can't hold ~28 fps over a 6 s window, drop once to the old 1.5x ("game-lite", logged).
    static bool lite;
    [RuntimeInitializeOnLoadMethod]
    static void Hook() { var g = new GameObject("FFDisplayWatch"); Object.DontDestroyOnLoad(g); g.hideFlags = HideFlags.HideInHierarchy; g.AddComponent<FFDisplayWatch>(); }
    public static void PerfTick(float dt, ref float t, ref float acc, ref int n)
    {
        if (mode != 0 || lite || !(Look.Mobile || MobileUA) || ScreenDpr <= 1.55f) { t = 0f; acc = 0f; n = 0; return; }
        t += dt;
        if (t < 4f) return;                 // settle after the lobby -> game switch
        acc += dt; n++;
        if (t < 10f) return;
        float avg = acc / Mathf.Max(1, n);
        if (avg > 1f / 28f) { lite = true; try { FFSetDPR(2); } catch { } Debug.Log("FFDPR game-lite: " + (1f / avg).ToString("0") + " fps at 2x"); }
        t = 4f; acc = 0f; n = 0;
    }

    public static void ApplyAA()
    {
        if (Realism.On) { QualitySettings.antiAliasing = 0; return; }   // the ?realism=1 test keeps its own (HDR + SSAO) path
        // lobby: 4x; gameplay: 2x on desktop post-processed views (a 4x 2560x1440 intermediate is heavy on laptops), phones
        // keep 0 for their offscreen targets (their cameras draw straight into the antialiased backbuffer anyway)
        QualitySettings.antiAliasing = mode == 1 ? 4 : Look.Mobile ? 0 : 2;
    }
}

public class FFDisplayWatch : MonoBehaviour
{
    float t, acc; int n;
    void Update() { FFDisplay.PerfTick(Mathf.Min(Time.unscaledDeltaTime, 0.5f), ref t, ref acc, ref n); }
}
