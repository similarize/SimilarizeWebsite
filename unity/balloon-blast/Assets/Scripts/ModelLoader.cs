using System;
using System.Collections.Generic;
using GLTFast;
using UnityEngine;

// Loads the Mixamo X-Bot (with idle/walk/run clips) once at runtime with glTFast and instantiates it per soldier.
// If loading fails, soldiers keep their procedural capsule bodies.
public static class ModelLoader
{
    public const string Url = "https://assets.babylonjs.com/meshes/Xbot.glb";
    static GltfImport gltf;
    public static bool Ready, Failed, Loading;
    static readonly List<Soldier> waiting = new List<Soldier>();

    public static async void Begin()
    {
        if (Loading || Ready) return;
        Loading = true;
        try
        {
            gltf = new GltfImport();
            var settings = new ImportSettings();
            settings.AnimationMethod = AnimationMethod.Legacy;
            bool ok = await gltf.Load(Url, settings);
            Ready = ok;
            Failed = !ok;
            Debug.Log("ModelLoader: Xbot load " + (ok ? "ok" : "FAILED"));
        }
        catch (Exception e)
        {
            Debug.LogWarning("ModelLoader: Xbot load exception " + e.Message);
            Failed = true;
        }
        Loading = false;
        if (Ready)
        {
            foreach (var s in waiting.ToArray()) if (s != null) Spawn(s);
        }
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
        GameObject holder = new GameObject("XbotHolder");
        holder.transform.SetParent(s.transform, false);
        try
        {
            bool ok = await gltf.InstantiateMainSceneAsync(holder.transform);
            if (s == null || holder == null) return;
            if (!ok) { UnityEngine.Object.Destroy(holder); return; }
            s.AttachModel(holder);
        }
        catch (Exception e)
        {
            Debug.LogWarning("ModelLoader: instantiate failed " + e.Message);
            if (holder != null) UnityEngine.Object.Destroy(holder);
        }
    }
}
