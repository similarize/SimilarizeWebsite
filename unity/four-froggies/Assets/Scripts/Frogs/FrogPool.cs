using UnityEngine;

// ffu29 backyard pool float (Bill: "The pool out back should be something the frog can get in and swim. Make it a relaxing
// swim, like he's sprawled out, maybe swimming on his back.")
// The pool (Layout.PoolC 16 x 9 m, Ranch.Pool) is a water plane 0.12 m over the flat yard inside a 0.3 m coping, so there
// is no basin to sink into: a froggy that walks / hops over the coping into the water rect starts FLOATING. The character
// controller stays on the pool floor (no terrain change, no new colliders) and only the model is laid back at the surface:
// on his back, head a little up, arms and legs sprawled, a slow gentle bob + roll, lazy alternating backstroke arms while
// drifting (head first, ~1.4 m/s, very soft acceleration), soft stroke swishes + expanding ripple rings. Hop / use (A,
// SPACE, E, tap) or pushing against the side for 0.6 s climbs out over the nearest edge (or up the ladder at the east end).
// AI froggies now and then wander over for a float (Think picks the pool ~7% of the time) and climb out after 14-30 s.
// Not synced online (guests see the owner's froggy standing in the pool).
public partial class Frog
{
    bool floating;
    float floatT, floatFor, poolCool, edgePushT, strokeT, rippleT;
    public bool Floating { get { return floating; } }
    public bool demoFloatStay;   // poolswim demo: AI froggy keeps floating

    const float FloatSpeed = 1.4f;
    static bool poolKitBuilt;

    public static bool InPool(float x, float z, float margin)
    {
        Vector2 c = Layout.PoolC, s = Layout.PoolSize * 0.5f;
        return Mathf.Abs(x - c.x) < s.x - margin && Mathf.Abs(z - c.y) < s.y - margin;
    }
    public static Vector3 PoolPoint()
    {
        Vector2 c = Layout.PoolC, s = Layout.PoolSize * 0.5f;
        float x = c.x + Random.Range(-s.x + 1.5f, s.x - 1.5f), z = c.y + Random.Range(-s.y + 1.2f, s.y - 1.2f);
        return new Vector3(x, Ranch.GY(x, z), z);
    }
    static float WaterY { get { return Ranch.GY(Layout.PoolC.x, Layout.PoolC.y) + 0.12f; } }

    // ladder at the east end (decor: rails + treads over the coping) - the "climb out" spot the prompt mentions
    static void BuildPoolKit()
    {
        poolKitBuilt = true;
        try
        {
            Vector2 c = Layout.PoolC, s = Layout.PoolSize;
            float y = Ranch.GY(c.x, c.y);
            var root = new GameObject("PoolLadder").transform;
            root.position = new Vector3(c.x + s.x * 0.5f - 0.15f, y, c.y + 2.2f);
            Material steel = Mats.Steel(new Color(0.82f, 0.84f, 0.86f));
            for (int k = -1; k <= 1; k += 2)
            {
                // rail: up out of the water, over the coping, down onto the deck (3 straight pieces)
                Vector3 a = new Vector3(-0.35f, 0.05f, 0.32f * k), b = new Vector3(-0.35f, 1.05f, 0.32f * k), d = new Vector3(0.45f, 1.05f, 0.32f * k), e = new Vector3(0.85f, 0.25f, 0.32f * k);
                foreach (var seg in new[] { new[] { a, b }, new[] { b, d }, new[] { d, e } })
                {
                    var g = Mats.Prim(PrimitiveType.Cylinder, root, (seg[0] + seg[1]) * 0.5f, new Vector3(0.05f, (seg[1] - seg[0]).magnitude * 0.5f, 0.05f), steel, false);
                    g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (seg[1] - seg[0]).normalized);
                }
            }
            for (int i = 0; i < 2; i++) Mats.Prim(PrimitiveType.Cube, root, new Vector3(-0.32f, 0.12f + i * 0.2f, 0f), new Vector3(0.12f, 0.03f, 0.6f), steel, false);
            // a floating ring (lounger) drifting in the deep end, just for the look
            var ring = Mats.Prim(PrimitiveType.Cylinder, null, new Vector3(c.x - 4.5f, y + 0.14f, c.y - 1.5f), new Vector3(0.8f, 0.06f, 0.8f), Mats.Shiny(new Color(1f, 0.42f, 0.35f)), false);
            ring.name = "PoolFloatie";
            var hole = Mats.Prim(PrimitiveType.Cylinder, ring.transform, new Vector3(0f, 0.6f, 0f), new Vector3(0.5f, 0.5f, 0.5f), Mats.Lit(new Color(0.3f, 0.75f, 0.85f)), false);
            hole.name = "Hole";
        }
        catch (System.Exception ex) { Debug.LogWarning("FrogPool: kit " + ex.Message); }
    }

    public void StartFloat()
    {
        if (floating) return;
        floating = true; floatT = 0f; edgePushT = 0f; strokeT = Random.Range(0f, 1f);
        floatFor = Random.Range(14f, 30f);
        planar *= 0.3f; vel = Vector3.zero;
        if (chute) SetChute(false);
        Vector3 p = transform.position;
        FX.Splash(new Vector3(p.x, WaterY + 0.1f, p.z), 10);
        if (human) Sfx.Play(Sfx.Splash, 0.55f, 0.9f); else Sfx.PlayAt(Sfx.Splash, p, 0.45f, 35f, 0.9f);
        if (human) Toast("Ahh... floating on your back", 2.5f);
        if (human || demoFloatStay) Debug.Log("FFPOOL " + nick + " floating at " + p.ToString("0.0"));
    }

    void EndFloat()
    {
        floating = false;
        if (model != null) { model.transform.localRotation = Quaternion.identity; model.transform.localPosition = Vector3.zero; }
    }

    // climb out over the nearest side (or the ladder end) onto the deck
    void ClimbOutOfPool()
    {
        Vector2 c = Layout.PoolC, s = Layout.PoolSize * 0.5f;
        Vector3 p = transform.position;
        float dx = p.x - c.x, dz = p.z - c.y;
        float ex = s.x - Mathf.Abs(dx), ez = s.y - Mathf.Abs(dz);
        Vector3 to; float ny;
        if (ex < ez) { to = new Vector3(c.x + Mathf.Sign(dx) * (s.x + 1.5f), 0f, p.z); ny = dx > 0f ? 90f : 270f; }
        else { to = new Vector3(p.x, 0f, c.y + Mathf.Sign(dz) * (s.y + 1.5f)); ny = dz > 0f ? 0f : 180f; }
        to.y = Ranch.GY(to.x, to.z) + 0.1f;
        EndFloat();
        FX.Splash(new Vector3(p.x, WaterY + 0.1f, p.z), 8);
        if (human) Sfx.Play(Sfx.Splash, 0.45f, 1.15f); else Sfx.PlayAt(Sfx.Splash, p, 0.35f, 30f, 1.15f);
        Teleport(to);
        yaw = ny; transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        vel.y = 3.5f;
        poolCool = 1.2f; exitCool = 0.4f;
        if (human || demoFloatStay) Debug.Log("FFPOOL " + nick + " climbed out at " + to.ToString("0.0"));
    }

    // called every frame from Update before walking / climbing; true = the froggy is floating (handled here)
    bool PoolTick(float dt)
    {
        if (!poolKitBuilt) BuildPoolKit();
        poolCool -= dt;
        Vector3 p = transform.position;
        bool ok = world == WorldId.Ranch && vehicle == null && passengerOf == null && remote == null && jetOf == null;
        if (!floating)
        {
            if (!ok || poolCool > 0f || Climbing) return false;
            if (!InPool(p.x, p.z, 0.25f) || p.y > Ranch.GY(p.x, p.z) + 0.6f) return false;
            StartFloat();
        }
        if (!ok || !InPool(p.x, p.z, -0.4f)) { EndFloat(); return false; }   // sent elsewhere / pushed out
        floatT += dt;

        // ---- input: humans drift with the stick; AI drift lazily and climb out after a while ----
        Vector2 mv = input.move;
        bool leave = human && (input.hop || input.use) && floatT > 0.4f;
        if (!human)
        {
            float a = Time.time * 0.13f + id * 1.7f;
            mv = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.35f * Mathf.Clamp01(Mathf.Sin(Time.time * 0.21f + id) + 0.4f);
            camYaw = 0f;
            if (!demoFloatStay && floatT > floatFor) leave = true;
        }
        if (leave) { ClimbOutOfPool(); return true; }

        Vector3 wish = Vector3.zero;
        if (mv.sqrMagnitude > 0.0001f)
        {
            wish = Quaternion.Euler(0f, camYaw, 0f) * new Vector3(mv.x, 0f, mv.y);
            if (wish.sqrMagnitude > 1f) wish.Normalize();
        }
        planar = Vector3.MoveTowards(planar, wish * FloatSpeed, (wish.sqrMagnitude > 0.01f ? 1.3f : 0.6f) * dt);   // drifts on
        float sp = planar.magnitude;
        if (sp > 0.15f)
        {
            // backstroke: head first, so the body's "forward" (the face, now looking up) points away from the travel
            float target = Mathf.Atan2(-planar.x, -planar.z) * Mathf.Rad2Deg;
            yaw = Mathf.MoveTowardsAngle(yaw, target, 50f * dt);
        }
        else yaw += 4f * dt;   // lazy spin while idle
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        // keep inside the water rect; pushing into a side for 0.6 s climbs out there
        Vector2 c = Layout.PoolC, hs = Layout.PoolSize * 0.5f - new Vector2(0.55f, 0.55f);
        vel.y = cc.isGrounded ? -2f : vel.y - Gravity * dt;
        cc.Move((planar + new Vector3(0f, vel.y, 0f)) * dt);
        Vector3 q = transform.position;
        bool atEdge = false;
        if (Mathf.Abs(q.x - c.x) > hs.x) { q.x = c.x + Mathf.Sign(q.x - c.x) * hs.x; planar.x *= -0.2f; atEdge = Vector3.Dot(wish, new Vector3(Mathf.Sign(q.x - c.x), 0f, 0f)) > 0.6f; }
        if (Mathf.Abs(q.z - c.y) > hs.y) { q.z = c.y + Mathf.Sign(q.z - c.y) * hs.y; planar.z *= -0.2f; atEdge |= Vector3.Dot(wish, new Vector3(0f, 0f, Mathf.Sign(q.z - c.y))) > 0.6f; }
        if (q != transform.position) { bool was = cc.enabled; cc.enabled = false; transform.position = q; cc.enabled = was; }
        edgePushT = atEdge && human ? edgePushT + dt : 0f;
        if (edgePushT > 0.6f) { ClimbOutOfPool(); return true; }

        // ---- the look: laid back at the surface, sprawled, bobbing, lazy strokes ----
        float rate = 0.32f + Mathf.Clamp01(sp / FloatSpeed) * 0.3f;   // stroke cycles per second
        float prevPh = strokeT;
        strokeT += dt * rate;
        float t = Time.time + id * 1.3f;
        if (model != null)
        {
            float bobY = Mathf.Sin(t * 1.25f) * 0.035f;
            model.transform.localRotation = Quaternion.Euler(-78f + Mathf.Sin(t * 0.8f) * 2f, 0f, Mathf.Sin(t * 0.6f) * 4f);
            model.transform.localPosition = new Vector3(0f, (WaterY - q.y) - 0.18f + bobY, 0.55f);
            model.AnimateFloat(strokeT, Mathf.Clamp01(sp / FloatSpeed), t);
        }
        // a soft swish + a ripple each half stroke (one arm enters the water)
        if (Mathf.Floor(prevPh * 2f) != Mathf.Floor(strokeT * 2f))
        {
            Vector3 hand = transform.position + transform.right * (Mathf.Floor(strokeT * 2f) % 2 == 0 ? 0.6f : -0.6f) - transform.forward * 0.6f;
            hand.y = WaterY + 0.01f;
            PoolFx.Ripple(hand, 0.9f + sp * 0.4f);
            AudioClip sw = PoolFx.Swish;
            if (sw != null) { if (human) Sfx.Play(sw, 0.16f + sp * 0.08f, Random.Range(0.9f, 1.08f)); else Sfx.PlayAt(sw, hand, 0.14f, 22f, Random.Range(0.9f, 1.08f)); }
        }
        rippleT -= dt;
        if (rippleT <= 0f) { rippleT = Random.Range(1.4f, 2.6f); Vector3 b = transform.position; b.y = WaterY + 0.01f; PoolFx.Ripple(b, 1.6f); }

        if (human)
            prompt = "Floating on your back - " + (inputKind == InputKind.Gamepad ? "L-stick drift  |  A: climb out" : inputKind == InputKind.Touch ? "stick: drift  |  A: climb out" : "WASD drift  |  SPACE / E: climb out") + "  (or swim into the side / the ladder)";
        return true;
    }
}

// shared pool ripples (6 expanding rings, reused) + the synthesised stroke swish
public static class PoolFx
{
    static Transform[] rings;
    static Material[] mats;
    static float[] age, size;
    static int next;
    static AudioClip swish;
    static bool hooked;

    public static AudioClip Swish
    {
        get
        {
            if (swish != null) return swish;
            // 0.9 s soft water swish: low-passed noise with a slow swell, a gentle gurgle wobble, quiet tail
            const int rate = 22050; int n = (int)(rate * 0.9f);
            var d = new float[n]; var r = new System.Random(29); float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float tt = i / (float)rate;
                float env = Mathf.Sin(Mathf.Clamp01(tt / 0.9f) * Mathf.PI); env *= env;
                float nz = (float)(r.NextDouble() * 2.0 - 1.0);
                float k = 0.08f + 0.06f * Mathf.Sin(tt * 2f * Mathf.PI * 3.5f);
                lp += (nz - lp) * k; lp2 += (lp - lp2) * 0.25f;
                d[i] = lp2 * env * 2.2f * (1f + 0.3f * Mathf.Sin(tt * 2f * Mathf.PI * 7f));
            }
            swish = AudioClip.Create("pool_swish", n, 1, rate, false);
            swish.SetData(d, 0);
            return swish;
        }
    }

    static void Init()
    {
        rings = new Transform[6]; mats = new Material[6]; age = new float[6]; size = new float[6];
        for (int i = 0; i < 6; i++)
        {
            var g = new GameObject("PoolRipple");
            g.AddComponent<MeshFilter>().sharedMesh = Showroom.Ring();
            var mr = g.AddComponent<MeshRenderer>();
            mats[i] = new Material(Mats.Fx != null ? Mats.Fx : Mats.Unlit(Color.white)) { renderQueue = 3001 };
            mats[i].color = new Color(1f, 1f, 1f, 0f);
            mr.sharedMaterial = mats[i];
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            g.SetActive(false);
            rings[i] = g.transform; age[i] = 99f;
        }
    }

    public static void Ripple(Vector3 p, float s)
    {
        if (rings == null) Init();
        if (!hooked) { hooked = true; var go = new GameObject("PoolFxTick"); go.AddComponent<PoolFxTick>(); }
        int i = next; next = (next + 1) % rings.Length;
        rings[i].position = p; rings[i].localScale = Vector3.one * 0.2f;
        age[i] = 0f; size[i] = s;
        rings[i].gameObject.SetActive(true);
    }

    public static void Tick(float dt)
    {
        if (rings == null) return;
        for (int i = 0; i < rings.Length; i++)
        {
            if (age[i] > 1.8f) continue;
            age[i] += dt;
            float k = Mathf.Clamp01(age[i] / 1.8f);
            rings[i].localScale = new Vector3(1f, 1f, 1f) * (0.2f + size[i] * (1f - (1f - k) * (1f - k)));
            mats[i].color = new Color(1f, 1f, 1f, 0.32f * (1f - k));
            if (age[i] > 1.8f) rings[i].gameObject.SetActive(false);
        }
    }
}

public class PoolFxTick : MonoBehaviour { void Update() { PoolFx.Tick(Mathf.Min(Time.deltaTime, 0.1f)); } }
