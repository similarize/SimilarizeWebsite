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
    public bool expert;          // u6g driving mode: false = KID (default, the lambo9b handling), true = EXPERT
}

// Lobby (pick a colour car + robot driver), 1-4 local split-screen, 3-2-1-GO, race, results, pause.
[DefaultExecutionOrder(-50)]
public partial class Game : MonoBehaviour
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
    // u6i: 9 cars (the 8 Lambo colours + the 2026 Roadster)
    const int NCars = 9;
    readonly Image[] carTiles = new Image[NCars], robotTiles = new Image[7];
    readonly RawImage[] carThumbs = new RawImage[NCars], robotThumbs = new RawImage[7];
    readonly Image[] carStrips = new Image[NCars];
    readonly Text[] carNames = new Text[NCars], robotNames = new Text[7], carBadges = new Text[NCars], robotBadges = new Text[7];
    readonly float[] carPop = new float[NCars], robotPop = new float[7];
    readonly Showroom.Stand[] stands = new Showroom.Stand[4];
    Vector2 cardSize, tileCarSize, tileRobotSize;
    float previewH, attractT, thumbCheckT;
    Vector2Int lastScreen;
    static readonly Color[] PlayerCol = { new Color(1f, 0.85f, 0.25f), new Color(0.3f, 0.9f, 1f), new Color(1f, 0.45f, 0.85f), new Color(0.55f, 1f, 0.35f) };
    Image pauseBtn, resumeBtn, restartBtn, quitBtn, creditsBtn, creditsPanel;
    // track picker (lobby): < NAME > box; T / Shift+T, LB / RB, tap the arrows or the box
    Image trackBox, trackPrev, trackNext;
    Text trackName, trackInfo;
    int pendingTrack = -1;
    float pendingT, trackPop;
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
        BuildNetUI();      // lambo8 online
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
        c.nearClipPlane = 0.5f;      // lambo8: 0.3 -> 0.5 m (more depth precision far away: no z-fighting on decals / curbs)
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
        if (AssignOnline()) return;     // lambo8: the room's line-up decides who drives which kart
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
        int g = 0, h = Mathf.Min(HumanKarts, karts.Count);
        for (int i = h; i < karts.Count; i++) { Vector3 p; float y; Track.GridSpot(g++, out p, out y); karts[i].Place(p, y); }
        for (int i = 0; i < h; i++) { Vector3 p; float y; Track.GridSpot(g++, out p, out y); karts[i].Place(p, y); }
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
    const string CreditsText =
        "<size=26><color=#7dff8a><b>CREDITS</b></color></size>\n" +
        "<b>Unitree</b> (Unitree H1): Unitree Robotics' official robot model (github.com/unitreerobotics/unitree_mujoco).\n" +
        "   Copyright (c) 2016-2024 HangZhou YuShu TECHNOLOGY CO.,LTD. (\"Unitree Robotics\"). BSD 3-Clause licence. Logo plate removed.\n" +
        "<b>Atlas HD</b> (hydraulic DRC Atlas): the MIT Atlas model from RobotLocomotion/models (Drake).\n" +
        "   Copyright 2012-2022 Robot Locomotion Group @ CSAIL. BSD 3-Clause licence. Team / sponsor logos removed.\n" +
        "<b>Optimus, Figure 02, Big Figure Two, Figure 03, Atlas electric</b> and the cars: modelled for this game from public photos (no logos).\n" +
        "<b>Skies</b> (Poly Haven, CC0): Kloofendal 48d Partly Cloudy, Syferfontein 18d Clear, Industrial Sunset 02, Pizzo Pernice, Kloofendal 28d Misty, Qwantani Late Afternoon (all Pure Sky).\n" +
        "<b>Ground textures</b> (Poly Haven, CC0): Aerial Beach 01, Coast Sand 01, Red Sand, Burned Ground 01, Snow 02, Forest Leaves 02, Aerial Asphalt 01, Asphalt 02.\n" +
        "<b>Surfaces</b> (ambientCG.com, CC0 1.0): Rock029, Rock030, Rock051, Rock063, Rock064, Bark012, Grass004, Snow006, Gravel042, Concrete034, Asphalt031, Lava004.\n" +
        "<b>Made for this game</b>: barriers, tyre walls, floodlights, distant mountains, car paint + lights, tyre smoke, road wear, engine sound (procedural); the 2026 Roadster model (original, from published photos' proportions, no logos).\n" +
        "<b>Fonts</b>: Montserrat (The Montserrat Project Authors) + Inter (The Inter Project Authors), SIL Open Font License 1.1.\n\n" +
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
        pauseBtn = UIK.RoundImg(topCanvas.transform, UIK.Round2, new Color(0.05f, 0.06f, 0.085f, 0.6f), new Vector2(0.5f, 1f), new Vector2(-40, -26), new Vector2(64, 40), 2f);
        UIK.Label(pauseBtn.transform, "II", 22, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 38), Color.white);
        pauseBtn.gameObject.SetActive(false);
    }

    void BuildResultsUI()
    {
        resultsCanvas = UIK.MakeCanvas("Results", null, 120, true);
        Transform r = resultsCanvas.transform;
        UIK.RoundImg(r, UIK.SoftRect, new Color(0f, 0f, 0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980, 720), 1f);
        UIK.RoundImg(r, UIK.Round2, new Color(0.05f, 0.06f, 0.085f, 0.9f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 640), 2f);
        resultRows = new Image[8];
        for (int i = 0; i < 8; i++) resultRows[i] = UIK.RoundImg(r, UIK.Round2, new Color(1f, 1f, 1f, 0.06f), new Vector2(0.5f, 0.5f), new Vector2(0, 170 - i * 50), new Vector2(820, 44), 3f);
        resultsText = UIK.Label(r, "", 24, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860, 620), Color.white);
        resultsCanvas.enabled = false;
    }

    void BuildPauseUI()
    {
        pauseCanvas = UIK.MakeCanvas("Pause", null, 150, true);
        Transform r = pauseCanvas.transform;
        UIK.Img(r, null, new Color(0.01f, 0.012f, 0.02f, 0.78f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000, 4000));
        pauseText = UIK.Label(r, "", 22, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 110), new Vector2(1180, 420), Color.white);
        resumeBtn = Button(r, "RESUME", new Vector2(-230, -170), new Color(0.15f, 0.6f, 0.25f, 0.9f));
        restartBtn = Button(r, "RESTART", new Vector2(0, -170), new Color(0.75f, 0.5f, 0.1f, 0.9f));
        quitBtn = Button(r, "LOBBY", new Vector2(230, -170), new Color(0.6f, 0.2f, 0.2f, 0.9f));
        pauseCanvas.enabled = false;
    }

    static Image Button(Transform r, string label, Vector2 pos, Color c)
    {
        Image b = UIK.RoundImg(r, UIK.Round2, c, new Vector2(0.5f, 0.5f), pos, new Vector2(200, 60), 1.4f);
        UIK.Label(b.transform, label, 26, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200, 58), Color.white);
        return b;
    }

    static string HelpLines { get { return
        "<b>Xbox pad</b>: RT / A gas · LT / B brake + reverse · L-stick steer · X / RB fire item · LB drift (hold, release = boost) · Start pause\n" +
        "<b>Keyboard</b>: WASD / arrows drive · Space fire · Shift drift · Esc pause · M sound      <b>Phone</b>: left stick steer · GAS · BRAKE · FIRE · DRIFT"; } }
    // u6i: Roadster help (pause screen; the lobby card shows the short version)
    const string RoadsterHelp = "<b>Roadster</b> thruster hop: J / E · pad Y (or R3) · phone JUMP - clears a car, refills on the ground; EXPERT: hold it to hover + brake the landing";
    // u6g: driving-mode help (pause screen + the lobby MODE button)
    const string ModeHelp = "<b>Driving mode</b> (lobby: G · pad Back · tap MODE): KID = easy grip   ·   EXPERT = squeeze the throttle (keys / GAS build up), too much spins the rear tyres, no assists, faster - brake before the bends";

    // ---------------- lobby ----------------
    public static bool TouchOnly { get { return Application.isMobilePlatform && Gamepad.all.Count == 0; } }

    Slot FindSlot(InputKind k) { foreach (var s in slots) if (s.kind == k) return s; return null; }
    Slot FindPad(Gamepad p) { foreach (var s in slots) if (s.kind == InputKind.Gamepad && s.pad == p) return s; return null; }

    bool CarFree(int c, Slot except) { foreach (var s in slots) if (s != except && s.car == c) return false; return true; }
    bool RobotFree(int r, Slot except) { foreach (var s in slots) if (s != except && s.robot == r) return false; return true; }

    Slot Join(InputKind kind, Gamepad pad, int car = -1)
    {
        // lambo7 one controller = one view (GameSeats.cs): touch / keyboard only seat a player when nobody is seated yet,
        // and a pad joining takes over an implicitly seated touch / keyboard player
        Slot seat;
        if (SeatGate(kind, pad, out seat)) return seat;
        if (slots.Count >= 4) return null;
        if (LBNet.On && slots.Count >= 1) { NetToast("Online: one player per device"); return null; }
        var s = new Slot { kind = kind, pad = pad, joinTime = Time.unscaledTime };
        if (car < 0 || !CarFree(car, null)) { car = 0; while (!CarFree(car, null)) car++; }
        int robot = slots.Count % Robots.Count;
        while (!RobotFree(robot, null)) robot = (robot + 1) % Robots.Count;
        s.car = car; s.robot = robot;
        s.expert = DriveModeSaved(kind);      // u6g: last mode picked on this kind of controller
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
        int nc = Cars.Count;
        for (int k = 0; k < nc; k++) { c = ((c + dir) % nc + nc) % nc; if (CarFree(c, s)) break; }
        s.car = c; NetPicks(s); Assign(); Sfx.Play(Sfx.Click, 0.3f);
    }
    void NetPicks(Slot s) { if (LBNet.On && slots.Count > 0 && s == slots[0]) LBNet.I.PicksChanged(s.car, s.robot); }

    void CycleRobot(Slot s, int dir)
    {
        int r = s.robot;
        for (int k = 0; k < Robots.Count; k++) { r = ((r + dir) % Robots.Count + Robots.Count) % Robots.Count; if (RobotFree(r, s)) break; }
        s.robot = r; NetPicks(s); Assign(); Sfx.Play(Sfx.Click, 0.3f);
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
            Kart.OffDemo = demoT > 0f && u.Contains("lboff");
            if (demoT > 0f) Debug.Log("Lambo Blast: demo mode");
        }
        if (demoT <= 0f) return;
        demoT += Time.unscaledDeltaTime;
        if (demoT > 6f && state == State.Lobby)
        {
            if (slots.Count == 0) Join(InputKind.Keyboard, null, DemoCar());
            bool guest = LBNet.On && LBNet.I.IsGuest, hostWaiting = LBNet.On && LBNet.I.IsHost && LBNet.I.players.Count < 2 && demoT < 90f;
            if (guest || hostWaiting || (Application.absoluteURL ?? "").Contains("room=") && !LBNet.On && demoT < 20f) return;   // online demo: the host starts once a guest is in
            demoT = 0f;
            StartRace();
        }
    }

    // u6i probe knob: ?lbcar=N picks P1's car in the demo (8 = Roadster)
    static int DemoCar()
    {
        string u = Application.absoluteURL ?? ""; int k = u.IndexOf("lbcar=");
        if (k < 0) return -1;
        int c; return int.TryParse(u.Substring(k + 6, 1), out c) && c >= 0 && c < Cars.Count ? c : -1;
    }

    float carProbeT = -1f;
    void LobbyCarProbe()
    {
        if (carProbeT < 0f) carProbeT = DemoCar() >= 0 && !(Application.absoluteURL ?? "").Contains("lbdemo") ? 0.01f : 0f;
        if (carProbeT <= 0f) return;
        carProbeT += Time.unscaledDeltaTime;
        if (carProbeT > 5f && slots.Count == 0) { Join(InputKind.Keyboard, null, DemoCar()); carProbeT = 0f; Debug.Log("U6I: lobby probe P1 on car " + DemoCar() + " (" + Cars.All[DemoCar()].name + ")"); }
    }

    void UpdateLobby(float dt)
    {
        lobbyDt = Mathf.Lerp(lobbyDt, Mathf.Min(Time.unscaledDeltaTime, 30f), 0.1f);     // u6g (RaceWarm calm threshold)
        DemoCheck();
        LobbyCarProbe();   // u6i ?lbcar=N (no lbdemo): seat a keyboard P1 on car N for showroom shots
        ProbeSeats();      // u6h ?lbseats=N (lobby layout probes)
        if (state != State.Lobby) return;
        if (Kb.CDown()) CreditsToggle();
        if (Kb.GDown() && !Kb.typing) ToggleDriveMode(FindSlot(InputKind.Keyboard) ?? (slots.Count > 0 ? slots[0] : null));     // u6g
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
        if (NetLobbyInput()) { RefreshLobby(dt); return; }     // lambo8: HOST / JOIN / keypad
        Sfx.Music("lobby");
        float now = Time.unscaledTime;

        // track picker
        bool netGuest = LBNet.On && LBNet.I.IsGuest;
        if (Kb.TDown() && !netGuest) PickTrack(Kb.Shift() ? -1 : 1);
        foreach (Gamepad pad in Gamepad.all)
        {
            if (IsGhostDev(pad) || FindPad(pad) == null && slots.Count > 0 && PadGuard.IsAlias(pad)) continue;
            if (netGuest) continue;
            if (pad.rightShoulder.wasPressedThisFrame) PickTrack(1);
            if (pad.leftShoulder.wasPressedThisFrame) PickTrack(-1);
        }
        if (pendingTrack >= 0)
        {
            pendingT -= Time.unscaledDeltaTime;
            if (pendingT <= 0f) { int id = pendingTrack; pendingTrack = -1; RebuildTrack(id); }
        }

        // keyboard
        Slot ks = FindSlot(InputKind.Keyboard);
        if (Kb.EnterDown())
        {
            if (slots.Count == 0) Join(InputKind.Keyboard, null);
            else if (now - slots[0].joinTime > 0.3f) { StartRace(); return; }      // Enter starts for whoever is seated
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

        // gamepads (joins: PadJoins via PadGuard in SeatsUpdate - an explicit A / Start, never a mirror)
        foreach (Gamepad pad in Gamepad.all)
        {
            Slot ps = FindPad(pad);
            bool g = IsGhostDev(pad);
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
            if (ps != null && !g && pad.selectButton.wasPressedThisFrame) ToggleDriveMode(ps);     // u6g: Back / View = driving mode
            if (!pad.buttonSouth.wasPressedThisFrame && !pad.startButton.wasPressedThisFrame) continue;
            if (ps == null || g) continue;
            if (now - ps.joinTime > 0.4f) { StartRace(); return; }
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
        for (int i = 0; i < 4 && i < slots.Count; i++)
            if (Hit(cardMode[i], pos)) { ToggleDriveMode(slots[i]); return false; }     // u6h: per-player MODE chip (2-4 players)
        if (modeBtn != null && Hit(modeBtn, pos))     // u6g: driving mode for the player on this input (or P1)
        {
            Slot ms = FindSlot(kind);
            if (ms == null && slots.Count == 0) ms = Join(kind, null);
            if (ms == null && slots.Count > 0) ms = slots[0];
            ToggleDriveMode(ms);
            return false;
        }
        bool netGuest = LBNet.On && LBNet.I.IsGuest;
        if (Hit(trackPrev, pos)) { if (!netGuest) PickTrack(-1); return false; }
        if (Hit(trackNext, pos) || Hit(trackBox, pos)) { if (!netGuest) PickTrack(1); return false; }
        if (Hit(playBtn, pos))
        {
            if (slots.Count == 0) Join(kind, null);
            if (slots.Count == 0) return false;
            StartRace();
            return true;
        }
        for (int i = 0; i < NCars; i++)
            if (Hit(carTiles[i], pos))
            {
                Slot s = FindSlot(kind);
                if (s == null) { s = Join(kind, null, i); if (s == null || s.car == i) return false; }   // gated: acts for the seated player
                if (CarFree(i, s)) { s.car = i; NetPicks(s); Assign(); Sfx.Play(Sfx.Click, 0.3f); }
                return false;
            }
        for (int i = 0; i < 7; i++)
            if (Hit(robotTiles[i], pos))
            {
                Slot s = FindSlot(kind);
                if (s == null) s = Join(kind, null);
                if (s != null && RobotFree(i, s)) { s.robot = i; NetPicks(s); Assign(); Sfx.Play(Sfx.Click, 0.3f); }
                return false;
            }
        return false;
    }

    // ---------------- track choice ----------------
    void PickTrack(int dir)
    {
        int cur = pendingTrack >= 0 ? pendingTrack : Track.Id;
        pendingTrack = ((cur + dir) % Track.Count + Track.Count) % Track.Count;
        if (pendingTrack == Track.Id) pendingTrack = -1;
        pendingT = 0.45f;          // wait for the player to stop flicking through before rebuilding the world
        trackPop = 1f;
        Sfx.Play(Sfx.Click, 0.4f);
    }

    void RebuildTrack(int id)
    {
        Bootstrap.BuildWorld(id);
        if (mapTex != null) Destroy(mapTex);
        mapTex = Track.MiniMapTex(256);
        foreach (var s in slots) if (s.hud != null) s.hud.SetMap(mapTex);
        Assign();
        PlaceGrid();
        PlayerPrefs.SetInt("lb.track", Track.Id);
        PlayerPrefs.Save();
        Vector2 mid = (Track.Min + Track.Max) * 0.5f;
        Look.RefreshIsland(new Vector3(mid.x, 0f, mid.y));
        if (trackThumb != null) trackThumb.texture = null;    // lambo7: re-render the track card preview
        Sfx.Play(Sfx.ItemGet, 0.5f);
    }

    // ---------------- race flow ----------------
    void StartRace() { StartRace(false); }
    void StartRace(bool fromHost)
    {
        if (slots.Count == 0) return;
        if (LBNet.On && LBNet.I.IsGuest && !fromHost) { NetToast("The host starts the race"); return; }
        hostFinishCount = 0;
        if (pendingTrack >= 0) { int id = pendingTrack; pendingTrack = -1; RebuildTrack(id); }
        Assign();
        ApplyDriveModes();     // u6g
        PlaceGrid();
        if (LBNet.On && LBNet.I.IsHost) LBNet.I.HostStart(Track.Id);     // lambo8: everybody in the room starts now
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
        LBDisplay.Lobby(false);   // lambo7: racing density + 2x MSAA (governor may lighten it)
        foreach (var st in stands) st.Tick(0f, false);
        foreach (var s in slots) MakeView(s);
        ApplyLayout();
        // demo screenshots: &lbgap=K puts P1 150 m before gap K (0-based) of a generated track
        string u = Application.absoluteURL ?? "";
        int gi = u.IndexOf("lbgap=");
        if (Kart.Autopilot && gi >= 0 && gi + 6 < u.Length && char.IsDigit(u[gi + 6]) && Track.Gaps.Count > 0 && slots.Count > 0)
        {
            var g = Track.Gaps[Mathf.Clamp(u[gi + 6] - '0', 0, Track.Gaps.Count - 1)];
            float s0 = Track.Wrap(g.a - 150f);
            Vector3 p = Track.PointAt(s0, 0f); float y; Vector3 nn;
            Track.Ground(p, out y, out nn);
            Vector3 t = Track.TangentAt(s0);
            slots[0].kart.Place(new Vector3(p.x, y, p.z), Mathf.Atan2(t.x, t.z) * Mathf.Rad2Deg);
            slots[0].rig.Reset();
            Debug.Log("Demo: P1 placed before " + g.name);
        }
        Sfx.Play(Sfx.Click, 0.8f);
        WarmBegin();     // u6g: loading overlay until the race view is on screen (starts the music, then the countdown)
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
        LastViews = views;
        Debug.Log("Layout: " + n + " players");
        SeatLog("layout " + n + " player(s), " + views + " view(s) | " + SeatList());
    }

    public void OnFinish(Kart k)
    {
        if (LBNet.On && LBNet.I.IsHost) { hostFinishCount = Mathf.Max(hostFinishCount, finishCount) + 1; finishCount = hostFinishCount; k.hostPlaced = true; LBNet.I.HostFinish(k.id, finishCount, k.finishTime); }
        else finishCount++;
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
                if (a.netPuppet && b.netPuppet) continue;     // lambo8: both posed by the network
                Vector3 pa = a.transform.position, pb = b.transform.position;
                if (Mathf.Abs(pa.y - pb.y) > 1.6f) continue;
                Vector3 d = Track.Flat(pb - pa);
                float dist = d.magnitude;
                const float R = 2.5f;
                if (dist >= R || dist < 0.001f) continue;
                Vector3 n = d / dist;
                float wa = a.netPuppet ? 1000f : a.spec.weight, wb = b.netPuppet ? 1000f : b.spec.weight;
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
        SeatsUpdate();   // lambo7: ghost / mirrored pad guard + one controller = one view, before anybody reads input
        Track.Tick(Time.time);
        touch.active = (state == State.Countdown || state == State.Race) && FindSlot(InputKind.Touch) != null && !paused;
        { Slot ts = FindSlot(InputKind.Touch); touch.jumpOn = ts != null && ts.kart != null && ts.kart.IsRoadster; }     // u6i JUMP button
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
        if (p) pauseText.text = "<size=48><color=#7dff8a>PAUSED</color></size>\n\n" + HelpLines.Replace(" · Start pause", "") + "\n" + ModeHelp + "\n" + RoadsterHelp + DriveModesNow() +
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
        if (countdown && !WarmTick())
            centre = "";     // u6g: the LOADING TRACK overlay covers the view; the countdown waits
        else if (countdown)
        {
            countdownT -= dt;
            int num = Mathf.CeilToInt(countdownT);
            if (num > 3) centre = "<size=44><color=#ffd84a>" + Track.Name.ToUpper() + "</color></size>\n<size=80>READY?</size>";
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
            if (slots.Count <= 2 && s.cam != null) FX.Ambient(s.cam.transform.position, s.cam.transform.forward, Track.Theme, dt / slots.Count);
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
        sb.Append("<size=46><color=#7dff8a>RACE RESULTS</color></size>  <size=24>" + Track.Name + "</size>\n\n");
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
        if (lobby) { if (LBNet.On && LBNet.I.IsGuest) LBNet.I.Leave("You left the room"); ToLobby(); }
        else if (again) StartRace();
    }

    void ToLobby()
    {
        if (LBNet.On && LBNet.I.IsHost) LBNet.I.HostBackToLobby();
        state = State.Lobby;
        WarmCancel();
        LBDisplay.Lobby(true);
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
