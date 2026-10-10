using UnityEngine;

// ffu14 lobby showroom (after Lambo Blast's Game/Showroom.cs): one turntable "stand" per seat card (own camera ->
// RenderTexture shown in the card) with the seat's character slowly turning on a glowing disc, idling (head looks
// around, tail wags, breathing), popping when the pick changes; plus one portrait thumbnail per roster character for
// the picker tiles. Everything lives on layer 20 far from every world (x/z 4200, y 600) and is lit by its own key +
// rim + fill lights; the world lights ignore layer 20.
// ffu17 (Bill: "a lot of pixelating ... old school"): the turntable targets were fixed 320x300 (phones 256x240) and the
// thumbnails 160 px, then stretched up to 2-3x on high-DPI screens -> blocky, aliased frogs. Now every turntable target
// is sized to its card's real on-screen pixels (GameLobby calls Stand.Resize) with 4x MSAA, thumbnails are rendered at
// 4x MSAA 384 px and resolved into a mip-mapped texture (supersampled when shown at ~200 px), and the stage is a studio
// set: smooth 128-segment glossy pedestal, glowing rim ring, soft contact shadow, light pool on the floor and a
// spotlight backdrop in the character's colour, lit by key / two rims / fill.
public static class Showroom
{
    public const int Layer = 20;
    public const int Mask = 1 << Layer;
    public static readonly Vector3 Base = new Vector3(4200f, 600f, 4200f);
    public static readonly RenderTexture[] Thumbs = new RenderTexture[Roster.Count];
    public const int ThumbSize = 384;   // ffu17: was 160 (upscaled 1.3-1.8x on phones / 2x desktops)
    static Camera thumbCam;
    static bool inited;
    public static bool ThumbsReady { get; private set; }

    public static void Init()
    {
        if (inited) return;
        inited = true;
        foreach (Light l in Object.FindObjectsOfType<Light>()) l.cullingMask &= ~Mask;
        // ffu14a probe: 1.15 / 0.9 / 0.35 washed the light coats out (Kitty read white, Tigy yellow) -> softer
        // ffu17 studio rig: warm key high front-right, cool rim back-left, warm rim back-right, soft fill front-left
        MakeLight("ShowroomKey", new Vector3(0.45f, -0.62f, -0.64f), 0.8f, new Color(1f, 0.96f, 0.9f));
        MakeLight("ShowroomRim", new Vector3(-0.5f, -0.25f, 0.85f), 0.7f, new Color(0.62f, 0.86f, 1f));
        MakeLight("ShowroomRim2", new Vector3(0.6f, -0.2f, 0.8f), 0.38f, new Color(1f, 0.85f, 0.7f));
        MakeLight("ShowroomFill", new Vector3(-0.6f, -0.3f, -0.5f), 0.2f, new Color(0.95f, 0.92f, 1f));
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
        l.transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    public static RenderTexture NewRT(int w, int h)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;            // ffu17: was 2 desktop / none on phones
        rt.filterMode = FilterMode.Bilinear;
        rt.Create();
        return rt;
    }

    // mip-mapped (no MSAA) copy target: a 4x MSAA render is resolved into it, then mips give clean minification
    static RenderTexture NewMipRT(int n)
    {
        var rt = new RenderTexture(n, n, 0, RenderTextureFormat.ARGB32);
        rt.useMipMap = true;
        rt.autoGenerateMips = true;
        rt.filterMode = FilterMode.Trilinear;
        rt.wrapMode = TextureWrapMode.Clamp;
        rt.Create();
        return rt;
    }

    // ---------- ffu17 studio set pieces ----------
    static Mesh disc, ring;
    // smooth pedestal: top cap + rounded (bevelled) edge + side, 128 segments, normals smooth around the rim
    public static Mesh Disc()
    {
        if (disc != null) return disc;
        const int N = 128;
        float[] pr = { 0f, 0.94f, 0.985f, 1f, 1f, 0.97f };          // profile radius (x radius 1)
        float[] py = { 0f, 0f, -0.006f, -0.03f, -0.1f, -0.12f };      // profile height
        float[] ny = { 1f, 1f, 0.7f, 0.15f, 0f, -0.6f };              // profile normal y (rest radial)
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
    // flat annulus (inner .955 .. outer 1.0), 128 segments: the glowing rim line
    public static Mesh Ring()
    {
        if (ring != null) return ring;
        const int N = 128;
        var v = new Vector3[(N + 1) * 2]; var tri = new int[N * 6];
        for (int i = 0; i <= N; i++)
        {
            float a = i / (float)N * Mathf.PI * 2f; float cx = Mathf.Cos(a), cz = Mathf.Sin(a);
            v[i * 2] = new Vector3(cx * 0.955f, 0f, cz * 0.955f); v[i * 2 + 1] = new Vector3(cx, 0f, cz);
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
    public static GameObject MeshObj(string name, Transform parent, Mesh m, Material mat, Vector3 pos, Vector3 scale)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos; g.transform.localScale = scale;
        g.AddComponent<MeshFilter>().sharedMesh = m;
        var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        return g;
    }
    // soft sprite-textured quad (Sprites/Default, alpha blended): glows, contact shadow, backdrop spot
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
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        return r;
    }
    public static Color SpotColor(int ch, float a)
    {
        Color k = Color.Lerp(Roster.UiColor(ch), Color.white, 0.15f);
        return new Color(k.r, k.g, k.b, a);
    }

    public static Camera NewCam(string name, Transform parent)
    {
        var c = new GameObject(name).AddComponent<Camera>();
        if (parent != null) c.transform.SetParent(parent, false);
        c.cullingMask = Mask;
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = new Color(0.06f, 0.09f, 0.13f);
        c.fieldOfView = 28f;
        c.nearClipPlane = 0.2f;
        c.farClipPlane = 40f;
        c.allowHDR = false;
        c.allowMSAA = true;
        c.enabled = false;
        return c;
    }

    public static Color Backdrop(int ch)
    {
        Color k = Roster.UiColor(ch);
        return new Color(0.04f + k.r * 0.16f, 0.06f + k.g * 0.16f, 0.08f + k.b * 0.16f);
    }

    static FrogModel MakeModel(Transform parent, int ch)
    {
        var g = new GameObject("Char " + Roster.Name(ch));
        g.transform.SetParent(parent, false);
        var m = g.AddComponent<FrogModel>();
        m.BuildChar(ch);
        Mats.SetLayer(g, Layer);
        foreach (var r in g.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return m;
    }

    // ---------- thumbnails: 3/4 front head-and-shoulders portraits, rendered once (again if the GPU drops them) ----------
    public static bool ThumbsLost()
    {
        if (!ThumbsReady) return false;
        foreach (var t in Thumbs) if (t == null || !t.IsCreated()) return true;
        return false;
    }

    public static void RenderThumbs()
    {
        Init();
        if (thumbCam == null) thumbCam = NewCam("ThumbCam", null);
        Vector3 p = Base + new Vector3(-300f, 0f, 0f);
        var tmp = NewRT(ThumbSize, ThumbSize);   // 4x MSAA, resolved into the mip-mapped thumb below
        var spotMat = SoftMat(Color.white, 2990);
        for (int i = 0; i < Thumbs.Length; i++)
        {
            if (Thumbs[i] == null) Thumbs[i] = NewMipRT(ThumbSize);
            else if (!Thumbs[i].IsCreated()) Thumbs[i].Create();
            var root = new GameObject("Thumb").transform;
            root.position = p;
            root.rotation = Quaternion.Euler(0f, 180f - 28f, 0f);   // face the camera, turned a little
            var m = MakeModel(root, i);
            m.ShowIdle(0f);
            thumbCam.backgroundColor = Backdrop(i);
            thumbCam.fieldOfView = 30f;
            bool dachs = i == 9;
            float fy = Roster.IsFrog(i) ? 0.72f : dachs ? 0.6f : 0.8f;
            Vector3 look = p + new Vector3(0f, fy, 0f);
            thumbCam.transform.position = look + new Vector3(0.35f, 0.32f, -2.55f) * (dachs ? 1.05f : 1f);
            thumbCam.transform.LookAt(look + Vector3.up * 0.05f);
            // ffu17: soft spotlight behind the head (backdrop depth instead of a flat fill)
            spotMat.color = SpotColor(i, 0.42f);
            var spot = SoftQuad("ThumbSpot", null, Vector3.zero, Vector3.zero, 3.4f, 3.4f, spotMat);
            spot.transform.position = look + (look - thumbCam.transform.position).normalized * 2.2f + Vector3.up * 0.1f;
            spot.transform.rotation = Quaternion.LookRotation(spot.transform.position - thumbCam.transform.position);
            spot.gameObject.layer = Layer;
            thumbCam.targetTexture = tmp;
            thumbCam.Render();
            thumbCam.targetTexture = null;
            Graphics.Blit(tmp, Thumbs[i]);   // MSAA resolve + mips
            Object.DestroyImmediate(spot.gameObject);
            Object.DestroyImmediate(root.gameObject);
        }
        thumbCam.targetTexture = null;
        tmp.Release(); Object.Destroy(tmp);
        Object.Destroy(spotMat);
        ThumbsReady = true;
        Debug.Log("Showroom: thumbnails rendered (" + Thumbs.Length + " characters, " + ThumbSize + " px 4x MSAA + mips)");
    }

    public static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float u = x - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    // ---------- per-seat turntable ----------
    public class Stand
    {
        public RenderTexture rt { get; private set; }
        public readonly Camera cam;
        public int ch = -1;
        readonly Transform root, turn;
        readonly Material ringMat, poolMat, spotMat, shadowMat;
        FrogModel model;
        float pop, yaw, t;
        int rw, rh;

        public Stand(int idx)
        {
            Init();
            root = new GameObject("Stand " + idx).transform;
            root.position = Base + new Vector3(idx * 60f, 0f, 0f);
            turn = Mats.Node(root, "Turn", Vector3.zero);
            // glossy dark pedestal (smooth mesh), turning with the character so its highlight slides
            var ped = new Material(Mats.PBR(new Color(0.06f, 0.07f, 0.09f), 0.82f, 0.35f));
            MeshObj("Pedestal", turn, Disc(), ped, Vector3.zero, new Vector3(1.25f, 1f, 1.25f));
            ringMat = new Material(Mats.Unlit(new Color(0.4f, 1f, 0.55f)));
            MeshObj("Rim", root, Ring(), ringMat, new Vector3(0f, 0.004f, 0f), new Vector3(1.255f, 1f, 1.255f));
            // light pool on the floor round the pedestal, contact shadow under the character, spotlight backdrop
            poolMat = SoftMat(Color.white, 2980);
            SoftQuad("Pool", root, new Vector3(0f, -0.125f, 0f), new Vector3(90f, 0f, 0f), 4.6f, 4.6f, poolMat);
            shadowMat = SoftMat(new Color(0f, 0f, 0f, 0.6f), 2985);
            SoftQuad("Contact", root, new Vector3(0f, 0.006f, 0f), new Vector3(90f, 0f, 0f), 1.5f, 1.5f, shadowMat);
            spotMat = SoftMat(Color.white, 2970);
            SoftQuad("Backdrop", root, new Vector3(0f, 1.1f, 4.5f), Vector3.zero, 7.5f, 5.2f, spotMat);
            Mats.SetLayer(root.gameObject, Layer);
            cam = NewCam("StandCam " + idx, root);
            cam.transform.localPosition = new Vector3(0f, 1.25f, -4.6f);
            cam.transform.LookAt(root.position + new Vector3(0f, 0.62f, 0f));
            cam.farClipPlane = 20f;
            cam.fieldOfView = 25f;   // ffu17b: the 1:1 target shows the whole window (no crop-zoom any more) -> frame a bit tighter
            Resize(Look.Mobile ? 440 : 520, Look.Mobile ? 300 : 360);
            yaw = 200f + idx * 35f;
        }

        // ffu17: match the target to the card's on-screen pixels (1:1, no upscaling); returns true when it changed
        public bool Resize(int w, int h)
        {
            w = Mathf.Clamp(w, 64, 1400); h = Mathf.Clamp(h, 64, 1100);
            if (rt != null && rt.IsCreated() && Mathf.Abs(w - rw) < 4 && Mathf.Abs(h - rh) < 4) return false;
            rw = w; rh = h;
            var old = rt;
            rt = NewRT(w, h);
            cam.targetTexture = rt;
            cam.ResetAspect();
            if (old != null) { old.Release(); Object.Destroy(old); }
            return true;
        }

        // returns true when the pick changed
        public bool Set(int c, bool fx)
        {
            if (c == ch) return false;
            bool had = model != null;
            ch = c;
            if (model != null) { model.gameObject.SetActive(false); Object.Destroy(model.gameObject); }
            model = MakeModel(turn, c);
            cam.backgroundColor = Backdrop(c);
            ringMat.color = Color.Lerp(Roster.UiColor(c), Color.white, 0.35f);
            poolMat.color = SpotColor(c, 0.5f);
            spotMat.color = SpotColor(c, 0.38f);
            if (had && fx) pop = 1f;
            if (!rt.IsCreated()) rt.Create();
            return true;
        }

        public void Tick(float dt, bool on)
        {
            if (cam.enabled != on) cam.enabled = on;
            if (!on || model == null) return;
            t += dt;
            pop = Mathf.Max(0f, pop - dt * 2.6f);
            yaw += dt * (22f + pop * 300f);
            turn.localRotation = Quaternion.Euler(0f, yaw, 0f);
            model.transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.75f, 1f, pop > 0f ? EaseOutBack(1f - pop) : 1f);
            float hop = pop > 0f ? Mathf.Sin((1f - pop) * Mathf.PI) * 0.25f : 0f;
            model.transform.localPosition = new Vector3(0f, hop, 0f);   // a little hop
            shadowMat.color = new Color(0f, 0f, 0f, 0.6f * (1f - hop * 1.6f));
            ringMat.color = Color.Lerp(Color.Lerp(Roster.UiColor(ch), Color.white, 0.35f), Color.white, pop * 0.6f);
            model.ShowIdle(t);
            if (!rt.IsCreated()) rt.Create();
        }
    }
}
