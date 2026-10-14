using System.Collections.Generic;
using UnityEngine;

// ffu43 MONSTER PARK (Bill: "more interesting pieces of the monster truck track, more 3D-ness and more features, maybe a
// high wall, something really fun for a monster truck, tank, Cybertruck or motorcycle to take on, and some hard parts").
// South-west of the house, between the mech yard and the 1000-story row (flattened ground, no trees):
//   north row (driven southbound):  CAR CRUSHER (x -164) | STEP-UPS + DROP (x -153) | GAP JUMP (x -142) | TABLETOP (x -131)
//   middle:  ROCK GARDEN (hard, x -168..-140, z -92..-106) | WHOOPS (x -131) | TEETER-TOTTER (hard, x -122)
//   south:   WALL OF DEATH bowl (centre -150, -124) | HALF-PIPE (x -110) | NARROW BRIDGE (hard, x -99) | HILL CLIMB (hard, z -136)
// Everything is static MeshColliders / boxes on the default layer (vehicles, bikes, mech suits, tanks all use their own
// suspension rays) except the wall-of-death and the half-pipe, which are on RallyTrack.StuntLayer: GroundVehicle / Bike
// stick to steep stunt surfaces while they have speed. Hard parts have a TOW TRUCK: stuck (< 0.7 m/s for 6 s) or on the
// roof (2.5 s) inside one -> hauled back to that feature's start. The car crusher's junk cars flatten under heavy /
// big-wheeled vehicles and pop back after 45 s.
public class MonsterPark : MonoBehaviour
{
    public static MonsterPark I;
    public static readonly Rect Area = new Rect(-172f, -143f, 76f, 86f);     // x -172..-96, z -143..-57
    public static readonly Rect PadCut = new Rect(-122f, -72f, 30f, 20f);    // the Starship pad's corner stays out
    // ffu44: bigger, gentler bowl (ffu43 R 7 + 4.5 curve: the Cybertruck's nose dug into the tight curve and stalled at 1.7 m)
    public const float BowlX = -150f, BowlZ = -124f, BowlR0 = 8f, BowlCurve = 6f, BowlWallTop = 7f;
    public const float BowlGap = 56f, BowlTaperIn = 110f, BowlTaperOut = 40f;   // opening (north) + the wall's two ramps
    public const float LeadHalfW = 4.5f;   // ffu44: clear lead-in lane through the rock garden, x BowlX +- this
    public const float HpX = -110f, HpZ0 = -126f, HpZ1 = -99f;
    // ffu46: the half-pipe cross-section (HalfPipe() builds it from these numbers too; Skateboard uses them for smooth normals)
    public const float HpHalf = 2.5f, HpR = 4f, HpVert = 0.8f, HpDeck = 1.6f;
    public static float FloorY { get { return gy0; } }
    public const float CrushX = -164f, CrushZ0 = -64f;
    static Transform root;
    static float gy0;
    static Material dirt, wood, plank, concrete, red, white, rust;

    public class Zone { public string name; public Bounds b; public Vector3 start; public float yaw; public bool hard; }
    public static readonly List<Zone> Zones = new List<Zone>();
    static readonly List<Bounds> walls = new List<Bounds>();
    readonly Dictionary<Vehicle, float> stuckT = new Dictionary<Vehicle, float>(), flipT = new Dictionary<Vehicle, float>();
    public static int Tows, Crushes;

    // ---------------- queries ----------------
    public static bool In(float x, float z, float margin)
    {
        if (x < Area.xMin - margin || x > Area.xMax + margin || z < Area.yMin - margin || z > Area.yMax + margin) return false;
        return !(x > PadCut.xMin + margin && z > PadCut.yMin + margin);
    }
    public static bool Reserved(float x, float z) { return In(x, z, 4f); }
    // terrain: 0 = flat inside the park, eased back to rolling over 18 m (Layout.Flatness)
    public static float FlatDist(float x, float z)
    {
        float dx = Mathf.Max(0f, Mathf.Max(Area.xMin - x, x - Area.xMax)), dz = Mathf.Max(0f, Mathf.Max(Area.yMin - z, z - Area.yMax));
        return Mathf.Sqrt(dx * dx + dz * dz);
    }
    public static bool OnWall(Vector3 p) { foreach (var b in walls) if (b.Contains(p)) return true; return false; }
    public static Zone ZoneAt(Vector3 p) { foreach (var z in Zones) if (z.b.Contains(p)) return z; return null; }

    // ---------------- build ----------------
    public static void Build()
    {
        if (I != null) return;
        root = new GameObject("Monster Park").transform;
        I = root.gameObject.AddComponent<MonsterPark>();
        gy0 = Ranch.GY(-140f, -100f);
        dirt = Mats.Lit(new Color(0.55f, 0.43f, 0.31f));
        wood = Mats.Lit(new Color(0.62f, 0.46f, 0.29f));
        plank = Mats.Lit(new Color(0.72f, 0.56f, 0.36f));
        concrete = Mats.Lit(new Color(0.74f, 0.74f, 0.72f));
        red = Mats.Lit(new Color(0.85f, 0.16f, 0.12f));
        white = Mats.Lit(new Color(0.93f, 0.93f, 0.9f));
        rust = Mats.Lit(new Color(0.48f, 0.3f, 0.2f));
        // dirt arena floor (visual only, 3 cm over the flattened ground)
        var floor = Mats.Prim(PrimitiveType.Cube, root, new Vector3(Area.center.x, gy0 + 0.01f, Area.center.y), new Vector3(Area.width, 0.04f, Area.height), dirt, false);
        floor.name = "Arena floor";
        Mats.NoShadows(floor);

        CarCrusher(CrushX, CrushZ0);
        // step-ups + drop: 1.5 m, then 3 m, then a 3 m drop off the end (~60 deg)
        Lane("Step-ups + drop", -153f, -64f, 7f, new[] { V(0, 0), V(5, 1.5f), V(9, 1.5f), V(13, 3f), V(18, 3f), V(19.7f, 0) }, wood, 0, false);
        // gap jump: curved kicker to 1.8 m, 9 m gap, landing deck + ramp
        Lane("Gap jump kicker", -142f, -64f, 7f, new[] { V(0, 0), V(2, 0.18f), V(4, 0.7f), V(5.2f, 1.25f), V(6, 1.8f), V(6.3f, 0f) }, wood, 0, false);
        Lane("Gap jump landing", -142f, -79.3f, 7f, new[] { V(0, 0), V(0.3f, 1.8f), V(2.5f, 1.8f), V(9.5f, 0f) }, wood, 0, false);
        AddZone("gap jump", new Vector3(-142f, 0f, -76f), new Vector3(12f, 8f, 26f), new Vector3(-142f, 0f, -60f), 180f, true);
        // tabletop
        Lane("Tabletop", -131f, -64f, 8f, new[] { V(0, 0), V(2, 0.25f), V(7, 2.2f), V(15, 2.2f), V(22, 0) }, dirt, 0, false, 1.4f);   // ffu45: sloped solid flanks
        // whoops / rollers: six 0.65 m humps, 4.4 m apart
        var wp = new List<Vector2>();
        for (int i = 0; i <= 6 * 16; i++) { float d = i * 4.4f / 16f; wp.Add(V(d, 0.325f * (1f - Mathf.Cos(2f * Mathf.PI * d / 4.4f)))); }
        Lane("Whoops", -131f, -95f, 7f, wp.ToArray(), dirt, 0, false, 1.6f);   // ffu45: sloped solid flanks
        RockGarden();
        Teeter(-122f, -101f);
        WallOfDeath();
        HalfPipe();
        // narrow bridge: 2.6 m wide, 1.6 m up, 16 m long (hard: fall off = drive back round)
        Lane("Narrow bridge", -99f, -92f, 2.6f, new[] { V(0, 0), V(5, 1.6f), V(21, 1.6f), V(26, 0) }, plank, 0, false);
        AddZone("narrow bridge", new Vector3(-99f, 0f, -105f), new Vector3(10f, 6f, 28f), new Vector3(-99f, 0f, -87f), 180f, true);
        // hill climb: 30 deg up to 6 m, a short top, 20 deg down (hard)
        LaneX("Hill climb", -129f, -136f, 9f, new[] { V(0, 0), V(1.5f, 0.3f), V(10.4f, 5.6f), V(11.6f, 6f), V(14.4f, 6f), V(16f, 5.6f), V(30f, 0.3f), V(31.5f, 0f) }, dirt);
        AddZone("hill climb", new Vector3(-113f, 0f, -136f), new Vector3(36f, 16f, 12f), new Vector3(-134f, 0f, -136f), 90f, true);
        AddZone("step-ups + drop", new Vector3(-153f, 0f, -74f), new Vector3(9f, 10f, 24f), new Vector3(-153f, 0f, -60f), 180f, false);

        // signs
        Ranch.Sign(new Vector3(-147f, gy0 + 8.5f, -57.5f), 0f, "MONSTER PARK\n<size=26>crusher - step-ups - gap - tabletop - whoops - rocks - teeter - WALL OF DEATH - half-pipe - bridge - hill</size>", new Color(0.55f, 0.12f, 0.08f), 14f, 3.2f);
        Label("CAR CRUSHER", CrushX, -62f); Label("STEP-UPS + DROP", -153f, -62f); Label("GAP JUMP", -142f, -62f); Label("TABLETOP", -131f, -62f);
        Label("WHOOPS", -131f, -93f); Label("ROCK GARDEN\n<size=20>hard - tow truck on call</size>", -168f, -99.5f, 90f); Label("TEETER-TOTTER\n<size=20>hard</size>", -122f, -91f);
        Label("WALL OF DEATH\n<size=20>keep it fast!</size>", BowlX, BowlZ + 14f); Label("HALF-PIPE", HpX, HpZ1 + 1.5f); Label("NARROW BRIDGE\n<size=20>hard</size>", -99f, -90f);
        Label("HILL CLIMB\n<size=20>hard - 30 deg</size>", -131f, -136f, 90f);
        Debug.Log("MonsterPark: built at x " + Area.xMin + ".." + Area.xMax + " z " + Area.yMin + ".." + Area.yMax + " ground y " + gy0.ToString("0.00") + " | " + Zones.Count + " tow zones, " + cars.Count + " junk cars, wall of death R " + (BowlR0 + BowlCurve) + " m x " + BowlWallTop + " m, half-pipe " + (HpZ1 - HpZ0) + " m");
    }

    static Vector2 V(float d, float y) { return new Vector2(d, y); }

    static void Label(string text, float x, float z, float yaw = 0f)
    {
        // overhead boards (no colliders; bottom ~5 m up, clear of monster-wheel trucks)
        Ranch.Sign(new Vector3(x, gy0 + 5.9f, z), yaw, text, new Color(0.15f, 0.15f, 0.18f), 5.2f, 1.6f);
    }

    static void AddZone(string name, Vector3 c, Vector3 size, Vector3 start, float yaw, bool hard)
    {
        Zones.Add(new Zone { name = name, b = new Bounds(new Vector3(c.x, gy0 + size.y * 0.5f - 1f, c.z), size), start = new Vector3(start.x, gy0, start.z), yaw = yaw, hard = hard });
    }

    // a solid along -z (southbound) from (x, z0): profile (d along the lane, height), width w
    static GameObject Lane(string name, float x, float z0, float w, Vector2[] prof, Material m, int layer, bool stunt, float flank = 0f)
    {
        return Extrude(name, new Vector3(x, gy0, z0), Vector3.back, w, prof, m, stunt ? RallyTrack.StuntLayer : layer, flank);
    }
    // a solid along +x (eastbound)
    static GameObject LaneX(string name, float x0, float z, float w, Vector2[] prof, Material m)
    {
        return Extrude(name, new Vector3(x0, gy0, z), Vector3.right, w, prof, m, 0);
    }

    // closed prism: top follows the profile, flat sides, base 0.4 m under the ground
    // ffu45: flank > 0 = the dirt mounds get sloped, solid sides (flank m of run per m of height) instead of vertical walls:
    // a truck / bike that comes at a mound from the side climbs it instead of hitting a wall
    static GameObject Extrude(string name, Vector3 o, Vector3 dir, float w, Vector2[] prof, Material m, int layer, float flank = 0f)
    {
        Vector3 rt = Vector3.Cross(Vector3.up, dir).normalized * (w * 0.5f);
        var v = new List<Vector3>(); var t = new List<int>();
        int n = prof.Length;
        const float baseY = -0.4f;
        // top strip
        for (int i = 0; i < n; i++) { Vector3 c = o + dir * prof[i].x + Vector3.up * prof[i].y; v.Add(c - rt); v.Add(c + rt); }
        for (int i = 0; i < n - 1; i++) { int a = i * 2; Tri(t, v, a, a + 2, a + 1, Vector3.up); Tri(t, v, a + 1, a + 2, a + 3, Vector3.up); }
        // sides
        for (int s = -1; s <= 1; s += 2)
        {
            int b0 = v.Count;
            for (int i = 0; i < n; i++) { Vector3 c = o + dir * prof[i].x; v.Add(c + rt * s + Vector3.up * prof[i].y); v.Add(flank > 0f ? c + rt * s + rt.normalized * s * (prof[i].y * flank) - Vector3.up * 0.05f : c + rt * s + Vector3.up * baseY); }
            for (int i = 0; i < n - 1; i++) { int a = b0 + i * 2; Vector3 wn = flank > 0f ? rt * s + Vector3.up * w * 0.5f : rt * s; Tri(t, v, a, a + 2, a + 1, wn); Tri(t, v, a + 1, a + 2, a + 3, wn); }
        }
        // end caps
        for (int e = 0; e < 2; e++)
        {
            int i = e == 0 ? 0 : n - 1; Vector3 c = o + dir * prof[i].x; Vector3 nn = e == 0 ? -dir : dir;
            if (prof[i].y < 0.02f) continue;
            int b0 = v.Count;
            v.Add(c - rt + Vector3.up * prof[i].y); v.Add(c + rt + Vector3.up * prof[i].y); v.Add(c - rt + Vector3.up * baseY); v.Add(c + rt + Vector3.up * baseY);
            Tri(t, v, b0, b0 + 1, b0 + 2, nn); Tri(t, v, b0 + 1, b0 + 3, b0 + 2, nn);
        }
        return MeshObj(name, v, t, m, layer);
    }

    static void Tri(List<int> t, List<Vector3> v, int a, int b, int c, Vector3 want)
    {
        Vector3 n = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
        if (Vector3.Dot(n, want) >= 0f) t.AddRange(new[] { a, b, c }); else t.AddRange(new[] { a, c, b });
    }

    static GameObject MeshObj(string name, List<Vector3> v, List<int> t, Material m, int layer)
    {
        // flat shading: unshare vertices so ramps / walls read crisply
        var fv = new List<Vector3>(); var ft = new List<int>();
        for (int i = 0; i < t.Count; i++) { fv.Add(v[t[i]]); ft.Add(i); }
        var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, name = name };
        mesh.SetVertices(fv); mesh.SetTriangles(ft, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.layer = layer;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = m;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        return go;
    }

    // ---------------- wall of death ----------------
    // a 300 deg ring open to the north: flat floor to r 7, a 4.5 m radius curve up to vertical at r 11.5, a vertical
    // wall to 7 m, a lip. The ring's two ends taper to nothing over 40 deg, so you drive in off the flat at the opening,
    // climb the curve as you speed up and ride the wall all the way round. Red / white stripe round the top.
    static void WallOfDeath()
    {
        var v = new List<Vector3>(); var t = new List<int>();
        Vector3 c = new Vector3(BowlX, gy0, BowlZ);
        const float gap = BowlGap;
        int segs = 120;
        var prof = new List<Vector2>();
        for (int k = 0; k <= 10; k++) { float a = k / 10f * Mathf.PI * 0.5f; prof.Add(new Vector2(BowlR0 + BowlCurve * Mathf.Sin(a), BowlCurve - BowlCurve * Mathf.Cos(a))); }
        prof.Add(new Vector2(BowlR0 + BowlCurve, BowlWallTop)); prof.Add(new Vector2(BowlR0 + BowlCurve + 0.7f, BowlWallTop)); prof.Add(new Vector2(BowlR0 + BowlCurve + 0.7f, 0f));
        int pn = prof.Count;
        float a0 = 90f + gap * 0.5f, span = 360f - gap;   // opening centred on +z (north)
        for (int i = 0; i <= segs; i++)
        {
            float u = i / (float)segs, ang = (a0 + span * u) * Mathf.Deg2Rad;
            // ffu44: asymmetric ramps - anticlockwise riders (enter, turn right / west) climb a long 110 deg ramp; the far end
            // drops back down over 40 deg into the opening
            float deg = span * u, k = Mathf.SmoothStep(0f, 1f, Mathf.Min(Mathf.Clamp01(deg / BowlTaperIn), Mathf.Clamp01((span - deg) / BowlTaperOut)));
            Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            for (int j = 0; j < pn; j++)
            {
                Vector2 p = prof[j];
                float r = p.x;   // ffu44b: ramps keep the full ring radius (ffu44a pulled the ends in to r 8..10 and a rider
                                 // crossing the opening at r 12.5 went round the OUTSIDE of the ring)
                v.Add(c + dir * r + Vector3.up * (p.y * k));
            }
        }
        for (int i = 0; i < segs; i++)
            for (int j = 0; j < pn - 1; j++)
            {
                int a = i * pn + j, b = a + pn;
                Vector3 mid = (v[a] + v[b + 1]) * 0.5f, inward = c + Vector3.up * 4f - mid;
                Vector3 want = j < pn - 2 ? inward : (j == pn - 2 ? (mid - c).normalized : Vector3.up);
                if (j == pn - 3) want = Vector3.up;   // lip top
                Tri(t, v, a, b, a + 1, want); Tri(t, v, a + 1, b, b + 1, want);
            }
        MeshObj("Wall of death", v, t, plank, RallyTrack.StuntLayer);
        // stripe + posts round the top (visual)
        for (int i = 0; i < 36; i++)
        {
            float deg = span * (i + 0.5f) / 36f, ang = (a0 + deg) * Mathf.Deg2Rad;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Min(Mathf.Clamp01(deg / BowlTaperIn), Mathf.Clamp01((span - deg) / BowlTaperOut)));
            if (k < 0.95f) continue;
            Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            var s = Mats.Prim(PrimitiveType.Cube, root, c + dir * (BowlR0 + BowlCurve + 0.75f) + Vector3.up * (BowlWallTop - 0.6f), new Vector3(0.06f, 0.9f, 2f * Mathf.PI * (BowlR0 + BowlCurve) / 36f * (span / 360f) + 0.05f), i % 2 == 0 ? red : white, false);
            s.transform.rotation = Quaternion.LookRotation(new Vector3(-dir.z, 0f, dir.x), Vector3.up);
            if (i % 3 == 0) Mats.Prim(PrimitiveType.Cube, root, c + dir * (BowlR0 + BowlCurve + 1.1f) + Vector3.up * (BowlWallTop * 0.5f), new Vector3(0.3f, BowlWallTop, 0.3f), wood, false);
        }
        walls.Add(new Bounds(c + Vector3.up * 4f, new Vector3((BowlR0 + BowlCurve) * 2f + 2f, 10f, (BowlR0 + BowlCurve) * 2f + 2f)));
        AddZone("wall of death", c, new Vector3((BowlR0 + BowlCurve) * 2f + 2f, 12f, (BowlR0 + BowlCurve) * 2f + 2f), c + new Vector3(0f, 0f, BowlR0 + BowlCurve + 8f), 180f, true);
    }

    // ---------------- half-pipe ----------------
    static void HalfPipe()
    {
        // cross-section (u across, y): deck, vertical, quarter pipe R 4, 5 m flat bottom, mirrored
        const float half = HpHalf, R = HpR, vert = HpVert, deck = HpDeck;
        var cs = new List<Vector2>();
        cs.Add(new Vector2(-(half + R + deck), R + vert)); cs.Add(new Vector2(-(half + R), R + vert));
        for (int k = 0; k <= 10; k++) { float a = (1f - k / 10f) * Mathf.PI * 0.5f; cs.Add(new Vector2(-(half + R * Mathf.Sin(a)), R - R * Mathf.Cos(a))); }
        for (int k = 0; k <= 10; k++) { float a = k / 10f * Mathf.PI * 0.5f; cs.Add(new Vector2(half + R * Mathf.Sin(a), R - R * Mathf.Cos(a))); }
        cs.Add(new Vector2(half + R, R + vert)); cs.Add(new Vector2(half + R + deck, R + vert));
        var v = new List<Vector3>(); var t = new List<int>();
        int segs = 18, pn = cs.Count;
        for (int i = 0; i <= segs; i++)
        {
            float z = Mathf.Lerp(HpZ0, HpZ1, i / (float)segs);
            foreach (var p in cs) v.Add(new Vector3(HpX + p.x, gy0 + p.y, z));
        }
        for (int i = 0; i < segs; i++)
            for (int j = 0; j < pn - 1; j++)
            {
                int a = i * pn + j, b = a + pn;
                Vector3 mid = (v[a] + v[b + 1]) * 0.5f;
                Vector3 want = new Vector3(HpX, gy0 + R + 2f, mid.z) - mid;
                if (j == 0 || j == pn - 2) want = Vector3.up;
                Tri(t, v, a, b, a + 1, want); Tri(t, v, a + 1, b, b + 1, want);
            }
        // outer backs (decks down to the ground), so it reads solid
        for (int s = -1; s <= 1; s += 2)
        {
            int b0 = v.Count; float ux = HpX + s * (half + R + deck);
            v.Add(new Vector3(ux, gy0 + R + vert, HpZ0)); v.Add(new Vector3(ux, gy0 + R + vert, HpZ1)); v.Add(new Vector3(ux, gy0 - 0.3f, HpZ0)); v.Add(new Vector3(ux, gy0 - 0.3f, HpZ1));
            Tri(t, v, b0, b0 + 1, b0 + 2, Vector3.right * s); Tri(t, v, b0 + 1, b0 + 3, b0 + 2, Vector3.right * s);
        }
        MeshObj("Half-pipe", v, t, concrete, RallyTrack.StuntLayer);
        walls.Add(new Bounds(new Vector3(HpX, gy0 + 3f, (HpZ0 + HpZ1) * 0.5f), new Vector3((half + R + deck) * 2f, 8f, HpZ1 - HpZ0)));
        // coping stripes
        for (int s = -1; s <= 1; s += 2)
            Mats.Prim(PrimitiveType.Cube, root, new Vector3(HpX + s * (half + R + 0.06f), gy0 + R + vert + 0.03f, (HpZ0 + HpZ1) * 0.5f), new Vector3(0.14f, 0.08f, HpZ1 - HpZ0), red, false);
    }

    // ---------------- rock garden ----------------
    static void RockGarden()
    {
        var r = new System.Random(4343);
        Material stone = Mats.Lit(new Color(0.5f, 0.49f, 0.46f)), stone2 = Mats.Lit(new Color(0.42f, 0.4f, 0.37f)), bark = Mats.Lit(new Color(0.36f, 0.25f, 0.15f));
        int rocks = 0, logs = 0;
        for (int i = 0; i < 46; i++)
        {
            float x = -167f + (float)r.NextDouble() * 26f, z = -93f - (float)r.NextDouble() * 13f;
            float rad = 0.35f + (float)r.NextDouble() * 0.75f;
            if (Mathf.Abs(x - BowlX) < LeadHalfW + rad) continue;   // ffu44: wall-of-death lead-in lane stays clear
            var g = Mats.Prim(PrimitiveType.Sphere, root, new Vector3(x, gy0 + rad * 0.3f, z), Vector3.one * rad * 2f, i % 3 == 0 ? stone2 : stone, true);
            g.name = "Rock garden boulder"; rocks++;
        }
        for (int i = 0; i < 9; i++)
        {
            float x = -165f + i * 2.9f + (float)r.NextDouble() * 1.2f, z = -99.5f + ((float)r.NextDouble() - 0.5f) * 8f;
            float rad = 0.22f + (float)r.NextDouble() * 0.25f, len = 3.5f + (float)r.NextDouble() * 3f;
            float yawL = (float)r.NextDouble() * 60f - 30f;
            if (Mathf.Abs(x - BowlX) < LeadHalfW + 2f) continue;   // ffu44: lead-in lane
            var g = Mats.Prim(PrimitiveType.Cylinder, root, new Vector3(x, gy0 + rad, z), new Vector3(rad * 2f, len * 0.5f, rad * 2f), new Vector3(90f, yawL, 0f), bark, true);
            g.name = "Rock garden log"; logs++;
        }
        AddZone("rock garden", new Vector3(-154f, 0f, -99.5f), new Vector3(30f, 8f, 17f), new Vector3(-170f, 0f, -99.5f), 90f, true);
    }

    // ---------------- teeter-totter ----------------
    Transform teeter; Rigidbody teeterRb; float teeterAng, teeterWant;
    const float TeeterLen = 14f, TeeterW = 3.4f, TeeterPivot = 1.3f, TeeterMax = 10.5f;
    static void Teeter(float x, float zc)
    {
        // pivot frame + a kinematic plank that tips toward the heavier end (vehicles' mass x distance from the pivot)
        Mats.Prim(PrimitiveType.Cube, root, new Vector3(x, gy0 + TeeterPivot * 0.5f - 0.1f, zc), new Vector3(TeeterW + 0.6f, TeeterPivot - 0.2f, 0.8f), rust, true);
        var go = new GameObject("Teeter-totter");
        go.transform.position = new Vector3(x, gy0 + TeeterPivot, zc);
        var p = Mats.Prim(PrimitiveType.Cube, go.transform, Vector3.zero, new Vector3(TeeterW, 0.25f, TeeterLen), plank, true);
        for (int i = -3; i <= 3; i += 2) Mats.Prim(PrimitiveType.Cube, go.transform, new Vector3(0f, 0.13f, i * 2f), new Vector3(TeeterW, 0.03f, 0.25f), i % 4 == 1 ? red : white, false);
        var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.Interpolate;
        I.teeter = go.transform; I.teeterRb = rb; I.teeterAng = TeeterMax; I.teeterWant = TeeterMax;   // starts with the north end up
        go.transform.rotation = Quaternion.Euler(-TeeterMax, 0f, 0f);
        AddZone("teeter-totter", new Vector3(x, 0f, zc), new Vector3(TeeterW + 4f, 6f, TeeterLen + 4f), new Vector3(x, 0f, zc + TeeterLen * 0.5f + 6f), 180f, true);
    }

    void TeeterStep(float dt)
    {
        if (teeter == null) return;
        float moment = 0f;
        Vector3 c = teeter.position, ax = teeter.forward;
        foreach (var v in Vehicle.All)
        {
            if (v == null || v.rb == null) continue;
            Vector3 d = v.rb.worldCenterOfMass - c;
            if (Mathf.Abs(Vector3.Dot(d, teeter.right)) > TeeterW * 0.5f + 0.6f) continue;
            float l = Vector3.Dot(d, ax);
            if (Mathf.Abs(l) > TeeterLen * 0.5f + 0.5f || Vector3.Dot(d, Vector3.up) > 4f + v.bigLift || Vector3.Dot(d, Vector3.up) < -1.5f) continue;
            moment += v.rb.mass * l;
        }
        // + moment = load toward +z (north) -> north end goes down (positive x-rotation tips +z down in Unity)
        if (Mathf.Abs(moment) > 60f) teeterWant = moment > 0f ? TeeterMax : -TeeterMax;
        float before = teeterAng;
        teeterAng = Mathf.MoveTowards(teeterAng, teeterWant, 16f * dt);
        if (teeterAng != before)
        {
            teeterRb.MoveRotation(Quaternion.Euler(teeterAng, 0f, 0f));
            if (Mathf.Abs(teeterAng) >= TeeterMax - 0.01f && Mathf.Abs(before) < TeeterMax - 0.01f) { Sfx.PlayAt(Sfx.Thud, teeter.position + ax * Mathf.Sign(teeterAng) * TeeterLen * 0.5f, 0.6f, 50f, 0.8f); FX.Dust(teeter.position + ax * Mathf.Sign(teeterAng) * TeeterLen * 0.5f, 1.2f); }
        }
    }
    public static float TeeterAngle { get { return I != null ? I.teeterAng : 0f; } }

    // ---------------- car crusher ----------------
    class JunkCar { public Transform roof, body; public BoxCollider roofCol, bodyCol; public float crushedT; public bool crushed; public Vector3 roofS, bodyS, roofP, bodyP; public Bounds top; }
    static readonly List<JunkCar> cars = new List<JunkCar>();
    static void CarCrusher(float x, float z0)
    {
        // entry ramp (crushed-car pile look), 7 junk cars side by side across the lane, exit ramp
        Lane("Crusher ramp in", x, z0, 6.4f, new[] { V(0, 0), V(4, 1.35f), V(4.2f, 1.35f), V(4.2f, 0f) }, rust, 0, false);
        Color[] paint = { new Color(0.55f, 0.15f, 0.12f), new Color(0.2f, 0.32f, 0.55f), new Color(0.75f, 0.62f, 0.2f), new Color(0.3f, 0.45f, 0.25f), new Color(0.6f, 0.6f, 0.62f), new Color(0.45f, 0.2f, 0.45f), new Color(0.15f, 0.15f, 0.17f) };
        float z = z0 - 4.3f;
        for (int i = 0; i < 7; i++)
        {
            float zc = z - 0.95f;
            var jc = new JunkCar();
            Material pm = Mats.Lit(paint[i]);
            var body = Mats.Prim(PrimitiveType.Cube, root, new Vector3(x, gy0 + 0.5f, zc), new Vector3(4.3f, 0.9f, 1.85f), pm, true);
            var roof = Mats.Prim(PrimitiveType.Cube, root, new Vector3(x + 0.2f, gy0 + 1.2f, zc), new Vector3(2.3f, 0.5f, 1.6f), pm, true);
            Mats.Prim(PrimitiveType.Cube, roof.transform, new Vector3(0f, 0f, 0f), new Vector3(0.7f, 0.75f, 1.02f), Mats.Lit(new Color(0.12f, 0.14f, 0.16f)), false);   // windows band
            for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cylinder, root, new Vector3(x + s * 1.35f, gy0 + 0.33f, zc), new Vector3(0.66f, 0.14f, 0.66f), new Vector3(90f, 0f, 0f), Mats.Lit(new Color(0.05f, 0.05f, 0.05f)), false);
            body.name = "Junk car " + i; roof.name = "Junk car roof " + i;
            jc.body = body.transform; jc.roof = roof.transform; jc.bodyCol = body.GetComponent<BoxCollider>(); jc.roofCol = roof.GetComponent<BoxCollider>();
            jc.roofS = roof.transform.localScale; jc.bodyS = body.transform.localScale; jc.roofP = roof.transform.position; jc.bodyP = body.transform.position;
            jc.top = new Bounds(new Vector3(x, gy0 + 2.6f, zc), new Vector3(4.6f, 2.6f, 2.2f));
            cars.Add(jc);
            z -= 1.9f;
        }
        Lane("Crusher ramp out", x, z - 0.1f, 6.4f, new[] { V(0, 0), V(0, 1.35f), V(0.2f, 1.35f), V(4.5f, 0f) }, rust, 0, false);
        AddZone("car crusher", new Vector3(x, 0f, z0 - 12f), new Vector3(9f, 8f, 26f), new Vector3(x, 0f, z0 + 4f), 180f, false);
    }

    void CrushStep(float dt)
    {
        foreach (var jc in cars)
        {
            if (jc.crushed)
            {
                jc.crushedT -= dt;
                if (jc.crushedT <= 0f && !Occupied(jc.top))
                {
                    jc.crushed = false;
                    jc.roof.localScale = jc.roofS; jc.roof.position = jc.roofP; jc.body.localScale = jc.bodyS; jc.body.position = jc.bodyP;
                    FX.Cloud(jc.roofP, 0.8f, 6, new Color(0.6f, 0.55f, 0.5f, 0.5f));
                }
                continue;
            }
            Vehicle by = Heavy(jc.top);
            if (by == null) continue;
            jc.crushed = true; jc.crushedT = 45f; Crushes++;
            jc.roof.localScale = new Vector3(jc.roofS.x * 1.05f, 0.12f, jc.roofS.z * 1.08f);
            jc.roof.position = jc.roofP + Vector3.up * (-0.62f);
            jc.body.localScale = new Vector3(jc.bodyS.x * 1.03f, 0.62f, jc.bodyS.z * 1.05f);
            jc.body.position = jc.bodyP + Vector3.up * (-0.14f);
            Sfx.PlayAt(Sfx.Pick(Sfx.Crash), jc.roofP, 0.85f, 70f, Random.Range(0.75f, 0.9f));
            FX.Cloud(jc.roofP, 1f, 10, new Color(0.45f, 0.42f, 0.4f, 0.6f));
            if (by.driver != null && by.driver.human) by.driver.Toast("CRUNCH! Car crushed (" + Crushes + ")", 1.2f);
            Debug.Log("MonsterPark: " + by.Title + " crushed junk car " + cars.IndexOf(jc) + " (mass " + by.rb.mass + ", wheels " + by.bigLift.ToString("0.0") + " m lift)");
        }
    }
    // a vehicle heavy enough to flatten a car (trucks, tanks, mech suit, or anything on big tyres), its body over the car
    static Vehicle Heavy(Bounds b)
    {
        foreach (var v in Vehicle.All)
        {
            if (v == null || v.rb == null || v.rb.isKinematic) continue;
            if (!(v.rb.mass >= 1500f || v.bigLift > 0.8f)) continue;
            if (b.Contains(v.rb.worldCenterOfMass) || b.Contains(v.transform.position)) return v;
        }
        return null;
    }
    static bool Occupied(Bounds b) { foreach (var v in Vehicle.All) if (v != null && b.Contains(v.transform.position)) return true; return false; }

    // ---------------- tow truck ----------------
    float towTick;
    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        TeeterStep(dt);
        CrushStep(dt);
    }

    void Update()
    {
        towTick -= Time.deltaTime;
        if (towTick > 0f) return;
        float dt = 0.25f - towTick; towTick = 0.25f;
        foreach (var v in Vehicle.All)
        {
            if (v == null || v.rb == null || v.rb.isKinematic || v.driver == null || v is StoryMech || v.flyer) continue;
            Zone z = ZoneAt(v.transform.position);
            if (z == null || !z.hard) { stuckT.Remove(v); flipT.Remove(v); continue; }
            float st, ft;
            stuckT.TryGetValue(v, out st); flipT.TryGetValue(v, out ft);
            st = v.Speed < 0.7f ? st + dt : 0f;
            ft = v.transform.up.y < 0.35f ? ft + dt : 0f;
            stuckT[v] = st; flipT[v] = ft;
            if (st > 6f || ft > 2.5f) { Tow(v, z, ft > 2.5f ? "on its roof" : "stuck"); stuckT[v] = 0f; flipT[v] = 0f; }
        }
    }

    public static void Tow(Vehicle v, Zone z, string why)
    {
        Tows++;
        Vector3 p = z.start + Vector3.up * (0.7f + v.bigLift);
        Quaternion q = Quaternion.Euler(0f, z.yaw, 0f);
        v.rb.velocity = Vector3.zero; v.rb.angularVelocity = Vector3.zero;
        v.rb.position = p; v.rb.rotation = q; v.transform.SetPositionAndRotation(p, q);
        Sfx.PlayAt(Sfx.Clank != null ? Sfx.Clank : Sfx.Thud, p, 0.6f, 50f, 0.9f);
        FX.Cloud(p, 1f, 8, new Color(0.62f, 0.52f, 0.38f, 0.6f));
        if (v.driver != null && v.driver.human) v.driver.Toast("TOW TRUCK! " + v.Title + " was " + why + " - hauled back to the start of the " + z.name, 2.6f);
        Debug.Log("MonsterPark: tow truck - " + v.Title + " " + why + " in the " + z.name + " -> back to its start");
    }
}
