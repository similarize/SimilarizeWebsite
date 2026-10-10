using System.Collections.Generic;
using UnityEngine;

// ffu21: the breakable ranch.
// - James's house is now built from segments: 9 + 9 + 6 + 6 wall panels per storey (each window is its own glass piece in
//   a real opening), north roof sections, the north / west parapets and the chimney all break and come back. Behind the
//   walls there is a plaster-lined, two-storey hollow with furniture (also breakable), so holes look into rooms. The
//   porch-side ground floor (front door, robot charging jacks), the south roof with the helipad + drone pad and the ramp
//   stay solid. The house interior world (dollhouse + the REAL ROOM) is a separate place far away and is never touched.
// - Track: RallyTrack's rails / tyres / kickers / pillars / arch / loop rails come in through a separate root and become
//   pieces; the track surface, berms, links, runways and loop meshes are cut into 3D cells (22 x 5 x 22 m, loop 7 m) that
//   each break on their own. Flags, trees and rocks are pieces too (Ranch.cs).
public static class RanchBreak
{
    public static Transform BreakRoot;
    static readonly Color Cream = new Color(0.9f, 0.86f, 0.77f);
    static readonly Color Upper = new Color(0.78f, 0.82f, 0.86f);
    static readonly Color Trim = new Color(0.97f, 0.97f, 0.95f);
    static readonly Color Stone = new Color(0.55f, 0.52f, 0.48f);
    static readonly Color RoofC = new Color(0.24f, 0.25f, 0.28f);

    public static Transform Root
    {
        get { if (BreakRoot == null) BreakRoot = new GameObject("RanchBreakables").transform; return BreakRoot; }
    }

    public static Transform Group(string name, Vector3 pos)
    {
        var g = new GameObject(name).transform;
        g.SetParent(Root, false);
        g.position = pos;
        return g;
    }

    static GameObject Box(Transform parent, Vector3 worldPos, Vector3 size, Material m, bool col)
    {
        var g = Mats.Prim(PrimitiveType.Cube, parent, Vector3.zero, size, m, col);
        g.transform.position = worldPos;
        return g;
    }

    // ---------------- house ----------------
    public static readonly List<Breakable> HousePieces = new List<Breakable>();
    public static Rect HouseInner;

    public static void House(Transform staticRoot)
    {
        Vector2 c = Layout.HouseC, s = Layout.HouseSize;
        float H = Layout.HouseH;
        float x0 = c.x - s.x * 0.5f, x1 = c.x + s.x * 0.5f, z0 = c.y - s.y * 0.5f, z1 = c.y + s.y * 0.5f;
        const float T = 0.5f;
        float slabBot = H - 0.4f, mid = H * 0.5f;
        HouseInner = new Rect(x0 + T, z0 + T, s.x - 2f * T, s.y - 2f * T);

        // hollow: plaster liner (inward-facing box), first floor slab (visual), skirting
        var liner = new GameObject("HouseLiner");
        liner.transform.SetParent(staticRoot, false);
        liner.transform.position = new Vector3(c.x, slabBot * 0.5f, c.y);
        liner.transform.localScale = new Vector3(s.x - 2f * T - 0.02f, slabBot, s.y - 2f * T - 0.02f);
        liner.AddComponent<MeshFilter>().sharedMesh = InvertedCube();
        liner.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit(new Color(0.86f, 0.82f, 0.74f));
        Box(staticRoot, new Vector3(c.x, mid, c.y), new Vector3(s.x - 2f * T - 0.04f, 0.3f, s.y - 2f * T - 0.04f), Mats.Lit(new Color(0.5f, 0.34f, 0.2f)), false);
        Box(staticRoot, new Vector3(c.x, 0.03f, c.y), new Vector3(s.x - 2f * T - 0.04f, 0.06f, s.y - 2f * T - 0.04f), Mats.Lit(new Color(0.55f, 0.4f, 0.26f)), false);

        Material cream = Mats.Lit(Cream), upper = Mats.Lit(Upper), trim = Mats.Lit(Trim), stone = Mats.Lit(Stone);
        // north (front, porch side) / south: 9 columns full width; west / east: 6 columns between them
        for (int face = 0; face < 4; face++)
        {
            bool alongX = face < 2;
            int cols = alongX ? 9 : 6;
            float a0 = alongX ? x0 : z0 + T, a1 = alongX ? x1 : z1 - T;
            float w = (a1 - a0) / cols;
            for (int i = 0; i < cols; i++)
                for (int row = 0; row < 2; row++)
                {
                    float a = a0 + w * (i + 0.5f);
                    float y0 = row == 0 ? 0f : mid, y1 = row == 0 ? mid : slabBot;
                    Vector3 outN; Vector3 ctr;
                    switch (face)
                    {
                        case 0: outN = Vector3.forward; ctr = new Vector3(a, (y0 + y1) * 0.5f, z1 - T * 0.5f); break;
                        case 1: outN = Vector3.back; ctr = new Vector3(a, (y0 + y1) * 0.5f, z0 + T * 0.5f); break;
                        case 2: outN = Vector3.left; ctr = new Vector3(x0 + T * 0.5f, (y0 + y1) * 0.5f, a); break;
                        default: outN = Vector3.right; ctr = new Vector3(x1 - T * 0.5f, (y0 + y1) * 0.5f, a); break;
                    }
                    bool porch = face == 0 && row == 0 && a > -56f && a < -28f;     // front door + charging jacks: solid
                    bool door = face == 0 && row == 0 && Mathf.Abs(a - c.x) < 3f;
                    Transform parent = porch ? staticRoot : Group("HouseWall", ctr);
                    Panel(parent, ctr, outN, w, y1 - y0, T, row == 0 ? cream : upper, trim, stone, row, !door, porch);
                    if (!porch)
                    {
                        var b = Destruct.Register(parent.gameObject, BreakKind.Wall);
                        HousePieces.Add(b);
                        if (!door && glassGo != null)
                        {
                            var wb = Destruct.Register(glassGo, BreakKind.Window);
                            wb.host = b; b.linked.Add(wb);
                            HousePieces.Add(wb);
                        }
                    }
                }
        }
        // roof: south part (helipad + drone pad) solid, north strip in 4 breakable sections
        float split = 2f;
        Box(staticRoot, new Vector3(c.x, H - 0.15f, (z0 - 0.2f + split) * 0.5f), new Vector3(s.x + 0.4f, 0.5f, split - (z0 - 0.2f)), Mats.Lit(RoofC), true);
        for (int i = 0; i < 4; i++)
        {
            float xa = x0 - 0.2f + (s.x + 0.4f) * i / 4f, xb = x0 - 0.2f + (s.x + 0.4f) * (i + 1) / 4f;
            Vector3 p = new Vector3((xa + xb) * 0.5f, H - 0.15f, (split + z1 + 0.2f) * 0.5f);
            var g = Group("HouseRoof", p);
            Box(g, p, new Vector3(xb - xa - 0.01f, 0.5f, z1 + 0.2f - split), Mats.Lit(RoofC), true);
            HousePieces.Add(Destruct.Register(g.gameObject, BreakKind.Roof));
        }
        // parapets: north + west breakable segments, south + east solid (the ramp lands on the east side)
        float ph = 0.8f, py = H + ph * 0.5f;
        for (int i = 0; i < 6; i++)
        {
            float xa = x0 + s.x * i / 6f, xb = x0 + s.x * (i + 1) / 6f;
            Vector3 p = new Vector3((xa + xb) * 0.5f, py, z1);
            var g = Group("Parapet", p); Box(g, p, new Vector3(xb - xa - 0.01f, ph, 0.4f), trim, true);
            HousePieces.Add(Destruct.Register(g.gameObject, BreakKind.Track));
        }
        for (int i = 0; i < 4; i++)
        {
            float za = z0 + s.y * i / 4f, zb = z0 + s.y * (i + 1) / 4f;
            Vector3 p = new Vector3(x0, py, (za + zb) * 0.5f);
            var g = Group("Parapet", p); Box(g, p, new Vector3(0.4f, ph, zb - za - 0.01f), trim, true);
            HousePieces.Add(Destruct.Register(g.gameObject, BreakKind.Track));
        }
        Box(staticRoot, new Vector3(c.x, py, z0), new Vector3(s.x, ph, 0.4f), trim, true);
        float gapA = 4.5f, gapB = 10.5f;
        Box(staticRoot, new Vector3(x1, py, (z0 + gapA) * 0.5f), new Vector3(0.4f, ph, gapA - z0), trim, true);
        if (z1 > gapB) Box(staticRoot, new Vector3(x1, py, (gapB + z1) * 0.5f), new Vector3(0.4f, ph, z1 - gapB), trim, true);
        // chimney
        {
            Vector3 p = new Vector3(c.x + 8f, H + 1.3f, c.y + 10f);
            var g = Group("Chimney", p); Box(g, p, new Vector3(2f, 2.4f, 2f), stone, true);
            HousePieces.Add(Destruct.Register(g.gameObject, BreakKind.Roof));
        }
        Furniture(x0 + T, x1 - T, z0 + T, z1 - T, mid);
        var tick = new GameObject("HouseSafety").AddComponent<HouseSafety>();
        tick.transform.SetParent(Root, false);
    }

    static GameObject glassGo;
    // one wall panel; a window panel is 4 blocks around a real opening + trims, the glass is a separate piece (glassGo)
    static void Panel(Transform parent, Vector3 ctr, Vector3 outN, float w, float h, float t, Material wall, Material trim, Material stone, int row, bool window, bool solidGlass)
    {
        glassGo = null;
        Vector3 along = Vector3.Cross(Vector3.up, outN);     // panel's width axis
        System.Func<float, float, float, float, Material, GameObject> B = (u, v, du, dv, m) =>
        {
            // u along width (centre-relative), v height (centre-relative), du / dv sizes; thickness t
            Vector3 p = ctr + along * u + Vector3.up * v;
            Vector3 sz = Mathf.Abs(outN.x) > 0.5f ? new Vector3(t, dv, du) : new Vector3(du, dv, t);
            return Box(parent, p, sz, m, true);
        };
        float ow = 2.2f, oh = 1.7f;
        float wy = (row == 0 ? 2.4f : 6.8f) - ctr.y;          // window centre height relative to panel centre
        if (window && w > ow + 0.6f)
        {
            float side = (w - ow) * 0.5f;
            B(-(ow + side) * 0.5f, 0f, side, h, wall);
            B((ow + side) * 0.5f, 0f, side, h, wall);
            float botH = (wy - oh * 0.5f) + h * 0.5f, topH = h * 0.5f - (wy + oh * 0.5f);
            if (botH > 0.02f) B(0f, -h * 0.5f + botH * 0.5f, ow, botH, wall);
            if (topH > 0.02f) B(0f, h * 0.5f - topH * 0.5f, ow, topH, wall);
            // trims above / below the opening (proud of the wall)
            Vector3 tp = ctr + along * 0f + outN * (t * 0.5f + 0.05f);
            Vector3 tsz = Mathf.Abs(outN.x) > 0.5f ? new Vector3(0.16f, 0.12f, ow + 0.3f) : new Vector3(ow + 0.3f, 0.12f, 0.16f);
            var tr1 = Box(parent, tp + Vector3.up * (wy + oh * 0.5f + 0.06f), tsz, trim, false);
            var tr2 = Box(parent, tp + Vector3.up * (wy - oh * 0.5f - 0.06f), tsz, trim, false);
            // glass in the opening, near the outer face
            Vector3 gp = ctr + Vector3.up * wy + outN * (t * 0.5f - 0.08f);
            Vector3 gsz = Mathf.Abs(outN.x) > 0.5f ? new Vector3(0.06f, oh, ow) : new Vector3(ow, oh, 0.06f);
            if (solidGlass) Box(parent, gp, gsz, Mats.Glass, false);
            else
            {
                var g = new GameObject("Window").transform;
                g.SetParent(Root, false); g.position = gp;
                Box(g, gp, gsz, Mats.Glass, false);
                glassGo = g.gameObject;
            }
        }
        else B(0f, 0f, w, h, wall);
        // ground floor: stone plinth strip + trim band on top, both proud of the wall
        if (row == 0)
        {
            Vector3 sp = ctr + outN * (t * 0.5f + 0.05f);
            Vector3 ssz = Mathf.Abs(outN.x) > 0.5f ? new Vector3(0.1f, 0.8f, w) : new Vector3(w, 0.8f, 0.1f);
            Box(parent, new Vector3(sp.x, 0.4f, sp.z), ssz, stone, false);
            Vector3 bsz = Mathf.Abs(outN.x) > 0.5f ? new Vector3(0.15f, 0.3f, w) : new Vector3(w, 0.3f, 0.15f);
            Box(parent, new Vector3(sp.x, ctr.y + h * 0.5f, sp.z), bsz, trim, false);
        }
    }

    static void Furniture(float xa, float xb, float za, float zb, float mid)
    {
        var r = new System.Random(21);
        Color[] sofa = { new Color(0.35f, 0.45f, 0.62f), new Color(0.62f, 0.32f, 0.28f), new Color(0.4f, 0.55f, 0.38f) };
        Material wood = Mats.Lit(new Color(0.5f, 0.33f, 0.18f)), white = Mats.Lit(new Color(0.92f, 0.9f, 0.86f));
        for (int floor = 0; floor < 2; floor++)
        {
            float fy = floor == 0 ? 0.06f : mid + 0.15f;
            // along each wall, every ~9 m, alternating furniture
            int k = 0;
            for (int face = 0; face < 4; face++)
            {
                bool alongX = face < 2;
                float a0 = alongX ? xa + 2f : za + 2f, a1 = alongX ? xb - 2f : zb - 2f;
                for (float a = a0; a < a1; a += 9f, k++)
                {
                    Vector3 inN = face == 0 ? Vector3.back : face == 1 ? Vector3.forward : face == 2 ? Vector3.right : Vector3.left;
                    Vector3 wallP = face == 0 ? new Vector3(a, fy, zb) : face == 1 ? new Vector3(a, fy, za) : face == 2 ? new Vector3(xa, fy, a) : new Vector3(xb, fy, a);
                    Vector3 p = wallP + inN * 0.9f;
                    var g = Group("Furniture", p);
                    g.rotation = Quaternion.LookRotation(inN);
                    int type = (k + floor) % 4;
                    System.Action<Vector3, Vector3, Material> P = (lp, sz, m) => { var o = Mats.Prim(PrimitiveType.Cube, g, lp, sz, m, false); };
                    if (type == 0) { Material m = Mats.Lit(sofa[r.Next(sofa.Length)]); P(new Vector3(0f, 0.25f, 0f), new Vector3(2.4f, 0.5f, 0.9f), m); P(new Vector3(0f, 0.65f, -0.35f), new Vector3(2.4f, 0.8f, 0.2f), m); P(new Vector3(-1.1f, 0.45f, 0f), new Vector3(0.2f, 0.4f, 0.9f), m); P(new Vector3(1.1f, 0.45f, 0f), new Vector3(0.2f, 0.4f, 0.9f), m); }
                    else if (type == 1) { P(new Vector3(0f, 1f, -0.2f), new Vector3(1.8f, 2f, 0.45f), wood); for (int sh = 0; sh < 4; sh++) P(new Vector3(0f, 0.3f + sh * 0.48f, -0.05f), new Vector3(1.6f, 0.3f, 0.2f), Mats.Lit(sofa[(sh + k) % sofa.Length])); }
                    else if (type == 2) { P(new Vector3(0f, 0.75f, 0.5f), new Vector3(1.6f, 0.08f, 0.9f), wood); for (int l = 0; l < 4; l++) P(new Vector3((l % 2 == 0 ? -0.7f : 0.7f), 0.37f, 0.5f + (l < 2 ? -0.35f : 0.35f)), new Vector3(0.08f, 0.74f, 0.08f), wood); P(new Vector3(0f, 0.95f, 0.5f), new Vector3(0.3f, 0.3f, 0.3f), Mats.Lit(new Color(0.9f, 0.75f, 0.3f))); }
                    else { P(new Vector3(0f, 0.3f, 0.4f), new Vector3(1.6f, 0.4f, 2.1f), wood); P(new Vector3(0f, 0.55f, 0.4f), new Vector3(1.5f, 0.2f, 2f), white); P(new Vector3(0f, 0.7f, -0.45f), new Vector3(1.2f, 0.18f, 0.4f), Mats.Lit(sofa[k % sofa.Length])); }
                    HousePieces.Add(Destruct.Register(g.gameObject, BreakKind.Furniture));
                }
            }
        }
    }

    static Mesh invCube;
    static Mesh InvertedCube()
    {
        if (invCube != null) return invCube;
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh src = tmp.GetComponent<MeshFilter>().sharedMesh;
        invCube = Object.Instantiate(src);
        Object.Destroy(tmp);
        var t = invCube.triangles;
        for (int i = 0; i < t.Length; i += 3) { int a = t[i]; t[i] = t[i + 1]; t[i + 1] = a; }
        invCube.triangles = t;
        var n = invCube.normals;
        for (int i = 0; i < n.Length; i++) n[i] = -n[i];
        invCube.normals = n;
        invCube.name = "InvertedCube";
        return invCube;
    }

    // ---------------- track ----------------
    // RallyTrack built its rails / tyres / kickers / pillars / arch into `parts`: each becomes a piece (tiny centre-line dots
    // go back to the merged static root)
    public static void TrackParts(Transform parts, Transform staticRoot)
    {
        var kids = new List<Transform>();
        foreach (Transform t in parts) kids.Add(t);
        int n = 0;
        foreach (var t in kids)
        {
            if (t.localScale.y <= 0.02f || t.GetComponent<MeshRenderer>() == null) { t.SetParent(staticRoot, true); continue; }
            t.SetParent(Root, true);
            Destruct.Register(t.gameObject, BreakKind.Track);
            n++;
        }
        Object.Destroy(parts.gameObject);
        Debug.Log("RanchBreak: " + n + " track parts");
    }

    // cut the track surface / berm / loop / runway / link meshes into 3D cells (22 x 5 x 22 m, loop 7 m), each cell = one
    // breakable piece (visual only). The colliders stay ONE continuous MeshCollider per original mesh (ffu19's seamless
    // loop needs that); TrackColliders re-cooks it from the intact cells' triangles whenever a cell breaks / comes back,
    // so a blown-up cell is a real gap and everything else stays seamless.
    public static void SplitTrackMeshes(GameObject rally)
    {
        if (rally == null) return;
        var mfs = rally.GetComponentsInChildren<MeshFilter>();
        var cells = new Dictionary<long, List<KeyValuePair<MeshFilter, List<int>>>>();
        var cellTris = new Dictionary<long, List<KeyValuePair<MeshFilter, List<int>>>>();   // triangle indices (into the source) per cell
        var cellCentre = new Dictionary<long, Vector3>();
        var cellCount = new Dictionary<long, int>();
        foreach (var mf in mfs)
        {
            Mesh m = mf.sharedMesh;
            if (m == null || !m.isReadable || m.subMeshCount > 1) continue;
            bool stunt = mf.gameObject.layer == RallyTrack.StuntLayer;
            float cx = stunt ? 7f : 22f, cy = stunt ? 7f : 5f;
            Vector3[] v = m.vertices;
            int[] tr = m.triangles;
            Matrix4x4 w = mf.transform.localToWorldMatrix;
            for (int i = 0; i < tr.Length; i += 3)
            {
                Vector3 ce = w.MultiplyPoint3x4((v[tr[i]] + v[tr[i + 1]] + v[tr[i + 2]]) / 3f);
                long key = ((long)Mathf.FloorToInt((ce.x + 5000f) / cx) << 34) ^ ((long)Mathf.FloorToInt((ce.y + 500f) / cy) << 20) ^ ((long)Mathf.FloorToInt((ce.z + 5000f) / cx)) ^ (stunt ? (1L << 60) : 0L);
                List<KeyValuePair<MeshFilter, List<int>>> l;
                if (!cells.TryGetValue(key, out l)) { l = new List<KeyValuePair<MeshFilter, List<int>>>(); cells[key] = l; cellCentre[key] = Vector3.zero; cellCount[key] = 0; }
                List<int> tl = null;
                foreach (var kv in l) if (kv.Key == mf) { tl = kv.Value; break; }
                if (tl == null) { tl = new List<int>(); l.Add(new KeyValuePair<MeshFilter, List<int>>(mf, tl)); }
                tl.Add(tr[i]); tl.Add(tr[i + 1]); tl.Add(tr[i + 2]);
                cellCentre[key] += ce; cellCount[key]++;
            }
        }
        int pieces = 0;
        var visualDone = new HashSet<MeshFilter>();
        foreach (var kv in cells)
        {
            Vector3 pc = cellCentre[kv.Key] / Mathf.Max(1, cellCount[kv.Key]);
            var g = Group("TrackCell", pc);
            var parts = new List<KeyValuePair<MeshCollider, List<int>>>();
            foreach (var part in kv.Value)
            {
                MeshFilter src = part.Key;
                Mesh sm = src.sharedMesh;
                Vector3[] v = sm.vertices; Vector3[] nr = sm.normals; Vector2[] uv = sm.uv;
                Matrix4x4 w = src.transform.localToWorldMatrix;
                var map = new Dictionary<int, int>();
                var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nu = new List<Vector2>(); var nt = new List<int>();
                foreach (int idx in part.Value)
                {
                    int j;
                    if (!map.TryGetValue(idx, out j))
                    {
                        j = nv.Count; map[idx] = j;
                        nv.Add(w.MultiplyPoint3x4(v[idx]) - pc);
                        if (nr != null && nr.Length == v.Length) nn.Add(w.MultiplyVector(nr[idx]).normalized);
                        if (uv != null && uv.Length == v.Length) nu.Add(uv[idx]);
                    }
                    nt.Add(j);
                }
                var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, name = src.name + " cell" };
                mesh.SetVertices(nv);
                if (nn.Count == nv.Count) mesh.SetNormals(nn);
                if (nu.Count == nv.Count) mesh.SetUVs(0, nu);
                mesh.SetTriangles(nt, 0);
                if (nn.Count != nv.Count) mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                var go = new GameObject(src.name);
                go.layer = src.gameObject.layer;
                go.transform.SetParent(g, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var sr = src.GetComponent<MeshRenderer>();
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = sr != null ? sr.sharedMaterial : Mats.Lit(Color.gray);
                if (sr != null) { r.shadowCastingMode = sr.shadowCastingMode; r.receiveShadows = sr.receiveShadows; }
                var mc = src.GetComponent<MeshCollider>();
                if (mc != null) parts.Add(new KeyValuePair<MeshCollider, List<int>>(mc, part.Value));
                visualDone.Add(src);
            }
            var b = Destruct.Register(g.gameObject, BreakKind.TrackSurface);
            foreach (var pr in parts) TrackColliders.Add(pr.Key, b, pr.Value);
            if (parts.Count > 0) b.onState = TrackColliders.Changed;
            pieces++;
        }
        // the originals keep only their (continuous) colliders; the cells draw the surface
        foreach (var mf in visualDone)
        {
            if (mf == null) continue;
            var mr = mf.GetComponent<MeshRenderer>();
            if (mr != null) Object.Destroy(mr);
            if (mf.GetComponent<MeshCollider>() == null) Object.Destroy(mf.gameObject);
        }
        Debug.Log("RanchBreak: track meshes cut into " + pieces + " pieces, " + TrackColliders.Count + " continuous colliders kept");
    }
}

// one continuous MeshCollider per original track mesh, re-cooked from the triangles of its intact cells
public static class TrackColliders
{
    class Src { public MeshCollider mc; public Mesh orig; public Vector3[] verts; public readonly List<KeyValuePair<Breakable, List<int>>> cells = new List<KeyValuePair<Breakable, List<int>>>(); public bool dirty; public Mesh live; }
    static readonly Dictionary<MeshCollider, Src> srcs = new Dictionary<MeshCollider, Src>();
    static readonly Dictionary<Breakable, List<Src>> ofCell = new Dictionary<Breakable, List<Src>>();
    static readonly List<Src> dirty = new List<Src>();
    public static int Count { get { return srcs.Count; } }

    public static void Add(MeshCollider mc, Breakable b, List<int> tris)
    {
        Src s;
        if (!srcs.TryGetValue(mc, out s)) { s = new Src { mc = mc, orig = mc.sharedMesh }; s.verts = s.orig.vertices; srcs[mc] = s; }
        s.cells.Add(new KeyValuePair<Breakable, List<int>>(b, tris));
        List<Src> l;
        if (!ofCell.TryGetValue(b, out l)) { l = new List<Src>(); ofCell[b] = l; }
        if (!l.Contains(s)) l.Add(s);
    }

    public static void Changed(Breakable b)
    {
        List<Src> l;
        if (!ofCell.TryGetValue(b, out l)) return;
        foreach (var s in l) if (!s.dirty) { s.dirty = true; dirty.Add(s); }
    }

    static readonly List<int> tmp = new List<int>();
    public static void Flush()
    {
        if (dirty.Count == 0) return;
        foreach (var s in dirty)
        {
            s.dirty = false;
            if (s.mc == null) continue;
            bool all = true;
            tmp.Clear();
            foreach (var c in s.cells)
            {
                bool solid = c.Key == null || c.Key.state == 0;
                if (solid) tmp.AddRange(c.Value); else all = false;
            }
            if (all) { s.mc.sharedMesh = null; s.mc.sharedMesh = s.orig; continue; }
            if (s.live == null) { s.live = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, name = s.orig.name + " live" }; }
            s.live.Clear();
            s.live.vertices = s.verts;
            s.live.SetTriangles(tmp, 0);
            s.live.RecalculateBounds();
            s.mc.sharedMesh = null;
            s.mc.enabled = tmp.Count > 0;
            if (tmp.Count > 0) s.mc.sharedMesh = s.live;
        }
        dirty.Clear();
    }
}

// a froggy left inside the hollow house when every wall is back gets walked out onto the porch
public class HouseSafety : MonoBehaviour
{
    readonly Dictionary<Frog, float> inside = new Dictionary<Frog, float>();
    float t;
    void Update()
    {
        t -= Time.deltaTime;
        if (t > 0f || Game.I == null) return;
        t = 0.5f;
        Rect r = RanchBreak.HouseInner;
        bool open = false;
        foreach (var b in RanchBreak.HousePieces) if (b != null && b.state != 0 && b.kind == BreakKind.Wall) { open = true; break; }
        foreach (var f in Game.I.frogs)
        {
            if (f == null || f.world != WorldId.Ranch || f.vehicle != null || f.netPuppet) continue;
            Vector3 p = f.transform.position;
            bool inH = r.Contains(new Vector2(p.x, p.z)) && p.y < Layout.HouseH - 0.5f;
            float k; inside.TryGetValue(f, out k);
            k = inH ? k + 0.5f : 0f;
            inside[f] = k;
            if (inH && !open && k > 2.5f)
            {
                Vector2 c = Layout.HouseC;
                float z = c.y + Layout.HouseSize.y * 0.5f + 3f;
                f.Teleport(new Vector3(c.x, Ranch.GY(c.x, z) + 0.6f, z));
                f.Toast("The walls grew back - out onto the porch you go!", 2.5f);
                inside[f] = 0f;
            }
        }
    }
}
