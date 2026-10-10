using UnityEngine;

// ffu20 demo / screenshot shots (?ffdemo=1&ffshot=...):
//   robotdrive  - Unitree races the Monster Truck round the figure-eight (chase camera on the truck, feed PiP on)
//   robotmission - Optimus on Mars collecting rocks, seen through the phone video feed (full screen)
//   robotreturn - the mission ship lands on the robot pad and the robot tips its rocks onto the display pile
//   phonepick   - the robot phone open on the carousel, stepping through the robots every 5 s
public partial class Game
{
    float demo20T;
    static bool IsRobot20(string sc) { return sc == "robotdrive" || sc == "robotmission" || sc == "robotreturn" || sc == "phonepick"; }

    void DemoRobot20Start(Frog f, string sc)
    {
        if (f.world != WorldId.Ranch) f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f);
        var rl = RanchLife.I;
        if (rl == null) return;
        demo20T = 0f; demoPhase = -1; demoCamPos = Vector3.zero;
        Robot.DtCap = 0.3f;          // SwiftShader runs ~3 fps: keep robot game time near real time
        StoryMech.DtCap = 0.3f;
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
