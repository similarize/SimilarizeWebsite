using UnityEngine;

// One world-space particle system, fed with EmitParams for every effect (cheap on WebGL).
public static class FX
{
    static ParticleSystem ps;

    public static void Init()
    {
        var go = new GameObject("FX");
        ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 3000;
        main.gravityModifier = 0.35f;
        main.startLifetime = 1.2f;
        main.startSpeed = 0f;
        var em = ps.emission;
        em.enabled = false;
        var sh = ps.shape;
        sh.enabled = false;
        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.25f, 1f), new Keyframe(1f, 1.5f)));
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = Mats.Fx;
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        ps.Play();
    }

    static void Emit(Vector3 p, Vector3 v, float size, float life, Color c)
    {
        var ep = new ParticleSystem.EmitParams();
        ep.position = p;
        ep.velocity = v;
        ep.startSize = size;
        ep.startLifetime = life;
        ep.startColor = c;
        ep.rotation = Random.Range(0f, 360f);
        ps.Emit(ep, 1);
    }

    public static void Boom(Vector3 p, float scale)
    {
        if (ps == null) return;
        // fireball
        for (int i = 0; i < 26; i++)
        {
            Vector3 d = Random.insideUnitSphere;
            Color c = Color.Lerp(new Color(1f, 0.9f, 0.3f), new Color(1f, 0.35f, 0.05f), Random.value);
            Emit(p + d * 0.6f * scale, d * 7f * scale + Vector3.up * 2f, Random.Range(1.2f, 2.4f) * scale, Random.Range(0.25f, 0.55f), c);
        }
        // smoke
        for (int i = 0; i < 18; i++)
        {
            Vector3 d = Random.insideUnitSphere;
            float g = Random.Range(0.18f, 0.38f);
            Emit(p + d * scale, d * 3f * scale + Vector3.up * 3.5f, Random.Range(1.6f, 3f) * scale, Random.Range(1.2f, 2.2f), new Color(g, g, g, 0.8f));
        }
        // sparks / debris
        for (int i = 0; i < 24; i++)
        {
            Vector3 d = Random.onUnitSphere; d.y = Mathf.Abs(d.y) + 0.3f;
            Emit(p, d * Random.Range(8f, 16f) * scale, Random.Range(0.12f, 0.25f), Random.Range(0.5f, 1.1f), i % 3 == 0 ? new Color(0.25f, 0.2f, 0.15f) : new Color(1f, 0.75f, 0.3f));
        }
    }

    public static void Smoke(Vector3 p, float size, Color c)
    {
        if (ps == null) return;
        Emit(p + Random.insideUnitSphere * 0.1f, Random.insideUnitSphere * 0.4f + Vector3.up * 0.6f, size, Random.Range(0.6f, 1.1f), c);
    }

    public static void Flame(Vector3 p, Vector3 dir)
    {
        if (ps == null) return;
        Emit(p, dir * 6f + Random.insideUnitSphere, Random.Range(0.25f, 0.45f), 0.15f, new Color(1f, 0.7f, 0.2f));
    }

    public static void Dust(Vector3 p, float amount)
    {
        if (ps == null || Random.value > amount) return;
        Emit(p + Random.insideUnitSphere * 0.3f, Random.insideUnitSphere * 0.8f + Vector3.up * 0.7f, Random.Range(0.5f, 1.1f), Random.Range(0.7f, 1.3f), new Color(0.62f, 0.52f, 0.38f, 0.55f));
    }

    public static void Splash(Vector3 p, float amount)
    {
        if (ps == null) return;
        int n = Mathf.Clamp(Mathf.RoundToInt(amount), 1, 30);
        for (int i = 0; i < n; i++)
        {
            Vector3 d = Random.insideUnitSphere; d.y = Mathf.Abs(d.y) * 2f + 0.5f;
            Emit(p, d * 2.5f, Random.Range(0.2f, 0.45f), Random.Range(0.4f, 0.8f), new Color(0.85f, 0.95f, 1f, 0.8f));
        }
    }

    public static void Muzzle(Vector3 p, Vector3 dir)
    {
        if (ps == null) return;
        for (int i = 0; i < 10; i++)
            Emit(p + dir * Random.Range(0f, 0.8f), dir * Random.Range(4f, 10f) + Random.insideUnitSphere * 2f, Random.Range(0.4f, 0.9f), Random.Range(0.1f, 0.25f), new Color(1f, 0.8f, 0.35f));
        for (int i = 0; i < 6; i++)
            Emit(p + dir * 0.5f, dir * 2f + Random.insideUnitSphere + Vector3.up, Random.Range(0.8f, 1.4f), Random.Range(0.6f, 1f), new Color(0.6f, 0.6f, 0.6f, 0.6f));
    }
}
