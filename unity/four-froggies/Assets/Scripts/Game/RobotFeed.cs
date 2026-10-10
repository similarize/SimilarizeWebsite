using UnityEngine;
using UnityEngine.UI;

// ffu20: live video feed from a robot (Bill: "we should be able to watch their video feed as they're doing that").
// A render-texture camera (sized to the panel's real on-screen pixels, capped lower on phones) shows the robot's own
// world - the ranch, space, the Mars or Callisto surface - with that world's sky / fog / light (Worlds.SetCamera), as a
// picture-in-picture panel top-left or full screen, with the mission status (phase, battery, distance) overlaid.
// DRONE cam = chase camera (ship hull / chase cam during the flight), EYE cam = the robot's head / cockpit window.
// Keys: O = feed off / PiP / full screen, I = drone / eye cam, Esc = back to PiP. Gamepad: in the phone, Y watch,
// X full screen, B camera. Touch / mouse: CAM, FULL, X on the panel; tap the picture = full screen / back.
public class RobotFeed : MonoBehaviour
{
    public static RobotFeed I;
    public Robot robot;
    public int mode;          // 0 off, 1 picture-in-picture, 2 full screen
    public bool eye;
    Camera cam;
    RenderTexture rt;
    Canvas canvas;
    RectTransform box;
    RawImage img;
    Image frameImg, band, dot;
    Text title, status, camLbl, fullLbl;
    Image camBtn, fullBtn, closeBtn;
    WorldId camWorldSet = (WorldId)(-1);
    Vector3 cpos; Quaternion crot; bool snap = true;
    Robot lastRobot;

    public static bool Watching(Robot r) { return I != null && I.mode > 0 && I.robot == r; }
    public static bool WatchingWorld(WorldId w) { return I != null && I.mode > 0 && I.robot != null && I.robot.world == w; }

    void Awake()
    {
        I = this;
        canvas = UIK.MakeCanvas("RobotFeed", null, 66, true);
        Transform r = canvas.transform;
        box = UIK.Rect(r, "FeedBox", new Vector2(0f, 1f), Vector2.zero, new Vector2(400f, 260f));
        box.pivot = new Vector2(0f, 1f);
        frameImg = UIK.Img(box, UIK.Round, new Color(0.03f, 0.04f, 0.06f, 0.92f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        frameImg.type = Image.Type.Sliced;
        UIK.Stretch(frameImg.rectTransform);
        frameImg.rectTransform.offsetMin = new Vector2(-4f, -4f); frameImg.rectTransform.offsetMax = new Vector2(4f, 4f);
        img = new GameObject("Feed", typeof(RectTransform)).AddComponent<RawImage>();
        img.rectTransform.SetParent(box, false);
        UIK.Anchor(img.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 0f), new Vector2(0f, -34f));
        img.raycastTarget = false;
        band = UIK.Img(box, null, new Color(0f, 0f, 0f, 0.55f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
        UIK.Anchor(band.rectTransform, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 46f));
        status = UIK.Label(band.transform, "", 16, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Color.white);
        UIK.Anchor(status.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-8f, 0f));
        status.supportRichText = true; status.verticalOverflow = VerticalWrapMode.Truncate;
        status.resizeTextForBestFit = true; status.resizeTextMinSize = 9; status.resizeTextMaxSize = 16;
        dot = UIK.Img(box, UIK.Circle, new Color(1f, 0.25f, 0.2f), new Vector2(0f, 1f), new Vector2(14f, -17f), new Vector2(11f, 11f));
        title = UIK.Label(box, "", 16, TextAnchor.MiddleLeft, new Vector2(0f, 1f), Vector2.zero, Vector2.zero, Color.white);
        UIK.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(26f, -34f), new Vector2(-170f, 0f));
        title.supportRichText = true; title.horizontalOverflow = HorizontalWrapMode.Wrap; title.verticalOverflow = VerticalWrapMode.Truncate;
        title.resizeTextForBestFit = true; title.resizeTextMinSize = 9; title.resizeTextMaxSize = 16;
        camBtn = Btn("CAM", -132f, out camLbl);
        fullBtn = Btn("FULL", -74f, out fullLbl);
        Text x; closeBtn = Btn("X", -22f, out x);
        closeBtn.rectTransform.sizeDelta = new Vector2(34f, 26f);
        box.gameObject.SetActive(false);
        var cg = new GameObject("Robot feed cam");
        cam = cg.AddComponent<Camera>();
        cam.enabled = false;
        cam.depth = -50;
        cam.fieldOfView = 62f;
        cam.cullingMask = ~((1 << Showroom.Layer) | (1 << 5));
        cam.allowHDR = false;
        cam.allowMSAA = true;
    }

    Image Btn(string label, float x, out Text t)
    {
        var b = UIK.Img(box, UIK.Round, new Color(1f, 1f, 1f, 0.14f), new Vector2(1f, 1f), new Vector2(x, -17f), new Vector2(54f, 26f));
        b.type = Image.Type.Sliced;
        t = UIK.Label(b.transform, label, 13, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54f, 26f), new Color(1f, 0.93f, 0.6f));
        return b;
    }

    public void Show(Robot r, int m)
    {
        if (r != null) { if (r != robot) snap = true; robot = r; }
        mode = robot == null ? 0 : m;
        Sfx.Play(Sfx.Click, 0.5f);
    }

    Robot DefaultRobot()
    {
        if (robot != null) return robot;
        if (RobotMission.Active != null) return RobotMission.Active.r;
        if (RobotPhone.I != null && RobotPhone.I.Selected != null) return RobotPhone.I.Selected;
        return RanchLife.I != null && RanchLife.I.robots.Count > 1 ? RanchLife.I.robots[1] : null;
    }

    public void Cycle() { Show(DefaultRobot(), (mode + 1) % 3); }
    public void ToggleFull() { Show(DefaultRobot(), mode == 2 ? 1 : 2); }
    public void ToggleCam() { eye = !eye; snap = true; Sfx.Play(Sfx.Click, 0.5f); }

    public bool Captures(Vector2 mp)
    {
        return mode > 0 && box.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(box, mp, null);
    }

    bool Hit(Image b, Vector2 p) { return RectTransformUtility.RectangleContainsScreenPoint(b.rectTransform, p, null); }

    void Click(Vector2 p)
    {
        if (Hit(closeBtn, p)) Show(null, 0);
        else if (Hit(camBtn, p)) ToggleCam();
        else if (Hit(fullBtn, p)) ToggleFull();
        else if (RectTransformUtility.RectangleContainsScreenPoint(img.rectTransform, p, null)) ToggleFull();
    }

    void Update()
    {
        bool playing = Game.I != null && Game.I.state == Game.State.Play;
        if (playing && !Kb.typing)
        {
            if (Kb.ODown()) Cycle();
            if (Kb.IDown() && mode > 0) ToggleCam();
            if (mode == 2 && Kb.EscDown()) Show(null, 1);
        }
        if (robot == null || !playing) mode = 0;
        bool on = mode > 0;
        if (box.gameObject.activeSelf != on) box.gameObject.SetActive(on);
        cam.enabled = on;
        if (!on) return;
        // clicks / taps on the panel
        if (UnityEngine.InputSystem.Mouse.current != null && Kb.MouseLeftDown() && Cursor.lockState != CursorLockMode.Locked)
            Click(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
        foreach (Vector2 tp in Kb.TouchesBegan()) if (Captures(tp)) Click(tp);
        DoLayout();
        PoseCamera();
        Status();
    }

    void DoLayout()
    {
        var cr = (RectTransform)canvas.transform;
        float cw = cr.rect.width, ch = cr.rect.height;
        bool portrait = ch > cw;
        if (mode == 2)
        {
            box.anchorMin = Vector2.zero; box.anchorMax = Vector2.one; box.pivot = new Vector2(0.5f, 0.5f);
            box.offsetMin = new Vector2(0f, 0f); box.offsetMax = new Vector2(0f, 0f);
            frameImg.color = new Color(0f, 0f, 0f, 1f);
        }
        else
        {
            float w = portrait ? Mathf.Min(330f, cw - 24f) : 420f;
            float h = w * 9f / 16f + 34f;
            box.anchorMin = box.anchorMax = new Vector2(0f, 1f); box.pivot = new Vector2(0f, 1f);
            box.sizeDelta = new Vector2(w, h);
            box.anchoredPosition = new Vector2(14f, portrait ? -112f : -110f);
            frameImg.color = new Color(0.03f, 0.04f, 0.06f, 0.92f);
        }
        float bx = mode == 2 ? -170f : 0f, by = mode == 2 ? -6f : 0f;   // full screen: clear of the page toolbar (top-right)
        camBtn.rectTransform.anchoredPosition = new Vector2(-132f + bx, -17f + by);
        fullBtn.rectTransform.anchoredPosition = new Vector2(-74f + bx, -17f + by);
        closeBtn.rectTransform.anchoredPosition = new Vector2(-22f + bx, -17f + by);
        title.rectTransform.offsetMax = new Vector2(-170f + bx, 0f);
        fullLbl.text = mode == 2 ? "PIP" : "FULL";
        camLbl.text = eye ? "EYE" : "DRONE";
        // render target = the picture's real on-screen pixels (phones capped at 960 wide)
        Vector3[] c = new Vector3[4];
        img.rectTransform.GetWorldCorners(c);
        int pw = Mathf.RoundToInt(Mathf.Abs(c[2].x - c[0].x)), ph = Mathf.RoundToInt(Mathf.Abs(c[2].y - c[0].y));
        int cap = Look.Mobile ? 960 : 1600;
        if (pw > cap) { ph = ph * cap / pw; pw = cap; }
        pw = Mathf.Max(160, pw); ph = Mathf.Max(90, ph);
        if (rt == null || Mathf.Abs(rt.width - pw) > 6 || Mathf.Abs(rt.height - ph) > 6 || !rt.IsCreated())
        {
            if (rt != null) { cam.targetTexture = null; rt.Release(); Destroy(rt); }
            rt = new RenderTexture(pw, ph, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = Look.Mobile ? 2 : 4;
            rt.filterMode = FilterMode.Bilinear;
            rt.Create();
            cam.targetTexture = rt;
            cam.ResetAspect();
            img.texture = rt;
        }
        dot.color = new Color(1f, 0.25f, 0.2f, 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 5f));
    }

    void PoseCamera()
    {
        Robot r = robot;
        if (r != lastRobot) { lastRobot = r; snap = true; }
        if (r.world != camWorldSet) { Worlds.SetCamera(cam, r.world); camWorldSet = r.world; snap = true; }
        Vector3 pos; Quaternion rot;
        var m = r.mission;
        if (m != null && m.Cam(eye, out pos, out rot)) { cpos = pos; crot = rot; snap = true; }
        else if (eye && r.eye != null && !r.hidden)
        {
            pos = r.eye.position + r.eye.forward * 0.05f;
            rot = Quaternion.LookRotation(r.transform.forward + Vector3.down * 0.12f, Vector3.up);
            cpos = pos; crot = rot; snap = true;
        }
        else
        {
            Vector3 fw, at; float back, up;
            if (r.seatedIn != null)
            {
                Vehicle v = r.seatedIn;
                fw = v.transform.forward; fw.y = 0f;
                at = v.transform.position + Vector3.up * v.camHeight * 0.5f;
                back = v.camDistance * 0.95f; up = v.camHeight * 1.1f;
            }
            else
            {
                fw = r.transform.forward; fw.y = 0f;
                at = r.transform.position + Vector3.up * r.height * 0.55f;
                back = 2.6f + r.height * 1.3f; up = 1.0f + r.height * 0.6f;
            }
            if (fw.sqrMagnitude < 0.001f) fw = Vector3.forward;
            fw.Normalize();
            Vector3 side = new Vector3(fw.z, 0f, -fw.x);
            pos = at - fw * back + side * back * 0.28f + Vector3.up * up;
            float gy = r.GroundAt(pos.x, pos.z) + 0.6f;
            if (r.world == WorldId.Ranch && Layout.InPond(pos.x, pos.z)) gy = Mathf.Max(gy, Layout.WaterY + 0.8f);
            if (pos.y < gy) pos.y = gy;
            rot = Quaternion.LookRotation(at - pos, Vector3.up);
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (snap || (cpos - pos).sqrMagnitude > 900f) { cpos = pos; crot = rot; snap = false; }
            else { cpos = Vector3.Lerp(cpos, pos, dt * 4f); crot = Quaternion.Slerp(crot, rot, dt * 5f); }
        }
        cam.transform.SetPositionAndRotation(cpos, crot);
        cam.fieldOfView = eye ? 70f : 60f;
    }

    void Status()
    {
        Robot r = robot;
        var m = r.mission;
        string head = "<color=#ff6b5e>LIVE</color>  " + r.robotName + "  <color=#9fb4c8>" + (m != null ? m.Name : r.drv != null && r.drv.v != null ? r.drv.v.Title : Worlds.Name(r.world)) + "</color>";
        if (title.text != head) title.text = head;
        string bc = r.Charging ? "#8cff8c" : r.battery < Robot.LowBattery ? "#ffb030" : "#ffffff";
        string s = "<color=#ffe27a>" + (m != null ? m.PhaseLine.ToUpper() : r.StatusLine) + "</color>   battery <color=" + bc + ">" + r.Pct + "</color>";
        if (m != null && m.DistLine.Length > 0) s += "   " + m.DistLine + " " + m.DistWhat;
        if (m != null && (m.phase >= 7)) s += "   " + (m.kind == 0 ? "rocks " : "ice ") + m.got + "/" + m.want;
        if (status.text != s) status.text = s;
    }
}
