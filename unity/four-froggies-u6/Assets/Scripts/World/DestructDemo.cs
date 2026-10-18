using UnityEngine;

// ffu21 probe shots (?ffdemo=1&ffshot=...):
//   destroyhouse  real missiles (Projectile) into the house's west + south walls, windows, parapet
//   destroyrobot  missiles + shells into the robot line on the lawn; robots blow apart and rebuild (shortened regen)
//   destroytrack  missiles into the loop, its runway, rails and the figure-eight near the loop
//   mechtopple    plasma bolts into the left leg of James's trillion-story mech: leg off -> slow topple -> impact
//                 shockwave -> lies smoking -> re-assembles -> stands back up (regen shortened)
//   regen         house pieces + trees blown up, then rebuild slowly so the re-assembly is visible
//   treeclimb     P1 pushes into a tree (progress ring), climbs and circles the trunk
//   treetop       P1 stands out on a branch at the top of a tree
// Logs "FFDEMO destruct <scene> phase N ..." for timing the probe shots.
public static class DestructDemo
{
    static string cur;
    static float t0;
    static int phase, fired;
    static float nextFire;
    static StoryMech mech;
    static TreeClimb.Tree tree;
    public static TreeClimb.Tree DemoTree { get { return tree; } }   // ffu27
    static float climbT0 = -1f;            // ffu21c: climb phases count from the moment the froggy grabs the trunk
    static Vector3 camPos, camLook;

    public static bool Is(string sc)
    {
        return sc == "destroyhouse" || sc == "destroyrobot" || sc == "destroytrack" || sc == "mechtopple" || sc == "regen" || sc == "treeclimb" || sc == "treetop";
    }

    public static void Start(Frog f, string sc)
    {
        cur = sc; t0 = Time.realtimeSinceStartup; phase = -1; fired = 0; nextFire = 2f; climbT0 = -1f;
        if (f.world != WorldId.Ranch) f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f);
        if (f.vehicle != null) f.ExitVehicle();
        Destruct.RegenScale = sc == "regen" ? 0.2f : sc == "destroyhouse" || sc == "destroytrack" ? 1f : 0.45f;
        Destruct.RebuildTime = sc == "regen" ? 4.5f : 1.6f;
        StoryMech.DtCap = 0.3f;
        Destruct.DtCap = 0.35f;
        if (sc == "mechtopple")
        {
            mech = null;
            foreach (var m in StoryMech.AllMechs) if (m != null && m.band == 3) { mech = m; break; }
        }
        if (sc == "treeclimb" || sc == "treetop")
        {
            float d;
            tree = null;
            Vector3 want = new Vector3(20f, 0f, 70f);
            float best = 1e9f;
            foreach (var t in TreeClimb.Trees)
            {
                float dd = (new Vector2(t.basePos.x, t.basePos.z) - new Vector2(want.x, want.z)).sqrMagnitude;
                if (dd < best && !t.Gone) { best = dd; tree = t; }
            }
            if (tree != null)
            {
                // ffu27: treeclimb starts 3 m out and really WALKS into the trunk slightly off-centre (Game.DemoClimbWalk
                // steers P1's stick) - the glancing contact that used to slide the froggy round the trunk
                Vector3 p = tree.basePos + new Vector3(0f, 0f, -(tree.r + (sc == "treeclimb" ? 3f : 0.45f)));
                f.DemoPose(p, 0f);
                if (sc == "treetop") f.DemoClimb(tree, 999f, -Mathf.PI * 0.5f);
                Debug.Log("FFDEMO destruct tree at " + tree.basePos.ToString("0") + " top " + tree.TopH.ToString("0.0"));
            }
            d = 0f;
        }
        Debug.Log("FFDEMO destruct start " + sc + " pieces " + Destruct.All.Count + " batches " + BreakBatch.CellCount + " t=" + t0.ToString("0.0"));
    }

    static void Missile(Vector3 target, Vector3 fromDir, float dist = 22f)
    {
        Vector3 from = target + fromDir.normalized * dist;
        Vector3 dir = (target - from).normalized;
        Projectile.Spawn(true, from, dir * 45f, null);
        fired++;
    }
    static void Shell(Vector3 target, Vector3 fromDir, float dist = 22f)
    {
        Vector3 from = target + fromDir.normalized * dist;
        Projectile.Spawn(false, from, (target - from).normalized * 75f, null);
        fired++;
    }

    static void Phase(int p, string what)
    {
        if (p == phase) return;
        phase = p;
        Debug.Log("FFDEMO destruct " + cur + " phase " + p + " " + what + " broken=" + Destruct.Count(1) + " rebuilding=" + Destruct.Count(2) + " chunks=" + Chunks.Live + " t=" + Time.realtimeSinceStartup.ToString("0.0"));
    }

    // returns false to leave the normal camera alone
    public static bool Cam(Frog f, string sc, out Vector3 pos, out Vector3 look)
    {
        float t = Time.realtimeSinceStartup - t0;
        pos = look = Vector3.zero;
        Vector2 hc = Layout.HouseC, hs = Layout.HouseSize;
        float x0 = hc.x - hs.x * 0.5f, z0 = hc.y - hs.y * 0.5f;
        switch (sc)
        {
            case "destroyhouse":
            case "regen":
                {
                    // from the south-west, outside: west wall (x0) on the left, south wall (z0) on the right
                    pos = new Vector3(x0 - 24f, 10f, z0 - 26f); look = new Vector3(x0 + 10f, 3.5f, z0 + 8f);
                    if (t > nextFire && fired < (sc == "regen" ? 6 : 12))
                    {
                        int i = fired;
                        nextFire = t + (sc == "regen" ? 0.5f : 1.1f);
                        Vector3 tgt = i % 2 == 0 ? new Vector3(x0, i % 4 == 0 ? 2.4f : 6.8f, z0 + 4f + (i / 2) * 5.2f)
                                                 : new Vector3(x0 + 6f + (i / 2) * 5.5f, i % 4 == 1 ? 6.8f : 2.4f, z0);
                        Missile(tgt, (i % 2 == 0 ? Vector3.left : Vector3.back) * 3f + Vector3.up * 0.4f + new Vector3(-0.5f, 0f, -0.5f));
                        if (sc == "regen" && i == 5)
                            foreach (var tr in TreeClimb.Trees) if (!tr.Gone && (tr.basePos - new Vector3(x0 - 10f, 0f, z0 - 10f)).sqrMagnitude < 40f * 40f) { Missile(tr.basePos + Vector3.up * 2f, Vector3.back + Vector3.up * 0.3f, 15f); break; }
                    }
                    Phase(t < 2f ? 0 : fired < 12 && sc != "regen" ? 1 : Destruct.Count(2) > 0 ? 3 : 2, Destruct.Count(2) > 0 ? "rebuilding" : "fire");
                    return true;
                }
            case "destroyrobot":
                {
                    var rl = RanchLife.I;
                    float gy = Ranch.GY(-43.8f, 28f);
                    pos = new Vector3(-43.8f, gy + 4.2f, 16.5f); look = new Vector3(-43.8f, gy + 1.2f, 29f);
                    if (rl != null && phase < 0)
                        for (int i = 0; i < rl.robots.Count; i++) { var r = rl.robots[i]; r.DemoStart(new Vector3(-36f - i * 2.6f, 0f, 29f), 180f, -1, 0.9f); r.Order("stop", f); }
                    if (rl != null && t > nextFire && fired < 5)
                    {
                        nextFire = t + 0.9f;
                        int i = Mathf.Min(rl.robots.Count - 1, fired * 2 % Mathf.Max(1, rl.robots.Count));
                        var r = rl.robots[i];
                        Vector3 tgt = r.transform.position + Vector3.up * r.height * 0.5f;
                        if (fired % 2 == 0) Missile(tgt, new Vector3(0.3f, 0.35f, 1f), 18f); else Shell(tgt, new Vector3(-0.2f, 0.3f, 1f), 18f);
                    }
                    int down = 0; if (rl != null) foreach (var r in rl.robots) if (r != null && !r.gameObject.activeSelf) down++;
                    Phase(t < 2f ? 0 : fired < 5 ? 1 : down > 0 ? 2 : 3, "robots down " + down);
                    return true;
                }
            case "destroytrack":
                {
                    Vector2 c = Layout.LoopC;
                    float gy = Ranch.GY(c.x, c.y);
                    pos = new Vector3(c.x - 4f, gy + 11f, c.y - 30f); look = new Vector3(c.x - 2f, gy + 5f, c.y + 4f);
                    if (t > nextFire && fired < 8)
                    {
                        nextFire = t + 1.1f;
                        Vector3[] tg = {
                            new Vector3(c.x, gy + Layout.LoopR * 2f, c.y + Layout.LoopShift * 0.5f),
                            new Vector3(c.x - 14f, gy + 0.5f, c.y),
                            new Vector3(c.x + Layout.LoopR, gy + Layout.LoopR, c.y + Layout.LoopShift * 0.5f),
                            new Vector3(c.x + 16f, gy + 0.5f, c.y + Layout.LoopShift),
                            new Vector3(c.x - Layout.LoopR, gy + Layout.LoopR, c.y + Layout.LoopShift * 0.5f),
                            new Vector3(c.x - 26f, gy + 0.5f, c.y),
                            new Vector3(c.x + 26f, gy + 0.5f, c.y + Layout.LoopShift),
                            new Vector3(c.x, gy + 0.5f, c.y + Layout.LoopShift * 0.5f) };
                        Missile(tg[fired % tg.Length], new Vector3(0.2f, 0.5f, -1f), 20f);
                    }
                    Phase(t < 2f ? 0 : fired < 8 ? 1 : 2, "track");
                    return true;
                }
            case "mechtopple":
                {
                    if (mech == null) return false;
                    float H = mech.height;
                    Vector3 mp = phase <= 1 ? mech.transform.position : camLook;
                    Vector3 fw = mech.transform.forward; fw.y = 0f; fw.Normalize();
                    if (phase < 1)
                    {
                        // camera in front of the mech, a bit to its left, far enough to see it all + the fall to its left
                        Vector3 rt = new Vector3(fw.z, 0f, -fw.x);
                        camPos = mp + fw * H * 2.1f - rt * H * 0.6f + Vector3.up * H * 0.45f;
                        camLook = mp - rt * H * 0.35f + Vector3.up * H * 0.35f;
                    }
                    pos = camPos; look = camLook;
                    if (t > nextFire && fired < 7 && !mech.Toppled)
                    {
                        nextFire = t + 0.45f;
                        Vector3 leg = mech.PartPoint(StoryMech.LegL);
                        Vector3 from = leg + fw * 70f + Vector3.up * 15f;
                        MechShot.Spawn(null, 1, from, (leg - from).normalized * 140f, null);
                        fired++;
                    }
                    if (t > 7f && fired >= 7 && !mech.Toppled && !mech.wrecked) mech.DemoBlowPart(StoryMech.LegL);   // backstop
                    Phase(t < 2f ? 0 : !mech.Toppled ? 1 : 2 + (mech.PartsLine.StartsWith("TIMBER") ? 0 : mech.PartsLine.StartsWith("down") ? 1 : mech.PartsLine.StartsWith("re-") ? 2 : 3), mech.Toppled ? mech.PartsLine : "firing");
                    if (phase >= 1 && phase < 6 && !mech.Toppled && t > 12f) Phase(6, "standing again");
                    return true;
                }
            case "treeclimb":
                {
                    if (tree == null) return false;
                    float ct = 0f;
                    if (f.Climbing)
                    {
                        if (climbT0 < 0f) climbT0 = t;
                        ct = t - climbT0;
                        f.demoPush = false;
                        f.demoTop = true;   // ffu27: stop the walk-in; go round a little, then straight up and out onto a branch
                        f.demoClimbMove = ct > 4f && ct < 7f ? new Vector2(0.9f, 0.05f) : new Vector2(0f, 1f);
                    }
                    else if (f.demoTop) f.demoClimbMove = Vector2.zero;
                    Vector3 fp = f.transform.position;
                    pos = tree.basePos + new Vector3(3.2f, 0f, -5.4f) + Vector3.up * Mathf.Max(1.6f, fp.y - tree.basePos.y + 0.8f);
                    look = new Vector3(tree.basePos.x, fp.y + 0.2f, tree.basePos.z);
                    Phase(f.Climbing ? (ct < 9f ? 1 : ct < 15f ? 2 : 3) : 0, f.Climbing ? "climbing h=" + (f.transform.position.y - tree.basePos.y).ToString("0.0") : "push ring " + f.climbProgress.ToString("0.00"));
                    return true;
                }
            case "treetop":
                {
                    if (tree == null) return false;
                    Vector3 fp = f.transform.position;
                    pos = fp + new Vector3(4.5f, 1.8f, -4.5f); look = fp + Vector3.up * 0.2f;
                    Phase(t < 3f ? 0 : f.Climbing ? 1 : 2, "on branch y=" + (fp.y - tree.basePos.y).ToString("0.0"));
                    return true;
                }
        }
        return false;
    }
}
