using System.Collections.Generic;
using UnityEngine;

// Beach Buggy Blast-style weapons. Every hit spins the car out; a shield blocks one hit.
public enum Item { None, Rocket, Fireball, Oil, Lightning, Bowling, Mine, Shield, Boost, Homing }

public static class Items
{
    public static readonly Item[] All = { Item.Rocket, Item.Fireball, Item.Oil, Item.Lightning, Item.Bowling, Item.Mine, Item.Shield, Item.Boost, Item.Homing };

    public static string Name(Item i)
    {
        switch (i)
        {
            case Item.Rocket: return "ROCKET";
            case Item.Fireball: return "FIREBALL";
            case Item.Oil: return "OIL SLICK";
            case Item.Lightning: return "LIGHTNING";
            case Item.Bowling: return "BOWLING BALL";
            case Item.Mine: return "MINE";
            case Item.Shield: return "SHIELD";
            case Item.Boost: return "BOOST";
            case Item.Homing: return "HOMING MISSILE";
        }
        return "";
    }

    // short verb for "hit by X's ..." messages
    public static string Hit(Item i)
    {
        switch (i)
        {
            case Item.Rocket: return "rocket";
            case Item.Fireball: return "fireball";
            case Item.Oil: return "oil slick";
            case Item.Lightning: return "lightning";
            case Item.Bowling: return "bowling ball";
            case Item.Mine: return "mine";
            case Item.Homing: return "homing missile";
        }
        return "hit";
    }

    public static Color Col(Item i)
    {
        switch (i)
        {
            case Item.Rocket: return new Color(1f, 0.3f, 0.2f);
            case Item.Fireball: return new Color(1f, 0.55f, 0.1f);
            case Item.Oil: return new Color(0.25f, 0.2f, 0.3f);
            case Item.Lightning: return new Color(1f, 0.95f, 0.3f);
            case Item.Bowling: return new Color(0.55f, 0.25f, 0.85f);
            case Item.Mine: return new Color(0.85f, 0.15f, 0.15f);
            case Item.Shield: return new Color(0.3f, 0.85f, 1f);
            case Item.Boost: return new Color(0.3f, 1f, 0.45f);
            case Item.Homing: return new Color(0.35f, 0.55f, 1f);
        }
        return Color.white;
    }

    // weighted roll by race position: leaders get defensive items, the back of the pack gets the big ones
    public static Item Roll(int place, int racers)
    {
        float f = racers > 1 ? (place - 1) / (float)(racers - 1) : 0.5f;   // 0 = leader, 1 = last
        var w = new Dictionary<Item, float>
        {
            { Item.Rocket, 3f },
            { Item.Fireball, 2.5f },
            { Item.Oil, Mathf.Lerp(3f, 0.8f, f) },
            { Item.Mine, Mathf.Lerp(3f, 0.8f, f) },
            { Item.Shield, Mathf.Lerp(2.5f, 1f, f) },
            { Item.Bowling, Mathf.Lerp(0.8f, 2.5f, f) },
            { Item.Boost, Mathf.Lerp(0.6f, 3f, f) },
            { Item.Homing, Mathf.Lerp(0.3f, 3f, f) },
            { Item.Lightning, f < 0.3f ? 0f : Mathf.Lerp(0.2f, 2f, f) },
        };
        float total = 0f;
        foreach (var kv in w) total += kv.Value;
        float r = Random.value * total;
        foreach (var kv in w) { r -= kv.Value; if (r <= 0f) return kv.Key; }
        return Item.Rocket;
    }

    public static bool IsForward(Item i) { return i == Item.Rocket || i == Item.Fireball || i == Item.Bowling || i == Item.Homing; }
    public static bool IsDrop(Item i) { return i == Item.Oil || i == Item.Mine; }

    public static void Use(Kart k, Item it)
    {
        Vector3 fwd = k.Forward, pos = k.transform.position;
        switch (it)
        {
            case Item.Rocket:
                Shot.Spawn(Item.Rocket, k, pos + fwd * 3f + Vector3.up * 0.7f, fwd * Mathf.Max(52f, k.speed + 26f));
                Sfx.PlayAt(Sfx.Rocket, pos, 0.8f);
                break;
            case Item.Homing:
                {
                    Shot s = Shot.Spawn(Item.Homing, k, pos + fwd * 3f + Vector3.up * 0.8f, fwd * Mathf.Max(44f, k.speed + 18f));
                    s.target = Game.I != null ? Game.I.KartAhead(k) : null;
                    Sfx.PlayAt(Sfx.Homing, pos, 0.8f);
                    break;
                }
            case Item.Fireball:
                for (int a = -1; a <= 1; a++)
                {
                    Vector3 d = Quaternion.Euler(0f, a * 11f, 0f) * fwd;
                    Shot.Spawn(Item.Fireball, k, pos + d * 3f + Vector3.up * 0.6f, d * Mathf.Max(40f, k.speed + 14f));
                }
                Sfx.PlayAt(Sfx.Fireball, pos, 0.8f);
                break;
            case Item.Bowling:
                Shot.Spawn(Item.Bowling, k, pos + fwd * 3.2f + Vector3.up * 0.9f, fwd * Mathf.Max(34f, k.speed + 8f));
                Sfx.PlayAt(Sfx.Bowl, pos, 0.9f);
                break;
            case Item.Oil:
                Shot.Spawn(Item.Oil, k, pos - fwd * 4f, Vector3.zero);
                Sfx.PlayAt(Sfx.Oil, pos, 0.7f);
                break;
            case Item.Mine:
                Shot.Spawn(Item.Mine, k, pos - fwd * 4f, Vector3.zero);
                Sfx.PlayAt(Sfx.Mine, pos, 0.7f);
                break;
            case Item.Shield:
                k.shieldT = 9f;
                Sfx.PlayAt(Sfx.ShieldUp, pos, 0.8f);
                break;
            case Item.Boost:
                k.StartBoost(2.6f, 1.4f);
                Sfx.PlayAt(Sfx.Boost, pos, 0.8f);
                break;
            case Item.Lightning:
                {
                    Sfx.PlayAt(Sfx.Zap, pos, 0.9f, 400f);
                    if (Game.I == null) break;
                    int n = 0;
                    for (int p = k.place - 1; p >= 1 && n < 3; p--)
                    {
                        Kart v = Game.I.KartAtPlace(p);
                        if (v == null || v == k || v.finished) continue;
                        FX.Zap(v.transform.position);
                        v.Hit(Item.Lightning, k);
                        n++;
                    }
                    break;
                }
        }
    }
}

// projectiles + dropped hazards
public class Shot : MonoBehaviour
{
    public static readonly List<Shot> Live = new List<Shot>();
    public Item type;
    public Kart owner, target;
    public Vector3 vel;
    float life, age, hover, radius;
    int hint = -1, hits;
    Transform vis, blink;

    public static Shot Spawn(Item t, Kart owner, Vector3 pos, Vector3 vel)
    {
        // keep the number of hazards on the track sane
        if (t == Item.Oil || t == Item.Mine)
        {
            int hz = 0; Shot oldest = null;
            foreach (var s in Live) if (s.type == Item.Oil || s.type == Item.Mine) { hz++; if (oldest == null) oldest = s; }
            if (hz >= 14 && oldest != null) oldest.Kill(false);
        }
        var go = new GameObject("Shot " + t);
        var sh = go.AddComponent<Shot>();
        sh.type = t; sh.owner = owner; sh.vel = vel;
        go.transform.position = pos;
        sh.Init();
        Live.Add(sh);
        return sh;
    }

    void Init()
    {
        Transform t = transform;
        switch (type)
        {
            case Item.Rocket:
            case Item.Homing:
                {
                    life = type == Item.Homing ? 7f : 3.2f; hover = 0.8f; radius = 2.0f;
                    Color c = type == Item.Homing ? new Color(0.3f, 0.5f, 1f) : new Color(0.9f, 0.15f, 0.12f);
                    vis = new GameObject("Vis").transform; vis.SetParent(t, false);
                    Mats.Prim(PrimitiveType.Cylinder, vis, Vector3.zero, new Vector3(0.32f, 0.6f, 0.32f), new Vector3(90f, 0f, 0f), Mats.Shiny(c));
                    Mats.Prim(PrimitiveType.Sphere, vis, new Vector3(0f, 0f, 0.6f), new Vector3(0.32f, 0.32f, 0.5f), Mats.Shiny(Color.white));
                    Mats.Prim(PrimitiveType.Cube, vis, new Vector3(0f, 0f, -0.45f), new Vector3(0.75f, 0.05f, 0.25f), Mats.Lit(c * 0.7f));
                    Mats.Prim(PrimitiveType.Cube, vis, new Vector3(0f, 0f, -0.45f), new Vector3(0.05f, 0.75f, 0.25f), Mats.Lit(c * 0.7f));
                    break;
                }
            case Item.Fireball:
                life = 2.4f; hover = 0.7f; radius = 1.8f;
                vis = Mats.Prim(PrimitiveType.Sphere, t, Vector3.zero, Vector3.one * 0.9f, Mats.Unlit(new Color(1f, 0.55f, 0.1f))).transform;
                Mats.Prim(PrimitiveType.Sphere, vis, Vector3.zero, Vector3.one * 0.7f, Mats.Unlit(new Color(1f, 0.9f, 0.4f)));
                break;
            case Item.Bowling:
                life = 8f; hover = 0.85f; radius = 2.2f;
                vis = Mats.Prim(PrimitiveType.Sphere, t, Vector3.zero, Vector3.one * 1.7f, Mats.Shiny(new Color(0.35f, 0.12f, 0.55f))).transform;
                for (int i = 0; i < 3; i++)
                    Mats.Prim(PrimitiveType.Sphere, vis, new Vector3(-0.12f + i * 0.12f, 0.36f, 0.25f - (i == 1 ? 0.06f : 0f)), Vector3.one * 0.11f, Mats.Lit(new Color(0.05f, 0.05f, 0.05f)));
                break;
            case Item.Oil:
                life = 28f; hover = 0.04f; radius = 2.6f;
                vis = Mats.Prim(PrimitiveType.Cylinder, t, Vector3.zero, new Vector3(4.6f, 0.02f, 3.8f), Mats.Steel(new Color(0.06f, 0.05f, 0.08f))).transform;
                Mats.Prim(PrimitiveType.Cylinder, vis, new Vector3(0.5f, 0.6f, 0.3f), new Vector3(0.4f, 1f, 0.5f), Mats.Steel(new Color(0.12f, 0.1f, 0.18f)));
                break;
            case Item.Mine:
                life = 30f; hover = 0.0f; radius = 1.9f;
                vis = Mats.Prim(PrimitiveType.Sphere, t, Vector3.zero, new Vector3(1.1f, 0.6f, 1.1f), Mats.Steel(new Color(0.2f, 0.2f, 0.22f))).transform;
                blink = Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 0.3f, 0f), Vector3.one * 0.25f, Mats.Unlit(new Color(1f, 0.1f, 0.1f))).transform;
                break;
        }
        foreach (var r in GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // drop hazards onto the ground
        if (vel.sqrMagnitude < 0.01f)
        {
            float y; Vector3 n;
            Track.Ground(transform.position, out y, out n);
            transform.position = new Vector3(transform.position.x, y + hover, transform.position.z);
            transform.rotation = Quaternion.FromToRotation(Vector3.up, n);
        }
    }

    public void Kill(bool boom)
    {
        if (boom) { FX.Boom(transform.position, type == Item.Fireball ? 0.5f : 0.8f); Sfx.PlayAt(Sfx.Boom, transform.position, 0.6f); }
        Live.Remove(this);
        Destroy(gameObject);
    }

    public static void ClearAll()
    {
        for (int i = Live.Count - 1; i >= 0; i--) if (Live[i] != null) Destroy(Live[i].gameObject);
        Live.Clear();
    }

    public void Tick(float dt)
    {
        age += dt;
        life -= dt;
        if (life <= 0f) { Kill(type != Item.Oil && type != Item.Mine); return; }
        Vector3 p = transform.position;
        bool moving = vel.sqrMagnitude > 0.01f;
        if (moving)
        {
            if (type == Item.Homing && target != null && age > 0.25f && !target.finished)
            {
                Vector3 to = Track.Flat(target.transform.position - p);
                Vector3 v = Track.Flat(vel);
                float sp = v.magnitude;
                Vector3 nd = Vector3.RotateTowards(v.normalized, to.normalized, 160f * Mathf.Deg2Rad * dt, 0f);
                vel = nd * Mathf.Max(sp, target.speed + 12f);
            }
            p += vel * dt;
            float gy; Vector3 n;
            bool hitGround = Track.Ground(p, out gy, out n);
            p.y = Mathf.Lerp(p.y, gy + hover, Mathf.Min(1f, dt * 12f));
            if (p.y < gy + hover * 0.5f) p.y = gy + hover * 0.5f;
            // walls
            Track.Proj pm = Track.Nearest(p, hint, 20);
            hint = pm.i;
            Track.Proj ps = Track.NearestShortcut(p);
            bool outside = pm.dist > Track.HalfW + Track.Wall - 1f && ps.dist > Track.ScHalf + 5f;
            if (outside)
            {
                if (type == Item.Bowling)
                {
                    Vector3 nrm = Track.Flat(p - pm.point).normalized;
                    float o = Vector3.Dot(vel, nrm);
                    if (o > 0f) vel -= nrm * o * 2f;
                    p -= nrm * 0.5f;
                    Sfx.PlayAt(Sfx.Bump, p, 0.5f);
                }
                else if (type != Item.Homing) { transform.position = p; Kill(true); return; }
            }
            if (gy < Track.WaterY - 1.2f && type != Item.Homing) { FX.Splash(new Vector3(p.x, Track.WaterY, p.z), 12); Kill(false); return; }
            transform.position = p;
            transform.rotation = Quaternion.LookRotation(vel.normalized, Vector3.up);
            if (type == Item.Rocket || type == Item.Homing) { FX.Smoke(p - vel.normalized * 0.8f, 0.6f, new Color(0.8f, 0.8f, 0.8f, 0.6f)); if (type == Item.Homing && Random.value < 0.2f) FX.Sparkle(p, new Color(0.4f, 0.6f, 1f), 1); }
            if (type == Item.Fireball) { FX.Flame(p, -vel.normalized); vis.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(age * 9f)) * 0.6f, 0f); }
            if (type == Item.Bowling) vis.Rotate(Vector3.right, vel.magnitude * dt / 0.85f * Mathf.Rad2Deg, Space.Self);
        }
        else if (blink != null) blink.gameObject.SetActive(((int)(age * 3f)) % 2 == 0);

        // hits
        if (Game.I == null) return;
        float arm = moving ? 0.35f : 0.6f;
        foreach (Kart k in Game.I.karts)
        {
            if (k == null || k.finished || k.respawning) continue;
            if (k == owner && age < arm) continue;
            Vector3 d = k.transform.position + Vector3.up * 0.6f - transform.position;
            if (Mathf.Abs(d.y) > 2.6f) continue;
            d.y = 0f;
            if (d.magnitude > radius) continue;
            k.Hit(type, owner);
            if (type == Item.Oil) { hits++; if (hits >= 2) Kill(false); return; }
            Kill(type == Item.Rocket || type == Item.Homing || type == Item.Mine || type == Item.Bowling);
            return;
        }
    }
}

// spinning "?" box: drive through it to get a random item (respawns after a moment)
public class ItemBox : MonoBehaviour
{
    public static readonly List<ItemBox> All = new List<ItemBox>();
    Transform vis, inner;
    float hiddenT;
    float phase;

    public static void SpawnAll()
    {
        Material shell = Mats.GlassTint(new Color(0.55f, 0.85f, 1f, 0.45f));
        Material gold = Mats.Shiny(new Color(1f, 0.78f, 0.15f));
        Material q = Mats.Unlit(Color.white);
        foreach (var kv in Track.BoxSpots)
        {
            var go = new GameObject("ItemBox");
            var b = go.AddComponent<ItemBox>();
            float y; Vector3 n;
            Track.Ground(kv.Key, out y, out n);
            go.transform.position = new Vector3(kv.Key.x, y + 1.3f, kv.Key.z);
            b.vis = new GameObject("Vis").transform;
            b.vis.SetParent(go.transform, false);
            Mats.Prim(PrimitiveType.Cube, b.vis, Vector3.zero, Vector3.one * 1.5f, shell);
            b.inner = Mats.Prim(PrimitiveType.Cube, b.vis, Vector3.zero, Vector3.one * 0.8f, gold).transform;
            // a "?" made of little bars on two faces
            for (int f = -1; f <= 1; f += 2)
            {
                Transform face = new GameObject("Q").transform;
                face.SetParent(b.vis, false);
                face.localPosition = new Vector3(0f, 0f, f * 0.77f);
                face.localRotation = Quaternion.Euler(0f, f > 0 ? 0f : 180f, 0f);
                Mats.Prim(PrimitiveType.Cube, face, new Vector3(0f, 0.38f, 0f), new Vector3(0.42f, 0.1f, 0.02f), q);
                Mats.Prim(PrimitiveType.Cube, face, new Vector3(0.18f, 0.22f, 0f), new Vector3(0.1f, 0.3f, 0.02f), q);
                Mats.Prim(PrimitiveType.Cube, face, new Vector3(0.05f, 0.05f, 0f), new Vector3(0.3f, 0.1f, 0.02f), q);
                Mats.Prim(PrimitiveType.Cube, face, new Vector3(-0.04f, -0.12f, 0f), new Vector3(0.1f, 0.22f, 0.02f), q);
                Mats.Prim(PrimitiveType.Cube, face, new Vector3(-0.04f, -0.38f, 0f), new Vector3(0.12f, 0.12f, 0.02f), q);
            }
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            b.phase = Random.value * 6f;
            All.Add(b);
        }
    }

    public static void ResetAll() { foreach (var b in All) { b.hiddenT = 0f; b.vis.gameObject.SetActive(true); } }

    void Update()
    {
        float dt = Time.deltaTime;
        if (hiddenT > 0f)
        {
            hiddenT -= dt;
            if (hiddenT <= 0f) { vis.gameObject.SetActive(true); vis.localScale = Vector3.one * 0.2f; }
            return;
        }
        phase += dt;
        vis.localScale = Vector3.MoveTowards(vis.localScale, Vector3.one, dt * 3f);
        vis.localRotation = Quaternion.Euler(20f, phase * 90f, 15f);
        vis.localPosition = new Vector3(0f, Mathf.Sin(phase * 2.5f) * 0.2f, 0f);
        inner.localRotation = Quaternion.Euler(phase * 120f, 0f, phase * 70f);
        if (Game.I == null || Game.I.state != Game.State.Race) return;
        foreach (Kart k in Game.I.karts)
        {
            if (k == null || k.respawning) continue;
            Vector3 d = k.transform.position + Vector3.up * 0.8f - transform.position;
            if (Mathf.Abs(d.y) > 2.4f) continue;
            d.y = 0f;
            if (d.magnitude < 2.3f)
            {
                hiddenT = 2.2f;
                vis.gameObject.SetActive(false);
                FX.Sparkle(transform.position, new Color(1f, 0.85f, 0.3f), 14);
                k.GiveItem();
                return;
            }
        }
    }
}
