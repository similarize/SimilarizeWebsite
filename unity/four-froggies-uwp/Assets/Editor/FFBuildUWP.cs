using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// ffuwp (Oct 20, 2026): Universal Windows Platform (UWP) build of Four Froggies for an Xbox One S in Dev Mode (sideload
// through Device Portal). This file is NOT part of unity/four-froggies: the UWP workflow
// (.github/workflows/unity-four-froggies-uwp.yml) copies unity/four-froggies-uwp/Assets/* into the project on the runner,
// so the WebGL / Windows / Android builds never see it and a change here never starts the WebGL build.
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

    static int Run()
    {
        Debug.Log("BuildScript: UWP start, Unity " + Application.unityVersion);
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WSAPlayer)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WSA, BuildTarget.WSAPlayer);

        BuildScript.SetInputHandler();
        Call("ConfigurePlayer");
        Call("ApplyIcons", false);
        Call("EnsureFolder", "Assets", "Generated");
        Call("EnsureFolder", "Assets", "Scenes");
        Call("ImportNativeMusic");
        AudioProject();

        // ---- UWP player settings ----
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.WSA, ScriptingImplementation.IL2CPP);   // UWP is IL2CPP only
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WSA, ManagedStrippingLevel.Low);
        PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.WSA, Il2CppCompilerConfiguration.Release);
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WSAPlayer, true);                      // Direct3D 11
        PlayerSettings.runInBackground = false;
        PlayerSettings.WSA.packageName = "SimilarizeFourFroggies";
        string run = Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER");
        int rn; if (!int.TryParse(run, out rn)) rn = 1;
        PlayerSettings.WSA.packageVersion = new Version(1, 0, rn, 0);
        PlayerSettings.bundleVersion = "1.0." + rn;
        PlayerSettings.WSA.applicationDescription = "Four Froggies - local couch play with Xbox controllers (one controller = one froggy).";
        PlayerSettings.WSA.tileShortName = "Four Froggies";

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
        Debug.Log("BUILD_RESULT: " + s.result + " target=UWP errors=" + s.totalErrors + " warnings=" + s.totalWarnings + " size=" + s.totalSize + " time=" + s.totalTime);
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
