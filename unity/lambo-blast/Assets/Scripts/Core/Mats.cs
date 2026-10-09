using System.Collections.Generic;
using UnityEngine;

// Material cache + primitive helpers (copied from Four Froggies Unity). Base materials come from editor-made assets (so their shader variants ship).
public static class Mats
{
    static Material lit, unlit, fx, water, glass;
    static readonly Dictionary<long, Material> cache = new Dictionary<long, Material>();

    public static Material Fx { get { return fx; } }
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
        cache[k] = m;
        return m;
    }

    public static Material Lit(Color c) { return Get(c, 0); }
    public static Material Shiny(Color c) { return Get(c, 1); }
    public static Material Unlit(Color c) { return Get(c, 2); }
    public static Material Steel(Color c) { return Get(c, 3); }

    // a lit (Standard) material with a generated texture
    public static Material Tex(Texture2D t, float gloss = 0.1f)
    {
        var m = new Material(lit);
        m.color = Color.white;
        m.mainTexture = t;
        m.SetFloat("_Glossiness", gloss);
        return m;
    }

    // tinted transparent copy of the glass material (shield bubble)
    public static Material GlassTint(Color c)
    {
        var m = new Material(glass);
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
