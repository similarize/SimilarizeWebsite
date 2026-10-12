using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// ffu35 demo shots / synthetic tests for the probe (?ffdemo=1&ffshot=...):
//   stocktrailer  the stock trailer by the pasture: gate down, two cows + a horse walk up the ramp (rear 3/4 view), side view
//                 through the slats, gate shut, James's Cybertruck hitches and tows it, the rig moves to the trailer-barn
//                 apron, gate down, the animals walk out and graze there (logs how far they stray from the drop spot).
//                 Logs "FFDEMO stock t phase ...".
//   padtest       virtual gamepads (InputSystem.AddDevice) - no real pad needed:
//                 1) GHOST: pad A joins mid-game; a MIRROR of pad A appears mid-game and presses the same buttons (same frame
//                    and 2 frames late, plus a Start "join" attempt) -> no extra froggy; pad A removed -> its mirror keeps
//                    the froggy; mirror removed + a new same-model pad presses A -> the same froggy is re-bound; a real
//                    second pad B presses A alone -> joins (two real pads still work).
//                 2) PHONE (split screen, 2 pads): LB opens / LB closes, LB opens / B closes, pad B's phone isn't closed by
//                    pad A's B, a mirrored LB press toggles it once, the phone closes when its user gets into a vehicle.
//                 3) LIVE FEED by pad: RB bigger, B smaller, View closes; in a vehicle hold View = bigger.
//                 Logs "PADTEST ..." lines and a final "PADTEST RESULT pass/total".
public partial class Game
{
    static bool Is35(string sc) { return sc == "stocktrailer" || sc == "padtest"; }
    float sfT, sfLog;
    int sfPhase = -1;
    Vehicle sfTruck;
    readonly List<Animal> sfAnimals = new List<Animal>();
    Vector3 sfDest; bool sfStarted, sfMoved, sfUnloadAsked;
    Vector3 sfCamP, sfCamL; bool sfCamInit;

    void Demo35Start(Frog f, string sc)
    {
        if (f.world != WorldId.Ranch) f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f);
        if (f.vehicle != null) f.ExitVehicle();
        if (sc == "padtest") { StartCoroutine(PadTest()); return; }
        if (!Roster.IsFrog(f.charId) || f.charId != 0) SetSeatChar(f.id, 0, false);
        demoKeepChars = true;
        sfT = 0f; sfLog = 0f; sfPhase = -1; sfStarted = false;
        Debug.Log("FFDEMO stock start trailer=" + (LivestockTrailer.I != null));
    }

    // ---------------- stock trailer demo ----------------
    bool Demo35Cam(Frog f, string sc, out Vector3 pos, out Vector3 look)
    {
        pos = look = Vector3.zero;
        if (sc != "stocktrailer") return false;
        var st = LivestockTrailer.I;
        if (st == null) { if (Time.frameCount % 120 == 0) Debug.Log("FFDEMO stock: no trailer yet"); return false; }
        if (!sfStarted) { sfStarted = true; StockSetup(f, st); }
        float dt = Mathf.Min(Time.deltaTime, 0.1f);
        sfT += dt;
        float t = sfT;
        Transform tt = st.tr.transform;
        Vector3 back = -tt.forward; back.y = 0f; back.Normalize();
        Vector3 side = new Vector3(back.z, 0f, -back.x);
        int ph = t < 1.5f ? 0 : t < 14f ? 1 : t < 17f ? 2 : t < 18f ? 3 : t < 27f ? 4 : t < 29f ? 5 : 6;
        if (ph != sfPhase)
        {
            sfPhase = ph;
            Debug.Log("FFDEMO stock phase " + ph + " t " + t.ToString("0.0") + " | " + st.LogLine());
            if (ph == 1)
            {
                // James at the gate: "Open the gate", then "Load the nearest ..." three times (the real hotspot actions)
                f.Teleport(st.RampFoot(1.2f) + side * 1.5f + Vector3.up * 0.1f);
                st.OpenGate(f);
            }
            if (ph == 3) { st.CloseGate(f); }
            if (ph == 4) StockHitch(f, st);
            if (ph == 5) StockMove(f, st);
            if (ph == 6)
            {
                if (f.vehicle != null) f.ExitVehicle();
                f.Teleport(st.RampFoot(1.4f) + side * 1.8f + Vector3.up * 0.1f);
                st.OpenGate(f);   // gate down -> they walk out
                sfUnloadAsked = true;
            }
        }
        if (ph == 1 && st.GateOpen && t > 2.5f && st.Aboard + CountWalking(st) < sfAnimals.Count)
        {
            foreach (var a in sfAnimals) if (a.enabled) { if (st.StartLoad(a, f)) break; }
        }
        sfLog += dt;
        if (sfLog > 1f)
        {
            sfLog = 0f;
            string extra = "";
            if (ph >= 6 && st.lastDrop != Vector3.zero)
            {
                float mx = 0f, sum = 0f; int n = 0;
                foreach (var a in sfAnimals) { Vector3 d = a.Pos - st.lastDrop; d.y = 0f; mx = Mathf.Max(mx, d.magnitude); sum += d.magnitude; n++; }
                extra = " | graze drop " + st.lastDrop.ToString("0") + " maxDist " + mx.ToString("0.0") + " avg " + (n > 0 ? sum / n : 0f).ToString("0.0") + " enabled " + sfAnimals.FindAll(a => a.enabled).Count;
                foreach (var a in sfAnimals) { var h = a.GetComponent<Horse>(); if (h != null) extra += " horseHome " + h.HomeRect.center.ToString("0") + (h.HomeIsPasture ? " pasture" : " moved"); }
            }
            Debug.Log("FFDEMO stock t " + t.ToString("0.0") + " ph " + ph + " | " + st.LogLine() + " | James " + f.transform.position.ToString("0") + (f.vehicle != null ? " in " + f.vehicle.Title : "") + " prompt '" + f.prompt + "'" + extra);
        }
        // cameras
        Vector3 c = tt.position + Vector3.up * 1.4f;
        Vector3 wantP, wantL;
        if (ph <= 1) { wantP = c + back * 10.5f - side * 7.5f + Vector3.up * 4.2f; wantL = c + back * 4.5f; }
        else if (ph <= 3) { wantP = c - side * 6.2f + back * 0.6f + Vector3.up * 1.6f; wantL = c + Vector3.up * 0.1f; }
        else if (ph <= 5)
        {
            Vector3 tp = sfTruck != null ? sfTruck.transform.position : c;
            Vector3 mid = (tp + tt.position) * 0.5f + Vector3.up * 1.2f;
            wantP = mid - side * 14f + back * 4f + Vector3.up * 5.5f; wantL = mid;
        }
        else
        {
            Vector3 d = st.lastDrop != Vector3.zero ? st.lastDrop : c;
            wantP = tt.position - side * 9f + tt.forward * 3f + Vector3.up * 6.5f; wantL = d + Vector3.up * 0.8f;
        }
        bool cut = !sfCamInit || ph != sfCamPh || (wantP - sfCamP).sqrMagnitude > 900f;
        sfCamPh = ph;
        if (cut) { sfCamP = wantP; sfCamL = wantL; sfCamInit = true; }
        else { float k = Mathf.Min(1f, dt * 3f); sfCamP = Vector3.Lerp(sfCamP, wantP, k); sfCamL = Vector3.Lerp(sfCamL, wantL, k); }
        pos = sfCamP; look = sfCamL;
        float gy = Ranch.GY(pos.x, pos.z) + 0.8f; if (pos.y < gy) pos.y = gy;
        return true;
    }
    int sfCamPh = -1;

    int CountWalking(LivestockTrailer st) { int n = 0; foreach (var r in st.riders) if (!r.inside) n++; return n; }

    void StockSetup(Frog f, LivestockTrailer st)
    {
        // two cows + one horse, as if led out of the pasture: grazing just south of the gate
        Transform tt = st.tr.transform;
        Vector3 back = -tt.forward; back.y = 0f; back.Normalize();
        Vector3 side = new Vector3(back.z, 0f, -back.x);
        Vector3 spot = st.RampFoot(3.5f) + side * 5.5f;
        sfAnimals.Clear();
        foreach (var a in Animal.All) if (a != null && a.kind == Animal.Kind.Cow && sfAnimals.Count < 2) sfAnimals.Add(a);
        foreach (var a in Animal.All) if (a != null && a.kind == Animal.Kind.Horse && a.GetComponent<Horse>() != null && sfAnimals.Count < 3) sfAnimals.Add(a);
        for (int i = 0; i < sfAnimals.Count; i++)
        {
            Animal a = sfAnimals[i];
            Vector3 p = spot + side * (i - 1) * 2.6f + back * ((i % 2) * 1.8f);
            p.y = Ranch.GY(p.x, p.z);
            a.area = new Rect(p.x - 1.5f, p.z - 1.5f, 3f, 3f);
            a.transform.position = p;
            a.skittish = false;
            a.SyncYaw();
        }
        sfTruck = null;
        foreach (var v in Vehicle.All) if (v != null && v.Title == "James's Cybertruck") { sfTruck = v; break; }
        if (sfTruck == null) foreach (var v in Vehicle.All) if (v != null && TrailerTow.CanTow(v)) { sfTruck = v; break; }
        Debug.Log("FFDEMO stock setup animals " + sfAnimals.Count + " truck " + (sfTruck != null ? sfTruck.Title : "none") + " trailer " + st.LogLine());
    }

    void SfPlace(Vehicle v, Vector3 p, float yaw)
    {
        RaycastHit h;
        if (Physics.Raycast(p + Vector3.up * 4f, Vector3.down, out h, 12f, Trailer.WheelMask, QueryTriggerInteraction.Ignore)) p.y = h.point.y;
        else p.y = Ranch.GY(p.x, p.z);
        p.y += 0.35f;
        Quaternion r = Quaternion.Euler(0f, yaw, 0f);
        if (!v.rb.isKinematic) { v.rb.velocity = Vector3.zero; v.rb.angularVelocity = Vector3.zero; }
        v.rb.position = p; v.rb.rotation = r; v.transform.SetPositionAndRotation(p, r);
        Physics.SyncTransforms();
    }

    void StockHitch(Frog f, LivestockTrailer st)
    {
        if (sfTruck == null) return;
        Transform tt = st.tr.transform;
        Vector3 fw = tt.forward; fw.y = 0f; fw.Normalize();
        float back = -Trailer.HitchLocal(sfTruck).z;
        SfPlace(sfTruck, st.tr.BallWorld + fw * (back + 0.15f), tt.eulerAngles.y);
        if (f.vehicle != null) f.ExitVehicle();
        f.EnterVehicle(sfTruck);
        st.tr.Hitch(sfTruck);
        demoHook = StockDrive;
        Debug.Log("FFDEMO stock hitched " + st.tr.Hitched + " to " + sfTruck.Title);
    }

    PIn StockDrive(PIn i)
    {
        var o = new PIn();
        if (sfTruck == null || sfPhase != 4) return o;
        float fs = sfTruck.ForwardSpeed;
        if (sfT > 25.5f) { o.brake = fs > 0.3f ? 1f : 0f; return o; }
        float e = 5f - fs;
        o.gas = Mathf.Clamp(e * 0.35f, 0f, 1f); o.brake = Mathf.Clamp(-e * 0.3f, 0f, 1f);
        return o;
    }

    // "tow it anywhere": the hitched rig to the trailer-barn apron (or 60 m east if there is no barn)
    void StockMove(Frog f, LivestockTrailer st)
    {
        demoHook = null;
        if (sfTruck == null) return;
        Vector3 dest = TrailerBarn.Built ? TrailerBarn.Centre + new Vector3(10f, 0f, -(TrailerBarn.Dp * 0.5f + 13f)) : st.tr.transform.position + new Vector3(60f, 0f, 0f);
        SfPlace(sfTruck, dest, 90f);
        if (st.tr.Hitched) st.tr.PlaceBehind(sfTruck);
        Physics.SyncTransforms();
        sfDest = dest; sfMoved = true;
        Debug.Log("FFDEMO stock moved rig to " + dest.ToString("0") + " | " + st.LogLine());
    }

    // ---------------- synthetic pad tests ----------------
    int ptPass, ptTotal;
    void Check(string what, bool ok, string detail = "")
    {
        ptTotal++; if (ok) ptPass++;
        Debug.Log("PADTEST " + (ok ? "PASS" : "FAIL") + " " + what + (detail.Length > 0 ? " | " + detail : ""));
    }

    static GamepadState Pst(params GamepadButton[] bs) { var s = new GamepadState(); foreach (var b in bs) s = s.WithButton(b); return s; }

    IEnumerator Frames(int n) { for (int k = 0; k < n; k++) yield return null; }

    // press + release one button; mirror = a second device that reports the same press (lag in frames)
    IEnumerator Press(Gamepad p, GamepadButton b, Gamepad mirror = null, int lag = 0)
    {
        InputSystem.QueueStateEvent(p, Pst(b));
        if (mirror != null && lag == 0) InputSystem.QueueStateEvent(mirror, Pst(b));
        if (mirror != null && lag > 0) { yield return Frames(lag); InputSystem.QueueStateEvent(mirror, Pst(b)); }
        yield return Frames(3);
        InputSystem.QueueStateEvent(p, new GamepadState());
        if (mirror != null) InputSystem.QueueStateEvent(mirror, new GamepadState());
        yield return Frames(3);
    }

    int PadSlots() { int n = 0; foreach (var s in slots) if (s.kind == InputKind.Gamepad) n++; return n; }

    IEnumerator Settle(float s) { yield return new WaitForSecondsRealtime(s); yield return Frames(3); }

    IEnumerator PadTest()
    {
        Debug.Log("PADTEST start (slots " + slots.Count + ", pads " + Gamepad.all.Count + ")");
        yield return Settle(1f);
        int basePads = PadSlots();
        // ---- 1) ghost / duplicate pad ----
        Gamepad A = InputSystem.AddDevice<Gamepad>("FFTestPad");
        yield return Settle(1.2f);
        Check("a new device alone does not join", PadSlots() == basePads, "pad slots " + PadSlots());
        yield return Press(A, GamepadButton.South);
        yield return Settle(0.6f);
        Check("pad A joins by pressing A mid-game", PadSlots() == basePads + 1 && FindPad(A) != null, "pad slots " + PadSlots());
        Slot sa = FindPad(A);
        Gamepad M = InputSystem.AddDevice<Gamepad>("FFTestPad");   // the same pad exposed twice
        yield return Settle(1.2f);
        for (int k = 0; k < 3; k++) { yield return Press(A, GamepadButton.South, M, 0); yield return Settle(0.35f); }
        Check("mirror pressing A with pad A (same frame x3): no extra froggy", PadSlots() == basePads + 1 && FindPad(M) == null, "pad slots " + PadSlots() + " mirrorAlias " + PadGuard.IsAlias(M));
        yield return Press(A, GamepadButton.South, M, 2);
        yield return Settle(0.4f);
        yield return Press(A, GamepadButton.Start, M, 1);
        yield return Settle(0.4f);
        yield return Press(A, GamepadButton.LeftShoulder, M, 0);   // phone open + close by the same physical press stays sane
        yield return Settle(0.3f);
        if (RobotPhone.I != null && RobotPhone.I.open) RobotPhone.I.Close(null, "test reset");
        Check("mirror 2 frames late + Start join attempt: still one froggy", PadSlots() == basePads + 1 && FindPad(M) == null && PadGuard.IsAlias(M), "pad slots " + PadSlots() + " blocked joins " + PadGuard.BlockedJoins);
        InputSystem.RemoveDevice(A);
        yield return Settle(0.6f);
        Check("pad A removed: its mirror keeps the same froggy", sa != null && sa.pad == M && PadSlots() == basePads + 1, "slot pad #" + (sa != null && sa.pad != null ? sa.pad.deviceId : -1) + " mirror #" + M.deviceId);
        InputSystem.RemoveDevice(M);
        yield return Settle(0.6f);
        Gamepad R = InputSystem.AddDevice<Gamepad>("FFTestPad");   // reconnect under a new id
        yield return Settle(1.2f);
        Check("reconnected pad does not join by itself", PadSlots() == basePads + 1, "pad slots " + PadSlots());
        yield return Press(R, GamepadButton.South);
        yield return Settle(0.6f);
        Check("reconnected pad presses A: the same froggy is re-bound", sa != null && sa.pad == R && PadSlots() == basePads + 1, "pad slots " + PadSlots());
        Gamepad B = InputSystem.AddDevice<Gamepad>("FFTestPadTwo");
        yield return Settle(1.2f);
        yield return Press(B, GamepadButton.South);
        yield return Settle(0.6f);
        Check("a real second pad presses A alone: joins", PadSlots() == basePads + 2 && FindPad(B) != null, "pad slots " + PadSlots());
        // ---- 2) phone by pad, split screen ----
        shared = false; ApplyLayout();
        Slot sr = FindPad(R), sb = FindPad(B);
        Frog fr = sr != null ? frogs[sr.frog] : null, fb = sb != null ? frogs[sb.frog] : null;
        if (fr != null && fb != null && RobotPhone.I != null)
        {
            foreach (var fx in new[] { fr, fb }) { if (fx.vehicle != null) fx.ExitVehicle(); if (fx.world != WorldId.Ranch) fx.SendTo(WorldId.Ranch, Ranch.FrogSpawn(fx.id), 0f); }
            yield return Settle(0.5f);
            var ph = RobotPhone.I;
            yield return Press(R, GamepadButton.LeftShoulder); yield return Settle(0.3f);
            Check("split screen: pad R LB opens the phone for its froggy", ph.open && ph.user == fr, "open " + ph.open + " user " + (ph.user != null ? ph.user.nick : "-"));
            yield return Press(R, GamepadButton.LeftShoulder); yield return Settle(0.3f);
            Check("pad R LB closes it", !ph.open);
            yield return Press(R, GamepadButton.LeftShoulder); yield return Settle(0.3f);
            yield return Press(R, GamepadButton.East); yield return Settle(0.3f);
            Check("pad R LB opens, B closes", !ph.open);
            yield return Press(B, GamepadButton.LeftShoulder); yield return Settle(0.3f);
            bool bOpen = ph.open && ph.user == fb;
            yield return Press(R, GamepadButton.East); yield return Settle(0.3f);
            Check("pad B opens its phone; pad R's B doesn't close pad B's phone", bOpen && ph.open && ph.user == fb);
            yield return Press(B, GamepadButton.East); yield return Settle(0.3f);
            Check("pad B's B closes its phone", !ph.open);
            yield return Press(B, GamepadButton.LeftShoulder); yield return Settle(0.3f);
            yield return Press(B, GamepadButton.LeftShoulder); yield return Settle(0.3f);
            Check("pad B LB opens, LB closes", !ph.open);
            Gamepad M2 = InputSystem.AddDevice<Gamepad>("FFTestPad");   // a mirror of pad R appears mid-game
            yield return Settle(1.2f);
            yield return Press(R, GamepadButton.LeftShoulder, M2, 0); yield return Settle(0.3f);
            bool once = ph.open && ph.user == fr;
            yield return Press(R, GamepadButton.LeftShoulder, M2, 0); yield return Settle(0.3f);
            Check("mirrored LB press opens / closes once (no double toggle)", once && !ph.open && FindPad(M2) == null, "opened " + once + " now " + ph.open);
            InputSystem.RemoveDevice(M2);
            yield return Settle(0.4f);
            // phone open, then the froggy gets into a vehicle on the ranch -> closes (no trap)
            yield return Press(R, GamepadButton.LeftShoulder); yield return Settle(0.3f);
            Vehicle veh = null; float bd = 1e9f;
            foreach (var v in Vehicle.All) if (v != null && v.driver == null && !v.stowed && v is GroundVehicle && v.CanEnter(fr)) { float d = (v.transform.position - fr.transform.position).sqrMagnitude; if (d < bd) { bd = d; veh = v; } }
            if (veh != null) fr.EnterVehicle(veh);
            yield return Settle(0.5f);
            Check("phone open + froggy enters a vehicle: phone closes", !ph.open || ph.user != fr, "vehicle " + (veh != null ? veh.Title : "none"));
            // ---- 3) live feed by pad ----
            Robot rob = RanchLife.I != null && RanchLife.I.robots.Count > 1 ? RanchLife.I.robots[1] : null;
            if (RobotFeed.I != null && rob != null)
            {
                if (fr.vehicle != null) fr.ExitVehicle();
                yield return Settle(0.4f);
                RobotFeed.I.Show(rob, 1);
                Debug.Log("FFDEMO padtest feed small t=" + Time.realtimeSinceStartup.ToString("0.0") + " hint '" + RobotFeed.I.HintText + "'");
                yield return Settle(10f);
                Debug.Log("PADTEST feed hint (pad) '" + RobotFeed.I.HintText + "'");
                Check("feed hint shows pad glyphs", RobotFeed.I.HintText.Contains("VIEW") && RobotFeed.I.HintText.Contains("RB"));
                yield return Press(R, GamepadButton.RightShoulder); yield return Settle(0.4f);
                Check("feed: RB makes it full screen", RobotFeed.I.mode == 2);
                Debug.Log("FFDEMO padtest feed full t=" + Time.realtimeSinceStartup.ToString("0.0"));
                yield return Settle(10f);
                yield return Press(R, GamepadButton.East); yield return Settle(0.4f);
                Check("feed: B back to the small panel", RobotFeed.I.mode == 1);
                yield return Press(B, GamepadButton.Select); yield return Settle(0.4f);
                Check("feed: View (pad B - any player's pad) closes it", RobotFeed.I.mode == 0);
                Debug.Log("FFDEMO padtest feed closed t=" + Time.realtimeSinceStartup.ToString("0.0"));
                yield return Settle(10f);
                // in a vehicle: hold View = bigger, then View tap closes
                Vehicle v2 = null; bd = 1e9f;
                foreach (var v in Vehicle.All) if (v != null && v.driver == null && !v.stowed && v is GroundVehicle && v.CanEnter(fr)) { float d = (v.transform.position - fr.transform.position).sqrMagnitude; if (d < bd) { bd = d; v2 = v; } }
                if (v2 != null)
                {
                    fr.EnterVehicle(v2); yield return Settle(0.4f);
                    RobotFeed.I.Show(rob, 1);
                    InputSystem.QueueStateEvent(R, Pst(GamepadButton.Select));
                    yield return new WaitForSecondsRealtime(0.9f); yield return Frames(4);
                    InputSystem.QueueStateEvent(R, new GamepadState()); yield return Frames(4);
                    Check("feed in a vehicle: hold View = full screen", RobotFeed.I.mode == 2);
                    yield return Press(R, GamepadButton.Select); yield return Settle(0.4f);
                    Check("feed in a vehicle: View tap closes", RobotFeed.I.mode == 0);
                    fr.ExitVehicle();
                }
            }
            else Debug.Log("PADTEST feed skipped (no robot / feed)");
        }
        else Debug.Log("PADTEST phone skipped (slots " + (sr != null) + " " + (sb != null) + ")");
        Debug.Log("PADTEST RESULT " + ptPass + "/" + ptTotal + " | guard aliases " + PadGuard.Aliases + " releases " + PadGuard.Releases + " blocked joins " + PadGuard.BlockedJoins + " | phone opens " + RobotPhone.Opens + " closes " + RobotPhone.Closes + " | " + PadGuard.List());
    }
}
