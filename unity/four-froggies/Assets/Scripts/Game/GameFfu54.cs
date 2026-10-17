using UnityEngine;

// ffu54 demo / probe hook for ROBOTS EVERYWHERE (World/RobotVisit54.cs). Only with ?ffdemo=1&ffshot=robots54[&ffr54=<phases>].
// Wall-clock phases (SwiftShader is slow), robot = Unitree (phone index 1), P1 = James, everything through the PHONE:
//   mars   James lands on Mars, phone CALL > Call Unitree: a drop pod rides a retro-rocket down, Unitree walks out, waves, says hi
//   pond   James back on the pond shore, PLAY > Swim with me: Unitree follows him home (jets in) and swims in the pond
//   horse  James by the pasture, PLAY > Ride a horse: Unitree climbs on a horse and follows him
//   piggy  PLAY > Piggyback! then PLAY > Build a castle on the pond beach
//   room   James in the REAL ROOM, the phone opens there (C3), CALL > Unitree walks in through the door, then PLAY > Play catch
// Logs "FFDEMO robots54 ..." + the per-world checks "ROBOTS54 CHECK ...", then "FFDEMO robots54 done".
[DefaultExecutionOrder(100)]
public class Ffu54 : MonoBehaviour
{
    public static Ffu54 I;
    bool started; int ph = -1, step; float phW, logT;
    Vector3 camPos, camLook; bool camOn;
    Frog f; Robot r;
    string[] order = { "mars", "pond", "horse", "piggy", "room" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        if (Ffu33.Url("ffdemo") == null || Ffu33.Url("ffshot") != "robots54") return;
        I = new GameObject("ffu54").AddComponent<Ffu54>();
        string o = Ffu33.Url("ffr54");
        if (!string.IsNullOrEmpty(o)) I.order = o.Split(',');
        DontDestroyOnLoad(I.gameObject);
    }

    void Next(string what) { ph++; step = 0; phW = Time.realtimeSinceStartup; camOn = false; Debug.Log("FFDEMO robots54 ph " + ph + " " + (ph < order.Length ? order[ph] : "end") + " " + what); }

    void Behind(Vector3 at, Vector3 fwd, float back, float up, Vector3 look)
    {
        Vector3 want = at - fwd * back + Vector3.up * up;
        camPos = camOn ? Vector3.Lerp(camPos, want, 1f - Mathf.Exp(-Time.deltaTime * 3f)) : want;
        camLook = camOn ? Vector3.Lerp(camLook, look, 1f - Mathf.Exp(-Time.deltaTime * 3f)) : look;
        camOn = true;
    }

    void Phone(string act) { RobotPhone.I.DemoOpen(f, 1, act); RobotPhone.I.DemoSend(); Debug.Log("FFDEMO robots54 phone " + act + " -> '" + (f.toast ?? "") + "'"); }

    void Checks()
    {
        Debug.Log("ROBOTS54 CHECK C3 phone Ok in the Real Room: " + (RobotPhone.Ok(f) ? "yes" : "no") + " (world " + f.world + ")");
        foreach (WorldId w in System.Enum.GetValues(typeof(WorldId)))
        {
            WorldId tw = w == WorldId.Underwater ? WorldId.Ranch : w;
            string how = tw == WorldId.Mars || tw == WorldId.Callisto || tw == WorldId.Moon ? "pod" : tw == WorldId.House || tw == WorldId.RealRoom ? "door" : "jet (or run over when close)";
            Debug.Log("ROBOTS54 CHECK C2 call in " + w + " -> robot world " + tw + " arrives by " + how);
        }
    }

    void Update()
    {
        if (Game.I == null || Game.I.state != Game.State.Play || Game.I.slots.Count == 0 || RanchLife.I == null || RanchLife.I.robots.Count < 2 || RobotPhone.I == null) return;
        if (!started)
        {
            started = true;
            f = Game.I.frogs[Game.I.slots[0].frog];
            r = RanchLife.I.robots[1];
            Robot.DtCap = 0.25f; Robot.IdleChoreAfter = 9999f;
            r.battery = 1f;
            Debug.Log("FFDEMO robots54 start robot " + r.robotName + " order " + string.Join(",", order));
            Next("start");
            return;
        }
        if (ph >= order.Length) return;
        float w = Time.realtimeSinceStartup - phW;
        string p = order[ph];
        var v = r.visit;
        switch (p)
        {
            case "mars":
                if (step == 0 && w > 2f) { SurfaceWorlds.LandMars(f, 0); step = 1; }
                if (step == 1 && w > 5f) { Phone("rcall:sel"); step = 2; }
                if (step >= 1) { Vector3 fw = Robot.LeadFwd(f); Vector3 lp = f.transform.position; Vector3 spot = v != null && v.ph == 0 && v.how == "pod" ? v.vp : r.transform.position; Behind(lp, fw, 6f, 2.4f, Vector3.Lerp(lp, spot, 0.6f) + Vector3.up * (v != null && v.ph == 0 && v.pod != null ? Mathf.Min(20f, (v.pod.transform.position.y - spot.y) * 0.5f) + 1.5f : 1.5f)); }
                if (step == 2 && v != null && v.ph == 1) { step = 3; phW = Time.realtimeSinceStartup; Debug.Log("FFDEMO robots54 mars greeted"); }
                if ((step == 3 && w > 25f) || w > 200f) Next("mars done (arrivals " + Robot.Arrivals + " greets " + Robot.Greets + ")");
                break;
            case "pond":
                if (step == 0)
                {
                    float x = Layout.PondC.x - Layout.PondR.x * 1.13f, z = Layout.PondC.y + 6f;
                    f.SendTo(WorldId.Ranch, new Vector3(x, Ranch.GY(x, z) + 0.3f, z), 90f);
                    step = 1;
                }
                if (step == 1 && w > 3f) { RobotPhone.I.DemoOpen(f, 1, "play:swim"); RobotPhone.I.DemoSend(); RobotPhone.I.open = false; step = 2; }
                if (step >= 1) { Vector3 rp = r.transform.position; Vector3 lp = f.transform.position; Behind(lp, (rp - lp).sqrMagnitude > 1f ? (rp - lp).normalized : Vector3.right, 7f, 4f, rp + Vector3.up * 0.5f); }
                if (step == 2 && v != null && v.swimming) { step = 3; phW = Time.realtimeSinceStartup; Debug.Log("FFDEMO robots54 swimming"); }
                if ((step == 3 && w > 22f) || w > 160f) Next("pond done (swimming " + (v != null && v.swimming) + ")");
                break;
            case "horse":
                if (step == 0)
                {
                    Horse h = Horse.Herd.Count > 0 ? Horse.Herd[0] : null;
                    Vector3 hp = h != null ? h.transform.position : new Vector3(-140f, 0f, 115f);
                    Vector3 at = hp + new Vector3(6f, 0f, -6f);
                    f.SendTo(WorldId.Ranch, new Vector3(at.x, Ranch.GY(at.x, at.z) + 0.3f, at.z), 315f);
                    step = 1;
                }
                if (step == 1 && w > 3f) { RobotPhone.I.DemoOpen(f, 1, "play:horse"); RobotPhone.I.DemoSend(); RobotPhone.I.open = false; step = 2; }
                if (step == 2 && r.drv != null && r.drv.phase == 0 && w > 14f) { r.drv.BoardNow(); step = 3; }
                if (step >= 1) { Vector3 rp = r.drv != null && r.drv.v != null ? r.drv.v.transform.position : r.transform.position; Vector3 lp = f.transform.position; Vector3 d = rp - lp; d.y = 0f; Behind(rp, d.sqrMagnitude > 1f ? d.normalized : Vector3.forward, 7f, 3f, rp + Vector3.up * 1.4f); }
                if (w > 55f) { if (r.drv != null) r.drv.GetOut(); Next("horse done"); }
                break;
            case "piggy":
                if (step == 0)
                {
                    if (r.drv != null) r.EndDrive(null);
                    float x = Layout.PondC.x - Layout.PondR.x * 1.18f, z = Layout.PondC.y - 4f;
                    f.SendTo(WorldId.Ranch, new Vector3(x, Ranch.GY(x, z) + 0.3f, z), 90f);
                    step = 1;
                }
                if (step == 1 && w > 3f) { RobotPhone.I.DemoOpen(f, 1, "play:piggy"); RobotPhone.I.DemoSend(); RobotPhone.I.open = false; step = 2; }
                if (step == 2 && f.piggyOf != null) { step = 3; phW = Time.realtimeSinceStartup - Mathf.Max(w, 20f); Debug.Log("FFDEMO robots54 piggyback on"); }
                if (step == 3 && w > 34f) { f.EndPiggy(true); r.visit.verb = "with"; RobotPhone.I.DemoOpen(f, 1, "play:build"); RobotPhone.I.DemoSend(); RobotPhone.I.open = false; step = 4; }
                {
                    Vector3 rp = r.transform.position; Vector3 fw = r.transform.forward;
                    if (step == 3) Behind(rp, Quaternion.Euler(0f, 60f, 0f) * fw, 4.5f, 2.2f, rp + Vector3.up * 1.4f);
                    else Behind(rp, Vector3.right, 6f, 3f, rp + Vector3.up * 0.6f);
                }
                if (w > 80f) Next("piggy + build done");
                break;
            case "room":
                if (step == 0) { RobotPhone.I.Close(f, "demo"); RealRoom.DemoStart(f, "realroom-body"); step = 1; }
                if (step == 1 && RealRoom.Captures(f) && w > 8f) { Checks(); step = 2; phW = Time.realtimeSinceStartup; }
                if (step == 2 && w > 10f) { RobotPhone.I.DemoOpen(f, 1, "rcall:sel"); RobotPhone.I.DemoSend(); step = 3; phW = Time.realtimeSinceStartup; }
                if (step == 3 && v != null && v.ph == 1 && v.w == WorldId.RealRoom && w > 12f) { RobotPhone.I.DemoOpen(f, 1, "play:catch"); RobotPhone.I.DemoSend(); step = 4; phW = Time.realtimeSinceStartup; }
                if ((step == 1 && w > 240f) || (step == 3 && w > 120f) || (step == 4 && w > 50f)) { Next("room done"); Debug.Log("FFDEMO robots54 done arrivals " + Robot.Arrivals + " greets " + Robot.Greets); }
                break;
        }
        if (Time.unscaledTime > logT)
        {
            logT = Time.unscaledTime + 2f;
            Vector3 rp = r.transform.position, lp = Robot.LeadPos(f);
            Debug.Log("FFDEMO robots54 t ph " + p + " step " + step + " James " + f.world + " robot " + r.robotName + " world " + r.world + " visit " + (v == null ? "-" : v.ph + "/" + v.how + "/" + v.verb + "/" + v.line) + " drv " + (r.drv != null ? r.drv.Status : "-") + " pos " + rp.ToString("0.0") + " d " + RobotNav.Flat(lp - rp).ToString("0.0") + " dy " + (rp.y - lp.y).ToString("0.0") + " pose hidden " + r.hidden + " piggy " + (f.piggyOf != null) + " bat " + r.Pct);
        }
    }

    void LateUpdate()
    {
        if (!camOn || ph >= order.Length || Game.I == null || Game.I.slots.Count == 0) return;
        if (order[ph] == "room") return;   // the Real Room has its own camera
        Camera c = Game.I.slots[0].cam;
        if (c == null) return;
        c.transform.position = camPos;
        c.transform.LookAt(camLook);
    }
}
