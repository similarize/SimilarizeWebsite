using UnityEngine;

// ffu32 GARDEN RETREAT campfire: a stone ring with a log teepee. A froggy presses A / E / tap at the ring to light it:
// a match spark, the kindling catches (whoomp), flames grow over ~3 s with a crackle loop, ONE flickering warm point light
// (no shadows; vertex-lit on phones), smoke and drifting embers. Press again to douse it: a hiss, a burst of steam,
// smoulder smoke for a few seconds. Purely visual: no damage, no Destruct calls, nothing breaks.
public class Campfire : MonoBehaviour
{
    public static Campfire I;
    public bool Lit { get { return state == 1 || state == 2; } }
    int state;           // 0 out, 1 igniting, 2 burning, 3 dousing
    float k, t, smoulderT, emitAcc, emberAcc, smokeAcc;
    Light fireLight;
    Transform[] tongues;
    Vector3[] tongueBase;
    AudioSource crackle;
    ParticleSystem flamePs;
    public Vector3 Pos { get { return transform.position; } }
    public string LitBy = "";

    public static Campfire Build(Transform parent, Vector3 pos)
    {
        var go = new GameObject("Campfire");
        go.transform.SetParent(parent, true);
        go.transform.position = pos;
        I = go.AddComponent<Campfire>();
        I.Make();
        return I;
    }

    void Make()
    {
        Transform t0 = transform;
        var r = new System.Random(321);
        Material stone = Mats.Lit(new Color(0.52f, 0.5f, 0.47f)), stoneD = Mats.Lit(new Color(0.4f, 0.39f, 0.37f));
        Material bark = Mats.TexTint("LB/wood", new Color(0.42f, 0.29f, 0.18f), 0.06f), endC = Mats.Lit(new Color(0.78f, 0.62f, 0.42f));
        // ash bed + charred ground
        Mats.Prim(PrimitiveType.Cylinder, t0, new Vector3(0f, 0.01f, 0f), new Vector3(1.55f, 0.01f, 1.55f), Mats.Lit(new Color(0.17f, 0.15f, 0.14f)));
        Mats.Prim(PrimitiveType.Cylinder, t0, new Vector3(0f, 0.025f, 0f), new Vector3(1.05f, 0.01f, 1.05f), Mats.Lit(new Color(0.32f, 0.3f, 0.28f)));
        // ring of 12 stones
        for (int i = 0; i < 12; i++)
        {
            float a = i / 12f * Mathf.PI * 2f + (float)r.NextDouble() * 0.15f;
            float s = 0.3f + (float)r.NextDouble() * 0.12f;
            var g = Mats.Prim(PrimitiveType.Sphere, t0, new Vector3(Mathf.Cos(a) * 0.95f, s * 0.28f, Mathf.Sin(a) * 0.95f), new Vector3(s * 1.2f, s * 0.75f, s), i % 3 == 0 ? stoneD : stone);
            g.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + (float)r.NextDouble() * 30f, (float)r.NextDouble() * 10f);
        }
        // log teepee (5 logs leaning in) + two cross logs at the base
        for (int i = 0; i < 5; i++)
        {
            float a = i / 5f * Mathf.PI * 2f + 0.3f;
            Vector3 foot = new Vector3(Mathf.Cos(a) * 0.55f, 0.06f, Mathf.Sin(a) * 0.55f), top = new Vector3(Mathf.Cos(a) * 0.06f, 0.78f, Mathf.Sin(a) * 0.06f);
            var lg = Mats.Prim(PrimitiveType.Cylinder, t0, (foot + top) * 0.5f, new Vector3(0.11f, (top - foot).magnitude * 0.5f, 0.11f), bark);
            lg.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (top - foot).normalized);
            lg.name = "Log";
        }
        for (int i = 0; i < 2; i++)
        {
            var lg = Mats.Prim(PrimitiveType.Cylinder, t0, new Vector3(0f, 0.09f + i * 0.1f, 0f), new Vector3(0.14f, 0.5f, 0.14f), new Vector3(90f, i * 90f + 20f, 0f), bark);
            lg.name = "Log";
        }
        // spare log pile + a water bucket beside the ring
        for (int i = 0; i < 3; i++)
        {
            Vector3 p = new Vector3(1.75f, 0.12f + (i == 2 ? 0.2f : 0f), 0.55f + (i == 2 ? 0f : (i - 0.5f) * 0.26f));
            Mats.Prim(PrimitiveType.Cylinder, t0, p, new Vector3(0.22f, 0.42f, 0.22f), new Vector3(90f, 8f, 0f), bark);
            for (int e = -1; e <= 1; e += 2) Mats.Prim(PrimitiveType.Cylinder, t0, p + Quaternion.Euler(0f, 8f, 0f) * new Vector3(0f, 0f, e * 0.42f), new Vector3(0.2f, 0.005f, 0.2f), new Vector3(90f, 8f, 0f), endC);
        }
        Mats.Prim(PrimitiveType.Cylinder, t0, new Vector3(-1.7f, 0.22f, 0.8f), new Vector3(0.38f, 0.22f, 0.38f), Mats.Steel(new Color(0.55f, 0.57f, 0.6f)));
        Mats.Prim(PrimitiveType.Cylinder, t0, new Vector3(-1.7f, 0.42f, 0.8f), new Vector3(0.33f, 0.01f, 0.33f), Mats.Shiny(new Color(0.25f, 0.45f, 0.6f)));
        // flame tongues (lathed teardrops, unlit): outer orange, mid amber, inner yellow-white
        Mesh tear = Teardrop();
        Color[] fc = { new Color(1f, 0.38f, 0.06f), new Color(1f, 0.62f, 0.12f), new Color(1f, 0.9f, 0.55f) };
        Vector3[] fp = { new Vector3(0f, 0.1f, 0f), new Vector3(0.14f, 0.12f, 0.08f), new Vector3(-0.12f, 0.1f, -0.06f), new Vector3(0.02f, 0.14f, 0.02f), new Vector3(-0.05f, 0.12f, 0.14f) };
        Vector3[] fs = { new Vector3(0.62f, 1.05f, 0.62f), new Vector3(0.38f, 0.8f, 0.38f), new Vector3(0.36f, 0.72f, 0.36f), new Vector3(0.3f, 0.66f, 0.3f), new Vector3(0.28f, 0.6f, 0.28f) };
        int[] fcol = { 0, 1, 1, 2, 0 };
        tongues = new Transform[fp.Length]; tongueBase = fs;
        for (int i = 0; i < fp.Length; i++)
        {
            var g = new GameObject("Flame");
            g.transform.SetParent(t0, false);
            g.transform.localPosition = fp[i];
            g.AddComponent<MeshFilter>().sharedMesh = tear;
            var mr = g.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Unlit(fc[fcol[i]]);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            g.transform.localScale = Vector3.zero;
            tongues[i] = g.transform;
        }
        // the one fire light: no shadows, vertex-lit + shorter on phones
        var lg2 = new GameObject("Fire light");
        lg2.transform.SetParent(t0, false);
        lg2.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        fireLight = lg2.AddComponent<Light>();
        fireLight.type = LightType.Point;
        fireLight.color = new Color(1f, 0.6f, 0.28f);
        fireLight.range = Look.Mobile ? 7f : 11f;
        fireLight.intensity = 0f;
        fireLight.shadows = LightShadows.None;
        fireLight.renderMode = Look.Mobile ? LightRenderMode.ForceVertex : LightRenderMode.Auto;
        fireLight.enabled = false;
        BuildParticles();
        GardenAudio.Ensure();
        crackle = Sfx.Loop(gameObject, GardenAudio.Crackle);
        var h = Interact.Add(transform.position + Vector3.up * 0.2f, 2.5f, "light the campfire", f => Toggle(f));
        h.dynLabel = f => Lit ? "put out the campfire" : "light the campfire";
        h.enabled = f => !Fig50.UsesFire(f);   // ffu50: holding a drumstick (or one is cooking for you) = cook / eat instead
    }

    static Mesh Teardrop()
    {
        // radius profile along y 0..1: round bulb at the bottom, pointed licking tip at the top
        int rings = 10, seg = 10;
        var v = new System.Collections.Generic.List<Vector3>(); var tri = new System.Collections.Generic.List<int>();
        for (int i = 0; i <= rings; i++)
        {
            float y = i / (float)rings;
            float rr = y < 0.25f ? Mathf.Sin(y / 0.25f * Mathf.PI * 0.5f) * 0.5f : 0.5f * Mathf.Pow(1f - (y - 0.25f) / 0.75f, 1.4f);
            for (int j = 0; j < seg; j++)
            {
                float a = j / (float)seg * Mathf.PI * 2f;
                v.Add(new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr));
            }
        }
        for (int i = 0; i < rings; i++)
            for (int j = 0; j < seg; j++)
            {
                int a = i * seg + j, b = i * seg + (j + 1) % seg, c = a + seg, d = b + seg;
                tri.Add(a); tri.Add(c); tri.Add(b); tri.Add(b); tri.Add(c); tri.Add(d);
            }
        var m = new Mesh { name = "FlameTear" };
        m.SetVertices(v); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    void BuildParticles()
    {
        var go = new GameObject("Fire FX");
        go.transform.SetParent(transform, false);
        flamePs = go.AddComponent<ParticleSystem>();
        flamePs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = flamePs.main;
        main.loop = true; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Look.Mobile ? 160 : 320; main.gravityModifier = -0.05f; main.startSpeed = 0f;
        var em = flamePs.emission; em.enabled = false;
        var sh = flamePs.shape; sh.enabled = false;
        var sz = flamePs.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.8f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.15f)));
        var col = flamePs.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f), new GradientColorKey(new Color(1f, 0.6f, 0.15f), 0.35f), new GradientColorKey(new Color(0.85f, 0.2f, 0.05f), 1f) },
                  new[] { new GradientAlphaKey(0.0f, 0f), new GradientAlphaKey(0.9f, 0.12f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var noise = flamePs.noise; noise.enabled = true; noise.strength = 0.6f; noise.frequency = 1.4f; noise.scrollSpeed = 1.2f;
        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = FX.SoftMat();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rend.receiveShadows = false;
        flamePs.Play();
    }

    void Emit(Vector3 p, Vector3 v, float size, float life, Color c)
    {
        var ep = new ParticleSystem.EmitParams();
        ep.position = p; ep.velocity = v; ep.startSize = size; ep.startLifetime = life; ep.startColor = c;
        ep.rotation = Random.Range(0f, 360f);
        flamePs.Emit(ep, 1);
    }

    public void Toggle(Frog by)
    {
        if (Lit) Douse(by); else Light(by);
    }

    public void Light(Frog by)
    {
        if (Lit) return;
        state = 1; t = 0f;
        LitBy = by != null ? by.nick : "";
        Vector3 p = transform.position + Vector3.up * 0.35f;
        FX.Sparkle(p, new Color(1f, 0.8f, 0.35f), 14);
        Sfx.PlayAt(GardenAudio.Ignite, p, 0.8f, 40f, Random.Range(0.95f, 1.05f));
        if (by != null && by.human) by.Toast("The campfire crackles to life!", 2.2f);
        Debug.Log("Campfire: lit" + (by != null ? " by " + by.nick : "") + " t=" + Time.realtimeSinceStartup.ToString("0.0"));
    }

    public void Douse(Frog by)
    {
        if (!Lit) return;
        state = 3; t = 0f; smoulderT = 7f;
        Vector3 p = transform.position + Vector3.up * 0.4f;
        Sfx.PlayAt(GardenAudio.Hiss, p, 0.8f, 40f, Random.Range(0.95f, 1.05f));
        for (int i = 0; i < 18; i++)
        {
            Vector3 d = Random.insideUnitSphere; d.y = Mathf.Abs(d.y) + 0.4f;
            FX.Puff(p + d * 0.3f, d * 1.4f + Vector3.up * 1.6f, Random.Range(0.7f, 1.3f), Random.Range(1.2f, 2.2f), new Color(0.86f, 0.86f, 0.88f, 0.75f));
        }
        if (by != null && by.human) by.Toast("Fire's out - sssss.", 2f);
        Debug.Log("Campfire: doused" + (by != null ? " by " + by.nick : ""));
    }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        t += dt;
        float target = 0f;
        if (state == 1) { target = t < 0.35f ? 0f : Mathf.Clamp01((t - 0.35f) / 3f); if (t > 3.4f) state = 2; }
        else if (state == 2) target = 1f;
        k = state == 3 ? Mathf.MoveTowards(k, 0f, dt * 1.4f) : state == 1 ? Mathf.Max(k, target * target) : Mathf.MoveTowards(k, target, dt);
        if (state == 3 && k <= 0f) state = 0;
        float near = Sfx.Near(transform.position);
        bool watched = near < 120f || near > 1e8f;
        Vector3 p0 = transform.position;
        float time = Time.time;
        // flames
        for (int i = 0; i < tongues.Length; i++)
        {
            if (k <= 0.001f) { if (tongues[i].localScale != Vector3.zero) tongues[i].localScale = Vector3.zero; continue; }
            float n = Mathf.PerlinNoise(time * (3.1f + i * 0.7f), i * 7.3f);
            float n2 = Mathf.PerlinNoise(i * 3.1f, time * 4.3f);
            float h = (0.75f + 0.5f * n) * Mathf.Lerp(0.35f, 1f, k);
            float w = (0.85f + 0.3f * n2) * Mathf.Lerp(0.5f, 1f, k);
            Vector3 b = tongueBase[i];
            tongues[i].localScale = new Vector3(b.x * w, b.y * h * k, b.z * w);
            tongues[i].localRotation = Quaternion.Euler((n2 - 0.5f) * 16f, time * 40f * (i % 2 == 0 ? 1f : -1f), (n - 0.5f) * 16f);
        }
        // light
        if (k > 0.01f && watched)
        {
            if (!fireLight.enabled) fireLight.enabled = true;
            float fl = Mathf.PerlinNoise(time * 7f, 0.3f) * 0.6f + Mathf.PerlinNoise(time * 17f, 4.1f) * 0.25f;
            fireLight.intensity = k * (1.5f + fl * 0.9f) * (Look.Mobile ? 1.15f : 1f);
            fireLight.transform.localPosition = new Vector3((Mathf.PerlinNoise(time * 3f, 1f) - 0.5f) * 0.15f, 0.85f + fl * 0.15f, (Mathf.PerlinNoise(1f, time * 3f) - 0.5f) * 0.15f);
        }
        else if (fireLight.enabled) fireLight.enabled = false;
        // particles: flame licks, embers, smoke (budgeted, only when someone is near)
        if (watched && k > 0.01f)
        {
            emitAcc += dt * k * (Look.Mobile ? 18f : 34f);
            while (emitAcc >= 1f)
            {
                emitAcc -= 1f;
                Vector2 u = Random.insideUnitCircle * 0.32f;
                Emit(p0 + new Vector3(u.x, 0.2f, u.y), new Vector3(u.x * 0.4f, Random.Range(1.2f, 2.2f) * Mathf.Lerp(0.5f, 1f, k), u.y * 0.4f), Random.Range(0.35f, 0.6f) * Mathf.Lerp(0.6f, 1f, k), Random.Range(0.35f, 0.65f), Color.white);
            }
            emberAcc += dt * k * (Look.Mobile ? 3f : 6f);
            while (emberAcc >= 1f)
            {
                emberAcc -= 1f;
                Emit(p0 + new Vector3(Random.Range(-0.2f, 0.2f), 0.5f, Random.Range(-0.2f, 0.2f)), new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(1.5f, 3.2f), Random.Range(-0.5f, 0.5f)), Random.Range(0.05f, 0.09f), Random.Range(1.6f, 2.8f), new Color(1f, 0.85f, 0.4f));
            }
        }
        if (watched && (k > 0.05f || smoulderT > 0f))
        {
            smokeAcc += dt * (k > 0.05f ? 2.4f * k : 1.6f * Mathf.Clamp01(smoulderT / 7f));
            while (smokeAcc >= 1f)
            {
                smokeAcc -= 1f;
                float g = Random.Range(0.32f, 0.45f);
                FX.Puff(p0 + new Vector3(Random.Range(-0.15f, 0.15f), 1.3f + k * 0.4f, Random.Range(-0.15f, 0.15f)), new Vector3(Random.Range(-0.25f, 0.25f) + 0.15f, Random.Range(0.8f, 1.3f), Random.Range(-0.25f, 0.25f)), Random.Range(0.6f, 1.1f), Random.Range(2.2f, 3.4f), new Color(g, g, g, 0.35f));
            }
        }
        if (state != 1 && state != 2) smoulderT -= dt;
        // crackle loop by distance
        if (crackle != null)
        {
            float a = near > 1e8f ? 0.5f : Mathf.Clamp01(1f - near / 30f);
            float vol = k * a * a * 0.55f;
            if (vol > 0.005f) { if (!crackle.isPlaying) crackle.Play(); crackle.volume = vol; }
            else if (crackle.isPlaying) { crackle.volume = 0f; crackle.Stop(); }
        }
    }
}
