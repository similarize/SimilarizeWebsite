using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

// ffu60 PERFORMANCE PASS. After the realism releases (ffu52-58) the web build drew ~5,000 draw calls / ~5 M triangles a
// frame at the ranch overview (probe, SwiftShader). This file holds:
//  * QUALITY TIERS 0 low / 1 med / 2 high (desktop starts high, phones start where they ended last time, default high).
//    Per tier: pixel density cap (DPR), sun shadows (distance / cascades / resolution), grass radius + density, SSAO,
//    bloom chain, pixel lights, the small-object cull distance. Split-screen (2-4 views) scales grass / cull further.
//  * GOVERNOR: under ~50 fps for 3 s in play -> one tier down; at >= 57 fps for a while -> try one tier up (the wait
//    doubles each time a step up has to be taken back). ?quality=low|med|high pins a tier (governor off).
//  * CULL60: small / medium props (and the ffu56 leaf sprig cards) stop rendering beyond a distance that grows with
//    their size (Renderer.forceRenderingOff, owned only here), checked in slices across frames against every camera.
//  * ?fps=1: on-screen dev readout (fps, ms, tier, DPR, views, grass, culled) + a "PERF60 census" log of what the
//    first camera draws (renderers / triangles by object) every 30 s. ?perf=old: everything as ffu59 (no ffu60).
public static class Perf60
{
    static int on = -1;
    public static bool On { get { if (on < 0) on = Ffu33.Url("perf") == "old" ? 0 : 1; return on == 1; } }
    public static int Tier { get; private set; }
    public static bool Forced { get; private set; }
    public static bool ShowFps { get; private set; }
    public static bool Demo { get; private set; }
    public static float Fps = 60f, Ms = 16.7f;
    public static int Views = 1, StepsDown, StepsUp;
    public static string Last = "";
    static readonly string[] Names = { "low", "med", "high" };
    public static string TierName { get { return Names[Mathf.Clamp(Tier, 0, 2)]; } }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void FFPerfSave(int tier);
    [DllImport("__Internal")] static extern int FFPerfLoad();
#else
    static void FFPerfSave(int tier) { }
    static int FFPerfLoad() { return -1; }
#endif

    // ---------------- knobs (read by Grass56, RealPost, Look, Light55, Cull60, FFDisplay) ----------------
    public static float GrassRadiusK { get { if (!On) return 1f; float k = Tier >= 2 ? 1f : Tier == 1 ? 0.72f : 0.5f; return k * (Views == 2 ? 0.7f : Views >= 3 ? 0.5f : 1f); } }
    public static float GrassDensK { get { if (!On) return 1f; float k = Tier >= 2 ? 1f : Tier == 1 ? 0.62f : 0.38f; return k * (Views >= 2 ? 0.75f : 1f); } }
    public static bool AO { get { return !On || (Tier >= 2 && Views <= 1); } }
    public static bool BloomLite { get { return On && Tier == 0; } }
    // a prop of size s (m, largest bounds extent x2) stops rendering beyond s * CullK (min 30 m)
    public static float CullK { get { float k = Tier >= 2 ? 125f : Tier == 1 ? 90f : 60f; return k * (Views == 2 ? 0.85f : Views >= 3 ? 0.7f : 1f); } }
    public static float SmallShadowDist { get { return Tier >= 2 ? 30f : Tier == 1 ? 20f : 12f; } }
    public static float LeafCardDist { get { float d = Tier >= 2 ? 110f : Tier == 1 ? 75f : 45f; return d * (Views >= 2 ? 0.8f : 1f); } }
    public static float DprCap { get { bool m = Look.Mobile; return Tier >= 2 ? (m ? 2f : 2f) : Tier == 1 ? 1.5f : (m ? 1.2f : 1f); } }

    static List<Camera> lastCams = new List<Camera>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!On) return;
        ShowFps = Ffu33.Url("fps") == "1";
        Demo = Ffu33.Url("ffdemo") != null;
        string q = Ffu33.Url("quality");
        int t = q == "low" ? 0 : q == "med" || q == "medium" ? 1 : q == "high" ? 2 : -1;
        if (t >= 0) { Tier = t; Forced = true; }
        else
        {
            int saved = -1; try { saved = FFPerfLoad(); } catch { }
            Tier = saved >= 0 && saved <= 2 ? saved : 2;   // start where this browser ended last time (a phone that needed low starts low)
        }
        var go = new GameObject("Perf60"); Object.DontDestroyOnLoad(go); go.hideFlags = HideFlags.HideInHierarchy;
        go.AddComponent<Perf60Runner>(); go.AddComponent<Cull60>();
        Debug.Log("PERF60 on: tier " + TierName + (Forced ? " (forced by ?quality)" : " (governor)") + " mobile " + Look.Mobile + " fps readout " + ShowFps);
    }

    // Look.ApplyViews (end): remember the views / cameras, then the per-tier shadow + light settings
    public static void AfterViews(int views, IEnumerable<Camera> cams)
    {
        if (!On) return;
        Views = Mathf.Max(1, views);
        if (cams != null && !ReferenceEquals(cams, lastCams)) { lastCams.Clear(); foreach (var c in cams) if (c != null) lastCams.Add(c); }
        Shadows(Views);
        QualitySettings.pixelLightCount = Look.Mobile ? 1 : (Tier >= 2 ? 3 : Tier == 1 ? 2 : 1);   // house lamps are Auto now (ffu60)
        FFDisplay.Cap(DprCap);
    }

    static void Shadows(int views)
    {
        if (views >= 3) { QualitySettings.shadows = ShadowQuality.Disable; return; }
        if (Realism.Degraded) return;   // the phone frame-rate guard already shed them
        if (Look.Mobile)
        {
            if (views >= 2 || Tier == 0) { QualitySettings.shadows = ShadowQuality.Disable; return; }
            QualitySettings.shadows = ShadowQuality.HardOnly; QualitySettings.shadowCascades = 1;
            if (Tier >= 2) { QualitySettings.shadowResolution = ShadowResolution.Medium; QualitySettings.shadowDistance = 40f; }
            else { QualitySettings.shadowResolution = ShadowResolution.Low; QualitySettings.shadowDistance = 28f; }
            return;
        }
        if (views == 2)
        {
            if (Tier == 0) { QualitySettings.shadows = ShadowQuality.Disable; return; }
            QualitySettings.shadows = ShadowQuality.HardOnly; QualitySettings.shadowCascades = 1;
            QualitySettings.shadowResolution = Tier >= 2 ? ShadowResolution.Medium : ShadowResolution.Low;
            QualitySettings.shadowDistance = Tier >= 2 ? 30f : 22f;
            return;
        }
        if (Tier >= 2)
        {   // ffu55 look: soft, 2 cascades; 60 m (was 70: the last 10 m are a few texels of far shadow)
            QualitySettings.shadows = ShadowQuality.All; QualitySettings.shadowResolution = Realism.On && !Realism.Lite ? ShadowResolution.VeryHigh : ShadowResolution.High;
            QualitySettings.shadowDistance = 60f; QualitySettings.shadowCascades = 2; QualitySettings.shadowCascade2Split = 0.22f;
        }
        else if (Tier == 1)
        {
            QualitySettings.shadows = ShadowQuality.All; QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.shadowDistance = 45f; QualitySettings.shadowCascades = 1;
        }
        else
        {
            QualitySettings.shadows = ShadowQuality.HardOnly; QualitySettings.shadowResolution = ShadowResolution.Medium;
            QualitySettings.shadowDistance = 30f; QualitySettings.shadowCascades = 1;
        }
    }

    public static void SetTier(int t, string why)
    {
        t = Mathf.Clamp(t, 0, 2);
        if (t == Tier) return;
        if (t < Tier) StepsDown++; else StepsUp++;
        Tier = t;
        Last = why;
        Look.ApplyViews(Look.Views > 0 ? Look.Views : Views, lastCams);   // re-runs Realism / Light55 and then AfterViews
        try { FFPerfSave(Tier); } catch { }
        Debug.Log("PERF60 tier -> " + TierName + " (" + why + ") dpr cap " + DprCap.ToString("0.0#") + " shadows " + QualitySettings.shadows + " " + QualitySettings.shadowDistance + " m x" + QualitySettings.shadowCascades + " grass r x" + GrassRadiusK.ToString("0.00") + " AO " + AO);
    }

    // ---------------- census (dev): what the first camera draws ----------------
    static readonly Dictionary<string, Vector2Int> census = new Dictionary<string, Vector2Int>();
    static readonly Plane[] planes = new Plane[6];
    public static void Census(Camera cam)
    {
        if (cam == null) return;
        census.Clear();
        GeometryUtility.CalculateFrustumPlanes(cam, planes);
        Vector3 cp = cam.transform.position; float far2 = cam.farClipPlane * cam.farClipPlane;
        int nR = 0; long nT = 0;
        foreach (var r in Object.FindObjectsOfType<Renderer>())
        {
            if (!r.enabled || r.forceRenderingOff || !r.gameObject.activeInHierarchy || (cam.cullingMask & (1 << r.gameObject.layer)) == 0) continue;
            if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
            Bounds b = r.bounds;
            if (b.SqrDistance(cp) > far2 || !GeometryUtility.TestPlanesAABB(planes, b)) continue;
            int tris = 0;
            var mf = r.GetComponent<MeshFilter>();
            Mesh m = mf != null ? mf.sharedMesh : (r is SkinnedMeshRenderer ? ((SkinnedMeshRenderer)r).sharedMesh : null);
            if (m != null) for (int s = 0; s < m.subMeshCount; s++) tris += (int)m.GetIndexCount(s) / 3;
            string k = Key(r.transform);
            Vector2Int v; census.TryGetValue(k, out v);
            census[k] = new Vector2Int(v.x + 1, v.y + tris);
            nR++; nT += tris;
        }
        var l = new List<KeyValuePair<string, Vector2Int>>(census);
        l.Sort((a, b) => b.Value.y.CompareTo(a.Value.y));
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < l.Count && i < 16; i++) sb.Append(" | ").Append(l[i].Key).Append(' ').Append(l[i].Value.x).Append("r ").Append((l[i].Value.y / 1000f).ToString("0.0")).Append("kt");
        var l2 = new List<KeyValuePair<string, Vector2Int>>(census);
        l2.Sort((a, b) => b.Value.x.CompareTo(a.Value.x));
        var sb2 = new System.Text.StringBuilder();
        for (int i = 0; i < l2.Count && i < 12; i++) sb2.Append(" | ").Append(l2[i].Key).Append(' ').Append(l2[i].Value.x).Append('r');
        Debug.Log("PERF60 census " + cam.name + " at " + cp.ToString("0") + ": " + nR + " renderers, " + (nT / 1000f).ToString("0") + "k tris in view; culled props " + Cull60.Culled + "/" + Cull60.Tracked + ", small-prop shadows off " + Cull60.ShadowCulled + " | by tris" + sb + " || by count" + sb2);
    }
    static string Key(Transform t)
    {
        // the top-level object under a scene root, digits stripped ("Tree 12" -> "Tree")
        Transform a = t; int guard = 0;
        while (a.parent != null && a.parent.parent != null && guard++ < 40) a = a.parent;
        string n = a.parent == null ? a.name : a.parent.name + "/" + a.name;
        var sb = new System.Text.StringBuilder(n.Length);
        foreach (char ch in n) if (!char.IsDigit(ch)) sb.Append(ch);
        return sb.ToString().Trim();
    }
}

// the governor + the ?fps=1 readout
public class Perf60Runner : MonoBehaviour
{
    float acc, win, lowT, highT, settle = 4f, upWait = 20f, lastUp = -999f, censusT = 25f, logT;
    int n;
    string text = "";
    GUIStyle style;
    bool wasPlay;

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        bool play = Game.I != null && Game.I.state == Game.State.Play;
        if (play != wasPlay) { wasPlay = play; settle = 4f; lowT = highT = 0f; }
        if (dt > 0.5f) { acc = 0f; n = 0; win = 0f; return; }   // a hitch / hidden tab / loading: not a frame-rate reading
        acc += dt; n++; win += dt;
        if (win < 1f) return;
        Perf60.Fps = n / Mathf.Max(0.001f, acc); Perf60.Ms = 1000f * acc / Mathf.Max(1, n);
        win = 0f; acc = 0f; n = 0;
        if (Perf60.ShowFps)
            text = Perf60.Fps.ToString("0") + " fps  " + Perf60.Ms.ToString("0.0") + " ms\n" + Perf60.TierName + (Perf60.Forced ? " (fixed)" : "") + "  dpr " + FFDisplay.CurrentDpr.ToString("0.0#") + "  views " + Perf60.Views +
                   "\nshadow " + QualitySettings.shadowDistance.ToString("0") + "m x" + QualitySettings.shadowCascades + (Grass56.I != null ? "  grass " + Grass56.I.DrawnInfo : "") + "\nculled " + Cull60.Culled + "/" + Cull60.Tracked + (Perf60.AO ? "  AO" : "");
        if (play && (Perf60.ShowFps || Perf60.Demo))
        {
            logT -= 1f;
            if (logT <= 0f) { logT = 10f; Debug.Log("PERF60 " + Perf60.Fps.ToString("0.0") + " fps " + Perf60.Ms.ToString("0") + " ms tier " + Perf60.TierName + " dpr " + FFDisplay.CurrentDpr.ToString("0.0#") + " views " + Perf60.Views + " culled " + Cull60.Culled + "/" + Cull60.Tracked + (Grass56.I != null ? " grass " + Grass56.I.DrawnInfo : "")); }
            censusT -= 1f;
            if (censusT <= 0f) { censusT = 30f; Camera c = Game.I.slots.Count > 0 ? Game.I.slots[0].cam : null; if (c != null) Perf60.Census(c); }
        }
        if (!play || Perf60.Forced) { lowT = highT = 0f; return; }
        if (settle > 0f) { settle -= 1f; return; }
        float fps = Perf60.Fps;
        if (fps < 50f) { lowT += 1f; highT = 0f; }
        else if (fps >= 57f) { highT += 1f; lowT = 0f; }
        else { lowT = 0f; }
        if (lowT >= 3f && Perf60.Tier > 0)
        {
            if (Time.unscaledTime - lastUp < 12f) upWait = Mathf.Min(upWait * 2f, 320f);   // the last step up didn't hold
            Perf60.SetTier(Perf60.Tier - 1, fps.ToString("0") + " fps for 3 s");
            lowT = highT = 0f; settle = 3f;
        }
        else if (highT >= upWait && Perf60.Tier < 2)
        {
            Perf60.SetTier(Perf60.Tier + 1, fps.ToString("0") + " fps for " + upWait.ToString("0") + " s");
            lastUp = Time.unscaledTime; lowT = highT = 0f; settle = 3f;
        }
    }

    void OnGUI()
    {
        if (!Perf60.ShowFps || text.Length == 0) return;
        if (style == null) { style = new GUIStyle(GUI.skin.box) { fontSize = Mathf.RoundToInt(13 * Mathf.Max(1f, Screen.dpi / 110f)), alignment = TextAnchor.UpperLeft }; style.normal.textColor = Color.white; }
        float w = style.fontSize * 19f, h = style.fontSize * 6.2f;
        GUI.Box(new Rect(Screen.width - w - 8f, Screen.height - h - 8f, w, h), text, style);
    }
}

// CULL60: props stop rendering beyond (size x CullK) from every camera; ffu56 leaf sprig cards beyond LeafCardDist
public class Cull60 : MonoBehaviour
{
    public static int Tracked, Culled;
    class R { public Renderer r; public float size; public bool leaf; public bool off; public bool caster, shOff; }
    public static int ShadowCulled;
    readonly List<R> list = new List<R>();
    readonly HashSet<int> known = new HashSet<int>();
    readonly List<Vector3> cams = new List<Vector3>();
    int idx; float scanT = 6f;
    static int skipLayers = -1;

    void Update()
    {
        if (Game.I == null || Game.I.state != Game.State.Play) return;
        scanT -= Time.unscaledDeltaTime;
        if (scanT <= 0f) { scanT = 8f; Scan(); }
        if (list.Count == 0) return;
        cams.Clear();
        foreach (var c in Camera.allCameras)
            if (c != null && c.isActiveAndEnabled && (c.cullingMask & 1) != 0) cams.Add(c.transform.position);   // slot views, the robot feed, any world camera
        if (cams.Count == 0) return;
        float k = Perf60.CullK, leafD = Perf60.LeafCardDist;
        int budget = Mathf.Min(list.Count, 900);
        for (int i = 0; i < budget; i++)
        {
            if (idx >= list.Count) idx = 0;
            R e = list[idx];
            if (e.r == null) { list.RemoveAt(idx); continue; }
            idx++;
            Bounds b = e.r.bounds;
            float d2 = 1e18f;
            for (int c = 0; c < cams.Count; c++) { float s = b.SqrDistance(cams[c]); if (s < d2) d2 = s; }
            float lim = e.leaf ? leafD : Mathf.Max(30f, e.size * k);
            float hyst = e.off ? 0.92f : 1f;   // come back a little sooner than they went (no flicker at the edge)
            bool off = d2 > lim * lim * hyst * hyst;
            if (off != e.off) { e.off = off; e.r.forceRenderingOff = off; }
            // small props (< 1.6 m) stop casting sun shadows past a short distance (a few texels of shadow there)
            if (e.caster)
            {
                float sl = Perf60.SmallShadowDist; bool sh = e.size < 1.6f && d2 > sl * sl;
                if (sh != e.shOff) { e.shOff = sh; e.r.shadowCastingMode = sh ? ShadowCastingMode.Off : ShadowCastingMode.On; }
            }
        }
        int nOff = 0, nSh = 0; for (int i = 0; i < list.Count; i++) { if (list[i].off) nOff++; if (list[i].shOff) nSh++; }
        Culled = nOff; Tracked = list.Count; ShadowCulled = nSh;
    }

    void Scan()
    {
        if (skipLayers < 0) skipLayers = (1 << 5) | (1 << Showroom.Layer) | (1 << RealRoom.Layer) | (1 << 20) | (1 << 21) | (1 << 22) | (1 << 23);
        int added = 0;
        foreach (var r in Object.FindObjectsOfType<Renderer>())
        {
            if (r == null) continue;
            int id = r.GetInstanceID();
            if (known.Contains(id)) continue;
            known.Add(id);
            if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer || r is SpriteRenderer) continue;
            if (((1 << r.gameObject.layer) & skipLayers) != 0) continue;
            if (UnderwaterWorld.Owns(r)) continue;   // the sea has its own per-camera switch
            bool leaf = r.gameObject.name == "Leaf56";
            Bounds b0 = r.bounds;
            // the ranch only (+-200 m): the countryside rings, space, Mars / Moon / Callisto and the house keep their own rules
            if (Mathf.Abs(b0.center.x) > Layout.Half + 40f || Mathf.Abs(b0.center.z) > Layout.Half + 40f || b0.center.y > 250f || b0.center.y < -60f) continue;
            float size = b0.size.magnitude;
            if (!leaf && (size > 14f || size <= 0f)) continue;   // big things (merged cells, buildings, terrain pieces) always draw
            if (r.GetComponentInParent<Canvas>() != null) continue;
            list.Add(new R { r = r, size = size, leaf = leaf, caster = r.shadowCastingMode == ShadowCastingMode.On });
            added++;
        }
        if (added > 0 && Perf60.ShowFps) Debug.Log("PERF60 cull: +" + added + " props tracked (" + list.Count + ")");
    }

    void OnDisable() { foreach (var e in list) if (e.r != null) { if (e.off) e.r.forceRenderingOff = false; if (e.shOff) e.r.shadowCastingMode = ShadowCastingMode.On; } }
}
