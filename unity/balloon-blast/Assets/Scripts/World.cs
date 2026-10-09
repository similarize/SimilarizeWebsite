using System.Collections.Generic;
using UnityEngine;

// Builds the airsoft field at runtime on top of the generated (Poly Haven textured) terrain.
// Graphics overhaul: a log fort in the middle, a red barn, sandbag nests, textured hay bales / crates / pallets,
// fallen logs, big Quaternius rocks, trees + pines, bushes, grass tufts, ferns and flowers. Static parts are merged
// per material and 60 m cell (MeshMerge) so split-screen stays cheap; colliders are kept (simple boxes / capsules,
// mesh colliders only on the rocks).
public static class World
{
    public static Terrain terrain;
    public static readonly List<Vector3> cover = new List<Vector3>();
    public static readonly List<Vector3> spawns = new List<Vector3>();
    public static Vector3 center = new Vector3(125f, 0f, 125f);
    public const float Min = 12f, Max = 238f;
    // landmarks (demo camera tour)
    public static Vector3 FortPos, BarnPos, NestPos;
    public static float BarnYaw;

    static readonly List<Vector3> placed = new List<Vector3>(); // x, z, radius in y
    static System.Random rnd;
    static Transform root, decor;

    static readonly Color Hay = new Color(1f, 0.93f, 0.8f);
    static readonly Color Wood = new Color(0.93f, 0.82f, 0.68f);
    static readonly Color WoodDark = new Color(0.62f, 0.5f, 0.38f);
    static readonly Color Rubber = new Color(0.12f, 0.12f, 0.13f);
    static readonly Color BarkTint = new Color(0.92f, 0.85f, 0.78f);
    static readonly Color Trim = new Color(0.92f, 0.9f, 0.85f);

    public static float Rand(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }

    public static float Ground(float x, float z)
    {
        if (terrain == null) return 0f;
        return terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
    }

    public static Vector3 OnGround(Vector3 p) { return new Vector3(p.x, Ground(p.x, p.z), p.z); }

    public static void Build(Terrain t, int seed)
    {
        terrain = t;
        rnd = new System.Random(seed);
        if (terrain != null)
        {
            Vector3 s = terrain.terrainData.size;
            center = terrain.transform.position + new Vector3(s.x * 0.5f, 0f, s.z * 0.5f);
        }
        root = new GameObject("Field Cover").transform;
        decor = new GameObject("Field Decor").transform;
        bool mob = Look.Mobile;

        spawns.Clear();
        float a0 = Rand(0f, 360f);
        for (int i = 0; i < 8; i++)
        {
            float a = (a0 + i * 45f + Rand(-8f, 8f)) * Mathf.Deg2Rad;
            float r = Rand(80f, 92f);
            spawns.Add(OnGround(center + new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r)));
        }

        // landmarks first (they claim their ground)
        FortPos = OnGround(center + Polar(Rand(0f, 360f), Rand(0f, 8f)));
        Fort(FortPos, Rand(0f, 90f));
        float ba = a0 + 22.5f + 45f * rnd.Next(8);
        BarnPos = OnGround(center + Polar(ba, Rand(42f, 52f)));
        BarnYaw = ba + 90f;
        Barn(BarnPos, BarnYaw);
        for (int k = 0; k < 2; k++)
        {
            Vector3 p;
            if (TryPlace(4f, 25f, 70f, out p)) { SandbagNest(p); if (k == 0) NestPos = p; }
        }

        Vector3 q;
        for (int i = 0; i < 24; i++) if (TryPlace(2.5f, 0f, 100f, out q)) HayGroup(q);
        for (int i = 0; i < 12; i++) if (TryPlace(2.6f, 0f, 100f, out q)) PalletWall(q);
        for (int i = 0; i < 12; i++) if (TryPlace(2.2f, 0f, 100f, out q)) Tyres(q);
        for (int i = 0; i < 18; i++) if (TryPlace(1.6f, 0f, 105f, out q)) Barrels(q);
        for (int i = 0; i < 16; i++) if (TryPlace(1.4f, 0f, 100f, out q)) Crates(q);
        for (int i = 0; i < 14; i++) if (TryPlace(2.0f, 0f, 100f, out q)) Logs(q);
        for (int i = 0; i < 14; i++) if (TryPlace(2.2f, 0f, 105f, out q)) Rock(q, Rand(1.1f, 2.2f));
        for (int i = 0; i < 16; i++) if (TryPlace(2.5f, 15f, 95f, out q)) Tree(q, false);
        for (int i = 0; i < 14; i++) if (TryPlace(1.4f, 0f, 100f, out q)) Bush(q);
        int outer = mob ? 42 : 64;
        for (int i = 0; i < outer; i++) if (TryPlace(2.5f, 100f, 132f, out q)) Tree(q, true);
        for (int i = 0; i < 10; i++) if (TryPlace(3f, 104f, 130f, out q)) Rock(q, Rand(2f, 3.4f));
        Decor(mob ? 160 : 380, mob ? 30 : 60, mob ? 40 : 90);

        MeshMerge.Merge(root, true);
        MeshMerge.Merge(decor, false);
    }

    static Vector3 Polar(float deg, float r)
    {
        float a = deg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
    }

    static bool TryPlace(float radius, float minC, float maxC, out Vector3 pos)
    {
        for (int tries = 0; tries < 30; tries++)
        {
            float x = Rand(Min + 4f, Max - 4f), z = Rand(Min + 4f, Max - 4f);
            float dc = new Vector2(x - center.x, z - center.z).magnitude;
            if (dc < minC || dc > maxC) continue;
            if (!Free(x, z, radius)) continue;
            placed.Add(new Vector3(x, radius, z));
            pos = OnGround(new Vector3(x, 0f, z));
            return true;
        }
        pos = Vector3.zero;
        return false;
    }

    static bool Free(float x, float z, float radius)
    {
        foreach (var q in placed)
            if (new Vector2(q.x - x, q.z - z).magnitude < radius + q.y + 1.5f) return false;
        foreach (var s in spawns)
            if (new Vector2(s.x - x, s.z - z).magnitude < radius + 5f) return false;
        return true;
    }

    static Transform Group(string name, Vector3 p, float yaw)
    {
        var g = new GameObject(name).transform;
        g.SetParent(root, false);
        g.position = p;
        g.rotation = Quaternion.Euler(0f, yaw, 0f);
        return g;
    }
    static Transform Group(string name, Vector3 p) { return Group(name, p, Rand(0f, 360f)); }

    static void AddCover(Transform g, float r)
    {
        for (int i = 0; i < 4; i++)
        {
            float a = i * 90f * Mathf.Deg2Rad;
            Vector3 q = g.position + g.rotation * new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * (r + 1.3f);
            if (q.x > Min && q.x < Max && q.z > Min && q.z < Max) cover.Add(OnGround(q));
        }
    }

    // primitive with a material; collider kept unless col = false
    static GameObject P(PrimitiveType t, Transform g, Vector3 lp, Vector3 s, Material m, Vector3 euler, bool col = true)
    {
        GameObject o = GameObject.CreatePrimitive(t);
        o.transform.SetParent(g, false);
        o.transform.localPosition = lp;
        o.transform.localRotation = Quaternion.Euler(euler);
        o.transform.localScale = s;
        o.GetComponent<Renderer>().sharedMaterial = m;
        if (t == PrimitiveType.Cube && m.mainTexture != null) Mats.BoxUV(o);
        if (!col) Object.DestroyImmediate(o.GetComponent<Collider>());
        o.isStatic = true;
        return o;
    }

    static void Box(Transform g, Vector3 centre, Vector3 size, float yaw = 0f)
    {
        var o = new GameObject("Col");
        o.transform.SetParent(g, false);
        o.transform.localPosition = centre;
        o.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        o.AddComponent<BoxCollider>().size = size;
    }

    static Transform Mesh(string pack, Transform g, Vector3 lp, float scale, float yaw, Transform parent = null)
    {
        LBPack p = LBPack.Get(pack);
        if (p == null) return null;
        return p.SpawnAll(parent != null ? parent : g, lp, scale, Color.white, yaw);
    }

    static Material WoodM(Color c) { return Mats.TexTint("LB/wood", c, 0.08f, 1.4f); }
    static Material BarkM(float tx, float ty) { return Mats.TexTiled("LB/q_bark", BarkTint, 0.06f, new Vector2(tx, ty)); }

    // ---------------- landmarks ----------------

    // log palisade fort: 15 x 15 m, chest-high walls (balloons peek over), gates on two sides, corner posts,
    // a flag pole and bunting; a few crates / hay inside
    static void Fort(Vector3 p, float yaw)
    {
        const float S = 7.5f;
        placed.Add(new Vector3(p.x, S + 2.5f, p.z));
        Transform g = Group("Fort", p, yaw);
        Material bark = BarkM(1f, 1.4f);
        for (int side = 0; side < 4; side++)
        {
            Quaternion r = Quaternion.Euler(0f, side * 90f, 0f);
            bool gate = side % 2 == 0;
            for (float x = -S; x <= S + 0.01f; x += 0.34f)
            {
                if (gate && Mathf.Abs(x) < 1.4f) continue;
                Vector3 lp = r * new Vector3(x, 0f, S);
                Vector3 w = g.TransformPoint(lp);
                float h = 1.65f + Mathf.PerlinNoise(x * 1.7f, side * 3.1f) * 0.35f;
                float gy = Ground(w.x, w.z) - p.y;
                P(PrimitiveType.Capsule, g, new Vector3(lp.x, gy + h * 0.5f - 0.35f, lp.z), new Vector3(0.36f, h * 0.5f + 0.35f, 0.36f), bark, new Vector3(0f, Rand(0f, 360f), 0f));
            }
            // horizontal rail (inside) + colliders per wall run
            if (gate)
            {
                for (int k = -1; k <= 1; k += 2)
                {
                    P(PrimitiveType.Cube, g, r * new Vector3(k * (S + 1.4f) * 0.5f, 1.1f, S - 0.25f), new Vector3(S - 1.4f, 0.12f, 0.1f), WoodM(WoodDark), new Vector3(0f, side * 90f, 0f), false);
                }
                // gate posts + lintel
                for (int k = -1; k <= 1; k += 2)
                    P(PrimitiveType.Capsule, g, r * new Vector3(k * 1.5f, 1.3f, S), new Vector3(0.46f, 1.6f, 0.46f), bark, Vector3.zero, false);
                P(PrimitiveType.Cylinder, g, r * new Vector3(0f, 2.75f, S), new Vector3(0.34f, 1.75f, 0.34f), bark, new Vector3(0f, side * 90f, 90f), false);
            }
            else
            {
                P(PrimitiveType.Cube, g, r * new Vector3(0f, 1.1f, S - 0.25f), new Vector3(S * 2f, 0.12f, 0.1f), WoodM(WoodDark), new Vector3(0f, side * 90f, 0f), false);
            }
            // corner post
            Vector3 cp = r * new Vector3(S, 0f, S);
            P(PrimitiveType.Capsule, g, new Vector3(cp.x, 1.5f, cp.z), new Vector3(0.62f, 2f, 0.62f), bark, Vector3.zero, false);
        }
        // flag pole + flag in the middle
        P(PrimitiveType.Cylinder, g, new Vector3(0f, 3.2f, 0f), new Vector3(0.12f, 3.2f, 0.12f), Mats.PBR(new Color(0.85f, 0.85f, 0.88f), 0.7f, 0.6f), Vector3.zero, true);
        P(PrimitiveType.Sphere, g, new Vector3(0f, 6.45f, 0f), Vector3.one * 0.22f, Mats.Paint(new Color(1f, 0.82f, 0.2f), 0.8f), Vector3.zero, false);
        P(PrimitiveType.Cube, g, new Vector3(0.75f, 5.75f, 0f), new Vector3(1.4f, 0.9f, 0.04f), Mats.Paint(new Color(1f, 0.32f, 0.3f), 0.3f), Vector3.zero, false);
        P(PrimitiveType.Cube, g, new Vector3(0.75f, 5.75f, 0.025f), new Vector3(0.5f, 0.5f, 0.02f), Mats.Paint(new Color(1f, 0.95f, 0.4f), 0.3f), new Vector3(0f, 0f, 45f), false);
        P(PrimitiveType.Cube, g, new Vector3(0.75f, 5.75f, -0.025f), new Vector3(0.5f, 0.5f, 0.02f), Mats.Paint(new Color(1f, 0.95f, 0.4f), 0.3f), new Vector3(0f, 0f, 45f), false);
        // bunting from the pole top to the four corner posts
        Color[] bc = { new Color(1f, 0.3f, 0.3f), new Color(1f, 0.85f, 0.2f), new Color(0.3f, 0.6f, 1f), new Color(0.35f, 0.85f, 0.35f), new Color(1f, 0.5f, 0.8f) };
        for (int side = 0; side < 4; side++)
        {
            Vector3 a = new Vector3(0f, 6.2f, 0f), b = Quaternion.Euler(0f, side * 90f, 0f) * new Vector3(S, 3.3f, S);
            Vector3 d = b - a;
            var line = P(PrimitiveType.Cylinder, g, (a + b) * 0.5f, new Vector3(0.02f, d.magnitude * 0.5f, 0.02f), Mats.Lit(new Color(0.9f, 0.9f, 0.9f)), Vector3.zero, false);
            line.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            int n = 9;
            for (int k = 1; k < n; k++)
            {
                Vector3 q = Vector3.Lerp(a, b, k / (float)n) + Vector3.down * (Mathf.Sin(k / (float)n * Mathf.PI) * 0.6f + 0.18f);
                var f = P(PrimitiveType.Cube, g, q, new Vector3(0.3f, 0.3f, 0.02f), Mats.Paint(bc[(k + side) % bc.Length], 0.35f), Vector3.zero, false);
                f.transform.localRotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, d.normalized)) * Quaternion.Euler(0f, 0f, 45f);
            }
        }
        // a little cover inside
        Transform fh = Group("FortHay", g.TransformPoint(new Vector3(-3.5f, 0f, 2.5f)), yaw + 90f);
        SquareBales(fh, 2, 3);
        Transform c = Group("FortCrates", g.TransformPoint(new Vector3(3.2f, 0f, -3f)), yaw + 20f);
        Crate(c, Vector3.zero, 1.2f, 0f);
        Crate(c, new Vector3(1.3f, 0f, 0.2f), 1.0f, 12f);
        Crate(c, new Vector3(0.6f, 1.2f, 0.1f), 0.9f, -8f);
        Box(c, new Vector3(0.65f, 0.9f, 0.1f), new Vector3(2.6f, 1.8f, 1.3f));
        cover.Add(OnGround(g.TransformPoint(new Vector3(-1.5f, 0f, 2.5f))));
        cover.Add(OnGround(g.TransformPoint(new Vector3(1.5f, 0f, -3f))));
        foreach (var v in new[] { new Vector3(0f, 0f, S + 1.6f), new Vector3(0f, 0f, -S - 1.6f), new Vector3(S + 1.6f, 0f, 3f), new Vector3(-S - 1.6f, 0f, -3f) })
            cover.Add(OnGround(g.TransformPoint(v)));
    }

    // gable-roofed red barn, open at both ends, side doors; hay inside
    static void Barn(Vector3 p, float yaw)
    {
        const float W = 9f, D = 13f, H = 3.8f, Peak = 6.4f, T = 0.25f;
        placed.Add(new Vector3(p.x, 9.5f, p.z));
        float gy = p.y;
        for (int i = 0; i < 5; i++) { float a = i * 72f * Mathf.Deg2Rad; gy = Mathf.Min(gy, Ground(p.x + Mathf.Sin(a) * 7f, p.z + Mathf.Cos(a) * 7f)); }
        Vector3 basePos = new Vector3(p.x, gy, p.z);
        Transform g = Group("Barn", basePos, yaw);
        float skirt = 1.2f + (p.y - gy);
        Material red = Mats.TexTint("LB/barnred", Color.white, 0.12f, 2.4f);
        Material roof = Mats.TexTint("LB/roofmetal", new Color(0.78f, 0.8f, 0.84f), 0.55f, 1.6f);
        Material trim = Mats.Paint(Trim, 0.4f);
        float y0 = -skirt, wallH = H + skirt;
        // long side walls (x = +-W/2) with a 2.4 m door gap in the middle
        for (int sg = -1; sg <= 1; sg += 2)
        {
            float x = sg * W * 0.5f;
            float seg = (D - 2.4f) * 0.5f;
            for (int k = -1; k <= 1; k += 2)
                P(PrimitiveType.Cube, g, new Vector3(x, y0 + wallH * 0.5f, k * (1.2f + seg * 0.5f)), new Vector3(T, wallH, seg), red, Vector3.zero);
            P(PrimitiveType.Cube, g, new Vector3(x, H - 0.5f, 0f), new Vector3(T, 1f, 2.4f), red, Vector3.zero);
            // trim: corners, door frame, eave
            foreach (float z in new[] { -D * 0.5f, D * 0.5f, -1.2f, 1.2f })
                P(PrimitiveType.Cube, g, new Vector3(x + sg * 0.02f, y0 + wallH * 0.5f, z), new Vector3(T + 0.06f, wallH, 0.2f), trim, Vector3.zero, false);
            P(PrimitiveType.Cube, g, new Vector3(x + sg * 0.02f, H - 1f, 0f), new Vector3(T + 0.06f, 0.18f, 2.4f), trim, Vector3.zero, false);
            // window with white cross
            for (int k = -1; k <= 1; k += 2)
            {
                Vector3 wc = new Vector3(x + sg * 0.03f, 2.3f, k * 4.2f);
                P(PrimitiveType.Cube, g, wc, new Vector3(T + 0.04f, 1.0f, 1.0f), Mats.Paint(new Color(0.14f, 0.12f, 0.1f), 0.6f), Vector3.zero, false);
                P(PrimitiveType.Cube, g, wc + new Vector3(sg * 0.02f, 0f, 0f), new Vector3(T + 0.06f, 1.0f, 0.1f), trim, Vector3.zero, false);
                P(PrimitiveType.Cube, g, wc + new Vector3(sg * 0.02f, 0f, 0f), new Vector3(T + 0.06f, 0.1f, 1.0f), trim, Vector3.zero, false);
            }
        }
        // gable ends (z = +-D/2): 4 m opening, wall either side, header + triangle above
        for (int sg = -1; sg <= 1; sg += 2)
        {
            float z = sg * D * 0.5f;
            float side = (W - 4f) * 0.5f;
            for (int k = -1; k <= 1; k += 2)
                P(PrimitiveType.Cube, g, new Vector3(k * (2f + side * 0.5f), y0 + wallH * 0.5f, z), new Vector3(side, wallH, T), red, Vector3.zero);
            P(PrimitiveType.Cube, g, new Vector3(0f, H - 0.35f, z), new Vector3(4f, 0.7f, T), red, Vector3.zero);
            var tri = new GameObject("Gable");
            tri.transform.SetParent(g, false);
            tri.transform.localPosition = new Vector3(0f, H, z);
            tri.AddComponent<MeshFilter>().sharedMesh = Prism(W, Peak - H, T, 2.4f);
            tri.AddComponent<MeshRenderer>().sharedMaterial = red;
            tri.isStatic = true;
            // open doors (X-braced), swung out against the wall
            for (int k = -1; k <= 1; k += 2)
            {
                Transform dpiv = new GameObject("Door").transform;
                dpiv.SetParent(g, false);
                dpiv.localPosition = new Vector3(k * 2f, 0f, z + sg * 0.15f);
                dpiv.localRotation = Quaternion.Euler(0f, k * sg * -100f, 0f);
                P(PrimitiveType.Cube, dpiv, new Vector3(k * 1f, 1.55f, 0f), new Vector3(2f, 3.1f, 0.12f), red, Vector3.zero);
                P(PrimitiveType.Cube, dpiv, new Vector3(k * 1f, 1.55f, sg * 0.07f), new Vector3(2.0f, 0.16f, 0.04f), trim, new Vector3(0f, 0f, 57f), false);
                P(PrimitiveType.Cube, dpiv, new Vector3(k * 1f, 1.55f, sg * 0.07f), new Vector3(2.0f, 0.16f, 0.04f), trim, new Vector3(0f, 0f, -57f), false);
            }
            P(PrimitiveType.Cube, g, new Vector3(0f, H - 0.72f, z + sg * 0.02f), new Vector3(4.2f, 0.16f, T + 0.06f), trim, Vector3.zero, false);
            P(PrimitiveType.Cube, g, new Vector3(0f, Peak - 0.9f, z + sg * 0.03f), new Vector3(0.9f, 0.9f, T + 0.04f), Mats.Paint(new Color(0.12f, 0.1f, 0.09f), 0.6f), Vector3.zero, false);
        }
        // roof: two metal slabs + ridge cap
        float run = W * 0.5f + 0.5f, rise = Peak - H + 0.3f;
        float ang = Mathf.Atan2(rise, run) * Mathf.Rad2Deg, len = Mathf.Sqrt(run * run + rise * rise);
        for (int sg = -1; sg <= 1; sg += 2)
            P(PrimitiveType.Cube, g, new Vector3(sg * run * 0.5f, H + rise * 0.5f - 0.15f, 0f), new Vector3(len + 0.1f, 0.14f, D + 1f), roof, new Vector3(0f, 0f, -sg * ang));
        P(PrimitiveType.Cube, g, new Vector3(0f, Peak + 0.12f, 0f), new Vector3(0.5f, 0.12f, D + 1.04f), Mats.PBR(new Color(0.6f, 0.62f, 0.66f), 0.6f, 0.5f), Vector3.zero, false);
        // hay inside + a cart of bales by the door
        Transform h1 = Group("BarnHay", g.TransformPoint(new Vector3(-2.6f, 0f, -3.5f)), yaw + 90f);
        SquareBales(h1, 3, 3);
        Transform h2 = Group("BarnHay2", g.TransformPoint(new Vector3(2.8f, 0f, 3.2f)), yaw);
        RoundBale(h2, Vector3.zero, 0f);
        Transform h3 = Group("BarnOut", g.TransformPoint(new Vector3(3f, 0f, D * 0.5f + 3.5f)), yaw);
        RoundBale(h3, Vector3.zero, 90f);
        RoundBale(h3, new Vector3(1.9f, 0f, 0.3f), 80f);
        foreach (var v in new[] { new Vector3(0f, 0f, D * 0.5f + 2f), new Vector3(0f, 0f, -D * 0.5f - 2f), new Vector3(W * 0.5f + 1.6f, 0f, 3.5f), new Vector3(-W * 0.5f - 1.6f, 0f, -3.5f), new Vector3(0.5f, 0f, -0.5f) })
            cover.Add(OnGround(g.TransformPoint(v)));
    }

    // triangular prism (gable): base width w at y = 0, apex height h, thickness t; UVs box-projected in metres / m
    static Mesh Prism(float w, float h, float t, float m)
    {
        float x = w * 0.5f, z = t * 0.5f;
        Vector3 a = new Vector3(-x, 0, 0), b = new Vector3(x, 0, 0), c = new Vector3(0, h, 0);
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
        System.Action<Vector3[], Vector3> face = (pts, nn) =>
        {
            int o = v.Count;
            foreach (var pt in pts)
            {
                v.Add(pt); n.Add(nn);
                uv.Add(Mathf.Abs(nn.z) > 0.5f ? new Vector2(pt.x / m, pt.y / m) : new Vector2((pt.z + pt.x) / m, pt.y / m));
            }
            for (int i = 1; i + 1 < pts.Length; i++) { tr.Add(o); tr.Add(o + i); tr.Add(o + i + 1); }
        };
        Vector3 f = new Vector3(0, 0, z), k = new Vector3(0, 0, -z);
        face(new[] { a + k, c + k, b + k }, Vector3.back);
        face(new[] { b + f, c + f, a + f }, Vector3.forward);
        Vector3 nl = new Vector3(-h, x, 0).normalized, nr = new Vector3(h, x, 0).normalized;
        face(new[] { a + f, c + f, c + k, a + k }, nl);
        face(new[] { c + f, b + f, b + k, c + k }, nr);
        var mesh = new Mesh { name = "Prism" };
        mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(tr, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // horseshoe of sandbags (two rows), opening away from the middle
    static void SandbagNest(Vector3 p)
    {
        Vector3 to = center - p;
        Transform g = Group("Sandbags", p, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
        Material bag = Mats.TexTint("LB/burlap", new Color(1f, 0.97f, 0.9f), 0.05f, 0.6f);
        float R = 2.6f;
        for (int row = 0; row < 3; row++)
        {
            int n = 11 - row;
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.Lerp(-110f, 110f, (i + (row % 2) * 0.5f) / (n - 0.5f)) * Mathf.Deg2Rad;
                Vector3 lp = new Vector3(Mathf.Sin(a) * R, 0f, Mathf.Cos(a) * R);
                Vector3 w = g.TransformPoint(lp);
                float gy = Ground(w.x, w.z) - p.y;
                P(PrimitiveType.Capsule, g, new Vector3(lp.x, gy + 0.18f + row * 0.3f, lp.z), new Vector3(0.38f, 0.42f, 0.55f), bag, new Vector3(0f, a * Mathf.Rad2Deg + Rand(-6f, 6f), 90f), false);
            }
        }
        for (int i = 0; i < 6; i++)
        {
            float a = Mathf.Lerp(-110f, 110f, (i + 0.5f) / 6f);
            float ar = a * Mathf.Deg2Rad;
            Box(g, new Vector3(Mathf.Sin(ar) * R, 0.55f, Mathf.Cos(ar) * R), new Vector3(1.6f, 1.3f, 0.6f), a);
        }
        cover.Add(OnGround(g.TransformPoint(new Vector3(0f, 0f, 1.2f))));
        cover.Add(OnGround(g.TransformPoint(new Vector3(0f, 0f, -0.5f))));
        AddCover(g, R);
    }

    // ---------------- scattered cover ----------------

    static void SquareBale(Transform g, Vector3 lp, float yaw)
    {
        Material hay = Mats.TexTint("LB/hay", Color.Lerp(Hay, new Color(0.9f, 0.8f, 0.62f), (float)rnd.NextDouble()), 0.05f, 1.1f);
        Material twine = Mats.Lit(new Color(0.45f, 0.32f, 0.2f));
        Transform b = new GameObject("Bale").transform;
        b.SetParent(g, false); b.localPosition = lp; b.localRotation = Quaternion.Euler(0f, yaw, 0f);
        P(PrimitiveType.Cube, b, new Vector3(0f, 0.3f, 0f), new Vector3(1.2f, 0.56f, 0.6f), hay, Vector3.zero);
        for (int k = -1; k <= 1; k += 2)
            P(PrimitiveType.Cube, b, new Vector3(k * 0.3f, 0.3f, 0f), new Vector3(0.03f, 0.58f, 0.62f), twine, Vector3.zero, false);
    }

    static void SquareBales(Transform g, int cols, int rows)
    {
        for (int r = 0; r < rows; r++)
        {
            int c = Mathf.Max(1, cols - r);
            for (int i = 0; i < c; i++)
                SquareBale(g, new Vector3((i - (c - 1) * 0.5f) * 1.24f + Rand(-0.04f, 0.04f), r * 0.57f, Rand(-0.04f, 0.04f)), Rand(-3f, 3f));
        }
    }

    static void RoundBale(Transform g, Vector3 lp, float yaw)
    {
        Material side = Mats.TexTiled("LB/hay", Color.Lerp(Hay, new Color(0.92f, 0.82f, 0.62f), (float)rnd.NextDouble()), 0.05f, new Vector2(4f, 1.2f));
        Material end = Mats.TexTiled("LB/hayend", Hay, 0.05f, Vector2.one);
        Transform b = new GameObject("RoundBale").transform;
        b.SetParent(g, false); b.localPosition = lp; b.localRotation = Quaternion.Euler(0f, yaw, 0f);
        P(PrimitiveType.Cylinder, b, new Vector3(0f, 0.78f, 0f), new Vector3(1.55f, 0.72f, 1.55f), side, new Vector3(0f, 0f, 90f));
        for (int k = -1; k <= 1; k += 2)
            P(PrimitiveType.Cylinder, b, new Vector3(k * 0.725f, 0.78f, 0f), new Vector3(1.45f, 0.01f, 1.45f), end, new Vector3(0f, 0f, 90f), false);
    }

    static void HayGroup(Vector3 p)
    {
        Transform g = Group("Hay", p + Vector3.down * 0.06f);
        if (rnd.NextDouble() < 0.45)
        {
            int n = 1 + rnd.Next(3);
            for (int i = 0; i < n; i++) RoundBale(g, new Vector3((i - (n - 1) * 0.5f) * 1.6f, 0f, Rand(-0.2f, 0.2f)), Rand(-4f, 4f));
        }
        else SquareBales(g, 4, 2 + rnd.Next(2));
        AddCover(g, 2f);
    }

    static void Pallet(Transform g, Vector3 lp, float yaw)
    {
        var pt = new GameObject("Pallet").transform;
        pt.SetParent(g, false);
        pt.localPosition = lp;
        pt.localRotation = Quaternion.Euler(0f, yaw, 0f);
        Material a = WoodM(Wood), b = WoodM(Color.Lerp(Wood, WoodDark, 0.35f)), d = WoodM(WoodDark);
        for (int i = 0; i < 6; i++)
            P(PrimitiveType.Cube, pt, new Vector3(0f, 0.12f + i * 0.27f, 0f), new Vector3(1.6f, 0.17f, 0.05f), (i % 2 == 0) ? a : b, Vector3.zero);
        for (int i = -1; i <= 1; i++)
            P(PrimitiveType.Cube, pt, new Vector3(i * 0.72f, 0.78f, -0.07f), new Vector3(0.1f, 1.6f, 0.1f), d, Vector3.zero);
    }

    static void PalletWall(Vector3 p)
    {
        Transform g = Group("PalletWall", p);
        int n = 2 + rnd.Next(2);
        for (int i = 0; i < n; i++) Pallet(g, new Vector3((i - (n - 1) * 0.5f) * 1.62f, 0f, 0f), Rand(-2f, 2f));
        if (rnd.NextDouble() < 0.4) Pallet(g, new Vector3(n * 0.81f + 0.05f, 0f, 0.8f), 90f);
        AddCover(g, 2.4f);
    }

    static void Tyres(Vector3 p)
    {
        Transform g = Group("Tyres", p);
        int stacks = 1 + rnd.Next(4);
        Material rub = Mats.Paint(Rubber, 0.25f);
        Material yel = Mats.Paint(new Color(1f, 0.85f, 0.2f), 0.5f);
        Material hub = Mats.Lit(new Color(0.05f, 0.05f, 0.05f));
        for (int sIdx = 0; sIdx < stacks; sIdx++)
        {
            int hgt = 2 + rnd.Next(3);
            for (int k = 0; k < hgt; k++)
            {
                Material m = (k % 2 == 1 && rnd.NextDouble() < 0.35) ? yel : rub;
                Vector3 lp = new Vector3(sIdx * 0.95f, 0.13f + k * 0.27f, Rand(-0.05f, 0.05f));
                P(PrimitiveType.Cylinder, g, lp, new Vector3(0.9f, 0.13f, 0.9f), m, Vector3.zero);
                P(PrimitiveType.Cylinder, g, lp + new Vector3(0f, 0.005f, 0f), new Vector3(0.5f, 0.132f, 0.5f), hub, Vector3.zero, false);
            }
        }
        AddCover(g, 1.8f);
    }

    static void Barrels(Vector3 p)
    {
        Transform g = Group("Barrels", p);
        Color[] cols = { new Color(0.85f, 0.22f, 0.16f), new Color(0.16f, 0.4f, 0.8f), new Color(0.98f, 0.76f, 0.12f), new Color(0.22f, 0.6f, 0.32f) };
        int n = 1 + rnd.Next(4);
        for (int i = 0; i < n; i++)
        {
            Color c = cols[rnd.Next(cols.Length)];
            Material m = Mats.PBR(c, 0.62f, 0.25f), rim = Mats.PBR(Color.Lerp(c, Color.black, 0.35f), 0.55f, 0.4f);
            Vector3 lp = new Vector3((i % 2) * 0.7f, 0.45f, (i / 2) * 0.7f);
            P(PrimitiveType.Cylinder, g, lp, new Vector3(0.6f, 0.45f, 0.6f), m, Vector3.zero);
            foreach (float y in new[] { -0.42f, -0.15f, 0.15f, 0.42f })
                P(PrimitiveType.Cylinder, g, lp + new Vector3(0f, y, 0f), new Vector3(0.625f, 0.018f, 0.625f), rim, Vector3.zero, false);
            P(PrimitiveType.Cylinder, g, lp + new Vector3(0.14f, 0.455f, 0.1f), new Vector3(0.08f, 0.01f, 0.08f), rim, Vector3.zero, false);
        }
        AddCover(g, 1.2f);
    }

    // wooden crate with darker edge frame + cross braces
    static void Crate(Transform g, Vector3 lp, float s, float yaw)
    {
        Transform c = new GameObject("Crate").transform;
        c.SetParent(g, false); c.localPosition = lp; c.localRotation = Quaternion.Euler(0f, yaw, 0f);
        Material body = WoodM(Color.Lerp(Wood, Color.white, 0.1f)), frame = WoodM(WoodDark);
        float h = s * 0.5f, e = s * 0.11f;
        P(PrimitiveType.Cube, c, new Vector3(0f, h, 0f), Vector3.one * s, body, Vector3.zero);
        for (int a = -1; a <= 1; a += 2)
            for (int b = -1; b <= 1; b += 2)
            {
                P(PrimitiveType.Cube, c, new Vector3(a * (h - e * 0.4f), h, b * (h - e * 0.4f)), new Vector3(e, s + 0.01f, e), frame, Vector3.zero, false);
                P(PrimitiveType.Cube, c, new Vector3(0f, h + a * (h - e * 0.4f), b * (h - e * 0.4f)), new Vector3(s + 0.01f, e, e), frame, Vector3.zero, false);
                P(PrimitiveType.Cube, c, new Vector3(b * (h - e * 0.4f), h + a * (h - e * 0.4f), 0f), new Vector3(e, e, s + 0.01f), frame, Vector3.zero, false);
            }
        for (int a = -1; a <= 1; a += 2)
        {
            P(PrimitiveType.Cube, c, new Vector3(0f, h, a * (h + 0.005f)), new Vector3(s * 1.25f, e * 0.8f, 0.02f), frame, new Vector3(0f, 0f, 45f * a), false);
            P(PrimitiveType.Cube, c, new Vector3(a * (h + 0.005f), h, 0f), new Vector3(0.02f, e * 0.8f, s * 1.25f), frame, new Vector3(45f * a, 0f, 0f), false);
        }
    }

    static void Crates(Vector3 p)
    {
        Transform g = Group("Crates", p);
        Crate(g, Vector3.zero, 1.2f, 0f);
        float r = (float)rnd.NextDouble();
        if (r < 0.5f) Crate(g, new Vector3(0.1f, 1.2f, 0.05f), 0.8f, Rand(-25f, 25f));
        if (r > 0.3f) Crate(g, new Vector3(1.25f, 0f, Rand(-0.3f, 0.3f)), Rand(0.8f, 1.1f), Rand(-20f, 20f));
        AddCover(g, 1.2f);
    }

    static void Logs(Vector3 p)
    {
        Transform g = Group("Logs", p);
        if (LBPack.Get("log0") == null) return;
        if (rnd.NextDouble() < 0.55)
        {
            float s = Rand(0.9f, 1.2f);
            Mesh("log0", g, new Vector3(0f, -0.08f, 0f), s, 0f);
            var cap = g.gameObject.AddComponent<CapsuleCollider>();
            cap.direction = 0; cap.radius = 0.3f * s; cap.height = 3.4f * s; cap.center = new Vector3(0f, 0.22f * s, 0f);
        }
        else
        {
            // pyramid pile of three
            Mesh("log0", g, new Vector3(0f, -0.05f, -0.31f), 1f, Rand(-3f, 3f));
            Mesh("log0", g, new Vector3(0.1f, -0.05f, 0.31f), 1f, 180f + Rand(-3f, 3f));
            Mesh("log0", g, new Vector3(0.05f, 0.47f, 0f), 0.95f, Rand(-4f, 4f));
            Box(g, new Vector3(0f, 0.55f, 0f), new Vector3(3.3f, 1.1f, 1.2f));
        }
        AddCover(g, 1.6f);
    }

    static void Rock(Vector3 p, float s)
    {
        string[] packs = { "qrock0", "qrock1", "qrock2" };
        string pk = packs[rnd.Next(packs.Length)];
        float ks = pk == "qrock2" ? s * 0.55f : s;
        Transform g = Group("Rock", p + Vector3.down * 0.25f * ks);
        Transform m = Mesh(pk, g, Vector3.zero, ks, 0f);
        if (m == null) return;
        foreach (var mf in m.GetComponentsInChildren<MeshFilter>())
            mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        AddCover(g, 1.2f * s);
    }

    static void Tree(Vector3 p, bool outer)
    {
        string[] packs = outer ? new[] { "tree0", "tree1", "tree2", "pine0", "pine1", "pine0" } : new[] { "tree0", "tree1", "tree2" };
        string pk = packs[rnd.Next(packs.Length)];
        float s = Rand(0.85f, 1.3f) * (pk.StartsWith("pine") ? 1.15f : 1f);
        Transform g = Group("Tree", p + Vector3.down * 0.15f);
        if (Mesh(pk, g, Vector3.zero, s, 0f) == null)
        {
            P(PrimitiveType.Cylinder, g, new Vector3(0f, 1.9f * s, 0f), new Vector3(0.38f * s, 1.9f * s, 0.38f * s), Mats.Lit(new Color(0.36f, 0.26f, 0.18f)), Vector3.zero);
            P(PrimitiveType.Sphere, g, new Vector3(0f, 4.4f * s, 0f), Vector3.one * 3.2f * s, Mats.Lit(new Color(0.24f, 0.45f, 0.16f)), Vector3.zero);
        }
        var cap = g.gameObject.AddComponent<CapsuleCollider>();
        cap.radius = 0.32f * s; cap.height = 5f * s; cap.center = new Vector3(0f, 2.5f * s, 0f);
        if (!outer) AddCover(g, 0.6f);
        // a bush or ferns at the foot of some trees
        if (rnd.NextDouble() < 0.5)
        {
            Vector3 w = OnGround(p + new Vector3(Rand(-1.6f, 1.6f), 0f, Rand(-1.6f, 1.6f)));
            Mesh(rnd.NextDouble() < 0.5 ? "fern0" : "bush0", g, w, Rand(0.8f, 1.3f), Rand(0f, 360f), decor);
        }
    }

    static void Bush(Vector3 p)
    {
        Transform g = Group("Bush", p);
        Mesh(rnd.NextDouble() < 0.5 ? "fbush0" : "bush0", g, p, Rand(1.1f, 1.6f), Rand(0f, 360f), decor);
        if (rnd.NextDouble() < 0.6) Mesh("bush0", g, OnGround(p + g.rotation * new Vector3(1.2f, 0f, 0.4f)), Rand(0.8f, 1.1f), Rand(0f, 360f), decor);
    }

    // grass tufts, flowers and ferns (no colliders, no shadows)
    static void Decor(int grass, int flowers, int ferns)
    {
        if (LBPack.Get("grass0") == null) return;
        for (int i = 0; i < grass; i++)
        {
            float x = Rand(Min, Max), z = Rand(Min, Max);
            if (!FreeDecor(x, z)) continue;
            Mesh(rnd.NextDouble() < 0.6 ? "grass1" : "grass0", null, OnGround(new Vector3(x, 0f, z)) + Vector3.down * 0.05f, Rand(0.7f, 1.2f), Rand(0f, 360f), decor);
        }
        for (int i = 0; i < flowers; i++)
        {
            float x = Rand(Min, Max), z = Rand(Min, Max);
            if (!FreeDecor(x, z)) continue;
            Mesh("flowers0", null, OnGround(new Vector3(x, 0f, z)), Rand(0.7f, 1.1f), Rand(0f, 360f), decor);
        }
        for (int i = 0; i < ferns; i++)
        {
            float x = Rand(Min, Max), z = Rand(Min, Max);
            if (!FreeDecor(x, z)) continue;
            Mesh("fern0", null, OnGround(new Vector3(x, 0f, z)), Rand(0.8f, 1.4f), Rand(0f, 360f), decor);
        }
    }

    static bool FreeDecor(float x, float z)
    {
        foreach (var q in placed)
            if (new Vector2(q.x - x, q.z - z).magnitude < q.y + 0.4f) return false;
        return true;
    }
}
