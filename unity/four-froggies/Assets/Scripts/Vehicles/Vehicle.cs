using System.Collections.Generic;
using UnityEngine;

// Base for everything a froggy can climb into. Physics runs in FixedUpdate from the latest PIn.
public abstract class Vehicle : MonoBehaviour
{
    public static readonly List<Vehicle> All = new List<Vehicle>();

    public string Title = "Ride";
    public string EnterVerb = "get in";
    public Frog driver;
    public Transform seat;
    public float seatScale = 0.75f;
    public bool showDriver = true;
    public Rigidbody rb;
    public BoxCollider body;
    public float camDistance = 11f, camHeight = 2.5f;
    public bool flyer;
    public int engineKind;   // 0 car, 1 tracks, 2 rotor, 3 drone, 4 boat, 5 mech (footsteps)
    float stepT;

    protected PIn inp;
    protected float camYawIn;
    protected Vector3 spawnPos;
    protected Quaternion spawnRot;
    float flipT;

    // layers: 0 world, 8 vehicles, 9 frogs, 10 projectiles, 11 loose props
    public const int VehicleLayer = 8, FrogLayer = 9, ProjectileLayer = 10, PropLayer = 11;
    public static int GroundMask { get { return ~((1 << 2) | (1 << VehicleLayer) | (1 << FrogLayer) | (1 << ProjectileLayer) | (1 << PropLayer)); } }

    protected Vector3 kinVel;      // kinematic vehicles (the space Starship) report their own velocity
    public Vector3 Velocity { get { return rb != null && !rb.isKinematic ? rb.velocity : kinVel; } }
    public float Speed { get { Vector3 v = Velocity; v.y = 0f; return v.magnitude; } }
    public float ForwardSpeed { get { return Vector3.Dot(Velocity, transform.forward); } }

    public virtual string HelpLine { get { return "L-stick steer | RT gas | LT brake/reverse | A get out"; } }
    // world yaw the camera should hold (tank turret), or NaN to follow the body
    public virtual float AimYaw { get { return float.NaN; } }

    protected void SetupBody(float mass, Vector3 center, Vector3 size, Vector3 com)
    {
        gameObject.layer = VehicleLayer;
        rb = gameObject.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.drag = 0.05f;
        rb.angularDrag = 0.8f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        body = gameObject.AddComponent<BoxCollider>();
        body.center = center;
        body.size = size;
        var pm = new PhysicMaterial("VehicleBody");
        pm.dynamicFriction = 0.25f;
        pm.staticFriction = 0.3f;
        pm.bounciness = 0.05f;
        pm.frictionCombine = PhysicMaterialCombine.Minimum;
        body.sharedMaterial = pm;
        rb.centerOfMass = com;
        spawnPos = transform.position;
        spawnRot = transform.rotation;
        All.Add(this);
    }

    public void SetupBodyPublic(float mass, Vector3 center, Vector3 size, Vector3 com) { SetupBody(mass, center, size, com); }
    public BoxCollider ExtraBoxPublic(Vector3 center, Vector3 size) { return ExtraBox(center, size); }

    protected BoxCollider ExtraBox(Vector3 center, Vector3 size)
    {
        var b = gameObject.AddComponent<BoxCollider>();
        b.center = center;
        b.size = size;
        b.sharedMaterial = body.sharedMaterial;
        return b;
    }

    protected void OnDestroy() { All.Remove(this); }

    // called by the driver's Frog.Update every frame
    public virtual void Drive(PIn i, float camYaw, float dt)
    {
        inp = i;
        camYawIn = camYaw;
    }

    public virtual void OnEnter() { if (rb != null) rb.WakeUp(); }
    public virtual void OnExit() { inp = new PIn(); }

    public float enginePitch = 1f;     // per-vehicle engine pitch (the Ripsaw M5 runs its diesel lower than the EV2)
    Sfx.EngineVoice voice;
    float crashCool;
    // GroundVehicle fills these for the skid layer
    [System.NonSerialized] public float slipSpeed, groundFrac = 1f;

    protected void EngineSound(float dt)
    {
        bool heard = driver != null && driver.human;
        Flyer fl = this as Flyer;
        if (fl != null && fl.returning) heard = true;
        float spd = Speed;
        crashCool -= dt;
        if (engineKind == 6) return;   // story mechs make their own footsteps
        if (engineKind == 5 && heard && spd > 0.8f)
        {
            stepT -= dt;
            if (stepT <= 0f) { stepT = Mathf.Clamp(1.4f / spd, 0.3f, 0.7f); Sfx.Play(Sfx.StepMetal != null ? Sfx.StepMetal : Sfx.Step, 0.5f, Random.Range(0.85f, 1.05f)); if (Random.value < 0.35f) Sfx.Play(Sfx.Pick(Sfx.Servo), 0.18f, Random.Range(0.9f, 1.2f)); }
        }
        if (voice == null)
        {
            if (!heard) return;
            voice = new Sfx.EngineVoice(gameObject, engineKind, enginePitch);
        }
        float load = Mathf.Clamp01(Mathf.Abs(inp.gas - inp.brake) + Mathf.Abs(inp.move.y) + Mathf.Abs(inp.climb));
        bool on = heard && !(fl != null && fl.returning && driver == null && false);
        voice.Tick(on, spd, load, slipSpeed, groundFrac, transform.position, dt);
        if (fl != null && fl.returning && driver == null && voice.main != null) voice.main.volume = Mathf.Min(voice.main.volume, 0.12f);
    }

    // ffu10: impact sounds (metal crunch for hard hits, soft bump for small ones)
    protected virtual void OnCollisionEnter(Collision c)
    {
        if (rb == null || rb.isKinematic || crashCool > 0f) return;
        float v = c.relativeVelocity.magnitude;
        if (v < 3.5f) return;
        crashCool = 0.25f;
        Vector3 p = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
        if (v > 8f) Sfx.PlayAt(Sfx.Pick(Sfx.Crash), p, Mathf.Clamp01(v / 22f) * 0.9f, 60f, Random.Range(0.85f, 1.05f));
        else Sfx.PlayAt(Sfx.BumpSoft != null ? Sfx.BumpSoft : Sfx.Thud, p, Mathf.Clamp01(v / 10f) * 0.6f, 40f, Random.Range(0.9f, 1.1f));
    }

    protected virtual void FixedUpdate()
    {
        EngineSound(Time.fixedDeltaTime);
        if (driver == null) inp = new PIn();
        // self-righting when flipped and slow
        if (!flyer)
        {
            bool loop = false;
            foreach (Bounds b in RallyTrack.LoopZones) if (b.Contains(rb.position)) loop = true;
            if (!loop && Vector3.Dot(transform.up, Vector3.up) < 0.35f && rb.velocity.magnitude < 3f) flipT += Time.fixedDeltaTime;
            else flipT = 0f;
            if (flipT > 1.6f)
            {
                flipT = 0f;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = rb.position + Vector3.up * 2f;
                rb.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            }
        }
        if (OutOfWorld(rb.position))
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = spawnPos + Vector3.up;
            rb.rotation = spawnRot;
        }
    }

    public Vector3 HomePos { get { return spawnPos; } }

    // vehicles that live in another world (Curiosity on Mars...) set a centre + radius
    public bool hasWorldBounds;
    public Vector3 worldCenter;
    public float worldRadius = 100f;
    public virtual bool CanExit(Frog f) { return true; }
    public virtual bool CanEnter(Frog f) { return true; }
    public virtual string DeniedLine { get { return Title; } }

    protected virtual bool OutOfWorld(Vector3 p)
    {
        if (hasWorldBounds) { Vector3 l = p - worldCenter; return l.y < -60f || new Vector2(l.x, l.z).magnitude > worldRadius; }
        return p.y < -40f || Mathf.Abs(p.x) > Layout.Half + 20f || Mathf.Abs(p.z) > Layout.Half + 20f;
    }

    public Vector3 ExitPoint()
    {
        Vector3 c = transform.position;
        float hw = body.size.x * 0.5f + 1.3f, hl = body.size.z * 0.5f + 1.6f;
        Vector3 right = transform.right; right.y = 0f; right.Normalize();
        Vector3 fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
        Vector3[] cands = { c - right * hw, c + right * hw, c - fwd * hl, c + fwd * hl, c - right * (hw + 2f), c + right * (hw + 2f) };
        int frogMask = ~((1 << 2) | (1 << FrogLayer) | (1 << ProjectileLayer));
        foreach (Vector3 q in cands)
        {
            RaycastHit hit;
            Vector3 from = q + Vector3.up * 4f;
            if (!Physics.Raycast(from, Vector3.down, out hit, 60f, GroundMask)) continue;
            Vector3 p = hit.point + Vector3.up * 0.1f;
            if (p.y > c.y + 2.5f) continue;
            if (Physics.CheckCapsule(p + Vector3.up * 0.5f, p + Vector3.up * 1.0f, 0.42f, frogMask, QueryTriggerInteraction.Ignore)) continue;
            return p;
        }
        return c + Vector3.up * (body.center.y + body.size.y * 0.5f + 0.6f);
    }

    public static Vehicle Nearest(Vector3 p, Frog asker)
    {
        Vehicle best = null;
        float bd = 2.6f;
        foreach (Vehicle v in All)
        {
            if (v == null || v.body == null) continue;
            Vector3 cp = v.body.ClosestPoint(p + Vector3.up * 0.6f);
            float d = (cp - (p + Vector3.up * 0.6f)).magnitude;
            if (d < bd) { bd = d; best = v; }
        }
        return best;
    }

    protected static float YawRate(Rigidbody rb, Transform t) { return Vector3.Dot(rb.angularVelocity, t.up); }
}
