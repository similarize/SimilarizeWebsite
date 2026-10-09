using System.Collections;
using UnityEngine;

// Lobby showroom: one turntable "stand" per player card (its own camera -> RenderTexture shown in the card) with the
// picked car + robot driver slowly rotating, plus one-off rendered thumbnail portraits of the 8 cars and 7 robots.
// Everything lives on layer 20, far from the island (x/z 3200, y 400), lit by its own key + rim light; the island sun
// ignores layer 20 and the game cameras don't draw it.
public static class Showroom
{
    public const int Layer = 20;
    public const int Mask = 1 << Layer;
    static readonly Vector3 Base = new Vector3(3200f, 400f, 3200f);
    public static Vector3 BasePos { get { return Base; } }
    public static readonly RenderTexture[] CarThumbs = new RenderTexture[8];
    public static readonly RenderTexture[] RobotThumbs = new RenderTexture[7];
    public const int CarThumbW = 192, CarThumbH = 112, RobotThumbW = 192, RobotThumbH = 122;
    static Camera thumbCam;
    static bool inited;
    public static bool ThumbsReady { get; private set; }

    public static void Init(Light sun)
    {
        if (inited) return;
        inited = true;
        foreach (Light l in Object.FindObjectsOfType<Light>()) l.cullingMask &= ~Mask;
        if (sun != null) sun.cullingMask &= ~Mask;
        MakeLight("ShowroomKey", new Vector3(0.45f, -0.55f, -0.7f), 1.25f, new Color(1f, 0.97f, 0.92f));
        MakeLight("ShowroomRim", new Vector3(-0.35f, -0.25f, 0.9f), 0.8f, new Color(0.6f, 0.8f, 1f));
    }

    static void MakeLight(string name, Vector3 dir, float intensity, Color c)
    {
        var l = new GameObject(name).AddComponent<Light>();
        l.type = LightType.Directional;
        l.cullingMask = Mask;
        l.intensity = intensity;
        l.color = c;
        l.shadows = LightShadows.None;
        l.transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    public static RenderTexture NewRT(int w, int h)
    {
        var rt = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 2;
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
        c.fieldOfView = 30f;
        c.nearClipPlane = 0.2f;
        c.farClipPlane = 40f;
        c.allowHDR = false;
        c.allowMSAA = true;
        c.enabled = false;
        return c;
    }

    // ---------- thumbnails (rendered once, re-rendered if the GPU drops the textures) ----------
    public static IEnumerator RenderThumbsLater()
    {
        yield return null;
        yield return null;
        RenderThumbs();
    }

    public static bool ThumbsLost()
    {
        if (!ThumbsReady) return false;
        foreach (var t in CarThumbs) if (t == null || !t.IsCreated()) return true;
        foreach (var t in RobotThumbs) if (t == null || !t.IsCreated()) return true;
        return false;
    }

    public static void RenderThumbs()
    {
        if (thumbCam == null) thumbCam = NewCam("ThumbCam", null);
        Vector3 p = Base + new Vector3(-300f, 0f, 0f);
        for (int i = 0; i < CarThumbs.Length; i++)
        {
            if (CarThumbs[i] == null) CarThumbs[i] = NewRT(CarThumbW, CarThumbH);
            else if (!CarThumbs[i].IsCreated()) CarThumbs[i].Create();
            var root = new GameObject("ThumbCar").transform;
            root.position = p;
            Transform[] st, sp; Transform hd;
            KartModel.Build(root, i, -1, out st, out sp, out hd);
            foreach (var w in st) w.localRotation = Quaternion.Euler(0f, 16f, 0f);
            Mats.SetLayer(root.gameObject, Layer);
            Color c = Cars.All[i].color;
            thumbCam.backgroundColor = new Color(0.05f + c.r * 0.18f, 0.07f + c.g * 0.18f, 0.1f + c.b * 0.18f);
            thumbCam.fieldOfView = 26f;
            thumbCam.transform.position = p + new Vector3(-3.9f, 2.0f, 5.3f);
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
            float s = RobotModel.Size(r);
            float f = s > 1.2f ? s * 0.86f : s;                    // Big Figure Two fills its frame (and a bit more)
            if (RobotModel.UsesPack(r)) f = s > 1.2f ? 1.12f : 1f;
            thumbCam.backgroundColor = new Color(0.08f, 0.11f, 0.17f);
            thumbCam.fieldOfView = 30f;
            bool pack = RobotModel.UsesPack(r);
            thumbCam.transform.position = p + (pack ? new Vector3(-1.05f, 0.98f, 1.3f) : new Vector3(-0.55f, 0.66f, 1.45f)) * f;
            thumbCam.transform.LookAt(p + (pack ? new Vector3(0f, 0.5f, 0.12f) : new Vector3(0f, 0.52f, 0.05f)) * f);
            thumbCam.targetTexture = RobotThumbs[r];
            thumbCam.Render();
            root.gameObject.SetActive(false);
            MeshMerge.DestroyWithMeshes(root.gameObject);
        }
        thumbCam.targetTexture = null;
        ThumbsReady = true;
        Debug.Log("Showroom: thumbnails rendered (8 cars, 7 robots)");
    }

    // ---------- per-player turntable ----------
    public class Stand
    {
        public readonly RenderTexture rt;
        public readonly Camera cam;
        public int car = -1, robot = -1;
        public object owner;
        readonly Transform root, turn;
        Transform model, head, wheel;
        Transform[] steer, spin;
        float pop, yaw, t, spinT;

        public Stand(int idx)
        {
            root = new GameObject("Stand " + idx).transform;
            root.position = Base + new Vector3(idx * 60f, 0f, 0f);
            turn = Mats.Node(root, "Turn", Vector3.zero);
            Mats.Prim(PrimitiveType.Cylinder, root, new Vector3(0f, -0.03f, 0f), new Vector3(6.4f, 0.03f, 6.4f), Mats.Paint(new Color(0.08f, 0.09f, 0.11f), 0.9f));
            Mats.Prim(PrimitiveType.Cylinder, root, new Vector3(0f, -0.05f, 0f), new Vector3(6.75f, 0.02f, 6.75f), Mats.Unlit(new Color(0.3f, 0.9f, 1f)));   // glowing rim
            Mats.SetLayer(root.gameObject, Layer);
            cam = NewCam("StandCam " + idx, root);
            cam.transform.localPosition = new Vector3(0f, 2.3f, 6.9f);
            cam.transform.LookAt(root.position + new Vector3(0f, 0.85f, 0f));
            rt = NewRT(448, 256);
            cam.targetTexture = rt;
            LBPost.Add(cam, true);
            yaw = 150f + idx * 40f;
        }

        // returns true when the pick changed
        public bool Set(int c, int r, object who, bool sound)
        {
            if (c == car && r == robot && who == owner) return false;
            bool fx = model != null && who == owner;
            owner = who;
            car = c; robot = r;
            if (model != null) MeshMerge.DestroyWithMeshes(model.gameObject);
            model = new GameObject("Model").transform;
            model.SetParent(turn, false);
            KartModel.Build(model, c, r, out steer, out spin, out head, out wheel);
            Mats.SetLayer(model.gameObject, Layer);
            Color k = Cars.All[c].color;
            cam.backgroundColor = new Color(0.04f + k.r * 0.2f, 0.06f + k.g * 0.2f, 0.09f + k.b * 0.2f);
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
            t += dt;
            pop = Mathf.Max(0f, pop - dt * 2.8f);
            yaw += dt * (22f + pop * 260f);
            turn.localRotation = Quaternion.Euler(0f, yaw, 0f);
            float k = 1f - pop;
            model.localScale = Vector3.one * Mathf.LerpUnclamped(0.8f, 1f, pop > 0f ? EaseOutBack(k) : 1f);
            float st = Mathf.Sin(t * 0.7f);
            if (steer != null) foreach (var s in steer) if (s != null) s.localRotation = Quaternion.Euler(0f, st * 20f, 0f);
            if (wheel != null) wheel.localRotation = Quaternion.AngleAxis(-st * 44f, RobotModel.WheelAxis);
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
            }
        }
    }
}
