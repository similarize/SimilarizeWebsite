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
    }

    public readonly List<Wheel> wheels = new List<Wheel>();
    public float rest = 0.35f, maxSpeed = 24f, accel = 12f, turnRate = 1.6f, grip = 7f, reverseFrac = 0.45f;
    public bool tracked;
    public override string[] TouchSet { get { return usesTriggers ? new[] { "A", null, null, "GAS", "BRAKE" } : new[] { "A", null, null, null, null }; } }
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
    public float Throttle01 { get { return driver != null ? Mathf.Clamp01(throttle) : 0f; } }
    public override string HelpLine
    {
        get { return amph != null && amph.Boat ? "CYBERBOAT! L-stick steer | RT jet | LT reverse | A hop out (swim)" : base.HelpLine; }
    }   // (forwardSpeed, dt) for custom visuals like mech legs

    public void AddWheel(Vector3 center, float radius, Transform visual, bool steers)
    {
        wheels.Add(new Wheel { mount = center + Vector3.up * rest, radius = radius, visual = visual, steers = steers, dist = rest + radius });
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
            float len = rest + w.radius;
            if (Physics.Raycast(origin, -up, out hit, len, mask, QueryTriggerInteraction.Ignore))
            {
                w.grounded = true;
                w.dist = hit.distance;
                float comp = len - hit.distance;
                float vUp = Vector3.Dot(rb.GetPointVelocity(origin), up);
                float f = spring * sm * comp - damper * dm * vUp;
                if (f < 0f) f = 0f;
                rb.AddForceAtPosition(up * f * ws, origin);
                groundedCount++;
                nsum += hit.normal;
                int hl = hit.collider.gameObject.layer;
                if (hl == RallyTrack.StuntLayer) onStunt = true;
                else if (hl == RallyTrack.TrackLayer) onTrack = true;
            }
            else
            {
                w.grounded = false;
                w.dist = Mathf.MoveTowards(w.dist, len, dt * 3f);
            }
        }
        float gf = (wheels.Count > 0 ? groundedCount / (float)wheels.Count : 0f) * ws;
        groundNormal = groundedCount > 0 ? nsum.normalized : Vector3.up;
        bool driven = driver != null;

        // rally assists: the loop holds you on (downforce + a minimum speed while you keep the throttle on),
        // banked turns get a little extra stick so the trucks can lean on the berms
        bool inLoop = false;
        Bounds zone = default(Bounds);
        float guideYaw = 0f;
        foreach (Bounds b in RallyTrack.LoopZones) if (b.Contains(rb.position)) { inLoop = true; zone = b; }
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
                if (driven && lfs > 2f && RallyTrack.LoopGuide(rb.position, out gt, out glat))
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
        loopLoaded = inLoop && onStunt && groundedCount > 0;

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
                        if (cp.y < Layout.WaterY && Layout.InPond(cp.x, cp.z)) { if (Random.value < 0.35f) FX.Splash(new Vector3(cp.x, Layout.WaterY + 0.1f, cp.z), 1 + Mathf.Abs(fs) * 0.08f); }
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

    protected virtual void Update()
    {
        float dt = Time.deltaTime;
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
