using System.Collections.Generic;
using UnityEngine;

// ffu24 STORY EPISODE 2 "Catch Jimmy!": the places only this episode visits, built the first time it starts.
//  MERCURY   (MercO)  scorched grey cratered plain, black sky, a HUGE sun low on the horizon (frogs use the Callisto
//                     world id there = low gravity, no fog; the per-camera look comes from PreCull below)
//  ENCELADUS (EncO)   Saturn's icy moon: white ice, blue "tiger stripe" grooves, geysers that launch froggies, Saturn +
//                     rings filling the sky
//  MARS DEEP (DeepO)  three chambers under Mars where the Mars froggies and doggies live, joined by sloping tunnels
//                     (Mars world id: Mars gravity; the dark cave look comes from PreCull)
//  TRAVEL / ORBIT     two "deep space" dioramas for the Starship trips and the final chase over Earth
// Mars and Callisto themselves are the existing SurfaceWorlds. Everything is primitives + a few generated meshes.
public class StoryEp2Set : MonoBehaviour
{
    public static StoryEp2Set I;
    public static readonly Vector3 MercO = new Vector3(-4000f, 0f, -2000f);
    public static readonly Vector3 EncO = new Vector3(-4000f, 0f, 2000f);
    public static readonly Vector3 DeepO = new Vector3(-1600f, 0f, -2400f);
    public static readonly Vector3 TravelV = new Vector3(-25000f, 3000f, 25000f);
    public static readonly Vector3 OrbitV = new Vector3(-25000f, 3000f, -25000f);
    // the three Mars chambers (local to DeepO): centre x/z, radius, floor height
    public static readonly Vector4[] Chambers = { new Vector4(0f, 0f, 21f, 0f), new Vector4(0f, 62f, 21f, -14f), new Vector4(0f, 124f, 22f, -28f) };

    public Transform mercRoot, encRoot, deepRoot, travelRoot, orbitRoot;
    public Transform mercShip, encShip, marsShip, calShip, ranchShip, travelShip, orbitShip;
    public Transform travelStars, orbitStars, orbitEarth, travelJim;
    public readonly Dictionary<string, Transform> travelDest = new Dictionary<string, Transform>();
    public readonly List<Vector3> vents = new List<Vector3>();
    public readonly List<FrogModel> marsFrogs = new List<FrogModel>();
    public readonly List<Animal> marsDogs = new List<Animal>();
    public Transform ventBeam, deepCrumbs;
    float ventT;
    public bool ventsOn;

    public static void Ensure()
    {
        if (I != null) return;
        I = new GameObject("StoryEp2Set").AddComponent<StoryEp2Set>();
        float t0 = Time.realtimeSinceStartup;
        I.BuildMercury(); I.BuildEnceladus(); I.BuildDeep(); I.BuildTravel(); I.BuildOrbit(); I.BuildShips();
        I.HideAll();
        Debug.Log("FFSTORY ep2 set built in " + ((Time.realtimeSinceStartup - t0) * 1000f).ToString("0") + " ms");
    }

    public void HideAll()
    {
        foreach (var t in new[] { mercRoot, encRoot, deepRoot, travelRoot, orbitRoot, marsShip, calShip, ranchShip }) if (t != null) t.gameObject.SetActive(false);
    }
    public static void Show(Transform t, bool on) { if (t != null && t.gameObject.activeSelf != on) t.gameObject.SetActive(on); }

    // ---------------- heights ----------------
    static readonly Vector3[] mercCraters = MakeCraters(11, 16, 88f);
    static Vector3[] MakeCraters(int seed, int n, float range)
    {
        var r = new System.Random(seed); var l = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float x, z;
            do { x = ((float)r.NextDouble() - 0.5f) * 2f * range; z = ((float)r.NextDouble() - 0.5f) * 2f * range; } while (new Vector2(x, z + 18f).magnitude < 16f);
            l[i] = new Vector3(x, z, 5f + (float)r.NextDouble() * (i < 4 ? 14f : 7f));
        }
        return l;
    }
    public static float MercY(float x, float z)
    {
        float y = (Mathf.PerlinNoise(x * 0.02f + 3f, z * 0.02f + 8f) - 0.5f) * 6f + (Mathf.PerlinNoise(x * 0.11f, z * 0.11f + 4f) - 0.5f) * 0.9f;
        foreach (Vector3 c in mercCraters)
        {
            float d = new Vector2(x - c.x, z - c.y).magnitude, R = c.z;
            y += Mathf.Exp(-Mathf.Pow((d - R) / (0.22f * R), 2f)) * 0.22f * R - Mathf.Clamp01(1f - d / R) * 0.38f * R;
        }
        y *= Mathf.Clamp01(new Vector2(x, z + 18f).magnitude / 13f);          // landing spot
        float e = new Vector2(x, z).magnitude;
        y += Mathf.SmoothStep(0f, 1f, (e - 80f) / 14f) * 22f;                   // rim
        return y;
    }
    public static float EncY(float x, float z)
    {
        float y = (Mathf.PerlinNoise(x * 0.018f + 1f, z * 0.018f + 6f) - 0.5f) * 5f;
        for (int k = 0; k < 4; k++)
        {
            // tiger stripes: four long gently curved grooves
            float zc = -40f + k * 26f + Mathf.Sin(x * 0.025f + k) * 6f;
            float d = Mathf.Abs(z - zc);
            y += Mathf.Exp(-d * d / 6f) * -1.6f + Mathf.Exp(-Mathf.Pow((d - 3f) / 1.2f, 2f)) * 0.8f;
        }
        y *= Mathf.Clamp01(new Vector2(x, z + 18f).magnitude / 13f);
        float e = new Vector2(x, z).magnitude;
        y += Mathf.SmoothStep(0f, 1f, (e - 80f) / 14f) * 20f;
        return y;
    }
    // inside metric for the deep chambers: < 0 inside a chamber / tunnel; floor = the floor height there
    static float DeepSd(float x, float z, out float floor)
    {
        float best = 1e9f; floor = 0f;
        for (int i = 0; i < 3; i++)
        {
            Vector4 c = Chambers[i];
            float s = new Vector2(x - c.x, z - c.y).magnitude - c.z;
            if (s < best) { best = s; floor = c.w; }
        }
        if (best > 0f)
        {
            for (int i = 0; i < 2; i++)
            {
                Vector4 a = Chambers[i], b = Chambers[i + 1];
                float z0 = a.y + a.z, z1 = b.y - b.z;
                float t = Mathf.Clamp01((z - z0) / (z1 - z0));
                float zz = Mathf.Clamp(z, a.y, b.y);
                float s = new Vector2(x, z - zz).magnitude - 3.6f;
                if (z < a.y || z > b.y) s = 1e9f;
                if (s < best) { best = s; floor = Mathf.Lerp(a.w, b.w, t); }
            }
        }
        return best;
    }
    public static float DeepY(float x, float z)
    {
        float f;
        float s = DeepSd(x, z, out f);
        float y = f + (Mathf.PerlinNoise(x * 0.3f + 7f, z * 0.3f) - 0.5f) * 0.25f;
        if (s > 0f) y += Mathf.SmoothStep(0f, 1f, s / 2.6f) * 16f + (Mathf.PerlinNoise(x * 0.4f, z * 0.4f + 3f)) * 2.5f * Mathf.Clamp01(s / 2f);
        return y;
    }
    public static int DeepLevel(Vector3 w)
    {
        Vector3 l = w - DeepO;
        int best = 0; float bd = 1e9f;
        for (int i = 0; i < 3; i++) { float d = new Vector2(l.x - Chambers[i].x, l.z - Chambers[i].y).magnitude + Mathf.Abs(l.y - Chambers[i].w) * 2f; if (d < bd) { bd = d; best = i; } }
        return best;
    }
    public static Vector3 DeepC(int i, float up = 0f) { Vector4 c = Chambers[i]; return DeepO + new Vector3(c.x, c.w + up, c.y); }

    // ---------------- shared builders ----------------
    static GameObject Ground(string name, System.Func<float, float, float> h, Vector3 o, float x0, float x1, float z0, float z1, float step, Material m, float uvM)
    {
        int nx = Mathf.CeilToInt((x1 - x0) / step), nz = Mathf.CeilToInt((z1 - z0) / step);
        var v = new Vector3[(nx + 1) * (nz + 1)]; var uv = new Vector2[v.Length];
        for (int z = 0; z <= nz; z++)
            for (int x = 0; x <= nx; x++)
            {
                float lx = x0 + x * step, lz = z0 + z * step;
                v[z * (nx + 1) + x] = o + new Vector3(lx, h(lx, lz), lz);
                uv[z * (nx + 1) + x] = new Vector2(lx / uvM, lz / uvM);
            }
        var tri = new int[nx * nz * 6]; int k = 0;
        for (int z = 0; z < nz; z++)
            for (int x = 0; x < nx; x++) { int a = z * (nx + 1) + x, b = a + 1, c = a + nx + 1, d = c + 1; tri[k++] = a; tri[k++] = c; tri[k++] = b; tri[k++] = b; tri[k++] = c; tri[k++] = d; }
        var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, name = name };
        mesh.vertices = v; mesh.uv = uv; mesh.triangles = tri; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = m;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        return go;
    }

    static Material GroundMat(string res, Color tint, float gloss)
    {
        var t = Resources.Load<Texture2D>(res);
        if (t == null) return Mats.Lit(tint);
        var m = Mats.Tex(t, gloss);
        m.color = tint;
        return m;
    }

    // an inside-out sphere (sky dome / cave dome): faces point inwards
    static Mesh inSphere;
    static Mesh InSphere()
    {
        if (inSphere != null) return inSphere;
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Mesh src = tmp.GetComponent<MeshFilter>().sharedMesh;
        Destroy(tmp);
        inSphere = new Mesh { name = "InSphere" };
        var vv = src.vertices; var nn = src.normals;
        for (int i = 0; i < nn.Length; i++) nn[i] = -nn[i];
        var tt = src.triangles; for (int i = 0; i < tt.Length; i += 3) { int a = tt[i + 1]; tt[i + 1] = tt[i + 2]; tt[i + 2] = a; }
        inSphere.vertices = vv; inSphere.normals = nn; inSphere.uv = src.uv; inSphere.triangles = tt; inSphere.RecalculateBounds();
        return inSphere;
    }
    static GameObject MeshGo(Transform parent, Mesh m, Material mat, Vector3 pos, Vector3 scale, string name)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.position = pos; g.transform.localScale = scale;
        g.AddComponent<MeshFilter>().sharedMesh = m;
        var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = m == null ? null : mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g;
    }
    static Transform Stars(Transform parent, Vector3 c, float r)
    {
        var st = Resources.Load<Texture2D>("LB/space_stars");
        Material m = st != null && Mats.UnlitTexBase != null ? Mats.UnlitTex(st) : Mats.Unlit(new Color(0.02f, 0.02f, 0.04f));
        var g = MeshGo(parent, InSphere(), m, c, Vector3.one * r * 2f, "Stars");
        g.GetComponent<MeshRenderer>().receiveShadows = false;
        return g.transform;
    }
    static Material Fx(Color c) { var m = new Material(Mats.Fx); m.color = c; return m; }

    // flat ring (annulus) in the local XZ plane, both faces
    static Mesh Annulus(float r0, float r1, int n)
    {
        var v = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>();
        for (int i = 0; i <= n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f, c = Mathf.Cos(a), s = Mathf.Sin(a);
            v.Add(new Vector3(c * r0, 0f, s * r0)); v.Add(new Vector3(c * r1, 0f, s * r1));
            uv.Add(new Vector2(0f, i / (float)n)); uv.Add(new Vector2(1f, i / (float)n));
        }
        for (int i = 0; i < n; i++)
        {
            int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
            t.Add(a); t.Add(b); t.Add(c); t.Add(c); t.Add(b); t.Add(d);
            t.Add(a); t.Add(c); t.Add(b); t.Add(c); t.Add(d); t.Add(b);
        }
        var m = new Mesh { name = "Annulus" };
        m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }
    static Texture2D Bands(Color a, Color b, int seed)
    {
        var t = new Texture2D(8, 128, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
        var r = new System.Random(seed);
        for (int y = 0; y < 128; y++)
        {
            float v = Mathf.PerlinNoise(y * 0.11f, seed) * 1.1f + (float)r.NextDouble() * 0.06f;
            Color c = Color.Lerp(a, b, v);
            for (int x = 0; x < 8; x++) t.SetPixel(x, y, c);
        }
        t.Apply(true);
        return t;
    }
    // Saturn: banded globe + four ring bands (two-sided annuli, translucent)
    public static Transform Saturn(Transform parent, Vector3 pos, float R, bool lit)
    {
        var t = new GameObject("Saturn").transform; t.SetParent(parent, false); t.position = pos;
        var tex = Bands(new Color(0.93f, 0.84f, 0.62f), new Color(0.74f, 0.6f, 0.4f), 9);
        Material m = lit ? new Material(Mats.Lit(Color.white)) { mainTexture = tex } : Mats.UnlitTex(tex);
        var g = Mats.Prim(PrimitiveType.Sphere, t, Vector3.zero, Vector3.one * R * 2f, m); Mats.NoShadows(g);
        float[] rr = { 1.22f, 1.5f, 1.56f, 1.92f, 2.0f, 2.3f };
        Color[] cc = { new Color(0.75f, 0.68f, 0.55f, 0.35f), new Color(0.92f, 0.85f, 0.7f, 0.75f), new Color(0.85f, 0.78f, 0.62f, 0.6f) };
        for (int k = 0; k < 3; k++)
        {
            var a = MeshGo(t, Annulus(rr[k * 2] * R, rr[k * 2 + 1] * R, 96), Fx(cc[k]), pos, Vector3.one, "Ring" + k);
            a.transform.localPosition = Vector3.zero;
        }
        t.rotation = Quaternion.Euler(-26f, 20f, 14f);
        return t;
    }
    public static Transform Sun(Transform parent, Vector3 pos, float R)
    {
        var t = new GameObject("Sun").transform; t.SetParent(parent, false); t.position = pos;
        var core = Mats.Prim(PrimitiveType.Sphere, t, Vector3.zero, Vector3.one * R * 2f, Mats.Unlit(new Color(1f, 0.97f, 0.86f))); Mats.NoShadows(core);
        var h1 = Mats.Prim(PrimitiveType.Sphere, t, Vector3.zero, Vector3.one * R * 2.5f, Fx(new Color(1f, 0.82f, 0.45f, 0.35f))); Mats.NoShadows(h1);
        var h2 = Mats.Prim(PrimitiveType.Sphere, t, Vector3.zero, Vector3.one * R * 3.4f, Fx(new Color(1f, 0.6f, 0.25f, 0.12f))); Mats.NoShadows(h2);
        return t;
    }
    static Transform Planet(Transform parent, string res, Color fallback, Vector3 pos, float R, float gloss = 0.06f)
    {
        var tx = Resources.Load<Texture2D>(res);
        Material m = tx != null ? Mats.Tex(tx, gloss) : Mats.Lit(fallback);
        var g = Mats.Prim(PrimitiveType.Sphere, parent, Vector3.zero, Vector3.one * R * 2f, m);
        g.transform.position = pos;
        Mats.NoShadows(g);
        return g.transform;
    }

    // keeps the two sign canvases Ranch.Sign creates under one of our roots
    static void Sign(Transform parent, Vector3 pos, float yaw, string text, Color bg, float w, float h)
    {
        Ranch.Sign(pos, yaw, text, bg, w, h);
        foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            if ((go.name == "Sign" || go.name == "SignBack") && (go.transform.position - pos).sqrMagnitude < 1f) go.transform.SetParent(parent, true);
    }

    // ---------------- Mercury ----------------
    void BuildMercury()
    {
        mercRoot = new GameObject("StoryMercury").transform;
        var stat = new GameObject("MercStatic").transform; stat.SetParent(mercRoot, false);
        var g = Ground("Mercury ground", MercY, MercO, -100f, 100f, -100f, 100f, Look.Mobile ? 2.2f : 1.7f, GroundMat("LB/calground", new Color(0.7f, 0.69f, 0.68f), 0.05f), 9f);
        g.transform.SetParent(mercRoot, true);
        g.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var r = new System.Random(17);
        Material rock = Mats.Lit(new Color(0.36f, 0.33f, 0.3f)), scorch = Mats.Shiny(new Color(0.13f, 0.11f, 0.1f));
        for (int i = 0; i < 46; i++)
        {
            float x = ((float)r.NextDouble() - 0.5f) * 150f, z = ((float)r.NextDouble() - 0.5f) * 150f;
            if (new Vector2(x, z + 18f).magnitude < 16f) continue;
            float s = 0.6f + (float)r.NextDouble() * (i % 7 == 0 ? 3.2f : 1.4f);
            Mats.Prim(PrimitiveType.Sphere, stat, MercO + new Vector3(x, MercY(x, z) + s * 0.25f, z), new Vector3(s * 1.3f, s * 0.8f, s), new Vector3(0f, (float)r.NextDouble() * 180f, 0f), i % 3 == 0 ? scorch : rock, true);
        }
        MeshMerge.Merge(stat, false);
        // the huge sun, low over the horizon, and the stars (no air = black sky)
        Sun(mercRoot, MercO + MercSunDir * -690f, 150f);
        Stars(mercRoot, MercO, 820f);
    }
    // direction the sunlight travels on Mercury (from the sun towards the ground): the sun sits low, north-east
    public static readonly Vector3 MercSunDir = new Vector3(-0.4f, -0.36f, -0.84f).normalized;

    // ---------------- Enceladus ----------------
    void BuildEnceladus()
    {
        encRoot = new GameObject("StoryEnceladus").transform;
        var stat = new GameObject("EncStatic").transform; stat.SetParent(encRoot, false);
        var g = Ground("Enceladus ground", EncY, EncO, -100f, 100f, -100f, 100f, Look.Mobile ? 2.2f : 1.7f, GroundMat("LB/calground", new Color(1.25f, 1.32f, 1.42f), 0.35f), 9f);
        g.transform.SetParent(encRoot, true);
        g.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var r = new System.Random(23);
        Material ice = Mats.Shiny(new Color(0.82f, 0.92f, 1f)), blue = Mats.Shiny(new Color(0.45f, 0.7f, 0.95f));
        for (int i = 0; i < 40; i++)
        {
            float x = ((float)r.NextDouble() - 0.5f) * 150f, z = ((float)r.NextDouble() - 0.5f) * 150f;
            if (new Vector2(x, z + 18f).magnitude < 16f) continue;
            float s = 0.6f + (float)r.NextDouble() * 1.8f;
            Mats.Prim(PrimitiveType.Cube, stat, EncO + new Vector3(x, EncY(x, z) + s * 0.6f, z), new Vector3(s * 0.6f, s * 1.7f, s * 0.6f), new Vector3((float)r.NextDouble() * 20f, (float)r.NextDouble() * 90f, (float)r.NextDouble() * 20f), i % 4 == 0 ? blue : ice, true);
        }
        // geysers in the tiger stripes: a blue vent ring each (the plumes are sprays, see Update)
        for (int k = 0; k < 4; k++)
            for (int j = 0; j < 2; j++)
            {
                float x = -45f + j * 70f + k * 9f;
                float zc = -40f + k * 26f + Mathf.Sin(x * 0.025f + k) * 6f;
                if (new Vector2(x, zc + 18f).magnitude < 15f) x += 18f;
                Vector3 p = EncO + new Vector3(x, EncY(x, zc), zc);
                vents.Add(p);
                Mats.Prim(PrimitiveType.Cylinder, stat, p + Vector3.up * 0.05f, new Vector3(3.2f, 0.06f, 3.2f), Mats.Unlit(new Color(0.35f, 0.75f, 1f)));
                Mats.Prim(PrimitiveType.Cylinder, stat, p + Vector3.up * 0.08f, new Vector3(1.6f, 0.06f, 1.6f), Mats.Lit(new Color(0.1f, 0.2f, 0.35f)));
            }
        MeshMerge.Merge(stat, false);
        // Saturn and its rings fill the sky; the far small sun
        Saturn(encRoot, EncO + new Vector3(70f, 175f, 455f), 150f, true);
        var sun = Mats.Prim(PrimitiveType.Sphere, encRoot, EncO + new Vector3(-420f, 260f, -520f), Vector3.one * 26f, Mats.Unlit(new Color(1f, 0.97f, 0.9f))); Mats.NoShadows(sun);
        Stars(encRoot, EncO, 820f);
    }
    public static readonly Vector3 EncSunDir = new Vector3(0.55f, -0.42f, 0.72f).normalized;

    // ---------------- Mars deep chambers ----------------
    static readonly Color[] FrogCols = { new Color(0.55f, 0.35f, 0.85f), new Color(0.95f, 0.55f, 0.75f), new Color(0.35f, 0.75f, 0.95f), new Color(0.9f, 0.75f, 0.25f), new Color(0.45f, 0.85f, 0.75f) };
    void BuildDeep()
    {
        deepRoot = new GameObject("StoryMarsDeep").transform;
        var stat = new GameObject("DeepStatic").transform; stat.SetParent(deepRoot, false);
        var g = Ground("Mars deep ground", DeepY, DeepO, -34f, 34f, -32f, 154f, Look.Mobile ? 1.4f : 1f, GroundMat("LB/marsground", new Color(0.85f, 0.52f, 0.42f), 0.08f), 6f);
        g.transform.SetParent(deepRoot, true);
        g.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Material rock = Mats.Lit(new Color(0.3f, 0.17f, 0.13f));
        Color[] glow = { new Color(0.35f, 0.75f, 0.72f), new Color(0.58f, 0.42f, 0.78f), new Color(0.8f, 0.52f, 0.32f) };   // ffu24b: calmer glow
        for (int i = 0; i < 3; i++)
        {
            Vector4 c = Chambers[i];
            Vector3 cp = DeepO + new Vector3(c.x, c.w, c.y);
            // dome (inside-out sphere sunk into the floor; no collider - an inward-facing collider would wall off the tunnels)
            var dome = MeshGo(deepRoot, InSphere(), rock, cp + Vector3.up * 1f, new Vector3((c.z + 4f) * 2f, 26f, (c.z + 4f) * 2f), "Dome" + i);
            var rr = new System.Random(40 + i);
            for (int k = 0; k < 14; k++)
            {
                // small crystal clusters along the walls (ffu24b: were big slabs in the middle of the floor)
                float a = (float)rr.NextDouble() * Mathf.PI * 2f, d = c.z - 2.5f - (float)rr.NextDouble() * 4f;
                float x = c.x + Mathf.Cos(a) * d, z = c.y + Mathf.Sin(a) * d;
                if (Mathf.Abs(x) < 6f) continue;   // keep the walkway clear
                float h = 0.4f + (float)rr.NextDouble() * 0.8f;
                Mats.Prim(PrimitiveType.Cube, stat, DeepO + new Vector3(x, DeepY(x, z) + h * 0.35f, z), new Vector3(0.2f, h, 0.2f), new Vector3(15f, k * 40f, 10f), Mats.Unlit(glow[k % 3]));
                Mats.Prim(PrimitiveType.Cube, stat, DeepO + new Vector3(x + 0.25f, DeepY(x, z) + h * 0.2f, z + 0.15f), new Vector3(0.14f, h * 0.6f, 0.14f), new Vector3(-20f, k * 40f + 30f, 5f), Mats.Unlit(glow[(k + 1) % 3]));
            }
            // lantern posts along the walls
            for (int k = 0; k < 6; k++)
            {
                float a = k * Mathf.PI / 3f + 0.4f;
                float x = c.x + Mathf.Cos(a) * (c.z - 3f), z = c.y + Mathf.Sin(a) * (c.z - 3f);
                if (Mathf.Abs(x) < 5f) continue;
                Vector3 p = DeepO + new Vector3(x, DeepY(x, z), z);
                Mats.Prim(PrimitiveType.Cube, stat, p + Vector3.up * 1.1f, new Vector3(0.18f, 2.2f, 0.18f), Mats.Lit(new Color(0.25f, 0.16f, 0.1f)));
                Mats.Prim(PrimitiveType.Sphere, stat, p + Vector3.up * 2.35f, Vector3.one * 0.55f, Mats.Unlit(new Color(1f, 0.78f, 0.4f)));
            }
            if (!Look.Mobile)
            {
                var lg = new GameObject("CaveLight" + i); lg.transform.SetParent(deepRoot, false); lg.transform.position = cp + Vector3.up * 8f;
                var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.range = c.z * 1.9f; l.intensity = 1.4f; l.color = new Color(1f, 0.72f, 0.5f); l.shadows = LightShadows.None;
            }
        }
        // tunnel ceilings
        for (int i = 0; i < 2; i++)
        {
            Vector4 a = Chambers[i], b = Chambers[i + 1];
            for (float z = a.y + a.z - 1f; z < b.y - b.z + 2f; z += 3f)
            {
                float f = DeepY(0f, z);
                Mats.Prim(PrimitiveType.Cube, stat, DeepO + new Vector3(0f, f + 6.5f, z), new Vector3(10f, 1.4f, 3.3f), rock);
                Mats.Prim(PrimitiveType.Sphere, stat, DeepO + new Vector3(3.6f, f + 2.4f, z), new Vector3(1.6f, 4.8f, 3f), rock);
                Mats.Prim(PrimitiveType.Sphere, stat, DeepO + new Vector3(-3.6f, f + 2.4f, z), new Vector3(1.6f, 4.8f, 3f), rock);
            }
        }
        // level 1: mushroom garden; level 2: doggy den (beds, bones); level 3: big crystal + the vent shaft
        Material cap1 = Mats.Lit(new Color(0.85f, 0.35f, 0.55f)), cap2 = Mats.Unlit(new Color(0.55f, 0.9f, 1f)), stalk = Mats.Lit(new Color(0.9f, 0.85f, 0.75f));
        var r2 = new System.Random(7);
        for (int k = 0; k < 14; k++)
        {
            float x = (k % 2 == 0 ? -1f : 1f) * (7f + (float)r2.NextDouble() * 9f), z = -10f + (float)r2.NextDouble() * 18f;
            float s = 0.6f + (float)r2.NextDouble() * 1.4f;
            Vector3 p = DeepO + new Vector3(x, DeepY(x, z), z);
            Mats.Prim(PrimitiveType.Cylinder, stat, p + Vector3.up * s * 0.6f, new Vector3(0.25f * s, 0.6f * s, 0.25f * s), stalk);
            Mats.Prim(PrimitiveType.Sphere, stat, p + Vector3.up * s * 1.2f, new Vector3(1.3f * s, 0.55f * s, 1.3f * s), k % 3 == 0 ? cap2 : cap1);
        }
        Color[] bed = { new Color(0.8f, 0.3f, 0.3f), new Color(0.3f, 0.5f, 0.85f), new Color(0.85f, 0.7f, 0.3f) };
        for (int k = 0; k < 7; k++)
        {
            float a = k * 0.9f + 0.3f, d = 9f + (k % 3) * 3f;
            float x = Mathf.Cos(a) * d, z = 62f + Mathf.Sin(a) * d;
            if (Mathf.Abs(x) < 4.5f) x += 6f * Mathf.Sign(x == 0f ? 1f : x);
            Vector3 p = DeepO + new Vector3(x, DeepY(x, z), z);
            Mats.Prim(PrimitiveType.Cylinder, stat, p + Vector3.up * 0.12f, new Vector3(1.8f, 0.12f, 1.8f), Mats.Lit(bed[k % 3]));
            Mats.Prim(PrimitiveType.Cylinder, stat, p + Vector3.up * 0.18f, new Vector3(1.3f, 0.1f, 1.3f), Mats.Lit(Color.Lerp(bed[k % 3], Color.white, 0.5f)));
            Mats.Prim(PrimitiveType.Capsule, stat, p + new Vector3(1.4f, 0.12f, 0.6f), new Vector3(0.12f, 0.35f, 0.12f), new Vector3(0f, 0f, 90f), Mats.Lit(new Color(0.95f, 0.93f, 0.85f)));
        }
        {
            Vector3 p = DeepC(2);
            p.y = DeepO.y + DeepY(-9f, 128f);
            for (int k = 0; k < 7; k++)
                Mats.Prim(PrimitiveType.Cube, stat, DeepO + new Vector3(-15f, DeepY(-15f, 128f), 128f) + new Vector3(Mathf.Cos(k) * 0.8f, 0.9f + (k % 3) * 0.3f, Mathf.Sin(k) * 0.8f), new Vector3(0.45f, 1.8f + (k % 3) * 0.6f, 0.45f), new Vector3(Mathf.Cos(k) * 22f, k * 51f, Mathf.Sin(k) * 22f), Mats.Unlit(glow[k % 2]));
        }
        MeshMerge.Merge(stat, false);
        // the vent shaft: a column of light coming down from a crack in the deepest ceiling
        Vector3 vp = DeepO + new Vector3(0f, -28f, 134f);
        var vb = Mats.Prim(PrimitiveType.Cylinder, deepRoot, vp + Vector3.up * 16f, new Vector3(3.4f, 16f, 3.4f), Fx(new Color(1f, 0.85f, 0.6f, 0.16f)));
        Mats.NoShadows(vb); ventBeam = vb.transform;
        // Jimmy's jet trail down the tunnels: glowing breadcrumbs
        deepCrumbs = new GameObject("JetTrail").transform; deepCrumbs.SetParent(deepRoot, false);
        Material crumb = Mats.Unlit(new Color(1f, 0.62f, 0.2f));
        for (float z = 10f; z < 120f; z += 3.2f)
        {
            float x = Mathf.Sin(z * 0.2f) * 0.8f;
            var c = Mats.Prim(PrimitiveType.Sphere, deepCrumbs, DeepO + new Vector3(x, DeepY(x, z) + 0.25f, z), Vector3.one * 0.32f, crumb);
            Mats.NoShadows(c);
        }
        // (no world-space signs here: big FFDisplay sign text overflows the dynamic font atlas; the HUD names the level)
        // the people who live down here: Mars froggies (unnamed) and Mars doggies (unnamed)
        var r3 = new System.Random(3);
        int[] frogsPer = { 4, 3, 3 }, dogsPer = { 1, 6, 4 };
        for (int i = 0; i < 3; i++)
        {
            Vector4 c = Chambers[i];
            for (int k = 0; k < frogsPer[i]; k++)
            {
                float a = (float)r3.NextDouble() * 6.28f, d = 6f + (float)r3.NextDouble() * 10f;
                float x = c.x + Mathf.Cos(a) * d, z = c.y + Mathf.Sin(a) * d;
                if (Mathf.Abs(x) < 4f) x += 5f;
                var fg = new GameObject("MarsFroggy");
                fg.transform.SetParent(deepRoot, false);
                fg.transform.position = DeepO + new Vector3(x, DeepY(x, z), z);
                fg.transform.rotation = Quaternion.Euler(0f, (float)r3.NextDouble() * 360f, 0f);
                var fm = fg.AddComponent<FrogModel>();
                fm.Build(FrogCols[(k + i) % FrogCols.Length]);
                fg.transform.localScale = Vector3.one * 0.9f;
                marsFrogs.Add(fm);
            }
            for (int k = 0; k < dogsPer[i]; k++)
            {
                float x = c.x + ((float)r3.NextDouble() - 0.5f) * c.z, z = c.y + ((float)r3.NextDouble() - 0.5f) * c.z;
                var d = Animal.Dog("", DeepO + new Vector3(x, DeepY(x, z), z), k % 2 == 0);
                d.transform.SetParent(deepRoot, true);
                d.transform.localScale = Vector3.one * 0.65f;
                d.Init(new Rect(DeepO.x + c.x - c.z * 0.6f, DeepO.z + c.y - c.z * 0.6f, c.z * 1.2f, c.z * 1.2f), DeepO.y + c.w);
                d.skittish = false;
                d.groundFn = (gx, gz) => DeepO.y + DeepY(gx - DeepO.x, gz - DeepO.z);
                marsDogs.Add(d);
            }
        }
    }

    // ---------------- the Starship (story copy, nose along +z; stood up for landings) ----------------
    public static Transform StarshipVis(Transform parent, float s)
    {
        var root = new GameObject("StoryStarship").transform; root.SetParent(parent, false);
        var vis = Mats.Node(root, "Vis", Vector3.zero); vis.localScale = Vector3.one * s;
        const float Rr = 2.1f, L = 13f;
        Material steel = Mats.PBR(new Color(0.86f, 0.87f, 0.89f), 0.5f, 0.15f), weld = Mats.Lit(new Color(0.66f, 0.67f, 0.7f));   // ffu24b: Paint read black on Mars
        Material tile = Mats.Lit(new Color(0.1f, 0.1f, 0.11f)), flapM = Mats.Lit(new Color(0.13f, 0.13f, 0.14f)), bell = Mats.Steel(new Color(0.42f, 0.38f, 0.34f));
        Mats.Prim(PrimitiveType.Cylinder, vis, Vector3.zero, new Vector3(Rr * 2f, L * 0.5f, Rr * 2f), new Vector3(90f, 0f, 0f), steel);
        for (int k = 0; k < 6; k++) Mats.Prim(PrimitiveType.Cylinder, vis, new Vector3(0f, 0f, -L * 0.5f + 1.2f + k * 2.1f), new Vector3(Rr * 2f + 0.03f, 0.03f, Rr * 2f + 0.03f), new Vector3(90f, 0f, 0f), weld);
        for (int k = -3; k <= 3; k++)
        {
            float ang = k * 22f;
            Vector3 n = Quaternion.Euler(0f, 0f, ang) * Vector3.down;
            Mats.Prim(PrimitiveType.Cube, vis, n * (Rr + 0.02f), new Vector3(0.84f, 0.05f, L - 0.4f), new Vector3(0f, 0f, ang), tile);
        }
        Mats.Prim(PrimitiveType.Sphere, vis, new Vector3(0f, 0f, L * 0.5f), new Vector3(Rr * 2f, Rr * 2f, 6.4f), steel);
        Mats.Prim(PrimitiveType.Sphere, vis, new Vector3(0f, 0f, L * 0.5f + 1.8f), new Vector3(Rr * 1.3f, Rr * 1.3f, 3.6f), steel);
        Mats.Prim(PrimitiveType.Sphere, vis, new Vector3(0f, -0.45f, L * 0.5f + 0.4f), new Vector3(Rr * 1.85f, Rr * 1.6f, 5.6f), tile);
        for (int sd = -1; sd <= 1; sd += 2)
        {
            Mats.Prim(PrimitiveType.Cube, vis, new Vector3(sd * (Rr + 0.48f), -0.35f, L * 0.5f - 0.6f), new Vector3(1.0f, 0.1f, 2.0f), new Vector3(0f, -sd * 8f, sd * -6f), flapM);
            Mats.Prim(PrimitiveType.Cube, vis, new Vector3(sd * (Rr + 0.75f), -0.35f, -L * 0.5f + 1.7f), new Vector3(1.55f, 0.12f, 3.0f), new Vector3(0f, sd * 4f, sd * -6f), flapM);
        }
        for (int k = 0; k < 4; k++) Mats.Prim(PrimitiveType.Cube, vis, new Vector3(-0.75f + k * 0.5f, Rr - 0.02f, L * 0.5f - 1.6f), new Vector3(0.28f, 0.06f, 0.22f), Mats.Unlit(new Color(0.55f, 0.8f, 1f)));
        // four froggy colour dots by the windows (James, Jimmy, Bubbles, Rexy)
        for (int i = 0; i < 4; i++) Mats.Prim(PrimitiveType.Sphere, vis, new Vector3(-0.6f + i * 0.4f, Rr - 0.1f, L * 0.5f - 2.4f), new Vector3(0.24f, 0.08f, 0.24f), Mats.Paint(Froggies.Color(i), 0.85f));
        Mats.Prim(PrimitiveType.Cylinder, vis, new Vector3(0f, 0f, -L * 0.5f - 0.05f), new Vector3(Rr * 2f - 0.1f, 0.05f, Rr * 2f - 0.1f), new Vector3(90f, 0f, 0f), tile);
        for (int k = 0; k < 6; k++)
        {
            bool vac = k >= 3;
            float rr = vac ? 1.32f : 0.6f, br = vac ? 0.62f : 0.42f;
            Vector3 c = Quaternion.Euler(0f, 0f, k * 120f + (vac ? 60f : 0f)) * new Vector3(0f, rr, 0f) + new Vector3(0f, 0f, -L * 0.5f - 0.45f);
            Mats.Prim(PrimitiveType.Cylinder, vis, c, new Vector3(br * 2f, 0.42f, br * 2f), new Vector3(90f, 0f, 0f), bell);
            Mats.Prim(PrimitiveType.Cylinder, vis, c + new Vector3(0f, 0f, -0.43f), new Vector3(br * 1.7f, 0.01f, br * 1.7f), new Vector3(90f, 0f, 0f), Mats.Unlit(new Color(1f, 0.78f, 0.45f)));
        }
        // landing legs (folded flat along the skirt) and the plume
        for (int k = 0; k < 6; k++) Mats.Prim(PrimitiveType.Cube, vis, Quaternion.Euler(0f, 0f, k * 60f) * new Vector3(0f, Rr + 0.15f, 0f) + new Vector3(0f, 0f, -L * 0.5f + 0.6f), new Vector3(0.3f, 0.3f, 2.2f), Quaternion.Euler(0f, 0f, k * 60f).eulerAngles, flapM);
        var flame = Mats.Node(vis, "Flame", new Vector3(0f, 0f, -L * 0.5f - 0.9f));
        Mats.Prim(PrimitiveType.Sphere, flame, new Vector3(0f, 0f, -3.4f), new Vector3(3.6f, 3.6f, 7.5f), Fx(new Color(0.55f, 0.62f, 1f, 0.3f)));
        Mats.Prim(PrimitiveType.Sphere, flame, new Vector3(0f, 0f, -1.6f), new Vector3(1.5f, 1.5f, 3.4f), Mats.Unlit(new Color(0.9f, 0.93f, 1f)));
        flame.gameObject.SetActive(false);
        Mats.NoShadows(root.gameObject);
        return root;
    }
    public static void ShipFlame(Transform ship, float k)
    {
        if (ship == null) return;
        var f = ship.Find("Vis/Flame");
        if (f == null) return;
        bool on = k > 0.01f;
        if (f.gameObject.activeSelf != on) f.gameObject.SetActive(on);
        if (on) f.localScale = new Vector3(1f, 1f, (0.5f + k) * (1f + Mathf.Sin(Time.time * 41f) * 0.07f));
    }
    // a standing ship: nose up, base on the ground at p
    public static void Stand(Transform ship, Vector3 p, float yaw)
    {
        float s = ship.Find("Vis").localScale.x;
        ship.rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(-90f, 0f, 0f);
        ship.position = p + Vector3.up * (6.5f + 1.1f) * s;
    }
    public static Vector3 NoseUp(Transform ship) { return ship.position + ship.forward * 9f * ship.Find("Vis").localScale.x; }

    void BuildShips()
    {
        float s = 1.45f;
        mercShip = StarshipVis(mercRoot, s); Stand(mercShip, MercO + new Vector3(-6f, MercY(-6f, -26f), -26f), 30f);
        encShip = StarshipVis(encRoot, s); Stand(encShip, EncO + new Vector3(-6f, EncY(-6f, -26f), -26f), 30f);
        marsShip = StarshipVis(null, s); marsShip.name = "StoryStarship (Mars)";
        Stand(marsShip, SurfaceWorlds.M(-16f, SurfaceWorlds.MarsY(-16f, -40f), -40f), 30f);
        calShip = StarshipVis(null, s); calShip.name = "StoryStarship (Callisto)";
        Stand(calShip, SurfaceWorlds.C(12f, SurfaceWorlds.CalY(12f, -9f), -9f), 30f);
        ranchShip = StarshipVis(null, s); ranchShip.name = "StoryStarship (ranch)";
        Stand(ranchShip, RanchLanding, 30f);
    }
    public static Vector3 RanchLanding { get { Vector3 p = new Vector3(-28f, 0f, -56f); p.y = Ranch.GY(p.x, p.z); return p; } }

    // ---------------- travel diorama ----------------
    void BuildTravel()
    {
        travelRoot = new GameObject("StoryEp2Travel").transform; travelRoot.position = TravelV;
        travelStars = Stars(travelRoot, TravelV, 9000f);
        Transform d;
        d = Node("mars"); Planet(d, "LB/space_mars", new Color(0.8f, 0.4f, 0.2f), TravelV + new Vector3(0f, -60f, 1500f), 430f);
        d = Node("callisto"); Planet(d, "LB/space_callisto", new Color(0.5f, 0.45f, 0.4f), TravelV + new Vector3(0f, -20f, 1300f), 170f);
        Planet(d, "LB/space_jupiter", new Color(0.85f, 0.7f, 0.5f), TravelV + new Vector3(900f, 300f, 3600f), 1300f);
        d = Node("mercury"); var mp = Mats.Prim(PrimitiveType.Sphere, d, Vector3.zero, Vector3.one * 380f, Mats.Lit(new Color(0.58f, 0.55f, 0.52f))); mp.transform.position = TravelV + new Vector3(0f, -30f, 1200f); Mats.NoShadows(mp);
        for (int k = 0; k < 9; k++) { Vector3 dir = Quaternion.Euler(k * 37f - 60f, k * 71f, 0f) * Vector3.back; var cr = Mats.Prim(PrimitiveType.Sphere, mp.transform, dir * 0.47f, Vector3.one * (0.08f + (k % 3) * 0.05f), Mats.Lit(new Color(0.4f, 0.38f, 0.36f))); Mats.NoShadows(cr); }
        Sun(d, TravelV + new Vector3(-1300f, 500f, 4200f), 900f);
        d = Node("saturn"); var en = Mats.Prim(PrimitiveType.Sphere, d, Vector3.zero, Vector3.one * 160f, Mats.Shiny(new Color(0.92f, 0.96f, 1f))); en.transform.position = TravelV + new Vector3(0f, -20f, 1150f); Mats.NoShadows(en);
        Saturn(d, TravelV + new Vector3(-700f, 250f, 3300f), 900f, true);
        d = Node("earth"); Planet(d, "LB/space_earth", new Color(0.2f, 0.4f, 0.85f), TravelV + new Vector3(0f, -80f, 1500f), 470f, 0.3f);
        var atm = new Material(Mats.Glass); atm.color = new Color(0.55f, 0.75f, 1f, 0.22f);
        var at = Mats.Prim(PrimitiveType.Sphere, d, Vector3.zero, Vector3.one * 470f * 2.07f, atm); at.transform.position = TravelV + new Vector3(0f, -80f, 1500f); Mats.NoShadows(at);
        Planet(d, "LB/space_moon", new Color(0.7f, 0.7f, 0.7f), TravelV + new Vector3(700f, 160f, 2400f), 120f);
        travelShip = StarshipVis(travelRoot, 1f);
        // Jimmy far ahead: a bright dot with a long jet trail
        travelJim = new GameObject("TravelJimmy").transform; travelJim.SetParent(travelRoot, false);
        var dot = Mats.Prim(PrimitiveType.Sphere, travelJim, Vector3.zero, Vector3.one * 1.6f, Mats.Unlit(new Color(1f, 0.85f, 0.4f))); Mats.NoShadows(dot);
        var tr = travelJim.gameObject.AddComponent<TrailRenderer>();
        tr.time = 0.35f; tr.startWidth = 0.9f; tr.endWidth = 0f; tr.sharedMaterial = Fx(new Color(1f, 0.65f, 0.25f, 0.6f));
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; tr.receiveShadows = false;
    }
    Transform Node(string id) { var t = new GameObject("Dest " + id).transform; t.SetParent(travelRoot, false); travelDest[id] = t; return t; }
    public void ShowDest(string id) { foreach (var kv in travelDest) Show(kv.Value, kv.Key == id); }

    // ---------------- Earth orbit (final chase) ----------------
    void BuildOrbit()
    {
        orbitRoot = new GameObject("StoryEp2Orbit").transform; orbitRoot.position = OrbitV;
        orbitStars = Stars(orbitRoot, OrbitV, 9000f);
        orbitEarth = Planet(orbitRoot, "LB/space_earth", new Color(0.2f, 0.4f, 0.85f), OrbitV + new Vector3(0f, -3400f, 0f), 3000f, 0.3f);
        orbitEarth.rotation = Quaternion.Euler(10f, 0f, 23f);
        var atm = new Material(Mats.Glass); atm.color = new Color(0.5f, 0.72f, 1f, 0.25f);
        var at = Mats.Prim(PrimitiveType.Sphere, orbitRoot, Vector3.zero, Vector3.one * 3000f * 2.03f, atm); at.transform.position = orbitEarth.position; Mats.NoShadows(at);
        Planet(orbitRoot, "LB/space_moon", new Color(0.7f, 0.7f, 0.7f), OrbitV + new Vector3(2600f, 700f, 6200f), 330f);
        Sun(orbitRoot, OrbitV + new Vector3(-6200f, 1800f, 7000f), 220f);
        orbitShip = StarshipVis(orbitRoot, 1f);
    }

    // ---------------- per camera look (called from Worlds.PreCull while episode 2 runs) ----------------
    static float sunBase = -1f; static Color sunCol0 = Color.white;
    public static void Install(bool on)
    {
        var sun = RenderSettings.sun;
        if (on)
        {
            if (sun != null && sunBase < 0f) { sunBase = sun.intensity; sunCol0 = sun.color; }
            Worlds.StoryHook = PreCull;
            Worlds.PlaceNameHook = PlaceName;
        }
        else
        {
            Worlds.StoryHook = null;
            Worlds.PlaceNameHook = null;
            if (sun != null && sunBase >= 0f) { sun.intensity = sunBase; sun.color = sunCol0; }
            sunBase = -1f;
        }
    }
    static bool Near(Vector3 a, Vector3 b, float r) { return (a - b).sqrMagnitude < r * r; }
    static string PlaceName(WorldId w, Vector3 p)
    {
        if (w == WorldId.Callisto && Near(p, MercO, 700f)) return "Mercury";
        if (w == WorldId.Callisto && Near(p, EncO, 700f)) return "Enceladus (Saturn)";
        if (w == WorldId.Mars && Near(p, DeepO + new Vector3(0f, -14f, 62f), 260f)) return "Under Mars · Level " + (DeepLevel(p) + 1);
        return null;
    }
    static void PreCull(Camera c, WorldId w)
    {
        var sun = RenderSettings.sun;
        if (c == null || c.cullingMask == (1 << 20)) return;
        Vector3 p = c.transform.position;
        float ib = sunBase >= 0f ? sunBase : 1f;
        if (w == WorldId.Callisto && Near(p, MercO, 700f))
        {
            RenderSettings.fog = false;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.42f, 0.43f); RenderSettings.ambientEquatorColor = new Color(0.33f, 0.32f, 0.32f); RenderSettings.ambientGroundColor = new Color(0.15f, 0.14f, 0.14f);
            c.backgroundColor = new Color(0.01f, 0.01f, 0.015f);
            if (sun != null) { sun.transform.rotation = Quaternion.LookRotation(MercSunDir); sun.color = new Color(1f, 0.96f, 0.9f); sun.intensity = ib * 1.6f; }
            return;
        }
        if (w == WorldId.Callisto && Near(p, EncO, 700f))
        {
            RenderSettings.fog = false;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.62f, 0.75f); RenderSettings.ambientEquatorColor = new Color(0.42f, 0.48f, 0.58f); RenderSettings.ambientGroundColor = new Color(0.25f, 0.3f, 0.38f);
            c.backgroundColor = new Color(0.005f, 0.008f, 0.02f);
            if (sun != null) { sun.transform.rotation = Quaternion.LookRotation(EncSunDir); sun.color = new Color(0.92f, 0.96f, 1f); sun.intensity = ib * 0.95f; }
            return;
        }
        if (w == WorldId.Mars && Near(p, DeepO + new Vector3(0f, -14f, 62f), 260f))
        {
            RenderSettings.fog = true; RenderSettings.fogColor = new Color(0.06f, 0.035f, 0.05f); RenderSettings.fogDensity = 0.016f;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.3f, 0.36f); RenderSettings.ambientEquatorColor = new Color(0.3f, 0.2f, 0.26f); RenderSettings.ambientGroundColor = new Color(0.12f, 0.08f, 0.1f);
            c.backgroundColor = new Color(0.03f, 0.015f, 0.025f);
            if (sun != null) { sun.color = new Color(1f, 0.7f, 0.5f); sun.intensity = ib * 0.12f; }
            return;
        }
        if (w == WorldId.Mars) c.backgroundColor = new Color(0.55f, 0.36f, 0.25f);   // a Mars camera that was in the deep before
        if (w == WorldId.Space && (Near(p, OrbitV, 9000f) || Near(p, TravelV, 9000f)))
        {
            if (sun != null) { sun.transform.rotation = Quaternion.LookRotation(Near(p, TravelV, 9000f) ? new Vector3(-0.45f, -0.3f, 0.84f) : new Vector3(0.62f, -0.25f, -0.74f)); sun.color = Color.white; sun.intensity = ib * 1.15f; }
            return;
        }
        if (sun != null) { sun.color = sunCol0; sun.intensity = ib; }
    }

    // ---------------- per frame: geysers, Mars froggies ----------------
    public readonly List<Frog> launched = new List<Frog>();
    void Update()
    {
        float dt = Time.deltaTime;
        if (encRoot != null && encRoot.gameObject.activeInHierarchy && ventsOn)
        {
            ventT += dt;
            for (int k = 0; k < vents.Count; k++)
            {
                float ph = Mathf.Repeat(ventT + k * 1.7f, 7f);
                bool erupt = ph < 3.2f;
                if (!erupt) { if (Random.value < dt * 2f) FX.Spray(vents[k] + Vector3.up * 0.3f, Vector3.up * 2f + Random.insideUnitSphere, 0.6f, 1.2f, new Color(0.9f, 0.96f, 1f, 0.35f)); continue; }
                int n = Look.Mobile ? 2 : 4;
                for (int i = 0; i < n; i++) if (Random.value < dt * 30f) FX.Spray(vents[k] + Vector3.up * 0.4f, Vector3.up * Random.Range(14f, 22f) + Random.insideUnitSphere * 2.2f, Random.Range(0.8f, 1.6f), Random.Range(1.4f, 2.2f), new Color(0.92f, 0.97f, 1f, 0.7f));
                if (Game.I == null) continue;
                foreach (var f in Game.I.frogs)
                {
                    if (f == null || f.world != WorldId.Callisto || f.vehicle != null) continue;
                    Vector3 d = f.transform.position - vents[k]; d.y = 0f;
                    if (d.magnitude < 2.2f && f.transform.position.y < vents[k].y + 2f)
                    {
                        f.Knock(Vector3.up * 13f + d.normalized * 3f);
                        if (f.human) f.Toast("WHEEE! Geyser jump!", 1.6f);
                        Sfx.PlayAt(Sfx.Splash != null ? Sfx.Splash : Sfx.Pickup, vents[k], 0.6f, 50f, 1.3f);
                    }
                }
            }
        }
        if (deepRoot != null && deepRoot.gameObject.activeInHierarchy)
            for (int i = 0; i < marsFrogs.Count; i++)
            {
                var m = marsFrogs[i];
                if (m == null) continue;
                float h = Mathf.Repeat(Time.time * 0.6f + i * 0.37f, 3f);
                bool hop = h < 0.5f;
                m.Animate(0f, hop, false, false, Mathf.Min(dt, 0.05f));
                if (m.bob != null) m.bob.localPosition = new Vector3(0f, hop ? Mathf.Sin(h / 0.5f * Mathf.PI) * 0.45f : 0f, 0f);
            }
    }

    // ---------------- fireworks (ending) ----------------
    static readonly Color[] FwC = { new Color(1f, 0.3f, 0.3f), new Color(1f, 0.85f, 0.3f), new Color(0.65f, 0.95f, 0.25f), new Color(0.4f, 0.7f, 1f), new Color(1f, 0.5f, 1f), Color.white };
    public static void Firework(Vector3 at)
    {
        Color c = FwC[Random.Range(0, FwC.Length)], c2 = FwC[Random.Range(0, FwC.Length)];
        int n = Look.Mobile ? 40 : 70;
        for (int i = 0; i < n; i++) FX.Spray(at, Random.onUnitSphere * Random.Range(9f, 13f), Random.Range(0.5f, 0.8f), Random.Range(1.1f, 1.7f), i % 3 == 0 ? c2 : c);
        FX.Sparkle(at, Color.white, 10);
        Sfx.PlayAt(Sfx.Boom, at, 0.55f, 260f, Random.Range(1.3f, 1.7f));
    }
}

// Jimmy, the runaway: a story-only froggy (his roster model) with a long-range jetpack, a jet trail and a name tag.
// Story.cs moves him; this only draws him (pose, jet flame / puffs, sputter smoke when he runs low on fuel).
public class JimmyNpc : MonoBehaviour
{
    public FrogModel model;
    public TrailRenderer trail;
    Transform pack, beacon, tag;
    readonly Transform[] noz = new Transform[2];
    public float jet, sputter, speed; public bool air;
    Material beaconMat;

    public static JimmyNpc Make()
    {
        var go = new GameObject("Jimmy (story)");
        var j = go.AddComponent<JimmyNpc>();
        j.Build();
        return j;
    }

    void Build()
    {
        var mg = new GameObject("Model"); mg.transform.SetParent(transform, false);
        model = mg.AddComponent<FrogModel>();
        model.BuildChar(1);   // Jimmy from the roster
        Transform g = model.bob != null ? model.bob : model.transform;
        pack = new GameObject("Jetpack").transform; pack.SetParent(g, false);
        Material white = Mats.Shiny(new Color(0.94f, 0.95f, 0.97f)), orange = Mats.Shiny(new Color(1f, 0.5f, 0.1f)), dark = Mats.Lit(new Color(0.12f, 0.13f, 0.15f));
        Mats.Prim(PrimitiveType.Cube, pack, new Vector3(0f, 0.62f, -0.58f), new Vector3(0.6f, 0.62f, 0.3f), white);
        for (int k = 0; k < 2; k++)
        {
            float x = k == 0 ? -0.2f : 0.2f;
            Mats.Prim(PrimitiveType.Capsule, pack, new Vector3(x, 0.7f, -0.78f), new Vector3(0.24f, 0.36f, 0.24f), orange);
            var nz = Mats.Prim(PrimitiveType.Cylinder, pack, new Vector3(x, 0.26f, -0.78f), new Vector3(0.16f, 0.1f, 0.16f), dark);
            noz[k] = nz.transform;
        }
        Mats.Prim(PrimitiveType.Cylinder, pack, new Vector3(0.24f, 1.05f, -0.6f), new Vector3(0.03f, 0.25f, 0.03f), dark);
        beaconMat = new Material(Mats.Unlit(new Color(0.75f, 1f, 0.35f)));
        beacon = Mats.Prim(PrimitiveType.Sphere, pack, new Vector3(0.24f, 1.32f, -0.6f), Vector3.one * 0.14f, beaconMat).transform;
        Mats.Prim(PrimitiveType.Sphere, g, new Vector3(0f, 0.93f, 0.1f), new Vector3(1.04f, 0.95f, 1.06f), Mats.GlassTint(new Color(0.7f, 0.95f, 0.8f, 0.25f)));
        Mats.SetLayer(gameObject, 9);
        var tg = new GameObject("TrailAt"); tg.transform.SetParent(pack, false); tg.transform.localPosition = new Vector3(0f, 0.15f, -0.78f);
        trail = tg.AddComponent<TrailRenderer>();
        trail.time = 1.4f; trail.startWidth = 0.55f; trail.endWidth = 0f; trail.minVertexDistance = 0.3f;
        var tm = new Material(Mats.Fx); tm.color = new Color(1f, 0.66f, 0.25f, 0.6f);
        trail.sharedMaterial = tm; trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; trail.receiveShadows = false;
        trail.emitting = false;
        var lab = new GameObject("Tag"); lab.transform.SetParent(transform, false); lab.transform.localPosition = Vector3.up * 2.2f;
        var t = lab.AddComponent<TextMesh>();
        t.text = "JIMMY"; UIK.WorldText(t, 48, 0.05f); t.anchor = TextAnchor.MiddleCenter; t.color = new Color(0.8f, 1f, 0.45f);
        lab.AddComponent<Billboard>();
        tag = lab.transform;
    }

    public void ShowTag(bool on) { if (tag != null && tag.gameObject.activeSelf != on) tag.gameObject.SetActive(on); }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.1f);
        if (model != null) model.Animate(speed, air, false, false, Mathf.Min(dt, 0.05f));
        if (trail != null) trail.emitting = jet > 0.2f;
        if (beaconMat != null) beaconMat.color = Mathf.Repeat(Time.time * 2f, 1f) < 0.5f ? new Color(0.75f, 1f, 0.35f) : new Color(0.25f, 0.4f, 0.1f);
        for (int k = 0; k < 2; k++)
        {
            if (noz[k] == null) continue;
            Vector3 np = noz[k].position;
            Vector3 dn = -transform.up;
            if (jet > 0.05f && Random.value < dt * (10f + jet * 40f))
            {
                FX.Flame(np, dn);
                FX.Spray(np + dn * 0.2f, dn * (4f + jet * 6f) + Random.insideUnitSphere, 0.4f + jet * 0.4f, 0.6f, new Color(1f, 0.8f, 0.5f, 0.6f));
            }
            if (sputter > 0.05f && Random.value < dt * sputter * 6f) FX.Smoke(np, Random.Range(0.6f, 1.1f), new Color(0.3f, 0.3f, 0.32f, 0.7f));
        }
    }
}
