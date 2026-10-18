using System.Collections.Generic;
using UnityEngine;

// ffu32 GARDEN RETREAT (Bill: "a little garden somewhere. A resting hammock area where each froggy can get into a hammock
// and swing. And a little campfire that the froggy can start, and sit around on some swings. A really nice little
// atmosphere."). A cozy corner on the ranch, placed by a fixed search for clear, gently sloped ground away from the
// tracks, the treehouses, the pads, the house / garage, the pasture and the mech rows (same spot every load):
//  * stone stepping-stone path from an arbor entrance, flower beds, a veggie patch, shrubs, a bird bath
//  * 4 hammocks on posts (GardenSeat Hammock): lie in one sprawled out, it swings, push it with the stick, jump out
//  * a campfire ring (Campfire): A / E / tap lights it (spark -> flames + crackle + one warm light, smoke, embers), again
//    douses it; 4 porch swings around it facing the fire (GardenSeat Swing)
//  * string lights over the fire circle and the hammocks + lanterns along the path (glowing bulbs, no extra lights)
//  * soft birdsong + crickets near the garden (GardenAudio, 2D loops faded by distance to the nearest player froggy)
//  * AI froggies wander over now and then to lie in a hammock or swing by the fire (Frog.GardenThink, FrogGarden.cs)
// Local frame: entrance at -z (faces the middle of the ranch), fire east of centre, hammocks west.
public class Garden : MonoBehaviour
{
    public static Garden I;
    public static bool Built { get { return I != null; } }
    public Vector3 C;                 // centre (world, on the ground)
    public float Yaw;
    public Campfire fire;
    public readonly List<GardenSeat> hammocks = new List<GardenSeat>(), swings = new List<GardenSeat>();
    AudioSource birds, crickets;
    public const float HalfX = 16f, HalfZ = 13f;
    static readonly Dictionary<GardenSeat, Frog> claims = new Dictionary<GardenSeat, Frog>();
    public static bool Claimed(GardenSeat s) { Frog f; return claims.TryGetValue(s, out f) && f != null && f.vehicle != s && GardenClaimLive(f, s); }
    static bool GardenClaimLive(Frog f, GardenSeat s) { return f.gardenSeat == s; }
    public static void Claim(GardenSeat s, Frog f) { claims[s] = f; }

    public Vector3 W(float x, float z) { return C + Quaternion.Euler(0f, Yaw, 0f) * new Vector3(x, 0f, z); }
    public Vector3 WG(float x, float z, float up = 0f) { Vector3 p = W(x, z); p.y = Ranch.GY(p.x, p.z) + up; return p; }
    public Vector3 Fwd { get { return Quaternion.Euler(0f, Yaw, 0f) * Vector3.forward; } }
    public Vector3 FirePos { get { return fire != null ? fire.transform.position : WG(5f, 1f); } }

    static readonly Vector2[] Prefer = { new Vector2(60f, 32f), new Vector2(-100f, -112f), new Vector2(25f, -155f), new Vector2(-150f, -95f) };

    public static void Build(List<Vector2> trees)
    {
        if (I != null) return;
        Physics.SyncTransforms();
        Vector2 site; bool ok = false; site = Prefer[0];
        foreach (Vector2 pref in Prefer)
        {
            var cands = new List<Vector2>();
            for (int dx = -6; dx <= 6; dx++)
                for (int dz = -6; dz <= 6; dz++) cands.Add(pref + new Vector2(dx * 6f, dz * 6f));
            cands.Sort((a, b) => (a - pref).sqrMagnitude.CompareTo((b - pref).sqrMagnitude));
            foreach (var c in cands) if (Clear(c, trees)) { site = c; ok = true; break; }
            if (ok) break;
        }
        if (!ok) Debug.Log("Garden: no fully clear site - using " + site.ToString("0"));
        var go = new GameObject("Garden retreat");
        I = go.AddComponent<Garden>();
        I.C = new Vector3(site.x, Ranch.GY(site.x, site.y), site.y);
        // entrance (-z) faces the middle of the ranch
        Vector2 toMid = -site;
        I.Yaw = Mathf.Atan2(-toMid.x, -toMid.y) * Mathf.Rad2Deg;
        I.Yaw = Mathf.Round(I.Yaw / 15f) * 15f;
        go.transform.position = I.C;
        go.transform.rotation = Quaternion.Euler(0f, I.Yaw, 0f);
        I.ClearTrees();
        I.Make();
        Debug.Log("Garden: built at " + I.C.ToString("0.0") + " yaw " + I.Yaw.ToString("0") + " (" + I.hammocks.Count + " hammocks, " + I.swings.Count + " swings, campfire)");
    }

    // ffu32b: hide any tree / rock inside the garden circle (the Prefer site at (60, 32) had two pines inside), the way
    // TrailerBarn.ClearTrees does it
    void ClearTrees()
    {
        int n = 0;
        foreach (var col in Physics.OverlapSphere(C + Vector3.up * 3f, 21.5f))
        {
            if (col is TerrainCollider || col.isTrigger || col.attachedRigidbody != null) continue;
            Transform t = col.transform;
            while (t.parent != null && t.parent.name != "RanchStatic" && t.parent.parent != null) t = t.parent;
            if (t.name.StartsWith("Tree") || t.name.StartsWith("Rock") || col.name.StartsWith("TreeCol") || col.name.StartsWith("RockCol"))
            {
                if (!t.gameObject.activeSelf) continue;
                // ffu32c: trees / rocks are Breakables drawn through BreakBatch (their own renderers are disabled), so
                // SetActive alone only hid the collider. Mark the piece broken (state 1, never queued for regen: it is not
                // in Destruct's active list) and re-flush its batch cells so it drops out of the combined mesh too.
                var b = t.GetComponent<Breakable>();
                if (b != null) { b.state = 1; b.hp = 0f; BreakBatch.Dirty(b); }
                t.gameObject.SetActive(false); n++;
            }
        }
        if (n > 0) Debug.Log("Garden: cleared " + n + " trees / rocks inside the garden");
    }

    // rotation-agnostic check on a circle that covers the garden (r 21 m) + the samples inside it
    static bool Clear(Vector2 c, List<Vector2> trees)
    {
        const float R = 20.5f;
        if (Mathf.Abs(c.x) > 158f || Mathf.Abs(c.y) > 158f) return false;
        float y0 = Ranch.GY(c.x, c.y), lo = y0, hi = y0;
        for (int k = 0; k < 17; k++)
        {
            Vector2 p = k == 0 ? c : c + new Vector2(Mathf.Cos(k * 0.785f), Mathf.Sin(k * 0.785f)) * (k <= 8 ? R : R * 0.5f);
            if (Layout.Flatness(p.x, p.y) < 0.55f || Layout.PondQ(p.x, p.y) < 1.3f) return false;
            if (Layout.RoadDist(p.x, p.y) < Layout.TrackW * 0.5f + 4f) return false;
            if (Layout.LoopLinkDist(p.x, p.y) < 10f) return false;
            if (Worlds.StageEOn && RanchLife.Reserved(p.x, p.y)) return false;
            if (RobotNav.Blocked(p.x, p.y)) return false;
            float y = Ranch.GY(p.x, p.y); lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y);
        }
        if (hi - lo > 2.2f) return false;
        if ((c - Layout.HouseC).sqrMagnitude < 52f * 52f || (c - Layout.GarageC).sqrMagnitude < 48f * 48f) return false;
        if ((c - Layout.PadC).sqrMagnitude < 55f * 55f || (c - StorySet.PadXZ).sqrMagnitude < 45f * 45f) return false;
        if ((c - new Vector2(-80f, -60f)).sqrMagnitude < 40f * 40f) return false;           // robot mission pad
        if ((c - Layout.LoopC).sqrMagnitude < 45f * 45f) return false;
        if ((c - new Vector2(-95f, 26.6f)).sqrMagnitude < 32f * 32f) return false;          // riding-mower lawn
        if ((c - new Vector2(-44f, 28f)).sqrMagnitude < 40f * 40f) return false;            // robot line-up
        if ((c - RanchLife.Pasture.center).sqrMagnitude < 62f * 62f) return false;
        // ffu30 RC raceway (centre 140, 28; x 116..167, z 7..43 + bike line at 141, 2) and the ffu31 trailer barn (searched
        // round 140, 26, doors west): the whole east block is theirs
        if (c.x > 78f && c.y > -50f && c.y < 90f) return false;
        foreach (var s in Treehouse.Spots) if ((new Vector2(s.x, s.z) - c).sqrMagnitude < 34f * 34f) return false;
        foreach (var t in trees) if ((t - c).sqrMagnitude < (R + 1.5f) * (R + 1.5f)) return false;
        foreach (var col in Physics.OverlapBox(new Vector3(c.x, y0 + 3.5f, c.y), new Vector3(R, 3.2f, R)))
            if (!(col is TerrainCollider) && !col.isTrigger) return false;
        return true;
    }

    Transform decor;
    static Material M(Color c) { return Mats.Lit(c); }
    GameObject D(PrimitiveType t, float x, float z, float up, Vector3 scale, Material m, Vector3 euler = default(Vector3), bool col = false)
    {
        Vector3 p = WG(x, z, up);
        var g = Mats.Prim(t, decor, decor.InverseTransformPoint(p), scale, m, col);
        g.transform.localRotation = Quaternion.Euler(euler);
        return g;
    }

    void Make()
    {
        GardenAudio.Ensure();
        decor = new GameObject("Garden decor").transform;
        decor.SetParent(transform, false);
        var r = new System.Random(3232);
        System.Func<float> rf = () => (float)r.NextDouble();
        Material soil = M(new Color(0.3f, 0.21f, 0.14f)), soilD = M(new Color(0.24f, 0.17f, 0.11f));
        Material plank = Mats.TexTint("LB/wood", new Color(0.66f, 0.5f, 0.34f), 0.1f), plankD = Mats.TexTint("LB/wood", new Color(0.48f, 0.35f, 0.24f), 0.1f);
        Material stone = M(new Color(0.62f, 0.6f, 0.56f)), stone2 = M(new Color(0.55f, 0.53f, 0.5f)), stone3 = M(new Color(0.7f, 0.67f, 0.6f));
        Material leaf = M(new Color(0.2f, 0.42f, 0.17f)), leaf2 = M(new Color(0.27f, 0.5f, 0.2f)), leafD = M(new Color(0.15f, 0.32f, 0.14f));
        Material bulb = Mats.Unlit(new Color(1f, 0.86f, 0.55f)), bulb2 = Mats.Unlit(new Color(1f, 0.72f, 0.42f)), wire = M(new Color(0.12f, 0.12f, 0.12f));
        // ---- stepping-stone path: arbor (5, -13) -> fire (5, -3); branch to the hammocks
        System.Action<Vector2, Vector2> Path = (a, b) =>
        {
            float len = (b - a).magnitude; int n = Mathf.Max(2, Mathf.RoundToInt(len / 0.85f));
            for (int i = 0; i <= n; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / (float)n) + new Vector2((rf() - 0.5f) * 0.35f, (rf() - 0.5f) * 0.2f);
                float s = 0.55f + rf() * 0.25f;
                D(PrimitiveType.Cylinder, p.x, p.y, 0.02f, new Vector3(s, 0.03f, s * (0.75f + rf() * 0.3f)), i % 3 == 0 ? stone2 : i % 3 == 1 ? stone : stone3, new Vector3(0f, rf() * 180f, 0f));
            }
        };
        Path(new Vector2(5f, -13.5f), new Vector2(5f, -4.2f));
        Path(new Vector2(4.2f, -7.5f), new Vector2(-9f, -4.4f));
        Path(new Vector2(-9.5f, -4f), new Vector2(-9.5f, -2.2f));
        // ---- arbor at the entrance with a sign + two lanterns
        {
            float ax = 5f, az = -13.6f;
            for (int s = -1; s <= 1; s += 2)
            {
                D(PrimitiveType.Cube, ax + s * 1.3f, az, 1.25f, new Vector3(0.16f, 2.5f, 0.16f), plank, default(Vector3), true);
                D(PrimitiveType.Cube, ax + s * 1.3f, az + 0.6f, 1.25f, new Vector3(0.16f, 2.5f, 0.16f), plank, default(Vector3), true);
                // vines up the posts
                for (int k = 0; k < 5; k++) D(PrimitiveType.Sphere, ax + s * 1.3f + (rf() - 0.5f) * 0.2f, az + 0.3f + (rf() - 0.5f) * 0.5f, 0.4f + k * 0.45f, Vector3.one * (0.32f + rf() * 0.15f), k % 2 == 0 ? leaf : leaf2);
            }
            for (int k = 0; k < 5; k++) D(PrimitiveType.Cube, ax, az - 0.1f + k * 0.2f, 2.55f, new Vector3(3f, 0.08f, 0.08f), plankD);
            for (int k = 0; k < 7; k++) D(PrimitiveType.Sphere, ax - 1.3f + k * 0.43f, az + 0.3f, 2.68f, new Vector3(0.45f, 0.25f, 0.6f), k % 2 == 0 ? leaf2 : leaf);
            Vector3 sp = WG(ax, az - 0.15f, 2.15f);
            Ranch.Sign(sp, Yaw + 180f, "GARDEN", new Color(0.28f, 0.42f, 0.2f), 2.4f, 0.6f);
        }
        // ---- path lanterns (glowing; no extra lights)
        float[] lz = { -11.5f, -9f, -6.5f };
        foreach (float z in lz)
            for (int s = -1; s <= 1; s += 2) Lantern(5f + s * 1.0f, z, plankD, bulb);
        Lantern(-3f, -6.3f, plankD, bulb); Lantern(-7.5f, -6f, plankD, bulb);
        // ---- flower beds along the back (north) edge + two side beds
        LBPack fl = LBPack.Get("flowers0");
        Color[] petals = { new Color(1f, 0.42f, 0.55f), new Color(1f, 0.85f, 0.22f), new Color(0.66f, 0.45f, 1f), new Color(1f, 1f, 1f), new Color(1f, 0.55f, 0.25f), new Color(0.95f, 0.3f, 0.3f) };
        System.Action<float, float, float, float> Bed = (x0, z0, x1, z1) =>
        {
            float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f, w = x1 - x0, d = z1 - z0;
            // deep blocks (top just above the ground) so a gentle slope never shows a gap underneath
            D(PrimitiveType.Cube, cx, cz, -0.16f, new Vector3(w, 0.6f, d), soil);
            // timber edging
            D(PrimitiveType.Cube, cx, z0, -0.1f, new Vector3(w + 0.16f, 0.6f, 0.1f), plankD);
            D(PrimitiveType.Cube, cx, z1, -0.1f, new Vector3(w + 0.16f, 0.6f, 0.1f), plankD);
            D(PrimitiveType.Cube, x0, cz, -0.1f, new Vector3(0.1f, 0.6f, d), plankD);
            D(PrimitiveType.Cube, x1, cz, -0.1f, new Vector3(0.1f, 0.6f, d), plankD);
            int n = Mathf.RoundToInt(w * d * 2.2f);
            for (int i = 0; i < n; i++)
            {
                float x = Mathf.Lerp(x0 + 0.25f, x1 - 0.25f, rf()), z = Mathf.Lerp(z0 + 0.2f, z1 - 0.2f, rf());
                if (fl != null && i % 3 == 0) { Vector3 p = WG(x, z, 0.1f); fl.SpawnAll(decor, decor.InverseTransformPoint(p), 0.5f + rf() * 0.25f, Color.white, rf() * 360f); continue; }
                float hgt = 0.25f + rf() * 0.35f;
                D(PrimitiveType.Cylinder, x, z, 0.12f + hgt * 0.5f, new Vector3(0.04f, hgt * 0.5f, 0.04f), leaf2);
                D(PrimitiveType.Sphere, x, z, 0.14f + hgt, new Vector3(0.24f, 0.16f, 0.24f) * (0.8f + rf() * 0.5f), M(petals[(i / 2) % petals.Length]));
                if (i % 2 == 0) D(PrimitiveType.Sphere, x + 0.1f, z, 0.18f, new Vector3(0.3f, 0.14f, 0.24f), leaf);
            }
        };
        Bed(-14.5f, 10f, -5.5f, 12f);
        Bed(-3.5f, 10f, 5.5f, 12f);
        Bed(7.5f, 10f, 14.5f, 12f);
        Bed(-15f, -11.8f, -9f, -10f);
        Bed(-6.5f, -11.8f, 2.6f, -10f);
        // ---- veggie patch (south-east): three raised beds - lettuce, carrots, tomatoes on stakes - plus a watering can
        for (int b = 0; b < 3; b++)
        {
            float x = 9.2f + b * 2.1f, z0 = -11.5f, z1 = -4.5f, cz = (z0 + z1) * 0.5f;
            D(PrimitiveType.Cube, x, cz, 0.0f, new Vector3(1.5f, 0.72f, z1 - z0), plank);
            D(PrimitiveType.Cube, x, cz, 0.37f, new Vector3(1.3f, 0.04f, z1 - z0 - 0.2f), soilD);
            for (float z = z0 + 0.45f; z < z1 - 0.3f; z += 0.62f)
                for (int s = -1; s <= 1; s += 2)
                {
                    float xx = x + s * 0.3f;
                    if (b == 0) { D(PrimitiveType.Sphere, xx, z, 0.47f, new Vector3(0.36f, 0.22f, 0.36f), leaf2); D(PrimitiveType.Sphere, xx, z, 0.53f, new Vector3(0.2f, 0.14f, 0.2f), M(new Color(0.55f, 0.78f, 0.35f))); }
                    else if (b == 1)
                    {
                        D(PrimitiveType.Sphere, xx, z, 0.4f, new Vector3(0.09f, 0.06f, 0.09f), M(new Color(0.95f, 0.5f, 0.1f)));
                        for (int k = 0; k < 3; k++) D(PrimitiveType.Cube, xx + (k - 1) * 0.04f, z, 0.55f, new Vector3(0.025f, 0.3f, 0.025f), leaf2, new Vector3(0f, 0f, (k - 1) * 18f));
                    }
                    else if (s < 0)
                    {
                        D(PrimitiveType.Cylinder, x, z, 0.95f, new Vector3(0.04f, 0.6f, 0.04f), plankD);
                        for (int k = 0; k < 4; k++) D(PrimitiveType.Sphere, x + (rf() - 0.5f) * 0.35f, z + (rf() - 0.5f) * 0.25f, 0.6f + k * 0.22f, new Vector3(0.34f, 0.26f, 0.3f), leaf);
                        for (int k = 0; k < 3; k++) D(PrimitiveType.Sphere, x + (rf() - 0.5f) * 0.32f, z + (rf() - 0.5f) * 0.25f, 0.62f + rf() * 0.55f, Vector3.one * 0.12f, M(new Color(0.9f, 0.15f, 0.1f)));
                    }
                }
        }
        D(PrimitiveType.Cylinder, 12.9f, -3.5f, 0.17f, new Vector3(0.3f, 0.16f, 0.3f), Mats.Paint(new Color(0.2f, 0.55f, 0.35f), 0.6f));
        D(PrimitiveType.Cylinder, 13.12f, -3.5f, 0.27f, new Vector3(0.05f, 0.2f, 0.05f), Mats.Paint(new Color(0.2f, 0.55f, 0.35f), 0.6f), new Vector3(0f, 0f, -50f));
        // ---- bird bath between the beds
        D(PrimitiveType.Cylinder, -1f, 7.5f, 0.4f, new Vector3(0.22f, 0.4f, 0.22f), stone3);
        D(PrimitiveType.Cylinder, -1f, 7.5f, 0.82f, new Vector3(0.9f, 0.06f, 0.9f), stone3);
        D(PrimitiveType.Cylinder, -1f, 7.5f, 0.86f, new Vector3(0.75f, 0.02f, 0.75f), Mats.Shiny(new Color(0.45f, 0.65f, 0.75f)));
        // ---- shrubs round the edge (skipping the entrance), bush meshes when available
        LBPack bush = LBPack.Get("bush0");
        for (int i = 0; i < 34; i++)
        {
            float a = i / 34f * Mathf.PI * 2f;
            float x = Mathf.Cos(a) * (HalfX + 0.5f), z = Mathf.Sin(a) * (HalfZ + 0.5f);
            if (z < -9f && Mathf.Abs(x - 5f) < 3.2f) continue;     // entrance
            x += (rf() - 0.5f) * 1.2f; z += (rf() - 0.5f) * 1.2f;
            if (bush != null && i % 2 == 0) { Vector3 p = WG(x, z, -0.1f); bush.SpawnAll(decor, decor.InverseTransformPoint(p), 0.9f + rf() * 0.6f, Color.white, rf() * 360f); continue; }
            float s = 1.1f + rf() * 0.8f;
            D(PrimitiveType.Sphere, x, z, s * 0.4f, new Vector3(s * 1.2f, s * 0.9f, s * 1.1f), i % 3 == 0 ? leafD : i % 3 == 1 ? leaf : leaf2);
        }
        // ---- seats: 4 hammocks (west), campfire (east of centre) with 4 swings around it
        var seatsRoot = new GameObject("Garden seats").transform; seatsRoot.SetParent(transform, false);
        float[] hx = { -13.5f, -10.5f, -7.5f, -4.5f }, hz = { 1.6f, 2.4f, 1.2f, 2.0f };
        for (int i = 0; i < 4; i++)
        {
            var h = GardenSeat.Create(GardenSeat.Kind.Hammock, seatsRoot, WG(hx[i], hz[i]), Yaw + (i % 2 == 0 ? 0f : 180f) + (i - 1.5f) * 3f, i);
            hammocks.Add(h);
        }
        fire = Campfire.Build(transform, WG(5f, 1f));
        float[] sa = { 45f, 135f, 225f, 315f };
        for (int i = 0; i < 4; i++)
        {
            float a = sa[i] * Mathf.Deg2Rad;
            float x = 5f + Mathf.Cos(a) * 4.7f, z = 1f + Mathf.Sin(a) * 4.7f;
            Vector3 p = WG(x, z);
            Vector3 to = FirePos - p; to.y = 0f;
            var s = GardenSeat.Create(GardenSeat.Kind.Swing, seatsRoot, p, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, i);
            swings.Add(s);
        }
        // ---- string lights: 4 poles round the fire circle (between the swings) + 2 over the hammocks
        Vector2[] poles = { new Vector2(5f + 7f, 1f), new Vector2(5f, 1f + 7f), new Vector2(5f - 7f, 1f), new Vector2(5f, 1f - 7f), new Vector2(-15.5f, -1.2f), new Vector2(-15.5f, 5.2f) };
        float poleH = 3.7f;
        var tops = new Vector3[poles.Length];
        for (int i = 0; i < poles.Length; i++)
        {
            D(PrimitiveType.Cylinder, poles[i].x, poles[i].y, poleH * 0.5f, new Vector3(0.13f, poleH * 0.5f, 0.13f), plankD, default(Vector3), true);
            D(PrimitiveType.Sphere, poles[i].x, poles[i].y, poleH + 0.05f, Vector3.one * 0.2f, bulb2);
            tops[i] = WG(poles[i].x, poles[i].y, poleH - 0.1f);
        }
        int[,] strands = { { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 0 }, { 0, 2 }, { 2, 4 }, { 2, 5 }, { 4, 5 }, { 1, 5 } };
        for (int s = 0; s < strands.GetLength(0); s++) Strand(tops[strands[s, 0]], tops[strands[s, 1]], s == 4 ? 0.9f : 0.6f, bulb, bulb2, wire);
        foreach (var rr in decor.GetComponentsInChildren<Renderer>()) rr.receiveShadows = true;
        MeshMerge.Merge(decor, true);
        // ambience
        birds = Sfx.Loop(gameObject, GardenAudio.Birds);
        crickets = Sfx.Loop(gameObject, GardenAudio.Crickets);
    }

    void Lantern(float x, float z, Material post, Material glow)
    {
        D(PrimitiveType.Cube, x, z, 0.5f, new Vector3(0.08f, 1.0f, 0.08f), post);
        D(PrimitiveType.Cube, x, z, 1.08f, new Vector3(0.24f, 0.04f, 0.24f), post);
        D(PrimitiveType.Cube, x, z, 1.3f, new Vector3(0.18f, 0.4f, 0.18f), glow);
        D(PrimitiveType.Cube, x, z, 1.53f, new Vector3(0.28f, 0.06f, 0.28f), post);
        D(PrimitiveType.Sphere, x, z, 1.6f, new Vector3(0.1f, 0.08f, 0.1f), post);
    }

    // a sagging strand of bulbs between two pole tops (world points); bulbs alternate warm white / amber
    void Strand(Vector3 a, Vector3 b, float sag, Material b1, Material b2, Material wire)
    {
        float len = (b - a).magnitude;
        int segs = Mathf.Max(4, Mathf.RoundToInt(len / 0.6f));
        Vector3 prev = a;
        for (int i = 1; i <= segs; i++)
        {
            float u = i / (float)segs;
            Vector3 p = Vector3.Lerp(a, b, u) - Vector3.up * sag * 4f * u * (1f - u);
            Vector3 mid = (prev + p) * 0.5f;
            var w = Mats.Prim(PrimitiveType.Cylinder, decor, decor.InverseTransformPoint(mid), new Vector3(0.015f, (p - prev).magnitude * 0.5f, 0.015f), wire);
            w.transform.rotation = Quaternion.FromToRotation(Vector3.up, (p - prev).normalized);
            if (i < segs)
            {
                var g = Mats.Prim(PrimitiveType.Sphere, decor, decor.InverseTransformPoint(p - Vector3.up * 0.07f), new Vector3(0.09f, 0.12f, 0.09f), i % 2 == 0 ? b1 : b2);
                g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            prev = p;
        }
    }

    void Update()
    {
        float d = Sfx.Near(C);
        if (d > 1e8f) d = 999f;
        float a = Mathf.Clamp01(1f - (d - 12f) / 48f);
        bool fireLit = fire != null && fire.Lit;
        Amb(birds, a * 0.32f);
        Amb(crickets, a * (fireLit ? 0.3f : 0.2f));
    }

    static void Amb(AudioSource s, float vol)
    {
        if (s == null) return;
        if (vol > 0.004f) { if (!s.isPlaying) s.Play(); s.volume = vol; }
        else if (s.isPlaying) { s.volume = 0f; s.Stop(); }
    }

    public bool Inside(Vector3 p, float pad = 0f)
    {
        Vector3 l = Quaternion.Euler(0f, -Yaw, 0f) * (p - C);
        return Mathf.Abs(l.x) < HalfX + pad && Mathf.Abs(l.z) < HalfZ + pad;
    }
}
