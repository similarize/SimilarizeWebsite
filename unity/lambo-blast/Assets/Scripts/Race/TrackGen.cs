using System.Collections.Generic;
using UnityEngine;

// Multi-track support: Track.Build(id) clears the old world and builds Coconut Cove (id 0, the original
// hand-made island in Track.cs) or one of the five generated adventure tracks from TrackDefs (ids 1-5):
// height profile with big hills / drops, auto-banked turns, half-pipe zones, huge jump gaps with full-width
// launch ramps, boost pads before every gap, a risky shortcut with its own gap, themed ground, water / lava,
// abutments under the lips. Layout + every gap were checked offline with work/lbtracks/sim.py.
public static partial class Track
{
    public static int Id { get; private set; }
    public static TrackDef Def { get; private set; }
    public static int Theme { get { return Def == null ? 0 : Def.theme; } }
    public static int Count { get { return 1 + TrackDefs.All.Length; } }
    public static string NameOf(int id) { return id <= 0 || id > TrackDefs.All.Length ? "Coconut Cove" : TrackDefs.All[id - 1].name; }
    public static string BlurbOf(int id) { return id <= 0 || id > TrackDefs.All.Length ? "tropical island · lagoon ford · sand-spit shortcut" : TrackDefs.All[id - 1].blurb; }
    public static string Name { get { return NameOf(Id); } }

    public static bool Lava;
    public static float LavaY = -999f;
    public static float[] Bank;                         // per main sample: tan(bank); surface y = centre y - lat * Bank
    public struct Gap { public float a, b, yA, yB, rampLen, rampH; public string name; public bool sc; }
    public static readonly List<Gap> Gaps = new List<Gap>();          // main road gaps (lip a -> landing b)
    public static Gap ScGap;
    public struct PadZone { public bool sc; public float s0, s1; }
    public static readonly List<PadZone> Pads = new List<PadZone>();
    public static readonly List<Vector2> PipeZones = new List<Vector2>();
    public static Material PadMat, LavaMat;
    static readonly List<Object> owned = new List<Object>();
    public static T Own<T>(T o) where T : Object { if (o != null) owned.Add(o); return o; }
    static bool apron;
    public static bool Apron { get { return apron; } }
    public static float SurfaceFloor { get { return Mathf.Max(WaterY, LavaY); } }

    // ---------------------------------------------------------------- build / clear
    public static void Build(int id)
    {
        Clear();
        Id = Mathf.Clamp(id, 0, Count - 1);
        Def = Id == 0 ? null : TrackDefs.All[Id - 1];
        Root = new GameObject("Track").transform;
        if (Def == null)
        {
            Scale = 0.85f; HalfW = 7.5f; ScHalf = 4.5f; Wall = 9f; WaterY = 0f;
            BuildCove();
        }
        else BuildGeneric(Def);
    }

    static void Clear()
    {
        if (Root != null)
        {
            GameObject g = Root.gameObject;
            foreach (MeshFilter mf in g.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null && mf.sharedMesh.name == MeshMerge.MeshName) Object.Destroy(mf.sharedMesh);
            g.SetActive(false);
            Object.DestroyImmediate(g);
            Root = null;
        }
        foreach (var o in owned) if (o != null) Object.Destroy(o);
        owned.Clear();
        BoxSpots.Clear(); RampS.Clear(); Gaps.Clear(); Pads.Clear(); PipeZones.Clear();
        hgrid = null; Bank = null; Lava = false; LavaY = -999f; PadMat = null; LavaMat = null; apron = false;
        ScGap = new Gap();
        Physics.SyncTransforms();
    }

    // pulsing boost pads + flowing lava (called every frame by Game)
    public static void Tick(float t)
    {
        if (PadMat != null)
        {
            float k = 0.5f + 0.5f * Mathf.Sin(t * 9f);
            PadMat.color = Color.Lerp(new Color(0.1f, 0.85f, 1f), new Color(0.85f, 1f, 1f), k);
        }
        if (LavaMat != null) LavaMat.mainTextureOffset = new Vector2(t * 0.006f, t * 0.004f);
    }

    // ---------------------------------------------------------------- queries
    static float WrapD(float d) { float L = Length; d %= L; if (d > L * 0.5f) d -= L; if (d < -L * 0.5f) d += L; return d; }

    // inside a main-road gap (+/- pad metres)?
    public static bool InGap(float s, float pad)
    {
        foreach (var g in Gaps)
        {
            float d = WrapD(s - g.a);
            if (d > -pad && d < g.b - g.a + pad) return true;
        }
        return false;
    }

    public static bool InPipe(float s)
    {
        foreach (var p in PipeZones) { float d = WrapD(s - p.x); if (d >= 0f && d <= p.y - p.x) return true; }
        return false;
    }

    // never respawn on a ramp approach or inside a gap: go back far enough to hit the boost pad at speed
    public static float SafeRespawnS(float s)
    {
        foreach (var g in Gaps)
        {
            float d = WrapD(s - g.a);
            if (d > -80f && d < g.b - g.a + 6f) return Wrap(g.a - 95f);
        }
        return s;
    }

    // metres to the next gap lip ahead (main road, or the shortcut gap when on the shortcut); -1 if none within `range`
    public static float JumpAhead(float s, float scS, float range)
    {
        float best = -1f;
        if (scS >= 0f && ScGap.b > ScGap.a)
        {
            float d = ScGap.a - scS;
            if (d > -2f && d < range) best = d;
            if (scS > ScGap.a && scS < ScGap.b + 4f) best = 0f;
            return best;
        }
        foreach (var g in Gaps)
        {
            float d = WrapD(g.a - s);
            if (d > -2f && d < range && (best < 0f || d < best)) best = d;
            if (WrapD(s - g.a) > 0f && WrapD(s - g.a) < g.b - g.a + 4f) best = 0f;
        }
        return best;
    }

    // did a kart driving along the road just cross a ramp lip? (frame-rate independent launch: the kart leaves at the
    // ramp's exact angle instead of whatever the per-frame climb estimate was - matters on slow devices)
    public static bool LipCross(float prevS, float curS, float lat, float prevScS, float curScS, float scDist, out float yTop, out float slope, out string name)
    {
        yTop = 0f; slope = 0f; name = null;
        foreach (var g in Gaps)
        {
            float d0 = WrapD(prevS - g.a), d1 = WrapD(curS - g.a);
            if (d0 < 0f && d0 > -(g.rampLen + 18f) && d1 >= -0.5f && d1 < 12f && Mathf.Abs(lat) < HalfW + 1f)
            { yTop = g.yA + g.rampH; slope = g.rampH / Mathf.Max(1f, g.rampLen); name = g.name; return true; }
        }
        var sg = ScGap;
        if (sg.b > sg.a && scDist < ScHalf + 1f && prevScS < sg.a && prevScS > sg.a - (sg.rampLen + 18f) && curScS >= sg.a - 0.5f && curScS < sg.a + 12f)
        { yTop = sg.yA + sg.rampH; slope = sg.rampH / Mathf.Max(1f, sg.rampLen); name = sg.name; return true; }
        return false;
    }

    public static string GapNameNear(float s, float scS)
    {
        if (scS >= 0f && ScGap.b > ScGap.a && Mathf.Abs(scS - ScGap.a) < 14f) return ScGap.name;
        foreach (var g in Gaps) { float d = WrapD(s - g.a); if (d > -14f && d < 6f) return g.name; }
        return null;
    }

    public static bool PadAt(float s, float lat, float scS, float scLat, bool onSc)
    {
        foreach (var p in Pads)
        {
            if (p.sc) { if (onSc && scS >= p.s0 && scS <= p.s1 && Mathf.Abs(scLat) < ScHalf + 0.6f) return true; }
            else
            {
                float d = WrapD(s - p.s0);
                if (d >= 0f && d <= p.s1 - p.s0 && Mathf.Abs(lat) < HalfW + 0.6f) return true;
            }
        }
        return false;
    }

    // ---------------------------------------------------------------- helpers
    static List<Vector3> SplinePts(List<Vector3> ctrl, bool closed, float step)
    {
        var dense = new List<Vector3>();
        int n = ctrl.Count;
        int segs = closed ? n : n - 1;
        for (int i = 0; i < segs; i++)
        {
            Vector3 p0 = ctrl[closed ? (i - 1 + n) % n : Mathf.Max(0, i - 1)];
            Vector3 p1 = ctrl[i];
            Vector3 p2 = ctrl[(i + 1) % n];
            Vector3 p3 = ctrl[closed ? (i + 2) % n : Mathf.Min(n - 1, i + 2)];
            for (int k = 0; k < 40; k++) dense.Add(CR(p0, p1, p2, p3, k / 40f));
        }
        if (!closed) dense.Add(ctrl[n - 1]);
        else dense.Add(dense[0]);
        var cum = new float[dense.Count];
        for (int i = 1; i < dense.Count; i++) cum[i] = cum[i - 1] + Flat(dense[i] - dense[i - 1]).magnitude;
        float total = cum[cum.Length - 1];
        int count = Mathf.Max(4, Mathf.RoundToInt(total / step));
        var outp = new List<Vector3>();
        int j = 0;
        int last = closed ? count : count + 1;
        for (int k = 0; k < last; k++)
        {
            float s = total * k / count;
            while (j < cum.Length - 2 && cum[j + 1] < s) j++;
            float seg = Mathf.Max(1e-4f, cum[j + 1] - cum[j]);
            outp.Add(Vector3.Lerp(dense[j], dense[j + 1], Mathf.Clamp01((s - cum[j]) / seg)));
        }
        return outp;
    }

    // height profile: (s, y) key pairs, smoothstep between keys (flat at every key); closed = wraps around the lap
    public static float Prof(float[] keys, float s, float L, bool closed)
    {
        int m = keys.Length / 2;
        if (m == 0) return 0f;
        if (closed) { s %= L; if (s < 0f) s += L; }
        float k0 = keys[0], y0 = keys[1], kl = keys[(m - 1) * 2], yl = keys[(m - 1) * 2 + 1];
        if (s <= k0)
        {
            if (!closed) return y0;
            float span = k0 + L - kl;
            return yl + (y0 - yl) * Smooth((s + L - kl) / span);
        }
        if (s >= kl)
        {
            if (!closed) return yl;
            float span = k0 + L - kl;
            return yl + (y0 - yl) * Smooth((s - kl) / span);
        }
        for (int i = 0; i < m - 1; i++)
        {
            float a = keys[i * 2], b = keys[i * 2 + 2];
            if (s >= a && s <= b) return keys[i * 2 + 1] + (keys[i * 2 + 3] - keys[i * 2 + 1]) * Smooth((s - a) / Mathf.Max(1e-3f, b - a));
        }
        return yl;
    }

    // nearest main sample for build-time work (coarse scan, then a fine window)
    static Proj NearestFast(Vector3 p)
    {
        int n = C.Length, best = 0; float bd = 1e18f;
        for (int i = 0; i < n; i += 5) { float d = Flat(p - C[i]).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
        return Nearest(p, best, 7);
    }

    static float BankAt(Proj pm)
    {
        if (Bank == null) return 0f;
        return Mathf.Lerp(Bank[pm.i], Bank[(pm.i + 1) % C.Length], pm.t);
    }

    static float ZoneW(float s, float lo, float hi, float ramp)
    {
        float d = Mathf.Max(WrapD(lo - s), WrapD(s - hi));
        if (WrapD(s - lo) >= 0f && WrapD(hi - s) >= 0f) return 1f;
        return 1f - Smooth(Mathf.Abs(d) / ramp);
    }

    static float PipeW(float s)
    {
        float w = 0f;
        foreach (var p in PipeZones) w = Mathf.Max(w, ZoneW(s, p.x, p.y, 18f));
        return w;
    }

    // ---------------------------------------------------------------- themes
    struct ThemeCols { public Color road, curbA, curbB, shoulder, cliff, ramp; }
    static ThemeCols Cols(int th)
    {
        switch (th)
        {
            case 1: return new ThemeCols { road = new Color(0.66f, 0.42f, 0.28f), curbA = new Color(0.85f, 0.2f, 0.12f), curbB = new Color(0.95f, 0.92f, 0.85f), shoulder = new Color(0.62f, 0.36f, 0.22f), cliff = new Color(0.58f, 0.3f, 0.18f), ramp = new Color(1f, 0.6f, 0.15f) };
            case 2: return new ThemeCols { road = new Color(0.32f, 0.29f, 0.28f), curbA = new Color(0.95f, 0.75f, 0.1f), curbB = new Color(0.08f, 0.08f, 0.08f), shoulder = new Color(0.27f, 0.24f, 0.22f), cliff = new Color(0.22f, 0.19f, 0.17f), ramp = new Color(0.95f, 0.35f, 0.08f) };
            case 3: return new ThemeCols { road = new Color(0.78f, 0.82f, 0.88f), curbA = new Color(0.15f, 0.4f, 0.9f), curbB = new Color(0.97f, 0.97f, 1f), shoulder = new Color(0.66f, 0.8f, 0.92f), cliff = new Color(0.55f, 0.62f, 0.7f), ramp = new Color(0.25f, 0.65f, 1f) };
            case 4: return new ThemeCols { road = new Color(0.5f, 0.5f, 0.42f), curbA = new Color(0.2f, 0.6f, 0.2f), curbB = new Color(0.9f, 0.88f, 0.75f), shoulder = new Color(0.42f, 0.44f, 0.34f), cliff = new Color(0.4f, 0.42f, 0.34f), ramp = new Color(0.95f, 0.75f, 0.2f) };
            default: return new ThemeCols { road = new Color(0.24f, 0.25f, 0.27f), curbA = new Color(0.9f, 0.15f, 0.15f), curbB = new Color(0.95f, 0.95f, 0.95f), shoulder = new Color(0.45f, 0.46f, 0.48f), cliff = new Color(0.4f, 0.42f, 0.46f), ramp = new Color(0.95f, 0.85f, 0.15f) };
        }
    }

    static float Natural(int th, float x, float z)
    {
        float p1 = Mathf.PerlinNoise(x * 0.0045f + 3.1f, z * 0.0045f + 7.7f);
        float p2 = Mathf.PerlinNoise(x * 0.02f + 1.3f, z * 0.02f + 5.1f);
        switch (th)
        {
            case 1:   // desert: plain with flat-topped mesas (stepped)
            {
                float mesa = Smooth((p1 - 0.5f) / 0.05f) * 30f + Smooth((p1 - 0.66f) / 0.04f) * 16f;
                return 1f + 3f * p2 + mesa;
            }
            case 2:   // volcano cone with a lava crater + lava pools on the plain
            {
                Vector2 c = new Vector2(-40f, -60f);
                float d = (new Vector2(x, z) - c).magnitude;
                float cone = 95f * Mathf.Pow(Mathf.Max(0f, 1f - d / 270f), 1.5f);
                float h = 3f + 5f * p2 + cone;
                if (d < 55f) h = Mathf.Lerp(h, LavaY - 4f, Smooth((55f - d) / 22f));
                float pool = Mathf.PerlinNoise(x * 0.012f + 9f, z * 0.012f + 2f);
                h -= 12f * Smooth((pool - 0.68f) / 0.08f) * Mathf.Clamp01((d - 120f) / 60f);
                return h;
            }
            case 3:   // mountains: big ridged peaks
            {
                float r = 1f - Mathf.Abs(2f * Mathf.PerlinNoise(x * 0.006f + 4f, z * 0.006f + 8f) - 1f);
                return 8f + 75f * p1 * p1 + 45f * r * r + 3f * p2;
            }
            case 4:   // rolling jungle hills
                return 7f + 18f * p1 + 4f * p2;
            default:  // city: flat streets
                return 0f;
        }
    }

    static float GapFloor(int th, float yA, float yB)
    {
        switch (th)
        {
            case 1: return WaterY - 3f;
            case 2: return LavaY - 5f;
            case 3: return Mathf.Min(yA, yB) - 34f;
            case 4: return WaterY - 5f;
            default: return 0f;
        }
    }

    // ---------------------------------------------------------------- generic build
    static void BuildGeneric(TrackDef d)
    {
        Scale = 1f; HalfW = 7.5f; ScHalf = 4.5f; Wall = 9f;
        int th = d.theme;
        WaterY = th == 1 ? -9f : th == 4 ? 2f : -999f;
        Lava = th == 2; LavaY = Lava ? -3f : -999f;
        apron = th == 5;

        // main line (same spline + resample as the design tool), rotated so s = 0 is 70 m into the start straight
        var cp = new List<Vector3>();
        foreach (var v in d.ctrl) cp.Add(new Vector3(v.x, 0f, v.y));
        var main = SplinePts(cp, true, 3f);
        int n = main.Count, sh = ((d.shift % n) + n) % n;
        C = new Vector3[n];
        for (int i = 0; i < n; i++) C[i] = main[(i + sh) % n];
        T = new Vector3[n]; Rt = new Vector3[n]; S = new float[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 dd = Flat(C[(i + 1) % n] - C[(i - 1 + n) % n]).normalized;
            T[i] = dd; Rt[i] = new Vector3(dd.z, 0f, -dd.x);
            if (i > 0) S[i] = S[i - 1] + Flat(C[i] - C[i - 1]).magnitude;
        }
        Length = S[n - 1] + Flat(C[0] - C[n - 1]).magnitude;
        Spacing = Length / n;
        for (int i = 0; i < n; i++) C[i].y = Prof(d.keys, S[i], Length, true);

        for (int k = 0; k + 3 < d.gaps.Length; k += 4)
        {
            float a = d.gaps[k], G = d.gaps[k + 1];
            Gaps.Add(new Gap { a = a, b = a + G, yA = Prof(d.keys, a, Length, true), yB = Prof(d.keys, a + G, Length, true), rampLen = d.gaps[k + 2], rampH = d.gaps[k + 3],
                name = k / 4 < d.gapNames.Length ? d.gapNames[k / 4] : "Gap" });
        }
        for (int k = 0; k + 1 < d.pipes.Length; k += 2) PipeZones.Add(new Vector2(d.pipes[k], d.pipes[k + 1]));

        // shortcut
        var sp = new List<Vector3>();
        foreach (var v in d.sc) sp.Add(new Vector3(v.x, 0f, v.y));
        var sc = SplinePts(sp, false, 3f);
        SC = sc.ToArray();
        SCT = new Vector3[SC.Length]; SCS = new float[SC.Length];
        for (int i = 0; i < SC.Length; i++)
        {
            SCT[i] = Flat(SC[Mathf.Min(SC.Length - 1, i + 1)] - SC[Mathf.Max(0, i - 1)]).normalized;
            if (i > 0) SCS[i] = SCS[i - 1] + Flat(SC[i] - SC[i - 1]).magnitude;
        }
        ScLength = SCS[SC.Length - 1];
        for (int i = 0; i < SC.Length; i++) SC[i].y = Prof(d.scKeys, SCS[i], ScLength, false);
        ScGapA = d.scGapA; ScGapB = d.scGapA + d.scGapLen;
        ScGap = new Gap { a = ScGapA, b = ScGapB, yA = Prof(d.scKeys, ScGapA, ScLength, false), yB = Prof(d.scKeys, ScGapB, ScLength, false), rampLen = d.scRampLen, rampH = d.scRampH, name = d.scName, sc = true };
        ScEnterS = Nearest(SC[0], -1, 0).s;
        ScExitS = Nearest(SC[SC.Length - 1], -1, 0).s;

        Min = new Vector2(1e9f, 1e9f); Max = new Vector2(-1e9f, -1e9f);
        foreach (var p in C) { Min = Vector2.Min(Min, new Vector2(p.x, p.z)); Max = Vector2.Max(Max, new Vector2(p.x, p.z)); }
        foreach (var p in SC) { Min = Vector2.Min(Min, new Vector2(p.x, p.z)); Max = Vector2.Max(Max, new Vector2(p.x, p.z)); }

        ComputeBank();
        ThemeCols tc = Cols(th);
        BuildGroundGeneric(th);
        var mainGaps = new List<Vector2>();
        foreach (var g in Gaps) mainGaps.Add(new Vector2(g.a, g.b));
        BuildRoadX(C, T, Bank, true, HalfW, tc, "Road", mainGaps, true, apron);
        BuildRoadX(SC, SCT, null, false, ScHalf, tc, "Shortcut", new List<Vector2> { new Vector2(ScGapA, ScGapB) }, false, false);
        if (WaterY > -100f) BuildWater();
        if (Lava) BuildLava();
        Physics.SyncTransforms();
        BuildRampsGeneric(d, tc);
        BuildPads(d);
        if (th != 5) BuildAbutments(th, tc);
        Physics.SyncTransforms();
        PlaceBoxesGeneric(d);
        for (int k = 0; k + 2 < d.hazards.Length; k += 3) Hazard.Create(Root, (int)d.hazards[k], d.hazards[k + 1], d.hazards[k + 2]);
        Debug.Log("Track: " + d.name + " length " + Length.ToString("F0") + " m (design " + d.expectLength.ToString("F0") + "), " + n + " samples, " + Gaps.Count +
            " gaps, shortcut " + ScLength.ToString("F0") + " m (design " + d.expectScLength.ToString("F0") + ", " + ScEnterS.ToString("F0") + " -> " + ScExitS.ToString("F0") + ")");
    }

    static void ComputeBank()
    {
        int n = C.Length;
        var kap = new float[n];
        for (int i = 0; i < n; i++)
            kap[i] = Vector3.SignedAngle(T[(i - 2 + n) % n], T[(i + 2) % n], Vector3.up) * Mathf.Deg2Rad / (4f * Spacing);
        Bank = new float[n];
        for (int i = 0; i < n; i++)
        {
            float k = 0f;
            for (int j = -6; j <= 6; j++) k += kap[(i + j + n) % n];
            k /= 13f;
            float zone = 0f;
            foreach (var g in Gaps) zone = Mathf.Max(zone, ZoneW(S[i], g.a - 70f, g.b + 45f, 22f));
            foreach (var p in PipeZones) zone = Mathf.Max(zone, ZoneW(S[i], p.x - 12f, p.y + 12f, 15f));
            zone = Mathf.Max(zone, ZoneW(S[i], Length - 70f, Length, 15f));     // start grid flat
            zone = Mathf.Max(zone, ZoneW(S[i], 0f, 20f, 15f));
            float deg = Mathf.Clamp(k * 620f, -16f, 16f) * (1f - zone);
            Bank[i] = Mathf.Tan(deg * Mathf.Deg2Rad);
        }
    }

    static void BuildGroundGeneric(int th)
    {
        float margin = 170f;
        gx0 = Min.x - margin; gz0 = Min.y - margin;
        float size = Mathf.Max(Max.x - Min.x, Max.y - Min.y) + margin * 2f;
        gn = 161;
        gcell = size / (gn - 1);
        hgrid = new float[gn * gn];
        var allGaps = new List<Gap>(Gaps);
        allGaps.Add(ScGap);
        for (int z = 0; z < gn; z++)
            for (int x = 0; x < gn; x++)
            {
                float wx = gx0 + x * gcell, wz = gz0 + z * gcell;
                float h = Natural(th, wx, wz);
                if (th != 5)
                {
                    var p = new Vector3(wx, 0f, wz);
                    Proj pm = NearestFast(p);
                    Proj ps = NearestShortcut(p);
                    float roadY = pm.point.y - Mathf.Clamp(pm.lat, -HalfW - 1.4f, HalfW + 1.4f) * BankAt(pm) - 0.4f;
                    float wM = Smooth((pm.dist - (HalfW + Wall + 2f)) / 26f);
                    float wS = Smooth((ps.dist - (ScHalf + Wall + 1f)) / 22f);
                    float scY = ps.point.y - 0.4f;
                    if (ps.dist < pm.dist) { h = Mathf.Lerp(roadY, h, wM); h = Mathf.Lerp(scY, h, wS); }
                    else { h = Mathf.Lerp(scY, h, wS); h = Mathf.Lerp(roadY, h, wM); }
                    // chasms under every gap
                    foreach (var g in allGaps)
                    {
                        if (g.b <= g.a) continue;
                        Vector3 ca = g.sc ? ShortcutPoint(g.a) : PointAt(g.a, 0f), cb = g.sc ? ShortcutPoint(g.b) : PointAt(g.b, 0f);
                        Vector3 c = (ca + cb) * 0.5f, t = Flat(cb - ca).normalized, nr = new Vector3(t.z, 0f, -t.x);
                        float half = (g.b - g.a) * 0.5f;
                        float along = Vector3.Dot(Flat(p - c), t), across = Vector3.Dot(Flat(p - c), nr);
                        float f = Smooth((half - 0.8f - Mathf.Abs(along)) / 3f) * Smooth((75f - Mathf.Abs(across)) / 25f);
                        if (f > 0f) h = Mathf.Lerp(h, Mathf.Min(h, GapFloor(th, g.yA, g.yB)), f);
                    }
                }
                hgrid[z * gn + x] = h;
            }
        var verts = new Vector3[gn * gn];
        var uv = new Vector2[gn * gn];
        for (int z = 0; z < gn; z++)
            for (int x = 0; x < gn; x++)
            {
                verts[z * gn + x] = new Vector3(gx0 + x * gcell, hgrid[z * gn + x], gz0 + z * gcell);
                uv[z * gn + x] = new Vector2(x / (float)(gn - 1), z / (float)(gn - 1));
            }
        var tris = new int[(gn - 1) * (gn - 1) * 6];
        int k = 0;
        for (int z = 0; z < gn - 1; z++)
            for (int x = 0; x < gn - 1; x++)
            {
                int a = z * gn + x, b = a + 1, c = a + gn, dd = c + 1;
                tris[k++] = a; tris[k++] = c; tris[k++] = b;
                tris[k++] = b; tris[k++] = c; tris[k++] = dd;
            }
        var mesh = Own(new Mesh());
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = verts; mesh.uv = uv; mesh.triangles = tris;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var go = new GameObject("Ground");
        go.transform.SetParent(Root, false);
        go.layer = GroundLayer;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        Texture2D gt = Own(GroundTexGeneric(th));
        mr.sharedMaterial = Own(th == 1 || th == 4 ? Mats.Detail(Mats.GroundBase, gt, 0.05f) : Mats.Tex(gt, th == 3 ? 0.25f : 0.08f));
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    static Texture2D GroundTexGeneric(int th)
    {
        const int n = 512;
        var tex = new Texture2D(n, n, TextureFormat.RGB24, true);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[n * n];
        var r = new System.Random(11 + th);
        float size = gcell * (gn - 1), e = size / n;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float wx = gx0 + (x + 0.5f) * e, wz = gz0 + (y + 0.5f) * e;
                float h = HeightAt(wx, wz);
                float gxs = (HeightAt(wx + 2f, wz) - HeightAt(wx - 2f, wz)) * 0.25f, gzs = (HeightAt(wx, wz + 2f) - HeightAt(wx, wz - 2f)) * 0.25f;
                float slope = Mathf.Sqrt(gxs * gxs + gzs * gzs);
                float nz = Mathf.PerlinNoise(wx * 0.05f, wz * 0.05f), nz2 = Mathf.PerlinNoise(wx * 0.013f + 7f, wz * 0.013f + 3f);
                Color c;
                switch (th)
                {
                    case 1:
                    {
                        Color sand = new Color(0.8f, 0.56f, 0.36f), rock = new Color(0.62f, 0.31f, 0.19f), band = new Color(0.72f, 0.42f, 0.26f), scrub = new Color(0.52f, 0.5f, 0.3f);
                        c = Color.Lerp(sand, scrub, Mathf.Clamp01((nz2 - 0.6f) * 3f) * 0.6f);
                        Color cliff = Color.Lerp(rock, band, 0.5f + 0.5f * Mathf.Sin(h * 0.9f));
                        c = Color.Lerp(c, cliff, Mathf.Clamp01((slope - 0.35f) * 3f));
                        if (h < WaterY + 1.5f) c = Color.Lerp(c, new Color(0.45f, 0.36f, 0.28f), 0.6f);
                        break;
                    }
                    case 2:
                    {
                        Color ash = new Color(0.44f, 0.4f, 0.37f), basalt = new Color(0.24f, 0.21f, 0.2f), glow = new Color(0.95f, 0.35f, 0.05f);
                        c = Color.Lerp(ash, basalt, Mathf.Clamp01(slope * 1.6f + (nz - 0.5f)));
                        c = Color.Lerp(c, glow, Mathf.Clamp01((LavaY + 2.5f - h) / 2.5f) * 0.85f);
                        break;
                    }
                    case 3:
                    {
                        Color snow = new Color(0.92f, 0.94f, 0.98f), rock = new Color(0.42f, 0.44f, 0.5f), ice = new Color(0.65f, 0.8f, 0.92f);
                        c = Color.Lerp(snow, rock, Mathf.Clamp01((slope - 0.55f) * 2.5f));
                        c = Color.Lerp(c, snow * 0.93f, nz * 0.4f);
                        if (slope < 0.3f && h < 10f) c = Color.Lerp(c, ice, 0.3f);
                        break;
                    }
                    case 4:
                    {
                        Color g1 = new Color(0.22f, 0.45f, 0.16f), g2 = new Color(0.16f, 0.36f, 0.12f), mud = new Color(0.4f, 0.3f, 0.2f), moss = new Color(0.33f, 0.38f, 0.26f);
                        c = Color.Lerp(g1, g2, nz);
                        c = Color.Lerp(c, moss, Mathf.Clamp01((slope - 0.4f) * 2.5f));
                        if (h < WaterY + 1.5f) c = Color.Lerp(c, mud, Mathf.Clamp01((WaterY + 1.5f - h) / 1.5f));
                        break;
                    }
                    default:
                    {
                        // city streets: asphalt with a 60 m block grid, pavements and lane lines
                        Color asph = new Color(0.2f, 0.2f, 0.22f), walk = new Color(0.5f, 0.5f, 0.52f), park = new Color(0.25f, 0.42f, 0.2f);
                        float bx = Mathf.Repeat(wx, 60f), bz = Mathf.Repeat(wz, 60f);
                        bool road = bx < 12f || bz < 12f;
                        c = road ? asph : (bx < 15f || bz < 15f || bx > 57f || bz > 57f) ? walk : (nz2 > 0.62f ? park : new Color(0.33f, 0.33f, 0.36f));
                        if (road && (Mathf.Abs(bx - 6f) < 0.25f || Mathf.Abs(bz - 6f) < 0.25f)) c = new Color(0.85f, 0.8f, 0.4f);
                        break;
                    }
                }
                float sp = (float)r.NextDouble();
                if (sp < 0.08f) c *= 0.93f; else if (sp > 0.95f) c = Color.Lerp(c, Color.white, 0.06f);
                c.a = 1f;
                px[y * n + x] = c;
            }
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }

    static Texture2D RoadTexT(ThemeCols tc, bool curbs, int seed)
    {
        const int w = 64, h = 256;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        tex.anisoLevel = 4;
        var r = new System.Random(seed);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;
                Color c = tc.road * (0.93f + 0.1f * (float)r.NextDouble());
                float tr = Mathf.Min(Mathf.Abs(u - 0.3f), Mathf.Abs(u - 0.7f));
                if (tr < 0.06f) c *= 0.92f + tr;
                if (Theme == 5 && Mathf.Abs(u - 0.5f) < 0.012f && (y / 24) % 2 == 0) c = new Color(0.95f, 0.95f, 0.9f);
                if (curbs)
                {
                    if (u < 0.07f || u > 0.93f) c = ((y / 32) % 2 == 0) ? tc.curbA : tc.curbB;
                    else if (u < 0.09f || u > 0.91f) c = tc.curbB;
                }
                else if (u < 0.05f || u > 0.95f) c *= 0.85f;
                c.a = 1f;
                px[y * w + x] = c;
            }
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }

    static Texture2D NoiseColTex(Color a, Color b, int seed, float freq)
    {
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGB24, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        var r = new System.Random(seed);
        var px = new Color32[n * n];
        float ox = (float)r.NextDouble() * 100f, oy = (float)r.NextDouble() * 100f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // tileable: blend 4 shifted noise samples
                float u = x / (float)n, v = y / (float)n;
                float f = 0f;
                for (int k = 0; k < 4; k++)
                {
                    float su = u + (k & 1), sv = v + (k >> 1);
                    float wgt = (1f - Mathf.Abs(su - 1f)) * (1f - Mathf.Abs(sv - 1f));
                    f += wgt * Mathf.PerlinNoise(ox + (x + (k & 1) * n) * freq, oy + (y + (k >> 1) * n) * freq);
                }
                Color c = Color.Lerp(a, b, Mathf.Clamp01(f * 1.6f - 0.3f)) * (0.94f + 0.08f * (float)r.NextDouble());
                c.a = 1f;
                px[y * n + x] = c;
            }
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }

    // road ribbon (curbed strip) + shoulder ribbon (skirts, half-pipe walls in pipe zones, or a flat rooftop apron),
    // both colliders on the Ground layer; banked by bank[] (tan); skips the gap ranges (in metres along the line)
    static void BuildRoadX(Vector3[] c, Vector3[] t, float[] bank, bool closed, float half, ThemeCols tc, string name, List<Vector2> gaps, bool pipes, bool apronMode)
    {
        float[] E = apronMode ? new[] { 0.5f, 1.4f, 3f, 6f, Wall + 1f, Wall + 1.1f } : new[] { 0.5f, 1.4f, 2.6f, 3.6f, 4.5f, 4.7f };
        float[] dN = apronMode ? new[] { -0.03f, -0.05f, -0.05f, -0.05f, -0.05f, -6f } : new[] { -0.18f, -0.5f, -1.04f, -1.49f, -1.9f, -2.0f };
        float[] dP = { 0.15f, 0.9f, 2.4f, 4.0f, 5.4f, -2.0f };
        int cols = 14;
        var rv = new List<Vector3>(); var ruv = new List<Vector2>(); var rt = new List<int>();
        var sv = new List<Vector3>(); var suv = new List<Vector2>(); var st = new List<int>();
        int n = c.Length;
        int count = closed ? n + 1 : n;
        float dist = 0f;
        bool prevOk = false;
        var lat = new float[cols]; var dy = new float[cols];
        for (int k = 0; k < count; k++)
        {
            int i = k % n;
            if (k > 0) dist += Flat(c[i] - c[(k - 1) % n]).magnitude;
            float sd = closed ? Wrap(dist) : dist;
            bool ok = true;
            foreach (var g in gaps) if (g.y > g.x && sd > g.x && sd < g.y) ok = false;
            Vector3 r = new Vector3(t[i].z, 0f, -t[i].x);
            float bk = bank != null ? bank[i] : 0f;
            float w = pipes ? PipeW(sd) : 0f;
            for (int j = 0; j < 6; j++)
            {
                float d0 = Mathf.Lerp(dN[j], dP[j], w);
                lat[5 - j] = -(half + E[j]); dy[5 - j] = d0;
                lat[8 + j] = half + E[j]; dy[8 + j] = d0;
            }
            lat[6] = -half; dy[6] = 0f; lat[7] = half; dy[7] = 0f;
            Vector3 p = c[i] + Vector3.up * 0.03f;
            int sb = sv.Count, rb = rv.Count;
            for (int j = 0; j < cols; j++)
            {
                float y = dy[j] - Mathf.Clamp(lat[j], -half, half) * bk;
                sv.Add(p + r * lat[j] + Vector3.up * y);
                suv.Add(new Vector2(lat[j] / 4f, dist / 4f));
            }
            rv.Add(p - r * half + Vector3.up * (half * bk)); ruv.Add(new Vector2(0f, dist / 16f));
            rv.Add(p + r * half - Vector3.up * (half * bk)); ruv.Add(new Vector2(1f, dist / 16f));
            if (k > 0 && ok && prevOk)
            {
                int a = sb - cols;
                for (int j = 0; j < cols - 1; j++)
                {
                    if (j == 6) continue;    // the road strip has its own mesh
                    st.Add(a + j); st.Add(sb + j); st.Add(a + j + 1);
                    st.Add(a + j + 1); st.Add(sb + j); st.Add(sb + j + 1);
                }
                int ra = rb - 2;
                rt.Add(ra); rt.Add(rb); rt.Add(ra + 1);
                rt.Add(ra + 1); rt.Add(rb); rt.Add(rb + 1);
            }
            prevOk = ok;
        }
        Material roadMat = Own(Mats.Detail(Mats.RoadBase, Own(RoadTexT(tc, half > 6f, 8 + Theme)), 0.08f));
        Material shMat = Own(Mats.Tex(Own(NoiseColTex(tc.shoulder, tc.shoulder * 0.8f, 30 + Theme, 0.08f)), Theme == 3 ? 0.4f : 0.1f));
        if (pipes && PipeZones.Count > 0)
        {
            // pipe walls get the curb colours so they read as a half-pipe
            shMat = Own(Mats.Tex(Own(NoiseColTex(tc.shoulder, Color.Lerp(tc.shoulder, tc.curbB, 0.5f), 31 + Theme, 0.06f)), Theme == 3 ? 0.5f : 0.15f));
        }
        MakeRibbon(name, rv, ruv, rt, roadMat, 0);
        MakeRibbon(name + " shoulders", sv, suv, st, shMat, 7);
    }

    static void MakeRibbon(string name, List<Vector3> v, List<Vector2> uv, List<int> tris, Material m, int upIndex)
    {
        if (tris.Count == 0) return;
        var mesh = Own(new Mesh());
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        if (mesh.normals.Length > upIndex && mesh.normals[upIndex].y < 0f)
        {
            for (int i = 0; i < tris.Count; i += 3) { int q = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = q; }
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
        }
        var go = new GameObject(name);
        go.transform.SetParent(Root, false);
        go.layer = GroundLayer;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = m;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    static void BuildLava()
    {
        // unlit (Sprites/Default) glowing lava; the quad carries tiled UVs itself (that shader ignores texture tiling)
        var go = new GameObject("Lava");
        go.transform.SetParent(Root, false);
        Vector3 c = new Vector3((Min.x + Max.x) * 0.5f, LavaY, (Min.y + Max.y) * 0.5f);
        var m = Own(new Mesh());
        const float H = 1300f, TILE = 60f;
        m.vertices = new[] { c + new Vector3(-H, 0f, -H), c + new Vector3(H, 0f, -H), c + new Vector3(H, 0f, H), c + new Vector3(-H, 0f, H) };
        m.uv = new[] { new Vector2(0f, 0f), new Vector2(TILE, 0f), new Vector2(TILE, TILE), new Vector2(0f, TILE) };
        m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        m.RecalculateNormals(); m.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = m;
        var mr = go.AddComponent<MeshRenderer>();
        Texture2D lt = Own(NoiseColTex(new Color(1f, 0.85f, 0.25f), new Color(0.75f, 0.12f, 0.02f), 5, 0.09f));
        LavaMat = Own(new Material(Mats.Fx));
        LavaMat.mainTexture = lt;
        LavaMat.color = Color.white;
        LavaMat.renderQueue = 2450;
        mr.sharedMaterial = LavaMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    static void BuildRampsGeneric(TrackDef d, ThemeCols tc)
    {
        for (int k = 0; k + 3 < d.gaps.Length; k += 4)
        {
            float a = d.gaps[k], Lr = d.gaps[k + 2], hr = d.gaps[k + 3];
            Vector3 pb = PointAt(a - Lr, 0f);
            RampS.Add(Wrap(a - Lr));
            RampX(new Vector3(pb.x, pb.y - 0.02f, pb.z), TangentAt(a - Lr * 0.5f), 2f * HalfW + 0.6f, Lr, hr, tc.ramp);
        }
        {
            float a = d.scGapA, Lr = d.scRampLen, hr = d.scRampH;
            Vector3 pc = ShortcutPoint(a - Lr), pe = ShortcutPoint(a);
            RampX(new Vector3(pc.x, pc.y - 0.02f, pc.z), Flat(pe - pc).normalized, 2f * ScHalf + 0.6f, Lr, hr, tc.ramp);
        }
    }

    static void RampX(Vector3 basePos, Vector3 dir, float w, float len, float h, Color col)
    {
        var go = new GameObject("Ramp");
        go.transform.SetParent(Root, false);
        go.layer = GroundLayer;
        go.transform.position = basePos;
        go.transform.rotation = Quaternion.LookRotation(Flat(dir).normalized, Vector3.up);
        Mesh m = Own(Geo.Wedge(w, len, h));
        go.AddComponent<MeshFilter>().sharedMesh = m;
        go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit(col);
        go.AddComponent<MeshCollider>().sharedMesh = m;
        float ang = Mathf.Atan2(h, len) * Mathf.Rad2Deg;
        for (int i = 0; i < 4; i++)
        {
            float z = len * (0.2f + i * 0.2f);
            var s = Mats.Prim(PrimitiveType.Cube, go.transform, new Vector3(0f, h * z / len + 0.03f, z), new Vector3(w * 0.8f, 0.04f, 0.5f), new Vector3(-ang, 0f, 0f), Mats.Lit(Color.white));
            s.layer = 0;
        }
        for (int sgn = -1; sgn <= 1; sgn += 2)
        {
            var rail = Mats.Prim(PrimitiveType.Cube, go.transform, new Vector3(sgn * (w / 2 + 0.12f), h * 0.5f + 0.1f, len * 0.5f), new Vector3(0.3f, 0.3f, Mathf.Sqrt(len * len + h * h)), new Vector3(-ang, 0f, 0f), Mats.Lit(new Color(0.15f, 0.15f, 0.17f)));
            rail.layer = 0;
            // tall warning posts at the lip
            var post = Mats.Prim(PrimitiveType.Cube, go.transform, new Vector3(sgn * (w / 2 + 0.6f), h + 1.2f, len - 0.2f), new Vector3(0.35f, 2.4f + h, 0.35f), Mats.Lit(new Color(0.95f, 0.85f, 0.1f)));
            post.transform.localPosition = new Vector3(sgn * (w / 2 + 0.6f), (h + 2.4f) * 0.5f - 0.6f, len - 0.2f);
            post.layer = 0;
            var cap = Mats.Prim(PrimitiveType.Cube, go.transform, new Vector3(sgn * (w / 2 + 0.6f), h + 1.9f, len - 0.2f), new Vector3(0.5f, 0.5f, 0.5f), Mats.Unlit(new Color(1f, 0.3f, 0.1f)));
            cap.layer = 0;
        }
    }

    static void BuildPads(TrackDef d)
    {
        PadMat = Own(new Material(Mats.Unlit(new Color(0.2f, 0.9f, 1f))));
        Material baseM = Mats.Unlit(new Color(0.03f, 0.2f, 0.32f));
        foreach (var g in Gaps)
        {
            float s0 = Wrap(g.a - 52f), s1 = Wrap(g.a - 44f);
            Pads.Add(new PadZone { sc = false, s0 = s0, s1 = s0 + 8f });
            float sm = Wrap(g.a - 48f);
            Vector3 p = PointAt(sm, 0f);
            PadVisual(p, TangentAt(sm), 2f * HalfW - 0.4f, 8f, baseM);
        }
        {
            float s0 = ScGapA - 46f;
            Pads.Add(new PadZone { sc = true, s0 = s0, s1 = s0 + 7f });
            Vector3 p = ShortcutPoint(s0 + 3.5f), q = ShortcutPoint(s0 + 5f);
            PadVisual(p, Flat(q - p).normalized, 2f * ScHalf - 0.4f, 7f, baseM);
        }
    }

    static void PadVisual(Vector3 p, Vector3 dir, float w, float len, Material baseM)
    {
        var t = new GameObject("Boost pad").transform;
        t.SetParent(Root, false);
        t.position = p + Vector3.up * 0.05f;
        t.rotation = Quaternion.LookRotation(Flat(dir).normalized, Vector3.up);
        Mats.Prim(PrimitiveType.Cube, t, Vector3.zero, new Vector3(w, 0.03f, len), baseM);
        for (int i = 0; i < 3; i++)
        {
            float z = -len * 0.3f + i * len * 0.3f;
            for (int s = -1; s <= 1; s += 2)
            {
                var bar = Mats.Prim(PrimitiveType.Cube, t, new Vector3(s * w * 0.17f, 0.03f, z), new Vector3(w * 0.38f, 0.03f, 0.55f), new Vector3(0f, s * 32f, 0f), PadMat);
                bar.transform.localPosition = new Vector3(s * w * 0.16f, 0.03f, z - w * 0.1f);
            }
        }
    }

    // rock / ice / stone faces under the lip and the landing of every gap (visual only)
    static void BuildAbutments(int th, ThemeCols tc)
    {
        Material m = Mats.Lit(tc.cliff);
        var list = new List<Gap>(Gaps);
        list.Add(ScGap);
        foreach (var g in list)
        {
            if (g.b <= g.a) continue;
            float floor = GapFloor(th, g.yA, g.yB) - 2f;
            float w = g.sc ? 2f * (ScHalf + 6f) + 4f : 2f * (HalfW + Wall) + 6f;
            for (int e = 0; e < 2; e++)
            {
                float s = e == 0 ? g.a - 3f : g.b + 3f;
                Vector3 p = g.sc ? ShortcutPoint(s) : PointAt(s, 0f);
                Vector3 tan = g.sc ? Flat(ShortcutPoint(g.b) - ShortcutPoint(g.a)).normalized : TangentAt(s);
                float top = (e == 0 ? g.yA : g.yB) - 0.1f;
                float hgt = Mathf.Max(1f, top - floor);
                var b = Mats.Prim(PrimitiveType.Cube, Root, new Vector3(p.x, top - hgt * 0.5f, p.z), new Vector3(w, hgt, 6f), m);
                b.transform.rotation = Quaternion.LookRotation(tan, Vector3.up);
                b.name = "Abutment";
            }
        }
    }

    static void PlaceBoxesGeneric(TrackDef d)
    {
        foreach (float s in d.boxes)
        {
            Vector3 tan = TangentAt(s);
            float yaw = Mathf.Atan2(tan.x, tan.z) * Mathf.Rad2Deg;
            for (int k = 0; k < 4; k++) BoxSpots.Add(new KeyValuePair<Vector3, float>(PointAt(s, -4.8f + k * 3.2f), yaw));
        }
        Vector3 q = ShortcutPoint(ScGapB + 16f), q2 = ShortcutPoint(ScGapB + 18f);
        Vector3 dd = Flat(q2 - q).normalized, rt = new Vector3(dd.z, 0f, -dd.x);
        float y2 = Mathf.Atan2(dd.x, dd.z) * Mathf.Rad2Deg;
        BoxSpots.Add(new KeyValuePair<Vector3, float>(q - rt * 1.6f, y2));
        BoxSpots.Add(new KeyValuePair<Vector3, float>(q + rt * 1.6f, y2));
    }
}
