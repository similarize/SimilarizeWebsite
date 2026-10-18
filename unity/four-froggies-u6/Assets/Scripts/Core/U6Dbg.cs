using UnityEngine;
using UnityEngine.Rendering;

// ffu6p diag knobs for the two open 6.6 bugs (no visible pond surface; individually transformed renderers cast no sun shadows):
//  u6srpb=0  SRP batcher off (GraphicsSettings.useScriptableRenderPipelineBatching)
//  u6tc=1    test casters ~7 m in front of the main camera, re-placed every 3 s: RED = Mats.Prim-style transformed cube (FF/Std),
//            BLUE = the same cube baked into world-space vertices on an identity transform (like the merged meshes, which DO cast),
//            WHITE (above) = cube on 'Universal Render Pipeline/Lit' when that shader is in the build
//  u6pw=1    pond plane gets an opaque magenta FF/Unlit material (renderer / mesh / culling test)
//  u6pw=2    FF/RealWater returns opaque magenta at the top of its fragment (shader runs + rasterises?)
//  u6pw=3    FF/RealWater returns (shore edge, pond mask, depth) as opaque rgb just before its output
public static class U6Dbg
{
    public static int PondMode, TestCasters;
    static float next;
    static GameObject red, blue, white; static Mesh cubeMesh, baked; static Vector3 lastP = new Vector3(1e9f, 0, 0);
    static bool logged;

    public static void Init()
    {
        if (U6.Q("u6srpb") == "0") GraphicsSettings.useScriptableRenderPipelineBatching = false;
        int.TryParse(U6.Q("u6pw") ?? "0", out PondMode);
        int.TryParse(U6.Q("u6tc") ?? "0", out TestCasters);
        Shader.SetGlobalVector("_FFDbgW", new Vector4(PondMode >= 2 ? PondMode - 1 : 0, 0, 0, 0));
        Debug.Log("U6DBG: srpb " + GraphicsSettings.useScriptableRenderPipelineBatching + " pw " + PondMode + " tc " + TestCasters);
    }

    public static void Tick()
    {
        if (PondMode == 0 && TestCasters == 0) return;
        if (Time.unscaledTime < next) return;
        next = Time.unscaledTime + 3f;
        if (PondMode == 1)
        {
            var go = GameObject.Find("PondWater");
            var r = go != null ? go.GetComponent<Renderer>() : null;
            if (r != null && (r.sharedMaterial == null || r.sharedMaterial.name != "U6DbgPond"))
            {
                var m = new Material(Mats.Unlit(Color.magenta)) { name = "U6DbgPond" };
                m.color = Color.magenta; m.renderQueue = 2000;
                r.sharedMaterial = m;
                Debug.Log("U6DBG: PondWater -> opaque magenta FF/Unlit (" + m.shader.name + ") bounds " + r.bounds.center.ToString("0.0") + "/" + r.bounds.size.ToString("0"));
            }
        }
        if (TestCasters != 0) PlaceCasters();
    }

    static void PlaceCasters()
    {
        var cam = Camera.main;
        if (cam == null) foreach (var c in Camera.allCameras) if (c.enabled && c.targetTexture == null) { cam = c; break; }
        if (cam == null) return;
        Vector3 f = cam.transform.forward; f.y = 0; if (f.sqrMagnitude < 1e-4f) f = Vector3.forward; f.Normalize();
        Vector3 rt = Vector3.Cross(Vector3.up, f);
        Vector3 p = cam.transform.position + f * 7f;
        RaycastHit hit;
        if (Physics.Raycast(p + Vector3.up * 60f, Vector3.down, out hit, 200f)) p.y = hit.point.y; else p.y = cam.transform.position.y - 1.6f;
        if ((p - lastP).sqrMagnitude < 1f) return;
        lastP = p;
        if (red == null)
        {
            red = Mats.Prim(PrimitiveType.Cube, null, Vector3.zero, Vector3.one * 1.2f, Mats.Lit(new Color(0.9f, 0.1f, 0.1f)), false);
            red.name = "U6DbgRed";
            cubeMesh = red.GetComponent<MeshFilter>().sharedMesh;
            blue = new GameObject("U6DbgBlue");
            blue.AddComponent<MeshFilter>();
            blue.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit(new Color(0.1f, 0.2f, 0.9f));
            var urp = Shader.Find("Universal Render Pipeline/Lit");
            if (urp != null)
            {
                white = Mats.Prim(PrimitiveType.Cube, null, Vector3.zero, Vector3.one * 1.2f, new Material(urp), false);
                white.name = "U6DbgWhite";
            }
            foreach (var g in new[] { red, blue, white })
            {
                if (g == null) continue;
                var r = g.GetComponent<MeshRenderer>();
                r.shadowCastingMode = ShadowCastingMode.On; r.receiveShadows = true; r.renderingLayerMask = 1;
            }
        }
        red.transform.position = p - rt * 2.5f + Vector3.up * 1.4f;
        red.transform.rotation = Quaternion.LookRotation(f);
        if (white != null) { white.transform.position = p + Vector3.up * 3.2f; white.transform.rotation = Quaternion.LookRotation(f); white.transform.localScale = Vector3.one * 1.2f; }
        var m = Matrix4x4.TRS(p + rt * 2.5f + Vector3.up * 1.4f, Quaternion.LookRotation(f), Vector3.one * 1.2f);
        var src = cubeMesh.vertices; var v = new Vector3[src.Length];
        for (int i = 0; i < v.Length; i++) v[i] = m.MultiplyPoint3x4(src[i]);
        var nn = cubeMesh.normals; for (int i = 0; i < nn.Length; i++) nn[i] = m.MultiplyVector(nn[i]).normalized;
        if (baked == null) { baked = new Mesh { name = "U6DbgBaked" }; }
        baked.Clear(); baked.vertices = v; baked.normals = nn; baked.uv = cubeMesh.uv; baked.triangles = cubeMesh.triangles; baked.RecalculateBounds(); baked.RecalculateTangents();
        blue.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); blue.transform.localScale = Vector3.one;
        blue.GetComponent<MeshFilter>().sharedMesh = baked;
        if (!logged)
        {
            logged = true;
            var rr = red.GetComponent<MeshRenderer>();
            Debug.Log("U6DBG casters: cam " + cam.name + " at " + p.ToString("0.0") + " red " + rr.sharedMaterial.shader.name + " sc " + rr.shadowCastingMode + " white " + (white != null ? "URP Lit" : "URP Lit NOT IN BUILD") + " srpb " + GraphicsSettings.useScriptableRenderPipelineBatching);
        }
    }
}
