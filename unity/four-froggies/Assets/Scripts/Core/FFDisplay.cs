using System.Runtime.InteropServices;
using UnityEngine;

// ffu17: pixel density + anti-aliasing per screen. The lobby renders at the screen's real density (capped 2.5x) so the
// UI text and the character turntables are 1:1 crisp on a Pixel 9 (2.625x) or a high-DPI desktop; gameplay goes back
// to the previous caps (phones 1.5x, desktop 2x) to keep the frame rate. MSAA: the WebGL context is created with
// antialias (BuildScript sets every quality level to 4x, which is what Unity reads at startup); cameras with image
// effects render into an MSAA intermediate sized by QualitySettings.antiAliasing.
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
        try { FFSetDPR(m); } catch { }
        ApplyAA();
    }

    public static void ApplyAA()
    {
        if (Realism.On) { QualitySettings.antiAliasing = 0; return; }   // the ?realism=1 test keeps its own (HDR + SSAO) path
        // lobby: 4x; gameplay: 2x on desktop post-processed views (a 4x 2560x1440 intermediate is heavy on laptops), phones
        // keep 0 for their offscreen targets (their cameras draw straight into the antialiased backbuffer anyway)
        QualitySettings.antiAliasing = mode == 1 ? 4 : Look.Mobile ? 0 : 2;
    }
}
