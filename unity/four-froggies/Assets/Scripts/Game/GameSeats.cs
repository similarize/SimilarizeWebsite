using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// ffu39 ONE CONTROLLER = ONE VIEW (Bill, Pixel 9 / Tesla touch screen + one Xbox pad: he picked his froggy with the pad,
// tapped PLAY on the screen, and the game split in two with only the pad's froggy moving).
//
// Root cause: every touch / mouse path in the lobby seated a NEW player when its own kind had no seat yet - the PLAY tap
// (`if (FindSlot(Touch) == null) Join(Touch)`), a character-tile tap, a seat-card tap, HOST / JOIN / NAME taps, any tap in
// play on a phone, Enter, and a mouse click on PLAY (keyboard seat). On a phone the lobby also auto-seated a touch player at
// once (the browser only reports a pad after its first button press), so the pad's A became P2. Any of these + the pad =
// two seats = split screen, and the touch seat's froggy had no on-screen controls.
//
// Rule now (Join -> SeatGate is the single choke point, so every caller obeys it):
//  * a touch or keyboard seat is only created when NOBODY is seated; otherwise taps / keys act for the seated player
//    (PLAY starts, tiles pick that player's character, NAME / HOST / JOIN act for them) and never add a seat;
//  * a pad joining (explicit A / Start through PadGuard) TAKES OVER a seated touch / keyboard player (same froggy, name and
//    character), so one pad + touch / keyboard is always one full-screen view;
//  * split screen therefore only happens with two distinct pads, each having pressed join (PadGuard mirrors never join);
//  * SeatTick collapses a split whose seat lost its device: a mirror / duplicate of another seat's pad leaves at once, a seat
//    whose pad is gone for 3 s leaves (its froggy goes back to AI, the view goes full screen); a non-pad seat next to a pad
//    seat (any path that bypassed the gate) leaves too.
//  * "SEATS:" console lines on every refused join / takeover / collapse / layout; &padlog=1 adds a 5 s seat summary.
public partial class Game
{
    public static int LastViews = 1;
    readonly Dictionary<Slot, float> seatDeadSince = new Dictionary<Slot, float>();
    float seatRefuseLogT = -10f, seatSumT;

    void SeatLog(string s) { Debug.Log("SEATS: " + s); }

    string SeatList()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            sb.Append("P").Append(i + 1).Append("=").Append(s.kind);
            if (s.pad != null) sb.Append(" #").Append(s.pad.deviceId).Append(PadGuard.IsAlias(s.pad) ? " (mirror)" : "").Append(PadGuard.IsRemoved(s.pad) ? " (gone)" : "");
            sb.Append(" seat ").Append(s.frog).Append("; ");
        }
        return sb.Length > 0 ? sb.ToString() : "nobody seated";
    }

    // true = handled (seat = the seat to use, may be null): no new seat is made
    bool SeatGate(InputKind kind, Gamepad pad, out Slot seat)
    {
        seat = null;
        if (slots.Count == 0) return false;
        if (kind != InputKind.Gamepad)
        {
            seat = FindSlot(kind);
            if (Time.unscaledTime - seatRefuseLogT > 1f)
            {
                seatRefuseLogT = Time.unscaledTime;
                SeatLog("no new " + kind + " seat (" + (FindSlot(InputKind.Gamepad) != null ? "a controller player is seated - taps / keys act for them" : "one player per touch / keyboard device") + ") | " + SeatList());
            }
            return true;
        }
        Slot np = null;
        foreach (var s in slots) if (s.kind != InputKind.Gamepad) { np = s; break; }
        if (np == null) return false;
        InputKind was = np.kind;
        np.kind = InputKind.Gamepad; np.pad = pad; np.joinTime = Time.unscaledTime;
        frogs[np.frog].inputKind = InputKind.Gamepad;
        if (was == InputKind.Keyboard && state == State.Play) Cursor.lockState = CursorLockMode.None;
        SeatLog("controller " + PadGuard.Describe(pad) + " takes over P" + (slots.IndexOf(np) + 1) + " (was " + was + ") - one player, one full-screen view");
        if (state == State.Play) ApplyLayout();
        seat = np;
        return true;
    }

    void SeatTick()
    {
        float now = Time.unscaledTime;
        if (PadGuard.Verbose && now - seatSumT > 5f) { seatSumT = now; SeatLog("summary " + state + " views " + LastViews + " | " + SeatList() + " | pads " + PadGuard.List()); }
        if (slots.Count < 2) { seatDeadSince.Clear(); return; }
        bool anyPad = false;
        foreach (var s in slots) if (s.kind == InputKind.Gamepad) anyPad = true;
        for (int i = slots.Count - 1; i >= 0 && slots.Count > 1; i--)
        {
            Slot s = slots[i];
            string why = null;
            if (s.kind != InputKind.Gamepad) { if (anyPad) why = s.kind + " seat next to a controller player"; }
            else if (s.pad != null && PadGuard.IsAlias(s.pad)) why = "its controller is a mirror of another device";
            else if (s.pad != null)
            {
                Gamepad root = PadGuard.RootOf(s.pad);
                for (int j = 0; j < i; j++)
                    if (slots[j].kind == InputKind.Gamepad && slots[j].pad != null && (slots[j].pad == s.pad || PadGuard.RootOf(slots[j].pad) == root)) why = "same controller as P" + (j + 1);
            }
            if (why == null && s.kind == InputKind.Gamepad && (s.pad == null || PadGuard.IsRemoved(s.pad)))
            {
                float t0;
                if (!seatDeadSince.TryGetValue(s, out t0)) { seatDeadSince[s] = now; t0 = now; }
                if (now - t0 > 3f) why = "its controller has been gone for 3 s";
            }
            else seatDeadSince.Remove(s);
            if (why == null) continue;
            SeatLog("COLLAPSE: P" + (i + 1) + " (" + frogs[s.frog].nick + ") leaves - " + why + " | " + SeatList());
            seatDeadSince.Remove(s);
            Leave(s);
            foreach (var o in slots) frogs[o.frog].Toast("One controller - back to one full-screen view", 2.5f);
        }
    }

    // ---------------- probe: ?ffdemo=1&ffshot=seattest ----------------
    // Phone path forced (Kb.TestMobile): the lobby auto-seats touch P1 as on a real phone; virtual pad A + its MIRROR (the
    // same pad exposed twice) appear; A presses A (+ mirror, same frame) = join; D-pad right picks a character; a synthetic
    // touch tap lands on PLAY. Expected: one seat (the pad), state Play, single full-screen view. Then a tap in play, a
    // mirror Start join attempt, a reconnect (A removed -> mirror keeps it, mirror removed, new device presses A) - still one
    // seat, one view. Logs "SEATTEST PASS/FAIL ..." and "SEATTEST RESULT p/t".
    bool seatTestOn;
    void SeatTestLobby()
    {
        if (seatTestOn) return;
        seatTestOn = true;
        Kb.TestMobile = true;
        StartCoroutine(SeatTest());
    }

    IEnumerator SeatTest()
    {
        Debug.Log("SEATTEST start (mobile path forced, slots " + slots.Count + ")");
        yield return Settle(2f);
        Check2("phone lobby auto-seats one touch player", slots.Count == 1 && slots[0].kind == InputKind.Touch, SeatList());
        Gamepad A = InputSystem.AddDevice<Gamepad>("FFTestPad");
        Gamepad M = InputSystem.AddDevice<Gamepad>("FFTestPad");   // the same pad exposed twice (XInput + standard mapping)
        yield return Settle(1.2f);
        Check2("two mirrored pad entries appearing seat nobody new", slots.Count == 1, SeatList());
        yield return Press(A, GamepadButton.South, M, 0);
        yield return Settle(0.6f);
        Check2("pad A presses A (mirror same frame): it takes over the touch seat", slots.Count == 1 && slots[0].kind == InputKind.Gamepad && (slots[0].pad == A || slots[0].pad == M), SeatList());
        int c0 = slots.Count > 0 ? charOf[slots[0].frog] : -1;
        yield return Press(A, GamepadButton.DpadRight, M, 0);
        yield return Settle(0.4f);
        Check2("D-pad right picks the next character for the pad player", slots.Count == 1 && charOf[slots[0].frog] != c0, "char " + c0 + " -> " + (slots.Count > 0 ? charOf[slots[0].frog] : -1));
        Vector2 play = playBtn != null ? (Vector2)RectTransformUtility.WorldToScreenPoint(null, playBtn.rectTransform.position) : new Vector2(Screen.width * 0.5f, Screen.height * 0.2f);
        Debug.Log("SEATTEST tap PLAY at " + play.ToString("0") + " screen " + Screen.width + "x" + Screen.height + " hit " + Hit(playBtn, play));
        Kb.FakeTap(play);
        yield return Settle(1f);
        Check2("touch tap on PLAY starts the game with ONE seat and a single full-screen view", state == State.Play && slots.Count == 1 && LastViews == 1 && slots[0].kind == InputKind.Gamepad, "state " + state + " views " + LastViews + " | " + SeatList());
        Kb.FakeTap(new Vector2(Screen.width * 0.7f, Screen.height * 0.5f));
        yield return Settle(0.6f);
        Check2("a tap in play adds no touch player", slots.Count == 1 && LastViews == 1, SeatList());
        yield return Press(M, GamepadButton.Start, A, 0);
        yield return Settle(0.6f);
        bool h = help; if (help) { yield return Press(A, GamepadButton.Start); yield return Settle(0.4f); }
        Check2("mirror + pad Start together: no second seat", slots.Count == 1, SeatList() + " help was " + h);
        Slot s0 = slots.Count > 0 ? slots[0] : null;
        InputSystem.RemoveDevice(A);
        yield return Settle(0.8f);
        Check2("pad A removed: the mirror keeps the one seat", slots.Count == 1 && s0 != null && s0.pad == M, SeatList());
        InputSystem.RemoveDevice(M);
        yield return Settle(1.5f);
        Gamepad R = InputSystem.AddDevice<Gamepad>("FFTestPad");   // reconnect under a new id
        Gamepad R2 = InputSystem.AddDevice<Gamepad>("FFTestPad");  // ... and its mirror again
        yield return Settle(1.2f);
        yield return Press(R, GamepadButton.South, R2, 1);
        yield return Settle(0.8f);
        Check2("reconnect (+ mirror) presses A: same seat re-bound, still one view", slots.Count == 1 && slots[0] == s0 && LastViews == 1, SeatList());
        // forced bad state: a second seat on the mirror device must collapse
        Slot ghost = new Slot { kind = InputKind.Gamepad, pad = R2, frog = FreeFrog(0, 1), joinTime = Time.unscaledTime };
        if (ghost.frog >= 0) { slots.Add(ghost); frogs[ghost.frog].human = true; MakeView(ghost); ApplyLayout(); }
        yield return Settle(4f);
        Check2("a split seat driven by a mirror / duplicate device collapses to one view", slots.Count == 1 && LastViews == 1, SeatList());
        Debug.Log("SEATTEST RESULT " + stPass + "/" + stTotal + " views " + LastViews + " | " + SeatList());
        Kb.TestMobile = false;
    }

    int stPass, stTotal;
    void Check2(string what, bool ok, string detail = "")
    {
        stTotal++; if (ok) stPass++;
        Debug.Log("SEATTEST " + (ok ? "PASS" : "FAIL") + " " + what + (detail.Length > 0 ? " | " + detail : ""));
    }
}
