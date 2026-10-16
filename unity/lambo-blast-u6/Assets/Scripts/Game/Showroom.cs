using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// Lobby showroom: one turntable "stand" per player card (its own camera -> RenderTexture shown in the card) with the
// picked car + robot driver slowly rotating, plus one-off rendered thumbnail portraits of the 8 cars and 7 robots.
// Everything lives on layer 20, far from the island (x/z 3200, y 400); the island sun ignores layer 20 and the game
// cameras don't draw it.
//
// lambo7 premium studio (Bill: "the lobby looks like an old arcade game"), modelled on Four Froggies ffu17 + ffu29:
//  * each stand target = its window's real on-screen pixels (Game.SyncStands -> Stand.Resize), 4x MSAA, 24-bit depth;
//    thumbnails rendered at 2x (384 px wide) with 4x MSAA;
//  * dark studio: layer-20 cameras get their own dim ambient (Camera.onPreCull / onPostRender save + restore the island's),
//    a three-point rig (warm key, cool rim, warm kicker, soft fill, all per-pixel), near-black backdrop with a hint of the
//    car colour, ShowroomGrade (ACES filmic, contrast, saturation, vignette) on every stand + thumb camera;
//  * glossy black turntable (bevelled 128-segment disc) with a thin light ring, a mirror-floor reflection (a mirrored copy
//    of the car under a dark glossy transparent floor; desktop only), soft contact shadow + coloured light pool;
//  * a reflection probe that sees softbox light panels (a hidden "environment" set at Base - 150 m) so the clear-coated
//    paint shows studio highlights instead of the island sky.
public static class Showroom
{
    public const int Layer = 20;
    public const int Mask = 1 << Layer;
    static readonly Vector3 Base = new Vector3(3200f, 400f, 3200f);
    public static Vector3 BasePos { get { return Base; } }
    public static Vector3 EnvPos { get { return Base + new Vector3(-150f, 0f, 0f); } }
    public static readonly RenderTexture[] CarThumbs = new RenderTexture[9];     // u6i: + the Roadster
    public static readonly RenderTexture[] RobotThumbs = new RenderTexture[7];
    public const int CarThumbW = 384, CarThumbH = 224, RobotThumbW = 384, RobotThumbH = 244;
    static Camera thumbCam;
    static bool inited;
    public static bool ThumbsReady { get; private set; }
    public static bool Mirror { get { return !Look.Mobile; } }

    public static void Init(Light sun)
    {
        if (inited) return;
        inited = true;
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) { l.cullingMask &= ~Mask; U6Layers.Island(l); }
        if (sun != null) { sun.cullingMask &= ~Mask; U6Layers.Island(sun); }
        // lambou6: URP has no Camera.onPreCull / onPostRender; the per-camera studio ambient swap runs on the SRP callbacks
        RenderPipelineManager.beginCameraRendering += (ctx, c) => StudioPreCull(c);
        RenderPipelineManager.endCameraRendering += (ctx, c) => StudioPostRender(c);
        // u6d: the lambo7 rig was tuned for Gamma; in Linear + ACES it read crushed on real phones -> ~2.4x key / fill, a front
        // fill from the camera side, brighter ambient + backdrop, HDR on every device, studio exposure +0.75 EV (U6.ShowroomExposure)
        MakeLight("ShowroomKey", new Vector3(0.5f, -0.62f, -0.6f), 2.5f * SK, new Color(1f, 0.95f, 0.88f));
        MakeLight("ShowroomRim", new Vector3(-0.55f, -0.3f, 0.78f), 2.2f * SK, new Color(0.62f, 0.82f, 1f));
        MakeLight("ShowroomKicker", new Vector3(0.62f, -0.18f, 0.76f), 1.3f * SK, new Color(1f, 0.82f, 0.64f));
        MakeLight("ShowroomFill", new Vector3(-0.7f, -0.2f, -0.68f), 0.9f * SK, new Color(0.85f, 0.9f, 1f));
        MakeLight("ShowroomFront", new Vector3(0.1f, -0.35f, -0.93f), 1.1f * SK, new Color(1f, 0.98f, 0.95f));
        BuildEnvironment();
        Debug.Log("Showroom: lambo7 studio (ACES grade, " + (Hdr ? "HDR" : "LDR") + ", mirror floor " + Mirror + ")");
    }

    static bool Hdr { get { return SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR); } }   // u6d: phones too (LDR + ACES crushed the blacks)
    // u6d: studio light scale (?u6slight=) for tuning without a rebuild
    static float SK { get { string s = U6.Q("u6slight"); float f; return s != null && float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f) ? f : 1f; } }

    // ---------- studio ambient for layer-20 cameras only ----------
    static float reflSaved = -1f;
    static Color sSky, sEq, sGr; static bool sFog; static AmbientMode sMode;
    static void StudioPreCull(Camera c)
    {
        if (c == null || c.cullingMask != Mask) return;
        if (reflSaved < 0f)
        {
            reflSaved = RenderSettings.reflectionIntensity; sSky = RenderSettings.ambientSkyColor; sEq = RenderSettings.ambientEquatorColor;
            sGr = RenderSettings.ambientGroundColor; sFog = RenderSettings.fog; sMode = RenderSettings.ambientMode;
        }
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.42f, 0.45f, 0.52f);      // u6d: was 0.17 / 0.10 / 0.035 (Gamma-tuned)
        RenderSettings.ambientEquatorColor = new Color(0.3f, 0.3f, 0.33f);
        RenderSettings.ambientGroundColor = new Color(0.13f, 0.13f, 0.14f);
        Gfx6.StudioBegin();
        RenderSettings.reflectionIntensity = 1f;      // the probe now holds the studio (softboxes), not the sky
        RenderSettings.fog = false;
        if (!probeSaved) { savedProbe = RenderSettings.ambientProbe; probeSaved = true; }
        RenderSettings.ambientProbe = U6Layers.TrilightSH(RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor, RenderSettings.ambientGroundColor);
    }
    static SphericalHarmonicsL2 savedProbe; static bool probeSaved;
    static void StudioPostRender(Camera c)
    {
        if (c == null || c.cullingMask != Mask || reflSaved < 0f) return;
        RenderSettings.reflectionIntensity = reflSaved; RenderSettings.ambientSkyColor = sSky; RenderSettings.ambientEquatorColor = sEq;
        RenderSettings.ambientGroundColor = sGr; RenderSettings.fog = sFog; RenderSettings.ambientMode = sMode;
        if (probeSaved) { RenderSettings.ambientProbe = savedProbe; probeSaved = false; }
        Gfx6.StudioEnd();
        reflSaved = -1f;
    }

    static void MakeLight(string name, Vector3 dir, float intensity, Color c)
    {
        var l = new GameObject(name).AddComponent<Light>();
        l.type = LightType.Directional;
        l.cullingMask = Mask;
        l.intensity = intensity;
        l.color = c;
        l.shadows = LightShadows.None;
        l.renderMode = LightRenderMode.ForcePixel;
        U6Layers.Studio(l);
        l.transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    // softbox panels round the probe point: what the clear coat reflects (never inside a stand camera's view)
    static void BuildEnvironment()
    {
        var root = new GameObject("ShowroomEnv").transform;
        root.position = EnvPos;
        // u6d: HDR softboxes (the probe is HDR now) -> bright studio highlights in the clear coat
        // u6e: ~25% dimmer (u6d: 2.6 / 2.4 / 2.4 washed the paint out and lit the glossy floor up white at grazing angles)
        Material box = Mats.Unlit(new Color(1.95f, 2f, 2.1f)), warm = Mats.Unlit(new Color(1.8f, 1.5f, 1.2f)), cool = Mats.Unlit(new Color(0.9f, 1.28f, 1.8f));
        Material floor = Mats.Unlit(new Color(0.09f, 0.095f, 0.11f));
        Mats.Prim(PrimitiveType.Cube, root, new Vector3(0f, 7f, 0f), new Vector3(9f, 0.1f, 2.4f), box);          // overhead strip
        Mats.Prim(PrimitiveType.Cube, root, new Vector3(0f, 7f, 4f), new Vector3(9f, 0.1f, 1.2f), box);
        Mats.Prim(PrimitiveType.Cube, root, new Vector3(-9f, 3f, 0f), new Vector3(0.1f, 3.5f, 7f), cool);        // side softboxes
        Mats.Prim(PrimitiveType.Cube, root, new Vector3(9f, 3f, 0f), new Vector3(0.1f, 3.5f, 7f), warm);
        Mats.Prim(PrimitiveType.Cube, root, new Vector3(0f, 3f, -10f), new Vector3(12f, 1.2f, 0.1f), box);      // back strip
        Mats.Prim(PrimitiveType.Cylinder, root, new Vector3(0f, -0.6f, 0f), new Vector3(60f, 0.1f, 60f), floor);
        Mats.SetLayer(root.gameObject, Layer);
    }

    public static void Grade(Camera c, bool thumb)
    {
        if (c == null) return;
        c.allowHDR = Hdr;
        var g = c.GetComponent<ShowroomGrade>(); if (g == null) g = c.gameObject.AddComponent<ShowroomGrade>();
        g.vignette = thumb ? 0.35f : 0.62f;
    }

    public static RenderTexture NewRT(int w, int h)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;
        rt.filterMode = FilterMode.Bilinear;
        rt.Create();
        return rt;
    }

    public static Camera NewCam(string name, Transform parent)
    {
        var c = new GameObject(name).AddComponent<Camera>();
        if (parent != null) c.transform.SetParent(parent, false);
        c.cullingMask = Mask;
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = new Color(0.1f, 0.11f, 0.13f);
        c.fieldOfView = 30f;
        c.nearClipPlane = 0.2f;
        c.farClipPlane = 40f;
        c.allowHDR = false;
        c.allowMSAA = true;
        c.enabled = false;
        return c;
    }

    // ---------- set pieces ----------
    static Mesh disc, ring;
    // smooth pedestal: top cap + rounded (bevelled) edge + side, 128 segments
    public static Mesh Disc()
    {
        if (disc != null) return disc;
        const int N = 128;
        float[] pr = { 0f, 0.94f, 0.985f, 1f, 1f, 0.97f };
        float[] py = { 0f, 0f, -0.006f, -0.03f, -0.1f, -0.12f };
        float[] ny = { 1f, 1f, 0.7f, 0.15f, 0f, -0.6f };
        var v = new System.Collections.Generic.List<Vector3>(); var n = new System.Collections.Generic.List<Vector3>();
        var tri = new System.Collections.Generic.List<int>();
        int P = pr.Length;
        for (int i = 0; i <= N; i++)
        {
            float a = i / (float)N * Mathf.PI * 2f; float cx = Mathf.Cos(a), cz = Mathf.Sin(a);
            for (int k = 0; k < P; k++)
            {
                v.Add(new Vector3(cx * pr[k], py[k], cz * pr[k]));
                float r = Mathf.Sqrt(Mathf.Max(0f, 1f - ny[k] * ny[k]));
                n.Add(new Vector3(cx * r, ny[k], cz * r).normalized);
            }
        }
        for (int i = 0; i < N; i++)
            for (int k = 0; k < P - 1; k++)
            {
                int a = i * P + k, b = (i + 1) * P + k;
                tri.Add(a); tri.Add(b); tri.Add(a + 1);
                tri.Add(b); tri.Add(b + 1); tri.Add(a + 1);
            }
        disc = new Mesh { name = "ShowroomDisc" };
        disc.SetVertices(v); disc.SetNormals(n); disc.SetTriangles(tri, 0); disc.RecalculateBounds();
        return disc;
    }
    public static Mesh Ring()
    {
        if (ring != null) return ring;
        const int N = 128;
        var v = new Vector3[(N + 1) * 2]; var tri = new int[N * 6];
        for (int i = 0; i <= N; i++)
        {
            float a = i / (float)N * Mathf.PI * 2f; float cx = Mathf.Cos(a), cz = Mathf.Sin(a);
            v[i * 2] = new Vector3(cx * 0.97f, 0f, cz * 0.97f); v[i * 2 + 1] = new Vector3(cx, 0f, cz);
        }
        for (int i = 0; i < N; i++)
        {
            int a = i * 2, b = a + 2, t = i * 6;
            tri[t] = a; tri[t + 1] = b; tri[t + 2] = a + 1; tri[t + 3] = b; tri[t + 4] = b + 1; tri[t + 5] = a + 1;
        }
        ring = new Mesh { name = "ShowroomRing" };
        ring.vertices = v; ring.triangles = tri; ring.RecalculateNormals(); ring.RecalculateBounds();
        return ring;
    }
    // flat annulus (inner fraction .. 1), the studio floor round the mirror well
    static Mesh annulus;
    public static Mesh Annulus()
    {
        if (annulus != null) return annulus;
        const int N = 128; const float inner = 0.2f;
        var v = new Vector3[(N + 1) * 2]; var nr = new Vector3[(N + 1) * 2]; var tri = new int[N * 6];
        for (int i = 0; i <= N; i++)
        {
            float a = i / (float)N * Mathf.PI * 2f; float cx = Mathf.Cos(a), cz = Mathf.Sin(a);
            v[i * 2] = new Vector3(cx * inner, 0f, cz * inner); v[i * 2 + 1] = new Vector3(cx, 0f, cz);
            nr[i * 2] = nr[i * 2 + 1] = Vector3.up;
        }
        for (int i = 0; i < N; i++)
        {
            int a = i * 2, b = a + 2, t = i * 6;
            tri[t] = a; tri[t + 1] = b; tri[t + 2] = a + 1; tri[t + 3] = b; tri[t + 4] = b + 1; tri[t + 5] = a + 1;
        }
        annulus = new Mesh { name = "ShowroomAnnulus" };
        annulus.vertices = v; annulus.normals = nr; annulus.triangles = tri; annulus.RecalculateBounds();
        return annulus;
    }
    public static GameObject MeshObj(string name, Transform parent, Mesh m, Material mat, Vector3 pos, Vector3 scale)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos; g.transform.localScale = scale;
        g.AddComponent<MeshFilter>().sharedMesh = m;
        var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        return g;
    }
    public static Material SoftMat(Color c, int queue)
    {
        var m = new Material(Mats.Fx != null ? Mats.Fx : Mats.Unlit(Color.white));
        m.mainTexture = UIK.SoftDot.texture;
        m.color = c;
        m.renderQueue = queue;
        return m;
    }
    public static Renderer SoftQuad(string name, Transform parent, Vector3 pos, Vector3 euler, float w, float h, Material m)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        g.name = name;
        Object.Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos; g.transform.localRotation = Quaternion.Euler(euler); g.transform.localScale = new Vector3(w, h, 1f);
        var r = g.GetComponent<Renderer>(); r.sharedMaterial = m;
        r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        return r;
    }
    static Color Backdrop(Color k) { return new Color(0.1f + k.r * 0.09f, 0.11f + k.g * 0.09f, 0.13f + k.b * 0.09f); }   // u6d: was 0.02 + k 0.05 (near black)
    static Color Spot(Color k, float a) { Color c = Color.Lerp(k, Color.white, 0.2f); return new Color(c.r, c.g, c.b, a); }

    // ---------- thumbnails (rendered once, re-rendered if the GPU drops the textures) ----------
    public static IEnumerator RenderThumbsLater()
    {
        yield return null;
        yield return null;
        yield return null;   // after the probes (Bootstrap) so the paint already reflects the studio
        RenderThumbs();
    }

    public static bool ThumbsLost()
    {
        if (!ThumbsReady) return false;
        foreach (var t in CarThumbs) if (t == null || !t.IsCreated()) return true;
        foreach (var t in RobotThumbs) if (t == null || !t.IsCreated()) return true;
        return false;
    }

    static void FitThumb(Transform root, Vector3 dir, float aspect, float margin)
    {
        Bounds bb = new Bounds(root.position + Vector3.up * 0.6f, Vector3.one * 0.5f);
        bool any = false;
        foreach (var rr in root.GetComponentsInChildren<Renderer>())
        {
            if (rr.name == "ThumbSpot") continue;
            if (!any) { bb = rr.bounds; any = true; } else bb.Encapsulate(rr.bounds);
        }
        dir = dir.normalized;
        Vector3 fwd = -dir, right = Vector3.Cross(Vector3.up, fwd).normalized, up = Vector3.Cross(fwd, right);
        float tv = Mathf.Tan(thumbCam.fieldOfView * 0.5f * Mathf.Deg2Rad), th = tv * aspect;
        float d = 0.5f;
        Vector3 c = bb.center, e = bb.extents;
        for (int i = 0; i < 8; i++)
        {
            Vector3 o = new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
            float z = Vector3.Dot(o, dir);
            d = Mathf.Max(d, z + Mathf.Abs(Vector3.Dot(o, right)) * margin / th, z + Mathf.Abs(Vector3.Dot(o, up)) * margin / tv);
        }
        thumbCam.transform.position = c + dir * d;
        thumbCam.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        thumbCam.farClipPlane = Mathf.Max(40f, d * 3f);
    }

    public static void RenderThumbs()
    {
        if (thumbCam == null) { thumbCam = NewCam("ThumbCam", null); Grade(thumbCam, true); }
        Vector3 p = Base + new Vector3(-300f, 0f, 0f);
        Material spotMat = SoftMat(new Color(1f, 1f, 1f, 0.3f), 2900);
        for (int i = 0; i < CarThumbs.Length; i++)
        {
            if (CarThumbs[i] == null) CarThumbs[i] = NewRT(CarThumbW, CarThumbH);
            else if (!CarThumbs[i].IsCreated()) CarThumbs[i].Create();
            var root = new GameObject("ThumbCar").transform;
            root.position = p;
            Transform[] st, sp; Transform hd;
            KartModel.Build(root, i, -1, out st, out sp, out hd);
            foreach (var w in st) w.localRotation = Quaternion.Euler(0f, 16f, 0f);
            Color c = Cars.All[i].color;
            spotMat.color = Spot(c, 0.22f);
            SoftQuad("ThumbSpot", root, new Vector3(0f, 0.01f, 0f), new Vector3(90f, 0f, 0f), 6.5f, 6.5f, spotMat);
            Mats.SetLayer(root.gameObject, Layer);
            thumbCam.backgroundColor = Backdrop(c) * 1.4f;
            thumbCam.fieldOfView = 24f;
            thumbCam.transform.position = p + new Vector3(-4.2f, 2.0f, 5.7f);
            thumbCam.transform.LookAt(p + new Vector3(0f, 0.5f, 0.1f));
            thumbCam.targetTexture = CarThumbs[i];
            thumbCam.Render();
            root.gameObject.SetActive(false);
            MeshMerge.DestroyWithMeshes(root.gameObject);
        }
        for (int r = 0; r < RobotThumbs.Length; r++)
        {
            if (RobotThumbs[r] == null) RobotThumbs[r] = NewRT(RobotThumbW, RobotThumbH);
            else if (!RobotThumbs[r].IsCreated()) RobotThumbs[r].Create();
            var root = new GameObject("ThumbRobot").transform;
            root.position = p;
            Transform head = RobotModel.Build(root, root, Vector3.zero, r, false);
            head.localRotation = Quaternion.Euler(0f, -12f, 0f);
            Mats.SetLayer(root.gameObject, Layer);
            thumbCam.backgroundColor = new Color(0.07f, 0.08f, 0.105f);
            spotMat.color = new Color(0.75f, 0.85f, 1f, 0.35f);
            var back = SoftQuad("ThumbSpot", root, new Vector3(0f, 1.0f, -1.2f), Vector3.zero, 3.4f, 3.4f, spotMat);
            Mats.SetLayer(back.gameObject, Layer);
            thumbCam.fieldOfView = 28f;
            FitThumb(root, new Vector3(-0.5f, 0.34f, 1.4f), RobotThumbW / (float)RobotThumbH, 1.04f);
            thumbCam.targetTexture = RobotThumbs[r];
            thumbCam.Render();
            root.gameObject.SetActive(false);
            MeshMerge.DestroyWithMeshes(root.gameObject);
        }
        thumbCam.targetTexture = null;
        ThumbsReady = true;
        Debug.Log("Showroom: thumbnails rendered (" + CarThumbs.Length + " cars, 7 robots) " + CarThumbW + "x" + CarThumbH + " MSAA 4");
    }

    // ---------- per-player turntable ----------
    public class Stand
    {
        public RenderTexture rt;
        public readonly Camera cam;
        public int car = -1, robot = -1;
        public object owner;
        readonly Transform root, turn, mirrorT;
        Transform model, head, wheel, mModel, mHead, mWheel;
        Transform[] steer, spin, mSteer, mSpin;
        readonly Material ringMat, poolMat, backMat;
        float pop, yaw, t, spinT;
        int rw, rh;

        public Stand(int idx)
        {
            root = new GameObject("Stand " + idx).transform;
            root.position = Base + new Vector3(idx * 60f, 0f, 0f);
            turn = Mats.Node(root, "Turn", Vector3.zero);
            if (Mirror)
            {
                mirrorT = Mats.Node(turn, "Mirror", Vector3.zero);
                mirrorT.localScale = new Vector3(1f, -1f, 1f);
                // dark glossy transparent turntable top over the mirrored car = floor reflection
                var top = MeshObj("Pedestal", turn, Disc(), Mats.GlassTint(new Color(0.012f, 0.014f, 0.018f, 0.86f)), new Vector3(0f, 0.002f, 0f), new Vector3(3.3f, 1f, 3.3f));
                top.GetComponent<Renderer>().sharedMaterial.SetFloat("_Glossiness", 0.93f);
                MeshObj("PedestalBase", root, Disc(), Mats.PBR(new Color(0.02f, 0.022f, 0.026f), 0.6f, 0.3f), new Vector3(0f, -2.4f, 0f), new Vector3(3.6f, 1f, 3.6f));
            }
            else MeshObj("Pedestal", turn, Disc(), Mats.PBR(new Color(0.13f, 0.14f, 0.16f), 0.88f, 0.25f), Vector3.zero, new Vector3(3.3f, 1f, 3.3f));
            ringMat = new Material(Mats.Unlit(Color.white));
            MeshObj("Rim", root, Ring(), ringMat, new Vector3(0f, -0.028f, 0f), new Vector3(3.36f, 1f, 3.36f));
            // studio floor + soft pools of light
            // u6e: darker, satin floor (u6d gloss 0.75 reflected the HDR softboxes at grazing angles -> white floor)
            MeshObj("Floor", root, Mirror ? Annulus() : Disc(), Mats.PBR(new Color(0.06f, 0.063f, 0.072f), 0.35f, 0f), new Vector3(0f, -0.13f, 0f), new Vector3(17f, 1f, 17f));
            poolMat = SoftMat(new Color(1f, 1f, 1f, 0.25f), 2950);
            SoftQuad("Pool", root, new Vector3(0f, -0.125f, 0f), new Vector3(90f, 0f, 0f), 10f, 10f, poolMat);
            if (!Mirror) SoftQuad("Contact", root, new Vector3(0f, 0.006f, 0f), new Vector3(90f, 0f, 0f), 5.2f, 2.8f, SoftMat(new Color(0f, 0f, 0f, 0.8f), 2960));
            backMat = SoftMat(new Color(1f, 1f, 1f, 0.34f), 2900);
            SoftQuad("Backdrop", root, new Vector3(0f, 1.6f, -9f), Vector3.zero, 18f, 9f, backMat);
            Mats.SetLayer(root.gameObject, Layer);
            cam = NewCam("StandCam " + idx, root);
            cam.transform.localPosition = new Vector3(0f, 2.15f, 8.2f);
            cam.transform.LookAt(root.position + new Vector3(0f, 0.72f, 0f));
            cam.farClipPlane = 60f;
            cam.fieldOfView = 24f;
            Grade(cam, false);
            Resize(Look.Mobile ? 480 : 640, Look.Mobile ? 260 : 300);
            yaw = 150f + idx * 40f;
        }

        // match the target to the card window's on-screen pixels (1:1); returns true when it changed
        public bool Resize(int w, int h)
        {
            w = Mathf.Clamp(w, 64, 1600); h = Mathf.Clamp(h, 64, 1100);
            if (rt != null && rt.IsCreated() && Mathf.Abs(w - rw) < 6 && Mathf.Abs(h - rh) < 6) return false;
            rw = w; rh = h;
            var old = rt;
            rt = NewRT(w, h);
            cam.targetTexture = rt;
            cam.ResetAspect();
            // keep the whole car in frame for tall windows (portrait 2x2 cards)
            float aspect = w / (float)h;
            cam.fieldOfView = aspect >= 1.7f ? 24f : Mathf.Lerp(34f, 24f, Mathf.InverseLerp(1.0f, 1.7f, aspect));
            if (old != null) { old.Release(); Object.Destroy(old); }
            return true;
        }

        // returns true when the pick changed
        public bool Set(int c, int r, object who, bool sound)
        {
            if (c == car && r == robot && who == owner) return false;
            bool fx = model != null && who == owner;
            owner = who;
            car = c; robot = r;
            if (model != null) MeshMerge.DestroyWithMeshes(model.gameObject);
            if (mModel != null) MeshMerge.DestroyWithMeshes(mModel.gameObject);
            model = new GameObject("Model").transform;
            model.SetParent(turn, false);
            KartModel.Build(model, c, r, out steer, out spin, out head, out wheel);
            Mats.SetLayer(model.gameObject, Layer);
            if (mirrorT != null)
            {
                mModel = new GameObject("MirrorModel").transform;
                mModel.SetParent(mirrorT, false);
                KartModel.Build(mModel, c, r, out mSteer, out mSpin, out mHead, out mWheel);
                Mats.SetLayer(mModel.gameObject, Layer);
                foreach (var rr in mModel.GetComponentsInChildren<Renderer>()) rr.shadowCastingMode = ShadowCastingMode.Off;
            }
            Color k = Cars.All[c].color;
            cam.backgroundColor = Backdrop(k);
            ringMat.color = Color.Lerp(k, Color.white, 0.35f) * 0.9f;
            poolMat.color = Spot(k, 0.12f);     // u6e: was 0.2
            backMat.color = Spot(k, 0.16f);
            if (fx)
            {
                pop = 1f;
                if (sound) Sfx.Play(Sfx.Pop, 0.55f, 0.9f + 0.05f * r);
            }
            if (!rt.IsCreated()) rt.Create();
            return true;
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = x - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        public void Tick(float dt, bool on)
        {
            if (cam.enabled != on) cam.enabled = on;
            if (!on || model == null) return;
            if (!rt.IsCreated()) rt.Create();
            t += dt;
            pop = Mathf.Max(0f, pop - dt * 2.8f);
            yaw += dt * (16f + pop * 260f);
            turn.localRotation = Quaternion.Euler(0f, yaw, 0f);
            float k = 1f - pop;
            Vector3 sc = Vector3.one * Mathf.LerpUnclamped(0.8f, 1f, pop > 0f ? EaseOutBack(k) : 1f);
            model.localScale = sc;
            if (mModel != null) mModel.localScale = sc;
            float st = Mathf.Sin(t * 0.7f);
            Quaternion sq = Quaternion.Euler(0f, st * 20f, 0f), wq = Quaternion.AngleAxis(-st * 44f, RobotModel.WheelAxis);
            if (steer != null) foreach (var s in steer) if (s != null) s.localRotation = sq;
            if (mSteer != null) foreach (var s in mSteer) if (s != null) s.localRotation = sq;
            if (wheel != null) wheel.localRotation = wq;
            if (mWheel != null) mWheel.localRotation = wq;
            if (head != null)
            {
                float hy = Mathf.Sin(t * 0.9f) * 28f;
                if (robot == 6)   // electric Atlas: its head turns all the way round every few seconds (360 joints)
                {
                    spinT += dt;
                    if (spinT > 6f) spinT = 0f;
                    if (spinT > 4.6f) hy += Mathf.SmoothStep(0f, 360f, (spinT - 4.6f) / 1.4f);
                }
                head.localRotation = Quaternion.Euler(0f, hy, 0f);
                if (mHead != null) mHead.localRotation = head.localRotation;
            }
        }
    }
}
