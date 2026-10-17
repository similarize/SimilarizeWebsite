using System.Collections.Generic;
using UnityEngine;

// ffu54 ROBOTS EVERYWHERE (Bill: "When James is on Mars, he may want to call a robot there and just have it come and say
// hi and do whatever there... the robot should be able to do anything a froggy can do... always doing chores while not
// being used, but they ought to do whatever James wants.")
//  * Robot.Call: the phone's CALL works in EVERY world (ranch, house, Real Room, Mars, Callisto, Moon, space, the pond /
//    under water, the forest). The robot arrives the way that world allows: runs over (same place), flies in on its jets
//    (ranch far away, space, the pond), rides a drop pod down on a retro-rocket (Mars, Callisto, Moon) or walks in through
//    the door (house, Real Room). Then it turns to the caller, waves and says hi.
//  * Robot.Play: the PLAY tab = the froggy verb set (follow, tag, hop, swim, dive, climb a tree, ride a horse / skateboard /
//    bike, ride along in your vehicle, play catch with a plushie, hunt (Figure 03's bow, borrowed), cook + eat (pretend),
//    push your swing, hammock nap, collect orbs / pearls / crystals, dance) + six-year-old extras (piggyback, build a
//    sandcastle / Mars base / ice fort / block fort, race me). Drive orders use the same Ownership53 table as froggies.
//  * A visiting robot stays with its caller (also across worlds); left alone it does a chore that fits the world (piles
//    Mars rocks, chips Callisto ice, tidies toys in the house, polishes the ship in space). Any order from James wins.
//    Back on the ranch an idle visitor simply returns to the ranch chore brain (RobotChores.cs).
// Logs "ROBOTS54 ..." lines (probe). Local per device (robots are not synced online), like the rest of the robot code.
public class RobotVisit
{
    public Frog lead;
    public WorldId w;
    public int ph;                 // 0 arriving, 1 with the caller
    public string how = "walk";    // walk | jet | pod | door
    public string verb = "with";
    public string line = "on my way";
    public float t, vt, idleT, total;
    public int vs, count;
    public Vector3 vp, vp2;
    public System.Action onArrive;
    public string queued;          // a PLAY verb to start on arrival
    public GameObject pod, prop, plush, build;
    public float podV, podT;
    public Vehicle vv;
    public TreeClimb.Tree tree;
    public Pickups.Item pick; public int orb = -1;
    public bool swimming;
    public Rigidbody rrPlush;
}

public partial class Robot
{
    public RobotVisit visit;
    [System.NonSerialized] public float yOver = float.NaN;   // visit: body height this frame (swim / jets / hop / float); NaN = on the ground
    [System.NonSerialized] public bool jetsOn;
    [System.NonSerialized] public float extPitch, extRoll;
    [System.NonSerialized] public Vehicle rideOf;            // riding along (not driving) in the caller's vehicle
    PIn piggyIn; float piggyYaw; bool piggyRun;
    public static float IdleChoreAfter = 30f;
    public static int Arrivals, Greets;
    public bool Visiting { get { return visit != null; } }
    bool Blk(float x, float z) { return visit != null ? RobotNav.WallBlocked(x, z) : RobotNav.Blocked(x, z); }

    static readonly string[] PlayVerbs = { "follow", "tag", "hop", "swim", "dive", "climb", "horse", "skate", "bike", "ridewith", "catch", "hunt", "cook", "swing", "hammock", "collect", "dance", "piggy", "build", "race", "wave", "lights", "fort", "bugs", "pets", "ridepet", "fetch", "stay", "home" };
    public static bool IsPlay(string v) { return System.Array.IndexOf(PlayVerbs, v) >= 0; }
    public static WorldId TargetWorld(Frog f) { return f == null ? WorldId.Ranch : f.world == WorldId.Underwater ? WorldId.Ranch : f.world; }   // ffu53: the pond IS the ranch
    public static string WorldName(WorldId w) { switch (w) { case WorldId.Space: return "space"; case WorldId.RealRoom: return "the Real Room"; case WorldId.House: return "the house"; case WorldId.Ranch: return "the ranch"; default: return w.ToString(); } }

    // where the caller is (the Real Room player has its own body; in a vehicle = the vehicle)
    public static Vector3 LeadPos(Frog f)
    {
        if (f == null) return Vector3.zero;
        if (f.world == WorldId.RealRoom && RealRoom.Captures(f)) return RealRoom.PlayerFeet;
        if (f.vehicle != null) return f.vehicle.transform.position;
        if (f.passengerOf != null) return f.passengerOf.transform.position;
        return f.transform.position;
    }
    public static Vector3 LeadFwd(Frog f)
    {
        Vector3 d = f.world == WorldId.RealRoom && RealRoom.Captures(f) ? Quaternion.Euler(0f, RealRoom.PlayerYaw, 0f) * Vector3.forward : (f.vehicle != null ? f.vehicle.transform.forward : f.transform.forward);
        d.y = 0f; return d.sqrMagnitude > 0.001f ? d.normalized : Vector3.forward;
    }

    float RayY(float x, float z, float refY)
    {
        RaycastHit h;
        int mask = world == WorldId.RealRoom ? 1 << RealRoom.Layer : Vehicle.GroundMask;
        if (Physics.Raycast(new Vector3(x, refY + 2.2f, z), Vector3.down, out h, 12f, mask, QueryTriggerInteraction.Ignore)) return h.point.y;
        return refY;
    }
    static bool LowG(WorldId w) { return w == WorldId.Mars || w == WorldId.Callisto || w == WorldId.Moon || w == WorldId.Space; }

    void SetWorld(WorldId nw)
    {
        if (world == nw) return;
        bool rr = nw == WorldId.RealRoom, wasRR = world == WorldId.RealRoom;
        world = nw;
        if (rr != wasRR) Mats.SetLayer(gameObject, rr ? RealRoom.Layer : 0);   // the Real Room camera only draws its own layer
    }

    // ---------------------------------------------------------------- CALL (every world)
    public string Call(Frog f, string thenVerb)
    {
        if (f == null) return "";
        if (mission != null) return robotName + " is on a space mission - MISSION > Abort first";
        if (RobotMechMission.For(this) != null) return robotName + " is flying a mech mission - MISSION > Recall mech first";
        if (hunt != null) hunt.Stop(null);
        if (drv != null) EndDrive(null);
        if (manual != null) ReleaseManual(false);
        if (rideOf != null) StopRide();
        AbortPlan();
        follow = null; danceT = 0f; landAuto = false; flying = false;
        WorldId tw = TargetWorld(f);
        Vector3 lp = LeadPos(f);
        float d = Vector2.Distance(new Vector2(lp.x, lp.z), new Vector2(transform.position.x, transform.position.z));
        if (visit != null && visit.lead == f && world == tw && visit.ph == 1)
        {
            // already here: just come close + say hi again
            EndVerb();
            visit.queued = thenVerb; visit.ph = 0; visit.how = "walk"; visit.t = 0f; visit.line = "coming over";
            ExtReset();
            return robotName + ": coming!";
        }
        if (visit != null && visit.lead == f && world == tw && visit.ph == 0) { if (thenVerb != null) visit.queued = thenVerb; return robotName + ": almost there - " + visit.line + "!"; }   // already on the way (mid-flight)
        if (visit != null) EndVisit(false, true);
        var v = new RobotVisit { lead = f, w = tw, queued = thenVerb };
        visit = v;
        cmd = "visit";
        Arrivals++;
        if (world == tw && d < 60f && !(tw == WorldId.Ranch && Layout.PondQ(lp.x, lp.z) < 0.97f)) { v.how = "walk"; v.line = "running over"; }
        else if (tw == WorldId.Mars || tw == WorldId.Callisto || tw == WorldId.Moon) { v.how = "pod"; v.line = "dropping in by rocket pod"; StartPod(); }
        else if (tw == WorldId.House || tw == WorldId.RealRoom) { v.how = "door"; v.line = "coming in the door"; StartDoor(); }
        else { v.how = "jet"; v.line = "flying in on my jets"; StartJet(); }
        Debug.Log("ROBOTS54 call " + robotName + " by " + f.nick + " world " + tw + " how " + v.how + (thenVerb != null ? " then " + thenVerb : "") + " dist " + (world == tw ? d.ToString("0") : "other world"));
        return robotName + ": on my way to " + WorldName(tw) + " - " + v.line + "!";
    }

    // ranch-place orders (chores, charging, roof pads, missions, ranch vehicles) from another world: fly home first
    public void ComeHomeFor(string why)
    {
        if (visit == null && world == WorldId.Ranch) return;
        bool away = world != WorldId.Ranch;
        EndVisit(false, false);
        if (away || transform.position.y < -50f)
        {
            Vector3 p0 = transform.position;
            FX.Smoke(p0 + Vector3.up * 0.5f, 1.4f, new Color(0.8f, 0.85f, 0.9f, 0.7f));
            SetWorld(WorldId.Ranch);
            Vector3 g = RobotNav.G(-44f + Random.Range(-5f, 5f), 27f + Random.Range(-2f, 2f));
            transform.position = g + Vector3.up * 16f;
            alt = transform.position.y;
            Debug.Log("ROBOTS54 home " + robotName + " for " + why);
        }
    }

    public void EndVisit(bool say, bool keepWorld)
    {
        if (visit == null) return;
        var v = visit;
        EndVerb();
        if (v.pod != null) Object.Destroy(v.pod);
        visit = null;
        yOver = float.NaN; jetsOn = false; extPitch = 0f; extRoll = 0f;
        if (hidden && seatedIn == null) SetHidden(false);
        if (cmd == "visit" || cmd == "dance") cmd = "auto";
        ExtReset();
        if (say && v.lead != null) v.lead.Toast(robotName + ": back to my chores - call me any time!", 2.5f);
        if (world == WorldId.Ranch && InWater(transform.position)) { Vector3 sh = RobotDriver.ShoreNear(transform.position); transform.position = sh; FX.Splash(sh, 1f); }   // climb out on the shore
    }

    // ---------------------------------------------------------------- PLAY (the froggy verb set)
    public string Play(string verb, Frog f)
    {
        if (f == null) return "";
        if (verb == "home")
        {
            if (visit == null && world == WorldId.Ranch) { Order("auto", f); return robotName + ": back to chores"; }
            ComeHomeFor("go home"); cmd = "auto"; forcedChore = -1;
            return robotName + ": flying home to my chores - bye " + f.nick + "!";
        }
        if (mission != null) return robotName + " is on a space mission (MISSION > Abort)";
        if (RobotMechMission.For(this) != null) return robotName + " is flying a mech mission (MISSION > Recall mech)";
        if (visit == null || visit.lead != f || world != TargetWorld(f) || visit.ph == 0)
        {
            string c = Call(f, verb);
            return c.Length > 0 && visit != null ? c + " Then: " + VerbName(verb) : c;
        }
        return StartVerb(verb);
    }

    static string VerbName(string v)
    {
        switch (v)
        {
            case "follow": return "follow you"; case "tag": return "play tag"; case "hop": return "hop with you"; case "swim": return "swim with you";
            case "dive": return "dive down"; case "climb": return "climb a tree"; case "horse": return "ride a horse"; case "skate": return "skateboard";
            case "bike": return "ride a bike"; case "ridewith": return "ride with you"; case "catch": return "play catch"; case "hunt": return "go hunting";
            case "cook": return "cook + eat"; case "swing": return "push your swing"; case "hammock": return "hammock nap"; case "collect": return "collect treasures";
            case "dance": return "dance"; case "piggy": return "piggyback"; case "build": return "build something"; case "race": return "race you";
            case "wave": return "wave"; case "lights": return "flip the lights"; case "fort": return "build the tree fort"; case "bugs": return "go on a bug hunt"; case "pets": return "pet + feed the animals"; case "ridepet": return "ride a cow"; case "fetch": return "play fetch"; case "stay": return "stay"; default: return v;
        }
    }

    Vector3 MyPos55() { return transform.position; }

    void EndVerb()
    {
        var v = visit;
        if (v == null) return;
        if (v.verb == "piggy") DropPiggy();
        if (v.verb == "fort") yOver = float.NaN;   // ffu56: interrupted on the ladder / deck = back on the ground
        if (v.verb == "pets" || v.verb == "ridepet" || v.verb == "fetch") EndPets57();   // ffu57
        if (rideOf != null) StopRide();
        if (v.prop != null) Object.Destroy(v.prop);
        if (v.plush != null) Object.Destroy(v.plush);
        if (v.rrPlush != null) { v.rrPlush.isKinematic = false; v.rrPlush.transform.SetParent(RealRoom.I != null ? RealRoom.I.transform : null, true); v.rrPlush = null; }
        if (Fig50.Bowed(this) && hunt == null) Fig50.Bow(this, false);
        v.prop = null; v.plush = null; v.vv = null; v.tree = null; v.pick = null; v.orb = -1;
        if (v.verb == "swim" || v.verb == "dive") { }
        v.verb = "with"; v.vs = 0; v.vt = 0f; v.count = 0;
        extPitch = 0f; extRoll = 0f;
        if (cmd == "dance") cmd = "visit";
    }

    string Say(string s) { if (visit != null) visit.line = s; return robotName + ": " + s; }

    string StartVerb(string verb)
    {
        var v = visit;
        if (drv != null) EndDrive(null);
        if (hunt != null) hunt.Stop(null);
        EndVerb();
        v.idleT = 0f;
        Frog f = v.lead;
        Vector3 lp = LeadPos(f);
        bool ranch = world == WorldId.Ranch;
        Debug.Log("ROBOTS54 verb " + robotName + " " + verb + " world " + world + " for " + f.nick);
        switch (verb)
        {
            case "follow": v.verb = verb; return Say("following you, " + f.nick + "!");
            case "tag": v.verb = verb; v.vs = 0; return Say("TAG! I'm it - run!");
            case "hop": v.verb = verb; return Say(LowG(world) ? "moon-bounce! boing... boing..." : "hop hop hop!");
            case "swim":
            case "dive":
                if (world == WorldId.Space) { v.verb = "swim"; return Say("space-swimming in zero-g!"); }
                if (!ranch) return Say("no water to swim in here - the pond is at the ranch");
                v.verb = verb; v.vs = 0; return Say(verb == "dive" ? "diving down to look for pearls!" : "to the pond - splash!");
            case "climb":
                {
                    float sd;
                    v.tree = ranch ? TreeClimb.Nearest(lp, 160f, out sd) : null;
                    if (v.tree == null) return Say(ranch ? "no tree close by to climb" : "no trees to climb here - the forest is at the ranch");
                    v.verb = verb; v.vs = 0; return Say("climbing a tree!");
                }
            case "horse":
            case "skate":
            case "bike":
                {
                    Vehicle best = null; float bd = 1e9f;
                    if (verb == "horse") best = Horse.FreeNear(transform.position, 600f);
                    else
                        foreach (var x in Vehicle.All)
                        {
                            if (x == null || x.driver != null || x.stowed || !x.isActiveAndEnabled || (verb == "skate" ? !(x is Skateboard) : !(x is Bike))) continue;
                            float dd = (x.transform.position - transform.position).sqrMagnitude;
                            if (dd < bd && dd < 600f * 600f) { bd = dd; best = x; }
                        }
                    if (best == null) return Say(ranch ? "no free " + (verb == "horse" ? "horse" : verb == "skate" ? "skateboard" : "bike") + " right now" : "the " + (verb == "horse" ? "horses are" : verb == "skate" ? "skateboards are" : "bikes are") + " at the ranch");
                    string s = DriveOrder(best, "follow", f);
                    return s;
                }
            case "ridewith":
                {
                    Vehicle x = f.vehicle;
                    if (x == null) return Say("hop in a vehicle and I'll ride along with you!");
                    if (x is Skateboard || x is GardenSeat) return Say("that one only fits one - I'll follow you");
                    v.verb = verb; v.vv = x; v.vs = 0; return Say("riding with you in the " + x.Title + "!");
                }
            case "catch":
                v.verb = verb; v.vs = 0; v.count = 0;
                if (world == WorldId.RealRoom && RealRoom.Plush != null) { v.rrPlush = RealRoom.Plush; return Say("I'll get the plush frog - catch!"); }
                v.plush = MakePlush(); v.plush.transform.SetParent(carryNode, false); v.plush.transform.localPosition = Vector3.zero;
                return Say("I brought a froggy plushie - catch!");
            case "hunt":
                if (!ranch) return Say("the bow is for the ranch cows - want a rock race instead?");
                return Fig50.PhoneHunt(this, f);
            case "cook": v.verb = verb; v.vs = 0; return Say(world == WorldId.Ranch && Campfire.I != null ? "to the campfire - pretend cookout!" : "pretend picnic time!");
            case "swing":
                {
                    if (!ranch || Garden.I == null) return Say("the swings are in the garden at the ranch");
                    var gs = f.vehicle as GardenSeat;
                    if (gs != null && gs.kind == GardenSeat.Kind.Swing) { v.verb = verb; v.vv = gs; v.vs = 0; return Say("I'll push you - higher!"); }
                    GardenSeat free = FreeSeat(GardenSeat.Kind.Swing, lp);
                    if (free == null) return Say("all the swings are taken");
                    return DriveOrder(free, "hold", f) + " (sit on a swing next to me and I'll push you!)";
                }
            case "hammock":
                {
                    if (!ranch || Garden.I == null) return Say("the hammocks are in the garden at the ranch");
                    GardenSeat free = FreeSeat(GardenSeat.Kind.Hammock, lp);
                    if (free == null) return Say("all the hammocks are taken");
                    return DriveOrder(free, "hold", f).Replace("on my way to the Hammock", "going for a hammock nap");
                }
            case "collect": v.verb = verb; v.vs = 0; v.count = 0; return Say(world == WorldId.Mars ? "collecting Mars crystals for you!" : world == WorldId.Callisto ? "collecting ice shards for you!" : world == WorldId.Moon ? "collecting moon samples!" : "collecting treasures for you!");
            case "dance": v.verb = verb; v.vt = 0f; cmd = "dance"; return Say("dance party!");
            case "piggy":
                if (f.vehicle != null || f.passengerOf != null || f.world == WorldId.RealRoom) return Say(f.world == WorldId.RealRoom ? "the ceiling is too low in here for a piggyback!" : "get out first - then hop on my back!");
                if (world == WorldId.Space) return Say("no piggybacks in zero-g - you'd float away!");
                v.verb = verb; v.vs = 0; return Say("hop on - piggyback ride!");
            case "build": v.verb = verb; v.vs = 0; v.vt = 0f; return Say("building you " + BuildName() + "!");
            case "race": v.verb = verb; v.vs = 0; v.vt = 0f; return Say("race you to the flag - ready, set, GO!");
            case "wave": wave = 2.4f; return Say("hi " + f.nick + "!");
            case "lights":   // ffu55: froggies flip the house / Real Room switches, so can robots
                {
                    Vector3 at;
                    if (!Light55.SwitchNear(world, MyPos55(), out at)) return Say(world == WorldId.Mars || world == WorldId.Moon || world == WorldId.Callisto ? "no light switch here - the sun is our lamp!" : "no light switch here - try the house!");
                    v.verb = verb; v.vp = at; v.vt = 0f; return Say("I'll get the lights!");
                }
            case "fort": return StartFort56();   // ffu56 forest toys (World/RobotVisit56.cs)
            case "bugs": return StartBugs56();
            case "pets": case "ridepet": case "fetch": return StartPets57(verb);   // ffu57 animals (World/RobotVisit57.cs)
            case "stay": v.verb = verb; return Say("I'll stay right here");
        }
        return "";
    }

    GardenSeat FreeSeat(GardenSeat.Kind k, Vector3 near)
    {
        GardenSeat best = null; float bd = 1e9f;
        foreach (var s in GardenSeat.Seats) { if (s == null || s.kind != k || s.driver != null) continue; float d = (s.transform.position - near).sqrMagnitude; if (d < bd) { bd = d; best = s; } }
        return best;
    }

    string BuildName()
    {
        switch (world)
        {
            case WorldId.Mars: return "a little Mars base";
            case WorldId.Callisto: case WorldId.Moon: return "an ice fort";
            case WorldId.House: case WorldId.RealRoom: return "a block tower";
            case WorldId.Space: return "a mini space station";
            default: return NearPond(transform.position) ? "a sandcastle" : "a block fort";
        }
    }
    static bool NearPond(Vector3 p) { return Layout.PondQ(p.x, p.z) < 1.4f; }

    // ---------------------------------------------------------------- arrivals
    Vector3 SpotByLead(float r, float side)
    {
        Frog f = visit.lead;
        Vector3 lp = LeadPos(f), fw = LeadFwd(f), rt = new Vector3(fw.z, 0f, -fw.x);
        Vector3 c = lp + fw * r + rt * side;
        return c;
    }

    void StartPod()
    {
        var v = visit;
        SetWorld(v.w);
        Vector3 s = SpotByLead(7f, 3f);
        s.y = GroundAt(s.x, s.z);
        v.vp = s;
        v.pod = MakePod(s + Vector3.up * 140f);
        v.podV = 30f; v.podT = 0f;
        transform.position = s + Vector3.up * 140f;
        SetHidden(true);
        yOver = transform.position.y;
    }

    void StartJet()
    {
        var v = visit;
        SetWorld(v.w);
        Vector3 s = SpotByLead(4f, 2.5f);
        Vector3 fw = LeadFwd(v.lead);
        Vector3 from = s + new Vector3(fw.z, 0f, -fw.x) * 45f + fw * 25f + Vector3.up * 24f;
        transform.position = from;
        yOver = from.y; jetsOn = true;
        v.vp = s;
        ExtYaw(Mathf.Atan2(s.x - from.x, s.z - from.z) * Mathf.Rad2Deg);
    }

    void StartDoor()
    {
        var v = visit;
        SetWorld(v.w);
        Vector3 p = v.w == WorldId.RealRoom ? RealRoom.DoorIn : HouseWorld.Spawn(3);
        p.y = RayY(p.x, p.z, p.y + 0.3f);
        transform.position = p;
        yOver = float.NaN;
        FX.Smoke(p + Vector3.up * 0.4f, 0.6f, new Color(0.9f, 0.9f, 0.9f, 0.4f));
        Sfx.Play(Sfx.Door, 0.6f, 1.05f);
    }

    GameObject MakePod(Vector3 at)
    {
        var go = new GameObject("RobotPod " + robotName);
        go.transform.position = at;
        Material hull = Mats.Shiny(new Color(0.86f, 0.87f, 0.9f)), dark = Mats.Paint(new Color(0.12f, 0.13f, 0.15f), 0.6f), win = Mats.Unlit(new Color(0.45f, 0.8f, 1f));
        Mats.Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0f, 1.3f, 0f), new Vector3(1.9f, 1.3f, 1.9f), hull);
        Mats.Prim(PrimitiveType.Sphere, go.transform, new Vector3(0f, 2.65f, 0f), new Vector3(1.9f, 1.3f, 1.9f), hull);
        Mats.Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0f, 0.12f, 0f), new Vector3(2.2f, 0.12f, 2.2f), dark);
        for (int i = 0; i < 3; i++)
        {
            float a = i * 2.094f;
            var leg = Mats.Prim(PrimitiveType.Cube, go.transform, new Vector3(Mathf.Sin(a) * 1.15f, 0.35f, Mathf.Cos(a) * 1.15f), new Vector3(0.12f, 0.8f, 0.12f), dark);
            leg.transform.localRotation = Quaternion.Euler(Mathf.Cos(a) * 18f, 0f, -Mathf.Sin(a) * 18f);
        }
        Mats.Prim(PrimitiveType.Cube, go.transform, new Vector3(0f, 1.9f, 0.93f), new Vector3(0.7f, 0.35f, 0.05f), win);
        var hatch = Mats.Node(go.transform, "Hatch", new Vector3(0f, 0.25f, 0.95f));
        Mats.Prim(PrimitiveType.Cube, hatch, new Vector3(0f, 0.8f, 0.02f), new Vector3(1.0f, 1.6f, 0.06f), dark);
        var flame = Mats.Node(go.transform, "Flame", new Vector3(0f, -0.4f, 0f));
        var fm = new Material(Mats.Fx); fm.color = new Color(1f, 0.6f, 0.2f, 0.85f);
        Mats.Prim(PrimitiveType.Sphere, flame, new Vector3(0f, -0.6f, 0f), new Vector3(0.9f, 1.8f, 0.9f), fm);
        if (world == WorldId.RealRoom) Mats.SetLayer(go, RealRoom.Layer);
        return go;
    }

    GameObject MakePlush()
    {
        var go = new GameObject("FroggyPlushie");
        Material g = Mats.Lit(new Color(0.25f, 0.75f, 0.25f)), wh = Mats.Lit(Color.white), bk = Mats.Lit(new Color(0.05f, 0.05f, 0.05f)), bl = Mats.Lit(new Color(0.85f, 1f, 0.7f));
        Mats.Prim(PrimitiveType.Sphere, go.transform, new Vector3(0f, 0f, 0f), new Vector3(0.34f, 0.26f, 0.32f), g);
        Mats.Prim(PrimitiveType.Sphere, go.transform, new Vector3(0f, -0.05f, 0.06f), new Vector3(0.26f, 0.16f, 0.22f), bl);
        for (int s = -1; s <= 1; s += 2)
        {
            Mats.Prim(PrimitiveType.Sphere, go.transform, new Vector3(0.08f * s, 0.12f, 0.08f), Vector3.one * 0.11f, wh);
            Mats.Prim(PrimitiveType.Sphere, go.transform, new Vector3(0.085f * s, 0.13f, 0.125f), Vector3.one * 0.05f, bk);
            Mats.Prim(PrimitiveType.Sphere, go.transform, new Vector3(0.14f * s, -0.1f, 0.1f), new Vector3(0.1f, 0.06f, 0.14f), g);
        }
        if (world == WorldId.RealRoom) Mats.SetLayer(go, RealRoom.Layer);
        return go;
    }

    // ---------------------------------------------------------------- per frame (from Update)
    void VisitTick(float dt)
    {
        var v = visit;
        Frog f = v.lead;
        if (f == null || !f.gameObject.activeInHierarchy) { EndVisit(false, false); ComeHomeFor("caller gone"); return; }
        v.t += dt; v.total += dt;
        if (cmd != "dance") cmd = "visit";
        // the caller moved to another world (or the Real Room): follow like a called froggy
        WorldId tw = TargetWorld(f);
        if (tw != world && rideOf == null && v.verb != "piggy")
        {
            if (v.ph == 1 && v.verb != "follow" && v.verb != "with" && v.verb != "stay") EndVerb();
            v.w = tw; v.ph = 0; v.t = 0f;
            if (v.pod != null) Object.Destroy(v.pod);
            jetsOn = false; yOver = float.NaN; SetHidden(false);
            if (tw == WorldId.Mars || tw == WorldId.Callisto || tw == WorldId.Moon) { v.how = "pod"; v.line = "dropping in by rocket pod"; StartPod(); }
            else if (tw == WorldId.House || tw == WorldId.RealRoom) { v.how = "door"; v.line = "coming in the door"; StartDoor(); }
            else { v.how = "jet"; v.line = "flying in on my jets"; StartJet(); }
            Debug.Log("ROBOTS54 follow-world " + robotName + " -> " + tw + " how " + v.how);
            return;
        }
        Vector3 lp = LeadPos(f);
        if (v.ph == 0) { status = v.line; ArriveTick(dt, lp); return; }   // ffu55: the tag shows the arrival, not the old chore
        // battery: visiting is gentle (Update drains at 1/4); nearly flat -> fly home to charge
        if (battery < flatAt + 0.02f && world != WorldId.Ranch) { f.Toast(robotName + ": low battery - flying home to charge. Call me again soon!", 3f); ComeHomeFor("battery"); return; }
        v.idleT += (v.verb == "with" || v.verb == "stay") ? dt : 0f;
        if (v.verb == "with" && v.idleT > IdleChoreAfter)
        {
            if (world == WorldId.Ranch) { Debug.Log("ROBOTS54 idle -> ranch chores " + robotName); EndVisit(false, false); cmd = "auto"; return; }
            v.verb = "chore"; v.vs = 0; v.vt = 0f; v.line = ChoreLine(); Debug.Log("ROBOTS54 idle chore " + robotName + " in " + world + ": " + v.line);
        }
        VerbTick(dt, lp);
        status = v.line;
    }

    void ArriveTick(float dt, Vector3 lp)
    {
        var v = visit;
        Vector3 p = transform.position;
        if (v.how == "pod")
        {
            if (v.pod == null) { v.how = "walk"; SetHidden(false); yOver = float.NaN; return; }
            Transform pt = v.pod.transform, flame = pt.Find("Flame"), hatch = pt.Find("Hatch");
            float h = pt.position.y - v.vp.y;
            if (h > 0.02f)
            {
                float want = h > 30f ? 30f : Mathf.Lerp(2.5f, 30f, h / 30f);
                v.podV = Mathf.MoveTowards(v.podV, want, 25f * dt);
                pt.position = new Vector3(v.vp.x, Mathf.Max(v.vp.y, pt.position.y - v.podV * dt), v.vp.z);
                flame.gameObject.SetActive(h < 32f);
                if (h < 32f && Random.value < 0.6f) FX.Smoke(pt.position + Vector3.down * 0.6f, 0.9f, new Color(1f, 0.7f, 0.35f, 0.7f));
                if (h < 8f && Random.value < 0.5f) FX.Dust(v.vp, 1.2f);
                transform.position = pt.position + Vector3.up * 0.3f; yOver = transform.position.y;
                if (pt.position.y <= v.vp.y + 0.02f) { FX.Dust(v.vp, 3f); Sfx.PlayAt(Sfx.Thud, v.vp, 0.8f, 80f, 0.8f); flame.gameObject.SetActive(false); v.podT = 0f; Debug.Log("ROBOTS54 pod landed " + robotName + " in " + world + " at " + v.vp.ToString("0.0")); }
                return;
            }
            v.podT += dt;
            hatch.localRotation = Quaternion.Euler(Mathf.Clamp01(v.podT / 0.8f) * 85f, 0f, 0f);
            if (v.podT < 0.8f) { transform.position = pt.position; return; }
            if (hidden) { SetHidden(false); transform.position = pt.position + pt.forward * 1.2f; ExtYaw(pt.eulerAngles.y); yOver = float.NaN; Sfx.PlayAt(Sfx.Door, pt.position, 0.6f, 40f); }
            v.how = "walk"; v.t = 0f;
            return;
        }
        if (v.how == "jet")
        {
            Vector3 goal = v.vp;
            if (world == WorldId.Space) { goal = lp + LeadFwd(v.lead) * 7f + Vector3.up * 1.5f; }
            else { Vector3 s = SpotByLead(4f, 2.5f); goal = new Vector3(s.x, 0f, s.z); }
            float gy = world == WorldId.Space ? goal.y : WaterOrGround(goal.x, goal.z, p.y);
            Vector3 to = goal - p; to.y = 0f;
            float d = to.magnitude;
            float sp = Mathf.Min(14f, d * 1.2f + 1.5f);
            Vector3 np = d > 0.05f ? p + to / d * Mathf.Min(d, sp * dt) : p;
            float wantY = d > 8f ? Mathf.Max(gy + 6f, p.y - 4f * dt) : gy;
            yOver = Mathf.MoveTowards(p.y, wantY, (d > 8f ? 4f : 5f) * dt);
            transform.position = new Vector3(np.x, yOver, np.z);
            if (d > 0.1f) ExtYaw(Mathf.MoveTowardsAngle(transform.eulerAngles.y, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 200f * dt));
            jetsOn = true;
            if (Random.value < 0.5f) FX.Smoke(transform.position + Vector3.up * height * 0.3f, 0.35f, new Color(0.7f, 0.9f, 1f, 0.6f));
            if (d < 0.8f && Mathf.Abs(yOver - gy) < 0.15f)
            {
                jetsOn = false;
                bool wet = world == WorldId.Ranch && InWater(transform.position);
                if (wet) { FX.Splash(transform.position, 2f); Sfx.PlayAt(Sfx.SplashBig != null ? Sfx.SplashBig : Sfx.Splash, transform.position, 0.7f, 60f); visit.swimming = true; }
                else if (world != WorldId.Space) { FX.Dust(transform.position, 1.5f); Sfx.PlayAt(Sfx.Land != null ? Sfx.Land : Sfx.Thud, transform.position, 0.5f, 40f); yOver = float.NaN; }
                Greet();
            }
            return;
        }
        // walk / door: go to the caller (routed round the ranch buildings)
        if (world == WorldId.Ranch && InWater(lp)) { v.how = "jet"; v.vp = lp; return; }   // caller is in the pond: hop in with the jets
        float stop = world == WorldId.RealRoom ? 1.3f : 2.4f;
        float dist = WalkTo(lp, RunSpeed, stop, dt);
        if (dist <= stop + 0.15f || v.t > 45f) Greet();
    }

    float WaterOrGround(float x, float z, float refY)
    {
        if (world == WorldId.Ranch && Layout.PondQ(x, z) < 1.0f) { float bed = Ranch.GY(x, z); if (bed < Layout.WaterY - 0.5f) return Layout.WaterY - 0.35f; }
        return world == WorldId.Ranch || world == WorldId.Mars || world == WorldId.Callisto || world == WorldId.Moon ? GroundAt(x, z) : RayY(x, z, refY);
    }
    static bool InWater(Vector3 p) { return Layout.PondQ(p.x, p.z) < 1.0f && Ranch.GY(p.x, p.z) < Layout.WaterY - 0.5f; }

    void Greet()
    {
        var v = visit;
        v.ph = 1; v.t = 0f; v.idleT = 0f;
        Vector3 lp = LeadPos(v.lead);
        Vector3 to = lp - transform.position; to.y = 0f;
        if (to.sqrMagnitude > 0.01f) ExtYaw(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
        wave = 2.6f;
        Greets++;
        string hi = robotName + ": Hi " + v.lead.nick + "! " + (world == WorldId.Mars ? "Mars is so red! " : world == WorldId.Space ? "Look at all the stars! " : world == WorldId.Callisto ? "Brrr, icy! " : world == WorldId.Moon ? "Look, Earth! " : InWater(transform.position) ? "Blub blub! " : "") + "What should we do?";
        v.lead.Toast(hi, 3.5f);
        if (RobotPhone.I != null && RobotPhone.I.user == v.lead) RobotPhone.I.Say(hi);
        Sfx.PlayAt(Sfx.Pick(Sfx.Servo), transform.position + Vector3.up * height, 0.5f, 40f, 1.25f);
        FX.Sparkle(transform.position + Vector3.up * (height + 0.3f), new Color(0.6f, 1f, 0.7f), 10);
        v.line = "says hi to " + v.lead.nick;
        Debug.Log("ROBOTS54 arrive+wave " + robotName + " in " + world + " via " + v.how + " d " + RobotNav.Flat(lp - transform.position).ToString("0.0") + " t " + v.total.ToString("0.0") + " swim " + v.swimming);
        if (v.pod != null) { var pod = v.pod; v.pod = null; v.prop = null; PodLeave(pod); }
        if (v.onArrive != null) { var a = v.onArrive; v.onArrive = null; a(); }
        else if (v.queued != null) { string q = v.queued; v.queued = null; string s = StartVerb(q); if (s.Length > 0) v.lead.Toast(s, 2.5f); if (RobotPhone.I != null && RobotPhone.I.user == v.lead) RobotPhone.I.Say(s); }
    }

    // the empty pod lifts off again after a few seconds (back up to the carrier sky)
    void PodLeave(GameObject pod) { var pl = pod.AddComponent<PodLift54>(); pl.wait = 6f; }

    // walk / swim towards a goal; returns the flat distance left
    float WalkTo(Vector3 goal, float spd, float stop, float dt)
    {
        Vector3 p = transform.position;
        float d = RobotNav.Flat(goal - p);
        bool wet = world == WorldId.Ranch && (InWater(p) || InWater(goal));
        if (world == WorldId.Ranch && d > 14f && !wet) { ExtWalk(goal, spd, stop, dt); }
        else if (d > stop) { Vector3 to = goal - p; to.y = 0f; goalDir = to.normalized; goalSpeed = Mathf.Min(spd, (d - stop) * 1.8f + 0.4f); }
        SwimFloat(dt, float.NaN);
        return d;
    }

    // in the pond: float at the surface (or towards a depth); out of it: hop / ground
    void SwimFloat(float dt, float wantY)
    {
        Vector3 p = transform.position;
        bool wet = world == WorldId.Ranch && InWater(p);
        if (world == WorldId.Space) { float fy = float.IsNaN(wantY) ? LeadPos(visit.lead).y + 1.2f + Mathf.Sin(Time.time * 0.8f) * 0.3f : wantY; yOver = Mathf.MoveTowards(float.IsNaN(yOver) ? p.y : yOver, fy, 3f * dt); extPitch = Mathf.MoveTowards(extPitch, visit.verb == "swim" ? 70f : 10f, 90f * dt); jetsOn = speed > 0.5f; return; }
        if (!wet) { if (visit.swimming) { visit.swimming = false; FX.Splash(p, 1f); } yOver = float.NaN; if (visit.verb != "hammock") extPitch = Mathf.MoveTowards(extPitch, 0f, 160f * dt); return; }
        if (!visit.swimming) { visit.swimming = true; FX.Splash(p, 1.4f); Sfx.PlayAt(Sfx.Splash, p, 0.6f, 50f); Debug.Log("ROBOTS54 swim start " + robotName + " at " + p.ToString("0.0")); }
        float bed = Ranch.GY(p.x, p.z) + 0.35f, top = Layout.WaterY - 0.3f;
        float y = float.IsNaN(wantY) ? top : Mathf.Clamp(wantY, bed, top);
        yOver = Mathf.MoveTowards(float.IsNaN(yOver) ? p.y : yOver, y, 1.6f * dt);
        extPitch = Mathf.MoveTowards(extPitch, 78f, 120f * dt);
        pose = 12;
        if (Random.value < dt * 3f) FX.Spray(new Vector3(p.x, Layout.WaterY, p.z) + transform.forward * 0.5f, Vector3.up * 1.2f, 0.12f, 0.5f, new Color(0.8f, 0.92f, 1f, 0.7f));
    }

    void Face(Vector3 at) { Vector3 to = at - transform.position; to.y = 0f; if (to.sqrMagnitude > 0.01f) faceYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg; }

    float Ground(Vector3 p) { return WaterOrGround(p.x, p.z, p.y); }

    void VerbTick(float dt, Vector3 lp)
    {
        var v = visit;
        Frog f = v.lead;
        Vector3 p = transform.position;
        Vector3 fw = LeadFwd(f);
        float d = RobotNav.Flat(lp - p);
        v.vt += dt;
        pose = 0;
        switch (v.verb)
        {
            case "with":
            case "stay":
                {
                    if (v.verb == "with" && d > 4.5f) WalkTo(lp, d > 10f ? RunSpeed : WalkSpeed, 3.2f, dt);
                    else { SwimFloat(dt, InWater(lp) ? lp.y : float.NaN); Face(lp); }
                    if (v.vt > 9f && d < 6f) { v.vt = 0f; if (Random.value < 0.4f) wave = 1.2f; }
                    break;
                }
            case "follow":
                {
                    Vector3 goal = lp - fw * 2.6f;
                    WalkTo(goal, Mathf.Max(RunSpeed, f.HSpeed * 1.05f), 0.6f, dt);
                    if (InWater(lp)) SwimFloat(dt, lp.y);
                    if (v.vt > 180f) { EndVerb(); }
                    break;
                }
            case "tag":
                {
                    if (v.vs == 2 || v.vs == 3) { Face(lp); if (v.vt > 1.6f) { v.vs -= 2; v.vt = 0f; } break; }   // freeze a beat after each tag
                    if (v.vs == 0)
                    {
                        WalkTo(lp, Mathf.Min(RunSpeed, 5.2f), 0.2f, dt);
                        if (d < 2.3f) { v.vs = 3; v.vt = 0f; v.count++; f.Toast(robotName + ": TAG! You're it, " + f.nick + "!", 2.2f); Sfx.Play(Sfx.Bonk != null ? Sfx.Bonk : Sfx.Pickup, 0.6f, 1.3f); wave = 1f; v.line = "tagged you - run away!"; Debug.Log("ROBOTS54 tag " + robotName + " tagged " + f.nick); }
                    }
                    else
                    {
                        Vector3 away = p - lp; away.y = 0f; if (away.sqrMagnitude < 0.01f) away = -fw;
                        Vector3 goal = p + away.normalized * 8f + new Vector3(Mathf.Sin(v.total * 0.7f), 0f, Mathf.Cos(v.total * 0.5f)) * 3f;
                        WalkTo(goal, d < 8f ? 5.4f : 2f, 0.2f, dt);
                        if (d < 2.3f) { v.vs = 2; v.vt = 0f; f.Toast(robotName + ": you got me! I'm it - here I come!", 2.2f); Sfx.Play(Sfx.Pickup, 0.6f, 1.5f); v.line = "it - chasing you!"; }
                    }
                    if (v.total > 600f || v.count > 20) EndVerb();
                    break;
                }
            case "hop":
                {
                    if (d > 3f) WalkTo(lp - fw * 1.5f + new Vector3(fw.z, 0f, -fw.x) * 2f, Mathf.Max(3f, f.HSpeed), 0.5f, dt); else Face(lp);
                    float per = LowG(world) ? 1.6f : 0.75f, hh = LowG(world) ? 2.4f : 0.7f;
                    float ph = (v.vt % per) / per;
                    float g = Ground(p);
                    yOver = g + Mathf.Sin(ph * Mathf.PI) * hh;
                    if (ph < dt / per * 1.5f) { Sfx.PlayAt(Sfx.Hop, p, 0.4f, 30f, 0.8f); FX.Dust(p, 0.4f); }
                    pose = 21;
                    if (v.vt > 40f) { yOver = float.NaN; EndVerb(); }
                    break;
                }
            case "swim":
            case "dive":
                {
                    if (world == WorldId.Space)
                    {
                        Vector3 goal = lp + fw * 4f + new Vector3(Mathf.Sin(v.vt * 0.5f), 0f, Mathf.Cos(v.vt * 0.5f)) * 3f;
                        WalkTo(goal, 2.2f, 0.3f, dt); pose = 12; SwimFloat(dt, lp.y + 1.5f + Mathf.Sin(v.vt) * 0.8f);
                        if (v.vt > 60f) EndVerb();
                        break;
                    }
                    bool inW = InWater(p);
                    if (v.vs == 5)
                    {
                        // CANNONBALL: a jet-assisted jump into the pond (the water within ~45 m)
                        float T = Mathf.Max(0.8f, RobotNav.Flat(v.vp2 - v.vp) / 9f), k = Mathf.Clamp01(v.vt / T);
                        Vector3 a5 = Vector3.Lerp(v.vp, v.vp2, k);
                        a5.y = Mathf.Lerp(v.vp.y, Layout.WaterY - 0.3f, k) + Mathf.Sin(k * Mathf.PI) * 4.5f;
                        transform.position = a5; yOver = a5.y; jetsOn = k < 0.7f; pose = 21; Face(v.vp2 + (v.vp2 - v.vp));
                        if (k >= 1f) { jetsOn = false; v.vs = 1; FX.Splash(a5, 3f); Sfx.PlayAt(Sfx.SplashBig != null ? Sfx.SplashBig : Sfx.Splash, a5, 0.8f, 70f); Debug.Log("ROBOTS54 cannonball splash " + robotName); }
                        break;
                    }
                    if (!inW && v.vs == 0)
                    {
                        Vector3 e0 = InWater(lp) ? lp : PondEdge(lp, 0.82f);
                        if (RobotNav.Flat(e0 - p) < 45f && v.vt > 0.6f)
                        {
                            v.vs = 5; v.vt = 0f; v.vp = p; v.vp2 = new Vector3(e0.x, Layout.WaterY - 0.3f, e0.z);
                            f.Toast(robotName + ": CANNONBALL!", 1.8f); Sfx.PlayAt(Sfx.Pick(Sfx.Servo), p, 0.5f, 40f, 1.3f);
                            break;
                        }
                        // walk to the shore nearest the caller (or into the pond where the caller is)
                        Vector3 goal = InWater(lp) ? lp : PondEdge(lp, 0.85f);
                        WalkTo(goal, RunSpeed, 0.3f, dt);
                        if (v.vt > 50f) { transform.position = new Vector3(goal.x, Layout.WaterY - 0.3f, goal.z); }
                        break;
                    }
                    v.vs = 1;
                    float wantY = float.NaN;
                    Vector3 sgoal;
                    if (v.verb == "dive")
                    {
                        if (v.pick == null || v.pick.taken) v.pick = Pickups.Nearest(WorldId.Ranch, p, 70f, "pearls");
                        if (v.pick != null && v.count < 3)
                        {
                            sgoal = v.pick.basePos; wantY = v.pick.basePos.y - 0.5f;
                            v.line = "diving for a pearl";
                            if (RobotNav.Flat(sgoal - p) < 1.6f && Mathf.Abs(p.y - v.pick.basePos.y) < 2.2f) { Pickups.CollectFor(v.pick, f); v.count++; v.pick = null; f.Toast(robotName + " found a pearl for you! (" + v.count + ")", 2.2f); Debug.Log("ROBOTS54 dive pearl " + robotName + " " + v.count); }
                        }
                        else { sgoal = lp; wantY = InWater(lp) ? lp.y : float.NaN; v.line = "swimming with you"; }
                    }
                    else
                    {
                        if (InWater(lp)) { sgoal = lp - fw * 2f; wantY = lp.y; v.line = "swimming with you"; }
                        else { float a = v.vt * 0.15f; Vector3 e = PondEdge(lp, 0.7f); sgoal = e + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 6f; v.line = "swimming laps - come in!"; }
                    }
                    Vector3 to = sgoal - p; to.y = 0f;
                    float dd = to.magnitude;
                    if (dd > 0.8f) { goalDir = to / dd; goalSpeed = Mathf.Min(2.4f, dd); }
                    SwimFloat(dt, wantY);
                    if (v.vt > 150f) EndVerb();
                    break;
                }
            case "climb":
                {
                    var tr = v.tree;
                    if (tr == null || tr.Gone) { EndVerb(); break; }
                    Vector3 b = tr.basePos;
                    Vector3 side = p - b; side.y = 0f; if (side.sqrMagnitude < 0.01f) side = Vector3.back;
                    side.Normalize();
                    float rr = tr.r + bulk * 0.35f + 0.15f;
                    if (v.vs == 0)
                    {
                        float dt0 = WalkTo(b + side * rr, RunSpeed, 0.25f, dt);
                        if (dt0 < 0.5f || v.vt > 60f) { v.vs = 1; v.vt = 0f; v.vp = b + side * rr; v.line = "climbing the tree!"; Debug.Log("ROBOTS54 climb start " + robotName); }
                        break;
                    }
                    float top = Mathf.Max(b.y + 2f, (tr.branchTop > 0f ? tr.branchTop : tr.topY - 1.5f) - 0.2f);
                    if (v.vs == 1)
                    {
                        float y = Mathf.Min(top, b.y + v.vt * 0.9f);
                        transform.position = new Vector3(v.vp.x, y, v.vp.z); yOver = y; Face(b); pose = 13;
                        if (y >= top - 0.01f) { v.vs = 2; v.vt = 0f; wave = 2f; f.Toast(robotName + ": I'm up the tree! I can see the whole ranch!", 2.5f); }
                    }
                    else if (v.vs == 2) { yOver = top; pose = 0; if (v.vt > 10f) { v.vs = 3; v.vt = 0f; } }
                    else
                    {
                        float y = Mathf.Max(b.y, top - v.vt * 1.6f);
                        transform.position = new Vector3(v.vp.x, y, v.vp.z); yOver = y; pose = 13;
                        if (y <= b.y + 0.01f) { yOver = float.NaN; EndVerb(); }
                    }
                    break;
                }
            case "ridewith":
                {
                    Vehicle x = v.vv;
                    if (x == null || f.vehicle != x) { if (rideOf != null) StopRide(); EndVerb(); break; }
                    if (rideOf == null)
                    {
                        float dx = WalkTo(x.transform.position, RunSpeed, 2.2f, dt);
                        if (dx < 3.2f || v.vt > 30f) { RideAlong(x); Debug.Log("ROBOTS54 ride along " + robotName + " in " + x.Title); }
                    }
                    break;
                }
            case "catch": CatchTick(dt, lp, d); break;
            case "cook": CookTick(dt, lp, d); break;
            case "swing":
                {
                    var gs = v.vv as GardenSeat;
                    if (gs == null || f.vehicle != gs) { EndVerb(); break; }
                    Vector3 behind = gs.transform.position - gs.transform.forward * 1.7f;
                    float dx = WalkTo(behind, RunSpeed, 0.3f, dt);
                    if (dx < 0.6f)
                    {
                        Face(gs.transform.position);
                        pose = 8;
                        if (v.vt > 1.6f) { v.vt = 0f; gs.Push(0.5f); Sfx.PlayAt(Sfx.Pick(Sfx.Servo), p + Vector3.up, 0.3f, 25f, 1.2f); v.count++; if (v.count == 1) f.Toast(robotName + ": wheee! Higher!", 1.8f); }
                    }
                    break;
                }
            case "collect": CollectTick(dt, lp); break;
            case "dance":
                cmd = "dance";
                if (d > 6f) { cmd = "visit"; WalkTo(lp, RunSpeed, 3f, dt); }
                if (v.vt > 14f) { cmd = "visit"; EndVerb(); }
                break;
            case "piggy": PiggyRobotTick(dt, lp, d); break;
            case "build": BuildTick(dt, lp); break;
            case "race": RaceTick(dt, lp); break;
            case "lights":
                {
                    float dd = WalkTo(v.vp, RunSpeed, 0.5f, dt);
                    if (dd < 1.0f) { Face(v.vp + (world == WorldId.House ? new Vector3(0f, 0f, v.vp.z > Worlds.HouseO.z + 4f ? -1f : 1f) : Vector3.zero)); pose = 8; if (v.vt > 0.8f) { string s = Light55.RobotFlip(world, transform.position); f.Toast(robotName + ": " + s, 2.2f); Debug.Log("ROBOTS55 lights " + robotName + " in " + world + ": " + s); EndVerb(); } }
                    else v.vt = 0f;
                    break;
                }
            case "fort": FortTick56(dt, lp); break;   // ffu56
            case "bugs": BugsTick56(dt, lp); break;
            case "pets": case "ridepet": case "fetch": PetsTick57(dt, lp); break;   // ffu57
            case "chore": ChoreTick(dt, lp, d); break;
            default: EndVerb(); break;
        }
    }

    static Vector3 PondEdge(Vector3 near, float q)
    {
        Vector2 c = Layout.PondC, R = Layout.PondR;
        Vector2 dd = new Vector2((near.x - c.x) / R.x, (near.z - c.y) / R.y);
        if (dd.sqrMagnitude < 0.0001f) dd = Vector2.left;
        dd.Normalize();
        float x = c.x + dd.x * R.x * q, z = c.y + dd.y * R.y * q;
        return new Vector3(x, Layout.WaterY, z);
    }

    // ---------------------------------------------------------------- ride along (passenger)
    void RideAlong(Vehicle x)
    {
        rideOf = x;
        Transform s = x.seat != null ? x.seat : x.transform;
        transform.SetParent(s, false);
        float ss = Mathf.Max(0.3f, x.seatScale);
        transform.localScale = Vector3.one * (ss * 1.15f / height);
        transform.localPosition = new Vector3(0.62f * ss, -0.44f * ss, -0.15f * ss);
        transform.localRotation = Quaternion.identity;
        pose = 9; speed = 0f;
        SetHidden(!x.showDriver);
        if (tagMesh != null) tagMesh.gameObject.SetActive(false);
        visit.line = "riding with " + visit.lead.nick;
    }
    void StopRide()
    {
        Vehicle x = rideOf;
        rideOf = null;
        transform.SetParent(null, true);
        transform.localScale = Vector3.one;
        Vector3 at = x != null ? x.ExitPoint() + (x.transform.right * 1.2f) : transform.position;
        transform.position = new Vector3(at.x, Ground(at), at.z);
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        pose = 0;
        SetHidden(false);
    }

    // ---------------------------------------------------------------- catch (plushie)
    void CatchTick(float dt, Vector3 lp, float d)
    {
        var v = visit; Frog f = v.lead;
        Transform pl = v.rrPlush != null ? v.rrPlush.transform : v.plush != null ? v.plush.transform : null;
        if (pl == null) { EndVerb(); return; }
        switch (v.vs)
        {
            case 0:   // go pick it up (the RR plush lies on the rug; ours starts in the hands)
                if (pl.parent == carryNode) { v.vs = 1; v.vt = 0f; break; }
                {
                    float dx = WalkTo(pl.position, RunSpeed, 0.7f, dt);
                    if (dx < 1.0f || v.vt > 25f)
                    {
                        pose = 3;
                        if (v.rrPlush != null) v.rrPlush.isKinematic = true;
                        pl.SetParent(carryNode, true); pl.localPosition = Vector3.zero;
                        v.vs = 1; v.vt = 0f;
                    }
                }
                break;
            case 1:   // to throwing distance, face the caller, wind up
                {
                    pose = 2;
                    float want = world == WorldId.RealRoom ? 1.6f : 4.5f;
                    if (d > want + 1f) WalkTo(lp, RunSpeed, want, dt);
                    else
                    {
                        Face(lp); pose = 16;
                        if (v.vt > 1.2f)
                        {
                            pose = 17;
                            pl.SetParent(null, true);
                            v.vp = pl.position; v.vp2 = lp + Vector3.up * (world == WorldId.RealRoom ? 0.3f : 0.6f) - LeadFwd(f) * -0.2f;
                            if (v.rrPlush != null) { v.rrPlush.isKinematic = false; Vector3 tv = v.vp2 - v.vp; v.rrPlush.velocity = tv / 0.7f + Vector3.up * 3.4f; v.vs = 3; }
                            else v.vs = 2;
                            v.vt = 0f; v.count++;
                            Sfx.PlayAt(Sfx.Pick(Sfx.Servo), p0(), 0.4f, 25f, 1.4f);
                            f.Toast(robotName + ": catch, " + f.nick + "! (" + v.count + ")", 1.8f);
                            Debug.Log("ROBOTS54 catch throw " + robotName + " " + v.count + (v.rrPlush != null ? " (Real Room plush)" : ""));
                        }
                    }
                    break;
                }
            case 2:   // our plushie flies a parabola to the caller
                {
                    pose = 17;
                    float k = Mathf.Clamp01(v.vt / 0.9f);
                    Vector3 a = Vector3.Lerp(v.vp, v.vp2, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 2.2f;
                    pl.position = a; pl.Rotate(400f * dt, 300f * dt, 0f, Space.Self);
                    if (k >= 1f) { FX.Sparkle(a, new Color(0.6f, 1f, 0.6f), 6); Sfx.PlayAt(Sfx.BumpSoft != null ? Sfx.BumpSoft : Sfx.Thud, a, 0.4f, 25f, 1.4f); Vector3 g = a; g.y = Ground(a) + 0.12f; pl.position = g; pl.rotation = Quaternion.identity; v.vs = 4; v.vt = 0f; }
                    break;
                }
            case 3:   // Real Room plush: physics flight
                pose = 17; if (v.vt > 2f) { v.vs = 4; v.vt = 0f; }
                break;
            case 4:   // fetch it again
                if (v.count >= 4) { f.Toast(robotName + ": that was fun! Keep the plushie!", 2f); if (v.plush != null) { v.plush = null; } v.rrPlush = null; EndVerb(); break; }
                if (v.vt > 1.2f) { v.vs = 0; v.vt = 0f; }
                break;
        }
    }
    Vector3 p0() { return transform.position + Vector3.up * height; }

    // ---------------------------------------------------------------- cook + eat (pretend)
    void CookTick(float dt, Vector3 lp, float d)
    {
        var v = visit; Frog f = v.lead;
        bool fire = world == WorldId.Ranch && Campfire.I != null && RobotNav.Flat(Campfire.I.Pos - lp) < 80f;
        bool wet = InWater(transform.position);
        switch (v.vs)
        {
            case 0:
                {
                    Vector3 at = fire ? Campfire.I.Pos + (transform.position - Campfire.I.Pos).normalized * 1.6f : lp + LeadFwd(f) * 2.2f;
                    float dx = WalkTo(at, RunSpeed, 0.4f, dt);
                    if (dx < 0.7f || v.vt > 40f)
                    {
                        v.vs = 1; v.vt = 0f;
                        if (fire && !Campfire.I.Lit) Campfire.I.Light(f);
                        if (!fire && !wet)
                        {
                            // a little pretend camp stove
                            v.prop = new GameObject("PretendStove");
                            v.prop.transform.position = new Vector3(transform.position.x, Ground(transform.position), transform.position.z) + transform.forward * 0.8f;
                            Mats.Prim(PrimitiveType.Cylinder, v.prop.transform, new Vector3(0f, 0.12f, 0f), new Vector3(0.45f, 0.12f, 0.45f), Mats.Paint(new Color(0.2f, 0.45f, 0.75f), 0.6f));
                            var fm = new Material(Mats.Fx); fm.color = new Color(0.4f, 0.7f, 1f, 0.8f);
                            Mats.Prim(PrimitiveType.Sphere, v.prop.transform, new Vector3(0f, 0.3f, 0f), new Vector3(0.22f, 0.18f, 0.22f), fm);
                            if (world == WorldId.RealRoom) Mats.SetLayer(v.prop, RealRoom.Layer);
                        }
                        v.line = wet ? "pretend underwater snack" : fire ? "roasting a pretend marshmallow" : "cooking pretend space soup";
                    }
                    break;
                }
            case 1:
                {
                    pose = 3;
                    Vector3 fp = fire ? Campfire.I.Pos : v.prop != null ? v.prop.transform.position : transform.position + transform.forward;
                    Face(fp);
                    if (Random.value < dt * 3f) FX.Smoke(fp + Vector3.up * 0.5f, 0.3f, new Color(0.8f, 0.8f, 0.8f, 0.5f));
                    if (v.vt > (wet ? 1f : 6f)) { v.vs = 2; v.vt = 0f; v.line = "pretend eating - nom nom"; f.Toast(robotName + ": mmm! (pretend robot munching - beep boop)", 2.4f); Debug.Log("ROBOTS54 cook done -> eat " + robotName + " fire " + fire); }
                    break;
                }
            case 2:
                pose = 18; Face(lp);
                if (v.vt > 4.5f) { wave = 1.5f; yOver = float.NaN; EndVerb(); }
                else if (v.vt > 3.6f) { yOver = Ground(transform.position) + Mathf.Sin((v.vt - 3.6f) / 0.9f * Mathf.PI) * 0.5f; }   // happy hop
                break;
        }
    }

    // ---------------------------------------------------------------- collect orbs / pearls / crystals / shards / samples
    void CollectTick(float dt, Vector3 lp)
    {
        var v = visit; Frog f = v.lead;
        Vector3 p = transform.position;
        if (v.count >= 5) { f.Toast(robotName + ": " + v.count + " treasures for you!", 2.2f); EndVerb(); return; }
        if (v.orb < 0 && (v.pick == null || v.pick.taken))
        {
            v.pick = null;
            if (world == WorldId.Ranch && Orbs.I != null)
            {
                float bd = 220f * 220f;
                for (int i = 0; i < Orbs.I.orbs.Count; i++) { var o = Orbs.I.orbs[i]; if (o.taken) continue; float dd = (o.root.position - p).sqrMagnitude; if (dd < bd) { bd = dd; v.orb = i; } }
            }
            if (v.orb < 0) v.pick = Pickups.Nearest(world, p, 260f, null);
            if (v.orb < 0 && v.pick == null) { f.Toast(robotName + ": nothing left to collect near here", 2f); EndVerb(); return; }
        }
        Vector3 at = v.orb >= 0 ? Orbs.I.orbs[v.orb].root.position : v.pick.basePos;
        if (v.orb >= 0 && Orbs.I.orbs[v.orb].taken) { v.orb = -1; return; }
        float dx = WalkTo(at, RunSpeed, 0.5f, dt);
        if (InWater(p)) SwimFloat(dt, at.y - 0.4f);
        float reach = 1.4f + height * 0.3f;
        if (dx < reach && Mathf.Abs(at.y - (p.y + height * 0.5f)) < height + 1.5f)
        {
            if (v.orb >= 0) { Orbs.I.Collect(v.orb, f); v.orb = -1; }
            else { Pickups.CollectFor(v.pick, f); v.pick = null; }
            v.count++; wave = 0.8f;
            Debug.Log("ROBOTS54 collect " + robotName + " " + v.count + " in " + world);
        }
        if (v.vt > 160f) EndVerb();
    }

    // ---------------------------------------------------------------- piggyback (the caller rides on the robot's back)
    public void PiggyInput(PIn i, float camYaw) { piggyIn = i; piggyYaw = camYaw; piggyRun = i.gas > 0.5f || i.brake > 0.5f; }
    public Vector3 PiggySeat { get { return transform.position + transform.up * height * 0.86f - transform.forward * (bulk * 0.35f + 0.12f); } }
    void PiggyRobotTick(float dt, Vector3 lp, float d)
    {
        var v = visit; Frog f = v.lead;
        if (v.vs == 0)
        {
            float dx = WalkTo(lp + LeadFwd(f) * 1.0f, RunSpeed, 0.4f, dt);
            if (dx < 1.4f || v.vt > 30f) { v.vs = 1; v.vt = 0f; }
            return;
        }
        if (v.vs == 1)
        {
            pose = 3; Face(lp + LeadFwd(f) * 3f);
            if (v.vt > 0.7f)
            {
                if (f.vehicle != null || f.passengerOf != null || TargetWorld(f) != world) { EndVerb(); return; }
                f.StartPiggy(this);
                v.vs = 2; v.vt = 0f; v.line = "giving " + f.nick + " a piggyback";
                f.Toast(robotName + ": hold on tight! Steer me with the stick - " + (f.inputKind == InputKind.Gamepad ? "A" : f.inputKind == InputKind.Touch ? "A" : "SPACE / E") + " hops off", 3f);
                Debug.Log("ROBOTS54 piggyback start " + robotName + " carries " + f.nick);
            }
            return;
        }
        if (f.piggyOf != this) { EndVerb(); return; }
        pose = 15;
        Vector3 m = new Vector3(piggyIn.move.x, 0f, piggyIn.move.y);
        if (m.sqrMagnitude > 0.01f)
        {
            goalDir = Quaternion.Euler(0f, piggyYaw, 0f) * m.normalized;
            goalSpeed = Mathf.Min(1f, m.magnitude) * (piggyRun ? RunSpeed : Mathf.Max(2.2f, WalkSpeed));
        }
        SwimFloat(dt, float.NaN);
        extPitch = Mathf.MoveTowards(extPitch, 10f, 60f * dt);
    }
    void DropPiggy()
    {
        if (visit == null) return;
        Frog f = visit.lead;
        if (f != null && f.piggyOf == this) f.EndPiggy(true);
    }

    // ---------------------------------------------------------------- build (sandcastle / Mars base / ice fort / block fort / block tower / station)
    void BuildTick(float dt, Vector3 lp)
    {
        var v = visit; Frog f = v.lead;
        if (v.vs == 0)
        {
            Vector3 at = lp + LeadFwd(f) * 3.5f;
            if (world == WorldId.Ranch && NearPond(lp) && !InWater(lp)) { Vector3 e = PondEdge(lp, 1.12f); at = new Vector3(e.x, 0f, e.z); }
            float dx = WalkTo(at + (transform.position - at).normalized * 1.3f, RunSpeed, 0.4f, dt);
            if (dx < 0.8f || v.vt > 35f)
            {
                v.vs = 1; v.vt = 0f;
                at.y = world == WorldId.Space ? lp.y : Ground(at);
                if (world == WorldId.Ranch && InWater(at)) at.y = Ranch.GY(at.x, at.z);
                if (build != null) Object.Destroy(build);
                build = new GameObject("RobotBuild54 " + BuildName());
                build.transform.position = at;
                buildN = 0;
                Debug.Log("ROBOTS54 build start " + robotName + " " + BuildName() + " in " + world + " at " + at.ToString("0.0"));
            }
            return;
        }
        if (build == null) { EndVerb(); return; }
        pose = 3; Face(build.transform.position);
        if (world == WorldId.Space) SwimFloat(dt, build.transform.position.y + 0.5f);
        int want = Mathf.Min(BuildParts, Mathf.FloorToInt(v.vt / 0.55f));
        while (buildN < want) { BuildPiece(buildN); buildN++; FX.Dust(build.transform.position + Vector3.up * 0.3f, 0.4f); if (buildN % 3 == 0) Sfx.PlayAt(Sfx.BumpSoft != null ? Sfx.BumpSoft : Sfx.Thud, build.transform.position, 0.3f, 25f, 1.3f); }
        if (buildN >= BuildParts)
        {
            wave = 2f;
            f.Toast(robotName + ": ta-da! " + char.ToUpper(BuildName()[0]) + BuildName().Substring(1) + " for " + f.nick + "!", 2.6f);
            Debug.Log("ROBOTS54 build done " + robotName + " " + BuildName() + " parts " + buildN);
            var keep = build; build = null; Object.Destroy(keep, 240f);
            EndVerb();
        }
    }
    GameObject build; int buildN;
    const int BuildParts = 14;
    void BuildPiece(int i)
    {
        Transform b = build.transform;
        string kind = BuildName();
        Color c; Material m;
        bool sand = kind.Contains("sand"), mars = kind.Contains("Mars"), ice = kind.Contains("ice"), station = kind.Contains("station");
        c = sand ? new Color(0.86f, 0.76f, 0.52f) : mars ? new Color(0.92f, 0.92f, 0.94f) : ice ? new Color(0.82f, 0.92f, 1f) : station ? new Color(0.85f, 0.86f, 0.9f) : Color.HSVToRGB((i * 0.13f) % 1f, 0.7f, 0.95f);
        m = sand || ice ? Mats.Lit(c) : Mats.Paint(c, 0.6f);
        GameObject g;
        if (sand)
        {
            // 4 towers + walls + a keep with a flag
            Vector3[] tw = { new Vector3(-0.7f, 0f, -0.7f), new Vector3(0.7f, 0f, -0.7f), new Vector3(0.7f, 0f, 0.7f), new Vector3(-0.7f, 0f, 0.7f) };
            if (i < 4) g = Mats.Prim(PrimitiveType.Cylinder, b, tw[i] + Vector3.up * 0.3f, new Vector3(0.38f, 0.3f, 0.38f), m);
            else if (i < 8) { Vector3 a = tw[i - 4], z = tw[(i - 3) % 4]; g = Mats.Prim(PrimitiveType.Cube, b, (a + z) * 0.5f + Vector3.up * 0.18f, new Vector3(Mathf.Abs(a.x - z.x) + 0.2f, 0.36f, Mathf.Abs(a.z - z.z) + 0.2f), m); }
            else if (i < 12) g = Mats.Prim(PrimitiveType.Sphere, b, tw[i - 8] + Vector3.up * 0.66f, new Vector3(0.3f, 0.2f, 0.3f), m);
            else if (i == 12) g = Mats.Prim(PrimitiveType.Cylinder, b, Vector3.up * 0.5f, new Vector3(0.6f, 0.5f, 0.6f), m);
            else { g = Mats.Prim(PrimitiveType.Cube, b, Vector3.up * 1.35f, new Vector3(0.03f, 0.7f, 0.03f), Mats.Lit(new Color(0.4f, 0.28f, 0.15f))); Mats.Prim(PrimitiveType.Cube, b, new Vector3(0.14f, 1.58f, 0f), new Vector3(0.26f, 0.16f, 0.02f), Mats.Lit(new Color(0.25f, 0.75f, 0.25f))); }
        }
        else if (mars || station)
        {
            if (i == 0) g = Mats.Prim(PrimitiveType.Sphere, b, Vector3.up * 0.2f, new Vector3(2.6f, 2.2f, 2.6f), m);
            else if (i < 5) { float a = i * 1.57f; g = Mats.Prim(PrimitiveType.Cylinder, b, new Vector3(Mathf.Sin(a) * 2.1f, 0.5f, Mathf.Cos(a) * 2.1f), new Vector3(0.9f, 0.5f, 0.9f), m); }
            else if (i < 9) { float a = i * 1.57f + 0.78f; g = Mats.Prim(PrimitiveType.Cube, b, new Vector3(Mathf.Sin(a) * 1.6f, 0.35f, Mathf.Cos(a) * 1.6f), new Vector3(0.5f, 0.5f, 1.2f), m); g.transform.localRotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f); }
            else if (i < 12) g = Mats.Prim(PrimitiveType.Cube, b, new Vector3(-2.8f, 0.6f + (i - 9) * 0.02f, -0.6f + (i - 9) * 0.6f), new Vector3(0.05f, 1.2f, 0.55f), Mats.Paint(new Color(0.1f, 0.15f, 0.35f), 0.9f));   // solar panels
            else if (i == 12) g = Mats.Prim(PrimitiveType.Cube, b, new Vector3(0f, 2.2f, 0f), new Vector3(0.05f, 1.4f, 0.05f), Mats.Lit(Color.white));
            else { g = Mats.Prim(PrimitiveType.Cube, b, new Vector3(0.3f, 2.7f, 0f), new Vector3(0.55f, 0.32f, 0.02f), Mats.Lit(new Color(0.25f, 0.75f, 0.25f))); }
        }
        else if (ice)
        {
            float a = i / (float)(BuildParts - 2) * Mathf.PI * 2f;
            if (i < BuildParts - 2) g = Mats.Prim(PrimitiveType.Cube, b, new Vector3(Mathf.Sin(a) * 1.5f, 0.25f + (i % 2) * 0.5f, Mathf.Cos(a) * 1.5f), new Vector3(0.7f, 0.5f, 0.35f), m);
            else g = Mats.Prim(PrimitiveType.Sphere, b, Vector3.up * (i == BuildParts - 2 ? 0.5f : 1.0f), new Vector3(2.2f, 1.4f, 2.2f) * (i == BuildParts - 2 ? 1f : 0.6f), m);
            if (g != null) g.transform.localRotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f);
        }
        else
        {
            bool tower = kind.Contains("tower");
            float s = tower ? 0.22f : 0.5f;
            int col = i % (tower ? 2 : 4), row = i / (tower ? 2 : 4);
            Vector3 lp = tower ? new Vector3((col - 0.5f) * s, s * 0.5f + row * s, 0f) : new Vector3((col - 1.5f) * s * 1.05f, s * 0.5f + row * s, row % 2 == 0 ? 0f : s * 0.5f);
            g = Mats.Prim(PrimitiveType.Cube, b, lp, Vector3.one * s, m);
            g.transform.localRotation = Quaternion.Euler(0f, (i * 37) % 11 - 5, 0f);
        }
        if (world == WorldId.RealRoom) Mats.SetLayer(build, RealRoom.Layer);
    }

    // ---------------------------------------------------------------- race to the flag
    void RaceTick(float dt, Vector3 lp)
    {
        var v = visit; Frog f = v.lead;
        if (v.vs == 0)
        {
            Vector3 fw = LeadFwd(f);
            float len = world == WorldId.RealRoom || world == WorldId.House ? 6f : 30f;
            Vector3 fl = lp + fw * len; fl.y = world == WorldId.Space ? lp.y : Ground(fl);
            v.vp = fl;
            v.prop = new GameObject("RaceFlag54");
            v.prop.transform.position = fl;
            Mats.Prim(PrimitiveType.Cube, v.prop.transform, Vector3.up * 1f, new Vector3(0.05f, 2f, 0.05f), Mats.Lit(Color.white));
            Mats.Prim(PrimitiveType.Cube, v.prop.transform, new Vector3(0.35f, 1.75f, 0f), new Vector3(0.7f, 0.45f, 0.02f), Mats.Unlit(new Color(1f, 0.2f, 0.2f)));
            if (world == WorldId.RealRoom) Mats.SetLayer(v.prop, RealRoom.Layer);
            transform.position = new Vector3(lp.x, transform.position.y, lp.z) + new Vector3(fw.z, 0f, -fw.x) * 1.8f;
            v.vs = 1; v.vt = 0f;
            return;
        }
        if (v.vs == 1)
        {
            float dr = WalkTo(v.vp, Mathf.Min(RunSpeed, LowG(world) ? 3.6f : 5.2f), 0.2f, dt);
            if (LowG(world)) { float per = 1.2f; yOver = Ground(transform.position) + Mathf.Abs(Mathf.Sin(v.vt / per * Mathf.PI)) * 1.6f; }
            float dl = RobotNav.Flat(v.vp - lp);
            if (dl < 1.6f) { v.vs = 2; v.vt = 0f; f.Toast(robotName + ": you WIN, " + f.nick + "! You're so fast!", 2.6f); Sfx.Play(Sfx.Win != null ? Sfx.Win : Sfx.Pickup, 0.6f); Debug.Log("ROBOTS54 race won by " + f.nick); }
            else if (dr < 0.6f) { v.vs = 2; v.vt = 0f; f.Toast(robotName + ": I win! Rematch? (PLAY > Race me!)", 2.6f); Debug.Log("ROBOTS54 race won by " + robotName); }
            if (v.vt > 60f) { v.vs = 2; v.vt = 0f; }
            return;
        }
        yOver = float.NaN; wave = 1.5f;
        if (v.vt > 3f) EndVerb();
    }

    // ---------------------------------------------------------------- idle chores in other worlds (any order wins)
    string ChoreLine()
    {
        switch (world)
        {
            case WorldId.Mars: return "piling up Mars rocks";
            case WorldId.Callisto: return "chipping ice blocks";
            case WorldId.Moon: return "stacking moon rocks";
            case WorldId.Space: return "polishing the ship";
            case WorldId.House: return "tidying up the toys";
            case WorldId.RealRoom: return "dusting the shelf";
            default: return "tidying up";
        }
    }
    void ChoreTick(float dt, Vector3 lp, float d)
    {
        var v = visit;
        if (d > 18f) { WalkTo(lp, RunSpeed, 6f, dt); return; }
        if (world == WorldId.Space || world == WorldId.RealRoom)
        {
            pose = 5; SwimFloat(dt, float.NaN);
            if (v.vt > 6f) { v.vt = 0f; Vector3 o = lp + new Vector3(Random.Range(-4f, 4f), 0f, Random.Range(-4f, 4f)); v.vp = o; }
            if (v.vp != Vector3.zero) WalkTo(v.vp, WalkSpeed, 0.4f, dt);
            return;
        }
        // pick a thing up, carry it to the pile, drop it (the pile grows)
        if (v.prop == null)
        {
            v.prop = new GameObject("ChorePile54");
            Vector3 pp = lp + LeadFwd(v.lead) * -4f + new Vector3(LeadFwd(v.lead).z, 0f, -LeadFwd(v.lead).x) * 3f;
            pp.y = Ground(pp);
            v.prop.transform.position = pp;
            v.vs = 0;
        }
        Vector3 pile = v.prop.transform.position;
        Color c = world == WorldId.Mars ? new Color(0.62f, 0.3f, 0.18f) : world == WorldId.Callisto ? new Color(0.8f, 0.9f, 1f) : world == WorldId.Moon ? new Color(0.55f, 0.55f, 0.56f) : Color.HSVToRGB(Random.value, 0.7f, 0.95f);
        switch (v.vs)
        {
            case 0: v.vp = pile + new Vector3(Random.Range(-7f, 7f), 0f, Random.Range(-7f, 7f)); v.vs = 1; v.vt = 0f; break;
            case 1: if (WalkTo(v.vp, WalkSpeed, 0.4f, dt) < 0.6f || v.vt > 20f) { v.vs = 2; v.vt = 0f; } break;
            case 2: pose = 3; if (v.vt > 1.2f) { v.vs = 3; v.vt = 0f; } break;
            case 3: pose = 2; if (WalkTo(pile, WalkSpeed, 0.9f, dt) < 1.2f || v.vt > 20f) { v.vs = 4; v.vt = 0f; } break;
            case 4:
                pose = 3;
                if (v.vt > 0.8f)
                {
                    int n = v.prop.transform.childCount;
                    if (n < 24)
                    {
                        float a = n * 2.4f, r = 0.15f + 0.05f * n;
                        var g = Mats.Prim(PrimitiveType.Sphere, v.prop.transform, new Vector3(Mathf.Sin(a) * r * 0.6f, 0.12f + n * 0.03f, Mathf.Cos(a) * r * 0.6f), new Vector3(0.32f, 0.22f, 0.28f) * Random.Range(0.8f, 1.3f), Mats.Lit(c));
                        if (world == WorldId.RealRoom) g.layer = RealRoom.Layer;
                    }
                    v.vs = 0;
                }
                break;
        }
    }
}

// the empty drop pod lifts off again (flame on, up and gone)
public class PodLift54 : MonoBehaviour
{
    public float wait = 6f; float t, v;
    void Update()
    {
        t += Time.deltaTime;
        if (t < wait) return;
        var fl = transform.Find("Flame"); if (fl != null && !fl.gameObject.activeSelf) fl.gameObject.SetActive(true);
        v = Mathf.Min(40f, v + 12f * Time.deltaTime);
        transform.position += Vector3.up * v * Time.deltaTime;
        if (Random.value < 0.5f) FX.Smoke(transform.position + Vector3.down * 0.6f, 0.8f, new Color(1f, 0.7f, 0.35f, 0.6f));
        if (t > wait + 9f) Destroy(gameObject);
    }
}

// ffu54 piggyback: the froggy rides on the robot's back and steers it with its own stick
public partial class Frog
{
    [System.NonSerialized] public Robot piggyOf;
    public void StartPiggy(Robot r)
    {
        if (vehicle != null) ExitVehicle();
        LeavePassenger();
        piggyOf = r;
        cc.enabled = false;
        transform.SetParent(r.transform, true);
        PiggyPlace();
    }
    void PiggyPlace()
    {
        transform.position = piggyOf.PiggySeat;
        transform.rotation = piggyOf.transform.rotation;
    }
    public void EndPiggy(bool say)
    {
        Robot r = piggyOf;
        piggyOf = null;
        transform.SetParent(null, true);
        transform.localScale = Vector3.one;
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        Vector3 at = r != null ? r.transform.position + r.transform.right * 1.2f + Vector3.up * 0.4f : transform.position;
        cc.enabled = true;
        Teleport(at);
        if (say && r != null) Toast(r.robotName + ": that was fun!", 1.8f);
    }
    // from Frog.Update: true = riding (input goes to the robot)
    bool PiggyTick(float dt)
    {
        if (piggyOf == null) return false;
        if (piggyOf.visit == null || piggyOf.visit.lead != this) { EndPiggy(false); return false; }
        bool off = human && exitCool <= 0f && (input.hop || input.use);
        piggyOf.PiggyInput(input, camYaw);
        PiggyPlace();
        model.Animate(0f, false, true, false, dt);
        prompt = "Piggyback on " + piggyOf.robotName + " - stick steers, RT runs, " + (inputKind == InputKind.Gamepad ? "A" : inputKind == InputKind.Touch ? "A" : "SPACE / E") + " hops off";
        if (off) { var r = piggyOf; EndPiggy(true); exitCool = 0.4f; if (r.visit != null && r.visit.verb == "piggy") { r.visit.verb = "with"; r.visit.vs = 0; } }
        input = new PIn();
        return true;
    }
}
