using UnityEngine;

// ffu38 demo shot for the probe (?ffdemo=1&ffshot=mechwade[&mwband=N]):
//   mechwade  James's 10-story mech (mwband=1..3 for a bigger one) starts exactly like ffu37's pondsink (north shore
//             (40, -12), heading south, same side camera, so the first ~25 s compare 1:1 with the ffu37 build), wades off
//             the beach down the drop-off until the water is ~60% up it, then circles there, stands still, and P1 hops
//             out into James's Cybertruck parked on the north lawn and drives it into the pond past the mech.
//             The camera never dips under the surface (pondsink's did once the mech was deep).
//             Logs "FFDEMO mechwade t ... phase / depth / speed / steps" every 0.5 s.
public partial class Game
{
    static bool Is38(string sc) { return sc == "mechwade"; }
    StoryMech p8Mech; Vehicle p8Truck; Frog p8Frog;
    int p8Phase; float p8T, p8PhT, p8Log; Vector3 p8Cam; float p8Deep;

    void Demo38Start(Frog f, string sc)
    {
        if (f.vehicle != null) f.ExitVehicle();
        if (!Roster.IsFrog(f.charId) || f.charId != 0) SetSeatChar(f.id, 0, false);
        demoKeepChars = true; p8Frog = f;
        if (f.world != WorldId.Ranch) f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f);
        int band = 0; string b = UrlParam("mwband"); if (b != null) int.TryParse(b, out band); band = Mathf.Clamp(band, 0, 3);
        p8Mech = FindMech(0, band);
        p8Phase = 0; p8T = 0f; p8PhT = 0f; p8Log = 0f; p8Cam = Vector3.zero;
        if (p8Mech == null) { Debug.Log("FFDEMO mechwade: no mech"); return; }
        p8Deep = Mathf.Min(0.6f * p8Mech.height, 18f);
        StoryMech.DtCap = 0.3f;
        p8Mech.DemoPlace(new Vector3(40f, 0f, -12f), 180f);
        f.EnterVehicle(p8Mech);
        foreach (var v in Vehicle.All) if (v != null && v.Title == "James's Cybertruck") { p8Truck = v; break; }
        demoHook = MechWadeInput;
        Debug.Log("FFDEMO mechwade start: " + p8Mech.Title + " H " + p8Mech.height + ", circles at depth " + p8Deep.ToString("0.0") + " m; WaveSim " + (WaveSim.Active ? "on" : WaveSim.I != null ? "idle" : "off"));
    }

    void P8Phase(int ph) { p8Phase = ph; p8PhT = p8T; Debug.Log("FFDEMO mechwade -> phase " + ph + " t=" + p8T.ToString("0.0")); }

    PIn MechWadeInput(PIn i)
    {
        var o = new PIn();
        if (p8Mech == null || p8T < 2f) return o;
        Frog f = p8Frog;
        if (p8Phase <= 2)
        {
            Vector3 mp = p8Mech.transform.position;
            float yaw = p8Mech.transform.eulerAngles.y;
            float depth = Ripples.SurfaceY - mp.y;
            if (p8Phase == 0)
            {
                // same as ffu37 pondsink phase 0: south (heading 180) at full stick
                o.move = new Vector2(Mathf.Clamp(Mathf.DeltaAngle(yaw, 180f) / 30f, -1f, 1f), 1f);
                if (depth >= p8Deep || mp.z < -72f) P8Phase(1);
            }
            else if (p8Phase == 1) { o.move = new Vector2(0.75f, 0.7f); if (p8T - p8PhT > 16f) P8Phase(2); }   // trudge round in circles
            else if (p8T - p8PhT > 5f) P8Phase(3);   // stand still (only the gentle breathing ring)
            return o;
        }
        if (p8Phase == 3)
        {
            if (f != null && p8Truck != null)
            {
                Vector3 p = new Vector3(56f, 0f, -12f); p.y = Ranch.GY(p.x, p.z) + 0.8f;
                f.ExitVehicle();
                Place(p8Truck, p, 180f, Vector3.zero);
                f.EnterVehicle(p8Truck);
            }
            p8Cam = Vector3.zero;
            P8Phase(4);
            return o;
        }
        if (p8Phase == 4 && p8Truck != null)
        {
            float yaw = p8Truck.transform.eulerAngles.y;
            o.gas = p8T - p8PhT > 1f ? 1f : 0f;
            o.move.x = Mathf.Clamp(Mathf.DeltaAngle(yaw, 180f) / 25f, -1f, 1f);
            if (p8T - p8PhT > 16f) P8Phase(5);
        }
        return o;
    }

    bool Demo38Cam(Frog f, string sc, out Vector3 pos, out Vector3 look)
    {
        pos = look = Vector3.zero;
        float dt = Mathf.Min(Time.deltaTime, 0.34f);
        p8T += dt;
        p8Log -= Time.unscaledDeltaTime;
        if (p8Mech == null) return false;
        float wy = Ripples.SurfaceY;
        if (p8Phase < 4)
        {
            // ffu37 pondsink's camera, but kept above the surface
            Vector3 mp = p8Mech.transform.position;
            float H = p8Mech.height;
            Vector3 want = mp + new Vector3(H * 1.15f, H * 0.45f, H * 0.55f);
            float gy = Ranch.GY(want.x, want.z) + 1.5f; if (want.y < gy) want.y = gy;
            if (want.y < wy + 1.2f) want.y = wy + 1.2f;
            p8Cam = p8Cam == Vector3.zero ? want : Vector3.Lerp(p8Cam, want, Mathf.Min(1f, Time.unscaledDeltaTime * 2f));
            pos = p8Cam; look = mp + Vector3.up * H * 0.45f;
            if (look.y < wy - H * 0.1f) look.y = wy - H * 0.1f;
            if (p8Log <= 0f)
            {
                p8Log = 0.5f;
                Debug.Log("FFDEMO mechwade t=" + p8T.ToString("0.0") + " phase=" + p8Phase + " mech y=" + mp.y.ToString("0.00") + " depth=" + (wy - mp.y).ToString("0.0") + " wade=" + StoryMech.PondWade(mp, H).ToString("0.00") + " v=" + p8Mech.Velocity.magnitude.ToString("0.0") + " pos=(" + mp.x.ToString("0") + "," + mp.z.ToString("0") + ") waveSteps=" + WaveSim.Steps + " sim=" + WaveSim.Active);
            }
            return true;
        }
        if (p8Truck == null) return false;
        Transform tt = p8Truck.transform;
        Vector3 fw = tt.forward; fw.y = 0f; fw.Normalize();
        Vector3 rt = new Vector3(fw.z, 0f, -fw.x);
        Vector3 tp = tt.position;
        Vector3 w2 = tp - fw * 8f + rt * 6f + Vector3.up * 3.5f;
        float g2 = Mathf.Max(Ranch.GY(w2.x, w2.z) + 1.2f, wy + 1.4f); if (w2.y < g2) w2.y = g2;
        p8Cam = p8Cam == Vector3.zero ? w2 : Vector3.Lerp(p8Cam, w2, Mathf.Min(1f, Time.unscaledDeltaTime * 2.5f));
        pos = p8Cam; look = tp + Vector3.up * 0.8f; if (look.y < wy) look.y = wy;
        if (p8Log <= 0f)
        {
            p8Log = 0.5f;
            Debug.Log("FFDEMO mechwade t=" + p8T.ToString("0.0") + " phase=" + p8Phase + " truck pos=(" + tp.x.ToString("0") + "," + tp.y.ToString("0.0") + "," + tp.z.ToString("0") + ") v=" + p8Truck.Speed.ToString("0.0") + " sub=" + p8Truck.pondSub.ToString("0.00") + " waveSteps=" + WaveSim.Steps);
        }
        return true;
    }
}
