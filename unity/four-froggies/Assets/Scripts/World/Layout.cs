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

    // Pond (ellipse) with a dock on the west shore
    public static readonly Vector2 PondC = new Vector2(64f, -64f);
    public static readonly Vector2 PondR = new Vector2(42f, 30f);

    // Rally oval + a dirt spur from the garage
    public static readonly Vector2 TrackC = new Vector2(40f, 112f);
    public static readonly Vector2 TrackR = new Vector2(58f, 32f);
    public const float TrackW = 12f;
    public static readonly Vector2[] Spur = { new Vector2(-2f, 31f), new Vector2(4f, 46f), new Vector2(16f, 62f), new Vector2(30f, 74f), new Vector2(40f, 80f) };

    // Starship on its Stage Zero pad (space world is stubbed)
    public static readonly Vector2 PadC = new Vector2(-105f, -55f);

    public static float PondQ(float x, float z)
    {
        float dx = (x - PondC.x) / PondR.x, dz = (z - PondC.y) / PondR.y;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    public static bool InPond(float x, float z) { return PondQ(x, z) < 1.08f; }

    public static Vector3 OvalPoint(float t)
    {
        return new Vector3(TrackC.x + Mathf.Cos(t) * TrackR.x, 0f, TrackC.y + Mathf.Sin(t) * TrackR.y);
    }

    static float SegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
        return (p - (a + ab * t)).magnitude;
    }

    // distance from (x,z) to the nearest dirt road centreline (oval + spur)
    public static float RoadDist(float x, float z)
    {
        Vector2 p = new Vector2(x, z);
        float best = 1e9f;
        const int n = 96;
        Vector3 prev = OvalPoint(0f);
        for (int i = 1; i <= n; i++)
        {
            Vector3 cur = OvalPoint(i * Mathf.PI * 2f / n);
            best = Mathf.Min(best, SegDist(p, new Vector2(prev.x, prev.z), new Vector2(cur.x, cur.z)));
            prev = cur;
        }
        for (int i = 0; i < Spur.Length - 1; i++) best = Mathf.Min(best, SegDist(p, Spur[i], Spur[i + 1]));
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
        d = Mathf.Min(d, RoadDist(x, z) - TrackW * 0.5f - 2f);
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
        }
        // edge berm
        float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) / Half;
        y += Mathf.SmoothStep(0f, 1f, (edge - 0.86f) / 0.14f) * 12f;
        return y;
    }
}
