using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Slot
{
    public InputKind kind;
    public Gamepad pad;
    public int frog;          // index into Game.frogs / Froggies
    public float joinTime;
    public Camera cam;
    public CamRig rig;
    public ViewHud hud;
    public PIn last;
    public WorldId world = WorldId.Ranch;
}

// Lobby (press A to claim a frog), 1-4 player split-screen or shared camera, input routing, mid-game joins.
[DefaultExecutionOrder(-50)]
public class Game : MonoBehaviour
{
    public static Game I;
    public enum State { Lobby, Play }
    public State state = State.Lobby;

    public readonly List<Frog> frogs = new List<Frog>();
    public readonly List<Slot> slots = new List<Slot>();
    public bool shared;            // Shared camera vs split-screen
    bool help;

    readonly HashSet<int> ghosts = new HashSet<int>();
    readonly Dictionary<int, float> pressTimes = new Dictionary<int, float>();
    readonly Dictionary<int, string> pressSig = new Dictionary<int, string>();

    Camera overview, sharedCam;
    float orbit, sharedYaw = 180f, sharedPitch = 48f, sharedZoom = 1f, sharedTrauma, autoStartT = -1f, lastViewToggle = -10f;
    Vector3 sharedFocus;
    bool sharedInit, mobileAutoJoined;

    Canvas lobbyCanvas, hudCanvas;
    Text lobbyStatus, viewText, joinText, helpText;
    Image viewBar, playBtn, helpBg;
    float lastTouchTime = -10f;
    readonly Image[] cards = new Image[4];
    readonly Text[] cardTexts = new Text[4];
    readonly Image[] swatches = new Image[4];
    Image lobbyBg, soundBtn;
    Text lobbyTitle, lobbySub, lobbyHelp, soundText;
    int lobbyLayout = -1;      // 0 landscape, 1 portrait
    int hudLayout = -1;
    ViewHud sharedHud;
    Image sepV, sepH;
    TouchControls touch;

    void Awake()
    {
        I = this;
        overview = MakeCam("OverviewCam", -10);
        sharedCam = MakeCam("SharedCam", 1);
        sharedCam.enabled = false;
        touch = gameObject.AddComponent<TouchControls>();
        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject("Frog" + i);
            Frog f = go.AddComponent<Frog>();
            f.Build(i, Ranch.FrogSpawn(i), 0f);
            frogs.Add(f);
        }
        BuildLobbyUI();
        BuildHudUI();
    }

    static Camera MakeCam(string name, int depth)
    {
        var g = new GameObject(name);
        Camera c = g.AddComponent<Camera>();
        c.depth = depth;
        Worlds.SetCamera(c, WorldId.Ranch);
        c.fieldOfView = 60f;
        c.clearFlags = CameraClearFlags.Skybox;
        return c;
    }

    // ---------------- UI ----------------
    void BuildLobbyUI()
    {
        lobbyCanvas = UIK.MakeCanvas("Lobby", null, 100, true);
        Transform r = lobbyCanvas.transform;
        lobbyBg = UIK.Img(r, null, new Color(0.03f, 0.08f, 0.05f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 650));
        lobbyTitle = UIK.Label(r, "FOUR FROGGIES", 70, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 262), new Vector2(1100, 90), new Color(0.55f, 1f, 0.45f));
        lobbySub = UIK.Label(r, "James's ranch: hop around, jump in any vehicle, blow stuff up. 1-4 players.", 24, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 200), new Vector2(1100, 40), Color.white);
        for (int i = 0; i < 4; i++)
        {
            Vector2 p = new Vector2(-420 + i * 280, 60);
            cards[i] = UIK.Img(r, null, new Color(1, 1, 1, 0.12f), new Vector2(0.5f, 0.5f), p, new Vector2(255, 200));
            swatches[i] = UIK.Img(r, UIK.Circle, Froggies.Color(i), new Vector2(0.5f, 0.5f), p + new Vector2(0, 52), new Vector2(70, 70));
            cardTexts[i] = UIK.Label(r, "", 24, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), p + new Vector2(0, -38), new Vector2(240, 110), Color.white);
        }
        viewBar = UIK.Img(r, null, new Color(0f, 0f, 0f, 0.45f), new Vector2(0.5f, 0.5f), new Vector2(0, -88), new Vector2(620, 44));
        viewText = UIK.Label(viewBar.transform, "", 22, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(610, 42), Color.white);
        playBtn = UIK.Img(r, null, new Color(0.2f, 0.65f, 0.25f, 0.85f), new Vector2(0.5f, 0.5f), new Vector2(0, -148), new Vector2(300, 56));
        UIK.Label(playBtn.transform, "PLAY", 34, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 56), Color.white);
        lobbyStatus = UIK.Label(r, "", 24, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -204), new Vector2(1150, 40), new Color(0.7f, 1f, 0.7f));
        lobbyHelp = UIK.Label(r, "", 19, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -262), new Vector2(1180, 60), new Color(1, 1, 1, 0.85f));
        soundBtn = UIK.Img(r, null, new Color(0f, 0f, 0f, 0.45f), new Vector2(0.5f, 0.5f), new Vector2(430, -148), new Vector2(200, 44));
        soundText = UIK.Label(soundBtn.transform, "", 20, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200, 42), Color.white);
        creditsBtn = UIK.Img(r, null, new Color(0f, 0f, 0f, 0.5f), new Vector2(0f, 1f), new Vector2(84, -26), new Vector2(150, 34));
        var cl = UIK.Label(creditsBtn.transform, "CREDITS (C)", 17, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 34), new Color(0.8f, 0.95f, 1f));
        UIK.Stretch(cl.rectTransform);
        creditsPanel = UIK.Img(r, null, new Color(0.01f, 0.05f, 0.03f, 0.95f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180, 640));
        var ct = UIK.Label(creditsPanel.transform, CreditsText, 17, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1120, 610), Color.white);
        ct.supportRichText = true;
        ct.horizontalOverflow = HorizontalWrapMode.Wrap;
        UIK.Stretch(ct.rectTransform);
        ct.rectTransform.offsetMin = new Vector2(30, 14); ct.rectTransform.offsetMax = new Vector2(-30, -14);
        creditsPanel.gameObject.SetActive(false);
    }

    Image creditsBtn, creditsPanel;
    bool lobbyLook;
    const string CreditsText =
        "<size=28><color=#8cff70><b>CREDITS</b></color></size>\n" +
        "<b>Unitree</b> (Unitree H1): Unitree Robotics' official robot model (github.com/unitreerobotics/unitree_mujoco).\n" +
        "   Copyright (c) 2016-2024 HangZhou YuShu TECHNOLOGY CO.,LTD. (\"Unitree Robotics\"). BSD 3-Clause licence. Logo plate removed.\n" +
        "<b>Atlas HD</b> (hydraulic DRC Atlas): the MIT Atlas model from RobotLocomotion/models (Drake).\n" +
        "   Copyright 2012-2022 Robot Locomotion Group @ CSAIL. BSD 3-Clause licence. Team / sponsor logos removed.\n" +
        "<b>Optimus, Figure 02, Big Figure Two, Figure 03, Atlas electric</b>, the froggies and the Cybertruck: modelled for this game\n" +
        "   from public photos (no logos).\n" +
        "<b>Trees, bushes, rocks, flowers</b>: Stylized Nature MegaKit by Quaternius (CC0).  <b>Cows, horses, sheep, pig</b>: Farm Animals\n" +
        "   pack by Quaternius (CC0).  <b>Sky</b> (Kloofendal 48d Partly Cloudy) and <b>grass / dirt / sand / wood / barn / metal textures</b>:\n" +
        "   Poly Haven (polyhaven.com), CC0.\n\n" +
        "Full licence texts: similarize.com/games/four-froggies-unity/LICENSES.txt\n" +
        "Robot and vehicle names describe the real machines only; this fan game is not affiliated with their makers.\n\n" +
        "<color=#9fd8ff>Press C, Esc or tap to close</color>";

    void CreditsToggle(bool? on = null)
    {
        if (creditsPanel == null) return;
        bool v = on ?? !creditsPanel.gameObject.activeSelf;
        creditsPanel.gameObject.SetActive(v);
        creditsPanel.transform.SetAsLastSibling();
    }

    // ---------------- demo / screenshot mode ----------------
    // ?ffdemo=1 in the page URL: joins keyboard P1 after ~5 s in the lobby and starts. &ffshot=<scene> pins P1's camera to a
    // showcase view (frog, robot, truck, ranch, barn, pond, house, under, space); &ffshot=tour cycles them every 9 s.
    // For work/webgl-probe/probe.py screenshots. Logs "FFDEMO scene <name>" whenever the view changes.
    static readonly string[] Tour = { "frog", "robot", "truck", "ranch", "barn", "pond", "house", "under", "space" };
    float demoT = -1f, demoPlayT;
    string demoShot = "", demoCur = "";
    bool DemoLobby()
    {
        if (demoT < 0f)
        {
            string u = Application.absoluteURL ?? "";
            demoT = u.Contains("ffdemo") ? 0.01f : 0f;
            int k = u.IndexOf("ffshot=");
            if (k >= 0) { demoShot = u.Substring(k + 7); int e = demoShot.IndexOfAny(new[] { '&', '#' }); if (e >= 0) demoShot = demoShot.Substring(0, e); }
            if (demoT > 0f) Debug.Log("Four Froggies: demo mode " + demoShot);
        }
        if (demoT <= 0f) return false;
        demoT += Time.unscaledDeltaTime;
        if (demoT > 5f && slots.Count == 0) { Join(InputKind.Keyboard, null); return false; }
        if (demoT > 6f && slots.Count > 0) { demoT = 0.01f; StartPlay(); demoPlayT = 0f; return true; }
        return false;
    }

    void DemoView()
    {
        if (demoShot.Length == 0 || slots.Count == 0 || slots[0].cam == null) return;
        demoPlayT += Time.unscaledDeltaTime;
        string sc = demoShot == "tour" ? Tour[Mathf.Min(Tour.Length - 1, (int)(demoPlayT / 9f))] : demoShot;
        Frog f = frogs[slots[0].frog];
        if (sc != demoCur)
        {
            demoCur = sc;
            Debug.Log("FFDEMO scene " + sc + " t=" + Time.realtimeSinceStartup.ToString("0.0"));
            if (sc == "house") f.SendTo(WorldId.House, HouseWorld.Spawn(f.id), 180f);
            else if (sc == "under" && UnderwaterWorld.I != null) { f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f); UnderwaterWorld.I.Dive(f); }
            else if (sc == "space") DemoSpace(f);
            else if (f.world != WorldId.Ranch) f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f);
        }
        Camera c = slots[0].cam;
        Vector3 pos, look;
        float gy;
        switch (sc)
        {
            case "frog":
                for (int i = 0; i < 3; i++) frogs[i].DemoPose(new Vector3(-12f + i * 1.5f, 0f, 38f), 0f);
                pos = new Vector3(-10.5f, Ranch.GY(-10.5f, 42.6f) + 1.35f, 42.6f); look = new Vector3(-10.5f, Ranch.GY(-10.5f, 38f) + 0.6f, 38f); break;
            case "robot":
                gy = Ranch.GY(-30f, 18f);
                pos = new Vector3(-24f, gy + 2.6f, 27f); look = new Vector3(-31f, gy + 1.4f, 17.5f); break;
            case "truck":
                gy = Ranch.GY(14f, 44f);
                pos = new Vector3(20.5f, gy + 2.2f, 50.5f); look = new Vector3(14f, gy + 1f, 44f); break;
            case "barn":
                gy = Ranch.GY(-140f, 112f);
                pos = new Vector3(-118f, gy + 7f, 104f); look = new Vector3(-148f, gy + 1.5f, 124f); break;
            case "pond":
                pos = new Vector3(10f, 9f, -40f); look = new Vector3(70f, -1f, -80f); break;
            case "ranch":
                pos = new Vector3(45f, 26f, 95f); look = new Vector3(-20f, 0f, 0f); break;
            default:
                return;   // house / under / space: the normal follow camera
        }
        c.transform.position = pos;
        c.transform.LookAt(look);
    }

    void DemoSpace(Frog f)
    {
        // straight into Earth orbit in the Starship (skips the launch sequence)
        if (SpaceWorld.I == null) { Debug.Log("FFDEMO: no space world"); return; }
        f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f);
        SpaceWorld.I.ToOrbit(f, "earth");
    }

    void BuildHudUI()
    {
        hudCanvas = UIK.MakeCanvas("HUD", null, 50, true);
        Transform r = hudCanvas.transform;
        sharedHud = new ViewHud(r, "Shared");
        sharedHud.SetActive(false);
        sepV = UIK.Img(r, null, new Color(0f, 0f, 0f, 0.85f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4f, 0f));
        sepV.rectTransform.anchorMin = new Vector2(0.5f, 0f); sepV.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        sepV.rectTransform.sizeDelta = new Vector2(4f, 0f);
        sepH = UIK.Img(r, null, new Color(0f, 0f, 0f, 0.85f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 4f));
        sepH.rectTransform.anchorMin = new Vector2(0f, 0.5f); sepH.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        sepH.rectTransform.sizeDelta = new Vector2(0f, 4f);
        joinText = UIK.Label(r, "Press A / Start to join", 28, TextAnchor.MiddleCenter, new Vector2(0.75f, 0.25f), Vector2.zero, new Vector2(500, 60), Color.white);
        helpBg = UIK.Img(r, null, new Color(0f, 0f, 0f, 0.75f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 590));
        helpText = UIK.Label(r, "", 21, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1150, 560), Color.white);
        helpBg.enabled = false;
        helpText.text = HelpBody;
        helpText.enabled = false;
        joinText.enabled = false;
        hudCanvas.enabled = false;
    }

    static string HelpBody { get { return
            "<size=34><color=#8cff70>FOUR FROGGIES - CONTROLS</color></size>\n\n" +
            "<b>Gamepad</b>  L-stick move / steer  |  R-stick camera (R3 resets it)  |  A hop, get in / out  |  D-pad up/down zoom\n" +
            "Cars, Ripsaw, boat: RT gas, LT brake / reverse.   Helicopter + drone: RT up, LT down. Bail out in the air = parachute, it flies home.\n" +
            "Tank: L-stick drive, R-stick aims the turret, RT fires shells, RB / Y missiles.   Beached boat: RB / Y pushes off.\n\n" +
            "<b>Keyboard + mouse (P1)</b>  WASD move  |  mouse camera (click to lock)  |  Space hop  |  E get in / out  |  0 camera reset\n" +
            "Fly: Space up, Shift down.  Tank: click shell, right-click missile.  Wheel / Q / X zoom.  V view.  H help.  M sound.\n\n" +
            "<b>Touch (P1)</b>  left stick  |  drag the free area for camera (double-tap = reset)  |  A  |  FIRE  |  MSL  |  UP / DOWN  |  - / +  |  SND\n\n" +
            "Rally: figure-8 with a bridge, jumps, and a loop lane west of the garage (keep the throttle on).  Pond: boat gate course - start at gate 1.\n" +
            (Worlds.UnderwaterOn ? "Pond dock: A at the submarine dives. Underwater: L-stick drive, RT up, LT down, A swim out in scuba (A / RT up, B / LT down), A by the sub climbs back in, surface + keep rising = ranch.\n" : "") +
            (Worlds.SpaceOn ? "Starship pad: A launches (3 s countdown + liftoff; A / FIRE skips). Space: D-pad < > target, X auto-transfer, LB / RB warp, RT boost, LT brake, Y land (Earth, Mars, Callisto).  Keys: T G Z C F.\n" : "") +
            (Worlds.StageEOn ? "Mechs: every froggy pilots its own 10 + 100-story mech (mech yard west); James & Bubbles also have a 1000-story (south edge); James alone has the trillion-story (north edge); RT / X omnigun.  Robot phone: LB / P / PHONE.\n" : "") +
            "House: walk into the front door. Inside, A at a fish tank feeds it, the toy box starts fetch with Germy + Daisy, the cat bed starts hide-and-seek, A near Dad to chat.\n" +
            "Back / V switches Shared and Split view.  Start / H closes this.  B / Esc here leaves your seat."; } }

    void RefreshLobby()
    {
        for (int i = 0; i < 4; i++)
        {
            Slot s = SlotForFrog(i);
            Color fc = Froggies.Color(i);
            if (s != null)
            {
                int pn = slots.IndexOf(s) + 1;
                string dev = s.kind == InputKind.Gamepad ? "Gamepad" : s.kind == InputKind.Keyboard ? "Keyboard" : "Touch";
                cards[i].color = new Color(fc.r, fc.g, fc.b, 0.6f);
                cardTexts[i].text = "<size=30>" + Froggies.Names[i] + "</size>\nP" + pn + "  " + dev + "\nREADY";
            }
            else
            {
                cards[i].color = new Color(1, 1, 1, 0.12f);
                cardTexts[i].text = "<size=30>" + Froggies.Names[i] + "</size>\nopen seat\n(AI frog)";
            }
        }
        bool touchOnly = TouchOnly;
        soundText.text = "SOUND: " + Sfx.LevelName + " <size=15>(M / tap)</size>";
        viewBar.gameObject.SetActive(!touchOnly);
        if (touchOnly)
        {
            lobbyHelp.text = "Phone = 1 player: tap the frog you want, then PLAY. The other frogs run on AI.\n" +
                "More players: connect gamepads (each presses A) for split-screen here, or use online host / join in Four Froggies 3D.";
            Slot ts = FindSlot(InputKind.Touch);
            for (int i = 0; i < 4; i++)
                if (ts == null || ts.frog != i)
                {
                    cards[i].color = new Color(1, 1, 1, 0.12f);
                    cardTexts[i].text = "<size=30>" + Froggies.Names[i] + "</size>\ntap to play\n(AI frog)";
                }
            lobbyStatus.text = ts == null ? "Tap a frog to pick it" : "You are " + Froggies.Names[ts.frog] + " - tap PLAY";
            return;
        }
        lobbyHelp.text = "Each gamepad: press A to claim a frog (D-pad < > to switch, B to leave). Start / A again = play.\nKeyboard: Enter to join / play, Left-Right to switch.  Touch: tap a frog, then PLAY.  Back / V = Shared / Split view.";
        viewText.text = shared ? "VIEW:   Split   <color=#ffd84a>[ SHARED ]</color>   <size=16>(Back / V / tap)</size>"
                               : "VIEW:   <color=#ffd84a>[ SPLIT ]</color>   Shared   <size=16>(Back / V / tap)</size>";
        if (slots.Count == 0) lobbyStatus.text = "Press A on a gamepad, Enter on the keyboard, or tap a frog to join";
        else lobbyStatus.text = slots.Count + " player" + (slots.Count > 1 ? "s" : "") + " ready - Start / A again / Enter / PLAY to begin" + (autoStartT > 0f ? "  (auto in " + Mathf.CeilToInt(autoStartT) + ")" : "");
    }

    // a phone / tablet with no gamepads: local multiplayer makes no sense, so the lobby is a 1-player picker
    public static bool TouchOnly { get { return Application.isMobilePlatform && Gamepad.all.Count == 0; } }

    void LayoutLobby()
    {
        bool portrait = Screen.height > Screen.width;
        int want = portrait ? 1 : 0;
        if (want == lobbyLayout) return;
        lobbyLayout = want;
        var sc = lobbyCanvas.GetComponent<CanvasScaler>();
        sc.referenceResolution = portrait ? new Vector2(720, 1280) : new Vector2(1280, 720);
        sc.matchWidthOrHeight = portrait ? 0f : 0.6f;
        System.Action<Graphic, Vector2, Vector2> put = (g, p, size) => { g.rectTransform.anchoredPosition = p; g.rectTransform.sizeDelta = size; };
        if (portrait)
        {
            put(lobbyBg, Vector2.zero, new Vector2(700, 1240));
            put(lobbyTitle, new Vector2(0, 530), new Vector2(680, 80)); lobbyTitle.fontSize = 54;
            put(lobbySub, new Vector2(0, 462), new Vector2(660, 70)); lobbySub.fontSize = 22;
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = new Vector2(i % 2 == 0 ? -170 : 170, i < 2 ? 280 : 50);
                put(cards[i], p, new Vector2(310, 205));
                put(swatches[i], p + new Vector2(0, 55), new Vector2(70, 70));
                put(cardTexts[i], p + new Vector2(0, -38), new Vector2(300, 110));
            }
            put(viewBar, new Vector2(0, -110), new Vector2(620, 50));
            put(playBtn, new Vector2(0, -200), new Vector2(360, 86));
            put(lobbyStatus, new Vector2(0, -280), new Vector2(680, 60));
            put(soundBtn, new Vector2(0, -350), new Vector2(260, 50));
            put(lobbyHelp, new Vector2(0, -460), new Vector2(680, 160)); lobbyHelp.fontSize = 20;
        }
        else
        {
            put(lobbyBg, Vector2.zero, new Vector2(1200, 650));
            put(lobbyTitle, new Vector2(0, 262), new Vector2(1100, 90)); lobbyTitle.fontSize = 70;
            put(lobbySub, new Vector2(0, 200), new Vector2(1100, 40)); lobbySub.fontSize = 24;
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = new Vector2(-420 + i * 280, 60);
                put(cards[i], p, new Vector2(255, 200));
                put(swatches[i], p + new Vector2(0, 52), new Vector2(70, 70));
                put(cardTexts[i], p + new Vector2(0, -38), new Vector2(240, 110));
            }
            put(viewBar, new Vector2(0, -88), new Vector2(620, 44));
            put(playBtn, new Vector2(0, -148), new Vector2(300, 56));
            put(lobbyStatus, new Vector2(0, -204), new Vector2(1150, 40));
            put(soundBtn, new Vector2(430, -148), new Vector2(200, 44));
            put(lobbyHelp, new Vector2(0, -262), new Vector2(1180, 60)); lobbyHelp.fontSize = 19;
        }
    }

    void LayoutHud()
    {
        bool portrait = Screen.height > Screen.width;
        bool touchUi = touch.active;
        int want = (portrait ? 1 : 0) + (touchUi ? 2 : 0);
        if (want != hudLayout)
        {
            hudLayout = want;
            var sc = hudCanvas.GetComponent<CanvasScaler>();
            sc.referenceResolution = portrait ? new Vector2(720, 1280) : new Vector2(1280, 720);
            sc.matchWidthOrHeight = portrait ? 0f : 0.6f;
            float w = portrait ? 720f : 1280f;
            helpBg.rectTransform.sizeDelta = portrait ? new Vector2(700, 1100) : new Vector2(1200, 590);
            helpText.rectTransform.sizeDelta = portrait ? new Vector2(680, 1080) : new Vector2(1150, 560);
            helpText.fontSize = portrait ? 19 : 21;
        }
        float inset = touchUi ? (portrait ? 290f : 120f) : 0f;
        float width = portrait ? 720f : 1280f;
        foreach (var s in slots) if (s.hud != null) s.hud.SetBottomInset(s.kind == InputKind.Touch ? inset : 0f, width);
        sharedHud.SetBottomInset(inset, width);
    }

    // ---------------- helpers ----------------
    Slot SlotForFrog(int f) { foreach (var s in slots) if (s.frog == f) return s; return null; }
    Slot FindSlot(InputKind k) { foreach (var s in slots) if (s.kind == k) return s; return null; }
    Slot FindPad(Gamepad p) { foreach (var s in slots) if (s.kind == InputKind.Gamepad && s.pad == p) return s; return null; }

    int FreeFrog(int from, int dir)
    {
        for (int k = 0; k < 4; k++)
        {
            int f = ((from + dir * k) % 4 + 4) % 4;
            if (SlotForFrog(f) == null) return f;
        }
        return -1;
    }

    Slot Join(InputKind kind, Gamepad pad, int frog = -1)
    {
        if (slots.Count >= 4) return null;
        if (frog < 0 || SlotForFrog(frog) != null) frog = FreeFrog(0, 1);
        if (frog < 0) return null;
        var s = new Slot { kind = kind, pad = pad, frog = frog, joinTime = Time.unscaledTime };
        slots.Add(s);
        frogs[frog].human = true;
        if (state == State.Play) { MakeView(s); ApplyLayout(); }
        else autoStartT = 30f;
        Debug.Log("Join P" + slots.Count + " " + kind + (pad != null ? " " + pad.displayName + " #" + pad.deviceId : "") + " -> " + Froggies.Names[frog]);
        return s;
    }

    void Leave(Slot s)
    {
        if (s == null) return;
        Frog f = frogs[s.frog];
        if (f.vehicle != null) f.ExitVehicle();
        f.human = false;
        if (s.cam != null) Destroy(s.cam.gameObject);
        if (s.hud != null) Destroy(s.hud.panel.gameObject);
        slots.Remove(s);
        if (slots.Count == 0) autoStartT = -1f;
        if (state == State.Play) ApplyLayout();
    }

    void Cycle(Slot s, int dir)
    {
        if (s == null) return;
        int f = FreeFrog(s.frog + dir, dir);
        if (f < 0) return;
        frogs[s.frog].human = false;
        s.frog = f;
        frogs[f].human = true;
    }

    void ToggleView()
    {
        if (Time.unscaledTime - lastViewToggle < 0.3f) return;   // duplicate pads report the same press
        lastViewToggle = Time.unscaledTime;
        shared = !shared;
        if (state == State.Play) ApplyLayout();
    }

    // A pad press is a ghost if another, already-joined pad reported an identical press within 150 ms.
    bool IsGhostPress(Gamepad pad)
    {
        float now = Time.unscaledTime;
        string sig = Pads.Signature(pad);
        bool ghost = false;
        foreach (Gamepad other in Gamepad.all)
        {
            if (other == pad) continue;
            float t;
            string os;
            if (pressTimes.TryGetValue(other.deviceId, out t) && now - t < 0.15f && FindPad(other) != null
                && pressSig.TryGetValue(other.deviceId, out os) && os == sig) ghost = true;
        }
        pressTimes[pad.deviceId] = now;
        pressSig[pad.deviceId] = sig;
        if (ghost) ghosts.Add(pad.deviceId); else ghosts.Remove(pad.deviceId);
        return ghost;
    }

    public static void Shake(Vector3 p, float power)
    {
        if (I == null) return;
        foreach (var s in I.slots)
        {
            if (s.rig == null) continue;
            float d = (I.frogs[s.frog].FocusPoint - p).magnitude;
            s.rig.AddShake(power * Mathf.Clamp01(1f - d / 45f) * 0.9f);
        }
        float sd = (I.sharedFocus - p).magnitude;
        I.sharedTrauma = Mathf.Min(1f, I.sharedTrauma + power * Mathf.Clamp01(1f - sd / 70f) * 0.6f);
    }

    // ---------------- main loop ----------------
    void Update()
    {
        float dt = Time.deltaTime;
        touch.active = state == State.Play && FindSlot(InputKind.Touch) != null && !help;
        if (state == State.Lobby) UpdateLobby(dt);
        else UpdatePlay(dt);
    }

    void UpdateLobby(float dt)
    {
        lobbyCanvas.enabled = true;
        hudCanvas.enabled = false;
        LayoutLobby();
        if (!lobbyLook) { lobbyLook = true; Look.ApplyViews(1, new List<Camera> { overview }); }
        if (Kb.CDown()) CreditsToggle();
        if (creditsPanel != null && creditsPanel.gameObject.activeSelf && (Kb.EscDown() || Kb.TouchesBegan().Count > 0 || Kb.MouseLeftDown())) { CreditsToggle(false); return; }
        if (DemoLobby()) return;
        Sfx.Music("lobby");
        overview.enabled = true;
        overview.rect = new Rect(0, 0, 1, 1);
        float now = Time.unscaledTime;

        // keyboard
        Slot ks = FindSlot(InputKind.Keyboard);
        if (Kb.EnterDown())
        {
            if (ks == null) Join(InputKind.Keyboard, null);
            else if (now - ks.joinTime > 0.3f) { StartPlay(); return; }
        }
        if (Kb.EscDown()) Leave(ks);
        if (Kb.VDown()) ToggleView();
        ks = FindSlot(InputKind.Keyboard);
        if (ks != null)
        {
            Keyboard k = Keyboard.current;
            if (k != null && (k.leftArrowKey.wasPressedThisFrame)) Cycle(ks, -1);
            if (k != null && (k.rightArrowKey.wasPressedThisFrame)) Cycle(ks, 1);
        }

        // gamepads
        foreach (Gamepad pad in Gamepad.all)
        {
            Slot ps = FindPad(pad);
            bool g = ghosts.Contains(pad.deviceId);
            if (ps != null && !g)
            {
                if (pad.buttonEast.wasPressedThisFrame) { Leave(ps); continue; }
                if (pad.dpad.left.wasPressedThisFrame) Cycle(ps, -1);
                if (pad.dpad.right.wasPressedThisFrame) Cycle(ps, 1);
            }
            if (pad.selectButton.wasPressedThisFrame && !g) ToggleView();
            if (!pad.buttonSouth.wasPressedThisFrame && !pad.startButton.wasPressedThisFrame) continue;
            if (IsGhostPress(pad) && ps == null) continue;
            if (ps == null) Join(InputKind.Gamepad, pad);
            else if (now - ps.joinTime > 0.4f) { StartPlay(); return; }
        }

        // touch
        if (Application.isMobilePlatform && !mobileAutoJoined && FindSlot(InputKind.Touch) == null && slots.Count == 0)
        {
            mobileAutoJoined = true;
            Join(InputKind.Touch, null);
        }
        foreach (Vector2 pos in Kb.TouchesBegan())
        {
            lastTouchTime = now;
            if (Hit(creditsBtn, pos)) { CreditsToggle(true); continue; }
            if (Hit(soundBtn, pos)) { Sfx.CycleVolume(); continue; }
            if (Hit(viewBar, pos)) { ToggleView(); Sfx.Play(Sfx.Click, 0.6f); continue; }
            if (Hit(playBtn, pos))
            {
                if (FindSlot(InputKind.Touch) == null) Join(InputKind.Touch, null);
                StartPlay(); return;
            }
            for (int i = 0; i < 4; i++)
                if (Hit(cards[i], pos))
                {
                    Slot ts = FindSlot(InputKind.Touch);
                    if (ts == null) Join(InputKind.Touch, null, i);
                    else if (SlotForFrog(i) == null) { frogs[ts.frog].human = false; ts.frog = i; frogs[i].human = true; }
                    Sfx.Play(Sfx.Click, 0.6f);
                }
        }
        // mouse clicks on the lobby (desktop without a touch screen)
        if (Kb.MouseLeftDown() && Kb.TouchCount() == 0 && now - lastTouchTime > 1f)
        {
            Vector2 mp = Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition;
            if (Hit(creditsBtn, mp)) CreditsToggle(true);
            else if (Hit(soundBtn, mp)) Sfx.CycleVolume();
            else if (Hit(viewBar, mp)) ToggleView();
            else if (Hit(playBtn, mp)) { if (FindSlot(InputKind.Keyboard) == null) Join(InputKind.Keyboard, null); StartPlay(); return; }
            else for (int i = 0; i < 4; i++)
                    if (Hit(cards[i], mp))
                    {
                        Slot k2 = FindSlot(InputKind.Keyboard);
                        if (k2 == null) Join(InputKind.Keyboard, null, i);
                        else if (SlotForFrog(i) == null) { frogs[k2.frog].human = false; k2.frog = i; frogs[i].human = true; }
                    }
        }

        if (slots.Count > 0 && autoStartT > 0f)
        {
            autoStartT -= dt;
            if (autoStartT <= 0f) { StartPlay(); return; }
        }
        orbit += dt * 5f;
        float a = orbit * Mathf.Deg2Rad;
        Vector3 c = new Vector3(-10f, 0f, 10f);
        overview.transform.position = c + new Vector3(Mathf.Sin(a) * 85f, 38f, Mathf.Cos(a) * 85f);
        overview.transform.LookAt(c + Vector3.up * 3f);
        RefreshLobby();
    }

    static bool Hit(Image img, Vector2 screenPos)
    {
        return img != null && img.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(img.rectTransform, screenPos, null);
    }

    void StartPlay()
    {
        if (slots.Count == 0) return;
        state = State.Play;
        lobbyCanvas.enabled = false;
        hudCanvas.enabled = true;
        autoStartT = -1f;
        if (slots.Count < 2) shared = false;
        Sfx.Play(Sfx.Click, 0.8f);
        Sfx.Music("ranch");
        for (int i = 0; i < frogs.Count; i++)
        {
            if (frogs[i].vehicle != null) frogs[i].ExitVehicle();
            frogs[i].SendTo(WorldId.Ranch, Ranch.FrogSpawn(i), 0f);
        }
        foreach (var s in slots) MakeView(s);
        sharedInit = false;
        ApplyLayout();
        if (FindSlot(InputKind.Keyboard) != null) Cursor.lockState = CursorLockMode.Locked;
    }

    void MakeView(Slot s)
    {
        if (s.cam == null)
        {
            s.cam = MakeCam("Cam P" + (slots.IndexOf(s) + 1), 2 + slots.IndexOf(s));
            s.rig = new CamRig(s.cam);
            s.rig.SetYaw(0f);   // behind the frog, looking out towards the track
        }
        if (s.hud == null) s.hud = new ViewHud(hudCanvas.transform, "P" + (slots.IndexOf(s) + 1));
        s.rig.Snap();
    }

    void ApplyLayout()
    {
        int n = slots.Count;
        bool split = (!shared || MixedWorlds) && n > 1;
        bool portrait = Screen.height > Screen.width;
        for (int i = 0; i < n; i++)
        {
            Slot s = slots[i];
            if (s.cam == null) MakeView(s);
            Rect r;
            if (n == 1) r = new Rect(0, 0, 1, 1);
            else if (n == 2) r = portrait ? (i == 0 ? new Rect(0, 0.5f, 1, 0.5f) : new Rect(0, 0, 1, 0.5f)) : (i == 0 ? new Rect(0, 0, 0.5f, 1) : new Rect(0.5f, 0, 0.5f, 1));
            else r = new Rect((i % 2) * 0.5f, i < 2 ? 0.5f : 0f, 0.5f, 0.5f);
            s.cam.rect = r;
            s.cam.depth = 2 + i;
            s.cam.enabled = split || n == 1;
            s.cam.fieldOfView = n == 2 && !portrait ? 70f : 60f;
            s.hud.SetRect(r);
            s.hud.SetActive(split || n == 1);
        }
        sharedCam.enabled = !(split || n == 1);
        if (n > 0) Worlds.SetCamera(sharedCam, frogs[slots[0].frog].world);
        sharedHud.SetActive(sharedCam.enabled);
        overview.enabled = split && n == 3;
        overview.rect = new Rect(0.5f, 0f, 0.5f, 0.5f);
        joinText.enabled = split && n == 3;
        sepV.enabled = split && (n >= 3 || (n == 2 && !portrait));
        sepH.enabled = split && (n >= 3 || (n == 2 && portrait));

        // performance: fewer shadows the more views we draw, post-fx only in one full-screen view (Look tiers)
        int views = split ? n + (n == 3 ? 1 : 0) : 1;
        var cams = new List<Camera> { sharedCam, overview };
        foreach (var sl in slots) if (sl.cam != null) cams.Add(sl.cam);
        Look.ApplyViews(views, cams);
        Debug.Log("Layout: " + n + " players, " + (split ? "split" : n == 1 ? "single" : "shared"));
    }

    void UpdatePlay(float dt)
    {
        float now = Time.unscaledTime;
        bool viewPressed = false, helpPressed = false;

        // mid-game joins: an unbound pad presses A / Start, Enter on the keyboard, a tap on a touch screen
        foreach (Gamepad pad in Gamepad.all)
        {
            if (FindPad(pad) != null) continue;
            if (!pad.buttonSouth.wasPressedThisFrame && !pad.startButton.wasPressedThisFrame) continue;
            if (IsGhostPress(pad)) continue;
            Join(InputKind.Gamepad, pad);
        }
        if (FindSlot(InputKind.Keyboard) == null && Kb.EnterDown()) Join(InputKind.Keyboard, null);
        if (FindSlot(InputKind.Touch) == null && Kb.TouchesBegan().Count > 0 && Application.isMobilePlatform) Join(InputKind.Touch, null);

        for (int k = slots.Count - 1; k >= 0; k--)
        {
            Slot s = slots[k];
            PIn i;
            switch (s.kind)
            {
                case InputKind.Gamepad:
                    if (s.pad == null || !s.pad.added) { i = new PIn(); break; }
                    if (s.pad.buttonSouth.wasPressedThisFrame || s.pad.startButton.wasPressedThisFrame) IsGhostPress(s.pad);
                    i = Pads.Read(s.pad, dt);
                    if (help && s.pad.buttonEast.wasPressedThisFrame) { help = false; Leave(s); continue; }
                    break;
                case InputKind.Keyboard:
                    if (Kb.MouseLeftDown() && Cursor.lockState != CursorLockMode.Locked && !help) Cursor.lockState = CursorLockMode.Locked;
                    i = Pads.ReadKeyboard(dt);
                    if (help && Kb.EscDown()) { help = false; Leave(s); continue; }
                    break;
                default:
                    {
                        Frog tf = frogs[s.frog];
                        TouchControls.spaceMode = tf.world == WorldId.Space && tf.vehicle is Starship;
                    }
                    i = touch.Read();
                    break;
            }
            {
                Frog pf = frogs[s.frog];
                if (i.phone && RobotPhone.I != null && pf.world == WorldId.Ranch && pf.vehicle == null) RobotPhone.I.Toggle(pf);
                if (RobotPhone.I != null && RobotPhone.I.Handle(pf, i)) { Vector2 lk = i.look; bool v = i.view, h = i.help; i = new PIn(); i.look = lk; i.view = v; i.help = h; }
            }
            if (i.view) viewPressed = true;
            if (i.help) helpPressed = true;
            if (help) i = new PIn();
            s.last = i;
            Frog f = frogs[s.frog];
            float camYaw = sharedCam.enabled ? sharedYaw : (s.rig != null ? s.rig.yaw : 0f);
            f.SetInput(i, camYaw);
        }
        if (viewPressed) ToggleView();
        if (helpPressed) { help = !help; if (help) Cursor.lockState = CursorLockMode.None; Sfx.Play(Sfx.Click, 0.7f); }
        if (help)
        {
            // sound level from the help / pause menu: Y on any joined pad (M works everywhere)
            foreach (var s in slots) if (s.kind == InputKind.Gamepad && s.pad != null && s.pad.added && s.pad.buttonNorth.wasPressedThisFrame) { Sfx.CycleVolume(); break; }
            helpText.text = HelpBody + "\n<color=#8cff70>Sound: " + Sfx.LevelName + "</color>  (Y here / M key / SND button cycles ON - LOW - OFF)";
        }
        LayoutHud();
        helpText.enabled = help;
        helpBg.enabled = help;
        if (state == State.Play && slots.Count == 0) EnterLobby();
    }

    void EnterLobby()
    {
        state = State.Lobby;
        foreach (var f in frogs) f.human = false;
        sharedCam.enabled = false;
        sharedHud.SetActive(false);
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowDistance = 45f;
        Cursor.lockState = CursorLockMode.None;
    }

    // humans in different worlds can't share one camera: Shared view splits until they are together again
    bool MixedWorlds
    {
        get
        {
            for (int i = 1; i < slots.Count; i++) if (frogs[slots[i].frog].world != frogs[slots[0].frog].world) return true;
            return false;
        }
    }

    void TrackWorlds()
    {
        bool relayout = false;
        foreach (var s in slots)
        {
            Frog f = frogs[s.frog];
            if (f.world == s.world) continue;
            s.world = f.world;
            relayout = true;
            if (s.cam != null) Worlds.SetCamera(s.cam, f.world);
            if (s.rig != null)
            {
                s.rig.Snap();
                s.rig.SetYaw(f.transform.eulerAngles.y);
                bool house = f.world == WorldId.House, under = f.world == WorldId.Underwater, space = f.world == WorldId.Space;
                s.rig.minPitch = house ? 26f : under ? -40f : space ? -70f : -5f;
                s.rig.maxPitch = house ? 80f : space ? 85f : 70f;
                s.rig.pitch = house ? 38f : under ? 10f : space ? 25f : 16f;
                s.rig.ResetView(f.transform.eulerAngles.y);
            }
        }
        if (relayout) ApplyLayout();
        if (slots.Count > 0) Sfx.Music(Worlds.Mood(frogs[slots[0].frog].world));
    }

    void LateUpdate()
    {
        if (state != State.Play) return;
        float dt = Time.deltaTime;
        TrackWorlds();
        foreach (var s in slots)
            if (s.rig != null && s.cam.enabled) s.rig.Update(frogs[s.frog], s.last, dt);
        if (sharedCam.enabled) UpdateShared(dt);
        if (overview.enabled)
        {
            orbit += dt * 5f;
            float a = orbit * Mathf.Deg2Rad;
            Vector3 c = new Vector3(-10f, 0f, 10f);
            overview.transform.position = c + new Vector3(Mathf.Sin(a) * 90f, 55f, Mathf.Cos(a) * 90f);
            overview.transform.LookAt(c);
        }
        for (int k = 0; k < slots.Count; k++)
        {
            Slot s = slots[k];
            if (s.hud == null) continue;
            if (s.cam.enabled) s.hud.Tick(s.cam, frogs[s.frog], "P" + (k + 1), frogs, null);
            string c = "";
            if (s.kind == InputKind.Keyboard && Cursor.lockState != CursorLockMode.Locked && !help) c = "<size=22>Click to use the mouse for the camera</size>";
            s.hud.SetCenter(c);
        }
        if (sharedCam.enabled)
        {
            var lines = new List<string>();
            for (int k = 0; k < slots.Count; k++)
            {
                Frog f = frogs[slots[k].frog];
                lines.Add("<color=#" + ColorUtility.ToHtmlStringRGB(f.color) + ">P" + (k + 1) + " " + f.nick + "</color>  " + (f.vehicle != null ? f.vehicle.Title : "") + (f.prompt.Length > 0 && f.vehicle == null ? "  " + f.prompt : ""));
            }
            sharedHud.Tick(sharedCam, null, "", frogs, string.Join("\n", lines.ToArray()));
        }
        // Starship blast-off: chase camera, countdown, fade (overrides the views of froggies aboard)
        foreach (var s in slots) if (s.cam != null && s.cam.enabled) LaunchSeq.View(s.cam, s.hud, frogs[s.frog]);
        if (demoT > 0f) DemoView();
        if (sharedCam.enabled)
        {
            Frog any = null;
            foreach (var s in slots) if (LaunchSeq.I != null && LaunchSeq.I.crew.Contains(frogs[s.frog])) { any = frogs[s.frog]; break; }
            LaunchSeq.View(sharedCam, sharedHud, any);
        }
    }

    void UpdateShared(float dt)
    {
        Vector3 sum = Vector3.zero;
        int n = 0;
        float look = 0f, zoom = 0f, pitchIn = 0f;
        foreach (var s in slots)
        {
            sum += frogs[s.frog].FocusPoint; n++;
            look += s.last.look.x; zoom += s.last.zoom; pitchIn += s.last.look.y;
        }
        if (n == 0) return;
        Vector3 c = sum / n;
        float spread = 0f;
        foreach (var s in slots) spread = Mathf.Max(spread, (frogs[s.frog].FocusPoint - c).magnitude);
        sharedYaw += look;
        sharedPitch = Mathf.Clamp(sharedPitch - pitchIn * 0.5f, 20f, 80f);
        sharedZoom = Mathf.Clamp(sharedZoom * (1f + zoom * 1.2f * dt), 0.5f, 2.5f);
        float dist = Mathf.Clamp(spread * 1.5f + 14f, 14f, 120f) * sharedZoom;
        if (!sharedInit) { sharedFocus = c; sharedInit = true; }
        sharedFocus = Vector3.Lerp(sharedFocus, c, Mathf.Min(1f, dt * 4f));
        Quaternion rot = Quaternion.Euler(sharedPitch, sharedYaw, 0f);
        sharedTrauma = Mathf.MoveTowards(sharedTrauma, 0f, dt * 1.5f);
        Vector3 shake = Random.insideUnitSphere * sharedTrauma * sharedTrauma * 0.8f;
        sharedCam.transform.position = sharedFocus + rot * new Vector3(0f, 0f, -dist) + shake;
        sharedCam.transform.rotation = rot;
        sharedCam.fieldOfView = 55f;
    }
}
