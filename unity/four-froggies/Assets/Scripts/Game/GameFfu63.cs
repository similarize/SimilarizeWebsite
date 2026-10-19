using UnityEngine;

// ffu63 robot wall / perimeter demo for the probe (?ffdemo=1&ffshot=wall63; &rc63=0 = old behaviour for a before shot).
//   0 car       a robot walks straight at a parked ranch vehicle to a spot right behind it: must go AROUND (no clip) and arrive
//   1 edge      a robot walks toward x = 240 (outside the ranch): must stop at the froggy perimeter (|x| <= Half - 4)
//   2 house     in the house world a robot walks at a spot behind an inner wall: must never pass through the wall
// Clip detector: line from last frame's position to this frame's at knee / chest height against solid colliders.
// Logs "WALL63 PASS/FAIL ...", "WALL63 RESULT n/m", "FFDEMO wall63 ph N <name>", "FFDEMO wall63 done".
[DefaultExecutionOrder(103)]
public class Ffu63 : MonoBehaviour
{
    public static Ffu63 I;
    bool on; float t0 = -1f, phT, logT; int ph = -1, pass, total; bool done;
    Frog f; Robot r; Vector3 prev; int clips; float minD = 1e9f, maxX = -1e9f, reachT = -1f; string clipWhat = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        I = new GameObject("ffu63").AddComponent<Ffu63>();
        DontDestroyOnLoad(I.gameObject);
    }

    void Update()
    {
        if (!on) { on = Ffu33.Url("ffdemo") != null && Ffu33.Url("ffshot") == "wall63"; if (!on) { enabled = false; return; } }
        if (done || Game.I == null || Game.I.state != Game.State.Play || Game.I.slots.Count == 0 || RanchLife.I == null || RanchLife.I.robots.Count == 0) return;
        if (t0 < 0f)
        {
            t0 = Time.realtimeSinceStartup; f = Game.I.frogs[Game.I.slots[0].frog];
            Robot.DtCap = 0.3f;
            r = RanchLife.I.robots[0];
            foreach (var x in RanchLife.I.robots) if (x.robotName != null && x.robotName.StartsWith("Unitree")) r = x;
            if (f.vehicle != null) f.ExitVehicle();
            Debug.Log("FFDEMO wall63 start p1 " + f.nick + " robot " + r.robotName + " c63 " + Robot.C63On);
        }
        phT += Time.unscaledDeltaTime;
        try { Step(); } catch (System.Exception e) { Debug.LogError("FFDEMO wall63 error " + e); Done(); }
    }

    void Next(int p, string name) { ph = p; phT = 0f; clips = 0; clipWhat = ""; minD = 1e9f; maxX = -1e9f; reachT = -1f; prev = r.transform.position; Debug.Log("FFDEMO wall63 ph " + p + " " + name + " t " + (Time.realtimeSinceStartup - t0).ToString("0.0")); }
    void Check(string what, bool ok, string detail) { total++; if (ok) pass++; Debug.Log("WALL63 " + (ok ? "PASS" : "FAIL") + " " + what + " | " + detail); }
    void Done() { if (done) return; done = true; Robot.DtCap = 0.05f; Debug.Log("WALL63 RESULT " + pass + "/" + total); Debug.Log("FFDEMO wall63 done"); }

    void Track()
    {
        Vector3 c = r.transform.position;
        float h = Mathf.Min(0.9f, r.height * 0.45f);
        RaycastHit hit;
        if ((c - prev).sqrMagnitude > 1e-4f && Physics.Linecast(prev + Vector3.up * h, c + Vector3.up * h, out hit, Robot.C63Mask, QueryTriggerInteraction.Ignore)
            && !hit.collider.transform.IsChildOf(r.transform) && Mathf.Abs(hit.normal.y) < 0.55f)
        { clips++; if (clipWhat.Length < 80) clipWhat += hit.collider.name + " "; }
        if ((c - prev).magnitude > 3f) { clips++; clipWhat += "JUMP" + (c - prev).magnitude.ToString("0.0") + " "; }
        prev = c;
        float d = r.Demo63Dist; minD = Mathf.Min(minD, d); maxX = Mathf.Max(maxX, c.x - (r.world == WorldId.Ranch ? 0f : 0f));
        if (reachT < 0f && d < 2.5f) reachT = phT;
        logT -= Time.unscaledDeltaTime;
        if (logT <= 0f) { logT = 5f; Debug.Log("WALL63 ph " + ph + " pos " + c.ToString("F1") + " d " + d.ToString("0.0") + " clips " + clips + " blocks " + r.c63Blocks + " status " + r.status); }
    }

    Vehicle car; Vector3 carC, carSide; float carExt;
    void Step()
    {
        switch (ph)
        {
            case -1:
                {
                    float bestS = -1f;
                    foreach (var v in Vehicle.All)
                    {
                        if (v == null || !v.isActiveAndEnabled || v.driver != null) continue;
                        Vector3 p = v.transform.position;
                        if (Mathf.Abs(p.x) > 150f || Mathf.Abs(p.z) > 150f || Mathf.Abs(p.y - RobotNav.Floor(p.x, p.z)) > 2f || RobotNav.InGarage(p)) continue;
                        Bounds b = new Bounds(p, Vector3.zero); bool any = false;
                        foreach (var col in v.GetComponentsInChildren<Collider>()) { if (col.isTrigger) continue; if (!any) { b = col.bounds; any = true; } else b.Encapsulate(col.bounds); }
                        if (!any) continue;
                        float s = Mathf.Min(b.size.x, b.size.z);
                        if (s > bestS && s < 6f && b.size.y > 1.0f) { bestS = s; car = v; carC = b.center; carExt = Mathf.Max(b.extents.x, b.extents.z); }
                    }
                    if (car == null) { Check("car found", false, "no parked vehicle"); Next(1, "edge"); StartEdge(); break; }
                    carSide = car.transform.right; carSide.y = 0f; carSide.Normalize();
                    Vector3 from = carC - carSide * (carExt + 6f), to = carC + carSide * (carExt + 2.2f);
                    r.Demo63Go(WorldId.Ranch, from, to);
                    Vector3 fp = carC - car.transform.forward * (carExt + 9f);
                    f.SendTo(WorldId.Ranch, RobotNav.G(fp.x, fp.z) + Vector3.up * 0.3f, Mathf.Atan2(carC.x - fp.x, carC.z - fp.z) * Mathf.Rad2Deg);
                    Debug.Log("WALL63 car " + car.Title + " at " + carC.ToString("F1") + " ext " + carExt.ToString("0.0") + " robot " + from.ToString("F1") + " -> " + to.ToString("F1"));
                    Next(0, "car");
                    break;
                }
            case 0:
                Track();
                if (reachT >= 0f && phT > reachT + 4f || phT > 110f)
                {
                    Check("around the car", clips == 0 && reachT >= 0f, "clips " + clips + " " + clipWhat + "reached " + (reachT >= 0f ? reachT.ToString("0") + " s" : "no") + " minD " + minD.ToString("0.0") + " blocks " + r.c63Blocks);
                    Next(1, "edge"); StartEdge();
                }
                break;
            case 1:
                Track();
                if (phT > 60f)
                {
                    Check("perimeter", maxX <= Layout.Half - 4f + 0.15f && clips == 0, "max x " + maxX.ToString("0.00") + " limit " + (Layout.Half - 4f) + " clips " + clips + " " + clipWhat);
                    Next(2, "house"); StartHouse();
                }
                break;
            case 2:
                Track();
                if (phT > 70f)
                {
                    Check("house wall", clips == 0, "clips " + clips + " " + clipWhat + "minD " + minD.ToString("0.0") + " blocks " + r.c63Blocks + (reachT >= 0f ? " reached (round)" : " held at the wall"));
                    f.SendTo(WorldId.Ranch, RobotNav.G(-40f, 30f) + Vector3.up * 0.3f, 0f);
                    Done();
                }
                break;
        }
    }

    void StartEdge()
    {
        float x0 = Layout.Half - 20f, z = -20f;
        r.Demo63Go(WorldId.Ranch, new Vector3(x0, 0f, z), new Vector3(Layout.Half + 40f, 0f, z));
        f.SendTo(WorldId.Ranch, RobotNav.G(x0 - 6f, z - 8f) + Vector3.up * 0.3f, 60f);
        prev = r.transform.position;
    }

    void StartHouse()
    {
        Vector3 s = HouseWorld.Spawn(0);
        Vector3 best = Vector3.zero; float bd = 0f;
        for (int i = 0; i < 16; i++)
        {
            Vector3 d = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward; RaycastHit h;
            if (Physics.Raycast(s + Vector3.up * 1f, d, out h, 12f, Robot.C63Mask, QueryTriggerInteraction.Ignore) && Mathf.Abs(h.normal.y) < 0.3f && h.distance > 2f && h.distance > bd)
            { bd = h.distance; best = h.point + d * 2.5f; best.y = s.y; }
        }
        if (bd <= 0f) { Check("house wall found", false, "no wall within 12 m of spawn"); Done(); return; }
        r.Demo63Go(WorldId.House, s, best);
        f.SendTo(WorldId.House, HouseWorld.Spawn(2), Mathf.Atan2(best.x - s.x, best.z - s.z) * Mathf.Rad2Deg);
        prev = r.transform.position;
        Debug.Log("WALL63 house robot " + s.ToString("F1") + " -> behind the wall " + best.ToString("F1") + " wall at " + bd.ToString("0.0") + " m");
    }
}
