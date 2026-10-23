using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// ffu75 probe ?ffdemo=1&ffshot=ffu75 (&f75n=1 = one view, &ff75old=1 = before). Logs "FFU75 PASS|FAIL ...", "FFU75 RESULT n/m",
// "FFDEMO ffu75 ph N <phase>". Parts:
//  B7  P1 in James's trillion-story mech, BRIDGE VIEW: standing still, then walking (pad stick held): the camera roll / frame-to-frame
//      turn and the near plane inside the bridge; four shots standing still + three walking (diffed outside).
//  MECH LANDS ITSELF: an AI froggy aboard the bridge, P1 flies the mech to space, Moon orbit, pad Y = land: the mech copy drops in on
//      its flames (crew held inside, P1's camera on it), touchdown, ramp down from the bridge, everyone walks down, the mech stays
//      parked (no Starship prop on the Moon, nothing drawn in orbit), A at the ramp foot = walk up + liftoff back to Moon orbit.
//      Then Earth: the same landing at the ranch, and the real ranch mech stands where it touched down.
//      Split (2 views): P2 flies its own 100-story mech to Mars (chest ramp). One view: P1's 10-story to Callisto.
public partial class Game
{
    int f75Pass, f75Total; bool f75On;
    void F75Check(string what, bool ok, string d) { f75Total++; if (ok) f75Pass++; Debug.Log("FFU75 " + (ok ? "PASS" : "FAIL") + " " + what + " | " + d); }
    void F75Info(string what, string d) { Debug.Log("FFU75 INFO " + what + " | " + d); }
    void F75Ph(int n, string name) { Debug.Log("FFDEMO ffu75 ph " + n + " " + name + " t " + Time.time.ToString("0.0")); }
    void Fix75TestLobby() { if (f75On) return; f75On = true; StartCoroutine(P75()); }

    // camera steadiness over n rendered frames: max |roll|, mean / max turn between frames (degrees)
    float roll75, turnMean75, turnMax75;
    IEnumerator Steady75(Camera c, int n)
    {
        roll75 = 0f; turnMean75 = 0f; turnMax75 = 0f;
        Quaternion last = Quaternion.identity; bool have = false; float sum = 0f; int m = 0;
        for (int k = 0; k < n; k++)
        {
            yield return new WaitForEndOfFrame();
            if (c == null) yield break;
            Quaternion q = c.transform.rotation;
            float roll = Mathf.Abs(Mathf.Asin(Mathf.Clamp((q * Vector3.right).y, -1f, 1f)) * Mathf.Rad2Deg);
            roll75 = Mathf.Max(roll75, roll);
            if (have) { float a = Quaternion.Angle(last, q); sum += a; m++; turnMax75 = Mathf.Max(turnMax75, a); }
            last = q; have = true;
        }
        turnMean75 = m > 0 ? sum / m : 0f;
    }
    static void Stick75(Gamepad p, Vector2 v) { if (p == null) return; var st = new GamepadState(); st.leftStick = v; InputSystem.QueueStateEvent(p, st); }

    float BodyH75(Transform t, out float bottom)
    {
        float lo = 1e9f, hi = -1e9f;
        foreach (var r in t.GetComponentsInChildren<MeshRenderer>())
        {
            if (r.gameObject.name.Contains("Flame") || r.transform.parent != null && (r.transform.parent.name.Contains("Flame") || r.transform.parent.name == "Run" || r.transform.parent.name == "Chevron")) continue;
            if (r.GetComponentInParent<Bridge73>() != null) continue;
            lo = Mathf.Min(lo, r.bounds.min.y); hi = Mathf.Max(hi, r.bounds.max.y);
        }
        bottom = lo; return hi - lo;
    }
    int FlamesOn75(MechLand75 ml) { int n = 0; foreach (Transform t in ml.bodyT) if (t.name.EndsWith("Flame75") && t.gameObject.activeSelf) n++; return n; }
    bool InView75(Camera c, Vector3 p) { if (c == null) return false; Vector3 v = c.WorldToViewportPoint(p); return v.z > 0f && v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f; }
    IEnumerator Phase75(MechLand75 ml, System.Func<bool> ok, float real) { float r0 = Time.realtimeSinceStartup; while (ml != null && !ok() && Time.realtimeSinceStartup - r0 < real) yield return null; }

    // one full mech landing on a world (new path) with shots; returns via landed75
    MechLand75 landed75; Vector3 lastC75; bool got75;
    IEnumerator Land75Run(Frog f, Starship s, StoryMech m, int bodyIdx, int slot, string tag, bool full)
    {
        var sw = SpaceWorld.I;
        landed75 = null;
        float r0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - r0 < 20f && (landed75 = MechLand75.Of(f)) == null) yield return null;
        MechLand75 ml = landed75;
        got75 = ml != null; if (got75) lastC75 = ml.C;
        yield return L59Frames(3);   // Watch72 has run: prop / orbit copy state settled
        string bn = sw.bodies[bodyIdx].name;
        if (F75.Old)
        {
            F75Check(tag + ": (old build) crew dropped straight onto " + bn, f.world != WorldId.Space, "world " + f.world + " | " + Ships72());
            yield break;
        }
        F75Check(tag + ": the mech itself lands on " + bn + " (MechLand75 started, " + m.Title + ")", ml != null && ml.mech == m, ml != null ? ml.name : "none | " + Ships72());
        if (ml == null) yield break;
        Camera cam = slot < slots.Count ? slots[slot].cam : null;
        float bottom; float bh = BodyH75(ml.bodyT, out bottom);
        F75Check(tag + ": crew on " + bn + " but held inside the mech; the mech copy is full size (" + m.height + " m)", f.land75 == ml && !f.model.gameObject.activeSelf && f.world == ml.world && bh > m.height * 0.8f && bh < m.height * 1.3f,
            "held " + (f.land75 == ml) + " model " + f.model.gameObject.activeSelf + " world " + f.world + " copy height " + bh.ToString("0.0") + " m (H " + m.height + ") crew " + ml.crew.Count);
        F75Check(tag + ": no Starship on " + bn + " (prop hidden) and nothing drawn in orbit", (ml.world == WorldId.Ranch || !MechLand75.PropActive(ml.world)) && !s.Shown72, "prop " + MechLand75.PropActive(ml.world) + " star " + SpaceWorld.StarAt + " ship shown " + s.Shown72);
        yield return Phase75(ml, () => ml.phase != MechLand75.Ph.Descend || ml.alt < ml.h0 * 0.45f, 60f);
        if (ml == null) yield break;
        F75Check(tag + ": descending on its flames, P1's camera on it", ml.phase == MechLand75.Ph.Descend && FlamesOn75(ml) >= 2 && InView75(cam, ml.C + Vector3.up * (ml.alt + ml.H * 0.5f)),
            "phase " + ml.phase + " alt " + ml.alt.ToString("0") + "/" + ml.h0.ToString("0") + " flames " + FlamesOn75(ml) + " in view " + InView75(cam, ml.C + Vector3.up * (ml.alt + ml.H * 0.5f)));
        if (full) F75Ph(1, tag + "desc");
        yield return Phase75(ml, () => ml.phase != MechLand75.Ph.Descend, 90f);
        if (ml == null) yield break;
        bh = BodyH75(ml.bodyT, out bottom);
        F75Check(tag + ": touchdown - feet on the ground, dust kicked up", ml.touchdowns == 1 && Mathf.Abs(bottom - ml.C.y) < Mathf.Max(1.5f, ml.H * 0.06f) && ml.dustPuffs > 0,
            "touchdowns " + ml.touchdowns + " foot bottom " + bottom.ToString("0.0") + " ground " + ml.C.y.ToString("0.0") + " dust " + ml.dustPuffs);
        if (full) F75Ph(2, tag + "touch");
        yield return Phase75(ml, () => ml.phase == MechLand75.Ph.WalkDown || (ml.phase == MechLand75.Ph.Ramp && ml.rampK > 0.85f), 60f);
        if (ml == null) yield break;
        if (full) F75Ph(3, tag + "ramp");
        Vector3 hl = ml.hingeL;
        F75Check(tag + ": ramp lowers from the " + (ml.bridge != null ? "bridge" : "chest") + " to the ground", ml.rampHinge.gameObject.activeSelf && (ml.bridge != null ? hl.y > ml.H * 0.8f : hl.y > ml.H * 0.4f && hl.y < ml.H * 0.85f) && ml.rampLen > 2f,
            "hinge h " + hl.y.ToString("0.0") + " (" + (hl.y / ml.H).ToString("0.00") + " H) ramp " + ml.rampLen.ToString("0.0") + " m angle " + ml.RampDegNow.ToString("0") + " deg");
        yield return Phase75(ml, () => ml.phase != MechLand75.Ph.Ramp, 30f);
        yield return Phase75(ml, () => { if (ml.phase != MechLand75.Ph.WalkDown) return true; foreach (var w in ml.walkers) if (w.model.gameObject.activeSelf && (w.transform.position - ml.HingeW).magnitude > 1f && (w.transform.position - ml.F).magnitude > 1f) return true; return false; }, 40f);
        if (ml != null && ml.phase == MechLand75.Ph.WalkDown && full) { F75Ph(4, tag + "walk"); yield return L59Settle(3f); }
        yield return Phase75(ml, () => ml.phase != MechLand75.Ph.WalkDown, 90f);
        yield return L59Frames(3);
    }

    IEnumerator P75()
    {
        const string T = "ffu75";
        int n = Ffu33.Url("f75n") == "1" ? 1 : 2;
        Debug.Log("FFU75 start (mech lands itself + ramp; B7 bridge view) old=" + F75.Old + " views " + n);
        var pads = new Gamepad[n];
        yield return F72Join(pads, n, "FFTestPad75");
        F75Check(n + " pad(s) + LT: play, " + n + " view(s)", state == State.Play && slots.Count == n && LastViews == n, "views " + LastViews + " | " + SeatList());
        if (slots.Count < n) { F75Result(T); yield break; }
        var sw = SpaceWorld.I;
        Frog fa = frogs[slots[0].frog];
        Frog fb = n > 1 ? frogs[slots[1].frog] : null;
        StoryMech big = FindMech(fa.charId, 3);
        F75Check("James's trillion-story mech found", big != null && sw != null, big != null ? big.Title + " H " + big.height : "-");
        if (big == null || sw == null) { F75Result(T); yield break; }
        float dtWas = StoryMech.DtCap; StoryMech.DtCap = 0.3f;
        int moon = sw.Find("moon"), earth = sw.Find("earth"), mars = sw.Find("mars"), cal = sw.Find("callisto");

        // ---------------- B7: bridge view steadiness ----------------
        fa.SendTo(WorldId.Ranch, big.transform.position + big.transform.right * (big.height * 0.3f + 4f), 0f);
        yield return L59Frames(2);
        fa.EnterVehicle(big);
        yield return L59Settle(1.5f);
        Bridge73.SetView(slots[0].rig, true);
        yield return L59Settle(2f);
        Camera ca = slots[0].rig != null ? slots[0].rig.cam : null;
        yield return Steady75(ca, 14);
        float stillRoll = roll75, stillTurn = turnMean75;
        float k = big.bridge73 != null ? big.bridge73.k : 1f;
        F75Check("B7: bridge view on, P1's camera inside the bridge", Bridge73.ViewOn(slots[0].rig) && big.bridge73 != null && ca != null && big.bridge73.Inside(ca.transform.position), "inside " + (big.bridge73 != null && ca != null && big.bridge73.Inside(ca.transform.position)));
        F75Check("B7: near plane inside the bridge >= 0.3 m (depth range sane, no z-fight flashing)", Bridge73.InNear75 >= 0.3f * k - 1e-4f, "near " + Bridge73.InNear75.ToString("0.000") + " far " + Bridge73.InFar75.ToString("0") + " ratio " + (Bridge73.InFar75 / Mathf.Max(1e-4f, Bridge73.InNear75)).ToString("0"));
        F75Check("B7: standing still the bridge view is steady (turn per frame < 0.05 deg, roll < 0.5 deg)", stillTurn < 0.05f && stillRoll < 0.5f, "turn mean " + stillTurn.ToString("0.000") + " max " + turnMax75.ToString("0.000") + " roll " + stillRoll.ToString("0.00"));
        for (int s = 1; s <= 4; s++) { F75Ph(1, "b7s" + s); yield return L59Frames(2); }
        yield return L59Settle(9f);
        Stick75(pads[0], new Vector2(0f, 1f));
        yield return L59Settle(3f);
        yield return Steady75(ca, 24);
        float walkRoll = roll75, walkTurn = turnMean75, walkMax = turnMax75;
        F75Check("B7: walking, the bridge view no longer rolls / swings with the stride (roll < 0.6 deg, turn per frame < 0.4 deg)", walkRoll < 0.6f && walkTurn < 0.4f && big.Speed > 0.5f,
            "roll max " + walkRoll.ToString("0.00") + " turn mean " + walkTurn.ToString("0.000") + " max " + walkMax.ToString("0.000") + " speed " + big.Speed.ToString("0.0"));
        for (int s = 1; s <= 3; s++) { F75Ph(2, "b7w" + s); yield return L59Frames(2); }
        yield return L59Settle(8f);
        Stick75(pads[0], Vector2.zero);
        yield return L59Settle(2f);
        Bridge73.SetView(slots[0].rig, false);
        yield return L59Settle(1f);

        // ---------------- the trillion-story mech lands itself on the Moon ----------------
        Frog ai = null;
        foreach (Frog f in frogs) if (f != null && !f.human && f.world == WorldId.Ranch && f.vehicle == null && f.passengerOf == null) { ai = f; break; }
        if (ai != null && big.bridge73 != null) Bridge73.Board(ai, big);
        yield return L59Settle(1f);
        big.GoToSpace();
        yield return F72Until(() => fa.world == WorldId.Space, 40f);
        yield return L59Settle(2f);
        Starship sm = sw.ShipOf(fa);
        // ffu75b: space is built on first use - before GoToSpace these Finds were -1 (bodies[-1] threw, as in ffu41c)
        moon = sw.Find("moon"); earth = sw.Find("earth"); mars = sw.Find("mars"); cal = sw.Find("callisto");
        if (moon < 0 || earth < 0) { F75Check("space bodies found (moon / earth)", false, "moon " + moon + " earth " + earth); StoryMech.DtCap = dtWas; F75Result(T); yield break; }
        F75Check("P1 flies the trillion-story mech in space (AI froggy aboard)", sm != null && fa.vehicle == sm && sm.mechForm == big && (ai == null || ai.passengerOf == sm), Ships72() + " | ai " + (ai != null ? ai.nick + " on " + (ai.passengerOf != null ? ai.passengerOf.Title : "-") : "-"));
        if (sm == null || sm.mechForm != big) { StoryMech.DtCap = dtWas; F75Result(T); yield break; }
        sm.EnterOrbit(moon, sw.bodies[moon].cap, 0f);
        yield return L59Settle(1.5f);
        yield return L59Press(pads[0], GamepadButton.North);   // Y = LAND (the same Land72 funnel as F / touch LAND / auto)
        yield return F72Until(() => fa.world != WorldId.Space, 10f);
        bool viaY = fa.world != WorldId.Space;
        F75Check("pad Y lands from Moon orbit", viaY, "P1 world " + fa.world);
        if (!viaY) { sw.Land(sm, moon); yield return L59Frames(3); }
        if (F75.Old)
        {
            // before: the crew stands at the Moon's Starship pad; shots for the composite at the same moments
            yield return L59Settle(2f);
            F75Check("old: Starship prop stands on the Moon, the mech waits in orbit", MechLand75.PropActive(WorldId.Moon) && sm.parkedFor == fa, Ships72());
            F75Ph(1, "mdesc"); yield return L59Settle(6f); F75Ph(2, "mtouch"); yield return L59Settle(6f); F75Ph(3, "mramp"); yield return L59Settle(6f);
            if (slots[0].rig != null) { slots[0].rig.SetYaw(180f); slots[0].rig.SetPitch(8f); }
            yield return L59Settle(2f); F75Ph(5, "msurf"); yield return L59Settle(8f);
            sw.ToOrbit(fa, "moon"); yield return L59Settle(3f);
            F75Ph(7, "mlift"); yield return L59Settle(8f);
            StoryMech.DtCap = dtWas; F75Result(T); yield break;
        }
        yield return Land75Run(fa, sm, big, moon, 0, "m", true);
        MechLand75 ml = landed75;
        if (ml == null || !MechLand75.All.Contains(ml)) { F75Check("moon landing finished with the mech parked", false, "landing object gone | " + Ships72()); StoryMech.DtCap = dtWas; F75Result(T); yield break; }
        float gy = MechLand75.Ground(ml.world, fa.transform.position.x, fa.transform.position.z);
        F75Check("everyone walked down onto the Moon (P1 + the AI rider), on foot beside the ramp", fa.land75 == null && fa.world == WorldId.Moon && fa.model.gameObject.activeSelf && Mathf.Abs(fa.transform.position.y - gy) < 2f && (fa.transform.position - ml.F).magnitude < 12f && (ai == null || (ai.land75 == null && ai.world == WorldId.Moon)),
            "P1 " + fa.transform.position.ToString("0.0") + " ground " + gy.ToString("0.0") + " ramp foot " + ml.F.ToString("0.0") + " ai " + (ai != null ? ai.world + " held " + (ai.land75 != null) : "-"));
        yield return L59Settle(4f);
        F75Check("the mech stays parked on the Moon (no Starship there, nothing in orbit)", MechLand75.All.Contains(ml) && ml.phase == MechLand75.Ph.Parked && !MechLand75.PropActive(WorldId.Moon) && !sm.Shown72 && sm.parkedFor == fa && big.Away72,
            "phase " + ml.phase + " prop " + MechLand75.PropActive(WorldId.Moon) + " shown " + sm.Shown72 + " | " + Ships72());
        // P1 beside the parked mech, looking up at it
        Vector3 standAt = ml.F + ml.fwd * 6f + ml.right * 5f; standAt.y = MechLand75.Ground(ml.world, standAt.x, standAt.z) + 0.3f;
        Vector3 toM = (ml.C + Vector3.up * ml.H * 0.4f) - standAt; Vector3 flat = toM; flat.y = 0f;
        fa.Teleport(standAt);
        if (slots[0].rig != null) { slots[0].rig.SetYaw(Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg); slots[0].rig.SetPitch(-12f); }
        yield return L59Settle(3f);
        F75Ph(5, "msurf");
        yield return L59Settle(8f);
        // A at the foot of the ramp
        Vector3 foot = ml.F + ml.fwd * 1.5f; foot.y = MechLand75.Ground(ml.world, foot.x, foot.z) + 0.3f;
        fa.Teleport(foot);
        yield return L59Settle(1.5f);
        string pr = fa.prompt;
        F75Check("at the ramp foot P1 sees 'board ... (take off)'", pr.Contains("take off"), "'" + pr + "'");
        yield return L59Press(pads[0], GamepadButton.South);
        yield return L59Frames(4);
        bool viaA = ml.phase == MechLand75.Ph.WalkUp || ml.phase == MechLand75.Ph.RampUp;
        F75Check("A at the ramp foot: P1 (and the AI rider) walk up the ramp", viaA && (ai == null || ai.land75 == ml), "phase " + ml.phase + " walkers " + ml.walkers.Count + " crew " + ml.crew.Count);
        if (!viaA) ml.Board(fa);
        yield return Phase75(ml, () => { foreach (var w in ml.walkers) if ((w.transform.position - ml.F).magnitude > 2f) return true; return ml.phase != MechLand75.Ph.WalkUp; }, 30f);
        F75Ph(6, "mboard");
        yield return Phase75(ml, () => ml.phase == MechLand75.Ph.Climb && ml.t > 1.2f, 90f);
        F75Check("ramp up, flames on, the mech lifts off", ml != null && ml.phase == MechLand75.Ph.Climb && ml.alt > 1f && FlamesOn75(ml) >= 2, ml != null ? "phase " + ml.phase + " alt " + ml.alt.ToString("0") : "gone");
        F75Ph(7, "mlift");
        yield return F72Until(() => fa.world == WorldId.Space, 60f);
        yield return L59Settle(1.5f);
        F75Check("back in Moon orbit: P1 flies the trillion-story mech, the AI rides, the parked copy is gone, Moon Starship prop back", fa.vehicle == sm && sm.mechForm == big && (ai == null || ai.passengerOf == sm) && !MechLand75.All.Contains(ml) && MechLand75.PropActive(WorldId.Moon) && sm.Shown72,
            Ships72() + " | prop " + MechLand75.PropActive(WorldId.Moon));

        // ---------------- home: the same landing at the ranch, then the real mech stands there ----------------
        sm.EnterOrbit(earth, sw.bodies[earth].cap, 0f);
        yield return L59Frames(3);
        sw.Land(sm, earth);
        yield return Land75Run(fa, sm, big, earth, 0, "e", false);
        MechLand75 me = landed75;
        bool gotE = got75; Vector3 eC = lastC75;
        if (slots[0].rig != null) { Vector3 tm = eC + Vector3.up * big.height * 0.4f - fa.transform.position; tm.y = 0f; if (tm.sqrMagnitude > 1f) slots[0].rig.SetYaw(Mathf.Atan2(tm.x, tm.z) * Mathf.Rad2Deg); slots[0].rig.SetPitch(-12f); }
        yield return F72Until(() => me == null || !MechLand75.All.Contains(me), 30f);
        yield return L59Settle(2f);
        F75Ph(8, "eramp");
        F75Check("ranch landing: everyone walked down, the real trillion-story mech stands where it landed, no space ship in use", gotE && (me == null || !MechLand75.All.Contains(me)) && fa.world == WorldId.Ranch && fa.land75 == null && !big.Away72 && big.gameObject.activeInHierarchy && (new Vector2(big.transform.position.x - eC.x, big.transform.position.z - eC.z)).magnitude < 3f && sw.ShipsInUse == 0,
            "mech at " + big.transform.position.ToString("0") + " landed " + eC.ToString("0") + " away " + big.Away72 + " | " + Ships72());
        F75Check("P1 can climb back into the trillion-story mech", big.CanEnter(fa), "can enter " + big.CanEnter(fa));

        // ---------------- a smaller mech: chest ramp ----------------
        Frog pf = n > 1 ? fb : fa; int ps = n > 1 ? 1 : 0; Gamepad pp = pads[ps];
        StoryMech small = FindMech(pf.charId, n > 1 ? 1 : 0);
        int dest = n > 1 ? mars : cal;
        string tag2 = n > 1 ? "m2" : "m3";
        if (small != null)
        {
            pf.SendTo(WorldId.Ranch, small.transform.position + small.transform.right * (small.height * 0.35f + 4f), 0f);
            yield return L59Frames(2);
            pf.EnterVehicle(small);
            yield return L59Settle(1f);
            small.GoToSpace();
            yield return F72Until(() => pf.world == WorldId.Space, 40f);
            yield return L59Settle(1.5f);
            Starship s2 = sw.ShipOf(pf);
            F75Check(pf.nick + " flies own " + small.Title + " in space", s2 != null && s2.mechForm == small && pf.vehicle == s2, Ships72());
            if (s2 != null && s2.mechForm == small)
            {
                s2.EnterOrbit(dest, sw.bodies[dest].cap, 0f);
                yield return L59Frames(3);
                sw.Land(s2, dest);
                yield return Land75Run(pf, s2, small, dest, ps, tag2, false);
                MechLand75 m2 = landed75;
                if (m2 != null)
                {
                    F75Ph(9, tag2 + "ramp");
                    yield return L59Settle(2f);
                    Vector3 sa = m2.F + m2.fwd * 4f + m2.right * 6f; sa.y = MechLand75.Ground(m2.world, sa.x, sa.z) + 0.3f;
                    pf.Teleport(sa);
                    Vector3 tm = m2.C + Vector3.up * m2.H * 0.4f - sa; tm.y = 0f;
                    if (slots[ps].rig != null) { slots[ps].rig.SetYaw(Mathf.Atan2(tm.x, tm.z) * Mathf.Rad2Deg); slots[ps].rig.SetPitch(-10f); }
                    yield return L59Settle(3f);
                    F75Check(tag2 + ": " + small.Title + " parked on " + sw.bodies[dest].name + ", " + pf.nick + " on foot beside it, no Starship prop there", MechLand75.All.Contains(m2) && m2.phase == MechLand75.Ph.Parked && pf.land75 == null && pf.world == m2.world && !MechLand75.PropActive(m2.world),
                        "phase " + m2.phase + " world " + pf.world + " prop " + MechLand75.PropActive(m2.world));
                    F75Ph(10, tag2 + "surf");
                    yield return L59Settle(8f);
                }
            }
        }
        else F75Info("smaller mech", "none found for " + pf.nick);
        StoryMech.DtCap = dtWas;
        F75Result(T);
    }
    void F75Result(string tag) { Debug.Log("FFU75 RESULT " + f75Pass + "/" + f75Total + " (" + tag + ")"); Debug.Log("FFDEMO " + tag + " done"); }
}
