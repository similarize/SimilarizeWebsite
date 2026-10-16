using UnityEngine;

// One world-space particle system, fed with EmitParams for every effect (cheap on WebGL).
// lambo7: particles are soft round sprites (they were untextured squares), bright effects (sparks, flames, embers, boost,
// zaps, muzzle flashes) go to an additive system so they glow, off-road dust takes the track's colour (red desert dust,
// ash + embers on the volcano, snow powder, leaf litter in the jungle, grit in the city), and each theme has ambient
// weather around the player's camera (Ambient: drifting snow, rising embers, dust motes, falling leaves).
public static class FX
{
    static ParticleSystem ps, bubbles, add, amb;
    static Texture2D softTex, sparkTex;

    static Texture2D SoftTex(int n, float power)
    {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
        var px = new Color32[n * n];
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Pow(Mathf.Clamp01(1f - d), power);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        t.SetPixels32(px); t.Apply(true);
        return t;
    }

    static ParticleSystem MakeSys(string name, Material m, int max, float gravity, bool sizeGrow)
    {
        var go = new GameObject(name);
        var p = go.AddComponent<ParticleSystem>();
        p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = p.main;
        main.loop = true; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = max; main.gravityModifier = gravity; main.startSpeed = 0f; main.startLifetime = 1f;
        var em = p.emission; em.enabled = false;
        var sh = p.shape; sh.enabled = false;
        var sz = p.sizeOverLifetime;
        sz.enabled = true;
        sz.size = sizeGrow ? new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1.4f)))
                           : new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.35f)));
        var col = p.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.85f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = m;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.maxParticleSize = 2f;
        p.Play();
        return p;
    }

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
        softTex = SoftTex(64, 1.6f); sparkTex = SoftTex(64, 3.2f);
        var softMat = new Material(Mats.Fx) { mainTexture = softTex };
        rend.sharedMaterial = softMat;
        rend.maxParticleSize = 2f;
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
        br.sharedMaterial = Mats.Fx; br.renderMode = ParticleSystemRenderMode.Billboard;
        br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; br.receiveShadows = false;
        bubbles.Play();
        // additive glow system + ambient weather system
        Material addMat = Mats.FxAdd != null ? new Material(Mats.FxAdd) : new Material(Mats.Fx);
        addMat.mainTexture = sparkTex;
        if (addMat.HasProperty("_TintColor")) addMat.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
        add = MakeSys("FX Glow", addMat, 2500, 0.25f, false);
        amb = MakeSys("FX Ambient", softMat, 900, 0f, true);
        var n = amb.noise; n.enabled = true; n.strength = 0.6f; n.frequency = 0.25f; n.scrollSpeed = 0.2f;
        Debug.Log("FX: lambo7 soft particles (additive " + (Mats.FxAdd != null) + ")");
    }

    static void EmitAdd(Vector3 p, Vector3 v, float size, float life, Color c)
    {
        if (add == null) { Emit(p, v, size, life, c); return; }
        var ep = new ParticleSystem.EmitParams { position = p, velocity = v, startSize = size, startLifetime = life, startColor = c, rotation = Random.Range(0f, 360f) };
        add.Emit(ep, 1);
    }

    // themed off-road kick-up behind a wheel (dust / ash / powder / leaves / grit)
    public static void ThemeDust(Vector3 p, Vector3 fwd, int theme, float amount)
    {
        if (ps == null || Random.value > amount) return;
        Vector3 v = -fwd * Random.Range(1f, 3f) + Random.insideUnitSphere * 0.9f + Vector3.up * Random.Range(0.6f, 1.6f);
        switch (theme)
        {
            case 1: Emit(p, v, Random.Range(0.7f, 1.5f), Random.Range(0.9f, 1.6f), new Color(0.86f, 0.55f, 0.35f, 0.5f)); break;
            case 2:
                Emit(p, v, Random.Range(0.7f, 1.4f), Random.Range(0.9f, 1.5f), new Color(0.28f, 0.26f, 0.25f, 0.55f));
                if (Random.value < 0.35f) EmitAdd(p + Vector3.up * 0.2f, v * 1.4f + Vector3.up * 2f, Random.Range(0.08f, 0.16f), Random.Range(0.5f, 1f), new Color(1f, 0.45f, 0.1f));
                break;
            case 3: Emit(p, v * 1.4f + Vector3.up * 1.2f, Random.Range(0.5f, 1.2f), Random.Range(0.6f, 1.2f), new Color(0.95f, 0.97f, 1f, 0.75f)); break;
            case 4:
                Emit(p, v, Random.Range(0.5f, 1f), Random.Range(0.7f, 1.2f), new Color(0.42f, 0.33f, 0.2f, 0.5f));
                if (Random.value < 0.3f) Emit(p + Vector3.up * 0.3f, v + Vector3.up * 2.5f, Random.Range(0.12f, 0.22f), Random.Range(1f, 1.8f), new Color(0.35f, 0.5f, 0.15f, 0.95f));
                break;
            case 5: Emit(p, v, Random.Range(0.5f, 1f), Random.Range(0.6f, 1.1f), new Color(0.55f, 0.55f, 0.58f, 0.45f)); break;
            default: Emit(p, v, Random.Range(0.6f, 1.2f), Random.Range(0.7f, 1.3f), new Color(0.93f, 0.85f, 0.66f, 0.55f)); break;
        }
    }

    // ambient weather around a camera (called per player view, per frame)
    static float ambAcc;
    public static void Ambient(Vector3 camPos, Vector3 camFwd, int theme, float dt)
    {
        if (amb == null || dt <= 0f) return;
        float rate = theme == 3 ? 70f : theme == 2 ? 26f : theme == 1 ? 10f : theme == 4 ? 9f : 0f;
        if (Look.Mobile) rate *= 0.5f;
        if (LBDisplay.Level >= 2) rate *= 0.4f;
        ambAcc += rate * dt;
        Vector3 c = camPos + Track.Flat(camFwd).normalized * 18f;
        while (ambAcc >= 1f)
        {
            ambAcc -= 1f;
            Vector3 p = c + new Vector3(Random.Range(-26f, 26f), Random.Range(-2f, 14f), Random.Range(-26f, 26f));
            var ep = new ParticleSystem.EmitParams { position = p, rotation = Random.Range(0f, 360f) };
            switch (theme)
            {
                case 3: ep.velocity = new Vector3(Random.Range(-0.6f, 0.6f), -Random.Range(1.2f, 2.2f), Random.Range(-0.6f, 0.6f)); ep.startSize = Random.Range(0.06f, 0.16f); ep.startLifetime = Random.Range(4f, 7f); ep.startColor = new Color(1f, 1f, 1f, 0.9f); break;
                case 2: ep.position = p - Vector3.up * 8f; ep.velocity = new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(0.8f, 2f), Random.Range(-0.4f, 0.4f)); ep.startSize = Random.Range(0.05f, 0.12f); ep.startLifetime = Random.Range(3f, 5f); ep.startColor = new Color(1f, 0.55f, 0.2f, 1f); if (add != null) { add.Emit(ep, 1); continue; } break;
                case 1: ep.velocity = new Vector3(Random.Range(0.5f, 1.5f), Random.Range(-0.1f, 0.2f), Random.Range(-0.3f, 0.3f)); ep.startSize = Random.Range(0.04f, 0.09f); ep.startLifetime = Random.Range(3f, 6f); ep.startColor = new Color(1f, 0.85f, 0.65f, 0.6f); break;
                default: ep.velocity = new Vector3(Random.Range(-0.5f, 0.5f), -Random.Range(0.4f, 0.9f), Random.Range(-0.5f, 0.5f)); ep.startSize = Random.Range(0.12f, 0.22f); ep.startLifetime = Random.Range(4f, 7f); ep.startColor = new Color(0.45f, 0.62f, 0.2f, 0.95f); break;
            }
            amb.Emit(ep, 1);
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

    public static void Emit(Vector3 p, Vector3 v, float size, float life, Color c)
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
            EmitAdd(p + d * 0.6f * scale, d * 7f * scale + Vector3.up * 2f, Random.Range(1.2f, 2.4f) * scale, Random.Range(0.25f, 0.55f), c);
        }
        // smoke
        for (int i = 0; i < 18; i++)
        {
            Vector3 d = Random.insideUnitSphere;
            float g = Random.Range(0.42f, 0.6f) + (Track.Theme == 3 ? 0.2f : 0f);   // lambo9: lighter smoke (dark blobs floated over Snowy)
            Emit(p + d * scale, d * 3f * scale + Vector3.up * 3.5f, Random.Range(1.6f, 3f) * scale, Random.Range(0.9f, 1.6f), new Color(g, g, g * 1.02f, 0.5f));
        }
        // sparks / debris
        for (int i = 0; i < 24; i++)
        {
            Vector3 d = Random.onUnitSphere; d.y = Mathf.Abs(d.y) + 0.3f;
            EmitAdd(p, d * Random.Range(8f, 16f) * scale, Random.Range(0.1f, 0.2f), Random.Range(0.5f, 1.1f), new Color(1f, 0.7f, 0.25f));
        }
    }

    // lambo9 tyre smoke from a sliding / spinning rear wheel on tarmac: pale, soft, grows and lingers
    public static void TyreSmoke(Vector3 p, Vector3 carVel, float str)
    {
        if (ps == null) return;
        float g = Track.Theme == 2 ? 0.62f : 0.86f;
        Emit(p + Vector3.up * 0.25f + Random.insideUnitSphere * 0.2f, carVel + Random.insideUnitSphere * 0.7f + Vector3.up * Random.Range(0.4f, 1.1f),
             Random.Range(0.9f, 1.7f) * (0.6f + 0.6f * str), Random.Range(1.1f, 1.9f), new Color(g, g, g * 1.02f, 0.18f + 0.22f * str));
    }

    public static void Smoke(Vector3 p, float size, Color c)
    {
        if (ps == null) return;
        Emit(p + Random.insideUnitSphere * 0.1f, Random.insideUnitSphere * 0.4f + Vector3.up * 0.6f, size, Random.Range(0.6f, 1.1f), c);
    }

    // u6i Roadster cold-gas thruster puff: dense white vapour jet out of a nozzle (dir = jet direction, world), grows + fades;
    // carVel keeps the cloud moving with the car for a moment (no flame, it is compressed air)
    public static void ColdGas(Vector3 p, Vector3 dir, Vector3 carVel, float strength)
    {
        if (ps == null || strength <= 0f) return;
        // u6i2: the jets used to fire straight into the road (gone in a frame) - now a short bright jet + a vapour cloud that
        // billows out along the ground / behind the car and lingers, big enough to read from the chase cam
        bool down = dir.y < -0.5f;
        int n = Look.Mobile ? 2 : 3;
        for (int i = 0; i < n; i++)
        {
            float g = Random.Range(0.9f, 1f);
            Vector3 spread = Random.insideUnitSphere; spread.y = Mathf.Abs(spread.y) * 0.4f;
            Vector3 v = carVel * 0.45f + (down ? new Vector3(spread.x * 4f, Random.Range(0.3f, 1.4f), spread.z * 4f) : dir * Random.Range(3f, 6f) * strength + spread * 1.2f);
            Vector3 at = p + (down ? dir * Random.Range(0.05f, 0.25f) : dir * Random.Range(0.1f, 0.5f));
            Emit(at, v, Random.Range(0.9f, 1.6f) * (0.7f + 0.5f * strength), Random.Range(0.7f, 1.2f), new Color(g, g, g * 1.02f, 0.5f + 0.35f * strength));
        }
        if (Random.value < 0.5f * strength) EmitAdd(p, dir * 2f + carVel * 0.5f, Random.Range(0.25f, 0.4f), 0.12f, new Color(0.8f, 0.9f, 1f));   // cold flash at the throat
    }

    public static void Flame(Vector3 p, Vector3 dir)
    {
        if (ps == null) return;
        EmitAdd(p, dir * 6f + Random.insideUnitSphere, Random.Range(0.3f, 0.55f), 0.16f, new Color(1f, 0.55f, 0.15f));
        if (Random.value < 0.5f) EmitAdd(p, dir * 4f + Random.insideUnitSphere * 0.5f, Random.Range(0.18f, 0.3f), 0.1f, new Color(0.5f, 0.7f, 1f));
    }

    // drift sparks from the rear wheels (blue = mini-turbo 1, orange = mini-turbo 2)
    public static void Spark(Vector3 p, Vector3 back, Color c)
    {
        if (ps == null) return;
        EmitAdd(p, back * Random.Range(2f, 5f) + Random.insideUnitSphere * 1.5f + Vector3.up * 1.5f, Random.Range(0.1f, 0.2f), Random.Range(0.18f, 0.35f), c);
    }

    // lightning bolt: a column of bright flashes from the sky down to the car
    public static void Zap(Vector3 p)
    {
        if (ps == null) return;
        Vector3 q = p + Vector3.up * 22f;
        for (int i = 0; i < 24; i++)
        {
            Vector3 a = Vector3.Lerp(q, p, i / 23f) + new Vector3(Random.Range(-0.7f, 0.7f), 0f, Random.Range(-0.7f, 0.7f));
            EmitAdd(a, Vector3.zero, Random.Range(0.7f, 1.3f), Random.Range(0.12f, 0.25f), new Color(0.75f, 0.85f, 1f));
        }
        for (int i = 0; i < 14; i++) EmitAdd(p, Random.insideUnitSphere * 6f + Vector3.up * 3f, Random.Range(0.15f, 0.3f), 0.4f, new Color(1f, 1f, 0.6f));
    }

    public static void Dust(Vector3 p, float amount)
    {
        if (ps == null || Random.value > amount) return;
        Emit(p + Random.insideUnitSphere * 0.3f, Random.insideUnitSphere * 0.8f + Vector3.up * 0.7f, Random.Range(0.5f, 1.1f), Random.Range(0.7f, 1.3f), new Color(0.93f, 0.85f, 0.66f, 0.55f));
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
            EmitAdd(p + dir * Random.Range(0f, 0.8f), dir * Random.Range(4f, 10f) + Random.insideUnitSphere * 2f, Random.Range(0.4f, 0.9f), Random.Range(0.1f, 0.25f), new Color(1f, 0.7f, 0.3f));
        for (int i = 0; i < 6; i++)
            Emit(p + dir * 0.5f, dir * 2f + Random.insideUnitSphere + Vector3.up, Random.Range(0.8f, 1.4f), Random.Range(0.6f, 1f), new Color(0.6f, 0.6f, 0.6f, 0.6f));
    }
}
