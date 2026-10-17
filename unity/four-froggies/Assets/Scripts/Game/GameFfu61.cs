using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// ffu61 hook-up + demos: builds the FROGGY BOW rack (World/Bow61.cs) once the ffu50 workbench exists, keeps the bows /
// the Apollo 11 site (World/Apollo61.cs) ticking. &ff61=old = the ffu60 bench (no rack, the old bench / quiver lines).
//  ?ffdemo=1&ffshot=hunt61[&h61=bow|pad|moon|touch|old] (logs "FFDEMO hunt61 ph N <name>", "HUNT61 PASS/FAIL ...", "FFDEMO hunt61 done"):
//   bow   : bench line with arrows in the quiver -> put 3 arrows in (toast) -> take a bow at the rack -> aimed MISS (arc) ->
//           pick the arrow up -> aimed HIT (lock-on) -> POOF -> drumstick -> pick up (bow on the back) -> cook -> eat ->
//           keyboard: hold right mouse = aim, left click = shoot, F = shoot (real InputSystem mouse / keyboard events)
//   pad   : P1 + a synthetic pad that joins as P2 (split screen): P2 LT = aim (only P2), RT = shoot (P2's arrow)
//   moon  : the Apollo 11 landing site: arrival toast, wide / flag / lander shots, read the plaque, collider checks
//   touch : (&ffjoin=touch) SHOOT + AIM buttons appear with a bow, AIM toggles the aim, SHOOT shoots
//   old   : (&ff61=old) the ffu60 bench dead end (before)
public partial class Game
{
    public Camera CamFor(Frog f)
    {
        if (f == null) return null;
        Slot s = SlotForFrog(f.id);
        if (s == null) return null;
        return sharedCam != null && sharedCam.enabled ? sharedCam : s.cam;
    }
    public CamRig RigFor(Frog f) { Slot s = f != null ? SlotForFrog(f.id) : null; return s != null ? s.rig : null; }
    public static string UrlP61(string k) { return UrlParam(k); }
    public Slot SlotOfPad61(Gamepad p) { return FindPad(p); }
    public void Ffu61Touch(Frog f) { Slot s = f != null ? SlotForFrog(f.id) : null; if (s == null) return; Kb.TestMobile = true; s.kind = InputKind.Touch; Debug.Log("FFDEMO hunt61: P1 switched to touch"); }
}

[DefaultExecutionOrder(101)]
public class Ffu61 : MonoBehaviour
{
    public static Ffu61 I;
    string shot = "", mode = "";
    float t0 = -1f, phT, logT;
    int ph; bool doneLogged, camOn;
    Frog f, f2; Animal cow; Gamepad pad;
    Vector3 camPos, camLook, walkTo, meatAt, arrowAt; bool walking, press, hAim, hFire, passRaw;
    int shots0, hits0, picked0, tries, pass, total;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        I = new GameObject("ffu61").AddComponent<Ffu61>();
        DontDestroyOnLoad(I.gameObject);
    }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.25f);
        if (!Bow61.Built && Fig50.I != null && Game.I != null)
        {
            Bow61.OldHints = Game.UrlP61("ff61") == "old";
            if (!Bow61.OldHints) { try { Bow61.Build(); } catch (System.Exception e) { Debug.LogError("BOW61 build: " + e); Bow61.Built = true; } }
            else { Bow61.Built = true; Debug.Log("BOW61: &ff61=old - no bow rack, ffu60 bench lines"); }
            shot = Ffu33.Url("ffdemo") != null && Ffu33.Url("ffshot") == "hunt61" ? "hunt61" : "";
            mode = Game.UrlP61("h61") ?? (Bow61.OldHints ? "old" : "bow");
            if (shot.Length > 0) Debug.Log("FFDEMO hunt61 demo " + mode);
        }
        Bow61.Upkeep();
        Apollo61.Tick(dt);
        if (shot.Length == 0 || Game.I == null || Game.I.state != Game.State.Play || Game.I.slots.Count == 0) return;
        if (t0 < 0f)
        {
            t0 = Time.time; f = Game.I.frogs[Game.I.slots[0].frog]; Frog.DtCap = 0.15f;
            Game.I.Demo33Hook(Hook);
            Game.SetHintsOn(true);
            if (mode == "touch") Game.I.Ffu61Touch(f);
            Debug.Log("FFDEMO hunt61 start " + mode + " p1 " + f.nick + " (" + Game.I.slots[0].kind + ") " + Bow61.Line + " | " + Stash50.Line);
        }
        float t = Time.time - t0;
        phT += Time.deltaTime;
        if (doneLogged) return;
        try
        {
            if (mode == "pad") Pad(t); else if (mode == "moon") Moon(t); else if (mode == "touch") Touch(t); else if (mode == "old") Old(t); else Bow(t);
        }
        catch (System.Exception e) { Debug.LogError("FFDEMO hunt61 error " + e); Done(); }
        if (Time.unscaledTime > logT)
        {
            logT = Time.unscaledTime + 1f;
            Debug.Log("FFDEMO hunt61 st ph " + ph + " t " + t.ToString("0.0") + " p1 " + f.transform.position.ToString("0.0") + " " + f.world + " held " + Fig50.HeldKind(f) + " aimK " + Bow61.AimK(f).ToString("0.00") + " lock " + Bow61.LockedOn(f) + " | prompt '" + f.prompt + "' | hint '" + Game.HintPrompt(f) + "' | toast '" + f.toast + "' | " + Bow61.Line + (f2 != null ? " | p2 " + f2.nick + " aimK " + Bow61.AimK(f2).ToString("0.00") : ""));
        }
    }

    void Next(int p, string name) { ph = p; phT = 0f; Debug.Log("FFDEMO hunt61 ph " + p + " " + name + " t " + (Time.time - t0).ToString("0.0") + " | " + Bow61.Line); }
    void Done()
    {
        if (doneLogged) return; doneLogged = true; hAim = false; camOn = false;
        Debug.Log("HUNT61 RESULT " + mode + " " + pass + "/" + total + " | " + Bow61.Line + " | " + Stash50.Line);
        Debug.Log("FFDEMO hunt61 done " + mode);
    }
    void Check(string what, bool ok, string detail = "") { total++; if (ok) pass++; Debug.Log("HUNT61 " + (ok ? "PASS" : "FAIL") + " " + what + (detail.Length > 0 ? " | " + detail : "")); }

    PIn Hook(PIn i)
    {
        var o = new PIn();
        if (passRaw) { o = i; }
        else
        {
            o.look = Vector2.zero;
            if (walking)
            {
                Vector3 d = walkTo - f.transform.position; d.y = 0f;
                if (d.magnitude > 0.9f) { float yaw = Game.I.slots[0].rig != null ? Game.I.slots[0].rig.yaw : 0f; Vector3 l = Quaternion.Euler(0f, -yaw, 0f) * d.normalized; o.move = new Vector2(l.x, l.z) * 0.8f; }
                else walking = false;
            }
            o.aimHeld = hAim;
        }
        if (press) { o.use = o.hop = true; press = false; }
        if (hFire) { o.bowFire = true; hFire = false; }
        return o;
    }

    CamRig Rig { get { return Game.I.slots.Count > 0 ? Game.I.slots[0].rig : null; } }
    static float YawTo(Vector3 a, Vector3 b) { Vector3 d = b - a; return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }
    void Face(Frog who, Vector3 target, float yawOff = 0f, float pitch = -100f)
    {
        CamRig r = Game.I.RigFor(who); if (r == null) return;
        Vector3 p = who.transform.position;
        r.SetYaw(YawTo(p, target) + yawOff);
        if (pitch > -99f) r.SetPitch(pitch);
        else
        {
            float d = new Vector2(target.x - p.x, target.z - p.z).magnitude;
            r.SetPitch(Mathf.Atan2((p.y + 1.45f) - (target.y + 0.85f), Mathf.Max(1f, d)) * Mathf.Rad2Deg);
        }
    }
    Vector3 Stand(Animal c, float dist, float side)
    {
        Vector3 cp = c.Pos, from = f.transform.position - cp; from.y = 0f;
        if (from.sqrMagnitude < 1f) from = Vector3.right;
        from = Quaternion.Euler(0f, side, 0f) * from.normalized;
        Vector3 p = cp + from * dist;
        return RobotNav.G(p.x, p.z);
    }
    void Freeze(Animal c) { if (c != null) c.transform.position = c.transform.position; }

    // ---------------- bow: the whole loop as a player ----------------
    void Bow(float t)
    {
        switch (ph)
        {
            case 0:
                if (t < 4f) break;
                if (Stash50.arrows < 6) { Stash50.arrows = 6; Stash50.Save(); Debug.Log("FFDEMO hunt61: demo top-up quiver -> 6"); }
                { Vector3 to = Fig50.BenchPos - Fig50.BenchFront; f.SendTo(WorldId.Ranch, Fig50.BenchFront, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg); }
                Next(1, "bench");
                break;
            case 1:
                if (phT < 1f) { Face(f, Fig50.BenchPos, 25f, 22f); break; }
                if (phT > 4f)
                {
                    Check("bench line names the next step when the quiver has arrows", f.prompt.Contains("grab a froggy bow") && f.prompt.Contains("PLAY > Hunt (bow)"), "prompt '" + f.prompt + "'");
                    Fig50.Hold(f, 3, 3); press = true; Next(2, "quiver");
                }
                break;
            case 2:
                if (phT > 3.5f)
                {
                    Check("putting arrows in the quiver toasts the next step", f.toast.Contains("froggy bow") && f.toast.Contains("Hunt (bow)"), "toast '" + f.toast + "' quiver " + Stash50.arrows);
                    Vector3 to = Bow61.RackPos - Bow61.RackFront; f.SendTo(WorldId.Ranch, Bow61.RackFront, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
                    Next(3, "rack");
                }
                break;
            case 3:
                if (phT < 1f) { Face(f, Bow61.RackPos, -20f, 20f); break; }
                if (phT > 3.5f && !Bow61.Has(f)) { Check("rack prompt offers the bow", f.prompt.Contains("take a froggy bow"), "prompt '" + f.prompt + "'"); press = true; }
                if (phT > 6f) { Check("P1 holds a froggy bow", Bow61.Has(f), "toast '" + f.toast + "'"); Next(4, "bow"); }
                break;
            case 4:
                if (phT < 3f) break;
                cow = Fig50.NearestCow(f.transform.position, 500f);
                if (cow == null) { Check("a cow to hunt", false); Done(); return; }
                { Vector3 sp = Stand(cow, 15f, 0f); f.SendTo(WorldId.Ranch, sp, YawTo(sp, cow.Pos)); }
                Next(5, "miss");
                break;
            case 5:
                Face(f, cow.Pos, 22f, 6f);
                hAim = phT > 1f;
                if (phT > 4.5f && tries == 0) { shots0 = Bow61.Shots; tries = 1; hFire = true; }
                if (phT > 6f && tries == 1 && Bow61.Shots == shots0) { tries = 2; hFire = true; }
                if (phT > 11f)
                {
                    Check("aimed shot (aim held, camera 22 deg off the cow) is fired + misses", Bow61.Shots > shots0 && Bow61.Hits == 0, "shots " + Bow61.Shots + " hits " + Bow61.Hits);
                    Check("the missed arrow stays stuck in the ground (pickable)", Arrow50.Stuck.Count > 0, "stuck " + Arrow50.Stuck.Count);
                    hAim = false;
                    if (Arrow50.Stuck.Count > 0) { arrowAt = Arrow50.Stuck[Arrow50.Stuck.Count - 1].transform.position; Vector3 sp = RobotNav.G(arrowAt.x + 1.0f, arrowAt.z); f.SendTo(WorldId.Ranch, sp, YawTo(sp, arrowAt)); }
                    picked0 = Bow61.Picked;
                    Next(6, "pickup");
                }
                break;
            case 6:
                if (phT < 1f) { Face(f, arrowAt, 30f, 30f); break; }
                if (phT > 3f && phT < 3.3f) { Debug.Log("FFDEMO hunt61: at the arrow, prompt '" + f.prompt + "' -> A"); press = true; }
                if (phT > 6f)
                {
                    Check("A picks the missed arrow up (back in the quiver)", Bow61.Picked > picked0, "picked " + Bow61.Picked + " quiver " + Stash50.arrows + " toast '" + f.toast + "'");
                    cow = Fig50.NearestCow(f.transform.position, 500f);
                    Vector3 sp = Stand(cow, 13f, 0f); f.SendTo(WorldId.Ranch, sp, YawTo(sp, cow.Pos));
                    hits0 = Bow61.Hits; tries = 0;
                    Next(7, "aim");
                }
                break;
            case 7:
                if (cow != null && cow.enabled) Face(f, cow.Pos);
                hAim = phT > 1f;
                if (phT > 5f && tries == 0 && (Bow61.LockedOn(f) || phT > 7f)) { Debug.Log("FFDEMO hunt61: lock-on " + Bow61.LockedOn(f) + " -> shoot"); Check("aim locks onto the cow (yellow arc)", Bow61.LockedOn(f)); tries = 1; hFire = true; }
                if (Bow61.Hits > hits0) { Check("aimed shot HITS the cow (tip-over + POOF)", true, "hits " + Bow61.Hits); hAim = false; Next(8, "poof"); break; }
                if (tries >= 1 && phT > 9f + tries * 4f) { if (tries < 3) { tries++; cow = Fig50.NearestCow(f.transform.position, 500f); hFire = true; } else { Check("aimed shot HITS the cow (tip-over + POOF)", false, "hits " + Bow61.Hits); hAim = false; Next(12, "kb"); } }
                break;
            case 8:
                if (Meat50.All.Count > 0 && phT > 3f)
                {
                    meatAt = Meat50.All[Meat50.All.Count - 1].transform.position;
                    Check("the POOF leaves a cartoon drumstick", true, "at " + meatAt.ToString("0.0"));
                    Vector3 sp = RobotNav.G(meatAt.x + 1.1f, meatAt.z); f.SendTo(WorldId.Ranch, sp, YawTo(sp, meatAt));
                    Next(9, "drum");
                }
                else if (phT > 15f) { Check("the POOF leaves a cartoon drumstick", false); Next(12, "kb"); }
                break;
            case 9:
                if (phT < 1f) { Face(f, meatAt, 30f, 28f); break; }
                if (phT > 2.5f && phT < 2.8f) press = true;
                if (phT > 5f)
                {
                    Check("picked up the drumstick with the bow on the back", Fig50.HeldKind(f) == 4 && Bow61.Has(f), "held " + Fig50.HeldKind(f));
                    if (Campfire.I == null) { Next(12, "kb"); break; }
                    Vector3 to = Campfire.I.Pos - Fig50.FireFront; f.SendTo(WorldId.Ranch, Fig50.FireFront, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
                    Next(10, "cook");
                }
                break;
            case 10:
                if (phT < 1f) { Face(f, Campfire.I.Pos, 35f, 25f); break; }
                if (phT > 3f && phT < 3.3f) { Debug.Log("FFDEMO hunt61: fire prompt '" + f.prompt + "' -> A (cook)"); press = true; }
                if (phT > 10f) { Debug.Log("FFDEMO hunt61: fire prompt '" + f.prompt + "' -> A (eat)"); press = true; Next(11, "eat"); }
                break;
            case 11:
                if (phT > 0.5f && phT < 0.8f && f.GetComponent<Eat50>() == null) press = true;
                if (phT > 5f) { Check("cooked + ate the drumstick (Yum toast)", f.toast.StartsWith("Yum") || f.GetComponent<Eat50>() != null, "toast '" + f.toast + "'"); Next(12, "kb"); }
                break;
            case 12:
                // keyboard + mouse: real InputSystem events on the page's mouse / keyboard
                if (Game.I.slots[0].kind != InputKind.Keyboard || Mouse.current == null) { Check("keyboard test (P1 is keyboard + a mouse exists)", false, Game.I.slots[0].kind + " mouse " + (Mouse.current != null)); Done(); return; }
                if (phT < 0.1f) { cow = Fig50.NearestCow(f.transform.position, 500f); if (cow != null) { Vector3 sp = Stand(cow, 13f, 25f); f.SendTo(WorldId.Ranch, sp, YawTo(sp, cow.Pos)); } passRaw = true; break; }
                if (cow != null && cow.enabled) Face(f, cow.Pos);
                if (phT > 1.5f && phT < 2f) { var ms = new MouseState().WithButton(MouseButton.Right); InputSystem.QueueStateEvent(Mouse.current, ms); }
                if (phT > 4f && tries < 10) { Check("keyboard: hold right mouse = aim", Bow61.AimK(f) > 0.5f, "aimK " + Bow61.AimK(f).ToString("0.00")); shots0 = Bow61.Shots; tries = 10; InputSystem.QueueStateEvent(Mouse.current, new MouseState().WithButton(MouseButton.Right).WithButton(MouseButton.Left)); }
                if (phT > 4.3f && tries == 10) { tries = 11; InputSystem.QueueStateEvent(Mouse.current, new MouseState().WithButton(MouseButton.Right)); }
                if (phT > 6f && tries == 11) { Check("keyboard: left click while aiming = shoot", Bow61.Shots > shots0, "shots " + Bow61.Shots); tries = 12; InputSystem.QueueStateEvent(Mouse.current, new MouseState()); }
                if (phT > 8f && tries == 12 && Keyboard.current != null) { shots0 = Bow61.Shots; tries = 13; InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.F)); }
                if (phT > 8.3f && tries == 13) { tries = 14; InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()); }
                if (phT > 11f && tries >= 13) { Check("keyboard: F = shoot (hip shot, aim assist)", Bow61.Shots > shots0, "shots " + Bow61.Shots + " hits " + Bow61.Hits); passRaw = false; Next(13, "end"); }
                if (phT > 14f) { passRaw = false; Next(13, "end"); }
                break;
            case 13:
                if (phT > 4f) Done();
                break;
        }
    }

    // ---------------- split screen: a synthetic pad joins as P2, LT aims, RT shoots ----------------
    void Pad(float t)
    {
        switch (ph)
        {
            case 0:
                if (t < 4f) break;
                if (Stash50.arrows < 8) { Stash50.arrows = 8; Stash50.Save(); }
                Bow61.DemoTake(f);
                pad = InputSystem.AddDevice<Gamepad>("FFTestPad61");
                Next(1, "join");
                break;
            case 1:
                if (phT > 1.5f && phT < 1.8f) InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
                if (phT > 2.0f && phT < 2.3f) InputSystem.QueueStateEvent(pad, new GamepadState());
                if (phT > 5f)
                {
                    Slot s2 = Game.I.SlotOfPad61(pad);
                    Check("the 2nd pad joins as P2 mid-game (split screen)", s2 != null && Game.I.slots.Count >= 2, "slots " + Game.I.slots.Count);
                    if (s2 == null) { Done(); return; }
                    f2 = Game.I.frogs[s2.frog];
                    cow = Fig50.NearestCow(Bow61.RackPos, 500f);
                    Vector3 a = Stand(cow, 14f, -12f), b = Stand(cow, 14f, 12f);
                    f.SendTo(WorldId.Ranch, a, YawTo(a, cow.Pos)); f2.SendTo(WorldId.Ranch, b, YawTo(b, cow.Pos));
                    Bow61.DemoTake(f2);
                    Next(2, "p2aim");
                }
                break;
            case 2:
                if (phT < 1f) break;
                Face(f, cow.Pos, 0f, 14f);
                if (cow.enabled) Face(f2, cow.Pos);
                { var st = new GamepadState(); st.leftTrigger = 1f; InputSystem.QueueStateEvent(pad, st); }
                if (phT > 4f)
                {
                    Check("P2 holds a froggy bow too", Bow61.Has(f2));
                    Check("P2's LT aims P2's bow only (P1 not aiming)", Bow61.AimK(f2) > 0.5f && Bow61.AimK(f) < 0.05f, "p2 " + Bow61.AimK(f2).ToString("0.00") + " p1 " + Bow61.AimK(f).ToString("0.00"));
                    shots0 = Bow61.Shots; hits0 = Bow61.Hits;
                    Next(3, "p2shot");
                }
                break;
            case 3:
                if (cow.enabled) Face(f2, cow.Pos);
                {
                    var st = new GamepadState(); st.leftTrigger = 1f;
                    if (phT > 1f && phT < 1.3f) st.rightTrigger = 1f;
                    InputSystem.QueueStateEvent(pad, st);
                }
                if (phT > 6f)
                {
                    Check("P2's RT shoots P2's arrow", Bow61.Shots > shots0 && Bow61.LastShooter == f2, "shots " + Bow61.Shots + " last " + (Bow61.LastShooter != null ? Bow61.LastShooter.nick : "-"));
                    Debug.Log("FFDEMO hunt61: P2 shot result hits " + (Bow61.Hits - hits0));
                    InputSystem.QueueStateEvent(pad, new GamepadState());
                    Next(4, "end");
                }
                break;
            case 4:
                if (phT > 3f) { Check("P2 released LT: aim off", Bow61.AimK(f2) < 0.05f); Done(); }
                break;
        }
    }

    // ---------------- the Apollo 11 landing site ----------------
    Vector3 SiteW(float x, float y, float z)
    {
        Vector3 w = Quaternion.Euler(0f, Apollo61.Yaw, 0f) * new Vector3(x, 0f, z);
        Vector2 c = Apollo61.C + new Vector2(w.x, w.z);
        return MoonWorld.L(c.x, MoonWorld.Y(c.x, c.y) + y, c.y);
    }
    void Moon(float t)
    {
        switch (ph)
        {
            case 0:
                if (t < 4f) break;
                if (Stash50.arrows < 4) { Stash50.arrows = 4; Stash50.Save(); }
                Bow61.DemoTake(f);
                MoonWorld.Land(f, 0);
                Next(1, "land");
                break;
            case 1:
                if (phT > 3f)
                {
                    Check("going to the Moon puts the froggy bow back on the rack", !Bow61.Has(f));
                    Check("Apollo 11 site built", Apollo61.Built, "center " + Apollo61.Center.ToString("0.0"));
                    Vector3 p = SiteW(-1.5f, 0f, Apollo61.PadR + 10f); f.SendTo(WorldId.Moon, p + Vector3.up * 0.3f, YawTo(p, Apollo61.Center));
                    camOn = true; Cam(SiteW(-10f, 3.2f, Apollo61.PadR + 13f), Apollo61.Center + Vector3.up * 1.8f, 1f);
                    Next(2, "site");
                }
                break;
            case 2:
                Cam(SiteW(-10f, 3.2f, Apollo61.PadR + 13f), Apollo61.Center + Vector3.up * 1.8f, 0.05f);
                if (phT > 6f) { Check("arrival toast 'Apollo 11 landing site - left here in 1969'", Apollo61.Greets > 0, "toast '" + f.toast + "'"); Next(3, "flag"); }
                break;
            case 3:
                Cam(Apollo61.FlagPos + (SiteW(-1f, 0f, Apollo61.PadR + 9f) - Apollo61.FlagPos).normalized * 3.6f + Vector3.up * 1.7f, Apollo61.FlagPos + Vector3.up * 1.6f, 0.1f);
                if (phT > 5f) Next(4, "ladder");
                break;
            case 4:
                Cam(SiteW(2.2f, 1.7f, Apollo61.PadR + 2.6f), SiteW(0f, 1.4f, Apollo61.PadR - 0.6f), 0.1f);
                if (phT > 5f) { Vector3 p = Apollo61.LadderFoot + (Apollo61.LadderFoot - Apollo61.Center).normalized * 0.6f; f.SendTo(WorldId.Moon, p + Vector3.up * 0.3f, YawTo(p, Apollo61.Center)); Next(5, "read"); }
                break;
            case 5:
                Cam(SiteW(3.5f, 2.6f, Apollo61.PadR + 6.5f), Apollo61.LadderFoot + Vector3.up * 1.8f, 0.1f);
                if (phT > 2.5f && phT < 2.8f) { Debug.Log("FFDEMO hunt61: at the ladder, prompt '" + f.prompt + "' -> A"); press = true; }
                if (phT > 6f) { Check("A at the ladder leg reads the plaque (panel + toast)", Apollo61.Reads > 0, "toast '" + f.toast + "'"); Next(6, "plaque"); }
                break;
            case 6:
                Cam(SiteW(2.2f, 2.4f, Apollo61.PadR + 5.2f), Apollo61.LadderFoot + Vector3.up * 2.2f, 0.1f);
                if (phT > 5f)
                {
                    // colliders: horizontal rays at buggy height through the stage, a leg and the flag pole
                    RaycastHit h;
                    Vector3 o = SiteW(-9f, 1.0f, 0f), d = (Apollo61.Center + Vector3.up * 1.0f - o).normalized;
                    bool body = Physics.Raycast(o, d, out h, 20f) && h.collider.transform.IsChildOf(GameObject.Find("Apollo 11 landing site").transform);
                    Check("collider: a buggy-height ray stops at the descent stage (nothing drives under / through)", body, body ? h.collider.name + " at " + h.distance.ToString("0.0") + " m" : "no hit");
                    Vector3 fo = Apollo61.FlagPos + Vector3.up * 1.0f + Vector3.right * 2f;
                    bool fl = Physics.Raycast(fo, Vector3.left, out h, 4f);
                    Check("collider: the flag pole", fl && h.distance < 2.2f, fl ? h.collider.name + " " + h.distance.ToString("0.00") : "no hit");
                    Next(7, "end");
                }
                break;
            case 7:
                Cam(SiteW(12f, 6f, 12f), Apollo61.Center + Vector3.up * 1.5f, 0.06f);
                if (phT > 5f) Done();
                break;
        }
    }

    // ---------------- touch: SHOOT + AIM ----------------
    void Touch(float t)
    {
        switch (ph)
        {
            case 0:
                if (t < 4f) break;
                if (Stash50.arrows < 4) { Stash50.arrows = 4; Stash50.Save(); }
                Bow61.DemoTake(f);
                cow = Fig50.NearestCow(f.transform.position, 500f);
                if (cow != null) { Vector3 sp = Stand(cow, 13f, 0f); f.SendTo(WorldId.Ranch, sp, YawTo(sp, cow.Pos)); }
                passRaw = true;
                Next(1, "touch");
                break;
            case 1:
                if (cow != null && cow.enabled) Face(f, cow.Pos);
                if (phT > 3f && tries == 0)
                {
                    tries = 1;
                    Check("touch: P1 is a touch player", Game.I.slots[0].kind == InputKind.Touch, Game.I.slots[0].kind.ToString());
                    Check("touch: SHOOT + AIM buttons with a bow in hand", TouchControls.want[1] == "SHOOT" && TouchControls.want[5] == "AIM" && TouchControls.bowMode, TouchControls.ModeKey);
                    TouchControls.TestAim = true;
                }
                if (phT > 6f && tries == 1) { tries = 2; Check("touch: AIM toggles the aim on", Bow61.AimK(f) > 0.5f, "aimK " + Bow61.AimK(f).ToString("0.00")); shots0 = Bow61.Shots; TouchControls.TestFire = true; Next(2, "tshot"); }
                break;
            case 2:
                if (cow != null && cow.enabled) Face(f, cow.Pos);
                if (phT > 3f && tries == 2) { tries = 3; Check("touch: SHOOT shoots", Bow61.Shots > shots0, "shots " + Bow61.Shots); TouchControls.TestAim = true; }
                if (phT > 6f) { Check("touch: AIM again toggles it off", Bow61.AimK(f) < 0.05f); passRaw = false; Done(); }
                break;
        }
    }

    // ---------------- before: the ffu60 bench dead end ----------------
    void Old(float t)
    {
        switch (ph)
        {
            case 0:
                if (t < 4f) break;
                if (Stash50.arrows < 6) { Stash50.arrows = 6; Stash50.Save(); }
                { Vector3 to = Fig50.BenchPos - Fig50.BenchFront; f.SendTo(WorldId.Ranch, Fig50.BenchFront, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg); }
                Next(1, "before");
                break;
            case 1:
                if (phT < 1f) { Face(f, Fig50.BenchPos, 25f, 22f); break; }
                if (phT > 8f) { Debug.Log("FFDEMO hunt61 before: prompt '" + f.prompt + "'"); Done(); }
                break;
        }
    }

    void Cam(Vector3 p, Vector3 look, float k)
    {
        if (!camOn) return;
        if (camPos == Vector3.zero || k >= 1f) { camPos = p; camLook = look; }
        camPos = Vector3.Lerp(camPos, p, k); camLook = Vector3.Lerp(camLook, look, Mathf.Max(k, 0.15f));
    }
    void LateUpdate()
    {
        if (!camOn || doneLogged || Game.I == null || Game.I.slots.Count == 0) return;
        Camera c = Game.I.slots[0].cam;
        if (c == null) return;
        c.transform.position = camPos;
        c.transform.LookAt(camLook);
    }
}
