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
