using System.Collections.Generic;
using UnityEngine;

// ffu21 TREE CLIMBING (frogs, cats, dogs - any on-foot character a player controls).
// Start: stand right at a trunk and deliberately push the stick / keys INTO it for ~0.8 s (a small ring fills on the
// HUD); walking past or brushing a tree never starts it. Climbing: stick up / down climbs, left / right circles the trunk,
// with a scramble-hop animation. At the top, keep pushing up to step out onto a branch (real walkable branch colliders).
// Jump / A leaps off in a nice arc; pushing down at the bottom lets go. If the tree gets blown up the froggy hops clear.
public static class TreeClimb
{
    public class Tree
    {
        public Transform t; public Vector3 basePos; public float r, topY, s;
        public readonly List<Vector3> branchDirs = new List<Vector3>();
        public float branchTop;
        Breakable b;
        public bool Gone
        {
            get
            {
                if (t == null) return true;
                if (b == null) b = t.GetComponent<Breakable>();
                return b != null && b.state != 0;
            }
        }
        public float TopH { get { return topY - basePos.y; } }
    }
    public static readonly List<Tree> Trees = new List<Tree>();

    public static void AddTree(Transform tg, Vector3 basePos, float trunkR, float s, bool conifer)
    {
        var tr = new Tree { t = tg, basePos = basePos, r = trunkR, s = s };
        float h = 6f * s;
        bool first = true; Bounds bb = new Bounds();
        foreach (var mr in tg.GetComponentsInChildren<MeshRenderer>()) { if (first) { bb = mr.bounds; first = false; } else bb.Encapsulate(mr.bounds); }
        if (!first) h = Mathf.Max(3f, bb.max.y - basePos.y);
        tr.topY = basePos.y + Mathf.Clamp(h * (conifer ? 0.45f : 0.55f), 2.4f, 9f);
        tr.branchTop = tr.topY;
        // three walkable branches sticking out of the trunk at the climb top (bark-brown, real box colliders)
        Material bark = Mats.Lit(new Color(0.38f, 0.26f, 0.16f));
        float a0 = (tg.position.x * 13.7f + tg.position.z * 7.1f) % 360f;
        float len = Mathf.Clamp(2.6f * s, 2f, 3.4f);
        for (int i = 0; i < 3; i++)
        {
            float a = (a0 + i * 120f) * Mathf.Deg2Rad;
            Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 c = basePos + d * (trunkR + len * 0.5f - 0.1f) + Vector3.up * (tr.topY - 0.14f);
            var br = Mats.Prim(PrimitiveType.Cube, tg, Vector3.zero, new Vector3(0.5f, 0.28f, len), bark, true);
            br.name = "Branch";
            br.transform.position = c;
            br.transform.rotation = Quaternion.LookRotation(d) * Quaternion.Euler(-4f, 0f, 0f);
            tr.branchDirs.Add(d);
        }
        Trees.Add(tr);
    }

    // nearest standing tree whose trunk surface is within `range` metres (horizontal) of p
    public static Tree Nearest(Vector3 p, float range, out float surfDist)
    {
        Tree best = null; surfDist = range;
        foreach (var t in Trees)
        {
            float dx = p.x - t.basePos.x, dz = p.z - t.basePos.z;
            if (dx * dx + dz * dz > (range + 1.5f) * (range + 1.5f)) continue;
            float d = Mathf.Sqrt(dx * dx + dz * dz) - t.r;
            if (d < surfDist && p.y < t.topY + 1f && p.y > t.basePos.y - 2f && !t.Gone) { surfDist = d; best = t; }
        }
        return best;
    }
}

public partial class Frog
{
    public float climbProgress;           // 0..1 HUD ring while pushing into a trunk
    public bool demoPush;                 // demo / probe: counts as pushing into the nearest trunk
    public Vector2 demoClimbMove;         // demo / probe: climbing stick
    TreeClimb.Tree climbTree, pushTree;
    float climbA, climbH, climbAnim, climbCool, climbSpd;
    public bool Climbing { get { return climbTree != null; } }
    const float ClimbHold = 0.8f;

    string ClimbKey(string pad, string touch, string keys) { return inputKind == InputKind.Gamepad ? pad : inputKind == InputKind.Touch ? touch : keys; }

    // after Walk: the "hold toward the tree" detection + hint
    void ClimbDetect(float dt)
    {
        climbCool -= dt;
        if (!human || world != WorldId.Ranch || vehicle != null || swimming || chute || remote != null || climbCool > 0f) { climbProgress = 0f; pushTree = null; return; }
        Vector3 p = transform.position;
        float d;
        var tree = TreeClimb.Nearest(p, 1.6f, out d);
        if (tree == null) { climbProgress = Mathf.Max(0f, climbProgress - dt * 3f); if (climbProgress <= 0f) pushTree = null; return; }
        Vector3 wish = Vector3.zero;
        if (input.move.sqrMagnitude > 0.0001f) wish = Quaternion.Euler(0f, camYaw, 0f) * new Vector3(input.move.x, 0f, input.move.y);
        Vector3 to = tree.basePos - p; to.y = 0f;
        bool pushing = (demoPush && d < 0.9f) || wish.sqrMagnitude > 0.3f && to.sqrMagnitude > 1e-4f && Vector3.Dot(wish.normalized, to.normalized) > 0.75f
                       && d < 0.8f && cc.isGrounded && p.y < tree.basePos.y + 1.6f;
        if (pushing)
        {
            if (pushTree != tree) { pushTree = tree; climbProgress = 0f; }
            climbProgress += dt / ClimbHold;
            if (climbProgress >= 1f) { StartClimb(tree); return; }
        }
        else climbProgress = Mathf.Max(0f, climbProgress - dt * 3f);
        if (string.IsNullOrEmpty(prompt) && d < 1.3f)
            prompt = climbProgress > 0.02f ? "Climbing..." : "Hold " + ClimbKey("the stick", "the stick", "W / arrows") + " toward the tree to climb";
    }

    void StartClimb(TreeClimb.Tree t)
    {
        climbTree = t; climbProgress = 0f; pushTree = null;
        Vector3 rad = transform.position - t.basePos; rad.y = 0f;
        if (rad.sqrMagnitude < 1e-4f) rad = Vector3.forward;
        climbA = Mathf.Atan2(rad.z, rad.x);
        climbH = Mathf.Clamp(transform.position.y - t.basePos.y + 0.3f, 0.3f, 1.2f);
        climbAnim = 0f;
        cc.enabled = false;
        vel = Vector3.zero; planar = Vector3.zero;
        Sfx.Play(Sfx.Hop, 0.55f, 1.2f);
        Toast("Climbing! " + ClimbKey("Stick", "Stick", "W / S") + " up / down, " + ClimbKey("left / right", "left / right", "A / D") + " go round, " + ClimbKey("A", "A", "SPACE") + " leap off", 3f);
    }

    // returns true while climbing (Walk is skipped)
    bool ClimbTick(float dt)
    {
        if (climbTree == null) return false;
        var t = climbTree;
        float R = t.r + 0.3f;
        Vector3 rad = new Vector3(Mathf.Cos(climbA), 0f, Mathf.Sin(climbA));
        Vector3 expect = t.basePos + rad * R + Vector3.up * climbH;
        if (t.Gone) { ClimbExit(rad * 2.5f + Vector3.up * 3f, "The tree got blown up - you hopped clear!"); return false; }
        if (world != WorldId.Ranch || vehicle != null || passengerOf != null || (transform.position - expect).sqrMagnitude > 9f) { ClimbExit(Vector3.zero, null); return false; }
        float up = input.move.y, side = input.move.x;
        if (demoClimbMove != Vector2.zero) { up = demoClimbMove.x == 0f && demoClimbMove.y == 0f ? up : demoClimbMove.y; side = demoClimbMove.x; }
        float top = t.TopH - 0.55f;
        // leap off: a nice arc away from the trunk
        if (input.hop)
        {
            ClimbExit(rad * 5.5f + Vector3.up * 7.5f, null);
            Sfx.Play(Sfx.Hop, 0.7f, 0.9f);
            FX.Sparkle(transform.position, new Color(0.7f, 1f, 0.6f), 6);
            return false;
        }
        // let go at the bottom
        if (climbH <= 0.35f && up < -0.5f) { ClimbExit(rad * 1.5f, null); return false; }
        // step out onto the nearest branch at the top
        if (climbH >= top - 0.05f && up > 0.5f && t.branchDirs.Count > 0)
        {
            Vector3 bd = t.branchDirs[0]; float bestDot = -2f;
            foreach (var d in t.branchDirs) { float k = Vector3.Dot(d, rad); if (k > bestDot) { bestDot = k; bd = d; } }
            transform.position = t.basePos + bd * (t.r + 0.85f) + Vector3.up * (t.branchTop + 0.1f);
            yaw = Mathf.Atan2(bd.x, bd.z) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            climbTree = null; cc.enabled = true; vel = Vector3.zero; planar = Vector3.zero; climbCool = 0.8f;
            Toast("Up in the branches! Walk out along one - " + ClimbKey("A", "A", "SPACE") + " to jump down", 3f);
            Sfx.Play(Sfx.Hop, 0.5f, 1.3f);
            return false;
        }
        float spd = Mathf.Sqrt(up * up + side * side);
        climbH = Mathf.Clamp(climbH + up * 2.6f * dt, 0.3f, top);
        climbA += side * 1.9f / R * dt;
        climbSpd = Mathf.MoveTowards(climbSpd, spd, dt * 6f);
        // scramble: little hops up the bark while moving
        if (climbSpd > 0.1f)
        {
            float before = climbAnim;
            climbAnim += dt * (5f + 4f * climbSpd);
            if (Mathf.Floor(before / Mathf.PI) != Mathf.Floor(climbAnim / Mathf.PI))
            {
                if (human) Sfx.Play(Sfx.Pick(Sfx.Foot) ?? Sfx.Step, 0.25f, Random.Range(1.2f, 1.5f));
                if (Random.value < 0.5f) FX.Dust(transform.position + Vector3.up * 0.3f, 1f);
            }
        }
        float bob = Mathf.Abs(Mathf.Sin(climbAnim)) * 0.16f * climbSpd;
        rad = new Vector3(Mathf.Cos(climbA), 0f, Mathf.Sin(climbA));
        Vector3 face = -rad;
        transform.position = t.basePos + rad * (R + bob * 0.4f) + Vector3.up * (climbH + bob);
        yaw = Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg;
        // belly to the bark, nose up the trunk, a little wiggle while scrambling
        transform.rotation = Quaternion.LookRotation(face) * Quaternion.Euler(-72f + Mathf.Sin(climbAnim * 2f) * 6f * climbSpd, 0f, Mathf.Sin(climbAnim) * 8f * climbSpd);
        model.Animate(climbSpd * 3.2f, false, false, false, dt);
        prompt = ClimbKey("Stick", "Stick", "W / S") + " climb, " + ClimbKey("left / right", "left / right", "A / D") + " go round  ·  " + ClimbKey("A", "A", "SPACE") + " leap off" + (climbH >= top - 0.05f ? "  ·  push UP to step onto the branches" : climbH <= 0.35f ? "  ·  push DOWN to let go" : "");
        return true;
    }

    void ClimbExit(Vector3 launch, string say)
    {
        if (climbTree == null) return;
        Vector3 rad = new Vector3(Mathf.Cos(climbA), 0f, Mathf.Sin(climbA));
        climbTree = null;
        transform.rotation = Quaternion.Euler(0f, yaw + 180f, 0f);
        yaw += 180f;
        transform.position += rad * 0.35f;
        cc.enabled = vehicle == null && passengerOf == null;
        vel = launch; planar = Vector3.zero;
        climbCool = 0.9f;
        if (say != null) Toast(say, 3f);
    }

    // demo / probe: put this froggy on a tree (h = height above its base; 999 = the top, stepped out onto a branch)
    public void DemoClimb(TreeClimb.Tree t, float h, float ang)
    {
        if (t == null) return;
        if (vehicle != null) ExitVehicle();
        StartClimb(t);
        climbA = ang;
        // ffu21c: put the froggy on the bark at the requested height first - ClimbTick lets go if it is > 3 m from there
        climbH = Mathf.Clamp(h, 0.3f, t.TopH - 0.55f);
        transform.position = t.basePos + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (t.r + 0.3f) + Vector3.up * climbH;
        if (h >= 999f) { climbH = t.TopH - 0.55f; input.move = new Vector2(0f, 1f); ClimbTick(0.02f); input = new PIn(); return; }
        climbH = Mathf.Clamp(h, 0.3f, t.TopH - 0.55f);
    }
}
