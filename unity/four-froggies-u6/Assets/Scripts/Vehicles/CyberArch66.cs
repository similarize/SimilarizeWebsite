using System.Collections.Generic;
using UnityEngine;

// ffct66 (Ben 2026-10-24: "the fenders are way too big for the wheels ... when the wheels expand they should tilt out and
// the wheel wells get a lot bigger"). Runs on the Cybertruck66 body (Resources/LB/cybertruck66.bytes, Tools/ct66/build_ct66.py):
// the outer body / flare vertices around each wheel are pulled in radially (side plane) so the arches hug the stock 37" tyres,
// and as BigWheels grows the tyres the arch opening follows them out (up to ~0.88 m) so big tyres sit in big wells.
// Per-truck mesh copies (the pack meshes are shared); rebuilt only when the opening changes. &ffct0=1 / &ff77old=1 = old truck.
public class CyberArch66 : MonoBehaviour
{
    static int old = -1;
    public static bool Old { get { if (old < 0) old = (Ffu33.Url("ffct0") == "1" || Ffu33.Url("ff77old") == "1") ? 1 : 0; return old == 1; } }

    public const float TyreR = 0.47f;          // 37 in tyre (stock pack wheel 0.446 m x 1.054)
    const float A = 0.50f, Re = 0.72f, D = 1.0f, Snug = 0.55f, Open = 0.88f;
    GroundVehicle gv;
    readonly List<Mesh> meshes = new List<Mesh>();
    readonly List<Vector3[]> src = new List<Vector3[]>();
    float cy = 0.439f; float[] wz;
    float cur = -1f;

    public static void Attach(GroundVehicle v, Transform body, float centreY, float[] wheelZ)
    {
        if (v == null || body == null) return;
        var c = v.gameObject.AddComponent<CyberArch66>();
        c.gv = v; c.cy = centreY; c.wz = wheelZ;
        foreach (var mf in body.GetComponentsInChildren<MeshFilter>(true))
        {
            string n = mf.gameObject.name;
            if (n == "seat" || n == "cabin" || mf.sharedMesh == null) continue;
            Mesh m = Instantiate(mf.sharedMesh);
            m.name = mf.sharedMesh.name + " arch66";
            mf.sharedMesh = m;
            c.meshes.Add(m); c.src.Add(m.vertices);
        }
        c.Apply(Snug);
    }

    void LateUpdate()
    {
        if (gv == null) return;
        float r = BigWheels.RFor(TyreR, gv.bigK);
        float want = Mathf.Clamp(Snug + (r - TyreR) * 1.1f, Snug, Open);
        if (Mathf.Abs(want - cur) > 0.006f) Apply(want);
    }

    float Map(float d, float rn)
    {
        if (d <= A) return d;
        if (d <= Re) return A + (d - A) * (rn - A) / (Re - A);
        float dd = Mathf.Max(D, rn + 0.15f);
        if (d < dd) return rn + (d - Re) * (dd - rn) / (dd - Re);
        return d;
    }

    void Apply(float rn)
    {
        cur = rn;
        for (int i = 0; i < meshes.Count; i++)
        {
            Vector3[] s = src[i];
            var v = new Vector3[s.Length];
            for (int k = 0; k < s.Length; k++)
            {
                Vector3 p = s[k];
                float wx = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Mathf.Abs(p.x) - 0.6f) / 0.2f));
                if (wx > 0f)
                    foreach (float z in wz)
                    {
                        float dy = p.y - cy, dz = p.z - z, d = Mathf.Sqrt(dy * dy + dz * dz);
                        if (d < 1e-5f || d >= Mathf.Max(D, rn + 0.15f)) continue;
                        float sc = 1f + (Map(d, rn) / d - 1f) * wx;
                        p.y = cy + dy * sc; p.z = z + dz * sc;
                    }
                v[k] = p;
            }
            meshes[i].vertices = v;
            meshes[i].RecalculateBounds();
        }
    }
}
