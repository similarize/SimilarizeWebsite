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
        PlayerSettings.productName = "Lambo Blast";
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

    static string CreateScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Material lit = MakeMat("Standard", "Diffuse", "Lit.mat");
        lit.SetFloat("_Glossiness", 0.15f);
        Material unlit = MakeMat("Unlit/Color", "Standard", "Unlit.mat");
        Material fx = MakeMat("Sprites/Default", "Unlit/Color", "Fx.mat");
        Material water = MakeMat("Standard", "Diffuse", "Water.mat");
        water.color = new Color(0.1f, 0.62f, 0.72f, 0.72f);
        MakeTransparent(water, false);
        water.SetFloat("_Glossiness", 0.92f);
        Material glass = MakeMat("Standard", "Diffuse", "Glass.mat");
        glass.color = new Color(0.55f, 0.7f, 0.8f, 0.35f);
        MakeTransparent(glass, true);
        glass.SetFloat("_Glossiness", 0.95f);
        glass.SetFloat("_Metallic", 0.2f);
        EditorUtility.SetDirty(water);
        EditorUtility.SetDirty(glass);
        Material sky = MakeMat("Skybox/Procedural", "Skybox/Procedural", "Sky.mat");
        sky.SetFloat("_SunSize", 0.045f);
        sky.SetFloat("_SunSizeConvergence", 5f);
        sky.SetFloat("_AtmosphereThickness", 0.85f);
        sky.SetColor("_SkyTint", new Color(0.4f, 0.62f, 0.9f));
        sky.SetColor("_GroundColor", new Color(0.35f, 0.6f, 0.68f));
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
        RenderSettings.fogDensity = 0.0022f;
        RenderSettings.fogColor = new Color(0.72f, 0.84f, 0.92f);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.70f, 0.82f);
        RenderSettings.ambientEquatorColor = new Color(0.62f, 0.62f, 0.55f);
        RenderSettings.ambientGroundColor = new Color(0.45f, 0.40f, 0.32f);

        var sunGo = new GameObject("Sun");
        Light sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.95f, 0.84f);
        sun.intensity = 1.25f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.65f;
        sun.shadowBias = 0.05f;
        sun.shadowNormalBias = 0.4f;
        sunGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        RenderSettings.sun = sun;

        var boot = new GameObject("Bootstrap");
        Bootstrap b = boot.AddComponent<Bootstrap>();
        b.litMat = lit;
        b.unlitMat = unlit;
        b.fxMat = fx;
        b.waterMat = water;
        b.glassMat = glass;
        b.sun = sun;

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new Exception("Failed to save scene");
        AssetDatabase.SaveAssets();
        Debug.Log("BuildScript: scene saved " + ScenePath);
        return ScenePath;
    }
}
