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
    // ffu67d probe fix 2/3 (probe only): the old test spot (0,-14) ran the 12 m/s circle into the pond (pondSub crawl cap ~3.6 m/s).
    // fix 3: a strict 55 m empty disk found nothing on the ranch (fix-2 log 'SPOT none found'), so the fast run-up is now ON the
    // circle (steer held from the start, no straight) and FwFindSpot picks the BEST 34 m disk: every sample must be dry (PondQ > 1.1,
    // above the water line); score = samples with a non-terrain collider in a 3x2x3 m box or > 3 m off the centre height (lowest
    // wins, ties -> nearer the house). The truck starts on the disk's edge with the centre on its right (steer > 0 turns right).
    bool fwSpotDone; Vector3 fwStart; float fwHead = 90f; Vector3 fwC;
    static readonly Collider[] fwBuf = new Collider[16];
    string fwWhy = "";
    // 0 = clear, 1 = obstacle / slope, 99 = water / off the map
    int FwSample(float x, float z, float y0, GroundVehicle v, bool why)
    {
        if (Mathf.Abs(x) > Layout.Half - 10f || Mathf.Abs(z) > Layout.Half - 10f) return 99;
        if (Layout.PondQ(x, z) < 1.1f) return 99;
        float y = Ranch.GY(x, z);
        if (y < Layout.WaterY + 0.6f) return 99;
        if (Mathf.Abs(y - y0) > 3f) { if (why) fwWhy += " slope"; return 10; }   // fix 4: slope weighs 10, an obstacle 1 (obstacles get cleared)
        int n = Physics.OverlapBoxNonAlloc(new Vector3(x, y + 1.3f, z), new Vector3(1.5f, 1f, 1.5f), fwBuf, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var c = fwBuf[i];
            if (c == null || c is TerrainCollider) continue;
            if (v != null && c.transform.IsChildOf(v.transform)) continue;
            if (why) fwWhy += " " + c.name;
            return 1;
        }
        return 0;
    }
    int FwScore(float cx, float cz, float R, int cap, GroundVehicle v, bool why)
    {
        float y0 = Ranch.GY(cx, cz); int sc = FwSample(cx, cz, y0, v, why); if (sc >= 99) return 999;
        for (float r = 5f; r <= R; r += 5.8f)
        {
            int na = Mathf.Max(6, (int)(r * 6.283f / 6f));
            for (int a = 0; a < na; a++)
            {
                float t = a * 6.2832f / na; int k = FwSample(cx + r * Mathf.Cos(t), cz + r * Mathf.Sin(t), y0, v, why);
                if (k >= 99) return 999; sc += k; if (sc > cap) return sc;
            }
        }
        return sc;
    }
    IEnumerator FwFindSpot(GroundVehicle v)
    {
        if (fwSpotDone) yield break;
        fwSpotDone = true;
        const float R = 34f; int tried = 0, best = 998; float bestD = 1e9f;
        for (float cx = -160f; cx <= 160f; cx += 10f)
        {
            for (float cz = -160f; cz <= 160f; cz += 10f)
            {
                tried++;
                if (Layout.PondQ(cx, cz) < 1.1f + R / 56f) continue;
                int sc = FwScore(cx, cz, R, best, v, false); if (sc >= 999) continue;
                float d = new Vector2(cx - Layout.HouseC.x, cz - Layout.HouseC.y).magnitude;
                if (sc < best || (sc == best && d < bestD)) { best = sc; bestD = d; fwC = new Vector3(cx, Ranch.GY(cx, cz), cz); }
            }
            yield return null;   // spread the search over frames
        }
        if (best < 998)
        {
            fwWhy = ""; FwScore(fwC.x, fwC.z, R, 9999, v, true);
            if (fwWhy.Length > 160) fwWhy = fwWhy.Substring(0, 160);
            // start on the circle of radius ~22 m (12 m/s, steer 0.35) round the centre: heading +x, the centre on the right (-z)
            fwHead = 90f; fwStart = fwC + new Vector3(0f, 0f, 22f); fwStart.y = Ranch.GY(fwStart.x, fwStart.z);
            FwClearArena(v, R + 10f);
            Debug.Log("FOURWS67 SPOT best dry lawn centre (" + fwC.x.ToString("0") + ", " + fwC.z.ToString("0") + ") y " + fwC.y.ToString("0.0") + " r " + R + " m: " + best + " blocked samples [" + fwWhy.Trim() + "], start (" + fwStart.x.ToString("0") + ", " + fwStart.z.ToString("0") + ") heading " + fwHead + " | PondQ " + Layout.PondQ(fwC.x, fwC.z).ToString("0.00") + " | " + tried + " candidates");
        }
        else
        {
            fwStart = new Vector3(0f, Ranch.GY(0f, -14f), -14f); fwHead = 90f;
            Debug.Log("FOURWS67 SPOT none found in " + tried + " candidates - falling back to (0,-14)");
        }
    }
    // fix 4 (probe only): try 3 picked the best disk at (-110, 0) but it still held trees / rocks / parked mechs, and the 12 m/s circle
    // wedged the truck on one (speed 0.0 at (-141, 1)). The test arena is now CLEARED for the run: every non-terrain, non-trigger
    // collider within R + 10 m (not the truck, not froggies' CharacterControllers) is switched off - a dynamic body is made kinematic
    // first so it cannot fall - and all of it is restored when the probe ends.
    readonly System.Collections.Generic.List<Collider> fwOff = new System.Collections.Generic.List<Collider>();
    readonly System.Collections.Generic.List<Rigidbody> fwKin = new System.Collections.Generic.List<Rigidbody>();
    void FwClearArena(GroundVehicle v, float r)
    {
        var hits = Physics.OverlapSphere(new Vector3(fwC.x, fwC.y + 2f, fwC.z), r, ~0, QueryTriggerInteraction.Ignore);
        var names = new System.Collections.Generic.Dictionary<string, int>();
        foreach (var c in hits)
        {
            if (c == null || !c.enabled || c is TerrainCollider || c is CharacterController) continue;
            if (v != null && c.transform.IsChildOf(v.transform)) continue;
            var rb = c.attachedRigidbody;
            if (rb != null && !rb.isKinematic && !fwKin.Contains(rb)) { rb.isKinematic = true; fwKin.Add(rb); }
            c.enabled = false; fwOff.Add(c);
            string n = c.name.Length > 24 ? c.name.Substring(0, 24) : c.name; names[n] = names.TryGetValue(n, out int k) ? k + 1 : 1;
        }
        string list = ""; foreach (var kv in names) list += kv.Key + " x" + kv.Value + ", ";
        if (list.Length > 220) list = list.Substring(0, 220);
        Debug.Log("FOURWS67 ARENA cleared " + fwOff.Count + " colliders (" + fwKin.Count + " bodies held) within " + r.ToString("0") + " m of (" + fwC.x.ToString("0") + ", " + fwC.z.ToString("0") + "): " + list);
    }
    void FwRestoreArena()
    {
        foreach (var c in fwOff) if (c != null) c.enabled = true;
        foreach (var rb in fwKin) if (rb != null) rb.isKinematic = false;
        if (fwOff.Count > 0) Debug.Log("FOURWS67 ARENA restored " + fwOff.Count + " colliders");
        fwOff.Clear(); fwKin.Clear();
    }
    float fwR, fwDia, fwRear, fwFront, fwYaw, fwLat, fwSpd;
    // circle at (speed, steer): settle, then measure 4 s of game time (yaw-rate radius, path diameter, rear wheel angle)
    IEnumerator FwCircle(GroundVehicle v, float speed, float steer, string tag)
    {
        FwPark(v, fwStart.x, fwStart.z, fwHead); yield return FwF(3);
        // fix 3: the fast run-up is on the circle itself (steer held), so it never needs a long straight lane
        fwSpeed = speed; fwSteer = speed > 6f ? steer : 0f; yield return FwGame(speed > 6f ? 5f : 2.5f);   // run-up to speed
        if (speed > 6f) Debug.Log("FOURWS67 RUNUP " + tag + " at (" + v.transform.position.x.ToString("0") + ", " + v.transform.position.z.ToString("0") + ") speed " + v.ForwardSpeed.ToString("0.0") + " m/s kinematic " + v.rb.isKinematic + " up " + v.transform.up.y.ToString("0.00"));
        fwSteer = steer; yield return FwGame(3f);
        float sv = 0f, sy = 0f, sl = 0f; int n = 0; float t = 0f, r0 = Time.realtimeSinceStartup; float subMax = v.pondSub, vMax = 0f;
        Vector3 c0 = v.transform.position; float dmax = 0f; float rearSum = 0f, frontSum = 0f; float h0 = v.transform.eulerAngles.y, hTurn = 0f, hPrev = h0;
        var pts = new System.Collections.Generic.List<Vector3>();
        while (t < 4f && Time.realtimeSinceStartup - r0 < 150f)
        {
            yield return null; t += Time.deltaTime;
            Vector3 vel = v.rb.velocity; vel.y = 0f;
            subMax = Mathf.Max(subMax, v.pondSub); vMax = Mathf.Max(vMax, vel.magnitude);
            sv += vel.magnitude; sy += Mathf.Abs(v.rb.angularVelocity.y); sl += Mathf.Abs(Vector3.Dot(vel, v.transform.right)); n++;
            Vector3 p = v.transform.position; p.y = 0f; pts.Add(p);
            float h = v.transform.eulerAngles.y; hTurn += Mathf.Abs(Mathf.DeltaAngle(hPrev, h)); hPrev = h;
            foreach (var w in v.wheels) if (w.visual != null) { float a = w.yawDeg; if (w.mount.z < 0f) rearSum += a; else frontSum += a; }
        }
        foreach (var a in pts) foreach (var b in pts) dmax = Mathf.Max(dmax, (a - b).magnitude);
        fwR = n > 0 && sy > 1e-4f ? sv / sy : 999f; fwDia = hTurn >= 300f ? dmax : -1f;
        fwRear = n > 0 ? rearSum / (n * 2f) : 0f; fwFront = n > 0 ? frontSum / (n * 2f) : 0f; fwYaw = n > 0 ? sy / n : 0f; fwLat = n > 0 ? sl / n : 0f;
        fwSpd = n > 0 ? sv / n : 0f;
        Debug.Log("FOURWS67 R " + tag + " speed " + (n > 0 ? sv / n : 0f).ToString("0.0") + " m/s yaw " + (fwYaw * Mathf.Rad2Deg).ToString("0") + " deg/s -> radius " + fwR.ToString("0.00") + " m | path diameter " + (fwDia > 0f ? fwDia.ToString("0.0") + " m" : "n/a (turned " + hTurn.ToString("0") + " deg)") + " | wheel yaw front " + fwFront.ToString("0.0") + " rear " + fwRear.ToString("0.0") + " | lateral " + fwLat.ToString("0.00") + " m/s | k " + v.bigK.ToString("0.00") + " rearSteer " + v.rearSteer + " crab " + v.crab + " | max " + vMax.ToString("0.0") + " m/s pondSub max " + subMax.ToString("0.00") + " at (" + v.transform.position.x.ToString("0") + ", " + v.transform.position.z.ToString("0") + ") " + (Layout.InPond(v.transform.position.x, v.transform.position.z) ? "IN POND" : "dry"));
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
        yield return FwFindSpot(ja);
        FwPark(ja, fwStart.x, fwStart.z, fwHead); yield return FwF(2);
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
    void FwDone() { FwRestoreArena(); Debug.Log("FOURWS67 RESULT " + fwPass + "/" + fwTotal); Debug.Log("FFDEMO fourws67 done"); }
}
