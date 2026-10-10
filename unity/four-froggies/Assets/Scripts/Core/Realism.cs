using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;

// PHOTOREAL TEST (2026-10-09): only with ?realism=1 in the page URL (realism=lite forces the phone tier, realism=full the
// desktop tier). Without the flag nothing here runs and the game looks / plays exactly as before.
// Test area = the pond's north-east shore (Layout.PondC + PondR, angles AreaA0..AreaA1). What it does:
//  - streams CC0 scans from web/real/ (NOT in the Unity build: zero build-size cost for normal players): Poly Haven
//    Sunflowers (Pure Sky) HDRI sky, Grass004 (ambientCG), dry_ground_01 / brown_mud_rocks_01 / brown_mud_02 /
//    rock_boulder_dry / bark_brown_02 (Poly Haven) albedo + normal maps, a generated water normal map + grass card;
//  - HDRI skybox + sun aligned to the HDRI's sun + ambient SH integrated from the HDRI (AmbTable) + haze matched to its
//    horizon; ranch terrain layers -> the scans with normal maps (a wet mud band + pebbly shore around the pond);
//  - FF/RealWater on the pond (normal-map ripples, Schlick fresnel, reflection probe / HDRI reflection, sun glints,
//    depth tint + soft shoreline); boulders (FF/RealTri triplanar rock) + grass clumps in the test corner; bark scan
//    on tree trunks, natural leaf tint;
//  - RealPost instead of LBPost: HDR camera (desktop), soft bloom, depth-based SSAO (desktop), ACES filmic tonemap.
// Tiers: full (desktop) vs Lite (phones: Look.Mobile; Tesla browser by user agent): 512 px scans, 1k sky, no HDR,
// no SSAO, quarter-res bloom, fewer clumps / boulders, the existing mobile shadow settings.
public static class Realism
{
    public static bool On { get; private set; }
    public static bool Lite { get; private set; }
    public static bool Ready { get; private set; }
    public static Material TriBase, WaterBase, TerrainNM;     // editor-made (BuildScript) so their shader variants ship

    // test area: pond shore arc between these angles (degrees on the PondC / PondR ellipse)
    public const float AreaA0 = -8f, AreaA1 = 82f;
    // HDRI (work/real/sky_meta.json): sun at u 0.6003, elevation 43 deg; SunAz = world azimuth we want the sun at
    // (atan2(z, x) in degrees: -135 = south-west, side-light for the shore cameras)
    const float SunU = 0.6003f, SunEl = 42.98f, SunAz = -135f;
    const float AmbK = 0.3f;             // ambient strength (linear scale of the HDRI table)
    static readonly Color SunCol = new Color(1f, 0.92f, 0.8f);
    static readonly Color Haze = new Color(0.70f, 0.77f, 0.85f);
    public static float SkyRot { get { float phiTex = (0.5f - SunU) * 360f; return Mathf.Repeat(SunAz - phiTex, 360f); } }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern string FFRealUA();
#else
    static string FFRealUA() { return ""; }
#endif

    static SphericalHarmonicsL2 probe;
    static Material sky;
    static readonly Dictionary<string, Texture2D> tex = new Dictionary<string, Texture2D>();
    static long bytes;

    static string Param(string u, string key)
    {
        int q = u.IndexOf('?'); if (q < 0) return null;
        foreach (var kv in u.Substring(q + 1).Split('&', '#'))
        {
            int e = kv.IndexOf('=');
            string k = e >= 0 ? kv.Substring(0, e) : kv;
            if (k == key) return e >= 0 ? kv.Substring(e + 1).ToLowerInvariant() : "1";
        }
        return null;
    }

    // Bootstrap, right after Look.Init (needs Look.Mobile) and before Worlds.Init (which captures sky / fog / sun)
    public static void Detect(Light sun)
    {
        string u = Application.absoluteURL ?? "";
        string v = Param(u, "realism");
        On = v != null && v != "0" && v != "off" && v != "false";
        if (!On) return;
        string ua = "";
        try { ua = FFRealUA() ?? ""; } catch (System.Exception) { }
        bool tesla = ua.Contains("Tesla") || ua.Contains("QtCarBrowser");
        Lite = v == "lite" || (v != "full" && (Look.Mobile || tesla));
        Debug.Log("Realism: ON tier " + (Lite ? "lite" : "full") + (tesla ? " (Tesla UA)" : "") + " skyRot " + SkyRot.ToString("0.0"));
        ApplyEnvironment(sun);
    }

    static Vector3 TexDirToWorld(float u, float elDeg)
    {
        float phi = (0.5f - u) * 2f * Mathf.PI, el = elDeg * Mathf.Deg2Rad;
        Vector3 d = new Vector3(Mathf.Cos(el) * Mathf.Cos(phi), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Sin(phi));
        // Skybox/Panoramic rotates the sky geometry by _Rotation (RotateAroundYInDegrees): world = R(rot) * texDir
        float r = SkyRot * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector3(c * d.x - s * d.z, d.y, s * d.x + c * d.z);
    }

    static void ApplyEnvironment(Light sun)
    {
        if (RenderSettings.skybox != null)
        {
            sky = new Material(RenderSettings.skybox);   // same shader + keywords as the shipped Sky.mat; texture set on load
            if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", 1f);
            RenderSettings.skybox = sky;
        }
        if (sun != null)
        {
            Vector3 toSun = TexDirToWorld(SunU, SunEl);
            sun.transform.rotation = Quaternion.LookRotation(-toSun);
            sun.color = SunCol;
            sun.intensity = Lite ? 1.12f : 1.22f;
            sun.shadowStrength = 0.86f;
            sun.shadows = LightShadows.Soft;
            sun.shadowBias = 0.04f; sun.shadowNormalBias = 0.35f;
        }
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = Haze;
        RenderSettings.fogDensity = 0.0026f;
        RenderSettings.reflectionIntensity = 1f;
        // ambient: integrate the HDRI table (gamma -> linear x AmbK -> gamma) into an SH probe
        probe = new SphericalHarmonicsL2();
        int nl = 8, nu = 16;
        for (int j = 0; j < nl; j++)
        {
            float e0 = 90f - 180f * j / nl, e1 = 90f - 180f * (j + 1) / nl, ec = (e0 + e1) * 0.5f;
            float dOmega = (Mathf.PI / nl) * (2f * Mathf.PI / nu) * Mathf.Cos(ec * Mathf.Deg2Rad);
            for (int i = 0; i < nu; i++)
            {
                int k = (j * nu + i) * 3;
                Color c = new Color(G(AmbTable[k]), G(AmbTable[k + 1]), G(AmbTable[k + 2]));
                probe.AddDirectionalLight(TexDirToWorld((i + 0.5f) / nu, ec), c, dOmega / Mathf.PI);
            }
        }
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientProbe = probe;
        // keep the Trilight colours sensible too (other worlds restore their own each frame)
        RenderSettings.ambientSkyColor = new Color(0.5f, 0.56f, 0.64f);
        RenderSettings.ambientEquatorColor = new Color(0.52f, 0.54f, 0.5f);
        RenderSettings.ambientGroundColor = new Color(0.3f, 0.3f, 0.22f);
    }
    static float G(float g) { return Mathf.Pow(Mathf.Pow(g, 2.2f) * AmbK, 1f / 2.2f); }

    // Worlds.PreCull hook: SH ambient on the ranch, the old Trilight elsewhere (house, underwater, space, Mars ...)
    public static void PreCull(WorldId w)
    {
        if (w == WorldId.Ranch) { RenderSettings.ambientMode = AmbientMode.Skybox; RenderSettings.ambientProbe = probe; }
        else RenderSettings.ambientMode = AmbientMode.Trilight;
    }

    // Look.ApplyViews hook: RealPost replaces LBPost; HDR + SSAO on the full tier; shadows by tier
    public static void ApplyQuality(int views, IEnumerable<Camera> cams)
    {
        bool post = Look.PostAllowed(views);
        if (!Lite && views <= 1)
        {
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.shadowDistance = 70f;
            QualitySettings.shadowCascades = 2;
            QualitySettings.shadowCascade2Split = 0.25f;
        }
        else if (Lite && !Look.Mobile && views <= 1)
        {
            // Tesla browser: soft shadows at medium resolution
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.Medium;
            QualitySettings.shadowDistance = 40f;
        }
        if (cams == null) return;
        foreach (var c in cams)
        {
            if (c == null) continue;
            var lp = c.GetComponent<LBPost>();
            if (lp != null) lp.enabled = false;
            var rp = c.GetComponent<RealPost>();
            if (rp == null && post) rp = c.gameObject.AddComponent<RealPost>();
            if (rp != null) rp.enabled = post && c.enabled;
            c.allowHDR = post && !Lite;
        }
    }

    // ---------------- streaming the scans ----------------
    static string BaseUrl()
    {
        string u = Application.absoluteURL ?? "";
        int q = u.IndexOfAny(new[] { '?', '#' }); if (q >= 0) u = u.Substring(0, q);
        int s = u.LastIndexOf('/'); if (s >= 0) u = u.Substring(0, s + 1);
        return u + "real/";
    }

    static IEnumerator Load(string file, string key, bool mips, bool compress, bool clampV = false)
    {
        string url = BaseUrl() + file + "?v=r3";
        using (var rq = UnityWebRequest.Get(url))
        {
            yield return rq.SendWebRequest();
            if (rq.result != UnityWebRequest.Result.Success) { Debug.LogWarning("Realism: " + file + " " + rq.error); yield break; }
            byte[] data = rq.downloadHandler.data;
            bytes += data.Length;
            var t = new Texture2D(2, 2, file.EndsWith(".png") ? TextureFormat.RGBA32 : TextureFormat.RGB24, mips, true);
            if (!ImageConversion.LoadImage(t, data, false)) { Debug.LogWarning("Realism: decode failed " + file); yield break; }
            t.name = key;
            t.wrapModeU = TextureWrapMode.Repeat;
            t.wrapModeV = clampV ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            t.filterMode = mips ? FilterMode.Trilinear : FilterMode.Bilinear;
            t.anisoLevel = mips ? (Lite ? 2 : 8) : 1;
            if (mips) t.Apply(true, false);
            bool packed = false;
            if (compress && SystemInfo.SupportsTextureFormat(TextureFormat.DXT1))
            {
                try { t.Compress(false); packed = true; } catch (System.Exception e) { Debug.LogWarning("Realism: compress " + file + " " + e.Message); }
            }
            t.Apply(false, true);   // upload, drop the CPU copy
            tex[key] = t;
            if (packed && key == "grass_a") Debug.Log("Realism: runtime compression -> " + t.format);
        }
    }

    static Texture2D T(string k) { Texture2D t; return tex.TryGetValue(k, out t) ? t : null; }

    // Bootstrap, after the ranch is built
    public static void Begin(MonoBehaviour host, Terrain terrain)
    {
        if (!On || host == null) return;
        host.StartCoroutine(Run(terrain));
    }

    static IEnumerator Run(Terrain terrain)
    {
        float t0 = Time.realtimeSinceStartup;
        string sfx = Lite ? "_512" : "";
        yield return Load("sky_2k.jpg", "sky", false, false, true);   // 1k looked blocky in portrait
        if (sky != null && T("sky") != null)
        {
            sky.SetTexture("_MainTex", T("sky"));
            if (sky.HasProperty("_Rotation")) sky.SetFloat("_Rotation", SkyRot);
        }
        string[] sets = { "grass", "dirt", "shore", "mud", "rock", "bark" };
        foreach (var s in sets)
        {
            yield return Load(s + "_a" + sfx + ".jpg", s + "_a", true, true);
            yield return Load(s + "_n" + sfx + ".jpg", s + "_n", true, !Lite);
        }
        yield return Load("water_n.png", "water_n", true, false);
        yield return Load("clump.png", "clump", true, false, true);   // clamp V: Repeat bled the blade bases onto the card tops (dark dashes)
        try { ApplyTerrain(terrain); } catch (System.Exception e) { Debug.LogWarning("Realism: terrain " + e); }
        try { ApplyMaterials(); } catch (System.Exception e) { Debug.LogWarning("Realism: materials " + e); }
        try { BuildArea(); } catch (System.Exception e) { Debug.LogWarning("Realism: area " + e); }
        yield return null;
        Look.RerenderProbe(Lite ? 64 : 256);
        Ready = true;
        Debug.Log("Realism: ready in " + (Time.realtimeSinceStartup - t0).ToString("0.0") + " s, " + tex.Count + " textures, " + (bytes / 1024) + " KB streamed");
    }

    // ---------------- terrain ----------------
    static TerrainLayer Layer(string k, float tile, float smooth, float nrm)
    {
        var a = T(k + "_a"); if (a == null) return null;
        var l = new TerrainLayer { diffuseTexture = a, tileSize = new Vector2(tile, tile), smoothness = smooth, metallic = 0f };
        var n = T(k + "_n");
        if (n != null) { l.normalMapTexture = n; l.normalScale = nrm; }
        return l;
    }

    static void ApplyTerrain(Terrain terrain)
    {
        if (terrain == null) return;
        var gl = Layer("grass", 2.6f, 0.05f, 0.9f); var dl = Layer("dirt", 3.5f, 0.08f, 1f);
        var sl = Layer("shore", 2.4f, 0.32f, 1.1f); var ml = Layer("mud", 3f, 0.42f, 1f);
        if (gl == null || dl == null || sl == null || ml == null) { Debug.LogWarning("Realism: terrain layers missing"); return; }
        var td = terrain.terrainData;
        td.terrainLayers = new[] { gl, dl, sl, ml };
        int ar = td.alphamapResolution;
        float size = Layout.Half * 2f;
        var am = new float[ar, ar, 4];
        for (int y = 0; y < ar; y++)
            for (int x = 0; x < ar; x++)
            {
                float wx = -Layout.Half + (x + 0.5f) * size / ar, wz = -Layout.Half + (y + 0.5f) * size / ar;
                float road = Mathf.Clamp01((Layout.TrackW * 0.5f + 1f - Layout.RoadDist(wx, wz)) / 2f);
                if (wx > Layout.GarageC.x - 30f && wx < Layout.GarageC.x + 28f && wz > 30f && wz < 40f) road = Mathf.Max(road, 0.8f);
                float patch = Mathf.Clamp01((Mathf.PerlinNoise(wx * 0.03f + 3f, wz * 0.03f + 7f) - 0.68f) * 3f) * 0.55f;
                // small worn spots break up the grass tiling
                float worn = Mathf.Clamp01((Mathf.PerlinNoise(wx * 0.11f + 17f, wz * 0.11f + 4f) - 0.62f) * 2.2f) * 0.45f;
                float q = Layout.PondQ(wx, wz);
                float jag = (Mathf.PerlinNoise(wx * 0.18f, wz * 0.18f) - 0.5f) * 0.06f;
                float shore = Mathf.Clamp01((1.09f + jag - q) / 0.05f);
                float mud = Mathf.Clamp01((1.2f + jag * 1.5f - q) / 0.07f) * (1f - shore);
                float d = Mathf.Max(road, Mathf.Max(patch, worn)) * (1f - shore - mud);
                float g = Mathf.Clamp01(1f - d - shore - mud);
                am[y, x, 0] = g; am[y, x, 1] = d; am[y, x, 2] = shore; am[y, x, 3] = mud;
            }
        td.SetAlphamaps(0, 0, am);
        if (TerrainNM != null) { TerrainNM.EnableKeyword("_NORMALMAP"); terrain.materialTemplate = TerrainNM; }
        terrain.basemapDistance = 400f;
        Debug.Log("Realism: terrain layers (grass / dirt / shore / mud with normal maps" + (TerrainNM != null ? ", NM material" : "") + ")");
    }

    // ---------------- materials ----------------
    static Material Tri(string k, Color c, float metres, float gloss, float bump)
    {
        if (TriBase == null || T(k + "_a") == null) return null;
        var m = new Material(TriBase);
        m.SetTexture("_MainTex", T(k + "_a"));
        if (T(k + "_n") != null) m.SetTexture("_BumpMap", T(k + "_n"));
        m.SetColor("_Color", c);
        m.SetFloat("_Scale", metres);
        m.SetFloat("_Gloss", gloss);
        m.SetFloat("_BumpScale", bump);
        return m;
    }

    static Material rockMat, barkMat;
    static void ApplyMaterials()
    {
        rockMat = Tri("rock", new Color(0.42f, 0.41f, 0.39f), 2.2f, 0.16f, 1.5f);
        barkMat = Tri("bark", new Color(1f, 0.97f, 0.92f), 1.4f, 0.1f, 1.3f);
        // pond water
        var go = GameObject.Find("PondWater");
        if (go != null && WaterBase != null && T("water_n") != null)
        {
            var src = Ranch.PondMaterial();
            var m = new Material(WaterBase);
            if (src != null && src.HasProperty("_SeaMap")) { m.SetTexture("_SeaMap", src.GetTexture("_SeaMap")); m.SetVector("_SeaRect", src.GetVector("_SeaRect")); }
            m.SetTexture("_NormalMap", T("water_n"));
            m.SetFloat("_Detail", Lite ? 0f : 1f);
            go.GetComponent<Renderer>().sharedMaterial = m;
        }
        // pond reeds + lily pads (Ranch.Cyl -> shared Mats.Lit instances): natural, darker colours
        Mats.Lit(new Color(0.35f, 0.5f, 0.2f)).color = new Color(0.24f, 0.27f, 0.13f);
        var pad = Mats.Lit(new Color(0.25f, 0.55f, 0.2f)); pad.color = new Color(0.13f, 0.22f, 0.08f); pad.SetFloat("_Glossiness", 0.45f);
        // bark scan on trunks, rock scan on the Quaternius rocks, natural (less saturated) leaf tint
        var swap = new Dictionary<Material, Material>();
        int n = 0;
        foreach (var r in Object.FindObjectsOfType<MeshRenderer>())
        {
            var ms = r.sharedMaterials; bool ch = false;
            for (int i = 0; i < ms.Length; i++)
            {
                var m = ms[i]; if (m == null) continue;
                Material rep;
                if (!swap.TryGetValue(m, out rep))
                {
                    rep = null;
                    string tn = m.HasProperty("_MainTex") && m.mainTexture != null ? m.mainTexture.name : "";
                    if (tn == "q_bark" && barkMat != null) rep = barkMat;
                    else if (tn == "q_rocks" && rockMat != null) rep = rockMat;
                    else if ((tn == "q_leaves" || tn == "q_pine" || tn == "q_bushleaf") && m.shader != null && m.shader.name == "FF/Foliage")
                    { rep = new Material(m); rep.color = m.color * new Color(0.6f, 0.7f, 0.48f); }
                    swap[m] = rep;
                }
                if (rep != null) { ms[i] = rep; ch = true; }
            }
            if (ch) { r.sharedMaterials = ms; n++; }
        }
        Debug.Log("Realism: re-skinned " + n + " renderers (bark / rock / leaves), water " + (go != null));
    }

    static bool Near(Color a, Color b) { return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.02f; }

    // ---------------- the test corner: boulders + grass clumps ----------------
    static Vector3 Shore(float aDeg, float q)
    {
        float a = aDeg * Mathf.Deg2Rad;
        float x = Layout.PondC.x + Layout.PondR.x * q * Mathf.Cos(a), z = Layout.PondC.y + Layout.PondR.y * q * Mathf.Sin(a);
        return new Vector3(x, Ranch.GY(x, z), z);
    }

    static void BuildArea()
    {
        var root = new GameObject("RealismArea").transform;
        var rnd = new System.Random(77);
        System.Func<float> R = () => (float)rnd.NextDouble();
        // boulders along the waterline (some half in the water) + scattered small stones on the bank
        if (rockMat != null)
        {
            var parts = new List<CombineInstance>();
            int big = Lite ? 9 : 16, small = Lite ? 14 : 34;
            for (int i = 0; i < big + small; i++)
            {
                bool b = i < big;
                float a = Mathf.Lerp(AreaA0 + 4f, AreaA1 - 6f, b ? (i + R() * 0.8f) / big : R());
                float q = b ? 0.99f + R() * 0.12f : 1.04f + R() * 0.22f;
                Vector3 p = Shore(a, q);
                float s = b ? 0.9f + R() * 1.6f : 0.18f + R() * 0.35f;
                Mesh m = Boulder(rnd.Next());
                var mtx = Matrix4x4.TRS(p + Vector3.down * s * 0.28f, Quaternion.Euler(R() * 14f - 7f, R() * 360f, R() * 14f - 7f), new Vector3(s * (1f + R() * 0.5f), s * (0.6f + R() * 0.35f), s * (1f + R() * 0.4f)));
                parts.Add(new CombineInstance { mesh = m, transform = mtx });
                if (b)
                {
                    var col = new GameObject("RealRockCol").AddComponent<SphereCollider>();
                    col.transform.SetParent(root, false);
                    col.transform.position = p + Vector3.up * s * 0.1f;
                    col.radius = s * 0.75f;
                }
            }
            var mesh = new Mesh { name = "RealBoulders", indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(parts.ToArray(), true, true);
            mesh.RecalculateBounds();
            var go = new GameObject("RealBoulders");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = rockMat;
            mr.shadowCastingMode = ShadowCastingMode.On;
            foreach (var c in parts) Object.Destroy(c.mesh);
        }
        // grass clumps: tall along the bank, shorter further up, none on the track / in the water
        if (T("clump") != null && Mats.FoliageBase != null)
        {
            var gm = new Material(Mats.FoliageBase);
            gm.mainTexture = T("clump");
            gm.color = new Color(0.8f, 0.86f, 0.72f);
            if (gm.HasProperty("_Cutoff")) gm.SetFloat("_Cutoff", 0.42f);
            if (gm.HasProperty("_Wind")) gm.SetFloat("_Wind", 0.07f);
            int want = Lite ? 1500 : 5200;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var nrm = new List<Vector3>(); var tri = new List<int>();
            int made = 0, chunk = 0;
            for (int tries = 0; tries < want * 4 && made < want; tries++)
            {
                float a = Mathf.Lerp(AreaA0, AreaA1, R());
                float q = 1.07f + Mathf.Pow(R(), 1.6f) * 0.75f;
                Vector3 p = Shore(a, q);
                if (p.x > 168f || p.z > -6f) continue;
                if (Layout.RoadDist(p.x, p.z) < Layout.TrackW * 0.5f + 2f) continue;
                if (Lite && q > 1.5f) continue;
                Vector3 cp, cl; DemoCam("realpond", out cp, out cl); if ((p - cp).sqrMagnitude < 9f) continue;
                float bank = Mathf.Clamp01(1f - (q - 1.08f) / 0.18f);
                float h = Mathf.Lerp(0.38f, 0.75f, R()) * (1f + bank * 0.8f);
                float w = h * (0.9f + R() * 0.4f);
                float yaw = R() * 180f;
                for (int k = 0; k < 3; k++)
                {
                    float ang = (yaw + k * 60f) * Mathf.Deg2Rad;
                    Vector3 dx = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * w * 0.5f;
                    int b0 = v.Count;
                    Vector3 lean = new Vector3(R() - 0.5f, 0f, R() - 0.5f) * h * 0.25f;
                    v.Add(p - dx + Vector3.down * 0.04f); v.Add(p + dx + Vector3.down * 0.04f); v.Add(p + dx + Vector3.up * h + lean); v.Add(p - dx + Vector3.up * h + lean);
                    uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(1f, 0f)); uv.Add(new Vector2(1f, 1f)); uv.Add(new Vector2(0f, 1f));
                    for (int z = 0; z < 4; z++) nrm.Add(Vector3.up);
                    tri.Add(b0); tri.Add(b0 + 2); tri.Add(b0 + 1); tri.Add(b0); tri.Add(b0 + 3); tri.Add(b0 + 2);
                }
                made++;
                if (v.Count > 60000) { Flush(root, gm, v, uv, nrm, tri, chunk++); }
            }
            if (v.Count > 0) Flush(root, gm, v, uv, nrm, tri, chunk++);
            Debug.Log("Realism: area built (" + made + " grass clumps, " + chunk + " meshes)");
        }
    }

    static void Flush(Transform root, Material m, List<Vector3> v, List<Vector2> uv, List<Vector3> n, List<int> t, int i)
    {
        var mesh = new Mesh { name = "RealGrass" + i };
        mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetNormals(n); mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();
        var go = new GameObject("RealGrass" + i);
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = m;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = true;
        v.Clear(); uv.Clear(); n.Clear(); t.Clear();
    }

    // noise-displaced icosphere, flattened base
    static Mesh Boulder(int seed)
    {
        var verts = new List<Vector3>(); var tris = new List<int>();
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        Vector3[] b = { new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0), new Vector3(0, -1, t), new Vector3(0, 1, t),
                        new Vector3(0, -1, -t), new Vector3(0, 1, -t), new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1) };
        foreach (var p in b) verts.Add(p.normalized);
        int[] f = { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8, 3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
        tris.AddRange(f);
        for (int s = 0; s < 3; s++)
        {
            var mid = new Dictionary<long, int>(); var nt = new List<int>();
            System.Func<int, int, int> M = (i0, i1) =>
            {
                long key = i0 < i1 ? ((long)i0 << 32) | (uint)i1 : ((long)i1 << 32) | (uint)i0;
                int r; if (mid.TryGetValue(key, out r)) return r;
                verts.Add(((verts[i0] + verts[i1]) * 0.5f).normalized); mid[key] = verts.Count - 1; return verts.Count - 1;
            };
            for (int i = 0; i < tris.Count; i += 3)
            {
                int a = tris[i], c1 = tris[i + 1], d = tris[i + 2];
                int ab = M(a, c1), bc = M(c1, d), ca = M(d, a);
                nt.AddRange(new[] { a, ab, ca, c1, bc, ab, d, ca, bc, ab, bc, ca });
            }
            tris = nt;
        }
        float ox = (seed % 997) * 0.37f, oz = (seed % 613) * 0.53f;
        for (int i = 0; i < verts.Count; i++)
        {
            Vector3 p = verts[i];
            float n1 = Mathf.PerlinNoise(p.x * 1.3f + ox, p.z * 1.3f + oz) + Mathf.PerlinNoise(p.y * 1.3f + oz, p.x * 1.3f + ox) - 1f;
            float n2 = Mathf.PerlinNoise(p.x * 4.1f + oz, p.y * 4.1f + ox) + Mathf.PerlinNoise(p.z * 4.1f + ox, p.y * 4.1f + oz) - 1f;
            float r = 1f + n1 * 0.34f + n2 * 0.09f;
            p *= r;
            if (p.y < -0.25f) p.y = -0.25f + (p.y + 0.25f) * 0.25f;
            verts[i] = p;
        }
        var m = new Mesh();
        m.SetVertices(verts); m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        return m;
    }

    // demo / screenshot cameras for the test corner (&ffshot=realpond / realpond2), same with or without realism
    public static void DemoCam(string sc, out Vector3 pos, out Vector3 look)
    {
        if (sc == "realpond2")
        {
            // low over the water at the east shore, looking north-west along the bank (rocks + water up close)
            pos = new Vector3(146f, Layout.WaterY + 1.25f, -66f); look = new Vector3(131f, Layout.WaterY + 0.2f, -40f);
            return;
        }
        // standing on the east bank (eye height), looking north-west across the corner of the pond
        float gy = Ranch.GY(158f, -69f);
        pos = new Vector3(158f, gy + 1.75f, -69f); look = new Vector3(133f, gy - 0.6f, -24f);
    }

    // generated from work/real/sky_meta.json (16 x 8 lat-long cells, gamma RGB, sun removed; lower half = ground bounce)
    static readonly float[] AmbTable = {
        0.453f, 0.536f, 0.612f, 0.444f, 0.529f, 0.605f, 0.458f, 0.539f, 0.612f, 0.466f, 0.547f, 0.619f, 0.478f, 0.559f, 0.631f, 0.594f, 0.653f, 0.698f, 0.555f, 0.631f, 0.691f, 0.588f, 0.666f, 0.725f,
        0.629f, 0.709f, 0.762f, 0.651f, 0.732f, 0.784f, 0.628f, 0.710f, 0.764f, 0.589f, 0.672f, 0.731f, 0.559f, 0.639f, 0.701f, 0.517f, 0.598f, 0.666f, 0.480f, 0.563f, 0.636f, 0.458f, 0.542f, 0.618f,
        0.578f, 0.628f, 0.675f, 0.534f, 0.596f, 0.655f, 0.498f, 0.566f, 0.636f, 0.487f, 0.559f, 0.634f, 0.619f, 0.666f, 0.703f, 0.958f, 0.960f, 0.924f, 1.035f, 1.038f, 1.004f, 1.015f, 1.032f, 1.010f,
        1.108f, 1.155f, 1.151f, 1.010f, 1.071f, 1.081f, 1.023f, 1.094f, 1.110f, 0.881f, 0.946f, 0.971f, 0.787f, 0.840f, 0.867f, 0.625f, 0.696f, 0.756f, 0.489f, 0.573f, 0.656f, 0.441f, 0.527f, 0.616f,
        0.555f, 0.655f, 0.746f, 0.673f, 0.736f, 0.781f, 0.599f, 0.677f, 0.746f, 0.644f, 0.712f, 0.767f, 0.700f, 0.757f, 0.798f, 0.719f, 0.783f, 0.832f, 0.824f, 0.881f, 0.909f, 1.113f, 1.133f, 1.101f,
        1.280f, 1.324f, 1.298f, 1.081f, 1.140f, 1.133f, 1.220f, 1.270f, 1.256f, 1.218f, 1.229f, 1.179f, 0.935f, 0.973f, 0.966f, 0.766f, 0.831f, 0.876f, 0.583f, 0.677f, 0.759f, 0.573f, 0.663f, 0.742f,
        0.867f, 0.920f, 0.923f, 0.879f, 0.931f, 0.931f, 0.836f, 0.897f, 0.904f, 0.814f, 0.867f, 0.869f, 0.807f, 0.866f, 0.876f, 0.881f, 0.923f, 0.912f, 0.930f, 0.968f, 0.951f, 1.051f, 1.085f, 1.058f,
        1.240f, 1.271f, 1.218f, 1.287f, 1.300f, 1.215f, 1.244f, 1.264f, 1.201f, 1.116f, 1.131f, 1.081f, 0.958f, 1.012f, 1.006f, 0.875f, 0.944f, 0.951f, 0.826f, 0.900f, 0.914f, 0.816f, 0.889f, 0.906f,
        0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f,
        0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f,
        0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f,
        0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f,
        0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f,
        0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f,
        0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f,
        0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f, 0.475f, 0.512f, 0.396f
    };
}
