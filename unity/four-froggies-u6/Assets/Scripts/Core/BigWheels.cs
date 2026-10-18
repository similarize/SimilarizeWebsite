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
    public static int Changes;

    // ---------------- ffu45: PER-VEHICLE wheel size ----------------
    // Bill: "making every vehicle's wheels bigger at once breaks things (a maxed Monster Truck can't get out of the garage)".
    // Each vehicle now has its own size (default 1x), saved per vehicle id (Vehicle.WheelId = title @ spawn x,z) in SaveStore
    // "ff.wh.<id>" (quarter steps x4). The ffu42 global "ff.wheels" is migrated to 1x for everything (reset, never applied).
    // Only the vehicle you are IN is resized (help Settings row, phone WHEELS tab, View + D-pad up / down, [ / ]); robots
    // driving a vehicle keep that vehicle's size. Inside an enclosed building (garage, trailer barn, jet hangar; + 5 m in
    // front) the tyres shrink to stock and grow back once outside; growing is held whenever the raised body + spread tyres
    // would overlap a wall / ceiling (static colliders), and a grown vehicle that ends up touching a ceiling shrinks.
    static readonly System.Collections.Generic.Dictionary<string, float> want = new System.Collections.Generic.Dictionary<string, float>();
    static readonly System.Collections.Generic.Dictionary<int, int> blocked = new System.Collections.Generic.Dictionary<int, int>();   // 0 free, 1 hold, 2 shrink
    static bool migrated;
    static void Migrate()
    {
        if (migrated) return;
        migrated = true;
        int old = SaveStore.GetInt("ff.wheels", 4);
        if (old != 4) { SaveStore.SetInt("ff.wheels", 4); SaveStore.Flush(); Debug.Log("BigWheels: old global wheel size " + (old / 4f).ToString("0.##") + "x migrated -> 1x for every vehicle (sizes are per vehicle now)"); }
    }
    static readonly System.Collections.Generic.Dictionary<int, string> keys = new System.Collections.Generic.Dictionary<int, string>();
    static string Key(Vehicle v)
    {
        string ks; int iid = v.GetHashCode();
        if (keys.TryGetValue(iid, out ks)) return ks;
        var sb = new System.Text.StringBuilder("ff.wh.");
        foreach (char c in v.WheelId) sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == ',' || c == '@' ? c : '_');
        ks = sb.ToString(); keys[iid] = ks;
        return ks;
    }
    public static bool Resizable(Vehicle v)
    {
        if (v == null || v.stowed) return false;
        var gv = v as GroundVehicle;
        if (gv != null) return gv.BigOk;
        return v is Bike || v is RcCar;
    }
    // the size this vehicle's owner picked (1x unless changed)
    public static float Want(Vehicle v)
    {
        if (v == null) return 1f;
        Migrate();
        string k = Key(v); float s;
        if (!want.TryGetValue(k, out s)) { s = Mathf.Clamp(SaveStore.GetInt(k, 4) / 4f, Min, Max); want[k] = s; }
        return s;
    }
    public static void SetFor(Vehicle v, float val, string why, bool save = true)
    {
        if (v == null) return;
        val = Mathf.Clamp(Mathf.Round(val / Step) * Step, Min, Max);
        if (Mathf.Abs(val - Want(v)) < 0.001f) return;
        string k = Key(v);
        want[k] = val; Changes++;
        if (save) { SaveStore.SetInt(k, Mathf.RoundToInt(val * 4f)); SaveStore.Flush(); }
        Debug.Log("BigWheels: " + v.Title + " wheel size " + LabelOf(val) + " (" + why + ")" + (Capped(v) ? " - held at stock inside " + Building(v.transform.position) : ""));
    }
    public static void NudgeFor(Vehicle v, int d, string why) { SetFor(v, Want(v) + d * Step, why); }
    public static void SetFracFor(Vehicle v, float f, string why) { SetFor(v, Min + Mathf.Clamp01(f) * (Max - Min), why); }
    public static float FracOf(float k) { return (k - Min) / (Max - Min); }
    public static string LabelOf(float k) { return k < 1.001f ? "1x (stock)" : k.ToString("0.##") + "x" + (k >= 2.5f ? " MONSTER" : ""); }
    public static string LabelFor(Vehicle v) { return LabelOf(Want(v)); }
    // the wheel size readout for a vehicle: "2.5x MONSTER" + why it isn't there yet
    public static string Readout(Vehicle v)
    {
        string s = v.Title + " wheels " + LabelFor(v);
        if (Capped(v) && Want(v) > 1.001f) s += " - stock size inside the " + Building(v.transform.position) + ", grows outside";
        else if (Blocked(v) && Want(v) > 1.001f) s += " - no room to grow here";
        return s;
    }

    // enclosed buildings a vehicle drives in and out of (+ 5 m in front of / around them)
    public static string Building(Vector3 p)
    {
        const float m = 5f;
        Vector2 gc = Layout.GarageC, gs = Layout.GarageSize;
        if (Mathf.Abs(p.x - gc.x) < gs.x * 0.5f + m && Mathf.Abs(p.z - gc.y) < gs.y * 0.5f + m && p.y < Ranch.GY(p.x, p.z) + Layout.GarageH + 3f) return "garage";
        if (TrailerBarn.Built)
        {
            Vector3 l = Quaternion.Euler(0f, -TrailerBarn.Yaw, 0f) * (p - TrailerBarn.Centre);
            if (Mathf.Abs(l.x) < TrailerBarn.Wd * 0.5f + m && Mathf.Abs(l.z) < TrailerBarn.Dp * 0.5f + m && l.y < TrailerBarn.WallH + 3f) return "trailer barn";
        }
        if (Mathf.Abs(p.x - JetLayout.HangarC.x) < JetLayout.HangarW * 0.5f + m && Mathf.Abs(p.z - JetLayout.HangarC.y) < JetLayout.HangarD * 0.5f + m && p.y < Ranch.GY(p.x, p.z) + JetLayout.HangarH + 3f) return "hangar";
        return null;
    }
    public static bool Capped(Vehicle v) { return v != null && Building(v.transform.position) != null; }
    public static bool Blocked(Vehicle v) { int b; return v != null && blocked.TryGetValue(v.GetHashCode(), out b) && b != 0; }

    // the size a vehicle's tyres should ease toward right now (cur = current k, r0 = biggest stock tyre radius)
    public static float Target(Vehicle v, float cur, float r0)
    {
        if (v == null || v.stowed) return 1f;
        float w = Want(v);
        if (w <= 1.001f && cur <= 1.001f) return 1f;
        if (Building(v.transform.position) != null) return 1f;
        int id = v.GetHashCode();
        int b; blocked.TryGetValue(id, out b);
        if (((Time.frameCount + (id & 0x7fff)) & 7) == 0)
        {
            if (w > cur + 0.001f) b = Room(v, cur, Mathf.Min(w, cur + 0.35f), r0) ? 0 : 1;
            else b = cur > 1.001f && !Room(v, cur, cur, r0) ? 2 : 0;
            if (b != 0 && (!blocked.ContainsKey(id) || blocked[id] == 0)) Debug.Log("BigWheels: " + v.Title + (b == 1 ? " no room to grow past " : " touching a ceiling at ") + cur.ToString("0.00") + "x");
            blocked[id] = b;
        }
        if (b == 1) return Mathf.Min(w, cur);
        if (b == 2) return Mathf.Max(1f, cur - 0.5f);
        return w;
    }
    static readonly Collider[] hits = new Collider[24];
    // is there room above / beside the body for tyres of size `next` (from `cur`)? static colliders only
    static bool Room(Vehicle v, float cur, float next, float r0)
    {
        BoxCollider body = v.body;
        if (body == null) return true;
        float drN = RFor(r0, next) - r0, drC = RFor(r0, cur) - r0;
        float dLift = Mathf.Max(0f, (drN - drC) * (1f + Drop));
        Vector3 c = body.center, sz = body.size;
        bool grow = next > cur + 0.001f;
        // growing: the raised body from its middle up + the spread-out tyres; holding: just the top of the body (a ceiling)
        float yBot = grow ? c.y + dLift : c.y + sz.y * 0.25f, yTop = c.y + sz.y * 0.5f + dLift + (grow ? 0.15f : 0.05f);
        Vector3 lc = new Vector3(c.x, (yBot + yTop) * 0.5f, c.z);
        Vector3 he = grow ? new Vector3(sz.x * 0.5f + drN * SpreadX + 0.1f, (yTop - yBot) * 0.5f, sz.z * 0.5f + drN * SpreadZ * 0.5f + 0.1f)
                          : new Vector3(sz.x * 0.45f, (yTop - yBot) * 0.5f, sz.z * 0.45f);
        Transform t = v.transform;
        Vector3 wc = t.TransformPoint(lc);
        Vector3 ls = t.lossyScale; he = Vector3.Scale(he, new Vector3(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z)));
        int n = Physics.OverlapBoxNonAlloc(wc, he, hits, t.rotation, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider h = hits[i];
            if (h == null || h is TerrainCollider || h.attachedRigidbody != null || h.transform.IsChildOf(t)) continue;
            if (h.gameObject.layer == 27) continue;   // RC raceway invisible fence
            return false;
        }
        return true;
    }

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
