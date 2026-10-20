using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// ffu67c probe: ?ffdemo=1&ffshot=mode67 - lobby mode pick -> FREE PLAY -> 3 players in SHARED view -> P1 walks James's
// trillion-story mech (per-footstep rumble: pilot + ground players by distance, logged) -> exits (rumble zero) -> the jump from
// the mech's head under the paraglider (visible in the shared camera) -> pause menu BACK TO LOBBY (confirm) -> STORY.
// Logs "MODE67 PASS|FAIL ...", "MODE67 RESULT n/m", "FFDEMO mode67 ph N <name>", "FFDEMO mode67 done" (+ RUMBLE67 / LOBBY67 lines).
public partial class Game
{
    bool m67On; int m67Pass, m67Total, m67Ph;
    void Mode67Lobby() { if (m67On) return; m67On = true; StartCoroutine(M67Run()); }
    void M67Check(string what, bool ok, string d) { m67Total++; if (ok) m67Pass++; Debug.Log("MODE67 " + (ok ? "PASS" : "FAIL") + " " + what + " | " + d); }
    void M67Next(string n) { Debug.Log("FFDEMO mode67 ph " + (m67Ph++) + " " + n + " t " + Time.time.ToString("0.0")); }
    IEnumerator M67F(int n) { for (int k = 0; k < n; k++) yield return null; }
    IEnumerator M67Wait(float s) { yield return new WaitForSecondsRealtime(s); yield return M67F(2); }
    IEnumerator M67Game(float s) { float t = 0f, r0 = Time.realtimeSinceStartup; while (t < s && Time.realtimeSinceStartup - r0 < 120f) { yield return null; t += Time.deltaTime; } }
    IEnumerator M67Press(Gamepad p, GamepadButton b) { InputSystem.QueueStateEvent(p, new GamepadState().WithButton(b)); yield return M67F(2); InputSystem.QueueStateEvent(p, new GamepadState()); yield return M67F(2); }
    IEnumerator M67LT(Gamepad p) { var st = new GamepadState(); st.leftTrigger = 1f; InputSystem.QueueStateEvent(p, st); yield return M67F(2); InputSystem.QueueStateEvent(p, new GamepadState()); yield return M67F(3); }

    IEnumerator M67Run()
    {
        yield return M67Wait(3f);
        Gamepad A = InputSystem.AddDevice<Gamepad>("FFTestPad67c");
        yield return M67Wait(1.2f);
        yield return M67Press(A, GamepadButton.South);
        yield return M67Wait(1f);
        M67Check("lobby: pad A joins, FREE PLAY highlighted, nothing started", state == State.Lobby && slots.Count == 1 && lobbyMode == 0, "state " + state + " mode " + ModeName(lobbyMode) + " | " + SeatList());
        M67Next("lobbyfree");
        yield return M67Wait(1.5f);
        yield return M67Press(A, GamepadButton.LeftShoulder);
        yield return M67Wait(1f);
        bool pickerOpen = picker != null && picker.IsOpen;
        M67Check("LB switches the highlight to STORY - and does NOT start a mode", state == State.Lobby && lobbyMode == 1 && !pickerOpen && !Story.Active, "state " + state + " mode " + ModeName(lobbyMode) + " picker " + pickerOpen);
        M67Next("lobbystory");
        yield return M67Wait(1.5f);
        yield return M67Press(A, GamepadButton.LeftShoulder);
        yield return M67Wait(1f);
        M67Check("LB again -> FREE PLAY highlighted", state == State.Lobby && lobbyMode == 0, "mode " + ModeName(lobbyMode));
        yield return M67LT(A);
        yield return M67Wait(2f);
        M67Check("LT starts the highlighted FREE PLAY (no story)", state == State.Play && !Story.Active, "state " + state + " story " + Story.Active);
        if (state != State.Play) { M67Done(); yield break; }
        // 2 more players join mid-game, shared view
        Gamepad B = InputSystem.AddDevice<Gamepad>("FFTestPad67c"); yield return M67Wait(1.2f); yield return M67Press(B, GamepadButton.South); yield return M67Wait(1f);
        Gamepad C = InputSystem.AddDevice<Gamepad>("FFTestPad67c"); yield return M67Wait(1.2f); yield return M67Press(C, GamepadButton.South); yield return M67Wait(1.5f);
        if (!shared) { ToggleView(); yield return M67Wait(0.6f); }
        Slot sa = FindPad(A), sb = FindPad(B), sc = FindPad(C);
        M67Check("3 players in ONE shared view", sa != null && sb != null && sc != null && slots.Count == 3 && shared && sharedCam.enabled, SeatList() + " shared " + shared + " sharedCam " + sharedCam.enabled);
        if (sa == null || sb == null || sc == null) { M67Done(); yield break; }
        Frog fa = frogs[sa.frog], fb = frogs[sb.frog], fc = frogs[sc.frog];
        StoryMech big = null; foreach (var m in StoryMech.AllMechs) if (m != null && m.band == 3 && m.driver == null) { big = m; break; }
        M67Check("trillion-story mech found", big != null, big != null ? big.Title + " at " + big.transform.position.ToString("0") : "none");
        if (big == null) { M67Done(); yield break; }
        Vector3 mp = big.transform.position, fw = big.transform.forward; fw.y = 0f; fw.Normalize();
        Vector3 rt = Vector3.Cross(Vector3.up, fw);
        fa.SendTo(WorldId.Ranch, mp + rt * 6f, 0f); yield return M67F(2);
        fa.EnterVehicle(big);
        Vector3 pb = mp + fw * 40f + rt * 10f, pc = mp + fw * 30f - rt * 70f;   // P2 ~10-40 m from the feet, P3 further out
        fb.SendTo(WorldId.Ranch, new Vector3(pb.x, Ranch.GY(pb.x, pb.z) + 0.4f, pb.z), 0f);
        fc.SendTo(WorldId.Ranch, new Vector3(pc.x, Ranch.GY(pc.x, pc.z) + 0.4f, pc.z), 0f);
        yield return M67Wait(2f);
        M67Check("P1 pilots the trillion-story mech", fa.vehicle == big, "vehicle " + (fa.vehicle != null ? fa.vehicle.Title : "-"));
        M67Next("mech");
        // walk it: left stick forward on P1's pad (game time ~10 s); footfalls -> per-player pulses (RUMBLE67 footfall lines)
        int ff0 = Footfalls, pu0 = RumblePulses;
        int pa0 = PulsesOf(A), pb0 = PulsesOf(B), pc0 = PulsesOf(C);
        PadMaxAmp.Clear();
        var walk = new GamepadState(); walk.leftStick = new Vector2(0f, 1f);
        InputSystem.QueueStateEvent(A, walk);
        float t = 0f, r0 = Time.realtimeSinceStartup; int maxActive = 0;
        while (t < 10f && Time.realtimeSinceStartup - r0 < 150f) { yield return null; t += Time.deltaTime; maxActive = Mathf.Max(maxActive, RumbleActive); }
        InputSystem.QueueStateEvent(A, new GamepadState());
        int steps = Footfalls - ff0, pulses = RumblePulses - pu0;
        M67Check("mech walk: rumble pulses only on footfalls (pulses <= 3 per footfall, never more than the 3 pads)", steps > 0 && pulses <= steps * 3 && maxActive <= 3, "footfalls " + steps + " pulses " + pulses + " max active pads " + maxActive);
        // per player: each pad keeps its own state; the pilot's pad pulses, no pad gets more than one pulse per footfall
        int na = PulsesOf(A) - pa0, nb = PulsesOf(B) - pb0, nc = PulsesOf(C) - pc0;
        float db = Vector3.Distance(fb.transform.position, big.transform.position), dc = Vector3.Distance(fc.transform.position, big.transform.position);
        string per = "P1 pilot " + na + " pulses max " + MaxAmpOf(A).ToString("0.00") + " | P2 " + db.ToString("0") + " m " + nb + " max " + MaxAmpOf(B).ToString("0.00") + " | P3 " + dc.ToString("0") + " m " + nc + " max " + MaxAmpOf(C).ToString("0.00") + " | footfalls " + steps;
        M67Check("per-player rumble: pilot pad pulses on footfalls, every pad <= 1 pulse per footfall", na > 0 && na <= steps && nb <= steps && nc <= steps, per);
        Debug.Log("MODE67 note per-pad " + per + ((db <= dc) == (MaxAmpOf(B) >= MaxAmpOf(C) - 0.01f) ? " (nearer = stronger)" : " (distance order differs)"));
        // standing still: no new pulses
        yield return M67Game(3f);
        int pu1 = RumblePulses; yield return M67Game(3f);
        M67Check("mech standing still: no rumble between steps", RumblePulses == pu1 && RumbleActive == 0, "pulses while still " + (RumblePulses - pu1) + " active pads " + RumbleActive);
        // exit with A (deploy) -> zero on every pad
        yield return M67Press(A, GamepadButton.South);
        yield return M67Wait(1f);
        if (fa.vehicle == big) { fa.ExitVehicle(); yield return M67F(2); Debug.Log("MODE67 note: pad A did not exit the mech, ExitVehicle called"); }
        M67Check("out of the mech: rumble ZERO on every pad", fa.vehicle == null && RumbleActive == 0, "vehicle " + (fa.vehicle != null ? fa.vehicle.Title : "-") + " active pads " + RumbleActive + " stops " + RumbleStops);
        // the jump from the trillion-story mech's head: freefall, then the paraglider
        Vector3 side = big.transform.right; side.y = 0f; side.Normalize();
        Vector3 head = big.transform.position + Vector3.up * big.height * 0.8f + side * (big.height * 0.3f + 2.5f);
        fa.BeginBail(big.Title, head, Vector3.zero, false);
        float gt = 0f; r0 = Time.realtimeSinceStartup;
        while (!fa.GlideFlying && gt < 15f && Time.realtimeSinceStartup - r0 < 150f) { yield return null; gt += Time.deltaTime; }
        yield return M67Game(1.5f);
        M67Next("glider");
        yield return M67F(3);
        GameObject g = fa.GliderGo; bool vis = false; string det = "no glider";
        if (g != null)
        {
            var planes = GeometryUtility.CalculateFrustumPlanes(sharedCam);
            int nr = 0, inView = 0, forcedOff = 0;
            foreach (var r in g.GetComponentsInChildren<Renderer>()) { nr++; if (r.forceRenderingOff) forcedOff++; else if (r.enabled && r.gameObject.activeInHierarchy && GeometryUtility.TestPlanesAABB(planes, r.bounds)) inView++; }
            Vector3 vp = sharedCam.WorldToViewportPoint(g.transform.position);
            vis = g.activeInHierarchy && inView > 0 && forcedOff == 0 && vp.z > 0f && vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f;
            det = "active " + g.activeInHierarchy + " renderers " + nr + " in shared view " + inView + " culled " + forcedOff + " viewport " + vp.ToString("0.00") + " glider y " + g.transform.position.y.ToString("0") + " cam " + sharedCam.transform.position.ToString("0") + " far " + sharedCam.farClipPlane.ToString("0") + " frameR " + SharedFrameR.ToString("0");
        }
        M67Check("SHARED view (3 players): the paraglider renders and is in frame", fa.GlideFlying && vis, "flying " + fa.GlideFlying + " | " + det);
        // SPLIT screen: same glider in P1's own view
        if (shared) { ToggleView(); yield return M67Wait(1.2f); }
        bool vs = false; string ds = "no glider / no P1 cam";
        if (g != null && sa.cam != null && sa.cam.enabled)
        {
            var pl = GeometryUtility.CalculateFrustumPlanes(sa.cam);
            int nr = 0, inView = 0, forcedOff = 0;
            foreach (var r in g.GetComponentsInChildren<Renderer>()) { nr++; if (r.forceRenderingOff) forcedOff++; else if (r.enabled && r.gameObject.activeInHierarchy && GeometryUtility.TestPlanesAABB(pl, r.bounds)) inView++; }
            Vector3 vp = sa.cam.WorldToViewportPoint(g.transform.position);
            vs = g.activeInHierarchy && inView > 0 && forcedOff == 0 && vp.z > 0f && vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f;
            ds = "active " + g.activeInHierarchy + " renderers " + nr + " in P1 view " + inView + " culled " + forcedOff + " viewport " + vp.ToString("0.00") + " rect " + sa.cam.rect;
        }
        M67Next("glidersplit");
        M67Check("SPLIT screen: the paraglider renders in P1's own view", !shared && fa.GlideFlying && vs, "shared " + shared + " flying " + fa.GlideFlying + " | " + ds);
        yield return M67Wait(2f);
        // pause menu -> BACK TO LOBBY (View asks, A confirms)
        yield return M67Press(A, GamepadButton.Start);
        yield return M67Wait(1f);
        bool helpOpen = help;
        yield return M67Press(A, GamepadButton.Select);
        yield return M67Wait(1f);
        bool asking = LobbyAsking;
        M67Next("lobbyask");
        yield return M67Wait(1.5f);
        M67Check("pause menu: View asks 'BACK TO LOBBY?' (no change yet)", helpOpen && asking && state == State.Play, "help " + helpOpen + " asking " + asking + " state " + state + " label '" + (hpLobbyT != null ? hpLobbyT.text : "") + "'");
        yield return M67Press(A, GamepadButton.South);
        yield return M67Wait(2f);
        int cams = 0; foreach (var s in slots) if (s.cam != null) cams++;
        M67Check("A confirms: all 3 players back in the lobby, seated, rumble zero, no views / split / shared camera, no glider", state == State.Lobby && slots.Count == 3 && cams == 0 && !sharedCam.enabled && RumbleActive == 0 && !fa.GlideOn && fa.vehicle == null && !help,
            "state " + state + " " + SeatList() + " cams " + cams + " sharedCam " + sharedCam.enabled + " rumble " + RumbleActive + " glide " + fa.GlideOn);
        M67Next("backlobby");
        yield return M67Wait(2f);
        yield return M67Press(A, GamepadButton.LeftShoulder);
        yield return M67Wait(1f);
        M67Check("in the lobby again: LB highlights STORY (no start)", state == State.Lobby && lobbyMode == 1 && !(picker != null && picker.IsOpen), "mode " + ModeName(lobbyMode));
        yield return M67LT(A);
        yield return M67Wait(1.5f);
        pickerOpen = picker != null && picker.IsOpen;
        M67Check("LT starts the highlighted STORY: the episode picker opens", pickerOpen && state == State.Lobby, "picker " + pickerOpen + " state " + state);
        M67Next("picker");
        yield return M67Wait(2f);
        yield return M67Press(A, GamepadButton.South);
        yield return M67Wait(3f);
        M67Check("A on the episode -> STORY mode running", state == State.Play && Story.Active, "state " + state + " story " + Story.Active + " episode " + Story.Episode);
        M67Next("story");
        yield return M67Wait(4f);
        M67Done();
    }
    void M67Done() { Debug.Log("MODE67 RESULT " + m67Pass + "/" + m67Total); Debug.Log("FFDEMO mode67 done"); }
}
