using UnityEngine;

// ffu41 demo shot for the probe (?ffdemo=1&ffshot=moon), wall-clock phases (SwiftShader runs ~3 fps):
//   0  t~1 s  P1 boards the Starship in Earth orbit, targets the Moon and starts the auto-transfer (X)
//   1  transfer -> captured into Moon orbit (normal space camera)
//   2  8 s in Moon orbit, then Y = land: P1 + the other 3 froggies land on the Moon
//   3  25 s of 1/6 g hops on the regolith (side camera), P1 tees off (moon golf) and the flag goes up
//   4  P1 drives the moon buggy with the 3 others riding (chase camera, rooster tails)
//   5  the buggy is parked on the BUGGY BAY -> stowed in the Starship, everyone back to Moon orbit
//   Logs "FFDEMO moon t phase P1 world pos hAboveGround | ship | buggy speed grounded aboard".
public partial class Game
{
    static bool Is41(string sc) { return sc == "moon"; }
    Frog m41F; int m41Phase = -1, m41Tries; float m41T, m41PhT, m41Log, m41MaxH, m41HopY0, m41TryT = -99f;

    void Demo41Start(Frog f, string sc)
    {
        m41F = f; m41Phase = 0; m41Tries = 0; m41TryT = -99f; m41T = 0f; m41PhT = 0f; m41Log = 0f; m41MaxH = 0f;
        if (f.vehicle != null) f.ExitVehicle();
        demoHook = MoonInput;
        Debug.Log("FFDEMO moon start");
    }

    PIn MoonInput(PIn i)
    {
        var o = new PIn();
        if (m41Phase == 3) { o.hop = true; o.move = new Vector2(0.15f, 0.55f); }
        else if (m41Phase == 4) { float k = m41T - m41PhT; o.move = new Vector2(k < 6f ? 0f : 0.45f, 1f); o.gas = 1f; }
        return o;
    }

    bool Demo41Cam(Frog f, string sc, out Vector3 pos, out Vector3 look)
    {
        pos = look = Vector3.zero;
        var sw = SpaceWorld.I;
        if (sw == null || m41Phase < 0) return false;
        float dt = Time.unscaledDeltaTime;
        m41T += dt; m41Log -= dt;
        int moon = sw.Find("moon");
        var mw = MoonWorld.I;
        switch (m41Phase)
        {
            case 0:
                if (m41T > 1f)
                {
                    sw.ToOrbit(f, "earth");
                    m41Phase = 1; m41PhT = m41T;
                    Debug.Log("FFDEMO moon -> phase 1 auto-transfer Earth orbit -> Moon t=" + m41T.ToString("0.0"));
                }
                break;
            case 1:
                // start the auto-transfer a frame after boarding (ship placed in Earth orbit first); retry if it didn't take
                if (!sw.ship.InOrbitOf(moon) && sw.ship.mode != Starship.Mode.Transfer && m41T - m41PhT > 1.5f && m41Tries < 3 && m41T - m41TryT > 6f)
                {
                    m41Tries++; m41TryT = m41T;
                    try { sw.ship.DemoAutoTransfer(moon); Debug.Log("FFDEMO moon auto-transfer try " + m41Tries + " -> mode " + sw.ship.mode); }
                    catch (System.Exception e) { Debug.Log("FFDEMO moon auto-transfer try " + m41Tries + " threw " + e); }
                }
                if (sw.ship.InOrbitOf(moon)) { m41Phase = 2; m41PhT = m41T; Debug.Log("FFDEMO moon -> phase 2 in Moon orbit t=" + m41T.ToString("0.0") + " ship-moon " + (sw.ship.transform.position - sw.bodies[moon].pos).magnitude.ToString("0.0") + " m"); }
                else if (m41T - m41PhT > 70f) { sw.ToOrbit(f, "moon"); Debug.Log("FFDEMO moon: transfer timeout - placed in Moon orbit"); }
                break;
            case 2:
                if (m41T - m41PhT > 8f)
                {
                    sw.Land(moon);
                    int k = 1;
                    foreach (Frog g in frogs) if (g != null && g != f && k < 4) { MoonWorld.Land(g, k); k++; }
                    m41Phase = 3; m41PhT = m41T; m41HopY0 = f.transform.position.y;
                    Debug.Log("FFDEMO moon -> phase 3 landed: P1 world " + f.world + " froggies on the Moon " + k + " t=" + m41T.ToString("0.0"));
                }
                break;
            case 3:
                {
                    float h = f.transform.position.y - MoonWorld.GY(f.transform.position);
                    if (h > m41MaxH) m41MaxH = h;
                    if (mw != null && m41T - m41PhT > 3f && m41T - m41PhT < 3.5f) { mw.TeeOff(f); mw.PlantFlag(f); m41PhT -= 0.5f; }
                    if (m41T - m41PhT > 26f && mw != null)
                    {
                        Debug.Log("FFDEMO moon hops: max height above ground " + m41MaxH.ToString("0.00") + " m (Earth hop ~" + (Roster.JumpV(f.charId) * Roster.JumpV(f.charId) / (2f * Frog.Gravity)).ToString("0.00") + " m)");
                        mw.DemoBoardDriver(f);
                        foreach (Frog g in frogs) if (g != null && g != f && g.world == WorldId.Moon) mw.Ride(g);
                        m41Phase = 4; m41PhT = m41T;
                        Debug.Log("FFDEMO moon -> phase 4 buggy: driver " + (mw.buggy.driver != null ? mw.buggy.driver.nick : "-") + " aboard " + mw.Aboard);
                    }
                    Vector3 fp = f.transform.position;
                    if (m41T - m41PhT < 5f) { pos = fp + new Vector3(2.5f, 1.4f, -6f); look = fp + MoonWorld.EarthDir * 60f; }   // the Earth over the lander / base
                    else { pos = fp + new Vector3(7.5f, 2.2f, -1.5f); look = fp + new Vector3(0f, 1.2f, 1.5f); }
                    break;
                }
            case 4:
                {
                    var b = mw.buggy;
                    Vector3 bp = b.transform.position, fw = b.transform.forward; fw.y = 0f; fw.Normalize();
                    Vector3 rt = Vector3.Cross(Vector3.up, fw);
                    pos = bp - fw * 7.5f + rt * 4.5f + Vector3.up * 3.2f; look = bp + fw * 1.5f + Vector3.up * 0.8f;
                    if (m41T - m41PhT > 34f) { m41Phase = 5; m41PhT = m41T; mw.DemoParkInBay(); Debug.Log("FFDEMO moon -> phase 5 park in the BUGGY BAY t=" + m41T.ToString("0.0")); }
                    break;
                }
            case 5:
                if (f.world == WorldId.Moon && mw != null)
                {
                    if (m41T - m41PhT < 1f) mw.DemoParkInBay();
                    Vector3 bp = mw.buggy.transform.position;
                    pos = bp + new Vector3(-9f, 5f, -9f); look = bp + Vector3.up;
                }
                else return false;
                break;
        }
        if (m41Log <= 0f)
        {
            m41Log = 1f;
            Vector3 fp = f.transform.position;
            string s = "FFDEMO moon t=" + m41T.ToString("0.0") + " phase " + m41Phase + " P1 " + f.world + " (" + fp.x.ToString("0.0") + ", " + fp.y.ToString("0.0") + ", " + fp.z.ToString("0.0") + ")";
            if (f.world == WorldId.Moon) s += " h " + (fp.y - MoonWorld.GY(fp)).ToString("0.00") + " g x" + f.spaceGravity.ToString("0.000");
            if (f.world == WorldId.Space && sw.ship != null) s += " | ship mode " + sw.ship.mode + " moonOrbit " + sw.ship.InOrbitOf(moon) + " d(moon) " + (sw.ship.transform.position - sw.bodies[moon].pos).magnitude.ToString("0");
            if (mw != null && mw.buggy != null && f.world == WorldId.Moon) s += " | buggy v " + mw.buggy.Speed.ToString("0.0") + " y " + (mw.buggy.transform.position.y - MoonWorld.GY(mw.buggy.transform.position)).ToString("0.00") + " wheels " + mw.buggy.Grounded + " aboard " + mw.Aboard;
            Debug.Log(s);
        }
        return f.world == WorldId.Moon;
    }
}
