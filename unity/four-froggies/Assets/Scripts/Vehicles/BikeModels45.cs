using UnityEngine;

// ffu45: FIVE NEW MOTORCYCLES in the trailer barn (Bill: "add 5 new rideable motorcycles of different kinds to the trailer
// barn"). Same Bike physics as the ffu30 garage bikes (two centre-line springs, upright body + leaning model, grip-limited
// yaw, wheelies, air control, soft crashes, ffu42/44 big-wheel code incl. the 4x stability fixes, per-vehicle wheel size
// ffu45), with per-type geometry + handling:
//   4 SPORT BIKE "Superbike": red full fairing, clip-ons, fat rear tyre, fastest (40 m/s), leans to 56 deg, sharp but
//     grip-limited steering, little wheelie; 4-stroke voice pitched high (1.55, screaming inline-four).
//   5 CRUISER "Chopper": long raked fork (front axle 1.15 m ahead), low seat, ape-hanger bars, chrome V-twin, fishtail
//     pipes; heavy (290 kg), slow to turn, leans only 30 deg, no wheelies; deep 4-stroke (0.6).
//   6 ADVENTURE "Adventure dual-sport": tall long-travel suspension, big knobbly front, beak + tall screen, panniers, crash
//     bars; extra damping + hill-climb assist (keeps its drive up slopes) = best off-road; 4-stroke 0.85.
//   7 TRIALS "Supermoto trials": tiny, light (85 kg), huge wheelie torque up to 16 m/s, balances wheelies to ~60 deg
//     (brake drops the nose), HOP on X / Space / click / HOP button; 2-stroke voice pitched up (1.3).
//   8 SIDECAR "Cyber sidecar e-bike": stainless faceted Cybertruck-style bike + angular sidecar on the right (3rd wheel,
//     light bars), seats 2: the rider + one froggy in the chair ("ride in the sidecar" hotspot when someone is riding);
//     the rig does not lean, pulls a little toward the chair under power; electric voice (1.05).
// Parked nose-out in a neat row along the back of the barn (barn-local z -5, x -13 .. -3.4): behind the Silver Bullet and
// the Teardrop, clear of the four hitch lanes (front) and the two toy-hauler ramp feet (bays 2 + 3). They load into the
// toy haulers like the garage bikes (the sidecar rig takes the whole bay), robots can drive them (any Vehicle), and
// BikeAI froggies near the barn take one out: down the lane between the trailers, out of the doors, two laps south of
// the barn, hop off; it goes home after 20 s with nobody near.
public partial class Bike
{
    public Frog passenger;                 // type 8 sidecar
    Transform seat2, scWheel; float scR = 0.3f, scSpin; Hotspot scSpot;
    public Vector2[] aiExit; public Vector2 aiLoopC; public float aiLoopR = 10f; public int aiWp;
    public static readonly string[] BarnNames = { "Superbike", "Chopper bike", "Adventure bike", "Trials bike", "Cyber sidecar e-bike" };
    public static readonly float[] BarnX = { -13f, -10.6f, -8.2f, -5.9f, -3.5f };
    public const float BarnZ = -5f;
    public static int Hops;

    static void SpawnBarn()
    {
        if (!TrailerBarn.Built) { Debug.Log("Bike: no trailer barn - barn bikes skipped"); return; }
        for (int i = 0; i < 5; i++)
        {
            Vector3 p = TrailerBarn.W(BarnX[i], 0.15f, BarnZ);
            p.y = TrailerBarn.FloorY + 0.15f;
            Bike b = Spawn(BarnNames[i], p, TrailerBarn.Yaw, 4 + i);
            Vector3 w0 = TrailerBarn.W(-6.9f, 0f, -2.5f), w1 = TrailerBarn.W(-6.9f, 0f, 9f), w2 = TrailerBarn.W(-6.9f, 0f, 15f), lc = TrailerBarn.W(0f, 0f, 25f);
            b.aiExit = new[] { new Vector2(w0.x, w0.z), new Vector2(w1.x, w1.z), new Vector2(w2.x, w2.z) };
            b.aiLoopC = new Vector2(lc.x, lc.z); b.aiLoopR = 9f;
        }
        Debug.Log("Bike: 5 barn bikes parked at the back of the trailer barn (" + TrailerBarn.Centre.ToString("0") + ")");
    }

    static Bike Spawn45(string title, Vector3 pos, float yaw, int type)
    {
        var go = new GameObject(title);
        go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        var b = go.AddComponent<Bike>();
        b.type = type; b.Title = title; b.EnterVerb = "ride the " + title.ToLower();
        // rest, front r, rear r, front z, rear z, mass, maxSpeed, accel, latCap, turnRate, lean, wheelieK, wheelieV, wheelieMax, kind, pitch
        float rest, rf, rr, zf, zr, mass;
        Vector3 bc = new Vector3(0f, 0.68f, 0f), bs = new Vector3(0.34f, 0.46f, 1.45f), com = new Vector3(0f, 0.5f, 0f);
        switch (type)
        {
            case 4: rest = 0.12f; rf = 0.30f; rr = 0.31f; zf = 0.72f; zr = -0.70f; mass = 185f; b.maxSpeed = 40f; b.accel = 15f; b.latCap = 18f; b.turnRate = 1.75f; b.leanMax = 56f; b.wheelieK = 14f; b.wheelieV = 9f; b.wheelieMax = 22f; b.engineKind = 16; b.enginePitch = 1.55f; b.camDistance = 4.8f; b.camHeight = 1.8f; break;
            case 5: rest = 0.10f; rf = 0.34f; rr = 0.34f; zf = 1.15f; zr = -0.82f; mass = 290f; b.maxSpeed = 27f; b.accel = 7.5f; b.latCap = 10f; b.turnRate = 1.35f; b.leanMax = 30f; b.wheelieK = 0f; b.engineKind = 16; b.enginePitch = 0.6f; b.camDistance = 5.4f; b.camHeight = 2.0f;
                bc = new Vector3(0f, 0.62f, 0.15f); bs = new Vector3(0.4f, 0.42f, 1.95f); com = new Vector3(0f, 0.42f, 0.1f); break;
            case 6: rest = 0.25f; rf = 0.40f; rr = 0.37f; zf = 0.80f; zr = -0.74f; mass = 225f; b.maxSpeed = 31f; b.accel = 10.5f; b.latCap = 12.5f; b.turnRate = 1.9f; b.leanMax = 42f; b.wheelieK = 18f; b.wheelieV = 11f; b.wheelieMax = 28f; b.slopeAssist = 0.6f; b.driftK = 0.8f; b.engineKind = 16; b.enginePitch = 0.85f; b.camDistance = 5f; b.camHeight = 2.1f;
                bc = new Vector3(0f, 0.8f, 0f); bs = new Vector3(0.4f, 0.5f, 1.5f); break;
            case 7: rest = 0.20f; rf = 0.33f; rr = 0.32f; zf = 0.64f; zr = -0.62f; mass = 85f; b.maxSpeed = 25f; b.accel = 13f; b.latCap = 13f; b.turnRate = 2.5f; b.leanMax = 46f; b.wheelieK = 44f; b.wheelieV = 16f; b.wheelieMax = 62f; b.canHop = true; b.engineKind = 14; b.enginePitch = 1.3f; b.camDistance = 4.4f; b.camHeight = 1.9f;
                bs = new Vector3(0.32f, 0.44f, 1.3f); break;
            default: rest = 0.14f; rf = 0.33f; rr = 0.33f; zf = 0.72f; zr = -0.70f; mass = 260f; b.maxSpeed = 28f; b.accel = 10f; b.latCap = 12f; b.turnRate = 1.6f; b.leanMax = 0f; b.wheelieK = 0f; b.engineKind = 15; b.enginePitch = 1.05f; b.camDistance = 5.6f; b.camHeight = 2.3f;
                bc = new Vector3(0.42f, 0.62f, -0.05f); bs = new Vector3(1.3f, 0.5f, 1.6f); com = new Vector3(0.2f, 0.45f, 0f); break;
        }
        b.rest = rest; b.fw.r = rf; b.rw.r = rr;
        b.fw.mount = new Vector3(0f, 0.58f * rest + rf, zf);
        b.rw.mount = new Vector3(0f, 0.58f * rest + rr, zr);
        b.fw.dist = rest + rf; b.rw.dist = rest + rr;
        b.SetupBodyPublic(mass, bc, bs, com);
        b.rb.angularDrag = type == 5 ? 2f : 1.2f;
        b.Build45();
        float perWheel = b.rb.mass * 9.81f / 2f;
        b.spring = perWheel / (b.rest * 0.42f);
        b.damper = 2f * Mathf.Sqrt(b.spring * b.rb.mass / 2f) * (type == 6 ? 0.6f : 0.45f);
        Mats.SetLayer(go, VehicleLayer);
        AllBikes.Add(b);
        if (type == 8) b.SidecarSpot();
        return b;
    }

    string Help45
    {
        get
        {
            switch (type)
            {
                case 4: return Title + ": L-stick steer (leans hard) | RT throttle (fastest bike) | LT brake | A get off";
                case 5: return Title + ": L-stick steer (heavy, wide turns) | RT throttle | LT brake | A get off";
                case 6: return Title + ": L-stick steer | RT throttle (climbs hills, soaks up rough ground) | LT brake | in the air stick pitches | A get off";
                case 7: return Title + ": L-stick steer | RT throttle (hard = wheelie, hold it to balance) | LT brake (drops the nose) | X hop | A get off";
                default: return Title + ": L-stick steer | RT throttle | LT brake | a friend can ride in the sidecar | A get off";
            }
        }
    }

    // ---------------- handling extras ----------------
    void Extra45(PIn ip, float dt, bool driven, Vector3 fwd, float fs, float pitchUp)
    {
        if (type < 4 || !driven) return;
        if (slopeAssist > 0f && throttle > 0.2f)
        {
            float gAlong = Vector3.Dot(Physics.gravity, fwd);   // < 0 going uphill
            if (gAlong < 0f) rb.AddForce(-fwd * gAlong * slopeAssist * throttle, ForceMode.Acceleration);
        }
        if (type == 7)
        {
            hopCool -= dt;
            bool hop = ip.fire || (ip.hop && !ip.use);
            if (hop && hopCool <= 0f && (fw.grounded || rw.grounded))
            {
                hopCool = 0.6f; Hops++;
                rb.AddForce(transform.up * 5.2f, ForceMode.VelocityChange);
                rb.AddTorque(-transform.right * 3f, ForceMode.VelocityChange);
                FX.Dust(transform.position, 0.6f);
                Sfx.PlayAt(Sfx.Hop, transform.position, 0.45f, 30f, 0.8f);
            }
            // wheelie balance: in a wheelie the throttle holds it, the brake brings the nose down
            if (!fw.grounded && rw.grounded && pitchUp > 8f)
            {
                if (ip.brake > 0.3f) rb.AddTorque(transform.right * 18f * ip.brake, ForceMode.Acceleration);
                else if (throttle > 0.4f && pitchUp > 45f) rb.AddTorque(transform.right * (pitchUp - 45f) * 1.6f, ForceMode.Acceleration);
            }
        }
        if (type == 8 && rw.grounded && Mathf.Abs(throttle) > 0.2f && fs > 2f)
            rb.AddTorque(transform.up * 0.35f * throttle, ForceMode.Acceleration);   // the chair drags: pulls toward it under power
    }

    void Pose45(FrogModel m)
    {
        float legX = -38f, reach = -70f, bob = 12f, spread = 8f;
        switch (type)
        {
            case 4: legX = -55f; reach = -50f; bob = 34f; break;          // tucked in
            case 5: legX = -78f; reach = -112f; bob = -10f; spread = 14f; break;   // feet forward, arms up to the apes, laid back
            case 6: legX = -32f; reach = -72f; bob = 6f; break;           // upright
            case 7: legX = -14f; reach = -82f; bob = 22f; break;          // standing on the pegs
            case 8: legX = -42f; reach = -66f; bob = 10f; break;
        }
        if (m.legL != null) m.legL.localRotation = Quaternion.Euler(legX, 0f, -spread);
        if (m.legR != null) m.legR.localRotation = Quaternion.Euler(legX, 0f, spread);
        if (m.armL != null) m.armL.localRotation = Quaternion.Euler(reach - steer * 6f, 0f, 0f);
        if (m.armR != null) m.armR.localRotation = Quaternion.Euler(reach + steer * 6f, 0f, 0f);
        if (m.bob != null) m.bob.localRotation = Quaternion.Euler(bob + Mathf.Clamp(-accLocal.z, -6f, 6f), 0f, 0f);
    }

    // ---------------- sidecar ----------------
    void SidecarSpot()
    {
        scSpot = Interact.Add(transform.position, 2.6f, "ride in the sidecar", f => SidecarIn(f));
        scSpot.enabled = f => f != null && f.vehicle == null && f.passengerOf == null && driver != null && driver != f && passenger == null && !stowed && f.world == WorldId.Ranch;
    }
    public bool SidecarIn(Frog f)
    {
        if (type != 8 || f == null || passenger != null || seat2 == null || stowed) return false;
        passenger = f;
        f.BoardAsPassenger(this);
        f.RideSeat(seat2, seatScale);
        if (f.human) f.Toast("In the sidecar" + (driver != null ? " with " + driver.nick : "") + " - A / E / OUT hops out", 2.2f);
        Sfx.Play(Sfx.Click, 0.5f, 1.2f);
        Debug.Log("Bike: " + f.nick + " rides in the sidecar of the " + Title);
        return true;
    }
    public void SidecarOut(Frog f)
    {
        if (f == null) return;
        if (passenger == f) passenger = null;
        if (f.passengerOf == this) f.LeavePassenger();
        Vector3 p = transform.TransformPoint(new Vector3(1.9f, 0f, -0.2f));
        RaycastHit h;
        if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out h, 8f, GroundMask, QueryTriggerInteraction.Ignore)) p.y = h.point.y + 0.05f; else p.y = Ranch.GY(p.x, p.z);
        f.SendTo(f.world, p, transform.eulerAngles.y);
    }

    void Update45(float dt)
    {
        if (scSpot != null) scSpot.pos = transform.position + transform.right * 1.0f;
        if (passenger != null)
        {
            if (passenger.passengerOf != this) passenger = null;
            else if (stowed || move3) SidecarOut(passenger);
            else passenger.RideSeat(seat2, seatScale);
        }
        if (scWheel != null)
        {
            // the chair wheel follows the ground (visual), sized like the bike's tyres
            float k = fw.r / Mathf.Max(0.01f, fw.r0 > 0f ? fw.r0 : fw.r);
            float r = scR * k;
            Vector3 top = transform.TransformPoint(new Vector3(0.98f, 1.2f + bigLift, -0.1f));
            RaycastHit h; float y;
            if (Physics.Raycast(top, -transform.up, out h, 3f + bigLift * 2f, GroundMask, QueryTriggerInteraction.Ignore)) y = transform.InverseTransformPoint(h.point).y + r;
            else y = r - 0.05f;
            y = Mathf.Clamp(y, r - 0.25f, r + 0.25f + bigLift);
            scSpin += ForwardSpeed / Mathf.Max(0.05f, r) * Mathf.Rad2Deg * dt;
            scWheel.localPosition = new Vector3(0.98f + (k - 1f) * 0.3f, y, -0.1f);
            scWheel.localRotation = Quaternion.Euler(scSpin, 0f, 0f);
            scWheel.localScale = Vector3.one * k;
        }
    }

    // ---------------- models ----------------
    void Build45()
    {
        Color pc = type == 4 ? new Color(0.85f, 0.08f, 0.1f) : type == 5 ? new Color(0.08f, 0.08f, 0.1f) : type == 6 ? new Color(0.92f, 0.5f, 0.1f) : type == 7 ? new Color(0.95f, 0.95f, 0.96f) : new Color(0.74f, 0.76f, 0.78f);
        Material paint = type == 8 ? Mats.PBR(pc, 0.75f, 0.9f) : Mats.Paint(pc, 0.9f), black = Mats.Lit(new Color(0.07f, 0.07f, 0.08f));
        Material alu = Mats.Steel(new Color(0.7f, 0.72f, 0.76f)), chrome = Mats.PBR(new Color(0.85f, 0.85f, 0.87f), 0.9f, 0.95f), tyre = Mats.Lit(new Color(0.05f, 0.05f, 0.055f));
        Material gold = Mats.PBR(new Color(0.88f, 0.66f, 0.22f), 0.7f, 0.8f), seatM = Mats.Paint(type == 5 ? new Color(0.25f, 0.13f, 0.07f) : new Color(0.1f, 0.1f, 0.11f), 0.3f);
        Material glow = Mats.Unlit(type == 8 ? new Color(0.85f, 0.95f, 1f) : new Color(1f, 0.95f, 0.85f)), red = Mats.Unlit(new Color(1f, 0.1f, 0.1f));
        Material accent = Mats.Paint(type == 4 ? new Color(0.95f, 0.95f, 0.95f) : type == 6 ? new Color(0.15f, 0.15f, 0.17f) : type == 7 ? new Color(0.1f, 0.45f, 0.95f) : new Color(0.3f, 0.3f, 0.32f), 0.8f);
        leanT = Mats.Node(transform, "Lean", Vector3.zero);
        Transform L = leanT;
        float headY = 1.0f, headZ = 0.48f, seatY = 0.9f, seatZ = -0.2f;
        bool knobby = type == 6 || type == 7;
        switch (type)
        {
            case 4:   // superbike: full fairing, tank hump, tail, clip-ons, screen
                headY = 0.98f; headZ = 0.5f; seatY = 0.86f; seatZ = -0.22f;
                Mats.Prim(PrimitiveType.Sphere, L, new Vector3(0f, 0.8f, 0.42f), new Vector3(0.44f, 0.56f, 1.05f), paint);
                Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.62f, 0.15f), new Vector3(0.4f, 0.24f, 0.7f), paint);   // belly pan
                Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.97f, 0.08f), new Vector3(0.32f, 0.17f, 0.42f), paint);   // tank
                for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, L, new Vector3(s * 0.205f, 0.78f, 0.42f), new Vector3(0.012f, 0.06f, 0.7f), new Vector3(0f, 0f, 0f), accent);   // side stripe
                { var scr = Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 1.1f, 0.68f), new Vector3(0.26f, 0.2f, 0.02f), Mats.Paint(new Color(0.1f, 0.15f, 0.2f), 0.95f)); scr.transform.localRotation = Quaternion.Euler(-48f, 0f, 0f); }
                Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.9f, -0.24f), new Vector3(0.24f, 0.07f, 0.36f), seatM);
                { var tail = Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 1.0f, -0.58f), new Vector3(0.2f, 0.12f, 0.42f), paint); tail.transform.localRotation = Quaternion.Euler(-14f, 0f, 0f); }
                for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, L, new Vector3(s * 0.08f, 0.84f, 0.95f), new Vector3(0.1f, 0.05f, 0.02f), glow);   // twin headlights
                Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 1.04f, -0.8f), new Vector3(0.14f, 0.03f, 0.02f), red);
                { var ex = Mats.Prim(PrimitiveType.Cylinder, L, new Vector3(0.14f, 0.5f, -0.45f), new Vector3(0.1f, 0.22f, 0.1f), Mats.Steel(new Color(0.3f, 0.3f, 0.33f))); ex.transform.localRotation = Quaternion.Euler(-70f, 0f, 0f); }
                Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.5f, 0.05f), new Vector3(0.28f, 0.26f, 0.34f), black);   // engine
                break;
            case 5:   // chopper: low seat, teardrop tank, V-twin, apes, fishtails, long fork
                headY = 1.08f; headZ = 0.58f; seatY = 0.7f; seatZ = -0.34f;
                { var fr = Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.86f, 0.12f), new Vector3(0.06f, 0.06f, 1.0f), chrome); fr.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f); }
                Mats.Prim(PrimitiveType.Sphere, L, new Vector3(0f, 0.98f, 0.2f), new Vector3(0.28f, 0.22f, 0.6f), paint);   // tank
                Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.98f, 0.2f), new Vector3(0.05f, 0.23f, 0.4f), Mats.Paint(new Color(0.85f, 0.45f, 0.08f), 0.9f));   // flame stripe
                Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.68f, -0.32f), new Vector3(0.3f, 0.08f, 0.42f), seatM);
                Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.82f, -0.55f), new Vector3(0.24f, 0.22f, 0.06f), seatM);   // backrest
                for (int s = -1; s <= 1; s += 2)
                {
                    var cyl = Mats.Prim(PrimitiveType.Cylinder, L, new Vector3(0f, 0.6f, s * 0.12f), new Vector3(0.17f, 0.2f, 0.17f), chrome);   // V-twin
                    cyl.transform.localRotation = Quaternion.Euler(s * 30f, 0f, 0f);
                    for (int k = 0; k < 3; k++) { var fin = Mats.Prim(PrimitiveType.Cylinder, cyl.transform.parent, cyl.transform.localPosition + cyl.transform.localRotation * new Vector3(0f, 0.04f + k * 0.07f, 0f), new Vector3(0.22f, 0.012f, 0.22f), black); fin.transform.localRotation = cyl.transform.localRotation; }
                    var pipe = Mats.Prim(PrimitiveType.Cylinder, L, new Vector3(0.2f, 0.4f + (s + 1) * 0.06f, -0.45f), new Vector3(0.07f, 0.55f, 0.07f), chrome);   // fishtails
                    pipe.transform.localRotation = Quaternion.Euler(-86f, 0f, 0f);
                }
                Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.45f, -0.05f), new Vector3(0.22f, 0.18f, 0.32f), chrome);   // crankcase
                Mats.Prim(PrimitiveType.Sphere, L, new Vector3(0f, 1.06f, 0.78f), new Vector3(0.2f, 0.2f, 0.16f), chrome);   // round headlight
                Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 1.06f, 0.86f), new Vector3(0.12f, 0.12f, 0.02f), glow);
                { var rf2 = Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.8f, -0.82f), new Vector3(0.24f, 0.03f, 0.55f), paint); rf2.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f); }
                Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.86f, -1.08f), new Vector3(0.12f, 0.04f, 0.02f), red);
                break;
            case 6:   // adventure: tall, beak, tall screen, big tank, panniers, crash bars, skid plate
                headY = 1.22f; headZ = 0.52f; seatY = 1.06f; seatZ = -0.22f;
                { var sp = Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.95f, 0.18f), new Vector3(0.07f, 0.07f, 0.9f), black); sp.transform.localRotation = Quaternion.Euler(-22f, 0f, 0f); }
                Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 1.12f, 0.2f), new Vector3(0.42f, 0.24f, 0.46f), paint);   // big tank
                Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 1.1f, -0.3f), new Vector3(0.28f, 0.08f, 0.6f), seatM);
                Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 1.16f, -0.72f), new Vector3(0.36f, 0.04f, 0.32f), alu);   // rack
                for (int s = -1; s <= 1; s += 2)
                {
                    Mats.Prim(PrimitiveType.Cube, L, new Vector3(s * 0.33f, 0.88f, -0.62f), new Vector3(0.16f, 0.36f, 0.48f), Mats.Lit(new Color(0.2f, 0.21f, 0.22f)));   // panniers
                    Mats.Prim(PrimitiveType.Cube, L, new Vector3(s * 0.415f, 0.88f, -0.62f), new Vector3(0.01f, 0.3f, 0.42f), alu);
                    var cb = Mats.Prim(PrimitiveType.Cylinder, L, new Vector3(s * 0.24f, 0.75f, 0.32f), new Vector3(0.035f, 0.22f, 0.035f), black);   // crash bars
                    cb.transform.localRotation = Quaternion.Euler(30f, 0f, s * 15f);
                }
                { var bk = Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 1.05f, 0.82f), new Vector3(0.2f, 0.05f, 0.38f), accent); bk.transform.localRotation = Quaternion.Euler(18f, 0f, 0f); }   // beak
                { var scr = Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 1.42f, 0.62f), new Vector3(0.3f, 0.36f, 0.02f), Mats.Paint(new Color(0.15f, 0.2f, 0.25f), 0.95f)); scr.transform.localRotation = Quaternion.Euler(-30f, 0f, 0f); }
                Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 1.2f, 0.72f), new Vector3(0.22f, 0.12f, 0.06f), accent);
                for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, L, new Vector3(s * 0.06f, 1.2f, 0.755f), new Vector3(0.07f, 0.06f, 0.02f), glow);
                Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.6f, 0.05f), new Vector3(0.3f, 0.3f, 0.38f), alu);   // twin engine
                Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.42f, 0.08f), new Vector3(0.3f, 0.04f, 0.5f), alu);   // skid plate
                Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 1.1f, -0.92f), new Vector3(0.14f, 0.03f, 0.02f), red);
                break;
            case 7:   // trials / supermoto: tiny, no real seat, big skid plate, numbered plate
                headY = 1.08f; headZ = 0.44f; seatY = 0.98f; seatZ = -0.24f;
                { var sp = Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.82f, 0.15f), new Vector3(0.06f, 0.06f, 0.78f), Mats.Paint(new Color(0.95f, 0.3f, 0.05f), 0.8f)); sp.transform.localRotation = Quaternion.Euler(-26f, 0f, 0f); }
                Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.96f, 0.12f), new Vector3(0.22f, 0.1f, 0.3f), paint);   // tank cover
                Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.96f, -0.34f), new Vector3(0.16f, 0.05f, 0.44f), seatM);   // thin seat pad
                { var tail = Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 1.0f, -0.62f), new Vector3(0.18f, 0.02f, 0.4f), accent); tail.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f); }
                Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.55f, 0.02f), new Vector3(0.22f, 0.22f, 0.24f), alu);
                Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.4f, 0.05f), new Vector3(0.24f, 0.03f, 0.42f), accent);   // skid plate
                { var ex = Mats.Prim(PrimitiveType.Capsule, L, new Vector3(0.12f, 0.6f, 0.2f), new Vector3(0.09f, 0.18f, 0.09f), chrome); ex.transform.localRotation = Quaternion.Euler(65f, 0f, 0f); }
                { var sil = Mats.Prim(PrimitiveType.Cylinder, L, new Vector3(0.15f, 0.85f, -0.55f), new Vector3(0.08f, 0.16f, 0.08f), alu); sil.transform.localRotation = Quaternion.Euler(-76f, 0f, 0f); }
                break;
            default:  // cyber sidecar e-bike: faceted stainless body, light bars, battery slab, angular chair
                headY = 1.0f; headZ = 0.5f; seatY = 0.92f; seatZ = -0.2f;
                { var w1 = Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.86f, 0.18f), new Vector3(0.36f, 0.22f, 1.2f), paint); w1.transform.localRotation = Quaternion.Euler(-7f, 0f, 0f); }
                { var w2 = Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.97f, 0.5f), new Vector3(0.34f, 0.12f, 0.55f), paint); w2.transform.localRotation = Quaternion.Euler(14f, 0f, 0f); }
                Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.6f, 0.0f), new Vector3(0.3f, 0.3f, 0.7f), black);   // battery slab
                Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.62f, 0.0f), new Vector3(0.305f, 0.02f, 0.6f), glow);
                Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.92f, 0.86f), new Vector3(0.34f, 0.025f, 0.02f), glow);   // front light bar
                Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.98f, -0.62f), new Vector3(0.3f, 0.025f, 0.02f), red);     // rear light bar
                Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.92f, -0.28f), new Vector3(0.26f, 0.07f, 0.44f), seatM);
                { var tail = Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.95f, -0.55f), new Vector3(0.3f, 0.08f, 0.2f), paint); tail.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f); }
                // the chair: a faceted wedge on the right on two struts
                Transform sc = Mats.Node(L, "Sidecar", new Vector3(0.98f, 0f, -0.05f));
                Mats.Prim(PrimitiveType.Cube, sc, new Vector3(0f, 0.55f, 0f), new Vector3(0.72f, 0.3f, 1.5f), paint);
                { var nose = Mats.Prim(PrimitiveType.Cube, sc, new Vector3(0f, 0.6f, 0.82f), new Vector3(0.7f, 0.18f, 0.5f), paint); nose.transform.localRotation = Quaternion.Euler(24f, 0f, 0f); }
                { var cowl = Mats.Prim(PrimitiveType.Cube, sc, new Vector3(0f, 0.78f, 0.34f), new Vector3(0.66f, 0.1f, 0.5f), paint); cowl.transform.localRotation = Quaternion.Euler(-10f, 0f, 0f); }
                Mats.Prim(PrimitiveType.Cube, sc, new Vector3(0f, 0.72f, -0.28f), new Vector3(0.5f, 0.06f, 0.7f), seatM);   // chair seat
                Mats.Prim(PrimitiveType.Cube, sc, new Vector3(0f, 0.92f, -0.62f), new Vector3(0.5f, 0.36f, 0.08f), seatM);   // backrest
                Mats.Prim(PrimitiveType.Cube, sc, new Vector3(0f, 0.66f, 1.06f), new Vector3(0.6f, 0.02f, 0.02f), glow);
                Mats.Prim(PrimitiveType.Cube, sc, new Vector3(0.37f, 0.6f, -0.72f), new Vector3(0.02f, 0.03f, 0.2f), red);
                for (int k = -1; k <= 1; k += 2) Mats.Prim(PrimitiveType.Cube, L, new Vector3(0.5f, 0.45f, k * 0.45f), new Vector3(0.7f, 0.05f, 0.05f), alu);   // struts
                scWheel = Mats.Node(L, "Sidecar wheel", new Vector3(0.98f, scR, -0.1f));
                Mats.Prim(PrimitiveType.Cylinder, scWheel, Vector3.zero, new Vector3(scR * 2f, 0.06f, scR * 2f), new Vector3(0f, 0f, 90f), tyre);
                Mats.Prim(PrimitiveType.Cylinder, scWheel, Vector3.zero, new Vector3(scR * 1.6f, 0.065f, scR * 1.6f), new Vector3(0f, 0f, 90f), alu);
                seat2 = Mats.Node(sc, "Sidecar seat", new Vector3(0f, 0.72f, -0.25f));
                break;
        }
        // swingarm + rear shock
        swing = Mats.Node(L, "Swingarm", new Vector3(0f, 0.55f, -0.1f));
        for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, swing, new Vector3(s * 0.08f, 0f, -0.32f), new Vector3(0.03f, 0.06f, 0.64f), type == 5 ? chrome : alu);
        rshock = Mats.Node(L, "Shock", Vector3.zero);
        Mats.Prim(PrimitiveType.Cylinder, rshock, Vector3.zero, new Vector3(0.05f, 1f, 0.05f), type == 5 ? chrome : gold);
        shockTop = new Vector3(0f, type == 5 ? 0.7f : type == 6 ? 1.08f : seatY - 0.02f, type == 5 ? -0.4f : -0.22f);
        // steering head, bars, forks
        headT = Mats.Node(L, "Head", new Vector3(0f, headY, headZ));
        bars = Mats.Node(headT, "Bars", Vector3.zero);
        if (type == 5)
        {
            // ape hangers: two risers up + a cross bar high
            for (int s = -1; s <= 1; s += 2)
            {
                Mats.Prim(PrimitiveType.Cylinder, bars, new Vector3(s * 0.2f, 0.22f, -0.08f), new Vector3(0.025f, 0.22f, 0.025f), chrome);
                Mats.Prim(PrimitiveType.Cylinder, bars, new Vector3(s * 0.3f, 0.44f, -0.14f), new Vector3(0.035f, 0.08f, 0.035f), new Vector3(0f, 0f, 90f), black);
            }
            Mats.Prim(PrimitiveType.Cylinder, bars, new Vector3(0f, 0.44f, -0.12f), new Vector3(0.025f, 0.26f, 0.025f), new Vector3(0f, 0f, 90f), chrome);
        }
        else
        {
            float bw = type == 4 ? 0.24f : type == 6 ? 0.4f : type == 7 ? 0.42f : 0.32f, by = type == 4 ? -0.02f : 0.1f;
            Mats.Prim(PrimitiveType.Cylinder, bars, new Vector3(0f, by, -0.04f), new Vector3(0.025f, bw, 0.025f), new Vector3(0f, 0f, 90f), black);
            for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cylinder, bars, new Vector3(s * (bw - 0.04f), by, -0.04f), new Vector3(0.035f, 0.06f, 0.035f), new Vector3(0f, 0f, 90f), black);
        }
        Mats.Prim(PrimitiveType.Cube, bars, new Vector3(0f, 0.02f, 0f), new Vector3(0.2f, 0.05f, 0.06f), type == 5 ? chrome : alu);
        if (type == 7)
        {
            var plate = Mats.Prim(PrimitiveType.Cube, bars, new Vector3(0f, -0.06f, 0.1f), new Vector3(0.24f, 0.22f, 0.02f), Mats.Paint(new Color(0.95f, 0.95f, 0.96f), 0.6f));
            plate.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
            Mats.Prim(PrimitiveType.Cube, bars, new Vector3(0f, -0.06f, 0.113f), new Vector3(0.1f, 0.12f, 0.004f), accent);
            var ff = Mats.Prim(PrimitiveType.Cube, bars, new Vector3(0f, -0.38f, 0.16f), new Vector3(0.15f, 0.02f, 0.46f), accent);
            ff.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
        }
        if (type == 6) { var ff = Mats.Prim(PrimitiveType.Cube, bars, new Vector3(0f, -0.5f, 0.2f), new Vector3(0.17f, 0.02f, 0.5f), accent); ff.transform.localRotation = Quaternion.Euler(8f, 0f, 0f); }
        forkL = Mats.Node(L, "ForkL", Vector3.zero); forkR = Mats.Node(L, "ForkR", Vector3.zero);
        foreach (var fk in new[] { forkL, forkR }) Mats.Prim(PrimitiveType.Cylinder, fk, Vector3.zero, new Vector3(type == 5 ? 0.045f : 0.055f, 1f, type == 5 ? 0.045f : 0.055f), type == 4 ? gold : chrome);
        // wheels
        foreach (var w in new[] { fw, rw })
        {
            bool f = w == fw;
            w.vis = Mats.Node(L, f ? "Front wheel" : "Rear wheel", w.mount - Vector3.up * (rest * 0.6f));
            float wd = type == 4 ? (f ? 0.13f : 0.22f) : type == 5 ? (f ? 0.1f : 0.24f) : type == 8 ? 0.16f : (f ? 0.1f : 0.13f);
            Mats.Prim(PrimitiveType.Cylinder, w.vis, Vector3.zero, new Vector3(w.r * 2f, wd * 0.5f, w.r * 2f), new Vector3(0f, 0f, 90f), tyre);
            Material rim = type == 4 ? Mats.Paint(new Color(0.95f, 0.75f, 0.1f), 0.8f) : type == 5 ? chrome : type == 8 ? Mats.Lit(new Color(0.15f, 0.15f, 0.17f)) : alu;
            Mats.Prim(PrimitiveType.Cylinder, w.vis, Vector3.zero, new Vector3(w.r * 1.5f, wd * 0.52f, w.r * 1.5f), new Vector3(0f, 0f, 90f), rim);
            if (type == 8)
            {
                // aero disc wheel with a glowing ring
                Mats.Prim(PrimitiveType.Cylinder, w.vis, Vector3.zero, new Vector3(w.r * 1.42f, wd * 0.56f, w.r * 1.42f), new Vector3(0f, 0f, 90f), paint);
                Mats.Prim(PrimitiveType.Cylinder, w.vis, Vector3.zero, new Vector3(w.r * 0.5f, wd * 0.58f, w.r * 0.5f), new Vector3(0f, 0f, 90f), glow);
            }
            else
            {
                Mats.Prim(PrimitiveType.Cylinder, w.vis, Vector3.zero, new Vector3(w.r * 1.4f, wd * 0.54f, w.r * 1.4f), new Vector3(0f, 0f, 90f), black);
                int sp = type == 4 ? 3 : type == 5 ? 12 : 8;
                for (int k = 0; k < sp; k++)
                    Mats.Prim(PrimitiveType.Cube, w.vis, Vector3.zero, new Vector3(0.012f, w.r * 1.38f, type == 4 ? 0.05f : 0.008f), new Vector3(k * 180f / sp, 0f, 0f), type == 4 ? rim : chrome);
            }
            Mats.Prim(PrimitiveType.Cylinder, w.vis, Vector3.zero, new Vector3(0.1f, wd * 0.6f, 0.1f), new Vector3(0f, 0f, 90f), alu);
            if (knobby)
                for (int k = 0; k < 16; k++)
                {
                    float a = k * 360f / 16f;
                    Mats.Prim(PrimitiveType.Cube, w.vis, Quaternion.Euler(a, 0f, 0f) * new Vector3(0f, w.r, 0f), new Vector3(wd * 0.95f, 0.04f, 0.05f), new Vector3(a, 0f, 0f), tyre);
                }
            if (type == 4 && f || type == 8 && f)   // front fender over a street wheel
            {
                var fd = Mats.Prim(PrimitiveType.Cube, w.vis.parent, w.mount + new Vector3(0f, -rest * 0.6f + w.r + 0.04f, 0.02f), new Vector3(0.16f, 0.02f, 0.42f), paint);
                fd.name = "Front fender";
            }
        }
        seat = Mats.Node(L, "Seat", new Vector3(0f, seatY, seatZ));
        seatScale = 0.66f;
        if (Look.Mobile) Mats.NoShadows(gameObject);
    }
}
