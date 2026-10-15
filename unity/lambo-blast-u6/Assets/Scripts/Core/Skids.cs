using UnityEngine;

// lambo8 skid marks: one shared ring-buffer mesh of soft-edged quads laid by the rear tyres while a kart drifts (and on hard
// landings). Lifted 4 cm along the ground normal and drawn after the road (queue 2460, no depth write) so it never z-fights.
// Oldest marks are overwritten (1400 quads). Theme colour: rubber black on tarmac, darker grooves on snow / sand.
public static class Skids
{
    const int Max = 1400;
    static Mesh mesh;
    static GameObject go;
    static Vector3[] v;
    static Color32[] c;
    static Vector2[] uv;
    static int head;
    static bool dirty;
    struct Last { public Vector3 p, r; public bool ok; public float t; }
    static readonly Last[] last = new Last[64];

    static void Ensure()
    {
        if (go != null) return;
        v = new Vector3[Max * 4]; c = new Color32[Max * 4]; uv = new Vector2[Max * 4];
        var idx = new int[Max * 6];
        for (int i = 0; i < Max; i++)
        {
            int b = i * 4, k = i * 6;
            idx[k] = b; idx[k + 1] = b + 2; idx[k + 2] = b + 1; idx[k + 3] = b + 1; idx[k + 4] = b + 2; idx[k + 5] = b + 3;
            uv[b] = new Vector2(0f, 0f); uv[b + 1] = new Vector2(1f, 0f); uv[b + 2] = new Vector2(0f, 1f); uv[b + 3] = new Vector2(1f, 1f);
        }
        mesh = new Mesh { name = "LB8 skids" };
        mesh.MarkDynamic();
        mesh.vertices = v; mesh.colors32 = c; mesh.uv = uv; mesh.triangles = idx;
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 5000f);
        go = new GameObject("Skid marks");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        var m = new Material(Mats.Fx) { name = "LB8 skid" };
        var t = new Texture2D(16, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int x = 0; x < 16; x++)
            for (int y = 0; y < 4; y++)
            {
                float e = Mathf.Sin((x + 0.5f) / 16f * Mathf.PI);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(e, 0.7f) * (0.75f + 0.25f * ((x * 7 + y * 3) % 5) / 4f)));
            }
        t.Apply(false);
        m.mainTexture = t;
        m.renderQueue = 2460;
        mr.sharedMaterial = m;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        go.AddComponent<SkidsTick>();
    }

    public static void Clear()
    {
        for (int i = 0; i < last.Length; i++) last[i].ok = false;
        if (v == null) return;
        for (int i = 0; i < v.Length; i++) v[i] = Vector3.zero;
        head = 0; dirty = true;
    }

    static Color32 Col()
    {
        switch (Track.Theme)
        {
            case 1: return new Color32(70, 40, 25, 150);
            case 3: return new Color32(95, 110, 135, 140);
            case 0: return new Color32(60, 50, 40, 140);
            default: return new Color32(18, 18, 20, 165);
        }
    }

    // slot = kart index * 2 + wheel; strength 0..1 (alpha)
    public static void Mark(int slot, Vector3 p, Vector3 normal, Vector3 right, float strength)
    {
        if (Gfx.Low || slot < 0 || slot >= last.Length) return;
        Ensure();
        p += normal * 0.04f;
        Last L = last[slot];
        last[slot] = new Last { p = p, r = right, ok = true, t = Time.time };
        if (!L.ok || Time.time - L.t > 0.15f || (p - L.p).sqrMagnitude > 9f) return;
        if ((p - L.p).sqrMagnitude < 0.09f) { last[slot] = L; return; }      // wait for 30 cm of travel
        float w = 0.16f;
        int b = head * 4;
        v[b] = L.p - L.r * w; v[b + 1] = L.p + L.r * w; v[b + 2] = p - right * w; v[b + 3] = p + right * w;
        Color32 cc = Col(); cc.a = (byte)(cc.a * Mathf.Clamp01(strength));
        c[b] = c[b + 1] = c[b + 2] = c[b + 3] = cc;
        head = (head + 1) % Max;
        dirty = true;
    }

    public static void Flush()
    {
        if (!dirty || mesh == null) return;
        mesh.vertices = v; mesh.colors32 = c;
        dirty = false;
    }
}

public class SkidsTick : MonoBehaviour
{
    void LateUpdate() { Skids.Flush(); }
}
