using System.Collections.Generic;
using UnityEngine;

// ffu49 THE ROADSTER (Bill, 2026-10-15, after the next-gen Roadster reveal): a low white SR-71-style two-seater with a
// steer-by-wire yoke, a retractable rear wing, suction fans round the rear diffuser and 10 perimeter cold-gas thrusters fed
// by a 600 L compressed-air tank. Our own procedural model (lofted body + canopy, no logos, no third-party meshes).
//  * LAUNCH: full throttle on flat ground = 0-60 mph (26.8 m/s) in ~0.8 s, top speed ~210 km/h.
//  * SUCTION FANS (on by default, F / pad Y / touch FANS): while the wheels touch any surface the fans pull the car onto it
//    (2.45 g along the surface normal; the springs stiffen with the load so the ride height stays put). On steep / upside-down
//    surfaces (the loop, the C-ramp ceiling) it switches to CRAWL: throttle nudges a crawl speed up / down, no throttle holds
//    it (stopped = stuck to the wall / ceiling), so you can go round the loop as slowly as you like.
//  * THRUSTERS (hold Space / pad X / touch JETS): thrust along the car's up axis (2.45 g): hop onto a roof, over a truck,
//    off the ceiling. In the air the stick / WASD pitches + rolls the car with thruster puffs; with no input it levels
//    itself; near the ground it eases itself down (landing puffs) while there is air.
//  * BARREL ROLL (R / pad RB / touch ROLL in the air): one-button roll (360 deg; 180 deg when it starts upside down, so
//    dropping off the ceiling + ROLL = land on its wheels); from a surface it hops off it first.
//  * AIR: 600 L. Thrust 120 L/s, attitude puffs a little, roll 60 L. Refills on the ground from the onboard compressor
//    (~30 s) and fast in the garage (~4 s parked). HUD gauge + litres.
// CANON (CANON.md, Bill 2026-10-15): the family car (owner TBD) - parked in the garage, anyone asks before borrowing and
// it must not be in use (ownerName "the family", the usual ask-to-borrow + in-use check).
public class Roadster49 : Vehicle
{
    public static Roadster49 I;
    public const float TankL = 600f, SucG = 24f, JetA = 24f, JetLps = 120f, RollL = 60f;
    public float air = TankL;
    public bool fansOn = true;
    [System.NonSerialized] public float sucK, agl = 99f, airT, crawlV;
    [System.NonSerialized] public bool crawl, jetsOn, landing, refilling, rolling;
    [System.NonSerialized] public int grounded;
    [System.NonSerialized] public Vector3 gN = Vector3.up;
    [System.NonSerialized] public float demoCrawl = -1f;   // probe: crawl speed override
    public static int Launches, Rolls, Hops; public static float LastLaunch = -1f;

    class Wh { public Vector3 mount; public Transform vis; public bool steer, ground; public float dist, spin; }
    readonly Wh[] wh = new Wh[4];
    const float Rest = 0.2f, WR = 0.34f;
    float spring0, damper0, steerIn, throttle, rollAng, rollGoal, rollDir = 1f, rollT, puffCmdP, puffCmdR, jetVis, landVis, wingK, fanSpin, launchT = -1f;
    bool fanQ, rollQ, rollInv;
    Transform wing, fanL, fanR, yoke;
    readonly List<Vector3> nozP = new List<Vector3>();   // local nozzle positions (10)
    AudioSource hissSrc, fanSrc;
    static AudioClip hissClip, puffClip, fanClip;

    public override string[] TouchSet { get { return new[] { "A", "JETS", grounded > 0 ? (fansOn ? "FANS ON" : "FANS") : "ROLL", "GAS", "BRAKE" }; } }
    public override string HelpLine { get { return "Roadster: RT gas (launch!) | LT brake | X hold = thrusters | Y suction fans | RB barrel roll | A get out"; } }
    public override string HelpFor(InputKind k)
    {
        if (k == InputKind.Gamepad) return "RT  /  LT::gas (full = launch!) / brake + reverse|L-stick::steer (in the air: pitch + roll)|X::hold = thrusters (hop / fly)|Y::suction fans on / off (drive on walls + ceilings)|RB::barrel roll|On a wall / ceiling::RT / LT = crawl faster / slower, let go = hold|A::get out";
        if (k == InputKind.Touch) return "GAS  /  BRAKE::gas (launch!) / brake + reverse|Stick::steer (in the air: pitch + roll)|JETS::hold = thrusters (hop / fly)|FANS::suction fans on / off|ROLL::barrel roll (in the air)|On a wall / ceiling::GAS / BRAKE = crawl faster / slower|A::get out";
        return "W  /  S::gas (hold W = launch!) / brake + reverse|A  /  D::steer (in the air: W S pitch, A D roll)|Space::hold = thrusters (hop / fly)|F::suction fans on / off (drive on walls + ceilings)|R::barrel roll|On a wall / ceiling::W / S = crawl faster / slower, let go = hold|E::get out";
    }
    // ffu49: the live context line (Hints46): in the air / crawling / out of air
    public string ContextHint(InputKind k, System.Func<string, string> cap)
    {
        bool pad = k == InputKind.Gamepad, touch = k == InputKind.Touch;
        string jet = pad ? "X" : touch ? "JETS" : "Space", roll = pad ? "RB" : touch ? "ROLL" : "R", att = pad ? "L-stick" : touch ? "Stick" : "W A S D";
        string gas = pad ? "RT / LT" : touch ? "GAS / BRAKE" : "W / S";
        if (air < 5f && grounded == 0) return "<color=#ff8a6a>Out of air!</color>  land it - the tank refills on the ground (fast in the garage)";
        if (grounded == 0 && airT > 0.35f) return cap(att) + " pitch + roll   " + cap(jet) + " thrusters   " + cap(roll) + " barrel roll   <color=#9fe0ff>it eases itself down while there's air</color>";
        if (crawl) return "<color=#9fe0ff>FANS holding</color>   " + cap(gas) + " crawl faster / slower (let go = hold)   " + cap(jet) + " jump off   " + cap(roll) + " roll off";
        return null;
    }
    public override string HudStatus
    {
        get
        {
            int n = Mathf.RoundToInt(air / TankL * 10f);
            string bar = "<color=#8fe3ff>" + new string('|', n) + "</color><color=#4a5560>" + new string('|', 10 - n) + "</color>";
            string s = "ROADSTER " + Mathf.RoundToInt(Velocity.magnitude * 3.6f) + " km/h  AIR " + bar + " " + Mathf.RoundToInt(air) + " L";
            s += fansOn ? "  FANS " + (sucK > 0.5f ? "<color=#9fe0ff>HOLDING</color>" : "on") : "   fans off";
            if (crawl) s += "   CRAWL " + Mathf.RoundToInt(Mathf.Abs(crawlV) * 3.6f) + " km/h";
            if (refilling) s += "   <color=#9fe0ff>refilling...</color>";
            if (grounded == 0 && agl < 90f) s += "   alt " + Mathf.RoundToInt(agl) + " m";
            return s;
        }
    }
    public override bool HoldsOn { get { return (grounded > 0 && sucK > 0.3f) || (grounded == 0 && air > 1f) || rolling; } }

    // ---------------- spawn ----------------
    public static Roadster49 Spawn(Vector3 pos, float yaw)
    {
        var go = new GameObject("Roadster");
        go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        var v = go.AddComponent<Roadster49>();
        v.Title = "Roadster"; v.EnterVerb = "drive the Roadster";
        v.ownerName = "the family"; v.ownerChar = -1;   // CANON: the family car - ask first, not if in use
        v.engineKind = 12; v.enginePitch = 1.3f;
        bool p52 = HeroCars52.Pack("roadster52") != null;   // ffu52: photo-matched model + real ~1700 kg
        v.SetupBodyPublic(p52 ? 1700f : 1350f, new Vector3(0f, 0.55f, 0f), new Vector3(1.96f, 0.62f, 4.35f), new Vector3(0f, 0.3f, 0f));
        v.ExtraBoxPublic(new Vector3(0f, 0.95f, -0.15f), new Vector3(1.1f, 0.3f, 1.9f));   // canopy
        v.rb.angularDrag = 0.6f;
        v.camDistance = 8.5f; v.camHeight = 2.0f;
        v.Build();
        v.seat = p52 ? Mats.Node(v.sprung52, "Seat", new Vector3(-0.30f, 0.45f, -0.30f)) : Mats.Node(go.transform, "Seat", new Vector3(-0.28f, 0.74f, -0.2f));
        v.seatScale = 0.3f;
        float m = v.rb.mass;
        v.spring0 = m * 9.81f / 4f / (Rest * 0.45f);
        v.damper0 = 2f * Mathf.Sqrt(v.spring0 * m / 4f) * 0.42f;
        if (p52 && v.sprung52 != null) CarFeel52.Attach(v, v.sprung52, new Vector3(0f, 0.45f, 0.05f), 0.11f, 0.16f, 1.6f, 4.36f, 1.98f);
        Mats.SetLayer(go, VehicleLayer);
        I = v;
        Debug.Log("Roadster49: spawned at " + pos.ToString("0.0") + " yaw " + yaw.ToString("0") + " (family car, ask to borrow)");
        return v;
    }

    // ---------------- model ----------------
    static float[] SZ = { 2.24f, 2.12f, 1.95f, 1.7f, 1.35f, 0.95f, 0.5f, 0.0f, -0.5f, -0.95f, -1.35f, -1.7f, -1.95f, -2.12f, -2.2f };
    static float[] SW = { 0.1f, 0.42f, 0.64f, 0.82f, 0.93f, 0.97f, 0.99f, 1.0f, 1.0f, 1.0f, 0.995f, 0.985f, 0.96f, 0.92f, 0.88f };
    static float[] SB = { 0.33f, 0.27f, 0.22f, 0.2f, 0.19f, 0.19f, 0.19f, 0.19f, 0.19f, 0.19f, 0.19f, 0.2f, 0.23f, 0.28f, 0.32f };
    static float[] SC = { 0.35f, 0.37f, 0.4f, 0.43f, 0.47f, 0.5f, 0.52f, 0.54f, 0.55f, 0.56f, 0.57f, 0.58f, 0.6f, 0.62f, 0.64f };
    static float[] ST = { 0.38f, 0.44f, 0.5f, 0.56f, 0.62f, 0.69f, 0.76f, 0.8f, 0.83f, 0.86f, 0.89f, 0.91f, 0.92f, 0.9f, 0.84f };
    static float[] CZ = { 1.05f, 0.75f, 0.42f, 0.1f, -0.2f, -0.5f, -0.8f, -1.1f, -1.38f };
    static float[] CH = { 0.8f, 0.94f, 1.04f, 1.1f, 1.12f, 1.1f, 1.04f, 0.96f, 0.88f };
    static float[] CW = { 0.3f, 0.5f, 0.58f, 0.61f, 0.61f, 0.59f, 0.55f, 0.47f, 0.34f };
    static float Body(float z, float[] a) { return Lerp(SZ, a, z); }
    static float Lerp(float[] zs, float[] a, float z)
    {
        if (z >= zs[0]) return a[0];
        for (int i = 1; i < zs.Length; i++) if (z >= zs[i]) { float t = (z - zs[i - 1]) / (zs[i] - zs[i - 1]); return Mathf.Lerp(a[i - 1], a[i], Mathf.SmoothStep(0f, 1f, t) * 0.35f + t * 0.65f); }
        return a[a.Length - 1];
    }
    static float SP(float v, float e) { return Mathf.Sign(v) * Mathf.Pow(Mathf.Abs(v), e); }

    // a loft: stations along z, each a closed ring (bottom centre -> right -> top centre -> left); upperOnly = canopy
    static Mesh Loft(float[] zs, System.Func<float, Vector4> sec, int nSide, bool upperOnly, float nl, float nu, bool capFront, bool capBack, Vector3 noseTip)
    {
        var v = new List<Vector3>(); var t = new List<int>();
        int ring = 0;
        var rings = new List<int>();
        foreach (float z in zs)
        {
            Vector4 s = sec(z);   // x = half width, y = bottom, z = chine, w = top
            rings.Add(v.Count);
            var pts = new List<Vector3>();
            float a0 = upperOnly ? 0f : -Mathf.PI * 0.5f;
            for (int i = 0; i <= nSide; i++)
            {
                float a = Mathf.Lerp(a0, Mathf.PI * 0.5f, i / (float)nSide);
                float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                float x, y;
                if (sn < 0f) { x = s.x * SP(c, 2f / nl); y = s.z + (s.z - s.y) * SP(sn, 2f / nl); }
                else { x = s.x * SP(c, 2f / nu); y = s.z + (s.w - s.z) * SP(sn, 2f / nu); }
                pts.Add(new Vector3(x, y, z));
            }
            // right side (bottom -> top) then left side (top -> bottom), centres not repeated
            for (int i = 0; i <= nSide; i++) v.Add(pts[i]);
            for (int i = nSide - 1; i >= (upperOnly ? 0 : 1); i--) v.Add(new Vector3(-pts[i].x, pts[i].y, z));
            ring = v.Count - rings[rings.Count - 1];
        }
        for (int k = 0; k < zs.Length - 1; k++)
        {
            int A = rings[k], B = rings[k + 1];
            int last = upperOnly ? ring - 1 : ring;
            for (int i = 0; i < last; i++)
            {
                int i2 = (i + 1) % ring;
                Tri(v, t, A + i, B + i, A + i2, new Vector3(0f, sec(zs[k]).z, zs[k]));
                Tri(v, t, A + i2, B + i, B + i2, new Vector3(0f, sec(zs[k]).z, zs[k]));
            }
        }
        if (capFront) { int c0 = v.Count; v.Add(noseTip); int A = rings[0]; for (int i = 0; i < ring; i++) Tri(v, t, A + i, A + (i + 1) % ring, c0, noseTip - Vector3.forward); }
        if (capBack)
        {
            int k = zs.Length - 1; Vector4 s = sec(zs[k]);
            int c0 = v.Count; v.Add(new Vector3(0f, s.z, zs[k])); int A = rings[k];
            for (int i = 0; i < ring; i++) Tri(v, t, A + i, A + (i + 1) % ring, c0, new Vector3(0f, s.z, zs[k] + 1f));
        }
        var m = new Mesh(); m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }
    // add a triangle facing away from `inside`
    static void Tri(List<Vector3> v, List<int> t, int a, int b, int c, Vector3 inside)
    {
        Vector3 n = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
        Vector3 ctr = (v[a] + v[b] + v[c]) / 3f;
        if (Vector3.Dot(n, ctr - inside) < 0f) { t.Add(a); t.Add(c); t.Add(b); } else { t.Add(a); t.Add(b); t.Add(c); }
    }
    static GameObject MeshGo(Transform p, string name, Mesh m, Material mat)
    {
        var g = new GameObject(name);
        g.transform.SetParent(p, false);
        g.AddComponent<MeshFilter>().sharedMesh = m;
        var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
        return g;
    }

    [System.NonSerialized] public Transform sprung52;
    // ffu52: the photo-matched 2026 Roadster (work/lb-gfx/ff/build_roadster52.py -> Resources/LB/roadster52.bytes): body, gold
    // canopy, cabin, pop-up wing, yoke and 295/30R20 + 335/30R21 wheels from the pack; fans + thruster nozzles + sounds here.
    void BuildPack52(LBPack pk)
    {
        Transform t = transform;
        sprung52 = Mats.Node(t, "Sprung", Vector3.zero);
        pk.Spawn("body", sprung52, Vector3.zero, 1f, HeroCars52.AnoWhite);
        Material blk = Mats.PBR(new Color(0.05f, 0.05f, 0.06f), 0.45f, 0.1f);
        Material rim = Mats.PBR(new Color(0.22f, 0.23f, 0.25f), 0.75f, 0.85f);
        // suction fans spinning in the rear ducts (pack: ducts at (+-0.40, 0.22, -2.09))
        for (int s = -1; s <= 1; s += 2)
        {
            Transform f = Mats.Node(sprung52, "Fan", new Vector3(0.40f * s, 0.22f, -2.075f));
            for (int k = 0; k < 6; k++)
            {
                var b = Mats.Prim(PrimitiveType.Cube, f, Quaternion.Euler(0f, 0f, k * 60f) * new Vector3(0f, 0.065f, 0f), new Vector3(0.04f, 0.11f, 0.008f), rim);
                b.transform.localRotation = Quaternion.Euler(0f, 18f, k * 60f);
            }
            Mats.Prim(PrimitiveType.Cylinder, f, Vector3.zero, new Vector3(0.05f, 0.01f, 0.05f), new Vector3(90f, 0f, 0f), blk);
            if (s < 0) fanL = f; else fanR = f;
        }
        wing = Mats.Node(sprung52, "Wing", new Vector3(0f, 0.9f, -1.98f));
        pk.Spawn("wing", wing, Vector3.zero, 1f, HeroCars52.AnoWhite);
        // wheels: mounts set so the static sag (Rest * 0.45) leaves the tyres centred in the arches; rears 335/30R21 (x1.07)
        float wy = WR - Rest * 0.45f;
        Vector3[] wp = { new Vector3(-0.84f, wy, 1.42f), new Vector3(0.84f, wy, 1.42f), new Vector3(-0.84f, wy, -1.38f), new Vector3(0.84f, wy, -1.38f) };
        for (int i = 0; i < 4; i++)
        {
            Transform n = Mats.Node(t, "Wheel", wp[i]);
            bool rear = wp[i].z < 0f;
            float k = rear ? 0.367f / 0.343f : 1f;
            Transform w = pk.Spawn("wheel", n, rear ? new Vector3(0f, 0.367f - 0.343f, 0f) : Vector3.zero, k, Color.white, wp[i].x < 0f);
            wh[i] = new Wh { mount = wp[i] + Vector3.up * Rest, vis = n, steer = wp[i].z > 0f, dist = Rest * 0.55f + WR };
        }
        yoke = Mats.Node(sprung52, "Yoke", new Vector3(-0.30f, 0.62f, 0.10f));
        if (pk.Has("yoke")) pk.Spawn("yoke", yoke, Vector3.zero, 1f, Color.white);
        // 10 perimeter cold-gas thrusters along the sills (small dark nozzles; puffs come out of them)
        foreach (float z in new[] { 1.85f, 1.0f, 0.05f, -0.95f, -1.85f })
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 p = new Vector3(0.975f * s, 0.215f, z);
                nozP.Add(p);
                Mats.Prim(PrimitiveType.Cylinder, sprung52, p, new Vector3(0.05f, 0.012f, 0.05f), new Vector3(0f, 0f, 90f * s), blk);
            }
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off) continue;   // glass / lamps (LBPack)
            bool main = r.transform.parent != null && r.transform.parent.name == "LB body";
            r.shadowCastingMode = Look.Mobile && !main ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
        }
        if (hissClip == null) MakeClips();
        hissSrc = Sfx.Loop(gameObject, hissClip); fanSrc = Sfx.Loop(gameObject, fanClip);
        Debug.Log("Roadster49: ffu52 photo-matched model (" + pk.tris + " tris)");
    }

    void Build()
    {
        LBPack pk52 = HeroCars52.Pack("roadster52");
        if (pk52 != null && pk52.Has("body") && pk52.Has("wheel")) { BuildPack52(pk52); return; }
        Transform t = transform;
        Material white = Mats.Paint(new Color(0.95f, 0.955f, 0.95f), 0.9f);
        Material blk = Mats.PBR(new Color(0.05f, 0.05f, 0.06f), 0.45f, 0.1f);
        Material satin = Mats.PBR(new Color(0.12f, 0.12f, 0.13f), 0.3f, 0.2f);
        Material glass = Mats.GlassTint(new Color(0.04f, 0.06f, 0.09f, 0.82f));
        Material tyre = Mats.Lit(new Color(0.06f, 0.06f, 0.065f));
        Material rim = Mats.PBR(new Color(0.22f, 0.23f, 0.25f), 0.75f, 0.85f);
        var zs = new List<float>();
        for (float z = 2.24f; z > -2.2f; z -= 0.16f) zs.Add(z);
        zs.Add(-2.2f);
        MeshGo(t, "Body", Loft(zs.ToArray(), z => new Vector4(Body(z, SW), Body(z, SB), Body(z, SC), Body(z, ST)), 9, false, 2.6f, 1.25f, true, true, new Vector3(0f, 0.35f, 2.3f)), white);
        var cz = new List<float>();
        for (float z = 1.05f; z > -1.38f; z -= 0.12f) cz.Add(z);
        cz.Add(-1.38f);
        MeshGo(t, "Canopy", Loft(cz.ToArray(), z => { float b = Body(z, ST) - 0.03f; return new Vector4(Lerp(CZ, CW, z), b, b, Lerp(CZ, CH, z)); }, 8, true, 2f, 2.3f, true, true, new Vector3(0f, Body(1.12f, ST) - 0.02f, 1.12f)), glass);
        // black lower skirts + diffuser + light bars
        for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, t, new Vector3(0.9f * s, 0.24f, 0f), new Vector3(0.12f, 0.1f, 3.4f), blk);
        Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.3f, -2.08f), new Vector3(1.55f, 0.2f, 0.3f), blk);
        Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.43f, 2.04f), new Vector3(0.95f, 0.025f, 0.05f), Mats.Unlit(new Color(0.95f, 0.97f, 1f)));
        Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.7f, -2.205f), new Vector3(1.55f, 0.035f, 0.03f), Mats.Unlit(new Color(1f, 0.08f, 0.06f)));
        // two suction fans in the rear diffuser (spin with the suction)
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 c = new Vector3(0.42f * s, 0.31f, -2.24f);
            Mats.Prim(PrimitiveType.Cylinder, t, c, new Vector3(0.36f, 0.015f, 0.36f), new Vector3(90f, 0f, 0f), satin);
            Transform f = Mats.Node(t, "Fan", c + Vector3.back * 0.02f);
            for (int k = 0; k < 5; k++)
            {
                var b = Mats.Prim(PrimitiveType.Cube, f, Quaternion.Euler(0f, 0f, k * 72f) * new Vector3(0f, 0.08f, 0f), new Vector3(0.05f, 0.15f, 0.01f), rim);
                b.transform.localRotation = Quaternion.Euler(0f, 18f, k * 72f);
            }
            Mats.Prim(PrimitiveType.Cylinder, f, Vector3.zero, new Vector3(0.06f, 0.012f, 0.06f), new Vector3(90f, 0f, 0f), blk);
            if (s < 0) fanL = f; else fanR = f;
        }
        // retractable rear wing (stowed flush with the deck; rises at speed / in the air)
        wing = Mats.Node(t, "Wing", new Vector3(0f, 0.9f, -1.98f));
        Mats.Prim(PrimitiveType.Cube, wing, new Vector3(0f, 0.02f, 0f), new Vector3(1.66f, 0.035f, 0.3f), new Vector3(-6f, 0f, 0f), white);
        for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, wing, new Vector3(0.5f * s, -0.16f, 0.02f), new Vector3(0.035f, 0.34f, 0.12f), blk);
        // wheels: tyre + dark aero rim
        Vector3[] wp = { new Vector3(-0.84f, WR, 1.42f), new Vector3(0.84f, WR, 1.42f), new Vector3(-0.84f, WR, -1.38f), new Vector3(0.84f, WR, -1.38f) };
        for (int i = 0; i < 4; i++)
        {
            Transform n = Mats.Node(t, "Wheel", wp[i]);
            Mats.Prim(PrimitiveType.Cylinder, n, Vector3.zero, new Vector3(WR * 2f, 0.12f, WR * 2f), new Vector3(0f, 0f, 90f), tyre);
            Mats.Prim(PrimitiveType.Cylinder, n, new Vector3(0.012f * Mathf.Sign(wp[i].x), 0f, 0f), new Vector3(WR * 1.45f, 0.121f, WR * 1.45f), new Vector3(0f, 0f, 90f), rim);
            for (int k = 0; k < 5; k++)
            {
                var sp = Mats.Prim(PrimitiveType.Cube, n, Quaternion.Euler(k * 72f, 0f, 0f) * new Vector3(0.122f * Mathf.Sign(wp[i].x) * 0.5f, WR * 0.36f, 0f), new Vector3(0.01f, WR * 0.5f, 0.07f), satin);
                sp.transform.localRotation = Quaternion.Euler(k * 72f, 0f, 0f);
            }
            wh[i] = new Wh { mount = wp[i] + Vector3.up * Rest, vis = n, steer = wp[i].z > 0f, dist = Rest * 0.55f + WR };
        }
        // cabin: two seats + the steer-by-wire yoke
        for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, t, new Vector3(0.28f * s, 0.9f, -0.55f), new Vector3(0.36f, 0.26f, 0.06f), new Vector3(-14f, 0f, 0f), satin);
        yoke = Mats.Node(t, "Yoke", new Vector3(-0.28f, 0.9f, 0.22f));
        Mats.Prim(PrimitiveType.Cube, yoke, Vector3.zero, new Vector3(0.34f, 0.035f, 0.035f), blk);
        for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, yoke, new Vector3(0.17f * s, -0.05f, 0f), new Vector3(0.035f, 0.12f, 0.035f), blk);
        // 10 perimeter cold-gas thrusters along the chine
        foreach (float z in new[] { 1.85f, 1.0f, 0.05f, -0.95f, -1.85f })
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 p = new Vector3(Body(z, SW) * s * 0.995f, Body(z, SC) - 0.01f, z);
                nozP.Add(p);
                Mats.Prim(PrimitiveType.Cylinder, t, p, new Vector3(0.085f, 0.03f, 0.085f), new Vector3(0f, 0f, 90f * s), blk);
            }
        foreach (var r in GetComponentsInChildren<Renderer>()) r.shadowCastingMode = Look.Mobile && r.name != "Body" ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
        // sounds
        if (hissClip == null) MakeClips();
        hissSrc = Sfx.Loop(gameObject, hissClip); fanSrc = Sfx.Loop(gameObject, fanClip);
    }

    static void MakeClips()
    {
        const int sr = 22050;
        var rnd = new System.Random(49);
        float[] h = new float[sr]; float lp = 0f, lp2 = 0f;
        for (int i = 0; i < sr; i++) { float n = (float)rnd.NextDouble() * 2f - 1f; lp += (n - lp) * 0.35f; lp2 += (lp - lp2) * 0.05f; h[i] = (lp - lp2) * 0.9f; }
        for (int i = 0; i < 400; i++) { float k = i / 400f; h[i] = h[i] * k + h[sr - 400 + i] * (1f - k); }   // loop seam
        hissClip = AudioClip.Create("roadster_hiss", sr - 400, 1, sr, false); { var d = new float[sr - 400]; System.Array.Copy(h, 400, d, 0, sr - 400); for (int i = 0; i < 400; i++) d[d.Length - 400 + i] = h[i]; hissClip.SetData(d, 0); }
        int pn = sr * 35 / 100; float[] p = new float[pn]; lp = lp2 = 0f;
        for (int i = 0; i < pn; i++) { float tt = i / (float)sr; float n = (float)rnd.NextDouble() * 2f - 1f; lp += (n - lp) * 0.5f; lp2 += (lp - lp2) * 0.08f; p[i] = (lp - lp2) * Mathf.Min(1f, tt * 200f) * Mathf.Exp(-tt * 9f); }
        puffClip = AudioClip.Create("roadster_puff", pn, 1, sr, false); puffClip.SetData(p, 0);
        float[] f = new float[sr]; lp = 0f;
        for (int i = 0; i < sr; i++) { float tt = i / (float)sr; float n = (float)rnd.NextDouble() * 2f - 1f; lp += (n - lp) * 0.06f; f[i] = 0.32f * Mathf.Sin(2f * Mathf.PI * 150f * tt) + 0.12f * Mathf.Sin(2f * Mathf.PI * 300f * tt) + 0.06f * Mathf.Sin(2f * Mathf.PI * 750f * tt) + lp * 0.8f; }
        fanClip = AudioClip.Create("roadster_fan", sr, 1, sr, false); fanClip.SetData(f, 0);
    }

    // ---------------- input ----------------
    bool Pad { get { return driver != null && inputHook == null && driver.inputKind == InputKind.Gamepad; } }
    bool Touch { get { return driver != null && inputHook == null && driver.inputKind == InputKind.Touch; } }
    bool JetsHeld()
    {
        if (driver == null) return false;
        if (Pad) return inp.gunHeld && !(inp.aimHeld && inp.gas > 0.5f);   // X held
        if (Touch) return inp.fireHeld;                                      // JETS
        return inp.upHeld || (inp.fireHeld && Cursor.lockState == CursorLockMode.Locked);   // Space (or mouse left when locked)
    }
    public override void Drive(PIn i, float camYaw, float dt)
    {
        base.Drive(i, camYaw, dt);
        // one-frame presses are caught here (per frame), acted on in FixedUpdate
        if (Pad) { if (inp.land) fanQ = true; if (inp.warpUp) rollQ = true; }
        else if (Touch) { if (inp.alt) { if (grounded > 0) fanQ = true; else rollQ = true; } }
        else { if (inp.land) fanQ = true; if (inp.warpHome) rollQ = true; }
    }
    public void DemoRoll(float dir = 1f) { rollQ = true; rollDir = dir; }
    public void DemoFans(bool on) { fansOn = on; }

    public override void OnEnter() { base.OnEnter(); if (driver != null && driver.human) driver.Toast(fansOn ? "Roadster: suction fans ON - it sticks to walls + ceilings" : "Roadster: fans off", 2.5f); }

    // ---------------- physics ----------------
    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        if (rb == null || rb.isKinematic) return;
        float dt = Time.fixedDeltaTime;
        bool driven = driver != null;
        if (fanQ) { fanQ = false; fansOn = !fansOn; if (driver != null && driver.human) driver.Toast(fansOn ? "Suction fans ON - drive up walls, round the loop, onto the ceiling" : "Suction fans off", 2f); Sfx.Play(Sfx.Toggle != null ? Sfx.Toggle : Sfx.Click, 0.5f); }
        steerIn = inp.move.x;
        throttle = Mathf.Clamp(inp.move.y + inp.gas - inp.brake, -1f, 1f);
        if (inp.gas > 0.1f) throttle = Mathf.Max(throttle, inp.gas);
        bool jets = driven && JetsHeld() && air > 0.5f;

        // --- suspension (springs scale with the suction load)
        float sk = 1f + SucG / 9.81f * sucK;
        float spring = spring0 * sk, damper = damper0 * Mathf.Sqrt(sk);
        Vector3 up = transform.up;
        grounded = 0; Vector3 nsum = Vector3.zero; bool onLoop = false;
        foreach (Wh w in wh)
        {
            Vector3 o = transform.TransformPoint(w.mount);
            float len = Rest + WR;
            RaycastHit hit;
            if (Physics.Raycast(o, -up, out hit, len, GroundMask, QueryTriggerInteraction.Ignore))
            {
                w.ground = true; w.dist = hit.distance;
                float comp = len - hit.distance;
                float vUp = Vector3.Dot(rb.GetPointVelocity(o), up);
                float f = spring * comp - damper * vUp;
                if (comp > Rest * 0.8f) f += spring * 6f * (comp - Rest * 0.8f);   // bump stop
                if (f < 0f) f = 0f;
                rb.AddForceAtPosition(up * f, o);
                grounded++; nsum += hit.normal;
                if (RallyTrack.IsStunt(hit.collider.gameObject.layer)) onLoop = true;
            }
            else { w.ground = false; w.dist = Mathf.MoveTowards(w.dist, len, dt * 3f); }
        }
        float gf = grounded / 4f;
        gN = grounded > 0 ? nsum.normalized : Vector3.up;
        airT = grounded > 0 ? 0f : airT + dt;

        // --- suction fans
        float sucWant = fansOn && grounded > 0 && !jets && !rolling && (driven || gN.y < 0.9f) ? 1f : 0f;   // parked on the flat: fans idle
        sucK = Mathf.MoveTowards(sucK, sucWant, dt * (sucWant > sucK ? 4f : 8f));
        if (grounded > 0 && sucK > 0f) rb.AddForce(-gN * SucG * sucK * Mathf.Max(gf, 0.5f), ForceMode.Acceleration);

        // --- drive (wheels on a surface)
        if (grounded > 0)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, gN).normalized;
            Vector3 right = Vector3.ProjectOnPlane(transform.right, gN).normalized;
            Vector3 v = rb.velocity;
            float fs = Vector3.Dot(v, fwd), ls = Vector3.Dot(v, right);
            float gAlong = Vector3.Dot(Physics.gravity, fwd);
            slipSpeed = ls; groundFrac = gf;
            bool steep = gN.y < 0.6f;
            float a;
            if (fansOn && sucK > 0.5f && steep && !jets)
            {
                // CRAWL: the throttle nudges the crawl speed, no throttle holds it (gravity cancelled by the fans' grip)
                if (!crawl) { crawl = true; crawlV = Mathf.Clamp(fs, -4f, 16f); }
                if (driven) crawlV = Mathf.Clamp(crawlV + throttle * 8f * dt, -4f, 16f);
                else crawlV = Mathf.MoveTowards(crawlV, 0f, dt * 6f);
                if (demoCrawl >= 0f) crawlV = demoCrawl;
                a = Mathf.Clamp((crawlV - fs) * 4f, -14f, 14f) - gAlong;
            }
            else
            {
                crawl = false;
                if (!driven) a = -fs * 3f - (sucK > 0.3f || Mathf.Abs(fs) < 1f ? gAlong : 0f);   // parked: hold
                else if (throttle > 0.02f)
                {
                    a = LaunchA(fs) * throttle;
                    if (throttle > 0.95f && fs < 2f && gN.y > 0.85f && launchT < 0f) { launchT = 0f; Launches++; }
                }
                else if (throttle < -0.02f) a = fs > 1f ? -16f * -throttle : (fs > -9f ? 7f * throttle : 0f);
                else a = -fs * 0.6f - (sucK > 0.5f && Mathf.Abs(fs) < 1.5f ? gAlong : 0f);
                if (pondSub > 0f) a *= 1f - 0.6f * pondSub;
            }
            if (launchT >= 0f)
            {
                launchT += dt;
                if (fs >= 26.82f) { LastLaunch = launchT; Debug.Log("Roadster49: LAUNCH 0-60 mph in " + launchT.ToString("0.00") + " s"); launchT = -1f; }
                else if (throttle < 0.9f || launchT > 3f) launchT = -1f;
            }
            rb.AddForce(fwd * a * gf, ForceMode.Acceleration);
            float grip = 8f + 7f * sucK;
            rb.AddForce(-right * ls * grip * gf, ForceMode.Acceleration);
            if (sucK > 0.3f) rb.AddForce(-right * Vector3.Dot(Physics.gravity, right) * sucK * gf, ForceMode.Acceleration);   // the fans' grip holds it across a wall
            // yaw (steer-by-wire: quick at low speed, calmer at speed) + lane guide in the loop
            float guide = 0f;
            Vector3 gt; float glat;
            bool inZone = false; foreach (Bounds b in RallyTrack.LoopZones) if (b.Contains(rb.position)) inZone = true;
            if (driven && onLoop && inZone && Mathf.Abs(fs) > 0.5f && RallyTrack.LoopGuide(rb.position, fs >= 0f ? fwd : -fwd, out gt, out glat))
            {
                Vector3 tn = Vector3.ProjectOnPlane(gt, gN).normalized; if (fs < 0f) tn = -tn;
                float ang = Vector3.SignedAngle(fwd, tn, gN) * Mathf.Deg2Rad;
                guide = Mathf.Clamp(ang * 3f - glat * 0.35f, -1.5f, 1.5f) * Mathf.Clamp01(Mathf.Abs(fs) / 2f);
                Vector3 gr = Vector3.Cross(gN, tn).normalized;
                rb.AddForce(-gr * Mathf.Clamp(glat, -2f, 2f) * 2.5f, ForceMode.Acceleration);
            }
            float tr = 1.9f / (1f + Mathf.Max(0f, Mathf.Abs(fs) - 8f) / 22f);
            float target = driven ? steerIn * tr * Mathf.Clamp(fs / 5f, -1f, 1f) : 0f;
            target += guide;
            float yr = Vector3.Dot(rb.angularVelocity, transform.up);
            rb.AddTorque(transform.up * (target - yr) * Mathf.Min(1f, 8f * dt) * gf, ForceMode.VelocityChange);
            if (Mathf.Abs(fs) > 5f && Random.value < 0.3f)
                foreach (Wh w in wh) if (w.ground && gN.y > 0.8f) { Vector3 cp = transform.TransformPoint(w.mount) - up * w.dist; if (!(Layout.InPond(cp.x, cp.z) && cp.y < Layout.WaterY)) FX.Dust(cp, 0.12f * Mathf.Clamp01(Mathf.Abs(fs) / 25f)); }
        }
        else { crawl = false; launchT = -1f; }

        // --- thrusters
        jetsOn = jets;
        if (jets)
        {
            rb.AddForce(transform.up * JetA, ForceMode.Acceleration);
            air = Mathf.Max(0f, air - JetLps * dt);
            if (!jetsPrevF) { Hops++; Puff(0.55f); if (grounded > 0 && agl < 2f) FX.Ring(transform.position, 4f, 14, 6f, new Color(0.85f, 0.88f, 0.9f, 0.5f)); }
        }
        jetsPrevF = jets;

        // --- altitude above ground (straight down)
        RaycastHit gh;
        agl = Physics.Raycast(rb.position + Vector3.up * 0.3f, Vector3.down, out gh, 120f, GroundMask, QueryTriggerInteraction.Ignore) ? gh.distance - 0.3f : 99f;

        // --- barrel roll
        if (rollQ)
        {
            rollQ = false;
            if (!rolling && air >= RollL * 0.5f)
            {
                rolling = true; rollT = 0f; rollAng = 0f; Rolls++;
                rollInv = transform.up.y < 0f;
                rollGoal = rollInv ? 180f : 360f;
                if (Mathf.Abs(steerIn) > 0.2f) rollDir = Mathf.Sign(steerIn);
                air = Mathf.Max(0f, air - RollL);
                if (grounded > 0) { rb.velocity += transform.up * 5f; sucK = 0f; }   // hop off the surface first
                Puff(0.7f);
                Debug.Log("Roadster49: BARREL ROLL start (" + (rollInv ? "from upside down, half roll" : "full roll") + ") at " + rb.position.ToString("0.0") + " grounded " + grounded);
            }
        }
        // --- attitude control in the air (thruster puffs), auto-level, roll
        puffCmdP = puffCmdR = 0f;
        if (rolling)
        {
            rollT += dt;
            Vector3 w = rb.angularVelocity;
            float wr = Vector3.Dot(w, transform.forward);
            float want = -rollDir * 7.2f;
            rb.AddTorque(transform.forward * (want - wr) * 14f, ForceMode.Acceleration);
            Vector3 other = w - transform.forward * wr; rb.AddTorque(-other * 6f, ForceMode.Acceleration);
            rollAng += Mathf.Abs(wr) * Mathf.Rad2Deg * dt;
            if (air > 0f) { rb.AddForce(Vector3.up * 8.5f, ForceMode.Acceleration); air = Mathf.Max(0f, air - 25f * dt); }   // hold altitude through the roll
            puffCmdR = -rollDir;
            if ((rollAng > rollGoal - 25f && transform.up.y > 0.75f) || rollAng > rollGoal + 90f || rollT > 2.5f)
            {
                rolling = false;
                Debug.Log("Roadster49: BARREL ROLL done " + rollAng.ToString("0") + " deg in " + rollT.ToString("0.00") + " s, up " + transform.up.y.ToString("0.00") + " agl " + agl.ToString("0.0"));
            }
        }
        else if (grounded == 0 && airT > 0.15f)
        {
            Vector3 w = rb.angularVelocity;
            float pIn = driven ? inp.move.y : 0f, rIn = driven ? inp.move.x : 0f;
            Vector3 want = transform.right * (pIn * 2.6f) + transform.forward * (-rIn * 3.4f) + transform.up * Vector3.Dot(w, transform.up);
            bool hands = Mathf.Abs(pIn) > 0.1f || Mathf.Abs(rIn) > 0.1f;
            if (!hands && air > 0f) want += Vector3.Cross(transform.up, Vector3.up) * 3.2f;   // level itself
            float gain = air > 0f ? 9f : 1.2f;
            Vector3 dw = want - w; dw -= transform.up * Vector3.Dot(dw, transform.up);
            rb.AddTorque(dw * gain, ForceMode.Acceleration);
            if (air > 0f) air = Mathf.Max(0f, air - Mathf.Min(dw.magnitude, 4f) * 2.2f * dt);
            puffCmdP = Mathf.Clamp(Vector3.Dot(dw, transform.right), -1f, 1f); puffCmdR = Mathf.Clamp(-Vector3.Dot(dw, transform.forward), -1f, 1f);
        }
        // --- landing assist: eases itself down while there is air
        landing = false;
        if (grounded == 0 && !jets && !rolling && air > 0.5f && transform.up.y > 0.55f && airT > 0.2f)
        {
            float vy = rb.velocity.y;
            float trig = 1.5f + Mathf.Max(0f, -vy) * 0.42f;
            if (agl < trig && vy < -2.2f)
            {
                float a = Mathf.Clamp((-1.6f - vy) * 5f + 9.81f, 0f, 30f);
                rb.AddForce(transform.up * a / Mathf.Max(0.55f, transform.up.y), ForceMode.Acceleration);
                air = Mathf.Max(0f, air - a / JetA * JetLps * dt);
                landing = true;
                if (!landPrev) Puff(0.45f);
            }
        }
        landPrev = landing;

        // --- air refill
        refilling = false;
        if (grounded > 0 && !jets && !rolling && air < TankL)
        {
            Vector3 p = rb.position;
            bool garage = Mathf.Abs(p.x - Layout.GarageC.x) < Layout.GarageSize.x * 0.5f && Mathf.Abs(p.z - Layout.GarageC.y) < Layout.GarageSize.y * 0.5f && Speed < 2f;
            air = Mathf.Min(TankL, air + (garage ? TankL / 4f : TankL / 30f) * dt);
            refilling = garage;
        }
    }
    bool jetsPrevF, landPrev;

    static float LaunchA(float fs)
    {
        if (fs < 26f) return 36f;                         // 0-60 mph in ~0.8 s
        return 36f * Mathf.Clamp01((60f - fs) / 34f);     // tops out ~216 km/h
    }

    void Puff(float vol)
    {
        float k = driver != null && driver.human ? 1f : Mathf.Clamp01(1f - Sfx.Near(transform.position) / 60f);
        if (puffClip != null && k > 0.02f) Sfx.Play(puffClip, vol * k, Random.Range(0.9f, 1.15f));
    }

    // ---------------- visuals ----------------
    void Update()
    {
        float dt = Time.deltaTime;
        float fs = ForwardSpeed;
        foreach (Wh w in wh)
        {
            Vector3 p = w.mount - Vector3.up * (w.dist - WR);
            w.spin += fs / WR * Mathf.Rad2Deg * dt;
            w.vis.localPosition = p;
            w.vis.localRotation = Quaternion.Euler(0f, w.steer ? steerIn * 24f : 0f, 0f) * Quaternion.Euler(w.spin, 0f, 0f);
        }
        if (yoke != null) yoke.localRotation = Quaternion.Euler(-20f, 0f, -steerIn * 35f);
        // wing rises at speed / in the air / on the thrusters
        float ww = Speed > 22f || grounded == 0 || jetsOn || crawl ? 1f : 0f;
        wingK = Mathf.MoveTowards(wingK, ww, dt * 2.5f);
        if (wing != null) wing.localPosition = new Vector3(0f, 0.9f + 0.3f * Mathf.SmoothStep(0f, 1f, wingK), -1.98f);
        // fans
        fanSpin += dt * (fansOn ? 600f + 2400f * sucK : 0f);
        if (fanL != null) { fanL.localRotation = Quaternion.Euler(0f, 0f, fanSpin); fanR.localRotation = Quaternion.Euler(0f, 0f, -fanSpin); }
        // thruster puffs (cold gas: white, quick)
        jetVis = Mathf.MoveTowards(jetVis, jetsOn ? 1f : 0f, dt * 12f);
        landVis = Mathf.MoveTowards(landVis, landing ? 1f : 0f, dt * 10f);
        float lift = Mathf.Max(jetVis, landVis * 0.7f);
        bool rollV = rolling;
        float everyK = Look.Mobile ? 0.5f : 1f;
        if (lift > 0.05f || Mathf.Abs(puffCmdP) > 0.2f || Mathf.Abs(puffCmdR) > 0.2f)
        {
            Vector3 v = Velocity;
            for (int i = 0; i < nozP.Count; i++)
            {
                if (Random.value > everyK) continue;
                Vector3 lp = nozP[i];
                float side = Mathf.Sign(lp.x), front = lp.z > 0.5f ? 1f : lp.z < -0.5f ? -1f : 0f;
                float wv = lift - puffCmdR * side * 0.8f - puffCmdP * front * 0.8f;
                if (Mathf.Abs(wv) < 0.18f) continue;
                Vector3 d = transform.TransformDirection(new Vector3(side * 0.35f, wv > 0f ? -1f : 1f, 0f)).normalized;
                Vector3 wp = transform.TransformPoint(lp);
                float s = Mathf.Clamp01(Mathf.Abs(wv));
                FX.Puff(wp + d * 0.08f, d * (10f + 8f * s) + v * 0.85f + Random.insideUnitSphere * 0.8f, Random.Range(0.25f, 0.45f) * (0.6f + s), Random.Range(0.28f, 0.45f), new Color(0.93f, 0.96f, 1f, 0.55f));
            }
        }
        if (rollV && Random.value < 0.5f) FX.Puff(transform.position + Random.insideUnitSphere * 0.5f, -Velocity * 0.1f, 0.6f, 0.6f, new Color(0.95f, 0.97f, 1f, 0.35f));
        // fans pull a little dust in from round the skirts on dirt / grass
        if (sucK > 0.6f && grounded > 0 && gN.y > 0.8f && Speed < 12f && Random.value < 0.25f * everyK)
        {
            float s = Random.value < 0.5f ? -1f : 1f;
            Vector3 p = transform.TransformPoint(new Vector3(1.4f * s, 0.05f, Random.Range(-1.6f, 1.6f)));
            FX.Puff(p, (transform.position - p) * 1.2f + Vector3.up * 0.2f, 0.35f, 0.6f, new Color(0.62f, 0.55f, 0.45f, 0.3f));
        }
        // sound
        float heard = driver != null && driver.human ? 1f : Mathf.Clamp01(1f - Sfx.Near(transform.position) / 55f);
        if (hissSrc != null)
        {
            float hv = Mathf.Max(jetVis, landVis * 0.8f, rollV ? 0.7f : 0f, Mathf.Clamp01(Mathf.Abs(puffCmdP) + Mathf.Abs(puffCmdR)) * 0.35f) * 0.55f * heard;
            hissSrc.volume = Mathf.MoveTowards(hissSrc.volume, hv, dt * 6f);
            hissSrc.pitch = 0.9f + 0.25f * jetVis;
            if (hv > 0.005f && !hissSrc.isPlaying) hissSrc.Play(); else if (hv <= 0.005f && hissSrc.volume < 0.003f && hissSrc.isPlaying) hissSrc.Pause();
        }
        if (fanSrc != null)
        {
            float fv = (fansOn && (driver != null || sucK > 0.1f) ? 0.05f + 0.16f * sucK : 0f) * heard;
            fanSrc.volume = Mathf.MoveTowards(fanSrc.volume, fv, dt * 2f);
            fanSrc.pitch = 0.8f + 0.5f * sucK;
            if (fv > 0.004f && !fanSrc.isPlaying) fanSrc.Play(); else if (fv <= 0.004f && fanSrc.volume < 0.003f && fanSrc.isPlaying) fanSrc.Pause();
        }
    }
}

// ffu49 C-RAMP by the loop: a lead-in pad, a half-loop that curls up and over (R 6.5 m) and an upside-down ceiling 13 m up
// that runs back over the lead-in for 14 m and ends in the air - the Roadster stunt from the reveal (drive up the C, stick
// to the ceiling with the fans, drop / jump off, barrel roll, ease down). The driving surface is on RallyTrack.LoopSurfLayer
// (wheel rays see it, vehicle bodies don't - same as the loop), rails + posts are solid. Site picked at build time near the
// loop on flat, empty ground (logged "CRamp49: site ...").
public static class CRamp49
{
    public const float R = 6.5f, W = 5.4f, Lead = 12f, Ceil = 14f;
    public static bool Built;
    public static Vector3 Origin;     // world point of the arc bottom (local 0,0,0), on the pad surface
    public static Quaternion Rot;      // local +z = driving direction into the C
    public static Vector3 Fwd { get { return Rot * Vector3.forward; } }
    public static Vector3 Right { get { return Rot * Vector3.right; } }

    // local centreline frame at arc length s from the lead-in start (s 0 = lead start at local z -Lead)
    public static float Length { get { return Lead + Mathf.PI * R + Ceil; } }
    public static void Frame(float s, out Vector3 p, out Vector3 t, out Vector3 n)
    {
        if (s <= Lead) { p = new Vector3(0f, 0f, -Lead + s); t = Vector3.forward; n = Vector3.up; }
        else if (s <= Lead + Mathf.PI * R)
        {
            float th = (s - Lead) / R;
            p = new Vector3(0f, R * (1f - Mathf.Cos(th)), R * Mathf.Sin(th));
            t = new Vector3(0f, Mathf.Sin(th), Mathf.Cos(th));
            n = new Vector3(0f, Mathf.Cos(th), -Mathf.Sin(th));   // towards the arc centre
        }
        else { float u = s - Lead - Mathf.PI * R; p = new Vector3(0f, 2f * R, -u); t = Vector3.back; n = Vector3.down; }
    }
    public static Vector3 W2(Vector3 local) { return Origin + Rot * local; }
    // nearest centreline sample to a world point (for the probe autopilot); returns s
    public static float Nearest(Vector3 world, out Vector3 p, out Vector3 t, out Vector3 n)
    {
        Vector3 l = Quaternion.Inverse(Rot) * (world - Origin);
        float best = 1e9f, bs = 0f;
        for (float s = 0f; s <= Length; s += 0.25f)
        {
            Vector3 q, tt, nn; Frame(s, out q, out tt, out nn);
            float d = (new Vector3(0f, l.y, l.z) - q).sqrMagnitude;
            if (d < best) { best = d; bs = s; }
        }
        Frame(bs, out p, out t, out n);
        p = W2(p); t = Rot * t; n = Rot * n;
        return bs;
    }

    public static void Build()
    {
        if (Built) return;
        Vector2 site; float yaw;
        if (!PickSite(out site, out yaw)) { Debug.Log("CRamp49: no clear site near the loop - not built"); return; }
        Built = true;
        Rot = Quaternion.Euler(0f, yaw, 0f);
        // pad height = highest ground under the footprint
        float hi = -1e9f;
        for (float u = -W * 0.5f - 1f; u <= W * 0.5f + 1f; u += 1f)
            for (float v = -Lead - 5f; v <= R + 1f; v += 1f) { Vector3 q = new Vector3(site.x, 0f, site.y) + Rot * new Vector3(u, 0f, v); hi = Mathf.Max(hi, Ranch.GY(q.x, q.z)); }
        Origin = new Vector3(site.x, hi + 0.12f, site.y);
        var root = new GameObject("C-Ramp (ffu49)").transform;
        Material surf = Mats.PBR(new Color(0.42f, 0.44f, 0.47f), 0.35f, 0.1f), under = Mats.PBR(new Color(0.85f, 0.86f, 0.88f), 0.4f, 0.3f);
        Material red = Mats.Paint(new Color(0.85f, 0.15f, 0.12f), 0.6f), white = Mats.Paint(new Color(0.95f, 0.95f, 0.95f), 0.6f), steel = Mats.Steel(new Color(0.55f, 0.57f, 0.6f));
        // driving surface (+ a short wedge from the ground up onto the pad)
        var sv = new List<Vector3>(); var st = new List<int>(); var ov = new List<Vector3>(); var ot = new List<int>();
        var path = new List<float>();
        for (float s = 0f; s < Length; s += 0.5f) path.Add(s);
        path.Add(Length);
        foreach (float s in path)
        {
            Vector3 p, t, n; Frame(s, out p, out t, out n);
            sv.Add(W2(p + Vector3.left * W * 0.5f)); sv.Add(W2(p + Vector3.right * W * 0.5f));
            ov.Add(W2(p - n * 0.35f + Vector3.left * (W * 0.5f + 0.15f))); ov.Add(W2(p - n * 0.35f + Vector3.right * (W * 0.5f + 0.15f)));
        }
        for (int i = 0; i < path.Count - 1; i++)
        {
            Vector3 p, t, n; Frame(path[i], out p, out t, out n); Vector3 nw = Rot * n;
            int a = i * 2;
            Quad(sv, st, a, a + 1, a + 2, a + 3, nw);
            Quad(ov, ot, a, a + 1, a + 2, a + 3, -nw);
        }
        var sg = MeshObj(root, "C-Ramp surface", sv, st, surf);
        sg.layer = RallyTrack.LoopSurfLayer;
        var mc = sg.AddComponent<MeshCollider>(); mc.sharedMesh = sg.GetComponent<MeshFilter>().sharedMesh;
        MeshObj(root, "C-Ramp underside", ov, ot, under);
        // wedge up onto the pad (solid, default layer) + the lead-in pad block
        float drop = Origin.y - Ranch.GY(W2(new Vector3(0f, 0f, -Lead - 4f)).x, W2(new Vector3(0f, 0f, -Lead - 4f)).z);
        Vector3 wc = W2(new Vector3(0f, -Mathf.Max(0.05f, drop) * 0.5f - 0.05f, -Lead - 2.5f));
        float wl = 5.2f, wa = Mathf.Atan2(Mathf.Max(0.05f, drop) + 0.1f, 5f) * Mathf.Rad2Deg;
        var wedge = Mats.Prim(PrimitiveType.Cube, root, wc, new Vector3(W, 0.3f, wl), surf, true);
        wedge.transform.rotation = Rot * Quaternion.Euler(-wa, 0f, 0f);
        var pad = Mats.Prim(PrimitiveType.Cube, root, W2(new Vector3(0f, -0.63f, -Lead * 0.5f + 0.2f)), new Vector3(W + 0.6f, 1.2f, Lead + 0.6f), under, true);
        pad.transform.rotation = Rot;
        // side rails along the whole C (solid, StuntLayer) - red / white blocks
        for (float s = Lead + 0.5f; s < Length - 0.3f; s += 1f)
        {
            Vector3 p, t, n; Frame(s, out p, out t, out n);
            for (int k = -1; k <= 1; k += 2)
            {
                var b = Mats.Prim(PrimitiveType.Cube, root, W2(p + Vector3.right * k * (W * 0.5f + 0.12f) + n * 0.15f), new Vector3(0.24f, 0.5f, 1.02f), ((int)s % 2 == 0) ? red : white, true);
                b.transform.rotation = Rot * Quaternion.LookRotation(t, n);
                b.layer = RallyTrack.StuntLayer;
            }
        }
        // support posts + gantry beams over the ceiling (outside the lane)
        foreach (float z in new[] { 1.0f, -6.5f, -13.5f })
        {
            for (int k = -1; k <= 1; k += 2)
            {
                Vector3 top = W2(new Vector3(k * (W * 0.5f + 0.7f), 2f * R + 0.6f, z));
                float g = Ranch.GY(top.x, top.z);
                Mats.Prim(PrimitiveType.Cube, root, new Vector3(top.x, (g + top.y) * 0.5f, top.z), new Vector3(0.4f, top.y - g, 0.4f), steel, true).transform.rotation = Rot;
            }
            var beam = Mats.Prim(PrimitiveType.Cube, root, W2(new Vector3(0f, 2f * R + 0.6f, z)), new Vector3(W + 1.8f, 0.4f, 0.4f), steel, true);
            beam.transform.rotation = Rot;
        }
        Vector3 sp = W2(new Vector3(-(W * 0.5f + 2.5f), 0f, -Lead - 3f));
        Ranch.Sign(new Vector3(sp.x, Ranch.GY(sp.x, sp.z) + 3f, sp.z), yaw + 180f, "C-RAMP\n<size=26>Roadster: fans ON</size>", new Color(0.12f, 0.3f, 0.55f), 5f, 2.2f);
        Debug.Log("CRamp49: site " + site.ToString("0.0") + " yaw " + yaw.ToString("0") + " origin y " + Origin.y.ToString("0.00") + " ceiling y " + (Origin.y + 2f * R).ToString("0.0") + " lead start " + W2(new Vector3(0f, 0f, -Lead)).ToString("0.0"));
    }

    static void Quad(List<Vector3> v, List<int> t, int a, int b, int c, int d, Vector3 want)
    {
        Vector3 n = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
        if (Vector3.Dot(n, want) >= 0f) { t.Add(a); t.Add(b); t.Add(c); t.Add(b); t.Add(d); t.Add(c); }
        else { t.Add(a); t.Add(c); t.Add(b); t.Add(b); t.Add(c); t.Add(d); }
    }
    static GameObject MeshObj(Transform p, string name, List<Vector3> v, List<int> t, Material m)
    {
        var g = new GameObject(name); g.transform.SetParent(p, false);
        var mesh = new Mesh(); mesh.SetVertices(v); mesh.SetTriangles(t, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        g.AddComponent<MeshFilter>().sharedMesh = mesh; g.AddComponent<MeshRenderer>().sharedMaterial = m;
        return g;
    }

    // ffu49b: candidates on a 6 m grid round the loop (nearest first, 4 headings); geometric checks, then nothing solid in the
    // footprint. If no spot is fully clear, the best one blocked only by a few small static props (trees / bushes / rocks:
    // no rigidbody, not a vehicle / prop / track layer, < 5 m) gets those props hidden. Rejection counts are logged.
    static readonly int[] why = new int[8];
    static bool PickSite(out Vector2 site, out float yaw)
    {
        Vector2 lc = Layout.LoopC + new Vector2(0f, Layout.LoopShift * 0.5f);
        var cands = new List<Vector3>();
        for (float x = -136f; x <= -16f; x += 6f)
            for (float z = 10f; z <= 118f; z += 6f)
                foreach (float y in new[] { 90f, 270f, 0f, 180f }) cands.Add(new Vector3(x, y, z));
        cands.Sort((a, b) => (new Vector2(a.x, a.z) - lc).sqrMagnitude.CompareTo((new Vector2(b.x, b.z) - lc).sqrMagnitude));
        System.Array.Clear(why, 0, why.Length);
        Vector3 bestSoft = Vector3.zero; List<Collider> bestBlock = null; int logged = 0;
        foreach (var c in cands)
        {
            Vector2 s = new Vector2(c.x, c.z);
            List<Collider> block;
            int r = Clear(s, c.y, out block);
            if (r == 0) { site = s; yaw = c.y; LogWhy("clear"); return true; }
            why[r]++;
            if (r == 6)
            {
                if (logged < 3) { logged++; string n = ""; for (int i = 0; i < block.Count && i < 4; i++) n += block[i].name + "(" + LayerMask.LayerToName(block[i].gameObject.layer) + "/" + block[i].gameObject.layer + ") "; Debug.Log("CRamp49: " + s.ToString("0") + " yaw " + c.y + " blocked by " + block.Count + ": " + n); }
                if (bestBlock == null || block.Count < bestBlock.Count)
                {
                    bool soft = block.Count <= 6;
                    foreach (var b in block) if (b.attachedRigidbody != null || b.gameObject.layer != 0 || b.bounds.size.magnitude > 9f || b is MeshCollider) soft = false;
                    if (soft) { bestBlock = block; bestSoft = c; }
                }
            }
        }
        if (bestBlock != null)
        {
            string n = "";
            foreach (var b in bestBlock) { n += b.name + " "; Transform t = b.transform; while (t.parent != null && t.parent.GetComponent<Collider>() == null && t.parent.childCount == 1) t = t.parent; t.gameObject.SetActive(false); }
            site = new Vector2(bestSoft.x, bestSoft.z); yaw = bestSoft.y; LogWhy("cleared " + bestBlock.Count + " small props (" + n.Trim() + ")"); return true;
        }
        LogWhy("none");
        site = Vector2.zero; yaw = 0f; return false;
    }
    static void LogWhy(string res) { Debug.Log("CRamp49: site pick " + res + " | rejected: bounds " + why[1] + " road " + why[2] + " pond " + why[3] + " reserved " + why[4] + " buildings " + why[5] + " solid " + why[6] + " slope " + why[7]); }
    // 0 ok, 1 bounds, 2 road, 3 pond, 4 reserved, 5 house / garage / pads, 6 something solid there, 7 too sloped
    static int Clear(Vector2 c, float yaw, out List<Collider> block)
    {
        block = null;
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        float lo = 1e9f, hi = -1e9f;
        const float runUp = 20f;
        for (float u = -5f; u <= 5f; u += 5f)
            for (float v = -Lead - runUp; v <= R + 3f; v += 4f)
            {
                Vector3 w = new Vector3(c.x, 0f, c.y) + q * new Vector3(u, 0f, v);
                if (Mathf.Abs(w.x) > 170f || Mathf.Abs(w.z) > 170f) return 1;
                if (Layout.RoadDist(w.x, w.z) < Layout.TrackW * 0.5f + 1.5f) return 2;
                if (Layout.PondQ(w.x, w.z) < 1.25f) return 3;
                if (JetLayout.Reserved(w.x, w.z) || RcTrack.Reserved(w.x, w.z) || MonsterPark.Reserved(w.x, w.z) || CarrierLayout.Reserved(w.x, w.z) || MissionSite.Reserved(w.x, w.z)) return 4;
                if (Worlds.StageEOn && RanchLife.Reserved(w.x, w.z)) return 4;
                if ((new Vector2(w.x, w.z) - StorySet.PadXZ).magnitude < 25f || (new Vector2(w.x, w.z) - Layout.PadC).magnitude < 30f) return 5;
                if (Mathf.Abs(w.x - Layout.HouseC.x) < Layout.HouseSize.x * 0.5f + 8f && Mathf.Abs(w.z - Layout.HouseC.y) < Layout.HouseSize.y * 0.5f + 8f) return 5;
                if (Mathf.Abs(w.x - Layout.GarageC.x) < Layout.GarageSize.x * 0.5f + 8f && Mathf.Abs(w.z - Layout.GarageC.y) < Layout.GarageSize.y * 0.5f + 14f) return 5;
                float g = Ranch.GY(w.x, w.z);
                if (v > -Lead - 6f) { lo = Mathf.Min(lo, g); hi = Mathf.Max(hi, g); }
            }
        if (hi - lo > 1.6f) return 7;
        // nothing built there already: the structure footprint (+1 m), 0.3 .. 15 m above the ground
        Vector3 ctr = new Vector3(c.x, lo + 7.8f, c.y) + q * new Vector3(0f, 0f, (-Lead - 6f + R + 1f) * 0.5f);
        var hits = Physics.OverlapBox(ctr, new Vector3(W * 0.5f + 1.6f, 7.4f, (Lead + 6f + R + 1f) * 0.5f), q, ~((1 << 2) | (1 << Vehicle.FrogLayer)), QueryTriggerInteraction.Ignore);
        foreach (var h in hits) if (!(h is TerrainCollider)) { if (block == null) block = new List<Collider>(); block.Add(h); }
        // and the run-up lane in front of it, low (fences / props)
        Vector3 rc = new Vector3(c.x, lo + 1.2f, c.y) + q * new Vector3(0f, 0f, -Lead - 6f - runUp * 0.5f);
        foreach (var h in Physics.OverlapBox(rc, new Vector3(2f, 0.9f, runUp * 0.5f), q, ~((1 << 2) | (1 << Vehicle.FrogLayer)), QueryTriggerInteraction.Ignore))
            if (!(h is TerrainCollider)) { if (block == null) block = new List<Collider>(); block.Add(h); }
        return block != null ? 6 : 0;
    }
}
