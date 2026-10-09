using UnityEngine;

// Particle effects (all pooled world-space systems, emitted by hand):
//  - Pop: latex shreds in the balloon colour + rainbow confetti that flutters down (quad meshes with 3D spin),
//    a soft white flash and a quick ring of sparkles.
//  - Muzzle: a tiny warm flash + a white puff at the barrel tip.  - Puff: dust where a BB hits cover.
public static class FX
{
    static ParticleSystem ps, confetti, glow;
    static readonly Color32[] Rainbow =
    {
        new Color32(255, 70, 70, 255), new Color32(255, 200, 40, 255), new Color32(70, 200, 90, 255), new Color32(60, 150, 255, 255),
        new Color32(190, 90, 255, 255), new Color32(255, 120, 200, 255), new Color32(60, 230, 230, 255), new Color32(255, 255, 255, 255)
    };

    static Texture2D Soft()
    {
        const int n = 64;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, true);
        t.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        t.SetPixels32(px);
        t.Apply(true);
        return t;
    }

    static Mesh Quad()
    {
        var m = new Mesh { name = "ConfettiQuad" };
        m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
        m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        m.RecalculateBounds();
        return m;
    }

    static ParticleSystem Make(string name, float gravity, int max)
    {
        var go = new GameObject(name);
        var p = go.AddComponent<ParticleSystem>();
        p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = p.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = max;
        main.gravityModifier = gravity;
        main.startLifetime = 1.2f;
        main.startSpeed = 0f;
        var em = p.emission; em.enabled = false;
        var sh = p.shape; sh.enabled = false;
        return p;
    }

    public static void Init()
    {
        // shreds + dust + muzzle puffs: soft round billboards
        ps = Make("FX", 0.9f, 3000);
        var r = ps.GetComponent<ParticleSystemRenderer>();
        Material soft = new Material(Mats.Fx);
        soft.mainTexture = Soft();
        r.sharedMaterial = soft;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        var col = ps.colorOverLifetime; col.enabled = true;
        var gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                   new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = gr;
        ps.Play();

        // confetti + latex shreds: spinning quads that drift down
        confetti = Make("Confetti", 0.32f, 3000);
        var cm = confetti.main;
        cm.startRotation3D = true;
        cm.startSize3D = true;
        var rot = confetti.rotationOverLifetime; rot.enabled = true; rot.separateAxes = true;
        rot.x = new ParticleSystem.MinMaxCurve(-9f, 9f); rot.y = new ParticleSystem.MinMaxCurve(-7f, 7f); rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
        var lim = confetti.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 2.2f; lim.dampen = 0.12f;
        var noise = confetti.noise; noise.enabled = true; noise.strength = 0.7f; noise.frequency = 0.8f; noise.scrollSpeed = 0.4f;
        var cc = confetti.colorOverLifetime; cc.enabled = true;
        var g2 = new Gradient();
        g2.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                   new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        cc.color = g2;
        var cr = confetti.GetComponent<ParticleSystemRenderer>();
        cr.sharedMaterial = Mats.Fx;
        cr.renderMode = ParticleSystemRenderMode.Mesh;
        cr.mesh = Quad();
        cr.alignment = ParticleSystemRenderSpace.World;
        confetti.Play();

        // flashes: soft glow sprites, no gravity, short
        glow = Make("Glow", 0f, 400);
        var gw = glow.GetComponent<ParticleSystemRenderer>();
        gw.sharedMaterial = soft;
        gw.renderMode = ParticleSystemRenderMode.Billboard;
        var sz = glow.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.4f));
        var gc = glow.colorOverLifetime; gc.enabled = true;
        var g3 = new Gradient();
        g3.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                   new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        gc.color = g3;
        glow.Play();
    }

    public static void Pop(Vector3 p, Color c)
    {
        if (ps == null) return;
        var ep = new ParticleSystem.EmitParams();
        Color32 cc = c;
        Color32 light = Color.Lerp(c, Color.white, 0.35f);
        // latex shreds (balloon colour), bigger, fast then fluttering
        for (int i = 0; i < 26; i++)
        {
            ep.position = p + Random.insideUnitSphere * 0.15f;
            ep.velocity = Random.onUnitSphere * Random.Range(2.5f, 6f) + Vector3.up * 1.2f;
            ep.startSize3D = new Vector3(Random.Range(0.07f, 0.16f), Random.Range(0.05f, 0.11f), 1f);
            ep.startLifetime = Random.Range(1.0f, 2.0f);
            ep.startColor = (i % 4 == 0) ? light : cc;
            ep.rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
            confetti.Emit(ep, 1);
        }
        // rainbow confetti
        int n = Look.Mobile ? 34 : 60;
        for (int i = 0; i < n; i++)
        {
            ep.position = p + Random.insideUnitSphere * 0.2f;
            ep.velocity = Random.onUnitSphere * Random.Range(1.5f, 5f) + Vector3.up * 2.4f;
            float s = Random.Range(0.045f, 0.08f);
            ep.startSize3D = new Vector3(s, s * Random.Range(0.45f, 0.75f), 1f);
            ep.startLifetime = Random.Range(1.6f, 2.8f);
            ep.startColor = Rainbow[Random.Range(0, Rainbow.Length)];
            ep.rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
            confetti.Emit(ep, 1);
        }
        ep = new ParticleSystem.EmitParams();
        // soft white flash + colour bloom
        ep.position = p; ep.velocity = Vector3.zero;
        ep.startSize = 1.3f; ep.startLifetime = 0.16f; ep.startColor = new Color32(255, 255, 255, 230);
        glow.Emit(ep, 1);
        ep.startSize = 1.0f; ep.startLifetime = 0.3f; ep.startColor = new Color32(cc.r, cc.g, cc.b, 170);
        glow.Emit(ep, 1);
        // sparkle ring
        for (int i = 0; i < 14; i++)
        {
            Vector3 d = Random.onUnitSphere;
            ep.position = p + d * 0.2f;
            ep.velocity = d * 7.5f;
            ep.startSize = Random.Range(0.08f, 0.15f);
            ep.startLifetime = Random.Range(0.18f, 0.3f);
            ep.startColor = new Color32(255, 250, 220, 255);
            ps.Emit(ep, 1);
        }
    }

    public static void Muzzle(Vector3 p, Vector3 dir)
    {
        if (ps == null) return;
        var ep = new ParticleSystem.EmitParams();
        ep.position = p + dir * 0.02f; ep.velocity = dir * 0.5f;
        ep.startSize = 0.11f; ep.startLifetime = 0.05f; ep.startColor = new Color32(255, 225, 150, 220);
        glow.Emit(ep, 1);
        for (int i = 0; i < 2; i++)
        {
            ep.position = p + dir * 0.03f;
            ep.velocity = dir * Random.Range(1.2f, 2.2f) + Random.insideUnitSphere * 0.25f;
            ep.startSize = Random.Range(0.04f, 0.07f);
            ep.startLifetime = Random.Range(0.18f, 0.32f);
            ep.startColor = new Color32(235, 235, 235, 120);
            ps.Emit(ep, 1);
        }
    }

    public static void Puff(Vector3 p, Vector3 normal)
    {
        if (ps == null) return;
        var ep = new ParticleSystem.EmitParams();
        for (int i = 0; i < 6; i++)
        {
            ep.position = p + normal * 0.03f;
            ep.velocity = (normal + Random.insideUnitSphere * 0.8f) * 1.2f;
            ep.startSize = Random.Range(0.05f, 0.1f);
            ep.startLifetime = Random.Range(0.25f, 0.5f);
            ep.startColor = new Color32(225, 215, 185, 200);
            ps.Emit(ep, 1);
        }
    }
}
