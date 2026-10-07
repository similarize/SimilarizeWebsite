using UnityEngine;

// "Critters" figure mode: ten named frogs, cats and dogs built procedurally from primitives.
public enum CritterKind { Frog, Cat, Dog }
public enum CritterLook { Plain, Spots, Stripes, Socks, Shepherd, Dachshund }

public class CritterDef
{
    public string name;
    public CritterKind kind;
    public CritterLook look;
    public Color main, accent;

    public CritterDef(string name, CritterKind kind, CritterLook look, string main, string accent)
    {
        this.name = name; this.kind = kind; this.look = look;
        this.main = Critters.Hex(main);
        this.accent = accent != null ? Critters.Hex(accent) : this.main;
    }

    public string KindName { get { return kind == CritterKind.Frog ? "Frog" : kind == CritterKind.Cat ? "Cat" : look == CritterLook.Shepherd ? "German shepherd" : "Dachshund"; } }
}

public static class Critters
{
    public static readonly CritterDef[] All =
    {
        new CritterDef("James", CritterKind.Frog, CritterLook.Plain, "#3fbf3f", null),
        new CritterDef("Jimmy", CritterKind.Frog, CritterLook.Plain, "#a6d832", null),
        new CritterDef("Bubbles", CritterKind.Frog, CritterLook.Plain, "#2fb3c4", null),
        new CritterDef("Rexy", CritterKind.Frog, CritterLook.Plain, "#f2582a", null),
        new CritterDef("Spotty", CritterKind.Cat, CritterLook.Spots, "#f4f1ea", "#2a2626"),
        new CritterDef("Tigy", CritterKind.Cat, CritterLook.Stripes, "#f28c28", "#7a3a10"),
        new CritterDef("Kitty", CritterKind.Cat, CritterLook.Plain, "#b8a9c9", null),
        new CritterDef("Little White Socks", CritterKind.Cat, CritterLook.Socks, "#34343a", "#ffffff"),
        new CritterDef("Germy", CritterKind.Dog, CritterLook.Shepherd, "#b47a3c", "#1e1a18"),
        new CritterDef("Daisy", CritterKind.Dog, CritterLook.Dachshund, "#a0482a", null),
    };

    public static int Count { get { return All.Length; } }

    public static Color Hex(string h)
    {
        Color c;
        return ColorUtility.TryParseHtmlString(h, out c) ? c : Color.magenta;
    }

    public static string HexOf(Color c) { return "#" + ColorUtility.ToHtmlStringRGB(c); }
}

// Builds and animates one critter body under a Soldier (feet at local origin, about 1.75 m tall).
public class Critter : MonoBehaviour
{
    Soldier s;
    public CritterDef def;
    Transform bob, headPivot, armPivot, legL, legR, tail, tail2;
    float phase, moveBlend, flop, seed;
    Quaternion tailBase = Quaternion.identity, tail2Base = Quaternion.identity;

    static readonly Color Gun = new Color(0.2f, 0.21f, 0.23f);
    static readonly Color Orange = new Color(1f, 0.45f, 0.05f);
    static readonly Color Black = new Color(0.05f, 0.05f, 0.06f);
    static readonly Color Pink = new Color(0.95f, 0.6f, 0.65f);

    // ---------- primitive helpers (all decorative: colliders removed) ----------
    static GameObject P(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Color c, Vector3 euler)
    {
        GameObject g = Mats.Prim(t, parent, pos, scale, Mats.Lit(c), false);
        g.transform.localRotation = Quaternion.Euler(euler);
        return g;
    }

    static GameObject P(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Color c)
    {
        return Mats.Prim(t, parent, pos, scale, Mats.Lit(c), false);
    }

    static GameObject Ball(Transform parent, Vector3 pos, float d, Color c)
    {
        return P(PrimitiveType.Sphere, parent, pos, Vector3.one * d, c);
    }

    // capsule whose end-cap centres sit on a and b
    static GameObject Limb(Transform parent, Vector3 a, Vector3 b, float r, Color c)
    {
        Vector3 d = b - a;
        float len = d.magnitude;
        GameObject g = Mats.Prim(PrimitiveType.Capsule, parent, (a + b) * 0.5f, new Vector3(r * 2f, len * 0.5f + r, r * 2f), Mats.Lit(c), false);
        if (len > 0.0001f) g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d / len);
        return g;
    }

    // flattened sphere lying on an ellipsoid surface (spots, stripes, patches)
    static GameObject Decal(Transform parent, Vector3 centre, Vector3 radii, Vector3 dir, Vector3 size, Color c, float twist = 0f)
    {
        dir.Normalize();
        Vector3 p = centre + new Vector3(dir.x * radii.x, dir.y * radii.y, dir.z * radii.z);
        Vector3 up = Mathf.Abs(dir.y) > 0.95f ? Vector3.forward : Vector3.up;
        GameObject g = Mats.Prim(PrimitiveType.Sphere, parent, p, size, Mats.Lit(c), false);
        g.transform.localRotation = Quaternion.LookRotation(dir, up) * Quaternion.Euler(0f, 0f, twist);
        return g;
    }

    static Transform Node(Transform parent, string name, Vector3 pos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        return t;
    }

    static Vector3 Dir(float yawDeg, float y)
    {
        float a = yawDeg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(a), y, Mathf.Cos(a));
    }

    // ---------- build ----------
    public void Build(Soldier owner, CritterDef d)
    {
        s = owner;
        def = d;
        seed = Random.value * 10f;
        bob = Node(transform, "Bob", Vector3.zero);
        switch (d.kind)
        {
            case CritterKind.Frog: BuildFrog(); break;
            case CritterKind.Cat: BuildCat(); break;
            default: BuildDog(); break;
        }
        if (tail != null) tailBase = tail.localRotation;
        if (tail2 != null) tail2Base = tail2.localRotation;
    }

    void BuildFrog()
    {
        Color m = def.main;
        Color belly = Color.Lerp(m, new Color(1f, 1f, 0.82f), 0.55f);
        Color dark = Color.Lerp(m, Color.black, 0.45f);

        // long folded back legs + big webbed feet
        for (int side = -1; side <= 1; side += 2)
        {
            Transform leg = Node(bob, side < 0 ? "LegL" : "LegR", new Vector3(0.3f * side, 0.55f, -0.05f));
            Vector3 hip = Vector3.zero, knee = new Vector3(0.06f * side, -0.28f, 0.26f), ankle = new Vector3(0.03f * side, -0.5f, -0.04f);
            Limb(leg, hip, knee, 0.12f, m);
            Limb(leg, knee, ankle, 0.08f, m);
            P(PrimitiveType.Sphere, leg, new Vector3(0.06f * side, -0.52f, 0.12f), new Vector3(0.3f, 0.07f, 0.5f), dark, new Vector3(0f, 15f * side, 0f));
            if (side < 0) legL = leg; else legR = leg;
        }
        // squat wide body + pale belly
        P(PrimitiveType.Sphere, bob, new Vector3(0f, 0.95f, 0f), new Vector3(0.95f, 0.85f, 0.75f), m);
        P(PrimitiveType.Sphere, bob, new Vector3(0f, 0.88f, 0.16f), new Vector3(0.72f, 0.62f, 0.48f), belly);

        // head with wide mouth and big bulging eyes
        headPivot = Node(bob, "Head", new Vector3(0f, 1.3f, 0f));
        P(PrimitiveType.Sphere, headPivot, new Vector3(0f, 0.12f, 0.04f), new Vector3(0.88f, 0.5f, 0.72f), m);
        P(PrimitiveType.Sphere, headPivot, new Vector3(0f, 0.05f, 0.1f), new Vector3(0.7f, 0.25f, 0.55f), belly);
        P(PrimitiveType.Cube, headPivot, new Vector3(0f, 0.04f, 0.39f), new Vector3(0.48f, 0.025f, 0.03f), dark);
        for (int side = -1; side <= 1; side += 2)
        {
            Ball(headPivot, new Vector3(0.22f * side, 0.34f, 0.12f), 0.3f, m);
            Ball(headPivot, new Vector3(0.22f * side, 0.36f, 0.2f), 0.24f, Color.white);
            Ball(headPivot, new Vector3(0.22f * side, 0.37f, 0.31f), 0.11f, Black);
            Ball(headPivot, new Vector3(0.2f * side, 0.4f, 0.35f), 0.035f, Color.white);
        }
        BuildArms(1.1f, 0.38f, m, m, 0.13f);
    }

    void BuildCat()
    {
        Color m = def.main;
        bool socks = def.look == CritterLook.Socks;
        Color paw = socks ? def.accent : m;
        Color chest = socks ? def.accent : Color.Lerp(m, Color.white, 0.5f);
        Color eye = def.look == CritterLook.Spots ? new Color(0.35f, 0.65f, 1f) : def.look == CritterLook.Stripes ? new Color(1f, 0.85f, 0.2f) : new Color(0.55f, 0.9f, 0.3f);
        Color whisker = def.main.grayscale > 0.6f ? new Color(0.25f, 0.22f, 0.22f) : new Color(0.92f, 0.92f, 0.92f);

        for (int side = -1; side <= 1; side += 2)
        {
            Transform leg = Node(bob, side < 0 ? "LegL" : "LegR", new Vector3(0.15f * side, 0.55f, 0f));
            Limb(leg, Vector3.zero, new Vector3(0f, -0.45f, 0.02f), 0.1f, m);
            P(PrimitiveType.Sphere, leg, new Vector3(0f, -0.5f, 0.08f), new Vector3(0.2f, 0.1f, 0.28f), paw);
            if (side < 0) legL = leg; else legR = leg;
        }
        Vector3 bc = new Vector3(0f, 0.92f, 0f);
        P(PrimitiveType.Capsule, bob, bc, new Vector3(0.56f, 0.42f, 0.46f), m);
        P(PrimitiveType.Sphere, bob, new Vector3(0f, 1.0f, 0.14f), new Vector3(0.36f, 0.45f, 0.25f), chest);

        headPivot = Node(bob, "Head", new Vector3(0f, 1.32f, 0f));
        Vector3 hc = new Vector3(0f, 0.18f, 0.03f);
        P(PrimitiveType.Sphere, headPivot, hc, new Vector3(0.52f, 0.46f, 0.46f), m);
        for (int side = -1; side <= 1; side += 2)
        {
            // triangle ears: diamond cubes half sunk into the head
            P(PrimitiveType.Cube, headPivot, new Vector3(0.15f * side, 0.4f, 0f), new Vector3(0.16f, 0.16f, 0.05f), m, new Vector3(0f, 0f, 45f - 10f * side));
            P(PrimitiveType.Cube, headPivot, new Vector3(0.15f * side, 0.39f, 0.022f), new Vector3(0.1f, 0.1f, 0.02f), Pink, new Vector3(0f, 0f, 45f - 10f * side));
            P(PrimitiveType.Sphere, headPivot, new Vector3(0.1f * side, 0.22f, 0.2f), new Vector3(0.1f, 0.11f, 0.06f), eye);
            P(PrimitiveType.Cube, headPivot, new Vector3(0.1f * side, 0.22f, 0.232f), new Vector3(0.025f, 0.08f, 0.015f), Black);
            for (int w = 0; w < 2; w++)
                P(PrimitiveType.Cube, headPivot, new Vector3(0.17f * side, 0.12f - 0.035f * w, 0.24f), new Vector3(0.18f, 0.008f, 0.008f), whisker, new Vector3(0f, 0f, (w == 0 ? 8f : -6f) * side));
        }
        P(PrimitiveType.Sphere, headPivot, new Vector3(0f, 0.1f, 0.22f), new Vector3(0.22f, 0.13f, 0.12f), Color.Lerp(m, Color.white, socks ? 1f : 0.6f));
        P(PrimitiveType.Sphere, headPivot, new Vector3(0f, 0.15f, 0.275f), new Vector3(0.06f, 0.045f, 0.04f), Pink);

        // tail: two-piece, curling up behind
        tail = Node(bob, "Tail", new Vector3(0f, 0.62f, -0.2f));
        Limb(tail, Vector3.zero, new Vector3(0f, 0.08f, -0.32f), 0.055f, m);
        tail2 = Node(tail, "Tail2", new Vector3(0f, 0.08f, -0.32f));
        Limb(tail2, Vector3.zero, new Vector3(0f, 0.34f, -0.06f), 0.05f, m);
        if (socks) Ball(tail2, new Vector3(0f, 0.36f, -0.065f), 0.11f, def.accent);

        if (def.look == CritterLook.Spots)
        {
            Vector3 r = new Vector3(0.28f, 0.42f, 0.23f);
            Vector3 sz = new Vector3(0.16f, 0.13f, 0.04f);
            Decal(bob, bc, r, Dir(70f, 0.3f), sz, def.accent);
            Decal(bob, bc, r, Dir(-100f, -0.2f), sz * 1.2f, def.accent);
            Decal(bob, bc, r, Dir(160f, 0.4f), sz, def.accent);
            Decal(bob, bc, r, Dir(-160f, -0.35f), sz * 0.9f, def.accent);
            Decal(bob, bc, r, Dir(120f, -0.5f), sz * 0.8f, def.accent);
            Decal(headPivot, hc, new Vector3(0.26f, 0.23f, 0.23f), Dir(-35f, 0.7f), new Vector3(0.13f, 0.11f, 0.03f), def.accent);
            Decal(headPivot, hc, new Vector3(0.26f, 0.23f, 0.23f), Dir(80f, 0.1f), new Vector3(0.09f, 0.08f, 0.03f), def.accent);
            Ball(tail2, new Vector3(0f, 0.3f, -0.05f), 0.1f, def.accent);
        }
        else if (def.look == CritterLook.Stripes)
        {
            Vector3 r = new Vector3(0.28f, 0.42f, 0.23f);
            float[] ys = { -0.35f, -0.12f, 0.1f, 0.32f };
            foreach (float y in ys)
                foreach (float a in new[] { 80f, 125f, 180f, 235f, 280f })
                    Decal(bob, bc, r, Dir(a, y), new Vector3(0.2f, 0.045f, 0.04f), def.accent);
            for (int k = -1; k <= 1; k++)
                Decal(headPivot, hc, new Vector3(0.26f, 0.23f, 0.23f), Dir(12f * k, 0.9f), new Vector3(0.03f, 0.1f, 0.02f), def.accent);
            Ball(tail, new Vector3(0f, 0.05f, -0.2f), 0.12f, def.accent);
            Ball(tail2, new Vector3(0f, 0.2f, -0.035f), 0.11f, def.accent);
        }
        BuildArms(1.15f, 0.25f, m, paw, 0.085f);
    }

    void BuildDog()
    {
        Color m = def.main;
        bool shep = def.look == CritterLook.Shepherd;
        Color dark = shep ? def.accent : Color.Lerp(m, Color.black, 0.35f);
        Color eye = new Color(0.3f, 0.18f, 0.08f);

        float hipY = shep ? 0.55f : 0.3f;
        float legLen = shep ? 0.45f : 0.22f;
        for (int side = -1; side <= 1; side += 2)
        {
            Transform leg = Node(bob, side < 0 ? "LegL" : "LegR", new Vector3(0.15f * side, hipY, 0f));
            Limb(leg, Vector3.zero, new Vector3(0f, -legLen, 0.02f), shep ? 0.11f : 0.1f, m);
            P(PrimitiveType.Sphere, leg, new Vector3(0f, -hipY + 0.05f, 0.08f), new Vector3(0.22f, 0.1f, 0.3f), m);
            if (side < 0) legL = leg; else legR = leg;
        }

        float headY;
        if (shep)
        {
            P(PrimitiveType.Capsule, bob, new Vector3(0f, 0.92f, 0f), new Vector3(0.6f, 0.44f, 0.5f), m);
            // black saddle over the back and shoulders
            P(PrimitiveType.Sphere, bob, new Vector3(0f, 1.0f, -0.08f), new Vector3(0.62f, 0.62f, 0.4f), dark);
            P(PrimitiveType.Sphere, bob, new Vector3(0f, 1.0f, 0.14f), new Vector3(0.34f, 0.42f, 0.25f), Color.Lerp(m, Color.white, 0.25f));
            headY = 1.34f;
        }
        else
        {
            // dachshund: long torso, stubby legs
            P(PrimitiveType.Capsule, bob, new Vector3(0f, 0.92f, 0f), new Vector3(0.5f, 0.62f, 0.44f), m);
            P(PrimitiveType.Sphere, bob, new Vector3(0f, 1.0f, 0.13f), new Vector3(0.3f, 0.6f, 0.24f), Color.Lerp(m, new Color(1f, 0.85f, 0.6f), 0.3f));
            headY = 1.5f;
        }

        headPivot = Node(bob, "Head", new Vector3(0f, headY, 0f));
        if (shep)
        {
            P(PrimitiveType.Sphere, headPivot, new Vector3(0f, 0.18f, 0f), new Vector3(0.48f, 0.44f, 0.46f), m);
            Limb(headPivot, new Vector3(0f, 0.1f, 0.15f), new Vector3(0f, 0.08f, 0.38f), 0.09f, dark);
            Ball(headPivot, new Vector3(0f, 0.12f, 0.47f), 0.07f, Black);
            for (int side = -1; side <= 1; side += 2)
            {
                // tall pointy ears: a diamond cube inside a vertically stretched pivot
                Transform ear = Node(headPivot, "Ear", new Vector3(0.14f * side, 0.42f, -0.02f));
                ear.localRotation = Quaternion.Euler(0f, 0f, -12f * side);
                ear.localScale = new Vector3(1f, 1.8f, 1f);
                P(PrimitiveType.Cube, ear, Vector3.zero, new Vector3(0.15f, 0.15f, 0.05f), m, new Vector3(0f, 0f, 45f));
                P(PrimitiveType.Cube, ear, new Vector3(0f, -0.005f, 0.02f), new Vector3(0.09f, 0.09f, 0.02f), dark, new Vector3(0f, 0f, 45f));
                Ball(headPivot, new Vector3(0.1f * side, 0.24f, 0.19f), 0.08f, eye);
                Ball(headPivot, new Vector3(0.1f * side, 0.245f, 0.225f), 0.045f, Black);
            }
            tail = Node(bob, "Tail", new Vector3(0f, 0.62f, -0.22f));
            Limb(tail, Vector3.zero, new Vector3(0f, -0.25f, -0.3f), 0.08f, m);
            Limb(tail, new Vector3(0f, -0.02f, -0.06f), new Vector3(0f, -0.22f, -0.3f), 0.06f, dark);
        }
        else
        {
            P(PrimitiveType.Sphere, headPivot, new Vector3(0f, 0.12f, 0.02f), new Vector3(0.42f, 0.38f, 0.42f), m);
            Limb(headPivot, new Vector3(0f, 0.06f, 0.12f), new Vector3(0f, 0.02f, 0.42f), 0.08f, m);
            Ball(headPivot, new Vector3(0f, 0.04f, 0.5f), 0.07f, Black);
            for (int side = -1; side <= 1; side += 2)
            {
                // floppy ears hanging at the sides
                GameObject earObj = Limb(headPivot, new Vector3(0.19f * side, 0.18f, 0f), new Vector3(0.24f * side, -0.12f, 0.03f), 0.08f, dark);
                earObj.transform.localScale = new Vector3(0.08f, earObj.transform.localScale.y, 0.2f);
                Ball(headPivot, new Vector3(0.09f * side, 0.18f, 0.19f), 0.075f, eye);
                Ball(headPivot, new Vector3(0.09f * side, 0.185f, 0.222f), 0.04f, Black);
            }
            tail = Node(bob, "Tail", new Vector3(0f, 0.4f, -0.2f));
            Limb(tail, Vector3.zero, new Vector3(0f, 0.12f, -0.32f), 0.05f, m);
        }
        BuildArms(shep ? 1.15f : 1.25f, shep ? 0.27f : 0.23f, m, m, 0.1f);
    }

    // arms + small orange-tipped blaster on a pivot that follows the aim pitch
    void BuildArms(float shoulderY, float shoulderX, Color arm, Color paw, float r)
    {
        armPivot = Node(bob, "Arms", new Vector3(0f, shoulderY, 0.06f));
        Vector3 gripR = new Vector3(0.03f, -0.12f, 0.3f), gripL = new Vector3(-0.03f, -0.07f, 0.48f);
        Limb(armPivot, new Vector3(shoulderX, 0f, 0f), gripR, r * 0.7f, arm);
        Limb(armPivot, new Vector3(-shoulderX, 0f, 0f), gripL, r * 0.7f, arm);
        Ball(armPivot, gripR, r * 1.15f, paw);
        Ball(armPivot, gripL, r * 1.15f, paw);

        Transform gun = Node(armPivot, "Blaster", new Vector3(0f, -0.04f, 0f));
        P(PrimitiveType.Cube, gun, new Vector3(0f, 0f, 0.42f), new Vector3(0.08f, 0.1f, 0.36f), Gun);
        P(PrimitiveType.Cube, gun, new Vector3(0f, -0.08f, 0.31f), new Vector3(0.05f, 0.12f, 0.06f), Gun);
        P(PrimitiveType.Cylinder, gun, new Vector3(0f, 0.01f, 0.64f), new Vector3(0.045f, 0.05f, 0.045f), Gun, new Vector3(90f, 0f, 0f));
        P(PrimitiveType.Cube, gun, new Vector3(0f, 0.01f, 0.7f), new Vector3(0.075f, 0.075f, 0.045f), Orange);
        P(PrimitiveType.Cube, gun, new Vector3(0f, 0.065f, 0.4f), new Vector3(0.025f, 0.03f, 0.12f), Orange);
    }

    // ---------- animation ----------
    public void ResetPose()
    {
        flop = 0f; moveBlend = 0f; phase = 0f;
        if (bob != null) { bob.localPosition = Vector3.zero; bob.localRotation = Quaternion.identity; bob.localScale = Vector3.one; }
    }

    void Update()
    {
        if (s == null || bob == null) return;
        float dt = Time.deltaTime, t = Time.time + seed;
        float spd = s.HSpeed;
        bool airborne = s.cc != null && !s.cc.isGrounded;
        moveBlend = Mathf.MoveTowards(moveBlend, s.alive && spd > 0.4f ? 1f : 0f, dt * 5f);
        flop = Mathf.MoveTowards(flop, s.alive ? 0f : 1f, dt * (s.alive ? 4f : 2.2f));
        bool frog = def.kind == CritterKind.Frog;

        phase += dt * (frog ? 7.5f : 11f) * Mathf.Clamp(spd / 5.4f, 0.35f, 1.3f) * (moveBlend > 0.01f ? 1f : 0f);
        float sn = Mathf.Sin(phase);
        float yOff, roll, tilt, legA, legB;
        if (frog)
        {
            float hop = Mathf.Abs(sn);
            yOff = hop * 0.22f * moveBlend;
            tilt = hop * 8f * moveBlend;
            roll = 0f;
            legA = legB = -hop * 40f * moveBlend;
        }
        else
        {
            yOff = Mathf.Abs(sn) * 0.06f * moveBlend;
            roll = sn * 7f * moveBlend;
            tilt = 4f * moveBlend;
            legA = sn * 32f * moveBlend;
            legB = -legA;
        }
        if (airborne && s.alive) { legA = legB = frog ? 30f : 20f; }

        // idle sway + breathing
        float idle = 1f - moveBlend;
        roll += Mathf.Sin(t * 1.6f) * 2.5f * idle;
        tilt += Mathf.Sin(t * 1.1f) * 1.2f * idle;
        bob.localScale = new Vector3(1f, 1f + Mathf.Sin(t * 2.4f) * 0.015f * idle, 1f);

        // flop over backwards when out
        float e = flop * flop * (3f - 2f * flop);
        Quaternion up = Quaternion.Euler(tilt, 0f, roll);
        Quaternion down = Quaternion.Euler(-82f, 0f, 14f);
        bob.localRotation = Quaternion.Slerp(up, down, e);
        bob.localPosition = new Vector3(0f, Mathf.Lerp(yOff, 0.3f, e), Mathf.Lerp(0f, -0.15f, e));

        if (legL != null) legL.localRotation = Quaternion.Euler(Mathf.Lerp(legA, -30f, e), 0f, 0f);
        if (legR != null) legR.localRotation = Quaternion.Euler(Mathf.Lerp(legB, -30f, e), 0f, 0f);

        float aim = Mathf.Clamp(s.pitch, -70f, 70f);
        if (armPivot != null) armPivot.localRotation = Quaternion.Euler(Mathf.Lerp(aim, 60f, e), 0f, 0f);
        if (headPivot != null) headPivot.localRotation = Quaternion.Euler(aim * 0.35f + Mathf.Sin(t * 0.9f) * 3f * idle, Mathf.Sin(t * 0.6f) * 6f * idle, 0f);

        if (tail != null)
        {
            float wag;
            if (def.kind == CritterKind.Dog) wag = Mathf.Sin(t * (s.alive ? 14f : 3f)) * (s.alive ? 28f : 6f);
            else wag = Mathf.Sin(t * (2f + moveBlend * 4f)) * (14f + moveBlend * 10f);
            tail.localRotation = tailBase * Quaternion.Euler(0f, wag, 0f);
            if (tail2 != null) tail2.localRotation = tail2Base * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 2.3f + 1f) * 18f);
        }
    }
}
