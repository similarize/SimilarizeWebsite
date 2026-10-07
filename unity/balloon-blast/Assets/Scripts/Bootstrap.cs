using UnityEngine;

// Lives in the generated Main scene. Everything else is created at runtime.
public class Bootstrap : MonoBehaviour
{
    public Material litMat;
    public Material unlitMat;
    public Material fxMat;
    public Terrain terrain;
    public Light sun;

    public static Bootstrap I;

    void Awake()
    {
        I = this;
        QualitySettings.vSyncCount = 0;
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.Medium;
        QualitySettings.shadowProjection = ShadowProjection.StableFit;
        QualitySettings.shadowDistance = 70f;
        QualitySettings.shadowCascades = 2;
        QualitySettings.pixelLightCount = 1;
        QualitySettings.antiAliasing = 2;
        if (Application.isMobilePlatform)
        {
            QualitySettings.shadowDistance = 40f;
            QualitySettings.shadowResolution = ShadowResolution.Low;
            QualitySettings.antiAliasing = 0;
        }
        if (sun != null) sun.shadows = LightShadows.Soft;
        if (terrain == null) terrain = Terrain.activeTerrain;

        Mats.Init(litMat, unlitMat, fxMat);
        World.Build(terrain, System.Environment.TickCount);
        FX.Init();
        gameObject.AddComponent<BBs>();
        gameObject.AddComponent<Game>();
        ModelLoader.Begin();
    }
}
