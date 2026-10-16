using System.Collections.Generic;
using UnityEngine;

// ffu31: the four modern camper trailers, built from primitives + extruded side profiles. Every one has roof solar
// panels, windows, a door, wheels and a hitch tongue (A-frame, coupler, jack). Static shell parts are merged per
// material under "Shell"; wheels, the jack and the toy-hauler ramp stay separate (they move).
public static class TrailerModels
{
    public static readonly Trailer.Spec[] Specs =
    {
        new Trailer.Spec { name = "Silver Bullet (Airstream-style)", shortName = "Silver Bullet", style = 0, len = 7.0f, wid = 2.4f, hgt = 2.45f, floorY = 0.58f, mass = 1300f, wheelR = 0.36f, axleZ = -0.35f, tongue = 1.55f, main = new Color(0.82f, 0.84f, 0.86f), accent = new Color(0.2f, 0.45f, 0.75f) },
        new Trailer.Spec { name = "Off-Grid Teardrop", shortName = "Teardrop", style = 1, len = 4.2f, wid = 2.0f, hgt = 1.75f, floorY = 0.72f, mass = 750f, wheelR = 0.42f, axleZ = -0.25f, tongue = 1.5f, chunky = true, main = new Color(0.62f, 0.58f, 0.44f), accent = new Color(0.95f, 0.55f, 0.15f) },
        new Trailer.Spec { name = "Toy Hauler 26 (ramp door)", shortName = "Toy Hauler", style = 2, len = 8.2f, wid = 2.5f, hgt = 2.75f, floorY = 0.6f, mass = 1800f, wheelR = 0.37f, axleZ = -0.55f, tongue = 1.6f, tandem = true, hauler = true, bayLen = 3.4f, main = new Color(0.95f, 0.95f, 0.94f), accent = new Color(0.12f, 0.3f, 0.55f) },
        new Trailer.Spec { name = "Cyber Camper (ramp hatch)", shortName = "Cyber Camper", style = 3, len = 6.6f, wid = 2.4f, hgt = 2.35f, floorY = 0.55f, mass = 1500f, wheelR = 0.38f, axleZ = -0.45f, tongue = 1.55f, hauler = true, bayLen = 2.9f, main = new Color(0.72f, 0.74f, 0.76f), accent = new Color(0.95f, 0.95f, 1f) },
    };

    static Material Lit(Color c) { return Mats.Lit(c); }
    static readonly Color Tire = new Color(0.07f, 0.07f, 0.08f), Dark = new Color(0.13f, 0.14f, 0.15f), Hub = new Color(0.6f, 0.61f, 0.63f);
    static readonly Color PanelC = new Color(0.05f, 0.08f, 0.2f), CellLine = new Color(0.32f, 0.4f, 0.55f), Frame = new Color(0.78f, 0.8f, 0.82f);
    static readonly Color Interior = new Color(0.36f, 0.37f, 0.39f), Deck = new Color(0.24f, 0.25f, 0.27f);

    static GameObject Box(Transform p, Vector3 pos, Vector3 size, Material m, Vector3 euler = default(Vector3), bool col = false)
    {
        return Mats.Prim(PrimitiveType.Cube, p, pos, size, euler, m, col);
    }

    // a convex side profile (z, y) extruded across the trailer's width; edge `skip` (from point skip to skip+1) is left
    // open (the toy hauler's rear door opening)
    public static GameObject Extrude(Transform parent, Vector2[] prof, float halfW, Material m, int skip = -1)
    {
        var verts = new List<Vector3>(); var norms = new List<Vector3>(); var tris = new List<int>();
        Vector2 c = Vector2.zero; foreach (var p in prof) c += p; c /= prof.Length;
        // side caps
        for (int s = -1; s <= 1; s += 2)
        {
            int b = verts.Count;
            Vector3 n = new Vector3(s, 0f, 0f);
            verts.Add(new Vector3(s * halfW, c.y, c.x)); norms.Add(n);
            foreach (var p in prof) { verts.Add(new Vector3(s * halfW, p.y, p.x)); norms.Add(n); }
            for (int i = 0; i < prof.Length; i++) Tri(verts, tris, b, b + 1 + i, b + 1 + (i + 1) % prof.Length, n);
        }
        // band
        for (int i = 0; i < prof.Length; i++)
        {
            if (i == skip) continue;
            Vector2 a = prof[i], bb = prof[(i + 1) % prof.Length];
            Vector2 e = bb - a; Vector2 pn = new Vector2(-e.y, e.x).normalized;
            if (Vector2.Dot(pn, (a + bb) * 0.5f - c) < 0f) pn = -pn;
            Vector3 n = new Vector3(0f, pn.y, pn.x);
            int b = verts.Count;
            verts.Add(new Vector3(-halfW, a.y, a.x)); verts.Add(new Vector3(halfW, a.y, a.x));
            verts.Add(new Vector3(halfW, bb.y, bb.x)); verts.Add(new Vector3(-halfW, bb.y, bb.x));
            for (int k = 0; k < 4; k++) norms.Add(n);
            Tri(verts, tris, b, b + 1, b + 2, n);
            Tri(verts, tris, b, b + 2, b + 3, n);
        }
        var mesh = new Mesh();
        mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        var go = new GameObject("Shell");
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = m;
        return go;
    }

    static void Tri(List<Vector3> v, List<int> t, int a, int b, int c, Vector3 n)
    {
        Vector3 x = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
        if (Vector3.Dot(x, n) < 0f) { t.Add(a); t.Add(c); t.Add(b); }
        else { t.Add(a); t.Add(b); t.Add(c); }
    }

    // a solar panel: dark cells, silver frame, cell grid; centre / size in parent space, tilted by euler
    public static void Solar(Transform p, Vector3 centre, float sx, float sz, Vector3 euler)
    {
        Transform n = Mats.Node(p, "Solar", centre);
        n.localRotation = Quaternion.Euler(euler);
        Box(n, Vector3.zero, new Vector3(sx, 0.04f, sz), Mats.Shiny(PanelC));
        Material fr = Mats.Steel(Frame);
        Box(n, new Vector3(0f, 0.012f, sz * 0.5f), new Vector3(sx + 0.04f, 0.05f, 0.04f), fr);
        Box(n, new Vector3(0f, 0.012f, -sz * 0.5f), new Vector3(sx + 0.04f, 0.05f, 0.04f), fr);
        Box(n, new Vector3(sx * 0.5f, 0.012f, 0f), new Vector3(0.04f, 0.05f, sz), fr);
        Box(n, new Vector3(-sx * 0.5f, 0.012f, 0f), new Vector3(0.04f, 0.05f, sz), fr);
        Material cl = Lit(CellLine);
        int nx = Mathf.Max(2, Mathf.RoundToInt(sx / 0.17f)), nz = Mathf.Max(2, Mathf.RoundToInt(sz / 0.17f));
        for (int i = 1; i < nx; i++) Box(n, new Vector3(-sx * 0.5f + sx * i / nx, 0.021f, 0f), new Vector3(0.012f, 0.005f, sz * 0.98f), cl);
        for (int i = 1; i < nz; i++) Box(n, new Vector3(0f, 0.021f, -sz * 0.5f + sz * i / nz), new Vector3(sx * 0.98f, 0.005f, 0.012f), cl);
    }

    static Transform WheelVis(Transform p, Vector3 pos, float r, float w, bool chunky, Color hubC)
    {
        Transform n = Mats.Node(p, "TWheel", pos);
        Mats.Prim(PrimitiveType.Cylinder, n, Vector3.zero, new Vector3(r * 2f, w * 0.5f, r * 2f), new Vector3(0f, 0f, 90f), Lit(Tire));
        Mats.Prim(PrimitiveType.Cylinder, n, Vector3.zero, new Vector3(r * 1.25f, w * 0.52f, r * 1.25f), new Vector3(0f, 0f, 90f), Mats.Steel(hubC));
        if (chunky)
            for (int k = 0; k < 10; k++)
            {
                float a = k * 36f;
                var lug = Mats.Prim(PrimitiveType.Cube, n, Quaternion.Euler(a, 0f, 0f) * new Vector3(0f, r * 0.97f, 0f), new Vector3(w * 1.04f, r * 0.14f, r * 0.3f), Lit(Tire));
                lug.transform.localRotation = Quaternion.Euler(a, 0f, 0f);
            }
        else
            for (int k = 0; k < 5; k++)
            {
                float a = k * 72f;
                var sp = Mats.Prim(PrimitiveType.Cube, n, Quaternion.Euler(a, 0f, 0f) * new Vector3(0f, r * 0.3f, 0f), new Vector3(w * 0.55f, r * 0.3f, r * 0.12f), Mats.Steel(hubC * 0.8f));
                sp.transform.localRotation = Quaternion.Euler(a, 0f, 0f);
            }
        return n;
    }

    // ---------------- build ----------------
    public static Trailer Build(Trailer.Spec s, Vector3 groundPos, float yaw)
    {
        var go = new GameObject("Trailer " + s.shortName);
        go.transform.SetPositionAndRotation(groundPos, Quaternion.Euler(0f, yaw, 0f));
        Transform t = go.transform;
        Transform shell = Mats.Node(t, "ShellParts", Vector3.zero);
        Trailer tr = go.AddComponent<Trailer>();
        float L = s.len, W = s.wid, H = s.hgt, F = s.floorY;
        float hz = L * 0.5f;
        Vector3 ball = new Vector3(0f, 0.5f, hz + s.tongue);
        tr.deck = F + 0.1f;

        switch (s.style)
        {
            case 0: SilverBullet(tr, shell, s); break;
            case 1: Teardrop(tr, shell, s); break;
            case 2: ToyHauler(tr, shell, s); break;
            case 4: CarrierModels.Camper(tr, shell, s); break;   // ffu48 Spotty's Carrier Camper
            default: Cyber(tr, shell, s); break;
        }

        // tongue: A-frame, coupler, safety chains, jack (+ propane / battery boxes on the bigger ones)
        Material frame = Lit(Dark);
        float fz = hz - 0.15f;
        for (int k = -1; k <= 1; k += 2)
        {
            Vector3 a = new Vector3(0.62f * k, 0.5f, fz), b = new Vector3(0.06f * k, 0.5f, ball.z - 0.35f);
            Vector3 mid = (a + b) * 0.5f; Vector3 d = b - a;
            Box(shell, mid, new Vector3(0.1f, 0.12f, d.magnitude), frame, new Vector3(0f, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 0f));
        }
        Box(shell, new Vector3(0f, 0.5f, ball.z - 0.18f), new Vector3(0.16f, 0.14f, 0.42f), frame);                 // coupler
        Mats.Prim(PrimitiveType.Sphere, shell, ball + new Vector3(0f, 0.06f, 0f), Vector3.one * 0.13f, Mats.Steel(Hub));
        Box(shell, new Vector3(0f, 0.36f, ball.z - 0.3f), new Vector3(0.22f, 0.02f, 0.02f), Mats.Steel(Hub));        // chain hint
        if (s.style == 0 || s.style == 2)
        {
            for (int k = -1; k <= 1; k += 2)
                Mats.Prim(PrimitiveType.Cylinder, shell, new Vector3(0.2f * k, 0.88f, hz + 0.45f), new Vector3(0.32f, 0.32f, 0.32f), Lit(new Color(0.94f, 0.94f, 0.92f)));
            Box(shell, new Vector3(0f, 0.7f, hz + 0.9f), new Vector3(0.5f, 0.32f, 0.3f), Lit(new Color(0.1f, 0.1f, 0.11f)));
        }
        else if (s.style == 1)
        {
            Box(shell, new Vector3(0f, 0.8f, hz + 0.5f), new Vector3(1.0f, 0.5f, 0.6f), Lit(new Color(0.15f, 0.15f, 0.15f)));   // tongue box
            Transform sp = Mats.Node(shell, "Spare", new Vector3(0f, 1.32f, hz + 0.5f));
            Mats.Prim(PrimitiveType.Cylinder, sp, Vector3.zero, new Vector3(0.8f, 0.12f, 0.8f), Lit(Tire));
            Box(shell, new Vector3(0.38f, 1.2f, hz + 0.35f), new Vector3(0.18f, 0.32f, 0.28f), Lit(new Color(0.85f, 0.2f, 0.1f)));   // jerry can
        }
        // jack (scaled down when hitched)
        Transform jk = Mats.Node(t, "Jack", ball + new Vector3(0.12f, 0.35f, -0.25f));
        Mats.Prim(PrimitiveType.Cylinder, jk, new Vector3(0f, -0.2f, 0f), new Vector3(0.07f, 0.2f, 0.07f), Lit(Dark));
        Box(jk, new Vector3(0f, -0.4f, 0f), new Vector3(0.18f, 0.03f, 0.18f), Lit(Dark));
        tr.jack = jk;

        // wheels
        float wx = W * 0.5f - 0.14f;
        float[] axles = s.tandem ? new[] { s.axleZ + 0.5f, s.axleZ - 0.5f } : new[] { s.axleZ };
        Color hubC = s.style == 3 ? new Color(0.75f, 0.77f, 0.8f) : Hub;
        foreach (float az in axles)
            for (int k = -1; k <= 1; k += 2)
            {
                Vector3 c = new Vector3(wx * k, s.wheelR, az);
                Transform wv = WheelVis(t, c, s.wheelR, 0.24f, s.chunky, hubC);
                tr.AddWheel(c, s.wheelR, wv);
            }
        // fenders
        foreach (float az in axles)
            for (int k = -1; k <= 1; k += 2)
                Box(shell, new Vector3((W * 0.5f - 0.1f) * k, s.wheelR * 2f + 0.06f, az), new Vector3(0.34f, 0.05f, s.wheelR * 2.4f), Lit(s.style == 1 ? Tire : Dark));

        MeshMerge.Merge(shell, true);
        tr.Init(s, ball);
        return tr;
    }

    static void Win(Transform p, Vector3 pos, Vector3 size, Vector3 euler = default(Vector3))
    {
        Box(p, pos, size, Mats.GlassTint(new Color(0.08f, 0.12f, 0.16f)), euler);
    }

    static void Lights(Transform p, float hz, float y, float halfX, bool front)
    {
        Material red = Mats.Unlit(new Color(1f, 0.12f, 0.1f)), amber = Mats.Unlit(new Color(1f, 0.6f, 0.1f));
        for (int k = -1; k <= 1; k += 2)
        {
            Box(p, new Vector3(halfX * k, y, -hz - 0.02f), new Vector3(0.28f, 0.1f, 0.05f), red);
            Box(p, new Vector3(halfX * k, y + 0.14f, -hz - 0.02f), new Vector3(0.1f, 0.06f, 0.05f), amber);
        }
    }

    // ---- 0: polished aluminium "silver bullet" ----
    static void SilverBullet(Trailer tr, Transform p, Trailer.Spec s)
    {
        float L = s.len, W = s.wid, H = s.hgt, F = s.floorY, hz = L * 0.5f;
        Material al = Mats.PBR(s.main, 0.9f, 0.85f);
        var body = Mats.Prim(PrimitiveType.Capsule, p, new Vector3(0f, F + H * 0.5f, 0f), new Vector3(W, hz, H), new Vector3(90f, 0f, 0f), al);
        Box(p, new Vector3(0f, F + 0.12f, 0f), new Vector3(W * 0.94f, 0.26f, L * 0.78f), Lit(Dark));                // belly pan
        // seam rings across the tube
        Material seam = Mats.Steel(s.main * 0.82f);
        for (float z = -hz * 0.5f; z <= hz * 0.5f + 0.01f; z += 0.7f)
            Mats.Prim(PrimitiveType.Cylinder, p, new Vector3(0f, F + H * 0.5f, z), new Vector3(W * 1.006f, 0.008f, H * 1.006f), new Vector3(90f, 0f, 0f), seam);
        // blue pinstripe band
        for (int k = -1; k <= 1; k += 2) Box(p, new Vector3((W * 0.5f + 0.005f) * k, F + H * 0.36f, 0f), new Vector3(0.01f, 0.06f, L * 0.52f), Lit(s.accent));
        // windows: sides + wrap-around front + rear
        float wy = F + H * 0.62f;
        for (int k = -1; k <= 1; k += 2)
            foreach (float z in new[] { -1.9f, 0.4f, 1.6f })
                Win(p, new Vector3((W * 0.5f - 0.02f) * k, wy, z), new Vector3(0.05f, 0.55f, 0.85f));
        Win(p, new Vector3(0f, F + H * 0.6f, hz - 0.62f), new Vector3(W * 0.62f, 0.42f, 0.05f), new Vector3(-48f, 0f, 0f));
        Win(p, new Vector3(0f, F + H * 0.62f, -hz + 0.58f), new Vector3(W * 0.42f, 0.32f, 0.05f), new Vector3(48f, 0f, 0f));
        // door (curb side = +x) with its little window + grab handle + step
        Box(p, new Vector3(W * 0.5f - 0.01f, F + 1.0f, -0.65f), new Vector3(0.05f, 1.85f, 0.72f), Mats.PBR(s.main * 0.92f, 0.8f, 0.85f));
        Win(p, new Vector3(W * 0.5f + 0.02f, F + 1.45f, -0.65f), new Vector3(0.03f, 0.35f, 0.45f));
        Box(p, new Vector3(W * 0.5f + 0.05f, F + 1.0f, -0.32f), new Vector3(0.04f, 0.04f, 0.1f), Lit(Dark));
        Box(p, new Vector3(W * 0.5f + 0.12f, F - 0.2f, -0.65f), new Vector3(0.3f, 0.05f, 0.6f), Lit(Dark));
        // awning roll along the curb side
        Mats.Prim(PrimitiveType.Cylinder, p, new Vector3(W * 0.5f - 0.05f, F + H * 0.86f, -0.2f), new Vector3(0.12f, 2.1f, 0.12f), new Vector3(90f, 0f, 0f), Lit(s.accent));
        // solar on the crown
        Solar(p, new Vector3(0f, F + H + 0.04f, 1.0f), 1.0f, 1.55f, Vector3.zero);
        Solar(p, new Vector3(0f, F + H + 0.04f, -0.75f), 1.0f, 1.55f, Vector3.zero);
        Box(p, new Vector3(0f, F + H - 0.04f, 0.12f), new Vector3(0.7f, 0.1f, 3.4f), Lit(Dark));                  // panel rails
        Lights(p, hz - 0.35f, F + 0.45f, W * 0.34f, false);
        // collider: one box (the capsule's own is removed by Prim)
        var col = new GameObject("TCol").AddComponent<BoxCollider>();
        col.transform.SetParent(tr.transform, false);
        col.center = new Vector3(0f, F + H * 0.5f, 0f); col.size = new Vector3(W, H, L - 0.2f);
    }

    // ---- 1: off-grid teardrop (lifted, chunky tyres, roof rack + solar, rear galley hatch) ----
    static void Teardrop(Trailer tr, Transform p, Trailer.Spec s)
    {
        float L = s.len, W = s.wid, H = s.hgt, F = s.floorY, hz = L * 0.5f;
        var pts = new List<Vector2>();
        float c1 = hz - 1.0f;
        pts.Add(new Vector2(-hz, 0f));
        pts.Add(new Vector2(hz, 0f));
        for (int i = 1; i <= 8; i++) { float a = i / 8f * Mathf.PI * 0.5f; pts.Add(new Vector2(c1 + 1.0f * Mathf.Cos(a), H * Mathf.Sin(a))); }
        for (int i = 1; i <= 9; i++) { float a = Mathf.PI * 0.5f + i / 10f * Mathf.PI * 0.5f; float r = c1 + hz; pts.Add(new Vector2(c1 + r * Mathf.Cos(a), H * Mathf.Pow(Mathf.Sin(a), 0.55f))); }
        pts.Add(new Vector2(-hz, 0.35f));
        var shellGo = Extrude(p, pts.ToArray(), W * 0.5f, Mats.Paint(s.main, 0.35f));
        shellGo.transform.localPosition = new Vector3(0f, F, 0f);
        // black lower band + chunky fender flares
        Box(p, new Vector3(0f, F + 0.12f, 0f), new Vector3(W + 0.02f, 0.24f, L - 0.05f), Lit(new Color(0.1f, 0.1f, 0.1f)));
        // doors both sides with round-cornered window
        for (int k = -1; k <= 1; k += 2)
        {
            Box(p, new Vector3((W * 0.5f + 0.01f) * k, F + 0.72f, 0.55f), new Vector3(0.03f, 1.0f, 0.85f), Lit(new Color(0.18f, 0.18f, 0.19f)));
            Mats.Prim(PrimitiveType.Cylinder, p, new Vector3((W * 0.5f + 0.025f) * k, F + 0.95f, 0.55f), new Vector3(0.38f, 0.01f, 0.38f), new Vector3(0f, 0f, 90f), Mats.GlassTint(new Color(0.08f, 0.12f, 0.16f)));
            Box(p, new Vector3((W * 0.5f + 0.03f) * k, F + 0.6f, 0.2f), new Vector3(0.03f, 0.04f, 0.12f), Mats.Steel(Hub));
        }
        // front window (a slit in the nose) + LED bar
        Win(p, new Vector3(0f, F + H * 0.72f, hz - 0.42f), new Vector3(W * 0.6f, 0.3f, 0.05f), new Vector3(-38f, 0f, 0f));
        Box(p, new Vector3(0f, F + H + 0.12f, 0.9f), new Vector3(W * 0.5f, 0.07f, 0.07f), Mats.Unlit(new Color(1f, 0.97f, 0.85f)));
        // rear galley hatch outline (aluminium trim)
        Material trim = Mats.Steel(new Color(0.8f, 0.8f, 0.8f));
        Box(p, new Vector3(0f, F + 0.42f, -hz - 0.01f), new Vector3(W * 0.9f, 0.04f, 0.05f), trim);
        Box(p, new Vector3(0f, F + H * 0.72f, -hz + 0.85f), new Vector3(W * 0.9f, 0.04f, 0.05f), trim, new Vector3(-62f, 0f, 0f));
        // roof rack + big panel
        for (int k = -1; k <= 1; k += 2) Box(p, new Vector3(0.75f * k, F + H + 0.06f, -0.15f), new Vector3(0.05f, 0.05f, 2.0f), Lit(Dark));
        Solar(p, new Vector3(0f, F + H + 0.1f, -0.15f), 1.5f, 1.9f, Vector3.zero);
        Lights(p, hz - 0.02f, F + 0.32f, W * 0.36f, false);
        Box(p, new Vector3(0f, F + 0.6f, -hz + 0.05f), new Vector3(0.3f, 0.12f, 0.05f), Lit(s.accent));
        var col = new GameObject("TCol").AddComponent<BoxCollider>();
        col.transform.SetParent(tr.transform, false);
        col.center = new Vector3(0f, F + H * 0.48f, 0.15f); col.size = new Vector3(W, H * 0.95f, L - 0.4f);
    }

    // shared toy-hauler bits: open-backed shell, lined bay with collider floor / walls / roof / bulkhead, rear ramp door
    static void HaulerBay(Trailer tr, Transform p, Trailer.Spec s, float rearH, float rampLen)
    {
        float L = s.len, W = s.wid, F = s.floorY, hz = L * 0.5f;
        float d = F + 0.1f;     // deck
        Transform t = tr.transform;
        Material inner = Lit(Interior), deckM = Lit(Deck);
        // colliders (+ visible lining)
        Box(p, new Vector3(0f, F + 0.05f, -hz + s.bayLen * 0.5f), new Vector3(W - 0.04f, 0.1f, s.bayLen), deckM, default(Vector3), true);
        for (int k = -1; k <= 1; k += 2)
            Box(p, new Vector3((W * 0.5f - 0.05f) * k, d + (rearH - 0.1f) * 0.5f, -hz + s.bayLen * 0.5f), new Vector3(0.06f, rearH - 0.1f, s.bayLen), inner, default(Vector3), true);
        Box(p, new Vector3(0f, F + rearH - 0.04f, -hz + s.bayLen * 0.5f), new Vector3(W - 0.1f, 0.06f, s.bayLen), inner, default(Vector3), true);
        Box(p, new Vector3(0f, d + (rearH - 0.1f) * 0.5f, -hz + s.bayLen), new Vector3(W - 0.1f, rearH - 0.1f, 0.08f), inner, default(Vector3), true);
        // tie-down rails + D-rings + wheel chocks on the deck
        for (int k = -1; k <= 1; k += 2) Box(p, new Vector3(0.82f * k, d + 0.01f, -hz + s.bayLen * 0.5f), new Vector3(0.05f, 0.02f, s.bayLen - 0.2f), Mats.Steel(Hub));
        for (int k = -1; k <= 1; k += 2) Box(p, new Vector3(0.45f * k, d + 0.08f, -hz + s.bayLen - 0.2f), new Vector3(0.18f, 0.16f, 0.22f), Lit(new Color(0.85f, 0.75f, 0.1f)));
        // interior strip light
        Box(p, new Vector3(0f, F + rearH - 0.08f, -hz + s.bayLen * 0.5f), new Vector3(0.12f, 0.02f, s.bayLen * 0.8f), Mats.Unlit(new Color(1f, 0.96f, 0.86f)));
        // collider for the solid living part in front of the bay
        var col = new GameObject("TCol").AddComponent<BoxCollider>();
        col.transform.SetParent(t, false);
        float lz0 = -hz + s.bayLen, lz1 = hz;
        col.center = new Vector3(0f, F + s.hgt * 0.5f, (lz0 + lz1) * 0.5f); col.size = new Vector3(W, s.hgt, lz1 - lz0);
        // ramp door: hinge at the rear deck edge, slab running back along -z (rotated up = closed)
        Transform hinge = Mats.Node(t, "RampHinge", new Vector3(0f, d, -hz));
        var slab = Box(hinge, new Vector3(0f, -0.04f, -rampLen * 0.5f), new Vector3(W - 0.08f, 0.08f, rampLen), Lit(s.style == 3 ? s.main : new Color(0.93f, 0.93f, 0.92f)), default(Vector3), true);
        // grippy top face: tread strips (visual)
        for (int i = 0; i < 7; i++) Box(hinge, new Vector3(0f, 0.005f, -0.2f - i * (rampLen - 0.3f) / 6f), new Vector3(W - 0.3f, 0.02f, 0.05f), Mats.Steel(Hub));
        for (int k = -1; k <= 1; k += 2) Box(hinge, new Vector3((W * 0.5f - 0.1f) * k, 0.03f, -rampLen * 0.5f), new Vector3(0.06f, 0.06f, rampLen), Lit(Dark));
        hinge.localRotation = Quaternion.Euler(90f, 0f, 0f);
        tr.rampHinge = hinge;
        tr.rampLen = rampLen;
        tr.slots.Add(new Trailer.Slot { pos = new Vector3(-0.48f, d + 0.02f, -hz + s.bayLen * 0.5f + 0.1f) });
        tr.slots.Add(new Trailer.Slot { pos = new Vector3(0.48f, d + 0.02f, -hz + s.bayLen * 0.5f + 0.1f) });
        // slot 0 doubles as the centre slot for a wide load (a riding mower): Trailer.FreeSlot handles it
    }

    // ---- 2: mid-size travel trailer, toy-hauler rear ramp door ----
    static void ToyHauler(Trailer tr, Transform p, Trailer.Spec s)
    {
        float L = s.len, W = s.wid, H = s.hgt, F = s.floorY, hz = L * 0.5f;
        float rearH = 2.4f;
        // profile (z, y): rear-top -> front -> bottom; edge 0 (rear-top .. rear) is skipped below the header
        Vector2[] prof = { new Vector2(-hz, H), new Vector2(-hz, 0f), new Vector2(hz - 0.12f, 0f), new Vector2(hz, 0.25f), new Vector2(hz, 1.35f), new Vector2(hz - 0.6f, H) };
        var sg = Extrude(p, prof, W * 0.5f, Mats.Paint(s.main, 0.6f), 0);
        sg.transform.localPosition = new Vector3(0f, F, 0f);
        Box(p, new Vector3(0f, F + rearH + (H - rearH) * 0.5f, -hz + 0.05f), new Vector3(W, H - rearH, 0.1f), Mats.Paint(s.main, 0.6f));   // header over the door
        HaulerBay(tr, p, s, rearH, 2.3f);
        // swoosh graphics (navy + grey bands sweeping up toward the front)
        for (int k = -1; k <= 1; k += 2)
        {
            float x = (W * 0.5f + 0.006f) * k;
            Box(p, new Vector3(x, F + 0.55f, -0.4f), new Vector3(0.01f, 0.16f, L * 0.8f), Lit(s.accent));
            Box(p, new Vector3(x, F + 0.85f, 1.4f), new Vector3(0.01f, 0.1f, 4.6f), Lit(new Color(0.55f, 0.57f, 0.6f)), new Vector3(-9f, 0f, 0f));
            Box(p, new Vector3(x, F + 1.3f, 2.6f), new Vector3(0.01f, 0.12f, 2.6f), Lit(s.accent), new Vector3(-20f, 0f, 0f));
            // windows on the living part
            foreach (float z in new[] { 0.3f, 2.0f })
                Win(p, new Vector3((W * 0.5f + 0.01f) * k, F + 1.65f, z), new Vector3(0.04f, 0.65f, 1.0f));
            // garage side window on the bay
            Win(p, new Vector3((W * 0.5f + 0.01f) * k, F + 1.75f, -hz + 1.7f), new Vector3(0.04f, 0.4f, 0.8f));
        }
        // entry door (curb side) + step + grab handle
        Box(p, new Vector3(W * 0.5f + 0.01f, F + 1.05f, -0.75f), new Vector3(0.04f, 1.95f, 0.75f), Mats.Paint(new Color(0.88f, 0.88f, 0.87f), 0.5f));
        Win(p, new Vector3(W * 0.5f + 0.035f, F + 1.5f, -0.75f), new Vector3(0.02f, 0.5f, 0.4f));
        Box(p, new Vector3(W * 0.5f + 0.15f, F - 0.2f, -0.75f), new Vector3(0.3f, 0.05f, 0.65f), Lit(Dark));
        // front window on the angled cap
        Win(p, new Vector3(0f, F + 1.95f, hz - 0.28f), new Vector3(W * 0.7f, 0.45f, 0.05f), new Vector3(-48f, 0f, 0f));
        // three panels on the roof
        for (int i = 0; i < 3; i++) Solar(p, new Vector3(0f, F + H + 0.06f, 1.9f - i * 1.75f), 1.1f, 1.6f, Vector3.zero);
        Box(p, new Vector3(0.9f, F + H + 0.12f, 2.8f), new Vector3(0.5f, 0.24f, 0.7f), Lit(new Color(0.9f, 0.9f, 0.9f)));   // AC unit
        Lights(p, hz, F + 0.2f, W * 0.4f, false);
    }

    // ---- 3: angular stainless "Cyber Camper" with a rear ramp hatch ----
    static void Cyber(Trailer tr, Transform p, Trailer.Spec s)
    {
        float L = s.len, W = s.wid, H = s.hgt, F = s.floorY, hz = L * 0.5f;
        float rearH = 2.05f;
        Vector2[] prof = { new Vector2(-hz, rearH), new Vector2(-hz, 0f), new Vector2(hz, 0f), new Vector2(hz, 0.55f), new Vector2(hz - 2.1f, H) };
        Material steel = Mats.PBR(s.main, 0.62f, 0.9f);
        var sg = Extrude(p, prof, W * 0.5f, steel, 0);
        sg.transform.localPosition = new Vector3(0f, F, 0f);
        HaulerBay(tr, p, s, rearH, 1.95f);
        // black lower cladding + front light bar + rear light strip
        Box(p, new Vector3(0f, F + 0.14f, 0f), new Vector3(W + 0.02f, 0.28f, L + 0.02f), Lit(new Color(0.08f, 0.08f, 0.09f)));
        float ang = Mathf.Atan2(H - 0.55f, 2.1f) * Mathf.Rad2Deg;
        Box(p, new Vector3(0f, F + 0.58f, hz + 0.01f), new Vector3(W * 0.92f, 0.04f, 0.03f), Mats.Unlit(s.accent));
        Box(p, new Vector3(0f, F + rearH - 0.12f, -hz - 0.02f), new Vector3(W * 0.92f, 0.05f, 0.03f), Mats.Unlit(new Color(1f, 0.1f, 0.08f)));
        // tinted glass band along the sloped nose + side glass
        Win(p, new Vector3(0f, F + 0.55f + (H - 0.55f) * 0.55f, hz - 2.1f * 0.55f), new Vector3(W * 0.82f, 0.05f, 1.1f), new Vector3(-ang, 0f, 0f));
        for (int k = -1; k <= 1; k += 2)
        {
            Win(p, new Vector3((W * 0.5f + 0.01f) * k, F + 1.45f, 0.95f), new Vector3(0.03f, 0.45f, 1.6f));
            // door outline (panel gaps)
            Box(p, new Vector3((W * 0.5f + 0.012f) * k, F + 0.98f, -0.25f), new Vector3(0.02f, 1.6f, 0.03f), Lit(Dark));
            Box(p, new Vector3((W * 0.5f + 0.012f) * k, F + 0.98f, 0.55f), new Vector3(0.02f, 1.6f, 0.03f), Lit(Dark));
            Box(p, new Vector3((W * 0.5f + 0.012f) * k, F + 1.78f, 0.15f), new Vector3(0.02f, 0.03f, 0.8f), Lit(Dark));
        }
        // integrated solar on the long rear slope
        float slope = Mathf.Atan2(H - rearH, L - 2.1f) * Mathf.Rad2Deg;
        for (int i = 0; i < 3; i++)
        {
            float z = hz - 2.1f - 0.75f - i * 1.42f;
            float y = rearH + (H - rearH) * ((z + hz) / (L - 2.1f));
            Solar(p, new Vector3(0f, F + y + 0.03f, z), 1.9f, 1.3f, new Vector3(-slope, 0f, 0f));
        }
    }
}
