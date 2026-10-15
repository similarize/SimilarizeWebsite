using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// lambou6: everything the Unity 6.6 URP port needs at runtime, in one place (the Built-in code called QualitySettings,
// OnRenderImage and Camera.onPreCull directly; in URP those live on the pipeline asset, per-camera data and Volumes).
//  * graphics API log ("U6: graphics API WebGPU ..." / "OpenGLES3") - the probe greps it
//  * shadows (resolution / distance / soft / per camera on-off), MSAA, STP / TAA, SSAO renderer feature on-off
//  * global post Volumes: race grade (ACES, bloom, colour adjustments, white balance, vignette; per-track values from LBPost)
//    on layer 0, showroom grade on layer 20 (stand / thumbnail cameras only see that one)
//  * URL overrides for tuning without a rebuild: u6exp=<EV bias> u6tm=aces|neutral|none u6aa=msaa|stp|taa|smaa|fxaa|none
//    u6ssao=0|1 u6bloom=<scale>
public static class U6
{
    public const int ShowroomVolumeLayer = 20;
    public static UniversalRenderPipelineAsset Asset { get { return GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset; } }
    public static bool WebGPU { get { return SystemInfo.graphicsDeviceType == GraphicsDeviceType.WebGPU; } }
    public static string Api { get { return SystemInfo.graphicsDeviceType.ToString(); } }
    public static VolumeProfile RaceProfile, ShowroomProfile;
    public static ScriptableRendererFeature Ssao;
    static Volume raceVol, showVol;
    static string url = "";
    public static float ExpBias = 0.55f, BloomScale = 1f;
    public static TonemappingMode Tm = TonemappingMode.ACES;
    public static string AaForce = "";
    static int ssaoForce = -1;

    public static void Init(VolumeProfile race, VolumeProfile show, ScriptableRendererFeature ssao)
    {
        url = Application.absoluteURL ?? "";
        RaceProfile = race; ShowroomProfile = show; Ssao = ssao;
        float f;
        string s = Q("u6exp"); if (s != null && float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f)) ExpBias = f;
        s = Q("u6bloom"); if (s != null && float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f)) BloomScale = f;
        s = Q("u6tm"); if (s == "neutral") Tm = TonemappingMode.Neutral; else if (s == "none") Tm = TonemappingMode.None;
        s = Q("u6aa"); if (s != null) AaForce = s;
        s = Q("u6ssao"); if (s == "0") ssaoForce = 0; else if (s == "1") ssaoForce = 1;
        var a = Asset;
        Debug.Log("U6: graphics API " + Api + " | device " + SystemInfo.graphicsDeviceName + " | " + SystemInfo.graphicsDeviceVersion +
            " | compute " + SystemInfo.supportsComputeShaders + " | Unity " + Application.unityVersion + " | pipeline " + (a != null ? a.name : "NONE (Built-in!)") +
            " | colour space " + QualitySettings.activeColorSpace + " | HDR " + (a != null && a.supportsHDR) + " | ssao " + (ssao != null) +
            " | profiles " + (race != null) + "/" + (show != null) + " | exp bias " + ExpBias + " tm " + Tm + (AaForce.Length > 0 ? " aa " + AaForce : ""));
        if (race != null) raceVol = MakeVolume("LBRaceVolume", race, 0);
        if (show != null) showVol = MakeVolume("LBShowroomVolume", show, ShowroomVolumeLayer);
        if (a != null) { a.shadowCascadeCount = 2; a.cascade2Split = 0.25f; }
    }

    // ?name=value in the page URL (null if absent)
    public static string Q(string name)
    {
        int i = url.IndexOf(name + "=");
        if (i < 0) return null;
        i += name.Length + 1;
        int j = i;
        while (j < url.Length && url[j] != '&' && url[j] != '#') j++;
        return url.Substring(i, j - i);
    }

    static Volume MakeVolume(string name, VolumeProfile p, int layer)
    {
        var go = new GameObject(name);
        Object.DontDestroyOnLoad(go);
        go.layer = layer;
        var v = go.AddComponent<Volume>();
        v.isGlobal = true;
        v.priority = 1;
        v.sharedProfile = p;
        return v;
    }

    static UniversalAdditionalCameraData Data(Camera c) { return c != null ? c.GetUniversalAdditionalCameraData() : null; }

    // a game / preview camera: HDR, post on (race volume)
    public static void RaceCam(Camera c, bool post)
    {
        var d = Data(c); if (d == null) return;
        c.allowHDR = true;
        d.renderPostProcessing = post;
        d.volumeLayerMask = 1;
        d.stopNaN = false;
        d.dithering = true;
    }

    // a showroom stand / thumbnail camera: post with the showroom volume only
    public static void ShowroomCam(Camera c)
    {
        var d = Data(c); if (d == null) return;
        d.renderPostProcessing = true;
        d.volumeLayerMask = 1 << ShowroomVolumeLayer;
        d.renderShadows = false;
        d.antialiasing = AntialiasingMode.None;
    }

    public static void Post(Camera c, bool on) { var d = Data(c); if (d != null) d.renderPostProcessing = on; }
    public static void CamShadows(Camera c, bool on) { var d = Data(c); if (d != null) d.renderShadows = on; }

    // Built-in QualitySettings.shadowResolution -> pixels (Low 512 .. VeryHigh 4096 for the sun)
    public static void Shadows(bool soft, int res, float distance, Light sun)
    {
        var a = Asset;
        if (a != null)
        {
            a.mainLightShadowmapResolution = res;
            a.shadowDistance = distance;
        }
        if (sun != null && sun.shadows != LightShadows.None) sun.shadows = soft ? LightShadows.Soft : LightShadows.Hard;
    }

    // MSAA count (1 = off) unless a temporal AA mode (STP / TAA) runs on this camera
    static string lastAa = "";
    public static void AntiAlias(int msaa, bool racing, bool allowTemporal, System.Collections.Generic.List<Camera> cams)
    {
        var a = Asset; if (a == null) return;
        string mode = AaForce.Length > 0 ? AaForce : (racing && allowTemporal && WebGPU ? "stp" : "msaa");
        if (!racing && (mode == "stp" || mode == "taa")) mode = "msaa";     // lobby: plain MSAA (RT showroom cameras)
        int m = mode == "msaa" ? Mathf.Max(1, msaa) : 1;
        a.msaaSampleCount = m;
#pragma warning disable 618
        a.upscalingFilter = mode == "stp" ? UpscalingFilterSelection.STP : UpscalingFilterSelection.Auto;
#pragma warning restore 618
        TrySetUpscalerName(a, mode == "stp" ? "STP" : "");
        AntialiasingMode cm = mode == "taa" ? AntialiasingMode.TemporalAntiAliasing : mode == "smaa" ? AntialiasingMode.SubpixelMorphologicalAntiAliasing :
            mode == "fxaa" ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None;
        if (cams != null) foreach (var c in cams) { var d = Data(c); if (d != null) d.antialiasing = cm; }
        string tag = mode + " msaa " + m;
        if (tag != lastAa) { lastAa = tag; Debug.Log("U6: AA " + tag + (racing ? " (race)" : " (lobby)")); }
    }

    static bool upTried; static System.Reflection.PropertyInfo upProp;
    static void TrySetUpscalerName(UniversalRenderPipelineAsset a, string n)
    {
        if (!upTried) { upTried = true; upProp = typeof(UniversalRenderPipelineAsset).GetProperty("upscalerName"); if (upProp != null && !upProp.CanWrite) upProp = null; }
        if (upProp == null) return;
        try { upProp.SetValue(a, n); } catch { }
    }

    public static void SsaoOn(bool on)
    {
        if (Ssao == null) return;
        if (ssaoForce >= 0) on = ssaoForce == 1;
        if (Ssao.isActive != on) { Ssao.SetActive(on); Debug.Log("U6: SSAO " + (on ? "on" : "off")); }
    }

    // ------------------------------------------------------------- post grade (values from LBPost's per-track table)
    public static void Grade(float sat, float con, float warm, float film, float bloom, float vignette, bool cheap)
    {
        var p = RaceProfile; if (p == null) return;
        Tonemapping tm; if (p.TryGet(out tm)) { tm.active = true; tm.mode.Override(Tm); }
        ColorAdjustments ca;
        if (p.TryGet(out ca))
        {
            ca.active = true;
            ca.postExposure.Override(ExpBias + Mathf.Log(Mathf.Max(0.05f, film), 2f));
            ca.saturation.Override(Mathf.Clamp((sat - 1f) * 90f, -100f, 100f));
            ca.contrast.Override(Mathf.Clamp((con - 1f) * 80f, -100f, 100f));
        }
        WhiteBalance wb; if (p.TryGet(out wb)) { wb.active = !cheap; wb.temperature.Override(Mathf.Clamp(warm * 220f, -100f, 100f)); }
        Bloom b;
        if (p.TryGet(out b))
        {
            b.active = !cheap && bloom > 0f;
            b.intensity.Override(bloom * 1.3f * BloomScale);
            b.threshold.Override(1.0f);
            b.scatter.Override(0.62f);
            b.highQualityFiltering.Override(!Look.Mobile);
        }
        Vignette v; if (p.TryGet(out v)) { v.active = vignette > 0.01f; v.intensity.Override(vignette * 0.62f); v.smoothness.Override(0.45f); }
    }
}
