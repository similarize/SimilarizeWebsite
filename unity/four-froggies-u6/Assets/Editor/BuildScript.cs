using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// ffu6: Four Froggies on Unity 6000.6.5f1 + URP 17.6 (side by side with the 2022.3 project unity/four-froggies). Two editor starts
// (as Lambo Blast u6): BuildScript.SetupURP (pass 1: Linear + WebGPU / WebGL2 player settings, the URP asset = Forward+, HDR,
// soft shadows, rendering layers, SSAO feature; exits) then BuildScript.BuildWebGL (pass 2, boots with URP active).
// WebGL only in this first milestone (no Windows / Android entry points here).

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
        if (!(GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset))
        {
            Debug.LogWarning("BuildScript: no URP asset active (setup pass missing?) - making it now");
            MakeURP();
        }
        Debug.Log("BuildScript: pipeline " + GraphicsSettings.defaultRenderPipeline.name + " colour " + PlayerSettings.colorSpace + " apis " + string.Join(",", PlayerSettings.GetGraphicsAPIs(BuildTarget.WebGL)));

        DefineRenderingLayers();
        string scene = CreateScene();
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scene, true) };
        AssetDatabase.SaveAssets();

        var opts = new BuildPlayerOptions
        {
            scenes = new[] { scene },
            locationPathName = "build/WebGL",
            target = BuildTarget.WebGL,
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
        PlayerSettings.productName = "Four Froggies 6.6";
        // ffu6: Linear + HDR (URP tonemapping); the Built-in project was Gamma
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.runInBackground = true;
        PlayerSettings.stripEngineCode = false;
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
        // ffu6: WebGPU first, WebGL 2 as the automatic fallback (Unity 6.6 production WebGPU)
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.WebGPU, GraphicsDeviceType.OpenGLES3 });

        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.nameFilesAsHashes = false;
        // Moderate memory (property names differ between Unity versions, so set them by reflection)
        SetStatic(typeof(PlayerSettings.WebGL), "initialMemorySize", 128);
        SetStatic(typeof(PlayerSettings.WebGL), "maximumMemorySize", 1024);
        Debug.Log("BuildScript: WebGL graphics APIs " + string.Join(",", PlayerSettings.GetGraphicsAPIs(BuildTarget.WebGL)) + " colour " + PlayerSettings.colorSpace);
    }

    // ffu6 pass 1 (own editor start, before BuildWebGL): player settings + the URP asset as the default pipeline + URP global
    // settings, then exit, so the BuildWebGL editor start already boots with URP active (shader stripping + global settings).
    public static void SetupURP()
    {
        int code = 0;
        try
        {
            Debug.Log("URPSETUP: start, Unity " + Application.unityVersion);
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            SetInputHandler();
            ConfigurePlayer();
            EnsureFolder("Assets", "Generated");
            UniversalRenderPipelineAsset a = MakeURP();
            DefineRenderingLayers();
            AssetDatabase.SaveAssets();
            Debug.Log("URPSETUP: ok asset " + AssetDatabase.GetAssetPath(a) + " default " + (GraphicsSettings.defaultRenderPipeline != null ? GraphicsSettings.defaultRenderPipeline.name : "null") +
                " colour " + PlayerSettings.colorSpace + " apis " + string.Join(",", PlayerSettings.GetGraphicsAPIs(BuildTarget.WebGL)));
        }
        catch (Exception e)
        {
            Debug.LogError("URPSETUP_EXCEPTION: " + e);
            code = 2;
        }
        EditorApplication.Exit(code);
    }

    // ffu6h: Unity 6 masks every light / shadow rendering layer with the layers DEFINED in Tags and Layers (only "Default" in a
    // fresh project) -> the lobby studio lights on rendering layer 2 came out as layer 0 = matched nothing: black portraits.
    // Define layers 1 and 2 (names only; U6Layers uses bit 0 world, bit 2 studio).
    static void DefineRenderingLayers()
    {
        try
        {
            var objs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (objs == null || objs.Length == 0) { Debug.LogWarning("URPSETUP: TagManager.asset not found"); return; }
            var so = new SerializedObject(objs[0]);
            var p = so.FindProperty("m_RenderingLayers");
            if (p == null || !p.isArray) { Debug.LogWarning("URPSETUP: TagManager.m_RenderingLayers missing"); return; }
            string[] want = { "Default", "World Extra", "Studio" };
            int old = p.arraySize;
            while (p.arraySize < want.Length) p.InsertArrayElementAtIndex(p.arraySize);
            for (int i = 1; i < want.Length; i++)
            {
                var e = p.GetArrayElementAtIndex(i);
                if (i >= old || string.IsNullOrEmpty(e.stringValue)) e.stringValue = want[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("URPSETUP: rendering layers defined " + p.arraySize + " (was " + old + "), mask " + RenderingLayerMask.GetDefinedRenderingLayersCombinedMaskValue());
        }
        catch (Exception e) { Debug.LogWarning("URPSETUP: rendering layers failed " + e.Message); }
    }

    const string UrpPath = Gen + "/FF_URP.asset", UrpRendererPath = Gen + "/FF_URP_Renderer.asset";

    static void SetProp(SerializedObject so, string name, Action<SerializedProperty> set)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null) { Debug.LogWarning("URPSETUP: property missing " + so.targetObject.GetType().Name + "." + name); return; }
        set(p);
    }

    // URP asset made in code (Lambo u6 MakeURP): Forward+, HDR, MSAA 2 (runtime U6 mirror sets 1 / 2 / 4), main light shadows
    // 2048 / 60 m, 2 cascades, soft (medium), rendering layers, reflection probe blending, SSAO renderer feature (off; U6 turns it on)
    static UniversalRenderPipelineAsset MakeURP()
    {
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(UrpPath) != null) AssetDatabase.DeleteAsset(UrpPath);
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(UrpRendererPath) != null) AssetDatabase.DeleteAsset(UrpRendererPath);
        ScriptableRendererData data = null;
        try
        {
            MethodInfo mi = typeof(UniversalRenderPipelineAsset).GetMethod("CreateRendererAsset", BindingFlags.NonPublic | BindingFlags.Static);
            if (mi != null) data = mi.Invoke(null, new object[] { UrpRendererPath, RendererType.UniversalRenderer, false, "Renderer" }) as ScriptableRendererData;
            Debug.Log("URPSETUP: renderer via CreateRendererAsset " + (data != null));
        }
        catch (Exception e) { Debug.LogWarning("URPSETUP: CreateRendererAsset failed " + e.Message); }
        if (data == null)
        {
            var ud = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(ud, UrpRendererPath);
            data = ud;
        }
        var sod = new SerializedObject(data);
        SetProp(sod, "m_RenderingMode", p => p.intValue = 2);       // Forward+
        SetProp(sod, "m_DepthPrimingMode", p => p.intValue = 0);
        sod.ApplyModifiedPropertiesWithoutUndo();
        var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
        ssao.name = "FF SSAO";
        AssetDatabase.AddObjectToAsset(ssao, data);
        var sso = new SerializedObject(ssao);
        SetProp(sso, "m_Settings.Intensity", p => p.floatValue = 0.9f);
        SetProp(sso, "m_Settings.Radius", p => p.floatValue = 0.4f);
        SetProp(sso, "m_Settings.DirectLightingStrength", p => p.floatValue = 0.2f);
        SetProp(sso, "m_Settings.Downsample", p => p.boolValue = true);
        SetProp(sso, "m_Settings.Samples", p => p.intValue = 1);          // Medium
        SetProp(sso, "m_Settings.BlurQuality", p => p.intValue = 1);      // Gaussian
        SetProp(sso, "m_Settings.Falloff", p => p.floatValue = 60f);
        sso.ApplyModifiedPropertiesWithoutUndo();
        // ffu6c: keep the feature ACTIVE in the asset (an inactive one gets its shader resources stripped -> "Couldn't find the required resources"); U6.SsaoOn toggles it at runtime
        long lid = 0; string guid;
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out guid, out lid);
        sod = new SerializedObject(data);
        SetProp(sod, "m_RendererFeatures", p => { p.arraySize = 1; p.GetArrayElementAtIndex(0).objectReferenceValue = ssao; });
        SetProp(sod, "m_RendererFeatureMap", p => { p.arraySize = 1; p.GetArrayElementAtIndex(0).longValue = lid; });
        sod.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);

        UniversalRenderPipelineAsset a = UniversalRenderPipelineAsset.Create(data);
        a.name = "FF_URP";
        AssetDatabase.CreateAsset(a, UrpPath);
        var so = new SerializedObject(a);
        SetProp(so, "m_SupportsHDR", p => p.boolValue = true);
        SetProp(so, "m_MSAA", p => p.intValue = 2);
        SetProp(so, "m_RenderScale", p => p.floatValue = 1f);
        SetProp(so, "m_RequireDepthTexture", p => p.boolValue = false);
        SetProp(so, "m_RequireOpaqueTexture", p => p.boolValue = false);
        SetProp(so, "m_MainLightRenderingMode", p => p.intValue = 1);
        SetProp(so, "m_MainLightShadowsSupported", p => p.boolValue = true);
        SetProp(so, "m_MainLightShadowmapResolution", p => p.intValue = 2048);
        SetProp(so, "m_AdditionalLightsRenderingMode", p => p.intValue = 1);
        SetProp(so, "m_AdditionalLightsPerObjectLimit", p => p.intValue = 4);
        SetProp(so, "m_AdditionalLightShadowsSupported", p => p.boolValue = false);
        SetProp(so, "m_ReflectionProbeBlending", p => p.boolValue = true);
        SetProp(so, "m_ShadowDistance", p => p.floatValue = 60f);
        SetProp(so, "m_ShadowCascadeCount", p => p.intValue = 2);
        SetProp(so, "m_Cascade2Split", p => p.floatValue = 0.25f);
        SetProp(so, "m_ShadowDepthBias", p => p.floatValue = 1.0f);
        SetProp(so, "m_ShadowNormalBias", p => p.floatValue = 0.8f);
        SetProp(so, "m_SoftShadowsSupported", p => p.boolValue = true);
        SetProp(so, "m_SoftShadowQuality", p => p.intValue = 2);
        SetProp(so, "m_SupportsLightLayers", p => p.boolValue = true);
        SetProp(so, "m_SupportsLightCookies", p => p.boolValue = false);
        SetProp(so, "m_MixedLightingSupported", p => p.boolValue = false);
        SetProp(so, "m_UseSRPBatcher", p => p.boolValue = true);
        SetProp(so, "m_ColorGradingMode", p => p.intValue = 1);
        SetProp(so, "m_ColorGradingLutSize", p => p.intValue = 32);
        SetProp(so, "m_VolumeFrameworkUpdateMode", p => p.intValue = 0);
        SetProp(so, "m_EnableLODCrossFade", p => p.boolValue = false);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(a);
        AssetDatabase.SaveAssets();

        GraphicsSettings.defaultRenderPipeline = a;
        int cur = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = a; }
        QualitySettings.SetQualityLevel(cur, false);
        try { EditorUtility.SetDirty(GraphicsSettings.GetGraphicsSettings()); } catch (Exception e) { Debug.LogWarning("URPSETUP: graphics settings dirty " + e.Message); }
        AssetDatabase.SaveAssets();
        try
        {
            Type gs = typeof(UniversalRenderPipelineAsset).Assembly.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings");
            MethodInfo en = gs != null ? gs.GetMethod("Ensure", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static) : null;
            object r = en != null ? en.Invoke(null, new object[] { true }) : null;
            Debug.Log("URPSETUP: global settings " + (r != null ? AssetDatabase.GetAssetPath((UnityEngine.Object)r) : "not ensured (method " + (en != null) + ")"));
        }
        catch (Exception e) { Debug.LogWarning("URPSETUP: global settings ensure failed " + e.Message); }
        Debug.Log("URPSETUP: URP asset Forward+ HDR MSAA2 shadows 2048/60m 2 cascades soft, rendering layers, SSAO feature lid " + lid);
        return a;
    }

    static ScriptableRendererFeature FindSsao()
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(UrpRendererPath))
            if (o is ScreenSpaceAmbientOcclusion) return (ScriptableRendererFeature)o;
        Debug.LogWarning("BuildScript: SSAO feature not found in " + UrpRendererPath);
        return null;
    }

    // global post: ACES, colour adjustments, bloom, vignette (+ a touch of warmth in the game grade); U6.Grade sets the values
    static VolumeProfile MakeProfile(string file, bool studio)
    {
        var p = SaveAsset(ScriptableObject.CreateInstance<VolumeProfile>(), file);
        var tm = p.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.ACES);
        var ca = p.Add<ColorAdjustments>(true);
        ca.postExposure.Override(studio ? 0.6f : 0.35f);
        ca.contrast.Override(studio ? 6f : 8f);
        ca.saturation.Override(studio ? 4f : -6f);
        var bl = p.Add<Bloom>(true); bl.intensity.Override(studio ? 0.3f : 0.32f); bl.threshold.Override(1.05f); bl.scatter.Override(0.6f);
        var vg = p.Add<Vignette>(true); vg.intensity.Override(studio ? 0.28f : 0.18f); vg.smoothness.Override(0.5f);
        if (!studio) { var wb = p.Add<WhiteBalance>(true); wb.temperature.Override(4f); }
        foreach (var c in p.components) { c.name = c.GetType().Name; AssetDatabase.AddObjectToAsset(c, p); }
        EditorUtility.SetDirty(p);
        AssetDatabase.SaveAssets();
        return p;
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
        m.SetShaderPassEnabled("ShadowCaster", false);   // ffu6: URP casts shadows from transparent queues too
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

        Material lit = MakeMat("FF/Std", "Universal Render Pipeline/Lit", "Lit.mat");   // ffu6: URP stand-in for Standard (same property names)
        lit.SetFloat("_Glossiness", 0.15f);
        Material unlit = MakeMat("FF/Unlit", "Universal Render Pipeline/Unlit", "Unlit.mat");
        Material fx = MakeMat("Sprites/Default", "Unlit/Color", "Fx.mat");
        Material water = MakeMat("FF/Std", "Universal Render Pipeline/Lit", "Water.mat");
        water.color = new Color(0.22f, 0.5f, 0.62f, 0.78f);
        MakeTransparent(water, false);
        water.SetFloat("_Glossiness", 0.92f);
        Material glass = MakeMat("FF/Std", "Universal Render Pipeline/Lit", "Glass.mat");
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
        // ffu60b: no GPU instancing on the base Standard / Unlit materials (60a had it: it stopped Unity's dynamic batching,
        // the ~40-cube batches became single / 2-4 instance draws = more draw calls). Left as before ffu60.
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
        // ffu56: instanced ranch grass (FF/Grass56); enableInstancing on a scene material keeps the instancing variants in the build
        Material grass56 = ShaderMat("FF/Grass56", "Grass56.mat");
        // ffu60: trees (leaves + scanned bark / rock) repeat one mesh + material many times: instanced
        if (foliage != null) { foliage.enableInstancing = true; EditorUtility.SetDirty(foliage); }
        if (realTri != null) { realTri.enableInstancing = true; EditorUtility.SetDirty(realTri); }
        if (grass56 != null) { grass56.enableInstancing = true; EditorUtility.SetDirty(grass56); }
        // ffu57: one skin shader for every animal + froggy (fur / wet skin); a scene material keeps it in the build
        Material skin57 = ShaderMat("FF/Skin57", "Skin57.mat");

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
        b.grass56Mat = grass56;
        b.skin57Mat = skin57;
        // ffu6: URP post volumes + the SSAO renderer feature
        b.gameProfile = MakeProfile("GameVolume.asset", false);
        b.studioProfile = MakeProfile("StudioVolume.asset", true);
        b.ssaoFeature = FindSsao();
        Debug.Log("BuildScript: ffu6 volumes game " + (b.gameProfile != null) + " studio " + (b.studioProfile != null) + " ssao " + (b.ssaoFeature != null) +
            " shaders FF/Std " + (Shader.Find("FF/Std") != null) + " FF/Unlit " + (Shader.Find("FF/Unlit") != null) + " FF/Skin57 " + (Shader.Find("FF/Skin57") != null) +
            " FF/Grass56 " + (Shader.Find("FF/Grass56") != null) + " URP terrain " + (Shader.Find("Universal Render Pipeline/Terrain/Lit") != null));

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
                float shore = Mathf.Clamp01((1.1f - q) / 0.08f);   // ffu39: sand only on the bank lip (Layout.PondRim), grass above
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
        Shader ts = Shader.Find("Universal Render Pipeline/Terrain/Lit");   // ffu6 (was Nature/Terrain/Standard)
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
