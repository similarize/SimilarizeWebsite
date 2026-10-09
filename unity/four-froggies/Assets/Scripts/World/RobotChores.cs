using System.Collections.Generic;
using UnityEngine;

// ffu12: James's ranch robots are always busy.
//  * Wall charging jacks (2 inside the garage, 2 on the garage's west wall outside, 2 on the house front wall under the
//    porch roof). A robot walks to a free jack when its battery is low (or tops up when no chore is free), backs onto the
//    floor pad, plugs a cable in (socket + robot chest lamp pulse green, quiet hum) and charges.
//  * Chores otherwise, each with a visible prop: sweep the porch (broom), vacuum the garage (upright vacuum), haul crates
//    (supply pile -> shed pad), haul hay (hay stack -> pasture gate), pick up litter (cans / balls / toys / tools that
//    respawn around the yard -> wheelie bin), mow the west lawn (push mower), rake leaf piles (rake; piles blow back),
//    water the porch flowers (watering can).
//  * Brain = a tiny plan runner per robot: battery -> pick chore -> list of steps (route walk / work walk / act in place
//    / charge) -> next. Routes go around the house, garage, pens + pasture (corner waypoints) and in through the
//    garage's bay-0 door / the porch steps. Moving robots steer apart from each other, frogs and vehicles.
//  * Cheap: no physics, no colliders, a few dozen distance checks per robot per frame; plans are built once per chore.
public static class Chores
{
    public const int Sweep = 0, Vacuum = 1, Crates = 2, Hay = 3, Litter = 4, Mow = 5, Rake = 6, Water = 7, Count = 8;
    public static readonly string[] Names = { "Sweep porch", "Vacuum garage", "Haul crates", "Haul hay", "Pick up litter", "Mow lawn", "Rake leaves", "Water flowers" };
    public static readonly string[] Doing = { "sweeping porch", "vacuuming garage", "hauling crates", "hauling hay", "picking up litter", "mowing lawn", "raking leaves", "watering flowers" };
    public static readonly int[] Cap = { 1, 1, 2, 1, 2, 1, 1, 1 };
    public static readonly float[] Weight = { 1f, 1f, 1.2f, 0.8f, 1.4f, 1f, 1f, 0.9f };
}

public class ChargeJack
{
    public string label;
    public Vector3 socket, normal, stand;
    public float faceYaw;
    public Robot user;            // claimed (walking there or plugged in)
    public Material glow, pad;
    public Transform cable;
    public int shown = -1;        // 0 free, 1 claimed, 2 charging
}

public class LitterItem { public Transform t; public bool active; public Robot claim; public float respawn; }
public class LeafPile { public Transform t; public float amount; public bool active; public Robot claim; public float respawn; }

// where robots may walk + how they get there
public static class RobotNav
{
    public static readonly Rect GarageIn = new Rect(-28.4f, 13.6f, 52.8f, 17.6f);
    public static readonly Rect Porch = new Rect(-53.6f, 11f, 23.2f, 7f);
    public static readonly Vector3 DoorOut = new Vector3(-26.4f, 0f, 34.5f), DoorIn = new Vector3(-26.4f, 0f, 27.5f);
    public static readonly Vector3 StepOut = new Vector3(-42f, 0f, 21.2f), StepIn = new Vector3(-42f, 0f, 15.6f);
    // route-around obstacles (x, z), a little bigger than the real thing
    static readonly Rect[] Obst =
    {
        new Rect(-65.4f, -19.4f, 46.8f, 30.8f),   // house (porch excluded)
        new Rect(-29.4f, 12.6f, 54.8f, 18.8f),    // garage
        new Rect(-62.4f, 17.6f, 9.8f, 7.8f),      // yard dog run
        new Rect(-74.4f, -36.4f, 10.8f, 8.8f),    // backyard animal yard
        new Rect(-74.4f, -14.4f, 7.8f, 6.8f),     // lizard terrace
        new Rect(-50.4f, -34.9f, 16.8f, 9.8f),    // pool
        new Rect(-171f, 85f, 58f, 62f),           // pasture + barn
    };
    // solid for walking (manual driving + avoidance pushes): house, the garage's three walls, the barn
    static readonly Rect[] Walls =
    {
        new Rect(-65f, -19f, 46f, 30f),
        new Rect(-29.05f, 13f, 0.6f, 18f), new Rect(24.45f, 13f, 0.6f, 18f), new Rect(-29f, 12.95f, 54f, 0.6f),
        new Rect(-166.2f, 131.3f, 12.4f, 9.4f),
    };

    public static bool InGarage(Vector3 p) { return GarageIn.Contains(new Vector2(p.x, p.z)); }
    public static bool InPorch(Vector3 p) { return Porch.Contains(new Vector2(p.x, p.z)); }

    public static bool Blocked(float x, float z)
    {
        var v = new Vector2(x, z);
        for (int i = 0; i < Walls.Length; i++) if (Walls[i].Contains(v)) return true;
        return Layout.PondQ(x, z) < 0.97f;
    }

    public static float Floor(float x, float z)
    {
        float g = Ranch.GY(x, z);
        return Porch.Contains(new Vector2(x, z)) ? Mathf.Max(g, 0.4f) : g;
    }

    public static Vector3 G(float x, float z) { return new Vector3(x, Floor(x, z), z); }

    public static void Route(Vector3 from, Vector3 to, List<Vector3> path)
    {
        path.Clear();
        bool fg = InGarage(from), tg = InGarage(to), fp = InPorch(from), tp = InPorch(to);
        if ((fg && tg) || (fp && tp)) { path.Add(to); return; }
        Vector3 a = from;
        if (fg) { path.Add(DoorIn); path.Add(DoorOut); a = DoorOut; }
        else if (fp) { path.Add(StepIn); path.Add(StepOut); a = StepOut; }
        Vector3 b = tg ? DoorOut : tp ? StepOut : to;
        Around(a, b, path);
        if (tg) { path.Add(DoorOut); path.Add(DoorIn); }
        else if (tp) { path.Add(StepOut); path.Add(StepIn); }
        path.Add(to);
    }

    static void Around(Vector3 a, Vector3 b, List<Vector3> path)
    {
        for (int it = 0; it < 5; it++)
        {
            int hit = -1; float best = 2f;
            for (int k = 0; k < Obst.Length; k++)
            {
                Rect r = Obst[k];
                if (r.Contains(new Vector2(a.x, a.z)) || r.Contains(new Vector2(b.x, b.z))) continue;
                float t;
                if (SegHits(a, b, r, out t) && t < best) { best = t; hit = k; }
            }
            if (hit < 0) return;
            Rect e = Obst[hit];
            e.xMin -= 1.8f; e.yMin -= 1.8f; e.xMax += 1.8f; e.yMax += 1.8f;
            Vector3[] cs = { new Vector3(e.xMin, 0f, e.yMin), new Vector3(e.xMax, 0f, e.yMin), new Vector3(e.xMin, 0f, e.yMax), new Vector3(e.xMax, 0f, e.yMax) };
            Vector3 pick = Vector3.zero; float pd = 1e9f; bool any = false;
            foreach (var c in cs)
            {
                if (Flat(c - a) < 0.6f) continue;
                float t;
                if (SegHits(a, c, Obst[hit], out t)) continue;
                float d = Flat(c - a) + Flat(b - c);
                if (d < pd) { pd = d; pick = c; any = true; }
            }
            if (!any) return;
            path.Add(pick);
            a = pick;
        }
    }

    public static float Flat(Vector3 v) { return Mathf.Sqrt(v.x * v.x + v.z * v.z); }

    // segment a-b (x, z) vs rect (Liang-Barsky); t = entry fraction
    static bool SegHits(Vector3 a, Vector3 b, Rect r, out float t)
    {
        float t0 = 0f, t1 = 1f, dx = b.x - a.x, dz = b.z - a.z;
        t = 0f;
        float[] p = { -dx, dx, -dz, dz }, q = { a.x - r.xMin, r.xMax - a.x, a.z - r.yMin, r.yMax - a.z };
        for (int i = 0; i < 4; i++)
        {
            if (Mathf.Abs(p[i]) < 1e-6f) { if (q[i] < 0f) return false; continue; }
            float u = q[i] / p[i];
            if (p[i] < 0f) { if (u > t1) return false; if (u > t0) t0 = u; }
            else { if (u < t0) return false; if (u < t1) t1 = u; }
        }
        t = t0;
        return true;
    }
}

public class RanchJobs : MonoBehaviour
{
    public static RanchJobs I;
    public readonly List<ChargeJack> jacks = new List<ChargeJack>();
    public readonly List<LitterItem> litter = new List<LitterItem>();
    public readonly List<LeafPile> leaves = new List<LeafPile>();
    public readonly int[] busy = new int[Chores.Count];
    public static readonly Vector2 BinP = new Vector2(-31.8f, 36.5f), CratePile = new Vector2(-37f, 45f), CrateDrop = new Vector2(-84f, 46f),
        HayPile = new Vector2(-99f, 92f), HayDrop = new Vector2(-109.5f, 116f);
    public static readonly Rect MowArea = new Rect(-106f, 30f, 20f, 24f);
    public int mowLane;
    readonly List<GameObject> crateStack = new List<GameObject>(), hayStack = new List<GameObject>();
    int crates, hay;
    static readonly Rect[] LitterAreas = { new Rect(-80f, 33f, 44f, 28f), new Rect(-26f, 35f, 40f, 10f), new Rect(-96f, -12f, 20f, 38f) };
    static readonly Rect LeafArea = new Rect(-82f, 36f, 32f, 16f);
    static readonly Color[] JackIdle = { new Color(0.25f, 0.6f, 1f), new Color(1f, 0.75f, 0.2f) };
    static readonly Color PadIdle = new Color(0.95f, 0.78f, 0.1f);

    public static void Create()
    {
        var go = new GameObject("RanchJobs");
        I = go.AddComponent<RanchJobs>();
        I.Build();
    }

    void Build()
    {
        Transform root = transform;
        Transform st = new GameObject("Static").transform;     // merged at the end (draw calls)
        st.SetParent(root, false);
        float gx = Ranch.GY(-28.4f, 18f);
        Jack(st, "garage (inside)", new Vector3(-28.43f, gx + 1.05f, 14.8f), Vector3.right);
        Jack(st, "garage (inside)", new Vector3(-28.43f, gx + 1.05f, 17.4f), Vector3.right);
        Jack(st, "garage wall", new Vector3(-29.07f, gx + 1.05f, 18.3f), Vector3.left);
        Jack(st, "garage wall", new Vector3(-29.07f, gx + 1.05f, 24.6f), Vector3.left);
        Jack(st, "house porch", new Vector3(-49f, 1.45f, 11.08f), Vector3.forward);
        Jack(st, "house porch", new Vector3(-35f, 1.45f, 11.08f), Vector3.forward);
        Ranch.Sign(new Vector3(-29.35f, gx + 3.3f, 21.45f), -90f, "ROBOT CHARGING", new Color(0.1f, 0.35f, 0.55f), 4.6f, 0.75f);

        // wheelie bin for litter
        Vector3 bp = RobotNav.G(BinP.x, BinP.y);
        Material binM = Mats.Paint(new Color(0.15f, 0.45f, 0.22f), 0.5f), dark = Mats.Lit(new Color(0.08f, 0.08f, 0.09f));
        Mats.Prim(PrimitiveType.Cube, st, bp + new Vector3(0f, 0.55f, 0f), new Vector3(0.75f, 1.1f, 0.8f), binM, true);
        Mats.Prim(PrimitiveType.Cube, st, bp + new Vector3(0f, 1.13f, -0.05f), new Vector3(0.82f, 0.07f, 0.9f), binM);
        for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cylinder, st, bp + new Vector3(s * 0.3f, 0.12f, -0.42f), new Vector3(0.24f, 0.04f, 0.24f), new Vector3(0f, 0f, 90f), dark);
        // crate supply pile (by the garage) + shed pad drop stack (north-west lawn)
        Material crateM = Mats.TexTint("LB/wood", new Color(0.85f, 0.65f, 0.4f), 0.1f, 1f);
        Vector3 cp = RobotNav.G(CratePile.x, CratePile.y);
        for (int i = 0; i < 6; i++)
            Mats.Prim(PrimitiveType.Cube, st, cp + new Vector3((i % 3 - 1) * 0.68f, 0.31f + (i / 3) * 0.62f, (i / 3) * 0.1f), Vector3.one * 0.6f, crateM);
        Vector3 cd = RobotNav.G(CrateDrop.x, CrateDrop.y);
        Mats.Prim(PrimitiveType.Cube, st, cd + new Vector3(0f, 0.05f, 0f), new Vector3(3.2f, 0.1f, 2.4f), Mats.Lit(new Color(0.5f, 0.5f, 0.52f)));
        for (int i = 0; i < 6; i++)
        {
            var g = Mats.Prim(PrimitiveType.Cube, root, cd + new Vector3((i % 3 - 1) * 0.7f, 0.41f + (i / 3) * 0.62f, 0f), Vector3.one * 0.6f, crateM);
            g.SetActive(false); crateStack.Add(g);
        }
        // hay stack + drop by the pasture gate
        Material hayM = Mats.Lit(new Color(0.86f, 0.73f, 0.36f));
        Vector3 hp = RobotNav.G(HayPile.x, HayPile.y);
        for (int i = 0; i < 5; i++)
            Mats.Prim(PrimitiveType.Cube, st, hp + new Vector3((i % 3 - 1) * 0.95f + (i / 3) * 0.45f, 0.24f + (i / 3) * 0.47f, 0f), new Vector3(0.9f, 0.46f, 0.5f), hayM);
        Vector3 hd = RobotNav.G(HayDrop.x, HayDrop.y);
        for (int i = 0; i < 4; i++)
        {
            var g = Mats.Prim(PrimitiveType.Cube, root, hd + new Vector3(0f, 0.24f + (i / 2) * 0.47f, (i % 2 - 0.5f) * 0.55f), new Vector3(0.9f, 0.46f, 0.5f), hayM);
            g.SetActive(false); hayStack.Add(g);
        }
        // litter: cans, balls, toy blocks, tools
        Color[] lc = { new Color(0.85f, 0.15f, 0.12f), new Color(0.2f, 0.45f, 0.95f), new Color(1f, 0.82f, 0.15f), new Color(0.6f, 0.62f, 0.66f), new Color(0.25f, 0.75f, 0.35f) };
        for (int i = 0; i < 10; i++)
        {
            var it = new LitterItem();
            var t = new GameObject("Litter").transform;
            t.SetParent(root, false);
            int k = i % 5;
            if (k == 0) Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0.16f, 0f), new Vector3(0.18f, 0.16f, 0.18f), Mats.Shiny(lc[i % lc.Length]));
            else if (k == 1) Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 0.19f, 0f), Vector3.one * 0.38f, Mats.Paint(lc[(i + 1) % lc.Length], 0.6f));
            else if (k == 2) Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.15f, 0f), Vector3.one * 0.3f, new Vector3(0f, 25f, 0f), Mats.Paint(lc[(i + 2) % lc.Length], 0.5f));
            else if (k == 3) { Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.04f, 0f), new Vector3(0.5f, 0.06f, 0.1f), Mats.Steel(lc[3])); Mats.Prim(PrimitiveType.Cube, t, new Vector3(0.24f, 0.04f, 0f), new Vector3(0.12f, 0.06f, 0.2f), Mats.Steel(lc[3])); }
            else Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0.08f, 0f), new Vector3(0.14f, 0.2f, 0.14f), new Vector3(0f, 0f, 90f), Mats.Paint(new Color(0.3f, 0.8f, 0.45f), 0.9f));
            it.t = t;
            t.localScale = Vector3.one * 1.45f;   // ffu14: easier to spot (was tiny next to the robots)
            Mats.NoShadows(t.gameObject);
            Respawn(it);
            litter.Add(it);
        }
        // leaf piles
        Material l1 = Mats.Lit(new Color(0.8f, 0.42f, 0.12f)), l2 = Mats.Lit(new Color(0.65f, 0.3f, 0.1f)), l3 = Mats.Lit(new Color(0.85f, 0.65f, 0.2f));
        for (int i = 0; i < 4; i++)
        {
            var lp = new LeafPile();
            var t = new GameObject("Leaf pile").transform;
            t.SetParent(root, false);
            Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 0.28f, 0f), new Vector3(1.5f, 0.9f, 1.3f), l1);
            Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0.45f, 0.12f, 0.3f), new Vector3(0.9f, 0.6f, 0.8f), l2);
            Mats.Prim(PrimitiveType.Sphere, t, new Vector3(-0.5f, 0.1f, -0.3f), new Vector3(0.8f, 0.55f, 0.7f), l3);
            Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0.1f, 0.62f, -0.1f), new Vector3(0.75f, 0.45f, 0.65f), l2);
            Mats.Prim(PrimitiveType.Sphere, t, new Vector3(-0.2f, 0.5f, 0.3f), new Vector3(0.6f, 0.4f, 0.5f), l3);
            for (int f = 0; f < 7; f++)
            {
                float a = f * 0.9f + i, rr = 1.1f + (f % 3) * 0.25f;
                Mats.Prim(PrimitiveType.Cube, t, new Vector3(Mathf.Cos(a) * rr, 0.02f, Mathf.Sin(a) * rr), new Vector3(0.18f, 0.015f, 0.12f), new Vector3(0f, f * 47f, 0f), f % 2 == 0 ? l1 : l3);
            }
            Mats.NoShadows(t.gameObject);
            lp.t = t;
            RespawnLeaves(lp);
            leaves.Add(lp);
        }
        MeshMerge.Merge(st, false);
        Debug.Log("RanchJobs: " + jacks.Count + " charging jacks, " + litter.Count + " litter items, " + leaves.Count + " leaf piles");
    }

    void Jack(Transform root, string label, Vector3 socket, Vector3 n)
    {
        var j = new ChargeJack { label = label, socket = socket, normal = n };
        j.faceYaw = Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg;
        Vector3 st = socket + n * 0.95f;
        j.stand = RobotNav.G(st.x, st.z);
        Quaternion q = Quaternion.LookRotation(n);
        var go = new GameObject("Charge jack " + label).transform;
        go.SetParent(root, false);
        go.SetPositionAndRotation(socket, q);
        Material plate = Mats.Paint(new Color(0.2f, 0.21f, 0.24f), 0.6f);
        Mats.Prim(PrimitiveType.Cube, go, Vector3.zero, new Vector3(0.46f, 0.66f, 0.07f), plate);
        j.glow = new Material(Mats.Unlit(Color.white));
        j.glow.color = JackIdle[0];
        var gl = Mats.Prim(PrimitiveType.Cube, go, new Vector3(0f, 0.1f, 0.04f), new Vector3(0.3f, 0.3f, 0.03f), j.glow);
        Mats.Prim(PrimitiveType.Cube, go, new Vector3(0f, 0.1f, 0.055f), new Vector3(0.1f, 0.1f, 0.02f), Mats.Lit(new Color(0.05f, 0.05f, 0.06f)));   // socket hole
        Mats.Prim(PrimitiveType.Cube, go, new Vector3(0f, -0.17f, 0.04f), new Vector3(0.06f, 0.15f, 0.02f), new Vector3(0f, 0f, 22f), Mats.Unlit(new Color(1f, 0.85f, 0.15f)));   // bolt
        float top = 2.9f - (socket.y - Ranch.GY(socket.x, socket.z));
        Mats.Prim(PrimitiveType.Cube, go, new Vector3(0f, 0.33f + top * 0.5f, -0.01f), new Vector3(0.07f, top, 0.05f), plate);   // conduit up the wall
        // floor pad: yellow border, dark centre
        Vector3 pad = j.stand;
        j.pad = new Material(Mats.Unlit(Color.white));
        j.pad.color = PadIdle;
        var pd = Mats.Prim(PrimitiveType.Cube, root, pad + Vector3.up * 0.02f, new Vector3(1.6f, 0.03f, 1.6f), j.pad);
        pd.transform.rotation = q;
        var pi = Mats.Prim(PrimitiveType.Cube, root, pad + Vector3.up * 0.035f, new Vector3(1.25f, 0.03f, 1.25f), Mats.Lit(new Color(0.16f, 0.17f, 0.19f)));
        pi.transform.rotation = q;
        // cable (hangs on the plate when free, stretched to the robot's back when charging)
        j.cable = Mats.Prim(PrimitiveType.Cylinder, transform, Vector3.zero, Vector3.one, Mats.Paint(new Color(0.95f, 0.45f, 0.08f), 0.5f)).transform;   // orange charging lead
        Mats.NoShadows(go.gameObject); Mats.NoShadows(j.cable.gameObject); Mats.NoShadows(pd); Mats.NoShadows(pi);
        HangCable(j);
        jacks.Add(j);
    }

    static void Stretch(Transform c, Vector3 a, Vector3 b, float thick)
    {
        Vector3 d = b - a;
        c.position = (a + b) * 0.5f;
        c.rotation = Quaternion.FromToRotation(Vector3.up, d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.up);
        c.localScale = new Vector3(thick, d.magnitude * 0.5f, thick);
    }

    static void HangCable(ChargeJack j)
    {
        Vector3 a = j.socket + j.normal * 0.06f + Vector3.up * 0.1f;
        Stretch(j.cable, a, a - Vector3.up * 0.6f + j.normal * 0.1f, 0.08f);
    }

    public ChargeJack ClaimJack(Robot r)
    {
        ChargeJack best = null; float bd = 1e9f;
        foreach (var j in jacks)
        {
            if (j.user != null && j.user != r) continue;
            float d = RobotNav.Flat(j.stand - r.transform.position);
            if (d < bd) { bd = d; best = j; }
        }
        if (best != null) best.user = r;
        return best;
    }

    public int PickChore(Robot r, int last)
    {
        float sum = 0f;
        float[] w = new float[Chores.Count];
        for (int c = 0; c < Chores.Count; c++)
        {
            if (c == last || busy[c] >= Chores.Cap[c]) continue;
            if (c == Chores.Litter && FreeLitter() < 1) continue;
            if (c == Chores.Rake && FreeLeaves() < 1) continue;
            w[c] = Chores.Weight[c]; sum += w[c];
        }
        if (sum <= 0f) return -1;
        float x = Random.value * sum;
        for (int c = 0; c < Chores.Count; c++) { x -= w[c]; if (w[c] > 0f && x <= 0f) return c; }
        return -1;
    }

    int FreeLitter() { int n = 0; foreach (var l in litter) if (l.active && l.claim == null) n++; return n; }
    int FreeLeaves() { int n = 0; foreach (var l in leaves) if (l.active && l.claim == null) n++; return n; }

    public LitterItem ClaimLitter(Robot r, Vector3 near)
    {
        LitterItem best = null; float bd = 1e9f;
        foreach (var l in litter)
        {
            if (!l.active || l.claim != null) continue;
            float d = RobotNav.Flat(l.t.position - near);
            if (d < bd) { bd = d; best = l; }
        }
        if (best != null) best.claim = r;
        return best;
    }

    public LeafPile ClaimLeaves(Robot r, Vector3 near)
    {
        LeafPile best = null; float bd = 1e9f;
        foreach (var l in leaves)
        {
            if (!l.active || l.claim != null) continue;
            float d = RobotNav.Flat(l.t.position - near);
            if (d < bd) { bd = d; best = l; }
        }
        if (best != null) best.claim = r;
        return best;
    }

    static Vector3 RandomSpot(Rect[] areas, float clear)
    {
        for (int k = 0; k < 30; k++)
        {
            Rect a = areas[Random.Range(0, areas.Length)];
            float x = Random.Range(a.xMin, a.xMax), z = Random.Range(a.yMin, a.yMax);
            if (RobotNav.Blocked(x, z) || RobotNav.InGarage(new Vector3(x, 0f, z)) || Layout.PondQ(x, z) < 1.15f) continue;
            if (Mathf.Abs(x - BinP.x) < clear && Mathf.Abs(z - BinP.y) < clear) continue;
            return RobotNav.G(x, z);
        }
        return RobotNav.G(-60f, 40f);
    }

    public void Respawn(LitterItem it)
    {
        if (it.t.parent != transform) it.t.SetParent(transform, true);
        it.t.position = RandomSpot(LitterAreas, 3f);
        it.t.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        it.t.gameObject.SetActive(true);
        it.active = true; it.claim = null;
    }

    public void Binned(LitterItem it)
    {
        it.t.SetParent(transform, true);
        it.t.gameObject.SetActive(false);
        it.active = false; it.claim = null;
        it.respawn = Random.Range(6f, 14f);
        Sfx.PlayAt(Sfx.Clank, RobotNav.G(BinP.x, BinP.y), 0.4f, 30f, 1.3f);
    }

    public void Dropped(LitterItem it, Vector3 at)
    {
        it.t.SetParent(transform, true);
        it.t.position = RobotNav.G(at.x, at.z);
        it.t.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        it.claim = null;
    }

    void RespawnLeaves(LeafPile lp)
    {
        lp.t.position = RandomSpot(new[] { LeafArea }, 0f);
        lp.amount = 1f; lp.active = true; lp.claim = null;
        lp.t.localScale = Vector3.one;
        lp.t.gameObject.SetActive(true);
    }

    public void Raked(LeafPile lp, float amount)
    {
        lp.amount = Mathf.Max(0f, lp.amount - amount);
        float s = 0.25f + 0.75f * lp.amount;
        lp.t.localScale = new Vector3(s, s, s);
        if (lp.amount <= 0f) { lp.active = false; lp.claim = null; lp.t.gameObject.SetActive(false); lp.respawn = Random.Range(12f, 24f); }
    }

    public void AddCrate()
    {
        if (crates >= crateStack.Count) { foreach (var g in crateStack) g.SetActive(false); crates = 0; FX.Dust(RobotNav.G(CrateDrop.x, CrateDrop.y) + Vector3.up * 0.5f, 1f); }
        crateStack[crates++].SetActive(true);
    }

    public void AddHay()
    {
        if (hay >= hayStack.Count) { foreach (var g in hayStack) g.SetActive(false); hay = 0; }
        hayStack[hay++].SetActive(true);
    }

    public Vector3 JackPlug(ChargeJack j) { return j.socket + j.normal * 0.06f + Vector3.up * 0.1f; }

    void Update()
    {
        float dt = Time.deltaTime, t = Time.time;
        foreach (var l in litter) if (!l.active && l.claim == null && l.t.parent == transform) { l.respawn -= dt; if (l.respawn <= 0f) Respawn(l); }
        foreach (var l in leaves) if (!l.active) { l.respawn -= dt; if (l.respawn <= 0f) RespawnLeaves(l); }
        foreach (var j in jacks)
        {
            bool chg = j.user != null && j.user.Charging && j.user.jack == j;
            int st = chg ? 2 : j.user != null ? 1 : 0;
            if (chg)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(t * 4.5f);
                j.glow.color = Color.Lerp(new Color(0.1f, 0.55f, 0.2f), new Color(0.55f, 1f, 0.6f), k);
                j.pad.color = Color.Lerp(new Color(0.1f, 0.6f, 0.2f), new Color(0.45f, 1f, 0.45f), k);
                Stretch(j.cable, JackPlug(j), j.user.PlugPoint, 0.09f);
            }
            else if (st != j.shown) { j.glow.color = JackIdle[st]; j.pad.color = PadIdle; HangCable(j); }
            j.shown = st;
        }
    }

    // ---------------- tools ----------------
    // built per robot on first use, under the robot's body, in metres in the robot's frame (+z forward)
    public static Transform MakeTool(Robot r, int chore, out Transform head)
    {
        float h = r.height;
        Vector3 hand = new Vector3(0f, h * 0.53f, h * 0.27f + 0.12f);
        var root = Mats.Node(r.Body, "Tool " + chore, hand);
        head = root;
        Material wood = Mats.TexTint("LB/wood", new Color(0.9f, 0.75f, 0.55f), 0.1f, 1f), dark = Mats.Lit(new Color(0.08f, 0.08f, 0.09f));
        Vector3 floor = new Vector3(0f, 0.05f - hand.y, 0.9f);     // ground ahead, relative to the hands
        switch (chore)
        {
            case Chores.Sweep:
                Stick(root, Vector3.zero, floor + new Vector3(0f, 0.12f, 0f), 0.05f, wood);
                head = Mats.Prim(PrimitiveType.Cube, root, floor + new Vector3(0f, 0.08f, 0f), new Vector3(0.62f, 0.14f, 0.14f), Mats.Paint(new Color(0.85f, 0.2f, 0.15f), 0.4f)).transform;
                Mats.Prim(PrimitiveType.Cube, root, floor + new Vector3(0f, -0.01f, 0.02f), new Vector3(0.6f, 0.08f, 0.1f), Mats.Lit(new Color(0.9f, 0.8f, 0.45f)));
                break;
            case Chores.Rake:
                Stick(root, Vector3.zero, floor + new Vector3(0f, 0.06f, 0.1f), 0.045f, wood);
                head = Mats.Prim(PrimitiveType.Cube, root, floor + new Vector3(0f, 0.06f, 0.12f), new Vector3(0.7f, 0.05f, 0.05f), Mats.Steel(new Color(0.6f, 0.62f, 0.65f))).transform;
                for (int i = 0; i < 7; i++) Mats.Prim(PrimitiveType.Cube, root, floor + new Vector3(-0.3f + i * 0.1f, 0.0f, 0.16f), new Vector3(0.025f, 0.12f, 0.025f), Mats.Steel(new Color(0.6f, 0.62f, 0.65f)));
                break;
            case Chores.Mow:
                {
                    Vector3 deck = floor + new Vector3(0f, 0.2f, 0.25f);
                    head = Mats.Prim(PrimitiveType.Cube, root, deck, new Vector3(0.62f, 0.22f, 0.7f), Mats.Paint(new Color(0.85f, 0.15f, 0.12f), 0.7f)).transform;
                    Mats.Prim(PrimitiveType.Cylinder, root, deck + new Vector3(0f, 0.17f, 0.05f), new Vector3(0.3f, 0.09f, 0.3f), Mats.Lit(new Color(0.15f, 0.15f, 0.16f)));
                    for (int sx = -1; sx <= 1; sx += 2)
                        for (int sz = -1; sz <= 1; sz += 2)
                            Mats.Prim(PrimitiveType.Cylinder, root, deck + new Vector3(sx * 0.34f, -0.08f, sz * 0.26f), new Vector3(0.2f, 0.035f, 0.2f), new Vector3(0f, 0f, 90f), dark);
                    for (int sx = -1; sx <= 1; sx += 2) Stick(root, new Vector3(sx * 0.2f, 0f, 0f), deck + new Vector3(sx * 0.22f, 0.08f, -0.3f), 0.035f, dark);
                    Stick(root, new Vector3(-0.22f, 0f, 0f), new Vector3(0.22f, 0f, 0f), 0.04f, dark);
                    break;
                }
            case Chores.Vacuum:
                {
                    Vector3 nozzle = floor + new Vector3(0f, 0.06f, -0.15f);
                    head = Mats.Prim(PrimitiveType.Cube, root, nozzle, new Vector3(0.42f, 0.1f, 0.24f), Mats.Paint(new Color(0.45f, 0.2f, 0.75f), 0.8f)).transform;
                    Vector3 mid = Vector3.Lerp(Vector3.zero, nozzle, 0.55f);
                    Mats.Prim(PrimitiveType.Cylinder, root, mid, new Vector3(0.22f, 0.28f, 0.22f), Quaternion.FromToRotation(Vector3.up, -nozzle.normalized).eulerAngles, Mats.Paint(new Color(0.2f, 0.85f, 0.85f), 0.8f));
                    Stick(root, Vector3.zero, nozzle, 0.04f, dark);
                    Mats.Prim(PrimitiveType.Cube, root, nozzle + new Vector3(0f, 0.06f, 0.1f), new Vector3(0.3f, 0.025f, 0.02f), Mats.Unlit(new Color(0.5f, 1f, 1f)));
                    break;
                }
            case Chores.Water:
                {
                    var can = Mats.Node(root, "Can", new Vector3(0f, -0.05f, 0.12f));
                    Mats.Prim(PrimitiveType.Cylinder, can, Vector3.zero, new Vector3(0.26f, 0.15f, 0.26f), Mats.Paint(new Color(0.2f, 0.6f, 0.3f), 0.7f));
                    Stick(can, new Vector3(0f, -0.05f, 0.1f), new Vector3(0f, 0.12f, 0.42f), 0.04f, Mats.Paint(new Color(0.2f, 0.6f, 0.3f), 0.7f));
                    Stick(can, new Vector3(0f, 0.16f, -0.08f), new Vector3(0f, 0.16f, 0.08f), 0.03f, dark);
                    head = Mats.Node(can, "Spout", new Vector3(0f, 0.12f, 0.44f));
                    break;
                }
        }
        Mats.NoShadows(root.gameObject);
        return root;
    }

    public static Transform Stick(Transform p, Vector3 a, Vector3 b, float thick, Material m)
    {
        var g = Mats.Prim(PrimitiveType.Cylinder, p, (a + b) * 0.5f, new Vector3(thick, (b - a).magnitude * 0.5f, thick), m);
        g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
        return g.transform;
    }

    public static GameObject MakeCargo(Robot r, int chore)
    {
        var n = r.CarryNode;
        GameObject g;
        if (chore == Chores.Hay) g = Mats.Prim(PrimitiveType.Cube, n, new Vector3(0f, 0f, 0.1f), new Vector3(0.9f, 0.46f, 0.5f), Mats.Lit(new Color(0.86f, 0.73f, 0.36f)));
        else g = Mats.Prim(PrimitiveType.Cube, n, new Vector3(0f, 0f, 0.1f), Vector3.one * 0.6f, Mats.TexTint("LB/wood", new Color(0.85f, 0.65f, 0.4f), 0.1f, 1f));
        Mats.NoShadows(g);
        g.SetActive(false);
        return g;
    }
}
