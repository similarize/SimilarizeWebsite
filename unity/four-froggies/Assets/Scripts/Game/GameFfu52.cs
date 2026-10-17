using UnityEngine;

// ffu52 demo / probe hook for the photo-matched hero cars (HeroCars52.cs). Only with ?ffdemo=1&ffshot=cars52
// (add &ffcars=old for the same tour with the pre-ffu52 cars = before shots). Tour (wall-clock holds, SwiftShader is slow):
//  1. Roadster parked on the east lawn: front 3/4, side, rear 3/4 views.   2. the yard Cybertruck: the same three views.
//  3. Roadster launch + hard stop (side camera): logs the body squat / dive (CarFeel52 pitch).
//  2b/3b. seated frog + yoke views (ph 10 / 11).  4. Cybertruck full-lock circle (outside camera): logs the body roll.     Logs "FFDEMO cars52 ..." and "FFDEMO cars52 done".
[DefaultExecutionOrder(100)]
public class Ffu52 : MonoBehaviour
{
    public static Ffu52 I;
    bool started; string shot = "";
    int ph = -1; float phW, phG, logT;
    Vector3 camPos, camLook; bool camOn;
    Frog f; Roadster49 rd; GroundVehicle ct;
    static readonly Vector3 RdSpot = new Vector3(52f, 0f, 8.6f), CtSpot = new Vector3(32f, 0f, 22f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        if (Ffu33.Url("ffdemo") == null || Ffu33.Url("ffshot") != "cars52") return;
        I = new GameObject("ffu52").AddComponent<Ffu52>();
        DontDestroyOnLoad(I.gameObject);
    }

    static Vector3 G(Vector3 p) { return new Vector3(p.x, Ranch.GY(p.x, p.z), p.z); }

    void Park(Vehicle v, Vector3 spot, float yaw)
    {
        Vector3 p = G(spot) + Vector3.up * 0.5f; Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        v.rb.isKinematic = false; v.rb.velocity = Vector3.zero; v.rb.angularVelocity = Vector3.zero;
        v.rb.position = p; v.rb.rotation = q; v.transform.SetPositionAndRotation(p, q);
    }

    void Next(int p, string what) { ph = p; phW = Time.realtimeSinceStartup; phG = Time.time; Debug.Log("FFDEMO cars52 ph " + p + " " + what + (HeroCars52.Old ? " (OLD cars)" : " (ffu52 cars)")); }

    void View(Vehicle v, float az, float dist, float h)
    {
        Vector3 c = v.transform.position + Vector3.up * 0.75f;
        Vector3 d = Quaternion.Euler(0f, v.transform.eulerAngles.y + az, 0f) * Vector3.forward;
        camPos = c + d * dist + Vector3.up * h; camLook = c; camOn = true;
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
            if (ct == null) { Debug.LogError("FFDEMO cars52: no yard Cybertruck"); shot = ""; return; }
            CarFeel52.Log = true;
            Park(rd, RdSpot, 300f); Park(ct, CtSpot, 300f);
            Next(0, "parked: Roadster at " + rd.transform.position.ToString("0.0") + ", Cybertruck at " + ct.transform.position.ToString("0.0") + " mass rd " + rd.rb.mass + " ct " + ct.rb.mass);
            shot = "cars52";
        }
        if (shot.Length == 0) return;
        float w = Time.realtimeSinceStartup - phW, g = Time.time - phG;
        const float Hold = 26f;
        switch (ph)
        {
            case 0: View(rd, 35f, 6.2f, 0.7f); if (w > Hold) Next(1, "Roadster side"); break;
            case 1: View(rd, 90f, 7.5f, 0.35f); if (w > Hold) Next(2, "Roadster rear 3/4"); break;
            case 2: View(rd, 150f, 6.4f, 0.9f); if (w > Hold) Next(3, "Cybertruck front 3/4"); break;
            case 3: View(ct, 35f, 8.0f, 0.8f); if (w > Hold) Next(4, "Cybertruck side"); break;
            case 4: View(ct, 90f, 9.0f, 0.3f); if (w > Hold) Next(5, "Cybertruck rear 3/4"); break;
            case 5: View(ct, 150f, 8.2f, 1.0f); if (w > Hold) { Enter(rd, new Vector3(66f, 0f, 8.6f), 270f); Next(10, "Roadster cockpit (seat / yoke)"); } break;
            case 10: Cockpit(rd); if (w > 20f) Next(6, "Roadster launch"); break;
            case 11: Cockpit(ct); if (w > 20f) Next(8, "Cybertruck full-lock circle"); break;
            case 6:
                rd.inputHook = i => { var o = new PIn(); o.gas = 1f; return o; };
                Chase(rd, 90f);
                if (g > 1.0f) { Next(7, "Roadster hard stop from " + (rd.ForwardSpeed * 3.6f).ToString("0") + " km/h"); }
                break;
            case 7:
                rd.inputHook = i => { var o = new PIn(); o.brake = 1f; return o; };
                Chase(rd, 90f);
                if (g > 3.5f || (g > 0.8f && rd.ForwardSpeed < 0.5f)) { Exit(rd); Enter(ct, new Vector3(52f, 0f, 15.6f), 270f); Next(11, "Cybertruck cockpit (seat / yoke)"); }
                break;
            case 8:
                ct.inputHook = i => { var o = new PIn(); o.gas = ct.ForwardSpeed < 11f ? 0.8f : 0.15f; o.move = new Vector2(-1f, 0f); return o; };
                Chase(ct, 60f);
                if (g > 9f) { Exit(ct); Next(9, "done"); Debug.Log("FFDEMO cars52 done"); }
                break;
        }
        if (Time.unscaledTime > logT && ph >= 0 && ph != 9)
        {
            logT = Time.unscaledTime + 1.5f;
            var cr = rd.GetComponent<CarFeel52>(); var cc = ct.GetComponent<CarFeel52>();
            Debug.Log("FFDEMO cars52 t " + g.ToString("0.00") + " rd v " + (rd.ForwardSpeed * 3.6f).ToString("0") + " km/h pitch " + (cr != null ? cr.Pitch.ToString("0.00") + " [" + cr.MinPitch.ToString("0.00") + ".." + cr.MaxPitch.ToString("0.00") + "]" : "-")
                + " | ct v " + (ct.ForwardSpeed * 3.6f).ToString("0") + " km/h roll " + (cc != null ? cc.Roll.ToString("0.00") + " max " + cc.MaxRoll.ToString("0.00") + " pitch " + cc.Pitch.ToString("0.00") : "-")
                + " | ct y " + ct.transform.position.y.ToString("0.00") + " rd y " + rd.transform.position.y.ToString("0.00"));
        }
    }

    void Enter(Vehicle v, Vector3 spot, float yaw)
    {
        if (f.vehicle != null) f.ExitVehicle();
        Park(v, spot, yaw);
        f.SendTo(WorldId.Ranch, v.transform.position + v.transform.right * -2.5f, yaw);
        f.EnterVehicle(v);
        var c = v.GetComponent<CarFeel52>(); if (c != null) { c.MaxPitch = c.MinPitch = c.MaxRoll = 0f; }
    }
    // high over the driver's door, looking down through the side glass at the seated frog + yoke
    void Cockpit(Vehicle v)
    {
        Transform t = v.transform;
        camPos = t.position - t.right * 2.3f + Vector3.up * 2.1f + t.forward * 0.3f;
        camLook = t.position + Vector3.up * 0.75f + t.forward * 0.35f; camOn = true;
    }
    void Exit(Vehicle v) { v.inputHook = null; if (f.vehicle != null) f.ExitVehicle(); }

    void Chase(Vehicle v, float az)
    {
        Vector3 c = v.transform.position + Vector3.up * 0.7f;
        Vector3 d = Quaternion.Euler(0f, v.transform.eulerAngles.y + az, 0f) * Vector3.forward;
        Vector3 want = c + d * 8.5f + Vector3.up * 0.6f;
        camPos = camOn ? Vector3.Lerp(camPos, want, 1f - Mathf.Exp(-Time.deltaTime * 6f)) : want;
        camLook = c; camOn = true;
    }

    void LateUpdate()
    {
        if (!camOn || ph == 9 || Game.I == null || Game.I.slots.Count == 0) return;
        Camera c = Game.I.slots[0].cam;
        if (c == null) return;
        c.transform.position = camPos;
        c.transform.LookAt(camLook);
    }
}
