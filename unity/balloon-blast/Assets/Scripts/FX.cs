using UnityEngine;

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
        main.maxParticles = 4000;
        main.gravityModifier = 0.9f;
        main.startLifetime = 1.2f;
        main.startSpeed = 0f;
        var em = ps.emission;
        em.enabled = false;
        var sh = ps.shape;
        sh.enabled = false;
        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = Mats.Fx;
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        ps.Play();
    }

    public static void Pop(Vector3 p, Color c)
    {
        if (ps == null) return;
        var ep = new ParticleSystem.EmitParams();
        for (int i = 0; i < 40; i++)
        {
            ep.position = p + Random.insideUnitSphere * 0.15f;
            ep.velocity = Random.insideUnitSphere * 4.5f + Vector3.up * 1.2f;
            ep.startSize = Random.Range(0.05f, 0.14f);
            ep.startLifetime = Random.Range(0.6f, 1.3f);
            ep.startColor = (i % 5 == 0) ? (Color32)Color.white : (Color32)c;
            ep.rotation = Random.Range(0f, 360f);
            ps.Emit(ep, 1);
        }
        // quick white flash ring
        for (int i = 0; i < 10; i++)
        {
            Vector3 d = Random.onUnitSphere;
            ep.position = p + d * 0.2f;
            ep.velocity = d * 7f;
            ep.startSize = 0.22f;
            ep.startLifetime = 0.18f;
            ep.startColor = new Color32(255, 255, 255, 200);
            ps.Emit(ep, 1);
        }
    }

    public static void Puff(Vector3 p, Vector3 normal)
    {
        if (ps == null) return;
        var ep = new ParticleSystem.EmitParams();
        for (int i = 0; i < 5; i++)
        {
            ep.position = p + normal * 0.03f;
            ep.velocity = (normal + Random.insideUnitSphere * 0.8f) * 1.2f;
            ep.startSize = Random.Range(0.03f, 0.06f);
            ep.startLifetime = Random.Range(0.25f, 0.5f);
            ep.startColor = new Color32(220, 210, 180, 255);
            ps.Emit(ep, 1);
        }
    }
}
