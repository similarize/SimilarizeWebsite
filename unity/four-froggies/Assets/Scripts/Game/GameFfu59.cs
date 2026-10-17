using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// ffu59 LOBBY CONTROLS (Bill, Xbox controller: "A joins as a player, but pressing A again starts the game").
//  * Pads: A (or Start) only JOINS - each controller pressing A becomes the next player, P1..P4 (PadGuard -> PadJoins ->
//    Join -> SeatGate, unchanged). A / Start again on a seated pad never starts: it pops that player's card and the status
//    line says "P1 is already in · LT: start game". LT (left trigger) STARTS the game from any seated controller
//    (an LT on an unseated controller does nothing; a mirror / ghost device never starts).
//  * Keyboard: Space joins (never starts), Enter = START (seats the keyboard player first only when nobody is seated).
//    Touch / mouse: a tap / click on a character or seat card joins / picks (never starts); the START button starts.
//    SeatGate (ffu39) still never adds a 2nd touch / keyboard seat.
//  * LT is also the mech aim / vehicle brake in play: an LT still held from the lobby is latched off (Slot.ltHold) - brake 0,
//    no aim, RT not a fire-while-aiming - until the trigger is released (< 0.15), then it is live again.
//  * Logs: "LOBBY59: ..." on every join / again / start / latch. Probe: ?ffdemo=1&ffshot=lobby59[&l59=pad|kb|touch]
//    (synthetic pads / keyboard / taps; "LOBBY59 PASS/FAIL ..." + "LOBBY59 RESULT p/t").
public partial class Game
{
    int lobbyAgainSeat = -1;
    float lobbyAgainT = -10f;
    InputKind lobbyAgainKind = InputKind.Gamepad;
    public static int LtSuppressed;

    void Lobby59Log(string s) { Debug.Log("LOBBY59: " + s); }

    // a seated player pressed their join key again: a nudge, never a start
    void Lobby59Again(Slot s, string key)
    {
        if (s == null) return;
        int pn = slots.IndexOf(s);
        if (pn < 0) return;
        float now = Time.unscaledTime;
        if (lobbyAgainSeat == pn && now - lobbyAgainT < 0.3f) return;
        lobbyAgainSeat = pn; lobbyAgainT = now;
        lobbyAgainKind = s.kind;
        cardPop[s.frog] = 1f;
        Sfx.Play(Sfx.Select, 0.35f, 1.15f);
        Lobby59Log(key + " again on P" + (pn + 1) + " (" + s.kind + ", already in) - no start; " + (s.kind == InputKind.Gamepad ? "LT" : "Enter / START") + " starts | " + SeatList());
    }

    void Lobby59LatchLT()
    {
        foreach (var s in slots)
        {
            s.ltHold = s.kind == InputKind.Gamepad && s.pad != null && s.pad.added && s.pad.leftTrigger.ReadValue() > 0.1f;
            if (s.ltHold) Lobby59Log("P" + (slots.IndexOf(s) + 1) + " LT still held from the lobby - ignored in play until released");
        }
    }

    PIn LtLatch(Slot s, PIn i)
    {
        if (s.pad == null || s.pad.leftTrigger.ReadValue() < 0.15f)
        {
            s.ltHold = false;
            Lobby59Log("P" + (slots.IndexOf(s) + 1) + " LT released - trigger live again (" + LtSuppressed + " frames ignored)");
            return i;
        }
        i.brake = 0f;
        i.climb = i.gas;
        i.aimHeld = false;
        i.upHeld = i.gas > 0.3f;
        i.gunFire = s.pad.buttonWest.wasPressedThisFrame;
        i.gunHeld = s.pad.buttonWest.isPressed;
        LtSuppressed++;
        return i;
    }

    // ---------------- probe: ?ffdemo=1&ffshot=lobby59[&l59=pad|kb|touch] ----------------
    bool l59On;
    void Lobby59TestLobby()
    {
        if (l59On) return;
        l59On = true;
        string m = UrlParam("l59") ?? "pad";
        if (m == "touch") Kb.TestMobile = true;
        StartCoroutine(m == "kb" ? L59Keys() : m == "touch" ? L59Touch() : L59Pads());
    }

    int l59Pass, l59Total;
    void L59Check(string what, bool ok, string detail = "")
    {
        l59Total++; if (ok) l59Pass++;
        Debug.Log("LOBBY59 " + (ok ? "PASS" : "FAIL") + " " + what + " | state " + state + " seats " + slots.Count + (detail.Length > 0 ? " | " + detail : "") + " | " + SeatList());
    }
    void L59Result(string m) { Debug.Log("LOBBY59 RESULT " + m + " " + l59Pass + "/" + l59Total + " | " + SeatList()); }

    IEnumerator L59Frames(int n) { for (int k = 0; k < n; k++) yield return null; }
    IEnumerator L59Settle(float s) { yield return new WaitForSecondsRealtime(s); yield return L59Frames(3); }
    IEnumerator L59Press(Gamepad p, GamepadButton b)
    {
        InputSystem.QueueStateEvent(p, new GamepadState().WithButton(b));
        yield return L59Frames(3);
        InputSystem.QueueStateEvent(p, new GamepadState());
        yield return L59Frames(3);
    }
    void L59Trigger(Gamepad p, float lt) { var st = new GamepadState(); st.leftTrigger = lt; InputSystem.QueueStateEvent(p, st); }

    IEnumerator L59Pads()
    {
        Debug.Log("LOBBY59 start pads (slots " + slots.Count + ", pads " + Gamepad.all.Count + ")");
        yield return L59Settle(2f);
        L59Check("lobby, nobody seated", state == State.Lobby && slots.Count == 0);
        Gamepad A = InputSystem.AddDevice<Gamepad>("FFTestPad59");
        yield return L59Settle(1.2f);
        yield return L59Press(A, GamepadButton.South);
        yield return L59Settle(0.6f);
        L59Check("pad A presses A once: joins as P1, no start", state == State.Lobby && slots.Count == 1 && FindPad(A) == slots[0]);
        yield return L59Press(A, GamepadButton.South);
        yield return L59Settle(0.6f);
        L59Check("pad A presses A a 2nd time: still 1 seat, NO start", state == State.Lobby && slots.Count == 1);
        yield return L59Press(A, GamepadButton.South);
        yield return L59Settle(0.6f);
        yield return L59Press(A, GamepadButton.Start);
        yield return L59Settle(0.6f);
        L59Check("A a 3rd time + Start on the seated pad: still 1 seat, NO start", state == State.Lobby && slots.Count == 1);
        Gamepad B = InputSystem.AddDevice<Gamepad>("FFTestPad59");
        yield return L59Settle(1.2f);
        yield return L59Press(B, GamepadButton.South);
        yield return L59Settle(0.6f);
        L59Check("a 2nd pad presses A: joins as P2, no start", state == State.Lobby && slots.Count == 2 && FindPad(B) == slots[1]);
        yield return L59Press(B, GamepadButton.South);
        yield return L59Settle(0.6f);
        L59Check("P2 presses A again: still 2 seats, NO start", state == State.Lobby && slots.Count == 2);
        Gamepad C = InputSystem.AddDevice<Gamepad>("FFTestPad59");
        yield return L59Settle(1.2f);
        L59Trigger(C, 1f); yield return L59Frames(3); L59Trigger(C, 0f);
        yield return L59Settle(0.6f);
        L59Check("LT on an unseated 3rd pad: no seat, NO start", state == State.Lobby && slots.Count == 2 && FindPad(C) == null);
        // P2 holds LT -> START, and keeps holding it into play
        L59Trigger(B, 1f);
        yield return L59Settle(0.5f);
        L59Check("P2 presses LT: the game STARTS with both players", state == State.Play && slots.Count == 2);
        yield return L59Settle(1.2f);
        Slot sb = FindPad(B);
        L59Check("LT still held from the lobby: no brake / aim in play", sb != null && sb.ltHold && sb.last.brake == 0f && !sb.last.aimHeld && LtSuppressed > 0,
            sb != null ? "ltHold " + sb.ltHold + " brake " + sb.last.brake.ToString("0.00") + " aim " + sb.last.aimHeld + " suppressed " + LtSuppressed : "no P2");
        L59Trigger(B, 0f);
        yield return L59Settle(0.6f);
        L59Check("LT released: latch cleared", sb != null && !sb.ltHold);
        L59Trigger(B, 1f);
        yield return L59Settle(0.6f);
        L59Check("a fresh LT press in play is live again (brake / aim)", sb != null && sb.last.brake > 0.9f && sb.last.aimHeld, sb != null ? "brake " + sb.last.brake.ToString("0.00") + " aim " + sb.last.aimHeld : "");
        L59Trigger(B, 0f);
        yield return L59Settle(0.4f);
        L59Result("pads");
    }

    Keyboard l59K;
    IEnumerator L59Key(Key k)
    {
        InputSystem.QueueStateEvent(l59K, new KeyboardState(k));
        yield return L59Frames(3);
        InputSystem.QueueStateEvent(l59K, new KeyboardState());
        yield return L59Frames(3);
    }
    Vector2 L59Scr(UnityEngine.UI.Image img) { return RectTransformUtility.WorldToScreenPoint(null, img.rectTransform.position); }

    IEnumerator L59Keys()
    {
        Debug.Log("LOBBY59 start keyboard");
        yield return L59Settle(2f);
        l59K = InputSystem.AddDevice<Keyboard>("FFTestKb59");
        l59K.MakeCurrent();
        yield return L59Settle(0.5f);
        L59Check("lobby, nobody seated", state == State.Lobby && slots.Count == 0);
        yield return L59Key(Key.Space);
        yield return L59Settle(0.6f);
        L59Check("Space: keyboard joins as P1, no start", state == State.Lobby && slots.Count == 1 && slots[0].kind == InputKind.Keyboard);
        yield return L59Key(Key.Space);
        yield return L59Settle(0.5f);
        yield return L59Key(Key.Space);
        yield return L59Settle(0.5f);
        L59Check("Space twice more: still 1 seat, NO start", state == State.Lobby && slots.Count == 1);
        Kb.FakeTap(L59Scr(tiles[5]));
        yield return L59Settle(0.6f);
        Kb.FakeTap(L59Scr(cards[2]));
        yield return L59Settle(0.6f);
        L59Check("tap a character tile + an empty seat card: no 2nd seat (ffu39 SeatGate), NO start", state == State.Lobby && slots.Count == 1, "char " + (slots.Count > 0 ? Roster.Name(charOf[slots[0].frog]) : "-"));
        yield return L59Key(Key.Enter);
        yield return L59Settle(0.6f);
        L59Check("Enter: the game STARTS with 1 seat", state == State.Play && slots.Count == 1);
        L59Result("kb");
    }

    IEnumerator L59Touch()
    {
        Debug.Log("LOBBY59 start touch (mobile path forced)");
        yield return L59Settle(2.5f);
        L59Check("phone lobby auto-seats one touch player, no start", state == State.Lobby && slots.Count == 1 && slots[0].kind == InputKind.Touch);
        Kb.FakeTap(L59Scr(tiles[4]));
        yield return L59Settle(0.6f);
        Kb.FakeTap(L59Scr(tiles[4]));
        yield return L59Settle(0.6f);
        Kb.FakeTap(L59Scr(cards[3]));
        yield return L59Settle(0.6f);
        L59Check("tile tapped twice + a seat card tap: still 1 seat, NO start", state == State.Lobby && slots.Count == 1, "char " + (slots.Count > 0 ? Roster.Name(charOf[slots[0].frog]) : "-"));
        Kb.FakeTap(L59Scr(playBtn));
        yield return L59Settle(0.8f);
        L59Check("tap START: the game STARTS with 1 seat", state == State.Play && slots.Count == 1);
        L59Result("touch");
        Kb.TestMobile = false;
    }
}
