using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// lambo8 graphics upgrade: CC0 PBR surfaces (ambientCG, Resources/LB8) on the scenery through world-space triplanar shaders,
// a real ground shader (two-scale grain + macro breakup + triplanar rock on steep faces), a hi-res marked asphalt road with
// damp patches, geometry grass that shrinks away with distance, procedural pines / broadleaf trees, grandstands, skid marks.
// ?gfx=low = the lambo7 look (old flat materials, no grass, no new trees) + the lightest quality level, for weak GPUs.
// Everything falls back to the old materials if a shader is missing or unsupported (logged "Gfx8: ...").
public static class Gfx
{
    public static bool Low { get; private set; }
    static Shader triS, terrS, roadS, grassS;
    static Texture2D macro;
    static readonly Dictionary<string, Texture2D> tex = new Dictionary<string, Texture2D>();
    static readonly Dictionary<long, Material> surf = new Dictionary<long, Material>();
    static Material grassMat;

    public enum K { SandRock, Basalt, GreyRock, MossRock, Stone, Bark, Leaf, Snow, Gravel, Concrete, Wood }
    static readonly string[] KTex = { "rk_sand", "rk_dark", "rk_grey", "rk_moss", "stone", "bark", "leaf", "snow", "gravel", "concrete", "bark" };
    static readonly float[] KScale = { 0.14f, 0.2f, 0.17f, 0.17f, 0.22f, 0.7f, 0.55f, 0.22f, 0.45f, 0.14f, 1.1f };
    static readonly float[] KGloss = { 0.14f, 0.3f, 0.2f, 0.14f, 0.12f, 0.06f, 0.18f, 0.4f, 0.08f, 0.16f, 0.14f };
    static readonly float[] KBump = { 1f, 1f, 1f, 1f, 1.1f, 1.2f, 0f, 0.6f, 1f, 0.7f, 0.8f };

    public static bool Tri { get { return !Low && triS != null; } }

    public static void Init()
    {
        string u = Application.absoluteURL ?? "";
        Low = u.Contains("gfx=low");
        triS = Load("LBTri"); terrS = Load("LBTerrain"); roadS = Load("LBRoad"); grassS = Load("LBGrass");
        macro = MacroNoise(128);
        Debug.Log("Gfx8: " + (Low ? "LOW (gfx=low)" : "full") + " tri " + (triS != null) + " terrain " + (terrS != null) + " road " + (roadS != null) + " grass " + (grassS != null));
    }

    static Shader Load(string n)
    {
        Shader s = Resources.Load<Shader>(n);
        if (s == null) s = Shader.Find("LB/" + n.Substring(2));
        if (s != null && !s.isSupported) { Debug.LogWarning("Gfx8: shader " + n + " unsupported"); s = null; }
        return s;
    }

    public static Texture2D T(string name)
    {
        Texture2D t;
        if (tex.TryGetValue(name, out t)) return t;
        t = Resources.Load<Texture2D>("LB8/" + name);
        tex[name] = t;
        return t;
    }

    // ---------------------------------------------------------------- per-level settings (LBDisplay governor / ?lbq / ?gfx=low)
    public static void ApplyLevel(int level, bool mobile)
    {
        bool lite = Low || level >= 2 || (mobile && level >= 1);
        if (lite) Shader.EnableKeyword("LB_LITE"); else Shader.DisableKeyword("LB_LITE");
        if (grassMat != null)
        {
            Vector4 f = level == 0 ? (mobile ? new Vector4(26f, 46f, 0f, 0f) : new Vector4(42f, 72f, 0f, 0f)) : level == 1 ? new Vector4(20f, 36f, 0f, 0f) : new Vector4(0f, 0f, 0f, 0f);
            grassMat.SetVector("_Fade", f);
            foreach (var g in grassRoots) if (g != null) g.SetActive(level < 2);
        }
    }

    // ---------------------------------------------------------------- materials
    // triplanar CC0 surface tinted with the old flat colour (falls back to the old Lit material)
    public static Material Surf(Color c, K k)
    {
        if (!Tri) return Mats.Lit(c);
        int th = Track.Theme;
        bool snow = th == 3 && k != K.Concrete && k != K.Snow;
        Color32 cc = c;
        long key = ((long)k << 40) | ((long)(snow ? 1 : 0) << 36) | ((long)cc.r << 24) | ((long)cc.g << 16) | ((long)cc.b << 8);
        Material m;
        if (surf.TryGetValue(key, out m) && m != null) return m;
        Texture2D d = T(KTex[(int)k]), n = T(KTex[(int)k] + "_n");
        if (d == null) return Mats.Lit(c);
        m = new Material(triS);
        m.name = "LB8 " + k;
        m.color = c;
        m.mainTexture = d;
        if (n != null) m.SetTexture("_BumpMap", n);
        m.SetFloat("_BumpScale", n != null ? KBump[(int)k] : 0f);
        m.SetFloat("_Scale", KScale[(int)k]);
        m.SetFloat("_Glossiness", KGloss[(int)k]);
        m.SetFloat("_Snow", snow ? 0.62f : 0f);
        m.SetFloat("_AO", k == K.Leaf || k == K.Bark ? 1f : 0f);
        surf[key] = m;
        return m;
    }

    // full-colour map projected in world space (Sky City facades: windows keep their size on every tower)
    public static Material Projected(Texture2D t, float tilesPerMetre, float gloss)
    {
        if (!Tri || t == null) return null;
        var m = new Material(triS);
        m.name = "LB8 projected";
        m.color = new Color(0.5f, 0.5f, 0.5f);
        m.mainTexture = t;
        m.SetFloat("_BumpScale", 0f);
        m.SetFloat("_Scale", tilesPerMetre);
        m.SetFloat("_Glossiness", gloss);
        return m;
    }

    public static K RockFor(int th) { return th == 1 ? K.SandRock : th == 2 ? K.Basalt : th == 4 ? K.MossRock : th == 5 ? K.Concrete : K.GreyRock; }

    static readonly string[] RockTexTheme = { "rk_grey", "rk_sand", "rk_dark", "rk_grey", "rk_moss", "concrete" };
    static readonly Color[] RockTint = { new Color(0.62f, 0.58f, 0.52f), new Color(0.7f, 0.42f, 0.28f), new Color(0.24f, 0.21f, 0.2f),
        new Color(0.52f, 0.56f, 0.62f), new Color(0.42f, 0.46f, 0.36f), new Color(0.5f, 0.5f, 0.52f) };

    // ground: LB/Terrain over the theme grain + colour map (null = keep Mats.Ground)
    public static Material Terrain(Texture2D colourMap, int theme, float worldSize, float gloss)
    {
        if (Low || terrS == null) return null;
        Texture2D d = Resources.Load<Texture2D>("LBTex/g" + theme + "_d"), n = Resources.Load<Texture2D>("LBTex/g" + theme + "_n");
        if (d == null) return null;
        var m = new Material(terrS);
        m.name = "LB8 terrain " + theme;
        m.SetTexture("_ColorMap", colourMap);
        m.mainTexture = d;
        if (n != null) m.SetTexture("_BumpMap", n);
        m.SetTexture("_Macro", macro);
        Texture2D rt = T(RockTexTheme[theme]), rn = T(RockTexTheme[theme] + "_n");
        if (rt != null) m.SetTexture("_RockTex", rt);
        if (rn != null) m.SetTexture("_RockBump", rn);
        m.SetColor("_RockTint", RockTint[theme]);
        m.SetFloat("_Rep", worldSize / (theme == 5 ? 10f : 7f));
        m.SetFloat("_RockScale", theme == 1 ? 0.045f : 0.07f);
        m.SetFloat("_RockAmt", theme == 5 ? 0f : theme == 0 ? 0.7f : 1f);
        m.SetFloat("_RockSlope", theme == 1 ? 0.3f : theme == 3 ? 0.38f : 0.34f);
        m.SetFloat("_BumpScale", theme == 3 ? 0.6f : 0.9f);
        m.SetFloat("_Glossiness", gloss);
        return m;
    }

    // road: LB/Road over a hi-res markings map (null = keep the old road material)
    public static Material Road(Texture2D markings, int theme)
    {
        if (Low || roadS == null) return null;
        Texture2D g = T("asphalt"), gn = T("asphalt_n");
        if (g == null) return null;
        var m = new Material(roadS);
        m.name = "LB8 road " + theme;
        m.mainTexture = markings;
        m.SetTexture("_Grain", g);
        if (gn != null) m.SetTexture("_GrainBump", gn);
        m.SetTexture("_Macro", macro);
        m.SetVector("_GrainRep", new Vector4(5f, 10.7f, 0f, 0f));
        m.SetFloat("_Glossiness", theme == 3 ? 0.55f : theme == 1 ? 0.22f : 0.32f);
        m.SetFloat("_Wet", theme == 1 ? 0.15f : theme == 3 ? 0.7f : theme == 2 ? 0.3f : 0.55f);
        m.SetFloat("_BumpScale", 0.8f);
        return m;
    }

    // 256 x 1024 road map for 32 m of road: asphalt tint + tyre-worn lanes, white edge lines, dashed centre line, two-tone curbs
    // with a bevel shade (u = 0..1 across the strip, alpha = paint). Replaces the 64 x 256 / 16 m lambo7 map (blurry curbs).
    public static Texture2D RoadMap(Color asphalt, Color curbA, Color curbB, bool curbs, bool centreLine, int seed)
    {
        const int w = 256, h = 1024;     // 32 m -> 3.1 cm per texel along, ~6 cm across a 15 m road
        var t = new Texture2D(w, h, TextureFormat.RGBA32, true);
        t.wrapMode = TextureWrapMode.Repeat;
        t.filterMode = FilterMode.Trilinear;
        t.anisoLevel = 8;
        var r = new System.Random(seed);
        var px = new Color32[w * h];
        Color paint = new Color(0.92f, 0.92f, 0.88f);
        for (int y = 0; y < h; y++)
        {
            float vm = y * 32f / h;                       // metres along
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;
                float n = Mathf.PerlinNoise(u * 9f + seed, vm * 0.35f) - 0.5f;
                Color c = asphalt * (1f + n * 0.12f);
                float lane = Mathf.Min(Mathf.Abs(u - 0.3f), Mathf.Abs(u - 0.7f));
                c *= 0.9f + Mathf.Clamp01(lane / 0.08f) * 0.1f;          // darker rubbered racing lanes
                float a = 0f;
                if (curbs)
                {
                    if (u < 0.065f || u > 0.935f)
                    {
                        bool odd = ((int)(vm / 2f)) % 2 == 0;
                        c = odd ? curbA : curbB;
                        float e = u < 0.5f ? u / 0.065f : (1f - u) / 0.065f;        // 0 outer edge .. 1 inner
                        c *= 0.78f + 0.22f * Mathf.Sin(e * Mathf.PI);              // rounded bevel
                        float seam = Mathf.Repeat(vm, 2f);
                        if (seam < 0.05f || seam > 1.95f) c *= 0.7f;
                        a = 0.6f;
                    }
                    else if (u < 0.08f || u > 0.92f) c *= 0.7f;              // dark gap between curb and line
                    else if (u < 0.095f || u > 0.905f) { c = paint; a = 1f; } // solid white edge line
                }
                else if (u < 0.04f || u > 0.96f) c *= 0.82f;
                else if (u < 0.055f || u > 0.945f) { c = paint; a = 1f; }
                if (centreLine && Mathf.Abs(u - 0.5f) < 0.006f && Mathf.Repeat(vm, 8f) < 4.5f) { c = paint; a = 1f; }
                if (a > 0.5f) c *= 0.94f + 0.06f * (float)r.NextDouble();   // worn paint
                c.a = a;
                px[y * w + x] = c;
            }
        }
        t.SetPixels32(px);
        t.Apply(true);
        return t;
    }

    // tileable 2-channel value noise (macro breakup / wet patches)
    static Texture2D MacroNoise(int n)
    {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, true);
        t.wrapMode = TextureWrapMode.Repeat;
        t.filterMode = FilterMode.Trilinear;
        var r = new System.Random(808);
        var g = new float[2][];
        for (int c = 0; c < 2; c++)
        {
            g[c] = new float[n * n];
            for (int oct = 0; oct < 4; oct++)
            {
                int cells = 3 << oct; float amp = 1f / (1 << oct);
                var lat = new float[cells * cells];
                for (int i = 0; i < lat.Length; i++) lat[i] = (float)r.NextDouble();
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float fx = x * cells / (float)n, fy = y * cells / (float)n;
                        int x0 = (int)fx, y0 = (int)fy; float tx = fx - x0, ty = fy - y0;
                        tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty);
                        int x1 = (x0 + 1) % cells, y1 = (y0 + 1) % cells;
                        float a = Mathf.Lerp(lat[y0 * cells + x0], lat[y0 * cells + x1], tx);
                        float b = Mathf.Lerp(lat[y1 * cells + x0], lat[y1 * cells + x1], tx);
                        g[c][y * n + x] += Mathf.Lerp(a, b, ty) * amp;
                    }
            }
        }
        var px = new Color[n * n];
        for (int i = 0; i < n * n; i++) px[i] = new Color(g[0][i] / 1.875f, g[1][i] / 1.875f, 0.5f, 1f);
        t.SetPixels(px);
        t.Apply(true);
        return t;
    }

    // ---------------------------------------------------------------- grass
    static readonly List<GameObject> grassRoots = new List<GameObject>();

    // blade tufts on the open ground beside the road (theme decides colour + density); chunked meshes, no colliders
    public static void Grass(Transform parent, int theme)
    {
        grassRoots.RemoveAll(g => g == null);
        if (Low || grassS == null) return;
        int want = theme == 4 ? 11000 : theme == 0 ? 6500 : theme == 1 ? 4200 : theme == 2 ? 1400 : theme == 3 ? 1800 : 0;
        if (Look.Mobile) want = want * 6 / 10;
        if (want == 0) return;
        if (grassMat == null) { grassMat = new Material(grassS); grassMat.name = "LB8 grass"; }
        grassMat.SetFloat("_Wind", theme == 3 ? 0.5f : 1f);
        Color a, b;
        switch (theme)
        {
            case 0: a = new Color(0.55f, 0.58f, 0.3f); b = new Color(0.72f, 0.66f, 0.4f); break;     // dune grass
            case 1: a = new Color(0.52f, 0.46f, 0.26f); b = new Color(0.64f, 0.55f, 0.32f); break;    // dry desert scrub
            case 2: a = new Color(0.3f, 0.27f, 0.2f); b = new Color(0.42f, 0.36f, 0.24f); break;      // scorched tufts
            case 3: a = new Color(0.42f, 0.4f, 0.3f); b = new Color(0.55f, 0.5f, 0.36f); break;       // dry alpine grass through snow
            default: a = new Color(0.2f, 0.42f, 0.14f); b = new Color(0.32f, 0.55f, 0.18f); break;    // lush jungle
        }
        var rnd = new System.Random(500 + theme);
        System.Func<float> R = () => (float)rnd.NextDouble();
        var cells = new Dictionary<int, List<Vector4>>();     // xyz root + seed
        int placed = 0;
        float bandIn = Track.HalfW + 0.9f, bandOut = Track.HalfW + Track.Wall + 34f;
        for (int tries = 0; tries < want * 4 && placed < want; tries++)
        {
            float s = R() * Track.Length;
            float side = R() < 0.5f ? -1f : 1f;
            float lat = side * Mathf.Lerp(bandIn, bandOut, R() * R());              // denser close to the road
            Vector3 p = Track.PointAt(s, lat);
            Track.Proj m = Track.NearestFast(p);
            if (m.dist < bandIn) continue;
            Track.Proj sc = Track.NearestShortcut(p);
            if (sc.dist < Track.ScHalf + 0.8f) continue;
            float y = Track.HeightAt(p.x, p.z);
            if (y < Track.SurfaceFloor + 0.35f) continue;
            float gx = Track.HeightAt(p.x + 1f, p.z) - Track.HeightAt(p.x - 1f, p.z), gz = Track.HeightAt(p.x, p.z + 1f) - Track.HeightAt(p.x, p.z - 1f);
            if (gx * gx + gz * gz > 1.1f) continue;                                 // no tufts on cliffs (> ~28 deg)
            if (theme == 0 && y < 0.7f) continue;                                    // not on the wet beach
            if (theme == 3 && Mathf.PerlinNoise(p.x * 0.03f, p.z * 0.03f) < 0.55f) continue;   // patches only
            if (theme == 1 && Mathf.PerlinNoise(p.x * 0.05f + 3f, p.z * 0.05f) < 0.4f) continue;
            int key = Mathf.FloorToInt(p.x / 48f) * 4096 + Mathf.FloorToInt(p.z / 48f);
            List<Vector4> l;
            if (!cells.TryGetValue(key, out l)) { l = new List<Vector4>(); cells[key] = l; }
            l.Add(new Vector4(p.x, y - 0.04f, p.z, R()));
            placed++;
        }
        var root = new GameObject("Grass8");
        root.transform.SetParent(parent, false);
        grassRoots.Add(root);
        int meshes = 0;
        var v = new List<Vector3>(); var col = new List<Color>(); var uv0 = new List<Vector2>(); var uv1 = new List<Vector2>(); var idx = new List<int>();
        foreach (var kv in cells)
        {
            v.Clear(); col.Clear(); uv0.Clear(); uv1.Clear(); idx.Clear();
            foreach (var q in kv.Value)
            {
                var rr = new System.Random((int)(q.w * 1e6f));
                float hs = theme == 4 ? 0.75f : theme == 0 ? 0.6f : 0.45f;
                int blades = 5 + rr.Next(3);
                Color cb = Color.Lerp(a, b, (float)rr.NextDouble());
                for (int k = 0; k < blades; k++)
                {
                    float ang = (float)rr.NextDouble() * Mathf.PI * 2f, off = (float)rr.NextDouble() * 0.18f;
                    Vector3 root0 = new Vector3(q.x + Mathf.Cos(ang) * off, q.y, q.z + Mathf.Sin(ang) * off);
                    float h = hs * (0.55f + 0.7f * (float)rr.NextDouble()), wd = 0.035f + 0.03f * (float)rr.NextDouble();
                    float face = (float)rr.NextDouble() * Mathf.PI * 2f;
                    Vector3 side = new Vector3(Mathf.Cos(face), 0f, Mathf.Sin(face)) * wd;
                    Vector3 lean = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * h * (0.25f + 0.35f * (float)rr.NextDouble());
                    int bi = v.Count;
                    Color c = cb * (0.85f + 0.3f * (float)rr.NextDouble()); c.a = 1f;
                    // 5 verts: root pair, mid pair (bent), tip
                    v.Add(root0 - side); v.Add(root0 + side);
                    v.Add(root0 - side * 0.7f + Vector3.up * h * 0.55f + lean * 0.35f); v.Add(root0 + side * 0.7f + Vector3.up * h * 0.55f + lean * 0.35f);
                    v.Add(root0 + Vector3.up * h + lean);
                    float[] hh = { 0f, 0f, 0.55f, 0.55f, 1f };
                    for (int j = 0; j < 5; j++) { col.Add(c); uv0.Add(new Vector2(hh[j], q.y)); uv1.Add(new Vector2(q.x, q.z)); }
                    idx.Add(bi); idx.Add(bi + 2); idx.Add(bi + 1);
                    idx.Add(bi + 1); idx.Add(bi + 2); idx.Add(bi + 3);
                    idx.Add(bi + 2); idx.Add(bi + 4); idx.Add(bi + 3);
                }
            }
            if (v.Count == 0) continue;
            var mesh = Track.Own(new Mesh());
            mesh.name = "LB8 grass";
            if (v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(v); mesh.SetColors(col); mesh.SetUVs(0, uv0); mesh.SetUVs(1, uv1); mesh.SetTriangles(idx, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var b2 = mesh.bounds; b2.Expand(new Vector3(1f, 1f, 1f)); mesh.bounds = b2;
            var go = new GameObject("Grass chunk");
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = grassMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
            meshes++;
        }
        ApplyLevel(LBDisplay.Level, Look.Mobile);
        Debug.Log("Gfx8 grass: " + placed + " tufts in " + meshes + " chunks (theme " + theme + ")");
    }

    // ---------------------------------------------------------------- trees (procedural meshes, cached variants)
    static Mesh[] pineF, pineT, broadF, broadT;

    static float Hash(int a) { unchecked { uint x = (uint)a * 747796405u + 2891336453u; x = ((x >> (int)((x >> 28) + 4u)) ^ x) * 277803737u; return ((x >> 22) ^ x) / 4294967295f; } }

    // tapered n-sided trunk from y0 to y1 (unit tree), slight lean
    static Mesh Trunk(float r0, float r1, float y1, float lean, int seed)
    {
        const int sides = 7, rings = 4;
        var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
        for (int k = 0; k <= rings; k++)
        {
            float f = k / (float)rings, y = f * y1, r = Mathf.Lerp(r0 * 1.35f, r1, Mathf.Sqrt(f)) * (k == 0 ? 1.2f : 1f);
            float ox = Mathf.Sin(f * 2.2f + seed) * lean * f, oz = Mathf.Cos(f * 1.7f + seed * 0.7f) * lean * f;
            for (int i = 0; i <= sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                v.Add(new Vector3(ox + Mathf.Cos(a) * r, y, oz + Mathf.Sin(a) * r));
                c.Add(new Color(0.75f + 0.25f * f, 0f, 0f, 1f));
            }
        }
        for (int k = 0; k < rings; k++)
            for (int i = 0; i < sides; i++)
            {
                int a = k * (sides + 1) + i, b = a + sides + 1;
                t.Add(a); t.Add(b); t.Add(a + 1); t.Add(a + 1); t.Add(b); t.Add(b + 1);
            }
        var m = new Mesh(); m.SetVertices(v); m.SetColors(c); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
        m.name = "LB8 trunk";
        return m;
    }

    // conifer: 6-8 jagged, drooping needle skirts with a closed underside; soft "volumetric" normals (blend toward the
    // crown's outward direction) + vertex AO (underside / inner dark, tips bright)
    static Mesh PineFoliage(int seed)
    {
        var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>(); var nn = new List<Vector3>();
        int tiers = 6 + (seed % 3);
        float baseY = 0.2f;
        for (int k = 0; k < tiers; k++)
        {
            float f = k / (float)(tiers - 1);
            float top = baseY + (k + 1f) / tiers * 0.8f + 0.04f;
            float rad = 0.42f * Mathf.Pow(1f - f * 0.88f, 1.05f) + 0.03f;
            float droop = 0.13f + rad * 0.45f;
            const int seg = 14;
            int apex = v.Count;
            v.Add(new Vector3(0f, top, 0f)); c.Add(new Color(0.8f, 0f, 0f, 1f));
            int rim = v.Count;
            for (int i = 0; i < seg; i++)
            {
                float a = (i + Hash(seed * 31 + k * 7) ) / seg * Mathf.PI * 2f;
                float jag = (i % 2 == 0 ? 1f : 0.74f) * (0.88f + 0.24f * Hash(seed * 131 + k * 17 + i));
                float r = rad * jag;
                v.Add(new Vector3(Mathf.Cos(a) * r, top - droop * (i % 2 == 0 ? 1f : 0.82f), Mathf.Sin(a) * r));
                c.Add(new Color(i % 2 == 0 ? 1f : 0.82f, 0f, 0f, 1f));
            }
            int inner = v.Count;
            for (int i = 0; i < seg; i++)
            {
                float a = (i + Hash(seed * 31 + k * 7)) / seg * Mathf.PI * 2f;
                v.Add(new Vector3(Mathf.Cos(a) * rad * 0.22f, top - droop * 0.78f, Mathf.Sin(a) * rad * 0.22f));
                c.Add(new Color(0.38f, 0f, 0f, 1f));
            }
            for (int i = 0; i < seg; i++)
            {
                int i1 = (i + 1) % seg;
                t.Add(apex); t.Add(rim + i1); t.Add(rim + i);                              // top cone
                t.Add(rim + i); t.Add(rim + i1); t.Add(inner + i);                          // underside
                t.Add(inner + i); t.Add(rim + i1); t.Add(inner + i1);
            }
        }
        var m = new Mesh(); m.SetVertices(v); m.SetColors(c); m.SetTriangles(t, 0); m.RecalculateNormals();
        var n = m.normals;
        for (int i = 0; i < n.Length; i++)
        {
            Vector3 o = v[i] - new Vector3(0f, 0.5f, 0f); o.y *= 0.5f;
            n[i] = (n[i] * 0.45f + o.normalized * 0.55f).normalized;
        }
        m.normals = n; m.RecalculateTangents(); m.RecalculateBounds();
        m.name = "LB8 pine";
        return m;
    }

    // broadleaf canopy: 5-7 noise-displaced blobs around the crown, AO darker underneath and toward the core
    static Mesh BroadFoliage(int seed)
    {
        var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
        Vector3 crown = new Vector3(0f, 0.74f, 0f);
        int blobs = 5 + seed % 3;
        Vector3[] ico; int[] tri;
        Ico(2, out ico, out tri);
        for (int b = 0; b < blobs; b++)
        {
            float a = b / (float)blobs * Mathf.PI * 2f + Hash(seed * 13 + b);
            float d = b == 0 ? 0f : 0.16f + 0.1f * Hash(seed * 7 + b);
            Vector3 cen = crown + new Vector3(Mathf.Cos(a) * d, (b == 0 ? 0.08f : -0.04f + 0.12f * Hash(seed * 3 + b)), Mathf.Sin(a) * d);
            float r = (b == 0 ? 0.26f : 0.17f + 0.07f * Hash(seed * 5 + b));
            int bi = v.Count;
            foreach (var q in ico)
            {
                float nz = Mathf.PerlinNoise(q.x * 2.3f + seed + b * 3.1f, q.z * 2.3f + q.y * 1.7f) - 0.5f;
                Vector3 p = cen + Vector3.Scale(q, new Vector3(1f, 0.78f, 1f)) * r * (1f + nz * 0.45f);
                v.Add(p);
                float ao = Mathf.Clamp01(0.55f + q.y * 0.35f + (p - crown).magnitude * 0.9f);
                c.Add(new Color(ao, 0f, 0f, 1f));
            }
            foreach (int i in tri) t.Add(bi + i);
        }
        var m = new Mesh(); m.SetVertices(v); m.SetColors(c); m.SetTriangles(t, 0); m.RecalculateNormals();
        var n = m.normals;
        for (int i = 0; i < n.Length; i++) n[i] = (n[i] * 0.4f + (v[i] - crown).normalized * 0.6f).normalized;
        m.normals = n; m.RecalculateTangents(); m.RecalculateBounds();
        m.name = "LB8 canopy";
        return m;
    }

    static void Ico(int sub, out Vector3[] verts, out int[] tris)
    {
        float g = (1f + Mathf.Sqrt(5f)) * 0.5f;
        var v = new List<Vector3> {
            new Vector3(-1, g, 0), new Vector3(1, g, 0), new Vector3(-1, -g, 0), new Vector3(1, -g, 0),
            new Vector3(0, -1, g), new Vector3(0, 1, g), new Vector3(0, -1, -g), new Vector3(0, 1, -g),
            new Vector3(g, 0, -1), new Vector3(g, 0, 1), new Vector3(-g, 0, -1), new Vector3(-g, 0, 1) };
        for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
        var f = new List<int> { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
        for (int s = 0; s < sub; s++)
        {
            var mid = new Dictionary<long, int>();
            var nf = new List<int>();
            System.Func<int, int, int> M = (a, b) =>
            {
                long k = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                int r;
                if (mid.TryGetValue(k, out r)) return r;
                v.Add(((v[a] + v[b]) * 0.5f).normalized); r = v.Count - 1; mid[k] = r; return r;
            };
            for (int i = 0; i < f.Count; i += 3)
            {
                int a = f[i], b = f[i + 1], c = f[i + 2];
                int ab = M(a, b), bc = M(b, c), ca = M(c, a);
                nf.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            f = nf;
        }
        verts = v.ToArray(); tris = f.ToArray();
    }

    static void EnsureTrees()
    {
        if (pineF != null) return;
        pineF = new Mesh[5]; pineT = new Mesh[5]; broadF = new Mesh[4]; broadT = new Mesh[4];
        for (int i = 0; i < 5; i++) { pineF[i] = PineFoliage(i + 1); pineT[i] = Trunk(0.035f, 0.012f, 0.62f, 0.01f, i); }
        for (int i = 0; i < 4; i++) { broadF[i] = BroadFoliage(i + 3); broadT[i] = Trunk(0.05f, 0.028f, 0.7f, 0.05f, i + 9); }
    }

    static void Put(Transform parent, Mesh mesh, Material mat, Vector3 p, float yaw, Vector3 scale)
    {
        var g = new GameObject("LB8 tree part");
        g.transform.SetParent(parent, false);
        g.transform.position = p;
        g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        g.transform.localScale = scale;
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        g.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    // a conifer of height h at p (false = not available, caller builds the old one)
    public static bool Pine(Transform parent, Vector3 p, float h, Color leaf, Color bark, int seed)
    {
        if (!Tri) return false;
        EnsureTrees();
        int k = Mathf.Abs(seed) % pineF.Length;
        float yaw = Hash(seed) * 360f, wide = 0.85f + 0.3f * Hash(seed + 5);
        Put(parent, pineT[k], Surf(bark, K.Bark), p, yaw, new Vector3(h, h, h));
        Put(parent, pineF[k], Surf(leaf, K.Leaf), p, yaw, new Vector3(h * wide, h, h * wide));
        return true;
    }

    public static bool Broadleaf(Transform parent, Vector3 p, float h, Color leaf, Color bark, int seed)
    {
        if (!Tri) return false;
        EnsureTrees();
        int k = Mathf.Abs(seed) % broadF.Length;
        float yaw = Hash(seed) * 360f, wide = 0.9f + 0.35f * Hash(seed + 5);
        Put(parent, broadT[k], Surf(bark, K.Bark), p, yaw, new Vector3(h, h, h));
        Put(parent, broadF[k], Surf(leaf, K.Leaf), p, yaw, new Vector3(h * wide, h, h * wide));
        return true;
    }

    // re-skins a spawned LB pack (rocks) with the triplanar rock surface
    public static void Reskin(Transform g, Color tint, K k)
    {
        if (!Tri || g == null) return;
        Material m = Surf(tint, k);
        foreach (var r in g.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = m;
    }

    // ---------------------------------------------------------------- grandstand beside the start straight
    public static void Grandstand(Transform parent, Vector3 p, Quaternion rot, float length, Color accent)
    {
        if (Low) return;
        var t = new GameObject("Grandstand").transform;
        t.SetParent(parent, false);
        t.position = p; t.rotation = rot;
        Material conc = Surf(new Color(0.62f, 0.62f, 0.6f), K.Concrete), steel = Mats.PBR(new Color(0.55f, 0.57f, 0.6f), 0.6f, 0.7f);
        Material seatA = Mats.Lit(accent), seatB = Mats.Lit(Color.Lerp(accent, Color.white, 0.35f)), roofM = Mats.PBR(new Color(0.9f, 0.9f, 0.92f), 0.55f, 0.2f);
        const int rows = 7;
        for (int i = 0; i < rows; i++)
        {
            float z = i * 1.1f, y = i * 0.75f;
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, y * 0.5f + 0.2f, z), new Vector3(length, y + 0.4f, 1.1f), conc);
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, y + 0.62f, z + 0.15f), new Vector3(length - 0.6f, 0.35f, 0.45f), i % 2 == 0 ? seatA : seatB);
        }
        float back = rows * 1.1f;
        Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, rows * 0.75f * 0.5f + 1.2f, back), new Vector3(length, rows * 0.75f + 2.4f, 0.4f), conc);
        for (int s = 0; s <= 4; s++)
        {
            float x = Mathf.Lerp(-length * 0.5f + 0.5f, length * 0.5f - 0.5f, s / 4f);
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(x, rows * 0.75f + 2.5f, back - 0.2f), new Vector3(0.3f, 5f, 0.3f), steel);
        }
        var roof = Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, rows * 0.75f + 5.1f, back * 0.5f), new Vector3(length + 1f, 0.25f, back + 2.5f), roofM);
        roof.transform.localRotation = Quaternion.Euler(-6f, 0f, 0f);
        // light towers at both ends
        for (int s = -1; s <= 1; s += 2)
        {
            Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(s * (length * 0.5f + 2f), 7f, back * 0.5f), new Vector3(0.35f, 7f, 0.35f), steel);
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(s * (length * 0.5f + 2f), 14.2f, back * 0.5f - 0.4f), new Vector3(2.6f, 1.4f, 0.4f), steel);
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(s * (length * 0.5f + 2f), 14.2f, back * 0.5f - 0.62f), new Vector3(2.3f, 1.1f, 0.06f), Mats.Unlit(new Color(1f, 0.97f, 0.88f)));
        }
    }
}
