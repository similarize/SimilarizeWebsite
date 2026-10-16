using UnityEngine;

// Lives in the generated Main scene. Everything else is created at runtime.
public class Bootstrap : MonoBehaviour
{
    public Material litMat;
    public Material unlitMat;
    public Material fxMat;
    public Terrain terrain;
    public Light sun;
    public Material skinMat, foliageMat, balloonMat;   // graphics overhaul (FF/Skin, FF/Foliage, BB/Balloon); may be null

    public static Bootstrap I;

    void Awake()
    {
        I = this;
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.Medium;
        QualitySettings.shadowDistance = 60f;
        QualitySettings.lodBias = 1f;
        if (sun != null) sun.shadows = LightShadows.Soft;
        if (terrain == null) terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            terrain.heightmapPixelError = 5f;
        }
        Debug.Log("Balloon Blast: graphics overhaul (skin " + (skinMat != null) + ", foliage " + (foliageMat != null) + ", balloon " + (balloonMat != null) + ")");

        Mats.Init(litMat, unlitMat, fxMat);
        Mats.SkinBase = skinMat; Mats.FoliageBase = foliageMat; Mats.BalloonBase = balloonMat;
        Look.Init(sun);
        Look.ApplyViews(1, null);
        float t0 = Time.realtimeSinceStartup;
        string url = Application.absoluteURL ?? "";
        int ai = url.IndexOf("arena=");
        if (ai >= 0 && ai + 6 < url.Length && char.IsDigit(url[ai + 6])) World.arena = (url[ai + 6] - '0') % World.ArenaNames.Length;
        World.Build(terrain, System.Environment.TickCount);
        Debug.Log("Balloon Blast: field built in " + Mathf.RoundToInt((Time.realtimeSinceStartup - t0) * 1000f) + " ms");
        FX.Init();
        Sfx.Init();
        gameObject.AddComponent<BBs>();
        gameObject.AddComponent<Game>();
        gameObject.AddComponent<PowerUps>();
        // U6 (Hark): no soldier models any more (the cast is the Four Froggies + friends), so ModelLoader is not started.
        StartCoroutine(Look.BuildProbe(World.center));
    }
}
