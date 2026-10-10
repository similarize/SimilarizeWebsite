using System.Collections.Generic;
using UnityEngine;

// ffu26 (Bill: "sometimes they mow with a riding lawnmower, not just the push mower" + "players should be able to drive the
// riding mower too"): sit-on lawn tractors that are real ranch vehicles (GroundVehicle: raycast wheels, small-engine
// voice), so James, the other frogs and every playable character get on with the usual enter key / button / touch
// (E / Y / A / tap) and steer it like a small vehicle - split-screen and every input device work like any other vehicle.
// Three of them: two parked in the garage (beside the Optimus suit bay) and one by the north-west lawn (RidingMower.I).
// Robots on the "Mow lawn" chore take the lawn one about 40% of the time (phone CHORES "Mow (riding)" / "Mow (push)"):
// the robot climbs on, its hidden pilot frog becomes the driver (so no player can take it), the mower turns kinematic and
// rides under the robot's root (the chore planner moves the robot), lanes across the lawn, then it is parked back. A mower
// a player is on is never taken by a robot (it push-mows instead). Model: chassis, hood + grille + headlights, big rear /
// small front wheels, steering wheel, seat + backrest, mowing deck + side chute (clippings fly out), exhaust. Fresh-cut
// stripes (MowStripes) behind every mower while it cuts grass, light one way and dark the other.
public class RidingMower : GroundVehicle
{
    public static RidingMower I;                       // the lawn mower the robots use
    public static readonly List<RidingMower> AllMowers = new List<RidingMower>();
    public Robot rider;
    public bool robotRide;
    public int lane;                                   // next lane across the lawn (shared by every riding job)
    public static readonly Vector2 ParkXZ = new Vector2(-95f, 26.6f);
    public const float ParkYaw = 0f;       // facing north, into the lawn
    public const float BaseH = 1.75f;      // built for a 1.75 m robot; scaled to the robot rider
    public const float SeatY = 0.8f;       // seat top above the ground (robot hips go here)
    public const float DeckW = 1.25f;
    readonly Transform[] wheelVis = new Transform[4];
    Transform steerT, chute, vis;
    AudioSource engine;
    Vector3 lastPos; float lastYaw, wheelSpin, steerK, fxT;

    public static void Ensure()
    {
        if (I != null) return;
        I = Spawn("Riding mower", RobotNav.G(ParkXZ.x, ParkXZ.y), ParkYaw, new Color(0.16f, 0.5f, 0.2f));
        // two more in the garage, either side of the Optimus suit bay
        float bx = Layout.BayX(5), bz = Layout.GarageC.y;
        Spawn("Riding mower", RobotNav.G(bx - 3.0f, bz), 0f, new Color(0.85f, 0.18f, 0.14f));
        Spawn("Riding mower", RobotNav.G(bx + 3.0f, bz), 0f, new Color(0.16f, 0.5f, 0.2f));
    }

    static RidingMower Spawn(string title, Vector3 ground, float yaw, Color paint)
    {
        var go = new GameObject(title);
        go.transform.position = ground + Vector3.up * 0.25f;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        var m = go.AddComponent<RidingMower>();
        m.Title = title;
        m.EnterVerb = "ride the mower";
        m.engineKind = 4; m.enginePitch = 1.45f;        // small-engine putter
        m.SetupBodyPublic(320f, new Vector3(0f, 0.55f, 0.45f), new Vector3(1.05f, 0.7f, 1.95f), new Vector3(0f, 0.32f, 0.4f));
        m.rest = 0.12f;
        m.maxSpeed = 6.5f; m.accel = 5f; m.turnRate = 1.9f; m.grip = 9f; m.reverseFrac = 0.5f; m.dustAmount = 0.15f;
        m.camDistance = 5.2f; m.camHeight = 2.3f;
        m.Build(paint);
        m.seat = Mats.Node(go.transform, "Seat", new Vector3(0f, SeatY + 0.02f, -0.06f));
        m.seatScale = 0.62f;
        m.FinishSetup();
        Mats.SetLayer(go, VehicleLayer);
        AllMowers.Add(m);
        m.lastPos = go.transform.position;
        return m;
    }

    void Build(Color paint)
    {
        vis = Mats.Node(transform, "Vis", Vector3.zero);
        Transform v = vis;
        Material body = Mats.Paint(paint, 0.75f), yellow = Mats.Paint(new Color(0.98f, 0.8f, 0.12f), 0.7f);
        Material dark = Mats.Lit(new Color(0.07f, 0.07f, 0.08f)), tyre = Mats.Lit(new Color(0.05f, 0.05f, 0.055f)), steel = Mats.Steel(new Color(0.55f, 0.56f, 0.6f));
        Material seatM = Mats.Paint(new Color(0.1f, 0.1f, 0.11f), 0.35f), lightM = Mats.Unlit(new Color(1f, 0.97f, 0.85f)), deckM = Mats.Paint(new Color(0.2f, 0.21f, 0.22f), 0.5f);
        // origin = seat point on the ground; +z forward. Rear axle under the seat, front axle 1.25 m ahead.
        float rearZ = -0.15f, frontZ = 1.1f;
        Mats.Prim(PrimitiveType.Cube, v, new Vector3(0f, 0.42f, 0.45f), new Vector3(0.78f, 0.16f, 1.75f), body);                 // frame / footboards
        Mats.Prim(PrimitiveType.Cube, v, new Vector3(0f, 0.36f, 0.42f), new Vector3(1.0f, 0.04f, 0.7f), dark);                   // foot plates
        Mats.Prim(PrimitiveType.Cube, v, new Vector3(0f, 0.68f, 1.05f), new Vector3(0.72f, 0.4f, 0.75f), body);                  // hood
        var hoodTop = Mats.Prim(PrimitiveType.Cube, v, new Vector3(0f, 0.86f, 0.98f), new Vector3(0.66f, 0.08f, 0.62f), body);
        hoodTop.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);
        Mats.Prim(PrimitiveType.Cube, v, new Vector3(0f, 0.66f, 1.43f), new Vector3(0.6f, 0.32f, 0.04f), dark);                  // grille
        for (int i = 0; i < 4; i++) Mats.Prim(PrimitiveType.Cube, v, new Vector3(0f, 0.54f + i * 0.075f, 1.452f), new Vector3(0.56f, 0.018f, 0.01f), steel);
        for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, v, new Vector3(s * 0.25f, 0.8f, 1.44f), new Vector3(0.12f, 0.06f, 0.03f), lightM);   // headlights
        Mats.Prim(PrimitiveType.Cube, v, new Vector3(0f, 0.6f, 1.06f), new Vector3(0.74f, 0.05f, 0.7f), yellow);               // side stripe
        Mats.Prim(PrimitiveType.Cube, v, new Vector3(0f, 0.58f, rearZ - 0.05f), new Vector3(1.0f, 0.18f, 0.8f), body);
        for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, v, new Vector3(s * 0.46f, 0.72f, rearZ), new Vector3(0.2f, 0.1f, 0.72f), body);   // fenders
        Mats.Prim(PrimitiveType.Cube, v, new Vector3(0f, SeatY - 0.06f, -0.05f), new Vector3(0.48f, 0.1f, 0.42f), seatM);
        var back = Mats.Prim(PrimitiveType.Cube, v, new Vector3(0f, SeatY + 0.2f, -0.29f), new Vector3(0.48f, 0.42f, 0.08f), seatM);
        back.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
        Mats.Prim(PrimitiveType.Cube, v, new Vector3(0f, SeatY - 0.115f, -0.05f), new Vector3(0.5f, 0.02f, 0.44f), yellow);
        var col = Mats.Prim(PrimitiveType.Cylinder, v, new Vector3(0f, 0.95f, 0.62f), new Vector3(0.05f, 0.22f, 0.05f), dark);
        col.transform.localRotation = Quaternion.Euler(-28f, 0f, 0f);
        steerT = Mats.Node(v, "Steering", new Vector3(0f, 1.15f, 0.52f));
        steerT.localRotation = Quaternion.Euler(-28f, 0f, 0f);
        Mats.Prim(PrimitiveType.Cylinder, steerT, Vector3.zero, new Vector3(0.36f, 0.015f, 0.36f), dark);
        Mats.Prim(PrimitiveType.Cylinder, steerT, new Vector3(0f, 0.012f, 0f), new Vector3(0.3f, 0.012f, 0.3f), Mats.Lit(new Color(0.18f, 0.18f, 0.2f)));
        Mats.Prim(PrimitiveType.Cube, steerT, new Vector3(0f, 0.02f, 0f), new Vector3(0.34f, 0.02f, 0.03f), dark);
        Mats.Prim(PrimitiveType.Cube, steerT, new Vector3(0f, 0.025f, 0f), new Vector3(0.07f, 0.02f, 0.07f), yellow);
        float[] wz = { rearZ, rearZ, frontZ, frontZ }, wr = { 0.31f, 0.31f, 0.2f, 0.2f }, wx = { -0.5f, 0.5f, -0.4f, 0.4f };
        for (int i = 0; i < 4; i++)
        {
            Vector3 c = new Vector3(wx[i], wr[i], wz[i]);
            Transform w = Mats.Node(v, "Wheel", c);
            Mats.Prim(PrimitiveType.Cylinder, w, Vector3.zero, new Vector3(wr[i] * 2f, i < 2 ? 0.14f : 0.08f, wr[i] * 2f), new Vector3(0f, 0f, 90f), tyre);
            Mats.Prim(PrimitiveType.Cylinder, w, new Vector3(Mathf.Sign(wx[i]) * (i < 2 ? 0.13f : 0.075f), 0f, 0f), new Vector3(wr[i] * 1.1f, 0.02f, wr[i] * 1.1f), new Vector3(0f, 0f, 90f), yellow);
            Mats.Prim(PrimitiveType.Cube, w, new Vector3(Mathf.Sign(wx[i]) * (i < 2 ? 0.135f : 0.08f), 0f, 0f), new Vector3(0.01f, wr[i] * 1.6f, 0.05f), dark);
            wheelVis[i] = w;
            AddWheel(c, wr[i], w, i >= 2);
        }
        Mats.Prim(PrimitiveType.Cube, v, new Vector3(0f, 0.17f, 0.5f), new Vector3(DeckW, 0.12f, 0.75f), deckM);
        Mats.Prim(PrimitiveType.Cube, v, new Vector3(0f, 0.235f, 0.5f), new Vector3(DeckW - 0.08f, 0.02f, 0.68f), yellow);
        for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cylinder, v, new Vector3(s * (DeckW * 0.5f - 0.04f), 0.08f, 0.85f), new Vector3(0.08f, 0.02f, 0.08f), new Vector3(0f, 0f, 90f), dark);
        chute = Mats.Node(v, "Chute", new Vector3(DeckW * 0.5f + 0.12f, 0.17f, 0.5f));
        var ch = Mats.Prim(PrimitiveType.Cube, chute, Vector3.zero, new Vector3(0.26f, 0.1f, 0.3f), deckM);
        ch.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
        Mats.Prim(PrimitiveType.Cylinder, v, new Vector3(-0.3f, 0.98f, 0.72f), new Vector3(0.05f, 0.12f, 0.05f), steel);
        if (Look.Mobile) Mats.NoShadows(gameObject);
        engine = Sfx.Loop(gameObject, Sfx.BoatMotor != null ? Sfx.BoatMotor : Sfx.EngineCar);
    }

    // players: free unless a robot is on it
    public override bool CanEnter(Frog f) { return !robotRide && rider == null && base.CanEnter(f); }
    public bool FreeFor(Robot r) { return (rider == null || rider == r) && (driver == null || driver == r.Pilot) && !Wrecked; }

    public Vector3 MountSpot { get { Vector3 p = transform.position - transform.right * 1.1f; return RobotNav.G(p.x, p.z); } }
    public float Yaw { get { return transform.eulerAngles.y; } }

    protected override void FixedUpdate()
    {
        if (robotRide) return;       // kinematic under the robot: the robot moves it
        base.FixedUpdate();
    }

    // the robot climbs on: its pilot frog takes the driver seat (players can't take it), the mower rides under the robot
    public bool Attach(Robot r)
    {
        if (!FreeFor(r)) return false;
        Frog pl = r.Pilot;
        if (driver == null) { pl.world = WorldId.Ranch; pl.EnterVehicle(this); }
        if (driver != pl) return false;
        rider = r; robotRide = true;
        rb.isKinematic = true;
        transform.SetParent(r.transform, false);
        float k = Mathf.Clamp(r.height / BaseH, 0.75f, 2.1f);
        transform.localScale = Vector3.one * k / Mathf.Max(0.01f, r.transform.lossyScale.x);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        lastPos = transform.position; lastYaw = transform.eulerAngles.y;
        Sfx.PlayAt(Sfx.Clank != null ? Sfx.Clank : Sfx.Thud, transform.position, 0.4f, 30f, 1.3f);
        return true;
    }

    public void Detach(Robot r)
    {
        if (rider != r) return;
        Vector3 p = transform.position; float y = transform.eulerAngles.y;
        transform.SetParent(null, true);
        transform.localScale = Vector3.one;
        p.y = Ranch.GY(p.x, p.z) + 0.15f;
        transform.position = p; transform.rotation = Quaternion.Euler(0f, y, 0f);
        rb.position = p; rb.rotation = transform.rotation;
        rb.isKinematic = false; rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
        robotRide = false; rider = null;
        Frog pl = r.Pilot;
        if (pl.vehicle == this) { pl.ExitVehicle(); pl.cc.enabled = false; }
        MowStripes.End(this);
    }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.1f);
        if (rider != null && rider.transform != transform.parent) Detach(rider);
        Vector3 p = transform.position;
        Vector3 d = p - lastPos; d.y = 0f;
        float sp = Mathf.Min(20f, d.magnitude / Mathf.Max(dt, 1e-4f));
        float yawRate = Mathf.DeltaAngle(lastYaw, transform.eulerAngles.y) / Mathf.Max(dt, 1e-4f);
        lastPos = p; lastYaw = transform.eulerAngles.y;
        float k = transform.lossyScale.x;
        if (robotRide)
        {
            // GroundVehicle is off while kinematic: spin the wheels ourselves
            wheelSpin += sp * dt / Mathf.Max(0.1f, k);
            for (int i = 0; i < 4; i++) if (wheelVis[i] != null) wheelVis[i].localRotation = Quaternion.Euler(wheelSpin / (i < 2 ? 0.31f : 0.2f) * Mathf.Rad2Deg, i >= 2 ? Mathf.Clamp(yawRate * 0.15f, -30f, 30f) : 0f, 0f);
            EngineVol(sp > 0.2f ? 0.24f : 0.13f, 1.0f + Mathf.Clamp01(sp / 3f) * 0.35f, dt);
        }
        else EngineVol(0f, 1f, dt);
        steerK = Mathf.Lerp(steerK, Mathf.Clamp(yawRate * 0.6f, -110f, 110f), dt * 6f);
        if (steerT != null) steerT.localRotation = Quaternion.Euler(-28f, 0f, 0f) * Quaternion.Euler(0f, -steerK, 0f);
        bool player = !robotRide && driver != null && driver.robotPilot == null;
        bool cutting = sp > 0.4f && (robotRide ? Mowing(p) : player && Grass(p));
        fxT -= dt;
        if (fxT <= 0f && sp > 0.3f && (robotRide || player))
        {
            fxT = Look.Mobile ? 0.14f : 0.07f;
            Vector3 cp = chute.position + transform.right * 0.15f * k;
            if (cutting) FX.Spray(cp, (transform.right * 2.2f + Vector3.up * 1.1f + Random.insideUnitSphere * 0.6f) * k, 0.07f * k, 0.6f, new Color(0.35f, 0.68f, 0.2f, 0.95f));
            if (cutting && Random.value < 0.5f) FX.Smoke(cp + transform.right * 0.3f * k, 0.35f * k, new Color(0.42f, 0.7f, 0.25f, 0.8f));
            if (Random.value < 0.25f) FX.Smoke(transform.TransformPoint(new Vector3(-0.3f, 1.12f, 0.72f)), 0.18f * k, new Color(0.5f, 0.5f, 0.52f, 0.4f));
        }
        MowStripes.Track(this, transform.TransformPoint(new Vector3(0f, 0f, 0.5f)), transform.forward, DeckW * k, cutting);
    }

    void EngineVol(float baseV, float pitch, float dt)
    {
        if (engine == null) return;
        float v = 0f;
        if (baseV > 0f) { float kk = Mathf.Clamp01(1f - Sfx.Near(transform.position) / 40f); v = baseV * kk * kk; }
        engine.volume = Mathf.MoveTowards(engine.volume, v, dt * 0.8f);
        engine.pitch = Mathf.Lerp(engine.pitch <= 0f ? 1f : engine.pitch, pitch * 1.25f, dt * 3f);
        if (engine.volume > 0.004f) { if (!engine.isPlaying) engine.Play(); } else if (engine.isPlaying) engine.Stop();
    }

    public static bool Mowing(Vector3 p)
    {
        Rect m = RanchJobs.MowArea;
        return p.x > m.xMin - 1.5f && p.x < m.xMax + 1.5f && p.z > m.yMin - 1.5f && p.z < m.yMax + 1.5f;
    }

    // players mow any ranch grass: not the pond, the house, the garage, the track or a pad
    public static bool Grass(Vector3 p)
    {
        if (Mathf.Abs(p.x) > Layout.Half - 10f || Mathf.Abs(p.z) > Layout.Half - 10f) return false;
        if (Layout.InPond(p.x, p.z) || RobotNav.InGarage(p) || RobotNav.Blocked(p.x, p.z)) return false;
        if (Mathf.Abs(p.x - Layout.HouseC.x) < 30f && Mathf.Abs(p.z - Layout.HouseC.y) < 22f) return false;
        if (MissionSite.OnSlab(p.x, p.z)) return false;
        return p.y < Ranch.GY(p.x, p.z) + 0.8f;
    }
}

// fresh-cut stripes: one growing ribbon per mower while it cuts in a straight line; light green going north, darker
// going south (like a real striped lawn). Ribbons fade back into the grass after a few minutes.
public static class MowStripes
{
    class Ribbon { public GameObject go; public Mesh mesh; public List<Vector3> pts = new List<Vector3>(); public Vector3 dir; public float w, born; public bool open; public Material mat; }
    static readonly Dictionary<object, Ribbon> live = new Dictionary<object, Ribbon>();
    static readonly List<Ribbon> all = new List<Ribbon>();
    static Material light, dark;
    const float Life = 240f;

    public static void End(object key) { Ribbon r; if (live.TryGetValue(key, out r)) { r.open = false; live.Remove(key); } }

    public static void Track(object key, Vector3 at, Vector3 fwd, float width, bool cutting)
    {
        Prune();
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) return;
        fwd.Normalize();
        Ribbon r;
        live.TryGetValue(key, out r);
        if (!cutting) { if (r != null) End(key); return; }
        // a new ribbon when there is none or the heading turned more than ~20 degrees
        if (r != null && Vector3.Dot(r.dir, fwd) < 0.94f) { End(key); r = null; }
        if (r == null)
        {
            if (light == null) { light = Mats.Lit(new Color(0.47f, 0.7f, 0.29f)); dark = Mats.Lit(new Color(0.27f, 0.46f, 0.17f)); }
            r = new Ribbon { dir = fwd, w = width, born = Time.time, open = true };
            r.go = new GameObject("Mow stripe");
            r.mesh = new Mesh();
            r.go.AddComponent<MeshFilter>().sharedMesh = r.mesh;
            var mr = r.go.AddComponent<MeshRenderer>();
            r.mat = new Material(fwd.z >= 0f ? light : dark);
            mr.sharedMaterial = r.mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            live[key] = r; all.Add(r);
            r.pts.Add(at);
            while (all.Count > 36) Kill(all[0]);
            return;
        }
        Vector3 last = r.pts[r.pts.Count - 1];
        Vector3 dd = at - last; dd.y = 0f;
        if (dd.sqrMagnitude < 0.36f) return;          // a point every 0.6 m
        r.pts.Add(at);
        r.born = Time.time;
        Rebuild(r);
    }

    static void Rebuild(Ribbon r)
    {
        int n = r.pts.Count;
        if (n < 2) return;
        var v = new Vector3[n * 2]; var t = new int[(n - 1) * 6]; var nm = new Vector3[n * 2];
        Vector3 side = new Vector3(r.dir.z, 0f, -r.dir.x) * r.w * 0.5f;
        for (int i = 0; i < n; i++)
        {
            Vector3 p = r.pts[i];
            Vector3 a = p - side, b = p + side;
            a.y = Ranch.GY(a.x, a.z) + 0.06f; b.y = Ranch.GY(b.x, b.z) + 0.06f;
            v[i * 2] = a; v[i * 2 + 1] = b; nm[i * 2] = nm[i * 2 + 1] = Vector3.up;
            if (i < n - 1) { int k = i * 6, q = i * 2; t[k] = q; t[k + 1] = q + 2; t[k + 2] = q + 1; t[k + 3] = q + 1; t[k + 4] = q + 2; t[k + 5] = q + 3; }
        }
        r.mesh.Clear();
        r.mesh.vertices = v; r.mesh.normals = nm; r.mesh.triangles = t;
        r.mesh.RecalculateBounds();
    }

    static float pruneT;
    static void Prune()
    {
        if (Time.time - pruneT < 2f) return;
        pruneT = Time.time;
        for (int i = all.Count - 1; i >= 0; i--)
        {
            var r = all[i];
            if (r.open) continue;
            float age = Time.time - r.born;
            if (age > Life) { Kill(r); continue; }
            if (age > Life * 0.7f && r.mat != null)
            {
                // fade toward the lawn colour by sinking the ribbon under the grass a little at a time
                if (r.go != null) r.go.transform.position = Vector3.down * Mathf.Lerp(0f, 0.06f, (age - Life * 0.7f) / (Life * 0.3f));
            }
        }
    }

    static void Kill(Ribbon r)
    {
        all.Remove(r);
        foreach (var kv in live) if (kv.Value == r) { live.Remove(kv.Key); break; }
        if (r.go != null) Object.Destroy(r.go);
        if (r.mesh != null) Object.Destroy(r.mesh);
        if (r.mat != null) Object.Destroy(r.mat);
    }
}
