using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// ffu73 (Oct 22, 2026): Universal Windows Platform (UWP) build of FOUR FROGGIES 6.6 (Unity 6000.6.5f1 + URP 17.6) for an Xbox
// One S in Dev Mode (sideload through Device Portal), adapted from the 2022.3 overlay (unity/four-froggies-uwp, ffuwp).
// This file is NOT part of unity/four-froggies-u6: .github/workflows/unity-four-froggies-u6-uwp.yml copies
// unity/four-froggies-u6-uwp/Assets/* into the project on the runner, so the 6.6 WebGL build never sees it.
// Two editor starts like the WebGL build: BuildScript.SetupURP (URP asset as the default pipeline), then this.
// Console URP: Direct3D 11 only, Forward (not Forward+), MSAA 1, HDR off, SSAO / renderer features off, 1024 shadow map.
// Identity: SimilarizeFourFroggies66 / "Four Froggies 6.6" (installs next to the 2022.3 SimilarizeFourFroggies app).
//   Unity -batchmode -buildTarget WSAPlayer -executeMethod FFBuildUWP.Build
// Reuses BuildScript's scene generation + player setup (private, so through reflection) and then sets the UWP specifics:
// D3D (CoreWindow) project, any device (Windows.Universal family = Xbox too), x64, IL2CPP, internetClient only (voice is a
// no-op off WebGL, so no microphone), the workflow's self-signed test certificate (publisher CN=Similarize), froggy tiles.
// Output: build/UWP/<Visual Studio solution> for MSBuild (Master|x64, AppxBundle) in the next workflow step.
// Env: FF_UWP_PFX (project-relative .pfx path), FF_UWP_PFX_PASS, FF_UWP_TILES (folder with the tile PNGs), GITHUB_RUN_NUMBER.
public static class FFBuildUWP
{
    public static void Build()
    {
        int code = 1;
        try { code = Run(); }
        catch (Exception e) { Debug.LogError("BUILD_SCRIPT_EXCEPTION: " + e); code = 2; }
        Debug.Log("BUILD_SCRIPT_EXIT: " + code);
        EditorApplication.Exit(code);
    }

    static object Call(string name, params object[] args)
    {
        MethodInfo m = typeof(BuildScript).GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (m == null) throw new Exception("BuildScript." + name + " not found");
        return m.Invoke(null, args);
    }

    // EditorUserBuildSettings.wsa* by name (enum values parsed by name) so a renamed property only logs a warning
    static void SetEub(string name, string value)
    {
        try
        {
            PropertyInfo p = typeof(EditorUserBuildSettings).GetProperty(name, BindingFlags.Public | BindingFlags.Static);
            if (p == null || !p.CanWrite) { Debug.LogWarning("FFUWP: EditorUserBuildSettings." + name + " not found"); return; }
            object v = p.PropertyType.IsEnum ? Enum.Parse(p.PropertyType, value) : Convert.ChangeType(value, p.PropertyType);
            p.SetValue(null, v, null);
            Debug.Log("FFUWP: EditorUserBuildSettings." + name + " = " + p.GetValue(null, null));
        }
        catch (Exception e) { Debug.LogWarning("FFUWP: set " + name + " = " + value + " failed: " + e.Message); }
    }

    static void Tile(PlayerSettings.WSAImageType type, string file)
    {
        try
        {
            string dir = Environment.GetEnvironmentVariable("FF_UWP_TILES");
            if (string.IsNullOrEmpty(dir)) return;
            string path = Path.Combine(dir, file);
            if (!File.Exists(path)) { Debug.LogWarning("FFUWP tile missing " + path); return; }
            PlayerSettings.WSA.SetVisualAssetsImage(path, type, PlayerSettings.WSAImageScale._100);
            Debug.Log("FFUWP tile " + type + " <- " + file);
        }
        catch (Exception e) { Debug.LogWarning("FFUWP tile " + type + ": " + e.Message); }
    }

    // ffu67x: audio project settings for the console - Unity audio on, 48 kHz (the Xbox mix rate), stereo, 1024-sample
    // DSP buffer (the runtime FFXboxAudio re-opens the output with the same values, and can try others from its panel)
    static void AudioProject()
    {
        try
        {
            UnityEngine.Object[] objs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset");
            if (objs == null || objs.Length == 0) { Debug.LogWarning("FFUWP audio: AudioManager.asset not found"); return; }
            var so = new SerializedObject(objs[0]);
            string log = "";
            SerializedProperty p;
            p = so.FindProperty("m_DisableAudio"); if (p != null) { log += " disableAudio " + p.boolValue + "->false"; p.boolValue = false; }
            p = so.FindProperty("m_SampleRate"); if (p != null) { log += " sampleRate " + p.intValue + "->48000"; p.intValue = 48000; }
            p = so.FindProperty("m_DSPBufferSize"); if (p != null) { log += " dsp " + p.intValue + "->1024"; p.intValue = 1024; }
            p = so.FindProperty("m_RequestedDSPBufferSize"); if (p != null) { log += " requestedDsp " + p.intValue + "->1024"; p.intValue = 1024; }
            p = so.FindProperty("Default Speaker Mode"); if (p != null) { log += " speakerMode " + p.intValue + "->2"; p.intValue = 2; }
            p = so.FindProperty("m_Volume"); if (p != null) { log += " volume " + p.floatValue; if (p.floatValue <= 0.01f) p.floatValue = 1f; }
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("FFUWP audio:" + log);
        }
        catch (Exception e) { Debug.LogWarning("FFUWP audio settings: " + e.Message); }
    }

    // ffu67x: on the console the music clips are kept compressed in memory (~11 MB of Vorbis) and preloaded, instead of
    // being streamed from the package file at play time (one stream per track change - the music was intermittent)
    static void MusicInMemory()
    {
        const string dir = "Assets/Resources/FFMusic";
        if (!AssetDatabase.IsValidFolder(dir)) return;
        int n = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { dir }))
        {
            var imp = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as AudioImporter;
            if (imp == null) continue;
            AudioImporterSampleSettings st = imp.defaultSampleSettings;
            st.loadType = AudioClipLoadType.CompressedInMemory;
            st.compressionFormat = AudioCompressionFormat.Vorbis;
            st.quality = 0.6f;
            st.preloadAudioData = true;
            imp.defaultSampleSettings = st;
            imp.loadInBackground = false;
            imp.SaveAndReimport();
            n++;
        }
        Debug.Log("FFUWP audio: music clips " + n + " compressed in memory + preloaded (not streamed)");
    }

    // the URP asset + its renderer, tuned for the One S (D3D11 FL10-class GPU): Forward path, no MSAA / HDR, no SSAO feature
    static void UrpConsole()
    {
        var a = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
        if (a == null) { Debug.LogWarning("FFUWP urp: no default pipeline"); return; }
        string log = "asset " + AssetDatabase.GetAssetPath(a);
        var so = new SerializedObject(a);
        SerializedProperty p;
        p = so.FindProperty("m_MSAA"); if (p != null) { log += " msaa " + p.intValue + "->1"; p.intValue = 1; }
        p = so.FindProperty("m_SupportsHDR"); if (p != null) { log += " hdr " + p.boolValue + "->false"; p.boolValue = false; }
        p = so.FindProperty("m_MainLightShadowmapResolution"); if (p != null) { log += " shadowmap " + p.intValue + "->1024"; p.intValue = 1024; }
        p = so.FindProperty("m_ShadowCascadeCount"); if (p != null) { log += " cascades " + p.intValue + "->1"; p.intValue = 1; }
        p = so.FindProperty("m_ShadowDistance"); if (p != null) { log += " shadowDist " + p.floatValue + "->30"; p.floatValue = Mathf.Min(p.floatValue, 30f); }
        p = so.FindProperty("m_SoftShadowsSupported"); if (p != null) { log += " soft " + p.boolValue + "->false"; p.boolValue = false; }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(a);
        p = new SerializedObject(a).FindProperty("m_RendererDataList");
        if (p != null && p.isArray)
            for (int i = 0; i < p.arraySize; i++)
            {
                var rd = p.GetArrayElementAtIndex(i).objectReferenceValue;
                if (rd == null) continue;
                var rso = new SerializedObject(rd);
                var rm = rso.FindProperty("m_RenderingMode"); if (rm != null) { log += " | renderer " + rd.name + " mode " + rm.intValue + "->0 (Forward)"; rm.intValue = 0; }
                rso.ApplyModifiedPropertiesWithoutUndo();
                var fl = rso.FindProperty("m_RendererFeatures");
                if (fl != null && fl.isArray)
                    for (int j = 0; j < fl.arraySize; j++)
                    {
                        var fo = fl.GetArrayElementAtIndex(j).objectReferenceValue;
                        if (fo == null) continue;
                        // fix 1 (run 39825512350): an inactive SSAO feature makes the build strip URP's SSAO persistent resources ->
                        // player: "Couldn't find the required resources for the ScreenSpaceAmbientOcclusion render feature", NullRef in
                        // ScriptableRenderer..ctor, then "Blitter is already initialized" every frame. Keep SSAO active in the asset;
                        // U6.SsaoOn turns it off at runtime (Perf60.AO is false on the low tier the console boots in).
                        if (fo.GetType().Name.Contains("ScreenSpaceAmbientOcclusion")) { log += " feature " + fo.name + " kept active (runtime off)"; continue; }
                        var fso = new SerializedObject(fo); var act = fso.FindProperty("m_Active");
                        if (act != null && act.boolValue) { act.boolValue = false; fso.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(fo); log += " feature " + fo.name + " off"; }
                    }
                EditorUtility.SetDirty(rd);
            }
        AssetDatabase.SaveAssets();
        Debug.Log("FFUWP urp: " + log);
    }

    static int Run()
    {
        Debug.Log("BuildScript: UWP 6.6 start, Unity " + Application.unityVersion);
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WSAPlayer)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WSA, BuildTarget.WSAPlayer);

        BuildScript.SetInputHandler();
        Call("ConfigurePlayer");
        Call("EnsureFolder", "Assets", "Generated");
        Call("EnsureFolder", "Assets", "Scenes");
        if (!(UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset))
        {
            Debug.LogWarning("FFUWP: no URP asset active (setup pass missing?) - making it now");
            Call("MakeURP");
        }
        UrpConsole();
        try { Call("DefineRenderingLayers"); } catch (Exception e) { Debug.LogWarning("FFUWP rendering layers: " + e.Message); }
        AudioProject();
        MusicInMemory();

        // ---- UWP player settings ----
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.WindowsStoreApps, ScriptingImplementation.IL2CPP);   // UWP is IL2CPP only
        PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WindowsStoreApps, ManagedStrippingLevel.Low);
        PlayerSettings.SetIl2CppCompilerConfiguration(UnityEditor.Build.NamedBuildTarget.WindowsStoreApps, Il2CppCompilerConfiguration.Release);
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WSAPlayer, false);                     // Direct3D 11 only (One S)
        PlayerSettings.SetGraphicsAPIs(BuildTarget.WSAPlayer, new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });
        PlayerSettings.stripEngineCode = false;   // ConfigurePlayer set it for WebGL; UWP keeps the default (reflection-heavy code)
        Debug.Log("FFUWP: graphics APIs " + string.Join(",", PlayerSettings.GetGraphicsAPIs(BuildTarget.WSAPlayer)) + " colour " + PlayerSettings.colorSpace);
        PlayerSettings.runInBackground = false;
        PlayerSettings.WSA.packageName = "SimilarizeFourFroggies66";   // distinct identity: installs next to the 2022.3 app
        PlayerSettings.productName = "Four Froggies 6.6";
        string run = Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER");
        int rn; if (!int.TryParse(run, out rn)) rn = 1;
        PlayerSettings.WSA.packageVersion = new Version(1, 0, rn, 0);
        PlayerSettings.bundleVersion = "1.0." + rn;
        PlayerSettings.WSA.applicationDescription = "Four Froggies 6.6 (Unity 6) - local couch play with Xbox controllers (one controller = one froggy).";
        PlayerSettings.WSA.tileShortName = "Four Froggies 6.6";

        // capabilities: internetClient only (online rooms stay web-only today, but the manifest allows network); no
        // microphone - RobotVoice66 / FFVoice is a no-op outside WebGL
        foreach (PlayerSettings.WSACapability c in Enum.GetValues(typeof(PlayerSettings.WSACapability)))
            PlayerSettings.WSA.SetCapability(c, c == PlayerSettings.WSACapability.InternetClient);
        Debug.Log("FFUWP: capabilities internetClient=" + PlayerSettings.WSA.GetCapability(PlayerSettings.WSACapability.InternetClient)
                  + " microphone=" + PlayerSettings.WSA.GetCapability(PlayerSettings.WSACapability.Microphone));

        // certificate made by the workflow (New-SelfSignedCertificate CN=Similarize); its subject becomes the manifest Publisher
        string pfx = Environment.GetEnvironmentVariable("FF_UWP_PFX");
        string pass = Environment.GetEnvironmentVariable("FF_UWP_PFX_PASS") ?? "";
        if (!string.IsNullOrEmpty(pfx))
        {
            bool ok = PlayerSettings.WSA.SetCertificate(pfx, pass);
            Debug.Log("FFUWP: certificate " + pfx + " set=" + ok + " subject=" + PlayerSettings.WSA.certificateSubject
                      + " issuer=" + PlayerSettings.WSA.certificateIssuer);
            if (!ok) { Debug.LogError("FFUWP: SetCertificate failed"); return 3; }
        }
        else Debug.LogWarning("FFUWP: no FF_UWP_PFX - package will need a certificate at MSBuild time");

        Tile(PlayerSettings.WSAImageType.PackageLogo, "sq50.png");
        Tile(PlayerSettings.WSAImageType.SplashScreenImage, "splash620x300.png");
        Tile(PlayerSettings.WSAImageType.UWPSquare44x44Logo, "sq44.png");
        Tile(PlayerSettings.WSAImageType.UWPSquare71x71Logo, "sq71.png");
        Tile(PlayerSettings.WSAImageType.UWPSquare150x150Logo, "sq150.png");
        Tile(PlayerSettings.WSAImageType.UWPSquare310x310Logo, "sq310.png");
        Tile(PlayerSettings.WSAImageType.UWPWide310x150Logo, "wide310x150.png");

        // ---- UWP build settings: D3D project, any device (Universal family includes Xbox), x64 ----
        SetEub("wsaUWPBuildType", "D3D");
        SetEub("wsaSubtarget", "AnyDevice");
        SetEub("wsaArchitecture", "x64");
        SetEub("wsaBuildAndRunDeployTarget", "LocalMachine");

        string scene = (string)Call("CreateScene");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scene, true) };
        AssetDatabase.SaveAssets();

        var opts = new BuildPlayerOptions
        {
            scenes = new[] { scene },
            locationPathName = "build/UWP",
            target = BuildTarget.WSAPlayer,
            targetGroup = BuildTargetGroup.WSA,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(opts);
        BuildSummary s = report.summary;
        Debug.Log("BUILD_RESULT: " + s.result + " target=UWP-6.6 errors=" + s.totalErrors + " warnings=" + s.totalWarnings + " size=" + s.totalSize + " time=" + s.totalTime);
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
}
