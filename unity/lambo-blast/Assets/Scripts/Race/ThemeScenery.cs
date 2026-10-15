using UnityEngine;

// Dressing for the five generated tracks (Coconut Cove keeps Scenery.cs). Static props are primitives / LB packs merged per
// material (a few dozen draw calls); moving bits (Mover) stay separate. Everything is parented to Track.Root.
public static class ThemeScenery
{
    static System.Random rnd;
    static float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
    static Transform root, live;

    public static void Build()
    {
        int th = Track.Theme;
        rnd = new System.Random(100 + th);
        root = new GameObject("Scenery").transform;
        root.SetParent(Track.Root, false);
        live = new GameObject("Movers").transform;
        live.SetParent(Track.Root, false);
        switch (th)
        {
            case 1: Canyon(); break;
            case 2: Volcano(); break;
            case 3: Snow(); break;
            case 4: Jungle(); break;
            case 5: Sky(); break;
        }
        GapSigns();
        StartArch();
        MeshMerge.Merge(root, true);
        Roadside.Build();               // lambo9: armco / tyre walls / poles / boards + distant horizon
        Gfx.Grass(Track.Root, th);      // lambo8: blade tufts (own chunk meshes, not merged)
    }

    // ---------------------------------------------------------------- helpers
    static bool Clear(Vector3 p, float extra)
    {
        Track.Proj m = Track.Nearest(p, -1, 0);
        if (m.dist < Track.HalfW + Track.Wall + extra) return false;
        Track.Proj s = Track.NearestShortcut(p);
        if (s.dist < Track.ScHalf + Track.Wall + extra) return false;
        return true;
    }

    // a random spot beside the road, `near`..`far` metres past the soft wall
    static bool Beside(float near, float far, float extra, out Vector3 p)
    {
        float s = R(0f, Track.Length);
        float side = rnd.NextDouble() < 0.5 ? -1f : 1f;
        p = Track.PointAt(s, side * R(Track.HalfW + Track.Wall + near, Track.HalfW + Track.Wall + far));
        p.y = Track.HeightAt(p.x, p.z);
        return Clear(p, extra);
    }

    static bool Anywhere(float margin, float extra, out Vector3 p)
    {
        p = new Vector3(R(Track.Min.x - margin, Track.Max.x + margin), 0f, R(Track.Min.y - margin, Track.Max.y + margin));
        p.y = Track.HeightAt(p.x, p.z);
        return Clear(p, extra);
    }

    static void Rock(Vector3 p, float s, Color tint)
    {
        LBPack pk = LBPack.Get(rnd.NextDouble() < 0.5 ? "rock0" : "rock1");
        if (pk != null)
        {
            var g = pk.Spawn("body", root, Vector3.zero, 1f, tint);
            if (g != null)
            {
                Gfx.Reskin(g, tint * 0.75f, Gfx.RockFor(Track.Theme));     // lambo8: triplanar CC0 rock
                g.position = p + Vector3.down * s * 0.15f;
                g.rotation = Quaternion.Euler(R(-8f, 8f), R(0f, 360f), R(-8f, 8f));
                g.localScale = new Vector3(s * R(1.1f, 1.7f), s * R(0.8f, 1.3f), s * R(1.1f, 1.5f));
                return;
            }
        }
        Mats.Prim(PrimitiveType.Sphere, root, p + Vector3.up * s * 0.2f, new Vector3(s * R(1f, 1.6f), s * R(0.6f, 0.9f), s * R(1f, 1.4f)), new Vector3(R(-10f, 10f), R(0f, 360f), 0f), Gfx.Surf(tint * 0.6f, Gfx.RockFor(Track.Theme)));
    }

    static Transform Node(Vector3 p, float yaw)
    {
        var t = new GameObject("Prop").transform;
        t.SetParent(root, false);
        t.position = p;
        t.rotation = Quaternion.Euler(0f, yaw, 0f);
        return t;
    }

    static Transform LiveNode(string name, Vector3 p)
    {
        var t = new GameObject(name).transform;
        t.SetParent(live, false);
        t.position = p;
        return t;
    }

    static void Pine(Vector3 p, float h, Material leaf, Material snow, Material trunk)
    {
        if (Gfx.Pine(root, p, h * 1.1f, new Color(0.16f, 0.3f, 0.17f), new Color(0.36f, 0.27f, 0.2f), rnd.Next())) return;   // lambo8
        var t = Node(p, R(0f, 360f));
        Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, h * 0.12f, 0f), new Vector3(h * 0.07f, h * 0.12f, h * 0.07f), trunk);
        for (int i = 0; i < 3; i++)
        {
            float y0 = h * (0.18f + i * 0.24f), w = h * (0.55f - i * 0.13f);
            Geo.Block(t, leaf, 0f, -w * 0.5f, w * 0.5f, w, 0f, y0, y0 + h * 0.42f, w, 0f, y0, y0 + h * 0.42f);
            Geo.Block(t, leaf, 0f, -w * 0.5f, w * 0.5f, w, 0f, y0, y0 + h * 0.42f, w, 0f, y0, y0 + h * 0.42f).transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            if (snow != null)
                Geo.Block(t, snow, 0f, -w * 0.2f, w * 0.2f, w * 0.4f, 0f, y0 + h * 0.25f, y0 + h * 0.43f, w * 0.4f, 0f, y0 + h * 0.25f, y0 + h * 0.43f);
        }
    }

    // big chevron boards beside every gap lip (yellow / black, the gap's own colour stripe on top)
    static void GapSigns()
    {
        Material yel = Mats.Lit(new Color(1f, 0.82f, 0.1f)), blk = Mats.Lit(new Color(0.08f, 0.08f, 0.08f)), red = Mats.Unlit(new Color(1f, 0.25f, 0.15f));
        foreach (var g in Track.Gaps)
        {
            float s = g.a - 60f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 p = Track.PointAt(s, side * (Track.HalfW + Track.Wall + 2f));
                p.y = Track.Apron ? Track.PointAt(s, 0f).y : Mathf.Max(Track.HeightAt(p.x, p.z), Track.PointAt(s, 0f).y - 3f);
                var t = Node(p, 0f);
                t.rotation = Quaternion.LookRotation(-Track.TangentAt(s), Vector3.up);
                Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 1.5f, 0f), new Vector3(0.25f, 3f, 0.25f), blk);
                Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 3.6f, 0f), new Vector3(3.4f, 1.6f, 0.15f), yel);
                for (int k = -1; k <= 1; k++)
                    Mats.Prim(PrimitiveType.Cube, t, new Vector3(k * 1f, 3.6f, -0.1f), new Vector3(0.28f, 1.2f, 0.05f), new Vector3(0f, 0f, 35f), blk);
                Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 4.55f, 0f), new Vector3(3.4f, 0.3f, 0.16f), red);
            }
        }
    }

    static void StartArch()
    {
        Vector3 c = Track.PointAt(0f, 0f);
        Vector3 tan = Track.TangentAt(0f);
        var t = Node(c, 0f);
        t.rotation = Quaternion.LookRotation(tan, Vector3.up);
        float x = Track.HalfW + 1.8f;
        Color[] cols = { Color.white, new Color(0.95f, 0.45f, 0.15f), new Color(0.95f, 0.3f, 0.05f), new Color(0.2f, 0.5f, 0.95f), new Color(0.3f, 0.75f, 0.25f), new Color(0.9f, 0.2f, 0.6f) };
        Material post = Mats.Lit(cols[Mathf.Clamp(Track.Theme, 0, 5)]), dark = Mats.Lit(new Color(0.15f, 0.15f, 0.17f));
        for (int s = -1; s <= 1; s += 2)
        {
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(s * x, 3.5f, 0f), new Vector3(0.9f, 7f, 0.9f), post);
            Mats.Prim(PrimitiveType.Sphere, t, new Vector3(s * x, 7.4f, 0f), Vector3.one * 1.1f, Mats.Lit(new Color(1f, 0.85f, 0.2f)));
        }
        Material chk = Checker();
        Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 6.4f, 0f), new Vector3(x * 2f, 1.5f, 0.35f), chk);
        Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.05f, 0f), new Vector3(Track.HalfW * 2f, 0.02f, 2.2f), chk);
        Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 5.45f, 0f), new Vector3(x * 2f, 0.3f, 0.5f), dark);
        if (!Track.Apron) StandAt(root, cols[Mathf.Clamp(Track.Theme, 0, 5)]);
    }

    // lambo8: a covered grandstand beside the start straight, just outside the soft wall (visual only, like every prop)
    public static void StandAt(Transform parent, Color accent)
    {
        float s = Track.Length - 38f;
        for (int side = 1; side >= -1; side -= 2)
        {
            Vector3 c0 = Track.PointAt(s, 0f), p = Track.PointAt(s, side * (Track.HalfW + Track.Wall + 6f));
            Track.Proj sc = Track.NearestShortcut(p);
            if (sc.dist < Track.ScHalf + 12f) continue;
            p.y = Mathf.Max(Track.HeightAt(p.x, p.z), c0.y - 2.5f) - 0.2f;
            Vector3 away = Track.Flat(p - c0).normalized;
            Gfx.Grandstand(parent, p, Quaternion.LookRotation(away, Vector3.up), 34f, accent);
            return;
        }
    }

    static Material checkerMat;
    static Material Checker()
    {
        if (checkerMat != null) return checkerMat;
        const int w = 64, h = 8;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, ((x / 4 + y / 4) % 2 == 0) ? Color.white : new Color(0.08f, 0.08f, 0.08f));
        tex.Apply();
        checkerMat = Mats.Tex(tex, 0.1f);
        return checkerMat;
    }

    // ---------------------------------------------------------------- Canyon Leap
    static void Canyon()
    {
        Color[] bands = { new Color(0.66f, 0.33f, 0.2f), new Color(0.76f, 0.45f, 0.28f), new Color(0.58f, 0.28f, 0.17f), new Color(0.8f, 0.55f, 0.36f) };
        Material cactus = Gfx.Surf(new Color(0.3f, 0.5f, 0.24f), Gfx.K.Leaf);
        // hoodoos / rock spires: stacked tapered blocks in sandstone bands
        for (int i = 0, made = 0; i < 160 && made < 34; i++)
        {
            Vector3 p;
            if (!(i % 2 == 0 ? Beside(8f, 60f, 8f, out p) : Anywhere(60f, 12f, out p))) continue;
            var t = Node(p + Vector3.down * 0.5f, R(0f, 360f));
            float w = R(5f, 11f), y = 0f;
            int parts = rnd.Next(2, 5);
            for (int k = 0; k < parts; k++)
            {
                float hh = R(4f, 9f), w2 = w * R(0.7f, 0.95f);
                Geo.Block(t, Gfx.Surf(bands[(k + made) % bands.Length], Gfx.K.SandRock), 0f, -w * 0.45f, w * 0.45f, w, w2, y, y + hh, w, w2, y, y + hh);
                y += hh; w = w2;
            }
            Geo.Block(t, Gfx.Surf(bands[3], Gfx.K.SandRock), 0f, -w * 0.6f, w * 0.6f, w * 1.3f, w * 1.2f, y, y + 1.6f, w * 1.3f, w * 1.2f, y, y + 1.6f);
            made++;
        }
        // saguaro cacti
        for (int i = 0, made = 0; i < 200 && made < 55; i++)
        {
            Vector3 p;
            if (!Beside(2f, 45f, 3f, out p)) continue;
            var t = Node(p, R(0f, 360f));
            float h = R(3.5f, 6.5f);
            Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, h * 0.5f, 0f), new Vector3(0.55f, h * 0.5f, 0.55f), cactus);
            for (int a = 0; a < 2; a++)
            {
                float sgn = a == 0 ? 1f : -1f, ay = h * R(0.35f, 0.6f);
                Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(sgn * 0.7f, ay, 0f), new Vector3(0.38f, 0.45f, 0.38f), new Vector3(0f, 0f, 90f), cactus);
                Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(sgn * 1.05f, ay + 0.9f, 0f), new Vector3(0.36f, 0.9f, 0.36f), cactus);
            }
            made++;
        }
        for (int i = 0; i < 70; i++) { Vector3 p; if (Beside(0f, 50f, 2f, out p)) Rock(p, R(0.8f, 2.6f), new Color(0.85f, 0.55f, 0.4f)); }
        // a broken rail trestle at the "Rail gap"
        if (Track.Gaps.Count > 3)
        {
            var g = Track.Gaps[3];
            Material wood = Gfx.Surf(new Color(0.45f, 0.32f, 0.2f), Gfx.K.Wood), steel = Mats.PBR(new Color(0.3f, 0.3f, 0.32f), 0.5f, 0.7f);
            for (int e = 0; e < 2; e++)
            {
                float s = e == 0 ? g.a - 1f : g.b + 1f;
                Vector3 p = Track.PointAt(s, 0f);
                var t = Node(p, 0f);
                t.rotation = Quaternion.LookRotation(Track.TangentAt(s), Vector3.up);
                for (int k = -2; k <= 2; k++)
                    Mats.Prim(PrimitiveType.Cube, t, new Vector3(k * 3.6f, -8f, 0f), new Vector3(0.6f, 16f, 0.6f), wood);
                Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, -1f, 0f), new Vector3(16f, 0.6f, 0.8f), wood);
                for (int sg = -1; sg <= 1; sg += 2)
                    Mats.Prim(PrimitiveType.Cube, t, new Vector3(sg * (Track.HalfW + 2.5f), 0.4f, (e == 0 ? 3f : -3f)), new Vector3(0.3f, 0.25f, 7f), new Vector3(e == 0 ? 8f : -8f, 0f, 0f), steel);
            }
        }
        // moving: tumbleweeds across the road, vultures circling, a hot-air balloon
        Material weed = Gfx.Surf(new Color(0.6f, 0.48f, 0.3f), Gfx.K.Wood);
        for (int i = 0; i < 4; i++)
        {
            float s = Track.Length * (0.15f + i * 0.23f);
            Vector3 a = Track.PointAt(s, -40f), b = Track.PointAt(s + 20f, 40f);
            var t = LiveNode("Tumbleweed", a);
            Mats.Prim(PrimitiveType.Sphere, t, Vector3.zero, Vector3.one * 1.3f, weed);
            Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0.2f, 0.1f, 0f), Vector3.one * 1.0f, new Vector3(30f, 20f, 0f), weed);
            var m = t.gameObject.AddComponent<Mover>(); m.mode = 2; m.a = a; m.b = b; m.speed = R(5f, 8f); m.phase = R(0f, 10f);
        }
        Material bird = Mats.Lit(new Color(0.12f, 0.1f, 0.1f));
        Vector3 mid = new Vector3((Track.Min.x + Track.Max.x) * 0.5f, 70f, (Track.Min.y + Track.Max.y) * 0.5f);
        for (int i = 0; i < 4; i++)
        {
            var t = LiveNode("Vulture", mid);
            Mats.Prim(PrimitiveType.Cube, t, Vector3.zero, new Vector3(3.2f, 0.12f, 0.8f), bird);
            Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 0f, 0.4f), new Vector3(0.5f, 0.4f, 0.9f), bird);
            var m = t.gameObject.AddComponent<Mover>(); m.mode = 0; m.a = mid + new Vector3(R(-80f, 80f), R(0f, 20f), R(-80f, 80f)); m.radius = R(25f, 50f); m.speed = R(0.15f, 0.3f) * (i % 2 == 0 ? 1f : -1f); m.phase = R(0f, 20f);
        }
        {
            var t = LiveNode("Balloon", mid);
            Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 6f, 0f), new Vector3(10f, 12f, 10f), Mats.Lit(new Color(0.95f, 0.3f, 0.2f)));
            Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 6f, 0f), new Vector3(10.2f, 4f, 10.2f), Mats.Lit(new Color(1f, 0.85f, 0.2f)));
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, -2.5f, 0f), new Vector3(2f, 1.4f, 2f), Mats.Lit(new Color(0.45f, 0.32f, 0.2f)));
            var m = t.gameObject.AddComponent<Mover>(); m.mode = 0; m.a = mid + Vector3.up * 30f; m.radius = 160f; m.speed = 0.03f;
        }
    }

    // ---------------------------------------------------------------- Volcano Rim
    static void Volcano()
    {
        Material basalt = Gfx.Surf(new Color(0.16f, 0.14f, 0.13f), Gfx.K.Basalt), glow = Mats.Unlit(new Color(1f, 0.45f, 0.08f)), dead = Gfx.Surf(new Color(0.22f, 0.17f, 0.14f), Gfx.K.Bark);
        for (int i = 0, made = 0; i < 160 && made < 40; i++)
        {
            Vector3 p;
            if (!(i % 2 == 0 ? Beside(4f, 50f, 6f, out p) : Anywhere(40f, 10f, out p))) continue;
            if (p.y < Track.LavaY + 0.5f) continue;
            var t = Node(p + Vector3.down * 0.5f, R(0f, 360f));
            float w = R(2.5f, 6f), h = R(6f, 18f);
            Geo.Block(t, basalt, 0f, -w * 0.5f, w * 0.5f, w, w * 0.25f, 0f, h, w * 0.8f, w * 0.2f, 0f, h * 0.9f);
            if (rnd.NextDouble() < 0.5) Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 0.2f, w * 0.6f), Vector3.one * R(0.6f, 1.2f), glow);
            made++;
        }
        for (int i = 0, made = 0; i < 120 && made < 30; i++)
        {
            Vector3 p;
            if (!Beside(1f, 40f, 3f, out p) || p.y < Track.LavaY + 0.5f) continue;
            var t = Node(p, R(0f, 360f));
            float h = R(4f, 7f);
            Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, h * 0.5f, 0f), new Vector3(0.35f, h * 0.5f, 0.35f), dead);
            Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(0.8f, h * 0.75f, 0f), new Vector3(0.18f, 1.1f, 0.18f), new Vector3(0f, 0f, -50f), dead);
            Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(-0.6f, h * 0.6f, 0.3f), new Vector3(0.15f, 0.9f, 0.15f), new Vector3(30f, 0f, 45f), dead);
            made++;
        }
        for (int i = 0; i < 70; i++) { Vector3 p; if (Beside(0f, 50f, 2f, out p) && p.y > Track.LavaY + 0.5f) Rock(p, R(0.8f, 2.4f), new Color(0.3f, 0.26f, 0.25f)); }
        // smoke columns from the crater + a few vents
        Vector3 crater = new Vector3(-40f, Track.HeightAt(-40f, -60f) + 4f, -60f);
        for (int i = 0; i < 3; i++)
        {
            var t = LiveNode("Crater smoke", crater + new Vector3(R(-20f, 20f), 0f, R(-20f, 20f)));
            var m = t.gameObject.AddComponent<Mover>(); m.mode = 3; m.speed = 7f; m.smoke = new Color(0.22f, 0.2f, 0.2f, 0.75f);
        }
        var gl = LiveNode("Crater glow", crater);
        var gm = gl.gameObject.AddComponent<Mover>(); gm.mode = 3; gm.speed = 5f; gm.smoke = new Color(1f, 0.45f, 0.1f, 0.8f);
    }

    // ---------------------------------------------------------------- Snowy Summit
    static void Snow()
    {
        Material leaf = Mats.Lit(new Color(0.12f, 0.3f, 0.18f)), snow = Mats.Lit(new Color(0.95f, 0.96f, 1f)), trunk = Mats.Lit(new Color(0.35f, 0.25f, 0.16f));
        int trees = Look.Mobile ? 70 : 130;
        for (int i = 0, made = 0; i < trees * 3 && made < trees; i++)
        {
            Vector3 p;
            if (!(i % 3 != 0 ? Beside(1f, 55f, 3f, out p) : Anywhere(80f, 8f, out p))) continue;
            Pine(p + Vector3.down * 0.3f, R(7f, 14f), leaf, snow, trunk);
            made++;
        }
        for (int i = 0; i < 50; i++) { Vector3 p; if (Beside(0f, 50f, 2f, out p)) Rock(p, R(1f, 3f), new Color(0.62f, 0.66f, 0.74f)); }
        // ski-jump tower + flags at the first gap
        if (Track.Gaps.Count > 0)
        {
            var g = Track.Gaps[0];
            Material red = Mats.Lit(new Color(0.9f, 0.15f, 0.15f)), white = Mats.Lit(Color.white), steel = Mats.Lit(new Color(0.45f, 0.47f, 0.5f));
            for (int k = 0; k < 6; k++)
            {
                float s = g.a - 10f - k * 12f;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 p = Track.PointAt(s, side * (Track.HalfW + 2f));
                    p.y = Track.PointAt(s, 0f).y;
                    Mats.Prim(PrimitiveType.Cylinder, root, p + Vector3.up * 2f, new Vector3(0.12f, 2f, 0.12f), white);
                    Mats.Prim(PrimitiveType.Cube, root, p + Vector3.up * 3.5f + Track.TangentAt(s) * 0.6f, new Vector3(0.05f, 0.9f, 1.2f), k % 2 == 0 ? red : Mats.Lit(new Color(0.15f, 0.4f, 0.95f)));
                }
            }
            Vector3 tp = Track.PointAt(g.a - 6f, Track.HalfW + 12f);
            tp.y = Track.HeightAt(tp.x, tp.z);
            var t = Node(tp, 0f);
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 9f, 0f), new Vector3(3f, 18f, 3f), steel);
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 19f, 0f), new Vector3(6f, 3f, 6f), red);
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 20.8f, 0f), new Vector3(6.6f, 0.4f, 6.6f), white);
        }
        // ski lift across the map: pylons + moving cabins
        Vector3 a = new Vector3(Track.Min.x - 40f, 0f, Track.Min.y + 60f), b = new Vector3(Track.Max.x + 40f, 0f, Track.Max.y - 60f);
        int pyl = 8;
        Material pm = Mats.Lit(new Color(0.4f, 0.42f, 0.46f));
        var cable = new Vector3[pyl];
        for (int i = 0; i < pyl; i++)
        {
            Vector3 p = Vector3.Lerp(a, b, i / (float)(pyl - 1));
            float gy = Track.HeightAt(p.x, p.z);
            Mats.Prim(PrimitiveType.Cube, root, new Vector3(p.x, gy + 11f, p.z), new Vector3(0.8f, 22f, 0.8f), pm);
            Mats.Prim(PrimitiveType.Cube, root, new Vector3(p.x, gy + 22f, p.z), new Vector3(5f, 0.5f, 0.6f), pm);
            cable[i] = new Vector3(p.x, gy + 21f, p.z);
        }
        var loop = new Vector3[pyl * 2];
        for (int i = 0; i < pyl; i++) { loop[i] = cable[i] + Vector3.right * 2f; loop[pyl * 2 - 1 - i] = cable[i] - Vector3.right * 2f; }
        Material cab = Mats.Lit(new Color(0.9f, 0.2f, 0.2f)), glass = Mats.Lit(new Color(0.4f, 0.6f, 0.75f));
        for (int i = 0; i < 6; i++)
        {
            var t = LiveNode("Gondola", loop[0]);
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, -3.5f, 0f), new Vector3(2.4f, 2.2f, 2.4f), cab);
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, -3.2f, 0f), new Vector3(2.5f, 0.9f, 2.0f), glass);
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, -1.2f, 0f), new Vector3(0.15f, 2.4f, 0.15f), pm);
            var m = t.gameObject.AddComponent<Mover>(); m.mode = 1; m.path = loop; m.speed = 5f; m.phase = i * 60f;
        }
    }

    // ---------------------------------------------------------------- Jungle Ruins
    static void Jungle()
    {
        Material trunk = Mats.Lit(new Color(0.36f, 0.27f, 0.18f)), leafA = Mats.Lit(new Color(0.15f, 0.42f, 0.14f)), leafB = Mats.Lit(new Color(0.22f, 0.52f, 0.18f));
        Material stone = Gfx.Surf(new Color(0.52f, 0.53f, 0.46f), Gfx.K.Stone), moss = Gfx.Surf(new Color(0.36f, 0.44f, 0.28f), Gfx.K.MossRock);
        int trees = Look.Mobile ? 60 : 110;
        for (int i = 0, made = 0; i < trees * 3 && made < trees; i++)
        {
            Vector3 p;
            if (!(i % 3 != 0 ? Beside(1f, 50f, 3f, out p) : Anywhere(60f, 8f, out p))) continue;
            if (p.y < Track.WaterY + 0.5f) continue;
            if (rnd.NextDouble() < 0.4)
            {
                LBPack pk = LBPack.Get(rnd.NextDouble() < 0.5 ? "palm0" : "palm1");
                if (pk != null)
                {
                    var g = pk.Spawn("body", root, Vector3.zero, R(7f, 11f) / 8f, Color.white);
                    if (g != null) { g.position = p + Vector3.down * 0.1f; g.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f); made++; continue; }
                }
            }
            {
                float h8 = R(11f, 19f);
                Color lc = Color.Lerp(new Color(0.13f, 0.36f, 0.12f), new Color(0.24f, 0.5f, 0.17f), R(0f, 1f));
                if (Gfx.Broadleaf(root, p + Vector3.down * 0.2f, h8, lc, new Color(0.4f, 0.32f, 0.24f), rnd.Next())) { made++; continue; }   // lambo8
            }
            var t = Node(p, R(0f, 360f));
            float h = R(9f, 16f);
            Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, h * 0.5f, 0f), new Vector3(0.7f, h * 0.5f, 0.7f), trunk);
            for (int k = 0; k < 3; k++)
                Mats.Prim(PrimitiveType.Sphere, t, new Vector3(R(-2f, 2f), h + R(-1f, 1.5f), R(-2f, 2f)), new Vector3(R(5f, 8f), R(3f, 4.5f), R(5f, 8f)), k % 2 == 0 ? leafA : leafB);
            made++;
        }
        // ruined pillars + blocks along the road
        for (int i = 0, made = 0; i < 120 && made < 36; i++)
        {
            Vector3 p;
            if (!Beside(0.5f, 14f, 2f, out p) || p.y < Track.WaterY + 0.5f) continue;
            var t = Node(p, R(0f, 360f));
            float h = R(2f, 7f);
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, h * 0.5f, 0f), new Vector3(1.6f, h, 1.6f), new Vector3(R(-6f, 6f), 0f, R(-6f, 6f)), stone);
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, h + 0.25f, 0f), new Vector3(2.1f, 0.5f, 2.1f), moss);
            made++;
        }
        // stepped temple pyramid inside the loop, next to the Temple steps gap
        {
            Vector3 c = new Vector3((Track.Min.x + Track.Max.x) * 0.5f, 0f, (Track.Min.y + Track.Max.y) * 0.5f);
            for (int tries = 0; tries < 40; tries++)
            {
                Vector3 p = c + new Vector3(R(-80f, 80f), 0f, R(-80f, 80f));
                if (!Clear(p, 30f)) continue;
                p.y = Track.HeightAt(p.x, p.z) - 1f;
                var t = Node(p, R(0f, 90f));
                float w = 46f;
                for (int k = 0; k < 6; k++)
                {
                    Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, k * 4f + 2f, 0f), new Vector3(w, 4f, w), k % 2 == 0 ? stone : moss);
                    w -= 7f;
                }
                Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 27f, 0f), new Vector3(7f, 6f, 7f), stone);
                Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 12f, 23f), new Vector3(8f, 24f, 4f), new Vector3(45f, 0f, 0f), moss);
                break;
            }
        }
        // the broken bridge: snapped plank decks + rope posts on both lips of the first gap
        if (Track.Gaps.Count > 0)
        {
            var g = Track.Gaps[0];
            Material plank = Gfx.Surf(new Color(0.5f, 0.36f, 0.22f), Gfx.K.Wood), rope = Mats.Lit(new Color(0.55f, 0.48f, 0.32f));
            for (int e = 0; e < 2; e++)
            {
                float s = e == 0 ? g.a : g.b;
                Vector3 p = Track.PointAt(s, 0f);
                var t = Node(p, 0f);
                t.rotation = Quaternion.LookRotation(Track.TangentAt(s), Vector3.up);
                float dir = e == 0 ? 1f : -1f;
                Mats.Prim(PrimitiveType.Cube, t, new Vector3(-2.5f, -1.6f, dir * 3f), new Vector3(4f, 0.3f, 7f), new Vector3(dir * 28f, 0f, 6f), plank);
                Mats.Prim(PrimitiveType.Cube, t, new Vector3(3f, -2.4f, dir * 2.5f), new Vector3(3.5f, 0.3f, 6f), new Vector3(dir * 40f, 0f, -8f), plank);
                for (int sg = -1; sg <= 1; sg += 2)
                {
                    Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(sg * (Track.HalfW + 1.2f), 1.5f, -dir * 1f), new Vector3(0.5f, 1.5f, 0.5f), plank);
                    Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(sg * (Track.HalfW + 1.2f), 2.6f, dir * 4f), new Vector3(0.06f, 5f, 0.06f), new Vector3(70f * dir, 0f, 0f), rope);
                }
            }
        }
        // a waterfall at the waterfall leap: a tall blue sheet + foam
        if (Track.Gaps.Count > 3)
        {
            var g = Track.Gaps[3];
            Vector3 p = Track.PointAt((g.a + g.b) * 0.5f, -40f);
            var t = Node(new Vector3(p.x, Track.WaterY, p.z), 0f);
            t.rotation = Quaternion.LookRotation(Track.TangentAt(g.a), Vector3.up);
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 14f, 0f), new Vector3(1f, 28f, g.b - g.a - 6f), Mats.Lit(new Color(0.55f, 0.8f, 0.95f)));
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(-6f, 30f, 0f), new Vector3(14f, 4f, g.b - g.a + 6f), stone);
            var f = LiveNode("Waterfall spray", new Vector3(p.x, Track.WaterY + 1f, p.z));
            var m = f.gameObject.AddComponent<Mover>(); m.mode = 3; m.speed = 8f; m.smoke = new Color(0.9f, 0.95f, 1f, 0.6f);
        }
        // birds circling the canopy
        Material bird = Mats.Lit(new Color(0.9f, 0.25f, 0.15f));
        Vector3 mid = new Vector3((Track.Min.x + Track.Max.x) * 0.5f, 45f, (Track.Min.y + Track.Max.y) * 0.5f);
        for (int i = 0; i < 5; i++)
        {
            var t = LiveNode("Parrot", mid);
            Mats.Prim(PrimitiveType.Cube, t, Vector3.zero, new Vector3(1.6f, 0.08f, 0.5f), bird);
            Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 0f, 0.3f), new Vector3(0.35f, 0.3f, 0.7f), bird);
            var m = t.gameObject.AddComponent<Mover>(); m.mode = 0; m.a = mid + new Vector3(R(-90f, 90f), R(-10f, 10f), R(-90f, 90f)); m.radius = R(20f, 40f); m.speed = R(0.3f, 0.5f) * (i % 2 == 0 ? 1f : -1f); m.phase = R(0f, 20f);
        }
    }

    // ---------------------------------------------------------------- Sky City
    static Material[] facades;
    static Material Facade(int k)
    {
        if (facades == null)
        {
            facades = new Material[3];
            Color[] wall = { new Color(0.55f, 0.6f, 0.68f), new Color(0.72f, 0.66f, 0.58f), new Color(0.32f, 0.38f, 0.46f) };
            for (int f = 0; f < 3; f++)
            {
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGB24, true);
                tex.wrapMode = TextureWrapMode.Repeat;
                var r = new System.Random(50 + f);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        bool win = (x % 8) > 1 && (y % 8) > 2;
                        Color c = win ? (r.NextDouble() < 0.18 ? new Color(1f, 0.9f, 0.6f) : new Color(0.16f, 0.25f, 0.36f) * (0.8f + 0.4f * (float)r.NextDouble())) : wall[f];
                        tex.SetPixel(x, y, c);
                    }
                tex.Apply(true);
                facades[f] = Gfx.Tri ? null : Mats.Tex(tex, 0.5f);
                if (facades[f] != null) { facades[f].mainTextureScale = new Vector2(3f, 6f); continue; }
                // lambo8: 128 px curtain-wall facade (mullions, spandrels, sky-tinted glass, a few lit / blind windows) projected
                // in world space -> 3.5 m windows on every tower whatever its size (the old map stretched with each cube)
                const int q = 128;
                var t2 = new Texture2D(q, q, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
                var px = new Color32[q * q];
                for (int y = 0; y < q; y++)
                    for (int x = 0; x < q; x++)
                    {
                        int cx = x % 16, cy = y % 16, wx = x / 16, wy = y / 16;
                        float h1 = Mathf.Abs(Mathf.Sin(wx * 12.9898f + wy * 78.233f + f * 3.1f) * 43758.5453f) % 1f;
                        Color c;
                        if (cx < 2 || cy < 3) c = wall[f] * (cx == 0 || cy == 0 ? 0.8f : 1f);              // mullion / spandrel
                        else
                        {
                            float g = (cy - 3) / 13f;
                            Color glassC = Color.Lerp(new Color(0.34f, 0.46f, 0.6f), new Color(0.14f, 0.2f, 0.3f), g) * (0.85f + 0.3f * h1);
                            if (h1 > 0.9f) glassC = new Color(1f, 0.88f, 0.62f) * (0.9f + 0.1f * g);          // lit office
                            else if (h1 < 0.12f) glassC = new Color(0.66f, 0.66f, 0.62f) * (0.8f + 0.2f * g); // blinds down
                            c = glassC;
                        }
                        c.a = 1f;
                        px[y * q + x] = c;
                    }
                t2.SetPixels32(px);
                t2.Apply(true);
                Object.Destroy(tex);
                facades[f] = Gfx.Projected(t2, 1f / 28f, 0.62f);
                if (facades[f] == null) { facades[f] = Mats.Tex(t2, 0.5f); facades[f].mainTextureScale = new Vector2(3f, 6f); }
            }
        }
        return facades[k % 3];
    }

    static void Sky()
    {
        Material roof = Gfx.Surf(new Color(0.42f, 0.43f, 0.45f), Gfx.K.Concrete), ac = Gfx.Surf(new Color(0.75f, 0.76f, 0.78f), Gfx.K.Concrete), red = Mats.Unlit(new Color(1f, 0.15f, 0.1f));
        // buildings under the skyway: a block every 12 m, never across a gap (the gaps are the alleys between towers)
        for (int i = 0; i < Track.C.Length; i += 4)
        {
            float s = Track.S[i];
            if (Track.InGap(s, 8f)) continue;
            Vector3 p = Track.C[i];
            float bk = Track.Bank != null ? Mathf.Abs(Track.Bank[i]) : 0f;
            float top = p.y - Track.HalfW * bk - 0.25f;
            var t = Node(new Vector3(p.x, 0f, p.z), 0f);
            t.rotation = Quaternion.LookRotation(Track.T[i], Vector3.up);
            float w = 2f * (Track.HalfW + Track.Wall + 0.9f);
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, top * 0.5f, 0f), new Vector3(w, top, 13f), Facade(i / 12));
        }
        // girder towers under the crane-girder shortcut
        Material steel = Mats.Lit(new Color(0.95f, 0.7f, 0.1f));
        for (float s = 10f; s < Track.ScLength - 5f; s += 18f)
        {
            if (s > Track.ScGapA - 4f && s < Track.ScGapB + 4f) continue;
            Vector3 p = Track.ShortcutPoint(s);
            for (int sg = -1; sg <= 1; sg += 2)
            {
                Vector3 d = Track.Flat(Track.ShortcutPoint(s + 1f) - p).normalized, rt = new Vector3(d.z, 0f, -d.x);
                Vector3 q = p + rt * sg * (Track.ScHalf - 1f);
                Mats.Prim(PrimitiveType.Cube, root, new Vector3(q.x, (p.y - 0.6f) * 0.5f, q.z), new Vector3(0.8f, p.y - 0.6f, 0.8f), steel);
            }
        }
        // skyline: towers around the course
        for (int i = 0, made = 0; i < 500 && made < (Look.Mobile ? 70 : 120); i++)
        {
            Vector3 p;
            if (!Anywhere(140f, 6f, out p)) continue;
            float h = R(35f, 150f), w = R(14f, 30f), dpt = R(14f, 30f);
            var t = Node(new Vector3(p.x, 0f, p.z), rnd.Next(4) * 90f + R(-3f, 3f));
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, dpt), Facade(made));
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, h + 0.4f, 0f), new Vector3(w * 0.96f, 0.8f, dpt * 0.96f), roof);
            if (rnd.NextDouble() < 0.5) Mats.Prim(PrimitiveType.Cube, t, new Vector3(w * 0.2f, h + 1.8f, 0f), new Vector3(3f, 2.2f, 3f), ac);
            if (rnd.NextDouble() < 0.35)
            {
                Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, h + 8f, 0f), new Vector3(0.3f, 8f, 0.3f), ac);
                Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0f, h + 16.3f, 0f), Vector3.one * 0.8f, red);
            }
            made++;
        }
        // rooftop clutter beside the skyway
        for (int i = 0; i < 60; i++)
        {
            float s = R(0f, Track.Length);
            if (Track.InGap(s, 10f)) continue;
            Vector3 p = Track.PointAt(s, (rnd.NextDouble() < 0.5 ? -1f : 1f) * (Track.HalfW + Track.Wall - 0.5f));
            p.y = Track.PointAt(s, 0f).y;
            Mats.Prim(PrimitiveType.Cube, root, p + Vector3.up * 0.8f, new Vector3(R(1.5f, 3f), 1.6f, R(1.5f, 3f)), ac);
        }
        // billboard over the billboard jump
        if (Track.Gaps.Count > 4)
        {
            var g = Track.Gaps[4];
            Vector3 p = Track.PointAt(g.b + 30f, 0f);
            var t = Node(p, 0f);
            t.rotation = Quaternion.LookRotation(-Track.TangentAt(g.b), Vector3.up);
            for (int sg = -1; sg <= 1; sg += 2) Mats.Prim(PrimitiveType.Cube, t, new Vector3(sg * (Track.HalfW + 2.5f), 6f, 0f), new Vector3(0.7f, 12f, 0.7f), roof);
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 11f, 0f), new Vector3(2f * Track.HalfW + 6f, 5f, 0.4f), Mats.Unlit(new Color(0.15f, 0.75f, 1f)));
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 11f, -0.25f), new Vector3(2f * Track.HalfW, 2f, 0.1f), Mats.Unlit(new Color(1f, 0.85f, 0.2f)));
        }
        // a sky train looping around the city + a blimp
        float m0 = 90f;
        Vector3[] loop = {
            new Vector3(Track.Min.x - m0, 48f, Track.Min.y - m0), new Vector3(Track.Max.x + m0, 48f, Track.Min.y - m0),
            new Vector3(Track.Max.x + m0, 48f, Track.Max.y + m0), new Vector3(Track.Min.x - m0, 48f, Track.Max.y + m0) };
        Material rail = Mats.Lit(new Color(0.5f, 0.52f, 0.56f));
        for (int i = 0; i < 4; i++)
        {
            Vector3 a = loop[i], b = loop[(i + 1) % 4];
            int posts = Mathf.CeilToInt((b - a).magnitude / 40f);
            for (int k = 0; k < posts; k++) { Vector3 q = Vector3.Lerp(a, b, k / (float)posts); Mats.Prim(PrimitiveType.Cube, root, new Vector3(q.x, 23.5f, q.z), new Vector3(1.6f, 47f, 1.6f), rail); }
            Vector3 mid = (a + b) * 0.5f;
            var beam = Mats.Prim(PrimitiveType.Cube, root, mid + Vector3.down * 0.9f, new Vector3(2.6f, 0.8f, (b - a).magnitude), rail);
            beam.transform.rotation = Quaternion.LookRotation((b - a).normalized, Vector3.up);
        }
        Material car = Mats.Lit(new Color(0.95f, 0.95f, 0.97f)), stripe = Mats.Unlit(new Color(0.2f, 0.6f, 1f));
        for (int i = 0; i < 4; i++)
        {
            var t = LiveNode("Sky train", loop[0]);
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 1.4f, 0f), new Vector3(3f, 2.8f, 13f), car);
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 1.7f, 0f), new Vector3(3.05f, 0.5f, 12.6f), stripe);
            var m = t.gameObject.AddComponent<Mover>(); m.mode = 1; m.path = loop; m.speed = 26f; m.phase = i * 0.55f;
        }
        {
            Vector3 c = new Vector3((Track.Min.x + Track.Max.x) * 0.5f, 140f, (Track.Min.y + Track.Max.y) * 0.5f);
            var t = LiveNode("Blimp", c);
            Mats.Prim(PrimitiveType.Sphere, t, Vector3.zero, new Vector3(12f, 12f, 40f), Mats.Lit(new Color(0.85f, 0.85f, 0.9f)));
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, -6.5f, 2f), new Vector3(3f, 2f, 7f), Mats.Lit(new Color(0.3f, 0.3f, 0.35f)));
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0f, -18f), new Vector3(0.4f, 10f, 6f), Mats.Lit(new Color(0.9f, 0.2f, 0.6f)));
            var m = t.gameObject.AddComponent<Mover>(); m.mode = 0; m.a = c; m.radius = 220f; m.speed = 0.04f;
        }
    }
}
