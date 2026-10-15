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

    // lambou6 pass 1 (own editor start, before BuildWebGL): Linear + WebGPU/WebGL2 player settings, the URP asset (Forward+,
    // HDR, soft shadows 2 cascades, rendering layers, SSAO feature) as the default pipeline, URP global settings. Exiting
    // here means the BuildWebGL editor start already boots with URP active (shader stripping + global settings in place).
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

    const string UrpPath = Gen + "/LB_URP.asset", UrpRendererPath = Gen + "/LB_URP_Renderer.asset";
    const string ShaftsMatPath = Gen + "/LBShafts.mat";

    static void SetProp(SerializedObject so, string name, Action<SerializedProperty> set)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null) { Debug.LogWarning("URPSETUP: property missing " + so.targetObject.GetType().Name + "." + name); return; }
        set(p);
    }

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
        // Forward+ renderer
        var sod = new SerializedObject(data);
        SetProp(sod, "m_RenderingMode", p => p.intValue = 2);
        SetProp(sod, "m_DepthPrimingMode", p => p.intValue = 0);
        sod.ApplyModifiedPropertiesWithoutUndo();
        // SSAO renderer feature (sub-asset of the renderer; toggled at runtime by U6.SsaoOn)
        var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
        ssao.name = "LB SSAO";
        AssetDatabase.AddObjectToAsset(ssao, data);
        var sso = new SerializedObject(ssao);
        SetProp(sso, "m_Settings.Intensity", p => p.floatValue = 1.1f);
        SetProp(sso, "m_Settings.Radius", p => p.floatValue = 0.35f);
        SetProp(sso, "m_Settings.DirectLightingStrength", p => p.floatValue = 0.15f);
        SetProp(sso, "m_Settings.Downsample", p => p.boolValue = true);
        SetProp(sso, "m_Settings.Samples", p => p.intValue = 1);          // Medium
        SetProp(sso, "m_Settings.BlurQuality", p => p.intValue = 1);      // Gaussian
        SetProp(sso, "m_Settings.Falloff", p => p.floatValue = 60f);
        sso.ApplyModifiedPropertiesWithoutUndo();
        long lid = 0; string guid;
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out guid, out lid);
        // u6d: god rays + heat haze = URP's FullScreenPassRendererFeature with the LB/Shafts material, before post (off by default;
        // Gfx6.ApplyViews turns it on for one desktop view at level 0 with temporal AA)
        FullScreenPassRendererFeature shaft = null; long lid2 = 0;
        Shader shs = Shader.Find("LB/Shafts");
        if (shs != null)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ShaftsMatPath) != null) AssetDatabase.DeleteAsset(ShaftsMatPath);
            var sm = new Material(shs) { name = "LBShafts" };
            AssetDatabase.CreateAsset(sm, ShaftsMatPath);
            shaft = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            shaft.name = "LB Shafts";
            shaft.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
            shaft.requirements = ScriptableRenderPassInput.Depth;
            shaft.fetchColorBuffer = true;
            shaft.passMaterial = sm;
            shaft.passIndex = 0;
            shaft.SetActive(false);
            AssetDatabase.AddObjectToAsset(shaft, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(shaft, out guid, out lid2);
        }
        else Debug.LogWarning("URPSETUP: LB/Shafts shader missing - no god rays feature");
        sod = new SerializedObject(data);
        int nf = shaft != null ? 2 : 1;
        SetProp(sod, "m_RendererFeatures", p => { p.arraySize = nf; p.GetArrayElementAtIndex(0).objectReferenceValue = ssao; if (shaft != null) p.GetArrayElementAtIndex(1).objectReferenceValue = shaft; });
        SetProp(sod, "m_RendererFeatureMap", p => { p.arraySize = nf; p.GetArrayElementAtIndex(0).longValue = lid; if (shaft != null) p.GetArrayElementAtIndex(1).longValue = lid2; });
        sod.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log("URPSETUP: u6d shafts feature " + (shaft != null) + " lid " + lid2);
        EditorUtility.SetDirty(data);

        UniversalRenderPipelineAsset a = UniversalRenderPipelineAsset.Create(data);
        a.name = "LB_URP";
        AssetDatabase.CreateAsset(a, UrpPath);
        var so = new SerializedObject(a);
        SetProp(so, "m_SupportsHDR", p => p.boolValue = true);
        SetProp(so, "m_MSAA", p => p.intValue = 2);
        SetProp(so, "m_RenderScale", p => p.floatValue = 1f);
        SetProp(so, "m_RequireDepthTexture", p => p.boolValue = false);
        SetProp(so, "m_RequireOpaqueTexture", p => p.boolValue = false);
        SetProp(so, "m_MainLightRenderingMode", p => p.intValue = 1);               // per pixel
        SetProp(so, "m_MainLightShadowsSupported", p => p.boolValue = true);
        SetProp(so, "m_MainLightShadowmapResolution", p => p.intValue = 4096);
        SetProp(so, "m_AdditionalLightsRenderingMode", p => p.intValue = 1);
        SetProp(so, "m_AdditionalLightsPerObjectLimit", p => p.intValue = 4);
        SetProp(so, "m_AdditionalLightShadowsSupported", p => p.boolValue = false);
        SetProp(so, "m_ReflectionProbeBlending", p => p.boolValue = true);
        SetProp(so, "m_ShadowDistance", p => p.floatValue = 80f);
        SetProp(so, "m_ShadowCascadeCount", p => p.intValue = 2);
        SetProp(so, "m_Cascade2Split", p => p.floatValue = 0.25f);
        SetProp(so, "m_ShadowDepthBias", p => p.floatValue = 1.0f);
        SetProp(so, "m_ShadowNormalBias", p => p.floatValue = 0.8f);
        SetProp(so, "m_SoftShadowsSupported", p => p.boolValue = true);
        SetProp(so, "m_SoftShadowQuality", p => p.intValue = 2);                   // Medium
        SetProp(so, "m_SupportsLightLayers", p => p.boolValue = true);              // rendering layers (showroom vs island)
        SetProp(so, "m_SupportsLightCookies", p => p.boolValue = false);
        SetProp(so, "m_MixedLightingSupported", p => p.boolValue = false);
        SetProp(so, "m_UseSRPBatcher", p => p.boolValue = true);
        SetProp(so, "m_ColorGradingMode", p => p.intValue = 1);                     // HDR grading
        SetProp(so, "m_ColorGradingLutSize", p => p.intValue = 32);
        SetProp(so, "m_VolumeFrameworkUpdateMode", p => p.intValue = 0);            // every frame
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
        Debug.Log("URPSETUP: URP asset Forward+ HDR MSAA2 shadows 4096/80m 2 cascades soft, rendering layers, SSAO feature lid " + lid);
        return a;
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
        PlayerSettings.productName = "Lambo Blast";
        // lambou6: Linear + HDR (URP tonemapping); the Built-in project was Gamma
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.runInBackground = true;
        PlayerSettings.stripEngineCode = false;
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
        // lambou6: WebGPU first, WebGL 2 as the automatic fallback (Unity 6.6 production WebGPU)
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
        m.SetShaderPassEnabled("ShadowCaster", false);      // lambou6: URP draws shadow casters of every queue
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

    static Texture2D LoadNormal(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) { Debug.LogWarning("LoadNormal: no importer for " + path); return null; }
        if (imp.textureType != TextureImporterType.NormalMap || imp.maxTextureSize != 512 || imp.wrapMode != TextureWrapMode.Repeat)
        {
            imp.textureType = TextureImporterType.NormalMap; imp.mipmapEnabled = true; imp.wrapMode = TextureWrapMode.Repeat; imp.maxTextureSize = 512; imp.anisoLevel = 4;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // lambo7: per-theme skies + ground grain / normal maps in Resources (loaded at runtime by Look / Mats.Ground)
    static void ImportThemeTextures()
    {
        int n = 0;
        for (int t = 1; t <= 5; t++) if (LoadTex("Assets/Resources/LBSky/sky" + t + ".jpg", false, TextureWrapMode.Repeat, TextureWrapMode.Clamp, 2048) != null) n++;
        for (int t = 0; t <= 5; t++)
        {
            Texture2D d = LoadTex("Assets/Resources/LBTex/g" + t + "_d.png", true, TextureWrapMode.Repeat, TextureWrapMode.Repeat, 512);
            var imp = AssetImporter.GetAtPath("Assets/Resources/LBTex/g" + t + "_d.png") as TextureImporter;
            if (imp != null && imp.anisoLevel != 4) { imp.anisoLevel = 4; imp.SaveAndReimport(); }
            if (d != null && LoadNormal("Assets/Resources/LBTex/g" + t + "_n.png") != null) n++;
        }
        LoadNormal("Assets/Resources/LBTex/road_n.png");
        Debug.Log("BuildScript: lambo7 theme textures imported (" + n + " / 11)");
    }

    // lambo8: CC0 ambientCG surfaces in Resources/LB8 (loaded at runtime by Gfx): mipmapped, repeat, trilinear, aniso 8;
    // *_n = normal maps. Up to 1024 px (asphalt), the rest 512.
    static void ImportLB8Textures()
    {
        int n = 0, nn = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/LB8" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) continue;
            bool nrm = path.EndsWith("_n.png");
            bool ch = false;
            if (nrm && imp.textureType != TextureImporterType.NormalMap) { imp.textureType = TextureImporterType.NormalMap; ch = true; }
            if (!imp.mipmapEnabled) { imp.mipmapEnabled = true; ch = true; }
            if (imp.wrapMode != TextureWrapMode.Repeat) { imp.wrapMode = TextureWrapMode.Repeat; ch = true; }
            if (imp.filterMode != FilterMode.Trilinear) { imp.filterMode = FilterMode.Trilinear; ch = true; }
            if (imp.anisoLevel != 8) { imp.anisoLevel = 8; ch = true; }
            if (imp.maxTextureSize != 1024) { imp.maxTextureSize = 1024; ch = true; }
            if (ch) imp.SaveAndReimport();
            n++; if (nrm) nn++;
        }
        Debug.Log("BuildScript: lambo8 LB8 textures imported (" + n + ", " + nn + " normal maps); shaders LB/Tri " + (Shader.Find("LB/Tri") != null) +
            " LB/Terrain " + (Shader.Find("LB/Terrain") != null) + " LB/Road " + (Shader.Find("LB/Road") != null) + " LB/Grass " + (Shader.Find("LB/Grass") != null));
    }

    static Material MakeDetailMat(string file, string detailPath, Vector2 tiling, float gloss)
    {
        Material m = MakeMat("LB/Std", "Universal Render Pipeline/Lit", file);
        m.SetFloat("_Glossiness", gloss);
        Texture2D d = LoadTex(detailPath, true, TextureWrapMode.Repeat, TextureWrapMode.Repeat, 512);
        if (d != null)
        {
            m.SetTexture("_DetailAlbedoMap", d);
            m.SetTextureScale("_DetailAlbedoMap", tiling);
            m.SetFloat("_UVSec", 0f);
            m.EnableKeyword("_DETAIL_MULX2");
        }
        else Debug.LogWarning("MakeDetailMat: missing " + detailPath);
        EditorUtility.SetDirty(m);
        return m;
    }

    // lambou6: URP post profiles (sub-asset components so they ship); U6.Grade rewrites the race values per track at runtime
    static VolumeProfile MakeProfile(string file, bool showroom)
    {
        var p = SaveAsset(ScriptableObject.CreateInstance<VolumeProfile>(), file);
        var tm = p.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.ACES);
        var ca = p.Add<ColorAdjustments>(true);
        ca.postExposure.Override(showroom ? 0.45f : 0.45f);
        ca.contrast.Override(showroom ? 6f : 12f);
        ca.saturation.Override(showroom ? -6f : 14f);
        var bl = p.Add<Bloom>(true); bl.intensity.Override(showroom ? 0.35f : 0.3f); bl.threshold.Override(1f); bl.scatter.Override(0.62f);
        var vg = p.Add<Vignette>(true); vg.intensity.Override(showroom ? 0.32f : 0.26f); vg.smoothness.Override(0.45f);
        if (!showroom) { var wb = p.Add<WhiteBalance>(true); wb.temperature.Override(6f); }
        foreach (var c in p.components) { c.name = c.GetType().Name; AssetDatabase.AddObjectToAsset(c, p); }
        EditorUtility.SetDirty(p);
        AssetDatabase.SaveAssets();
        return p;
    }

    // u6d: the god rays feature (sub-asset of the renderer, made in pass 1)
    static ScriptableRendererFeature FindShafts()
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(UrpRendererPath))
            if (o is FullScreenPassRendererFeature) return (ScriptableRendererFeature)o;
        Debug.LogWarning("BuildScript: u6d shafts feature not found in " + UrpRendererPath);
        return null;
    }

    // u6d: scanned CC0 PBR sets in Resources/U6T (ambientCG; lambou6/u6d/prep_tex6.py): <name>_c.jpg albedo (sRGB, DXT1),
    // <name>_d.png packed data (linear: r roughness, g normal Y, b AO, a normal X; DXT5). Mipmapped, repeat, trilinear, aniso 8.
    static void ImportU6DTextures()
    {
        int nc = 0, nd = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/U6T" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) continue;
            bool data = path.EndsWith("_d.png");
            if (imp.userData == "u6d1") { if (data) nd++; else nc++; continue; }   // already imported with these settings (Library cache)
            imp.userData = "u6d1";
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = !data;
            imp.alphaSource = data ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            imp.mipmapEnabled = true;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.filterMode = FilterMode.Trilinear;
            imp.anisoLevel = 8;
            imp.maxTextureSize = 1024;
            imp.textureCompression = TextureImporterCompression.Compressed;
            var ps = imp.GetPlatformTextureSettings("WebGL");
            ps.overridden = true;
            ps.maxTextureSize = 1024;
            ps.format = data ? TextureImporterFormat.DXT5 : TextureImporterFormat.DXT1;
            imp.SetPlatformTextureSettings(ps);
            imp.SaveAndReimport();
            if (data) nd++; else nc++;
        }
        Debug.Log("BuildScript: u6d U6T scans imported (" + nc + " albedo DXT1, " + nd + " data DXT5); shaders LB/GrassI " + (Shader.Find("LB/GrassI") != null) +
            " LB/Shafts " + (Shader.Find("LB/Shafts") != null));
    }

    static ScriptableRendererFeature FindSsao()
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(UrpRendererPath))
            if (o is ScreenSpaceAmbientOcclusion) return (ScriptableRendererFeature)o;
        Debug.LogWarning("BuildScript: SSAO feature not found in " + UrpRendererPath);
        return null;
    }

    static string CreateScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Material lit = MakeMat("LB/Std", "Universal Render Pipeline/Lit", "Lit.mat");
        lit.SetFloat("_Glossiness", 0.15f);
        Material unlit = MakeMat("LB/Unlit", "Universal Render Pipeline/Unlit", "Unlit.mat");
        Material fx = MakeMat("Sprites/Default", "Unlit/Color", "Fx.mat");
        Material water = MakeMat("LB/Std", "Universal Render Pipeline/Lit", "Water.mat");
        water.color = new Color(0.1f, 0.62f, 0.72f, 0.72f);
        MakeTransparent(water, false);
        water.SetFloat("_Glossiness", 0.92f);
        Material glass = MakeMat("LB/Std", "Universal Render Pipeline/Lit", "Glass.mat");
        glass.color = new Color(0.55f, 0.7f, 0.8f, 0.35f);
        MakeTransparent(glass, true);
        glass.SetFloat("_Glossiness", 0.95f);
        glass.SetFloat("_Metallic", 0.2f);
        EditorUtility.SetDirty(water);
        EditorUtility.SetDirty(glass);
        // sky: Poly Haven CC0 HDRI (Kloofendal 48d partly cloudy, pure sky, tonemapped 2k) on Skybox/Panoramic;
        // procedural sky if the texture / shader is missing
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
            sky.SetFloat("_Exposure", 1.12f);
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
            sky.SetColor("_SkyTint", new Color(0.4f, 0.62f, 0.9f));
            sky.SetColor("_GroundColor", new Color(0.35f, 0.6f, 0.68f));
            sky.SetFloat("_Exposure", 1.3f);
        }
        EditorUtility.SetDirty(sky);

        // ground + road: Standard with a Poly Haven CC0 sand "detail albedo x2" map (keyword variant ships with these assets)
        // lambo7: ground base also has _NORMALMAP (theme grain + normal map tiled on uv0, colour map as the x2 detail on uv1)
        ImportThemeTextures();
        ImportLB8Textures();
        ImportU6DTextures();
        Material ground = MakeDetailMat("Ground.mat", "Assets/Textures/sand_detail.png", new Vector2(1f, 1f), 0.06f);
        Texture2D g1d = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/LBTex/g1_d.png");
        Texture2D g1n = LoadNormal("Assets/Resources/LBTex/g1_n.png");
        if (g1d != null && g1n != null)
        {
            ground.mainTexture = g1d;
            ground.SetTexture("_BumpMap", g1n);
            ground.EnableKeyword("_NORMALMAP");
            ground.SetFloat("_UVSec", 1f);
            EditorUtility.SetDirty(ground);
            Debug.Log("BuildScript: lambo7 ground base (normal map + detail on uv1)");
        }
        else Debug.LogWarning("BuildScript: lambo7 ground textures missing - old ground");
        bool asph = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/LBTex/road_d.png") != null;
        Material road = MakeDetailMat("Road.mat", asph ? "Assets/Resources/LBTex/road_d.png" : "Assets/Textures/road_detail.png", new Vector2(2f, 2f), 0.12f);
        // lambo7: additive particles (sparks, flames, embers, boost) - editor-made so the shader ships
        Material fxAdd = MakeMat("LB/FxAdd", "Sprites/Default", "FxAdd.mat");

        // sea: custom animated shader (Assets/Shaders/LBWater.shader)
        Shader ws = Shader.Find("LB/Water");
        if (ws != null)
        {
            water = SaveAsset(new Material(ws), "Water.mat");
            Debug.Log("BuildScript: LB/Water shader");
        }
        else Debug.LogWarning("BuildScript: LB/Water shader missing - Standard water");
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
        RenderSettings.fogColor = new Color(0.66f, 0.74f, 0.84f);
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
        sun.shadowNormalBias = 0.4f;     // lambo8b: back to the lambo7 values (0.04 / 0.35 showed acne dots in the cockpits on phones)
        sunGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        RenderSettings.sun = sun;

        var boot = new GameObject("Bootstrap");
        Bootstrap b = boot.AddComponent<Bootstrap>();
        b.litMat = lit;
        b.unlitMat = unlit;
        b.fxMat = fx;
        b.waterMat = water;
        b.glassMat = glass;
        b.groundMat = ground;
        b.roadMat = road;
        b.fxAddMat = fxAdd;
        b.sun = sun;
        b.raceProfile = MakeProfile("RaceVolume.asset", false);
        b.showroomProfile = MakeProfile("ShowroomVolume.asset", true);
        b.ssaoFeature = FindSsao();
        b.shaftsFeature = FindShafts();
        b.shaftsMat = AssetDatabase.LoadAssetAtPath<Material>(ShaftsMatPath);
        Debug.Log("BuildScript: u6d shafts feature " + (b.shaftsFeature != null) + " material " + (b.shaftsMat != null));
        Debug.Log("BuildScript: lambou6 volumes race " + (b.raceProfile != null) + " showroom " + (b.showroomProfile != null) + " ssao " + (b.ssaoFeature != null) +
            " shaders LB/Std " + (Shader.Find("LB/Std") != null) + " LB/Unlit " + (Shader.Find("LB/Unlit") != null) + " LB/FxAdd " + (Shader.Find("LB/FxAdd") != null) +
            " LB/Paint " + (Shader.Find("LB/Paint") != null) + " LB/Blob " + (Shader.Find("LB/Blob") != null) + " LB/Far " + (Shader.Find("LB/Far") != null));

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new Exception("Failed to save scene");
        AssetDatabase.SaveAssets();
        Debug.Log("BuildScript: scene saved " + ScenePath);
        return ScenePath;
    }
}
