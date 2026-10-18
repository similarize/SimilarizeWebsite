using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ffu33 James's VTOL jet (Bill: "James needs a jet that he has a hangar for, and he can go really fast around the earth in";
// then: it's a VTOL jet, plus a short runway). Kinematic, own flight model (no rigidbody forces = nothing to explode):
//  * HOVER (VTOL): swivelling rear nozzle + lift fan point down (fan doors open), downwash dust ring + heat shimmer;
//    stick = move / turn, up / down = climb; vertical lift-off from the pad in front of the hangar (or straight up out of the
//    hangar: the roof slides open); vertical landing anywhere flat on the ranch.
//  * TRANSITION: hover with the stick pushed forward past ~16 m/s (or press VTOL) -> the nozzles rotate aft over ~2 s while
//    the wings take over the lift; slowing below ~38 m/s in the air swings them back down (automatic hover).
//  * JET: throttle, pitch, roll, rudder; afterburner + contrails; short rolling take-off / landing on the runway; Mach 1 =
//    sonic boom + vapour cone; speed lines.
//  * HYPER ("around the earth"): climbs to ~1.6 km (sky darkens, the countryside curves away - Countryside / Worlds altitude
//    systems) and runs at ~Mach 3.5; at 9 km out the jet comes back round the far side of the world (lap counter, earth-lap
//    progress on the HUD).
//  * Crashes: boom + the jet (pilot still inside) is back on its pad 1.5 s later. Wrecks (ffu21 VehicleWreck) are handled the
//    same way. Only James flies it (if nobody plays James, any human player can); other froggies hop in as a passenger.
public class Jet : Vehicle
{
    public static Jet I;
    public enum Mode { Parked, Hover, Jet, Hyper }
    // ---- state ----
    public Mode mode = Mode.Parked;
    public bool vtol = true;          // wanted nozzle state (true = down)
    public float nz = 1f;             // nozzle angle 0 = aft (jet), 1 = down (hover)
    public float throttle;            // 0..1 (jet)
    public bool afterburner, onGround = true, gearDown = true;
    public float agl, groundY;
    public int laps; public float lapDist;
    public Vector3 vel;
    public float Mach { get { return vel.magnitude / 343f; } }
    Quaternion rot;
    Vector3 pos;
    float heading;                    // hover heading (deg)
    float transT = -99f;
    float crashT = -1f, toggleCool, boomCool, ringT, smokeT, autoMsgT;
    bool wasSuper;
    public Vector3 homePos; public float homeYaw;
    public float hyperAlt = 1600f;
    public const float HyperSpeed = 1200f, WrapR = 9000f;
    public static float DemoDt = 0.05f;   // probe: SwiftShader frames are slow
    public System.Func<Jet, JetIn> autopilot;   // demo
    // ---- input this frame ----
    public struct JetIn { public float pitch, roll, yaw, thr, climb, fwd, strafe; public bool burn, toggle, hyper; }
    JetIn ji;
    // ---- model ----
    Transform model, nozzle, fanDoorL, fanDoorR, fan, flame, gear, vapour, rollL, rollR, shimmer;
    Renderer flameR, vapourR;
    Material flameMat, vapourMat;
    TrailRenderer trailL, trailR, trailE;
    ParticleSystem speedLines;
    readonly List<Renderer> rends = new List<Renderer>();
    // ---- audio ----
    AudioSource aWhine, aRoar, aBurner, aFan;

    public override string[] TouchSet { get { return new[] { "OUT", "BURN", "VTOL", "UP", "DOWN" }; } }

    public static Jet Spawn(Vector3 p, float yaw)
    {
        var go = new GameObject("James's jet");
        go.transform.position = p;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        var j = go.AddComponent<Jet>();
        j.Title = "James's jet";
        j.EnterVerb = "fly James's VTOL jet";
        j.flyer = true;
        j.engineKind = 6;   // own audio (Vehicle.EngineSound skips kind 6)
        j.camDistance = 19f; j.camHeight = 4.5f;
        j.seatScale = 0.62f; j.showDriver = true;
        j.BuildModel();
        j.SetupBody(6000f, new Vector3(0f, 2f, -0.2f), new Vector3(2.3f, 2.3f, 12.8f), new Vector3(0f, 2f, 0f));
        j.ExtraBox(new Vector3(0f, 1.95f, -1.5f), new Vector3(9.2f, 0.4f, 3.6f));
        j.rb.isKinematic = true; j.rb.useGravity = false;
        j.homePos = p; j.homeYaw = yaw;
        j.pos = p; j.rot = go.transform.rotation; j.heading = yaw;
        I = j;
        if (!fogHooked) { fogHooked = true; U6Cam.PreCull += HyperFog; }
        return j;
    }

    // HYPER: thicker high-altitude haze for cameras near the jet so the far edge of the countryside melts into the sky
    static bool fogHooked;
    static void HyperFog(Camera c)
    {
        Jet j = I;
        if (j == null || c == null || j.mode != Mode.Hyper || !RenderSettings.fog) return;
        if ((c.transform.position - j.transform.position).sqrMagnitude > 600f * 600f) return;
        RenderSettings.fogDensity = Mathf.Max(RenderSettings.fogDensity, 1.7e-4f);
        RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, new Color(0.42f, 0.5f, 0.66f), 0.5f);
    }

    // ================= ownership / exit =================
    static bool HumanJamesPlaying()
    {
        if (Game.I == null) return false;
        foreach (Slot s in Game.I.slots)
        {
            if (s == null || s.frog < 0 || s.frog >= Game.I.frogs.Count) continue;
            Frog f = Game.I.frogs[s.frog];
            if (f != null && f.human && f.charId == 0) return true;
        }
        return false;
    }
    public override bool CanEnter(Frog f)
    {
        if (f == null || crashT >= 0f) return false;
        if (!f.human || f.netPuppet || f.robotPilot != null) return false;   // AI froggies / robots never take it
        if (f.charId == 0) return true;
        return !f.IsPet && !HumanJamesPlaying();
    }
    public override string DeniedLine
    {
        get
        {
            if (crashT >= 0f) return Title + " - getting fixed up...";
            return "James's jet - only James can fly it" + (HumanJamesPlaying() ? " (when he's flying, A / E next to it rides along)" : "");
        }
    }
    public override bool CanExit(Frog f)
    {
        if (agl < 2.6f && vel.magnitude < 7f && mode != Mode.Hyper) return true;
        if (!Frog.ChuteOld && agl > 6f && mode != Mode.Hyper) return true;   // ffu47: eject + paraglider, the jet flies itself home
        f.Toast(mode == Mode.Hyper ? "Leave HYPER first (B / Y), then you can eject" : "Land first (or hover low) to hop out", 1.6f);
        return false;
    }
    protected override bool OutOfWorld(Vector3 p) { return false; }

    public override void OnEnter()
    {
        base.OnEnter();
        if (mode == Mode.Parked) { mode = onGround && !vtol ? Mode.Jet : Mode.Hover; }
        Sfx.Play(Sfx.Click, 0.5f, 0.8f);
    }
    public override void OnExit()
    {
        base.OnExit();
        throttle = 0f; afterburner = false;
        if (mode == Mode.Hyper) mode = Mode.Jet;
        bool air = agl > 4f && !Frog.ChuteOld;
        if (air) { homing = true; homeT = 0f; Debug.Log("FFDEMO jet ejected at agl " + agl.ToString("0") + " - flying itself home"); }
        // passengers hop out with the pilot (ffu47: in the air they eject too and glide down)
        int k = 0;
        foreach (Frog p in Passengers())
        {
            p.LeavePassenger();
            if (air) { k++; p.BeginBail(Title, pos + rot * new Vector3((k % 2 == 0 ? 1f : -1f) * (2.5f + k), 3.5f, -3f * k), vel * 0.5f + Vector3.up * 8f, true); }
            else p.Teleport(ExitPoint());
        }
    }

    // ffu47: nobody aboard after an ejection - climb to a safe height, fly back over the VTOL pad, descend and park
    public bool homing; float homeT;
    void HomeTick(float dt)
    {
        homeT += dt;
        vtol = true; nz = Mathf.MoveTowards(nz, 1f, dt * 1.5f); throttle = 0f; afterburner = false; mode = Mode.Hover;
        Vector3 to = homePos - pos; to.y = 0f;
        float d = to.magnitude;
        float cruise = Mathf.Max(groundY + 40f, homePos.y + 30f);
        Vector3 want;
        if (d > 8f) want = to.normalized * Mathf.Min(32f, d * 0.6f + 4f) + Vector3.up * Mathf.Clamp((cruise - pos.y) * 0.5f, -6f, 10f);
        else want = to * 0.8f + Vector3.up * (pos.y - homePos.y > 3f ? -5f : -1.5f);
        vel = Vector3.MoveTowards(vel, want, 14f * dt);
        if (to.sqrMagnitude > 1f) heading = Mathf.MoveTowardsAngle(heading, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 50f * dt);
        rot = Quaternion.Slerp(rot, Quaternion.Euler(Mathf.Clamp(vel.magnitude * 0.25f, 0f, 8f), heading, 0f), Mathf.Min(1f, dt * 3f));
        if ((d < 2.5f && pos.y - homePos.y < 2.5f) || homeT > 90f) { homing = false; ResetToPad(); Debug.Log("FFDEMO jet home on its pad after " + homeT.ToString("0") + " s"); }
    }

    public List<Frog> Passengers()
    {
        var l = new List<Frog>();
        if (Game.I != null) foreach (Frog f in Game.I.frogs) if (f != null && f.passengerOf == this) l.Add(f);
        return l;
    }

    public override string HelpLine
    {
        get
        {
            InputKind k = driver != null ? driver.inputKind : InputKind.Keyboard;
            bool pad = k == InputKind.Gamepad, touch = k == InputKind.Touch;
            if (mode == Mode.Hyper)
                return pad ? "HYPER: L-stick steer / climb | B or Y: back to normal flight" : touch ? "HYPER: stick steer / climb | VTOL: back to normal flight" : "HYPER: arrows steer / climb | B or F: back to normal flight";
            if (vtol || nz > 0.5f)
                return pad ? "VTOL HOVER: L-stick move + turn | RT up, LT down | LB/RB slide | push forward fast or Y = jet mode | A out (landed)"
                     : touch ? "VTOL HOVER: stick move + turn | UP / DOWN | push forward fast or VTOL = jet mode | OUT (landed)"
                     : "VTOL HOVER: arrows move + turn | W up, S down | , . slide | push forward fast or F = jet mode | E out (landed)";
            return pad ? "JET: L-stick pitch + roll | RT/LT throttle | LB/RB rudder | X afterburner | Y hover | B HYPER (around the earth)"
                 : touch ? "JET: stick pitch + roll | UP/DOWN throttle | hold BURN afterburner (2 s high up = HYPER) | VTOL hover"
                 : "JET: arrows pitch + roll | W/S throttle | A/D rudder | SHIFT afterburner | F hover | B HYPER (around the earth)";
        }
    }

    // ================= input =================
    Slot SlotOf(Frog f)
    {
        if (Game.I == null || f == null) return null;
        foreach (Slot s in Game.I.slots) if (s != null && s.frog >= 0 && s.frog < Game.I.frogs.Count && Game.I.frogs[s.frog] == f) return s;
        return null;
    }
    static bool K(System.Func<Keyboard, bool> f) { var k = Keyboard.current; return k != null && f(k); }

    float burnHoldT;
    public override void Drive(PIn i, float camYaw, float dt)
    {
        base.Drive(i, camYaw, dt);
        var o = new JetIn();
        Slot s = SlotOf(driver);
        InputKind kind = driver != null ? driver.inputKind : InputKind.Keyboard;
        if (kind == InputKind.Keyboard && s != null && s.kind == InputKind.Keyboard)
        {
            float up = K(k => k.upArrowKey.isPressed) ? 1f : 0f, dn = K(k => k.downArrowKey.isPressed) ? 1f : 0f;
            float lf = K(k => k.leftArrowKey.isPressed) ? 1f : 0f, rt = K(k => k.rightArrowKey.isPressed) ? 1f : 0f;
            float w = K(k => k.wKey.isPressed) ? 1f : 0f, sk = K(k => k.sKey.isPressed) ? 1f : 0f;
            float a = K(k => k.aKey.isPressed) ? 1f : 0f, d = K(k => k.dKey.isPressed) ? 1f : 0f;
            float cm = K(k => k.commaKey.isPressed) ? 1f : 0f, pd = K(k => k.periodKey.isPressed) ? 1f : 0f;
            o.pitch = dn - up;            // down arrow = pull up
            o.roll = rt - lf;
            o.yaw = (d - a) + (pd - cm);
            o.thr = w - sk;
            o.climb = w - sk + (K(k => k.spaceKey.isPressed) ? 1f : 0f);
            o.fwd = up - dn; o.strafe = pd - cm;
            o.burn = K(k => k.leftShiftKey.isPressed || k.rightShiftKey.isPressed);
            o.toggle = i.land || K(k => k.fKey.wasPressedThisFrame);
            o.hyper = K(k => k.bKey.wasPressedThisFrame);
            // hover: arrows left / right turn, A / D slide too
            if (vtol || nz > 0.5f) { o.yaw = rt - lf; o.strafe = (d - a) + (pd - cm); o.roll = 0f; }
        }
        else if (kind == InputKind.Gamepad && s != null && s.pad != null)
        {
            Gamepad p = s.pad;
            o.pitch = -i.move.y; o.roll = i.move.x;
            o.fwd = i.move.y; o.strafe = (p.rightShoulder.isPressed ? 1f : 0f) - (p.leftShoulder.isPressed ? 1f : 0f);
            o.yaw = o.strafe;
            o.thr = i.gas - i.brake; o.climb = i.gas - i.brake;
            o.burn = p.buttonWest.isPressed;
            o.toggle = p.buttonNorth.wasPressedThisFrame;
            o.hyper = p.buttonEast.wasPressedThisFrame;
            if (vtol || nz > 0.5f) { o.yaw = i.move.x; o.roll = 0f; }
        }
        else
        {
            // touch (and anything else): stick, UP/DOWN, BURN (FIRE), VTOL (MSL); hold BURN 2 s high up = HYPER
            o.pitch = -i.move.y; o.roll = i.move.x; o.fwd = i.move.y;
            o.thr = i.climb; o.climb = i.climb;
            o.burn = i.fireHeld;
            o.toggle = i.alt || i.land;
            if (vtol || nz > 0.5f) { o.yaw = i.move.x; o.roll = 0f; }
            burnHoldT = o.burn && mode == Mode.Jet && agl > 150f ? burnHoldT + dt : 0f;
            if (burnHoldT > 2f) { o.hyper = true; burnHoldT = -99f; }
        }
        if (autopilot != null) o = autopilot(this);
        // one-shot presses latch until a physics step uses them (slow frames can skip FixedUpdate)
        o.toggle |= ji.toggle; o.hyper |= ji.hyper;
        ji = o;
        // chase camera: in fast flight the view eases in behind the nose unless the player is looking around
        if (s != null && s.rig != null && !i.lookHeld && i.look.sqrMagnitude < 0.0001f && vel.magnitude > 30f && !(vtol || nz > 0.5f))
        {
            float hdg = Mathf.Atan2(vel.x, vel.z) * Mathf.Rad2Deg;
            s.rig.SetYaw(Mathf.LerpAngle(s.rig.yaw, hdg, Mathf.Min(1f, dt * 2.2f)));
        }
        camDistance = mode == Mode.Hyper ? 30f : (vtol || nz > 0.5f) ? 17f : 21f;
    }

    // ================= ground =================
    public static bool OnRanch(Vector3 p) { return Mathf.Abs(p.x) < Layout.Half && Mathf.Abs(p.z) < Layout.Half; }
    float GroundAt(Vector3 p, out Vector3 n)
    {
        n = Vector3.up;
        if (!OnRanch(p)) return Countryside.Height(p.x, p.z, false);
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(p.x, p.y + 2.2f, p.z), Vector3.down, out hit, 4000f, GroundMask, QueryTriggerInteraction.Ignore))
        {
            n = hit.normal;
            float y = hit.point.y;
            if (Layout.InPond(p.x, p.z)) y = Mathf.Max(y, Layout.WaterY);
            return y;
        }
        return Ranch.GY(p.x, p.z);
    }

    // ================= flight =================
    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        float dt = Mathf.Min(Time.fixedDeltaTime, DemoDt);
        if (crashT >= 0f) { CrashTick(Time.fixedDeltaTime); return; }
        if (Wrecked) { if (!onGround) Crash("shot down"); return; }
        if (driver == null)
        {
            // nobody aboard: settle (hover drops gently onto the ground; a rolling jet brakes)
            ji = new JetIn();
            throttle = 0f; afterburner = false;
            if (!onGround) { vel.x = Mathf.MoveTowards(vel.x, 0f, 4f * dt); vel.z = Mathf.MoveTowards(vel.z, 0f, 4f * dt); vel.y = Mathf.MoveTowards(vel.y, -3f, 6f * dt); vtol = true; }
            else { vel = Vector3.MoveTowards(vel, Vector3.zero, 10f * dt); if (vel.sqrMagnitude < 0.01f) mode = Mode.Parked; }
        }
        if (driver != null) homing = false;
        if (driver == null && homing) { HomeTick(dt); Integrate(dt); Effects(dt); Audio(dt); return; }
        toggleCool -= dt; boomCool -= dt;
        if (ji.toggle && toggleCool <= 0f)
        {
            toggleCool = 0.35f;
            if (mode == Mode.Hyper) { mode = Mode.Jet; Say("Normal flight"); }
            else { vtol = !vtol; transT = Time.time; Say(vtol ? "VTOL - nozzles down, hover" : "Jet mode - nozzles aft"); if (!vtol) throttle = Mathf.Max(throttle, 0.65f); if (vtol) heading = rot.eulerAngles.y; }
        }
        if (ji.hyper && toggleCool <= 0f)
        {
            toggleCool = 0.35f;
            if (mode == Mode.Hyper) { mode = Mode.Jet; Say("Normal flight"); }
            else if (!vtol && nz < 0.4f && !onGround && agl > 40f) { mode = Mode.Hyper; hyperAlt = Mathf.Clamp(pos.y + 900f, 1400f, 2400f); Say("HYPER! Climbing to " + Mathf.RoundToInt(hyperAlt) + " m - let's go around the earth"); }
            else Say(onGround || agl <= 40f ? "Get up high in jet mode first, then HYPER" : "Switch to jet mode for HYPER");
        }
        ji.toggle = false; ji.hyper = false;
        nz = Mathf.MoveTowards(nz, vtol ? 1f : 0f, dt * 0.48f);
        if (mode == Mode.Hyper) Hyper(dt);
        else if (vtol || nz > 0.55f) Hover(dt);
        else JetFlight(dt);
        Integrate(dt);
        Effects(dt);
        Audio(dt);
    }

    void Hover(float dt)
    {
        if (driver != null) mode = Mode.Hover;
        afterburner = false;
        float fwdIn = ji.fwd;
        heading += ji.yaw * 62f * dt;
        Quaternion hq = Quaternion.Euler(0f, heading, 0f);
        Vector3 f = hq * Vector3.forward, r = hq * Vector3.right;
        Vector3 wish = f * fwdIn * 22f + r * ji.strafe * 9f;
        float wishV = ji.climb * 8f;
        if (driver == null) wishV = -3f;
        if (onGround && wishV <= 0.05f) { wish = f * fwdIn * 6f; }   // taxi on the wheels
        Vector3 hv = Vector3.MoveTowards(new Vector3(vel.x, 0f, vel.z), wish, 9f * dt);
        float vy = Mathf.MoveTowards(vel.y, wishV, 10f * dt);
        if (Mathf.Abs(ji.climb) < 0.05f && !onGround && driver != null) vy = Mathf.MoveTowards(vel.y, 0f, 8f * dt);
        if (!OnRanch(pos) && agl < 60f && vy < 0f) { vy = Mathf.Max(vy, 0f); AutoMsg("No landing out in the countryside - fly back to the ranch"); }
        vel = new Vector3(hv.x, vy, hv.z);
        // tilt with the acceleration (nose down going forward)
        Vector3 local = Quaternion.Inverse(hq) * vel;
        float tiltK = onGround ? 0f : 1f;
        float pitchVis = Mathf.Clamp(local.z * 0.45f, -10f, 10f) * tiltK, rollVis = Mathf.Clamp(-local.x * 1.2f - ji.yaw * 6f, -14f, 14f) * tiltK;
        rot = Quaternion.Slerp(rot, Quaternion.Euler(pitchVis, heading, rollVis), Mathf.Min(1f, dt * 4f));
        // transition: pushing forward fast in the air swings the nozzles aft
        if (vtol && fwdIn > 0.75f && local.z > 16f && agl > 4f && driver != null)
        {
            vtol = false; throttle = Mathf.Max(throttle, 0.72f); transT = Time.time;
            Say("TRANSITION - nozzles rotating aft, wings taking the lift");
        }
        if (!vtol)
        {
            // mid-transition: keep the forward speed building
            vel += (rot * Vector3.forward) * (throttle * 20f) * dt;
        }
    }

    void JetFlight(float dt)
    {
        if (driver != null) mode = Mode.Jet;
        throttle = Mathf.Clamp01(throttle + ji.thr * 0.55f * dt);
        afterburner = ji.burn && driver != null;
        if (afterburner) throttle = Mathf.Max(throttle, 0.9f);
        float s = vel.magnitude;
        Vector3 fwd = rot * Vector3.forward;
        float thrust = throttle * 24f + (afterburner ? 20f : 0f);
        float drag = 2.9e-4f * s * s + (onGround ? 0.35f : 0f);
        if (onGround)
        {
            // rolling on the runway / grass: throttle, wheel brake (throttle down at idle), rudder / stick steer, rotate at 52+ m/s
            float brake = ji.thr < -0.3f && throttle < 0.05f ? 9f : 0f;
            float gs = Vector3.Dot(vel, fwd);
            gs += (thrust - drag - brake * Mathf.Sign(gs)) * dt;
            if (Mathf.Abs(gs) < 0.2f && thrust < 0.1f) gs = 0f;
            float steer = (ji.yaw + ji.roll) * Mathf.Lerp(40f, 6f, Mathf.Clamp01(gs / 50f));
            Vector3 e = rot.eulerAngles;
            float pitch = Mathf.DeltaAngle(0f, e.x);
            if (gs > 48f) pitch = Mathf.MoveTowards(pitch, -ji.pitch * 12f, 25f * dt);
            else pitch = Mathf.MoveTowards(pitch, 0f, 20f * dt);
            rot = Quaternion.Euler(pitch, e.y + steer * dt, 0f);
            vel = (Quaternion.Euler(0f, rot.eulerAngles.y, 0f) * Vector3.forward) * gs;
            if (pitch < -5f && gs > 52f) { vel += Vector3.up * 3f; onGround = false; Say("Airborne!"); }
            return;
        }
        // in the air: body-rate controls + a coordinated turn from the bank
        float pr = ji.pitch * 58f, rr = ji.roll * 150f, yr = ji.yaw * 22f;
        rot = rot * Quaternion.Euler(-pr * dt, yr * dt, -rr * dt);
        Vector3 e2 = rot.eulerAngles;
        float bank = Mathf.DeltaAngle(0f, e2.z);
        if (Mathf.Abs(ji.roll) < 0.1f) bank = Mathf.MoveTowards(bank, 0f, 45f * dt);
        float turn = 9.81f * Mathf.Tan(Mathf.Clamp(-bank, -78f, 78f) * Mathf.Deg2Rad) / Mathf.Max(s, 45f) * Mathf.Rad2Deg;
        rot = Quaternion.Euler(e2.x, e2.y + turn * dt, bank);
        fwd = rot * Vector3.forward;
        // speed along the path: thrust - drag - gravity along the climb
        s += (thrust * (1f - nz) - drag - 9.81f * fwd.y) * dt;
        s = Mathf.Max(s, 0f);
        Vector3 dir = s > 0.5f ? vel / Mathf.Max(0.01f, vel.magnitude) : fwd;
        dir = Vector3.Slerp(dir, fwd, Mathf.Min(1f, dt * Mathf.Clamp01(s / 55f) * 3.2f));
        vel = dir * s;
        // lift: wings (speed^2) + nozzles still pointing down mid-transition; short of it the jet sinks
        float lift = Mathf.Clamp01((s / 62f) * (s / 62f)) * Mathf.Abs(Mathf.Cos(bank * Mathf.Deg2Rad)) + nz;
        vel.y -= 9.81f * Mathf.Clamp01(1f - lift) * dt;
        if (nz > 0.05f) vel += Vector3.up * (nz * 6f * Mathf.Clamp01(-vel.y)) * dt;
        // too slow for the wings -> nozzles swing down (automatic hover)
        if (s < 30f && nz < 0.05f && agl > 3f && Time.time - transT > 4f)
        {
            vtol = true; heading = rot.eulerAngles.y;
            Say("Slow - VTOL hover");
        }
        // far out over the countryside: bank gently back towards the ranch
        float dist = new Vector2(pos.x, pos.z).magnitude;
        if (dist > 14000f)
        {
            float want = Mathf.Atan2(-pos.x, -pos.z) * Mathf.Rad2Deg;
            float yaw = Mathf.MoveTowardsAngle(rot.eulerAngles.y, want, 25f * dt);
            rot = Quaternion.Euler(rot.eulerAngles.x, yaw, rot.eulerAngles.z);
            AutoMsg("Turning back - the ranch is this way");
        }
    }

    void Hyper(float dt)
    {
        afterburner = true; throttle = 1f; nz = 0f; vtol = false;
        float s = vel.magnitude;
        s = Mathf.MoveTowards(s, HyperSpeed, (s < 343f ? 70f : 95f) * dt);
        hyperAlt = Mathf.Clamp(hyperAlt - ji.pitch * 260f * dt, 700f, 3000f);
        float vy = Mathf.Clamp((hyperAlt - pos.y) * 0.45f, -160f, 260f);
        float hs = Mathf.Sqrt(Mathf.Max(1f, s * s - vy * vy));
        Vector3 e = rot.eulerAngles;
        float yaw = e.y + (ji.roll * 26f + ji.yaw * 10f) * dt;
        float bank = Mathf.MoveTowards(Mathf.DeltaAngle(0f, e.z), -ji.roll * 50f, 70f * dt);
        float pitch = -Mathf.Atan2(vy, hs) * Mathf.Rad2Deg;
        rot = Quaternion.Euler(pitch, yaw, bank);
        Vector3 h = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        vel = h * hs + Vector3.up * vy;
        lapDist += hs * dt;
        // around the world: at WrapR out the jet comes back round the other side of the planet
        Vector2 xz = new Vector2(pos.x, pos.z);
        if (xz.magnitude > WrapR && Vector2.Dot(xz, new Vector2(vel.x, vel.z)) > 0f)
        {
            Vector2 nxz = -xz.normalized * (WrapR - 400f);
            pos = new Vector3(nxz.x, pos.y, nxz.y);
            transform.position = pos; rb.position = pos;
            if (trailL != null) { trailL.Clear(); trailR.Clear(); trailE.Clear(); }
            laps++;
            Say(laps == 1 ? "AROUND THE WORLD! The ranch is coming up ahead" : "AROUND THE WORLD x" + laps + "!");
            FX.Sparkle(pos + h * 30f, Color.white, 40);
            foreach (Slot sl in RiderSlots()) if (sl.rig != null) { sl.rig.Snap(); sl.rig.AddShake(0.5f); }
            Debug.Log("FFDEMO jet lap " + laps + " t=" + Time.realtimeSinceStartup.ToString("0.0"));
        }
    }

    public float LapFrac { get { return Mathf.Repeat(lapDist / (2f * WrapR), 1f); } }

    void Integrate(float dt)
    {
        Vector3 step = vel * dt;
        // obstacles (ranch buildings, trees, hangar walls): slow = bump and slide, fast = crash
        if (OnRanch(pos) && step.sqrMagnitude > 1e-6f && pos.y < 400f)
        {
            RaycastHit hit;
            Vector3 c = pos + rot * new Vector3(0f, 2f, 0f);
            if (Physics.SphereCast(c, 1.1f, step.normalized, out hit, step.magnitude + 0.4f, GroundMask, QueryTriggerInteraction.Ignore) && hit.normal.y < 0.6f)
            {
                float into = -Vector3.Dot(vel, hit.normal);
                if (into > 21f) { Crash("hit " + hit.collider.name); return; }
                vel += hit.normal * Mathf.Max(0f, into) * 1.1f;
                step = vel * dt;
            }
        }
        pos += step;
        if (onGround) pos += CarrierRig.Carry(pos, dt);   // ffu48: parked on Spotty's carrier deck - ride along
        // inside the hangar with the roof (still) shut: stay under the ceiling (the roof slides open for a VTOL lift-off)
        Vector2 hc = JetLayout.HangarC;
        if (JetBase.I != null && Mathf.Abs(pos.x - hc.x) < JetLayout.HangarW * 0.5f && Mathf.Abs(pos.z - hc.y) < JetLayout.HangarD * 0.5f && pos.y < JetBase.FloorY + JetLayout.HangarH + 1f && JetBase.I.RoofK < 0.97f)
        {
            float ceil = JetBase.FloorY + JetLayout.HangarH - 3.4f;
            if (pos.y > ceil) { pos.y = ceil; if (vel.y > 0f) vel.y = 0f; if (Time.time - autoMsgT > 3f && driver != null) AutoMsg("Roof opening..."); }
        }
        Vector3 n;
        groundY = GroundAt(pos, out n);
        agl = pos.y - groundY;
        if (agl <= 0.02f)
        {
            bool hover = vtol || nz > 0.55f;
            float vy = vel.y;
            Vector3 e = rot.eulerAngles;
            float pitch = Mathf.DeltaAngle(0f, e.x), bank = Mathf.DeltaAngle(0f, e.z);
            if (!onGround)
            {
                bool soft = hover ? vy > -12f : (vy > -9f && Mathf.Abs(bank) < 30f && pitch < 14f && pitch > -22f);
                if (mode == Mode.Hyper || !soft || !OnRanch(pos)) { Crash(mode == Mode.Hyper ? "too low" : "hard landing"); return; }
                if (n.y < 0.82f && hover) { pos.y = groundY + 0.05f; vel.y = 1.5f; AutoMsg("Too steep to land here"); }
                else
                {
                    onGround = true;
                    Sfx.PlayAt(Sfx.Land != null ? Sfx.Land : Sfx.Thud, pos, 0.8f, 80f, 0.8f);
                    FX.Dust(pos, 1f);
                    if (!hover) Say("Touchdown!");
                    heading = e.y;
                }
            }
            pos.y = groundY;
            agl = 0f;
            if (vel.y < 0f) vel.y = 0f;
            rot = Quaternion.Euler(Mathf.Min(0f, Mathf.DeltaAngle(0f, rot.eulerAngles.x)), rot.eulerAngles.y, 0f);
        }
        else if (onGround && agl > 0.25f) onGround = false;
        if (onGround && (vtol || nz > 0.55f) && vel.y > 0.1f) onGround = false;   // lift-off
        if (onGround && (vtol || nz > 0.55f)) rot = Quaternion.Euler(0f, heading, 0f);
        if (onGround && driver == null && vel.sqrMagnitude < 0.01f) rot = Quaternion.Euler(0f, rot.eulerAngles.y, 0f);
        kinVel = vel;
        rb.MovePosition(pos);
        rb.MoveRotation(rot);
        // sonic boom
        bool sup = vel.magnitude > 343f;
        if (sup && !wasSuper && boomCool <= 0f)
        {
            boomCool = 3f;
            Sfx.Play(Synth33.Boom, Heard() ? 1f : 0.6f * HeardK());
            vapourT = 0.9f;
            foreach (Slot sl in RiderSlots()) if (sl.rig != null) sl.rig.AddShake(0.45f);
            Say("MACH 1 - sonic boom!");
            Debug.Log("FFDEMO jet sonic boom t=" + Time.realtimeSinceStartup.ToString("0.0"));
        }
        wasSuper = sup;
    }

    // ================= crash + recovery =================
    void Crash(string why)
    {
        if (crashT >= 0f) return;
        crashT = 0f;
        Vector3 p = pos + Vector3.up * 2f;
        FX.Boom(p, 2.6f); FX.Boom(p + Random.insideUnitSphere * 3f, 1.8f);
        FX.Smoke(p, 3f, new Color(0.2f, 0.2f, 0.2f, 0.7f));
        Sfx.PlayAt(Sfx.Boom, p, 1f, 300f, 0.7f);
        if (Heard()) Sfx.Play(Sfx.Boom, 0.8f, 0.9f);
        if (OnRanch(p)) { try { Boom.At(p, 7f, 1.2f, 18f, this); } catch (System.Exception e) { Debug.Log("Jet: boom failed " + e.Message); } }
        foreach (Slot sl in RiderSlots()) if (sl.rig != null) sl.rig.AddShake(1f);
        foreach (Renderer r in rends) if (r != null) r.enabled = false;
        if (trailL != null) { trailL.emitting = trailR.emitting = trailE.emitting = false; }
        if (driver != null) driver.Toast("Crashed! You're fine - the jet's back on its pad in a moment", 3.5f);
        Debug.Log("FFDEMO jet crash (" + why + ") at " + pos.ToString("0") + " t=" + Time.realtimeSinceStartup.ToString("0.0"));
        vel = Vector3.zero; kinVel = Vector3.zero;
    }

    void CrashTick(float dt)
    {
        crashT += dt;
        kinVel = Vector3.zero;
        if (crashT < 1.5f) return;
        crashT = -1f;
        ResetToPad();
    }

    public void ResetToPad()
    {
        pos = homePos; rot = Quaternion.Euler(0f, homeYaw, 0f); heading = homeYaw;
        vel = Vector3.zero; kinVel = Vector3.zero;
        vtol = true; nz = 1f; throttle = 0f; afterburner = false; onGround = true; mode = driver != null ? Mode.Hover : Mode.Parked;
        laps = 0; lapDist = 0f;
        transform.position = pos; transform.rotation = rot;
        rb.position = pos; rb.rotation = rot;
        foreach (Renderer r in rends) if (r != null) r.enabled = r != flameR && r != vapourR && (shimmer == null || !r.transform.IsChildOf(shimmer));
        if (trailL != null) { trailL.Clear(); trailR.Clear(); trailE.Clear(); }
        foreach (Slot sl in RiderSlots()) if (sl.rig != null) sl.rig.Snap();
        if (Wrecked) { /* VehicleWreck runs its own timer; it ends with ResetHome */ }
    }

    // ffu47 demo: hovering at p, heading yaw, drifting forward
    public void DemoHover(Vector3 p, float yawDeg, float fwd)
    {
        pos = p; heading = yawDeg; rot = Quaternion.Euler(0f, yawDeg, 0f);
        vel = rot * Vector3.forward * fwd; kinVel = vel;
        vtol = true; nz = 1f; throttle = 0f; onGround = false; gearDown = false; mode = Mode.Hover; homing = false;
        transform.position = pos; transform.rotation = rot; rb.position = pos; rb.rotation = rot;
    }

    // demo: start in the air in jet mode (jetat=hyper)
    public void DemoAir(Vector3 p, float speed)
    {
        pos = p; rot = Quaternion.Euler(-4f, homeYaw, 0f); heading = homeYaw;
        vel = rot * Vector3.forward * speed; kinVel = vel;
        vtol = false; nz = 0f; throttle = 1f; onGround = false; mode = Mode.Jet;
        transform.position = pos; transform.rotation = rot; rb.position = pos; rb.rotation = rot;
    }

    // ================= helpers =================
    List<Slot> RiderSlots()
    {
        var l = new List<Slot>();
        if (Game.I == null) return l;
        foreach (Slot s in Game.I.slots)
        {
            if (s == null || s.frog < 0 || s.frog >= Game.I.frogs.Count) continue;
            Frog f = Game.I.frogs[s.frog];
            if (f != null && (f.vehicle == this || f.passengerOf == this)) l.Add(s);
        }
        return l;
    }
    bool Heard()
    {
        if (driver != null && driver.human) return true;
        foreach (Frog p in Passengers()) if (p.human) return true;
        return false;
    }
    float HeardK() { float k = Mathf.Clamp01(1f - Sfx.Near(pos) / 350f); return k * k; }

    public void Say(string s)
    {
        if (driver != null) driver.Toast(s, 2.6f);
        foreach (Frog p in Passengers()) p.Toast(s, 2.6f);
        Debug.Log("FFDEMO jet say: " + s + " t=" + Time.realtimeSinceStartup.ToString("0.0"));
    }
    void AutoMsg(string s) { if (Time.time - autoMsgT > 4f) { autoMsgT = Time.time; Say(s); } }

    // ================= effects =================
    float vapourT, gearK = 1f;
    void Effects(float dt)
    {
        float t = Time.time;
        bool on = driver != null && crashT < 0f;
        // nozzle + lift fan + doors
        if (nozzle != null) nozzle.localRotation = Quaternion.Euler(-90f * nz, 0f, 0f);
        float doors = Mathf.Clamp01(nz * 1.4f - 0.2f);
        if (fanDoorL != null) { fanDoorL.localRotation = Quaternion.Euler(0f, 0f, 100f * doors); fanDoorR.localRotation = Quaternion.Euler(0f, 0f, -100f * doors); }
        if (fan != null && on) fan.Rotate(0f, 1500f * dt * Mathf.Max(0.2f, nz), 0f, Space.Self);
        if (rollL != null) { rollL.gameObject.SetActive(nz > 0.6f && on); rollR.gameObject.SetActive(nz > 0.6f && on); }
        // gear: down on the ground and low + slow
        gearDown = onGround || (agl < 40f && vel.magnitude < 110f);
        gearK = Mathf.MoveTowards(gearK, gearDown ? 1f : 0f, dt * 1.5f);
        if (gear != null) { gear.localScale = Vector3.one * Mathf.Lerp(0.05f, 1f, gearK); gear.localPosition = Vector3.up * 1.6f * (1f - gearK); gear.gameObject.SetActive(gearK > 0.02f); }
        // flame: forward thrust aft, hover thrust down (shorter, bluer)
        float power = on ? (nz > 0.5f ? 0.35f + Mathf.Abs(ji.climb) * 0.25f : throttle * 0.7f + (afterburner ? 0.9f : 0f)) : 0f;
        if (flame != null)
        {
            bool fv = power > 0.05f;
            if (flameR.enabled != fv) flameR.enabled = fv;
            float fl = (0.6f + power * 2.6f) * (0.92f + 0.08f * Mathf.Sin(t * 47f));
            flame.localScale = new Vector3(0.9f + power * 0.25f, 0.9f + power * 0.25f, fl);
            Color fc = afterburner ? new Color(1f, 0.55f, 0.18f) * 2.6f : new Color(0.45f, 0.65f, 1f) * (1.6f + power);
            flameMat.color = fc;
        }
        // downwash: dust ring + heat shimmer under the nozzles while hovering low
        bool wash = on && nz > 0.5f && agl < 14f;
        if (shimmer != null)
        {
            shimmer.gameObject.SetActive(wash);
            if (wash)
            {
                shimmer.position = new Vector3(pos.x, groundY + Mathf.Min(agl, 3.5f) * 0.5f + 0.3f, pos.z);
                shimmer.rotation = Quaternion.Euler(0f, t * 40f, 0f);
                float k = 1f - agl / 14f;
                shimmer.localScale = new Vector3(1f + Mathf.Sin(t * 31f) * 0.05f, Mathf.Max(0.3f, Mathf.Min(agl, 3.5f)) * (1f + Mathf.Sin(t * 23f) * 0.08f), 1f) * (0.8f + k * 0.4f);
            }
        }
        if (wash)
        {
            ringT -= dt;
            float k = 1f - agl / 14f;
            if (ringT <= 0f)
            {
                ringT = 0.22f;
                Vector3 gp = new Vector3(pos.x, groundY + 0.2f, pos.z);
                bool water = OnRanch(pos) && Layout.InPond(pos.x, pos.z);
                FX.Ring(gp, 2.5f + k * 2f, Look.Mobile ? 8 : 14, 7f + k * 9f, water ? new Color(0.85f, 0.92f, 1f, 0.55f) : new Color(0.66f, 0.58f, 0.45f, 0.45f * k + 0.1f));
                if (water) FX.Splash(gp, 0.6f * k);
            }
            if (Random.value < 0.5f) FX.Dust(new Vector3(pos.x, groundY, pos.z) + Random.insideUnitSphere * 3f, 0.4f * k);
        }
        // contrails at the wingtips (fast or high), engine smoke trail on afterburner
        bool con = on && (vel.magnitude > 140f || (pos.y > 350f && vel.magnitude > 60f));
        if (trailL != null) { trailL.emitting = con; trailR.emitting = con; trailE.emitting = on && (afterburner || mode == Mode.Hyper); }
        // vapour cone at Mach 1
        vapourT -= dt;
        if (vapour != null)
        {
            bool vv = vapourT > 0f && on;
            if (vapourR.enabled != vv) vapourR.enabled = vv;
            if (vv) { float a = Mathf.Clamp01(vapourT / 0.9f); vapourMat.color = new Color(1f, 1f, 1f, 0.55f * a); vapour.localScale = Vector3.one * (1f + (1f - a) * 0.4f); }
        }
        // speed lines (only seen by the riders: they sit right around the camera)
        if (speedLines != null)
        {
            var em = speedLines.emission;
            float sk = Mathf.Clamp01((vel.magnitude - 120f) / 500f);
            em.rateOverTime = on ? sk * (Look.Mobile ? 40f : 90f) : 0f;
        }
        // wing-root smoke puff while rolling fast on the runway
        if (onGround && vel.magnitude > 20f && on) { smokeT -= dt; if (smokeT < 0f) { smokeT = 0.1f; FX.Dust(pos, 0.25f); } }
    }

    // ================= audio =================
    void Audio(float dt)
    {
        if (aWhine == null)
        {
            if (!Heard() && HeardK() < 0.02f) return;
            aWhine = Sfx.Loop(gameObject, Synth33.Whine); aRoar = Sfx.Loop(gameObject, Synth33.Roar);
            aBurner = Sfx.Loop(gameObject, Synth33.Burner); aFan = Sfx.Loop(gameObject, Synth33.Fan);
            foreach (var a in new[] { aWhine, aRoar, aBurner, aFan }) a.Play();
        }
        bool on = driver != null && crashT < 0f;
        float k = Heard() ? 1f : HeardK();
        float s = vel.magnitude;
        float spool = on ? (nz > 0.5f ? 0.55f + Mathf.Abs(ji.climb) * 0.25f : 0.35f + throttle * 0.6f) : 0f;
        aWhine.volume = Mathf.MoveTowards(aWhine.volume, spool * 0.32f * k, dt * 0.8f);
        aWhine.pitch = 0.65f + spool * 0.55f;
        aRoar.volume = Mathf.MoveTowards(aRoar.volume, (on ? 0.25f + throttle * 0.45f : 0f) * k * (s > 343f && Heard() ? 0.6f : 1f), dt * 1.2f);
        aRoar.pitch = 0.75f + throttle * 0.35f;
        aBurner.volume = Mathf.MoveTowards(aBurner.volume, (on && (afterburner || mode == Mode.Hyper) ? 0.7f : 0f) * k, dt * 2f);
        aBurner.pitch = 0.9f + Mathf.Clamp01(s / 1200f) * 0.25f;
        aFan.volume = Mathf.MoveTowards(aFan.volume, (on ? nz * (0.35f + Mathf.Abs(ji.climb) * 0.25f) : 0f) * k, dt * 1.5f);
        aFan.pitch = 0.85f + nz * 0.2f;
    }

    // ================= model =================
    static Mesh Lathe(float[] zs, float[] rs, int sides, float ySquash)
    {
        var v = new List<Vector3>(); var t = new List<int>();
        for (int i = 0; i < zs.Length; i++)
            for (int s = 0; s <= sides; s++)
            {
                float a = s / (float)sides * Mathf.PI * 2f;
                v.Add(new Vector3(Mathf.Cos(a) * rs[i], Mathf.Sin(a) * rs[i] * ySquash, zs[i]));
            }
        int w = sides + 1;
        for (int i = 0; i < zs.Length - 1; i++)
            for (int s = 0; s < sides; s++)
            {
                int a = i * w + s, b = a + 1, c = a + w, d = c + 1;
                t.Add(a); t.Add(c); t.Add(b); t.Add(b); t.Add(c); t.Add(d);
            }
        var m = new Mesh { name = "lathe" };
        m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        // outward winding check (normal of a mid vertex should point away from the axis)
        Vector3[] nn = m.normals; int mid = (zs.Length / 2) * w + sides / 4;
        Vector3 radial = new Vector3(v[mid].x, v[mid].y, 0f);
        if (Vector3.Dot(nn[mid], radial) < 0f) { t.Reverse(); m.SetTriangles(t, 0); m.RecalculateNormals(); }
        return m;
    }

    // flat polygon (x, z) extruded by `thick` along y, centred; every face wound outwards (separate vertices per face)
    static Mesh Plate(Vector2[] poly, float thick)
    {
        var v = new List<Vector3>(); var t = new List<int>();
        int n = poly.Length;
        Vector3 cen = Vector3.zero; foreach (Vector2 q in poly) cen += new Vector3(q.x, 0f, q.y); cen /= n;
        System.Action<int, int, int, Vector3> Tri = (a, b, c, want) =>
        {
            Vector3 nn = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            if (Vector3.Dot(nn, want) >= 0f) { t.Add(a); t.Add(b); t.Add(c); } else { t.Add(a); t.Add(c); t.Add(b); }
        };
        int top = v.Count; for (int i = 0; i < n; i++) v.Add(new Vector3(poly[i].x, thick * 0.5f, poly[i].y));
        int bot = v.Count; for (int i = 0; i < n; i++) v.Add(new Vector3(poly[i].x, -thick * 0.5f, poly[i].y));
        for (int i = 1; i < n - 1; i++) { Tri(top, top + i, top + i + 1, Vector3.up); Tri(bot, bot + i, bot + i + 1, Vector3.down); }
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n, a = v.Count;
            v.Add(v[top + i]); v.Add(v[top + j]); v.Add(v[bot + i]); v.Add(v[bot + j]);
            Vector3 mid = (v[a] + v[a + 1]) * 0.5f; Vector3 outw = mid - cen; outw.y = 0f;
            Tri(a, a + 1, a + 2, outw); Tri(a + 1, a + 3, a + 2, outw);
        }
        var m = new Mesh { name = "plate" };
        m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    GameObject MeshGo(Transform parent, string name, Mesh mesh, Material mat, Vector3 lp, Vector3 euler, Vector3 scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = lp; go.transform.localRotation = Quaternion.Euler(euler); go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
        rends.Add(r);
        return go;
    }
    GameObject P(PrimitiveType t, Transform parent, Vector3 lp, Vector3 s, Vector3 euler, Material m)
    {
        var g = Mats.Prim(t, parent, lp, s, euler, m, false);
        rends.Add(g.GetComponent<Renderer>());
        return g;
    }

    void BuildModel()
    {
        model = new GameObject("Model").transform;
        model.SetParent(transform, false);
        Color james = Roster.Color(0);
        Material body = Mats.Paint(new Color(0.34f, 0.38f, 0.43f), 0.82f);
        Material dark = Mats.Paint(new Color(0.12f, 0.13f, 0.15f), 0.6f);
        Material green = Mats.Paint(james, 0.85f);
        Material metal = Mats.Steel(new Color(0.55f, 0.55f, 0.58f));
        Material glass = Mats.GlassTint(new Color(0.85f, 0.65f, 0.25f, 0.45f));
        Material tyre = Mats.Lit(new Color(0.08f, 0.08f, 0.08f));
        const float Y = 2f;
        // fuselage (lathe, slightly flattened)
        float[] zs = { -5.9f, -5.3f, -4.2f, -2f, 0.5f, 2.8f, 4.6f, 5.9f, 6.9f, 7.25f };
        float[] rs = { 0.62f, 0.85f, 1.0f, 1.08f, 1.08f, 0.98f, 0.74f, 0.42f, 0.12f, 0.02f };
        MeshGo(model, "Fuselage", Lathe(zs, rs, 18, 0.82f), body, new Vector3(0f, Y, 0f), Vector3.zero, Vector3.one);
        // canopy + seat
        P(PrimitiveType.Sphere, model, new Vector3(0f, Y + 0.72f, 2.6f), new Vector3(1.05f, 0.95f, 3.2f), Vector3.zero, glass);
        P(PrimitiveType.Cube, model, new Vector3(0f, Y + 0.25f, 2.4f), new Vector3(0.8f, 0.3f, 1.6f), Vector3.zero, dark);
        seat = Mats.Node(model, "Seat", new Vector3(0f, Y + 0.32f, 2.55f));
        // intakes
        for (int s = -1; s <= 1; s += 2)
        {
            P(PrimitiveType.Cube, model, new Vector3(s * 1.08f, Y + 0.05f, 0.9f), new Vector3(0.62f, 1.05f, 2.8f), new Vector3(0f, -s * 4f, 0f), body);
            P(PrimitiveType.Cube, model, new Vector3(s * 1.12f, Y + 0.05f, 2.31f), new Vector3(0.5f, 0.85f, 0.04f), new Vector3(0f, -s * 4f, 0f), dark);
        }
        // swept wings + tails (green tips)
        var wing = new[] { new Vector2(0.9f, 1.5f), new Vector2(4.7f, -2.5f), new Vector2(4.7f, -3.5f), new Vector2(0.9f, -4.0f) };
        var wingL = new Vector2[4]; for (int i = 0; i < 4; i++) wingL[i] = new Vector2(-wing[i].x, wing[i].y);
        MeshGo(model, "Wing R", Plate(wing, 0.2f), body, new Vector3(0f, Y - 0.1f, 0f), new Vector3(0f, 0f, -3f), Vector3.one);
        MeshGo(model, "Wing L", Plate(wingL, 0.2f), body, new Vector3(0f, Y - 0.1f, 0f), new Vector3(0f, 0f, 3f), Vector3.one);
        for (int s = -1; s <= 1; s += 2)
        {
            P(PrimitiveType.Cube, model, new Vector3(s * 4.62f, Y + 0.07f, -3.0f), new Vector3(0.16f, 0.24f, 1.05f), Vector3.zero, green);
            var ht = new[] { new Vector2(0.7f * s, -4.4f), new Vector2(2.7f * s, -5.6f), new Vector2(2.7f * s, -6.2f), new Vector2(0.7f * s, -6.0f) };
            MeshGo(model, "Tailplane", Plate(ht, 0.14f), body, new Vector3(0f, Y - 0.05f, 0f), Vector3.zero, Vector3.one);
            // canted fins
            var fin = new[] { new Vector2(0f, -3.6f), new Vector2(2.3f, -5.2f), new Vector2(2.3f, -6.0f), new Vector2(0f, -5.9f) };
            var fg = MeshGo(model, "Fin", Plate(fin, 0.14f), green, new Vector3(s * 0.85f, Y + 0.55f, 0f), new Vector3(0f, 0f, s > 0 ? 70f : 110f), Vector3.one);
        }
        // swivelling rear nozzle (pivot at the tail; the nozzle extends aft and rotates down for hover)
        nozzle = Mats.Node(model, "Nozzle pivot", new Vector3(0f, Y - 0.05f, -5.7f));
        P(PrimitiveType.Cylinder, nozzle, new Vector3(0f, 0f, -0.55f), new Vector3(1.05f, 0.55f, 1.05f), new Vector3(90f, 0f, 0f), metal);
        P(PrimitiveType.Cylinder, nozzle, new Vector3(0f, 0f, -1.12f), new Vector3(0.82f, 0.04f, 0.82f), new Vector3(90f, 0f, 0f), dark);
        flame = Mats.Node(nozzle, "Flame", new Vector3(0f, 0f, -1.15f));
        flameMat = new Material(Mats.Unlit(Color.white)) { name = "Jet flame" };
        float[] fz = { 0f, -0.4f, -1.2f, -2.4f, -3.4f }; float[] fr = { 0.36f, 0.42f, 0.34f, 0.18f, 0.02f };
        var fgo = MeshGo(flame, "Flame mesh", Lathe(fz, fr, 12, 1f), flameMat, Vector3.zero, Vector3.zero, Vector3.one);
        flameR = fgo.GetComponent<Renderer>(); flameR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; flameR.enabled = false;
        // lift fan behind the cockpit: fan disc + two doors that hinge open on top
        fan = P(PrimitiveType.Cylinder, model, new Vector3(0f, Y + 0.55f, 0.75f), new Vector3(1.1f, 0.03f, 1.1f), Vector3.zero, dark).transform;
        P(PrimitiveType.Cube, fan, Vector3.zero, new Vector3(1f, 1.5f, 0.12f), Vector3.zero, metal);
        P(PrimitiveType.Cube, fan, Vector3.zero, new Vector3(0.12f, 1.5f, 1f), Vector3.zero, metal);
        fanDoorL = Mats.Node(model, "Fan door L", new Vector3(-0.55f, Y + 0.86f, 0.75f));
        P(PrimitiveType.Cube, fanDoorL, new Vector3(0.28f, 0f, 0f), new Vector3(0.56f, 0.05f, 1.2f), Vector3.zero, body);
        fanDoorR = Mats.Node(model, "Fan door R", new Vector3(0.55f, Y + 0.86f, 0.75f));
        P(PrimitiveType.Cube, fanDoorR, new Vector3(-0.28f, 0f, 0f), new Vector3(0.56f, 0.05f, 1.2f), Vector3.zero, body);
        // roll posts under the wings (puff blue in hover)
        Material post = new Material(Mats.Unlit(Color.white)) { name = "Roll post" }; post.color = new Color(0.5f, 0.7f, 1f) * 1.8f;
        rollL = P(PrimitiveType.Cylinder, model, new Vector3(-3.9f, Y - 0.6f, -3.0f), new Vector3(0.22f, 0.45f, 0.22f), Vector3.zero, post).transform;
        rollR = P(PrimitiveType.Cylinder, model, new Vector3(3.9f, Y - 0.6f, -3.0f), new Vector3(0.22f, 0.45f, 0.22f), Vector3.zero, post).transform;
        // green spine stripe + nose tip
        P(PrimitiveType.Cube, model, new Vector3(0f, Y + 0.86f, -2.2f), new Vector3(0.28f, 0.06f, 5.2f), Vector3.zero, green);
        P(PrimitiveType.Sphere, model, new Vector3(0f, Y, 6.95f), new Vector3(0.22f, 0.2f, 0.6f), Vector3.zero, green);
        // landing gear (scaled flat when retracted)
        gear = Mats.Node(model, "Gear", Vector3.zero);
        Vector3[] wp = { new Vector3(0f, 0f, 4.6f), new Vector3(-1.9f, 0f, -1.6f), new Vector3(1.9f, 0f, -1.6f) };
        foreach (Vector3 w in wp)
        {
            P(PrimitiveType.Cylinder, gear, w + new Vector3(0f, 0.95f, 0f), new Vector3(0.14f, 0.6f, 0.14f), Vector3.zero, metal);
            P(PrimitiveType.Cylinder, gear, w + new Vector3(0f, 0.36f, 0f), new Vector3(0.72f, 0.14f, 0.72f), new Vector3(0f, 0f, 90f), tyre);
        }
        // name on both sides + tail number
        for (int s = -1; s <= 1; s += 2)
        {
            var tg = new GameObject("Name");
            tg.transform.SetParent(model, false);
            tg.transform.localPosition = new Vector3(s * 1.0f, Y + 0.15f, -2.2f);
            tg.transform.localRotation = Quaternion.Euler(0f, s > 0 ? -90f : 90f, 0f);
            var tm = tg.AddComponent<TextMesh>();
            tm.text = "JAMES"; tm.anchor = TextAnchor.MiddleCenter; tm.color = james;
            tm.font = UIK.WorldFont; tm.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            UIK.WorldText(tm, 64, 0.045f);
            rends.Add(tm.GetComponent<MeshRenderer>());
        }
        // vapour cone (Mach 1) - translucent white lathe around the wing roots
        vapourMat = new Material(FX.SoftMat()) { name = "Vapour cone" };
        vapourMat.mainTexture = null;
        float[] vz = { 2.5f, 0.5f, -2f, -3.5f }; float[] vr = { 0.6f, 2.6f, 4.4f, 5.2f };
        var vg = MeshGo(model, "Vapour", Lathe(vz, vr, 20, 0.8f), vapourMat, new Vector3(0f, Y, 0f), Vector3.zero, Vector3.one);
        vapour = vg.transform; vapourR = vg.GetComponent<Renderer>(); vapourR.enabled = false; vapourR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // heat shimmer under the hover nozzles: a faint rippling column of translucent rings
        shimmer = new GameObject("Shimmer").transform;
        shimmer.SetParent(transform, false);
        var shm = new Material(FX.SoftMat()) { name = "Heat shimmer" }; shm.color = new Color(1f, 0.96f, 0.9f, 0.09f);
        for (int k = 0; k < 3; k++)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(shimmer, false);
            q.transform.localRotation = Quaternion.Euler(0f, k * 60f, 0f);
            q.transform.localScale = new Vector3(3.2f, 1f, 1f);
            var mr = q.GetComponent<MeshRenderer>(); mr.sharedMaterial = shm; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        shimmer.gameObject.SetActive(false);
        // trails: wingtips (contrails) + engine
        trailL = Trail(new Vector3(-4.7f, Y + 0.07f, -3.3f), 0.25f, 1.6f, 2.2f, new Color(1f, 1f, 1f, 0.55f));
        trailR = Trail(new Vector3(4.7f, Y + 0.07f, -3.3f), 0.25f, 1.6f, 2.2f, new Color(1f, 1f, 1f, 0.55f));
        trailE = Trail(new Vector3(0f, Y, -7f), 1.0f, 4.5f, 3f, new Color(0.92f, 0.92f, 0.95f, 0.45f));
        // speed lines: streaks in a tube around the jet, moving aft fast (local space)
        speedLines = new GameObject("Speed lines").AddComponent<ParticleSystem>();
        speedLines.transform.SetParent(transform, false);
        speedLines.transform.localPosition = new Vector3(0f, 3f, 40f);
        speedLines.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = speedLines.main; m.loop = true; m.startLifetime = 0.45f; m.startSpeed = 160f; m.startSize = 0.12f;
        m.simulationSpace = ParticleSystemSimulationSpace.Local; m.maxParticles = 160; m.startColor = new Color(1f, 1f, 1f, 0.5f);
        var sh = speedLines.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 0f; sh.radius = 14f; sh.radiusThickness = 0.35f;
        sh.rotation = new Vector3(0f, 180f, 0f);
        var em = speedLines.emission; em.rateOverTime = 0f;
        var pr = speedLines.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Stretch; pr.velocityScale = 0.06f; pr.lengthScale = 2f;
        pr.sharedMaterial = FX.SoftMat();
        speedLines.Play();
        foreach (var r in model.GetComponentsInChildren<Renderer>()) if (r != flameR && r != vapourR && r.sharedMaterial != glass) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
    }

    TrailRenderer Trail(Vector3 lp, float w0, float w1, float time, Color c)
    {
        var go = new GameObject("Trail");
        go.transform.SetParent(model, false);
        go.transform.localPosition = lp;
        var tr = go.AddComponent<TrailRenderer>();
        tr.time = time; tr.startWidth = w0; tr.endWidth = w1; tr.minVertexDistance = 3f;
        var mat = new Material(FX.SoftMat()) { name = "Contrail" }; mat.color = c;
        tr.material = mat;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        tr.colorGradient = g;
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; tr.receiveShadows = false;
        tr.emitting = false;
        return tr;
    }

    // per-frame visual sync (rb interpolation handles the body; this keeps `pos` right after teleports)
    void LateUpdate()
    {
        if (crashT < 0f && rb != null && (transform.position - pos).sqrMagnitude > 400f && !rb.isKinematic) { transform.position = pos; }
    }
}
