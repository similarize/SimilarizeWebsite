using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

// ffu45 HELP / PAUSE SCREEN REDESIGN (Bill: "the H help / pause screen is messy, hard to read and runs off the screen").
//  * One clean card inside Screen.safeArea (notches / rounded corners / browser bars) on every aspect: desktop 16:9,
//    phone portrait, phone landscape. Dimmed backdrop behind it.
//  * Header: title, which controls are shown ("Gamepad" / "Keyboard + mouse" / "Touch"), an obvious CLOSE chip (tap /
//    click it, Start on a pad, H on keys).
//  * Your vehicle's controls come first ("NOW DRIVING ..."), then sections MOVE / VEHICLES / ACTIONS / PHONE + CAMERA
//    laid out masonry-style in 3 columns (wide), 2 (medium) or one scrolling column (narrow). Only the bindings of the
//    device that opened the menu are listed (pad glyphs in the Xbox colours, key names, or touch button names).
//  * Text auto-scales (down to 0.7x) so the wide layouts fit without scrolling; whatever still doesn't fit scrolls
//    (stick / D-pad up-down, arrow keys / wheel, finger drag), with a scroll bar.
//  * SETTINGS rows pinned under the sections, always visible: WHEEL SIZE slider (ffu45: the wheels of the vehicle YOU are
//    in; greyed out with "Get in a vehicle to resize its wheels" on foot) - pad D-pad < > / LB RB, arrow keys - =, drag;
//    SOUND (Y / M / SND).
//  * UIK modern fonts (Montserrat ExtraBold headers, Inter SemiBold body).
// Also here: the in-vehicle wheel-size shortcut (hold View + D-pad up / down on a pad, [ / ] on keys) with a toast readout,
// and a small MENU chip for touch players (touch had no way to open this screen).
public partial class Game
{
    RectTransform hpRoot, hpSafe, hpCard, hpView, hpContent, hpSettings, hpClose, hpBar, hpBarThumb, hpMenuChip;
    Image hpDim; Text hpTitle, hpSub, hpCloseT, hpFoot, hpSound, hpWheelHint, hpScrollHint;
    Slot helpBy;
    string hpSig = "";
    float hpScroll, hpMaxScroll, hpFs = 1f;
    bool hpDragging; Vector2 hpDragLast;
    readonly List<GameObject> hpRows = new List<GameObject>();
    readonly Dictionary<Slot, bool> viewCombo = new Dictionary<Slot, bool>();
    readonly Dictionary<Slot, bool> viewHeld = new Dictionary<Slot, bool>();
    public static int HelpRebuilds;
    int hpForce = -1;
    // the web page draws its own fullscreen / Arcade buttons over the top-right corner (~55 css px): keep the card under them
    static float HpTopGap { get { return Application.platform == RuntimePlatform.WebGLPlayer ? 40f : 0f; } }
    public static string HelpLayoutInfo = "";

    static readonly Color HpAccent = new Color(0.55f, 1f, 0.44f), HpCardC = new Color(0.07f, 0.09f, 0.12f, 0.93f), HpSecC = new Color(1f, 1f, 1f, 0.055f);

    void HelpBuild(Transform r)
    {
        hpRoot = UIK.Rect(r, "HelpScreen", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        UIK.Stretch(hpRoot);
        // its own sorting layer above the HUD, the per-view HUDs and the name tags (they used to draw over the panel)
        var hc = hpRoot.gameObject.AddComponent<Canvas>(); hc.overrideSorting = true; hc.sortingOrder = 400;
        hpDim = UIK.Img(hpRoot, null, new Color(0f, 0.02f, 0.04f, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        UIK.Stretch(hpDim.rectTransform);
        hpSafe = UIK.Rect(hpRoot, "Safe", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        UIK.Stretch(hpSafe);
        var card = UIK.Glass(hpSafe, HpCardC, Vector2.zero, Vector2.zero);
        hpCard = card.rectTransform; UIK.Stretch(hpCard);
        var edge = UIK.Sliced(hpCard, UIK.Edge2, new Color(1f, 1f, 1f, 0.14f), 2f); UIK.Stretch(edge.rectTransform);
        hpTitle = UIK.Label(hpCard, "PAUSED  ·  CONTROLS", 30, TextAnchor.MiddleLeft, new Vector2(0f, 1f), Vector2.zero, new Vector2(600f, 40f), HpAccent);
        UIK.Modernize(hpTitle, true, 0.3f);
        hpSub = UIK.Label(hpCard, "", 16, TextAnchor.MiddleLeft, new Vector2(0f, 1f), Vector2.zero, new Vector2(600f, 24f), new Color(0.82f, 0.86f, 0.9f));
        UIK.Modernize(hpSub, false, 0.25f);
        var cb = UIK.Glass(hpCard, new Color(0.95f, 0.3f, 0.25f, 0.92f), Vector2.zero, new Vector2(150f, 44f));
        hpClose = cb.rectTransform; hpClose.anchorMin = hpClose.anchorMax = new Vector2(1f, 1f); hpClose.pivot = new Vector2(1f, 1f);
        hpCloseT = UIK.Label(hpClose, "CLOSE", 18, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 44f), Color.white);
        UIK.Stretch(hpCloseT.rectTransform); UIK.Modernize(hpCloseT, true, 0.3f);
        // scrolling viewport (clips its rows)
        hpView = UIK.Rect(hpCard, "View", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        hpView.gameObject.AddComponent<RectMask2D>();
        hpContent = UIK.Rect(hpView, "Content", new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
        hpContent.pivot = new Vector2(0f, 1f);
        var bar = UIK.Img(hpCard, UIK.Round, new Color(1f, 1f, 1f, 0.08f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(6f, 100f));
        bar.type = Image.Type.Sliced; hpBar = bar.rectTransform;
        var th = UIK.Img(hpBar, UIK.Round, new Color(0.55f, 1f, 0.44f, 0.7f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(6f, 40f));
        th.type = Image.Type.Sliced; hpBarThumb = th.rectTransform; hpBarThumb.pivot = new Vector2(0.5f, 1f);
        hpScrollHint = UIK.Label(hpCard, "", 14, TextAnchor.MiddleRight, new Vector2(1f, 0f), Vector2.zero, new Vector2(400f, 20f), new Color(1f, 1f, 1f, 0.55f));
        UIK.Modernize(hpScrollHint, false, 0f);
        // settings (pinned)
        var st = UIK.Glass(hpCard, new Color(1f, 1f, 1f, 0.07f), Vector2.zero, Vector2.zero);
        hpSettings = st.rectTransform;
        var sh = UIK.Label(hpSettings, "SETTINGS", 15, TextAnchor.MiddleLeft, new Vector2(0f, 1f), Vector2.zero, new Vector2(200f, 22f), HpAccent);
        UIK.Modernize(sh, true, 0f); sh.rectTransform.pivot = new Vector2(0f, 1f); sh.rectTransform.anchoredPosition = new Vector2(16f, -8f); sh.name = "SettingsHead";
        wheelLabel = UIK.Label(hpSettings, "", 18, TextAnchor.MiddleLeft, new Vector2(0f, 1f), Vector2.zero, new Vector2(300f, 30f), Color.white);
        UIK.Modernize(wheelLabel, true, 0.2f);
        wheelTrack = UIK.Img(hpSettings, UIK.Round, new Color(1f, 1f, 1f, 0.16f), new Vector2(0f, 1f), Vector2.zero, new Vector2(400f, 22f));
        wheelTrack.type = Image.Type.Sliced;
        wheelFill = UIK.Img(wheelTrack.transform, UIK.Round, new Color(0.55f, 1f, 0.44f, 0.95f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
        wheelFill.type = Image.Type.Sliced; wheelFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        wheelKnob = UIK.Img(wheelTrack.transform, UIK.Circle, Color.white, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(34f, 34f));
        hpWheelHint = UIK.Label(hpSettings, "", 14, TextAnchor.MiddleLeft, new Vector2(0f, 1f), Vector2.zero, new Vector2(600f, 22f), new Color(0.8f, 0.84f, 0.9f));
        UIK.Modernize(hpWheelHint, false, 0f);
        hpSound = UIK.Label(hpSettings, "", 16, TextAnchor.MiddleLeft, new Vector2(0f, 1f), Vector2.zero, new Vector2(600f, 24f), new Color(0.9f, 0.92f, 0.95f));
        UIK.Modernize(hpSound, false, 0f);
        hpFoot = UIK.Label(hpCard, "", 14, TextAnchor.MiddleLeft, new Vector2(0f, 0f), Vector2.zero, new Vector2(800f, 20f), new Color(1f, 1f, 1f, 0.55f));
        UIK.Modernize(hpFoot, false, 0f);
        // touch: a small MENU chip (opens this screen)
        var mc = UIK.Glass(r, new Color(0.07f, 0.09f, 0.12f, 0.7f), Vector2.zero, new Vector2(92f, 40f));
        hpMenuChip = mc.rectTransform; hpMenuChip.anchorMin = hpMenuChip.anchorMax = new Vector2(0.5f, 1f); hpMenuChip.pivot = new Vector2(0.5f, 1f);
        var mt = UIK.Label(hpMenuChip, "MENU", 16, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(92f, 40f), Color.white);
        UIK.Stretch(mt.rectTransform); UIK.Modernize(mt, true, 0.2f);
        hpMenuChip.gameObject.SetActive(false);
        hpRoot.gameObject.SetActive(false);
    }

    // ---------------- per frame ----------------
    // returns true when the help screen wants to be closed by a tap / click on CLOSE
    void HelpTick(float dt)
    {
        // touch MENU chip (play, help closed)
        bool chip = state == State.Play && touch != null && touch.active && !help;
        if (hpMenuChip.gameObject.activeSelf != chip) hpMenuChip.gameObject.SetActive(chip);
        if (chip)
        {
            PlaceInSafe(hpMenuChip, new Vector2(0f, -12f));
            foreach (Vector2 tp in Kb.TouchesBegan()) if (Hit(hpMenuChip, tp, 10f)) { help = true; helpBy = FindSlot(InputKind.Touch); Sfx.Play(Sfx.Click, 0.7f); hpScroll = 0f; break; }
        }
        if (hpRoot.gameObject.activeSelf != help) { hpRoot.gameObject.SetActive(help); if (help) { hpSig = ""; hpScroll = 0f; } }
        if (!help) { hpDragging = false; return; }
        if (helpBy == null || !slots.Contains(helpBy)) helpBy = slots.Count > 0 ? slots[0] : null;
        // safe area -> anchors
        Rect sa = Screen.safeArea;
        float sw = Mathf.Max(1, Screen.width), shh = Mathf.Max(1, Screen.height);
        hpSafe.anchorMin = new Vector2(sa.xMin / sw, sa.yMin / shh); hpSafe.anchorMax = new Vector2(sa.xMax / sw, sa.yMax / shh);
        Vector2 cs = ((RectTransform)hudCanvas.transform).rect.size;
        bool portrait = cs.y > cs.x;
        float mg = portrait ? 12f : 22f;
        hpCard.offsetMin = new Vector2(mg, mg); hpCard.offsetMax = new Vector2(-mg, -mg - HpTopGap);
        Frog f = helpBy != null ? frogs[helpBy.frog] : null;
        InputKind kind = helpBy != null ? helpBy.kind : InputKind.Keyboard;
        if (hpForce >= 0) kind = (InputKind)hpForce;   // demo / probe: &helpdev=pad|keys|touch
        Vehicle v = f != null ? (f.vehicle != null ? f.vehicle : null) : null;
        string sig = kind + "|" + (v != null ? v.Title : "-") + "|" + Screen.width + "x" + Screen.height + "|" + sa + "|" + Worlds.SpaceOn + Worlds.StageEOn;
        if (sig != hpSig) { hpSig = sig; HelpRebuild(kind, v, f); }
        HelpSettingsDraw(kind, v);
        HelpScrollInput(kind, dt);
        // CLOSE: tap / click
        bool closeHit = false;
        foreach (Vector2 tp in Kb.TouchesBegan()) if (Hit(hpClose, tp, 12f)) closeHit = true;
        var mouse = Mouse.current;
        if (mouse != null && Kb.MouseLeftDown() && Hit(hpClose, mouse.position.ReadValue(), 4f)) closeHit = true;
        if (closeHit) { help = false; Sfx.Play(Sfx.Click, 0.7f); hpRoot.gameObject.SetActive(false); }
    }

    void PlaceInSafe(RectTransform rt, Vector2 ofs)
    {
        // top-centre of the safe area in canvas units (the top corners hold the page's buttons, PHONE and the zoom keys)
        Rect sa = Screen.safeArea;
        Vector2 cs = ((RectTransform)hudCanvas.transform).rect.size;
        float kx = cs.x / Mathf.Max(1, Screen.width), ky = cs.y / Mathf.Max(1, Screen.height);
        rt.anchoredPosition = new Vector2((sa.center.x - Screen.width * 0.5f) * kx + ofs.x, -(Screen.height - sa.yMax) * ky + ofs.y);
    }

    static bool Hit(RectTransform rt, Vector2 screen, float pad)
    {
        Vector2 lp;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screen, null, out lp)) return false;
        Rect r = rt.rect;
        return lp.x > r.xMin - pad && lp.x < r.xMax + pad && lp.y > r.yMin - pad && lp.y < r.yMax + pad;
    }

    // ---------------- content ----------------
    struct HRow { public string k, d; public HRow(string k, string d) { this.k = k; this.d = d; } }
    class HSec { public string title; public bool accent; public List<HRow> rows = new List<HRow>(); public HSec(string t, bool a = false) { title = t; accent = a; } public HSec R(string k, string d) { rows.Add(new HRow(k, d)); return this; } }

    static string G(string b)
    {
        // Xbox-coloured pad glyphs
        switch (b)
        {
            case "A": return "<color=#62d66a>A</color>";
            case "B": return "<color=#ff6058>B</color>";
            case "X": return "<color=#52a8ff>X</color>";
            case "Y": return "<color=#ffd23f>Y</color>";
        }
        return b;
    }
    static string Pk(string s)
    {
        // colour every lone A / B / X / Y token in a pad key string
        var parts = s.Split(' ');
        for (int i = 0; i < parts.Length; i++) parts[i] = G(parts[i]);
        return string.Join(" ", parts);
    }

    List<HSec> HelpSections(InputKind kind, Vehicle v, Frog f)
    {
        bool pad = kind == InputKind.Gamepad, keys = kind == InputKind.Keyboard;
        var L = new List<HSec>();
        if (v != null)
        {
            var s = new HSec("NOW DRIVING  ·  " + v.Title.ToUpperInvariant(), true);
            foreach (string part in v.HelpLine.Split('|'))
            {
                string p = part.Trim(); if (p.Length == 0) continue;
                string k, d; SplitBinding(p, out k, out d);
                if (v.Title.Length > 0 && d.StartsWith(v.Title + ":")) d = d.Substring(v.Title.Length + 1).Trim();
                if (k.Length == 0 && p.StartsWith(v.Title + ":")) { string q = p.Substring(v.Title.Length + 1).Trim(); SplitBinding(q, out k, out d); }
                s.R(pad ? Pk(k.IndexOf(" | ") >= 0 ? k.Substring(0, k.IndexOf(" | ")) : k) : MapKey(k, kind), d);
            }
            if (BigWheels.Resizable(v)) s.R(pad ? "View + D-pad up / down" : keys ? "[  /  ]" : "WHEELS tab", "this vehicle's wheel size (now " + BigWheels.LabelFor(v) + ")");
            L.Add(s);
        }
        var mv = new HSec("MOVE");
        if (pad) mv.R("L-stick", "walk / steer").R("R-stick", "look around (R3 resets)").R(G("A"), "hop  ·  swim up").R("Hold " + G("B"), "dive when swimming").R("D-pad up / down", "zoom");
        else if (keys) mv.R("W A S D", "walk / steer").R("Mouse", "look around (click to lock)").R("Space", "hop  ·  swim up").R("Shift", "dive when swimming").R("Wheel  /  Q  /  X", "zoom").R("0", "reset the camera");
        else mv.R("Left stick", "walk / steer").R("Drag", "look around (double-tap resets)").R("A", "hop  ·  swim up").R("DIVE", "dive when swimming").R("-  /  +", "zoom");
        L.Add(mv);
        var vh = new HSec("VEHICLES");
        if (pad) vh.R(G("A"), "get in / out").R("RT  /  LT", "gas  /  brake + reverse").R("RT  /  LT (flying)", "up  /  down; bail out = parachute").R("R-stick  ·  RT  ·  RB / " + G("Y"), "tank: aim  ·  shell  ·  missile").R("View + D-pad", "wheel size of the vehicle you're in");
        else if (keys) vh.R("E", "get in / out").R("W  /  S", "gas  /  brake + reverse").R("Space  /  Shift", "fly up  /  down").R("Click  /  Right-click", "tank shell  /  missile").R("[  /  ]", "wheel size of the vehicle you're in");
        else vh.R("A", "get in / out").R("GAS  /  BRAKE", "drive").R("UP  /  DOWN", "fly").R("FIRE  /  MSL", "weapons");
        vh.R("Back up to a hitch", "trucks tow the barn trailers (" + (pad ? G("A") + " / " + G("Y") : keys ? "E" : "tap") + ")");
        vh.R("Bikes", "garage + trailer barn; ride into a toy hauler to stow");
        L.Add(vh);
        var ac = new HSec("ACTIONS");
        string use = pad ? G("A") : keys ? "E" : "A";
        ac.R(use, "at a prompt: doors, gates, fish tanks, ramps, chat");
        ac.R("Rally", "figure-8, bridge, jumps, loop lane west of the garage");
        ac.R("Pond", "boat gate course from gate 1; sub at the dock");
        if (Worlds.SpaceOn) ac.R("Starship pad", use + " launches (Earth, Moon, Mars, Callisto)");
        if (Worlds.StageEOn) ac.R("Mech yard", "every froggy has its own mech" + (pad ? "  ·  RT / " + G("X") + " omnigun" : ""));
        ac.R("House", "walk into the front door");
        L.Add(ac);
        var pc = new HSec("PHONE + CAMERA");
        if (pad) pc.R("LB (on foot)", "robot phone: chores, drive, missions, call, wheels").R(G("Y") + " (phone)", "robot video feed").R("View", "shared / split screen").R("Start", "this menu").R(G("B") + " here", "leave your seat");
        else if (keys) pc.R("P", "robot phone: chores, drive, missions, call, wheels").R("O  ·  I", "robot video feed  ·  feed camera").R("V", "shared / split screen").R("H", "this menu").R("M", "sound on / low / off").R("Esc here", "leave your seat");
        else pc.R("PHONE", "robot phone: chores, drive, missions, call, wheels").R("SND", "sound on / low / off").R("MENU", "this menu");
        pc.R("Online", "lobby: HOST shows a code, friends JOIN with it");
        L.Add(pc);
        return L;
    }

    static readonly string[] PadTok = { "L-stick", "R-stick", "RT", "LT", "RB", "LB", "R3", "D-pad", "View", "Start", "A", "B", "X", "Y", "stick" };
    static void SplitBinding(string p, out string k, out string d)
    {
        // leading pad tokens ("RT", "LB / RB", "A", "L-stick") = the binding, the rest = what it does
        var w = p.Split(' ');
        int n = 0;
        while (n < w.Length)
        {
            string t = w[n];
            bool tok = System.Array.IndexOf(PadTok, t) >= 0 || t == "/" || t == "+" || (n > 0 && (t == "<" || t == ">" || t == "up/down"));
            if (!tok) break;
            n++;
        }
        while (n > 0 && (w[n - 1] == "/" || w[n - 1] == "+")) n--;
        // "Y / F Cyberboat mode": a pad button + its keyboard key ("/ F") -> keep both in the binding ("Y | F")
        if (n > 0 && n + 1 < w.Length && w[n] == "/" && w[n + 1].Length <= 3 && w[n + 1].ToUpperInvariant() == w[n + 1] && System.Array.IndexOf(PadTok, w[n + 1]) < 0 && n + 2 < w.Length)
        { k = string.Join(" ", w, 0, n) + " | " + w[n + 1]; d = string.Join(" ", w, n + 2, w.Length - n - 2); return; }
        if (n == 0 || n == w.Length) { k = ""; d = p; return; }
        k = string.Join(" ", w, 0, n); d = string.Join(" ", w, n, w.Length - n);
    }
    static string MapKey(string k, InputKind kind)
    {
        if (k.Length == 0) return "";
        bool keys = kind == InputKind.Keyboard;
        int bar = k.IndexOf(" | ");
        if (bar >= 0) { if (keys) return k.Substring(bar + 3); k = k.Substring(0, bar); }
        var w = k.Split(' ');
        for (int i = 0; i < w.Length; i++)
        {
            string t = w[i], m = t;
            if (keys)
                switch (t) { case "L-stick": m = "WASD"; break; case "R-stick": m = "Mouse"; break; case "RT": m = "W"; break; case "LT": m = "S"; break; case "A": m = "E"; break; case "B": m = "Shift"; break; case "X": m = "Click"; break; case "Y": case "RB": m = "Right-click"; break; case "LB": m = "Shift"; break; case "R3": m = "0"; break; case "D-pad": m = "Q / X"; break; case "View": m = "V"; break; case "Start": m = "H"; break; }
            else
                switch (t) { case "L-stick": m = "Stick"; break; case "R-stick": m = "Drag"; break; case "RT": m = "GAS"; break; case "LT": m = "BRAKE"; break; case "A": m = "A"; break; case "B": m = "DIVE"; break; case "X": m = "FIRE"; break; case "Y": case "RB": m = "MSL"; break; case "LB": m = "UP"; break; case "D-pad": m = "- / +"; break; case "View": m = "VIEW"; break; case "Start": m = "MENU"; break; }
            w[i] = m;
        }
        return string.Join(" ", w);
    }

    // ---------------- layout ----------------
    void HelpRebuild(InputKind kind, Vehicle v, Frog f)
    {
        HelpRebuilds++;
        foreach (var g in hpRows) if (g != null) Destroy(g);
        hpRows.Clear();
        Canvas.ForceUpdateCanvases();
        Vector2 cs = ((RectTransform)hudCanvas.transform).rect.size;
        Rect sa = Screen.safeArea;
        float kx = cs.x / Mathf.Max(1, Screen.width), ky = cs.y / Mathf.Max(1, Screen.height);
        float mg = cs.y > cs.x ? 12f : 22f;
        float W = sa.width * kx - mg * 2f, H = sa.height * ky - mg * 2f - HpTopGap;
        bool narrow = W < 700f;
        float pad = narrow ? 16f : 24f;
        float base0 = narrow ? 1.12f : 1f;
        // header
        float tsz = Mathf.Round((narrow ? 26f : 30f));
        hpTitle.fontSize = (int)tsz;
        Place(hpTitle.rectTransform, new Vector2(0f, 1f), new Vector2(pad, -pad), new Vector2(W - pad * 2f - 170f, tsz + 10f));
        hpTitle.text = narrow ? "CONTROLS" : "PAUSED  ·  CONTROLS";
        hpSub.fontSize = narrow ? 15 : 16;
        Place(hpSub.rectTransform, new Vector2(0f, 1f), new Vector2(pad, -pad - tsz - 8f), new Vector2(W - pad * 2f - 170f, 22f));
        hpSub.text = "Showing " + (kind == InputKind.Gamepad ? "gamepad" : kind == InputKind.Touch ? "touch" : "keyboard + mouse") + " controls" + (narrow ? "" : "  ·  game paused for you");
        hpClose.sizeDelta = new Vector2(narrow ? 132f : 168f, narrow ? 44f : 46f);
        hpClose.anchoredPosition = new Vector2(-pad + 6f, -pad + 6f);
        hpCloseT.fontSize = narrow ? 16 : 17;
        hpCloseT.text = kind == InputKind.Gamepad ? "CLOSE  ·  Start" : kind == InputKind.Touch ? "CLOSE" : "CLOSE  ·  H";
        float headH = pad + tsz + 8f + 26f + 10f;
        // settings block (pinned at the bottom) + footer
        float setH = narrow ? 168f : 92f, footH = 24f;
        hpSettings.anchorMin = hpSettings.anchorMax = new Vector2(0f, 0f); hpSettings.pivot = new Vector2(0f, 0f);
        hpSettings.anchoredPosition = new Vector2(pad, pad + footH); hpSettings.sizeDelta = new Vector2(W - pad * 2f, setH);
        Place(hpFoot.rectTransform, new Vector2(0f, 0f), new Vector2(pad, pad), new Vector2(W - pad * 2f, 20f), new Vector2(0f, 0f));
        hpFoot.text = kind == InputKind.Gamepad ? "Start closes  ·  " + G("B") + " leaves your seat  ·  " + G("Y") + " sound" : kind == InputKind.Touch ? "Tap CLOSE to play  ·  drag to scroll" : "H closes  ·  Esc leaves your seat  ·  M sound";
        // viewport
        float viewH = H - headH - setH - footH - pad - 10f;
        hpView.anchorMin = hpView.anchorMax = new Vector2(0f, 1f); hpView.pivot = new Vector2(0f, 1f);
        hpView.anchoredPosition = new Vector2(pad, -headH); hpView.sizeDelta = new Vector2(W - pad * 2f, viewH);
        hpBar.anchorMin = hpBar.anchorMax = new Vector2(1f, 1f); hpBar.pivot = new Vector2(1f, 1f);
        hpBar.anchoredPosition = new Vector2(-pad * 0.4f, -headH); hpBar.sizeDelta = new Vector2(5f, viewH);
        Place(hpScrollHint.rectTransform, new Vector2(1f, 0f), new Vector2(-pad, pad + footH + setH + 2f), new Vector2(420f, 18f), new Vector2(1f, 0f));
        // columns + font scale: shrink until the sections fit (wide), never below 0.7x; narrow = one column at full size, scrolls
        float cw = W - pad * 2f;
        int cols = narrow ? 1 : cw >= 1040f ? 3 : cw >= 660f ? 2 : 1;   // phone portrait = one scrolling column
        var secs = HelpSections(kind, v, f);
        float fs = base0, contentH = 0f;
        for (int it = 0; it < 8; it++)
        {
            contentH = HelpPlace(secs, cols, cw, fs, false);
            if (contentH <= viewH || cols == 1 || fs <= 0.78f) break;
            fs = Mathf.Max(0.78f, fs - 0.05f);
        }
        hpFs = fs;
        contentH = HelpPlace(secs, cols, cw, fs, true);
        hpContent.sizeDelta = new Vector2(cw, contentH);
        hpMaxScroll = Mathf.Max(0f, contentH - viewH);
        hpScroll = Mathf.Clamp(hpScroll, 0f, hpMaxScroll);
        HelpLayoutInfo = "canvas " + cs.x.ToString("0") + "x" + cs.y.ToString("0") + " safe " + sa + " card " + W.ToString("0") + "x" + H.ToString("0") + " cols " + cols + " font x" + fs.ToString("0.00") + " content " + contentH.ToString("0") + " view " + viewH.ToString("0") + " scroll max " + hpMaxScroll.ToString("0") + " device " + kind + " vehicle " + (v != null ? v.Title : "-");
        Debug.Log("HELP: layout " + HelpLayoutInfo);
        // settings rows
        float sx = 16f, sy = -32f;
        float lw = narrow ? cw - 32f : 300f;
        wheelLabel.fontSize = Mathf.RoundToInt(narrow ? 19f : 18f);
        Place(wheelLabel.rectTransform, new Vector2(0f, 1f), new Vector2(sx, sy), new Vector2(lw, 30f));
        float tx = narrow ? sx + 18f : sx + lw + 30f, ty = narrow ? sy - 46f : sy - 4f, tw = narrow ? cw - 32f - 36f : cw - lw - 90f;
        tw = Mathf.Min(tw, 640f);
        wheelTrack.rectTransform.anchorMin = wheelTrack.rectTransform.anchorMax = new Vector2(0f, 1f);
        wheelTrack.rectTransform.pivot = new Vector2(0f, 1f);
        wheelTrack.rectTransform.anchoredPosition = new Vector2(tx, ty); wheelTrack.rectTransform.sizeDelta = new Vector2(tw, 22f);
        Place(hpWheelHint.rectTransform, new Vector2(0f, 1f), new Vector2(sx, narrow ? sy - 82f : sy - 32f), new Vector2(narrow ? cw - 32f : cw * 0.62f, 20f));
        hpWheelHint.fontSize = narrow ? 15 : 14;
        Place(hpSound.rectTransform, new Vector2(0f, 1f), narrow ? new Vector2(sx, sy - 110f) : new Vector2(cw * 0.64f, sy - 30f), new Vector2(narrow ? cw - 32f : cw * 0.36f - 16f, 22f));
        hpSound.fontSize = narrow ? 17 : 16;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
    {
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot ?? new Vector2(0f, 1f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    float TextH(Text t, float w)
    {
        var gs = t.GetGenerationSettings(new Vector2(w, 0f));
        float ppu = Mathf.Max(0.01f, t.pixelsPerUnit);
        return t.cachedTextGeneratorForLayout.GetPreferredHeight(t.text, gs) / ppu;
    }

    // masonry: each section goes into the shortest column. build = create the UI, else just measure. returns content height
    float HelpPlace(List<HSec> secs, int cols, float cw, float fs, bool build)
    {
        float gap = 16f, colW = (cw - gap * (cols - 1)) / cols;
        var colY = new float[cols];
        int rowFs = Mathf.RoundToInt(17f * fs), headFs = Mathf.RoundToInt(16f * fs);
        float keyW = Mathf.Round(colW * (cols == 1 ? 0.34f : 0.38f)), ip = 12f * Mathf.Clamp(fs, 0.8f, 1.1f);
        Text probe = build ? null : MeasureText(rowFs);
        foreach (var s in secs)
        {
            // accent "now driving" section spans the full width on top
            bool span = s.accent && cols > 1;
            int c = 0;
            if (span) { float m = 0f; foreach (float cyv in colY) m = Mathf.Max(m, cyv); for (int i = 0; i < cols; i++) colY[i] = m; }
            else for (int i = 1; i < cols; i++) if (colY[i] < colY[c] - 0.5f) c = i;
            float x = span ? 0f : c * (colW + gap), w = span ? cw : colW, y0 = colY[c];
            float y = ip;
            Transform sec = null;
            if (build)
            {
                var bg = UIK.Glass(hpContent, s.accent ? new Color(0.35f, 0.75f, 0.3f, 0.16f) : HpSecC, Vector2.zero, Vector2.zero);
                sec = bg.rectTransform; Place((RectTransform)sec, new Vector2(0f, 1f), new Vector2(x, -y0), new Vector2(w, 10f));
                hpRows.Add(bg.gameObject);
                var ht = UIK.Label(sec, s.title, headFs, TextAnchor.UpperLeft, new Vector2(0f, 1f), Vector2.zero, new Vector2(w - ip * 2f, headFs + 8f), s.accent ? HpAccent : new Color(0.55f, 0.85f, 1f));
                UIK.Modernize(ht, true, 0f); Place(ht.rectTransform, new Vector2(0f, 1f), new Vector2(ip, -y), new Vector2(w - ip * 2f, headFs + 8f));
            }
            y += headFs + 12f;
            // rows: two-column (binding | action) when the section is wide enough, else stacked
            float kw = span ? Mathf.Min(cols == 1 ? keyW : 170f, w * 0.3f) : keyW;
            int perRow = span && w > 900f ? 2 : 1;
            float cellW = (w - ip * 2f - (perRow - 1) * 18f) / perRow;
            for (int r = 0; r < s.rows.Count; r += perRow)
            {
                float rh = 0f;
                for (int q = 0; q < perRow && r + q < s.rows.Count; q++)
                {
                    var row = s.rows[r + q];
                    float cx = ip + q * (cellW + 18f);
                    float dW = cellW - kw - 10f;
                    bool stacked = row.k.Length > 0 && dW < 120f;
                    float h;
                    if (build)
                    {
                        Text kt = null;
                        if (row.k.Length > 0)
                        {
                            kt = UIK.Label(sec, row.k, rowFs, TextAnchor.UpperLeft, new Vector2(0f, 1f), Vector2.zero, new Vector2(stacked ? cellW : kw, 10f), Color.white);
                            UIK.Modernize(kt, true, 0f);
                            Place(kt.rectTransform, new Vector2(0f, 1f), new Vector2(cx, -y), new Vector2(stacked ? cellW : kw, TextH(kt, stacked ? cellW : kw) + 2f));
                        }
                        float dx = row.k.Length == 0 || stacked ? cx : cx + kw + 10f, dw = row.k.Length == 0 || stacked ? cellW : dW;
                        float dy = stacked ? TextH(kt, cellW) + 2f : 0f;
                        var dt = UIK.Label(sec, row.d, rowFs, TextAnchor.UpperLeft, new Vector2(0f, 1f), Vector2.zero, new Vector2(dw, 10f), new Color(0.86f, 0.89f, 0.93f));
                        UIK.Modernize(dt, false, 0f);
                        float dh = TextH(dt, dw);
                        Place(dt.rectTransform, new Vector2(0f, 1f), new Vector2(dx, -y - dy), new Vector2(dw, dh + 2f));
                        h = Mathf.Max(kt != null && !stacked ? TextH(kt, kw) : 0f, dh + dy);
                    }
                    else
                    {
                        float dw = row.k.Length == 0 || stacked ? cellW : dW;
                        probe.text = row.d; float dh = TextH(probe, dw);
                        float kh = 0f; if (row.k.Length > 0) { probe.text = row.k; kh = TextH(probe, stacked ? cellW : kw); }
                        h = stacked ? kh + 2f + dh : Mathf.Max(kh, dh);
                    }
                    rh = Mathf.Max(rh, h);
                }
                y += rh + 7f * fs;
            }
            y += ip - 4f;
            if (build) ((RectTransform)sec).sizeDelta = new Vector2(w, y);
            if (span) for (int i = 0; i < cols; i++) colY[i] = y0 + y + gap; else colY[c] = y0 + y + gap;
        }
        float mx = 0f; foreach (float cy in colY) mx = Mathf.Max(mx, cy);
        return Mathf.Max(0f, mx - gap);
    }

    Text measure;
    Text MeasureText(int size)
    {
        if (measure == null)
        {
            measure = UIK.Label(hpRoot, "", size, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(-5000f, 0f), new Vector2(100f, 10f), Color.clear);
            UIK.Modernize(measure, false, 0f);
        }
        measure.fontSize = size;
        return measure;
    }

    // ---------------- settings + scrolling ----------------
    Vehicle HelpVehicle { get { if (helpBy == null || helpBy.frog < 0 || helpBy.frog >= frogs.Count) return null; Frog f = frogs[helpBy.frog]; return f != null && BigWheels.Resizable(f.vehicle) ? f.vehicle : null; } }

    void HelpSettingsDraw(InputKind kind, Vehicle v)
    {
        Vehicle hv = HelpVehicle;
        bool on = hv != null;
        float w = wheelTrack.rectTransform.rect.width, k = on ? BigWheels.Want(hv) : 1f, fr = BigWheels.FracOf(k);
        wheelFill.rectTransform.sizeDelta = new Vector2(Mathf.Max(22f, w * fr), 22f);
        wheelKnob.rectTransform.anchoredPosition = new Vector2(w * fr, 0f);
        wheelTrack.color = on ? new Color(1f, 1f, 1f, 0.16f) : new Color(1f, 1f, 1f, 0.07f);
        wheelFill.color = on ? new Color(0.55f, 1f, 0.44f, 0.95f) : new Color(0.6f, 0.6f, 0.6f, 0.35f);
        wheelKnob.color = !on ? new Color(0.6f, 0.6f, 0.6f, 0.6f) : wheelDrag ? new Color(1f, 0.85f, 0.3f) : Color.white;
        string l = on ? "WHEEL SIZE  " + BigWheels.LabelOf(k) : "WHEEL SIZE";
        if (wheelLabel.text != l) wheelLabel.text = l;
        wheelLabel.color = on ? new Color(1f, 0.9f, 0.5f) : new Color(0.65f, 0.67f, 0.7f);
        string how = kind == InputKind.Gamepad ? "D-pad < > or LB / RB" : kind == InputKind.Touch ? "drag the bar" : "arrow keys / - = or drag the bar";
        string hint = !on ? "Get in a vehicle to resize its wheels" : hv.Title + " only  ·  1x - 4x  ·  " + how + (BigWheels.Capped(hv) && k > 1.001f ? "  ·  stock size indoors, grows outside" : "");
        if (hpWheelHint.text != hint) hpWheelHint.text = hint;
        hpWheelHint.color = on ? new Color(0.8f, 0.84f, 0.9f) : new Color(1f, 0.8f, 0.45f);
        string snd = "SOUND  <color=#8cff70>" + Sfx.LevelName + "</color>   " + (kind == InputKind.Gamepad ? G("Y") + " cycles" : kind == InputKind.Touch ? "SND button cycles" : "M cycles") + " on / low / off";
        if (hpSound.text != snd) hpSound.text = snd;
    }

    void HelpScrollInput(InputKind kind, float dt)
    {
        float d = 0f;
        foreach (var s in slots)
        {
            if (s.kind != InputKind.Gamepad || s.pad == null || !s.pad.added) continue;
            float y = s.pad.leftStick.ReadValue().y + s.pad.rightStick.ReadValue().y;
            if (Mathf.Abs(y) > 0.25f) d -= y * 900f * dt;
            if (s.pad.dpad.up.wasPressedThisFrame) d -= 120f;
            if (s.pad.dpad.down.wasPressedThisFrame) d += 120f;
        }
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.upArrowKey.isPressed || kb.pageUpKey.wasPressedThisFrame) d -= kb.pageUpKey.wasPressedThisFrame ? 400f : 700f * dt;
            if (kb.downArrowKey.isPressed || kb.pageDownKey.wasPressedThisFrame) d += kb.pageDownKey.wasPressedThisFrame ? 400f : 700f * dt;
        }
        float sc = Kb.Scroll(); if (Mathf.Abs(sc) > 0.01f) d -= sc / 120f * 90f;
        // finger / mouse drag inside the viewport (not on the slider)
        Vector2 cs = ((RectTransform)hudCanvas.transform).rect.size;
        float ky = cs.y / Mathf.Max(1, Screen.height);
        Vector2 pt = Vector2.zero; bool down = false, began = false;
        if (Kb.TouchCount() > 0) { try { var t = Input.GetTouch(0); pt = t.position; down = t.phase != UnityEngine.TouchPhase.Ended && t.phase != UnityEngine.TouchPhase.Canceled; began = t.phase == UnityEngine.TouchPhase.Began; } catch { } }
        else { var m = Mouse.current; if (m != null && Kb.MouseLeft()) { pt = m.position.ReadValue(); down = true; began = Kb.MouseLeftDown(); } }
        if (began && Hit(hpView, pt, 0f) && !OnWheelBar(pt)) { hpDragging = true; hpDragLast = pt; }
        if (hpDragging && down) { d += (pt.y - hpDragLast.y) * ky; hpDragLast = pt; }
        if (!down) hpDragging = false;
        hpScroll = Mathf.Clamp(hpScroll + d, 0f, hpMaxScroll);
        hpContent.anchoredPosition = new Vector2(0f, hpScroll);
        bool scrolls = hpMaxScroll > 1f;
        if (hpBar.gameObject.activeSelf != scrolls) hpBar.gameObject.SetActive(scrolls);
        if (scrolls)
        {
            float vh = hpView.rect.height, total = vh + hpMaxScroll;
            float th = Mathf.Max(30f, vh * vh / total);
            hpBarThumb.sizeDelta = new Vector2(5f, th);
            hpBarThumb.anchoredPosition = new Vector2(0f, -(vh - th) * (hpScroll / hpMaxScroll));
        }
        string sh = !scrolls ? "" : hpScroll >= hpMaxScroll - 1f ? "end of list" : kind == InputKind.Gamepad ? "more below  ·  stick / D-pad scrolls" : kind == InputKind.Touch ? "more below  ·  drag to scroll" : "more below  ·  wheel / arrow keys scroll";
        if (hpScrollHint.text != sh) hpScrollHint.text = sh;
    }

    // ---------------- in-vehicle wheel-size shortcut ----------------
    // pad: hold View + D-pad up / down (View alone still toggles shared / split, on release); keys: [ / ]
    PIn WheelShortcut(Slot s, PIn i)
    {
        Frog f = frogs[s.frog];
        int d = 0;
        if (s.kind == InputKind.Gamepad && s.pad != null && s.pad.added)
        {
            bool held = s.pad.selectButton.isPressed, was; viewHeld.TryGetValue(s, out was);
            bool combo; viewCombo.TryGetValue(s, out combo);
            if (held)
            {
                if (s.pad.dpad.up.wasPressedThisFrame) { d = 1; combo = true; }
                if (s.pad.dpad.down.wasPressedThisFrame) { d = -1; combo = true; }
                i.zoom = 0f;
            }
            i.view = false;
            if (was && !held) { if (!combo) i.view = true; combo = false; }
            viewHeld[s] = held; viewCombo[s] = combo;
        }
        else if (s.kind == InputKind.Keyboard)
        {
            var kb = Keyboard.current;
            if (kb != null && !Kb.typing) { if (kb.rightBracketKey.wasPressedThisFrame) d = 1; else if (kb.leftBracketKey.wasPressedThisFrame) d = -1; }
        }
        if (d != 0 && f != null)
        {
            if (BigWheels.Resizable(f.vehicle))
            {
                BigWheels.NudgeFor(f.vehicle, d, s.kind == InputKind.Gamepad ? "View + D-pad" : "[ ]");
                f.Toast(BigWheels.Readout(f.vehicle), 1.5f);
                Sfx.Play(Sfx.Click, 0.5f, d > 0 ? 1.1f : 0.9f);
            }
            else f.Toast("Get in a vehicle to resize its wheels", 1.4f);
        }
        return i;
    }
}
