using UnityEngine;

// ffu42 demo shot for the probe (?ffdemo=1&ffshot=bigwheels): the wheel-size setting on a Cybertruck and a dirt bike.
//   A test lane on the south lawn (x 6 -> 46, z -6 truck / -1 bike) with a log across both lanes at x 22, a boulder in
//   each lane at x 30 and a second, thicker log at x 37 (all with colliders).
//   phase 0 "park1x"  0-7 s : both parked side by side at the start, wheels 1x, fixed side camera (comparison frame A)
//   phase 1 "park4x"  7-14 s: wheel size set to 4x, tyres inflate, same camera (comparison frame B)
//   phase 2 "truck1x"        : back to 1x, P1 drives the Cybertruck down the lane (gas 0.55, straight) over log / rock / log
//   phase 3 "truck4x"        : truck reset to the start, 4x, same run
//   phase 4 "bike1x" / 5 "bike4x": the same two runs on the 2-stroke dirt bike
//   phase 6 "done"           : both parked at 4x, camera on them
//   &bwmax=3 changes the big size (default 4). Logs "FFDEMO bigwheels t phase veh x h k up v grounded" every 0.5 s and a
//   per-run summary "FFDEMO bigwheels run <name>: reached x .. max air .. min up .. flips ..".
public partial class Game
{
    static bool Is42(string sc) { return sc == "bigwheels"; }
    GroundVehicle b42Truck; Bike b42Bike; Frog b42F;
    float b42T, b42PhaseT, b42Log, b42Max = 4f;
    int b42Phase = -1;
    float b42RunMaxH, b42RunMinUp, b42RunMaxX; int b42Flips; bool b42WasUp;
    const float B42X0 = 8f, B42X1 = 46f, B42ZT = -6f, B42ZB = -1f;
    static readonly string[] B42Names = { "park1x", "park4x", "truck1x", "truck4x", "bike1x", "bike4x", "done" };

    void Demo42Start(Frog f, string sc)
    {
        if (f.vehicle != null) f.ExitVehicle();
        if (f.world != WorldId.Ranch) f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f);
        b42F = f; b42T = 0f; b42Log = 0f;
        string mx = UrlParam("bwmax"); float m; if (mx != null && float.TryParse(mx, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out m)) b42Max = Mathf.Clamp(m, 1f, BigWheels.Max);
        foreach (var v in Vehicle.All)
        {
            var gv = v as GroundVehicle;
            if (gv == null || v.driver != null || !v.Title.Contains("Cybertruck") || gv.amph == null) continue;
            if (b42Truck == null || v.Title == "Cybertruck") b42Truck = gv;
        }
        foreach (var b in Bike.AllBikes) if (b.type == 0) b42Bike = b;
        var ai = FindObjectOfType<BikeAI>(); if (ai != null) ai.enabled = false;   // no AI froggy borrows the demo bike
        // obstacles: logs across both lanes + a boulder in each lane
        var root = new GameObject("BigWheels demo lane").transform;
        Material bark = Mats.Lit(new Color(0.36f, 0.25f, 0.15f)), stone = Mats.Lit(new Color(0.5f, 0.49f, 0.46f));
        B42Log(root, 22f, 0.28f, bark);
        B42Log(root, 37f, 0.42f, bark);
        foreach (float z in new[] { B42ZT + 0.5f, B42ZB })
        {
            var r = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            r.name = "Demo boulder"; r.transform.SetParent(root, true);
            r.transform.position = new Vector3(30f, Ranch.GY(30f, z) + 0.12f, z);
            r.transform.localScale = new Vector3(1.5f, 0.95f, 1.3f);
            r.GetComponent<Renderer>().sharedMaterial = stone;
        }
        Debug.Log("FFDEMO bigwheels start: truck " + (b42Truck != null ? b42Truck.Title : "NONE") + ", bike " + (b42Bike != null ? b42Bike.Title : "NONE") + ", max " + b42Max + "x, saved setting was " + BigWheels.Label);
        BigWheels.Set(1f, "demo");
        B42Phase(0);
        demoHook = Demo42Drive;
    }

    static void B42Log(Transform root, float x, float r, Material m)
    {
        var lg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        lg.name = "Demo log"; lg.transform.SetParent(root, true);
        float zc = (B42ZT + B42ZB) * 0.5f;
        lg.transform.position = new Vector3(x, Ranch.GY(x, zc) + r * 0.8f, zc);
        lg.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        lg.transform.localScale = new Vector3(r * 2f, 5.5f, r * 2f);   // 11 m long
        lg.GetComponent<Renderer>().sharedMaterial = m;
    }

    static void B42Park(Vehicle v, float x, float z)
    {
        if (v == null) return;
        if (v.driver != null) v.driver.ExitVehicle();
        Vector3 p = new Vector3(x, Ranch.GY(x, z) + 0.4f + v.bigLift, z);
        Quaternion q = Quaternion.Euler(0f, 90f, 0f);
        v.rb.velocity = Vector3.zero; v.rb.angularVelocity = Vector3.zero;
        v.rb.position = p; v.rb.rotation = q; v.transform.SetPositionAndRotation(p, q);
    }

    Vehicle B42Veh { get { return b42Phase == 4 || b42Phase == 5 ? (Vehicle)b42Bike : b42Truck; } }

    void B42Phase(int ph)
    {
        if (b42Phase >= 2 && b42Phase <= 5)
            Debug.Log("FFDEMO bigwheels run " + B42Names[b42Phase] + ": reached x " + b42RunMaxX.ToString("0.0") + (b42RunMaxX > 40f ? " (over all 3 obstacles)" : " (stopped short)") +
                " | max body height " + b42RunMaxH.ToString("0.00") + " m | min upright " + b42RunMinUp.ToString("0.00") + " | flips " + b42Flips);
        b42Phase = ph; b42PhaseT = 0f;
        b42RunMaxH = 0f; b42RunMinUp = 1f; b42RunMaxX = -1e9f; b42Flips = 0; b42WasUp = true;
        Debug.Log("FFDEMO bigwheels phase " + ph + " " + B42Names[ph] + " t=" + b42T.ToString("0.0"));
        switch (ph)
        {
            case 0: BigWheels.Set(1f, "demo"); B42Park(b42Truck, B42X0, B42ZT); B42Park(b42Bike, B42X0 + 1f, B42ZB + 1.6f); break;
            case 1: BigWheels.Set(b42Max, "demo"); break;
            case 2: case 3:
                BigWheels.Set(ph == 2 ? 1f : b42Max, "demo");
                B42Park(b42Bike, B42X0 - 10f, B42ZB + 6f);
                B42Park(b42Truck, B42X0, B42ZT);
                if (b42Truck != null) b42F.EnterVehicle(b42Truck);
                break;
            case 4: case 5:
                BigWheels.Set(ph == 4 ? 1f : b42Max, "demo");
                B42Park(b42Truck, B42X0 - 12f, B42ZT - 7f);
                B42Park(b42Bike, B42X0, B42ZB);
                if (b42Bike != null) b42F.EnterVehicle(b42Bike);
                break;
            case 6:
                BigWheels.Set(b42Max, "demo");
                B42Park(b42Truck, B42X1 - 4f, B42ZT); B42Park(b42Bike, B42X1 - 3f, B42ZB + 1.6f);
                break;
        }
    }

    // P1's input while a run is on: wait for the tyres to settle (4 s), then gas straight down the lane (+x)
    PIn Demo42Drive(PIn i)
    {
        var o = new PIn();
        if (b42Phase < 2 || b42Phase > 5) return o;
        Vehicle v = B42Veh;
        if (v == null || b42PhaseT < 4f) { o.brake = 0.4f; return o; }
        float yaw = v.transform.eulerAngles.y;
        float z = v.transform.position.z, zl = b42Phase >= 4 ? B42ZB : B42ZT;
        float want = 90f + Mathf.Clamp((z - zl) * 6f, -25f, 25f);   // steer back onto the lane line (+z is left when heading +x)
        o.move.x = Mathf.Clamp(Mathf.DeltaAngle(yaw, want) / 25f, -1f, 1f);
        o.gas = v.Speed < 7f ? 0.6f : 0.25f;
        return o;
    }

    bool Demo42Cam(Frog f, string sc, out Vector3 pos, out Vector3 look)
    {
        pos = look = Vector3.zero;
        if (b42Phase < 0) return false;
        float dt = Mathf.Min(Time.deltaTime, 0.4f);
        b42T += dt; b42PhaseT += dt; b42Log -= dt;
        Vehicle v = B42Veh;
        bool run = b42Phase >= 2 && b42Phase <= 5;
        if (run && v != null)
        {
            Vector3 p = v.transform.position;
            float h = p.y - Ranch.GY(p.x, p.z);
            b42RunMaxH = Mathf.Max(b42RunMaxH, h); b42RunMaxX = Mathf.Max(b42RunMaxX, p.x);
            float up = v.transform.up.y; b42RunMinUp = Mathf.Min(b42RunMinUp, up);
            if (up < 0.2f && b42WasUp) b42Flips++;
            b42WasUp = up > 0.5f;
        }
        if (b42Phase == 0 && b42PhaseT > 7f) B42Phase(1);
        else if (b42Phase == 1 && b42PhaseT > 7f) B42Phase(2);
        else if (run && v != null && (v.transform.position.x > B42X1 || b42PhaseT > 26f)) B42Phase(b42Phase + 1);
        // camera
        if (b42Phase <= 1)
        {
            Vector3 c = new Vector3(B42X0 + 0.5f, Ranch.GY(B42X0, -3f), (B42ZT + B42ZB) * 0.5f);
            pos = c + new Vector3(-6.5f, 4f, -15f);   // one camera for both comparison frames
            look = c + new Vector3(0.4f, 1.8f, 0f);
        }
        else if (run && v != null)
        {
            Vector3 p = v.transform.position;
            float gy = Ranch.GY(p.x, p.z);
            float back = b42Phase >= 4 ? 7.5f : 11f;
            pos = new Vector3(p.x - 2.5f, gy + 2.6f + v.bigLift * 0.6f, p.z - back - v.bigLift * 1.6f);
            look = new Vector3(p.x + 1.5f, gy + 0.9f + v.bigLift * 0.5f, p.z);
        }
        else
        {
            Vector3 c = new Vector3(B42X1 - 3.5f, Ranch.GY(B42X1 - 3.5f, -3f), -3f);
            pos = c + new Vector3(-9f, 4.5f, -14f); look = c + Vector3.up * 2f;
        }
        if (b42Log <= 0f)
        {
            b42Log = 0.5f;
            string s = "FFDEMO bigwheels t=" + b42T.ToString("0.0") + " phase " + B42Names[b42Phase] + " setting " + BigWheels.Label;
            foreach (Vehicle w in new Vehicle[] { b42Truck, b42Bike })
            {
                if (w == null) continue;
                Vector3 p = w.transform.position;
                float k = w is GroundVehicle ? ((GroundVehicle)w).bigK : w is Bike ? ((Bike)w).bigK : 1f;
                int g = 0;
                var gv = w as GroundVehicle; if (gv != null) foreach (var wh in gv.wheels) if (wh.grounded) g++;
                s += " | " + (w is Bike ? "bike" : "truck") + " x " + p.x.ToString("0.0") + " h " + (p.y - Ranch.GY(p.x, p.z)).ToString("0.00") + " k " + k.ToString("0.00") +
                     " lift " + w.bigLift.ToString("0.00") + " up " + w.transform.up.y.ToString("0.00") + " v " + w.Speed.ToString("0.0") + (gv != null ? " g " + g : "") + (w.driver != null ? " DRIVEN" : "");
            }
            Debug.Log(s);
        }
        return true;
    }
}
