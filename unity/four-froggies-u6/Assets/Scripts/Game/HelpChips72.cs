using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ffu72 (Bill, Oct 22 07:55): split screen had ONE MENU / Help chip (hpMenuChip, top centre of the right-most top view), so
// three of four views had no help button. Now every split view has its own chip at the top of its own viewport, with that
// player's device glyph (pad Menu / H key / MENU for touch) and a coloured P1..P4 tag; tapping / clicking a view's chip opens
// help FOR THAT VIEW'S PLAYER (helpBy = that slot, the ffu69b per-view help: HelpSplit / HelpVR), or moves an open menu to
// that player. A view's chip hides while that view's help or Robot Phone is open. Name tags stay under the chips
// (ViewHud.TopBand72). One view (single player / shared camera) keeps the old single chip.
public partial class Game
{
    class Chip72
    {
        public RectTransform rt; public Image cap; public Text key, label; public readonly List<Image> bars = new List<Image>(); public string sig = "";
        public bool shown; public Rect screen;
    }
    readonly Chip72[] chips72 = new Chip72[4];
    public static string Chip72Info = "";
    public static int Chip72Opens;
    public bool Chips72On { get { return !F72.Old && state == State.Play && slots.Count > 1 && LastViews > 1; } }
    public bool MainChipShown { get { return hpMenuChip != null && hpMenuChip.gameObject.activeSelf; } }

    Chip72 MakeChip72(int k)
    {
        var c = new Chip72();
        var g = UIK.Glass(hpMenuChip.parent, new Color(0.07f, 0.09f, 0.12f, 0.7f), Vector2.zero, new Vector2(92f, 40f));
        c.rt = g.rectTransform; c.rt.name = "HelpChip P" + (k + 1);
        c.rt.anchorMin = c.rt.anchorMax = Vector2.zero; c.rt.pivot = new Vector2(0.5f, 1f);
        c.rt.SetSiblingIndex(hpMenuChip.GetSiblingIndex());
        c.label = UIK.Label(c.rt, "Help", 16, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(92f, 40f), Color.white);
        UIK.Stretch(c.label.rectTransform); UIK.Modernize(c.label, true, 0.2f);
        c.label.supportRichText = true;
        c.cap = UIK.Img(c.rt, UIK.Round, new Color(1f, 1f, 1f, 0.92f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(28f, 26f));
        c.cap.type = Image.Type.Sliced;
        c.key = UIK.Label(c.cap.transform, "H", 16, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28f, 26f), new Color(0.08f, 0.1f, 0.12f));
        UIK.Modernize(c.key, true, 0f);
        foreach (var sh in c.key.GetComponents<Shadow>()) sh.enabled = false;
        for (int i = 0; i < 3; i++) c.bars.Add(UIK.Img(c.cap.transform, null, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, 5f - i * 5f), new Vector2(12f, 2f)));
        c.rt.gameObject.SetActive(false);
        return c;
    }

    // the same look as the single chip (Hints46.ChipTick), per player: device glyph + coloured P tag
    void StyleChip72(Chip72 c, int pn, InputKind k, bool narrow)
    {
        string sig = k + "|" + narrow + "|" + pn;
        if (sig == c.sig) return;
        c.sig = sig;
        bool keys = k == InputKind.Keyboard, pad = k == InputKind.Gamepad;
        c.cap.gameObject.SetActive(keys || pad);
        c.key.gameObject.SetActive(keys);
        foreach (var b in c.bars) b.gameObject.SetActive(pad);
        c.cap.sprite = pad ? UIK.Circle : UIK.Round;
        c.cap.type = pad ? Image.Type.Simple : Image.Type.Sliced;
        c.cap.color = pad ? new Color(0.22f, 0.24f, 0.27f, 0.95f) : new Color(1f, 1f, 1f, 0.92f);
        c.cap.rectTransform.sizeDelta = pad ? new Vector2(28f, 28f) : new Vector2(28f, 26f);
        string word = keys ? (narrow ? "Help" : "Help & controls") : pad ? "Help" : "MENU";
        c.label.text = "<color=" + PlayerHex(pn) + ">P" + (pn + 1) + "</color> " + word;
        c.label.fontSize = 16;
        float lw = c.label.preferredWidth + 2f;
        float x0 = keys || pad ? 8f + 28f + 9f : 14f;
        float w = x0 + lw + 14f;
        c.rt.sizeDelta = new Vector2(w, 40f);
        c.cap.rectTransform.anchoredPosition = new Vector2(8f + 14f, 0f);
        var lr = c.label.rectTransform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = new Vector2(x0, 0f); lr.offsetMax = new Vector2(-8f, 0f);
        c.label.alignment = TextAnchor.MiddleLeft;
        Debug.Log("HINTS: P" + (pn + 1) + " view chip '" + (keys ? "[H] " : pad ? "[Menu] " : "") + word + "' device " + k + " width " + w.ToString("0"));
    }

    static Rect ScreenRect72(RectTransform t) { var c = new Vector3[4]; t.GetWorldCorners(c); return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y); }

    void OpenHelp72(Slot s, string how)
    {
        int pn = slots.IndexOf(s);
        if (help && HelpSplit && helpBy != s) { helpBy = s; hpSig = ""; hpScroll = 0f; }
        else { help = true; helpBy = s; hpScroll = 0f; }
        if (how == "mouse") Cursor.lockState = CursorLockMode.None;
        Sfx.Play(Sfx.Click, 0.7f);
        Chip72Opens++;
        Debug.Log("HINTS: P" + (pn + 1) + " view chip opened help (" + how + ") for P" + (pn + 1));
    }

    // per frame from HelpTick (before the help screen itself): place / show / hit-test the per-view chips.
    void Chips72Tick()
    {
        bool on = Chips72On;
        float band = 0f;
        Vector2 cs = ((RectTransform)hudCanvas.transform).rect.size;
        float kx = cs.x / Mathf.Max(1, Screen.width), ky = cs.y / Mathf.Max(1, Screen.height);
        Rect sa = Screen.safeArea;
        var sb = on ? new System.Text.StringBuilder() : null;
        for (int k = 0; k < 4; k++)
        {
            Slot s = on && k < slots.Count ? slots[k] : null;
            bool show = s != null && s.cam != null && s.cam.enabled && !HelpFor(s);
            if (show && s.frog >= 0 && s.frog < 4 && RobotPhone.Seat[s.frog] != null && RobotPhone.Seat[s.frog].open) show = false;   // that player's phone is open
            if (show && chips72[k] == null) chips72[k] = MakeChip72(k);
            Chip72 c = chips72[k];
            if (c == null) continue;
            if (c.rt.gameObject.activeSelf != show) c.rt.gameObject.SetActive(show);
            c.shown = show;
            if (!show) continue;
            Rect r = s.cam.rect;
            float x0 = r.xMin * cs.x, x1 = r.xMax * cs.x, top = r.yMax * cs.y;
            if (r.yMax > 0.99f) top -= (Screen.height - sa.yMax) * ky;
            float vw = x1 - x0;
            StyleChip72(c, k, HintForce >= 0 ? (InputKind)HintForce : s.kind, vw < 640f);
            float w = c.rt.sizeDelta.x;
            // top centre of the view, clear of the view's "P2 name" title (top-left) and, top-right view, the page's buttons
            float titleR = s.hud != null ? s.hud.TitleRight72 : 160f;
            float right = x1 - w * 0.5f - 8f - (r.xMax > 0.99f && r.yMax > 0.99f ? 64f : 0f);
            float cx = Mathf.Max((x0 + x1) * 0.5f, x0 + titleR + 14f + w * 0.5f);
            float y = top - 10f;
            if (cx > right)
            {
                // too narrow for one row: second row, right of the PHONE button column
                cx = Mathf.Max((x0 + x1) * 0.5f, x0 + 176f + w * 0.5f);
                cx = Mathf.Min(cx, x1 - w * 0.5f - 6f);
                y = top - 52f;
            }
            c.rt.anchoredPosition = new Vector2(cx, y);
            c.screen = ScreenRect72(c.rt);
            band = Mathf.Max(band, (top - (y - 40f) + 10f) / ky);   // px from the view's top to under the chip
            sb.Append("P" + (k + 1) + " " + c.screen.ToString("0") + " ");
        }
        ViewHud.TopBand72 = band;
        if (!on) return;
        Chip72Info = sb.ToString();
        foreach (Vector2 tp in Kb.TouchesBegan())
            for (int k = 0; k < 4 && k < slots.Count; k++)
                if (chips72[k] != null && chips72[k].shown && Hit(chips72[k].rt, tp, 10f)) { OpenHelp72(slots[k], "touch"); return; }
        var cm = Mouse.current;
        if (cm != null && Kb.MouseLeftDown() && Cursor.lockState != CursorLockMode.Locked)
        {
            Vector2 mp = cm.position.ReadValue();
            for (int k = 0; k < 4 && k < slots.Count; k++)
                if (chips72[k] != null && chips72[k].shown && Hit(chips72[k].rt, mp, 4f)) { OpenHelp72(slots[k], "mouse"); return; }
        }
    }

    bool Chips72UnderMouse(Vector2 mp)
    {
        for (int k = 0; k < 4; k++) if (chips72[k] != null && chips72[k].shown && Hit(chips72[k].rt, mp, 4f)) return true;
        return false;
    }
    public bool Chip72Shown(int k) { return k >= 0 && k < 4 && chips72[k] != null && chips72[k].shown; }
    public Rect Chip72Rect(int k) { return k >= 0 && k < 4 && chips72[k] != null ? chips72[k].screen : new Rect(); }
    public string Chip72Label(int k) { return k >= 0 && k < 4 && chips72[k] != null ? chips72[k].label.text : ""; }
    public bool Chip72PadGlyph(int k) { return k >= 0 && k < 4 && chips72[k] != null && chips72[k].bars.Count > 0 && chips72[k].bars[0].gameObject.activeSelf; }
}
