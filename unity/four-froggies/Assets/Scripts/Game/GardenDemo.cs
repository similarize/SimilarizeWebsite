using UnityEngine;

// ffu32 demo shots (?ffdemo=1&ffshot=...):
//   garden    - dusk-ish light. James by the cold fire ring presses E (t 2): spark, flames grow (close-up) -> James lies in a
//               hammock, Jimmy / Bubbles / Rexy on the swings round the fire -> wide view of the garden -> hammock close-up
//               (James rocks it) -> swings + fire close-up -> James hops out and douses the fire (t 46). Logs
//               "FFDEMO garden t=.. phase=.." every second.
//   horseride - James mounts a pasture horse: stand, walk, trot, canter out through the gate (it swings open), gallop across
//               the ranch, jump, slow down, rear up + whinny; Bubbles rides along on another horse. Chase camera on the left.
//               Logs "FFDEMO horse t=.. gait=.. v=.. pos=.. air" every 0.5 s.
public partial class Game
{
    public static bool IsGardenDemo(string sc) { return sc == "garden" || sc == "horseride"; }
    float gdT, gdLog, gdExp = -1f; int gdPhase, gdFired; Vector3 gdCam; Horse gdHorse;

    void GardenDemoStart(Frog f, string sc)
    {
        gdT = 0f; gdLog = 0f; gdPhase = -1; gdFired = 0; gdCam = Vector3.zero; demoHook = null;
        if (f.world != WorldId.Ranch) f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f);
        if (f.vehicle != null) f.ExitVehicle();
        for (int i = 0; i < 4; i++) if (frogs[i] != null && frogs[i].charId != i) SetSeatChar(i, i, false);   // the four froggies
        demoKeepChars = true;
        Frog.TagOn = false;
        if (sc == "garden")
        {
            var g = Garden.I;
            if (g == null) { Debug.Log("FFDEMO garden: no garden built"); return; }
            // dusk-ish: low warm sun, dimmer sky + ambient (demo only)
            if (RenderSettings.sun != null) { RenderSettings.sun.intensity *= 0.42f; RenderSettings.sun.color = new Color(1f, 0.62f, 0.38f); }
            Material sk = RenderSettings.skybox;
            gdExp = sk != null && sk.HasProperty("_Exposure") ? sk.GetFloat("_Exposure") : -1f;
            Worlds.StoryHook = DuskHook;
            Vector3 p = g.WG(5f, -0.9f);
            f.SendTo(WorldId.Ranch, p + Vector3.up * 0.1f, g.Yaw);
            for (int i = 1; i < 4; i++) if (frogs[i] != null) frogs[i].SendTo(WorldId.Ranch, g.WG(-1f + i * 1.5f, -9f) + Vector3.up * 0.1f, g.Yaw);
            demoHook = GardenDemoInput;
            Debug.Log("FFDEMO garden start at " + g.C.ToString("0") + " yaw " + g.Yaw.ToString("0"));
        }
        else
        {
            if (Horse.Herd.Count == 0) { Debug.Log("FFDEMO horse: no horses"); return; }
            Rect P = RanchLife.Pasture;
            gdHorse = Horse.Herd[0];
            // put the horse inside the pasture, 30 m west of the gate, facing east (the gate)
            Vector3 hp = new Vector3(P.xMax - 30f, 0f, P.center.y); hp.y = Ranch.GY(hp.x, hp.z);
            gdHorse.transform.position = hp; gdHorse.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            gdHorse.anim.SyncYaw();
            f.SendTo(WorldId.Ranch, hp + new Vector3(0f, 0.2f, -1.6f), 90f);
            f.EnterVehicle(gdHorse);
            if (Horse.Herd.Count > 1 && frogs[2] != null)
            {
                Horse h2 = Horse.Herd[1];
                Vector3 p2 = hp + new Vector3(-5f, 0f, 4f); p2.y = Ranch.GY(p2.x, p2.z);
                h2.transform.position = p2; h2.transform.rotation = Quaternion.Euler(0f, 90f, 0f); h2.anim.SyncYaw();
                frogs[2].SendTo(WorldId.Ranch, p2 + new Vector3(0f, 0.2f, -1.6f), 90f);
                frogs[2].AiRide(h2, 120f);
            }
            demoHook = HorseDemoInput;
            Debug.Log("FFDEMO horse start at " + hp.ToString("0"));
        }
    }

    void DuskHook(Camera c, WorldId w)
    {
        if (w != WorldId.Ranch) return;
        RenderSettings.ambientSkyColor = new Color(0.36f, 0.33f, 0.42f);
        RenderSettings.ambientEquatorColor = new Color(0.36f, 0.27f, 0.24f);
        RenderSettings.ambientGroundColor = new Color(0.13f, 0.1f, 0.09f);
        RenderSettings.fogColor = new Color(0.62f, 0.45f, 0.38f);
        Material sky = RenderSettings.skybox;
        if (sky != null && gdExp > 0f && sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", gdExp * 0.55f);
        if (sky != null && sky.HasProperty("_Tint")) sky.SetColor("_Tint", new Color(0.62f, 0.45f, 0.4f));
    }

    // ffu32b: each scripted press fires exactly once, the first time gdT crosses its time (SwiftShader frames can be
    // longer than a 0.2 s window, so a window test skipped presses)
    bool GdPress(int bit, float at) { if (gdT < at || (gdFired & (1 << bit)) != 0) return false; gdFired |= 1 << bit; return true; }

    PIn GardenDemoInput(PIn i)
    {
        var o = new PIn();
        float t = gdT;
        if (gdPhase < 1 && GdPress(0, 2f)) o.use = true;                        // light the fire (hotspot)
        if (t > 23f && t < 31f) o.move = new Vector2(Mathf.Sin(t * 2.4f), 0f);    // rock the hammock
        if (GdPress(1, 42.6f)) o.hop = true;                                // hop out
        if (GdPress(2, 46.5f)) o.use = true;                                // douse
        return o;
    }

    PIn HorseDemoInput(PIn i)
    {
        var o = new PIn();
        if (gdHorse == null) return o;
        float t = gdT;
        Rect P = RanchLife.Pasture;
        Vector3 gate = PastureGate.Outside;
        Vector3 far = gate + new Vector3(55f, 0f, -8f);
        Vector3 goal = t < 20f ? gate + new Vector3(4f, 0f, 0f) : far;
        PIn s = gdHorse.SteerTo(goal, 0.5f, false);
        o.move.x = s.move.x;
        if (t < 3f) o.move.y = 0f;
        else if (t < 8f) o.move.y = 0.2f;          // walk
        else if (t < 13f) o.move.y = 0.6f;         // trot
        else if (t < 21f) o.move.y = 0.88f;        // canter (through the gate)
        else if (t < 29f) { o.move.y = 1f; o.boostHeld = true; }   // gallop
        else if (t < 32f) o.move.y = 0.9f;
        else if (t < 36f) o.move.y = 0f;           // slow to a stop
        if (GdPress(3, 26.5f)) o.hop = true;  // jump at the gallop
        if (GdPress(4, 37f)) o.hop = true;    // rear + whinny (standing)
        return o;
    }

    bool GardenDemoCam(Frog f, string sc, out Vector3 pos, out Vector3 look)
    {
        pos = look = Vector3.zero;
        float dt = Time.deltaTime;
        gdT += dt;
        gdLog -= Time.unscaledDeltaTime;
        if (sc == "garden")
        {
            Garden g = Garden.I;
            if (g == null) return false;
            int ph = gdT < 8f ? 0 : gdT < 20f ? 1 : gdT < 32f ? 2 : gdT < 43f ? 3 : 4;
            if (ph != gdPhase)
            {
                gdPhase = ph;
                if (ph == 1)
                {
                    // James into a hammock, the others onto the swings
                    if (f.vehicle != null) f.ExitVehicle();
                    if (g.hammocks.Count > 0) f.EnterVehicle(g.hammocks[1]);
                    for (int i = 1; i < 4; i++) if (frogs[i] != null && i - 1 < g.swings.Count) frogs[i].AiSit(g.swings[(i + 0) % g.swings.Count], 200f);
                    if (Campfire.I != null && !Campfire.I.Lit) Campfire.I.Light(f);
                }
                if (ph == 4)
                {
                    // hop out (t 43.5) -> back by the ring to douse it (t 46.5)
                    if (f.vehicle != null) f.ExitVehicle();
                    f.SendTo(WorldId.Ranch, g.WG(5f, -0.9f) + Vector3.up * 0.1f, g.Yaw);
                }
                Debug.Log("FFDEMO garden phase " + ph + " t=" + gdT.ToString("0.0") + " rt=" + Time.realtimeSinceStartup.ToString("0.0"));
            }
            Vector3 fire = g.FirePos;
            switch (ph)
            {
                case 0: pos = fire + g.Fwd * -3.4f + Quaternion.Euler(0f, g.Yaw, 0f) * new Vector3(1.6f, 1.4f, 0f); look = fire + Vector3.up * 0.6f; break;
                case 1: pos = g.WG(-2f, -17.5f, 7.5f); look = g.WG(-1f, 2f, 0.6f); break;
                case 2:
                    {
                        Transform h = g.hammocks.Count > 1 ? g.hammocks[1].transform : g.transform;
                        pos = h.position + h.right * 3.2f + h.forward * -1.2f + Vector3.up * 2.4f; look = h.position + Vector3.up * 0.9f; break;
                    }
                case 3: pos = fire + Quaternion.Euler(0f, g.Yaw, 0f) * new Vector3(0f, 2.6f, -8.2f); look = fire + Vector3.up * 0.8f; break;
                default: pos = fire + Quaternion.Euler(0f, g.Yaw + 30f, 0f) * new Vector3(0f, 2.2f, -5.5f); look = fire + Vector3.up * 0.5f; break;
            }
            if (gdLog <= 0f)
            {
                gdLog = 1f;
                int busy = 0; foreach (var s in GardenSeat.Seats) if (s.driver != null) busy++;
                Debug.Log("FFDEMO garden t=" + gdT.ToString("0.0") + " phase=" + ph + " fire=" + (Campfire.I != null && Campfire.I.Lit) + " seats=" + busy + " p1=" + (f.vehicle != null ? f.vehicle.Title : "foot") + " rt=" + Time.realtimeSinceStartup.ToString("0.0"));
            }
            return true;
        }
        // horseride: chase camera on the horse's left, eased
        if (gdHorse == null) return false;
        Transform ht = gdHorse.transform;
        Vector3 fw = ht.forward; fw.y = 0f; fw.Normalize();
        Vector3 lf = new Vector3(-fw.z, 0f, fw.x);
        Vector3 want = ht.position + lf * 6.5f - fw * 4.5f + Vector3.up * 2.6f;
        if (gdT > 36f) want = ht.position + lf * 5.5f + fw * 3.5f + Vector3.up * 1.8f;   // rearing: front three-quarter
        want.y = Mathf.Max(want.y, Ranch.GY(want.x, want.z) + 1.2f);
        gdCam = gdCam == Vector3.zero || (gdCam - want).sqrMagnitude > 900f ? want : Vector3.Lerp(gdCam, want, Mathf.Min(1f, dt * 2.2f));
        pos = gdCam; look = ht.position + Vector3.up * 1.3f;
        if (gdLog <= 0f)
        {
            gdLog = 0.5f;
            Debug.Log("FFDEMO horse t=" + gdT.ToString("0.0") + " gait=" + gdHorse.GaitName + " v=" + gdHorse.Speed.ToString("0.0") + " pos=" + ht.position.ToString("0.0") + " rider=" + (gdHorse.driver != null ? gdHorse.driver.nick : "-") + " hoof=" + Horse.HoofCount + " rt=" + Time.realtimeSinceStartup.ToString("0.0"));
        }
        return true;
    }
}
