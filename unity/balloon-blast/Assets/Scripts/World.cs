using System.Collections.Generic;
using UnityEngine;

// Builds the airsoft field's cover at runtime on top of the generated terrain.
public static class World
{
    public static Terrain terrain;
    public static readonly List<Vector3> cover = new List<Vector3>();
    public static readonly List<Vector3> spawns = new List<Vector3>();
    public static Vector3 center = new Vector3(125f, 0f, 125f);
    public const float Min = 12f, Max = 238f;

    static readonly List<Vector3> placed = new List<Vector3>(); // x, z, radius in y
    static System.Random rnd;
    static Transform root;

    static readonly Color Hay = new Color(0.86f, 0.74f, 0.38f);
    static readonly Color HayDark = new Color(0.74f, 0.62f, 0.30f);
    static readonly Color Wood = new Color(0.62f, 0.47f, 0.30f);
    static readonly Color WoodDark = new Color(0.45f, 0.33f, 0.21f);
    static readonly Color Concrete = new Color(0.58f, 0.60f, 0.52f);
    static readonly Color Rubber = new Color(0.12f, 0.12f, 0.13f);
    static readonly Color Bark = new Color(0.36f, 0.26f, 0.18f);

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

        spawns.Clear();
        float a0 = Rand(0f, 360f);
        for (int i = 0; i < 8; i++)
        {
            float a = (a0 + i * 45f + Rand(-8f, 8f)) * Mathf.Deg2Rad;
            float r = Rand(80f, 92f);
            spawns.Add(OnGround(center + new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r)));
        }

        Vector3 p;
        for (int i = 0; i < 30; i++) if (TryPlace(2.5f, 0f, 100f, out p)) HayGroup(p);
        for (int i = 0; i < 16; i++) if (TryPlace(2.6f, 0f, 100f, out p)) PalletWall(p);
        for (int i = 0; i < 6; i++) if (TryPlace(4.5f, 10f, 75f, out p)) Bunker(p);
        for (int i = 0; i < 16; i++) if (TryPlace(2.2f, 0f, 100f, out p)) Tyres(p);
        for (int i = 0; i < 22; i++) if (TryPlace(1.6f, 0f, 105f, out p)) Barrels(p);
        for (int i = 0; i < 14; i++) if (TryPlace(1.2f, 0f, 100f, out p)) Crate(p);
        for (int i = 0; i < 18; i++) if (TryPlace(2.5f, 15f, 95f, out p)) Tree(p);
        for (int i = 0; i < 60; i++) if (TryPlace(2.5f, 100f, 130f, out p)) Tree(p);

        StaticBatchingUtility.Combine(root.gameObject);
    }

    static bool TryPlace(float radius, float minC, float maxC, out Vector3 pos)
    {
        for (int tries = 0; tries < 30; tries++)
        {
            float x = Rand(Min + 4f, Max - 4f), z = Rand(Min + 4f, Max - 4f);
            float dc = new Vector2(x - center.x, z - center.z).magnitude;
            if (dc < minC || dc > maxC) continue;
            bool ok = true;
            foreach (var q in placed)
                if (new Vector2(q.x - x, q.z - z).magnitude < radius + q.y + 1.5f) { ok = false; break; }
            if (!ok) continue;
            foreach (var s in spawns)
                if (new Vector2(s.x - x, s.z - z).magnitude < radius + 5f) { ok = false; break; }
            if (!ok) continue;
            placed.Add(new Vector3(x, radius, z));
            pos = OnGround(new Vector3(x, 0f, z));
            return true;
        }
        pos = Vector3.zero;
        return false;
    }

    static Transform Group(string name, Vector3 p)
    {
        var g = new GameObject(name).transform;
        g.SetParent(root, false);
        g.position = p;
        g.rotation = Quaternion.Euler(0f, Rand(0f, 360f), 0f);
        return g;
    }

    static void AddCover(Transform g, float r)
    {
        for (int i = 0; i < 4; i++)
        {
            float a = i * 90f * Mathf.Deg2Rad;
            Vector3 q = g.position + g.rotation * new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * (r + 1.3f);
            if (q.x > Min && q.x < Max && q.z > Min && q.z < Max) cover.Add(OnGround(q));
        }
    }

    static GameObject P(PrimitiveType t, Transform g, Vector3 lp, Vector3 s, Color c, Vector3 euler)
    {
        GameObject o = Mats.Prim(t, g, lp, s, Mats.Lit(c));
        o.transform.localRotation = Quaternion.Euler(euler);
        o.isStatic = true;
        return o;
    }

    static void HayGroup(Vector3 p)
    {
        Transform g = Group("Hay", p + Vector3.down * 0.1f);
        if (rnd.NextDouble() < 0.5)
        {
            int n = 1 + rnd.Next(3);
            for (int i = 0; i < n; i++)
            {
                Color c = Color.Lerp(Hay, HayDark, (float)rnd.NextDouble());
                P(PrimitiveType.Cylinder, g, new Vector3((i - (n - 1) * 0.5f) * 1.55f, 0.75f, 0f), new Vector3(1.5f, 0.7f, 1.5f), c, new Vector3(0f, 0f, 90f));
            }
        }
        else
        {
            int rows = 2 + rnd.Next(2);
            for (int r = 0; r < rows; r++)
            {
                int cols = 4 - r;
                for (int i = 0; i < cols; i++)
                {
                    Color c = Color.Lerp(Hay, HayDark, (float)rnd.NextDouble());
                    P(PrimitiveType.Cube, g, new Vector3((i - (cols - 1) * 0.5f) * 1.22f, 0.3f + r * 0.56f, 0f), new Vector3(1.2f, 0.55f, 0.6f), c, Vector3.zero);
                }
            }
        }
        AddCover(g, 2f);
    }

    static void Pallet(Transform g, Vector3 lp, float yaw)
    {
        var pt = new GameObject("Pallet").transform;
        pt.SetParent(g, false);
        pt.localPosition = lp;
        pt.localRotation = Quaternion.Euler(0f, yaw, 0f);
        for (int i = 0; i < 6; i++)
            P(PrimitiveType.Cube, pt, new Vector3(0f, 0.12f + i * 0.27f, 0f), new Vector3(1.6f, 0.17f, 0.05f), (i % 2 == 0) ? Wood : Color.Lerp(Wood, WoodDark, 0.4f), Vector3.zero);
        for (int i = -1; i <= 1; i++)
            P(PrimitiveType.Cube, pt, new Vector3(i * 0.72f, 0.78f, -0.07f), new Vector3(0.1f, 1.6f, 0.1f), WoodDark, Vector3.zero);
    }

    static void PalletWall(Vector3 p)
    {
        Transform g = Group("PalletWall", p);
        int n = 2 + rnd.Next(2);
        for (int i = 0; i < n; i++) Pallet(g, new Vector3((i - (n - 1) * 0.5f) * 1.62f, 0f, 0f), 0f);
        if (rnd.NextDouble() < 0.4) Pallet(g, new Vector3(n * 0.81f + 0.05f, 0f, 0.8f), 90f);
        AddCover(g, 2.4f);
    }

    static void Bunker(Vector3 p)
    {
        Transform g = Group("Bunker", p + Vector3.down * 0.4f);
        float s = 2.6f, h = 2.4f, th = 0.4f;
        Color c = Concrete;
        // back wall with a slit window
        P(PrimitiveType.Cube, g, new Vector3(0f, 0.7f, -s), new Vector3(s * 2f + th, 1.4f, th), c, Vector3.zero);
        P(PrimitiveType.Cube, g, new Vector3(0f, h - 0.3f, -s), new Vector3(s * 2f + th, 0.6f, th), c, Vector3.zero);
        // front wall with door and slit
        P(PrimitiveType.Cube, g, new Vector3(-s * 0.6f, 0.7f, s), new Vector3(s * 0.8f + th, 1.4f, th), c, Vector3.zero);
        P(PrimitiveType.Cube, g, new Vector3(s * 0.6f, 0.7f, s), new Vector3(s * 0.8f + th, 1.4f, th), c, Vector3.zero);
        P(PrimitiveType.Cube, g, new Vector3(0f, h - 0.3f, s), new Vector3(s * 2f + th, 0.6f, th), c, Vector3.zero);
        // side walls (solid)
        P(PrimitiveType.Cube, g, new Vector3(-s, h * 0.5f, 0f), new Vector3(th, h, s * 2f), c, Vector3.zero);
        P(PrimitiveType.Cube, g, new Vector3(s, h * 0.5f, 0f), new Vector3(th, h, s * 2f), c, Vector3.zero);
        // roof + sandbag rim
        P(PrimitiveType.Cube, g, new Vector3(0f, h + 0.15f, 0f), new Vector3(s * 2f + 0.8f, 0.3f, s * 2f + 0.8f), Color.Lerp(c, Color.black, 0.15f), Vector3.zero);
        for (int i = -2; i <= 2; i++)
            P(PrimitiveType.Capsule, g, new Vector3(i * 1.1f, 0.25f, s + 1.1f), new Vector3(0.5f, 0.5f, 0.45f), new Color(0.62f, 0.58f, 0.42f), new Vector3(0f, 0f, 90f));
        AddCover(g, 3.6f);
    }

    static void Tyres(Vector3 p)
    {
        Transform g = Group("Tyres", p);
        int stacks = 1 + rnd.Next(4);
        for (int sIdx = 0; sIdx < stacks; sIdx++)
        {
            int hgt = 2 + rnd.Next(3);
            for (int k = 0; k < hgt; k++)
            {
                Color c = (k % 2 == 0) ? Rubber : (rnd.NextDouble() < 0.3 ? new Color(0.9f, 0.85f, 0.2f) : Rubber);
                P(PrimitiveType.Cylinder, g, new Vector3(sIdx * 0.95f, 0.13f + k * 0.27f, Rand(-0.05f, 0.05f)), new Vector3(0.9f, 0.13f, 0.9f), c, Vector3.zero);
            }
        }
        AddCover(g, 1.8f);
    }

    static void Barrels(Vector3 p)
    {
        Transform g = Group("Barrels", p);
        Color[] cols = { new Color(0.8f, 0.2f, 0.15f), new Color(0.15f, 0.35f, 0.75f), new Color(0.95f, 0.75f, 0.1f), new Color(0.25f, 0.55f, 0.3f) };
        int n = 1 + rnd.Next(4);
        for (int i = 0; i < n; i++)
        {
            Color c = cols[rnd.Next(cols.Length)];
            Vector3 lp = new Vector3((i % 2) * 0.7f, 0.45f, (i / 2) * 0.7f);
            P(PrimitiveType.Cylinder, g, lp, new Vector3(0.6f, 0.45f, 0.6f), c, Vector3.zero);
            P(PrimitiveType.Cylinder, g, lp + new Vector3(0f, 0.15f, 0f), new Vector3(0.62f, 0.02f, 0.62f), Color.Lerp(c, Color.black, 0.4f), Vector3.zero);
        }
        AddCover(g, 1.2f);
    }

    static void Crate(Vector3 p)
    {
        Transform g = Group("Crate", p);
        P(PrimitiveType.Cube, g, new Vector3(0f, 0.6f, 0f), Vector3.one * 1.2f, Wood, Vector3.zero);
        if (rnd.NextDouble() < 0.5) P(PrimitiveType.Cube, g, new Vector3(0.1f, 1.55f, 0.05f), Vector3.one * 0.7f, Color.Lerp(Wood, WoodDark, 0.5f), new Vector3(0f, 20f, 0f));
        AddCover(g, 0.9f);
    }

    static void Tree(Vector3 p)
    {
        Transform g = Group("Tree", p + Vector3.down * 0.2f);
        float s = Rand(0.8f, 1.35f);
        P(PrimitiveType.Cylinder, g, new Vector3(0f, 1.9f * s, 0f), new Vector3(0.38f * s, 1.9f * s, 0.38f * s), Bark, Vector3.zero);
        Color leaf = Color.Lerp(new Color(0.16f, 0.36f, 0.12f), new Color(0.32f, 0.52f, 0.18f), (float)rnd.NextDouble());
        P(PrimitiveType.Sphere, g, new Vector3(0f, 4.4f * s, 0f), Vector3.one * 3.2f * s, leaf, Vector3.zero);
        P(PrimitiveType.Sphere, g, new Vector3(0.9f * s, 3.8f * s, 0.4f * s), Vector3.one * 2.3f * s, Color.Lerp(leaf, Color.black, 0.12f), Vector3.zero);
        P(PrimitiveType.Sphere, g, new Vector3(-0.7f * s, 4.0f * s, -0.6f * s), Vector3.one * 2.4f * s, Color.Lerp(leaf, Color.white, 0.08f), Vector3.zero);
        P(PrimitiveType.Sphere, g, new Vector3(0.1f * s, 5.4f * s, -0.1f * s), Vector3.one * 2.0f * s, leaf, Vector3.zero);
        AddCover(g, 0.6f);
    }
}
