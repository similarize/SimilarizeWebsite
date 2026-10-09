using System.Collections.Generic;
using UnityEngine;

// ffu14: countryside around the ranch out to the horizon - fields (crop / grass patchwork, hedgerows, dirt roads),
// woods, rolling hills, no towns. Cheap on purpose (Pixel 9 / Cybertruck browser):
//  * one ring mesh from the ranch square out to 19 km (192 x 30 vertices), heights from noise, gently curved with
//    distance (Earth curvature, R = 50 km) so the horizon bends when you climb;
//  * two procedural patchwork textures generated once at start: a "near" one over 3.6 km (sharp field edges, hedges,
//    roads) and a "far" one over 40 km (blurry patchwork + woods) - the far mesh rings use the far one;
//  * low-poly trees (pine + broadleaf, vertex coloured) in the woods and hedgerows within ~1.4 km, merged into a
//    handful of meshes per sector with distance culling; woods further out are canopy bumps + dark texture;
//  * fog does the rest at ground level; Worlds.PreCull thins the fog, opens the far plane and darkens the sky with
//    the camera's altitude, so a mech or the Starship climbing sees the ranch shrink into the patchwork.
public static class Countryside
{
    public const float Inner = Layout.Half, R = 50000f, FarEdge = 19000f, NearS = 3600f, FarS = 40000f;
    static Transform root;
    static Material groundNear, groundFar, treeMat;

    static float Hash(int a, int b) { unchecked { uint h = (uint)(a * 374761393 + b * 668265263); h = (h ^ (h >> 13)) * 1274126177u; return (h ^ (h >> 16)) / 4294967295f; } }

    static float ForestMask(float x, float z)
    {
        return Mathf.PerlinNoise(x * 0.0016f + 11.3f, z * 0.0016f + 5.7f) + 0.28f * (Mathf.PerlinNoise(x * 0.0071f + 2.1f, z * 0.0071f + 8.4f) - 0.5f);
    }
    public static bool IsForest(float x, float z) { return ForestMask(x, z) > 0.7f; }

    // rolling hills (m), 0 at the ranch boundary handled by the caller
    static float Hills(float x, float z)
    {
        return 10f + 26f * (Mathf.PerlinNoise(x * 0.0009f + 3.1f, z * 0.0009f + 7.7f) - 0.45f) + 8f * (Mathf.PerlinNoise(x * 0.0042f + 1.3f, z * 0.0042f + 2.9f) - 0.5f);
    }
    static float Drop(float x, float z) { return (x * x + z * z) / (2f * R); }

    // distance outside the ranch square (Chebyshev) - 0 at the boundary
    static float Outside(float x, float z) { return Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) - Inner; }

    static float edgeH = 12f;
    public static float Height(float x, float z, bool bumps)
    {
        float d = Outside(x, z);
        float h = Hills(x, z);
        float w = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d / 260f));
        float y = Mathf.Lerp(edgeH, h, w);
        if (bumps && IsForest(x, z)) y += 9f * Mathf.Clamp01((ForestMask(x, z) - 0.7f) / 0.06f);
        return y - Drop(x, z);
    }

    // the patchwork: fields on a rotated, staggered grid; hedgerows on some edges; dirt roads; woods
    static readonly Color[] Crops =
    {
        new Color(0.80f, 0.70f, 0.36f), new Color(0.36f, 0.52f, 0.20f), new Color(0.46f, 0.60f, 0.27f), new Color(0.45f, 0.36f, 0.25f),
        new Color(0.82f, 0.78f, 0.28f), new Color(0.66f, 0.64f, 0.38f), new Color(0.40f, 0.56f, 0.24f), new Color(0.55f, 0.62f, 0.30f)
    };
    const float FieldW = 170f, FieldH = 130f, RoadU = 1190f, RoadV = 910f;
    static readonly float Rot = 14f * Mathf.Deg2Rad;
    static void Field(float x, float z, out int cu, out int cv, out float edge, out float u, out float v)
    {
        float cs = Mathf.Cos(Rot), sn = Mathf.Sin(Rot);
        u = x * cs - z * sn; v = x * sn + z * cs;
        cu = Mathf.FloorToInt(u / FieldW);
        float vo = v + Hash(cu, 77) * 60f;
        cv = Mathf.FloorToInt(vo / FieldH);
        float du = Mathf.Min(u - cu * FieldW, (cu + 1) * FieldW - u), dv = Mathf.Min(vo - cv * FieldH, (cv + 1) * FieldH - vo);
        edge = Mathf.Min(du, dv);
    }
    static bool HedgeEdge(int cu, int cv) { return Hash(cu * 3 + 1, cv * 5 + 2) < 0.4f; }

    public static Color Patch(float x, float z, float texel)
    {
        float fm = ForestMask(x, z);
        int cu, cv; float edge, u, v;
        Field(x, z, out cu, out cv, out edge, out u, out v);
        float n = Mathf.PerlinNoise(x * 0.05f, z * 0.05f) * 0.12f - 0.06f;
        Color c;
        if (fm > 0.7f) c = new Color(0.13f, 0.25f, 0.11f) * (0.85f + Mathf.PerlinNoise(x * 0.03f, z * 0.03f) * 0.3f);
        else
        {
            float h = Hash(cu, cv);
            c = Crops[Mathf.Min(Crops.Length - 1, (int)(h * Crops.Length))];
            if (h < 0.5f) c *= 0.95f + 0.05f * Mathf.Sin(u * 0.9f + h * 30f);   // crop rows (fine texture only)
            c += new Color(n, n, n * 0.5f);
            if (edge < Mathf.Max(3.5f, texel * 0.6f) && HedgeEdge(cu, cv)) c = Color.Lerp(c, new Color(0.16f, 0.3f, 0.12f), 0.85f);
            else if (fm > 0.64f) c = Color.Lerp(c, new Color(0.2f, 0.33f, 0.15f), (fm - 0.64f) / 0.06f * 0.7f);
        }
        float ru = Mathf.Abs(u - Mathf.Round(u / RoadU) * RoadU), rv = Mathf.Abs(v - Mathf.Round(v / RoadV) * RoadV);
        float rw = Mathf.Max(3.5f, texel * 0.5f);
        if (Mathf.Min(ru, rv) < rw) c = Color.Lerp(c, new Color(0.62f, 0.54f, 0.41f), 0.9f);
        c.a = 1f;
        return c;
    }

    static Texture2D MakeTex(int n, float size)
    {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, true);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Trilinear;
        t.anisoLevel = Look.Mobile ? 1 : 4;
        var px = new Color32[n * n];
        float texel = size / n;
        for (int j = 0; j < n; j++)
        {
            float z = (j + 0.5f) * texel - size * 0.5f;
            for (int i = 0; i < n; i++)
            {
                float x = (i + 0.5f) * texel - size * 0.5f;
                px[j * n + i] = Patch(x, z, texel);
            }
        }
        t.SetPixels32(px);
        t.Apply(true, true);   // mipmaps = the soft, hazy patchwork from high up; then free the CPU copy
        return t;
    }

    static Material Mat(Texture tex, Color tint)
    {
        Shader sh = Resources.Load<Shader>("FFCountryside");
        if (sh == null) sh = Shader.Find("FF/Countryside");
        Material m;
        if (sh != null && sh.isSupported) m = new Material(sh);
        else { m = new Material(Mats.Lit(Color.white)); Debug.Log("Countryside: FF/Countryside shader missing - Standard fallback"); }
        m.mainTexture = tex;
        m.color = tint;
        return m;
    }

    public static void Build()
    {
        float t0 = Time.realtimeSinceStartup;
        root = new GameObject("Countryside").transform;
        // ranch edge height: the berm top (sample just inside the boundary)
        edgeH = Ranch.GY(Inner - 1f, 0f);
        groundNear = Mat(MakeTex(Look.Mobile ? 512 : 1024, NearS), Color.white);
        groundFar = Mat(MakeTex(Look.Mobile ? 256 : 512, FarS), Color.white);
        treeMat = Mat(Texture2D.whiteTexture, Color.white);
        BuildGround();
        int trees = BuildTrees(Look.Mobile ? 1400 : 2800);
        Debug.Log("Countryside: built in " + ((Time.realtimeSinceStartup - t0) * 1000f).ToString("0") + " ms, " + trees + " trees, shader " + (treeMat.shader != null ? treeMat.shader.name : "?"));
    }

    // ring mesh: inner boundary = the ranch square, rings outward; near part (UV over NearS) + far part (UV over FarS)
    static readonly float[] Rings = { -4f, 0f, 12f, 30f, 55f, 85f, 125f, 175f, 240f, 320f, 420f, 540f, 690f, 870f, 1080f, 1350f, 1650f,
                                      1650f, 2000f, 2450f, 3000f, 3700f, 4600f, 5700f, 7000f, 8600f, 10500f, 12700f, 15500f, FarEdge };
    static void BuildGround()
    {
        const int A = 192;
        int split = 17;   // Rings[16] and Rings[17] are the same distance: near mesh ends, far mesh starts
        BuildRing("Countryside near", A, 0, split, groundNear, NearS, false);
        BuildRing("Countryside far", A, split, Rings.Length, groundFar, FarS, true);
    }

    static void BuildRing(string name, int A, int r0, int r1, Material m, float uvSize, bool bumps)
    {
        int nr = r1 - r0;
        var v = new Vector3[A * nr]; var uv = new Vector2[A * nr]; var col = new Color[A * nr];
        for (int a = 0; a < A; a++)
        {
            float th = a * Mathf.PI * 2f / A;
            Vector2 dir = new Vector2(Mathf.Cos(th), Mathf.Sin(th));
            float k = Inner / Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.y));
            Vector2 b = dir * k;
            for (int r = 0; r < nr; r++)
            {
                float d = Rings[r0 + r];
                Vector2 p = b + dir * d;
                float y = d < 0f ? edgeH - 1.5f : Height(p.x, p.y, bumps);
                int i = a * nr + r;
                v[i] = new Vector3(p.x, y, p.y);
                uv[i] = new Vector2(p.x / uvSize + 0.5f, p.y / uvSize + 0.5f);
                col[i] = Color.white;
            }
        }
        var tri = new List<int>(A * nr * 6);
        for (int a = 0; a < A; a++)
        {
            int a2 = (a + 1) % A;
            for (int r = 0; r < nr - 1; r++)
            {
                int i0 = a * nr + r, i1 = a2 * nr + r, i2 = a * nr + r + 1, i3 = a2 * nr + r + 1;
                tri.Add(i0); tri.Add(i1); tri.Add(i2);
                tri.Add(i2); tri.Add(i1); tri.Add(i3);
            }
        }
        var mesh = new Mesh { name = name };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = v; mesh.uv = uv; mesh.colors = col;
        mesh.SetTriangles(tri, 0);
        mesh.RecalculateNormals();
        // winding check: normals must point up
        Vector3[] nm = mesh.normals;
        if (nm.Length > 0 && nm[nr * 3 + 2].y < 0f) { tri.Reverse(); mesh.SetTriangles(tri, 0); mesh.RecalculateNormals(); }
        mesh.RecalculateBounds();
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = m;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = !bumps;
    }

    // ---------------- low-poly trees ----------------
    static void AddCone(List<Vector3> v, List<Color> c, List<int> t, Vector3 basePos, float r, float h, int sides, Color col, float rot)
    {
        int b = v.Count;
        v.Add(basePos + Vector3.up * h); c.Add(col * 1.15f);
        for (int i = 0; i < sides; i++)
        {
            float a = rot + i * Mathf.PI * 2f / sides;
            v.Add(basePos + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r)); c.Add(col * (0.8f + 0.2f * Mathf.Sin(a + 1f)));
        }
        for (int i = 0; i < sides; i++) { t.Add(b); t.Add(b + 1 + (i + 1) % sides); t.Add(b + 1 + i); }
    }
    static void AddBlob(List<Vector3> v, List<Color> c, List<int> t, Vector3 ctr, float r, float h, Color col, float rot)
    {
        // a squashed octahedron-ish canopy: top, 6 equator points, bottom
        int b = v.Count;
        v.Add(ctr + Vector3.up * h * 0.5f); c.Add(col * 1.18f);
        for (int i = 0; i < 6; i++)
        {
            float a = rot + i * Mathf.PI / 3f;
            v.Add(ctr + new Vector3(Mathf.Cos(a) * r, h * 0.05f, Mathf.Sin(a) * r)); c.Add(col * (0.82f + 0.18f * Mathf.Sin(a + 0.7f)));
        }
        v.Add(ctr - Vector3.up * h * 0.5f); c.Add(col * 0.6f);
        for (int i = 0; i < 6; i++)
        {
            int p = b + 1 + i, q = b + 1 + (i + 1) % 6;
            t.Add(b); t.Add(q); t.Add(p);
            t.Add(b + 7); t.Add(p); t.Add(q);
        }
    }
    static void AddTree(List<Vector3> v, List<Color> c, List<int> t, Vector3 p, float s, bool pine, float hue)
    {
        Color trunk = new Color(0.33f, 0.24f, 0.16f);
        float rot = hue * 6.28f;
        AddCone(v, c, t, p - Vector3.up * 0.5f, 0.35f * s, 3.2f * s, 4, trunk, rot);
        if (pine)
        {
            Color g = Color.Lerp(new Color(0.12f, 0.27f, 0.13f), new Color(0.2f, 0.33f, 0.16f), hue);
            AddCone(v, c, t, p + Vector3.up * 1.6f * s, 2.4f * s, 5.5f * s, 7, g, rot);
            AddCone(v, c, t, p + Vector3.up * 4.2f * s, 1.7f * s, 4.2f * s, 7, g * 1.05f, rot + 0.3f);
        }
        else
        {
            Color g = Color.Lerp(new Color(0.22f, 0.4f, 0.15f), new Color(0.36f, 0.5f, 0.19f), hue);
            AddBlob(v, c, t, p + Vector3.up * 4.6f * s, 3.1f * s, 4.6f * s, g, rot);
            AddBlob(v, c, t, p + new Vector3(1.4f, 3.6f, 0.8f) * s, 2.0f * s, 3f * s, g * 0.95f, rot + 1f);
        }
    }

    static int BuildTrees(int cap)
    {
        const int Sectors = 8;
        var buckets = new Dictionary<int, (List<Vector3> v, List<Color> c, List<int> t)>();
        int count = 0;
        var rnd = new System.Random(1234);
        System.Func<float> R01 = () => (float)rnd.NextDouble();
        // candidate points: jittered 13 m grid within 1.4 km of the ranch; woods dense, hedgerows thin lines
        const float step = 13f, maxD = 1400f;
        var cands = new List<Vector3>();
        for (float z = -Inner - maxD; z <= Inner + maxD; z += step)
            for (float x = -Inner - maxD; x <= Inner + maxD; x += step)
            {
                float jx = x + (R01() - 0.5f) * step, jz = z + (R01() - 0.5f) * step;
                float d = Outside(jx, jz);
                if (d < 28f || d > maxD) continue;
                bool forest = IsForest(jx, jz);
                bool hedge = false;
                if (!forest)
                {
                    int cu, cv; float edge, u, vv;
                    Field(jx, jz, out cu, out cv, out edge, out u, out vv);
                    hedge = edge < 4f && HedgeEdge(cu, cv) && R01() < 0.9f;
                    float ru = Mathf.Abs(u - Mathf.Round(u / RoadU) * RoadU), rv = Mathf.Abs(vv - Mathf.Round(vv / RoadV) * RoadV);
                    if (Mathf.Min(ru, rv) < 6f) hedge = false;
                }
                if (!forest && !hedge) continue;
                if (forest && R01() < 0.15f) continue;
                cands.Add(new Vector3(jx, d, jz));
            }
        // nearest first, up to the cap
        cands.Sort((a, b) => a.y.CompareTo(b.y));
        foreach (var cpt in cands)
        {
            if (count >= cap) break;
            float x = cpt.x, z = cpt.z, d = cpt.y;
            float y = Height(x, z, false);
            float s = 0.8f + R01() * 0.55f;
            bool pine = Mathf.PerlinNoise(x * 0.004f + 40f, z * 0.004f) > 0.55f || R01() < 0.15f;
            int sector = Mathf.FloorToInt((Mathf.Atan2(z, x) / (Mathf.PI * 2f) + 0.5f) * Sectors) % Sectors;
            int band = d < 500f ? 0 : 1;
            int key = sector * 2 + band;
            if (!buckets.ContainsKey(key)) buckets[key] = (new List<Vector3>(), new List<Color>(), new List<int>());
            var bk = buckets[key];
            AddTree(bk.v, bk.c, bk.t, new Vector3(x, y, z), s, pine, R01());
            count++;
        }
        foreach (var kv in buckets)
        {
            var bk = kv.Value;
            var mesh = new Mesh { name = "Trees " + kv.Key };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(bk.v); mesh.SetColors(bk.c); mesh.SetTriangles(bk.t, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject("Trees " + kv.Key);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = treeMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var lod = go.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(kv.Key % 2 == 0 ? 0.004f : 0.012f, new Renderer[] { mr }) });
        }
        return count;
    }
}
