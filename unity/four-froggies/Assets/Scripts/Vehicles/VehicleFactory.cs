using System.Collections.Generic;
using UnityEngine;

// Builds every rideable vehicle from primitives and configures its physics.
public static class VehicleFactory
{
    static Material M(Color c) { return Mats.Lit(c); }
    static readonly Color Tire = new Color(0.08f, 0.08f, 0.09f);
    static readonly Color Hub = new Color(0.55f, 0.56f, 0.58f);
    static readonly Color Dark = new Color(0.14f, 0.15f, 0.16f);

    static GameObject Box(Transform p, Vector3 pos, Vector3 size, Material m, Vector3 euler = default(Vector3))
    {
        return Mats.Prim(PrimitiveType.Cube, p, pos, size, euler, m);
    }

    // a flat slab joining two points in the local y/z plane
    static GameObject Slab(Transform p, float y1, float z1, float y2, float z2, float width, float thick, Material m)
    {
        float dy = y2 - y1, dz = z2 - z1;
        float len = Mathf.Sqrt(dy * dy + dz * dz);
        float ang = Mathf.Atan2(dy, dz) * Mathf.Rad2Deg;
        return Mats.Prim(PrimitiveType.Cube, p, new Vector3(0f, (y1 + y2) * 0.5f, (z1 + z2) * 0.5f), new Vector3(width, thick, len), new Vector3(-ang, 0f, 0f), m);
    }

    static Transform WheelVis(Transform p, Vector3 pos, float r, float width, bool chunky)
    {
        Transform n = Mats.Node(p, "Wheel", pos);
        Mats.Prim(PrimitiveType.Cylinder, n, Vector3.zero, new Vector3(r * 2f, width * 0.5f, r * 2f), new Vector3(0f, 0f, 90f), M(Tire));
        Mats.Prim(PrimitiveType.Cylinder, n, Vector3.zero, new Vector3(r * 1.1f, width * 0.52f, r * 1.1f), new Vector3(0f, 0f, 90f), Mats.Steel(Hub));
        if (chunky)
            for (int k = 0; k < 8; k++)
            {
                float a = k * 45f;
                GameObject lug = Mats.Prim(PrimitiveType.Cube, n, Quaternion.Euler(a, 0f, 0f) * new Vector3(0f, r * 0.98f, 0f), new Vector3(width * 1.02f, r * 0.12f, r * 0.35f), M(Tire));
                lug.transform.localRotation = Quaternion.Euler(a, 0f, 0f);
            }
        return n;
    }

    static T Root<T>(string name, Vector3 pos, float yaw) where T : Vehicle
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        var v = go.AddComponent<T>();
        v.Title = name;
        return v;
    }

    static void Done(Vehicle v)
    {
        Mats.SetLayer(v.gameObject, Vehicle.VehicleLayer);
    }

    // ---------------- Cybertruck ----------------
    public static GroundVehicle Cybertruck(string title, Vector3 pos, float yaw, Color accent)
    {
        var v = Root<GroundVehicle>(title, pos, yaw);
        v.EnterVerb = "drive the " + title;
        v.SetupBodyPublic(2300f, new Vector3(0f, 1.2f, 0f), new Vector3(2.15f, 1.1f, 5.7f), new Vector3(0f, 0.55f, 0f));
        v.maxSpeed = 30f; v.accel = 13f; v.turnRate = 1.7f; v.grip = 7.5f;
        Transform t = v.transform;
        Material steel = Mats.Steel(new Color(0.74f, 0.76f, 0.78f));
        Material black = M(new Color(0.07f, 0.07f, 0.08f));
        Box(t, new Vector3(0f, 0.98f, 0f), new Vector3(2.12f, 0.62f, 5.7f), steel);
        Slab(t, 1.29f, 2.85f, 1.36f, 1.3f, 2.08f, 0.08f, steel);                 // short hood
        Slab(t, 1.36f, 1.3f, 1.99f, -0.3f, 1.98f, 0.05f, Mats.Glass);           // flat windshield to the roof peak
        Slab(t, 1.99f, -0.3f, 1.62f, -1.35f, 2.04f, 0.08f, steel);             // roof falls to the bed
        Box(t, new Vector3(0f, 1.42f, -2.1f), new Vector3(2.06f, 0.3f, 1.5f), black);  // black tonneau
        for (int s = -1; s <= 1; s += 2)
        {
            Box(t, new Vector3(1.02f * s, 1.52f, 0.35f), new Vector3(0.04f, 0.42f, 1.6f), Mats.Glass);   // side glass
            Box(t, new Vector3(1.0f * s, 0.92f, 1.82f), new Vector3(0.18f, 0.4f, 1.25f), black);        // arches
            Box(t, new Vector3(1.0f * s, 0.92f, -1.75f), new Vector3(0.18f, 0.4f, 1.25f), black);
        }
        Box(t, new Vector3(0f, 1.3f, 0.3f), new Vector3(1.9f, 0.08f, 1.8f), M(Dark));     // cabin floor
        Box(t, new Vector3(0f, 1.24f, 2.86f), new Vector3(2.0f, 0.05f, 0.04f), Mats.Unlit(Color.white));
        Box(t, new Vector3(0f, 1.36f, -2.86f), new Vector3(2.0f, 0.06f, 0.04f), Mats.Unlit(new Color(1f, 0.1f, 0.1f)));
        Box(t, new Vector3(0f, 1.0f, 0f), new Vector3(2.14f, 0.06f, 4.0f), M(accent));   // froggy stripe
        float r = 0.48f;
        foreach (var w in new[] { new Vector3(-0.98f, r, 1.85f), new Vector3(0.98f, r, 1.85f), new Vector3(-0.98f, r, -1.75f), new Vector3(0.98f, r, -1.75f) })
            v.AddWheel(w, r, WheelVis(t, w, r, 0.4f, false), w.z > 0f);
        v.seat = Mats.Node(t, "Seat", new Vector3(-0.45f, 1.05f, 0.25f));
        v.seatScale = 0.66f;
        v.FinishSetup();
        Done(v);
        return v;
    }

    // ---------------- Monster truck ----------------
    public static GroundVehicle Monster(Vector3 pos, float yaw)
    {
        var v = Root<GroundVehicle>("Monster Truck", pos, yaw);
        v.EnterVerb = "drive the Monster Truck";
        v.rest = 0.65f;
        v.SetupBodyPublic(2600f, new Vector3(0f, 2.3f, 0f), new Vector3(2.4f, 1.5f, 4.8f), new Vector3(0f, 1.2f, 0f));
        v.maxSpeed = 26f; v.accel = 12f; v.turnRate = 1.6f; v.grip = 6f;
        v.camDistance = 13f; v.camHeight = 3.5f;
        Transform t = v.transform;
        Material paint = Mats.Shiny(Mats.Hex("#c3fc40"));
        Material black = M(new Color(0.07f, 0.07f, 0.08f));
        Box(t, new Vector3(0f, 1.95f, 0f), new Vector3(1.6f, 0.35f, 4.2f), black);              // chassis
        Box(t, new Vector3(0f, 2.45f, 0.3f), new Vector3(2.3f, 0.7f, 4.6f), paint);             // body
        Box(t, new Vector3(0f, 3.15f, -0.2f), new Vector3(2.1f, 0.75f, 1.8f), paint);           // cab
        Slab(t, 2.8f, 1.3f, 3.5f, 0.7f, 2.0f, 0.05f, Mats.Glass);                              // windshield
        Box(t, new Vector3(0f, 3.25f, -1.12f), new Vector3(1.9f, 0.45f, 0.04f), Mats.Glass);
        for (int s = -1; s <= 1; s += 2) Box(t, new Vector3(1.06f * s, 3.2f, -0.2f), new Vector3(0.04f, 0.4f, 1.4f), Mats.Glass);
        Box(t, new Vector3(0f, 3.58f, -0.3f), new Vector3(1.6f, 0.12f, 0.3f), Mats.Unlit(new Color(1f, 0.95f, 0.7f)));  // roof lights
        Box(t, new Vector3(0f, 2.3f, 2.66f), new Vector3(2.3f, 0.3f, 0.2f), Mats.Steel(Hub));    // bumper
        float r = 0.95f;
        foreach (var w in new[] { new Vector3(-1.4f, r, 1.75f), new Vector3(1.4f, r, 1.75f), new Vector3(-1.4f, r, -1.6f), new Vector3(1.4f, r, -1.6f) })
            v.AddWheel(w, r, WheelVis(t, w, r, 0.75f, true), w.z > 0f);
        v.seat = Mats.Node(t, "Seat", new Vector3(-0.45f, 2.75f, -0.2f));
        v.seatScale = 0.62f;
        v.FinishSetup();
        Done(v);
        return v;
    }

    static List<Transform> Tracks(Transform t, float x, float y, float len, float h, float w, Color col)
    {
        var blocks = new List<Transform>();
        Material m = M(Tire);
        for (int s = -1; s <= 1; s += 2)
        {
            Box(t, new Vector3(x * s, y, 0f), new Vector3(w, h, len), m);
            // road wheels
            int n = Mathf.RoundToInt(len / 1.1f);
            for (int i = 0; i < n; i++)
            {
                float z = -len * 0.5f + 0.55f + i * (len - 1.1f) / Mathf.Max(1, n - 1);
                Mats.Prim(PrimitiveType.Cylinder, t, new Vector3((x + w * 0.5f + 0.02f) * s, y - h * 0.12f, z), new Vector3(h * 0.7f, 0.04f, h * 0.7f), new Vector3(0f, 0f, 90f), Mats.Steel(Hub));
            }
            // moving tread cleats on top
            int cleats = Mathf.RoundToInt(len / 0.45f);
            for (int i = 0; i < cleats; i++)
            {
                GameObject c = Box(t, new Vector3(x * s, y + h * 0.5f + 0.03f, -len * 0.5f + i * len / cleats), new Vector3(w * 1.02f, 0.06f, 0.16f), M(col));
                blocks.Add(c.transform);
            }
        }
        return blocks;
    }

    // ---------------- Ripsaw ----------------
    public static GroundVehicle Ripsaw(Vector3 pos, float yaw)
    {
        var v = Root<GroundVehicle>("Ripsaw", pos, yaw);
        v.EnterVerb = "drive the Ripsaw";
        v.tracked = true;
        v.engineKind = 1;
        v.SetupBodyPublic(3200f, new Vector3(0f, 1.15f, 0f), new Vector3(3.3f, 1.0f, 5.4f), new Vector3(0f, 0.5f, 0f));
        v.maxSpeed = 30f; v.accel = 14f; v.turnRate = 2.1f; v.grip = 9f;
        Transform t = v.transform;
        Material paint = Mats.Shiny(Mats.Hex("#407d2b"));
        Material black = M(new Color(0.07f, 0.07f, 0.08f));
        Box(t, new Vector3(0f, 1.05f, -0.2f), new Vector3(2.1f, 0.7f, 4.4f), paint);
        Slab(t, 0.75f, 2.65f, 1.4f, 1.6f, 2.1f, 0.12f, paint);                 // glacis
        Box(t, new Vector3(0f, 1.48f, -1.6f), new Vector3(2.0f, 0.3f, 1.6f), black);       // engine deck
        for (int s = -1; s <= 1; s += 2)
        {
            Box(t, new Vector3(0.75f * s, 2.0f, 0.2f), new Vector3(0.08f, 1.0f, 0.08f), M(Dark), new Vector3(-15f, 0f, 0f));  // roll cage
            Box(t, new Vector3(0.75f * s, 2.0f, -0.9f), new Vector3(0.08f, 1.0f, 0.08f), M(Dark));
        }
        Box(t, new Vector3(0f, 2.5f, -0.35f), new Vector3(1.6f, 0.08f, 1.4f), M(Dark));
        Slab(t, 1.45f, 1.55f, 2.35f, 0.4f, 1.5f, 0.04f, Mats.Glass);
        Box(t, new Vector3(0f, 1.5f, 2.2f), new Vector3(1.6f, 0.06f, 0.05f), Mats.Unlit(Color.white));
        v.treadMarks = Tracks(t, 1.33f, 0.62f, 5.3f, 0.95f, 0.55f, new Color(0.18f, 0.18f, 0.18f));
        v.treadHalf = 2.65f;
        foreach (float z in new[] { 1.9f, 0f, -1.9f })
            for (int s = -1; s <= 1; s += 2) v.AddWheel(new Vector3(1.33f * s, 0.45f, z), 0.45f, null, false);
        v.seat = Mats.Node(t, "Seat", new Vector3(0f, 1.25f, -0.3f));
        v.seatScale = 0.7f;
        v.FinishSetup();
        Done(v);
        return v;
    }

    // ---------------- Tank ----------------
    public static Tank TankAt(Vector3 pos, float yaw)
    {
        var v = Root<Tank>("Tank", pos, yaw);
        v.EnterVerb = "command the Tank";
        v.tracked = true;
        v.engineKind = 1;
        v.usesTriggers = false;
        v.SetupBodyPublic(11000f, new Vector3(0f, 1.35f, 0f), new Vector3(4.5f, 1.2f, 6.5f), new Vector3(0f, 0.6f, 0f));
        v.maxSpeed = 13f; v.accel = 7f; v.turnRate = 1.1f; v.grip = 10f;
        v.camDistance = 14f; v.camHeight = 3.5f;
        Transform t = v.transform;
        Color olive = new Color(0.33f, 0.37f, 0.27f);
        Material paint = M(olive);
        Box(t, new Vector3(0f, 1.25f, 0f), new Vector3(3.3f, 0.9f, 6.0f), paint);
        Slab(t, 0.95f, 3.3f, 1.7f, 2.2f, 3.3f, 0.15f, paint);
        Box(t, new Vector3(0f, 1.72f, 0f), new Vector3(4.4f, 0.08f, 6.4f), M(Color.Lerp(olive, Color.black, 0.2f)));  // fenders
        v.treadMarks = Tracks(t, 1.95f, 0.68f, 6.4f, 1.1f, 0.7f, new Color(0.2f, 0.2f, 0.2f));
        v.treadHalf = 3.2f;
        foreach (float z in new[] { 2.4f, 0.8f, -0.8f, -2.4f })
            for (int s = -1; s <= 1; s += 2) v.AddWheel(new Vector3(1.95f * s, 0.5f, z), 0.5f, null, false);

        Transform tur = Mats.Node(t, "Turret", new Vector3(0f, 1.76f, -0.3f));
        Box(tur, new Vector3(0f, 0.4f, 0f), new Vector3(2.5f, 0.8f, 3.0f), paint);
        Box(tur, new Vector3(0f, 0.38f, 1.6f), new Vector3(1.4f, 0.65f, 0.4f), M(Color.Lerp(olive, Color.black, 0.25f)));
        Mats.Prim(PrimitiveType.Cylinder, tur, new Vector3(0.5f, 0.84f, -0.5f), new Vector3(0.9f, 0.06f, 0.9f), M(Dark));     // hatch ring
        Box(tur, new Vector3(-0.8f, 0.95f, -0.9f), new Vector3(0.05f, 0.9f, 0.05f), M(Dark));                                // antenna
        // missile pod on the right
        Box(tur, new Vector3(1.45f, 0.75f, 0f), new Vector3(0.45f, 0.45f, 1.3f), M(new Color(0.3f, 0.32f, 0.25f)));
        Transform bar = Mats.Node(tur, "Barrel", new Vector3(0f, 0.45f, 1.7f));
        Transform tube = Mats.Node(bar, "Tube", new Vector3(0f, 0f, 2.1f));
        Mats.Prim(PrimitiveType.Cylinder, tube, Vector3.zero, new Vector3(0.24f, 2.1f, 0.24f), new Vector3(90f, 0f, 0f), M(Color.Lerp(olive, Color.black, 0.35f)));
        Mats.Prim(PrimitiveType.Cylinder, tube, new Vector3(0f, 0f, 2.0f), new Vector3(0.36f, 0.2f, 0.36f), new Vector3(90f, 0f, 0f), M(Dark));
        Transform muzzle = Mats.Node(tube, "Muzzle", new Vector3(0f, 0f, 2.25f));
        v.turret = tur; v.barrel = bar; v.muzzle = muzzle;
        v.seat = Mats.Node(tur, "Seat", new Vector3(0.5f, 0.55f, -0.5f));
        v.seatScale = 0.66f;
        v.FinishSetup();
        Done(v);
        return v;
    }

    // ---------------- Optimus mech ----------------
    public static GroundVehicle Mech(Vector3 pos, float yaw)
    {
        var v = Root<GroundVehicle>("Optimus", pos, yaw);
        v.EnterVerb = "ride Optimus";
        v.tracked = true;
        v.engineKind = 5;
        v.rest = 0.9f;
        v.dustAmount = 0f;
        v.SetupBodyPublic(1400f, new Vector3(0f, 2.4f, 0f), new Vector3(1.4f, 2.0f, 1.0f), new Vector3(0f, 1.0f, 0f));
        v.maxSpeed = 7f; v.accel = 7f; v.turnRate = 1.6f; v.grip = 10f;
        v.camDistance = 10f; v.camHeight = 3.5f;
        Transform t = v.transform;
        Material white = Mats.Shiny(new Color(0.88f, 0.88f, 0.9f));
        Material black = Mats.Shiny(new Color(0.06f, 0.06f, 0.07f));
        Transform torso = Mats.Node(t, "Torso", new Vector3(0f, 2.3f, 0f));
        Box(torso, Vector3.zero, new Vector3(1.2f, 1.2f, 0.7f), white);
        Box(torso, new Vector3(0f, -0.75f, 0f), new Vector3(0.9f, 0.35f, 0.55f), black);
        Box(torso, new Vector3(0f, 0.15f, 0.36f), new Vector3(0.5f, 0.06f, 0.02f), Mats.Unlit(new Color(0.4f, 0.8f, 1f)));
        Transform head = Mats.Node(torso, "Head", new Vector3(0f, 0.95f, 0f));
        Box(head, Vector3.zero, new Vector3(0.5f, 0.55f, 0.55f), white);
        Box(head, new Vector3(0f, 0.02f, 0.28f), new Vector3(0.44f, 0.36f, 0.02f), black);
        var legs = new Transform[2];
        var arms = new Transform[2];
        for (int s = -1; s <= 1; s += 2)
        {
            Transform leg = Mats.Node(t, "Leg", new Vector3(0.32f * s, 1.7f, 0f));
            Box(leg, new Vector3(0f, -0.3f, 0f), new Vector3(0.32f, 0.65f, 0.36f), white);
            Box(leg, new Vector3(0f, -0.65f, 0.02f), new Vector3(0.26f, 0.2f, 0.3f), black);
            Box(leg, new Vector3(0f, -0.95f, 0f), new Vector3(0.28f, 0.55f, 0.32f), white);
            Box(leg, new Vector3(0f, -1.25f, 0.08f), new Vector3(0.34f, 0.12f, 0.55f), black);
            legs[s < 0 ? 0 : 1] = leg;
            Transform arm = Mats.Node(torso, "Arm", new Vector3(0.75f * s, 0.45f, 0f));
            Box(arm, new Vector3(0f, -0.35f, 0f), new Vector3(0.24f, 0.7f, 0.26f), white);
            Box(arm, new Vector3(0f, -0.85f, 0.05f), new Vector3(0.2f, 0.45f, 0.22f), black);
            arms[s < 0 ? 0 : 1] = arm;
        }
        float phase = 0f;
        v.animate = (fs, dt) =>
        {
            float sp = Mathf.Clamp(fs / 6f, -1f, 1f);
            phase += dt * 7f * Mathf.Abs(sp);
            float a = Mathf.Sin(phase) * 32f * Mathf.Abs(sp);
            legs[0].localRotation = Quaternion.Euler(a, 0f, 0f);
            legs[1].localRotation = Quaternion.Euler(-a, 0f, 0f);
            arms[0].localRotation = Quaternion.Euler(-a * 0.7f, 0f, 0f);
            arms[1].localRotation = Quaternion.Euler(a * 0.7f, 0f, 0f);
            torso.localPosition = new Vector3(0f, 2.3f + Mathf.Abs(Mathf.Sin(phase)) * 0.06f, 0f);
        };
        foreach (var w in new[] { new Vector3(-0.45f, 0.3f, 0.35f), new Vector3(0.45f, 0.3f, 0.35f), new Vector3(-0.45f, 0.3f, -0.35f), new Vector3(0.45f, 0.3f, -0.35f) })
            v.AddWheel(w, 0.3f, null, false);
        v.seat = Mats.Node(torso, "Seat", new Vector3(0f, 0.6f, -0.62f));
        v.seatScale = 0.6f;
        v.FinishSetup();
        Done(v);
        return v;
    }

    // ---------------- Helicopter ----------------
    public static Flyer Helicopter(Vector3 pos, float yaw)
    {
        var v = Root<Flyer>("Helicopter", pos, yaw);
        v.EnterVerb = "fly the Helicopter";
        v.InitFlyer(1500f, new Vector3(0f, 1.35f, 0.4f), new Vector3(2.2f, 1.9f, 4.4f));
        v.maxSpeed = 24f; v.climbSpeed = 8f; v.turnRate = 75f;
        v.camDistance = 15f; v.camHeight = 4f;
        Transform t = v.transform;
        Material white = Mats.Shiny(Mats.Hex("#f2f2f2"));
        Material stripe = Mats.Shiny(new Color(0.15f, 0.55f, 0.25f));
        Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 1.45f, 0.3f), new Vector3(2.2f, 1.95f, 3.8f), white);
        Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 1.45f, 1.45f), new Vector3(1.9f, 1.55f, 1.9f), Mats.Glass);
        Box(t, new Vector3(0f, 1.0f, 0.2f), new Vector3(2.0f, 0.12f, 3.0f), stripe);
        Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 1.75f, -3.1f), new Vector3(0.45f, 2.4f, 0.45f), new Vector3(90f, 0f, 0f), white);
        Box(t, new Vector3(0f, 2.4f, -5.3f), new Vector3(0.12f, 1.4f, 0.8f), stripe, new Vector3(-20f, 0f, 0f));
        Box(t, new Vector3(0f, 1.8f, -5.0f), new Vector3(1.6f, 0.08f, 0.5f), white);
        for (int s = -1; s <= 1; s += 2)
        {
            Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(0.95f * s, 0.1f, 0.3f), new Vector3(0.12f, 1.9f, 0.12f), new Vector3(90f, 0f, 0f), M(Dark));
            Box(t, new Vector3(0.8f * s, 0.45f, 1.0f), new Vector3(0.08f, 0.75f, 0.08f), M(Dark), new Vector3(0f, 0f, 20f * s));
            Box(t, new Vector3(0.8f * s, 0.45f, -0.4f), new Vector3(0.08f, 0.75f, 0.08f), M(Dark), new Vector3(0f, 0f, 20f * s));
        }
        Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 2.6f, 0.3f), new Vector3(0.25f, 0.25f, 0.25f), M(Dark));
        Transform rotor = Mats.Node(t, "Rotor", new Vector3(0f, 2.85f, 0.3f));
        Box(rotor, Vector3.zero, new Vector3(9.5f, 0.05f, 0.32f), M(Dark));
        Box(rotor, Vector3.zero, new Vector3(0.32f, 0.05f, 9.5f), M(Dark));
        v.rotors.Add(rotor); v.rotorAxes.Add(Vector3.up);
        Transform tail = Mats.Node(t, "TailRotor", new Vector3(0.18f, 2.2f, -5.5f));
        Box(tail, Vector3.zero, new Vector3(0.04f, 1.5f, 0.16f), M(Dark));
        Box(tail, Vector3.zero, new Vector3(0.04f, 0.16f, 1.5f), M(Dark));
        v.tailRotor = tail;
        v.ExtraBoxPublic(new Vector3(0f, 0.15f, 0.3f), new Vector3(2.1f, 0.3f, 3.4f));
        v.ExtraBoxPublic(new Vector3(0f, 1.9f, -3.6f), new Vector3(0.6f, 0.7f, 3.6f));
        v.seat = Mats.Node(t, "Seat", new Vector3(0f, 0.95f, 1.0f));
        v.seatScale = 0.72f;
        Done(v);
        return v;
    }

    // ---------------- Passenger drone ----------------
    public static Flyer Drone(Vector3 pos, float yaw)
    {
        var v = Root<Flyer>("Passenger Drone", pos, yaw);
        v.EnterVerb = "board the Passenger Drone";
        v.isDrone = true;
        v.InitFlyer(650f, new Vector3(0f, 1.05f, 0f), new Vector3(1.7f, 1.5f, 2.0f));
        v.maxSpeed = 18f; v.climbSpeed = 8.5f; v.turnRate = 140f;
        v.camDistance = 11f; v.camHeight = 3f;
        Transform t = v.transform;
        Material white = Mats.Shiny(new Color(0.95f, 0.95f, 0.96f));
        Material black = Mats.Shiny(new Color(0.08f, 0.08f, 0.1f));
        Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 1.1f, -0.1f), new Vector3(1.7f, 1.5f, 1.9f), white);
        Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 1.25f, 0.45f), new Vector3(1.45f, 1.15f, 1.2f), Mats.Glass);
        for (int k = 0; k < 4; k++)
        {
            float a = 45f + k * 90f;
            Vector3 d = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
            Box(t, new Vector3(0f, 1.55f, 0f) + d * 1.05f, new Vector3(0.14f, 0.1f, 2.1f), black, new Vector3(0f, a, 0f));
            Vector3 hubP = new Vector3(0f, 1.6f, 0f) + d * 2.05f;
            Mats.Prim(PrimitiveType.Cylinder, t, hubP, new Vector3(0.22f, 0.15f, 0.22f), black);
            for (int lvl = 0; lvl < 2; lvl++)
            {
                Transform rot = Mats.Node(t, "Rotor", hubP + Vector3.up * (lvl == 0 ? 0.18f : -0.18f));
                Box(rot, Vector3.zero, new Vector3(1.7f, 0.03f, 0.16f), M(Dark));
                v.rotors.Add(rot); v.rotorAxes.Add(Vector3.up);
            }
            Mats.Prim(PrimitiveType.Cylinder, t, hubP + Vector3.up * 0.0f, new Vector3(1.9f, 0.01f, 1.9f), Mats.Lit(new Color(0.2f, 0.9f, 0.4f)));
        }
        for (int s = -1; s <= 1; s += 2)
        {
            Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(0.7f * s, 0.06f, 0f), new Vector3(0.1f, 0.9f, 0.1f), new Vector3(90f, 0f, 0f), black);
            Box(t, new Vector3(0.62f * s, 0.32f, 0.4f), new Vector3(0.06f, 0.55f, 0.06f), black);
            Box(t, new Vector3(0.62f * s, 0.32f, -0.4f), new Vector3(0.06f, 0.55f, 0.06f), black);
        }
        v.ExtraBoxPublic(new Vector3(0f, 0.1f, 0f), new Vector3(1.5f, 0.2f, 1.8f));
        v.seat = Mats.Node(t, "Seat", new Vector3(0f, 0.6f, 0.2f));
        v.seatScale = 0.7f;
        Done(v);
        return v;
    }

    // ---------------- Boat ----------------
    public static Boat BoatAt(Vector3 pos, float yaw)
    {
        var v = Root<Boat>("Boat", pos, yaw);
        v.EnterVerb = "take the Boat";
        v.engineKind = 4;
        v.SetupBodyPublic(700f, new Vector3(0f, 0.5f, -0.2f), new Vector3(2.2f, 0.8f, 5.0f), new Vector3(0f, 0.15f, 0f));
        v.rb.drag = 0.1f;
        v.rb.angularDrag = 2f;
        v.camDistance = 12f; v.camHeight = 3f;
        Transform t = v.transform;
        Material hull = Mats.Shiny(new Color(0.12f, 0.4f, 0.75f));
        Material white = Mats.Shiny(new Color(0.95f, 0.95f, 0.95f));
        Box(t, new Vector3(0f, 0.38f, -0.5f), new Vector3(2.1f, 0.62f, 4.0f), hull);
        Box(t, new Vector3(0f, 0.38f, 1.5f), new Vector3(1.48f, 0.62f, 1.48f), hull, new Vector3(0f, 45f, 0f));
        Box(t, new Vector3(0f, 0.7f, -0.5f), new Vector3(1.98f, 0.05f, 3.9f), white);
        Box(t, new Vector3(0f, 0.7f, 1.5f), new Vector3(1.38f, 0.05f, 1.38f), white, new Vector3(0f, 45f, 0f));
        Box(t, new Vector3(0f, 0.95f, 0.5f), new Vector3(1.0f, 0.5f, 0.5f), white);
        Slab(t, 1.2f, 0.75f, 1.6f, 0.45f, 1.0f, 0.04f, Mats.Glass);
        Box(t, new Vector3(0f, 0.6f, -2.7f), new Vector3(0.45f, 0.9f, 0.5f), M(Dark));
        Transform prop = Mats.Node(t, "Prop", new Vector3(0f, 0.05f, -2.75f));
        Box(prop, Vector3.zero, new Vector3(0.6f, 0.08f, 0.04f), Mats.Steel(Hub));
        Box(prop, Vector3.zero, new Vector3(0.08f, 0.6f, 0.04f), Mats.Steel(Hub));
        v.prop = prop;
        v.seat = Mats.Node(t, "Seat", new Vector3(0f, 0.72f, -0.4f));
        v.seatScale = 0.72f;
        Done(v);
        return v;
    }
}
