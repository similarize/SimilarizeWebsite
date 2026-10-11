using System.Collections.Generic;
using UnityEngine;

// ffu31: a towable camper trailer (TrailerBarn spawns four). Not a Vehicle (nobody climbs in): its own Rigidbody on
// TrailerLayer with raycast suspension, lateral tyre grip at the axle (that is what makes it track through turns and
// jackknife in reverse), a parking brake + tongue jack while unhitched, and a ConfigurableJoint at the hitch ball while
// a truck tows it. Watchdogs unhitch it safely when the physics goes wild (joint stretch, spin, roll-over, a teleported
// truck, the loop, the Cyberboat). Toy haulers have a rear ramp door and a bay that stows bikes (kinematic cargo).
public class Trailer : MonoBehaviour
{
    public static readonly List<Trailer> All = new List<Trailer>();
    public const int TrailerLayer = 17;
    // trailer wheels must not see their own colliders (or another trailer's)
    public static int WheelMask { get { return Vehicle.GroundMask & ~(1 << TrailerLayer); } }

    public class Spec
    {
        public string name, shortName;
        public int style;                 // 0 silver bullet, 1 teardrop, 2 toy hauler, 3 cyber camper
        public float len, wid, hgt, floorY = 0.58f, mass, wheelR = 0.36f, axleZ, tongue = 1.6f;
        public bool tandem, hauler, chunky;
        public float bayLen;
        public Color main, accent;
    }

    public class Wheel { public Vector3 mount; public float r, dist; public bool grounded; public Transform vis; public float spin; }

    public Spec spec;
    public string Title { get { return spec.name; } }
    public Rigidbody rb;
    public readonly List<Wheel> wheels = new List<Wheel>();
    public Vector3 ballLocal;
    public float rest = 0.2f;
    float spring, damper;
    public Vehicle tower;
    ConfigurableJoint joint;
    Vector3 homePos; Quaternion homeRot;
    Vector3 lastTowerPos; float lastTowerSpeed;
    int stretchN; float flipT, wetT, jackT, jackK = 1f;
    public Transform jack;            // tongue jack (extends while unhitched)
    float swayPhase;
    public float hitchAngle;          // degrees, trailer heading vs truck heading (+ = trailer swung right)
    bool jackknifeSaid;
    public string lastDetach = "";
    public float sinceHitch;
    int towerIters = -1, towerVelIters = -1;
    public static int Hitches, Detaches, SafeDetaches;
    public float uprightNow { get { return transform.up.y; } }

    // toy hauler
    public Transform rampHinge;
    public float rampK;               // 0 closed (vertical rear door), 1 lowered (ramp)
    float rampWant;
    public float rampLen = 2.3f, deck;
    public Hotspot rampSpot;
    public class Slot { public Vector3 pos; public Vehicle bike; public bool wide; }
    public readonly List<Slot> slots = new List<Slot>();
    // cargo moving in / out on rails (scripted ride up / down the ramp)
    public class Move { public Vehicle v; public Frog rider; public List<Vector3> pts = new List<Vector3>(); public List<float> yaws = new List<float>(); public float t, dur; public bool unload; public Slot slot; }
    public Move move;
    readonly Dictionary<Vehicle, float> parkedT = new Dictionary<Vehicle, float>();
    readonly List<Collider> tmpCols = new List<Collider>();
    readonly Dictionary<Vehicle, List<Collider>> frozenCols = new Dictionary<Vehicle, List<Collider>>();

    public void Init(Spec s, Vector3 ball)
    {
        spec = s;
        ballLocal = ball;
        rb = gameObject.AddComponent<Rigidbody>();
        rb.mass = s.mass;
        rb.drag = 0.03f;
        rb.angularDrag = 0.6f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.solverIterations = 12; rb.solverVelocityIterations = 4;
        rb.maxAngularVelocity = 12f;
        // explicit mass properties: the toy hauler's ramp collider moves, and the auto inertia of a long thin box with a
        // tongue sticking out is not what a towed trailer feels like. CoM a little ahead of the axle = tongue weight.
        float L = s.len + s.tongue, W = s.wid, H = s.hgt + s.floorY;
        rb.centerOfMass = new Vector3(0f, s.floorY + s.hgt * 0.38f, s.axleZ + 0.35f);
        rb.inertiaTensor = new Vector3(s.mass / 12f * (H * H + L * L), s.mass / 12f * (W * W + L * L), s.mass / 12f * (W * W + H * H));
        rb.inertiaTensorRotation = Quaternion.identity;
        int n = Mathf.Max(1, wheels.Count);
        spring = s.mass * 9.81f * 0.9f / n / (rest * 0.45f);
        damper = 2f * Mathf.Sqrt(spring * s.mass / n) * 0.22f;    // under-damped a bit: a little bounce on the bumps
        homePos = transform.position; homeRot = transform.rotation;
        swayPhase = Random.value * 6.28f;
        Mats.SetLayer(gameObject, TrailerLayer);
        All.Add(this);
    }

    public void AddWheel(Vector3 axleCentre, float r, Transform vis)
    {
        wheels.Add(new Wheel { mount = axleCentre + Vector3.up * rest, r = r, dist = rest + r, vis = vis });
    }

    void OnDestroy() { All.Remove(this); }

    public Vector3 BallWorld { get { return transform.TransformPoint(ballLocal); } }
    public bool Hitched { get { return joint != null && tower != null; } }
    public float Speed { get { return rb != null ? rb.velocity.magnitude : 0f; } }
    public Vector3 HomePos { get { return homePos; } }

    // ---------------- hitching ----------------
    public static Vector3 HitchLocal(Vehicle v)
    {
        BoxCollider b = v.body;
        float back = b != null ? b.center.z - b.size.z * 0.5f : -2.8f;
        return new Vector3(0f, 0.55f, back - 0.22f);
    }
    public static Vector3 HitchWorld(Vehicle v) { return v.transform.TransformPoint(HitchLocal(v)); }

    // can this truck hitch to us right now? (hitch ball within reach, truck facing away from the trailer, slow)
    public bool CanHitch(Vehicle v, out float dist)
    {
        dist = 1e9f;
        if (Hitched || move != null || rampK > 0.05f) return false;
        Vector3 h = HitchWorld(v), b = BallWorld;
        Vector3 d = h - b;
        if (Mathf.Abs(d.y) > 0.45f) return false;
        d.y = 0f;
        dist = d.magnitude;
        if (dist > 1.25f) return false;
        Vector3 tf = v.transform.forward; tf.y = 0f; Vector3 mf = transform.forward; mf.y = 0f;
        if (Vector3.Angle(tf, mf) > 55f) return false;
        if (v.Speed > 3f) return false;
        if (transform.up.y < 0.8f) return false;
        return true;
    }

    public void Hitch(Vehicle v)
    {
        if (Hitched) Detach("rehitch", false);
        tower = v;
        // the joint's angular limits are measured from the relative pose at creation: create it with the trailer lined
        // up behind the truck (ball on the hitch), then put the trailer back at its real angle (ball still on the hitch)
        Vector3 hw = HitchWorld(v);
        Quaternion r0 = rb.rotation;
        // slide (never lift / sink) the trailer so the coupler is over the ball; the joint takes up the last few cm of
        // height by pitching the trailer about its axle
        Vector3 dh = hw - BallWorld; dh.y = 0f;
        Vector3 p0 = rb.position + dh;
        Vector3 tf = v.transform.forward; tf.y = 0f; if (tf.sqrMagnitude < 0.01f) tf = transform.forward; tf.Normalize();
        Quaternion ra = Quaternion.LookRotation(tf, Vector3.up);
        ra = Quaternion.FromToRotation(Vector3.up, v.transform.up) * ra;
        Vector3 pa = hw - ra * ballLocal;
        transform.SetPositionAndRotation(pa, ra); rb.position = pa; rb.rotation = ra;
        Physics.SyncTransforms();
        joint = gameObject.AddComponent<ConfigurableJoint>();
        joint.autoConfigureConnectedAnchor = false;
        joint.anchor = ballLocal;
        joint.axis = Vector3.right; joint.secondaryAxis = Vector3.up;
        joint.connectedBody = v.rb;
        joint.connectedAnchor = HitchLocal(v);
        joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;
        joint.angularXMotion = ConfigurableJointMotion.Limited;      // pitch (hills, the ramp up to the barn)
        joint.angularYMotion = ConfigurableJointMotion.Free;         // yaw: our own soft jackknife stop (FixedUpdate)
        joint.angularZMotion = ConfigurableJointMotion.Limited;      // roll
        joint.lowAngularXLimit = new SoftJointLimit { limit = -30f };
        joint.highAngularXLimit = new SoftJointLimit { limit = 30f };
        joint.angularZLimit = new SoftJointLimit { limit = 20f };
        joint.angularXLimitSpring = new SoftJointLimitSpring { spring = 0f, damper = 0f };
        joint.projectionMode = JointProjectionMode.PositionAndRotation;
        joint.projectionDistance = 0.08f; joint.projectionAngle = 4f;
        joint.enableCollision = false;
        joint.enablePreprocessing = false;
        joint.breakForce = spec.mass * 9.81f * 45f;
        joint.breakTorque = Mathf.Infinity;
        transform.SetPositionAndRotation(p0, r0); rb.position = p0; rb.rotation = r0;
        rb.velocity = v.rb.velocity; rb.angularVelocity = Vector3.zero;
        Physics.SyncTransforms();
        towerIters = v.rb.solverIterations; towerVelIters = v.rb.solverVelocityIterations;
        v.rb.solverIterations = Mathf.Max(towerIters, 12); v.rb.solverVelocityIterations = Mathf.Max(towerVelIters, 4);
        lastTowerPos = v.rb.position; lastTowerSpeed = v.Speed;
        stretchN = 0; sinceHitch = 0f; jackknifeSaid = false;
        if (rampK > 0.01f || rampWant > 0f) rampWant = 0f;
        Hitches++;
        Sfx.PlayAt(Sfx.Clank != null ? Sfx.Clank : Sfx.Thud, hw, 0.8f, 40f, 0.9f);
        FX.Puff(hw, Vector3.up * 0.4f, 0.5f, 0.6f, new Color(0.7f, 0.65f, 0.55f, 0.5f));
        Debug.Log("Trailer: " + v.Title + " hitched to " + Title);
    }

    // safe = the watchdog pulled the pin (physics went wild), not the player
    public void Detach(string why, bool safe)
    {
        Vehicle v = tower;
        if (joint != null) Destroy(joint);
        joint = null;
        tower = null;
        lastDetach = why;
        if (v != null && v.rb != null)
        {
            if (towerIters > 0) v.rb.solverIterations = towerIters;
            if (towerVelIters > 0) v.rb.solverVelocityIterations = towerVelIters;
        }
        towerIters = towerVelIters = -1;
        Detaches++;
        if (safe)
        {
            SafeDetaches++;
            // calm it down: no wild spin, no faster than the truck was going
            rb.angularVelocity *= 0.25f;
            float cap = v != null ? v.Speed + 2f : 10f;
            if (rb.velocity.magnitude > cap) rb.velocity = rb.velocity.normalized * cap;
            if (v != null && v.driver != null && v.driver.human) v.driver.Toast("Trailer unhitched - " + why, 2.8f);
        }
        Sfx.PlayAt(Sfx.Clank != null ? Sfx.Clank : Sfx.Thud, BallWorld, 0.6f, 40f, 1.15f);
        Debug.Log("Trailer: " + Title + " unhitched (" + why + (safe ? ", watchdog" : "") + ")");
    }

    void OnJointBreak(float force)
    {
        joint = null;   // Unity removes the component itself
        Vehicle v = tower;
        tower = null;
        if (v != null) { tower = v; Detach("hitch overloaded", true); }
    }

    // ---------------- physics ----------------
    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        if (rb == null || rb.isKinematic) return;
        if (joint == null && tower != null) { Vehicle v = tower; tower = null; tower = v; Detach("hitch lost", true); }
        if (Hitched) { sinceHitch += dt; if (Watchdog(dt)) return; }

        Vector3 up = transform.up;
        int mask = WheelMask, g = 0;
        float spd = rb.velocity.magnitude;
        Vector3 nsum = Vector3.zero;
        bool dirt = false;
        foreach (Wheel w in wheels)
        {
            Vector3 o = transform.TransformPoint(w.mount);
            RaycastHit hit;
            float len = rest + w.r;
            if (Physics.Raycast(o, -up, out hit, len, mask, QueryTriggerInteraction.Ignore))
            {
                w.grounded = true; w.dist = hit.distance;
                float comp = len - hit.distance;
                bool soft = hit.collider is TerrainCollider || hit.collider.gameObject.layer == RallyTrack.TrackLayer;
                if (soft && spd > 3f) { dirt = true; comp += (Mathf.PerlinNoise(o.x * 0.9f, o.z * 0.9f) - 0.5f) * 0.07f * Mathf.Clamp01(spd / 12f); }   // washboard bounce
                float vUp = Vector3.Dot(rb.GetPointVelocity(o), up);
                float f = spring * comp - damper * vUp;
                if (comp > rest * 0.9f) f += spring * 4f * (comp - rest * 0.9f);   // bump stop
                if (f > 0f) rb.AddForceAtPosition(up * f, o);
                g++; nsum += hit.normal;
                if (soft && spd > 4f && Random.value < 0.6f) FX.Dust(hit.point, Mathf.Clamp01(spd / 18f) * 0.35f);
                if (Layout.InPond(hit.point.x, hit.point.z) && hit.point.y < Layout.WaterY && spd > 2f && Random.value < 0.3f) FX.Splash(new Vector3(hit.point.x, Layout.WaterY + 0.1f, hit.point.z), 1 + spd * 0.1f);
            }
            else { w.grounded = false; w.dist = Mathf.MoveTowards(w.dist, len, dt * 3f); }
        }
        // tongue jack: only while unhitched (holds the nose up)
        jackK = Mathf.MoveTowards(jackK, Hitched ? 0f : 1f, dt * 2.5f);
        if (!Hitched && jackK > 0.5f)
        {
            Vector3 jl = ballLocal + new Vector3(0f, 0.25f, -0.25f);
            Vector3 o = transform.TransformPoint(jl);
            RaycastHit hit;
            float len = jl.y + 0.08f;
            if (Physics.Raycast(o, -up, out hit, len, mask, QueryTriggerInteraction.Ignore))
            {
                float comp = len - hit.distance;
                float vUp = Vector3.Dot(rb.GetPointVelocity(o), up);
                float k = spec.mass * 9.81f * 0.25f / 0.05f;
                float f = k * comp - 2f * Mathf.Sqrt(k * spec.mass * 0.25f) * 0.6f * vUp;
                if (f > 0f) rb.AddForceAtPosition(up * f, o);
            }
        }

        if (g > 0)
        {
            Vector3 n = nsum.normalized;
            Vector3 axle = transform.TransformPoint(new Vector3(0f, spec.floorY * 0.5f, spec.axleZ));
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, n).normalized;
            Vector3 right = Vector3.ProjectOnPlane(transform.right, n).normalized;
            Vector3 pv = rb.GetPointVelocity(axle);
            float fs = Vector3.Dot(pv, fwd), ls = Vector3.Dot(pv, right);
            float gf = g / (float)wheels.Count;
            // tyre side grip at the axle, capped at what rubber can give (slides when you yank it = jackknife fun)
            float latA = Mathf.Clamp(-ls * 9f, -8.5f, 8.5f);
            rb.AddForceAtPosition(right * latA * rb.mass * gf, axle);
            float longA;
            if (!Hitched)
            {
                longA = Mathf.Clamp(-fs * 12f, -9f, 9f);                     // parking brake
                // parked: no creep (the jack + springs used to walk it ~5 cm/s)
                if (rb.velocity.sqrMagnitude < 0.09f && rb.angularVelocity.sqrMagnitude < 0.04f) { Vector3 v0 = rb.velocity; rb.velocity = new Vector3(v0.x * 0.8f, v0.y, v0.z * 0.8f); }
            }
            else
            {
                longA = -fs * 0.06f;                                          // rolling resistance
                // electric brake controller: when the truck slows hard, the trailer brakes with it (no shove-and-fold)
                float ts = tower.Speed;
                float decel = (lastTowerSpeed - ts) / dt;
                if (decel > 2.5f && Mathf.Abs(fs) > 1f) longA -= Mathf.Sign(fs) * Mathf.Min(decel * 0.75f, 7f);
                // a little sway at speed (dies down on its own - the side grip damps it)
                if (spd > 11f) rb.AddForceAtPosition(right * Mathf.Sin(Time.time * 3.6f + swayPhase) * Mathf.Clamp01((spd - 11f) / 14f) * 1.4f * rb.mass, axle - fwd * 1.5f);
            }
            rb.AddForceAtPosition(fwd * longA * rb.mass * gf, axle);
        }
        if (Hitched) lastTowerSpeed = tower.Speed;

        // unhitched and on its side / roof: set it back on its wheels after a moment (like the vehicles do)
        if (!Hitched && transform.up.y < 0.35f && spd < 3f) flipT += dt; else flipT = 0f;
        if (flipT > 1.8f)
        {
            flipT = 0f;
            rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
            rb.position += Vector3.up * 1.5f;
            rb.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        }
        // dumped in the pond or off the world: back to its barn bay
        Vector3 p = rb.position;
        bool wet = !Hitched && Layout.InPond(p.x, p.z) && p.y < Layout.WaterY - 0.7f;
        wetT = wet ? wetT + dt : 0f;
        if (wetT > 4f || p.y < -40f || Mathf.Abs(p.x) > Layout.Half + 15f || Mathf.Abs(p.z) > Layout.Half + 15f) { wetT = 0f; GoHome(); }
        StepRamp(dt);
        StepCargo(dt);
    }

    // true = unhitched this step
    bool Watchdog(float dt)
    {
        Vehicle v = tower;
        string why = null;
        if (v == null || !v.isActiveAndEnabled || v.rb == null || v.rb.isKinematic) why = "the truck is gone";
        else if (v.Wrecked) why = "the truck is wrecked";
        else
        {
            GroundVehicle gv = v as GroundVehicle;
            Vector3 tp = v.rb.position;
            float jump = (tp - lastTowerPos).magnitude;
            if (jump > v.rb.velocity.magnitude * dt * 3f + 2.5f) why = "the truck jumped";
            lastTowerPos = tp;
            if (gv != null && gv.amph != null && gv.amph.Boat) why = "Cyberboat mode!";
            foreach (Bounds b in RallyTrack.LoopZones) if (b.Contains(tp) || b.Contains(rb.position)) why = "no trailers in the loop!";
            float err = (HitchWorld(v) - BallWorld).magnitude;
            stretchN = err > 0.6f ? stretchN + 1 : 0;
            if (stretchN > 3) why = "the hitch was stretched";
            if (rb.angularVelocity.magnitude > 8f) why = "it was spinning";
            if (rb.velocity.magnitude > 48f || (rb.velocity - v.rb.velocity).magnitude > 18f) why = "it got thrown";
            if (transform.up.y < 0.25f) why = "it rolled over";
            if (Mathf.Abs(tp.x) > Layout.Half + 10f || Mathf.Abs(tp.z) > Layout.Half + 10f || tp.y < -30f) why = "off the ranch";
            // jackknife: the joint's yaw is free; a soft stop from 68 deg (equal and opposite torque on both), pin pulled
            // only if it is folded right past 100 deg
            Vector3 tf = v.transform.forward; tf.y = 0f; Vector3 mf = transform.forward; mf.y = 0f;
            hitchAngle = Vector3.SignedAngle(tf, mf, Vector3.up);
            float over = Mathf.Abs(hitchAngle) - 68f;
            if (over > 0f)
            {
                float relW = Vector3.Dot(rb.angularVelocity - v.rb.angularVelocity, Vector3.up);
                float s = Mathf.Sign(hitchAngle);
                float acc = -s * Mathf.Min(over, 25f) * Mathf.Deg2Rad * 60f - (relW * s > 0f ? relW * 10f : 0f);   // rad/s^2
                rb.AddTorque(Vector3.up * acc, ForceMode.Acceleration);
                v.rb.AddTorque(-Vector3.up * acc * Mathf.Clamp(rb.mass / v.rb.mass, 0.2f, 1f) * 0.5f, ForceMode.Acceleration);
                if (!jackknifeSaid && over > 4f)
                {
                    jackknifeSaid = true;
                    Sfx.PlayAt(Sfx.Clank != null ? Sfx.Clank : Sfx.Thud, BallWorld, 0.7f, 40f, 0.75f);
                    if (v.driver != null && v.driver.human) v.driver.Toast("JACKKNIFE! Pull forward to straighten out", 2.2f);
                }
            }
            else if (Mathf.Abs(hitchAngle) < 40f) jackknifeSaid = false;
            if (Mathf.Abs(hitchAngle) > 100f) why = "jackknifed too far";
        }
        if (why == null) return false;
        Detach(why, true);
        return true;
    }

    public void GoHome()
    {
        if (Hitched) Detach("sent home", false);
        rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
        rb.position = homePos + Vector3.up * 0.3f; rb.rotation = homeRot;
        transform.SetPositionAndRotation(homePos + Vector3.up * 0.3f, homeRot);
        rampWant = 0f;
    }

    // demo: move the whole rig (truck + trailer) at once, keeping the hitch together
    public void PlaceBehind(Vehicle v)
    {
        Vector3 hw = HitchWorld(v);
        Vector3 tf = v.transform.forward; tf.y = 0f; tf.Normalize();
        Quaternion r = Quaternion.LookRotation(tf, Vector3.up);
        Vector3 p = hw - r * ballLocal;
        rb.velocity = v.rb.velocity; rb.angularVelocity = Vector3.zero;
        rb.position = p; rb.rotation = r; transform.SetPositionAndRotation(p, r);
        lastTowerPos = v.rb.position; stretchN = 0;   // not a "the truck jumped" teleport
    }

    // ---------------- visuals ----------------
    void Update()
    {
        float dt = Time.deltaTime;
        float fs = rb != null ? Vector3.Dot(rb.velocity, transform.forward) : 0f;
        foreach (Wheel w in wheels)
        {
            if (w.vis == null) continue;
            w.spin += fs / Mathf.Max(0.1f, w.r) * Mathf.Rad2Deg * dt;
            w.vis.localPosition = w.mount - Vector3.up * (w.dist - w.r);
            w.vis.localRotation = Quaternion.Euler(w.spin, 0f, 0f);
        }
        if (jack != null)
        {
            jack.localScale = new Vector3(1f, Mathf.Lerp(0.25f, 1f, jackK), 1f);
        }
        if (rampHinge != null) rampHinge.localRotation = Quaternion.Euler(RampAngle(), 0f, 0f);
        if (rampSpot != null)
        {
            Vector3 foot = RampFoot(0.8f);
            rampSpot.pos = foot;
        }
    }

    // ---------------- toy hauler: ramp + bay ----------------
    public bool RampDown { get { return rampK > 0.97f; } }
    public bool CanMoveRamp { get { return spec.hauler && (!Hitched || tower.Speed < 1f) && Speed < 1.2f && move == null; } }
    public void SetRamp(bool down) { if (spec.hauler) rampWant = down ? 1f : 0f; }

    float rampDownAng = 15f;
    float RampAngle()
    {
        // ramp mesh runs from the hinge along local -z: +90 = door standing up (closed), -a = tip down to the ground
        return Mathf.Lerp(90f, -rampDownAng, Mathf.SmoothStep(0f, 1f, rampK));
    }

    void StepRamp(float dt)
    {
        if (!spec.hauler || rampHinge == null) return;
        if (Hitched && tower.Speed > 1.5f) rampWant = 0f;           // never drive off with the door down
        if (rampWant > 0.5f && !CanMoveRamp && rampK < 0.02f) rampWant = 0f;
        float before = rampK;
        rampK = Mathf.MoveTowards(rampK, rampWant, dt / 1.4f);
        if (Mathf.Abs(rampK - before) > 0f && Random.value < 0.08f) Sfx.PlayAt(Sfx.Pick(Sfx.Servo), rampHinge.position, 0.25f, 25f, 0.8f);
        // lowered: aim the tip just above whatever is under it (the trailer rides on its springs, the ground rolls)
        Vector3 hinge = rampHinge.position;
        Vector3 back = -transform.forward;
        RaycastHit h;
        Vector3 probe = hinge + back * rampLen * 0.95f + Vector3.up * 1f;
        float gy = Physics.Raycast(probe, Vector3.down, out h, 4f, WheelMask, QueryTriggerInteraction.Ignore) ? h.point.y : hinge.y - deck;
        float drop = Mathf.Clamp(hinge.y - gy - 0.03f, 0.05f, rampLen * 0.6f);
        float want = Mathf.Asin(drop / rampLen) * Mathf.Rad2Deg;
        rampDownAng = Mathf.Lerp(rampDownAng, want, dt * 4f);
    }

    // ground point behind the ramp tip (where a rider lines up / steps off)
    public Vector3 RampFoot(float extra)
    {
        if (!spec.hauler) return transform.position;
        Vector3 back = -transform.forward; back.y = 0f; back.Normalize();
        Vector3 p = transform.TransformPoint(new Vector3(0f, 0f, -spec.len * 0.5f)) + back * (rampLen + extra);
        RaycastHit h;
        if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out h, 8f, WheelMask, QueryTriggerInteraction.Ignore)) p.y = h.point.y;
        else p.y = Ranch.GY(p.x, p.z);
        return p;
    }

    public static bool Loadable(Vehicle v)
    {
        if (v == null || v.body == null || v.stowed) return false;
        if (!(v is GroundVehicle) && v.GetType().Name != "Bike") return false;   // ffu30 dirt / sport bikes are their own Vehicle class
        if (v is StoryMech || v is Tank) return false;
        RidingMower rm = v as RidingMower;
        if (rm != null && (rm.robotRide || rm.rider != null)) return false;
        string t = (v.Title + " " + v.GetType().Name).ToLowerInvariant();
        bool kind = t.Contains("bike") || t.Contains("moto") || t.Contains("dirt") || t.Contains("mower") || t.Contains("quad") || t.Contains("atv") || t.Contains("scooter");
        return kind && v.body.size.x < 1.4f && v.body.size.z < 2.9f;
    }

    public int Stowed { get { int n = 0; foreach (var s in slots) if (s.bike != null) n++; return n; } }
    public Vehicle FirstStowed { get { foreach (var s in slots) if (s.bike != null) return s.bike; return null; } }

    Slot FreeSlot(Vehicle v)
    {
        bool wide = v.body.size.x > 0.95f;
        bool any = false;
        foreach (var s in slots) if (s.bike != null) any = true;
        if (wide) return any || slots.Count == 0 ? null : slots[0];
        foreach (var s in slots) if (s.bike != null && s.wide) return null;
        foreach (var s in slots) if (s.bike == null) return s;
        return null;
    }
    public bool HasRoomFor(Vehicle v) { return spec.hauler && FreeSlot(v) != null; }

    // local-space bay box (floor up to the roof, from the ramp hinge to the bulkhead)
    public bool InBay(Vector3 world)
    {
        if (!spec.hauler) return false;
        Vector3 l = transform.InverseTransformPoint(world);
        float z0 = -spec.len * 0.5f, z1 = z0 + spec.bayLen;
        return Mathf.Abs(l.x) < spec.wid * 0.5f - 0.05f && l.z > z0 + 0.2f && l.z < z1 && l.y > deck - 0.4f && l.y < deck + 2.2f;
    }

    // near the ramp foot, lined up to ride in
    public bool AtRampFoot(Vehicle v)
    {
        if (!spec.hauler) return false;
        Vector3 f = RampFoot(0.6f);
        Vector3 d = v.transform.position - f; d.y = 0f;
        return d.magnitude < 3.2f;
    }

    // scripted ride up the ramp into a slot (prompt "Load bike"), rider hops off at the ramp foot
    public bool StartLoad(Vehicle v, Frog rider)
    {
        Slot s = FreeSlot(v);
        if (s == null || move != null) return false;
        s.bike = v;
        s.wide = v.body.size.x > 0.95f;
        if (rampWant < 0.5f) rampWant = 1f;
        Freeze(v, true);
        var m = new Move { v = v, rider = rider, slot = s, unload = false };
        Vector3 foot = RampFoot(0.9f);
        float yIn = transform.eulerAngles.y;     // facing the front of the trailer
        Vector3 top = transform.TransformPoint(new Vector3(s.pos.x, deck, -spec.len * 0.5f + 0.4f));
        m.pts.Add(v.transform.position); m.yaws.Add(v.transform.eulerAngles.y);
        m.pts.Add(foot); m.yaws.Add(yIn);
        m.pts.Add(top); m.yaws.Add(yIn);
        m.pts.Add(transform.TransformPoint(s.pos)); m.yaws.Add(yIn);
        m.dur = 2.4f;
        move = m;
        Sfx.PlayAt(Sfx.Pick(Sfx.Servo), foot, 0.3f, 25f, 1f);
        return true;
    }

    // ride out: the frog gets on, the bike rolls back down the ramp and turns away from the trailer
    public bool StartUnload(Frog rider)
    {
        if (move != null || !RampDown) return false;
        Slot s = null;
        foreach (var q in slots) if (q.bike != null) { s = q; break; }
        if (s == null) return false;
        Vehicle v = s.bike;
        if (rider != null)
        {
            if (rider.vehicle != null) rider.ExitVehicle();
            rider.EnterVehicle(v);
        }
        var m = new Move { v = v, rider = rider, slot = s, unload = true };
        float yIn = transform.eulerAngles.y;
        Vector3 back = -transform.forward; back.y = 0f; back.Normalize();
        Vector3 foot = RampFoot(1.2f), out2 = RampFoot(3.4f) + transform.right * 0.8f;
        m.pts.Add(v.transform.position); m.yaws.Add(v.transform.eulerAngles.y);
        m.pts.Add(transform.TransformPoint(new Vector3(s.pos.x, deck, -spec.len * 0.5f + 0.4f))); m.yaws.Add(yIn);
        m.pts.Add(foot); m.yaws.Add(yIn);
        m.pts.Add(out2); m.yaws.Add(yIn + 180f);
        m.dur = 2.6f;
        move = m;
        return true;
    }

    // a bike parked in the bay by its rider (physically ridden up the ramp): snap it into the nearest free slot
    public bool StowParked(Vehicle v)
    {
        Slot s = FreeSlot(v);
        if (s == null) return false;
        Frog rider = v.driver;
        s.bike = v; s.wide = v.body.size.x > 0.95f;
        Freeze(v, true);
        var m = new Move { v = v, rider = rider, slot = s, unload = false };
        m.pts.Add(v.transform.position); m.yaws.Add(v.transform.eulerAngles.y);
        m.pts.Add(transform.TransformPoint(s.pos)); m.yaws.Add(transform.eulerAngles.y);
        m.dur = 0.5f;
        move = m;
        return true;
    }

    void Freeze(Vehicle v, bool on)
    {
        if (on)
        {
            v.stowed = true;
            if (!v.rb.isKinematic) { v.rb.velocity = Vector3.zero; v.rb.angularVelocity = Vector3.zero; }
            v.rb.interpolation = RigidbodyInterpolation.None;
            v.rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            v.rb.isKinematic = true;
            // only the vehicle's own colliders (never the rider's CharacterController, which rides as a child of the seat)
            var off = new List<Collider>();
            v.GetComponentsInChildren(tmpCols);
            foreach (var c in tmpCols)
                if (c != null && c.enabled && !c.isTrigger && !(c is CharacterController) && c.GetComponentInParent<Frog>() == null) { c.enabled = false; off.Add(c); }
            frozenCols[v] = off;
            v.transform.SetParent(transform, true);
        }
        else
        {
            v.transform.SetParent(null, true);
            List<Collider> off;
            if (frozenCols.TryGetValue(v, out off)) { foreach (var c in off) if (c != null) c.enabled = true; frozenCols.Remove(v); }
            v.rb.isKinematic = false;
            v.rb.interpolation = RigidbodyInterpolation.Interpolate;
            v.rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            v.rb.velocity = Vector3.zero; v.rb.angularVelocity = Vector3.zero;
            v.stowed = false;
        }
    }

    void StepCargo(float dt)
    {
        if (!spec.hauler) return;
        if (move != null) return;
        // a loadable vehicle parked inside the bay: an empty one locks in after a moment; a ridden one waits for the
        // rider's "Stow" (TrailerTow prompt) or for them to get off
        foreach (Vehicle v in Vehicle.All)
        {
            if (v == null || v.stowed || !Loadable(v)) continue;
            if (!InBay(v.transform.position)) { parkedT.Remove(v); continue; }
            Vector3 rel = v.rb.velocity - rb.velocity;
            float t; parkedT.TryGetValue(v, out t);
            t = rel.magnitude < 0.6f ? t + dt : 0f;
            parkedT[v] = t;
            if (v.driver == null && t > 1.0f && HasRoomFor(v)) { parkedT.Remove(v); StowParked(v); Sfx.PlayAt(Sfx.Clank != null ? Sfx.Clank : Sfx.Thud, v.transform.position, 0.5f, 30f, 1.3f); break; }
        }
        // something else moved a stowed bike (a reset-home, a wreck respawn): give it its physics back
        foreach (var s in slots)
            if (s.bike != null && (s.bike.transform.parent != transform || (s.bike.transform.localPosition - s.pos).sqrMagnitude > 1f))
            { Vehicle b = s.bike; s.bike = null; Freeze(b, false); Debug.Log("Trailer: " + b.Title + " left the " + Title + " (moved by something else)"); }
        // roll-over: let the cargo go (it would be welded upside down otherwise)
        if (transform.up.y < 0.3f) foreach (var s in slots) if (s.bike != null) { Vehicle b = s.bike; s.bike = null; Freeze(b, false); }
    }

    void LateUpdate()
    {
        if (move == null) return;
        Move m = move;
        if (m.v == null) { move = null; return; }
        m.t += Time.deltaTime;
        // points are captured in world space at the start; the trailer may sag a little but doesn't move (it's parked)
        float u = Mathf.Clamp01(m.t / m.dur);
        int segs = m.pts.Count - 1;
        float fu = Mathf.SmoothStep(0f, 1f, u) * segs;
        int i = Mathf.Min(segs - 1, (int)fu);
        float k = fu - i;
        Vector3 p = Vector3.Lerp(m.pts[i], m.pts[i + 1], k);
        float y = Mathf.LerpAngle(m.yaws[i], m.yaws[i + 1], k);
        // pitch along the ramp
        Vector3 d = m.pts[i + 1] - m.pts[i];
        float pitch = 0f;
        if (d.sqrMagnitude > 0.01f) { Vector3 h = d; h.y = 0f; pitch = -Mathf.Atan2(d.y, Mathf.Max(0.01f, h.magnitude)) * Mathf.Rad2Deg; }
        Vector3 flatFwd = Quaternion.Euler(0f, y, 0f) * Vector3.forward;
        if (Vector3.Dot(flatFwd, d) < 0f) pitch = -pitch;   // backing down: nose stays up
        m.v.transform.SetPositionAndRotation(p, Quaternion.Euler(Mathf.Abs(d.y) > 0.05f ? pitch : 0f, y, 0f));
        if (u >= 1f)
        {
            move = null;
            if (!m.unload)
            {
                m.v.transform.localPosition = m.slot.pos;
                m.v.transform.localRotation = Quaternion.identity;
                if (m.rider != null && m.rider.vehicle == m.v)
                {
                    m.rider.ExitVehicle();
                    Vector3 foot = RampFoot(0.9f) + transform.right * 1.1f;
                    m.rider.Teleport(foot + Vector3.up * 0.1f);
                    if (m.rider.human) m.rider.Toast(m.v.Title + " stowed in the " + spec.shortName + " - tow it anywhere!", 2.8f);
                }
                Sfx.PlayAt(Sfx.Clank != null ? Sfx.Clank : Sfx.Thud, m.v.transform.position, 0.55f, 30f, 1.25f);
                Debug.Log("Trailer: " + m.v.Title + " stowed in " + Title);
            }
            else
            {
                m.slot.bike = null;
                Vector3 pos = m.v.transform.position;
                RaycastHit h;
                if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out h, 6f, WheelMask, QueryTriggerInteraction.Ignore)) pos.y = h.point.y + 0.15f;
                Freeze(m.v, false);
                m.v.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, m.v.transform.eulerAngles.y, 0f));
                m.v.rb.position = pos; m.v.rb.rotation = m.v.transform.rotation;
                m.v.rb.velocity = m.v.transform.forward * 1.2f;   // rolling, not dropped
                Debug.Log("Trailer: unloaded at " + pos.ToString("0.0") + " yaw " + m.v.transform.eulerAngles.y.ToString("0"));
                if (m.rider != null && m.rider.human) m.rider.Toast("Out of the " + spec.shortName + " - ride on!", 2f);
                Debug.Log("Trailer: " + m.v.Title + " unloaded from " + Title);
            }
        }
    }

    public void DropCargoHome()
    {
        foreach (var s in slots) if (s.bike != null) { Vehicle b = s.bike; s.bike = null; Freeze(b, false); b.ResetHome(); }
    }
}
