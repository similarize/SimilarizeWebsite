using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ffu46 HELPER HINTS (Bill: "in each mode there should be helper hints so people know what they can do, like I didn't know
// about the H in desktop mode."). For keyboard + mouse, gamepad and touch:
//  * a persistent small chip at the top centre of the safe area naming the menu key of the device in use: keys = an "H" key
//    cap + "Help & controls" (clickable), pad = the Menu / Start glyph (three bars in a circle) + "Help", touch = MENU.
//  * context prompts in the HUD prompt line, in the device's own bindings / glyphs: on foot near something usable
//    ("E  Ride the red skateboard", pad "A  ...", touch "A  ..."); in a vehicle, for a few seconds after getting in (12 s the
//    very first time in that kind of vehicle, else 7 s): its 3 main controls + how to get out (+ "[ ]  wheel size" where
//    the vehicle has one) + the menu key - then the line clears (no clutter). Other contextual prompts (hitch / load /
//    stow...) show as before.
//  * a short first-time QUICK TIPS card when play starts, once per device (SaveStore ff.tips.<device>); the help screen's
//    SETTINGS has "HINTS On / Off" (N / pad X / click / tap). Off = no tips, no in-vehicle hint line; On again = the tips
//    show once more.
// Everything sits inside Screen.safeArea, the prompt line keeps clear of the touch controls (ViewHud bottom inset), and the
// tips card drops below the touch SND / zoom buttons on a phone in portrait. &hintdev=pad|keys|touch forces the device
// (probe / demo), &ffjoin=touch makes the demo's P1 a touch player.
public partial class Game
{
    static int hintsOn = -1;
    public static bool HintsOn { get { if (hintsOn < 0) hintsOn = SaveStore.GetInt("ff.hints", 1) != 0 ? 1 : 0; return hintsOn == 1; } }
    public static int HintForce = -1;
    public static void SetHintsOn(bool on)
    {
        SaveStore.SetInt("ff.hints", on ? 1 : 0); hintsOn = on ? 1 : 0;
        if (on) foreach (var k in new[] { InputKind.Keyboard, InputKind.Gamepad, InputKind.Touch }) SaveStore.SetInt("ff.tips." + k, 0);
        SaveStore.Flush();
        if (I != null && on) I.tipsDone = false;
        Debug.Log("HINTS: " + (on ? "on (tips will show again)" : "off"));
    }
    // first time ever in this kind of vehicle (longer hint)
    public static bool FirstUse(Vehicle v)
    {
        if (v == null) return false;
        string key = "ff.used." + v.GetType().Name;
        if (SaveStore.GetInt(key, 0) != 0) return false;
        SaveStore.SetInt(key, 1);
        return true;
    }
    public static InputKind HintKind(Frog f) { return HintForce >= 0 ? (InputKind)HintForce : f != null ? f.inputKind : InputKind.Keyboard; }

    static string Cap(string k, InputKind kind)
    {
        if (k.Length == 0) return "";
        return "<b>" + (kind == InputKind.Gamepad ? Pk(k) : "<color=#ffffff>" + k + "</color>") + "</b>";
    }
    public static string MenuKey(InputKind k) { return k == InputKind.Gamepad ? "Start" : k == InputKind.Touch ? "MENU" : "H"; }
    static string UseKey(InputKind k) { return k == InputKind.Gamepad ? "A" : k == InputKind.Touch ? "A" : "E"; }
    static string Up1(string s) { return s.Length > 0 ? char.ToUpperInvariant(s[0]) + s.Substring(1) : s; }
    static string Short(string d) { int i = d.IndexOf(" ("); if (i > 0) d = d.Substring(0, i); i = d.IndexOf(" - "); if (i > 0) d = d.Substring(0, i); return d.Trim(); }

    // what the HUD prompt line shows for this froggy (ViewHud)
    public static string HintPrompt(Frog f)
    {
        if (f == null) return "";
        string p = f.prompt ?? "";
        InputKind k = HintKind(f);
        Vehicle v = f.vehicle;
        if (v != null && p == v.HelpLine)
        {
            if (!HintsOn || !f.human) return "";
            float show = f.vehFirst ? 12f : 7f;
            if (f.vehT > show) return "";
            return VehicleHint(v, k);
        }
        if (p.StartsWith("A / E: ")) return Cap(UseKey(k), k) + "  " + Up1(p.Substring(7));
        return p;
    }

    public static string VehicleHint(Vehicle v, InputKind k)
    {
        var parts = new List<string>();
        string hf = v.HelpFor(k);
        if (hf != null)
        {
            var rows = hf.Split('|');
            for (int i = 0; i < rows.Length; i++)
            {
                if (i >= 3 && i != rows.Length - 1) continue;   // the 3 main controls + the last row (how to get off)
                int c = rows[i].IndexOf("::"); if (c < 0) continue;
                parts.Add(Cap(rows[i].Substring(0, c).Trim(), k) + " " + Short(rows[i].Substring(c + 2)));
            }
        }
        else
        {
            string outPart = null; int n = 0;
            foreach (string part in v.HelpLine.Split('|'))
            {
                string p = part.Trim(); if (p.Length == 0) continue;
                if (v.Title.Length > 0 && p.StartsWith(v.Title + ":")) p = p.Substring(v.Title.Length + 1).Trim();
                int colon = p.IndexOf(": "); if (colon > 0 && colon < 24 && p.Substring(0, colon).ToUpperInvariant() == p.Substring(0, colon)) p = p.Substring(colon + 2).Trim();   // "MOON BUGGY: ..."
                string key, d; SplitBinding(p, out key, out d);
                if (key.Length == 0) continue;
                string kk = k == InputKind.Gamepad ? (key.IndexOf(" | ") >= 0 ? key.Substring(0, key.IndexOf(" | ")) : key) : MapKey(key, k);
                string item = Cap(kk, k) + " " + Short(d);
                bool isOut = d.Contains("get out") || d.Contains("get off") || d.Contains("swim out");
                if (isOut) { outPart = item; continue; }
                if (n < 3) { parts.Add(item); n++; }
            }
            if (outPart != null) parts.Add(outPart);
        }
        if (BigWheels.Resizable(v)) parts.Add(k == InputKind.Gamepad ? Cap("View + D-pad", k) + " wheel size" : k == InputKind.Keyboard ? Cap("[  ]", k) + " wheel size" : Cap("PHONE", k) + " wheels tab");
        parts.Add(Cap(MenuKey(k), k) + " all controls");
        return string.Join("     ", parts.ToArray());
    }

    // ---------------- top-centre chip + first-time tips ----------------
    Image chipCap; Text chipKey, chipLabel; readonly List<Image> chipBars = new List<Image>();
    RectTransform tipsCard; Text tipsTitle, tipsBody, tipsFoot; CanvasGroup tipsGroup;
    float tipsT = -1f; bool tipsDone; InputKind tipsKind; string chipSig = "";
    public static string HintInfo = "";
    const float TipsSecs = 9f;

    void ChipBuild(Text menuLabel)
    {
        chipLabel = menuLabel;
        chipLabel.alignment = TextAnchor.MiddleLeft;
        chipCap = UIK.Img(hpMenuChip, UIK.Round, new Color(1f, 1f, 1f, 0.92f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(28f, 26f));
        chipCap.type = Image.Type.Sliced;
        chipKey = UIK.Label(chipCap.transform, "H", 16, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28f, 26f), new Color(0.08f, 0.1f, 0.12f));
        UIK.Modernize(chipKey, true, 0f);
        foreach (var sh in chipKey.GetComponents<Shadow>()) sh.enabled = false;
        for (int i = 0; i < 3; i++) chipBars.Add(UIK.Img(chipCap.transform, null, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, 5f - i * 5f), new Vector2(12f, 2f)));
        // the first-time tips card
        var card = UIK.Glass(hpMenuChip.parent, new Color(0.07f, 0.09f, 0.12f, 0.86f), Vector2.zero, new Vector2(600f, 150f));
        tipsCard = card.rectTransform; tipsCard.anchorMin = tipsCard.anchorMax = new Vector2(0.5f, 1f); tipsCard.pivot = new Vector2(0.5f, 1f);
        tipsGroup = tipsCard.gameObject.AddComponent<CanvasGroup>(); tipsGroup.blocksRaycasts = false; tipsGroup.interactable = false;
        tipsTitle = UIK.Label(tipsCard, "QUICK TIPS", 17, TextAnchor.UpperLeft, new Vector2(0f, 1f), Vector2.zero, new Vector2(560f, 24f), HpAccent);
        UIK.Modernize(tipsTitle, true, 0.2f);
        tipsBody = UIK.Label(tipsCard, "", 17, TextAnchor.UpperLeft, new Vector2(0f, 1f), Vector2.zero, new Vector2(560f, 80f), Color.white);
        UIK.Modernize(tipsBody, false, 0.2f); tipsBody.supportRichText = true;
        tipsFoot = UIK.Label(tipsCard, "", 13, TextAnchor.UpperLeft, new Vector2(0f, 1f), Vector2.zero, new Vector2(560f, 18f), new Color(1f, 1f, 1f, 0.6f));
        UIK.Modernize(tipsFoot, false, 0f);
        tipsCard.gameObject.SetActive(false);
    }

    InputKind ChipKind { get { if (HintForce >= 0) return (InputKind)HintForce; var hs = helpBy != null && slots.Contains(helpBy) ? helpBy : slots.Count > 0 ? slots[0] : null; return hs != null ? hs.kind : InputKind.Keyboard; } }
    bool NarrowHud { get { Vector2 cs = ((RectTransform)hudCanvas.transform).rect.size; return cs.x < 900f; } }

    void ChipTick(InputKind k)
    {
        bool narrow = NarrowHud;
        string sig = k + "|" + narrow + "|" + HintsOn;
        if (sig == chipSig) return;
        chipSig = sig;
        bool keys = k == InputKind.Keyboard, pad = k == InputKind.Gamepad;
        chipCap.gameObject.SetActive(keys || pad);
        chipKey.gameObject.SetActive(keys);
        foreach (var b in chipBars) b.gameObject.SetActive(pad);
        chipCap.sprite = pad ? UIK.Circle : UIK.Round;
        chipCap.type = pad ? Image.Type.Simple : Image.Type.Sliced;
        chipCap.color = pad ? new Color(0.22f, 0.24f, 0.27f, 0.95f) : new Color(1f, 1f, 1f, 0.92f);
        chipCap.rectTransform.sizeDelta = pad ? new Vector2(28f, 28f) : new Vector2(28f, 26f);
        chipLabel.text = keys ? (narrow ? "Help" : "Help & controls") : pad ? "Help" : "MENU";
        chipLabel.fontSize = 16;
        float lw = chipLabel.preferredWidth + 2f;
        float x0 = keys || pad ? 8f + 28f + 9f : 16f;
        float w = x0 + lw + 14f;
        hpMenuChip.sizeDelta = new Vector2(w, 40f);
        chipCap.rectTransform.anchoredPosition = new Vector2(8f + 14f, 0f);
        var lr = chipLabel.rectTransform; lr.anchorMin = new Vector2(0f, 0f); lr.anchorMax = new Vector2(1f, 1f); lr.offsetMin = new Vector2(x0, 0f); lr.offsetMax = new Vector2(-8f, 0f);
        if (!(keys || pad)) chipLabel.alignment = TextAnchor.MiddleCenter; else chipLabel.alignment = TextAnchor.MiddleLeft;
        if (!(keys || pad)) { lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero; }
        Debug.Log("HINTS: chip '" + (keys ? "[H] " : pad ? "[Menu] " : "") + chipLabel.text + "' device " + k + " width " + w.ToString("0"));
    }

    void TipsTick(float dt)
    {
        bool play = state == State.Play && slots.Count > 0;
        if (!play || help) { if (tipsCard.gameObject.activeSelf) tipsCard.gameObject.SetActive(false); return; }
        InputKind k = ChipKind;
        if (!tipsDone)
        {
            tipsDone = true;
            if (HintsOn && SaveStore.GetInt("ff.tips." + k, 0) == 0)
            {
                tipsT = TipsSecs; tipsKind = k;
                SaveStore.SetInt("ff.tips." + k, 1); SaveStore.Flush();
                Debug.Log("HINTS: first-time tips shown for " + k);
            }
            else Debug.Log("HINTS: tips already seen for " + k + " (or hints off: " + !HintsOn + ")");
        }
        if (tipsT <= 0f) { if (tipsCard.gameObject.activeSelf) tipsCard.gameObject.SetActive(false); return; }
        tipsT -= dt;
        if (!tipsCard.gameObject.activeSelf) tipsCard.gameObject.SetActive(true);
        tipsGroup.alpha = Mathf.Clamp01(tipsT / 0.8f) * Mathf.Clamp01((TipsSecs - tipsT) / 0.3f);
        bool narrow = NarrowHud, keys = tipsKind == InputKind.Keyboard, pad = tipsKind == InputKind.Gamepad;
        string sep = "<color=#7f8a96>   ·   </color>";
        string body;
        if (pad) body = Cap("L-stick", tipsKind) + " move" + sep + Cap("R-stick", tipsKind) + " look" + sep + Cap("A", tipsKind) + " hop" + (narrow ? "\n" : sep) + Cap("A", tipsKind) + " get in / on (near a vehicle)" + sep + Cap("RT / LT", tipsKind) + " gas / brake\n" + Cap("LB", tipsKind) + " robot phone" + sep + Cap("Start", tipsKind) + " help + every control";
        else if (keys) body = Cap("W A S D", tipsKind) + " move" + sep + Cap("Mouse", tipsKind) + " look (click to lock)" + (narrow ? "\n" : sep) + Cap("Space", tipsKind) + " hop" + sep + Cap("E", tipsKind) + " get in / on (near a vehicle)\n" + Cap("P", tipsKind) + " robot phone" + sep + Cap("H", tipsKind) + " help + every control";
        else body = Cap("Left stick", tipsKind) + " move" + sep + Cap("drag", tipsKind) + " look" + (narrow ? "\n" : sep) + Cap("A", tipsKind) + " hop / get in" + sep + Cap("PHONE", tipsKind) + " robots\n" + Cap("MENU", tipsKind) + " (top) help + every control";
        if (tipsBody.text != body) tipsBody.text = body;
        tipsFoot.text = "Turn hints off: " + MenuKey(tipsKind) + "  ›  SETTINGS  ›  HINTS";
        Vector2 cs = ((RectTransform)hudCanvas.transform).rect.size;
        float w = Mathf.Min(narrow ? cs.x - 28f : 640f, cs.x - 28f);
        tipsBody.rectTransform.anchoredPosition = new Vector2(18f, -40f);
        tipsBody.rectTransform.pivot = new Vector2(0f, 1f); tipsTitle.rectTransform.pivot = new Vector2(0f, 1f); tipsFoot.rectTransform.pivot = new Vector2(0f, 1f);
        tipsBody.rectTransform.sizeDelta = new Vector2(w - 36f, 10f);
        float bh = tipsBody.preferredHeight;
        tipsBody.rectTransform.sizeDelta = new Vector2(w - 36f, bh + 4f);
        tipsTitle.rectTransform.anchoredPosition = new Vector2(18f, -12f); tipsTitle.rectTransform.sizeDelta = new Vector2(w - 36f, 24f);
        tipsFoot.rectTransform.anchoredPosition = new Vector2(18f, -46f - bh); tipsFoot.rectTransform.sizeDelta = new Vector2(w - 36f, 18f);
        tipsCard.sizeDelta = new Vector2(w, 46f + bh + 28f);
        // below the chip; on a phone in portrait also below the touch SND / zoom buttons (top corners)
        bool touchPortrait = touch != null && touch.active && cs.y > cs.x;
        PlaceInSafe(tipsCard, new Vector2(0f, touchPortrait ? -200f : -64f));
        HintInfo = "tips " + tipsKind + " card " + w.ToString("0") + "x" + tipsCard.sizeDelta.y.ToString("0") + " at " + tipsCard.anchoredPosition.ToString("0") + " canvas " + cs.ToString("0");
    }

    // help screen SETTINGS: HINTS On / Off (N / pad X / click / tap on the row)
    void HintsSettingsInput()
    {
        bool toggle = false;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && !Kb.typing && kb.nKey.wasPressedThisFrame) toggle = true;
        foreach (var s in slots) if (s.kind == InputKind.Gamepad && s.pad != null && s.pad.added && s.pad.buttonWest.wasPressedThisFrame) { toggle = true; break; }
        foreach (Vector2 tp in Kb.TouchesBegan()) if (Hit(hpHints.rectTransform, tp, 10f)) toggle = true;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && Kb.MouseLeftDown() && Hit(hpHints.rectTransform, mouse.position.ReadValue(), 4f)) toggle = true;
        if (toggle) { SetHintsOn(!HintsOn); Sfx.Play(Sfx.Click, 0.6f); }
    }
}
