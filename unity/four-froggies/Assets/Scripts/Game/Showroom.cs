using UnityEngine;

// ffu14 lobby showroom (after Lambo Blast's Game/Showroom.cs): one turntable "stand" per seat card (own camera ->
// RenderTexture shown in the card) with the seat's character slowly turning on a glowing disc, idling (head looks
// around, tail wags, breathing), popping when the pick changes; plus one portrait thumbnail per roster character for
// the picker tiles. Everything lives on layer 20 far from every world (x/z 4200, y 600) and is lit by its own key +
// rim + fill lights; the world lights ignore layer 20.
public static class Showroom
{
    public const int Layer = 20;
    public const int Mask = 1 << Layer;
    static readonly Vector3 Base = new Vector3(4200f, 600f, 4200f);
    public static readonly RenderTexture[] Thumbs = new RenderTexture[Roster.Count];
    public const int ThumbSize = 160;
    static Camera thumbCam;
    static bool inited;
    public static bool ThumbsReady { get; private set; }

    public static void Init()
    {
        if (inited) return;
        inited = true;
        foreach (Light l in Object.FindObjectsOfType<Light>()) l.cullingMask &= ~Mask;
        MakeLight("ShowroomKey", new Vector3(0.5f, -0.6f, -0.65f), 1.15f, new Color(1f, 0.96f, 0.9f));
        MakeLight("ShowroomRim", new Vector3(-0.4f, -0.2f, 0.9f), 0.9f, new Color(0.55f, 0.85f, 1f));
        MakeLight("ShowroomFill", new Vector3(-0.6f, -0.3f, -0.5f), 0.35f, new Color(1f, 0.85f, 0.95f));
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
        var rt = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32);
        rt.antiAliasing = Look.Mobile ? 1 : 2;
        rt.Create();
        return rt;
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
        for (int i = 0; i < Thumbs.Length; i++)
        {
            if (Thumbs[i] == null) Thumbs[i] = NewRT(ThumbSize, ThumbSize);
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
            thumbCam.targetTexture = Thumbs[i];
            thumbCam.Render();
            Object.DestroyImmediate(root.gameObject);
        }
        thumbCam.targetTexture = null;
        ThumbsReady = true;
        Debug.Log("Showroom: thumbnails rendered (" + Thumbs.Length + " characters)");
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float u = x - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    // ---------- per-seat turntable ----------
    public class Stand
    {
        public readonly RenderTexture rt;
        public readonly Camera cam;
        public int ch = -1;
        readonly Transform root, turn;
        readonly Material ringMat;
        FrogModel model;
        float pop, yaw, t;

        public Stand(int idx)
        {
            Init();
            root = new GameObject("Stand " + idx).transform;
            root.position = Base + new Vector3(idx * 60f, 0f, 0f);
            turn = Mats.Node(root, "Turn", Vector3.zero);
            Mats.Prim(PrimitiveType.Cylinder, root, new Vector3(0f, -0.03f, 0f), new Vector3(2.5f, 0.03f, 2.5f), Mats.Paint(new Color(0.07f, 0.08f, 0.1f), 0.9f));
            ringMat = new Material(Mats.Unlit(new Color(0.4f, 1f, 0.55f)));
            Mats.Prim(PrimitiveType.Cylinder, root, new Vector3(0f, -0.05f, 0f), new Vector3(2.68f, 0.02f, 2.68f), ringMat);   // glowing rim
            Mats.SetLayer(root.gameObject, Layer);
            cam = NewCam("StandCam " + idx, root);
            cam.transform.localPosition = new Vector3(0f, 1.25f, -4.6f);
            cam.transform.LookAt(root.position + new Vector3(0f, 0.62f, 0f));
            rt = NewRT(Look.Mobile ? 256 : 320, Look.Mobile ? 240 : 300);
            cam.targetTexture = rt;
            if (!Look.Mobile) LBPost.Add(cam, true);
            yaw = 200f + idx * 35f;
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
            ringMat.color = Color.Lerp(Roster.UiColor(c), Color.white, 0.25f);
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
            yaw += dt * (24f + pop * 300f);
            turn.localRotation = Quaternion.Euler(0f, yaw, 0f);
            model.transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.75f, 1f, pop > 0f ? EaseOutBack(1f - pop) : 1f);
            model.transform.localPosition = new Vector3(0f, pop > 0f ? Mathf.Sin((1f - pop) * Mathf.PI) * 0.25f : 0f, 0f);   // a little hop
            model.ShowIdle(t);
            if (!rt.IsCreated()) rt.Create();
        }
    }
}
