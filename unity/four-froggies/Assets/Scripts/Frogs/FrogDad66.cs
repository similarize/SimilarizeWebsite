using System.Collections.Generic;
using UnityEngine;

// ffu66 (Bill 2026-10-20: "he should be doing stuff that all the froggies can do"): DAD = A FULL FROGGY.
// Dad keeps his house life loop (World/HouseWorld.cs Dad: cook, dishes, fridge, wipe, walk round the house). When he goes out
// the front door he becomes an AI-driven froggy agent on the ranch - a real Frog (CharacterController, hops, swims, sits on
// swings / hammocks, drives) using the same froggy AI verbs and vehicle code as the AI froggies. He is NOT a roster seat and not
// joinable: the agent is not in Game.frogs (the roster stays the fixed four), never human, never tags along across worlds.
// Outdoors he picks his own activities: hop about the lawn, swim in the pond, swing / hammock in the garden, play with the kids,
// and drive HIS OWN Cybertruck (garage bay 1, ownerName "Dad") round the ranch and park it back in his bay. Then back in the house.
// Robot phone PLAY tab + voice: "Dad, come play", "Dad, go for a drive", "Dad, swim with me" (DadFroggy.Order).
// Logs "DAD66 ...".
public static class DadFroggy
{
    public static Frog F;                 // Dad's froggy agent (null until his first outing)
    public static int Outings, Hops, Swims, Seats, Plays, Drives, Parks, Orders, Homes;
    public static readonly Color Green = new Color(0.22f, 0.48f, 0.2f);
    public static bool On { get { return Ffu33.Url("dad66") != "0"; } }   // ?dad66=0 = the ffu65 porch stroll
    public static bool Active { get { return F != null && F.gameObject.activeSelf; } }
    public static string Act { get { return Active ? F.DadAct : ""; } }

    public static Vehicle Truck()
    {
        foreach (var v in Vehicle.All) if (v != null && v.ownerName == "Dad" && v.body != null) return v;
        return null;
    }

    static Frog Make(Vector3 pos)
    {
        var go = new GameObject("Dad (froggy)");
        var f = go.AddComponent<Frog>();
        f.Build(1, pos, 0f);
        f.id = 9; f.nick = "Dad"; f.isDad = true; f.human = false; f.world = WorldId.Ranch;
        // the froggy daddy look (same as in the house): froggy mesh at 1.5x, darker green, apron + red tie
        if (f.model != null) { f.model.gameObject.SetActive(false); Object.Destroy(f.model.gameObject); }
        var mg = new GameObject("Model"); mg.transform.SetParent(go.transform, false);
        var dm = new GameObject("FroggyDaddy"); dm.transform.SetParent(mg.transform, false); dm.transform.localScale = Vector3.one * 1.5f;
        var fm = dm.AddComponent<FrogModel>(); fm.Build(Green);
        var body = fm.bob != null ? fm.bob : dm.transform;
        Mats.Prim(PrimitiveType.Cube, body, new Vector3(0f, 0.42f, 0.36f), new Vector3(0.5f, 0.42f, 0.04f), Vector3.zero, Mats.Lit(new Color(0.92f, 0.9f, 0.85f)), false);
        Mats.Prim(PrimitiveType.Cube, body, new Vector3(0f, 0.66f, 0.34f), new Vector3(0.36f, 0.05f, 0.03f), Vector3.zero, Mats.Lit(new Color(0.75f, 0.2f, 0.18f)), false);
        // FrogModel lives on the scaled child; the Frog animates it like any froggy
        f.model = fm;
        Mats.SetLayer(go, 9);
        f.cc.height = 1.6f; f.cc.radius = 0.5f; f.cc.center = new Vector3(0f, 0.82f, 0f);
        var tag = new GameObject("Tag"); tag.transform.SetParent(dm.transform, false); tag.transform.localPosition = Vector3.up * 1.55f; tag.transform.localScale = Vector3.one / 1.5f;
        var tm = tag.AddComponent<TextMesh>(); tm.text = "Dad"; UIK.WorldText(tm, 48, 0.04f); tm.anchor = TextAnchor.MiddleCenter;
        tag.AddComponent<Billboard>();
        Debug.Log("DAD66 froggy agent built (not a roster seat: in Game.frogs = " + (Game.I != null && Game.I.frogs.Contains(f)) + ")");
        return f;
    }

    // the house Dad steps out of the front door: the froggy agent takes over on the ranch
    public static void GoOut(Vector3 at, string first, Frog with)
    {
        if (F == null) F = Make(at);
        F.gameObject.SetActive(true);
        F.world = WorldId.Ranch;
        F.Teleport(at + Vector3.up * 0.2f);
        F.DadStart(first, with);
        Outings++;
        Debug.Log("DAD66 out on the ranch " + at.ToString("0.0") + " first " + (first ?? "(his own pick)"));
    }

    // back through the front door: the house Dad takes over again
    public static void ComeIn()
    {
        if (F == null) return;
        if (F.vehicle != null) F.ExitVehicle();
        F.gameObject.SetActive(false);
        Homes++;
        Debug.Log("DAD66 back in the house");
        if (Dad.I != null) Dad.I.BackIn();
    }

    // nobody in the house: now and then Dad goes out for some fun on his own (inside, his life loop's front-door plan does it)
    static float outT = -1f;
    public static void Tick(bool anyoneInside, float dt)
    {
        if (!On || Dad.I == null || Active || Story.Active || Ffu33.Url("ffdemo") != null) return;
        if (outT < 0f) outT = Random.Range(120f, 240f);
        if (anyoneInside) return;
        outT -= Mathf.Min(dt, 0.1f);
        if (outT <= 0f) { outT = -1f; Dad.I.OutNow(null, null); }
    }

    // robot phone / voice: "Dad, come play" (play), "Dad, go for a drive" (drive), "Dad, swim with me" (swim)
    public static string Order(Frog from, string what)
    {
        Orders++;
        if (Dad.I == null && HouseWorld.I != null) HouseWorld.I.EnsureBuilt();   // Dad lives in the house (built on first use)
        if (Dad.I == null && F == null) return "Dad isn't home in this mode";
        string line = what == "drive" ? "Dad: Road trip round the ranch! I'll take my Cybertruck."
                    : what == "swim" ? "Dad: Last one in the pond is a tadpole!"
                    : "Dad: Coming to play! Ready or not!";
        if (what == "drive")
        {
            var t = Truck();
            if (t == null) line = "Dad: My Cybertruck isn't here right now.";
            else if (t.driver != null && t.driver != F) line = "Dad: " + t.driver.nick + " has my Cybertruck - ask when you're done!";
        }
        if (!Active) { if (Dad.I != null) Dad.I.OutNow(what, from); }
        else F.DadStart(what, from);
        Debug.Log("DAD66 order " + what + " from " + (from != null ? from.nick : "?") + " -> '" + line + "'");
        return line;
    }
}

public partial class Frog
{
    public bool isDad;                       // ffu66: Dad's froggy agent (not in Game.frogs; not joinable)
    string dadAct = "";
    public string DadAct { get { return dadAct; } }
    public bool DadSwimming { get { return swimming; } }
    readonly List<string> dadPlan = new List<string>();
    float dadT, dadWalkT, dadHopT;
    int dadStep;
    bool dadSat, dadWet;
    Vector3 dadGoal;
    Frog dadKid;
    Vector3[] dadRoute;
    static readonly string[] DadOwn = { "hop", "swim", "swing", "play", "drive" };

    public void DadStart(string first, Frog with)
    {
        if (vehicle != null) ExitVehicle();
        if (gardenSeat != null) { gardenSeat = null; }
        dadPlan.Clear();
        if (!string.IsNullOrEmpty(first)) dadPlan.Add(first);
        int n = Random.Range(2, 4);
        for (int k = 0; k < n; k++) { string a = DadOwn[Random.Range(0, DadOwn.Length)]; if (dadPlan.Count == 0 || dadPlan[dadPlan.Count - 1] != a) dadPlan.Add(a); }
        dadPlan.Add("home");
        dadKid = with;
        dadStep = -1; DadNext();
    }
    public void DadForce(string a) { dadPlan.Insert(Mathf.Clamp(dadStep + 1, 0, dadPlan.Count), a); if (vehicle != null && a != "park") ExitVehicle(); DadNext(); }

    void DadNext()
    {
        if (vehicle is GardenSeat) ExitVehicle();
        gardenSeat = null;
        dadStep++;
        if (dadStep >= dadPlan.Count) { dadPlan.Add("home"); }
        dadAct = dadPlan[dadStep]; dadT = 0f; dadWalkT = 0f; dadSat = false; dadWet = false;
        if (dadAct == "play" && (dadKid == null || dadKid.world != WorldId.Ranch || !dadKid.gameObject.activeInHierarchy)) dadKid = DadPickKid();
        if (dadAct == "hop") dadGoal = transform.position + new Vector3(Random.Range(-14f, 14f), 0f, Random.Range(4f, 14f));
        if (dadAct == "swim") dadGoal = DadPond(transform.position, 0.9f, 0f);
        if (dadAct == "drive") { dadRoute = DadRoute(); }
        Debug.Log("DAD66 activity " + dadAct + (dadAct == "play" && dadKid != null ? " with " + dadKid.nick : "") + " t=" + Time.time.ToString("0.0"));
    }
    Frog DadPickKid()
    {
        if (Game.I == null) return null;
        var l = new List<Frog>();
        foreach (var k in Game.I.frogs) if (k != null && k.world == WorldId.Ranch && k.gameObject.activeInHierarchy && !k.IsPet && k.passengerOf == null) l.Add(k);
        return l.Count > 0 ? l[Random.Range(0, l.Count)] : null;
    }
    // a point on the pond: q < 1 in the water, > 1 on the bank, along the line from the pond centre through p (rotated by `turn` rad)
    public static Vector3 DadPond(Vector3 p, float q, float turn)
    {
        Vector2 c = Layout.PondC, R = Layout.PondR;
        Vector2 n = new Vector2((p.x - c.x) / R.x, (p.z - c.y) / R.y);
        if (n.sqrMagnitude < 1e-4f) n = Vector2.left;
        n.Normalize();
        float a = Mathf.Atan2(n.y, n.x) + turn;
        float x = c.x + Mathf.Cos(a) * R.x * q, z = c.y + Mathf.Sin(a) * R.y * q;
        return new Vector3(x, Ranch.GY(x, z), z);
    }
    static Vector3[] DadRoute()
    {
        float bx = Layout.BayX(1), fz = Layout.GarageC.y + Layout.GarageSize.y * 0.5f + 8f;
        return new[] { new Vector3(bx, 0f, fz), new Vector3(15f, 0f, 58f), new Vector3(-25f, 0f, 64f), new Vector3(-40f, 0f, 46f), new Vector3(bx, 0f, fz) };
    }

    // true = Dad set his own input this frame; false = the shared froggy AI below runs (garden seats)
    bool DadThink(float dt)
    {
        if (Dad.I != null && Dad.I.hotspot != null) Dad.I.hotspot.pos = vehicle != null ? vehicle.transform.position : transform.position;   // "talk to Dad" follows him outdoors
        gardenCool = 999f; horseCool = 999f;   // he picks his own activities (the shared seat AI only runs when he chose a seat)
        callT = 0f; callTo = null;
        if (world != WorldId.Ranch) { SendTo(WorldId.Ranch, Dad.PorchPoint(), 0f); }
        dadT += dt;
        var i = new PIn();
        Vector3 p = transform.position;
        switch (dadAct)
        {
            case "hop":
                {
                    Vector3 to = dadGoal - p; to.y = 0f;
                    if (to.magnitude < 1.5f) dadGoal = p + new Vector3(Random.Range(-10f, 10f), 0f, Random.Range(-10f, 10f));
                    if (to.magnitude > 0.3f) { Vector3 d = to.normalized; i.move = new Vector2(d.x, d.z) * 0.7f; }
                    dadHopT -= dt;
                    if (dadHopT <= 0f && cc.isGrounded) { dadHopT = 0.9f; i.hop = true; DadFroggy.Hops++; }
                    if (dadT > 16f) DadNext();
                    break;
                }
            case "swim":
                {
                    if (swimming && !dadWet) { dadWet = true; DadFroggy.Swims++; dadT = 0f; Debug.Log("DAD66 swimming in the pond " + p.ToString("0.0")); }
                    if (dadWet)
                    {
                        if (dadT < 22f) dadGoal = DadPond(p, 0.82f, 0.08f);       // paddle round the pond
                        else dadGoal = DadPond(p, 1.12f, 0f);                     // then out onto the bank
                        if (dadT > 22f && !swimming) { DadNext(); break; }
                        if (dadT > 60f) { Teleport(DadPond(p, 1.12f, 0f) + Vector3.up * 0.4f); DadNext(); break; }
                    }
                    else if ((dadWalkT += dt) > 70f) { FX.Smoke(p + Vector3.up * 0.4f, 1.4f, new Color(0.88f, 0.88f, 0.88f, 0.7f)); Teleport(DadPond(p, 1.03f, 0f) + Vector3.up * 0.4f); dadWalkT = 0f; }
                    Vector3 to = dadGoal - p; to.y = 0f;
                    if (dadKid != null && dadKid.world == WorldId.Ranch && dadKid.DadSwimmingOther && (dadKid.transform.position - p).magnitude < 25f) { to = dadKid.transform.position + dadKid.transform.right * 2f - p; to.y = 0f; }   // swim with the one who asked
                    WalkInput(ref i, to, dt);
                    if (swimming) i.hop = false;
                    break;
                }
            case "swing":
                {
                    if (vehicle is GardenSeat) { if (!dadSat) { dadSat = true; DadFroggy.Seats++; Debug.Log("DAD66 relaxing in the " + vehicle.Title); } if (dadT > 60f) DadNext(); return false; }
                    if (dadSat) { DadNext(); break; }   // the seat AI got him up: next thing
                    if (gardenSeat == null)
                    {
                        GardenSeat.Kind k = Random.value < 0.5f ? GardenSeat.Kind.Hammock : GardenSeat.Kind.Swing;
                        GardenSeat s = GardenSeat.FreeNear(p, 800f, k) ?? GardenSeat.FreeNear(p, 800f);
                        if (s == null) { DadNext(); break; }
                        gardenSeat = s; Garden.Claim(s, this); gardenWalkT = 0f;
                    }
                    if (dadT > 120f) { DadNext(); break; }
                    return false;   // the froggies' own seat AI walks him over, sits him down and pushes the swing (FrogGarden.cs)
                }
            case "play":
                {
                    Frog k = dadKid;
                    if (k == null || k.world != WorldId.Ranch || !k.gameObject.activeInHierarchy) { k = dadKid = DadPickKid(); if (k == null) { DadNext(); break; } }
                    Vector3 kp = k.vehicle != null ? k.vehicle.transform.position : k.transform.position;
                    Vector3 to = kp - p; to.y = 0f;
                    if (to.magnitude < 3.5f)
                    {
                        if (!dadSat) { dadSat = true; DadFroggy.Plays++; dadT = 0f; if (k.human) k.Toast("Dad: Tag - you're it!", 2.5f); Debug.Log("DAD66 playing with " + k.nick + " (" + to.magnitude.ToString("0.0") + " m)"); }
                        // circle round the kid and hop with them
                        Vector3 side = Quaternion.Euler(0f, 90f, 0f) * (to.sqrMagnitude > 0.01f ? to.normalized : transform.forward);
                        i.move = new Vector2(side.x, side.z) * 0.55f;
                        dadHopT -= dt;
                        if (dadHopT <= 0f && cc.isGrounded) { dadHopT = 1.4f; i.hop = true; DadFroggy.Hops++; }
                    }
                    else
                    {
                        WalkInput(ref i, to, dt);
                        if ((dadWalkT += dt) > 60f && k.vehicle == null) { FX.Smoke(p + Vector3.up * 0.4f, 1.4f, new Color(0.88f, 0.88f, 0.88f, 0.7f)); Teleport(kp - to.normalized * 3f + Vector3.up * 0.3f); dadWalkT = 0f; }
                    }
                    if (dadSat && dadT > 30f || dadT > 120f) DadNext();
                    break;
                }
            case "drive":
            case "park":
                {
                    Vehicle t = DadFroggy.Truck();
                    if (t == null || (t.driver != null && t.driver != this)) { DadNext(); break; }
                    if (vehicle != t)
                    {
                        if (vehicle != null) ExitVehicle();
                        Vector3 cp = t.body.ClosestPoint(p + Vector3.up);
                        Vector3 to = cp - p; to.y = 0f;
                        if (to.magnitude < 2.6f || (dadWalkT += dt) > 80f)
                        {
                            EnterVehicle(t);
                            if (vehicle != t) { DadNext(); break; }
                            DadFroggy.Drives++; dadT = 0f; dadStep2 = dadAct == "park" ? 99 : 0;
                            Debug.Log("DAD66 drives his own " + t.Title + " (owner " + t.ownerName + ")");
                        }
                        else { WalkInput(ref i, to - to.normalized * 1.4f, dt); }
                        break;
                    }
                    // in his Cybertruck: the route round the ranch, then home to his bay
                    Vector3 vp = t.transform.position;
                    bool home = dadStep2 >= dadRoute.Length || dadT > 150f;
                    Vector3 goal = home ? t.HomePos : dadRoute[dadStep2];
                    float dd = new Vector2(goal.x - vp.x, goal.z - vp.z).magnitude;
                    if (!home && dd < 7f) { dadStep2++; break; }
                    if (home && (dd < 3f && Mathf.Abs(t.ForwardSpeed) < 1.2f || dadT > 260f))
                    {
                        t.ResetHome();   // squared up in his bay
                        DadFroggy.Parks++;
                        Debug.Log("DAD66 parked his Cybertruck back in bay 1 (" + (t.transform.position - t.HomePos).magnitude.ToString("0.0") + " m from the bay spot)");
                        ExitVehicle();
                        DadNext(); break;
                    }
                    i = DadSteer(t, goal, home ? 7f : 13f, home ? 2.5f : 0f, out camYaw);
                    input = i;
                    return true;
                }
            default:   // home: back to the front door
                {
                    Vector3 hp = Dad.PorchPoint();
                    Vector3 to = hp - p; to.y = 0f;
                    if (to.magnitude < 1.6f || (dadWalkT += dt) > 90f) { input = new PIn(); DadFroggy.ComeIn(); return true; }
                    WalkInput(ref i, to, dt);
                    break;
                }
        }
        input = i; camYaw = 0f;
        return true;
    }
    int dadStep2;
    public bool DadSwimmingOther { get { return swimming; } }
    float dadStuckT, dadRevT;
    PIn DadSteer(Vehicle v, Vector3 goal, float maxSp, float arrive, out float cy)
    {
        // the same ground steering the robots use for the ranch vehicles (World/RobotDrive.cs Steer), for Dad's own truck
        var i = new PIn();
        Vector3 p = v.transform.position, fwd = v.transform.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        fwd.Normalize();
        Vector3 to = goal - p; to.y = 0f;
        float d = to.magnitude, ang = d > 0.01f ? Vector3.SignedAngle(fwd, to, Vector3.up) : 0f, fs = v.ForwardSpeed;
        cy = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
        if (arrive > 0f && d < arrive) { i.move.y = fs > 0.8f ? -1f : 0f; return i; }
        if (dadRevT > 0f) { dadRevT -= Time.deltaTime; i.move.y = -0.8f; i.move.x = -Mathf.Sign(ang == 0f ? 1f : ang); return i; }
        float steer = Mathf.Clamp(ang / 28f, -1f, 1f);
        RaycastHit hit;
        Vector3 o = p + Vector3.up * 1.2f + fwd * (v.body.size.z * 0.5f);
        if (Physics.SphereCast(o, 0.9f, fwd, out hit, 5f + Mathf.Max(0f, fs) * 0.8f, Vehicle.GroundMask, QueryTriggerInteraction.Ignore) && hit.normal.y < 0.5f)
            steer = Mathf.Clamp(steer + (Vector3.Dot(hit.normal, new Vector3(fwd.z, 0f, -fwd.x)) >= 0f ? 1f : -1f) * 1.2f, -1f, 1f);
        float corner = Mathf.Clamp01(Mathf.Abs(ang) / 110f);
        float want = Mathf.Min(maxSp, d * 0.7f + 3f) * (1f - corner * 0.65f);
        if (arrive > 0f) want = Mathf.Min(want, (d - arrive) * 0.8f + 1.5f);
        float thr = Mathf.Clamp((want - fs) * 0.35f, -1f, 1f);
        if (Mathf.Abs(ang) > 120f && d < 18f) { thr = -0.7f; steer = -Mathf.Sign(ang); }
        i.move.x = steer; i.move.y = thr;
        if (thr > 0.15f && Mathf.Abs(fs) < 0.7f) dadStuckT += Time.deltaTime; else dadStuckT = Mathf.Max(0f, dadStuckT - Time.deltaTime);
        if (dadStuckT > 2.2f) { dadStuckT = 0f; dadRevT = 1.6f; }
        return i;
    }
}
