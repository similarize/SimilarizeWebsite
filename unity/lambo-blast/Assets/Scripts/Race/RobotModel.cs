using UnityEngine;

// Procedural, low-poly models of the 7 bible robots, shaped after the real machines (public photos / spec pages):
//  Optimus        Tesla Optimus Gen 2: white body panels over a black chassis, small head with a glossy black face plate,
//                 slim build, black actuator joints at elbows / knees / shoulders.
//  Unitree        Unitree H1: dark charcoal, slim torso, long thin legs, round exposed motor housings at hips / knees /
//                 shoulders, head pod with a black face, a depth camera below it and the 3D lidar puck on top.
//  Figure 03      soft knitted textile cover in light cream-grey, rounded padded forms, helmet head with a glossy black
//                 face plate and the small side screens.
//  Figure 02      matte black / dark grey body, narrow shoulders, grey upper panels, fabric neck, silver-grey head with a
//                 big glossy black face plate.
//  Big Figure Two the bible's oversized Figure 02 (same kind, height 3.4 vs 2.05, bulk 1.35 vs 0.6): Figure 02 x 1.35.
//  Atlas HD       hydraulic Atlas: bulky, blue-grey torso shell, hydraulic power pack on the back, thick dark limbs with
//                 silver hydraulic rams, small sensor head (stereo cameras + lidar), blue status LEDs.
//  Atlas electric 2024 electric Atlas: sleek light grey, slim limbs on big round rotary joints (they spin 360), a rotary
//                 waist, and the round head: a perfectly round dark screen ringed by a warm ring light.
// Everything is built from Unity primitives + Geo blocks; the static part merges with the car, the head stays separate.
public static class RobotModel
{
    public static float Size(int r)
    {
        switch (r)
        {
            case 1: return 0.9f;    // Unitree (thin)
            case 2: return 0.97f;   // Figure 03
            case 3: return 0.95f;   // Figure 02
            case 4: return 1.35f;   // Big Figure Two
            case 5: return 0.97f;   // Atlas HD (short but wide)
            default: return 1f;     // Optimus, Atlas electric
        }
    }

    static float HeadY(int r) { return r == 5 ? 0.71f : r == 6 ? 0.78f : 0.76f; }
    static float ShoulderW(int r) { switch (r) { case 1: return 0.4f; case 3: case 4: return 0.4f; case 5: return 0.56f; case 2: return 0.45f; default: return 0.46f; } }

    // ---------- materials ----------
    static Material knit;
    static Material Knit()
    {
        if (knit != null) return knit;
        const int n = 32;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, true);
        t.wrapMode = TextureWrapMode.Repeat;
        t.filterMode = FilterMode.Bilinear;
        Color a = Mats.Hex("#E1DCD2"), b = Mats.Hex("#C7C1B6");
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float rib = Mathf.Abs((x % 4) - 1.5f) / 1.5f;              // vertical knit ribs
                float row = ((y / 2) % 2 == 0) ? 0.04f : 0f;                // fine courses
                Color c = Color.Lerp(a, b, rib * 0.75f) * (1f - row);
                c.a = 1f;
                px[y * n + x] = c;
            }
        t.SetPixels(px);
        t.Apply(true);
        knit = Mats.Tex(t, 0.05f);
        return knit;
    }

    static Material GlossBlack { get { return Mats.Paint(new Color(0.015f, 0.015f, 0.02f), 0.95f); } }

    // ---------- shape helpers (robot-local space) ----------
    static GameObject Cap(Transform p, Vector3 a, Vector3 b, float th, Material m)
    {
        Vector3 d = b - a;
        float len = Mathf.Max(0.001f, d.magnitude);
        var g = Mats.Prim(PrimitiveType.Capsule, p, (a + b) * 0.5f, new Vector3(th, Mathf.Max(th * 0.5f, len * 0.5f + th * 0.2f), th), m);
        g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d / len);
        return g;
    }

    static GameObject Disc(Transform p, Vector3 c, float dia, float thick, Material m, Vector3 axis)
    {
        var g = Mats.Prim(PrimitiveType.Cylinder, p, c, new Vector3(dia, thick * 0.5f, dia), m);
        g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, axis);
        return g;
    }

    static GameObject Ball(Transform p, Vector3 c, Vector3 s, Material m) { return Mats.Prim(PrimitiveType.Sphere, p, c, s, m); }
    static GameObject Box(Transform p, Vector3 c, Vector3 s, Material m) { return Mats.Prim(PrimitiveType.Cube, p, c, s, m); }
    static readonly Vector3 X = Vector3.right, Y = Vector3.up, Z = Vector3.forward;

    // seated skeleton: s = -1 left, +1 right
    static Vector3 Sh(int s, float sw) { return new Vector3(s * sw * 0.5f, 0.5f, 0f); }
    static Vector3 El(int s, float sw, bool drive) { return drive ? new Vector3(s * (sw * 0.5f + 0.05f), 0.3f, 0.2f) : new Vector3(s * (sw * 0.5f + 0.04f), 0.27f, 0.07f); }
    static Vector3 Ha(int s, float sw, bool drive) { return drive ? new Vector3(s * 0.17f, 0.44f, 0.46f) : new Vector3(s * (sw * 0.5f - 0.03f), 0.17f, 0.32f); }
    static Vector3 Hip(int s, float hw) { return new Vector3(s * hw, 0.08f, 0.02f); }
    static Vector3 Knee(int s, float hw) { return new Vector3(s * (hw + 0.02f), 0.16f, 0.45f); }
    static Vector3 Ank(int s, float hw) { return new Vector3(s * (hw + 0.02f), -0.24f, 0.56f); }

    // Builds robot r seated at `seat` (in the local space of `stat`, which must share its space with `headParent`).
    // drive = hands on a steering wheel (in the car); otherwise hands rest on the thighs (portraits).
    // Returns the head transform (separate so it can look into corners / idle in the showroom).
    public static Transform Build(Transform stat, Transform headParent, Vector3 seat, int r, bool drive)
    {
        float size = Size(r);
        var rb = new GameObject("Robot").transform;
        rb.SetParent(stat, false);
        rb.localPosition = seat;
        rb.localScale = Vector3.one * size;
        var head = new GameObject("Head").transform;
        head.SetParent(headParent, false);
        head.localPosition = seat + Vector3.up * HeadY(r) * size;
        head.localScale = Vector3.one * size;
        float sw = ShoulderW(r);
        switch (r)
        {
            case 0: Optimus(rb, head, sw, drive); break;
            case 1: Unitree(rb, head, sw, drive); break;
            case 2: Figure03(rb, head, sw, drive); break;
            case 3: case 4: Figure02(rb, head, sw, drive); break;
            case 5: AtlasHD(rb, head, sw, drive); break;
            default: AtlasElectric(rb, head, sw, drive); break;
        }
        if (drive)
        {
            Material k = Mats.Lit(new Color(0.06f, 0.06f, 0.07f));
            Vector3 wc = new Vector3(0f, 0.4f, 0.52f);
            Mats.Prim(PrimitiveType.Cylinder, rb, wc, new Vector3(0.36f, 0.015f, 0.36f), new Vector3(70f, 0f, 0f), k);
            Mats.Prim(PrimitiveType.Cylinder, rb, wc + new Vector3(0f, -0.1f, 0.12f), new Vector3(0.05f, 0.12f, 0.05f), new Vector3(70f, 0f, 0f), k);
        }
        return head;
    }

    // ---------- Tesla Optimus (Gen 2) ----------
    static void Optimus(Transform rb, Transform head, float sw, bool drive)
    {
        Material W = Mats.Paint(Mats.Hex("#EDEDEB"), 0.8f), K = Mats.Paint(Mats.Hex("#151517"), 0.55f), F = GlossBlack;
        Geo.Block(rb, W, 0f, -0.1f, 0.1f, 0.28f, 0.3f, 0.02f, 0.15f, 0.26f, 0.28f, 0.03f, 0.15f);           // pelvis
        Geo.Block(rb, K, 0f, -0.08f, 0.08f, 0.2f, 0.24f, 0.14f, 0.3f, 0.19f, 0.22f, 0.14f, 0.3f);            // black waist
        Geo.Block(rb, W, 0f, -0.12f, 0.12f, 0.28f, 0.44f, 0.28f, 0.58f, 0.26f, 0.38f, 0.3f, 0.56f);          // chest shell
        for (int s = -1; s <= 1; s += 2) Box(rb, new Vector3(s * 0.165f, 0.4f, -0.01f), new Vector3(0.05f, 0.18f, 0.2f), K);   // black chassis sides
        Box(rb, new Vector3(0f, 0.575f, 0f), new Vector3(0.22f, 0.04f, 0.15f), K);                           // collar
        Mats.Prim(PrimitiveType.Cylinder, rb, new Vector3(0f, 0.62f, 0f), new Vector3(0.085f, 0.045f, 0.085f), K);
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 sh = Sh(s, sw), el = El(s, sw, drive), ha = Ha(s, sw, drive);
            Disc(rb, sh, 0.15f, 0.08f, K, X);
            Ball(rb, sh + new Vector3(s * 0.02f, 0.02f, 0f), new Vector3(0.15f, 0.14f, 0.16f), W);            // shoulder cap
            Cap(rb, sh, el, 0.1f, W);
            Disc(rb, el, 0.11f, 0.1f, K, X);                                                                   // elbow actuator
            Cap(rb, el, Vector3.Lerp(el, ha, 0.82f), 0.09f, W);
            Ball(rb, ha, new Vector3(0.07f, 0.1f, 0.08f), K);                                                  // hand
            Vector3 hp = Hip(s, 0.1f), kn = Knee(s, 0.1f), an = Ank(s, 0.1f);
            Cap(rb, hp, kn, 0.15f, W);
            Disc(rb, kn, 0.13f, 0.13f, K, X);                                                                  // knee actuator
            Cap(rb, kn, an, 0.12f, W);
            Box(rb, an + new Vector3(0f, -0.03f, 0.06f), new Vector3(0.1f, 0.06f, 0.2f), K);
        }
        // small head: white shell, full glossy black face plate
        Ball(head, new Vector3(0f, 0f, -0.012f), new Vector3(0.2f, 0.24f, 0.22f), W);
        Ball(head, new Vector3(0f, -0.005f, 0.035f), new Vector3(0.185f, 0.2f, 0.17f), F);
    }

    // ---------- Unitree H1 ----------
    static void Unitree(Transform rb, Transform head, float sw, bool drive)
    {
        Material D = Mats.Paint(Mats.Hex("#2B2E34"), 0.45f), K = Mats.Lit(Mats.Hex("#0F1012")), M = Mats.Steel(Mats.Hex("#6E737C")), F = GlossBlack;
        Box(rb, new Vector3(0f, 0.09f, 0f), new Vector3(0.24f, 0.12f, 0.16f), K);                             // pelvis
        Geo.Block(rb, D, 0f, -0.1f, 0.1f, 0.22f, 0.34f, 0.16f, 0.58f, 0.2f, 0.3f, 0.18f, 0.56f);             // slim torso
        Box(rb, new Vector3(0f, 0.43f, 0.095f), new Vector3(0.18f, 0.16f, 0.02f), K);                        // chest panel
        Mats.Prim(PrimitiveType.Cylinder, rb, new Vector3(0f, 0.61f, 0f), new Vector3(0.07f, 0.04f, 0.07f), K);
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 sh = Sh(s, sw), el = El(s, sw, drive), ha = Ha(s, sw, drive);
            Disc(rb, sh, 0.14f, 0.1f, D, X); Disc(rb, sh, 0.08f, 0.104f, M, X);                              // shoulder motor
            Cap(rb, sh, el, 0.075f, D);
            Disc(rb, el, 0.1f, 0.08f, K, X); Disc(rb, el, 0.05f, 0.084f, M, X);
            Cap(rb, el, Vector3.Lerp(el, ha, 0.85f), 0.065f, D);
            Ball(rb, ha, new Vector3(0.06f, 0.08f, 0.07f), K);
            Vector3 hp = Hip(s, 0.1f), kn = Knee(s, 0.1f), an = Ank(s, 0.1f);
            Disc(rb, hp + new Vector3(s * 0.03f, 0f, 0f), 0.17f, 0.1f, D, X); Disc(rb, hp + new Vector3(s * 0.03f, 0f, 0f), 0.09f, 0.104f, M, X);   // hip motor
            Cap(rb, hp, kn, 0.1f, D);                                                                            // long thin thigh
            Disc(rb, kn, 0.14f, 0.09f, D, X); Disc(rb, kn, 0.07f, 0.094f, M, X);                              // knee motor
            Cap(rb, kn, an, 0.07f, K);                                                                           // thin shin
            Box(rb, an + new Vector3(0f, -0.03f, 0.05f), new Vector3(0.08f, 0.05f, 0.18f), K);
        }
        // head pod + black face, depth camera, 3D lidar puck on top
        Ball(head, new Vector3(0f, -0.01f, 0f), new Vector3(0.17f, 0.17f, 0.2f), D);
        Ball(head, new Vector3(0f, -0.015f, 0.04f), new Vector3(0.15f, 0.13f, 0.14f), F);
        Box(head, new Vector3(0f, -0.06f, 0.092f), new Vector3(0.09f, 0.025f, 0.02f), K);
        for (int s = -1; s <= 1; s += 2) Disc(head, new Vector3(s * 0.025f, -0.06f, 0.103f), 0.016f, 0.004f, M, Z);
        Disc(head, new Vector3(0f, 0.095f, 0.005f), 0.13f, 0.08f, D, Y);
        Disc(head, new Vector3(0f, 0.095f, 0.005f), 0.134f, 0.03f, F, Y);                                       // lidar window band
        Ball(head, new Vector3(0f, 0.135f, 0.005f), new Vector3(0.12f, 0.04f, 0.12f), D);
    }

    // ---------- Figure 03 ----------
    static void Figure03(Transform rb, Transform head, float sw, bool drive)
    {
        Material C = Knit(), Cd = Mats.Lit(Mats.Hex("#9A958D")), Hs = Mats.Paint(Mats.Hex("#E3DFD7"), 0.55f), F = GlossBlack;
        Material Scr = Mats.Unlit(Mats.Hex("#CFEFFF"));
        Ball(rb, new Vector3(0f, 0.09f, 0f), new Vector3(0.3f, 0.16f, 0.2f), C);                              // pelvis
        Cap(rb, new Vector3(0f, 0.12f, 0f), new Vector3(0f, 0.32f, 0f), 0.22f, C);                           // waist
        Ball(rb, new Vector3(0f, 0.43f, 0f), new Vector3(0.4f, 0.34f, 0.25f), C);                              // padded chest
        Mats.Prim(PrimitiveType.Cylinder, rb, new Vector3(0f, 0.61f, 0f), new Vector3(0.1f, 0.05f, 0.1f), Cd);
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 sh = Sh(s, sw), el = El(s, sw, drive), ha = Ha(s, sw, drive);
            Ball(rb, sh, new Vector3(0.16f, 0.16f, 0.16f), C);
            Cap(rb, sh, el, 0.11f, C);
            Ball(rb, el, Vector3.one * 0.1f, Cd);
            Cap(rb, el, Vector3.Lerp(el, ha, 0.82f), 0.1f, C);
            Ball(rb, ha, new Vector3(0.075f, 0.1f, 0.08f), Cd);
            Vector3 hp = Hip(s, 0.1f), kn = Knee(s, 0.1f), an = Ank(s, 0.1f);
            Cap(rb, hp, kn, 0.16f, C);
            Ball(rb, kn, Vector3.one * 0.13f, Cd);
            Cap(rb, kn, an, 0.13f, C);
            Ball(rb, an + new Vector3(0f, -0.03f, 0.06f), new Vector3(0.1f, 0.07f, 0.2f), Cd);
        }
        // helmet head, glossy black face plate, small side screens
        Ball(head, new Vector3(0f, 0f, -0.01f), new Vector3(0.22f, 0.25f, 0.24f), Hs);
        Ball(head, new Vector3(0f, -0.01f, 0.042f), new Vector3(0.19f, 0.2f, 0.16f), F);
        for (int s = -1; s <= 1; s += 2) Box(head, new Vector3(s * 0.108f, 0f, -0.01f), new Vector3(0.012f, 0.06f, 0.08f), Scr);
    }

    // ---------- Figure 02 (and Big Figure Two = the same, scaled) ----------
    static void Figure02(Transform rb, Transform head, float sw, bool drive)
    {
        Material G = Mats.Paint(Mats.Hex("#2F3134"), 0.3f), P = Mats.Paint(Mats.Hex("#4E5155"), 0.35f), K = Mats.Lit(Mats.Hex("#121315"));
        Material S = Mats.Steel(Mats.Hex("#A6AAB0")), F = GlossBlack, Fab = Mats.Lit(Mats.Hex("#1B1C1E"));
        Geo.Block(rb, K, 0f, -0.1f, 0.1f, 0.28f, 0.28f, 0.02f, 0.15f, 0.26f, 0.26f, 0.03f, 0.15f);           // pelvis
        Geo.Block(rb, G, 0f, -0.08f, 0.08f, 0.2f, 0.22f, 0.14f, 0.3f, 0.19f, 0.21f, 0.14f, 0.3f);            // abdomen
        Geo.Block(rb, P, 0f, -0.11f, 0.11f, 0.26f, 0.38f, 0.28f, 0.57f, 0.24f, 0.34f, 0.3f, 0.55f);          // narrow chest
        Box(rb, new Vector3(0f, 0.38f, -0.12f), new Vector3(0.26f, 0.24f, 0.06f), G);                        // battery in the back
        Mats.Prim(PrimitiveType.Cylinder, rb, new Vector3(0f, 0.615f, 0f), new Vector3(0.1f, 0.05f, 0.1f), Fab);   // fabric neck
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 sh = Sh(s, sw), el = El(s, sw, drive), ha = Ha(s, sw, drive);
            Ball(rb, sh, new Vector3(0.13f, 0.13f, 0.14f), G);
            Cap(rb, sh, el, 0.09f, G);
            Ball(rb, el, Vector3.one * 0.085f, Fab);                                                          // soft stop
            Cap(rb, el, Vector3.Lerp(el, ha, 0.82f), 0.08f, P);
            Box(rb, ha, new Vector3(0.065f, 0.1f, 0.09f), K);
            Vector3 hp = Hip(s, 0.1f), kn = Knee(s, 0.1f), an = Ank(s, 0.1f);
            Cap(rb, hp, kn, 0.13f, G);
            Ball(rb, kn, Vector3.one * 0.1f, Fab);
            Cap(rb, kn, an, 0.1f, G);
            Box(rb, an + new Vector3(0f, -0.03f, 0.06f), new Vector3(0.1f, 0.06f, 0.2f), K);
        }
        // silver-grey head with a big glossy black face plate
        Ball(head, new Vector3(0f, 0f, -0.01f), new Vector3(0.2f, 0.24f, 0.23f), S);
        Ball(head, new Vector3(0f, -0.005f, 0.04f), new Vector3(0.19f, 0.21f, 0.16f), F);
    }

    // ---------- Atlas HD (hydraulic) ----------
    static void AtlasHD(Transform rb, Transform head, float sw, bool drive)
    {
        Material T = Mats.Paint(Mats.Hex("#5D7088"), 0.5f), H = Mats.Paint(Mats.Hex("#2A2C30"), 0.4f), R = Mats.Steel(Mats.Hex("#9A9EA4"));
        Material K = Mats.Lit(Mats.Hex("#111214")), Bl = Mats.Unlit(Mats.Hex("#3FA9FF")), F = GlossBlack, Hd = Mats.Paint(Mats.Hex("#3A3D42"), 0.45f);
        Box(rb, new Vector3(0f, 0.09f, 0f), new Vector3(0.32f, 0.14f, 0.2f), H);                              // pelvis
        Box(rb, new Vector3(0f, 0.22f, 0f), new Vector3(0.24f, 0.12f, 0.16f), K);                             // waist
        Geo.Block(rb, T, 0f, -0.14f, 0.15f, 0.36f, 0.5f, 0.26f, 0.6f, 0.34f, 0.46f, 0.28f, 0.58f);           // big torso shell
        Box(rb, new Vector3(0f, 0.41f, -0.19f), new Vector3(0.36f, 0.3f, 0.1f), H);                          // hydraulic power pack
        for (int s = -1; s <= 1; s += 2) Box(rb, new Vector3(s * 0.15f, 0.5f, 0.152f), new Vector3(0.05f, 0.02f, 0.01f), Bl);   // blue LEDs
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 sh = Sh(s, sw), el = El(s, sw, drive), ha = Ha(s, sw, drive);
            Vector3 o = new Vector3(s * 0.055f, 0f, 0f);
            Box(rb, sh + new Vector3(0f, 0.01f, 0f), new Vector3(0.16f, 0.15f, 0.18f), H);
            Cap(rb, sh, el, 0.13f, H);
            Cap(rb, sh + o, el + o, 0.035f, R);                                                                   // hydraulic ram
            Disc(rb, el, 0.13f, 0.12f, K, X);
            Cap(rb, el, Vector3.Lerp(el, ha, 0.82f), 0.12f, H);
            Cap(rb, el + o * 0.8f, Vector3.Lerp(el, ha, 0.7f) + o * 0.8f, 0.03f, R);
            Box(rb, ha, new Vector3(0.09f, 0.11f, 0.11f), K);
            Vector3 hp = Hip(s, 0.12f), kn = Knee(s, 0.12f), an = Ank(s, 0.12f);
            Cap(rb, hp, kn, 0.19f, H);
            Cap(rb, hp + o * 1.4f, kn + o * 1.4f, 0.05f, R);
            Disc(rb, kn, 0.17f, 0.15f, K, X);
            Cap(rb, kn, an, 0.15f, H);
            Cap(rb, kn + o * 1.2f, an + o * 1.2f, 0.04f, R);
            Box(rb, an + new Vector3(0f, -0.03f, 0.06f), new Vector3(0.13f, 0.07f, 0.24f), K);
        }
        // compact sensor head: dark visor, stereo cameras, lidar
        Box(head, Vector3.zero, new Vector3(0.17f, 0.13f, 0.18f), Hd);
        Ball(head, new Vector3(0f, 0.04f, -0.01f), new Vector3(0.17f, 0.11f, 0.18f), Hd);
        Box(head, new Vector3(0f, -0.005f, 0.09f), new Vector3(0.15f, 0.07f, 0.015f), F);
        for (int s = -1; s <= 1; s += 2) Disc(head, new Vector3(s * 0.04f, -0.005f, 0.099f), 0.03f, 0.006f, R, Z);
        Disc(head, new Vector3(0f, 0.105f, 0f), 0.08f, 0.07f, K, Y);
        Disc(head, new Vector3(0f, 0.105f, 0f), 0.083f, 0.025f, R, Y);
    }

    // ---------- Atlas electric (2024) ----------
    static void AtlasElectric(Transform rb, Transform head, float sw, bool drive)
    {
        Material B = Mats.Paint(Mats.Hex("#C2C4C7"), 0.65f), J = Mats.Paint(Mats.Hex("#393B3F"), 0.5f), F = GlossBlack, L = Mats.Unlit(Mats.Hex("#FFE7B3"));
        Geo.Block(rb, B, 0f, -0.1f, 0.1f, 0.28f, 0.28f, 0.02f, 0.15f, 0.26f, 0.26f, 0.03f, 0.15f);           // pelvis
        Disc(rb, new Vector3(0f, 0.2f, 0f), 0.2f, 0.09f, J, Y);                                                 // rotary waist
        Geo.Block(rb, B, 0f, -0.11f, 0.11f, 0.24f, 0.42f, 0.24f, 0.57f, 0.22f, 0.38f, 0.26f, 0.55f);         // sleek torso
        Mats.Prim(PrimitiveType.Cylinder, rb, new Vector3(0f, 0.61f, 0f), new Vector3(0.06f, 0.045f, 0.06f), J);
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 sh = Sh(s, sw), el = El(s, sw, drive), ha = Ha(s, sw, drive);
            Disc(rb, sh, 0.15f, 0.08f, J, X);                                                                   // 360 shoulder
            Ball(rb, sh + new Vector3(s * 0.02f, 0f, 0f), Vector3.one * 0.12f, B);
            Cap(rb, sh, el, 0.085f, B);
            Disc(rb, el, 0.12f, 0.1f, J, X);
            Cap(rb, el, Vector3.Lerp(el, ha, 0.84f), 0.08f, B);
            Box(rb, ha, new Vector3(0.07f, 0.09f, 0.09f), J);
            Vector3 hp = Hip(s, 0.1f), kn = Knee(s, 0.1f), an = Ank(s, 0.1f);
            Disc(rb, hp + new Vector3(s * 0.02f, 0f, 0f), 0.15f, 0.1f, J, X);                                 // 360 hip
            Cap(rb, hp, kn, 0.12f, B);
            Disc(rb, kn, 0.13f, 0.12f, J, X);
            Cap(rb, kn, an, 0.1f, B);
            Box(rb, an + new Vector3(0f, -0.03f, 0.06f), new Vector3(0.1f, 0.06f, 0.2f), J);
        }
        // round head: grey bucket, warm ring light around a perfectly round dark screen
        Disc(head, new Vector3(0f, 0f, -0.01f), 0.3f, 0.12f, B, Z);
        Disc(head, new Vector3(0f, 0f, 0.052f), 0.27f, 0.006f, L, Z);
        Disc(head, new Vector3(0f, 0f, 0.056f), 0.235f, 0.006f, F, Z);
    }
}
