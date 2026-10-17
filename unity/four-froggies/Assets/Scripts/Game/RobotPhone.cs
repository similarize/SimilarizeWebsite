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
// ffu59b (Bill): in split-screen every player has their OWN phone - one RobotPhone per seat (Seat[0..3], seat = frog
// index; I = seat 0, the one the demos drive), each with its own robot pick / tab / orders / calls acting for that seat's
// froggy, opened from that player's own controller / keys / touch. Each phone (and its PHONE button) lives inside its
// player's split viewport and scales to fit; small viewports (quarter screen) use a compact layout (no turntable card).
public class RobotPhone : MonoBehaviour
{
    public static RobotPhone I;
    public static readonly RobotPhone[] Seat = new RobotPhone[4];
    static int nextSeat;
    public int seat;
    public bool open;
    public Frog user;
    int sel = 1;              // Unitree is the default link, like the 3D phone
    int tab, cur;             // cur: 0..NT-1 = the tabs, NT.. = items of the tab
    const int NT = 7;         // ffu27: + CALL; ffu42: + WHEELS (the wheel-size setting); ffu54: + PLAY (robots everywhere)
    struct Item { public string label, act; }
    readonly List<Item>[] tabs = new List<Item>[NT];
    static readonly string[] TabNames = { "CHORES", "ORDERS", "DRIVE", "MISSION", "CALL", "WHEELS", "PLAY" };
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

    RectTransform vp;           // ffu59b: this seat's viewport (anchors = its camera rect)
    Text head;
    readonly List<RectTransform> shiftRT = new List<RectTransform>();
    readonly List<Vector2> shiftPos = new List<Vector2>();
    int layoutKey = -1;
    public int Tab { get { return tab; } }
    public float PanelScale { get { return panel.rectTransform.localScale.x; } }
    public bool Compact { get { return !card.gameObject.activeSelf; } }

    // the phone of the seat this froggy sits in (made on first use)
    public static RobotPhone Of(Frog f)
    {
        if (f == null || Game.I == null) return I;
        int k = Game.I.frogs.IndexOf(f);
        if (k < 0 || k > 3) return I;
        if (Seat[k] == null && I != null)
        {
            nextSeat = k;
            Seat[k] = new GameObject("RobotPhone" + (k + 1)).AddComponent<RobotPhone>();
            nextSeat = 0;
        }
        return Seat[k] != null ? Seat[k] : I;
    }
    public static RobotPhone OpenFor(Frog f) { foreach (var p in Seat) if (p != null && p.open && p.user == f) return p; return null; }
    public static bool AnyCaptures(Vector2 mp) { foreach (var p in Seat) if (p != null && p.Captures(mp)) return true; return false; }
    public static void ShowAll(bool on) { foreach (var p in Seat) if (p != null && p.canvas != null) p.canvas.enabled = on; }
    public static void CloseAll() { foreach (var p in Seat) if (p != null) p.open = false; }
    public static void SayAll(string s) { foreach (var p in Seat) if (p != null && (p.user != null || p == I)) p.Say(s); }
    public static void SayTo(Frog f, string s) { foreach (var p in Seat) if (p != null && p.user == f) p.Say(s); }
    static RobotPhone demoPh;

    void Awake()
    {
        seat = nextSeat;
        if (seat == 0) I = this;
        Seat[seat] = this;
        BuildItems();
        canvas = UIK.MakeCanvas("Phone" + (seat > 0 ? (seat + 1).ToString() : ""), null, 70 + seat, true);
        vp = UIK.Rect(canvas.transform, "PhoneViewport", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        vp.anchorMin = Vector2.zero; vp.anchorMax = Vector2.one; vp.offsetMin = vp.offsetMax = Vector2.zero;
        Transform r = vp;
        panel = UIK.Img(r, UIK.Round, new Color(0.035f, 0.045f, 0.065f, 0.95f), new Vector2(1f, 0.5f), new Vector2(-PW * 0.5f - 12f, -24f), new Vector2(PW, PH));   // ffu20: clear of the page toolbar
        panel.type = Image.Type.Sliced;
        Transform p = panel.transform;
        head = UIK.Label(p, "ROBOT PHONE", 18, TextAnchor.MiddleLeft, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(PW - 36f, 26f), new Color(0.55f, 1f, 0.6f));
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
        for (int i = 0; i < NT; i++)
        {
            var b = UIK.Img(p, UIK.Round, Color.white, new Vector2(0.5f, 1f), new Vector2(-183f + i * 61f, ty), new Vector2(58f, 28f)); b.type = Image.Type.Sliced;   // ffu54: 7 tabs
            var tl = UIK.Label(b.transform, TabNames[i], 12, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(57f, 28f), i == 4 ? new Color(0.6f, 1f, 0.6f) : i == 5 ? new Color(1f, 0.88f, 0.45f) : i == 6 ? new Color(1f, 0.7f, 0.95f) : Color.white);
            tl.resizeTextForBestFit = true; tl.resizeTextMinSize = 9; tl.resizeTextMaxSize = 12;
            tabBtns.Add(b);
        }
        // ---- item grid (max 30 = 10 rows x 3; ffu26: was 7 rows; ffu56: 9 rows; ffu57: 10 rows in the same height for the animals) ----
        float gy = ty - 36f + 1.5f;
        for (int i = 0; i < 30; i++)
        {
            int col = i % 3, row = i / 3;
            var b = UIK.Img(p, UIK.Round, Color.white, new Vector2(0.5f, 1f), new Vector2(-139f + col * 139f, gy - row * 26.4f), new Vector2(134f, 23.6f)); b.type = Image.Type.Sliced;
            var t = UIK.Label(b.transform, "", 13, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(130f, 22.6f), Color.white);
            t.resizeTextForBestFit = true; t.resizeTextMinSize = 9; t.resizeTextMaxSize = 13; t.verticalOverflow = VerticalWrapMode.Truncate; t.horizontalOverflow = HorizontalWrapMode.Wrap;
            itemBtns.Add(b); itemLbls.Add(t);
        }
        msg = UIK.Label(p, "Pick a robot, then an order.", 14, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 42f), new Vector2(PW - 24f, 24f), new Color(1f, 0.93f, 0.6f));
        msg.horizontalOverflow = HorizontalWrapMode.Wrap; msg.resizeTextForBestFit = true; msg.resizeTextMinSize = 10; msg.resizeTextMaxSize = 14;
        hint = UIK.Label(p, "", 12, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 17f), new Vector2(PW - 16f, 22f), new Color(0.72f, 0.78f, 0.84f));
        hint.resizeTextForBestFit = true; hint.resizeTextMinSize = 9; hint.resizeTextMaxSize = 12;
        // ffu59b compact layout: these move up by the card's height when the card is hidden
        shiftRT.Add(batBg.rectTransform); shiftRT.Add(batT.rectTransform); shiftRT.Add(jobT.rectTransform);
        foreach (var b in tabBtns) shiftRT.Add(b.rectTransform);
        foreach (var b in itemBtns) shiftRT.Add(b.rectTransform);
        foreach (var t in shiftRT) shiftPos.Add(t.anchoredPosition);
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
        for (int i = 0; i < NT; i++) tabs[i] = new List<Item>();
        void Add(int k, string l, string a) { tabs[k].Add(new Item { label = l, act = a }); }
        Add(0, "Auto chores", "auto"); Add(0, "Go charge", "charge"); Add(0, "DRIVE IT!", "drive");
        string[] ch = { "Sweep porch", "Vacuum garage", "Haul crates", "Haul hay", "Pick up litter", "Mow lawn", "Rake leaves", "Water flowers" };
        for (int i = 0; i < ch.Length; i++)
        {
            if (i == Chores.Mow) { Add(0, "Mow (riding)", "mow:ride"); Add(0, "Mow (push)", "mow:push"); continue; }   // ffu26 riding mower
            Add(0, ch[i], "chore:" + i);
        }
        string[] oc = { "come", "go", "stop", "wave", "roofHeli", "roofDrone", "follow", "dance" };
        string[] ol = { "Come here", "Go (8 m)", "Stop", "Wave", "Roof helipad", "Drone pad", "Follow me", "Dance" };
        for (int i = 0; i < oc.Length; i++) Add(1, ol[i], oc[i]);
        Add(1, "Fly your rocket", "rocket"); Add(1, "Hunt (bow)", "hunt");   // ffu50: Figure 03 only (Fig03Rocket50.cs)
        vehicles = RobotDriver.List();
        for (int i = 0; i < vehicles.Count && i < 12; i++) Add(2, vehicles[i].Title, "veh:" + i);
        string[] bands = { "10-story mech", "100-story mech", "1000-story mech", "Trillion mech" };
        for (int b = 0; b < 4; b++) Add(2, bands[b], "mech:" + b);
        Add(2, "Board (stand by)", "dmode:hold"); Add(2, "Walk / wander", "dmode:wander"); Add(2, "Follow me", "dmode:follow"); Add(2, "Race track", "dmode:race"); Add(2, "Go to my spot", "dmode:spot"); Add(2, "GET OUT", "getout");
        Add(3, "Mars rocks x3", "mis:0:3"); Add(3, "Mars rocks x5", "mis:0:5"); Add(3, "Callisto ice x4", "mis:1:4");
        Add(3, "Mech to Mars rocks", "mmis:0:4"); Add(3, "Mech to Callisto", "mmis:1:3"); Add(3, "Mech space patrol", "mmis:2:0"); Add(3, "Recall mech", "recall");   // ffu26
        // ffu42 WHEELS tab; ffu45: resizes the wheels of the vehicle the phone's user is IN (per vehicle), works in every world
        Add(5, "Smaller  -", "wheels:-"); Add(5, "Stock 1x", "wheels:1"); Add(5, "Bigger  +", "wheels:+");
        Add(5, "2x", "wheels:2"); Add(5, "3x", "wheels:3"); Add(5, "4x MONSTER", "wheels:4");
        Add(3, "Stash", "stash");   // ffu50: shared materials / arrows count (label is live, see Refresh)
        Add(3, "Abort mission", "abort"); Add(3, "Watch feed", "feed"); Add(3, "Feed full screen", "feedfull"); Add(3, "Feed cam: drone/eye", "feedcam");
        // ffu54 PLAY: everything a froggy can do, in whatever world the phone's user is in (World/RobotVisit54.cs)
        string[] pv = { "follow", "tag", "hop", "swim", "dive", "climb", "horse", "skate", "bike", "ridewith", "catch", "hunt", "cook", "swing", "hammock", "collect", "dance", "piggy", "build", "race", "wave", "lights", "fort", "bugs", "pets", "ridepet", "fetch", "stay", "home" };
        string[] pl = { "Follow me", "Play tag", "Hop / bounce", "Swim with me", "Dive for pearls", "Climb a tree", "Ride a horse", "Skateboard", "Ride a bike", "Ride with me", "Play catch", "Hunt (bow)", "Cook + eat", "Push my swing", "Hammock nap", "Collect stuff", "Dance", "Piggyback!", "Build a castle", "Race me!", "Wave hi", "Lights on / off", "Build the tree fort", "Bug hunt (jar)", "Pet + feed animals", "Ride a cow", "Fetch with the dogs", "Stay here", "Go home (chores)" };
        for (int i = 0; i < pv.Length; i++) Add(6, pl[i], "play:" + pv[i]);
    }

    // ffu54: DRIVE lists the vehicles next to the user first when off the ranch (Mars rover, moon buggy...), then the ranch ones
    void BuildDrive(Frog f)
    {
        var t = tabs[2]; t.Clear();
        vehicles = RobotDriver.ListFor(f);
        for (int i = 0; i < vehicles.Count && i < 12; i++) t.Add(new Item { label = vehicles[i].Title, act = "veh:" + i });
        string[] bands = { "10-story mech", "100-story mech", "1000-story mech", "Trillion mech" };
        for (int b = 0; b < 4; b++) t.Add(new Item { label = bands[b], act = "mech:" + b });
        t.Add(new Item { label = "Board (stand by)", act = "dmode:hold" }); t.Add(new Item { label = "Walk / wander", act = "dmode:wander" }); t.Add(new Item { label = "Follow me", act = "dmode:follow" });
        t.Add(new Item { label = "Race track", act = "dmode:race" }); t.Add(new Item { label = "Go to my spot", act = "dmode:spot" }); t.Add(new Item { label = "GET OUT", act = "getout" });
        if (pickVeh != null && !vehicles.Contains(pickVeh)) pickVeh = null;
    }

    // ---------- 3D turntable (Showroom studio set, own stand at the showroom base) ----------
    void BuildStand()
    {
        Showroom.Init();
        standRoot = new GameObject("PhoneStand").transform;
        standRoot.position = Showroom.Base + new Vector3(seat * 40f, 0f, 260f);   // ffu59b: one studio stand per seat, 40 m apart
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
        standCam.enabled = open && card.gameObject.activeSelf;
        if (!standCam.enabled) return;
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
    int toggleFrame = -1;
    public static int Opens, Closes;
    public void Toggle(Frog f)
    {
        if (f == null) return;
        { var mine = Of(f); if (mine != null && mine != this) { mine.Toggle(f); return; } }   // ffu59b: every seat has its own phone
        // ffu35: one phone press per frame. Two slots fed by one physical pad (a mirrored "ghost" pad) used to toggle it
        // twice in one frame - open for the ghost froggy, then open again for the real one - so LB never closed it.
        if (Time.frameCount == toggleFrame) return;
        toggleFrame = Time.frameCount;
        if (f.remote != null) { f.remote.ReleaseManual(true); return; }
        if (!Ok(f)) return;
        if (open && user == f) { Close(f, "toggle"); return; }
        open = !open || user != f;
        if (open) Opens++;
        Debug.Log("RobotPhone: " + (open ? "open" : "closed") + " for " + f.nick + " (" + f.inputKind + ", seat " + (seat + 1) + " phone)");
        user = f;
        if (open) { BuildCalls(f); BuildDrive(f); if (f.world != WorldId.Ranch) { tab = 4; cur = NT; msg.text = "CALL a robot or a froggy to " + Robot.WorldName(Robot.TargetWorld(f)) + " - every order works here"; } }
        Sfx.Play(Sfx.Click, 0.7f);
        if (open) f.Toast(f.inputKind == InputKind.Gamepad ? "Robot phone: stick picks, A sends - LB or B closes" : f.inputKind == InputKind.Touch ? "Robot phone: tap PHONE to close" : "Robot phone: W/S robot, A/D order, E send - P closes", 3f);
    }

    // ffu35: always closes for its user, whatever world / vehicle / split screen (LB, B, P, the PHONE button)
    public void Close(Frog f, string why)
    {
        if (!open) return;
        toggleFrame = Time.frameCount;
        open = false; Closes++;
        Sfx.Play(Sfx.Click, 0.6f, 0.9f);
        Debug.Log("RobotPhone: closed (" + why + ") by " + (f != null ? f.nick : "?"));
    }

    // ffu27 (Bill: "wherever James goes, his phone goes with him"): the phone works in every world - Mars, Callisto,
    // space / orbit, Mercury / Enceladus / the Deep chambers (story places on the Mars / Callisto ids), underwater, the
    // house - on foot, and off the ranch also inside a vehicle (the Starship in orbit). Not in the REAL ROOM (own camera +
    // first-person input) and not during the ranch blast-off / landing.
    // ffu54: the REAL ROOM too (C3) - the phone is a screen overlay, its input is taken before the room's first-person input
    public static bool Ok(Frog f)
    {
        if (f == null || f.launching) return false;
        return f.vehicle == null || f.remote != null || f.world != WorldId.Ranch;
    }

    void BuildCalls(Frog f)
    {
        var t = tabs[4]; t.Clear();
        t.Add(new Item { label = "Call this robot", act = "rcall:sel" });   // ffu54: robots come to any world (label shows the robot)
        t.Add(new Item { label = "Call ALL robots", act = "rcall:all" });
        t.Add(new Item { label = "Call everyone", act = "call:all" });
        if (Game.I != null)
            for (int k = 0; k < Game.I.frogs.Count; k++)
            {
                var o = Game.I.frogs[k];
                if (o == null || o == f || !o.gameObject.activeInHierarchy || o.world == WorldId.RealRoom) continue;
                t.Add(new Item { label = "Call " + o.nick, act = "call:" + k });
            }
    }

    public void Say(string s) { if (msg != null) msg.text = s; if (user != null && s.Length > 0) user.Toast(s, 2.5f); }

    // demo / screenshot mode: robot index + an action string (or a tab name)
    public void DemoOpen(Frog f, int robot, string act)
    {
        { var mine = Of(f); if (mine != null && mine != this) { mine.DemoOpen(f, robot, act); return; } }   // ffu59b
        demoPh = this;
        user = f; open = true; sel = robot;
        BuildCalls(f); BuildDrive(f);
        for (int k = 0; k < NT; k++)
        {
            if (TabNames[k] == act) { tab = k; cur = k; return; }
            int j = tabs[k].FindIndex(it => it.act == act);
            if (j >= 0) { tab = k; cur = NT + j; msg.text = RanchLife.I.robots[robot].robotName + " selected - " + tabs[k][j].label; return; }
        }
    }
    public void DemoSend() { var p = demoPh != null ? demoPh : this; if (p.user != null) p.Activate(p.user); }
    public void DemoAct(Frog f, int robot, string act) { DemoOpen(f, robot, act); Activate(f); }

    int Count { get { return NT + tabs[tab].Count; } }

    // called by Game with the phone user's input; returns true when the phone ate the input
    public bool Handle(Frog f, PIn i)
    {
        if (!open || f != user) return false;
        if (!Ok(f) || f.remote != null) { open = false; return false; }
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
        if (i.use || i.hop || (f.inputKind == InputKind.Keyboard && Kb.EnterDown())) Activate(f);
        if (f.inputKind == InputKind.Gamepad)
        {
            if (i.warpUp) { tab = (tab + 1) % NT; cur = tab; Sfx.Play(Sfx.Click, 0.4f); }          // RB next tab
            if (i.land) Do(f, "feed");                                                          // Y
            if (i.auto) Do(f, "feedfull");                                                      // X
            if (i.cargo) { Close(f, "B"); return true; }                                        // ffu35: B closes (was feed camera)
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
        if (cur < NT) { tab = cur; Sfx.Play(Sfx.Click, 0.5f); return; }
        int j = cur - NT;
        if (j < tabs[tab].Count) Do(f, tabs[tab][j].act);
    }

    void Do(Frog f, string a)
    {
        string say = "";
        // ffu27 CALL tab: works in every world
        if (a.StartsWith("call:"))
        {
            string w = a.Substring(5);
            int fi; Frog who = null;
            if (w != "all" && int.TryParse(w, out fi) && Game.I != null && fi >= 0 && fi < Game.I.frogs.Count) who = Game.I.frogs[fi];
            say = Frog.Call(f, who);
            msg.text = say; if (say.Length > 0) f.Toast(say, 2.5f);
            return;
        }
        if (a.StartsWith("wheels:"))
        {
            string w = a.Substring(7);
            Vehicle wv = f.vehicle;
            if (!BigWheels.Resizable(wv)) { say = "Wheel size: get in a vehicle to resize its wheels"; msg.text = say; f.Toast(say, 1.8f); Sfx.Play(Sfx.Click, 0.4f, 0.7f); return; }
            if (w == "-") BigWheels.NudgeFor(wv, -1, "phone"); else if (w == "+") BigWheels.NudgeFor(wv, 1, "phone");
            else { float fv; if (float.TryParse(w, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out fv)) BigWheels.SetFor(wv, fv, "phone"); }
            say = "Wheel size " + BigWheels.Readout(wv) + " (this vehicle only)";
            msg.text = say; f.Toast(BigWheels.Readout(wv), 1.6f); Sfx.Play(Sfx.Pickup, 0.45f, 0.8f + BigWheels.FracOf(BigWheels.Want(wv)) * 0.6f);
            return;
        }
        // ffu54 ROBOTS EVERYWHERE (C2): no more "that order works on the ranch" - every order works from every world.
        // Orders about the user (CALL, PLAY, come / follow / wave / dance, DRIVE IT!, vehicles next to the user) bring the
        // robot to the user's world first; ranch-place orders (chores, charging, roof pads, ranch vehicles, missions) fly it home.
        if (a == "rcall:all")
        {
            int n = 0;
            if (RanchLife.I != null) foreach (var rb in RanchLife.I.robots) { if (rb.mission == null && RobotMechMission.For(rb) == null) { rb.Call(f, null); n++; } }
            say = n > 0 ? "Calling all " + n + " robots to " + Robot.WorldName(Robot.TargetWorld(f)) + "!" : "The robots are all on missions";
            msg.text = say; f.Toast(say, 2.5f); Sfx.Play(Sfx.Pickup, 0.5f, 1.4f); return;
        }
        var r = Selected;
        if (r == null) return;
        if (a == "rcall:sel") { say = r.Call(f, null); msg.text = say; if (say.Length > 0) f.Toast(say, 2.5f); Sfx.Play(Sfx.Pickup, 0.5f, 1.3f); return; }
        if (a.StartsWith("play:")) { say = r.Play(a.Substring(5), f); msg.text = say; if (say.Length > 0) f.Toast(say, 2.5f); Sfx.Play(Sfx.Pickup, 0.5f, 1.3f); return; }
        bool userAway = Robot.TargetWorld(f) != WorldId.Ranch;
        if (a == "drive" && (userAway || r.world != WorldId.Ranch) && (r.visit == null || r.world != Robot.TargetWorld(f) || r.visit.ph == 0))
        {
            say = r.Call(f, null);
            if (r.visit != null) { Robot rr = r; Frog ff = f; r.visit.onArrive = () => { rr.TakeManual(ff); open = false; }; say += " Then you drive me!"; }
            msg.text = say; f.Toast(say, 2.5f); return;
        }
        if ((a.StartsWith("mis:") || a.StartsWith("mmis:") || a == "rocket" || a == "hunt") && r.world != WorldId.Ranch) r.ComeHomeFor(a);
        var mm = RobotMechMission.For(r);
        if (mm != null && a != "recall" && a != "abort" && !a.StartsWith("feed"))
        {
            say = r.robotName + " is flying a mech mission (" + mm.PhaseLine + ") - Recall mech to bring it home";
            msg.text = say; f.Toast(say, 2.2f); Sfx.Play(Sfx.Click, 0.5f); return;
        }
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
            else if (v != null && userAway && RobotDriver.ListFor(f).IndexOf(v) >= 0 && (v.transform.position - Robot.LeadPos(f)).sqrMagnitude < 400f * 400f && (r.world != Robot.TargetWorld(f) || r.visit == null || r.visit.ph == 0))
            {
                // ffu54: a vehicle in the user's world (Mars rover, moon buggy): come over first, then take it
                say = r.Call(f, null);
                string md = a.Substring(6); Vehicle vv = v; Robot rr = r; Frog ff = f;
                if (r.visit != null) { r.visit.onArrive = () => { string s2 = rr.DriveOrder(vv, md, ff); ff.Toast(s2, 2.5f); Say(s2); }; say += " Then the " + v.Title + "!"; }
            }
            else
            {
                if (v != null && r.world != WorldId.Ranch && !(userAway && (v.transform.position - Robot.LeadPos(f)).sqrMagnitude < 400f * 400f)) r.ComeHomeFor("drive " + v.Title);   // a ranch vehicle: fly home for it
                say = r.DriveOrder(v, a.Substring(6), f);
            }
        }
        else if (a == "getout") { if (r.drv != null) { r.drv.GetOut(); say = r.robotName + ": getting out"; } else say = r.robotName + " isn't driving"; }
        else if (a.StartsWith("mis:"))
        {
            var ps = a.Split(':');
            say = RobotMission.Start(r, int.Parse(ps[1]), int.Parse(ps[2]));
            if (r.mission != null && RobotFeed.I != null) RobotFeed.I.ShowFor(r, 1, f);   // start watching straight away
        }
        else if (a.StartsWith("mmis:"))
        {
            // ffu26: the mech the robot is piloting, else the band picked in DRIVE, else the nearest free 10-story
            var ps = a.Split(':');
            StoryMech mech = r.drv != null ? r.drv.v as StoryMech : null;
            if (mech == null) mech = RobotDriver.MechOfBand(pickBand >= 0 ? pickBand : 0, r.transform.position);
            say = RobotMechMission.Start(r, mech, int.Parse(ps[1]), int.Parse(ps[2]), f);
            if (RobotMechMission.For(r) != null && RobotFeed.I != null) RobotFeed.I.ShowFor(r, 1, f);
        }
        else if (a == "recall")
        {
            if (mm != null) say = mm.Recall();
            else if (r.drv != null && r.drv.v is StoryMech) { r.drv.user = f; r.drv.SetMode("home"); say = r.robotName + ": walking " + r.drv.v.Title + " home"; }
            else say = r.robotName + " isn't in a mech";
        }
        else if (a == "rocket") say = Fig50.PhoneRocket(r, f);   // ffu50: owner-locked to Figure 03
        else if (a == "hunt") say = Fig50.PhoneHunt(r, f);
        else if (a == "stash") say = "Stash: " + Stash50.Line;
        else if (a == "abort") say = mm != null ? mm.Recall() : r.mission != null ? r.mission.Abort() : r.robotName + " isn't on a mission";
        else if (a == "feed") { if (RobotFeed.I != null) { bool on = RobotFeed.I.mode > 0 && RobotFeed.I.robot == r && RobotFeed.I.viewer == f; RobotFeed.I.ShowFor(r, on ? 0 : 1, f); say = on ? "Feed off" : "Watching " + r.robotName + "'s camera (O)"; } }
        else if (a == "feedfull") { if (RobotFeed.I != null) { RobotFeed.I.ShowFor(r, RobotFeed.I.mode == 2 && RobotFeed.I.robot == r && RobotFeed.I.viewer == f ? 1 : 2, f); say = "Feed " + (RobotFeed.I.mode == 2 ? (RobotFeed.I.InSplit ? "fills your screen (Esc / tap to shrink)" : "full screen (Esc / tap to shrink)") : "picture-in-picture"); } }
        else if (a == "feedcam") { if (RobotFeed.I != null) { if (RobotFeed.I.mode == 0) RobotFeed.I.ShowFor(r, 1, f); RobotFeed.I.ToggleCam(); say = "Feed camera: " + (RobotFeed.I.eye ? "robot's eye" : "drone"); } }
        else say = r.Order(a, f);
        msg.text = say;
        if (say.Length > 0) f.Toast(say, 2.2f);
        Sfx.Play(Sfx.Pickup, 0.5f, 1.3f);
    }

    // ffu59b: the PHONE button of THIS seat's player (each split view shows its own)
    Slot BtnSlot()
    {
        if (Game.I == null) return null;
        return Game.I.SlotOfSeat(seat);
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
        for (int i = 0; i < NT; i++) if (In(tabBtns[i], p)) { tab = i; cur = i; Sfx.Play(Sfx.Click, 0.5f); return; }
        for (int i = 0; i < itemBtns.Count; i++) if (In(itemBtns[i], p) && i < tabs[tab].Count) { cur = NT + i; if (user != null) Do(user, tabs[tab][i].act); return; }
        if (In(card, p)) { dragOn = true; dragX0 = p.x; }
    }

    void Update()
    {
        Slot bs = BtnSlot();
        Frog bf = bs != null ? Game.I.frogs[bs.frog] : null;
        bool showBtn = Game.I != null && Game.I.state == Game.State.Play && bf != null && (Ok(bf) || bf.remote != null) && !Game.I.HelpOpen;   // ffu27: every world
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
                if (RectTransformUtility.RectangleContainsScreenPoint(btn.rectTransform, tp, null) && bs.kind == InputKind.Touch) Toggle(bf);
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
        if (user != null && (!Ok(user) || Game.I == null || Game.I.state != Game.State.Play)) open = false;
        if (open && (bs == null || bf != user)) open = false;   // ffu59b: the seat's player left / changed
        panel.gameObject.SetActive(open);
        if (open || showBtn) FitViewport(bs);
        TickStand(Mathf.Min(Time.unscaledDeltaTime, 0.1f));
        if (!open) return;
        Refresh();
    }

    // ffu59b: the phone + PHONE button sit inside this player's viewport: the camera rect in split-screen, a column of
    // the screen in shared view with several phones, else the whole screen (single player = unchanged look)
    public Rect ViewRect(Slot bs)
    {
        if (Game.I == null || bs == null) return new Rect(0f, 0f, 1f, 1f);
        int n = Game.I.slots.Count;
        if (n <= 1) return new Rect(0f, 0f, 1f, 1f);
        if (bs.cam != null && bs.cam.enabled && Game.LastViews > 1) return bs.cam.rect;
        int k = Mathf.Max(0, Game.I.slots.IndexOf(bs));
        return new Rect(k / (float)n, 0f, 1f / n, 1f);   // shared camera: one column each
    }

    void FitViewport(Slot bs)
    {
        Rect vr = ViewRect(bs);
        vp.anchorMin = vr.min; vp.anchorMax = vr.max; vp.offsetMin = vp.offsetMax = Vector2.zero;
        var cr = (RectTransform)canvas.transform;
        float vw = cr.rect.width * vr.width, vh = cr.rect.height * vr.height;
        bool full = vr.width > 0.99f && vr.height > 0.99f;
        bool compact = !full && vh < 560f;
        float shift = compact ? CardH + 22f : 0f;
        float ph = PH - shift;
        float sc = Mathf.Min(1f, (vh - (full ? 60f : 12f)) / ph, (vw - 16f) / PW);
        int key = (compact ? 1 : 0) + Mathf.RoundToInt(sc * 1000f) * 2 + Mathf.RoundToInt(vw) * 4000 + Mathf.RoundToInt(vh) * 9000000;
        if (key == layoutKey) return;
        layoutKey = key;
        card.gameObject.SetActive(!compact);
        foreach (var d in dots) d.gameObject.SetActive(!compact);
        for (int i = 0; i < shiftRT.Count; i++) shiftRT[i].anchoredPosition = shiftPos[i] + new Vector2(0f, shift);
        panel.rectTransform.sizeDelta = new Vector2(PW, ph);
        panel.rectTransform.localScale = Vector3.one * sc;
        panel.rectTransform.anchoredPosition = new Vector2(-PW * sc * 0.5f - (full ? 12f : 6f), full ? -24f : 0f);
        float bsc = full ? 1f : Mathf.Clamp(sc * 1.1f, 0.6f, 1f);
        btn.rectTransform.localScale = Vector3.one * bsc;
        btn.rectTransform.anchoredPosition = full ? new Vector2(98f, -76f) : new Vector2(75f * bsc + 8f, -(23f * bsc + 8f) - (vr.yMax > 0.99f ? 40f : 0f));
        Debug.Log("RobotPhone: seat " + (seat + 1) + " layout " + (full ? "full screen" : "viewport " + vr.ToString("0.00")) + (compact ? " compact" : "") + " scale " + sc.ToString("0.00") + " (" + vw.ToString("0") + "x" + vh.ToString("0") + " canvas units)");
    }

    void Refresh()
    {
        var robots = RanchLife.I.robots;
        var r = robots[sel];
        string nm = r.robotName;
        if (nameT.text != nm) nameT.text = nm;
        // ffu59b: whose phone it is (split) + the picked robot in the header when the card is hidden (compact)
        bool many = Game.I != null && Game.I.slots.Count > 1;
        int pn = Game.I != null && user != null ? Game.I.slots.IndexOf(Game.I.SlotOfSeat(seat)) : -1;
        string hd = (many && pn >= 0 ? "<color=" + Game.PlayerHex(pn) + ">P" + (pn + 1) + "</color>  " : "") + "ROBOT PHONE" + (!card.gameObject.activeSelf ? "  <color=#ffffff>·  " + nm + "</color>" : "");
        if (head.text != hd) { head.supportRichText = true; head.text = hd; }
        string ow = (r.owner.Length > 0 ? r.owner + "'s robot" : "Ranch robot") + "  ·  " + r.height.ToString("0.0") + " m";
        if (ownerT.text != ow) ownerT.text = ow;
        float b = Mathf.Clamp01(r.battery);
        batFill.rectTransform.sizeDelta = new Vector2(Mathf.Max(10f, 90f * b), 14f);
        batFill.color = r.Charging ? new Color(0.45f, 1f, 0.5f) : b < r.lowAt ? new Color(1f, 0.62f, 0.15f) : new Color(0.55f, 0.9f, 1f);
        batT.text = (r.Charging ? "+" : "") + r.Pct;
        jobT.text = r.StatusLine;
        for (int i = 0; i < dots.Count; i++) { dots[i].color = i == sel ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 1f, 1f, 0.3f); dots[i].rectTransform.sizeDelta = Vector2.one * (i == sel ? 10f : 7f); }
        for (int i = 0; i < NT; i++)
            tabBtns[i].color = i == tab ? (cur == i ? new Color(1f, 0.75f, 0.2f, 0.95f) : new Color(0.2f, 0.6f, 0.4f, 0.9f)) : (cur == i ? new Color(1f, 0.75f, 0.2f, 0.6f) : new Color(1f, 1f, 1f, 0.1f));
        var items = tabs[tab];
        for (int i = 0; i < itemBtns.Count; i++)
        {
            bool has = i < items.Count;
            if (itemBtns[i].gameObject.activeSelf != has) itemBtns[i].gameObject.SetActive(has);
            if (!has) continue;
            string act = items[i].act;
            string lbl = act == "rcall:sel" ? "Call " + r.robotName : act == "stash" ? Stash50.Short : act == "hunt" && r.id == Fig03Rocket.Owner ? "Hunt (bow, " + Fig50.ArrowsFor(r) + " arrows)" : items[i].label;   // ffu50 live labels
            if (itemLbls[i].text != lbl) itemLbls[i].text = lbl;
            Color c = tab == 0 ? (i < 3 ? new Color(0.15f, 0.55f, 0.3f, 0.78f) : new Color(0.12f, 0.45f, 0.5f, 0.72f))
                    : tab == 1 ? new Color(0.2f, 0.35f, 0.7f, 0.65f)
                    : tab == 2 ? (act.StartsWith("dmode") || act == "getout" ? new Color(0.55f, 0.3f, 0.75f, 0.78f) : new Color(0.16f, 0.3f, 0.45f, 0.75f))
                    : (act.StartsWith("mis") ? new Color(0.7f, 0.32f, 0.12f, 0.82f) : act.StartsWith("mmis") || act == "recall" ? new Color(0.62f, 0.2f, 0.42f, 0.85f) : new Color(0.2f, 0.32f, 0.5f, 0.75f));
            bool picked = (act.StartsWith("veh:") && pickVeh != null && vehicles.IndexOf(pickVeh) == int.Parse(act.Substring(4))) || (act.StartsWith("mech:") && pickBand == int.Parse(act.Substring(5)));
            if (picked) c = new Color(0.1f, 0.75f, 0.85f, 0.95f);
            if (tab == 4) c = act == "call:all" ? new Color(0.15f, 0.6f, 0.3f, 0.85f) : act.StartsWith("rcall") ? new Color(0.55f, 0.32f, 0.7f, 0.88f) : new Color(0.18f, 0.42f, 0.32f, 0.78f);
            if (tab == 6) c = act == "play:home" ? new Color(0.2f, 0.32f, 0.5f, 0.75f) : act == "play:piggy" || act == "play:build" || act == "play:race" ? new Color(0.75f, 0.3f, 0.55f, 0.88f) : new Color(0.55f, 0.25f, 0.6f, 0.78f);
            if (tab == 5) { float wv; bool now = act.Length == 8 && float.TryParse(act.Substring(7), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out wv) && user != null && BigWheels.Resizable(user.vehicle) && Mathf.Abs(wv - BigWheels.Want(user.vehicle)) < 0.01f; c = now ? new Color(0.1f, 0.75f, 0.85f, 0.95f) : new Color(0.5f, 0.38f, 0.12f, 0.8f); }
            if (cur == NT + i) c = new Color(1f, 0.75f, 0.2f, 0.95f);
            itemBtns[i].color = c;
        }
        if (tab == 5) { Vehicle uv = user != null ? user.vehicle : null; string wl = BigWheels.Resizable(uv) ? "WHEEL SIZE " + BigWheels.LabelFor(uv) + " - " + uv.Title + " only (1x - 4x)" : "Get in a vehicle to resize its wheels"; if (msg.text != wl && !msg.text.StartsWith("Wheel size")) msg.text = wl; }
        InputKind ik = user != null ? user.inputKind : InputKind.Keyboard;
        string h = ik == InputKind.Gamepad ? "Up/Down robot · Left/Right order · A send · RB tab · Y feed · X full · LB / B close"
                 : ik == InputKind.Touch ? "Swipe the robot · tap a tab, then an order · PHONE closes"
                 : "W/S robot · A/D order · E/Enter/click send · O feed · I cam · P close";
        if (hint.text != h) hint.text = h;
    }
}
