using System.Collections.Generic;
using UnityEngine;

// ffu27 TREEHOUSES (Bill): two wooden play houses up in big trees on the ranch. Each: a thick trunk with a ladder of
// rungs (climb it with the tree-climb: push into the trunk, the ring fills, climb, push UP at the top to step out onto
// the deck), a railed deck 3.55 m up, a little house on it (walls, windows, a doorway, pitched roof) you can walk around
// inside, two stacked crates on the deck to hop up onto the roof, and a leafy crown above. Solid box colliders; not
// registered with Destruct (missiles / mechs leave them standing). Placed by a fixed-seed search for clear, flat ground
// away from trees, roads, the pond, the house, the pads and the robot zones (same every load).
public static class Treehouse
{
    public const float DeckY = 3.55f;
    public static readonly List<Vector3> Spots = new List<Vector3>();       // trunk base (world)
    public static readonly List<TreeClimb.Tree> Climbs = new List<TreeClimb.Tree>();
    static readonly Vector2[] Prefer = { new Vector2(30f, -60f), new Vector2(-80f, 60f), new Vector2(80f, 40f), new Vector2(-20f, 110f), new Vector2(60f, -110f) };

    public static void BuildAll(List<Vector2> trees)
    {
        Spots.Clear(); Climbs.Clear();
        Physics.SyncTransforms();
        var r = new System.Random(2027);
        foreach (Vector2 pref in Prefer)
        {
            if (Spots.Count >= 2) break;
            for (int k = 0; k < 160; k++)
            {
                float x = pref.x + (float)(r.NextDouble() * 2 - 1) * 30f, z = pref.y + (float)(r.NextDouble() * 2 - 1) * 30f;
                if (!Clear(x, z, trees)) continue;
                Build(new Vector3(x, Ranch.GY(x, z), z));
                break;
            }
        }
        Debug.Log("Treehouse: " + Spots.Count + " built" + (Spots.Count > 0 ? " first at " + Spots[0].ToString("0") : ""));
    }

    static bool Clear(float x, float z, List<Vector2> trees)
    {
        if (Mathf.Abs(x) > 160f || Mathf.Abs(z) > 160f) return false;
        if (Layout.Flatness(x, z) < 0.8f || Layout.PondQ(x, z) < 1.5f) return false;
        if (Layout.RoadDist(x, z) < Layout.TrackW + 9f) return false;
        if (x > -36f && x < 36f && z > 24f && z < 76f) return false;
        if (Worlds.StageEOn && RanchLife.Reserved(x, z)) return false;
        if (RobotNav.Blocked(x, z)) return false;
        Vector2 p = new Vector2(x, z);
        if ((p - StorySet.PadXZ).sqrMagnitude < 30f * 30f || (p - Layout.PadC).sqrMagnitude < 45f * 45f) return false;
        if ((p - Layout.HouseC).sqrMagnitude < 40f * 40f) return false;
        foreach (var t in trees) if ((t - p).sqrMagnitude < 7.5f * 7.5f) return false;
        foreach (var s in Spots) if ((new Vector2(s.x, s.z) - p).sqrMagnitude < 50f * 50f) return false;
        float gy = Ranch.GY(x, z);
        foreach (var c in Physics.OverlapBox(new Vector3(x + 1.5f, gy + 4f, z), new Vector3(5.5f, 3.6f, 4.5f)))
            if (!(c is TerrainCollider) && !c.isTrigger) return false;
        return true;
    }

    static Transform root;
    static GameObject B(Vector3 c, Vector3 size, Color col, bool solid = true, float rotX = 0f)
    {
        var g = Mats.Prim(PrimitiveType.Cube, root, c, size, Mats.Lit(col), solid);
        if (rotX != 0f) g.transform.localRotation = Quaternion.Euler(rotX, 0f, 0f);
        return g;
    }

    static void Build(Vector3 basePos)
    {
        root = new GameObject("Treehouse").transform;
        root.position = basePos;
        Color bark = new Color(0.36f, 0.25f, 0.16f), plank = new Color(0.62f, 0.44f, 0.26f), plankD = new Color(0.5f, 0.34f, 0.2f),
              roofC = new Color(0.55f, 0.2f, 0.14f), trim = new Color(0.9f, 0.86f, 0.74f);
        float D = DeckY, H = 2.4f;
        // trunk (capsule collider: radius 0.45, 10 m) + ladder rungs on the -x side (visual)
        Mats.Prim(PrimitiveType.Cylinder, root, new Vector3(0f, 5f, 0f), new Vector3(0.9f, 5f, 0.9f), Mats.Lit(bark), true);
        for (float y = 0.45f; y < D - 0.2f; y += 0.36f) B(new Vector3(-0.52f, y, 0f), new Vector3(0.1f, 0.07f, 0.62f), plankD, false);
        for (int s = -1; s <= 1; s += 2) B(new Vector3(-0.5f, D * 0.5f, 0.33f * s), new Vector3(0.08f, D, 0.08f), plankD, false);
        // deck + joists + railing
        B(new Vector3(1.5f, D - 0.15f, 0f), new Vector3(8f, 0.3f, 6.4f), plank);
        for (int s = -1; s <= 1; s += 2) B(new Vector3(1.5f, D - 0.45f, 2.6f * s), new Vector3(8f, 0.3f, 0.2f), plankD, false);
        for (int s = -1; s <= 1; s += 2)
        {
            B(new Vector3(1.5f, D + 0.65f, 3.15f * s), new Vector3(8f, 0.1f, 0.1f), trim);
            B(new Vector3(1.5f, D + 0.3f, 3.15f * s), new Vector3(8f, 0.6f, 0.06f), plankD, true);
        }
        B(new Vector3(-2.45f, D + 0.65f, 0f), new Vector3(0.1f, 0.1f, 6.4f), trim);
        B(new Vector3(-2.45f, D + 0.3f, 0f), new Vector3(0.06f, 0.6f, 6.4f), plankD, true);
        B(new Vector3(5.45f, D + 0.65f, 0f), new Vector3(0.1f, 0.1f, 6.4f), trim);
        B(new Vector3(5.45f, D + 0.3f, 0f), new Vector3(0.06f, 0.6f, 6.4f), plankD, true);
        // house: x 1.2..5.2, z -2.2..2.2, walls 2.4 m; doorway in the trunk-side wall, windows in the other three
        float wy = D;
        // back wall (x 5.2) with a window z -0.6..0.6
        B(new Vector3(5.2f, wy + 0.45f, 0f), new Vector3(0.15f, 0.9f, 4.4f), plank);
        B(new Vector3(5.2f, wy + 2.05f, 0f), new Vector3(0.15f, 0.7f, 4.4f), plank);
        for (int s = -1; s <= 1; s += 2) B(new Vector3(5.2f, wy + 1.3f, 1.4f * s), new Vector3(0.15f, 0.8f, 1.6f), plank);
        // side walls (z +-2.2) with a window x 2.7..3.7
        for (int s = -1; s <= 1; s += 2)
        {
            float zz = 2.2f * s;
            B(new Vector3(3.2f, wy + 0.45f, zz), new Vector3(4.15f, 0.9f, 0.15f), plank);
            B(new Vector3(3.2f, wy + 2.05f, zz), new Vector3(4.15f, 0.7f, 0.15f), plank);
            B(new Vector3(1.95f, wy + 1.3f, zz), new Vector3(1.5f, 0.8f, 0.15f), plank);
            B(new Vector3(4.45f, wy + 1.3f, zz), new Vector3(1.5f, 0.8f, 0.15f), plank);
            B(new Vector3(3.2f, wy + 0.92f, zz + 0.1f * s), new Vector3(1.1f, 0.06f, 0.12f), trim, false);   // sill
        }
        // front wall (x 1.2): doorway z -0.6..0.6, 1.9 m high
        for (int s = -1; s <= 1; s += 2) B(new Vector3(1.2f, wy + H * 0.5f, 1.4f * s), new Vector3(0.15f, H, 1.6f), plank);
        B(new Vector3(1.2f, wy + 2.15f, 0f), new Vector3(0.15f, 0.5f, 1.2f), plank);
        // pitched roof (ridge along x at 1.2 m above the walls), walkable slabs, gable fill
        float ang = Mathf.Atan2(1.2f, 2.4f) * Mathf.Rad2Deg, top = wy + H;
        for (int s = -1; s <= 1; s += 2)
            B(new Vector3(3.2f, top + 0.62f, 1.3f * s), new Vector3(4.7f, 0.14f, 2.95f), roofC, true, ang * s);
        for (int k = 0; k < 3; k++)
            for (int e = 0; e < 2; e++)
                B(new Vector3(e == 0 ? 1.2f : 5.2f, top + 0.2f + k * 0.4f, 0f), new Vector3(0.14f, 0.4f, 4.2f * (1f - k / 3f)), plankD, false);
        B(new Vector3(3.2f, top + 1.25f, 0f), new Vector3(4.8f, 0.12f, 0.16f), trim, false);   // ridge cap
        // two crates on the deck by the front wall: hop crate -> crate -> roof
        B(new Vector3(-0.3f, D + 0.5f, -2.45f), new Vector3(0.9f, 1f, 0.9f), new Color(0.72f, 0.55f, 0.3f));
        B(new Vector3(0.65f, D + 1f, -2.45f), new Vector3(0.9f, 2f, 0.9f), new Color(0.66f, 0.5f, 0.28f));
        // inside: a little table + a toy chest
        B(new Vector3(3.6f, D + 0.35f, 0.8f), new Vector3(1f, 0.08f, 0.7f), plankD, false);
        B(new Vector3(4.6f, D + 0.25f, -1.4f), new Vector3(0.7f, 0.5f, 0.5f), new Color(0.3f, 0.5f, 0.85f));
        // leafy crown above the roof (no collision)
        Color[] leaf = { new Color(0.22f, 0.45f, 0.18f), new Color(0.28f, 0.52f, 0.2f), new Color(0.18f, 0.4f, 0.2f) };
        Vector3[] cr = { new Vector3(0f, 10f, 0f), new Vector3(-2.2f, 9f, 1.8f), new Vector3(-1.8f, 9.4f, -2f), new Vector3(2.6f, 10.2f, -0.8f), new Vector3(0.8f, 11f, 1.6f) };
        for (int k = 0; k < cr.Length; k++)
            Mats.Prim(PrimitiveType.Sphere, root, cr[k], new Vector3(4.6f, 3.4f, 4.6f) * (1f - k * 0.06f), Mats.Lit(leaf[k % 3]));
        foreach (var rr in root.GetComponentsInChildren<Renderer>()) rr.receiveShadows = true;
        // climb the trunk; step out onto the deck either side of it
        Climbs.Add(TreeClimb.AddClimb(root, basePos, 0.45f, basePos.y + D, new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, -1f)));
        Spots.Add(basePos);
    }
}
