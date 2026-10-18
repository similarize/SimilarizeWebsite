using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// ffu56 TERRAIN / GRASS / TREES / BUILDINGS (Bill: realistic ground, grass that sways and bends, better bark + leaves with wind,
// real building materials; WebGL + phone friendly). Runs after Realism.Ready (the ffu23 scans), only when Realism is on;
// ?terrain=old (or 0 / off) = the ffu55 look exactly (before shots). Everything streams from real/ (CC0 ambientCG scans, see
// web/LICENSES.txt section 14) - the Unity build does not grow.
//  * GROUND: the ffu23 grass / dirt / shore / mud blend + worn PATHS (house -> garage, house -> pond, garage -> trailer barn,
//    meadow -> forest -> tree fort) and FOREST LEAF LITTER (desktop: 2 extra terrain layers; phones: painted into the dirt
//    layer), wider wet MUD patches round the pond rim (footprints stay there longest, Tracks56).
//  * GRASS (Grass56): GPU-instanced 3-card clumps across the whole ranch lawn, streamed in 10 m cells round each ranch camera,
//    FF/Grass56 sways in the wind and BENDS / flattens under frogs, robots, animals and vehicles (8 nearest movers).
//  * TREES: Bark014 on every trunk (the shared triplanar bark material), leafy sprig cards (Leaf001) scattered over each
//    ranch tree canopy, swaying with FF/Foliage wind; canopies a touch darker inside for depth.
//  * BUILDINGS: painted wood siding (house + garage walls), roof shingles, plank porch, red plank walls on the trailer barn
//    (triplanar FF/RealTri, so merged meshes need no UVs).
// Phones (Realism.Lite): 512 px scans, 4 terrain layers, grass radius 26 m at lower density, fewer leaf cards; the Realism
// frame-rate guard also sheds the grass + leaf cards.
public static class Terrain56
{
    public static bool On { get; private set; }
    public static bool Lite { get; private set; }
    public static bool Ready { get; private set; }
    public static Material GrassBase;      // editor-made FF/Grass56 (instancing on) - Bootstrap
    public static int LeafCards, BuildingSwaps, Layers;
    static Terrain terr;
    static MonoBehaviour host;

    public static void Begin(MonoBehaviour h, Terrain t)
    {
        string v = Ffu33.Url("terrain");
        bool old = v != null && (v == "old" || v == "0" || v == "off" || v == "false");
        On = Realism.On && !old;
        Debug.Log("TERRAIN56 " + (On ? "on" : old ? "off (?terrain=old)" : "off (realism off)"));
        if (!On || h == null) return;
        host = h; terr = t; Lite = Realism.Lite;
        h.StartCoroutine(Run());
    }

    static IEnumerator Run()
    {
        while (!Realism.Ready) yield return null;
        float t0 = Time.realtimeSinceStartup;
        string sfx = Lite ? "_512" : "";
        foreach (var s in new[] { "path", "litter", "bark2", "planks", "shingle", "siding" })
        {
            yield return Realism.LoadTex(s + "_a" + sfx + ".jpg", s + "_a", true, true);
            yield return Realism.LoadTex(s + "_n" + sfx + ".jpg", s + "_n", true, !Lite);
        }
        yield return Realism.LoadTex("leafcard.png", "leafcard", true, false);
        try { ApplyTerrain(); } catch (System.Exception e) { Debug.LogWarning("TERRAIN56 terrain " + e); }
        try { Trees(); } catch (System.Exception e) { Debug.LogWarning("TERRAIN56 trees " + e); }
        try { Buildings(); } catch (System.Exception e) { Debug.LogWarning("TERRAIN56 buildings " + e); }
        try { Grass56.Create(); } catch (System.Exception e) { Debug.LogWarning("TERRAIN56 grass " + e); }
        Ready = true;
        Look.RerenderProbe(Lite ? 64 : 256);
        Debug.Log("TERRAIN56 ready in " + (Time.realtimeSinceStartup - t0).ToString("0.0") + " s: layers " + Layers + ", leaf cards " + LeafCards + ", building swaps " + BuildingSwaps + ", grass " + (Grass56.I != null ? Grass56.I.Info : "off"));
    }

    // ---------------------------------------------------------------- shared helpers
    // every tree on the ranch (decorative + climbable + forest + treehouse trunks): demo spot pickers use this (ffu56 fix)
    public static bool NearTree(float x, float z, float r)
    {
        float r2 = r * r;
        foreach (var s in Ranch.TreeSpots) if ((s.x - x) * (s.x - x) + (s.y - z) * (s.y - z) < r2) return true;
        foreach (var s in Forest.TreeBases) if ((s.x - x) * (s.x - x) + (s.z - z) * (s.z - z) < r2) return true;
        foreach (var s in Treehouse.Spots) if ((s.x - x) * (s.x - x) + (s.z - z) * (s.z - z) < r2) return true;
        foreach (var t in TreeClimb.Trees) if (t != null && (t.basePos.x - x) * (t.basePos.x - x) + (t.basePos.z - z) * (t.basePos.z - z) < r2) return true;
        return false;
    }

    public static WorldId WorldAt(Vector3 p)
    {
        if (Mathf.Abs(p.x) < Layout.Half + 5f && Mathf.Abs(p.z) < Layout.Half + 5f && p.y > Layout.TerrainY - 5f && p.y < 400f) return WorldId.Ranch;
        if ((new Vector2(p.x, p.z) - new Vector2(Worlds.MarsO.x, Worlds.MarsO.z)).sqrMagnitude < 700f * 700f) return WorldId.Mars;
        if ((new Vector2(p.x, p.z) - new Vector2(Worlds.CallistoO.x, Worlds.CallistoO.z)).sqrMagnitude < 700f * 700f) return WorldId.Callisto;
        if ((new Vector2(p.x, p.z) - new Vector2(Worlds.MoonO.x, Worlds.MoonO.z)).sqrMagnitude < 700f * 700f) return WorldId.Moon;
        return WorldId.Space;
    }

    // ---------------------------------------------------------------- paths
    static readonly List<Vector2> pa = new List<Vector2>(), pb = new List<Vector2>();
    static void Seg(Vector2 a, Vector2 b) { pa.Add(a); pb.Add(b); }
    static void Poly(params Vector2[] p) { for (int i = 0; i + 1 < p.Length; i++) Seg(p[i], p[i + 1]); }
    public static Vector2 FortSpot = new Vector2(141f, 96f);
    static void BuildPaths()
    {
        pa.Clear(); pb.Clear();
        Vector2 hc = Layout.HouseC; float hz1 = hc.y + Layout.HouseSize.y * 0.5f, hx1 = hc.x + Layout.HouseSize.x * 0.5f;
        Vector2 porch = new Vector2(hc.x, hz1 + 7.6f);
        // porch -> garage front (the yard path), porch -> lawn
        Poly(porch, new Vector2(hc.x + 8f, hz1 + 13f), new Vector2(-24f, 34.5f), new Vector2(-14f, 36f));
        // house east side -> down to the pond's west shore
        Poly(new Vector2(hx1 + 6f, hc.y - 6f), new Vector2(-10f, -24f), new Vector2(-8f, -46f), new Vector2(-6f, -64f));
        // garage east end -> trailer barn (if built)
        Vector3 tb = TrailerBarn.Centre;
        if (tb.sqrMagnitude > 1f) Poly(new Vector2(Layout.GarageC.x + Layout.GarageSize.x * 0.5f + 3f, Layout.GarageC.y + 4f), new Vector2((Layout.GarageC.x + tb.x) * 0.5f + 6f, (Layout.GarageC.y + tb.z) * 0.5f + 3f), new Vector2(tb.x, tb.z));
        // meadow -> forest edge -> the tree fort
        Poly(new Vector2(104f, 62f), new Vector2(122f, 78f), new Vector2(FortSpot.x - 6f, FortSpot.y - 6f), FortSpot);
    }
    static float PathDist(float x, float z)
    {
        Vector2 p = new Vector2(x, z); float best = 1e9f;
        for (int i = 0; i < pa.Count; i++)
        {
            Vector2 a = pa[i], ab = pb[i] - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
            float d = (a + ab * t - p).sqrMagnitude; if (d < best) best = d;
        }
        return Mathf.Sqrt(best);
    }

    // surface weights at a ranch point: grass, dirt, shore, mud, path, litter (sum 1). Same numbers as the alphamap.
    public struct W { public float g, d, s, m, p, l; }
    public static W Weights(float wx, float wz)
    {
        float road = Mathf.Clamp01((Layout.TrackW * 0.5f + 1f - Layout.RoadDist(wx, wz)) / 2f);
        if (wx > Layout.GarageC.x - 30f && wx < Layout.GarageC.x + 28f && wz > 30f && wz < 40f) road = Mathf.Max(road, 0.8f);
        float patch = Mathf.Clamp01((Mathf.PerlinNoise(wx * 0.03f + 3f, wz * 0.03f + 7f) - 0.68f) * 3f) * 0.55f;
        float worn = Mathf.Clamp01((Mathf.PerlinNoise(wx * 0.11f + 17f, wz * 0.11f + 4f) - 0.62f) * 2.2f) * 0.45f;
        float q = Layout.PondQ(wx, wz);
        float jag = (Mathf.PerlinNoise(wx * 0.18f, wz * 0.18f) - 0.5f) * 0.06f;
        float shore = Mathf.Clamp01((1.03f + jag - q) / 0.04f);
        // ffu56: wider, patchy wet mud round the rim (puddle-ish blobs up to q 1.2)
        float blob = Mathf.Clamp01((Mathf.PerlinNoise(wx * 0.09f + 41f, wz * 0.09f + 13f) - 0.45f) * 3f);
        float mud = Mathf.Max(Mathf.Clamp01((1.1f + jag * 1.5f - q) / 0.06f), Mathf.Clamp01((1.2f + jag - q) / 0.08f) * blob) * (1f - shore);
        float pd = PathDist(wx, wz) + (Mathf.PerlinNoise(wx * 0.4f + 5f, wz * 0.4f) - 0.5f) * 0.8f;
        float path = Mathf.Clamp01((1.15f - pd) / 0.7f) * (1f - road) * (1f - shore);
        float lit = 0f;
        Vector2 a = JetLayout.ForestMin, b = JetLayout.ForestMax;
        float fd = Mathf.Min(Mathf.Min(wx - a.x, b.x - wx), Mathf.Min(wz - a.y, b.y - wz));
        if (fd > -4f) lit = Mathf.Clamp01((fd + 4f) / 8f) * (0.75f + 0.25f * Mathf.PerlinNoise(wx * 0.2f, wz * 0.2f + 9f));
        W w;
        float rest = 1f - shore - mud;
        w.s = shore; w.m = mud;
        w.p = path * rest; rest -= w.p;
        w.l = lit * rest * (1f - road); rest -= w.l;
        w.d = Mathf.Max(road, Mathf.Max(patch, worn)) * rest; rest -= w.d;
        w.g = Mathf.Max(0f, rest);
        return w;
    }

    // ---------------------------------------------------------------- ground
    static TerrainLayer Layer(string k, float tile, float smooth, float nrm)
    {
        var a = Realism.Tex(k + "_a"); if (a == null) return null;
        var l = new TerrainLayer { diffuseTexture = a, tileSize = new Vector2(tile, tile), smoothness = smooth, metallic = 0f };
        var n = Realism.Tex(k + "_n");
        if (n != null) { l.normalMapTexture = n; l.normalScale = nrm; }
        return l;
    }

    static void ApplyTerrain()
    {
        BuildPaths();
        if (terr == null) return;
        var td = terr.terrainData;
        var old = td.terrainLayers;
        if (old == null || old.Length < 4) { Debug.LogWarning("TERRAIN56 terrain: Realism layers missing"); return; }
        TerrainLayer pl = Layer("path", 2.2f, 0.06f, 1f), ll = Layer("litter", 2.6f, 0.1f, 1f);
        bool six = !Lite && pl != null && ll != null;
        td.terrainLayers = six ? new[] { old[0], old[1], old[2], old[3], pl, ll } : old;
        Layers = six ? 6 : 4;
        int ar = td.alphamapResolution, nl = Layers;
        float size = Layout.Half * 2f;
        var am = new float[ar, ar, nl];
        for (int y = 0; y < ar; y++)
            for (int x = 0; x < ar; x++)
            {
                float wx = -Layout.Half + (x + 0.5f) * size / ar, wz = -Layout.Half + (y + 0.5f) * size / ar;
                W w = Weights(wx, wz);
                am[y, x, 0] = w.g; am[y, x, 2] = w.s; am[y, x, 3] = w.m;
                if (six) { am[y, x, 1] = w.d; am[y, x, 4] = w.p; am[y, x, 5] = w.l; }
                else am[y, x, 1] = w.d + w.p + w.l;
            }
        td.SetAlphamaps(0, 0, am);
        Debug.Log("TERRAIN56 ground: " + nl + " layers (grass / dirt / shore / mud" + (six ? " / path / litter" : ", paths + litter in dirt") + "), " + pa.Count + " path segments, alphamap " + ar);
    }

    // ---------------------------------------------------------------- trees
    static void Trees()
    {
        // bark: the shared triplanar bark material (Realism) gets Bark014
        int bark = 0;
        var seen = new HashSet<Material>();
        Material leafSrc = null;
        var canopies = new List<MeshRenderer>();
        foreach (var r in Object.FindObjectsByType<MeshRenderer>())
        {
            foreach (var m in r.sharedMaterials)
            {
                if (m == null) continue;
                if (!seen.Contains(m))
                {
                    seen.Add(m);
                    if (m.shader != null && m.shader.name == "FF/RealTri" && m.mainTexture != null && m.mainTexture.name == "bark_a" && Realism.Tex("bark2_a") != null)
                    {
                        m.SetTexture("_MainTex", Realism.Tex("bark2_a"));
                        if (Realism.Tex("bark2_n") != null) m.SetTexture("_BumpMap", Realism.Tex("bark2_n"));
                        m.SetColor("_Color", new Color(0.92f, 0.88f, 0.84f)); m.SetFloat("_Scale", 1.1f); m.SetFloat("_BumpScale", 1.5f);
                        bark++;
                    }
                }
                if (m.shader != null && m.shader.name == "FF/Foliage" && m.mainTexture != null && m.mainTexture.name == "q_leaves") { leafSrc = m; canopies.Add(r); break; }
            }
        }
        var lt = Realism.Tex("leafcard");
        if (lt == null || Mats.FoliageBase == null) { Debug.Log("TERRAIN56 trees: bark mats " + bark + ", no leaf cards (texture missing)"); return; }
        var lm = new Material(Mats.FoliageBase) { name = "Leaf56" };
        lm.mainTexture = lt; lm.color = new Color(0.74f, 0.82f, 0.6f);
        if (lm.HasProperty("_Cutoff")) lm.SetFloat("_Cutoff", 0.4f);
        if (lm.HasProperty("_Wind")) lm.SetFloat("_Wind", 0.1f);
        var inner = new Dictionary<Material, Material>();
        int budget = Lite ? 5000 : 18000, trees = 0;
        float dens = Lite ? 0.45f : 1.2f;
        var rnd = new System.Random(56);
        foreach (var r in canopies)
        {
            if (LeafCards >= budget) break;
            if (Forest.Root != null && r.transform.IsChildOf(Forest.Root)) continue;
            Vector3 c = r.bounds.center;
            if (WorldAt(c) != WorldId.Ranch || JetLayout.InForest(c.x, c.z, 2f)) continue;
            var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
            var mesh = mf.sharedMesh;
            var vs = mesh.vertices; var ns = mesh.normals; var ts = mesh.triangles;
            if (ts.Length < 3) continue;
            Matrix4x4 l2w = r.transform.localToWorldMatrix;
            // area-weighted triangle sampling (world space)
            var cum = new float[ts.Length / 3]; float area = 0f;
            for (int i = 0; i < cum.Length; i++)
            {
                Vector3 a = l2w.MultiplyPoint3x4(vs[ts[i * 3]]), b = l2w.MultiplyPoint3x4(vs[ts[i * 3 + 1]]), cc = l2w.MultiplyPoint3x4(vs[ts[i * 3 + 2]]);
                area += Vector3.Cross(b - a, cc - a).magnitude * 0.5f; cum[i] = area;
            }
            int n = Mathf.Min(budget - LeafCards, Mathf.CeilToInt(area * dens));
            if (n < 6) continue;
            Matrix4x4 w2l = r.transform.worldToLocalMatrix;
            var v = new List<Vector3>(n * 4); var uv = new List<Vector2>(n * 4); var nn = new List<Vector3>(n * 4); var tri = new List<int>(n * 6);
            for (int k = 0; k < n; k++)
            {
                float pick = (float)rnd.NextDouble() * area;
                int lo = 0, hi = cum.Length - 1;
                while (lo < hi) { int mid = (lo + hi) / 2; if (cum[mid] < pick) lo = mid + 1; else hi = mid; }
                float u1 = (float)rnd.NextDouble(), u2 = (float)rnd.NextDouble(); if (u1 + u2 > 1f) { u1 = 1f - u1; u2 = 1f - u2; }
                int i0 = ts[lo * 3], i1 = ts[lo * 3 + 1], i2 = ts[lo * 3 + 2];
                Vector3 p = l2w.MultiplyPoint3x4(vs[i0] + (vs[i1] - vs[i0]) * u1 + (vs[i2] - vs[i0]) * u2);
                Vector3 nrm = ns != null && ns.Length == vs.Length ? l2w.MultiplyVector(ns[i0]).normalized : (p - c).normalized;
                Vector3 outw = (nrm + (p - c).normalized * 0.6f).normalized;
                float sz = 0.9f + (float)rnd.NextDouble() * 0.7f;
                // the sprig hangs out of the canopy surface, tip outwards / a little down, random roll
                Vector3 dir = (outw + Vector3.down * 0.25f + new Vector3((float)rnd.NextDouble() - 0.5f, 0f, (float)rnd.NextDouble() - 0.5f) * 0.6f).normalized;
                Vector3 side = Vector3.Cross(dir, Random2(rnd)).normalized; if (side.sqrMagnitude < 0.01f) side = Vector3.right;
                Vector3 b0 = p - outw * 0.25f;
                int bi = v.Count;
                Vector3 hw = side * sz * 0.32f, len = dir * sz;
                v.Add(w2l.MultiplyPoint3x4(b0 - hw)); v.Add(w2l.MultiplyPoint3x4(b0 + hw)); v.Add(w2l.MultiplyPoint3x4(b0 + hw + len)); v.Add(w2l.MultiplyPoint3x4(b0 - hw + len));
                uv.Add(new Vector2(0.18f, 0f)); uv.Add(new Vector2(0.82f, 0f)); uv.Add(new Vector2(0.82f, 1f)); uv.Add(new Vector2(0.18f, 1f));
                Vector3 ln = w2l.MultiplyVector(outw).normalized;
                for (int z = 0; z < 4; z++) nn.Add(ln);
                tri.Add(bi); tri.Add(bi + 2); tri.Add(bi + 1); tri.Add(bi); tri.Add(bi + 3); tri.Add(bi + 2);
            }
            var cm = new Mesh { name = "Leaf56", indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            cm.SetVertices(v); cm.SetUVs(0, uv); cm.SetNormals(nn); cm.SetTriangles(tri, 0); cm.RecalculateBounds();
            var go = new GameObject("Leaf56");
            go.transform.SetParent(r.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = cm;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = lm; mr.shadowCastingMode = Lite ? ShadowCastingMode.Off : ShadowCastingMode.On;
            go.AddComponent<Leaf56Follow>().src = r;
            // the old lollipop volume reads as the shaded inside of the crown
            var ms = r.sharedMaterials;
            for (int i = 0; i < ms.Length; i++)
            {
                if (ms[i] != leafSrc && (ms[i] == null || ms[i].mainTexture == null || ms[i].mainTexture.name != "q_leaves")) continue;
                Material im;
                if (!inner.TryGetValue(ms[i], out im)) { im = new Material(ms[i]) { name = "LeafInner56" }; im.color = ms[i].color * new Color(0.72f, 0.76f, 0.68f); inner[ms[i]] = im; }
                ms[i] = im;
            }
            r.sharedMaterials = ms;
            LeafCards += n; trees++;
        }
        Debug.Log("TERRAIN56 trees: bark mats " + bark + " -> Bark014, leaf sprig cards " + LeafCards + " on " + trees + " canopies (" + canopies.Count + " q_leaves renderers)");
    }
    static Vector3 Random2(System.Random r) { return new Vector3((float)r.NextDouble() - 0.5f, (float)r.NextDouble() - 0.5f, (float)r.NextDouble() - 0.5f); }

    // ---------------------------------------------------------------- buildings
    static readonly Color Cream = new Color(0.9f, 0.86f, 0.77f), Upper = new Color(0.78f, 0.82f, 0.86f), RoofC = new Color(0.24f, 0.25f, 0.28f), Wood = new Color(0.55f, 0.38f, 0.22f);
    static bool InRect(Bounds b, Vector2 c, Vector2 s, float m)
    {
        return b.max.x > c.x - s.x * 0.5f - m && b.min.x < c.x + s.x * 0.5f + m && b.max.z > c.y - s.y * 0.5f - m && b.min.z < c.y + s.y * 0.5f + m && b.center.y < 30f && b.min.y > -10f;
    }
    static void Buildings()
    {
        Material cream = Mats.Lit(Cream), upper = Mats.Lit(Upper), roof = Mats.Lit(RoofC), wood = Mats.Lit(Wood);
        Material siding = Realism.MakeTri("siding", new Color(0.98f, 0.95f, 0.88f), 2.1f, 0.14f, 1.1f);
        Material siding2 = Realism.MakeTri("siding", new Color(0.82f, 0.87f, 0.92f), 2.1f, 0.14f, 1.1f);
        Material shingle = Realism.MakeTri("shingle", new Color(0.7f, 0.68f, 0.68f), 3.2f, 0.08f, 1.3f);
        Material planks = Realism.MakeTri("planks", new Color(1f, 0.9f, 0.8f), 2.4f, 0.16f, 1f);
        Material barnRed = Realism.MakeTri("planks", new Color(0.95f, 0.32f, 0.24f), 2.4f, 0.12f, 1f);
        if (siding == null || shingle == null || planks == null) { Debug.LogWarning("TERRAIN56 buildings: textures missing"); return; }
        var map = new Dictionary<Material, Material> { { cream, siding }, { upper, siding2 ?? siding }, { roof, shingle }, { wood, planks } };
        Transform tbRoot = null; var tbo = GameObject.Find("TrailerBarn"); if (tbo != null) tbRoot = tbo.transform;
        int house = 0, garage = 0, barn = 0;
        Vector2 porchC = new Vector2(Layout.HouseC.x, Layout.HouseC.y + Layout.HouseSize.y * 0.5f + 3.5f);
        foreach (var r in Object.FindObjectsByType<MeshRenderer>())
        {
            var b = r.bounds;
            bool inBarn = tbRoot != null && r.transform.IsChildOf(tbRoot);
            bool inHouse = InRect(b, Layout.HouseC, Layout.HouseSize, 1.5f) || InRect(b, porchC, new Vector2(24f, 8f), 0.5f);
            bool inGarage = InRect(b, Layout.GarageC, Layout.GarageSize, 1.5f);
            if (!inBarn && !inHouse && !inGarage) continue;
            if (WorldAt(b.center) != WorldId.Ranch) continue;
            var ms = r.sharedMaterials; bool ch = false;
            for (int i = 0; i < ms.Length; i++)
            {
                var m = ms[i]; if (m == null) continue;
                Material rep = null;
                if (inBarn)
                {
                    if (barnRed != null && m.shader != null && m.shader.name == "FF/Std" && m.mainTexture == null && m.color.r > 0.35f && m.color.g < 0.3f && m.color.b < 0.3f) rep = barnRed;
                }
                else map.TryGetValue(m, out rep);
                if (rep != null) { ms[i] = rep; ch = true; }
            }
            if (!ch) continue;
            r.sharedMaterials = ms; BuildingSwaps++;
            if (inBarn) barn++; else if (inHouse) house++; else garage++;
        }
        Debug.Log("TERRAIN56 buildings: house " + house + ", garage " + garage + ", trailer barn " + barn + " renderers -> siding / shingles / planks (triplanar)");
    }
}

// leaf cards hide with their canopy (a tree that breaks / is hidden takes its sprigs with it)
public class Leaf56Follow : MonoBehaviour
{
    public Renderer src;
    MeshRenderer mine; float t;
    void Start() { mine = GetComponent<MeshRenderer>(); }
    void Update()
    {
        t -= Time.deltaTime; if (t > 0f) return; t = 0.5f;
        if (mine == null) return;
        bool on = src != null && src.enabled && !Realism.Degraded;
        if (mine.enabled != on) mine.enabled = on;
    }
}
