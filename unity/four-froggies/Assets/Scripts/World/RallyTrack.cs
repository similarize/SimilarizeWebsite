using System.Collections.Generic;
using UnityEngine;

// The rally course: a raised figure-eight of packed dirt (banked lobes, a crossover bridge on pillars with
// guard rails, sloped berms and tyre walls), two kicker jumps by the underpass, and a separate loop-the-loop lane.
// Track surfaces are MeshColliders on layer 12 (Track) and 13 (Stunt = the loop); GroundVehicle reads those
// layers for a downforce / speed assist so the banking and the loop actually hold the trucks.
public static class RallyTrack
{
    public const int TrackLayer = 12, StuntLayer = 13;
    public static readonly List<Bounds> LoopZones = new List<Bounds>();
    static Material dirt, dirtDark, berm;
    static Transform root;

    const int N = 360;

    static float Gy(float x, float z) { return Ranch.GY(x, z); }

    static Texture2D DirtTex()
    {
        const int n = 128;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, true);
        t.wrapMode = TextureWrapMode.Repeat;
        t.filterMode = FilterMode.Bilinear;
        var px = new Color32[n * n];
        var r = new System.Random(77);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float v = Mathf.PerlinNoise(x * 0.06f, y * 0.06f) * 0.5f + Mathf.PerlinNoise(x * 0.23f + 9f, y * 0.23f) * 0.3f + (float)r.NextDouble() * 0.2f;
                // two darker ruts along the lane (u = across)
                float u = x / (float)n;
                float rut = Mathf.Exp(-Mathf.Pow((u - 0.32f) * 14f, 2f)) + Mathf.Exp(-Mathf.Pow((u - 0.68f) * 14f, 2f));
                Color c = Color.Lerp(new Color(0.46f, 0.34f, 0.22f), new Color(0.66f, 0.53f, 0.37f), v);
                c = Color.Lerp(c, new Color(0.33f, 0.24f, 0.16f), rut * 0.45f);
                if (r.NextDouble() < 0.025) c *= 0.75f;
                px[y * n + x] = c;
            }
        t.SetPixels32(px);
        t.Apply(true);
        return t;
    }

    static Material TexMat(Texture2D tex, Color tint)
    {
        var m = new Material(Mats.Lit(Color.white));
        m.color = tint;
        m.mainTexture = tex;
        m.SetFloat("_Glossiness", 0.08f);
        return m;
    }

    public static void Build(Transform staticRoot)
    {
        root = new GameObject("RallyTrack").transform;
        Texture2D tex = DirtTex();
        dirt = TexMat(tex, Color.white);
        dirtDark = TexMat(tex, new Color(0.8f, 0.72f, 0.62f));
        berm = Mats.Lit(new Color(0.42f, 0.33f, 0.22f));
        Figure8(staticRoot);
        Loop(staticRoot);
        LoopLinks(staticRoot);
    }

    // ---------------- figure-eight ----------------
    struct Sample { public Vector3 c, right, fwd, up; public float bank, h, ground; }

    static Sample[] samples;

    static float Curv(float t)
    {
        // signed curvature of the planar centreline (positive = turning left / counter-clockwise from above)
        float ax = Layout.TrackAx, az = Layout.TrackAz;
        float x1 = ax * Mathf.Cos(t), z1 = 2f * az * Mathf.Cos(2f * t);
        float x2 = -ax * Mathf.Sin(t), z2 = -4f * az * Mathf.Sin(2f * t);
        float sp = Mathf.Sqrt(x1 * x1 + z1 * z1);
        return (x1 * z2 - z1 * x2) / (sp * sp * sp);
    }

    static Sample Eval(float t)
    {
        var s = new Sample();
        Vector3 p = Layout.TrackPoint(t);
        Vector3 q = Layout.TrackPoint(t + 0.002f);
        s.h = Layout.TrackH(t);
        s.ground = Gy(p.x, p.z);
        Vector3 f = q - p; f.y = 0f; f.Normalize();
        // bank into the turn: up to 20 deg in the tight part of the lobes, faded out on the bridge
        float k = Curv(t);
        float bank = Mathf.Clamp(k * 900f, -20f, 20f) * Mathf.Clamp01(1f - s.h / 2f);
        s.bank = bank;
        Vector3 rightFlat = Vector3.Cross(Vector3.up, f).normalized;
        // positive curvature = left turn: raise the right (outer) edge
        Quaternion b = Quaternion.AngleAxis(bank, f);
        s.right = b * rightFlat;
        s.up = b * Vector3.up;
        // lift so the low edge stays above the ground
        float lift = Mathf.Abs(Mathf.Sin(bank * Mathf.Deg2Rad)) * Layout.TrackW * 0.5f;
        s.c = new Vector3(p.x, s.ground + 0.3f + lift + s.h, p.z);
        s.fwd = f;
        return s;
    }

    static void Figure8(Transform staticRoot)
    {
        samples = new Sample[N];
        for (int i = 0; i < N; i++) samples[i] = Eval(i * Mathf.PI * 2f / N);
        float hw = Layout.TrackW * 0.5f;

        // two meshes (so neither gets near 65k verts): surface + berms/underside
        var sv = new List<Vector3>(); var suv = new List<Vector2>(); var st = new List<int>();
        var bv = new List<Vector3>(); var bt = new List<int>();
        float vAcc = 0f;
        for (int i = 0; i <= N; i++)
        {
            Sample s = samples[i % N];
            if (i > 0) vAcc += (samples[i % N].c - samples[i - 1].c).magnitude / 10f;
            Vector3 L = s.c - s.right * hw, R = s.c + s.right * hw;
            sv.Add(L); sv.Add(R);
            suv.Add(new Vector2(0f, vAcc)); suv.Add(new Vector2(1f, vAcc));
            // berm / deck edge: low track -> sloped berm to the ground, high track -> thin deck lip
            float hb = Mathf.Clamp01((s.h - 1.0f) / 1.5f);
            Vector3 rf = new Vector3(s.right.x, 0f, s.right.z).normalized;
            Vector3 Lb = Vector3.Lerp(new Vector3(L.x - rf.x * 3f, s.ground - 0.2f, L.z - rf.z * 3f), L - Vector3.up * 0.7f, hb);
            Vector3 Rb = Vector3.Lerp(new Vector3(R.x + rf.x * 3f, s.ground - 0.2f, R.z + rf.z * 3f), R - Vector3.up * 0.7f, hb);
            bv.Add(L); bv.Add(Lb); bv.Add(Rb); bv.Add(R);
            if (i > 0)
            {
                int a = (i - 1) * 2, b = i * 2;
                st.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                int c = (i - 1) * 4, d = i * 4;
                // left berm (L -> Lb), outward facing
                bt.AddRange(new[] { c, c + 1, d, d, c + 1, d + 1 });
                // right berm (R -> Rb)
                bt.AddRange(new[] { c + 3, d + 3, c + 2, c + 2, d + 3, d + 2 });
                // underside (Lb -> Rb), facing down
                bt.AddRange(new[] { c + 1, c + 2, d + 1, d + 1, c + 2, d + 2 });
            }
        }
        var surf = MakeMesh("Track surface", sv, suv, st, dirt, TrackLayer, true);
        var sides = MakeMesh("Track berms", bv, null, bt, berm, TrackLayer, true);

        // centre dots (racing line) and guard rails / tyre walls / pillars
        Color tyre = new Color(0.08f, 0.08f, 0.09f), tyreW = new Color(0.92f, 0.92f, 0.92f);
        Material rail = Mats.Lit(new Color(0.85f, 0.85f, 0.85f));
        Material redW = Mats.Lit(new Color(0.85f, 0.15f, 0.12f));
        for (int i = 0; i < N; i += 3)
        {
            Sample s = samples[i];
            Sample n = samples[(i + 3) % N];
            Vector3 seg = n.c - s.c;
            float len = seg.magnitude;
            Quaternion rot = Quaternion.LookRotation(seg.normalized, s.up);
            if (i % 6 == 0)
            {
                var dot = Mats.Prim(PrimitiveType.Cylinder, staticRoot, s.c + s.up * 0.04f, new Vector3(0.35f, 0.01f, 0.35f), Mats.Unlit(new Color(1f, 0.85f, 0.15f)), false);
                dot.transform.rotation = Quaternion.FromToRotation(Vector3.up, s.up);
            }
            if (s.h > 1.2f)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 e = s.c + s.right * (Layout.TrackW * 0.5f + 0.15f) * side;
                    var g = Mats.Prim(PrimitiveType.Cube, staticRoot, e + s.up * 0.55f + seg * 0.5f, new Vector3(0.3f, 1.1f, len + 0.05f), (i / 3) % 2 == 0 ? rail : redW, true);
                    g.transform.rotation = rot;
                    g.layer = TrackLayer;
                    // pillars every ~6 m, kept out of the underpass lane
                    if (i % 9 == 0 && s.h > 2f && UnderpassClear(e))
                    {
                        float top = s.c.y - 0.6f, bot = s.ground - 0.2f;
                        Mats.Prim(PrimitiveType.Cube, staticRoot, new Vector3(e.x, (top + bot) * 0.5f, e.z), new Vector3(0.9f, top - bot, 0.9f), Mats.Lit(new Color(0.6f, 0.6f, 0.58f)), true);
                    }
                }
            }
            else if (Mathf.Abs(s.bank) > 8f && i % 3 == 0 && Layout.LoopLinkDist(s.c.x, s.c.z) > Layout.TrackW + 6f)
            {
                // tyre wall along the outside foot of the banked berm
                float outer = s.bank > 0f ? 1f : -1f;
                Vector3 rf = new Vector3(s.right.x, 0f, s.right.z).normalized;
                Vector3 e = new Vector3(s.c.x, 0f, s.c.z) + rf * outer * (Layout.TrackW * 0.5f + 3.6f);
                float gy = Gy(e.x, e.z);
                for (int k = 0; k < 2; k++)
                    Mats.Prim(PrimitiveType.Cylinder, staticRoot, new Vector3(e.x, gy + 0.25f + k * 0.5f, e.z), new Vector3(1.2f, 0.25f, 1.2f), Mats.Lit((i / 3 + k) % 2 == 0 ? tyre : tyreW), k == 0);
            }
        }

        // kicker jumps + landings on the low straights either side of the underpass
        foreach (float tj in new[] { Mathf.PI - 0.62f, Mathf.PI + 0.62f })
        {
            int i = Mathf.RoundToInt(tj / (Mathf.PI * 2f) * N) % N;
            Sample s = samples[i];
            Kicker(staticRoot, s, 0f, 1.5f, 6f, true);
            int j = (i + 9) % N;
            Kicker(staticRoot, samples[j], 0f, 1.5f, 7.5f, false);
        }

        // start / finish arch near where the spur joins
        {
            Sample s = samples[Mathf.RoundToInt(2.2f / (Mathf.PI * 2f) * N)];
            Quaternion rot = Quaternion.LookRotation(s.fwd, Vector3.up);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 e = new Vector3(s.c.x, 0f, s.c.z) + new Vector3(s.right.x, 0f, s.right.z).normalized * (Layout.TrackW * 0.5f + 1.2f) * side;
                float gy = Gy(e.x, e.z);
                Mats.Prim(PrimitiveType.Cube, staticRoot, new Vector3(e.x, gy + 3.5f, e.z), new Vector3(0.6f, 7f, 0.6f), Mats.Lit(new Color(0.15f, 0.15f, 0.15f)), true);
            }
            for (int k = 0; k < 8; k++)
            {
                float off = -Layout.TrackW * 0.5f - 1f + (k + 0.5f) * (Layout.TrackW + 2f) / 8f;
                var g = Mats.Prim(PrimitiveType.Cube, staticRoot, new Vector3(s.c.x, s.ground + 7.2f, s.c.z) + new Vector3(s.right.x, 0f, s.right.z).normalized * off,
                    new Vector3((Layout.TrackW + 2f) / 8f, 0.9f, 0.3f), Mats.Lit(k % 2 == 0 ? Color.white : Color.black), false);
                g.transform.rotation = rot;
            }
        }
    }

    static bool UnderpassClear(Vector3 p)
    {
        // keep pillars off the low branch (t near PI) that passes under the bridge
        for (int i = 0; i < N; i++)
        {
            if (samples[i].h > 0.5f) continue;
            Vector3 c = samples[i].c;
            if (new Vector2(p.x - c.x, p.z - c.z).magnitude < Layout.TrackW * 0.5f + 2f) return false;
        }
        return true;
    }

    // a wedge sitting on the track surface: up = kicker facing travel, !up = landing ramp falling away
    static void Kicker(Transform parent, Sample s, float offset, float height, float length, bool up)
    {
        float ang = Mathf.Atan2(height, length) * Mathf.Rad2Deg;
        Vector3 fwd = s.fwd;
        Quaternion baseRot = Quaternion.LookRotation(fwd, s.up);
        Quaternion tilt = Quaternion.Euler(up ? -ang : ang, 0f, 0f);
        float thick = 0.5f;
        Vector3 centre = s.c + s.up * (height * 0.5f - thick * 0.3f);
        var g = Mats.Prim(PrimitiveType.Cube, parent, centre, new Vector3(Layout.TrackW - 2f, thick, Mathf.Sqrt(height * height + length * length)), dirtDark, true);
        g.transform.rotation = baseRot * tilt;
        g.layer = TrackLayer;
        // solid fill under the wedge so it is not a floating plank
        var f = Mats.Prim(PrimitiveType.Cube, parent, s.c + s.up * (height * 0.25f) + fwd * (up ? length * 0.25f : -length * 0.25f), new Vector3(Layout.TrackW - 2.2f, height * 0.5f, length * 0.5f), berm, true);
        f.transform.rotation = baseRot;
        f.layer = TrackLayer;
    }

    // ---------------- ffu14: links that make the loop part of the circuit ----------------
    // A dirt ribbon (same look + Track layer downforce as the figure-eight) along Layout.LoopEntryPath / LoopExitPath.
    // At the figure-eight end it blends its height, banking and width into the track sample there and tucks 5 cm under
    // the track surface (no z-fighting); at the loop end it narrows to the runway width at runway height.
    static void LoopLinks(Transform staticRoot)
    {
        Link("Loop link in", Layout.LoopEntryPath, Layout.EntryT, true);
        Link("Loop link out", Layout.LoopExitPath, Layout.ExitT, false);
        // signs at the branch + merge
        Vector2 b = Layout.LoopEntryPath[8];
        Ranch.Sign(new Vector3(b.x + 4f, Gy(b.x, b.y) + 3.2f, b.y + 9f), 225f, "LOOP  >\n<size=26>this way</size>", new Color(0.7f, 0.1f, 0.1f), 6f, 2.4f);
    }

    static void Link(string name, Vector2[] path, float trackT, bool trackAtStart)
    {
        int n = path.Length;
        Sample ts = Eval(trackT);
        float hwTrack = Layout.TrackW * 0.5f, hwLoop = Layout.LoopW * 0.5f + 0.5f;
        var sv = new List<Vector3>(); var suv = new List<Vector2>(); var st = new List<int>();
        var bv = new List<Vector3>(); var bt = new List<int>();
        float vAcc = 0f;
        for (int i = 0; i < n; i++)
        {
            Vector2 p = path[i];
            Vector2 d2 = i < n - 1 ? (path[i + 1] - p) : (p - path[i - 1]);
            d2.Normalize();
            float u = i / (float)(n - 1);
            float kTrack = trackAtStart ? 1f - u : u;                       // 1 at the figure-eight end
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((kTrack - 0.72f) / 0.28f));
            float gy = Gy(p.x, p.y);
            Vector3 c = new Vector3(p.x, gy + 0.3f, p.y);
            Vector3 rightFlat = new Vector3(d2.y, 0f, -d2.x);
            Vector3 right = Vector3.Slerp(rightFlat, ts.right, blend);
            float y = Mathf.Lerp(c.y, ts.c.y, blend) - 0.05f * blend;
            c.y = y;
            float hw = Mathf.Lerp(hwLoop, hwTrack, Mathf.SmoothStep(0f, 1f, kTrack));
            if (i > 0) vAcc += (path[i] - path[i - 1]).magnitude / 10f;
            Vector3 L = c - right * hw, R = c + right * hw;
            sv.Add(L); sv.Add(R);
            suv.Add(new Vector2(0f, vAcc)); suv.Add(new Vector2(1f, vAcc));
            Vector3 Lb = new Vector3(L.x - rightFlat.x * 2.5f, Gy(L.x, L.z) - 0.3f, L.z - rightFlat.z * 2.5f);
            Vector3 Rb = new Vector3(R.x + rightFlat.x * 2.5f, Gy(R.x, R.z) - 0.3f, R.z + rightFlat.z * 2.5f);
            bv.Add(L); bv.Add(Lb); bv.Add(Rb); bv.Add(R);
            if (i > 0)
            {
                int a = (i - 1) * 2, b = i * 2;
                st.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                int c0 = (i - 1) * 4, d = i * 4;
                bt.AddRange(new[] { c0, c0 + 1, d, d, c0 + 1, d + 1 });
                bt.AddRange(new[] { c0 + 3, d + 3, c0 + 2, c0 + 2, d + 3, d + 2 });
            }
        }
        MakeMesh(name, sv, suv, st, dirt, TrackLayer, true);
        MakeMesh(name + " berms", bv, null, bt, berm, TrackLayer, false);
        // racing-line dots so it reads as part of the course
        for (int i = 2; i < n - 2; i += 4)
        {
            Vector2 p = path[i];
            var dot = Mats.Prim(PrimitiveType.Cylinder, root, new Vector3(p.x, sv[i * 2].y * 0.5f + sv[i * 2 + 1].y * 0.5f + 0.04f, p.y), new Vector3(0.35f, 0.01f, 0.35f), Mats.Unlit(new Color(1f, 0.85f, 0.15f)), false);
        }
        Debug.Log("RallyTrack: " + name + " " + n + " samples, " + (path[0] - path[n - 1]).magnitude.ToString("0") + " m chord");
    }

    // ---------------- loop-the-loop lane ----------------
    static void Loop(Transform staticRoot)
    {
        Vector2 c = Layout.LoopC;
        float R = Layout.LoopR, W = Layout.LoopW, shift = Layout.LoopShift;
        float gy = Gy(c.x, c.y);
        float baseY = gy + 0.3f;
        // runways in and out
        Runway(staticRoot, new Vector3(c.x - Layout.LoopRun - 6f, 0f, c.y), new Vector3(c.x, 0f, c.y), W);
        Runway(staticRoot, new Vector3(c.x, 0f, c.y + shift), new Vector3(c.x + Layout.LoopRun, 0f, c.y + shift), W);
        // the loop: inner face up (towards the loop centre)
        const int segs = 96;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        var ov = new List<Vector3>(); var ot = new List<int>();
        for (int i = 0; i <= segs; i++)
        {
            float s = i / (float)segs;
            float th = s * Mathf.PI * 2f;
            float lat = shift * Mathf.SmoothStep(0f, 1f, s);
            Vector3 p = new Vector3(c.x + R * Mathf.Sin(th), baseY + R * (1f - Mathf.Cos(th)), c.y + lat);
            Vector3 n = new Vector3(-Mathf.Sin(th), Mathf.Cos(th), 0f);   // towards the centre
            Vector3 L = p - Vector3.forward * W * 0.5f, Rr = p + Vector3.forward * W * 0.5f;
            v.Add(L); v.Add(Rr);
            uv.Add(new Vector2(0f, s * 5f)); uv.Add(new Vector2(1f, s * 5f));
            ov.Add(L - n * 0.4f); ov.Add(Rr - n * 0.4f);
            if (i > 0)
            {
                int a = (i - 1) * 2, b = i * 2;
                // winding so the face normal points to the loop centre
                tri.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                ot.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
            }
        }
        var inner = MakeMesh("Loop", v, uv, tri, dirt, StuntLayer, true);
        MakeMesh("Loop outer", ov, null, ot, berm, StuntLayer, false);
        // side rails so a wobble does not drop you off the top
        Material rail = Mats.Lit(new Color(0.9f, 0.2f, 0.15f));
        for (int i = 0; i < segs; i += 2)
        {
            float s = i / (float)segs, s2 = (i + 2) / (float)segs;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 a = LoopPt(c, R, baseY, shift, s) + Vector3.forward * (W * 0.5f + 0.1f) * side;
                Vector3 b = LoopPt(c, R, baseY, shift, s2) + Vector3.forward * (W * 0.5f + 0.1f) * side;
                float th = s * Mathf.PI * 2f;
                Vector3 n = new Vector3(-Mathf.Sin(th), Mathf.Cos(th), 0f);
                var g = Mats.Prim(PrimitiveType.Cube, staticRoot, (a + b) * 0.5f + n * 0.5f, new Vector3(0.2f, 1f, (b - a).magnitude + 0.05f), rail, true);
                g.transform.rotation = Quaternion.LookRotation((b - a).normalized, n);
                g.layer = StuntLayer;
            }
        }
        // support frame
        for (int side = -1; side <= 1; side += 2)
        {
            float z = c.y + (side < 0 ? -W * 0.5f - 0.6f : shift + W * 0.5f + 0.6f);
            Mats.Prim(PrimitiveType.Cube, staticRoot, new Vector3(c.x, gy + R + 0.3f, z), new Vector3(0.5f, R * 2f + 1.2f, 0.5f), Mats.Lit(new Color(0.3f, 0.3f, 0.32f)), true);
        }
        var zone = new Bounds(new Vector3(c.x, baseY + R, c.y + shift * 0.5f), new Vector3(R * 2f + 6f, R * 2f + 6f, W + shift + 4f));
        LoopZones.Add(zone);
        Ranch.Sign(new Vector3(c.x - Layout.LoopRun - 4f, gy + 3f, c.y - W * 0.5f - 2.5f), 270f, "LOOP!\n<size=26>full throttle</size>", new Color(0.7f, 0.1f, 0.1f), 6f, 2.4f);
    }

    static Vector3 LoopPt(Vector2 c, float R, float baseY, float shift, float s)
    {
        float th = s * Mathf.PI * 2f;
        return new Vector3(c.x + R * Mathf.Sin(th), baseY + R * (1f - Mathf.Cos(th)), c.y + shift * Mathf.SmoothStep(0f, 1f, s));
    }

    static void Runway(Transform parent, Vector3 a, Vector3 b, float w)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        int n = Mathf.CeilToInt((b - a).magnitude / 2f);
        Vector3 dir = (b - a).normalized, right = Vector3.Cross(Vector3.up, dir);
        for (int i = 0; i <= n; i++)
        {
            Vector3 p = Vector3.Lerp(a, b, i / (float)n);
            float y = Gy(p.x, p.z) + 0.3f;
            Vector3 L = new Vector3(p.x, y, p.z) - right * w * 0.5f, R = new Vector3(p.x, y, p.z) + right * w * 0.5f;
            Vector3 Lb = L - right * 1.5f - Vector3.up * 0.5f, Rb = R + right * 1.5f - Vector3.up * 0.5f;
            v.Add(Lb); v.Add(L); v.Add(R); v.Add(Rb);
            float vv = i * 0.2f;
            uv.Add(new Vector2(-0.2f, vv)); uv.Add(new Vector2(0f, vv)); uv.Add(new Vector2(1f, vv)); uv.Add(new Vector2(1.2f, vv));
            if (i > 0)
            {
                int p0 = (i - 1) * 4, p1 = i * 4;
                for (int k = 0; k < 3; k++) t.AddRange(new[] { p0 + k, p1 + k, p0 + k + 1, p0 + k + 1, p1 + k, p1 + k + 1 });
            }
        }
        MakeMesh("Runway", v, uv, t, dirt, StuntLayer, true);
    }

    static GameObject MakeMesh(string name, List<Vector3> v, List<Vector2> uv, List<int> t, Material m, int layer, bool collider)
    {
        var mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(v);
        if (uv != null) mesh.SetUVs(0, uv);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.layer = layer;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        return go;
    }
}
