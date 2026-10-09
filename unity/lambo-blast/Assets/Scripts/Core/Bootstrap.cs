using UnityEngine;

// Lives in the generated Main scene. Builds the island circuit, the cars and the game at runtime.
public class Bootstrap : MonoBehaviour
{
    public Material litMat;
    public Material unlitMat;
    public Material fxMat;
    public Material waterMat;
    public Material glassMat;
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
        QualitySettings.pixelLightCount = 1;
        QualitySettings.antiAliasing = 0;
        QualitySettings.lodBias = 1f;
        if (Application.isMobilePlatform)
        {
            QualitySettings.shadowDistance = 30f;
            QualitySettings.shadowResolution = ShadowResolution.Low;
        }
        if (sun != null) sun.shadows = LightShadows.Soft;

        Mats.Init(litMat, unlitMat, fxMat, waterMat, glassMat);
        Sfx.Init();
        FX.Init();
        float t0 = Time.realtimeSinceStartup;
        Track.Build();
        Physics.SyncTransforms();
        Scenery.Build();
        ItemBox.SpawnAll();
        Debug.Log("Lambo Blast: world built in " + ((Time.realtimeSinceStartup - t0) * 1000f).ToString("F0") + " ms");
        gameObject.AddComponent<Game>();
    }
}
