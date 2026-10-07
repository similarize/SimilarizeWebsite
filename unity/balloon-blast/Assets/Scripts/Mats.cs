using System.Collections.Generic;
using UnityEngine;

public static class Mats
{
    static Material lit, unlit, fx;
    static readonly Dictionary<Color32, Material> litCache = new Dictionary<Color32, Material>();
    static readonly Dictionary<Color32, Material> shinyCache = new Dictionary<Color32, Material>();
    static readonly Dictionary<Color32, Material> unlitCache = new Dictionary<Color32, Material>();

    public static Material Fx { get { return fx; } }

    public static void Init(Material litMat, Material unlitMat, Material fxMat)
    {
        lit = litMat != null ? litMat : new Material(Shader.Find("Standard"));
        unlit = unlitMat != null ? unlitMat : lit;
        fx = fxMat != null ? fxMat : unlit;
    }

    public static Material Lit(Color c)
    {
        Color32 k = c;
        Material m;
        if (!litCache.TryGetValue(k, out m))
        {
            m = new Material(lit);
            m.color = c;
            m.SetFloat("_Glossiness", 0.12f);
            litCache[k] = m;
        }
        return m;
    }

    public static Material Shiny(Color c)
    {
        Color32 k = c;
        Material m;
        if (!shinyCache.TryGetValue(k, out m))
        {
            m = new Material(lit);
            m.color = c;
            m.SetFloat("_Glossiness", 0.75f);
            shinyCache[k] = m;
        }
        return m;
    }

    public static Material Unlit(Color c)
    {
        Color32 k = c;
        Material m;
        if (!unlitCache.TryGetValue(k, out m))
        {
            m = new Material(unlit);
            m.color = c;
            unlitCache[k] = m;
        }
        return m;
    }

    public static GameObject Prim(PrimitiveType t, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool keepCollider = true)
    {
        GameObject g = GameObject.CreatePrimitive(t);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        if (!keepCollider)
        {
            Collider c = g.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
        }
        return g;
    }

    public static void SetLayer(GameObject g, int layer)
    {
        g.layer = layer;
        foreach (Transform t in g.transform) SetLayer(t.gameObject, layer);
    }
}
