using System.Collections.Generic;
using UnityEngine;

// Simple ballistic BB simulation using ray steps (no rigidbodies).
public class BBs : MonoBehaviour
{
    public static BBs I;
    public const float Speed = 75f;
    public const float Drop = 2.5f;

    class B { public Vector3 p, v; public Soldier owner; public float t; public Transform vis; }

    readonly List<B> live = new List<B>();
    readonly Stack<Transform> pool = new Stack<Transform>();
    readonly RaycastHit[] hits = new RaycastHit[16];
    Material tracer;

    void Awake() { I = this; tracer = Mats.Unlit(new Color(1f, 0.97f, 0.75f)); }

    public void Fire(Vector3 origin, Vector3 velocity, Soldier owner)
    {
        Transform vis = pool.Count > 0 ? pool.Pop() : null;
        if (vis == null)
        {
            vis = Mats.Prim(PrimitiveType.Sphere, transform, Vector3.zero, Vector3.one * 0.05f, tracer, false).transform;
            var r = vis.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        vis.gameObject.SetActive(true);
        vis.position = origin;
        live.Add(new B { p = origin, v = velocity, owner = owner, t = 0f, vis = vis });
    }

    public void ClearAll()
    {
        foreach (var b in live) Recycle(b.vis);
        live.Clear();
    }

    void Recycle(Transform t)
    {
        if (t == null) return;
        t.gameObject.SetActive(false);
        pool.Push(t);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        for (int i = live.Count - 1; i >= 0; i--)
        {
            B b = live[i];
            Vector3 np = b.p + b.v * dt;
            b.v += Vector3.down * Drop * dt;
            Vector3 seg = np - b.p;
            float dist = seg.magnitude;
            bool done = false;
            if (dist > 0.0001f)
            {
                int n = Physics.RaycastNonAlloc(b.p, seg / dist, hits, dist, ~0, QueryTriggerInteraction.Collide);
                int best = -1; float bd = float.MaxValue;
                for (int k = 0; k < n; k++)
                {
                    Soldier hs = hits[k].collider.GetComponentInParent<Soldier>();
                    if (hs != null && hs == b.owner) continue;
                    if (hits[k].distance < bd) { bd = hits[k].distance; best = k; }
                }
                if (best >= 0)
                {
                    RaycastHit h = hits[best];
                    Balloon bal = h.collider.GetComponent<Balloon>();
                    if (bal != null && bal.owner != null) bal.owner.PopBalloon(bal, b.owner);
                    else { FX.Puff(h.point, h.normal); Sfx.OnImpact(h.point, h.collider); }
                    done = true;
                }
            }
            b.p = np;
            b.t += dt;
            if (b.t > 1.6f) done = true;
            if (done) { Recycle(b.vis); live.RemoveAt(i); }
            else b.vis.position = np;
        }
    }
}
