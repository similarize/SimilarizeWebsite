using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum InputKind { Gamepad, Keyboard, Touch }

public class Slot
{
    public InputKind kind;
    public Gamepad pad;
    public int index;
    public Soldier soldier;
    public Camera cam;
    public Hud hud;
    public Transform viewModel;
    public float joinTime;
    public int spectate;
    public int critter;   // index into Critters.All (used in Critters mode)
}

// Lobby, split-screen, rounds (best of 3), input routing.
[DefaultExecutionOrder(-50)]
public class Game : MonoBehaviour
{
    public static Game I;
    public enum State { Lobby, Countdown, Playing, RoundOver, MatchOver }
    public State state = State.Lobby;

    public readonly List<Slot> slots = new List<Slot>();
    public readonly List<Soldier> soldiers = new List<Soldier>();
    public const int TotalPlayers = 8;
    public const int WinsNeeded = 2;
    public const float RoundLength = 150f;

    public int round;
    public int[] wins = new int[TotalPlayers];
    float stateT, roundClock, autoStartT = -1f, orbit;
    Soldier roundWinner, matchWinner;
    bool mobileAutoJoined;
    int lastBeep = -1;

    // Figures: false = Soldiers (X-Bot), true = Critters
    public static bool critterMode;
    float lastFigureToggle = -10f;
    Image figImg;
    Text figText;
    readonly Image[] arrowL = new Image[4], arrowR = new Image[4], swatches = new Image[4];

    readonly HashSet<int> ghosts = new HashSet<int>();
    readonly Dictionary<int, float> southTimes = new Dictionary<int, float>();
    readonly List<string> feedLines = new List<string>();
    readonly List<float> feedTimes = new List<float>();

    Camera overview;
    Canvas lobbyCanvas;
    Text lobbyStatus, lobbyHint;
    readonly Text[] slotTexts = new Text[4];
    readonly Image[] slotPanels = new Image[4];
    TouchControls touch;

    public static readonly Color[] Colors =
    {
        new Color(0.95f, 0.25f, 0.25f), new Color(0.25f, 0.5f, 1f), new Color(0.3f, 0.85f, 0.3f), new Color(1f, 0.85f, 0.15f),
        new Color(0.7f, 0.35f, 0.95f), new Color(1f, 0.55f, 0.1f), new Color(0.2f, 0.85f, 0.9f), new Color(1f, 0.45f, 0.75f)
    };
    static readonly string[] BotNames = { "Ace", "Bolt", "Comet", "Dash", "Echo", "Fizz", "Gizmo", "Hopper" };

    void Awake()
    {
        I = this;
        var ov = new GameObject("OverviewCam");
        overview = ov.AddComponent<Camera>();
        overview.depth = -10;
        overview.farClipPlane = 600f;
        overview.fieldOfView = 55f;
        touch = gameObject.AddComponent<TouchControls>();
        BuildLobbyUI();
        Look.ApplyViews(1, new[] { overview });
        string url = "";
        try { url = Application.absoluteURL ?? ""; } catch { }
        Demo = url.Contains("bbdemo");
        if (Demo)
        {
            int k = url.IndexOf("bbshot=");
            if (k >= 0) { demoShot = url.Substring(k + 7); int e = demoShot.IndexOfAny(new[] { '&', '#' }); if (e >= 0) demoShot = demoShot.Substring(0, e); }
            if (url.Contains("bbcrit=1")) critterMode = true;
            demoTouch = url.Contains("bbtouch=1");
            TouchControls.DemoPress = url.Contains("bbpress=1");
            int nk = url.IndexOf("bbn=");
            if (nk >= 0 && nk + 4 < url.Length) int.TryParse(url.Substring(nk + 4, 1), out demoN);
            int tk = url.IndexOf("bbtarget=");
            if (tk >= 0 && tk + 9 < url.Length) int.TryParse(url.Substring(tk + 9, 1), out demoTarget);
            Debug.Log("Balloon Blast: demo mode, shot " + demoShot + (critterMode ? ", critters" : ""));
        }
#if ENABLE_INPUT_SYSTEM
        Debug.Log("Balloon Blast: ENABLE_INPUT_SYSTEM defined");
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        Debug.Log("Balloon Blast: ENABLE_LEGACY_INPUT_MANAGER defined");
#endif
    }

    // ---------------- lobby UI ----------------
    void BuildLobbyUI()
    {
        lobbyCanvas = UIK.MakeCanvas("Lobby", null, 100, true);
        Transform r = lobbyCanvas.transform;
        lobbyBg = UIK.Img(r, null, new Color(0.05f, 0.08f, 0.12f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180, 640));
        lobbyBg.raycastTarget = false;
        lobbyTitle = UIK.Label(r, "BALLOON BLAST AIRSOFT", 64, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(1100, 90), new Color(1f, 0.85f, 0.2f));
        lobbyDesc = UIK.Label(r, "Pop everyone else's balloons! 3 balloons each - lose them all and you're out.\nLast one standing wins the round. Best of 3. 8 players: empty seats are bots.", 24, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 182), new Vector2(1100, 70), Color.white);
        figImg = UIK.Img(r, null, new Color(0f, 0f, 0f, 0.45f), new Vector2(0.5f, 0.5f), new Vector2(0, 126), new Vector2(640, 46));
        figText = UIK.Label(figImg.transform, "", 24, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(630, 44), Color.white);
        for (int i = 0; i < 4; i++)
        {
            Vector2 p = new Vector2(-405 + i * 270, 22);
            slotPanels[i] = UIK.Img(r, null, new Color(1, 1, 1, 0.12f), new Vector2(0.5f, 0.5f), p, new Vector2(250, 150));
            slotTexts[i] = UIK.Label(r, "", 22, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), p, new Vector2(196, 140), Color.white);
            swatches[i] = UIK.Img(r, null, Color.white, new Vector2(0.5f, 0.5f), p + new Vector2(0, -64), new Vector2(110, 9));
            arrowL[i] = UIK.Img(r, null, new Color(0f, 0f, 0f, 0.4f), new Vector2(0.5f, 0.5f), p + new Vector2(-104, 0), new Vector2(38, 70));
            UIK.Label(arrowL[i].transform, "<", 34, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(38, 60), Color.white);
            arrowR[i] = UIK.Img(r, null, new Color(0f, 0f, 0f, 0.4f), new Vector2(0.5f, 0.5f), p + new Vector2(104, 0), new Vector2(38, 70));
            UIK.Label(arrowR[i].transform, ">", 34, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(38, 60), Color.white);
        }
        lobbyStatus = UIK.Label(r, "", 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -90), new Vector2(1100, 60), new Color(0.6f, 1f, 0.6f));
        creditsBtn = UIK.Img(r, null, new Color(0f, 0f, 0f, 0.5f), new Vector2(0f, 1f), new Vector2(84, -26), new Vector2(150, 34));
        var cl = UIK.Label(creditsBtn.transform, "CREDITS (C)", 17, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 34), new Color(0.8f, 0.95f, 1f));
        UIK.Stretch(cl.rectTransform);
        lobbyHint = UIK.Label(r, "Gamepad: L-stick move | R-stick look | RT fire | LT aim | A jump | X reload\nKeyboard: WASD | mouse look | click fire | right-click aim | Space jump | R reload\nTouch: left stick | drag right side to look | FIRE / ADS / JUMP / RELOAD\nFigures: Y / F / tap the FIGURES bar.  Critters: pick with D-pad / Left-Right arrows / tap < >.  Sound: M / SOUND button", 20, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -208), new Vector2(1150, 130), new Color(1, 1, 1, 0.85f));
    }

    void BuildCredits()
    {
        Transform r = lobbyCanvas.transform;
        creditsPanel = UIK.Img(r, null, new Color(0.02f, 0.04f, 0.07f, 0.95f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180, 640));
        var ct = creditsText = UIK.Label(creditsPanel.transform, CreditsText, 18, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1120, 610), Color.white);
        ct.supportRichText = true;
        ct.horizontalOverflow = HorizontalWrapMode.Wrap;
        UIK.Stretch(ct.rectTransform);
        ct.rectTransform.offsetMin = new Vector2(30, 14); ct.rectTransform.offsetMax = new Vector2(-30, -14);
        creditsPanel.gameObject.SetActive(false);
        lobbyLaid = -1;
    }
    Text creditsText;

    Image creditsBtn, creditsPanel, lobbyBg;
    Text lobbyTitle, lobbyDesc;
    int lobbyLaid = -1;

    static void Pos(Graphic g, Vector2 pos, Vector2 size) { g.rectTransform.anchoredPosition = pos; g.rectTransform.sizeDelta = size; }

    // Landscape = the original 1280x720 design. Portrait (phone held upright) = a 720-wide column: title on two lines,
    // FIGURES bar, the four seats in a 2x2 grid, status and the controls help below, everything inside the screen.
    void LayoutLobby()
    {
        int want = Page.Portrait ? 1 : 0;
        if (want == lobbyLaid) return;
        lobbyLaid = want;
        CanvasScaler sc = lobbyCanvas.GetComponent<CanvasScaler>();
        bool p = want == 1;
        sc.referenceResolution = p ? new Vector2(720, 1280) : new Vector2(1280, 720);
        sc.screenMatchMode = p ? CanvasScaler.ScreenMatchMode.Expand : CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.6f;
        if (p)
        {
            Pos(lobbyBg, new Vector2(0, -10), new Vector2(700, 1190));
            Pos(lobbyTitle, new Vector2(0, 470), new Vector2(680, 150)); lobbyTitle.fontSize = 56; lobbyTitle.text = "BALLOON BLAST\nAIRSOFT";
            Pos(lobbyDesc, new Vector2(0, 350), new Vector2(660, 120)); lobbyDesc.fontSize = 22;
            Pos(figImg, new Vector2(0, 262), new Vector2(660, 50)); Pos(figText, Vector2.zero, new Vector2(650, 48)); figText.fontSize = 22;
            for (int i = 0; i < 4; i++)
            {
                Vector2 c = new Vector2(i % 2 == 0 ? -168 : 168, i < 2 ? 140 : -36);
                SeatPos(i, c, new Vector2(320, 160), 140);
            }
            Pos(lobbyStatus, new Vector2(0, -175), new Vector2(660, 100)); lobbyStatus.fontSize = 26;
            Pos(lobbyHint, new Vector2(0, -400), new Vector2(670, 330)); lobbyHint.fontSize = 19;
        }
        else
        {
            Pos(lobbyBg, Vector2.zero, new Vector2(1180, 640));
            Pos(lobbyTitle, new Vector2(0, 250), new Vector2(1100, 90)); lobbyTitle.fontSize = 64; lobbyTitle.text = "BALLOON BLAST AIRSOFT";
            Pos(lobbyDesc, new Vector2(0, 182), new Vector2(1100, 70)); lobbyDesc.fontSize = 24;
            Pos(figImg, new Vector2(0, 126), new Vector2(640, 46)); Pos(figText, Vector2.zero, new Vector2(630, 44)); figText.fontSize = 24;
            for (int i = 0; i < 4; i++) SeatPos(i, new Vector2(-405 + i * 270, 22), new Vector2(250, 150), 104);
            Pos(lobbyStatus, new Vector2(0, -90), new Vector2(1100, 60)); lobbyStatus.fontSize = 30;
            Pos(lobbyHint, new Vector2(0, -208), new Vector2(1150, 130)); lobbyHint.fontSize = 20;
        }
        if (creditsPanel != null)
        {
            Pos(creditsPanel, Vector2.zero, p ? new Vector2(700, 1300) : new Vector2(1180, 640));
            creditsText.fontSize = p ? 19 : 18;
        }
    }

    void SeatPos(int i, Vector2 c, Vector2 panel, float arrowX)
    {
        Pos(slotPanels[i], c, panel);
        Pos(slotTexts[i], c, new Vector2(196, 140));
        Pos(swatches[i], c + new Vector2(0, -64), new Vector2(110, 9));
        Pos(arrowL[i], c + new Vector2(-arrowX, 0), new Vector2(38, 70));
        Pos(arrowR[i], c + new Vector2(arrowX, 0), new Vector2(38, 70));
    }
    const string CreditsText =
        "<size=30><color=#ffd84a><b>CREDITS</b></color></size>\n\n" +
        "<b>Trees, pines, bushes, rocks, grass, ferns, flowers</b>: Stylized Nature MegaKit by Quaternius (quaternius.com), CC0.\n" +
        "<b>Sky</b> (Kloofendal 48d Partly Cloudy Pure Sky) and <b>grass / dirt / wood / barn / metal roof textures</b>: Poly Haven\n" +
        "   (polyhaven.com), CC0.\n" +
        "<b>Soldiers</b>: Mixamo X Bot via the Babylon.js asset library (assets.babylonjs.com, loaded at runtime).\n" +
        "<b>Airsoft rifle, balloons, frogs / cats / dogs, fort, barn, hay, crates, log</b>: modelled for this game (no logos).\n" +
        "<b>Music</b>: \"Happy Clappy Loop\" by OwlishMedia and \"Happy Beat\" by burabotti (OpenGameArt), CC0.\n" +
        "<b>Sounds</b>: Kenney (kenney.nl) Impact, RPG, Interface + Jingles packs; cheers and countryside ambience from BigSoundBank\n" +
        "   (DenisChardonnet, Joseph Sardin); applause \"Well Done\" by qubodup and \"Cheers\" by Nocturnal_Vanguard (OpenGameArt). All CC0.\n" +
        "Shots, balloon pops and jingles for countdown / GO are synthesised in the game.\n\n" +
        "Full licence notes: similarize.com/games/balloon-blast-unity/LICENSES.txt\n\n" +
        "<color=#9fd8ff>Press C, Esc or tap to close</color>";

    void CreditsToggle(bool? on = null)
    {
        if (creditsPanel == null) BuildCredits();
        bool v = on ?? !creditsPanel.gameObject.activeSelf;
        if (v != creditsPanel.gameObject.activeSelf) Sfx.UiPanel(v);
        creditsPanel.gameObject.SetActive(v);
        creditsPanel.transform.SetAsLastSibling();
    }

    void RefreshLobbyUI()
    {
        for (int i = 0; i < 4; i++)
        {
            if (i < slots.Count)
            {
                Slot s = slots[i];
                string dev = s.kind == InputKind.Gamepad ? "Gamepad" : s.kind == InputKind.Keyboard ? "Keyboard + Mouse" : "Touch";
                slotPanels[i].color = new Color(Colors[i].r, Colors[i].g, Colors[i].b, 0.55f);
                if (critterMode)
                {
                    CritterDef d = Critters.All[s.critter];
                    slotTexts[i].text = "P" + (i + 1) + "  " + dev + "\n<size=26>" + d.name + "</size>\n" + d.KindName + "\nREADY";
                    swatches[i].color = d.main;
                }
                else slotTexts[i].text = "P" + (i + 1) + "\n" + dev + "\nREADY";
            }
            else
            {
                slotTexts[i].text = "P" + (i + 1) + "\nPress A / Enter\nto join";
                slotPanels[i].color = new Color(1, 1, 1, 0.12f);
            }
            bool pick = critterMode && i < slots.Count;
            swatches[i].enabled = pick;
            arrowL[i].gameObject.SetActive(pick);
            arrowR[i].gameObject.SetActive(pick);
        }
        figText.text = critterMode
            ? "FIGURES:   Soldiers   <color=#ffd84a>[ CRITTERS ]</color>     <size=18>(Y / F / tap)</size>"
            : "FIGURES:   <color=#ffd84a>[ SOLDIERS ]</color>   Critters     <size=18>(Y / F / tap)</size>";
        string model = critterMode || ModelLoader.Ready ? "" : ModelLoader.Failed ? "  (using simple soldiers)" : "  (loading soldiers...)";
        if (slots.Count == 0) lobbyStatus.text = "Press A on a gamepad or Enter on the keyboard to join" + model;
        else
        {
            string auto = autoStartT > 0 ? "  - starting in " + Mathf.CeilToInt(autoStartT) : "";
            lobbyStatus.text = "Joined players: press A / Enter / tap again to START. B / Esc leaves." + auto + model;
        }
    }

    // ---------------- main loop ----------------
    void Update()
    {
        float dt = Time.deltaTime;
        Slot tslot = FindSlot(InputKind.Touch);
        touch.active = tslot != null && state != State.Lobby;
        if (tslot != null && tslot.cam != null) touch.view = tslot.cam.pixelRect;
        else touch.view = new Rect(0, 0, Screen.width, Screen.height);

        switch (state)
        {
            case State.Lobby: UpdateLobby(dt); break;
            case State.Countdown:
                stateT -= dt;
                {
                    int c = Mathf.CeilToInt(stateT);
                    if (c != lastBeep && c >= 1 && c <= 3) Sfx.CountBeep();
                    lastBeep = c;
                }
                if (stateT <= 0f) { state = State.Playing; Sfx.RoundGo(); }
                break;
            case State.Playing:
                roundClock -= dt;
                CheckRoundEnd();
                break;
            case State.RoundOver:
                stateT -= dt;
                if (stateT <= 0f) StartRound();
                break;
            case State.MatchOver:
                stateT -= dt;
                if (stateT <= 0f || AnyStartPressed()) EnterLobby();
                break;
        }

        if (state != State.Lobby)
            foreach (var sl in slots) ReadInput(sl, dt);

        for (int i = feedTimes.Count - 1; i >= 0; i--)
            if (Time.time - feedTimes[i] > 6f) { feedTimes.RemoveAt(i); feedLines.RemoveAt(i); }
    }

    bool AnyStartPressed()
    {
        if (stateT > 6f) return false;
        if (Kb.EnterDown()) return true;
        foreach (var sl in slots) if (sl.pad != null && sl.pad.added && sl.pad.startButton.wasPressedThisFrame) return true;
        return false;
    }

    void UpdateLobby(float dt)
    {
        lobbyCanvas.enabled = true;
        Page.Refresh();
        LayoutLobby();
        overview.rect = new Rect(0, 0, 1, 1);
        overview.enabled = true;

        if (Kb.CDown()) CreditsToggle();
        if (creditsPanel != null && creditsPanel.gameObject.activeSelf)
        {
            if (Kb.EscDown() || Kb.TouchesBegan().Count > 0 || Kb.MouseLeftDown()) CreditsToggle(false);
            RefreshLobbyUI();
            return;
        }
        if (Kb.MouseLeftDown() && Cursor.lockState != CursorLockMode.Locked && Hit(creditsBtn, Kb.MousePos())) { CreditsToggle(true); return; }
        if (Demo && slots.Count == 0 && Time.timeSinceLevelLoad > 4.5f)
        {
            Join(demoTouch ? InputKind.Touch : InputKind.Keyboard, null);
            for (int k = 1; k < demoN; k++) Join(k == 1 && !demoTouch ? InputKind.Touch : InputKind.Keyboard, null);
            Cursor.lockState = CursorLockMode.None;
        }
        if (Demo && demoShot == "lobby") autoStartT = -1f;
        else if (Demo && slots.Count > 0 && Time.unscaledTime - slots[0].joinTime > 1f)
        {
            for (int k = slots.Count; k < demoN && k < 4; k++) Join(InputKind.Keyboard, null);
            Cursor.lockState = CursorLockMode.None;
            StartMatch(); return;
        }

        // keyboard
        if (Kb.EnterDown())
        {
            Slot ks = FindSlot(InputKind.Keyboard);
            if (ks == null) Join(InputKind.Keyboard, null);
            else if (Time.unscaledTime - ks.joinTime > 0.3f) { StartMatch(); return; }
        }
        if (Kb.EscDown()) Leave(FindSlot(InputKind.Keyboard));
        if (Kb.FDown()) ToggleFigures();
        if (critterMode)
        {
            Slot kslot = FindSlot(InputKind.Keyboard);
            if (kslot != null)
            {
                if (Kb.LeftDown()) CycleCritter(kslot, -1);
                if (Kb.RightDown()) CycleCritter(kslot, 1);
            }
        }

        // gamepads (with ghost/duplicate filtering)
        float now = Time.unscaledTime;
        foreach (Gamepad pad in Gamepad.all)
        {
            if (pad.buttonEast.wasPressedThisFrame && !ghosts.Contains(pad.deviceId)) Leave(FindPad(pad));
            if (pad.buttonNorth.wasPressedThisFrame && !ghosts.Contains(pad.deviceId)) ToggleFigures();
            if (critterMode)
            {
                Slot ps = FindPad(pad);
                if (ps != null)
                {
                    if (pad.dpad.left.wasPressedThisFrame) CycleCritter(ps, -1);
                    if (pad.dpad.right.wasPressedThisFrame) CycleCritter(ps, 1);
                }
            }
            if (!pad.buttonSouth.wasPressedThisFrame && !pad.startButton.wasPressedThisFrame) continue;
            bool coincident = false;
            foreach (Gamepad other in Gamepad.all)
            {
                if (other == pad) continue;
                float t;
                if (southTimes.TryGetValue(other.deviceId, out t) && now - t < 0.15f && FindPad(other) != null) coincident = true;
            }
            southTimes[pad.deviceId] = now;
            Slot existing = FindPad(pad);
            if (coincident && existing == null) { ghosts.Add(pad.deviceId); continue; }
            ghosts.Remove(pad.deviceId);
            if (existing == null) Join(InputKind.Gamepad, pad);
            else if (now - existing.joinTime > 0.4f) { StartMatch(); return; }
        }

        // touch: phones auto-join; a tap joins / starts
        if (Application.isMobilePlatform && !mobileAutoJoined && FindSlot(InputKind.Touch) == null)
        {
            mobileAutoJoined = true;
            Join(InputKind.Touch, null);
            autoStartT = 5f;
        }
        if (LobbyTaps())
        {
            Slot ts = FindSlot(InputKind.Touch);
            if (ts == null) Join(InputKind.Touch, null);
            else if (now - ts.joinTime > 0.6f) { StartMatch(); return; }
        }

        if (slots.Count > 0 && autoStartT > 0f)
        {
            autoStartT -= dt;
            if (autoStartT <= 0f) { StartMatch(); return; }
        }

        orbit += dt * 4f;
        float a = orbit * Mathf.Deg2Rad;
        overview.transform.position = World.center + new Vector3(Mathf.Sin(a) * 120f, 45f, Mathf.Cos(a) * 120f);
        overview.transform.LookAt(World.center + Vector3.up * 2f);
        RefreshLobbyUI();
    }

    // Touch in the lobby: the FIGURES bar toggles, the < > arrows pick critters, any other tap joins / starts.
    bool LobbyTaps()
    {
        bool generic = false;
        foreach (Vector2 pos in Kb.TouchesBegan())
        {
            if (Sfx.ButtonHit(pos)) continue;   // SOUND button
            if (Hit(creditsBtn, pos)) { CreditsToggle(true); continue; }
            if (Hit(figImg, pos)) { ToggleFigures(); continue; }
            bool used = false;
            if (critterMode)
                for (int i = 0; i < slots.Count && i < 4; i++)
                {
                    if (Hit(arrowL[i], pos)) { CycleCritter(slots[i], -1); used = true; break; }
                    if (Hit(arrowR[i], pos)) { CycleCritter(slots[i], 1); used = true; break; }
                }
            if (!used) generic = true;
        }
        return generic;
    }

    static bool Hit(Image img, Vector2 screenPos)
    {
        return img != null && img.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(img.rectTransform, screenPos, null);
    }

    void ToggleFigures()
    {
        float now = Time.unscaledTime;
        if (now - lastFigureToggle < 0.3f) return;   // duplicate/ghost pads report the same press
        lastFigureToggle = now;
        critterMode = !critterMode;
        Sfx.UiSwitch();
        BumpAutoStart();
        Debug.Log("Figures: " + (critterMode ? "Critters" : "Soldiers"));
    }

    void BumpAutoStart()
    {
        if (slots.Count > 0 && autoStartT > 0f && autoStartT < 10f) autoStartT = 10f;
    }

    bool CritterTaken(int c, Slot except)
    {
        foreach (var o in slots) if (o != except && o.critter == c) return true;
        return false;
    }

    int FirstFreeCritter(Slot except)
    {
        for (int c = 0; c < Critters.Count; c++) if (!CritterTaken(c, except)) return c;
        return 0;
    }

    void CycleCritter(Slot sl, int dir)
    {
        int n = Critters.Count;
        for (int k = 1; k <= n; k++)
        {
            int c = ((sl.critter + dir * k) % n + n) % n;
            if (!CritterTaken(c, sl)) { sl.critter = c; break; }
        }
        Sfx.UiSelect();
        BumpAutoStart();
    }

    Slot FindSlot(InputKind k)
    {
        foreach (var s in slots) if (s.kind == k) return s;
        return null;
    }

    Slot FindPad(Gamepad p)
    {
        foreach (var s in slots) if (s.kind == InputKind.Gamepad && s.pad == p) return s;
        return null;
    }

    void Join(InputKind kind, Gamepad pad)
    {
        if (slots.Count >= 4) return;
        var s = new Slot { kind = kind, pad = pad, index = slots.Count, joinTime = Time.unscaledTime };
        s.critter = FirstFreeCritter(null);
        slots.Add(s);
        if (kind == InputKind.Keyboard) Cursor.lockState = CursorLockMode.Locked;
        autoStartT = (kind == InputKind.Touch && slots.Count == 1) ? 5f : 20f;
        Sfx.UiJoin();
        Debug.Log("Joined P" + slots.Count + " via " + kind + (pad != null ? " " + pad.displayName + " #" + pad.deviceId : ""));
    }

    void Leave(Slot s)
    {
        if (s == null) return;
        Sfx.UiBack();
        slots.Remove(s);
        for (int i = 0; i < slots.Count; i++) slots[i].index = i;
        if (slots.Count == 0) autoStartT = -1f;
    }

    // ---------------- match / rounds ----------------
    void ClearMatch()
    {
        if (BBs.I != null) BBs.I.ClearAll();
        foreach (var sl in slots)
        {
            if (sl.hud != null) sl.hud.Destroy();
            sl.hud = null;
            if (sl.cam != null) Destroy(sl.cam.gameObject);
            sl.cam = null; sl.soldier = null; sl.viewModel = null;
        }
        foreach (var s in soldiers) if (s != null) Destroy(s.gameObject);
        soldiers.Clear();
    }

    void EnterLobby()
    {
        ClearMatch();
        state = State.Lobby;
        Sfx.SetScene(false);
        autoStartT = slots.Count > 0 ? 20f : -1f;
        lobbyCanvas.enabled = true;
        Look.ApplyViews(1, new[] { overview });
    }

    void StartMatch()
    {
        if (slots.Count == 0) return;
        ClearMatch();
        Sfx.UiStart();
        Sfx.SetScene(true);
        lobbyCanvas.enabled = false;
        autoStartT = -1f;
        for (int i = 0; i < TotalPlayers; i++) wins[i] = 0;
        round = 0;
        matchWinner = null;
        feedLines.Clear(); feedTimes.Clear();

        // Critters mode: humans keep their picks, bots get different random critters
        var botCritters = new List<int>();
        if (critterMode)
        {
            for (int c = 0; c < Critters.Count; c++) if (!CritterTaken(c, null)) botCritters.Add(c);
            for (int k = botCritters.Count - 1; k > 0; k--) { int j = Random.Range(0, k + 1); int t = botCritters[k]; botCritters[k] = botCritters[j]; botCritters[j] = t; }
        }

        int bot = 0;
        for (int i = 0; i < TotalPlayers; i++)
        {
            bool human = i < slots.Count;
            var go = new GameObject("Soldier" + i);
            var s = go.AddComponent<Soldier>();
            int critter = -1;
            string nick;
            if (critterMode)
            {
                critter = human ? slots[i].critter : botCritters[bot++ % botCritters.Count];
                nick = Critters.All[critter].name;
            }
            else nick = human ? "P" + (i + 1) : BotNames[bot++ % BotNames.Length];
            s.Build(i, nick, Colors[i], human, human ? 20 + i : 0, critter);
            soldiers.Add(s);
            if (human)
            {
                Slot sl = slots[i];
                sl.soldier = s;
                s.slot = sl;
                SetupCamera(sl);
                if (Demo) go.AddComponent<BotBrain>();   // demo: P1 plays itself
            }
            else go.AddComponent<BotBrain>();
        }
        LayoutCameras();
        StartRound();
    }

    void SetupCamera(Slot sl)
    {
        var cg = new GameObject("Cam P" + (sl.index + 1));
        cg.transform.SetParent(sl.soldier.eye, false);
        Camera cam = cg.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 450f;
        cam.depth = sl.index;
        int mask = ~0;
        for (int i = 0; i < 4; i++) { mask &= ~(1 << (24 + i)); }
        mask &= ~(1 << (20 + sl.index));
        mask |= 1 << (24 + sl.index);
        cam.cullingMask = mask;
        sl.cam = cam;

        // first-person airsoft rifle (mesh + gloved hands; sleeves in the seat colour, or paws / fur for critters)
        var vm = new GameObject("ViewModel").transform;
        vm.SetParent(cg.transform, false);
        Color glove = new Color(0.15f, 0.16f, 0.18f), sleeve = Color.Lerp(sl.soldier.color, new Color(0.3f, 0.32f, 0.3f), 0.35f);
        if (sl.soldier.critter != null)
        {
            CritterDef cd = sl.soldier.critter.def;
            glove = cd.look == CritterLook.Socks ? cd.accent : cd.kind == CritterKind.Frog ? Color.Lerp(cd.main, Color.black, 0.14f) : cd.main;
            sleeve = cd.main;
        }
        Transform vmMuzzle;
        Gear.Rifle(vm, Vector3.zero, 0.9f, sl.soldier.color, true, glove, sleeve, out vmMuzzle);
        Mats.SetLayer(vm.gameObject, 24 + sl.index);
        Mats.NoShadows(vm.gameObject);
        foreach (var rr in vm.GetComponentsInChildren<Renderer>()) rr.receiveShadows = false;
        sl.viewModel = vm;
        sl.soldier.viewMuzzle = vmMuzzle;

        string hudName = "P" + (sl.index + 1) + (sl.soldier.critter != null ? "  " + sl.soldier.nick : "");
        sl.hud = new Hud(cam, hudName, sl.soldier.color, 10 + sl.index);
        sl.hud.touchLayout = sl.kind == InputKind.Touch;
    }

    void LayoutCameras()
    {
        int n = slots.Count;
        for (int i = 0; i < n; i++)
        {
            Rect r;
            if (n == 1) r = new Rect(0, 0, 1, 1);
            else if (n == 2) r = i == 0 ? new Rect(0, 0.5f, 1, 0.5f) : new Rect(0, 0, 1, 0.5f);
            else r = new Rect((i % 2) * 0.5f, i < 2 ? 0.5f : 0f, 0.5f, 0.5f);
            if (slots[i].cam != null)
            {
                slots[i].cam.rect = r;
                slots[i].cam.fieldOfView = n == 2 ? 52f : 68f;
            }
        }
        overview.enabled = n == 3;
        overview.rect = new Rect(0.5f, 0f, 0.5f, 0.5f);
        var cams = new List<Camera>();
        foreach (var sl in slots) if (sl.cam != null) cams.Add(sl.cam);
        cams.Add(overview);
        Look.ApplyViews(n + (n == 3 ? 1 : 0), cams);
    }

    void StartRound()
    {
        round++;
        if (BBs.I != null) BBs.I.ClearAll();
        var order = new List<int>();
        for (int i = 0; i < World.spawns.Count; i++) order.Add(i);
        for (int i = order.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); int t = order[i]; order[i] = order[j]; order[j] = t; }
        for (int i = 0; i < soldiers.Count; i++)
        {
            Vector3 sp = World.spawns[order[i % order.Count]];
            Vector3 to = World.center - sp;
            float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            soldiers[i].ResetForRound(sp, yaw);
        }
        foreach (var sl in slots)
        {
            if (sl.cam != null)
            {
                sl.cam.transform.SetParent(sl.soldier.eye, false);
                sl.cam.transform.localPosition = Vector3.zero;
                sl.cam.transform.localRotation = Quaternion.identity;
            }
            sl.spectate = 0;
        }
        roundClock = RoundLength;
        roundWinner = null;
        state = State.Countdown;
        stateT = 3.5f;
        lastBeep = -1;
    }

    void CheckRoundEnd()
    {
        int alive = 0; Soldier last = null;
        foreach (var s in soldiers) if (s.alive) { alive++; last = s; }
        if (alive > 1 && roundClock > 0f) return;

        Soldier w = last;
        if (alive != 1)
        {
            int best = -1;
            foreach (var s in soldiers)
            {
                if (!s.alive) continue;
                int sc = s.BalloonsLeft * 100 + s.pops;
                if (sc > best) { best = sc; w = s; }
            }
        }
        if (w == null) w = soldiers[0];
        roundWinner = w;
        wins[w.id]++;
        if (wins[w.id] >= WinsNeeded) { matchWinner = w; state = State.MatchOver; stateT = 9f; }
        else { state = State.RoundOver; stateT = 4.5f; }
        Sfx.RoundEnd(w.human, state == State.MatchOver);
    }

    public void OnPop(Soldier by, Soldier victim)
    {
        string who = by != null ? by.nick : "Someone";
        AddFeed(who + " popped " + victim.nick + "'s balloon");
    }

    public void OnOut(Soldier victim, Soldier by)
    {
        AddFeed(victim.nick + " is OUT!");
        Sfx.OnOut(victim, by);
    }

    void AddFeed(string line)
    {
        feedLines.Add(line);
        feedTimes.Add(Time.time);
        while (feedLines.Count > 4) { feedLines.RemoveAt(0); feedTimes.RemoveAt(0); }
    }

    // ---------------- input ----------------
    static Vector2 Dead(Vector2 v, float dz)
    {
        float m = v.magnitude;
        if (m < dz) return Vector2.zero;
        return v.normalized * Mathf.Clamp01((m - dz) / (1f - dz));
    }

    void ReadInput(Slot sl, float dt)
    {
        Soldier s = sl.soldier;
        if (s == null) return;
        Vector2 mv = Vector2.zero, lk = Vector2.zero;
        bool fire = false, ads = false, jump = false, reload = false, next = false;

        switch (sl.kind)
        {
            case InputKind.Gamepad:
                {
                    Gamepad p = sl.pad;
                    if (p == null || !p.added) break;
                    mv = Dead(p.leftStick.ReadValue(), 0.18f);
                    Vector2 r = Dead(p.rightStick.ReadValue(), 0.12f);
                    float spd = Mathf.Lerp(170f, 75f, s.adsBlend);
                    lk = new Vector2(r.x, r.y * 0.75f) * r.magnitude * spd * dt;
                    fire = p.rightTrigger.ReadValue() > 0.35f || p.rightShoulder.isPressed;
                    ads = p.leftTrigger.ReadValue() > 0.35f || p.leftShoulder.isPressed;
                    jump = p.buttonSouth.wasPressedThisFrame;
                    reload = p.buttonWest.wasPressedThisFrame;
                    next = jump || p.rightShoulder.wasPressedThisFrame;
                    break;
                }
            case InputKind.Keyboard:
                {
                    mv = Kb.Move();
                    bool locked = Cursor.lockState == CursorLockMode.Locked;
                    if (Kb.MouseLeftDown() && !locked) Cursor.lockState = CursorLockMode.Locked;
                    lk = Kb.MouseDelta() * Mathf.Lerp(0.1f, 0.05f, s.adsBlend);
                    fire = locked && Kb.MouseLeft();
                    ads = Kb.MouseRight();
                    jump = Kb.JumpDown();
                    reload = Kb.ReloadDown();
                    next = jump;
                    break;
                }
            case InputKind.Touch:
                touch.Read(out mv, out lk, out fire, out ads, out jump, out reload);
                next = jump || Kb.AnyTouchBegan();
                break;
        }

        if (state != State.Playing) { mv = Vector2.zero; fire = false; jump = false; }
        s.inMove = mv;
        s.inLook = lk;
        s.inFire = fire;
        s.inAds = ads;
        if (jump) s.inJump = true;
        if (reload) s.inReload = true;
        if (!s.alive && next) sl.spectate++;
    }

    // ---------------- cameras & HUD ----------------
    void LateUpdate()
    {
        if (state == State.Lobby) return;
        float dt = Time.deltaTime;
        int alive = 0;
        foreach (var s in soldiers) if (s.alive) alive++;

        if (overview.enabled)
        {
            orbit += dt * 4f;
            float a = orbit * Mathf.Deg2Rad;
            overview.transform.position = World.center + new Vector3(Mathf.Sin(a) * 110f, 70f, Mathf.Cos(a) * 110f);
            overview.transform.LookAt(World.center);
        }

        int secs = Mathf.Max(0, Mathf.CeilToInt(roundClock));
        string clock = (secs / 60) + ":" + (secs % 60).ToString("00");
        string feed = string.Join("\n", feedLines.ToArray());

        foreach (var sl in slots)
        {
            Soldier s = sl.soldier;
            if (s == null || sl.cam == null) continue;
            float baseFov = slots.Count == 2 ? 52f : 68f;
            sl.cam.fieldOfView = Mathf.Lerp(baseFov, baseFov * 0.55f, s.adsBlend);

            bool spectating = !s.alive && (state == State.Playing || state == State.RoundOver);
            Soldier target = null;
            if (spectating)
            {
                var living = new List<Soldier>();
                foreach (var o in soldiers) if (o.alive) living.Add(o);
                if (living.Count > 0) target = living[((sl.spectate % living.Count) + living.Count) % living.Count];
            }
            if (target != null)
            {
                if (sl.cam.transform.parent != null) sl.cam.transform.SetParent(null, true);
                Vector3 want = target.eye.position - target.transform.forward * 4.5f + Vector3.up * 1.6f;
                sl.cam.transform.position = Vector3.Lerp(sl.cam.transform.position, want, Mathf.Min(1f, dt * 4f));
                sl.cam.transform.rotation = Quaternion.Slerp(sl.cam.transform.rotation, Quaternion.LookRotation(target.eye.position + Vector3.up * 0.4f - sl.cam.transform.position), Mathf.Min(1f, dt * 6f));
            }
            if (sl.viewModel != null)
            {
                sl.viewModel.gameObject.SetActive(s.alive && sl.cam.transform.parent == s.eye);
                Vector3 hip = new Vector3(0.15f, -0.27f, 0.3f), aim = new Vector3(0f, -0.141f, 0.16f);
                float bob = Mathf.Clamp01(s.HSpeed / 5f) * (1f - s.adsBlend * 0.8f);
                float tt = Time.time * 9f;
                Vector3 sway = new Vector3(Mathf.Sin(tt * 0.5f) * 0.008f, Mathf.Abs(Mathf.Sin(tt * 0.5f)) * 0.008f, 0f) * bob;
                sl.viewModel.localPosition = Vector3.Lerp(hip, aim, s.adsBlend) + sway + new Vector3(0f, 0f, -0.035f * s.kick);
                sl.viewModel.localRotation = Quaternion.Euler(-2.5f * s.kick + Mathf.Lerp(-1.5f, 0f, s.adsBlend), Mathf.Lerp(-3.5f, 0f, s.adsBlend), 0f);
            }

            string top = "ROUND " + round + "  |  " + alive + " LEFT  |  " + clock + "\nWINS  " + WinsLine(s);
            string center = "";
            switch (state)
            {
                case State.Countdown: center = "ROUND " + round + "\n" + Mathf.CeilToInt(stateT); break;
                case State.Playing:
                    if (s.alive && roundClock > RoundLength - 0.9f) center = "<size=72>GO!</size>";
                    if (!s.alive) center = "YOU'RE OUT!\n<size=24>Spectating " + (target != null ? target.nick : "") + " - press JUMP to switch</size>";
                    break;
                case State.RoundOver:
                    center = roundWinner == s ? "YOU WIN ROUND " + round + "!" : roundWinner.nick + " wins round " + round;
                    break;
                case State.MatchOver:
                    center = matchWinner == s ? "YOU WIN THE MATCH!" : matchWinner.nick + " WINS THE MATCH!";
                    center += "\n<size=24>Press Enter / Start for the lobby</size>";
                    break;
            }
            if (sl.kind == InputKind.Keyboard && state == State.Playing && s.alive && Cursor.lockState != CursorLockMode.Locked && !Demo)
                center = "Click to aim with the mouse";
            sl.hud.Tick(s, top, center, feed, state == State.Playing, dt);
        }
        if (Demo) DemoCamera(dt);
    }

    // ---------------- demo / screenshot mode ----------------
    // ?bbdemo=1 in the page URL: joins keyboard P1 after ~5 s in the lobby, starts, P1 plays itself (BotBrain) and nobody
    // goes out (popped balloons refill). &bbshot=<scene> pins P1's camera: fp (first person), soldier, pop (close-up of
    // a bot whose balloons keep popping), field, fort, barn; &bbshot=tour cycles them every 9 s. &bbcrit=1 = Critters.
    // For work/webgl-probe/probe.py screenshots. Logs "BBDEMO scene <name>" whenever the view changes.
    public static bool Demo;
    static string demoShot = "tour";
    static bool demoTouch;      // &bbtouch=1: P1 joins as Touch (on-screen controls drawn) even on desktop
    static int demoN = 1;       // &bbn=2..4: extra (bot-driven) players -> split-screen
    static readonly string[] Tour = { "fp", "soldier", "pop", "field", "fort", "barn" };
    string demoScene = "";
    static int demoTarget = 1;   // &bbtarget=N: which soldier the soldier / pop shots look at
    float demoT, demoPopT;

    void DemoCamera(float dt)
    {
        if (slots.Count == 0 || slots[0].cam == null || soldiers.Count < 2) return;
        if (state == State.Countdown) return;
        demoT += dt;
        string want = demoShot;
        if (demoShot == "tour" || demoShot == "") want = Tour[Mathf.FloorToInt(demoT / 9f) % Tour.Length];
        else if (demoShot.Contains(",")) { string[] l = demoShot.Split(','); want = l[Mathf.FloorToInt(demoT / 9f) % l.Length]; }
        Camera cam = slots[0].cam;
        if (want != demoScene) { demoScene = want; demoPopT = 1f; Debug.Log("BBDEMO scene " + want + " t=" + Time.timeSinceLevelLoad.ToString("0.0")); }
        if (slots[0].hud != null && slots[0].hud.canvas != null) slots[0].hud.canvas.enabled = want == "fp";
        if (want == "fp")
        {
            if (cam.transform.parent != slots[0].soldier.eye)
            {
                cam.transform.SetParent(slots[0].soldier.eye, false);
                cam.transform.localPosition = Vector3.zero; cam.transform.localRotation = Quaternion.identity;
            }
            return;
        }
        if (cam.transform.parent != null) cam.transform.SetParent(null, true);
        cam.fieldOfView = 55f;
        Soldier t = soldiers[Mathf.Clamp(demoTarget, 1, soldiers.Count - 1)];
        Vector3 pos = cam.transform.position, look = World.center;
        switch (want)
        {
            case "soldier":
            case "pop":
                {
                    float dist = want == "pop" ? 2.6f : 3.6f;
                    Vector3 f = t.transform.forward;
                    pos = t.transform.position + f * dist + t.transform.right * (want == "pop" ? 0.6f : 1.2f) + Vector3.up * (want == "pop" ? 2.2f : 1.7f);
                    look = want == "pop" ? t.AimPoint() : t.transform.position + Vector3.up * 1.5f;
                    if (want == "pop")
                    {
                        demoPopT -= dt;
                        if (demoPopT <= 0f)
                        {
                            demoPopT = 1.5f;
                            foreach (var b in t.balloons) if (!b.popped) { t.PopBalloon(b, soldiers[0]); break; }
                        }
                    }
                    break;
                }
            case "field":
                {
                    Vector3 d = (World.BarnPos - World.FortPos); d.y = 0f; d = d.sqrMagnitude > 1f ? d.normalized : Vector3.forward;
                    pos = World.FortPos - d * 38f + Vector3.Cross(Vector3.up, d) * 14f + Vector3.up * 16f;
                    look = Vector3.Lerp(World.FortPos, World.BarnPos, 0.45f) + Vector3.up * 1f;
                    break;
                }
            case "fort":
                pos = World.FortPos + new Vector3(13f, 5.5f, -12f); look = World.FortPos + Vector3.up * 1f; break;
            case "barn":
                {
                    Quaternion r = Quaternion.Euler(0f, World.BarnYaw, 0f);
                    pos = World.BarnPos + r * new Vector3(9f, 3.2f, 17f); look = World.BarnPos + Vector3.up * 2.4f; break;
                }
        }
        pos.y = Mathf.Max(pos.y, World.Ground(pos.x, pos.z) + 1.2f);
        cam.transform.position = pos;
        cam.transform.rotation = Quaternion.LookRotation(look - pos);
    }

    string WinsLine(Soldier me)
    {
        var parts = new List<string>();
        foreach (var s in soldiers)
            if (wins[s.id] > 0 || s == me) parts.Add((s == me ? (s.critter != null ? s.nick + " (YOU)" : "YOU") : s.nick) + " " + wins[s.id]);
        return string.Join("  ", parts.ToArray()) + "  (first to " + WinsNeeded + ")";
    }
}
