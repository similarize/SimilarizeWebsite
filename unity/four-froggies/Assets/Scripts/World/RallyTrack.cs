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
    // ffu19: ?loopold=1 builds the ffu18 loop (separate runways + circle + links, banked west lobe) for before/after shots
    public static bool Legacy;
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
        string url = Application.absoluteURL ?? "";
        Legacy = url.Contains("loopold=1");
        if (Legacy)
        {
            Figure8(staticRoot);
            Loop(staticRoot);
            LoopLinks(staticRoot);
            Debug.Log("RallyTrack: legacy (ffu18) loop");
        }
        else
        {
            BranchFrames();          // before the figure-eight: its berms tuck under where the branch covers its edge
            Figure8(staticRoot);
            Branch(staticRoot);
        }
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
        float bank = Mathf.Clamp(k * 900f, -20f, 20f) * Mathf.Clamp01(1f - s.h / 2f) * BankMul(t);
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

    // ffu19: the west lobe is flat between t -2.3 and -0.9, where the loop branch leaves and rejoins the figure-eight:
    // a banked edge would put a ridge (20 deg crease, up to 4 m high) right where the branch peels off. The fades are long
    // enough that the lobe's height change stays gentler than the existing bridge ramp.
    static float BankMul(float t)
    {
        if (Legacy) return 1f;
        t = Mathf.Atan2(Mathf.Sin(t), Mathf.Cos(t));
        if (t > 0f) return 1f;
        return Mathf.Max(1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t + 3.0f) / 0.7f)), Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t + 0.9f) / 0.3f)));
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
            if (!Legacy)
            {
                // ffu19: where the loop branch lies over this (right) edge, the berm drops straight down under it (no crease)
                float cv = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((BranchCover(R) + 0.3f) / 0.6f));
                if (cv > 0f) Rb = Vector3.Lerp(Rb, R - Vector3.up * 0.6f, cv);
            }
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
    const float LegR = 7.5f, LegW = 6f, LegShift = 7.5f;    // ffu18 loop numbers (legacy build only)
    static Vector2[] LegacyExitPath()
    {
        var o = new List<Vector2>();
        Vector2 p0 = Layout.LoopC + new Vector2(Layout.LoopRun, LegShift), p3 = LT2(Layout.ExitT), d3 = Layout.TrackDir(Layout.ExitT);
        Vector2 a = p0, b = p0 + Vector2.right * 7f, c = p3 - d3 * 7f, d = p3;
        for (int i = 0; i <= 24; i++) { float u = i / 24f, v = 1f - u; o.Add(v * v * v * a + 3f * v * v * u * b + 3f * v * u * u * c + u * u * u * d); }
        return o.ToArray();
    }
    static Vector2 LT2(float t) { Vector3 p = Layout.TrackPoint(t); return new Vector2(p.x, p.z); }

    static void LoopLinks(Transform staticRoot)
    {
        Link("Loop link in", Layout.LoopEntryPath, Layout.EntryT, true);
        Link("Loop link out", LegacyExitPath(), Layout.ExitT, false);
        // signs at the branch + merge
        Vector2 b = Layout.LoopEntryPath[8];
        Ranch.Sign(new Vector3(b.x + 4f, Gy(b.x, b.y) + 3.2f, b.y + 9f), 225f, "LOOP  >\n<size=26>this way</size>", new Color(0.7f, 0.1f, 0.1f), 6f, 2.4f);
    }

    static void Link(string name, Vector2[] path, float trackT, bool trackAtStart)
    {
        int n = path.Length;
        Sample ts = Eval(trackT);
        float hwTrack = Layout.TrackW * 0.5f, hwLoop = LegW * 0.5f + 0.5f;
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

    // ================= ffu19: the loop branch as ONE continuous road =================
    // Bill: "the track has a separation between the loop and the rest of the track". ffu18 built the loop out of four
    // separate pieces: a 7 m link ribbon (berm-coloured slopes), a 6 m runway (dirt side flaps), a 6 m circle that started
    // with full curvature at the runway's end (straight -> R 7.5 m in zero distance) with its own dark outer band and rails
    // that started in mid-air, then another runway and link. Widths, edges, materials and curvature all jumped at each joint.
    // Now the entry link, approach straight, loop (clothoid ease-in -> circular top -> clothoid ease-out, sideways shift so
    // the exit lane clears the entry lane), exit straight and merge link are ONE centreline sampled every 0.5 m with a
    // moving frame. Surface, skirts, curbs, rails and racing dots are all swept along it, so nothing changes at the joints.
    // At the figure-eight the branch is cut exactly on the track edge (its inner vertices sit on the edge polyline) instead
    // of overlapping it, and the west lobe is unbanked there (BankMul) so both roads meet flush.
    struct BF { public Vector3 p, fwd, up, right; public float hw, kT, arc; public int sec; }
    struct BRow { public Vector3 L, R; public int clip; public bool ok; }
    static BF[] br;
    static BRow[] rows;
    static int loopA, loopB;                  // first / last frame of the loop section (shared with the straights)
    static float brBaseY, brTopY, brXtop, brXmin, brXmax;
    public static Vector3 JointIn, JointOut, LinkJoin;   // demo cameras (loop start / end on the centreline, link -> straight)
    const float BrDs = 0.5f;

    static List<Vector2> Resample(List<Vector2> pts, out List<float> arcs)
    {
        var acc = new List<float> { 0f };
        for (int i = 1; i < pts.Count; i++) acc.Add(acc[i - 1] + (pts[i] - pts[i - 1]).magnitude);
        float L = acc[acc.Count - 1];
        int n = Mathf.Max(1, Mathf.CeilToInt(L / BrDs));
        var o = new List<Vector2>(); arcs = new List<float>();
        int j = 0;
        for (int i = 0; i <= n; i++)
        {
            float a = L * i / n;
            while (j < pts.Count - 2 && acc[j + 1] < a) j++;
            float u = Mathf.Clamp01((a - acc[j]) / Mathf.Max(1e-5f, acc[j + 1] - acc[j]));
            o.Add(Vector2.Lerp(pts[j], pts[j + 1], u)); arcs.Add(a);
        }
        return o;
    }

    static float PathLen(Vector2[] p) { float l = 0f; for (int i = 1; i < p.Length; i++) l += (p[i] - p[i - 1]).magnitude; return l; }

    static void EnsureSamples()
    {
        if (samples != null) return;
        samples = new Sample[N];
        for (int i = 0; i < N; i++) samples[i] = Eval(i * Mathf.PI * 2f / N);
    }

    // signed offset of p from the figure-eight centreline along the flat right vector of the nearest sample
    static float TrackLat(Vector3 p, out int idx)
    {
        float bd = 1e18f; idx = 0;
        for (int i = 0; i < N; i++)
        {
            float dx = p.x - samples[i].c.x, dz = p.z - samples[i].c.z, d = dx * dx + dz * dz;
            if (d < bd) { bd = d; idx = i; }
        }
        Sample s = samples[idx];
        Vector3 rf = new Vector3(s.right.x, 0f, s.right.z).normalized;
        return Vector3.Dot(p - s.c, rf);
    }

    // height of the figure-eight surface plane (bank-aware) at p
    static float TrackYAt(Vector3 p)
    {
        int i; float l = TrackLat(p, out i);
        Sample s = samples[i];
        float rf = new Vector2(s.right.x, s.right.z).magnitude;
        return s.c.y + l * s.right.y / Mathf.Max(0.2f, rf);
    }

    static Vector3 Edge(int j, int side)
    {
        Sample s = samples[((j % N) + N) % N];
        return s.c + s.right * (Layout.TrackW * 0.5f) * side;
    }

    static bool SegInt(Vector3 a, Vector3 b, Vector3 c, Vector3 d, out float u, out float v)
    {
        float rx = b.x - a.x, rz = b.z - a.z, sx = d.x - c.x, sz = d.z - c.z;
        float den = rx * sz - rz * sx; u = v = 0f;
        if (Mathf.Abs(den) < 1e-9f) return false;
        float wx = c.x - a.x, wz = c.z - a.z;
        u = (wx * sz - wz * sx) / den; v = (wx * rz - wz * rx) / den;
        return u >= -1e-5f && u <= 1.00001f && v >= -1e-5f && v <= 1.00001f;
    }

    // cut the part of a branch row that lies over the figure-eight off at the track edge (exact point on the edge polyline)
    static BRow ClipRow(Vector3 L, Vector3 R)
    {
        float hw = Layout.TrackW * 0.5f;
        int iL, iR; float lL = TrackLat(L, out iL), lR = TrackLat(R, out iR);
        bool inL = Mathf.Abs(lL) < hw, inR = Mathf.Abs(lR) < hw;
        var r = new BRow { L = L, R = R, clip = 0, ok = true };
        if (!inL && !inR) return r;
        r.ok = false;
        if (inL && inR) return r;
        float lout = inL ? lR : lL;
        int side = lout > 0f ? 1 : -1;
        for (int j = Mathf.Min(iL, iR) - 6; j <= Mathf.Max(iL, iR) + 6; j++)
        {
            Vector3 e0 = Edge(j, side), e1 = Edge(j + 1, side);
            float u, v;
            if (SegInt(L, R, e0, e1, out u, out v))
            {
                Vector3 pt = Vector3.Lerp(e0, e1, v);
                r.ok = true;
                if (inL) { r.L = pt; r.clip = -1; } else { r.R = pt; r.clip = 1; }
                return r;
            }
        }
        return r;
    }

    // how far (m) P lies inside the branch surface near the figure-eight (positive = covered); -1e9 when nowhere near
    static float BranchCover(Vector3 P)
    {
        if (br == null) return -1e9f;
        float best = -1e9f;
        for (int i = 0; i < br.Length; i++)
        {
            BF f = br[i];
            if (f.sec == 1 || f.kT < 0.25f || f.kT > 0.985f) continue;
            Vector3 d = P - f.p;
            if (Mathf.Abs(Vector3.Dot(d, f.fwd)) > 0.4f) continue;
            best = Mathf.Max(best, f.hw - Mathf.Abs(Vector3.Dot(d, f.right)));
        }
        return best;
    }

    static void BranchFrames()
    {
        EnsureSamples();
        Vector2 c = Layout.LoopC;
        float W = Layout.LoopW, shift = Layout.LoopShift;
        // --- clothoid loop profile: curvature ramps 0 -> 1/TopR over Ease m, holds, ramps back to 0 (local x fwd, y up)
        float km = 1f / Layout.LoopTopR, Lt = Layout.LoopEase;
        float La = (2f * Mathf.PI - km * Lt) / km, S = 2f * Lt + La;
        System.Func<float, float> K = s0 => s0 < Lt ? km * s0 / Lt : (s0 < Lt + La ? km : km * Mathf.Max(0f, S - s0) / Lt);
        int n = Mathf.CeilToInt(S / BrDs); float h = S / n;
        var prof = new List<Vector4> { Vector4.zero };     // (s, x, y, heading)
        double x = 0, y = 0, ph = 0;
        for (int i = 0; i < n; i++)
            for (int j = 0; j < 8; j++)
            {
                float s0 = (i * 8 + j) * h / 8f, hh = h / 8f;
                double phm = ph + (K(s0) + K(s0 + hh * 0.5f)) * 0.5f * hh * 0.5f;
                x += System.Math.Cos(phm) * hh; y += System.Math.Sin(phm) * hh;
                ph += (K(s0) + 4f * K(s0 + hh * 0.5f) + K(s0 + hh)) / 6f * hh;
                if (j == 7) prof.Add(new Vector4((i + 1) * h, (float)x, (float)y, (float)ph));
            }
        Vector4 top = prof[0]; brXmin = 1e9f; brXmax = -1e9f;
        foreach (var q in prof) { if (q.z > top.z) top = q; brXmin = Mathf.Min(brXmin, q.y); brXmax = Mathf.Max(brXmax, q.y); }
        brXtop = top.y;
        float Xs = c.x - top.y, Xe = Xs + prof[prof.Count - 1].y;
        brBaseY = Gy(Xs, c.y) + 0.3f;
        brTopY = brBaseY + top.z;

        var F = new List<BF>();
        // --- entry: fork -> hairpin -> approach straight
        Vector2[] ein = Layout.LoopEntryPath;
        float Lin = PathLen(ein);
        var pin = new List<Vector2>(ein);
        pin.Add(new Vector2(Xs, c.y));
        List<float> arcs;
        var rin = Resample(pin, out arcs);
        for (int i = 0; i < rin.Count; i++)
        {
            var f = new BF { sec = 0, kT = 1f - Mathf.Clamp01(arcs[i] / Lin) };
            float gy = Gy(rin[i].x, rin[i].y) + 0.3f;
            gy = Mathf.Lerp(brBaseY, gy, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Xs - rin[i].x) / 10f)));
            f.p = new Vector3(rin[i].x, gy, rin[i].y);
            F.Add(f);
        }
        loopA = F.Count - 1;
        // --- loop
        for (int i = 1; i < prof.Count; i++)
        {
            Vector4 q = prof[i];
            float lk = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((q.x - 0.5f * Lt) / (S - Lt)));
            var f = new BF { sec = 1, kT = 0f, p = new Vector3(Xs + q.y, brBaseY + q.z, c.y + shift * lk) };
            f.up = new Vector3(-Mathf.Sin(q.w), Mathf.Cos(q.w), 0f);
            F.Add(f);
        }
        loopB = F.Count - 1;
        // --- exit: straight -> merge link
        Vector2[] eout = Layout.LoopExitPath;
        float Lst = eout[0].x - Xe, Lout = PathLen(eout);
        var pout = new List<Vector2> { new Vector2(Xe, c.y + shift) };
        pout.AddRange(eout);
        var rout = Resample(pout, out arcs);
        for (int i = 1; i < rout.Count; i++)
        {
            var f = new BF { sec = 2, kT = Mathf.Clamp01((arcs[i] - Lst) / Lout) };
            float gy = Gy(rout[i].x, rout[i].y) + 0.3f;
            gy = Mathf.Lerp(brBaseY, gy, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((rout[i].x - Xe) / 10f)));
            f.p = new Vector3(rout[i].x, gy, rout[i].y);
            F.Add(f);
        }
        // --- heights near the figure-eight follow its surface; frames; widths; arc length
        br = F.ToArray();
        int m = br.Length;
        for (int i = 0; i < m; i++)
            if (br[i].sec != 1)
            {
                float bl = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((br[i].kT - 0.55f) / 0.45f));
                if (bl > 0f) br[i].p.y = Mathf.Lerp(br[i].p.y, TrackYAt(br[i].p), bl);
            }
        float arc = 0f;
        for (int i = 0; i < m; i++)
        {
            Vector3 a = br[Mathf.Max(0, i - 1)].p, b = br[Mathf.Min(m - 1, i + 1)].p;
            Vector3 fw = (b - a).normalized;
            Vector3 up = br[i].sec == 1 ? br[i].up : Vector3.up;
            Vector3 r = Vector3.Cross(up, fw).normalized;
            br[i].fwd = fw; br[i].right = r; br[i].up = Vector3.Cross(fw, r).normalized;
            br[i].hw = W * 0.5f + (Layout.TrackW * 0.5f - W * 0.5f) * Mathf.SmoothStep(0f, 1f, br[i].kT);
            if (i > 0) arc += (br[i].p - br[i - 1].p).magnitude;
            br[i].arc = arc;
        }
        // --- rows: edge points, blended onto the track plane near the forks, cut at the track edge
        rows = new BRow[m];
        for (int i = 0; i < m; i++)
        {
            BF f = br[i];
            Vector3 L = f.p - f.right * f.hw, R = f.p + f.right * f.hw;
            if (f.sec != 1 && f.kT > 0.3f)
            {
                float bl = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((f.kT - 0.45f) / 0.35f));
                if (bl > 0f) { L.y = Mathf.Lerp(L.y, TrackYAt(L), bl); R.y = Mathf.Lerp(R.y, TrackYAt(R), bl); }
                rows[i] = ClipRow(L, R);
            }
            else rows[i] = new BRow { L = L, R = R, ok = true };
        }
        JointIn = br[loopA].p; JointOut = br[loopB].p;
        LinkJoin = new Vector3(c.x - Layout.LoopRun - 6f, brBaseY, c.y);
        Debug.Log("RallyTrack: loop branch " + m + " frames, " + arc.ToString("0") + " m, loop " + S.ToString("0.0") + " m (top R " + Layout.LoopTopR + ", ease " + Lt + " m), height " + top.z.ToString("0.0") + " m, x " + Xs.ToString("0.0") + ".." + Xe.ToString("0.0"));
    }

    // a triangle pair facing roughly along wantN (works out the winding so faces and MeshCollider backfaces are right)
    static void Quad(List<int> t, List<Vector3> v, int a, int b, int c, int d, Vector3 wantN)
    {
        // a,b = row i (inner, outer), c,d = row i+1
        Vector3 n = Vector3.Cross(v[c] - v[a], v[b] - v[a]);
        if (Vector3.Dot(n, wantN) >= 0f) t.AddRange(new[] { a, c, b, b, c, d });
        else t.AddRange(new[] { a, b, c, b, d, c });
    }

    static GameObject SurfaceMesh(string name, int from, int to, int layer)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var nr = new List<Vector3>(); var t = new List<int>();
        int prev = -1;
        for (int i = from; i <= to; i++)
        {
            if (!rows[i].ok) { prev = -1; continue; }
            int k = v.Count;
            v.Add(rows[i].L); v.Add(rows[i].R);
            float vv = br[i].arc / 10f;
            uv.Add(new Vector2(0f, vv)); uv.Add(new Vector2(1f, vv));
            nr.Add(br[i].up); nr.Add(br[i].up);
            if (prev >= 0) Quad(t, v, prev, prev + 1, k, k + 1, br[i].up);
            prev = k;
        }
        var mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetNormals(nr); mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.layer = layer;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = dirt;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        return go;
    }

    static void Branch(Transform staticRoot)
    {
        int m = br.Length;
        Vector2 c = Layout.LoopC;
        // --- driving surface: Track layer on the links/straights, Stunt layer on the loop; they share the boundary rows
        // and their normals come from the frame, so the split is invisible
        SurfaceMesh("Loop branch in", 0, loopA, TrackLayer);
        SurfaceMesh("Loop", loopA, loopB, StuntLayer);
        SurfaceMesh("Loop branch out", loopB, m - 1, TrackLayer);

        // --- skirts: berm slopes to the ground on the flat, closing into a 0.45 m thick shell once the road leaves the ground
        var sv = new List<Vector3>(); var st = new List<int>();
        int sp = -1;
        for (int i = 0; i < m; i++)
        {
            if (!rows[i].ok) { sp = -1; continue; }
            BF f = br[i]; BRow r = rows[i];
            float k = f.sec == 1 ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((f.p.y - brBaseY - 0.6f) / 1.2f)) : 0f;
            Vector3 rf = new Vector3(f.right.x, 0f, f.right.z);
            rf = rf.sqrMagnitude > 1e-4f ? rf.normalized : f.right;
            float wl = r.clip == -1 ? 0f : 2.5f, wr = r.clip == 1 ? 0f : 2.5f;
            if (f.sec != 1 && f.kT > 0.3f && r.clip == 0)
            {
                int ii;
                wl = Mathf.Min(wl, Mathf.Clamp((Mathf.Abs(TrackLat(r.L, out ii)) - Layout.TrackW * 0.5f) * 0.9f, 0f, 2.5f));
                wr = Mathf.Min(wr, Mathf.Clamp((Mathf.Abs(TrackLat(r.R, out ii)) - Layout.TrackW * 0.5f) * 0.9f, 0f, 2.5f));
            }
            Vector3 Lb = SkirtPt(r.L, -1, wl, rf, f.up, k), Rb = SkirtPt(r.R, 1, wr, rf, f.up, k);
            int b0 = sv.Count;
            sv.Add(r.L); sv.Add(Lb); sv.Add(Rb); sv.Add(r.R); sv.Add(r.L - f.up * 0.45f); sv.Add(r.R - f.up * 0.45f);
            if (sp >= 0)
            {
                Quad(st, sv, sp, sp + 1, b0, b0 + 1, -f.right);
                Quad(st, sv, sp + 3, sp + 2, b0 + 3, b0 + 2, f.right);
                Quad(st, sv, sp + 4, sp + 5, b0 + 4, b0 + 5, -f.up);
            }
            sp = b0;
        }
        MakeMesh("Loop branch skirts", sv, null, st, berm, TrackLayer, false);

        // --- red / white curbs along both edges, all the way through the loop (visual only, 7 cm)
        Material red = Mats.Lit(new Color(0.82f, 0.12f, 0.1f)), white = Mats.Lit(new Color(0.93f, 0.93f, 0.9f));
        var cv = new List<Vector3>[] { new List<Vector3>(), new List<Vector3>() };
        var ct = new List<int>[] { new List<int>(), new List<int>() };
        for (int side = -1; side <= 1; side += 2)
        {
            int run0 = -1;
            for (int i = 0; i <= m; i++)
            {
                bool okc = i < m && rows[i].ok && rows[i].clip != side;
                if (okc && run0 < 0) run0 = i;
                if ((!okc || i == m) && run0 >= 0)
                {
                    CurbRun(run0, i - 1, side, cv, ct);
                    run0 = -1;
                }
            }
        }
        MakeMesh("Loop curbs red", cv[0], null, ct[0], red, TrackLayer, false);
        MakeMesh("Loop curbs white", cv[1], null, ct[1], white, TrackLayer, false);

        // --- guard rails: rise out of the ground 16 m before the loop, run through it, sink back 16 m after
        float a0 = br[loopA].arc - 16f, a1 = br[loopB].arc + 16f;
        Material railR = Mats.Lit(new Color(0.85f, 0.15f, 0.12f)), railW = Mats.Lit(new Color(0.85f, 0.85f, 0.85f));
        var rv = new List<Vector3>[] { new List<Vector3>(), new List<Vector3>() };
        var rt = new List<int>[] { new List<int>(), new List<int>() };
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < m - 1; i++)
            {
                if (br[i].arc < a0 || br[i + 1].arc > a1 || !rows[i].ok || !rows[i + 1].ok) continue;
                int col = Mathf.FloorToInt((br[i].arc + br[i + 1].arc) * 0.5f / 2.5f) & 1;
                RailQuad(i, side, a0, a1, rv[col], rt[col]);
            }
        MakeMesh("Loop rails red", rv[0], null, rt[0], railR, StuntLayer, true);
        MakeMesh("Loop rails white", rv[1], null, rt[1], railW, StuntLayer, true);

        // --- racing-line dots along the whole branch (also round the loop), only where it is clear of the figure-eight
        Material dotM = Mats.Unlit(new Color(1f, 0.85f, 0.15f));
        float nextDot = 2f;
        for (int i = 0; i < m; i++)
        {
            if (br[i].arc < nextDot) continue;
            nextDot += 4f;
            int ii;
            if (!rows[i].ok || (br[i].sec != 1 && Mathf.Abs(TrackLat(br[i].p, out ii)) < Layout.TrackW * 0.5f + 0.5f)) continue;
            var dot = Mats.Prim(PrimitiveType.Cylinder, root, br[i].p + br[i].up * 0.04f, new Vector3(0.35f, 0.01f, 0.35f), dotM, false);
            dot.transform.rotation = Quaternion.FromToRotation(Vector3.up, br[i].up);
        }

        // --- support gantry over the top of the loop (posts clear of both lanes and rails)
        float hw = Layout.LoopW * 0.5f;
        float zA = c.y - hw - 1.2f, zB = c.y + Layout.LoopShift + hw + 1.2f, barY = brTopY + 0.95f;
        Material steel = Mats.Lit(new Color(0.3f, 0.3f, 0.32f));
        foreach (float z in new[] { zA, zB })
        {
            float g = Gy(c.x, z);
            Mats.Prim(PrimitiveType.Cube, staticRoot, new Vector3(c.x, (g + barY + 0.25f) * 0.5f, z), new Vector3(0.5f, barY + 0.25f - g, 0.5f), steel, true);
        }
        Mats.Prim(PrimitiveType.Cube, staticRoot, new Vector3(c.x, barY, (zA + zB) * 0.5f), new Vector3(0.5f, 0.5f, zB - zA + 0.5f), steel, true);

        // --- loop zone (downforce / speed assist / no self-righting); centre = the top's centre of curvature
        float cy = brTopY - Layout.LoopTopR;
        float hx = Mathf.Max(brXtop - brXmin, brXmax - brXtop) + 3f;
        float hy = Mathf.Max(cy - (brBaseY - 1.5f), brTopY + 3f - cy);
        var zone = new Bounds(new Vector3(c.x, cy, c.y + Layout.LoopShift * 0.5f), new Vector3(hx * 2f, hy * 2f, Layout.LoopShift + Layout.LoopW + 4f));
        LoopZones.Add(zone);

        float gy0 = Gy(c.x - Layout.LoopRun - 4f, c.y);
        Ranch.Sign(new Vector3(c.x - Layout.LoopRun - 4f, gy0 + 3f, c.y - hw - 2.6f), 270f, "LOOP!\n<size=26>full throttle</size>", new Color(0.7f, 0.1f, 0.1f), 6f, 2.4f);
        Vector2 b = Layout.LoopEntryPath[8];
        Ranch.Sign(new Vector3(b.x + 4f, Gy(b.x, b.y) + 3.2f, b.y + 9f), 225f, "LOOP  >\n<size=26>this way</size>", new Color(0.7f, 0.1f, 0.1f), 6f, 2.4f);
        Debug.Log("RallyTrack: loop branch built (surface " + (loopA + 1) + "+" + (loopB - loopA) + "+" + (m - loopB) + " rows, curbs " + (ct[0].Count + ct[1].Count) / 3 + " tris, rails " + (rt[0].Count + rt[1].Count) / 3 + " tris)");
    }

    static Vector3 SkirtPt(Vector3 E, int side, float w, Vector3 rf, Vector3 up, float k)
    {
        if (w <= 0.05f) return E - up * 0.3f;
        Vector3 o = E + rf * w * side;
        Vector3 flat = new Vector3(o.x, Mathf.Lerp(E.y - 0.3f, Gy(o.x, o.z) - 0.3f, w / 2.5f), o.z);
        return Vector3.Lerp(flat, E - up * 0.45f, k);
    }

    static void CurbRun(int i0, int i1, int side, List<Vector3>[] cv, List<int>[] ct)
    {
        if (i1 - i0 < 2) return;
        float arc0 = br[i0].arc, arc1 = br[i1].arc;
        for (int i = i0; i < i1; i++)
        {
            int col = Mathf.FloorToInt((br[i].arc + br[i + 1].arc) * 0.5f / 1.5f) & 1;
            var v = cv[col]; var t = ct[col];
            Vector3[] A = CurbSection(i, side, arc0, arc1), B = CurbSection(i + 1, side, arc0, arc1);
            Vector3 up = br[i].up, outv = br[i].right * side;
            Vector3[] want = { outv, up, up - outv * 0.3f };
            for (int k = 0; k < 3; k++)
            {
                int b = v.Count;
                v.Add(A[k]); v.Add(A[k + 1]); v.Add(B[k]); v.Add(B[k + 1]);
                Quad(t, v, b, b + 1, b + 2, b + 3, want[k]);
            }
        }
    }

    static Vector3[] CurbSection(int i, int side, float arc0, float arc1)
    {
        BF f = br[i];
        Vector3 E = side < 0 ? rows[i].L : rows[i].R, o = f.right * side, up = f.up;
        float taper = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((f.arc - arc0) / 3f)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((arc1 - f.arc) / 3f));
        float ch = 0.07f * taper;
        return new[] { E + o * 0.03f - up * 0.06f, E + o * 0.03f + up * ch, E - o * 0.5f + up * ch, E - o * 0.68f - up * 0.015f };
    }

    static void RailQuad(int i, int side, float a0, float a1, List<Vector3> v, List<int> t)
    {
        Vector3[] A = RailSection(i, side, a0, a1), B = RailSection(i + 1, side, a0, a1);
        Vector3 up = br[i].up, outv = br[i].right * side;
        Vector3[] want = { -outv, up, outv };
        for (int k = 0; k < 3; k++)
        {
            int b = v.Count;
            v.Add(A[k]); v.Add(A[k + 1]); v.Add(B[k]); v.Add(B[k + 1]);
            Quad(t, v, b, b + 1, b + 2, b + 3, want[k]);
        }
    }

    static Vector3[] RailSection(int i, int side, float a0, float a1)
    {
        BF f = br[i];
        Vector3 E = side < 0 ? rows[i].L : rows[i].R, o = f.right * side, up = f.up;
        float hr = 1.0f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((f.arc - a0) / 5f)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((a1 - f.arc) / 5f));
        float hb = -0.12f;
        return new[] { E + o * 0.05f + up * hb, E + o * 0.05f + up * Mathf.Max(hr, hb + 0.01f), E + o * 0.3f + up * Mathf.Max(hr, hb + 0.01f), E + o * 0.3f + up * hb };
    }

    // ffu19: nearest loop-section frame (GroundVehicle steers the truck along the lane through the sideways shift;
    // the loopdrive demo pilot uses BranchNearest over the whole branch)
    public static bool LoopGuide(Vector3 pos, out Vector3 tangent, out float lateral)
    {
        tangent = Vector3.forward; lateral = 0f;
        if (Legacy || br == null) return false;
        int bi = -1; float bd = 36f;
        for (int i = loopA; i <= loopB; i++) { float d = (br[i].p - pos).sqrMagnitude; if (d < bd) { bd = d; bi = i; } }
        if (bi < 0) return false;
        tangent = br[bi].fwd; lateral = Vector3.Dot(pos - br[bi].p, br[bi].right);
        return true;
    }

    public static bool BranchNearest(Vector3 pos, out Vector3 p, out Vector3 fwd, out Vector3 right, out Vector3 up, out float arc)
    {
        p = fwd = right = up = Vector3.zero; arc = 0f;
        if (br == null) return false;
        int bi = 0; float bd = 1e18f;
        for (int i = 0; i < br.Length; i++) { float d = (br[i].p - pos).sqrMagnitude; if (d < bd) { bd = d; bi = i; } }
        p = br[bi].p; fwd = br[bi].fwd; right = br[bi].right; up = br[bi].up; arc = br[bi].arc;
        return true;
    }
    public static Vector3 BranchPoint(float arc, out Vector3 fwd)
    {
        fwd = Vector3.right;
        if (br == null) return Vector3.zero;
        for (int i = 1; i < br.Length; i++) if (br[i].arc >= arc) { fwd = br[i].fwd; return br[i].p; }
        fwd = br[br.Length - 1].fwd; return br[br.Length - 1].p;
    }
    public static float LoopArcA { get { return br != null ? br[loopA].arc : 0f; } }
    public static float LoopArcB { get { return br != null ? br[loopB].arc : 0f; } }
    public static float BranchArcEnd { get { return br != null ? br[br.Length - 1].arc : 0f; } }

    // ---------------- loop-the-loop lane ----------------
    static void Loop(Transform staticRoot)
    {
        Vector2 c = Layout.LoopC;
        float R = LegR, W = LegW, shift = LegShift;
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
        JointIn = new Vector3(c.x, baseY, c.y); JointOut = new Vector3(c.x, baseY, c.y + shift);
        LinkJoin = new Vector3(c.x - Layout.LoopRun - 6f, baseY, c.y);
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
