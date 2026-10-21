using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ffu22 story mode glue inside Game: the lobby STORY button (same pill style as HOST / PLAY / JOIN; gamepad LB, key S,
// tap / click), starting the story, input routing while a cutscene or the repair meter is up, the full-screen story
// camera (the per-player views are switched off underneath it and restored after), and the objective markers per view.
public partial class Game
{
    ModernButton storyB;
    Image storyBtn;
    Text storyText;
    Camera storyCam;
    bool storyCamOn;
    float lastStoryBtn = -10f;

    void BuildStoryButton(Transform r)
    {
        storyB = new ModernButton(r, "STORY", 3, new Color(0.95f, 0.5f, 0.15f, 1f));
        storyBtn = storyB.root; storyText = storyB.label;
        UIK.Modernize(storyText, true, 0.35f);
    }

    // after LayoutLobby: four buttons in one row (landscape) or PLAY / STORY over HOST / JOIN (portrait)
    void StoryRelayout(bool portrait)
    {
        if (storyBtn == null) return;
        System.Action<Graphic, float, float, float, float> put = (g, x, y, w, h) => { g.rectTransform.anchoredPosition = new Vector2(x, y); g.rectTransform.sizeDelta = new Vector2(w, h); };
        if (portrait)
        {
            put(playBtn, -172, -452, 320, 92);
            put(storyBtn, 172, -452, 320, 92);
            put(hostBtn, -172, -556, 300, 72);
            put(joinBtn, 172, -556, 300, 72);
            put(lobbyStatus, 0, -660, 700, 40); lobbyStatus.fontSize = 20;
            put(lobbyHelp, 0, -747, 690, 42); lobbyHelp.fontSize = 15;
        }
        else
        {
            put(hostBtn, -455, -224, 250, 64);
            put(playBtn, -155, -224, 290, 64);
            put(storyBtn, 155, -224, 290, 64);
            put(joinBtn, 455, -224, 250, 64);
        }
    }

    // after LayoutNetRow (portrait): the room code / NAME rows sit under the second button row
    void StoryNetRow()
    {
        if (storyBtn == null || lobbyLayout != 1) return;
        System.Action<Graphic, float, float, float, float> put = (g, x, y, w, h) => { g.rectTransform.anchoredPosition = new Vector2(x, y); g.rectTransform.sizeDelta = new Vector2(w, h); };
        put(netText, 0, -620, 700, 50);
        if (viewBar.gameObject.activeSelf) { put(viewBar, -170, -706, 340, 50); put(nameBtn, 180, -706, 320, 50); }
        else put(nameBtn, 0, -706, 420, 50);
    }

    void TickStoryButton(bool mouseOk, bool touchOnly, bool online)
    {
        if (storyB == null) return;
        storyBtn.gameObject.SetActive(!online);
        bool sel = lobbyMode == 1 && !online;
        storyText.text = sel ? "> STORY <" : "STORY";
        storyB.SetBadge(touchOnly ? (sel ? "tap to start" : "") : sel ? "LT · ENTER start" : "LB · S");
        storyB.SetTint(sel ? new Color(0.95f, 0.5f, 0.15f, 1f) : new Color(0.45f, 0.32f, 0.22f, 0.85f));
        storyB.focus = sel;
        storyB.Tick(mouseOk);
    }

    // lobby: LB (pad), S (keyboard), tap / click on STORY. True = handled (the game started).
    // ffu67c LOBBY MODE PICK (Bill Oct 20: "pressing LB in the lobby jumps straight into STORY mode, and he can't choose FREE PLAY").
    // FREE PLAY and STORY are two visible mode options (the two big lobby buttons); the highlighted one is what starts.
    //   pad: LB switches the highlight (never starts), LT starts the highlighted mode (ffu59);
    //   keys: F = free play, S = story (select only), Enter starts; touch / mouse: tap a mode to highlight it, tap the highlighted
    //   one again to start. STORY starting opens the episode picker (A / Enter / tap a card). Logs "LOBBY67 ...".
    public int lobbyMode;   // 0 = FREE PLAY, 1 = STORY
    public static string ModeName(int m) { return m == 1 ? "STORY" : "FREE PLAY"; }
    public void SelectMode(int m, string why)
    {
        if (Net.I != null && Net.I.Online) m = 0;
        if (m == lobbyMode) return;
        lobbyMode = m; Sfx.Play(Sfx.Select, 0.45f, m == 1 ? 1.1f : 0.95f);
        Debug.Log("LOBBY67 mode -> " + ModeName(m) + " (" + why + ", no start)");
    }
    public void StartLobbyMode(InputKind kind, Gamepad pad, string why)
    {
        if (slots.Count == 0) return;
        Debug.Log("LOBBY67 start " + ModeName(lobbyMode) + " (" + why + ", " + slots.Count + " seated)");
        if (lobbyMode == 1 && !(Net.I != null && Net.I.Online)) PressStory(kind, pad);
        else StartPlay();
    }
    bool StoryLobbyInput()
    {
        if (storyBtn == null || !storyBtn.gameObject.activeInHierarchy) return false;
        if (Net.I != null && Net.I.Online) return false;
        foreach (Gamepad pad in Gamepad.all)
            if (pad != null && !IsGhostDev(pad) && pad.leftShoulder.wasPressedThisFrame && FindPad(pad) != null) { SelectMode(1 - lobbyMode, "LB"); return true; }
        var k = Keyboard.current;
        if (k != null && !Kb.typing && k.sKey.wasPressedThisFrame) { SelectMode(1, "S key"); return true; }
        if (k != null && !Kb.typing && k.fKey.wasPressedThisFrame) { SelectMode(0, "F key"); return true; }
        foreach (Vector2 pos in Kb.TouchesBegan()) if (Hit(storyBtn, pos)) { lastTouchTime = Time.unscaledTime; if (FindSlot(InputKind.Touch) == null && slots.Count == 0) Join(InputKind.Touch, null); if (lobbyMode == 1) StartLobbyMode(InputKind.Touch, null, "tap STORY again"); else SelectMode(1, "tap STORY"); return true; }
        if (Kb.MouseLeftDown() && Kb.TouchCount() == 0 && Time.unscaledTime - lastTouchTime > 1f && Mouse.current != null && Hit(storyBtn, Mouse.current.position.ReadValue()))
        { if (FindSlot(InputKind.Keyboard) == null && slots.Count == 0) Join(InputKind.Keyboard, null); if (lobbyMode == 1) StartLobbyMode(InputKind.Keyboard, null, "click STORY again"); else SelectMode(1, "click STORY"); return true; }
        return false;
    }
    // the touch / mouse FREE PLAY button: highlight first, start when already highlighted
    bool FreePlayTap(InputKind kind)
    {
        if (lobbyMode != 0) { SelectMode(0, kind == InputKind.Touch ? "tap FREE PLAY" : "click FREE PLAY"); return false; }
        if (FindSlot(kind) == null && slots.Count == 0) Join(kind, null);
        StartLobbyMode(kind, null, kind == InputKind.Touch ? "tap FREE PLAY again" : "click FREE PLAY again");
        return true;
    }

    void PressStory(InputKind kind, Gamepad pad)
    {
        if (Time.unscaledTime - lastStoryBtn < 0.5f) return;
        lastStoryBtn = Time.unscaledTime;
        Slot s = kind == InputKind.Gamepad ? FindPad(pad) : FindSlot(kind);
        if (s == null) s = Join(kind, pad);
        if (s == null && slots.Count == 0) return;
        Sfx.Play(Sfx.Click, 0.8f);
        Debug.Log("FFSTORY picker from the lobby (" + kind + ", " + slots.Count + " players)");
        pickKind = kind;
        OpenPicker();   // ffu24: pick the episode first
    }

    // ---------------- ffu24 story picker ----------------
    StoryPicker picker;
    InputKind pickKind = InputKind.Keyboard;
    float pickDemoT;
    void OpenPicker()
    {
        if (picker == null)
        {
            picker = new StoryPicker();
            picker.onPick = (ep, over) =>
            {
                Story.Episode = ep; Story.PickMode = over ? 2 : 1;
                Debug.Log("FFSTORY start episode " + ep + (over ? " from the start" : " (continue)") + " (" + slots.Count + " players)");
                StartPlay();
                if (state == State.Play) Story.Begin();
            };
        }
        int pre = UrlParam("story") == "3" ? 2 : UrlParam("story") == "2" ? 1 : (Story.ReadSave(Story.SaveKey).Length > 0 ? 1 : 0);
        picker.Show(pre);
        pickDemoT = 0f;
    }
    // UpdateLobby: while the picker is open it owns the lobby input
    bool StoryPickerTick(float dt)
    {
        if (picker == null || !picker.IsOpen) return false;
        InputKind k = Kb.TouchCount() > 0 ? InputKind.Touch : pickKind;
        if (demoShot == "storypick")
        {
            // screenshot demo: show each card in turn, never start
            pickDemoT += Time.unscaledDeltaTime;
            if (pickDemoT > 5f) { pickDemoT = 0f; picker.sel = (picker.sel + 1) % 3; Debug.Log("FFSTORY picker demo sel " + (picker.sel + 1)); }
            picker.Tick(Screen.height > Screen.width, InputKind.Keyboard, Time.unscaledDeltaTime);
            if (!picker.IsOpen) picker.Show(picker.sel);
            return true;
        }
        picker.Tick(Screen.height > Screen.width, k, Time.unscaledDeltaTime);
        return true;
    }
    public float StoryCamYaw() { if (slots.Count == 0) return 0f; return sharedCam.enabled ? sharedYaw : (slots[0].rig != null ? slots[0].rig.yaw : 0f); }

    // demo: ffshot=story* presses STORY instead of PLAY
    bool StoryDemoStart()
    {
        if (demoShot == "storypick") { if (picker == null || !picker.IsOpen) { pickKind = InputKind.Keyboard; OpenPicker(); } return true; }   // ffu24
        if (!demoShot.StartsWith("story") && !demoShot.StartsWith("ep2") && !demoShot.StartsWith("ep3")) return false;
        StartPlay();
        if (state == State.Play) Story.Begin();
        demoPlayT = 0f;
        return true;
    }

    // per slot, before the view / help buttons are read: cutscenes and the repair meter take the input
    PIn StoryFilter(Slot s, Frog f, PIn i)
    {
        if (!Story.Active || Story.I == null || !Story.I.BlocksInput) return i;
        Story.I.SlotPress(i);
        var o = new PIn();
        return o;
    }

    bool StoryBlocksTouch { get { return Story.Active && Story.I != null && Story.I.BlocksInput; } }

    public Texture StoryPortrait(int c) { return Roster.Valid(c) && tileRT[c] != null ? tileRT[c].texture : null; }

    public void StoryFaceCam(float yaw)
    {
        foreach (var s in slots) if (s.rig != null) { s.rig.Snap(); s.rig.SetYaw(yaw); s.rig.ResetView(yaw); }
        sharedYaw = yaw;
    }

    // the cameras that are showing the game right now (for rain), and whether one looks at the ranch
    public List<Camera> StoryViewCams()
    {
        var l = new List<Camera>();
        if (storyCamOn && storyCam != null) { l.Add(storyCam); return l; }
        if (sharedCam != null && sharedCam.enabled) l.Add(sharedCam);
        foreach (var s in slots) if (s.cam != null && s.cam.enabled) l.Add(s.cam);
        return l;
    }
    public bool CamWorldIsRanch(Camera c)
    {
        if (c == storyCam) return Story.I != null && Story.I.CamWorld == WorldId.Ranch;
        if (c == sharedCam) return slots.Count > 0 && frogs[slots[0].frog].world == WorldId.Ranch;
        foreach (var s in slots) if (s.cam == c) return frogs[s.frog].world == WorldId.Ranch;
        return false;
    }

    // end of LateUpdate: story camera on / off, markers per view
    void StoryLate()
    {
        var st = Story.I;
        bool want = Story.Active && st != null && st.CamOn;
        if (st != null && Story.Active) st.CamUpdate();
        if (want)
        {
            if (storyCam == null)
            {
                storyCam = MakeCam("StoryCam", 30);
                storyCam.enabled = false;
            }
            if (!storyCamOn)
            {
                storyCamOn = true;
                storyCam.enabled = true;
                Look.ApplyViews(1, new List<Camera> { storyCam });
            }
            Worlds.SetCamera(storyCam, st.CamWorld);
            storyCam.rect = new Rect(0, 0, 1, 1);
            storyCam.fieldOfView = st.CamFov;
            storyCam.transform.position = st.CamPos;
            Vector3 d = st.CamLook - st.CamPos;
            if (d.sqrMagnitude > 1e-4f) storyCam.transform.rotation = Quaternion.LookRotation(d);
            sharedCam.enabled = false; overview.enabled = false;
            foreach (var s in slots) if (s.cam != null) s.cam.enabled = false;
            hudCanvas.enabled = false;
            RobotPhone.ShowAll(false);
            Sfx.ListenerPos = st.CamPos;
        }
        else if (storyCamOn)
        {
            storyCamOn = false;
            if (storyCam != null) storyCam.enabled = false;
            hudCanvas.enabled = true;
            RobotPhone.ShowAll(true);
            ApplyLayout();
        }
        if (!Story.Active || st == null || st.ui == null) return;
        // objective markers: one per visible view
        var cams = new List<Camera>(); var tg = new List<Vector3?>(); var from = new List<Vector3>();
        if (!storyCamOn && !st.BlocksInput)
        {
            if (sharedCam.enabled && slots.Count > 0) { Frog f = frogs[slots[0].frog]; cams.Add(sharedCam); tg.Add(st.Target(f)); from.Add(f.FocusPoint); }
            else foreach (var s in slots) if (s.cam != null && s.cam.enabled) { Frog f = frogs[s.frog]; cams.Add(s.cam); tg.Add(st.Target(f)); from.Add(f.FocusPoint); }
        }
        st.ui.Markers(cams, tg, from);
    }
}
