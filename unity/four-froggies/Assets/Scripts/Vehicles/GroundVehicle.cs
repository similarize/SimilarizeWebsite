using System.Collections.Generic;
using UnityEngine;

// Raycast-suspension ground vehicle: springs at each wheel / track roller, drive force along the ground,
// lateral grip, and yaw steering (Ackermann-ish for wheels, skid-steer for tracks and the mech).
public class GroundVehicle : Vehicle
{
    public class Wheel
    {
        public Vector3 mount;       // local mount point (top of suspension travel)
        public float radius;
        public Transform visual;    // optional
        public bool steers;
        public float dist;          // last hit distance
        public bool grounded;
        public float spin;
        // ffu42 big wheels: stock axle / radius / visual scale, front flag, and the current extra ray length, sphere
        // radius and travel (BigWheels.Wheel)
        public Vector3 axle0, vs0 = Vector3.one;
        public float r0, extra, sr, wrest;
        public bool front;
    }

    public readonly List<Wheel> wheels = new List<Wheel>();
    public float rest = 0.35f, maxSpeed = 24f, accel = 12f, turnRate = 1.6f, grip = 7f, reverseFrac = 0.45f;
    public bool tracked;
    public override string[] TouchSet { get { return usesTriggers ? new[] { "A", null, amph != null ? (amph.Boat ? "TRUCK" : "BOAT") : null, "GAS", "BRAKE" } : new[] { "A", null, null, null, null }; } }
    public bool usesTriggers = true;     // tank uses RT to fire, so it drives with the stick only
    public float dustAmount = 0.5f;
    protected float spring, damper;
    protected float throttle, steer;
    protected int groundedCount;
    protected Vector3 groundNormal = Vector3.up;
    bool loopLoaded;                     // ffu19: last step was on the loop surface (stiffer springs)
    public List<Transform> treadMarks;   // optional moving track blocks
    public float treadHalf = 2.6f;
    public System.Action<float, float> animate;
    public CyberBoat amph;               // ffu11: amphibious Cybertruck (null for everything else)
    public System.Action<GroundVehicle, int> fixedProbe;   // ffu19: demo physics probe (gets the wheels-on-ground count each step)
    // ffu28: walkers (the Optimus mech suit): bump stop near the end of travel, stay upright, and if the body ever ends up
    // with its spring origins under the terrain (rays start below the surface and miss -> no spring, no drive -> the old
    // "stuck in the floor up to the knees" bug) lift it back onto the surface
    public bool walker;
    float sinkT;
    public float ThrottleRaw { get { return driver != null ? throttle : 0f; } }
    public float Throttle01 { get { return driver != null ? Mathf.Clamp01(throttle) : 0f; } }
    public override string HelpLine
    {
        get { return amph != null && amph.Boat ? "CYBERBOAT! L-stick steer | RT jet | LT reverse | Y / F truck mode | A hop out (swim)" : amph != null ? base.HelpLine + " | Y / F Cyberboat mode" : base.HelpLine; }
    }   // (forwardSpeed, dt) for custom visuals like mech legs

    public void AddWheel(Vector3 center, float radius, Transform visual, bool steers)
    {
        wheels.Add(new Wheel { mount = center + Vector3.up * rest, radius = radius, visual = visual, steers = steers, dist = rest + radius, r0 = radius, vs0 = visual != null ? visual.localScale : Vector3.one });
    }

    public void FinishSetup()
    {
        int n = Mathf.Max(1, wheels.Count);
        float perWheel = rb.mass * 9.81f / n;
        spring = perWheel / (rest * 0.45f);
        damper = 2f * Mathf.Sqrt(spring * rb.mass / n) * 0.35f;
    }

    public override void Drive(PIn i, float camYaw, float dt)
    {
        base.Drive(i, camYaw, dt);
    }

    protected virtual void ComputeControls()
    {
        steer = inp.move.x;
        throttle = usesTriggers ? Mathf.Clamp(inp.move.y + inp.gas - inp.brake, -1f, 1f) : Mathf.Clamp(inp.move.y, -1f, 1f);
        if (usesTriggers && inp.gas > 0.1f) throttle = Mathf.Max(throttle, inp.gas);
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        float dt = Time.fixedDeltaTime;
        ComputeControls();
        float ws = 1f;
        if (amph != null)
        {
            if (driver != null && (inp.alt || inp.land)) amph.button = true;   // ffu37: BOAT button
            amph.Step(dt, steer, throttle);
            ws = amph.WheelScale;
        }
        groundedCount = 0;
        slipSpeed = 0f;
        Vector3 nsum = Vector3.zero;
        Vector3 up = transform.up;
        int mask = GroundMask;
        bool onStunt = false, onTrack = false;
        // ffu19: in the loop the suspension stiffens as the surface tilts (up to 2.5x), so the high-g bottom of the loop
        // (5 g at 22 m/s) cannot bottom the springs out and drag the body along the surface
        float sm = 1f, dm = 1f;
        if (loopLoaded && !RallyTrack.Legacy) { sm = 1f + 1.5f * Mathf.Clamp01((1f - groundNormal.y) / 0.15f); dm = Mathf.Sqrt(sm); }
        foreach (Wheel w in wheels)
        {
            Vector3 origin = transform.TransformPoint(w.mount);
            RaycastHit hit;
            bool big = bigK > 1.0001f;
            float wr = big ? w.wrest : rest;
            float len = wr + w.radius + (big ? w.extra : 0f);
            float hd;
            bool hitOk;
            if (big) hitOk = BigWheels.Cast(origin, up, len, w.sr, mask, out hit, out hd);   // ffu42: sphere-cast tyre
            else { hitOk = Physics.Raycast(origin, -up, out hit, len, mask, QueryTriggerInteraction.Ignore); hd = hitOk ? hit.distance : len; }
            if (hitOk)
            {
                w.grounded = true;
                w.dist = hd;
                float comp = len - hd;
                if (big) comp = Mathf.Min(comp, wr * 1.35f);   // a rock under the tyre lifts it, never a launch
                float vUp = Vector3.Dot(rb.GetPointVelocity(origin), up);
                float f = spring * sm * comp - damper * dm * vUp;
                if (walker && comp > rest * 0.85f) f += spring * 5f * (comp - rest * 0.85f);   // ffu28 bump stop
                else if (loopLoaded && comp > wr * 0.85f) f += spring * 4f * (comp - wr * 0.85f);   // ffu43: the loop surface no longer holds the body - springs must not bottom out
                if (f < 0f) f = 0f;
                rb.AddForceAtPosition(up * f * ws, origin);
                groundedCount++;
                nsum += hit.normal;
                int hl = hit.collider.gameObject.layer;
                if (RallyTrack.IsStunt(hl)) onStunt = true;
                else if (hl == RallyTrack.TrackLayer) onTrack = true;
            }
            else
            {
                w.grounded = false;
                w.dist = Mathf.MoveTowards(w.dist, len, dt * 3f);
            }
        }
        if (walker && !rb.isKinematic) WalkerAssist(dt);
        if (bigK > 1.01f && !rb.isKinematic) BigAntiRoll(dt);
        float gf = (wheels.Count > 0 ? groundedCount / (float)wheels.Count : 0f) * ws;
        groundNormal = groundedCount > 0 ? nsum.normalized : Vector3.up;
        bool driven = driver != null;

        // rally assists: the loop holds you on (downforce + a minimum speed while you keep the throttle on),
        // banked turns get a little extra stick so the trucks can lean on the berms
        bool inLoop = false;
        Bounds zone = default(Bounds);
        float guideYaw = 0f;
        foreach (Bounds b in RallyTrack.LoopZones) if (b.Contains(rb.position)) { inLoop = true; zone = b; }
        // ffu19: continuous collision sweeps the body box along a straight line each step; inside the loop that line runs
        // into the surface curving up ahead and PhysX stops the truck dead (seen in the loopdrive probe: 16 -> 4 m/s at the
        // vertical). Discrete contacts in the loop zone (springs keep the body ~0.4 m off the surface), continuous elsewhere.
        if (!RallyTrack.Legacy && !rb.isKinematic)
        {
            var want = inLoop ? CollisionDetectionMode.Discrete : CollisionDetectionMode.Continuous;
            if (rb.collisionDetectionMode != want) rb.collisionDetectionMode = want;
        }
        if (inLoop)
        {
            if (groundedCount > 0 && onStunt)
            {
                // ffu19: the hold-on force fades in as the loop climbs (none on the flat start of the ease-in), so there is no
                // sudden squat where the straight turns into the loop
                // (only on the upper half, where it is needed - lower down it would just add to the spring load)
                float kd = RallyTrack.Legacy ? 1f : Mathf.Clamp01((0.35f - groundNormal.y) / 0.6f);
                rb.AddForce(-groundNormal * 17f * kd, ForceMode.Acceleration);
                Vector3 lf = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
                float lfs = Vector3.Dot(rb.velocity, lf);
                if (driven && throttle > -0.1f && lfs < 17f && lfs > -1f) rb.AddForce(lf * 11f, ForceMode.Acceleration);
                // ffu19: gentle governor above 19 m/s so a flat-out truck does not hit the loop at 6+ g
                if (!RallyTrack.Legacy && lfs > 19f) rb.AddForce(-lf * Mathf.Min(8f, (lfs - 19f) * 1.5f), ForceMode.Acceleration);
                // ffu19: keep the truck on the lane while the loop shifts sideways (yaw toward the lane + gentle centring)
                Vector3 gt; float glat;
                if (driven && lfs > 2f && RallyTrack.LoopGuide(rb.position, lf, out gt, out glat))   // ffu43: either direction
                {
                    Vector3 tn = Vector3.ProjectOnPlane(gt, groundNormal).normalized;
                    float ang = Vector3.SignedAngle(lf, tn, groundNormal) * Mathf.Deg2Rad;
                    guideYaw = Mathf.Clamp(ang * 3f - glat * 0.35f, -1.5f, 1.5f);
                    Vector3 gr = Vector3.Cross(groundNormal, tn).normalized;
                    rb.AddForce(-gr * Mathf.Clamp(glat, -2f, 2f) * 2.5f, ForceMode.Acceleration);
                }
            }
            else if (groundedCount == 0)
            {
                Vector3 radial = rb.position - zone.center; radial.z = 0f;
                if (radial.y > -1f) rb.AddForce(radial.normalized * 15f, ForceMode.Acceleration);
            }
        }
        else if (onTrack && groundedCount > 0) rb.AddForce(-groundNormal * 4f, ForceMode.Acceleration);
        else if (onStunt && groundedCount > 0)
        {
            // ffu43 Monster Park stunt surfaces (wall of death, half-pipe): stick to the surface while there is speed, scaled
            // by how steep it is; slow down and gravity wins (you slide / fall off). Lateral tyre grip below holds the wall.
            float spd = rb.velocity.magnitude;
            float steep = Mathf.Clamp01((1f - groundNormal.y) / 0.6f);
            rb.AddForce(-groundNormal * 14f * steep * Mathf.Clamp01((spd - 4f) / 5f), ForceMode.Acceleration);
        }
        loopLoaded = inLoop && onStunt && groundedCount > 0;
        // ffu44: U-turn braking zone on the loop branch (RallyTrack.UTurnBrake)
        if (driven && groundedCount > 0 && (onTrack || onStunt) && !rb.isKinematic)
        {
            float ub = RallyTrack.UTurnBrake(rb.position, rb.velocity);
            if (ub > 0f) rb.AddForce(-rb.velocity.normalized * ub, ForceMode.Acceleration);
        }

        if (groundedCount > 0 && ws > 0f)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
            Vector3 right = Vector3.ProjectOnPlane(transform.right, groundNormal).normalized;
            Vector3 v = rb.velocity;
            float fs = Vector3.Dot(v, fwd);
            float ls = Vector3.Dot(v, right);
            slipSpeed = ls; groundFrac = wheels.Count > 0 ? groundedCount / (float)wheels.Count : 1f;

            float force = 0f;
            if (!driven) force = -fs * 3f;   // parking brake
            else if (throttle > 0.02f) force = fs < maxSpeed ? accel * throttle * Mathf.Clamp01(1.15f - fs / maxSpeed) : 0f;
            else if (throttle < -0.02f) force = fs > 1f ? -accel * 1.6f * -throttle : (fs > -maxSpeed * reverseFrac ? accel * 0.7f * throttle : 0f);
            else force = -fs * 0.8f;
            if (pondSub > 0f) { force *= 1f - 0.5f * pondSub; if (driven && throttle > 0.02f) { float lim = maxSpeed * Mathf.Lerp(1f, 0.22f, pondSub); if (fs > lim) force = Mathf.Min(force, 0f); } }   // ffu37: crawling on the pond bed
            rb.AddForce(fwd * force * rb.mass * gf, ForceMode.Force);
            rb.AddForce(-right * ls * grip * rb.mass * gf, ForceMode.Force);

            float yr = YawRate(rb, transform);
            float target;
            if (tracked) target = steer * turnRate * (fs < -0.5f ? -1f : 1f);
            else target = steer * turnRate * Mathf.Clamp(fs / 6f, -1f, 1f);
            if (!driven) target = 0f;
            target += guideYaw;
            rb.AddTorque(transform.up * (target - yr) * Mathf.Min(1f, 8f * dt) * gf, ForceMode.VelocityChange);

            if (Mathf.Abs(fs) > 4f && dustAmount > 0f)
                foreach (Wheel w in wheels)
                    if (w.grounded)
                    {
                        Vector3 cp = transform.TransformPoint(w.mount) - up * (w.dist);
                        // ffu11: wheels churning through the pond shallows throw water, not dust
                        if (cp.y < Layout.WaterY - 1.5f && Layout.InPond(cp.x, cp.z)) { if (Random.value < 0.25f) FX.Bubble(cp + Vector3.up * 0.3f, 2); }   // ffu37: on the deep bed: silt bubbles
                        else if (cp.y < Layout.WaterY && Layout.InPond(cp.x, cp.z)) { if (Random.value < 0.35f) FX.Splash(new Vector3(cp.x, Layout.WaterY + 0.1f, cp.z), 1 + Mathf.Abs(fs) * 0.08f); }
                        else FX.Dust(cp, dustAmount * Mathf.Clamp01(Mathf.Abs(fs) / 20f) * 0.25f);
                    }
        }
        else if (ws > 0f)
        {
            // a little air control so jumps can be levelled
            rb.AddTorque(transform.up * steer * 0.6f * dt, ForceMode.VelocityChange);
        }
        if (fixedProbe != null) fixedProbe(this, groundedCount);
    }

    void WalkerAssist(float dt)
    {
        // stand upright like a person (no leaning into slopes / tipping onto the body box)
        Vector3 tilt = Vector3.Cross(transform.up, Vector3.up);
        Vector3 w = rb.angularVelocity; Vector3 wh = w - Vector3.up * w.y;
        rb.AddTorque(tilt * 14f - wh * 3f, ForceMode.Acceleration);
        // sunk: the feet (root) more than 0.35 m under the ranch terrain, or no spring ray finding ground while the root
        // is below the terrain -> lift onto the surface (Ranch.GY = the terrain itself, never a roof above)
        Vector3 p = rb.position;
        if (Mathf.Abs(p.x) > Layout.Half + 25f || Mathf.Abs(p.z) > Layout.Half + 25f || p.y < -60f) return;   // ranch only (other worlds sit 1.4+ km away)
        float gy = Ranch.GY(p.x, p.z);
        bool sunk = p.y < gy - 0.35f || (groundedCount == 0 && p.y < gy - 0.05f);
        sinkT = sunk ? sinkT + dt : 0f;
        if (sinkT > 0.1f)
        {
            sinkT = 0f;
            rb.position = new Vector3(p.x, gy + 0.15f, p.z);
            Vector3 v = rb.velocity; if (v.y < 0f) v.y = 0f; rb.velocity = v;
            Debug.Log("GroundVehicle: " + Title + " was " + (gy - p.y).ToString("0.00") + " m under the terrain - lifted");
        }
    }

    // ---------------- ffu42 big wheels ----------------
    [System.NonSerialized] public float bigK = 1f;
    bool bigReady;
    float rest0, spring0, damper0, cam0, camH0, pitch0, zc0, gap0, rmax0;
    Vector3 com0;
    public virtual bool BigOk { get { return !tracked && !walker && wheels.Count > 0; } }
    protected virtual float BigTarget()
    {
        if (!BigOk || stowed) return 1f;
        float t = BigWheels.Target(this, bigK, rmax0);   // ffu45: this vehicle's own size (building cap + clearance)
        if (amph != null) t = Mathf.Lerp(t, 1f, Mathf.Clamp01(amph.k * 2f));   // the Cyberboat tucks stock-size wheels away
        return t;
    }
    // called every frame (GroundVehicle.Update, RidingMower.Update); snap = jump straight to the target
    public void BigTick(float dt, bool snap = false)
    {
        if (rb == null || wheels.Count == 0) return;
        if (!bigReady)
        {
            bigReady = true;
            rest0 = rest; spring0 = spring; damper0 = damper; com0 = rb.centerOfMass;
            cam0 = camDistance; camH0 = camHeight; pitch0 = enginePitch;
            float zc = 0f;
            var ax = new List<Vector3>();
            foreach (Wheel w in wheels) { w.axle0 = w.mount - Vector3.up * rest; zc += w.axle0.z; ax.Add(w.axle0); rmax0 = Mathf.Max(rmax0, w.r0); }
            zc /= wheels.Count; zc0 = zc; gap0 = BigWheels.MinGap(ax);
            foreach (Wheel w in wheels) { w.front = w.axle0.z > zc; w.wrest = rest; }
        }
        float t = BigTarget();
        float k = snap ? t : BigWheels.Ease(bigK, t, dt);
        if (Mathf.Abs(k - bigK) < 1e-5f) return;
        bigK = k;
        ApplyBig();
    }
    void ApplyBig()
    {
        float lift = 0f, restK = 0f, pad = 0f, zrK = 1e9f, zr1 = 1e9f;
        float bodyHalf = body != null ? body.size.x * 0.5f : 1f;
        float fz = BigWheels.ZFactor(bigK, rmax0, gap0);
        foreach (Wheel w in wheels)
        {
            var g = BigWheels.Wheel(bigK, w.axle0, w.r0, rest0, zc0, fz);
            w.radius = g.r; w.mount = g.mount; w.extra = g.extra; w.sr = g.sr; w.wrest = g.rest;
            BigWheels.Vis(w.visual, w.vs0, g);
            lift += g.lift; restK += g.rest;
            Vector3 a = g.mount - Vector3.up * g.rest;
            pad = Mathf.Max(pad, Mathf.Abs(a.x) + g.r * 0.25f - Mathf.Max(bodyHalf, Mathf.Abs(w.axle0.x) + w.r0 * 0.25f));
            zrK = Mathf.Min(zrK, a.z - g.r); zr1 = Mathf.Min(zr1, w.axle0.z - w.r0);
        }
        lift /= wheels.Count; restK /= wheels.Count;
        bigLift = bigK > 1.0001f ? lift : 0f; bigPad = Mathf.Max(0f, pad); bigBack = Mathf.Max(0f, zr1 - zrK);
        float rr = rest0 / Mathf.Max(0.01f, restK);
        spring = spring0 * rr; damper = damper0 * Mathf.Sqrt(rr);   // same sag fraction on the longer travel
        if (!walker) rb.centerOfMass = com0 - Vector3.up * bigLift * BigWheels.ComCut;
        camDistance = cam0 * (1f + (bigK - 1f) * 0.22f); camHeight = camH0 + bigLift * 0.75f;
        if (Mathf.Abs(bigK - BigTarget()) < 0.001f) RetuneEngine(BigWheels.PitchFor(pitch0, bigK));   // once settled (rebuilds the voice)
        foreach (Wheel w in wheels) if (!w.grounded) w.dist = w.wrest + w.radius + w.extra;
        rb.WakeUp();
    }
    // big tyres: anti-roll bar (roll toward the ground plane, damped) + a little self-levelling in the air
    void BigAntiRoll(float dt)
    {
        float b = Mathf.Clamp01((bigK - 1f) / 1.5f);
        Vector3 n = groundedCount > 0 ? groundNormal : Vector3.up;
        float roll = Vector3.Dot(Vector3.Cross(transform.up, n), transform.forward);
        float rr = Vector3.Dot(rb.angularVelocity, transform.forward);
        float kk = groundedCount > 0 ? 9f : 3f;
        rb.AddTorque(transform.forward * (roll * kk - rr * 2.2f) * b, ForceMode.Acceleration);
    }

    protected virtual void Update()
    {
        float dt = Time.deltaTime;
        BigTick(Mathf.Min(dt, 0.1f));
        float fs = ForwardSpeed;
        foreach (Wheel w in wheels)
        {
            if (w.visual == null) continue;
            Vector3 p = w.mount - Vector3.up * (w.dist - w.radius);
            w.visual.localPosition = p;
            w.spin += fs / Mathf.Max(0.1f, w.radius) * Mathf.Rad2Deg * dt;
            float st = w.steers ? steer * 28f : 0f;
            Quaternion rot = Quaternion.Euler(0f, st, 0f) * Quaternion.Euler(w.spin, 0f, 0f);
            if (amph != null) amph.WheelPose(w.mount, ref p, ref rot);
            w.visual.localPosition = p;
            w.visual.localRotation = rot;
        }
        if (treadMarks != null)
        {
            foreach (Transform t in treadMarks)
            {
                Vector3 lp = t.localPosition;
                lp.z += fs * dt;
                float half = treadHalf;
                if (lp.z > half) lp.z -= half * 2f;
                if (lp.z < -half) lp.z += half * 2f;
                t.localPosition = lp;
            }
        }
        if (animate != null) animate(fs, dt);
    }
}
