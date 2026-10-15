using System.Collections.Generic;
using UnityEngine;

// bb6: floating power-up balloons. Shoot one (or run into it) to claim it.
// Shield (blue): your balloons can't be popped for 8 s. Speed (green): run 50% faster for 8 s.
// Magnet (purple): your BBs curve toward nearby enemy balloons for 8 s. Golden (rare): regrows a lost balloon.
public enum PowerKind { Shield, Speed, Magnet, Golden }

public class PowerBalloon : MonoBehaviour
{
    public PowerKind kind;
    public Vector3 home;
    public bool claimed;
    float t0;
    Transform ring;

    public static readonly Color[] Col = { new Color(0.35f, 0.75f, 1f), new Color(0.4f, 1f, 0.4f), new Color(0.78f, 0.45f, 1f), new Color(1f, 0.8f, 0.15f) };
    public static readonly string[] Name = { "SHIELD", "SPEED", "MAGNET", "GOLDEN BALLOON" };

    public void Setup(PowerKind k, Vector3 pos)
    {
        kind = k; home = pos; t0 = Random.value * 6f;
        transform.position = pos;
        float sc = k == PowerKind.Golden ? 1.25f : 1f;
        var bg = new GameObject("Body");
        bg.transform.SetParent(transform, false);
        bg.transform.localScale = new Vector3(0.75f, 0.9f, 0.75f) * sc;
        bg.AddComponent<MeshFilter>().sharedMesh = Gear.BalloonMesh;
        bg.AddComponent<MeshRenderer>().sharedMaterial = k == PowerKind.Golden ? Mats.PBR(Col[3], 0.95f, 0.85f) : Mats.Balloon(Col[(int)k]);
        var col = gameObject.AddComponent<SphereCollider>();
        col.radius = 0.55f * sc; col.isTrigger = true;
        // spinning halo ring + string
        ring = Mats.Prim(PrimitiveType.Cylinder, transform, Vector3.zero, new Vector3(1.3f, 0.02f, 1.3f) * sc, Mats.Unlit(Color.Lerp(Col[(int)k], Color.white, 0.4f)), false).transform;
        var st = Mats.Prim(PrimitiveType.Cylinder, transform, new Vector3(0f, -0.9f * sc, 0f), new Vector3(0.012f, 0.45f, 0.012f), Mats.Lit(Color.white), false);
        st.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void Update()
    {
        if (claimed) return;
        float t = Time.time + t0;
        transform.position = home + Vector3.up * Mathf.Sin(t * 1.6f) * 0.25f;
        transform.rotation = Quaternion.Euler(0f, t * 40f, Mathf.Sin(t) * 6f);
        if (ring != null) ring.localRotation = Quaternion.Euler(Mathf.Sin(t * 2f) * 25f, t * 120f, 0f);
        if (Game.I != null && Game.I.state == Game.State.Playing)
            foreach (var s in Game.I.soldiers)
                if (s.alive && (s.transform.position + Vector3.up - transform.position).sqrMagnitude < 2.2f) { Claim(s); break; }
    }

    public void Claim(Soldier s)
    {
        if (claimed || s == null || !s.alive) return;
        claimed = true;
        const float Dur = 8f;
        switch (kind)
        {
            case PowerKind.Shield: s.shieldT = Dur; break;
            case PowerKind.Speed: s.speedT = Dur; break;
            case PowerKind.Magnet: s.magnetT = Dur; break;
            case PowerKind.Golden: s.Regrow(); break;
        }
        string msg = kind == PowerKind.Golden ? "GOLDEN BALLOON! +1 BALLOON" : Name[(int)kind] + "!";
        s.Callout("<color=#" + ColorUtility.ToHtmlStringRGB(Col[(int)kind]) + ">" + msg + "</color>", 1.8f);
        FX.Pop(transform.position, Col[(int)kind]);
        if (kind == PowerKind.Golden) FX.Celebrate(transform.position, Col[3], 0.8f); else FX.Combo(transform.position, 2);
        Sfx.UiSelect();
        if (PowerUps.I != null) PowerUps.I.Announce(s.nick + " got " + Name[(int)kind]);
        Destroy(gameObject);
    }
}

public class PowerUps : MonoBehaviour
{
    public static PowerUps I;
    readonly List<PowerBalloon> live = new List<PowerBalloon>();
    float spawnT = 6f, roundT;
    bool goldenDone;
    readonly Dictionary<Soldier, GameObject> bubbles = new Dictionary<Soldier, GameObject>();

    void Awake() { I = this; }

    public void Announce(string line) { if (Game.I != null) Game.I.Feed(line); }

    public void ClearAll()
    {
        foreach (var p in live) if (p != null) Destroy(p.gameObject);
        live.Clear();
        spawnT = 6f; roundT = 0f; goldenDone = false;
    }

    void Update()
    {
        var g = Game.I;
        if (g == null) return;
        float dt = Time.deltaTime;
        live.RemoveAll(p => p == null);
        if (g.state == Game.State.Playing && !Game.Demo)
        {
            roundT += dt;
            spawnT -= dt;
            if (spawnT <= 0f && live.Count < 4)
            {
                spawnT = Random.Range(7f, 11f);
                PowerKind k = (PowerKind)Random.Range(0, 3);
                if (!goldenDone && roundT > 25f && Random.value < 0.5f) { k = PowerKind.Golden; goldenDone = true; }
                Spawn(k);
            }
        }
        foreach (var s in g.soldiers)
        {
            if (s == null) continue;
            if (s.shieldT > 0f) s.shieldT -= dt;
            if (s.speedT > 0f) { s.speedT -= dt; if (s.HSpeed > 4f && Random.value < 0.3f) FX.Puff(s.transform.position + Vector3.up * 0.2f, Vector3.up); }
            if (s.magnetT > 0f) s.magnetT -= dt;
            GameObject b;
            bubbles.TryGetValue(s, out b);
            bool want = s.alive && s.shieldT > 0f;
            if (want && b == null)
            {
                b = new GameObject("Shield");
                b.transform.SetParent(s.transform, false);
                b.transform.localPosition = new Vector3(0f, 1.4f, 0f);
                b.transform.localScale = new Vector3(2.0f, 2.6f, 2.0f);
                b.AddComponent<MeshFilter>().sharedMesh = Gear.BalloonMesh;
                var r = b.AddComponent<MeshRenderer>();
                r.sharedMaterial = Mats.Balloon(new Color(0.55f, 0.85f, 1f, 0.35f));
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Mats.SetLayer(b, s.gameObject.layer);
                bubbles[s] = b;
            }
            if (b != null)
            {
                b.SetActive(want);
                if (want) b.transform.localScale = new Vector3(2.0f, 2.6f, 2.0f) * (1f + Mathf.Sin(Time.time * 6f) * 0.03f) * (s.shieldT < 2f && Mathf.Repeat(Time.time * 6f, 1f) < 0.5f ? 0.9f : 1f);
            }
        }
    }

    void Spawn(PowerKind k)
    {
        for (int tries = 0; tries < 20; tries++)
        {
            Vector2 r = Random.insideUnitCircle * 75f;
            Vector3 p = World.OnGround(World.center + new Vector3(r.x, 0f, r.y)) + Vector3.up * 1.6f;
            if (Physics.CheckSphere(p, 1.2f, ~0, QueryTriggerInteraction.Ignore)) continue;
            var go = new GameObject("Power_" + k);
            var pb = go.AddComponent<PowerBalloon>();
            pb.Setup(k, p);
            live.Add(pb);
            return;
        }
    }

    // magnet BBs: steer toward the nearest enemy balloon within 7 m ahead
    public static Vector3 Home(Vector3 pos, Vector3 vel, Soldier owner, float dt)
    {
        var g = Game.I;
        if (g == null) return vel;
        Vector3 best = Vector3.zero; float bd = 49f;
        Vector3 fwd = vel.normalized;
        foreach (var s in g.soldiers)
        {
            if (s == owner || !s.alive || s.shieldT > 0f) continue;
            foreach (var b in s.balloons)
            {
                if (b.popped) continue;
                Vector3 d = b.transform.position - pos;
                float m = d.sqrMagnitude;
                if (m < bd && Vector3.Dot(d, fwd) > 0f) { bd = m; best = d; }
            }
        }
        if (bd >= 49f) return vel;
        float sp = vel.magnitude;
        return Vector3.RotateTowards(vel, best.normalized * sp, 9f * dt, 0f);
    }
}
