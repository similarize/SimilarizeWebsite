using UnityEngine;

// ffu14: play-mode helpers that grew out of Game.cs (touch context buttons, shared-screen space trips).
public partial class Game
{
    string lastTouchKey = "";

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
            o.alt = t > 3f && Mathf.Repeat(t, 2f) < 0.1f;
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
            Vector3 mid = demoFoe != null ? (mp + demoFoe.HomeOrNow) * 0.5f : mp;
            pos = mid + new Vector3(4f, H * 0.75f, H * 2.6f); look = mid + Vector3.up * H * 0.4f; return true;
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
        if (f.passengerOf != null || f.launching) w = new string[] { null, null, null, null, null };
        else if (f.remote != null) w = new[] { "WAVE", null, null, "RUN", null };
        else if (f.vehicle != null)
        {
            w = f.vehicle.TouchSet;
            TouchControls.mechMode = f.vehicle is StoryMech;
        }
        else if (f.world == WorldId.Underwater) w = new[] { "A", null, null, "UP", "DOWN" };
        else w = new[] { "A", null, null, null, null };
        for (int i = 0; i < 5; i++) TouchControls.want[i] = i < w.Length ? w[i] : null;
        string key = TouchControls.ModeKey;
        if (key != lastTouchKey) { lastTouchKey = key; Debug.Log("Touch set: " + key.Replace("|", " ")); }
    }
}
