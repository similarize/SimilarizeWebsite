using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// u6d (Unity 6.6 visual upgrade): everything that makes the URP build look clearly different from the 2022.3 lambo9 build.
//  * scanned CC0 PBR surfaces (ambientCG, Resources/U6T/<name>_c.jpg albedo + <name>_d.png packed data: r roughness, g normal Y,
//    b AO, a normal X) per track on LB/Terrain (2 ground layers + triplanar rock), LB/Road (asphalt) and LB/Tri rock props;
//    loaded per track, the previous track's textures are unloaded after a world rebuild
//  * GPU-instanced grass (LB/GrassI) and foliage (pines / broadleaf trees / bushes on LB/Tri with wind sway + distance shrink),
//    Graphics.RenderMeshInstanced per 32 m / 96 m cell, density + distance by device tier and LBDisplay level, no colliders
//  * atmosphere: height fog + sun in-scattering in every LB shader (globals _LBAtmos*), screen-space god rays + heat haze as a
//    URP FullScreenPassRendererFeature (LB/Shafts, desktop / WebGPU temporal-AA path, single view, level 0)
//  * sharper car reflections: HDR island probe + a realtime, time-sliced probe that follows the player camera (desktop, level 0)
// Off under ?gfx=low (lambo7 look); ?u6d=0 turns off the instanced foliage / atmosphere / shafts / car probe / HDR glows (surfaces stay). URL tuning: u6grass= u6fol= u6fog= u6shaft= u6probe=0|1 u6sunflip=1
public static class Gfx6
{
    public static bool On { get; private set; }
    static float grassK = 1f, folK = 1f, fogK = 1f, shaftK = 1f;
    static int probeForce = -1;
    static bool sunFlip;
    static Material shaftsMat;
    static ScriptableRendererFeature shafts;
    static Shader grassS, triS;
    static Light sun;
    static GameObject runnerGo;

    // ---------------------------------------------------------------- textures
    class TexE { public Texture2D t; public int gen; }
    static readonly Dictionary<string, TexE> texs = new Dictionary<string, TexE>();
    static int gen;
    // linear mean albedo of each scan (lambou6/u6d/prep_tex6.py -> means.json)
    static readonly Dictionary<string, Color> Mean = new Dictionary<string, Color> {
        { "asphalt", new Color(0.0254f, 0.025f, 0.0223f) }, { "sand", new Color(0.4619f, 0.3426f, 0.1651f) },
        { "grass_dry", new Color(0.1269f, 0.1613f, 0.0343f) }, { "rock_grey", new Color(0.1892f, 0.1791f, 0.1365f) },
        { "desert", new Color(0.4657f, 0.3237f, 0.1756f) }, { "rock_red", new Color(0.2036f, 0.0911f, 0.0397f) },
        { "ash", new Color(0.145f, 0.1188f, 0.0914f) }, { "gravel_dark", new Color(0.1329f, 0.1325f, 0.1117f) },
        { "basalt", new Color(0.0072f, 0.0134f, 0.0159f) }, { "snow", new Color(0.7358f, 0.8616f, 0.9752f) },
        { "snow_ground", new Color(0.3872f, 0.4081f, 0.3752f) }, { "rock_alpine", new Color(0.1073f, 0.1054f, 0.0995f) },
        { "grass", new Color(0.0604f, 0.1091f, 0.0226f) }, { "forest_dirt", new Color(0.3306f, 0.3062f, 0.1114f) },
        { "rock_moss", new Color(0.2399f, 0.2063f, 0.0503f) }, { "concrete", new Color(0.12f, 0.1098f, 0.1007f) } };
    public const string CreditLine = "<b>u6 scanned PBR surfaces</b> (ambientCG.com, CC0 1.0): Asphalt025C, Ground080, Grass004, Rock051, Ground079L, Rock029, " +
        "Ground110, Gravel040, Rock035, Snow010A, Snow015, Rock050, Grass001, Ground037, Rock063, Concrete042A.\n";

    static Texture2D Tex(string name)
    {
        TexE e;
        if (!texs.TryGetValue(name, out e)) { e = new TexE(); texs[name] = e; }
        if (e.t == null)
        {
            e.t = Resources.Load<Texture2D>("U6T/" + name);
            if (e.t == null) Debug.LogWarning("U6D: texture U6T/" + name + " missing");
        }
        e.gen = gen;
        return e.t;
    }
    static Color M(string n) { Color c; return Mean.TryGetValue(n, out c) ? c : new Color(0.3f, 0.3f, 0.3f); }

    // a world rebuild starts (first ground material) / ends (Look.SetTheme): unload the scans the new track does not use
    static void BeginWorld() { gen++; }
    public static void EndWorld()
    {
        int n = 0;
        foreach (var kv in texs)
            if (kv.Value.t != null && kv.Value.gen < gen) { Resources.UnloadAsset(kv.Value.t); kv.Value.t = null; n++; }
        if (n > 0) Debug.Log("U6D: unloaded " + n + " scans of the previous track");
    }

    // ---------------------------------------------------------------- init
    public static void Init(Material shaftMaterial, ScriptableRendererFeature shaftFeature, Light s)
    {
        string u = Application.absoluteURL ?? "";
        On = !Gfx.Low && !u.Contains("u6d=0");
        grassK = Qf("u6grass", 1f); folK = Qf("u6fol", 1f); fogK = Qf("u6fog", 1f); shaftK = Qf("u6shaft", 1f);
        string p = U6.Q("u6probe"); if (p == "0") probeForce = 0; else if (p == "1") probeForce = 1;
        sunFlip = U6.Q("u6sunflip") == "1";
        shaftsMat = shaftMaterial; shafts = shaftFeature; sun = s;
        if (shafts != null) shafts.SetActive(false);
        grassS = Resources.Load<Shader>("LBGrassI");
        if (grassS != null && !grassS.isSupported) { Debug.LogWarning("U6D: LB/GrassI unsupported"); grassS = null; }
        triS = Resources.Load<Shader>("LBTri");
        RenderPipelineManager.beginCameraRendering += BeginCam;
        runnerGo = new GameObject("U6D draw");
        Object.DontDestroyOnLoad(runnerGo);
        runnerGo.AddComponent<Gfx6Draw>();
        SetAmbientGlobals();
        Shader.SetGlobalVector("_LBAtmos", Vector4.zero);
        Debug.Log("U6D: " + (On ? "on" : "off (gfx=low or u6d=0)") + " | instancing " + SystemInfo.supportsInstancing + " grassI " + (grassS != null) +
            " | shafts " + (shafts != null) + "/" + (shaftsMat != null) + " | k grass " + grassK + " fol " + folK + " fog " + fogK + " shaft " + shaftK);
    }

    static float Qf(string n, float d)
    {
        string s = U6.Q(n); float f;
        if (s != null && float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f)) return Mathf.Max(0f, f);
        return d;
    }

    // ---------------------------------------------------------------- per-track surfaces
    // ground layers A (main), B (patches / green areas), R (rock on slopes) per theme
    static readonly string[,] Layers = {
        { "sand", "grass_dry", "rock_grey" }, { "desert", "gravel_dark", "rock_red" }, { "ash", "gravel_dark", "basalt" },
        { "snow", "snow_ground", "rock_alpine" }, { "grass", "forest_dirt", "rock_moss" }, { "concrete", "gravel_dark", "concrete" } };
    static readonly Vector4[] BMask = { new Vector4(0.5f, 6f, 25f, 1f), new Vector4(0.6f, 5f, 0f, 0.7f), new Vector4(0.5f, 5f, 0f, 0.85f),
        new Vector4(0.57f, 5f, 0f, 0.8f), new Vector4(0.55f, 5f, -18f, 1f), new Vector4(0.7f, 4f, 0f, 0f) };
    static readonly float[] ATile = { 3.5f, 3.5f, 3.2f, 3.5f, 2.6f, 3f };      // metres per repeat
    static readonly float[] BTile = { 2.4f, 2.5f, 2.5f, 3f, 3f, 2.5f };
    static readonly float[] MapTint = { 0.5f, 0.6f, 0.55f, 0.45f, 0.5f, 0.75f };
    static readonly float[] SmoothK = { 0.75f, 0.7f, 0.72f, 0.9f, 0.72f, 0.75f };

    public static void Terrain(Material m, Texture2D cmap, int th, float worldSize)
    {
        if (m == null) return;           // always (the u6d LB/Terrain needs its layers; ?gfx=low never gets here)
        BeginWorld();
        th = Mathf.Clamp(th, 0, 5);
        string a = Layers[th, 0], b = Layers[th, 1], r = Layers[th, 2];
        Texture2D ac = Tex(a + "_c"), ad = Tex(a + "_d"), bc = Tex(b + "_c"), bd = Tex(b + "_d"), rc = Tex(r + "_c"), rd = Tex(r + "_d");
        if (ac != null) m.SetTexture("_ACol", ac);
        if (ad != null) m.SetTexture("_AData", ad);
        if (bc != null) m.SetTexture("_BCol", bc);
        if (bd != null) m.SetTexture("_BData", bd);
        if (rc != null) m.SetTexture("_RCol", rc);
        if (rd != null) m.SetTexture("_RData", rd);
        // u6e: the means are LINEAR values -> SetVector (SetColor gamma->linear converts them again in Linear space: ~10x too small,
        // so scan / mean blew the road and ground up to near white in u6d)
        m.SetVector("_AMean", (Vector4)M(a)); m.SetVector("_BMean", (Vector4)M(b)); m.SetVector("_RMean", (Vector4)M(r));
        m.SetVector("_BMask", BMask[th]);
        m.SetFloat("_ARep", worldSize / ATile[th]);
        m.SetFloat("_BRep", worldSize / BTile[th]);
        m.SetFloat("_RockScale", th == 1 ? 1f / 9f : 1f / 6f);
        m.SetFloat("_MapTint", MapTint[th]);
        m.SetFloat("_SmoothK", SmoothK[th]);
        m.SetFloat("_BumpScale", th == 3 ? 0.8f : 1.05f);
        Color mm = new Color(0.3f, 0.3f, 0.3f);
        try
        {
            int mip = Mathf.Max(0, cmap.mipmapCount - 5);
            Color[] px = cmap.GetPixels(mip);
            Color sum = Color.black;
            foreach (var c in px) sum += c;
            if (px.Length > 0) mm = (sum / px.Length).linear;
        }
        catch (System.Exception e) { Debug.LogWarning("U6D: colour map mean " + e.Message); }
        mm.a = 1f;
        m.SetVector("_MapMean", (Vector4)mm);                // u6e: linear -> SetVector
        // old (u6c) props stay set for the LB_LITE fallback-free path; log what took
        Debug.Log("U6D terrain " + th + ": " + a + " / " + b + " / " + r + " tex " + (ac != null && ad != null) + (bc != null && bd != null) + (rc != null && rd != null) +
            " map mean " + mm.r.ToString("F3") + "," + mm.g.ToString("F3") + "," + mm.b.ToString("F3"));
    }

    public static void Road(Material m, int th)
    {
        if (m == null) return;
        Texture2D c = Tex("asphalt_c"), d = Tex("asphalt_d");
        if (c != null) m.SetTexture("_Grain", c);
        if (d != null) m.SetTexture("_GrainData", d);
        m.SetVector("_GrainMean", (Vector4)M("asphalt"));   // u6e: linear -> SetVector
        m.SetFloat("_GrainCon", th == 3 ? 0.7f : 0.85f);
        m.SetVector("_GrainRep", new Vector4(6f, 12.8f, 0f, 0f));
        m.SetFloat("_BumpScale", 1f);
    }

    // LB/Tri rock / snow / concrete props on the scans (other kinds keep the lambo8 detail maps)
    public static string SurfName(Gfx.K k, int th)
    {
        switch (k)
        {
            case Gfx.K.SandRock: return "rock_red";
            case Gfx.K.Basalt: return "basalt";
            case Gfx.K.GreyRock: return th == 3 ? "rock_alpine" : "rock_grey";
            case Gfx.K.MossRock: return "rock_moss";
            case Gfx.K.Snow: return "snow";
            case Gfx.K.Concrete: return "concrete";
            case Gfx.K.Gravel: return "gravel_dark";
        }
        return null;
    }
    public static void Surf(Material m, Gfx.K k, int th)
    {
        if (!On || m == null) return;
        string n = SurfName(k, th);
        if (n == null) return;
        Texture2D c = Tex(n + "_c"), d = Tex(n + "_d");
        if (c == null || d == null) return;
        m.mainTexture = c;
        m.SetTexture("_DataMap", d);
        m.SetVector("_Mean", (Vector4)M(n));                  // u6e: linear -> SetVector
        m.SetFloat("_Full", 1f);
        m.SetFloat("_TintK", k == Gfx.K.Snow || k == Gfx.K.Concrete ? 0.5f : 0.65f);
        m.SetFloat("_BumpScale", 1.1f);
    }

    // ---------------------------------------------------------------- atmosphere + shafts
    static readonly float[] FogF = { 0.45f, 0.5f, 0.7f, 0.5f, 0.65f, 0.45f };
    static readonly float[] FogFall = { 1f / 70f, 1f / 90f, 1f / 60f, 1f / 80f, 1f / 40f, 1f / 55f };
    static readonly float[] Scatter = { 0.9f, 1f, 1.2f, 0.7f, 0.8f, 0.9f };
    static readonly float[] ShaftI = { 0.5f, 0.45f, 0.6f, 0.55f, 0.7f, 0.5f };
    static readonly float[] Haze = { 0f, 0.6f, 1f, 0f, 0f, 0f };
    static Vector4 shaftSS, hazeV;

    static void SetAmbientGlobals()
    {
        Shader.SetGlobalVector("_LBAmbSky", (Vector4)RenderSettings.ambientSkyColor.linear);
        Shader.SetGlobalVector("_LBAmbEq", (Vector4)RenderSettings.ambientEquatorColor.linear);
        Shader.SetGlobalVector("_LBAmbGr", (Vector4)RenderSettings.ambientGroundColor.linear);
    }

    public static void Atmos(int th, Color fog, float fogD)
    {
        th = Mathf.Clamp(th, 0, 5);
        SetAmbientGlobals();
        bool on = On && fogK > 0f;
        float baseY = 0f;
        if (Track.C != null && Track.C.Length > 0) { foreach (var p in Track.C) baseY += p.y; baseY /= Track.C.Length; }
        baseY -= 6f;
        Color sc = sun != null ? sun.color : Color.white;
        Shader.SetGlobalVector("_LBAtmos", new Vector4(on ? 1f : 0f, fogD * FogF[th] * fogK, FogFall[th], baseY));
        Color fl = fog.linear; fl.a = 0.94f;
        Shader.SetGlobalVector("_LBAtmosCol", fl);
        Color sl = Color.Lerp(fog, sc, 0.6f).linear * Scatter[th];
        Shader.SetGlobalVector("_LBAtmosSun", new Vector4(sl.r, sl.g, sl.b, 0.76f));
        Color shc = sc.linear;
        Shader.SetGlobalVector("_LBShaftCol", new Vector4(shc.r, shc.g, shc.b, 1f));
        shaftSS = new Vector4(0.5f, 0.5f, 0f, ShaftI[th] * shaftK);
        hazeV = new Vector4(On ? Haze[th] : 0f, 1f, 0f, 0f);
        Shader.SetGlobalVector("_LBHaze", hazeV);
        EndWorld();
        Debug.Log("U6D atmos theme " + th + " " + (on ? "height fog dens " + (fogD * FogF[th] * fogK).ToString("F5") + " falloff " + FogFall[th].ToString("F4") + " base " + baseY.ToString("F1") : "off (Unity fog)") +
            " shafts " + shaftSS.w.ToString("F2") + " haze " + hazeV.x);
    }

    static Vector4 savedAtmos; static bool studio;
    public static void StudioBegin() { if (studio) return; studio = true; savedAtmos = Shader.GetGlobalVector("_LBAtmos"); Shader.SetGlobalVector("_LBAtmos", Vector4.zero); }
    public static void StudioEnd() { if (!studio) return; studio = false; Shader.SetGlobalVector("_LBAtmos", savedAtmos); }

    static bool shaftsOn;
    static void BeginCam(ScriptableRenderContext ctx, Camera c)
    {
        if (!shaftsOn || c == null) return;
        bool main = c.cameraType == CameraType.Game && c.targetTexture == null && (c.cullingMask & 1) != 0 && sun != null;
        if (!main) { Shader.SetGlobalVector("_LBSunSS", Vector4.zero); Shader.SetGlobalVector("_LBHaze", Vector4.zero); return; }
        Vector3 sd = -sun.transform.forward;
        Vector3 vp = c.WorldToViewportPoint(c.transform.position + sd * 2000f);
        float facing = Vector3.Dot(c.transform.forward, sd);
        float vis = vp.z > 0f ? Mathf.Clamp01(facing * 1.8f - 0.25f) : 0f;
        Shader.SetGlobalVector("_LBSunSS", new Vector4(vp.x, sunFlip ? 1f - vp.y : vp.y, vis, shaftSS.w));
        Shader.SetGlobalVector("_LBHaze", hazeV);
    }

    // ---------------------------------------------------------------- views / levels
    static int views, level;
    static bool mobile;
    public static void ApplyViews(int v, int lvl, bool mob)
    {
        views = v; level = lvl; mobile = mob;
        var a = U6.Asset;
        bool temporal = a != null && a.msaaSampleCount <= 1 && U6.WebGPU;
        bool want = On && shafts != null && shaftsMat != null && shaftK > 0f && v == 1 && !mob && lvl == 0 && temporal;
        if (want != shaftsOn || (shafts != null && shafts.isActive != want))
        {
            shaftsOn = want;
            if (shafts != null) shafts.SetActive(want);
            Debug.Log("U6D: god rays / heat haze " + (want ? "on" : "off") + " (views " + v + " level " + lvl + (mob ? " mobile" : "") + (temporal ? " temporal AA" : " msaa") + ")");
        }
        bool probe = On && !mob && v == 1 && lvl == 0;
        if (probeForce >= 0) probe = On && probeForce == 1 && v >= 1;
        Gfx6Draw.SetProbe(probe);
        ApplyLevel(lvl, mob);
    }

    // density fraction + draw distance per layer kind for the current tier / level
    public static float GrassFrac = 1f, FolFrac = 1f, TreeDist = 650f, BushDist = 220f;
    static Material grassMat;
    static readonly List<Material> folMats = new List<Material>();
    public static void ApplyLevel(int lvl, bool mob)
    {
        level = lvl; mobile = mob;
        Vector4 f = lvl == 0 ? (mob ? new Vector4(35f, 60f, 0f, 0f) : new Vector4(70f, 120f, 0f, 0f)) : lvl == 1 ? (mob ? new Vector4(24f, 42f, 0f, 0f) : new Vector4(40f, 70f, 0f, 0f)) : Vector4.zero;
        GrassFrac = lvl >= 2 ? 0f : lvl == 1 ? (mob ? 0.5f : 0.6f) : 1f;
        FolFrac = lvl >= 2 ? 0.35f : lvl == 1 ? 0.6f : 1f;
        TreeDist = (mob ? 320f : 650f) * (lvl >= 1 ? 0.7f : 1f);
        BushDist = (mob ? 110f : 220f) * (lvl >= 2 ? 0f : lvl == 1 ? 0.7f : 1f);
        if (grassMat != null) grassMat.SetVector("_Fade", f);
        Gfx6Draw.GrassDist = f.y;
        foreach (var m in folMats)
        {
            if (m == null) continue;
            bool bush = m.name.Contains("bush");
            float d = bush ? BushDist : TreeDist;
            m.SetVector("_Fade", new Vector4(d * 0.82f, d, 0f, 0f));
        }
    }

    // ---------------------------------------------------------------- distance-to-road grid (main road + shortcut edges)
    static float[] edge; static int en; static float ex0, ez0; const float ECell = 4f;
    static void BuildEdgeGrid()
    {
        Rect r = Track.TerrainRect;
        ex0 = r.xMin - 20f; ez0 = r.yMin - 20f;
        en = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(r.width, r.height) + 40f) / ECell) + 1, 16, 700);
        edge = new float[en * en];
        for (int i = 0; i < edge.Length; i++) edge[i] = 1e6f;
        Stamp(Track.C, Track.HalfW);
        Stamp(Track.SC, Track.ScHalf);
        // two-pass chamfer (3-4) distance transform in metres
        float d1 = ECell, d2 = ECell * 1.41421f;
        for (int z = 0; z < en; z++)
            for (int x = 0; x < en; x++)
            {
                int i = z * en + x; float v = edge[i];
                if (x > 0) v = Mathf.Min(v, edge[i - 1] + d1);
                if (z > 0) { v = Mathf.Min(v, edge[i - en] + d1); if (x > 0) v = Mathf.Min(v, edge[i - en - 1] + d2); if (x < en - 1) v = Mathf.Min(v, edge[i - en + 1] + d2); }
                edge[i] = v;
            }
        for (int z = en - 1; z >= 0; z--)
            for (int x = en - 1; x >= 0; x--)
            {
                int i = z * en + x; float v = edge[i];
                if (x < en - 1) v = Mathf.Min(v, edge[i + 1] + d1);
                if (z < en - 1) { v = Mathf.Min(v, edge[i + en] + d1); if (x < en - 1) v = Mathf.Min(v, edge[i + en + 1] + d2); if (x > 0) v = Mathf.Min(v, edge[i + en - 1] + d2); }
                edge[i] = v;
            }
    }
    static void Stamp(Vector3[] line, float half)
    {
        if (line == null) return;
        for (int k = 0; k < line.Length; k++)
        {
            Vector3 a = line[k], b = line[Mathf.Min(k + 1, line.Length - 1)];
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / 2f));
            for (int st = 0; st < steps; st++)
            {
                Vector3 p = Vector3.Lerp(a, b, st / (float)steps);
                int cx = Mathf.FloorToInt((p.x - ex0) / ECell), cz = Mathf.FloorToInt((p.z - ez0) / ECell);
                int rr = Mathf.CeilToInt((half + 8f) / ECell);
                for (int z = cz - rr; z <= cz + rr; z++)
                    for (int x = cx - rr; x <= cx + rr; x++)
                    {
                        if (x < 0 || z < 0 || x >= en || z >= en) continue;
                        float wx = ex0 + (x + 0.5f) * ECell, wz = ez0 + (z + 0.5f) * ECell;
                        float d = Mathf.Sqrt((wx - p.x) * (wx - p.x) + (wz - p.z) * (wz - p.z)) - half;
                        int i = z * en + x;
                        if (d < edge[i]) edge[i] = d;
                    }
            }
        }
    }
    // metres from the nearest road / shortcut edge (negative = on the road), bilinear
    static float Edge(float x, float z)
    {
        if (edge == null) return 1e6f;
        float fx = (x - ex0) / ECell - 0.5f, fz = (z - ez0) / ECell - 0.5f;
        int ix = Mathf.Clamp(Mathf.FloorToInt(fx), 0, en - 2), iz = Mathf.Clamp(Mathf.FloorToInt(fz), 0, en - 2);
        float tx = Mathf.Clamp01(fx - ix), tz = Mathf.Clamp01(fz - iz);
        float a = Mathf.Lerp(edge[iz * en + ix], edge[iz * en + ix + 1], tx), b = Mathf.Lerp(edge[(iz + 1) * en + ix], edge[(iz + 1) * en + ix + 1], tx);
        return Mathf.Lerp(a, b, tz);
    }

    // ---------------------------------------------------------------- instanced grass + foliage
    static Mesh[] tufts;
    static readonly int[] GrassWant = { 60000, 34000, 14000, 16000, 110000, 0 };
    static readonly float[] GrassH = { 0.6f, 0.45f, 0.4f, 0.4f, 0.8f, 0.4f };

    // returns true when the instanced path took over (Gfx.Grass then skips its chunk meshes)
    public static bool Grass(int theme)
    {
        Gfx6Draw.Clear();
        if (!On || grassS == null || triS == null || !SystemInfo.supportsInstancing) return false;
        float t0 = Time.realtimeSinceStartup;
        theme = Mathf.Clamp(theme, 0, 5);
        BuildEdgeGrid();
        if (grassMat == null) { grassMat = new Material(grassS) { name = "U6D grass", enableInstancing = true }; }
        Color a, b;
        switch (theme)
        {
            case 0: a = new Color(0.55f, 0.58f, 0.3f); b = new Color(0.74f, 0.68f, 0.4f); break;
            case 1: a = new Color(0.52f, 0.46f, 0.26f); b = new Color(0.66f, 0.56f, 0.32f); break;
            case 2: a = new Color(0.3f, 0.27f, 0.2f); b = new Color(0.44f, 0.37f, 0.24f); break;
            case 3: a = new Color(0.42f, 0.4f, 0.3f); b = new Color(0.56f, 0.52f, 0.37f); break;
            default: a = new Color(0.18f, 0.42f, 0.12f); b = new Color(0.36f, 0.58f, 0.18f); break;
        }
        grassMat.SetColor("_ColA", a); grassMat.SetColor("_ColB", b);
        grassMat.SetFloat("_Wind", theme == 3 ? 0.55f : theme == 1 ? 0.8f : 1f);
        grassMat.SetFloat("_Trans", theme == 4 || theme == 0 ? 0.75f : 0.5f);
        if (tufts == null) tufts = new[] { Tuft(11, 9, 1f), Tuft(23, 12, 1.25f) };
        int want = Mathf.RoundToInt(GrassWant[theme] * grassK * (Look.Mobile ? 0.35f : 1f));
        int placed = 0;
        if (want > 0)
        {
            var rnd = new System.Random(600 + theme);
            var cells = new Dictionary<int, List<Matrix4x4>>[2] { new Dictionary<int, List<Matrix4x4>>(), new Dictionary<int, List<Matrix4x4>>() };
            float bandW = Track.Wall + 60f, hs = GrassH[theme];
            for (int tries = 0; tries < want * 3 && placed < want; tries++)
            {
                float s = (float)rnd.NextDouble() * Track.Length;
                float side = rnd.NextDouble() < 0.5 ? -1f : 1f;
                float u = (float)rnd.NextDouble();
                float lat = side * (Track.HalfW + 0.9f + u * u * bandW);
                Vector3 p = Track.PointAt(s, lat);
                p.x += ((float)rnd.NextDouble() - 0.5f) * 3f; p.z += ((float)rnd.NextDouble() - 0.5f) * 3f;
                if (Edge(p.x, p.z) < 0.9f) continue;
                float y = Track.HeightAt(p.x, p.z);
                if (y < Track.SurfaceFloor + 0.35f) continue;
                float gx = Track.HeightAt(p.x + 1f, p.z) - Track.HeightAt(p.x - 1f, p.z), gz = Track.HeightAt(p.x, p.z + 1f) - Track.HeightAt(p.x, p.z - 1f);
                if (gx * gx + gz * gz > 1.1f) continue;
                if (theme == 0 && y < 0.7f) continue;
                if (theme == 3 && Mathf.PerlinNoise(p.x * 0.03f, p.z * 0.03f) < 0.52f) continue;
                if (theme == 1 && Mathf.PerlinNoise(p.x * 0.05f + 3f, p.z * 0.05f) < 0.38f) continue;
                if (theme == 2 && Mathf.PerlinNoise(p.x * 0.04f + 9f, p.z * 0.04f) < 0.4f) continue;
                int v = rnd.NextDouble() < 0.6 ? 0 : 1;
                float sc = hs * (0.7f + 0.6f * (float)rnd.NextDouble());
                var mtx = Matrix4x4.TRS(new Vector3(p.x, y - 0.03f, p.z), Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f), new Vector3(sc * (0.9f + 0.3f * (float)rnd.NextDouble()), sc, sc));
                Add(cells[v], p, 32f, mtx);
                placed++;
            }
            for (int v = 0; v < 2; v++) Gfx6Draw.AddCells(cells[v], 32f, tufts[v], grassMat, 0, false, 1.4f);
        }
        int fol = Foliage(theme);
        ApplyLevel(level, Look.Mobile);
        Debug.Log("U6D grass: " + placed + " instanced tufts, foliage " + fol + " instances, " + Gfx6Draw.Batches + " batches, " +
            ((Time.realtimeSinceStartup - t0) * 1000f).ToString("F0") + " ms (theme " + theme + (Look.Mobile ? ", mobile" : "") + ")");
        return true;
    }

    static void Add(Dictionary<int, List<Matrix4x4>> cells, Vector3 p, float cell, Matrix4x4 m)
    {
        int key = Mathf.FloorToInt(p.x / cell) * 8192 + Mathf.FloorToInt(p.z / cell);
        List<Matrix4x4> l;
        if (!cells.TryGetValue(key, out l)) { l = new List<Matrix4x4>(); cells[key] = l; }
        l.Add(m);
    }

    // one grass tuft: n bent blades round the origin (uv0.x = height 0..1, uv0.y = blade phase, colour = per-blade shade)
    static Mesh Tuft(int seed, int blades, float tall)
    {
        var rr = new System.Random(seed);
        var v = new List<Vector3>(); var nn = new List<Vector3>(); var col = new List<Color>(); var uv = new List<Vector2>(); var idx = new List<int>();
        for (int k = 0; k < blades; k++)
        {
            float ang = (float)rr.NextDouble() * Mathf.PI * 2f, off = (float)rr.NextDouble() * 0.22f;
            Vector3 r0 = new Vector3(Mathf.Cos(ang) * off, 0f, Mathf.Sin(ang) * off);
            float h = tall * (0.55f + 0.75f * (float)rr.NextDouble()), wd = 0.03f + 0.028f * (float)rr.NextDouble();
            float face = (float)rr.NextDouble() * Mathf.PI * 2f;
            Vector3 side = new Vector3(Mathf.Cos(face), 0f, Mathf.Sin(face)) * wd;
            Vector3 lean = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * h * (0.2f + 0.4f * (float)rr.NextDouble());
            Vector3 fn = Vector3.Cross(side, Vector3.up).normalized;
            int bi = v.Count;
            float sh = 0.7f + 0.3f * (float)rr.NextDouble(), ph = (float)rr.NextDouble();
            v.Add(r0 - side); v.Add(r0 + side);
            v.Add(r0 - side * 0.7f + Vector3.up * h * 0.55f + lean * 0.35f); v.Add(r0 + side * 0.7f + Vector3.up * h * 0.55f + lean * 0.35f);
            v.Add(r0 + Vector3.up * h + lean);
            float[] hh = { 0f, 0f, 0.55f, 0.55f, 1f };
            for (int j = 0; j < 5; j++) { col.Add(new Color(sh, sh, sh, 1f)); uv.Add(new Vector2(hh[j], ph)); nn.Add((fn * 0.5f + Vector3.up).normalized); }
            idx.Add(bi); idx.Add(bi + 2); idx.Add(bi + 1);
            idx.Add(bi + 1); idx.Add(bi + 2); idx.Add(bi + 3);
            idx.Add(bi + 2); idx.Add(bi + 4); idx.Add(bi + 3);
        }
        var m = new Mesh { name = "U6D tuft" };
        m.SetVertices(v); m.SetNormals(nn); m.SetColors(col); m.SetUVs(0, uv); m.SetTriangles(idx, 0);
        m.RecalculateBounds();
        var bb = m.bounds; bb.Expand(0.6f); m.bounds = bb;
        return m;
    }

    // ---------------------------------------------------------------- foliage (instanced trees / bushes)
    static Mesh pineF, pineF2, pineT, broadF, broadF2, broadT, bushF;
    static readonly int[,] FolWant = {   // pines, broadleaf, bushes (desktop)
        { 0, 0, 3500 }, { 0, 0, 1600 }, { 0, 0, 900 }, { 6500, 0, 1500 }, { 0, 7000, 9000 }, { 0, 0, 0 } };

    static Material FolMat(string name, Color c, Gfx.K k, float sway, bool snow)
    {
        var m = new Material(triS) { name = name, enableInstancing = true };
        m.color = c;
        Texture2D d = Gfx.T(k == Gfx.K.Leaf ? "leaf" : "bark"), n = Gfx.T(k == Gfx.K.Leaf ? "leaf_n" : "bark_n");
        if (d != null) m.mainTexture = d;
        if (n != null) m.SetTexture("_BumpMap", n);
        m.SetFloat("_BumpScale", k == Gfx.K.Leaf ? 0f : 1.2f);
        m.SetFloat("_Scale", k == Gfx.K.Leaf ? 0.55f : 0.7f);
        m.SetFloat("_Glossiness", k == Gfx.K.Leaf ? 0.18f : 0.06f);
        m.SetFloat("_AO", 1f);
        m.SetFloat("_Snow", snow ? 0.62f : 0f);
        m.SetFloat("_Sway", sway);
        m.SetFloat("_OwnAmb", 1f);
        m.SetVector("_Fade", new Vector4(500f, 650f, 0f, 0f));
        folMats.Add(m);
        return m;
    }

    static int Foliage(int theme)
    {
        foreach (var m in folMats) if (m != null) Object.Destroy(m);
        folMats.Clear();
        int np = FolWant[theme, 0], nb = FolWant[theme, 1], nbu = FolWant[theme, 2];
        float k = folK * (Look.Mobile ? 0.4f : 1f);
        np = Mathf.RoundToInt(np * k); nb = Mathf.RoundToInt(nb * k); nbu = Mathf.RoundToInt(nbu * k);
        if (np + nb + nbu == 0) return 0;
        if (pineF == null)
        {
            pineF = Gfx.PineMesh(2); pineF2 = Gfx.PineMesh(4); pineT = Gfx.TrunkMesh(0.035f, 0.012f, 0.62f, 0.01f, 1);
            broadF = Gfx.BroadMesh(4, 1); broadF2 = Gfx.BroadMesh(6, 1); broadT = Gfx.TrunkMesh(0.05f, 0.028f, 0.7f, 0.05f, 10);
            bushF = Gfx.BroadMesh(5, 1);
        }
        bool snowy = theme == 3;
        Color leaf, bark = new Color(0.36f, 0.27f, 0.2f), bushC;
        switch (theme)
        {
            case 0: leaf = new Color(0.22f, 0.4f, 0.16f); bushC = new Color(0.34f, 0.44f, 0.2f); break;
            case 1: leaf = new Color(0.4f, 0.42f, 0.3f); bushC = new Color(0.46f, 0.46f, 0.32f); break;
            case 2: leaf = new Color(0.2f, 0.18f, 0.15f); bushC = new Color(0.24f, 0.2f, 0.16f); break;
            case 3: leaf = new Color(0.16f, 0.3f, 0.17f); bushC = new Color(0.22f, 0.32f, 0.2f); break;
            default: leaf = new Color(0.17f, 0.4f, 0.13f); bushC = new Color(0.24f, 0.46f, 0.15f); break;
        }
        Material mLeaf = FolMat("U6D tree leaf", leaf, Gfx.K.Leaf, theme == 3 ? 0.6f : 1f, snowy);
        Material mBark = FolMat("U6D tree bark", bark, Gfx.K.Bark, 0f, snowy);
        Material mBush = FolMat("U6D bush leaf", bushC, Gfx.K.Leaf, 1.2f, snowy);
        Material mLeaf2 = FolMat("U6D tree leaf 2", Color.Lerp(leaf, new Color(0.3f, 0.45f, 0.12f), 0.35f), Gfx.K.Leaf, theme == 3 ? 0.6f : 1f, snowy);
        var rnd = new System.Random(900 + theme);
        // kinds: 0 pineF 1 pineF2 2 pineT 3 broadF 4 broadF2 5 broadT 6 bush
        var cells = new Dictionary<int, List<Matrix4x4>>[7];
        for (int i = 0; i < 7; i++) cells[i] = new Dictionary<int, List<Matrix4x4>>();
        int made = 0;
        int total = np + nb + nbu;
        for (int tries = 0; tries < total * 4 && made < total; tries++)
        {
            int kind = made < np ? 0 : made < np + nb ? 1 : 2;           // 0 pine, 1 broadleaf, 2 bush
            bool tree = kind < 2;
            float inner = tree ? Track.Wall + 7f : Track.Wall + 2f, outer = tree ? 175f : 120f;
            float s = (float)rnd.NextDouble() * Track.Length;
            float side = rnd.NextDouble() < 0.5 ? -1f : 1f;
            float u = (float)rnd.NextDouble();
            float lat = side * (Track.HalfW + inner + (tree ? u : u * u) * outer);
            Vector3 p = Track.PointAt(s, lat);
            p.x += ((float)rnd.NextDouble() - 0.5f) * 8f; p.z += ((float)rnd.NextDouble() - 0.5f) * 8f;
            float e = Edge(p.x, p.z);
            if (e < inner) continue;
            float y = Track.HeightAt(p.x, p.z);
            if (y < Track.SurfaceFloor + 0.5f) continue;
            float gx = Track.HeightAt(p.x + 2f, p.z) - Track.HeightAt(p.x - 2f, p.z), gz = Track.HeightAt(p.x, p.z + 2f) - Track.HeightAt(p.x, p.z - 2f);
            if (gx * gx + gz * gz > (tree ? 5f : 7f)) continue;          // no trees on cliffs (> ~30 deg)
            if (theme == 0 && y < 1.2f) continue;                          // not on the beach
            // clumps: forests grow in patches (and thin out into meadows)
            float clump = Mathf.PerlinNoise(p.x * 0.012f + theme * 7.1f, p.z * 0.012f + 3.3f);
            if (clump < (tree ? 0.38f : 0.3f) && rnd.NextDouble() < 0.8) continue;
            float yaw = (float)rnd.NextDouble() * 360f;
            Vector3 gp = new Vector3(p.x, y - 0.15f, p.z);
            if (kind == 0)
            {
                float h = 6f + 8f * (float)rnd.NextDouble(), w = h * (0.85f + 0.3f * (float)rnd.NextDouble());
                var m = Matrix4x4.TRS(gp, Quaternion.Euler(0f, yaw, 0f), new Vector3(w, h, w));
                Add(cells[rnd.NextDouble() < 0.5 ? 0 : 1], p, 96f, m);
                Add(cells[2], p, 96f, Matrix4x4.TRS(gp, Quaternion.Euler(0f, yaw, 0f), new Vector3(h, h, h)));
            }
            else if (kind == 1)
            {
                float h = 7f + 9f * (float)rnd.NextDouble(), w = h * (0.9f + 0.4f * (float)rnd.NextDouble());
                Add(cells[rnd.NextDouble() < 0.5 ? 3 : 4], p, 96f, Matrix4x4.TRS(gp, Quaternion.Euler(0f, yaw, 0f), new Vector3(w, h, w)));
                Add(cells[5], p, 96f, Matrix4x4.TRS(gp, Quaternion.Euler(0f, yaw, 0f), new Vector3(h, h, h)));
            }
            else
            {
                float h = 1.6f + 2.2f * (float)rnd.NextDouble(), w = h * (1.3f + 0.6f * (float)rnd.NextDouble());
                // the canopy blob sits at 0.74 of a unit tree: drop it so the bush sits on the ground
                Vector3 bp = gp - new Vector3(0f, h * 0.5f, 0f);
                Add(cells[6], p, 96f, Matrix4x4.TRS(bp, Quaternion.Euler(0f, yaw, 0f), new Vector3(w, h, w)));
            }
            made++;
        }
        bool sh = !Look.Mobile;
        Gfx6Draw.AddCells(cells[0], 96f, pineF, mLeaf, 1, sh, 16f);
        Gfx6Draw.AddCells(cells[1], 96f, pineF2, mLeaf2, 1, sh, 16f);
        Gfx6Draw.AddCells(cells[2], 96f, pineT, mBark, 1, sh, 16f);
        Gfx6Draw.AddCells(cells[3], 96f, broadF, mLeaf, 1, sh, 18f);
        Gfx6Draw.AddCells(cells[4], 96f, broadF2, mLeaf2, 1, sh, 18f);
        Gfx6Draw.AddCells(cells[5], 96f, broadT, mBark, 1, sh, 18f);
        Gfx6Draw.AddCells(cells[6], 96f, bushF, mBush, 2, sh, 5f);
        return made;
    }
}

// u6d: issues the instanced draws every frame (RenderMeshInstanced per cell, distance-culled against the player cameras;
// frustum culling by Unity from each batch's world bounds) + the follow-the-camera realtime reflection probe.
public class Gfx6Draw : MonoBehaviour
{
    class Batch { public Mesh mesh; public Material mat; public Matrix4x4[] m; public int n; public Bounds b; public Vector2 c; public float rad; public int kind; public bool shadows; }
    static readonly List<Batch> batches = new List<Batch>();
    public static int Batches { get { return batches.Count; } }
    public static float GrassDist = 120f;
    static readonly List<Vector3> camPos = new List<Vector3>();
    static Camera fallbackCam;
    static ReflectionProbe probe;
    static bool probeWant;
    static int probeId = -1;
    static float probeT;

    public static void Clear() { batches.Clear(); }

    // kind 0 grass, 1 trees, 2 bushes
    public static void AddCells(Dictionary<int, List<Matrix4x4>> cells, float cell, Mesh mesh, Material mat, int kind, bool shadows, float height)
    {
        if (mesh == null || mat == null) return;
        foreach (var kv in cells)
        {
            var l = kv.Value;
            for (int start = 0; start < l.Count; start += 1000)
            {
                int n = Mathf.Min(1000, l.Count - start);
                var arr = new Matrix4x4[n];
                Vector3 mn = new Vector3(1e9f, 1e9f, 1e9f), mx = -mn;
                for (int i = 0; i < n; i++)
                {
                    arr[i] = l[start + i];
                    Vector3 p = arr[i].GetColumn(3);
                    mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p);
                }
                var b = new Bounds(); b.SetMinMax(mn - new Vector3(height, 2f, height), mx + new Vector3(height, height * 1.3f + 2f, height));
                batches.Add(new Batch { mesh = mesh, mat = mat, m = arr, n = n, b = b, c = new Vector2(b.center.x, b.center.z), rad = Mathf.Max(b.extents.x, b.extents.z), kind = kind, shadows = shadows });
            }
        }
    }

    public static void SetProbe(bool on) { probeWant = on; }

    void LateUpdate()
    {
        GatherCams();
        if (batches.Count > 0 && camPos.Count > 0)
        {
            foreach (var bt in batches)
            {
                float maxD = bt.kind == 0 ? GrassDist : bt.kind == 1 ? Gfx6.TreeDist : Gfx6.BushDist;
                float frac = bt.kind == 0 ? Gfx6.GrassFrac : Gfx6.FolFrac;
                if (maxD <= 0f || frac <= 0f) continue;
                float d = 1e9f;
                foreach (var cp in camPos) d = Mathf.Min(d, Vector2.Distance(new Vector2(cp.x, cp.z), bt.c));
                if (d - bt.rad > maxD) continue;
                int cnt = frac >= 0.999f ? bt.n : Mathf.FloorToInt(bt.n * frac);
                if (cnt <= 0) continue;
                var rp = new RenderParams(bt.mat);
                rp.worldBounds = bt.b;
                rp.shadowCastingMode = bt.shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                rp.receiveShadows = true;
                rp.layer = 0;
                rp.renderingLayerMask = 1u;
                rp.lightProbeUsage = LightProbeUsage.Off;
                Graphics.RenderMeshInstanced(rp, bt.mesh, 0, bt.m, cnt, 0);
            }
        }
        UpdateProbe();
    }

    void GatherCams()
    {
        camPos.Clear();
        var cams = Look.Cams;
        if (cams != null && Look.Views > 0)
            foreach (var c in cams) if (c != null && c.isActiveAndEnabled) camPos.Add(c.transform.position);
        if (camPos.Count > 0) return;
        if (fallbackCam == null || !fallbackCam.isActiveAndEnabled || Time.frameCount % 60 == 0)
        {
            fallbackCam = null;
            foreach (var c in Camera.allCameras)
                if ((c.cullingMask & 1) != 0 && c.cameraType == CameraType.Game) { if (fallbackCam == null || c.targetTexture == null) fallbackCam = c; }
        }
        if (fallbackCam != null) camPos.Add(fallbackCam.transform.position);
    }

    void UpdateProbe()
    {
        bool want = probeWant && camPos.Count > 0;
        if (!want) { if (probe != null && probe.gameObject.activeSelf) probe.gameObject.SetActive(false); return; }
        if (probe == null)
        {
            var go = new GameObject("U6D car probe");
            DontDestroyOnLoad(go);
            probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.resolution = 256;
            probe.hdr = true;
            probe.size = new Vector3(90f, 50f, 90f);
            probe.boxProjection = false;
            probe.cullingMask = ~Showroom.Mask;
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            probe.nearClipPlane = 0.6f;
            probe.farClipPlane = 900f;
            probe.importance = 5;
            probe.blendDistance = 8f;
            probe.intensity = 1f;
            Debug.Log("U6D: realtime car reflection probe 256 HDR (time-sliced)");
        }
        if (!probe.gameObject.activeSelf) probe.gameObject.SetActive(true);
        probe.transform.position = camPos[0] + Vector3.up * 0.5f;
        probeT += Time.unscaledDeltaTime;
        if ((probeId < 0 || probe.IsFinishedRendering(probeId)) && probeT > 0.25f)
        {
            probeT = 0f;
            probeId = probe.RenderProbe();
        }
    }
}
