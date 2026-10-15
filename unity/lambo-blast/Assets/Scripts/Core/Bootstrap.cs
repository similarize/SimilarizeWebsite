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

    void Awake()
    {
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.Medium;
        QualitySettings.shadowProjection = ShadowProjection.StableFit;
        QualitySettings.shadowDistance = 45f;
        QualitySettings.shadowCascades = 1;
        QualitySettings.pixelLightCount = 4;      // lambo7: the showroom rig's four lights are per-pixel (island = sun only)
        QualitySettings.antiAliasing = 0;
        QualitySettings.lodBias = 1f;
        if (Application.isMobilePlatform)
        {
            QualitySettings.shadowDistance = 30f;
            QualitySettings.shadowResolution = ShadowResolution.Low;
        }
        if (sun != null) sun.shadows = LightShadows.Soft;

        Mats.Init(litMat, unlitMat, fxMat, waterMat, glassMat);
        Mats.GroundBase = groundMat; Mats.RoadBase = roadMat; Mats.FxAdd = fxAddMat;
        LBDisplay.Init();
        Gfx.Init();
        Look.Init(sun);
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
