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
        RenderSettings.ambientSkyColor = new Color(0.5f, 0.62f, 0.8f);
        RenderSettings.ambientEquatorColor = new Color(0.56f, 0.56f, 0.52f);
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
