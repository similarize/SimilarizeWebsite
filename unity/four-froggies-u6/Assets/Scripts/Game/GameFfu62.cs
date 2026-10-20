using UnityEngine;

// ffu62 Starship flight demo for the probe (?ffdemo=1&ffshot=star62), wall-clock phases (SwiftShader runs ~3 fps):
//   0 orbit    board in Earth orbit
//   1 switch   D-pad > target twice (target switch), X auto-transfer toward it: nose must follow the arc
//   2 warp     free flight 800 m out from Callisto, nose 90 deg off, target Callisto, RB warp: nose swings to Callisto,
//              warp drops at Callisto's zone, LT brake -> captured into Callisto orbit
//   3 manual   free flight 330 m out from Callisto, drifting TAIL-FIRST, stick up (thrust) toward it then LT -> captured
//   Logs "STAR62 PASS/FAIL ...", "STAR62 RESULT n/m", "FFDEMO star62 ph N <name>", "FFDEMO star62 done".
[DefaultExecutionOrder(102)]
public class Ffu62 : MonoBehaviour
{
    public static Ffu62 I;
    bool on; float t0 = -1f, phT, logT; int ph = -1, pass, total, k; bool done;
    Frog f; float minDot = 9f; bool warpDropped, pressT, pressAuto, pressWarp; float brake, thrust;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        I = new GameObject("ffu62").AddComponent<Ffu62>();
        DontDestroyOnLoad(I.gameObject);
    }

    void Update()
    {
        if (!on) { on = Ffu33.Url("ffdemo") != null && Ffu33.Url("ffshot") == "star62"; if (!on) { enabled = false; return; } }
        if (done || Game.I == null || Game.I.state != Game.State.Play || Game.I.slots.Count == 0) return;
        if (t0 < 0f)
        {
            t0 = Time.time; f = Game.I.frogs[Game.I.slots[0].frog]; Frog.DtCap = 0.15f;
            Starship.Log62 = true;
            Game.I.Demo33Hook(Hook);
            if (f.vehicle != null) f.ExitVehicle();
            Debug.Log("FFDEMO star62 start p1 " + f.nick);
        }
        phT += Time.deltaTime;
        try { Step(); } catch (System.Exception e) { Debug.LogError("FFDEMO star62 error " + e); Done(); }
    }

    PIn Hook(PIn i)
    {
        var o = new PIn();
        o.target = pressT; o.auto = pressAuto; o.warpUp = pressWarp;
        pressT = pressAuto = pressWarp = false;
        o.brake = brake; o.move = new Vector2(0f, thrust);
        return o;
    }

    void Next(int p, string name) { ph = p; phT = 0f; k = 0; minDot = 9f; Debug.Log("FFDEMO star62 ph " + p + " " + name + " t " + (Time.time - t0).ToString("0.0")); }
    void Check(string what, bool ok, string detail) { total++; if (ok) pass++; Debug.Log("STAR62 " + (ok ? "PASS" : "FAIL") + " " + what + " | " + detail); }
    void Done() { if (done) return; done = true; brake = thrust = 0f; Debug.Log("STAR62 RESULT " + pass + "/" + total); Debug.Log("FFDEMO star62 done"); }

    void Step()
    {
        var sw = SpaceWorld.I;
        Starship s = sw != null ? sw.ship : null;
        int cal = sw != null ? sw.Find("callisto") : -1;
        switch (ph)
        {
            case -1:
                if (Time.time - t0 > 2f) { if (SpaceWorld.I == null) return; SpaceWorld.I.ToOrbit(f, "earth"); Next(0, "orbit"); }
                break;
            case 0:
                if (phT > 4f) { Next(1, "switch"); }
                break;
            case 1:
                if (k == 0) { pressT = true; k = 1; }
                else if (k == 1 && phT > 1.5f) { pressT = true; k = 2; }
                else if (k == 2 && phT > 3f) { Debug.Log("STAR62 target now " + sw.bodies[s.target].name); pressAuto = true; k = 3; }
                else if (k == 3 && phT > 5f && s.mode == Starship.Mode.Transfer) minDot = Mathf.Min(minDot, s.NoseDotWorld);
                if (k == 3 && phT > 14f)
                {
                    Check("transfer nose along the arc", minDot > 0.9f && minDot < 2f, "min dot " + minDot.ToString("0.00") + " mode " + s.mode);
                    Vector3 away = (sw.bodies[cal].pos - sw.bodies[sw.Find("jupiter")].pos); away.y = 0f; away.Normalize();
                    Vector3 side = Vector3.Cross(Vector3.up, away);
                    s.SetTarget(cal);
                    s.DemoFree(sw.bodies[cal].pos + away * 800f, sw.bodies[cal].vel, Mathf.Atan2(side.x, side.z) * Mathf.Rad2Deg);   // nose 90 deg off
                    Next(2, "warp");
                }
                break;
            case 2:
                if (k == 0 && phT > 1.5f) { pressWarp = true; k = 1; }
                else if (k == 1 && phT > 2f) { if (s.Warping) { k = 2; Debug.Log("STAR62 warping"); } else if (phT > 6f) { Check("warp starts", false, "mode " + s.mode); k = 2; } }
                else if (k == 2)
                {
                    if (s.Warping && phT > 5f) minDot = Mathf.Min(minDot, s.NoseDot);
                    if (!s.Warping && !warpDropped) { warpDropped = true; Check("warp heads to Callisto + drops at its zone", s.CaptureBody == cal, "near " + sw.bodies[s.CaptureBody].name + " d " + (s.transform.position - sw.bodies[cal].pos).magnitude.ToString("0")); brake = 1f; }
                    if (s.InOrbitOf(cal) || phT > 40f)
                    {
                        Check("warp nose toward target", minDot > 0.9f && minDot < 2f, "min dot " + minDot.ToString("0.00"));
                        Check("brake capture into Callisto orbit (after warp)", s.InOrbitOf(cal), "mode " + s.mode);
                        brake = 0f; k = 3; phT = 0f;
                    }
                }
                else if (k == 3 && phT > 4f)
                {
                    Vector3 away = (sw.bodies[cal].pos - sw.bodies[sw.Find("jupiter")].pos); away.y = 0f; away.Normalize();
                    s.SetTarget(cal); // 62c: orbit entry resets the target to Earth
                    // facing Callisto but drifting AWAY from it tail-first at 12 m/s (the "backwards" case)
                    s.DemoFree(sw.bodies[cal].pos + away * 330f, sw.bodies[cal].vel + away * 12f, Mathf.Atan2(-away.x, -away.z) * Mathf.Rad2Deg + 25f);
                    Next(3, "manual");
                }
                break;
            case 3:
                thrust = s.mode == Starship.Mode.Free && brake == 0f ? 1f : 0f;
                if (phT > 4f && s.mode == Starship.Mode.Free && brake == 0f) minDot = Mathf.Min(minDot, s.NoseDot);
                if (brake == 0f && s.mode == Starship.Mode.Free && (s.transform.position - sw.bodies[cal].pos).magnitude < 150f) { brake = 1f; Debug.Log("STAR62 manual brake at d " + (s.transform.position - sw.bodies[cal].pos).magnitude.ToString("0")); }
                if (s.mode == Starship.Mode.Orbit || phT > 45f)
                {
                    Check("manual: ship flies where the nose points", minDot > 0.9f && minDot < 2f, "min dot " + minDot.ToString("0.00"));
                    Check("manual capture into Callisto orbit", s.InOrbitOf(cal), "mode " + s.mode + (s.mode == Starship.Mode.Orbit ? " orbit " + sw.bodies[s.OrbitBody].name : ""));
                    thrust = 0f; brake = 0f; Next(4, "orbitcal");
                }
                break;
            case 4:
                if (phT > 6f) Done();
                break;
        }
        if (Time.unscaledTime > logT && s != null)
        {
            logT = Time.unscaledTime + 1f;
            Debug.Log("FFDEMO star62 st ph " + ph + " mode " + s.mode + " dot " + s.NoseDot.ToString("0.00") + " toast '" + f.toast + "'");
        }
    }
}

public partial class Game
{
    public bool Help62 { get { return help; } set { help = value; if (value) helpBy = slots.Count > 0 ? slots[0] : null; } }
}

// ffu62 demos: ?ffdemo=1&ffshot=hitch62 (trailer hitched, stock then MAX wheels; side camera) and ?ffdemo=1&ffshot=esc62
// (open Help, press Esc: help closes, still playing, same seats). Logs "HITCH62 ..." / "ESC62 PASS|FAIL ..." + "FFDEMO <shot> ph N <name>".
[DefaultExecutionOrder(30000)]
public class Ffu62b : MonoBehaviour
{
    public static Ffu62b I;
    string shot = ""; float t0 = -1f, phT; int ph = -1; bool done, camOn; Vector3 camPos, camLook;
    Vehicle v; Trailer tr; int slots0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { if (I != null) return; I = new GameObject("ffu62b").AddComponent<Ffu62b>(); DontDestroyOnLoad(I.gameObject); }

    void Next(int p, string n) { ph = p; phT = 0f; Debug.Log("FFDEMO " + shot + " ph " + p + " " + n + " t " + (Time.time - t0).ToString("0.0")); }
    void Done() { done = true; camOn = false; Debug.Log("FFDEMO " + shot + " done"); }

    void Update()
    {
        if (shot.Length == 0) { string s = Ffu33.Url("ffdemo") != null ? Ffu33.Url("ffshot") : null; if (s != "hitch62" && s != "esc62") { enabled = false; return; } shot = s; }
        if (done || Game.I == null || Game.I.state != Game.State.Play || Game.I.slots.Count == 0) return;
        if (t0 < 0f) { t0 = Time.time; slots0 = Game.I.slots.Count; Debug.Log("FFDEMO " + shot + " start p1 kind " + Game.I.slots[0].kind + " slots " + slots0); }
        phT += Time.deltaTime;
        try { if (shot == "esc62") Esc(); else Hitch(); } catch (System.Exception e) { Debug.LogError("FFDEMO " + shot + " error " + e); Done(); }
    }

    void Esc()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        switch (ph)
        {
            case -1: if (phT > 3f) { Game.I.Help62 = true; Next(0, "helpopen"); } break;
            case 0:
                if (phT > 3f && kb != null) { UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Escape)); Next(1, "escdown"); }
                else if (phT > 3f) { Debug.Log("ESC62 FAIL no keyboard device"); Done(); }
                break;
            case 1:
                if (phT > 0.3f) { UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState()); Next(2, "escup"); }
                break;
            case 2:
                if (phT > 3f)
                {
                    bool ok = Game.I.state == Game.State.Play && Game.I.slots.Count == slots0 && !Game.I.Help62;
                    Debug.Log("ESC62 " + (ok ? "PASS" : "FAIL") + " state " + Game.I.state + " slots " + Game.I.slots.Count + "/" + slots0 + " help " + Game.I.Help62 + " p1 kind " + (Game.I.slots.Count > 0 ? Game.I.slots[0].kind.ToString() : "-"));
                    Next(3, "after"); 
                }
                break;
            case 3: if (phT > 2f) Done(); break;
        }
    }

    void Hitch()
    {
        switch (ph)
        {
            case -1:
                if (phT > 4f && TrailerBarn.Built && TrailerBarn.Trailers.Count > 0)
                {
                    foreach (var x in Vehicle.All) if (x != null && TrailerTow.CanTow(x) && x.Title != null && x.Title.Contains("Cyber")) { v = x; break; }
                    if (v == null) foreach (var x in Vehicle.All) if (x != null && TrailerTow.CanTow(x)) { v = x; break; }
                    tr = TrailerBarn.Trailers[0];
                    // move the trailer to open ground (big wheels are capped inside buildings), then the truck onto its ball
                    Vector3 o = TrailerBarn.Centre; Vector3 spot = Vector3.zero; bool found = false;
                    for (int k = 0; k < 16 && !found; k++)
                    {
                        float a = k * 22.5f * Mathf.Deg2Rad; Vector3 p = o + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 38f;
                        if (BigWheels.Building(p) == null && BigWheels.Building(p + Vector3.forward * 8f) == null && BigWheels.Building(p - Vector3.forward * 8f) == null) { spot = p; found = true; }
                    }
                    if (!found) spot = o + new Vector3(0f, 0f, -38f);
                    Quaternion rot = Quaternion.Euler(0f, 0f, 0f);
                    float ty = tr.transform.position.y - Ranch.GY(tr.transform.position.x, tr.transform.position.z);
                    Vector3 tp = new Vector3(spot.x, Ranch.GY(spot.x, spot.z) + ty, spot.z);
                    tr.transform.SetPositionAndRotation(tp, rot); tr.GetComponent<Rigidbody>().position = tp; tr.GetComponent<Rigidbody>().rotation = rot; tr.GetComponent<Rigidbody>().velocity = Vector3.zero;
                    Physics.SyncTransforms();
                    Vector3 ball = tr.BallWorld;
                    float vy = v.transform.position.y - Ranch.GY(v.transform.position.x, v.transform.position.z);
                    Vector3 hl = Trailer.HitchLocal(v);
                    Vector3 vp = ball - rot * new Vector3(hl.x, 0f, hl.z); vp.y = Ranch.GY(vp.x, vp.z) + vy;
                    v.transform.SetPositionAndRotation(vp, rot); v.rb.position = vp; v.rb.rotation = rot; v.rb.velocity = Vector3.zero; v.rb.angularVelocity = Vector3.zero;
                    Physics.SyncTransforms();
                    Debug.Log("HITCH62 truck " + v.Title + " trailer " + tr.spec.shortName + " spot " + spot.ToString("0") + " found " + found);
                    Next(0, "settle");
                }
                break;
            case 0:
                if (phT > 3f) { tr.Hitch(v); camOn = true; Next(1, "stock"); }
                break;
            case 1:
                Look();
                if (phT > 6f) { Log("stock"); BigWheels.SetFor(v, BigWheels.Max, "demo62", false); Next(2, "grow"); }
                break;
            case 2:
                Look();
                if (phT > 14f) { Log("max"); Next(3, "max"); }
                break;
            case 3:
                Look();
                if (phT > 6f) { Log("max-end"); Done(); }
                break;
        }
    }

    void Log(string tag)
    {
        Vector3 hw = Trailer.HitchWorld(v), bw = tr.BallWorld;
        var rig = v.transform.Find("HitchReceiver");
        Debug.Log("HITCH62 " + tag + " wheels " + BigWheels.LabelFor(v) + " lift " + v.bigLift.ToString("0.00") + " back " + v.bigBack.ToString("0.00") + " hitched " + tr.Hitched + " |joint ball - truck hitch| " + (hw - bw).magnitude.ToString("0.000") + " m  hitch y above ground " + (hw.y - Ranch.GY(hw.x, hw.z)).ToString("0.00") + " rig " + (rig != null));
    }

    void Look()
    {
        Vector3 b = tr.BallWorld; Vector3 r = v.transform.right;
        camPos = b + r * 6.5f + Vector3.up * 1.6f + v.transform.forward * 0.5f; camLook = b + Vector3.up * 0.6f;
    }

    void LateUpdate()
    {
        if (!camOn || done || Game.I == null || Game.I.slots.Count == 0) return;
        Camera c = Game.I.slots[0].cam; if (c == null) return;
        c.transform.position = camPos; c.transform.LookAt(camLook);
    }
}
