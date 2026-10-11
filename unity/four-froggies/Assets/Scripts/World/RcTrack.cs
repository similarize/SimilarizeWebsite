using System.Collections.Generic;
using UnityEngine;

// ffu30 FROGGY RC RACEWAY (Bill: "a little dirt track for RC cars, with four RC cars there that each froggy can control.
// You can see them all driving around their little track. A really fun little track, and they look like real RC cars").
// A raised dirt pad east of the ranch (centre C, clear of the rally track, treehouses and the robot pad) with a twisty
// 3.2 m wide circuit: start straight with a start gate + tabletop, a banked sweeper, a whoop section, three bermed hairpins
// and a gap jump; red / white pipe borders + tyre stacks; a covered driver's stand with four driver spots.
// Any froggy (human or AI) walks up to the stand and presses A / E / tap to take a car's remote: the froggy stands on the
// stand holding a transmitter and its slot camera chases the RC car (RcCar = a Vehicle whose seat is on the stand, so
// split-screen, input devices and exit all work like any other vehicle). AI froggies near the track race the spare cars;
// while a human races, driverless cars run on autopilot too, so all four are always out there together.
public class RcTrack : MonoBehaviour
{
    public static RcTrack I;
    public static readonly Vector2 C = new Vector2(140f, 28f);       // world centre of the pad (local 0,0)
    public const float W = 3.2f;                                    // track width
    public static readonly Vector2 MxC = new Vector2(141f, 2f);     // bike jump line (Bike.cs) just south of the pad
    // local domain of the pad (the skirt slopes down to the terrain over the last 2.5 m)
    const float X0 = -24f, X1 = 27f, Z0 = -20.5f, Z1 = 14.5f, Skirt = 2.5f, Res = 0.25f;

    public class Samp { public Vector2 p, t; public float s, k, h, bank, berm; }
    public readonly List<Samp> path = new List<Samp>();
    public float Length;
    public float BaseY;
    public readonly List<RcCar> cars = new List<RcCar>();
    public readonly List<Transform> seats = new List<Transform>();
    public readonly List<GameObject> remotes = new List<GameObject>();
    public Vector3 StandTop, StandFoot;
    public readonly Vector3[] Grid = new Vector3[4];
    public int startIdx;
    Transform root;
    Material trackM, fieldM;
    readonly List<Renderer> lights = new List<Renderer>();

    public static bool Reserved(float x, float z)
    {
        float lx = x - C.x, lz = z - C.y;
        if (lx > X0 - 3f && lx < X1 + 3f && lz > Z0 - 3f && lz < Z1 + 3f) return true;
        float mx = x - MxC.x, mz = z - MxC.y;
        return mx > -26f && mx < 26f && mz > -9f && mz < 9f;
    }

    public static void Build()
    {
        if (I != null) return;
        var go = new GameObject("RC Raceway");
        I = go.AddComponent<RcTrack>();
        try { I.Make(); }
        catch (System.Exception e) { Debug.LogWarning("RcTrack: build failed " + e); }
    }

    public Vector3 W3(float lx, float lz, float up = 0f) { return new Vector3(C.x + lx, Height(lx, lz) + up, C.y + lz); }
    public Vector3 W3(Vector2 l, float up = 0f) { return W3(l.x, l.y, up); }

    // ---------------- path ----------------
    static readonly Vector2[] Ctrl =
    {
        new Vector2(-12f, -11.5f), new Vector2(-2f, -11.5f), new Vector2(8f, -11.5f), new Vector2(15f, -10.5f), new Vector2(19.5f, -6f),
        new Vector2(20f, 0f), new Vector2(17f, 5.5f), new Vector2(10f, 8.2f), new Vector2(2f, 8.6f), new Vector2(-6f, 8.6f),
        new Vector2(-11.5f, 7.8f), new Vector2(-14.5f, 4.8f), new Vector2(-13f, 1.8f), new Vector2(-8.5f, 1.4f), new Vector2(-2f, 1.4f),
        new Vector2(4f, 1.2f), new Vector2(8.5f, 0.2f), new Vector2(10.2f, -2.6f), new Vector2(8.2f, -5.2f), new Vector2(3.5f, -5.6f),
        new Vector2(-4f, -5.6f), new Vector2(-11.5f, -5.8f), new Vector2(-16.5f, -6.8f), new Vector2(-17.5f, -9.8f), new Vector2(-15.2f, -11.6f)
    };
    static Vector2 CR(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float u)
    {
        float u2 = u * u, u3 = u2 * u;
        return 0.5f * ((2f * b) + (-a + c) * u + (2f * a - 5f * b + 4f * c - d) * u2 + (-a + 3f * b - 3f * c + d) * u3);
    }
    void MakePath()
    {
        var dense = new List<Vector2>();
        int n = Ctrl.Length;
        for (int i = 0; i < n; i++)
            for (int k = 0; k < 40; k++)
                dense.Add(CR(Ctrl[(i - 1 + n) % n], Ctrl[i], Ctrl[(i + 1) % n], Ctrl[(i + 2) % n], k / 40f));
        // resample every Res m by arc length
        float acc = 0f, next = 0f; path.Clear();
        for (int i = 0; i < dense.Count; i++)
        {
            Vector2 a = dense[i], b = dense[(i + 1) % dense.Count];
            float L = (b - a).magnitude;
            while (next <= acc + L)
            {
                float u = (next - acc) / Mathf.Max(1e-5f, L);
                path.Add(new Samp { p = Vector2.Lerp(a, b, u), s = next });
                next += Res;
            }
            acc += L;
        }
        Length = acc;
        int m = path.Count;
        for (int i = 0; i < m; i++) path[i].t = (path[(i + 1) % m].p - path[(i - 1 + m) % m].p).normalized;
        // signed curvature (+ = turning left), smoothed over +-1 m
        var raw = new float[m];
        for (int i = 0; i < m; i++)
        {
            Vector2 t0 = path[(i - 2 + m) % m].t, t1 = path[(i + 2) % m].t;
            float ang = Mathf.Atan2(t0.x * t1.y - t0.y * t1.x, Vector2.Dot(t0, t1));
            raw[i] = ang / (4f * Res);
        }
        for (int i = 0; i < m; i++) { float s = 0f; for (int j = -4; j <= 4; j++) s += raw[(i + j + m) % m]; path[i].k = s / 9f; }
        // features
        int tab = Nearest(new Vector2(3f, -11.5f)), gap = Nearest(new Vector2(-1f, 1.4f));
        int whA = Nearest(new Vector2(7f, 8.4f)), whB = Nearest(new Vector2(-4f, 8.6f));
        int bkA = Nearest(new Vector2(13f, -11f)), bkB = Nearest(new Vector2(14f, 7f));
        startIdx = Nearest(new Vector2(-6f, -11.5f));
        for (int i = 0; i < m; i++)
        {
            Samp q = path[i];
            float h = 0f;
            float ds = Ds(i, tab);
            // tabletop: 2.2 m up to 0.42, 3 m top, 2.5 m down
            if (ds > -3.7f && ds < -1.5f) h = 0.42f * Mathf.SmoothStep(0f, 1f, (ds + 3.7f) / 2.2f);
            else if (ds >= -1.5f && ds <= 1.5f) h = 0.42f;
            else if (ds > 1.5f && ds < 4.0f) h = 0.42f * (1f - Mathf.SmoothStep(0f, 1f, (ds - 1.5f) / 2.5f));
            // gap jump: concave kicker 1.8 m to 0.5, gap 2.2 m, landing 0.42 -> 0 over 2.4 m (0.5 m lead-in face)
            ds = Ds(i, gap);
            if (ds > -2.6f && ds <= -0.8f) h = Mathf.Max(h, 0.5f * Mathf.Pow((ds + 2.6f) / 1.8f, 1.6f));
            else if (ds > 0.9f && ds <= 1.4f) h = Mathf.Max(h, 0.42f * (ds - 0.9f) / 0.5f);
            else if (ds > 1.4f && ds < 3.8f) h = Mathf.Max(h, 0.42f * (1f - (ds - 1.4f) / 2.4f));
            // whoops
            float wa = Ds(i, whA);
            float wl = Ds(whB, whA);
            if (wa > 0f && wa < wl) { float sn = Mathf.Sin(Mathf.PI * wa / 1.2f); h = Mathf.Max(h, 0.13f * sn * sn * Mathf.Clamp01(Mathf.Min(wa, wl - wa) / 0.6f)); }
            q.h = h;
            // banked sweeper (16 deg, outside up), fading in / out over 2 m
            float ba = Ds(i, bkA), bl = Ds(bkB, bkA);
            if (ba > 0f && ba < bl) q.bank = Mathf.Tan(16f * Mathf.Deg2Rad) * Mathf.Clamp01(Mathf.Min(ba, bl - ba) / 2f);
            // berms on the tight turns (radius < 6 m)
            else if (Mathf.Abs(q.k) > 1f / 6f) q.berm = Mathf.Clamp01((Mathf.Abs(q.k) - 1f / 6f) / 0.12f);
        }
    }
    // signed arc distance from sample b to sample a along the loop (-L/2..L/2)
    float Ds(int a, int b)
    {
        float d = path[a].s - path[b].s;
        if (d > Length * 0.5f) d -= Length; if (d < -Length * 0.5f) d += Length;
        return d;
    }
    public int Nearest(Vector2 p)
    {
        int best = 0; float bd = 1e9f;
        for (int i = 0; i < path.Count; i++) { float d = (path[i].p - p).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
        return best;
    }
    // nearest sample with a coarse pass then a local refine (parallel straights are >= 6 m apart)
    public int NearestFast(Vector2 p, out float dist)
    {
        int best = 0; float bd = 1e9f; int m = path.Count;
        for (int i = 0; i < m; i += 4) { float d = (path[i].p - p).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
        int c = best;
        for (int j = -5; j <= 5; j++) { int i = (c + j + m) % m; float d = (path[i].p - p).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
        dist = Mathf.Sqrt(bd);
        return best;
    }
    public int NearestFrom(Vector2 p, int hint, int win)
    {
        int m = path.Count, best = hint; float bd = 1e9f;
        for (int j = -win; j <= win; j++) { int i = (hint + j + m) % m; float d = (path[i].p - p).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
        return best;
    }

    // surface height (world y) at a local point
    public float Height(float lx, float lz)
    {
        float y = BaseY;
        float dist; int i = NearestFast(new Vector2(lx, lz), out dist);
        Samp q = path[i];
        Vector2 rel = new Vector2(lx, lz) - q.p;
        float u = q.t.x * rel.y - q.t.y * rel.x;      // + = left of the direction of travel
        float hw = W * 0.5f;
        float fade = 1f - Mathf.SmoothStep(0f, 1f, (dist - hw) / 0.8f);
        y += q.h * fade;
        if (q.bank > 0f)
        {
            float outU = q.k > 0f ? -u : u;           // outside of the turn
            float b = q.bank * (Mathf.Clamp(outU, -hw, hw) + hw);
            if (outU > hw) b *= 1f - Mathf.SmoothStep(0f, 1f, (outU - hw) / 1.1f);
            if (outU < -hw) b = 0f;
            y += b;
        }
        if (q.berm > 0f)
        {
            float outU = q.k > 0f ? -u : u;
            float r = Mathf.SmoothStep(0f, 1f, (outU - (hw - 0.7f)) / 0.7f);
            if (outU > hw + 0.15f) r *= 1f - Mathf.SmoothStep(0f, 1f, (outU - hw - 0.15f) / 0.9f);
            y += 0.34f * q.berm * r;
        }
        // skirt
        float sx = Mathf.Max(0f, Mathf.Max(X0 + Skirt - lx, lx - (X1 - Skirt))), sz = Mathf.Max(0f, Mathf.Max(Z0 + Skirt - lz, lz - (Z1 - Skirt)));
        float sk = Mathf.Clamp01(Mathf.Max(sx, sz) / Skirt);
        if (sk > 0f) y = Mathf.Lerp(y, Ranch.GY(C.x + lx, C.y + lz) - 0.25f, Mathf.SmoothStep(0f, 1f, sk));
        return y;
    }
    public float OnTrackDist(Vector3 world) { float d; NearestFast(new Vector2(world.x - C.x, world.z - C.y), out d); return d; }

    // ---------------- build ----------------
    static Texture2D DirtTex()
    {
        var t = new Texture2D(128, 128, TextureFormat.RGBA32, true);
        var px = new Color32[128 * 128];
        var r = new System.Random(77);
        for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.09f, y * 0.09f) * 0.5f + Mathf.PerlinNoise(x * 0.31f + 7f, y * 0.31f + 3f) * 0.3f + (float)r.NextDouble() * 0.2f;
                float pebble = r.NextDouble() < 0.012 ? 0.25f : 0f;
                byte v = (byte)Mathf.Clamp(150 + (n - 0.5f) * 90f + pebble * 255f, 0, 255);
                px[y * 128 + x] = new Color32(v, (byte)(v * 0.86f), (byte)(v * 0.68f), 255);
            }
        t.SetPixels32(px); t.Apply(true); t.wrapMode = TextureWrapMode.Repeat;
        return t;
    }
    static Material TexMat(Texture2D tex, Color tint)
    {
        var m = new Material(Mats.Lit(Color.white));
        m.color = tint; m.mainTexture = tex;
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.06f);
        return m;
    }

    void Make()
    {
        root = transform;
        MakePath();
        // base = highest terrain under the inner pad (the pad is a raised, levelled dirt bed)
        float hi = -1e9f;
        for (float x = X0 + Skirt; x <= X1 - Skirt; x += 2f)
            for (float z = Z0 + Skirt; z <= Z1 - Skirt; z += 2f) hi = Mathf.Max(hi, Ranch.GY(C.x + x, C.y + z));
        BaseY = hi + 0.12f;
        var tex = DirtTex();
        trackM = TexMat(tex, new Color(0.78f, 0.6f, 0.44f));
        fieldM = TexMat(tex, new Color(0.74f, 0.7f, 0.5f));
        Surface();
        Pipes();
        Tyres();
        Stand();
        Gate();
        // grid: two rows of two behind the gate
        Vector2[] g = { new Vector2(-8.6f, -10.75f), new Vector2(-8.6f, -12.25f), new Vector2(-10.6f, -10.75f), new Vector2(-10.6f, -12.25f) };
        for (int i = 0; i < 4; i++) Grid[i] = W3(g[i], 0.25f);
        for (int i = 0; i < 4; i++) cars.Add(RcCar.Spawn(this, i, Grid[i], 90f));
        Ranch.Sign(new Vector3(C.x - 6f, BaseY + 5.0f, C.y - 15.9f), 180f, "FROGGY RC RACEWAY\n<size=22>walk up to the stand: A / E / tap = take a remote</size>", new Color(0.55f, 0.18f, 0.08f), 9f, 1.8f);
        if (Look.Mobile) Mats.NoShadows(gameObject);
        Debug.Log("RcTrack: built at " + C + " base y " + BaseY.ToString("0.00") + ", lap " + Length.ToString("0") + " m, " + path.Count + " samples, " + cars.Count + " cars");
    }

    void Surface()
    {
        int nx = Mathf.RoundToInt((X1 - X0) / Res) + 1, nz = Mathf.RoundToInt((Z1 - Z0) / Res) + 1;
        var v = new Vector3[nx * nz]; var uv = new Vector2[nx * nz]; var on = new bool[nx * nz];
        for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                float lx = X0 + i * Res, lz = Z0 + j * Res;
                float y = Height(lx, lz);
                float d; NearestFast(new Vector2(lx, lz), out d);
                v[j * nx + i] = new Vector3(lx, y - BaseY, lz);
                uv[j * nx + i] = new Vector2(lx / 2.2f, lz / 2.2f);
                on[j * nx + i] = d < W * 0.5f + 0.2f;
            }
        var ta = new List<int>(); var tb = new List<int>();
        for (int j = 0; j < nz - 1; j++)
            for (int i = 0; i < nx - 1; i++)
            {
                int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                var L = (on[a] && on[b] && on[c] && on[d]) ? ta : tb;
                L.Add(a); L.Add(c); L.Add(b); L.Add(b); L.Add(c); L.Add(d);
            }
        var mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = v; mesh.uv = uv; mesh.subMeshCount = 2;
        mesh.SetTriangles(ta, 0); mesh.SetTriangles(tb, 1);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var go = new GameObject("RC Surface");
        go.transform.SetParent(root, false);
        go.transform.position = new Vector3(C.x, BaseY, C.y);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = new[] { trackM, fieldM };
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    // red / white plastic pipe borders along both edges (skipped where an edge runs onto another part of the track)
    void Pipes()
    {
        Material red = Mats.Paint(new Color(0.85f, 0.12f, 0.1f), 0.6f), white = Mats.Paint(new Color(0.93f, 0.93f, 0.9f), 0.6f);
        const float pr = 0.1f; const int ring = 8;
        foreach (int side in new[] { -1, 1 })
        {
            var vr = new List<Vector3>[2] { new List<Vector3>(), new List<Vector3>() };
            var tr = new List<int>[2] { new List<int>(), new List<int>() };
            int m = path.Count;
            for (int i = 0; i < m; i += 2)
            {
                int i2 = (i + 2) % m;
                Vector3 a, b; if (!EdgePt(i, side, out a) || !EdgePt(i2, side, out b)) continue;
                int col = (i / 12) % 2;
                var V = vr[col]; var T = tr[col];
                int b0 = V.Count;
                Vector3 dir = (b - a).normalized; if (dir.sqrMagnitude < 1e-6f) continue;
                Vector3 n1 = Vector3.Cross(dir, Vector3.up).normalized, n2 = Vector3.Cross(n1, dir);
                for (int e = 0; e < 2; e++)
                {
                    Vector3 c = e == 0 ? a : b;
                    for (int k = 0; k < ring; k++) { float an = k * Mathf.PI * 2f / ring; V.Add(c + (n1 * Mathf.Cos(an) + n2 * Mathf.Sin(an)) * pr); }
                }
                for (int k = 0; k < ring; k++)
                {
                    int k2 = (k + 1) % ring;
                    T.Add(b0 + k); T.Add(b0 + ring + k); T.Add(b0 + k2);
                    T.Add(b0 + k2); T.Add(b0 + ring + k); T.Add(b0 + ring + k2);
                }
            }
            for (int col = 0; col < 2; col++)
            {
                if (vr[col].Count == 0) continue;
                var mesh = new Mesh(); mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(vr[col]); mesh.SetTriangles(tr[col], 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
                var go = new GameObject("RC Pipes");
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = col == 0 ? red : white;
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
        }
    }
    bool EdgePt(int i, int side, out Vector3 w)
    {
        Samp q = path[i];
        Vector2 nrm = new Vector2(-q.t.y, q.t.x) * side;
        Vector2 l = q.p + nrm * (W * 0.5f + 0.16f);
        w = Vector3.zero;
        float d; int j = NearestFast(l, out d);
        if (d < W * 0.5f + 0.05f) return false;                    // on top of another bit of track
        w = new Vector3(C.x + l.x, Height(l.x, l.y) + 0.1f, C.y + l.y);
        return true;
    }

    void Tyres()
    {
        Material tyre = Mats.Lit(new Color(0.06f, 0.06f, 0.065f)), rim = Mats.Paint(new Color(0.95f, 0.8f, 0.1f), 0.5f);
        int m = path.Count;
        int placed = 0;
        for (int i = 0; i < m && placed < 14; i += 6)
        {
            Samp q = path[i];
            if (q.berm < 0.6f) continue;
            // outside of the hairpin, 0.55 m beyond the pipe
            float side = q.k > 0f ? -1f : 1f;
            Vector2 nrm = new Vector2(-q.t.y, q.t.x) * side;
            Vector2 l = q.p + nrm * (W * 0.5f + 0.75f);
            float d; NearestFast(l, out d); if (d < W * 0.5f + 0.5f) continue;
            bool clash = false;
            foreach (Transform c in root) if (c.name == "Tyres" && (new Vector2(c.position.x - C.x, c.position.z - C.y) - l).sqrMagnitude < 1.6f) clash = true;
            if (clash) continue;
            var st = new GameObject("Tyres").transform; st.SetParent(root, false); st.position = W3(l);
            for (int k = 0; k < 3; k++)
            {
                Mats.Prim(PrimitiveType.Cylinder, st, new Vector3(0f, 0.09f + k * 0.18f, 0f), new Vector3(0.62f, 0.09f, 0.62f), tyre);
                Mats.Prim(PrimitiveType.Cylinder, st, new Vector3(0f, 0.09f + k * 0.18f, 0f), new Vector3(0.36f, 0.092f, 0.36f), k == 2 ? rim : tyre);
            }
            var cc = st.gameObject.AddComponent<CapsuleCollider>(); cc.radius = 0.31f; cc.height = 0.6f; cc.center = new Vector3(0f, 0.3f, 0f);
            placed++;
        }
    }

    void Stand()
    {
        Material wood = Mats.Lit(new Color(0.55f, 0.38f, 0.22f)), dark = Mats.Lit(new Color(0.3f, 0.2f, 0.12f)), roofM = Mats.Paint(new Color(0.14f, 0.36f, 0.62f), 0.5f), rail = Mats.Steel(new Color(0.7f, 0.72f, 0.75f));
        var st = new GameObject("Driver's stand").transform; st.SetParent(root, false); st.position = new Vector3(C.x, BaseY, C.y);
        float top = 1.25f, cz = -15.7f;
        Mats.Prim(PrimitiveType.Cube, st, new Vector3(-6f, top - 0.075f, cz), new Vector3(10f, 0.15f, 2.2f), wood, true);
        for (int i = 0; i < 6; i++) for (int s = -1; s <= 1; s += 2)
            Mats.Prim(PrimitiveType.Cube, st, new Vector3(-10.8f + i * 1.95f, (top - 0.15f) * 0.5f, cz + s * 0.95f), new Vector3(0.14f, top - 0.15f, 0.14f), dark);
        Mats.Prim(PrimitiveType.Cube, st, new Vector3(-6f, top + 0.95f, cz + 1.05f), new Vector3(10f, 0.06f, 0.06f), rail, true);   // front rail
        Mats.Prim(PrimitiveType.Cube, st, new Vector3(-6f, top + 0.5f, cz + 1.05f), new Vector3(10f, 0.04f, 0.04f), rail);
        for (int i = 0; i <= 5; i++) Mats.Prim(PrimitiveType.Cube, st, new Vector3(-11f + i * 2f, top + 0.47f, cz + 1.05f), new Vector3(0.05f, 0.95f, 0.05f), rail);
        Mats.Prim(PrimitiveType.Cube, st, new Vector3(-6f, top + 0.5f, cz - 1.08f), new Vector3(10f, 1f, 0.05f), wood, true);      // back wall
        Mats.Prim(PrimitiveType.Cube, st, new Vector3(-1.02f, top + 0.5f, cz), new Vector3(0.05f, 1f, 2.2f), wood, true);           // east wall
        // stairs down the west end
        for (int k = 1; k <= 5; k++)
        {
            float y = top - 0.25f * k;
            Mats.Prim(PrimitiveType.Cube, st, new Vector3(-11f - 0.5f * k + 0.25f, y * 0.5f, cz), new Vector3(0.5f, Mathf.Max(0.05f, y), 1.4f), wood, true);
        }
        // canopy
        for (int i = 0; i < 2; i++) for (int s = -1; s <= 1; s += 2)
            Mats.Prim(PrimitiveType.Cube, st, new Vector3(i == 0 ? -10.8f : -1.2f, top + 1.3f, cz + s * 0.95f), new Vector3(0.12f, 2.6f, 0.12f), rail);
        Mats.Prim(PrimitiveType.Cube, st, new Vector3(-6f, top + 2.65f, cz), new Vector3(10.6f, 0.1f, 2.8f), roofM, true);
        Mats.Prim(PrimitiveType.Cube, st, new Vector3(-6f, top + 2.55f, cz + 1.42f), new Vector3(10.6f, 0.22f, 0.06f), Mats.Paint(new Color(0.95f, 0.8f, 0.12f), 0.5f));
        StandTop = st.TransformPoint(new Vector3(-6f, top, cz));
        StandFoot = st.TransformPoint(new Vector3(-14.2f, 0f, cz));
        StandFoot.y = Height(StandFoot.x - C.x, StandFoot.z - C.y);
        // four driver spots facing the track, each with a transmitter that shows while someone drives that car
        Material txM = Mats.Lit(new Color(0.1f, 0.1f, 0.12f)), txGrey = Mats.Steel(new Color(0.6f, 0.62f, 0.66f));
        for (int i = 0; i < 4; i++)
        {
            Transform s = Mats.Node(st, "RC seat " + i, new Vector3(-9.4f + i * 2.2f, top, cz + 0.2f));
            seats.Add(s);
            // spot marker in the car's colour
            Mats.Prim(PrimitiveType.Cylinder, s, new Vector3(0f, 0.005f, 0f), new Vector3(0.9f, 0.01f, 0.9f), Mats.Paint(Froggies.Color(i), 0.4f));
            var tx = new GameObject("Transmitter"); tx.transform.SetParent(s, false); tx.transform.localPosition = new Vector3(0f, 0.7f, 0.52f);
            tx.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            Mats.Prim(PrimitiveType.Cube, tx.transform, Vector3.zero, new Vector3(0.24f, 0.1f, 0.2f), txM);
            Mats.Prim(PrimitiveType.Cube, tx.transform, new Vector3(0f, -0.13f, -0.04f), new Vector3(0.06f, 0.2f, 0.07f), txM);       // pistol grip
            Mats.Prim(PrimitiveType.Cylinder, tx.transform, new Vector3(0.14f, 0f, 0.04f), new Vector3(0.12f, 0.02f, 0.12f), new Vector3(0f, 0f, 90f), txGrey);   // wheel
            Mats.Prim(PrimitiveType.Cube, tx.transform, new Vector3(0f, 0.06f, -0.03f), new Vector3(0.08f, 0.02f, 0.06f), Mats.Unlit(Froggies.Color(i)));
            Mats.Prim(PrimitiveType.Cylinder, tx.transform, new Vector3(-0.08f, 0.22f, -0.02f), new Vector3(0.012f, 0.18f, 0.012f), txGrey);   // antenna
            tx.SetActive(false);
            remotes.Add(tx);
        }
        var h1 = Interact.Add(StandTop + new Vector3(0f, 0f, 0f), 5.6f, "take an RC remote", f => Take(f));
        h1.enabled = f => f.world == WorldId.Ranch && f.vehicle == null;
        h1.dynLabel = f => TakeLabel(f);
        var h2 = Interact.Add(StandFoot, 2.6f, "take an RC remote", f => Take(f));
        h2.enabled = h1.enabled; h2.dynLabel = h1.dynLabel;
    }

    void Gate()
    {
        Material post = Mats.Paint(new Color(0.12f, 0.12f, 0.14f), 0.5f), blk = Mats.Lit(new Color(0.05f, 0.05f, 0.05f)), wht = Mats.Lit(new Color(0.95f, 0.95f, 0.95f));
        int gi = startIdx; Samp q = path[gi];
        Vector2 nrm = new Vector2(-q.t.y, q.t.x);
        var g = new GameObject("Start gate").transform; g.SetParent(root, false);
        g.position = W3(q.p); g.rotation = Quaternion.LookRotation(new Vector3(q.t.x, 0f, q.t.y));
        float hw = W * 0.5f + 0.4f;
        for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, g, new Vector3(s * hw, 0.8f, 0f), new Vector3(0.12f, 1.6f, 0.12f), post, true);
        Mats.Prim(PrimitiveType.Cube, g, new Vector3(0f, 1.55f, 0f), new Vector3(hw * 2f + 0.12f, 0.1f, 0.1f), post);
        for (int k = 0; k < 12; k++) for (int r = 0; r < 2; r++)
            Mats.Prim(PrimitiveType.Cube, g, new Vector3(-hw + 0.3f + k * (hw * 2f - 0.6f) / 11f, 1.32f + r * 0.14f, 0f), new Vector3((hw * 2f - 0.6f) / 11f, 0.14f, 0.03f), (k + r) % 2 == 0 ? blk : wht);
        for (int k = 0; k < 3; k++)
        {
            var l = Mats.Prim(PrimitiveType.Sphere, g, new Vector3(-0.3f + k * 0.3f, 1.75f, 0f), Vector3.one * 0.16f, Mats.Unlit(new Color(0.9f, 0.1f, 0.08f)));
            lights.Add(l.GetComponent<Renderer>());
        }
        // start / finish line painted on the dirt
        for (int k = 0; k < 10; k++) for (int r = 0; r < 2; r++)
            Mats.Prim(PrimitiveType.Cube, g, new Vector3(-W * 0.5f + 0.16f + k * W / 10f, 0.012f, -0.08f + r * 0.16f), new Vector3(W / 10f, 0.01f, 0.16f), (k + r) % 2 == 0 ? blk : wht);
    }

    // ---------------- taking a remote ----------------
    RcCar CarFor(Frog f)
    {
        int want = Mathf.Clamp(f.id, 0, 3);
        RcCar c = cars[want];
        if (c.driver == null || !c.driver.human) return c;
        foreach (RcCar o in cars) if (o.driver == null || !o.driver.human) return o;
        return null;
    }
    string TakeLabel(Frog f)
    {
        RcCar c = CarFor(f);
        return c == null ? "every RC car is taken" : "take the " + c.Title + " remote";
    }
    public void Take(Frog f)
    {
        RcCar c = CarFor(f);
        if (c == null) { f.Toast("All four RC cars are being driven!", 2f); return; }
        if (c.driver != null) c.driver.ExitVehicle();
        f.EnterVehicle(c);
        if (f.human) f.Toast(c.Title + " - you've got the remote! Race the froggies round the track.", 3.5f);
    }

    // ---------------- AI racers ----------------
    float thinkT;
    public bool HumanRacing { get; private set; }
    public float HumanNear { get; private set; }
    public static bool DemoAll;     // rctrack demo: every car races, AI froggies jump onto the stand
    void Update()
    {
        for (int i = 0; i < 4 && i < remotes.Count; i++) if (i < cars.Count) { bool on = cars[i].driver != null; if (remotes[i].activeSelf != on) remotes[i].SetActive(on); }
        thinkT -= Time.deltaTime;
        if (thinkT > 0f) return;
        thinkT = 0.5f;
        var g = Game.I;
        if (g == null || cars.Count == 0) return;
        HumanRacing = false; HumanNear = 1e9f;
        foreach (RcCar c in cars) if (c.driver != null && c.driver.human) HumanRacing = true;
        foreach (Frog f in g.frogs) if (f != null && f.human && f.world == WorldId.Ranch) HumanNear = Mathf.Min(HumanNear, (f.transform.position - StandTop).magnitude);
        bool active = HumanRacing || HumanNear < 45f || DemoAll;
        Frog p1 = Frog.P1();
        bool p1Home = p1 == null || p1.world == WorldId.Ranch;
        for (int i = 0; i < cars.Count; i++)
        {
            RcCar c = cars[i];
            Frog d = c.driver;
            if (d != null && !d.human)
            {
                // AI racers go home when the players leave, the story starts, or they get called away
                if (!active || !p1Home || Story.Active || d.callT > 0f || d.world != WorldId.Ranch) { d.ExitVehicle(); continue; }
            }
            if (d == null && active && p1Home && !Story.Active && i < g.frogs.Count)
            {
                Frog f = g.frogs[i];
                if (f != null && !f.human && !f.netPuppet && f.robotPilot == null && f.vehicle == null && f.remote == null && f.world == WorldId.Ranch && f.callT <= 0f && f.gameObject.activeInHierarchy)
                {
                    float dd = (f.transform.position - StandTop).magnitude;
                    if (dd < 9.5f || (DemoAll && dd < 400f)) { f.EnterVehicle(c); Debug.Log("RcTrack: " + f.nick + " takes the " + c.Title); }
                    else if (dd < 150f) f.RcInvite(StandFoot);
                }
            }
        }
        // nobody about: park the cars back on the grid
        if (!active)
            for (int i = 0; i < cars.Count; i++) if (cars[i].driver == null && !cars[i].Parked) cars[i].Park(Grid[i], 90f);
    }
    // autopilot for driverless cars while somebody races (so all four are out there)
    public bool AutoFor(RcCar c) { return c.driver == null ? (HumanRacing || DemoAll) : !c.driver.human; }
}
