using System.Collections.Generic;
using UnityEngine;

// ffu46 SKATEBOARDS (Bill: "I see a half[-pipe] and looks like a dirt arena. And I think there should be a skateboard that
// the froggies can ride."). Procedural boards (deck + grip tape + kicktails, two trucks, four urethane wheels) in six
// colours: a rack of 4 at the Monster Park half-pipe (north mouth) and 2 at the back of the garage (bay 4 / 5 corner).
// Riding: step on / off like a bike (E / A / tap A on foot; on the board E, pad B / RB, touch OFF steps off - A is the ollie),
// push (kick animation) with RT / W / PUSH, carve with the stick, ollie on A / Space / OLLIE, foot brake LT / S / STOP.
// Physics is the board's own (kinematic body, sub-stepped): the wheels stick to whatever surface is under them (terrain, the
// ffu45 solid berms / park mounds, decks, the half-pipe, the wall of death) by a raycast along the board's normal every
// 10 ms, gravity acts along the rolling direction only (urethane grips sideways), it leaves the surface over a crest when
// gravity can't hold it down (kickers, the coping), lands by a swept sphere. Half-pipe: smooth analytic normals for the
// quarter-pipes, pumping (hold push on the way DOWN a transition adds speed), and past the coping the board goes into
// "vert air" (it stays over the coping line, up and back down into the pipe); stick spins it in the air; land straight
// or fakie, else BAIL. Bails also on a too-hard landing, a landing far off level, or hitting a wall: the froggy tumbles
// off, the board rolls away on its own and stops; walk up to it to get back on. No wheel size (BigWheels.Resizable is false
// for boards - they keep stock wheels). Voice: SynthVoice kind 17 (urethane roll + bearing hum, its own pitch).
// AI froggies now and then walk to the park rack, grab a board and ride the half-pipe for a while (SkateAI).
public class Skateboard : Vehicle
{
    public static readonly List<Skateboard> Boards = new List<Skateboard>();
    public enum Mode { Ground, Air, PipeAir }
    public Mode mode = Mode.Ground;
    public bool rack;                 // one of the 4 at the park rack (AI picks these)
    // state (world space): contact point, velocity, surface normal, rolling direction
    Vector3 pos, vel, n = Vector3.up, head = Vector3.forward, airUp = Vector3.up, hd0;
    float s;                          // signed speed along head (fakie < 0)
    float spin; int pipeSide;         // vert air: degrees turned about the wall normal, which wall (+1 east / -1 west)
    public bool rolling;              // moving on its own (after a bail / stepping off at speed)
    public int pushes, ollies, airs, pipeAirs, bails, landings;
    public float maxAirH, maxSpeed, airT, lastAirT, rideT;
    public string lastSurface = "-", lastBail = "";
    public float kickT, crouchT, ollieCool, bailCool;
    public bool onPipe;               // last ground contact was the half-pipe
    public Frog lastRider;
    Transform deckT, seatT; readonly Transform[] wheelT = new Transform[4]; float wheelSpin;
    SynthVoice voice;
    public System.Func<PIn, PIn> demoHook;
    // AI ride (SkateAI / demo)
    public Frog aiRider; public int aiPhase; public float aiT, aiStuck; public float aiSpinGoal = 180f, aiRunSecs = 40f; public bool aiOllie, aiOllied;

    const float WheelR = 0.028f, DeckY = 0.1f;
    const float PushMax = 7.2f, PumpMax = 11.5f;

    static readonly Color[] DeckCols = { new Color(0.86f, 0.17f, 0.15f), new Color(0.1f, 0.62f, 0.66f), new Color(0.48f, 0.26f, 0.78f), new Color(0.98f, 0.78f, 0.12f), new Color(0.98f, 0.45f, 0.08f), new Color(0.12f, 0.36f, 0.85f) };
    static readonly Color[] WheelCols = { new Color(0.96f, 0.94f, 0.86f), new Color(0.62f, 0.94f, 0.78f), new Color(0.98f, 0.62f, 0.72f), new Color(0.99f, 0.9f, 0.45f), new Color(0.96f, 0.94f, 0.86f), new Color(0.6f, 0.82f, 1f) };
    static readonly string[] ColNames = { "red", "teal", "purple", "yellow", "orange", "blue" };

    public static Vector3 RackC;      // centre of the park rack
    public static float RackYaw = 180f;

    // ---------------- spawn ----------------
    public static void SpawnAll()
    {
        if (Boards.Count > 0) return;
        float gy = MonsterPark.FloorY;
        RackC = new Vector3(MonsterPark.HpX, gy, MonsterPark.HpZ1 + 4.2f);
        BuildRack(RackC);
        for (int i = 0; i < 4; i++)
        {
            Vector3 p = RackC + new Vector3(-1.35f + i * 0.9f, 0f, 0f);
            var b = Spawn("Skateboard", p, RackYaw, i); b.rack = true;
        }
        // two more at the back of the garage, between the Ripsaw (bay 4) and the mech (bay 5)
        float gz = Layout.GarageC.y - Layout.GarageSize.y * 0.5f + 3.4f, gx = Layout.BayX(4) + 3.1f;
        Spawn("Skateboard", new Vector3(gx, Ranch.GY(gx, gz), gz), 0f, 4);
        Spawn("Skateboard", new Vector3(gx + 1.0f, Ranch.GY(gx + 1f, gz), gz), 0f, 5);
        new GameObject("Skate AI").AddComponent<SkateAI>();
        Debug.Log("Skateboard: " + Boards.Count + " boards (4 on the half-pipe rack at " + RackC.ToString("0.0") + ", 2 in the garage at " + new Vector2(gx, gz).ToString("0.0") + ")");
    }

    static Skateboard Spawn(string title, Vector3 p, float yaw, int col)
    {
        var go = new GameObject(title + " (" + ColNames[col] + ")");
        // settle on whatever is under it (garage floor, arena)
        RaycastHit h;
        if (Physics.Raycast(p + Vector3.up * 1.2f, Vector3.down, out h, 4f, GroundMask, QueryTriggerInteraction.Ignore)) p = h.point;
        go.transform.position = p; go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        var b = go.AddComponent<Skateboard>();
        b.Title = "Skateboard"; b.EnterVerb = "ride the " + ColNames[col] + " skateboard";
        b.Build(col);
        b.SetupBodyPublic(4f, new Vector3(0f, 0.065f, 0f), new Vector3(0.22f, 0.11f, 0.82f), new Vector3(0f, 0.06f, 0f));
        b.rb.isKinematic = true; b.rb.interpolation = RigidbodyInterpolation.Interpolate;
        b.rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        b.engineKind = 17; b.enginePitch = 1f;
        b.camDistance = 4.2f; b.camHeight = 1.7f;
        b.pos = p; b.head = go.transform.forward; b.n = Vector3.up;
        if (h.collider != null) { b.n = b.SmoothN(h); b.head = Vector3.ProjectOnPlane(b.head, b.n).normalized; }
        Mats.SetLayer(go, VehicleLayer);
        Boards.Add(b);
        return b;
    }

    void Build(int col)
    {
        Material deck = Mats.Paint(DeckCols[col], 0.75f), ply = Mats.Lit(new Color(0.78f, 0.62f, 0.42f)), grip = Mats.Lit(new Color(0.07f, 0.07f, 0.075f));
        Material alu = Mats.Steel(new Color(0.74f, 0.75f, 0.78f)), dark = Mats.Lit(new Color(0.12f, 0.12f, 0.13f)), ure = Mats.Paint(WheelCols[col], 0.35f), bear = Mats.Steel(new Color(0.55f, 0.56f, 0.6f));
        Material art = Mats.Paint(Color.Lerp(DeckCols[col], Color.white, 0.55f), 0.7f);
        deckT = Mats.Node(transform, "Deck", Vector3.zero);
        Transform D = deckT;
        // deck: flat middle + two kicktails (tilted ~16 deg) + rounded nose / tail; thin ply edge line, grip tape on top
        Mats.Prim(PrimitiveType.Cube, D, new Vector3(0f, DeckY - 0.0125f, 0f), new Vector3(0.21f, 0.022f, 0.56f), deck);
        Mats.Prim(PrimitiveType.Cube, D, new Vector3(0f, DeckY - 0.0125f, 0f), new Vector3(0.212f, 0.006f, 0.56f), ply);
        Mats.Prim(PrimitiveType.Cube, D, new Vector3(0f, DeckY + 0.0005f, 0f), new Vector3(0.2f, 0.004f, 0.55f), grip);
        Mats.Prim(PrimitiveType.Cube, D, new Vector3(0f, DeckY - 0.0245f, 0f), new Vector3(0.12f, 0.002f, 0.36f), art);   // underside graphic
        for (int e = -1; e <= 1; e += 2)
        {
            var tail = Mats.Node(D, e > 0 ? "Nose" : "Tail", new Vector3(0f, DeckY - 0.0125f, e * 0.28f));
            tail.localRotation = Quaternion.Euler(-e * 16f, 0f, 0f);
            Mats.Prim(PrimitiveType.Cube, tail, new Vector3(0f, 0f, e * 0.055f), new Vector3(0.21f, 0.022f, 0.11f), deck);
            Mats.Prim(PrimitiveType.Cube, tail, new Vector3(0f, 0.0125f, e * 0.055f), new Vector3(0.2f, 0.004f, 0.105f), grip);
            Mats.Prim(PrimitiveType.Cylinder, tail, new Vector3(0f, 0f, e * 0.11f), new Vector3(0.21f, 0.011f, 0.13f), deck);
            Mats.Prim(PrimitiveType.Cylinder, tail, new Vector3(0f, 0.0125f, e * 0.11f), new Vector3(0.2f, 0.002f, 0.12f), grip);
        }
        // trucks: baseplate, kingpin + bushing, hanger, axle; wheels with bearings
        int w = 0;
        for (int e = -1; e <= 1; e += 2)
        {
            float z = e * 0.215f;
            Mats.Prim(PrimitiveType.Cube, D, new Vector3(0f, DeckY - 0.03f, z), new Vector3(0.06f, 0.012f, 0.085f), alu);
            Mats.Prim(PrimitiveType.Cylinder, D, new Vector3(0f, DeckY - 0.047f, z - e * 0.012f), new Vector3(0.022f, 0.012f, 0.022f), Mats.Paint(new Color(0.95f, 0.85f, 0.2f), 0.4f));
            Mats.Prim(PrimitiveType.Cube, D, new Vector3(0f, DeckY - 0.058f, z), new Vector3(0.05f, 0.03f, 0.03f), alu);
            Mats.Prim(PrimitiveType.Cylinder, D, new Vector3(0f, WheelR, z), new Vector3(0.022f, 0.085f, 0.022f), new Vector3(0f, 0f, 90f), alu);   // hanger
            Mats.Prim(PrimitiveType.Cylinder, D, new Vector3(0f, WheelR, z), new Vector3(0.008f, 0.11f, 0.008f), new Vector3(0f, 0f, 90f), dark);  // axle
            for (int sx = -1; sx <= 1; sx += 2)
            {
                var wt = Mats.Node(D, "Wheel", new Vector3(sx * 0.098f, WheelR, z));
                Mats.Prim(PrimitiveType.Cylinder, wt, Vector3.zero, new Vector3(WheelR * 2f, 0.016f, WheelR * 2f), new Vector3(0f, 0f, 90f), ure);
                Mats.Prim(PrimitiveType.Cylinder, wt, new Vector3(sx * 0.002f, 0f, 0f), new Vector3(WheelR * 1.05f, 0.0165f, WheelR * 1.05f), new Vector3(0f, 0f, 90f), bear);
                Mats.Prim(PrimitiveType.Cube, wt, new Vector3(sx * 0.0085f, 0f, 0f), new Vector3(0.002f, WheelR * 1.6f, 0.006f), dark);   // spin marker
                wheelT[w++] = wt;
            }
        }
        seatT = Mats.Node(transform, "Seat", new Vector3(0f, DeckY + 0.004f, -0.02f));
        seat = seatT; seatScale = 0.82f;
        if (Look.Mobile) Mats.NoShadows(gameObject);
    }

    // the park rack: a low wooden frame with 4 slots at the half-pipe's north mouth (visual only: boards roll out freely)
    static void BuildRack(Vector3 c)
    {
        var root = new GameObject("Skateboard rack").transform;
        root.position = c; root.rotation = Quaternion.Euler(0f, RackYaw, 0f);
        Material wood = Mats.Lit(new Color(0.6f, 0.44f, 0.27f)), dark = Mats.Lit(new Color(0.3f, 0.22f, 0.14f));
        for (int s = -1; s <= 1; s += 2)
            Mats.Prim(PrimitiveType.Cube, root, new Vector3(0f, 0.04f, s * 0.48f), new Vector3(3.9f, 0.08f, 0.08f), wood);   // fore / aft rails
        for (int i = 0; i <= 4; i++)
        {
            float x = -1.8f + i * 0.9f;
            Mats.Prim(PrimitiveType.Cube, root, new Vector3(x, 0.17f, -0.48f), new Vector3(0.06f, 0.34f, 0.08f), wood);      // back posts
            Mats.Prim(PrimitiveType.Cube, root, new Vector3(x, 0.09f, -0.1f), new Vector3(0.03f, 0.18f, 0.7f), dark);          // slot dividers
        }
        Mats.Prim(PrimitiveType.Cube, root, new Vector3(0f, 0.36f, -0.48f), new Vector3(3.9f, 0.06f, 0.1f), wood);             // top bar
        // ffu46b: one-line board beside the rack (east), readable from the north (the two-line 0.9 m board best-fit to garbage)
        Vector3 sc = c + new Vector3(3.1f, 1.05f, 0f);
        Ranch.Sign(sc, 0f, "SKATEBOARDS", new Color(0.12f, 0.32f, 0.55f), 2.4f, 0.6f);
        var legs = new GameObject("Skateboard sign legs").transform;
        for (int s = -1; s <= 1; s += 2)
            Mats.Prim(PrimitiveType.Cylinder, legs, new Vector3(sc.x + s * 1.0f, (c.y + sc.y - 0.3f) * 0.5f, sc.z + 0.06f), new Vector3(0.06f, (sc.y - 0.3f - c.y) * 0.5f, 0.06f), dark);   // sign legs (no collider)
    }

    // ---------------- controls / help ----------------
    public override string[] TouchSet { get { return new[] { "OLLIE", null, "OFF", "PUSH", "STOP" }; } }
    public override string HelpLine { get { return "Skateboard: L-stick carve | RT push (kick) | A ollie | LT foot brake | hold RT going down a ramp = pump | stick spins in the air | B / RB step off"; } }
    public override string HelpFor(InputKind k)
    {
        if (k == InputKind.Gamepad) return "L-stick::carve (lean to steer)|RT::push (kick)|A::ollie|LT::foot brake|Hold RT going down::pump the half-pipe - air out of the coping|L-stick in the air::spin it - land straight (or fakie) or bail!|B  /  RB::step off";
        if (k == InputKind.Touch) return "Stick::carve|PUSH::push (kick)|OLLIE::ollie|STOP::foot brake|Hold PUSH going down::pump the half-pipe - air out of the coping|Stick in the air::spin - land straight or bail!|OFF::step off";
        return "A  /  D::carve|W::push (kick)|Space::ollie|S::foot brake|Hold W going down::pump the half-pipe - air out of the coping|A  /  D in the air::spin - land straight (or fakie) or bail!|E::step off";
    }
    public override string HudStatus
    {
        get
        {
            string m = mode == Mode.PipeAir ? "  AIR " + Mathf.Max(0f, pos.y - CopingY).ToString("0.0") + " m" : mode == Mode.Air ? "  air" : (s < -0.5f ? "  fakie" : "");
            return "Skateboard  " + Mathf.RoundToInt(Mathf.Abs(s) * 3.6f) + " km/h" + m;
        }
    }
    // step off: E (keys), B / RB / Y (pad), OFF (touch), right-click; the pad / touch A is the ollie, not "get off"
    public bool WantsOff(PIn i) { return (i.use && !i.hop) || i.cargo || i.alt; }

    public override void OnEnter()
    {
        rolling = false; lastRider = driver; rideT = 0f; bailCool = 0.3f;
        if (mode == Mode.Ground && Mathf.Abs(s) < 0.3f) s = 0f;
    }
    public override void OnExit()
    {
        base.OnExit();
        if (Mathf.Abs(s) > 0.4f || mode != Mode.Ground) rolling = true;   // stepped off moving: it rolls on by itself
        kickT = 0f; crouchT = 0f; aiRider = null;
    }

    // ---------------- physics ----------------
    static float CopingY { get { return MonsterPark.FloorY + MonsterPark.HpR + MonsterPark.HpVert; } }
    public float AirOverCoping { get { return pos.y - CopingY; } }
    public float SignedSpeed { get { return s; } }
    public Vector3 Normal { get { return n; } }
    public Vector3 Head { get { return head; } }
    public float SpinDeg { get { return spin; } }

    Vector3 SmoothN(RaycastHit h)
    {
        if (h.collider == null || h.collider.name != "Half-pipe") return h.normal;
        // analytic half-pipe normals (MonsterPark.HalfPipe cross-section): flat, quarter pipe R about (+-half, R), vertical, deck
        float half = MonsterPark.HpHalf, R = MonsterPark.HpR;
        float u = h.point.x - MonsterPark.HpX, y = h.point.y - MonsterPark.FloorY, au = Mathf.Abs(u), sg = Mathf.Sign(u);
        if (au <= half + 0.01f) return Vector3.up;
        if (y < R - 0.005f && au < half + R)
        {
            Vector2 d = new Vector2(sg * half - u, R - y);
            if (d.sqrMagnitude < 1e-4f) return h.normal;
            d.Normalize();
            return new Vector3(d.x, d.y, 0f);
        }
        if (y < R + MonsterPark.HpVert - 0.005f && au > half + R - 0.08f) return new Vector3(-sg, 0f, 0f);
        return h.normal.y > 0.5f ? Vector3.up : h.normal;
    }

    bool InPipeBand(Vector3 p) { return p.z > MonsterPark.HpZ0 + 0.2f && p.z < MonsterPark.HpZ1 - 0.2f && Mathf.Abs(p.x - MonsterPark.HpX) < MonsterPark.HpHalf + MonsterPark.HpR + MonsterPark.HpDeck; }

    protected override void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        PIn ip = driver != null ? inp : new PIn();
        if (demoHook != null && driver != null) ip = demoHook(ip);
        if (aiRider != null && driver == aiRider) ip = SkateAI.Ride(this, dt);
        bool ridden = driver != null;
        if (ridden) rideT += dt;
        kickT = Mathf.Max(0f, kickT - dt); crouchT = Mathf.Max(0f, crouchT - dt); ollieCool -= dt; bailCool -= dt;
        bool active = ridden || rolling || mode != Mode.Ground;
        if (active)
        {
            int ns = Mathf.Max(1, Mathf.CeilToInt(dt / 0.01f));
            float h = dt / ns;
            bool hop = ip.hop;
            for (int k = 0; k < ns; k++)
            {
                if (driver == null && ridden) break;   // bailed in this step
                if (mode == Mode.Ground) GroundStep(h, ip, ridden && driver != null, hop && k == 0);
                else if (mode == Mode.PipeAir) PipeAirStep(h, ip, ridden && driver != null);
                else AirStep(h, ip, ridden && driver != null);
            }
            if (!ridden && rolling && mode == Mode.Ground && Mathf.Abs(s) < 0.12f && n.y > 0.8f) { rolling = false; s = 0f; }
            if (pos.y < -40f || OutOfWorld(pos)) Home();
            Vector3 up = mode == Mode.Air ? airUp : n;
            Vector3 f = Vector3.ProjectOnPlane(head, up); if (f.sqrMagnitude < 1e-4f) f = Vector3.ProjectOnPlane(transform.forward, up);
            Quaternion q = Quaternion.LookRotation(f.normalized, up);
            if (mode == Mode.PipeAir) q = Quaternion.AngleAxis(spin, n) * Quaternion.LookRotation(Vector3.ProjectOnPlane(hd0, n).normalized, n);
            rb.MovePosition(pos); rb.MoveRotation(q);
            kinVel = vel;
        }
        else { kinVel = Vector3.zero; vel = Vector3.zero; }
        maxSpeed = Mathf.Max(maxSpeed, ridden ? vel.magnitude : 0f);
        Voice(dt, ridden);
    }

    void GroundStep(float dt, PIn ip, bool ridden, bool hopNow)
    {
        Vector3 g = Physics.gravity;
        float steer = ridden ? Mathf.Clamp(ip.move.x, -1f, 1f) : 0f;
        float push = ridden ? Mathf.Max(ip.gas, ip.move.y) : 0f, brake = ridden ? Mathf.Max(ip.brake, -ip.move.y) : 0f;
        head = Vector3.ProjectOnPlane(head, n); if (head.sqrMagnitude < 1e-6f) head = Vector3.ProjectOnPlane(Vector3.forward, n); head.Normalize();
        // gravity along the rolling direction only (the wheels grip sideways)
        s += Vector3.Dot(g, head) * dt;
        float sp = Mathf.Abs(s);
        s = Mathf.MoveTowards(s, 0f, (0.16f + 0.0032f * sp * sp) * dt);   // rolling resistance + air
        if (brake > 0.1f) s = Mathf.MoveTowards(s, 0f, 5f * brake * dt);
        if (!ridden) s = Mathf.MoveTowards(s, 0f, 1.1f * dt);   // ffu46b: a loose board slows down sooner (ffu46a rolled 30 m)
        // push (kick) on flat-ish ground, below push speed
        if (ridden && push > 0.3f && n.y > 0.9f && kickT <= 0f && s > -0.6f && s < PushMax)
        {
            kickT = 0.6f; pushes++;
            s = Mathf.Min(PushMax, Mathf.Max(s, 0f) + 1.55f);
        }
        // pumping: hold push on the way DOWN a transition (the half-pipe / wall of death / any steep curve)
        if (ridden && push > 0.3f && n.y < 0.985f && n.y > 0.05f && Vector3.Dot(head * s, g) > 0f && sp < PumpMax) s += Mathf.Sign(s) * 2.8f * dt;
        // carve
        float yr = steer * Mathf.Min(2.6f, 7.5f / Mathf.Max(1f, sp)) * (n.y < 0.5f ? 0.6f : 1f);
        if (Mathf.Abs(yr) > 1e-4f) head = Quaternion.AngleAxis(yr * dt * Mathf.Rad2Deg, n) * head;
        // stalled on a steep wall: swing round to the fall line and roll back down
        if (n.y < 0.35f && sp < 0.6f)
        {
            Vector3 fall = Vector3.ProjectOnPlane(Vector3.down, n);
            if (fall.sqrMagnitude > 1e-4f) { fall.Normalize(); Vector3 want = Vector3.Dot(head, fall) >= 0f ? fall : -fall; head = Vector3.Slerp(head, want, Mathf.Min(1f, 10f * dt)).normalized; }
        }
        // ollie
        if (hopNow && ridden && ollieCool <= 0f)
        {
            ollieCool = 0.45f; ollies++; crouchT = 0.35f;
            vel = head * s + n * 4.3f;
            mode = Mode.Air; airUp = n; airT = 0f;
            Sfx.PlayAt(Sfx.Thud, pos, 0.35f, 30f, 1.9f);
            return;
        }
        vel = head * s;
        Vector3 p1 = pos + vel * dt;
        // walls / obstacles ahead (a steep face relative to the surface we ride on)
        float vm = vel.magnitude;
        if (vm > 0.05f)
        {
            RaycastHit wh;
            if (Physics.SphereCast(pos + n * 0.32f, 0.16f, vel / vm, out wh, vm * dt + 0.06f, GroundMask, QueryTriggerInteraction.Ignore) && Vector3.Dot(wh.normal, n) < 0.55f)
            {
                float into = -Vector3.Dot(vel, wh.normal);
                if (into > 0f)
                {
                    if (ridden && into > 4.5f && bailCool <= 0f) { Bail("hit the " + Nice(wh.collider)); return; }
                    vel -= wh.normal * into * (ridden ? 1f : 1.5f);
                    vel = Vector3.ProjectOnPlane(vel, n);
                    if (!ridden && vel.sqrMagnitude > 1e-4f) head = Vector3.Dot(vel, head) >= 0f ? vel.normalized : -vel.normalized;
                    s = Vector3.Dot(vel, head); vel = head * s;
                    p1 = pos + vel * dt;
                }
            }
        }
        // stick to the surface under the wheels
        RaycastHit h;
        Vector3 o = p1 + n * 0.5f;
        if (Physics.Raycast(o, -n, out h, 0.5f + 0.35f, GroundMask, QueryTriggerInteraction.Ignore))
        {
            float d = h.distance - 0.5f;   // > 0: the surface falls away (crest), < 0: rises (concave / bump)
            Vector3 n2 = SmoothN(h);
            float hold = Mathf.Max(0f, -Vector3.Dot(g, n));
            if (d > 0.004f && vm > 2f && Vector3.Dot(n2 - n, vel / vm) > 0.02f && 2f * d / (dt * dt) > hold + 4f)
            {
                // over a crest faster than gravity can follow: airborne
                pos = p1; mode = Mode.Air; airUp = n; airT = 0f;
                return;
            }
            pos = h.point;
            onPipe = h.collider.name == "Half-pipe";
            lastSurface = h.collider.name;
            n = Vector3.Slerp(n, n2, 0.85f).normalized;
            Vector3 nh = Vector3.ProjectOnPlane(head, n);
            if (nh.sqrMagnitude > 1e-6f) head = nh.normalized;
            vel = head * s;
        }
        else
        {
            pos = p1;
            float u = pos.x - MonsterPark.HpX;
            if (onPipe && Mathf.Abs(u) > MonsterPark.HpHalf + MonsterPark.HpR - 0.3f && pos.y > CopingY - 0.5f && vel.y > 0.2f && InPipeBand(pos) && Mathf.Abs(n.y) < 0.3f)
            {
                // out of the coping: vert air
                mode = Mode.PipeAir; pipeSide = u > 0f ? 1 : -1; spin = 0f; hd0 = head; airT = 0f;
                n = new Vector3(-pipeSide, 0f, 0f);
                vel = new Vector3(0f, vel.y, vel.z);
                pipeAirs++;
            }
            else { mode = Mode.Air; airUp = n; airT = 0f; }
        }
    }

    void PipeAirStep(float dt, PIn ip, bool ridden)
    {
        float steer = ridden ? Mathf.Clamp(ip.move.x, -1f, 1f) : 0f;
        airT += dt;
        vel.y += Physics.gravity.y * dt; vel.x = 0f;
        pos += vel * dt;
        pos.x = MonsterPark.HpX + pipeSide * (MonsterPark.HpHalf + MonsterPark.HpR - 0.01f);
        spin += steer * 330f * dt;
        if (ridden) maxAirH = Mathf.Max(maxAirH, pos.y - CopingY);
        if (!InPipeBand(pos)) { mode = Mode.Air; airUp = Quaternion.AngleAxis(spin, n) * n; head = Quaternion.AngleAxis(spin, n) * hd0; return; }
        if (vel.y < 0f && pos.y < CopingY - 0.04f)
        {
            // back onto the vertical wall
            n = new Vector3(-pipeSide, 0f, 0f);
            pos.x = MonsterPark.HpX + pipeSide * (MonsterPark.HpHalf + MonsterPark.HpR);
            Vector3 hh = Vector3.ProjectOnPlane(Quaternion.AngleAxis(spin, n) * hd0, n).normalized;
            float c = Vector3.Dot(hh, vel.normalized);
            lastAirT = airT; airs++;
            if (ridden && Mathf.Abs(c) < 0.6f && bailCool <= 0f) { mode = Mode.Ground; head = hh; s = Vector3.Dot(vel, hh); Bail("landed sideways out of the air (" + Mathf.RoundToInt(Mathf.Repeat(spin, 360f)) + " deg)"); return; }
            head = hh; s = Vector3.Dot(vel, head); mode = Mode.Ground; onPipe = true; landings++;
            if (ridden && driver != null && driver.human) driver.Toast((Mathf.Abs(Mathf.DeltaAngle(spin, 0f)) > 120f ? "180 AIR! " : "AIR! ") + Mathf.Max(0.1f, maxAirRun).ToString("0.0") + " m over the coping", 1.3f);
            Sfx.PlayAt(Sfx.Thud, pos, 0.45f, 35f, 1.5f);
            maxAirRun = 0f;
        }
        else maxAirRun = Mathf.Max(maxAirRun, pos.y - CopingY);
    }
    float maxAirRun;

    void AirStep(float dt, PIn ip, bool ridden)
    {
        float steer = ridden ? Mathf.Clamp(ip.move.x, -1f, 1f) : 0f;
        airT += dt;
        vel += Physics.gravity * dt;
        if (Mathf.Abs(steer) > 0.05f) head = Quaternion.AngleAxis(steer * 270f * dt, airUp) * head;
        airUp = Vector3.Slerp(airUp, Vector3.up, Mathf.Min(1f, 1.4f * dt)).normalized;
        head = Vector3.ProjectOnPlane(head, airUp).normalized;
        float vm = vel.magnitude;
        RaycastHit h;
        if (vm > 1e-3f && Physics.SphereCast(pos + airUp * 0.2f, 0.12f, vel / vm, out h, vm * dt + 0.09f, GroundMask, QueryTriggerInteraction.Ignore))
        {
            Vector3 n2 = SmoothN(h);
            float vn = -Vector3.Dot(vel, n2);
            Vector3 vt = Vector3.ProjectOnPlane(vel, n2);
            Vector3 hh = Vector3.ProjectOnPlane(head, n2); hh = hh.sqrMagnitude > 1e-6f ? hh.normalized : vt.normalized;
            bool stunt = RallyTrack.IsStunt(h.collider.gameObject.layer);
            lastAirT = airT;
            if (ridden && bailCool <= 0f)
            {
                string why = null;
                if (Vector3.Angle(airUp, n2) > 55f && !(stunt && Vector3.Angle(airUp, n2) < 80f)) why = "landed way off level";
                else if (n2.y < 0.25f && !stunt) why = "hit the " + Nice(h.collider);
                else if (vn > 10.5f) why = "landed too hard (" + vn.ToString("0.0") + " m/s)";
                else if (vt.magnitude > 1.5f && Mathf.Abs(Vector3.Dot(hh, vt.normalized)) < 0.6f) why = "landed sideways";
                if (why != null) { pos = h.point; n = n2; head = hh; s = Vector3.Dot(vt, hh); mode = Mode.Ground; Bail(why); return; }
            }
            pos = h.point; n = n2; head = hh; mode = Mode.Ground;
            s = Vector3.Dot(vt, hh) * 0.95f;
            if (!ridden && Mathf.Abs(s) < 0.2f && vt.magnitude > 0.5f) { head = vt.normalized; s = vt.magnitude * 0.9f; }
            onPipe = h.collider.name == "Half-pipe"; lastSurface = h.collider.name;
            if (airT > 0.25f) { airs++; landings++; Sfx.PlayAt(Sfx.Thud, pos, Mathf.Clamp01(vn / 8f) * 0.5f + 0.15f, 35f, 1.6f); }
            vel = head * s;
            return;
        }
        pos += vel * dt;
    }

    static string Nice(Collider c) { string nm = c != null ? c.name : "wall"; if (nm.Length > 28) nm = nm.Substring(0, 28); return nm.ToLowerInvariant(); }

    // bail: the rider tumbles off, the board rolls away on its own
    public void Bail(string why)
    {
        Frog f = driver;
        bails++; lastBail = why; bailCool = 0.6f;
        Vector3 v0 = vel;
        Debug.Log("Skateboard: " + (f != null ? f.nick : "?") + " BAILED (" + why + ") at " + pos.ToString("0.0") + " v " + v0.magnitude.ToString("0.0") + " m/s, mode " + mode + ", spin " + Mathf.RoundToInt(spin));
        FX.Cloud(pos + Vector3.up * 0.3f, 0.5f, 6, new Color(0.62f, 0.52f, 0.38f, 0.55f));
        Sfx.PlayAt(Sfx.Pick(Sfx.Crash), pos, 0.35f, 40f, 1.5f);
        // the board shoots out the way it was going, a little off line
        Vector3 bh = Quaternion.AngleAxis(Random.Range(-35f, 35f), n) * head;
        head = Vector3.ProjectOnPlane(bh, n).normalized;
        s = Mathf.Clamp(Vector3.Dot(v0, head) * 0.8f + Mathf.Sign(Vector3.Dot(v0, head)) * 1.2f, -9f, 9f);
        if (mode == Mode.PipeAir) mode = Mode.Air;
        rolling = true;
        if (f != null)
        {
            f.ExitVehicle();
            f.Knock(v0 * 0.55f + Vector3.up * 3.5f);
            var tb = f.GetComponent<FrogTumble>(); if (tb == null) tb = f.gameObject.AddComponent<FrogTumble>();
            tb.Go(f, 1.1f);
            if (f.human) f.Toast("BAIL! " + why + " - walk up to the board to get back on", 2.2f);
        }
    }

    public void Home()
    {
        if (driver != null) driver.ExitVehicle();
        Vector3 p = HomePos;
        RaycastHit h;
        if (Physics.Raycast(p + Vector3.up * 1.2f, Vector3.down, out h, 4f, GroundMask, QueryTriggerInteraction.Ignore)) p = h.point;
        pos = p; vel = Vector3.zero; s = 0f; n = Vector3.up; head = spawnRot * Vector3.forward; mode = Mode.Ground; rolling = false;
        rb.position = p; rb.rotation = spawnRot; transform.SetPositionAndRotation(p, spawnRot);
    }
    public bool AtHome { get { return (pos - HomePos).magnitude < 1.2f && !rolling && mode == Mode.Ground; } }

    void Voice(float dt, bool ridden)
    {
        if (voice == null) voice = new SynthVoice(gameObject, 17, enginePitch);
        float gk = 1f;
        if (driver == null || !driver.human) { gk = Mathf.Clamp01(1f - Sfx.Near(transform.position) / 45f); gk *= gk; }
        voice.gain = gk;
        bool on = ridden || rolling || mode != Mode.Ground;
        voice.Tick(on, Mathf.Abs(mode == Mode.Ground ? s : vel.magnitude), kickT > 0f ? 1f : 0f, onPipe ? 0f : 1f, mode == Mode.Ground ? 1f : 0f, dt);
    }
    public string VoiceLevels { get { return voice != null ? voice.Levels() : "-"; } }

    // ---------------- visuals: wheels + rider pose ----------------
    void Update()
    {
        float dt = Time.deltaTime;
        wheelSpin += (mode == Mode.Ground ? s : s * 0.98f) / WheelR * Mathf.Rad2Deg * dt;
        foreach (var w in wheelT) if (w != null) w.localRotation = Quaternion.Euler(wheelSpin, 0f, 0f);
        // the deck tips up a little on the tail during an ollie pop
        if (deckT != null) deckT.localRotation = Quaternion.Euler(crouchT > 0.2f ? -12f * (crouchT - 0.2f) / 0.15f : 0f, 0f, 0f);
    }

    void LateUpdate()
    {
        if (driver == null) return;
        var m = driver.model;
        if (m == null || m.bob == null) return;
        // skate stance: body turned ~35 deg toward the toe side, head to the nose, knees soft; kick = back leg swings,
        // ollie = crouch then tuck, air = tucked with arms out, carve = lean into the turn
        if (seatT != null) seatT.localRotation = Quaternion.Euler(0f, -35f, 0f);
        float steerLean = driver.vehicle == this ? Mathf.Clamp(inp.move.x, -1f, 1f) : 0f;
        float t = Time.time;
        bool air = mode != Mode.Ground;
        float crouch = crouchT > 0f ? 1f : air ? 0.7f : 0.15f + Mathf.Clamp01(Mathf.Abs(s) / 12f) * 0.25f;
        m.bob.localPosition = new Vector3(0f, -0.12f * crouch, 0f);
        m.bob.localRotation = Quaternion.Euler(8f + crouch * 14f, 0f, -steerLean * 14f);
        float legBend = -20f - crouch * 40f;
        if (m.legL != null) m.legL.localRotation = Quaternion.Euler(legBend, 0f, -14f);
        if (m.legR != null)
        {
            if (kickT > 0f && !air) { float ph = 1f - kickT / 0.6f; m.legR.localRotation = Quaternion.Euler(-10f + 75f * Mathf.Sin(ph * Mathf.PI), 0f, 18f); m.bob.localPosition += new Vector3(0f, -0.06f * Mathf.Sin(ph * Mathf.PI), 0f); }
            else m.legR.localRotation = Quaternion.Euler(legBend, 0f, 14f);
        }
        float arm = air ? -70f : -25f - Mathf.Clamp01(Mathf.Abs(s) / 8f) * 20f;
        if (m.armL != null) m.armL.localRotation = Quaternion.Euler(arm + Mathf.Sin(t * 3f) * 4f, 0f, -35f);
        if (m.armR != null) m.armR.localRotation = Quaternion.Euler(arm - Mathf.Sin(t * 3f) * 4f, 0f, 35f);
        if (m.head != null) m.head.localRotation = Quaternion.Euler(0f, 30f, 0f);
    }
}

// ffu46: the froggy tumbling off after a bail (spins the body for a moment; the knock-back throws it)
public class FrogTumble : MonoBehaviour
{
    Frog f; float t, t0; Vector3 ax;
    public bool Active { get { return t > 0f; } }
    public void Go(Frog frog, float secs) { f = frog; t = t0 = secs; ax = Random.value < 0.5f ? Vector3.right : Vector3.forward; }
    void LateUpdate()
    {
        if (t <= 0f || f == null || f.model == null || f.model.bob == null) return;
        t -= Time.deltaTime;
        if (f.vehicle != null) { t = 0f; return; }
        float k = 1f - Mathf.Clamp01(t / t0);
        f.model.bob.localRotation = Quaternion.AngleAxis(k * 540f, ax) * f.model.bob.localRotation;
        f.model.bob.localPosition = new Vector3(0f, 0.25f * Mathf.Sin(k * Mathf.PI), 0f);
    }
}

// AI froggies now and then walk to the half-pipe rack, grab a board and ride the half-pipe for ~40 s, then roll out, step
// off; boards left out go back to their spot once nobody is near them for 20 s.
public class SkateAI : MonoBehaviour
{
    public static SkateAI I;
    float t = 30f;
    public Skateboard target; public Frog goer; float walkT;
    readonly Dictionary<Skateboard, float> idle = new Dictionary<Skateboard, float>();
    public static int Rides;
    public static bool Paused;   // demo: no random rides
    void Awake() { I = this; }

    void Update()
    {
        var g = Game.I;
        if (g == null || g.state != Game.State.Play) return;
        float dt = Time.deltaTime;
        if (goer != null)
        {
            walkT += dt;
            if (!goer.AiIdleOnRanch || target == null || target.driver != null || walkT > 75f || Story.Active) { goer = null; target = null; }
            else
            {
                goer.RcInvite(target.transform.position + target.transform.right * 0.9f);
                if (Flat(goer.transform.position - target.transform.position) < 2.2f)
                {
                    goer.EnterVehicle(target);
                    target.aiRider = goer; target.aiPhase = 0; target.aiT = 0f; target.aiStuck = 0f; target.aiSpinGoal = 180f; target.aiRunSecs = 40f; target.aiOllie = false;
                    Rides++;
                    Debug.Log("SkateAI: " + goer.nick + " grabs the " + target.name + " at the half-pipe");
                    goer = null; target = null;
                }
            }
        }
        foreach (var b in Skateboard.Boards)
        {
            if (b == null) continue;
            float it; idle.TryGetValue(b, out it);
            if (b.driver != null || b.AtHome) { idle[b] = 0f; continue; }
            it = Sfx.Near(b.transform.position) > 30f ? it + dt : 0f;
            if (it > 20f) { it = 0f; b.Home(); }
            idle[b] = it;
        }
        t -= dt;
        if (t > 0f || goer != null || Story.Active || Paused) return;
        t = Random.Range(35f, 70f);
        foreach (var b in Skateboard.Boards) if (b.aiRider != null) return;   // one AI skater at a time
        if (Random.value > 0.5f) return;
        Send(null);
    }

    // pick (or use the given) idle froggy and send it to a free rack board; returns the froggy or null
    public Frog Send(Frog who)
    {
        var g = Game.I; if (g == null) return null;
        if (who == null)
        {
            var cands = new List<Frog>();
            foreach (Frog f in g.frogs) if (f != null && f.AiIdleOnRanch && Flat(f.transform.position - Skateboard.RackC) < 170f) cands.Add(f);
            if (cands.Count == 0) return null;
            who = cands[Random.Range(0, cands.Count)];
        }
        Skateboard pick = null; float bd = 1e9f;
        foreach (var b in Skateboard.Boards)
        {
            if (b == null || !b.rack || b.driver != null || !b.AtHome) continue;
            float d = Flat(b.transform.position - who.transform.position);
            if (d < bd) { bd = d; pick = b; }
        }
        if (pick == null) return null;
        goer = who; target = pick; walkT = 0f;
        Debug.Log("SkateAI: " + who.nick + " heads for the half-pipe rack (" + pick.name + ", " + bd.ToString("0") + " m)");
        return who;
    }

    static float Flat(Vector3 d) { d.y = 0f; return d.magnitude; }

    // the AI's riding: roll out of the rack into the pipe, pump side to side (airs + 180s), roll out north, stop, step off
    public static PIn Ride(Skateboard b, float dt)
    {
        var o = new PIn();
        Frog f = b.aiRider;
        b.aiT += dt;
        Vector3 p = b.transform.position;
        float u = p.x - MonsterPark.HpX;
        Vector3 n = b.Normal, head = b.Head;
        float s = b.SignedSpeed;
        bool inPipe = p.z < MonsterPark.HpZ1 - 1.5f && p.z > MonsterPark.HpZ0 + 1f && Mathf.Abs(u) < MonsterPark.HpHalf + MonsterPark.HpR + 0.5f;
        Vector3 want;
        if (b.aiPhase == 0)
        {
            // out of the rack and down the middle of the pipe to z -106
            Vector3 tgt = new Vector3(MonsterPark.HpX + 0.4f, p.y, -106f);
            want = tgt - p; want.y = 0f;
            o.gas = s < 4.5f ? 1f : 0f;
            if (p.z < -104f) { b.aiPhase = 1; b.aiT = 0f; }
        }
        else if (b.aiPhase == 1 || n.y < 0.95f)
        {
            // ffu46b: also while rolling out (phase 2) but still on a wall - steering for the exit on a wall turned the board
            // along the wall, and a 180 out of the coping then landed it sideways (Jimmy's ffu46a bail at spin 175)
            // runs: across the pipe (+x first), drift kept between z -121 and -104
            // keep the board's travel in the pipe's cross-section (x / y) with a small z drift; slow on the flat = head east
            Vector3 v = head * s;
            Vector3 wv = new Vector3(v.x, v.y, 0f);
            if (wv.magnitude < 0.8f) wv = new Vector3(n.y > 0.95f ? (Mathf.Abs(u) < 0.6f ? 1f : -Mathf.Sign(u)) : -Mathf.Sign(u), 0f, 0f);
            wv.Normalize();
            float zb = p.z < -119f ? 0.22f : p.z > -105f ? -0.22f : (Mathf.Repeat(b.aiT, 20f) < 10f ? -0.06f : 0.06f);
            want = wv + new Vector3(0f, 0f, zb);
            o.gas = 1f;
            if (b.aiPhase == 1 && b.aiT > b.aiRunSecs && n.y > 0.95f) b.aiPhase = 2;
        }
        else
        {
            // roll out of the north end, stop, step off
            want = new Vector3(MonsterPark.HpX, 0f, MonsterPark.HpZ1 + 6f) - new Vector3(p.x, 0f, p.z);
            if (p.z > MonsterPark.HpZ1 + 2f) { o.brake = 1f; want = Vector3.ProjectOnPlane(head * Mathf.Sign(s + 0.01f), Vector3.up); }
            else o.gas = s < 4f ? 1f : 0f;
            if (p.z > MonsterPark.HpZ1 + 2f && Mathf.Abs(s) < 0.4f) { b.aiRider = null; if (f != null && f.vehicle == b) f.ExitVehicle(); return o; }
        }
        if (b.mode == Skateboard.Mode.PipeAir)
        {
            // spin toward the goal (180 = lands forward; the demo's 90 = bail)
            float sp = Mathf.Abs(b.SpinDeg);
            o.move.x = sp < b.aiSpinGoal - 8f ? 1f : 0f;
            o.gas = 0f;
            return o;
        }
        if (b.mode == Skateboard.Mode.Air) { o.gas = 0f; return o; }
        Vector3 tw = Vector3.ProjectOnPlane(want, n);
        if (tw.sqrMagnitude > 1e-4f)
        {
            tw.Normalize();
            Vector3 hw = s >= -0.3f ? tw : -tw;   // fakie: the nose points back up the way we came
            float ang = Vector3.SignedAngle(head, hw, n);
            o.move.x = Mathf.Clamp(ang / 22f, -1f, 1f);
        }
        if (b.aiOllie && !b.aiOllied && b.aiPhase == 0 && n.y > 0.98f && s > 2.6f && b.aiT > 0.8f) { o.hop = true; b.aiOllied = true; }
        if (f == null || f.vehicle != b) b.aiRider = null;
        if (Mathf.Abs(s) < 0.2f && o.gas > 0.5f && n.y > 0.9f && b.kickT <= 0f) b.aiStuck += dt; else b.aiStuck = 0f;
        if (b.aiStuck > 4f || b.aiT > b.aiRunSecs + 50f) { b.aiStuck = 0f; b.aiRider = null; if (f != null && f.vehicle == b) f.ExitVehicle(); }
        return o;
    }
}
