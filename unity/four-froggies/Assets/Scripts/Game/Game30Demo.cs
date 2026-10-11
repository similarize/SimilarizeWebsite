using UnityEngine;

// ffu30 demo shots (?ffdemo=1&ffshot=...):
//   rctrack    - P1 takes James's RC buggy at the stand (autopilot drives it), the AI froggies take the other three; chase cam,
//                then a wide shot of the whole raceway, a close low shot at the gap jump, the froggies on the stand, chase again
//   rcwide     - the wide shot only (all four cars racing), rcclose - the low trackside shot only
//   cybersound - P1 drives a Cybertruck round the rally figure-eight: full power, coast (regen), brake, power again;
//                logs the EV voice layers (whine / hum / regen / road volume@pitch) every 0.5 s (audio can't be heard headless)
//   dirtbike   - P1 on the 2-stroke dirt bike: wheelie launch, kicker + gap + landing, tabletop, turn, back over the rollers;
//                chase cam + a side tracking shot; logs speed, lean, wheelie angle, air time, crashes, voice levels
public partial class Game
{
    public static bool Is30(string sc) { return sc == "rctrack" || sc == "rcwide" || sc == "rcclose" || sc == "cybersound" || sc == "dirtbike"; }
    float d30T, d30Log;
    Vehicle d30V;
    RcCar d30Rc;
    Bike d30Bike;
    float d30Track;

    void Demo30Start(Frog f, string sc)
    {
        if (f.world != WorldId.Ranch) f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f);
        if (f.vehicle != null) f.ExitVehicle();
        d30T = 0f; d30Log = 0f; d30V = null; d30Rc = null; d30Bike = null;
        if (sc.StartsWith("rc"))
        {
            if (RcTrack.I == null) { Debug.Log("FFDEMO rc: no track"); return; }
            RcTrack.DemoAll = true;
            f.Teleport(RcTrack.I.StandFoot + Vector3.up * 0.2f);
            RcTrack.I.Take(f);
            d30Rc = f.vehicle as RcCar;
            if (d30Rc != null) d30Rc.demoAuto = true;
            // the AI froggies walk over (DemoAll lets them take the remotes from anywhere on the ranch)
            foreach (Frog o in frogs) if (o != null && o != f && !o.human && o.world == WorldId.Ranch) o.Teleport(RcTrack.I.StandFoot + new Vector3(Random.Range(-1.5f, 1.5f), 0.2f, Random.Range(-2f, 0f)));
            Debug.Log("FFDEMO rc start " + sc + " car=" + (d30Rc != null ? d30Rc.Title : "-"));
        }
        else if (sc == "cybersound")
        {
            Vehicle best = null;
            foreach (var v in Vehicle.All) if (v != null && v.driver == null && v is GroundVehicle && v.name.Contains("Cybertruck")) { best = v; break; }
            if (best == null) { Debug.Log("FFDEMO cybersound: no Cybertruck"); return; }
            d30Track = 1.4f;
            Vector3 p = Layout.TrackPoint(d30Track); p.y = Ranch.GY(p.x, p.z) + 0.8f;
            Vector3 dir = Layout.TrackPoint(d30Track + 0.02f) - Layout.TrackPoint(d30Track); dir.y = 0f;
            best.rb.velocity = Vector3.zero; best.rb.angularVelocity = Vector3.zero;
            best.rb.position = p; best.rb.rotation = Quaternion.LookRotation(dir.normalized);
            best.transform.position = p; best.transform.rotation = best.rb.rotation;
            f.EnterVehicle(best);
            d30V = best;
            demoHook = i => CyberSoundInput(i);
            Debug.Log("FFDEMO cybersound start " + best.Title + " kind=" + best.engineKind);
        }
        else if (sc == "dirtbike")
        {
            if (Bike.AllBikes.Count == 0) { Debug.Log("FFDEMO dirtbike: no bikes"); return; }
            Bike b = Bike.AllBikes[0];
            Vector2 c = RcTrack.MxC;
            Vector3 p = new Vector3(c.x - 34f, 0f, c.y); p.y = Ranch.GY(p.x, p.z) + 0.4f;
            b.rb.velocity = Vector3.zero; b.rb.angularVelocity = Vector3.zero;
            b.rb.position = p; b.rb.rotation = Quaternion.Euler(0f, 90f, 0f);
            b.transform.position = p; b.transform.rotation = b.rb.rotation;
            f.EnterVehicle(b);
            d30V = b; d30Bike = b;
            demoHook = i => BikeDemoInput(i);
            Debug.Log("FFDEMO dirtbike start " + b.Title + " kind=" + b.engineKind);
        }
    }

    PIn CyberSoundInput(PIn i)
    {
        var o = new PIn();
        if (d30V == null) return o;
        float t = d30T;
        // follow the rally figure-eight centreline
        Vector3 p = d30V.transform.position;
        float best = d30Track, bd = 1e9f;
        for (float k = -0.05f; k <= 0.3f; k += 0.01f) { Vector3 q = Layout.TrackPoint(d30Track + k); float d = (new Vector2(q.x - p.x, q.z - p.z)).sqrMagnitude; if (d < bd) { bd = d; best = d30Track + k; } }
        d30Track = best;
        Vector3 tg = Layout.TrackPoint(d30Track + 0.12f) - p; tg.y = 0f;
        Vector3 fw = d30V.transform.forward; fw.y = 0f;
        float ang = Vector3.SignedAngle(fw, tg, Vector3.up);
        o.move = new Vector2(Mathf.Clamp(ang / 30f, -1f, 1f), 0f);
        if (t < 8f) o.gas = 1f;                 // full power
        else if (t < 13f) { }                   // coast: regen whir
        else if (t < 15f) o.brake = 1f;         // brake
        else if (t < 22f) o.gas = 0.6f;         // power again
        else if (t < 26f) { }
        else o.gas = 0.3f;
        return o;
    }

    PIn BikeDemoInput(PIn i)
    {
        var o = new PIn();
        if (d30Bike == null) return o;
        float t = d30T;
        Vector3 p = d30Bike.transform.position;
        Vector2 c = RcTrack.MxC;
        float fs = d30Bike.ForwardSpeed;
        // keep to the jump line (z = c.y) heading east, then turn round and come back over the rollers (z = c.y - 6)
        bool back = p.x > c.x + 22f || t > 18f;
        float zt = back ? c.y - 6f : c.y;
        Vector3 aim = back ? new Vector3(p.x - 6f, 0f, zt) : new Vector3(p.x + 6f, 0f, zt);
        Vector3 tg = aim - p; tg.y = 0f;
        Vector3 fw = d30Bike.transform.forward; fw.y = 0f;
        float ang = Vector3.SignedAngle(fw, tg, Vector3.up);
        o.move = new Vector2(Mathf.Clamp(ang / 28f, -1f, 1f), 0f);
        if (d30Bike.airborne) { o.move.y = 0f; return o; }
        float vt = back ? 9f : (p.x < c.x - 24f ? 14f : 13f);
        if (t < 1.6f) o.gas = 1f;                                  // launch wheelie
        else if (vt > fs) o.gas = Mathf.Clamp01((vt - fs) * 0.35f + 0.3f);
        else o.brake = Mathf.Clamp01((fs - vt) * 0.3f);
        return o;
    }

    bool Demo30Cam(Frog f, string sc, out Vector3 pos, out Vector3 look)
    {
        pos = look = Vector3.zero;
        d30T += Time.deltaTime;
        d30Log -= Time.unscaledDeltaTime;
        if (d30Log <= 0f)
        {
            d30Log = 0.5f;
            if (sc.StartsWith("rc") && RcTrack.I != null)
            {
                var sb = new System.Text.StringBuilder("FFDEMO rc t=" + d30T.ToString("0.0"));
                foreach (RcCar c in RcTrack.I.cars)
                    sb.Append(" | " + c.index + ":" + (c.driver != null ? c.driver.nick : "auto") + " v=" + c.Speed.ToString("0.0") + " lap=" + c.laps + (c.airborne ? " AIR" : "") + " up=" + c.transform.up.y.ToString("0.00") + " d=" + RcTrack.I.OnTrackDist(c.transform.position).ToString("0.0"));
                Debug.Log(sb.ToString());
                if (d30Rc != null) Debug.Log("FFDEMO rc voice " + d30Rc.VoiceLevels);
            }
            else if (sc == "cybersound" && d30V != null)
                Debug.Log("FFDEMO cybersound t=" + d30T.ToString("0.0") + " v=" + d30V.Speed.ToString("0.0") + " " + (SynthVoice.LastCyber != null ? SynthVoice.LastCyber.Levels() + " decel=" + SynthVoice.LastCyber.decel.ToString("0.00") : "no voice"));
            else if (sc == "dirtbike" && d30Bike != null)
                Debug.Log("FFDEMO dirtbike t=" + d30T.ToString("0.0") + " v=" + d30Bike.Speed.ToString("0.0") + " lean=" + d30Bike.Lean.ToString("0") + " wheelie=" + d30Bike.WheelieDeg.ToString("0") + " wheelies=" + d30Bike.wheelies + (d30Bike.airborne ? " AIR " + d30Bike.airT.ToString("0.00") : "") + " lastAir=" + d30Bike.lastAir.ToString("0.00") + " crashes=" + d30Bike.crashes + " pos=" + d30Bike.transform.position.ToString("0.0") + " " + d30Bike.VoiceLevels);
        }
        if (sc.StartsWith("rc"))
        {
            if (RcTrack.I == null) return false;
            Vector2 C = RcTrack.C; float by = RcTrack.I.BaseY;
            int ph = sc == "rcwide" ? 1 : sc == "rcclose" ? 2 : (d30T < 12f ? 0 : d30T < 24f ? 1 : d30T < 34f ? 2 : d30T < 42f ? 3 : 0);
            if (ph == 0) return false;   // the normal chase cam
            if (ph == 1) { pos = new Vector3(C.x - 20f, by + 13f, C.y - 26f); look = new Vector3(C.x + 1f, by, C.y - 1f); return true; }
            if (ph == 2)
            {
                // low at the gap jump, panning with the nearest car
                pos = RcTrack.I.W3(1.2f, 4.3f, 0.55f);
                Vector3 tgt = RcTrack.I.W3(-1f, 1.4f, 0.4f);
                float bd = 1e9f; foreach (RcCar c in RcTrack.I.cars) { float d = (c.transform.position - pos).sqrMagnitude; if (d < bd && d < 64f) { bd = d; tgt = c.transform.position + Vector3.up * 0.2f; } }
                look = tgt; return true;
            }
            pos = RcTrack.I.StandTop + new Vector3(3.5f, 1.5f, 3.2f); look = RcTrack.I.StandTop + new Vector3(-1.5f, 0.8f, 0f); return true;
        }
        if (sc == "dirtbike" && d30Bike != null)
        {
            if (d30T < 4f || d30T > 16f) return false;   // chase cam
            Vector3 bp = d30Bike.transform.position;
            pos = new Vector3(bp.x + 1f, Ranch.GY(bp.x, bp.z - 10f) + 2.2f, bp.z - 10f); look = bp + Vector3.up * 0.9f; return true;
        }
        return false;
    }
}
