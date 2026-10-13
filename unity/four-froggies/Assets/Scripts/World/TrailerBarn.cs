using System.Collections.Generic;
using UnityEngine;

// ffu31: the TRAILER BARN - a second barn on the ranch with big slid-open doors, a concrete floor on a foundation pad,
// solid colliders, and four modern camper trailers parked nose-out (hitch balls just inside the doorway, so a truck
// backs up to them). Site: north of the rally track's centre crossing, doors facing south down to it (x ~40, z ~158):
// flat, tree-free by construction (trees / treehouses need Flatness >= 0.6 / 0.8; that strip is < 0.6), clear of the
// figure-eight, the loop, the treehouses, the robot pad, the pasture, the garage yard, the ffu30 RC raceway + MX line
// (east, ~140, 28) and the ffu33 jet-base / garden areas. First clear candidate of a small fixed grid wins.
// Not registered with Destruct: missiles / mech stomps leave it standing.
public static class TrailerBarn
{
    public static bool Built;
    public static Vector3 Centre;          // floor centre (world)
    public static float Yaw = 180f;        // barn local +z (the doors) faces south, down to the rally track
    public static float FloorY;
    public const float Wd = 30f, Dp = 20f, WallH = 6.2f, RidgeH = 4.2f;
    public static readonly List<Trailer> Trailers = new List<Trailer>();
    static Transform root;
    public static Vector3 W(float lx, float ly, float lz) { return Centre + Quaternion.Euler(0f, Yaw, 0f) * new Vector3(lx, ly, lz); }
    public static Vector3 Dir(float lx, float lz) { return Quaternion.Euler(0f, Yaw, 0f) * new Vector3(lx, 0f, lz); }
    public static readonly float[] BayX = { -10.5f, -3.5f, 3.5f, 10.5f };

    // world-space keep-out for other systems (robots, AI) - the barn box + apron
    public static bool Inside(Vector3 p)
    {
        if (!Built) return false;
        Vector3 l = Quaternion.Euler(0f, -Yaw, 0f) * (p - Centre);
        return Mathf.Abs(l.x) < Wd * 0.5f + 0.5f && l.z > -Dp * 0.5f - 0.5f && l.z < Dp * 0.5f + 0.5f;
    }

    public static void Build()
    {
        if (Built) return;
        Physics.SyncTransforms();
        PickSite();
        root = new GameObject("TrailerBarn").transform;
        root.SetPositionAndRotation(Centre, Quaternion.Euler(0f, Yaw, 0f));
        Barn();
        MeshMerge.Merge(root, true);
        // the four trailers, nose (hitch) toward the doors
        for (int i = 0; i < 4; i++)
        {
            Trailer.Spec s = TrailerModels.Specs[i];
            float ballZ = Dp * 0.5f - 1.6f;
            float cz = ballZ - s.tongue - s.len * 0.5f;
            Vector3 p = W(BayX[i], 0.02f, cz);
            Trailer t = TrailerModels.Build(s, p, Yaw);
            Trailers.Add(t);
            if (s.hauler) HaulerSpot(t);
        }
        // a hitch receiver + ball on every truck that can tow (the Monster Truck gets a drop bar down to ball height)
        foreach (var v in Vehicle.All) if (v != null && TrailerTow.CanTow(v)) Receiver(v);
        Built = true;
        Debug.Log("TrailerBarn: built at " + Centre.ToString("0.0") + " yaw " + Yaw + " floor " + FloorY.ToString("0.00") + ", " + Trailers.Count + " trailers");
    }

    static void PickSite()
    {
        // footprint (+ apron and door rails) in barn-local space, sampled on a grid; the candidates all face west
        float best = 1e9f; Vector3 bestC = new Vector3(40f, 0f, 158f);
        bool found = false;
        Vector2[] cands = { new Vector2(40f, 158f), new Vector2(36f, 158f), new Vector2(44f, 158f), new Vector2(40f, 156f), new Vector2(32f, 157f), new Vector2(48f, 157f), new Vector2(40f, 160f), new Vector2(28f, 156f), new Vector2(52f, 156f) };
        foreach (Vector2 c in cands)
        {
            if (!Clear(c)) continue;
            float lo = 1e9f, hi = -1e9f;
            for (float lx = -16f; lx <= 16f; lx += 4f)
                for (float lz = -11f; lz <= 14f; lz += 3f)
                {
                    Vector3 w = new Vector3(c.x, 0f, c.y) + Quaternion.Euler(0f, Yaw, 0f) * new Vector3(lx, 0f, lz);
                    float g = Ranch.GY(w.x, w.z);
                    lo = Mathf.Min(lo, g); hi = Mathf.Max(hi, g);
                }
            best = hi - lo; bestC = new Vector3(c.x, 0f, c.y); found = true;
            break;
        }
        if (!found) Debug.Log("TrailerBarn: no clear candidate - using the first one as is");
        // floor height: the highest ground under the barn itself
        float top = -1e9f, low = 1e9f;
        for (float lx = -15f; lx <= 15f; lx += 2.5f)
            for (float lz = -10f; lz <= 10f; lz += 2.5f)
            {
                Vector3 w = bestC + Quaternion.Euler(0f, Yaw, 0f) * new Vector3(lx, 0f, lz);
                float g = Ranch.GY(w.x, w.z); top = Mathf.Max(top, g); low = Mathf.Min(low, g);
            }
        FloorY = top + 0.15f;
        lowY = low;
        Centre = new Vector3(bestC.x, FloorY, bestC.z);
        Debug.Log("TrailerBarn: site " + bestC.ToString("0") + " relief " + best.ToString("0.00") + (found ? "" : " (fallback: first candidate, nothing removed)"));
    }
    static float lowY;

    static bool Clear(Vector2 c)
    {
        // keep-outs: the track (and its flat apron), the loop branch, the pond, the pasture / mech rows / mission pad
        Quaternion q = Quaternion.Euler(0f, Yaw, 0f);
        // the barn + door rails (|lx| 22, lz -11..11) and the apron in front of the doors (|lx| 15, lz 11..16)
        for (float lx = -22f; lx <= 22f; lx += 2f)
            for (float lz = -11f; lz <= 16f; lz += 1.5f)
            {
                if (lz > 11f && Mathf.Abs(lx) > 15f) continue;
                Vector3 w = new Vector3(c.x, 0f, c.y) + q * new Vector3(lx, 0f, lz);
                if (Mathf.Abs(w.x) > 170f || Mathf.Abs(w.z) > 170f) return false;
                if (Layout.RoadDist(w.x, w.z) < Layout.TrackW * 0.5f + 4f) return false;
                if (Layout.PondQ(w.x, w.z) < 1.3f) return false;
                if (Worlds.StageEOn && RanchLife.Reserved(w.x, w.z)) return false;
                if (RobotNav.Blocked(w.x, w.z)) return false;
            }
        foreach (var s in Treehouse.Spots) if ((new Vector2(s.x, s.z) - c).sqrMagnitude < 40f * 40f) return false;
        // the ffu30 RC raceway pad + MX jump line (east) and anything else that may land out there
        Vector2[] keep = { new Vector2(140f, 28f), new Vector2(141f, 2f), new Vector2(127f, 30f), new Vector2(60f, 32f) };
        foreach (var k in keep) if ((k - c).sqrMagnitude < 45f * 45f) return false;
        foreach (var t in Ranch.TreeSpots) { Vector3 l = Quaternion.Euler(0f, -Yaw, 0f) * new Vector3(t.x - c.x, 0f, t.y - c.y); if (Mathf.Abs(l.x) < 23.5f && l.z > -10.6f && l.z < 16.5f) return false; }
        float gy = Ranch.GY(c.x, c.y);
        Vector3 ctr = new Vector3(c.x, gy + 5f, c.y) + q * new Vector3(0f, 0f, 3f);
        foreach (var col in Physics.OverlapBox(ctr, new Vector3(23f, 4.8f, 13f), q))
            if (!(col is TerrainCollider) && !col.isTrigger) return false;
        return true;
    }

    static void ClearTrees(Vector2 c)
    {
        Quaternion q = Quaternion.Euler(0f, Yaw, 0f);
        float gy = Ranch.GY(c.x, c.y);
        Vector3 ctr = new Vector3(c.x, gy + 5f, c.y) + q * new Vector3(0f, 0f, 2.5f);
        foreach (var col in Physics.OverlapBox(ctr, new Vector3(23f, 4.8f, 13.5f), q))
        {
            if (col is TerrainCollider || col.isTrigger || col.attachedRigidbody != null) continue;
            Transform t = col.transform;
            while (t.parent != null && t.parent.name != "RanchStatic" && t.parent.parent != null) t = t.parent;
            if (t.name.StartsWith("Tree") || t.name.StartsWith("Rock") || col.name.StartsWith("TreeCol") || col.name.StartsWith("RockCol")) t.gameObject.SetActive(false);
        }
    }

    // ---------------- the barn ----------------
    static readonly Color Red = new Color(0.62f, 0.13f, 0.11f), RedD = new Color(0.45f, 0.09f, 0.08f), White = new Color(0.95f, 0.94f, 0.9f);
    static readonly Color RoofM = new Color(0.27f, 0.29f, 0.31f), Conc = new Color(0.6f, 0.6f, 0.58f);

    static GameObject B(Vector3 p, Vector3 s, Material m, bool col = true, Vector3 e = default(Vector3))
    {
        return Mats.Prim(PrimitiveType.Cube, root, p, s, e, m, col);
    }

    static void Barn()
    {
        float hw = Wd * 0.5f, hd = Dp * 0.5f;
        Material red = Mats.Lit(Red), redD = Mats.Lit(RedD), white = Mats.Lit(White), roof = Mats.Steel(RoofM), conc = Mats.Lit(Conc);
        // foundation pad + floor (top at local y 0)
        float depth = FloorY - lowY + 0.6f;
        B(new Vector3(0f, -depth * 0.5f, 0.6f), new Vector3(Wd + 1.2f, depth, Dp + 1.6f), conc);
        // apron ramp out of the doors down to the ground
        Vector3 aw = W(0f, 0f, hd + 7f);
        float gy = Ranch.GY(aw.x, aw.z);
        float drop = Mathf.Max(0.02f, FloorY - gy + 0.05f);
        float alen = Mathf.Clamp(drop / Mathf.Tan(7f * Mathf.Deg2Rad), 4f, 12f);
        float ang = Mathf.Atan2(drop, alen) * Mathf.Rad2Deg;
        float sl = Mathf.Sqrt(alen * alen + drop * drop);
        B(new Vector3(0f, -drop * 0.5f - 0.15f * Mathf.Cos(ang * Mathf.Deg2Rad), hd + 1.4f + alen * 0.5f), new Vector3(Wd - 1f, 0.3f, sl), conc, true, new Vector3(ang, 0f, 0f));
        // painted bay lines + yellow nose stops on the floor
        Material yel = Mats.Lit(new Color(0.95f, 0.78f, 0.12f));
        for (int i = 0; i < 5; i++) B(new Vector3(-14f + i * 7f, 0.006f, 0f), new Vector3(0.12f, 0.012f, Dp - 3f), yel, false);
        // walls: back + sides full height, front = corner posts + header beam over one wide opening
        float wt = 0.3f;
        B(new Vector3(0f, WallH * 0.5f, -hd), new Vector3(Wd, WallH, wt), red);
        for (int k = -1; k <= 1; k += 2) B(new Vector3(hw * k, WallH * 0.5f, 0f), new Vector3(wt, WallH, Dp), red);
        for (int k = -1; k <= 1; k += 2) B(new Vector3((hw - 0.7f) * k, WallH * 0.5f, hd), new Vector3(1.4f, WallH, wt), red);
        B(new Vector3(0f, WallH - 0.4f, hd), new Vector3(Wd, 0.8f, wt), red);
        // board-and-batten: vertical battens on the outside of the side + back walls
        for (float x = -hw + 0.6f; x < hw; x += 1.2f) B(new Vector3(x, WallH * 0.5f, -hd - 0.17f), new Vector3(0.08f, WallH, 0.05f), redD, false);
        for (int k = -1; k <= 1; k += 2) for (float z = -hd + 0.6f; z < hd; z += 1.2f) B(new Vector3((hw + 0.17f) * k, WallH * 0.5f, z), new Vector3(0.05f, WallH, 0.08f), redD, false);
        // white trim: corners, eaves, header edge
        for (int a = -1; a <= 1; a += 2) for (int b = -1; b <= 1; b += 2) B(new Vector3((hw + 0.12f) * a, WallH * 0.5f, (hd + 0.12f) * b), new Vector3(0.3f, WallH, 0.3f), white, false);
        B(new Vector3(0f, WallH - 0.85f, hd + 0.17f), new Vector3(Wd - 2.6f, 0.12f, 0.06f), white, false);
        B(new Vector3(0f, 0.25f, -hd + 0.2f), new Vector3(Wd - 0.6f, 0.5f, 0.12f), Mats.Lit(new Color(0.3f, 0.3f, 0.3f)), false);   // kick board
        // gable roof (ridge front to back), overhangs, colliders on both slabs
        float run = hw + 0.8f, rise = RidgeH * run / hw;
        float rl = Mathf.Sqrt(run * run + rise * rise), ra = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
        for (int k = -1; k <= 1; k += 2)
            RoofBreak.Add(B(new Vector3(k * run * 0.5f, WallH + rise * 0.5f - 0.8f * RidgeH / hw + 0.12f, 0f), new Vector3(rl, 0.18f, Dp + 1.6f), roof, true, new Vector3(0f, 0f, -k * ra)), 1, 4);   // ffu39: breakable
        B(new Vector3(0f, WallH + RidgeH + 0.1f, 0f), new Vector3(0.5f, 0.2f, Dp + 1.7f), roof, false);   // ridge cap
        // gable ends (triangles, both faces)
        for (int k = -1; k <= 1; k += 2) Gable(new Vector3(0f, WallH, hd * k + 0.02f * k), hw, RidgeH, red);
        // gable trim + loft door + hay hood on the front
        for (int k = -1; k <= 1; k += 2)
            B(new Vector3(k * hw * 0.5f, WallH + RidgeH * 0.5f, hd + 0.2f), new Vector3(Mathf.Sqrt(hw * hw + RidgeH * RidgeH), 0.22f, 0.08f), white, false, new Vector3(0f, 0f, -k * Mathf.Atan2(RidgeH, hw) * Mathf.Rad2Deg));
        // door rail on the front + four big sliding doors, slid wide open past both corners
        B(new Vector3(0f, WallH - 0.05f, hd + 0.35f), new Vector3(Wd + 14f, 0.16f, 0.16f), Mats.Steel(new Color(0.3f, 0.3f, 0.32f)), false);
        for (int k = -1; k <= 1; k += 2)
        {
            B(new Vector3((hw + 7.2f) * k, (WallH - 0.1f) * 0.5f - 0.3f, hd + 0.35f), new Vector3(0.22f, WallH + 0.3f, 0.22f), Mats.Lit(new Color(0.3f, 0.3f, 0.32f)), true);   // rail post
            Door(new Vector3((hw + 3.6f) * k, 0f, hd + 0.5f), Red, White);
            Door(new Vector3((hw + 2.9f) * k, 0f, hd + 0.75f), Red, White);
        }
        // inside: roof trusses + warm strip lights, workbench, tyre rack, barrels, a charging post per bay
        Material wood = Mats.Lit(new Color(0.5f, 0.36f, 0.22f)), lamp = Mats.Unlit(new Color(1f, 0.95f, 0.8f));
        for (float z = -hd + 2.5f; z < hd - 1f; z += 5f)
        {
            B(new Vector3(0f, WallH - 0.15f, z), new Vector3(Wd - 0.6f, 0.25f, 0.2f), wood, false);
            B(new Vector3(0f, WallH - 0.33f, z + 0.3f), new Vector3(Wd * 0.7f, 0.06f, 0.18f), lamp, false);
        }
        B(new Vector3(-12.5f, 0.45f, -hd + 0.75f), new Vector3(3.5f, 0.9f, 0.9f), wood, true);           // workbench
        B(new Vector3(-12.5f, 1.4f, -hd + 0.4f), new Vector3(3.5f, 1.0f, 0.06f), Mats.Lit(new Color(0.75f, 0.72f, 0.65f)), false);   // pegboard
        for (int i = 0; i < 4; i++)
            Mats.Prim(PrimitiveType.Cylinder, root, new Vector3(12.8f, 0.4f + i * 0.0f, -hd + 0.8f + i * 0.9f), new Vector3(0.8f, 0.14f, 0.8f), new Vector3(0f, 0f, 90f), Mats.Lit(new Color(0.08f, 0.08f, 0.09f)));
        for (int i = 0; i < 3; i++)
            Mats.Prim(PrimitiveType.Cylinder, root, new Vector3(-6f + i * 0.8f, 0.45f, -hd + 0.7f), new Vector3(0.6f, 0.45f, 0.6f), Mats.Lit(i == 1 ? new Color(0.15f, 0.35f, 0.7f) : new Color(0.75f, 0.2f, 0.12f)), true);
        Material post = Mats.Lit(new Color(0.92f, 0.92f, 0.9f)), glow = Mats.Unlit(new Color(0.2f, 0.95f, 0.45f));
        for (int i = 0; i < 4; i++)
        {
            B(new Vector3(BayX[i] + 3.2f, 0.75f, -hd + 1.2f), new Vector3(0.3f, 1.5f, 0.3f), post, true);
            B(new Vector3(BayX[i] + 3.2f, 1.25f, -hd + 1.36f), new Vector3(0.16f, 0.1f, 0.02f), glow, false);
        }
        // sign on the front gable
        Ranch.Sign(W(0f, WallH + 1.6f, hd + 0.35f), Yaw + 180f, "TRAILER BARN\n<size=22>back a truck up to a hitch</size>", new Color(0.1f, 0.18f, 0.12f), 7.5f, 1.9f);
    }

    static void Door(Vector3 c, Color red, Color white)
    {
        Material r = Mats.Lit(red), w = Mats.Lit(white);
        float dw = 7.0f, dh = WallH - 0.6f;
        B(c + new Vector3(0f, dh * 0.5f + 0.15f, 0f), new Vector3(dw, dh, 0.14f), r, true);
        Vector3 f = c + new Vector3(0f, 0f, 0.09f);
        B(f + new Vector3(0f, dh + 0.1f, 0f), new Vector3(dw, 0.2f, 0.05f), w, false);
        B(f + new Vector3(0f, 0.25f, 0f), new Vector3(dw, 0.2f, 0.05f), w, false);
        B(f + new Vector3(dw * 0.5f - 0.1f, dh * 0.5f + 0.15f, 0f), new Vector3(0.2f, dh, 0.05f), w, false);
        B(f + new Vector3(-dw * 0.5f + 0.1f, dh * 0.5f + 0.15f, 0f), new Vector3(0.2f, dh, 0.05f), w, false);
        B(f + new Vector3(0f, dh * 0.5f + 0.15f, 0f), new Vector3(dw, 0.16f, 0.05f), w, false);
        // a big X in each half of the door
        float cell = dh * 0.5f - 0.1f, diag = Mathf.Sqrt(dw * dw + cell * cell), a = Mathf.Atan2(cell, dw) * Mathf.Rad2Deg;
        for (int k = -1; k <= 1; k += 2)
            for (int m = -1; m <= 1; m += 2)
                B(f + new Vector3(0f, dh * 0.5f + 0.15f + m * dh * 0.25f, 0f), new Vector3(diag - 0.3f, 0.14f, 0.04f), w, false, new Vector3(0f, 0f, k * a));
        // hangers on the rail
        for (int k = -1; k <= 1; k += 2) B(c + new Vector3(k * dw * 0.35f, WallH - 0.25f, -0.05f), new Vector3(0.25f, 0.4f, 0.1f), Mats.Lit(new Color(0.2f, 0.2f, 0.22f)), false);
    }

    static void Gable(Vector3 baseCentre, float hw, float h, Material m)
    {
        var mesh = new Mesh();
        Vector3 a = baseCentre + new Vector3(-hw, 0f, 0f), b = baseCentre + new Vector3(hw, 0f, 0f), c = baseCentre + new Vector3(0f, h, 0f);
        mesh.vertices = new[] { a, b, c, a, b, c };
        mesh.triangles = new[] { 0, 2, 1, 3, 4, 5 };
        mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.forward, Vector3.forward, Vector3.forward };
        mesh.RecalculateBounds();
        var go = new GameObject("Gable");
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = m;
    }

    static void Receiver(Vehicle v)
    {
        Vector3 h = Trailer.HitchLocal(v);
        BoxCollider b = v.body;
        float bumperY = b != null ? Mathf.Max(0.55f, b.center.y - b.size.y * 0.5f + 0.1f) : 0.6f;
        Transform n = Mats.Node(v.transform, "HitchReceiver", Vector3.zero);
        Material dark = Mats.Lit(new Color(0.1f, 0.1f, 0.11f)), steel = Mats.Steel(new Color(0.7f, 0.71f, 0.73f));
        float z0 = h.z + 0.75f;
        if (bumperY > 0.9f)
            Mats.Prim(PrimitiveType.Cube, n, new Vector3(0f, (bumperY + h.y) * 0.5f, z0), new Vector3(0.12f, bumperY - h.y + 0.1f, 0.12f), dark);
        Mats.Prim(PrimitiveType.Cube, n, new Vector3(0f, h.y - 0.03f, (z0 + h.z) * 0.5f), new Vector3(0.09f, 0.09f, z0 - h.z + 0.1f), dark);
        Mats.Prim(PrimitiveType.Sphere, n, h + new Vector3(0f, 0.04f, 0f), Vector3.one * 0.1f, steel);
        Mats.SetLayer(n.gameObject, Vehicle.VehicleLayer);
    }

    // ramp-door hotspot for a toy hauler: lower / raise the ramp, ride a stowed bike out
    static void HaulerSpot(Trailer t)
    {
        var h = new Hotspot { pos = t.RampFoot(0.8f), radius = 2.6f, label = "" };
        h.enabled = f => f.vehicle == null && f.world == WorldId.Ranch && t != null && t.spec.hauler && t.move == null && (t.CanMoveRamp || t.RampDown);
        h.dynLabel = f =>
        {
            if (!t.RampDown) return t.rampK > 0.03f ? "Ramp moving..." : "Lower the " + t.spec.shortName + " ramp" + (t.Stowed > 0 ? " (" + t.FirstStowed.Title + " inside)" : "");
            Vehicle b = t.FirstStowed;
            if (b != null) return "Ride the " + b.Title + " out";
            return "Close the " + t.spec.shortName + " ramp";
        };
        h.act = f =>
        {
            if (!t.RampDown) { if (t.rampK < 0.03f && t.CanMoveRamp) { t.SetRamp(true); Sfx.Play(Sfx.Click, 0.5f); } return; }
            if (t.FirstStowed != null) { t.StartUnload(f); return; }
            if (t.CanMoveRamp) { t.SetRamp(false); Sfx.Play(Sfx.Click, 0.5f); }
        };
        Interact.All.Add(h);
        t.rampSpot = h;
    }
}
