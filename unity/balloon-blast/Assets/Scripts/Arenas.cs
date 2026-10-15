using UnityEngine;

// bb6: extra arenas built from primitives on the shared terrain (WebGL-safe, no new assets).
// 1 Candy Carnival: lollipops, gumdrops, candy canes, cupcakes, striped tents. 2 Snowy Fort: snowmen, ice walls, snow drifts, frosted pines.
public static class Arenas
{
    static bool saved;
    static Color fogC, ambS, ambE, ambG; static float fogD;
    static Texture[] layerTex;
    static Texture2D candyTex, snowTex;

    static float R(float a, float b) { return World.Rand(a, b); }
    static T Pick<T>(T[] a) { return a[World.rnd.Next(a.Length)]; }

    public static void ApplyLook(int arena, Terrain t)
    {
        if (!saved)
        {
            saved = true;
            fogC = RenderSettings.fogColor; fogD = RenderSettings.fogDensity;
            ambS = RenderSettings.ambientSkyColor; ambE = RenderSettings.ambientEquatorColor; ambG = RenderSettings.ambientGroundColor;
            if (t != null && t.terrainData != null)
            {
                var ls = t.terrainData.terrainLayers;
                layerTex = new Texture[ls.Length];
                for (int i = 0; i < ls.Length; i++) layerTex[i] = ls[i] != null ? ls[i].diffuseTexture : null;
            }
        }
        Texture ground = null;
        if (arena == 1)
        {
            RenderSettings.fogColor = new Color(1f, 0.82f, 0.9f); RenderSettings.fogDensity = 0.004f;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.55f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.66f, 0.55f, 0.6f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.32f, 0.36f);
            if (candyTex == null) candyTex = Noise(new Color(0.3f, 0.66f, 0.48f), new Color(0.82f, 0.45f, 0.62f), 7);
            ground = candyTex;
        }
        else if (arena == 2)
        {
            RenderSettings.fogColor = new Color(0.86f, 0.92f, 1f); RenderSettings.fogDensity = 0.006f;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.7f, 0.85f);
            RenderSettings.ambientEquatorColor = new Color(0.6f, 0.65f, 0.72f);
            RenderSettings.ambientGroundColor = new Color(0.55f, 0.58f, 0.64f);
            if (snowTex == null) snowTex = Noise(new Color(0.86f, 0.9f, 0.96f), new Color(1f, 1f, 1f), 11);
            ground = snowTex;
        }
        else
        {
            RenderSettings.fogColor = fogC; RenderSettings.fogDensity = fogD;
            RenderSettings.ambientSkyColor = ambS; RenderSettings.ambientEquatorColor = ambE; RenderSettings.ambientGroundColor = ambG;
        }
        if (t != null && t.terrainData != null && layerTex != null)
        {
            var ls = t.terrainData.terrainLayers;
            for (int i = 0; i < ls.Length && i < layerTex.Length; i++)
                if (ls[i] != null) ls[i].diffuseTexture = (Texture2D)(ground != null ? ground : layerTex[i]);
            t.terrainData.terrainLayers = ls;
        }
    }

    static Texture2D Noise(Color a, Color b, int seed)
    {
        var tex = new Texture2D(128, 128, TextureFormat.RGBA32, true);
        var r = new System.Random(seed);
        var px = new Color[128 * 128];
        for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.06f + seed, y * 0.06f) * 0.7f + (float)r.NextDouble() * 0.3f;
                px[y * 128 + x] = Color.Lerp(a, b, n);
            }
        tex.SetPixels(px); tex.Apply(true);
        tex.wrapMode = TextureWrapMode.Repeat;
        return tex;
    }


    // bb6: Kenney CC0 model (LB pack converted from glTF), scaled to height h, with a box collider + cover points
    static bool Model(string pack, Vector3 p, float h, float coverR)
    {
        var pk = LBPack.Get(pack);
        if (pk == null) return false;
        float s = h / Mathf.Max(0.02f, pk.Height());
        Transform g = World.Group(pack, p, 0f);
        pk.SpawnAll(g, Vector3.zero, s, Color.white);
        var b = new Bounds(g.position + Vector3.up * h * 0.5f, Vector3.zero);
        bool any = false;
        foreach (var r in g.GetComponentsInChildren<Renderer>()) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
        var bc = g.gameObject.AddComponent<BoxCollider>();
        bc.center = b.center - g.position; bc.size = b.size;
        g.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
        foreach (var t in g.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
        if (coverR > 0f) World.AddCover(g, coverR);
        return true;
    }

    // ---------------- Candy Carnival ----------------
    static readonly Color[] Cols = {
        new Color(1f, 0.42f, 0.62f), new Color(0.45f, 0.82f, 1f), new Color(1f, 0.85f, 0.3f),
        new Color(0.6f, 0.95f, 0.5f), new Color(0.8f, 0.55f, 1f), new Color(1f, 0.6f, 0.35f) };

    public static void Candy(bool mob)
    {
        Vector3 q;
        for (int i = 0; i < 18; i++) if (World.TryPlace(2.2f, 0f, 100f, out q) && !Model("k_lollypop", q, R(4f, 5.5f), 0.8f)) Lollipop(q);
        for (int i = 0; i < 10; i++) if (World.TryPlace(2.0f, 0f, 100f, out q)) Gumdrops(q);
        for (int i = 0; i < 14; i++) if (World.TryPlace(1.6f, 0f, 100f, out q) && !Model("k_candy-cane-red", q, R(3.2f, 4f), 0.6f)) CandyCane(q);
        for (int i = 0; i < 12; i++) if (World.TryPlace(2.4f, 0f, 100f, out q) && !Model("k_cupcake", q, R(2.2f, 2.8f), 1.4f)) Cupcake(q);
        string[] treats = { "k_cake", "k_muffin", "k_ice-cream", "k_popsicle", "k_donut-sprinkles", "k_cookie-chocolate", "k_gingerbread-man", "k_present-a-cube" };
        float[] th = { 2.2f, 2.0f, 3.6f, 3.6f, 1.0f, 0.7f, 3.0f, 1.5f };
        for (int i = 0; i < 22; i++) if (World.TryPlace(2.4f, 0f, 100f, out q)) { int k = World.rnd.Next(treats.Length); Model(treats[k], q, th[k] * R(0.9f, 1.15f), 1.3f); }
        for (int i = 0; i < 8; i++) if (World.TryPlace(3.2f, 10f, 95f, out q)) Tent(q);
        for (int i = 0; i < 12; i++) if (World.TryPlace(1.4f, 0f, 100f, out q)) World.Crates(q);
        int outer = mob ? 30 : 48;
        for (int i = 0; i < outer; i++) if (World.TryPlace(2.5f, 100f, 132f, out q)) Lollipop(q, 1.8f);
    }

    static void Lollipop(Vector3 p, float s = 1f)
    {
        s *= R(0.9f, 1.4f);
        Transform g = World.Group("Lollipop", p);
        World.P(PrimitiveType.Cylinder, g, new Vector3(0f, 1.6f * s, 0f), new Vector3(0.18f, 1.6f * s, 0.18f), Mats.Paint(Color.white, 0.6f), Vector3.zero);
        Color c = Pick(Cols);
        World.P(PrimitiveType.Cylinder, g, new Vector3(0f, 3.6f * s, 0f), new Vector3(2.0f * s, 0.18f, 2.0f * s), Mats.Paint(c, 0.9f), new Vector3(90f, 0f, 0f));
        World.P(PrimitiveType.Cylinder, g, new Vector3(0f, 3.6f * s, 0f), new Vector3(1.3f * s, 0.2f, 1.3f * s), Mats.Paint(Color.white, 0.9f), new Vector3(90f, 0f, 0f), false);
        World.P(PrimitiveType.Cylinder, g, new Vector3(0f, 3.6f * s, 0f), new Vector3(0.65f * s, 0.22f, 0.65f * s), Mats.Paint(c, 0.9f), new Vector3(90f, 0f, 0f), false);
        World.AddCover(g, 0.6f);
    }

    static void Gumdrops(Vector3 p)
    {
        Transform g = World.Group("Gumdrops", p);
        int n = World.rnd.Next(2, 5);
        for (int k = 0; k < n; k++)
        {
            float s = R(0.9f, 1.7f);
            Vector3 lp = new Vector3(R(-1.2f, 1.2f), 0.25f * s, R(-1.2f, 1.2f));
            World.P(PrimitiveType.Sphere, g, lp, new Vector3(s, s * 1.1f, s), Mats.Paint(Pick(Cols), 0.95f), Vector3.zero);
        }
        World.AddCover(g, 1.4f);
    }

    static void CandyCane(Vector3 p)
    {
        Transform g = World.Group("CandyCane", p);
        Material red = Mats.Paint(new Color(0.95f, 0.18f, 0.25f), 0.85f), wht = Mats.Paint(Color.white, 0.85f);
        for (int k = 0; k < 8; k++)
            World.P(PrimitiveType.Cylinder, g, new Vector3(0f, 0.2f + k * 0.4f, 0f), new Vector3(0.45f, 0.2f, 0.45f), k % 2 == 0 ? red : wht, Vector3.zero);
        World.P(PrimitiveType.Cylinder, g, new Vector3(0.35f, 3.35f, 0f), new Vector3(0.45f, 0.4f, 0.45f), red, new Vector3(0f, 0f, 60f));
        World.P(PrimitiveType.Cylinder, g, new Vector3(0.85f, 3.15f, 0f), new Vector3(0.45f, 0.3f, 0.45f), wht, new Vector3(0f, 0f, 120f));
        World.AddCover(g, 0.4f);
    }

    static void Cupcake(Vector3 p)
    {
        Transform g = World.Group("Cupcake", p);
        float s = R(1f, 1.5f);
        World.P(PrimitiveType.Cylinder, g, new Vector3(0f, 0.55f * s, 0f), new Vector3(2.0f * s, 0.55f * s, 2.0f * s), Mats.Paint(Pick(Cols), 0.4f), Vector3.zero);
        World.P(PrimitiveType.Sphere, g, new Vector3(0f, 1.25f * s, 0f), new Vector3(2.3f * s, 1.3f * s, 2.3f * s), Mats.Paint(new Color(1f, 0.93f, 0.95f), 0.7f), Vector3.zero);
        World.P(PrimitiveType.Sphere, g, new Vector3(0f, 2.0f * s, 0f), Vector3.one * 0.5f * s, Mats.Paint(new Color(0.95f, 0.1f, 0.2f), 0.95f), Vector3.zero, false);
        for (int k = 0; k < 10; k++)
        {
            Vector3 d = Random.onUnitSphere; d.y = Mathf.Abs(d.y) * 0.6f + 0.3f;
            World.P(PrimitiveType.Cube, g, new Vector3(0f, 1.25f * s, 0f) + Vector3.Scale(d.normalized, new Vector3(1.1f, 0.62f, 1.1f) * s), new Vector3(0.08f, 0.08f, 0.25f), Mats.Paint(Pick(Cols), 0.9f), new Vector3(R(0f, 360f), R(0f, 360f), 0f), false);
        }
        World.AddCover(g, 1.2f * s);
    }

    static void Tent(Vector3 p)
    {
        Transform g = World.Group("Tent", p);
        Color c = Pick(Cols);
        for (int k = 0; k < 8; k++)
        {
            float a = k * 45f;
            Vector3 d = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
            World.P(PrimitiveType.Cube, g, d * 2.2f + Vector3.up * 1.1f, new Vector3(1.85f, 2.2f, 0.12f), Mats.Paint(k % 2 == 0 ? c : Color.white, 0.5f), new Vector3(0f, a, 0f));
        }
        // roof: stacked shrinking discs
        for (int k = 0; k < 5; k++)
            World.P(PrimitiveType.Cylinder, g, new Vector3(0f, 2.3f + k * 0.35f, 0f), new Vector3(5.2f - k * 1.0f, 0.18f, 5.2f - k * 1.0f), Mats.Paint(k % 2 == 0 ? c : Color.white, 0.6f), Vector3.zero, false);
        World.P(PrimitiveType.Sphere, g, new Vector3(0f, 4.2f, 0f), Vector3.one * 0.45f, Mats.Paint(new Color(1f, 0.85f, 0.25f), 0.9f), Vector3.zero, false);
        World.AddCover(g, 2.4f);
    }

    // ---------------- Snowy Fort ----------------
    public static void Snow(bool mob)
    {
        Vector3 q;
        for (int i = 0; i < 16; i++) if (World.TryPlace(1.6f, 0f, 100f, out q) && !Model(World.rnd.Next(2) == 0 ? "k_snowman" : "k_snowman-hat", q, R(2.3f, 2.8f), 0.8f)) Snowman(q);
        string[] gifts = { "k_present-a-cube", "k_present-b-round", "k_sled", "k_lantern", "k_gingerbread-man" };
        float[] gh = { 1.4f, 1.2f, 1.1f, 3.2f, 2.6f };
        for (int i = 0; i < 16; i++) if (World.TryPlace(1.8f, 0f, 100f, out q)) { int k = World.rnd.Next(gifts.Length); Model(gifts[k], q, gh[k] * R(0.9f, 1.15f), 1.0f); }
        for (int i = 0; i < 16; i++) if (World.TryPlace(2.6f, 0f, 100f, out q)) IceWall(q);
        for (int i = 0; i < 22; i++) if (World.TryPlace(2.4f, 0f, 105f, out q)) Drift(q);
        for (int i = 0; i < 12; i++) if (World.TryPlace(2.0f, 0f, 100f, out q)) World.Logs(q);
        for (int i = 0; i < 10; i++) if (World.TryPlace(2.2f, 0f, 105f, out q)) World.Rock(q, R(1.1f, 2.0f));
        string[] pines = { "k_tree-snow-a", "k_tree-snow-b", "k_tree-snow-c" };
        for (int i = 0; i < 18; i++) if (World.TryPlace(2.5f, 15f, 95f, out q) && !Model(pines[World.rnd.Next(3)], q, R(6f, 8f), 0.8f)) Pine(q, 1f);
        int outer = mob ? 40 : 64;
        for (int i = 0; i < outer; i++) if (World.TryPlace(2.5f, 100f, 132f, out q) && !Model(pines[World.rnd.Next(3)], q, R(8f, 11f), 0f)) Pine(q, 1.4f);
    }

    static Material SnowM { get { return Mats.Paint(new Color(0.96f, 0.98f, 1f), 0.35f); } }

    static void Snowman(Vector3 p)
    {
        Transform g = World.Group("Snowman", p);
        float s = R(0.9f, 1.25f);
        World.P(PrimitiveType.Sphere, g, new Vector3(0f, 0.6f * s, 0f), Vector3.one * 1.3f * s, SnowM, Vector3.zero);
        World.P(PrimitiveType.Sphere, g, new Vector3(0f, 1.5f * s, 0f), Vector3.one * 0.95f * s, SnowM, Vector3.zero);
        World.P(PrimitiveType.Sphere, g, new Vector3(0f, 2.2f * s, 0f), Vector3.one * 0.65f * s, SnowM, Vector3.zero);
        Material coal = Mats.Lit(new Color(0.1f, 0.1f, 0.12f));
        World.P(PrimitiveType.Sphere, g, new Vector3(-0.12f * s, 2.3f * s, 0.29f * s), Vector3.one * 0.08f * s, coal, Vector3.zero, false);
        World.P(PrimitiveType.Sphere, g, new Vector3(0.12f * s, 2.3f * s, 0.29f * s), Vector3.one * 0.08f * s, coal, Vector3.zero, false);
        World.P(PrimitiveType.Cylinder, g, new Vector3(0f, 2.2f * s, 0.42f * s), new Vector3(0.08f, 0.16f, 0.08f) * s, Mats.Paint(new Color(1f, 0.5f, 0.1f), 0.5f), new Vector3(90f, 0f, 0f), false);
        Color scarf = Pick(Cols);
        World.P(PrimitiveType.Cylinder, g, new Vector3(0f, 1.9f * s, 0f), new Vector3(0.72f, 0.07f, 0.72f) * s, Mats.Paint(scarf, 0.3f), Vector3.zero, false);
        World.P(PrimitiveType.Cylinder, g, new Vector3(0f, 2.6f * s, 0f), new Vector3(0.5f, 0.2f, 0.5f) * s, Mats.Paint(scarf, 0.3f), Vector3.zero, false);
        World.P(PrimitiveType.Sphere, g, new Vector3(0f, 2.85f * s, 0f), Vector3.one * 0.2f * s, Mats.Paint(Color.white, 0.3f), Vector3.zero, false);
        World.AddCover(g, 0.7f * s);
    }

    static void IceWall(Vector3 p)
    {
        Transform g = World.Group("IceWall", p);
        Material ice = Mats.Shiny(new Color(0.7f, 0.88f, 1f));
        int n = World.rnd.Next(3, 6);
        for (int k = 0; k < n; k++)
            for (int h = 0; h < 2; h++)
                World.P(PrimitiveType.Cube, g, new Vector3((k - n * 0.5f + 0.5f) * 1.05f + (h == 1 ? 0.5f : 0f), 0.4f + h * 0.8f, 0f), new Vector3(1f, 0.78f, 0.8f), ice, Vector3.zero);
        World.AddCover(g, n * 0.5f);
    }

    static void Drift(Vector3 p)
    {
        Transform g = World.Group("Drift", p);
        int n = World.rnd.Next(2, 4);
        for (int k = 0; k < n; k++)
        {
            float s = R(1.6f, 3.2f);
            World.P(PrimitiveType.Sphere, g, new Vector3(R(-1f, 1f), 0f, R(-1f, 1f)), new Vector3(s, s * 0.55f, s * 0.8f), SnowM, Vector3.zero);
        }
        World.AddCover(g, 1.5f);
    }

    static void Pine(Vector3 p, float s)
    {
        s *= R(0.85f, 1.3f);
        Transform g = World.Group("SnowPine", p);
        World.P(PrimitiveType.Cylinder, g, new Vector3(0f, 0.8f * s, 0f), new Vector3(0.35f * s, 0.8f * s, 0.35f * s), Mats.Lit(new Color(0.4f, 0.28f, 0.2f)), Vector3.zero);
        Material green = Mats.Lit(new Color(0.16f, 0.4f, 0.26f));
        for (int k = 0; k < 4; k++)
        {
            float w = (2.8f - k * 0.6f) * s, y = (1.6f + k * 1.0f) * s;
            World.P(PrimitiveType.Sphere, g, new Vector3(0f, y, 0f), new Vector3(w, 1.1f * s, w), green, Vector3.zero, false);
            World.P(PrimitiveType.Sphere, g, new Vector3(0f, y + 0.35f * s, 0f), new Vector3(w * 0.8f, 0.5f * s, w * 0.8f), SnowM, Vector3.zero, false);
        }
        var cap = g.gameObject.AddComponent<CapsuleCollider>();
        cap.radius = 0.4f * s; cap.height = 5f * s; cap.center = new Vector3(0f, 2.5f * s, 0f);
        World.AddCover(g, 0.8f);
    }
}
