using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ffu13: on-screen letter / number pad for the lobby (room code to JOIN, the player's own name). Works with
// touch (tap keys), mouse (click), keyboard (type, Backspace, Enter, Esc) and gamepads (D-pad / stick move,
// A presses the key, B deletes (or cancels when empty), Start = OK). Name mode accepts A-Z only, max 4.
public class Keypad
{
    public bool open;
    public bool nameMode;
    public string text = "";
    System.Action<string> onOk;
    System.Action onCancel;

    readonly RectTransform root;
    readonly Image shade, panel, entryBg;
    readonly Text title, entry, hint;
    readonly List<Image> keys = new List<Image>();
    readonly List<Text> keyTexts = new List<Text>();
    readonly List<string> vals = new List<string>();
    int cols, sel, builtLayout = -1;
    bool builtName;
    float navT, pressT, errT, openT;
    string err = "";
    bool hooked;
    readonly System.Text.StringBuilder typed = new System.Text.StringBuilder();

    const string Letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public Keypad(Transform canvas)
    {
        root = UIK.Rect(canvas, "Keypad", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        UIK.Stretch(root);
        shade = UIK.Img(root, null, new Color(0f, 0f, 0f, 0.78f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        UIK.Stretch(shade.rectTransform);
        panel = UIK.Img(root, null, new Color(0.03f, 0.1f, 0.06f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 640));
        title = UIK.Label(root, "", 34, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 270), new Vector2(980, 50), new Color(0.55f, 1f, 0.45f));
        entryBg = UIK.Img(root, null, new Color(0f, 0f, 0f, 0.55f), new Vector2(0.5f, 0.5f), new Vector2(0, 205), new Vector2(420, 72));
        entry = UIK.Label(root, "", 52, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 205), new Vector2(420, 72), Color.white);
        hint = UIK.Label(root, "", 19, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -282), new Vector2(980, 50), new Color(1f, 1f, 1f, 0.85f));
        root.gameObject.SetActive(false);
    }

    public void Open(bool name, string initial, System.Action<string> ok, System.Action cancel)
    {
        nameMode = name;
        text = initial ?? "";
        onOk = ok; onCancel = cancel;
        open = true; Kb.typing = true;
        err = ""; errT = 0f; sel = 0; openT = Time.unscaledTime;
        typed.Length = 0;
        builtLayout = -1;
        root.gameObject.SetActive(true);
        root.SetAsLastSibling();
    }

    public void Close()
    {
        open = false; Kb.typing = false;
        root.gameObject.SetActive(false);
    }

    void OnChar(char c) { if (open) typed.Append(c); }

    void Build(bool portrait)
    {
        foreach (var k in keys) Object.Destroy(k.gameObject);
        foreach (var t in keyTexts) Object.Destroy(t.gameObject);
        keys.Clear(); keyTexts.Clear(); vals.Clear();
        string chars = nameMode ? Letters : Net.CodeChars;
        cols = portrait ? 6 : (nameMode ? 9 : 8);
        float kw = portrait ? 100f : 92f, kh = portrait ? 84f : 60f, gap = portrait ? 10f : 8f;
        int rows = (chars.Length + cols - 1) / cols;
        float gridW = cols * kw + (cols - 1) * gap;
        float top = portrait ? 300f : 140f;
        for (int i = 0; i < chars.Length; i++)
        {
            int r = i / cols, c = i % cols;
            Vector2 p = new Vector2(-gridW * 0.5f + kw * 0.5f + c * (kw + gap), top - r * (kh + gap));
            AddKey(chars[i].ToString(), p, new Vector2(kw, kh), portrait ? 40 : 32);
        }
        float ay = top - rows * (kh + gap) - (portrait ? 14f : 8f);
        float aw = portrait ? 205f : 220f, ah = portrait ? 92f : 62f;
        AddKey("DEL", new Vector2(-(aw + gap), ay), new Vector2(aw, ah), portrait ? 30 : 26);
        AddKey("CANCEL", new Vector2(0f, ay), new Vector2(aw, ah), portrait ? 30 : 26);
        AddKey(nameMode ? "OK" : "JOIN", new Vector2(aw + gap, ay), new Vector2(aw, ah), portrait ? 34 : 28);
        float h = portrait ? 1180f : 650f;
        panel.rectTransform.sizeDelta = portrait ? new Vector2(700f, h) : new Vector2(Mathf.Max(gridW + 80f, 900f), h);
        title.rectTransform.anchoredPosition = new Vector2(0f, portrait ? 540f : 285f);
        title.rectTransform.sizeDelta = new Vector2(portrait ? 680f : 980f, 90f);
        title.fontSize = portrait ? 32 : 32;
        entryBg.rectTransform.anchoredPosition = entry.rectTransform.anchoredPosition = new Vector2(0f, portrait ? 440f : 215f);
        hint.rectTransform.anchoredPosition = new Vector2(0f, portrait ? -520f : -290f);
        hint.rectTransform.sizeDelta = new Vector2(portrait ? 680f : 980f, portrait ? 110f : 50f);
        if (sel >= keys.Count) sel = 0;
    }

    void AddKey(string v, Vector2 p, Vector2 size, int font)
    {
        var img = UIK.Img(root, null, new Color(1f, 1f, 1f, 0.14f), new Vector2(0.5f, 0.5f), p, size);
        var t = UIK.Label(img.transform, v, font, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, size, Color.white);
        UIK.Stretch(t.rectTransform);
        keys.Add(img); keyTexts.Add(t); vals.Add(v);
    }

    int MaxLen { get { return nameMode ? 4 : 4; } }

    void Type(string v)
    {
        if (v == "DEL") { if (text.Length > 0) text = text.Substring(0, text.Length - 1); err = ""; return; }
        if (v == "CANCEL") { Cancel(); return; }
        if (v == "OK" || v == "JOIN") { Confirm(); return; }
        char c = char.ToUpperInvariant(v[0]);
        bool okChar = nameMode ? (c >= 'A' && c <= 'Z') : Net.CodeChars.IndexOf(c) >= 0;
        if (!okChar)
        {
            Flash(nameMode ? "Letters A-Z only" : (c == 'O' || c == '0' || c == 'I' || c == '1' ? "Room codes never use O, 0, I or 1" : "Letters and numbers from the room code only"));
            return;
        }
        if (text.Length >= MaxLen) { Flash(nameMode ? "Max 4 letters" : "Room codes are 4 characters"); return; }
        text += c; err = "";
        Sfx.Play(Sfx.Click, 0.4f, 1.2f);
    }

    void Flash(string s) { err = s; errT = 2f; Sfx.Play(Sfx.Click, 0.5f, 0.6f); }

    void Confirm()
    {
        if (nameMode)
        {
            string n = Net.CleanName(text);
            if (n == null) { Flash("Letters A-Z only, max 4"); return; }
            Close(); if (onOk != null) onOk(n);
            return;
        }
        string c = Net.CleanCode(text);
        if (c.Length < 4) { Flash("Type all 4 characters of the room code"); return; }
        Close(); if (onOk != null) onOk(c);
    }

    void Cancel() { Close(); if (onCancel != null) onCancel(); }

    static bool Hit(Image img, Vector2 sp) { return img != null && RectTransformUtility.RectangleContainsScreenPoint(img.rectTransform, sp, null); }

    // returns true while open (the lobby skips its own input)
    public bool Update(bool portrait)
    {
        if (!open) return false;
        int lay = portrait ? 1 : 0;
        if (lay != builtLayout || builtName != nameMode) { builtLayout = lay; builtName = nameMode; Build(portrait); }
        if (!hooked && Keyboard.current != null) { Keyboard.current.onTextInput += OnChar; hooked = true; }
        float now = Time.unscaledTime;
        bool fresh = now - openT < 0.25f;      // ignore the press that opened the pad

        // keyboard
        if (!fresh)
        {
            for (int i = 0; i < typed.Length; i++)
            {
                char c = typed[i];
                if (c < ' ' || c == 127) continue;    // control chars (Backspace / Enter arrive as keys)
                if (c == ' ') { Flash(nameMode ? "Letters A-Z only - no spaces" : "No spaces in room codes"); continue; }
                Type(c.ToString());
                if (!open) break;
            }
        }
        typed.Length = 0;
        if (!open) return false;
        if (!fresh && Kb.BackDown()) Type("DEL");
        if (!fresh && Kb.EnterDown()) { Confirm(); if (!open) return true; }
        if (Kb.EscDown()) { Cancel(); return true; }

        // gamepads (any pad; a short global lock stops a mirrored "ghost" pad from double-moving the cursor)
        foreach (Gamepad pad in Gamepad.all)
        {
            if (pad == null) continue;
            Vector2 st = Pads.Dead(pad.leftStick.ReadValue(), 0.5f);
            int dx = (pad.dpad.right.isPressed || st.x > 0.5f ? 1 : 0) - (pad.dpad.left.isPressed || st.x < -0.5f ? 1 : 0);
            int dy = (pad.dpad.down.isPressed || st.y < -0.5f ? 1 : 0) - (pad.dpad.up.isPressed || st.y > 0.5f ? 1 : 0);
            bool edge = pad.dpad.right.wasPressedThisFrame || pad.dpad.left.wasPressedThisFrame || pad.dpad.up.wasPressedThisFrame || pad.dpad.down.wasPressedThisFrame;
            if ((dx != 0 || dy != 0) && (edge ? now - navT > 0.09f : now - navT > 0.2f)) { navT = now; Move(dx, dy); }
            if (fresh || now - pressT < 0.15f) continue;
            if (pad.buttonSouth.wasPressedThisFrame) { pressT = now; Type(vals[sel]); if (!open) return true; }
            else if (pad.buttonEast.wasPressedThisFrame) { pressT = now; if (text.Length > 0) Type("DEL"); else { Cancel(); return true; } }
            else if (pad.startButton.wasPressedThisFrame) { pressT = now; Confirm(); if (!open) return true; }
        }

        // touch + mouse
        var taps = Kb.TouchesBegan();
        if (Kb.MouseLeftDown() && Kb.TouchCount() == 0 && Mouse.current != null) taps.Add(Mouse.current.position.ReadValue());
        if (!fresh)
            foreach (Vector2 tp in taps)
            {
                for (int i = 0; i < keys.Count; i++)
                    if (Hit(keys[i], tp)) { sel = i; Type(vals[i]); break; }
                if (!open) return true;
            }

        // draw
        string shown = text;
        for (int i = text.Length; i < MaxLen; i++) shown += "_";
        entry.text = string.Join(" ", System.Array.ConvertAll(shown.ToCharArray(), ch => ch.ToString()));
        title.text = nameMode ? "YOUR NAME  <size=22>(letters A-Z, max 4)</size>" : "ROOM CODE  <size=22>(from the host's screen)</size>";
        if (errT > 0f) errT -= Time.unscaledDeltaTime;
        hint.text = errT > 0f ? "<color=#ff8a80>" + err + "</color>" :
            (nameMode ? "Leave it empty to use your froggy's own name.  " : "") +
            (portrait ? "\nTap keys.  Gamepad: D-pad move, A press, B delete, Start OK.  Keyboard: type, Enter, Esc."
                      : "Tap / click keys  |  Gamepad: D-pad move, A press, B delete, Start OK  |  Keyboard: type, Enter OK, Esc cancel");
        for (int i = 0; i < keys.Count; i++)
        {
            bool s = i == sel;
            string v = vals[i];
            Color baseC = v == "OK" || v == "JOIN" ? new Color(0.2f, 0.65f, 0.25f, 0.9f) : v == "CANCEL" ? new Color(0.6f, 0.2f, 0.2f, 0.75f) : v == "DEL" ? new Color(0.5f, 0.4f, 0.1f, 0.75f) : new Color(1f, 1f, 1f, 0.14f);
            keys[i].color = s ? new Color(1f, 0.85f, 0.25f, 0.95f) : baseC;
            keyTexts[i].color = s ? Color.black : Color.white;
        }
        return true;
    }

    void Move(int dx, int dy)
    {
        int nChars = keys.Count - 3;
        int rows = (nChars + cols - 1) / cols;
        bool inAction = sel >= nChars;
        if (inAction)
        {
            int a = sel - nChars;
            if (dx != 0) a = Mathf.Clamp(a + dx, 0, 2);
            if (dy < 0) { int col = Mathf.Clamp(Mathf.RoundToInt((a + 0.5f) / 3f * cols - 0.5f), 0, cols - 1); int idx = (rows - 1) * cols + col; sel = Mathf.Min(idx, nChars - 1); return; }
            sel = nChars + a; return;
        }
        int r = sel / cols, c = sel % cols;
        if (dx != 0) { c = (c + dx + cols) % cols; int i2 = r * cols + c; if (i2 >= nChars) i2 = dx > 0 ? r * cols : nChars - 1; sel = i2; }
        if (dy != 0)
        {
            int nr = r + dy;
            if (nr < 0) nr = 0;
            int i2 = nr * cols + c;
            if (nr >= rows || i2 >= nChars) { sel = nChars + Mathf.Clamp(Mathf.FloorToInt((float)c / cols * 3f), 0, 2); return; }
            sel = i2;
        }
        Sfx.Play(Sfx.Click, 0.25f, 1.5f);
    }
}
