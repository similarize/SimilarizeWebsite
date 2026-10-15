using UnityEngine;

// lambo9 per-car dressing: a soft contact shadow / ambient-occlusion pad under the car (Resources/LBBlob.shader, multiply
// blend, stays on the ground and fades out while the car is airborne), glowing head- and taillight sprites (additive,
// taillights flare when braking, a soft exhaust glow while boosting). All procedural, no assets.
public class CarDress
{
    static Material blobMat, glowMat;
    static Texture2D blobTex, glowTex;
    static bool tried;
    Transform kart, blob;
    Renderer blobR;
    Renderer[] tails, heads;
    Mesh quad;
    MaterialPropertyBlock mpb;
    float brakeK, blobK = 1f;

    static void InitShared()
    {
        if (tried) return;
        tried = true;
        Shader s = Resources.Load<Shader>("LBBlob");
        if (s == null) s = Shader.Find("LB/Blob");
        if (s != null && s.isSupported)
        {
            blobTex = BlobTex(48, 96);
            blobMat = new Material(s) { name = "LB9 blob", mainTexture = blobTex };
        }
        if (Mats.FxAdd != null)
        {
            glowTex = GlowTex(64);
            glowMat = new Material(Mats.FxAdd) { name = "LB9 lamp glow", mainTexture = glowTex };
        }
        Debug.Log("CarDress: lambo9 blob " + (blobMat != null) + " glow " + (glowMat != null));
    }

    // rounded-rectangle soft shadow (darkest under the floor, soft falloff past the bodywork)
    static Texture2D BlobTex(int w, int h)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = Mathf.Abs((x + 0.5f) / w * 2f - 1f), v = Mathf.Abs((y + 0.5f) / h * 2f - 1f);
                float dx = Mathf.Max(0f, u - 0.45f) / 0.55f, dy = Mathf.Max(0f, v - 0.62f) / 0.38f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        t.SetPixels32(px); t.Apply(true);
        return t;
    }

    static Texture2D GlowTex(int n)
    {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[n * n];
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c * 1.8f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                float core = Mathf.Pow(a, 6f), halo = Mathf.Pow(a, 2.2f) * 0.55f;
                byte b = (byte)(Mathf.Clamp01(core + halo) * 255);
                px[y * n + x] = new Color32(b, b, b, b);
            }
        t.SetPixels32(px); t.Apply(true);
        return t;
    }

    static Mesh Quad()
    {
        var m = new Mesh { name = "LB9 quad" };
        m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
        m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        m.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    static Renderer Sprite(Mesh q, Transform parent, Material m, Vector3 pos, Vector3 scale, float yawDeg)
    {
        var g = new GameObject("Glow");
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
        g.transform.localScale = scale;
        g.AddComponent<MeshFilter>().sharedMesh = q;
        var r = g.AddComponent<MeshRenderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return r;
    }

    public static CarDress Attach(Transform kart, Transform sprung, Color paint)
    {
        InitShared();
        var d = new CarDress { kart = kart, mpb = new MaterialPropertyBlock() };
        d.quad = Quad();
        if (blobMat != null)
        {
            var g = new GameObject("ContactShadow");
            g.transform.SetParent(kart, false);
            g.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            g.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            g.transform.localScale = new Vector3(2.7f, 5.5f, 1f);
            g.AddComponent<MeshFilter>().sharedMesh = d.quad;
            d.blobR = g.AddComponent<MeshRenderer>();
            d.blobR.sharedMaterial = blobMat;
            d.blobR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            d.blobR.receiveShadows = false;
            d.blob = g.transform;
        }
        if (glowMat != null && sprung != null)
        {
            d.tails = new Renderer[3];
            d.heads = new Renderer[2];
            for (int i = 0; i < 2; i++)
            {
                float sx = i == 0 ? -1f : 1f;
                d.tails[i] = Sprite(d.quad, sprung, glowMat, new Vector3(sx * 0.64f, 0.68f, -2.33f), new Vector3(0.75f, 0.32f, 1f), 180f);
                d.heads[i] = Sprite(d.quad, sprung, glowMat, new Vector3(sx * 0.62f, 0.45f, 2.3f), new Vector3(0.6f, 0.26f, 1f), 0f);
            }
            d.tails[2] = Sprite(d.quad, sprung, glowMat, new Vector3(0f, 0.74f, -2.31f), new Vector3(1.9f, 0.16f, 1f), 180f);   // full-width light bar
            d.Tint(d.heads[0], new Color(0.42f, 0.4f, 0.34f, 1f)); d.Tint(d.heads[1], new Color(0.42f, 0.4f, 0.34f, 1f));
        }
        return d;
    }

    void Tint(Renderer r, Color c)
    {
        if (r == null) return;
        mpb.SetColor("_TintColor", c);
        r.SetPropertyBlock(mpb);
    }

    public void Tick(Kart k, bool braking, bool boost)
    {
        float dt = Time.deltaTime;
        if (tails != null)
        {
            float want = braking ? 1f : 0f;
            float nb = Mathf.MoveTowards(brakeK, want, dt * 10f);
            if (Mathf.Abs(nb - brakeK) > 0.001f || Time.frameCount % 30 == (k.id % 30))
            {
                brakeK = nb;
                Color c = Color.Lerp(new Color(0.34f, 0.035f, 0.03f, 1f), new Color(0.95f, 0.1f, 0.07f, 1f), brakeK);
                Tint(tails[0], c); Tint(tails[1], c);
                Tint(tails[2], c * (0.55f + 0.25f * brakeK));
                float sc = 1f + 0.35f * brakeK;
                tails[0].transform.localScale = tails[1].transform.localScale = new Vector3(0.75f * sc, 0.32f * sc, 1f);
            }
        }
        if (blob != null)
        {
            // stays on the ground under a flying car, shrinking + fading with height
            float y = k.transform.position.y, gy = y;
            if (!k.grounded)
            {
                float hy; Vector3 hn;
                Track.Ground(k.transform.position, out hy, out hn);
                gy = hy;
            }
            float hgt = Mathf.Max(0f, y - gy);
            float want = k.respawning ? 0f : Mathf.Clamp01(1f - hgt / 7f);
            blobK = Mathf.MoveTowards(blobK, want, dt * 4f);
            if (k.grounded) { blob.localPosition = new Vector3(0f, 0.05f, 0f); blob.localRotation = Quaternion.Euler(90f, 0f, 0f); }
            else { Vector3 p = k.transform.position; blob.position = new Vector3(p.x, gy + 0.05f, p.z); blob.rotation = Quaternion.Euler(90f, k.yaw, 0f); }
            float s = 1f + hgt * 0.06f;
            blob.localScale = new Vector3(2.7f * s, 5.5f * s, 1f);
            if (Time.frameCount % 3 == (k.id % 3))
            {
                mpb.SetFloat("_Strength", blobK * 0.78f);
                blobR.SetPropertyBlock(mpb);
            }
        }
    }

    public void Destroy()
    {
        if (blob != null) Object.Destroy(blob.gameObject);
        if (quad != null) Object.Destroy(quad);
        blob = null; tails = null; heads = null;
    }
}
