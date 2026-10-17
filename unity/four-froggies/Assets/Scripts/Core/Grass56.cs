using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// ffu56 instanced ranch grass (see Terrain56). 10 m cells over the ranch lawn, each a cached Matrix4x4[] of 3-card clumps on
// the Terrain56 grass weight (no grass on roads / paths / shore / mud / litter / buildings / the pond / reserved pads). Every
// frame each ranch camera draws the cells within its radius (frustum-tested) with Graphics.DrawMeshInstanced (FF/Grass56).
// Movers (_FFBend, 8 nearest the first ranch camera): frogs + froggies, robots, animals, vehicles (one bend per grounded wheel
// pair centre) - the grass leans away and flattens under them. Also owns Tracks56 (footprints + tyre tracks).
public class Grass56 : MonoBehaviour
{
    public static Grass56 I;
    public string Info = "";
    const float Cell = 10f;
    float radius, density;
    Material mat; Mesh clump;
    class C { public Matrix4x4[] m; public int n; public Bounds b; }
    readonly Dictionary<int, C> cells = new Dictionary<int, C>();
    readonly Vector4[] bend = new Vector4[8];
    readonly List<Vector4> cand = new List<Vector4>();
    Frog[] frogs = new Frog[0]; float frogT;
    int built, drawn, drawnInst;
    float fadeR = -1f;   // ffu60: the radius the fade uniforms were set for (Perf60 scales radius / density per tier + views)
    public string DrawnInfo { get { return drawn + "c/" + (drawnInst / 1000f).ToString("0.0") + "k r" + (radius * Perf60.GrassRadiusK).ToString("0"); } }
    Plane[] planes = new Plane[6];
    public static int Bends;

    public static void Create()
    {
        if (I != null) return;
        var tex = Realism.Tex("clump");
        if (Terrain56.GrassBase == null || tex == null || !SystemInfo.supportsInstancing)
        {
            Debug.LogWarning("TERRAIN56 grass off: " + (Terrain56.GrassBase == null ? "FF/Grass56 material missing" : tex == null ? "clump texture missing" : "no GPU instancing"));
            new GameObject("Tracks56").AddComponent<Tracks56>();
            return;
        }
        I = new GameObject("Grass56").AddComponent<Grass56>();
        I.Init(tex);
        I.gameObject.AddComponent<Tracks56>();
    }

    void Init(Texture2D tex)
    {
        bool lite = Terrain56.Lite;
        radius = lite ? 26f : 48f; density = lite ? 0.9f : 2.0f;
        mat = new Material(Terrain56.GrassBase) { name = "Grass56", enableInstancing = true };
        mat.mainTexture = tex;
        mat.color = new Color(0.8f, 0.87f, 0.7f);
        mat.SetFloat("_Cutoff", 0.42f); mat.SetFloat("_Wind", 0.13f);
        mat.SetFloat("_FadeNear", radius * 0.7f); mat.SetFloat("_FadeFar", radius);
        clump = Clump();
        Shader.SetGlobalVectorArray("_FFBend", bend);
        Info = (lite ? "lite" : "full") + " r " + radius + " m, " + density + " /m2";
        Debug.Log("TERRAIN56 grass: instanced clumps, " + Info + ", shader " + (mat.shader != null ? mat.shader.name : "-") + " supported " + (mat.shader != null && mat.shader.isSupported));
    }

    static Mesh Clump()
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var n = new List<Vector3>(); var t = new List<int>();
        for (int k = 0; k < 3; k++)
        {
            float a = k * 60f * Mathf.Deg2Rad;
            Vector3 dx = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.5f;
            int b = v.Count;
            v.Add(-dx + Vector3.down * 0.04f); v.Add(dx + Vector3.down * 0.04f); v.Add(dx + Vector3.up); v.Add(-dx + Vector3.up);
            uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(1f, 0f)); uv.Add(new Vector2(1f, 1f)); uv.Add(new Vector2(0f, 1f));
            for (int z = 0; z < 4; z++) n.Add(Vector3.up);
            t.Add(b); t.Add(b + 2); t.Add(b + 1); t.Add(b); t.Add(b + 3); t.Add(b + 2);
        }
        var m = new Mesh { name = "Clump56" };
        m.SetVertices(v); m.SetUVs(0, uv); m.SetNormals(n); m.SetTriangles(t, 0);
        m.bounds = new Bounds(Vector3.up * 0.5f, new Vector3(3f, 3f, 3f));   // generous: the shader bends / sways the cards
        return m;
    }

    static bool Excluded(float x, float z)
    {
        if (Layout.PondQ(x, z) < 1.08f) return true;
        if (Layout.RoadDist(x, z) < Layout.TrackW * 0.5f + 1.2f) return true;
        Vector2 hc = Layout.HouseC, hs = Layout.HouseSize;
        if (Mathf.Abs(x - hc.x) < hs.x * 0.5f + 1f && z > hc.y - hs.y * 0.5f - 1f && z < hc.y + hs.y * 0.5f + 8f) return true;
        Vector2 gc = Layout.GarageC, gs = Layout.GarageSize;
        if (Mathf.Abs(x - gc.x) < gs.x * 0.5f + 1f && Mathf.Abs(z - gc.y) < gs.y * 0.5f + 1f) return true;
        if (Mathf.Abs(x - Layout.PoolC.x) < Layout.PoolSize.x * 0.5f + 2f && Mathf.Abs(z - Layout.PoolC.y) < Layout.PoolSize.y * 0.5f + 2f) return true;
        Vector3 tb = TrailerBarn.Centre;
        if (tb.sqrMagnitude > 1f && (new Vector2(x - tb.x, z - tb.z)).sqrMagnitude < 18f * 18f) return true;
        if (JetLayout.Reserved(x, z) || RcTrack.Reserved(x, z) || MonsterPark.Reserved(x, z) || CarrierLayout.Reserved(x, z)) return true;
        if ((new Vector2(x, z) - StorySet.PadXZ).sqrMagnitude < 16f * 16f) return true;
        if ((new Vector2(x, z) - Terrain56.FortSpot).sqrMagnitude < 3f * 3f) return true;
        return false;
    }

    C Build(int cx, int cz)
    {
        var list = new List<Matrix4x4>();
        uint seed = (uint)(cx * 73856093) ^ (uint)(cz * 19349663) ^ 56u;
        var r = new System.Random((int)(seed & 0x7fffffff));
        int want = Mathf.RoundToInt(Cell * Cell * density);
        float x0 = cx * Cell, z0 = cz * Cell;
        float ymin = 1e9f, ymax = -1e9f;
        for (int i = 0; i < want && list.Count < 1023; i++)
        {
            float x = x0 + (float)r.NextDouble() * Cell, z = z0 + (float)r.NextDouble() * Cell;
            double roll = r.NextDouble(), hroll = r.NextDouble(), yaw = r.NextDouble();
            if (Mathf.Abs(x) > Layout.Half - 3f || Mathf.Abs(z) > Layout.Half - 3f) continue;
            var w = Terrain56.Weights(x, z);
            if (w.g < 0.55f || roll > w.g * 1.15f) continue;
            if (Excluded(x, z)) continue;
            float y = Ranch.GY(x, z);
            float bank = Mathf.Clamp01(1f - (Layout.PondQ(x, z) - 1.1f) / 0.25f);
            float h = (0.32f + (float)hroll * 0.34f) * (1f + bank * 0.7f);
            float wd = h * (0.9f + (float)hroll * 0.3f);
            list.Add(Matrix4x4.TRS(new Vector3(x, y - 0.02f, z), Quaternion.Euler(0f, (float)yaw * 360f, 0f), new Vector3(wd, h, wd)));
            ymin = Mathf.Min(ymin, y); ymax = Mathf.Max(ymax, y + h);
        }
        var c = new C { m = list.ToArray(), n = list.Count };
        if (c.n > 0) c.b = new Bounds(new Vector3(x0 + Cell * 0.5f, (ymin + ymax) * 0.5f, z0 + Cell * 0.5f), new Vector3(Cell + 3f, ymax - ymin + 3f, Cell + 3f));
        built++;
        return c;
    }

    void Update()
    {
        if (Game.I == null || Game.I.slots == null) return;
        if (Realism.Degraded) return;   // the lite frame-rate guard sheds the grass
        Camera first = null;
        int buildBudget = 6; drawn = 0; drawnInst = 0;
        float R = radius * Perf60.GrassRadiusK, dk = Perf60.GrassDensK;   // ffu60
        if (Mathf.Abs(R - fadeR) > 0.5f) { fadeR = R; mat.SetFloat("_FadeNear", R * 0.7f); mat.SetFloat("_FadeFar", R); }
        foreach (var s in Game.I.slots)
        {
            if (s == null || s.cam == null || !s.cam.isActiveAndEnabled || s.frog < 0 || s.frog >= Game.I.frogs.Count) continue;
            var f = Game.I.frogs[s.frog];
            if (f == null || (f.world != WorldId.Ranch && f.world != WorldId.Underwater)) continue;
            Vector3 cp = s.cam.transform.position;
            if (Terrain56.WorldAt(cp) != WorldId.Ranch) continue;
            if (first == null) first = s.cam;
            GeometryUtility.CalculateFrustumPlanes(s.cam, planes);
            int c0x = Mathf.FloorToInt((cp.x - R) / Cell), c1x = Mathf.FloorToInt((cp.x + R) / Cell);
            int c0z = Mathf.FloorToInt((cp.z - R) / Cell), c1z = Mathf.FloorToInt((cp.z + R) / Cell);
            float hgt = Mathf.Abs(cp.y - Ranch.GY(cp.x, cp.z));
            if (hgt > R) continue;   // high above the ranch: grass is sub-pixel
            for (int cx = c0x; cx <= c1x; cx++)
                for (int cz = c0z; cz <= c1z; cz++)
                {
                    float ccx = (cx + 0.5f) * Cell - cp.x, ccz = (cz + 0.5f) * Cell - cp.z;
                    float cd2 = ccx * ccx + ccz * ccz;
                    if (cd2 > (R + Cell * 0.71f) * (R + Cell * 0.71f)) continue;
                    int key = (cx + 1000) * 4096 + (cz + 1000);
                    C c;
                    if (!cells.TryGetValue(key, out c)) { if (buildBudget-- <= 0) continue; c = Build(cx, cz); cells[key] = c; }
                    if (c.n == 0 || !GeometryUtility.TestPlanesAABB(planes, c.b)) continue;
                    // ffu60: density by tier, and the outer half of the radius thins out (it fades there anyway); the cell's
                    // matrices are in random order, so the first n is an even subsample
                    float far = Mathf.Clamp01((Mathf.Sqrt(cd2) - R * 0.5f) / (R * 0.5f));
                    int cnt = Mathf.Clamp(Mathf.CeilToInt(c.n * dk * (Perf60.On ? 1f - 0.45f * far : 1f)), 1, c.n);
                    Graphics.DrawMeshInstanced(clump, 0, mat, c.m, cnt, null, ShadowCastingMode.Off, true, 0, s.cam);
                    drawn++; drawnInst += cnt;
                }
        }
        UpdateBenders(first);
    }

    void UpdateBenders(Camera cam)
    {
        for (int i = 0; i < 8; i++) bend[i] = Vector4.zero;
        Bends = 0;
        if (cam != null)
        {
            Vector3 cp = cam.transform.position;
            cand.Clear(); rads.Clear();
            frogT -= Time.deltaTime;
            if (frogT <= 0f) { frogT = 2f; frogs = Object.FindObjectsOfType<Frog>(); }
            foreach (var f in frogs)
            {
                if (f == null || !f.isActiveAndEnabled || f.vehicle != null || f.world != WorldId.Ranch) continue;
                Add(f.transform.position, 0.55f * Mathf.Clamp(f.transform.lossyScale.y, 0.6f, 2.5f), cp);
            }
            if (RanchLife.I != null)
                foreach (var r in RanchLife.I.robots) if (r != null && r.isActiveAndEnabled && r.world == WorldId.Ranch && r.rideOf == null) Add(r.transform.position, 0.65f, cp);
            foreach (var a in Animal.All) if (a != null && a.isActiveAndEnabled) Add(a.transform.position, 0.9f, cp);
            foreach (var v in Vehicle.All)
            {
                var gv = v as GroundVehicle;
                if (gv == null)
                {   // own-physics wheels: the Roadster, bikes, skateboards, RC cars
                    if (v == null || !v.isActiveAndEnabled || v.stowed) continue;
                    float or_ = v is Roadster49 ? 2.3f : v is Bike ? 0.9f : v is Skateboard ? 0.45f : v is RcCar ? 0.35f : 0f;
                    if (or_ > 0f) Add(v.transform.position, or_, cp);
                    continue;
                }
                if (!gv.isActiveAndEnabled || gv.stowed) continue;
                Vector3 p = gv.transform.position;
                if ((p - cp).sqrMagnitude > 45f * 45f) continue;
                float rr = 1.2f;
                foreach (var w in gv.wheels) { float d = new Vector2(w.mount.x, w.mount.z).magnitude; if (d + w.radius > rr) rr = d + w.radius; }
                Add(p, Mathf.Min(rr * 0.9f, 4f), cp);
            }
            for (int i = 0; i < cand.Count && i < 8; i++) { var c = cand[i]; bend[i] = new Vector4(c.x, c.y, c.z, radTmp[i]); Bends++; }
        }
        Shader.SetGlobalVectorArray("_FFBend", bend);
    }
    readonly float[] radTmp = new float[8];
    readonly List<float> rads = new List<float>();
    void Add(Vector3 p, float r, Vector3 cp)
    {
        float d = (p - cp).sqrMagnitude; if (d > 40f * 40f) return;
        float gy = Ranch.GY(p.x, p.z); if (p.y - gy > 1.2f || p.y - gy < -0.6f) return;   // in the air / on a bridge / indoors
        // keep the candidate list sorted by distance with its radius alongside (simple insertion; the list is small)
        int i = 0; while (i < cand.Count && cand[i].w < d) i++;
        if (i >= 8) return;
        cand.Insert(i, new Vector4(p.x, gy, p.z, d)); rads.Insert(i, r);
        if (cand.Count > 8) { cand.RemoveAt(8); rads.RemoveAt(8); }
        for (int k = 0; k < rads.Count && k < 8; k++) radTmp[k] = rads[k];
    }

    public string Stats() { return "cells built " + built + " drawn " + drawn + " bends " + Bends; }
}
