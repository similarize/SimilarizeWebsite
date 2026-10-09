using UnityEngine;

// Camera image effect (Built-in pipeline): bloom + warm saturated grade + vignette (Resources/LBPost.shader).
// Only enabled for a full-screen camera or one that renders into a RenderTexture (split-screen viewports skip it),
// and only when the quality tier allows it (Look.PostAllowed).
[RequireComponent(typeof(Camera))]
public class LBPost : MonoBehaviour
{
    static Material mat;
    static bool tried;
    public float bloom = 0.6f, threshold = 0.82f, vignette = 0.55f;
    public float saturation = 1.18f, contrast = 1.08f, warmth = 0.045f, exposure = 1.03f;
    public int levels = 4;
    readonly RenderTexture[] chain = new RenderTexture[6];
    Camera cam;

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
                Debug.Log("LBPost: shader " + (mat != null ? "ok" : "MISSING / unsupported"));
            }
            return mat;
        }
    }

    public static LBPost Add(Camera c, bool small = false)
    {
        var p = c.gameObject.GetComponent<LBPost>();
        if (p == null) p = c.gameObject.AddComponent<LBPost>();
        if (small) { p.levels = 3; p.bloom = 0.75f; p.vignette = 0.25f; }
        return p;
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        Material m = Mat;
        if (m == null) { Graphics.Blit(src, dst); return; }
        if (cam == null) cam = GetComponent<Camera>();
        int lv = Mathf.Clamp(Look.Mobile ? levels - 1 : levels, 2, chain.Length);
        int w = Mathf.Max(8, src.width / (Look.Mobile ? 4 : 2)), h = Mathf.Max(8, src.height / (Look.Mobile ? 4 : 2));
        m.SetVector("_Params", new Vector4(threshold, 0.25f, bloom, vignette));
        m.SetVector("_Grade", new Vector4(saturation, contrast, warmth, exposure));
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
