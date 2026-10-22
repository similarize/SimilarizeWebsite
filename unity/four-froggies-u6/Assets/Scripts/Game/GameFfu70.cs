using System.Collections;
using UnityEngine;

// ffu70 (Ben's 6.6 work list, Batch A) switches + probe.
// F70.Old (&ff70old=1) = the pre-ffu70 behaviour for every Batch A item, so one build gives true before / after shots.
// Probe: ?ffdemo=1&ffshot=ffu70[&f70=lake,skate,tank,swim] - logs "FFU70 PASS|FAIL <what> | <detail>", "FFU70 RESULT n/m",
// "FFDEMO ffu70 ph N <phase>" and "FFDEMO ffu70 done".
public static class F70
{
    static int old = -1;
    public static bool Old { get { if (old < 0) old = Ffu33.Url("ff70old") == "1" ? 1 : 0; return old == 1; } }
    public static bool Has(string part) { string s = Ffu33.Url("f70"); return string.IsNullOrEmpty(s) || ("," + s + ",").Contains("," + part + ","); }
}

public class Ffu70 : MonoBehaviour
{
    public static Ffu70 I;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        if (F70.Old) Debug.Log("FFU70 OLD behaviour on (&ff70old=1): Batch A items reverted for before shots");
        I = new GameObject("ffu70").AddComponent<Ffu70>();
        DontDestroyOnLoad(I.gameObject);
    }
    bool started;
    void Update()
    {
        if (started) return;
        string s = Ffu33.Url("ffdemo") != null ? Ffu33.Url("ffshot") : null;
        if (s != "ffu70") { enabled = false; return; }
        if (Game.I == null || Game.I.state != Game.State.Play || Game.I.slots.Count == 0) return;
        started = true;
        Game.I.StartCoroutine(Game.I.F70Run());
    }
}

public partial class Game
{
    int f7Pass, f7Total, f7Ph;
    PIn f7In = new PIn(); bool f7Hook;
    void F7Check(string what, bool ok, string d) { f7Total++; if (ok) f7Pass++; Debug.Log("FFU70 " + (ok ? "PASS" : "FAIL") + " " + what + " | " + d); }
    void F7Next(string n) { Debug.Log("FFDEMO ffu70 ph " + (f7Ph++) + " " + n + " t " + Time.time.ToString("0.0")); }
    IEnumerator F7Game(float s) { float t = 0f, r0 = Time.realtimeSinceStartup; while (t < s && Time.realtimeSinceStartup - r0 < 150f) { yield return null; t += Time.deltaTime; } }
    bool f7Pad;   // the tank test plays P1 as a gamepad (Tank reads driver.inputKind)
    PIn F7Hook(PIn i) { if (f7Pad && slots.Count > 0) frogs[slots[0].frog].inputKind = InputKind.Gamepad; return f7Hook ? f7In : i; }
    PIn F7Set(Vector2 move, float gas, float brake, bool b, bool rtPress) { var o = new PIn(); o.move = move; o.gas = gas; o.brake = brake; o.handbrake = b; o.downHeld = b; o.fire = rtPress; o.fireHeld = gas > 0.5f; o.climb = gas - brake; f7In = o; f7Hook = true; return o; }
    static float F7Dt { get { return Mathf.Min(Time.deltaTime, Frog.DtCap); } }   // the froggy's own (capped) step: SwiftShader frames are slow
    static string V3(Vector3 v) { return "(" + v.x.ToString("0.0") + ", " + v.y.ToString("0.0") + ", " + v.z.ToString("0.0") + ")"; }

    // a deep pond spot (bed at least 2.5 m under the surface), nearest the pond centre
    static Vector3 F7DeepSpot(float minDepth)
    {
        Vector3 best = new Vector3(Layout.PondC.x, Layout.WaterY, Layout.PondC.y); float bd = 1e9f;
        for (int ix = -6; ix <= 6; ix++)
            for (int iz = -5; iz <= 5; iz++)
            {
                float x = Layout.PondC.x + ix * 8f, z = Layout.PondC.y + iz * 8f;
                if (Layout.PondQ(x, z) > 0.7f) continue;
                if (Ranch.GY(x, z) > Layout.WaterY - minDepth) continue;
                float d = ix * ix + iz * iz;
                if (d < bd) { bd = d; best = new Vector3(x, Layout.WaterY, z); }
            }
        return best;
    }

    public IEnumerator F70Run()
    {
        yield return new WaitForSecondsRealtime(3f);
        Frog f = frogs[slots[0].frog];
        Frog.DtCap = 0.1f; StoryMech.DtCap = 0.1f;
        demoHook = F7Hook;
        Debug.Log("FFU70 start: P1 " + f.nick + " old=" + F70.Old + " parts " + (Ffu33.Url("f70") ?? "all"));
        if (F70.Has("lake")) yield return F7Lake(f);
        if (F70.Has("skate")) yield return F7Skate(f);
        if (F70.Has("tank")) yield return F7Tank(f);
        F7Done();
    }

    void F7Done()
    {
        f7Hook = false; demoHook = null;
        Debug.Log("FFU70 RESULT " + f7Pass + "/" + f7Total + (F70.Old ? " (OLD behaviour)" : ""));
        Debug.Log("FFDEMO ffu70 done");
    }

    // ---------------- item 3: exiting a vehicle / mech into the lake must not push the froggy across it ----------------
    IEnumerator F7Lake(Frog f)
    {
        if (f.vehicle != null) f.ExitVehicle();
        if (f.world != WorldId.Ranch) { f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f); yield return F7Game(1f); }
        Vector3 deep = F7DeepSpot(2.5f);
        Debug.Log("FFU70 lake: deep spot " + V3(deep) + " bed " + Ranch.GY(deep.x, deep.z).ToString("0.0") + " PondQ " + Layout.PondQ(deep.x, deep.z).ToString("0.00"));
        // (a) a truck coasting at 10 m/s through deep water
        GroundVehicle truck = null;
        foreach (Vehicle v in Vehicle.All) { var g = v as GroundVehicle; if (g != null && g.amph != null && g.driver == null && !g.stowed && g.rb != null && !g.rb.isKinematic) { truck = g; break; } }
        if (truck == null) F7Check("lake: a Cybertruck to test with", false, "none free");
        else
        {
            Quaternion q = Quaternion.Euler(0f, 90f, 0f); Vector3 p = deep + Vector3.up * 0.4f;
            truck.rb.velocity = Vector3.zero; truck.rb.angularVelocity = Vector3.zero; truck.rb.position = p; truck.rb.rotation = q; truck.transform.SetPositionAndRotation(p, q);
            f.Teleport(p + Vector3.up * 2f); f.EnterVehicle(truck);
            yield return F7Game(1.5f);
            F7Next("lakeexit");
            Vector3 fw = truck.transform.forward; fw.y = 0f; fw.Normalize();
            truck.rb.velocity = fw * 10f;
            yield return null;
            Vector3 tv = truck.Velocity;
            f.ExitVehicle();
            yield return F7SwimDrift(f, "truck", tv);
        }
        // (b) a 10-story mech walking through the lake
        StoryMech mech = null;
        foreach (var m in StoryMech.AllMechs) if (m != null && m.driver == null && !m.stowed && m.height < 20f) { mech = m; break; }
        if (mech == null) { Debug.Log("FFU70 lake: no free 10-story mech - mech exit not tested"); yield break; }
        if (f.vehicle != null) f.ExitVehicle();
        Vector3 mp = deep + new Vector3(0f, Ranch.GY(deep.x, deep.z) - deep.y, 0f);
        mech.transform.SetPositionAndRotation(mp, Quaternion.Euler(0f, 0f, 0f)); if (mech.rb != null) { mech.rb.position = mp; mech.rb.rotation = Quaternion.identity; }
        f.Teleport(deep + Vector3.up * 3f); f.EnterVehicle(mech);
        var walk = new PIn(); walk.move = new Vector2(0f, 1f);
        mech.inputHook = i => walk;
        yield return F7Game(3f);
        F7Next("mechexit");
        Vector3 mv = mech.Velocity;
        mech.inputHook = null;
        f.ExitVehicle();
        yield return F7SwimDrift(f, "mech", mv);
    }

    IEnumerator F7SwimDrift(Frog f, string tag, Vector3 vehVel)
    {
        Vector3 e0 = f.transform.position; bool intoW = f.exitIntoWater;
        Vector3 v0 = f.DbgVel; v0.y = 0f;
        // wait (<= 4 s) until it is swimming, then 6 s of no input
        float t = 0f, r0 = Time.realtimeSinceStartup;
        while (!f.Swimming && t < 4f && Time.realtimeSinceStartup - r0 < 90f) { yield return null; t += F7Dt; }
        bool swam = f.Swimming;
        Vector3 s0 = f.transform.position; float maxV = 0f;
        t = 0f; r0 = Time.realtimeSinceStartup;
        while (t < 6f && Time.realtimeSinceStartup - r0 < 180f) { yield return null; t += F7Dt; Vector3 dv = f.DbgVel; dv.y = 0f; if (t > 2f) maxV = Mathf.Max(maxV, dv.magnitude); }
        Vector3 s1 = f.transform.position;
        float drift = new Vector2(s1.x - s0.x, s1.z - s0.z).magnitude, total = new Vector2(s1.x - e0.x, s1.z - e0.z).magnitude;
        Vector3 vEnd = f.DbgVel; vEnd.y = 0f;
        Debug.Log("FFU70 lake " + tag + ": vehicle v " + V3(vehVel) + " | exit spot " + V3(e0) + " into water " + intoW + " | vel right after exit " + v0.magnitude.ToString("0.00") + " m/s | swimming " + swam + " | drift " + drift.ToString("0.0") + " m in 6 s (total " + total.ToString("0.0") + " m from the exit spot) | vel at end " + vEnd.magnitude.ToString("0.00") + " | max vel after 2 s " + maxV.ToString("0.00"));
        F7Check("lake " + tag + ": no push across the lake after getting out in deep water", swam && drift < 2.5f && total < 6f && vEnd.magnitude < 0.3f,
            "swimming " + swam + ", drift " + drift.ToString("0.0") + " m / 6 s, " + total.ToString("0.0") + " m from the exit spot, end vel " + vEnd.magnitude.ToString("0.00") + " m/s, vehicle was at " + new Vector2(vehVel.x, vehVel.z).magnitude.ToString("0.0") + " m/s");
    }

    // ---------------- item 10: skateboards = rolling wheels + clacks, no motor sound ----------------
    IEnumerator F7Skate(Frog f)
    {
        if (f.vehicle != null) f.ExitVehicle();
        Skateboard b = null;
        foreach (var x in Skateboard.Boards) if (x != null && x.rack && x.driver == null) { b = x; break; }
        if (b == null) { F7Check("skate: a rack board to ride", false, "none free"); yield break; }
        f.SendTo(WorldId.Ranch, Skateboard.RackC + new Vector3(0.5f, 0f, 3f), 180f);
        yield return F7Game(1.5f);
        int c0 = b.clacks, p0 = b.pops, o0 = b.ollies, l0 = b.landings;
        f.EnterVehicle(b);
        b.aiRider = f; b.aiPhase = 0; b.aiT = 0f; b.aiSpinGoal = 180f; b.aiOllie = true; b.aiOllied = false; b.aiRunSecs = 999f;
        F7Next("skate");
        float t = 0f, r0 = Time.realtimeSinceStartup, maxRoll = 0f, maxHiss = 0f, maxSpd = 0f; string lv = "-"; bool hum = false, pipeShot = false; float hardT = 0f; Vector3 lp = b.transform.position;
        while (t < 26f && Time.realtimeSinceStartup - r0 < 240f)
        {
            yield return null; t += Time.deltaTime;
            if (f.vehicle != b) break;
            hum |= b.HasHum;
            Vector3 bp = b.transform.position; if (Time.deltaTime > 0f) maxSpd = Mathf.Max(maxSpd, (bp - lp).magnitude / Time.deltaTime); lp = bp;
            if (b.hardSurface && b.mode == Skateboard.Mode.Ground) hardT += Time.deltaTime;
            var sv = b.VoiceRaw; if (sv != null) { maxRoll = Mathf.Max(maxRoll, sv.vol[0]); maxHiss = Mathf.Max(maxHiss, sv.vol[1]); }
            if (t > 6f && lv == "-") lv = b.VoiceLevels;
            if (t > 9f && !pipeShot) { pipeShot = true; F7Next("skatepipe"); }
        }
        int clacks = b.clacks - c0, pops = b.pops - p0, ollies = b.ollies - o0, lands = b.landings - l0;
        Debug.Log("FFU70 skate: " + b.name + " rode " + t.ToString("0") + " s, max speed " + maxSpd.ToString("0.0") + " m/s, " + hardT.ToString("0.0") + " s on a hard surface (" + b.lastSurface + ") | clacks " + clacks + " pops " + pops + " (ollies " + ollies + ", landings " + lands + ") | voice " + lv + "| max roll " + maxRoll.ToString("0.00") + " hiss " + maxHiss.ToString("0.00") + " | hum source " + hum);
        F7Check("skate: no motor / bearing hum layer on the board", !hum, "hum source " + hum + ", voice " + lv);
        F7Check("skate: rolling-wheel sound while it rolls", maxRoll > 0.05f && maxHiss > 0.02f, "max roll " + maxRoll.ToString("0.00") + ", hiss " + maxHiss.ToString("0.00"));
        F7Check("skate: clacks over the seams at speed", clacks >= 4, clacks + " clacks in " + hardT.ToString("0.0") + " s on concrete, max " + maxSpd.ToString("0.0") + " m/s");
        F7Check("skate: ollie pop / landing clack", ollies + lands == 0 || pops >= 1, pops + " pops for " + ollies + " ollies + " + lands + " landings");
        b.aiRider = null;
        if (f.vehicle != null) f.ExitVehicle();
        yield return F7Game(1f);
    }

    // ---------------- item 11: Ripsaw M5 tank + Ripsaw EV2: RT drives, B fires (tank), tracked = no drift ----------------
    IEnumerator F7Tank(Frog f)
    {
        if (f.vehicle != null) f.ExitVehicle();
        Tank tank = null; GroundVehicle rip = null;
        foreach (Vehicle v in Vehicle.All)
        {
            if (v == null || v.driver != null || v.stowed) continue;
            if (tank == null && v is Tank) tank = (Tank)v;
            else if (rip == null && v is GroundVehicle && !(v is Tank) && ((GroundVehicle)v).tracked) rip = (GroundVehicle)v;
        }
        f7Pad = true;
        Vector3 lawn = Ranch.FrogSpawn(0) + new Vector3(-6f, 0f, 14f);
        if (tank == null) F7Check("tank: the Ripsaw M5 to test with", false, "none free");
        else
        {
            F7Place(tank, lawn, 90f); f.Teleport(lawn + Vector3.up * 3f); f.EnterVehicle(tank);
            F7Set(Vector2.zero, 0f, 1f, false, false); yield return F7Game(1.5f);
            F7Next("tankrt");
            int s0 = tank.shells;
            // pad RT: gas 1, the press frame also sets i.fire (as Pads.Read does) - must drive, must not fire
            F7Set(Vector2.zero, 1f, 0f, false, true); yield return null;
            F7Set(Vector2.zero, 1f, 0f, false, false); yield return F7Game(2.5f);
            float fs = tank.ForwardSpeed; int shRT = tank.shells - s0;
            F7Check("tank: RT drives forward", fs > 3f, "forward speed " + fs.ToString("0.0") + " m/s after 2.5 s of RT");
            F7Check("tank: RT no longer fires", shRT == 0, shRT + " shells while holding RT");
            // B held at speed with full steer: tracked -> no drift
            F7Set(new Vector2(1f, 0f), 1f, 0f, true, false); float dk = 0f; bool dr = false;
            { float t = 0f, r0 = Time.realtimeSinceStartup; while (t < 1.2f && Time.realtimeSinceStartup - r0 < 60f) { yield return null; t += Time.deltaTime; dk = Mathf.Max(dk, tank.driftK); dr |= tank.drifting; } }
            F7Check("tank: tracked = no drift (B + full steer at speed)", dk < 0.01f && !dr, "driftK max " + dk.ToString("0.00") + ", drifting " + dr);
            F7Next("tankb");
            F7Set(Vector2.zero, 0f, 1f, false, false); yield return F7Game(2f);
            s0 = tank.shells;
            F7Set(Vector2.zero, 0f, 0f, true, false); yield return null; yield return null;
            F7Set(Vector2.zero, 0f, 0f, false, false); yield return F7Game(1.2f);
            int shTap = tank.shells - s0;
            F7Check("tank: B fires a shell", shTap == 1, shTap + " shell(s) for one B tap");
            s0 = tank.shells;
            F7Set(Vector2.zero, 0f, 0f, true, false); yield return F7Game(3.2f);
            int shHold = tank.shells - s0;
            F7Check("tank: holding B repeats", shHold >= 2, shHold + " shells in 3.2 s of B held");
            F7Set(Vector2.zero, 0f, 1f, false, false); yield return F7Game(3f);
            float rv = tank.ForwardSpeed;
            F7Check("tank: LT brakes then reverses", rv < -1f, "forward speed " + rv.ToString("0.0") + " m/s after 3 s of LT");
            string hl = tank.HelpLine, hp = tank.HelpFor(InputKind.Gamepad) ?? "";
            F7Check("tank: hint line says RT drive / B fire", hl.Contains("RT drive") && hl.Contains("B fire") && hp.Contains("B::fire"), hl);
            F7Set(Vector2.zero, 0f, 1f, false, false); yield return F7Game(1f);
            if (f.vehicle != null) f.ExitVehicle();
            yield return F7Game(1f);
        }
        if (rip == null) Debug.Log("FFU70 tank: no free Ripsaw EV2 - EV2 not tested");
        else
        {
            Vector3 l2 = lawn + new Vector3(0f, 0f, 12f);
            F7Place(rip, l2, 90f); f.Teleport(l2 + Vector3.up * 3f); f.EnterVehicle(rip);
            F7Set(Vector2.zero, 0f, 1f, false, false); yield return F7Game(1.5f);
            F7Next("ripsaw");
            F7Set(Vector2.zero, 1f, 0f, false, true); yield return null;
            F7Set(Vector2.zero, 1f, 0f, false, false); yield return F7Game(2.5f);
            float fs = rip.ForwardSpeed;
            F7Check("Ripsaw EV2: RT drives forward", fs > 3f, rip.Title + " forward speed " + fs.ToString("0.0") + " m/s");
            F7Set(new Vector2(1f, 0f), 1f, 0f, true, false); float dk = 0f; bool dr = false;
            { float t = 0f, r0 = Time.realtimeSinceStartup; while (t < 1.2f && Time.realtimeSinceStartup - r0 < 60f) { yield return null; t += Time.deltaTime; dk = Mathf.Max(dk, rip.driftK); dr |= rip.drifting; } }
            F7Check("Ripsaw EV2: tracked = no drift", dk < 0.01f && !dr, "driftK max " + dk.ToString("0.00") + ", drifting " + dr);
            F7Check("Ripsaw EV2: hint line", rip.HelpLine.Contains("RT drive"), rip.HelpLine);
            F7Set(Vector2.zero, 0f, 1f, false, false); yield return F7Game(1.5f);
            if (f.vehicle != null) f.ExitVehicle();
        }
        f7Pad = false; f7Hook = false;
        yield return F7Game(1f);
    }
    static void F7Place(GroundVehicle v, Vector3 at, float yaw)
    {
        Vector3 p = new Vector3(at.x, Ranch.GY(at.x, at.z) + 0.8f + v.bigLift, at.z); Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        v.rb.velocity = Vector3.zero; v.rb.angularVelocity = Vector3.zero; v.rb.position = p; v.rb.rotation = q; v.transform.SetPositionAndRotation(p, q);
    }
}
