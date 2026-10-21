using System.Collections;
using UnityEngine;

// ffu67d probe: ?ffdemo=1&ffshot=fourws67 - CYBERTRUCK FOUR-WHEEL STEERING (Bill Oct 20 16:20: monster-truck style, rears steer
// opposite the fronts for a much tighter circle at low speed, blending toward less at speed). P1 drives James's Cybertruck on the
// lawn through the vehicle input hook (the same ComputeControls path every driver - player, robot, Dad - goes through), full lock
// at a held speed. Turning circle (R = speed / yaw rate, + the path's diameter) BEFORE (rearSteer 0 = the old 2WS truck) and AFTER
// (rearSteer 1) at 3 m/s and 12 m/s, at stock and 3x wheels; rear wheel meshes turned opposite; crab steer slides sideways.
// Logs "FOURWS67 PASS|FAIL ...", "FOURWS67 R ...", "FOURWS67 RESULT n/m", "FFDEMO fourws67 ph N <name>", "FFDEMO fourws67 done".
public class Ffu67d : MonoBehaviour
{
    public static Ffu67d I;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        I = new GameObject("ffu67d").AddComponent<Ffu67d>();
        DontDestroyOnLoad(I.gameObject);
    }
    bool started; public bool camOn; public Vector3 camPos, camLook;
    void Update()
    {
        if (started) return;
        string s = Ffu33.Url("ffdemo") != null ? Ffu33.Url("ffshot") : null;
        if (s != "fourws67") { enabled = false; return; }
        if (Game.I == null || Game.I.state != Game.State.Play || Game.I.slots.Count == 0) return;
        started = true;
        Game.I.StartCoroutine(Game.I.Fws67Run());
    }
    void LateUpdate()
    {
        if (!camOn || Game.I == null || Game.I.slots.Count == 0) return;
        Camera c = Game.I.slots[0].cam; if (c == null) return;
        c.transform.position = camPos; c.transform.LookAt(camLook);
    }
}

public partial class Game
{
    int fwPass, fwTotal, fwPh;
    float fwSteer, fwSpeed;
    void FwCheck(string what, bool ok, string d) { fwTotal++; if (ok) fwPass++; Debug.Log("FOURWS67 " + (ok ? "PASS" : "FAIL") + " " + what + " | " + d); }
    void FwNext(string n) { Debug.Log("FFDEMO fourws67 ph " + (fwPh++) + " " + n + " t " + Time.time.ToString("0.0")); }
    IEnumerator FwF(int n) { for (int k = 0; k < n; k++) yield return null; }
    IEnumerator FwGame(float s) { float t = 0f, r0 = Time.realtimeSinceStartup; while (t < s && Time.realtimeSinceStartup - r0 < 150f) { yield return null; t += Time.deltaTime; } }
    PIn FwHook(GroundVehicle v, PIn i)
    {
        var o = new PIn();
        float fs = v.ForwardSpeed;
        o.move = new Vector2(fwSteer, 0f);
        if (fwSpeed <= 0.01f) { o.brake = fs > 0.3f ? 1f : 0f; return o; }
        o.gas = Mathf.Clamp01((fwSpeed - fs) * 0.5f + 0.08f);
        if (fs > fwSpeed + 1.2f) { o.gas = 0f; o.brake = 0.3f; }
        return o;
    }
    void FwPark(GroundVehicle v, float x, float z, float yaw)
    {
        Vector3 p = new Vector3(x, Ranch.GY(x, z) + 0.6f + v.bigLift, z);
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        v.rb.velocity = Vector3.zero; v.rb.angularVelocity = Vector3.zero;
        v.rb.position = p; v.rb.rotation = q; v.transform.SetPositionAndRotation(p, q);
    }
    float fwR, fwDia, fwRear, fwFront, fwYaw, fwLat, fwSpd;
    // circle at (speed, steer): settle, then measure 4 s of game time (yaw-rate radius, path diameter, rear wheel angle)
    IEnumerator FwCircle(GroundVehicle v, float speed, float steer, string tag)
    {
        FwPark(v, 0f, -14f, 90f); yield return FwF(3);
        fwSpeed = speed; fwSteer = 0f; yield return FwGame(speed > 6f ? 4.5f : 2.5f);   // straight run-up to speed
        fwSteer = steer; yield return FwGame(3f);
        float sv = 0f, sy = 0f, sl = 0f; int n = 0; float t = 0f, r0 = Time.realtimeSinceStartup;
        Vector3 c0 = v.transform.position; float dmax = 0f; float rearSum = 0f, frontSum = 0f; float h0 = v.transform.eulerAngles.y, hTurn = 0f, hPrev = h0;
        var pts = new System.Collections.Generic.List<Vector3>();
        while (t < 4f && Time.realtimeSinceStartup - r0 < 150f)
        {
            yield return null; t += Time.deltaTime;
            Vector3 vel = v.rb.velocity; vel.y = 0f;
            sv += vel.magnitude; sy += Mathf.Abs(v.rb.angularVelocity.y); sl += Mathf.Abs(Vector3.Dot(vel, v.transform.right)); n++;
            Vector3 p = v.transform.position; p.y = 0f; pts.Add(p);
            float h = v.transform.eulerAngles.y; hTurn += Mathf.Abs(Mathf.DeltaAngle(hPrev, h)); hPrev = h;
            foreach (var w in v.wheels) if (w.visual != null) { float a = w.yawDeg; if (w.mount.z < 0f) rearSum += a; else frontSum += a; }
        }
        foreach (var a in pts) foreach (var b in pts) dmax = Mathf.Max(dmax, (a - b).magnitude);
        fwR = n > 0 && sy > 1e-4f ? sv / sy : 999f; fwDia = hTurn >= 300f ? dmax : -1f;
        fwRear = n > 0 ? rearSum / (n * 2f) : 0f; fwFront = n > 0 ? frontSum / (n * 2f) : 0f; fwYaw = n > 0 ? sy / n : 0f; fwLat = n > 0 ? sl / n : 0f;
        fwSpd = n > 0 ? sv / n : 0f;
        Debug.Log("FOURWS67 R " + tag + " speed " + (n > 0 ? sv / n : 0f).ToString("0.0") + " m/s yaw " + (fwYaw * Mathf.Rad2Deg).ToString("0") + " deg/s -> radius " + fwR.ToString("0.00") + " m | path diameter " + (fwDia > 0f ? fwDia.ToString("0.0") + " m" : "n/a (turned " + hTurn.ToString("0") + " deg)") + " | wheel yaw front " + fwFront.ToString("0.0") + " rear " + fwRear.ToString("0.0") + " | lateral " + fwLat.ToString("0.00") + " m/s | k " + v.bigK.ToString("0.00") + " rearSteer " + v.rearSteer + " crab " + v.crab);
    }

    public IEnumerator Fws67Run()
    {
        yield return new WaitForSecondsRealtime(3f); yield return FwF(2);
        var cam = Ffu67d.I;
        Frog f = frogs[slots[0].frog];
        GroundVehicle ja = null;
        foreach (var v in FindObjectsOfType<GroundVehicle>()) if (v.Title == "James's Cybertruck") { ja = v; break; }
        int n4 = 0, nOther = 0, nCyb = 0; foreach (var v in FindObjectsOfType<GroundVehicle>()) { bool cy = v.Title.Contains("Cybertruck"); if (cy) nCyb++; if (v.rearSteer > 0f) { if (cy) n4++; else nOther++; } }
        FwCheck("every Cybertruck has four-wheel steering (James's, Dad's, the shared one), nothing else", ja != null && ja.rearSteer > 0f && n4 == nCyb && n4 >= 3 && nOther == 0, n4 + "/" + nCyb + " Cybertrucks 4WS, " + nOther + " other 4WS vehicles");
        if (ja == null) { FwDone(); yield break; }
        BigWheels.SetFor(ja, 1f, "demo", true, f);
        if (f.vehicle != null) f.ExitVehicle();
        FwPark(ja, 0f, -14f, 90f); yield return FwF(2);
        f.EnterVehicle(ja); yield return FwF(4);
        FwCheck("P1 drives James's Cybertruck", f.vehicle == ja, f.vehicle != null ? f.vehicle.Title : "-");
        ja.inputHook = i => FwHook(ja, i);
        cam.camOn = true;

        // BEFORE = the old 2WS truck (rearSteer 0)
        ja.rearSteer = 0f; ja.crab = false;
        yield return FwCircle(ja, 3f, 1f, "BEFORE low"); float bL = fwR, bLd = fwDia, bRear = fwRear;
        Vector3 rp = ja.transform.TransformPoint(new Vector3(0f, 0.5f, -1.7f));
        cam.camPos = rp - ja.transform.forward * 4.5f + ja.transform.right * 3.2f + Vector3.up * 2.4f; cam.camLook = rp;
        FwNext("before"); yield return FwF(3);
        yield return FwCircle(ja, 12f, 0.35f, "BEFORE high"); float bH = fwR;

        // AFTER = four-wheel steering
        ja.rearSteer = 1f;
        yield return FwCircle(ja, 3f, 1f, "AFTER low"); float aL = fwR, aLd = fwDia, aRear = fwRear, aFront = fwFront;
        rp = ja.transform.TransformPoint(new Vector3(0f, 0.5f, -1.7f));
        cam.camPos = rp - ja.transform.forward * 4.5f + ja.transform.right * 3.2f + Vector3.up * 2.4f; cam.camLook = rp;
        FwNext("rearturn"); yield return FwF(3);
        FwCheck("BEFORE (2WS): rear wheels straight", Mathf.Abs(bRear) < 1f, "rear " + bRear.ToString("0.0") + " deg");
        FwCheck("AFTER low speed: rear wheel meshes turn OPPOSITE the fronts (visibly, > 15 deg)", aRear * aFront < 0f && Mathf.Abs(aRear) > 15f, "front " + aFront.ToString("0.0") + " rear " + aRear.ToString("0.0"));
        FwCheck("AFTER low speed (3 m/s): turning circle much tighter (radius <= 65% of 2WS)", aL < bL * 0.65f, "radius " + bL.ToString("0.00") + " -> " + aL.ToString("0.00") + " m, path diameter " + bLd.ToString("0.0") + " -> " + aLd.ToString("0.0") + " m");
        yield return FwCircle(ja, 12f, 0.35f, "AFTER high"); float aH = fwR, aHRear = fwRear, aHFront = fwFront;
        // fix 1: steer 0.35 at 12 m/s (full lock scrubbed the truck down to ~4 m/s, so the blend never kicked in); compare rear/front RATIO
        float rLow = Mathf.Abs(aRear) / Mathf.Max(1f, Mathf.Abs(aFront)), rHigh = Mathf.Abs(aHRear) / Mathf.Max(1f, Mathf.Abs(aHFront));
        FwCheck("AFTER 12 m/s: blended (held >= 9 m/s, rear/front ratio < half the 3 m/s one, radius between 60% and 100% of 2WS), upright", fwSpd >= 9f && rHigh < rLow * 0.5f && aH > bH * 0.6f && aH <= bH * 1.001f && ja.transform.up.y > 0.8f, "speed " + fwSpd.ToString("0.0") + " m/s, radius " + bH.ToString("0.00") + " -> " + aH.ToString("0.00") + " m, rear " + aHRear.ToString("0.0") + " / front " + aHFront.ToString("0.0") + " (ratio " + rHigh.ToString("0.00") + " vs low " + rLow.ToString("0.00") + "), RearK(4/8/12/15) " + ja.RearK(4f).ToString("0.00") + "/" + ja.RearK(8f).ToString("0.00") + "/" + ja.RearK(12f).ToString("0.00") + "/" + ja.RearK(15f).ToString("0.00") + ", up " + ja.transform.up.y.ToString("0.00"));

        // every 67a wheel size: 3x
        fwSpeed = 0f; fwSteer = 0f; yield return FwGame(1.5f);
        BigWheels.SetFor(ja, 3f, "demo", true, f); yield return FwGame(4f);
        yield return FwCircle(ja, 3f, 1f, "AFTER 3x low"); float kL = fwR, kRear = fwRear; float kk = ja.bigK;
        rp = ja.transform.TransformPoint(new Vector3(0f, 0.5f, -1.7f));
        cam.camPos = rp - ja.transform.forward * 7f + ja.transform.right * 5f + Vector3.up * 3.5f; cam.camLook = rp;
        FwNext("rear3x"); yield return FwF(3);
        int gr = 0; foreach (var w in ja.wheels) if (w.grounded) gr++;
        FwCheck("3x wheels: rears still steer opposite, circle still tight, wheels on the ground", kk > 2.5f && kRear * aFront < 0f && Mathf.Abs(kRear) > 15f && kL < bL * 0.65f && gr >= 3, "k " + kk.ToString("0.00") + " radius " + kL.ToString("0.00") + " m rear " + kRear.ToString("0.0") + " grounded " + gr + "/4");
        fwSpeed = 0f; fwSteer = 0f; yield return FwGame(1.5f);
        BigWheels.SetFor(ja, 1f, "demo", true, f); yield return FwGame(4f);

        // crab steer (per truck): rears WITH the fronts -> little yaw, slides sideways
        ja.crab = true;
        yield return FwCircle(ja, 4f, 0.7f, "CRAB"); float cR = fwR, cLat = fwLat, cRear = fwRear, cFront = fwFront;
        FwNext("crab"); yield return FwF(3);
        FwCheck("crab steer: rears turn WITH the fronts, the truck slides diagonally with little yaw", cRear * cFront > 0f && cLat > 0.8f && cR > aL * 3f, "front " + cFront.ToString("0.0") + " rear " + cRear.ToString("0.0") + " lateral " + cLat.ToString("0.00") + " m/s radius " + cR.ToString("0.0") + " m");
        ja.crab = false;
        int other = 0; foreach (var v in FindObjectsOfType<GroundVehicle>()) if (v != ja && v.crab) other++;
        FwCheck("crab is per truck (no other vehicle changed)", other == 0, other + " others in crab");

        fwSpeed = 0f; fwSteer = 0f; yield return FwGame(1.5f);
        ja.inputHook = null; cam.camOn = false;
        FwDone();
    }
    void FwDone() { Debug.Log("FOURWS67 RESULT " + fwPass + "/" + fwTotal); Debug.Log("FFDEMO fourws67 done"); }
}
