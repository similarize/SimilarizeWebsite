using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// lambo7 ONE CONTROLLER = ONE VIEW (ported from Four Froggies ffu39; Bill: with one pad the screen split into two players).
//
// Root causes in Lambo Blast (lambo1-6):
//  * the lobby joined a pad straight from its A / Start press inside the per-pad loop; a browser that exposes the one pad
//    twice (XInput + standard mapping, or a reconnect under a new id) could join the mirror as P2 when its press landed in a
//    different frame / order than the old 150 ms IsGhostPress check expected;
//  * every touch / mouse / key path seated a NEW player when its own kind had no seat yet: Enter (keyboard seat), a mouse
//    click on a car / robot tile (keyboard seat), a tap on a tile (touch seat), and phones auto-seated a touch player before
//    the browser even reported the pad (it only appears after its first press), so the pad's A became P2 -> split screen.
// Rule now (Join -> SeatGate is the single choke point):
//  * a touch or keyboard seat is only created when NOBODY is seated; otherwise taps / keys act for the seated player
//    (RACE starts, tiles pick that player's car / robot) and never add a seat;
//  * a pad joining (explicit A / Start through PadGuard, never a mirror) TAKES OVER a seated touch / keyboard player
//    (same car + robot), so one pad + touch / keyboard is always one full-screen view;
//  * split screen only happens with two distinct pads that each pressed join;
//  * SeatTick collapses a split whose seat lost its device (mirror / duplicate leaves at once, a pad gone for 3 s, a non-pad
//    seat next to a pad seat). "SEATS:" console lines; &padlog=1 adds per-press PADS lines + a 5 s seat summary.
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
            sb.Append(" car ").Append(s.car).Append("; ");
        }
        return sb.Length > 0 ? sb.ToString() : "nobody seated";
    }

    // true = handled (seat = the seat to act for, may be null): no new seat is made
    bool SeatGate(InputKind kind, Gamepad pad, out Slot seat)
    {
        seat = null;
        if (slots.Count == 0) return false;
        if (kind != InputKind.Gamepad)
        {
            seat = FindSlot(kind) ?? (slots.Count == 1 ? slots[0] : null);
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
        SeatLog("controller " + PadGuard.Describe(pad) + " takes over P" + (slots.IndexOf(np) + 1) + " (was " + was + ") - one player, one full-screen view");
        if (state != State.Lobby) ApplyLayout();
        seat = np;
        return true;
    }

    // removes one seat; in a race its kart carries on as an AI racer where it is (no grid reset)
    void DropSeat(Slot s)
    {
        if (state == State.Lobby) { Leave(s); return; }
        if (s.cam != null) Destroy(s.cam.gameObject);
        if (s.hud != null) Destroy(s.hud.panel.gameObject);
        if (s.kart != null) { s.kart.SetHuman(false); s.kart.slot = -1; }
        slots.Remove(s);
        for (int i = 0; i < slots.Count; i++) if (slots[i].kart != null) slots[i].kart.slot = i;
        ApplyLayout();
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
            SeatLog("COLLAPSE: P" + (i + 1) + " leaves - " + why + " | " + SeatList());
            seatDeadSince.Remove(s);
            DropSeat(s);
            foreach (var o in slots) if (o.kart != null) o.kart.Toast("One controller - back to one full-screen view", 2.5f);
        }
    }

    // a pad to ignore in menus: the old same-press ghost flag, or a PadGuard mirror of another device
    bool IsGhostDev(Gamepad pad) { return pad == null || ghosts.Contains(pad.deviceId) || PadGuard.IsAlias(pad); }

    // accepted join presses (PadGuard: an explicit A / Start, 150 ms after the press, not a mirror). A seat whose pad was
    // lost (removed / disconnected) is re-bound first (same model preferred) instead of adding a second player.
    void PadJoins()
    {
        Gamepad jp;
        while ((jp = PadGuard.TakeJoin()) != null)
        {
            if (FindPad(jp) != null) continue;
            Slot orphan = null;
            foreach (var s in slots) if (s.kind == InputKind.Gamepad && PadGuard.IsRemoved(s.pad) && (orphan == null || PadGuard.SameModel(s.pad, jp))) orphan = s;
            if (orphan != null) { Rebind(orphan, jp, "join press from a new device"); continue; }
            if (state != State.Lobby) continue;      // no mid-race joins (as before)
            PadGuard.Log("join " + PadGuard.Describe(jp) + " (" + state + ")");
            Join(InputKind.Gamepad, jp);
        }
    }

    void Rebind(Slot s, Gamepad pad, string why)
    {
        PadGuard.Log("REBIND P" + (slots.IndexOf(s) + 1) + " from " + PadGuard.Describe(s.pad) + " to " + PadGuard.Describe(pad) + " - " + why);
        s.pad = pad;
        s.joinTime = Time.unscaledTime;
        if (s.kart != null) s.kart.Toast("Controller reconnected", 2f);
    }

    // per-frame pad housekeeping: a removed pad's mirror takes over its seat; a mirror that somehow got its own seat gives it up
    void PadTick()
    {
        Gamepad now, old, alias, root;
        while (PadGuard.TakePromotion(out now, out old))
        {
            Slot s = FindPad(old);
            if (s != null && FindPad(now) == null) Rebind(s, now, "old device removed, its mirror lives on");
        }
        while (PadGuard.TakeMerge(out alias, out root))
        {
            Slot sa = FindPad(alias), sr = FindPad(root);
            if (sa != null && sr != null)
            {
                PadGuard.Log("MERGE: P" + (slots.IndexOf(sa) + 1) + " was driven by a mirror of P" + (slots.IndexOf(sr) + 1) + "'s controller - removed");
                DropSeat(sa);
            }
            else if (sa != null && sr == null) { sa.pad = root; PadGuard.Log("MERGE: P" + (slots.IndexOf(sa) + 1) + " now reads the root device #" + root.deviceId); }
        }
    }

    void SeatsUpdate()
    {
        PadGuard.Assigned = p => FindPad(p) != null;   // before anybody reads a pad this frame
        PadGuard.GraceOn = state != State.Lobby;
        PadGuard.Tick();
        PadTick();
        SeatTick();
        PadJoins();
    }
}
