using UnityEngine;

// ?realism=1 test only (Realism.ApplyQuality swaps it in for LBPost): soft bloom on HDR highlights, depth-based SSAO
// (full tier), ACES filmic tonemap + light grade + soft vignette (Resources/FFRealPost.shader). Lite tier: LDR, no AO,
// quarter-res bloom.
[RequireComponent(typeof(Camera))]
public class RealPost : MonoBehaviour
{
    static Material mat;
    static bool tried;
    public float bloom = 0.16f, exposure = 0.92f, saturation = 1.04f, vignette = 0.2f, aoStrength = 0.7f, aoRadius = 0.7f;
    readonly RenderTexture[] chain = new RenderTexture[6];
    Camera cam;

    static Material Mat
    {
        get
        {
            if (mat == null && !tried)
            {
                tried = true;
                Shader s = Resources.Load<Shader>("FFRealPost");
                if (s == null) s = Shader.Find("Hidden/FFRealPost");
                if (s != null && s.isSupported) mat = new Material(s) { hideFlags = HideFlags.DontSave };
                Debug.Log("RealPost: shader " + (mat != null ? "ok" : "MISSING / unsupported"));
            }
            return mat;
        }
    }

    bool AO { get { return !Realism.Lite; } }

    void OnEnable()
    {
        cam = GetComponent<Camera>();
        if (AO && cam != null) cam.depthTextureMode |= DepthTextureMode.Depth;
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        Material m = Mat;
        if (m == null) { Graphics.Blit(src, dst); return; }
        if (cam == null) cam = GetComponent<Camera>();
        bool hdr = src.format == RenderTextureFormat.ARGBHalf || src.format == RenderTextureFormat.DefaultHDR || src.format == RenderTextureFormat.ARGBFloat;
        float thr = hdr ? 1.05f : 0.86f;
        int lv = Realism.Lite ? 3 : 5;
        int div = Realism.Lite ? 4 : 2;
        int w = Mathf.Max(8, src.width / div), h = Mathf.Max(8, src.height / div);
        m.SetVector("_Params", new Vector4(thr, 0.35f, bloom, vignette));
        m.SetVector("_Grade", new Vector4(saturation, exposure, hdr ? 1f : 0f, 0f));
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

        RenderTexture ao = null, ao2 = null;
        if (AO && cam != null)
        {
            Matrix4x4 p = cam.projectionMatrix;
            m.SetVector("_ProjInfo", new Vector4(1f / p[0, 0], 1f / p[1, 1], aoRadius, aoStrength));
            ao = RenderTexture.GetTemporary(src.width / 2, src.height / 2, 0, RenderTextureFormat.ARGB32);
            ao2 = RenderTexture.GetTemporary(src.width / 2, src.height / 2, 0, RenderTextureFormat.ARGB32);
            ao.filterMode = FilterMode.Bilinear; ao2.filterMode = FilterMode.Bilinear;
            Graphics.Blit(src, ao, m, 3);
            Graphics.Blit(ao, ao2, m, 4);
            m.SetTexture("_AO", ao2);
            m.SetFloat("_AOOn", 1f);
        }
        else { m.SetTexture("_AO", Texture2D.whiteTexture); m.SetFloat("_AOOn", 0f); }

        Graphics.Blit(src, dst, m, 5);
        for (int i = 0; i < made; i++) { RenderTexture.ReleaseTemporary(chain[i]); chain[i] = null; }
        if (ao != null) RenderTexture.ReleaseTemporary(ao);
        if (ao2 != null) RenderTexture.ReleaseTemporary(ao2);
    }
}
