using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// lbuwp (Oct 20, 2026): Universal Windows Platform (UWP) build of Balloon Blast (unity/balloon-blast, Unity 2022.3) for an Xbox One S
// in Dev Mode (sideload through Device Portal) - the same build as Four Froggies' FFBuildUWP (unity/four-froggies-uwp).
// This file is NOT part of unity/balloon-blast: .github/workflows/unity-balloon-blast-uwp.yml copies unity/balloon-blast-uwp/Assets/* into the
// project on the runner, so the WebGL build never sees it and a change here never starts the WebGL build.
//   Unity -batchmode -buildTarget WSAPlayer -executeMethod BBBuildUWP.Build
// Reuses BuildScript's scene generation + player setup (private, so through reflection) and then sets the UWP specifics:
// D3D (CoreWindow) project, any device (Windows.Universal family = Xbox too), x64, IL2CPP, internetClient only, the
// workflow's self-signed test certificate (publisher CN=Similarize), tiles, audio project settings for the console.
// Output: build/UWP/<Visual Studio solution> for MSBuild (Master|x64, AppxBundle) in the next workflow step.
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

    // audio project settings for the console - Unity audio on, 48 kHz (the Xbox mix rate), stereo, 1024-sample
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

    static int Run()
    {
        Debug.Log("BuildScript: UWP start, Unity " + Application.unityVersion);
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WSAPlayer)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WSA, BuildTarget.WSAPlayer);

        BuildScript.SetInputHandler();
        Call("ConfigurePlayer");
        Call("EnsureFolder", "Assets", "Generated");
        Call("EnsureFolder", "Assets", "Scenes");
        AudioProject();

        // ---- UWP player settings ----
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.WSA, ScriptingImplementation.IL2CPP);   // UWP is IL2CPP only
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WSA, ManagedStrippingLevel.Low);
        PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.WSA, Il2CppCompilerConfiguration.Release);
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WSAPlayer, true);                      // Direct3D 11
        PlayerSettings.runInBackground = false;
        PlayerSettings.WSA.packageName = "SimilarizeBalloonBlast";
        string run = Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER");
        int rn; if (!int.TryParse(run, out rn)) rn = 1;
        PlayerSettings.WSA.packageVersion = new Version(1, 0, rn, 0);
        PlayerSettings.bundleVersion = "1.0." + rn;
        PlayerSettings.WSA.applicationDescription = "Balloon Blast Airsoft - local split-screen balloon popping with Xbox controllers.";
        PlayerSettings.WSA.tileShortName = "Balloon Blast";

        // capabilities: internetClient only (online play stays web-only today; the game fetches its soldier model
        // from assets.babylonjs.com), no microphone
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
