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
    AudioSource engine;
    float stepT;

    protected PIn inp;
    protected float camYawIn;
    protected Vector3 spawnPos;
    protected Quaternion spawnRot;
    float flipT;

    // layers: 0 world, 8 vehicles, 9 frogs, 10 projectiles, 11 loose props
    public const int VehicleLayer = 8, FrogLayer = 9, ProjectileLayer = 10, PropLayer = 11;
    public static int GroundMask { get { return ~((1 << 2) | (1 << VehicleLayer) | (1 << FrogLayer) | (1 << ProjectileLayer) | (1 << PropLayer)); } }

    public Vector3 Velocity { get { return rb != null ? rb.velocity : Vector3.zero; } }
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

    protected void EngineSound(float dt)
    {
        bool heard = driver != null && driver.human;
        Flyer fl = this as Flyer;
        if (fl != null && fl.returning) heard = true;
        float spd = Speed;
        if (engineKind == 5)
        {
            if (heard && spd > 0.8f)
            {
                stepT -= dt;
                if (stepT <= 0f) { stepT = Mathf.Clamp(1.4f / spd, 0.3f, 0.7f); Sfx.Play(Sfx.Step, 0.55f, Random.Range(0.9f, 1.05f)); }
            }
            return;
        }
        if (engine == null)
        {
            if (!heard) return;
            AudioClip c = engineKind == 1 ? Sfx.EngineTank : engineKind == 2 ? Sfx.Rotor : engineKind == 3 ? Sfx.DroneWhine : engineKind == 4 ? Sfx.BoatMotor : Sfx.EngineCar;
            if (c == null) return;
            engine = Sfx.Loop(gameObject, c);
            engine.Play();
        }
        float target = heard ? (fl != null && fl.returning && driver == null ? 0.12f : 0.32f) : 0f;
        engine.volume = Mathf.MoveTowards(engine.volume, target, dt * 0.8f);
        float load = Mathf.Clamp01(Mathf.Abs(inp.gas - inp.brake) + Mathf.Abs(inp.move.y) + Mathf.Abs(inp.climb));
        engine.pitch = Mathf.Lerp(engine.pitch, 0.75f + spd / 28f + load * 0.15f, dt * 3f);
        if (engine.volume <= 0.001f && engine.isPlaying) engine.Pause();
        else if (engine.volume > 0.001f && !engine.isPlaying) engine.UnPause();
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
        if (rb.position.y < -40f || Mathf.Abs(rb.position.x) > Layout.Half + 20f || Mathf.Abs(rb.position.z) > Layout.Half + 20f)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = spawnPos + Vector3.up;
            rb.rotation = spawnRot;
        }
    }

    public Vector3 HomePos { get { return spawnPos; } }

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
