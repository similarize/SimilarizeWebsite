using System.Collections.Generic;
using UnityEngine;

// Material cache + primitive helpers. Base materials come from editor-made assets (so their shader variants ship).
public static class Mats
{
    static Material lit, unlit, fx, water, glass;
    static readonly Dictionary<long, Material> cache = new Dictionary<long, Material>();

    public static Material Fx { get { return fx; } }
    // graphics overhaul bases (editor-made assets so the shaders ship); any may be null -> Standard fallbacks
    public static Material SkinBase, FoliageBase, PondBase, GrassDetailBase, UnderwaterBase, UnlitTexBase;
    public static Material Water { get { return water; } }
    public static Material Glass { get { return glass; } }

    public static void Init(Material litMat, Material unlitMat, Material fxMat, Material waterMat, Material glassMat)
    {
        lit = litMat != null ? litMat : new Material(Shader.Find("Standard"));
        unlit = unlitMat != null ? unlitMat : lit;
        fx = fxMat != null ? fxMat : unlit;
        water = waterMat != null ? waterMat : Lit(new Color(0.2f, 0.45f, 0.6f));
        glass = glassMat != null ? glassMat : Shiny(new Color(0.2f, 0.3f, 0.4f));
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
        else if (kind == 1) { m.SetFloat("_Glossiness", 0.72f); m.SetFloat("_Metallic", 0.55f); }
        else if (kind == 3) { m.SetFloat("_Glossiness", 0.82f); m.SetFloat("_Metallic", 0.85f); }
        else if (kind >= 1000) { int q = kind - 1000; m.SetFloat("_Glossiness", (q / 21) / 20f); m.SetFloat("_Metallic", (q % 21) / 20f); }   // PBR
        else if (kind >= 10) { m.SetFloat("_Glossiness", (kind - 10) / 20f); m.SetFloat("_Metallic", 0.12f); }   // Paint
        cache[k] = m;
        return m;
    }

    public static Material Lit(Color c) { return Get(c, 0); }
    public static Material Shiny(Color c) { return Get(c, 1); }
    public static Material Unlit(Color c) { return Get(c, 2); }
    public static Material Steel(Color c) { return Get(c, 3); }

    // glossy / satin painted or moulded surface (robot shells, accents): low metal, smoothness 0..1
    public static Material Paint(Color c, float gloss = 0.85f) { return Get(c, 10 + Mathf.Clamp(Mathf.RoundToInt(gloss * 20f), 0, 20)); }

    // Standard with chosen smoothness + metallic (quantised to 1/20)
    public static Material PBR(Color c, float gloss, float metal)
    {
        int g = Mathf.Clamp(Mathf.RoundToInt(gloss * 20f), 0, 20), mt = Mathf.Clamp(Mathf.RoundToInt(metal * 20f), 0, 20);
        return Get(c, 1000 + g * 21 + mt);
    }

    // a lit (Standard) material with a texture
    public static Material Tex(Texture t, float gloss = 0.1f)
    {
        var m = new Material(lit);
        m.color = Color.white;
        m.mainTexture = t;
        m.SetFloat("_Glossiness", gloss);
        return m;
    }

    // textured + tinted (wood, siding, metal sheet), cached per texture + colour + tiling
    static readonly Dictionary<string, Material> texCache = new Dictionary<string, Material>();
    // `metres` = size of one texture repeat: textured cubes made by Prim get box-projected UVs in metres (BoxUV)
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

    // tinted transparent copy of the glass material
    public static Material GlassTint(Color c)
    {
        var m = new Material(glass);
        m.color = c;
        return m;
    }

    // soft stylised skin (FF/Skin: wrapped diffuse, warm terminator, rim, soft highlight) for the frogs
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

    // alpha-cut, double-sided foliage with a gentle wind sway (FF/Foliage); Standard cutout-less fallback
    public static Material Foliage(Texture t, Color c)
    {
        Material m;
        if (FoliageBase == null) { m = Tex(t, 0.05f); m.color = c; return m; }
        m = new Material(FoliageBase);
        m.mainTexture = t;
        m.color = c;
        return m;
    }

    public static Color Hex(string hex)
    {
        Color c;
        return ColorUtility.TryParseHtmlString(hex, out c) ? c : Color.magenta;
    }

    public static GameObject Prim(PrimitiveType t, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool keepCollider = false)
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
            if (c != null) Object.Destroy(c);
        }
        return g;
    }

    public static GameObject Prim(PrimitiveType t, Transform parent, Vector3 localPos, Vector3 scale, Vector3 euler, Material mat, bool keepCollider = false)
    {
        GameObject g = Prim(t, parent, localPos, scale, mat, keepCollider);
        g.transform.localRotation = Quaternion.Euler(euler);
        return g;
    }

    // replaces a cube's 0..1-per-face UVs with box-projected UVs in metres (by its scale), so textures tile instead of
    // stretching over long walls / rails (the material's texture scale sets metres per repeat)
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

    // underwater variant of a lit material (FF/Underwater caustics), cached per source material
    static readonly Dictionary<Material, Material> uwCache = new Dictionary<Material, Material>();
    public static Material Underwater(Material src, float surfaceY)
    {
        if (UnderwaterBase == null || src == null || src.shader == null || src.shader.name != "Standard" || src.renderQueue >= 2450) return src;
        Material m;
        if (uwCache.TryGetValue(src, out m)) return m;
        m = new Material(UnderwaterBase);
        m.color = src.color;
        if (src.mainTexture != null) { m.mainTexture = src.mainTexture; m.mainTextureScale = src.mainTextureScale; }
        var ct = Resources.Load<Texture2D>("LB/caustics");
        if (ct != null) m.SetTexture("_Caustics", ct);
        m.SetFloat("_SurfaceY", surfaceY);
        uwCache[src] = m;
        return m;
    }

    // unlit textured (sky spheres / far planets); Unlit/Color tint fallback
    public static Material UnlitTex(Texture t)
    {
        if (UnlitTexBase == null || t == null) return Unlit(Color.white);
        var m = new Material(UnlitTexBase);
        m.mainTexture = t;
        return m;
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

    public static void NoReceive(GameObject g)
    {
        foreach (var r in g.GetComponentsInChildren<Renderer>()) r.receiveShadows = false;
    }
}
