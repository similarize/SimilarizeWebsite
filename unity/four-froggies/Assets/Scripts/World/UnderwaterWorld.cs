using System.Collections.Generic;
using UnityEngine;

// Underwater world under the pond. Board the submarine at the pond dock and it dives straight down into a
// separate hidden root at Worlds.UnderO: coral reef, kelp forest, a shipwreck, a rock cave, fish schools,
// turtles, rays, jellies and a whale (the 3D version's sea life). Goal loop: find all the pearls.
// A in the sub = swim out in scuba gear (3D swim: stick moves, RT / A rises, LT / B sinks, gentle buoyancy,
// bubbles); A next to the sub = climb back in; surface the sub (or swim to the top) + keep rising = ranch.
public class UnderwaterWorld : MonoBehaviour
{
    public static UnderwaterWorld I;
    Transform root;
    bool built;
    public Submarine sub;
    float best = -1f, roundStart = -1f;
    const string Group = "pearls";

    class School { public Vector3 c, vel; public float t, seed; public readonly List<Transform> fish = new List<Transform>(); public readonly List<Vector3> off = new List<Vector3>(); }
    readonly List<School> schools = new List<School>();
    class Swimmer { public Transform t; public float a, r, sp, y; public Vector3 c; public bool bob; }
    readonly List<Swimmer> swimmers = new List<Swimmer>();
    readonly List<Transform> kelp = new List<Transform>();

    public static Vector3 L(float x, float y, float z) { return Worlds.UnderO + new Vector3(x, y, z); }

    // seabed height (local)
    public static float SeaY(float x, float z)
    {
        float y = -22f + (Mathf.PerlinNoise(x * 0.03f + 3f, z * 0.03f + 7f) - 0.5f) * 8f + (Mathf.PerlinNoise(x * 0.11f, z * 0.11f) - 0.5f) * 1.6f;
        float reef = Mathf.Clamp01(1f - new Vector2(x, z).magnitude / 38f);
        y += reef * reef * 7f;                                  // shallow reef mound in the middle
        float trench = Mathf.Clamp01(1f - Mathf.Abs(z + 70f) / 14f);
        y -= trench * 9f;                                        // a deep trench to the south
        float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
        y += Mathf.SmoothStep(0f, 1f, (edge - 92f) / 16f) * 24f;  // walls at the edge
        return y;
    }

    public static void Create()
    {
        var go = new GameObject("UnderwaterWorld");
        I = go.AddComponent<UnderwaterWorld>();
        Pickups.Listen((it, f) => { if (it.group == Group) I.OnPearl(f); });
    }

    public Vector3 DockSpot { get { Vector2 c = Layout.PondC, r = Layout.PondR; return new Vector3(c.x - r.x + 2f, 0.7f, c.y); } }

    // ranch side: board the parked sub at the dock -> dive
    public void Dive(Frog f)
    {
        if (!built) Build();
        if (sub.driver == null)
        {
            f.SendTo(WorldId.Underwater, sub.transform.position + Vector3.up * 2f, sub.transform.eulerAngles.y);
            f.EnterVehicle(sub);
            f.Toast("Diving! L-stick drive, RT up, LT down, A = swim out in scuba gear", 4f);
        }
        else
        {
            f.SendTo(WorldId.Underwater, sub.transform.position + sub.transform.right * 3.5f, sub.transform.eulerAngles.y);
            f.Toast(sub.driver.nick + " has the sub - you're in scuba gear. Find the pearls!", 4f);
        }
        if (roundStart < 0f) roundStart = Time.time;
        FX.Splash(DockSpot, 20);
        Sfx.Play(Sfx.Splash, 1f, 0.8f);
    }

    public void Surface(Frog f)
    {
        if (f.vehicle != null) f.ExitVehicle();
        Vector3 d = DockSpot + new Vector3((f.id - 1.5f) * 1.6f, 0f, 0f);
        f.SendTo(WorldId.Ranch, d, 270f);
        f.Toast("Back at the pond dock", 2.5f);
        Sfx.Play(Sfx.Splash, 0.9f, 1.2f);
    }

    void OnPearl(Frog f)
    {
        int left = Pickups.Remaining(Group), total = Pickups.Total(Group);
        if (left == 0)
        {
            float t = Time.time - roundStart;
            bool rec = best < 0f || t < best;
            if (rec) best = t;
            Toast("OCEAN EXPLORER! All " + total + " pearls in " + Mathf.RoundToInt(t) + " s" + (rec ? " - new best!" : "") + "  They'll reappear soon.", 5f);
            Sfx.Play(Sfx.Win, 1f);
            Invoke("NewRound", 12f);
        }
        else f.Toast("Pearl! " + (total - left) + " / " + total, 1.8f);
    }

    void NewRound() { Pickups.Reset(Group); roundStart = Time.time; Toast("The pearls are back - go again!", 3f); }

    void Toast(string s, float t)
    {
        if (Game.I == null) return;
        foreach (Frog f in Game.I.frogs) if (f != null && f.human && f.world == WorldId.Underwater) f.Toast(s, t);
    }

    // ---------------- build ----------------
    GameObject P(PrimitiveType t, Vector3 lp, Vector3 s, Color c, bool col = false, Vector3 euler = default(Vector3))
    {
        return Mats.Prim(t, root, L(lp.x, lp.y, lp.z), s, euler, Mats.Lit(c), col);
    }

    void Build()
    {
        built = true;
        root = new GameObject("Underwater").transform;
        Seabed();
        Surface();
        var rnd = new System.Random(8);
        Reef(rnd);
        Kelp(rnd);
        Wreck();
        Cave();
        Rocks(rnd);
        foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // graphics overhaul stage B: animated caustics on every lit surface (FF/Underwater), merged afterwards
        int conv = 0;
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) { var nm = Mats.Underwater(mats[i], Worlds.UnderO.y); if (nm != mats[i]) { mats[i] = nm; conv++; } }
            r.sharedMaterials = mats;
        }
        Debug.Log("Underwater: caustics on " + conv + " surfaces");
        MeshMerge.Merge(root, false);
        Motes();
        Shafts();
        Life(rnd);
        Pearls(rnd);
        sub = Submarine.Build(L(0f, -5f, -62f), 0f);
        // yaw 180: reads the right way round from the sub's start (it was mirrored)
        Ranch.Sign(L(0f, -2.5f, -55f), 180f, "UNDERWATER\n<size=22>find all the pearls - reef, kelp, shipwreck, cave</size>", new Color(0.05f, 0.3f, 0.55f), 7f, 2f);
    }

    void Seabed()
    {
        const int n = 96; const float size = 220f;
        var v = new Vector3[(n + 1) * (n + 1)];
        var uv = new Vector2[v.Length];
        for (int z = 0; z <= n; z++)
            for (int x = 0; x <= n; x++)
            {
                float lx = -size * 0.5f + x * size / n, lz = -size * 0.5f + z * size / n;
                v[z * (n + 1) + x] = L(lx, SeaY(lx, lz), lz);
                uv[z * (n + 1) + x] = new Vector2(lx / 6f, lz / 6f);
            }
        var tri = new int[n * n * 6];
        int k = 0;
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int a = z * (n + 1) + x, b = a + 1, c = a + n + 1, d = c + 1;
                tri[k++] = a; tri[k++] = c; tri[k++] = b; tri[k++] = b; tri[k++] = c; tri[k++] = d;
            }
        var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.vertices = v; mesh.uv = uv; mesh.triangles = tri;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var go = new GameObject("Seabed");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        // sandy texture: Poly Haven CC0 aerial sand (Resources/LB/seabed), generated noise if missing
        var sb = Resources.Load<Texture2D>("LB/seabed");
        if (sb != null)
        {
            var sm = Mats.Tex(sb, 0.05f); sm.color = new Color(0.95f, 0.92f, 0.82f);
            var smr = go.AddComponent<MeshRenderer>(); smr.sharedMaterial = sm;
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.transform.SetParent(root, true);
            return;
        }
        var tex = new Texture2D(64, 64, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
        var px = new Color32[64 * 64];
        var r = new System.Random(4);
        for (int i = 0; i < px.Length; i++) { float q = 0.8f + (float)r.NextDouble() * 0.25f; px[i] = new Color(0.78f * q, 0.72f * q, 0.55f * q); }
        tex.SetPixels32(px); tex.Apply(true);
        var m = new Material(Mats.Lit(Color.white)) { mainTexture = tex };
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = m;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        go.transform.SetParent(root, true);
    }

    void Surface()
    {
        var m = new Material(Mats.Water);
        m.color = new Color(0.55f, 0.85f, 0.95f, 0.55f);
        var s = Mats.Prim(PrimitiveType.Cube, root, L(0f, 0.05f, 0f), new Vector3(240f, 0.1f, 240f), m, false);
        s.name = "SurfaceFromBelow";
    }

    void Reef(System.Random r)
    {
        Color[] cols = { new Color(1f, 0.45f, 0.5f), new Color(1f, 0.6f, 0.2f), new Color(0.6f, 0.35f, 0.9f), new Color(0.95f, 0.85f, 0.3f), new Color(0.3f, 0.85f, 0.75f), new Color(0.95f, 0.3f, 0.3f), new Color(0.4f, 0.6f, 1f) };
        for (int i = 0; i < 170; i++)
        {
            float a = (float)r.NextDouble() * 6.283f, d = Mathf.Sqrt((float)r.NextDouble()) * 40f;
            float x = Mathf.Cos(a) * d, z = Mathf.Sin(a) * d, y = SeaY(x, z);
            Color c = cols[r.Next(cols.Length)];
            float s = 0.6f + (float)r.NextDouble() * 1.8f;
            switch (i % 5)
            {
                case 0: P(PrimitiveType.Sphere, new Vector3(x, y + s * 0.3f, z), new Vector3(s * 1.4f, s * 0.8f, s * 1.3f), c, true); break;      // brain coral
                case 1:   // branching coral
                    for (int b = 0; b < 4; b++) P(PrimitiveType.Cylinder, new Vector3(x + (b - 1.5f) * 0.3f * s, y + s * 0.6f, z + (b % 2) * 0.3f), new Vector3(0.22f * s, s * 0.7f, 0.22f * s), c, false, new Vector3((b - 1.5f) * 18f, 0f, (b % 2 - 0.5f) * 30f));
                    break;
                case 2: P(PrimitiveType.Cube, new Vector3(x, y + s, z), new Vector3(s * 1.8f, s * 2f, 0.08f), c, false, new Vector3(0f, a * 57f, 0f)); break;   // sea fan
                case 3:   // tube sponge cluster
                    for (int b = 0; b < 3; b++) P(PrimitiveType.Cylinder, new Vector3(x + b * 0.5f, y + s * 0.5f, z + (b % 2) * 0.4f), new Vector3(0.45f, s * 0.5f + b * 0.2f, 0.45f), c);
                    break;
                default:  // anemone
                    P(PrimitiveType.Sphere, new Vector3(x, y + 0.25f, z), new Vector3(0.9f, 0.5f, 0.9f), c);
                    for (int b = 0; b < 6; b++) P(PrimitiveType.Cylinder, new Vector3(x + Mathf.Cos(b) * 0.3f, y + 0.6f, z + Mathf.Sin(b) * 0.3f), new Vector3(0.08f, 0.3f, 0.08f), Color.Lerp(c, Color.white, 0.4f));
                    break;
            }
        }
        // starfish + shells on the sand
        for (int i = 0; i < 40; i++)
        {
            float x = ((float)r.NextDouble() - 0.5f) * 170f, z = ((float)r.NextDouble() - 0.5f) * 170f, y = SeaY(x, z);
            Color c = i % 2 == 0 ? new Color(1f, 0.45f, 0.2f) : new Color(0.95f, 0.85f, 0.7f);
            for (int k = 0; k < 5; k++) P(PrimitiveType.Cube, new Vector3(x, y + 0.05f, z) + Quaternion.Euler(0f, k * 72f, 0f) * Vector3.forward * 0.18f, new Vector3(0.12f, 0.06f, 0.36f), c, false, new Vector3(0f, k * 72f, 0f));
        }
    }

    void Kelp(System.Random r)
    {
        var km = new Material(Mats.Lit(new Color(0.2f, 0.5f, 0.2f)));
        for (int i = 0; i < 46; i++)
        {
            float x = -40f + ((float)r.NextDouble() - 0.5f) * 36f, z = -35f + ((float)r.NextDouble() - 0.5f) * 36f, y = SeaY(x, z);
            float h = 6f + (float)r.NextDouble() * 10f;
            var k = new GameObject("Kelp").transform;
            k.position = L(x, y, z);
            for (int s = 0; s < 5; s++)
            {
                var seg = Mats.Prim(PrimitiveType.Cube, k, new Vector3(0f, s * h / 5f + h / 10f, 0f), new Vector3(0.5f - s * 0.05f, h / 5f, 0.08f), km);
                seg.transform.localRotation = Quaternion.Euler(0f, s * 25f, 0f);
            }
            Mats.NoShadows(k.gameObject);
            k.SetParent(transform, true);
            kelp.Add(k);
        }
    }

    void Wreck()
    {
        Vector3 c = new Vector3(55f, 0f, 40f);
        float y = SeaY(c.x, c.z);
        Color wood = new Color(0.36f, 0.26f, 0.16f), dark = new Color(0.22f, 0.16f, 0.1f);
        Vector3 e = new Vector3(0f, 25f, 14f);   // tilted on its side a bit
        var hull = new GameObject("Wreck").transform;
        hull.SetParent(root, false);
        hull.position = L(c.x, y + 2f, c.z);
        hull.rotation = Quaternion.Euler(e);
        System.Action<Vector3, Vector3, Color, bool> B = (p, s, col, collide) => Mats.Prim(PrimitiveType.Cube, hull, p, s, Mats.Lit(col), collide);
        B(new Vector3(0f, -1.2f, 0f), new Vector3(5f, 0.6f, 20f), dark, true);           // keel / floor
        B(new Vector3(-2.6f, 0.6f, 0f), new Vector3(0.4f, 3.6f, 20f), wood, true);        // port side
        B(new Vector3(2.6f, 0.6f, -4f), new Vector3(0.4f, 3.6f, 12f), wood, true);        // starboard (broken: a hole to swim in)
        B(new Vector3(2.6f, -0.4f, 7f), new Vector3(0.4f, 1.6f, 6f), wood, true);
        B(new Vector3(0f, 0.6f, 10.2f), new Vector3(5.2f, 3.6f, 0.4f), wood, true);       // bow
        B(new Vector3(0f, 0.6f, -10.2f), new Vector3(5.2f, 3.6f, 0.4f), wood, true);      // stern
        B(new Vector3(0f, 2.5f, -6f), new Vector3(5f, 0.3f, 8f), wood, true);             // half deck
        B(new Vector3(0f, 3.8f, -7f), new Vector3(3f, 2.4f, 3.5f), dark, true);           // cabin
        Mats.Prim(PrimitiveType.Cylinder, hull, new Vector3(0f, 6f, 1f), new Vector3(0.4f, 4f, 0.4f), Mats.Lit(dark), true);   // broken mast
        Mats.Prim(PrimitiveType.Cylinder, hull, new Vector3(4f, -0.6f, 3f), new Vector3(0.35f, 3f, 0.35f), Vector3.zero + new Vector3(0f, 0f, 80f), Mats.Lit(dark), false);
        // treasure chest inside
        B(new Vector3(0f, -0.4f, 4f), new Vector3(1.4f, 0.9f, 0.9f), new Color(0.55f, 0.35f, 0.15f), true);
        B(new Vector3(0f, 0.2f, 4f), new Vector3(1.45f, 0.3f, 0.95f), new Color(0.85f, 0.7f, 0.2f), false);
        Ranch.Sign(L(c.x - 8f, y + 6f, c.z - 12f), 220f, "SHIPWRECK", new Color(0.35f, 0.25f, 0.12f), 4f, 1f);
    }

    void Cave()
    {
        // rock tunnel along x at (-58, z 30): rings of boulders, open at both ends, glowing crystals inside
        Color rock = new Color(0.32f, 0.32f, 0.35f);
        float cz = 30f;
        for (int k = 0; k <= 12; k++)
        {
            float x = -72f + k * 2.4f;
            float y0 = SeaY(x, cz);
            for (int j = 0; j < 9; j++)
            {
                float a = Mathf.PI * (j / 8f);   // half ring: left wall, roof, right wall
                Vector3 p = new Vector3(x, y0 + Mathf.Sin(a) * 6.5f, cz + Mathf.Cos(a) * 6.5f);
                P(PrimitiveType.Sphere, p, new Vector3(3.2f, 3f, 3.2f), Color.Lerp(rock, Color.black, (k + j) % 3 * 0.12f), true);
            }
            if (k % 3 == 1)
            {
                Color glow = k % 2 == 0 ? new Color(0.3f, 1f, 0.9f) : new Color(0.75f, 0.4f, 1f);
                Mats.Prim(PrimitiveType.Cube, root, L(x, y0 + 0.6f, cz + 3.5f), new Vector3(0.4f, 1.2f, 0.4f), new Vector3(10f, 30f, 15f), Mats.Unlit(glow), false);
                Mats.Prim(PrimitiveType.Cube, root, L(x + 0.4f, y0 + 0.4f, cz - 3.6f), new Vector3(0.3f, 0.8f, 0.3f), new Vector3(-12f, 10f, -18f), Mats.Unlit(glow), false);
            }
        }
        Ranch.Sign(L(-74f, SeaY(-74f, cz) + 8.5f, cz), 270f, "CAVE", new Color(0.2f, 0.2f, 0.3f), 3f, 1f);
    }

    void Rocks(System.Random r)
    {
        for (int i = 0; i < 40; i++)
        {
            float x = ((float)r.NextDouble() - 0.5f) * 180f, z = ((float)r.NextDouble() - 0.5f) * 180f;
            if (new Vector2(x, z).magnitude < 30f) continue;
            float s = 1.2f + (float)r.NextDouble() * 3f;
            P(PrimitiveType.Sphere, new Vector3(x, SeaY(x, z) + s * 0.2f, z), new Vector3(s * 1.6f, s, s * 1.3f), new Color(0.35f, 0.36f, 0.38f), true, new Vector3(0f, (float)r.NextDouble() * 180f, 0f));
        }
    }

    // drifting marine snow + tiny bubbles around the reef (one particle system, fewer on phones)
    void Motes()
    {
        var go = new GameObject("Motes");
        go.transform.SetParent(root, false);
        go.transform.position = L(0f, -14f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 18f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.95f, 1f, 0.55f), new Color(1f, 1f, 0.9f, 0.25f));
        main.maxParticles = Look.Mobile ? 500 : 1400;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.002f;
        main.prewarm = true;
        var em = ps.emission; em.rateOverTime = Look.Mobile ? 35f : 95f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(170f, 26f, 170f);
        var nz = ps.noise; nz.enabled = true; nz.strength = 0.15f; nz.frequency = 0.25f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var pr = go.GetComponent<ParticleSystemRenderer>();
        var m = new Material(Mats.Fx);
        m.mainTexture = SoftDot();
        pr.sharedMaterial = m;
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
    }

    static Texture2D SoftDot()
    {
        const int n = 32;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = new Vector2(x - n * 0.5f + 0.5f, y - n * 0.5f + 0.5f).magnitude / (n * 0.5f);
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d));
            }
        t.SetPixels(px); t.Apply(true);
        return t;
    }

    void Shafts()
    {
        var m = new Material(Mats.Fx);
        m.color = new Color(0.75f, 0.95f, 1f, 0.07f);
        for (int i = 0; i < 9; i++)
        {
            var s = Mats.Prim(PrimitiveType.Cube, root, L((i - 4) * 18f, -12f, (i % 2 == 0 ? 14f : -12f)), new Vector3(2.2f, 26f, 2.2f), new Vector3(0f, i * 20f, (i - 4) * 2.5f), m, false);
            s.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    void Life(System.Random r)
    {
        Color[] fc = { new Color(1f, 0.8f, 0.1f), new Color(0.1f, 0.65f, 1f), new Color(1f, 0.45f, 0.1f), new Color(0.6f, 0.95f, 0.6f), new Color(1f, 0.5f, 0.6f), new Color(0.7f, 0.7f, 1f) };
        for (int s = 0; s < 6; s++)
        {
            var sc = new School { seed = s * 13.7f, c = L(((float)r.NextDouble() - 0.5f) * 80f, -10f, ((float)r.NextDouble() - 0.5f) * 80f) };
            Material m = Mats.Shiny(fc[s]);
            for (int i = 0; i < 16; i++)
            {
                float sz = 0.25f + (float)r.NextDouble() * 0.15f;
                var g = Mats.Prim(PrimitiveType.Sphere, transform, sc.c, new Vector3(sz * 0.45f, sz * 0.6f, sz * 1.4f), m);
                g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sc.fish.Add(g.transform);
                sc.off.Add(new Vector3(((float)r.NextDouble() - 0.5f) * 5f, ((float)r.NextDouble() - 0.5f) * 2.4f, ((float)r.NextDouble() - 0.5f) * 5f));
            }
            schools.Add(sc);
        }
        // turtles
        for (int i = 0; i < 4; i++)
        {
            var t = new GameObject("Turtle").transform; t.SetParent(transform, false);
            Mats.Prim(PrimitiveType.Sphere, t, Vector3.zero, new Vector3(1.4f, 0.55f, 1.7f), Mats.Lit(new Color(0.25f, 0.45f, 0.25f)));
            Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 0f, 1f), new Vector3(0.45f, 0.4f, 0.5f), Mats.Lit(new Color(0.55f, 0.6f, 0.4f)));
            for (int f = 0; f < 4; f++) Mats.Prim(PrimitiveType.Cube, t, new Vector3(f % 2 == 0 ? -0.8f : 0.8f, -0.05f, f < 2 ? 0.5f : -0.6f), new Vector3(0.7f, 0.08f, 0.35f), Mats.Lit(new Color(0.55f, 0.6f, 0.4f)));
            Mats.NoShadows(t.gameObject);
            swimmers.Add(new Swimmer { t = t, a = i * 1.6f, r = 20f + i * 8f, sp = 0.12f, y = -9f - i, c = L(0f, 0f, 0f) });
        }
        // rays
        for (int i = 0; i < 3; i++)
        {
            var t = new GameObject("Ray").transform; t.SetParent(transform, false);
            Mats.Prim(PrimitiveType.Sphere, t, Vector3.zero, new Vector3(3f, 0.25f, 2f), Mats.Lit(new Color(0.35f, 0.38f, 0.45f)));
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0f, -1.8f), new Vector3(0.08f, 0.08f, 2f), Mats.Lit(new Color(0.3f, 0.3f, 0.35f)));
            Mats.NoShadows(t.gameObject);
            swimmers.Add(new Swimmer { t = t, a = i * 2.1f, r = 30f + i * 10f, sp = 0.16f, y = -7f - i * 2f, c = L(10f, 0f, -10f) });
        }
        // jellies
        var jm = new Material(Mats.Glass); jm.color = new Color(1f, 0.6f, 0.85f, 0.45f);
        for (int i = 0; i < 10; i++)
        {
            var t = new GameObject("Jelly").transform; t.SetParent(transform, false);
            Mats.Prim(PrimitiveType.Sphere, t, Vector3.zero, new Vector3(0.9f, 0.5f, 0.9f), jm);
            for (int k = 0; k < 5; k++) Mats.Prim(PrimitiveType.Cube, t, new Vector3(Mathf.Cos(k) * 0.2f, -0.55f, Mathf.Sin(k) * 0.2f), new Vector3(0.03f, 0.9f, 0.03f), jm);
            swimmers.Add(new Swimmer { t = t, a = i * 0.7f, r = 6f + i * 4f, sp = 0.05f, y = -4f - (i % 4) * 2.5f, c = L(-20f + i * 5f, 0f, 20f - i * 3f), bob = true });
        }
        // a whale far out, slowly circling
        {
            var t = new GameObject("Whale").transform; t.SetParent(transform, false);
            Mats.Prim(PrimitiveType.Capsule, t, Vector3.zero, new Vector3(4f, 7f, 3.6f), new Vector3(90f, 0f, 0f), Mats.Lit(new Color(0.25f, 0.32f, 0.45f)));
            Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0f, -0.9f, 1f), new Vector3(3.4f, 1.6f, 9f), Mats.Lit(new Color(0.85f, 0.85f, 0.88f)));
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0f, -7.4f), new Vector3(5f, 0.3f, 1.6f), Mats.Lit(new Color(0.25f, 0.32f, 0.45f)));
            for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, t, new Vector3(2.4f * s, -1f, 2f), new Vector3(2.8f, 0.2f, 1.2f), new Vector3(0f, 20f * s, -25f * s), Mats.Lit(new Color(0.25f, 0.32f, 0.45f)));
            Mats.NoShadows(t.gameObject);
            swimmers.Add(new Swimmer { t = t, a = 0f, r = 75f, sp = 0.035f, y = -12f, c = L(0f, 0f, 0f) });
        }
    }

    void Pearls(System.Random r)
    {
        var spots = new List<Vector3>();
        for (int i = 0; i < 8; i++) { float a = i * 0.785f; float x = Mathf.Cos(a) * (12f + i * 3f), z = Mathf.Sin(a) * (12f + i * 3f); spots.Add(new Vector3(x, SeaY(x, z) + 1.5f, z)); }
        for (int i = 0; i < 5; i++) { float x = -40f + (i - 2) * 6f, z = -35f + (i % 2) * 8f; spots.Add(new Vector3(x, SeaY(x, z) + 3f + i, z)); }
        spots.Add(new Vector3(55f, SeaY(55f, 44f) + 2.2f, 44f));
        spots.Add(new Vector3(55f, SeaY(55f, 36f) + 4f, 36f));
        spots.Add(new Vector3(50f, SeaY(50f, 30f) + 6f, 30f));
        spots.Add(new Vector3(62f, SeaY(62f, 46f) + 3f, 46f));
        for (int i = 0; i < 5; i++) { float x = -70f + i * 5f; spots.Add(new Vector3(x, SeaY(x, 30f) + 1.6f, 30f)); }
        spots.Add(new Vector3(0f, SeaY(0f, -70f) + 2f, -70f));
        spots.Add(new Vector3(30f, SeaY(30f, -70f) + 2f, -70f));
        var holder = new GameObject("Pearls").transform;
        var pm = Mats.Shiny(new Color(0.97f, 0.95f, 1f));
        var gm = Mats.Unlit(new Color(1f, 0.85f, 0.3f));
        for (int i = 0; i < spots.Count; i++)
        {
            Vector3 s = spots[i];
            var g = Mats.Prim(PrimitiveType.Sphere, holder, L(s.x, s.y, s.z), Vector3.one * (i % 6 == 0 ? 0.7f : 0.45f), i % 6 == 0 ? gm : pm);
            // open clam shell under each pearl
            var shell = Mats.Prim(PrimitiveType.Sphere, g.transform, new Vector3(0f, -0.8f, 0f), new Vector3(2.4f, 0.6f, 2f), Mats.Lit(new Color(0.75f, 0.55f, 0.65f)));
            Pickups.Add(g.transform, WorldId.Underwater, Group, 1.8f);
        }
    }

    // ---------------- update ----------------
    void Update()
    {
        if (!built) return;
        bool anyone = false;
        if (Game.I != null) foreach (Frog f in Game.I.frogs) if (f != null && f.world == WorldId.Underwater) anyone = true;
        if (root.gameObject.activeSelf != anyone)
        {
            root.gameObject.SetActive(anyone);
            for (int i = 0; i < transform.childCount; i++) transform.GetChild(i).gameObject.SetActive(anyone);
        }
        if (!anyone) return;
        float dt = Mathf.Min(Time.deltaTime, 0.05f), t = Time.time;
        foreach (School s in schools)
        {
            s.t += dt;
            Vector3 home = L(Mathf.Sin(s.t * 0.05f + s.seed) * 60f, -9f + Mathf.Sin(s.t * 0.13f + s.seed) * 4f, Mathf.Sin(s.t * 0.037f + s.seed * 2f) * 60f);
            Vector3 want = (home - s.c).normalized * 3.2f;
            // scatter from frogs and the sub
            foreach (Frog f in Game.I.frogs)
                if (f != null && f.world == WorldId.Underwater) { Vector3 d = s.c - f.FocusPoint; if (d.magnitude < 7f) want += d.normalized * 8f; }
            s.vel = Vector3.MoveTowards(s.vel, want, dt * 4f);
            s.c += s.vel * dt;
            float floor = Worlds.UnderO.y + SeaY(s.c.x - Worlds.UnderO.x, s.c.z - Worlds.UnderO.z) + 2.5f;
            if (s.c.y < floor) s.c.y = floor;
            Quaternion rot = s.vel.sqrMagnitude > 0.01f ? Quaternion.LookRotation(s.vel) : Quaternion.identity;
            for (int i = 0; i < s.fish.Count; i++)
            {
                Vector3 o = s.off[i] + new Vector3(Mathf.Sin(t * 1.3f + i), Mathf.Sin(t * 0.9f + i * 2f) * 0.4f, Mathf.Cos(t * 1.1f + i)) * 0.5f;
                s.fish[i].position = s.c + rot * o;
                s.fish[i].rotation = rot * Quaternion.Euler(0f, Mathf.Sin(t * 8f + i) * 12f, 0f);
            }
        }
        foreach (Swimmer w in swimmers)
        {
            w.a += w.sp * dt;
            Vector3 p = w.c + new Vector3(Mathf.Cos(w.a) * w.r, w.y + (w.bob ? Mathf.Sin(t * 0.8f + w.r) * 1.5f : Mathf.Sin(t * 0.3f + w.r) * 0.6f), Mathf.Sin(w.a) * w.r);
            Vector3 d = p - w.t.position;
            w.t.position = p;
            if (!w.bob && d.sqrMagnitude > 1e-6f) w.t.rotation = Quaternion.LookRotation(d) * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.5f + w.r) * 8f);
            if (w.bob) w.t.localScale = new Vector3(1f + Mathf.Sin(t * 3f + w.r) * 0.12f, 1f - Mathf.Sin(t * 3f + w.r) * 0.12f, 1f + Mathf.Sin(t * 3f + w.r) * 0.12f);
        }
        for (int i = 0; i < kelp.Count; i++) kelp[i].rotation = Quaternion.Euler(Mathf.Sin(t * 0.7f + i) * 6f, i * 37f, Mathf.Cos(t * 0.5f + i) * 6f);
    }
}

// The submarine: hovers neutrally buoyant, left stick drives + turns, RT rises, LT sinks, A = swim out.
// At the surface keep rising to pop up at the pond dock (back to the ranch).
public class Submarine : Vehicle
{
    float yaw, pitchVis, surfaceT;
    Transform prop;

    public override string HelpLine { get { return "L-stick drive + turn | RT / Space up | LT / Shift down | A swim out (scuba) | surface + keep rising = ranch"; } }

    public static Submarine Build(Vector3 pos, float yawDeg)
    {
        var go = new GameObject("Submarine");
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);
        var v = go.AddComponent<Submarine>();
        v.Title = "Submarine";
        v.EnterVerb = "climb into the Submarine";
        v.flyer = true;
        v.engineKind = 4;
        v.SetupBodyPublic(3000f, new Vector3(0f, 0.2f, 0f), new Vector3(2.6f, 2.6f, 7f), Vector3.zero);
        v.rb.useGravity = false; v.rb.freezeRotation = true; v.rb.drag = 0f;
        v.camDistance = 13f; v.camHeight = 3f;
        v.yaw = yawDeg;
        Transform t = go.transform;
        Material hull = Mats.Shiny(Mats.Hex("#00a5ff")), dark = Mats.Lit(new Color(0.12f, 0.13f, 0.15f));
        Mats.Prim(PrimitiveType.Capsule, t, Vector3.zero, new Vector3(2.6f, 3.6f, 2.6f), new Vector3(90f, 0f, 0f), hull);
        Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 1.5f, -0.6f), new Vector3(1.2f, 1.1f, 2.2f), hull);
        Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 1.1f, 1.5f), new Vector3(1.5f, 1.2f, 1.8f), Mats.Glass);
        Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(0.3f, 2.4f, -0.9f), new Vector3(0.12f, 0.5f, 0.12f), dark);
        for (int s = -1; s <= 1; s += 2)
        {
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(1.6f * s, 0f, -2.6f), new Vector3(1.2f, 0.12f, 0.8f), dark);
            Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0.75f * s, -0.3f, 3.35f), Vector3.one * 0.35f, Mats.Unlit(new Color(1f, 0.97f, 0.8f)));
            for (int k = 0; k < 3; k++) Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(1.28f * s, 0.25f, -1f + k * 1.1f), new Vector3(0.45f, 0.03f, 0.45f), new Vector3(0f, 0f, 90f), Mats.Steel(new Color(0.7f, 0.7f, 0.72f)));
        }
        Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.9f, -3.2f), new Vector3(0.12f, 1.2f, 0.8f), dark);
        v.prop = Mats.Node(t, "Prop", new Vector3(0f, 0f, -3.75f));
        Mats.Prim(PrimitiveType.Cube, v.prop, Vector3.zero, new Vector3(1.4f, 0.12f, 0.05f), Mats.Steel(new Color(0.8f, 0.7f, 0.3f)));
        Mats.Prim(PrimitiveType.Cube, v.prop, Vector3.zero, new Vector3(0.12f, 1.4f, 0.05f), Mats.Steel(new Color(0.8f, 0.7f, 0.3f)));
        v.seat = Mats.Node(t, "Seat", new Vector3(0f, 0.55f, 1.4f));
        v.seatScale = 0.8f;
        Mats.SetLayer(go, VehicleLayer);
        Mats.NoShadows(go);
        return v;
    }

    protected override bool OutOfWorld(Vector3 p)
    {
        Vector3 l = p - Worlds.UnderO;
        return Mathf.Abs(l.x) > 130f || Mathf.Abs(l.z) > 130f || l.y < -60f || l.y > 20f;
    }

    public override void OnEnter() { base.OnEnter(); yaw = transform.eulerAngles.y; surfaceT = 0f; }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        float dt = Time.fixedDeltaTime;
        bool on = driver != null;
        Vector3 v = rb.velocity;
        Vector3 local = transform.position - Worlds.UnderO;
        float climb = on ? Mathf.Clamp(inp.climb + (inp.hopHeld ? 1f : 0f) - (inp.downHeld ? 1f : 0f), -1f, 1f) : 0f;
        if (on) yaw += inp.move.x * 55f * dt;
        Vector3 fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 wishH = on ? fwd * inp.move.y * 11f : Vector3.zero;
        float wishV = climb * 5f + (on ? 0f : Mathf.Sin(Time.time * 0.6f) * 0.2f);
        Vector3 hv = Vector3.MoveTowards(new Vector3(v.x, 0f, v.z), wishH, 6f * dt);
        float vy = Mathf.MoveTowards(v.y, wishV, 5f * dt);
        if (local.y > -1.6f && vy > 0f) { vy = 0f; }
        rb.velocity = new Vector3(hv.x, vy, hv.z);
        pitchVis = Mathf.Lerp(pitchVis, -vy * 3f, dt * 3f);
        rb.MoveRotation(Quaternion.Euler(pitchVis, yaw, -inp.move.x * 5f));
        // surfacing: at the top and still rising -> pop up at the ranch dock
        if (on && local.y > -2.2f && climb > 0.3f) surfaceT += dt; else surfaceT = 0f;
        if (on && driver.human && surfaceT > 0.25f && surfaceT - dt <= 0.25f) driver.Toast("Surfacing... keep rising to head back to the ranch", 1.5f);
        if (on && surfaceT > 1.2f && UnderwaterWorld.I != null)
        {
            surfaceT = 0f;
            Frog d = driver;
            UnderwaterWorld.I.Surface(d);
        }
    }

    void Update()
    {
        float sp = Speed + Mathf.Abs(rb.velocity.y);
        if (prop != null) prop.Rotate(Vector3.forward, (driver != null ? 300f + sp * 120f : 0f) * Time.deltaTime, Space.Self);
        if (driver != null && Random.value < 0.15f + sp * 0.05f) FX.Bubble(transform.TransformPoint(new Vector3(0f, 0f, -4f)), 2);
    }
}
