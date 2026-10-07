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
        var bg = UIK.Img(r, null, new Color(0.05f, 0.08f, 0.12f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180, 640));
        bg.raycastTarget = false;
        UIK.Label(r, "BALLOON BLAST AIRSOFT", 64, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(1100, 90), new Color(1f, 0.85f, 0.2f));
        UIK.Label(r, "Pop everyone else's balloons! 3 balloons each - lose them all and you're out.\nLast one standing wins the round. Best of 3. 8 players: empty seats are bots.", 24, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 175), new Vector2(1100, 70), Color.white);
        for (int i = 0; i < 4; i++)
        {
            Vector2 p = new Vector2(-405 + i * 270, 40);
            slotPanels[i] = UIK.Img(r, null, new Color(1, 1, 1, 0.12f), new Vector2(0.5f, 0.5f), p, new Vector2(250, 150));
            slotTexts[i] = UIK.Label(r, "", 24, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), p, new Vector2(240, 140), Color.white);
        }
        lobbyStatus = UIK.Label(r, "", 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -90), new Vector2(1100, 60), new Color(0.6f, 1f, 0.6f));
        lobbyHint = UIK.Label(r, "Gamepad: L-stick move | R-stick look | RT fire | LT aim | A jump | X reload\nKeyboard: WASD | mouse look | click fire | right-click aim | Space jump | R reload\nTouch: left stick | drag right side to look | FIRE / ADS / JUMP / RELOAD", 20, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -200), new Vector2(1150, 110), new Color(1, 1, 1, 0.85f));
    }

    void RefreshLobbyUI()
    {
        for (int i = 0; i < 4; i++)
        {
            if (i < slots.Count)
            {
                Slot s = slots[i];
                string dev = s.kind == InputKind.Gamepad ? "Gamepad" : s.kind == InputKind.Keyboard ? "Keyboard + Mouse" : "Touch";
                slotTexts[i].text = "P" + (i + 1) + "\n" + dev + "\nREADY";
                slotPanels[i].color = new Color(Colors[i].r, Colors[i].g, Colors[i].b, 0.55f);
            }
            else
            {
                slotTexts[i].text = "P" + (i + 1) + "\nPress A / Enter\nto join";
                slotPanels[i].color = new Color(1, 1, 1, 0.12f);
            }
        }
        string model = ModelLoader.Ready ? "" : ModelLoader.Failed ? "  (using simple soldiers)" : "  (loading soldiers...)";
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
        touch.active = FindSlot(InputKind.Touch) != null && state != State.Lobby;

        switch (state)
        {
            case State.Lobby: UpdateLobby(dt); break;
            case State.Countdown:
                stateT -= dt;
                if (stateT <= 0f) state = State.Playing;
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
        overview.rect = new Rect(0, 0, 1, 1);
        overview.enabled = true;

        // keyboard
        if (Kb.EnterDown())
        {
            Slot ks = FindSlot(InputKind.Keyboard);
            if (ks == null) Join(InputKind.Keyboard, null);
            else if (Time.unscaledTime - ks.joinTime > 0.3f) { StartMatch(); return; }
        }
        if (Kb.EscDown()) Leave(FindSlot(InputKind.Keyboard));

        // gamepads (with ghost/duplicate filtering)
        float now = Time.unscaledTime;
        foreach (Gamepad pad in Gamepad.all)
        {
            if (pad.buttonEast.wasPressedThisFrame && !ghosts.Contains(pad.deviceId)) Leave(FindPad(pad));
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
        if (Kb.AnyTouchBegan())
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
        slots.Add(s);
        if (kind == InputKind.Keyboard) Cursor.lockState = CursorLockMode.Locked;
        autoStartT = (kind == InputKind.Touch && slots.Count == 1) ? 5f : 20f;
        Debug.Log("Joined P" + slots.Count + " via " + kind + (pad != null ? " " + pad.displayName + " #" + pad.deviceId : ""));
    }

    void Leave(Slot s)
    {
        if (s == null) return;
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
        autoStartT = slots.Count > 0 ? 20f : -1f;
        lobbyCanvas.enabled = true;
    }

    void StartMatch()
    {
        if (slots.Count == 0) return;
        ClearMatch();
        lobbyCanvas.enabled = false;
        autoStartT = -1f;
        for (int i = 0; i < TotalPlayers; i++) wins[i] = 0;
        round = 0;
        matchWinner = null;
        feedLines.Clear(); feedTimes.Clear();

        int bot = 0;
        for (int i = 0; i < TotalPlayers; i++)
        {
            bool human = i < slots.Count;
            var go = new GameObject("Soldier" + i);
            var s = go.AddComponent<Soldier>();
            string nick = human ? "P" + (i + 1) : BotNames[bot++ % BotNames.Length];
            s.Build(i, nick, Colors[i], human, human ? 20 + i : 0);
            soldiers.Add(s);
            if (human)
            {
                Slot sl = slots[i];
                sl.soldier = s;
                s.slot = sl;
                SetupCamera(sl);
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

        // first-person airsoft rifle
        var vm = new GameObject("ViewModel").transform;
        vm.SetParent(cg.transform, false);
        Color dark = new Color(0.16f, 0.17f, 0.19f);
        Material dm = Mats.Lit(dark);
        GameObject[] parts =
        {
            Mats.Prim(PrimitiveType.Cube, vm, new Vector3(0f, 0f, 0f), new Vector3(0.06f, 0.08f, 0.5f), dm, false),
            Mats.Prim(PrimitiveType.Cube, vm, new Vector3(0f, -0.08f, -0.05f), new Vector3(0.04f, 0.12f, 0.06f), dm, false),
            Mats.Prim(PrimitiveType.Cube, vm, new Vector3(0f, -0.07f, 0.1f), new Vector3(0.035f, 0.13f, 0.06f), Mats.Lit(sl.soldier.color), false),
            Mats.Prim(PrimitiveType.Cube, vm, new Vector3(0f, 0.055f, 0.02f), new Vector3(0.025f, 0.03f, 0.14f), dm, false),
            Mats.Prim(PrimitiveType.Cube, vm, new Vector3(0f, 0f, 0.27f), new Vector3(0.065f, 0.065f, 0.05f), Mats.Lit(new Color(1f, 0.45f, 0.05f)), false)
        };
        foreach (var p in parts)
        {
            p.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            p.layer = 24 + sl.index;
        }
        vm.gameObject.layer = 24 + sl.index;
        sl.viewModel = vm;

        sl.hud = new Hud(cam, "P" + (sl.index + 1), sl.soldier.color, 10 + sl.index);
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
    }

    public void OnPop(Soldier by, Soldier victim)
    {
        string who = by != null ? by.nick : "Someone";
        AddFeed(who + " popped " + victim.nick + "'s balloon");
    }

    public void OnOut(Soldier victim, Soldier by)
    {
        AddFeed(victim.nick + " is OUT!");
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
                Vector3 hip = new Vector3(0.17f, -0.16f, 0.36f), aim = new Vector3(0f, -0.085f, 0.3f);
                sl.viewModel.localPosition = Vector3.Lerp(hip, aim, s.adsBlend) + new Vector3(0f, 0f, -0.04f * s.kick);
            }

            string top = "ROUND " + round + "  |  " + alive + " LEFT  |  " + clock + "\nWINS  " + WinsLine(s);
            string center = "";
            switch (state)
            {
                case State.Countdown: center = "ROUND " + round + "\n" + Mathf.CeilToInt(stateT); break;
                case State.Playing:
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
            if (sl.kind == InputKind.Keyboard && state == State.Playing && s.alive && Cursor.lockState != CursorLockMode.Locked)
                center = "Click to aim with the mouse";
            sl.hud.Tick(s, top, center, feed, state == State.Playing, dt);
        }
    }

    string WinsLine(Soldier me)
    {
        var parts = new List<string>();
        foreach (var s in soldiers)
            if (wins[s.id] > 0 || s == me) parts.Add((s == me ? "YOU" : s.nick) + " " + wins[s.id]);
        return string.Join("  ", parts.ToArray()) + "  (first to " + WinsNeeded + ")";
    }
}
