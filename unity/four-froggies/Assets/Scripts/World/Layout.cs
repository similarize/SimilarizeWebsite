using UnityEngine;

// Ranch layout shared by the editor build script (terrain shaping) and the runtime world builder.
// Positions follow the three.js Four Froggies ranch (house west, 6-bay garage north of it,
// pond to the south-east, rally oval to the north), compressed to fit a 400 m square.
public static class Layout
{
    public const float Half = 200f;          // terrain spans -Half..Half on x and z
    // ffu37: the terrain reaches down to the deep pond bed (the submarine's seabed, ~-27 m), so the range starts lower;
    // the top stays at +24 (was -6 .. +24)
    public const float TerrainY = -34f;      // terrain transform y
    public const float TerrainH = 58f;       // terrain height range
    public const float WaterY = -0.55f;      // pond surface

    // James's house (two storeys, flat roof with helipad + drone pad)
    public static readonly Vector2 HouseC = new Vector2(-42f, -4f);
    public static readonly Vector2 HouseSize = new Vector2(46f, 30f);
    public const float HouseH = 9f;

    // 6-bay garage, bays open to +z (towards the track)
    public static readonly Vector2 GarageC = new Vector2(-2f, 22f);
    public static readonly Vector2 GarageSize = new Vector2(54f, 18f);
    public const float GarageH = 6f;
    public const int Bays = 6;
    public static float BayX(int i) { return GarageC.x - GarageSize.x * 0.5f + GarageSize.x / Bays * (i + 0.5f); }

    public static readonly Vector2 PoolC = new Vector2(-42f, -30f);
    public static readonly Vector2 PoolSize = new Vector2(16f, 9f);

    // Pond (ellipse) with a dock on the west shore, three islands, a boat ramp and a buoy gate course
    public static readonly Vector2 PondC = new Vector2(70f, -80f);
    public static readonly Vector2 PondR = new Vector2(80f, 56f);
    // islands: x, z, radius (inside the pond)
    public static readonly Vector3[] Islands = { new Vector3(88f, -70f, 10f), new Vector3(118f, -98f, 6.5f), new Vector3(46f, -108f, 5.5f) };

    // Figure-eight dirt rally track (crossover bridge at the centre) + loop-the-loop stunt lane + spur from the garage
    public static readonly Vector2 TrackC = new Vector2(40f, 112f);
    public const float TrackAx = 75f, TrackAz = 34f;   // lobe half-length (x), lobe half-height (z)
    public const float TrackW = 12f;
    public const float BridgeH = 7.5f;                 // deck height above ground at the crossover
    public static readonly Vector2[] Spur = { new Vector2(-2f, 31f), new Vector2(10f, 46f), new Vector2(35f, 60f), new Vector2(65f, 70f), new Vector2(93f, 77f) };
    // loop-the-loop runway (along +x)
    public static readonly Vector2 LoopC = new Vector2(-70f, 64f);
    // ffu19: LoopW is the lane width of the whole loop branch (link, runways, loop) and LoopShift the sideways offset of the
    // exit lane; the loop itself is a clothoid-eased teardrop (top radius LoopTopR, ease-in length LoopEase), see RallyTrack.Branch.
    public const float LoopR = 7.5f, LoopW = 7f, LoopShift = 9f, LoopRun = 34f;
    public const float LoopTopR = 8f, LoopEase = 16f;

    // ffu14: the loop is part of the circuit. Driving the figure-eight in +t, the LOOP branch leaves the west lobe at
    // t = EntryT, swings round (south-west) into the loop runway, goes through the loop, and the exit link merges back
    // into the figure-eight at t = ExitT (just before the bridge ramp starts).
    public const float EntryT = -2.0f, ExitT = -0.95f;
    static Vector2[] entryPath, exitPath;
    static Vector2 T2(float t) { Vector3 p = TrackPoint(t); return new Vector2(p.x, p.z); }
    public static Vector2 TrackDir(float t) { return (T2(t + 0.002f) - T2(t)).normalized; }
    static void Bez(System.Collections.Generic.List<Vector2> o, Vector2 a, Vector2 b, Vector2 c, Vector2 d, int n)
    {
        for (int i = (o.Count > 0 ? 1 : 0); i <= n; i++)
        {
            float u = i / (float)n, v = 1f - u;
            o.Add(v * v * v * a + 3f * v * v * u * b + 3f * v * u * u * c + u * u * u * d);
        }
    }
    public static Vector2[] LoopEntryPath
    {
        get
        {
            if (entryPath != null) return entryPath;
            var o = new System.Collections.Generic.List<Vector2>();
            Vector2 q0 = T2(EntryT), d0 = TrackDir(EntryT);
            Vector2 q1 = new Vector2(-100f, 79f), d1 = new Vector2(-0.94f, -0.34f).normalized;
            Vector2 q2 = LoopC + new Vector2(-LoopRun - 6f, 0f), d2 = Vector2.right;
            Bez(o, q0, q0 + d0 * 26f, q1 - d1 * 26f, q1, 60);
            Bez(o, q1, q1 + d1 * 13f, q2 - d2 * 13f, q2, 34);
            return entryPath = o.ToArray();
        }
    }
    public static Vector2[] LoopExitPath
    {
        get
        {
            if (exitPath != null) return exitPath;
            var o = new System.Collections.Generic.List<Vector2>();
            Vector2 p0 = LoopC + new Vector2(LoopRun, LoopShift), p3 = T2(ExitT), d3 = TrackDir(ExitT);
            Bez(o, p0, p0 + Vector2.right * 7f, p3 - d3 * 7f, p3, 24);
            return exitPath = o.ToArray();
        }
    }
    static float PathDist(Vector2 p, Vector2[] path)
    {
        float best = 1e9f;
        for (int i = 0; i < path.Length - 1; i++) best = Mathf.Min(best, SegDist(p, path[i], path[i + 1]));
        return best;
    }
    public static float LoopLinkDist(float x, float z)
    {
        Vector2 p = new Vector2(x, z);
        return Mathf.Min(PathDist(p, LoopEntryPath), PathDist(p, LoopExitPath));
    }

    // Starship on its Stage Zero pad
    public static readonly Vector2 PadC = new Vector2(-105f, -55f);

    public static float PondQ(float x, float z)
    {
        float dx = (x - PondC.x) / PondR.x, dz = (z - PondC.y) / PondR.y;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    public static bool InPond(float x, float z) { return PondQ(x, z) < 1.045f; }   // ffu39: was 1.08 - the shoreline is the ellipse now (PondRim)

    // 0 outside every island, 1 at an island's centre
    public static float IslandK(float x, float z, out int which)
    {
        float best = 0f; which = -1;
        for (int i = 0; i < Islands.Length; i++)
        {
            float d = new Vector2(x - Islands[i].x, z - Islands[i].y).magnitude / Islands[i].z;
            float k = Mathf.Clamp01(1.35f - d);
            if (k > best) { best = k; which = i; }
        }
        return best;
    }

    // centreline of the figure-eight (y = 0); t in radians, t=0 bridge over the crossing, t=PI the underpass
    public static Vector3 TrackPoint(float t)
    {
        return new Vector3(TrackC.x + TrackAx * Mathf.Sin(t), 0f, TrackC.y + TrackAz * Mathf.Sin(2f * t));
    }

    // deck height above the ground along the track: a ramped bridge around t=0, flat elsewhere
    public static float TrackH(float t)
    {
        float a = Mathf.Abs(Mathf.Atan2(Mathf.Sin(t), Mathf.Cos(t)));
        return BridgeH * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1.15f - a) / 0.85f));
    }

    // kept for older callers: a point on the track
    public static Vector3 OvalPoint(float t) { return TrackPoint(t); }

    static float SegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
        return (p - (a + ab * t)).magnitude;
    }

    // distance from (x,z) to the nearest dirt road centreline (figure-eight + spur + loop runway)
    public static float RoadDist(float x, float z)
    {
        Vector2 p = new Vector2(x, z);
        float best = 1e9f;
        const int n = 128;
        Vector3 prev = TrackPoint(0f);
        for (int i = 1; i <= n; i++)
        {
            Vector3 cur = TrackPoint(i * Mathf.PI * 2f / n);
            best = Mathf.Min(best, SegDist(p, new Vector2(prev.x, prev.z), new Vector2(cur.x, cur.z)));
            prev = cur;
        }
        for (int i = 0; i < Spur.Length - 1; i++) best = Mathf.Min(best, SegDist(p, Spur[i], Spur[i + 1]));
        best = Mathf.Min(best, SegDist(p, LoopC + new Vector2(-LoopRun - 6f, 0f), LoopC + new Vector2(LoopRun, LoopShift)) - LoopShift * 0.5f);
        best = Mathf.Min(best, LoopLinkDist(x, z));
        return best;
    }

    static float RectDist(float x, float z, Vector2 c, Vector2 size)
    {
        float dx = Mathf.Max(0f, Mathf.Abs(x - c.x) - size.x * 0.5f);
        float dz = Mathf.Max(0f, Mathf.Abs(z - c.y) - size.y * 0.5f);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    // 0 = must be flat (buildings, yard, roads), 1 = free to roll
    public static float Flatness(float x, float z)
    {
        float d = Mathf.Min(RectDist(x, z, HouseC, HouseSize + new Vector2(14f, 14f)), RectDist(x, z, GarageC, GarageSize + new Vector2(10f, 22f)));
        d = Mathf.Min(d, RectDist(x, z, PadC, new Vector2(30f, 30f)));
        d = Mathf.Min(d, RoadDist(x, z) - TrackW * 0.5f - 6f);
        d = Mathf.Min(d, RectDist(x, z, LoopC + new Vector2(-6f, LoopShift * 0.5f), new Vector2(LoopRun * 2f + 24f, LoopShift + LoopW + 14f)));
        d = Mathf.Min(d, RectDist(x, z, new Vector2(TrackC.x, TrackC.y), new Vector2(TrackAx * 2f + 20f, TrackAz * 2f + 24f)) );
        return Mathf.Clamp01(d / 18f);
    }

    // world height of the ground (used by the build script to shape the terrain)
    public static float GroundY(float x, float z)
    {
        float n = (Mathf.PerlinNoise(x * 0.012f + 31.7f, z * 0.012f + 11.3f) - 0.5f) * 3.2f
                + (Mathf.PerlinNoise(x * 0.04f + 5.1f, z * 0.04f + 9.9f) - 0.5f) * 0.8f;
        float y = n * Flatness(x, z) * JetLayout.Flat(x, z);   // ffu33: flat under James's hangar + runway
        // pond basin (ffu37: the bed is the submarine's seabed - see PondBedY)
        float q = PondQ(x, z);
        if (q < 1.25f)
        {
            float k = Mathf.SmoothStep(0f, 1f, (1.25f - q) / 0.45f);
            y = Mathf.Lerp(y, PondBedY(x, z), k);
        }
        y = PondRim(x, z, y);   // ffu39: a natural bank all round the pond - no lawn below the water line outside it
        // edge berm
        float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) / Half;
        y += Mathf.SmoothStep(0f, 1f, (edge - 0.86f) / 0.14f) * 12f;
        return y;
    }

    // ---------------- ffu37: ONE pond bed for everything ----------------
    // The underwater world (the submarine) is the truth: its seabed SubSeaY (local metres around the pond centre, 0 = the
    // water surface) maps 1:1 onto the ranch pond (UnderwaterWorld.SurfaceSwim / DiveFromPond use the same mapping).
    // Ranch pond bed = that seabed over the middle of the pond (q < 0.42), rising through a drop-off to the old shallow
    // shelf by the shore (q 0.92 .. 1.25 unchanged), islands stand on skirts. The baked terrain, Ranch.GY, vehicles,
    // mechs, frogs and the sub all read this one surface.
    public static float SubSeaY(float x, float z)
    {
        float y = -22f + (Mathf.PerlinNoise(x * 0.03f + 3f, z * 0.03f + 7f) - 0.5f) * 8f + (Mathf.PerlinNoise(x * 0.11f, z * 0.11f) - 0.5f) * 1.6f;
        float reef = Mathf.Clamp01(1f - new Vector2(x, z).magnitude / 38f);
        y += reef * reef * 7f;                                  // shallow reef mound in the middle
        float trench = Mathf.Clamp01(1f - Mathf.Abs(z + 70f) / 14f);
        y -= trench * 9f;                                        // a deep trench to the south
        float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
        y += Mathf.SmoothStep(0f, 1f, (edge - 92f) / 16f) * 24f;  // walls at the edge
        // ffu37: the pond's three islands stand on rocky cones (~48 deg) that the sub sees too; their tops stay 2.5 m under
        // the surface here (the beach above the water only exists on the ranch side, PondBedY)
        for (int i = 0; i < Islands.Length; i++)
        {
            float d = new Vector2(x - (Islands[i].x - PondC.x), z - (Islands[i].y - PondC.y)).magnitude;
            y = Mathf.Max(y, -2.5f - Mathf.Max(0f, d - Islands[i].z * 1.35f) * 1.1f);
        }
        return y;
    }

    // ---------------- ffu39: pond rim ----------------
    // Before: from q 1.0 out to the water plane's edge (q 1.25) the ground only blended from the shelf back to the Perlin
    // lawn, so wherever the lawn noise dipped (down to ~2 m under WaterY) the pond plane covered grass = "flooding" up to
    // 20 m past the reeds (and its hard elliptical edge showed). Now the shoreline is the pond ellipse itself: the bed
    // rises through q 0.985..1.045 to a low grassy bank RimTop (32 cm above the water), the ground never sits below that
    // bank out to q 1.3, then eases down at ~1:2 to whatever the lawn does further out (a hollow there holds no water - the
    // plane ends at ShoreQMax). PondRimLow(x, z) says where the old ground would have flooded (rocks / logs go there).
    public const float ShoreQ = 1.0f, ShoreQMax = 1.06f, RimTop = 0.32f;
    public static float PondRim(float x, float z, float y)
    {
        float q = PondQ(x, z);
        if (q < 0.985f || q > 2.2f) return y;
        int wi;
        if (IslandK(x, z, out wi) > 0f) return y;
        float rise = Mathf.SmoothStep(0f, 1f, (q - 0.985f) / 0.06f);
        float floorY = Mathf.Lerp(WaterY - 0.35f, WaterY + RimTop, rise) - Mathf.Max(0f, q - 1.3f) * Mathf.Min(PondR.x, PondR.y) * 0.5f;
        return Mathf.Max(y, floorY);
    }
    // how far under the water line the pre-ffu39 ground was just outside the shore (> 0 = it flooded there)
    public static float PondRimLow(float x, float z)
    {
        float n = (Mathf.PerlinNoise(x * 0.012f + 31.7f, z * 0.012f + 11.3f) - 0.5f) * 3.2f
                + (Mathf.PerlinNoise(x * 0.04f + 5.1f, z * 0.04f + 9.9f) - 0.5f) * 0.8f;
        float y = n * Flatness(x, z) * JetLayout.Flat(x, z);
        float q = PondQ(x, z);
        if (q < 1.25f) { float k = Mathf.SmoothStep(0f, 1f, (1.25f - q) / 0.45f); y = Mathf.Lerp(y, PondBedY(x, z), k); }
        return WaterY - y;
    }

    public const float DeepQ = 0.42f, ShelfQ = 0.92f;
    // world height of the ranch pond bed at (x, z) (only meaningful for PondQ < 1.25)
    public static float PondBedY(float x, float z)
    {
        float q = PondQ(x, z);
        float shelf = -4.2f + q * 1.5f;                                   // the old shallow basin (shore band)
        float deep = WaterY + SubSeaY(x - PondC.x, z - PondC.y);           // the submarine's seabed
        float bottom = Mathf.Min(shelf, Mathf.Lerp(deep, shelf, Mathf.SmoothStep(0f, 1f, (q - DeepQ) / (ShelfQ - DeepQ))));
        // islands: their underwater cones come with the seabed (SubSeaY); the beach rises out of the water here
        int wi;
        float ik = IslandK(x, z, out wi);
        if (ik > 0f) bottom = Mathf.Max(bottom, Mathf.Lerp(bottom, WaterY + 1.1f, Mathf.SmoothStep(0f, 1f, ik / 0.55f)));
        return bottom;
    }
}
