using UnityEngine;

// Beach dressing around the circuit: palms, umbrellas + towels, tiki huts, rocks, tiki torches on the
// shortcut, buoys and boats offshore, the start arch. Everything is primitives merged per material
// (MeshMerge) so the whole island is a few dozen draw calls (Cybertruck browser friendly).
public static class Scenery
{
    static System.Random rnd;
    static float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }

    static Material trunkA, trunkB, leafA, leafB, coco, rock, wood, thatch, white, flame, dark;

    public static void Build()
    {
        rnd = new System.Random(42);
        trunkA = Mats.Lit(new Color(0.55f, 0.42f, 0.28f)); trunkB = Mats.Lit(new Color(0.47f, 0.35f, 0.23f));
        leafA = Mats.Lit(new Color(0.22f, 0.6f, 0.2f)); leafB = Mats.Lit(new Color(0.32f, 0.7f, 0.25f));
        coco = Mats.Lit(new Color(0.35f, 0.25f, 0.12f)); rock = Mats.Lit(new Color(0.55f, 0.53f, 0.5f));
        wood = Mats.Lit(new Color(0.58f, 0.4f, 0.22f)); thatch = Mats.Lit(new Color(0.82f, 0.68f, 0.4f));
        white = Mats.Lit(new Color(0.96f, 0.96f, 0.94f)); flame = Mats.Unlit(new Color(1f, 0.6f, 0.15f));
        dark = Mats.Lit(new Color(0.15f, 0.15f, 0.17f));

        var root = new GameObject("Scenery").transform;
        root.SetParent(Track.Root, false);

        // palms + rocks along both sides of the main road
        for (float s = 0f; s < Track.Length; s += 19f)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                if (rnd.NextDouble() < 0.25) continue;
                float lat = side * R(Track.HalfW + Track.Wall + 3f, Track.HalfW + Track.Wall + 20f);
                Vector3 p = Track.PointAt(s + R(-6f, 6f), lat);
                if (!Clear(p, 4f)) continue;
                float h = Track.HeightAt(p.x, p.z);
                if (h < 0.35f) { if (h > -1.2f && rnd.NextDouble() < 0.3) Rock(root, new Vector3(p.x, h, p.z), R(0.8f, 2f)); continue; }
                if (rnd.NextDouble() < 0.82) Palm(root, new Vector3(p.x, h - 0.1f, p.z), R(6f, 10f));
                else Rock(root, new Vector3(p.x, h, p.z), R(0.8f, 1.8f));
            }
        }
        // inland grove
        int grove = Look.Mobile ? 30 : 70;     // phones: fewer inland palms (each palm ~2.4k tris)
        for (int i = 0; i < grove; i++)
        {
            Vector3 p = new Vector3(R(Track.Min.x, Track.Max.x), 0f, R(Track.Min.y, Track.Max.y));
            float h = Track.HeightAt(p.x, p.z);
            if (h < 0.6f || !Clear(p, 10f)) continue;
            Palm(root, new Vector3(p.x, h - 0.1f, p.z), R(6f, 11f));
        }
        // beach umbrellas + towels on the outer beach
        int umbrellas = 0;
        for (float s = 10f; s < Track.Length && umbrellas < 22; s += 37f)
        {
            Vector3 p = Track.PointAt(s, 0f);
            Vector3 rt = Track.PointAt(s, 1f) - p;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 q = p + rt * side * R(Track.HalfW + Track.Wall + 2.5f, Track.HalfW + Track.Wall + 8f);
                float h = Track.HeightAt(q.x, q.z);
                if (h < 0.2f || h > 1.3f || !Clear(q, 1.5f)) continue;
                Umbrella(root, new Vector3(q.x, h, q.z));
                umbrellas++;
                break;
            }
        }
        // tiki huts inland
        int huts = 0;
        for (int i = 0; i < 60 && huts < 6; i++)
        {
            Vector3 p = new Vector3(R(Track.Min.x + 40f, Track.Max.x - 40f), 0f, R(Track.Min.y + 40f, Track.Max.y - 40f));
            float h = Track.HeightAt(p.x, p.z);
            if (h < 0.5f || h > 3f || !Clear(p, 12f)) continue;
            Hut(root, new Vector3(p.x, h, p.z), R(0f, 360f));
            huts++;
        }
        // tiki torches lining the shortcut
        for (float s = 6f; s < Track.ScLength - 4f; s += 14f)
        {
            if (s > Track.ScGapA - 12f && s < Track.ScGapB + 2f) continue;
            Vector3 p = Track.ShortcutPoint(s), p2 = Track.ShortcutPoint(s + 1f);
            Vector3 d = Track.Flat(p2 - p).normalized, rt = new Vector3(d.z, 0f, -d.x);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 q = p + rt * side * (Track.ScHalf + 1.6f);
                Torch(root, new Vector3(q.x, Track.HeightAt(q.x, q.z), q.z));
            }
        }
        // buoys + boats offshore
        for (int i = 0; i < 26; i++)
        {
            float s = Track.Length * i / 26f;
            Vector3 p = Track.PointAt(s, 0f);
            Vector3 c = new Vector3((Track.Min.x + Track.Max.x) * 0.5f, 0f, (Track.Min.y + Track.Max.y) * 0.5f);
            Vector3 out1 = Track.Flat(p - c).normalized;
            Vector3 q = p + out1 * R(55f, 75f);
            if (Track.HeightAt(q.x, q.z) > -1.5f) continue;
            if (i % 5 == 0) Boat(root, q, R(0f, 360f));
            else Buoy(root, q, i % 2 == 0 ? new Color(1f, 0.35f, 0.15f) : new Color(1f, 0.9f, 0.2f));
        }
        StartArch(root);
        MeshMerge.Merge(root, true);
    }

    // keep props off the drivable band (road + soft wall) and off the shortcut
    static bool Clear(Vector3 p, float extra)
    {
        Track.Proj m = Track.Nearest(p, -1, 0);
        if (m.dist < Track.HalfW + Track.Wall + extra) return false;
        Track.Proj s = Track.NearestShortcut(p);
        if (s.dist < Track.ScHalf + Track.Wall + extra) return false;
        return true;
    }

    // mesh packs from work/lb-gfx/build_props.py (null -> the primitive versions below)
    static LBPack PackOr(string n) { return LBPack.Get(n); }

    static void Palm(Transform root, Vector3 p, float height)
    {
        LBPack pk = PackOr(rnd.NextDouble() < 0.5 ? "palm0" : "palm1");
        if (pk != null)
        {
            var g = pk.Spawn("body", root, Vector3.zero, height / 8f, Color.white);
            g.position = p;
            g.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
            return;
        }
        var t = new GameObject("Palm").transform;
        t.SetParent(root, false);
        t.position = p;
        t.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
        int segs = 6;
        float lean = R(8f, 22f);
        Vector3 cur = Vector3.zero;
        float segLen = height / segs;
        for (int i = 0; i < segs; i++)
        {
            float a = lean * (i + 1) / segs;
            Vector3 dir = Quaternion.Euler(0f, 0f, -a) * Vector3.up;   // bend towards +x
            Vector3 mid = cur + dir * segLen * 0.5f;
            float r = Mathf.Lerp(0.42f, 0.26f, i / (float)segs);
            Mats.Prim(PrimitiveType.Cylinder, t, mid, new Vector3(r, segLen * 0.52f, r), new Vector3(0f, 0f, -a), i % 2 == 0 ? trunkA : trunkB);
            cur += dir * segLen;
        }
        // crown
        int fronds = 7;
        for (int i = 0; i < fronds; i++)
        {
            float yaw = i * 360f / fronds + R(-10f, 10f);
            var f = new GameObject("Frond").transform;
            f.SetParent(t, false);
            f.localPosition = cur;
            f.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Mats.Prim(PrimitiveType.Cube, f, new Vector3(0f, -0.4f, 1.6f), new Vector3(0.9f, 0.06f, 3.4f), new Vector3(R(18f, 30f), 0f, 0f), i % 2 == 0 ? leafA : leafB);
        }
        for (int i = 0; i < 3; i++)
            Mats.Prim(PrimitiveType.Sphere, t, cur + new Vector3(Mathf.Cos(i * 2.1f) * 0.3f, -0.35f, Mathf.Sin(i * 2.1f) * 0.3f), Vector3.one * 0.32f, coco);
    }

    static void Rock(Transform root, Vector3 p, float s)
    {
        LBPack pk = PackOr(rnd.NextDouble() < 0.5 ? "rock0" : "rock1");
        if (pk != null)
        {
            var g = pk.Spawn("body", root, Vector3.zero, 1f, Color.white);
            g.position = p + Vector3.down * s * 0.15f;
            g.rotation = Quaternion.Euler(R(-8f, 8f), R(0f, 360f), R(-8f, 8f));
            g.localScale = new Vector3(s * R(1.1f, 1.7f), s * R(0.8f, 1.2f), s * R(1.1f, 1.5f));
            return;
        }
        Mats.Prim(PrimitiveType.Sphere, root, p + Vector3.up * s * 0.2f, new Vector3(s * R(1f, 1.6f), s * R(0.6f, 0.9f), s * R(1f, 1.4f)), new Vector3(R(-10f, 10f), R(0f, 360f), 0f), rock);
    }

    static void Umbrella(Transform root, Vector3 p)
    {
        var t = new GameObject("Umbrella").transform;
        t.SetParent(root, false);
        t.position = p;
        t.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
        Color[] cols = { new Color(1f, 0.3f, 0.3f), new Color(0.2f, 0.6f, 1f), new Color(1f, 0.8f, 0.2f), new Color(0.3f, 0.85f, 0.5f), new Color(1f, 0.5f, 0.8f) };
        Color c = cols[rnd.Next(cols.Length)];
        Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 1.3f, 0f), new Vector3(0.08f, 1.3f, 0.08f), new Vector3(8f, 0f, 0f), white);
        Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 2.55f, 0.18f), new Vector3(3.2f, 0.7f, 3.2f), new Vector3(8f, 0f, 0f), Mats.Lit(c));
        Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 2.62f, 0.18f), new Vector3(1.5f, 0.6f, 1.5f), new Vector3(8f, 0f, 0f), white);
        Mats.Prim(PrimitiveType.Cube, t, new Vector3(1.2f, 0.03f, 0.6f), new Vector3(0.9f, 0.04f, 1.9f), Mats.Lit(cols[rnd.Next(cols.Length)]));
        Mats.Prim(PrimitiveType.Cube, t, new Vector3(-1.1f, 0.03f, 0.4f), new Vector3(0.9f, 0.04f, 1.9f), new Vector3(0f, 12f, 0f), Mats.Lit(cols[rnd.Next(cols.Length)]));
        if (rnd.NextDouble() < 0.5)
            Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0.4f, 0.3f, -1.6f), Vector3.one * 0.6f, Mats.Lit(cols[rnd.Next(cols.Length)]));
    }

    static void Hut(Transform root, Vector3 p, float yaw)
    {
        LBPack pk = PackOr("hut");
        if (pk != null)
        {
            var g = pk.Spawn("body", root, Vector3.zero, 1.15f, Color.white);
            g.position = p;
            g.rotation = Quaternion.Euler(0f, yaw, 0f);
            return;
        }
        var t = new GameObject("Hut").transform;
        t.SetParent(root, false);
        t.position = p;
        t.rotation = Quaternion.Euler(0f, yaw, 0f);
        for (int i = 0; i < 4; i++)
        {
            float x = (i % 2 == 0 ? -1f : 1f) * 1.9f, z = (i < 2 ? -1f : 1f) * 1.9f;
            Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(x, 1.4f, z), new Vector3(0.22f, 1.4f, 0.22f), wood);
        }
        Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.15f, 0f), new Vector3(4.6f, 0.3f, 4.6f), wood);
        Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 1.1f, 1.6f), new Vector3(3.6f, 0.15f, 0.8f), wood);   // bar counter
        Geo.Block(t, thatch, 0f, -2.8f, 2.8f, 5.6f, 0.2f, 2.8f, 4.6f, 5.6f, 0.2f, 2.8f, 4.6f);
    }

    static void Torch(Transform root, Vector3 p)
    {
        Mats.Prim(PrimitiveType.Cylinder, root, p + Vector3.up * 1f, new Vector3(0.12f, 1f, 0.12f), wood);
        Mats.Prim(PrimitiveType.Cylinder, root, p + Vector3.up * 2.05f, new Vector3(0.24f, 0.12f, 0.24f), dark);
        Mats.Prim(PrimitiveType.Sphere, root, p + Vector3.up * 2.35f, new Vector3(0.28f, 0.45f, 0.28f), flame);
    }

    static void Buoy(Transform root, Vector3 p, Color c)
    {
        Mats.Prim(PrimitiveType.Sphere, root, new Vector3(p.x, 0.1f, p.z), new Vector3(1f, 0.9f, 1f), Mats.Lit(c));
        Mats.Prim(PrimitiveType.Cylinder, root, new Vector3(p.x, 0.9f, p.z), new Vector3(0.12f, 0.5f, 0.12f), white);
    }

    static void Boat(Transform root, Vector3 p, float yaw)
    {
        var t = new GameObject("Boat").transform;
        t.SetParent(root, false);
        t.position = new Vector3(p.x, 0f, p.z);
        t.rotation = Quaternion.Euler(0f, yaw, 0f);
        Geo.Block(t, white, 0f, -3f, 3.2f, 1.6f, 2.4f, -0.4f, 0.7f, 0.1f, 1.4f, 0.3f, 0.9f);
        Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.95f, -0.6f), new Vector3(1.4f, 0.6f, 1.6f), Mats.Lit(new Color(0.2f, 0.5f, 0.85f)));
        Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 3f, 0.6f), new Vector3(0.1f, 2.6f, 0.1f), wood);
    }

    static void StartArch(Transform root)
    {
        Vector3 c = Track.PointAt(0f, 0f);
        Vector3 tan = Track.TangentAt(0f);
        var t = new GameObject("Start arch").transform;
        t.SetParent(root, false);
        t.position = new Vector3(c.x, c.y, c.z);
        t.rotation = Quaternion.LookRotation(tan, Vector3.up);
        float x = Track.HalfW + 1.8f;
        Material post = Mats.Lit(new Color(0.95f, 0.4f, 0.1f));
        for (int s = -1; s <= 1; s += 2)
        {
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(s * x, 3.5f, 0f), new Vector3(0.9f, 7f, 0.9f), post);
            Mats.Prim(PrimitiveType.Sphere, t, new Vector3(s * x, 7.4f, 0f), Vector3.one * 1.1f, Mats.Lit(new Color(1f, 0.85f, 0.2f)));
        }
        // checkered banner + start line
        Texture2D chk = Checker();
        var banner = Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 6.4f, 0f), new Vector3(x * 2f, 1.5f, 0.35f), Mats.Tex(chk, 0.1f));
        banner.name = "Banner";
        var line = Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.05f, 0f), new Vector3(Track.HalfW * 2f, 0.02f, 2.2f), Mats.Tex(chk, 0.1f));
        line.name = "Start line";
        Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 5.45f, 0f), new Vector3(x * 2f, 0.3f, 0.5f), dark);
    }

    static Texture2D Checker()
    {
        const int w = 64, h = 8;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, ((x / 4 + y / 4) % 2 == 0) ? Color.white : new Color(0.08f, 0.08f, 0.08f));
        tex.Apply();
        return tex;
    }
}
