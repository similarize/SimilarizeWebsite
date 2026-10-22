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
        public float yawDeg;   // ffu67d: the steer angle the visual got this frame (probe)
    }

    public readonly List<Wheel> wheels = new List<Wheel>();
    public float rest = 0.35f, maxSpeed = 24f, accel = 12f, turnRate = 1.6f, grip = 7f, reverseFrac = 0.45f;
    // ffu67d FOUR-WHEEL STEERING (Bill Oct 20 16:20, Cybertruck, monster-truck style): rearSteer = how far the rear wheels follow the
    // fronts (1 = same angle). Normal: rears turn OPPOSITE (full at <= 4 m/s, blending to 10% by 20 m/s) -> yaw x (1 + k), radius / (1 + k).
    // crab (per truck, phone WHEELS tab): rears turn the SAME way -> little yaw, the truck slides diagonally. 0 = 2WS (every other vehicle).
    public float rearSteer = 0f;
    public bool crab;
    public const float SteerDeg = 28f;
    // ffu67d fix 1 (Bill Oct 20 21:15): fully opposite up to 4 m/s, smoothstep down to ~0 by 15 m/s (a hair same-direction, -0.08, above that
    // for highway stability). fs = ForwardSpeed in m/s. Negative = rears follow the fronts slightly (never in crab, which clamps to >= 0).
    public float RearK(float fs) { if (rearSteer <= 0f || tracked || walker) return 0f; float u = Mathf.Clamp01((Mathf.Abs(fs) - 4f) / 11f); u = u * u * (3f - 2f * u); return rearSteer * Mathf.Lerp(1f, -0.08f, u); }
    // rear wheel angle in degrees (+ = same side as the fronts) for the current steer + speed
    public float RearAngle { get { float k = RearK(ForwardSpeed); if (crab) k = Mathf.Max(0f, k); return (crab ? 1f : -1f) * steer * SteerDeg * k; } }
    public float SteerInput { get { return steer; } }
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
        get { return amph != null && amph.Boat ? "CYBERBOAT! L-stick steer | RT jet | LT reverse | Y / F truck mode | A hop out (swim)" : amph != null ? base.HelpLine + " | Y / F Cyberboat mode" : tracked && !F70.Old ? "L-stick steer (tracks turn on the spot) | RT drive | LT brake/reverse | A get out" : base.HelpLine; }   // ffu70: tracked Ripsaw EV2
    }   // (forwardSpeed, dt) for custom visuals like mech legs

    // ffu68 DRIFT + BURNOUT (Ben 2026-10-21, bot-notes c830b3c): wheeled ground vehicles only (Cybertruck, Monster Truck, moon buggy,
    // mower, Curiosity), human drivers only. Burnout: LT + RT (or handbrake + RT) while slow = held on the fronts, rear wheels spin,
    // smoke + squeal, let go of the brake = launch. Drift: at speed turn hard + B handbrake (keyboard Space / B) or an LT tap -> rear
    // grip drops, the tail slides (slip angle capped ~35 deg, never past 90), countersteer holds it, grip comes back over ~0.5 s.
    public bool DriftOk { get { return !tracked && !walker && usesTriggers && wheels.Count > 0 && !(amph != null && amph.Boat); } }
    [System.NonSerialized] public float driftK, burnK, slipAng;
    [System.NonSerialized] public bool drifting, burning;
    float launchT; int fxTick; AudioSource squeal;
    static bool toldBurn, toldDrift;
    static readonly Color SmokeCol = new Color(0.86f, 0.86f, 0.86f, 0.6f);
    public override string HelpFor(InputKind k)
    {
        if (GetType() != typeof(GroundVehicle) || !DriftOk) return base.HelpFor(k);   // subclasses (tank, mower, moon buggy) + boat mode keep their lines
        if (k == InputKind.Gamepad) return "RT  /  LT::gas / brake + reverse|L-stick::steer|B::drift (turn hard at speed - an LT tap works too)|LT + RT::burnout when stopped (let go of LT = launch!)" + (amph != null ? "|Y::Cyberboat mode" : "") + "|A::get out";
        if (k == InputKind.Keyboard) return "W  /  S::gas / brake + reverse|A  /  D::steer|Space::drift (turn hard at speed - B works too)|W + Space::burnout when stopped (let go of Space = launch!)" + (amph != null ? "|F::Cyberboat mode" : "") + "|E::get out";
        return null;   // touch: the HelpLine (GAS + BRAKE together = burnout)
    }

    void DriftStep(bool driven, bool stunt, float dt)
    {
        Vector3 v = rb.velocity;
        float fs = Vector3.Dot(v, transform.forward), ls = Vector3.Dot(v, transform.right), sl = Mathf.Abs(ls);
        bool ok = DriftOk && driven && driver.human && groundedCount > 0 && pondSub <= 0f && !stunt && !rb.isKinematic;
        bool brakeHeld = inp.brake > 0.4f || inp.handbrake;
        bool burnNow = ok && inp.gas > 0.4f && brakeHeld && Mathf.Abs(fs) < 3f;
        if (burning && !burnNow && ok && inp.gas > 0.4f) launchT = 0.6f;   // let go of the brake = launch
        burning = burnNow;
        if (launchT > 0f) launchT -= dt;
        burnK = Mathf.MoveTowards(burnK, burning ? 1f : 0f, dt * 4f);
        slipAng = Mathf.Atan2(sl, Mathf.Max(0.5f, Mathf.Abs(fs))) * Mathf.Rad2Deg;
        bool want = ok && !burning && fs > 8f && Mathf.Abs(steer) > 0.6f && (inp.handbrake || inp.brake > 0.3f);
        if (want && !drifting)
        {
            drifting = true;
            rb.AddTorque(transform.up * steer * 0.5f, ForceMode.VelocityChange);   // kick the tail out
            if (!toldDrift) { toldDrift = true; driver.Toast("DRIFT!", 1.5f); }
        }
        if (drifting && (!ok || fs < 6f || (!inp.handbrake && inp.brake < 0.3f && Mathf.Abs(steer) < 0.3f))) drifting = false;
        driftK = Mathf.MoveTowards(driftK, drifting ? 1f : 0f, dt * (drifting ? 6.7f : 2f));
        if (burning && !toldBurn) { toldBurn = true; driver.Toast("BURNOUT! Let go of the brake to launch", 2f); }

        // effects: smoke (pavement) / dust (grass, dirt, moon) at the rear tyres + a tyre squeal scaled by the slip
        bool fx = burnK > 0.3f || (driftK > 0.3f && sl > 4f);
        float surf = fx ? CtSurface() : 0f;
        if (fx && (++fxTick % 3) == 0)
            foreach (Wheel w in wheels)
                if (w.grounded && w.mount.z < 0f)
                {
                    Vector3 cp = transform.TransformPoint(w.mount) - transform.up * w.dist;
                    if (surf >= 0.5f) FX.Dust(cp, 1f);
                    else FX.Smoke(cp + Vector3.up * 0.25f, Random.Range(0.8f, 1.4f), SmokeCol);
                }
        float sq = Mathf.Max(0.45f * burnK, 0.45f * driftK * Mathf.Clamp01((sl - 2f) / 6f));
        if (surf >= 0.5f) sq *= 0.5f;
        if (!(driven && driver.human)) sq = 0f;
        SquealTick(sq, burnK > driftK ? 1.05f + 0.05f * Mathf.Sin(Time.time * 9f) : 0.9f + sl / 30f, dt);
    }

    float DriftGrip()
    {
        if (driftK <= 0f) return 1f;
        float gk = Mathf.Lerp(1f, 0.25f, driftK);
        if (slipAng > 50f) gk = 1.5f;                                       // never spin out: strong restoring grip
        else if (slipAng > 35f) gk = Mathf.Lerp(gk, 1f, (slipAng - 35f) / 15f);   // ~35 deg cap
        return gk;
    }

    void SquealTick(float vol, float pitch, float dt)
    {
        if (squeal == null)
        {
            if (vol <= 0.001f || Sfx.Skid == null) return;
            squeal = Sfx.Loop(gameObject, Sfx.Skid); squeal.Play(); squeal.Pause();
        }
        squeal.volume = Mathf.MoveTowards(squeal.volume, vol, dt * 3f);
        squeal.pitch = Mathf.Lerp(squeal.pitch, Mathf.Max(0.05f, pitch), Mathf.Min(1f, dt * 4f));
        if (squeal.volume <= 0.001f) { if (squeal.isPlaying) squeal.Pause(); }
        else if (!squeal.isPlaying) squeal.UnPause();
    }

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

        DriftStep(driven, inLoop || onStunt, dt);   // ffu68
        if (groundedCount > 0 && ws > 0f)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
            Vector3 right = Vector3.ProjectOnPlane(transform.right, groundNormal).normalized;
            Vector3 v = rb.velocity;
            float fs = Vector3.Dot(v, fwd);
            float ls = Vector3.Dot(v, right);
            slipSpeed = ls; groundFrac = wheels.Count > 0 ? groundedCount / (float)wheels.Count : 1f;

            float force = 0f;
            if (!driven && Behave58.On)
            {
                // ffu58: a parked car HOLDS on a slope (park pawl + brakes = static friction): cancel the slope's pull
                // along and across the car and damp what is left, instead of a viscous -fs * 3 that let a parked
                // Cybertruck creep at ~1 km/h down the ranch's gentle slopes
                float gsF = Vector3.Dot(Physics.gravity, fwd), gsR = Vector3.Dot(Physics.gravity, right);
                force = -fs * 8f - gsF;
                if (Mathf.Abs(fs) < 0.6f && Mathf.Abs(ls) < 0.6f) rb.AddForce(-right * gsR * rb.mass * gf, ForceMode.Force);
            }
            else if (!driven) force = -fs * 3f;   // parking brake
            else if (throttle > 0.02f) force = fs < maxSpeed ? accel * throttle * Mathf.Clamp01(1.15f - fs / maxSpeed) : 0f;
            else if (throttle < -0.02f) force = fs > 1f ? -accel * 1.6f * -throttle : (fs > -maxSpeed * reverseFrac ? accel * 0.7f * throttle : 0f);
            else force = -fs * 0.8f;
            if (burning) force = -fs * 8f;                          // ffu68 burnout: the fronts hold it
            else if (launchT > 0f && force > 0f) force *= 1.6f;     // ffu68 launch
            if (pondSub > 0f) { force *= 1f - 0.5f * pondSub; if (driven && throttle > 0.02f) { float lim = maxSpeed * Mathf.Lerp(1f, 0.22f, pondSub); if (fs > lim) force = Mathf.Min(force, 0f); } }   // ffu37: crawling on the pond bed
            rb.AddForce(fwd * force * rb.mass * gf, ForceMode.Force);
            float lsT = 0f;   // ffu67d crab: the grip holds the slide the 4 parallel wheels point at instead of zero
            if (crab && driven && !(amph != null && amph.Boat)) { float rk2 = RearK(fs); if (rk2 > 0f) lsT = fs * Mathf.Tan(steer * SteerDeg * Mathf.Deg2Rad) * Mathf.Min(1f, rk2); }
            rb.AddForce(-right * (ls - lsT) * grip * DriftGrip() * rb.mass * gf, ForceMode.Force);   // ffu68: drift drops the grip

            float yr = YawRate(rb, transform);
            float target;
            if (tracked) target = steer * turnRate * (fs < -0.5f ? -1f : 1f);
            else
            {
                target = steer * turnRate * Mathf.Clamp(fs / 6f, -1f, 1f);
                float rk = (amph != null && amph.Boat) ? 0f : RearK(fs);   // ffu67d 4WS: bicycle model, yaw ~ tan(front) - tan(rear)
                if (crab) { if (rk > 0f) target *= Mathf.Max(0f, 1f - rk); } else if (rk != 0f) target *= 1f + rk;
            }
            if (driftK > 0f) target *= 1f + 0.8f * driftK * Mathf.Clamp01((45f - slipAng) / 10f);   // ffu68 drift: more yaw, fading near the cap
            if (burning) target = steer * 0.9f;                     // ffu68 burnout donut
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
            if (burnK > 0f && w.mount.z < 0f) w.spin += burnK * 25f * Mathf.Rad2Deg * dt;   // ffu68 burnout: rears spin in place
            float st = w.steers ? steer * SteerDeg : 0f;
            if (!w.steers && rearSteer > 0f && w.mount.z < 0f) st = RearAngle;   // ffu67d: rear wheels visibly steer
            Quaternion rot = Quaternion.Euler(0f, st, 0f) * Quaternion.Euler(w.spin, 0f, 0f);
            if (amph != null) amph.WheelPose(w.mount, ref p, ref rot);
            w.visual.localPosition = p;
            w.visual.localRotation = rot;
            w.yawDeg = st;
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
