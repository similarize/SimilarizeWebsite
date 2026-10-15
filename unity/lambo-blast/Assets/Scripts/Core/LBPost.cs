using UnityEngine;

// Camera image effect (Built-in pipeline): bloom + filmic ACES tonemap + per-track colour grade + vignette
// (Resources/LBPost.shader). Only enabled for a full-screen camera (split-screen viewports skip it, Look.PostAllowed).
// lambo7: the grade comes from the track theme (LBPost.SetTheme from Look.SetTheme); Cheap (LBDisplay level 2) = no bloom
// chain, one grade blit.
[RequireComponent(typeof(Camera))]
public class LBPost : MonoBehaviour
{
    static Material mat;
    static bool tried;
    public float bloom = 0.28f, threshold = 0.9f, vignette = 0.42f;
    public float saturation = 1.15f, contrast = 1.08f, warmth = 0.03f, exposure = 1.0f;
    public float filmic = 1.05f, vigInner = 0.32f;
    public int levels = 5;
    public bool themed = true;
    public static bool Cheap;
    readonly RenderTexture[] chain = new RenderTexture[6];

    // per theme: saturation, contrast, warmth, filmic exposure, bloom
    static readonly float[,] Theme = {
        { 1.16f, 1.12f, 0.03f, 0.94f, 0.22f },   // 0 Coconut Cove (lambo8: was 1.02 exposure - the beach read washed-out / near white)
        { 1.16f, 1.17f, 0.05f, 0.92f, 0.2f },    // 1 Canyon Leap (7c: lower exposure)
        { 1.16f, 1.18f, 0.06f, 0.98f, 0.34f },   // 2 Volcano Rim (lava glow blooms more)
        { 1.12f, 1.18f, -0.035f, 0.85f, 0.16f }, // 3 Snowy Summit (snow + sky clip fast: lower exposure, more contrast; 7c lower again)
        { 1.08f, 1.12f, 0.01f, 0.97f, 0.2f },    // 4 Jungle Ruins (lambo8b: less saturation - the grass read neon)
        { 1.14f, 1.16f, -0.01f, 0.98f, 0.28f },  // 5 Sky City
    };
    static int theme;
    public static void SetTheme(int th) { theme = Mathf.Clamp(th, 0, Theme.GetLength(0) - 1); }

    public static Material Mat
    {
        get
        {
            if (mat == null && !tried)
            {
                tried = true;
                Shader s = Resources.Load<Shader>("LBPost");
                if (s == null) s = Shader.Find("Hidden/LBPost");
                if (s != null && s.isSupported) mat = new Material(s) { hideFlags = HideFlags.DontSave };
                Debug.Log("LBPost: shader " + (mat != null ? "ok (lambo7 filmic)" : "MISSING / unsupported"));
            }
            return mat;
        }
    }

    public static LBPost Add(Camera c, bool small = false)
    {
        var p = c.gameObject.GetComponent<LBPost>();
        if (p == null) p = c.gameObject.AddComponent<LBPost>();
        if (small) { p.levels = 3; p.bloom = 0.5f; p.vignette = 0.25f; p.themed = false; }
        return p;
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        Material m = Mat;
        if (m == null) { Graphics.Blit(src, dst); return; }
        float sat = saturation, con = contrast, warm = warmth, film = filmic, bl = bloom;
        if (themed) { sat = Theme[theme, 0]; con = Theme[theme, 1]; warm = Theme[theme, 2]; film = Theme[theme, 3]; bl = Theme[theme, 4]; }
        m.SetVector("_Grade", new Vector4(sat, con, warm, exposure));
        m.SetVector("_Look", new Vector4(film, vigInner, 0f, 0f));
        if (Cheap)
        {
            m.SetVector("_Params", new Vector4(threshold, 0.25f, 0f, vignette));
            m.SetTexture("_Bloom", Texture2D.blackTexture);
            Graphics.Blit(src, dst, m, 3);
            return;
        }
        m.SetVector("_Params", new Vector4(threshold, 0.25f, bl, vignette));
        int lv = Mathf.Clamp(Look.Mobile ? levels - 1 : levels, 2, chain.Length);
        int w = Mathf.Max(8, src.width / (Look.Mobile ? 4 : 2)), h = Mathf.Max(8, src.height / (Look.Mobile ? 4 : 2));
        int made = 0;
        for (int i = 0; i < lv; i++)
        {
            chain[i] = RenderTexture.GetTemporary(w, h, 0, src.format);
            chain[i].filterMode = FilterMode.Bilinear;
            Graphics.Blit(i == 0 ? src : chain[i - 1], chain[i], m, i == 0 ? 0 : 1);
            made++;
            w = Mathf.Max(4, w / 2); h = Mathf.Max(4, h / 2);
            if (w <= 8 || h <= 8) break;
        }
        for (int i = made - 2; i >= 0; i--) Graphics.Blit(chain[i + 1], chain[i], m, 2);
        m.SetTexture("_Bloom", chain[0]);
        Graphics.Blit(src, dst, m, 3);
        for (int i = 0; i < made; i++) { RenderTexture.ReleaseTemporary(chain[i]); chain[i] = null; }
    }
}
