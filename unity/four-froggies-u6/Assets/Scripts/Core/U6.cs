using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// ffu6 (Four Froggies on Unity 6.6 URP, side by side with the 2022.3 build; same pattern as Lambo Blast u6's U6.cs).
// Everything the URP port needs at runtime, in one place. The Built-in game code still sets QualitySettings (shadows,
// distance, resolution, MSAA), toggles LBPost / RealPost / ShowroomGrade components and hooks Camera.onPreCull; in URP those
// live on the pipeline asset, per-camera data, Volumes and RenderPipelineManager, so:
//  * U6Cam      = Camera.onPreCull / onPostRender stand-ins (RenderPipelineManager.begin / endCameraRendering, same order),
//                 + per-camera trilight ambient -> SH (URP reads RenderSettings.ambientProbe)
//  * U6Runner   = LateUpdate mirror: QualitySettings shadows / distance / resolution / MSAA -> URP asset + per-camera
//                 shadows; post on/off per camera from the old post components; SSAO feature on the high tier, one view
//  * U6         = graphics API log, global post Volumes (game grade on layer 0, studio grade on layer 20), URL knobs
//  * U6Layers   = rendering layers for the lobby studio lights (Built-in used Light.cullingMask)
// URL knobs (no rebuild): u6exp=<EV bias> u6tm=aces|neutral|none u6ssao=0|1 u6bloom=<scale> u6msaa=1|2|4 u6sat=<+-> u6con=<+->
public static class U6
{
    public const int StudioVolumeLayer = 20;
    public static UniversalRenderPipelineAsset Asset { get { return GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset; } }
    public static bool WebGPU { get { return SystemInfo.graphicsDeviceType == GraphicsDeviceType.WebGPU; } }
    public static string Api { get { return SystemInfo.graphicsDeviceType.ToString(); } }
    public static bool Linear { get { return QualitySettings.activeColorSpace == ColorSpace.Linear; } }
    public static VolumeProfile GameProfile, StudioProfile;
    public static ScriptableRendererFeature Ssao;
    static string url = "";
    public static float ExpBias = 0.35f, BloomScale = 1f, SatAdd = 0f, ConAdd = 0f;
    public static TonemappingMode Tm = TonemappingMode.ACES;
    public static int MsaaForce = 0, SsaoForce = -1;
    public static bool Ready;

    public static void Init(VolumeProfile game, VolumeProfile studio, ScriptableRendererFeature ssao)
    {
        url = Application.absoluteURL ?? "";
        GameProfile = game; StudioProfile = studio; Ssao = ssao;
        float f;
        if (F("u6exp", out f)) ExpBias = f;
        if (F("u6bloom", out f)) BloomScale = f;
        if (F("u6sat", out f)) SatAdd = f;
        if (F("u6con", out f)) ConAdd = f;
        string s = Q("u6tm"); if (s == "neutral") Tm = TonemappingMode.Neutral; else if (s == "none") Tm = TonemappingMode.None;
        s = Q("u6ssao"); if (s == "0") SsaoForce = 0; else if (s == "1") SsaoForce = 1;
        s = Q("u6msaa"); int mi; if (s != null && int.TryParse(s, out mi)) MsaaForce = Mathf.Clamp(mi, 1, 8);
        var a = Asset;
        Debug.Log("U6: graphics API " + Api + " | device " + SystemInfo.graphicsDeviceName + " | " + SystemInfo.graphicsDeviceVersion +
            " | compute " + SystemInfo.supportsComputeShaders + " | Unity " + Application.unityVersion + " | pipeline " + (a != null ? a.name : "NONE (Built-in!)") +
            " | colour space " + QualitySettings.activeColorSpace + " | HDR " + (a != null && a.supportsHDR) + " | ssao " + (ssao != null) +
            " | profiles " + (game != null) + "/" + (studio != null) + " | exp bias " + ExpBias + " tm " + Tm);
        if (game != null) MakeVolume("FFGameVolume", game, 0);
        if (studio != null) MakeVolume("FFStudioVolume", studio, StudioVolumeLayer);
        Grade(0.84f);
        if (studio != null)
        {
            ColorAdjustments ca; if (studio.TryGet(out ca)) { ca.active = true; ca.postExposure.Override(0.5f + ExpBias * 0.5f); }
            Tonemapping tm; if (studio.TryGet(out tm)) { tm.active = Tm != TonemappingMode.None; tm.mode.Override(Tm); }
        }
        if (a != null) { a.shadowCascadeCount = 2; a.cascade2Split = 0.25f; }
        var go = new GameObject("U6Runner");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<U6Runner>();
        Ready = true;
    }

    public static string Q(string name)
    {
        int i = url.IndexOf(name + "=");
        if (i < 0) return null;
        i += name.Length + 1;
        int j = i;
        while (j < url.Length && url[j] != '&' && url[j] != '#') j++;
        return url.Substring(i, j - i);
    }
    static bool F(string name, out float v)
    {
        v = 0f; string s = Q(name);
        return s != null && float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v);
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

    // a colour the old (Gamma) code meant as a LINEAR value for a custom shader (REAL ROOM): Material.SetColor converts
    // gamma -> linear in a Linear project, so hand it the gamma encoding (Lambo u6f lesson)
    public static Color Lin(Color c) { return Linear ? c.gamma : c; }

    static float lastExp = -99f;
    // game grade: RealPost's / LBPost's exposure (Light55 moves it per world) -> URP post exposure; ACES; light bloom; vignette
    public static void Grade(float exposure)
    {
        var p = GameProfile; if (p == null) return;
        float ev = ExpBias + Mathf.Log(Mathf.Max(0.05f, exposure) / 0.84f, 2f);
        if (Mathf.Abs(ev - lastExp) < 0.002f) return;
        lastExp = ev;
        Tonemapping tm; if (p.TryGet(out tm)) { tm.active = Tm != TonemappingMode.None; tm.mode.Override(Tm); }
        ColorAdjustments ca;
        if (p.TryGet(out ca)) { ca.active = true; ca.postExposure.Override(ev); ca.saturation.Override(10f + SatAdd); ca.contrast.Override(8f + ConAdd); }
        Bloom b; if (p.TryGet(out b)) { b.active = BloomScale > 0f; b.intensity.Override(0.32f * BloomScale); b.threshold.Override(1.05f); b.scatter.Override(0.6f); b.highQualityFiltering.Override(!Look.Mobile); }
        Vignette v; if (p.TryGet(out v)) { v.active = true; v.intensity.Override(0.18f); v.smoothness.Override(0.5f); }
    }

    static UniversalAdditionalCameraData Data(Camera c) { return c != null ? c.GetUniversalAdditionalCameraData() : null; }

    public static void SsaoOn(bool on)
    {
        if (Ssao == null) return;
        if (SsaoForce >= 0) on = SsaoForce == 1;
        if (Ssao.isActive != on) { Ssao.SetActive(on); Debug.Log("U6: SSAO " + (on ? "on" : "off")); }
    }

    static readonly Camera[] camBuf = new Camera[32];
    static int lastRes = -1, lastMsaa = -1; static float lastDist = -1f; static UnityEngine.ShadowQuality lastQ = (UnityEngine.ShadowQuality)(-1);
    static string lastTag = "";

    // the LateUpdate mirror (U6Runner)
    public static void Mirror()
    {
        var a = Asset; if (a == null) return;
        UnityEngine.ShadowQuality q = QualitySettings.shadows;
        int res = QualitySettings.shadowResolution == UnityEngine.ShadowResolution.VeryHigh ? 4096 : QualitySettings.shadowResolution == UnityEngine.ShadowResolution.High ? 2048 :
                  QualitySettings.shadowResolution == UnityEngine.ShadowResolution.Medium ? 1024 : 512;
        if (res != lastRes) { lastRes = res; a.mainLightShadowmapResolution = res; }
        float dist = Mathf.Max(5f, QualitySettings.shadowDistance);
        if (Mathf.Abs(dist - lastDist) > 0.01f) { lastDist = dist; a.shadowDistance = dist; }
        Light sun = RenderSettings.sun;
        if (q != lastQ)
        {
            lastQ = q;
            if (sun != null && sun.shadows != LightShadows.None) sun.shadows = q == UnityEngine.ShadowQuality.HardOnly ? LightShadows.Hard : LightShadows.Soft;
        }
        int n = Camera.GetAllCameras(camBuf);
        int views = 0;
        for (int i = 0; i < n; i++) if (camBuf[i] != null && camBuf[i].targetTexture == null && camBuf[i].cullingMask != (1 << StudioVolumeLayer)) views++;
        // MSAA: the Built-in code set QualitySettings.antiAliasing (0 under the default realism look); URP: 4x lobby, 2x in game on
        // desktop, off on the low tier / phones in split-screen. u6msaa= overrides.
        int msaa = Mathf.Max(1, QualitySettings.antiAliasing);
        if (Realism.On) msaa = FFDisplay.InLobby ? 4 : (Perf60.Tier == 0 ? 1 : (Look.Mobile && views > 1 ? 1 : 2));
        if (MsaaForce > 0) msaa = MsaaForce;
        msaa = msaa >= 4 ? 4 : msaa >= 2 ? 2 : 1;
        if (msaa != lastMsaa) { lastMsaa = msaa; a.msaaSampleCount = msaa; }
        bool shadowsOn = q != UnityEngine.ShadowQuality.Disable;
        for (int i = 0; i < n; i++)
        {
            Camera c = camBuf[i]; if (c == null) continue;
            var d = Data(c); if (d == null) continue;
            bool studio = c.GetComponent<ShowroomGrade>() != null;
            bool rr = c.cullingMask == (1 << RealRoom.Layer);
            bool post = !rr && (On<LBPost>(c) || On<RealPost>(c) || On<ShowroomGrade>(c));
            if (d.renderPostProcessing != post) d.renderPostProcessing = post;
            int vm = studio ? 1 << StudioVolumeLayer : 1;
            if (d.volumeLayerMask.value != vm) d.volumeLayerMask = vm;
            bool sh = shadowsOn && !studio && !rr;
            if (d.renderShadows != sh) d.renderShadows = sh;
            if (post && !d.dithering) d.dithering = true;
        }
        SsaoOn(Perf60.AO && views <= 1 && !FFDisplay.InLobby);
        string tag = "views " + views + " msaa " + msaa + " shadows " + q + " " + res + "/" + dist.ToString("0") + " tier " + Perf60.TierName;
        if (tag != lastTag) { lastTag = tag; Debug.Log("U6: " + tag); }
    }

    static bool On<T>(Camera c) where T : Behaviour { var b = c.GetComponent<T>(); return b != null && b.enabled; }

    // per camera, just before it renders (after the game's own pre-cull hooks): the trilight ambient as SH and the camera's
    // post exposure
    public static void BeforeCamera(Camera c)
    {
        if (RenderSettings.ambientMode == AmbientMode.Trilight)
            RenderSettings.ambientProbe = U6Layers.TrilightSH(RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor, RenderSettings.ambientGroundColor);
        var rp = c.GetComponent<RealPost>();
        if (rp != null && rp.enabled) { Grade(rp.exposure); return; }
        var lp = c.GetComponent<LBPost>();
        if (lp != null && lp.enabled) Grade(lp.exposure * 0.84f / 0.94f);
    }
}

// Camera.onPreCull / Camera.onPostRender for URP (Built-in only fires those). Same subscription order, same signature.
public static class U6Cam
{
    static System.Action<Camera> pre, post;
    static bool hooked;
    static int errs;
    public static event System.Action<Camera> PreCull { add { Hook(); pre += value; } remove { pre -= value; } }
    public static event System.Action<Camera> PostRender { add { Hook(); post += value; } remove { post -= value; } }

    static void Hook()
    {
        if (hooked) return;
        hooked = true;
        RenderPipelineManager.beginCameraRendering += Begin;
        RenderPipelineManager.endCameraRendering += End;
    }
    static void Begin(ScriptableRenderContext ctx, Camera c)
    {
        try { if (pre != null) pre(c); if (U6.Ready) U6.BeforeCamera(c); }
        catch (System.Exception e) { if (errs++ < 20) Debug.LogException(e); }
    }
    static void End(ScriptableRenderContext ctx, Camera c)
    {
        try { if (post != null) post(c); }
        catch (System.Exception e) { if (errs++ < 20) Debug.LogException(e); }
    }
}

public class U6Runner : MonoBehaviour
{
    void LateUpdate() { U6.Mirror(); }
}

// lobby studio vs world lights with URP rendering layers (Built-in used Light.cullingMask): studio lights only light layer-20
// renderers (renderingLayerMask = StudioBit, set by Mats.SetLayer), every other light only the default bit
public static class U6Layers
{
    public const uint WorldBit = 1u, StudioBit = 1u << 2;
    public static void World(Light l) { Set(l, WorldBit); }
    public static void Studio(Light l) { Set(l, StudioBit); }
    static void Set(Light l, uint bits)
    {
        if (l == null) return;
        var d = l.GetUniversalAdditionalLightData();
        if (d != null) { d.renderingLayers = bits; d.shadowRenderingLayers = bits; }
    }
    public static void Renderers(GameObject g, int layer)
    {
        var r = g.GetComponent<Renderer>();
        if (r == null) return;
        if (layer == U6.StudioVolumeLayer) r.renderingLayerMask = StudioBit;
        else if (r.renderingLayerMask == StudioBit) r.renderingLayerMask = WorldBit;
    }

    // Unity's trilight ambient as SH: equator everywhere + sky lobe up + ground lobe down (colours in the active space)
    public static SphericalHarmonicsL2 TrilightSH(Color sky, Color eq, Color gr)
    {
        Color s = sky.linear, e = eq.linear, g = gr.linear;
        if (!U6.Linear) { s = sky; e = eq; g = gr; }
        var sh = new SphericalHarmonicsL2();
        sh.Clear();
        sh.AddAmbientLight(e);
        sh.AddDirectionalLight(Vector3.up, (s - e) * 0.9f, 1f);
        sh.AddDirectionalLight(Vector3.down, (g - e) * 0.9f, 1f);
        return sh;
    }
}
