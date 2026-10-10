using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// James's robot phone (LB pad / P keys / PHONE button). ffu20 rebuild:
//  * robot picker = a lobby-style carousel: a live 3D turntable of the picked robot (studio set from the ffu17
//    Showroom: glossy pedestal, glowing rim, contact shadow, spotlight backdrop, 4x MSAA target at the card's real
//    pixel size) with its name, owner, battery bar and current job; arrows / Up-Down / swipe / drag change robots.
//  * orders in four tabs: CHORES (auto, charge, DRIVE IT!, 8 chores), ORDERS (the 3D orders), DRIVE (pick any ranch
//    vehicle or a story-mech band, then wander / follow me / race / go to my spot / get out) and MISSION (Mars rock run,
//    Callisto ice run, abort, and the live video feed: watch / full screen / camera).
// Controls: Up/Down (stick, D-pad, W/S, arrows) robot, Left/Right move along the tabs + orders, A / E / Enter / click /
// tap sends; pad RB = next tab, Y feed on/off, X feed full screen, B feed camera; LB / P / PHONE closes.
public class RobotPhone : MonoBehaviour
{
    public static RobotPhone I;
    public bool open;
    public Frog user;
    int sel = 1;              // Unitree is the default link, like the 3D phone
    int tab, cur;             // cur: 0..3 = the tabs, 4.. = items of the tab
    struct Item { public string label, act; }
    readonly List<Item>[] tabs = new List<Item>[4];
    static readonly string[] TabNames = { "CHORES", "ORDERS", "DRIVE", "MISSION" };
    Canvas canvas;
    public Canvas UICanvas { get { return canvas; } }   // ffu22: story cutscenes hide it
    Image panel, btn, card, batBg, batFill;
    RawImage stage;
    Text nameT, ownerT, jobT, batT, msg, hint, btnKey;
    readonly List<Image> tabBtns = new List<Image>(), itemBtns = new List<Image>(), dots = new List<Image>();
    readonly List<Text> itemLbls = new List<Text>();
    Image arrowL, arrowR;
    float stickCool;
    const float PW = 440f, PH = 664f, CardH = 196f;
    List<Vehicle> vehicles = new List<Vehicle>();
    Vehicle pickVeh; int pickBand = -1;
    // carousel turntable
    Transform standRoot, turn;
    Camera standCam;
    RenderTexture standRT;
    Robot shown; int shownIdx = -1;
    Material ringMat, poolMat, spotMat;
    float yaw = 200f, pop, slide;
    int rw, rh;
    // swipe
    bool dragOn; float dragX0;

    public Robot Selected { get { var r = RanchLife.I != null ? RanchLife.I.robots : null; return r != null && r.Count > 0 ? r[Mathf.Clamp(sel, 0, r.Count - 1)] : null; } }

    void Awake()
    {
        I = this;
        BuildItems();
        canvas = UIK.MakeCanvas("Phone", null, 70, true);
        Transform r = canvas.transform;
        panel = UIK.Img(r, UIK.Round, new Color(0.035f, 0.045f, 0.065f, 0.95f), new Vector2(1f, 0.5f), new Vector2(-PW * 0.5f - 12f, -24f), new Vector2(PW, PH));   // ffu20: clear of the page toolbar
        panel.type = Image.Type.Sliced;
        Transform p = panel.transform;
        var head = UIK.Label(p, "ROBOT PHONE", 18, TextAnchor.MiddleLeft, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(PW - 36f, 26f), new Color(0.55f, 1f, 0.6f));
        UIK.Modernize(head, true);
        // ---- carousel card ----
        card = UIK.Img(p, UIK.Round, new Color(1f, 1f, 1f, 0.06f), new Vector2(0.5f, 1f), new Vector2(0f, -40f - CardH * 0.5f), new Vector2(PW - 24f, CardH));
        card.type = Image.Type.Sliced;
        var mask = card.gameObject.AddComponent<Mask>(); mask.showMaskGraphic = true;
        stage = new GameObject("Turntable", typeof(RectTransform)).AddComponent<RawImage>();
        stage.rectTransform.SetParent(card.transform, false);
        UIK.Stretch(stage.rectTransform);
        stage.raycastTarget = false;
        var grad = UIK.Img(card.transform, null, new Color(0f, 0f, 0f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(PW - 24f, 60f));
        nameT = UIK.Label(card.transform, "", 26, TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(160f, 30f), new Vector2(290f, 34f), Color.white);
        UIK.Modernize(nameT, true, 0.6f);
        ownerT = UIK.Label(card.transform, "", 14, TextAnchor.UpperLeft, new Vector2(0f, 0f), new Vector2(160f, 11f), new Vector2(290f, 20f), new Color(0.75f, 0.85f, 0.95f));
        arrowL = Arrow(card.transform, -1); arrowR = Arrow(card.transform, 1);
        int n = RanchLife.I != null ? RanchLife.I.robots.Count : 7;
        for (int i = 0; i < n; i++) dots.Add(UIK.Img(p, UIK.Circle, Color.white, new Vector2(0.5f, 1f), new Vector2((i - (n - 1) * 0.5f) * 16f, -40f - CardH - 11f), new Vector2(8f, 8f)));
        // ---- battery + job ----
        float iy = -40f - CardH - 34f;
        batBg = UIK.Img(p, UIK.Round, new Color(1f, 1f, 1f, 0.12f), new Vector2(0f, 1f), new Vector2(18f + 45f, iy), new Vector2(90f, 14f)); batBg.type = Image.Type.Sliced;
        batFill = UIK.Img(batBg.transform, UIK.Round, new Color(0.4f, 1f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(90f, 14f)); batFill.type = Image.Type.Sliced;
        batFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        batT = UIK.Label(p, "", 15, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(18f + 90f + 8f + 30f, iy), new Vector2(60f, 22f), Color.white);
        jobT = UIK.Label(p, "", 15, TextAnchor.MiddleRight, new Vector2(1f, 1f), new Vector2(-18f - 125f, iy), new Vector2(250f, 22f), new Color(0.62f, 0.86f, 1f));
        jobT.horizontalOverflow = HorizontalWrapMode.Wrap; jobT.verticalOverflow = VerticalWrapMode.Truncate;
        jobT.resizeTextForBestFit = true; jobT.resizeTextMinSize = 10; jobT.resizeTextMaxSize = 15;
        // ---- tabs ----
        float ty = iy - 32f;
        for (int i = 0; i < 4; i++)
        {
            var b = UIK.Img(p, UIK.Round, Color.white, new Vector2(0.5f, 1f), new Vector2(-153f + i * 102f, ty), new Vector2(98f, 28f)); b.type = Image.Type.Sliced;
            UIK.Label(b.transform, TabNames[i], 13, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(98f, 28f), Color.white);
            tabBtns.Add(b);
        }
        // ---- item grid (max 21 = 7 rows x 3) ----
        float gy = ty - 36f;
        for (int i = 0; i < 21; i++)
        {
            int col = i % 3, row = i / 3;
            var b = UIK.Img(p, UIK.Round, Color.white, new Vector2(0.5f, 1f), new Vector2(-139f + col * 139f, gy - row * 33f), new Vector2(134f, 29f)); b.type = Image.Type.Sliced;
            var t = UIK.Label(b.transform, "", 13, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(130f, 28f), Color.white);
            t.resizeTextForBestFit = true; t.resizeTextMinSize = 9; t.resizeTextMaxSize = 13; t.verticalOverflow = VerticalWrapMode.Truncate; t.horizontalOverflow = HorizontalWrapMode.Wrap;
            itemBtns.Add(b); itemLbls.Add(t);
        }
        msg = UIK.Label(p, "Pick a robot, then an order.", 14, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 42f), new Vector2(PW - 24f, 24f), new Color(1f, 0.93f, 0.6f));
        msg.horizontalOverflow = HorizontalWrapMode.Wrap; msg.resizeTextForBestFit = true; msg.resizeTextMinSize = 10; msg.resizeTextMaxSize = 14;
        hint = UIK.Label(p, "", 12, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 17f), new Vector2(PW - 16f, 22f), new Color(0.72f, 0.78f, 0.84f));
        // ffu15: the PHONE button on every device, with the key for the player's device as a badge
        btn = UIK.Panel(r, new Color(0.08f, 0.1f, 0.13f, 0.82f), new Vector2(0f, 0f), new Vector2(150f, 46f));
        btn.rectTransform.anchorMin = btn.rectTransform.anchorMax = new Vector2(0f, 1f);
        btn.rectTransform.anchoredPosition = new Vector2(98f, -76f);
        var pl = UIK.Label(btn.transform, "PHONE", 19, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(62f, 0f), new Vector2(110f, 40f), new Color(0.6f, 1f, 0.55f));
        UIK.Modernize(pl, true);
        var badge = UIK.Panel(btn.transform, new Color(1f, 1f, 1f, 0.16f), new Vector2(0f, 0f), new Vector2(40f, 26f));
        badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        badge.rectTransform.anchoredPosition = new Vector2(-26f, 0f);
        btnKey = UIK.Label(badge.transform, "P", 15, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 26f), new Color(1f, 0.92f, 0.55f));
        panel.gameObject.SetActive(false);
        btn.gameObject.SetActive(false);
        BuildStand();
    }

    Image Arrow(Transform parent, int dir)
    {
        var a = UIK.Img(parent, UIK.Circle, new Color(0f, 0f, 0f, 0.45f), new Vector2(dir < 0 ? 0f : 1f, 0.5f), new Vector2(dir * -26f, 12f), new Vector2(40f, 40f));
        var t = UIK.Label(a.transform, dir < 0 ? "<" : ">", 24, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(dir * 1f, 1f), new Vector2(40f, 40f), Color.white);
        UIK.Modernize(t, true);
        return a;
    }

    void BuildItems()
    {
        for (int i = 0; i < 4; i++) tabs[i] = new List<Item>();
        void Add(int k, string l, string a) { tabs[k].Add(new Item { label = l, act = a }); }
        Add(0, "Auto chores", "auto"); Add(0, "Go charge", "charge"); Add(0, "DRIVE IT!", "drive");
        string[] ch = { "Sweep porch", "Vacuum garage", "Haul crates", "Haul hay", "Pick up litter", "Mow lawn", "Rake leaves", "Water flowers" };
        for (int i = 0; i < ch.Length; i++) Add(0, ch[i], "chore:" + i);
        string[] oc = { "come", "go", "stop", "wave", "roofHeli", "roofDrone", "follow", "dance" };
        string[] ol = { "Come here", "Go (8 m)", "Stop", "Wave", "Roof helipad", "Drone pad", "Follow me", "Dance" };
        for (int i = 0; i < oc.Length; i++) Add(1, ol[i], oc[i]);
        vehicles = RobotDriver.List();
        for (int i = 0; i < vehicles.Count && i < 12; i++) Add(2, vehicles[i].Title, "veh:" + i);
        string[] bands = { "10-story mech", "100-story mech", "1000-story mech", "Trillion mech" };
        for (int b = 0; b < 4; b++) Add(2, bands[b], "mech:" + b);
        Add(2, "Wander", "dmode:wander"); Add(2, "Follow me", "dmode:follow"); Add(2, "Race track", "dmode:race"); Add(2, "Go to my spot", "dmode:spot"); Add(2, "GET OUT", "getout");
        Add(3, "Mars rocks x3", "mis:0:3"); Add(3, "Mars rocks x5", "mis:0:5"); Add(3, "Callisto ice x4", "mis:1:4");
        Add(3, "Abort mission", "abort"); Add(3, "Watch feed", "feed"); Add(3, "Feed full screen", "feedfull"); Add(3, "Feed cam: drone/eye", "feedcam");
    }

    // ---------- 3D turntable (Showroom studio set, own stand at the showroom base) ----------
    void BuildStand()
    {
        Showroom.Init();
        standRoot = new GameObject("PhoneStand").transform;
        standRoot.position = Showroom.Base + new Vector3(0f, 0f, 260f);
        turn = Mats.Node(standRoot, "Turn", Vector3.zero);
        var ped = new Material(Mats.PBR(new Color(0.06f, 0.07f, 0.09f), 0.82f, 0.35f));
        Showroom.MeshObj("Pedestal", turn, Showroom.Disc(), ped, Vector3.zero, new Vector3(1.05f, 1f, 1.05f));
        ringMat = new Material(Mats.Unlit(new Color(0.45f, 1f, 0.6f)));
        Showroom.MeshObj("Rim", standRoot, Showroom.Ring(), ringMat, new Vector3(0f, 0.004f, 0f), new Vector3(1.055f, 1f, 1.055f));
        poolMat = Showroom.SoftMat(new Color(0.5f, 0.9f, 1f, 0.45f), 2980);
        Showroom.SoftQuad("Pool", standRoot, new Vector3(0f, -0.125f, 0f), new Vector3(90f, 0f, 0f), 4.4f, 4.4f, poolMat);
        Showroom.SoftQuad("Contact", standRoot, new Vector3(0f, 0.006f, 0f), new Vector3(90f, 0f, 0f), 1.3f, 1.3f, Showroom.SoftMat(new Color(0f, 0f, 0f, 0.6f), 2985));
        spotMat = Showroom.SoftMat(new Color(0.5f, 0.85f, 1f, 0.35f), 2970);
        Showroom.SoftQuad("Backdrop", standRoot, new Vector3(0f, 1.1f, 4.5f), Vector3.zero, 9f, 5.2f, spotMat);
        Mats.SetLayer(standRoot.gameObject, Showroom.Layer);
        standCam = Showroom.NewCam("PhoneStandCam", standRoot);
        standCam.transform.localPosition = new Vector3(0f, 1.15f, -4.3f);
        standCam.transform.LookAt(standRoot.position + new Vector3(0f, 0.9f, 0f));
        // a soft key light of its own (layer 20 only, short range: the lobby stands are 60+ m away)
        var kl = new GameObject("PhoneStandKey").AddComponent<Light>();
        kl.transform.SetParent(standRoot, false); kl.transform.localPosition = new Vector3(1.6f, 2.6f, -2.4f);
        kl.type = LightType.Point; kl.range = 9f; kl.intensity = 1.6f; kl.color = new Color(1f, 0.97f, 0.92f);
        kl.cullingMask = Showroom.Mask; kl.shadows = LightShadows.None; kl.renderMode = LightRenderMode.ForcePixel;
        standCam.fieldOfView = 26f;
        standCam.farClipPlane = 25f;
    }

    void ShowRobot(int idx)
    {
        var src = RanchLife.I.robots[idx];
        if (shown != null) Destroy(shown.gameObject);
        shown = Robot.Build(src.id, src.robotName, src.owner, src.tint, src.height, src.bulk, src.maxSpeed, standRoot.position);
        shown.PrepareDisplay();
        Transform t = shown.transform;
        t.SetParent(turn, false);
        t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one * (1.7f / Mathf.Max(0.5f, src.height));
        Mats.SetLayer(shown.gameObject, Showroom.Layer);
        foreach (var rr in shown.GetComponentsInChildren<Renderer>(true)) { rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rr.receiveShadows = false; }
        Color k = Color.Lerp(src.tint, new Color(0.45f, 0.85f, 1f), 0.6f);
        ringMat.color = Color.Lerp(k, Color.white, 0.3f);
        poolMat.color = new Color(k.r, k.g, k.b, 0.45f);
        spotMat.color = new Color(k.r, k.g, k.b, 0.35f);
        standCam.backgroundColor = new Color(0.04f + k.r * 0.1f, 0.06f + k.g * 0.1f, 0.09f + k.b * 0.12f);
        shownIdx = idx;
    }

    void TickStand(float dt)
    {
        standCam.enabled = open;
        if (!open) return;
        // render target = the card's real on-screen pixels, 4x MSAA (like the ffu17 lobby turntables)
        Vector3[] c = new Vector3[4];
        stage.rectTransform.GetWorldCorners(c);
        int w = Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(c[2].x - c[0].x)), 64, 1400), h = Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(c[2].y - c[0].y)), 64, 900);
        if (standRT == null || !standRT.IsCreated() || Mathf.Abs(w - rw) > 3 || Mathf.Abs(h - rh) > 3)
        {
            rw = w; rh = h;
            if (standRT != null) { standCam.targetTexture = null; standRT.Release(); Destroy(standRT); }
            standRT = Showroom.NewRT(w, h);
            standCam.targetTexture = standRT; standCam.ResetAspect();
            stage.texture = standRT;
        }
        if (shownIdx != sel) { int from = shownIdx; ShowRobot(sel); pop = 1f; slide = from < 0 ? 0f : (sel > from ? 1f : -1f) * (Mathf.Abs(sel - from) > 3 ? -1f : 1f); }
        pop = Mathf.Max(0f, pop - dt * 2.6f);
        slide = Mathf.MoveTowards(slide, 0f, dt * 4.5f);
        yaw += dt * (24f + pop * 280f);
        turn.localRotation = Quaternion.Euler(0f, yaw, 0f);
        turn.localPosition = new Vector3(slide * slide * Mathf.Sign(slide) * 2.6f, pop > 0f ? Mathf.Sin((1f - pop) * Mathf.PI) * 0.18f : 0f, 0f);
        if (shown != null) shown.transform.localScale = Vector3.one * (1.7f / Mathf.Max(0.5f, RanchLife.I.robots[sel].height)) * Mathf.LerpUnclamped(0.8f, 1f, pop > 0f ? Showroom.EaseOutBack(1f - pop) : 1f);
    }

    // ---------- open / close ----------
    public void Toggle(Frog f)
    {
        if (f == null) return;
        if (f.remote != null) { f.remote.ReleaseManual(true); return; }
        if (f.world != WorldId.Ranch) return;
        open = !open || user != f;
        user = f;
        Sfx.Play(Sfx.Click, 0.7f);
        if (open) f.Toast("Robot phone: Up/Down robot, Left/Right orders, A send, LB / P close", 3f);
    }

    public void Say(string s) { if (msg != null) msg.text = s; if (user != null && s.Length > 0) user.Toast(s, 2.5f); }

    // demo / screenshot mode: robot index + an action string (or a tab name)
    public void DemoOpen(Frog f, int robot, string act)
    {
        user = f; open = true; sel = robot;
        for (int k = 0; k < 4; k++)
        {
            if (TabNames[k] == act) { tab = k; cur = k; return; }
            int j = tabs[k].FindIndex(it => it.act == act);
            if (j >= 0) { tab = k; cur = 4 + j; msg.text = RanchLife.I.robots[robot].robotName + " selected - " + tabs[k][j].label; return; }
        }
    }
    public void DemoSend() { if (user != null) Activate(user); }
    public void DemoAct(Frog f, int robot, string act) { DemoOpen(f, robot, act); Activate(f); }

    int Count { get { return 4 + tabs[tab].Count; } }

    // called by Game with the phone user's input; returns true when the phone ate the input
    public bool Handle(Frog f, PIn i)
    {
        if (!open || f != user) return false;
        if (f.world != WorldId.Ranch || f.vehicle != null || f.remote != null) { open = false; return false; }
        stickCool -= Time.unscaledDeltaTime;
        var robots = RanchLife.I.robots;
        float y = i.move.y, x = i.move.x;
        if (stickCool <= 0f)
        {
            if (y > 0.6f) { Pick(sel - 1); stickCool = 0.24f; }
            else if (y < -0.6f) { Pick(sel + 1); stickCool = 0.24f; }
            else if (x > 0.6f) { Move(1); stickCool = 0.16f; }
            else if (x < -0.6f) { Move(-1); stickCool = 0.16f; }
        }
        if (Mathf.Abs(x) < 0.3f && Mathf.Abs(y) < 0.3f) stickCool = 0f;
        if (i.use || i.hop || Kb.EnterDown()) Activate(f);
        if (f.inputKind == InputKind.Gamepad)
        {
            if (i.warpUp) { tab = (tab + 1) % 4; cur = tab; Sfx.Play(Sfx.Click, 0.4f); }          // RB next tab
            if (i.land) Do(f, "feed");                                                          // Y
            if (i.auto) Do(f, "feedfull");                                                      // X
            if (i.cargo) Do(f, "feedcam");                                                      // B
        }
        return true;
    }

    void Pick(int k)
    {
        int n = RanchLife.I.robots.Count;
        sel = ((k % n) + n) % n;
        Sfx.Play(Sfx.Select != null ? Sfx.Select : Sfx.Click, 0.45f, 1.1f);
    }

    void Move(int d)
    {
        cur = ((cur + d) % Count + Count) % Count;
        Sfx.Play(Sfx.Click, 0.4f);
    }

    void Activate(Frog f)
    {
        if (cur < 4) { tab = cur; Sfx.Play(Sfx.Click, 0.5f); return; }
        int j = cur - 4;
        if (j < tabs[tab].Count) Do(f, tabs[tab][j].act);
    }

    void Do(Frog f, string a)
    {
        var r = Selected;
        if (r == null) return;
        string say = "";
        if (a == "drive") { open = false; r.TakeManual(f); msg.text = "Driving " + r.robotName; return; }
        if (a.StartsWith("veh:"))
        {
            int k = int.Parse(a.Substring(4));
            if (k < vehicles.Count) { pickVeh = vehicles[k]; pickBand = -1; say = pickVeh.Title + " picked - now Wander / Follow me / Race / Go to my spot"; }
        }
        else if (a.StartsWith("mech:")) { pickBand = int.Parse(a.Substring(5)); pickVeh = null; say = StoryMech.BandName[pickBand] + " mech picked - now how to drive it"; }
        else if (a.StartsWith("dmode:"))
        {
            Vehicle v = pickVeh;
            if (pickBand >= 0) v = RobotDriver.MechOfBand(pickBand, r.transform.position);
            if (v == null && r.drv != null) v = r.drv.v;
            if (v == null && pickBand >= 0) say = "No free " + StoryMech.BandName[pickBand] + " mech";
            else say = r.DriveOrder(v, a.Substring(6), f);
        }
        else if (a == "getout") { if (r.drv != null) { r.drv.GetOut(); say = r.robotName + ": getting out"; } else say = r.robotName + " isn't driving"; }
        else if (a.StartsWith("mis:"))
        {
            var ps = a.Split(':');
            say = RobotMission.Start(r, int.Parse(ps[1]), int.Parse(ps[2]));
            if (r.mission != null && RobotFeed.I != null) RobotFeed.I.Show(r, 1);   // start watching straight away
        }
        else if (a == "abort") say = r.mission != null ? r.mission.Abort() : r.robotName + " isn't on a mission";
        else if (a == "feed") { if (RobotFeed.I != null) { bool on = RobotFeed.I.mode > 0 && RobotFeed.I.robot == r; RobotFeed.I.Show(r, on ? 0 : 1); say = on ? "Feed off" : "Watching " + r.robotName + "'s camera (O)"; } }
        else if (a == "feedfull") { if (RobotFeed.I != null) { RobotFeed.I.Show(r, RobotFeed.I.mode == 2 && RobotFeed.I.robot == r ? 1 : 2); say = "Feed " + (RobotFeed.I.mode == 2 ? "full screen (Esc / tap to shrink)" : "picture-in-picture"); } }
        else if (a == "feedcam") { if (RobotFeed.I != null) { if (RobotFeed.I.mode == 0) RobotFeed.I.Show(r, 1); RobotFeed.I.ToggleCam(); say = "Feed camera: " + (RobotFeed.I.eye ? "robot's eye" : "drone"); } }
        else say = r.Order(a, f);
        msg.text = say;
        if (say.Length > 0) f.Toast(say, 2.2f);
        Sfx.Play(Sfx.Pickup, 0.5f, 1.3f);
    }

    Slot BtnSlot()
    {
        if (Game.I == null) return null;
        Slot best = null;
        foreach (var s in Game.I.slots) { if (s.kind == InputKind.Touch) return s; if (s.kind == InputKind.Keyboard && (best == null || best.kind != InputKind.Keyboard)) best = s; else if (best == null) best = s; }
        return best;
    }

    public bool Captures(Vector2 mp)
    {
        if (btn.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(btn.rectTransform, mp, null)) return true;
        return open && RectTransformUtility.RectangleContainsScreenPoint(panel.rectTransform, mp, null);
    }

    bool In(Image im, Vector2 p) { return im.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(im.rectTransform, p, null); }

    // a click / tap on the open phone
    void Press(Vector2 p)
    {
        if (In(arrowL, p)) { Pick(sel - 1); return; }
        if (In(arrowR, p)) { Pick(sel + 1); return; }
        for (int i = 0; i < dots.Count; i++) if (RectTransformUtility.RectangleContainsScreenPoint(dots[i].rectTransform, p, null)) { Pick(i); return; }
        for (int i = 0; i < 4; i++) if (In(tabBtns[i], p)) { tab = i; cur = i; Sfx.Play(Sfx.Click, 0.5f); return; }
        for (int i = 0; i < itemBtns.Count; i++) if (In(itemBtns[i], p) && i < tabs[tab].Count) { cur = 4 + i; if (user != null) Do(user, tabs[tab][i].act); return; }
        if (In(card, p)) { dragOn = true; dragX0 = p.x; }
    }

    void Update()
    {
        Slot bs = BtnSlot();
        Frog bf = bs != null ? Game.I.frogs[bs.frog] : null;
        bool showBtn = Game.I != null && Game.I.state == Game.State.Play && bf != null && bf.world == WorldId.Ranch && (bf.vehicle == null || bf.remote != null) && !Game.I.HelpOpen;
        if (btn.gameObject.activeSelf != showBtn) btn.gameObject.SetActive(showBtn);
        if (showBtn) { string k = bs.kind == InputKind.Gamepad ? "LB" : bs.kind == InputKind.Touch ? "TAP" : "P"; if (btnKey.text != k) btnKey.text = k; }
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (showBtn && bs.kind != InputKind.Touch && mouse != null)
        {
            if (open && user == bf && bs.kind == InputKind.Keyboard && Cursor.lockState == CursorLockMode.Locked) Cursor.lockState = CursorLockMode.None;
            Vector2 mp = mouse.position.ReadValue();
            if (Kb.MouseLeftDown() && Cursor.lockState != CursorLockMode.Locked)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(btn.rectTransform, mp, null)) Toggle(bf);
                else if (open) Press(mp);
            }
            if (dragOn && !Kb.MouseLeft()) { float dx = mp.x - dragX0; dragOn = false; if (Mathf.Abs(dx) > 50f) Pick(sel + (dx < 0f ? 1 : -1)); }
        }
        if (showBtn)
        {
            foreach (Vector2 tp in Kb.TouchesBegan())
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(btn.rectTransform, tp, null))
                    foreach (var s in Game.I.slots) if (s.kind == InputKind.Touch) Toggle(Game.I.frogs[s.frog]);
                if (open) Press(tp);
            }
            // swipe on the turntable card
            if (dragOn && Kb.TouchCount() > 0)
            {
                try
                {
                    var t = Input.GetTouch(0);
                    if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) { float dx = t.position.x - dragX0; dragOn = false; if (Mathf.Abs(dx) > 50f) Pick(sel + (dx < 0f ? 1 : -1)); }
                }
                catch { dragOn = false; }
            }
        }
        if (user != null && (user.world != WorldId.Ranch || Game.I == null || Game.I.state != Game.State.Play)) open = false;
        panel.gameObject.SetActive(open);
        TickStand(Mathf.Min(Time.unscaledDeltaTime, 0.1f));
        if (!open) return;
        Refresh();
    }

    void Refresh()
    {
        var robots = RanchLife.I.robots;
        var r = robots[sel];
        string nm = r.robotName;
        if (nameT.text != nm) nameT.text = nm;
        string ow = (r.owner.Length > 0 ? r.owner + "'s robot" : "Ranch robot") + "  ·  " + r.height.ToString("0.0") + " m";
        if (ownerT.text != ow) ownerT.text = ow;
        float b = Mathf.Clamp01(r.battery);
        batFill.rectTransform.sizeDelta = new Vector2(Mathf.Max(10f, 90f * b), 14f);
        batFill.color = r.Charging ? new Color(0.45f, 1f, 0.5f) : b < Robot.LowBattery ? new Color(1f, 0.62f, 0.15f) : new Color(0.55f, 0.9f, 1f);
        batT.text = (r.Charging ? "+" : "") + r.Pct;
        jobT.text = r.StatusLine;
        for (int i = 0; i < dots.Count; i++) { dots[i].color = i == sel ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 1f, 1f, 0.3f); dots[i].rectTransform.sizeDelta = Vector2.one * (i == sel ? 10f : 7f); }
        for (int i = 0; i < 4; i++)
            tabBtns[i].color = i == tab ? (cur == i ? new Color(1f, 0.75f, 0.2f, 0.95f) : new Color(0.2f, 0.6f, 0.4f, 0.9f)) : (cur == i ? new Color(1f, 0.75f, 0.2f, 0.6f) : new Color(1f, 1f, 1f, 0.1f));
        var items = tabs[tab];
        for (int i = 0; i < itemBtns.Count; i++)
        {
            bool has = i < items.Count;
            if (itemBtns[i].gameObject.activeSelf != has) itemBtns[i].gameObject.SetActive(has);
            if (!has) continue;
            string act = items[i].act;
            if (itemLbls[i].text != items[i].label) itemLbls[i].text = items[i].label;
            Color c = tab == 0 ? (i < 3 ? new Color(0.15f, 0.55f, 0.3f, 0.78f) : new Color(0.12f, 0.45f, 0.5f, 0.72f))
                    : tab == 1 ? new Color(0.2f, 0.35f, 0.7f, 0.65f)
                    : tab == 2 ? (act.StartsWith("dmode") || act == "getout" ? new Color(0.55f, 0.3f, 0.75f, 0.78f) : new Color(0.16f, 0.3f, 0.45f, 0.75f))
                    : (act.StartsWith("mis") ? new Color(0.7f, 0.32f, 0.12f, 0.82f) : new Color(0.2f, 0.32f, 0.5f, 0.75f));
            bool picked = (act.StartsWith("veh:") && pickVeh != null && vehicles.IndexOf(pickVeh) == int.Parse(act.Substring(4))) || (act.StartsWith("mech:") && pickBand == int.Parse(act.Substring(5)));
            if (picked) c = new Color(0.1f, 0.75f, 0.85f, 0.95f);
            if (cur == 4 + i) c = new Color(1f, 0.75f, 0.2f, 0.95f);
            itemBtns[i].color = c;
        }
        InputKind ik = user != null ? user.inputKind : InputKind.Keyboard;
        string h = ik == InputKind.Gamepad ? "Up/Down robot · Left/Right order · A send · RB tab · Y feed · X full · B cam · LB close"
                 : ik == InputKind.Touch ? "Swipe the robot · tap a tab, then an order · PHONE closes"
                 : "W/S robot · A/D order · E/Enter/click send · O feed · I cam · P close";
        if (hint.text != h) hint.text = h;
    }
}
