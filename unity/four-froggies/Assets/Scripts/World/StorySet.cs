using System.Collections.Generic;
using UnityEngine;

// ffu22 STORY MODE "The Big Launch": the story-only set, built the first time a story starts (free play never builds it).
// Launch pad south of James's house with the frogs' own little rocket, the part pickups around the ranch / house / track,
// the repair and storm props, rain + lightning, fireworks, the Mars copy of the rocket + the cave rockfall and pups, and
// a small "deep space" diorama far from every world for the flight cutscenes. Everything is primitives + a few lathed
// meshes (rocket body, nose, bell, tanks) on shared materials, merged where static, so phones pay very little.
public class StorySet : MonoBehaviour
{
    public static StorySet I;
    public static readonly Vector2 PadXZ = new Vector2(-38f, -92f);
    public static Vector3 Pad;                 // centre of the pad deck (top of the concrete)
    public const float CradleH = 2.85f;        // rocket body bottom above the deck
    public Transform root;                     // ranch story props
    public Rocket rocket;                      // the ranch rocket (root moves for launches)
    public Vector3 PanelPos, BenchPos, BoardPos, TankFixPos, GatherPos;
    public Transform benchFins;                // the two spare fins lying on the workbench (chapter 6)

    // part pickups
    public Transform engineCrate;              // carried on a vehicle roof once loaded
    public Transform engineSpot;               // its sign
    public Vector3 EnginePos = new Vector3(-92f, 0f, 104f);
    public readonly List<Pickups.Item> fuel = new List<Pickups.Item>(), fins = new List<Pickups.Item>();
    public Pickups.Item noseItem, chipItem;
    public Vector3 ChipVaultPos = new Vector3(-60f, 0f, 36f);
    public Transform chipVaultLid, chipVault;
    public Vector3 SimonC;                     // house: centre of the four floor pads
    public readonly Vector3[] simonPads = new Vector3[4];
    public readonly Renderer[] simonGlow = new Renderer[4];
    public Transform noseChest, noseChestLid;
    public readonly List<Transform> beams = new List<Transform>();
    Material beamMat;

    // storm
    public readonly List<Transform> anchors = new List<Transform>();
    public readonly List<Renderer> anchorLights = new List<Renderer>();
    public readonly List<Rigidbody> debris = new List<Rigidbody>();
    readonly List<ParticleSystem> rain = new List<ParticleSystem>();
    LineRenderer bolt;
    float boltT;
    public Transform stormFence;

    // Mars
    public Rocket marsRocket;
    public Vector3 MarsRocketPos, RockfallPos;
    public readonly List<Transform> rocks = new List<Transform>();
    public readonly List<Animal> pups = new List<Animal>();
    public readonly List<Animal> homePups = new List<Animal>();
    Transform marsRoot;

    // deep space diorama (flight cutscenes)
    public static readonly Vector3 SpaceV = new Vector3(25000f, 3000f, 25000f);
    public Transform spaceRoot, spaceRocket, spaceMars, spaceEarth, spaceStars;

    static Material white, red, orange, steel, dark, glassM, yellow, concrete, ghost, lightCyan, lightGreen, lightAmber, lightRed;

    public static void Ensure()
    {
        if (I != null) return;
        I = new GameObject("StorySet").AddComponent<StorySet>();
        I.Build();
    }

    static void InitMats()
    {
        if (white != null) return;
        white = Mats.Paint(new Color(0.93f, 0.93f, 0.9f), 0.8f);
        red = Mats.Paint(new Color(0.86f, 0.16f, 0.12f), 0.85f);
        orange = Mats.Paint(new Color(1f, 0.55f, 0.12f), 0.8f);
        steel = Mats.Steel(new Color(0.72f, 0.73f, 0.76f));
        dark = Mats.Lit(new Color(0.16f, 0.17f, 0.19f));
        glassM = Mats.GlassTint(new Color(0.35f, 0.75f, 1f));
        yellow = Mats.Unlit(new Color(1f, 0.8f, 0.15f));
        concrete = Mats.Lit(new Color(0.62f, 0.61f, 0.58f));
        ghost = new Material(Mats.Fx); ghost.color = new Color(0.4f, 0.9f, 1f, 0.22f);
        lightCyan = Mats.Unlit(new Color(0.3f, 1f, 0.95f));
        lightGreen = Mats.Unlit(new Color(0.35f, 1f, 0.4f));
        lightAmber = Mats.Unlit(new Color(1f, 0.7f, 0.15f));
        lightRed = Mats.Unlit(new Color(1f, 0.2f, 0.15f));
    }

    public static float GY(float x, float z) { return Ranch.GY(x, z); }

    // Ranch.Sign makes two root objects (front + back canvas): keep them under the story roots so they hide with them
    Transform signParent;
    void StorySign(Vector3 pos, float yaw, string text, Color bg, float w, float h)
    {
        Ranch.Sign(pos, yaw, text, bg, w, h);
        Transform par = signParent != null ? signParent : root;
        foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            if ((go.name == "Sign" || go.name == "SignBack") && (go.transform.position - pos).sqrMagnitude < 1f) go.transform.SetParent(par, true);
    }
    static Vector3 G(float x, float z, float up = 0f) { return new Vector3(x, Ranch.GY(x, z) + up, z); }

    // ---------------- meshes ----------------
    // lathe: profile (radius, height) points bottom -> top, n segments, smooth normals from the profile slope
    public static Mesh Lathe(Vector2[] prof, int n)
    {
        int m = prof.Length;
        var v = new Vector3[m * (n + 1)]; var nor = new Vector3[v.Length]; var uv = new Vector2[v.Length];
        for (int j = 0; j < m; j++)
        {
            Vector2 a = prof[Mathf.Max(0, j - 1)], b = prof[Mathf.Min(m - 1, j + 1)];
            Vector2 d = (b - a); if (d.sqrMagnitude < 1e-8f) d = Vector2.up;
            Vector2 pn = new Vector2(d.y, -d.x).normalized;   // outward
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n * Mathf.PI * 2f, c = Mathf.Cos(t), s = Mathf.Sin(t);
                int k = j * (n + 1) + i;
                v[k] = new Vector3(prof[j].x * c, prof[j].y, prof[j].x * s);
                nor[k] = new Vector3(pn.x * c, pn.y, pn.x * s);
                uv[k] = new Vector2(i / (float)n, j / (float)(m - 1));
            }
        }
        var tri = new List<int>();
        for (int j = 0; j < m - 1; j++)
            for (int i = 0; i < n; i++)
            {
                int a = j * (n + 1) + i, b = a + 1, c = a + n + 1, d2 = c + 1;
                tri.Add(a); tri.Add(c); tri.Add(b); tri.Add(b); tri.Add(c); tri.Add(d2);
            }
        var mesh = new Mesh { name = "Lathe" };
        mesh.vertices = v; mesh.normals = nor; mesh.uv = uv; mesh.SetTriangles(tri, 0); mesh.RecalculateBounds();
        return mesh;
    }

    // a flat trapezoid fin (extruded), root along y at x 0, tip outwards along +x
    static Mesh finMesh;
    public static Mesh FinMesh()
    {
        if (finMesh != null) return finMesh;
        Vector2[] o = { new Vector2(0f, 0f), new Vector2(1.5f, -0.5f), new Vector2(1.5f, 0.4f), new Vector2(0f, 2.6f) };
        float w = 0.09f;
        var v = new List<Vector3>(); var tri = new List<int>();
        foreach (float z in new[] { -w, w }) foreach (var p in o) v.Add(new Vector3(p.x, p.y, z));
        int[] f = { 0, 1, 2, 0, 2, 3 };
        for (int i = 0; i < 6; i += 3) { tri.Add(f[i]); tri.Add(f[i + 2]); tri.Add(f[i + 1]); tri.Add(4 + f[i]); tri.Add(4 + f[i + 1]); tri.Add(4 + f[i + 2]); }
        for (int i = 0; i < 4; i++) { int a = i, b = (i + 1) % 4; tri.Add(a); tri.Add(b); tri.Add(4 + a); tri.Add(b); tri.Add(4 + b); tri.Add(4 + a); }
        finMesh = new Mesh { name = "Fin" };
        finMesh.SetVertices(v); finMesh.SetTriangles(tri, 0); finMesh.RecalculateNormals(); finMesh.RecalculateBounds();
        return finMesh;
    }

    static GameObject MeshGo(Transform parent, Mesh m, Material mat, Vector3 lp, Vector3 euler, Vector3 scale, string name = "Part")
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = lp; g.transform.localRotation = Quaternion.Euler(euler); g.transform.localScale = scale;
        g.AddComponent<MeshFilter>().sharedMesh = m;
        g.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return g;
    }

    static Mesh bodyM, noseM, bellM, tankM, capM;
    static void InitMeshes()
    {
        if (bodyM != null) return;
        bodyM = Lathe(new[] { new Vector2(0f, 0f), new Vector2(1.3f, 0f), new Vector2(1.45f, 0.12f), new Vector2(1.5f, 0.5f), new Vector2(1.5f, 9.0f), new Vector2(1.42f, 9.5f), new Vector2(1.38f, 9.62f), new Vector2(0f, 9.62f) }, 40);
        var np = new List<Vector2>();
        for (int i = 0; i <= 14; i++) { float t = i / 14f; np.Add(new Vector2(1.4f * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t * 0.98f)) * (1f - t * 0.35f), t * 3.8f)); }
        np.Add(new Vector2(0f, 3.86f));
        noseM = Lathe(np.ToArray(), 36);
        bellM = Lathe(new[] { new Vector2(0.62f, 0.0f), new Vector2(0.52f, -0.35f), new Vector2(0.72f, -0.9f), new Vector2(0.98f, -1.5f), new Vector2(1.18f, -2.0f), new Vector2(1.1f, -2.02f), new Vector2(0.9f, -1.5f), new Vector2(0.62f, -0.9f), new Vector2(0.44f, -0.35f), new Vector2(0.5f, -0.02f) }, 32);
        var tp = new List<Vector2>();
        for (int i = 0; i <= 6; i++) { float a = i / 6f * Mathf.PI * 0.5f; tp.Add(new Vector2(0.62f * Mathf.Sin(a), 0.62f - 0.62f * Mathf.Cos(a))); }
        for (int i = 0; i <= 8; i++) { float a = i / 8f * Mathf.PI * 0.5f; tp.Add(new Vector2(0.62f * Mathf.Cos(a), 4.6f + 0.9f * Mathf.Sin(a))); }
        tankM = Lathe(tp.ToArray(), 24);
        capM = Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.9f, 0f), new Vector2(0.95f, 0.1f), new Vector2(0.95f, 0.7f), new Vector2(0f, 0.75f) }, 24);
    }

    // ---------------- the rocket ----------------
    public class Rocket
    {
        public Transform root;                       // origin = bottom of the body
        public GameObject engine, tankL, tankR, tankRBurst, patch, nose, chip, frame;
        public readonly GameObject[] fins = new GameObject[4], finStubs = new GameObject[4], ghosts = new GameObject[5];
        public readonly List<Renderer> chipLights = new List<Renderer>();
        public Transform flame, flameInner, smokeAt;
        public Light glow;
        public float flameK, sputter;

        public Vector3 SlotWorld(int part)
        {
            switch (part)
            {
                case 0: return root.TransformPoint(new Vector3(0f, -1f, 0f));
                case 1: return root.TransformPoint(new Vector3(1.95f, 3.5f, 0f));
                case 2: return root.TransformPoint(new Vector3(0f, 11f, 0f));
                case 3: return root.TransformPoint(new Vector3(0f, 1.4f, 1.7f));
                default: return root.TransformPoint(new Vector3(0f, 5.2f, 1.55f));
            }
        }

        // mask bits: 0 engine, 1 fuel tanks, 2 nose cone, 3 fins, 4 chip
        public void Show(int mask, bool ghostsOn, bool burst, bool patched, int finsMissing)
        {
            Set(engine, (mask & 1) != 0);
            Set(tankL, (mask & 2) != 0);
            Set(tankR, (mask & 2) != 0 && !burst);
            Set(tankRBurst, (mask & 2) != 0 && burst);
            Set(patch, (mask & 2) != 0 && patched && !burst);
            Set(nose, (mask & 4) != 0);
            Set(chip, (mask & 16) != 0);
            for (int i = 0; i < 4; i++)
            {
                bool gone = (mask & 8) != 0 && finsMissing > 0 && (i == 1 || (i == 3 && finsMissing > 1));
                Set(fins[i], (mask & 8) != 0 && !gone);
                Set(finStubs[i], gone);
            }
            for (int i = 0; i < 5; i++) Set(ghosts[i], ghostsOn && (mask & (1 << i)) == 0);
        }
        static void Set(GameObject g, bool on) { if (g != null && g.activeSelf != on) g.SetActive(on); }
    }

    public static Rocket BuildRocket(Transform parent, Vector3 pos, float scale = 1f)
    {
        InitMats(); InitMeshes();
        var r = new Rocket();
        r.root = new GameObject("StoryRocket").transform;
        r.root.SetParent(parent, false);
        r.root.position = pos; r.root.localScale = Vector3.one * scale;
        Transform t = r.root;
        // hull (always there: "the frame we built from scrap")
        r.frame = MeshGo(t, bodyM, white, Vector3.zero, Vector3.zero, Vector3.one, "Hull");
        foreach (float y in new[] { 1.9f, 8.3f }) MeshGo(t, Lathe(new[] { new Vector2(1.53f, 0f), new Vector2(1.53f, 0.42f) }, 40), red, new Vector3(0f, y, 0f), Vector3.zero, Vector3.one, "Stripe");
        Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 7.1f, 1.38f), new Vector3(1.05f, 1.05f, 0.5f), glassM);
        MeshGo(t, Lathe(new[] { new Vector2(0.55f, 0f), new Vector2(0.66f, 0f), new Vector2(0.66f, 0.14f), new Vector2(0.55f, 0.14f), new Vector2(0.55f, 0f) }, 28), steel, new Vector3(0f, 7.1f, 1.42f), new Vector3(90f, 0f, 0f), Vector3.one, "WindowRing");
        // the four froggy colour dots under the window (James, Jimmy, Bubbles, Rexy)
        for (int i = 0; i < 4; i++) Mats.Prim(PrimitiveType.Sphere, t, new Vector3(-0.45f + i * 0.3f, 6.15f, 1.47f), new Vector3(0.2f, 0.2f, 0.08f), Mats.Paint(Froggies.Color(i), 0.85f));
        // engine
        r.engine = new GameObject("Engine"); r.engine.transform.SetParent(t, false);
        MeshGo(r.engine.transform, capM, dark, new Vector3(0f, -0.72f, 0f), Vector3.zero, Vector3.one, "Block");
        MeshGo(r.engine.transform, bellM, steel, new Vector3(0f, -0.72f, 0f), Vector3.zero, Vector3.one, "Bell");
        r.flame = Mats.Node(t, "Flame", new Vector3(0f, -2.7f, 0f));
        var fo = Mats.Prim(PrimitiveType.Sphere, r.flame, new Vector3(0f, -2.2f, 0f), new Vector3(1.9f, 4.6f, 1.9f), Mats.Unlit(new Color(1f, 0.55f, 0.12f)));
        var fi = Mats.Prim(PrimitiveType.Sphere, r.flame, new Vector3(0f, -1.4f, 0f), new Vector3(1.0f, 3.0f, 1.0f), Mats.Unlit(new Color(1f, 0.95f, 0.75f)));
        Mats.NoShadows(fo); Mats.NoShadows(fi);
        r.flameInner = fi.transform;
        r.flame.gameObject.SetActive(false);
        r.smokeAt = Mats.Node(t, "SmokeAt", new Vector3(0f, -3f, 0f));
        // fuel tanks (two strapped boosters)
        r.tankL = new GameObject("TankL"); r.tankL.transform.SetParent(t, false);
        r.tankR = new GameObject("TankR"); r.tankR.transform.SetParent(t, false);
        foreach (var go in new[] { r.tankL, r.tankR })
        {
            float sx = go == r.tankL ? -1.95f : 1.95f;
            MeshGo(go.transform, tankM, orange, new Vector3(sx, 0.9f, 0f), Vector3.zero, Vector3.one, "Tank");
            MeshGo(go.transform, Lathe(new[] { new Vector2(0.64f, 0f), new Vector2(0.64f, 0.5f) }, 24), white, new Vector3(sx, 3.2f, 0f), Vector3.zero, Vector3.one, "Band");
            foreach (float y in new[] { 1.6f, 5.2f }) Mats.Prim(PrimitiveType.Cube, go.transform, new Vector3(sx * 0.62f, y, 0f), new Vector3(1.2f, 0.16f, 0.22f), steel);
        }
        r.tankRBurst = new GameObject("TankRBurst"); r.tankRBurst.transform.SetParent(t, false);
        MeshGo(r.tankRBurst.transform, tankM, Mats.Lit(new Color(0.25f, 0.18f, 0.14f)), new Vector3(1.95f, 0.9f, 0f), new Vector3(0f, 0f, -6f), new Vector3(1f, 0.62f, 1f), "Torn");
        for (int i = 0; i < 5; i++) Mats.Prim(PrimitiveType.Cube, r.tankRBurst.transform, new Vector3(1.95f + Mathf.Cos(i * 1.3f) * 0.5f, 3.7f + i * 0.08f, Mathf.Sin(i * 1.3f) * 0.5f), new Vector3(0.12f, 0.7f, 0.4f), new Vector3(i * 20f, i * 70f, 35f), Mats.Lit(new Color(0.3f, 0.2f, 0.15f)));
        r.patch = new GameObject("Patch"); r.patch.transform.SetParent(t, false);
        for (int i = 0; i < 3; i++)
        {
            Mats.Prim(PrimitiveType.Cube, r.patch.transform, new Vector3(1.95f, 2.4f + i * 0.9f, 0.6f), new Vector3(0.85f, 0.55f, 0.08f), new Vector3(0f, 0f, i * 7f - 7f), steel);
            Mats.Prim(PrimitiveType.Cube, r.patch.transform, new Vector3(1.95f, 2.4f + i * 0.9f, 0.645f), new Vector3(0.9f, 0.05f, 0.02f), lightAmber);
        }
        // nose cone
        r.nose = MeshGo(t, noseM, red, new Vector3(0f, 9.55f, 0f), Vector3.zero, Vector3.one, "Nose");
        Mats.Prim(PrimitiveType.Cylinder, r.nose.transform, new Vector3(0f, 4.2f, 0f), new Vector3(0.05f, 0.5f, 0.05f), steel);
        Mats.Prim(PrimitiveType.Sphere, r.nose.transform, new Vector3(0f, 4.72f, 0f), Vector3.one * 0.16f, lightRed);
        // fins + stubs (snapped in the storm)
        for (int i = 0; i < 4; i++)
        {
            float a = 45f + i * 90f;
            Quaternion q = Quaternion.Euler(0f, -a, 0f);
            Vector3 p = q * new Vector3(1.45f, -0.5f, 0f);
            r.fins[i] = MeshGo(t, FinMesh(), red, p, new Vector3(0f, -a, 0f), Vector3.one, "Fin");
            r.finStubs[i] = MeshGo(t, FinMesh(), red, p, new Vector3(0f, -a, 18f), new Vector3(0.35f, 0.45f, 1f), "FinStub");
        }
        // computer chip: a panel under the window with blinking lights
        r.chip = new GameObject("Chip"); r.chip.transform.SetParent(t, false);
        Mats.Prim(PrimitiveType.Cube, r.chip.transform, new Vector3(0f, 5.0f, 1.47f), new Vector3(1.1f, 0.62f, 0.1f), dark);
        for (int i = 0; i < 5; i++)
        {
            var l = Mats.Prim(PrimitiveType.Cube, r.chip.transform, new Vector3(-0.4f + i * 0.2f, 5.0f, 1.53f), new Vector3(0.12f, 0.12f, 0.04f), i % 2 == 0 ? lightCyan : lightGreen);
            r.chipLights.Add(l.GetComponent<Renderer>());
        }
        // ghost outlines of the missing parts (chapter 2)
        r.ghosts[0] = MeshGo(t, bellM, ghost, new Vector3(0f, -0.72f, 0f), Vector3.zero, Vector3.one * 1.04f, "GhostEngine");
        r.ghosts[1] = new GameObject("GhostTanks"); r.ghosts[1].transform.SetParent(t, false);
        foreach (float sx in new[] { -1.95f, 1.95f }) MeshGo(r.ghosts[1].transform, tankM, ghost, new Vector3(sx, 0.9f, 0f), Vector3.zero, Vector3.one * 1.04f, "GhostTank");
        r.ghosts[2] = MeshGo(t, noseM, ghost, new Vector3(0f, 9.55f, 0f), Vector3.zero, Vector3.one * 1.04f, "GhostNose");
        r.ghosts[3] = new GameObject("GhostFins"); r.ghosts[3].transform.SetParent(t, false);
        for (int i = 0; i < 4; i++) { float a = 45f + i * 90f; MeshGo(r.ghosts[3].transform, FinMesh(), ghost, Quaternion.Euler(0f, -a, 0f) * new Vector3(1.45f, -0.5f, 0f), new Vector3(0f, -a, 0f), Vector3.one * 1.05f, "GhostFin"); }
        r.ghosts[4] = Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 5.0f, 1.5f), new Vector3(1.2f, 0.7f, 0.14f), ghost);
        foreach (var g in r.ghosts) Mats.NoShadows(g);
        var lg = new GameObject("EngineLight"); lg.transform.SetParent(t, false); lg.transform.localPosition = new Vector3(0f, -3.5f, 0f);
        r.glow = lg.AddComponent<Light>(); r.glow.type = LightType.Point; r.glow.color = new Color(1f, 0.6f, 0.25f); r.glow.range = 30f; r.glow.intensity = 0f; r.glow.enabled = false;
        if (Look.Mobile) foreach (var mr in t.GetComponentsInChildren<Renderer>(true)) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return r;
    }

    // engine flame: k 0 off .. 1 full; sputter makes it cough
    public static void Flame(Rocket r, float k, float sputter, float dt)
    {
        if (r == null) return;
        bool on = k > 0.01f;
        if (r.flame.gameObject.activeSelf != on) r.flame.gameObject.SetActive(on);
        r.glow.enabled = on && !Look.Mobile;
        if (!on) { r.glow.intensity = 0f; return; }
        float cough = sputter > 0f ? (Mathf.PerlinNoise(Time.time * 9f, 3f) > 0.55f - sputter * 0.3f ? 1f : 0.15f) : 1f;
        float fl = (1f + Mathf.Sin(Time.time * 47f) * 0.08f + Random.Range(-0.07f, 0.07f)) * cough;
        r.flame.localScale = new Vector3(0.7f + 0.3f * k, (0.3f + 0.9f * k) * fl, 0.7f + 0.3f * k);
        r.glow.intensity = 2.5f * k * cough;
        Vector3 b = r.smokeAt.position;
        if (Random.value < k * 0.9f) FX.Flame(b, -r.root.up);
        if (sputter > 0f && Random.value < sputter * 0.5f) FX.Smoke(b + Random.insideUnitSphere, Random.Range(1.5f, 3f), new Color(0.25f, 0.25f, 0.27f, 0.8f));
    }

    // ---------------- build the ranch set ----------------
    void Build()
    {
        InitMats(); InitMeshes();
        root = new GameObject("StoryRanch").transform;
        float gy = GY(PadXZ.x, PadXZ.y);
        Pad = new Vector3(PadXZ.x, gy + 0.6f, PadXZ.y);
        var stat = new GameObject("StoryStatic").transform; stat.SetParent(root, false);
        // pad: round deck, hazard ring, cradle arms, small gantry, flame trench
        MeshGo(stat, Lathe(new[] { new Vector2(0f, -1.6f), new Vector2(10.2f, -1.6f), new Vector2(10.2f, 0.45f), new Vector2(9.9f, 0.6f), new Vector2(0f, 0.6f) }, 48), concrete, new Vector3(Pad.x, gy, Pad.z), Vector3.zero, Vector3.one, "Deck");
        var deckGo = stat.Find("Deck"); if (deckGo != null) deckGo.gameObject.AddComponent<MeshCollider>().sharedMesh = deckGo.GetComponent<MeshFilter>().sharedMesh;
        MeshGo(stat, Lathe(new[] { new Vector2(8.6f, 0.62f), new Vector2(9.3f, 0.62f) }, 48), yellow, new Vector3(Pad.x, gy, Pad.z), Vector3.zero, Vector3.one, "Hazard");
        MeshGo(stat, Lathe(new[] { new Vector2(2.4f, 0.615f), new Vector2(3.1f, 0.615f) }, 40), Mats.Lit(new Color(0.2f, 0.2f, 0.22f)), new Vector3(Pad.x, gy, Pad.z), Vector3.zero, Vector3.one, "Scorch");
        for (int i = 0; i < 4; i++)
        {
            Quaternion q = Quaternion.Euler(0f, i * 90f, 0f);   // between the fins (45 deg)
            Vector3 o = q * Vector3.forward;
            Mats.Prim(PrimitiveType.Cube, stat, Pad + o * 2.6f + Vector3.up * 1.3f, new Vector3(0.4f, 2.6f, 0.4f), q.eulerAngles, steel, true);
            Mats.Prim(PrimitiveType.Cube, stat, Pad + o * 2.0f + Vector3.up * 2.55f, new Vector3(0.3f, 0.3f, 1.3f), q.eulerAngles, steel);
        }
        Vector3 gt = Pad + new Vector3(-6.5f, 0f, -1.5f);
        for (int s = 0; s < 4; s++)
        {
            Vector3 o = new Vector3((s % 2) * 1.6f - 0.8f, 0f, (s / 2) * 1.6f - 0.8f);
            Mats.Prim(PrimitiveType.Cube, stat, gt + o + Vector3.up * 8f, new Vector3(0.22f, 16f, 0.22f), Vector3.zero, red, s == 0);
        }
        for (float h = 1.5f; h < 16f; h += 2.2f)
        {
            Mats.Prim(PrimitiveType.Cube, stat, gt + new Vector3(0f, h, -0.8f), new Vector3(1.8f, 0.14f, 0.14f), red);
            Mats.Prim(PrimitiveType.Cube, stat, gt + new Vector3(0f, h, 0.8f), new Vector3(1.8f, 0.14f, 0.14f), red);
            Mats.Prim(PrimitiveType.Cube, stat, gt + new Vector3(-0.8f, h, 0f), new Vector3(0.14f, 0.14f, 1.8f), red);
            Mats.Prim(PrimitiveType.Cube, stat, gt + new Vector3(0.8f, h, 0f), new Vector3(0.14f, 0.14f, 1.8f), red);
        }
        Mats.Prim(PrimitiveType.Cube, stat, gt + new Vector3(2.4f, 11.5f, 1.5f), new Vector3(3.6f, 0.3f, 0.4f), steel);   // umbilical arm
        // control panel (front right) + workbench (front left) + blueprint board (front)
        PanelPos = Pad + new Vector3(9f, 0f, 8.5f);
        BenchPos = Pad + new Vector3(-9f, 0f, 8.5f);
        BoardPos = Pad + new Vector3(0f, 0f, 13.5f);
        TankFixPos = Pad + new Vector3(4.6f, 0f, 1.5f);
        GatherPos = new Vector3(-26f, 0f, -52f); GatherPos.y = GY(GatherPos.x, GatherPos.z);
        Vector3 pp = G(PanelPos.x, PanelPos.z);
        Mats.Prim(PrimitiveType.Cube, stat, pp + new Vector3(0f, 0.55f, 0f), new Vector3(2.2f, 1.1f, 1.1f), dark, true);
        Mats.Prim(PrimitiveType.Cube, stat, pp + new Vector3(0f, 1.25f, -0.15f), new Vector3(2.2f, 0.3f, 0.9f), new Vector3(-25f, 0f, 0f), steel);
        Mats.Prim(PrimitiveType.Cube, stat, pp + new Vector3(0f, 1.9f, 0.35f), new Vector3(1.6f, 0.9f, 0.1f), dark);
        Mats.Prim(PrimitiveType.Cube, stat, pp + new Vector3(0f, 1.9f, 0.29f), new Vector3(1.45f, 0.75f, 0.02f), lightCyan);
        Mats.Prim(PrimitiveType.Cylinder, stat, pp + new Vector3(0.5f, 1.42f, -0.2f), new Vector3(0.36f, 0.08f, 0.36f), new Vector3(-25f, 0f, 0f), lightRed);
        for (int i = 0; i < 4; i++) Mats.Prim(PrimitiveType.Cube, stat, pp + new Vector3(-0.75f + i * 0.22f, 1.4f, -0.18f), new Vector3(0.14f, 0.06f, 0.14f), new Vector3(-25f, 0f, 0f), i % 2 == 0 ? lightGreen : lightAmber);
        Vector3 bp = G(BenchPos.x, BenchPos.z);
        Mats.Prim(PrimitiveType.Cube, stat, bp + new Vector3(0f, 0.9f, 0f), new Vector3(3.2f, 0.12f, 1.4f), Mats.Lit(new Color(0.55f, 0.38f, 0.22f)), true);
        foreach (var lx in new[] { -1.4f, 1.4f }) foreach (var lz in new[] { -0.55f, 0.55f }) Mats.Prim(PrimitiveType.Cube, stat, bp + new Vector3(lx, 0.45f, lz), new Vector3(0.12f, 0.9f, 0.12f), Mats.Lit(new Color(0.4f, 0.27f, 0.15f)));
        Mats.Prim(PrimitiveType.Cube, stat, bp + new Vector3(-1f, 1.05f, 0.2f), new Vector3(0.5f, 0.18f, 0.3f), red);   // toolbox
        Mats.Prim(PrimitiveType.Cylinder, stat, bp + new Vector3(0.9f, 1.15f, -0.3f), new Vector3(0.18f, 0.2f, 0.18f), steel);
        Vector3 bd = G(BoardPos.x, BoardPos.z);
        foreach (var lx in new[] { -2.6f, 2.6f }) Mats.Prim(PrimitiveType.Cube, stat, bd + new Vector3(lx, 1.3f, 0f), new Vector3(0.15f, 2.6f, 0.15f), Mats.Lit(new Color(0.45f, 0.3f, 0.18f)));
        StorySign(bd + new Vector3(0f, 2.55f, 0.05f), 0f, "THE BIG LAUNCH\n<size=19>engine  ·  fuel tanks  ·  nose cone  ·  fins  ·  computer chip</size>", new Color(0.08f, 0.22f, 0.45f), 5.4f, 1.9f);
        // two spare fins on the bench (shown in chapter 6)
        benchFins = new GameObject("BenchFins").transform; benchFins.SetParent(root, false);
        for (int i = 0; i < 2; i++) MeshGo(benchFins, FinMesh(), red, bp + new Vector3(-0.3f + i * 0.9f, 1.06f, -0.2f), new Vector3(90f, 0f, 90f), new Vector3(0.7f, 0.7f, 0.7f), "SpareFin");
        benchFins.gameObject.SetActive(false);
        // tie-down anchors (storm): three posts with a rope to the rocket
        for (int i = 0; i < 3; i++)
        {
            Quaternion q = Quaternion.Euler(0f, 30f + i * 120f, 0f);
            Vector3 ap = Pad + q * Vector3.forward * 7.2f;
            var a = new GameObject("Anchor" + i).transform; a.SetParent(root, false); a.position = ap;
            Mats.Prim(PrimitiveType.Cylinder, a, new Vector3(0f, 0.5f, 0f), new Vector3(0.35f, 0.5f, 0.35f), steel);
            var lt = Mats.Prim(PrimitiveType.Sphere, a, new Vector3(0f, 1.1f, 0f), Vector3.one * 0.4f, lightGreen);
            anchors.Add(a); anchorLights.Add(lt.GetComponent<Renderer>());
            var rope = a.gameObject.AddComponent<LineRenderer>();
            rope.positionCount = 2; rope.widthMultiplier = 0.06f; rope.sharedMaterial = Mats.Lit(new Color(0.9f, 0.85f, 0.6f));
            rope.SetPosition(0, ap + Vector3.up * 0.95f); rope.SetPosition(1, Pad + Vector3.up * (CradleH + 6f) + (ap - Pad).normalized * 1.5f);
            a.gameObject.SetActive(false);
        }
        foreach (var rr in stat.GetComponentsInChildren<Renderer>()) if (Look.Mobile) rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        MeshMerge.Merge(stat, false);

        rocket = BuildRocket(root, Pad + Vector3.up * CradleH);
        rocket.Show(0, true, false, false, 0);

        BuildPickups();
        beamMat = new Material(Mats.Fx); beamMat.color = new Color(1f, 0.92f, 0.45f, 0.22f);
        for (int i = 0; i < 6; i++)
        {
            var b = Mats.Prim(PrimitiveType.Cylinder, root, Vector3.zero, new Vector3(1.1f, 18f, 1.1f), beamMat);
            Mats.NoShadows(b); b.SetActive(false); beams.Add(b.transform);
        }
        BuildBolt();
        Debug.Log("FFSTORY set built at pad (" + Pad.x.ToString("0") + ", " + Pad.y.ToString("0.0") + ", " + Pad.z.ToString("0") + ")");
    }

    // ---------------- part pickups ----------------
    public static GameObject EngineModel(Transform parent, Vector3 lp, float s)
    {
        InitMats(); InitMeshes();
        var g = new GameObject("EngineModel"); g.transform.SetParent(parent, false); g.transform.localPosition = lp; g.transform.localScale = Vector3.one * s;
        MeshGo(g.transform, capM, dark, new Vector3(0f, 2.05f, 0f), Vector3.zero, Vector3.one, "Block");
        MeshGo(g.transform, bellM, steel, new Vector3(0f, 2.05f, 0f), Vector3.zero, Vector3.one, "Bell");
        Mats.Prim(PrimitiveType.Cube, g.transform, new Vector3(0f, 0.08f, 0f), new Vector3(2.6f, 0.16f, 2.6f), Mats.Lit(new Color(0.55f, 0.4f, 0.24f)));
        return g;
    }

    void BuildPickups()
    {
        // ENGINE: heavy, on a pallet by the pasture - needs a truck (it rides on the roof)
        EnginePos.y = GY(EnginePos.x, EnginePos.z);
        engineCrate = new GameObject("EngineCrate").transform; engineCrate.SetParent(root, false); engineCrate.position = EnginePos;
        EngineModel(engineCrate, Vector3.zero, 0.8f);
        engineSpot = new GameObject("EngineSpot").transform; engineSpot.SetParent(root, false);
        signParent = engineSpot;
        StorySign(EnginePos + new Vector3(0f, 3.4f, -2.4f), 180f, "ROCKET ENGINE\n<size=17>too heavy to carry - bring a truck!</size>", new Color(0.35f, 0.18f, 0.08f), 4.4f, 1.3f);
        // FUEL TANKS: three canisters on the loop lane - one sits at the very top of the loop (timed run)
        float lgy = GY(Layout.LoopC.x, Layout.LoopC.y);
        Vector3[] fp = { new Vector3(Layout.LoopC.x - 20f, lgy + 1.4f, Layout.LoopC.y), new Vector3(Layout.LoopC.x, lgy + 2f * Layout.LoopR - 1.6f, Layout.LoopC.y + Layout.LoopShift * 0.5f), new Vector3(Layout.LoopC.x + 20f, GY(Layout.LoopC.x + 20f, Layout.LoopC.y + Layout.LoopShift) + 1.4f, Layout.LoopC.y + Layout.LoopShift) };
        if (RallyTrack.BranchArcEnd > 1f)
        {
            // ffu19 seamless loop: on the approach straight, at the very top of the loop (hanging from the road), on the exit
            float a = RallyTrack.LoopArcA, b = RallyTrack.LoopArcB;
            Vector3 f, u;
            Vector3 q0 = RallyTrack.BranchFrameAt(a - 18f, out f, out u); fp[0] = q0 + u * 1.4f;
            Vector3 q1 = RallyTrack.BranchFrameAt((a + b) * 0.5f, out f, out u); fp[1] = q1 + u * 1.3f;
            Vector3 q2 = RallyTrack.BranchFrameAt(b + 18f, out f, out u); fp[2] = q2 + u * 1.4f;
        }
        for (int i = 0; i < 3; i++)
        {
            var t = new GameObject("FuelCan" + i).transform; t.SetParent(root, false); t.position = fp[i];
            MeshGo(t, tankM, orange, new Vector3(0f, -0.9f, 0f), Vector3.zero, Vector3.one * 0.38f, "Can");
            Mats.Prim(PrimitiveType.Sphere, t, Vector3.zero, Vector3.one * 2.2f, beamMatOr());
            fuel.Add(Pickups.Add(t, WorldId.Ranch, "st_fuel", i == 1 ? 3.6f : 2.6f));
        }
        // FINS: six around the rally figure-eight (four for the rocket + two spares)
        float[] ts = { 0.7f, 1.3f, 2.0f, 3.9f, 4.6f, 5.5f };   // clear of the crossover bridge (t 0 / pi)
        for (int i = 0; i < ts.Length; i++)
        {
            Vector3 p = Layout.TrackPoint(ts[i]);
            p.y = GY(p.x, p.z) + 1.7f;
            var t = new GameObject("FinPickup" + i).transform; t.SetParent(root, false); t.position = p;
            MeshGo(t, FinMesh(), red, new Vector3(-0.5f, -0.8f, 0f), Vector3.zero, Vector3.one * 0.7f, "Fin");
            fins.Add(Pickups.Add(t, WorldId.Ranch, "st_fin", 2.4f));
        }
        // NOSE CONE: in a chest in James's living room - opens after the light-pad puzzle
        SimonC = HouseWorld.L(-9.2f, 0f, 14.2f);
        Color[] sc = { new Color(1f, 0.25f, 0.2f), new Color(1f, 0.85f, 0.15f), new Color(0.25f, 0.85f, 0.3f), new Color(0.25f, 0.5f, 1f) };
        for (int i = 0; i < 4; i++)
        {
            simonPads[i] = SimonC + new Vector3((i % 2) * 2.4f - 1.2f, 0.03f, (i / 2) * 2.4f - 1.2f);
            Mats.Prim(PrimitiveType.Cylinder, root, simonPads[i], new Vector3(1.9f, 0.02f, 1.9f), Mats.Lit(sc[i] * 0.45f));
            var gl = Mats.Prim(PrimitiveType.Cylinder, root, simonPads[i] + Vector3.up * 0.025f, new Vector3(1.5f, 0.02f, 1.5f), Mats.Unlit(sc[i]));
            simonGlow[i] = gl.GetComponent<Renderer>(); simonGlow[i].enabled = false;
        }
        Vector3 chest = HouseWorld.L(-9.2f, 0f, 9.4f);
        noseChest = new GameObject("NoseChest").transform; noseChest.SetParent(root, false); noseChest.position = chest;
        Mats.Prim(PrimitiveType.Cube, noseChest, new Vector3(0f, 0.4f, 0f), new Vector3(1.6f, 0.8f, 1f), Mats.Paint(new Color(0.85f, 0.65f, 0.15f), 0.7f), true);
        noseChestLid = Mats.Node(noseChest, "Lid", new Vector3(0f, 0.8f, -0.5f));
        Mats.Prim(PrimitiveType.Cube, noseChestLid, new Vector3(0f, 0.08f, 0.5f), new Vector3(1.65f, 0.16f, 1.05f), Mats.Paint(new Color(0.75f, 0.52f, 0.1f), 0.7f));
        var nt = new GameObject("NosePickup").transform; nt.SetParent(root, false); nt.position = chest + Vector3.up * 1.4f;
        MeshGo(nt, noseM, red, new Vector3(0f, -0.4f, 0f), Vector3.zero, Vector3.one * 0.28f, "Nose");
        noseItem = Pickups.Add(nt, WorldId.House, "st_nose", 1.6f);
        noseItem.taken = true; nt.gameObject.SetActive(false);      // revealed by the puzzle
        // COMPUTER CHIP: in a heavy vault crate west of the garage - two robots have to lift the lid
        ChipVaultPos.y = GY(ChipVaultPos.x, ChipVaultPos.z);
        chipVault = new GameObject("ChipVault").transform; chipVault.SetParent(root, false); chipVault.position = ChipVaultPos;
        Mats.Prim(PrimitiveType.Cube, chipVault, new Vector3(0f, 0.7f, 0f), new Vector3(2.4f, 1.4f, 1.8f), steel, true);
        Mats.Prim(PrimitiveType.Cube, chipVault, new Vector3(0f, 0.7f, 0.91f), new Vector3(1.6f, 0.2f, 0.02f), yellow);
        chipVaultLid = Mats.Node(chipVault, "Lid", new Vector3(0f, 1.4f, 0f));
        Mats.Prim(PrimitiveType.Cube, chipVaultLid, new Vector3(0f, 0.15f, 0f), new Vector3(2.5f, 0.3f, 1.9f), dark);
        signParent = chipVault;
        StorySign(ChipVaultPos + new Vector3(0f, 2.6f, 1.6f), 0f, "CHIP VAULT\n<size=16>heavy lid - call two robots (phone: Come here)</size>", new Color(0.1f, 0.25f, 0.35f), 4.6f, 1.2f);
        signParent = null;
        var ct = new GameObject("ChipPickup").transform; ct.SetParent(root, false); ct.position = ChipVaultPos + Vector3.up * 2.0f;
        Mats.Prim(PrimitiveType.Cube, ct, Vector3.zero, new Vector3(0.7f, 0.12f, 0.7f), dark);
        Mats.Prim(PrimitiveType.Cube, ct, Vector3.up * 0.07f, new Vector3(0.4f, 0.04f, 0.4f), lightCyan);
        chipItem = Pickups.Add(ct, WorldId.Ranch, "st_chip", 2.2f);
        chipItem.taken = true; ct.gameObject.SetActive(false);
    }

    static Material orGlow;
    public static Material GlowMat() { return beamMatOr(); }
    static Material beamMatOr() { if (orGlow == null) { orGlow = new Material(Mats.Fx); orGlow.color = new Color(1f, 0.7f, 0.2f, 0.18f); } return orGlow; }

    public void ShowPickups(bool on, int partsDone)
    {
        if (engineCrate != null && engineCrate.parent == root) engineCrate.gameObject.SetActive(on && (partsDone & 1) == 0);
        engineSpot.gameObject.SetActive(on && (partsDone & 1) == 0);
        chipVault.gameObject.SetActive(on || (partsDone & 16) == 0);
        noseChest.gameObject.SetActive(on);
        for (int i = 0; i < 4; i++) simonGlow[i].transform.parent.gameObject.SetActive(true);
    }

    // world-space light beams at the current objective spots (ranch / house)
    public void Beams(List<Vector3> at, WorldId world)
    {
        for (int i = 0; i < beams.Count; i++)
        {
            bool on = i < at.Count;
            if (beams[i].gameObject.activeSelf != on) beams[i].gameObject.SetActive(on);
            if (on) beams[i].position = at[i] + Vector3.up * 17f;
        }
        float k = 0.18f + 0.06f * Mathf.Sin(Time.time * 3f);
        beamMat.color = new Color(1f, 0.92f, 0.45f, k);
    }

    // ---------------- weather ----------------
    public void Rain(List<Camera> cams, float k, Vector3 wind)
    {
        while (rain.Count < cams.Count) rain.Add(MakeRain());
        for (int i = 0; i < rain.Count; i++)
        {
            var ps = rain[i];
            bool on = i < cams.Count && k > 0.02f && cams[i] != null;
            var em = ps.emission;
            em.rateOverTime = on ? (Look.Mobile ? 260f : 700f) * k : 0f;
            if (!on) continue;
            Transform c = cams[i].transform;
            ps.transform.position = c.position + c.forward * 9f + Vector3.up * 9f - wind * 0.4f;
            var vel = ps.velocityOverLifetime; vel.enabled = true;
            vel.x = new ParticleSystem.MinMaxCurve(wind.x); vel.y = new ParticleSystem.MinMaxCurve(-2f); vel.z = new ParticleSystem.MinMaxCurve(wind.z);
        }
    }

    ParticleSystem MakeRain()
    {
        var go = new GameObject("StoryRain");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main;
        m.loop = true; m.playOnAwake = false; m.simulationSpace = ParticleSystemSimulationSpace.World;
        m.maxParticles = Look.Mobile ? 600 : 1500; m.startLifetime = 1.1f; m.startSpeed = 18f;
        m.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.06f);
        m.startColor = new Color(0.75f, 0.82f, 0.95f, 0.5f);
        m.gravityModifier = 0.6f;
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(36f, 36f, 1f);   // (rotated 90 about x: a flat 36 x 36 sheet emitting down)
        sh.rotation = new Vector3(90f, 0f, 0f);   // emit downwards
        var em = ps.emission; em.enabled = true; em.rateOverTime = 0f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = Mats.Fx; r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.06f; r.lengthScale = 2f;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        ps.Play();
        return ps;
    }

    void BuildBolt()
    {
        var go = new GameObject("StoryBolt");
        go.transform.SetParent(transform, false);
        bolt = go.AddComponent<LineRenderer>();
        bolt.positionCount = 12; bolt.widthMultiplier = 0.55f; bolt.sharedMaterial = Mats.Unlit(new Color(0.92f, 0.95f, 1f));
        bolt.enabled = false;
    }

    // a lightning strike at p (ground point): jagged bolt from the clouds, flash, thunder a moment later
    public void Strike(Vector3 p)
    {
        Vector3 top = p + new Vector3(Random.Range(-15f, 15f), 120f, Random.Range(-15f, 15f));
        for (int i = 0; i < bolt.positionCount; i++)
        {
            float t = i / (float)(bolt.positionCount - 1);
            Vector3 q = Vector3.Lerp(top, p, t);
            if (i > 0 && i < bolt.positionCount - 1) q += new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f));
            bolt.SetPosition(i, q);
        }
        bolt.enabled = true; boltT = 0.18f;
        Worlds.Flash = 1f;
        FX.Sparkle(p + Vector3.up * 0.5f, new Color(0.8f, 0.9f, 1f), 24);
        FX.Boom(p, 0.5f);
        if (Destruct.On) Destruct.Blast(p + Vector3.up * 0.5f, 5f, 45f, null);   // ffu21: the strike knocks trees / rocks / flags over (they grow back)
        Sfx.PlayAt(Sfx.Boom, p, 1f, 400f, 0.42f);
        StartCoroutine(Thunder(Sfx.Near(p)));
    }
    System.Collections.IEnumerator Thunder(float dist)
    {
        yield return new WaitForSeconds(Mathf.Clamp(dist / 340f, 0.15f, 1.2f));
        Sfx.Play(Sfx.Boom, 0.7f, 0.32f);
        Sfx.Play(Sfx.Thud != null ? Sfx.Thud : Sfx.Boom, 0.6f, 0.5f);
    }

    // storm debris: crates / barrels / hay that tumble towards the pad on the wind
    static readonly Color[] DebrisC = { new Color(0.68f, 0.5f, 0.28f), new Color(0.85f, 0.2f, 0.15f), new Color(0.85f, 0.75f, 0.35f) };
    public Rigidbody SpawnDebris(Vector3 from)
    {
        int k = Random.Range(0, 3);
        var go = Mats.Prim(k == 1 ? PrimitiveType.Cylinder : PrimitiveType.Cube, root, from, k == 1 ? new Vector3(0.9f, 0.6f, 0.9f) : k == 2 ? new Vector3(1.4f, 0.9f, 0.9f) : Vector3.one * 1.1f, Mats.Lit(DebrisC[k]), true);
        go.layer = Vehicle.PropLayer;
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 30f; rb.angularDrag = 0.3f;
        debris.Add(rb);
        return rb;
    }

    // the fence by the pad that the storm's lightning knocks apart (destruction)
    public void BuildStormFence()
    {
        if (stormFence != null) return;
        stormFence = new GameObject("StormFence").transform; stormFence.SetParent(root, false);
        Vector3 c = Pad + new Vector3(-15f, 0f, -6f);
        for (int i = 0; i < 6; i++)
        {
            Vector3 p = c + new Vector3(0f, 0f, i * 1.8f - 4.5f); p.y = GY(p.x, p.z);
            var post = Mats.Prim(PrimitiveType.Cube, stormFence, p + Vector3.up * 0.6f, new Vector3(0.18f, 1.2f, 0.18f), Mats.Lit(new Color(0.45f, 0.32f, 0.2f)), true);
            post.layer = Vehicle.PropLayer;
            if (i < 5) { var rail = Mats.Prim(PrimitiveType.Cube, stormFence, p + new Vector3(0f, 0.85f, 0.9f), new Vector3(0.08f, 0.14f, 1.8f), Mats.Lit(new Color(0.55f, 0.4f, 0.25f)), true); rail.layer = Vehicle.PropLayer; }
        }
    }

    public void SmashFence(Vector3 from)
    {
        if (stormFence == null) return;
        foreach (Transform t in stormFence)
        {
            if (t.GetComponent<Rigidbody>() != null) continue;
            var rb = t.gameObject.AddComponent<Rigidbody>(); rb.mass = 8f;
            rb.AddExplosionForce(900f, from, 14f, 1.2f, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 40f, ForceMode.Impulse);
        }
        FX.Boom(from, 0.8f);
        Game.Shake(from, 0.6f);
    }

    public void ClearStorm()
    {
        foreach (var d in debris) if (d != null) Destroy(d.gameObject);
        debris.Clear();
        foreach (var a in anchors) a.gameObject.SetActive(false);
        if (stormFence != null) { Destroy(stormFence.gameObject); stormFence = null; }
    }

    // ---------------- fireworks ----------------
    static readonly Color[] FwC = { new Color(1f, 0.3f, 0.3f), new Color(1f, 0.85f, 0.3f), new Color(0.4f, 1f, 0.5f), new Color(0.4f, 0.7f, 1f), new Color(1f, 0.5f, 1f), Color.white };
    public void Firework(Vector3 at)
    {
        Color c = FwC[Random.Range(0, FwC.Length)], c2 = FwC[Random.Range(0, FwC.Length)];
        int n = Look.Mobile ? 40 : 70;
        for (int i = 0; i < n; i++)
        {
            Vector3 d = Random.onUnitSphere;
            FX.Spray(at, d * Random.Range(9f, 13f), Random.Range(0.5f, 0.8f), Random.Range(1.1f, 1.7f), i % 3 == 0 ? c2 : c);
        }
        FX.Sparkle(at, Color.white, 10);
        Sfx.PlayAt(Sfx.Boom, at, 0.55f, 260f, Random.Range(1.3f, 1.7f));
    }

    // ---------------- Mars ----------------
    public void BuildMars()
    {
        if (marsRoot != null) return;
        marsRoot = new GameObject("StoryMars").transform;
        float x = -10f, z = -40f;
        MarsRocketPos = SurfaceWorlds.M(x, SurfaceWorlds.MarsY(x, z), z);
        marsRocket = BuildRocket(marsRoot, MarsRocketPos + Vector3.up * CradleH);
        marsRocket.Show(31, false, false, true, 0);
        for (int i = 0; i < 4; i++)
        {
            Quaternion q = Quaternion.Euler(0f, i * 90f, 0f);   // between the fins (45 deg)
            Vector3 o = q * Vector3.forward;
            Mats.Prim(PrimitiveType.Cube, marsRoot, MarsRocketPos + o * 2.4f + Vector3.up * 1.2f, new Vector3(0.3f, 2.6f, 0.3f), new Vector3(0f, q.eulerAngles.y, 18f), steel);
        }
        // rockfall across the cave mouth (A clears it, one boulder at a time)
        RockfallPos = SurfaceWorlds.M(0f, SurfaceWorlds.MarsY(0f, 33f), 33f);
        signParent = marsRoot;
        for (int i = 0; i < 3; i++)
        {
            float rx = -2.6f + i * 2.6f;
            var r = Mats.Prim(PrimitiveType.Sphere, marsRoot, SurfaceWorlds.M(rx, SurfaceWorlds.MarsY(rx, 36.5f) + 1.4f, 36.5f), new Vector3(2.9f, 3.2f, 2.6f), new Vector3(0f, i * 50f, 0f), Mats.Lit(new Color(0.48f, 0.26f, 0.16f)), true);
            rocks.Add(r.transform);
        }
        StorySign(SurfaceWorlds.M(4.5f, SurfaceWorlds.MarsY(4.5f, 31f) + 1.6f, 31f), 180f, "<size=22>PUPS INSIDE!</size>", new Color(0.45f, 0.15f, 0.08f), 2.6f, 0.8f);
        // the four Mars pups who called for help, huddled at the back of the cave (golden collars)
        Vector3[] spots = { new Vector3(-4f, 0f, 76f), new Vector3(-1.5f, 0f, 78f), new Vector3(1.5f, 0f, 78f), new Vector3(4f, 0f, 76f) };
        for (int i = 0; i < 4; i++)
        {
            Vector3 s = spots[i];
            var p = Animal.Dog("", SurfaceWorlds.M(s.x, SurfaceWorlds.MarsY(s.x, s.z), s.z), i % 2 == 0);
            p.transform.localScale = Vector3.one * 0.55f;
            p.transform.SetParent(marsRoot, true);
            p.Init(new Rect(Worlds.MarsO.x + s.x - 1.5f, Worlds.MarsO.z + s.z - 1.5f, 3f, 3f), 0f);
            p.skittish = false;
            p.groundFn = (gx, gz) => Worlds.MarsO.y + SurfaceWorlds.MarsY(gx - Worlds.MarsO.x, gz - Worlds.MarsO.z);
            if (p.head != null) Mats.Prim(PrimitiveType.Cylinder, p.head, new Vector3(0f, -0.12f, -0.1f), new Vector3(0.32f, 0.05f, 0.32f), yellow);
            pups.Add(p);
        }
        foreach (var rr in marsRoot.GetComponentsInChildren<Renderer>()) rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    public void MarsActive(bool on) { if (marsRoot != null && marsRoot.gameObject.activeSelf != on) marsRoot.gameObject.SetActive(on); }

    // the rescued pups at the ranch party
    public void HomePups(Vector3 c)
    {
        if (homePups.Count > 0) return;
        for (int i = 0; i < 4; i++)
        {
            Vector3 p = c + new Vector3(-3f + i * 2f, 0f, 2.5f); p.y = GY(p.x, p.z);
            var d = Animal.Dog("", p, i % 2 == 0);
            d.transform.localScale = Vector3.one * 0.55f;
            d.transform.SetParent(root, true);
            d.Init(new Rect(c.x - 7f, c.z - 3f, 14f, 9f), 0f);
            d.skittish = false;
            d.groundFn = (gx, gz) => Ranch.GY(gx, gz);
            if (d.head != null) Mats.Prim(PrimitiveType.Cylinder, d.head, new Vector3(0f, -0.12f, -0.1f), new Vector3(0.32f, 0.05f, 0.32f), yellow);
            homePups.Add(d);
        }
    }

    // ---------------- deep space diorama ----------------
    public void BuildSpace()
    {
        if (spaceRoot != null) return;
        spaceRoot = new GameObject("StorySpace").transform;
        spaceRoot.position = SpaceV;
        var mt = Resources.Load<Texture2D>("LB/space_mars");
        var et = Resources.Load<Texture2D>("LB/space_earth");
        var mm = mt != null ? Mats.Tex(mt, 0.05f) : Mats.Lit(new Color(0.75f, 0.38f, 0.2f));
        var em = et != null ? Mats.Tex(et, 0.3f) : Mats.Lit(new Color(0.2f, 0.4f, 0.85f));
        spaceMars = Mats.Prim(PrimitiveType.Sphere, spaceRoot, new Vector3(140f, -60f, 1500f), Vector3.one * 700f, mm).transform;
        spaceEarth = Mats.Prim(PrimitiveType.Sphere, spaceRoot, new Vector3(-260f, 80f, -900f), Vector3.one * 420f, em).transform;
        var atm = new Material(Mats.Glass); atm.color = new Color(0.55f, 0.75f, 1f, 0.22f);
        Mats.Prim(PrimitiveType.Sphere, spaceEarth, Vector3.zero, Vector3.one * 1.035f, atm);
        var st = Resources.Load<Texture2D>("LB/space_stars");
        if (st != null && Mats.UnlitTexBase != null)
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Mesh src = tmp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmp);
            var inv = new Mesh { name = "StoryStars" };
            var vv = src.vertices; for (int i = 0; i < vv.Length; i++) vv[i] *= 16000f;
            var tt = src.triangles; for (int i = 0; i < tt.Length; i += 3) { int a = tt[i + 1]; tt[i + 1] = tt[i + 2]; tt[i + 2] = a; }
            inv.vertices = vv; inv.uv = src.uv; inv.triangles = tt;
            var sg = new GameObject("Stars"); sg.transform.SetParent(spaceRoot, false);
            sg.AddComponent<MeshFilter>().sharedMesh = inv;
            var sr = sg.AddComponent<MeshRenderer>(); sr.sharedMaterial = Mats.UnlitTex(st);
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; sr.receiveShadows = false;
            spaceStars = sg.transform;
        }
        var rk = BuildRocket(spaceRoot, SpaceV, 1f);
        rk.Show(31, false, false, true, 0);
        spaceRocket = rk.root;
        spaceRocketRig = rk;
        foreach (var rr in spaceRoot.GetComponentsInChildren<Renderer>()) rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        spaceRoot.gameObject.SetActive(false);
    }
    public Rocket spaceRocketRig;

    // ---------------- per frame ----------------
    void Update()
    {
        if (boltT > 0f) { boltT -= Time.deltaTime; if (boltT <= 0f) bolt.enabled = false; }
        if (Worlds.Flash > 0f) Worlds.Flash = Mathf.MoveTowards(Worlds.Flash, 0f, Time.deltaTime * 4f);
        if (rocket != null && rocket.chip.activeSelf)
            for (int i = 0; i < rocket.chipLights.Count; i++) rocket.chipLights[i].enabled = Mathf.Repeat(Time.time * 3f + i * 0.37f, 1f) < 0.6f;
    }
}
