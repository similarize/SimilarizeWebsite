using UnityEngine;

// lambou6: the Built-in LBPost image effect (OnRenderImage bloom + ACES + grade + vignette) became URP post-processing:
// one global Volume (U6.RaceProfile: Tonemapping ACES, Bloom, ColorAdjustments, WhiteBalance, Vignette) whose values follow
// the same per-track table as before. This component only marks a camera as post-processed (enabled = renderPostProcessing),
// so Look.ApplyViews / Game.SetPost keep working unchanged (split-screen views still skip post).
// Cheap (LBDisplay level 2) = no bloom / white balance, tonemap + colour adjustments only. Heat haze (lambo9, a UV wobble in the
// old final blit) is not ported.
[RequireComponent(typeof(Camera))]
public class LBPost : MonoBehaviour
{
    public float vignette = 0.42f;
    public bool themed = true;
    static bool cheap;
    public static bool Cheap { get { return cheap; } set { if (cheap != value) { cheap = value; Apply(); } } }

    // per theme: saturation, contrast, warmth, filmic exposure, bloom (same table as lambo9)
    static readonly float[,] Theme = {
        { 1.16f, 1.12f, 0.03f, 0.94f, 0.22f },   // 0 Coconut Cove
        { 1.16f, 1.17f, 0.05f, 0.92f, 0.2f },    // 1 Canyon Leap
        { 1.16f, 1.18f, 0.06f, 0.98f, 0.34f },   // 2 Volcano Rim
        { 1.12f, 1.18f, -0.035f, 0.85f, 0.16f }, // 3 Snowy Summit
        { 1.08f, 1.12f, 0.01f, 0.97f, 0.2f },    // 4 Jungle Ruins
        { 1.14f, 1.16f, -0.01f, 0.98f, 0.28f },  // 5 Sky City
    };
    static int theme;
    public static void SetTheme(int th) { theme = Mathf.Clamp(th, 0, Theme.GetLength(0) - 1); Apply(); }

    public static void Apply()
    {
        U6.Grade(Theme[theme, 0], Theme[theme, 1], Theme[theme, 2], Theme[theme, 3], Theme[theme, 4], 0.42f, cheap);
    }

    public static LBPost Add(Camera c, bool small = false)
    {
        var p = c.gameObject.GetComponent<LBPost>();
        if (p == null) p = c.gameObject.AddComponent<LBPost>();
        if (small) { p.vignette = 0.25f; p.themed = false; }
        U6.RaceCam(c, p.enabled);
        return p;
    }

    void OnEnable() { U6.Post(GetComponent<Camera>(), true); }
    void OnDisable() { U6.Post(GetComponent<Camera>(), false); }
}
