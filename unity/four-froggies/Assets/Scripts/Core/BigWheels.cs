using UnityEngine;

// ffu42 (Bill: "a slider to increase the size of the wheels ... all of the vehicle wheels ... just for fun, like a monster
// truck"): ONE global wheel-size setting, 1x (stock) .. 4x, saved in SaveStore "ff.wheels" (quarter steps x4), and the
// shared geometry every wheeled vehicle uses for it (GroundVehicle = Cybertrucks, Monster Truck, riding mowers, moon
// buggy, Curiosity; Bike = the garage bikes; RcCar = the RC cars). Tracked vehicles (Ripsaw, tank, Optimus suit) and
// walkers keep their running gear. Towed trailers keep stock wheels; the tow vehicle's hitch drops to stay at ball height.
//
// Per wheel, for a scale k (tyre radius r = r0 k, capped at MaxR):
//   * the axle moves out sideways (SpreadX x dr) and fore / aft (SpreadZ x dr) so big tyres clear the body and each other
//     (wider track + longer wheelbase = stable),
//   * the suspension ray gets longer by the bigger radius plus `extra` = Drop x dr: the axle hangs lower under the body,
//     so the body rides up on the tyres (body lift ~ dr + extra) instead of the tyres swallowing the body,
//   * travel (rest) grows a little and the springs soften with it (same sag fraction),
//   * the ray becomes a sphere cast of radius SphereK x dr: the front of a big tyre "sees" a rock / log before the axle is
//     over it, so the spring lifts the truck over it (climbing),
//   * the visual node scales (width a bit less than diameter); spin uses the new radius (v / r) so it stays correct.
// Vehicles ease their own k toward the setting (Rate per s): tyres "inflate" over ~1-3 s, no launch.
public static class BigWheels
{
    public const float Min = 1f, Max = 4f, Step = 0.25f;
    public const float MaxR = 2.4f;          // no tyre bigger than 2.4 m radius (the Monster Truck tops out at ~2.5x)
    public const float Drop = 0.85f, RestGrow = 0.35f, WidthGrow = 0.5f, SpreadX = 0.45f, SpreadZ = 0.75f, SphereK = 0.5f;
    public const float Rate = 1.1f;          // scale units per second
    public const float ComCut = 0.9f;        // centre of mass drops by this x the body lift (stays at its stock height off the ground)
    static float s = -1f;
    public static int Changes;

    public static float S
    {
        get
        {
            if (s < 0f) s = Mathf.Clamp(SaveStore.GetInt("ff.wheels", 4) / 4f, Min, Max);
            return s;
        }
    }
    public static float Frac { get { return (S - Min) / (Max - Min); } }
    public static bool On { get { return S > 1.001f; } }

    public static void Set(float v, string why)
    {
        v = Mathf.Clamp(Mathf.Round(v / Step) * Step, Min, Max);
        if (Mathf.Abs(v - S) < 0.001f) return;
        s = v; Changes++;
        SaveStore.SetInt("ff.wheels", Mathf.RoundToInt(v * 4f));
        SaveStore.Flush();
        Debug.Log("BigWheels: wheel size " + Label + " (" + why + ")");
    }
    public static void Nudge(int d, string why) { Set(S + d * Step, why); }
    public static void SetFrac(float f, string why) { Set(Min + Mathf.Clamp01(f) * (Max - Min), why); }
    public static string Label { get { return S < 1.001f ? "1x (stock)" : S.ToString("0.##") + "x" + (S >= 2.5f ? " MONSTER" : ""); } }

    public static float Ease(float k, float target, float dt) { return Mathf.MoveTowards(k, target, Rate * dt); }
    public static float RFor(float r0, float k) { return k <= 1.0001f ? r0 : Mathf.Min(r0 * k, Mathf.Max(r0, MaxR)); }

    public struct Geo { public float r, rest, extra, sr, kv, kw, lift; public Vector3 mount; }

    // wheelbase stretch for the vehicle: at least SpreadZ x (kv - 1) longer, and enough that neighbouring tyres on one side
    // (stock gap minGap0 between axles) never touch: (2 r_max + 8 cm) / minGap0
    public static float ZFactor(float k, float rMax0, float minGap0)
    {
        float rk = RFor(rMax0, k), kv = rk / Mathf.Max(0.01f, rMax0);
        float f = 1f + (kv - 1f) * SpreadZ * 0.27f;
        if (minGap0 > 0.05f) f = Mathf.Max(f, (2f * rk + 0.08f) / minGap0);
        return Mathf.Max(1f, f);
    }
    // stock axle gap between neighbouring wheels on the same side (x sign); 0 = no neighbours
    public static float MinGap(System.Collections.Generic.List<Vector3> axles)
    {
        float g = 0f;
        for (int i = 0; i < axles.Count; i++)
            for (int j = i + 1; j < axles.Count; j++)
            {
                Vector3 a = axles[i], b = axles[j];
                if (Mathf.Abs(a.x - b.x) > 0.3f) continue;
                float d = Mathf.Abs(a.z - b.z);
                if (d > 0.05f && (g == 0f || d < g)) g = d;
            }
        return g;
    }

    // axle0 = the stock axle centre (mount - up x rest0) in vehicle space; zc / fz = wheelbase centre + stretch (ZFactor)
    public static Geo Wheel(float k, Vector3 axle0, float r0, float rest0, float zc, float fz)
    {
        Geo g;
        g.r = RFor(r0, k);
        float dr = g.r - r0;
        g.kv = g.r / Mathf.Max(0.01f, r0);
        g.kw = 1f + (g.kv - 1f) * WidthGrow;
        g.rest = rest0 * (1f + (g.kv - 1f) * RestGrow);
        g.extra = dr * Drop;
        g.sr = dr * SphereK;
        Vector3 a = axle0;
        if (Mathf.Abs(a.x) > 0.05f) a.x += Mathf.Sign(a.x) * dr * SpreadX;
        if (k > 1.0001f) a.z = zc + (axle0.z - zc) * fz;
        g.mount = a + Vector3.up * g.rest;
        // body height gain at the same sag fraction (~0.45 of travel)
        g.lift = dr + g.extra - 0.45f * (g.rest - rest0);
        return g;
    }

    // suspension probe: a sphere (big tyres) then the plain ray. dist = equivalent ray distance from the origin.
    public static bool Cast(Vector3 o, Vector3 up, float len, float sr, int mask, out RaycastHit h, out float dist)
    {
        if (sr > 0.03f && len > sr * 1.2f && Physics.SphereCast(o, sr, -up, out h, len - sr, mask & ~(1 << Trailer.TrailerLayer), QueryTriggerInteraction.Ignore))
        {
            dist = h.distance + sr;
            return true;
        }
        if (Physics.Raycast(o, -up, out h, len, mask, QueryTriggerInteraction.Ignore)) { dist = h.distance; return true; }
        dist = len;
        return false;
    }

    public static void Vis(Transform vis, Vector3 scale0, Geo g)
    {
        if (vis == null) return;
        vis.localScale = Vector3.Scale(scale0, new Vector3(g.kw, g.kv, g.kv));
    }

    // engine note a touch lower on big tyres (bigger, heavier rolling stock)
    public static float PitchFor(float p0, float k) { return p0 * (1f - 0.05f * (Mathf.Min(k, 4f) - 1f)); }
}
