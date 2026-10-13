using UnityEngine;

// Track hazards (spin you out, never knock you off the track) and cheap moving decoration.
//  type 0 = fire geyser (Volcano Rim): a vent in one lane that erupts for ~1 s every 3.4 s, smoke warns first.
//  type 1 = swinging stone (Jungle Ruins): a temple gantry over the road with a stone block swinging across it.
public class Hazard : MonoBehaviour
{
    public int type;
    float t, period, phase;
    Transform fire, bob, pivot;
    Vector3 vent;
    static Material fireM, ventM, stoneM, woodM, ropeM;

    public static Hazard Create(Transform root, int type, float s, float lat)
    {
        if (fireM == null)
        {
            fireM = Mats.Unlit(new Color(1f, 0.55f, 0.1f)); ventM = Mats.Lit(new Color(0.08f, 0.07f, 0.07f));
            stoneM = Mats.Lit(new Color(0.48f, 0.5f, 0.42f)); woodM = Mats.Lit(new Color(0.42f, 0.3f, 0.18f)); ropeM = Mats.Lit(new Color(0.3f, 0.28f, 0.22f));
        }
        var go = new GameObject(type == 0 ? "Geyser" : "Swinging stone");
        go.transform.SetParent(root, false);
        var h = go.AddComponent<Hazard>();
        h.type = type;
        Vector3 p = Track.PointAt(s, lat);
        float y; Vector3 n;
        Track.Ground(p, out y, out n);
        Vector3 tan = Track.TangentAt(s);
        go.transform.position = new Vector3(p.x, y, p.z);
        go.transform.rotation = Quaternion.LookRotation(tan, Vector3.up);
        h.vent = go.transform.position;
        h.phase = Mathf.Repeat(s * 0.37f, 3.4f);
        if (type == 0)
        {
            h.period = 3.4f;
            Mats.Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0f, 0.04f, 0f), new Vector3(3.2f, 0.06f, 3.2f), ventM);
            Mats.Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0f, 0.07f, 0f), new Vector3(1.6f, 0.05f, 1.6f), fireM);
            h.fire = Mats.Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0f, 4f, 0f), new Vector3(1.8f, 4f, 1.8f), fireM).transform;
            h.fire.gameObject.SetActive(false);
            Mats.NoShadows(go);
        }
        else
        {
            h.period = 3.0f;
            float x = Track.HalfW + 1.6f;
            for (int sg = -1; sg <= 1; sg += 2)
                Mats.Prim(PrimitiveType.Cube, go.transform, new Vector3(sg * x, 6.5f, 0f), new Vector3(1.4f, 13f, 1.4f), stoneM);
            Mats.Prim(PrimitiveType.Cube, go.transform, new Vector3(0f, 13.4f, 0f), new Vector3(2f * x + 2.4f, 1.6f, 1.8f), stoneM);
            h.pivot = new GameObject("Pivot").transform;
            h.pivot.SetParent(go.transform, false);
            h.pivot.localPosition = new Vector3(0f, 12.6f, 0f);
            Mats.Prim(PrimitiveType.Cube, h.pivot, new Vector3(0f, -4.6f, 0f), new Vector3(0.25f, 9.2f, 0.25f), ropeM);
            h.bob = Mats.Prim(PrimitiveType.Cube, h.pivot, new Vector3(0f, -10.2f, 0f), new Vector3(2.6f, 2.6f, 2.6f), stoneM).transform;
            Mats.Prim(PrimitiveType.Cube, h.bob, new Vector3(0f, 0f, 0f), new Vector3(1.08f, 0.25f, 1.08f), woodM);
        }
        return h;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        t += dt;
        bool racing = Game.I != null && Game.I.state == Game.State.Race;
        if (type == 0)
        {
            float k = Mathf.Repeat(t + phase, period);
            bool erupt = k > period - 1.1f;
            bool warn = k > period - 1.9f && !erupt;
            if (fire.gameObject.activeSelf != erupt) fire.gameObject.SetActive(erupt);
            if (erupt)
            {
                float e = (k - (period - 1.1f)) / 1.1f;
                float hgt = 9f * Mathf.Sin(Mathf.Clamp01(e * 1.4f) * Mathf.PI * 0.5f) * (e > 0.85f ? (1f - e) / 0.15f : 1f) + 0.5f;
                fire.localScale = new Vector3(1.8f + 0.3f * Mathf.Sin(t * 30f), hgt * 0.5f, 1.8f);
                fire.localPosition = new Vector3(0f, hgt * 0.5f, 0f);
                if (Random.value < 0.5f) FX.Emit(vent + Vector3.up * hgt, Random.insideUnitSphere * 3f + Vector3.up * 4f, Random.Range(0.5f, 1.1f), 0.5f, new Color(1f, Random.Range(0.4f, 0.8f), 0.1f));
            }
            else if (warn && Random.value < 0.4f) FX.Smoke(vent + Vector3.up * 0.5f, 1.2f, new Color(0.3f, 0.28f, 0.27f, 0.7f));
            if (!erupt || !racing) return;
            foreach (Kart kt in Game.I.karts)
            {
                if (kt == null || kt.respawning || kt.finished || kt.spinT > 0f) continue;
                Vector3 d = kt.transform.position - vent;
                if (d.y > 7f || d.y < -1.5f) continue;
                d.y = 0f;
                if (d.magnitude < 2.4f) { kt.Hit(Item.Fireball, null); if (kt.human) kt.Toast("Scorched by a fire geyser!", 1.6f); }
            }
        }
        else
        {
            float ang = 55f * Mathf.Sin((t + phase) * Mathf.PI * 2f / period);
            pivot.localRotation = Quaternion.Euler(0f, 0f, ang);
            if (!racing) return;
            Vector3 bp = bob.position;
            foreach (Kart kt in Game.I.karts)
            {
                if (kt == null || kt.respawning || kt.finished || kt.spinT > 0f) continue;
                if ((kt.transform.position + Vector3.up * 0.7f - bp).magnitude < 2.8f)
                {
                    kt.Hit(Item.Bowling, null);
                    if (kt.human) kt.Toast("Smacked by a swinging stone!", 1.6f);
                }
            }
        }
    }
}

// Moving decoration (no gameplay): circling birds / balloon / blimp, cabins along a cable or a sky train on a loop,
// tumbleweeds rolling across the desert, smoke columns, spinning fans.
public class Mover : MonoBehaviour
{
    public int mode;            // 0 circle, 1 path loop, 2 roll a->b, 3 smoke, 4 spin
    public Vector3 a, b;
    public float speed = 1f, radius = 30f, phase;
    public Vector3[] path;
    public Color smoke = new Color(0.25f, 0.23f, 0.22f, 0.7f);
    float t, pathLen;
    float[] cum;

    void Start()
    {
        if (path != null && path.Length > 1)
        {
            cum = new float[path.Length + 1];
            for (int i = 0; i < path.Length; i++) cum[i + 1] = cum[i] + (path[(i + 1) % path.Length] - path[i]).magnitude;
            pathLen = cum[path.Length];
        }
        t = phase;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        t += dt;
        switch (mode)
        {
            case 0:
            {
                float ang = t * speed;
                transform.position = a + new Vector3(Mathf.Cos(ang) * radius, Mathf.Sin(t * 0.6f) * 2f, Mathf.Sin(ang) * radius);
                Vector3 dir = new Vector3(-Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * Mathf.Sign(speed);
                transform.rotation = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, 0f, -12f * Mathf.Sign(speed));
                break;
            }
            case 1:
            {
                if (pathLen <= 0f) return;
                float d = Mathf.Repeat(t * speed, pathLen);
                int i = 0;
                while (i < path.Length - 1 && cum[i + 1] < d) i++;
                Vector3 p0 = path[i], p1 = path[(i + 1) % path.Length];
                float k = (d - cum[i]) / Mathf.Max(0.01f, cum[i + 1] - cum[i]);
                transform.position = Vector3.Lerp(p0, p1, k);
                Vector3 dir = Track.Flat(p1 - p0);
                if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir.normalized, Vector3.up), Mathf.Min(1f, dt * 3f));
                break;
            }
            case 2:
            {
                float L = (b - a).magnitude;
                float d = Mathf.Repeat(t * speed, L + 40f);
                Vector3 p = Vector3.Lerp(a, b, Mathf.Clamp01(d / L));
                p.y = Track.HeightAt(p.x, p.z) + 0.6f + Mathf.Abs(Mathf.Sin(t * 3f)) * 0.8f;
                transform.position = p;
                transform.rotation = Quaternion.LookRotation(Track.Flat(b - a).normalized, Vector3.up) * Quaternion.Euler(t * 300f, 0f, 0f);
                gameObject.SetActive(true);
                break;
            }
            case 3:
                if (Random.value < dt * speed) FX.Emit(transform.position + Random.insideUnitSphere * 2f, Vector3.up * Random.Range(3f, 6f) + Random.insideUnitSphere, Random.Range(4f, 8f), Random.Range(2.5f, 4f), smoke);
                break;
            case 4:
                transform.Rotate(0f, speed * dt, 0f, Space.Self);
                break;
        }
    }
}
