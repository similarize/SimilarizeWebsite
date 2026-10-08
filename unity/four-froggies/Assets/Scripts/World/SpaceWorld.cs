using System.Collections.Generic;
using UnityEngine;

// Space, mirroring the three.js ff3d112 solar system at 10 m per 3D unit: compressed orbits
// (Mercury 400 m ... Pluto 3350 m), Sun r 110 m, Kepler periods (Earth 600 s), the Station in the 54 m low
// Earth parking orbit, the Moon 180 m out, Callisto 240 m from Jupiter. Board the Starship on the ranch pad:
// it launches into sunlit Earth orbit beside the Station.
// Gamepad-first flight with assists (same mapping as 3D): D-pad < > target, X auto-transfer (arc + time warp),
// LB / RB warp x1/x4/x16/x64 while coasting, L-stick turn + thrust, RT boost, LT brake vs the nearest body,
// in orbit push up / hold RT ~0.6 s to burn out, Y land (Earth -> ranch, Mars, Callisto). Approach assist
// captures you into orbit when slow near a body; the Sun's heat shield bounces you back.
// Keys: T / Shift+T target, G auto, Z / C warp, F / Enter land, WASD fly, Shift brake, Space boost.
// Touch: A = next target, FIRE = auto, MSL = land, UP = burn / boost, DOWN = brake.
public class SpaceWorld : MonoBehaviour
{
    public static SpaceWorld I;
    public const float K = 10f;                 // metres per three.js unit
    public class Body
    {
        public string id, name, kind;
        public int parent = -1;
        public float a, bas, w, r, soft, cap;
        public Transform t;
        public bool landable;
        public Vector3 pos, vel;                 // current (world)
    }
    public readonly List<Body> bodies = new List<Body>();
    public readonly List<int> targets = new List<int>();
    public float simT;
    Transform root, belt, stars;
    public Starship ship;
    bool built;
    readonly List<LineRenderer> orbitLines = new List<LineRenderer>();
    LineRenderer path, targetRing, targetArrow, progradeLine;

    public static Vector3 O { get { return Worlds.SpaceO; } }

    public static void Create()
    {
        var go = new GameObject("SpaceWorld");
        I = go.AddComponent<SpaceWorld>();
    }

    void AddBody(string id, string name, string kind, int parent, float a, float bas, float wOrPeriod, bool isPeriod, float r, float soft, float cap, bool land)
    {
        var b = new Body { id = id, name = name, kind = kind, parent = parent, a = a * K, bas = bas, w = isPeriod ? (wOrPeriod > 0f ? 1f / wOrPeriod : 0f) : wOrPeriod, r = r * K, soft = soft * K, cap = cap * K, landable = land };
        bodies.Add(b);
    }

    public int Find(string id) { for (int i = 0; i < bodies.Count; i++) if (bodies[i].id == id) return i; return -1; }

    // body position at sim time t (world)
    public Vector3 PosAt(int i, float t)
    {
        Body b = bodies[i];
        Vector3 c = b.parent >= 0 ? PosAt(b.parent, t) : O;
        if (b.a <= 0f) return c;
        float ang = b.bas + b.w * t;
        return c + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * b.a;
    }

    public Vector3 VelAt(int i, float t) { return (PosAt(i, t + 0.05f) - PosAt(i, t - 0.05f)) / 0.1f; }

    // ---------------- launch / land ----------------
    public void Launch(Frog f)
    {
        if (!built) Build();
        Sfx.Play(Sfx.Boom, 0.9f, 0.6f);
        if (ship.driver == null)
        {
            int earth = Find("earth"), st = Find("station");
            float sAng = Mathf.Atan2((PosAt(st, simT) - PosAt(earth, simT)).z, (PosAt(st, simT) - PosAt(earth, simT)).x);
            ship.EnterOrbit(earth, bodies[st].a, sAng - 0.18f);
            f.SendTo(WorldId.Space, ship.transform.position, 0f);
            f.EnterVehicle(ship);
            f.Toast("LIFTOFF! Parked in sunlit Earth orbit beside the Station.  D-pad < > target, X auto-transfer, Y land", 5f);
        }
        else
        {
            f.SendTo(WorldId.Space, ship.transform.position, 0f);
            f.BoardAsPassenger(ship);
            f.Toast("Liftoff! You're riding with " + ship.driver.nick, 3f);
        }
    }

    // back to orbit from a surface (Mars / Callisto) via the Starship standing there
    public void ToOrbit(Frog f, string bodyId)
    {
        if (!built) Build();
        int b = Find(bodyId);
        if (ship.driver == null)
        {
            ship.EnterOrbit(b, bodies[b].cap, Random.value * 6.28f);
            f.SendTo(WorldId.Space, ship.transform.position, 0f);
            f.EnterVehicle(ship);
            f.Toast("Back in " + bodies[b].name + " orbit. Earth is the next target.", 3.5f);
            ship.SetTarget(Find("earth"));
        }
        else
        {
            f.SendTo(WorldId.Space, ship.transform.position, 0f);
            f.BoardAsPassenger(ship);
            f.Toast("Beamed up to the Starship with " + ship.driver.nick, 3f);
        }
        Sfx.Play(Sfx.Boom, 0.7f, 0.7f);
    }

    public void Land(int bodyIdx)
    {
        Body b = bodies[bodyIdx];
        var crew = new List<Frog>();
        if (ship.driver != null) crew.Add(ship.driver);
        if (Game.I != null) foreach (Frog f in Game.I.frogs) if (f != null && f.passengerOf == ship) crew.Add(f);
        if (!b.landable)
        {
            if (ship.driver != null) ship.driver.Toast("Can't land on " + b.name + " - Earth, Mars and Callisto are landable", 3f);
            return;
        }
        int k = 0;
        foreach (Frog f in crew)
        {
            if (b.id == "earth")
            {
                Vector2 pc = Layout.PadC;
                float x = pc.x + 16f, z = pc.y - 4.5f + k * 3f;
                f.SendTo(WorldId.Ranch, new Vector3(x, Ranch.GY(x, z) + 1.2f, z), 90f);
                f.Toast("Touchdown at the ranch!", 3f);
            }
            else if (b.id == "mars") SurfaceWorlds.LandMars(f, k);
            else SurfaceWorlds.LandCallisto(f, k);
            k++;
        }
        Sfx.Play(Sfx.Boom, 0.8f, 0.5f);
    }

    // ---------------- build ----------------
    void Build()
    {
        built = true;
        root = new GameObject("Space").transform;
        AddBody("sun", "Sun", "star", -1, 0f, 0f, 0f, true, 11f, 0f, 0f, false);
        AddBody("mercury", "Mercury", "planet", 0, 40f, 0.5f, 36.46f, true, 1.05f, 7f, 3.4f, false);
        AddBody("venus", "Venus", "planet", 0, 56f, 2.1f, 60.4f, true, 1.5f, 8f, 4f, false);
        AddBody("earth", "Earth", "planet", 0, 76f, 0.15f, 95.49f, true, 2.3f, 11f, 5.4f, true);
        AddBody("mars", "Mars", "planet", 0, 104f, 2.7f, 152.86f, true, 1.6f, 9f, 4.4f, true);
        AddBody("jupiter", "Jupiter", "planet", 0, 150f, 4.1f, 264.78f, true, 5.2f, 16f, 9f, false);
        AddBody("saturn", "Saturn", "planet", 0, 200f, 5.3f, 407.66f, true, 4.2f, 14f, 9.4f, false);
        AddBody("uranus", "Uranus", "planet", 0, 250f, 1f, 569.72f, true, 2.6f, 10f, 5.6f, false);
        AddBody("neptune", "Neptune", "planet", 0, 295f, 3.4f, 730.27f, true, 2.5f, 10f, 5.4f, false);
        AddBody("pluto", "Pluto", "dwarf", 0, 335f, 5.9f, 883.73f, true, 0.9f, 6f, 2.8f, false);
        AddBody("moon", "Moon", "moon", Find("earth"), 18f, 0.8f, 0.07f, false, 0.72f, 6f, 2.6f, false);
        AddBody("callisto", "Callisto", "moon", Find("jupiter"), 24f, 2.2f, 0.045f, false, 0.8f, 6f, 2.7f, true);
        AddBody("station", "Station", "station", Find("earth"), 5.4f, 2.4f, 0.55f, false, 0.5f, 0f, 0f, false);
        foreach (string id in new[] { "mercury", "venus", "earth", "station", "moon", "mars", "jupiter", "callisto", "saturn", "uranus", "neptune", "pluto" }) targets.Add(Find(id));

        for (int i = 0; i < bodies.Count; i++) bodies[i].t = Visual(bodies[i]);
        Belt();
        Stars();
        for (int i = 1; i < bodies.Count; i++)
        {
            Body b = bodies[i];
            if (b.kind == "station") continue;
            var lr = Line(b.kind == "moon" ? new Color(0.5f, 0.6f, 0.8f, 0.25f) : new Color(0.55f, 0.65f, 0.9f, 0.3f), b.kind == "moon" ? 0.6f : 2.5f, 128, true);
            orbitLines.Add(lr);
        }
        path = Line(new Color(0.6f, 0.9f, 1f, 0.7f), 1.2f, 90, false);
        targetRing = Line(new Color(1f, 0.85f, 0.2f, 0.9f), 1.5f, 48, true);
        targetArrow = Line(new Color(1f, 0.85f, 0.2f, 0.95f), 1.8f, 2, false);
        progradeLine = Line(new Color(0.3f, 1f, 0.4f, 0.9f), 1.4f, 2, false);
        ship = Starship.Build(this);
        foreach (var r in root.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
        Tick(0f);
    }

    LineRenderer Line(Color c, float w, int n, bool loop)
    {
        var go = new GameObject("Line");
        go.transform.SetParent(root, false);
        var lr = go.AddComponent<LineRenderer>();
        var m = new Material(Mats.Fx); m.color = c;
        lr.sharedMaterial = m;
        lr.startColor = lr.endColor = c;
        lr.widthMultiplier = w;
        lr.positionCount = n;
        lr.loop = loop;
        lr.useWorldSpace = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }

    static Texture2D Bands(Color a, Color b, int seed, float contrast)
    {
        var t = new Texture2D(8, 128, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
        var r = new System.Random(seed);
        for (int y = 0; y < 128; y++)
        {
            float v = Mathf.PerlinNoise(y * 0.09f, seed) * contrast + (float)r.NextDouble() * 0.08f;
            Color c = Color.Lerp(a, b, v);
            for (int x = 0; x < 8; x++) t.SetPixel(x, y, c);
        }
        t.Apply(true);
        return t;
    }

    Transform Visual(Body b)
    {
        var t = new GameObject(b.name).transform;
        t.SetParent(root, false);
        float d = b.r * 2f;
        switch (b.id)
        {
            case "sun":
                Mats.Prim(PrimitiveType.Sphere, t, Vector3.zero, Vector3.one * d, Mats.Unlit(new Color(1f, 0.85f, 0.35f)));
                var glow = new Material(Mats.Fx); glow.color = new Color(1f, 0.7f, 0.25f, 0.18f);
                Mats.Prim(PrimitiveType.Sphere, t, Vector3.zero, Vector3.one * d * 1.5f, glow);
                break;
            case "earth":
                Mats.Prim(PrimitiveType.Sphere, t, Vector3.zero, Vector3.one * d, Mats.Lit(new Color(0.15f, 0.4f, 0.85f)));
                var rr = new System.Random(3);
                for (int i = 0; i < 9; i++)
                {
                    Vector3 dir = Random.onUnitSphere;
                    dir = new Vector3((float)rr.NextDouble() - 0.5f, ((float)rr.NextDouble() - 0.5f) * 1.2f, (float)rr.NextDouble() - 0.5f).normalized;
                    var land = Mats.Prim(PrimitiveType.Sphere, t, dir * b.r * 0.86f, new Vector3(b.r * 0.9f, b.r * 0.5f, b.r * 0.7f), Mats.Lit(new Color(0.25f, 0.55f, 0.25f)));
                    land.transform.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f);
                }
                var cl = new Material(Mats.Glass); cl.color = new Color(1f, 1f, 1f, 0.35f);
                Mats.Prim(PrimitiveType.Sphere, t, Vector3.zero, Vector3.one * d * 1.04f, cl);
                break;
            case "jupiter":
            case "saturn":
                {
                    bool j = b.id == "jupiter";
                    var m = new Material(Mats.Lit(Color.white)) { mainTexture = Bands(j ? new Color(0.85f, 0.7f, 0.5f) : new Color(0.9f, 0.82f, 0.6f), j ? new Color(0.6f, 0.38f, 0.25f) : new Color(0.75f, 0.65f, 0.45f), j ? 7 : 9, 1.2f) };
                    Mats.Prim(PrimitiveType.Sphere, t, Vector3.zero, Vector3.one * d, m);
                    if (!j)
                    {
                        var rm = new Material(Mats.Glass); rm.color = new Color(0.85f, 0.78f, 0.6f, 0.55f);
                        // ring as a band of flat slabs around the planet
                        for (int k = 0; k < 36; k++)
                        {
                            float a = k * 10f;
                            var s = Mats.Prim(PrimitiveType.Cube, t, Quaternion.Euler(0f, a, 0f) * Vector3.forward * b.r * 1.8f, new Vector3(b.r * 0.62f, 0.05f, b.r * 0.7f), rm);
                            s.transform.localRotation = Quaternion.Euler(0f, a, 0f);
                        }
                        t.localRotation = Quaternion.Euler(18f, 0f, 8f);
                    }
                    if (j) Mats.Prim(PrimitiveType.Sphere, t, new Vector3(b.r * 0.55f, -b.r * 0.3f, -b.r * 0.72f), new Vector3(b.r * 0.4f, b.r * 0.22f, b.r * 0.2f), Mats.Lit(new Color(0.8f, 0.35f, 0.25f)));
                }
                break;
            case "station":
                Mats.Prim(PrimitiveType.Cube, t, Vector3.zero, new Vector3(1f, 1f, 9f), Mats.Steel(new Color(0.8f, 0.8f, 0.82f)));
                for (int s = -1; s <= 1; s += 2)
                {
                    Mats.Prim(PrimitiveType.Cube, t, new Vector3(5f * s, 0f, 0f), new Vector3(8f, 0.1f, 3.2f), Mats.Shiny(new Color(0.15f, 0.25f, 0.6f)));
                    Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0f, 3f * s), new Vector3(2f, 1.4f, 2f), new Vector3(90f, 0f, 0f), Mats.Lit(Color.white));
                }
                break;
            default:
                {
                    Color c = b.id == "mercury" ? new Color(0.6f, 0.58f, 0.55f) : b.id == "venus" ? new Color(0.95f, 0.85f, 0.6f) : b.id == "mars" ? new Color(0.8f, 0.38f, 0.2f)
                        : b.id == "uranus" ? new Color(0.6f, 0.9f, 0.92f) : b.id == "neptune" ? new Color(0.25f, 0.4f, 0.95f) : b.id == "pluto" ? new Color(0.85f, 0.75f, 0.65f)
                        : b.id == "moon" ? new Color(0.72f, 0.72f, 0.72f) : b.id == "callisto" ? new Color(0.5f, 0.45f, 0.4f) : Color.gray;
                    Mats.Prim(PrimitiveType.Sphere, t, Vector3.zero, Vector3.one * d, Mats.Lit(c));
                    if (b.id == "mars" || b.id == "moon" || b.id == "callisto" || b.id == "mercury")
                        for (int k = 0; k < 6; k++)
                        {
                            Vector3 dir = Quaternion.Euler(k * 50f, k * 77f, 0f) * Vector3.forward;
                            Mats.Prim(PrimitiveType.Sphere, t, dir * b.r * 0.92f, Vector3.one * b.r * 0.4f, Mats.Lit(Color.Lerp(c, Color.black, 0.25f)));
                        }
                    if (b.id == "mars") Mats.Prim(PrimitiveType.Sphere, t, Vector3.up * b.r * 0.9f, new Vector3(b.r * 0.8f, b.r * 0.3f, b.r * 0.8f), Mats.Lit(Color.white));
                }
                break;
        }
        // name label
        var lab = new GameObject("Label");
        lab.transform.SetParent(t, false);
        lab.transform.localPosition = Vector3.up * (b.r + 6f + b.r * 0.2f);
        var tm = lab.AddComponent<TextMesh>();
        tm.text = b.name; tm.font = UIK.Font; tm.fontSize = 64; tm.anchor = TextAnchor.MiddleCenter;
        tm.characterSize = Mathf.Clamp(b.r * 0.06f, 0.5f, 3f);
        tm.color = b.id == "sun" ? new Color(1f, 0.9f, 0.5f) : new Color(0.85f, 0.92f, 1f);
        lab.GetComponent<MeshRenderer>().sharedMaterial = UIK.Font != null ? UIK.Font.material : null;
        lab.AddComponent<Billboard>();
        return t;
    }

    void Belt()
    {
        belt = new GameObject("Asteroid belt").transform;
        belt.SetParent(root, false);
        belt.position = O;
        var r = new System.Random(12);
        var m = Mats.Lit(new Color(0.45f, 0.42f, 0.4f));
        for (int i = 0; i < 160; i++)
        {
            float a = (float)r.NextDouble() * 6.283f, d = 1250f + (float)r.NextDouble() * 120f, s = 1.5f + (float)r.NextDouble() * 5f;
            var g = Mats.Prim(PrimitiveType.Sphere, belt, new Vector3(Mathf.Cos(a) * d, ((float)r.NextDouble() - 0.5f) * 14f, Mathf.Sin(a) * d), new Vector3(s, s * 0.7f, s * 0.85f), m);
        }
        MeshMerge.Merge(belt, false);
    }

    void Stars()
    {
        stars = new GameObject("Stars").transform;
        stars.SetParent(root, false);
        var r = new System.Random(77);
        var v = new List<Vector3>(); var tri = new List<int>();
        for (int i = 0; i < 900; i++)
        {
            Vector3 d = new Vector3((float)r.NextDouble() - 0.5f, (float)r.NextDouble() - 0.5f, (float)r.NextDouble() - 0.5f).normalized * 9000f;
            float s = 6f + (float)r.NextDouble() * 18f;
            Quaternion q = Quaternion.LookRotation(-d);
            int b = v.Count;
            v.Add(d + q * new Vector3(-s, -s, 0f)); v.Add(d + q * new Vector3(s, -s, 0f)); v.Add(d + q * new Vector3(0f, s, 0f));
            tri.Add(b); tri.Add(b + 2); tri.Add(b + 1);
        }
        var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(v); mesh.SetTriangles(tri, 0); mesh.RecalculateBounds();
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 20000f);
        stars.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = stars.gameObject.AddComponent<MeshRenderer>();
        mr.sharedMaterial = Mats.Unlit(new Color(0.95f, 0.95f, 1f));
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // per-camera hooks (called from Worlds' onPreCull for space cameras)
    public void PreCull(Camera c)
    {
        if (stars != null) stars.position = c.transform.position;
        Light sun = RenderSettings.sun;
        if (sun != null && ship != null) sun.transform.rotation = Quaternion.LookRotation((c.transform.position - O).normalized);
    }

    // ---------------- update ----------------
    void Update()
    {
        if (!built) return;
        bool anyone = false;
        if (Game.I != null) foreach (Frog f in Game.I.frogs) if (f != null && f.world == WorldId.Space) anyone = true;
        if (root.gameObject.activeSelf != anyone) root.gameObject.SetActive(anyone);
        if (!anyone) return;
        Tick(Time.deltaTime);
    }

    void Tick(float dt)
    {
        float warp = ship != null ? ship.SimRate : 1f;
        simT += dt * warp;
        for (int i = 0; i < bodies.Count; i++)
        {
            Body b = bodies[i];
            b.pos = PosAt(i, simT);
            b.vel = VelAt(i, simT);
            b.t.position = b.pos;
            if (b.kind != "station") b.t.Rotate(0f, dt * warp * 6f, 0f, Space.Self);
            else b.t.rotation = Quaternion.LookRotation(b.vel.sqrMagnitude > 0.001f ? b.vel : Vector3.forward);
        }
        if (belt != null) belt.rotation = Quaternion.Euler(0f, -simT / 260f * Mathf.Rad2Deg, 0f);
        // orbit circles
        int li = 0;
        for (int i = 1; i < bodies.Count; i++)
        {
            Body b = bodies[i];
            if (b.kind == "station") continue;
            LineRenderer lr = orbitLines[li++];
            if (Time.frameCount % 4 != 0 && b.kind != "moon") continue;
            Vector3 c = b.parent >= 0 ? bodies[b.parent].pos : O;
            for (int k = 0; k < lr.positionCount; k++) { float a = k * Mathf.PI * 2f / lr.positionCount; lr.SetPosition(k, c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * b.a); }
        }
        if (ship != null) ship.Sim(dt);
        DrawGuides();
    }

    void DrawGuides()
    {
        if (ship == null) return;
        // predicted path
        var pts = ship.Predict(path.positionCount);
        for (int k = 0; k < pts.Length; k++) path.SetPosition(k, pts[k]);
        // target ring + arrow
        int ti = ship.target;
        if (ti >= 0)
        {
            Body b = bodies[ti];
            float rr = Mathf.Max(b.r * 1.6f, b.cap > 0 ? b.cap : b.r * 2f, 8f);
            targetRing.enabled = true;
            for (int k = 0; k < targetRing.positionCount; k++) { float a = k * Mathf.PI * 2f / targetRing.positionCount; targetRing.SetPosition(k, b.pos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rr); }
            Vector3 sp = ship.transform.position;
            Vector3 dir = (b.pos - sp).normalized;
            targetArrow.SetPosition(0, sp + dir * 10f);
            targetArrow.SetPosition(1, sp + dir * 26f);
        }
        else targetRing.enabled = false;
        Vector3 v = ship.Vel;
        Vector3 rel = v - ship.RefVel;
        Vector3 pv = rel.sqrMagnitude > 0.01f ? rel.normalized : ship.transform.forward;
        progradeLine.SetPosition(0, ship.transform.position + pv * 9f);
        progradeLine.SetPosition(1, ship.transform.position + pv * 22f);
    }
}

// The Starship in space. Kinematic: the flight model below moves it (planar, in the plane of the orbits).
public class Starship : Vehicle
{
    SpaceWorld W;
    public enum Mode { Orbit, Free, Transfer }
    public Mode mode = Mode.Orbit;
    public int target = -1;
    int orbitBody = -1;
    float orbitR, orbitAng, orbitW = 0.42f, burnT;
    Vector3 pos, vel;
    float heading;
    int warpIdx;
    static readonly float[] Warps = { 1f, 4f, 16f, 64f };
    // transfer
    Vector3 tS, tC1, tC2, tE;
    float tDur, tEl, tWarp = 8f, tEndR, tEndAng;
    int tBody;
    Transform flame;

    public float SimRate { get { return mode == Mode.Transfer ? tWarp : Warps[warpIdx]; } }
    public Vector3 Vel { get { return vel; } }
    public Vector3 RefVel { get { int n = Nearest(); return n >= 0 ? W.bodies[n].vel : Vector3.zero; } }

    public override bool CanExit(Frog f) { return false; }

    public override string HelpLine
    {
        get
        {
            string t = target >= 0 ? W.bodies[target].name : "-";
            float dist = target >= 0 ? (W.bodies[target].pos - pos).magnitude : 0f;
            string m = mode == Mode.Orbit ? "ORBIT " + W.bodies[orbitBody].name : mode == Mode.Transfer ? "AUTO-TRANSFER  ETA " + Mathf.CeilToInt(tDur - tEl) + " s" : "FREE FLIGHT";
            string land = mode == Mode.Orbit && W.bodies[orbitBody].landable ? "  |  Y / F LAND" : "";
            return m + "  |  target " + t + " " + Mathf.RoundToInt(dist) + " m  |  warp x" + Mathf.RoundToInt(SimRate) +
                   "\n<size=17>D-pad < > target  X / G auto  LB RB / Z C warp  L-stick turn + thrust  RT boost  LT brake" + land + "</size>";
        }
    }

    public static Starship Build(SpaceWorld w)
    {
        var go = new GameObject("Starship (space)");
        var v = go.AddComponent<Starship>();
        v.W = w;
        v.Title = "Starship";
        v.EnterVerb = "board the Starship";
        v.flyer = true;
        v.engineKind = 2;
        v.showDriver = false;
        v.SetupBodyPublic(5000f, new Vector3(0f, 0f, 0f), new Vector3(3.6f, 3.6f, 16f), Vector3.zero);
        v.rb.isKinematic = true; v.rb.useGravity = false;
        v.rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        v.body.isTrigger = true;
        v.camDistance = 34f; v.camHeight = 6f;
        Transform t = go.transform;
        Material steel = Mats.Steel(new Color(0.78f, 0.79f, 0.8f)), tile = Mats.Lit(new Color(0.12f, 0.12f, 0.13f));
        Mats.Prim(PrimitiveType.Cylinder, t, Vector3.zero, new Vector3(3.6f, 6f, 3.6f), new Vector3(90f, 0f, 0f), steel);
        Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, -0.25f, 0f), new Vector3(3.3f, 5.9f, 3.3f), new Vector3(90f, 0f, 0f), tile);
        Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 0f, 6.3f), new Vector3(3.6f, 3.6f, 5.4f), steel);
        for (int s = -1; s <= 1; s += 2)
        {
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(2.1f * s, 0f, 5.2f), new Vector3(1.2f, 0.15f, 2.2f), tile);
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(2.3f * s, 0f, -4.6f), new Vector3(1.6f, 0.15f, 2.8f), tile);
        }
        for (int k = 0; k < 3; k++) Mats.Prim(PrimitiveType.Cylinder, t, Quaternion.Euler(0f, 0f, k * 120f) * new Vector3(0f, 0.9f, 0f) + new Vector3(0f, 0f, -6.3f), new Vector3(1f, 0.5f, 1f), new Vector3(90f, 0f, 0f), Mats.Steel(new Color(0.35f, 0.33f, 0.3f)));
        v.flame = Mats.Node(t, "Flame", new Vector3(0f, 0f, -8.5f));
        var fm = new Material(Mats.Fx); fm.color = new Color(1f, 0.6f, 0.2f, 0.75f);
        Mats.Prim(PrimitiveType.Sphere, v.flame, Vector3.zero, new Vector3(2.2f, 2.2f, 6f), fm);
        v.seat = Mats.Node(t, "Seat", new Vector3(0f, 0.5f, 4f));
        v.seatScale = 0.5f;
        Mats.SetLayer(go, VehicleLayer);
        Mats.NoShadows(go);
        return v;
    }

    protected override bool OutOfWorld(Vector3 p) { return false; }

    int Nearest()
    {
        int best = -1; float bd = 1e9f;
        for (int i = 1; i < W.bodies.Count; i++)
        {
            var b = W.bodies[i];
            if (b.kind == "station") continue;
            float d = (b.pos - pos).magnitude - b.soft * 2.5f;
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    public void SetTarget(int i) { target = i; }

    public void EnterOrbit(int body, float r, float ang)
    {
        mode = Mode.Orbit; orbitBody = body; orbitR = Mathf.Max(r, W.bodies[body].r + 8f); orbitAng = ang;
        orbitW = W.bodies[body].id == "earth" ? 0.55f : 0.42f;
        warpIdx = 0;
        if (target < 0 || target == body) target = W.Find(W.bodies[body].id == "earth" ? "moon" : "earth");
        Place();
    }

    void Place()
    {
        Vector3 c = W.bodies[orbitBody].pos;
        pos = c + new Vector3(Mathf.Cos(orbitAng), 0f, Mathf.Sin(orbitAng)) * orbitR;
        Vector3 tangent = new Vector3(-Mathf.Sin(orbitAng), 0f, Mathf.Cos(orbitAng));
        vel = W.bodies[orbitBody].vel + tangent * orbitW * orbitR;
        heading = Mathf.Atan2(tangent.x, tangent.z) * Mathf.Rad2Deg;
        Apply();
    }

    void Apply()
    {
        transform.position = pos;
        transform.rotation = Quaternion.Euler(0f, heading, 0f);
    }

    // edge-triggered controls arrive here once per frame from the driver's Frog.Update
    public override void Drive(PIn i, float camYaw, float dt)
    {
        base.Drive(i, camYaw, dt);
        if (i.target) Cycle(1);
        if (i.targetPrev) Cycle(-1);
        if (i.auto) StartTransfer();
        if (i.land) { if (mode == Mode.Orbit) W.Land(orbitBody); else if (driver != null) driver.Toast("Get into orbit first (auto-transfer or slow down near a planet), then land", 2.5f); }
        if (mode == Mode.Free)
        {
            if (i.warpUp) warpIdx = Mathf.Min(3, warpIdx + 1);
            if (i.warpDown) warpIdx = Mathf.Max(0, warpIdx - 1);
        }
    }

    void Cycle(int d)
    {
        int k = W.targets.IndexOf(target);
        k = ((k + d) % W.targets.Count + W.targets.Count) % W.targets.Count;
        if (W.targets[k] == orbitBody && mode == Mode.Orbit) k = ((k + d) % W.targets.Count + W.targets.Count) % W.targets.Count;
        target = W.targets[k];
        Sfx.Play(Sfx.Click, 0.5f);
    }

    void StartTransfer()
    {
        if (mode == Mode.Transfer) { mode = Mode.Free; if (driver != null) driver.Toast("Auto-transfer cancelled", 1.5f); return; }
        if (target < 0) return;
        var b = W.bodies[target];
        float dist = (b.pos - pos).magnitude;
        tDur = Mathf.Clamp(4f + dist / 160f, 4f, 24f);
        tWarp = Mathf.Clamp(dist / 120f, 2f, 16f);
        tEl = 0f;
        float tArr = W.simT + tWarp * tDur;
        int body = target;
        tEndR = b.cap > 0f ? b.cap : b.r + 10f;
        if (b.kind == "station")
        {
            body = b.parent;
            tEndR = b.a;
            Vector3 sp = W.PosAt(target, tArr) - W.PosAt(body, tArr);
            tEndAng = Mathf.Atan2(sp.z, sp.x) - 0.15f;   // dock just behind the Station, same orbit
        }
        else
        {
            // arrive on the sunlit side
            Vector3 bp = W.PosAt(body, tArr);
            Vector3 toSun = (SpaceWorld.O - bp); toSun.y = 0f;
            tEndAng = toSun.sqrMagnitude > 1f ? Mathf.Atan2(toSun.z, toSun.x) + 0.5f : 0f;
        }
        tBody = body;
        Vector3 bpos = W.PosAt(body, tArr);
        tE = bpos + new Vector3(Mathf.Cos(tEndAng), 0f, Mathf.Sin(tEndAng)) * tEndR;
        tS = pos;
        Vector3 v0 = vel - RefVelAt(pos);
        Vector3 dirS = v0.sqrMagnitude > 1f ? v0.normalized : transform.forward;
        float span = (tE - tS).magnitude;
        Vector3 side = Vector3.Cross(Vector3.up, (tE - tS).normalized);
        tC1 = tS + dirS * span * 0.35f;
        tC2 = tE + (tS - tE).normalized * span * 0.3f + side * span * 0.18f;
        mode = Mode.Transfer;
        if (driver != null) driver.Toast("Auto-transfer to " + b.name + " (" + Mathf.RoundToInt(tDur) + " s, warp x" + Mathf.RoundToInt(tWarp) + ")", 2.5f);
        Sfx.Play(Sfx.Missile, 0.6f, 0.6f);
    }

    Vector3 RefVelAt(Vector3 p) { int n = Nearest(); return n >= 0 ? W.bodies[n].vel : Vector3.zero; }

    static Vector3 Bez(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
    {
        float u = 1f - t;
        return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
    }

    const float MuSun = 48100f;

    Vector3 Accel(Vector3 p, bool includeBody)
    {
        Vector3 d = SpaceWorld.O - p; d.y = 0f;
        float r = Mathf.Max(d.magnitude, 30f);
        Vector3 a = d / r * (MuSun / (r * r));
        if (includeBody)
        {
            for (int i = 1; i < W.bodies.Count; i++)
            {
                var b = W.bodies[i];
                if (b.kind == "station" || b.cap <= 0f) continue;
                Vector3 db = b.pos - p; db.y = 0f;
                float rb = db.magnitude;
                if (rb > b.soft * 3f) continue;
                float vc = 0.42f * b.cap;
                float mu = vc * vc * b.cap;
                rb = Mathf.Max(rb, b.r + 2f);
                a += db.normalized * (mu / (rb * rb));
            }
        }
        return a;
    }

    public void Sim(float dt)
    {
        if (W == null) return;
        bool on = driver != null;
        float thrustIn = on ? Mathf.Clamp01(inp.move.y) + (on ? inp.gas : 0f) : 0f;
        bool flameOn = false;
        switch (mode)
        {
            case Mode.Orbit:
                {
                    if (on)
                    {
                        orbitW = Mathf.Clamp(orbitW + inp.move.x * 0.25f * dt, 0.15f, 0.9f);
                        if (inp.move.y > 0.5f || inp.gas > 0.5f) burnT += dt; else burnT = 0f;
                        flameOn = burnT > 0f;
                    }
                    orbitAng += orbitW * dt * Warps[warpIdx];
                    Place();
                    if (burnT > 0.6f)
                    {
                        burnT = 0f;
                        Vector3 tangent = new Vector3(-Mathf.Sin(orbitAng), 0f, Mathf.Cos(orbitAng));
                        vel = W.bodies[orbitBody].vel + tangent * (orbitW * orbitR + 25f);
                        pos += tangent * 2f;
                        mode = Mode.Free;
                        if (on) driver.Toast("Burned out of " + W.bodies[orbitBody].name + " orbit", 2f);
                    }
                    break;
                }
            case Mode.Transfer:
                {
                    tEl += dt;
                    float u = Mathf.Clamp01(tEl / tDur);
                    float s = u * u * (3f - 2f * u);
                    Vector3 np = Bez(tS, tC1, tC2, tE, s);
                    Vector3 d = np - pos;
                    if (d.sqrMagnitude > 0.0001f) heading = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                    vel = dt > 0f ? d / Mathf.Max(dt * tWarp, 1e-4f) : vel;
                    pos = np;
                    flameOn = u < 0.15f || u > 0.85f;
                    if (u >= 1f)
                    {
                        EnterOrbit(tBody, tEndR, tEndAng + W.bodies[tBody].w * 0f);
                        // re-align to the body's actual position
                        Vector3 rel = pos - W.bodies[tBody].pos;
                        if (driver != null) { driver.Toast("Arrived: " + W.bodies[target].name + " orbit" + (W.bodies[tBody].landable ? " - Y / F to land" : ""), 3f); Sfx.Play(Sfx.Pickup, 0.7f); }
                    }
                    break;
                }
            default:
                {
                    if (on)
                    {
                        heading += inp.move.x * 80f * dt;
                        if (thrustIn > 0.05f || inp.brake > 0.05f) warpIdx = 0;
                    }
                    float sim = dt * Warps[warpIdx];
                    int steps = Mathf.Clamp(Mathf.CeilToInt(sim / 0.05f), 1, 64);
                    float h = sim / steps;
                    Vector3 fwd = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
                    for (int k = 0; k < steps; k++)
                    {
                        Vector3 a = Accel(pos, true);
                        if (on && thrustIn > 0.02f) a += fwd * (8f * Mathf.Clamp01(inp.move.y) + 22f * inp.gas);
                        vel += a * h;
                        pos += vel * h;
                    }
                    flameOn = on && thrustIn > 0.05f;
                    int n = Nearest();
                    if (n >= 0)
                    {
                        var b = W.bodies[n];
                        Vector3 rel = vel - b.vel;
                        Vector3 d = pos - b.pos; d.y = 0f;
                        if (on && inp.brake > 0.05f) vel = Vector3.MoveTowards(vel, b.vel, 20f * inp.brake * dt);
                        // approach assist: damp + pull inside a body's zone, capture when slow and close
                        if (b.cap > 0f && d.magnitude < b.soft * 2f)
                        {
                            vel = Vector3.MoveTowards(vel, b.vel, rel.magnitude * 0.3f * dt);
                            if (d.magnitude > b.cap * 1.2f) vel -= d.normalized * 6f * dt;
                            if (d.magnitude < b.cap * 1.8f && rel.magnitude < 32f)
                            {
                                EnterOrbit(n, b.cap, Mathf.Atan2(d.z, d.x));
                                if (on) { driver.Toast("Captured into " + b.name + " orbit" + (b.landable ? " - Y / F to land" : ""), 2.5f); Sfx.Play(Sfx.Pickup, 0.6f); }
                            }
                        }
                    }
                    // the Sun's heat shield
                    Vector3 ds = pos - SpaceWorld.O; ds.y = 0f;
                    if (ds.magnitude < W.bodies[0].r * 2.2f)
                    {
                        pos = SpaceWorld.O + ds.normalized * W.bodies[0].r * 2.2f;
                        vel = Vector3.Reflect(vel, ds.normalized) * 0.6f;
                        if (on) driver.Toast("Too hot! The heat shield bounced you off the Sun", 2f);
                        Sfx.Play(Sfx.Boom, 0.5f, 1.5f);
                    }
                    pos.y = SpaceWorld.O.y;
                    if (mode == Mode.Free) Apply();
                    break;
                }
        }
        kinVel = vel - RefVel;
        if (flame != null) { flame.gameObject.SetActive(flameOn); if (flameOn) flame.localScale = new Vector3(1f, 1f, 0.8f + Random.value * 0.5f); }
    }

    // dotted prediction: orbit circle, transfer arc, or a short free-flight integration
    public Vector3[] Predict(int n)
    {
        var pts = new Vector3[n];
        if (mode == Mode.Orbit)
        {
            Vector3 c = W.bodies[orbitBody].pos;
            for (int k = 0; k < n; k++) { float a = orbitAng + k * Mathf.PI * 2f / (n - 1); pts[k] = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * orbitR; }
        }
        else if (mode == Mode.Transfer)
        {
            float u0 = Mathf.Clamp01(tEl / tDur);
            for (int k = 0; k < n; k++) { float u = Mathf.Lerp(u0, 1f, k / (float)(n - 1)); pts[k] = Bez(tS, tC1, tC2, tE, u * u * (3f - 2f * u)); }
        }
        else
        {
            Vector3 p = pos, v = vel;
            for (int k = 0; k < n; k++)
            {
                pts[k] = p;
                for (int s = 0; s < 4; s++) { v += Accel(p, false) * 0.5f; p += v * 0.5f; }
            }
        }
        return pts;
    }
}
