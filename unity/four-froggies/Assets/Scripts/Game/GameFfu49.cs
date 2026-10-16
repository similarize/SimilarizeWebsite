using UnityEngine;

// ffu49 hook-up + demos (kept out of Bootstrap / Game like ffu33 / ffu48): once the ranch + Game exist it parks the Roadster
// in the garage (bay 1, right of Dad's Cybertruck, nose out) and builds the C-ramp by the loop (CRamp49).
//  ?ffdemo=1&ffshot=roadster-roof : launch test on the east lawn (0-60 mph timer), slow to 6 m/s, thruster hop over the east
//      parapet gap onto the ranch house roof, eases down, brakes. Logs "FFDEMO roadster ..." + "roadster49 done".
//  ?ffdemo=1&ffshot=roadster-roll : up the C-ramp at crawl speed, sticks to the ceiling, stops upside down, then jumps off
//      with the thrusters + barrel roll (half roll from upside down) + soft landing.
//  ?ffdemo=1&ffshot=roadster-loop : the existing loop at ~3.5 m/s (13 km/h) with the suction fans, logs the inverted pass.
[DefaultExecutionOrder(100)]
public class Ffu49 : MonoBehaviour
{
    public static Ffu49 I;
    bool built; string shot = "";
    float t0 = -1f, logT, phT; int ph;
    Vector3 camPos, camLook; bool camInit;
    Frog f; Roadster49 car;
    float airVy, airMax, invT, invVmin = 99f, invVmax, minUp = 1f; int invGmin = 9; bool wasAir, landedLogged, inverted;
    float startAir; Vector3 hopFrom; float rollT0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        I = new GameObject("ffu49").AddComponent<Ffu49>();
        DontDestroyOnLoad(I.gameObject);
    }

    void Update()
    {
        if (!built)
        {
            if (Bootstrap.I == null || Game.I == null || Time.timeSinceLevelLoad < 0.5f) return;
            built = true;
            try
            {
                float x = Layout.BayX(1) + 2.9f, z = Layout.GarageC.y + 3.0f;
                Roadster49.Spawn(new Vector3(x, Ranch.GY(x, z) + 0.45f, z), 0f);
            }
            catch (System.Exception e) { Debug.LogError("ffu49 roadster: " + e); }
            try { CRamp49.Build(); } catch (System.Exception e) { Debug.LogError("ffu49 c-ramp: " + e); }
            shot = Ffu33.Url("ffdemo") != null ? (Ffu33.Url("ffshot") ?? "") : "";
            if (!shot.StartsWith("roadster")) shot = "";
            if (shot.Length > 0) Debug.Log("FFDEMO ffu49 demo " + shot);
        }
        if (shot.Length == 0 || Game.I.state != Game.State.Play || Game.I.slots.Count == 0 || Roadster49.I == null) return;
        car = Roadster49.I;
        if (t0 < 0f) { t0 = Time.time; f = Game.I.frogs[Game.I.slots[0].frog]; Frog.DtCap = 0.15f; }
        float t = Time.time - t0;
        phT += Time.deltaTime;
        Track();
        if (shot == "roadster-roof") Roof(t);
        else if (shot == "roadster-roll") Roll(t);
        else if (shot == "roadster-loop") Loop(t);
        if (Time.unscaledTime > logT)
        {
            logT = Time.unscaledTime + 1f;
            Vector3 p = car.transform.position;
            Debug.Log("FFDEMO roadster " + shot + " ph " + ph + " t " + t.ToString("0.0") + " pos " + p.ToString("0.0") + " v " + car.Velocity.magnitude.ToString("0.0") + " fs " + car.ForwardSpeed.ToString("0.0") + " up " + car.transform.up.y.ToString("0.00") + " g " + car.grounded + " suc " + car.sucK.ToString("0.0") + " crawl " + car.crawl + " air " + car.air.ToString("0") + " agl " + car.agl.ToString("0.0") + " jets " + car.jetsOn + " land " + car.landing + " roll " + car.rolling + " f.veh " + (f.vehicle != null ? f.vehicle.Title : "-"));
        }
    }

    void Next(int p) { ph = p; phT = 0f; }

    // touchdown / inverted bookkeeping
    void Track()
    {
        if (car == null || car.rb == null) return;
        bool air = car.grounded == 0;
        if (air) { airVy = car.rb.velocity.y; airMax = Mathf.Max(airMax, car.transform.position.y); }
        if (wasAir && !air && car.airT == 0f && ph > 0) Debug.Log("FFDEMO roadster touchdown at " + car.transform.position.ToString("0.0") + " vy " + airVy.ToString("0.0") + " up " + car.transform.up.y.ToString("0.00") + " peak y " + airMax.ToString("0.0") + " air " + car.air.ToString("0") + " L");
        if (!air) airMax = car.transform.position.y;
        wasAir = air;
        minUp = Mathf.Min(minUp, car.transform.up.y);
        if (car.transform.up.y < -0.9f && !air)
        {
            inverted = true; invT += Time.deltaTime; float v = car.Velocity.magnitude;
            invVmin = Mathf.Min(invVmin, v); invVmax = Mathf.Max(invVmax, v); invGmin = Mathf.Min(invGmin, car.grounded);
        }
    }

    void Enter(Vector3 p, Quaternion q, float v0)
    {
        if (f.vehicle != null) f.ExitVehicle();
        f.SendTo(WorldId.Ranch, p + q * Vector3.left * 2f, q.eulerAngles.y);
        car.rb.isKinematic = false; car.rb.angularVelocity = Vector3.zero;
        car.rb.position = p; car.rb.rotation = q; car.transform.SetPositionAndRotation(p, q);
        car.rb.velocity = q * Vector3.forward * v0;
        f.EnterVehicle(car);
        car.fansOn = true; car.air = Roadster49.TankL;
        startAir = car.air;
    }

    // speed controller + lane steering in the car's own frame (works upside down)
    static PIn Pilot(Roadster49 c, float vt, Vector3 laneP, Vector3 laneT)
    {
        var o = new PIn();
        Vector3 up = c.transform.up;
        Vector3 fw = Vector3.ProjectOnPlane(c.transform.forward, up);
        Vector3 tn = Vector3.ProjectOnPlane(laneT, up);
        float ang = Vector3.SignedAngle(fw, tn, up);
        float e = Vector3.Dot(c.transform.position - laneP, c.transform.right);
        o.move = new Vector2(Mathf.Clamp(ang / 25f - e * 0.35f, -1f, 1f), 0f);
        float fs = c.ForwardSpeed;
        if (fs < vt) o.gas = Mathf.Clamp01((vt - fs) * 0.25f + 0.1f); else o.brake = Mathf.Clamp01((fs - vt) * 0.25f);
        return o;
    }

    // ---------------- roof hop ----------------
    void Roof(float t)
    {
        const float lane = 8f;
        switch (ph)
        {
            case 0:
                if (t < 4f) break;
                Enter(new Vector3(42f, Ranch.GY(42f, lane) + 0.5f, lane), Quaternion.Euler(0f, 270f, 0f), 0f);
                car.inputHook = i => { var o = Pilot(car, 99f, new Vector3(0f, 0f, lane), Vector3.left); o.gas = 1f; o.brake = 0f; return o; };
                Next(1); Debug.Log("FFDEMO roadster roof start: launch from " + car.transform.position.ToString("0.0") + " heading west on z " + lane);
                break;
            case 1:
                if (car.ForwardSpeed >= 26.82f || phT > 4f)
                {
                    Debug.Log("FFDEMO roadster LAUNCH 0-60 mph: " + (Roadster49.LastLaunch > 0f ? Roadster49.LastLaunch.ToString("0.00") + " s (car timer)" : "n/a") + ", demo clock " + phT.ToString("0.00") + " s, fs " + car.ForwardSpeed.ToString("0.0") + " at x " + car.transform.position.x.ToString("0.0"));
                    Next(2);
                    car.inputHook = i => Pilot(car, 6f, new Vector3(0f, 0f, lane), Vector3.left);
                }
                break;
            case 2:
                if (car.transform.position.x <= -2f)
                {
                    Next(3); hopFrom = car.transform.position; Time.timeScale = 0.45f;
                    Debug.Log("FFDEMO roadster HOP: thrusters at " + hopFrom.ToString("0.0") + " fs " + car.ForwardSpeed.ToString("0.0"));
                }
                break;
            case 3:
                {
                    // hold thrusters 1.0 s of game time
                    float gt = phT;   // phT counts scaled time
                    car.inputHook = i => { var o = new PIn(); o.upHeld = gt < 1.0f; return o; };
                    if (phT > 1.2f && car.grounded > 0) { Next(4); car.inputHook = i => { var o = new PIn(); o.brake = 1f; return o; }; }
                    else if (phT > 12f) { Next(4); Debug.Log("FFDEMO roadster HOP never landed"); }
                }
                break;
            case 4:
                if (phT > 3f)
                {
                    Time.timeScale = 1f;
                    Vector3 p = car.transform.position;
                    bool inHouse = p.x > Layout.HouseC.x - Layout.HouseSize.x * 0.5f && p.x < Layout.HouseC.x + Layout.HouseSize.x * 0.5f && p.z > Layout.HouseC.y - Layout.HouseSize.y * 0.5f && p.z < Layout.HouseC.y + Layout.HouseSize.y * 0.5f;
                    bool onRoof = inHouse && p.y > Layout.HouseH - 0.5f && car.grounded > 0;
                    Debug.Log("FFDEMO roadster ROOF " + (onRoof ? "LANDED ON THE ROOF" : "missed the roof") + " at " + p.ToString("0.00") + " (roof top ~" + (Layout.HouseH + 0.1f).ToString("0.0") + ") up " + car.transform.up.y.ToString("0.00") + " v " + car.Velocity.magnitude.ToString("0.0") + " air used " + (startAir - car.air).ToString("0") + " L (refills on the ground)");
                    Debug.Log("FFDEMO roadster49 done roof t=" + t.ToString("0"));
                    Next(5);
                }
                break;
        }
        CamChase(ph >= 3 ? 1 : 0);
    }

    // ---------------- C-ramp: ceiling, jump off, barrel roll, soft landing ----------------
    void Roll(float t)
    {
        if (!CRamp49.Built) { RollFallback(t); return; }
        float sTop = CRamp49.Lead + Mathf.PI * CRamp49.R;
        Vector3 lp, lt, ln;
        float s = CRamp49.Nearest(car.transform.position, out lp, out lt, out ln);
        switch (ph)
        {
            case 0:
                if (t < 4f) break;
                {
                    Vector3 p = CRamp49.W2(new Vector3(0f, 0.55f, -CRamp49.Lead + 1f));
                    Enter(p, CRamp49.Rot, 2f);
                    car.inputHook = i => { Vector3 a, b, c; CRamp49.Nearest(car.transform.position, out a, out b, out c); return Pilot(car, 4f, a, b); };
                    Next(1); Debug.Log("FFDEMO roadster ramp start at " + p.ToString("0.0") + " ceiling at y " + (CRamp49.Origin.y + 2f * CRamp49.R).ToString("0.0"));
                }
                break;
            case 1:
                if (car.crawl) car.demoCrawl = 3.5f;
                if (s > sTop + 6f && car.transform.up.y < -0.9f)
                {
                    Next(2); car.demoCrawl = 0f;
                    Debug.Log("FFDEMO roadster CEILING: stuck upside down at " + car.transform.position.ToString("0.0") + " up " + car.transform.up.y.ToString("0.00") + " grounded " + car.grounded + " suc " + car.sucK.ToString("0.00"));
                }
                else if (phT > 60f) { Next(2); Debug.Log("FFDEMO roadster ramp: never reached the ceiling (s " + s.ToString("0.0") + ")"); }
                break;
            case 2:
                car.inputHook = i => { var o = new PIn(); return o; };
                if (phT > 2.5f)
                {
                    Debug.Log("FFDEMO roadster CEILING hold 2.5 s: still at " + car.transform.position.ToString("0.0") + " v " + car.Velocity.magnitude.ToString("0.00") + " up " + car.transform.up.y.ToString("0.00") + " grounded " + car.grounded);
                    Time.timeScale = 0.4f; rollT0 = Time.time;
                    car.DemoRoll(1f); car.demoCrawl = -1f;
                    Next(3);
                }
                break;
            case 3:
                if (!car.rolling && phT > 0.3f && car.grounded > 0) { Next(4); car.inputHook = i => { var o = new PIn(); o.brake = 1f; return o; }; }
                else if (phT > 20f) Next(4);
                break;
            case 4:
                if (phT > 3f)
                {
                    Time.timeScale = 1f;
                    Debug.Log("FFDEMO roadster ROLL result: rolls " + Roadster49.Rolls + " on its wheels " + (car.transform.up.y > 0.9f && car.grounded >= 3) + " at " + car.transform.position.ToString("0.0") + " up " + car.transform.up.y.ToString("0.00") + " min up seen " + minUp.ToString("0.00") + " air left " + car.air.ToString("0") + " L");
                    Debug.Log("FFDEMO roadster49 done roll t=" + t.ToString("0"));
                    Next(5);
                }
                break;
        }
        CamRamp();
    }
    void RollFallback(float t)
    {
        switch (ph)
        {
            case 0:
                if (t < 4f) break;
                Enter(new Vector3(42f, Ranch.GY(42f, 8f) + 0.5f, 8f), Quaternion.Euler(0f, 270f, 0f), 0f);
                Next(1); Time.timeScale = 0.45f; Debug.Log("FFDEMO roadster roll (no ramp): hop up from the lawn");
                break;
            case 1:
                { float gt = phT; car.inputHook = i => { var o = new PIn(); o.upHeld = gt < 1.4f; return o; }; }
                if (phT > 1.9f) { car.DemoRoll(1f); Next(3); }
                break;
            case 3:
                if (!car.rolling && phT > 0.3f && car.grounded > 0) { Next(4); car.inputHook = i => { var o = new PIn(); o.brake = 1f; return o; }; }
                else if (phT > 20f) Next(4);
                break;
            case 4:
                if (phT > 3f) { Time.timeScale = 1f; Debug.Log("FFDEMO roadster ROLL result: on its wheels " + (car.transform.up.y > 0.9f) + " up " + car.transform.up.y.ToString("0.00")); Debug.Log("FFDEMO roadster49 done roll t=" + t.ToString("0")); Next(5); }
                break;
        }
        CamChase(1);
    }

    // ---------------- the loop, slowly ----------------
    void Loop(float t)
    {
        switch (ph)
        {
            case 0:
                if (t < 4f) break;
                {
                    Vector3 p = new Vector3(Layout.ApproachX + 2f, RallyTrack.LinkJoin.y + 0.6f, Layout.LoopC.y);
                    Enter(p, Quaternion.LookRotation(Vector3.right, Vector3.up), 3f);
                    car.inputHook = i =>
                    {
                        Vector3 lp, lf, lr, lu; float arc;
                        if (!RallyTrack.BranchNearest(car.transform.position, out lp, out lf, out lr, out lu, out arc)) { lp = car.transform.position; lf = car.transform.forward; }
                        return Pilot(car, 3.5f, lp, lf);
                    };
                    Next(1); Debug.Log("FFDEMO roadster loop start at " + p.ToString("0.0") + " joints in " + RallyTrack.JointIn.ToString("0.0") + " out " + RallyTrack.JointOut.ToString("0.0"));
                }
                break;
            case 1:
                if (car.crawl) car.demoCrawl = 3.5f; else car.demoCrawl = -1f;
                if (inverted && car.transform.up.y > 0.9f && car.transform.position.x > RallyTrack.JointOut.x + 6f)
                {
                    Next(2); car.demoCrawl = -1f; car.inputHook = i => { var o = new PIn(); o.brake = 1f; return o; };
                    Debug.Log("FFDEMO roadster LOOP pass: upside down (up < -0.9, wheels on) for " + invT.ToString("0.0") + " s, speed there " + invVmin.ToString("0.0") + ".." + invVmax.ToString("0.0") + " m/s (" + (invVmax * 3.6f).ToString("0") + " km/h max), min wheels on " + invGmin + ", min up " + minUp.ToString("0.00") + ", out at " + car.transform.position.ToString("0.0"));
                }
                else if (phT > 150f) { Next(2); Debug.Log("FFDEMO roadster LOOP FAIL: no complete pass (inverted " + inverted + " min up " + minUp.ToString("0.00") + ") at " + car.transform.position.ToString("0.0")); }
                break;
            case 2:
                if (phT > 3f) { Debug.Log("FFDEMO roadster49 done loop t=" + t.ToString("0")); Next(3); }
                break;
        }
        CamLoop();
    }

    // ---------------- cameras ----------------
    void CamChase(int mode)
    {
        Vector3 c = car.transform.position;
        Vector3 fw = car.Velocity; fw.y = 0f; if (fw.magnitude < 1f) { fw = car.transform.forward; fw.y = 0f; } fw.Normalize();
        Vector3 rt = new Vector3(fw.z, 0f, -fw.x);
        Vector3 p, look;
        if (mode == 0) { p = c - fw * 9f - rt * 4f + Vector3.up * 3f; look = c + fw * 4f + Vector3.up * 0.8f; }
        else { p = c - fw * 6f - rt * 15f + Vector3.up * 5f; look = c + fw * 2f + Vector3.up * 1f; }
        Ease(p, look, mode == 0 ? 4f : 2f);
    }
    void CamRamp()
    {
        Vector3 o = CRamp49.Origin;
        Vector3 c = car.transform.position;
        Vector3 p = o - CRamp49.Right * 24f + Vector3.up * 7f - CRamp49.Fwd * 4f;
        Vector3 look = Vector3.Lerp(o + Vector3.up * 6f - CRamp49.Fwd * 3f, c, 0.4f);
        Ease(p, look, 2f);
    }
    void CamLoop()
    {
        Vector3 c = car.transform.position;
        Vector3 lc = new Vector3(Layout.LoopC.x, RallyTrack.LinkJoin.y + 7f, Layout.LoopC.y + Layout.LoopShift * 0.5f);
        Vector3 p, look;
        if ((c - lc).magnitude < 20f) { p = new Vector3(lc.x - 3f, lc.y + 1f, Layout.LoopC.y - 22f); look = Vector3.Lerp(lc, c, 0.5f); }
        else { Vector3 fw = car.transform.forward; fw.y = 0f; fw.Normalize(); Vector3 rt = new Vector3(fw.z, 0f, -fw.x); p = c - fw * 9f - rt * 5f + Vector3.up * 3.5f; look = c + fw * 3f; }
        Ease(p, look, 2f);
    }
    void Ease(Vector3 p, Vector3 look, float ease)
    {
        if (!camInit) { camPos = p; camLook = look; camInit = true; }
        float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * ease);
        camPos = Vector3.Lerp(camPos, p, k); camLook = Vector3.Lerp(camLook, look, k);
    }

    void LateUpdate()
    {
        if (shot.Length == 0 || !camInit || Game.I == null || Game.I.slots.Count == 0 || ph == 0) return;
        Camera c = Game.I.slots[0].cam;
        if (c == null) return;
        c.transform.position = camPos;
        c.transform.LookAt(camLook);
    }
}
