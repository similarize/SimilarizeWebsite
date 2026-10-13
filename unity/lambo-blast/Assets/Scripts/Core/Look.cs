using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Rendering look + quality tiers: warm beach lighting, reflection probes (island + showroom sky), post on/off,
// shadows by tier and number of split views. Mobile = phones / tablets (Pixel 9); Desktop = PCs + the Tesla browser.
public static class Look
{
    public static bool Mobile { get; private set; }
    public static int Views { get; private set; }
    static ReflectionProbe island, showroom;
    static Light sun;

    public static void Init(Light s)
    {
        sun = s;
        Mobile = Application.isMobilePlatform || (Input.touchSupported && Mathf.Min(Screen.width, Screen.height) < 900 && Screen.dpi > 200f);
        QualitySettings.realtimeReflectionProbes = true;
        QualitySettings.softParticles = false;
        QualitySettings.anisotropicFiltering = Mobile ? AnisotropicFiltering.Disable : AnisotropicFiltering.Enable;
        // warm late-morning sun, saturated sky / sea bounce
        if (sun != null)
        {
            sun.color = new Color(1f, 0.93f, 0.8f);
            sun.intensity = 1.1f;
            sun.shadowStrength = 0.72f;
            sun.transform.rotation = Quaternion.Euler(48f, -38f, 0f);
        }
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.42f, 0.5f, 0.64f);
        RenderSettings.ambientEquatorColor = new Color(0.5f, 0.5f, 0.46f);
        RenderSettings.ambientGroundColor = new Color(0.42f, 0.36f, 0.28f);
        RenderSettings.fogColor = new Color(0.66f, 0.75f, 0.86f);
        RenderSettings.reflectionIntensity = 1f;
        Debug.Log("Look: tier " + (Mobile ? "mobile" : "desktop") + " " + Screen.width + "x" + Screen.height + " dpi " + Screen.dpi);
    }

    static ReflectionProbe MakeProbe(string name, Vector3 pos, Vector3 size, int mask, int res)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var p = go.AddComponent<ReflectionProbe>();
        p.mode = ReflectionProbeMode.Realtime;
        p.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
        p.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
        p.resolution = res;
        p.size = size;
        p.boxProjection = false;
        p.cullingMask = mask;
        p.clearFlags = ReflectionProbeClearFlags.Skybox;
        p.nearClipPlane = 1f;
        p.farClipPlane = 1500f;
        p.hdr = false;
        p.intensity = 1f;
        p.importance = 1;
        return p;
    }

    // island probe (sky + island + sea seen from above the start line) and a sky-only probe for the showroom
    public static IEnumerator BuildProbes(Vector3 islandCentre, Vector3 showroomBase, int excludeMask)
    {
        yield return null;
        try
        {
            island = MakeProbe("IslandProbe", islandCentre + Vector3.up * 14f, new Vector3(2400f, 900f, 2400f), ~excludeMask, Mobile ? 64 : 128);
            island.RenderProbe();
            showroom = MakeProbe("ShowroomProbe", showroomBase + Vector3.up * 30f, new Vector3(1200f, 600f, 1200f), 0, 64);
            showroom.RenderProbe();
            Debug.Log("Look: reflection probes rendered");
        }
        catch (System.Exception e) { Debug.LogWarning("Look: probes failed " + e.Message); }
    }

    // per-track lighting: sun, ambient, fog and sky tint (0 = Coconut Cove's original look)
    static Material skyMat;
    static float skyExposure = -1f;
    public static void SetTheme(int th)
    {
        if (skyMat == null && RenderSettings.skybox != null)
        {
            skyMat = new Material(RenderSettings.skybox);
            RenderSettings.skybox = skyMat;
            if (skyMat.HasProperty("_Exposure")) skyExposure = skyMat.GetFloat("_Exposure");
        }
        Color sunC = new Color(1f, 0.93f, 0.8f), sky = new Color(0.42f, 0.5f, 0.64f), eq = new Color(0.5f, 0.5f, 0.46f), gr = new Color(0.42f, 0.36f, 0.28f);
        Color fog = new Color(0.66f, 0.75f, 0.86f), tint = new Color(0.5f, 0.5f, 0.5f);
        float sunI = 1.1f, fogD = 0.0022f, exp = 1f; Vector3 sunE = new Vector3(48f, -38f, 0f);
        switch (th)
        {
            case 1: sunC = new Color(1f, 0.87f, 0.68f); sunI = 1.15f; sunE = new Vector3(40f, -60f, 0f); sky = new Color(0.5f, 0.48f, 0.52f); eq = new Color(0.58f, 0.46f, 0.36f); gr = new Color(0.5f, 0.32f, 0.22f); fog = new Color(0.86f, 0.72f, 0.58f); fogD = 0.0016f; tint = new Color(0.56f, 0.5f, 0.44f); break;
            case 2: sunC = new Color(1f, 0.58f, 0.38f); sunI = 0.95f; sunE = new Vector3(26f, 30f, 0f); sky = new Color(0.3f, 0.22f, 0.24f); eq = new Color(0.42f, 0.26f, 0.2f); gr = new Color(0.55f, 0.22f, 0.08f); fog = new Color(0.32f, 0.2f, 0.16f); fogD = 0.0028f; tint = new Color(0.4f, 0.24f, 0.2f); exp = 0.75f; break;
            case 3: sunC = new Color(0.96f, 0.97f, 1f); sunI = 1.1f; sunE = new Vector3(36f, -20f, 0f); sky = new Color(0.46f, 0.54f, 0.68f); eq = new Color(0.52f, 0.56f, 0.62f); gr = new Color(0.5f, 0.52f, 0.58f); fog = new Color(0.8f, 0.85f, 0.92f); fogD = 0.0024f; tint = new Color(0.5f, 0.53f, 0.58f); exp = 1.1f; break;
            case 4: sunC = new Color(1f, 0.95f, 0.8f); sunI = 1.05f; sunE = new Vector3(55f, -70f, 0f); sky = new Color(0.4f, 0.5f, 0.52f); eq = new Color(0.4f, 0.48f, 0.36f); gr = new Color(0.26f, 0.32f, 0.2f); fog = new Color(0.55f, 0.66f, 0.56f); fogD = 0.0032f; tint = new Color(0.46f, 0.52f, 0.46f); break;
            case 5: sunC = new Color(1f, 0.9f, 0.8f); sunI = 1.1f; sunE = new Vector3(32f, -110f, 0f); sky = new Color(0.44f, 0.5f, 0.64f); eq = new Color(0.5f, 0.5f, 0.52f); gr = new Color(0.35f, 0.35f, 0.38f); fog = new Color(0.72f, 0.78f, 0.88f); fogD = 0.0011f; tint = new Color(0.5f, 0.52f, 0.56f); exp = 1.05f; break;
        }
        if (sun != null) { sun.color = sunC; sun.intensity = sunI; sun.transform.rotation = Quaternion.Euler(sunE); }
        RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = eq; RenderSettings.ambientGroundColor = gr;
        RenderSettings.fogColor = fog; RenderSettings.fogDensity = fogD;
        if (skyMat != null)
        {
            if (skyMat.HasProperty("_Tint")) skyMat.SetColor("_Tint", tint);
            if (skyMat.HasProperty("_Exposure") && skyExposure > 0f) skyMat.SetFloat("_Exposure", skyExposure * exp);
        }
        Debug.Log("Look: theme " + th);
    }

    // re-render the island probe after a track rebuild
    public static void RefreshIsland(Vector3 centre)
    {
        if (island == null) return;
        island.transform.position = centre + Vector3.up * 14f;
        island.RenderProbe();
    }

    public static bool PostAllowed(int views) { return views == 1; }

    // called whenever the number of views changes (lobby = 0 player views)
    public static void ApplyViews(int views, List<Camera> playerCams)
    {
        Views = views;
        if (views >= 3) QualitySettings.shadows = ShadowQuality.Disable;
        else if (Mobile)
        {
            QualitySettings.shadows = views == 1 ? ShadowQuality.HardOnly : ShadowQuality.Disable;
            QualitySettings.shadowResolution = ShadowResolution.Low;
            QualitySettings.shadowDistance = 32f;
        }
        else
        {
            QualitySettings.shadows = views <= 1 ? ShadowQuality.All : ShadowQuality.HardOnly;
            QualitySettings.shadowResolution = views <= 1 ? ShadowResolution.High : ShadowResolution.Medium;
            QualitySettings.shadowDistance = views <= 1 ? 55f : 30f;
        }
        QualitySettings.lodBias = Mobile || views >= 2 ? 0.7f : 1f;
        bool post = PostAllowed(views);
        if (playerCams != null)
            foreach (var c in playerCams)
            {
                if (c == null) continue;
                var p = c.GetComponent<LBPost>();
                if (p != null) p.enabled = post;
            }
    }
}
