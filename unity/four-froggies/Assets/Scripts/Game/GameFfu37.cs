using UnityEngine;

// ffu37 demo shots for the probe (?ffdemo=1&ffshot=...):
//   pondsink   James's 10-story mech walks south off the north shore into the deep middle of the pond (x 40), stands on the
//              bed, turns round and walks back out up the bank; at the same time Dad's Cybertruck (no driver) and the
//              Optimus mech suit are dropped into deep water nearby and sink to the bed. Camera beside the mech (under
//              water once it's deep). Logs "FFDEMO pondsink t ... mech y / bed / depth / subMax ..." every 0.5 s.
//   pondtruck  P1 drives James's Cybertruck off the north lawn into the pond at full throttle (no Cyberboat), it sinks to
//              the bed, crawls on, turns round and climbs back out up the bank. Logs "FFDEMO pondtruck ...".
//   breach     P1 in the submarine deep on the reef under the pond's west half (local -25, -10), full UP: the ballast
//              blow builds the climb to ~11 m/s, it breaches in the ranch pond, arcs, falls back, sinks back down.
//              Slow motion (x0.35) while breaching; side camera on the ranch pond. Twice (2nd from 12 m for a lower one).
//              Logs "FFDEMO breach ..." and the Submarine: BREACH / re-entry / breach over lines (max height above water).
public partial class Game
{
    static bool Is37(string sc) { return sc == "pondsink" || sc == "pondtruck" || sc == "breach"; }
    float p7T, p7Log;
    StoryMech p7Mech;
    Vehicle p7Truck, p7Suit;
    Submarine p7Sub;
    int p7Round;
    float p7RoundT, p7MaxDepthMech, p7MaxDepthTruck, p7MaxDepthSuit;
    Vector3 p7Cam;
    bool p7Dropped;

    void Demo37Start(Frog f, string sc)
    {
        if (f.vehicle != null) f.ExitVehicle();
        if (!Roster.IsFrog(f.charId) || f.charId != 0) SetSeatChar(f.id, 0, false);
        demoKeepChars = true;
        p7T = 0f; p7Log = 0f; p7Round = 0; p7RoundT = 0f; p7Dropped = false; p7Cam = Vector3.zero;
        p7MaxDepthMech = p7MaxDepthTruck = p7MaxDepthSuit = 0f;
        Debug.Log("FFDEMO " + sc + " start; pond: WaterY " + Layout.WaterY + ", deepest bed " + DeepestBed().ToString("0.0") + " m");
        if (sc == "breach")
        {
            if (UnderwaterWorld.I == null) { Debug.Log("FFDEMO breach: no underwater world"); return; }
            if (f.world != WorldId.Ranch) f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f);
            UnderwaterWorld.I.Dive(f);
            p7Sub = UnderwaterWorld.I.sub;
            BreachPlace(-25f, -10f, -1.5f);
            demoHook = BreachInput;
            return;
        }
        if (f.world != WorldId.Ranch) f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f);
        if (sc == "pondsink")
        {
            p7Mech = FindMech(0, 0);
            if (p7Mech == null) { Debug.Log("FFDEMO pondsink: no mech"); return; }
            StoryMech.DtCap = 0.3f;
            p7Mech.DemoPlace(new Vector3(40f, 0f, -12f), 180f);
            f.EnterVehicle(p7Mech);
            foreach (var v in Vehicle.All) if (v != null && v.driver == null && v.Title == "Cybertruck" && v.HomePos.z > 0f && v.HomePos.z < 30f) { p7Truck = v; break; }
            foreach (var v in Vehicle.All) if (v != null && v.Title.StartsWith("Optimus")) { p7Suit = v; break; }
            demoHook = PondSinkInput;
        }
        else
        {
            foreach (var v in Vehicle.All) if (v != null && v.Title == "James's Cybertruck") { p7Truck = v; break; }
            if (p7Truck == null) { Debug.Log("FFDEMO pondtruck: no truck"); return; }
            Vector3 p = new Vector3(40f, 0f, -8f); p.y = Ranch.GY(p.x, p.z) + 0.8f;
            Place(p7Truck, p, 180f, Vector3.zero);
            f.EnterVehicle(p7Truck);
            p7Truck.inputHook = PondTruckInput;
        }
    }

    static float DeepestBed()
    {
        float lo = 0f;
        for (float x = Layout.PondC.x - Layout.PondR.x; x <= Layout.PondC.x + Layout.PondR.x; x += 2f)
            for (float z = Layout.PondC.y - Layout.PondR.y; z <= Layout.PondC.y + Layout.PondR.y; z += 2f)
                if (Layout.InPond(x, z)) lo = Mathf.Min(lo, Ranch.GY(x, z) - Layout.WaterY);
        return lo;
    }

    static void Place(Vehicle v, Vector3 p, float yaw, Vector3 vel)
    {
        v.enabled = true;
        if (v.rb == null) return;
        v.rb.isKinematic = false;
        v.rb.velocity = vel; v.rb.angularVelocity = Vector3.zero;
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        v.rb.position = p; v.rb.rotation = q; v.transform.SetPositionAndRotation(p, q);
    }

    // ---------------- pondsink ----------------
    PIn PondSinkInput(PIn i)
    {
        var o = new PIn();
        float t = p7T;
        if (t > 2f && t < 26f) o.move = new Vector2(0f, 1f);          // south into the pond
        else if (t >= 30f && t < 33.2f) o.move = new Vector2(1f, 0f);  // turn round on the bed
        else if (t >= 33.2f) o.move = new Vector2(0f, 1f);            // north, up the bank and out
        return o;
    }

    // ---------------- pondtruck ----------------
    PIn PondTruckInput(PIn i)
    {
        var o = new PIn();
        float t = p7T;
        var v = p7Truck;
        if (v == null) return o;
        float yaw = v.transform.eulerAngles.y;
        if (t < 1.5f) return o;
        if (t < 26f) { o.gas = 1f; o.move.x = Mathf.Clamp(Mathf.DeltaAngle(yaw, 180f) / 25f, -1f, 1f); }
        else if (t < 34f && Mathf.Abs(Mathf.DeltaAngle(yaw, 0f)) > 20f) { o.gas = 0.8f; o.move.x = 1f; }
        else { o.gas = 1f; o.move.x = Mathf.Clamp(Mathf.DeltaAngle(yaw, 0f) / 25f, -1f, 1f); }
        return o;
    }

    // ---------------- breach ----------------
    void BreachPlace(float lx, float lz, float upFromBed)
    {
        if (p7Sub == null) return;
        float y = Layout.SubSeaY(lx, lz) + 2.6f + upFromBed;
        Vector3 p = Worlds.UnderO + new Vector3(lx, y, lz);
        p7Sub.rb.velocity = Vector3.zero;
        p7Sub.rb.position = p; p7Sub.transform.position = p;
        p7RoundT = 0f;
        Debug.Log("FFDEMO breach round " + (p7Round + 1) + ": sub at reef (" + lx + ", " + lz + ") depth " + (-y).ToString("0.0") + " m (seabed " + (-Layout.SubSeaY(lx, lz)).ToString("0.0") + " m)");
    }

    PIn BreachInput(PIn i)
    {
        var o = new PIn();
        if (p7Sub == null) return o;
        if (p7RoundT > 1.5f && !p7Sub.Breaching && p7RoundT < 30f && p7Sub.driver != null && p7Sub.driver.world == WorldId.Underwater && !after) o.climb = 1f;
        return o;
    }
    bool after;

    bool Demo37Cam(Frog f, string sc, out Vector3 pos, out Vector3 look)
    {
        pos = look = Vector3.zero;
        float dt = Mathf.Min(Time.deltaTime, 0.34f);
        p7T += dt; p7RoundT += dt;
        p7Log -= Time.unscaledDeltaTime;
        if (sc == "breach") return BreachCam(f, out pos, out look);
        if (sc == "pondsink")
        {
            if (!p7Dropped && p7T > 6f)
            {
                p7Dropped = true;
                // into deep water beside the mech's path: the empty Cybertruck rolls off a "ledge" 1 m up, the suit too
                if (p7Truck != null) Place(p7Truck, new Vector3(52f, Layout.WaterY + 1.2f, -66f), 160f, new Vector3(0f, -1f, -6f));
                if (p7Suit != null) Place(p7Suit, new Vector3(28f, Layout.WaterY + 1.5f, -70f), 200f, new Vector3(0f, -1f, -3f));
                Debug.Log("FFDEMO pondsink: dropped " + (p7Truck != null ? p7Truck.Title : "-") + " + " + (p7Suit != null ? p7Suit.Title : "-") + " into deep water");
            }
            if (p7Mech == null) return false;
            Vector3 mp = p7Mech.transform.position;
            float H = p7Mech.height;
            Vector3 want = mp + new Vector3(H * 1.15f, H * 0.45f, H * 0.55f);
            float wy = Layout.WaterY;
            // keep the camera out of the bank
            float gy = Ranch.GY(want.x, want.z) + 1.5f; if (want.y < gy) want.y = gy;
            p7Cam = p7Cam == Vector3.zero ? want : Vector3.Lerp(p7Cam, want, Mathf.Min(1f, Time.unscaledDeltaTime * 2f));
            pos = p7Cam; look = mp + Vector3.up * H * 0.45f;
            if (p7Log <= 0f)
            {
                p7Log = 0.5f;
                float bed = Ranch.GY(mp.x, mp.z);
                p7MaxDepthMech = Mathf.Max(p7MaxDepthMech, wy - mp.y);
                string s = "FFDEMO pondsink t=" + p7T.ToString("0.0") + " mech y=" + mp.y.ToString("0.00") + " bed=" + bed.ToString("0.00") + " feet-bed=" + (mp.y - bed).ToString("0.00") + " depth=" + (wy - bed).ToString("0.0") + " subMax=" + (-Layout.SubSeaY(mp.x - Layout.PondC.x, mp.z - Layout.PondC.y)).ToString("0.0") + " wade=" + StoryMech.PondWade(mp, H).ToString("0.00") + " pos=(" + mp.x.ToString("0") + "," + mp.z.ToString("0") + ")";
                s += VehLog("truck", p7Truck, ref p7MaxDepthTruck) + VehLog("suit", p7Suit, ref p7MaxDepthSuit);
                s += " | max depth mech " + p7MaxDepthMech.ToString("0.0") + " truck " + p7MaxDepthTruck.ToString("0.0") + " suit " + p7MaxDepthSuit.ToString("0.0") + " camUnder=" + (pos.y < wy && Layout.InPond(pos.x, pos.z));
                Debug.Log(s);
            }
            return true;
        }
        // pondtruck: chase cam on the right rear
        if (p7Truck == null) return false;
        Transform tt = p7Truck.transform;
        Vector3 fw = tt.forward; fw.y = 0f; fw.Normalize();
        Vector3 rt = new Vector3(fw.z, 0f, -fw.x);
        Vector3 tp = tt.position;
        Vector3 w2 = tp - fw * 7f + rt * 5f + Vector3.up * 3f;
        float g2 = Ranch.GY(w2.x, w2.z) + 1.2f; if (w2.y < g2) w2.y = g2;
        p7Cam = p7Cam == Vector3.zero ? w2 : Vector3.Lerp(p7Cam, w2, Mathf.Min(1f, Time.unscaledDeltaTime * 2.5f));
        pos = p7Cam; look = tp + Vector3.up * 0.8f;
        if (p7Log <= 0f)
        {
            p7Log = 0.5f;
            Debug.Log("FFDEMO pondtruck t=" + p7T.ToString("0.0") + VehLog("truck", p7Truck, ref p7MaxDepthTruck) + " v=" + p7Truck.Speed.ToString("0.0") + " vy=" + p7Truck.Velocity.y.ToString("0.0") + " boat=" + (((GroundVehicle)p7Truck).amph != null ? ((GroundVehicle)p7Truck).amph.k.ToString("0.00") : "-") + " | max depth " + p7MaxDepthTruck.ToString("0.0"));
        }
        return true;
    }

    static string VehLog(string tag, Vehicle v, ref float maxDepth)
    {
        if (v == null) return " | " + tag + " -";
        Vector3 p = v.transform.position;
        float bed = Ranch.GY(p.x, p.z);
        float bottom = v.body != null ? v.body.bounds.min.y : p.y;
        if (Layout.InPond(p.x, p.z)) maxDepth = Mathf.Max(maxDepth, Layout.WaterY - bottom);
        return " | " + tag + " y=" + p.y.ToString("0.00") + " bottom=" + bottom.ToString("0.00") + " bed=" + bed.ToString("0.00") + " bottom-bed=" + (bottom - bed).ToString("0.00") + " depth=" + (Layout.WaterY - bed).ToString("0.0") + " subMax=" + (-Layout.SubSeaY(p.x - Layout.PondC.x, p.z - Layout.PondC.y)).ToString("0.0") + " sub=" + v.pondSub.ToString("0.00") + " pos=(" + p.x.ToString("0") + "," + p.z.ToString("0") + ")";
    }

    bool BreachCam(Frog f, out Vector3 pos, out Vector3 look)
    {
        pos = look = Vector3.zero;
        var s = p7Sub;
        if (s == null) return false;
        Time.timeScale = s.Breaching ? 0.35f : 1f;
        if (p7Log <= 0f)
        {
            p7Log = s.Breaching ? 0.15f : 0.5f;
            Vector3 p = s.transform.position;
            Debug.Log("FFDEMO breach round " + (p7Round + 1) + " t=" + p7RoundT.ToString("0.0") + (s.Breaching ? " RANCH y-above-water=" + (p.y - Layout.WaterY).ToString("0.00") + " vy=" + s.rb.velocity.y.ToString("0.0") : " under depth=" + (-(p - Worlds.UnderO).y).ToString("0.0") + " vy=" + s.rb.velocity.y.ToString("0.0") + " blow=" + s.BlowK.ToString("0.00")) + " world=" + (s.driver != null ? s.driver.world.ToString() : "-") + " breaches=" + Submarine.Breaches + " lastMax=" + Submarine.LastMaxH.ToString("0.00") + "/" + Submarine.LastMaxBottom.ToString("0.00"));
        }
        // next round: 8 s after the first breach ended, from a shallower start (lower breach)
        if (!s.Breaching && Submarine.Breaches > p7Round)
        {
            if (!after) { after = true; p7RoundT = 0f; }
            else if (p7RoundT > 8f && p7Round == 0) { p7Round = 1; after = false; BreachPlace(-25f, -10f, 7.5f); }
        }
        if (!s.Breaching) return false;   // under water: the normal follow camera
        Vector3 sp = Submarine.LastSpot;
        Vector3 side = new Vector3(0.8f, 0f, -0.6f).normalized;
        pos = new Vector3(sp.x, Layout.WaterY + 2.2f, sp.z) + side * 24f;
        look = new Vector3(sp.x, Layout.WaterY + 3f, sp.z);
        return true;
    }
}
