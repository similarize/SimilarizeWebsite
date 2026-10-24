using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// ffu73 probe: ?ffdemo=1&ffshot=ffu73 (&ff73old=1 = before). 2-way split, P1 = James pilots his trillion-story mech, P2 on the
// ranch in front of it looking up at its face. Bridge built, P1 in the captain's chair, AI froggies in crew seats, no glass
// bubble outside (flush visor), R3 = bridge view (P1's camera inside the room), robot seat = the chair; then the mech flies to
// space: riders inside the space copy's bridge (no spacesuits on the shoulders), P1's bridge view in space, R3 = outside view;
// lands home; P1 climbs out and the riders beam down to the ground. Logs "FFU73 PASS|FAIL ...", "FFU73 RESULT n/m (ffu73)",
// "FFDEMO ffu73 ph N <ranch|spacein|spaceout|home>".
public partial class Game
{
    int f73Pass, f73Total; bool b73On;
    void F73Check(string what, bool ok, string d) { f73Total++; if (ok) f73Pass++; Debug.Log("FFU73 " + (ok ? "PASS" : "FAIL") + " " + what + " | " + d); }
    void F73Info(string what, string d) { Debug.Log("FFU73 INFO " + what + " | " + d); }
    void Bridge73TestLobby() { if (b73On) return; b73On = true; StartCoroutine(B73()); }

    static int Helmets73(System.Collections.Generic.List<Frog> fs)
    {
        int n = 0;
        foreach (var g in fs) if (g != null) foreach (var r in g.GetComponentsInChildren<Renderer>()) if (r.gameObject.name == "Helmet" && r.enabled && r.gameObject.activeInHierarchy) n++;
        return n;
    }

    IEnumerator B73()
    {
        const string T = "ffu73";
        Debug.Log("FFU73 start bridge (trillion-story mech) old=" + F73.Old);
        var pads = new Gamepad[2];
        yield return F72Join(pads, 2, "FFTestPad73");
        F73Check("2 pads + LT: a 2-way split", state == State.Play && slots.Count == 2 && LastViews == 2, "views " + LastViews + " | " + SeatList());
        if (slots.Count < 2) { F73Result(T); yield break; }
        Frog fa = frogs[slots[0].frog], fb = frogs[slots[1].frog];
        StoryMech big = FindMech(fa.charId, 3);
        StoryMech small = FindMech(fa.charId, 0), mid = FindMech(fa.charId, 2);
        F73Check("P1's trillion-story mech found (James)", big != null && fa.charId == 0, big != null ? big.Title + " H " + big.height : "none (char " + fa.charId + ")");
        if (big == null) { F73Result(T); yield break; }
        F73Check("10 / 100-story mechs have NO bridge (bubbles unchanged); 1000-story has one (ffbr66)", small != null && small.bridge73 == null && (mid == null || (mid.bridge73 != null) != F73.Old66) && FindMech(1, 1) != null && FindMech(1, 1).bridge73 == null,
            (small != null ? small.Title : "-") + " / " + (mid != null ? mid.Title : "-"));
        fa.SendTo(WorldId.Ranch, big.transform.position + big.transform.right * (big.height * 0.3f + 4f), 0f);
        yield return L59Frames(2);
        fa.EnterVehicle(big);
        yield return L59Settle(1.5f);
        var b = big.bridge73;
        F73Check("P1 pilots the trillion-story mech", fa.vehicle == big, "vehicle " + (fa.vehicle != null ? fa.vehicle.Title : "-"));
        F73Check("the bridge is built inside the head (enclosed room, captain's chair, crew seats, viewscreen)", b != null && b.captain != null && b.crew[3] != null && b.transform.IsChildOf(big.Bone64(1)) && b.transform.Find("Visor73") != null,
            b != null ? "parts " + b.GetComponentsInChildren<Renderer>(true).Length + " scale " + b.k : "no bridge");
        F73Check("P1 sits in the captain's chair, visible, froggy-sized", b != null && fa.transform.parent == b.captain && fa.model.gameObject.activeInHierarchy && Mathf.Abs(fa.transform.lossyScale.x - 1f) < 0.05f && b.Inside(fa.transform.position + Vector3.up * 0.4f),
            "parent " + (fa.transform.parent != null ? fa.transform.parent.name : "-") + " scale " + fa.transform.lossyScale.x.ToString("0.00") + " shown " + fa.model.gameObject.activeInHierarchy);
        // AI froggies take the crew seats
        var ai = new System.Collections.Generic.List<Frog>();
        for (int k = 0; k < frogs.Count; k++) if (frogs[k] != null && !frogs[k].human && frogs[k] != fa && frogs[k] != fb) ai.Add(frogs[k]);
        if (b != null) foreach (var g in ai) Bridge73.Board(g, big);
        yield return L59Settle(1.5f);
        int seated = 0;
        if (b != null) foreach (var g in ai) if (g.passengerOf == big && b.Inside(g.transform.position + Vector3.up * 0.3f) && g.model.gameObject.activeInHierarchy) seated++;
        F73Check("AI froggy riders sit in the bridge's crew seats", ai.Count > 0 && seated == ai.Count, "AI " + ai.Count + " seated " + seated);
        F73Check("a robot pilot would sit in the captain's chair (no glass canopy on the trillion-story head)", b != null && big.CanopySeat != null && big.CanopySeat.IsChildOf(b.captain) && big.Bone64(1).Find("RobotCanopy") == null,
            "robot seat " + (big.CanopySeat != null ? big.CanopySeat.name + " under " + big.CanopySeat.parent.name : "-"));
        int helm = Helmets73(ai);
        int glass = 0; foreach (var r in big.Bone64(1).GetComponentsInChildren<Renderer>(true)) if (r.gameObject.name == "glass") glass++;
        int smallGlass = 0; if (small != null) foreach (var r in small.Bone64(1).GetComponentsInChildren<Renderer>(true)) if (r.gameObject.name == "glass") smallGlass++;
        F73Check("outside: no bubble (no glass cockpit dome on the head, no canopy, no rider helmets), flush visor", helm == 0 && glass == 0 && big.Bone64(1).Find("RobotCanopy") == null && b != null, "head glass " + glass + " helmets " + helm);
        F73Check("the 10-story mech keeps its glass cockpit dome", smallGlass > 0, "10-story head glass " + smallGlass);
        // P2 out in front looking up at the face; P1 presses R3 = bridge view
        Vector3 fw = big.transform.forward; fw.y = 0f; fw.Normalize();
        Vector3 p2 = big.transform.position + fw * (big.height * 1.05f); p2.y = Ranch.GY(p2.x, p2.z) + 0.5f;
        float face = Mathf.Atan2(-fw.x, -fw.z) * Mathf.Rad2Deg;
        fb.SendTo(WorldId.Ranch, p2, face);
        if (slots[1].rig != null) { slots[1].rig.minPitch = -45f; slots[1].rig.SetYaw(face); slots[1].rig.SetPitch(-36f); }
        int ci0 = Bridge73.CamsInside;
        yield return L59Press(pads[0], GamepadButton.RightStick);
        yield return L59Settle(1.5f);
        Camera ca = slots[0].rig != null ? slots[0].rig.cam : null;
        F73Check("R3: P1's camera is on the bridge (behind the chair, through the viewscreen)", Bridge73.ViewOn(slots[0].rig) && ca != null && b != null && b.Inside(ca.transform.position) && Bridge73.CamsInside > ci0,
            "view " + Bridge73.ViewOn(slots[0].rig) + " cam " + (ca != null ? ca.transform.position.ToString("0.0") : "-") + " inside-frames " + (Bridge73.CamsInside - ci0));
        Debug.Log("FFDEMO " + T + " ph 1 ranch t " + Time.time.ToString("0.0"));
        yield return L59Settle(12f);
        // to space: riders ride inside the space copy's bridge
        var sw = SpaceWorld.I;
        if (sw != null) sw.EnsureBuilt();
        big.GoToSpace();
        yield return F72Until(() => fa.world == WorldId.Space, 30f);
        yield return L59Settle(2.5f);
        Starship s = fa.vehicle as Starship;
        F73Check("the trillion-story mech flies to space with P1", s != null && s.mechForm == big, s != null ? s.Title : "no ship");
        if (s != null && !F73.Old)
        {
            int inS = 0; foreach (var g in ai) if (g.passengerOf == s) inS++;
            if (inS < ai.Count) { s.DemoRiders(ai.ToArray()); yield return L59Settle(1f); inS = 0; foreach (var g in ai) if (g.passengerOf == s) inS++; }
            int inside = 0; foreach (var g in ai) if (g.passengerOf == s && s.bridge73 != null && s.bridge73.Inside(g.transform.position + Vector3.up * 0.03f)) inside++;
            F73Check("space: riders are INSIDE the copy's bridge (not on the shoulders), no spacesuit bubbles", s.bridge73 != null && inS == ai.Count && inside == ai.Count && Helmets73(ai) == 0,
                "aboard " + inS + "/" + ai.Count + " inside " + inside + " helmets " + Helmets73(ai));
            F73Check("space: P1 sits in the copy's captain's chair", s.bridge73 != null && s.bridge73.Inside(fa.transform.position + Vector3.up * 0.03f) && fa.model.gameObject.activeInHierarchy, "P1 at " + fa.transform.position.ToString("0.0"));
        }
        else if (s != null) { s.DemoRiders(ai.ToArray()); yield return L59Settle(1f); F73Info("space (old)", "riders on the shoulders, helmets " + Helmets73(ai)); }
        // fix 1: judged by frames RENDERED from inside a bridge (the camera is re-posed right before it renders; between frames a
        // fast ship has already moved on)
        int ci1 = Bridge73.CamsInside;
        yield return L59Settle(1f);
        F73Check("space: bridge view puts P1's camera inside the copy's bridge", s != null && s.bridge73 != null && Bridge73.ViewOn(slots[0].rig) && Bridge73.CamsInside > ci1, "view " + Bridge73.ViewOn(slots[0].rig) + " inside-frames " + (Bridge73.CamsInside - ci1));
        Debug.Log("FFDEMO " + T + " ph 2 spacein t " + Time.time.ToString("0.0"));
        yield return L59Settle(10f);
        yield return L59Press(pads[0], GamepadButton.RightStick);
        yield return L59Settle(0.5f);
        if (slots[0].rig != null && s != null) { slots[0].rig.SetYaw(s.transform.eulerAngles.y + 150f); slots[0].rig.SetPitch(12f); }
        yield return L59Settle(1f);
        F73Check("R3 again: outside view (chase camera outside the hull)", !Bridge73.ViewOn(slots[0].rig) && (s == null || s.bridge73 == null || !s.bridge73.Inside(slots[0].rig.cam.transform.position)), "view " + Bridge73.ViewOn(slots[0].rig));
        Debug.Log("FFDEMO " + T + " ph 3 spaceout t " + Time.time.ToString("0.0"));
        yield return L59Settle(10f);
        // home
        if (s != null && sw != null)
        {
            int earth = sw.Find("earth");
            s.EnterOrbit(earth, sw.bodies[earth].cap, 0f); yield return L59Frames(3);
            sw.Land(s, earth);
            { float r0 = Time.realtimeSinceStartup; while ((LaunchSeq.Busy || fa.launching || fa.world != WorldId.Ranch) && Time.realtimeSinceStartup - r0 < 120f) { LaunchSeq.Skip(fa); yield return null; } }
            yield return L59Settle(2f);
        }
        foreach (var g in ai) if (g != null && g.world == WorldId.Space) { g.LeavePassenger(); g.SendTo(WorldId.Ranch, Ranch.FrogSpawn(g.id), 0f); }
        yield return L59Settle(1f);
        F73Check("landed: P1 is back in the chair of the ranch mech", fa.world == WorldId.Ranch && fa.vehicle == big && (F73.Old || fa.transform.parent == big.bridge73.captain), "world " + fa.world + " veh " + (fa.vehicle != null ? fa.vehicle.Title : "-"));
        if (big.bridge73 != null) foreach (var g in ai) Bridge73.Board(g, big);
        yield return L59Settle(1f);
        fa.ExitVehicle();
        yield return L59Settle(1.5f);
        int down = 0; foreach (var g in ai) if (g.passengerOf == null && g.world == WorldId.Ranch && g.transform.position.y < Ranch.GY(g.transform.position.x, g.transform.position.z) + 4f) down++;
        F73Check("P1 climbs out: the riders beam down to the ground (nobody left in the head)", big.bridge73 != null && down == ai.Count && Bridge73.Riders(big) == 0, "down " + down + "/" + ai.Count + " still aboard " + Bridge73.Riders(big));
        Debug.Log("FFDEMO " + T + " ph 4 home t " + Time.time.ToString("0.0"));
        yield return L59Settle(4f);
        F73Result(T);
    }
    void F73Result(string tag) { Debug.Log("FFU73 RESULT " + f73Pass + "/" + f73Total + " (" + tag + ")"); Debug.Log("FFDEMO " + tag + " done"); }
}
