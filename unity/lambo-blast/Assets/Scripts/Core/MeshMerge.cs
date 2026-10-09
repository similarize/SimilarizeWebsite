using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Merges every static MeshRenderer under a root into one mesh per material (keeps colliders).
// Cuts the draw calls of the primitive-built ranch from hundreds to a few dozen, which matters
// in split-screen where every camera draws the whole scene.
public static class MeshMerge
{
    const float CellSize = 60f;

    public static void Merge(Transform root, bool castShadows) { Merge(root, castShadows, true); }

    public const string MeshName = "LBMerged";

    public static void Merge(Transform root, bool castShadows, bool log)
    {
        var groups = new Dictionary<KeyValuePair<Material, int>, List<CombineInstance>>();
        var filters = root.GetComponentsInChildren<MeshFilter>();
        Matrix4x4 toRoot = root.worldToLocalMatrix;
        var done = new List<MeshFilter>();
        foreach (MeshFilter mf in filters)
        {
            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            if (mr == null || mf.sharedMesh == null || !mr.enabled) continue;
            if (!mf.sharedMesh.isReadable) continue;
            Material m = mr.sharedMaterial;
            if (m == null) continue;
            Vector3 wp = mr.bounds.center;
            int cell = Mathf.FloorToInt((wp.x + 400f) / CellSize) * 1000 + Mathf.FloorToInt((wp.z + 400f) / CellSize);
            var key = new KeyValuePair<Material, int>(m, cell);
            List<CombineInstance> l;
            if (!groups.TryGetValue(key, out l)) { l = new List<CombineInstance>(); groups[key] = l; }
            l.Add(new CombineInstance { mesh = mf.sharedMesh, transform = toRoot * mf.transform.localToWorldMatrix });
            done.Add(mf);
        }
        int made = 0;
        foreach (var kv in groups)
        {
            List<CombineInstance> all = kv.Value;
            // split into chunks to stay well inside vertex limits
            int start = 0;
            while (start < all.Count)
            {
                int verts = 0, end = start;
                while (end < all.Count && verts + all[end].mesh.vertexCount < 60000) { verts += all[end].mesh.vertexCount; end++; }
                if (end == start) end = start + 1;
                var mesh = new Mesh();
                mesh.name = MeshName;
                mesh.indexFormat = IndexFormat.UInt32;
                mesh.CombineMeshes(all.GetRange(start, end - start).ToArray(), true, true);
                mesh.RecalculateBounds();
                var go = new GameObject("Merged " + kv.Key.Key.name + " " + made);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = kv.Key.Key;
                r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                made++;
                start = end;
            }
        }
        foreach (MeshFilter mf in done)
        {
            var mr = mf.GetComponent<MeshRenderer>();
            if (mr != null) Object.Destroy(mr);
            Object.Destroy(mf);
        }
        if (log) Debug.Log("MeshMerge " + root.name + ": " + done.Count + " parts -> " + made + " meshes");
    }

    // destroys a model built at runtime together with the meshes MeshMerge made for it (no leak on rebuilds)
    public static void DestroyWithMeshes(GameObject g)
    {
        if (g == null) return;
        foreach (MeshFilter mf in g.GetComponentsInChildren<MeshFilter>(true))
            if (mf.sharedMesh != null && mf.sharedMesh.name == MeshName) Object.Destroy(mf.sharedMesh);
        Object.Destroy(g);
    }
}
