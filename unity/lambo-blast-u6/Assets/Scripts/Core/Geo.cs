using System.Collections.Generic;
using UnityEngine;

// Small procedural mesh kit: tapered blocks (the Lambo body is made of these), ribbons and wedges.
public static class Geo
{
    // A six-sided block symmetric about x = cx, running along z from zb (back) to zf (front).
    // Each end has its own bottom width, top width, bottom height and top height, so one call
    // makes a wedge, a tapered nose or a sloped deck. Flat shaded.
    public static GameObject Block(Transform parent, Material m, float cx, float zb, float zf,
        float wbB, float wtB, float ybB, float ytB, float wbF, float wtF, float ybF, float ytF)
    {
        var c = new Vector3[8];
        c[0] = new Vector3(cx - wbB / 2, ybB, zb); c[1] = new Vector3(cx + wbB / 2, ybB, zb);
        c[2] = new Vector3(cx + wtB / 2, ytB, zb); c[3] = new Vector3(cx - wtB / 2, ytB, zb);
        c[4] = new Vector3(cx - wbF / 2, ybF, zf); c[5] = new Vector3(cx + wbF / 2, ybF, zf);
        c[6] = new Vector3(cx + wtF / 2, ytF, zf); c[7] = new Vector3(cx - wtF / 2, ytF, zf);
        return Hexa(parent, m, c, "Block");
    }

    // 8 corners: 0-3 back face (bl, br, tr, tl), 4-7 front face (bl, br, tr, tl)
    public static GameObject Hexa(Transform parent, Material m, Vector3[] c, string name)
    {
        int[][] faces = {
            new[] { 0, 1, 2, 3 }, new[] { 5, 4, 7, 6 }, new[] { 4, 0, 3, 7 },
            new[] { 1, 5, 6, 2 }, new[] { 3, 2, 6, 7 }, new[] { 4, 5, 1, 0 } };
        Vector3 centre = Vector3.zero;
        foreach (var p in c) centre += p;
        centre /= 8f;
        var v = new List<Vector3>();
        var t = new List<int>();
        foreach (int[] f in faces)
        {
            Vector3 a = c[f[0]], b = c[f[1]], cc = c[f[2]], d = c[f[3]];
            Vector3 n = Vector3.Cross(b - a, cc - a) + Vector3.Cross(cc - a, d - a);
            if (n.sqrMagnitude < 1e-10f) continue;          // degenerate face (a point / an edge)
            Vector3 fc = (a + b + cc + d) * 0.25f;
            bool flip = Vector3.Dot(n, fc - centre) < 0f;
            int s = v.Count;
            v.Add(a); v.Add(b); v.Add(cc); v.Add(d);
            if (!flip) { t.Add(s); t.Add(s + 2); t.Add(s + 1); t.Add(s); t.Add(s + 3); t.Add(s + 2); }
            else { t.Add(s); t.Add(s + 1); t.Add(s + 2); t.Add(s); t.Add(s + 2); t.Add(s + 3); }
        }
        var mesh = new Mesh();
        mesh.SetVertices(v);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        // Unity is left-handed: make sure normals point outwards (flip the whole thing if needed)
        Vector3[] nr = mesh.normals;
        float score = 0f;
        for (int i = 0; i < v.Count; i++) score += Vector3.Dot(nr[i], v[i] - centre);
        if (score < 0f)
        {
            for (int i = 0; i < t.Count; i += 3) { int k = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = k; }
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
        }
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = m;
        return go;
    }

    // simple ramp: rises from 0 at the back (z = 0) to h at the front (z = len), width w
    public static Mesh Wedge(float w, float len, float h)
    {
        float x = w / 2;
        var c = new[] {
            new Vector3(-x, 0, 0), new Vector3(x, 0, 0), new Vector3(x, 0.02f, 0), new Vector3(-x, 0.02f, 0),
            new Vector3(-x, 0, len), new Vector3(x, 0, len), new Vector3(x, h, len), new Vector3(-x, h, len) };
        GameObject tmp = Hexa(null, null, c, "tmp");
        Mesh m = tmp.GetComponent<MeshFilter>().sharedMesh;
        Object.Destroy(tmp);
        return m;
    }
}
