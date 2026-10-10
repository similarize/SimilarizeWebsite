using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ffu24: the lobby STORY button opens this episode picker. Two glass cards in the ffu17 lobby style (FFDisplay /
// FFBody faces): episode number, title, one-line blurb, chapter count, progress read from each episode's own
// localStorage save, and CONTINUE / START OVER. Landscape = side by side, portrait = stacked.
// Keyboard: arrows choose, Enter / Space continue, Y or N start over, Esc / Backspace back.
// Gamepad: D-pad or stick choose, A continue, Y or X start over, B back. Touch / mouse: tap a card, tap its buttons, BACK.
public class StoryPicker
{
    class Card
    {
        public RectTransform rt;
        public Image glow, body, edge, disc, barBg, barFill, btnA, btnB;
        public Text kicker, title, blurb, info, prog, num, btnAT, btnBT;
        public bool hasSave, done;
        public float k01;
        public int ch;
        public Color accent;
    }
    readonly Canvas canvas;
    readonly RectTransform root;
    readonly Image dim, backBtn;
    readonly Text head, sub, hints, backT;
    readonly Card[] cards = new Card[2];
    int layout = -1;
    public bool IsOpen { get; private set; }
    public int sel;
    float openT, stickT, popK;
    public System.Action<int, bool> onPick;     // episode (1 / 2), startOver
    public System.Action onClose;

    static readonly string[] Kick = { "EPISODE 1", "EPISODE 2" };
    static readonly string[] Name = { "The Big Launch", Story.Ep2Name };
    static readonly string[] Blurb =
    {
        "The Mars pups are stuck in a cave! Build a rocket from parts all over the ranch - and never give up.",
        "Jimmy's new jetpack flies REALLY far. Chase him from Mars to Callisto, Mercury, Saturn and deep under Mars!"
    };
    static readonly Color[] Accent = { new Color(0.98f, 0.55f, 0.16f), new Color(0.65f, 0.86f, 0.2f) };

    public StoryPicker()
    {
        canvas = UIK.MakeCanvas("StoryPicker", null, 150, true);
        canvas.GetComponent<CanvasScaler>().screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        root = (RectTransform)canvas.transform;
        dim = UIK.Img(root, UIK.Gradient(new Color(0.01f, 0.03f, 0.05f, 0.92f), new Color(0.02f, 0.05f, 0.07f, 0.8f), new Color(0.01f, 0.02f, 0.04f, 0.94f)), Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        UIK.Stretch(dim.rectTransform);
        dim.raycastTarget = false;
        head = L(root, "STORY MODE", 46, TextAnchor.MiddleCenter, Color.white, true);
        var g = head.gameObject.AddComponent<UIGradient>(); g.top = new Color(0.8f, 1f, 0.7f); g.bottom = new Color(0.35f, 0.9f, 0.85f);
        sub = L(root, "Pick an episode", 20, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.75f), false);
        hints = L(root, "", 16, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.7f), false);
        backBtn = UIK.Glass(root, new Color(1f, 1f, 1f, 0.12f), Vector2.zero, new Vector2(130, 46));
        backT = L(backBtn.transform, "BACK", 18, TextAnchor.MiddleCenter, Color.white, true);
        UIK.Stretch(backT.rectTransform);
        for (int i = 0; i < 2; i++) cards[i] = MakeCard(i);
        canvas.enabled = false;
    }

    static Text L(Transform p, string s, int size, TextAnchor a, Color col, bool display)
    {
        var t = UIK.Label(p, s, size, a, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 40), col);
        UIK.Modernize(t, display, 0.45f);
        t.supportRichText = true;
        return t;
    }

    Card MakeCard(int i)
    {
        var c = new Card { accent = Accent[i] };
        c.rt = UIK.Rect(root, "Card" + (i + 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500, 400));
        c.glow = UIK.Sliced(c.rt, UIK.SoftRect, new Color(c.accent.r, c.accent.g, c.accent.b, 0.3f), 1f);
        UIK.Anchor(c.glow.rectTransform, Vector2.zero, Vector2.one, new Vector2(-40, -40), new Vector2(40, 40));
        c.body = UIK.Glass(c.rt, new Color(0.04f, 0.08f, 0.1f, 0.88f), Vector2.zero, Vector2.zero);
        UIK.Stretch(c.body.rectTransform);
        c.edge = UIK.Sliced(c.rt, UIK.Edge2, new Color(1f, 1f, 1f, 0.25f), 2f);
        UIK.Stretch(c.edge.rectTransform);
        c.disc = UIK.Img(c.rt, UIK.Circle, c.accent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84, 84));
        c.num = L(c.disc.transform, (i + 1).ToString(), 46, TextAnchor.MiddleCenter, Color.white, true);
        UIK.Stretch(c.num.rectTransform);
        c.kicker = L(c.rt, Kick[i], 16, TextAnchor.MiddleLeft, c.accent, true);
        c.title = L(c.rt, Name[i], 34, TextAnchor.MiddleLeft, Color.white, true);
        c.title.resizeTextForBestFit = true; c.title.resizeTextMinSize = 20; c.title.resizeTextMaxSize = 34;
        c.blurb = L(c.rt, Blurb[i], 18, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.86f), false);
        c.info = L(c.rt, "7 chapters", 15, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.6f), false);
        c.prog = L(c.rt, "", 18, TextAnchor.MiddleLeft, Color.white, false);
        c.barBg = UIK.Glass(c.rt, new Color(1f, 1f, 1f, 0.12f), Vector2.zero, new Vector2(420, 10));
        c.barFill = UIK.Glass(c.barBg.transform, c.accent, Vector2.zero, new Vector2(0, 10));
        c.barFill.rectTransform.anchorMin = c.barFill.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        c.barFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        c.btnA = UIK.Glass(c.rt, new Color(0.18f, 0.66f, 0.3f, 0.95f), Vector2.zero, new Vector2(210, 56));
        c.btnAT = L(c.btnA.transform, "CONTINUE", 19, TextAnchor.MiddleCenter, Color.white, true); UIK.Stretch(c.btnAT.rectTransform);
        c.btnB = UIK.Glass(c.rt, new Color(1f, 1f, 1f, 0.14f), Vector2.zero, new Vector2(210, 56));
        c.btnBT = L(c.btnB.transform, "START OVER", 19, TextAnchor.MiddleCenter, Color.white, true); UIK.Stretch(c.btnBT.rectTransform);
        return c;
    }

    // read both saves (episode 1 "bl1|ch|step|parts", episode 2 "cj1|ch|step")
    void ReadSaves()
    {
        for (int i = 0; i < 2; i++)
        {
            var c = cards[i];
            string v = Story.ReadSave(i == 0 ? Story.SaveKey : Story.SaveKey2);
            var a = v.Split('|');
            int ch = 0, st = 0, pt = 0;
            bool ok = a.Length >= 3 && a[0] == (i == 0 ? "bl1" : "cj1") && int.TryParse(a[1], out ch) && int.TryParse(a[2], out st);
            if (ok && i == 0 && a.Length >= 4) int.TryParse(a[3], out pt);
            if (ok && ch == 1 && st == 0 && pt == 0) ok = false;
            c.hasSave = ok && ch >= 1; c.done = ok && ch >= 8; c.ch = ch;
            string[] titles = i == 0 ? Story.ChTitle : Story.ChTitle2;
            if (!c.hasSave) c.prog.text = "<color=#ffffffaa>Not started yet</color>";
            else if (c.done) c.prog.text = "<color=#b6ff5a>COMPLETED!</color>  <color=#ffffffaa>all 7 chapters</color>";
            else c.prog.text = "Chapter " + ch + " of 7  ·  <color=#ffffffcc>" + titles[Mathf.Clamp(ch, 1, 7)] + "</color>";
            float k = !c.hasSave ? 0f : c.done ? 1f : (ch - 1) / 7f;
            c.k01 = Mathf.Max(k, c.hasSave ? 0.03f : 0f);
            c.barFill.enabled = c.hasSave;
            c.btnAT.text = !c.hasSave ? "START" : c.done ? "REPLAY FINALE" : "CONTINUE";
            c.btnBT.text = c.done ? "PLAY AGAIN" : "START OVER";
            c.btnB.gameObject.SetActive(c.hasSave);
        }
    }

    public void Show(int preselect)
    {
        ReadSaves();
        sel = Mathf.Clamp(preselect, 0, 1);
        IsOpen = true; canvas.enabled = true; openT = 0f; popK = 0f; layout = -1;
        Debug.Log("FFSTORY picker open (sel " + (sel + 1) + ")");
    }
    public void Hide() { IsOpen = false; canvas.enabled = false; }

    void Layout(bool portrait, string hintText)
    {
        int want = portrait ? 1 : 0;
        hints.text = hintText;
        if (want == layout) return;
        layout = want;
        var sc = canvas.GetComponent<CanvasScaler>();
        sc.referenceResolution = portrait ? new Vector2(720, 1280) : new Vector2(1280, 720);
        System.Action<RectTransform, float, float, float, float> put = (r, x, y, w, h) => { r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h); };
        if (portrait)
        {
            put(head.rectTransform, 0, 540, 680, 64); head.fontSize = 44;
            put(sub.rectTransform, 0, 492, 680, 30);
            put(hints.rectTransform, 0, -560, 680, 60);
            put(backBtn.rectTransform, -270, 600, 130, 46);
            for (int i = 0; i < 2; i++) put(cards[i].rt, 0, i == 0 ? 220 : -230, 640, 410);
        }
        else
        {
            put(head.rectTransform, 0, 292, 900, 60); head.fontSize = 46;
            put(sub.rectTransform, 0, 248, 900, 30);
            put(hints.rectTransform, 0, -312, 1200, 30);
            put(backBtn.rectTransform, -560, 300, 130, 46);
            for (int i = 0; i < 2; i++) put(cards[i].rt, i == 0 ? -280 : 280, -16, 520, 410);
        }
        for (int i = 0; i < 2; i++)
        {
            var c = cards[i];
            float w = c.rt.sizeDelta.x, half = w * 0.5f;
            put(c.disc.rectTransform, -half + 70, 140, 84, 84);
            put(c.kicker.rectTransform, 60, 158, w - 160, 24);
            put(c.title.rectTransform, 60, 122, w - 160, 44);
            put(c.blurb.rectTransform, 0, 38, w - 60, 92);
            put(c.info.rectTransform, 0, -36, w - 60, 22);
            put(c.prog.rectTransform, 0, -68, w - 60, 26);
            put(c.barBg.rectTransform, 0, -96, w - 60, 10);
        }
    }

    void LayoutButtons()
    {
        for (int i = 0; i < 2; i++)
        {
            var c = cards[i];
            float w = c.rt.sizeDelta.x;
            if (c.hasSave)
            {
                c.btnA.rectTransform.anchoredPosition = new Vector2(-w * 0.22f, -152); c.btnA.rectTransform.sizeDelta = new Vector2(w * 0.4f, 56);
                c.btnB.rectTransform.anchoredPosition = new Vector2(w * 0.22f, -152); c.btnB.rectTransform.sizeDelta = new Vector2(w * 0.4f, 56);
            }
            else { c.btnA.rectTransform.anchoredPosition = new Vector2(0, -152); c.btnA.rectTransform.sizeDelta = new Vector2(w * 0.56f, 56); }
            c.barFill.rectTransform.sizeDelta = new Vector2(c.barBg.rectTransform.sizeDelta.x * c.k01, 10);
        }
    }

    static bool Hit(Graphic g, Vector2 sp) { return g != null && g.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(g.rectTransform, sp, null); }

    // one lobby frame while open; true = still open
    public bool Tick(bool portrait, InputKind kind, float dt)
    {
        if (!IsOpen) return false;
        string h = kind == InputKind.Gamepad ? "D-PAD choose  ·  A play  ·  Y start over  ·  B back"
                 : kind == InputKind.Touch ? "tap a card to choose  ·  tap CONTINUE or START OVER"
                 : "ARROWS choose  ·  ENTER play  ·  Y start over  ·  ESC back";
        Layout(portrait, h);
        LayoutButtons();
        openT += dt;
        popK = Mathf.MoveTowards(popK, 1f, dt * 5f);
        bool ready = openT > 0.2f;
        int dir = 0; bool go = false, over = false, back = false;
        var k = Keyboard.current;
        if (ready && k != null && !Kb.typing)
        {
            if (k.leftArrowKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame) dir = -1;
            if (k.rightArrowKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame || k.tabKey.wasPressedThisFrame) dir = 1;
            if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame) go = true;
            if (k.yKey.wasPressedThisFrame || k.nKey.wasPressedThisFrame) over = true;
            if (k.escapeKey.wasPressedThisFrame || k.backspaceKey.wasPressedThisFrame) back = true;
        }
        stickT -= dt;
        foreach (var pad in Gamepad.all)
        {
            if (pad == null || !ready) continue;
            if (pad.dpad.left.wasPressedThisFrame || pad.dpad.up.wasPressedThisFrame) dir = -1;
            if (pad.dpad.right.wasPressedThisFrame || pad.dpad.down.wasPressedThisFrame) dir = 1;
            Vector2 st = pad.leftStick.ReadValue();
            if (st.magnitude > 0.6f && stickT <= 0f) { dir = (st.x + (portrait ? -st.y : 0f)) > 0f ? 1 : -1; if (Mathf.Abs(st.x) < 0.3f && !portrait) dir = 0; stickT = 0.35f; }
            if (st.magnitude < 0.3f) stickT = Mathf.Min(stickT, 0f);
            if (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame) go = true;
            if (pad.buttonNorth.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame) over = true;
            if (pad.buttonEast.wasPressedThisFrame) back = true;
        }
        if (ready)
        {
            var pts = new System.Collections.Generic.List<Vector2>(Kb.TouchesBegan());
            if (Kb.MouseLeftDown() && Kb.TouchCount() == 0 && Mouse.current != null) pts.Add(Mouse.current.position.ReadValue());
            foreach (var p in pts)
            {
                if (Hit(backBtn, p)) { back = true; break; }
                for (int i = 0; i < 2; i++)
                {
                    var c = cards[i];
                    if (Hit(c.btnA, p)) { sel = i; go = true; break; }
                    if (Hit(c.btnB, p)) { sel = i; over = true; break; }
                    if (Hit(c.body, p)) { if (sel != i) { sel = i; Sfx.Play(Sfx.Click, 0.5f, 1.2f); } }
                }
            }
        }
        if (dir != 0) { int ns = Mathf.Clamp(sel + dir, 0, 1); if (ns != sel) { sel = ns; popK = 0.6f; Sfx.Play(Sfx.Click, 0.5f, 1.2f); Debug.Log("FFSTORY picker sel " + (sel + 1)); } }
        // look: the selected card glows in its colour and pops forward
        float t = Time.unscaledTime;
        for (int i = 0; i < 2; i++)
        {
            var c = cards[i];
            bool on = i == sel;
            float s = on ? Mathf.Lerp(0.98f, 1.03f, popK) : 0.95f;
            c.rt.localScale = Vector3.Lerp(c.rt.localScale, Vector3.one * s, Mathf.Min(1f, dt * 12f));
            c.glow.color = new Color(c.accent.r, c.accent.g, c.accent.b, on ? 0.42f + 0.1f * Mathf.Sin(t * 3f) : 0.06f);
            c.edge.color = on ? new Color(c.accent.r, c.accent.g, c.accent.b, 0.95f) : new Color(1f, 1f, 1f, 0.2f);
            c.body.color = on ? new Color(0.05f, 0.1f, 0.12f, 0.92f) : new Color(0.04f, 0.07f, 0.09f, 0.8f);
            c.btnA.color = on ? new Color(0.18f, 0.68f, 0.3f, 1f) : new Color(0.18f, 0.5f, 0.28f, 0.6f);
            c.btnAT.text = (!c.hasSave ? "START" : c.done ? "REPLAY FINALE" : "CONTINUE") + (on && kind == InputKind.Gamepad ? "  (A)" : "");
            c.btnBT.text = (c.done ? "PLAY AGAIN" : "START OVER") + (on && kind != InputKind.Touch ? (kind == InputKind.Gamepad ? "  (Y)" : "  (Y)") : "");
        }
        if (back) { Sfx.Play(Sfx.Click, 0.6f, 0.9f); Hide(); Debug.Log("FFSTORY picker closed"); if (onClose != null) onClose(); return false; }
        if (go || (over && cards[sel].hasSave))
        {
            bool startOver = over || !cards[sel].hasSave;
            if (go && cards[sel].hasSave) startOver = false;
            Sfx.Play(Sfx.Confirm != null ? Sfx.Confirm : Sfx.Click, 0.8f);
            Hide();
            Debug.Log("FFSTORY picker pick episode " + (sel + 1) + (startOver ? " (start over)" : " (continue)"));
            if (onPick != null) onPick(sel + 1, startOver);
            return false;
        }
        return true;
    }
}
