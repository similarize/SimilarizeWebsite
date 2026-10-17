using System.Collections.Generic;
using UnityEngine;

// ffu58 demo / probe hook (Core/Behave58.cs). Only with ?ffdemo=1&ffshot=behave58 [&ffr58=<phases>] (+ &behave=old = before).
// Wall-clock phases (short, one thing each, for the slow SwiftShader sandbox):
//   scale   C8 scale table (SCALE58 log) + James in the Cybertruck seat       lie      Tigy lies down (legs folded under)
//   follow  Spotty + Germy come along house -> ranch -> Mars (spacing log)    truck    the pets hop in the Cybertruck's back seats
//   swim    James swims in the pond: Germy paddles, Spotty waits on shore     ride     James rides Daisy (ride pose)
//   park    a parked Cybertruck on a slope: drift km/h over 12 s              tracks   the Cybertruck drives over the lawn (tyre marks)
//   robot   Unitree called from far (arrival time), PLAY pets (toast) + Ride a cow (pose)
//   fort    the tree fort finished, Unitree climbs the rope ladder            mech     mech foot prints, close camera
//   boat    the Cybertruck drives into the pond -> Cyberboat hull + floats (C10)
// Logs "FFDEMO behave58 ph <n> <name> ..." and "FFDEMO behave58 done".
[DefaultExecutionOrder(100)]
public class Ffu58 : MonoBehaviour
{
    public static Ffu58 I;
    bool started, allDone, houseKick; int ph = -1, step; float phW, logT, stepT;
    Vector3 camPos, camLook; bool camOn; Transform camFollow; Vector3 camOff;
    Frog f; GroundVehicle ct; Vector3 parkP; float robotSendT;
    string[] order = { "scale", "lie", "ride", "follow", "truck", "swim", "park", "tracks", "robot", "fort", "mech", "boat" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        if (Ffu33.Url("ffdemo") == null || Ffu33.Url("ffshot") != "behave58") return;
        I = new GameObject("ffu58").AddComponent<Ffu58>();
        string o = Ffu33.Url("ffr58");
        if (!string.IsNullOrEmpty(o)) I.order = o.Split(',');
        DontDestroyOnLoad(I.gameObject);
    }

    static string Stats()
    {
        return "bedRides " + Behave58.BedRides + " hops " + Behave58.BedHops + " swims " + Behave58.Swims + " shore " + Behave58.ShoreWaits + " carWatch " + Behave58.CarWatches + " robotWaves " + Behave58.RobotWaves + " arrivals " + Animals57.Arrivals + " robot pets " + Animals57.RobotPets + " robot rides " + Animals57.RobotRides + " tyre " + Tracks56.TyreMarks + " mech prints " + Tracks56.MechPrints;
    }
    void Next(string what) { ph++; step = 0; phW = Time.realtimeSinceStartup; camOn = false; camFollow = null; Animals57.DemoSteer = null; Debug.Log("FFDEMO behave58 ph " + ph + " " + (ph < order.Length ? order[ph] : "end") + " " + what + (Behave58.On ? " (ffu58 behave)" : " (OLD behave)") + " " + Stats()); }
    void Cam(Vector3 p, Vector3 look) { camPos = p; camLook = look; camOn = true; camFollow = null; }
    void CamOn(Transform t, Vector3 off, float lookUp) { camFollow = t; camOff = off; camLook = Vector3.up * lookUp; camOn = true; }

    static readonly Dictionary<string, Animal> petCache = new Dictionary<string, Animal>(); static float petFindT;
    static Animal Pet(string n)
    {
        foreach (var a in Animal.All) if (a != null && a.petName == n) return a;
        Animal c;
        if (petCache.TryGetValue(n, out c) && c != null) return c;
        if (Time.unscaledTime < petFindT) return null;
        petFindT = Time.unscaledTime + 1f;
        foreach (var a in Resources.FindObjectsOfTypeAll<Animal>())
            if (a != null && a.gameObject.scene.IsValid() && a.petName.Length > 0) petCache[a.petName] = a;
        return petCache.TryGetValue(n, out c) ? c : null;
    }
    static Vector3 Beside(Animal a, float d) { return a.transform.position - a.transform.forward * 0.2f + a.transform.right * d; }
    static Vector3 G(float x, float z) { return new Vector3(x, Ranch.GY(x, z), z); }
    void Park(Vehicle v, Vector3 spot, float yaw)
    {
        Vector3 p = G(spot.x, spot.z) + Vector3.up * 0.6f; Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        v.rb.isKinematic = false; v.rb.velocity = Vector3.zero; v.rb.angularVelocity = Vector3.zero;
        v.rb.position = p; v.rb.rotation = q; v.transform.SetPositionAndRotation(p, q);
    }
    // a sloped lawn spot (4-9 deg) away from the house, for the parked-truck drift test; returns the downhill yaw too
    Vector3 SlopeSpot(out float yaw)
    {
        Vector3 best = new Vector3(30f, 0f, -30f); float bs = 0f; yaw = 0f;
        for (float x = -120f; x <= 120f; x += 8f)
            for (float z = -120f; z <= 120f; z += 8f)
            {
                if (Layout.PondQ(x, z) < 1.4f || Layout.RoadDist(x, z) < 8f) continue;
                Vector2 hc = Layout.HouseC; if (Mathf.Abs(x - hc.x) < 40f && Mathf.Abs(z - hc.y) < 35f) continue;
                float gx = (Ranch.GY(x + 2f, z) - Ranch.GY(x - 2f, z)) / 4f, gz = (Ranch.GY(x, z + 2f) - Ranch.GY(x, z - 2f)) / 4f;
                float sl = Mathf.Sqrt(gx * gx + gz * gz);
                float deg = Mathf.Atan(sl) * Mathf.Rad2Deg;
                if (deg > 9f || deg < 2f) continue;
                if (sl > bs) { bs = sl; best = new Vector3(x, 0f, z); yaw = Mathf.Atan2(-gx, -gz) * Mathf.Rad2Deg; }
            }
        best.y = Ranch.GY(best.x, best.z);
        Debug.Log("FFDEMO behave58 slope spot " + best.ToString("0") + " slope " + (Mathf.Atan(bs) * Mathf.Rad2Deg).ToString("0.0") + " deg, nose downhill yaw " + yaw.ToString("0"));
        return best;
    }

    void Update()
    {
        if (Game.I == null || Game.I.state != Game.State.Play || Game.I.slots.Count == 0) return;
        if (!started)
        {
            if (Realism.On && !Realism.Ready) return;
            if (Terrain56.On && !Terrain56.Ready) return;
            if (Pet("Spotty") == null)
            {
                if (!houseKick && HouseWorld.I != null) { houseKick = true; var f0 = Game.I.frogs[Game.I.slots[0].frog]; HouseWorld.I.Enter(f0); Debug.Log("FFDEMO behave58 entering the house to build it"); }
                return;
            }
            started = true;
            f = Game.I.frogs[Game.I.slots[0].frog];
            float best = 1e9f;
            foreach (var v in Vehicle.All)
            {
                var gv = v as GroundVehicle;
                if (gv == null || !gv.Title.Contains("Cybertruck") || gv.transform.Find("Sprung") == null && !HeroCars52.Old) continue;
                float d = gv.Title == "Cybertruck" ? 0f : 1f;
                if (d < best) { best = d; ct = gv; }
            }
            if (RanchLife.I != null) { Robot.DtCap = 0.25f; Robot.IdleChoreAfter = 9999f; }
            Debug.Log("FFDEMO behave58 start order " + string.Join(",", order) + " truck " + (ct != null ? ct.Title : "none"));
            Next("start");
            return;
        }
        if (ph >= order.Length) { if (!allDone) { allDone = true; Debug.Log("FFDEMO behave58 done " + Stats()); } return; }
        float w = Time.realtimeSinceStartup - phW;
        Animal spotty = Pet("Spotty"), germy = Pet("Germy"), daisy = Pet("Daisy"), tigy = Pet("Tigy");
        bool logNow = Time.unscaledTime > logT; if (logNow) logT = Time.unscaledTime + 3f;
        switch (order[ph])
        {
            case "scale":
                if (step == 0) { Behave58.ScaleReport(); step = 1; stepT = w; }
                if (step == 1 && ct != null && w - stepT > 1f)
                {
                    Vector2 hc = Layout.HouseC; float z1 = hc.y + Layout.HouseSize.y * 0.5f;
                    Park(ct, new Vector3(hc.x + 10f, 0f, z1 + 20f), 90f);
                    f.SendTo(WorldId.Ranch, G(hc.x + 8f, z1 + 18f) + Vector3.up * 0.3f, 90f);
                    step = 2; stepT = w;
                }
                if (step == 2 && w - stepT > 1.5f) { f.EnterVehicle(ct); step = 3; stepT = w; }
                if (step == 3 && w - stepT > 2f)
                {
                    Vector3 sz; float fh = Behave58.Height(f.gameObject, out sz);
                    float top = ct.transform.InverseTransformPoint(f.transform.position).y + fh;
                    Debug.Log("FFDEMO behave58 scale James in " + ct.Title + " seat: scale " + f.transform.localScale.x.ToString("0.00") + " frog top y " + top.ToString("0.00") + " (roof inside ~1.70)");
                    step = 4;
                }
                if (ct != null && step >= 3) CamOn(ct.transform, new Vector3(-3.2f, 1.9f, 0.6f), 1.2f);
                if (w > 12f) { if (f.vehicle != null) f.ExitVehicle(); Next("scale done"); }
                break;
            case "lie":
                if (tigy == null) { Next("lie skipped"); break; }
                if (step == 0) { f.SendTo(WorldId.House, Beside(tigy, 2.5f) + Vector3.up * 0.2f, 0f); tigy.holdPosition = true; step = 1; stepT = w; }
                if (step == 1 && w - stepT > 1.5f) { tigy.DemoLie58(); step = 2; }
                CamOn(tigy.transform, new Vector3(1.0f, 0.6f, 1.0f), 0.15f);
                if (w > 22f) { tigy.holdPosition = false; Next("lie done"); }
                break;
            case "ride":
                if (daisy == null || Animals57.I == null) { Next("ride skipped"); break; }
                if (step == 0) { f.SendTo(WorldId.House, Beside(daisy, 1.0f) + Vector3.up * 0.2f, 0f); stepT = w; step = 1; }
                if (step == 1 && w - stepT > 1.5f) { Animals57.I.Act(f, daisy, "ride"); step = 2; stepT = w; }
                if (step == 2) { Animals57.DemoSteer = new Vector2(0.3f, 0.7f); CamOn(daisy.transform, new Vector3(1.4f, 0.8f, 0.2f), 0.35f); }
                if (step == 2 && w - stepT > 8f) { Animals57.DemoSteer = null; if (f.rideAnimal != null) { var s = f.rideSt; f.EndAnimalRide(true); if (s != null) Animals57.I.EndRide(s); } step = 3; stepT = w; }
                if (step == 3 && w - stepT > 1.5f) Next("ride done");
                break;
            case "follow":
                if (spotty == null || germy == null || Animals57.I == null) { Next("follow skipped"); break; }
                if (step == 0) { f.SendTo(WorldId.House, Beside(spotty, 1.0f) + Vector3.up * 0.2f, 0f); stepT = w; step = 1; }
                if (step == 1 && w - stepT > 1.5f) { Animals57.I.Act(f, spotty, "come"); Animals57.I.Act(f, germy, "come"); step = 2; stepT = w; }
                if (step == 2 && w - stepT > 3f) { Vector2 hc = Layout.HouseC; float z1 = hc.y + Layout.HouseSize.y * 0.5f; f.SendTo(WorldId.Ranch, G(hc.x + 6f, z1 + 12f) + Vector3.up * 0.3f, 0f); step = 3; stepT = w; }
                if (step == 3 && w - stepT > 8f) { Gap("ranch", spotty, germy); f.SendTo(WorldId.Mars, SurfaceWorlds.M(4f, SurfaceWorlds.MarsY(4f, 60f) + 0.3f, 60f), 0f); step = 4; stepT = w; }
                if (step == 4 && w - stepT > 1.2f) { Gap("Mars arrival", spotty, germy); step = 5; }
                if (step >= 3) Cam(f.transform.position + new Vector3(2.5f, 2.2f, -5f), f.transform.position);
                if (step == 5 && w - stepT > 16f) { Gap("Mars after 16 s", spotty, germy); Vector2 hc = Layout.HouseC; float z1 = hc.y + Layout.HouseSize.y * 0.5f; f.SendTo(WorldId.Ranch, G(hc.x + 6f, z1 + 14f) + Vector3.up * 0.3f, 0f); Next("follow done"); }
                break;
            case "truck":
                if (ct == null || spotty == null || germy == null) { Next("truck skipped"); break; }
                if (step == 0) { Vector3 p = f.transform.position; Park(ct, p + new Vector3(4f, 0f, 3f), 0f); stepT = w; step = 1; }
                if (step == 1 && w - stepT > 2f) { f.EnterVehicle(ct); step = 2; stepT = w; }
                if (step == 2 && w - stepT > 2f) { step = 3; stepT = w; }
                if (step == 3) { if (!ct.rb.isKinematic) { Vector3 vv = ct.transform.forward * 5f; vv.y = ct.rb.velocity.y; ct.rb.velocity = vv; } CamOn(ct.transform, new Vector3(-2.8f, 2.2f, -1.6f), 1.1f); }
                if (step == 3 && w - stepT > 7f) { ct.rb.velocity = Vector3.zero; Debug.Log("FFDEMO behave58 truck pets: Spotty parent " + (spotty.transform.parent != null ? spotty.transform.parent.name : "-") + " Germy parent " + (germy.transform.parent != null ? germy.transform.parent.name : "-") + " " + Stats()); step = 4; stepT = w; }
                if (step == 4 && w - stepT > 1.5f) { f.ExitVehicle(); step = 5; stepT = w; }
                if (step == 5) Cam(f.transform.position + new Vector3(3f, 2.2f, -4f), f.transform.position);
                if (step == 5 && w - stepT > 3f) { Gap("out of the truck", spotty, germy); Next("truck done"); }
                break;
            case "swim":
                if (spotty == null || germy == null) { Next("swim skipped"); break; }
                {
                    Vector2 pc = Layout.PondC;
                    Vector3 water = new Vector3(pc.x + Layout.PondR.x * 0.55f, Layout.WaterY - 0.2f, pc.y);
                    if (step == 0) { f.SendTo(WorldId.Ranch, water + new Vector3(Layout.PondR.x * 0.6f, 0f, 0f) + Vector3.up * 1.5f, 270f); step = 1; stepT = w; }
                    if (step == 1 && w - stepT > 4f) { f.SendTo(WorldId.Ranch, water, 270f); step = 2; stepT = w; }
                    Cam(water + new Vector3(6f, 3.5f, -7f), water + new Vector3(3f, 0f, 0f));
                    if (logNow && step == 2) Debug.Log("FFDEMO behave58 swim James " + f.transform.position.ToString("0.0") + " Germy " + germy.transform.position.ToString("0.0") + " Spotty " + spotty.transform.position.ToString("0.0") + " " + Stats());
                    if (w > 26f) { Animals57.I.Act(f, spotty, "come"); Animals57.I.Act(f, germy, "come"); f.SendTo(WorldId.Ranch, water + new Vector3(Layout.PondR.x * 0.7f, 0f, 0f) + Vector3.up * 1.5f, 270f); Next("swim done (pets told to stay)"); }
                    break;
                }
            case "park":
                if (ct == null) { Next("park skipped"); break; }
                if (step == 0) { if (f.vehicle != null) f.ExitVehicle(); float yaw; parkP = SlopeSpot(out yaw); Park(ct, parkP, yaw); f.SendTo(WorldId.Ranch, parkP + new Vector3(6f, 0.5f, 0f), 270f); step = 1; stepT = w; }
                if (step == 1 && w - stepT > 3f) { parkP = ct.rb.position; step = 2; stepT = w; }
                Cam(parkP + new Vector3(7f, 3f, -6f), parkP);
                if (step == 2 && w - stepT > 12f)
                {
                    float dd = (ct.rb.position - parkP).magnitude, tt = w - stepT;
                    Debug.Log("FFDEMO behave58 park drift " + dd.ToString("0.000") + " m in " + tt.ToString("0.0") + " s = " + (dd / tt * 3.6f).ToString("0.00") + " km/h (frame dt " + Time.deltaTime.ToString("0.00") + ")");
                    Next("park done");
                }
                break;
            case "tracks":
                if (ct == null) { Next("tracks skipped"); break; }
                if (step == 0) { Vector3 p = parkP.sqrMagnitude > 1f ? parkP : new Vector3(30f, 0f, -30f); Park(ct, p, 0f); f.SendTo(WorldId.Ranch, p + new Vector3(4f, 0.5f, 0f), 0f); stepT = w; step = 1; }
                if (step == 1 && w - stepT > 1.5f) { f.EnterVehicle(ct); step = 2; stepT = w; }
                if (step == 2 && w - stepT < 14f) { if (!ct.rb.isKinematic) { Vector3 vv = Quaternion.Euler(0f, (w - stepT) * 9f, 0f) * Vector3.forward * 6f; vv.y = ct.rb.velocity.y; ct.rb.velocity = vv; } }
                if (step == 2 && w - stepT >= 14f) { ct.rb.velocity = Vector3.zero; f.ExitVehicle(); step = 3; stepT = w; }
                if (step == 2) Cam(ct.transform.position + new Vector3(-6f, 6f, -8f), ct.transform.position + new Vector3(0f, 0f, -4f));
                if (step == 3) Cam(ct.transform.position + new Vector3(-3f, 4.5f, -9f), ct.transform.position + new Vector3(0f, 0f, -6f));   // look back along the tyre marks
                if (logNow) Debug.Log("FFDEMO behave58 tracks tyre " + Tracks56.TyreMarks + " truck " + ct.transform.position.ToString("0.0") + " v " + ct.Speed.ToString("0.0"));
                if (step == 3 && w - stepT > 12f) Next("tracks done (tyre " + Tracks56.TyreMarks + ")");
                break;
            case "robot":
                if (RanchLife.I == null || RanchLife.I.robots.Count < 2 || RobotPhone.I == null) { Next("robot skipped (no robots)"); break; }
                {
                    var r = RanchLife.I.robots[1];
                    if (step == 0)
                    {
                        if (f.vehicle != null) f.ExitVehicle();
                        Animal cow = null; foreach (var a in Animal.All) if (a != null && a.kind == Animal.Kind.Cow) { cow = a; break; }
                        if (cow != null) { Vector3 c = cow.transform.position; f.SendTo(WorldId.Ranch, G(c.x + 6f, c.z) + Vector3.up * 0.3f, 270f); }
                        step = -1; stepT = w; break;
                    }
                    if (step == -1 && w - stepT > 2f) step = -2;
                    if (step == -2) { r.battery = 1f; robotSendT = w; RobotPhone.I.DemoOpen(f, 1, "play:pets"); RobotPhone.I.DemoSend(); RobotPhone.I.open = false; Debug.Log("FFDEMO behave58 phone play:pets -> '" + (f.toast ?? "") + "' robot at " + (r.transform.position - f.transform.position).magnitude.ToString("0") + " m"); step = 1; stepT = w; }
                    if (step < 0) break;
                    if (step == 1 && r.visit != null && r.visit.ph == 1) { Debug.Log("FFDEMO behave58 robot arrived after " + (w - robotSendT).ToString("0.0") + " s wall (" + r.visit.total.ToString("0.0") + " s game)"); step = 2; stepT = w; }
                    if (step == 1 && w - stepT > 70f) { step = 2; stepT = w; Debug.Log("FFDEMO behave58 robot NOT arrived in 70 s"); }
                    if (step == 2 && (w - stepT > 60f || r.visit != null && r.visit.verb != "pets" && w - stepT > 8f)) { RobotPhone.I.DemoOpen(f, 1, "play:ridepet"); RobotPhone.I.DemoSend(); RobotPhone.I.open = false; Debug.Log("FFDEMO behave58 phone play:ridepet -> '" + (f.toast ?? "") + "'"); step = 3; stepT = w; }
                    CamOn(r.transform, step >= 3 ? new Vector3(2.6f, 1.6f, 0.4f) : new Vector3(3.5f, 2.4f, -4f), step >= 3 ? 1.5f : 1f);
                    if (logNow) Debug.Log("FFDEMO behave58 robot " + r.robotName + " world " + r.world + " visit " + (r.visit == null ? "-" : r.visit.verb + "/" + r.visit.vs + "/" + r.visit.line) + " pos " + r.transform.position.ToString("0.0") + " " + Stats());
                    if (step == 3 && w - stepT > 45f) Next("robot done");
                }
                break;
            case "fort":
                if (RanchLife.I == null || RanchLife.I.robots.Count < 2 || RobotPhone.I == null || Fort56.Total == 0) { Next("fort skipped"); break; }
                {
                    var r = RanchLife.I.robots[1];
                    if (step == 0)
                    {
                        if (!Fort56.Done) Fort56.AddPlanks(Fort56.Total - Fort56.Built, r.robotName);
                        f.SendTo(WorldId.Ranch, Fort56.Ladder + new Vector3(2.5f, 0.4f, -2.5f), 0f);
                        r.transform.position = Fort56.Ladder + new Vector3(-1.5f, 0f, -2f);   // demo: skip the long walk over (slow sandbox)
                        step = 1; stepT = w;
                    }
                    if (step == 1 && w - stepT > 2f) { RobotPhone.I.DemoOpen(f, 1, "play:fort"); RobotPhone.I.DemoSend(); RobotPhone.I.open = false; Debug.Log("FFDEMO behave58 phone play:fort -> '" + (f.toast ?? "") + "' fort " + Fort56.Built + " / " + Fort56.Total); step = 2; stepT = w; }
                    Cam(Fort56.Ladder + new Vector3(4.5f, 2.5f, -6f), Fort56.Ladder + Vector3.up * 2.5f);
                    if (logNow) Debug.Log("FFDEMO behave58 fort robot " + r.transform.position.ToString("0.0") + " visit " + (r.visit == null ? "-" : r.visit.verb + "/" + r.visit.vs) + " deck y " + Fort56.DeckY.ToString("0.0"));
                    if (w > 90f) Next("fort done");
                }
                break;
            case "mech":
                {
                    Vector2 hc = Layout.HouseC; float z1 = hc.y + Layout.HouseSize.y * 0.5f;
                    Vector3 o = new Vector3(hc.x + 18f, 0f, z1 + 24f);
                    float gy = Ranch.GY(o.x, o.z);
                    if (step == 0) { f.SendTo(WorldId.Ranch, new Vector3(o.x - 6f, gy + 0.3f, o.z - 6f), 45f); step = 1; stepT = w; }
                    if (step >= 1 && step < 9 && w - stepT > 0.6f) { float x = o.x + step * 2.2f, z = o.z + (step % 2 == 0 ? 0.9f : -0.9f); MechStomp.Foot(new Vector3(x, Ranch.GY(x, z), z), 3.3f, 1f, false); step++; stepT = w; }
                    Cam(o + new Vector3(6f, gy + 4.5f, -5f), o + new Vector3(9f, gy, 0f));
                    if (w > 22f) Next("mech done (" + Tracks56.MechPrints + " mech prints)");
                    break;
                }
            case "boat":
                if (ct == null) { Next("boat skipped"); break; }
                {
                    Vector2 pc = Layout.PondC;
                    Vector3 shore = new Vector3(pc.x - Layout.PondR.x * 0.72f, 0f, pc.y + Layout.PondR.y * 0.3f);   // already in deep water: floats up + transforms
                    if (step == 0) { if (f.vehicle != null) f.ExitVehicle(); Park(ct, shore, 90f); f.SendTo(WorldId.Ranch, G(shore.x - 3f, shore.z + 3f) + Vector3.up * 0.3f, 90f); step = 1; stepT = w; }
                    if (step == 1 && w - stepT > 1.5f) { f.EnterVehicle(ct); step = 2; stepT = w; }
                    if (step == 2 && w - stepT > 6f && w - stepT < 16f && !ct.rb.isKinematic) { Vector3 vv = ct.transform.forward * 4f; vv.y = ct.rb.velocity.y; ct.rb.velocity = vv; }
                    if (step == 2 && w - stepT >= 16f) { step = 3; stepT = w; }
                    if (step >= 2) CamOn(ct.transform, new Vector3(-5.5f, 1.6f, 2.5f), 0.4f);
                    if (logNow) Debug.Log("FFDEMO behave58 boat truck " + ct.transform.position.ToString("0.0") + " wet " + (ct.GetComponent<CyberBoat>() != null ? ct.GetComponent<CyberBoat>().Wet.ToString("0.00") : "-"));
                    if (step == 3 && w - stepT > 14f) Next("boat done");
                    break;
                }
            default: Next("unknown phase"); break;
        }
    }

    void Gap(string where, Animal a, Animal b)
    {
        Vector3 j = f.transform.position;
        Debug.Log("FFDEMO behave58 gap " + where + ": James-Spotty " + Flat(a.transform.position - j).ToString("0.0") + " James-Germy " + Flat(b.transform.position - j).ToString("0.0") + " Spotty-Germy " + Flat(a.transform.position - b.transform.position).ToString("0.0") + " " + Stats());
    }
    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

    void LateUpdate()
    {
        if (!camOn || ph < 0 || ph >= order.Length || Game.I == null || Game.I.slots.Count == 0) return;
        Camera c = Game.I.slots[0].cam;
        if (c == null) return;
        if (camFollow != null)
        {
            Vector3 p = camFollow.position;
            c.transform.position = p + camFollow.rotation * camOff;
            c.transform.LookAt(p + camLook);
            return;
        }
        c.transform.position = camPos;
        c.transform.LookAt(camLook);
    }
}
