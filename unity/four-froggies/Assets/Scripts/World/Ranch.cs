using UnityEngine;
using UnityEngine.UI;

// Builds the ranch at runtime: James's house (roof helipad + drone pad, ramp up), the 6-bay garage,
// porch, backyard pool, pond with dock, rally oval with jumps and flags, trees, loose props,
// and the stubbed Starship pad / submarine. Static parts are merged per material and area.
public static class Ranch
{
    static Terrain terrain;
    static Transform root;

    public static float GY(float x, float z)
    {
        if (terrain == null) return 0f;
        return terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
    }

    static GameObject B(Vector3 pos, Vector3 size, Color c, bool col = true, Vector3 euler = default(Vector3))
    {
        return Mats.Prim(PrimitiveType.Cube, root, pos, size, euler, Mats.Lit(c), col);
    }

    static GameObject BM(Vector3 pos, Vector3 size, Material m, bool col = true, Vector3 euler = default(Vector3))
    {
        return Mats.Prim(PrimitiveType.Cube, root, pos, size, euler, m, col);
    }

    static GameObject Cyl(Vector3 pos, Vector3 scale, Color c, bool col = false, Vector3 euler = default(Vector3))
    {
        return Mats.Prim(PrimitiveType.Cylinder, root, pos, scale, euler, Mats.Lit(c), col);
    }

    static GameObject Ball(Vector3 pos, Vector3 scale, Color c)
    {
        return Mats.Prim(PrimitiveType.Sphere, root, pos, scale, Mats.Lit(c), false);
    }

    static readonly Color Cream = new Color(0.9f, 0.86f, 0.77f);
    static readonly Color Trim = new Color(0.97f, 0.97f, 0.95f);
    static readonly Color RoofC = new Color(0.24f, 0.25f, 0.28f);
    static readonly Color Stone = new Color(0.55f, 0.52f, 0.48f);
    static readonly Color Wood = new Color(0.55f, 0.38f, 0.22f);
    static readonly Color WindowC = new Color(0.16f, 0.24f, 0.32f);
    static readonly Color Concrete = new Color(0.62f, 0.62f, 0.6f);

    public static Vector3 FrogSpawn(int i) { return new Vector3(-12f + i * 3.2f, GY(-12f + i * 3.2f, 38f) + 0.3f, 38f); }

    public static void Build(Terrain t)
    {
        terrain = t;
        root = new GameObject("RanchStatic").transform;
        House();
        Garage();
        Pool();
        Pond();
        Track();
        Starship();
        Trees();
        MeshMerge.Merge(root, true);
        Props();
    }

    // ---------------- house ----------------
    static void Window(Vector3 p, bool alongX)
    {
        Vector3 s = alongX ? new Vector3(2.2f, 1.7f, 0.12f) : new Vector3(0.12f, 1.7f, 2.2f);
        BM(p, s, Mats.Glass, false);
        Vector3 f = alongX ? new Vector3(2.5f, 0.12f, 0.16f) : new Vector3(0.16f, 0.12f, 2.5f);
        B(p + Vector3.up * 0.9f, f, Trim, false);
        B(p - Vector3.up * 0.9f, f, Trim, false);
    }

    static void House()
    {
        Vector2 c = Layout.HouseC, s = Layout.HouseSize;
        float H = Layout.HouseH;
        float x0 = c.x - s.x * 0.5f, x1 = c.x + s.x * 0.5f, z0 = c.y - s.y * 0.5f, z1 = c.y + s.y * 0.5f;
        // shell (solid, walkable roof)
        B(new Vector3(c.x, H * 0.25f, c.y), new Vector3(s.x, H * 0.5f, s.y), Cream);
        B(new Vector3(c.x, H * 0.75f, c.y), new Vector3(s.x - 0.02f, H * 0.5f, s.y - 0.02f), new Color(0.78f, 0.82f, 0.86f));
        B(new Vector3(c.x, 0.4f, c.y), new Vector3(s.x + 0.2f, 0.8f, s.y + 0.2f), Stone, false);
        B(new Vector3(c.x, H * 0.5f, c.y), new Vector3(s.x + 0.3f, 0.3f, s.y + 0.3f), Trim, false);
        B(new Vector3(c.x, H - 0.1f, c.y), new Vector3(s.x + 0.4f, 0.2f, s.y + 0.4f), RoofC, false);
        // parapet (gap on the east side where the ramp lands)
        float ph = 0.8f, py = H + ph * 0.5f;
        B(new Vector3(c.x, py, z0), new Vector3(s.x, ph, 0.4f), Trim);
        B(new Vector3(c.x, py, z1), new Vector3(s.x, ph, 0.4f), Trim);
        B(new Vector3(x0, py, c.y), new Vector3(0.4f, ph, s.y), Trim);
        float gapA = 4.5f, gapB = 10.5f; // z range of the gap
        B(new Vector3(x1, py, (z0 + gapA) * 0.5f), new Vector3(0.4f, ph, gapA - z0), Trim);
        if (z1 > gapB) B(new Vector3(x1, py, (gapB + z1) * 0.5f), new Vector3(0.4f, ph, z1 - gapB), Trim);
        // windows on all four faces, two floors
        for (float x = x0 + 4f; x < x1 - 2f; x += 5f)
        {
            bool door = Mathf.Abs(x - c.x) < 3f;
            foreach (float y in new[] { 2.4f, 6.8f })
            {
                if (!(door && y < 4f)) Window(new Vector3(x, y, z1 + 0.06f), true);
                Window(new Vector3(x, y, z0 - 0.06f), true);
            }
        }
        for (float z = z0 + 4f; z < z1 - 2f; z += 5f)
            foreach (float y in new[] { 2.4f, 6.8f })
            {
                Window(new Vector3(x0 - 0.06f, y, z), false);
                if (z < 0f) Window(new Vector3(x1 + 0.06f, y, z), false);
            }
        // front door
        B(new Vector3(c.x, 1.6f, z1 + 0.08f), new Vector3(2.8f, 3.2f, 0.12f), new Color(0.35f, 0.22f, 0.12f), false);
        B(new Vector3(c.x, 3.35f, z1 + 0.1f), new Vector3(3.2f, 0.25f, 0.16f), Trim, false);
        // porch
        float pz0 = z1, pz1 = z1 + 7f, px0 = -53.6f, px1 = -30.4f;
        B(new Vector3((px0 + px1) * 0.5f, 0.2f, (pz0 + pz1) * 0.5f), new Vector3(px1 - px0, 0.4f, pz1 - pz0), Wood);
        B(new Vector3((px0 + px1) * 0.5f, 3.75f, (pz0 + pz1) * 0.5f), new Vector3(px1 - px0 + 0.6f, 0.25f, pz1 - pz0 + 0.6f), RoofC, false);
        for (int i = 0; i < 4; i++)
        {
            float x = Mathf.Lerp(px0 + 0.4f, px1 - 0.4f, i / 3f);
            B(new Vector3(x, 2f, pz1 - 0.3f), new Vector3(0.3f, 3.6f, 0.3f), Trim);
        }
        B(new Vector3(px0 + 4f, 1.0f, pz1 - 0.3f), new Vector3(7f, 0.1f, 0.1f), Trim, false);
        B(new Vector3(px1 - 4f, 1.0f, pz1 - 0.3f), new Vector3(7f, 0.1f, 0.1f), Trim, false);
        // flowers by the porch and the west yard
        var r = new System.Random(5);
        Color[] petals = { new Color(1f, 0.4f, 0.55f), new Color(1f, 0.85f, 0.2f), new Color(0.65f, 0.45f, 1f), new Color(1f, 1f, 1f) };
        for (int i = 0; i < 40; i++)
        {
            float x = i < 24 ? Mathf.Lerp(px0, px1, (float)r.NextDouble()) : x0 - 2f - (float)r.NextDouble() * 3f;
            float z = i < 24 ? pz1 + 0.8f + (float)r.NextDouble() * 1.4f : Mathf.Lerp(z0, z1, (float)r.NextDouble());
            Ball(new Vector3(x, 0.25f, z), new Vector3(0.5f, 0.35f, 0.5f), new Color(0.2f, 0.5f, 0.18f));
            Ball(new Vector3(x, 0.45f, z), Vector3.one * 0.22f, petals[i % petals.Length]);
        }
        // ramp from the backyard up the east wall to the roof
        float rx = x1 + 2f, rz0 = z0 - 3f, rz1 = gapA + 1f;
        float len = Mathf.Sqrt((rz1 - rz0) * (rz1 - rz0) + H * H), ang = Mathf.Atan2(H, rz1 - rz0) * Mathf.Rad2Deg;
        B(new Vector3(rx, H * 0.5f - 0.2f, (rz0 + rz1) * 0.5f), new Vector3(4f, 0.4f, len), Concrete, true, new Vector3(-ang, 0f, 0f));
        B(new Vector3(rx + 2.05f, H * 0.5f + 0.75f, (rz0 + rz1) * 0.5f), new Vector3(0.12f, 0.12f, len), Trim, true, new Vector3(-ang, 0f, 0f));
        B(new Vector3(rx, H - 0.2f, (rz1 + gapB) * 0.5f), new Vector3(4f, 0.4f, gapB - rz1), Concrete);   // landing
        B(new Vector3(rx + 2.05f, H + 0.5f, (rz1 + gapB) * 0.5f), new Vector3(0.12f, 1f, gapB - rz1), Trim);
        for (float z = rz0 + 3f; z < rz1; z += 6f)
        {
            float y = (z - rz0) / (rz1 - rz0) * H;
            B(new Vector3(rx, y * 0.5f, z), new Vector3(0.5f, y, 0.5f), Stone, false);
        }
        // roof pads
        RoofPad(new Vector3(-52f, H, -6f), 6f, new Color(1f, 0.82f, 0.1f), true);
        RoofPad(new Vector3(-30f, H, -6f), 4.5f, new Color(0.2f, 0.9f, 0.45f), false);
        B(new Vector3(c.x + 8f, H + 1.2f, c.y + 10f), new Vector3(2f, 2.4f, 2f), Stone);    // chimney
    }

    static void RoofPad(Vector3 p, float r, Color ring, bool heli)
    {
        Cyl(p + Vector3.up * 0.03f, new Vector3(r * 2f, 0.03f, r * 2f), ring);
        Cyl(p + Vector3.up * 0.05f, new Vector3(r * 1.8f, 0.03f, r * 1.8f), new Color(0.18f, 0.19f, 0.21f));
        Color w = Color.white;
        if (heli)
        {
            B(p + new Vector3(-1.2f, 0.09f, 0f), new Vector3(0.5f, 0.02f, 3.4f), w, false);
            B(p + new Vector3(1.2f, 0.09f, 0f), new Vector3(0.5f, 0.02f, 3.4f), w, false);
            B(p + new Vector3(0f, 0.09f, 0f), new Vector3(2.0f, 0.02f, 0.5f), w, false);
        }
        else
        {
            for (int k = 0; k < 4; k++)
                B(p + Quaternion.Euler(0f, 45f + k * 90f, 0f) * new Vector3(0f, 0.09f, 1.6f), new Vector3(0.4f, 0.02f, 1.4f), w, false, new Vector3(0f, 45f + k * 90f, 0f));
        }
    }

    // ---------------- garage ----------------
    static void Garage()
    {
        Vector2 c = Layout.GarageC, s = Layout.GarageSize;
        float H = Layout.GarageH;
        float x0 = c.x - s.x * 0.5f, x1 = c.x + s.x * 0.5f, z0 = c.y - s.y * 0.5f, z1 = c.y + s.y * 0.5f;
        B(new Vector3(c.x, H * 0.5f, z0 + 0.25f), new Vector3(s.x, H, 0.5f), Cream);
        B(new Vector3(x0 + 0.25f, H * 0.5f, c.y), new Vector3(0.5f, H, s.y), Cream);
        B(new Vector3(x1 - 0.25f, H * 0.5f, c.y), new Vector3(0.5f, H, s.y), Cream);
        B(new Vector3(c.x, H + 0.2f, c.y), new Vector3(s.x + 0.6f, 0.4f, s.y + 0.6f), RoofC);
        B(new Vector3(c.x, H - 0.6f, z1 - 0.3f), new Vector3(s.x, 1.2f, 0.6f), Cream);   // header over the bays
        B(new Vector3(c.x, H - 1.25f, z1 - 0.2f), new Vector3(s.x, 0.1f, 0.7f), Trim, false);
        B(new Vector3(c.x, 0.03f, c.y), new Vector3(s.x - 1f, 0.06f, s.y - 1f), Concrete, false);
        float bay = s.x / Layout.Bays;
        for (int i = 0; i <= Layout.Bays; i++)
        {
            float x = x0 + i * bay;
            B(new Vector3(Mathf.Clamp(x, x0 + 0.35f, x1 - 0.35f), (H - 1.2f) * 0.5f, z1 - 0.3f), new Vector3(0.7f, H - 1.2f, 0.6f), Trim);
            if (i > 0 && i < Layout.Bays) B(new Vector3(x, 0.07f, c.y + 1f), new Vector3(0.15f, 0.02f, s.y - 4f), Color.white, false);
        }
        for (int i = 0; i < Layout.Bays; i++)
        {
            float x = Layout.BayX(i);
            BM(new Vector3(x, H - 0.25f, c.y), new Vector3(bay * 0.5f, 0.06f, 0.4f), Mats.Unlit(new Color(1f, 0.97f, 0.85f)), false);
            for (int k = 0; k < 4; k++)   // rolled-up door slats
                B(new Vector3(x, H - 1.45f - k * 0.12f, z1 - 0.65f), new Vector3(bay - 1.2f, 0.1f, 0.1f), new Color(0.82f, 0.82f, 0.84f), false);
        }
        // tool benches on the back wall
        for (int i = 0; i < 3; i++) B(new Vector3(x0 + 6f + i * 18f, 0.5f, z0 + 1f), new Vector3(3f, 1f, 1f), Wood);
        Sign(new Vector3(c.x, H - 0.6f, z1 + 0.05f), 0f, "JAMES'S GARAGE", new Color(0.12f, 0.35f, 0.15f), 14f, 1.1f);
    }

    // ---------------- pool ----------------
    static void Pool()
    {
        Vector2 c = Layout.PoolC, s = Layout.PoolSize;
        float y = GY(c.x, c.y);
        Color coping = new Color(0.92f, 0.9f, 0.85f);
        B(new Vector3(c.x, y + 0.15f, c.y + s.y * 0.5f + 0.5f), new Vector3(s.x + 2f, 0.3f, 1f), coping);
        B(new Vector3(c.x, y + 0.15f, c.y - s.y * 0.5f - 0.5f), new Vector3(s.x + 2f, 0.3f, 1f), coping);
        B(new Vector3(c.x - s.x * 0.5f - 0.5f, y + 0.15f, c.y), new Vector3(1f, 0.3f, s.y), coping);
        B(new Vector3(c.x + s.x * 0.5f + 0.5f, y + 0.15f, c.y), new Vector3(1f, 0.3f, s.y), coping);
        B(new Vector3(c.x, y + 0.02f, c.y), new Vector3(s.x, 0.04f, s.y), new Color(0.3f, 0.75f, 0.85f), false);
        BM(new Vector3(c.x, y + 0.12f, c.y), new Vector3(s.x, 0.02f, s.y), Mats.Water, false);
        for (int i = 0; i < 3; i++)
            B(new Vector3(c.x - 5f + i * 5f, y + 0.3f, c.y + s.y * 0.5f + 2.5f), new Vector3(1f, 0.25f, 2.2f), i % 2 == 0 ? new Color(1f, 0.5f, 0.3f) : new Color(0.3f, 0.6f, 1f), true, new Vector3(-8f, 0f, 0f));
    }

    // ---------------- pond ----------------
    static void Pond()
    {
        Vector2 c = Layout.PondC, r = Layout.PondR;
        var water = Mats.Prim(PrimitiveType.Cylinder, root, new Vector3(c.x, Layout.WaterY, c.y), new Vector3(r.x * 2.5f, 0.01f, r.y * 2.5f), Mats.Water, false);
        water.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        water.name = "PondWater";
        water.transform.SetParent(null, true);   // keep separate (not merged, no shadows)
        // dock on the west shore
        float dz = c.y, dx0 = c.x - r.x - 10f, dx1 = c.x - r.x + 9f;
        B(new Vector3((dx0 + dx1) * 0.5f, 0.3f, dz), new Vector3(dx1 - dx0, 0.25f, 3f), Wood);
        for (float x = dx0 + 1f; x < dx1; x += 3f)
            for (int s = -1; s <= 1; s += 2) Cyl(new Vector3(x, -1f, dz + 1.4f * s), new Vector3(0.3f, 1.4f, 0.3f), Color.Lerp(Wood, Color.black, 0.3f));
        // reeds + lily pads
        var rnd = new System.Random(11);
        for (int i = 0; i < 70; i++)
        {
            float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
            bool pad = i % 3 == 0;
            float q = pad ? 0.75f + (float)rnd.NextDouble() * 0.25f : 1.02f + (float)rnd.NextDouble() * 0.1f;
            float x = c.x + Mathf.Cos(a) * r.x * q, z = c.y + Mathf.Sin(a) * r.y * q;
            if (Mathf.Abs(z - dz) < 5f && x < c.x) continue;
            if (pad) Cyl(new Vector3(x, Layout.WaterY + 0.03f, z), new Vector3(1.1f, 0.01f, 1.1f), new Color(0.25f, 0.55f, 0.2f));
            else
            {
                float gy = GY(x, z);
                for (int k = 0; k < 3; k++)
                    Cyl(new Vector3(x + k * 0.25f, gy + 0.9f, z + (k % 2) * 0.2f), new Vector3(0.08f, 1f + k * 0.2f, 0.08f), new Color(0.35f, 0.5f, 0.2f));
            }
        }
        // underwater world: stubbed. A parked submarine + sign.
        Vector3 sp = new Vector3(c.x - r.x + 13f, Layout.WaterY - 0.4f, dz - 7f);
        Material subM = Mats.Shiny(Mats.Hex("#00a5ff"));
        Mats.Prim(PrimitiveType.Capsule, root, sp, new Vector3(2.4f, 3.6f, 2.4f), new Vector3(0f, 0f, 90f), subM, true);
        Mats.Prim(PrimitiveType.Cube, root, sp + new Vector3(0.5f, 1.4f, 0f), new Vector3(1.6f, 1.2f, 1.2f), Vector3.zero, subM, false);
        Mats.Prim(PrimitiveType.Sphere, root, sp + new Vector3(0.5f, 2.0f, 0f), new Vector3(1.1f, 0.6f, 0.9f), Vector3.zero, Mats.Glass, false);
        Cyl(sp + new Vector3(0.2f, 2.6f, 0f), new Vector3(0.12f, 0.6f, 0.12f), new Color(0.2f, 0.2f, 0.2f));
        Sign(new Vector3(dx1 - 1f, 2.4f, dz + 1.7f), 270f, "UNDERWATER WORLD\n<size=30>coming soon</size>", new Color(0.05f, 0.3f, 0.55f), 7f, 2.2f);
        B(new Vector3(dx1 - 1f, 1.1f, dz + 1.6f), new Vector3(0.15f, 1.6f, 0.15f), Wood, false);
    }

    // ---------------- rally track ----------------
    static void Track()
    {
        Color[] flagC = { new Color(1f, 0.25f, 0.2f), new Color(1f, 0.9f, 0.2f), new Color(0.2f, 0.6f, 1f), Color.white };
        const int n = 28;
        for (int i = 0; i < n; i++)
        {
            float t = i * Mathf.PI * 2f / n;
            Vector3 p = Layout.OvalPoint(t);
            Vector3 outward = new Vector3(Mathf.Cos(t) / Layout.TrackR.x, 0f, Mathf.Sin(t) / Layout.TrackR.y).normalized;
            Vector3 fp = p + outward * (Layout.TrackW * 0.5f + 2.5f);
            if ((new Vector2(fp.x, fp.z) - Layout.Spur[Layout.Spur.Length - 1]).magnitude < 12f) continue;
            float gy = GY(fp.x, fp.z);
            Cyl(new Vector3(fp.x, gy + 1.5f, fp.z), new Vector3(0.12f, 1.5f, 0.12f), new Color(0.9f, 0.9f, 0.9f), true);
            B(new Vector3(fp.x, gy + 2.6f, fp.z) + Vector3.Cross(outward, Vector3.up) * 0.6f, new Vector3(1.2f, 0.7f, 0.04f), flagC[i % flagC.Length], false,
              new Vector3(0f, Mathf.Atan2(outward.x, outward.z) * Mathf.Rad2Deg + 90f, 0f));
            // tyre stacks on the inside
            Vector3 ip = p - outward * (Layout.TrackW * 0.5f + 1.5f);
            float iy = GY(ip.x, ip.z);
            for (int k = 0; k < 2; k++) Cyl(new Vector3(ip.x, iy + 0.25f + k * 0.5f, ip.z), new Vector3(1.2f, 0.25f, 1.2f), k == 0 ? new Color(0.1f, 0.1f, 0.1f) : new Color(0.9f, 0.9f, 0.9f), k == 0);
        }
        // start / finish arch on the east side
        Vector3 a = Layout.OvalPoint(0f);
        float ay = GY(a.x, a.z);
        for (int s = -1; s <= 1; s += 2) B(new Vector3(a.x, ay + 3f, a.z + (Layout.TrackW * 0.5f + 1f) * s), new Vector3(0.6f, 6f, 0.6f), new Color(0.15f, 0.15f, 0.15f));
        for (int k = 0; k < 8; k++)
            B(new Vector3(a.x, ay + 6.2f, a.z - Layout.TrackW * 0.5f - 0.75f + k * (Layout.TrackW + 1.5f) / 8f + 0.9f), new Vector3(0.3f, 0.8f, (Layout.TrackW + 1.5f) / 8f), k % 2 == 0 ? Color.white : Color.black, false);
        // two dirt jumps on the north straight
        foreach (float tt in new[] { Mathf.PI * 0.42f, Mathf.PI * 0.62f })
        {
            Vector3 p = Layout.OvalPoint(tt);
            Vector3 tan = new Vector3(-Mathf.Sin(tt) * Layout.TrackR.x, 0f, Mathf.Cos(tt) * Layout.TrackR.y).normalized;
            float yaw = Mathf.Atan2(-tan.x, -tan.z) * Mathf.Rad2Deg;   // ramps face clockwise travel
            float gy = GY(p.x, p.z);
            B(new Vector3(p.x, gy + 0.6f, p.z), new Vector3(8f, 0.4f, 7f), new Color(0.5f, 0.4f, 0.28f), true, new Vector3(-14f, yaw, 0f));
        }
        Sign(new Vector3(Layout.Spur[2].x - 9f, GY(Layout.Spur[2].x - 9f, Layout.Spur[2].y) + 3f, Layout.Spur[2].y), 200f, "RALLY TRACK", new Color(0.55f, 0.25f, 0.1f), 8f, 1.6f);
    }

    // ---------------- Starship (space world stubbed) ----------------
    static void Starship()
    {
        Vector2 c = Layout.PadC;
        float y = GY(c.x, c.y);
        B(new Vector3(c.x, y + 0.5f, c.y), new Vector3(26f, 1f, 26f), Concrete);
        float top = y + 1f;
        for (int k = 0; k < 6; k++)
        {
            Vector3 d = Quaternion.Euler(0f, k * 60f, 0f) * Vector3.forward * 4.2f;
            B(new Vector3(c.x, top + 2.5f, c.y) + d, new Vector3(1f, 5f, 1f), new Color(0.3f, 0.3f, 0.32f));
        }
        Cyl(new Vector3(c.x, top + 5.2f, c.y), new Vector3(9.5f, 0.4f, 9.5f), new Color(0.35f, 0.35f, 0.37f), true);
        Material steel = Mats.Steel(new Color(0.78f, 0.79f, 0.8f));
        Material tile = Mats.Lit(new Color(0.12f, 0.12f, 0.13f));
        float by = top + 5.6f;
        Mats.Prim(PrimitiveType.Cylinder, root, new Vector3(c.x, by + 17f, c.y), new Vector3(7f, 17f, 7f), Vector3.zero, steel, true);   // booster
        Mats.Prim(PrimitiveType.Cylinder, root, new Vector3(c.x, by + 35.5f, c.y), new Vector3(7.05f, 1.5f, 7.05f), Vector3.zero, tile, false); // hot-staging ring
        Mats.Prim(PrimitiveType.Cylinder, root, new Vector3(c.x, by + 49f, c.y), new Vector3(7f, 12f, 7f), Vector3.zero, steel, false);       // ship
        Mats.Prim(PrimitiveType.Cylinder, root, new Vector3(c.x + 0.6f, by + 49f, c.y), new Vector3(6.2f, 12f, 6.2f), Vector3.zero, tile, false);  // heat shield side
        Mats.Prim(PrimitiveType.Sphere, root, new Vector3(c.x, by + 61f, c.y), new Vector3(7f, 10f, 7f), Vector3.zero, steel, false);       // nose
        for (int s = -1; s <= 1; s += 2)
        {
            B(new Vector3(c.x, by + 58f, c.y + 3.8f * s), new Vector3(1.2f, 4f, 2.2f), new Color(0.15f, 0.15f, 0.16f), false);    // forward flaps
            B(new Vector3(c.x, by + 44f, c.y + 4f * s), new Vector3(1.4f, 5f, 2.8f), new Color(0.15f, 0.15f, 0.16f), false);      // aft flaps
            B(new Vector3(c.x + 3.8f * s, by + 32f, c.y), new Vector3(2.6f, 2.6f, 0.4f), new Color(0.3f, 0.3f, 0.3f), false);     // grid fins
        }
        // tower + chopsticks
        Vector3 tw = new Vector3(c.x - 10f, top, c.y);
        Color lat = new Color(0.25f, 0.25f, 0.27f);
        for (int s = 0; s < 4; s++)
        {
            Vector3 o = new Vector3((s % 2) * 3f - 1.5f, 0f, (s / 2) * 3f - 1.5f);
            B(tw + o + Vector3.up * 36f, new Vector3(0.5f, 72f, 0.5f), lat, s == 0);
        }
        for (float h = 4f; h < 72f; h += 6f)
        {
            B(tw + new Vector3(0f, h, -1.5f), new Vector3(3.4f, 0.3f, 0.3f), lat, false);
            B(tw + new Vector3(0f, h, 1.5f), new Vector3(3.4f, 0.3f, 0.3f), lat, false);
            B(tw + new Vector3(-1.5f, h, 0f), new Vector3(0.3f, 0.3f, 3.4f), lat, false);
            B(tw + new Vector3(1.5f, h, 0f), new Vector3(0.3f, 0.3f, 3.4f), lat, false);
        }
        for (int s = -1; s <= 1; s += 2) B(tw + new Vector3(5.5f, 52f, 2.6f * s), new Vector3(9f, 0.8f, 0.6f), lat, false, new Vector3(0f, -12f * s, 0f));
        Sign(new Vector3(c.x + 15f, top + 2.8f, c.y), 90f, "SPACE WORLD\n<size=30>coming soon</size>", new Color(0.1f, 0.1f, 0.25f), 8f, 2.4f);
        B(new Vector3(c.x + 15f, top + 1f, c.y), new Vector3(0.2f, 2f, 0.2f), new Color(0.3f, 0.3f, 0.3f), false);
    }

    // ---------------- trees + rocks ----------------
    static void Trees()
    {
        var r = new System.Random(42);
        Color trunk = new Color(0.36f, 0.25f, 0.16f);
        Color[] leaf = { new Color(0.2f, 0.42f, 0.16f), new Color(0.28f, 0.5f, 0.18f), new Color(0.16f, 0.36f, 0.2f) };
        Color pine = new Color(0.12f, 0.3f, 0.18f);
        int placed = 0;
        for (int tries = 0; tries < 2000 && placed < 150; tries++)
        {
            float x = (float)(r.NextDouble() * 2 - 1) * 175f, z = (float)(r.NextDouble() * 2 - 1) * 175f;
            if (Layout.Flatness(x, z) < 0.6f) continue;
            if (Layout.PondQ(x, z) < 1.3f) continue;
            if (Layout.RoadDist(x, z) < Layout.TrackW + 4f) continue;
            if (x > -30f && x < 30f && z > 30f && z < 70f) continue;   // keep the yard in front of the garage clear
            float gy = GY(x, z);
            float s = 0.8f + (float)r.NextDouble() * 0.6f;
            bool conifer = r.NextDouble() < 0.45;
            Cyl(new Vector3(x, gy + 1.6f * s, z), new Vector3(0.55f * s, 1.6f * s, 0.55f * s), trunk, true);
            if (conifer)
            {
                for (int k = 0; k < 3; k++)
                {
                    float w = (3.2f - k * 0.8f) * s;
                    Ball(new Vector3(x, gy + (3.2f + k * 1.7f) * s, z), new Vector3(w, 2.4f * s, w), pine);
                }
            }
            else
            {
                Color lc = leaf[placed % leaf.Length];
                Ball(new Vector3(x, gy + 4.4f * s, z), new Vector3(4.2f, 3.6f, 4.2f) * s, lc);
                Ball(new Vector3(x + 1.2f * s, gy + 3.8f * s, z + 0.6f * s), new Vector3(2.8f, 2.4f, 2.8f) * s, lc);
                Ball(new Vector3(x - 1f * s, gy + 4f * s, z - 0.8f * s), new Vector3(2.6f, 2.4f, 2.6f) * s, lc);
            }
            placed++;
        }
        for (int i = 0; i < 40; i++)
        {
            float x = (float)(r.NextDouble() * 2 - 1) * 170f, z = (float)(r.NextDouble() * 2 - 1) * 170f;
            if (Layout.Flatness(x, z) < 0.5f || Layout.PondQ(x, z) < 1.2f) continue;
            float s = 0.5f + (float)r.NextDouble() * 1.4f;
            float g = 0.45f + (float)r.NextDouble() * 0.15f;
            var rock = Mats.Prim(PrimitiveType.Sphere, root, new Vector3(x, GY(x, z) + 0.2f * s, z), new Vector3(1.4f, 0.8f, 1.1f) * s, new Vector3(0f, (float)r.NextDouble() * 180f, 0f), Mats.Lit(new Color(g, g, g * 0.95f)), true);
        }
    }

    // ---------------- loose props (knocked around by explosions and vehicles) ----------------
    static void Props()
    {
        var parent = new GameObject("Props").transform;
        Color crate = new Color(0.68f, 0.5f, 0.28f);
        // crate pyramid in the track infield (tank target range)
        Vector3 c = new Vector3(Layout.TrackC.x, 0f, Layout.TrackC.y);
        float gy = GY(c.x, c.y);
        int idx = 0;
        for (int row = 0; row < 4; row++)
            for (int i = 0; i < 4 - row; i++)
            {
                Prop(parent, PrimitiveType.Cube, new Vector3(c.x - (3 - row) * 0.6f + i * 1.25f, gy + 0.6f + row * 1.2f, c.z), Vector3.one * 1.2f, crate, 45f);
                idx++;
            }
        for (int i = 0; i < 6; i++)
            Prop(parent, PrimitiveType.Cube, new Vector3(c.x + 14f + (i % 3) * 1.3f, gy + 0.6f + (i / 3) * 1.2f, c.z - 6f), Vector3.one * 1.2f, crate, 45f);
        // barrels by the garage and the drive
        Color[] bc = { new Color(0.85f, 0.2f, 0.15f), new Color(0.2f, 0.45f, 0.85f), new Color(0.95f, 0.75f, 0.15f) };
        for (int i = 0; i < 9; i++)
        {
            float x = 30f + (i % 3) * 1.4f, z = 30f + (i / 3) * 1.4f;
            Prop(parent, PrimitiveType.Cylinder, new Vector3(x, GY(x, z) + 0.75f, z), new Vector3(1f, 0.75f, 1f), bc[i % 3], 60f);
        }
        // hay bales near the pond path
        for (int i = 0; i < 6; i++)
        {
            float x = 12f + i * 2.4f, z = -24f;
            var hb = Prop(parent, PrimitiveType.Cylinder, new Vector3(x, GY(x, z) + 0.8f, z), new Vector3(1.6f, 0.7f, 1.6f), new Color(0.85f, 0.72f, 0.35f), 80f);
            hb.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
        }
        // traffic cones along the spur
        for (int i = 0; i < 10; i++)
        {
            Vector2 a = Layout.Spur[1 + i % 3], b = Layout.Spur[2 + i % 3];
            Vector2 p = Vector2.Lerp(a, b, (i / 3) / 4f + 0.1f);
            Vector2 n = new Vector2(-(b - a).y, (b - a).x).normalized * (Layout.TrackW * 0.5f + 0.5f) * (i % 2 == 0 ? 1f : -1f);
            p += n;
            var cone = Prop(parent, PrimitiveType.Cylinder, new Vector3(p.x, GY(p.x, p.y) + 0.4f, p.y), new Vector3(0.45f, 0.4f, 0.45f), new Color(1f, 0.45f, 0.05f), 4f);
        }
    }

    static GameObject Prop(Transform parent, PrimitiveType t, Vector3 pos, Vector3 scale, Color c, float mass)
    {
        var g = Mats.Prim(t, parent, pos, scale, Mats.Lit(c), true);
        g.layer = Vehicle.PropLayer;
        var rb = g.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.drag = 0.05f;
        rb.angularDrag = 0.3f;
        rb.Sleep();
        return g;
    }

    // ---------------- signs ----------------
    public static void Sign(Vector3 pos, float yaw, string text, Color bg, float width, float height)
    {
        var go = new GameObject("Sign");
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, yaw + 180f, 0f);
        var cv = go.AddComponent<Canvas>();
        cv.renderMode = RenderMode.WorldSpace;
        var cs = go.AddComponent<CanvasScaler>();
        cs.dynamicPixelsPerUnit = 3f;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(width * 50f, height * 50f);
        go.transform.localScale = Vector3.one * 0.02f;
        var img = UIK.Img(go.transform, null, bg, new Vector2(0.5f, 0.5f), Vector2.zero, rt.sizeDelta);
        img.raycastTarget = false;
        Text t = UIK.Label(go.transform, text, Mathf.RoundToInt(Mathf.Min(height * 50f * 0.55f, 54f)), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, rt.sizeDelta * 0.95f, Color.white);
        t.supportRichText = true;
        // back face so it reads from behind as a plain board
        var back = Mats.Prim(PrimitiveType.Cube, go.transform, new Vector3(0f, 0f, 3f), new Vector3(width * 50f, height * 50f, 4f), Mats.Lit(Color.Lerp(bg, Color.black, 0.4f)));
    }
}
