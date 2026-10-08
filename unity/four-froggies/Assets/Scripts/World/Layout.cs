using UnityEngine;

// Ranch layout shared by the editor build script (terrain shaping) and the runtime world builder.
// Positions follow the three.js Four Froggies ranch (house west, 6-bay garage north of it,
// pond to the south-east, rally oval to the north), compressed to fit a 400 m square.
public static class Layout
{
    public const float Half = 200f;          // terrain spans -Half..Half on x and z
    public const float TerrainY = -6f;       // terrain transform y
    public const float TerrainH = 30f;       // terrain height range
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
    public const float LoopR = 7.5f, LoopW = 6f, LoopShift = 7.5f, LoopRun = 34f;

    // Starship on its Stage Zero pad
    public static readonly Vector2 PadC = new Vector2(-105f, -55f);

    public static float PondQ(float x, float z)
    {
        float dx = (x - PondC.x) / PondR.x, dz = (z - PondC.y) / PondR.y;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    public static bool InPond(float x, float z) { return PondQ(x, z) < 1.08f; }

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
        float y = n * Flatness(x, z);
        // pond basin
        float q = PondQ(x, z);
        if (q < 1.25f)
        {
            float k = Mathf.SmoothStep(0f, 1f, (1.25f - q) / 0.45f);
            float bottom = -4.2f + q * 1.5f;
            y = Mathf.Lerp(y, bottom, k);
            int wi;
            float ik = IslandK(x, z, out wi);
            if (ik > 0f) y = Mathf.Max(y, Mathf.Lerp(bottom, WaterY + 1.1f, Mathf.SmoothStep(0f, 1f, ik / 0.55f)));
        }
        // edge berm
        float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) / Half;
        y += Mathf.SmoothStep(0f, 1f, (edge - 0.86f) / 0.14f) * 12f;
        return y;
    }
}
