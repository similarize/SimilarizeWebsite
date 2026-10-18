using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// ffu55 LIGHT + MATERIAL PASS (C4, C7). One light model for every world, built-in 2022.3 only, WebGL friendly:
//  * ONE SUN: the ranch sun (Realism's HDRI-matched sun) is the reference; every world scales it by its real sunlight
//    (Mars ~0.6, Callisto ~0.45 after the eye adapts, the Moon a bit harsher + white, under water blue-green and weaker,
//    inside the house only the daylight through the windows). The house takes the ranch sky's ambient (same time of day)
//    instead of its own hand-tuned warm grey; RealPost exposure adapts per space (indoors a little brighter).
//  * Shadows per tier: desktop single view 70 m, 2 cascades; phones single view Medium res 40 m hard shadows.
//  * Reflection probes: box-projected probes in the garage and in the house (rendered once, time-sliced), so the hero
//    cars' brushed stainless / paint reflect the room they stand in instead of the open sky.
//  * Hero car materials: brushed stainless = metallic 1 / smoothness 0.62 (satin streaks, not chrome, not flat grey);
//    Ano White / paint = clear-coat smoothness 0.93; tyres = rubber 0.3. Per-car clones, so nothing else changes.
//  * House interior (C4): a real ceiling at the shell's ground-floor height (4.5 m = Layout.HouseH / 2), walls up to
//    it, windows where the outside shell has them, ceiling lamps per room with working LIGHT SWITCHES (froggies and
//    robots - PLAY > Lights on / off); the camera now stays inside the walls and under the ceiling.
//  * The Real Room camera lights Standard-shader guests (robots) with the room's own lamp + exposure (they were lit
//    by the ranch sun and then pushed through the room's x4 exposure = washed out).
//  ?light=old : everything as before ffu55 (old dollhouse house, old per-world light, no probes / material tune).
public static class Light55
{
    static int on = -1;
    public static bool On { get { if (on < 0) on = Ffu33.Url("light") == "old" ? 0 : 1; return on == 1; } }
    public const float HouseH = Layout.HouseH * 0.5f;   // 4.5 m: the shell's ground floor (first floor slab at mid height)

    static Light sun; static float sunI0 = -1f; static Color sunC0; static float shadowS0;
    static Color amb0, amb1, amb2;
    static readonly Dictionary<Camera, RealPost> posts = new Dictionary<Camera, RealPost>();
    static readonly Dictionary<RealPost, float> postExp0 = new Dictionary<RealPost, float>();

    // Worlds.Init (after Look / Realism set the ranch sun + ambient)
    public static void Capture()
    {
        sun = RenderSettings.sun;
        if (sun != null) { sunI0 = sun.intensity; sunC0 = sun.color; shadowS0 = sun.shadowStrength; }
        amb0 = RenderSettings.ambientSkyColor; amb1 = RenderSettings.ambientEquatorColor; amb2 = RenderSettings.ambientGroundColor;
        if (!On) return;
        QualitySettings.pixelLightCount = Look.Mobile ? 1 : 2;   // house / Real Room lamps are ForcePixel anyway
        var go = new GameObject("Light55"); Object.DontDestroyOnLoad(go); go.AddComponent<Light55Runner>();
        U6Cam.PostRender += PostRender;
        Debug.Log("LIGHT55 on: sun " + sunI0.ToString("0.00") + " " + sunC0 + " tier " + (Look.Mobile ? "mobile" : "desktop"));
    }

    // per-world sun: (intensity scale, colour); the ranch / space keep the reference sun
    public static float SunK(WorldId w)
    {
        switch (w)
        {
            case WorldId.Mars: return 0.62f;
            case WorldId.Callisto: return 0.45f;
            case WorldId.Moon: return 1.12f;
            case WorldId.Underwater: return 0.55f;
            case WorldId.House: return 0.18f;
            default: return 1f;
        }
    }
    static Color SunCol(WorldId w)
    {
        switch (w)
        {
            case WorldId.Mars: return new Color(1f, 0.86f, 0.72f);      // dust-filtered
            case WorldId.Callisto: return new Color(1f, 0.98f, 0.95f);
            case WorldId.Moon: return new Color(1f, 1f, 0.98f);         // no air: white sunlight
            case WorldId.Underwater: return new Color(0.72f, 0.95f, 1f);
            case WorldId.House: return Color.Lerp(baseC, new Color(1f, 0.95f, 0.88f), 0.4f);
            default: return baseC;
        }
    }
    static float ExpK(WorldId w) { return w == WorldId.House ? 1.18f : w == WorldId.Mars ? 1.04f : w == WorldId.Callisto ? 1.1f : 1f; }

    // Worlds.PreCull, after the world's fog / ambient (and Realism's ranch SH) are set - every camera sets the sun
    // for its own world, so split views in different worlds never leak each other's light
    public static void PreCull(Camera c, WorldId w)
    {
        if (!On || sun == null || sunI0 < 0f) return;
        // the sun as the game left it (story storm dimming, garden demo dusk ...) is this camera's reference; PostRender
        // puts it back after the camera, so scripts between frames always see and change the reference value
        if (!applied) { baseI = sun.intensity; baseC = sun.color; baseS = sun.shadowStrength; applied = true; }
        if (c.cullingMask == (1 << RealRoom.Layer)) { RoomPreCull(); return; }
        sun.intensity = baseI * SunK(w);
        sun.color = w == WorldId.Ranch || w == WorldId.Space ? baseC : SunCol(w);
        sun.shadowStrength = w == WorldId.Moon ? 0.97f : w == WorldId.Mars ? 0.8f : baseS;
        if (w == WorldId.House)
        {
            // indoors: the ranch sky's light (same time of day) through the windows + the room lamps (Light55House)
            // 55b: the lamp fill follows the switch of the room the camera is in (one global ambient, so per camera)
            float lit = Light55House.I != null ? Light55House.I.LitAt(c.transform.position) : 1f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            Color lamp = new Color(0.36f, 0.31f, 0.24f) * lit;
            RenderSettings.ambientSkyColor = amb0 * 0.32f + lamp;
            RenderSettings.ambientEquatorColor = amb1 * 0.32f + lamp * 0.9f;
            RenderSettings.ambientGroundColor = amb2 * 0.4f + lamp * 0.6f;
        }
        else if (w == WorldId.Mars)
        {
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.45f, 0.34f); RenderSettings.ambientEquatorColor = new Color(0.46f, 0.32f, 0.24f); RenderSettings.ambientGroundColor = new Color(0.24f, 0.15f, 0.1f);
        }
        RealPost rp;
        if (!posts.TryGetValue(c, out rp)) { rp = c.GetComponent<RealPost>(); posts[c] = rp; }
        if (rp != null)
        {
            float e0;
            if (!postExp0.TryGetValue(rp, out e0)) { e0 = rp.exposure; postExp0[rp] = e0; }
            rp.exposure = e0 * ExpK(w);
        }
    }

    static bool applied; static float baseI, baseS; static Color baseC;
    static void PostRender(Camera c)
    {
        if (!applied || sun == null) return;
        sun.intensity = baseI; sun.color = baseC; sun.shadowStrength = baseS; applied = false;
    }

    // ---- the Real Room camera: Standard-shader guests (robots, their props) see the room's lamp, not the ranch sun
    static Vector3 rrLamp; static float rrLampK = 1f, rrExpo = 1f; static Vector3 rrTv; static Light rrLight;
    public static void RoomState(Vector3 lampPos, float lampK, float postExpo, Vector3 tv)
    {
        rrLamp = lampPos; rrLampK = lampK; rrExpo = Mathf.Max(1f, postExpo); rrTv = tv;
        if (!On) return;
        if (rrLight == null)
        {
            var g = new GameObject("RRLamp55");
            rrLight = g.AddComponent<Light>();
            rrLight.type = LightType.Point; rrLight.shadows = LightShadows.None; rrLight.renderMode = LightRenderMode.ForcePixel;
            rrLight.cullingMask = 1 << RealRoom.Layer; rrLight.range = 7f; rrLight.color = new Color(1f, 0.86f, 0.68f);
        }
        rrLight.transform.position = lampPos;
        rrLight.intensity = 1.5f * lampK / rrExpo;
        rrLight.enabled = lampK > 0.01f;
    }
    static void RoomPreCull()
    {
        sun.intensity = baseI * 0.05f / rrExpo;   // moonlight through the window
        sun.color = new Color(0.7f, 0.8f, 1f);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        float tvL = 0.2126f * rrTv.x + 0.7152f * rrTv.y + 0.0722f * rrTv.z;
        Color fill = (new Color(0.30f, 0.27f, 0.22f) * rrLampK + new Color(0.05f, 0.06f, 0.09f) + new Color(0.12f, 0.16f, 0.22f) * tvL * 0.25f) / rrExpo;
        RenderSettings.ambientSkyColor = fill; RenderSettings.ambientEquatorColor = fill * 0.85f; RenderSettings.ambientGroundColor = fill * 0.6f;
    }

    // Look.ApplyViews: shadow distance / cascades per tier
    public static void Quality(int views)
    {
        if (!On || views >= 3) return;
        if (Look.Mobile)
        {
            if (views == 1) { QualitySettings.shadowResolution = ShadowResolution.Medium; QualitySettings.shadowDistance = 40f; QualitySettings.shadowCascades = 1; }
        }
        else if (views == 1)
        {
            QualitySettings.shadowDistance = 70f; QualitySettings.shadowCascades = 2; QualitySettings.shadowCascade2Split = 0.22f;
        }
    }

    // ---- reflection probes (box projected, rendered once, time-sliced)
    public static ReflectionProbe Probe(string name, Vector3 centre, Vector3 size, int importance)
    {
        var go = new GameObject(name);
        go.transform.position = centre;
        var p = go.AddComponent<ReflectionProbe>();
        p.mode = ReflectionProbeMode.Realtime;
        p.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
        p.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
        p.resolution = Look.Mobile ? 64 : 128;
        p.size = size; p.boxProjection = true; p.importance = importance;
        p.clearFlags = ReflectionProbeClearFlags.Skybox; p.nearClipPlane = 0.3f; p.farClipPlane = 220f; p.hdr = true;   // ffu6
        p.blendDistance = 1.5f;
        p.RenderProbe();
        Debug.Log("LIGHT55 probe " + name + " at " + centre.ToString("0.0") + " size " + size.ToString("0") + " res " + p.resolution);
        return p;
    }

    // ---- hero car materials (Roadster49 + every VehicleFactory.Cybertruck); per-car clones
    static readonly Dictionary<Material, Material> carMat = new Dictionary<Material, Material>();
    public static int Steel, Paint, Rubber;
    public static void TuneCar(GameObject car)
    {
        if (!On || car == null) return;
        foreach (var r in car.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
            var ms = r.sharedMaterials; bool ch = false;
            for (int i = 0; i < ms.Length; i++)
            {
                var m = ms[i];
                if (m == null || m.shader == null || m.shader.name != "FF/Std" || m.renderQueue >= 2900) continue;
                Material t;
                if (!carMat.TryGetValue(m, out t))
                {
                    t = null;
                    float metal = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f;
                    float gl = m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : 0f;
                    Color c = m.HasProperty("_Color") ? m.color : Color.white;
                    float lum = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                    bool emis = m.IsKeywordEnabled("_EMISSION");
                    if (!emis)
                    {
                        if (metal >= 0.5f) { t = new Material(m); t.SetFloat("_Metallic", 1f); t.SetFloat("_Glossiness", 0.62f); t.color = new Color(Mathf.Max(c.r, 0.78f), Mathf.Max(c.g, 0.78f), Mathf.Max(c.b, 0.79f), c.a); Steel++; }
                        else if (gl >= 0.55f && lum > 0.3f) { t = new Material(m); t.SetFloat("_Glossiness", 0.93f); Paint++; }
                        else if (lum < 0.09f && gl < 0.6f) { t = new Material(m); t.SetFloat("_Glossiness", 0.3f); Rubber++; }
                    }
                    if (t != null) t.name = m.name + " (ffu55)";
                    carMat[m] = t;
                }
                if (t != null) { ms[i] = t; ch = true; }
            }
            if (ch) r.sharedMaterials = ms;
            r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbesAndSkybox;
        }
    }

    // robots (PLAY > Lights on / off): the nearest switch in the robot's world, or "" when there is none
    public static bool SwitchNear(WorldId w, Vector3 p, out Vector3 at)
    {
        at = p;
        if (!On) return false;
        if (w == WorldId.House && Light55House.I != null) return Light55House.I.Nearest(p, out at);
        if (w == WorldId.RealRoom && RealRoom.I != null) { at = RealRoom.SwitchPos55; return true; }
        return false;
    }
    public static string RobotFlip(WorldId w, Vector3 p)
    {
        if (w == WorldId.House && Light55House.I != null) return Light55House.I.FlipNearest(p, null);
        if (w == WorldId.RealRoom && RealRoom.I != null) { RealRoom.I.ToggleLights(); return RealRoom.I.LightsOn55 ? "lights on!" : "lights off - spooky!"; }
        return "no light switch here";
    }
}

// probes + car tune, once the ranch + vehicles exist (and again after Realism's HDRI sky streamed in)
public class Light55Runner : MonoBehaviour
{
    bool cars, garage; float t;
    void Update()
    {
        t += Time.unscaledDeltaTime;
        if (Game.I == null || Game.I.state != Game.State.Play) return;
        if (!cars && Roadster49.I != null && t > 2f)
        {
            cars = true;
            Light55.TuneCar(Roadster49.I.gameObject);
            int n = 0;
            foreach (var v in Vehicle.All) if (v != null && v.Title == "Cybertruck") { Light55.TuneCar(v.gameObject); n++; }
            Debug.Log("LIGHT55 hero cars tuned: Roadster + " + n + " Cybertrucks; steel " + Light55.Steel + " paint " + Light55.Paint + " rubber " + Light55.Rubber + " materials");
        }
        if (!garage && (Realism.Ready || !Realism.On || t > 90f) && t > 6f)
        {
            garage = true;
            Vector2 c = Layout.GarageC, s = Layout.GarageSize;
            Light55.Probe("GarageProbe55", new Vector3(c.x, Layout.GarageH * 0.5f, c.y), new Vector3(s.x + 2f, Layout.GarageH + 1f, s.y + 2f), 2);
            enabled = false;
        }
    }
}

// the house interior's ceiling lamps + light switches (built by HouseWorld when Light55.On)
public class Light55House : MonoBehaviour
{
    public static Light55House I;
    public static float LitFrac = 1f;
    class Room { public string name; public Rect r; public Light lamp; public Renderer bulb; public bool on = true; public Vector3 sw; public Transform rocker; }
    readonly List<Room> rooms = new List<Room>();
    static Material bulbOn, bulbOff;

    public static void Build(Transform root, Vector3 o, float H, Material trim)
    {
        var go = new GameObject("HouseLights55"); go.transform.SetParent(root, false);
        I = go.AddComponent<Light55House>();
        bulbOn = Mats.Unlit(new Color(1f, 0.93f, 0.78f)); bulbOff = Mats.Lit(new Color(0.75f, 0.74f, 0.7f));
        // (name, rect x, z, w, h in house-local metres, switch x, z (beside the doorway on the z = 4 wall))
        I.Add(root, o, H, "LIVING ROOM", new Rect(-30f, 4f, 24f, 16f), new Vector2(-15.4f, 4.2f), trim);
        I.Add(root, o, H, "FOYER", new Rect(-6f, 4f, 12f, 16f), new Vector2(2.6f, 4.2f), trim);
        I.Add(root, o, H, "KITCHEN", new Rect(6f, 4f, 24f, 16f), new Vector2(20.6f, 4.2f), trim);
        I.Add(root, o, H, "PET PARLOR", new Rect(-30f, -20f, 20f, 24f), new Vector2(-15.4f, 3.8f), trim);
        I.Add(root, o, H, "FISH GALLERY", new Rect(-10f, -20f, 20f, 24f), new Vector2(2.6f, 3.8f), trim);
        I.Add(root, o, H, "REPTILE ROOM", new Rect(10f, -20f, 20f, 24f), new Vector2(20.6f, 3.8f), trim);
        LitFrac = 1f;
        Debug.Log("LIGHT55 house: ceiling " + H + " m, " + I.rooms.Count + " rooms with lamps + switches");
    }

    void Add(Transform root, Vector3 o, float H, string name, Rect r, Vector2 sw, Material trim)
    {
        var rm = new Room { name = name, r = r };
        Vector3 c = o + new Vector3(r.center.x, H - 0.75f, r.center.y);
        var lg = new GameObject("Lamp " + name); lg.transform.SetParent(transform, false); lg.transform.position = c;
        rm.lamp = lg.AddComponent<Light>();
        rm.lamp.type = LightType.Point; rm.lamp.shadows = LightShadows.None; rm.lamp.renderMode = Perf60.On ? LightRenderMode.Auto : LightRenderMode.ForcePixel;   // ffu60: Auto (pixelLightCount per tier) - 6 forced pixel lamps = up to 6 extra passes per object
        rm.lamp.range = Mathf.Max(r.width, r.height) * 0.78f; rm.lamp.intensity = 1.7f; rm.lamp.color = new Color(1f, 0.88f, 0.72f);
        // flush ceiling light (disc) + its rim
        var b = Mats.Prim(PrimitiveType.Cylinder, transform, o + new Vector3(r.center.x, H - 0.05f, r.center.y), new Vector3(1.4f, 0.04f, 1.4f), bulbOn);
        Mats.NoShadows(b); rm.bulb = b.GetComponent<Renderer>();
        var rim = Mats.Prim(PrimitiveType.Cylinder, transform, o + new Vector3(r.center.x, H - 0.03f, r.center.y), new Vector3(1.6f, 0.03f, 1.6f), trim);
        Mats.NoShadows(rim);
        // switch plate + rocker on the wall (faces +z on the north side of the z = 4 wall, -z on the south side)
        float face = sw.y > 4f ? 1f : -1f;
        Vector3 sp = o + new Vector3(sw.x, 1.25f, sw.y);
        var plate = Mats.Prim(PrimitiveType.Cube, transform, sp, new Vector3(0.16f, 0.24f, 0.04f), Mats.Lit(new Color(0.96f, 0.95f, 0.92f)));
        Mats.NoShadows(plate);
        rm.rocker = Mats.Node(transform, "Rocker", sp + new Vector3(0f, 0f, face * 0.03f));
        Mats.NoShadows(Mats.Prim(PrimitiveType.Cube, rm.rocker, Vector3.zero, new Vector3(0.07f, 0.11f, 0.03f), Mats.Lit(new Color(0.9f, 0.88f, 0.84f))));
        rm.rocker.localRotation = Quaternion.Euler(-12f * face, 0f, 0f);
        rm.sw = sp;
        var hs = Interact.Add(new Vector3(sp.x, o.y + 0.1f, sp.z + face * 0.6f), 1.5f, "flip the light switch", f => Flip(rm, f));
        hs.enabled = f => f.world == WorldId.House;
        hs.dynLabel = f => rm.on ? "lights OFF in the " + rm.name : "lights ON in the " + rm.name;
        rooms.Add(rm);
    }

    void Flip(Room rm, Frog f)
    {
        rm.on = !rm.on;
        rm.lamp.enabled = rm.on;
        rm.bulb.sharedMaterial = rm.on ? bulbOn : bulbOff;
        float face = rm.rocker.position.z > Worlds.HouseO.z + 4f ? 1f : -1f;
        rm.rocker.localRotation = Quaternion.Euler((rm.on ? -12f : 12f) * face, 0f, 0f);
        Sfx.Play(Sfx.Click != null ? Sfx.Click : Sfx.Toggle, 0.9f);
        int n = 0; foreach (var r in rooms) if (r.on) n++;
        LitFrac = rooms.Count > 0 ? n / (float)rooms.Count : 1f;
        string line = rm.on ? "click! lights on in the " + rm.name : (n == 0 ? "click! ALL the lights are off - it's dark in here!" : "click! lights off in the " + rm.name + " - spooky!");
        if (f != null) f.Toast(line, 2.2f);
        Debug.Log("LIGHT55 switch " + rm.name + " " + (rm.on ? "ON" : "OFF") + " by " + (f != null ? f.nick : "robot") + " lit " + n + "/" + rooms.Count);
    }

    public bool Nearest(Vector3 p, out Vector3 at)
    {
        at = p; float bd = 1e9f; Room best = null;
        foreach (var r in rooms) { float d = (r.sw - p).sqrMagnitude; if (d < bd) { bd = d; best = r; } }
        if (best == null) return false;
        float face = best.sw.z > Worlds.HouseO.z + 4f ? 1f : -1f;
        at = new Vector3(best.sw.x, Worlds.HouseO.y, best.sw.z + face * 0.7f);
        return true;
    }
    public string FlipNearest(Vector3 p, Frog f)
    {
        float bd = 1e9f; Room best = null;
        foreach (var r in rooms) { float d = (r.sw - p).sqrMagnitude; if (d < bd) { bd = d; best = r; } }
        if (best == null) return "no light switch here";
        Flip(best, f);
        return best.on ? "lights on in the " + best.name + "!" : "lights off in the " + best.name + " - spooky!";
    }
    // 1 = the room around p has its lamp on, 0 = off (outside every room: the share of lit rooms)
    public float LitAt(Vector3 p)
    {
        Vector3 l = p - Worlds.HouseO;
        foreach (var r in rooms) if (r.r.Contains(new Vector2(l.x, l.z))) return r.on ? 1f : 0f;
        return LitFrac;
    }
    public bool AnyOff { get { foreach (var r in rooms) if (!r.on) return true; return false; } }
    // demo / probe
    public void DemoSet(bool on, Frog f) { foreach (var r in rooms) if (r.on != on) Flip(r, f); }
    public void DemoFlip(int i, Frog f) { if (i >= 0 && i < rooms.Count) Flip(rooms[i], f); }
}
