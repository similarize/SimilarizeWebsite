using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

// ffu74 (Bill, Oct 23, Pixel 9 on the 6.6 cabinet ffu73):
//  B5  BOOST in a mech only "boosted in place" - it never went up. Not an ffu73 regression: since ffu15 BOOST (LB / Shift /
//      touch BOOST) only sped up walking and slowed a fall; the rockets were JUMP / RT / Space held. Now holding BOOST lifts
//      every mech (StoryMech.Update: after LiftDelay74 it leaves the ground and climbs at the afterburner rate).
//  B6  No visible way into the trillion-story mech's bridge view (R3 / 0 / a hidden double-tap). Now:
//      touch  - an on-screen BRIDGE button (OUTSIDE while the bridge view is on): next to the MENU chip on a single view,
//               next to that view's own Help / MENU chip in a split view; only for a touch player in or aboard the mech.
//      pad    - the hint line "R3: Bridge view" (keyboard "0: Bridge view") while in or aboard it ("Outside view" when on).
//      Plain text, no rich-text tags. Pad Menu / View are not used for it.
// &ff74old=1 = before (no lift on BOOST, no button, no line). Probe ?ffdemo=1&ffshot=ffu74 (&f74n=1 = one view). Logs "FFU74 ...".
public static class F74
{
    static int old = -1;
    public static bool Old { get { if (old < 0) old = Ffu33.Url("ff74old") == "1" ? 1 : 0; return old == 1; } }
}

public partial class StoryMech
{
    float boostHeldT;
    public const float LiftDelay74 = 0.2f;   // a shorter BOOST tap is still a stride burst on the ground
    public static int Lift74;                // probe: lift-offs on BOOST
    public bool Grounded74 { get { return grounded; } }
}

public partial class Game
{
    // ---------------- B6: the BRIDGE button (touch) ----------------
    class BChip74 { public RectTransform rt; public Text label; public Image bg; public bool shown, on; public Rect screen; }
    readonly BChip74[] bchips74 = new BChip74[4];
    public static int Bridge74Taps;

    BChip74 MakeBChip74(int k)
    {
        var c = new BChip74();
        c.bg = UIK.Glass(hpMenuChip.parent, new Color(0.07f, 0.2f, 0.3f, 0.82f), Vector2.zero, new Vector2(110f, 40f));
        c.rt = c.bg.rectTransform; c.rt.name = "BridgeChip P" + (k + 1);
        c.rt.anchorMin = c.rt.anchorMax = Vector2.zero; c.rt.pivot = new Vector2(0.5f, 1f);
        c.label = UIK.Label(c.rt, "BRIDGE", 16, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 40f), Color.white);
        UIK.Stretch(c.label.rectTransform); UIK.Modernize(c.label, true, 0.2f);
        c.label.supportRichText = false;
        c.rt.gameObject.SetActive(false);
        return c;
    }

    // which touch player (slot index) gets a BRIDGE button: in / aboard a bridge, own camera on screen, help + phone closed
    bool Bridge74Wants(int k)
    {
        if (F74.Old || state != State.Play || k >= slots.Count) return false;
        Slot s = slots[k];
        if (s.kind != InputKind.Touch || s.cam == null || !s.cam.enabled || s.rig == null) return false;
        if (help) return false;
        if (s.frog < 0 || s.frog >= frogs.Count) return false;
        Frog f = frogs[s.frog];
        if (f == null || f.launching || Bridge73.Of(f) == null) return false;
        if (s.frog < 4 && RobotPhone.Seat[s.frog] != null && RobotPhone.Seat[s.frog].open) return false;
        return true;
    }

    void Bridge74Tick()
    {
        if (hpMenuChip == null || hudCanvas == null) return;
        if (TouchControls.UiHit == null) TouchControls.UiHit = Bridge74Hit;
        Vector2 cs = ((RectTransform)hudCanvas.transform).rect.size;
        float kx = cs.x / Mathf.Max(1, Screen.width), ky = cs.y / Mathf.Max(1, Screen.height);
        Rect sa = Screen.safeArea;
        for (int k = 0; k < 4; k++)
        {
            bool show = Bridge74Wants(k);
            if (show && bchips74[k] == null) bchips74[k] = MakeBChip74(k);
            BChip74 c = bchips74[k];
            if (c == null) continue;
            if (c.rt.gameObject.activeSelf != show) c.rt.gameObject.SetActive(show);
            c.shown = show;
            if (!show) continue;
            Slot s = slots[k];
            c.on = Bridge73.ViewOn(s.rig);
            string w = c.on ? "OUTSIDE" : "BRIDGE";
            if (c.label.text != w) c.label.text = w;
            c.bg.color = c.on ? new Color(0.85f, 0.45f, 0.1f, 0.85f) : new Color(0.07f, 0.2f, 0.3f, 0.82f);
            float bw = c.label.preferredWidth + 34f; c.rt.sizeDelta = new Vector2(bw, 40f);
            // the chip it sits beside: this view's own Help / MENU chip (split) or the single MENU chip
            Rect anchor = new Rect();
            if (Chip72Shown(k)) anchor = Chip72Rect(k);
            else if (hpMenuChip.gameObject.activeInHierarchy) anchor = ScreenRect72(hpMenuChip);
            Rect vr = s.cam.rect;
            float x0 = vr.xMin * cs.x, x1 = vr.xMax * cs.x, top = vr.yMax * cs.y;
            if (vr.yMax > 0.99f) top -= (Screen.height - sa.yMax) * ky;
            // ffu74b: the page's own top-right toolbar (fullscreen + "X Arcade", ~150 x 46 CSS px) sits over a view that
            // touches the screen's top-right corner; Unity can't see it, so keep the button out of that box
            float right = x1 - 8f - (vr.xMax > 0.99f && vr.yMax > 0.99f ? Mathf.Max(64f, Tb74W * Tb74Dpr() * kx) : 0f);
            float cx, y;
            if (anchor.width > 1f)
            {
                cx = anchor.xMax * kx + 10f + bw * 0.5f;
                y = anchor.yMax * ky;
                if (cx + bw * 0.5f > right) { cx = Mathf.Clamp(anchor.center.x * kx, x0 + bw * 0.5f + 6f, right - bw * 0.5f); y = anchor.yMin * ky - 8f; }   // no room: under the chip
            }
            else { cx = (x0 + x1) * 0.5f; y = top - 12f; }
            c.rt.anchoredPosition = new Vector2(cx, y);
            c.screen = ScreenRect72(c.rt);
        }
        // a tap on a BRIDGE button: that player's camera switches bridge <-> outside
        foreach (Vector2 tp in Kb.TouchesBegan())
            for (int k = 0; k < 4 && k < slots.Count; k++)
                if (bchips74[k] != null && bchips74[k].shown && Hit(bchips74[k].rt, tp, 10f)) { Bridge74Press(k, "touch"); return; }
    }

    public const float Tb74W = 150f, Tb74H = 46f;   // page toolbar box (CSS px, from the right / top edge)
    float tb74Dpr = 1f, tb74T = -9f;
    public float Tb74Dpr() { if (Time.unscaledTime - tb74T > 1f) { tb74T = Time.unscaledTime; tb74Dpr = Mathf.Max(0.5f, FFDisplay.CurrentDpr); } return tb74Dpr; }
    // probe: does a screen rect (pixels, y up) stay out of the page toolbar box?
    public bool ClearOfToolbar74(Rect r) { float d = Tb74Dpr(); return !(r.xMax > Screen.width - Tb74W * d && r.yMax > Screen.height - Tb74H * d); }

    void Bridge74Press(int k, string how)
    {
        Slot s = slots[k];
        Frog f = frogs[s.frog];
        if (Bridge73.Toggle(s.rig, f)) { Bridge74Taps++; Sfx.Play(Sfx.Click, 0.7f); Debug.Log("FF74 BRIDGE button (" + how + ") P" + (k + 1) + " -> " + (Bridge73.ViewOn(s.rig) ? "bridge" : "outside")); }
    }

    // TouchControls: a touch that starts on a BRIDGE button is not a look drag / double-tap
    bool Bridge74Hit(Vector2 p)
    {
        for (int k = 0; k < 4; k++) if (bchips74[k] != null && bchips74[k].shown && Hit(bchips74[k].rt, p, 10f)) return true;
        return false;
    }
    public bool Bridge74Shown(int k) { return k >= 0 && k < 4 && bchips74[k] != null && bchips74[k].shown; }
    public Rect Bridge74Rect(int k) { return k >= 0 && k < 4 && bchips74[k] != null ? bchips74[k].screen : new Rect(); }
    public string Bridge74Label(int k) { return k >= 0 && k < 4 && bchips74[k] != null ? bchips74[k].label.text : ""; }

    // ---------------- B6: the pad / keyboard line ----------------
    public static string Bridge74Line(Frog f, InputKind k)
    {
        if (F74.Old || f == null || !f.human || k == InputKind.Touch || f.launching || Bridge73.Of(f) == null) return null;
        bool on = false;
        if (I != null) foreach (var s in I.slots) if (s.frog >= 0 && s.frog < I.frogs.Count && I.frogs[s.frog] == f) on = Bridge73.ViewOn(s.rig);
        return (k == InputKind.Gamepad ? "R3" : "0") + ": " + (on ? "Outside view" : "Bridge view");
    }

    public static string HintPrompt(Frog f)
    {
        string p = HintPrompt46(f);
        string b = Bridge74Line(f, HintKind(f));
        if (b == null) return p;
        return string.IsNullOrEmpty(p) ? b : p + "   " + b;
    }

    // ---------------- probe ----------------
    int f74Pass, f74Total; bool f74On;
    void F74Check(string what, bool ok, string d) { f74Total++; if (ok) f74Pass++; Debug.Log("FFU74 " + (ok ? "PASS" : "FAIL") + " " + what + " | " + d); }
    void F74Info(string what, string d) { Debug.Log("FFU74 INFO " + what + " | " + d); }
    void Fix74TestLobby() { if (f74On) return; f74On = true; StartCoroutine(P74()); }

    static bool NoTags74(string s) { return s != null && s.IndexOf('<') < 0 && s.IndexOf('>') < 0; }

    // hold BOOST (pad LB or touch BOOST) for `gameSecs` of game time; returns via maxAlt
    float alt74;
    IEnumerator Hold74(StoryMech m, Gamepad pad, bool touch, bool jump, float gameSecs, string shot)
    {
        alt74 = 0f;
        if (touch) { if (jump) TouchControls.TestUp = true; else TouchControls.TestBoost = true; }
        else { var st = jump ? new GamepadState() : new GamepadState().WithButton(GamepadButton.LeftShoulder); if (jump) st.rightTrigger = 1f; InputSystem.QueueStateEvent(pad, st); }
        float t0 = Time.time, r0 = Time.realtimeSinceStartup; bool logged = false;
        while (Time.time - t0 < gameSecs && Time.realtimeSinceStartup - r0 < 60f)
        {
            alt74 = Mathf.Max(alt74, m.Altitude);
            if (!logged && shot != null && Time.time - t0 > gameSecs * 0.45f) { logged = true; Debug.Log("FFDEMO ffu74 ph 1 " + shot + " t " + Time.time.ToString("0.0")); }
            yield return null;
        }
        alt74 = Mathf.Max(alt74, m.Altitude);
        TouchControls.TestBoost = TouchControls.TestUp = false;
        if (pad != null) InputSystem.QueueStateEvent(pad, new GamepadState());
        yield return L59Frames(3);
    }
    IEnumerator Land74(StoryMech m) { float r0 = Time.realtimeSinceStartup; while ((m.Altitude > 0.5f || !m.Grounded74) && Time.realtimeSinceStartup - r0 < 60f) yield return null; yield return L59Settle(0.5f); }

    IEnumerator P74()
    {
        const string T = "ffu74";
        int n = Ffu33.Url("f74n") == "1" ? 1 : 2;
        Debug.Log("FFU74 start (BOOST lift + BRIDGE button) old=" + F74.Old + " old73=" + F73.Old + " views " + n);
        var pads = new Gamepad[n];
        yield return F72Join(pads, n, "FFTestPad74");
        F74Check(n + " pad(s) + LT: play, " + n + " view(s)", state == State.Play && slots.Count == n && LastViews == n, "views " + LastViews + " | " + SeatList());
        if (slots.Count < n) { F74Result(T); yield break; }
        Frog fa = frogs[slots[0].frog];
        Frog fb = n > 1 ? frogs[slots[1].frog] : null;
        StoryMech big = FindMech(fa.charId, 3);
        StoryMech hund = FindMech(fa.charId, 1); if (hund == null) for (int o = 0; o < 4 && hund == null; o++) hund = FindMech(o, 1);
        F74Check("James's trillion-story + a 100-story mech found", big != null && hund != null, (big != null ? big.Title + " H " + big.height : "-") + " / " + (hund != null ? hund.Title + " H " + hund.height : "-"));
        if (big == null || hund == null) { F74Result(T); yield break; }
        float dtWas = StoryMech.DtCap; StoryMech.DtCap = 0.3f;   // SwiftShader frames: judge by game time
        // ---- B5: pad LB (BOOST) alone, 100-story then trillion-story; then RT (rockets) for comparison ----
        foreach (var m in new[] { hund, big })
        {
            fa.SendTo(WorldId.Ranch, m.transform.position + m.transform.right * (m.height * 0.3f + 4f), 0f);
            yield return L59Frames(2);
            fa.EnterVehicle(m);
            yield return L59Settle(1.5f);
            if (fb != null && m == big)
            {
                Vector3 fw = m.transform.forward; fw.y = 0f; fw.Normalize();
                Vector3 side = Quaternion.Euler(0f, 35f, 0f) * fw;
                Vector3 p2 = m.transform.position + side * (m.height * 2.2f); p2.y = Ranch.GY(p2.x, p2.z) + 0.5f;
                float face = Mathf.Atan2(-side.x, -side.z) * Mathf.Rad2Deg;
                fb.SendTo(WorldId.Ranch, p2, face);
                if (slots[1].rig != null) { slots[1].rig.minPitch = -45f; slots[1].rig.SetYaw(face); slots[1].rig.SetPitch(-22f); }
                yield return L59Settle(1f);
            }
            int l0 = StoryMech.Lift74;
            yield return Hold74(m, pads[0], false, false, 3f, m == big ? "lift" : null);
            float boostAlt = alt74;
            bool want = !F74.Old;
            F74Check("pad: hold LB (BOOST) alone lifts the " + m.Title + " (band " + m.band + ")", fa.vehicle == m && boostAlt > Mathf.Max(15f, m.height * 0.5f) && StoryMech.Lift74 > l0,
                "max altitude " + boostAlt.ToString("0.0") + " m in 3 game-s (H " + m.height + ") lift-offs " + (StoryMech.Lift74 - l0) + (want ? "" : " [old build: BOOST only glides]"));
            yield return Land74(m);
            yield return Hold74(m, pads[0], false, true, 3f, null);
            F74Info("pad: hold RT (rockets) on the " + m.Title, "max altitude " + alt74.ToString("0.0") + " m in 3 game-s");
            yield return Land74(m);
            if (m == hund) { fa.ExitVehicle(); yield return L59Settle(1f); }
        }
        // ---- B6: pad / keyboard line while in the trillion-story mech ----
        string padLine = HintPrompt(fa);
        HintForce = (int)InputKind.Keyboard; string keyLine = HintPrompt(fa); HintForce = -1;
        F74Check("pad hint line names R3: Bridge view (plain text)", padLine.Contains("R3: Bridge view") && NoTags74(Bridge74Line(fa, InputKind.Gamepad)), "'" + padLine + "'");
        F74Check("keyboard hint line names 0: Bridge view (plain text)", keyLine.Contains("0: Bridge view") && NoTags74(Bridge74Line(fa, InputKind.Keyboard)), "'" + keyLine + "'");
        F74Check("no BRIDGE button for a pad player", !Bridge74Shown(0), "shown " + Bridge74Shown(0));
        if (fb != null)
        {
            Bridge73.Board(fb, big);
            yield return L59Settle(1f);
            string l2 = HintPrompt(fb);
            F74Check("P2 aboard (rider, pad): its line names R3: Bridge view", fb.passengerOf == big && l2.Contains("R3: Bridge view"), "'" + l2 + "'");
            yield return L59Press(pads[1], GamepadButton.RightStick);
            yield return L59Settle(1f);
            string l3 = HintPrompt(fb);
            F74Check("P2 R3: bridge view on, line says R3: Outside view", Bridge73.ViewOn(slots[1].rig) && l3.Contains("R3: Outside view"), "view " + Bridge73.ViewOn(slots[1].rig) + " '" + l3 + "'");
            yield return L59Press(pads[1], GamepadButton.RightStick);
            yield return L59Settle(0.5f);
            fb.LeavePassenger();
            Vector3 fw = big.transform.forward; fw.y = 0f; fw.Normalize();
            Vector3 side = Quaternion.Euler(0f, 35f, 0f) * fw;
            Vector3 p2 = big.transform.position + side * (big.height * 2.2f); p2.y = Ranch.GY(p2.x, p2.z) + 0.5f;
            float face = Mathf.Atan2(-side.x, -side.z) * Mathf.Rad2Deg;
            fb.SendTo(WorldId.Ranch, p2, face);
            if (slots[1].rig != null) { slots[1].rig.SetYaw(face); slots[1].rig.SetPitch(-22f); }
        }
        // ffu74b: the pad line must not sit on the view's status block (split views are narrow)
        yield return L59Settle(0.5f);
        { string pd = "no hud"; bool ok = slots[0].hud != null && slots[0].hud.PromptClear74(out pd);
          F74Check("pad line sits clear of the status block (P1 view)", F74.Old || ok, pd); }
        if (n > 1)
        {
            // a touch seat can't sit next to a controller player (SEATS COLLAPSE), so the touch part runs on the one-view probe (&f74n=1)
            F74Info("touch part skipped on " + n + " views", "seat rule: touch + pad players can't share; see the &f74n=1 run");
            fa.ExitVehicle(); yield return L59Settle(1f);
            StoryMech.DtCap = dtWas; F74Result(T); yield break;
        }
        // ---- P1 becomes a touch player (the phone) ----
        Kb.TestMobile = true; slots[0].kind = InputKind.Touch;
        yield return L59Settle(1.5f);
        F74Check("touch: P1 in the trillion-story mech gets a BRIDGE button", Bridge74Shown(0) && Bridge74Label(0) == "BRIDGE", "shown " + Bridge74Shown(0) + " label '" + Bridge74Label(0) + "' at " + Bridge74Rect(0).ToString("0"));
        Rect menu = Chip72Shown(0) ? Chip72Rect(0) : (hpMenuChip != null && hpMenuChip.gameObject.activeInHierarchy ? ScreenRect72(hpMenuChip) : new Rect());
        Rect br = Bridge74Rect(0), vr0 = slots[0].cam != null ? slots[0].cam.pixelRect : new Rect();
        F74Check("touch: the BRIDGE button sits beside the " + (n > 1 ? "view's own Help/MENU chip" : "MENU chip") + ", inside P1's view, no overlap", menu.width > 1f && br.width > 1f && !menu.Overlaps(br) && vr0.Contains(br.center) && Mathf.Abs(br.center.y - menu.center.y) < 60f,
            "menu " + menu.ToString("0") + " bridge " + br.ToString("0") + " view " + vr0.ToString("0"));
        F74Check("touch: the BRIDGE button stays clear of the page's fullscreen / Arcade buttons (top right)", br.width > 1f && ClearOfToolbar74(br),
            "bridge " + br.ToString("0") + " screen " + Screen.width + "x" + Screen.height + " dpr " + Tb74Dpr().ToString("0.0#") + " toolbar box x > " + (Screen.width - Tb74W * Tb74Dpr()).ToString("0") + ", y > " + (Screen.height - Tb74H * Tb74Dpr()).ToString("0"));
        if (fb != null) F74Check("touch: no BRIDGE button in P2's view (pad, on foot)", !Bridge74Shown(1), "shown " + Bridge74Shown(1));
        string tl = HintPrompt(fa);
        F74Check("touch: no R3 / 0 line for the touch player (the button instead)", !tl.Contains("Bridge view"), "'" + tl + "'");
        // touch BOOST lifts too
        int lt0 = StoryMech.Lift74;
        yield return Hold74(big, null, true, false, 3f, "touchlift");
        F74Check("touch: hold BOOST lifts the trillion-story mech", alt74 > big.height * 0.5f && StoryMech.Lift74 > lt0, "max altitude " + alt74.ToString("0.0") + " m lift-offs " + (StoryMech.Lift74 - lt0));
        yield return Land74(big);
        // tap the BRIDGE button
        int ci0 = Bridge73.CamsInside, tp0 = Bridge74Taps;
        if (Bridge74Shown(0)) Kb.FakeTap(Bridge74Rect(0).center);
        yield return L59Settle(1.5f);
        Camera ca = slots[0].rig != null ? slots[0].rig.cam : null;
        F74Check("touch: tapping BRIDGE puts P1's camera on the bridge; the button now says OUTSIDE", Bridge74Taps > tp0 && Bridge73.ViewOn(slots[0].rig) && ca != null && big.bridge73 != null && big.bridge73.Inside(ca.transform.position) && Bridge73.CamsInside > ci0 && Bridge74Label(0) == "OUTSIDE",
            "taps " + (Bridge74Taps - tp0) + " view " + Bridge73.ViewOn(slots[0].rig) + " inside-frames " + (Bridge73.CamsInside - ci0) + " label '" + Bridge74Label(0) + "'");
        Debug.Log("FFDEMO " + T + " ph 2 bridgetap t " + Time.time.ToString("0.0"));
        yield return L59Settle(8f);
        if (Bridge74Shown(0)) Kb.FakeTap(Bridge74Rect(0).center);
        yield return L59Settle(1.5f);
        F74Check("touch: tapping OUTSIDE goes back to the outside view", !Bridge73.ViewOn(slots[0].rig) && Bridge74Label(0) == "BRIDGE", "view " + Bridge73.ViewOn(slots[0].rig) + " label '" + Bridge74Label(0) + "'");
        Debug.Log("FFDEMO " + T + " ph 3 outside t " + Time.time.ToString("0.0"));
        yield return L59Settle(6f);
        fa.ExitVehicle();
        yield return L59Settle(1.5f);
        F74Check("touch: climbed out -> no BRIDGE button", !Bridge74Shown(0), "shown " + Bridge74Shown(0));
        StoryMech.DtCap = dtWas;
        F74Result(T);
    }
    void F74Result(string tag) { Debug.Log("FFU74 RESULT " + f74Pass + "/" + f74Total + " (" + tag + ")"); Debug.Log("FFDEMO " + tag + " done"); }
}
