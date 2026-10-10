using UnityEngine;

// ffu20 demo / screenshot shots (?ffdemo=1&ffshot=...):
//   robotdrive  - Unitree races the Monster Truck round the figure-eight (chase camera on the truck, feed PiP on)
//   robotmission - Optimus on Mars collecting rocks, seen through the phone video feed (full screen)
//   robotreturn - the mission ship lands on the robot pad and the robot tips its rocks onto the display pile
//   phonepick   - the robot phone open on the carousel, stepping through the robots every 5 s
// ffu26 (riding mower + robot mech pilots):
//   robotmow       - Unitree climbs on the riding mower and mows lanes (fresh stripes, clippings); Figure 02 push-mows beside
//   robotmech      - Unitree walks to a 10-story mech, climbs into its glass cockpit and walks it round the ranch
//   robotmechspace - a robot-piloted mech landing on Mars and picking rocks into its cargo bay, seen through the phone
//                    feed full screen (&mechat=space: the mech flying the Earth -> Mars transfer instead)
//   playermow      - James on a garage riding mower out on the lawn, driving lanes (normal player camera + HUD)
//   robotmechphone - the phone's MISSION tab with the mech missions; sends "Mech to Mars rocks" at 10 s, DRIVE tab at 20 s
public partial class Game
{
    float demo20T;
    StoryMech demo25Mech;
    static bool IsRobot20(string sc) { return sc == "robotdrive" || sc == "robotmission" || sc == "robotreturn" || sc == "phonepick" || IsRobot25(sc); }
    static bool IsRobot25(string sc) { return sc == "playermow" || sc == "robotmow" || sc == "robotmech" || sc == "robotmechspace" || sc == "robotmechphone"; }

    static StoryMech NearestMech(int band, Vector3 near)
    {
        StoryMech best = null; float bd = 1e18f;
        foreach (var m in StoryMech.AllMechs) { if (m == null || m.band != band || m.driver != null) continue; float d = (m.transform.position - near).sqrMagnitude; if (d < bd) { bd = d; best = m; } }
        return best;
    }

    RidingMower demoMower; int demoLane; bool demoUp = true; float demoStuck, demoBack;
    PIn DemoMowInput(PIn i)
    {
        var m = demoMower;
        if (m == null || m.driver == null) return i;
        Rect a = RanchJobs.MowArea;
        Vector3 p = m.transform.position;
        // ffu26b: turn at the lawn edge (was 0.5 m past it at full throttle -> ran onto the race track and wedged on a sign)
        if ((demoUp && p.z > a.yMax - 0.5f) || (!demoUp && p.z < a.yMin + 0.5f)) { demoLane = (demoLane + 3) % 12; demoUp = !demoUp; }
        float x = a.xMin + 1f + demoLane * 1.6f, z = demoUp ? a.yMax + 3f : a.yMin - 3f;
        Vector3 fw = m.transform.forward; fw.y = 0f;
        Vector3 to = new Vector3(x - p.x, 0f, z - p.z);
        float ang = Vector3.SignedAngle(fw, to, Vector3.up);
        float edge = demoUp ? a.yMax - p.z : p.z - a.yMin;
        float thr = Mathf.Abs(ang) > 100f ? 0.4f : Mathf.Lerp(0.35f, 0.8f, Mathf.Clamp01((edge - 1f) / 5f));
        // stuck (against a fence / sign): back up with the wheel turned the other way
        if (demoBack > 0f) { demoBack -= Time.deltaTime; i.move = new Vector2(-Mathf.Sign(ang), -0.8f); return i; }
        if (Mathf.Abs(m.Speed) < 0.4f) demoStuck += Time.deltaTime; else demoStuck = 0f;
        if (demoStuck > 2f) { demoStuck = 0f; demoBack = 1.4f; }
        i.move = new Vector2(Mathf.Clamp(ang / 35f, -1f, 1f), thr);
        return i;
    }

    void DemoRobot25Start(Frog f, string sc, RanchLife rl)
    {
        if (sc == "playermow")
        {
            RidingMower.Ensure();
            RidingMower m = RidingMower.AllMowers.Count > 1 ? RidingMower.AllMowers[1] : RidingMower.I;
            Rect a = RanchJobs.MowArea;
            Vector3 g = RobotNav.G(a.xMin + 1f, a.yMin - 2f);
            m.transform.position = g + Vector3.up * 0.3f; m.transform.rotation = Quaternion.identity;
            if (m.body != null && m.body.attachedRigidbody != null) { var rb = m.body.attachedRigidbody; rb.position = m.transform.position; rb.rotation = Quaternion.identity; rb.velocity = Vector3.zero; }
            f.SendTo(WorldId.Ranch, g + Vector3.left * 2f, 0f);
            f.EnterVehicle(m);
            demoMower = m; demoLane = 0; demoUp = true;
            demoHook = DemoMowInput;
            Debug.Log("FFDEMO playermow " + f.nick + " on " + m.Title + " driver=" + (m.driver != null ? m.driver.nick : "none"));
            return;
        }
        if (sc == "robotmow")
        {
            RidingMower.Ensure();
            Robot r = rl.robots[1];
            Vector3 ms = RidingMower.I.MountSpot;
            r.DemoStart(new Vector3(ms.x - 1.5f, 0f, ms.z), 90f, Chores.Mow, 0.95f);
            r.Order("mow:ride", f);
            Robot p = rl.robots[3];
            Rect a = RanchJobs.MowArea;
            p.DemoStart(new Vector3(a.xMax - 2f, 0f, a.yMin + 2f), 0f, -1, 0.95f);
            p.Order("mow:push", f);
            Debug.Log("FFDEMO robotmow riding=" + r.robotName + " push=" + p.robotName);
        }
        else if (sc == "robotmech")
        {
            Robot r = rl.robots[1];
            StoryMech m = NearestMech(0, r.transform.position);
            demo25Mech = m;
            if (m != null)
            {
                Vector3 mp = m.transform.position - m.transform.forward * (m.height * 0.45f + 5f) + m.transform.right * 3f;
                r.DemoStart(mp, 0f, -1, 0.95f);
                Debug.Log("FFDEMO robotmech " + r.DriveOrder(m, "wander", f) + " mech " + m.Title + " at " + m.transform.position.ToString("0"));
            }
            if (RobotFeed.I != null) RobotFeed.I.Show(r, 1);
        }
        else if (sc == "robotmechspace")
        {
            Robot r = rl.robots[0];
            StoryMech m = NearestMech(0, r.transform.position);
            demo25Mech = m;
            string at = UrlParam("mechat") ?? "collect";
            var mm = RobotMechMission.Demo(r, m, 0, 4, at, f);
            Debug.Log("FFDEMO robotmechspace " + (mm != null ? mm.PhaseLine : "failed") + " mech " + (m != null ? m.Title : "none"));
            if (RobotFeed.I != null) RobotFeed.I.Show(r, 2);
        }
        else if (sc == "robotmechphone")
        {
            if (RobotPhone.I != null) RobotPhone.I.DemoOpen(f, 1, "mmis:0:4");
        }
    }

    bool DemoRobot25Cam(Frog f, string sc, RanchLife rl, out Vector3 pos, out Vector3 look)
    {
        pos = look = Vector3.zero;
        float dtc = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        if (sc == "playermow")
        {
            if (demoLogT <= 0f && demoMower != null) { demoLogT = 2f; Debug.Log("FFDEMO playermow t=" + Time.realtimeSinceStartup.ToString("0.0") + " at " + demoMower.transform.position.ToString("0.0") + " v=" + demoMower.Speed.ToString("0.0") + " lane " + demoLane); }
            return false;      // the player's own camera + HUD
        }
        if (sc == "robotmow")
        {
            Robot r = rl.robots[1];
            if (demoLogT <= 0f) { demoLogT = 2f; Debug.Log("FFDEMO robotmow t=" + Time.realtimeSinceStartup.ToString("0.0") + " " + r.StatusLine + " riding=" + r.riding + " at " + r.transform.position.ToString("0.0")); }
            Vector3 tp = r.transform.position;
            Vector3 fw0 = r.transform.forward; fw0.y = 0f; fw0.Normalize();
            demoCamPos = demoCamPos == Vector3.zero ? fw0 : Vector3.Slerp(demoCamPos, fw0, dtc * 1.2f);
            Vector3 fw = demoCamPos; fw.y = 0f; fw.Normalize();
            Vector3 rt = new Vector3(fw.z, 0f, -fw.x);
            pos = tp + rt * 4.6f + fw * 3.6f + Vector3.up * 2.4f; look = tp + Vector3.up * 0.9f;
            float g = Ranch.GY(pos.x, pos.z) + 1f; if (pos.y < g) pos.y = g;
            return true;
        }
        if (sc == "robotmech")
        {
            Robot r = rl.robots[1];
            StoryMech m = demo25Mech;
            if (demoLogT <= 0f) { demoLogT = 2f; Debug.Log("FFDEMO robotmech t=" + Time.realtimeSinceStartup.ToString("0.0") + " " + r.StatusLine + (m != null ? " mech at " + m.transform.position.ToString("0") + " canopy " + m.RobotAboard : "")); }
            if (m == null) return false;
            float H = m.height;
            Vector3 fw = m.transform.forward; fw.y = 0f; fw.Normalize();
            Vector3 rt = new Vector3(fw.z, 0f, -fw.x);
            if (r.seatedIn != m)
            {
                // boarding: wide enough for the robot walking up and the mech
                Vector3 at = Vector3.Lerp(r.transform.position, m.transform.position, 0.5f) + Vector3.up * H * 0.35f;
                pos = at - fw * (H * 0.9f + 6f) + rt * (H * 0.7f + 4f) + Vector3.up * H * 0.1f; look = at;
            }
            else if (demo20T < 32f)
            {
                // close on the glass cockpit: the robot pilot in the mech's head
                Vector3 at = m.CanopySeat.position;
                pos = at + fw * H * 0.32f + rt * H * 0.2f + Vector3.up * H * 0.06f; look = at;
            }
            else
            {
                Vector3 at = m.transform.position + Vector3.up * H * 0.55f;
                pos = at - fw * (H * 1.3f + 6f) + rt * (H * 1.0f + 4f) + Vector3.up * H * 0.2f; look = at;
            }
            float g = Ranch.GY(pos.x, pos.z) + 1.5f; if (pos.y < g) pos.y = g;
            return true;
        }
        if (sc == "robotmechspace")
        {
            var mm = RobotMechMission.Active;
            if (demoLogT <= 0f) { demoLogT = 2f; Debug.Log("FFDEMO robotmechspace t=" + Time.realtimeSinceStartup.ToString("0.0") + " " + (mm != null ? mm.r.robotName + " " + mm.PhaseLine + " " + mm.DistLine + " got " + mm.got + " world " + mm.r.world : "no mission")); }
            Vector2 pc = MissionSite.PadC;
            pos = new Vector3(pc.x + 14f, MissionSite.PadTop + 7f, pc.y + 26f);
            look = new Vector3(pc.x - 1f, MissionSite.PadTop + 3f, pc.y + 6f);
            return true;
        }
        if (sc == "robotmechphone")
        {
            if (RobotPhone.I != null)
            {
                if (demo20T > 10f && demoPhase < 1) { demoPhase = 1; RobotPhone.I.DemoSend(); Debug.Log("FFDEMO robotmechphone sent t=" + Time.realtimeSinceStartup.ToString("0.0")); }
                if (demo20T > 20f && demoPhase < 2) { demoPhase = 2; RobotPhone.I.DemoOpen(f, 1, "dmode:hold"); }
                if (demo20T > 30f && demoPhase < 3) { demoPhase = 3; RobotPhone.I.DemoOpen(f, 1, "mmis:2:0"); }
            }
            if (demoLogT <= 0f) { demoLogT = 2f; var mm = RobotMechMission.Active; Debug.Log("FFDEMO robotmechphone t=" + Time.realtimeSinceStartup.ToString("0.0") + " " + (mm != null ? mm.PhaseLine : "no mission")); }
            float gy = Ranch.GY(-43.8f, 28f);
            pos = new Vector3(-36f, gy + 2.6f, 18.5f); look = new Vector3(-46f, gy + 1.2f, 28f);
            return true;
        }
        return false;
    }

    void DemoRobot20Start(Frog f, string sc)
    {
        if (f.world != WorldId.Ranch) f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f);
        var rl = RanchLife.I;
        if (rl == null) return;
        demo20T = 0f; demoPhase = -1; demoCamPos = Vector3.zero;
        Robot.DtCap = 0.3f;          // SwiftShader runs ~3 fps: keep robot game time near real time
        StoryMech.DtCap = 0.3f;
        if (IsRobot25(sc)) { DemoRobot25Start(f, sc, rl); return; }
        if (sc == "robotdrive")
        {
            Robot r = rl.robots[1];
            Vehicle mt = null;
            foreach (var v in Vehicle.All) if (v != null && v.Title == "Monster Truck") { mt = v; break; }
            Debug.Log("FFDEMO robotdrive " + r.DriveOrder(mt, "race", f));
            if (r.drv != null) r.drv.BoardNow();
            if (RobotFeed.I != null) RobotFeed.I.Show(r, 1);
        }
        else if (sc == "robotmission")
        {
            Robot r = rl.robots[0];
            r.battery = 0.9f;
            RobotMission.Demo(r, 0, 5, "collect");
            if (RobotFeed.I != null) RobotFeed.I.Show(r, 2);
        }
        else if (sc == "robotreturn")
        {
            Robot r = rl.robots[0];
            r.battery = 0.7f;
            RobotMission.Demo(r, 0, 5, "return");
            if (RobotFeed.I != null) RobotFeed.I.Show(r, 1);
        }
        else if (sc == "phonepick")
        {
            if (RobotPhone.I != null) RobotPhone.I.DemoOpen(f, 0, "MISSION");
        }
    }

    bool DemoRobot20Cam(Frog f, string sc, out Vector3 pos, out Vector3 look)
    {
        pos = look = Vector3.zero;
        var rl = RanchLife.I;
        if (rl == null) return false;
        demo20T += Time.unscaledDeltaTime;
        demoLogT -= Time.unscaledDeltaTime;
        if (IsRobot25(sc)) return DemoRobot25Cam(f, sc, rl, out pos, out look);
        if (sc == "robotdrive")
        {
            Robot r = rl.robots[1];
            Vehicle v = r.seatedIn;
            if (demoLogT <= 0f) { demoLogT = 2f; Debug.Log("FFDEMO robotdrive t=" + Time.realtimeSinceStartup.ToString("0.0") + " " + r.StatusLine + " v=" + (v != null ? v.Speed.ToString("0.0") : "-") + " at " + r.transform.position.ToString("0")); }
            if (v == null) { pos = r.transform.position + new Vector3(6f, 4f, 8f); look = r.transform.position; return true; }
            Vector3 fw0 = v.transform.forward; fw0.y = 0f; fw0.Normalize();
            float dtc = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            demoCamPos = demoCamPos == Vector3.zero ? fw0 : Vector3.Slerp(demoCamPos, fw0, dtc * 2f);
            Vector3 fw = demoCamPos; fw.y = 0f; fw.Normalize();
            Vector3 rt = new Vector3(fw.z, 0f, -fw.x);
            Vector3 tp = v.transform.position;
            pos = tp + rt * 6.5f + fw * 5.5f + Vector3.up * 2.6f; look = tp + Vector3.up * 1.2f;
            float g = Ranch.GY(pos.x, pos.z) + 1f; if (pos.y < g) pos.y = g;
            return true;
        }
        if (sc == "robotmission" || sc == "robotreturn")
        {
            var m = RobotMission.Active;
            if (demoLogT <= 0f) { demoLogT = 2f; Debug.Log("FFDEMO " + sc + " t=" + Time.realtimeSinceStartup.ToString("0.0") + " " + (m != null ? m.r.robotName + " " + m.PhaseLine + " " + m.DistLine + " got " + m.got : "no mission") + " pile " + MissionSite.PileCount(0)); }
            // the main view watches the robot mission pad + the display tables
            Vector2 pc = MissionSite.PadC;
            pos = new Vector3(pc.x + 14f, MissionSite.PadTop + 7f, pc.y + 26f);
            look = new Vector3(pc.x - 1f, MissionSite.PadTop + (sc == "robotreturn" && m != null && m.phase <= 13 ? 9f : 3f), pc.y + 6f);
            return true;
        }
        if (sc == "phonepick")
        {
            int k = (int)(demo20T / 5f) % rl.robots.Count;
            if (k != demoPhase && RobotPhone.I != null) { demoPhase = k; RobotPhone.I.DemoOpen(f, k, k % 2 == 0 ? "MISSION" : "DRIVE"); Debug.Log("FFDEMO phonepick " + rl.robots[k].robotName + " t=" + Time.realtimeSinceStartup.ToString("0.0")); }
            float gy = Ranch.GY(-43.8f, 28f);
            pos = new Vector3(-36f, gy + 2.6f, 18.5f); look = new Vector3(-46f, gy + 1.2f, 28f);
            return true;
        }
        return false;
    }
}
