using System.Collections.Generic;
using UnityEngine;

// "Coconut Cove": one closed tropical island circuit (~1.25 km), built entirely in code.
//  * Main loop: Catmull-Rom through hand-placed control points, resampled every ~3 m.
//    Sandy road with red / white curbs, a hill section, a shallow lagoon ford (splashes) and two ramps.
//  * Shortcut: a narrow sand spit on the east side that skips the hill hairpin, with a ramp jump over
//    a lagoon gap (too slow = splash + respawn).
//  * Ground: one height-field mesh (beach, inland hills, sea floor) under a transparent sea plane.
// All drivable surfaces are colliders on layer 8 (Ground); karts and projectiles ray-cast down onto them.
public static class Track
{
    public const int GroundLayer = 8;
    public const int GroundMask = 1 << GroundLayer;
    public const float HalfW = 7.5f;      // main road half width
    public const float ScHalf = 4.5f;     // shortcut half width
    public const float Wall = 9f;         // invisible soft wall this far beyond the road edge
    public const float WaterY = 0f;
    public const float Scale = 0.85f;
    public const int Laps = 3;

    public static Vector3[] C, T, Rt;     // main centre line (road surface height), tangents, right vectors
    public static float[] S;              // distance along the line
    public static float Length, Spacing;
    public static Vector3[] SC, SCT;      // shortcut centre line + tangents
    public static float[] SCS;
    public static float ScGapA, ScGapB;   // gap (no road) along the shortcut, in metres from its start
    public static float ScEnterS, ScExitS;
    public static float ScLength;
    public static readonly List<KeyValuePair<Vector3, float>> BoxSpots = new List<KeyValuePair<Vector3, float>>();   // position, yaw
    public static readonly List<float> RampS = new List<float>();
    public static Vector2 Min, Max;
    public static Transform Root;

    static float[] hgrid;
    static int gn;
    static float gx0, gz0, gcell;

    // control points (x, z, road height) before scaling
    static readonly Vector3[] Ctrl = {
        new Vector3(0, -150, 1), new Vector3(80, -158, 1), new Vector3(160, -142, 1.2f), new Vector3(222, -100, 1.5f),
        new Vector3(242, -40, 1.5f), new Vector3(222, 20, 1.5f), new Vector3(172, 42, 2.5f), new Vector3(132, 72, 5f),
        new Vector3(128, 122, 7f), new Vector3(165, 160, 4f), new Vector3(160, 212, 1.5f), new Vector3(100, 232, 1f),
        new Vector3(30, 214, 0.6f), new Vector3(-20, 178, -0.3f), new Vector3(-62, 186, -0.3f), new Vector3(-122, 202, 0.5f),
        new Vector3(-182, 172, 1f), new Vector3(-212, 110, 1.2f), new Vector3(-200, 40, 2f), new Vector3(-158, -18, 3f),
        new Vector3(-170, -82, 2f), new Vector3(-132, -136, 1.2f), new Vector3(-70, -152, 1f) };

    // shortcut: leaves the main road at the east bend, runs up the sand spit, rejoins after the hill
    static readonly Vector3[] ScCtrl = {
        new Vector3(224, 14, 1.5f), new Vector3(230, 40, 1.4f), new Vector3(238, 75, 1.3f), new Vector3(236, 112, 1.3f),
        new Vector3(222, 150, 1.4f), new Vector3(196, 178, 2f), new Vector3(166, 188, 2.6f) };

    static Vector3 CR(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    static Vector3 W(Vector3 c) { return new Vector3(c.x * Scale, c.z, c.y * Scale); }

    // dense Catmull-Rom, then resample at an even spacing
    static List<Vector3> Spline(Vector3[] ctrl, bool closed, float step)
    {
        var dense = new List<Vector3>();
        int n = ctrl.Length;
        int segs = closed ? n : n - 1;
        for (int i = 0; i < segs; i++)
        {
            Vector3 p0 = W(ctrl[closed ? (i - 1 + n) % n : Mathf.Max(0, i - 1)]);
            Vector3 p1 = W(ctrl[i]);
            Vector3 p2 = W(ctrl[(i + 1) % n]);
            Vector3 p3 = W(ctrl[closed ? (i + 2) % n : Mathf.Min(n - 1, i + 2)]);
            for (int k = 0; k < 40; k++) dense.Add(CR(p0, p1, p2, p3, k / 40f));
        }
        if (!closed) dense.Add(W(ctrl[n - 1]));
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

    public static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    public static void Build()
    {
        Root = new GameObject("Track").transform;
        var main = Spline(Ctrl, true, 3f);
        int n = main.Count;
        C = main.ToArray();
        T = new Vector3[n]; Rt = new Vector3[n]; S = new float[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 d = Flat(C[(i + 1) % n] - C[(i - 1 + n) % n]).normalized;
            T[i] = d;
            Rt[i] = new Vector3(d.z, 0f, -d.x);
            if (i > 0) S[i] = S[i - 1] + Flat(C[i] - C[i - 1]).magnitude;
        }
        Length = S[n - 1] + Flat(C[0] - C[n - 1]).magnitude;
        Spacing = Length / n;

        var sc = Spline(ScCtrl, false, 3f);
        SC = sc.ToArray();
        SCT = new Vector3[SC.Length]; SCS = new float[SC.Length];
        for (int i = 0; i < SC.Length; i++)
        {
            SCT[i] = Flat(SC[Mathf.Min(SC.Length - 1, i + 1)] - SC[Mathf.Max(0, i - 1)]).normalized;
            if (i > 0) SCS[i] = SCS[i - 1] + Flat(SC[i] - SC[i - 1]).magnitude;
        }
        ScLength = SCS[SC.Length - 1];
        ScGapA = ScLength * 0.47f; ScGapB = ScGapA + 13f;
        ScEnterS = Nearest(SC[0], -1, 0).s;
        ScExitS = Nearest(SC[SC.Length - 1], -1, 0).s;

        Min = new Vector2(1e9f, 1e9f); Max = new Vector2(-1e9f, -1e9f);
        foreach (var p in C) { Min = Vector2.Min(Min, new Vector2(p.x, p.z)); Max = Vector2.Max(Max, new Vector2(p.x, p.z)); }
        foreach (var p in SC) { Min = Vector2.Min(Min, new Vector2(p.x, p.z)); Max = Vector2.Max(Max, new Vector2(p.x, p.z)); }

        BuildGround();
        BuildRoad(C, T, true, HalfW, RoadTex(true), "Road", 0f, 0f);
        BuildRoad(SC, SCT, false, ScHalf, RoadTex(false), "Shortcut", ScGapA, ScGapB);
        BuildWater();
        Physics.SyncTransforms();
        BuildRamps();
        Physics.SyncTransforms();
        PlaceBoxes();
        Debug.Log("Track: length " + Length.ToString("F0") + " m, " + n + " samples, shortcut " + ScLength.ToString("F0") + " m (" + ScEnterS.ToString("F0") + " -> " + ScExitS.ToString("F0") + ")");
    }

    // ---------------- queries ----------------
    public struct Proj
    {
        public int i;          // segment start index
        public float t, s, lat, dist;
        public Vector3 point, tan, right;
    }

    // nearest point on the main line; hint < 0 = search everything, else +/- window samples around hint
    public static Proj Nearest(Vector3 p, int hint, int window)
    {
        int n = C.Length;
        var best = new Proj { dist = 1e9f };
        int from = hint < 0 ? 0 : hint - window, to = hint < 0 ? n - 1 : hint + window;
        for (int k = from; k <= to; k++)
        {
            int i = ((k % n) + n) % n;
            Vector3 a = C[i], b = C[(i + 1) % n];
            Vector3 ab = Flat(b - a), ap = Flat(p - a);
            float len2 = Mathf.Max(1e-4f, ab.sqrMagnitude);
            float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / len2);
            Vector3 q = a + (b - a) * t;
            float d = Flat(p - q).sqrMagnitude;
            if (d < best.dist) { best.dist = d; best.i = i; best.t = t; best.point = q; }
        }
        best.dist = Mathf.Sqrt(best.dist);
        int i0 = best.i, i1 = (best.i + 1) % n;
        best.tan = Vector3.Lerp(T[i0], T[i1], best.t).normalized;
        best.right = new Vector3(best.tan.z, 0f, -best.tan.x);
        best.lat = Vector3.Dot(Flat(p - best.point), best.right);
        best.s = S[i0] + best.t * Flat(C[i1] - C[i0]).magnitude;
        return best;
    }

    // nearest point on the shortcut (small, always a full search). s = metres from the shortcut start.
    public static Proj NearestShortcut(Vector3 p)
    {
        var best = new Proj { dist = 1e9f };
        for (int i = 0; i < SC.Length - 1; i++)
        {
            Vector3 a = SC[i], b = SC[i + 1];
            Vector3 ab = Flat(b - a), ap = Flat(p - a);
            float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
            Vector3 q = a + (b - a) * t;
            float d = Flat(p - q).sqrMagnitude;
            if (d < best.dist) { best.dist = d; best.i = i; best.t = t; best.point = q; }
        }
        best.dist = Mathf.Sqrt(best.dist);
        best.tan = SCT[best.i];
        best.right = new Vector3(best.tan.z, 0f, -best.tan.x);
        best.lat = Vector3.Dot(Flat(p - best.point), best.right);
        best.s = SCS[best.i] + best.t * Flat(SC[best.i + 1] - SC[best.i]).magnitude;
        return best;
    }

    public static float Wrap(float s) { s %= Length; return s < 0f ? s + Length : s; }

    public static int IndexAt(float s) { return Mathf.Clamp(Mathf.FloorToInt(Wrap(s) / Spacing), 0, C.Length - 1); }

    public static Vector3 PointAt(float s, float lat)
    {
        s = Wrap(s);
        int i = IndexAt(s);
        int j = (i + 1) % C.Length;
        float t = Mathf.Clamp01((s - S[i]) / Spacing);
        Vector3 p = Vector3.Lerp(C[i], C[j], t);
        Vector3 tan = Vector3.Lerp(T[i], T[j], t).normalized;
        return p + new Vector3(tan.z, 0f, -tan.x) * lat;
    }

    public static Vector3 TangentAt(float s)
    {
        s = Wrap(s);
        int i = IndexAt(s);
        return Vector3.Lerp(T[i], T[(i + 1) % C.Length], Mathf.Clamp01((s - S[i]) / Spacing)).normalized;
    }

    public static Vector3 ShortcutPoint(float s)
    {
        s = Mathf.Clamp(s, 0f, ScLength);
        int i = 0;
        while (i < SC.Length - 2 && SCS[i + 1] < s) i++;
        float seg = Mathf.Max(1e-4f, SCS[i + 1] - SCS[i]);
        return Vector3.Lerp(SC[i], SC[i + 1], Mathf.Clamp01((s - SCS[i]) / seg));
    }

    // heading change (degrees) over the next `dist` metres: how sharp the road gets ahead
    public static float Bend(float s, float dist)
    {
        Vector3 a = TangentAt(s), b = TangentAt(s + dist);
        return Vector3.Angle(a, b);
    }

    public static bool Ground(Vector3 p, out float y, out Vector3 normal)
    {
        RaycastHit h;
        if (Physics.Raycast(p + Vector3.up * 4f, Vector3.down, out h, 14f, GroundMask, QueryTriggerInteraction.Ignore))
        {
            y = h.point.y; normal = h.normal;
            return true;
        }
        y = HeightAt(p.x, p.z); normal = Vector3.up;
        return false;
    }

    // terrain height from the generated grid (bilinear)
    public static float HeightAt(float x, float z)
    {
        if (hgrid == null) return 0f;
        float fx = (x - gx0) / gcell, fz = (z - gz0) / gcell;
        int ix = Mathf.Clamp(Mathf.FloorToInt(fx), 0, gn - 2), iz = Mathf.Clamp(Mathf.FloorToInt(fz), 0, gn - 2);
        float tx = Mathf.Clamp01(fx - ix), tz = Mathf.Clamp01(fz - iz);
        float a = Mathf.Lerp(hgrid[iz * gn + ix], hgrid[iz * gn + ix + 1], tx);
        float b = Mathf.Lerp(hgrid[(iz + 1) * gn + ix], hgrid[(iz + 1) * gn + ix + 1], tx);
        return Mathf.Lerp(a, b, tz);
    }

    // is a point inside the main loop (polygon test on the centre line)?
    static bool Inside(float x, float z)
    {
        bool inside = false;
        int n = C.Length;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            Vector3 a = C[i], b = C[j];
            if (((a.z > z) != (b.z > z)) && (x < (b.x - a.x) * (z - a.z) / (b.z - a.z) + a.x)) inside = !inside;
        }
        return inside;
    }

    static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

    // ---------------- ground ----------------
    static float NaturalHeight(float x, float z, Proj pm, Proj ps)
    {
        float yRoad = pm.point.y;
        float h;
        bool inside = Inside(x, z);
        if (inside)
        {
            float hills = Mathf.Max(0f, Mathf.PerlinNoise(x * 0.011f + 5.3f, z * 0.011f + 1.7f) - 0.38f) * 18f;
            h = 1.1f + hills * Smooth((pm.dist - 24f) / 60f) + 0.3f * Mathf.PerlinNoise(x * 0.05f, z * 0.05f);
            // lagoon inlet around the ford
            Vector2 lag = new Vector2(-40f, 150f) * Scale;
            float dl = (new Vector2(x, z) - lag).magnitude;
            if (dl < 55f) h = Mathf.Lerp(-1.8f, h, Smooth((dl - 14f) / 41f));
        }
        else
        {
            float beachTop = Mathf.Min(yRoad - 0.3f, 1.1f);
            h = Mathf.Lerp(beachTop, -5f, Smooth((pm.dist - HalfW - 14f) / 38f)) + 0.15f * Mathf.PerlinNoise(x * 0.07f, z * 0.07f);
        }
        // road corridor
        float corridor = yRoad - 0.35f;
        h = Mathf.Lerp(corridor, h, Smooth((pm.dist - HalfW - 3f) / 12f));
        // shortcut spit (with the lagoon gap)
        bool gap = ps.s > ScGapA - 3f && ps.s < ScGapB + 3f;
        if (gap)
        {
            if (ps.dist < ScHalf + 8f) h = Mathf.Min(h, Mathf.Lerp(-2.6f, h, Smooth((ps.dist - ScHalf - 2f) / 6f)));
        }
        else
        {
            float sh = ps.point.y - 0.35f;
            h = Mathf.Max(h, Mathf.Lerp(sh, -6f, Smooth((ps.dist - ScHalf - 2f) / 16f)));
        }
        return h;
    }

    static void BuildGround()
    {
        float margin = 130f;
        gx0 = Min.x - margin; gz0 = Min.y - margin;
        float size = Mathf.Max(Max.x - Min.x, Max.y - Min.y) + margin * 2f;
        gn = 121;
        gcell = size / (gn - 1);
        hgrid = new float[gn * gn];
        int hint = -1;
        for (int z = 0; z < gn; z++)
            for (int x = 0; x < gn; x++)
            {
                float wx = gx0 + x * gcell, wz = gz0 + z * gcell;
                var p = new Vector3(wx, 0f, wz);
                Proj pm = Nearest(p, -1, 0);
                Proj ps = NearestShortcut(p);
                hgrid[z * gn + x] = NaturalHeight(wx, wz, pm, ps);
            }
        // edge of the world: sea floor
        for (int z = 0; z < gn; z++)
            for (int x = 0; x < gn; x++)
                if (x < 2 || z < 2 || x > gn - 3 || z > gn - 3) hgrid[z * gn + x] = Mathf.Min(hgrid[z * gn + x], -6f);

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
                int a = z * gn + x, b = a + 1, c = a + gn, d = c + 1;
                tris[k++] = a; tris[k++] = c; tris[k++] = b;
                tris[k++] = b; tris[k++] = c; tris[k++] = d;
            }
        var mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = verts; mesh.uv = uv; mesh.triangles = tris;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var go = new GameObject("Island");
        go.transform.SetParent(Root, false);
        go.layer = GroundLayer;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = Mats.Detail(Mats.GroundBase, GroundTex(), 0.05f);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    static Texture2D GroundTex()
    {
        const int n = 512;
        var tex = new Texture2D(n, n, TextureFormat.RGB24, true);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[n * n];
        var r = new System.Random(4);
        float size = gcell * (gn - 1);
        Color sand = new Color(0.76f, 0.6f, 0.37f), wet = new Color(0.58f, 0.45f, 0.29f), sea = new Color(0.34f, 0.62f, 0.6f),   // tuned so lit sand stays golden, not white
              deep = new Color(0.16f, 0.4f, 0.47f), grass = new Color(0.32f, 0.56f, 0.21f), grass2 = new Color(0.24f, 0.46f, 0.17f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float wx = gx0 + (x + 0.5f) * size / n, wz = gz0 + (y + 0.5f) * size / n;
                float h = HeightAt(wx, wz);
                Color c;
                if (h < -0.6f) c = Color.Lerp(sea, deep, Mathf.Clamp01((-h - 0.6f) / 4f));
                else if (h < 0.25f) c = Color.Lerp(sea, wet, Mathf.Clamp01((h + 0.6f) / 0.85f));
                else
                {
                    c = Color.Lerp(wet, sand, Mathf.Clamp01((h - 0.25f) / 0.5f));
                    float g = Mathf.PerlinNoise(wx * 0.02f + 11f, wz * 0.02f + 3f);
                    float grassAmt = Mathf.Clamp01((h - 1.7f) * 1.2f) * Mathf.Clamp01((g - 0.42f) * 5f);
                    c = Color.Lerp(c, Color.Lerp(grass, grass2, Mathf.PerlinNoise(wx * 0.09f, wz * 0.09f)), grassAmt);
                }
                float sp = (float)r.NextDouble();
                if (sp < 0.08f) c *= 0.93f; else if (sp > 0.95f) c = Color.Lerp(c, Color.white, 0.08f);
                px[y * n + x] = c;
            }
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }

    static Texture2D RoadTex(bool curbs)
    {
        const int w = 64, h = 256;      // v repeats every 16 m
        var tex = new Texture2D(w, h, TextureFormat.RGB24, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        tex.anisoLevel = 4;
        var r = new System.Random(curbs ? 8 : 9);
        var px = new Color32[w * h];
        Color baseC = curbs ? new Color(0.66f, 0.53f, 0.36f) : new Color(0.69f, 0.56f, 0.38f);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;
                Color c = baseC * (0.94f + 0.08f * (float)r.NextDouble());
                // tyre tracks
                float tr = Mathf.Min(Mathf.Abs(u - 0.3f), Mathf.Abs(u - 0.7f));
                if (tr < 0.06f) c *= 0.9f + tr;
                if (curbs)
                {
                    if (u < 0.07f || u > 0.93f) c = ((y / 32) % 2 == 0) ? new Color(0.88f, 0.15f, 0.12f) : new Color(0.93f, 0.93f, 0.9f);
                    else if (u < 0.09f || u > 0.91f) c = new Color(0.94f, 0.94f, 0.91f);
                }
                else if (u < 0.05f || u > 0.95f) c *= 0.85f;
                c.a = 1f;
                px[y * w + x] = c;
            }
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }

    // road ribbon with sloped skirts; skips [gapA, gapB] metres (shortcut lagoon jump)
    static void BuildRoad(Vector3[] c, Vector3[] t, bool closed, float half, Texture2D tex, string name, float gapA, float gapB)
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        int n = c.Length;
        float dist = 0f;
        int count = closed ? n + 1 : n;
        bool prevOk = false;
        for (int k = 0; k < count; k++)
        {
            int i = k % n;
            if (k > 0) dist += Flat(c[i] - c[(k - 1) % n]).magnitude;
            bool ok = !(gapB > gapA && dist > gapA && dist < gapB);
            Vector3 rt = new Vector3(t[i].z, 0f, -t[i].x);
            Vector3 p = c[i] + Vector3.up * 0.03f;
            float v = dist / 16f;
            int b = verts.Count;
            verts.Add(p - rt * (half + 1.4f) + Vector3.down * 0.5f); uvs.Add(new Vector2(0.01f, v));
            verts.Add(p - rt * half); uvs.Add(new Vector2(0f, v));
            verts.Add(p + rt * half); uvs.Add(new Vector2(1f, v));
            verts.Add(p + rt * (half + 1.4f) + Vector3.down * 0.5f); uvs.Add(new Vector2(0.99f, v));
            if (k > 0 && ok && prevOk)
            {
                int a = b - 4;
                for (int s = 0; s < 3; s++)
                {
                    tris.Add(a + s); tris.Add(b + s); tris.Add(a + s + 1);
                    tris.Add(a + s + 1); tris.Add(b + s); tris.Add(b + s + 1);
                }
            }
            prevOk = ok;
        }
        var mesh = new Mesh();
        mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        // make sure the road faces up
        if (mesh.normals.Length > 1 && mesh.normals[1].y < 0f)
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
        mr.sharedMaterial = Mats.Detail(Mats.RoadBase, tex, 0.08f);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    static void BuildWater()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(go.GetComponent<Collider>());
        go.name = "Sea";
        go.transform.SetParent(Root, false);
        go.transform.position = new Vector3((Min.x + Max.x) * 0.5f, WaterY, (Min.y + Max.y) * 0.5f);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = new Vector3(2600f, 2600f, 1f);
        var mr = go.GetComponent<MeshRenderer>();
        Material wm = Mats.Water;
        if (wm != null && wm.shader != null && wm.shader.name == "LB/Water")
        {
            wm = new Material(wm);
            float size = gcell * (gn - 1);
            wm.SetTexture("_SeaMap", SeaMap(256, size));
            wm.SetTexture("_Noise", NoiseTex(128));
            wm.SetVector("_SeaRect", new Vector4(gx0, gz0, 1f / size, 1f / size));
        }
        mr.sharedMaterial = wm;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    // R = depth (0 at the waterline .. 1 at 5 m deep), G = shore foam mask (peaks just off the beach)
    static Texture2D SeaMap(int n, float size)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float wx = gx0 + (x + 0.5f) * size / n, wz = gz0 + (y + 0.5f) * size / n;
                float h = HeightAt(wx, wz);
                float depth = Mathf.Clamp01(-h / 5f);
                float foam = Mathf.Clamp01(1f - Mathf.Abs(h + 0.35f) / 0.65f);
                px[y * n + x] = new Color(depth, foam, 0f, 1f);
            }
        tex.SetPixels32(px);
        tex.Apply(false);
        return tex;
    }

    // tileable value noise, 3 independent channels (water normals + foam break-up)
    static Texture2D NoiseTex(int n)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        var r = new System.Random(77);
        var px = new Color[n * n];
        float[][] g = new float[3][];
        for (int c = 0; c < 3; c++)
        {
            g[c] = new float[n * n];
            for (int oct = 0; oct < 4; oct++)
            {
                int cells = 4 << oct; float amp = 1f / (1 << oct);
                var lat = new float[cells * cells];
                for (int i = 0; i < lat.Length; i++) lat[i] = (float)r.NextDouble();
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float fx = x * cells / (float)n, fy = y * cells / (float)n;
                        int x0 = (int)fx, y0 = (int)fy; float tx = fx - x0, ty = fy - y0;
                        tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty);
                        int x1 = (x0 + 1) % cells, y1 = (y0 + 1) % cells;
                        float a = Mathf.Lerp(lat[y0 * cells + x0], lat[y0 * cells + x1], tx);
                        float b = Mathf.Lerp(lat[y1 * cells + x0], lat[y1 * cells + x1], tx);
                        g[c][y * n + x] += Mathf.Lerp(a, b, ty) * amp;
                    }
            }
        }
        for (int i = 0; i < n * n; i++) px[i] = new Color(g[0][i] / 1.875f, g[1][i] / 1.875f, g[2][i] / 1.875f, 1f);
        tex.SetPixels(px);
        tex.Apply(true);
        return tex;
    }

    // lowest-curvature sample within +/- window metres of a wanted distance
    static float FindStraight(float want, float window)
    {
        float best = want, bestBend = 1e9f;
        for (float s = want - window; s <= want + window; s += Spacing)
        {
            float b = Bend(s - 10f, 40f);
            if (b < bestBend) { bestBend = b; best = s; }
        }
        return Wrap(best);
    }

    static void Ramp(Vector3 basePos, Vector3 dir, float w, float len, float h, Color col)
    {
        var go = new GameObject("Ramp");
        go.transform.SetParent(Root, false);
        go.layer = GroundLayer;
        go.transform.position = basePos;
        go.transform.rotation = Quaternion.LookRotation(Flat(dir).normalized, Vector3.up);
        Mesh m = Geo.Wedge(w, len, h);
        go.AddComponent<MeshFilter>().sharedMesh = m;
        go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit(col);
        go.AddComponent<MeshCollider>().sharedMesh = m;
        // chevrons on the slope
        float ang = Mathf.Atan2(h, len) * Mathf.Rad2Deg;
        for (int i = 0; i < 3; i++)
        {
            float z = len * (0.25f + i * 0.25f);
            var s = Mats.Prim(PrimitiveType.Cube, go.transform, new Vector3(0f, h * z / len + 0.03f, z), new Vector3(w * 0.8f, 0.04f, 0.5f), new Vector3(-ang, 0f, 0f), Mats.Lit(Color.white));
            s.layer = 0;
        }
        // side rails
        for (int sgn = -1; sgn <= 1; sgn += 2)
        {
            var rail = Mats.Prim(PrimitiveType.Cube, go.transform, new Vector3(sgn * (w / 2 + 0.1f), h * 0.5f, len * 0.5f), new Vector3(0.25f, 0.25f, Mathf.Sqrt(len * len + h * h)), new Vector3(-ang, 0f, 0f), Mats.Lit(new Color(0.2f, 0.2f, 0.22f)));
            rail.transform.localPosition = new Vector3(sgn * (w / 2 + 0.12f), h * 0.5f + 0.1f, len * 0.5f);
            rail.layer = 0;
        }
    }

    static void BuildRamps()
    {
        // main road: one on the start straight, one on the way down from the hill
        float a = FindStraight(Length * 0.075f, 40f);
        float b = FindStraight(Length * 0.33f, 30f);
        RampS.Add(a); RampS.Add(b);
        Vector3 pa = PointAt(a, -2.8f), pb = PointAt(b, 2.8f);
        float ya, yb; Vector3 nn;
        Ground(pa, out ya, out nn); Ground(pb, out yb, out nn);
        Ramp(new Vector3(pa.x, ya - 0.02f, pa.z), TangentAt(a), 7f, 9f, 1.7f, new Color(1f, 0.55f, 0.1f));
        Ramp(new Vector3(pb.x, yb - 0.02f, pb.z), TangentAt(b), 7f, 9f, 1.7f, new Color(0.15f, 0.75f, 0.85f));
        // shortcut: launch ramp right before the lagoon gap
        Vector3 pc = ShortcutPoint(ScGapA - 9.5f);
        Vector3 dir = Flat(ShortcutPoint(ScGapA) - pc).normalized;
        float yc; Ground(pc + dir * 0.3f, out yc, out nn);
        Ramp(new Vector3(pc.x, yc - 0.02f, pc.z), dir, 7.5f, 9.5f, 2.1f, new Color(0.95f, 0.85f, 0.15f));
    }

    static void PlaceBoxes()
    {
        float[] rows = { 0.13f, 0.5f, 0.68f, 0.88f };
        foreach (float f in rows)
        {
            float s = Length * f;
            Vector3 tan = TangentAt(s);
            float yaw = Mathf.Atan2(tan.x, tan.z) * Mathf.Rad2Deg;
            for (int k = 0; k < 4; k++)
                BoxSpots.Add(new KeyValuePair<Vector3, float>(PointAt(s, -4.8f + k * 3.2f), yaw));
        }
        // a reward row on the shortcut, after the landing
        Vector3 q = ShortcutPoint(ScGapB + 14f), q2 = ShortcutPoint(ScGapB + 16f);
        Vector3 d = Flat(q2 - q).normalized, rt = new Vector3(d.z, 0f, -d.x);
        float y2 = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        BoxSpots.Add(new KeyValuePair<Vector3, float>(q - rt * 1.6f, y2));
        BoxSpots.Add(new KeyValuePair<Vector3, float>(q + rt * 1.6f, y2));
    }

    // start grid: slot 0 = pole. Two columns, staggered.
    public static void GridSpot(int slot, out Vector3 pos, out float yaw)
    {
        int row = slot / 2, col = slot % 2;
        float s = Length - 9f - row * 8f - col * 4f;
        Vector3 p = PointAt(s, col == 0 ? -3.4f : 3.4f);
        float y; Vector3 nn;
        Ground(p, out y, out nn);
        pos = new Vector3(p.x, y, p.z);
        Vector3 t = TangentAt(s);
        yaw = Mathf.Atan2(t.x, t.z) * Mathf.Rad2Deg;
    }

    // ---------------- minimap ----------------
    public static Vector2 MapUV(Vector3 p)
    {
        float size = Mathf.Max(Max.x - Min.x, Max.y - Min.y) + 40f;
        Vector2 c = (Min + Max) * 0.5f;
        return new Vector2((p.x - c.x) / size + 0.5f, (p.z - c.y) / size + 0.5f);
    }

    public static Texture2D MiniMapTex(int n)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[n * n];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);
        System.Action<Vector3, float, Color32> dot = (p, rad, col) =>
        {
            Vector2 uv = MapUV(p);
            int cx = Mathf.RoundToInt(uv.x * n), cy = Mathf.RoundToInt(uv.y * n), r = Mathf.CeilToInt(rad);
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || y < 0 || x >= n || y >= n) continue;
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) > rad * rad) continue;
                    px[y * n + x] = col;
                }
        };
        foreach (var p in C) dot(p, n / 64f + 1.6f, new Color32(20, 20, 30, 200));
        foreach (var p in SC) dot(p, n / 90f + 1f, new Color32(20, 20, 30, 170));
        foreach (var p in C) dot(p, n / 64f, new Color32(250, 235, 200, 255));
        for (int i = 0; i < SC.Length; i++)
        {
            float s = SCS[i];
            if (s > ScGapA && s < ScGapB) continue;
            dot(SC[i], n / 110f + 0.5f, new Color32(255, 210, 120, 255));
        }
        dot(C[0], n / 40f, new Color32(255, 255, 255, 255));
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }
}
