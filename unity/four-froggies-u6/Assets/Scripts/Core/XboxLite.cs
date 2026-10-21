using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

// ffxweb3 (2026-10-20): Xbox Edge low-memory lite path. Edge on Xbox Series shares ~1 GB across all its processes and kills
// the tab ("fatal memory exceeded"); the wasm heap of the full game sat at the 512 MB cap from 12 s. On SimXbox.lowmem only
// (Xbox UA or ?lowmem=1 on the page; ?lowmem=0 turns it off) the game takes the phone tier (Look.Mobile -> Realism lite,
// fewer trees / grass / leaf cards, LDR), Perf60 locked at its low tier, half-resolution textures (mip limit 1)
// and - once the world is built - drops the CPU-side copy of big static render meshes (UploadMeshData(true)),
// so the heap reuses that memory instead of growing. Meshes that anything may still read or rewrite are left alone:
// MeshCollider / skinned meshes, Breakable (Destruct / RanchBreak pieces), MarkDynamic'd or small meshes. ?xlfree=0 skips that step.
// Desktop, phones and the Tesla browser never run any of this.
public static class XboxLite
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern int FFXboxLowMem();
#else
    static int FFXboxLowMem() { return 0; }
#endif
    static int lm = -1;
    public static bool LowMem { get { if (lm < 0) { try { lm = FFXboxLowMem() == 1 ? 1 : 0; } catch { lm = 0; } } return lm == 1; } }

    // Look.Init (before anything is built)
    public static void Apply()
    {
        if (!LowMem) return;
        QualitySettings.globalTextureMipmapLimit = 1;     // half-resolution textures
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
        QualitySettings.lodBias = 0.7f;
        Debug.Log("XBOXLITE: on (phone tier, Perf60 low, mip limit 1, static mesh CPU copies freed)");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!LowMem) return;
        var go = new GameObject("XboxLite"); Object.DontDestroyOnLoad(go); go.hideFlags = HideFlags.HideInHierarchy;
        go.AddComponent<XboxLiteRunner>();
    }

    static bool Skip(GameObject g)
    {
        return g.GetComponentInParent<Breakable>() != null;   // RanchBreak is a static class (its pieces carry Breakable)
    }

    // drop the CPU copy of big static render meshes; returns bytes freed (approx)
    public static long FreeStatic(string when)
    {
        string u = FFPlatform.PageUrl;
        if (u.Contains("xlfree=0")) return 0;
        float t0 = Time.realtimeSinceStartup;
        var keep = new HashSet<Mesh>();
        foreach (var mc in Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (mc.sharedMesh != null) keep.Add(mc.sharedMesh);
        foreach (var sr in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (sr.sharedMesh != null) keep.Add(sr.sharedMesh);
        long freed = 0, readable = 0; int n = 0;
        var done = new HashSet<Mesh>();
        foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var m = mf.sharedMesh;
            if (m == null || !m.isReadable || done.Contains(m)) continue;
            done.Add(m);
            long b = (long)m.vertexCount * 48 + (long)m.GetIndexCount(0) * 4;
            readable += b;
            if (keep.Contains(m) || m.vertexCount < 1500 || mf.GetComponent<MeshRenderer>() == null || Skip(mf.gameObject)) continue;
            try { m.UploadMeshData(true); freed += b; n++; } catch (System.Exception e) { Debug.LogWarning("XBOXLITE: " + m.name + " " + e.Message); }
        }
        Debug.Log("XBOXLITE: " + when + " freed CPU copies of " + n + " static meshes ~" + (freed >> 20) + " MB (readable before ~" + (readable >> 20) + " MB) in " +
            ((Time.realtimeSinceStartup - t0) * 1000f).ToString("0") + " ms");
        return freed;
    }
}

public class XboxLiteRunner : MonoBehaviour
{
    IEnumerator Start()
    {
        // after Bootstrap's world build + the first frames (late spawns / merges), then after Realism / Terrain56 finish
        yield return new WaitForSecondsRealtime(6f); XboxLite.FreeStatic("6 s");
        yield return new WaitForSecondsRealtime(30f); XboxLite.FreeStatic("36 s");
        yield return new WaitForSecondsRealtime(90f); XboxLite.FreeStatic("126 s");
    }
}
