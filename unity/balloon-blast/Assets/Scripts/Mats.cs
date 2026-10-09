using System.Collections.Generic;
using UnityEngine;

// Material cache + primitive helpers. Base materials come from editor-made assets (so their shader variants ship).
// Graphics overhaul: Paint / PBR / textured (box-projected UVs) materials, FF/Skin for the critters, FF/Foliage for
// the Quaternius leaves, BB/Balloon for the glossy translucent balloons (all with Standard fallbacks).
public static class Mats
{
    static Material lit, unlit, fx, glass;
    static readonly Dictionary<long, Material> cache = new Dictionary<long, Material>();

    public static Material Fx { get { return fx; } }
    public static Material SkinBase, FoliageBase, BalloonBase;

    public static void Init(Material litMat, Material unlitMat, Material fxMat)
    {
        lit = litMat != null ? litMat : new Material(Shader.Find("Standard"));
        unlit = unlitMat != null ? unlitMat : lit;
        fx = fxMat != null ? fxMat : unlit;
    }

    static long Key(Color c, int kind)
    {
        Color32 k = c;
        return ((long)kind << 32) | ((long)k.r << 24) | ((long)k.g << 16) | ((long)k.b << 8) | k.a;
    }

    static Material Get(Color c, int kind)
    {
        long k = Key(c, kind);
        Material m;
        if (cache.TryGetValue(k, out m)) return m;
        m = new Material(kind == 2 ? unlit : lit);
        m.color = c;
        if (kind == 0) m.SetFloat("_Glossiness", 0.12f);
        else if (kind == 1) m.SetFloat("_Glossiness", 0.75f);
        else if (kind >= 1000) { int q = kind - 1000; m.SetFloat("_Glossiness", (q / 21) / 20f); m.SetFloat("_Metallic", (q % 21) / 20f); }   // PBR
        else if (kind >= 10) { m.SetFloat("_Glossiness", (kind - 10) / 20f); m.SetFloat("_Metallic", 0.08f); }   // Paint
        cache[k] = m;
        return m;
    }

    public static Material Lit(Color c) { return Get(c, 0); }
    public static Material Shiny(Color c) { return Get(c, 1); }
    public static Material Unlit(Color c) { return Get(c, 2); }

    // glossy / satin painted or moulded surface: low metal, smoothness 0..1
    public static Material Paint(Color c, float gloss = 0.85f) { return Get(c, 10 + Mathf.Clamp(Mathf.RoundToInt(gloss * 20f), 0, 20)); }

    // Standard with chosen smoothness + metallic (quantised to 1/20)
    public static Material PBR(Color c, float gloss, float metal)
    {
        int g = Mathf.Clamp(Mathf.RoundToInt(gloss * 20f), 0, 20), mt = Mathf.Clamp(Mathf.RoundToInt(metal * 20f), 0, 20);
        return Get(c, 1000 + g * 21 + mt);
    }

    public static Material Tex(Texture t, float gloss = 0.1f)
    {
        var m = new Material(lit);
        m.color = Color.white;
        m.mainTexture = t;
        m.SetFloat("_Glossiness", gloss);
        return m;
    }

    // textured + tinted (wood, hay, siding, metal), cached per texture + colour + tiling. `metres` = size of one repeat:
    // textured cubes made by Prim get box-projected UVs in metres (BoxUV)
    static readonly Dictionary<string, Material> texCache = new Dictionary<string, Material>();
    public static Material TexTint(string res, Color c, float gloss = 0.1f, float metres = 2f)
    {
        string k = res + ColorUtility.ToHtmlStringRGBA(c) + gloss + "_" + metres;
        Material m;
        if (texCache.TryGetValue(k, out m)) return m;
        var t = Resources.Load<Texture2D>(res);
        if (t == null) m = Lit(c);
        else { m = Tex(t, gloss); m.color = c; m.mainTextureScale = new Vector2(1f / metres, 1f / metres); }
        texCache[k] = m;
        return m;
    }

    // same, but the UV repeat is given directly (for cylinders / meshes with 0..1 UVs)
    public static Material TexTiled(string res, Color c, float gloss, Vector2 tiling)
    {
        string k = res + ColorUtility.ToHtmlStringRGBA(c) + gloss + "_t" + tiling.x + "x" + tiling.y;
        Material m;
        if (texCache.TryGetValue(k, out m)) return m;
        var t = Resources.Load<Texture2D>(res);
        if (t == null) m = Lit(c);
        else { m = Tex(t, gloss); m.color = c; m.mainTextureScale = tiling; }
        texCache[k] = m;
        return m;
    }

    // tinted glass (red-dot lens): glossy paint stand-in (the field has no transparent glass material)
    public static Material GlassTint(Color c)
    {
        if (glass == null) glass = PBR(new Color(0.45f, 0.75f, 0.95f), 0.95f, 0.3f);
        return PBR(new Color(c.r, c.g, c.b, 1f), 0.95f, 0.3f);
    }

    // soft stylised skin (FF/Skin: wrapped diffuse, warm terminator, rim, soft highlight) for the critters
    static readonly Dictionary<long, Material> skinCache = new Dictionary<long, Material>();
    public static Material Skin(Color c, float gloss = 0.45f)
    {
        long k = Key(c, 77) ^ ((long)Mathf.RoundToInt(gloss * 20f) << 40);
        Material m;
        if (skinCache.TryGetValue(k, out m)) return m;
        if (SkinBase == null) m = Paint(c, gloss * 0.9f);
        else { m = new Material(SkinBase); m.color = c; m.SetFloat("_Gloss", gloss); }
        skinCache[k] = m;
        return m;
    }

    // alpha-cut, double-sided foliage with a gentle wind sway (FF/Foliage)
    public static Material Foliage(Texture t, Color c)
    {
        Material m;
        if (FoliageBase == null) { m = Tex(t, 0.05f); m.color = c; return m; }
        m = new Material(FoliageBase);
        m.mainTexture = t;
        m.color = c;
        return m;
    }

    // glossy translucent latex balloon (BB/Balloon); falls back to the old shiny opaque material
    static readonly Dictionary<long, Material> balloonCache = new Dictionary<long, Material>();
    public static Material Balloon(Color c)
    {
        long k = Key(c, 88);
        Material m;
        if (balloonCache.TryGetValue(k, out m)) return m;
        if (BalloonBase == null) m = Shiny(c);
        else { m = new Material(BalloonBase); m.color = c; }
        balloonCache[k] = m;
        return m;
    }

    public static GameObject Prim(PrimitiveType t, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool keepCollider = true)
    {
        GameObject g = GameObject.CreatePrimitive(t);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        if (t == PrimitiveType.Cube && mat != null && mat.HasProperty("_MainTex") && mat.mainTexture != null) BoxUV(g);
        if (!keepCollider)
        {
            Collider c = g.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
        }
        return g;
    }

    // replaces a cube's 0..1-per-face UVs with box-projected UVs in metres (by its scale), so textures tile instead of
    // stretching (the material's texture scale sets metres per repeat)
    static Mesh cubeMesh;
    public static void BoxUV(GameObject g)
    {
        var mf = g.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        if (cubeMesh == null) cubeMesh = mf.sharedMesh;
        Vector3 s = g.transform.lossyScale;
        var src = cubeMesh;
        var v = src.vertices; var n = src.normals; var uv = new Vector2[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            Vector3 p = Vector3.Scale(v[i], s), a = n[i];
            if (Mathf.Abs(a.x) > 0.5f) uv[i] = new Vector2(p.z * Mathf.Sign(a.x), p.y);
            else if (Mathf.Abs(a.y) > 0.5f) uv[i] = new Vector2(p.x, p.z * Mathf.Sign(a.y));
            else uv[i] = new Vector2(-p.x * Mathf.Sign(a.z), p.y);
        }
        var m = new Mesh { name = "BoxUV" };
        m.vertices = v; m.normals = n; m.uv = uv; m.triangles = src.triangles; m.tangents = src.tangents;
        m.RecalculateBounds();
        mf.sharedMesh = m;
    }

    public static Transform Node(Transform parent, string name, Vector3 localPos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localPos;
        return t;
    }

    public static void SetLayer(GameObject g, int layer)
    {
        g.layer = layer;
        foreach (Transform t in g.transform) SetLayer(t.gameObject, layer);
    }

    public static void NoShadows(GameObject g)
    {
        foreach (var r in g.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
