using UnityEngine;
using UnityEngine.Rendering;

// ffu6p 6.6 diag knobs + fixes for the two open bugs (pond surface without its sky mirror; individually transformed renderers cast no sun shadows):
//  SRP batcher: forced ON (the URP asset asks for it, but GraphicsSettings read False at runtime); u6srpb=0 forces it off
//  pond mirror: binds the ranch reflection probe's cubemap as _FFRanchCube for FF/RealWater, which falls back to it (then to the SH sky)
//               when URP's GlossyEnvironmentReflection returns black there; u6envfb=0 turns the fallback off
//  u6tc=1    test casters on the player's view camera: NEAR pair 7 m ahead (shadow cascade 0) + FAR pair 30 m ahead (cascade 1);
//            RED = Mats.Prim-style transformed cube, BLUE = the same cube baked to world-space vertices on an identity transform
//            (like the merged meshes, which DO cast); each stands on a white FF/Std receiver plate
//  u6pw=1    pond plane gets an opaque magenta FF/Unlit material; u6pw=2 FF/RealWater returns magenta; u6pw=3 returns (edge, mask, depth)
//  u6pw=4    FF/RealWater returns its reflection colour (after the fallback) as opaque rgb
public static class U6Dbg
{
    public static int PondMode, TestCasters;
    static float next; static bool envOff, srpbLogged;
    static readonly System.Collections.Generic.List<GameObject> objs = new System.Collections.Generic.List<GameObject>();
    static Mesh cubeMesh; static readonly Mesh[] baked = new Mesh[2]; static Vector3 lastP = new Vector3(1e9f, 0, 0);
    static int placeLogs; static Texture boundCube;

    public static void Init()
    {
        bool want = U6.Q("u6srpb") != "0";
        GraphicsSettings.useScriptableRenderPipelineBatching = want;
        int.TryParse(U6.Q("u6pw") ?? "0", out PondMode);
        int.TryParse(U6.Q("u6tc") ?? "0", out TestCasters);
        envOff = U6.Q("u6envfb") == "0";
        Shader.SetGlobalVector("_FFDbgW", new Vector4(PondMode >= 2 ? PondMode - 1 : 0, 0, 0, 0));
        Shader.SetGlobalVector("_FFEnvFb", new Vector4(envOff ? 0f : 1f, 0f, 0f, 0f));
        Debug.Log("U6DBG: srpb " + GraphicsSettings.useScriptableRenderPipelineBatching + " (want " + want + ") pw " + PondMode + " tc " + TestCasters + " envfb " + !envOff);
    }

    public static void Tick()
    {
        if (Time.unscaledTime < next) return;
        next = Time.unscaledTime + 3f;
        if (!srpbLogged && Time.unscaledTime > 10f) { srpbLogged = true; Debug.Log("U6DBG: srpb at " + Time.unscaledTime.ToString("0") + " s = " + GraphicsSettings.useScriptableRenderPipelineBatching); }
        if (!envOff) BindRanchCube();
        if (PondMode == 1)
        {
            var go = GameObject.Find("PondWater");
            var r = go != null ? go.GetComponent<Renderer>() : null;
            if (r != null && (r.sharedMaterial == null || r.sharedMaterial.name != "U6DbgPond"))
            {
                var m = new Material(Mats.Unlit(Color.magenta)) { name = "U6DbgPond" };
                m.color = Color.magenta; m.renderQueue = 2000;
                r.sharedMaterial = m;
            }
        }
        if (TestCasters != 0) PlaceCasters();
    }

    // the ranch probe (Look.BuildProbes "RanchProbe") rendered by script; hand its cubemap straight to the pond shader
    static void BindRanchCube()
    {
        var go = GameObject.Find("RanchProbe");
        var p = go != null ? go.GetComponent<ReflectionProbe>() : null;
        Texture t = p != null ? (p.realtimeTexture != null ? (Texture)p.realtimeTexture : p.texture) : null;
        if (t == boundCube) return;
        boundCube = t;
        if (t != null)
        {
            Shader.SetGlobalTexture("_FFRanchCube", t);
            Vector3 c = p.transform.position;
            Shader.SetGlobalVector("_FFEnvFb", new Vector4(1f, 1f, 0f, 0f));
            Debug.Log("U6DBG: ranch probe cube bound for the pond (" + t.name + " " + t.width + "px dim " + t.dimension + ") at " + c.ToString("0"));
        }
        else Shader.SetGlobalVector("_FFEnvFb", new Vector4(1f, 0f, 0f, 0f));
    }

    static Camera ViewCam()
    {
        Camera best = null;
        foreach (var c in Camera.allCameras)
        {
            if (c == null || !c.isActiveAndEnabled || c.targetTexture != null || (c.cullingMask & 1) == 0) continue;
            if (c.rect.width < 0.3f || c.rect.height < 0.3f) continue;
            if (best == null || c.depth > best.depth) best = c;
        }
        return best != null ? best : Camera.main;
    }

    static GameObject Plate(Vector3 at)
    {
        var g = Mats.Prim(PrimitiveType.Cube, null, at, new Vector3(3.2f, 0.04f, 3.2f), Mats.Lit(new Color(0.92f, 0.92f, 0.9f)), false);
        g.name = "U6DbgPlate"; var r = g.GetComponent<MeshRenderer>(); r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = true;
        return g;
    }

    static void PlaceCasters()
    {
        var cam = ViewCam();
        if (cam == null) return;
        Vector3 f = cam.transform.forward; f.y = 0; if (f.sqrMagnitude < 1e-4f) f = Vector3.forward; f.Normalize();
        Vector3 rt = Vector3.Cross(Vector3.up, f);
        Vector3 cp = cam.transform.position;
        if ((cp - lastP).sqrMagnitude < 1f) return;
        lastP = cp;
        if (objs.Count == 0)
        {
            for (int k = 0; k < 2; k++)
            {
                var red = Mats.Prim(PrimitiveType.Cube, null, Vector3.zero, Vector3.one * 1.2f, Mats.Lit(new Color(0.9f, 0.1f, 0.1f)), false);
                red.name = "U6DbgRed" + k; cubeMesh = red.GetComponent<MeshFilter>().sharedMesh;
                var blue = new GameObject("U6DbgBlue" + k);
                blue.AddComponent<MeshFilter>(); blue.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit(new Color(0.1f, 0.2f, 0.9f));
                baked[k] = new Mesh { name = "U6DbgBaked" + k };
                foreach (var g in new[] { red, blue }) { var r = g.GetComponent<MeshRenderer>(); r.shadowCastingMode = ShadowCastingMode.On; r.receiveShadows = true; r.renderingLayerMask = 1; }
                objs.Add(red); objs.Add(blue); objs.Add(Plate(Vector3.zero)); objs.Add(Plate(Vector3.zero));
            }
        }
        string log = "";
        for (int k = 0; k < 2; k++)
        {
            float dist = k == 0 ? 7f : 30f, side = k == 0 ? 2.2f : 5f;
            Vector3 p = cp + f * dist;
            RaycastHit hit;
            if (Physics.Raycast(p + Vector3.up * 60f, Vector3.down, out hit, 200f)) p.y = hit.point.y; else p.y = cp.y - 1.6f;
            var red = objs[k * 4]; var blue = objs[k * 4 + 1];
            Vector3 pr = p - rt * side, pb = p + rt * side;
            objs[k * 4 + 2].transform.position = pr + Vector3.up * 0.03f;
            objs[k * 4 + 3].transform.position = pb + Vector3.up * 0.03f;
            red.transform.position = pr + Vector3.up * 1.6f; red.transform.rotation = Quaternion.LookRotation(f);
            var m = Matrix4x4.TRS(pb + Vector3.up * 1.6f, Quaternion.LookRotation(f), Vector3.one * 1.2f);
            var src = cubeMesh.vertices; var v = new Vector3[src.Length];
            for (int i = 0; i < v.Length; i++) v[i] = m.MultiplyPoint3x4(src[i]);
            var nn = cubeMesh.normals; for (int i = 0; i < nn.Length; i++) nn[i] = m.MultiplyVector(nn[i]).normalized;
            var bm = baked[k];
            bm.Clear(); bm.vertices = v; bm.normals = nn; bm.uv = cubeMesh.uv; bm.triangles = cubeMesh.triangles; bm.RecalculateBounds(); bm.RecalculateTangents();
            blue.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); blue.transform.localScale = Vector3.one;
            blue.GetComponent<MeshFilter>().sharedMesh = bm;
            log += (k == 0 ? " near " : " far ") + p.ToString("0.0");
        }
        if (placeLogs++ < 4) Debug.Log("U6DBG casters: cam " + cam.name + " at " + cp.ToString("0.0") + log + " srpb " + GraphicsSettings.useScriptableRenderPipelineBatching);
    }
}
