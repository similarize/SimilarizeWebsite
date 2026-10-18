using UnityEngine;

// ffu27 AI froggies come to James:
//  * PHONE > CALL (RobotPhone): "Call everyone" or one froggy. An AI froggy in the same world runs over; one in another
//    world jetpack-hops to the caller's world (a puff of smoke + dust, lands a few metres away) and then runs to him.
//    A human player only gets a toast ("James is calling you").
//  * TAG-ALONG (Bill: "when James goes to space, bring the froggies with him ... like in a story"): in free play the AI
//    froggies follow player 1 across worlds. Root cause of them staying behind: Frog.Think (the AI) only ever picked
//    random RANCH wander targets and never looked at worlds at all - an AI froggy only changed world when a launch /
//    landing flow happened to include it (LaunchSeq takes AI froggies within 45 m of the pad; Land takes the ship's
//    passengers). Now, whenever P1 is off the ranch (or the AI froggy is), it rides along as a passenger of the Starship
//    in space, lands with the crew, hops over to P1's world when left behind, and tags along near P1 with a little
//    wander. Back on the ranch both are home and the normal ranch wander takes over again.
//    Off in story mode (the story drives its own crew), for robots, online puppets and the parked REAL ROOM seat.
public partial class Frog
{
    public float CamYaw { get { return camYaw; } }
    public bool demoTop;             // treeclimb demo: climb on up to the branches
    public float Yaw { get { return yaw; } }
    public Frog callTo;              // explicit phone call: run to this froggy
    public float callT;              // seconds left of the call
    public static bool TagOn = true; // free-play tag-along (demo &tag=0 turns it off for the callall shot)
    float hopWorldT = -1f, tagWanderT;
    Vector3 tagOfs;

    public static Frog P1()
    {
        var g = Game.I;
        if (g == null || g.slots.Count == 0) return null;
        int k = g.slots[0].frog;
        return k >= 0 && k < g.frogs.Count ? g.frogs[k] : null;
    }

    // RobotPhone CALL: caller rings `who` (null = everyone)
    public static string Call(Frog caller, Frog who)
    {
        if (Game.I == null || caller == null) return "";
        int n = 0; string last = "";
        foreach (Frog f in Game.I.frogs)
        {
            if (f == null || f == caller || (who != null && f != who)) continue;
            if (!f.gameObject.activeInHierarchy) continue;
            if (f.human || f.netPuppet)
            {
                f.Toast(caller.nick + " is calling you" + (f.world != caller.world ? " from " + (caller.world == WorldId.Space ? "space" : caller.world.ToString()) : "") + "!", 4f);
                n++; last = f.nick; continue;
            }
            if (f.robotPilot != null || f.world == WorldId.RealRoom) continue;   // the story's parked seat / robot bodies
            f.callTo = caller; f.callT = 40f; f.hopWorldT = -1f;
            n++; last = f.nick;
        }
        Sfx.Play(Sfx.Pickup, 0.5f, 1.5f);
        if (n == 0) return "Nobody to call";
        return who == null ? "Calling everyone - the froggies are on their way!" : "Calling " + last + "...";
    }

    Frog TagLead()
    {
        if (!TagOn || Story.Active || Game.I == null || Game.I.state != Game.State.Play) return null;
        Frog p = P1();
        if (p == null || p == this || !p.human) return null;
        if (p.world == WorldId.Ranch && world == WorldId.Ranch) return null;   // both home: normal ranch life
        return p;
    }

    // called first thing in Think; true = it set this AI froggy's input
    bool FollowThink(float dt)
    {
        if (netPuppet || robotPilot != null || human) return false;
        Frog lead = null;
        if (callT > 0f) { callT -= dt; lead = callTo; if (lead == null || lead.world == WorldId.RealRoom) callT = 0f; }
        if (callT <= 0f) { callTo = null; lead = TagLead(); }
        if (lead == null || lead == this || world == WorldId.RealRoom) { hopWorldT = -1f; return false; }
        var i = new PIn();
        input = i; camYaw = 0f;
        if (lead.launching || launching) return true;                       // the ranch blast-off / landing owns them
        if (lead.world == WorldId.RealRoom) return true;                    // wait outside the photoreal room
        // ---- P1 is in space: ride along in the Starship ----
        if (lead.world == WorldId.Space)
        {
            var sw = SpaceWorld.I;
            var ship = sw != null ? sw.ship : null;
            if (ship == null) return true;
            if (passengerOf == ship || vehicle == ship) return true;
            if (lead.vehicle == ship || lead.passengerOf == ship || lead.jetOf != null)
            {
                SendTo(WorldId.Space, ship.transform.position, 0f);
                BoardAsPassenger(ship);
                Debug.Log("FFTAG " + nick + " boarded the Starship with " + lead.nick);
            }
            return true;
        }
        if (passengerOf != null && passengerOf == lead.vehicle) return true;   // riding along in P1's vehicle
        if (vehicle != null) ExitVehicle();
        if (passengerOf != null) LeavePassenger();
        // ---- another world: jetpack hop over after a beat ----
        Vector3 lp = lead.transform.position;
        float dist = world == lead.world ? Vector2.Distance(new Vector2(lp.x, lp.z), new Vector2(transform.position.x, transform.position.z)) : 1e9f;
        if (world != lead.world || dist > 70f)
        {
            if (hopWorldT < 0f) { hopWorldT = 0.8f + id * 0.35f; FX.Smoke(transform.position + Vector3.up * 0.4f, 1.6f, new Color(0.85f, 0.85f, 0.85f, 0.7f)); }
            hopWorldT -= dt;
            if (hopWorldT > 0f) return true;
            hopWorldT = -1f;
            Vector3 spot = SpotNear(lead, 4.5f);
            bool other = world != lead.world;
            SendTo(lead.world, spot, lead.yaw + 180f);
            vel = Vector3.down * 2f;
            FX.Smoke(spot + Vector3.up * 0.4f, 1.8f, new Color(0.9f, 0.9f, 0.9f, 0.7f));
            FX.Dust(spot, 1.2f);
            if (lead.human) Sfx.PlayAt(Sfx.Hop, spot, 0.5f, 30f, 0.8f);
            Debug.Log("FFTAG " + nick + (other ? " jetpacked to " + lead.world : " hopped over") + " near " + lead.nick + " t=" + Time.realtimeSinceStartup.ToString("0.0"));
            if (callT > 0f && lead.human) lead.Toast(nick + " jetpacked over!", 2f);
            return true;
        }
        // ---- same world: run to the caller, then tag along with a little wander ----
        tagWanderT -= dt;
        if (tagWanderT <= 0f || tagOfs == Vector3.zero)
        {
            tagWanderT = Random.Range(3f, 6f);
            float a = (id * 1.9f + Random.Range(-0.7f, 0.7f));
            tagOfs = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(2.2f, 3.6f);
        }
        Vector3 goal = lp + tagOfs;
        Vector3 to = goal - transform.position; to.y = 0f;
        float m = to.magnitude;
        if (callT > 0f && dist < 3.2f) { callT = 0f; callTo = null; if (lead.human) Voice(false, transform.position + Vector3.up * 0.5f, 0.6f); }
        if (m > 1.2f)
        {
            Vector3 d = to / m;
            i.move = new Vector2(d.x, d.z) * (dist > 9f ? 1f : m > 3f ? 0.8f : 0.45f);
            aiHopT -= dt;
            if (aiHopT <= 0f) { aiHopT = Random.Range(1.5f, 4f); i.hop = dist > 9f || Random.value < 0.25f; }
            if (cc != null && cc.isGrounded && planar.magnitude < 0.6f && Random.value < dt * 2f) i.hop = true;   // blocked: hop
        }
        input = i;
        return true;
    }

    // a free spot `r` metres from the leader (behind / beside it), on whatever floor is there
    static Vector3 SpotNear(Frog lead, float r)
    {
        Vector3 lp = lead.transform.position;
        if (lead.vehicle != null) lp = lead.vehicle.transform.position;
        float baseA = (lead.yaw + 180f) * Mathf.Deg2Rad;
        for (int k = 0; k < 10; k++)
        {
            float a = baseA + (k % 2 == 0 ? 1 : -1) * (k / 2) * 0.6f;
            Vector3 c = lp + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * r;
            RaycastHit h;
            if (Physics.Raycast(c + Vector3.up * 4f, Vector3.down, out h, 12f, Vehicle.GroundMask, QueryTriggerInteraction.Ignore)) c.y = h.point.y + 0.6f;
            else c.y = lp.y + 0.6f;
            if (!Physics.CheckCapsule(c + Vector3.up * 0.1f, c + Vector3.up * 0.8f, 0.4f, Vehicle.GroundMask, QueryTriggerInteraction.Ignore)) return c;
        }
        return lp + Vector3.up * 1.2f;
    }
}
