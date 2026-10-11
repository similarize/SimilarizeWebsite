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

    // ffu9 vehicle packs (Resources/LB/<name>.bytes, built by work/lb-gfx/ff/build_*.py): spawn a part so that the
    // returned node sits at the part's pivot under `parent` (unscaled parent, pack in vehicle space)
    static Transform Part(LBPack pk, string part, Transform parent, Vector3 parentPivot, Color tint, bool mirror = false)
    {
        return pk.Spawn(part, parent, -parentPivot, 1f, tint, mirror);
    }

    // wheel visual node at `pos` with the pack part "wheel" (or another part) under it; left side mirrored
    static Transform PackWheel(LBPack pk, Transform t, Vector3 pos, Color tint, string part = "wheel")
    {
        Transform wn = Mats.Node(t, "Wheel", pos);
        pk.Spawn(part, wn, Vector3.zero, 1f, tint, pos.x < 0f);
        return wn;
    }

    // per-instance copies of a part's materials (track belts scroll their texture independently)
    static Material InstanceMat(Transform part)
    {
        if (part == null) return null;
        var r = part.GetComponentInChildren<MeshRenderer>();
        if (r == null) return null;
        var m = new Material(r.sharedMaterial);
        foreach (var rr in part.GetComponentsInChildren<MeshRenderer>()) rr.sharedMaterial = m;
        return m;
    }

    // Ripsaw running gear shared by the EV2 and the M5: 12 sprung road wheels, sprockets, idlers, rollers and two
    // belts whose texture scrolls with each side's ground speed (skid steer: the outer track runs faster)
    public const float RipTX = 1.22f, RipTilesPerM = 2.9357f;
    static readonly float[] RipWZ = { -1.75f, -1.05f, -0.35f, 0.35f, 1.05f, 1.75f };
    static void RipsawGear(GroundVehicle v, LBPack pk, Color tint)
    {
        Transform t = v.transform;
        pk.Spawn("body", t, Vector3.zero, 1f, tint);
        Transform bl = pk.Spawn("beltL", t, Vector3.zero, 1f, tint), br = pk.Spawn("beltR", t, Vector3.zero, 1f, tint);
        Material ml = InstanceMat(bl), mr = InstanceMat(br);
        foreach (float z in RipWZ)
            for (int sx = -1; sx <= 1; sx += 2)
            {
                Vector3 w = new Vector3(RipTX * sx, 0.36f, z);
                v.AddWheel(w, 0.36f, PackWheel(pk, t, w, tint), false);
            }
        var spin = new List<Transform>();
        for (int sx = -1; sx <= 1; sx += 2)
        {
            spin.Add(PackWheel(pk, t, new Vector3(RipTX * sx, 0.62f, -2.45f), tint, "sprocket"));
            spin.Add(PackWheel(pk, t, new Vector3(RipTX * sx, 0.72f, 2.42f), tint, "idler"));
            PackWheel(pk, t, new Vector3(RipTX * sx, 0.98f, -0.85f), tint, "roller");
            PackWheel(pk, t, new Vector3(RipTX * sx, 0.99f, 0.75f), tint, "roller");
        }
        float offL = 0f, offR = 0f, angL = 0f, angR = 0f;
        v.treadMarks = null;
        v.animate = (fs, dt) =>
        {
            float w = v.rb != null ? Vector3.Dot(v.rb.angularVelocity, v.transform.up) : 0f;
            float vl = fs + w * RipTX, vr = fs - w * RipTX;
            offL = Mathf.Repeat(offL - vl * dt * RipTilesPerM, 1f); offR = Mathf.Repeat(offR - vr * dt * RipTilesPerM, 1f);
            if (ml != null) ml.mainTextureOffset = new Vector2(0f, offL);
            if (mr != null) mr.mainTextureOffset = new Vector2(0f, offR);
            angL += vl / 0.38f * Mathf.Rad2Deg * dt; angR += vr / 0.38f * Mathf.Rad2Deg * dt;
            for (int i = 0; i < spin.Count; i++) spin[i].localRotation = Quaternion.Euler(i < 2 ? angL : angR, 0f, 0f);
        };
    }

    // ---------------- Cybertruck ----------------
    public static GroundVehicle Cybertruck(string title, Vector3 pos, float yaw, Color accent)
    {
        var v = Root<GroundVehicle>(title, pos, yaw);
        v.EnterVerb = "drive the " + title;
        v.engineKind = 7;   // electric whine
        v.SetupBodyPublic(2300f, new Vector3(0f, 1.2f, 0f), new Vector3(2.15f, 1.1f, 5.7f), new Vector3(0f, 0.55f, 0f));
        v.maxSpeed = 30f; v.accel = 13f; v.turnRate = 1.7f; v.grip = 7.5f;
        Transform t = v.transform;
        float r = 0.48f;
        // graphics overhaul: faceted stainless body + glass greenhouse + aero wheels (Resources/LB/cybertruck.bytes)
        LBPack pk = LBPack.Get("cybertruck");
        if (pk != null && pk.Has("body") && pk.Has("wheel"))
        {
            pk.Spawn("body", t, Vector3.zero, 1f, accent);
            foreach (var w in new[] { new Vector3(-0.98f, r, 1.85f), new Vector3(0.98f, r, 1.85f), new Vector3(-0.98f, r, -1.75f), new Vector3(0.98f, r, -1.75f) })
            {
                Transform wn = Mats.Node(t, "Wheel", w);
                pk.Spawn("wheel", wn, Vector3.zero, 1f, accent, w.x < 0f);
                v.AddWheel(w, r, wn, w.z > 0f);
            }
        }
        else
        {
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
        foreach (var w in new[] { new Vector3(-0.98f, r, 1.85f), new Vector3(0.98f, r, 1.85f), new Vector3(-0.98f, r, -1.75f), new Vector3(0.98f, r, -1.75f) })
            v.AddWheel(w, r, WheelVis(t, w, r, 0.4f, false), w.z > 0f);
        }
        if (pk != null && pk.Has("body") && pk.Has("wheel"))
        {
            // mesh cabin: cushion top y 1.46 (z -0.33..0.23), backrest front z -0.26, roof glass ~1.89 above the frog's eyes.
            // A 0.4 frog (0.46 m tall, 0.39 m deep) sits on the cushion clear of the backrest and the glass.
            v.seat = Mats.Node(t, "Seat", new Vector3(-0.45f, 1.40f, 0f));
            v.seatScale = 0.4f;
        }
        else
        {
            v.seat = Mats.Node(t, "Seat", new Vector3(-0.45f, 1.3f, -0.08f));
            v.seatScale = 0.55f;
        }
        v.FinishSetup();
        CyberBoat.Attach(v);     // ffu11: drives into the pond -> transforms into the Cyberboat
        Done(v);
        return v;
    }

    // ---------------- Monster truck ----------------
    public static GroundVehicle Monster(Vector3 pos, float yaw)
    {
        var v = Root<GroundVehicle>("Monster Truck", pos, yaw);
        v.EnterVerb = "drive the Monster Truck";
        v.engineKind = 8;   // supercharged V8
        v.rest = 0.65f;
        v.SetupBodyPublic(2600f, new Vector3(0f, 2.3f, 0f), new Vector3(2.4f, 1.5f, 4.8f), new Vector3(0f, 1.2f, 0f));
        v.maxSpeed = 26f; v.accel = 12f; v.turnRate = 1.6f; v.grip = 6f;
        v.camDistance = 13f; v.camHeight = 3.5f;
        Transform t = v.transform;
        LBPack pk = LBPack.Get("monster");
        float r = 0.95f;
        if (pk != null && pk.Has("body") && pk.Has("wheel"))
        {
            Color lime = Mats.Hex("#c3fc40");
            pk.Spawn("body", t, Vector3.zero, 1f, lime);
            foreach (var w in new[] { new Vector3(-1.4f, r, 1.75f), new Vector3(1.4f, r, 1.75f), new Vector3(-1.4f, r, -1.6f), new Vector3(1.4f, r, -1.6f) })
                v.AddWheel(w, r, PackWheel(pk, t, w, lime), w.z > 0f);
            v.seat = Mats.Node(t, "Seat", new Vector3(-0.45f, 2.9f, -0.2f));
            v.seatScale = 0.6f;
            v.FinishSetup();
            Done(v);
            return v;
        }
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
        LBPack pk = LBPack.Get("ripsaw");
        if (pk != null && pk.Has("body") && pk.Has("beltL"))
        {
            // Howe & Howe Ripsaw EV2 look: graphite paint, glass canopy, two seats (frog in the left one)
            v.Title = "Ripsaw EV2"; v.EnterVerb = "drive the Ripsaw EV2";
            v.maxSpeed = 28f; v.accel = 13f;
            RipsawGear(v, pk, new Color(0.13f, 0.14f, 0.15f));
            v.seat = Mats.Node(t, "Seat", new Vector3(-0.42f, 1.16f, -0.3f));
            v.seatScale = 0.6f;
            v.camDistance = 12f; v.camHeight = 3.2f;
            v.FinishSetup();
            Done(v);
            return v;
        }
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
        LBPack pk = LBPack.Get("ripsaw_m5");
        if (pk != null && pk.Has("body") && pk.Has("turret") && pk.Has("barrel"))
        {
            // ffu9: the tank is now a Ripsaw M5 (armed Ripsaw): same shells (RT) + missiles (RB / LT), faster on its tracks
            v.Title = "Ripsaw M5"; v.EnterVerb = "command the Ripsaw M5";
            v.body.center = new Vector3(0f, 1.15f, 0f); v.body.size = new Vector3(3.3f, 1.0f, 5.4f);
            v.rb.mass = 7000f; v.rb.centerOfMass = new Vector3(0f, 0.55f, 0f);
            v.maxSpeed = 21f; v.accel = 10f; v.turnRate = 1.5f; v.grip = 9.5f;
            v.camDistance = 12.5f; v.camHeight = 3.6f;
            RipsawGear(v, pk, Color.white);
            v.engineKind = 1; v.enginePitch = 0.82f;
            Vector3 T0 = pk.parts["turret"].pivot, B0 = pk.parts["barrel"].pivot;
            Transform mTur = Mats.Node(t, "Turret", T0);
            Part(pk, "turret", mTur, T0, Color.white);
            Transform mBar = Mats.Node(mTur, "Barrel", B0 - T0);
            Transform mTube = Mats.Node(mBar, "Tube", new Vector3(0f, 0f, 2.1f));     // Tank.Update slides child 0 back on recoil
            pk.Spawn("barrel", mTube, -B0 - new Vector3(0f, 0f, 2.1f), 1f, Color.white);
            Transform mMuz = Mats.Node(mTube, "Muzzle", new Vector3(0f, 0f, -2.1f + 1.85f));
            v.turret = mTur; v.barrel = mBar; v.muzzle = mMuz;
            v.recoilDist = 0.25f;
            v.missileOffset = new Vector3(0.62f, 0.32f, 0.5f);
            v.seat = Mats.Node(t, "Seat", new Vector3(-0.42f, 1.16f, -0.3f));
            v.seatScale = 0.6f;
            v.FinishSetup();
            Done(v);
            return v;
        }
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
        LBPack opk = LBPack.Get("sr_optimus");
        if (opk != null && opk.Has("body") && opk.Has("thighL") && opk.Has("uarmL") && opk.Has("head"))
        {
            // ffu9: the rideable suit uses the Optimus Gen 2 standing mesh at 3.3 m; the frog rides on its shoulders
            float hh = 3.3f, sc = hh / Mathf.Max(0.5f, opk.Height());
            Color wc = new Color(0.92f, 0.92f, 0.93f);
            Transform bodyN = Mats.Node(t, "Body", Vector3.zero);
            opk.Spawn("body", bodyN, Vector3.zero, sc, wc);
            opk.Spawn("head", bodyN, Vector3.zero, sc, wc);
            Transform[] lg = { SuitLimb(opk, bodyN, "thighL", "shinL", sc, wc), SuitLimb(opk, bodyN, "thighR", "shinR", sc, wc) };
            Transform[] am = { SuitLimb(opk, bodyN, "uarmL", "farmL", sc, wc), SuitLimb(opk, bodyN, "uarmR", "farmR", sc, wc) };
            Transform[] kn = { lg[0].Find("LB shinL"), lg[1].Find("LB shinR") };
            // ffu28 sinking fix. Root cause: the ffu9 mesh has its soles at the root (y 0) but the four spring rays were
            // set up for the old box robot (feet 0.4 m up): mount y 1.2, reach 1.2, resting 0.4 m compressed -> the root (and
            // the soles) sat 0.4 m under the ground, and any hard drop compressed the springs past their 1.2 m origin: the
            // rays then started UNDER the terrain, missed, the springs and the drive force switched off (drive scales with
            // wheels-on-ground) and the suit sank onto its body box (bottom 1.4 m up) = buried to the knees and stuck for good.
            // Now: ray origins 1.5 m up (inside the hips), reach 1.9 m, resting compression puts the soles exactly on the
            // surface; walker = bump stop + upright assist + lift back out if it is ever under the terrain (GroundVehicle).
            float hipH = opk.parts["thighL"].pivot.y * sc;   // hip pivot above the soles (1.8 m)
            float footLocal = -opk.parts["thighL"].pivot.y;  // the sole in thigh space (pack units; the thigh node carries sc)
            float ph = 0f, lift = 0f, airT = 0f, minVy = 0f; int lastStrike = int.MinValue;
            v.walker = true; v.syncedSteps = true;
            v.animate = (fs, dt) =>
            {
                float sp = Mathf.Clamp(fs / 6f, -1f, 1f), asp = Mathf.Abs(sp);
                ph += dt * 7f * asp;
                float s0 = Mathf.Sin(ph), c0 = Mathf.Cos(ph);
                float a = s0 * 30f * asp;                       // + = leg 0 swings back, leg 1 forward
                lg[0].localRotation = Quaternion.Euler(a, 0f, 0f);
                lg[1].localRotation = Quaternion.Euler(-a, 0f, 0f);
                // the leg swinging forward bends its knee (shin folds back) so it clears the ground; straight at the strike
                if (kn[0] != null) kn[0].localRotation = Quaternion.Euler(Mathf.Max(0f, -c0) * 55f * asp, 0f, 0f);
                if (kn[1] != null) kn[1].localRotation = Quaternion.Euler(Mathf.Max(0f, c0) * 55f * asp, 0f, 0f);
                am[0].localRotation = Quaternion.Euler(-a * 0.7f, 0f, 0f);
                am[1].localRotation = Quaternion.Euler(a * 0.7f, 0f, 0f);
                // straight stance legs reach hipH*cos(a) below the hip: drop the body by the difference (feet stay down)
                float drop = hipH * (1f - Mathf.Cos(a * Mathf.Deg2Rad));
                bodyN.localPosition = new Vector3(0f, -drop + lift, 0f);
                // slopes: raise the body until neither foot is under the surface (eased, max 0.6 m)
                float pen = -9f;
                for (int i = 0; i < 2; i++)
                {
                    Vector3 fw = lg[i].TransformPoint(new Vector3(0f, footLocal, 0f));
                    RaycastHit h;
                    if (Physics.Raycast(fw + Vector3.up * 1.4f, Vector3.down, out h, 2.8f, Vehicle.GroundMask, QueryTriggerInteraction.Ignore))
                        pen = Mathf.Max(pen, h.point.y - fw.y);
                }
                float wantLift = pen > -8f ? Mathf.Clamp(lift + pen, 0f, 0.6f) : 0f;
                lift = Mathf.Lerp(lift, wantLift, Mathf.Min(1f, dt * 12f));
                // footfall at the heel strike: leg 1 forward at sin = +1, leg 0 forward at sin = -1
                int strike = Mathf.FloorToInt((ph - Mathf.PI * 0.5f) / Mathf.PI);
                if (lastStrike == int.MinValue) lastStrike = strike;
                int gcount = 0; foreach (var w in v.wheels) if (w.grounded) gcount++;
                if (strike != lastStrike)
                {
                    lastStrike = strike;
                    if (asp > 0.15f && gcount > 0 && v.driver != null)
                    {
                        int leg = (strike & 1) == 0 ? 1 : 0;
                        Vector3 foot = lg[leg].TransformPoint(new Vector3(0f, footLocal, 0f));
                        RaycastHit h;
                        if (Physics.Raycast(foot + Vector3.up * 1.4f, Vector3.down, out h, 2.8f, Vehicle.GroundMask, QueryTriggerInteraction.Ignore)) foot = h.point;
                        MechStomp.Foot(foot, hh, 0.6f + asp * 0.4f);
                        if (Sfx.StepMetal != null) Sfx.PlayAt(Sfx.StepMetal, foot, 0.3f, 40f, Random.Range(0.8f, 0.95f));
                        if (Random.value < 0.3f) Sfx.PlayAt(Sfx.Pick(Sfx.Servo), foot, 0.15f, 40f, Random.Range(0.9f, 1.2f));
                    }
                }
                // landing from a fall / jump: both feet slam down
                float vy = v.rb != null ? v.rb.velocity.y : 0f;
                if (gcount == 0) { airT += dt; minVy = Mathf.Min(minVy, vy); }
                else
                {
                    if (airT > 0.25f && minVy < -3f) MechStomp.Foot(v.transform.position, hh, Mathf.Clamp(1f + (-minVy - 3f) * 0.12f, 1f, 2.2f));
                    airT = 0f; minVy = 0f;
                }
            };
            // spring rays: centre y 0.595, radius 1.0 -> origin 1.495 m up, reach 1.9 m; at rest compression (0.405 m) the
            // ray meets the ground exactly at the soles (root y 0)
            foreach (var w in new[] { new Vector3(-0.45f, 0.595f, 0.35f), new Vector3(0.45f, 0.595f, 0.35f), new Vector3(-0.45f, 0.595f, -0.35f), new Vector3(0.45f, 0.595f, -0.35f) })
                v.AddWheel(w, 1.0f, null, false);
            v.seat = Mats.Node(bodyN, "Seat", new Vector3(0f, hh * 0.79f, -0.2f));
            v.seatScale = 0.5f;
            v.FinishSetup();
            Done(v);
            return v;
        }
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

    static Transform SuitLimb(LBPack pk, Transform body, string upper, string lower, float s, Color c)
    {
        Transform u = pk.Spawn(upper, body, Vector3.zero, s, c);
        if (u == null) return Mats.Node(body, upper, Vector3.zero);
        pk.Spawn(lower, u, -pk.parts[upper].pivot, 1f, c);
        return u;
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
        LBPack hpk = LBPack.Get("heli");
        if (hpk != null && hpk.Has("body") && hpk.Has("rotor") && hpk.Has("tailrotor"))
        {
            hpk.Spawn("body", t, Vector3.zero, 1f, new Color(0.96f, 0.96f, 0.97f));
            Vector3 R0 = hpk.parts["rotor"].pivot, T0 = hpk.parts["tailrotor"].pivot;
            Transform rotorN = Mats.Node(t, "Rotor", R0); Part(hpk, "rotor", rotorN, R0, Color.white);
            v.rotors.Add(rotorN); v.rotorAxes.Add(Vector3.up);
            Transform tailN = Mats.Node(t, "TailRotor", T0); Part(hpk, "tailrotor", tailN, T0, Color.white);
            v.tailRotor = tailN;
            v.ExtraBoxPublic(new Vector3(0f, 0.15f, 0.3f), new Vector3(2.1f, 0.3f, 3.4f));
            v.ExtraBoxPublic(new Vector3(0f, 1.9f, -3.6f), new Vector3(0.6f, 0.7f, 3.6f));
            v.seat = Mats.Node(t, "Seat", new Vector3(-0.4f, 1.02f, 0.95f));
            v.seatScale = 0.62f;
            Done(v);
            return v;
        }
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
        LBPack dpk = LBPack.Get("drone");
        if (dpk != null && dpk.Has("body") && dpk.Has("prop"))
        {
            // EHang 216-style: 8 arms, coaxial props (16 rotors)
            dpk.Spawn("body", t, Vector3.zero, 1f, new Color(0.96f, 0.96f, 0.97f));
            for (int k = 0; k < 8; k++)
            {
                float a = (22.5f + k * 45f) * Mathf.Deg2Rad;
                Vector3 hub = new Vector3(0f, 1.92f, -0.25f) + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 2.05f;
                for (int lvl = 0; lvl < 2; lvl++)
                {
                    Transform pn = Mats.Node(t, "Rotor", hub + Vector3.up * (lvl == 0 ? 0.2f : -0.2f));
                    dpk.Spawn("prop", pn, Vector3.zero, 1f, Color.white);
                    pn.localRotation = Quaternion.Euler(0f, k * 37f + lvl * 90f, 0f);
                    v.rotors.Add(pn); v.rotorAxes.Add(Vector3.up);
                }
            }
            v.ExtraBoxPublic(new Vector3(0f, 0.1f, 0f), new Vector3(1.5f, 0.2f, 1.8f));
            v.seat = Mats.Node(t, "Seat", new Vector3(-0.33f, 0.82f, 0.15f));
            v.seatScale = 0.62f;
            Done(v);
            return v;
        }
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
    public static Boat BoatAt(Vector3 pos, float yaw) { return BoatAt(pos, yaw, new Color(0.12f, 0.4f, 0.75f)); }

    public static Boat BoatAt(Vector3 pos, float yaw, Color hullTint)
    {
        var v = Root<Boat>("Boat", pos, yaw);
        v.EnterVerb = "take the Boat";
        v.engineKind = 4;
        v.SetupBodyPublic(700f, new Vector3(0f, 0.5f, -0.2f), new Vector3(2.2f, 0.8f, 5.0f), new Vector3(0f, 0.15f, 0f));
        v.rb.drag = 0.1f;
        v.rb.angularDrag = 2f;
        v.camDistance = 12f; v.camHeight = 3f;
        Transform t = v.transform;
        LBPack bpk = LBPack.Get("boat");
        if (bpk != null && bpk.Has("body") && bpk.Has("prop"))
        {
            bpk.Spawn("body", t, Vector3.zero, 1f, hullTint);
            Vector3 P0 = bpk.parts["prop"].pivot;
            Transform pn = Mats.Node(t, "Prop", P0); Part(bpk, "prop", pn, P0, Color.white);
            v.prop = pn;
            v.seat = Mats.Node(t, "Seat", new Vector3(0.45f, 0.66f, -0.22f));
            v.seatScale = 0.6f;
            Done(v);
            return v;
        }
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
