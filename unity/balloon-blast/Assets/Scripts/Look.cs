using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Rendering look + quality tiers (same scheme as Lambo Blast / Four Froggies): warm afternoon sun under the Poly Haven
// HDRI sky, one field reflection probe (balloons, rifles, barrels), post (LBPost bloom / grade / vignette) only with a
// single full-screen view, shadows by tier and number of split views. Mobile = phones / tablets (Pixel 9);
// Desktop = PCs + the Tesla browser.
public static class Look
{
    public static bool Mobile { get; private set; }
    public static int Views { get; private set; }
    static Light sun;

    public static void Init(Light s)
    {
        sun = s;
        Mobile = Application.isMobilePlatform || (Input.touchSupported && Mathf.Min(Screen.width, Screen.height) < 900 && Screen.dpi > 200f);
        QualitySettings.realtimeReflectionProbes = true;
        QualitySettings.softParticles = false;
        QualitySettings.anisotropicFiltering = Mobile ? AnisotropicFiltering.Disable : AnisotropicFiltering.Enable;
        QualitySettings.antiAliasing = Mobile ? 0 : 2;
        QualitySettings.shadowCascades = Mobile ? 1 : 2;
        QualitySettings.shadowProjection = ShadowProjection.StableFit;
        QualitySettings.pixelLightCount = 1;
        if (sun != null)
        {
            sun.color = new Color(1f, 0.94f, 0.82f);
            sun.intensity = 1.08f;
            sun.shadowStrength = 0.7f;
            sun.shadowBias = 0.05f;
            sun.shadowNormalBias = 0.4f;
            sun.transform.rotation = Quaternion.Euler(46f, -38f, 0f);
        }
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.46f, 0.53f, 0.66f);
        RenderSettings.ambientEquatorColor = new Color(0.48f, 0.5f, 0.42f);
        RenderSettings.ambientGroundColor = new Color(0.3f, 0.28f, 0.2f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.70f, 0.78f, 0.87f);
        RenderSettings.fogDensity = 0.0045f;
        RenderSettings.reflectionIntensity = 0.9f;
        Debug.Log("Look: tier " + (Mobile ? "mobile" : "desktop") + " " + Screen.width + "x" + Screen.height + " dpi " + Screen.dpi);
    }

    // field probe: sky + field seen from above the middle (glossy balloons, rifles, barrels reflect it)
    public static IEnumerator BuildProbe(Vector3 centre)
    {
        yield return null;
        yield return null;
        try
        {
            var go = new GameObject("FieldProbe");
            go.transform.position = centre + Vector3.up * 10f;
            var p = go.AddComponent<ReflectionProbe>();
            p.mode = ReflectionProbeMode.Realtime;
            p.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            p.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            p.resolution = Mobile ? 64 : 128;
            p.size = new Vector3(700f, 300f, 700f);
            p.boxProjection = false;
            p.cullingMask = 1;   // default layer: field + cover only (no players, no view models)
            p.clearFlags = ReflectionProbeClearFlags.Skybox;
            p.nearClipPlane = 1f;
            p.farClipPlane = 700f;
            p.hdr = false;
            p.importance = 1;
            p.RenderProbe();
            Debug.Log("Look: field reflection probe rendered");
        }
        catch (System.Exception e) { Debug.LogWarning("Look: probe failed " + e.Message); }
    }

    public static bool PostAllowed(int views) { return views == 1; }

    // called whenever the number of drawn views changes (lobby = 1)
    public static void ApplyViews(int views, IEnumerable<Camera> cams)
    {
        Views = views;
        if (views >= 3) QualitySettings.shadows = ShadowQuality.Disable;
        else if (Mobile)
        {
            QualitySettings.shadows = views == 1 ? ShadowQuality.HardOnly : ShadowQuality.Disable;
            QualitySettings.shadowResolution = ShadowResolution.Low;
            QualitySettings.shadowDistance = 34f;
        }
        else
        {
            QualitySettings.shadows = views <= 1 ? ShadowQuality.All : ShadowQuality.HardOnly;
            QualitySettings.shadowResolution = views <= 1 ? ShadowResolution.High : ShadowResolution.Medium;
            QualitySettings.shadowDistance = views <= 1 ? 60f : 32f;
        }
        QualitySettings.lodBias = Mobile || views >= 2 ? 0.7f : 1f;
        bool post = PostAllowed(views);
        if (cams != null)
            foreach (var c in cams)
            {
                if (c == null) continue;
                var p = c.GetComponent<LBPost>();
                if (p == null && post) p = LBPost.Add(c);
                if (p != null) p.enabled = post && c.enabled;
            }
        Debug.Log("Look: " + views + " view(s), shadows " + QualitySettings.shadows + ", post " + post);
    }
}
