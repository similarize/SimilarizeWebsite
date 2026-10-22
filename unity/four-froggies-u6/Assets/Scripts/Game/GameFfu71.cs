using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// ffu71 (Ben's 6.6 work list, Batch B) probes.
// Item 1 - split-screen Robot Phones: ?ffdemo=1&ffshot=phone71 (lobby probe, synthetic pads like phone59):
//   2 pads join + LT = 2-way split; each LB opens only that player's phone, in that player's viewport (panel + PHONE button
//   inside the camera rect, no overlap); pad nav (ffu68 stick / A / B) moves only its own phone; LB in a ranch vehicle (by
//   design: LB = boost there, ffu35) is logged as INFO; 2 more pads join = 4-way: compact phones, Up to the (hidden) robot
//   card shows the header cue. Logs "FFU71 PASS|FAIL|INFO ...", "FFU71 RESULT n/m", "FFDEMO phone71 ph N <phase>", "FFDEMO phone71 done".
// F71.Old (&ff71old=1) = the pre-ffu71 behaviour (no compact header cue; no second jet), so one build gives true before / after shots.
public static class F71
{
    static int old = -1;
    public static bool Old { get { if (old < 0) old = Ffu33.Url("ff71old") == "1" ? 1 : 0; return old == 1; } }
}

public partial class Game
{
    int f71Pass, f71Total;
    bool p71On;
    void F71Check(string what, bool ok, string d) { f71Total++; if (ok) f71Pass++; Debug.Log("FFU71 " + (ok ? "PASS" : "FAIL") + " " + what + " | " + d); }
    void F71Info(string what, string d) { Debug.Log("FFU71 INFO " + what + " | " + d); }
    void F71Ph(string tag, int n, string name) { Debug.Log("FFDEMO " + tag + " ph " + n + " " + name + " t " + Time.time.ToString("0.0")); }
    void F71Result(string tag) { Debug.Log("FFU71 RESULT " + f71Pass + "/" + f71Total + " (" + tag + ")"); Debug.Log("FFDEMO " + tag + " done"); }

    void Phone71TestLobby() { if (p71On) return; p71On = true; StartCoroutine(P71()); }

    IEnumerator P71Stick(Gamepad p, Vector2 v)
    {
        var st = new GamepadState(); st.leftStick = v;
        InputSystem.QueueStateEvent(p, st);
        yield return L59Frames(2);
        InputSystem.QueueStateEvent(p, new GamepadState());
        yield return L59Frames(3);
        yield return L59Settle(0.35f);
    }

    static bool RectIn(Rect a, Rect b, float m) { return a.xMin >= b.xMin - m && a.yMin >= b.yMin - m && a.xMax <= b.xMax + m && a.yMax <= b.yMax + m; }
    static string R(Rect r) { return "[" + r.xMin.ToString("0") + "," + r.yMin.ToString("0") + " - " + r.xMax.ToString("0") + "," + r.yMax.ToString("0") + "]"; }

    // every OPEN phone: its view rect is its player's camera rect, the panel sits inside that camera's pixels, the PHONE button
    // does not cover the panel, and no two open panels overlap
    void P71Layout(string tag)
    {
        bool ok = true; var sb = new System.Text.StringBuilder();
        var open = new System.Collections.Generic.List<RobotPhone>();
        for (int k = 0; k < 4; k++)
        {
            var p = RobotPhone.Seat[k]; if (p == null || !p.open) continue;
            Slot s = SlotOfSeat(k); if (s == null || s.cam == null) { ok = false; sb.Append("seat" + (k + 1) + " no slot/cam; "); continue; }
            open.Add(p);
            Rect vr = p.ViewRect(s), cr = s.cam.rect, px = s.cam.pixelRect, pr = p.PanelScreenRect, br = p.ButtonScreenRect;
            bool same = Mathf.Abs(vr.x - cr.x) < 0.01f && Mathf.Abs(vr.y - cr.y) < 0.01f && Mathf.Abs(vr.width - cr.width) < 0.01f && Mathf.Abs(vr.height - cr.height) < 0.01f;
            bool inside = RectIn(pr, px, 2f);
            bool btnOk = !p.ButtonShown || (RectIn(br, px, 2f) && !br.Overlaps(pr));
            if (!same || !inside || !btnOk) ok = false;
            sb.Append("P" + (slots.IndexOf(s) + 1) + " view " + R(px) + " panel " + R(pr) + (p.ButtonShown ? " btn " + R(br) : " btn hidden") + " scale " + p.PanelScale.ToString("0.00") + (p.Compact ? " compact" : "")
                + (same ? "" : " VIEWRECT!=CAM") + (inside ? "" : " PANEL-OUTSIDE") + (btnOk ? "" : " BTN-BAD") + "; ");
        }
        for (int a = 0; a < open.Count; a++) for (int b = a + 1; b < open.Count; b++) if (open[a].PanelScreenRect.Overlaps(open[b].PanelScreenRect)) { ok = false; sb.Append("OVERLAP seat" + (open[a].seat + 1) + "/seat" + (open[b].seat + 1) + "; "); }
        F71Check(tag + ": every open phone inside its own viewport, no overlap (" + open.Count + " open)", ok && open.Count > 0, sb.ToString());
    }

    IEnumerator P71()
    {
        Debug.Log("FFU71 start phone71 (split-screen Robot Phones) old=" + F71.Old);
        yield return L59Settle(2f);
        var pads = new Gamepad[4];
        for (int k = 0; k < 2; k++)
        {
            pads[k] = InputSystem.AddDevice<Gamepad>("FFTestPad71");
            yield return L59Settle(1f);
            yield return L59Press(pads[k], GamepadButton.South);
            yield return L59Settle(0.6f);
        }
        L59Trigger(pads[0], 1f); yield return L59Frames(3); L59Trigger(pads[0], 0f);
        yield return L59Settle(4f);
        F71Check("2 pads + LT: a 2-way split", state == State.Play && slots.Count == 2 && LastViews == 2, "views " + LastViews + " | " + SeatList());
        if (slots.Count < 2) { F71Result("phone71"); yield break; }
        Frog f1 = frogs[slots[0].frog], f2 = frogs[slots[1].frog];
        RobotPhone p1 = RobotPhone.Of(f1), p2 = RobotPhone.Of(f2);
        F71Ph("phone71", 1, "split2");
        yield return L59Settle(2f);
        yield return L59Press(pads[0], GamepadButton.LeftShoulder);
        yield return L59Settle(1f);
        F71Check("P1 LB opens only P1's phone", p1 != null && p1.open && p1.user == f1 && (p2 == null || !p2.open), PhLine());
        yield return L59Press(pads[1], GamepadButton.LeftShoulder);
        yield return L59Settle(1.2f);
        F71Check("P2 LB opens P2's OWN phone (P1's stays open)", p2 != null && p2 != p1 && p2.open && p2.user == f2 && p1.open, PhLine());
        P71Layout("2-way");
        F71Ph("phone71", 2, "two");
        yield return L59Settle(5f);
        // pad nav: P2's stick moves only P2's cursor; P1 Down + A sends from P1's phone only
        int c1 = p1.CursorIdx, c2 = p2.CursorIdx;
        yield return P71Stick(pads[1], new Vector2(1f, 0f));
        F71Check("P2 stick Right moves only P2's tab cursor", p2.CursorIdx != c2 && p1.CursorIdx == c1, "P1 cur " + c1 + "->" + p1.CursorIdx + " P2 cur " + c2 + "->" + p2.CursorIdx);
        string s2 = p2.LastSent;
        yield return P71Stick(pads[0], new Vector2(0f, -1f));
        int c1b = p1.CursorIdx;
        yield return L59Press(pads[0], GamepadButton.South);
        yield return L59Settle(0.8f);
        F71Check("P1 Down + A sends an option from P1's phone only", c1b >= 7 && !string.IsNullOrEmpty(p1.LastSent) && p2.LastSent == s2 && p1.open && p2.open,
            "P1 cur " + c1b + " sent '" + p1.LastSent + "' P2 sent '" + p2.LastSent + "' toast P1 '" + (f1.toast ?? "") + "'");
        F71Ph("phone71", 3, "nav");
        yield return L59Settle(5f);
        yield return L59Press(pads[1], GamepadButton.East);   // B closes
        yield return L59Settle(0.8f);
        F71Check("P2 B closes only P2's phone", !p2.open && p1.open, PhLine());
        // in a ranch vehicle: LB is boost (ffu35) - the phone does not open there by design (it does off the ranch)
        yield return L59Press(pads[0], GamepadButton.LeftShoulder);
        yield return L59Settle(0.6f);
        Vehicle dv = null;
        foreach (Vehicle v in Vehicle.All) if (v is GroundVehicle && v.driver == null && v.CanEnter(f1) && (v.transform.position - f1.transform.position).sqrMagnitude < 400f * 400f) { dv = v; break; }
        if (dv != null && !p1.open)
        {
            f1.EnterVehicle(dv);
            yield return L59Settle(1.5f);
            yield return L59Press(pads[0], GamepadButton.LeftShoulder);
            yield return L59Settle(0.8f);
            F71Info("P1 LB while driving on the ranch", "vehicle " + dv.Title + " in " + (f1.vehicle != null) + " phone open " + p1.open + " (ffu35 design: LB = boost in vehicles, phone opens on foot and in vehicles off the ranch)");
            if (f1.vehicle != null) f1.ExitVehicle();
            yield return L59Settle(1.5f);
            if (p1.open) p1.Close(f1, "probe");
        }
        else F71Info("P1 LB while driving on the ranch", "skipped (vehicle " + (dv != null ? dv.Title : "none") + ", phone open " + p1.open + ")");
        // 4-way: two more pads join mid-game, everyone opens a phone
        for (int k = 2; k < 4; k++)
        {
            pads[k] = InputSystem.AddDevice<Gamepad>("FFTestPad71");
            yield return L59Settle(1f);
            yield return L59Press(pads[k], GamepadButton.South);
            yield return L59Settle(1.5f);
        }
        yield return L59Settle(2f);
        F71Check("2 more pads join: a 4-way split", slots.Count == 4 && LastViews == 4, "views " + LastViews + " | " + SeatList());
        for (int k = 0; k < 4 && k < slots.Count; k++) { var pk = RobotPhone.Of(frogs[slots[k].frog]); if (pk != null && !pk.open) { yield return L59Press(pads[k], GamepadButton.LeftShoulder); yield return L59Settle(0.9f); } }
        int nOpen = 0; for (int k = 0; k < 4; k++) if (RobotPhone.Seat[k] != null && RobotPhone.Seat[k].open) nOpen++;
        F71Check("4-way: all four players' own phones open", nOpen == 4 && slots.Count == 4, PhLine());
        P71Layout("4-way");
        if (slots.Count == 4)
        {
            RobotPhone p4 = RobotPhone.Of(frogs[slots[3].frog]), p3 = RobotPhone.Of(frogs[slots[2].frog]);
            yield return P71Stick(pads[3], new Vector2(0f, 1f));   // Up from the tab row = the robot card (hidden when compact)
            F71Check("4-way compact: P4 Up = robot card, the header shows the cue; P3 untouched", p4.OnCard && p4.Compact && (p4.HeadText.Contains("ffbf33") || F71.Old) && !p3.OnCard,
                "P4 onCard " + p4.OnCard + " compact " + p4.Compact + " head '" + p4.HeadText + "' P3 onCard " + p3.OnCard);
            string h0 = p4.HeadText;
            yield return P71Stick(pads[3], new Vector2(1f, 0f));   // Right on the card = next robot
            F71Check("4-way compact: P4 Right on the card swaps P4's robot only", p4.HeadText != h0 && p4.OnCard, "head '" + h0 + "' -> '" + p4.HeadText + "'");
        }
        F71Ph("phone71", 4, "four");
        yield return L59Settle(6f);
        F71Result("phone71");
    }
}

// Item 5 - the second jet: ?ffdemo=1&ffshot=jet71 (play probe, P1 = James): two jets in the hangar (clearances), James's jet
// unchanged, the second jet unnamed + same who-can-fly rule; James hovers it under the shut side roof (held under the ceiling),
// taxis out of the doors, climbs clear, goes to jet mode, then it is parked back in its bay.
// Logs "FFU71 PASS|FAIL ...", "FFU71 RESULT n/m (jet71)", "FFDEMO jet71 ph N <phase>", "FFDEMO jet71 done".
public class Ffu71 : MonoBehaviour
{
    public static Ffu71 I;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        I = new GameObject("ffu71").AddComponent<Ffu71>();
        DontDestroyOnLoad(I.gameObject);
    }
    bool started;
    void Update()
    {
        if (started) return;
        string s = Ffu33.Url("ffdemo") != null ? Ffu33.Url("ffshot") : null;
        if (s != "jet71") { enabled = false; return; }
        if (Game.I == null || Game.I.state != Game.State.Play || Game.I.slots.Count == 0 || JetBase.I == null) return;
        started = true;
        Game.I.StartCoroutine(Game.I.J71Run());
    }
}

public partial class Game
{
    Jet.JetIn j71In;
    IEnumerator J71Fly(Jet j, float secs, System.Func<bool> until)
    {
        float r0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - r0 < secs && (until == null || !until())) yield return null;
    }

    public IEnumerator J71Run()
    {
        yield return new WaitForSecondsRealtime(3f);
        Frog f = frogs[slots[0].frog];
        Jet j1 = Jet.I, j2 = JetBase.I.jet2;
        Vector2 hc = JetLayout.HangarC;
        Debug.Log("FFU71 start jet71: P1 " + f.nick + " jets " + Jet.Jets.Count + " old=" + F71.Old);
        if (F71.Old)
        {
            // before shot only: the hangar with James's jet alone
            Vector3 ap0 = new Vector3(JetLayout.DoorX - 15f, 0f, hc.y + 4f); ap0.y = Ranch.GY(ap0.x, ap0.z) + 0.3f;
            f.Teleport(ap0); f.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            if (slots[0].rig != null) slots[0].rig.SetYaw(90f);
            yield return new WaitForSecondsRealtime(4f);
            F71Ph("jet71", 1, "hangar");
            yield return new WaitForSecondsRealtime(7f);
            F71Result("jet71");
            yield break;
        }
        F71Check("two jets in the hangar (James's jet + the second jet)", Jet.Jets.Count == 2 && j1 != null && j2 != null && j1 != j2 && j1.style == 0 && j2.style == 1,
            "jets " + Jet.Jets.Count + " j1 " + (j1 != null ? j1.Title + " @" + j1.transform.position.ToString("0.0") : "-") + " j2 " + (j2 != null ? j2.Title + " @" + j2.transform.position.ToString("0.0") : "-"));
        if (j1 == null || j2 == null) { F71Result("jet71"); yield break; }
        float dz = Mathf.Abs(j2.homePos.z - j1.homePos.z), wingGap = dz - 4.7f - Jet.Span1, wallGap = (JetLayout.HangarD * 0.5f - 0.3f) - (Mathf.Abs(j2.homePos.z - hc.y) + Jet.Span1), postGap = (JetLayout.HangarD * 0.5f - 1f) - (Mathf.Abs(j2.homePos.z - hc.y) + Jet.Span1);
        bool inHangar = Mathf.Abs(j2.homePos.x - hc.x) < JetLayout.HangarW * 0.5f - 6.5f && Mathf.Abs(j2.homePos.z - hc.y) < JetLayout.HangarD * 0.5f;
        F71Check("second jet parks in the same hangar, clear of James's jet, the side wall and the door posts", inHangar && wingGap > 0.1f && wallGap > 0.3f && postGap > 0.1f,
            "wingtip gap " + wingGap.ToString("0.00") + " m, wall gap " + wallGap.ToString("0.00") + " m, door-post gap " + postGap.ToString("0.00") + " m");
        F71Check("James's jet unchanged (Jet.I, centre bay, name)", Jet.I == j1 && Mathf.Abs(j1.homePos.z - hc.y) < 0.01f && j1.Title == "James's jet", "home " + j1.homePos.ToString("0.0"));
        F71Check("the second jet is unnamed (no lettering, title 'the second jet')", j2.Title == "the second jet" && j2.GetComponentsInChildren<TextMesh>(true).Length == 0, "texts " + j2.GetComponentsInChildren<TextMesh>(true).Length);
        Frog ai = null; foreach (Frog o in frogs) if (o != null && !o.human && o.gameObject.activeInHierarchy) { ai = o; break; }
        F71Check("same who-can-fly rule as James's jet (James yes, AI froggies no)", j2.CanEnter(f) == j1.CanEnter(f) && j2.CanEnter(f) && (ai == null || !j2.CanEnter(ai)), "James " + j2.CanEnter(f) + " AI " + (ai != null ? ai.nick + " " + j2.CanEnter(ai) : "-"));
        // hangar shot from the apron
        Vector3 ap = new Vector3(JetLayout.DoorX - 15f, 0f, hc.y + 4f);
        ap.y = Ranch.GY(ap.x, ap.z) + 0.3f;
        f.Teleport(ap); f.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        if (slots[0].rig != null) slots[0].rig.SetYaw(90f);
        yield return new WaitForSecondsRealtime(4f);
        F71Ph("jet71", 1, "hangar");
        yield return new WaitForSecondsRealtime(7f);
        // fly the second jet: hover up in the side bay (roof stays shut there), taxi out of the doors, climb, jet mode
        f.Teleport(j2.transform.position + Vector3.right * 2f + Vector3.forward * -4f);
        yield return null;
        f.EnterVehicle(j2);
        yield return new WaitForSecondsRealtime(1f);
        F71Check("James gets in the second jet", f.vehicle == j2 && j2.driver == f, "vehicle " + (f.vehicle != null ? f.vehicle.Title : "-"));
        j2.autopilot = q => j71In;
        float ceil = JetBase.FloorY + JetLayout.HangarH - 3.4f;
        j71In = new Jet.JetIn { climb = 1f };
        yield return J71Fly(j2, 6f, null);
        float yUp = j2.transform.position.y;
        F71Check("hovering up in the side bay: held under the shut roof", yUp <= ceil + 0.1f && yUp > JetBase.FloorY + 2f && JetBase.I.RoofK < 0.5f, "y " + yUp.ToString("0.0") + " ceiling " + ceil.ToString("0.0") + " roofK " + JetBase.I.RoofK.ToString("0.00"));
        j71In = new Jet.JetIn { climb = -1f };
        yield return J71Fly(j2, 6f, () => j2.agl < 2.6f);
        j71In = new Jet.JetIn { fwd = 0.6f };
        yield return J71Fly(j2, 45f, () => j2.transform.position.x < JetLayout.DoorX - 14f);
        F71Check("taxis out through the hangar doors", j2.transform.position.x < JetLayout.DoorX - 10f, "x " + j2.transform.position.x.ToString("0.0") + " (doors at " + JetLayout.DoorX.ToString("0.0") + ") door k " + JetBase.I.DoorK.ToString("0.00"));
        j71In = new Jet.JetIn { climb = 1f };
        yield return J71Fly(j2, 20f, () => j2.transform.position.y > JetBase.FloorY + JetLayout.HangarH + 12f);
        F71Check("climbs clear of the hangar", j2.transform.position.y > JetBase.FloorY + JetLayout.HangarH + 8f, "y " + j2.transform.position.y.ToString("0.0") + " mode " + j2.mode);
        if (slots[0].rig != null) slots[0].rig.SetYaw(j2.transform.eulerAngles.y + 160f);
        F71Ph("jet71", 2, "jet2hover");
        yield return new WaitForSecondsRealtime(6f);
        j71In = new Jet.JetIn { fwd = 1f, climb = 0.3f };
        yield return J71Fly(j2, 25f, () => !j2.vtol && j2.nz < 0.3f);
        j71In = new Jet.JetIn { thr = 0.6f, pitch = 0.05f };   // pitch > 0 = gentle pull up
        yield return J71Fly(j2, 4f, null);
        F71Check("same flight model: transition to jet mode", !j2.vtol && j2.mode == Jet.Mode.Jet, "mode " + j2.mode + " vtol " + j2.vtol + " nz " + j2.nz.ToString("0.00") + " speed " + j2.vel.magnitude.ToString("0") + " m/s");
        if (slots[0].rig != null) slots[0].rig.SetYaw(j2.transform.eulerAngles.y + 150f);
        F71Ph("jet71", 3, "jet2fly");
        yield return new WaitForSecondsRealtime(6f);
        j2.autopilot = null;
        j2.ResetToPad();
        yield return new WaitForSecondsRealtime(1f);
        if (f.vehicle == j2) f.ExitVehicle();
        yield return new WaitForSecondsRealtime(1.5f);
        F71Check("parked back in its bay beside James's jet", f.vehicle == null && (j2.transform.position - j2.homePos).magnitude < 0.5f && (j1.transform.position - j1.homePos).magnitude < 0.5f, "j2 " + j2.transform.position.ToString("0.0") + " home " + j2.homePos.ToString("0.0"));
        F71Result("jet71");
    }
}
