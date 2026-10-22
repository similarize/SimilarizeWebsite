using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// ffu72 (Ben's 6.6 work list, Batch C) probes. Lobby probes with synthetic pads (like phone71).
// Item 6 - world-state: ?ffdemo=1&ffshot=world72 - 2-way split. P1 launches the Starship: the ranch pad is empty (P2 stands
//   there and can't board: "The Starship is up in space"), exactly one Starship exists (the one P1 flies); P1 lands: the
//   stack is back, nothing left in Earth orbit. P1's mech flies to space: it is hidden on the ranch (P2 looks at its empty
//   spot), the Starship stays on the pad; the mech drops P1 on the Moon and waits in orbit (still hidden), P1 beams back up
//   to it, flies home and lands: the mech is back on the ranch with P1 in it.
// Logs "FFU72 PASS|FAIL ...", "FFU72 RESULT n/m (tag)", "FFDEMO <tag> ph N <phase>", "FFDEMO <tag> done".
// F72.Old (&ff72old=1) = the pre-ffu72 behaviour (one shared space ship, mechs stay on the ranch, Earth-orbit copy, one chip).
public static class F72
{
    static int old = -1;
    public static bool Old { get { if (old < 0) old = Ffu33.Url("ff72old") == "1" ? 1 : 0; return old == 1; } }
}

public partial class Game
{
    int f72Pass, f72Total; bool w72On;
    void F72Check(string what, bool ok, string d) { f72Total++; if (ok) f72Pass++; Debug.Log("FFU72 " + (ok ? "PASS" : "FAIL") + " " + what + " | " + d); }
    void F72Info(string what, string d) { Debug.Log("FFU72 INFO " + what + " | " + d); }
    void F72Ph(string tag, int n, string name) { Debug.Log("FFDEMO " + tag + " ph " + n + " " + name + " t " + Time.time.ToString("0.0")); }
    void F72Result(string tag) { Debug.Log("FFU72 RESULT " + f72Pass + "/" + f72Total + " (" + tag + ")"); Debug.Log("FFDEMO " + tag + " done"); }
    IEnumerator F72Until(System.Func<bool> ok, float realSecs) { float r0 = Time.realtimeSinceStartup; while (!ok() && Time.realtimeSinceStartup - r0 < realSecs) yield return null; yield return L59Frames(3); }

    // 2 synthetic pads join + LT = play in a 2-way split; P1 / P2 = James / the second froggy (mech owners 0 / 1)
    IEnumerator F72Join(Gamepad[] pads, int n, string name)
    {
        yield return L59Settle(2f);
        for (int k = 0; k < n; k++)
        {
            pads[k] = InputSystem.AddDevice<Gamepad>(name);
            yield return L59Settle(1f);
            yield return L59Press(pads[k], GamepadButton.South);
            yield return L59Settle(0.6f);
        }
        for (int k = 0; k < n && k < slots.Count; k++) SetSeatChar(slots[k].frog, k, false);
        demoKeepChars = true;
        L59Trigger(pads[0], 1f); yield return L59Frames(3); L59Trigger(pads[0], 0f);
        yield return L59Settle(4f);
        if (shared) { ToggleView(); yield return L59Settle(1f); }
    }

    string Ships72()
    {
        var sw = SpaceWorld.I; if (sw == null) return "no space";
        var sb = new System.Text.StringBuilder("star " + SpaceWorld.StarAt + " pad " + SpaceWorld.PadShown + " ships " + sw.ships.Count + ":");
        foreach (var s in sw.ships)
            sb.Append(" [" + s.gameObject.name + " " + (s.mechForm != null ? s.mechForm.Title : "Starship") + " drv " + (s.driver != null ? s.driver.nick : "-") + " riders " + SpaceWorld.Riders(s)
                + (s.parkedFor != null ? " waits for " + s.parkedFor.nick : "") + " shown " + s.Shown72 + " at " + s.transform.position.ToString("0") + "]");
        return sb.ToString();
    }

    void World72TestLobby() { if (w72On) return; w72On = true; StartCoroutine(W72()); }

    IEnumerator W72()
    {
        const string T = "world72";
        Debug.Log("FFU72 start world72 (item 6 world-state) old=" + F72.Old);
        var pads = new Gamepad[2];
        yield return F72Join(pads, 2, "FFTestPad72");
        F72Check("2 pads + LT: a 2-way split", state == State.Play && slots.Count == 2 && LastViews == 2, "views " + LastViews + " | " + SeatList());
        var sw = SpaceWorld.I;
        if (slots.Count < 2 || sw == null) { F72Result(T); yield break; }
        Frog fa = frogs[slots[0].frog], fb = frogs[slots[1].frog];
        sw.EnsureBuilt();
        yield return L59Settle(1f);
        F72Check("start: the Starship is on the ranch pad, nothing drawn in Earth orbit", SpaceWorld.PadShown && (F72.Old || (SpaceWorld.StarAt == StarLoc.Pad && sw.ShipsShown == 0)), Ships72());
        // P1 boards at the pad (launch sequence, A skips the climb); P2 waits by the pad
        Vector2 pc = Layout.PadC;
        fa.SendTo(WorldId.Ranch, new Vector3(pc.x + 10f, Ranch.GY(pc.x + 10f, pc.y) + 0.5f, pc.y), -90f);
        Vector3 pb = new Vector3(pc.x + 46f, 0f, pc.y + 6f); pb.y = Ranch.GY(pb.x, pb.z) + 0.5f;
        fb.SendTo(WorldId.Ranch, pb, -90f);
        yield return L59Settle(1f);
        LaunchSeq.Begin(fa);
        yield return L59Settle(1.5f);
        LaunchSeq.Skip(fa);
        yield return F72Until(() => fa.world == WorldId.Space && !LaunchSeq.Busy, 90f);
        yield return L59Settle(1.5f);
        Starship sa = fa.vehicle as Starship;
        if (slots[1].rig != null) { slots[1].rig.SetYaw(-82f); slots[1].rig.SetPitch(8f); }
        F72Check("P1 launched: flies the Starship in space", sa != null && sa.mechForm == null && fa.world == WorldId.Space, Ships72());
        F72Check("the ranch pad is EMPTY while the Starship is in space", F72.Old ? false : !SpaceWorld.PadShown && SpaceWorld.StarAt == StarLoc.Space, Ships72());
        F72Check("exactly ONE Starship anywhere (pad + space)", (SpaceWorld.PadShown ? 1 : 0) + (F72.Old ? 1 : sw.StarshipsShown) == 1, Ships72());
        fb.SendTo(WorldId.Ranch, new Vector3(pc.x + 10f, Ranch.GY(pc.x + 10f, pc.y) + 0.5f, pc.y + 3f), -90f);
        yield return L59Frames(3);
        if (F72.Old) F72Info("P2 at the pad (old)", "the pad still shows a Starship while P1 flies it in space - not boarded in the before run");
        else
        {
            LaunchSeq.Begin(fb);
            yield return L59Settle(1f);
            F72Check("P2 at the empty pad can't board a Starship that isn't there", fb.world == WorldId.Ranch && !fb.launching && (fb.toast ?? "").Contains("Starship is"), "P2 world " + fb.world + " launching " + fb.launching + " toast '" + fb.toast + "'");
        }
        fb.SendTo(WorldId.Ranch, pb, -90f);
        if (slots[1].rig != null) { slots[1].rig.SetYaw(-82f); slots[1].rig.SetPitch(8f); }
        F72Ph(T, 1, "padempty");
        yield return L59Settle(12f);
        // P1 lands at the ranch (landing sequence, skipped)
        sa = sw.ShipOf(fa);
        int earth = sw.Find("earth"), moon = sw.Find("moon");
        sa.EnterOrbit(earth, sw.bodies[earth].cap, 0f);
        sw.Land(sa, earth);
        yield return L59Settle(1f);
        LaunchSeq.Skip(fa);
        yield return F72Until(() => fa.world == WorldId.Ranch && !LaunchSeq.Busy && !fa.launching, 60f);
        yield return L59Settle(2f);
        F72Check("P1 landed: the Starship is back on the pad, nothing left in Earth orbit", fa.world == WorldId.Ranch && SpaceWorld.PadShown && (F72.Old || (SpaceWorld.StarAt == StarLoc.Pad && sw.ShipsShown == 0)), Ships72());
        F72Ph(T, 2, "padback");
        yield return L59Settle(10f);
        // P1's mech (James, 10-story) flies to space
        StoryMech ma = FindMech(fa.charId, 0);
        F72Check("P1's own 10-story mech found", ma != null, ma != null ? ma.Title : "none (char " + fa.charId + ")");
        if (ma == null) { F72Result(T); yield break; }
        Vector3 spot = ma.transform.position;
        fa.SendTo(WorldId.Ranch, spot + ma.transform.right * 5f, 0f); yield return L59Frames(2);
        fa.EnterVehicle(ma);
        yield return L59Settle(1f);
        ma.GoToSpace();
        yield return F72Until(() => fa.world == WorldId.Space, 30f);
        yield return L59Settle(2f);
        Starship sm = sw.ShipOf(fa);
        Vector3 look = spot + new Vector3(0f, 0f, -38f);
        fb.SendTo(WorldId.Ranch, new Vector3(look.x, Ranch.GY(look.x, look.z) + 0.5f, look.z), 0f);
        if (slots[1].rig != null) { slots[1].rig.SetYaw(0f); slots[1].rig.SetPitch(6f); }
        F72Check("P1 flies the mech in space (space copy)", fa.vehicle == sm && sm != null && sm.mechForm == ma, Ships72());
        F72Check("the ranch mech is HIDDEN while it is in space (stowed, inactive, can't be boarded)", !F72.Old && ma.Away72 && !ma.gameObject.activeInHierarchy && ma.stowed && !ma.CanEnter(fa), "away " + ma.Away72 + " active " + ma.gameObject.activeInHierarchy + " stowed " + ma.stowed);
        F72Check("the Starship stays on the pad (no Starship drawn in space)", SpaceWorld.PadShown && (F72.Old ? false : sw.StarshipsShown == 0), Ships72());
        F72Ph(T, 3, "mechaway");
        yield return L59Settle(12f);
        // the mech drops P1 on the Moon and waits in orbit; P1 beams back up to it
        sm.EnterOrbit(moon, sw.bodies[moon].cap, 0f);
        yield return L59Frames(3);
        sw.Land(sm, moon);
        yield return L59Settle(3f);
        F72Check("mech lands P1 on the Moon and WAITS in Moon orbit (still not on the ranch)", fa.world == WorldId.Moon && !F72.Old && sm.parkedFor == fa && sm.mechForm == ma && ma.Away72, Ships72() + " | P1 world " + fa.world);
        F72Check("the Starship is still on the ranch pad (it did not fly to the Moon)", SpaceWorld.PadShown && SpaceWorld.StarAt == StarLoc.Pad, Ships72());
        sw.ToOrbit(fa, "moon");
        yield return L59Settle(2f);
        F72Check("board at the Moon pad: P1 beams back up into the waiting mech", fa.vehicle == sm && sm.mechForm == ma && sm.parkedFor == null, Ships72() + " | P1 veh " + (fa.vehicle != null ? fa.vehicle.Title : "-"));
        // home
        sm.EnterOrbit(earth, sw.bodies[earth].cap, 0f);
        yield return L59Frames(3);
        sw.Land(sm, earth);
        yield return L59Settle(3f);
        F72Check("mech lands at the ranch: visible again with P1 in it, no space ship left in use", fa.world == WorldId.Ranch && fa.vehicle == ma && !ma.Away72 && ma.gameObject.activeInHierarchy && !ma.stowed && sw.ShipsInUse == 0, Ships72() + " | P1 veh " + (fa.vehicle != null ? fa.vehicle.Title : "-"));
        Vector3 mp = ma.transform.position;
        fb.SendTo(WorldId.Ranch, new Vector3(mp.x + 26f, Ranch.GY(mp.x + 26f, mp.z) + 0.5f, mp.z), -90f);
        if (slots[1].rig != null) { slots[1].rig.SetYaw(-90f); slots[1].rig.SetPitch(8f); }
        F72Ph(T, 4, "mechhome");
        yield return L59Settle(10f);
        F72Result(T);
    }
}
