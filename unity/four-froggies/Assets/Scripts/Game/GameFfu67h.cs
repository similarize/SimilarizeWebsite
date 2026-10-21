using System.Collections;
using UnityEngine;

// ffu67h probe: ?ffdemo=1&ffshot=rcstick67 - RC CAR STICK = STEER ONLY (Bill Oct 20 16:39). P1 takes a remote at the raceway stand and
// drives through the vehicle input hook (the same inp path a pad fills): stick UP alone -> the car stays put; RT (gas) -> it goes;
// LT (brake) -> stops then reverses; stick sideways + RT -> it turns. Logs "RCSTICK67 PASS|FAIL ...", "RCSTICK67 RESULT n/m",
// "FFDEMO rcstick67 ph N <name>", "FFDEMO rcstick67 done".
public class Ffu67h : MonoBehaviour
{
    public static Ffu67h I;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        I = new GameObject("ffu67h").AddComponent<Ffu67h>();
        DontDestroyOnLoad(I.gameObject);
    }
    bool started;
    void Update()
    {
        if (started) return;
        string s = Ffu33.Url("ffdemo") != null ? Ffu33.Url("ffshot") : null;
        if (s != "rcstick67") { enabled = false; return; }
        if (Game.I == null || Game.I.state != Game.State.Play || Game.I.slots.Count == 0) return;
        started = true;
        Game.I.StartCoroutine(Game.I.Rc67Run());
    }
}

public partial class Game
{
    int rsPass, rsTotal, rsPh;
    PIn rsIn = new PIn();
    void RsCheck(string what, bool ok, string d) { rsTotal++; if (ok) rsPass++; Debug.Log("RCSTICK67 " + (ok ? "PASS" : "FAIL") + " " + what + " | " + d); }
    void RsNext(string n) { Debug.Log("FFDEMO rcstick67 ph " + (rsPh++) + " " + n + " t " + Time.time.ToString("0.0")); }
    IEnumerator RsGame(float s) { float t = 0f, r0 = Time.realtimeSinceStartup; while (t < s && Time.realtimeSinceStartup - r0 < 120f) { yield return null; t += Time.deltaTime; } }
    bool rsHome;
    PIn RsSteerTo(RcCar c, Vector3 tgt)
    {
        Vector3 d = tgt - c.transform.position; d.y = 0f;
        float ang = Vector3.SignedAngle(new Vector3(c.transform.forward.x, 0f, c.transform.forward.z), d, Vector3.up);
        var o = new PIn(); o.move = new Vector2(Mathf.Clamp(ang / 30f, -1f, 1f), 0f);
        o.gas = Mathf.Abs(ang) > 70f ? 0.4f : 0.85f; rsIn = o; return o;
    }
    PIn RsSet(Vector2 move, float gas, float brake) { var o = new PIn(); o.move = move; o.gas = gas; o.brake = brake; rsIn = o; return o; }

    public IEnumerator Rc67Run()
    {
        yield return new WaitForSecondsRealtime(3f);
        Frog f = frogs[slots[0].frog];
        if (RcTrack.I == null) { RsCheck("raceway exists", false, "no RcTrack"); RsDone(); yield break; }
        if (f.vehicle != null) f.ExitVehicle();
        if (f.world != WorldId.Ranch) { f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f); yield return RsGame(1f); }
        f.Teleport(RcTrack.I.StandFoot + Vector3.up * 0.2f); yield return null;
        RcTrack.I.Take(f); yield return RsGame(0.5f);
        RcCar c = f.vehicle as RcCar;
        RsCheck("P1 holds an RC remote", c != null, c != null ? c.Title : "-");
        if (c == null) { RsDone(); yield break; }
        c.demoAuto = false;
        RsSet(Vector2.zero, 0f, 1f);
        c.inputHook = i => rsIn;
        yield return RsGame(1.5f);
        RsNext("stickup");
        Vector3 p0 = c.transform.position;
        RsSet(new Vector2(0f, 1f), 0f, 0f); yield return RsGame(3f);
        float s1 = c.ForwardSpeed, d1 = Vector3.Distance(c.transform.position, p0);
        RsCheck("stick UP alone (no RT) does not drive", Mathf.Abs(s1) < 0.4f && d1 < 0.6f, "speed " + s1.ToString("0.00") + " m/s, moved " + d1.ToString("0.00") + " m in 3 s");
        RsSet(new Vector2(0f, -1f), 0f, 0f); yield return RsGame(2f);
        float s1b = c.ForwardSpeed;
        RsCheck("stick DOWN alone does not reverse", Mathf.Abs(s1b) < 0.4f, "speed " + s1b.ToString("0.00") + " m/s");
        RsNext("gas");
        RsSet(Vector2.zero, 1f, 0f); yield return RsGame(1.2f);
        float s2 = c.ForwardSpeed;
        RsCheck("RT (gas) moves the car forward", s2 > 2f, "speed " + s2.ToString("0.00") + " m/s");
        RsNext("steer");
        float h0 = c.transform.eulerAngles.y;
        RsSet(new Vector2(1f, 0f), 0.5f, 0f); yield return RsGame(1f);
        float dh = Mathf.DeltaAngle(h0, c.transform.eulerAngles.y);
        RsCheck("stick sideways steers (with RT)", Mathf.Abs(dh) > 20f, "heading change " + dh.ToString("0") + " deg in 1 s");
        RsNext("brake");
        RsSet(Vector2.zero, 0f, 1f); yield return RsGame(3f);
        float s3 = c.ForwardSpeed;
        RsCheck("LT (brake) stops then reverses", s3 < -0.5f, "speed " + s3.ToString("0.00") + " m/s");
        RsSet(Vector2.zero, 0f, 0f); yield return RsGame(1f);

        // ---- FREE ROAM (Bill 16:45): off the track toward the house, no fence / snap-back / leash; never through the house
        RsNext("roam");
        int rec0 = c.recovers, br0 = c.bringBacks; bool parkedEver = false, inHouse = false; float maxD = 0f, minHouse = 1e9f, t = 0f, r0 = Time.realtimeSinceStartup;
        Vector2 hc = Layout.HouseC, hs = Layout.HouseSize * 0.5f;
        rsHome = true;
        c.inputHook = i => RsSteerTo(c, new Vector3(hc.x + hs.x + 4f, 0f, hc.y));
        while (t < 30f && Time.realtimeSinceStartup - r0 < 240f)
        {
            yield return null; t += Time.deltaTime;
            Vector3 p = c.transform.position;
            maxD = Mathf.Max(maxD, new Vector2(p.x - RcTrack.C.x, p.z - RcTrack.C.y).magnitude);
            if (c.Parked) parkedEver = true;
            float dx = Mathf.Abs(p.x - hc.x) - hs.x, dz = Mathf.Abs(p.z - hc.y) - hs.y;
            if (dx < -0.5f && dz < -0.5f) inHouse = true;
            minHouse = Mathf.Min(minHouse, Mathf.Max(dx, dz));
            if (minHouse < 6f) break;
        }
        Vector3 pe = c.transform.position;
        RsCheck("free roam: drives off the track past the old 70 m leash, no snap back to the track / grid", maxD > 75f && c.recovers == rec0 && !parkedEver, "max " + maxD.ToString("0") + " m from the pad, recovers +" + (c.recovers - rec0) + ", parked " + parkedEver + ", at (" + pe.x.ToString("0") + ", " + pe.z.ToString("0") + ")");
        RsCheck("free roam: reaches the house and never goes through it", minHouse < 15f && !inHouse, "closest " + minHouse.ToString("0.0") + " m outside the house walls, inside " + inHouse);
        RsNext("athouse");
        RsSet(Vector2.zero, 0f, 1f); c.inputHook = i => rsIn; yield return RsGame(1.5f);
        // flipped off the track -> righted where it is
        RsNext("flip");
        Vector3 pf = c.transform.position; int rt0 = c.righted;
        c.rb.rotation = Quaternion.Euler(0f, c.transform.eulerAngles.y, 180f); c.transform.rotation = c.rb.rotation; c.rb.velocity = Vector3.zero;
        RsSet(Vector2.zero, 0f, 0f); yield return RsGame(4f);
        float fd = Vector3.Distance(new Vector3(pf.x, 0f, pf.z), new Vector3(c.transform.position.x, 0f, c.transform.position.z));
        RsCheck("flipped off the track: back on its wheels WHERE IT IS", c.transform.up.y > 0.8f && fd < 3f && c.recovers == rec0, "up " + c.transform.up.y.ToString("0.00") + " moved " + fd.ToString("0.0") + " m, righted +" + (c.righted - rt0) + " recovers +" + (c.recovers - rec0));
        // bring it back (hold B)
        RsNext("bring");
        var bi = new PIn(); bi.downHeld = true; rsIn = bi; yield return RsGame(1.8f);
        RsSet(Vector2.zero, 0f, 0f); yield return RsGame(0.5f);
        float gd = Vector3.Distance(c.transform.position, c.track.Grid[c.index]);
        RsCheck("hold B 'bring it back': the car is back at the stand's grid", c.bringBacks == br0 + 1 && gd < 2f, "bringBacks +" + (c.bringBacks - br0) + " dist to grid " + gd.ToString("0.0") + " m");
        RsNext("home");
        c.inputHook = null;

        // ---- DRIVER'S-STAND VIEW: Y toggles; the camera stays at the froggy's spot and tracks the car round a lap
        CamRig rig = slots[0].rig;
        if (rig == null) { RsCheck("P1 camera rig", false, "no rig"); RsDone(); yield break; }
        bool was = rig.rcStand; rig.ToggleRcStand(f); yield return RsGame(0.6f);
        RsCheck("Y toggles the stand view on (per player)", rig.rcStand != was && rig.RcStandActive, "rcStand " + rig.rcStand + " active " + rig.RcStandActive);
        RsNext("stand");
        c.demoAuto = true;
        Camera cm = slots[0].cam; Vector3 fp = f.transform.position;
        int fr = 0, inView = 0; float maxCamMove = 0f, fovMin = 999f, fovMax = 0f, t2 = 0f, r2 = Time.realtimeSinceStartup; Vector3 cp0 = cm.transform.position;
        while (t2 < 22f && Time.realtimeSinceStartup - r2 < 240f)
        {
            yield return null; t2 += Time.deltaTime; if (t2 < 1f) continue;
            fr++;
            Vector3 vp = cm.WorldToViewportPoint(c.transform.position);
            if (vp.z > 0f && vp.x > -0.02f && vp.x < 1.02f && vp.y > -0.02f && vp.y < 1.02f) inView++;
            maxCamMove = Mathf.Max(maxCamMove, Vector3.Distance(cm.transform.position, fp));
            fovMin = Mathf.Min(fovMin, cm.fieldOfView); fovMax = Mathf.Max(fovMax, cm.fieldOfView);
            if (fr == 200 || fr == 400) RsNext("standlap");
        }
        float vk = fr > 0 ? inView / (float)fr : 0f;
        RsCheck("stand view: camera stays at the froggy's spot (no follow-move)", fr > 20 && maxCamMove < 2.5f, "max " + maxCamMove.ToString("0.00") + " m from the froggy over " + fr + " frames");
        RsCheck("stand view: the car stays in frame round the lap (>= 85% of frames)", vk >= 0.85f, (vk * 100f).ToString("0") + "% of " + fr + " frames");
        RsCheck("stand view: FOV auto-zooms with distance", fovMax - fovMin > 3f, "FOV " + fovMin.ToString("0") + " .. " + fovMax.ToString("0"));
        c.demoAuto = false;
        rig.ToggleRcStand(f); yield return RsGame(0.6f);
        RsCheck("Y again -> back to the chase view", !rig.rcStand && !rig.RcStandActive, "rcStand " + rig.rcStand);
        RsDone();
    }
    void RsDone() { Debug.Log("RCSTICK67 RESULT " + rsPass + "/" + rsTotal); Debug.Log("FFDEMO rcstick67 done"); }
}
