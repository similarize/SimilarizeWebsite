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
        PlayerSettings.productName = "Balloon Blast Airsoft";
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
        QualitySettings.shadowDistance = 70f;
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

    static string CreateScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Material lit = MakeMat("Standard", "Diffuse", "Lit.mat");
        lit.SetFloat("_Glossiness", 0.15f);
        Material unlit = MakeMat("Unlit/Color", "Standard", "Unlit.mat");
        Material fx = MakeMat("Sprites/Default", "Unlit/Color", "Fx.mat");
        Material sky = MakeMat("Skybox/Procedural", "Skybox/Procedural", "Sky.mat");
        sky.SetFloat("_SunSize", 0.045f);
        sky.SetFloat("_SunSizeConvergence", 5f);
        sky.SetFloat("_AtmosphereThickness", 0.85f);
        sky.SetColor("_SkyTint", new Color(0.45f, 0.58f, 0.78f));
        sky.SetColor("_GroundColor", new Color(0.42f, 0.45f, 0.40f));
        sky.SetFloat("_Exposure", 1.3f);
        EditorUtility.SetDirty(sky);
        EditorUtility.SetDirty(lit);

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
        RenderSettings.fogDensity = 0.0075f;
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
        sun.shadowStrength = 0.72f;
        sun.shadowBias = 0.05f;
        sun.shadowNormalBias = 0.4f;
        sunGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
        RenderSettings.sun = sun;

        Terrain terrain = CreateTerrain();

        var boot = new GameObject("Bootstrap");
        Bootstrap b = boot.AddComponent<Bootstrap>();
        b.litMat = lit;
        b.unlitMat = unlit;
        b.fxMat = fx;
        b.terrain = terrain;
        b.sun = sun;

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

    static Terrain CreateTerrain()
    {
        const int res = 257;
        var td = new TerrainData();
        td.heightmapResolution = res;
        td.size = new Vector3(250f, 14f, 250f);
        var h = new float[res, res];
        float ox = 37.1f, oz = 91.7f;
        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                float u = x / (float)(res - 1), v = z / (float)(res - 1);
                float n = Mathf.PerlinNoise(ox + u * 3.2f, oz + v * 3.2f) * 0.55f
                        + Mathf.PerlinNoise(ox * 2f + u * 8f, oz * 2f + v * 8f) * 0.2f
                        + Mathf.PerlinNoise(u * 22f + 5f, v * 22f + 9f) * 0.04f;
                float edge = Mathf.Max(Mathf.Abs(u - 0.5f), Mathf.Abs(v - 0.5f)) * 2f;
                float berm = Mathf.SmoothStep(0f, 1f, (edge - 0.84f) / 0.16f);
                h[z, x] = n * 0.42f + berm * 0.5f;
            }
        td.SetHeights(0, 0, h);

        Texture2D grass = MakeTex("GrassTex", new Color(0.26f, 0.42f, 0.13f), new Color(0.47f, 0.60f, 0.22f), 7, 0.06f);
        Texture2D dirt = MakeTex("DirtTex", new Color(0.40f, 0.32f, 0.22f), new Color(0.58f, 0.50f, 0.36f), 11, 0.03f);
        var gl = new TerrainLayer { diffuseTexture = grass, tileSize = new Vector2(6f, 6f) };
        var dl = new TerrainLayer { diffuseTexture = dirt, tileSize = new Vector2(5f, 5f) };
        SaveAsset(gl, "Grass.terrainlayer");
        SaveAsset(dl, "Dirt.terrainlayer");
        td.terrainLayers = new[] { gl, dl };
        td.alphamapResolution = 128;
        var am = new float[128, 128, 2];
        for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float d = Mathf.PerlinNoise(x * 0.07f + 3f, y * 0.07f + 7f);
                float w = Mathf.Clamp01((d - 0.6f) * 4f);
                am[y, x, 0] = 1f - w;
                am[y, x, 1] = w;
            }
        td.SetAlphamaps(0, 0, am);
        SaveAsset(td, "Field.asset");

        GameObject go = Terrain.CreateTerrainGameObject(td);
        go.name = "Field";
        Terrain t = go.GetComponent<Terrain>();
        Shader ts = Shader.Find("Nature/Terrain/Standard");
        if (ts != null)
        {
            var tm = new Material(ts);
            SaveAsset(tm, "TerrainMat.mat");
            t.materialTemplate = tm;
        }
        t.heightmapPixelError = 4f;
        t.basemapDistance = 400f;
        t.drawTreesAndFoliage = false;
        EditorUtility.SetDirty(td);
        return t;
    }
}
