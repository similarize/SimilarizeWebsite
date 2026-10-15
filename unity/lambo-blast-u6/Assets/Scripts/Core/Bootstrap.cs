using UnityEngine;

// Lives in the generated Main scene. Builds the island circuit, the cars and the game at runtime.
public class Bootstrap : MonoBehaviour
{
    public Material litMat;
    public Material unlitMat;
    public Material fxMat;
    public Material waterMat;
    public Material glassMat;
    public Material groundMat;
    public Material roadMat;
    public Material fxAddMat;      // lambo7: additive particles
    public Light sun;

    // lambou6: URP post profiles + the SSAO renderer feature (editor-made assets, BuildScript)
    public UnityEngine.Rendering.VolumeProfile raceProfile, showroomProfile;
    public UnityEngine.Rendering.Universal.ScriptableRendererFeature ssaoFeature;
    // u6d: god rays / heat haze full-screen pass (URP FullScreenPassRendererFeature + LB/Shafts material, BuildScript)
    public UnityEngine.Rendering.Universal.ScriptableRendererFeature shaftsFeature;
    public Material shaftsMat;

    void Awake()
    {
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        QualitySettings.lodBias = 1f;
        // lambou6: shadows / MSAA are URP asset settings now (U6 + Look.ApplyViews); soft shadows per light
        if (sun != null) sun.shadows = LightShadows.Soft;
        U6.Init(raceProfile, showroomProfile, ssaoFeature);

        Mats.Init(litMat, unlitMat, fxMat, waterMat, glassMat);
        Mats.GroundBase = groundMat; Mats.RoadBase = roadMat; Mats.FxAdd = fxAddMat;
        LBDisplay.Init();
        Gfx.Init();
        Look.Init(sun);
        Gfx6.Init(shaftsMat, shaftsFeature, sun);
        Showroom.Init(sun);
        Sfx.Init();
        FX.Init();
        BuildWorld(StartTrack());
        Vector2 mid = (Track.Min + Track.Max) * 0.5f;
        StartCoroutine(Look.BuildProbes(new Vector3(mid.x, 0f, mid.y), Showroom.BasePos, Showroom.Mask));
        gameObject.AddComponent<Game>();
        LBDisplay.Lobby(true);
    }

    // ?lbtrack=N in the page URL (0 = Coconut Cove .. 5 = Sky City) wins; else the last track picked in the lobby
    static int StartTrack()
    {
        string u = Application.absoluteURL ?? "";
        int i = u.IndexOf("lbtrack=");
        if (i >= 0 && i + 8 < u.Length && char.IsDigit(u[i + 8])) return Mathf.Clamp(u[i + 8] - '0', 0, Track.Count - 1);
        return Mathf.Clamp(PlayerPrefs.GetInt("lb.track", 0), 0, Track.Count - 1);
    }

    // (re)builds the whole world for one track: road + ground + scenery + item boxes + lighting theme
    public static void BuildWorld(int id)
    {
        float t0 = Time.realtimeSinceStartup;
        Shot.ClearAll();
        Skids.Clear();
        ItemBox.ClearAll();
        Track.Build(id);
        Physics.SyncTransforms();
        if (Track.Def == null) Scenery.Build(); else ThemeScenery.Build();
        ItemBox.SpawnAll();
        Look.SetTheme(Track.Theme);
        Gfx.ApplyLevel(LBDisplay.Level, Look.Mobile);
        Debug.Log("Lambo Blast: world built in " + ((Time.realtimeSinceStartup - t0) * 1000f).ToString("F0") + " ms (track " + Track.Id + " " + Track.Name + ")");
    }
}
