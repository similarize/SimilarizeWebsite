using UnityEngine;

// ffu55 demo / probe hook for the LIGHT + MATERIAL PASS (Core/Light55.cs). Only with ?ffdemo=1&ffshot=light55[&ffl55=<phases>]
// (add &light=old for the same tour with the pre-ffu55 look = before shots). Wall-clock phases (SwiftShader is slow):
//   ranch     wide view of the yard + the house          cars   Roadster + Cybertruck front 3/4 (paint / brushed steel)
//   houseout  the house front from the lawn             housein living room: lamps on, lights OFF (switch), back on,
//                                                                then the game's own camera (stays inside, under the ceiling)
//   mars      James on Mars by the rover                robot  Unitree called into the house, PLAY > Lights on / off
// Logs "FFDEMO light55 ..." and "FFDEMO light55 done".
[DefaultExecutionOrder(100)]
public class Ffu55 : MonoBehaviour
{
    public static Ffu55 I;
    bool started, allDone; int ph = -1, step; float phW, logT;
    Vector3 camPos, camLook; bool camOn;
    Frog f; Roadster49 rd; GroundVehicle ct;
    string[] order = { "ranch", "houseout", "cars", "housein", "mars", "robot" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        if (Ffu33.Url("ffdemo") == null || Ffu33.Url("ffshot") != "light55") return;
        I = new GameObject("ffu55").AddComponent<Ffu55>();
        string o = Ffu33.Url("ffl55");
        if (!string.IsNullOrEmpty(o)) I.order = o.Split(',');
        DontDestroyOnLoad(I.gameObject);
    }

    void Next(string what) { ph++; step = 0; phW = Time.realtimeSinceStartup; camOn = false; Debug.Log("FFDEMO light55 ph " + ph + " " + (ph < order.Length ? order[ph] : "end") + " " + what + (Light55.On ? " (ffu55 light)" : " (OLD light)")); }
    void Cam(Vector3 p, Vector3 look) { camPos = p; camLook = look; camOn = true; }
    static Vector3 G(float x, float z) { return new Vector3(x, Ranch.GY(x, z), z); }

    void Park(Vehicle v, Vector3 spot, float yaw)
    {
        Vector3 p = G(spot.x, spot.z) + Vector3.up * 0.5f; Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        v.rb.isKinematic = false; v.rb.velocity = Vector3.zero; v.rb.angularVelocity = Vector3.zero;
        v.rb.position = p; v.rb.rotation = q; v.transform.SetPositionAndRotation(p, q);
    }
    void View(Vehicle v, float az, float dist, float h)
    {
        Vector3 c = v.transform.position + Vector3.up * 0.75f;
        Vector3 d = Quaternion.Euler(0f, v.transform.eulerAngles.y + az, 0f) * Vector3.forward;
        Cam(c + d * dist + Vector3.up * h, c);
    }

    void Update()
    {
        if (Game.I == null || Game.I.state != Game.State.Play || Game.I.slots.Count == 0 || Roadster49.I == null) return;
        if (!started)
        {
            started = true;
            f = Game.I.frogs[Game.I.slots[0].frog];
            rd = Roadster49.I;
            float best = 1e9f;
            foreach (var v in Vehicle.All)
            {
                var gv = v as GroundVehicle;
                if (gv == null || gv.Title != "Cybertruck") continue;
                float d = (new Vector2(v.transform.position.x, v.transform.position.z) - new Vector2(14f, 44f)).sqrMagnitude;
                if (d < best) { best = d; ct = gv; }
            }
            if (RanchLife.I != null) { Robot.DtCap = 0.25f; Robot.IdleChoreAfter = 9999f; }
            Debug.Log("FFDEMO light55 start order " + string.Join(",", order) + " sun " + (RenderSettings.sun != null ? RenderSettings.sun.intensity.ToString("0.00") : "-") + " shadows " + QualitySettings.shadowDistance + " m x" + QualitySettings.shadowCascades + " pixel lights " + QualitySettings.pixelLightCount);
            Next("start");
            return;
        }
        if (ph >= order.Length) { if (!allDone) { allDone = true; Debug.Log("FFDEMO light55 done"); } return; }
        float w = Time.realtimeSinceStartup - phW;
        Vector2 hc = Layout.HouseC; float hz1 = hc.y + Layout.HouseSize.y * 0.5f;
        switch (order[ph])
        {
            case "ranch":
                if (step == 0) { f.SendTo(WorldId.Ranch, G(hc.x + 20f, hz1 + 30f) + Vector3.up * 0.3f, 200f); step = 1; }
                Cam(G(hc.x + 46f, hz1 + 48f) + Vector3.up * 7f, new Vector3(hc.x, 4f, hc.y));
                if (w > 22f) Next("ranch done");
                break;
            case "cars":
                if (step == 0)
                {
                    Vector3 rs = new Vector3(hc.x + 8f, 0f, hz1 + 24f), cs = new Vector3(hc.x + 18f, 0f, hz1 + 26f);
                    Park(rd, rs, 150f); if (ct != null) Park(ct, cs, 150f);
                    f.SendTo(WorldId.Ranch, G(hc.x + 2f, hz1 + 34f) + Vector3.up * 0.3f, 150f);
                    Debug.Log("FFDEMO light55 parked Roadster " + rd.transform.position.ToString("0.0") + " Cybertruck " + (ct != null ? ct.transform.position.ToString("0.0") : "none"));
                    step = 1;
                }
                if (w < 22f) View(rd, 35f, 6.4f, 0.8f);
                else if (ct != null && w < 44f) View(ct, 35f, 8.2f, 0.9f);
                else if (ct != null && w < 64f) View(ct, 140f, 7.6f, 2.2f);
                else Next("cars done");
                break;
            case "houseout":
                Cam(G(hc.x - 13f, hz1 + 21f) + Vector3.up * 3.2f, new Vector3(hc.x + 2f, 3.4f, hz1));   // 55b: west of the garage (was inside its wall)
                if (w > 22f) Next("house outside done");
                break;
            case "housein":
                if (step == 0) { if (HouseWorld.I != null) HouseWorld.I.Enter(f); step = 1; }
                if (step == 1) { Cam(HouseWorld.L(-8f, 3.4f, 18.6f), HouseWorld.L(-21f, 1.2f, 9f)); if (w > 22f) { if (Light55House.I != null) Light55House.I.DemoFlip(0, f); step = 2; phW = Time.realtimeSinceStartup; w = 0f; } }
                if (step == 2) { Cam(HouseWorld.L(-8f, 3.4f, 18.6f), HouseWorld.L(-21f, 1.2f, 9f)); if (w > 20f) { if (Light55House.I != null) Light55House.I.DemoFlip(0, f); step = 3; phW = Time.realtimeSinceStartup; w = 0f; } }
                if (step == 3) { Cam(HouseWorld.L(4f, 3.6f, 2.5f), HouseWorld.L(0f, 1.4f, -9f)); if (w > 20f) { step = 4; phW = Time.realtimeSinceStartup; w = 0f; camOn = false; f.SendTo(WorldId.House, HouseWorld.L(-14f, 0.15f, 12f), 230f); } }
                if (step == 4)
                {
                    camOn = false;
                    if (Time.unscaledTime > logT) { var cam = Game.I.slots[0].cam; if (cam != null) Debug.Log("FFDEMO light55 game cam in house at " + (cam.transform.position - Worlds.HouseO).ToString("0.0") + " (ceiling " + Light55.HouseH + ")"); }
                    if (w > 22f) Next("house inside done");
                }
                break;
            case "mars":
                if (step == 0) { SurfaceWorlds.LandMars(f, 0); step = 1; }
                if (w > 3f) { Vector3 lp = f.transform.position; Vector3 fw = f.transform.forward; Cam(lp - fw * 6f + Vector3.up * 2.4f + Quaternion.Euler(0f, 90f, 0f) * fw * 2f, lp + Vector3.up * 1f + fw * 4f); }
                if (w > 26f) Next("mars done");
                break;
            case "robot":
                if (RanchLife.I == null || RanchLife.I.robots.Count < 2 || RobotPhone.I == null) { Next("robot skipped"); break; }
                {
                    Robot r = RanchLife.I.robots[1];
                    if (step == 0) { if (HouseWorld.I != null && f.world != WorldId.House) HouseWorld.I.Enter(f); f.SendTo(WorldId.House, HouseWorld.L(-12f, 0.15f, 9f), 0f); r.battery = 1f; step = 1; }
                    if (step == 1 && w > 3f) { RobotPhone.I.DemoOpen(f, 1, "play:lights"); RobotPhone.I.DemoSend(); RobotPhone.I.open = false; Debug.Log("FFDEMO light55 phone play:lights -> '" + (f.toast ?? "") + "'"); step = 2; }
                    if (step >= 2) { Vector3 rp = r.transform.position; Cam(rp + new Vector3(4f, 2.6f, -4f), rp + Vector3.up * 1f); }
                    if (step == 2 && Light55House.I != null && Light55House.I.AnyOff) { step = 3; phW = Time.realtimeSinceStartup; w = 0f; Debug.Log("FFDEMO light55 robot flipped a switch"); }
                    if ((step == 3 && w > 14f) || w > 120f) Next("robot done");
                    if (Time.unscaledTime > logT) Debug.Log("FFDEMO light55 robot " + r.robotName + " world " + r.world + " visit " + (r.visit == null ? "-" : r.visit.ph + "/" + r.visit.how + "/" + r.visit.verb + "/" + r.visit.line) + " pos " + (r.transform.position - Worlds.HouseO).ToString("0.0") + " status '" + r.StatusLine + "'");
                }
                break;
            default: Next("unknown phase"); break;
        }
        if (Time.unscaledTime > logT) logT = Time.unscaledTime + 2f;
    }

    void LateUpdate()
    {
        if (!camOn || ph < 0 || ph >= order.Length || Game.I == null || Game.I.slots.Count == 0) return;
        Camera c = Game.I.slots[0].cam;
        if (c == null) return;
        c.transform.position = camPos;
        c.transform.LookAt(camLook);
    }
}
