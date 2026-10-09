using System.Collections.Generic;
using UnityEngine;

// ffu14 combat: big chest shots from the story mechs (plasma cannon bolts + homing chest missiles), damage from every
// explosion (mech / tank shells and missiles), props that blow apart and come back, vehicles that get wrecked
// (charred, smoking, the driver bounces out unhurt) and roll back to their spot. Kid-friendly: nobody gets hurt,
// froggies only get knocked around.
public class MechShot : MonoBehaviour
{
    StoryMech owner;
    int kind;           // 1 plasma bolt, 2 chest missile
    Vehicle target;
    Vector3 vel;
    float life, scale;
    bool dead;
    TrailRenderer trail;

    public static void Spawn(StoryMech from, int kind, Vector3 pos, Vector3 velocity, Vehicle target)
    {
        float H = from != null ? from.height : 12f;
        var go = new GameObject(kind == 1 ? "PlasmaBolt" : "ChestMissile");
        go.transform.position = pos;
        go.transform.rotation = Quaternion.LookRotation(velocity.normalized);
        var s = go.AddComponent<MechShot>();
        s.owner = from; s.kind = kind; s.vel = velocity; s.target = target;
        s.scale = Mathf.Clamp(H / 12f, 1f, 6f);
        float k = s.scale;
        if (kind == 1)
        {
            Mats.Prim(PrimitiveType.Sphere, go.transform, Vector3.zero, new Vector3(0.9f, 0.9f, 2.6f) * k, Mats.Unlit(new Color(0.55f, 0.95f, 1f)));
            Mats.Prim(PrimitiveType.Sphere, go.transform, Vector3.zero, new Vector3(0.5f, 0.5f, 1.8f) * k, Mats.Unlit(Color.white));
            s.trail = go.AddComponent<TrailRenderer>();
            s.trail.time = 0.18f; s.trail.startWidth = 0.8f * k; s.trail.endWidth = 0.05f;
            s.trail.material = Mats.Unlit(new Color(0.4f, 0.85f, 1f));
            Sfx.PlayAt(Sfx.Shell, pos, 0.85f, 160f, 1.7f);
            Sfx.PlayAt(Sfx.EngServo != null ? Sfx.EngServo : Sfx.Missile, pos, 0.35f, 120f, 2.2f);
            FX.Sparkle(pos, new Color(0.5f, 0.95f, 1f), 10);
        }
        else
        {
            Mats.Prim(PrimitiveType.Cylinder, go.transform, Vector3.zero, new Vector3(0.45f, 1.2f, 0.45f) * k, new Vector3(90f, 0f, 0f), Mats.Lit(new Color(0.9f, 0.9f, 0.86f)));
            Mats.Prim(PrimitiveType.Sphere, go.transform, new Vector3(0f, 0f, 1.2f * k), new Vector3(0.45f, 0.45f, 0.7f) * k, Mats.Lit(new Color(0.95f, 0.25f, 0.15f)));
            for (int f = 0; f < 4; f++)
            {
                var fin = Mats.Prim(PrimitiveType.Cube, go.transform, new Vector3(0f, 0f, -1f * k), new Vector3(0.05f, 0.9f, 0.5f) * k, Mats.Lit(new Color(0.3f, 0.3f, 0.32f)));
                fin.transform.localRotation = Quaternion.Euler(0f, 0f, f * 90f);
            }
            Mats.Prim(PrimitiveType.Sphere, go.transform, new Vector3(0f, 0f, -1.4f * k), new Vector3(0.5f, 0.5f, 0.9f) * k, Mats.Unlit(new Color(1f, 0.75f, 0.3f)));
            s.trail = go.AddComponent<TrailRenderer>();
            s.trail.time = 0.6f; s.trail.startWidth = 0.7f * k; s.trail.endWidth = 1.6f * k;
            s.trail.material = Mats.Unlit(new Color(0.85f, 0.85f, 0.85f));
            Sfx.PlayAt(Sfx.Missile, pos, 0.95f, 180f, 0.75f);
        }
        if (s.trail != null) { s.trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; s.trail.receiveShadows = false; }
        Mats.NoShadows(go);
        FX.Muzzle(pos, velocity.normalized);
    }

    void Update()
    {
        if (dead) return;
        float dt = Mathf.Min(Time.deltaTime, StoryMech.DtCap);
        life += dt;
        if (kind == 2)
        {
            // chest missile: speeds up, homes gently on its target
            float sp = Mathf.Min(vel.magnitude + 70f * dt, 95f + scale * 8f);
            Vector3 dir = vel.normalized;
            if (target != null && target.body != null && target.body.enabled && life > 0.25f)
                dir = Vector3.RotateTowards(dir, (target.body.bounds.center - transform.position).normalized, 2.2f * dt, 0f);
            else if (life > 0.35f) dir = Vector3.RotateTowards(dir, new Vector3(dir.x, Mathf.Min(dir.y, -0.15f), dir.z).normalized, 0.8f * dt, 0f);
            vel = dir * sp;
            if (Random.value < 0.8f) FX.Smoke(transform.position - dir * 1.4f * scale, 0.9f * scale, new Color(0.8f, 0.8f, 0.8f, 0.6f));
            FX.Flame(transform.position - dir * 1.4f * scale, -dir);
        }
        else vel += Vector3.down * 4f * dt;
        Vector3 p0 = transform.position, step = vel * dt;
        RaycastHit hit;
        int mask = ~((1 << 2) | (1 << Vehicle.ProjectileLayer) | (1 << Vehicle.FrogLayer));
        if (Physics.SphereCast(p0, 0.35f * scale, step.normalized, out hit, step.magnitude + 0.1f, mask, QueryTriggerInteraction.Ignore)
            && (owner == null || !hit.collider.transform.IsChildOf(owner.transform)))
        { Explode(hit.point); return; }
        transform.position = p0 + step;
        if (vel.sqrMagnitude > 1f) transform.rotation = Quaternion.LookRotation(vel);
        Vector3 p = transform.position;
        if (Mathf.Abs(p.x) <= Layout.Half && Mathf.Abs(p.z) <= Layout.Half && p.y < Ranch.GY(p.x, p.z)) { Explode(new Vector3(p.x, Ranch.GY(p.x, p.z), p.z)); return; }
        if (p.y < Layout.WaterY && Layout.InPond(p.x, p.z)) { FX.Splash(p, 30); Explode(p); return; }
        if (life > 6f) Explode(p);
    }

    void Explode(Vector3 p)
    {
        dead = true;
        if (kind == 1) Boom.At(p, 5f + scale * 2.2f, 1f + scale * 0.25f, 24f, owner, new Color(0.5f, 0.95f, 1f));
        else Boom.At(p, 8f + scale * 3f, 1.3f + scale * 0.35f, 42f, owner);
        if (trail != null) { trail.transform.SetParent(null, true); trail.emitting = false; Destroy(trail.gameObject, trail.time + 0.1f); foreach (Transform c in trail.transform) c.gameObject.SetActive(false); }
        else Destroy(gameObject);
        enabled = false;
    }
}

// a loose prop (crate, hay bale, cone, barrel...) that blows apart and comes back where it stood
public class Wreckable : MonoBehaviour
{
    Vector3 home; Quaternion homeRot; Vector3 homeScale;
    float downT = -1f;
    Renderer rend; Collider col; Rigidbody rb;
    public static readonly List<Wreckable> All = new List<Wreckable>();

    void Awake() { home = transform.position; homeRot = transform.rotation; homeScale = transform.localScale; rend = GetComponent<Renderer>(); col = GetComponent<Collider>(); rb = GetComponent<Rigidbody>(); All.Add(this); }
    void OnDestroy() { All.Remove(this); }

    public void Blast(Vector3 from, float dmg)
    {
        if (downT >= 0f || dmg < 8f) return;
        downT = 18f + Random.value * 6f;
        Color c = rend != null && rend.sharedMaterial != null ? rend.sharedMaterial.color : Color.gray;
        Material m = Mats.Lit(Color.Lerp(c, Color.black, 0.35f));
        Vector3 p = transform.position;
        float s = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y) * 0.4f;
        for (int i = 0; i < (Look.Mobile ? 3 : 5); i++)
        {
            var g = Mats.Prim(PrimitiveType.Cube, null, p + Random.insideUnitSphere * s, Vector3.one * s * Random.Range(0.4f, 0.8f), m, true);
            g.layer = Vehicle.PropLayer;
            var r = g.AddComponent<Rigidbody>(); r.mass = 5f;
            r.velocity = (p - from).normalized * 9f + Random.insideUnitSphere * 5f + Vector3.up * 7f;
            r.angularVelocity = Random.insideUnitSphere * 8f;
            Destroy(g, 7f);
        }
        FX.Smoke(p, s * 3f + 1f, new Color(0.25f, 0.25f, 0.25f, 0.7f));
        if (rend != null) rend.enabled = false;
        if (col != null) col.enabled = false;
        if (rb != null) { rb.isKinematic = true; }
    }

    void Update()
    {
        if (downT < 0f) return;
        downT -= Time.deltaTime;
        if (downT > 0f) return;
        downT = -1f;
        transform.position = home; transform.rotation = homeRot; transform.localScale = homeScale;
        if (rend != null) rend.enabled = true;
        if (col != null) col.enabled = true;
        if (rb != null) { rb.isKinematic = false; rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; rb.Sleep(); }
        FX.Sparkle(home + Vector3.up * 0.5f, new Color(1f, 1f, 0.7f), 8);
    }
}

// wrecked ordinary vehicles: charred materials, smoke + small flames, can't be driven, then back at their spot
public class VehicleWreck : MonoBehaviour
{
    public Vehicle v;
    public float hp = 100f;
    float downT = -1f;
    Renderer[] rends; Material[][] orig;
    static Material charred;

    public bool Down { get { return downT >= 0f; } }
    public float TimeLeft { get { return Mathf.Max(0f, downT); } }

    public static bool Eligible(Vehicle v)
    {
        return v != null && !(v is StoryMech) && !(v is Starship) && !(v is Submarine) && !v.hasWorldBounds && v.rb != null;
    }

    public void Damage(float d, Vector3 at)
    {
        if (Down || d <= 0f) return;
        hp -= d;
        if (hp > 0f) { FX.Smoke(at, 1.5f, new Color(0.3f, 0.3f, 0.3f, 0.6f)); return; }
        downT = 16f;
        Frog drv = v.driver;
        if (drv != null && !drv.netPuppet)
        {
            drv.ExitVehicle();
            drv.Knock(Vector3.up * 8f + Random.insideUnitSphere * 3f);
            drv.Toast(v.Title + " got wrecked! You're fine - it's back at its spot soon", 3.5f);
        }
        if (charred == null) charred = Mats.Lit(new Color(0.09f, 0.08f, 0.08f));
        rends = v.GetComponentsInChildren<Renderer>();
        orig = new Material[rends.Length][];
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] is ParticleSystemRenderer || rends[i] is TrailRenderer) continue;
            orig[i] = rends[i].sharedMaterials;
            var arr = new Material[orig[i].Length];
            for (int k = 0; k < arr.Length; k++) arr[k] = charred;
            rends[i].sharedMaterials = arr;
        }
        Vector3 c = v.transform.position + Vector3.up;
        FX.Boom(c, 1.6f);
        Sfx.PlayAt(Sfx.Boom, c, 1f, 160f, 0.8f);
        if (!v.rb.isKinematic) v.rb.AddForce(Vector3.up * 7f + Random.insideUnitSphere * 2f, ForceMode.VelocityChange);
        if (!v.rb.isKinematic) v.rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.VelocityChange);
    }

    void Update()
    {
        if (!Down) return;
        float dt = Time.deltaTime;
        downT -= dt;
        Vector3 c = v.transform.position + Vector3.up * 1.2f;
        if (Random.value < dt * 8f) FX.Smoke(c + Random.insideUnitSphere * 0.8f, 2.2f, new Color(0.15f, 0.15f, 0.15f, 0.7f));
        if (downT > 6f && Random.value < dt * 10f) FX.Flame(c + Random.insideUnitSphere * 0.6f, Vector3.up);
        if (v.driver != null && !v.driver.netPuppet) v.driver.ExitVehicle();
        if (downT > 0f) return;
        downT = -1f; hp = 100f;
        for (int i = 0; i < rends.Length; i++) if (rends[i] != null && orig[i] != null) rends[i].sharedMaterials = orig[i];
        v.ResetHome();
        FX.Sparkle(v.transform.position + Vector3.up * 1.5f, new Color(1f, 1f, 0.7f), 16);
    }
}
