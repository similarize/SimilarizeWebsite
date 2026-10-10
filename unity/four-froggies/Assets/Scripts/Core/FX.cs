using UnityEngine;

// One world-space particle system, fed with EmitParams for every effect (cheap on WebGL).
public static class FX
{
    static ParticleSystem ps, bubbles;

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
        // ffu21: soft round puff texture - Mats.Fx (Sprites/Default) has no texture, so every fireball / smoke puff /
        // dust particle used to draw as a flat grey / orange SQUARE
        rend.sharedMaterial = SoftMat();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        ps.Play();

        // bubbles rise (negative gravity) in their own little system
        var bgo = new GameObject("FX Bubbles");
        bubbles = bgo.AddComponent<ParticleSystem>();
        bubbles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var bm = bubbles.main;
        bm.loop = true; bm.playOnAwake = false; bm.simulationSpace = ParticleSystemSimulationSpace.World;
        bm.maxParticles = 600; bm.gravityModifier = -0.12f; bm.startSpeed = 0f;
        var bem = bubbles.emission; bem.enabled = false;
        var bsh = bubbles.shape; bsh.enabled = false;
        var bcol = bubbles.colorOverLifetime; bcol.enabled = true;
        var bg = new Gradient();
        bg.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.6f, 0.7f), new GradientAlphaKey(0f, 1f) });
        bcol.color = bg;
        var bn = bubbles.noise; bn.enabled = true; bn.strength = 0.4f; bn.frequency = 0.8f;
        var br = bgo.GetComponent<ParticleSystemRenderer>();
        br.sharedMaterial = SoftMat(); br.renderMode = ParticleSystemRenderMode.Billboard;
        br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; br.receiveShadows = false;
        bubbles.Play();
    }

    // ffu21: one soft, slightly lumpy round puff (64 px, alpha falls off smoothly; a little value noise so smoke reads
    // as billowy instead of a perfect disc). Shared by the main FX system and the bubbles.
    static Material softMat;
    public static Material SoftMat()
    {
        if (softMat != null) return softMat;
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Trilinear;
        var px = new Color32[n * n];
        var rnd = new System.Random(7);
        float[] noise = new float[16 * 16];
        for (int i = 0; i < noise.Length; i++) noise[i] = (float)rnd.NextDouble();
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                // bilinear value noise (16x16 grid) for a lumpy edge + soft inner variation
                float gx = (x + 0.5f) / n * 15f, gy = (y + 0.5f) / n * 15f;
                int ix = Mathf.Min(14, (int)gx), iy = Mathf.Min(14, (int)gy);
                float fx = gx - ix, fy = gy - iy;
                float nv = Mathf.Lerp(Mathf.Lerp(noise[iy * 16 + ix], noise[iy * 16 + ix + 1], fx), Mathf.Lerp(noise[(iy + 1) * 16 + ix], noise[(iy + 1) * 16 + ix + 1], fx), fy);
                float edge = r + (nv - 0.5f) * 0.22f;
                float a = Mathf.Clamp01((1f - edge) / 0.55f); a = a * a * (3f - 2f * a);
                float v = 0.86f + 0.14f * nv;
                px[y * n + x] = new Color32((byte)(v * 255f), (byte)(v * 255f), (byte)(v * 255f), (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply(true);
        softMat = new Material(Mats.Fx) { name = "FX soft puff" };
        softMat.mainTexture = tex;
        return softMat;
    }

    // ffu21: a ring of dust thrown outwards along the ground (mech topple impact shockwave, big landings)
    public static void Ring(Vector3 p, float radius, int n, float speed, Color c)
    {
        if (ps == null) return;
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n + Random.Range(-0.05f, 0.05f);
            Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Emit(p + d * radius * Random.Range(0.2f, 0.45f) + Vector3.up * Random.Range(0.2f, 1f) * radius * 0.05f,
                 d * speed * Random.Range(0.75f, 1.2f) + Vector3.up * speed * Random.Range(0.05f, 0.18f),
                 radius * Random.Range(0.1f, 0.2f), Random.Range(1.4f, 2.6f), c);
        }
    }

    // ffu21: a big billowing dust / smoke cloud (collapses, poofs)
    public static void Cloud(Vector3 p, float size, int n, Color c)
    {
        if (ps == null) return;
        for (int i = 0; i < n; i++)
        {
            Vector3 d = Random.insideUnitSphere;
            Emit(p + d * size * 0.5f, d * size * 0.9f + Vector3.up * size * 0.5f, size * Random.Range(0.5f, 0.9f), Random.Range(1.0f, 2.2f), c);
        }
    }

    public static void Bubble(Vector3 p, int n)
    {
        if (bubbles == null) return;
        for (int i = 0; i < n; i++)
        {
            var ep = new ParticleSystem.EmitParams();
            ep.position = p + Random.insideUnitSphere * 0.15f;
            ep.velocity = Vector3.up * Random.Range(0.6f, 1.4f) + Random.insideUnitSphere * 0.3f;
            ep.startSize = Random.Range(0.06f, 0.18f);
            ep.startLifetime = Random.Range(1.5f, 3f);
            ep.startColor = new Color(0.85f, 0.95f, 1f, 0.8f);
            bubbles.Emit(ep, 1);
        }
    }

    // generic coloured puff (pickups, beacons)
    public static void Sparkle(Vector3 p, Color c, int n)
    {
        if (ps == null) return;
        for (int i = 0; i < n; i++) Emit(p, Random.insideUnitSphere * 3f + Vector3.up * 2f, Random.Range(0.15f, 0.35f), Random.Range(0.4f, 0.8f), c);
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

    // ffu11: directed water spray (Cyberboat bow spray, rooster tail, transform burst)
    // own system with a soft round droplet texture (the shared one draws plain quads)
    static ParticleSystem spray;
    public static void Spray(Vector3 p, Vector3 v, float size, float life, Color c)
    {
        if (ps == null) return;
        if (spray == null) InitSpray();
        var ep = new ParticleSystem.EmitParams();
        ep.position = p; ep.velocity = v; ep.startSize = size; ep.startLifetime = life; ep.startColor = c;
        ep.rotation = Random.Range(0f, 360f);
        spray.Emit(ep, 1);
    }
    static void InitSpray()
    {
        var go = new GameObject("FX Spray");
        spray = go.AddComponent<ParticleSystem>();
        spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = spray.main;
        main.loop = true; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 1500; main.gravityModifier = 0.9f; main.startSpeed = 0f;
        var em = spray.emission; em.enabled = false;
        var sh = spray.shape; sh.enabled = false;
        var sz = spray.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1.8f)));
        var col = spray.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        const int n = 32;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - r); a = a * a * (3f - 2f * a);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        var m = new Material(Mats.Fx); m.mainTexture = tex;
        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = m; rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rend.receiveShadows = false;
        spray.Play();
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
