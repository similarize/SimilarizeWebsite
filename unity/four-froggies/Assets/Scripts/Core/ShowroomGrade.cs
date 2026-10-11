using UnityEngine;

// ffu29 lobby showroom grade (Resources/FFShowroomGrade.shader), on the turntable + thumbnail cameras only (Showroom.Grade).
// HDR source (desktop) or LDR (phones): exposure -> ACES filmic (Narkowicz fit) -> gentle contrast round mid-grey ->
// natural saturation -> soft vignette that darkens the backdrop corners so the character pops. One full-screen blit.
[RequireComponent(typeof(Camera))]
public class ShowroomGrade : MonoBehaviour
{
    static Material mat;
    static bool tried;
    public float vignette = 0.6f;
    public static float Exposure = 1.0f, Contrast = 1.06f, Saturation = 1.0f;

    static Material Mat
    {
        get
        {
            if (mat == null && !tried)
            {
                tried = true;
                Shader s = Resources.Load<Shader>("FFShowroomGrade");
                if (s == null) s = Shader.Find("Hidden/FFShowroomGrade");
                if (s != null && s.isSupported) mat = new Material(s) { hideFlags = HideFlags.DontSave };
                Debug.Log("ShowroomGrade: shader " + (mat != null ? "ok" : "MISSING / unsupported"));
            }
            return mat;
        }
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        Material m = Mat;
        if (m == null) { Graphics.Blit(src, dst); return; }
        bool hdr = src.format == RenderTextureFormat.ARGBHalf || src.format == RenderTextureFormat.ARGBFloat ||
                   src.format == RenderTextureFormat.RGB111110Float || src.format == RenderTextureFormat.DefaultHDR;
        // LDR input tops out at 1.0: a little more exposure keeps whites near 0.9 after the filmic shoulder
        m.SetVector("_Grade", new Vector4(Exposure * (hdr ? 1.0f : 1.3f), Contrast, Saturation, vignette));
        Graphics.Blit(src, dst, m, 0);
    }
}
