using UnityEngine;

// ffu19 loop-joint demo shots (?ffdemo=1&ffshot=...). Work with the new branch and with ?loopold=1 (the ffu18 loop), so
// the same URL gives before / after pictures:
//   lj-in-side / lj-in-chase    the joint where the approach straight turns into the loop (side view / driver's view)
//   lj-out-side / lj-out-chase  the joint where the loop comes back down onto the exit straight (side / looking back)
//   lj-link                     where the link from the figure-eight meets the approach straight (ffu18: 7 m link -> 6 m runway)
//   lj-fork / lj-merge          the branch leaving / rejoining the figure-eight
//   loopdrive / loopdrive-side  P1 drives a Cybertruck through the loop at full throttle (chase cam rolls with the truck /
//                               fixed side camera), slow motion 0.3x; logs "FFDEMO loopdrive ..." with per-section physics
//                               numbers (wheels on the ground, accelerometer along the truck's up axis, airborne steps)
public partial class Game
{
    GroundVehicle ldCar;
    float ldLogT, ldStartT;
    bool ldTop, ldDone;
    Vector3 ldLastV;
    string ldSec = "";
    int ldSteps, ldAir, ldMinW;
    float ldAMin, ldAMax, ldYMin;

    void DemoLoopStart(Frog f, string sc)
    {
        if (f.world != WorldId.Ranch) f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f);
        if (!sc.StartsWith("loopdrive")) return;
        if (f.vehicle != null) f.ExitVehicle();
        GroundVehicle best = null; float bd = 1e18f;
        foreach (var v in Vehicle.All)
        {
            var g = v as GroundVehicle;
            if (g == null || g.driver != null || g.tracked || !g.Title.Contains("Cybertruck")) continue;
            float d = (v.transform.position - RallyTrack.LinkJoin).sqrMagnitude;
            if (d < bd) { bd = d; best = g; }
        }
        if (best == null) { Debug.Log("FFDEMO loopdrive: no free Cybertruck"); return; }
        best.enabled = true;
        Vector3 p = RallyTrack.LinkJoin + new Vector3(2f, 0.9f, 0f);
        Quaternion q = Quaternion.LookRotation(Vector3.right, Vector3.up);
        best.rb.isKinematic = false; best.rb.angularVelocity = Vector3.zero;
        best.rb.position = p; best.rb.rotation = q; best.transform.SetPositionAndRotation(p, q);
        best.rb.velocity = Vector3.right * 14f;
        f.EnterVehicle(best);
        best.inputHook = DemoLoopPilot;
        best.fixedProbe = DemoLoopProbe;
        ldCar = best; ldTop = false; ldDone = false; ldSec = ""; ldLastV = best.rb.velocity; ldStartT = Time.time; ldLogT = 0f;
        Time.timeScale = 0.3f;
        Debug.Log("FFDEMO loopdrive start " + best.Title + " at " + p.ToString("F1") + (RallyTrack.Legacy ? " (legacy loop)" : "") +
                  " joints in " + RallyTrack.JointIn.ToString("F1") + " out " + RallyTrack.JointOut.ToString("F1"));
    }

    PIn DemoLoopPilot(PIn i)
    {
        var o = new PIn();
        var c = ldCar;
        if (c == null) return o;
        Vector3 pos = c.transform.position;
        if (ldTop && pos.x > RallyTrack.JointOut.x + 24f)
        {
            if (c.ForwardSpeed > 0.5f) o.brake = 1f;
            if (!ldDone) { ldDone = true; Time.timeScale = 1f; DemoLoopFlush(); Debug.Log("FFDEMO loopdrive done t=" + (Time.time - ldStartT).ToString("0.0")); }
            return o;
        }
        o.gas = 1f;
        // steer along the lane on the flat parts (the loop's own guide / rails do the rest)
        Vector3 lp, lf, lr, lu; float arc;
        if (!RallyTrack.Legacy && RallyTrack.BranchNearest(pos, out lp, out lf, out lr, out lu, out arc))
        {
            if (lu.y > 0.97f)
            {
                Vector3 fw = c.transform.forward; fw.y = 0f; lf.y = 0f;
                float ang = Vector3.SignedAngle(fw, lf, Vector3.up);
                float e = Vector3.Dot(pos - lp, lr);
                o.move.x = Mathf.Clamp(ang / 25f - e * 0.25f, -1f, 1f);
            }
        }
        else
        {
            // ffu18 layout: entry runway on z = LoopC.y, exit runway on z = LoopC.y + 7.5, both along +x
            float zLane = ldTop ? RallyTrack.JointOut.z : RallyTrack.JointIn.z;
            if (pos.y < RallyTrack.JointIn.y + 1.5f)
            {
                float yaw = c.transform.eulerAngles.y;
                o.move.x = Mathf.Clamp(Mathf.DeltaAngle(yaw, 90f) / 25f + (zLane - pos.z) * 0.25f, -1f, 1f);
            }
        }
        return o;
    }

    void DemoLoopProbe(GroundVehicle g, int wheels)
    {
        if (g != ldCar || ldDone) return;
        float dt = Time.fixedDeltaTime;
        Vector3 v = g.rb.velocity;
        Vector3 acc = (v - ldLastV) / dt; ldLastV = v;
        float aUp = Vector3.Dot(acc - Physics.gravity, g.transform.up);   // accelerometer along the truck's up (9.8 at rest)
        Vector3 p = g.rb.position;
        float baseY = RallyTrack.JointIn.y;
        if (p.y > baseY + 9f) ldTop = true;
        string sec = p.y > baseY + 2.5f ? "loop" : (!ldTop ? (p.x < RallyTrack.JointIn.x - 4f ? "approach" : "entry-joint") : (p.x < RallyTrack.JointOut.x + 4f ? "exit-joint" : "exit"));
        if (sec != ldSec) { DemoLoopFlush(); ldSec = sec; ldSteps = 0; ldAir = 0; ldMinW = 99; ldAMin = 1e9f; ldAMax = -1e9f; ldYMin = 1e9f; }
        ldSteps++;
        if (wheels == 0) ldAir++;
        ldMinW = Mathf.Min(ldMinW, wheels);
        if (ldSteps > 1) { ldAMin = Mathf.Min(ldAMin, aUp); ldAMax = Mathf.Max(ldAMax, aUp); }
        ldYMin = Mathf.Min(ldYMin, p.y);
        if (Time.time - ldLogT > 0.25f)
        {
            ldLogT = Time.time;
            Debug.Log("FFDEMO loopdrive t=" + (Time.time - ldStartT).ToString("0.00") + " " + sec + " pos " + p.ToString("F1") + " v " + v.magnitude.ToString("0.0") +
                      " wheels " + wheels + " aUp " + aUp.ToString("0.0") + " up " + g.transform.up.ToString("F2"));
        }
    }

    void DemoLoopFlush()
    {
        if (ldSec.Length == 0 || ldSteps == 0) return;
        Debug.Log("FFDEMO loopdrive section " + ldSec + " steps " + ldSteps + " airborne " + ldAir + " minWheels " + ldMinW +
                  " aUp " + ldAMin.ToString("0.0") + ".." + ldAMax.ToString("0.0") + " minY " + ldYMin.ToString("0.00"));
    }

    // sets the demo camera itself; false = let DemoView carry on
    bool DemoLoopCam(Frog f, string sc, Camera c)
    {
        Vector3 jin = RallyTrack.JointIn, jout = RallyTrack.JointOut, lj = RallyTrack.LinkJoin;
        Vector3 pos, look, up = Vector3.up;
        switch (sc)
        {
            case "lj-in-side": pos = jin + new Vector3(2f, 3f, -17f); look = jin + new Vector3(2f, 3.2f, 0f); break;
            case "lj-in-chase": pos = jin + new Vector3(-14f, 2.6f, 0f); look = jin + new Vector3(6f, 2.6f, 0f); break;
            case "lj-out-side": pos = jout + new Vector3(-2f, 3f, 17f); look = jout + new Vector3(-2f, 3.2f, 0f); break;
            case "lj-out-chase": pos = jout + new Vector3(14f, 2.6f, 0f); look = jout + new Vector3(-6f, 2.6f, 0f); break;
            case "lj-link": pos = lj + new Vector3(-4f, 4f, 10f); look = lj + new Vector3(4f, 0.3f, -1f); break;
            case "lj-fork":
                {
                    Vector3 a = Layout.TrackPoint(Layout.EntryT - 0.22f), b = Layout.TrackPoint(Layout.EntryT + 0.12f);
                    pos = new Vector3(a.x, Ranch.GY(a.x, a.z) + 3.6f, a.z); look = new Vector3(b.x, Ranch.GY(b.x, b.z) + 0.8f, b.z); break;
                }
            case "lj-merge":
                {
                    Vector2 a = Layout.LoopExitPath[6]; Vector3 b = Layout.TrackPoint(Layout.ExitT + 0.1f);
                    pos = new Vector3(a.x, Ranch.GY(a.x, a.y) + 3.2f, a.y); look = new Vector3(b.x, Ranch.GY(b.x, b.z) + 1.2f, b.z); break;
                }
            case "loopdrive-side":
                {
                    Vector2 lc = Layout.LoopC;
                    pos = new Vector3(lc.x, jin.y + 8f, lc.y - 36f); look = new Vector3(lc.x, jin.y + 7.5f, lc.y + 4.5f); break;
                }
            case "loopdrive":
                {
                    if (ldCar == null) return false;
                    Transform t = ldCar.transform;
                    pos = t.position - t.forward * 9f + t.up * 3.2f; look = t.position + t.forward * 3f + t.up * 1f; up = t.up; break;
                }
            default: return false;
        }
        c.transform.position = pos;
        c.transform.LookAt(look, up);
        return true;
    }
}
