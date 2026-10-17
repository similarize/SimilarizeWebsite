using System.Collections.Generic;
using UnityEngine;

// ffu61 APOLLO 11 LANDING SITE on the Moon world (Bill, 2026-10-17): what the first Moon landing left behind, built from our own
// primitives (no ripped meshes), at real scale, ~37 m east of the Starship pad:
//  * the Lunar Module DESCENT STAGE (the ascent stage flew back up, so only the bottom half stays): gold-foil octagonal base
//    (4.2 m across, underside 1.55 m up, top deck 3.2 m), crinkled foil panels, the black / silver underside and descent
//    engine bell, four spindly legs (primary strut + two side struts each) with round 0.94 m footpads 9.4 m apart across the
//    diagonal, bent contact probes on three legs, the ladder + porch on the front leg, and the small stainless-steel PLAQUE on
//    the ladder strut ("Here men from the planet Earth first set foot upon the Moon, July 1969 A.D. We came in peace for all
//    mankind." - A / E at the ladder leg reads it: a big readable panel + a toast).
//  * the American FLAG on its 2.4 m pole with the horizontal crossbar (no air = no wind), slightly rumpled (procedural mesh +
//    a texture drawn in code: 13 stripes, 50 stars).
//  * the laser ranging RETROREFLECTOR and the passive SEISMOMETER (solar panel wings) a little way off, and BOOTPRINTS in
//    the dust round the lander, out to the flag and to the experiments.
//  * colliders: a solid block under the stage (nothing drives under it), each leg, the footpads, the flag pole, the
//    experiments - the buggy / mechs / froggies bump into them instead of clipping through.
//  * a toast the first time each player comes near: "Apollo 11 landing site - left here in 1969 (Tranquility Base)".
// Logs "APOLLO61: ...".
public static class Apollo61
{
    public static readonly Vector2 C = new Vector2(37f, -38f);   // moon-local centre of the descent stage
    public static float Yaw;   // the ladder (front, +Z) faces the Starship pad
    public static bool Built;
    public static Vector3 Center, LadderFoot, FlagPos, PlaqueW;
    static Transform panel; static float panelT; static Frog reader;
    static readonly HashSet<Frog> greeted = new HashSet<Frog>();
    public static int Reads, Greets;
    public const float Body = 4.2f, Under = 1.55f, Top = 3.2f, PadR = 4.7f;

    // MoonWorld.Y: the ground round the site is flattened (the LM set down on a fairly flat patch)
    public static float FlatK(float x, float z) { return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((new Vector2(x - C.x, z - C.y).magnitude - 7f) / 11f)); }
    public static bool Near(float x, float z, float r) { return (new Vector2(x - C.x, z - C.y)).sqrMagnitude < r * r; }

    static float G(float lx, float lz) { return MoonWorld.Y(lx, lz); }
    static Vector3 W(Vector3 local) { Vector3 w = Quaternion.Euler(0f, Yaw, 0f) * local; return MoonWorld.L(C.x + w.x, 0f, C.y + w.z) + Vector3.up * G(C.x + w.x, C.y + w.z) + Vector3.up * local.y; }

    public static void Build(Transform st)
    {
        if (Built) return;
        Built = true;
        Vector2 to = MoonWorld.PadP - C;
        Yaw = Mathf.Atan2(to.x, to.y) * Mathf.Rad2Deg;
        float gy = G(C.x, C.y);
        Center = MoonWorld.L(C.x, gy, C.y);
        Transform root = new GameObject("Apollo 11 landing site").transform; root.SetParent(st, false);
        root.position = Center; root.rotation = Quaternion.Euler(0f, Yaw, 0f);
        DescentStage(root);
        Flag(st);
        Experiments(st);
        Bootprints(st);
        // read the plaque at the ladder leg; the flag
        LadderFoot = W(new Vector3(0f, 0f, PadR - 0.2f + 0.9f));
        var h = Interact.Add(LadderFoot, 2.4f, "read the plaque on the ladder leg (Apollo 11, 1969)", f => ReadPlaque(f));
        h.enabled = f => f.world == WorldId.Moon && f.vehicle == null && f.passengerOf == null;
        var fh = Interact.Add(FlagPos, 1.8f, "look at the Apollo 11 flag", f => f.Toast("Apollo 11's American flag, planted July 20, 1969 - a crossbar holds it out because there's no air on the Moon to make it wave", 5f));
        fh.enabled = f => f.world == WorldId.Moon && f.vehicle == null && f.passengerOf == null;
        Debug.Log("APOLLO61: Apollo 11 landing site built at moon-local " + C + " (world " + Center.ToString("0.0") + ") yaw " + Yaw.ToString("0") + " - descent stage " + Body + " m across, underside " + Under + " m, deck " + Top + " m, footpads " + (PadR * 2f) + " m apart (diagonal), flag at " + FlagPos.ToString("0.0") + ", plaque at " + PlaqueW.ToString("0.00"));
    }

    // ---------------- the descent stage ----------------
    static void DescentStage(Transform r)
    {
        Material gold = Mats.Shiny(new Color(0.86f, 0.66f, 0.2f)), amber = Mats.Shiny(new Color(0.72f, 0.5f, 0.16f)), pale = Mats.Shiny(new Color(0.93f, 0.8f, 0.42f));
        Material black = Mats.Lit(new Color(0.09f, 0.09f, 0.1f)), silver = Mats.Steel(new Color(0.78f, 0.78f, 0.8f)), strut = Mats.Shiny(new Color(0.8f, 0.68f, 0.38f));
        Material grey = Mats.Paint(new Color(0.6f, 0.6f, 0.6f), 0.4f), foilS = Mats.Steel(new Color(0.72f, 0.7f, 0.66f));
        float h = Top - Under, cy = Under + h * 0.5f;
        // octagonal body = a square + the same square turned 45 deg (gold foil), slightly smaller inner box = the black underside
        Mats.Prim(PrimitiveType.Cube, r, new Vector3(0f, cy, 0f), new Vector3(Body * 0.83f, h, Body * 0.83f), gold, true);
        Mats.Prim(PrimitiveType.Cube, r, new Vector3(0f, cy, 0f), new Vector3(Body * 0.83f, h - 0.02f, Body * 0.83f), new Vector3(0f, 45f, 0f), amber);
        Mats.Prim(PrimitiveType.Cube, r, new Vector3(0f, Under + 0.03f, 0f), new Vector3(Body * 0.8f, 0.07f, Body * 0.8f), black);
        Mats.Prim(PrimitiveType.Cube, r, new Vector3(0f, Under + 0.03f, 0f), new Vector3(Body * 0.8f, 0.069f, Body * 0.8f), new Vector3(0f, 45f, 0f), black);
        // crinkled foil: small panels at odd angles and shades on every face
        var rnd = new System.Random(1969);
        Material[] fm = { gold, amber, pale, foilS };
        for (int face = 0; face < 8; face++)
        {
            Quaternion q = Quaternion.Euler(0f, face * 45f, 0f);
            float rad = face % 2 == 0 ? Body * 0.415f : Body * 0.415f;
            for (int k = 0; k < 7; k++)
            {
                float u = ((float)rnd.NextDouble() - 0.5f) * 1.3f, v = Under + 0.15f + (float)rnd.NextDouble() * (h - 0.3f);
                Vector3 lp = q * new Vector3(u, 0f, rad + 0.012f); lp.y = v;
                var pnl = Mats.Prim(PrimitiveType.Cube, r, lp, new Vector3(0.3f + (float)rnd.NextDouble() * 0.5f, 0.25f + (float)rnd.NextDouble() * 0.45f, 0.02f), fm[rnd.Next(fm.Length)]);
                pnl.transform.localRotation = q * Quaternion.Euler(((float)rnd.NextDouble() - 0.5f) * 14f, ((float)rnd.NextDouble() - 0.5f) * 14f, ((float)rnd.NextDouble() - 0.5f) * 30f);
            }
        }
        // the top deck the ascent stage lifted off from: grey deck, interstage fittings, cut umbilical, MESA bay hint on the front
        Mats.Prim(PrimitiveType.Cube, r, new Vector3(0f, Top + 0.02f, 0f), new Vector3(Body * 0.78f, 0.04f, Body * 0.78f), grey);
        Mats.Prim(PrimitiveType.Cube, r, new Vector3(0f, Top + 0.02f, 0f), new Vector3(Body * 0.78f, 0.039f, Body * 0.78f), new Vector3(0f, 45f, 0f), grey);
        for (int k = 0; k < 4; k++) { Vector3 fp = Quaternion.Euler(0f, 45f + k * 90f, 0f) * new Vector3(0f, Top + 0.12f, 1.05f); Mats.Prim(PrimitiveType.Cylinder, r, fp, new Vector3(0.14f, 0.1f, 0.14f), silver); }
        Mats.Prim(PrimitiveType.Cube, r, new Vector3(0.5f, Top + 0.1f, -0.4f), new Vector3(0.5f, 0.14f, 0.3f), black);
        Mats.Prim(PrimitiveType.Cylinder, r, new Vector3(0.75f, Top + 0.25f, -0.4f), new Vector3(0.05f, 0.18f, 0.05f), new Vector3(0f, 0f, 30f), foilS);
        Mats.Prim(PrimitiveType.Cube, r, new Vector3(-0.9f, Under + 0.75f, Body * 0.415f + 0.02f), new Vector3(1.1f, 0.9f, 0.03f), foilS);   // stowage bay door (left of the ladder)
        // descent engine bell under the stage (dark, tapering) - 0.3 m clear of the dust
        Mats.Prim(PrimitiveType.Cylinder, r, new Vector3(0f, Under - 0.25f, 0f), new Vector3(1.0f, 0.25f, 1.0f), black);
        Mats.Prim(PrimitiveType.Cylinder, r, new Vector3(0f, Under - 0.75f, 0f), new Vector3(1.35f, 0.26f, 1.35f), Mats.Lit(new Color(0.16f, 0.15f, 0.15f)));
        Mats.Prim(PrimitiveType.Cylinder, r, new Vector3(0f, Under - 1.04f, 0f), new Vector3(1.48f, 0.04f, 1.48f), black);
        // solid block so nothing drives under the stage (invisible)
        var blk = new GameObject("LM collider"); blk.transform.SetParent(r, false);
        var bc = blk.AddComponent<BoxCollider>(); bc.center = new Vector3(0f, Top * 0.5f, 0f); bc.size = new Vector3(Body * 0.83f, Top, Body * 0.83f);
        // four legs on the main faces: +Z front (ladder), -Z, +X, -X
        Vector3 groundUnder = Vector3.zero;
        for (int k = 0; k < 4; k++)
        {
            Quaternion q = Quaternion.Euler(0f, k * 90f, 0f);
            Vector3 pad = q * new Vector3(0f, 0f, PadR);
            pad.y = PadY(r, pad) + 0.07f;
            Vector3 top = q * new Vector3(0f, Top - 0.3f, Body * 0.4f);
            Vector3 knee = Vector3.Lerp(pad, top, 0.42f);
            Rod(r, top, knee, 0.15f, strut, true);         // primary strut (upper, foil-wrapped)
            Rod(r, knee, pad + Vector3.up * 0.08f, 0.1f, silver, true);   // shock absorber (lower)
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 side = q * new Vector3(s * Body * 0.38f, Under + 0.1f, Body * 0.33f);
                Rod(r, side, knee + Vector3.up * 0.05f, 0.06f, strut, false);   // secondary (side) struts
            }
            Rod(r, q * new Vector3(0f, Under + 0.05f, Body * 0.4f), knee, 0.06f, strut, false);   // down-lock strut
            // round footpad (0.94 m) with a shallow cone + a little dust heaped at its rim
            Mats.Prim(PrimitiveType.Cylinder, r, pad + Vector3.up * 0.0f, new Vector3(0.94f, 0.06f, 0.94f), silver, true);
            Mats.Prim(PrimitiveType.Cylinder, r, pad + Vector3.up * 0.1f, new Vector3(0.6f, 0.06f, 0.6f), foilS);
            Mats.Prim(PrimitiveType.Sphere, r, pad + Vector3.up * 0.15f, new Vector3(0.26f, 0.14f, 0.26f), silver);
            // lunar surface sensing probes (1.7 m) - all but the ladder leg; bent over by the landing
            if (k != 0)
            {
                Vector3 pb = pad + q * new Vector3(0.25f, 0.1f, -0.1f);
                Vector3 pe = pb + q * new Vector3(0.35f, -0.05f, 1.6f); pe.y = PadY(r, pe) + 0.04f;
                Rod(r, pb, pe, 0.025f, silver, false);
            }
            if (k == 0) Ladder(r, top, knee, pad, strut, silver);
        }
    }

    static float PadY(Transform r, Vector3 local)
    {
        Vector3 w = r.TransformPoint(new Vector3(local.x, 0f, local.z));
        return MoonWorld.GY(w) - r.position.y;
    }

    static GameObject Rod(Transform parent, Vector3 a, Vector3 b, float thick, Material m, bool col)
    {
        Vector3 d = b - a;
        var g = Mats.Prim(PrimitiveType.Cylinder, parent, (a + b) * 0.5f, new Vector3(thick, d.magnitude * 0.5f, thick), m, col);
        g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
        return g;
    }

    // ladder on the front (+Z) leg's primary strut: two rails + 9 rungs from the porch down to ~0.8 m above the footpad, the
    // porch at the deck, and the plaque on the strut
    static void Ladder(Transform r, Vector3 top, Vector3 knee, Vector3 pad, Material strut, Material silver)
    {
        Vector3 hi = new Vector3(0f, Top - 0.05f, Body * 0.42f + 0.25f);
        Vector3 lo = Vector3.Lerp(knee, pad, 0.15f) + new Vector3(0f, 0.75f, 0.25f);
        lo.y = Mathf.Max(lo.y, pad.y + 0.85f);
        for (int s = -1; s <= 1; s += 2) Rod(r, hi + Vector3.right * s * 0.26f, lo + Vector3.right * s * 0.26f, 0.035f, silver, false);
        for (int i = 0; i < 9; i++)
        {
            Vector3 p = Vector3.Lerp(hi, lo, (i + 0.5f) / 9f);
            Mats.Prim(PrimitiveType.Cylinder, r, p, new Vector3(0.025f, 0.26f, 0.025f), new Vector3(0f, 0f, 90f), silver);
        }
        // porch (front platform) with its handrails
        Mats.Prim(PrimitiveType.Cube, r, new Vector3(0f, Top - 0.02f, Body * 0.42f + 0.38f), new Vector3(0.82f, 0.04f, 0.72f), silver, true);
        for (int s = -1; s <= 1; s += 2) Rod(r, new Vector3(s * 0.4f, Top, Body * 0.42f + 0.7f), new Vector3(s * 0.4f, Top + 0.85f, Body * 0.42f + 0.1f), 0.025f, silver, false);
        // the plaque: a 23 x 19 cm stainless-steel plate on the ladder strut, behind the rungs (two hemispheres + the words)
        Vector3 pp = Vector3.Lerp(hi, lo, 0.42f) + new Vector3(0f, 0f, -0.04f);
        Vector3 dir = (hi - lo).normalized;
        var pl = new GameObject("Plaque").transform; pl.SetParent(r, false);
        pl.localPosition = pp; pl.localRotation = Quaternion.LookRotation(Vector3.Cross(Vector3.right, dir), dir) * Quaternion.Euler(0f, 180f, 0f);
        Mats.Prim(PrimitiveType.Cube, pl, Vector3.zero, new Vector3(0.23f, 0.19f, 0.008f), Mats.Steel(new Color(0.85f, 0.85f, 0.86f)));
        for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cylinder, pl, new Vector3(s * 0.045f, 0.045f, -0.006f), new Vector3(0.08f, 0.002f, 0.08f), new Vector3(90f, 0f, 0f), Mats.Lit(new Color(0.35f, 0.36f, 0.38f)));
        var tg = new GameObject("PlaqueText"); tg.transform.SetParent(pl, false); tg.transform.localPosition = new Vector3(0f, -0.035f, -0.006f); tg.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        var tm = tg.AddComponent<TextMesh>(); UIK.WorldText(tm, 48, 0.0016f);
        tm.text = "HERE MEN FROM THE PLANET EARTH\nFIRST SET FOOT UPON THE MOON\nJULY 1969, A.D.\nWE CAME IN PEACE FOR ALL MANKIND"; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = new Color(0.12f, 0.12f, 0.13f);
        PlaqueW = pl.position;
    }

    // ---------------- the flag ----------------
    static void Flag(Transform st)
    {
        Vector3 local = new Vector3(-4.2f, 0f, PadR + 3.8f);   // out in front of the ladder, a little to the left (as in the photos)
        Vector3 p = W(local);
        FlagPos = p;
        Transform f = new GameObject("Apollo 11 flag").transform; f.SetParent(st, false);
        f.position = p; f.rotation = Quaternion.Euler(0f, Yaw + 160f, 0f);   // the stars side faces the way froggies arrive
        Material pole = Mats.Steel(new Color(0.88f, 0.88f, 0.88f));
        const float hgt = 2.4f, flyW = 1.52f, hoist = 0.91f;
        var pg = Mats.Prim(PrimitiveType.Cylinder, f, new Vector3(0f, hgt * 0.5f - 0.3f, 0f), new Vector3(0.035f, hgt * 0.5f, 0.035f), pole, true);   // 0.3 m pushed into the dust
        pg.transform.localRotation = Quaternion.Euler(2.5f, 0f, -1.5f);   // never perfectly straight
        Mats.Prim(PrimitiveType.Cylinder, f, new Vector3(flyW * 0.5f, hgt - 0.33f, 0f), new Vector3(0.02f, flyW * 0.5f + 0.03f, 0.02f), new Vector3(0f, 0f, 90f), pole);   // the crossbar
        Mats.Prim(PrimitiveType.Sphere, f, new Vector3(0f, hgt - 0.3f, 0f), Vector3.one * 0.05f, pole);
        // cloth: a rumpled grid mesh, both sides, hanging from the crossbar
        Texture2D tex = FlagTex();
        Material cm = Mats.Tex(tex, 0.12f);
        for (int side = 0; side < 2; side++)
        {
            var go = new GameObject(side == 0 ? "Flag cloth" : "Flag cloth back"); go.transform.SetParent(f, false);
            go.transform.localPosition = new Vector3(0.02f, hgt - 0.35f, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = Cloth(flyW, hoist, side == 1);
            go.AddComponent<MeshRenderer>().sharedMaterial = cm;
        }
    }

    static Mesh Cloth(float w, float h, bool back)
    {
        const int nx = 18, ny = 10;
        var v = new Vector3[(nx + 1) * (ny + 1)]; var uv = new Vector2[v.Length];
        for (int j = 0; j <= ny; j++)
            for (int i = 0; i <= nx; i++)
            {
                float u = i / (float)nx, t = j / (float)ny;
                // creases from being folded in the tube + the bottom hem sagging a little where the crossbar doesn't hold it
                float z = 0.03f * Mathf.Sin(u * 21f + 0.6f) * (0.3f + u) + 0.018f * Mathf.Sin(t * 9f + u * 4f) + 0.025f * Mathf.Sin(u * 6.3f) * t;
                float sag = t * t * 0.05f * Mathf.Sin(u * Mathf.PI) + u * t * 0.03f;
                v[j * (nx + 1) + i] = new Vector3(u * w, -t * h - sag, z + (back ? -0.004f : 0.004f));
                uv[j * (nx + 1) + i] = new Vector2(u, 1f - t);   // the back shows the mirror image, canton by the pole (as a real flag)
            }
        var tri = new int[nx * ny * 6]; int k = 0;
        for (int j = 0; j < ny; j++)
            for (int i = 0; i < nx; i++)
            {
                int a = j * (nx + 1) + i, b = a + 1, c = a + nx + 1, d = c + 1;
                if (!back) { tri[k++] = a; tri[k++] = b; tri[k++] = c; tri[k++] = b; tri[k++] = d; tri[k++] = c; }
                else { tri[k++] = a; tri[k++] = c; tri[k++] = b; tri[k++] = b; tri[k++] = c; tri[k++] = d; }
            }
        var m = new Mesh(); m.vertices = v; m.uv = uv; m.triangles = tri; m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    // the US flag drawn in code: 13 stripes (7 red, 6 white), a blue canton 7 stripes tall x 2/5 of the fly, 50 white stars
    // in 9 rows of 6 and 5
    public static Texture2D FlagTex()
    {
        const int W = 380, H = 200;
        var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
        Color red = new Color(0.70f, 0.13f, 0.20f), white = new Color(0.96f, 0.95f, 0.93f), blue = new Color(0.24f, 0.23f, 0.43f);
        var px = new Color[W * H];
        float stripe = H / 13f, cW = W * 0.4f, cH = stripe * 7f;
        for (int y = 0; y < H; y++)
        {
            int sFromTop = Mathf.FloorToInt((H - 1 - y) / stripe);
            Color row = sFromTop % 2 == 0 ? red : white;
            for (int x = 0; x < W; x++) px[y * W + x] = row;
        }
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) if (x < cW && (H - 1 - y) < cH) px[y * W + x] = blue;
        // stars: 9 rows; even rows 6 stars, odd rows 5 (offset)
        float sx = cW / 12f, sy = cH / 10f, R = Mathf.Min(sx, sy) * 0.8f;
        for (int r = 0; r < 9; r++)
        {
            int n = r % 2 == 0 ? 6 : 5;
            for (int c = 0; c < n; c++)
            {
                float cx = (r % 2 == 0 ? (2 * c + 1) : (2 * c + 2)) * sx, cy = (r + 1) * sy;
                Star(px, W, H, cx, H - 1 - cy, R, white);
            }
        }
        t.SetPixels(px); t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear; t.Apply(false, false);
        return t;
    }
    static void Star(Color[] px, int W, int H, float cx, float cy, float R, Color c)
    {
        var pts = new Vector2[10];
        for (int i = 0; i < 10; i++) { float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f; float rr = i % 2 == 0 ? R : R * 0.382f; pts[i] = new Vector2(cx + Mathf.Cos(a) * rr, cy + Mathf.Sin(a) * rr); }
        for (int y = Mathf.FloorToInt(cy - R); y <= Mathf.CeilToInt(cy + R); y++)
            for (int x = Mathf.FloorToInt(cx - R); x <= Mathf.CeilToInt(cx + R); x++)
            {
                if (x < 0 || y < 0 || x >= W || y >= H) continue;
                bool inside = false; Vector2 q = new Vector2(x + 0.5f, y + 0.5f);
                for (int i = 0, j = 9; i < 10; j = i++)
                    if ((pts[i].y > q.y) != (pts[j].y > q.y) && q.x < (pts[j].x - pts[i].x) * (q.y - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x) inside = !inside;
                if (inside) px[y * W + x] = c;
            }
    }

    // ---------------- experiments (EASEP) a little way behind the lander ----------------
    static void Experiments(Transform st)
    {
        Material gold = Mats.Shiny(new Color(0.86f, 0.66f, 0.2f)), silver = Mats.Steel(new Color(0.8f, 0.8f, 0.82f)), dark = Mats.Lit(new Color(0.08f, 0.08f, 0.1f)), cell = Mats.Paint(new Color(0.12f, 0.16f, 0.32f), 0.7f);
        // laser ranging retroreflector: a tilted grey panel of 100 glass corner cubes on a little stand
        Vector3 lp = W(new Vector3(6.5f, 0f, -12f));
        Transform lr = new GameObject("Laser retroreflector").transform; lr.SetParent(st, false); lr.position = lp; lr.rotation = Quaternion.Euler(0f, Yaw + 20f, 0f);
        Mats.Prim(PrimitiveType.Cube, lr, new Vector3(0f, 0.12f, 0f), new Vector3(0.5f, 0.24f, 0.3f), silver, true);
        var face = Mats.Node(lr, "Panel", new Vector3(0f, 0.32f, 0f)); face.localRotation = Quaternion.Euler(-30f, 0f, 0f);
        Mats.Prim(PrimitiveType.Cube, face, Vector3.zero, new Vector3(0.64f, 0.05f, 0.46f), silver);
        for (int i = 0; i < 10; i++) for (int j = 0; j < 10; j++) Mats.Prim(PrimitiveType.Cylinder, face, new Vector3(-0.27f + i * 0.06f, 0.026f, -0.19f + j * 0.042f), new Vector3(0.036f, 0.004f, 0.036f), dark);
        // passive seismometer: a gold-foil box, two solar-panel wings, a little antenna
        Vector3 sp = W(new Vector3(9.5f, 0f, -8.5f));
        Transform se = new GameObject("Passive seismometer").transform; se.SetParent(st, false); se.position = sp; se.rotation = Quaternion.Euler(0f, Yaw - 15f, 0f);
        Mats.Prim(PrimitiveType.Cube, se, new Vector3(0f, 0.28f, 0f), new Vector3(0.45f, 0.5f, 0.45f), gold, true);
        Mats.Prim(PrimitiveType.Cylinder, se, new Vector3(0f, 0.58f, 0f), new Vector3(0.3f, 0.06f, 0.3f), silver);
        for (int s = -1; s <= 1; s += 2)
        {
            var wing = Mats.Prim(PrimitiveType.Cube, se, new Vector3(s * 0.85f, 0.42f, 0f), new Vector3(1.2f, 0.03f, 0.62f), new Vector3(0f, 0f, s * -8f), cell, true);
            for (int k = 1; k < 4; k++) Mats.Prim(PrimitiveType.Cube, se, new Vector3(s * (0.25f + k * 0.3f), 0.44f - k * 0.04f * 0f, 0f), new Vector3(0.012f, 0.04f, 0.63f), new Vector3(0f, 0f, s * -8f), silver);
        }
        Mats.Prim(PrimitiveType.Cylinder, se, new Vector3(0.12f, 0.85f, 0.12f), new Vector3(0.015f, 0.28f, 0.015f), silver);
        Mats.Prim(PrimitiveType.Cylinder, se, new Vector3(0.12f, 1.14f, 0.12f), new Vector3(0.14f, 0.01f, 0.14f), new Vector3(20f, 0f, 0f), silver);
        // the TV camera's tripod a few metres off, and the Solar Wind collector pole stub (left-behind kit)
        Vector3 tp = W(new Vector3(-7.5f, 0f, -3f));
        Transform tv = new GameObject("TV camera tripod").transform; tv.SetParent(st, false); tv.position = tp;
        for (int k = 0; k < 3; k++) { Quaternion q = Quaternion.Euler(0f, k * 120f, 0f); Rod(tv, q * new Vector3(0f, 0f, 0.35f), new Vector3(0f, 1.1f, 0f), 0.025f, silver, false); }
        Mats.Prim(PrimitiveType.Cube, tv, new Vector3(0f, 1.2f, 0f), new Vector3(0.18f, 0.16f, 0.3f), Mats.Paint(new Color(0.85f, 0.85f, 0.83f), 0.4f));
        Mats.Prim(PrimitiveType.Cylinder, tv, new Vector3(0f, 1.2f, 0.17f), new Vector3(0.08f, 0.04f, 0.08f), new Vector3(90f, 0f, 0f), dark);
    }

    // ---------------- bootprints (ridged soles, ~0.75 m strides) ----------------
    static void Bootprints(Transform st)
    {
        Material pm = Mats.Lit(new Color(0.27f, 0.27f, 0.27f)), rim = Mats.Lit(new Color(0.48f, 0.48f, 0.47f));
        var rnd = new System.Random(720);
        // paths in the lander's frame: ladder foot -> flag, -> TV camera, -> experiments, a loop round the stage, wandering
        var paths = new List<Vector3[]>
        {
            new[] { new Vector3(0.3f, 0f, PadR + 0.9f), new Vector3(-2f, 0f, PadR + 2.6f), new Vector3(-4.0f, 0f, PadR + 3.4f) },
            new[] { new Vector3(-0.3f, 0f, PadR + 0.8f), new Vector3(-5f, 0f, 3f), new Vector3(-7.3f, 0f, -2.5f) },
            new[] { new Vector3(0.6f, 0f, PadR + 0.7f), new Vector3(5.6f, 0f, 3f), new Vector3(7.2f, 0f, -3f), new Vector3(9.3f, 0f, -8f), new Vector3(6.6f, 0f, -11.4f) },
            new[] { new Vector3(-4.2f, 0f, 2.5f), new Vector3(-4.6f, 0f, -3f), new Vector3(-1f, 0f, -5.5f), new Vector3(4f, 0f, -4.6f), new Vector3(5.2f, 0f, 1.5f) },
            new[] { new Vector3(-3.4f, 0f, PadR + 3.6f), new Vector3(-1f, 0f, PadR + 6f), new Vector3(3f, 0f, PadR + 5f), new Vector3(2.4f, 0f, PadR + 1.6f) },
        };
        int n = 0;
        foreach (var path in paths)
        {
            for (int s = 0; s + 1 < path.Length; s++)
            {
                Vector3 a = path[s], b = path[s + 1], d = b - a; float len = d.magnitude; Vector3 dn = d / Mathf.Max(0.01f, len), side = Vector3.Cross(Vector3.up, dn);
                for (float t = 0f; t < len; t += 0.75f)
                {
                    int foot = (Mathf.RoundToInt(t / 0.75f) % 2 == 0) ? 1 : -1;
                    Vector3 lp = a + dn * t + side * foot * 0.14f + new Vector3((float)rnd.NextDouble() - 0.5f, 0f, (float)rnd.NextDouble() - 0.5f) * 0.06f;
                    // stay off the stage's footprint / footpads
                    if (Mathf.Abs(lp.x) < Body * 0.45f && Mathf.Abs(lp.z) < Body * 0.45f) continue;
                    Print(st, lp, Mathf.Atan2(dn.x, dn.z) * Mathf.Rad2Deg + ((float)rnd.NextDouble() - 0.5f) * 12f, pm, rim);
                    n++;
                }
            }
        }
        // the famous one: right by the ladder leg's footpad
        Print(st, new Vector3(0.55f, 0f, PadR + 0.75f), 10f, pm, rim); n++;
        Debug.Log("APOLLO61: " + n + " bootprints");
    }
    static void Print(Transform st, Vector3 local, float yawLocal, Material pm, Material rim)
    {
        Vector3 w = W(local);
        Quaternion q = Quaternion.Euler(0f, Yaw + yawLocal, 0f);
        var t = new GameObject("Bootprint").transform; t.SetParent(st, false); t.position = w + Vector3.up * 0.012f; t.rotation = q;
        Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.004f, 0f), new Vector3(0.15f, 0.008f, 0.33f), rim);
        Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.006f, 0f), new Vector3(0.12f, 0.008f, 0.3f), pm);
        for (int k = 0; k < 4; k++) Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.0085f, -0.11f + k * 0.075f), new Vector3(0.12f, 0.004f, 0.02f), rim);   // sole ridges
    }

    // ---------------- reading the plaque + the greeting ----------------
    public static void ReadPlaque(Frog f)
    {
        Reads++;
        const string words = "Here men from the planet Earth first set foot upon the Moon, July 1969 A.D. We came in peace for all mankind.";
        if (f != null) f.Toast("The plaque on Eagle's ladder: \"" + words + "\"", 7f);
        reader = f; ShowPanel();
        Debug.Log("APOLLO61: " + (f != null ? f.nick : "-") + " read the plaque: \"" + words + "\"");
    }
    static void ShowPanel()
    {
        if (panel == null)
        {
            panel = new GameObject("Plaque (readable)").transform;
            Mats.Prim(PrimitiveType.Cube, panel, Vector3.zero, new Vector3(2.3f, 1.25f, 0.02f), Mats.Unlit(new Color(0.16f, 0.16f, 0.18f)));
            Mats.Prim(PrimitiveType.Cube, panel, new Vector3(0f, 0f, -0.006f), new Vector3(2.2f, 1.15f, 0.02f), Mats.Unlit(new Color(0.82f, 0.82f, 0.84f)));
            for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cylinder, panel, new Vector3(s * 0.33f, 0.3f, -0.018f), new Vector3(0.52f, 0.004f, 0.52f), new Vector3(90f, 0f, 0f), Mats.Unlit(new Color(0.55f, 0.56f, 0.6f)));
            var tg = new GameObject("Text"); tg.transform.SetParent(panel, false); tg.transform.localPosition = new Vector3(0f, -0.2f, -0.024f);
            var tm = tg.AddComponent<TextMesh>(); UIK.WorldText(tm, 48, 0.012f);
            tm.text = "HERE MEN FROM THE PLANET EARTH\nFIRST SET FOOT UPON THE MOON\nJULY 1969, A.D.\nWE CAME IN PEACE FOR ALL MANKIND"; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = new Color(0.1f, 0.1f, 0.12f);
            var cap = new GameObject("Caption"); cap.transform.SetParent(panel, false); cap.transform.localPosition = new Vector3(0f, 0.75f, -0.024f);
            var ct = cap.AddComponent<TextMesh>(); UIK.WorldText(ct, 48, 0.011f);
            ct.text = "APOLLO 11 PLAQUE  -  on Eagle's ladder leg"; ct.anchor = TextAnchor.MiddleCenter; ct.alignment = TextAlignment.Center; ct.color = new Color(1f, 0.9f, 0.55f);
            Mats.NoShadows(panel.gameObject);
        }
        panel.position = LadderFoot + Vector3.up * 2.2f + (LadderFoot - Center).normalized * 0.8f;
        panel.gameObject.SetActive(true);
        panelT = 10f;
    }

    // per frame (Ffu61): greeting toasts + the readable panel timer
    public static void Tick(float dt)
    {
        if (!Built) return;
        if (panel != null && panelT > 0f)
        {
            panelT -= dt; if (panelT <= 0f) panel.gameObject.SetActive(false);
            // face the reader's camera (the panel root has no renderer of its own, so no Billboard)
            Camera c = Game.I != null && reader != null ? Game.I.CamFor(reader) : Camera.main;
            if (c != null) { Vector3 d = panel.position - c.transform.position; d.y *= 0.3f; if (d.sqrMagnitude > 0.01f) panel.rotation = Quaternion.LookRotation(d); }
        }
        if (Game.I == null) return;
        foreach (Frog f in Game.I.frogs)
        {
            if (f == null || !f.human || f.world != WorldId.Moon || greeted.Contains(f)) continue;
            Vector3 d = f.transform.position - Center; d.y = 0f;
            if (d.magnitude < 16f)
            {
                greeted.Add(f); Greets++;
                f.Toast("Apollo 11 landing site - left here in 1969 (Tranquility Base). Walk up to the ladder leg to read the plaque", 5f);
                Debug.Log("APOLLO61: " + f.nick + " arrived at the Apollo 11 landing site (" + d.magnitude.ToString("0.0") + " m)");
            }
        }
    }
}
