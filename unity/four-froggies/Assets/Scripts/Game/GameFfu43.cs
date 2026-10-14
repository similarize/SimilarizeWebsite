using UnityEngine;

// ffu43 demo shot for the probe (?ffdemo=1&ffshot=mtrack): the rebuilt loop branch both ways + the Monster Park.
//   phase 0 "loopF"   : a Cybertruck starts on the branch just past the fork (arc 6 m) and drives it FORWARD: banked
//                       U-turn, approach, loop, exit, merge back onto the figure-eight (chase cam along the road).
//   phase 1 "loopR"   : the same truck starts on the merge end of the branch and drives it BACKWARDS: S-bend, the loop
//                       from the exit lane, the approach, the U-turn the other way, out at the fork.
//   phase 2 "wall"    : wall of death: the truck enters through the opening and circles on the curve / wall (20 s).
//   phase 3 "crusher" : wheel size 3x, the truck drives south over the car crusher row (cars flatten).
//   &mtonly=loopF|loopR|wall|crusher runs one phase. Logs "FFDEMO mtrack t phase pos arc v wheels up | ..." every 0.5 s
//   and a per-phase summary (loop: reached the top, min speed in the loop, exited the other end; wall: max height on
//   the wall, laps; crusher: cars crushed).
public partial class Game
{
    static bool Is43(string sc) { return sc == "mtrack"; }
    GroundVehicle m43Car; Frog m43F;
    int m43Phase = -1, m43Only = -1;
    float m43T, m43PhT, m43Log, m43MinLoopV, m43MaxY, m43WallH, m43Ang, m43Laps, m43LastAng;
    bool m43Top, m43InLoop, m43Done, m43LeftLoop;
    int m43Crush0;
    static readonly string[] M43Names = { "loopF", "loopR", "wall", "crusher", "done" };

    void Demo43Start(Frog f, string sc)
    {
        if (f.vehicle != null) f.ExitVehicle();
        if (f.world != WorldId.Ranch) f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f);
        m43F = f; m43T = 0f;
        foreach (var v in Vehicle.All)
        {
            var g = v as GroundVehicle;
            if (g == null || g.driver != null || g.tracked || !g.Title.Contains("Cybertruck")) continue;
            if (m43Car == null || g.Title == "Cybertruck") m43Car = g;
        }
        string on = UrlParam("mtonly");
        if (on != null) m43Only = System.Array.IndexOf(M43Names, on);
        Debug.Log("FFDEMO mtrack start: truck " + (m43Car != null ? m43Car.Title : "NONE") + " branch " + RallyTrack.BranchArcEnd.ToString("0") + " m, loop arcs " + RallyTrack.LoopArcA.ToString("0") + ".." + RallyTrack.LoopArcB.ToString("0") +
            " | U-turn R " + Layout.UTurnR.ToString("0.0") + " | park zones " + MonsterPark.Zones.Count);
        if (m43Car == null) return;
        BigWheels.Set(1f, "demo");
        M43Phase(m43Only >= 0 ? m43Only : 0);
        demoHook = Demo43Drive;
    }

    void M43Place(Vector3 p, Vector3 fwd, float speed)
    {
        var c = m43Car;
        if (c.driver != null && c.driver != m43F) c.driver.ExitVehicle();
        Quaternion q = Quaternion.LookRotation(new Vector3(fwd.x, 0f, fwd.z).normalized, Vector3.up);
        p += Vector3.up * (0.8f + c.bigLift);
        c.rb.isKinematic = false; c.rb.angularVelocity = Vector3.zero;
        c.rb.position = p; c.rb.rotation = q; c.transform.SetPositionAndRotation(p, q);
        c.rb.velocity = q * Vector3.forward * speed;
        if (m43F.vehicle != c) { if (m43F.vehicle != null) m43F.ExitVehicle(); m43F.EnterVehicle(c); }
    }

    void M43Phase(int ph)
    {
        if (m43Phase >= 0 && m43Phase < 4) M43Summary();
        if (m43Only >= 0 && ph != m43Only) ph = 4;
        m43Phase = ph; m43PhT = 0f; m43Top = false; m43InLoop = false; m43Done = false; m43LeftLoop = false;
        m43MinLoopV = 1e9f; m43MaxY = -1e9f; m43WallH = 0f; m43Laps = 0f; m43LastAng = float.NaN;
        Debug.Log("FFDEMO mtrack phase " + ph + " " + M43Names[ph] + " t=" + m43T.ToString("0.0"));
        Vector3 fw, up;
        switch (ph)
        {
            case 0: { Vector3 p = RallyTrack.BranchFrameAt(6f, out fw, out up); M43Place(p, fw, 9f); break; }
            case 1: { Vector3 p = RallyTrack.BranchFrameAt(RallyTrack.BranchArcEnd - 6f, out fw, out up); M43Place(p, -fw, 9f); break; }
            case 2: M43Place(new Vector3(MonsterPark.BowlX, Ranch.GY(MonsterPark.BowlX, MonsterPark.BowlZ + 18f), MonsterPark.BowlZ + 18f), Vector3.back, 6f); break;
            case 3:
                BigWheels.Set(3f, "demo");
                M43Place(new Vector3(MonsterPark.CrushX, Ranch.GY(MonsterPark.CrushX, MonsterPark.CrushZ0 + 9f), MonsterPark.CrushZ0 + 9f), Vector3.back, 0f);
                m43Crush0 = MonsterPark.Crushes; break;
            case 4: if (m43F.vehicle != null) m43F.ExitVehicle(); break;
        }
    }

    void M43Summary()
    {
        string s = "FFDEMO mtrack summary " + M43Names[m43Phase] + ":";
        if (m43Phase <= 1)
            s += " reached the top " + m43Top + " | min speed in the loop " + (m43MinLoopV < 1e8f ? m43MinLoopV.ToString("0.0") : "-") + " m/s | came out the other side " + m43LeftLoop + " | finished the branch " + m43Done + " | max y " + m43MaxY.ToString("0.0");
        else if (m43Phase == 2) s += " max height on the wall " + m43WallH.ToString("0.0") + " m | laps " + (m43Laps / 360f).ToString("0.00");
        else if (m43Phase == 3) s += " cars crushed " + (MonsterPark.Crushes - m43Crush0) + " | wheel size " + BigWheels.Label + " | tows " + MonsterPark.Tows;
        s += " | t " + m43PhT.ToString("0.0") + " s";
        Debug.Log(s);
    }

    // autopilot
    PIn Demo43Drive(PIn i)
    {
        var o = new PIn();
        var c = m43Car;
        if (c == null || m43Phase > 3) return o;
        Vector3 pos = c.transform.position;
        float spd = c.ForwardSpeed;
        if (m43Phase <= 1)
        {
            bool fwd = m43Phase == 0;
            Vector3 lp, lf, lr, lu; float arc;
            if (!RallyTrack.BranchNearest(pos, out lp, out lf, out lr, out lu, out arc)) return o;
            if (!fwd) { lf = -lf; lr = -lr; }
            bool nearLoop = arc > RallyTrack.LoopArcA - 26f && arc < RallyTrack.LoopArcB + 26f;
            float want = nearLoop ? 30f : 13f;
            o.gas = spd < want ? 1f : 0f;
            if (spd > want + 3f) o.brake = 0.4f;
            if (lu.y > 0.9f)
            {
                // steer onto the centreline a few metres ahead
                Vector3 ap, af, au; float la = arc + (fwd ? 5f : -5f);
                ap = RallyTrack.BranchFrameAt(la, out af, out au);
                Vector3 to = ap - pos; to.y = 0f;
                Vector3 hf = c.transform.forward; hf.y = 0f;
                float ang = Vector3.SignedAngle(hf, to, Vector3.up);
                o.move.x = Mathf.Clamp(ang / 22f, -1f, 1f);
            }
            return o;
        }
        if (m43Phase == 2)
        {
            // circle the bowl anticlockwise (seen from above) on the curve / wall, ~1.5 m inside the wall
            Vector3 cc = new Vector3(MonsterPark.BowlX, pos.y, MonsterPark.BowlZ);
            Vector3 rad = pos - cc; rad.y = 0f;
            float r = rad.magnitude;
            Vector3 tang = Vector3.Cross(Vector3.up, rad.normalized);   // left-handed: up x radial = clockwise from above... flip below
            tang = -tang;
            float target = MonsterPark.BowlR0 + MonsterPark.BowlCurve - 1.3f;
            Vector3 aim = r > 2f ? (tang - rad.normalized * Mathf.Clamp((target - r) * 0.25f, -0.6f, 0.6f)).normalized : Vector3.back;
            if (m43PhT < 2.5f || r > target + 4f) aim = (cc - pos).normalized;   // drive in through the opening first
            Vector3 hf = c.transform.forward; hf.y = 0f;
            float ang = Vector3.SignedAngle(hf, aim, c.transform.up);
            o.move.x = Mathf.Clamp(ang / 20f, -1f, 1f);
            o.gas = spd < 17f ? 1f : 0.2f;
            return o;
        }
        // crusher: straight south down the lane
        if (m43PhT < 2.5f) return o;
        {
            float yaw = c.transform.eulerAngles.y;
            o.move.x = Mathf.Clamp(Mathf.DeltaAngle(yaw, 180f + Mathf.Clamp((MonsterPark.CrushX - pos.x) * -6f, -20f, 20f)) / 20f, -1f, 1f);
            o.gas = spd < 5f ? 0.7f : 0.15f;
        }
        return o;
    }

    bool Demo43Cam(Frog f, string sc, out Vector3 pos, out Vector3 look)
    {
        pos = look = Vector3.zero;
        if (m43Phase < 0 || m43Car == null) return false;
        float dt = Mathf.Min(Time.deltaTime, 0.4f);
        m43T += dt; m43PhT += dt; m43Log -= dt;
        var c = m43Car;
        Vector3 p = c.transform.position;
        m43MaxY = Mathf.Max(m43MaxY, p.y);
        Vector3 bp, bf, br, bu; float arc = 0f;
        bool onBr = RallyTrack.BranchNearest(p, out bp, out bf, out br, out bu, out arc);
        if (m43Phase <= 1)
        {
            float a0 = RallyTrack.LoopArcA, a1 = RallyTrack.LoopArcB;
            bool inLoop = arc > a0 + 1f && arc < a1 - 1f && (bp - p).magnitude < 6f;
            if (inLoop) { m43InLoop = true; m43MinLoopV = Mathf.Min(m43MinLoopV, c.Speed > 0f ? c.Velocity.magnitude : 0f); if (p.y > RallyTrack.JointIn.y + 14f) m43Top = true; }
            if (m43InLoop && m43Top && ((m43Phase == 0 && arc > a1 + 3f) || (m43Phase == 1 && arc < a0 - 3f))) m43LeftLoop = true;
            bool end = m43Phase == 0 ? arc > RallyTrack.BranchArcEnd - 3f : arc < 3f;
            if (end && m43LeftLoop) { m43Done = true; M43Phase(m43Phase + 1); }
            else if (m43PhT > 75f) M43Phase(m43Phase + 1);
            // chase cam along the road, behind the truck in its direction of travel
            Vector3 cf, cu;
            float back = m43Phase == 0 ? -8f : 8f;
            Vector3 cp = RallyTrack.BranchFrameAt(arc + back, out cf, out cu);
            pos = cp + cu * 3.2f; look = p + c.transform.up * 1f + c.transform.forward * 2f;
        }
        else if (m43Phase == 2)
        {
            Vector3 cc = new Vector3(MonsterPark.BowlX, Ranch.GY(MonsterPark.BowlX, MonsterPark.BowlZ), MonsterPark.BowlZ);
            float h = p.y - cc.y; m43WallH = Mathf.Max(m43WallH, h);
            Vector3 rad = p - cc; rad.y = 0f;
            float ang = Mathf.Atan2(rad.z, rad.x) * Mathf.Rad2Deg;
            if (!float.IsNaN(m43LastAng) && rad.magnitude > 4f) m43Laps += Mathf.Abs(Mathf.DeltaAngle(m43LastAng, ang));
            m43LastAng = ang;
            if (m43PhT > 22f) M43Phase(3);
            pos = cc + new Vector3(0f, 13f, 20f); look = cc + Vector3.up * 2f;
        }
        else if (m43Phase == 3)
        {
            if (p.z < MonsterPark.CrushZ0 - 26f || m43PhT > 26f) M43Phase(4);
            pos = new Vector3(p.x + 13f, Ranch.GY(p.x, p.z) + 5f + c.bigLift, p.z - 3f); look = p + Vector3.up * 1.2f;
        }
        else
        {
            pos = new Vector3(-147f, 22f, -40f); look = new Vector3(-140f, 0f, -100f);
        }
        if (m43Log <= 0f && m43Phase <= 3)
        {
            m43Log = 0.5f;
            int g = 0; foreach (var w in c.wheels) if (w.grounded) g++;
            Debug.Log("FFDEMO mtrack t=" + m43T.ToString("0.0") + " " + M43Names[m43Phase] + " pos (" + p.x.ToString("0.0") + ", " + p.y.ToString("0.0") + ", " + p.z.ToString("0.0") + ") arc " + arc.ToString("0") +
                " v " + c.Velocity.magnitude.ToString("0.0") + " wheels " + g + " up " + c.transform.up.y.ToString("0.00") + " k " + c.bigK.ToString("0.00") + (m43Phase == 2 ? " wallH " + (p.y - Ranch.GY(MonsterPark.BowlX, MonsterPark.BowlZ)).ToString("0.0") : "") +
                (m43Phase == 3 ? " crushed " + (MonsterPark.Crushes - m43Crush0) : "") + " teeter " + MonsterPark.TeeterAngle.ToString("0"));
        }
        return true;
    }
}
