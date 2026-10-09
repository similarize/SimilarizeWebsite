using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

// Screen metrics the UI lays itself out against (all in canvas / Screen pixels):
//  - safe-area insets (CSS env(safe-area-inset-*) read by Plugins/WebGL/BBPage.jslib, plus Screen.safeArea),
//  - the page's own toolbar (fullscreen + "Arcade" buttons, top-right) so the SOUND button never sits under it,
//  - Ui = pixels per UI unit (short screen side / 720), the same everywhere so portrait and landscape match.
public static class Page
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern int BBPage_Metric(int which);
    static int Metric(int w) { try { return BBPage_Metric(w); } catch { return 0; } }
#else
    static int Metric(int w) { return 0; }
#endif

    public static int L, R, T, B;      // safe-area insets in px
    public static int TbW, TbH;        // page toolbar width (from the right edge) and bottom (from the top edge) in px
    static float nextT = -1f;
    static int lastW, lastH;

    public static void Refresh()
    {
        if (Time.unscaledTime < nextT && Screen.width == lastW && Screen.height == lastH) return;
        nextT = Time.unscaledTime + 0.5f;
        lastW = Screen.width; lastH = Screen.height;
        L = Metric(0); R = Metric(1); T = Metric(2); B = Metric(3);
        TbW = Metric(4); TbH = Metric(5);
        Rect sa = Screen.safeArea;
        if (sa.width > 10 && sa.height > 10)
        {
            L = Mathf.Max(L, Mathf.RoundToInt(sa.xMin));
            B = Mathf.Max(B, Mathf.RoundToInt(sa.yMin));
            R = Mathf.Max(R, Mathf.RoundToInt(Screen.width - sa.xMax));
            T = Mathf.Max(T, Mathf.RoundToInt(Screen.height - sa.yMax));
        }
    }

    public static bool Portrait { get { return Screen.height > Screen.width * 1.05f; } }

    // pixels per UI unit: 1 at 720 px on the short side
    public static float Ui { get { return Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 720f, 0.45f, 3f); } }

    // the usable part of a pixel rect after removing the safe-area insets
    public static Rect Safe(Rect r)
    {
        float x0 = Mathf.Max(r.xMin, L), y0 = Mathf.Max(r.yMin, B);
        float x1 = Mathf.Min(r.xMax, Screen.width - R), y1 = Mathf.Min(r.yMax, Screen.height - T);
        return new Rect(x0, y0, Mathf.Max(1f, x1 - x0), Mathf.Max(1f, y1 - y0));
    }
}
