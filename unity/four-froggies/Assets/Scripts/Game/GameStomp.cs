using UnityEngine;

// ffu28: ground quake from giant footfalls (MechStomp) + the mechstomp / optimus demo shots.
public partial class Game
{
    // quake every player view within `range` of p (falls off with distance squared); nothing in the lobby, the help
    // menu or a story cutscene. Split views each get their own amount; the shared view uses its trauma.
    public void Quake(Vector3 p, float power, float range)
    {
        if (state != State.Play || help) return;
        if (Story.Active && Story.I != null && Story.I.BlocksInput) return;
        foreach (var s in slots)
        {
            if (s.rig == null) continue;
            Frog f = frogs[s.frog];
            if (f == null || f.world != WorldId.Ranch) continue;
            float d = (f.FocusPoint - p).magnitude;
            float k = Mathf.Clamp01(1f - d / range); k *= k;
            if (k < 0.01f) continue;
            s.rig.AddQuake(power * k);
            // a short low thump on the pad's heavy motor
            if (s.kind == InputKind.Gamepad && s.pad != null && s.pad.added && power * k > 0.12f)
            {
                try { s.pad.SetMotorSpeeds(Mathf.Clamp01(power * k * 0.9f), Mathf.Clamp01(power * k * 0.25f)); rumblePad = s.pad; rumbleEnd = Time.unscaledTime + 0.12f; } catch { }
            }
        }
        if (sharedCam != null && sharedCam.enabled)
        {
            float sd = (sharedFocus - p).magnitude;
            float k = Mathf.Clamp01(1f - sd / (range * 1.3f)); k *= k;
            sharedTrauma = Mathf.Min(1f, sharedTrauma + power * k * 0.45f);
        }
    }

    // ---------------- demo shots ----------------
    //   mechstomp - James's 10-story walks past a camera on the ground (frog eye level), stops, jumps and lands
    //   optimus   - P1 walks the Optimus mech suit up the east edge berm (~20 deg), along the side slope, back down;
    //               low side camera on the feet; logs foot height vs terrain every 0.5 s
    public static bool IsStompDemo(string sc) { return sc == "mechstomp" || sc == "optimus"; }
    Vehicle stompV;
    float stompT, stompLogT;
    Vector3 stompCam;
    int stompSteps0;

    void StompDemoStart(Frog f, string sc)
    {
        if (f.world != WorldId.Ranch) f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f);
        if (f.vehicle != null) f.ExitVehicle();
        if (!Roster.IsFrog(f.charId) || f.charId != 0) SetSeatChar(f.id, 0, false);
        demoKeepChars = true;
        stompT = 0f; stompLogT = 0f; stompSteps0 = MechStomp.Count;
        if (sc == "mechstomp")
        {
            StoryMech m = FindMech(0, 0);
            if (m == null) { Debug.Log("FFDEMO stomp: no mech"); return; }
            m.DemoPlace(new Vector3(-100f, 0f, 40f), 270f);   // heading west along z 40 (open yard north of the 10-story row)
            stompV = m;
            StoryMech.DtCap = 0.3f;
            f.EnterVehicle(m);
            float cx = -88f, cz = 30f;
            stompCam = new Vector3(cx, Ranch.GY(cx, cz) + 1.6f, cz);
        }
        else
        {
            Vehicle s = null;
            foreach (var v in Vehicle.All) if (v != null && v.Title.StartsWith("Optimus")) { s = v; break; }
            if (s == null) { Debug.Log("FFDEMO stomp: no Optimus suit"); return; }
            Vector3 p = new Vector3(158f, 0f, 4f); p.y = Ranch.GY(p.x, p.z) + 0.3f;
            s.rb.velocity = Vector3.zero; s.rb.angularVelocity = Vector3.zero;
            s.rb.position = p; s.rb.rotation = Quaternion.Euler(0f, 90f, 0f);
            s.transform.position = p; s.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            stompV = s;
            f.EnterVehicle(s);
        }
        demoHook = i => StompDemoInput(i, sc);
        Debug.Log("FFDEMO stomp start " + sc + " " + (stompV != null ? stompV.Title : "-"));
    }

    PIn StompDemoInput(PIn i, string sc)
    {
        float t = stompT;
        var o = new PIn();
        if (sc == "mechstomp")
        {
            o.move = t < 15f ? new Vector2(0f, 0.45f) : Vector2.zero;
            o.upHeld = t > 17f && t < 17.45f;          // a hop (+ a puff of rockets) -> landing quake
            if (t > 22f && t < 34f) o.move = new Vector2(0.35f, 0.7f);
        }
        else
        {
            // up the berm (east), along the side slope (north), turn round, back down the slope (west)
            if (t < 5.5f) o.move = new Vector2(0f, 0.5f);
            else if (t < 7.3f) o.move = new Vector2(-1f, 0.15f);
            else if (t < 17f) o.move = new Vector2(0f, 0.45f);
            else if (t < 18.8f) o.move = new Vector2(-1f, 0.15f);
            else o.move = new Vector2(0f, 0.45f);
        }
        return o;
    }

    bool StompDemoCam(Frog f, string sc, out Vector3 pos, out Vector3 look)
    {
        pos = look = Vector3.zero;
        float dt = sc == "mechstomp" ? Mathf.Min(Time.deltaTime, StoryMech.DtCap) : Time.deltaTime;
        stompT += dt;
        if (stompV == null) return false;
        Transform vt = stompV.transform;
        Vector3 vp = vt.position;
        CamRig rig = slots.Count > 0 ? slots[0].rig : null;
        stompLogT -= Time.unscaledDeltaTime;
        if (sc == "mechstomp")
        {
            StoryMech m = (StoryMech)stompV;
            // a spectator on the grass walking alongside, a little ahead, 13 m south of the path, eye level 1.7 m
            float cx = vp.x - 7f, cz = vp.z - 13f;
            pos = new Vector3(cx, Ranch.GY(cx, cz) + 1.7f, cz);
            look = vp + Vector3.up * m.height * 0.45f;
            float cd = (vp - pos).magnitude;
            if (stompLogT <= 0f)
            {
                stompLogT = 0.25f;
                Debug.Log("FFDEMO stomp t=" + stompT.ToString("0.0") + " steps=" + (MechStomp.Count - stompSteps0) + " vol=" + MechStomp.LastVol.ToString("0.00") + " shake=" + MechStomp.LastShake.ToString("0.00") + " quake=" + (rig != null ? rig.QuakeK.ToString("0.00") : "-") + " dist=" + cd.ToString("0") + " pos=" + vp.ToString("0"));
            }
            if (rig != null) { pos += rig.QuakePos(cd); }
            return true;
        }
        // optimus: low camera on the suit's left side, 7 m out, eased
        Vector3 fw = vt.forward; fw.y = 0f; fw.Normalize();
        Vector3 lf = new Vector3(-fw.z, 0f, fw.x);
        Vector3 want = vp + lf * 7.5f + fw * 1.5f; want.y = Mathf.Max(Ranch.GY(want.x, want.z), vp.y) + 1.6f;
        stompCam = stompCam == Vector3.zero || (stompCam - want).sqrMagnitude > 400f ? want : Vector3.Lerp(stompCam, want, Mathf.Min(1f, Time.unscaledDeltaTime * 1.5f));
        pos = stompCam; look = vp + Vector3.up * 1.1f;
        if (stompLogT <= 0f)
        {
            stompLogT = 0.5f;
            GroundVehicle gv = stompV as GroundVehicle;
            int g = 0; if (gv != null) foreach (var w in gv.wheels) if (w.grounded) g++;
            RaycastHit h; float floor = Ranch.GY(vp.x, vp.z);
            if (Physics.Raycast(vp + Vector3.up * 3f, Vector3.down, out h, 8f, Vehicle.GroundMask, QueryTriggerInteraction.Ignore)) floor = h.point.y;
            Debug.Log("FFDEMO optimus t=" + stompT.ToString("0.0") + " feet-ground=" + (vp.y - floor).ToString("0.00") + " gy=" + floor.ToString("0.00") + " wheels=" + g + " v=" + stompV.Speed.ToString("0.0") + " up=" + vt.up.y.ToString("0.00") + " steps=" + (MechStomp.Count - stompSteps0) + " pos=" + vp.ToString("0.0"));
        }
        if (rig != null) pos += rig.QuakePos(7.5f);
        return true;
    }
}
