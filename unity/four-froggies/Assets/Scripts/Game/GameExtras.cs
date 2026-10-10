using UnityEngine;
using UnityEngine.InputSystem;

// ffu14: play-mode helpers that grew out of Game.cs (touch context buttons, shared-screen space trips).
public partial class Game
{
    string lastTouchKey = "";

    // ffu15: mouse over the robot phone (button / open panel) or the space radar -> a click there doesn't grab the mouse
    bool UiUnderMouse()
    {
        if (UnityEngine.InputSystem.Mouse.current == null) return false;
        Vector2 mp = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
        if (RobotPhone.I != null && RobotPhone.I.Captures(mp)) return true;
        if (RobotFeed.I != null && RobotFeed.I.Captures(mp)) return true;   // ffu20 video feed buttons
        return false;
    }

    // ffu15: a mech shot's recoil on the pilot's own view: camera kick + shake + pad rumble
    float rumbleEnd = -1f;
    Gamepad rumblePad;
    public void PilotKick(Frog pilot, float power, bool missile)
    {
        foreach (var s in slots)
        {
            if (frogs[s.frog] != pilot) continue;
            if (s.rig != null) { s.rig.Kick(missile ? 3.2f * power : 2.2f * power); s.rig.AddShake(missile ? 0.5f * power : 0.32f * power); }
            if (sharedCam != null && sharedCam.enabled) sharedTrauma = Mathf.Min(1f, sharedTrauma + 0.25f * power);
            if (s.kind == InputKind.Gamepad && s.pad != null && s.pad.added)
            {
                try { s.pad.SetMotorSpeeds(missile ? 0.9f : 0.6f, missile ? 0.6f : 0.85f); rumblePad = s.pad; rumbleEnd = Time.unscaledTime + (missile ? 0.28f : 0.14f); } catch { }
            }
        }
    }
    void TickRumble()
    {
        if (rumblePad != null && rumbleEnd > 0f && Time.unscaledTime > rumbleEnd)
        {
            try { rumblePad.SetMotorSpeeds(0f, 0f); } catch { }
            rumbleEnd = -1f; rumblePad = null;
        }
    }

    // ffu14: a mech rocketing up: the sky darkens towards black and the far plane opens up with altitude, per camera
    void AscentViews()
    {
        float sharedK = 0f, sharedAlt = 0f;
        foreach (var s in slots)
        {
            Frog f = frogs[s.frog];
            StoryMech m = f.vehicle as StoryMech;
            float k = m != null ? m.SpaceK : 0f, alt = m != null ? m.Altitude : 0f;
            if (k > sharedK) sharedK = k;
            if (alt > sharedAlt) sharedAlt = alt;
            if (s.cam != null && s.cam.enabled) ApplyAscent(s.cam, f.world, k, alt);
        }
        if (sharedCam.enabled) ApplyAscent(sharedCam, frogs[slots[0].frog].world, sharedK, sharedAlt);
    }

    static void ApplyAscent(Camera c, WorldId w, float k, float alt)
    {
        if (w != WorldId.Ranch) return;
        Ascent.Set(c, k);
        c.farClipPlane = 900f + alt * 1.6f;
    }

    // ---------------- ffu14 mech demos (&ffshot=mech / mechfight / mechspace) ----------------
    StoryMech demoMech, demoFoe;
    float demoMechT;
    static StoryMech FindMech(int owner, int band) { foreach (var m in StoryMech.AllMechs) if (m != null && m.owner == owner && m.band == band) return m; return null; }

    void DemoMechStart(Frog f, string sc)
    {
        if (f.world != WorldId.Ranch) f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f);
        if (f.vehicle != null) f.ExitVehicle();
        int band = sc == "mechspace" ? 3 : sc == "mech" ? 1 : 0;
        demoMech = FindMech(0, band);
        if (demoMech == null) return;
        if (!Roster.IsFrog(f.charId) || f.charId != 0) SetSeatChar(f.id, 0, false);
        demoKeepChars = true;
        if (sc == "mechfight")
        {
            demoMech.DemoPlace(new Vector3(-104f, 0f, 38f), 270f);
            demoFoe = FindMech(1, 0);
            Frog foePilot = frogs[1];
            if (demoFoe != null)
            {
                demoFoe.DemoPlace(new Vector3(-138f, 0f, 38f), 90f);
                if (foePilot.vehicle != null) foePilot.ExitVehicle();
                if (foePilot.charId != 1) SetSeatChar(1, 1, false);
                foePilot.EnterVehicle(demoFoe);
            }
        }
        f.EnterVehicle(demoMech);
        demoMechT = 0f;
        if (sc == "mechspace") StoryMech.DemoClimb = 2.5f;
        StoryMech.DtCap = 0.3f;   // probe runs at ~2 fps: let game time keep up with wall time
        demoHook = i => DemoMechInput(i, sc);
        Debug.Log("FFDEMO mech start " + sc + " " + demoMech.Title);
    }

    PIn DemoMechInput(PIn i, string sc)
    {
        float t = demoMechT;
        var o = new PIn(); o.look = i.look;
        if (demoMech != null) o.look = Vector2.zero;
        if (sc == "mech")
        {
            // walk, turn, jump, rocket with afterburner, land
            o.move = t < 6f ? new Vector2(t > 3f ? 0.6f : 0f, 1f) : t < 9f ? new Vector2(0f, 0.6f) : Vector2.zero;
            o.upHeld = (t > 6.5f && t < 6.65f) || (t > 9f && t < 13f);
            o.boostHeld = t > 10.5f && t < 13f;
            if (t > 13f && t < 15f) o.move = new Vector2(0f, 1f);
        }
        else if (sc == "mechfight")
        {
            o.gunHeld = t > 2f; o.gunFire = t > 2f && Time.frameCount % 2 == 0;
            o.alt = t > 3f && Mathf.Repeat(t, 2f) < 0.1f; o.mslFire = o.alt;
        }
        else
        {
            o.upHeld = t > 1.5f; o.boostHeld = t > 3f;
            o.move = new Vector2(0f, t > 3f ? 0.3f : 0f);
        }
        return o;
    }

    bool DemoMechCam(Frog f, string sc, out Vector3 pos, out Vector3 look)
    {
        pos = look = Vector3.zero;
        demoMechT += Mathf.Min(Time.deltaTime, StoryMech.DtCap);
        if (demoMech == null) return false;
        StoryMech m = demoMech;
        float H = m.height;
        Vector3 mp = m.transform.position, fw = m.transform.forward, rt = m.transform.right;
        demoLogT -= Time.unscaledDeltaTime;
        if (demoLogT <= 0f)
        {
            demoLogT = 1f;
            Debug.Log("FFDEMO " + sc + " t=" + demoMechT.ToString("0.0") + " " + m.StatusLine + " pos=" + mp.ToString("0") + (demoFoe != null ? " foe " + demoFoe.StatusLine + (demoFoe.wrecked ? " WRECKED" : "") : "") + " world=" + f.world);
        }
        if (sc == "mech")
        {
            if (demoMechT < 4f) { pos = mp + Vector3.up * H * 1.45f - fw * H * 0.35f + rt * H * 0.2f; look = mp + Vector3.up * H * 0.8f; return true; }   // looking down on the collar
            pos = mp - fw * H * 1.6f + rt * H * 0.9f + Vector3.up * H * 0.7f; look = mp + Vector3.up * H * 0.5f; return true;
        }
        if (sc == "mechfight")
        {
            // ffu14e: over James's shoulder towards the foe; mech fire aims along the camera RIG yaw, which the demo never
            // set, so every shot went into the parked mechs instead of the foe - point the rig at the foe
            Vector3 fp = demoFoe != null ? demoFoe.HomeOrNow : mp + fw * H * 3f;
            Vector3 tf = fp - mp; tf.y = 0f; tf = tf.sqrMagnitude > 1f ? tf.normalized : fw;
            Vector3 tr = new Vector3(tf.z, 0f, -tf.x);
            if (slots.Count > 0 && slots[0].rig != null) slots[0].rig.SetYaw(Mathf.Atan2(tf.x, tf.z) * Mathf.Rad2Deg);   // the aim yaw
            pos = mp - tf * H * 1.5f + tr * H * 0.55f + Vector3.up * H * 1.0f; look = fp + Vector3.up * H * 0.45f; return true;
        }
        // mechspace: chase from above-behind so the ranch shrinks below; normal views once in space
        if (f.world != WorldId.Ranch) return false;
        pos = mp - fw * H * 2.2f + Vector3.up * H * 1.3f; look = mp + Vector3.down * H * 0.6f;
        return true;
    }

    // ---------------- ffu14 &ffshot=touch: P1 steps through every control mode (6 s each) for the touch-button shots ----------------
    static readonly string[] TouchModes = { "foot", "car", "tank", "heli", "boat", "mech", "robot", "sub", "space" };
    int touchModeIdx = -1;
    float touchModeT0 = -1f;
    void DemoTouchModes(Frog f)
    {
        if (touchModeT0 < 0f) touchModeT0 = Time.realtimeSinceStartup;
        int i = Mathf.Min(TouchModes.Length - 1, (int)((Time.realtimeSinceStartup - touchModeT0) / 6f));
        if (i == touchModeIdx) return;
        touchModeIdx = i;
        string m = TouchModes[i];
        if (f.remote != null && RobotPhone.I != null) RobotPhone.I.Toggle(f);
        if (f.world != WorldId.Ranch) f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f);
        if (f.vehicle != null) f.ExitVehicle();
        System.Func<System.Func<Vehicle, bool>, Vehicle> near = pred =>
        {
            Vehicle best = null; float bd = 1e12f;
            foreach (var v in Vehicle.All) { if (v == null || v.driver != null || !pred(v)) continue; float d = (v.transform.position - f.transform.position).sqrMagnitude; if (d < bd) { bd = d; best = v; } }
            return best;
        };
        Vehicle pick = null;
        switch (m)
        {
            case "car": pick = near(v => v is GroundVehicle && !(v is Tank) && ((GroundVehicle)v).usesTriggers); break;
            case "tank": pick = near(v => v is Tank); break;
            case "heli": pick = near(v => v is Flyer); break;
            case "boat": pick = near(v => v is Boat); break;
            case "mech": pick = FindMech(0, 0); if (f.charId != 0) SetSeatChar(f.id, 0, false); break;
            case "robot": if (RobotPhone.I != null) { RobotPhone.I.DemoOpen(f, 1, "drive"); RobotPhone.I.DemoSend(); } break;
            case "sub": if (UnderwaterWorld.I != null) UnderwaterWorld.I.Dive(f); break;
            case "space": if (SpaceWorld.I != null) SpaceWorld.I.ToOrbit(f, "earth"); break;
        }
        if (pick != null)
        {
            Vector3 p = pick.transform.position + pick.transform.right * 3f;
            f.Teleport(new Vector3(p.x, Ranch.GY(p.x, p.z) + 0.3f, p.z));
            f.EnterVehicle(pick);
        }
        Debug.Log("FFDEMO touch mode " + m + (pick != null ? " (" + pick.Title + ")" : "") + " t=" + Time.realtimeSinceStartup.ToString("0.0"));
    }

    // shared-screen local multiplayer (one camera for 2+ local players): space trips take everyone along
    public bool SharedScreen { get { return slots.Count > 1 && shared; } }

    // only the on-screen buttons that do something right now (on foot / vehicle / mech / boat / sub / space / robot)
    void SetTouchSet(Frog f)
    {
        string[] w;
        TouchControls.mechMode = false;
        if (f.launching) w = new string[] { null, null, null, null, null };
        else if (f.passengerOf != null) w = f.passengerOf is Starship && ((Starship)f.passengerOf).BayOpen ? new[] { "OUT", null, null, null, null } : new string[] { null, null, null, null, null };
        else if (f.jetOf != null) w = new[] { "A", null, null, "UP", "DOWN" };
        else if (f.remote != null) w = new[] { "WAVE", null, null, "RUN", null };
        else if (f.vehicle != null)
        {
            w = f.vehicle.TouchSet;
            TouchControls.mechMode = f.vehicle is StoryMech;
        }
        else if (f.world == WorldId.Underwater) w = new[] { "A", null, null, "UP", "DOWN" };
        else if (f.Swimming && f.world == WorldId.Ranch) w = new[] { "A", null, null, null, "DIVE" };
        else w = new[] { "A", null, null, null, null };
        for (int i = 0; i < 6; i++) TouchControls.want[i] = i < w.Length ? w[i] : null;
        // ffu15: 6th button - AIM in a mech, BAY in a mech flying in space
        if (f.vehicle is StoryMech) TouchControls.want[5] = "AIM";
        else if (f.vehicle is Starship && ((Starship)f.vehicle).mechForm != null) TouchControls.want[5] = "BAY";
        string key = TouchControls.ModeKey;
        if (key != lastTouchKey) { lastTouchKey = key; Debug.Log("Touch set: " + key.Replace("|", " ")); }
    }

    // ---------------- ffu15 demos: &ffshot=mechaim / mechlook / surface / spacemap / mechspace / cargobay ----------------
    float d15T, d15Log;
    int d15Phase;
    void Demo15Start(Frog f, string sc)
    {
        if (f.world != WorldId.Ranch) f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f);
        if (f.vehicle != null) f.ExitVehicle();
        d15T = 0f; d15Phase = -1; demoHook = null;
        if (!Roster.IsFrog(f.charId) || f.charId != 0) SetSeatChar(f.id, 0, false);
        demoKeepChars = true;
        StoryMech.DtCap = 0.3f;
        if (sc == "mechaim")
        {
            demoMech = FindMech(0, 0);
            demoFoe = FindMech(1, 1);
            if (demoMech == null) return;
            demoMech.DemoPlace(new Vector3(-100f, 0f, 40f), 270f);
            if (demoFoe != null) demoFoe.DemoPlace(new Vector3(-150f, 0f, 30f), 90f);
            f.EnterVehicle(demoMech);
            demoHook = i =>
            {
                var o = new PIn();
                o.aimHeld = d15T > 1.5f;
                o.gunHeld = d15T > 4f; o.gunFire = o.gunHeld;
                o.mslFire = d15T > 6f && Mathf.Repeat(d15T, 3f) < 0.35f;
                return o;
            };
        }
        else if (sc == "mechlook") { }
        else if (sc == "surface")
        {
            if (UnderwaterWorld.I == null) return;
            UnderwaterWorld.I.Dive(f);
            if (f.vehicle != null) f.ExitVehicle();     // swim out in scuba
            f.Teleport(Worlds.UnderO + new Vector3(10f, -4f, 6f));
            demoHook = i =>
            {
                var o = new PIn();
                Frog me = frogs[slots[0].frog];
                if (me.world == WorldId.Underwater && d15Phase < 1) { o.hopHeld = true; o.move = new Vector2(0f, 0.4f); }
                if (me.world == WorldId.Ranch && d15Phase < 1) { d15Phase = 1; d15Log = d15T; Debug.Log("FFDEMO surface: floating in the pond t=" + Time.realtimeSinceStartup.ToString("0.0")); }
                if (d15Phase == 1) { o.move = new Vector2(0.3f, 0.5f); if (d15T - d15Log > 14f) o.downHeld = true; }
                if (d15Phase == 1 && me.world == WorldId.Underwater) { d15Phase = 2; Debug.Log("FFDEMO surface: dived back down t=" + Time.realtimeSinceStartup.ToString("0.0")); }
                if (d15Phase == 2) o.move = new Vector2(0f, 0.6f);
                return o;
            };
        }
        else if (sc == "spacemap")
        {
            if (SpaceWorld.I == null) return;
            SpaceWorld.I.ToOrbit(f, "earth");
            demoHook = i =>
            {
                var o = new PIn();
                if (d15T > 8f && d15Phase < 1) { d15Phase = 1; var sh = SpaceWorld.I.ship; sh.SetTarget(SpaceWorld.I.Find("mars")); o.auto = true; Debug.Log("FFDEMO spacemap: auto-transfer to Mars t=" + Time.realtimeSinceStartup.ToString("0.0")); }
                return o;
            };
        }
        else if (sc == "mechspace")
        {
            // the bug Bill hit: rocket a mech past 2.4 km -> it must stay a mech in space (shoulder riders added for the shot)
            demoMech = FindMech(0, 3);
            if (demoMech == null) return;
            f.EnterVehicle(demoMech);
            StoryMech.DemoClimb = 2.5f;
            demoHook = i =>
            {
                var o = new PIn();
                Frog me = frogs[slots[0].frog];
                if (me.world == WorldId.Ranch) { o.upHeld = d15T > 1f; o.boostHeld = d15T > 2.5f; }
                else { o.boostHeld = true; o.move = new Vector2(Mathf.Sin(d15T * 0.4f) * 0.4f, 0f); }
                return o;
            };
        }
        else if (sc == "cargobay")
        {
            demoMech = FindMech(0, 1);
            if (demoMech == null || SpaceWorld.I == null) return;
            f.EnterVehicle(demoMech);
            demoMech.GoToSpace();
            demoHook = i => { var o = new PIn(); o.boostHeld = d15T < 4f; return o; };
        }
        Debug.Log("FFDEMO 15 start " + sc);
    }

    bool Demo15Cam(Frog f, string sc, out Vector3 pos, out Vector3 look)
    {
        pos = look = Vector3.zero;
        float dt = Mathf.Min(Time.deltaTime, 0.3f);
        d15T += dt;
        var sw = SpaceWorld.I;
        bool log = (d15Log -= Time.unscaledDeltaTime) <= 0f && sc != "surface";
        if (log) d15Log = 1.5f;
        switch (sc)
        {
            case "mechaim":
                {
                    if (demoMech == null) return false;
                    Vector3 mp = demoMech.transform.position, fp = demoFoe != null ? demoFoe.transform.position : mp + demoMech.transform.forward * 60f;
                    Vector3 tf = fp - mp; tf.y = 0f; tf.Normalize();
                    if (slots[0].rig != null) { slots[0].rig.SetYaw(Mathf.Atan2(tf.x, tf.z) * Mathf.Rad2Deg); slots[0].rig.SetPitch(-2f); }
                    if (log) Debug.Log("FFDEMO mechaim t=" + d15T.ToString("0.0") + " aimK=" + demoMech.aimK.ToString("0.00") + " recoil=" + demoMech.recoilK.ToString("0.00") + " onTarget=" + demoMech.aimOnTarget + " foe " + (demoFoe != null ? demoFoe.StatusLine : "-"));
                    if (d15T < 1.5f) { pos = mp - tf * 30f + Vector3.up * 14f + new Vector3(tf.z, 0f, -tf.x) * 14f; look = mp + Vector3.up * 8f; return true; }
                    return false;   // the real over-the-shoulder aim camera + reticle
                }
            case "mechlook":
                {
                    // looking UP at mechs from the ground: chest plates must read correctly (front + back), several sizes
                    int ph = d15T < 6f ? 0 : d15T < 12f ? 1 : d15T < 18f ? 2 : 3;
                    StoryMech m = ph == 0 ? FindMech(0, 1) : ph == 1 ? FindMech(0, 1) : ph == 2 ? FindMech(0, 3) : FindMech(2, 0);
                    if (m == null) return false;
                    if (ph != d15Phase) { d15Phase = ph; Debug.Log("FFDEMO mechlook phase " + ph + " " + m.Title + (ph == 1 ? " (back)" : " (front)") + " t=" + Time.realtimeSinceStartup.ToString("0.0")); }
                    Vector3 mp = m.transform.position, fw = m.transform.forward; float H = m.height;
                    Vector3 side = ph == 1 ? -fw : fw;
                    pos = mp + side * (H * 0.55f + 4f) + m.transform.right * H * 0.12f + Vector3.up * 2.2f;
                    pos.y = Mathf.Max(pos.y, Ranch.GY(pos.x, pos.z) + 1.6f);
                    look = mp + Vector3.up * H * 0.62f;
                    return true;
                }
            case "surface":
                {
                    Transform ft = f.transform;
                    Vector3 fw = ft.forward; fw.y = 0f; fw = fw.sqrMagnitude > 0.01f ? fw.normalized : Vector3.forward;
                    Vector3 rt = new Vector3(fw.z, 0f, -fw.x);
                    Vector3 fp = ft.position + Vector3.up * 0.7f;
                    pos = fp + rt * 2.6f + fw * 2.4f + Vector3.up * (f.world == WorldId.Ranch ? 1.4f : 0.6f);
                    look = fp;
                    if ((d15Log -= Time.unscaledDeltaTime) <= -1.5f) { d15Log = 0f; }
                    return true;
                }
            case "spacemap":
                return false;   // follow cam: the radar + HUD are the point
            case "mechspace":
            case "cargobay":
                {
                    if (f.world == WorldId.Ranch)
                    {
                        if (sc == "cargobay" || demoMech == null) return false;
                        float H = demoMech.height; Vector3 mp = demoMech.transform.position, fw = demoMech.transform.forward;
                        pos = mp - fw * H * 2.2f + Vector3.up * H * 1.3f; look = mp + Vector3.down * H * 0.6f;
                        if (log) Debug.Log("FFDEMO mechspace climbing " + demoMech.StatusLine + " t=" + Time.realtimeSinceStartup.ToString("0.0"));
                        return true;
                    }
                    if (sw == null || sw.ship == null || f.world != WorldId.Space) return false;
                    Starship sh = sw.ship;
                    if (d15Phase < 0)
                    {
                        d15Phase = 0; d15Log = 0f;
                        sh.DemoRiders(frogs[1], frogs[2]);
                        Debug.Log("FFDEMO " + sc + ": in space as " + sh.Title + " mechForm=" + (sh.mechForm != null) + " riders added t=" + Time.realtimeSinceStartup.ToString("0.0"));
                        d15T = 0f;
                    }
                    if (sc == "cargobay" && d15T > 3f && d15Phase == 0) { d15Phase = 1; sh.ToggleBay(); Debug.Log("FFDEMO cargobay: bay open, jetters out t=" + Time.realtimeSinceStartup.ToString("0.0")); }
                    if (sc == "cargobay" && d15T > 30f && d15Phase == 1) { d15Phase = 2; sh.ReboardAll(); Debug.Log("FFDEMO cargobay: everyone back, doors closed t=" + Time.realtimeSinceStartup.ToString("0.0")); }
                    Transform st = sh.transform;
                    Vector3 sp = st.position, sf = st.forward, sr = st.right;
                    if (sc == "mechspace") { pos = sp + sr * 24f + Vector3.up * 9f + sf * 6f; look = sp + sf * 2f; }
                    else { pos = sp + sr * 20f + Vector3.up * 22f - sf * 10f; look = sp + Vector3.up * 3f; }
                    return true;
                }
        }
        return false;
    }
}
