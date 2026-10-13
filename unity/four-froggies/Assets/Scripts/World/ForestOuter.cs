using System.Collections.Generic;
using UnityEngine;

// ffw (Four Froggies World, Ben: "make the forest quite a bit bigger, more trees, more room to explore"): outer woods
// grown around the existing Enchanted Forest (no new name - it is just more of that forest's edge). Layered on top:
// the ffu33 core (JetLayout.ForestMin..Max: canopy, orbs, mushrooms, logs, shafts, haze) is untouched.
//  * a belt up to Depth m outside the core rectangle, inside the existing terrain (|x|,|z| <= Lim, the edge berm included),
//    so the ground, walk area and camera already cover it - world bounds are NOT changed (the baked terrain, edge berm,
//    pond / track / base maths all key off Layout.Half);
//  * mixed broadleaf + pine, size and tint vary, sparser towards the outside (depth), two winding trails in to the core
//    and three clearings kept open;
//  * every tree is a ranch-style breakable (RanchBreak.Group + Destruct.Register(Tree) + trunk capsule): mechs crush them
//    and they regrow on the usual timers; intact ones are merged per cell by BreakBatch (same draw cost model);
//  * keeps off the track, pond, jet base, RC raceway, story pad, reserved ranch spots, and anything with a collider
//    already there (garden, treehouses, barns, signs, existing trees);
//  * Look.Mobile (phones / Quest / Tesla): ~45% of the trees, wider spacing, no outermost layer.
public static class ForestOuter
{
    public const float Depth = 42f, Lim = 186f;
    public static int Placed;
    public static readonly List<Vector2> Clearings = new List<Vector2>();

    static float Out(float x, float z)
    {
        Vector2 a = JetLayout.ForestMin, b = JetLayout.ForestMax;
        float dx = Mathf.Max(0f, Mathf.Max(a.x - x, x - b.x)), dz = Mathf.Max(0f, Mathf.Max(a.y - z, z - b.y));
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    // two trails: from the core's west + south edges out into the ranch, gently winding
    static float TrailDist(float x, float z)
    {
        Vector2 a = JetLayout.ForestMin, b = JetLayout.ForestMax;
        float zc = (a.y + b.y) * 0.5f + Mathf.Sin(x * 0.09f) * 5f;            // west trail runs along x at mid z
        float d1 = x < a.x + 2f ? Mathf.Abs(z - zc) : 1e9f;
        float xc = (a.x + b.x) * 0.5f + Mathf.Sin(z * 0.08f) * 5f;            // south trail runs along z at mid x
        float d2 = z < a.y + 2f ? Mathf.Abs(x - xc) : 1e9f;
        return Mathf.Min(d1, d2);
    }

    public static void Build()
    {
        if (Placed > 0 || LBPack.Get("tree0") == null || LBPack.Get("pine0") == null) return;
        float t0 = Time.realtimeSinceStartup;
        var r = new System.Random(4207);
        System.Func<float> R = () => (float)r.NextDouble();
        bool mob = Look.Mobile;
        Vector2 mn = JetLayout.ForestMin, mx = JetLayout.ForestMax;
        float depth = mob ? Depth * 0.7f : Depth;

        // clearings (open glades to find), in the belt
        for (int i = 0; i < 40 && Clearings.Count < 3; i++)
        {
            float x = Mathf.Lerp(mn.x - depth, Lim, R()), z = Mathf.Lerp(mn.y - depth, Lim, R());
            float o = Out(x, z);
            if (o < 8f || o > depth - 6f || !Ok(x, z)) continue;
            bool far = true; foreach (var c in Clearings) if ((c - new Vector2(x, z)).sqrMagnitude < 30f * 30f) far = false;
            if (far) Clearings.Add(new Vector2(x, z));
        }

        string[] broad = { "tree0", "tree1", "tree2" }, pines = { "pine0", "pine1" };
        Physics.SyncTransforms();   // ranch / garden / treehouse colliders made earlier are where CheckSphere sees them
        var spots = new List<Vector2>();
        int want = mob ? 70 : 160;
        float minD2 = mob ? 7.5f * 7.5f : 5.2f * 5.2f;
        for (int tries = 0; tries < 6000 && Placed < want; tries++)
        {
            float x = Mathf.Lerp(mn.x - depth, Lim, R()), z = Mathf.Lerp(mn.y - depth, Lim, R());
            float o = Out(x, z);
            if (o <= 1.5f || o > depth) continue;                                   // outside the core, inside the belt
            if (R() > Mathf.Lerp(1f, 0.35f, o / depth)) continue;                  // thins out towards the open ranch
            if (TrailDist(x, z) < 3.2f) continue;
            bool skip = false;
            foreach (var c in Clearings) if ((c - new Vector2(x, z)).sqrMagnitude < 9f * 9f) { skip = true; break; }
            if (skip) continue;
            foreach (var s2 in spots) if ((s2 - new Vector2(x, z)).sqrMagnitude < minD2) { skip = true; break; }
            if (skip || !Ok(x, z)) continue;
            float gy = Ranch.GY(x, z);
            if (Physics.CheckSphere(new Vector3(x, gy + 3f, z), 2.2f, ~0, QueryTriggerInteraction.Ignore)) continue;   // something already there

            bool pine = R() < 0.4f;
            string pk = pine ? pines[r.Next(pines.Length)] : broad[r.Next(broad.Length)];
            LBPack p = LBPack.Get(pk);
            if (p == null) continue;
            float s = Mathf.Lerp(1.7f, 1.0f, o / depth) + R() * 0.5f;           // tall near the core, ranch-size at the rim
            Color tint = Color.Lerp(new Color(0.82f, 0.9f, 0.8f), Color.white, o / depth) * (0.92f + R() * 0.1f); tint.a = 1f;
            Transform tg = RanchBreak.Group("Tree", new Vector3(x, gy - 0.15f, z));
            p.SpawnAll(tg, Vector3.zero, s, tint, R() * 360f);
            var col = new GameObject("TreeCol").AddComponent<CapsuleCollider>();
            col.transform.SetParent(tg, false);
            col.transform.position = new Vector3(x, gy + 2f * s, z);
            col.radius = 0.35f * s; col.height = 4f * s;
            Destruct.Register(tg.gameObject, BreakKind.Tree);
            spots.Add(new Vector2(x, z));
            Placed++;
        }

        // a few mossy rocks round each clearing (breakable, regrow)
        string[] rocks = { "qrock0", "qrock1" };
        foreach (var c in Clearings)
            for (int i = 0; i < (mob ? 2 : 4); i++)
            {
                float a = R() * 6.283f, x = c.x + Mathf.Cos(a) * 7f, z = c.y + Mathf.Sin(a) * 7f;
                LBPack p = LBPack.Get(rocks[i % 2]);
                if (p == null || !Ok(x, z)) continue;
                float s = 0.4f + R() * 0.5f, gy = Ranch.GY(x, z);
                Transform rg = RanchBreak.Group("Rock", new Vector3(x, gy - 0.25f * s, z));
                p.SpawnAll(rg, Vector3.zero, s, Color.white, R() * 360f);
                var sc = new GameObject("RockCol").AddComponent<SphereCollider>();
                sc.transform.SetParent(rg, false);
                sc.transform.position = new Vector3(x, gy + 0.3f * s, z); sc.radius = 1.1f * s;
                Destruct.Register(rg.gameObject, BreakKind.Rock);
            }
        Debug.Log("ForestOuter: " + Placed + " trees, " + Clearings.Count + " clearings in " + ((Time.realtimeSinceStartup - t0) * 1000f).ToString("0") + " ms" + (mob ? " (mobile)" : ""));
    }

    // the same keep-out rules as Ranch.MeshTrees
    static bool Ok(float x, float z)
    {
        if (Mathf.Abs(x) > Lim || Mathf.Abs(z) > Lim) return false;
        if (Layout.PondQ(x, z) < 1.3f) return false;
        if (Layout.RoadDist(x, z) < Layout.TrackW + 4f) return false;
        if (x > -30f && x < 30f && z > 30f && z < 70f) return false;
        if (Worlds.StageEOn && RanchLife.Reserved(x, z)) return false;
        if ((new Vector2(x, z) - StorySet.PadXZ).sqrMagnitude < 20f * 20f) return false;
        if (JetLayout.Reserved(x, z) || RcTrack.Reserved(x, z)) return false;
        return true;
    }
}
