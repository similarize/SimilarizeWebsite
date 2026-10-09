using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

// Loads the "LBM1" mesh packs made offline by work/lb-gfx/bb (+ the Four Froggies nature packs), Resources/LB/<name>.bytes,
// same format as Lambo Blast / Four Froggies: the airsoft rifle, the Critters (frog / cat / shepherd / dachshund), and the
// Quaternius CC0 trees, bushes, rocks, grass, ferns, plus a procedural log.
// Each pack = materials + parts; each part = one mesh per material, stored relative to the part's pivot.
// Meshes are cached and shared (never destroyed); materials come from Mats.
public class LBPack
{
    public class Mat { public string name, texName; public Color color; public float gloss, metal; public bool emit, paint, tex, alpha; }
    public class Chunk { public int mat; public Mesh mesh, mirrored; }
    public class Part { public string name; public Vector3 pivot; public readonly List<Chunk> chunks = new List<Chunk>(); }

    public Mat[] mats;
    public readonly Dictionary<string, Part> parts = new Dictionary<string, Part>();
    public int tris;
    public string name;

    static readonly Dictionary<string, LBPack> cache = new Dictionary<string, LBPack>();
    static readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();

    public static LBPack Get(string name)
    {
        LBPack p;
        if (cache.TryGetValue(name, out p)) return p;
        p = null;
        try
        {
            var ta = Resources.Load<TextAsset>("LB/" + name);
            if (ta != null) p = Parse(name, ta.bytes);
            if (p != null) Debug.Log("LBPack " + name + ": " + p.parts.Count + " parts, " + p.tris + " tris");
            else Debug.LogWarning("LBPack " + name + " missing");
        }
        catch (System.Exception e) { Debug.LogWarning("LBPack " + name + " failed: " + e.Message); p = null; }
        cache[name] = p;
        return p;
    }

    static string Str(BinaryReader r) { int n = r.ReadByte(); return n == 0 ? "" : System.Text.Encoding.UTF8.GetString(r.ReadBytes(n)); }

    static LBPack Parse(string name, byte[] bytes)
    {
        var r = new BinaryReader(new MemoryStream(bytes));
        if (new string(r.ReadChars(4)) != "LBM1") throw new System.Exception("bad magic");
        var p = new LBPack { name = name };
        int nm = r.ReadByte();
        p.mats = new Mat[nm];
        for (int i = 0; i < nm; i++)
        {
            var m = new Mat();
            m.name = Str(r);
            byte cr = r.ReadByte(), cg = r.ReadByte(), cb = r.ReadByte(), ca = r.ReadByte();
            m.color = new Color32(cr, cg, cb, ca);
            m.gloss = r.ReadByte() / 255f; m.metal = r.ReadByte() / 255f;
            int fl = r.ReadByte();
            m.emit = (fl & 1) != 0; m.paint = (fl & 2) != 0; m.tex = (fl & 4) != 0; m.alpha = (fl & 8) != 0;
            m.texName = Str(r);
            p.mats[i] = m;
        }
        int np = r.ReadByte();
        for (int i = 0; i < np; i++)
        {
            var part = new Part();
            part.name = Str(r);
            part.pivot = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            int nc = r.ReadUInt16();
            for (int c = 0; c < nc; c++)
            {
                var ch = new Chunk();
                ch.mat = r.ReadByte();
                int vc = (int)r.ReadUInt32();
                var v = new Vector3[vc]; var n = new Vector3[vc];
                for (int k = 0; k < vc; k++) v[k] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                for (int k = 0; k < vc; k++) n[k] = new Vector3(r.ReadSByte() / 127f, r.ReadSByte() / 127f, r.ReadSByte() / 127f).normalized;
                Vector2[] uv = null;
                if (p.mats[ch.mat].tex)
                {
                    uv = new Vector2[vc];
                    for (int k = 0; k < vc; k++) uv[k] = new Vector2(Mathf.HalfToFloat(r.ReadUInt16()), Mathf.HalfToFloat(r.ReadUInt16()));
                }
                int ic = (int)r.ReadUInt32();
                bool big = r.ReadByte() != 0;
                var idx = new int[ic];
                for (int k = 0; k < ic; k++) idx[k] = big ? (int)r.ReadUInt32() : r.ReadUInt16();
                var mesh = new Mesh();
                mesh.name = "LB " + name + " " + part.name + " " + p.mats[ch.mat].name;
                if (vc > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.vertices = v; mesh.normals = n;
                if (uv != null) mesh.uv = uv;
                mesh.triangles = idx;
                mesh.RecalculateBounds();
                ch.mesh = mesh;
                p.tris += ic / 3;
                part.chunks.Add(ch);
            }
            p.parts[part.name] = part;
        }
        return p;
    }

    public bool Has(string part) { return parts.ContainsKey(part); }

    static readonly Color Cream = new Color(0.98f, 0.97f, 0.78f);

    // material rules: emit = unlit; paint = tint (frog skin uses the soft skin shader); frog "belly"/"toe" derive from the
    // tint; alpha = tinted glass; textures named leaf_* = alpha-cut foliage (wind sway); other textures = lit + colour.
    public Material MaterialFor(int i, Color tint)
    {
        Mat m = mats[i];
        if (m.emit) return Mats.Unlit(m.color);
        bool frog = name == "frog" || name == "bbfrog";
        if (m.paint) return frog ? Mats.Skin(tint, 0.45f) : Mats.Paint(tint, m.gloss);
        if (frog && m.name == "belly") return Mats.Skin(Color.Lerp(tint, Cream, 0.62f), 0.35f);
        if (frog && m.name == "toe") return Mats.Skin(Color.Lerp(tint, Color.black, 0.14f), 0.4f);
        if (m.alpha)
        {
            string k = "glass" + ColorUtility.ToHtmlStringRGBA(m.color);
            Material g;
            if (!matCache.TryGetValue(k, out g)) { g = Mats.GlassTint(m.color); matCache[k] = g; }
            return g;
        }
        if (m.tex)
        {
            bool leaf = m.name.StartsWith("leaf");
            string k = "tex" + m.texName + m.gloss + ColorUtility.ToHtmlStringRGB(m.color) + leaf;
            Material t;
            if (!matCache.TryGetValue(k, out t))
            {
                var tx = Resources.Load<Texture2D>("LB/" + m.texName);
                if (tx == null) t = Mats.PBR(m.color, m.gloss, m.metal);
                else if (leaf) t = Mats.Foliage(tx, m.color);
                else { t = Mats.Tex(tx, m.gloss); t.color = m.color; }
                matCache[k] = t;
            }
            return t;
        }
        return Mats.PBR(m.color, m.gloss, m.metal);
    }

    // highest point of the pack (pivot + chunk bounds), for scaling models to a target height
    public float Height()
    {
        float h = 0f;
        foreach (var p in parts.Values)
            foreach (var c in p.chunks) h = Mathf.Max(h, p.pivot.y + c.mesh.bounds.max.y);
        return h;
    }

    // every part of the pack as one static model under `parent` (props, trees): returns the root
    public Transform SpawnAll(Transform parent, Vector3 localPos, float scale, Color tint, float yaw = 0f, System.Func<Mat, Material> matFn = null)
    {
        var root = new GameObject("LB " + name).transform;
        root.SetParent(parent, false);
        root.localPosition = localPos;
        root.localRotation = Quaternion.Euler(0f, yaw, 0f);
        foreach (var k in parts.Keys) Spawn(k, root, Vector3.zero, scale, tint, false, matFn);
        return root;
    }

    static Mesh Mirror(Mesh src)
    {
        var m = new Mesh();
        m.name = src.name + " mirror";
        var v = src.vertices; var n = src.normals;
        for (int i = 0; i < v.Length; i++) { v[i].x = -v[i].x; n[i].x = -n[i].x; }
        var t = src.triangles;
        for (int i = 0; i < t.Length; i += 3) { int a = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = a; }
        m.indexFormat = src.indexFormat;
        m.vertices = v; m.normals = n;
        var uv = src.uv; if (uv != null && uv.Length == v.Length) m.uv = uv;
        m.triangles = t;
        m.RecalculateBounds();
        return m;
    }

    // Spawns part `name` under `parent`: the pack's origin sits at `origin` (parent space), scaled by `scale`.
    // The returned transform sits at the part's pivot (so rotating it turns the part about its pivot).
    public Transform Spawn(string partName, Transform parent, Vector3 origin, float scale, Color tint, bool mirrorX = false, System.Func<Mat, Material> matFn = null)
    {
        Part part;
        if (!parts.TryGetValue(partName, out part)) return null;
        var go = new GameObject("LB " + partName);
        go.transform.SetParent(parent, false);
        Vector3 pv = part.pivot; if (mirrorX) pv.x = -pv.x;
        go.transform.localPosition = origin + pv * scale;
        go.transform.localScale = Vector3.one * scale;
        foreach (var ch in part.chunks)
        {
            var cg = new GameObject(mats[ch.mat].name);
            cg.transform.SetParent(go.transform, false);
            Mesh mesh = ch.mesh;
            if (mirrorX) { if (ch.mirrored == null) ch.mirrored = Mirror(ch.mesh); mesh = ch.mirrored; }
            cg.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = cg.AddComponent<MeshRenderer>();
            mr.sharedMaterial = matFn != null ? (matFn(mats[ch.mat]) ?? MaterialFor(ch.mat, tint)) : MaterialFor(ch.mat, tint);
            if (mats[ch.mat].emit || mats[ch.mat].alpha) mr.shadowCastingMode = ShadowCastingMode.Off;
        }
        return go.transform;
    }
}
