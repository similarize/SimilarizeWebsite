using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Slot
{
    public InputKind kind;
    public Gamepad pad;
    public float joinTime;
    public int car, robot;
    public Camera cam;
    public ChaseCam rig;
    public ViewHud hud;
    public Kart kart;
    public bool stickHeldX, stickHeldY;
}

// Lobby (pick a colour car + robot driver), 1-4 local split-screen, 3-2-1-GO, race, results, pause.
[DefaultExecutionOrder(-50)]
public class Game : MonoBehaviour
{
    public static Game I;
    public enum State { Lobby, Countdown, Race, Results }
    public State state = State.Lobby;

    public readonly List<Kart> karts = new List<Kart>();
    public readonly List<Slot> slots = new List<Slot>();
    readonly List<Kart> order = new List<Kart>();
    public const int Racers = 8;

    float countdownT, raceTime, resultsT, allHumansDoneT = -1f, lastBeep;
    int finishCount;
    bool paused;
    public float RaceTime { get { return raceTime; } }

    readonly HashSet<int> ghosts = new HashSet<int>();
    readonly Dictionary<int, float> pressTimes = new Dictionary<int, float>();
    readonly Dictionary<int, string> pressSig = new Dictionary<int, string>();

    Camera overview;
    float orbit;
    Texture2D mapTex;
    TouchControls touch;

    // UI
    Canvas lobbyCanvas, hudCanvas, resultsCanvas, pauseCanvas, topCanvas;
    Image lobbyBg, playBtn, sepV, sepH;
    Text lobbyTitle, lobbySub, lobbyStatus, lobbyHelp, resultsText, pauseText;
    readonly Image[] cards = new Image[4];
    readonly Text[] cardTexts = new Text[4];
    readonly Image[][] bars = new Image[4][];
    readonly Image[] cardSwatch = new Image[4];
    readonly Text[] cardHead = new Text[4];
    readonly RawImage[] cardPreview = new RawImage[4];
    readonly Image[] cardPreviewFrame = new Image[4];
    readonly Text[][] barLabs = new Text[4][];
    readonly Image[][] barBgs = new Image[4][];
    // thumbnail tiles: 8 car colours + 7 robots (tap / click to pick, highlighted + P1-P4 badges)
    readonly Image[] carTiles = new Image[8], robotTiles = new Image[7];
    readonly RawImage[] carThumbs = new RawImage[8], robotThumbs = new RawImage[7];
    readonly Image[] carStrips = new Image[8];
    readonly Text[] carNames = new Text[8], robotNames = new Text[7], carBadges = new Text[8], robotBadges = new Text[7];
    readonly float[] carPop = new float[8], robotPop = new float[7];
    readonly Showroom.Stand[] stands = new Showroom.Stand[4];
    Vector2 cardSize, tileCarSize, tileRobotSize;
    float previewH, attractT, thumbCheckT;
    Vector2Int lastScreen;
    static readonly Color[] PlayerCol = { new Color(1f, 0.85f, 0.25f), new Color(0.3f, 0.9f, 1f), new Color(1f, 0.45f, 0.85f), new Color(0.55f, 1f, 0.35f) };
    Image pauseBtn, resumeBtn, restartBtn, quitBtn, creditsBtn, creditsPanel;
    Image[] resultRows;
    int lobbyLayout = -1;
    float lastTouchTime = -10f;
    static Game inst;

    void Awake()
    {
        I = this; inst = this;
        overview = MakeCam("OverviewCam", -10);
        touch = gameObject.AddComponent<TouchControls>();
        mapTex = Track.MiniMapTex(256);
        for (int i = 0; i < Racers; i++) karts.Add(Kart.Create(i, i, i % Robots.Count));
        Assign();
        PlaceGrid();
        for (int i = 0; i < 4; i++) stands[i] = new Showroom.Stand(i);
        StartCoroutine(Showroom.RenderThumbsLater());
        BuildLobbyUI();
        BuildHudUI();
        BuildResultsUI();
        BuildPauseUI();
        Sfx.Music("lobby");
    }

    static void SetPost(Camera c, bool on)
    {
        if (c == null) return;
        var p = c.GetComponent<LBPost>();
        if (p != null && p.enabled != on) p.enabled = on;
    }

    static Camera MakeCam(string name, int depth)
    {
        var g = new GameObject(name);
        Camera c = g.AddComponent<Camera>();
        c.depth = depth;
        c.nearClipPlane = 0.3f;
        c.farClipPlane = 1400f;
        c.fieldOfView = 60f;
        c.clearFlags = CameraClearFlags.Skybox;
        c.cullingMask = ~Showroom.Mask;
        c.allowHDR = false;
        LBPost.Add(c);
        return c;
    }

    // ---------------- assignment ----------------
    // humans drive karts[0..h-1] with their picks; the AI gets the other colours and robots
    // (all 7 robots are used before one drives a second time).
    void Assign()
    {
        var usedCar = new bool[Cars.All.Length];
        var robotUses = new int[Robots.Count];
        for (int i = 0; i < slots.Count; i++)
        {
            Slot s = slots[i];
            usedCar[s.car] = true;
            robotUses[s.robot]++;
            s.kart = karts[i];
            karts[i].slot = i;
            karts[i].SetHuman(true);
            karts[i].SetLook(s.car, s.robot);
        }
        int nextCar = 0, rOff = 0;
        for (int i = slots.Count; i < karts.Count; i++)
        {
            while (nextCar < usedCar.Length && usedCar[nextCar]) nextCar++;
            int c = nextCar < usedCar.Length ? nextCar : i % Cars.All.Length;
            if (nextCar < usedCar.Length) usedCar[nextCar] = true;
            // pick the least-used robot, scanning from a rotating offset so the AI line-up varies
            int pick = -1, min = 99;
            for (int k = 0; k < Robots.Count; k++) { int r = (k + rOff) % Robots.Count; if (robotUses[r] < min) { min = robotUses[r]; pick = r; } }
            robotUses[pick]++;
            rOff = pick + 1;
            karts[i].slot = -1;
            karts[i].SetHuman(false);
            karts[i].SetLook(c, pick);
        }
    }

    // AI in front, humans at the back of the grid (like Beach Buggy Blast)
    void PlaceGrid()
    {
        int g = 0;
        for (int i = slots.Count; i < karts.Count; i++) { Vector3 p; float y; Track.GridSpot(g++, out p, out y); karts[i].Place(p, y); }
        for (int i = 0; i < slots.Count; i++) { Vector3 p; float y; Track.GridSpot(g++, out p, out y); karts[i].Place(p, y); }
        Shot.ClearAll();
        ItemBox.ResetAll();
    }

    // ---------------- queries used by karts / items / audio ----------------
    public float NearestHumanDistance(Vector3 p)
    {
        float d = 1e9f;
        foreach (var s in slots) if (s.kart != null) d = Mathf.Min(d, (s.kart.transform.position - p).magnitude);
        if (d > 1e8f)
        {
            if (state == State.Lobby && overview != null) return (overview.transform.position - p).magnitude;
            return 30f;
        }
        return d;
    }

    public float HumanVolumeScale { get { return 1f / Mathf.Sqrt(Mathf.Max(1, slots.Count)); } }

    public Kart KartAtPlace(int p) { foreach (var k in karts) if (k.place == p) return k; return null; }
    public Kart KartAhead(Kart k) { return k.place > 1 ? KartAtPlace(k.place - 1) : null; }

    public string FireHint(Kart k)
    {
        if (k.slot < 0 || k.slot >= slots.Count) return "";
        switch (slots[k.slot].kind)
        {
            case InputKind.Gamepad: return "X / RB to use";
            case InputKind.Keyboard: return "SPACE to use";
            default: return "FIRE to use";
        }
    }

    public static void Shake(Kart k, float p)
    {
        if (I == null || k == null || k.slot < 0 || k.slot >= I.slots.Count) return;
        Slot s = I.slots[k.slot];
        if (s.rig != null) s.rig.AddShake(p);
    }

    public static bool PauseButtonHit(Vector2 screenPos)
    {
        return inst != null && inst.pauseBtn != null && inst.pauseBtn.gameObject.activeInHierarchy &&
               RectTransformUtility.RectangleContainsScreenPoint(inst.pauseBtn.rectTransform, screenPos, null);
    }

    // ---------------- UI construction ----------------
    void BuildLobbyUI()
    {
        lobbyCanvas = UIK.MakeCanvas("Lobby", null, 100, true);
        Transform r = lobbyCanvas.transform;
        lobbyBg = UIK.Img(r, null, new Color(0.02f, 0.06f, 0.1f, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240, 690));
        lobbyTitle = UIK.Label(r, "LAMBO BLAST", 50, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 60), new Color(0.35f, 1f, 0.45f));
        lobbySub = UIK.Label(r, "Robot racers on Coconut Cove  ·  8 cars  ·  3 laps  ·  grab the ? boxes and blast them", 17, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180, 30), Color.white);
        string[] lab = { "TOP SPEED", "ACCEL", "HANDLING", "WEIGHT" };
        for (int i = 0; i < 4; i++)
        {
            cards[i] = UIK.Img(r, null, new Color(1, 1, 1, 0.12f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(290, 330));
            Transform c = cards[i].transform;
            cardSwatch[i] = UIK.Img(c, null, Color.white, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(270, 8));
            cardHead[i] = UIK.Label(c, "", 16, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(280, 22), Color.white);
            cardPreviewFrame[i] = UIK.Img(c, null, new Color(0f, 0f, 0f, 0.6f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(278, 160));
            var pr = UIK.Rect(c, "Preview", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(274, 156));
            cardPreview[i] = pr.gameObject.AddComponent<RawImage>();
            cardPreview[i].texture = stands[i].rt;
            cardPreview[i].raycastTarget = false;
            cardTexts[i] = UIK.Label(c, "", 20, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(280, 60), Color.white);
            bars[i] = new Image[4]; barLabs[i] = new Text[4]; barBgs[i] = new Image[4];
            for (int b = 0; b < 4; b++)
            {
                barLabs[i][b] = UIK.Label(c, lab[b], 13, TextAnchor.MiddleLeft, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(110, 18), new Color(1, 1, 1, 0.8f));
                barBgs[i][b] = UIK.Img(c, null, new Color(0, 0, 0, 0.4f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(140, 11));
                bars[i][b] = UIK.Img(c, null, Color.white, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(140, 11));
                bars[i][b].rectTransform.pivot = new Vector2(0f, 0.5f);
                barBgs[i][b].rectTransform.pivot = new Vector2(0f, 0.5f);
            }
        }
        for (int i = 0; i < 8; i++)
        {
            carTiles[i] = UIK.Img(r, null, new Color(0f, 0f, 0f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(124, 84));
            Transform t = carTiles[i].transform;
            var th = UIK.Rect(t, "Thumb", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(118, 64));
            carThumbs[i] = th.gameObject.AddComponent<RawImage>();
            carThumbs[i].raycastTarget = false;
            carThumbs[i].color = new Color(1f, 1f, 1f, 0f);
            carStrips[i] = UIK.Img(t, null, Cars.All[i].color, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(118, 16));
            bool darkText = i == 6 || i == 2;
            carNames[i] = UIK.Label(t, Cars.All[i].name.ToUpper(), 13, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(118, 16), darkText ? new Color(0.1f, 0.1f, 0.1f) : Color.white);
            carNames[i].GetComponent<Outline>().enabled = !darkText;
            carBadges[i] = UIK.Label(t, "", 13, TextAnchor.UpperLeft, new Vector2(0f, 1f), Vector2.zero, new Vector2(118, 18), Color.white);
            carBadges[i].supportRichText = true;
        }
        for (int i = 0; i < 7; i++)
        {
            robotTiles[i] = UIK.Img(r, null, new Color(0f, 0f, 0f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(132, 100));
            Transform t = robotTiles[i].transform;
            var th = UIK.Rect(t, "Thumb", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(126, 80));
            robotThumbs[i] = th.gameObject.AddComponent<RawImage>();
            robotThumbs[i].raycastTarget = false;
            robotThumbs[i].color = new Color(1f, 1f, 1f, 0f);
            robotNames[i] = UIK.Label(t, Robots.Short[i], 13, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(126, 16), Color.white);
            robotBadges[i] = UIK.Label(t, "", 13, TextAnchor.UpperLeft, new Vector2(0f, 1f), Vector2.zero, new Vector2(126, 18), Color.white);
            robotBadges[i].supportRichText = true;
        }
        playBtn = UIK.Img(r, null, new Color(0.15f, 0.65f, 0.25f, 0.9f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260, 46));
        var pl = UIK.Label(playBtn.transform, "RACE!", 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260, 46), Color.white);
        UIK.Stretch(pl.rectTransform);
        lobbyStatus = UIK.Label(r, "", 17, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180, 26), new Color(0.7f, 1f, 0.7f));
        lobbyHelp = UIK.Label(r, "", 12, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 34), new Color(1, 1, 1, 0.85f));
        creditsBtn = UIK.Img(r, null, new Color(0f, 0f, 0f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(118, 26));
        var cl = UIK.Label(creditsBtn.transform, "CREDITS (C)", 13, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(118, 26), new Color(0.8f, 0.95f, 1f));
        UIK.Stretch(cl.rectTransform);
        creditsPanel = UIK.Img(r, null, new Color(0.01f, 0.04f, 0.08f, 0.94f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1040, 470));
        var ct = UIK.Label(creditsPanel.transform, CreditsText, 16, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980, 440), Color.white);
        ct.supportRichText = true;
        ct.horizontalOverflow = HorizontalWrapMode.Wrap;
        UIK.Stretch(ct.rectTransform);
        ct.rectTransform.offsetMin = new Vector2(30, 14); ct.rectTransform.offsetMax = new Vector2(-30, -14);
        creditsPanel.gameObject.SetActive(false);
    }

    const string CreditsText =
        "<size=26><color=#7dff8a><b>CREDITS</b></color></size>\n" +
        "<b>Unitree</b> (Unitree H1): Unitree Robotics' official robot model (github.com/unitreerobotics/unitree_mujoco).\n" +
        "   Copyright (c) 2016-2024 HangZhou YuShu TECHNOLOGY CO.,LTD. (\"Unitree Robotics\"). BSD 3-Clause licence. Logo plate removed.\n" +
        "<b>Atlas HD</b> (hydraulic DRC Atlas): the MIT Atlas model from RobotLocomotion/models (Drake).\n" +
        "   Copyright 2012-2022 Robot Locomotion Group @ CSAIL. BSD 3-Clause licence. Team / sponsor logos removed.\n" +
        "<b>Optimus, Figure 02, Big Figure Two, Figure 03, Atlas electric</b> and the cars: modelled for this game from public photos (no logos).\n" +
        "<b>Sky</b> (Kloofendal 48d Partly Cloudy, Greg Zaal + Jarod Guest) and <b>sand textures</b>: Poly Haven (polyhaven.com), CC0.\n\n" +
        "Full licence texts: similarize.com/games/lambo-blast/LICENSES.txt\n" +
        "Robot names describe the real machines only; this fan game is not affiliated with their makers.\n\n" +
        "<color=#9fd8ff>Press C, Esc or tap to close</color>";

    void CreditsToggle(bool? on = null)
    {
        if (creditsPanel == null) return;
        bool v = on ?? !creditsPanel.gameObject.activeSelf;
        creditsPanel.gameObject.SetActive(v);
        creditsPanel.transform.SetAsLastSibling();
    }

    void BuildHudUI()
    {
        hudCanvas = UIK.MakeCanvas("HUD", null, 50, true);
        Transform r = hudCanvas.transform;
        sepV = UIK.Img(r, null, new Color(0f, 0f, 0f, 0.85f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4f, 0f));
        sepV.rectTransform.anchorMin = new Vector2(0.5f, 0f); sepV.rectTransform.anchorMax = new Vector2(0.5f, 1f); sepV.rectTransform.sizeDelta = new Vector2(4f, 0f);
        sepH = UIK.Img(r, null, new Color(0f, 0f, 0f, 0.85f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 4f));
        sepH.rectTransform.anchorMin = new Vector2(0f, 0.5f); sepH.rectTransform.anchorMax = new Vector2(1f, 0.5f); sepH.rectTransform.sizeDelta = new Vector2(0f, 4f);
        hudCanvas.enabled = false;
        // pause button next to the SOUND button (top centre), always on top
        topCanvas = UIK.MakeCanvas("PauseButton", null, 199, true);
        pauseBtn = UIK.Img(topCanvas.transform, null, new Color(0f, 0f, 0f, 0.5f), new Vector2(0.5f, 1f), new Vector2(-40, -26), new Vector2(64, 40));
        UIK.Label(pauseBtn.transform, "II", 22, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 38), Color.white);
        pauseBtn.gameObject.SetActive(false);
    }

    void BuildResultsUI()
    {
        resultsCanvas = UIK.MakeCanvas("Results", null, 120, true);
        Transform r = resultsCanvas.transform;
        UIK.Img(r, null, new Color(0f, 0.04f, 0.08f, 0.78f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 640));
        resultRows = new Image[8];
        for (int i = 0; i < 8; i++) resultRows[i] = UIK.Img(r, null, new Color(1f, 1f, 1f, 0.06f), new Vector2(0.5f, 0.5f), new Vector2(0, 170 - i * 50), new Vector2(820, 44));
        resultsText = UIK.Label(r, "", 24, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860, 620), Color.white);
        resultsCanvas.enabled = false;
    }

    void BuildPauseUI()
    {
        pauseCanvas = UIK.MakeCanvas("Pause", null, 150, true);
        Transform r = pauseCanvas.transform;
        UIK.Img(r, null, new Color(0f, 0f, 0f, 0.7f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000, 4000));
        pauseText = UIK.Label(r, "", 22, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 110), new Vector2(1180, 420), Color.white);
        resumeBtn = Button(r, "RESUME", new Vector2(-230, -170), new Color(0.15f, 0.6f, 0.25f, 0.9f));
        restartBtn = Button(r, "RESTART", new Vector2(0, -170), new Color(0.75f, 0.5f, 0.1f, 0.9f));
        quitBtn = Button(r, "LOBBY", new Vector2(230, -170), new Color(0.6f, 0.2f, 0.2f, 0.9f));
        pauseCanvas.enabled = false;
    }

    static Image Button(Transform r, string label, Vector2 pos, Color c)
    {
        Image b = UIK.Img(r, null, c, new Vector2(0.5f, 0.5f), pos, new Vector2(200, 60));
        UIK.Label(b.transform, label, 26, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200, 58), Color.white);
        return b;
    }

    static string HelpLines { get { return
        "<b>Xbox pad</b>: RT / A gas · LT / B brake + reverse · L-stick steer · X / RB fire item · LB drift (hold, release = boost) · Start pause\n" +
        "<b>Keyboard</b>: WASD / arrows drive · Space fire · Shift drift · Esc pause · M sound      <b>Phone</b>: left stick steer · GAS · BRAKE · FIRE · DRIFT"; } }

    // ---------------- lobby ----------------
    public static bool TouchOnly { get { return Application.isMobilePlatform && Gamepad.all.Count == 0; } }

    // static layout (title, tiles, buttons, card insides); re-run when the screen size / orientation changes.
    // Reference canvas: landscape 1280x720 (fits >= 16:9 by height, narrower by width),
    // portrait 720x1280 (tall phones by width, squarer screens by height) so nothing ever falls off-screen.
    void LayoutLobby()
    {
        var now = new Vector2Int(Screen.width, Screen.height);
        if (now == lastScreen) return;
        lastScreen = now;
        bool portrait = Screen.height > Screen.width;
        lobbyLayout = portrait ? 1 : 0;
        float aspect = Screen.width / (float)Mathf.Max(1, Screen.height);
        var sc = lobbyCanvas.GetComponent<CanvasScaler>();
        sc.referenceResolution = portrait ? new Vector2(720, 1280) : new Vector2(1280, 720);
        sc.matchWidthOrHeight = portrait ? (1f / aspect >= 1.75f ? 0f : 1f) : (aspect >= 1.78f ? 1f : 0f);
        System.Action<Graphic, Vector2, Vector2> put = (g, p, size) => { g.rectTransform.anchoredPosition = p; g.rectTransform.sizeDelta = size; };
        if (portrait)
        {
            put(lobbyBg, Vector2.zero, new Vector2(712, 1268));
            put(lobbyTitle, new Vector2(0, 600), new Vector2(680, 52)); lobbyTitle.fontSize = 44;
            put(lobbySub, new Vector2(0, 558), new Vector2(690, 30)); lobbySub.fontSize = 15;
            cardSize = new Vector2(345, 0); previewH = 160f;
            tileCarSize = new Vector2(166, 80); tileRobotSize = new Vector2(166, 92);
            for (int i = 0; i < 8; i++) put(carTiles[i], new Vector2(-258 + (i % 4) * 172, -182 - (i / 4) * 86), tileCarSize);
            for (int i = 0; i < 7; i++) put(robotTiles[i], new Vector2(i < 4 ? -258 + i * 172 : -172 + (i - 4) * 172, -362 - (i / 4) * 96), tileRobotSize);
            put(playBtn, new Vector2(0, -540), new Vector2(340, 54));
            put(lobbyStatus, new Vector2(0, -585), new Vector2(690, 30)); lobbyStatus.fontSize = 15;
            put(lobbyHelp, new Vector2(0, -613), new Vector2(700, 34)); lobbyHelp.fontSize = 11;
            put(creditsBtn, new Vector2(282, -540), new Vector2(118, 30));
            put(creditsPanel, Vector2.zero, new Vector2(700, 900));
        }
        else
        {
            put(lobbyBg, Vector2.zero, new Vector2(1260, 712));
            put(lobbyTitle, new Vector2(0, 326), new Vector2(1100, 56)); lobbyTitle.fontSize = 50;
            put(lobbySub, new Vector2(0, 292), new Vector2(1180, 26)); lobbySub.fontSize = 17;
            cardSize = new Vector2(290, 0); previewH = 152f;
            tileCarSize = new Vector2(124, 84); tileRobotSize = new Vector2(132, 100);
            for (int i = 0; i < 8; i++) put(carTiles[i], new Vector2(-483 + i * 138, -106), tileCarSize);
            for (int i = 0; i < 7; i++) put(robotTiles[i], new Vector2(-474 + i * 158, -204), tileRobotSize);
            put(playBtn, new Vector2(0, -284), new Vector2(260, 44));
            put(lobbyStatus, new Vector2(0, -318), new Vector2(1180, 26)); lobbyStatus.fontSize = 17;
            put(lobbyHelp, new Vector2(0, -344), new Vector2(1200, 32)); lobbyHelp.fontSize = 12;
            put(creditsBtn, new Vector2(540, -284), new Vector2(118, 28));
            put(creditsPanel, Vector2.zero, new Vector2(1040, 470));
        }
        // card insides (anchored to the card's top edge)
        float w = cardSize.x, pw = w - 16f, y = -6f;
        for (int i = 0; i < 4; i++)
        {
            put(cardSwatch[i], new Vector2(0, y), new Vector2(w - 20, 8));
            put(cardHead[i], new Vector2(0, -21), new Vector2(w - 10, 22));
            put(cardPreviewFrame[i], new Vector2(0, -32 - previewH * 0.5f), new Vector2(pw + 4, previewH + 4));
            put(cardPreview[i], new Vector2(0, -32 - previewH * 0.5f), new Vector2(pw, previewH));
            // crop the 448x256 render to the preview's aspect (no stretching)
            float ra = 448f / 256f, pa = pw / previewH;
            cardPreview[i].uvRect = pa > ra ? new Rect(0f, (1f - ra / pa) * 0.5f, 1f, ra / pa) : new Rect((1f - pa / ra) * 0.5f, 0f, pa / ra, 1f);
            put(cardTexts[i], new Vector2(0, -38 - previewH - 28), new Vector2(w - 8, 56));
            for (int b = 0; b < 4; b++)
            {
                float by = -38 - previewH - 66 - b * 19;
                put(barLabs[i][b], new Vector2(-w * 0.5f + 70, by), new Vector2(110, 18));
                barBgs[i][b].rectTransform.anchoredPosition = new Vector2(-w * 0.5f + 122, by);
                barBgs[i][b].rectTransform.sizeDelta = new Vector2(w - 140, 11);
                bars[i][b].rectTransform.anchoredPosition = new Vector2(-w * 0.5f + 122, by);
            }
        }
        cardSize.y = 38 + previewH + 66 + 3 * 19 + 16;
        foreach (var c in cards) c.rectTransform.sizeDelta = cardSize;
        // tile insides
        for (int i = 0; i < 8; i++)
        {
            Vector2 ts = tileCarSize;
            put(carThumbs[i], new Vector2(0, -3 - (ts.y - 22) * 0.5f), new Vector2(ts.x - 6, ts.y - 22));
            CropTo(carThumbs[i], ts.x - 6, ts.y - 22, Showroom.CarThumbW, Showroom.CarThumbH);
            put(carStrips[i], new Vector2(0, 9), new Vector2(ts.x - 6, 16));
            put(carNames[i], new Vector2(0, 9), new Vector2(ts.x - 6, 16));
            put(carBadges[i], new Vector2(4 + (ts.x - 8) * 0.5f, -10), new Vector2(ts.x - 8, 18));
        }
        for (int i = 0; i < 7; i++)
        {
            Vector2 ts = tileRobotSize;
            put(robotThumbs[i], new Vector2(0, -3 - (ts.y - 22) * 0.5f), new Vector2(ts.x - 6, ts.y - 22));
            CropTo(robotThumbs[i], ts.x - 6, ts.y - 22, Showroom.RobotThumbW, Showroom.RobotThumbH);
            put(robotNames[i], new Vector2(0, 9), new Vector2(ts.x - 6, 16));
            put(robotBadges[i], new Vector2(4 + (ts.x - 8) * 0.5f, -10), new Vector2(ts.x - 8, 18));
        }
    }

    static void CropTo(RawImage img, float w, float h, int tw, int th)
    {
        float ra = tw / (float)th, pa = w / Mathf.Max(1f, h);
        img.uvRect = pa > ra ? new Rect(0f, (1f - ra / pa) * 0.5f, 1f, ra / pa) : new Rect((1f - pa / ra) * 0.5f, 0f, pa / ra, 1f);
    }

    // card positions: visible cards are centred (1 card in the middle, 2 side by side, 3-4 in a row / 2x2 grid in portrait)
    Vector2 CardPos(int i, int visible)
    {
        if (lobbyLayout == 1)
        {
            if (visible == 1) return new Vector2(0, 372);
            return new Vector2(i % 2 == 0 ? -177 : 177, i < 2 ? 372 : 372 - cardSize.y - 8);
        }
        return new Vector2((i - (visible - 1) * 0.5f) * 304f, 104f);
    }

    void RefreshLobby(float dt)
    {
        int visible = Mathf.Max(1, slots.Count);
        attractT += dt;
        for (int i = 0; i < 4; i++)
        {
            bool on = i < slots.Count;
            bool show = on || (i == 0 && slots.Count == 0);
            cards[i].gameObject.SetActive(show);
            stands[i].Tick(dt, show && state == State.Lobby);
            if (!show) continue;
            cards[i].rectTransform.anchoredPosition = CardPos(i, visible);
            if (!on)
            {
                // nobody in yet: the showroom cycles through every car and robot
                int k = (int)(attractT / 2.6f);
                stands[i].Set(k % 8, k % 7, this, false);
                cards[i].color = new Color(1, 1, 1, 0.1f);
                cardSwatch[i].color = new Color(1, 1, 1, 0.2f);
                cardHead[i].text = "JOIN";
                cardTexts[i].text = "<size=16>Press A on a gamepad, Enter on the keyboard,\nor tap / click a car or a robot below</size>";
                foreach (var b in bars[i]) b.enabled = false;
                foreach (var b in barBgs[i]) b.enabled = false;
                foreach (var b in barLabs[i]) b.enabled = false;
                continue;
            }
            Slot s = slots[i];
            if (stands[i].owner == s)
            {
                if (stands[i].car != s.car) carPop[s.car] = 1f;
                if (stands[i].robot != s.robot) robotPop[s.robot] = 1f;
            }
            stands[i].Set(s.car, s.robot, s, true);
            CarSpec cs = Cars.All[s.car];
            string dev = s.kind == InputKind.Gamepad ? "Gamepad" : s.kind == InputKind.Keyboard ? "Keyboard" : "Touch";
            cards[i].color = new Color(cs.color.r * 0.5f, cs.color.g * 0.5f, cs.color.b * 0.5f, 0.55f);
            cardSwatch[i].color = cs.color;
            cardHead[i].text = "<color=#" + ColorUtility.ToHtmlStringRGB(PlayerCol[i]) + ">P" + (i + 1) + "</color> · " + dev;
            string nav = s.kind == InputKind.Gamepad ? "D-pad < > car · ^ v robot" : s.kind == InputKind.Keyboard ? "arrows < > car · ^ v robot · or click" : "tap a car / robot below";
            Color nameCol = s.car == 7 ? new Color(0.7f, 0.7f, 0.75f) : cs.color;
            cardTexts[i].text = "<size=21><color=#" + ColorUtility.ToHtmlStringRGB(nameCol) + ">" + cs.name.ToUpper() + "</color>  ·  " + Robots.Names[s.robot] + "</size>\n" +
                "<size=12>" + Robots.Looks[s.robot] + "</size>\n<size=12><color=#bfe6ff>" + nav + "</color></size>";
            float[] v = { Mathf.InverseLerp(27f, 34f, cs.top), Mathf.InverseLerp(9f, 14f, cs.accel), Mathf.InverseLerp(78f, 106f, cs.handling), Mathf.InverseLerp(0.8f, 1.4f, cs.weight) };
            float bw = cardSize.x - 140f;
            for (int b = 0; b < 4; b++)
            {
                bars[i][b].enabled = true; barBgs[i][b].enabled = true; barLabs[i][b].enabled = true;
                bars[i][b].rectTransform.sizeDelta = new Vector2(bw * Mathf.Max(0.08f, v[b]), 11f);
                bars[i][b].color = b == 0 && s.car == 0 ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 0.85f, 0.3f);
            }
        }
        // thumbnails: textures, highlight, badges, pop
        if (Showroom.ThumbsReady)
        {
            thumbCheckT += dt;
            if (thumbCheckT > 2f) { thumbCheckT = 0f; if (Showroom.ThumbsLost()) Showroom.RenderThumbs(); }
        }
        Slot ps = FindSlot(InputKind.Touch) ?? FindSlot(InputKind.Keyboard);
        for (int i = 0; i < 8; i++)
        {
            if (Showroom.ThumbsReady && carThumbs[i].texture == null) { carThumbs[i].texture = Showroom.CarThumbs[i]; carThumbs[i].color = Color.white; }
            bool taken = false; string badge = ""; int firstPick = -1;
            for (int k = 0; k < slots.Count; k++)
                if (slots[k].car == i)
                {
                    if (slots[k] != ps) taken = true;
                    if (firstPick < 0) firstPick = k;
                    badge += "<color=#" + ColorUtility.ToHtmlStringRGB(PlayerCol[k]) + ">P" + (k + 1) + "</color> ";
                }
            carBadges[i].text = badge;
            TileStyle(carTiles[i], carThumbs[i], firstPick, ps != null && ps.car == i, taken, ref carPop[i], dt);
        }
        for (int i = 0; i < 7; i++)
        {
            if (Showroom.ThumbsReady && robotThumbs[i].texture == null) { robotThumbs[i].texture = Showroom.RobotThumbs[i]; robotThumbs[i].color = Color.white; }
            bool taken = false; string badge = ""; int firstPick = -1;
            for (int k = 0; k < slots.Count; k++)
                if (slots[k].robot == i)
                {
                    if (slots[k] != ps) taken = true;
                    if (firstPick < 0) firstPick = k;
                    badge += "<color=#" + ColorUtility.ToHtmlStringRGB(PlayerCol[k]) + ">P" + (k + 1) + "</color> ";
                }
            robotBadges[i].text = badge;
            TileStyle(robotTiles[i], robotThumbs[i], firstPick, ps != null && ps.robot == i, taken, ref robotPop[i], dt);
        }
        string top = "<color=#7dff8a>GREEN is the fastest car</color> (" + Cars.All[0].Kmh + " km/h top speed)  ·  ";
        if (slots.Count == 0) lobbyStatus.text = top + "join to pick your car + robot";
        else lobbyStatus.text = top + slots.Count + " player" + (slots.Count > 1 ? "s" : "") + " in  ·  A / Start / Enter / RACE! to go";
        lobbyHelp.text = HelpLines;
    }

    // tile look: frame in the colour of the (first) player who picked it, white ring for the tap / mouse player's pick,
    // dimmed when another player holds it, and a short pop when it is picked
    static void TileStyle(Image tile, RawImage thumb, int pickedBy, bool mine, bool taken, ref float pop, float dt)
    {
        pop = Mathf.Max(0f, pop - dt * 3.5f);
        float s = 1f + 0.16f * Mathf.Sin(pop * Mathf.PI);
        if (pickedBy >= 0) s += 0.04f;
        tile.rectTransform.localScale = Vector3.one * s;
        Color pc = pickedBy >= 0 ? PlayerCol[pickedBy] : new Color(0f, 0f, 0f, 0.55f);
        tile.color = pickedBy >= 0 ? new Color(pc.r, pc.g, pc.b, 0.95f) : pc;
        var ol = tile.GetComponent<Outline>();
        if (ol == null) { ol = tile.gameObject.AddComponent<Outline>(); ol.effectDistance = new Vector2(3, 3); }
        ol.effectColor = mine ? Color.white : new Color(0, 0, 0, 0.5f);
        if (thumb.texture != null) thumb.color = taken && !mine ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
    }

    Slot FindSlot(InputKind k) { foreach (var s in slots) if (s.kind == k) return s; return null; }
    Slot FindPad(Gamepad p) { foreach (var s in slots) if (s.kind == InputKind.Gamepad && s.pad == p) return s; return null; }

    bool CarFree(int c, Slot except) { foreach (var s in slots) if (s != except && s.car == c) return false; return true; }
    bool RobotFree(int r, Slot except) { foreach (var s in slots) if (s != except && s.robot == r) return false; return true; }

    Slot Join(InputKind kind, Gamepad pad, int car = -1)
    {
        if (slots.Count >= 4) return null;
        var s = new Slot { kind = kind, pad = pad, joinTime = Time.unscaledTime };
        if (car < 0 || !CarFree(car, null)) { car = 0; while (!CarFree(car, null)) car++; }
        int robot = slots.Count % Robots.Count;
        while (!RobotFree(robot, null)) robot = (robot + 1) % Robots.Count;
        s.car = car; s.robot = robot;
        slots.Add(s);
        Assign(); PlaceGrid();
        Sfx.Play(Sfx.ItemGet, 0.6f);
        Debug.Log("Join P" + slots.Count + " " + kind + (pad != null ? " " + pad.displayName + " #" + pad.deviceId : ""));
        return s;
    }

    void Leave(Slot s)
    {
        if (s == null) return;
        if (s.cam != null) Destroy(s.cam.gameObject);
        if (s.hud != null) Destroy(s.hud.panel.gameObject);
        slots.Remove(s);
        foreach (var k in karts) k.SetHuman(false);
        Assign(); PlaceGrid();
        Sfx.Play(Sfx.Click, 0.6f);
    }

    void CycleCar(Slot s, int dir)
    {
        int c = s.car;
        for (int k = 0; k < 8; k++) { c = ((c + dir) % 8 + 8) % 8; if (CarFree(c, s)) break; }
        s.car = c; Assign(); Sfx.Play(Sfx.Click, 0.3f);
    }

    void CycleRobot(Slot s, int dir)
    {
        int r = s.robot;
        for (int k = 0; k < Robots.Count; k++) { r = ((r + dir) % Robots.Count + Robots.Count) % Robots.Count; if (RobotFree(r, s)) break; }
        s.robot = r; Assign(); Sfx.Play(Sfx.Click, 0.3f);
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
            float t; string os;
            if (pressTimes.TryGetValue(other.deviceId, out t) && now - t < 0.15f && FindPad(other) != null
                && pressSig.TryGetValue(other.deviceId, out os) && os == sig) ghost = true;
        }
        pressTimes[pad.deviceId] = now;
        pressSig[pad.deviceId] = sig;
        if (ghost) ghosts.Add(pad.deviceId); else ghosts.Remove(pad.deviceId);
        return ghost;
    }

    static bool Hit(Image img, Vector2 screenPos)
    {
        return img != null && img.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(img.rectTransform, screenPos, null);
    }

    // ?lbdemo in the page URL: join keyboard P1 and start a self-driving race (headless screenshots / attract checks)
    float demoT = -1f;
    void DemoCheck()
    {
        if (demoT < 0f)
        {
            string u = Application.absoluteURL ?? "";
            demoT = u.Contains("lbdemo") ? 0.01f : 0f;
            Kart.Autopilot = demoT > 0f;
            if (demoT > 0f) Debug.Log("Lambo Blast: demo mode");
        }
        if (demoT <= 0f) return;
        demoT += Time.unscaledDeltaTime;
        if (demoT > 6f && state == State.Lobby)
        {
            demoT = 0f;
            if (slots.Count == 0) Join(InputKind.Keyboard, null);
            StartRace();
        }
    }

    void UpdateLobby(float dt)
    {
        DemoCheck();
        if (state != State.Lobby) return;
        if (Kb.CDown()) CreditsToggle();
        if (creditsPanel != null && creditsPanel.gameObject.activeSelf)
        {
            bool close = Kb.EscDown() || Kb.EnterDown() || (Kb.MouseLeftDown() && Kb.TouchCount() == 0);
            foreach (Vector2 tp in Kb.TouchesBegan()) close = true;
            foreach (var pd in Gamepad.all) if (pd.buttonEast.wasPressedThisFrame || pd.buttonSouth.wasPressedThisFrame) close = true;
            if (close) CreditsToggle(false);
            RefreshLobby(dt);
            return;
        }
        lobbyCanvas.enabled = true;
        hudCanvas.enabled = false;
        resultsCanvas.enabled = false;
        pauseBtn.gameObject.SetActive(false);
        LayoutLobby();
        Sfx.Music("lobby");
        float now = Time.unscaledTime;

        // keyboard
        Slot ks = FindSlot(InputKind.Keyboard);
        if (Kb.EnterDown())
        {
            if (ks == null) Join(InputKind.Keyboard, null);
            else if (now - ks.joinTime > 0.3f) { StartRace(); return; }
        }
        ks = FindSlot(InputKind.Keyboard);
        if (ks != null)
        {
            if (Kb.EscDown()) { Leave(ks); ks = null; }
            else
            {
                if (Kb.LeftDown()) CycleCar(ks, -1);
                if (Kb.RightDown()) CycleCar(ks, 1);
                if (Kb.UpDown()) CycleRobot(ks, -1);
                if (Kb.DownDown()) CycleRobot(ks, 1);
            }
        }

        // gamepads
        foreach (Gamepad pad in Gamepad.all)
        {
            Slot ps = FindPad(pad);
            bool g = ghosts.Contains(pad.deviceId);
            if (ps != null && !g)
            {
                if (pad.buttonEast.wasPressedThisFrame) { Leave(ps); continue; }
                Vector2 st = pad.leftStick.ReadValue();
                bool lx = st.x < -0.6f, rx = st.x > 0.6f, uy = st.y > 0.6f, dy = st.y < -0.6f;
                if (pad.dpad.left.wasPressedThisFrame || (lx && !ps.stickHeldX)) CycleCar(ps, -1);
                if (pad.dpad.right.wasPressedThisFrame || (rx && !ps.stickHeldX)) CycleCar(ps, 1);
                if (pad.dpad.up.wasPressedThisFrame || (uy && !ps.stickHeldY)) CycleRobot(ps, -1);
                if (pad.dpad.down.wasPressedThisFrame || (dy && !ps.stickHeldY)) CycleRobot(ps, 1);
                ps.stickHeldX = Mathf.Abs(st.x) > 0.3f;
                ps.stickHeldY = Mathf.Abs(st.y) > 0.3f;
            }
            if (!pad.buttonSouth.wasPressedThisFrame && !pad.startButton.wasPressedThisFrame) continue;
            if (IsGhostPress(pad) && ps == null) continue;
            if (ps == null) Join(InputKind.Gamepad, pad);
            else if (now - ps.joinTime > 0.4f) { StartRace(); return; }
        }

        // touch: a phone with no pads joins P1 straight away
        if (Application.isMobilePlatform && FindSlot(InputKind.Touch) == null && slots.Count == 0) Join(InputKind.Touch, null);
        foreach (Vector2 pos in Kb.TouchesBegan())
        {
            lastTouchTime = now;
            if (Sfx.ButtonHit(pos)) continue;
            if (LobbyTap(pos, InputKind.Touch)) return;
        }
        // mouse (desktop)
        if (Kb.MouseLeftDown() && Kb.TouchCount() == 0 && now - lastTouchTime > 1f)
        {
            Vector2 mp = Kb.MousePos();
            if (!Sfx.ButtonHit(mp) && LobbyTap(mp, InputKind.Keyboard)) return;
        }

        // showroom camera: slow orbit around the grid
        orbit += dt * 9f;
        Vector3 c = Track.PointAt(Track.Length - 26f, 0f) + Vector3.up * 1f;
        float a = orbit * Mathf.Deg2Rad;
        overview.enabled = true;
        overview.rect = new Rect(0, 0, 1, 1);
        overview.orthographic = false;
        overview.fieldOfView = 55f;
        SetPost(overview, true);
        Sfx.PlaceButton(true);
        overview.transform.position = c + new Vector3(Mathf.Sin(a) * 30f, 9f, Mathf.Cos(a) * 30f);
        overview.transform.LookAt(c);
        foreach (var k in karts) k.Tick(dt, false, false);
        RefreshLobby(dt);
    }

    // returns true when the race started
    bool LobbyTap(Vector2 pos, InputKind kind)
    {
        if (Hit(creditsBtn, pos)) { CreditsToggle(true); return false; }
        if (Hit(playBtn, pos))
        {
            if (slots.Count == 0) Join(kind, null);
            StartRace();
            return true;
        }
        for (int i = 0; i < 8; i++)
            if (Hit(carTiles[i], pos))
            {
                Slot s = FindSlot(kind);
                if (s == null) { Join(kind, null, i); return false; }
                if (CarFree(i, s)) { s.car = i; Assign(); Sfx.Play(Sfx.Click, 0.3f); }
                return false;
            }
        for (int i = 0; i < 7; i++)
            if (Hit(robotTiles[i], pos))
            {
                Slot s = FindSlot(kind);
                if (s == null) s = Join(kind, null);
                if (s != null && RobotFree(i, s)) { s.robot = i; Assign(); Sfx.Play(Sfx.Click, 0.3f); }
                return false;
            }
        return false;
    }

    // ---------------- race flow ----------------
    void StartRace()
    {
        if (slots.Count == 0) return;
        Assign();
        PlaceGrid();
        state = State.Countdown;
        countdownT = 3.8f;
        lastBeep = 4f;
        raceTime = 0f;
        finishCount = 0;
        allHumansDoneT = -1f;
        paused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        lobbyCanvas.enabled = false;
        resultsCanvas.enabled = false;
        hudCanvas.enabled = true;
        foreach (var st in stands) st.Tick(0f, false);
        foreach (var s in slots) MakeView(s);
        ApplyLayout();
        Sfx.Music("beach");
        Sfx.Play(Sfx.Click, 0.8f);
    }

    void MakeView(Slot s)
    {
        int idx = slots.IndexOf(s);
        if (s.cam == null)
        {
            s.cam = MakeCam("Cam P" + (idx + 1), 2 + idx);
            s.rig = new ChaseCam(s.cam);
        }
        if (s.hud == null) s.hud = new ViewHud(hudCanvas.transform, "P" + (idx + 1), mapTex);
        s.rig.Reset();
    }

    void ApplyLayout()
    {
        int n = slots.Count;
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
            s.cam.enabled = true;
            s.hud.SetRect(r, n == 1 ? 1f : n == 2 ? 0.8f : 0.6f);
            s.hud.SetActive(true);
        }
        // 3 players: the 4th quarter shows the whole island from above
        overview.enabled = n == 3;
        if (n == 3)
        {
            overview.rect = new Rect(0.5f, 0f, 0.5f, 0.5f);
            overview.orthographic = true;
            overview.orthographicSize = Mathf.Max(Track.Max.x - Track.Min.x, Track.Max.y - Track.Min.y) * 0.5f + 25f;
            Vector2 c = (Track.Min + Track.Max) * 0.5f;
            overview.transform.position = new Vector3(c.x, 300f, c.y);
            overview.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
        sepV.enabled = n >= 3 || (n == 2 && !portrait);
        sepH.enabled = n >= 3 || (n == 2 && portrait);
        // performance: quality tier by device + number of views (shadows, post) - see Look
        int views = n + (n == 3 ? 1 : 0);
        var cams = new List<Camera>();
        foreach (var sl in slots) if (sl.cam != null) cams.Add(sl.cam);
        Look.ApplyViews(views, cams);
        SetPost(overview, false);
        Sfx.PlaceButton(false);
        Debug.Log("Layout: " + n + " players");
    }

    public void OnFinish(Kart k)
    {
        finishCount++;
        k.place = finishCount;
        if (k.human)
        {
            Sfx.Play(k.place <= 3 ? Sfx.Win : Sfx.Finish, 0.8f);
            Sfx.Duck(2.5f);
            k.Toast(k.place == 1 ? "YOU WIN!" : "Finished " + k.place + (k.place == 2 ? "nd" : k.place == 3 ? "rd" : "th") + "!", 4f);
        }
    }

    void UpdateStandings()
    {
        order.Clear();
        order.AddRange(karts);
        order.Sort((a, b) =>
        {
            if (a.finished && b.finished) return a.place.CompareTo(b.place);
            if (a.finished) return -1;
            if (b.finished) return 1;
            return b.RaceDist.CompareTo(a.RaceDist);
        });
        for (int i = 0; i < order.Count; i++) if (!order[i].finished) order[i].place = i + 1;
    }

    void KartCollisions()
    {
        for (int i = 0; i < karts.Count; i++)
            for (int j = i + 1; j < karts.Count; j++)
            {
                Kart a = karts[i], b = karts[j];
                if (a.respawning || b.respawning) continue;
                Vector3 pa = a.transform.position, pb = b.transform.position;
                if (Mathf.Abs(pa.y - pb.y) > 1.6f) continue;
                Vector3 d = Track.Flat(pb - pa);
                float dist = d.magnitude;
                const float R = 2.5f;
                if (dist >= R || dist < 0.001f) continue;
                Vector3 n = d / dist;
                float wa = a.spec.weight, wb = b.spec.weight;
                float over = R - dist;
                a.transform.position -= n * over * wb / (wa + wb);
                b.transform.position += n * over * wa / (wa + wb);
                Vector3 va = a.Forward * a.speed + a.Right * a.lat, vb = b.Forward * b.speed + b.Right * b.lat;
                float rel = Vector3.Dot(va - vb, n);
                if (rel <= 0f) continue;
                float J = rel * 1.3f / (1f / wa + 1f / wb);
                va -= n * J / wa; vb += n * J / wb;
                a.speed = Vector3.Dot(va, a.Forward); a.lat = Vector3.Dot(va, a.Right);
                b.speed = Vector3.Dot(vb, b.Forward); b.lat = Vector3.Dot(vb, b.Right);
                if (rel > 5f)
                {
                    Sfx.PlayAt(Sfx.Bump, (pa + pb) * 0.5f, Mathf.Clamp01(rel / 15f));
                    Shake(a, 0.25f); Shake(b, 0.25f);
                }
            }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        touch.active = (state == State.Countdown || state == State.Race) && FindSlot(InputKind.Touch) != null && !paused;
        switch (state)
        {
            case State.Lobby: UpdateLobby(dt); break;
            case State.Results: UpdateResults(dt); break;
            default: UpdateRace(dt); break;
        }
    }

    void ReadInputs()
    {
        for (int k = 0; k < slots.Count; k++)
        {
            Slot s = slots[k];
            KIn i;
            switch (s.kind)
            {
                case InputKind.Gamepad:
                    if (s.pad == null || !s.pad.added) { i = new KIn(); break; }
                    if (Pads.AnyButtonDown(s.pad)) IsGhostPress(s.pad);
                    i = Pads.Read(s.pad);
                    break;
                case InputKind.Keyboard: i = Kb.Read(); break;
                default: i = touch.Read(); break;
            }
            s.kart.input = i;
        }
    }

    bool AnyPausePressed()
    {
        foreach (var s in slots)
        {
            if (s.kind == InputKind.Gamepad && s.pad != null && s.pad.added && s.pad.startButton.wasPressedThisFrame) return true;
            if (s.kind == InputKind.Keyboard && (Kb.EscDown() || Kb.PDown())) return true;
        }
        if (FindSlot(InputKind.Keyboard) == null && Kb.EscDown()) return true;
        foreach (Vector2 p in Kb.TouchesBegan()) if (PauseButtonHit(p)) return true;
        if (Kb.MouseLeftDown() && Kb.TouchCount() == 0 && PauseButtonHit(Kb.MousePos())) return true;
        return false;
    }

    void SetPaused(bool p)
    {
        paused = p;
        Time.timeScale = p ? 0f : 1f;
        AudioListener.pause = p;
        pauseCanvas.enabled = p;
        if (p) pauseText.text = "<size=48><color=#7dff8a>PAUSED</color></size>\n\n" + HelpLines.Replace(" · Start pause", "") +
            "\n\n<b>Pad</b>: Start / A resume · Y restart · B lobby      <b>Keys</b>: Esc / Enter resume · R restart · Q lobby";
    }

    void UpdatePause()
    {
        bool resume = false, restart = false, quit = false;
        foreach (var s in slots)
        {
            if (s.kind != InputKind.Gamepad || s.pad == null || !s.pad.added) continue;
            if (s.pad.startButton.wasPressedThisFrame || s.pad.buttonSouth.wasPressedThisFrame) resume = true;
            if (s.pad.buttonNorth.wasPressedThisFrame) restart = true;
            if (s.pad.buttonEast.wasPressedThisFrame) quit = true;
        }
        if (Kb.EscDown() || Kb.EnterDown() || Kb.PDown()) resume = true;
        if (Kb.RDown()) restart = true;
        if (Kb.QDown() || Kb.BackDown()) quit = true;
        var taps = Kb.TouchesBegan();
        if (Kb.MouseLeftDown() && Kb.TouchCount() == 0) taps.Add(Kb.MousePos());
        foreach (Vector2 p in taps)
        {
            if (Hit(resumeBtn, p) || PauseButtonHit(p)) resume = true;
            else if (Hit(restartBtn, p)) restart = true;
            else if (Hit(quitBtn, p)) quit = true;
        }
        if (quit) { SetPaused(false); ToLobby(); }
        else if (restart) { SetPaused(false); StartRace(); }
        else if (resume) SetPaused(false);
    }

    void UpdateRace(float dt)
    {
        lobbyCanvas.enabled = false;
        pauseBtn.gameObject.SetActive(true);
        if (paused) { UpdatePause(); return; }
        if (AnyPausePressed()) { SetPaused(true); return; }
        ReadInputs();
        bool countdown = state == State.Countdown;
        string centre = "";
        if (countdown)
        {
            countdownT -= dt;
            int num = Mathf.CeilToInt(countdownT);
            if (num > 3) centre = "<size=80>READY?</size>";
            else if (num >= 1)
            {
                centre = "<size=150>" + num + "</size>";
                if (num < lastBeep) { lastBeep = num; Sfx.Play(Sfx.Beep, 0.8f); }
            }
            if (countdownT <= 0f)
            {
                state = State.Race;
                Sfx.Play(Sfx.Go, 0.9f);
                goT = 1f;
                foreach (var k in karts) k.LaunchStart();
            }
        }
        else raceTime += dt;
        if (goT > 0f) { goT -= dt; centre = "<size=150><color=#7dff8a>GO!</color></size>"; }

        bool racing = state == State.Race;
        foreach (var k in karts) k.Tick(dt, racing, countdown);
        if (racing)
        {
            for (int i = Shot.Live.Count - 1; i >= 0; i--) if (i < Shot.Live.Count && Shot.Live[i] != null) Shot.Live[i].Tick(dt);
            KartCollisions();
        }
        UpdateStandings();
        RubberBand();
        AIEngineSound();

        // end of race: every human finished -> give the AI a few seconds, then results
        bool allHumans = slots.Count > 0;
        foreach (var s in slots) if (!s.kart.finished) allHumans = false;
        bool allKarts = true;
        foreach (var k in karts) if (!k.finished) allKarts = false;
        if (allHumans && allHumansDoneT < 0f) allHumansDoneT = 5f;
        if (allHumansDoneT > 0f) { allHumansDoneT -= dt; if (allHumansDoneT <= 0f || allKarts) ShowResults(); }

        // views
        float fov = slots.Count == 2 && Screen.width > Screen.height ? 70f : 62f;
        foreach (var s in slots)
        {
            s.rig.Update(s.kart, dt, fov);
            s.hud.bottomInset = s.kind == InputKind.Touch ? touch.BottomBand : 0f;
            s.hud.Tick(s.cam, s.kart, this, centre);
        }
    }

    float goT;

    void RubberBand()
    {
        float best = -99f;
        foreach (var s in slots) best = Mathf.Max(best, s.kart.RaceDist);
        foreach (var k in karts)
        {
            if (k.human) continue;
            if (slots.Count == 0) { k.aiTopMul = 1f; continue; }
            float metres = (k.RaceDist - best) * Track.Length;
            k.aiTopMul = Mathf.Clamp(1f - metres * 0.0008f, 0.88f, 1.08f);
        }
    }

    void AIEngineSound()
    {
        float bestD = 1e9f, sp = 0f;
        foreach (var k in karts)
        {
            if (k.human) continue;
            float d = NearestHumanDistance(k.transform.position);
            if (d < bestD) { bestD = d; sp = Mathf.Abs(k.speed) / k.spec.top; }
        }
        Sfx.SetAIEngine(0.13f * Mathf.Clamp01(1f - bestD / 35f) * (state == State.Lobby ? 0f : 1f), 0.7f + sp * 0.9f);
    }

    void ShowResults()
    {
        state = State.Results;
        resultsT = 0f;
        UpdateStandings();
        // unfinished karts keep their running order
        foreach (var k in karts) if (!k.finished) { k.finishTime = -1f; }
        int bestHuman = 99;
        foreach (var s in slots) bestHuman = Mathf.Min(bestHuman, s.kart.place);
        Sfx.Play(bestHuman <= 3 ? Sfx.Win : Sfx.Lose, 0.9f);
        Sfx.Duck(3f);
        var sb = new System.Text.StringBuilder();
        sb.Append("<size=46><color=#7dff8a>RACE RESULTS</color></size>\n\n");
        for (int i = 0; i < order.Count; i++)
        {
            Kart k = order[i];
            string t = k.finished ? FormatTime(k.finishTime) : "--:--.-";
            string who = k.human ? "  <color=#ffd84a>(P" + (k.slot + 1) + ")</color>" : "";
            Color c = k.Color == Cars.All[7].color ? new Color(0.7f, 0.7f, 0.75f) : k.Color;
            sb.Append((i + 1) + ".  <color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" + k.Nick + "</color>" + who + "      " + t + "\n");
            resultRows[i].color = k.human ? new Color(1f, 0.85f, 0.3f, 0.18f) : new Color(1f, 1f, 1f, 0.06f);
        }
        sb.Append("\n<size=20>A / Enter / tap: race again   ·   B / Esc: back to the lobby</size>");
        resultsText.text = sb.ToString();
        resultsCanvas.enabled = true;
    }

    static string FormatTime(float t)
    {
        if (t < 0f) return "--:--.-";
        int m = (int)(t / 60f);
        float s = t - m * 60f;
        return m + ":" + s.ToString("00.0");
    }

    void UpdateResults(float dt)
    {
        resultsT += dt;
        pauseBtn.gameObject.SetActive(false);
        // the cars keep cruising behind the results
        foreach (var k in karts) k.Tick(dt, true, false);
        for (int i = Shot.Live.Count - 1; i >= 0; i--) if (i < Shot.Live.Count && Shot.Live[i] != null) Shot.Live[i].Tick(dt);
        foreach (var s in slots) { s.rig.Update(s.kart, dt, 62f); s.hud.Tick(s.cam, s.kart, this, ""); }
        if (resultsT < 1.2f) return;
        bool again = Kb.EnterDown() || Kb.SpaceDown(), lobby = Kb.EscDown() || Kb.BackDown();
        foreach (var s in slots)
        {
            if (s.kind != InputKind.Gamepad || s.pad == null || !s.pad.added) continue;
            if (s.pad.buttonSouth.wasPressedThisFrame || s.pad.startButton.wasPressedThisFrame) again = true;
            if (s.pad.buttonEast.wasPressedThisFrame) lobby = true;
        }
        if (Kb.TouchesBegan().Count > 0) again = true;
        if (Kb.MouseLeftDown() && Kb.TouchCount() == 0 && !Sfx.ButtonHit(Kb.MousePos())) again = true;
        if (lobby) ToLobby();
        else if (again) StartRace();
    }

    void ToLobby()
    {
        state = State.Lobby;
        resultsCanvas.enabled = false;
        hudCanvas.enabled = false;
        foreach (var s in slots)
        {
            if (s.cam != null) s.cam.enabled = false;
            if (s.hud != null) s.hud.SetActive(false);
        }
        overview.orthographic = false;
        Look.ApplyViews(1, null);
        Assign();
        PlaceGrid();
    }
}
