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
    // short real-world look line per robot (lobby card); the models in RobotModel.cs follow these
    public static readonly string[] Looks = {
        "Tesla Optimus: white panels, black face plate",
        "Unitree H1: charcoal, thin legs, lidar head",
        "Figure 03: soft knit cover, black face plate",
        "Figure 02: matte black, silver head, black visor",
        "Figure 02, super-sized (x1.35)",
        "hydraulic Atlas: bulky, blue-grey, sensor head",
        "electric Atlas: sleek grey, ring-light face" };
    public static readonly string[] Short = { "Optimus", "Unitree", "Figure 03", "Figure 02", "Big Fig Two", "Atlas HD", "Atlas e" };
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
        Material paint = Mats.Paint(cs.color, car == 7 ? 0.95f : 0.9f);
        Material carbon = Mats.Lit(new Color(0.07f, 0.07f, 0.08f));
        Material glass = Mats.Glass;
        Material lamp = Mats.Unlit(new Color(1f, 0.98f, 0.9f));
        Material tail = Mats.Unlit(new Color(1f, 0.12f, 0.1f));
        Material tailGlow = Mats.Unlit(new Color(1f, 0.32f, 0.26f));

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

        // full-width glowing taillight bar + front / side intakes
        Mats.Prim(PrimitiveType.Cube, stat, new Vector3(0f, 0.735f, -2.275f), new Vector3(1.62f, 0.035f, 0.04f), tailGlow);
        Mats.Prim(PrimitiveType.Cube, stat, new Vector3(0f, 0.7f, -2.27f), new Vector3(1.7f, 0.012f, 0.05f), carbon);
        for (int s = -1; s <= 1; s += 2)
        {
            var fi = Mats.Prim(PrimitiveType.Cube, stat, new Vector3(s * 0.62f, 0.3f, 2.2f), new Vector3(0.5f, 0.13f, 0.12f), carbon);   // front corner intake
            fi.transform.localRotation = Quaternion.Euler(-14f, s * 12f, s * 8f);
            Geo.Block(stat, carbon, s * 0.96f, -1.05f, -0.15f, 0.04f, 0.04f, 0.32f, 0.66f, 0.02f, 0.02f, 0.4f, 0.52f);   // big side scoop
        }
        Mats.Prim(PrimitiveType.Cube, stat, new Vector3(0f, 0.27f, 2.32f), new Vector3(0.5f, 0.09f, 0.1f), new Vector3(-12f, 0f, 0f), carbon);   // centre intake

        // ---- robot driver ----
        head = robot >= 0 ? RobotModel.Build(stat, body, new Vector3(0f, 0.6f, -0.35f), robot, true) : null;

        MeshMerge.Merge(stat, true, false);
        if (head != null) MeshMerge.Merge(head, true, false);

        // ---- wheels ----
        steer = new Transform[2];
        spin = new Transform[4];
        Material tyre = Mats.Lit(new Color(0.08f, 0.08f, 0.09f));
        Material rim = Mats.Steel(new Color(0.78f, 0.78f, 0.8f));
        Material dark = Mats.Steel(new Color(0.12f, 0.12f, 0.13f));
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
            float o = sx * w * 0.5f;
            Mats.Prim(PrimitiveType.Cylinder, sp, new Vector3(o, 0f, 0f), new Vector3(0.58f, 0.012f, 0.58f), new Vector3(0f, 0f, 90f), rim);              // rim lip
            Mats.Prim(PrimitiveType.Cylinder, sp, new Vector3(o + sx * 0.006f, 0f, 0f), new Vector3(0.5f, 0.012f, 0.5f), new Vector3(0f, 0f, 90f), dark);  // barrel
            for (int k = 0; k < 5; k++)                                                                                                                      // 10 spokes
                Mats.Prim(PrimitiveType.Cube, sp, new Vector3(o + sx * 0.018f, 0f, 0f), new Vector3(0.02f, 0.5f, 0.05f), new Vector3(k * 36f, 0f, 0f), rim);
            Mats.Prim(PrimitiveType.Cylinder, sp, new Vector3(o + sx * 0.03f, 0f, 0f), new Vector3(0.11f, 0.012f, 0.11f), new Vector3(0f, 0f, 90f), dark);  // centre cap
            MeshMerge.Merge(sp, true, false);
            if (front) steer[i] = pivot;
            spin[i] = sp;
        }
    }
}
