using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// bb-u6-uwp (Oct 23, 2026): Universal Windows Platform (UWP) build of BALLOON BLAST 6.6 (unity/balloon-blast on branch bb-unity6,
// Unity 6000.6.5f1 + URP 17.6) for an Xbox One S in Dev Mode (sideload through Device Portal) - the Balloon Blast twin of
// FFBuildUWP (unity/four-froggies-u6-uwp, ffu73b, confirmed on Bill's One S) / BBBuildUWP (unity/lambo-blast-u6-uwp).
// This file lives on unity-src in unity/balloon-blast-u6-uwp/ and is NOT part of the game: .github/workflows/unity-balloon-blast-u6-uwp.yml
// checks out bb-unity6 and copies unity/balloon-blast-u6-uwp/Assets/* into that project on the runner, so the 6.6 WebGL build
// (unity-balloon-blast-preview.yml on bb-unity6) never sees it.
// ONE editor start (like the 6.6 WebGL build: BuildScript.CreateScene runs its own SetupURP, which (re)creates Generated/URP.asset);
// the console URP tweaks are applied to that asset AFTER CreateScene: Direct3D 11 only, Forward, MSAA 1, HDR off, 1024 shadow map,
// one cascade. Renderer features LEFT AS THE WEBGL BUILD HAS THEM (ffu73b fix a2b7f202: an inactive SSAO feature lets the build strip
// URP's SSAO resources -> NullRef in ScriptableRenderer..ctor + "Blitter is already initialized" every frame).
// Identity: SimilarizeBalloonBlast66 / "Balloon Blast 6.6" (installs next to the 2022.3 SimilarizeBalloonBlast app).
//   Unity -batchmode -buildTarget WSAPlayer -executeMethod BBBuildUWP.Build
// Env: UWP_PFX (project-relative .pfx path), UWP_PFX_PASS, UWP_TILES (folder with the tile PNGs), GITHUB_RUN_NUMBER.
public static class BBBuildUWP
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
            if (p == null || !p.CanWrite) { Debug.LogWarning("BBUWP: EditorUserBuildSettings." + name + " not found"); return; }
            object v = p.PropertyType.IsEnum ? Enum.Parse(p.PropertyType, value) : Convert.ChangeType(value, p.PropertyType);
            p.SetValue(null, v, null);
            Debug.Log("BBUWP: EditorUserBuildSettings." + name + " = " + p.GetValue(null, null));
        }
        catch (Exception e) { Debug.LogWarning("BBUWP: set " + name + " = " + value + " failed: " + e.Message); }
    }

    static void Tile(PlayerSettings.WSAImageType type, string file)
    {
        try
        {
            string dir = Environment.GetEnvironmentVariable("UWP_TILES");
            if (string.IsNullOrEmpty(dir)) return;
            string path = Path.Combine(dir, file);
            if (!File.Exists(path)) { Debug.LogWarning("BBUWP tile missing " + path); return; }
            PlayerSettings.WSA.SetVisualAssetsImage(path, type, PlayerSettings.WSAImageScale._100);
            Debug.Log("BBUWP tile " + type + " <- " + file);
        }
        catch (Exception e) { Debug.LogWarning("BBUWP tile " + type + ": " + e.Message); }
    }

    // ffu67x: audio project settings for the console - Unity audio on, 48 kHz (the Xbox mix rate), stereo, 1024-sample
    // DSP buffer (the runtime BBXboxAudio re-opens the output with the same values, and can try others from its panel)
    static void AudioProject()
    {
        try
        {
            UnityEngine.Object[] objs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset");
            if (objs == null || objs.Length == 0) { Debug.LogWarning("BBUWP audio: AudioManager.asset not found"); return; }
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
            Debug.Log("BBUWP audio:" + log);
        }
        catch (Exception e) { Debug.LogWarning("BBUWP audio settings: " + e.Message); }
    }

    // the URP asset + its renderer, tuned for the One S (D3D11 FL10-class GPU): Forward path, no MSAA / HDR; features untouched
    static void UrpConsole()
    {
        var a = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
        if (a == null) { Debug.LogWarning("BBUWP urp: no default pipeline"); return; }
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
                        var act = new SerializedObject(fo).FindProperty("m_Active");
                        // left exactly as the WebGL build (see header): SSAO active
                        log += " feature " + fo.name + " (" + fo.GetType().Name + ") active=" + (act != null && act.boolValue) + " kept";
                    }
                EditorUtility.SetDirty(rd);
            }
        AssetDatabase.SaveAssets();
        Debug.Log("BBUWP urp: " + log);
    }

    static int Run()
    {
        Debug.Log("BuildScript: Balloon Blast 6.6 UWP start, Unity " + Application.unityVersion);
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WSAPlayer)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WSA, BuildTarget.WSAPlayer);

        BuildScript.SetInputHandler();
        Call("ConfigurePlayer");
        Call("EnsureFolder", "Assets", "Generated");
        Call("EnsureFolder", "Assets", "Scenes");
        AudioProject();

        // ---- UWP player settings ----
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.WindowsStoreApps, ScriptingImplementation.IL2CPP);   // UWP is IL2CPP only
        PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WindowsStoreApps, ManagedStrippingLevel.Low);
        PlayerSettings.SetIl2CppCompilerConfiguration(UnityEditor.Build.NamedBuildTarget.WindowsStoreApps, Il2CppCompilerConfiguration.Release);
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WSAPlayer, false);                     // Direct3D 11 only (One S)
        PlayerSettings.SetGraphicsAPIs(BuildTarget.WSAPlayer, new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });
        PlayerSettings.stripEngineCode = false;   // ConfigurePlayer set it for WebGL; UWP keeps the default (as FF 6.6 UWP)
        Debug.Log("BBUWP: graphics APIs " + string.Join(",", PlayerSettings.GetGraphicsAPIs(BuildTarget.WSAPlayer)) + " colour " + PlayerSettings.colorSpace);
        PlayerSettings.runInBackground = false;
        PlayerSettings.WSA.packageName = "SimilarizeBalloonBlast66";   // distinct identity: installs next to the 2022.3 Balloon Blast app
        PlayerSettings.productName = "Balloon Blast 6.6";
        string run = Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER");
        int rn; if (!int.TryParse(run, out rn)) rn = 1;
        PlayerSettings.WSA.packageVersion = new Version(1, 0, rn, 0);
        PlayerSettings.bundleVersion = "1.0." + rn;
        PlayerSettings.WSA.applicationDescription = "Balloon Blast 6.6 (Unity 6) - balloon airsoft battles with Xbox controllers (one controller = one player, split screen).";
        PlayerSettings.WSA.tileShortName = "Balloon Blast 6.6";

        // capabilities: internetClient only (online play stays web-only; the manifest allows network)
        foreach (PlayerSettings.WSACapability c in Enum.GetValues(typeof(PlayerSettings.WSACapability)))
            PlayerSettings.WSA.SetCapability(c, c == PlayerSettings.WSACapability.InternetClient);
        Debug.Log("BBUWP: capabilities internetClient=" + PlayerSettings.WSA.GetCapability(PlayerSettings.WSACapability.InternetClient)
                  + " microphone=" + PlayerSettings.WSA.GetCapability(PlayerSettings.WSACapability.Microphone));

        // certificate made by the workflow (New-SelfSignedCertificate CN=Similarize); its subject becomes the manifest Publisher
        string pfx = Environment.GetEnvironmentVariable("UWP_PFX");
        string pass = Environment.GetEnvironmentVariable("UWP_PFX_PASS") ?? "";
        if (!string.IsNullOrEmpty(pfx))
        {
            bool ok = PlayerSettings.WSA.SetCertificate(pfx, pass);
            Debug.Log("BBUWP: certificate " + pfx + " set=" + ok + " subject=" + PlayerSettings.WSA.certificateSubject
                      + " issuer=" + PlayerSettings.WSA.certificateIssuer);
            if (!ok) { Debug.LogError("BBUWP: SetCertificate failed"); return 3; }
        }
        else Debug.LogWarning("BBUWP: no UWP_PFX - package will need a certificate at MSBuild time");

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

        string scene = (string)Call("CreateScene");   // runs BuildScript.SetupURP (new URP asset) + builds the scene
        UrpConsole();
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
        Debug.Log("BUILD_RESULT: " + s.result + " target=UWP-BB-6.6 errors=" + s.totalErrors + " warnings=" + s.totalWarnings + " size=" + s.totalSize + " time=" + s.totalTime);
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
