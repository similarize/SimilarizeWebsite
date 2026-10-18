using UnityEngine;

// ffu50 hook-up + demos (kept out of Bootstrap / Game like ffu49): once the ranch, the garden and the robot mission pad exist it
// builds Figure 03's rocket + pad + materials bins + the garden workbench (Fig03Rocket50.cs).
//  ?ffdemo=1&ffshot=fig03rocket : P1 DRIVE IT!s Figure 03 to his ramp and presses A -> boards, countdown, launch (ascent).
//  ?ffdemo=1&ffshot=fig03refuse : P1 walks to the ramp + presses A; P1 drives Unitree to the ramp + A; an AI froggy and a pet
//      are put at the ramp; the phone "Fly your rocket" order for Unitree -> all refused. Then the phone order for Figure 03 ->
//      he walks over and boards (phone path).
//  ?ffdemo=1&ffshot=fig03land   : Figure 03 coming home (RobotMission.Demo "return"): landing on his pad, out, samples into
//      his materials bin (+ meteorite metal) -> stash.
//  ?ffdemo=1&ffshot=craft50     : P1 takes materials from Figure 03's bin -> garden workbench: arrowheads -> arrows -> hands
//      them to Figure 03.
 //  ?ffdemo=1&ffshot=carrierexit : (Bill's bug) carrier mode in the pond; P1 at the helm presses E (parked), idles 3 s, walks
//      the 4 deck directions; then E at speed; then in the water beside it. Logs drift / push / carry in the deck frame.
//  ?ffdemo=1&ffshot=hunt50      : Figure 03 by the pasture, phone "Hunt" -> shoots a cow (tip-over + POOF + drumstick); P1
//      picks it up, cooks it on the garden campfire, eats it; the cow pops back.
// Logs "FFDEMO ffu50 ..." and "FFDEMO ffu50 done".
[DefaultExecutionOrder(100)]
public class Ffu50 : MonoBehaviour
{
    public static Ffu50 I;
    bool built; string shot = "";
    float t0 = -1f, logT, phT, buildWait;
    int ph;
    Vector3 camPos, camLook; bool camInit, doneLogged;
    Frog f; Robot fig, other;
    bool press;
    Vector3 walkTo; bool walking; Robot walkBot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        I = new GameObject("ffu50").AddComponent<Ffu50>();
        DontDestroyOnLoad(I.gameObject);
    }

    static Robot Bot(string id)
    {
        if (RanchLife.I == null) return null;
        foreach (var r in RanchLife.I.robots) if (r != null && r.id == id && !r.displayOnly) return r;
        return null;
    }

    void Update()
    {
        if (!built)
        {
            if (Bootstrap.I == null || Game.I == null || RanchLife.I == null || Time.timeSinceLevelLoad < 1.0f) return;
            buildWait += Time.unscaledDeltaTime;
            if (Garden.I == null && buildWait < 6f) return;
            built = true;
            try { Fig03Rocket.Build(); } catch (System.Exception e) { Debug.LogError("ffu50 rocket: " + e); }
            try { Fig50.Create(); } catch (System.Exception e) { Debug.LogError("ffu50 glue: " + e); }
            shot = Ffu33.Url("ffdemo") != null ? (Ffu33.Url("ffshot") ?? "") : "";
            if (!(shot.StartsWith("fig03") || shot == "craft50" || shot == "hunt50" || shot == "carrierexit")) shot = "";
            if (shot.Length > 0) Debug.Log("FFDEMO ffu50 demo " + shot);
        }
        if (shot.Length == 0 || Game.I.state != Game.State.Play || Game.I.slots.Count == 0) return;
        if (t0 < 0f)
        {
            t0 = Time.time; f = Game.I.frogs[Game.I.slots[0].frog]; Frog.DtCap = 0.15f; Robot.DtCap = 0.3f;
            fig = Bot(Fig03Rocket.Owner); other = Bot("unitree") ?? Bot("optimus");
            Destruct.RegenScale = 0.25f;
            Game.I.Demo33Hook(Hook);
            Debug.Log("FFDEMO ffu50 start " + shot + " p1 " + f.nick + " fig " + (fig != null) + " rocket " + (Fig03Rocket.ship != null) + " " + Stash50.Line);
        }
        if (shot == "carrierexit") { CarrierExit(Time.time - t0); phT += Time.deltaTime; return; }
        if (fig == null) { if (!doneLogged) { doneLogged = true; Debug.Log("FFDEMO ffu50 done (no Figure 03)"); } return; }
        float t = Time.time - t0;
        phT += Time.deltaTime;
        if (shot == "fig03rocket") Rocket(t);
        else if (shot == "fig03refuse") Refuse(t);
        else if (shot == "fig03land") Land(t);
        else if (shot == "craft50") Craft(t);
        else if (shot == "hunt50") Hunt(t);
        if (Time.unscaledTime > logT)
        {
            logT = Time.unscaledTime + 1f;
            var m = fig.mission;
            Debug.Log("FFDEMO ffu50 " + shot + " ph " + ph + " t " + t.ToString("0.0") + " fig " + fig.transform.position.ToString("0.0") + " " + fig.StatusLine + " | mission " + (m != null ? m.PhaseLine + " " + m.DistLine : "-") + " | ship " + (Fig03Rocket.ship != null ? Fig03Rocket.ship.transform.position.ToString("0.0") : "-") + " | p1 " + f.transform.position.ToString("0.0") + " held " + Fig50.HeldKind(f) + " prompt '" + f.prompt + "' | " + Stash50.Short);
        }
    }

    void Next(int p) { ph = p; phT = 0f; Debug.Log("FFDEMO ffu50 " + shot + " phase " + p + " t " + (Time.time - t0).ToString("0.0")); }
    void Done() { if (!doneLogged) { doneLogged = true; Debug.Log("FFDEMO ffu50 done " + shot + " | " + Stash50.Line); } }

    // P1's input: walk the froggy (or the robot it drives) towards walkTo, one-shot A presses
    PIn Hook(PIn i)
    {
        var o = new PIn(); o.look = Vector2.zero;
        if (dirMove.sqrMagnitude > 0.01f)
        {
            float yaw = Game.I.slots[0].rig != null ? Game.I.slots[0].rig.yaw : 0f;
            Vector3 l = Quaternion.Euler(0f, -yaw, 0f) * dirMove.normalized;
            o.move = new Vector2(l.x, l.z);
        }
        else if (walking)
        {
            Vector3 from = walkBot != null ? walkBot.transform.position : f.transform.position;
            Vector3 d = walkTo - from; d.y = 0f;
            if (d.magnitude > 0.9f)
            {
                float yaw = Game.I.slots[0].rig != null ? Game.I.slots[0].rig.yaw : 0f;
                Vector3 l = Quaternion.Euler(0f, -yaw, 0f) * d.normalized;
                o.move = new Vector2(l.x, l.z) * 0.8f;
            }
            else walking = false;
        }
        if (press) { o.use = true; press = false; }
        return o;
    }
    Vector3 dirMove;
    void WalkTo(Vector3 p, Robot bot = null) { walkTo = p; walking = true; walkBot = bot; }

    static Vector3 Out(float d, float side = 0f)
    {
        Quaternion q = Quaternion.Euler(0f, Fig03Rocket.Yaw, 0f);
        Vector3 p = Fig03Rocket.RampFoot + q * new Vector3(side, 0f, d);
        return RobotNav.G(p.x, p.z);
    }
    void CamRocket(float back, float up, bool follow)
    {
        Quaternion q = Quaternion.Euler(0f, Fig03Rocket.Yaw, 0f);
        Vector3 b = Fig03Rocket.Base;
        Vector3 sp = Fig03Rocket.ship != null ? Fig03Rocket.ship.transform.position : b;
        Vector3 look = follow ? sp + Vector3.up * 9f : b + Vector3.up * 6f;
        if (follow && sp.y - b.y > 60f) look = sp + Vector3.up * 9f;
        Cam(b + q * new Vector3(back * 0.55f, 0f, back) + Vector3.up * up, look, 0.08f);
    }
    void Cam(Vector3 p, Vector3 look, float k)
    {
        if (!camInit) { camPos = p; camLook = look; camInit = true; }
        camPos = Vector3.Lerp(camPos, p, k); camLook = Vector3.Lerp(camLook, look, Mathf.Max(k, 0.15f));
    }
    void LateUpdate()
    {
        if (shot.Length == 0 || !camInit || Game.I == null || Game.I.slots.Count == 0) return;
        Camera c = Game.I.slots[0].cam;
        if (c == null) return;
        c.transform.position = camPos;
        c.transform.LookAt(camLook);
    }

    // ---------------- Figure 03 boards via DRIVE IT! and launches ----------------
    void Rocket(float t)
    {
        if (Fig03Rocket.ship == null) { Done(); return; }
        switch (ph)
        {
            case 0:
                if (t < 4f) break;
                fig.DemoStart(Out(9f, 1.5f), Fig03Rocket.Yaw + 180f, -1, 1f);
                f.SendTo(WorldId.Ranch, Out(12f, 5f), Fig03Rocket.Yaw + 180f);
                fig.TakeManual(f);
                WalkTo(Out(0.6f), fig);
                Debug.Log("FFDEMO ffu50 fig03rocket: P1 drives Figure 03 (DRIVE IT!) from " + fig.transform.position.ToString("0.0") + " to the ramp foot " + Fig03Rocket.RampFoot.ToString("0.0"));
                Next(1);
                break;
            case 1:
                if (fig.manual == null && fig.mission == null) { fig.TakeManual(f); }
                if (!walking || RobotNav.Flat(fig.transform.position - Fig03Rocket.RampFoot) < 2.2f)
                {
                    walking = false;
                    Debug.Log("FFDEMO ffu50 fig03rocket: at the ramp (" + RobotNav.Flat(fig.transform.position - Fig03Rocket.RampFoot).ToString("0.0") + " m), prompt '" + f.prompt + "' -> A");
                    press = true; Next(2);
                }
                else if (phT > 40f) { Debug.Log("FFDEMO ffu50 fig03rocket: walk timeout - pressing anyway"); press = true; Next(2); }
                break;
            case 2:
                if (fig.mission != null) { Debug.Log("FFDEMO ffu50 fig03rocket: BOARDED via DRIVE IT! - " + fig.mission.PhaseLine + " own " + fig.mission.own); Next(3); }
                else if (phT > 3f) { press = true; phT = 0f; }
                break;
            case 3:
                if (fig.mission == null || phT > 60f) { Done(); break; }
                if (fig.mission.PhaseLine.StartsWith("in transit")) { Debug.Log("FFDEMO ffu50 fig03rocket: in transit to Mars (ascent done) - ship " + Fig03Rocket.ship.transform.position.ToString("0")); Done(); }
                break;
        }
        if (ph >= 1) CamRocket(30f, ph >= 3 ? 4f : 7f, ph >= 3);
    }

    // ---------------- everybody else is refused; then the phone order for Figure 03 ----------------
    void Refuse(float t)
    {
        if (Fig03Rocket.ship == null) { Done(); return; }
        switch (ph)
        {
            case 0:
                if (t < 4f) break;
                fig.DemoStart(Out(14f, -6f), 0f, -1, 1f); fig.Order("stop", f);
                f.SendTo(WorldId.Ranch, Out(6f, 0.5f), Fig03Rocket.Yaw + 180f);
                WalkTo(Out(1.2f));
                Debug.Log("FFDEMO ffu50 fig03refuse: P1 (" + f.nick + ") walks up to the ramp");
                Next(1);
                break;
            case 1:
                if (phT > 6f) { Debug.Log("FFDEMO ffu50 fig03refuse: P1 at " + RobotNav.Flat(f.transform.position - Fig03Rocket.RampFoot).ToString("0.0") + " m, prompt '" + f.prompt + "' -> A"); press = true; Next(2); }
                break;
            case 2:
                if (phT < 3f) break;
                if (other != null)
                {
                    other.DemoStart(Out(8f, 3f), Fig03Rocket.Yaw + 180f, -1, 1f);
                    f.SendTo(WorldId.Ranch, Out(10f, 7f), Fig03Rocket.Yaw + 180f);
                    other.TakeManual(f);
                    WalkTo(Out(0.8f), other);
                    Debug.Log("FFDEMO ffu50 fig03refuse: P1 drives " + other.robotName + " to the ramp");
                }
                Next(3);
                break;
            case 3:
                if (other == null || !walking || phT > 30f) { Debug.Log("FFDEMO ffu50 fig03refuse: " + (other != null ? other.robotName : "-") + " at " + (other != null ? RobotNav.Flat(other.transform.position - Fig03Rocket.RampFoot).ToString("0.0") : "-") + " m -> A"); walking = false; press = true; Next(4); }
                break;
            case 4:
                if (phT < 3f) break;
                if (other != null && other.manual != null) other.ReleaseManual(false);
                // an AI froggy and a pet at the ramp
                foreach (var o in Game.I.frogs) if (o != null && !o.human && o.world == WorldId.Ranch) { o.SendTo(WorldId.Ranch, Out(1.0f, 1.2f), Fig03Rocket.Yaw); Debug.Log("FFDEMO ffu50 fig03refuse: AI " + o.nick + " put at the ramp"); break; }
                foreach (var a in Animal.All) if (a != null && a.petName.Length > 0) { Vector3 pp = Out(0.8f, -1.4f); a.transform.position = new Vector3(pp.x, a.Ground(pp.x, pp.z), pp.z); a.Scare(pp + Vector3.forward * 0.1f); Debug.Log("FFDEMO ffu50 fig03refuse: pet " + a.petName + " put at the ramp"); break; }
                if (other != null) Debug.Log("FFDEMO ffu50 fig03refuse: phone 'Fly your rocket' for " + other.robotName + " -> " + Fig50.PhoneRocket(other, f));
                Next(5);
                break;
            case 5:
                if (phT < 4f) break;
                Debug.Log("FFDEMO ffu50 fig03refuse: phone 'Fly your rocket' for Figure 03 -> " + Fig50.PhoneRocket(fig, f));
                Next(6);
                break;
            case 6:
                if (fig.mission == null) { if (phT > 5f) { Debug.Log("FFDEMO ffu50 fig03refuse: Figure 03 has no mission?"); Done(); } break; }
                if (fig.mission.PhaseLine.StartsWith("launching") || fig.mission.PhaseLine.StartsWith("in transit") || phT > 70f) { Debug.Log("FFDEMO ffu50 fig03refuse: phone path -> " + fig.mission.PhaseLine); if (phT > 3f && fig.mission.PhaseLine.StartsWith("launching")) Done(); }
                break;
        }
        if (ph >= 1) CamRocket(24f, 6f, ph >= 6);
    }

    // ---------------- coming home: landing on his pad, samples into his bin ----------------
    void Land(float t)
    {
        if (Fig03Rocket.ship == null) { Done(); return; }
        switch (ph)
        {
            case 0:
                if (t < 4f) break;
                f.SendTo(WorldId.Ranch, Out(16f, 8f), Fig03Rocket.Yaw + 180f);
                fig.battery = 1f;
                RobotMission.Demo(fig, 0, 3, "return");
                Debug.Log("FFDEMO ffu50 fig03land: Figure 03 returning with 3 Mars rocks, " + Stash50.Line);
                Next(1);
                break;
            case 1:
                if (fig.mission == null) { Debug.Log("FFDEMO ffu50 fig03land: home - " + Stash50.Line + " rocket at " + Fig03Rocket.ship.transform.position.ToString("0.0")); Next(2); }
                else if (phT > 90f) Done();
                break;
            case 2:
                if (phT > 3f) Done();
                break;
        }
        if (ph >= 1)
        {
            bool landed = fig.mission == null || fig.mission.phase >= 14;
            if (!landed) CamRocket(32f, 5f, true);
            else { Vector3 bp = Fig03Rocket.BinPos; Quaternion q = Quaternion.Euler(0f, Fig03Rocket.Yaw, 0f); Cam(bp + q * new Vector3(2.5f, 2.6f, 6.5f), bp + Vector3.up * 0.9f, 0.06f); }
        }
    }

    // ---------------- craft: bin -> workbench (arrowheads, arrows) -> Figure 03 ----------------
    void Craft(float t)
    {
        switch (ph)
        {
            case 0:
                if (t < 4f) break;
                if (Stash50.Materials < 3) { Stash50.Add(0, 2, "demo top-up"); Stash50.Add(1, 1, "demo top-up"); Stash50.Add(2, 1, "demo top-up"); }
                {
                    Vector3 bp = Fig03Rocket.Built ? Fig03Rocket.BinPos : MissionSite.ShipBase + new Vector3(0f, 0f, 12f);
                    Quaternion q = Quaternion.Euler(0f, Fig03Rocket.Built ? Fig03Rocket.Yaw : 0f, 0f);
                    Vector3 fp = bp + q * new Vector3(0f, 0f, 1.5f);
                    f.SendTo(WorldId.Ranch, RobotNav.G(fp.x, fp.z), (Fig03Rocket.Built ? Fig03Rocket.Yaw : 0f) + 180f);
                    Cam(bp + q * new Vector3(2.8f, 2.4f, 5.5f), bp + Vector3.up * 0.8f, 1f);
                }
                Next(1);
                break;
            case 1:
                if (phT > 3f) { Debug.Log("FFDEMO ffu50 craft50: at the bin, prompt '" + f.prompt + "' -> A"); press = true; Next(2); }
                break;
            case 2:
                if (phT < 3f) break;
                Debug.Log("FFDEMO ffu50 craft50: holding kind " + Fig50.HeldKind(f) + " -> to the workbench " + Fig50.BenchPos.ToString("0.0"));
                f.SendTo(WorldId.Ranch, Fig50.BenchFront, 0f);
                { Vector3 to = Fig50.BenchPos - Fig50.BenchFront; f.SendTo(WorldId.Ranch, Fig50.BenchFront, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg); }
                Next(3);
                break;
            case 3:
                if (phT > 3f) { Debug.Log("FFDEMO ffu50 craft50: bench prompt '" + f.prompt + "' -> A (arrowheads)"); press = true; Next(4); }
                break;
            case 4:
                if (phT > 5f) { Debug.Log("FFDEMO ffu50 craft50: holding kind " + Fig50.HeldKind(f) + ", prompt '" + f.prompt + "' -> A (arrows)"); press = true; Next(5); }
                break;
            case 5:
                if (phT > 5f)
                {
                    Debug.Log("FFDEMO ffu50 craft50: holding kind " + Fig50.HeldKind(f) + " -> Figure 03 comes over");
                    Vector3 to = Fig50.BenchPos - Fig50.BenchFront; to.y = 0f; Vector3 sd = Vector3.Cross(Vector3.up, to.normalized);
                    Vector3 fp = Fig50.BenchFront + sd * 1.6f - to.normalized * 0.4f;
                    fig.DemoStart(fp, Mathf.Atan2(-sd.x, -sd.z) * Mathf.Rad2Deg, -1, 1f); fig.Order("stop", f);
                    Next(6);
                }
                break;
            case 6:
                if (phT > 3f) { Debug.Log("FFDEMO ffu50 craft50: next to Figure 03, prompt '" + f.prompt + "' -> A"); press = true; Next(7); }
                break;
            case 7:
                if (phT > 4f) { Debug.Log("FFDEMO ffu50 craft50: holding kind " + Fig50.HeldKind(f) + ", Figure 03 arrows " + Fig50.ArrowsFor(fig)); Done(); }
                break;
        }
        if (ph >= 2 && ph < 8)
        {
            Vector3 to = Fig50.BenchPos - Fig50.BenchFront; to.y = 0f;
            Vector3 sd = Vector3.Cross(Vector3.up, to.normalized);
            Cam(Fig50.BenchFront - to.normalized * 3.4f + sd * 2.6f + Vector3.up * 2.2f, (Fig50.BenchPos + Fig50.BenchFront) * 0.5f + Vector3.up * 0.9f, ph == 2 ? 1f : 0.1f);
        }
    }

    // ---------------- hunt: Figure 03 shoots a cow; P1 cooks + eats the drumstick ----------------
    Animal cow; Vector3 meatAt;
    void Hunt(float t)
    {
        switch (ph)
        {
            case 0:
                if (t < 4f) break;
                if (Fig50.ArrowsFor(fig) < 3) { Stash50.figArrows += 3; Stash50.Save(); Debug.Log("FFDEMO ffu50 hunt50: demo top-up 3 arrows -> " + Stash50.Line); }
                cow = Fig50.NearestCow(new Vector3(-110f, 0f, 116f));
                if (cow == null) { Debug.Log("FFDEMO ffu50 hunt50: no cow"); Done(); return; }
                {
                    Vector3 c = cow.Pos, dir = new Vector3(1f, 0f, 0.15f).normalized;
                    Vector3 fp = c + dir * 20f;
                    fig.DemoStart(fp, Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg, -1, 1f);
                    f.SendTo(WorldId.Ranch, RobotNav.G(fp.x + 4f, fp.z + 6f), 270f);
                    Debug.Log("FFDEMO ffu50 hunt50: cow at " + c.ToString("0.0") + ", Figure 03 at " + fig.transform.position.ToString("0.0") + " -> phone Hunt: " + Fig50.PhoneHunt(fig, f));
                }
                Next(1);
                break;
            case 1:
                if (Meat50.All.Count > 0) { meatAt = Meat50.All[0].transform.position; Debug.Log("FFDEMO ffu50 hunt50: drumstick at " + meatAt.ToString("0.0") + " after " + phT.ToString("0.0") + " s"); Next(2); }
                else if (fig.hunt == null && phT > 40f) { Debug.Log("FFDEMO ffu50 hunt50: hunt ended without a drumstick (" + fig.StatusLine + ")"); Done(); }
                else if (phT > 120f) Done();
                break;
            case 2:
                if (phT < 3f) break;
                f.SendTo(WorldId.Ranch, RobotNav.G(meatAt.x + 1.1f, meatAt.z), 270f);
                Next(3);
                break;
            case 3:
                if (phT > 2.5f) { Debug.Log("FFDEMO ffu50 hunt50: at the drumstick, prompt '" + f.prompt + "' -> A"); press = true; Next(4); }
                break;
            case 4:
                if (phT < 2.5f) break;
                Debug.Log("FFDEMO ffu50 hunt50: holding kind " + Fig50.HeldKind(f) + " -> to the campfire " + (Campfire.I != null ? Campfire.I.Pos.ToString("0.0") : "-"));
                if (Campfire.I == null) { Done(); return; }
                { Vector3 to = Campfire.I.Pos - Fig50.FireFront; f.SendTo(WorldId.Ranch, Fig50.FireFront, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg); }
                Next(5);
                break;
            case 5:
                if (phT > 3f) { Debug.Log("FFDEMO ffu50 hunt50: at the fire, prompt '" + f.prompt + "' -> A (cook)"); press = true; Next(6); }
                break;
            case 6:
                if (phT > 8f) { Debug.Log("FFDEMO ffu50 hunt50: prompt '" + f.prompt + "' -> A (eat)"); press = true; Next(7); }
                break;
            case 7:
                if (phT > 8f || (phT > 4f && TipOver50.Respawning.Count == 0)) { Debug.Log("FFDEMO ffu50 hunt50: respawn pending " + TipOver50.Respawning.Count); Done(); }
                break;
        }
        if (ph >= 1 && ph <= 2)
        {
            Vector3 a = fig.transform.position, b = cow != null && cow.enabled ? cow.Pos : (Meat50.All.Count > 0 ? Meat50.All[0].transform.position : a);
            Vector3 mid = (a + b) * 0.5f, d = b - a; d.y = 0f; Vector3 sd = Vector3.Cross(Vector3.up, d.normalized);
            Cam(mid + sd * (8f + d.magnitude * 0.45f) + Vector3.up * 3.5f, mid + Vector3.up * 1f, ph == 1 && phT < 0.2f ? 1f : 0.08f);
        }
        else if (ph >= 3 && ph <= 4) Cam(meatAt + new Vector3(4f, 2.5f, 3f), meatAt + Vector3.up * 0.5f, 0.2f);
        else if (ph >= 5 && Campfire.I != null)
        {
            Vector3 fp = Campfire.I.Pos, to = fp - Fig50.FireFront; to.y = 0f; Vector3 sd = Vector3.Cross(Vector3.up, to.normalized);
            Cam(fp - to.normalized * 4.2f + sd * 2.2f + Vector3.up * 2.2f, (fp + Fig50.FireFront) * 0.5f + Vector3.up * 0.8f, ph == 5 && phT < 0.2f ? 1f : 0.1f);
        }
    }

    // ---------------- Bill's carrier bug: E off the helm -> stand still + full control (deck, moving, water) ----------------
    Vector3 l0, w0; int dirI; float idleT; string leg = "parked";
    static readonly string[] DirName = { "fwd", "right", "back", "left" };
    void CarrierExit(float t)
    {
        var rig = CarrierRig.I;
        if (rig == null) { Done(); return; }
        Transform rt = rig.transform;
        Vector3 fw = rt.forward; fw.y = 0f; fw.Normalize(); Vector3 rr = new Vector3(fw.z, 0f, -fw.x);
        Vector3 lp = rt.InverseTransformPoint(f.transform.position);
        switch (ph)
        {
            case 0:
                if (t < 4f) break;
                if (!rig.Carrier) rig.ForceCarrier(new Vector3(Layout.PondC.x, 0f, Layout.PondC.y), 0f);
                Next(1);
                break;
            case 1:
                if (!rig.Carrier) { if (phT > 20f) { Debug.Log("FFDEMO ffu50 carrierexit: no carrier mode"); Done(); } break; }
                if (phT < 2f) break;
                f.SendTo(WorldId.Ranch, rig.DeckPoint(-0.5f, 0f) + Vector3.up * 0.2f, rt.eulerAngles.y);
                f.EnterVehicle(rig.helm);
                Debug.Log("FFDEMO ffu50 carrierexit: P1 at the helm: " + (f.vehicle != null ? f.vehicle.Title : "-") + " carrier at " + rt.position.ToString("0.0"));
                Next(2);
                break;
            case 2:   // E (parked, or moving after leg 2)
                if (phT < (leg == "parked" ? 3f : 3.5f) || (leg == "moving" && rig.trailer.rb.velocity.magnitude < 1.5f && phT < 6f)) break;   // ffu50c: E while really under way, well before the shore turns it back into bike + camper
                if (leg == "moving") rig.helm.demoHook = null;
                Debug.Log("FFDEMO ffu50 carrierexit [" + leg + "] E at carrier speed " + rig.trailer.rb.velocity.magnitude.ToString("0.0") + " m/s");
                press = true; Next(3);
                break;
            case 3:
                if (f.vehicle != null) { if (phT > 2f) { Debug.Log("FFDEMO ffu50 carrierexit [" + leg + "] still at the helm, canExit " + f.vehicle.CanExit(f) + " -> E again"); press = true; phT = 1f; } break; }
                if (phT < 0.4f) break;
                l0 = lp; w0 = f.transform.position; idleT = 0f;
                Debug.Log("FFDEMO ffu50 carrierexit [" + leg + "] off the helm at deck-local " + lp.ToString("0.00") + " onDeck " + CarrierRig.OnDeck(f.transform.position) + " vel0 " + f.HSpeed.ToString("0.00"));
                Next(4);
                break;
            case 4:   // idle 3 s, no input: must not drift on the deck
                if (Time.unscaledTime > logT - 0.5f) Debug.Log("FFDEMO ffu50 carrierexit [" + leg + "] idle t " + phT.ToString("0.0") + " deck-local " + lp.ToString("0.00") + " drift " + (lp - l0).magnitude.ToString("0.00") + " m push " + f.dbgPush.magnitude.ToString("0.000") + " carry " + f.dbgCarry.magnitude.ToString("0.000") + " ship v " + rig.trailer.rb.velocity.magnitude.ToString("0.0"));
                if (phT > 3f) { Debug.Log("FFDEMO ffu50 carrierexit [" + leg + "] IDLE DRIFT " + (lp - l0).ToString("0.00") + " (" + (lp - l0).magnitude.ToString("0.00") + " m in the deck frame, world " + (f.transform.position - w0).magnitude.ToString("0.0") + " m)"); dirI = 0; l0 = lp; w0 = f.transform.position; Next(5); }
                break;
            case 5:   // walk fwd / right / back / left 1.5 s each
                {
                    Vector3 want = dirI == 0 ? fw : dirI == 1 ? rr : dirI == 2 ? -fw : -rr;
                    if (leg == "water") { want = dirI == 0 ? Vector3.forward : dirI == 1 ? Vector3.right : dirI == 2 ? Vector3.back : Vector3.left; }
                    dirMove = want;
                    if (phT > 1.5f)
                    {
                        Vector3 d = leg == "water" ? f.transform.position - w0 : rt.TransformDirection(lp - l0);
                        d.y = 0f;
                        float along = Vector3.Dot(d, want), side = Vector3.Dot(d, Vector3.Cross(Vector3.up, want));
                        Debug.Log("FFDEMO ffu50 carrierexit [" + leg + "] WALK " + DirName[dirI] + ": moved " + along.ToString("0.00") + " m along, " + side.ToString("0.00") + " m sideways (" + (leg == "water" ? "world" : "deck") + " frame)");
                        l0 = lp; w0 = f.transform.position; dirI++; phT = 0f;
                        if (dirI >= 4)
                        {
                            dirMove = Vector3.zero;
                            if (leg == "parked")
                            {
                                leg = "moving";
                                f.SendTo(WorldId.Ranch, rig.DeckPoint(-0.5f, 0f) + Vector3.up * 0.2f, rt.eulerAngles.y);
                                f.EnterVehicle(rig.helm);
                                rig.helm.demoHook = i => { var o = new PIn(); o.gas = 0.9f; o.move = new Vector2(0.15f, 0f); return o; };
                                Next(2);
                            }
                            else if (leg == "moving")
                            {
                                leg = "water";
                                Vector3 wp = rt.position - rr * 9f; wp.y = Ripples.SurfaceY - 0.2f;
                                f.SendTo(WorldId.Ranch, wp, 0f);
                                l0 = lp; w0 = f.transform.position;
                                Next(6);
                            }
                            else { Done(); }
                        }
                    }
                    break;
                }
            case 6:   // in the water, no input 3 s
                if (phT > 3f) { Debug.Log("FFDEMO ffu50 carrierexit [water] IDLE DRIFT " + (f.transform.position - w0).magnitude.ToString("0.00") + " m (world) swimming push " + f.dbgPush.magnitude.ToString("0.000") + " carry " + f.dbgCarry.magnitude.ToString("0.000")); dirI = 0; w0 = f.transform.position; Next(5); }
                break;
        }
        if (Time.unscaledTime > logT) { logT = Time.unscaledTime + 1f; Debug.Log("FFDEMO ffu50 carrierexit ph " + ph + " leg " + leg + " t " + t.ToString("0.0") + " f " + f.transform.position.ToString("0.0") + " local " + lp.ToString("0.0") + " veh " + (f.vehicle != null ? f.vehicle.Title : "-") + " ship v " + rig.trailer.rb.velocity.magnitude.ToString("0.0") + " prompt '" + f.prompt + "'"); }
        if (ph >= 2) { Vector3 fp = f.transform.position; Cam(fp - fw * 7f + rr * 5f + Vector3.up * 5f, fp + Vector3.up * 0.6f, 0.25f); }
    }
}
