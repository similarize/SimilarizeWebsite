using UnityEngine;

// The four froggies. Names and colours are fixed: never add others.
public static class Froggies
{
    public static readonly string[] Names = { "James", "Jimmy", "Bubbles", "Rexy" };
    public static readonly string[] Hex = { "#3fbf3f", "#a6d832", "#2fb3c4", "#f2582a" };
    public static Color Color(int i) { return Mats.Hex(Hex[i]); }
}

// Procedural frog: squat body, pale belly, big bulging eyes, folded back legs with webbed feet.
// Built from primitives with no colliders. ~1.25 m tall.
public class FrogModel : MonoBehaviour
{
    public Transform bob, head, legL, legR, armL, armR;
    float seed;

    static readonly Color Black = new Color(0.05f, 0.05f, 0.06f);

    static GameObject P(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Color c, Vector3 euler = default(Vector3))
    {
        GameObject g = Mats.Prim(t, parent, pos, scale, Mats.Lit(c));
        g.transform.localRotation = Quaternion.Euler(euler);
        return g;
    }

    static void Limb(Transform parent, Vector3 a, Vector3 b, float thick, Color c)
    {
        Vector3 d = b - a;
        GameObject g = Mats.Prim(PrimitiveType.Capsule, parent, (a + b) * 0.5f, new Vector3(thick, d.magnitude * 0.5f + thick * 0.4f, thick), Mats.Lit(c));
        g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
    }

    // graphics overhaul: smooth stylised frog mesh (Resources/LB/frog.bytes, work/lb-gfx/ff/build_frog.py) with the soft
    // FF/Skin shader; falls back to the primitive frog below if the pack is missing.
    public const float MeshScale = 1.1f;
    bool BuildMesh(Color m)
    {
        LBPack p = LBPack.Get("frog");
        if (p == null || !p.Has("body")) return false;
        seed = Random.value * 10f;
        bob = Mats.Node(transform, "Bob", Vector3.zero);
        Transform body = Mats.Node(bob, "Body", Vector3.zero);
        p.Spawn("body", body, Vector3.zero, MeshScale, m);
        head = Mats.Node(body, "Head", new Vector3(0f, 0.8f, 0.15f));   // no own mesh: the head is part of the body surface
        legL = p.Spawn("legL", body, Vector3.zero, MeshScale, m);
        legR = p.Spawn("legR", body, Vector3.zero, MeshScale, m);
        armL = p.Spawn("armL", body, Vector3.zero, MeshScale, m);
        armR = p.Spawn("armR", body, Vector3.zero, MeshScale, m);
        if (legL == null || legR == null || armL == null || armR == null) { Destroy(bob.gameObject); return false; }
        Mats.SetLayer(gameObject, 9);
        return true;
    }

    public void Build(Color m)
    {
        if (BuildMesh(m)) return;
        seed = Random.value * 10f;
        Color belly = Color.Lerp(m, new Color(1f, 1f, 0.82f), 0.55f);
        Color dark = Color.Lerp(m, Color.black, 0.45f);
        Color spot = Color.Lerp(m, Color.black, 0.25f);
        const float S = 0.72f;   // overall scale of the critter frog design
        bob = Mats.Node(transform, "Bob", Vector3.zero);
        Transform body = Mats.Node(bob, "Body", Vector3.zero);
        body.localScale = Vector3.one * S;

        for (int side = -1; side <= 1; side += 2)
        {
            Transform leg = Mats.Node(body, side < 0 ? "LegL" : "LegR", new Vector3(0.32f * side, 0.55f, -0.12f));
            Vector3 knee = new Vector3(0.14f * side, -0.26f, 0.3f), ankle = new Vector3(0.08f * side, -0.5f, -0.06f);
            Limb(leg, Vector3.zero, knee, 0.2f, m);
            Limb(leg, knee, ankle, 0.12f, m);
            P(PrimitiveType.Sphere, leg, new Vector3(0.1f * side, -0.53f, 0.12f), new Vector3(0.34f, 0.07f, 0.52f), dark, new Vector3(0f, 18f * side, 0f));
            if (side < 0) legL = leg; else legR = leg;
        }
        // squat wide body, belly, back spots
        P(PrimitiveType.Sphere, body, new Vector3(0f, 0.85f, 0f), new Vector3(1.05f, 0.8f, 0.95f), m, new Vector3(-12f, 0f, 0f));
        P(PrimitiveType.Sphere, body, new Vector3(0f, 0.74f, 0.22f), new Vector3(0.78f, 0.56f, 0.55f), belly, new Vector3(-12f, 0f, 0f));
        P(PrimitiveType.Sphere, body, new Vector3(0.22f, 1.12f, -0.18f), new Vector3(0.2f, 0.08f, 0.2f), spot);
        P(PrimitiveType.Sphere, body, new Vector3(-0.18f, 1.08f, -0.3f), new Vector3(0.16f, 0.07f, 0.16f), spot);

        // head: wide flat with a big smile and bulging eyes
        head = Mats.Node(body, "Head", new Vector3(0f, 1.12f, 0.2f));
        P(PrimitiveType.Sphere, head, new Vector3(0f, 0.1f, 0.08f), new Vector3(0.98f, 0.52f, 0.8f), m);
        P(PrimitiveType.Sphere, head, new Vector3(0f, 0.0f, 0.14f), new Vector3(0.8f, 0.28f, 0.62f), belly);
        P(PrimitiveType.Cube, head, new Vector3(0f, 0.03f, 0.47f), new Vector3(0.56f, 0.03f, 0.03f), dark);
        for (int side = -1; side <= 1; side += 2)
        {
            P(PrimitiveType.Sphere, head, new Vector3(0.25f * side, 0.34f, 0.14f), Vector3.one * 0.34f, m);
            P(PrimitiveType.Sphere, head, new Vector3(0.25f * side, 0.37f, 0.23f), Vector3.one * 0.27f, UnityEngine.Color.white);
            P(PrimitiveType.Sphere, head, new Vector3(0.25f * side, 0.38f, 0.35f), Vector3.one * 0.13f, Black);
            P(PrimitiveType.Sphere, head, new Vector3(0.22f * side, 0.42f, 0.4f), Vector3.one * 0.04f, UnityEngine.Color.white);
            P(PrimitiveType.Sphere, head, new Vector3(0.33f * side, -0.02f, 0.38f), new Vector3(0.1f, 0.05f, 0.04f), new Color(1f, 0.55f, 0.55f));
        }
        // short front arms with splayed hands
        for (int side = -1; side <= 1; side += 2)
        {
            Transform arm = Mats.Node(body, side < 0 ? "ArmL" : "ArmR", new Vector3(0.36f * side, 0.82f, 0.32f));
            Limb(arm, Vector3.zero, new Vector3(0.08f * side, -0.42f, 0.12f), 0.13f, m);
            P(PrimitiveType.Sphere, arm, new Vector3(0.1f * side, -0.47f, 0.18f), new Vector3(0.2f, 0.05f, 0.22f), dark);
            if (side < 0) armL = arm; else armR = arm;
        }
        Mats.SetLayer(gameObject, 9);
    }

    // speed: horizontal m/s, air: airborne, seated: in a vehicle, swim: in water
    public void Animate(float speed, bool air, bool seated, bool swim, float dt)
    {
        if (bob == null) return;
        float t = Time.time + seed;
        if (seated)
        {
            bob.localPosition = Vector3.zero;
            bob.localRotation = Quaternion.identity;
            legL.localRotation = legR.localRotation = Quaternion.Euler(-60f, 0f, 0f);
            armL.localRotation = armR.localRotation = Quaternion.Euler(-50f, 0f, 0f);
            head.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.7f) * 3f, Mathf.Sin(t * 0.5f) * 12f, 0f);
            return;
        }
        float move = Mathf.Clamp01(speed / 4f);
        float ph = t * 9f;
        float hop = Mathf.Abs(Mathf.Sin(ph)) * move;
        float y = air ? 0f : hop * 0.28f;
        float tilt = air ? -12f : hop * 10f;
        if (swim) { y = Mathf.Sin(t * 3f) * 0.05f; tilt = -25f; }
        bob.localPosition = new Vector3(0f, y, 0f);
        float breathe = 1f + Mathf.Sin(t * 2.4f) * 0.02f * (1f - move);
        bob.localScale = new Vector3(1f, breathe, 1f);
        bob.localRotation = Quaternion.Euler(tilt + Mathf.Sin(t * 1.1f) * 1.5f * (1f - move), 0f, Mathf.Sin(t * 1.6f) * 2f * (1f - move));
        float legA = air ? 55f : swim ? Mathf.Sin(t * 6f) * 45f + 20f : -hop * 45f;
        legL.localRotation = Quaternion.Euler(legA, 0f, 0f);
        legR.localRotation = Quaternion.Euler(swim ? -legA + 40f : legA, 0f, 0f);
        float armA = air ? -40f : swim ? Mathf.Sin(t * 6f + 1f) * 40f : -hop * 25f;
        armL.localRotation = Quaternion.Euler(armA, 0f, 0f);
        armR.localRotation = Quaternion.Euler(armA, 0f, 0f);
        head.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.9f) * 3f, Mathf.Sin(t * 0.6f) * 8f * (1f - move), 0f);
    }
}
