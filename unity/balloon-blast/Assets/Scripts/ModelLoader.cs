using System;
using System.Collections.Generic;
using GLTFast;
using UnityEngine;

// bb6: soldiers are Kenney "Mini Characters" (CC0, animated: idle / walk / sprint / die / holding-both...), 8 variants
// shipped in StreamingAssets/chars, loaded once each with glTFast (legacy animation) and instantiated per soldier.
// If none load, falls back to the Mixamo X-Bot from earlier builds; if that fails too, soldiers keep capsule bodies.
public static class ModelLoader
{
    public const string Url = "https://assets.babylonjs.com/meshes/Xbot.glb";
    static readonly string[] Chars = { "character-male-a", "character-female-a", "character-male-b", "character-female-b",
                                       "character-male-c", "character-female-c", "character-male-d", "character-female-d" };
    static readonly List<GltfImport> kenney = new List<GltfImport>();
    static readonly List<Texture2D> kenneyTex = new List<Texture2D>();
    static GltfImport xbot;
    public static bool Ready, Failed, Loading;
    public static bool IsKenney { get { return kenney.Count > 0; } }
    static readonly List<Soldier> waiting = new List<Soldier>();

    public static async void Begin()
    {
        if (Loading || Ready) return;
        Loading = true;
        var settings = new ImportSettings();
        settings.AnimationMethod = AnimationMethod.Legacy;
        foreach (var c in Chars)
        {
            try
            {
                var g = new GltfImport();
                string url = Application.streamingAssetsPath + "/chars/" + c + ".glb";
                if (await g.Load(url, settings))
                {
                    kenney.Add(g);
                    Texture2D t = null;
                    try { t = g.GetTexture(0); } catch (Exception) { }
                    kenneyTex.Add(t);
                }
                else Debug.LogWarning("ModelLoader: " + c + " FAILED");
            }
            catch (Exception e) { Debug.LogWarning("ModelLoader: " + c + " exception " + e.Message); }
        }
        Debug.Log("ModelLoader: Kenney characters loaded " + kenney.Count + "/" + Chars.Length);
        if (kenney.Count == 0)
        {
            try
            {
                xbot = new GltfImport();
                bool ok = await xbot.Load(Url, settings);
                if (!ok) xbot = null;
                Debug.Log("ModelLoader: Xbot fallback " + (ok ? "ok" : "FAILED"));
            }
            catch (Exception e) { Debug.LogWarning("ModelLoader: Xbot exception " + e.Message); xbot = null; }
        }
        Ready = kenney.Count > 0 || xbot != null;
        Failed = !Ready;
        Loading = false;
        if (Ready) foreach (var s in waiting.ToArray()) if (s != null) Spawn(s);
        waiting.Clear();
    }

    public static void Request(Soldier s)
    {
        if (Ready) Spawn(s);
        else if (!Failed) waiting.Add(s);
    }

    static async void Spawn(Soldier s)
    {
        if (s == null) return;
        GameObject holder = new GameObject("ModelHolder");
        holder.transform.SetParent(s.transform, false);
        try
        {
            bool ok;
            Texture2D tex = null;
            if (kenney.Count > 0)
            {
                int v = ((s.id % kenney.Count) + kenney.Count) % kenney.Count;
                ok = await kenney[v].InstantiateMainSceneAsync(holder.transform);
                tex = kenneyTex[v];
            }
            else ok = await xbot.InstantiateMainSceneAsync(holder.transform);
            if (s == null || holder == null) return;
            if (!ok) { UnityEngine.Object.Destroy(holder); return; }
            s.AttachModel(holder, kenney.Count > 0, tex);
        }
        catch (Exception e)
        {
            Debug.LogWarning("ModelLoader: instantiate failed " + e.Message);
            if (holder != null) UnityEngine.Object.Destroy(holder);
        }
    }
}
