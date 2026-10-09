using UnityEngine;

// Graphics overhaul gear: the airsoft rifle mesh (Resources/LB/rifle.bytes, work/lb-gfx/bb/build_rifle.py) used as the
// first-person view model (with gloved hands) and as the third-person gun, and the latex balloon mesh (lathed teardrop
// with a knot) drawn with BB/Balloon.
public static class Gear
{
    // rifle space: origin = right hand on the grip, +z forward; the orange tip ends at z 0.678, bore at y 0.078
    public static readonly Vector3 MuzzleLocal = new Vector3(0f, 0.078f, 0.69f);

    // builds the rifle under `parent`; returns the root (at the grip) and the muzzle tip in `muzzle`
    public static Transform Rifle(Transform parent, Vector3 localPos, float scale, Color accent, bool hands, Color glove, Color sleeve, out Transform muzzle)
    {
        var root = new GameObject("Rifle").transform;
        root.SetParent(parent, false);
        root.localPosition = localPos;
        root.localScale = Vector3.one * scale;
        muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(root, false);
        muzzle.localPosition = MuzzleLocal;
        LBPack p = LBPack.Get("rifle");
        System.Func<LBPack.Mat, Material> mf = m =>
        {
            if (m.name == "accent") return Mats.Paint(accent, 0.72f);
            if (m.name == "glove") return Mats.Paint(glove, 0.25f);
            if (m.name == "sleeve") return Mats.Paint(sleeve, 0.12f);
            return null;
        };
        if (p != null && p.Has("gun"))
        {
            p.Spawn("gun", root, Vector3.zero, 1f, accent, false, mf);
            if (hands && p.Has("hands")) p.Spawn("hands", root, Vector3.zero, 1f, accent, false, mf);
        }
        else
        {
            // old box gun fallback
            Color dark = new Color(0.16f, 0.17f, 0.19f);
            Mats.Prim(PrimitiveType.Cube, root, new Vector3(0f, 0.06f, 0.2f), new Vector3(0.06f, 0.08f, 0.5f), Mats.Lit(dark), false);
            Mats.Prim(PrimitiveType.Cube, root, new Vector3(0f, -0.03f, 0f), new Vector3(0.04f, 0.12f, 0.06f), Mats.Lit(dark), false);
            Mats.Prim(PrimitiveType.Cube, root, new Vector3(0f, -0.02f, 0.12f), new Vector3(0.035f, 0.13f, 0.06f), Mats.Lit(accent), false);
            Mats.Prim(PrimitiveType.Cube, root, new Vector3(0f, 0.06f, 0.47f), new Vector3(0.065f, 0.065f, 0.05f), Mats.Lit(new Color(1f, 0.45f, 0.05f)), false);
            muzzle.localPosition = new Vector3(0f, 0.06f, 0.5f);
        }
        return root;
    }

    static Mesh balloon;
    // unit balloon matching the old sphere primitive (radius 0.5, centre at 0) with a knot hanging to y -0.6
    public static Mesh BalloonMesh
    {
        get
        {
            if (balloon != null) return balloon;
            const int seg = 28, rings = 26;
            var prof = new Vector2[rings + 5];   // (radius, y) from the top down
            for (int i = 0; i <= rings; i++)
            {
                float th = Mathf.PI * i / rings;                       // 0 top .. pi bottom
                float r = 0.5f * Mathf.Sin(th);
                float taper = Mathf.Clamp01((th - 1.55f) / 1.59f);     // narrows into the neck
                r *= 1f - 0.42f * taper * taper;
                float y = 0.53f * Mathf.Cos(th) - 0.07f * taper * taper * taper;
                prof[i] = new Vector2(r, y + 0.02f);
            }
            // neck + knot
            float yb = prof[rings].y;
            prof[rings] = new Vector2(0.035f, yb);
            prof[rings + 1] = new Vector2(0.05f, yb - 0.025f);
            prof[rings + 2] = new Vector2(0.06f, yb - 0.055f);
            prof[rings + 3] = new Vector2(0.035f, yb - 0.085f);
            prof[rings + 4] = new Vector2(0.0f, yb - 0.09f);
            int nr = prof.Length;
            var v = new Vector3[nr * (seg + 1)];
            var n = new Vector3[v.Length];
            var uv = new Vector2[v.Length];
            for (int i = 0; i < nr; i++)
            {
                Vector2 a = prof[Mathf.Max(0, i - 1)], b = prof[Mathf.Min(nr - 1, i + 1)];
                Vector2 t = (b - a).normalized;                       // down the profile
                Vector2 pn = new Vector2(-t.y, t.x);                  // outward (radius, y)
                if (pn.x < 0f) pn = -pn;
                if (i == 0) pn = new Vector2(0f, 1f);
                if (i == nr - 1) pn = new Vector2(0f, -1f);
                for (int j = 0; j <= seg; j++)
                {
                    float ph = 2f * Mathf.PI * j / seg;
                    float c = Mathf.Cos(ph), s = Mathf.Sin(ph);
                    int k = i * (seg + 1) + j;
                    v[k] = new Vector3(prof[i].x * c, prof[i].y, prof[i].x * s);
                    n[k] = new Vector3(pn.x * c, pn.y, pn.x * s).normalized;
                    uv[k] = new Vector2(j / (float)seg, i / (float)(nr - 1));
                }
            }
            var tr = new System.Collections.Generic.List<int>();
            for (int i = 0; i < nr - 1; i++)
                for (int j = 0; j < seg; j++)
                {
                    int a = i * (seg + 1) + j, b = a + 1, c = a + seg + 1, d = c + 1;
                    tr.Add(a); tr.Add(b); tr.Add(c);
                    tr.Add(b); tr.Add(d); tr.Add(c);
                }
            balloon = new Mesh { name = "Balloon" };
            balloon.vertices = v; balloon.normals = n; balloon.uv = uv;
            balloon.SetTriangles(tr, 0);
            balloon.RecalculateBounds();
            return balloon;
        }
    }
}
