using System.Collections.Generic;
using UnityEngine;

// ffu30 motorbikes in the garage (Bill: "Motorcycles in the garage too that the froggies can drive. Electric motorcycles
// and gas ones, mostly dirt bikes."). Four bikes parked beside the vehicles in bays 0-3: a 2-stroke dirt bike, a 4-stroke
// dirt bike, an electric dirt bike (Stark Varg / Surron style) and an electric street / sport bike. Usual enter key /
// button / tap (E / Y / A / tap).
// Handling: two raycast springs on the centre line (long-travel forks + rear shock), the physics body is held upright and
// the MODEL leans into turns (lean from speed x yaw rate), wheelies on hard throttle from low speed (held, never loops
// over), roost + dust from the rear tyre, air control (stick pitches / yaws), soft crashes: land on the bars / cartwheel
// -> the bike tumbles for a moment and stands itself back up with the rider on (no hard death).
// Voices: SynthVoice 14 (2-stroke brap) / 16 (4-stroke thump) with an RPM + gear-band model, 15 (electric whine, higher
// than the Cybertruck's). AI froggies take a bike out for a few laps of the front yard now and then (BikeAI).
// Jump line ("MX") just south of the RC raceway: kicker + landing and a tabletop (BuildMx).
public class Bike : Vehicle
{
    public static readonly List<Bike> AllBikes = new List<Bike>();
    public int type;                 // 0 2-stroke dirt, 1 4-stroke dirt, 2 electric dirt, 3 electric street
    public bool Electric { get { return type >= 2; } }
    class Wh { public Vector3 mount; public float r, dist, spin; public bool grounded; public Transform vis; public Vector3 axle0, vs0; public float r0, extra, sr, wrest; }
    readonly Wh fw = new Wh(), rw = new Wh();
    float rest, spring, damper;
    public float maxSpeed = 24f, accel = 11f, latCap = 11f, turnRate = 2.1f;
    float steer, throttle, lean, leanV, flipT, roostT, crashT, wheelieDeg;
    Transform leanT, bars, swing, rshock, forkL, forkR, headT;
    Vector3 lastVel, accLocal;
    SynthVoice voice;
    public bool airborne; public float airT, lastAir;
    public int crashes, wheelies;
    bool inWheelie;
    // AI ride (BikeAI)
    public Frog aiRider; public int aiPhase; public float aiAng, aiTurned, aiT, aiStuck;
    public System.Func<PIn, PIn> demoHook;

    public static void SpawnAll()
    {
        if (AllBikes.Count > 0) return;
        float bz = Layout.GarageC.y - 4.2f;
        string[] names = { "2-stroke dirt bike", "4-stroke dirt bike", "Electric dirt bike", "Electric sport bike" };
        for (int i = 0; i < 4; i++)
        {
            float x = Layout.BayX(i) + 3.3f;
            Vector3 p = new Vector3(x, Ranch.GY(x, bz) + 0.15f, bz);
            Spawn(names[i], p, 0f, i);
        }
        BuildMx();
        new GameObject("Bike AI").AddComponent<BikeAI>();
        Debug.Log("Bike: " + AllBikes.Count + " bikes in the garage, MX jump line at " + RcTrack.MxC);
    }

    static Bike Spawn(string title, Vector3 pos, float yaw, int type)
    {
        var go = new GameObject(title);
        go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        var b = go.AddComponent<Bike>();
        b.type = type; b.Title = title; b.EnterVerb = "ride the " + title.ToLower();
        bool street = type == 3;
        b.rest = street ? 0.13f : 0.26f;
        float rf = street ? 0.31f : 0.36f, rr = street ? 0.31f : 0.33f;
        b.fw.r = rf; b.rw.r = rr;
        // ray origins so that at rest (spring 42% compressed) the tyres touch y = 0 of the model: the model is built on the ground line
        b.fw.mount = new Vector3(0f, 0.58f * b.rest + rf, street ? 0.7f : 0.74f);
        b.rw.mount = new Vector3(0f, 0.58f * b.rest + rr, street ? -0.68f : -0.72f);
        b.fw.dist = b.rest + rf; b.rw.dist = b.rest + rr;
        b.SetupBodyPublic(street ? 170f : 115f, new Vector3(0f, 0.68f, 0f), new Vector3(0.34f, 0.46f, 1.45f), new Vector3(0f, 0.5f, 0f));
        b.rb.angularDrag = 1.2f;
        b.maxSpeed = type == 0 ? 24f : type == 1 ? 23f : type == 2 ? 22f : 33f;
        b.accel = type == 0 ? 12f : type == 1 ? 11f : type == 2 ? 13f : 12.5f;
        b.latCap = street ? 15f : 11f;
        b.turnRate = street ? 1.9f : 2.2f;
        b.engineKind = type == 0 ? 14 : type == 1 ? 16 : 15;
        b.enginePitch = type == 0 ? 1f : type == 1 ? 1f : type == 2 ? 1.55f : 1.3f;
        b.camDistance = 4.6f; b.camHeight = 1.9f;
        b.Build();
        float perWheel = b.rb.mass * 9.81f / 2f;
        b.spring = perWheel / (b.rest * 0.42f);
        b.damper = 2f * Mathf.Sqrt(b.spring * b.rb.mass / 2f) * 0.45f;
        Mats.SetLayer(go, VehicleLayer);
        AllBikes.Add(b);
        return b;
    }

    void Build()
    {
        bool street = type == 3;
        Color pc = type == 0 ? new Color(0.98f, 0.42f, 0.05f) : type == 1 ? new Color(0.98f, 0.82f, 0.08f) : type == 2 ? new Color(0.08f, 0.35f, 0.85f) : new Color(0.12f, 0.75f, 0.6f);
        Material paint = Mats.Paint(pc, 0.85f), black = Mats.Lit(new Color(0.07f, 0.07f, 0.08f)), plastic = Mats.Paint(new Color(0.94f, 0.94f, 0.92f), 0.6f);
        Material alu = Mats.Steel(new Color(0.7f, 0.72f, 0.76f)), chrome = Mats.PBR(new Color(0.8f, 0.8f, 0.82f), 0.85f, 0.9f), tyre = Mats.Lit(new Color(0.05f, 0.05f, 0.055f));
        Material gold = Mats.PBR(new Color(0.88f, 0.66f, 0.22f), 0.7f, 0.8f), seatM = Mats.Paint(street ? new Color(0.1f, 0.1f, 0.11f) : new Color(0.12f, 0.14f, 0.2f), 0.3f), glow = Mats.Unlit(new Color(0.3f, 0.9f, 1f));
        leanT = Mats.Node(transform, "Lean", Vector3.zero);
        Transform L = leanT;
        // frame + swingarm pivot
        var spar = Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.86f, 0.18f), new Vector3(0.07f, 0.07f, 0.85f), Electric ? black : alu);
        spar.transform.localRotation = Quaternion.Euler(-24f, 0f, 0f);
        Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.62f, -0.1f), new Vector3(0.12f, 0.2f, 0.1f), alu);
        Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.95f, -0.42f), new Vector3(0.08f, 0.05f, 0.6f), alu);   // subframe
        if (!street)
        {
            Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 1.0f, 0.24f), new Vector3(0.26f, 0.16f, 0.42f), paint);   // tank
            for (int s = -1; s <= 1; s += 2)
            {
                var sh = Mats.Prim(PrimitiveType.Cube, L, new Vector3(s * 0.15f, 0.95f, 0.32f), new Vector3(0.03f, 0.22f, 0.36f), paint);   // radiator shrouds
                sh.transform.localRotation = Quaternion.Euler(0f, s * 10f, 0f);
                var sp = Mats.Prim(PrimitiveType.Cube, L, new Vector3(s * 0.155f, 0.84f, -0.42f), new Vector3(0.025f, 0.16f, 0.4f), plastic);   // side panels
            }
            Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 1.0f, -0.33f), new Vector3(0.21f, 0.07f, 0.74f), seatM);   // long flat seat
            var rf = Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 1.03f, -0.86f), new Vector3(0.2f, 0.02f, 0.5f), plastic);   // rear fender
            rf.transform.localRotation = Quaternion.Euler(-14f, 0f, 0f);
        }
        else
        {
            // sport fairing, tail, screen
            Mats.Prim(PrimitiveType.Sphere, L, new Vector3(0f, 0.86f, 0.38f), new Vector3(0.42f, 0.5f, 0.9f), paint);
            Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.98f, 0.1f), new Vector3(0.3f, 0.16f, 0.4f), paint);
            var scr = Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 1.12f, 0.62f), new Vector3(0.24f, 0.2f, 0.02f), Mats.Paint(new Color(0.1f, 0.15f, 0.2f), 0.95f));
            scr.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
            Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.9f, -0.25f), new Vector3(0.26f, 0.08f, 0.4f), seatM);
            var tail = Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.98f, -0.55f), new Vector3(0.22f, 0.12f, 0.36f), paint);
            tail.transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);
            Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.86f, 0.83f), new Vector3(0.18f, 0.05f, 0.02f), glow);   // DRL
            Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.98f, -0.74f), new Vector3(0.16f, 0.03f, 0.02f), Mats.Unlit(new Color(1f, 0.1f, 0.1f)));
        }
        // power unit
        if (!Electric)
        {
            Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.55f, 0.05f), new Vector3(0.26f, 0.26f, 0.3f), alu);   // crankcase
            Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.75f, 0.12f), new Vector3(0.18f, 0.2f, 0.16f), black);   // cylinder + head
            for (int k = 0; k < 4; k++) Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.68f + k * 0.04f, 0.12f), new Vector3(0.22f, 0.012f, 0.18f), alu);   // fins
            if (type == 0)
            {
                // 2-stroke expansion chamber + silencer on the right
                var p1 = Mats.Prim(PrimitiveType.Capsule, L, new Vector3(0.1f, 0.5f, 0.25f), new Vector3(0.11f, 0.22f, 0.11f), chrome);
                p1.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
                var p2 = Mats.Prim(PrimitiveType.Capsule, L, new Vector3(0.15f, 0.62f, -0.12f), new Vector3(0.12f, 0.24f, 0.12f), chrome);
                p2.transform.localRotation = Quaternion.Euler(-70f, 0f, 0f);
            }
            var sil = Mats.Prim(PrimitiveType.Cylinder, L, new Vector3(0.17f, 0.9f, -0.62f), new Vector3(0.09f, 0.2f, 0.09f), Mats.Steel(new Color(0.35f, 0.35f, 0.38f)));
            sil.transform.localRotation = Quaternion.Euler(-78f, 0f, 0f);
        }
        else
        {
            Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.62f, 0.05f), new Vector3(0.24f, 0.3f, 0.42f), black);   // battery
            Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 0.62f, 0.05f), new Vector3(0.245f, 0.03f, 0.3f), glow);   // charge strip
            Mats.Prim(PrimitiveType.Cylinder, L, new Vector3(0f, 0.5f, -0.18f), new Vector3(0.16f, 0.06f, 0.16f), new Vector3(0f, 0f, 90f), alu);   // motor
        }
        // swingarm + rear shock
        swing = Mats.Node(L, "Swingarm", new Vector3(0f, 0.6f, -0.1f));
        for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, swing, new Vector3(s * 0.08f, 0f, -0.32f), new Vector3(0.03f, 0.06f, 0.64f), alu);
        rshock = Mats.Node(L, "Shock", Vector3.zero);
        Mats.Prim(PrimitiveType.Cylinder, rshock, Vector3.zero, new Vector3(0.05f, 1f, 0.05f), gold);
        // fork: steering head, bars, fork legs, front fender + number plate
        headT = Mats.Node(L, "Head", new Vector3(0f, street ? 1.0f : 1.1f, street ? 0.48f : 0.5f));
        bars = Mats.Node(headT, "Bars", Vector3.zero);
        Mats.Prim(PrimitiveType.Cylinder, bars, new Vector3(0f, street ? 0.02f : 0.12f, -0.04f), new Vector3(0.025f, street ? 0.3f : 0.4f, 0.025f), new Vector3(0f, 0f, 90f), black);
        for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cylinder, bars, new Vector3(s * (street ? 0.27f : 0.36f), street ? 0.02f : 0.12f, -0.04f), new Vector3(0.035f, 0.06f, 0.035f), new Vector3(0f, 0f, 90f), black);
        Mats.Prim(PrimitiveType.Cube, bars, new Vector3(0f, 0.02f, 0f), new Vector3(0.2f, 0.05f, 0.06f), alu);   // triple clamp
        if (!street)
        {
            var plate = Mats.Prim(PrimitiveType.Cube, bars, new Vector3(0f, -0.04f, 0.1f), new Vector3(0.24f, 0.22f, 0.02f), plastic);
            plate.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
            Mats.Prim(PrimitiveType.Cube, bars, new Vector3(0f, -0.04f, 0.113f), new Vector3(0.1f, 0.12f, 0.004f), paint);
        }
        forkL = Mats.Node(L, "ForkL", Vector3.zero); forkR = Mats.Node(L, "ForkR", Vector3.zero);
        foreach (var fk in new[] { forkL, forkR })
        {
            Mats.Prim(PrimitiveType.Cylinder, fk, Vector3.zero, new Vector3(0.05f, 1f, 0.05f), street ? gold : chrome);
        }
        // wheels
        foreach (var w in new[] { fw, rw })
        {
            bool f = w == fw;
            w.vis = Mats.Node(L, f ? "Front wheel" : "Rear wheel", w.mount - Vector3.up * (rest * 0.6f));
            float wd = street ? (f ? 0.12f : 0.18f) : (f ? 0.09f : 0.12f);
            Mats.Prim(PrimitiveType.Cylinder, w.vis, Vector3.zero, new Vector3(w.r * 2f, wd * 0.5f, w.r * 2f), new Vector3(0f, 0f, 90f), tyre);
            Mats.Prim(PrimitiveType.Cylinder, w.vis, Vector3.zero, new Vector3(w.r * 1.55f, wd * 0.52f, w.r * 1.55f), new Vector3(0f, 0f, 90f), street ? Mats.Lit(new Color(0.12f, 0.12f, 0.13f)) : alu);
            Mats.Prim(PrimitiveType.Cylinder, w.vis, Vector3.zero, new Vector3(w.r * 1.45f, wd * 0.54f, w.r * 1.45f), new Vector3(0f, 0f, 90f), black);
            int sp = street ? 5 : 8;
            for (int k = 0; k < sp; k++)
                Mats.Prim(PrimitiveType.Cube, w.vis, Vector3.zero, new Vector3(0.012f, w.r * 1.42f, street ? 0.04f : 0.008f), new Vector3(k * 180f / sp, 0f, 0f), street ? Mats.Paint(pc, 0.7f) : chrome);
            Mats.Prim(PrimitiveType.Cylinder, w.vis, Vector3.zero, new Vector3(0.1f, wd * 0.56f, 0.1f), new Vector3(0f, 0f, 90f), alu);   // hub
            if (!street)
                for (int k = 0; k < 14; k++)
                {
                    float a = k * 360f / 14f;
                    Mats.Prim(PrimitiveType.Cube, w.vis, Quaternion.Euler(a, 0f, 0f) * new Vector3(0f, w.r, 0f), new Vector3(wd * 0.95f, 0.035f, 0.05f), new Vector3(a, 0f, 0f), tyre);
                }
        }
        // front fender rides on the forks (dirt) / over the wheel (street)
        if (!street)
        {
            var ff = Mats.Prim(PrimitiveType.Cube, bars, new Vector3(0f, -0.35f, 0.18f), new Vector3(0.16f, 0.02f, 0.5f), plastic);
            ff.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
        }
        seat = Mats.Node(L, "Seat", new Vector3(0f, street ? 0.88f : 0.98f, street ? -0.2f : -0.2f));
        seatScale = 0.66f;
        if (Look.Mobile) Mats.NoShadows(gameObject);
    }

    public override string[] TouchSet { get { return new[] { "A", null, null, "GAS", "BRAKE" }; } }
    public override string HelpLine
    {
        get { return Title + ": L-stick steer | RT throttle (hard from slow = wheelie!) | LT brake | in the air stick pitches | A get off"; }
    }

    protected override void OnCollisionEnter(Collision c)
    {
        base.OnCollisionEnter(c);
        if (driver == null || crashT > 0f) return;
        if (c.relativeVelocity.magnitude > 13f) Crash("Ouch! Hit something - ");
    }

    void Crash(string why)
    {
        crashT = 1.4f; crashes++;
        if (driver != null && driver.human) driver.Toast(why + "back on the bike in a sec", 1.6f);
        FX.Cloud(transform.position + Vector3.up * 0.4f, 0.6f, 8, new Color(0.62f, 0.52f, 0.38f, 0.6f));
        Sfx.PlayAt(Sfx.Pick(Sfx.Crash), transform.position, 0.45f, 40f, 1.3f);
        Debug.Log("Bike: " + Title + " crash (" + why + ") at " + transform.position.ToString("0.0"));
    }

    protected override void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        PondPhysics.Step(this, dt);   // ffu37: sinks to the real pond bed
        PIn ip = driver != null ? inp : new PIn();
        if (demoHook != null && driver != null) ip = demoHook(ip);
        if (aiRider != null && driver == aiRider) ip = BikeAI.Ride(this, dt);
        bool driven = driver != null;
        steer = Mathf.Clamp(ip.move.x, -1f, 1f);
        throttle = Mathf.Clamp(ip.move.y + ip.gas - ip.brake, -1f, 1f);
        if (ip.gas > 0.1f) throttle = Mathf.Max(throttle, ip.gas);
        Vector3 v = rb.velocity;
        accLocal = transform.InverseTransformDirection((v - lastVel) / dt); lastVel = v;

        if (crashT > 0f)
        {
            // tumbling: no rider control, no upright assist; then stand it back up where it lies
            crashT -= dt;
            if (crashT <= 0f)
            {
                rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
                RaycastHit h; Vector3 p = rb.position;
                float gy = Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out h, 12f, GroundMask, QueryTriggerInteraction.Ignore) ? h.point.y : Ranch.GY(p.x, p.z);
                rb.position = new Vector3(p.x, gy + 0.35f, p.z);
                Vector3 f = transform.forward; f.y = 0f; if (f.sqrMagnitude < 0.01f) f = Vector3.forward;
                rb.rotation = Quaternion.LookRotation(f.normalized, Vector3.up);
                lean = 0f; leanV = 0f;
            }
            Voice(dt, driven, 0f, 0f);
            return;
        }

        Vector3 up = transform.up;
        int g = 0; Vector3 ns = Vector3.zero; onStuntB = false;
        foreach (var w in new[] { fw, rw })
        {
            Vector3 o = transform.TransformPoint(w.mount);
            bool big = bigK > 1.0001f;
            float wr = big ? w.wrest : rest;
            float len = wr + w.r + (big ? w.extra : 0f);
            RaycastHit h; float hd; bool hitOk;
            if (big) hitOk = BigWheels.Cast(o, up, len, w.sr, GroundMask, out h, out hd);   // ffu42: sphere-cast tyre
            else { hitOk = Physics.Raycast(o, -up, out h, len, GroundMask, QueryTriggerInteraction.Ignore); hd = hitOk ? h.distance : len; }
            if (hitOk)
            {
                w.grounded = true; w.dist = hd;
                float comp = len - hd;
                if (big) comp = Mathf.Min(comp, wr * 1.35f);
                float vUp = Vector3.Dot(rb.GetPointVelocity(o), up);
                float f = spring * comp - damper * vUp;
                if (comp > wr * 0.88f) f += spring * 6f * (comp - wr * 0.88f);
                if (f > 0f) rb.AddForceAtPosition(up * f, o);
                g++; ns += h.normal;
                if (RallyTrack.IsStunt(h.collider.gameObject.layer)) onStuntB = true;
            }
            else { w.grounded = false; w.dist = Mathf.MoveTowards(w.dist, len, dt * 2.5f); }
        }
        airborne = g == 0;
        if (airborne) airT += dt; else { if (airT > 0.35f) { lastAir = airT; if (airT > 0.6f && driver != null && driver.human) driver.Toast("AIR TIME " + airT.ToString("0.0") + " s!", 1.2f); } airT = 0f; }
        Vector3 n = g > 0 ? ns.normalized : Vector3.up;
        // body upright about its long axis (the model leans instead); ffu43: on a stunt wall "upright" = the surface normal,
        // and the bike sticks to it while it has speed
        Vector3 upRef = onStuntB && g > 0 ? n : Vector3.up;
        if (onStuntB && g > 0) rb.AddForce(-n * 14f * Mathf.Clamp01((1f - n.y) / 0.6f) * Mathf.Clamp01((v.magnitude - 4f) / 5f), ForceMode.Acceleration);
        Vector3 tilt = Vector3.Project(Vector3.Cross(up, upRef), transform.forward);
        float rollRate = Vector3.Dot(rb.angularVelocity, transform.forward);
        rb.AddTorque(tilt * 60f - transform.forward * rollRate * 9f, ForceMode.Acceleration);

        float fs = Vector3.Dot(v, transform.forward), ls = 0f;
        if (g > 0)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, n).normalized, right = Vector3.ProjectOnPlane(transform.right, n).normalized;
            fs = Vector3.Dot(v, fwd); ls = Vector3.Dot(v, right);
            float rearK = rw.grounded ? 1f : 0.15f;
            float force;
            if (!driven) force = -fs * 3f;
            else if (throttle > 0.02f) force = fs < -0.5f ? 16f * throttle : accel * throttle * Mathf.Clamp01(1.1f - fs / maxSpeed) * rearK;
            else if (throttle < -0.02f) force = fs > 0.5f ? -15f * -throttle : (fs > -2.5f ? accel * 0.35f * throttle : 0f);
            else force = -fs * 0.5f;
            float gf = g / 2f;
            rb.AddForce(fwd * force * rb.mass * gf, ForceMode.Force);
            float cap = latCap * (driven && throttle > 0.7f && Mathf.Abs(steer) > 0.5f && fs > 4f && type != 3 ? 0.65f : 1f);
            rb.AddForce(right * Mathf.Clamp(-ls * 8f, -cap, cap) * rb.mass * gf, ForceMode.Force);
            slipSpeed = ls; groundFrac = gf;
            float yr = YawRate(rb, transform);
            float tr = Mathf.Min(turnRate, latCap * 0.95f / Mathf.Max(1f, Mathf.Abs(fs)));   // no tighter than the tyres can hold
            float target = driven ? steer * tr * Mathf.Clamp(fs / 3f, -1f, 1f) : 0f;
            if (fw.grounded) rb.AddTorque(transform.up * (target - yr) * Mathf.Min(1f, 8f * dt), ForceMode.VelocityChange);
            rb.AddForce(-n * 2f * gf, ForceMode.Acceleration);
            // wheelies
            float pitchUp = Mathf.Asin(Mathf.Clamp(Vector3.Dot(transform.forward, n), -1f, 1f)) * Mathf.Rad2Deg;
            wheelieDeg = rw.grounded && !fw.grounded ? pitchUp : 0f;
            if (driven && rw.grounded && throttle > 0.75f && fs > -0.3f && fs < 13f && pitchUp < 34f)
                rb.AddTorque(-transform.right * 26f * throttle * (1f - fs / 13f), ForceMode.Acceleration);
            if (!fw.grounded && pitchUp > 40f) rb.AddTorque(transform.right * 30f, ForceMode.Acceleration);
            bool wh = !fw.grounded && rw.grounded && pitchUp > 8f;
            if (wh && !inWheelie) wheelies++;
            inWheelie = wh;
            // roost from the rear tyre
            roostT -= dt;
            if (roostT <= 0f && rw.grounded && driven && (throttle > 0.4f || Mathf.Abs(ls) > 2f) && type != 3)
            {
                roostT = 0.04f;
                Vector3 cp = transform.TransformPoint(rw.mount) - up * rw.dist;
                float k = Mathf.Clamp01(throttle * (1.2f - fs / maxSpeed) + Mathf.Abs(ls) * 0.15f);
                if (Random.value < k)
                {
                    Vector3 back = -transform.forward * (3f + throttle * 4f) + Vector3.up * (1.5f + Random.value * 2f) + Random.insideUnitSphere * 1.2f;
                    FX.Puff(cp + Vector3.up * 0.1f, back, Random.Range(0.35f, 0.7f), Random.Range(0.6f, 1.1f), new Color(0.56f, 0.44f, 0.3f, 0.65f));
                }
            }
        }
        else if (driven)
        {
            // air control: stick forward = nose down, back = nose up; sideways = yaw
            rb.AddTorque(transform.right * ip.move.y * 7f + transform.up * steer * 3.5f, ForceMode.Acceleration);
        }
        // crashes: on its back / standing on its nose for a moment, or a hard landing far off level -> soft crash
        if (up.y < 0.2f && !(onStuntB && v.magnitude > 5f)) flipT += dt; else flipT = 0f;   // ffu43: not while riding a wall
        if (flipT > 0.35f) { flipT = 0f; Crash("Wipeout! "); }
        // lean the model into the turn (lean from speed x yaw rate), a little extra with the stick at low speed
        float yawRate = YawRate(rb, transform);
        float leanT2 = airborne ? lean * 0.98f : Mathf.Clamp(Mathf.Atan2(fs * yawRate, 9.81f) * Mathf.Rad2Deg, -44f, 44f);
        leanV += ((leanT2 - lean) * 70f - leanV * 11f) * dt; lean += leanV * dt;
        // fell off the world
        if (OutOfWorld(rb.position)) ResetHome();
        Voice(dt, driven, fs, ls);
    }

    void Voice(float dt, bool driven, float fs, float ls)
    {
        if (voice == null) voice = new SynthVoice(gameObject, engineKind, enginePitch);
        float gk = 1f;
        if (driver == null || !driver.human) { gk = Mathf.Clamp01(1f - Sfx.Near(transform.position) / 60f); gk *= gk; }
        voice.gain = gk;
        voice.Tick(driven, Mathf.Abs(fs), driven ? Mathf.Clamp01(throttle) : 0f, ls, airborne ? 0f : 1f, dt);
    }
    public string VoiceLevels { get { return voice != null ? voice.Levels() : "-"; } }
    public float Lean { get { return lean; } }
    public float WheelieDeg { get { return wheelieDeg; } }

    // ---------------- ffu42 big wheels (BigWheels.cs): monster bike ----------------
    [System.NonSerialized] public float bigK = 1f;
    const float BikeComCut = 1.12f;   // ffu44 (trucks keep BigWheels.ComCut 0.9)
    bool bigReady; float rest0, spring0, damper0, cam0, camH0, pitch0, zc0, gap0, rmax0; Vector3 com0;
    void BigTick(float dt)
    {
        if (rb == null) return;
        if (!bigReady)
        {
            bigReady = true;
            rest0 = rest; spring0 = spring; damper0 = damper; com0 = rb.centerOfMass; cam0 = camDistance; camH0 = camHeight; pitch0 = enginePitch;
            foreach (var w in new[] { fw, rw }) { w.axle0 = w.mount - Vector3.up * rest; w.r0 = w.r; w.vs0 = w.vis != null ? w.vis.localScale : Vector3.one; w.wrest = rest; rmax0 = Mathf.Max(rmax0, w.r); }
            zc0 = (fw.axle0.z + rw.axle0.z) * 0.5f; gap0 = Mathf.Abs(fw.axle0.z - rw.axle0.z);
        }
        float t = stowed || move3 ? 1f : BigWheels.S;
        float k = BigWheels.Ease(bigK, t, dt);
        if (Mathf.Abs(k - bigK) < 1e-5f) return;
        bigK = k;
        float fz = BigWheels.ZFactor(bigK, rmax0, gap0), lift = 0f, restK = 0f, zrK = 1e9f, zr1 = 1e9f;
        foreach (var w in new[] { fw, rw })
        {
            var g = BigWheels.Wheel(bigK, w.axle0, w.r0, rest0, zc0, fz);
            w.r = g.r; w.mount = g.mount; w.extra = g.extra; w.sr = g.sr; w.wrest = g.rest;
            BigWheels.Vis(w.vis, w.vs0, g);
            lift += g.lift * 0.5f; restK += g.rest * 0.5f;
            zrK = Mathf.Min(zrK, g.mount.z - g.r); zr1 = Mathf.Min(zr1, w.axle0.z - w.r0);
            if (!w.grounded) w.dist = g.rest + g.r + g.extra;
        }
        bigLift = bigK > 1.0001f ? lift : 0f; bigPad = Mathf.Max(0f, (fw.r - fw.r0) * 0.3f); bigBack = Mathf.Max(0f, zr1 - zrK);
        float rr = rest0 / Mathf.Max(0.01f, restK);
        // ffu44: calmer monster bike (ffu42 probe: 2 flips at 4x over the boulder + thick log) - more damping as the tyres
        // grow (4x = 1.9x), centre of mass a little BELOW its stock height off the ground, more angular drag
        spring = spring0 * rr; damper = damper0 * Mathf.Sqrt(rr) * (1f + 0.3f * (bigK - 1f));
        rb.centerOfMass = com0 - Vector3.up * bigLift * BikeComCut;
        rb.angularDrag = 1.2f + 0.6f * (bigK - 1f);
        camDistance = cam0 * (1f + (bigK - 1f) * 0.25f); camHeight = camH0 + bigLift * 0.8f;
        rb.WakeUp();
    }
    float swingLen0;
    bool onStuntB;
    bool move3 { get { return rb != null && rb.isKinematic; } }   // ffu31 ramp ride / bay: kinematic cargo keeps stock wheels

    void Update()
    {
        float dt = Time.deltaTime;
        BigTick(Mathf.Min(dt, 0.1f));
        if (leanT != null) leanT.localRotation = Quaternion.Euler(0f, 0f, -lean);
        float fs = ForwardSpeed;
        foreach (var w in new[] { fw, rw })
        {
            Vector3 hub = w.mount - Vector3.up * (w.dist - w.r);
            float add = (w == rw && driver != null && (airborne || !rw.grounded || throttle > 0.9f && Mathf.Abs(fs) < 4f)) ? throttle * 14f : 0f;   // spins up in the air / on a launch
            w.spin += (fs + add) / w.r * Mathf.Rad2Deg * dt;
            w.vis.localPosition = hub;
            w.vis.localRotation = (w == fw ? Quaternion.Euler(0f, steer * 22f, 0f) : Quaternion.identity) * Quaternion.Euler(w.spin, 0f, 0f);
        }
        // fork legs: steering head -> front hub
        Vector3 fh = fw.mount - Vector3.up * (fw.dist - fw.r);
        for (int s = 0; s < 2; s++)
        {
            Transform fk = s == 0 ? forkL : forkR; float sx = s == 0 ? -0.09f : 0.09f;
            Vector3 a = headT.localPosition + new Vector3(sx, 0.05f, 0f), b = fh + new Vector3(sx, 0f, 0f);
            Vector3 d = a - b; float len = d.magnitude;
            fk.localPosition = (a + b) * 0.5f; fk.localRotation = Quaternion.FromToRotation(Vector3.up, d / len); fk.localScale = new Vector3(1f, len * 0.5f, 1f);
        }
        if (bars != null) bars.localRotation = Quaternion.Euler(0f, steer * 22f, 0f);
        // swingarm follows the rear hub, shock from the frame to the arm
        Vector3 rh = rw.mount - Vector3.up * (rw.dist - rw.r);
        Vector3 sa = rh - swing.localPosition;
        swing.localRotation = Quaternion.FromToRotation(Vector3.back, sa.normalized);
        if (swingLen0 <= 0f) swingLen0 = Mathf.Max(0.1f, sa.magnitude);
        float sk = bigK > 1.0001f ? sa.magnitude / swingLen0 : 1f;   // ffu42: the arm reaches a big, set-back rear hub
        swing.localScale = new Vector3(1f + (sk - 1f) * 0.6f, 1f + (sk - 1f) * 0.6f, sk);
        Vector3 top = new Vector3(0f, 0.98f, -0.22f), bot = swing.localPosition + sa * 0.45f + Vector3.up * 0.04f;
        Vector3 sd = top - bot; float sl = sd.magnitude;
        rshock.localPosition = (top + bot) * 0.5f; rshock.localRotation = Quaternion.FromToRotation(Vector3.up, sd / sl); rshock.localScale = new Vector3(1f, sl * 0.5f, 1f);
    }

    // the rider: seated, hands on the bars, leaning forward into it
    void LateUpdate()
    {
        if (driver == null) return;
        var m = driver.model;
        if (m == null) return;
        bool street = type == 3;
        if (m.legL != null) m.legL.localRotation = Quaternion.Euler(-38f, 0f, -8f);
        if (m.legR != null) m.legR.localRotation = Quaternion.Euler(-38f, 0f, 8f);
        float reach = street ? -62f : -78f;
        if (m.armL != null) m.armL.localRotation = Quaternion.Euler(reach - steer * 6f, 0f, 0f);
        if (m.armR != null) m.armR.localRotation = Quaternion.Euler(reach + steer * 6f, 0f, 0f);
        if (m.bob != null) m.bob.localRotation = Quaternion.Euler((street ? 22f : 12f) + Mathf.Clamp(-accLocal.z, -6f, 6f), 0f, 0f);
    }

    // ---------------- MX jump line (south of the RC raceway) ----------------
    static void BuildMx()
    {
        var root = new GameObject("MX jumps").transform;
        var tex = Mats.Lit(new Color(0.55f, 0.42f, 0.28f));
        Vector2 c = RcTrack.MxC;
        // kicker (4.5 m to 1.25 m) -> 5 m gap -> landing (1.15 m over 6.5 m); then a tabletop (up 4, top 4, down 5.5)
        Wedge(root, tex, c.x - 18f, c.y, 4.5f, 0f, 1.25f, 4.2f);
        Wedge(root, tex, c.x - 8.5f, c.y, 6.5f, 1.15f, 0f, 4.6f);
        Wedge(root, tex, c.x + 3f, c.y, 4f, 0f, 1.05f, 4.4f);
        Wedge(root, tex, c.x + 7f, c.y, 4f, 1.05f, 1.05f, 4.4f);
        Wedge(root, tex, c.x + 11f, c.y, 5.5f, 1.05f, 0f, 4.4f);
        // a return line the other way (rollers)
        for (int k = 0; k < 4; k++) { Wedge(root, tex, c.x + 12f - k * 7f, c.y - 6f, 2.5f, 0f, 0.55f, 4f); Wedge(root, tex, c.x + 14.5f - k * 7f, c.y - 6f, 2.5f, 0.55f, 0f, 4f); }
        // little start flag
        Mats.Prim(PrimitiveType.Cylinder, root, new Vector3(c.x - 24f, Ranch.GY(c.x - 24f, c.y + 3f) + 1.2f, c.y + 3f), new Vector3(0.06f, 1.2f, 0.06f), Mats.Lit(new Color(0.2f, 0.2f, 0.2f)));
        Mats.Prim(PrimitiveType.Cube, root, new Vector3(c.x - 23.6f, Ranch.GY(c.x - 24f, c.y + 3f) + 2.15f, c.y + 3f), new Vector3(0.8f, 0.5f, 0.02f), Mats.Unlit(new Color(0.98f, 0.45f, 0.05f)));
    }
    // ramp along +x starting at x0, length len, from height h0 to h1 (above the ground line), width wd; the base sits
    // below the lowest terrain so it never floats
    static void Wedge(Transform root, Material m, float x0, float z, float len, float h0, float h1, float wd)
    {
        float gy0 = Mathf.Min(Ranch.GY(x0, z), Ranch.GY(x0 + len, z), Ranch.GY(x0, z - wd * 0.5f), Ranch.GY(x0 + len, z + wd * 0.5f));
        float y0 = Ranch.GY(x0, z), y1 = Ranch.GY(x0 + len, z);
        float bot = gy0 - 0.4f;
        float hw = wd * 0.5f;
        Vector3[] v =
        {
            new Vector3(x0, bot, z - hw), new Vector3(x0 + len, bot, z - hw), new Vector3(x0 + len, bot, z + hw), new Vector3(x0, bot, z + hw),
            new Vector3(x0, y0 + h0, z - hw), new Vector3(x0 + len, y1 + h1, z - hw), new Vector3(x0 + len, y1 + h1, z + hw), new Vector3(x0, y0 + h0, z + hw)
        };
        int[] t = { 4, 7, 6, 4, 6, 5, 0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5, 2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7, 0, 3, 2, 0, 2, 1 };
        // split into flat-shaded faces
        var vv = new List<Vector3>(); var tt = new List<int>();
        for (int i = 0; i < t.Length; i += 3) { int b = vv.Count; vv.Add(v[t[i]]); vv.Add(v[t[i + 1]]); vv.Add(v[t[i + 2]]); tt.Add(b); tt.Add(b + 1); tt.Add(b + 2); }
        var mesh = new Mesh(); mesh.SetVertices(vv); mesh.SetTriangles(tt, 0);
        // make every face point outwards
        var verts = mesh.vertices; var tris = mesh.triangles; Vector3 cen = Vector3.zero; foreach (var p in v) cen += p; cen /= v.Length;
        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f - cen) < 0f) { int k = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = k; }
        }
        mesh.triangles = tris; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var go = new GameObject("MX ramp"); go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = m;
        var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = mesh; mc.convex = true;
    }
}

// AI froggies take a bike out now and then: walk to a free bike, ride out of the garage, a few laps of the front yard,
// stop, hop off. The bike is put back in its garage spot once nobody is near it.
public class BikeAI : MonoBehaviour
{
    float t = 25f;
    Bike target; Frog goer; float walkT;
    static readonly Vector2 LoopC = new Vector2(-8f, 54f);
    const float LoopR = 10f;
    float[] idle = new float[8];

    void Update()
    {
        var g = Game.I;
        if (g == null || g.state != Game.State.Play) return;
        float dt = Time.deltaTime;
        // walking to a bike
        if (goer != null)
        {
            walkT += dt;
            if (!goer.AiIdleOnRanch || target.driver != null || walkT > 45f || Story.Active) { goer = null; target = null; }
            else
            {
                goer.RcInvite(target.transform.position - target.transform.right * 1.2f);
                if ((goer.transform.position - target.transform.position).magnitude < 2.6f)
                {
                    goer.EnterVehicle(target);
                    target.aiRider = goer; target.aiPhase = 0; target.aiTurned = 0f; target.aiT = 0f;
                    Debug.Log("BikeAI: " + goer.nick + " rides the " + target.Title);
                    goer = null; target = null;
                }
            }
        }
        // bikes left out: home again when nobody is around
        for (int i = 0; i < Bike.AllBikes.Count; i++)
        {
            Bike b = Bike.AllBikes[i];
            if (b.driver != null || b.stowed || (b.transform.position - b.HomePos).magnitude < 2f) { idle[i] = 0f; continue; }   // ffu31: a bike riding in a toy hauler stays there
            idle[i] = Sfx.Near(b.transform.position) > 30f ? idle[i] + dt : 0f;
            if (idle[i] > 20f) { idle[i] = 0f; b.ResetHome(); }
        }
        t -= dt;
        if (t > 0f || goer != null || Story.Active) return;
        t = Random.Range(40f, 80f);
        foreach (Bike b in Bike.AllBikes) if (b.aiRider != null) return;   // one AI rider at a time
        if (Random.value > 0.45f) return;
        var cands = new List<Frog>();
        foreach (Frog f in g.frogs) if (f != null && f.AiIdleOnRanch && (f.transform.position - new Vector3(Layout.GarageC.x, f.transform.position.y, Layout.GarageC.y)).magnitude < 70f) cands.Add(f);
        if (cands.Count == 0) return;
        var free = new List<Bike>();
        foreach (Bike b in Bike.AllBikes) if (b.driver == null && !b.Wrecked && (b.transform.position - b.HomePos).magnitude < 3f) free.Add(b);
        if (free.Count == 0) return;
        goer = cands[Random.Range(0, cands.Count)]; target = free[Random.Range(0, free.Count)]; walkT = 0f;
        Debug.Log("BikeAI: " + goer.nick + " heads for the " + target.Title);
    }

    // the AI's riding: out of the garage (north), 2 laps round the front yard, stop, hop off
    public static PIn Ride(Bike b, float dt)
    {
        var o = new PIn();
        Frog f = b.aiRider;
        b.aiT += dt;
        Vector3 p = b.transform.position;
        Vector2 p2 = new Vector2(p.x, p.z);
        Vector2 tgt;
        float vt = 8f;
        if (b.aiPhase == 0)
        {
            tgt = new Vector2(b.HomePos.x, 40f);
            if (p.z > 37f || b.aiT > 15f) { b.aiPhase = 1; b.aiAng = Mathf.Atan2(p.z - LoopC.y, p.x - LoopC.x); }
        }
        else if (b.aiPhase == 1)
        {
            float a = Mathf.Atan2(p.z - LoopC.y, p.x - LoopC.x);
            float da = Mathf.DeltaAngle(b.aiAng * Mathf.Rad2Deg, a * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            b.aiTurned += Mathf.Abs(da); b.aiAng = a;
            float ahead = a + 0.6f;   // counter-clockwise
            tgt = LoopC + new Vector2(Mathf.Cos(ahead), Mathf.Sin(ahead)) * LoopR;
            vt = 10f;
            if (b.aiTurned > Mathf.PI * 4f || b.aiT > 70f) b.aiPhase = 2;
        }
        else
        {
            tgt = p2 + new Vector2(b.transform.forward.x, b.transform.forward.z);
            vt = 0f;
            if (b.Speed < 0.5f) { b.aiRider = null; if (f != null && f.vehicle == b) f.ExitVehicle(); return o; }
        }
        Vector2 fw = new Vector2(b.transform.forward.x, b.transform.forward.z).normalized, to = (tgt - p2).normalized;
        float ang = Mathf.Atan2(fw.x * to.y - fw.y * to.x, Vector2.Dot(fw, to)) * Mathf.Rad2Deg;
        o.move = new Vector2(Mathf.Clamp(-ang / 30f, -1f, 1f), 0f);
        float fs = b.ForwardSpeed;
        if (vt > fs) o.gas = Mathf.Clamp01((vt - fs) * 0.3f + 0.2f); else o.brake = Mathf.Clamp01((fs - vt) * 0.4f);
        if (f == null || f.vehicle != b) b.aiRider = null;
        // stuck against a tree / wall: give up, hop off (the bike goes home later)
        if (Mathf.Abs(fs) < 0.3f && o.gas > 0.2f) b.aiStuck += dt; else b.aiStuck = 0f;
        if (b.aiStuck > 3f) { b.aiStuck = 0f; b.aiRider = null; if (f != null && f.vehicle == b) f.ExitVehicle(); }
        return o;
    }
}
