using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Sets the player to use both the old Input Manager and the new Input System as early as possible.
[InitializeOnLoad]
public static class InputHandlerSetter
{
    static InputHandlerSetter()
    {
        try { BuildScript.SetInputHandler(); }
        catch (Exception e) { Debug.LogWarning("InputHandlerSetter: " + e.Message); }
    }
}

public static class BuildScript
{
    const string Gen = "Assets/Generated";
    const string ScenePath = "Assets/Scenes/Main.unity";

    // Entry point: Unity -batchmode -executeMethod BuildScript.BuildWebGL
    public static void BuildWebGL()
    {
        int code = 1;
        try
        {
            code = Run();
        }
        catch (Exception e)
        {
            Debug.LogError("BUILD_SCRIPT_EXCEPTION: " + e);
            code = 2;
        }
        Debug.Log("BUILD_SCRIPT_EXIT: " + code);
        EditorApplication.Exit(code);
    }

    static int Run()
    {
        Debug.Log("BuildScript: start, Unity " + Application.unityVersion);
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

        SetInputHandler();
        ConfigurePlayer();
        EnsureFolder("Assets", "Generated");
        EnsureFolder("Assets", "Scenes");

        string scene = CreateScene();
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scene, true) };
        AssetDatabase.SaveAssets();

        var opts = new BuildPlayerOptions
        {
            scenes = new[] { scene },
            locationPathName = "build/WebGL",
            target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(opts);
        BuildSummary s = report.summary;
        Debug.Log("BUILD_RESULT: " + s.result + " errors=" + s.totalErrors + " warnings=" + s.totalWarnings + " size=" + s.totalSize + " time=" + s.totalTime);
        if (s.result != BuildResult.Succeeded)
        {
            foreach (var step in report.steps)
                foreach (var m in step.messages)
                    if (m.type == LogType.Error || m.type == LogType.Exception)
                        Debug.Log("BUILD_ERROR: " + m.content);
            return 1;
        }
        return 0;
    }

    public static void SetInputHandler()
    {
        UnityEngine.Object ps = null;
        PlayerSettings[] all = Resources.FindObjectsOfTypeAll<PlayerSettings>();
        if (all != null && all.Length > 0) ps = all[0];
        if (ps == null)
        {
            UnityEngine.Object[] objs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (objs != null && objs.Length > 0) ps = objs[0];
        }
        if (ps == null) { Debug.LogWarning("SetInputHandler: PlayerSettings object not found"); return; }
        var so = new SerializedObject(ps);
        SerializedProperty p = so.FindProperty("activeInputHandler");
        if (p == null) { Debug.LogWarning("SetInputHandler: activeInputHandler property missing"); return; }
        if (p.intValue != 2)
        {
            p.intValue = 2; // 0 = old, 1 = new Input System, 2 = both
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("SetInputHandler: activeInputHandler set to Both");
        }
    }

    static void SetStatic(Type t, string name, object value)
    {
        try
        {
            PropertyInfo pi = t.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
            if (pi != null && pi.CanWrite) { pi.SetValue(null, Convert.ChangeType(value, pi.PropertyType)); Debug.Log("PlayerSettings " + t.Name + "." + name + " = " + value); }
            else Debug.Log("PlayerSettings " + t.Name + "." + name + " not available");
        }
        catch (Exception e) { Debug.LogWarning("SetStatic " + name + ": " + e.Message); }
    }

    static void ConfigurePlayer()
    {
        PlayerSettings.companyName = "Similarize";
        PlayerSettings.productName = "Four Froggies";
        PlayerSettings.colorSpace = ColorSpace.Gamma;
        PlayerSettings.runInBackground = true;
        PlayerSettings.stripEngineCode = false;
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, ManagedStrippingLevel.Low);
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.OpenGLES3 });

        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.nameFilesAsHashes = false;
        PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
        // Moderate memory (property names differ between Unity versions, so set them by reflection)
        SetStatic(typeof(PlayerSettings.WebGL), "initialMemorySize", 128);
        SetStatic(typeof(PlayerSettings.WebGL), "maximumMemorySize", 1024);

        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowDistance = 45f;
    }

    static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }

    static T SaveAsset<T>(T obj, string file) where T : UnityEngine.Object
    {
        string path = Gen + "/" + file;
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(obj, path);
        return obj;
    }

    static Material MakeMat(string shaderName, string fallback, string file)
    {
        Shader sh = Shader.Find(shaderName);
        if (sh == null) { Debug.LogWarning("Shader missing: " + shaderName); sh = Shader.Find(fallback); }
        var m = new Material(sh);
        return SaveAsset(m, file);
    }

    // Standard shader blend modes: Fade (alpha blend) for water, Transparent (premultiplied, keeps highlights) for glass
    static void MakeTransparent(Material m, bool premultiply)
    {
        m.SetFloat("_Mode", premultiply ? 3f : 2f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", premultiply ? (int)BlendMode.One : (int)BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON");
        if (premultiply) { m.DisableKeyword("_ALPHABLEND_ON"); m.EnableKeyword("_ALPHAPREMULTIPLY_ON"); }
        else { m.EnableKeyword("_ALPHABLEND_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON"); }
        m.renderQueue = 3000;
    }

    static Texture2D LoadTex(string path, bool mips, TextureWrapMode wrapU, TextureWrapMode wrapV, int max)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) { Debug.LogWarning("LoadTex: no importer for " + path); return AssetDatabase.LoadAssetAtPath<Texture2D>(path); }
        bool ch = false;
        if (imp.mipmapEnabled != mips) { imp.mipmapEnabled = mips; ch = true; }
        if (imp.wrapModeU != wrapU) { imp.wrapModeU = wrapU; ch = true; }
        if (imp.wrapModeV != wrapV) { imp.wrapModeV = wrapV; ch = true; }
        if (imp.maxTextureSize != max) { imp.maxTextureSize = max; ch = true; }
        if (ch) imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material ShaderMat(string shader, string file)
    {
        Shader sh = Shader.Find(shader);
        if (sh == null) { Debug.LogWarning("BuildScript: shader missing " + shader); return null; }
        Debug.Log("BuildScript: " + shader + " shader");
        return SaveAsset(new Material(sh), file);
    }

    static string CreateScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Material lit = MakeMat("Standard", "Diffuse", "Lit.mat");
        lit.SetFloat("_Glossiness", 0.15f);
        Material unlit = MakeMat("Unlit/Color", "Standard", "Unlit.mat");
        Material fx = MakeMat("Sprites/Default", "Unlit/Color", "Fx.mat");
        Material water = MakeMat("Standard", "Diffuse", "Water.mat");
        water.color = new Color(0.22f, 0.5f, 0.62f, 0.78f);
        MakeTransparent(water, false);
        water.SetFloat("_Glossiness", 0.92f);
        Material glass = MakeMat("Standard", "Diffuse", "Glass.mat");
        glass.color = new Color(0.55f, 0.7f, 0.8f, 0.35f);
        MakeTransparent(glass, true);
        glass.SetFloat("_Glossiness", 0.95f);
        glass.SetFloat("_Metallic", 0.2f);
        EditorUtility.SetDirty(water);
        EditorUtility.SetDirty(glass);
        Material sky = null;
        Texture2D skyTex = LoadTex("Assets/Textures/sky_pano.jpg", false, TextureWrapMode.Repeat, TextureWrapMode.Clamp, 2048);
        Shader pano = Shader.Find("Skybox/Panoramic");
        if (skyTex != null && pano != null)
        {
            sky = SaveAsset(new Material(pano), "Sky.mat");
            sky.SetTexture("_MainTex", skyTex);
            sky.SetFloat("_Mapping", 1f);
            sky.SetFloat("_ImageType", 0f);
            sky.SetFloat("_MirrorOnBack", 0f);
            sky.SetFloat("_Layout", 0f);
            sky.SetFloat("_Exposure", 1.05f);
            sky.SetFloat("_Rotation", 18f);      // puts the HDRI sun where our sun light comes from (az ~142 deg, elev 48)
            sky.EnableKeyword("_MAPPING_LATITUDE_LONGITUDE_LAYOUT");
            sky.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");
            Debug.Log("BuildScript: HDRI sky");
        }
        else
        {
            Debug.LogWarning("BuildScript: HDRI sky missing (tex " + (skyTex != null) + ", shader " + (pano != null) + ") - procedural");
            sky = MakeMat("Skybox/Procedural", "Skybox/Procedural", "Sky.mat");
            sky.SetFloat("_SunSize", 0.045f);
            sky.SetFloat("_SunSizeConvergence", 5f);
            sky.SetFloat("_AtmosphereThickness", 0.85f);
            sky.SetColor("_SkyTint", new Color(0.45f, 0.58f, 0.78f));
            sky.SetColor("_GroundColor", new Color(0.42f, 0.45f, 0.40f));
            sky.SetFloat("_Exposure", 1.3f);
        }
        EditorUtility.SetDirty(sky);
        EditorUtility.SetDirty(lit);
        // graphics overhaul shaders (Assets/Shaders): frog skin, foliage, animated pond water
        Material skin = ShaderMat("FF/Skin", "Skin.mat");
        Material foliage = ShaderMat("FF/Foliage", "Foliage.mat");
        Material pond = ShaderMat("LB/Water", "Pond.mat");
        // stage B: underwater caustics surfaces, unlit textured sky spheres (space starfield, Jupiter in Callisto's sky)
        Material underwater = ShaderMat("FF/Underwater", "Underwater.mat");
        Material unlitTex = ShaderMat("Unlit/Texture", "UnlitTex.mat");
        // ?realism=1 test (Realism.cs): triplanar scanned rock / bark, realistic pond water (variants ship via these assets)
        Material realTri = ShaderMat("FF/RealTri", "RealTri.mat");
        Material realWater = ShaderMat("FF/RealWater", "RealWater.mat");

        try
        {
            var ls = new LightingSettings();
            ls.name = "Lighting";
            ls.bakedGI = false;
            ls.realtimeGI = false;
            ls.autoGenerate = false;
            SaveAsset(ls, "Lighting.lighting");
            Lightmapping.lightingSettings = ls;
        }
        catch (Exception e) { Debug.LogWarning("LightingSettings: " + e.Message); }

        RenderSettings.skybox = sky;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.0042f;
        RenderSettings.fogColor = new Color(0.70f, 0.78f, 0.86f);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.70f, 0.82f);
        RenderSettings.ambientEquatorColor = new Color(0.50f, 0.54f, 0.48f);
        RenderSettings.ambientGroundColor = new Color(0.28f, 0.25f, 0.20f);

        var sunGo = new GameObject("Sun");
        Light sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.95f, 0.84f);
        sun.intensity = 1.15f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.65f;
        sun.shadowBias = 0.05f;
        sun.shadowNormalBias = 0.4f;
        sunGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        RenderSettings.sun = sun;

        Terrain terrain = CreateTerrain();

        var boot = new GameObject("Bootstrap");
        Bootstrap b = boot.AddComponent<Bootstrap>();
        b.litMat = lit;
        b.unlitMat = unlit;
        b.fxMat = fx;
        b.waterMat = water;
        b.glassMat = glass;
        b.terrain = terrain;
        b.sun = sun;
        b.skinMat = skin;
        b.foliageMat = foliage;
        b.pondMat = pond;
        b.underwaterMat = underwater;
        b.unlitTexMat = unlitTex;
        b.realTriMat = realTri;
        b.realWaterMat = realWater;
        b.realTerrainMat = terrainNM;

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new Exception("Failed to save scene");
        AssetDatabase.SaveAssets();
        Debug.Log("BuildScript: scene saved " + ScenePath);
        return ScenePath;
    }

    // ---------- terrain ----------
    static float[] Grid(System.Random r, int n)
    {
        var g = new float[n * n];
        for (int i = 0; i < g.Length; i++) g[i] = (float)r.NextDouble();
        return g;
    }

    static float ValNoise(float[] g, int gs, int x, int y, int n)
    {
        float fx = x * gs / (float)n, fy = y * gs / (float)n;
        int x0 = (int)fx, y0 = (int)fy;
        float tx = fx - x0, ty = fy - y0;
        tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty);
        int x1 = (x0 + 1) % gs, y1 = (y0 + 1) % gs; x0 %= gs; y0 %= gs;
        float a = Mathf.Lerp(g[y0 * gs + x0], g[y0 * gs + x1], tx);
        float b = Mathf.Lerp(g[y1 * gs + x0], g[y1 * gs + x1], tx);
        return Mathf.Lerp(a, b, ty);
    }

    static Texture2D MakeTex(string name, Color a, Color b, int seed, float speckle)
    {
        const int n = 256;
        var r = new System.Random(seed);
        float[] g1 = Grid(r, 8), g2 = Grid(r, 32), g3 = Grid(r, 64);
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 4;
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float v = 0.45f * ValNoise(g1, 8, x, y, n) + 0.3f * ValNoise(g2, 32, x, y, n) + 0.15f * ValNoise(g3, 64, x, y, n) + 0.1f * (float)r.NextDouble();
                Color c = Color.Lerp(a, b, v);
                double s = r.NextDouble();
                if (s < speckle) c = Color.Lerp(c, b * 1.25f, 0.6f);
                else if (s < speckle * 2) c *= 0.8f;
                c.a = 1f;
                px[y * n + x] = c;
            }
        tex.SetPixels(px);
        tex.Apply(true);
        return SaveAsset(tex, name + ".asset");
    }

    static Material terrainNM;
    static Terrain CreateTerrain()
    {
        const int res = 513;
        var td = new TerrainData();
        td.heightmapResolution = res;
        float size = Layout.Half * 2f;
        td.size = new Vector3(size, Layout.TerrainH, size);
        var h = new float[res, res];
        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                float wx = -Layout.Half + x * size / (res - 1), wz = -Layout.Half + z * size / (res - 1);
                float y = Layout.GroundY(wx, wz);
                h[z, x] = Mathf.Clamp01((y - Layout.TerrainY) / Layout.TerrainH);
            }
        td.SetHeights(0, 0, h);

        // Poly Haven CC0 based textures (work/lb-gfx/ff/make_ff_tex.py); generated noise if missing
        Texture2D grass = LoadTex("Assets/Textures/grass.jpg", true, TextureWrapMode.Repeat, TextureWrapMode.Repeat, 512);
        Texture2D dirt = LoadTex("Assets/Textures/dirt.jpg", true, TextureWrapMode.Repeat, TextureWrapMode.Repeat, 512);
        Texture2D sand = LoadTex("Assets/Textures/sand.jpg", true, TextureWrapMode.Repeat, TextureWrapMode.Repeat, 512);
        Debug.Log("BuildScript: terrain textures " + (grass != null) + " " + (dirt != null) + " " + (sand != null));
        if (grass == null) grass = MakeTex("GrassTex", new Color(0.28f, 0.46f, 0.15f), new Color(0.46f, 0.6f, 0.22f), 7, 0.06f);
        if (dirt == null) dirt = MakeTex("DirtTex", new Color(0.45f, 0.35f, 0.24f), new Color(0.62f, 0.52f, 0.38f), 11, 0.03f);
        if (sand == null) sand = MakeTex("SandTex", new Color(0.55f, 0.5f, 0.38f), new Color(0.7f, 0.65f, 0.5f), 13, 0.02f);
        var gl = new TerrainLayer { diffuseTexture = grass, tileSize = new Vector2(6f, 6f) };
        var dl = new TerrainLayer { diffuseTexture = dirt, tileSize = new Vector2(5f, 5f) };
        var sl = new TerrainLayer { diffuseTexture = sand, tileSize = new Vector2(5f, 5f) };
        SaveAsset(gl, "Grass.terrainlayer");
        SaveAsset(dl, "Dirt.terrainlayer");
        SaveAsset(sl, "Sand.terrainlayer");
        td.terrainLayers = new[] { gl, dl, sl };
        const int ar = 256;
        td.alphamapResolution = ar;
        var am = new float[ar, ar, 3];
        for (int y = 0; y < ar; y++)
            for (int x = 0; x < ar; x++)
            {
                float wx = -Layout.Half + (x + 0.5f) * size / ar, wz = -Layout.Half + (y + 0.5f) * size / ar;
                float road = Mathf.Clamp01((Layout.TrackW * 0.5f + 1f - Layout.RoadDist(wx, wz)) / 2f);
                // dirt apron in front of the garage
                if (wx > Layout.GarageC.x - 30f && wx < Layout.GarageC.x + 28f && wz > 30f && wz < 40f) road = Mathf.Max(road, 0.8f);
                float patch = Mathf.Clamp01((Mathf.PerlinNoise(wx * 0.03f + 3f, wz * 0.03f + 7f) - 0.68f) * 3f) * 0.7f;
                float q = Layout.PondQ(wx, wz);
                float shore = Mathf.Clamp01((1.3f - q) / 0.2f);
                float d = Mathf.Max(road, patch) * (1f - shore);
                am[y, x, 0] = Mathf.Clamp01(1f - d - shore);
                am[y, x, 1] = d;
                am[y, x, 2] = shore;
            }
        td.SetAlphamaps(0, 0, am);
        SaveAsset(td, "Field.asset");

        GameObject go = Terrain.CreateTerrainGameObject(td);
        go.name = "Field";
        go.transform.position = new Vector3(-Layout.Half, Layout.TerrainY, -Layout.Half);
        Terrain t = go.GetComponent<Terrain>();
        Shader ts = Shader.Find("Nature/Terrain/Standard");
        if (ts != null)
        {
            var tm = new Material(ts);
            SaveAsset(tm, "TerrainMat.mat");
            t.materialTemplate = tm;
            // realism test: a copy with _NORMALMAP so the normal-mapped terrain variant is in the build
            terrainNM = new Material(ts);
            terrainNM.EnableKeyword("_NORMALMAP");
            SaveAsset(terrainNM, "TerrainMatNM.mat");
            Debug.Log("BuildScript: terrain NM material");
        }
        t.heightmapPixelError = 6f;
        t.basemapDistance = 300f;
        t.drawTreesAndFoliage = false;
        EditorUtility.SetDirty(td);
        return t;
    }
}
