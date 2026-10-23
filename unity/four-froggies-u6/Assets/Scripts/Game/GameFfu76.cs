using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// ffu76 probe ?ffdemo=1&ffshot=ffu76 (&f76n=1 = one view, &ff76old=1 = before). Logs "FFU76 PASS|FAIL ...", "FFU76 RESULT n/m",
// "FFDEMO ffu76 ph N <phase>".
//  1 RANCH DEFAULT (B9): P1 lands James's trillion-story mech at the ranch without touching the stick (2 views: P2's 1000-story
//    mech lands at the same moment). The spot: footprint, whole ramp line, foot and walk-off fan dry (the pond / swim test) and
//    clear; the two mechs apart. After the walk down, P1 walks on 3 s: never swimming.
//  2 STEERING: P1 lands again and pushes the stick toward the pond: the touchdown point moves, the marker turns red and the HUD
//    says so; stick released: it nudges itself to dry ground and touches down there; P1 walks off dry.
//  3 MOON / MARS still land: Moon (touchdown, ramp, walk down, parked), A at the ramp foot = liftoff, then Mars (2 views: P2's
//    mech lands on Mars too - two parked mechs apart).
public partial class Game
{
    int f76Pass, f76Total; bool f76On;
    void F76Check(string what, bool ok, string d) { f76Total++; if (ok) f76Pass++; Debug.Log("FFU76 " + (ok ? "PASS" : "FAIL") + " " + what + " | " + d); }
    void F76Info(string what, string d) { Debug.Log("FFU76 INFO " + what + " | " + d); }
    void F76Ph(int n, string name) { Debug.Log("FFDEMO ffu76 ph " + n + " " + name + " t " + Time.time.ToString("0.0")); }
    void F76Result(string tag) { Debug.Log("FFU76 RESULT " + f76Pass + "/" + f76Total + " (" + tag + ")"); Debug.Log("FFDEMO " + tag + " done"); }
    void Fix76TestLobby() { if (f76On) return; f76On = true; StartCoroutine(P76()); }

    static float D76(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return (a - b).magnitude; }
    // the whole ramp line (hinge -> foot, every metre), the foot and the mech's centre: none of it in water
    static string WetAlong76(MechLand75 ml)
    {
        Vector3 hx = ml.C + ml.fwd * ml.hingeL.z, f = ml.F;
        int n = Mathf.Max(2, Mathf.CeilToInt(D76(hx, f)));
        int wet = 0;
        for (int i = 0; i <= n; i++) if (MechLand75.Wet(ml.world, Vector3.Lerp(hx, f, i / (float)n))) wet++;
        bool fw = MechLand75.Wet(ml.world, f), cw = MechLand75.Wet(ml.world, ml.C);
        return wet == 0 && !fw && !cw && f.y > Layout.WaterY + 0.3f ? "" : "wet ramp samples " + wet + "/" + (n + 1) + " foot wet " + fw + " centre wet " + cw + " foot y " + f.y.ToString("0.0");
    }
    string Spot76(MechLand75 ml)
    {
        string why; bool ok = ml.SpotOk(ml.C, ml.yawDeg, out why);
        return (ok ? "ok" : "NOT OK: " + why) + " C " + ml.C.ToString("0") + " yaw " + ml.yawDeg.ToString("0") + " foot " + ml.F.ToString("0") + " old " + ml.C0.ToString("0") + " reach " + ml.reach76.ToString("0");
    }
    MechLand75 Wait76(Frog f) { return MechLand75.Of(f); }
    IEnumerator Until76(System.Func<bool> ok, float real) { float r0 = Time.realtimeSinceStartup; while (!ok() && Time.realtimeSinceStartup - r0 < real) yield return null; }

    // P1 (or P2) climbs into its mech and flies it to orbit; returns the ship via ship76
    Starship ship76;
    IEnumerator ToSpace76(Frog f, StoryMech m)
    {
        ship76 = null;
        var sw = SpaceWorld.I;
        if (f.world != WorldId.Ranch || D76(f.transform.position, m.transform.position) > m.height * 0.6f + 8f) { f.SendTo(WorldId.Ranch, m.transform.position + m.transform.right * (m.height * 0.3f + 4f), 0f); yield return L59Frames(3); }
        if (f.vehicle != m) f.EnterVehicle(m);
        yield return L59Settle(1f);
        m.GoToSpace();
        yield return F72Until(() => f.world == WorldId.Space, 40f);
        yield return L59Settle(1f);
        ship76 = sw.ShipOf(f);
    }
    // walk the froggy on for a few seconds (stick forward along the ramp's direction) and report swimming
    IEnumerator WalkOn76(Gamepad pad, int slot, Frog f, Vector3 dir, float secs)
    {
        if (slot < slots.Count && slots[slot].rig != null) slots[slot].rig.SetYaw(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg);
        yield return L59Frames(2);
        Stick75(pad, new Vector2(0f, 1f));
        float r0 = Time.realtimeSinceStartup; swam76 = false;
        while (Time.realtimeSinceStartup - r0 < secs) { if (f.Swimming || MechLand75.Wet(f.world, f.transform.position)) swam76 = true; yield return null; }
        Stick75(pad, Vector2.zero);
        yield return L59Frames(3);
        if (f.Swimming) swam76 = true;
    }
    bool swam76;

    IEnumerator Down76(MechLand75 ml, float real)
    {
        yield return Until76(() => ml == null || ml.phase == MechLand75.Ph.Parked || ml.phase == MechLand75.Ph.Done || !MechLand75.All.Contains(ml), real);
        yield return L59Frames(3);
    }

    IEnumerator P76()
    {
        const string T = "ffu76";
        int n = Ffu33.Url("f76n") == "1" ? 1 : 2;
        Debug.Log("FFU76 start (dry landing spot + pilot steering) old=" + F76.Old + " views " + n);
        var pads = new Gamepad[n];
        yield return F72Join(pads, n, "FFTestPad76");
        F76Check(n + " pad(s) + LT: play, " + n + " view(s)", state == State.Play && slots.Count == n && LastViews == n, "views " + LastViews + " | " + SeatList());
        if (slots.Count < n) { F76Result(T); yield break; }
        var sw = SpaceWorld.I;
        Frog fa = frogs[slots[0].frog];
        Frog fb = n > 1 ? frogs[slots[1].frog] : null;
        StoryMech big = FindMech(fa.charId, 3);
        StoryMech mid = fb != null ? FindMech(fb.charId, 2) : null;
        F76Check("James's trillion-story mech found" + (n > 1 ? " + P2's 1000-story mech" : ""), big != null && sw != null && (n == 1 || mid != null), (big != null ? big.Title : "-") + " / " + (mid != null ? mid.Title : "-"));
        if (big == null || sw == null) { F76Result(T); yield break; }
        float dtWas = StoryMech.DtCap; StoryMech.DtCap = 0.3f;

        // ---------------- 1 ranch default (both at once in split) ----------------
        yield return ToSpace76(fa, big);
        Starship sa = ship76;
        Starship sb = null;
        if (mid != null) { yield return ToSpace76(fb, mid); sb = ship76; }
        int earth = sw.Find("earth"), moon = sw.Find("moon"), mars = sw.Find("mars");
        F76Check("in space: P1 flies the trillion-story mech" + (sb != null ? ", P2 its 1000-story mech" : ""), sa != null && sa.mechForm == big && earth >= 0 && (mid == null || (sb != null && sb.mechForm == mid)), Ships72());
        if (sa == null || earth < 0) { StoryMech.DtCap = dtWas; F76Result(T); yield break; }
        sa.EnterOrbit(earth, sw.bodies[earth].cap, 0f);
        if (sb != null) sb.EnterOrbit(earth, sw.bodies[earth].cap, 1f);
        yield return L59Frames(3);
        sw.Land(sa, earth);
        if (sb != null) sw.Land(sb, earth);
        yield return Until76(() => Wait76(fa) != null && (fb == null || sb == null || Wait76(fb) != null), 20f);
        yield return L59Frames(3);
        MechLand75 ma = Wait76(fa), mb = fb != null ? Wait76(fb) : null;
        if (F75.Old) { F76Check("(&ff75old) old Starship landing", fa.world == WorldId.Ranch, "world " + fa.world); StoryMech.DtCap = dtWas; F76Result(T); yield break; }
        F76Check("both mechs land themselves at the ranch", ma != null && (fb == null || mb != null), (ma != null ? ma.name : "none") + " / " + (mb != null ? mb.name : "-"));
        if (ma == null) { StoryMech.DtCap = dtWas; F76Result(T); yield break; }
        F76Info("P1 spot", Spot76(ma)); if (mb != null) F76Info("P2 spot", Spot76(mb));
        foreach (var s in MechLand75.Log76) F76Info("log", s);
        yield return Until76(() => ma.alt < ma.h0 * 0.6f || ma.phase != MechLand75.Ph.Descend, 60f);
        string pr = fa.prompt ?? "";
        if (F76.Old)
        {
            F76Info("old spot (ff76old)", "C " + ma.C.ToString("0") + " foot " + ma.F.ToString("0") + " " + WetAlong76(ma));
            F76Ph(1, "edesc");
            yield return Until76(() => ma.phase == MechLand75.Ph.WalkDown, 120f);
            F76Ph(2, "eramp");
            yield return Down76(ma, 150f);
            yield return L59Settle(2f);
            F76Check("(old) P1 after the walk down", true, "swimming " + fa.Swimming + " at " + fa.transform.position.ToString("0"));
            F76Ph(3, "ewalk");
            yield return L59Settle(8f);
            StoryMech.DtCap = dtWas; F76Result(T); yield break;
        }
        F76Check("P1's ranch spot: mech, whole ramp line and foot dry + clear (no pond)", WetAlong76(ma) == "" && ma.SpotOk(ma.C, ma.yawDeg, out pr), WetAlong76(ma) + " | " + Spot76(ma));
        pr = fa.prompt ?? "";
        F76Check("descending: green marker ring under P1's mech, HUD steering hint without raw tags", ma.MarkerOn76 && !ma.MarkerRed76 && pr.Contains("steer") && !pr.Contains("<"), "marker " + ma.MarkerOn76 + " red " + ma.MarkerRed76 + " prompt '" + pr + "'");
        if (mb != null)
        {
            F76Check("P2's 1000-story spot dry + clear too", WetAlong76(mb) == "" && mb.SpotOk(mb.C, mb.yawDeg, out pr), WetAlong76(mb) + " | " + Spot76(mb));
            F76Check("two mechs landing at once stand apart (footprints + ramps clear)", D76(ma.C, mb.C) > ma.FootR + mb.FootR + 6f && D76(ma.F, mb.F) > 9f, "dist " + D76(ma.C, mb.C).ToString("0") + " feet " + D76(ma.F, mb.F).ToString("0"));
        }
        F76Ph(1, "edesc");
        yield return Until76(() => ma.phase == MechLand75.Ph.WalkDown || ma.phase == MechLand75.Ph.Done, 150f);
        F76Check("touchdown + ramp down on the default spot", ma.touchdowns == 1 && ma.rampLen > 2f && WetAlong76(ma) == "", "touchdowns " + ma.touchdowns + " ramp " + ma.rampLen.ToString("0") + " | " + Spot76(ma));
        F76Ph(2, "eramp");
        Vector3 eC = ma.C, eF = ma.F, eFwd = ma.fwd;
        yield return Down76(ma, 180f);
        if (mb != null) yield return Down76(mb, 60f);
        yield return L59Settle(1.5f);
        F76Check("P1 walked down onto dry ground at the ranch (not swimming)", fa.world == WorldId.Ranch && fa.land75 == null && !fa.Swimming && !MechLand75.Wet(WorldId.Ranch, fa.transform.position) && D76(fa.transform.position, eF) < 14f,
            "at " + fa.transform.position.ToString("0.0") + " foot " + eF.ToString("0") + " swimming " + fa.Swimming);
        if (fb != null) F76Check("P2 walked down dry too", fb.land75 == null && !fb.Swimming && !MechLand75.Wet(WorldId.Ranch, fb.transform.position), "at " + fb.transform.position.ToString("0.0") + " swimming " + fb.Swimming);
        if (slots[0].rig != null) { Vector3 tm = eC + Vector3.up * big.height * 0.4f - fa.transform.position; tm.y = 0f; slots[0].rig.SetYaw(Mathf.Atan2(tm.x, tm.z) * Mathf.Rad2Deg); slots[0].rig.SetPitch(-12f); }
        yield return L59Settle(2f);
        F76Ph(3, "ewalk");
        yield return L59Settle(4f);
        yield return WalkOn76(pads[0], 0, fa, eFwd, 3f);
        F76Check("walking on from the ramp foot P1 never swims", !swam76 && fa.world == WorldId.Ranch, "at " + fa.transform.position.ToString("0.0") + " swam " + swam76);
        F76Check("the real trillion-story mech stands where it landed", !big.Away72 && D76(big.transform.position, eC) < 3f, "mech " + big.transform.position.ToString("0") + " landed " + eC.ToString("0"));

        // ---------------- 2 steering toward the pond: red, refused, nudged to dry ground ----------------
        yield return ToSpace76(fa, big);
        sa = ship76;
        if (sa == null) { F76Check("back to space for the steering test", false, Ships72()); StoryMech.DtCap = dtWas; F76Result(T); yield break; }
        sa.EnterOrbit(earth, sw.bodies[earth].cap, 0f);
        yield return L59Frames(3);
        sw.Land(sa, earth);
        yield return Until76(() => Wait76(fa) != null, 20f);
        ma = Wait76(fa);
        if (ma == null) { F76Check("second ranch landing started", false, Ships72()); StoryMech.DtCap = dtWas; F76Result(T); yield break; }
        yield return L59Frames(4);
        Vector3 c0 = ma.C;
        Vector3 pondW = new Vector3(Layout.PondC.x, 0f, Layout.PondC.y);
        float r0 = Time.realtimeSinceStartup; float maxMove = 0f; bool sawRed = false; string redPrompt = "";
        float redSince = -1f;
        while (ma != null && ma.phase == MechLand75.Ph.Descend && Time.realtimeSinceStartup - r0 < 60f)
        {
            Vector3 cf = ma.CamFwd76; cf.y = 0f; cf.Normalize(); Vector3 cr = new Vector3(cf.z, 0f, -cf.x);
            Vector3 d = pondW - ma.C; d.y = 0f; d.Normalize();
            Stick75(pads[0], new Vector2(Vector3.Dot(d, cr), Vector3.Dot(d, cf)).normalized);
            maxMove = Mathf.Max(maxMove, D76(ma.C, c0));
            if (ma.MarkerRed76 && !sawRed) { sawRed = true; redSince = Time.realtimeSinceStartup; F76Ph(4, "sred"); }
            if (sawRed && redPrompt == "") redPrompt = fa.prompt ?? "";
            if (sawRed && Time.realtimeSinceStartup - redSince > 3f && MechLand75.Wet(WorldId.Ranch, ma.C)) break;
            yield return null;
        }
        Stick75(pads[0], Vector2.zero);
        F76Check("steering: the stick moves the touchdown point (toward the pond)", maxMove > 15f && ma != null && ma.steerDist > 15f, "moved " + maxMove.ToString("0") + " m, steered " + (ma != null ? ma.steerDist.ToString("0") : "-") + " m");
        F76Check("over the water the marker turns red and the HUD says so (no raw tags)", sawRed && (redPrompt.Contains("water") && !redPrompt.Contains("<")), "red " + sawRed + " prompt '" + redPrompt + "' C " + (ma != null ? ma.C.ToString("0") : "-"));
        if (ma == null) { StoryMech.DtCap = dtWas; F76Result(T); yield break; }
        yield return Until76(() => ma.phase != MechLand75.Ph.Descend, 150f);
        F76Ph(5, "sland");
        F76Check("stick released: touchdown refused on water, nudged to dry ground and landed there", ma.touchdowns == 1 && ma.nudges >= 1 && WetAlong76(ma) == "" && ma.SpotOk(ma.C, ma.yawDeg, out pr), "nudges " + ma.nudges + " " + WetAlong76(ma) + " | " + Spot76(ma));
        Vector3 sF = ma.F, sFwd = ma.fwd;
        yield return Down76(ma, 180f);
        yield return L59Settle(1.5f);
        F76Ph(6, "swalk");
        yield return WalkOn76(pads[0], 0, fa, sFwd, 3f);
        F76Check("after the steered landing P1 walks off dry (never swimming)", !swam76 && fa.land75 == null && fa.world == WorldId.Ranch && D76(fa.transform.position, sF) < 20f, "at " + fa.transform.position.ToString("0.0") + " foot " + sF.ToString("0") + " swam " + swam76);

        // ---------------- 3 Moon, then Mars ----------------
        yield return ToSpace76(fa, big);
        sa = ship76;
        if (sa == null || moon < 0) { F76Check("to space for the Moon", false, Ships72()); StoryMech.DtCap = dtWas; F76Result(T); yield break; }
        sa.EnterOrbit(moon, sw.bodies[moon].cap, 0f);
        yield return L59Frames(3);
        sw.Land(sa, moon);
        yield return Until76(() => Wait76(fa) != null, 20f);
        MechLand75 mm = Wait76(fa);
        F76Check("Moon: the mech lands itself", mm != null && mm.world == WorldId.Moon, mm != null ? Spot76(mm) : Ships72());
        if (mm == null) { StoryMech.DtCap = dtWas; F76Result(T); yield break; }
        F76Info("moon spot", Spot76(mm));
        yield return Until76(() => mm.phase == MechLand75.Ph.WalkDown, 150f);
        F76Ph(7, "mramp");
        yield return Down76(mm, 180f);
        yield return L59Settle(1f);
        float gy = MechLand75.Ground(WorldId.Moon, fa.transform.position.x, fa.transform.position.z);
        F76Check("Moon: touchdown, ramp, P1 walked down beside it, mech parked, spot clear", mm.touchdowns == 1 && MechLand75.All.Contains(mm) && mm.phase == MechLand75.Ph.Parked && fa.land75 == null && fa.world == WorldId.Moon && Mathf.Abs(fa.transform.position.y - gy) < 2f && D76(fa.transform.position, mm.F) < 14f && mm.SpotOk(mm.C, mm.yawDeg, out pr),
            "phase " + mm.phase + " P1 " + fa.transform.position.ToString("0") + " | " + Spot76(mm));
        // A at the ramp foot: back to orbit
        Vector3 foot = mm.F + mm.fwd * 1.5f; foot.y = MechLand75.Ground(mm.world, foot.x, foot.z) + 0.3f;
        fa.Teleport(foot);
        yield return L59Settle(1.5f);
        yield return L59Press(pads[0], GamepadButton.South);
        yield return L59Frames(4);
        if (mm.phase == MechLand75.Ph.Parked) mm.Board(fa);
        yield return F72Until(() => fa.world == WorldId.Space, 150f);
        F76Check("Moon: A at the ramp foot = liftoff back to orbit", fa.world == WorldId.Space && fa.vehicle == sa, Ships72());
        if (fa.world != WorldId.Space || mars < 0) { StoryMech.DtCap = dtWas; F76Result(T); yield break; }
        sa.EnterOrbit(mars, sw.bodies[mars].cap, 0f);
        if (mid != null)
        {
            // P2's mech joins on Mars: two parked mechs at once off Earth
            yield return ToSpace76(fb, mid); sb = ship76;
            if (sb != null) sb.EnterOrbit(mars, sw.bodies[mars].cap, 1f);
        }
        yield return L59Frames(3);
        sw.Land(sa, mars);
        if (sb != null && fb != null && fb.world == WorldId.Space) sw.Land(sb, mars);
        yield return Until76(() => Wait76(fa) != null && (sb == null || Wait76(fb) != null), 20f);
        MechLand75 mr = Wait76(fa), mr2 = fb != null ? Wait76(fb) : null;
        F76Check("Mars: the trillion-story mech lands itself" + (mr2 != null ? " (P2's 1000-story too)" : ""), mr != null && mr.world == WorldId.Mars && (mid == null || mr2 != null), (mr != null ? Spot76(mr) : Ships72()) + (mr2 != null ? " | P2 " + Spot76(mr2) : ""));
        if (mr == null) { StoryMech.DtCap = dtWas; F76Result(T); yield break; }
        yield return Until76(() => mr.phase == MechLand75.Ph.WalkDown, 150f);
        F76Ph(8, "marsramp");
        yield return Down76(mr, 180f);
        if (mr2 != null) yield return Down76(mr2, 90f);
        yield return L59Settle(1f);
        F76Check("Mars: touchdown, ramp, P1 walked down, parked inside the world, spot clear", mr.touchdowns == 1 && mr.phase == MechLand75.Ph.Parked && fa.land75 == null && fa.world == WorldId.Mars && mr.SpotOk(mr.C, mr.yawDeg, out pr), "phase " + mr.phase + " | " + Spot76(mr));
        if (mr2 != null) F76Check("Mars: two parked mechs apart, P2 walked down", mr2.phase == MechLand75.Ph.Parked && fb.land75 == null && D76(mr.C, mr2.C) > mr.FootR + mr2.FootR + 6f, "dist " + D76(mr.C, mr2.C).ToString("0") + " P2 phase " + mr2.phase);
        Vector3 sa2 = mr.F + mr.fwd * 6f + mr.right * 6f; sa2.y = MechLand75.Ground(mr.world, sa2.x, sa2.z) + 0.3f;
        fa.Teleport(sa2);
        if (slots[0].rig != null) { Vector3 tm = mr.C + Vector3.up * mr.H * 0.4f - sa2; tm.y = 0f; slots[0].rig.SetYaw(Mathf.Atan2(tm.x, tm.z) * Mathf.Rad2Deg); slots[0].rig.SetPitch(-12f); }
        yield return L59Settle(2f);
        F76Ph(9, "marssurf");
        yield return L59Settle(6f);
        foreach (var s in MechLand75.Log76) F76Info("log", s);
        StoryMech.DtCap = dtWas;
        F76Result(T);
    }
}
