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
        Mobile = Application.isMobilePlatform || LBDisplay.MobileUA || (Input.touchSupported && Mathf.Min(Screen.width, Screen.height) < 900 && Screen.dpi > 200f);
        QualitySettings.realtimeReflectionProbes = true;
        QualitySettings.softParticles = false;
        // lambo8: anisotropic filtering everywhere (road / ground at grazing angles were the worst blur + shimmer); phones per texture
        QualitySettings.anisotropicFiltering = Mobile ? AnisotropicFiltering.Enable : AnisotropicFiltering.ForceEnable;
        Texture.SetGlobalAnisotropicFilteringLimits(Mobile ? 4 : 8, 16);
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
            island = MakeProbe("IslandProbe", islandCentre + Vector3.up * 14f, new Vector3(2400f, 900f, 2400f), ~excludeMask, Mobile ? 256 : 512);   // lambo9: sharper clear-coat reflections
            island.hdr = Gfx6.On && !Mobile;                                             // u6d: HDR sky / sun in the paint
            island.RenderProbe();
            // lambo7: the showroom probe sees the studio (softbox panels + dark set on layer 20) so the paint reflects it
            showroom = MakeProbe("ShowroomProbe", Showroom.EnvPos + new Vector3(0f, 1.6f, 0f), new Vector3(1200f, 600f, 1200f), excludeMask, Mobile ? 128 : 256);
            showroom.clearFlags = ReflectionProbeClearFlags.SolidColor;
            showroom.backgroundColor = new Color(0.08f, 0.09f, 0.1f);     // u6d: brighter studio (was 0.035)
            showroom.hdr = true;                                             // u6d: HDR softboxes -> real highlights in the clear coat
            showroom.nearClipPlane = 0.3f;
            showroom.farClipPlane = 60f;
            showroom.RenderProbe();
            Debug.Log("Look: reflection probes rendered");
        }
        catch (System.Exception e) { Debug.LogWarning("Look: probes failed " + e.Message); }
    }

    // per-track lighting: sun, ambient, fog and sky tint (0 = Coconut Cove's original look)
    static Material skyMat;
    static Texture skyBase;
    public static int Theme;
    // sky exposure / rotation per theme (sun azimuth of each panorama vs the theme's sun light)
    static readonly float[] SkyExp = { 1f, 0.8f, 0.85f, 0.78f, 0.85f, 0.82f };   // lambo7c: sky jpgs also de-haloed (lbgfx2/sky7b/dehalo.py); lambo7b: the new panoramas read washed-out at 1.0 under the filmic grade
    // lambo7b: +180 on the new skies - with the sky sun matched to the sun light the start straights looked straight into a
    // white sun glow (probe A-T1 / A-T3); the panorama sun now sits behind the grid, the sky in view stays blue
    static readonly float[] SkyRot = { 18f, 174f, 256f, 214f, 211f, 124f };   // R = sunAz - (u_sun - 0.25) * 360 (measured, lb-gfx/make_theme_tex.py)
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
            case 1: sunC = new Color(1f, 0.87f, 0.68f); sunI = 1.15f; sunE = new Vector3(40f, -60f, 0f); sky = new Color(0.5f, 0.48f, 0.52f); eq = new Color(0.58f, 0.46f, 0.36f); gr = new Color(0.5f, 0.32f, 0.22f); fog = new Color(0.8f, 0.64f, 0.5f); fogD = 0.0013f; tint = new Color(0.56f, 0.5f, 0.44f); break;
            case 2: sunC = new Color(1f, 0.68f, 0.48f); sunI = 1.2f; sunE = new Vector3(34f, 30f, 0f); sky = new Color(0.46f, 0.36f, 0.36f); eq = new Color(0.52f, 0.36f, 0.28f); gr = new Color(0.6f, 0.28f, 0.1f); fog = new Color(0.42f, 0.28f, 0.22f); fogD = 0.0022f; tint = new Color(0.44f, 0.3f, 0.26f); exp = 0.9f; break;
            case 3: sunC = new Color(0.96f, 0.97f, 1f); sunI = 1.1f; sunE = new Vector3(36f, -20f, 0f); sky = new Color(0.46f, 0.54f, 0.68f); eq = new Color(0.52f, 0.56f, 0.62f); gr = new Color(0.5f, 0.52f, 0.58f); fog = new Color(0.68f, 0.77f, 0.9f); fogD = 0.0016f; tint = new Color(0.5f, 0.53f, 0.58f); exp = 1.1f; break;
            case 4: sunC = new Color(1f, 0.95f, 0.8f); sunI = 1.05f; sunE = new Vector3(55f, -70f, 0f); sky = new Color(0.4f, 0.5f, 0.52f); eq = new Color(0.4f, 0.48f, 0.36f); gr = new Color(0.26f, 0.32f, 0.2f); fog = new Color(0.5f, 0.62f, 0.54f); fogD = 0.0025f; tint = new Color(0.46f, 0.52f, 0.46f); break;
            case 5: sunC = new Color(1f, 0.9f, 0.8f); sunI = 1.1f; sunE = new Vector3(32f, -110f, 0f); sky = new Color(0.44f, 0.5f, 0.64f); eq = new Color(0.5f, 0.5f, 0.52f); gr = new Color(0.35f, 0.35f, 0.38f); fog = new Color(0.72f, 0.78f, 0.88f); fogD = 0.0011f; tint = new Color(0.5f, 0.52f, 0.56f); exp = 1.05f; break;
        }
        if (sun != null) { sun.color = sunC; sun.intensity = sunI; sun.transform.rotation = Quaternion.Euler(sunE); }
        RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = eq; RenderSettings.ambientGroundColor = gr;
        RenderSettings.fogColor = fog; RenderSettings.fogDensity = fogD;
        // lambo7: each theme gets its own Poly Haven CC0 sky panorama (Resources/LBSky/sky<th>.jpg; the cove keeps the
        // original Kloofendal sky), so the tint is gentler than before; rotation lines the sun up with the light
        Texture2D sk = th >= 1 && th <= 5 ? Resources.Load<Texture2D>("LBSky/sky" + th) : null;
        if (skyMat != null)
        {
            if (skyBase == null && skyMat.HasProperty("_MainTex")) skyBase = skyMat.GetTexture("_MainTex");
            if (skyMat.HasProperty("_MainTex")) skyMat.SetTexture("_MainTex", sk != null ? sk : skyBase);
            if (sk != null) tint = Color.Lerp(new Color(0.5f, 0.5f, 0.5f), tint, 0.35f);
            if (skyMat.HasProperty("_Tint")) skyMat.SetColor("_Tint", tint);
            if (skyMat.HasProperty("_Exposure") && skyExposure > 0f) skyMat.SetFloat("_Exposure", skyExposure * exp * (sk != null ? SkyExp[th] : 1f));
            if (skyMat.HasProperty("_Rotation")) skyMat.SetFloat("_Rotation", sk != null ? SkyRot[th] : 18f);
        }
        LBPost.SetTheme(th);
        Theme = th;
        Gfx6.Atmos(th, fog, fogD);                                                       // u6d: height fog + sun scattering, shafts / haze
        Debug.Log("Look: theme " + th + (sk != null ? " sky " + sk.name + " " + sk.width + "x" + sk.height : ""));
    }

    // re-render the island probe after a track rebuild
    public static void RefreshIsland(Vector3 centre)
    {
        if (island == null) return;
        island.transform.position = centre + Vector3.up * 14f;
        island.RenderProbe();
    }

    public static bool PostAllowed(int views) { return views == 1; }

    // re-apply the per-level settings (LBDisplay governor / ?lbq) to the current views
    static List<Camera> lastCams;
    public static void ApplyLevel()
    {
        LBPost.Cheap = LBDisplay.Level >= 2;
        Gfx.ApplyLevel(LBDisplay.Level, Mobile);
        if (Views > 0) ApplyViews(Views, lastCams);
    }

    // called whenever the number of views changes (lobby = 0 player views)
    // lambou6: shadows / cascades / AA now live on the URP asset + per-camera data (U6); same tiers as lambo8/9:
    //  desktop 1 view: soft, 4096 / 80 m at level 0, 2048 / 50 m at level 1, hard 2048 / 50 m at level 2; 2 views hard 1024 / 30 m;
    //  phones 1 view: hard 1024 / 40 m (level 0) or 512 / 32 m; 3-4 views and phone split: no shadows. Cascades fixed at 2
    //  (one asset, no variant switching). SSAO only in one full-screen view on desktop / Tesla at level 0.
    public static void ApplyViews(int views, List<Camera> playerCams)
    {
        Views = views;
        if (playerCams != null) lastCams = playerCams;
        bool shadows = views < 3 && !(Mobile && views > 1);
        bool soft; int res; float dist;
        if (Mobile) { soft = false; res = views == 1 && LBDisplay.Level == 0 ? 1024 : 512; dist = views == 1 && LBDisplay.Level == 0 ? 40f : 32f; }
        else
        {
            soft = views <= 1 && LBDisplay.Level < 2;
            res = views <= 1 ? (LBDisplay.Level == 0 ? 4096 : 2048) : 1024;
            dist = views <= 1 ? (LBDisplay.Level == 0 ? 80f : 50f) : 30f;
        }
        U6.Shadows(soft, res, dist, sun);
        QualitySettings.lodBias = Mobile || views >= 2 ? 0.7f : 1f;
        bool post = PostAllowed(views);
        U6.SsaoOn(views == 1 && !Mobile && LBDisplay.Level == 0);
        if (playerCams != null)
            foreach (var c in playerCams)
            {
                if (c == null) continue;
                U6.CamShadows(c, shadows);
                var p = c.GetComponent<LBPost>();
                if (p != null) p.enabled = post;
            }
        LBDisplay.ApplyAA();
        Gfx6.ApplyViews(views, LBDisplay.Level, Mobile);                                 // u6d: god rays / heat haze, car probe
        Debug.Log("U6: views " + views + " level " + LBDisplay.Level + " shadows " + (shadows ? (soft ? "soft " : "hard ") + res + " / " + dist + " m" : "off") + " post " + post);
    }

    public static List<Camera> Cams { get { return lastCams; } }
}
