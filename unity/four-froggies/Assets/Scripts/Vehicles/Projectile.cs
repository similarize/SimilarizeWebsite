using System.Collections.Generic;
using UnityEngine;

// Tank shells (ballistic) and missiles (straight, smoke trail). Both explode on contact.
public class Projectile : MonoBehaviour
{
    bool missile, dead;
    float life;
    Rigidbody rb;
    Vehicle owner;

    public static void Spawn(bool isMissile, Vector3 pos, Vector3 velocity, Vehicle from)
    {
        var go = new GameObject(isMissile ? "Missile" : "Shell");
        go.layer = Vehicle.ProjectileLayer;
        go.transform.position = pos;
        go.transform.rotation = Quaternion.LookRotation(velocity.normalized);
        if (isMissile)
        {
            Mats.Prim(PrimitiveType.Cylinder, go.transform, Vector3.zero, new Vector3(0.22f, 0.6f, 0.22f), new Vector3(90f, 0f, 0f), Mats.Lit(new Color(0.85f, 0.85f, 0.8f)));
            Mats.Prim(PrimitiveType.Sphere, go.transform, new Vector3(0f, 0f, 0.6f), new Vector3(0.22f, 0.22f, 0.35f), Mats.Lit(new Color(0.9f, 0.2f, 0.15f)));
            for (int k = 0; k < 4; k++)
            {
                GameObject fin = Mats.Prim(PrimitiveType.Cube, go.transform, new Vector3(0f, 0f, -0.5f), new Vector3(0.02f, 0.4f, 0.25f), Mats.Lit(new Color(0.3f, 0.3f, 0.3f)));
                fin.transform.localRotation = Quaternion.Euler(0f, 0f, k * 90f);
            }
        }
        else
        {
            Mats.Prim(PrimitiveType.Sphere, go.transform, Vector3.zero, new Vector3(0.28f, 0.28f, 0.6f), Mats.Unlit(new Color(1f, 0.85f, 0.4f)));
        }
        Mats.SetLayer(go, Vehicle.ProjectileLayer);
        Mats.NoShadows(go);
        var col = go.AddComponent<SphereCollider>();
        col.radius = 0.2f;
        var p = go.AddComponent<Projectile>();
        p.missile = isMissile;
        p.owner = from;
        p.rb = go.AddComponent<Rigidbody>();
        p.rb.mass = isMissile ? 20f : 15f;
        p.rb.useGravity = !isMissile;
        p.rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        p.rb.interpolation = RigidbodyInterpolation.Interpolate;
        p.rb.velocity = velocity;
        if (from != null)
            foreach (Collider c in from.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(col, c);
    }

    void FixedUpdate()
    {
        if (dead) return;
        life += Time.fixedDeltaTime;
        if (missile)
        {
            rb.velocity = rb.velocity.normalized * Mathf.Min(rb.velocity.magnitude + 30f * Time.fixedDeltaTime, 60f);
            FX.Smoke(transform.position - transform.forward * 0.6f, 0.6f, new Color(0.75f, 0.75f, 0.75f, 0.7f));
            FX.Flame(transform.position - transform.forward * 0.6f, -transform.forward);
        }
        if (rb.velocity.sqrMagnitude > 1f) transform.rotation = Quaternion.LookRotation(rb.velocity.normalized);
        Vector3 p = transform.position;
        if (p.y < Layout.WaterY && Layout.InPond(p.x, p.z)) { FX.Splash(p, 30); Explode(p); return; }
        if (life > (missile ? 5f : 7f)) Explode(p);
    }

    void OnCollisionEnter(Collision c)
    {
        if (dead) return;
        Vector3 p = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
        Explode(p);
    }

    void Explode(Vector3 p)
    {
        dead = true;
        Boom.At(p, missile ? 9f : 7.5f, missile ? 1.25f : 1f, missile ? 40f : 30f, owner);
        Destroy(gameObject);
    }
}

public static class Boom
{
    static readonly HashSet<Rigidbody> seen = new HashSet<Rigidbody>();

    public static void At(Vector3 p, float radius, float power) { At(p, radius, power, 0f, null); }

    // ffu14: damage (falls off with distance) to mechs, vehicles and props; froggies only get knocked about
    static readonly HashSet<Object> hitOnce = new HashSet<Object>();
    public static void At(Vector3 p, float radius, float power, float damage, Vehicle by, Color? flash = null)
    {
        FX.Boom(p, power);
        if (flash.HasValue) FX.Sparkle(p, flash.Value, 20);
        if (damage > 0f)
        {
            hitOnce.Clear();
            foreach (Collider c in Physics.OverlapSphere(p, radius * 1.4f, ~0, QueryTriggerInteraction.Ignore))
            {
                Vector3 cp = c.ClosestPoint(p);
                float k = 1f - Mathf.Clamp01((cp - p).magnitude / (radius * 1.4f));
                float d = damage * (0.35f + 0.65f * k);
                StoryMech sm = c.GetComponentInParent<StoryMech>();
                if (sm != null) { if (hitOnce.Add(sm)) sm.Damage(d, cp, by); continue; }
                Vehicle v = c.GetComponentInParent<Vehicle>();
                if (v != null)
                {
                    if (v != by && VehicleWreck.Eligible(v) && hitOnce.Add(v)) v.Wreck.Damage(d * 1.6f, cp);
                    continue;
                }
                Wreckable w = c.GetComponent<Wreckable>();
                if (w != null && hitOnce.Add(w)) w.Blast(p, d);
            }
        }
        Sfx.PlayAt(Sfx.Boom, p, Mathf.Clamp(power, 0.5f, 1f), 140f, Random.Range(0.85f, 1.1f));
        var lg = new GameObject("BoomLight");
        lg.transform.position = p + Vector3.up;
        var l = lg.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.7f, 0.35f);
        l.range = radius * 3f;
        l.intensity = 3f;
        l.shadows = LightShadows.None;
        Object.Destroy(lg, 0.18f);

        seen.Clear();
        foreach (Collider c in Physics.OverlapSphere(p, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            Rigidbody r = c.attachedRigidbody;
            if (r != null && !r.isKinematic && seen.Add(r))
            {
                float scale = Mathf.Clamp(900f / r.mass, 0.18f, 1.6f) * power;
                r.AddExplosionForce(14f * scale, p, radius, 1.2f, ForceMode.VelocityChange);
                continue;
            }
            Frog f = c.GetComponent<Frog>();
            if (f != null)
            {
                Vector3 d = f.Center - p;
                float k = 1f - Mathf.Clamp01(d.magnitude / radius);
                d.y = Mathf.Max(d.y, 0.5f);
                f.Knock(d.normalized * (6f + 12f * k) * power);
            }
        }
        Game.Shake(p, power);
    }
}
