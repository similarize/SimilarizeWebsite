using System.Collections.Generic;
using UnityEngine;

// lambo9 roadside realism: galvanised armco (W-beam on posts) round the outside of every real corner - and it is solid:
// karts bounce off it (Track.Barriers, Kart.BarrierCheck) - tyre walls behind the sharpest ones, floodlight poles behind
// the barriers, 3-2-1 braking boards before the big corners, and a ring of distant mountains / islands on the horizon
// (Resources/LBFar.shader: aerial-perspective haze towards the fog colour, snow caps on Snowy). All procedural; built on
// every track except Sky City's rooftops (its apron + skyline already do the job). ?gfx=low keeps the barriers (they are
// physics) but skips the far ring.
public static class Roadside
{
    public struct Barrier { public float a, b, lat; public int side; }
    static System.Random rnd;
    static float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }

    static bool Excluded(float s)
    {
        if (Track.InGap(s, 45f)) return true;
        foreach (float r in Track.RampS) { float d = Mathf.Abs(Track.WrapDist(s - r)); if (d < 40f) return true; }
        if (Track.InPipe(s - 18f) || Track.InPipe(s) || Track.InPipe(s + 18f)) return true;
        if (Track.SC != null && Track.SC.Length > 1 && (Mathf.Abs(Track.WrapDist(s - Track.ScEnterS)) < 50f || Mathf.Abs(Track.WrapDist(s - Track.ScExitS)) < 50f)) return true;
        return false;
    }

    public static void Build()
    {
        Track.Barriers.Clear();
        if (Track.C == null || Track.Apron) return;
        int th = Track.Theme;
        rnd = new System.Random(900 + th);
        var root = new GameObject("Roadside9").transform;
        root.SetParent(Track.Root, false);
        int n = Track.C.Length;
        float sp = Track.Spacing;
        // signed curvature (+ = right-hand bend), smoothed
        var k0 = new float[n]; var k = new float[n];
        for (int i = 0; i < n; i++) k0[i] = Vector3.SignedAngle(Track.T[(i - 2 + n) % n], Track.T[(i + 2) % n], Vector3.up) * Mathf.Deg2Rad / (4f * sp);
        for (int i = 0; i < n; i++) { float a = 0f; for (int j = -5; j <= 5; j++) a += k0[(i + j + n) % n]; k[i] = a / 11f; }
        // corner runs: |k| > 1/160 m, grown by 25 m each way
        var mark = new int[n];
        int grow = Mathf.CeilToInt(25f / sp);
        for (int i = 0; i < n; i++)
            if (Mathf.Abs(k[i]) > 1f / 160f)
                for (int j = -grow; j <= grow; j++) { int q = (i + j + n) % n; int sd = k[i] > 0f ? -1 : 1; if (mark[q] == 0 || Mathf.Abs(k[i]) > 1f / 300f) mark[q] = sd; }
        for (int i = 0; i < n; i++) if (mark[i] != 0 && Excluded(Track.S[i])) mark[i] = 0;
        Material steel = Mats.Steel(new Color(0.7f, 0.72f, 0.74f)), post = Mats.Lit(new Color(0.3f, 0.31f, 0.33f));
        Material tyre = Mats.Lit(new Color(0.07f, 0.07f, 0.075f)), tyreR = Mats.Lit(new Color(0.8f, 0.12f, 0.1f)), tyreW = Mats.Lit(new Color(0.88f, 0.88f, 0.86f));
        Material pole = Mats.Steel(new Color(0.55f, 0.56f, 0.58f)), lamp = Mats.Unlit(new Color(1f, 0.97f, 0.88f));
        Material board = Mats.Lit(new Color(0.92f, 0.92f, 0.9f)), stripe = Mats.Lit(new Color(0.08f, 0.08f, 0.1f));
        float lat = Track.HalfW + 1.9f;
        int barriers = 0, tyres = 0, poles = 0, boards = 0;
        int iStart = 0;
        while (iStart < n && mark[iStart] != 0) iStart++;          // start the scan on an unmarked sample (runs never split at 0)
        if (iStart >= n) iStart = 0;
        for (int c = 0; c < n; )
        {
            int i = (iStart + c) % n;
            if (mark[i] == 0) { c++; continue; }
            int side = mark[i], len = 0;
            while (c + len < n && mark[(iStart + c + len) % n] == side) len++;
            float sa = Track.S[i], sb = sa + len * sp;
            if (len * sp >= 30f)
            {
                BuildRail(root, sa, sb, side, lat, steel, post);
                Track.Barriers.Add(new Barrier { a = sa, b = sb, side = side, lat = lat });
                barriers++;
                // the sharpest corners get a tyre wall behind the armco, every run gets floodlights behind it
                float kmax = 0f; for (int j = 0; j < len; j++) kmax = Mathf.Max(kmax, Mathf.Abs(k[(i + j) % n]));
                if (kmax > 1f / 85f && tyres < 520) tyres += TyreWall(root, sa + len * sp * 0.25f, sa + len * sp * 0.8f, side, lat + 1.1f, tyre, tyreR, tyreW);
                if (th != 4 && poles < 34) for (float s = sa + 20f; s < sb - 10f && poles < 34; s += 55f) { Pole(root, s, side, lat + 1.4f, pole, lamp); poles++; }
                // braking boards 150 / 100 / 50 m before the corner (3 / 2 / 1 stripes) on the approach side
                if (kmax > 1f / 110f && boards < 30)
                    for (int b = 3; b >= 1; b--) { float s = sa + 25f - b * 50f; if (!Excluded(s)) { Board(root, s, side, Track.HalfW + 3.2f, b, board, stripe); boards++; } }
            }
            c += len;
        }
        MeshMerge.Merge(root, true, false);
        if (!Gfx.Low) FarRing(th);
        Debug.Log("Roadside9: " + barriers + " armco runs, " + tyres + " tyres, " + poles + " poles, " + boards + " boards (theme " + th + ")");
    }

    static Vector3 GroundAt(float s, float lat)
    {
        Vector3 p = Track.PointAt(s, lat);
        float y; Vector3 nn;
        if (Track.Ground(p, out y, out nn)) p.y = y; else p.y = Mathf.Max(p.y - 0.6f, Track.HeightAt(p.x, p.z));
        return p;
    }

    static void BuildRail(Transform root, float sa, float sb, int side, float lat, Material steel, Material post)
    {
        float step = 4f;
        Vector3 prev = Vector3.zero; bool has = false;
        for (float s = sa; s <= sb + 0.01f; s += step)
        {
            Vector3 g = GroundAt(s, side * lat);
            if (g.y < Track.SurfaceFloor + 0.2f) { has = false; continue; }
            Mats.Prim(PrimitiveType.Cube, root, g + Vector3.up * 0.36f, new Vector3(0.12f, 0.72f, 0.1f), post);
            Vector3 top = g + Vector3.up * 0.6f;
            if (has && (top - prev).sqrMagnitude < 40f)
            {
                Vector3 mid = (top + prev) * 0.5f, d = top - prev;
                Quaternion q = Quaternion.LookRotation(d.normalized, Vector3.up);
                var a = Mats.Prim(PrimitiveType.Cube, root, mid, new Vector3(0.06f, 0.32f, d.magnitude + 0.04f), steel);
                a.transform.rotation = q;
                var gr = Mats.Prim(PrimitiveType.Cube, root, mid + q * new Vector3(side * 0.02f, 0f, 0f), new Vector3(0.05f, 0.08f, d.magnitude + 0.04f), post);   // the W-beam's groove
                gr.transform.rotation = q;
            }
            prev = top; has = true;
        }
    }

    static int TyreWall(Transform root, float sa, float sb, int side, float lat, Material tyre, Material red, Material white)
    {
        int n = 0;
        for (float s = sa; s <= sb; s += 0.72f)
        {
            Vector3 g = GroundAt(s, side * lat);
            if (g.y < Track.SurfaceFloor + 0.2f) continue;
            for (int h = 0; h < 3; h++)
            {
                Material m = h == 2 ? (((int)(s / 0.72f)) % 2 == 0 ? red : white) : tyre;
                Mats.Prim(PrimitiveType.Cylinder, root, g + Vector3.up * (0.13f + h * 0.25f), new Vector3(0.7f, 0.12f, 0.7f), m);
                n++;
            }
        }
        return n;
    }

    static void Pole(Transform root, float s, int side, float lat, Material pole, Material lamp)
    {
        Vector3 g = GroundAt(s, side * lat);
        if (g.y < Track.SurfaceFloor + 0.2f) return;
        Vector3 inward = -Track.Flat(Track.PointAt(s, side * lat) - Track.PointAt(s, 0f)).normalized;
        Mats.Prim(PrimitiveType.Cylinder, root, g + Vector3.up * 4.5f, new Vector3(0.22f, 4.5f, 0.22f), pole);
        var arm = Mats.Prim(PrimitiveType.Cube, root, g + Vector3.up * 8.9f + inward * 1.1f, new Vector3(0.12f, 0.12f, 2.4f), pole);
        arm.transform.rotation = Quaternion.LookRotation(inward, Vector3.up);
        var head = Mats.Prim(PrimitiveType.Cube, root, g + Vector3.up * 8.78f + inward * 2.2f, new Vector3(0.5f, 0.1f, 0.9f), lamp);
        head.transform.rotation = Quaternion.LookRotation(inward, Vector3.up);
    }

    static void Board(Transform root, float s, int side, float lat, int stripes, Material board, Material stripe)
    {
        Vector3 g = GroundAt(s, side * lat);
        if (g.y < Track.SurfaceFloor + 0.2f) return;
        Vector3 back = -Track.TangentAt(s);                         // faces oncoming cars
        Quaternion q = Quaternion.LookRotation(back, Vector3.up);
        Mats.Prim(PrimitiveType.Cube, root, g + Vector3.up * 0.55f, new Vector3(0.08f, 1.1f, 0.08f), stripe);
        var b = Mats.Prim(PrimitiveType.Cube, root, g + Vector3.up * 1.45f, new Vector3(1.3f, 0.9f, 0.05f), board);
        b.transform.rotation = q;
        for (int i = 0; i < stripes; i++)
        {
            float x = (i - (stripes - 1) * 0.5f) * 0.32f;
            var st = Mats.Prim(PrimitiveType.Cube, root, g + Vector3.up * 1.45f + q * new Vector3(x, 0f, 0.035f), new Vector3(0.13f, 0.92f, 0.02f), stripe);
            st.transform.rotation = q * Quaternion.Euler(0f, 0f, 24f);
        }
    }

    // ---------------------------------------------------------------- distant horizon ring
    static void FarRing(int th)
    {
        Shader sh = Resources.Load<Shader>("LBFar");
        if (sh == null) sh = Shader.Find("LB/Far");
        if (sh == null || !sh.isSupported || th == 5) return;
        Rect tr = Track.TerrainRect;
        Vector3 c = new Vector3(tr.center.x, 0f, tr.center.y);
        float rad = Mathf.Max(tr.width, tr.height) * 0.5f + 160f;
        const int N = 160;
        var v = new List<Vector3>(); var col = new List<Color>(); var idx = new List<int>();
        Color baseC, topC; float hMin, hMax, freq;
        switch (th)
        {
            case 0: baseC = new Color(0.32f, 0.42f, 0.3f); topC = new Color(0.4f, 0.48f, 0.34f); hMin = -20f; hMax = 70f; freq = 3.5f; break;   // distant islands
            case 1: baseC = new Color(0.62f, 0.38f, 0.26f); topC = new Color(0.78f, 0.52f, 0.36f); hMin = 20f; hMax = 140f; freq = 5f; break;   // mesas
            case 2: baseC = new Color(0.2f, 0.17f, 0.16f); topC = new Color(0.33f, 0.27f, 0.25f); hMin = 30f; hMax = 190f; freq = 4f; break;
            case 3: baseC = new Color(0.36f, 0.4f, 0.48f); topC = new Color(0.95f, 0.97f, 1f); hMin = 70f; hMax = 300f; freq = 6f; break;      // snow-capped range
            default: baseC = new Color(0.16f, 0.3f, 0.14f); topC = new Color(0.24f, 0.38f, 0.2f); hMin = 30f; hMax = 130f; freq = 4.5f; break;
        }
        float seed = 13.7f + th * 5.1f;
        var ridge = new float[N + 1];
        for (int i = 0; i <= N; i++)
        {
            float a = i / (float)N;
            float u = Mathf.Cos(a * Mathf.PI * 2f) * freq + seed, w = Mathf.Sin(a * Mathf.PI * 2f) * freq + seed;
            float h = Mathf.PerlinNoise(u, w) * 0.65f + Mathf.PerlinNoise(u * 3.1f, w * 3.1f) * 0.25f + Mathf.PerlinNoise(u * 9f, w * 9f) * 0.1f;
            if (th == 1) h = Mathf.Round(h * 5f) / 5f * 0.85f + h * 0.15f;                          // flat-topped mesas
            if (th == 3 || th == 2) h = Mathf.Pow(h, 1.6f) * 1.4f;                                   // jagged peaks
            if (th == 0) h = Mathf.Max(0f, h - 0.45f) * 2.2f;                                         // islands out at sea
            ridge[i] = Mathf.Lerp(hMin, hMax, Mathf.Clamp01(h));
        }
        for (int i = 0; i <= N; i++)
        {
            float a = i / (float)N * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            float r2 = rad + (th == 0 ? 220f : 120f) + 60f * Mathf.PerlinNoise(i * 0.21f, seed);
            Vector3 lo = c + dir * (rad + 40f) + Vector3.up * (th == 0 ? -6f : -30f);
            Vector3 hi = c + dir * r2 + Vector3.up * ridge[i];
            Vector3 back = c + dir * (r2 + 260f) + Vector3.up * (ridge[i] * 0.4f);
            v.Add(lo); v.Add(hi); v.Add(back);
            float snowK = th == 3 ? Mathf.Clamp01((ridge[i] - 150f) / 90f) : 0f;
            Color cl = baseC; cl.a = 0f;
            Color ch = Color.Lerp(Color.Lerp(baseC, topC, 0.6f), topC, snowK); ch.a = 1f;
            Color cb = Color.Lerp(baseC, topC, 0.3f); cb.a = 0.6f;
            col.Add(cl); col.Add(ch); col.Add(cb);
            if (i > 0)
            {
                int p = (i - 1) * 3, q = i * 3;
                idx.Add(p); idx.Add(p + 1); idx.Add(q); idx.Add(q); idx.Add(p + 1); idx.Add(q + 1);
                idx.Add(p + 1); idx.Add(p + 2); idx.Add(q + 1); idx.Add(q + 1); idx.Add(p + 2); idx.Add(q + 2);
            }
        }
        var mesh = Track.Own(new Mesh());
        mesh.name = "LB9 far ring";
        mesh.SetVertices(v); mesh.SetColors(col); mesh.SetTriangles(idx, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var go = new GameObject("FarRing9");
        go.transform.SetParent(Track.Root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        var m = Track.Own(new Material(sh));
        m.SetFloat("_Haze", th == 3 ? 0.42f : th == 2 ? 0.5f : th == 0 ? 0.58f : 0.5f);
        mr.sharedMaterial = m;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }
}
