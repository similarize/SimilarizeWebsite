using UnityEngine;

// ffw (Four Froggies World): water ripples on the ranch pond. A step / wade / mech footfall in the pond throws a ring
// of soft white foam particles that spreads out flat along the surface and fades (the shared FX particle system, so it
// is pooled and capped there). At most 16 ripple sources per 0.25 s window and fewer particles on phones / Quest /
// Tesla. Bigger mechs = bigger, faster rings + spray; froggies leave gentle wakes while they move.
public static class Ripples
{
    public const int MaxSources = 16;
    static float winT; static int used;
    static readonly Color Foam = new Color(0.92f, 0.97f, 1f, 0.55f);
    static readonly Color Spray = new Color(0.85f, 0.93f, 1f, 0.7f);

    public static bool InWater(Vector3 p) { return Layout.InPond(p.x, p.z) && Ranch.GY(p.x, p.z) < Layout.WaterY - 0.02f; }

    static bool Budget()
    {
        if (Time.unscaledTime > winT) { winT = Time.unscaledTime + 0.25f; used = 0; }
        return used++ < MaxSources;
    }

    // radius = how far the ring travels (m); strength 0..1 controls particle count / opacity
    public static void Ring(Vector3 p, float radius, float strength = 1f)
    {
        if (!InWater(p) || !Budget()) return;
        float y = Layout.WaterY + Rise + 0.06f;
        int n = Mathf.Clamp(Mathf.RoundToInt((Look.Mobile ? 8f : 14f) * Mathf.Clamp(strength, 0.3f, 1.5f)), 5, Look.Mobile ? 12 : 22);
        float life = Mathf.Clamp(0.9f + radius * 0.05f, 0.9f, 2.4f);
        float sz = Mathf.Clamp(radius * 0.12f, 0.15f, 3f);
        Color c = Foam; c.a *= Mathf.Clamp01(0.5f + strength * 0.5f);
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n + Random.Range(-0.08f, 0.08f);
            Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            FX.Puff(new Vector3(p.x, y, p.z) + d * radius * 0.08f, d * (radius / life), sz * Random.Range(0.8f, 1.2f), life, c);
        }
    }

    // mech footfall in the pond: an outward ring (two for big mechs) + a short spray column
    public static void Footfall(Vector3 p, float mechH)
    {
        if (!InWater(p)) return;
        float r = Mathf.Clamp(mechH * 0.35f, 3f, 60f);
        Ring(p, r, 1.2f);
        Surge(mechH);
        if (mechH > 40f) Ring(p, r * 0.55f, 0.8f);
        Vector3 s = new Vector3(p.x, Layout.WaterY + 0.2f, p.z);
        FX.Splash(s, Mathf.Clamp(mechH * 0.4f, 8f, Look.Mobile ? 16f : 30f));
        int n = Look.Mobile ? 4 : 8; float h = Mathf.Clamp(mechH * 0.08f, 1f, 12f);
        for (int i = 0; i < n; i++)
        {
            Vector2 u = Random.insideUnitCircle * 0.4f;
            FX.Puff(s, new Vector3(u.x, 1f, u.y) * h * Random.Range(1.2f, 2f), h * 0.18f, Random.Range(0.6f, 1.1f), Spray);
        }
    }

    // ---------------- ffw displacement surge ----------------
    // A mech stepping into the pond pushes its volume aside: the pond surface plane eases up (capped) and settles back,
    // a foam band washes up the shoreline and recedes, and a dark wet band is left on the bank that fades out. Scaled by
    // mech size (10-story small, trillion-story the cap). No fluid sim: one transform offset + pooled particles.
    public static Transform Water;
    public const float MaxRise = 0.35f;
    public static float Rise { get; private set; }
    static float amp, t = 99f, dur = 2.5f, shoreT;

    public static void Register(GameObject water) { Water = water.transform; water.AddComponent<RippleDriver>(); }

    public static void Surge(float mechH)
    {
        float a = Mathf.Clamp(0.04f + mechH * 0.0018f, 0.05f, MaxRise);   // 30 m mech ~0.1 m, 160 m+ = cap
        if (t < dur && amp >= a) return;
        amp = Mathf.Max(a, Rise); t = 0f; dur = 1.8f + a * 5f; shoreT = 0f;
    }

    internal static void Tick(float dt)
    {
        if (Water == null) return;
        if (t >= dur) { if (Rise != 0f) { Rise = 0f; SetY(); } return; }
        t += dt;
        float u = Mathf.Clamp01(t / dur);
        // fast eased rise (first 25%), then a damped settle with one small slosh
        Rise = u < 0.25f ? amp * Mathf.SmoothStep(0f, 1f, u / 0.25f)
                         : amp * Mathf.Exp(-(u - 0.25f) * 4f) * Mathf.Cos((u - 0.25f) * 7f);
        SetY();
        shoreT -= dt;
        if (shoreT <= 0f && u < 0.7f) { shoreT = Look.Mobile ? 0.35f : 0.2f; ShoreWash(amp / MaxRise, u); }
    }
    static void SetY() { var p = Water.position; p.y = Layout.WaterY + Rise; Water.position = p; }

    static readonly Color WetC = new Color(0.22f, 0.18f, 0.13f, 0.45f);
    // foam + thin sheet pushing up the bank (outward while rising, back in as it settles), dark wet band that fades
    static void ShoreWash(float k, float u)
    {
        Vector2 c = Layout.PondC, r = Layout.PondR;
        int n = Mathf.RoundToInt((Look.Mobile ? 10f : 20f) * Mathf.Clamp(k, 0.4f, 1f));
        bool out_ = u < 0.3f;
        float push = (out_ ? 1f : -0.6f) * (1.5f + k * 4f);
        for (int i = 0; i < n; i++)
        {
            float a = Random.value * Mathf.PI * 2f;
            Vector3 d = new Vector3(Mathf.Cos(a) * r.x, 0f, Mathf.Sin(a) * r.y);
            Vector3 e = new Vector3(c.x, 0f, c.y) + d * Random.Range(0.97f, 1.03f);
            e.y = Mathf.Max(Ranch.GY(e.x, e.z), Layout.WaterY) + 0.08f;
            Vector3 dir = d.normalized;
            FX.Puff(e, dir * push, 0.8f + k * 1.4f, Random.Range(0.8f, 1.3f), Foam);                    // foam / water sheet
            if (out_ && Random.value < 0.5f)
                FX.Puff(e + dir * (1f + k * 3f), Vector3.zero, 1.2f + k * 2f, Random.Range(3f, 5f), WetC); // wet ground, fades
        }
    }
}

public class RippleDriver : MonoBehaviour { void Update() { Ripples.Tick(Mathf.Min(Time.deltaTime, 0.1f)); } }
