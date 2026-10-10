using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Rendering look + quality tiers (same scheme as Lambo Blast): warm ranch sun under the Poly Haven HDRI sky,
// a ranch reflection probe, post (LBPost bloom / grade / vignette) only with a single full-screen view, and shadows by
// tier and number of split views. Mobile = phones / tablets (Pixel 9); Desktop = PCs + the Tesla browser.
public static class Look
{
    public static bool Mobile { get; private set; }
    public static int Views { get; private set; }
    static ReflectionProbe ranch;
    static Light sun;

    public static void Init(Light s)
    {
        sun = s;
        // ffu17: the lobby now renders at the real pixel density (Screen.width 1030+ on a Pixel 9), so the size test alone
        // would miss phones; the browser user agent (same test as the page) decides too
        Mobile = Application.isMobilePlatform || FFDisplay.MobileUA || (Input.touchSupported && Mathf.Min(Screen.width, Screen.height) < 900 && Screen.dpi > 200f);
        QualitySettings.realtimeReflectionProbes = true;
        QualitySettings.softParticles = false;
        QualitySettings.anisotropicFiltering = Mobile ? AnisotropicFiltering.Disable : AnisotropicFiltering.Enable;
        if (sun != null)
        {
            sun.color = new Color(1f, 0.94f, 0.82f);
            sun.intensity = 1.08f;
            sun.shadowStrength = 0.7f;
            sun.transform.rotation = Quaternion.Euler(48f, -38f, 0f);
        }
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.46f, 0.53f, 0.66f);
        RenderSettings.ambientEquatorColor = new Color(0.48f, 0.5f, 0.42f);
        RenderSettings.ambientGroundColor = new Color(0.3f, 0.28f, 0.2f);
        RenderSettings.fogColor = new Color(0.68f, 0.76f, 0.86f);
        RenderSettings.fogDensity = 0.0032f;
        RenderSettings.reflectionIntensity = 0.9f;
        Debug.Log("Look: tier " + (Mobile ? "mobile" : "desktop") + " " + Screen.width + "x" + Screen.height + " dpi " + Screen.dpi);
    }

    // ranch probe: sky + ranch seen from above the yard (pond, Cybertruck stainless and glass reflect it)
    public static IEnumerator BuildProbes(Vector3 centre, int excludeMask)
    {
        yield return null;
        yield return null;
        try
        {
            var go = new GameObject("RanchProbe");
            go.transform.position = centre + Vector3.up * 12f;
            ranch = go.AddComponent<ReflectionProbe>();
            ranch.mode = ReflectionProbeMode.Realtime;
            ranch.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            ranch.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            ranch.resolution = Mobile ? 64 : (Realism.On && !Realism.Lite ? 256 : 128);
            ranch.size = new Vector3(900f, 400f, 900f);
            ranch.boxProjection = false;
            ranch.cullingMask = ~excludeMask;
            ranch.clearFlags = ReflectionProbeClearFlags.Skybox;
            ranch.nearClipPlane = 1f;
            ranch.farClipPlane = 900f;
            ranch.hdr = false;
            ranch.importance = 1;
            ranch.RenderProbe();
            Debug.Log("Look: ranch reflection probe rendered");
        }
        catch (System.Exception e) { Debug.LogWarning("Look: probe failed " + e.Message); }
    }

    // realism test: re-render once the HDRI sky + scans have streamed in
    public static void RerenderProbe(int res)
    {
        if (ranch == null) return;
        try { ranch.resolution = res; ranch.RenderProbe(); Debug.Log("Look: ranch probe re-rendered " + res); }
        catch (System.Exception e) { Debug.LogWarning("Look: probe re-render failed " + e.Message); }
    }

    public static bool PostAllowed(int views) { return views == 1; }

    // called whenever the number of drawn views changes
    public static void ApplyViews(int views, IEnumerable<Camera> cams)
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
        if (cams != null)
            foreach (var c in cams)
            {
                if (c == null) continue;
                var p = c.GetComponent<LBPost>();
                if (p == null && post) p = LBPost.Add(c);
                if (p != null) p.enabled = post && c.enabled;
            }
        if (Realism.On) Realism.ApplyQuality(views, cams);   // ?realism=1 test only
    }
}
