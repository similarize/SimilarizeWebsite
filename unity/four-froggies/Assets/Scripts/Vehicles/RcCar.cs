using UnityEngine;

// ffu30 RC car (RcTrack.cs): a 1/5-scale-ish buggy / truggy a froggy drives from the driver's stand. It is a Vehicle so
// the usual enter / exit / split-screen / input plumbing works; its `seat` is the froggy's spot on the stand, so the froggy
// stands there holding a transmitter while the slot camera chases the car.
// Arcade-real handling: raycast spring per wheel (independent bounce + squat), quick and grippy with a capped lateral
// grip (slides / drifts, more with power on in a turn), wheelies on hard throttle from low speed, real-RC air control
// (throttle = nose up, brake = nose down, stick = yaw), spinning + steering wheels, shocks that stretch, the body shell
// rolling / pitching on its own spring, a whip antenna, dirt roost + dust, and an automatic flip-back when it lands upside
// down. Voice: SynthVoice kind 13 (high brushless whine + gravel), louder the closer a player is.
public class RcCar : Vehicle
{
    public RcTrack track;
    public int index;
    public bool Parked { get; private set; }
    class W { public Vector3 mount; public float r, dist; public bool grounded, front; public Transform vis, shock, arm; public float spin; public Vector3 tower; public Vector3 axle0, vs0; public float r0, extra, sr, wrest; }
    readonly W[] wh = new W[4];
    float rest = 0.14f, spring, damper;
    public float vmax = 13.5f, accel = 15f, latCap = 13f, turnMax = 3.4f;
    float steer, throttle, flipT, roostT, stuckT, revT, skill = 1f, laneSeed;
    int ground, aiIdx = -1, lastIdx = -1;
    Transform shell, antenna;
    Vector3 lastVel, accLocal;
    float shellRoll, shellRollV, shellPitch, shellPitchV, antX, antZ, antXV, antZV;
    SynthVoice voice;
    public int laps; public float lapStart = -1f, lastLap, bestLap;
    public bool airborne; public float airT;
    public bool demoAuto;    // rctrack demo: autopilot even with P1 holding the remote
    public string VoiceLevels { get { return voice != null ? voice.Levels() : "-"; } }

    public static RcCar Spawn(RcTrack t, int i, Vector3 pos, float yaw)
    {
        var go = new GameObject("RC car " + i);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        var c = go.AddComponent<RcCar>();
        c.track = t; c.index = i;
        bool truggy = i % 2 == 1;
        c.Title = Froggies.Names[i] + "'s RC " + (truggy ? "truggy" : "buggy");
        c.EnterVerb = "drive " + Froggies.Names[i] + "'s RC car";
        c.SetupBodyPublic(4.5f, new Vector3(0f, 0.25f, 0f), new Vector3(0.48f, 0.18f, 0.84f), new Vector3(0f, 0.07f, -0.02f));
        c.rb.angularDrag = 1.5f;
        c.seat = t.seats[i];
        c.seatScale = 1f; c.showDriver = true;
        c.camDistance = 3.1f; c.camHeight = 1.15f;
        c.engineKind = 13; c.enginePitch = 0.95f + 0.05f * i;
        c.skill = new float[] { 1f, 0.94f, 0.97f, 0.91f }[i];
        c.laneSeed = i * 1.7f;
        c.Build(Froggies.Color(i), truggy);
        int n = 4; float perWheel = c.rb.mass * 9.81f / n;
        c.spring = perWheel / (c.rest * 0.42f);
        c.damper = 2f * Mathf.Sqrt(c.spring * c.rb.mass / n) * 0.42f;
        Mats.SetLayer(go, VehicleLayer);
        c.Parked = true;
        return c;
    }

    void Build(Color paint, bool truggy)
    {
        Material body = Mats.Paint(paint, 0.9f), dark = Mats.Lit(new Color(0.08f, 0.08f, 0.09f)), tyre = Mats.Lit(new Color(0.05f, 0.05f, 0.055f));
        Material alu = Mats.Steel(new Color(0.62f, 0.64f, 0.7f)), gold = Mats.PBR(new Color(0.85f, 0.62f, 0.2f), 0.7f, 0.8f), white = Mats.Paint(new Color(0.95f, 0.95f, 0.95f), 0.8f);
        Material rimM = Mats.Paint(truggy ? new Color(0.95f, 0.95f, 0.95f) : new Color(0.98f, 0.76f, 0.1f), 0.6f), glass = Mats.Paint(new Color(0.08f, 0.1f, 0.14f), 0.95f);
        float r = truggy ? 0.15f : 0.13f, tw = truggy ? 0.3f : 0.27f, wz = 0.3f;
        var ch = Mats.Node(transform, "Chassis", Vector3.zero);
        // aluminium chassis plate, battery + ESC, front / rear bumpers, shock towers
        Mats.Prim(PrimitiveType.Cube, ch, new Vector3(0f, 0.13f, 0f), new Vector3(0.22f, 0.02f, 0.78f), alu);
        Mats.Prim(PrimitiveType.Cube, ch, new Vector3(0.06f, 0.17f, -0.02f), new Vector3(0.08f, 0.06f, 0.3f), dark);
        Mats.Prim(PrimitiveType.Cube, ch, new Vector3(-0.06f, 0.17f, 0.08f), new Vector3(0.07f, 0.05f, 0.08f), Mats.Lit(new Color(0.15f, 0.35f, 0.75f)));
        Mats.Prim(PrimitiveType.Cube, ch, new Vector3(0f, 0.14f, 0.45f), new Vector3(0.26f, 0.05f, 0.08f), dark);
        Mats.Prim(PrimitiveType.Cube, ch, new Vector3(0f, 0.16f, -0.43f), new Vector3(0.24f, 0.06f, 0.06f), dark);
        for (int s = -1; s <= 1; s += 2) for (int f = -1; f <= 1; f += 2)
            Mats.Prim(PrimitiveType.Cube, ch, new Vector3(0f, 0.27f, f * (wz - 0.02f)), new Vector3(0.36f, 0.025f, 0.04f), alu);
        Mats.Prim(PrimitiveType.Cylinder, ch, new Vector3(-0.03f, 0.2f, -0.2f), new Vector3(0.07f, 0.04f, 0.07f), new Vector3(0f, 0f, 90f), Mats.Steel(new Color(0.5f, 0.5f, 0.55f)));   // motor
        // body shell on its own (visual) spring
        shell = Mats.Node(transform, "Shell", new Vector3(0f, 0f, 0f));
        if (!truggy)
        {
            // 1/8 buggy: low cab-forward shell, side pods, rear wing on stalks
            var cab = Mats.Prim(PrimitiveType.Sphere, shell, new Vector3(0f, 0.26f, 0.02f), new Vector3(0.24f, 0.16f, 0.5f), body);
            Mats.Prim(PrimitiveType.Sphere, shell, new Vector3(0f, 0.3f, 0.1f), new Vector3(0.15f, 0.1f, 0.22f), glass);
            for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, shell, new Vector3(s * 0.12f, 0.2f, -0.02f), new Vector3(0.08f, 0.06f, 0.42f), body);
            Mats.Prim(PrimitiveType.Cube, shell, new Vector3(0f, 0.21f, 0.33f), new Vector3(0.14f, 0.04f, 0.2f), white);   // nose
            for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, shell, new Vector3(s * 0.06f, 0.33f, -0.36f), new Vector3(0.015f, 0.14f, 0.04f), dark);
            var wing = Mats.Prim(PrimitiveType.Cube, shell, new Vector3(0f, 0.41f, -0.4f), new Vector3(0.34f, 0.012f, 0.16f), body);
            wing.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
            for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, shell, new Vector3(s * 0.17f, 0.43f, -0.4f), new Vector3(0.01f, 0.06f, 0.17f), dark);
            Mats.Prim(PrimitiveType.Cube, shell, new Vector3(0f, 0.27f, 0.12f), new Vector3(0.05f, 0.004f, 0.3f), white);  // stripe
        }
        else
        {
            // truggy: chunkier shell over the chassis, roof scoop, big wing
            Mats.Prim(PrimitiveType.Cube, shell, new Vector3(0f, 0.25f, 0.02f), new Vector3(0.3f, 0.1f, 0.62f), body);
            var cab = Mats.Prim(PrimitiveType.Cube, shell, new Vector3(0f, 0.34f, -0.02f), new Vector3(0.24f, 0.1f, 0.28f), body);
            Mats.Prim(PrimitiveType.Cube, shell, new Vector3(0f, 0.345f, 0.08f), new Vector3(0.22f, 0.07f, 0.08f), glass);
            for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, shell, new Vector3(s * 0.121f, 0.345f, -0.02f), new Vector3(0.004f, 0.06f, 0.18f), glass);
            Mats.Prim(PrimitiveType.Cube, shell, new Vector3(0f, 0.3f, 0.29f), new Vector3(0.26f, 0.05f, 0.1f), dark);   // hood vents
            for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, shell, new Vector3(s * 0.07f, 0.37f, -0.36f), new Vector3(0.015f, 0.12f, 0.04f), dark);
            Mats.Prim(PrimitiveType.Cube, shell, new Vector3(0f, 0.44f, -0.38f), new Vector3(0.38f, 0.012f, 0.18f), white);
            Mats.Prim(PrimitiveType.Cube, shell, new Vector3(0f, 0.31f, 0f), new Vector3(0.302f, 0.02f, 0.5f), white);   // side stripe
        }
        // antenna (whip with a little flag)
        antenna = Mats.Node(transform, "Antenna", new Vector3(-0.08f, 0.22f, -0.12f));
        Mats.Prim(PrimitiveType.Cylinder, antenna, new Vector3(0f, 0.17f, 0f), new Vector3(0.008f, 0.17f, 0.008f), dark);
        Mats.Prim(PrimitiveType.Cube, antenna, new Vector3(0.025f, 0.32f, 0f), new Vector3(0.05f, 0.03f, 0.003f), Mats.Unlit(paint));
        // wheels, shocks, arms
        for (int i = 0; i < 4; i++)
        {
            bool front = i < 2; float sx = (i % 2 == 0) ? -1f : 1f;
            var w = new W { front = front, r = r, dist = rest + r };
            w.mount = new Vector3(sx * tw, 0.3f, front ? wz : -wz);
            w.tower = new Vector3(sx * 0.13f, 0.28f, front ? wz - 0.02f : -wz + 0.02f);
            w.vis = Mats.Node(transform, "Wheel", w.mount - Vector3.up * (rest * 0.6f));
            float wd = truggy ? 0.13f : 0.11f;
            Mats.Prim(PrimitiveType.Cylinder, w.vis, Vector3.zero, new Vector3(r * 2f, wd * 0.5f, r * 2f), new Vector3(0f, 0f, 90f), tyre);
            // knobs: little blocks round the tread so the spin reads
            for (int k = 0; k < 10; k++)
            {
                float a = k * 36f;
                var kb = Mats.Prim(PrimitiveType.Cube, w.vis, Quaternion.Euler(a, 0f, 0f) * new Vector3(0f, r, 0f), new Vector3(wd * 0.95f, 0.022f, 0.03f), tyre);
                kb.transform.localRotation = Quaternion.Euler(a, 0f, 0f);
            }
            Mats.Prim(PrimitiveType.Cylinder, w.vis, new Vector3(sx * wd * 0.27f, 0f, 0f), new Vector3(r * 1.25f, 0.012f, r * 1.25f), new Vector3(0f, 0f, 90f), rimM);
            Mats.Prim(PrimitiveType.Cube, w.vis, new Vector3(sx * wd * 0.29f, 0f, 0f), new Vector3(0.006f, r * 1.1f, 0.02f), dark);
            w.shock = Mats.Node(transform, "Shock", Vector3.zero);
            Mats.Prim(PrimitiveType.Cylinder, w.shock, Vector3.zero, new Vector3(0.035f, 1f, 0.035f), gold);
            Mats.Prim(PrimitiveType.Cylinder, w.shock, new Vector3(0f, 0.2f, 0f), new Vector3(0.05f, 0.35f, 0.05f), Mats.Paint(paint, 0.6f));
            w.arm = Mats.Node(transform, "Arm", Vector3.zero);
            Mats.Prim(PrimitiveType.Cube, w.arm, Vector3.zero, new Vector3(1f, 0.025f, 0.07f), dark);
            wh[i] = w;
        }
        if (Look.Mobile) Mats.NoShadows(gameObject);
    }

    public override string[] TouchSet { get { return new[] { "A", null, null, "GAS", "BRAKE" }; } }
    public override string HelpLine
    {
        get
        {
            string lap = laps > 0 ? "  |  lap " + laps + (bestLap > 0f ? "  best " + bestLap.ToString("0.0") + " s" : "") : "";
            return "RC " + Froggies.Names[index] + ": L-stick steer | RT throttle | LT brake / reverse | in the air RT nose up, LT nose down | A hand back the remote" + lap;
        }
    }
    public override bool CanEnter(Frog f) { return f.world == WorldId.Ranch && base.CanEnter(f); }
    public Vector3 ExitSpot { get { return seat.position - seat.forward * 0.5f + Vector3.up * 0.08f; } }

    float beachT, autoT;
    public int recovers;
    public void Recover()
    {
        if (track == null) return;
        Vector2 lp = new Vector2(transform.position.x - RcTrack.C.x, transform.position.z - RcTrack.C.y);
        float d; int i = track.NearestFast(lp, out d);
        var q = track.path[i];
        Vector3 p = track.W3(q.p, 0.3f);
        rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
        rb.position = p; rb.rotation = Quaternion.LookRotation(new Vector3(q.t.x, 0f, q.t.y));
        transform.position = p; transform.rotation = rb.rotation;
        aiIdx = i; recovers++;
        FX.Puff(p, Vector3.up, 0.5f, 0.6f, new Color(0.65f, 0.55f, 0.4f, 0.6f));
        if (driver != null && driver.human) driver.Toast("Back on the track!", 1.2f);
    }

    public void Park(Vector3 p, float yaw)
    {
        rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
        rb.position = p; rb.rotation = Quaternion.Euler(0f, yaw, 0f);
        transform.position = p; transform.rotation = rb.rotation;
        Parked = true; aiIdx = -1; laps = 0; lapStart = -1f;
    }

    protected override void OnCollisionEnter(Collision c)
    {
        float v = c.relativeVelocity.magnitude;
        if (v < 2.5f) return;
        Vector3 p = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
        Sfx.PlayAt(Sfx.BumpSoft != null ? Sfx.BumpSoft : Sfx.Click, p, Mathf.Clamp01(v / 12f) * 0.35f, 25f, Random.Range(1.5f, 1.9f));
    }

    // ---------------- physics ----------------
    protected override void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        PondPhysics.Step(this, dt);   // ffu37: sinks to the real pond bed
        bool auto = track != null && (track.AutoFor(this) || demoAuto);
        PIn ip = auto ? AiInput(dt) : (driver != null ? inp : new PIn());
        steer = Mathf.Clamp(ip.move.x, -1f, 1f);
        throttle = Mathf.Clamp(ip.move.y + ip.gas - ip.brake, -1f, 1f);
        if (ip.gas > 0.1f) throttle = Mathf.Max(throttle, ip.gas);
        bool driven = driver != null || auto;
        if (driven && rb.velocity.sqrMagnitude > 0.3f) Parked = false;

        Vector3 up = transform.up;
        ground = 0; Vector3 nsum = Vector3.zero; bool rearDown = false, frontDown = false;
        foreach (W w in wh)
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
                if (comp > wr * 0.9f) f += spring * 6f * (comp - wr * 0.9f);   // bump stop
                if (f > 0f) rb.AddForceAtPosition(up * f, o);
                ground++; nsum += h.normal;
                if (w.front) frontDown = true; else rearDown = true;
            }
            else { w.grounded = false; w.dist = Mathf.MoveTowards(w.dist, len, dt * 2f); }
        }
        float gf = ground / 4f;
        Vector3 n = ground > 0 ? nsum.normalized : Vector3.up;
        Vector3 v = rb.velocity;
        accLocal = transform.InverseTransformDirection((v - lastVel) / dt); lastVel = v;
        float fs = 0f, ls = 0f;
        airborne = ground == 0;
        airT = airborne ? airT + dt : 0f;
        if (ground > 0)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, n).normalized, right = Vector3.ProjectOnPlane(transform.right, n).normalized;
            fs = Vector3.Dot(v, fwd); ls = Vector3.Dot(v, right);
            float force;
            float rearK = rearDown ? 1f : 0.25f;                     // 4WD, but most of the shove is from the rear
            if (!driven) force = -fs * 4f;
            else if (throttle > 0.02f) force = fs < -0.5f ? 22f * throttle : accel * throttle * Mathf.Clamp01(1.12f - fs / vmax) * rearK;
            else if (throttle < -0.02f) force = fs > 0.5f ? -22f * -throttle : (fs > -5.5f ? accel * 0.6f * throttle : 0f);
            else force = -fs * 1.1f;
            rb.AddForce(fwd * force * rb.mass * gf, ForceMode.Force);
            float cap = latCap * (driven && throttle > 0.6f && Mathf.Abs(steer) > 0.4f && fs > 5f ? 0.6f : 1f);
            float lat = Mathf.Clamp(-ls * 10f, -cap, cap);
            rb.AddForce(right * lat * rb.mass * gf, ForceMode.Force);
            slipSpeed = ls; groundFrac = gf;
            float yr = YawRate(rb, transform);
            float tr = Mathf.Min(turnMax, latCap * 1.15f / Mathf.Max(1f, Mathf.Abs(fs)));   // grippy, a little past the limit = a small drift
            float target = driven ? steer * tr * Mathf.Clamp(fs / 2.2f, -1f, 1f) : 0f;
            rb.AddTorque(transform.up * (target - yr) * Mathf.Min(1f, 10f * dt) * gf, ForceMode.VelocityChange);
            rb.AddForce(-n * 3f * gf, ForceMode.Acceleration);
            // wheelies: hard throttle at low speed lifts the nose (held up to ~32 deg, never loops over)
            float pitchUp = Mathf.Asin(Mathf.Clamp(Vector3.Dot(transform.forward, n), -1f, 1f)) * Mathf.Rad2Deg;   // + = nose up relative to the ground
            if (driven && rearDown && throttle > 0.6f && fs > -0.3f && fs < 7.5f && pitchUp < 30f)
                rb.AddTorque(-transform.right * 26f * throttle * (1f - fs / 7.5f), ForceMode.Acceleration);
            if (!frontDown && pitchUp > 36f) rb.AddTorque(transform.right * 40f, ForceMode.Acceleration);
            // roost + dust
            roostT -= dt;
            if (roostT <= 0f && (Mathf.Abs(fs) > 2.5f || (driven && throttle > 0.5f)))
            {
                roostT = 0.045f;
                foreach (W w in wh)
                {
                    if (!w.grounded || (w.front && Random.value < 0.6f)) continue;
                    Vector3 cp = transform.TransformPoint(w.mount) - up * w.dist;
                    float k = Mathf.Clamp01(Mathf.Abs(fs) / 10f + (driven ? throttle * 0.4f : 0f) + Mathf.Abs(ls) * 0.1f);
                    if (Random.value > k) continue;
                    Vector3 back = -transform.forward * (2f + throttle * 2.5f) + Vector3.up * (1.2f + Random.value * 1.4f) + Random.insideUnitSphere * 0.8f;
                    FX.Puff(cp + Vector3.up * 0.05f, back, Random.Range(0.18f, 0.38f), Random.Range(0.5f, 0.9f), new Color(0.6f, 0.47f, 0.32f, 0.6f));
                }
            }
        }
        else
        {
            // real-RC air control: throttle spins the wheels = nose up, brake = nose down; stick yaws
            if (driven)
            {
                rb.AddTorque(-transform.right * throttle * 9f + transform.up * steer * 5f, ForceMode.Acceleration);
                Vector3 roll = Vector3.Project(Vector3.Cross(transform.up, Vector3.up), transform.forward);
                rb.AddTorque(roll * 6f, ForceMode.Acceleration);
            }
        }
        // landed on its lid / side: flip back over
        if (transform.up.y < 0.25f && rb.velocity.magnitude < 2.5f) flipT += dt; else flipT = 0f;
        if (flipT > 0.8f)
        {
            flipT = 0f;
            rb.angularVelocity = Vector3.zero;
            rb.position += Vector3.up * 0.35f;
            rb.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            rb.velocity = Vector3.up * 1.6f;
            FX.Puff(transform.position, Vector3.up, 0.5f, 0.6f, new Color(0.65f, 0.55f, 0.4f, 0.6f));
            Sfx.PlayAt(Sfx.Hop, transform.position, 0.4f, 30f, 1.6f);
        }
        // ffu30b beached (on its side against the fence, high-centred, wheels in the air) and not moving: back onto the
        // track at the nearest point, facing the way round
        bool beached = (ground == 0 || transform.up.y < 0.6f) && rb.velocity.magnitude < 0.6f && !Parked;
        beachT = beached ? beachT + dt : 0f;
        if (beachT > 1.2f) { beachT = 0f; Recover(); }
        if (driven) autoT += dt; else autoT = 0f;
        // fell off the world / far off the pad: back to the grid
        Vector3 rp = rb.position;
        if (track != null && (rp.y < track.BaseY - 8f || new Vector2(rp.x - RcTrack.C.x, rp.z - RcTrack.C.y).magnitude > 70f))
            Park(track.Grid[index], 90f);
        Laps();
        // voice
        if (voice == null) voice = new SynthVoice(gameObject, 13, enginePitch);
        float near = Sfx.Near(transform.position);
        float gk = Mathf.Clamp01(1f - near / 38f); gk *= gk;
        voice.gain = gk * 0.9f;
        voice.Tick(driven || rb.velocity.sqrMagnitude > 0.5f, Mathf.Abs(Vector3.Dot(v, transform.forward)), driven ? Mathf.Abs(throttle) : 0f, ls, gf, dt);
    }

    void Laps()
    {
        if (track == null) return;
        Vector2 lp = new Vector2(transform.position.x - RcTrack.C.x, transform.position.z - RcTrack.C.y);
        int idx = lastIdx < 0 ? track.Nearest(lp) : track.NearestFrom(lp, lastIdx, 10);
        int m = track.path.Count, st = track.startIdx;
        if (lastIdx >= 0)
        {
            int a = (lastIdx - st + m) % m, b = (idx - st + m) % m;
            if (a > m - 30 && b < 30)
            {
                if (lapStart > 0f) { lastLap = Time.time - lapStart; if (bestLap <= 0f || lastLap < bestLap) bestLap = lastLap; laps++; if (driver != null && driver.human) driver.Toast("Lap " + laps + "  " + lastLap.ToString("0.0") + " s" + (lastLap <= bestLap ? "  BEST!" : ""), 2f); }
                lapStart = Time.time;
            }
        }
        lastIdx = idx;
    }

    // ---------------- autopilot (AI froggies + spare cars) ----------------
    PIn AiInput(float dt)
    {
        var o = new PIn();
        if (track == null) return o;
        var path = track.path; int m = path.Count;
        Vector3 p3 = transform.position;
        Vector2 lp = new Vector2(p3.x - RcTrack.C.x, p3.z - RcTrack.C.y);
        float off;
        if (aiIdx < 0) aiIdx = track.NearestFast(lp, out off);
        else { aiIdx = track.NearestFrom(lp, aiIdx, 16); off = (path[aiIdx].p - lp).magnitude; }
        float fs = Vector3.Dot(rb.velocity, transform.forward);
        if (airborne)
        {
            // level the car for the landing (nose up = brake, nose down = throttle)
            float pitch = -Mathf.Asin(Mathf.Clamp(transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;   // + = nose down
            float want = 6f;                                                                        // land slightly nose-down... rear first feels better
            o.gas = Mathf.Clamp01((pitch - want) / 20f);     // nose too low -> throttle lifts it
            o.brake = Mathf.Clamp01((want - pitch) / 20f);   // nose too high -> brake drops it
            return o;
        }
        int look = Mathf.RoundToInt((1.3f + Mathf.Abs(fs) * 0.26f) / 0.25f);
        Samp(path, (aiIdx + look) % m, out Vector2 tp, out Vector2 tt);
        float lane = Mathf.Sin(path[aiIdx].s * 0.13f + laneSeed) * 0.55f;
        tp += new Vector2(-tt.y, tt.x) * lane;
        if (off > 2.4f) tp = path[(aiIdx + 4) % m].p;             // off the track: head back to it
        Vector2 f2 = new Vector2(transform.forward.x, transform.forward.z).normalized;
        Vector2 to = (tp - lp).normalized;
        float ang = Mathf.Atan2(f2.x * to.y - f2.y * to.x, Vector2.Dot(f2, to)) * Mathf.Rad2Deg;   // + = target to the left
        float st = Mathf.Clamp(-ang / 28f, -1f, 1f);
        // speed from the tightest bend in the next ~5 m, a bit easier before jumps / whoops
        float kmax = 0.02f, hmax = 0f;
        for (int j = 2; j < 22; j++) { var q = path[(aiIdx + j) % m]; kmax = Mathf.Max(kmax, Mathf.Abs(q.k)); hmax = Mathf.Max(hmax, q.h); }
        float vt = Mathf.Min(vmax * skill, Mathf.Sqrt(10.5f * skill / kmax));
        if (hmax > 0.3f) vt = Mathf.Min(vt, 8.5f * skill);
        if (hmax > 0.08f && hmax < 0.2f) vt = Mathf.Min(vt, 9.5f * skill);
        float th = Mathf.Clamp((vt - fs) * 0.6f + 0.25f, -1f, 1f);
        if (autoT < index * 0.45f && laps == 0) th = -0.2f;   // ffu30b: staggered start (no four-wide pile-up into turn 1)
        // stuck against a pipe / car: back out
        if (revT > 0f) { revT -= dt; o.move = new Vector2(-st, 0f); o.brake = 1f; return o; }
        if (Mathf.Abs(fs) < 0.35f && th > 0.3f && !Parked) stuckT += dt; else stuckT = 0f;
        if (stuckT > 1.3f) { stuckT = 0f; revT = 0.9f; }
        o.move = new Vector2(st, 0f);
        if (th >= 0f) o.gas = th; else o.brake = -th;
        return o;
    }
    static void Samp(System.Collections.Generic.List<RcTrack.Samp> path, int i, out Vector2 p, out Vector2 t) { p = path[i].p; t = path[i].t; }

    // ---------------- visuals ----------------
    // ---------------- ffu42 big wheels (BigWheels.cs): monster RC ----------------
    [System.NonSerialized] public float bigK = 1f;
    bool bigReady; float rest0, spring0, damper0, cam0, camH0, zc0, gap0, rmax0; Vector3 com0;
    void BigTick(float dt)
    {
        if (rb == null || wh[0] == null) return;
        if (!bigReady)
        {
            bigReady = true;
            rest0 = rest; spring0 = spring; damper0 = damper; com0 = rb.centerOfMass; cam0 = camDistance; camH0 = camHeight;
            var ax = new System.Collections.Generic.List<Vector3>();
            foreach (W w in wh) { w.axle0 = w.mount - Vector3.up * rest; w.r0 = w.r; w.vs0 = w.vis != null ? w.vis.localScale : Vector3.one; w.wrest = rest; zc0 += w.axle0.z * 0.25f; ax.Add(w.axle0); rmax0 = Mathf.Max(rmax0, w.r); }
            gap0 = BigWheels.MinGap(ax);
        }
        float t = rb.isKinematic ? 1f : BigWheels.S;
        float k = BigWheels.Ease(bigK, t, dt);
        if (Mathf.Abs(k - bigK) < 1e-5f) return;
        bigK = k;
        float fz = BigWheels.ZFactor(bigK, rmax0, gap0), lift = 0f, restK = 0f;
        foreach (W w in wh)
        {
            var g = BigWheels.Wheel(bigK, w.axle0, w.r0, rest0, zc0, fz);
            w.r = g.r; w.mount = g.mount; w.extra = g.extra; w.sr = g.sr; w.wrest = g.rest;
            BigWheels.Vis(w.vis, w.vs0, g);
            lift += g.lift * 0.25f; restK += g.rest * 0.25f;
            if (!w.grounded) w.dist = g.rest + g.r + g.extra;
        }
        bigLift = bigK > 1.0001f ? lift : 0f; bigPad = Mathf.Max(0f, (wh[0].r - wh[0].r0) * 0.6f);
        float rr = rest0 / Mathf.Max(0.01f, restK);
        spring = spring0 * rr; damper = damper0 * Mathf.Sqrt(rr);
        rb.centerOfMass = com0 - Vector3.up * bigLift * BigWheels.ComCut;
        camDistance = cam0 * (1f + (bigK - 1f) * 0.25f); camHeight = camH0 + bigLift * 0.8f;
        rb.WakeUp();
    }

    protected virtual void Update()
    {
        float dt = Time.deltaTime;
        BigTick(Mathf.Min(dt, 0.1f));
        float fs = ForwardSpeed;
        foreach (W w in wh)
        {
            Vector3 hub = w.mount - Vector3.up * (w.dist - w.r);
            w.spin += (w.grounded || driver != null ? fs + (w.grounded ? 0f : throttle * 12f) : fs) / w.r * Mathf.Rad2Deg * dt;
            float sa = w.front ? steer * 26f : 0f;
            w.vis.localPosition = hub;
            w.vis.localRotation = Quaternion.Euler(0f, sa, 0f) * Quaternion.Euler(w.spin, 0f, 0f);
            // shock: tower -> hub (slightly inboard), stretches with travel
            Vector3 lower = hub + new Vector3(-Mathf.Sign(hub.x) * 0.06f, 0f, 0f);
            Vector3 d = w.tower - lower; float len = d.magnitude;
            w.shock.localPosition = (w.tower + lower) * 0.5f;
            w.shock.localRotation = Quaternion.FromToRotation(Vector3.up, d / Mathf.Max(0.001f, len));
            w.shock.localScale = new Vector3(1f, len * 0.5f, 1f);
            // lower arm: chassis -> hub
            Vector3 inner = new Vector3(Mathf.Sign(hub.x) * 0.07f, 0.13f, hub.z);
            Vector3 a = hub - inner; float al = a.magnitude;
            w.arm.localPosition = (hub + inner) * 0.5f;
            w.arm.localRotation = Quaternion.FromToRotation(Vector3.right, a / Mathf.Max(0.001f, al));
            w.arm.localScale = new Vector3(al, 1f, 1f);
        }
        // body shell rolls / pitches on its own soft spring (squat under power, dive under braking)
        float rollT = Mathf.Clamp(accLocal.x * 1.1f, -9f, 9f), pitchT = Mathf.Clamp(-accLocal.z * 0.9f, -7f, 7f);
        if (airborne) { rollT = 0f; pitchT = 0f; }
        Spring(ref shellRoll, ref shellRollV, rollT, dt, 90f, 9f);
        Spring(ref shellPitch, ref shellPitchV, pitchT, dt, 90f, 9f);
        float comp = 0f; foreach (W w in wh) comp += ((bigK > 1.0001f ? w.wrest + w.extra : rest) + w.r - w.dist); comp /= 4f;
        shell.localPosition = new Vector3(0f, -comp * 0.35f, 0f);
        shell.localRotation = Quaternion.Euler(shellPitch, 0f, -shellRoll);
        Spring(ref antX, ref antXV, Mathf.Clamp(accLocal.z * 2.5f, -30f, 30f), dt, 60f, 3f);
        Spring(ref antZ, ref antZV, Mathf.Clamp(-accLocal.x * 2.5f, -30f, 30f), dt, 60f, 3f);
        antenna.localRotation = Quaternion.Euler(antX, 0f, antZ);
    }
    static void Spring(ref float x, ref float v, float target, float dt, float k, float c)
    {
        dt = Mathf.Min(dt, 0.05f);
        v += ((target - x) * k - v * c) * dt; x += v * dt;
    }

    // the froggy on the stand: standing, holding the transmitter, turned to watch its car
    void LateUpdate()
    {
        if (driver == null || seat == null) return;
        Vector3 to = transform.position - seat.position; to.y = 0f;
        if (to.sqrMagnitude > 0.5f)
        {
            float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            seat.rotation = Quaternion.Slerp(seat.rotation, Quaternion.Euler(0f, yaw, 0f), Mathf.Min(1f, Time.deltaTime * 4f));
        }
        var m = driver.model;
        if (m == null) return;
        if (m.legL != null) m.legL.localRotation = Quaternion.identity;
        if (m.legR != null) m.legR.localRotation = Quaternion.identity;
        float tw = Mathf.Sin(Time.time * 7f) * 4f * Mathf.Abs(steer);
        if (m.armL != null) m.armL.localRotation = Quaternion.Euler(-68f + tw, 0f, 0f);
        if (m.armR != null) m.armR.localRotation = Quaternion.Euler(-68f - tw, 0f, 0f);
        if (m.bob != null) m.bob.localRotation = Quaternion.Euler(4f, 0f, -steer * 6f);
    }
}
