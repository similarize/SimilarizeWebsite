using UnityEngine;

// ffu63 (Bill Oct 19 16:14 MDT): robots never walk through walls / fences / buildings / furniture / vehicles / trees and stay inside the
// froggy perimeter in every world. Robots have no CharacterController (they are placed kinematically), so every walking step is swept
// with a sphere against the same solid colliders a froggy bumps into PLUS vehicles (froggies get pushed by vehicles in code instead):
//   * look-ahead steering: a wall in the way -> walk along it (bug-style, keeps the side until the goal is clear again)
//   * the actual step is swept: stop at the wall and slide along it, never through it
//   * hits within ~1.6 m of the walk goal are allowed (walking up to a charger / vehicle door / chore spot)
//   * perimeter: ranch square +-(Half - 4) m, Moon +-90 m, Mars / Callisto ground mesh +-(size - 4); off-ground in other worlds = blocked
//   * the old step / ExtWalk TIMEOUT TELEPORT to the goal (= clip-through) is gone: short clear hop only (< 2.5 m, nothing in between),
//     otherwise re-route up to 3 times, then give the step up where the robot stands.
// Knob: &rc63=0 = old behaviour (no sweep, timeout teleport).
public partial class Robot
{
    static int c63On = -1;   // lazy: never read the URL from a static initializer
    public static bool C63On { get { if (c63On < 0) c63On = Ffu33.Url("rc63") == "0" ? 0 : 1; return c63On == 1; } }
    const int C63Ignore = (1 << 2) | (1 << Vehicle.FrogLayer) | (1 << Vehicle.ProjectileLayer) | (1 << Vehicle.PropLayer) | (1 << 27);
    public static int C63Mask { get { return ~C63Ignore; } }
    [System.NonSerialized] public Vector3 c63Goal = new Vector3(float.NaN, 0f, 0f);
    [System.NonSerialized] public int c63Tries, c63Blocks;
    float c63Side, c63SideT;
    static readonly RaycastHit[] c63Hits = new RaycastHit[16];

    float C63R { get { return Mathf.Clamp(0.26f + bulk * 0.32f, 0.26f, 0.9f); } }

    // solid only while walking on the ground (not seated, flying, carried, jet-packing high above the ground)
    bool C63Solid(Vector3 p)
    {
        if (!C63On || flying || hidden || seatedIn != null || rideOf != null) return false;
        if (!float.IsNaN(yOver) && yOver > GroundAt(p.x, p.z) + 1.0f) return false;
        return true;
    }

    // first solid wall-like hit along the flat sweep from p (dist), skipping own colliders / triggers / floors / goal-side contacts
    bool C63Cast(Vector3 p, Vector3 dir, float dist, out RaycastHit best)
    {
        best = default(RaycastHit);
        float r = C63R;
        float lift = Mathf.Min(0.42f, height * 0.25f) + r;
        Vector3 o = new Vector3(p.x, p.y + lift, p.z);
        int n = Physics.SphereCastNonAlloc(o, r, dir, c63Hits, dist, C63Mask, QueryTriggerInteraction.Ignore);
        bool any = false; float bd = 1e9f;
        bool hasGoal = !float.IsNaN(c63Goal.x);
        float goalR = 1.6f + bulk * 0.6f;
        for (int i = 0; i < n; i++)
        {
            var h = c63Hits[i];
            if (h.collider == null || h.distance <= 0f && h.point == Vector3.zero) continue;   // overlapping at the start: let it walk out
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (Mathf.Abs(h.normal.y) > 0.55f) continue;                                        // floor / ramp / roof top, not a wall
            if (hasGoal && new Vector2(h.point.x - c63Goal.x, h.point.z - c63Goal.z).magnitude < goalR) continue;
            if (drv != null && drv.v != null && h.collider.transform.IsChildOf(drv.v.transform)) continue;
            if (h.distance < bd) { bd = h.distance; best = h; any = true; }
        }
        return any;
    }

    static Vector3 C63Flat(Vector3 n) { n.y = 0f; return n.sqrMagnitude > 1e-6f ? n.normalized : Vector3.zero; }

    // look-ahead: a wall in the way -> follow it on one side until the way to the goal is clear
    Vector3 C63Steer(Vector3 p, Vector3 dir, float spd, float dt)
    {
        c63SideT -= dt;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-4f) return dir;
        dir.Normalize();
        float look = 0.7f + bulk * 0.5f + spd * 0.5f;
        RaycastHit h;
        if (!C63Cast(p, dir, look, out h)) { if (c63SideT < -2f) c63Side = 0f; return dir; }
        Vector3 n = C63Flat(h.normal);
        if (n == Vector3.zero) return dir;
        Vector3 t = new Vector3(n.z, 0f, -n.x);
        if (c63Side == 0f || c63SideT < -2f) c63Side = Vector3.Dot(t, dir) >= 0f ? 1f : -1f;
        c63SideT = 1.2f;
        Vector3 nd = (t * c63Side + n * 0.18f).normalized;
        RaycastHit h2;
        if (C63Cast(p, nd, look * 0.8f, out h2))
        {
            c63Side = -c63Side;                                   // cornered: try the other way round
            nd = (t * c63Side + n * 0.18f).normalized;
        }
        c63Blocks++;
        return nd;
    }

    // the real step: never through a wall, slide along it; then keep it inside the world's perimeter
    Vector3 C63Move(Vector3 p, Vector3 np)
    {
        Vector3 d = np - p; d.y = 0f;
        float m = d.magnitude;
        if (m > 1e-4f)
        {
            Vector3 dir = d / m; RaycastHit h;
            const float skin = 0.04f;
            if (C63Cast(p, dir, m + skin, out h))
            {
                float ok = Mathf.Max(0f, h.distance - skin);
                Vector3 q = p + dir * ok;
                Vector3 n = C63Flat(h.normal);
                Vector3 rest = d - dir * ok;
                rest -= n * Vector3.Dot(rest, n);
                float rm = rest.magnitude; RaycastHit h2;
                if (rm > 1e-3f && !C63Cast(q, rest / rm, rm + skin, out h2)) q += rest;
                np.x = q.x; np.z = q.z;
            }
        }
        return C63Clamp(p, np);
    }

    public Vector3 C63Clamp(Vector3 p, Vector3 np)
    {
        Vector3 c; float half;
        if (C63Box(world, out c, out half))
        {
            np.x = Mathf.Clamp(np.x, c.x - half, c.x + half);
            np.z = Mathf.Clamp(np.z, c.z - half, c.z + half);
        }
        else if (world == WorldId.House || world == WorldId.RealRoom)
        {
            // nothing solid under the next step (off the end of the floor) = stay
            if (!Physics.Raycast(new Vector3(np.x, p.y + 1.5f, np.z), Vector3.down, 6f, C63Mask, QueryTriggerInteraction.Ignore)) { np.x = p.x; np.z = p.z; }
        }
        return np;
    }

    // the froggy-walkable square of a world (centre, half size); false = no square (house / Real Room / space use the floor test)
    public static bool C63Box(WorldId w, out Vector3 c, out float half)
    {
        switch (w)
        {
            case WorldId.Ranch: c = Vector3.zero; half = Layout.Half - 4f; return true;
            case WorldId.Moon: c = Worlds.MoonO; half = 90f; return true;
            case WorldId.Mars: c = Worlds.MarsO; half = SurfaceHalf63; return true;
            case WorldId.Callisto: c = Worlds.CallistoO; half = SurfaceHalf63; return true;
        }
        c = Vector3.zero; half = 0f; return false;
    }
    public static float SurfaceHalf63 = 90f;

    // a timed-out walk step: true = done with it (snapped a short clear hop, or gave up), false = re-route and keep walking
    bool C63Timeout(Vector3 goal)
    {
        Vector3 p = transform.position;
        Vector3 g = new Vector3(goal.x, GroundAt(goal.x, goal.z), goal.z);
        if (!C63On) { transform.position = g; return true; }
        float d = new Vector2(g.x - p.x, g.z - p.z).magnitude;
        Vector3 up = Vector3.up * Mathf.Min(1f, height * 0.5f);
        bool clear = !Physics.Linecast(p + up, g + up, C63Mask, QueryTriggerInteraction.Ignore);
        if (d < 2.5f && clear) { transform.position = C63Clamp(p, g); c63Tries = 0; return true; }
        c63Tries++;
        c63Side = -c63Side; c63SideT = 2f;
        if (c63Tries >= 3)
        {
            Debug.Log("ROBOT63 " + robotName + " gave up a walk step " + d.ToString("0.0") + " m short (no teleport)");
            c63Tries = 0; return true;
        }
        return false;
    }

    // visit / jet-pack / pod arrival spot: inside the perimeter and not inside a wall / vehicle
    public Vector3 C63Spot(Vector3 s, Vector3 lead)
    {
        if (!C63On) return s;
        s = C63Clamp(s, s);
        float r = C63R + 0.1f, lift = Mathf.Min(0.42f, height * 0.25f) + r;
        for (int i = 0; i < 17; i++)
        {
            Vector3 q = s;
            if (i > 0)
            {
                float a = i * 0.785f, rad = 1.5f + (i / 8) * 1.8f;
                q = lead + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * (2.5f + rad);
                q = C63Clamp(q, q);
            }
            q.y = GroundAt(q.x, q.z);
            if (!Physics.CheckCapsule(q + Vector3.up * lift, q + Vector3.up * Mathf.Max(lift, height - r), r, C63Mask, QueryTriggerInteraction.Ignore) || i == 16)
            { if (i > 0) Debug.Log("ROBOT63 " + robotName + " arrival spot moved " + Vector3.Distance(new Vector3(s.x, 0, s.z), new Vector3(q.x, 0, q.z)).ToString("0.0") + " m (was in a wall)"); return i == 16 ? s : q; }
        }
        return s;
    }

    // demo hooks (GameFfu63)
    public void Demo63Go(WorldId w, Vector3 from, Vector3 to)
    {
        AbortPlan(); if (visit != null) EndVisit(false, true);
        SetWorld(w); flying = false; yOver = float.NaN;
        from.y = GroundAt(from.x, from.z);
        if (w == WorldId.House) from.y = RayY(from.x, from.z, from.y + 1.5f);
        transform.position = from;
        cmd = "go"; target = to; c63Goal = to; c63Tries = 0; c63Blocks = 0; c63Side = 0f;
        status = "demo63 walking";
    }
    public float Demo63Dist { get { return new Vector2(target.x - transform.position.x, target.z - transform.position.z).magnitude; } }
}
