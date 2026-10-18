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
// URL knobs (no rebuild): u6fog=0 (flat fog) u6pl=0 (no Built-in point light match) u6plk=<x> u6exp=<EV bias> u6tm=aces|neutral|none u6ssao=0|1 u6bloom=<scale> u6msaa=1|2|4 u6sat=<+-> u6con=<+->
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
    public static float WorldSat = 0f, SatBase = 4f, ExpBias = 1.25f, BloomScale = 1f, SatAdd = 0f, ConAdd = 0f;
    // ffu6e: brighter + warmer (6.6 read ~0.6x as bright as 2022.3 everywhere): +0.9 EV, sat -6 -> +4, white balance 4 -> 9 (+ per world)
    public static float WorldWarm = 0f, WarmBase = 9f, WarmAdd = 0f, WorldCon = 0f;   // ffu6k WorldCon: per-world contrast (Light55)
    const float ExpBias0 = 1.25f;
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
        if (F("u6warm", out f)) WarmAdd = f;
        string s = Q("u6tm"); if (s == "neutral") Tm = TonemappingMode.Neutral; else if (s == "none") Tm = TonemappingMode.None;
        if (Q("u6fog") == "0") FogOn = false;
        if (F("u6skyev", out f)) SkyEv = f;
        if (F("u6skysat", out f)) SkySat = f;
        if (F("u6wisp", out f)) SkyWisp = f;
        if (Q("u6sun") == "0") SkySun = 0f;
        if (F("u6macro", out f)) MacroAmp = f;
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
            ColorAdjustments ca; if (studio.TryGet(out ca)) { ca.active = true; ca.postExposure.Override(0.675f + (ExpBias - ExpBias0) * 0.5f); }   // studio (phone / showroom) unchanged by the ffu6e lift
            Tonemapping tm; if (studio.TryGet(out tm)) { tm.active = Tm != TonemappingMode.None; tm.mode.Override(Tm); }
        }
        if (a != null) { a.shadowCascadeCount = 2; a.cascade2Split = 0.25f; }
        var go = new GameObject("U6Runner");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<U6Runner>();
        U6Lights.Init();
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

    static float lastExp = -99f, lastSat = -99f, lastWarm = -99f, lastCon = -99f;
    // game grade: RealPost's / LBPost's exposure (Light55 moves it per world) -> URP post exposure; ACES; light bloom; vignette
    public static void Grade(float exposure)
    {
        var p = GameProfile; if (p == null) return;
        float ev = ExpBias + Mathf.Log(Mathf.Max(0.05f, exposure) / 0.84f, 2f);
        if (Mathf.Abs(ev - lastExp) < 0.002f && Mathf.Abs(WorldSat - lastSat) < 0.01f && Mathf.Abs(WorldWarm - lastWarm) < 0.01f && Mathf.Abs(WorldCon - lastCon) < 0.01f) return;
        lastExp = ev; lastSat = WorldSat; lastWarm = WorldWarm; lastCon = WorldCon;
        Tonemapping tm; if (p.TryGet(out tm)) { tm.active = Tm != TonemappingMode.None; tm.mode.Override(Tm); }
        ColorAdjustments ca;
        if (p.TryGet(out ca)) { ca.active = true; ca.postExposure.Override(ev); ca.saturation.Override(SatBase + SatAdd + WorldSat); ca.contrast.Override(12f + ConAdd + WorldCon);   /* ffu6f: 8 -> 12 with the brighter grade */ }
        Bloom b; if (p.TryGet(out b)) { b.active = BloomScale > 0f; b.intensity.Override(0.32f * BloomScale); b.threshold.Override(1.05f); b.scatter.Override(0.6f); b.highQualityFiltering.Override(!Look.Mobile); }
        WhiteBalance wb; if (p.TryGet(out wb)) { wb.active = true; wb.temperature.Override(Mathf.Clamp(WarmBase + WarmAdd + WorldWarm, -100f, 100f)); }
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
    static string lastTag = ""; static int lastCasc = -1; static float lastSplit = -1f;

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
        // ffu6i: mirror the per-tier cascade count too (Perf60: 1 cascade on phones / tier med-low / split views, 2 on desktop high);
        // the asset was fixed at 2 cascades -> phones and split views paid for a second shadow pass the 2022.3 build never drew
        int casc = QualitySettings.shadowCascades >= 2 ? 2 : 1; float split = Mathf.Clamp(QualitySettings.shadowCascade2Split, 0.05f, 0.6f);
        if (casc != lastCasc || Mathf.Abs(split - lastSplit) > 0.001f) { lastCasc = casc; lastSplit = split; a.shadowCascadeCount = casc; a.cascade2Split = split; }
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
        string tag = "views " + views + " msaa " + msaa + " shadows " + q + " " + res + "/" + dist.ToString("0") + " x" + lastCasc + " tier " + Perf60.TierName;
        if (tag != lastTag) { lastTag = tag; Debug.Log("U6: " + tag); }
    }

    static bool On<T>(Camera c) where T : Behaviour { var b = c.GetComponent<T>(); return b != null && b.enabled; }

    // ffu6d: URP reads RenderSettings fog (unity_FogParams / unity_FogColor / FOG_* keywords) ONCE per frame, before any camera's
    // beginCameraRendering, but Worlds.PreCull sets fog PER CAMERA (underwater murk, Mars dust, house / Moon / space none) -> every
    // camera rendered with whatever fog the previous frame ended on (underwater came out clear and bright). Re-publish per camera.
    static readonly int idFogP = Shader.PropertyToID("unity_FogParams"), idFogC = Shader.PropertyToID("unity_FogColor");
    static void Fog(Camera c)
    {
        bool on = RenderSettings.fog;
        float d = on ? RenderSettings.fogDensity : 0f;
        float st = RenderSettings.fogStartDistance, en = RenderSettings.fogEndDistance;
        float lz = on && RenderSettings.fogMode == FogMode.Linear ? -1f / Mathf.Max(0.01f, en - st) : 0f;
        float lw = on && RenderSettings.fogMode == FogMode.Linear ? en / Mathf.Max(0.01f, en - st) : 1f;
        Shader.SetGlobalVector(idFogP, new Vector4(d * 1.2011224f, d * 1.4426950f, lz, lw));
        Shader.SetGlobalColor(idFogC, RenderSettings.fogColor);
        if (on)
        {
            FogMode m = RenderSettings.fogMode;
            if (m == FogMode.ExponentialSquared) Shader.EnableKeyword("FOG_EXP2"); else Shader.DisableKeyword("FOG_EXP2");
            if (m == FogMode.Exponential) Shader.EnableKeyword("FOG_EXP"); else Shader.DisableKeyword("FOG_EXP");
            if (m == FogMode.Linear) Shader.EnableKeyword("FOG_LINEAR"); else Shader.DisableKeyword("FOG_LINEAR");
        }
    }

    // ffu6c visual pass: outdoors (the ranch haze: exp2 fog 0.001..0.0055 / m, below 300 m, not the Real Room) the FF shaders'
    // LBFog switches from Unity's flat exp2 fog to exponential HEIGHT fog integrated along the view ray + Henyey-Greenstein
    // sun glow (Lambo u6d _LBAtmos globals; FFURP.hlsl LBFogAmount). Same colour and roughly the same distance as before at
    // ground level, thinner up the hills / in the sky, warm in-scatter towards the sun. u6fog=0 = old flat fog.
    public static bool FogOn = true;
    static readonly int idAtm = Shader.PropertyToID("_LBAtmos"), idAtmC = Shader.PropertyToID("_LBAtmosCol"), idAtmS = Shader.PropertyToID("_LBAtmosSun");
    static bool atmLogged;
    static void Atmos(Camera c)
    {
        bool on = FogOn && RenderSettings.fog && RenderSettings.fogMode == FogMode.ExponentialSquared && RenderSettings.fogDensity > 0.001f &&
                  RenderSettings.fogDensity < 0.0055f && c.transform.position.y < 300f && c.cullingMask != (1 << RealRoom.Layer) &&
                  c.GetComponent<ShowroomGrade>() == null;
        if (!on) { Shader.SetGlobalVector(idAtm, Vector4.zero); return; }
        Color fc = RenderSettings.fogColor; if (Linear) fc = fc.linear;
        Shader.SetGlobalVector(idAtm, new Vector4(1f, RenderSettings.fogDensity * 0.45f, 0.011f, 0f));   // ffu6f: 0.6 -> 0.45 (the ffu6e +0.9 EV lift made the haze milky)
        Shader.SetGlobalVector(idAtmC, new Vector4(fc.r, fc.g, fc.b, 0.95f));
        Shader.SetGlobalVector(idAtmS, new Vector4(0.34f, 0.26f, 0.16f, 0.62f));
        if (!atmLogged) { atmLogged = true; Debug.Log("U6: height fog + sun glow on (density " + RenderSettings.fogDensity.ToString("0.0000") + ")"); }
    }

    // ffu6j FF/Sky globals (x exposure mult, y zenith chroma, z high wisps, w sun disc). The sky gets its own -0.6 EV trim (the
    // game grade's +EV lift washed the HDRI to white); wisps only on the full tier (phones / Tesla: 0 = no noise maths).
    // Knobs u6skyev=<EV> u6skysat=<x> u6wisp=<0..1> u6sun=0.
    public static float SkyEv = -0.6f, SkySat = 2.6f, SkyWisp = -1f, SkySun = 1f;
    static readonly int idSky = Shader.PropertyToID("_FFSky"), idMacro = Shader.PropertyToID("_FFMacro");
    public static float MacroAmp = -1f;   // ffu6k FF/Std macro variation amplitude (-1 = by tier); knob u6macro=<x>
    static bool skyLogged;
    static void SkyGlobals()
    {
        float w = SkyWisp >= 0f ? SkyWisp : (Realism.Lite || Look.Mobile || Perf60Low() ? 0f : 0.5f);
        Shader.SetGlobalVector(idSky, new Vector4(Mathf.Pow(2f, SkyEv), SkySat, w, SkySun));
        if (!skyLogged) { skyLogged = true; Debug.Log("U6: FF/Sky ev " + SkyEv + " chroma " + SkySat + " wisps " + w + " sun " + SkySun + " (sky shader " + (RenderSettings.skybox != null && RenderSettings.skybox.shader != null ? RenderSettings.skybox.shader.name : "none") + ")"); }
    }
    static bool Perf60Low() { return Perf60.On && Perf60.Tier < 2; }

    // per camera, just before it renders (after the game's own pre-cull hooks): the trilight ambient as SH and the camera's
    // post exposure
    public static void BeforeCamera(Camera c)
    {
        if (RenderSettings.ambientMode == AmbientMode.Trilight)
            RenderSettings.ambientProbe = U6Layers.TrilightSH(RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor, RenderSettings.ambientGroundColor);
        Fog(c);
        Atmos(c);
        SkyGlobals();
        Shader.SetGlobalVector(idMacro, new Vector4(MacroAmp >= 0f ? MacroAmp : (Perf60.On && Perf60.Tier < 1 ? 0f : 0.14f), 0f, 0f, 0f));
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
    public const uint WorldBit = 1u;
    // ffu6h: Unity 6 drops undefined rendering layers from lights (BuildScript.DefineRenderingLayers defines bit 2). If bit 2 is
    // still undefined in this player, studio + world share bit 0 and the split relies on Light.cullingMask + Showroom's
    // per-camera world-sun switch-off (logged once).
    static uint studioBit;
    public static uint StudioBit
    {
        get
        {
            if (studioBit == 0u)
            {
                uint def = RenderingLayerMask.GetDefinedRenderingLayersCombinedMaskValue();
                studioBit = (def & 4u) != 0u ? 4u : 1u;
                Debug.Log("U6: rendering layers defined mask " + def + " -> studio bit " + studioBit + (studioBit == 1u ? " (FALLBACK: shared with world)" : ""));
            }
            return studioBit;
        }
    }
    public static void World(Light l) { Set(l, WorldBit); }
    public static void Studio(Light l) { Set(l, StudioBit); }
    static void Set(Light l, uint bits)
    {
        if (l == null) return;
        var d = l.GetUniversalAdditionalLightData();
        if (d != null) { d.renderingLayers = bits; d.shadowRenderingLayers = bits; }
        l.renderingLayerMask = (int)bits;   // ffu6g: URP 17 reads the light layer from Light.renderingLayerMask too
    }
    public static void Renderers(GameObject g, int layer)
    {
        var r = g.GetComponent<Renderer>();
        if (r == null) return;
        if (layer == U6.StudioVolumeLayer) r.renderingLayerMask = StudioBit;
        else if (r.renderingLayerMask == (1u << 2)) r.renderingLayerMask = WorldBit;
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

// ffu6c: Built-in -> URP point / spot light match. Built-in (Gamma) lit with its falloff texture ~ 1 / (1 + 25 (d/r)^2) on gamma
// values; URP (Linear) uses inverse square (1/d^2 x a range window) on linear values, so every lamp authored for the 2022.3 build
// (house rooms 1.7 / r 18.7, campfire, rocket glows ...) came out ~3x too dark at mid range. Per frame, just before URP culls,
// each point / spot light gets k = (0.2 I)^2.2 x 0.168 r^2 / I (matches the two at d = 0.4 r), clamped 1..8; restored after the
// frame so game scripts always read / animate their own values. u6pl=0 disables, u6plk= scales k.
public static class U6Lights
{
    static readonly List<Light> lights = new List<Light>();
    static readonly List<float> saved = new List<float>();
    static int nextScan = -1; static bool on = true; static float kScale = 1f; static bool logged;
    public static void Init()
    {
        string s = U6.Q("u6pl"); if (s == "0") on = false;
        s = U6.Q("u6plk"); float f; if (s != null && float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f)) kScale = f;
        if (!on) { Debug.Log("U6: point light match off"); return; }
        RenderPipelineManager.beginContextRendering += Begin;
        RenderPipelineManager.endContextRendering += End;
    }
    public static float K(float I, float r)
    {
        if (I <= 0f) return 1f;
        float k = Mathf.Pow(0.2f * I, 2.2f) * 0.168f * r * r / I * kScale;
        return Mathf.Clamp(k, 1f, 8f);
    }
    static void Begin(ScriptableRenderContext ctx, List<Camera> cams)
    {
        try
        {
            if (Time.frameCount >= nextScan)
            {
                nextScan = Time.frameCount + 90;
                lights.Clear();
                foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Point || l.type == LightType.Spot) lights.Add(l);
                if (!logged && lights.Count > 0)
                {
                    logged = true; string ex = "";
                    for (int i = 0; i < lights.Count && i < 4; i++) ex += " " + lights[i].name + " " + lights[i].intensity.ToString("0.0") + "/" + lights[i].range.ToString("0") + " x" + K(lights[i].intensity, lights[i].range).ToString("0.0");
                    Debug.Log("U6: point light match " + lights.Count + " lights:" + ex);
                }
            }
            saved.Clear();
            for (int i = 0; i < lights.Count; i++)
            {
                var l = lights[i];
                if (l == null || !l.isActiveAndEnabled) { saved.Add(-1f); continue; }
                float I = l.intensity; saved.Add(I);
                l.intensity = I * K(I, l.range);
            }
        }
        catch (System.Exception e) { Debug.LogException(e); }
    }
    static void End(ScriptableRenderContext ctx, List<Camera> cams)
    {
        for (int i = 0; i < saved.Count && i < lights.Count; i++)
            if (saved[i] >= 0f && lights[i] != null) lights[i].intensity = saved[i];
        saved.Clear();
    }
}
