using UnityEngine;

// ffu56 demo / probe hook (Core/Terrain56.cs, Grass56.cs, Tracks56.cs, World/Forest56.cs). Only with ?ffdemo=1&ffshot=terrain56
// [&ffr56=<phases>] (+ &terrain=old for the same tour with the ffu55 look = before shots). Wall-clock phases:
//   ranch      ground + paths from above the porch      grass    close-up lawn: James + a parked Cybertruck (bend)
//   forest     the forest edge + the tree fort tree     tree     a ranch tree canopy close (leaf sprigs, bark)
//   buildings  house siding + shingles, garage, barn    tracks   James walks + Cybertruck drives through the pond-rim mud
//   fort       robot PLAY > Build the tree fort, climbs  bugs     James catches a firefly, robot PLAY > Bug hunt
// Logs "FFDEMO terrain56 ..." and "FFDEMO terrain56 done".
[DefaultExecutionOrder(100)]
public class Ffu56 : MonoBehaviour
{
    public static Ffu56 I;
    bool started, allDone; int ph = -1, step; float phW, logT, stepT;
    Vector3 camPos, camLook; bool camOn;
    Frog f; GroundVehicle ct; Vector3 lawn, rim, rimDir;
    string[] order = { "ranch", "grass", "tree", "buildings", "tracks", "forest", "fort", "bugs" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        if (Ffu33.Url("ffdemo") == null || Ffu33.Url("ffshot") != "terrain56") return;
        I = new GameObject("ffu56").AddComponent<Ffu56>();
        string o = Ffu33.Url("ffr56");
        if (!string.IsNullOrEmpty(o)) I.order = o.Split(',');
        DontDestroyOnLoad(I.gameObject);
    }

    void Next(string what) { ph++; step = 0; phW = Time.realtimeSinceStartup; camOn = false; Debug.Log("FFDEMO terrain56 ph " + ph + " " + (ph < order.Length ? order[ph] : "end") + " " + what + (Terrain56.On ? " (ffu56 terrain)" : " (OLD terrain)") + (Grass56.I != null ? " grass " + Grass56.I.Stats() : "") + " prints " + Tracks56.Prints + " (frog " + Tracks56.FrogPrints + " robot " + Tracks56.RobotPrints + " tyre " + Tracks56.TyreMarks + " animal " + Tracks56.AnimalPrints + ")"); }
    void Cam(Vector3 p, Vector3 look) { camPos = p; camLook = look; camOn = true; }
    static Vector3 G(float x, float z) { return new Vector3(x, Ranch.GY(x, z), z); }

    void Park(Vehicle v, Vector3 spot, float yaw)
    {
        Vector3 p = G(spot.x, spot.z) + Vector3.up * 0.5f; Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        v.rb.isKinematic = false; v.rb.velocity = Vector3.zero; v.rb.angularVelocity = Vector3.zero;
        v.rb.position = p; v.rb.rotation = q; v.transform.SetPositionAndRotation(p, q);
    }

    // a lawn spot with full grass, nothing reserved, no tree within 7 m, flat
    Vector3 PickLawn(Vector2 want)
    {
        Vector3 best = new Vector3(want.x, 0f, want.y); float bd = 1e9f;
        for (float x = want.x - 30f; x <= want.x + 30f; x += 3f)
            for (float z = want.y - 30f; z <= want.y + 30f; z += 3f)
            {
                var w = Terrain56.Weights(x, z);
                if (w.g < 0.9f || Layout.PondQ(x, z) < 1.3f || Layout.RoadDist(x, z) < Layout.TrackW + 4f || Terrain56.NearTree(x, z, 7f)) continue;
                if (JetLayout.Reserved(x, z) || RcTrack.Reserved(x, z) || MonsterPark.Reserved(x, z) || CarrierLayout.Reserved(x, z)) continue;
                float y0 = Ranch.GY(x, z); if (Mathf.Abs(Ranch.GY(x + 4f, z) - y0) > 0.3f || Mathf.Abs(Ranch.GY(x, z + 4f) - y0) > 0.3f) continue;
                bool hit = false;
                foreach (var col in Physics.OverlapSphere(new Vector3(x, y0 + 1.3f, z), 4f, ~0, QueryTriggerInteraction.Ignore)) if (!(col is TerrainCollider) && col.gameObject.layer != Vehicle.VehicleLayer && col.gameObject.layer != Vehicle.FrogLayer) { hit = true; break; }
                if (hit) continue;
                float d = (new Vector2(x, z) - want).sqrMagnitude; if (d < bd) { bd = d; best = new Vector3(x, y0, z); }
            }
        Debug.Log("FFDEMO terrain56 lawn spot " + best.ToString("0.0") + (bd > 1e8f ? " (none found)" : ""));
        return best;
    }

    void Update()
    {
        if (Game.I == null || Game.I.state != Game.State.Play || Game.I.slots.Count == 0) return;
        if (!started)
        {
            // wait for the streamed scans (ffu56 look) - or a fixed beat with the old look
            if (Realism.On && !Realism.Ready) return;
            if (Terrain56.On && !Terrain56.Ready) return;
            started = true;
            f = Game.I.frogs[Game.I.slots[0].frog];
            float best = 1e9f;
            foreach (var v in Vehicle.All)
            {
                var gv = v as GroundVehicle;
                if (gv == null || gv.Title != "Cybertruck") continue;
                float d = (new Vector2(v.transform.position.x, v.transform.position.z) - new Vector2(14f, 44f)).sqrMagnitude;
                if (d < best) { best = d; ct = gv; }
            }
            if (RanchLife.I != null) { Robot.DtCap = 0.25f; Robot.IdleChoreAfter = 9999f; }
            lawn = PickLawn(new Vector2(20f, -20f));
            // the pond rim's mud on the west shore (path end)
            float a = 200f * Mathf.Deg2Rad;
            rim = new Vector3(Layout.PondC.x + Layout.PondR.x * 1.12f * Mathf.Cos(a), 0f, Layout.PondC.y + Layout.PondR.y * 1.12f * Mathf.Sin(a)); rim.y = Ranch.GY(rim.x, rim.z);
            rimDir = new Vector3(-Mathf.Sin(a) * Layout.PondR.x, 0f, Mathf.Cos(a) * Layout.PondR.y).normalized;
            Debug.Log("FFDEMO terrain56 start order " + string.Join(",", order) + " terrain " + (Terrain56.On ? "ffu56" : "old") + " layers " + Terrain56.Layers + " leaf cards " + Terrain56.LeafCards + " building swaps " + Terrain56.BuildingSwaps + " fort " + Fort56.Base.ToString("0.0") + " bugs " + Bug56.All.Count + " rim " + rim.ToString("0.0") + " mud " + Terrain56.Weights(rim.x, rim.z).m.ToString("0.00"));
            Next("start");
            return;
        }
        if (ph >= order.Length) { if (!allDone) { allDone = true; Debug.Log("FFDEMO terrain56 done"); } return; }
        float w = Time.realtimeSinceStartup - phW;
        Vector2 hc = Layout.HouseC; float hz1 = hc.y + Layout.HouseSize.y * 0.5f;
        switch (order[ph])
        {
            case "ranch":
                if (step == 0) { f.SendTo(WorldId.Ranch, G(hc.x + 4f, hz1 + 14f) + Vector3.up * 0.3f, 120f); step = 1; }
                Cam(G(hc.x - 4f, hz1 + 4f) + Vector3.up * 9f, G(hc.x + 34f, hz1 + 22f));
                if (w > 22f) Next("ranch done");
                break;
            case "grass":
                if (step == 0)
                {
                    f.SendTo(WorldId.Ranch, lawn + Vector3.up * 0.3f, 90f);
                    if (ct != null) Park(ct, lawn + new Vector3(5.5f, 0f, 3f), 200f);
                    step = 1;
                }
                if (w < 22f) Cam(lawn + new Vector3(-3.2f, 0.75f, -1.6f), lawn + new Vector3(0.6f, 0.35f, 0.4f));
                else if (w < 44f && ct != null) Cam(ct.transform.position + new Vector3(-3.8f, 1.1f, -3.2f), ct.transform.position + new Vector3(0f, 0.3f, 0f));
                else Next("grass done");
                break;
            case "tree":
                {
                    if (step == 0)
                    {
                        // a decorative (canopy) tree near the yard
                        Vector2 tp = Vector2.zero; float bd = 1e9f;
                        foreach (var s in Ranch.TreeSpots) { float d = (s - new Vector2(10f, -10f)).sqrMagnitude; if (d < bd) { bd = d; tp = s; } }
                        camLook = G(tp.x, tp.y) + Vector3.up * 3.6f; step = 1;
                        Debug.Log("FFDEMO terrain56 tree at " + tp.ToString("0.0"));
                    }
                    Vector3 lk = camLook;
                    Cam(lk + new Vector3(6.5f, -1.2f, -5.5f), lk);
                    if (w > 22f) Next("tree done");
                    break;
                }
            case "buildings":
                if (w < 20f) Cam(G(hc.x + 18f, hz1 + 16f) + Vector3.up * 4.5f, new Vector3(hc.x + 6f, 4.5f, hz1));
                else if (w < 40f) Cam(G(hc.x + 30f, hz1 + 3f) + Vector3.up * 11f, new Vector3(hc.x + 10f, 8.8f, hc.y));
                else if (w < 60f && TrailerBarn.Centre.sqrMagnitude > 1f) { Vector3 tb = TrailerBarn.Centre; Cam(tb + new Vector3(16f, 5f, -18f), tb + Vector3.up * 3f); }
                else Next("buildings done");
                break;
            case "tracks":
                {
                    Vector3 side = Vector3.Cross(Vector3.up, rimDir);
                    if (step == 0)
                    {
                        f.SendTo(WorldId.Ranch, rim - rimDir * 6f + Vector3.up * 0.2f, Mathf.Atan2(rimDir.x, rimDir.z) * Mathf.Rad2Deg);
                        if (ct != null) Park(ct, rim - rimDir * 14f + side * 3.2f, Mathf.Atan2(rimDir.x, rimDir.z) * Mathf.Rad2Deg);
                        if (RanchLife.I != null && RanchLife.I.robots.Count > 1 && RobotPhone.I != null) { RanchLife.I.robots[1].battery = 1f; RobotPhone.I.DemoOpen(f, 1, "play:follow"); RobotPhone.I.DemoSend(); RobotPhone.I.open = false; }
                        step = 1; stepT = 0f;
                    }
                    if (w > 2f && w < 26f)
                    {
                        // James walks along the rim (0.4 m steps), the truck drives the same way on the outside
                        stepT += Time.deltaTime;
                        if (stepT > 0.3f) { stepT = 0f; Vector3 p = f.transform.position + rimDir * 0.4f; p.y = Ranch.GY(p.x, p.z) + 0.05f; f.SendTo(WorldId.Ranch, p, Mathf.Atan2(rimDir.x, rimDir.z) * Mathf.Rad2Deg); }
                        if (ct != null && !ct.rb.isKinematic) { Vector3 vv = rimDir * 3.5f; vv.y = ct.rb.velocity.y; ct.rb.velocity = vv; }
                    }
                    if (ct != null && w >= 26f && w < 27f) ct.rb.velocity = Vector3.zero;
                    Cam(rim + rimDir * 2f - side * 4.5f + Vector3.up * 3.2f, rim + rimDir * 2f + side * 1.2f);
                    if (Time.unscaledTime > logT) Debug.Log("FFDEMO terrain56 tracks prints " + Tracks56.Prints + " frog " + Tracks56.FrogPrints + " robot " + Tracks56.RobotPrints + " tyre " + Tracks56.TyreMarks + " James " + f.transform.position.ToString("0.0") + " mud " + Terrain56.Weights(f.transform.position.x, f.transform.position.z).m.ToString("0.00"));
                    if (w > 40f) Next("tracks done");
                    break;
                }
            case "forest":
                {
                    Vector3 fb = Fort56.Base;
                    if (step == 0) { f.SendTo(WorldId.Ranch, Fort56.Ladder + new Vector3(-1.5f, 0.2f, -3f), 20f); step = 1; }
                    Cam(fb + new Vector3(-11f, 2.4f, -12f), fb + new Vector3(0f, 2.6f, 0f));
                    if (w > 22f) Next("forest done");
                    break;
                }
            case "fort":
                {
                    if (RanchLife.I == null || RanchLife.I.robots.Count < 2 || RobotPhone.I == null) { Next("fort skipped (no robots)"); break; }
                    Robot r = RanchLife.I.robots[1];
                    Vector3 fb = Fort56.Base;
                    if (step == 0)
                    {
                        f.SendTo(WorldId.Ranch, Fort56.Ladder + new Vector3(-2f, 0.2f, -2.5f), 20f); r.battery = 1f; step = 1;
                        // demo only: start the robot near the fort (the 250 m walk from the yard takes minutes under SwiftShader)
                        if (r.world == WorldId.Ranch && r.rideOf == null) { Vector3 rp = Fort56.Ladder + new Vector3(4f, 0f, -6f); rp.y = Ranch.GY(rp.x, rp.z); r.transform.position = rp; }
                    }
                    if (step == 1 && w > 2f) { RobotPhone.I.DemoOpen(f, 1, "play:fort"); RobotPhone.I.DemoSend(); RobotPhone.I.open = false; Debug.Log("FFDEMO terrain56 phone play:fort -> '" + (f.toast ?? "") + "'"); step = 2; }
                    // James hammers too (every 4 s) once the robot is there
                    if (step == 2 && r.visit != null && r.visit.verb == "fort" && r.visit.vs >= 1 && !Fort56.Done) { stepT += Time.deltaTime; if (stepT > 4f) { stepT = 0f; f.Toast(Fort56.AddPlanks(4, f.nick), 2f); } }
                    if (step == 2 && Fort56.Done) { step = 3; phW = Time.realtimeSinceStartup; w = 0f; }
                    if (step == 3 && w > 12f) { Fort56.DemoClimb(f); step = 4; phW = Time.realtimeSinceStartup; w = 0f; }
                    if (step <= 2) Cam(fb + new Vector3(-7f, 3.2f, -8.5f), fb + new Vector3(0f, 2.2f, 0f));
                    else Cam(fb + new Vector3(-6.5f, 5.5f, -7.5f), fb + new Vector3(0f, Fort56.DeckY - fb.y + 0.8f, 0f));
                    if (Time.unscaledTime > logT) Debug.Log("FFDEMO terrain56 fort built " + Fort56.Built + " / " + Fort56.Total + " robot " + r.robotName + " visit " + (r.visit == null ? "-" : r.visit.ph + "/" + r.visit.verb + "/" + r.visit.vs + "/" + r.visit.line) + " pos " + r.transform.position.ToString("0.0") + " James y " + f.transform.position.y.ToString("0.0"));
                    if ((step == 4 && w > 16f) || w > 150f) Next("fort done");
                    break;
                }
            case "bugs":
                {
                    if (step == 0)
                    {
                        Bug56 b = Bug56.NearestFree(Fort56.Base, 200f);
                        if (b != null) f.SendTo(WorldId.Ranch, G(b.transform.position.x - 1.2f, b.transform.position.z - 1.2f) + Vector3.up * 0.2f, 45f);
                        step = 1;
                    }
                    if (step == 1 && w > 4f)
                    {
                        Bug56 b = Bug56.NearestFree(f.transform.position, 30f);
                        if (b != null) { f.Toast(Jar56.Put(f, f.transform, new Vector3(0.28f, 0.45f, 0.18f), b), 2.4f); Debug.Log("FFDEMO terrain56 James caught a " + b.kindName + ": '" + f.toast + "'"); }
                        if (RanchLife.I != null && RanchLife.I.robots.Count > 1 && RobotPhone.I != null) { RanchLife.I.robots[1].battery = 1f; RobotPhone.I.DemoOpen(f, 1, "play:bugs"); RobotPhone.I.DemoSend(); RobotPhone.I.open = false; Debug.Log("FFDEMO terrain56 phone play:bugs -> '" + (f.toast ?? "") + "'"); }
                        step = 2;
                    }
                    Vector3 fp = f.transform.position, fw = f.transform.forward;
                    Cam(fp - fw * 2.6f + Vector3.up * 1.4f + Quaternion.Euler(0f, 90f, 0f) * fw * 1.2f, fp + Vector3.up * 0.6f + fw * 1.5f);
                    if (Time.unscaledTime > logT && RanchLife.I != null && RanchLife.I.robots.Count > 1) { var r = RanchLife.I.robots[1]; Debug.Log("FFDEMO terrain56 bugs jar James " + Jar56.Count(f) + " robot " + Jar56.Count(r) + " caught " + Jar56.Caught + " released " + Jar56.Released + " robot visit " + (r.visit == null ? "-" : r.visit.verb + "/" + r.visit.vs + "/" + r.visit.line)); }
                    if (w > 60f) Next("bugs done");
                    break;
                }
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
