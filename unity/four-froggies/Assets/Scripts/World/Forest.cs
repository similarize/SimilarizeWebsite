using System.Collections.Generic;
using UnityEngine;

// ffu33 (Bill: "a forested area where you can go in under cover of the canopy and find glowing orbs that you collect"):
// the Enchanted Forest in the ranch's north-east corner (JetLayout.ForestMin..Max, behind the track's east lobe).
//  * tall trees (the ranch's Quaternius packs at ~2x, trunk colliders) packed tight, plus a closed canopy of alpha-cut leaf
//    cards at 11-17 m that casts real (dappled) sun shadows, with a few gaps where light shafts come down;
//  * ferns, mushrooms (red caps + glowing blue ones), mossy fallen logs (colliders, hop over them), fog wisps + fireflies
//    (two particle systems), an entrance sign;
//  * under the canopy the camera gets darker, greener ambient + a little haze (Camera.onPreCull, after Worlds.PreCull);
//  * fewer trees / cards / ferns on phones (Look.Mobile). Static parts are merged (MeshMerge). Not destructible.
// The orbs themselves live in Orbs.cs.
public static class Forest
{
    public static Transform Root;
    public static readonly List<Vector3> TreeBases = new List<Vector3>();
    public static readonly List<Vector3> Gaps = new List<Vector3>();   // canopy gaps (light shafts) - x, z, radius in y
    public const float CanopyLo = 11f, CanopyHi = 17f;
    static bool hooked;

    static float GY(float x, float z) { return Ranch.GY(x, z); }

    public static bool UnderCanopy(Vector3 p)
    {
        if (!JetLayout.InForest(p.x, p.z, -1.5f)) return false;
        return p.y < GY(p.x, p.z) + CanopyLo + 1f;
    }

    // 0..1: how deep inside the forest a point is (eased over the outer 6 m)
    public static float Inside(Vector3 p)
    {
        Vector2 a = JetLayout.ForestMin, b = JetLayout.ForestMax;
        float d = Mathf.Min(Mathf.Min(p.x - a.x, b.x - p.x), Mathf.Min(p.z - a.y, b.y - p.z));
        if (d <= 0f) return 0f;
        float k = Mathf.Clamp01(d / 6f);
        float h = p.y - GY(p.x, p.z);
        k *= Mathf.Clamp01((CanopyLo + 2f - h) / 4f);
        return k * k * (3f - 2f * k);
    }

    public static void Build()
    {
        if (Root != null) return;
        float t0 = Time.realtimeSinceStartup;
        Root = new GameObject("Enchanted Forest").transform;
        var r = new System.Random(3303);
        System.Func<float> R = () => (float)r.NextDouble();
        Vector2 mn = JetLayout.ForestMin, mx = JetLayout.ForestMax;
        bool mob = Look.Mobile;

        // canopy gaps first (shafts), so trees and cards keep clear of them
        int nGaps = mob ? 5 : 9;
        for (int i = 0; i < nGaps; i++)
        {
            float x = Mathf.Lerp(mn.x + 6f, mx.x - 6f, R()), z = Mathf.Lerp(mn.y + 8f, mx.y - 8f, R());
            Gaps.Add(new Vector3(x, 2.2f + R() * 1.2f, z));
        }

        // ---- trees ----
        string[] broad = { "tree0", "tree1", "tree2" }, pines = { "pine0", "pine1" };
        Transform staticRoot = new GameObject("Forest static").transform; staticRoot.SetParent(Root, false);
        int want = mob ? 46 : 92, placed = 0;
        for (int tries = 0; tries < 2500 && placed < want; tries++)
        {
            // denser along the edges (hides the canopy rim), everywhere else a loose grid
            float x = Mathf.Lerp(mn.x + 1.5f, mx.x - 1.5f, R()), z = Mathf.Lerp(mn.y + 1.5f, mx.y - 1.5f, R());
            bool ok = true;
            foreach (Vector3 b in TreeBases) if ((new Vector2(b.x - x, b.z - z)).sqrMagnitude < (mob ? 36f : 22f)) { ok = false; break; }
            if (!ok) continue;
            foreach (Vector3 g in Gaps) if ((new Vector2(g.x - x, g.z - z)).sqrMagnitude < (g.y + 1.2f) * (g.y + 1.2f)) { ok = false; break; }
            if (!ok) continue;
            if (Layout.RoadDist(x, z) < Layout.TrackW * 0.5f + 5f) continue;
            float gy = GY(x, z);
            bool pine = R() < 0.35f;
            string pk = pine ? pines[r.Next(pines.Length)] : broad[r.Next(broad.Length)];
            LBPack p = LBPack.Get(pk);
            float s = 1.8f + R() * 0.8f;
            float yaw = R() * 360f;
            if (p != null) p.SpawnAll(staticRoot, new Vector3(x, gy - 0.2f, z), s, new Color(0.82f, 0.9f, 0.8f), yaw);
            else
            {
                Mats.Prim(PrimitiveType.Cylinder, staticRoot, new Vector3(x, gy + 3.5f * s, z), new Vector3(0.6f * s, 3.5f * s, 0.6f * s), Mats.Lit(new Color(0.3f, 0.22f, 0.15f)), false);
                Mats.Prim(PrimitiveType.Sphere, staticRoot, new Vector3(x, gy + 8f * s, z), new Vector3(5f, 4f, 5f) * s, Mats.Lit(new Color(0.15f, 0.3f, 0.12f)), false);
            }
            var col = new GameObject("ForestTrunk").AddComponent<CapsuleCollider>();
            col.transform.SetParent(Root, false);
            col.transform.position = new Vector3(x, gy + 2.5f * s, z);
            col.radius = 0.32f * s; col.height = 5f * s;
            TreeBases.Add(new Vector3(x, gy, z));
            placed++;
        }

        // ---- canopy: leaf cards ----
        Texture leafTex = Resources.Load<Texture2D>("LB/q_bushleaf");
        if (leafTex == null) leafTex = Resources.Load<Texture2D>("LB/q_leaves");
        BuildCanopy(R, mob, leafTex);

        // ---- ferns ----
        BuildFerns(R, mob ? 70 : 150);

        // ---- mushrooms (red caps with white spots; blue glowing ones in clusters) ----
        Material stem = Mats.Lit(new Color(0.88f, 0.84f, 0.74f));
        Material capR = Mats.Paint(new Color(0.82f, 0.12f, 0.08f), 0.55f);
        Material dot = Mats.Lit(new Color(0.96f, 0.95f, 0.9f));
        Material glowCap = new Material(Mats.Unlit(Color.white)) { name = "Forest glow cap" };
        glowCap.color = new Color(0.35f, 0.95f, 1.6f);
        Material glowStem = Mats.Unlit(new Color(0.55f, 0.85f, 0.95f));
        int nMush = mob ? 26 : 50;
        for (int i = 0; i < nMush; i++)
        {
            float x = Mathf.Lerp(mn.x + 3f, mx.x - 3f, R()), z = Mathf.Lerp(mn.y + 3f, mx.y - 3f, R());
            bool glow = i % 3 == 0;
            int n = glow ? 3 + r.Next(3) : 1 + r.Next(2);
            for (int k = 0; k < n; k++)
            {
                float mx2 = x + (R() - 0.5f) * 1.2f, mz2 = z + (R() - 0.5f) * 1.2f;
                float gy = GY(mx2, mz2), s = (glow ? 0.18f : 0.3f) + R() * (glow ? 0.18f : 0.25f);
                Mats.Prim(PrimitiveType.Cylinder, staticRoot, new Vector3(mx2, gy + s * 0.7f, mz2), new Vector3(s * 0.32f, s * 0.7f, s * 0.32f), glow ? glowStem : stem, false);
                Mats.Prim(PrimitiveType.Sphere, staticRoot, new Vector3(mx2, gy + s * 1.4f, mz2), new Vector3(s * 1.5f, s * 0.75f, s * 1.5f), glow ? glowCap : capR, false);
                if (!glow)
                    for (int d = 0; d < 4; d++)
                    {
                        float a = d * 1.57f + R();
                        Mats.Prim(PrimitiveType.Sphere, staticRoot, new Vector3(mx2 + Mathf.Cos(a) * s * 0.45f, gy + s * 1.62f, mz2 + Mathf.Sin(a) * s * 0.45f), Vector3.one * s * 0.18f, dot, false);
                    }
            }
        }
        // a fairy ring of glowing mushrooms deep in the north-east (the big golden orb floats in it)
        Vector3 ring = FairyRing;
        for (int i = 0; i < 14; i++)
        {
            float a = i / 14f * Mathf.PI * 2f, rr = 3.2f + (R() - 0.5f) * 0.4f;
            float x = ring.x + Mathf.Cos(a) * rr, z = ring.z + Mathf.Sin(a) * rr, gy = GY(x, z), s = 0.28f + R() * 0.12f;
            Mats.Prim(PrimitiveType.Cylinder, staticRoot, new Vector3(x, gy + s * 0.7f, z), new Vector3(s * 0.32f, s * 0.7f, s * 0.32f), glowStem, false);
            Mats.Prim(PrimitiveType.Sphere, staticRoot, new Vector3(x, gy + s * 1.4f, z), new Vector3(s * 1.5f, s * 0.75f, s * 1.5f), glowCap, false);
        }

        // ---- mossy fallen logs (colliders: hop over them) ----
        Material bark = Mats.TexTint("LB/q_bark", new Color(0.62f, 0.55f, 0.46f), 0.08f, 1.6f);
        Material moss = Mats.Lit(new Color(0.24f, 0.42f, 0.14f));
        Material endWood = Mats.Lit(new Color(0.62f, 0.48f, 0.3f));
        int nLogs = mob ? 6 : 10;
        for (int i = 0; i < nLogs; i++)
        {
            float x = Mathf.Lerp(mn.x + 6f, mx.x - 6f, R()), z = Mathf.Lerp(mn.y + 8f, mx.y - 8f, R());
            float len = 5f + R() * 5f, rad = 0.35f + R() * 0.3f, yaw = R() * 180f;
            float gy = GY(x, z);
            var lg = Mats.Prim(PrimitiveType.Cylinder, Root, new Vector3(x, gy + rad * 0.8f, z), new Vector3(rad * 2f, len * 0.5f, rad * 2f), new Vector3(90f, yaw, 0f), bark, true);
            Vector3 along = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Mats.Prim(PrimitiveType.Cylinder, staticRoot, new Vector3(x, gy + rad * 1.55f, z), new Vector3(rad * 1.5f, len * 0.42f, rad * 0.5f), new Vector3(90f, yaw, 0f), moss, false);
            for (int e = -1; e <= 1; e += 2)
                Mats.Prim(PrimitiveType.Cylinder, staticRoot, new Vector3(x, gy + rad * 0.8f, z) + along * (len * 0.5f + 0.01f) * e, new Vector3(rad * 1.86f, 0.02f, rad * 1.86f), new Vector3(90f, yaw, 0f), endWood, false);
        }

        // ---- light shafts at the canopy gaps ----
        BuildShafts();

        // ---- entrance sign (south-west corner, facing the track) ----
        Vector3 sp = new Vector3(mn.x + 3f, 0f, mn.y - 1.5f);
        try { Ranch.Sign(new Vector3(sp.x, GY(sp.x, sp.z) + 2.3f, sp.z), 225f, "ENCHANTED FOREST\nfind the glowing orbs!", new Color(0.12f, 0.3f, 0.16f), 4.2f, 1.5f); } catch (System.Exception e) { Debug.Log("Forest: sign failed " + e.Message); }

        MeshMerge.Merge(staticRoot, true);
        Particles(mob);
        if (!hooked) { hooked = true; Camera.onPreCull += PreCull; }
        Debug.Log("Forest: built in " + ((Time.realtimeSinceStartup - t0) * 1000f).ToString("0") + " ms, " + placed + " trees, " + Gaps.Count + " shafts" + (mob ? " (mobile)" : ""));
    }

    public static Vector3 FairyRing { get { float x = JetLayout.ForestMax.x - 11f, z = JetLayout.ForestMax.y - 13f; return new Vector3(x, GY(x, z), z); } }

    static bool InGap(float x, float z, float pad)
    {
        foreach (Vector3 g in Gaps) if ((new Vector2(g.x - x, g.z - z)).sqrMagnitude < (g.y + pad) * (g.y + pad)) return true;
        return false;
    }

    static void BuildCanopy(System.Func<float> R, bool mob, Texture tex)
    {
        Vector2 mn = JetLayout.ForestMin, mx = JetLayout.ForestMax;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var nrm = new List<Vector3>(); var tri = new List<int>();
        float step = mob ? 3.6f : 2.8f;
        int layers = mob ? 1 : 2;
        for (int L = 0; L < layers; L++)
            for (float z = mn.y + 1f; z < mx.y - 1f; z += step)
                for (float x = mn.x + 1f; x < mx.x - 1f; x += step)
                {
                    float cx = x + (R() - 0.5f) * step, cz = z + (R() - 0.5f) * step;
                    if (InGap(cx, cz, 0.6f)) continue;
                    float gy = GY(cx, cz);
                    float h = Mathf.Lerp(CanopyLo, CanopyHi, R()) + L * 1.5f;
                    float size = (mob ? 6.2f : 5f) + R() * 2f;
                    Quaternion q = Quaternion.Euler((R() - 0.5f) * 50f, R() * 360f, (R() - 0.5f) * 50f);
                    Vector3 c = new Vector3(cx, gy + h, cz);
                    Vector3 a = q * new Vector3(size * 0.5f, 0f, 0f), b = q * new Vector3(0f, 0f, size * 0.5f);
                    int i0 = v.Count;
                    v.Add(c - a - b); v.Add(c + a - b); v.Add(c + a + b); v.Add(c - a + b);
                    uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(1f, 0f)); uv.Add(new Vector2(1f, 1f)); uv.Add(new Vector2(0f, 1f));
                    Vector3 n = q * Vector3.up; if (n.y < 0f) n = -n;
                    for (int k = 0; k < 4; k++) nrm.Add(Vector3.Slerp(n, Vector3.up, 0.5f));
                    tri.Add(i0); tri.Add(i0 + 2); tri.Add(i0 + 1); tri.Add(i0); tri.Add(i0 + 3); tri.Add(i0 + 2);
                    // FF/Foliage is double-sided; the Standard fallback is not, so add the back faces there
                    if (Mats.FoliageBase == null) { tri.Add(i0); tri.Add(i0 + 1); tri.Add(i0 + 2); tri.Add(i0); tri.Add(i0 + 2); tri.Add(i0 + 3); }
                }
        var mesh = new Mesh { name = "Forest canopy" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetNormals(nrm); mesh.SetTriangles(tri, 0);
        mesh.RecalculateBounds();
        var go = new GameObject("Forest canopy");
        go.transform.SetParent(Root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        Material m = tex != null ? Mats.Foliage(tex, new Color(0.3f, 0.5f, 0.2f)) : Mats.Lit(new Color(0.16f, 0.32f, 0.12f));
        mr.sharedMaterial = m;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        mr.receiveShadows = false;
    }

    // fern: 7 arching fronds (tapered strips, both faces), merged into one mesh per colour
    static void BuildFerns(System.Func<float> R, int n)
    {
        Vector2 mn = JetLayout.ForestMin, mx = JetLayout.ForestMax;
        var sets = new[] { (new List<Vector3>(), new List<int>()), (new List<Vector3>(), new List<int>()) };
        for (int i = 0; i < n; i++)
        {
            float x = Mathf.Lerp(mn.x + 1f, mx.x - 1f, R()), z = Mathf.Lerp(mn.y + 1f, mx.y - 1f, R());
            Vector3 c = new Vector3(x, GY(x, z) - 0.05f, z);
            float s = 0.6f + R() * 0.7f;
            var set = sets[i % 2];
            int fronds = 6 + (int)(R() * 3f);
            for (int f = 0; f < fronds; f++)
            {
                float a = f / (float)fronds * Mathf.PI * 2f + R() * 0.5f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), side = new Vector3(-dir.z, 0f, dir.x);
                float lift = 0.9f + R() * 0.5f;
                int b0 = set.Item1.Count;
                const int seg = 5;
                for (int k = 0; k <= seg; k++)
                {
                    float u = k / (float)seg;
                    Vector3 p = c + (dir * u * 1.1f + Vector3.up * (Mathf.Sin(u * 2.6f) * 0.55f * lift)) * s;
                    float w = Mathf.Sin(Mathf.Clamp01(u * 1.15f) * Mathf.PI) * 0.17f * s + 0.01f;
                    set.Item1.Add(p - side * w); set.Item1.Add(p + side * w);
                }
                for (int k = 0; k < seg; k++)
                {
                    int q = b0 + k * 2;
                    set.Item2.AddRange(new[] { q, q + 2, q + 1, q + 1, q + 2, q + 3, q, q + 1, q + 2, q + 1, q + 3, q + 2 });
                }
            }
        }
        Color[] cols = { new Color(0.2f, 0.42f, 0.13f), new Color(0.3f, 0.52f, 0.17f) };
        for (int k = 0; k < 2; k++)
        {
            var mesh = new Mesh { name = "Ferns " + k };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(sets[k].Item1); mesh.SetTriangles(sets[k].Item2, 0);
            // both faces share the vertices, so RecalculateNormals sums to ~zero (NaN shading: black fronds + bloom halos in
            // ffu33a) - give every vertex an up-ish normal instead (soft, evenly lit fronds from either side)
            var fn = new Vector3[sets[k].Item1.Count];
            for (int v = 0; v < fn.Length; v++) fn[v] = Vector3.up;
            mesh.normals = fn; mesh.RecalculateBounds();
            var go = new GameObject("Ferns " + k);
            go.transform.SetParent(Root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Lit(cols[k]);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    // soft vertical light shafts from each canopy gap down to the ground along the sun direction (two crossed quads)
    static void BuildShafts()
    {
        const int W = 32, H = 64;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W * 2f - 1f, vv = (y + 0.5f) / H;
                float a = Mathf.Pow(Mathf.Clamp01(1f - u * u), 1.6f) * Mathf.SmoothStep(0f, 1f, vv) * Mathf.SmoothStep(0f, 1f, (1f - vv) * 6f);
                px[y * W + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        var m = new Material(Mats.Fx) { name = "Forest light shaft" };
        m.mainTexture = tex;
        m.color = new Color(1f, 0.93f, 0.7f, 0.2f);
        Vector3 sunDir = RenderSettings.sun != null ? RenderSettings.sun.transform.forward : new Vector3(0.3f, -0.85f, 0.4f).normalized;
        if (sunDir.y > -0.3f) sunDir = new Vector3(sunDir.x, -0.3f, sunDir.z).normalized;
        foreach (Vector3 g in Gaps)
        {
            float gy = GY(g.x, g.z);
            Vector3 top = new Vector3(g.x, gy + CanopyLo + 2.5f, g.z);
            float len = (CanopyLo + 2.5f) / -sunDir.y;
            Vector3 bot = top + sunDir * len;
            Vector3 mid = (top + bot) * 0.5f;
            var root = new GameObject("Shaft").transform;
            root.SetParent(Root, false);
            root.position = mid;
            root.rotation = Quaternion.LookRotation(Vector3.Cross(sunDir, Vector3.right).normalized, -sunDir);
            for (int k = 0; k < 2; k++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.Destroy(q.GetComponent<Collider>());
                q.transform.SetParent(root, false);
                q.transform.localRotation = Quaternion.Euler(0f, k * 90f, 0f);
                q.transform.localScale = new Vector3(g.y * 1.4f, len, 1f);
                var mr = q.GetComponent<MeshRenderer>();
                mr.sharedMaterial = m;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            // a sunlit patch on the forest floor where the shaft lands
            var patch = Mats.Prim(PrimitiveType.Cylinder, Root, new Vector3(bot.x, GY(bot.x, bot.z) + 0.04f, bot.z), new Vector3(g.y * 1.6f, 0.005f, g.y * 1.6f), FX.SoftMat(), false);
            patch.GetComponent<MeshRenderer>().sharedMaterial = PatchMat();
            Mats.NoShadows(patch);
        }
    }
    static Material patchMat;
    static Material PatchMat()
    {
        if (patchMat != null) return patchMat;
        patchMat = new Material(FX.SoftMat()) { name = "Forest sun patch" };
        patchMat.color = new Color(1f, 0.92f, 0.6f, 0.28f);
        return patchMat;
    }

    static void Particles(bool mob)
    {
        Vector2 mn = JetLayout.ForestMin, mx = JetLayout.ForestMax;
        Vector3 c = new Vector3((mn.x + mx.x) * 0.5f, GY((mn.x + mx.x) * 0.5f, (mn.y + mx.y) * 0.5f), (mn.y + mx.y) * 0.5f);
        Vector3 size = new Vector3(mx.x - mn.x - 4f, 1f, mx.y - mn.y - 4f);
        // fireflies: tiny bright yellow-green motes that wander and blink
        var ff = new GameObject("Fireflies").AddComponent<ParticleSystem>();
        ff.transform.SetParent(Root, false);
        ff.transform.position = c + Vector3.up * 1.6f;
        ff.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var mainF = ff.main;
        mainF.loop = true; mainF.duration = 5f; mainF.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
        mainF.startSpeed = 0.15f; mainF.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.14f);
        mainF.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 1f, 0.35f, 1f), new Color(0.55f, 1f, 0.55f, 1f));
        mainF.maxParticles = mob ? 60 : 140; mainF.simulationSpace = ParticleSystemSimulationSpace.World; mainF.gravityModifier = 0f;
        mainF.prewarm = true;
        var emF = ff.emission; emF.rateOverTime = mob ? 10f : 22f;
        var shF = ff.shape; shF.shapeType = ParticleSystemShapeType.Box; shF.scale = new Vector3(size.x, 2.4f, size.z);
        var noF = ff.noise; noF.enabled = true; noF.strength = 0.6f; noF.frequency = 0.25f; noF.scrollSpeed = 0.2f;
        var colF = ff.colorOverLifetime; colF.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.2f, 0.35f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0.3f, 0.75f), new GradientAlphaKey(0f, 1f) });
        colF.color = grad;
        var rF = ff.GetComponent<ParticleSystemRenderer>();
        var fm = new Material(FX.SoftMat()) { name = "Firefly" };
        fm.color = new Color(1.6f, 1.8f, 0.8f, 1f);
        rF.sharedMaterial = fm; rF.maxParticleSize = 0.05f;
        ff.Play();

        // fog wisps: big, faint, slow puffs hugging the forest floor
        var fw = new GameObject("Fog wisps").AddComponent<ParticleSystem>();
        fw.transform.SetParent(Root, false);
        fw.transform.position = c + Vector3.up * 0.9f;
        fw.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var mainW = fw.main;
        mainW.loop = true; mainW.duration = 10f; mainW.startLifetime = new ParticleSystem.MinMaxCurve(14f, 22f);
        mainW.startSpeed = 0.2f; mainW.startSize = new ParticleSystem.MinMaxCurve(5f, 9f);
        mainW.startColor = new Color(0.82f, 0.92f, 0.86f, 0.13f);
        mainW.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        mainW.maxParticles = mob ? 14 : 34; mainW.simulationSpace = ParticleSystemSimulationSpace.World; mainW.prewarm = true;
        var emW = fw.emission; emW.rateOverTime = mob ? 0.8f : 1.8f;
        var shW = fw.shape; shW.shapeType = ParticleSystemShapeType.Box; shW.scale = new Vector3(size.x, 0.8f, size.z);
        var colW = fw.colorOverLifetime; colW.enabled = true;
        var gw = new Gradient();
        gw.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                   new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        colW.color = gw;
        var rotW = fw.rotationOverLifetime; rotW.enabled = true; rotW.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
        var rW = fw.GetComponent<ParticleSystemRenderer>();
        rW.sharedMaterial = FX.SoftMat(); rW.maxParticleSize = 2f;
        rW.sortingFudge = 10f;
        fw.Play();
    }

    // under the canopy: darker, greener ambient + a little green haze (after Worlds.PreCull set the ranch values)
    static void PreCull(Camera c)
    {
        if (Root == null || c == null || c.cullingMask == (1 << 20)) return;
        if (Game.I == null) return;
        float k = Inside(c.transform.position);
        if (k <= 0.001f || !RenderSettings.fog && RenderSettings.fogDensity <= 0f) return;
        if (c.transform.position.y < -50f) return;
        if (RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Skybox)
        {
            // realism (default since ffu23): SH ambient from the HDRI - scale it down (Worlds re-sets it per camera)
            var sh = RenderSettings.ambientProbe;
            sh *= Mathf.Lerp(1f, 0.42f, k);
            RenderSettings.ambientProbe = sh;
        }
        RenderSettings.ambientSkyColor = Color.Lerp(RenderSettings.ambientSkyColor, new Color(0.2f, 0.3f, 0.24f), k * 0.75f);
        RenderSettings.ambientEquatorColor = Color.Lerp(RenderSettings.ambientEquatorColor, new Color(0.13f, 0.2f, 0.13f), k * 0.75f);
        RenderSettings.ambientGroundColor = Color.Lerp(RenderSettings.ambientGroundColor, new Color(0.06f, 0.09f, 0.05f), k * 0.75f);
        RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, new Color(0.2f, 0.27f, 0.22f), k * 0.8f);
        RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, Mathf.Max(RenderSettings.fogDensity, 0.018f), k);
    }
}
