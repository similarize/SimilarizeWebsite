using UnityEngine;

// Car colours: each colour is its own car with its own stats. GREEN has the clearly highest top speed.
public struct CarSpec
{
    public string name;
    public Color color;
    public float top;        // top speed, m/s
    public float accel;      // m/s^2
    public float handling;   // max turn rate, deg/s
    public float weight;     // bump strength + shorter spin-outs when heavier
    public CarSpec(string n, Color c, float t, float a, float h, float w) { name = n; color = c; top = t; accel = a; handling = h; weight = w; }
    public int Kmh { get { return Mathf.RoundToInt(top * 3.6f); } }
}

public static class Cars
{
    public static readonly CarSpec[] All = {
        new CarSpec("Green",  new Color(0.18f, 0.90f, 0.33f), 33.5f, 10.0f,  82f, 1.10f),
        new CarSpec("Orange", new Color(1.00f, 0.48f, 0.10f), 31.0f, 11.0f,  88f, 1.00f),
        new CarSpec("Yellow", new Color(1.00f, 0.82f, 0.12f), 30.5f, 12.0f,  92f, 0.95f),
        new CarSpec("Red",    new Color(0.90f, 0.15f, 0.15f), 30.0f, 11.5f,  95f, 1.00f),
        new CarSpec("Blue",   new Color(0.18f, 0.45f, 1.00f), 29.5f, 12.5f,  98f, 0.95f),
        new CarSpec("Purple", new Color(0.58f, 0.30f, 1.00f), 29.0f, 13.0f, 100f, 0.90f),
        new CarSpec("White",  new Color(0.95f, 0.95f, 0.95f), 28.5f, 13.5f, 104f, 0.90f),
        new CarSpec("Black",  new Color(0.10f, 0.10f, 0.12f), 29.0f, 10.5f,  90f, 1.35f) };
    public const float MaxTop = 33.5f, MinTop = 28.5f;
}

// The robot roster from the Four Froggies bible (ff3d112 robot table). Names are never invented;
// with 8 racers and 7 robots one robot drives twice (shown as "robot · colour").
public static class Robots
{
    public static readonly string[] Names = { "Optimus", "Unitree", "Figure 03", "Figure 02", "Big Figure Two", "Atlas HD", "Atlas electric" };
    public static readonly string[] Looks = {
        "glossy white, black face visor",
        "compact charcoal, cyan eye strip",
        "soft grey, black egg face",
        "dark body, silver head, light strip",
        "big + bulky, boxy head",
        "slim, round lamp head, amber ring",
        "grey + black joints, white ring, blue eye" };
    public static int Count { get { return Names.Length; } }
}

public static class KartModel
{
    // Builds the visual car + robot under `body`. Returns wheel steer pivots (FL, FR) and spinning wheels (all 4)
    // and the robot head (turns into corners).
    public static void Build(Transform body, int car, int robot, out Transform[] steer, out Transform[] spin, out Transform head)
    {
        CarSpec cs = Cars.All[car];
        var stat = new GameObject("Static").transform;
        stat.SetParent(body, false);
        Material paint = Mats.Shiny(cs.color);
        Material carbon = Mats.Lit(new Color(0.07f, 0.07f, 0.08f));
        Material glass = Mats.Glass;
        Material lamp = Mats.Unlit(new Color(1f, 0.98f, 0.9f));
        Material tail = Mats.Unlit(new Color(1f, 0.12f, 0.1f));

        // ---- wedge body ----
        Geo.Block(stat, paint, 0f, 0.85f, 2.3f, 1.96f, 1.78f, 0.2f, 0.74f, 1.8f, 1.5f, 0.22f, 0.42f);      // nose
        Geo.Block(stat, paint, 0f, -0.85f, 0.85f, 2.02f, 1.86f, 0.18f, 0.8f, 1.96f, 1.78f, 0.2f, 0.74f);   // mid
        Geo.Block(stat, paint, 0f, -2.25f, -0.85f, 1.98f, 1.7f, 0.22f, 0.78f, 2.02f, 1.86f, 0.18f, 0.8f);  // rear deck
        Geo.Block(stat, paint, 0f, -2.1f, -1.0f, 1.3f, 0.9f, 0.78f, 0.9f, 1.5f, 1.1f, 0.8f, 1.0f);         // engine cover
        for (int i = 0; i < 4; i++)                                                                           // louvres
            Mats.Prim(PrimitiveType.Cube, stat, new Vector3(0f, 0.97f - i * 0.025f, -1.15f - i * 0.22f), new Vector3(0.95f, 0.03f, 0.08f), carbon);
        Geo.Block(stat, carbon, 0f, -0.85f, 0.42f, 1.5f, 1.5f, 0.78f, 0.81f, 1.5f, 1.5f, 0.78f, 0.81f);   // cockpit opening
        Geo.Block(stat, glass, 0f, 0.35f, 0.85f, 1.35f, 1.35f, 1.06f, 1.12f, 1.6f, 1.6f, 0.74f, 0.8f);      // windscreen
        Mats.Prim(PrimitiveType.Cube, stat, new Vector3(0f, 1.11f, 0.37f), new Vector3(1.38f, 0.05f, 0.07f), carbon);
        Geo.Block(stat, paint, 0f, -1.02f, -0.86f, 0.62f, 0.38f, 0.8f, 1.1f, 0.62f, 0.38f, 0.8f, 1.1f);     // roll hoop
        for (int s = -1; s <= 1; s += 2)
        {
            Geo.Block(stat, carbon, s * 0.99f, -1.25f, -0.2f, 0.1f, 0.1f, 0.3f, 0.74f, 0.03f, 0.03f, 0.45f, 0.6f);   // side intake
            Mats.Prim(PrimitiveType.Cube, stat, new Vector3(s * 1.0f, 0.24f, 0f), new Vector3(0.06f, 0.12f, 2.0f), carbon);   // skirt
            var hl = Mats.Prim(PrimitiveType.Cube, stat, new Vector3(s * 0.6f, 0.44f, 2.24f), new Vector3(0.44f, 0.05f, 0.1f), lamp);
            hl.transform.localRotation = Quaternion.Euler(0f, s * 14f, s * -6f);
            Mats.Prim(PrimitiveType.Cube, stat, new Vector3(s * 0.74f, 0.38f, 2.2f), new Vector3(0.05f, 0.1f, 0.08f), lamp);
            var tl = Mats.Prim(PrimitiveType.Cube, stat, new Vector3(s * 0.62f, 0.67f, -2.26f), new Vector3(0.55f, 0.06f, 0.05f), tail);
            tl.transform.localRotation = Quaternion.Euler(0f, 0f, s * 8f);
            Mats.Prim(PrimitiveType.Cube, stat, new Vector3(s * 0.38f, 0.58f, -2.26f), new Vector3(0.06f, 0.16f, 0.05f), tail);
            Mats.Prim(PrimitiveType.Cube, stat, new Vector3(s * 1.04f, 0.93f, 0.42f), new Vector3(0.2f, 0.1f, 0.09f), paint);   // mirror
            Mats.Prim(PrimitiveType.Cube, stat, new Vector3(s * 0.94f, 0.86f, 0.42f), new Vector3(0.12f, 0.04f, 0.04f), carbon);
            Mats.Prim(PrimitiveType.Cube, stat, new Vector3(s * 0.55f, 1.0f, -1.98f), new Vector3(0.06f, 0.38f, 0.12f), carbon);  // wing struts
        }
        Geo.Block(stat, carbon, 0f, 1.98f, 2.42f, 1.84f, 1.84f, 0.15f, 0.21f, 1.7f, 1.7f, 0.15f, 0.21f);    // splitter
        Geo.Block(stat, carbon, 0f, -2.34f, -2.0f, 1.72f, 1.72f, 0.15f, 0.42f, 1.72f, 1.72f, 0.15f, 0.42f);  // diffuser
        Mats.Prim(PrimitiveType.Cylinder, stat, new Vector3(0f, 0.47f, -2.33f), new Vector3(0.24f, 0.06f, 0.24f), new Vector3(90f, 0f, 0f), Mats.Steel(new Color(0.3f, 0.3f, 0.32f)));
        Mats.Prim(PrimitiveType.Cube, stat, new Vector3(0f, 1.2f, -2.03f), new Vector3(1.96f, 0.06f, 0.44f), new Vector3(-6f, 0f, 0f), carbon);   // rear wing
        Mats.Prim(PrimitiveType.Cube, stat, new Vector3(0f, 0.795f, 1.55f), new Vector3(0.14f, 0.012f, 1.3f), new Vector3(-12f, 0f, 0f), carbon); // hood stripe

        // ---- robot driver ----
        head = BuildRobot(body, stat, robot);

        MeshMerge.Merge(stat, true);

        // ---- wheels ----
        steer = new Transform[2];
        spin = new Transform[4];
        Material tyre = Mats.Lit(new Color(0.08f, 0.08f, 0.09f));
        Material rim = Mats.Steel(new Color(0.75f, 0.75f, 0.78f));
        for (int i = 0; i < 4; i++)
        {
            bool front = i < 2;
            float sx = (i % 2 == 0) ? -1f : 1f;
            float w = front ? 0.3f : 0.38f;
            var pivot = new GameObject(front ? "Steer" : "Axle").transform;
            pivot.SetParent(body, false);
            pivot.localPosition = new Vector3(sx * 0.93f, 0.37f, front ? 1.42f : -1.42f);
            var sp = new GameObject("Spin").transform;
            sp.SetParent(pivot, false);
            Mats.Prim(PrimitiveType.Cylinder, sp, Vector3.zero, new Vector3(0.74f, w * 0.5f, 0.74f), new Vector3(0f, 0f, 90f), tyre);
            Mats.Prim(PrimitiveType.Cylinder, sp, new Vector3(sx * w * 0.5f, 0f, 0f), new Vector3(0.5f, 0.02f, 0.5f), new Vector3(0f, 0f, 90f), rim);
            if (front) steer[i] = pivot;
            spin[i] = sp;
        }
    }

    static void Limb(Transform p, Vector3 a, Vector3 b, float th, Material m)
    {
        Vector3 d = b - a;
        var g = Mats.Prim(PrimitiveType.Cube, p, (a + b) * 0.5f, new Vector3(th, th, d.magnitude + th * 0.5f), m);
        g.transform.localRotation = Quaternion.LookRotation(d.normalized, Vector3.up);
    }

    // A simple seated robot holding the wheel. Static parts merge with the car; the head stays separate so it
    // can look into corners. Each robot from the roster has its own simple, recognisable look.
    static Transform BuildRobot(Transform body, Transform stat, int r)
    {
        Color bodyC; Material joint; float size = 1f;
        Material shiny = null;
        switch (r)
        {
            case 0: bodyC = new Color(0.88f, 0.88f, 0.88f); break;                 // Optimus  E1E1E1
            case 1: bodyC = new Color(0.17f, 0.18f, 0.22f); size = 0.86f; break;   // Unitree  2C2F38
            case 2: bodyC = new Color(0.76f, 0.74f, 0.70f); break;                 // Figure 03 (soft grey cover)
            case 3: bodyC = new Color(0.24f, 0.25f, 0.27f); break;                 // Figure 02 (dark)
            case 4: bodyC = new Color(0.53f, 0.53f, 0.53f); size = 1.3f; break;    // Big Figure Two 888888
            case 5: bodyC = new Color(0.82f, 0.82f, 0.82f); break;                 // Atlas HD D2D2D2
            default: bodyC = new Color(0.74f, 0.74f, 0.74f); break;                // Atlas electric BCBCBC
        }
        Material bm = r == 2 ? Mats.Lit(bodyC) : Mats.Shiny(bodyC);
        joint = Mats.Lit(r == 1 ? new Color(0.08f, 0.08f, 0.1f) : new Color(0.1f, 0.1f, 0.11f));
        shiny = Mats.Steel(new Color(0.05f, 0.05f, 0.06f));

        var rb = new GameObject("Robot").transform;
        rb.SetParent(stat, false);
        rb.localPosition = new Vector3(0f, 0.6f, -0.35f);
        rb.localScale = Vector3.one * size;
        float sw = r == 4 ? 0.62f : r == 1 ? 0.44f : r == 5 ? 0.42f : 0.5f;   // shoulder width

        // pelvis + torso
        Mats.Prim(PrimitiveType.Cube, rb, new Vector3(0f, 0.08f, 0f), new Vector3(0.34f, 0.16f, 0.26f), joint);
        Geo.Block(rb, bm, 0f, -0.13f, 0.13f, 0.3f, sw * 0.86f, 0.14f, 0.56f, 0.3f, sw * 0.86f, 0.14f, 0.56f);
        // chest detail per robot
        switch (r)
        {
            case 0: Mats.Prim(PrimitiveType.Cube, rb, new Vector3(0f, 0.42f, 0.135f), new Vector3(0.22f, 0.12f, 0.02f), joint); break;
            case 1: Mats.Prim(PrimitiveType.Cube, rb, new Vector3(0f, 0.44f, 0.135f), new Vector3(0.14f, 0.03f, 0.02f), Mats.Unlit(new Color(0.3f, 0.95f, 1f))); break;
            case 3: Mats.Prim(PrimitiveType.Cube, rb, new Vector3(0f, 0.3f, 0.135f), new Vector3(0.04f, 0.28f, 0.02f), Mats.Steel(new Color(0.7f, 0.7f, 0.72f))); break;
            case 4: Mats.Prim(PrimitiveType.Cube, rb, new Vector3(0f, 0.36f, 0.135f), new Vector3(0.36f, 0.2f, 0.03f), joint); break;
            case 6: Mats.Prim(PrimitiveType.Cube, rb, new Vector3(0f, 0.25f, 0f), new Vector3(0.31f, 0.06f, 0.27f), joint); break;
        }
        // shoulders, arms to the wheel
        Vector3 wheelC = new Vector3(0f, 0.4f, 0.52f);
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 sh = new Vector3(s * sw * 0.5f, 0.5f, 0f);
            Mats.Prim(PrimitiveType.Sphere, rb, sh, Vector3.one * (r == 4 ? 0.2f : 0.14f), r == 6 ? joint : bm);
            Vector3 elbow = new Vector3(s * (sw * 0.5f + 0.03f), 0.3f, 0.26f);
            Vector3 hand = wheelC + new Vector3(s * 0.17f, 0.04f, -0.04f);
            Limb(rb, sh, elbow, r == 4 ? 0.13f : 0.09f, bm);
            Limb(rb, elbow, hand, r == 4 ? 0.11f : 0.08f, r == 3 ? joint : bm);
            Mats.Prim(PrimitiveType.Sphere, rb, hand, Vector3.one * 0.08f, joint);
        }
        // steering wheel
        Mats.Prim(PrimitiveType.Cylinder, rb, wheelC, new Vector3(0.36f, 0.015f, 0.36f), new Vector3(70f, 0f, 0f), joint);
        Mats.Prim(PrimitiveType.Cylinder, rb, wheelC + new Vector3(0f, -0.1f, 0.12f), new Vector3(0.05f, 0.12f, 0.05f), new Vector3(70f, 0f, 0f), joint);
        // neck
        Mats.Prim(PrimitiveType.Cylinder, rb, new Vector3(0f, 0.6f, 0f), new Vector3(0.09f, 0.05f, 0.09f), joint);

        // head (separate, not merged)
        var head = new GameObject("Head").transform;
        head.SetParent(body, false);
        head.localPosition = rb.localPosition + new Vector3(0f, 0.74f, 0f) * size;
        head.localScale = Vector3.one * size;
        Material face = shiny;
        switch (r)
        {
            case 0:   // Optimus: rounded white head, full black visor
                Mats.Prim(PrimitiveType.Sphere, head, Vector3.zero, new Vector3(0.24f, 0.28f, 0.26f), bm);
                Mats.Prim(PrimitiveType.Sphere, head, new Vector3(0f, 0.01f, 0.06f), new Vector3(0.21f, 0.18f, 0.17f), face);
                break;
            case 1:   // Unitree: small dark block head, cyan eye strip
                Mats.Prim(PrimitiveType.Cube, head, Vector3.zero, new Vector3(0.22f, 0.19f, 0.24f), bm);
                Mats.Prim(PrimitiveType.Cube, head, new Vector3(0f, 0.02f, 0.122f), new Vector3(0.19f, 0.04f, 0.01f), Mats.Unlit(new Color(0.3f, 0.95f, 1f)));
                break;
            case 2:   // Figure 03: soft egg head with a black face screen
                Mats.Prim(PrimitiveType.Sphere, head, new Vector3(0f, 0.01f, 0f), new Vector3(0.24f, 0.3f, 0.25f), bm);
                Mats.Prim(PrimitiveType.Sphere, head, new Vector3(0f, 0.02f, 0.065f), new Vector3(0.2f, 0.2f, 0.14f), face);
                break;
            case 3:   // Figure 02: silver head, black face band with a white light line
                Mats.Prim(PrimitiveType.Sphere, head, Vector3.zero, new Vector3(0.23f, 0.27f, 0.25f), Mats.Steel(new Color(0.68f, 0.68f, 0.7f)));
                Mats.Prim(PrimitiveType.Cube, head, new Vector3(0f, 0.02f, 0.1f), new Vector3(0.2f, 0.09f, 0.06f), face);
                Mats.Prim(PrimitiveType.Cube, head, new Vector3(0f, 0.02f, 0.132f), new Vector3(0.15f, 0.015f, 0.01f), Mats.Unlit(Color.white));
                break;
            case 4:   // Big Figure Two: big boxy head, wide black visor
                Mats.Prim(PrimitiveType.Cube, head, Vector3.zero, new Vector3(0.3f, 0.27f, 0.3f), bm);
                Mats.Prim(PrimitiveType.Cube, head, new Vector3(0f, 0.02f, 0.15f), new Vector3(0.27f, 0.1f, 0.02f), face);
                break;
            case 5:   // Atlas HD: round lamp head, amber ring
                Mats.Prim(PrimitiveType.Cylinder, head, Vector3.zero, new Vector3(0.3f, 0.07f, 0.3f), new Vector3(90f, 0f, 0f), bm);
                Mats.Prim(PrimitiveType.Cylinder, head, new Vector3(0f, 0f, 0.07f), new Vector3(0.25f, 0.01f, 0.25f), new Vector3(90f, 0f, 0f), Mats.Unlit(new Color(1f, 0.72f, 0.2f)));
                Mats.Prim(PrimitiveType.Cylinder, head, new Vector3(0f, 0f, 0.08f), new Vector3(0.17f, 0.01f, 0.17f), new Vector3(90f, 0f, 0f), face);
                break;
            default:  // Atlas electric: round lamp head, white ring, blue eye
                Mats.Prim(PrimitiveType.Cylinder, head, Vector3.zero, new Vector3(0.3f, 0.07f, 0.3f), new Vector3(90f, 0f, 0f), bm);
                Mats.Prim(PrimitiveType.Cylinder, head, new Vector3(0f, 0f, 0.07f), new Vector3(0.25f, 0.01f, 0.25f), new Vector3(90f, 0f, 0f), Mats.Unlit(new Color(0.95f, 0.97f, 1f)));
                Mats.Prim(PrimitiveType.Cylinder, head, new Vector3(0f, 0f, 0.08f), new Vector3(0.17f, 0.01f, 0.17f), new Vector3(90f, 0f, 0f), joint);
                Mats.Prim(PrimitiveType.Cylinder, head, new Vector3(0f, 0f, 0.09f), new Vector3(0.06f, 0.01f, 0.06f), new Vector3(90f, 0f, 0f), Mats.Unlit(new Color(0.25f, 0.6f, 1f)));
                break;
        }
        return head;
    }
}
