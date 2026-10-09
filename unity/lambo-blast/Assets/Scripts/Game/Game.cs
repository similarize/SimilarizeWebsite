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
    readonly Image[] swatches = new Image[8];
    readonly Image[] robotBtns = new Image[7];
    readonly Text[] robotTexts = new Text[7];
    Image pauseBtn, resumeBtn, restartBtn, quitBtn;
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
        BuildLobbyUI();
        BuildHudUI();
        BuildResultsUI();
        BuildPauseUI();
        Sfx.Music("lobby");
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
        lobbyBg = UIK.Img(r, null, new Color(0.02f, 0.06f, 0.1f, 0.6f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240, 690));
        lobbyTitle = UIK.Label(r, "LAMBO BLAST", 66, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 300), new Vector2(1100, 80), new Color(0.35f, 1f, 0.45f));
        lobbySub = UIK.Label(r, "Robot racers on Coconut Cove  ·  8 cars  ·  3 laps  ·  grab the ? boxes and blast them", 22, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 252), new Vector2(1180, 36), Color.white);
        for (int i = 0; i < 4; i++)
        {
            cards[i] = UIK.Img(r, null, new Color(1, 1, 1, 0.12f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(290, 250));
            cardSwatch[i] = UIK.Img(cards[i].transform, null, Color.white, new Vector2(0.5f, 1f), new Vector2(0, -12), new Vector2(270, 10));
            cardTexts[i] = UIK.Label(cards[i].transform, "", 20, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0, -88), new Vector2(280, 140), Color.white);
            bars[i] = new Image[4];
            string[] lab = { "TOP SPEED", "ACCEL", "HANDLING", "WEIGHT" };
            for (int b = 0; b < 4; b++)
            {
                float y = -150 - b * 22;
                UIK.Label(cards[i].transform, lab[b], 14, TextAnchor.MiddleLeft, new Vector2(0.5f, 1f), new Vector2(-70, y), new Vector2(120, 20), new Color(1, 1, 1, 0.8f));
                UIK.Img(cards[i].transform, null, new Color(0, 0, 0, 0.4f), new Vector2(0.5f, 1f), new Vector2(60, y), new Vector2(140, 12));
                bars[i][b] = UIK.Img(cards[i].transform, null, Color.white, new Vector2(0.5f, 1f), new Vector2(60, y), new Vector2(140, 12));
                bars[i][b].rectTransform.pivot = new Vector2(0f, 0.5f);
                bars[i][b].rectTransform.anchoredPosition = new Vector2(-10, y);
            }
        }
        for (int i = 0; i < 8; i++)
        {
            swatches[i] = UIK.Img(r, null, Cars.All[i].color, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100, 46));
            var t = UIK.Label(swatches[i].transform, Cars.All[i].name.ToUpper(), 16, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100, 40), i == 6 || i == 2 ? new Color(0.1f, 0.1f, 0.1f) : Color.white);
            t.GetComponent<Outline>().enabled = !(i == 6 || i == 2);
        }
        for (int i = 0; i < 7; i++)
        {
            robotBtns[i] = UIK.Img(r, null, new Color(0f, 0f, 0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(156, 40));
            robotTexts[i] = UIK.Label(robotBtns[i].transform, Robots.Names[i], 17, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(156, 38), Color.white);
        }
        playBtn = UIK.Img(r, null, new Color(0.15f, 0.65f, 0.25f, 0.9f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 56));
        UIK.Label(playBtn.transform, "RACE!", 34, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 56), Color.white);
        lobbyStatus = UIK.Label(r, "", 22, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180, 34), new Color(0.7f, 1f, 0.7f));
        lobbyHelp = UIK.Label(r, "", 16, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 60), new Color(1, 1, 1, 0.85f));
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
            put(lobbyBg, new Vector2(0, -60), new Vector2(710, 1060));
            put(lobbyTitle, new Vector2(0, 430), new Vector2(680, 70)); lobbyTitle.fontSize = 54;
            put(lobbySub, new Vector2(0, 380), new Vector2(680, 50)); lobbySub.fontSize = 18;
            for (int i = 0; i < 4; i++) put(cards[i], new Vector2(i % 2 == 0 ? -175 : 175, i < 2 ? 210 : -50), new Vector2(330, 250));
            for (int i = 0; i < 8; i++) put(swatches[i], new Vector2(-255 + (i % 4) * 170, -210 - (i / 4) * 56), new Vector2(160, 48));
            for (int i = 0; i < 7; i++) put(robotBtns[i], new Vector2(i < 4 ? -255 + i * 170 : -170 + (i - 4) * 170, -340 - (i / 4) * 50), new Vector2(160, 42));
            put(playBtn, new Vector2(0, -470), new Vector2(360, 80));
            put(lobbyStatus, new Vector2(0, -530), new Vector2(690, 40));
            put(lobbyHelp, new Vector2(0, -575), new Vector2(690, 60)); lobbyHelp.fontSize = 13;
        }
        else
        {
            put(lobbyBg, Vector2.zero, new Vector2(1240, 690));
            put(lobbyTitle, new Vector2(0, 300), new Vector2(1100, 80)); lobbyTitle.fontSize = 66;
            put(lobbySub, new Vector2(0, 252), new Vector2(1180, 36)); lobbySub.fontSize = 22;
            for (int i = 0; i < 4; i++) put(cards[i], new Vector2(-456 + i * 304, 98), new Vector2(290, 250));
            for (int i = 0; i < 8; i++) put(swatches[i], new Vector2(-490 + i * 140, -64), new Vector2(128, 46));
            for (int i = 0; i < 7; i++) put(robotBtns[i], new Vector2(-504 + i * 168, -120), new Vector2(160, 42));
            put(playBtn, new Vector2(0, -184), new Vector2(300, 56));
            put(lobbyStatus, new Vector2(0, -232), new Vector2(1180, 34));
            put(lobbyHelp, new Vector2(0, -284), new Vector2(1200, 60)); lobbyHelp.fontSize = 16;
        }
    }

    void RefreshLobby()
    {
        for (int i = 0; i < 4; i++)
        {
            bool on = i < slots.Count;
            cards[i].gameObject.SetActive(on || (i == 0 && slots.Count == 0));
            if (!on)
            {
                cards[i].color = new Color(1, 1, 1, 0.1f);
                cardSwatch[i].color = new Color(1, 1, 1, 0.2f);
                cardTexts[i].text = "<size=24>JOIN</size>\n\nPress A on a gamepad,\nEnter on the keyboard,\nor tap / click a colour";
                foreach (var b in bars[i]) b.enabled = false;
                continue;
            }
            Slot s = slots[i];
            CarSpec cs = Cars.All[s.car];
            string dev = s.kind == InputKind.Gamepad ? "Gamepad" : s.kind == InputKind.Keyboard ? "Keyboard" : "Touch";
            cards[i].color = new Color(cs.color.r * 0.5f, cs.color.g * 0.5f, cs.color.b * 0.5f, 0.55f);
            cardSwatch[i].color = cs.color;
            string nav = s.kind == InputKind.Gamepad ? "D-pad" : s.kind == InputKind.Keyboard ? "arrows" : "tap";
            cardTexts[i].text = "<size=18>P" + (i + 1) + " · " + dev + "</size>\n<size=26><color=#" + ColorUtility.ToHtmlStringRGB(cs.color == Cars.All[7].color ? new Color(0.7f, 0.7f, 0.75f) : cs.color) + ">" + cs.name.ToUpper() + "</color></size>  <size=15>(" + nav + " < >)</size>\n" +
                "<size=22>" + Robots.Names[s.robot] + "</size>  <size=15>(" + nav + " ^ v)</size>\n<size=14>" + Robots.Looks[s.robot] + "</size>";
            float[] v = { Mathf.InverseLerp(27f, 34f, cs.top), Mathf.InverseLerp(9f, 14f, cs.accel), Mathf.InverseLerp(78f, 106f, cs.handling), Mathf.InverseLerp(0.8f, 1.4f, cs.weight) };
            for (int b = 0; b < 4; b++)
            {
                bars[i][b].enabled = true;
                bars[i][b].rectTransform.sizeDelta = new Vector2(140f * Mathf.Max(0.08f, v[b]), 12f);
                bars[i][b].color = b == 0 && s.car == 0 ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 0.85f, 0.3f);
            }
        }
        // which picks belong to the touch / mouse player
        Slot ps = FindSlot(InputKind.Touch) ?? FindSlot(InputKind.Keyboard);
        for (int i = 0; i < 8; i++)
        {
            bool taken = false; foreach (var s in slots) if (s.car == i && s != ps) taken = true;
            Color c = Cars.All[i].color;
            swatches[i].color = taken ? new Color(c.r, c.g, c.b, 0.25f) : c;
            var ol = swatches[i].GetComponent<Outline>();
            if (ol == null) { ol = swatches[i].gameObject.AddComponent<Outline>(); ol.effectDistance = new Vector2(3, 3); }
            ol.effectColor = ps != null && ps.car == i ? Color.white : new Color(0, 0, 0, 0.5f);
        }
        for (int i = 0; i < 7; i++)
        {
            bool mine = ps != null && ps.robot == i;
            robotBtns[i].color = mine ? new Color(0.2f, 0.55f, 0.85f, 0.9f) : new Color(0f, 0f, 0f, 0.5f);
        }
        string top = "<color=#7dff8a>GREEN is the fastest car</color> (" + Cars.All[0].Kmh + " km/h top speed)  ·  ";
        if (slots.Count == 0) lobbyStatus.text = top + "join to pick your car + robot";
        else lobbyStatus.text = top + slots.Count + " player" + (slots.Count > 1 ? "s" : "") + " in  ·  A / Start / Enter / RACE! to go";
        lobbyHelp.text = HelpLines;
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
        s.car = c; Assign(); Sfx.Play(Sfx.Click, 0.6f);
    }

    void CycleRobot(Slot s, int dir)
    {
        int r = s.robot;
        for (int k = 0; k < Robots.Count; k++) { r = ((r + dir) % Robots.Count + Robots.Count) % Robots.Count; if (RobotFree(r, s)) break; }
        s.robot = r; Assign(); Sfx.Play(Sfx.Click, 0.6f);
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

    void UpdateLobby(float dt)
    {
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
        overview.transform.position = c + new Vector3(Mathf.Sin(a) * 30f, 9f, Mathf.Cos(a) * 30f);
        overview.transform.LookAt(c);
        foreach (var k in karts) k.Tick(dt, false, false);
        RefreshLobby();
    }

    // returns true when the race started
    bool LobbyTap(Vector2 pos, InputKind kind)
    {
        if (Hit(playBtn, pos))
        {
            if (slots.Count == 0) Join(kind, null);
            StartRace();
            return true;
        }
        for (int i = 0; i < 8; i++)
            if (Hit(swatches[i], pos))
            {
                Slot s = FindSlot(kind);
                if (s == null) { Join(kind, null, i); return false; }
                if (CarFree(i, s)) { s.car = i; Assign(); Sfx.Play(Sfx.Click, 0.6f); }
                return false;
            }
        for (int i = 0; i < 7; i++)
            if (Hit(robotBtns[i], pos))
            {
                Slot s = FindSlot(kind);
                if (s == null) s = Join(kind, null);
                if (s != null && RobotFree(i, s)) { s.robot = i; Assign(); Sfx.Play(Sfx.Click, 0.6f); }
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
        // performance: fewer shadows the more views we draw (Tesla browser friendly)
        int views = n + (n == 3 ? 1 : 0);
        if (views >= 3) QualitySettings.shadows = ShadowQuality.Disable;
        else { QualitySettings.shadows = ShadowQuality.All; QualitySettings.shadowDistance = views == 2 ? 30f : 45f; }
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
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowDistance = 45f;
        Assign();
        PlaceGrid();
    }
}
