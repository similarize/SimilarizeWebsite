using System.Collections.Generic;
using UnityEngine;

// James's animals. Named pets use the roster only: cats Spotty, Tigy, Kitty, Little White Socks;
// dogs Germy (German shepherd) and Daisy (dachshund). Everything else (rabbits, lizards, snakes, fish,
// cows, horses...) stays unnamed. Models are primitives with no colliders; motion is kinematic.
public class Animal : MonoBehaviour
{
    public enum Kind { Cat, Dog, Rabbit, Lizard, Cow, Horse, Chicken, Goat }
    public Kind kind;
    public string petName = "";        // empty = unnamed
    public Rect area;                  // wander bounds (x/z, world)
    public float floorY;
    public float speed = 1.6f, runSpeed = 4.5f;
    public bool skittish = true;       // flees from frogs / vehicles that come close
    public Transform body, head, tail;
    public readonly List<Transform> legs = new List<Transform>();
    public Vector3 target;
    public Transform follow;           // walk to / stay near this
    public float followDist = 1.6f;
    public bool frozen;                // hide-and-seek pose
    public bool holdPosition;
    float timer, phase, grazeT, spd, yaw, fleeT;
    Vector3 fleeFrom;
    public static readonly List<Animal> All = new List<Animal>();

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public Vector3 Pos { get { return transform.position; } }

    public void Init(Rect r, float y)
    {
        area = r; floorY = y;
        yaw = Random.value * 360f;
        PickTarget();
        timer = Random.Range(0f, 4f);
        phase = Random.value * 10f;
    }

    void PickTarget()
    {
        target = new Vector3(Random.Range(area.xMin + 0.6f, area.xMax - 0.6f), floorY, Random.Range(area.yMin + 0.6f, area.yMax - 0.6f));
    }

    public void Scare(Vector3 from) { fleeFrom = from; fleeT = 1.6f; }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        Vector3 p = transform.position;
        float want = 0f;
        Vector3 to = Vector3.zero;
        if (frozen) { Animate(0f, dt); return; }

        // react to frogs / vehicles that come close (ranch animals bolt, pets just trot aside)
        if (skittish && fleeT <= 0f && Game.I != null && Time.frameCount % 6 == (GetInstanceID() & 5))
        {
            float r = kind == Kind.Cow || kind == Kind.Horse ? 7f : 3.2f;
            foreach (Frog f in Game.I.frogs)
                if (f != null && (f.FocusPoint - p).sqrMagnitude < (f.vehicle != null ? r * r * 4f : r * r)) { Scare(f.FocusPoint); break; }
            foreach (Vehicle v in Vehicle.All)
                if (v != null && v.Speed > 2f && (v.transform.position - p).sqrMagnitude < r * r * 6f) { Scare(v.transform.position); break; }
        }

        if (fleeT > 0f)
        {
            fleeT -= dt;
            to = p - fleeFrom; to.y = 0f;
            if (to.sqrMagnitude < 0.01f) to = Vector3.forward;
            want = runSpeed;
            if (fleeT <= 0f) PickTarget();
        }
        else if (follow != null)
        {
            to = follow.position - p; to.y = 0f;
            float d = to.magnitude;
            want = d > followDist ? Mathf.Min(runSpeed, (d - followDist) * 2.5f + speed) : 0f;
        }
        else if (holdPosition) want = 0f;
        else
        {
            timer -= dt;
            to = target - p; to.y = 0f;
            if (grazeT > 0f) { grazeT -= dt; want = 0f; }
            else if (to.magnitude < 0.4f || timer <= 0f)
            {
                timer = Random.Range(4f, 9f);
                grazeT = Random.Range(1.5f, 5f);
                PickTarget();
            }
            else want = speed;
        }
        spd = Mathf.MoveTowards(spd, want, dt * 6f);
        if (to.sqrMagnitude > 0.0001f && spd > 0.05f)
        {
            float ty = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            yaw = Mathf.MoveTowardsAngle(yaw, ty, 300f * dt);
        }
        Vector3 fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        p += fwd * spd * dt;
        // stay inside the area
        p.x = Mathf.Clamp(p.x, area.xMin + 0.3f, area.xMax - 0.3f);
        p.z = Mathf.Clamp(p.z, area.yMin + 0.3f, area.yMax - 0.3f);
        p.y = floorY >= -1e4f ? floorY : p.y;
        if (floorY < -1e4f) p.y = Ranch.GY(p.x, p.z);
        transform.position = p;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        Animate(spd, dt);
    }

    void Animate(float s, float dt)
    {
        float t = Time.time + phase;
        float rate = (kind == Kind.Cow ? 4f : kind == Kind.Horse ? 6f : 9f) * Mathf.Clamp(s / Mathf.Max(0.5f, speed), 0.3f, 2.2f);
        for (int i = 0; i < legs.Count; i++)
        {
            float a = s > 0.1f ? Mathf.Sin(t * rate + (i % 2 == (i / 2) % 2 ? 0f : Mathf.PI)) * 32f : 0f;
            legs[i].localRotation = Quaternion.Euler(a, 0f, 0f);
        }
        if (body != null)
        {
            float bob = s > 0.1f ? Mathf.Abs(Mathf.Sin(t * rate)) * 0.04f * (kind == Kind.Rabbit ? 4f : 1f) : Mathf.Sin(t * 2f) * 0.01f;
            body.localPosition = new Vector3(0f, bob, 0f);
            if (frozen) body.localScale = new Vector3(1f, 0.8f, 1f); else body.localScale = Vector3.one;
        }
        if (head != null)
        {
            bool grazing = s < 0.05f && grazeT > 0f && (kind == Kind.Cow || kind == Kind.Horse || kind == Kind.Goat || kind == Kind.Rabbit || kind == Kind.Chicken);
            float nod = grazing ? 38f + Mathf.Sin(t * 3f) * 6f : Mathf.Sin(t * 1.3f) * 5f;
            head.localRotation = Quaternion.Euler(nod, Mathf.Sin(t * 0.7f) * 15f, 0f);
        }
        if (tail != null) tail.localRotation = Quaternion.Euler(kind == Kind.Dog || kind == Kind.Cat ? -30f : 20f, Mathf.Sin(t * (kind == Kind.Dog ? 14f : 3f)) * 25f, 0f);
    }

    // ---------------- builders ----------------
    static GameObject P(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Color c, Vector3 euler = default(Vector3))
    {
        var g = Mats.Prim(t, parent, pos, scale, Mats.Lit(c));
        g.transform.localRotation = Quaternion.Euler(euler);
        return g;
    }

    static Animal Root(string name, Vector3 pos, Kind k)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var a = go.AddComponent<Animal>();
        a.kind = k;
        a.body = Mats.Node(go.transform, "Body", Vector3.zero);
        return a;
    }

    // quadruped: torso length L, height H (to belly), leg thickness w
    static void Quad(Animal a, Color c, Color legC, float L, float H, float W, float thick, float bodyH)
    {
        Transform b = a.body;
        P(PrimitiveType.Capsule, b, new Vector3(0f, H + bodyH * 0.5f, 0f), new Vector3(W, L * 0.5f, bodyH), c, new Vector3(90f, 0f, 0f));
        for (int i = 0; i < 4; i++)
        {
            float x = (i % 2 == 0 ? -1f : 1f) * W * 0.32f, z = (i < 2 ? 1f : -1f) * L * 0.36f;
            Transform leg = Mats.Node(b, "Leg", new Vector3(x, H + 0.05f, z));
            P(PrimitiveType.Cube, leg, new Vector3(0f, -H * 0.5f, 0f), new Vector3(thick, H + 0.05f, thick), legC);
            a.legs.Add(leg);
        }
    }

    public static Animal Cat(string name, Vector3 pos, Color fur, Color patch, bool socks)
    {
        var a = Root(name.Length > 0 ? "Cat " + name : "Cat", pos, Kind.Cat);
        a.petName = name;
        a.speed = 1.2f; a.runSpeed = 4.2f;
        Quad(a, fur, socks ? Color.white : fur, 0.62f, 0.2f, 0.26f, 0.07f, 0.24f);
        P(PrimitiveType.Sphere, a.body, new Vector3(0.05f, 0.42f, -0.05f), new Vector3(0.2f, 0.12f, 0.24f), patch);
        a.head = Mats.Node(a.body, "Head", new Vector3(0f, 0.46f, 0.36f));
        P(PrimitiveType.Sphere, a.head, Vector3.zero, new Vector3(0.24f, 0.21f, 0.22f), fur);
        for (int s = -1; s <= 1; s += 2)
        {
            P(PrimitiveType.Cube, a.head, new Vector3(0.07f * s, 0.12f, 0f), new Vector3(0.06f, 0.09f, 0.03f), patch, new Vector3(0f, 0f, 20f * -s));
            P(PrimitiveType.Sphere, a.head, new Vector3(0.055f * s, 0.03f, 0.1f), Vector3.one * 0.05f, new Color(0.45f, 0.8f, 0.3f));
        }
        P(PrimitiveType.Sphere, a.head, new Vector3(0f, -0.02f, 0.115f), Vector3.one * 0.03f, new Color(1f, 0.6f, 0.65f));
        a.tail = Mats.Node(a.body, "Tail", new Vector3(0f, 0.4f, -0.32f));
        P(PrimitiveType.Capsule, a.tail, new Vector3(0f, 0.18f, -0.05f), new Vector3(0.05f, 0.2f, 0.05f), patch, new Vector3(-20f, 0f, 0f));
        Mats.NoShadows(a.gameObject);
        return a;
    }

    public static Animal Dog(string name, Vector3 pos, bool shepherd)
    {
        var a = Root(name.Length > 0 ? "Dog " + name : "Dog", pos, Kind.Dog);
        a.petName = name;
        a.skittish = false;
        Color tan = shepherd ? new Color(0.72f, 0.5f, 0.26f) : new Color(0.55f, 0.28f, 0.12f);
        Color dark = shepherd ? new Color(0.12f, 0.1f, 0.09f) : new Color(0.35f, 0.16f, 0.07f);
        float L = shepherd ? 0.95f : 0.8f, H = shepherd ? 0.42f : 0.13f, W = shepherd ? 0.34f : 0.24f;
        a.speed = shepherd ? 1.8f : 1.3f; a.runSpeed = shepherd ? 6.5f : 4.5f;
        Quad(a, tan, shepherd ? tan : dark, L, H, W, shepherd ? 0.1f : 0.08f, shepherd ? 0.36f : 0.25f);
        if (shepherd) P(PrimitiveType.Capsule, a.body, new Vector3(0f, H + 0.3f, -0.05f), new Vector3(W * 0.9f, L * 0.36f, 0.2f), dark, new Vector3(90f, 0f, 0f));   // black saddle
        a.head = Mats.Node(a.body, "Head", new Vector3(0f, H + (shepherd ? 0.42f : 0.22f), L * 0.52f));
        P(PrimitiveType.Sphere, a.head, Vector3.zero, new Vector3(0.26f, 0.24f, 0.28f), tan);
        P(PrimitiveType.Cube, a.head, new Vector3(0f, -0.04f, 0.17f), new Vector3(0.13f, 0.11f, 0.2f), shepherd ? dark : tan);
        P(PrimitiveType.Sphere, a.head, new Vector3(0f, -0.01f, 0.28f), Vector3.one * 0.06f, Color.black);
        for (int s = -1; s <= 1; s += 2)
        {
            if (shepherd) P(PrimitiveType.Cube, a.head, new Vector3(0.08f * s, 0.17f, -0.02f), new Vector3(0.07f, 0.16f, 0.04f), dark, new Vector3(0f, 0f, 12f * -s));
            else P(PrimitiveType.Cube, a.head, new Vector3(0.14f * s, -0.04f, -0.02f), new Vector3(0.04f, 0.18f, 0.1f), dark, new Vector3(0f, 0f, 8f * s));
            P(PrimitiveType.Sphere, a.head, new Vector3(0.07f * s, 0.05f, 0.11f), Vector3.one * 0.045f, Color.black);
        }
        a.tail = Mats.Node(a.body, "Tail", new Vector3(0f, H + 0.25f, -L * 0.5f));
        P(PrimitiveType.Capsule, a.tail, new Vector3(0f, 0.12f, -0.12f), new Vector3(0.07f, 0.18f, 0.07f), shepherd ? dark : tan, new Vector3(-40f, 0f, 0f));
        Mats.NoShadows(a.gameObject);
        return a;
    }

    public static Animal Rabbit(Vector3 pos, Color c)
    {
        var a = Root("Rabbit", pos, Kind.Rabbit);
        a.speed = 1f; a.runSpeed = 3.8f;
        Quad(a, c, c, 0.32f, 0.06f, 0.22f, 0.06f, 0.24f);
        a.head = Mats.Node(a.body, "Head", new Vector3(0f, 0.28f, 0.2f));
        P(PrimitiveType.Sphere, a.head, Vector3.zero, new Vector3(0.17f, 0.16f, 0.18f), c);
        for (int s = -1; s <= 1; s += 2) P(PrimitiveType.Capsule, a.head, new Vector3(0.04f * s, 0.15f, -0.02f), new Vector3(0.04f, 0.1f, 0.025f), c, new Vector3(-10f, 0f, 8f * s));
        P(PrimitiveType.Sphere, a.body, new Vector3(0f, 0.22f, -0.18f), Vector3.one * 0.08f, Color.white);
        Mats.NoShadows(a.gameObject);
        return a;
    }

    public static Animal Lizard(Vector3 pos, Color c)
    {
        var a = Root("Lizard", pos, Kind.Lizard);
        a.speed = 0.4f; a.runSpeed = 1.2f; a.skittish = false;
        Quad(a, c, c, 0.26f, 0.03f, 0.1f, 0.03f, 0.06f);
        a.head = Mats.Node(a.body, "Head", new Vector3(0f, 0.07f, 0.17f));
        P(PrimitiveType.Sphere, a.head, Vector3.zero, new Vector3(0.07f, 0.05f, 0.09f), c);
        a.tail = Mats.Node(a.body, "Tail", new Vector3(0f, 0.06f, -0.14f));
        P(PrimitiveType.Capsule, a.tail, new Vector3(0f, 0f, -0.1f), new Vector3(0.04f, 0.1f, 0.03f), c, new Vector3(90f, 0f, 0f));
        Mats.NoShadows(a.gameObject);
        return a;
    }

    public static Animal Cow(Vector3 pos, bool spotted)
    {
        var a = Root("Cow", pos, Kind.Cow);
        a.speed = 0.7f; a.runSpeed = 3.2f;
        Color w = spotted ? new Color(0.95f, 0.95f, 0.93f) : new Color(0.45f, 0.28f, 0.16f);
        Color s = spotted ? new Color(0.1f, 0.1f, 0.1f) : new Color(0.3f, 0.18f, 0.1f);
        Quad(a, w, w, 1.9f, 0.75f, 0.85f, 0.2f, 0.85f);
        if (spotted)
        {
            P(PrimitiveType.Sphere, a.body, new Vector3(0.36f, 1.35f, 0.2f), new Vector3(0.2f, 0.45f, 0.55f), s);
            P(PrimitiveType.Sphere, a.body, new Vector3(-0.38f, 1.2f, -0.4f), new Vector3(0.2f, 0.4f, 0.45f), s);
        }
        P(PrimitiveType.Sphere, a.body, new Vector3(0f, 0.72f, -0.4f), new Vector3(0.3f, 0.18f, 0.3f), new Color(1f, 0.7f, 0.7f));   // udder
        a.head = Mats.Node(a.body, "Head", new Vector3(0f, 1.45f, 1.05f));
        P(PrimitiveType.Cube, a.head, new Vector3(0f, -0.1f, 0.25f), new Vector3(0.42f, 0.42f, 0.55f), w);
        P(PrimitiveType.Cube, a.head, new Vector3(0f, -0.22f, 0.53f), new Vector3(0.4f, 0.22f, 0.12f), new Color(0.95f, 0.7f, 0.7f));
        for (int k = -1; k <= 1; k += 2)
        {
            P(PrimitiveType.Cube, a.head, new Vector3(0.24f * k, 0.12f, 0.1f), new Vector3(0.18f, 0.05f, 0.05f), new Color(0.95f, 0.92f, 0.8f));
            P(PrimitiveType.Sphere, a.head, new Vector3(0.14f * k, 0.02f, 0.5f), Vector3.one * 0.07f, Color.black);
        }
        a.tail = Mats.Node(a.body, "Tail", new Vector3(0f, 1.5f, -0.95f));
        P(PrimitiveType.Cube, a.tail, new Vector3(0f, -0.35f, -0.02f), new Vector3(0.05f, 0.7f, 0.05f), s);
        return a;
    }

    public static Animal Horse(Vector3 pos, Color c, Color mane)
    {
        var a = Root("Horse", pos, Kind.Horse);
        a.speed = 1.1f; a.runSpeed = 6.5f;
        Quad(a, c, c, 1.8f, 1.0f, 0.7f, 0.17f, 0.75f);
        a.head = Mats.Node(a.body, "Head", new Vector3(0f, 1.75f, 0.95f));
        P(PrimitiveType.Cube, a.head, new Vector3(0f, 0.25f, 0.05f), new Vector3(0.3f, 0.8f, 0.35f), c, new Vector3(-30f, 0f, 0f));
        P(PrimitiveType.Cube, a.head, new Vector3(0f, 0.6f, 0.38f), new Vector3(0.28f, 0.3f, 0.6f), c);
        P(PrimitiveType.Cube, a.head, new Vector3(0f, 0.48f, -0.05f), new Vector3(0.08f, 0.8f, 0.2f), mane, new Vector3(-30f, 0f, 0f));
        for (int k = -1; k <= 1; k += 2) P(PrimitiveType.Cube, a.head, new Vector3(0.09f * k, 0.82f, 0.16f), new Vector3(0.06f, 0.14f, 0.05f), c);
        a.tail = Mats.Node(a.body, "Tail", new Vector3(0f, 1.6f, -0.92f));
        P(PrimitiveType.Cube, a.tail, new Vector3(0f, -0.4f, -0.1f), new Vector3(0.12f, 0.8f, 0.12f), mane, new Vector3(15f, 0f, 0f));
        return a;
    }

    public static Animal Chicken(Vector3 pos)
    {
        var a = Root("Chicken", pos, Kind.Chicken);
        a.speed = 0.9f; a.runSpeed = 3.5f;
        Transform b = a.body;
        P(PrimitiveType.Sphere, b, new Vector3(0f, 0.32f, 0f), new Vector3(0.28f, 0.26f, 0.34f), Color.white);
        for (int s = -1; s <= 1; s += 2)
        {
            Transform leg = Mats.Node(b, "Leg", new Vector3(0.06f * s, 0.2f, 0f));
            P(PrimitiveType.Cube, leg, new Vector3(0f, -0.1f, 0f), new Vector3(0.03f, 0.2f, 0.03f), new Color(1f, 0.7f, 0.2f));
            a.legs.Add(leg);
        }
        a.head = Mats.Node(b, "Head", new Vector3(0f, 0.5f, 0.12f));
        P(PrimitiveType.Sphere, a.head, Vector3.zero, Vector3.one * 0.13f, Color.white);
        P(PrimitiveType.Cube, a.head, new Vector3(0f, 0.08f, 0f), new Vector3(0.03f, 0.06f, 0.08f), new Color(0.9f, 0.1f, 0.1f));
        P(PrimitiveType.Cube, a.head, new Vector3(0f, 0f, 0.08f), new Vector3(0.04f, 0.03f, 0.05f), new Color(1f, 0.7f, 0.2f));
        Mats.NoShadows(a.gameObject);
        return a;
    }

    public static Animal Goat(Vector3 pos)
    {
        var a = Root("Goat", pos, Kind.Goat);
        a.speed = 0.9f; a.runSpeed = 4f;
        Color c = new Color(0.88f, 0.86f, 0.8f);
        Quad(a, c, c, 0.9f, 0.45f, 0.4f, 0.08f, 0.42f);
        a.head = Mats.Node(a.body, "Head", new Vector3(0f, 0.95f, 0.5f));
        P(PrimitiveType.Cube, a.head, new Vector3(0f, 0f, 0.1f), new Vector3(0.2f, 0.22f, 0.32f), c);
        for (int s = -1; s <= 1; s += 2) P(PrimitiveType.Cube, a.head, new Vector3(0.06f * s, 0.18f, -0.02f), new Vector3(0.04f, 0.18f, 0.04f), new Color(0.5f, 0.45f, 0.4f), new Vector3(-30f, 0f, 0f));
        P(PrimitiveType.Cube, a.head, new Vector3(0f, -0.14f, 0.18f), new Vector3(0.05f, 0.12f, 0.05f), new Color(0.7f, 0.68f, 0.6f));
        a.tail = Mats.Node(a.body, "Tail", new Vector3(0f, 0.85f, -0.45f));
        P(PrimitiveType.Cube, a.tail, new Vector3(0f, 0.06f, 0f), new Vector3(0.05f, 0.12f, 0.05f), c);
        Mats.NoShadows(a.gameObject);
        return a;
    }

    // a snake that slithers inside a box (terrarium): head follows a lissajous path, segments trail it
    public static Transform Snake(Transform parent, Vector3 centre, Vector3 half, Color c, Color band)
    {
        var go = new GameObject("Snake");
        go.transform.SetParent(parent, false);
        go.transform.position = centre;
        var s = go.AddComponent<Snake>();
        s.Build(half, c, band);
        return go.transform;
    }
}

public class Snake : MonoBehaviour
{
    readonly List<Transform> segs = new List<Transform>();
    readonly List<Vector3> trail = new List<Vector3>();
    Vector3 half;
    float seed;

    public void Build(Vector3 h, Color c, Color band)
    {
        half = h;
        seed = Random.value * 20f;
        for (int i = 0; i < 14; i++)
        {
            float r = i == 0 ? 0.11f : Mathf.Lerp(0.09f, 0.035f, i / 13f);
            var g = Mats.Prim(PrimitiveType.Sphere, transform, Vector3.zero, new Vector3(r * 1.1f, r * 0.8f, r * 1.3f), Mats.Lit(i % 3 == 1 ? band : c));
            segs.Add(g.transform);
        }
        Mats.Prim(PrimitiveType.Sphere, segs[0], new Vector3(0.3f, 0.3f, 0.35f), Vector3.one * 0.25f, Mats.Lit(Color.black));
        Mats.Prim(PrimitiveType.Sphere, segs[0], new Vector3(-0.3f, 0.3f, 0.35f), Vector3.one * 0.25f, Mats.Lit(Color.black));
        Mats.NoShadows(gameObject);
    }

    void Update()
    {
        float t = Time.time * 0.35f + seed;
        Vector3 head = new Vector3(Mathf.Sin(t * 1.3f) * half.x * 0.8f, 0.06f, Mathf.Sin(t * 0.9f + 1f) * half.z * 0.75f);
        head.x += Mathf.Sin(t * 9f) * 0.05f;
        if (trail.Count == 0 || (trail[0] - head).sqrMagnitude > 0.0009f) trail.Insert(0, head);
        if (trail.Count > 200) trail.RemoveAt(trail.Count - 1);
        // place segments every ~0.09 m along the trail
        int ti = 0; float acc = 0f;
        for (int i = 0; i < segs.Count; i++)
        {
            float need = i * 0.09f;
            while (ti < trail.Count - 1 && acc + (trail[ti] - trail[ti + 1]).magnitude < need) { acc += (trail[ti] - trail[ti + 1]).magnitude; ti++; }
            Vector3 p = trail[Mathf.Min(ti, trail.Count - 1)];
            segs[i].localPosition = p;
            if (i > 0) { Vector3 d = segs[i - 1].localPosition - p; if (d.sqrMagnitude > 1e-6f) segs[i].localRotation = Quaternion.LookRotation(d); }
            else if (trail.Count > 1) { Vector3 d = trail[0] - trail[1]; if (d.sqrMagnitude > 1e-6f) segs[0].localRotation = Quaternion.LookRotation(d); }
        }
    }
}
