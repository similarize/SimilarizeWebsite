using UnityEngine;

// ffu31 demo shot ?ffdemo=1&ffshot=trailertow - one scripted run (game time):
//   0-6    wide shot into the trailer barn (four trailers, solar roofs)
//   6-16   James's Cybertruck backs up to the Toy Hauler's tongue, the hitch prompt comes up, E -> hitched (low side cam)
//   16-24  pulls the trailer out of the barn
//   24-48  the rig on the spur road and round the figure-eight at ~13 m/s (chase cam: tracking, sway, dust)
//   48-56  stop, reverse with lock on -> jackknife (soft stop, no explosion)
//   56-68  in the garage yard: ramp down, James rides a bike (ffu30 dirt bike, else a garage riding mower) up the
//          ramp into the bay -> stow (scripted load if the ride-in stalls)
//   68-82  back in the truck, ramp closed, tows the bike across the yard
//   82-96  stop, ramp down, rides the bike back out and away
// &tt=hitch|tow|bike starts at that phase. Logs "FFDEMO trailer ..." every 0.5 s.
public partial class Game
{
    public static bool IsTrailerDemo(string sc) { return sc == "trailertow"; }
    Vehicle ttTruck, ttBike;
    Trailer ttTrailer;
    float ttT, ttLog, ttPressT = -1f;
    int ttPhase = -1, ttWp;
    Vector3 ttCam, ttHead;
    bool ttPhysLoad, ttUnloadStarted, ttRig6;
    string ttNote = "";

    void TrailerDemoStart(Frog f, string sc)
    {
        if (f.world != WorldId.Ranch) f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f);
        if (f.vehicle != null) f.ExitVehicle();
        if (!Roster.IsFrog(f.charId) || f.charId != 0) SetSeatChar(f.id, 0, false);
        demoKeepChars = true;
        ttT = 0f; ttLog = 0f; ttPhase = -1; ttWp = 0; ttCam = Vector3.zero;
        string jump = UrlParam("tt");
        if (jump == "hitch") ttT = 6f; else if (jump == "tow") ttT = 16f; else if (jump == "bike") ttT = 56f;
        foreach (var v in Vehicle.All) if (v != null && v.Title == "James's Cybertruck") { ttTruck = v; break; }
        foreach (var t in TrailerBarn.Trailers) if (t != null && t.spec.style == 2) ttTrailer = t;
        ttBike = null;
        foreach (var v in Vehicle.All) { if (v == null || !Trailer.Loadable(v) || v.driver != null) continue; string n = v.Title.ToLowerInvariant(); if (n.Contains("bike") || n.Contains("moto") || n.Contains("dirt")) { ttBike = v; break; } }
        if (ttBike == null) foreach (var v in Vehicle.All) if (v != null && Trailer.Loadable(v) && v.driver == null && v != RidingMower.I) { ttBike = v; break; }
        if (ttTruck == null || ttTrailer == null) { Debug.Log("FFDEMO trailer: missing truck/trailer " + (ttTruck != null) + " " + (ttTrailer != null)); return; }
        // the truck in front of the toy hauler, facing out of the barn, hitch ~6 m from the ball
        Vector3 ball = ttTrailer.BallWorld;
        Vector3 fw = TrailerBarn.Dir(0f, 1f);
        float back = -Trailer.HitchLocal(ttTruck).z;
        PlaceVeh(ttTruck, ball + fw * (6f + back), TrailerBarn.Yaw);
        f.EnterVehicle(ttTruck);
        demoHook = TrailerDemoInput;
        Debug.Log("FFDEMO trailer start bike=" + (ttBike != null ? ttBike.Title : "none") + " barn=" + TrailerBarn.Centre.ToString("0.0") + " trailers=" + TrailerBarn.Trailers.Count);
    }

    static void PlaceVeh(Vehicle v, Vector3 p, float yaw)
    {
        RaycastHit h;
        if (Physics.Raycast(p + Vector3.up * 4f, Vector3.down, out h, 12f, Trailer.WheelMask, QueryTriggerInteraction.Ignore)) p.y = h.point.y;
        p.y += 0.35f;
        Quaternion r = Quaternion.Euler(0f, yaw, 0f);
        if (!v.rb.isKinematic) { v.rb.velocity = Vector3.zero; v.rb.angularVelocity = Vector3.zero; }
        v.rb.position = p; v.rb.rotation = r; v.transform.SetPositionAndRotation(p, r);
    }

    // move the hitched rig in one go (truck, then the trailer lined up behind it)
    void PlaceRig(Vector3 p, float yaw)
    {
        PlaceVeh(ttTruck, p, yaw);
        Physics.SyncTransforms();
        if (ttTrailer.Hitched) ttTrailer.PlaceBehind(ttTruck);
        Physics.SyncTransforms();
    }

    PIn TtDrive(Vector3 target, float speed, bool reverse = false)
    {
        var o = new PIn();
        Transform t = ttTruck.transform;
        Vector3 l = t.InverseTransformPoint(target);
        float fs = ttTruck.ForwardSpeed;
        if (!reverse)
        {
            o.move.x = Mathf.Clamp(Mathf.Atan2(l.x, Mathf.Max(0.5f, l.z)) * 1.8f, -1f, 1f);
            float e = speed - fs;
            o.gas = Mathf.Clamp(e * 0.35f, 0f, 1f); o.brake = Mathf.Clamp(-e * 0.3f, 0f, 1f);
        }
        else
        {
            float e = -speed - fs;   // want fs = -speed
            o.brake = Mathf.Clamp(-e * 0.5f, 0f, 1f); o.gas = Mathf.Clamp(e * 0.4f, 0f, 1f);
        }
        return o;
    }

    PIn TrailerDemoInput(PIn i)
    {
        var o = new PIn();
        Frog f = frogs[slots[0].frog];
        if (ttTruck == null || ttTrailer == null) return o;
        float t = ttT;
        if (t < 6f) return o;
        if (t < 16f)
        {
            if (ttTrailer.Hitched) return Stop(o, ttTruck.ForwardSpeed);
            // reverse to the ball: steer the hitch onto it (reversing: steer toward the side the ball is on)
            Vector3 bl = ttTruck.transform.InverseTransformPoint(ttTrailer.BallWorld) - Trailer.HitchLocal(ttTruck);
            float dist = new Vector2(bl.x, bl.z).magnitude;
            float want = dist > 2.5f ? 1.4f : 0.6f;
            o = TtDrive(Vector3.zero, want, true);
            o.move.x = Mathf.Clamp(bl.x * 0.9f, -1f, 1f);
            float d;
            if (ttTrailer.CanHitch(ttTruck, out d) && dist < 0.9f) { o = Stop(o, ttTruck.ForwardSpeed); o.use = Time.frameCount % 2 == 0; }
            return o;
        }
        if (t < 24f)
        {
            if (!ttTrailer.Hitched) return o;
            return TtDrive(ttTruck.transform.position + TrailerBarn.Dir(0f, 1f) * 30f, 5f);
        }
        if (t < 48f) return SpurDrive(12f);
        if (t < 56f)
        {
            if (t < 50f) return Stop(o, ttTruck.ForwardSpeed);
            o = TtDrive(Vector3.zero, 3f, true); o.move.x = 0.85f; return o;
        }
        return BikeIn(o, f, t);
    }

    // brake to a stop without backing up (LT below ~1 m/s is reverse)
    static PIn Stop(PIn o, float fs)
    {
        o.move = Vector2.zero; o.gas = 0f; o.brake = 0f;
        if (fs > 1f) o.brake = 1f; else if (fs < -0.3f) o.gas = 0.4f;
        return o;
    }

    // the rig on the figure-eight at track parameter t, pointing along +t
    void PlaceOnTrack(float t)
    {
        Vector3 a = Layout.TrackPoint(t), b = Layout.TrackPoint(t + 0.02f);
        PlaceRig(a, Quaternion.LookRotation(new Vector3(b.x - a.x, 0f, b.z - a.z)).eulerAngles.y);
    }

    // round the figure-eight (track points ahead of the truck)
    PIn SpurDrive(float speed)
    {
        {
            Vector3 tp = ttTruck.transform.position;
            float best = 1e9f, bt = 0f;
            for (int k = 0; k < 256; k++) { float tt = k * Mathf.PI * 2f / 256f; Vector3 q = Layout.TrackPoint(tt); float dd = (new Vector2(q.x - tp.x, q.z - tp.z)).sqrMagnitude; if (dd < best) { best = dd; bt = tt; } }
            Vector3 tgt = Layout.TrackPoint(bt + 0.12f);   // always +t (the way PlaceOnTrack points the rig)
            return TtDrive(new Vector3(tgt.x, tp.y, tgt.z), speed);
        }
    }

    PIn BikeIn(PIn o, Frog f, float t)
    {
        if (t < 68f)
        {
            if (f.vehicle == ttBike && ttBike != null && !ttBike.stowed)
            {
                // ride straight at the bay; brake once inside; E to stow when slow (scripted load if it stalls)
                Vector3 bayT = ttTrailer.transform.TransformPoint(new Vector3(0f, ttTrailer.deck, -ttTrailer.spec.len * 0.5f + ttTrailer.spec.bayLen * 0.5f));
                Vector3 l = ttBike.transform.InverseTransformPoint(bayT);
                o.move.x = Mathf.Clamp(Mathf.Atan2(l.x, Mathf.Max(0.5f, l.z)) * 2f, -1f, 1f);
                bool inBay = ttTrailer.InBay(ttBike.transform.position);
                float fs = ttBike.ForwardSpeed;
                if (inBay) { o.brake = fs > 0.5f ? 1f : 0f; if (Mathf.Abs(fs) < 0.8f) { o.use = Time.frameCount % 2 == 0; ttPhysLoad = true; } }
                else { o.gas = fs < 2.6f ? 0.6f : 0f; o.move.y = 0f; }
                if (t > 64.5f && !inBay && ttTrailer.move == null && ttTrailer.AtRampFoot(ttBike)) { o.use = Time.frameCount % 2 == 0; ttNote = "scripted-load"; }
            }
            return o;
        }
        if (t < 82f)
        {
            if (f.vehicle != ttTruck || !ttRig6) return o;
            return SpurDrive(10f);
        }
        if (f.vehicle == ttTruck) return Stop(o, ttTruck.ForwardSpeed);
        if (f.vehicle != null && f.vehicle == ttBike && !ttBike.stowed && ttTrailer.move == null) { o.gas = 0.55f; return o; }
        return o;
    }

    void TrailerDemoStep(Frog f)
    {
        float t = ttT;
        int ph = t < 6f ? 0 : t < 16f ? 1 : t < 24f ? 2 : t < 48f ? 3 : t < 56f ? 4 : t < 68f ? 5 : t < 82f ? 6 : 7;
        if (ph == ttPhase) return;
        int prev = ttPhase;
        ttPhase = ph;
        Debug.Log("FFDEMO trailer phase " + ph + " t=" + t.ToString("0.0"));
        if (ph >= 2 && !ttTrailer.Hitched && ph <= 6)
        {
            // jumped straight to a later phase (&tt=) or the reverse-in missed: hitch it in place
            Vector3 fw = ttTrailer.transform.forward; fw.y = 0f; fw.Normalize();
            if (f.vehicle != ttTruck) { if (f.vehicle != null) f.ExitVehicle(); f.EnterVehicle(ttTruck); }
            float back = -Trailer.HitchLocal(ttTruck).z;
            PlaceVeh(ttTruck, ttTrailer.BallWorld + fw * (back + 0.05f), Quaternion.LookRotation(fw).eulerAngles.y);
            Physics.SyncTransforms();
            ttTrailer.Hitch(ttTruck);
            ttNote = "auto-hitched at phase " + ph;
        }
        if (ph == 3)
        {
            ttWp = 0;
            PlaceOnTrack(1.25f);   // the east lobe of the figure-eight, heading for the underpass jumps
            ttCam = Vector3.zero;
        }
        if (ph == 5)
        {
            // the garage yard: truck heading +x, trailer behind; ramp down; James on the bike 9 m behind the ramp
            PlaceRig(new Vector3(24f, 0f, 64f), 90f);   // the open yard north of the garage, clear of the AI bike loop
            ttCam = Vector3.zero;
            if (f.vehicle != null) f.ExitVehicle();
            ttTrailer.SetRamp(true);
            if (ttBike != null)
            {
                Vector3 foot = ttTrailer.RampFoot(0f);
                Vector3 bk = -ttTrailer.transform.forward; bk.y = 0f; bk.Normalize();
                PlaceVeh(ttBike, foot + bk * 7f, Quaternion.LookRotation(-bk).eulerAngles.y);
                f.EnterVehicle(ttBike);
            }
        }
        if (ph == 6)
        {
            if (ttBike != null && !ttBike.stowed && ttTrailer.move == null)
            {
                // the bike never made it in: scripted load so the rest of the run still shows towing a bike
                if (f.vehicle != ttBike) { if (f.vehicle != null) f.ExitVehicle(); f.EnterVehicle(ttBike); }
                ttTrailer.StartLoad(ttBike, f);
                ttNote = "late scripted load";
            }
        }
        if (ph == 6) ttRig6 = false;
        if (ph == 7) ttUnloadStarted = false;
    }

    bool TrailerDemoCam(Frog f, string sc, out Vector3 pos, out Vector3 look)
    {
        pos = look = Vector3.zero;
        if (ttTruck == null || ttTrailer == null) return false;
        ttT += Mathf.Min(Time.deltaTime, 0.34f);   // game time (= physics time) so the script keeps pace with the rig
        TrailerDemoStep(f);
        float t = ttT;
        // phase 6: truck back in, ramp closed; phase 7: stop, ramp down, ride out
        if (ttPhase == 6 && t > 68.5f && f.vehicle != ttTruck && ttTrailer.move == null)
        {
            ttTrailer.SetRamp(false);
            if (t > 69.5f) { if (f.vehicle != null) f.ExitVehicle(); f.EnterVehicle(ttTruck); }
        }
        if (ttPhase == 6 && !ttRig6 && f.vehicle == ttTruck && ttTrailer.rampK < 0.02f && t > 69.5f)
        {
            // tow the bike off round the track (cargo rides along in the bay)
            ttRig6 = true; ttWp = 0; ttCam = Vector3.zero;
            PlaceOnTrack(1.25f);
        }
        if (ttPhase == 7 && ttTruck.Speed < 0.5f && t > 84f && !ttUnloadStarted)
        {
            if (f.vehicle == ttTruck) f.ExitVehicle();
            if (!ttTrailer.RampDown) ttTrailer.SetRamp(true);
            else if (ttTrailer.Stowed > 0) { ttUnloadStarted = ttTrailer.StartUnload(f); Debug.Log("FFDEMO trailer unload start " + ttUnloadStarted); }
        }

        Transform tr = ttTrailer.transform, tk = ttTruck.transform;
        Vector3 hd = TrailerBarn.Dir(0f, 1f), rt = TrailerBarn.Dir(1f, 0f);
        float dtc = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        switch (ttPhase)
        {
            case 0:
                pos = TrailerBarn.W(-6f, 5.5f, TrailerBarn.Dp * 0.5f + 16f); look = TrailerBarn.W(1.5f, 1.6f, -1f); break;
            case 1:
                {
                    Vector3 b = ttTrailer.BallWorld;
                    pos = b - rt * 4.2f + hd * 1.8f + Vector3.up * 1.3f; look = b + hd * 1.2f + Vector3.up * 0.3f; break;
                }
            case 2:
                pos = TrailerBarn.W(9f, 3.2f, TrailerBarn.Dp * 0.5f + 20f); look = (tk.position + tr.position) * 0.5f + Vector3.up; break;
            case 3:
            case 6:
                {
                    // chase: 3/4 front-left of the rig, heading eased
                    Vector3 fw0 = tk.forward; fw0.y = 0f; fw0.Normalize();
                    ttHead = ttCam == Vector3.zero ? fw0 : Vector3.Slerp(ttHead, fw0, dtc * 1.5f);
                    Vector3 fw = ttHead; fw.y = 0f; fw.Normalize();
                    Vector3 lf = new Vector3(-fw.z, 0f, fw.x);
                    Vector3 mid = (tk.position + tr.position) * 0.5f;
                    Vector3 want = mid + lf * 11f + fw * 7f; want.y = Mathf.Max(Ranch.GY(want.x, want.z), mid.y) + 3.2f;
                    ttCam = ttCam == Vector3.zero ? want : Vector3.Lerp(ttCam, want, dtc * 3f);
                    pos = ttCam; look = mid + Vector3.up * 1.2f; break;
                }
            case 4:
                {
                    Vector3 mid = (tk.position + tr.position) * 0.5f;
                    pos = mid + Vector3.up * 13f - tk.forward * 4f + tk.right * 9f; look = mid; break;
                }
            default:
                {
                    if (ttPhase == 7 && ttUnloadStarted && ttBike != null && !ttBike.stowed && ttTrailer.move == null)
                    {
                        // ridden out: chase the bike away from the trailer
                        Transform bt = ttBike.transform;
                        Vector3 bf = bt.forward; bf.y = 0f; bf.Normalize();
                        Vector3 want = bt.position - bf * 5.5f + new Vector3(bf.z, 0f, -bf.x) * 2.5f + Vector3.up * 2.2f;
                        ttCam = ttCam == Vector3.zero ? want : Vector3.Lerp(ttCam, want, dtc * 3f);
                        pos = ttCam; look = bt.position + Vector3.up * 0.8f;
                        break;
                    }
                    // the ramp from the side: rider, bike, ramp, bay
                    Vector3 foot = ttTrailer.RampFoot(0f);
                    Vector3 side = tr.right; side.y = 0f; side.Normalize();
                    Vector3 bk = -tr.forward; bk.y = 0f; bk.Normalize();
                    pos = foot + side * 8.5f + bk * 2.5f + Vector3.up * 2.4f;
                    look = foot - bk * 1.2f + Vector3.up * 0.9f;
                    break;
                }
        }
        ttLog -= Time.unscaledDeltaTime;
        if (ttLog <= 0f)
        {
            ttLog = 0.5f;
            float err = ttTrailer.Hitched ? (Trailer.HitchWorld(ttTruck) - ttTrailer.BallWorld).magnitude : -1f;
            Debug.Log("FFDEMO trailer t=" + t.ToString("0.0") + " ph=" + ttPhase + " hitched=" + ttTrailer.Hitched + " ang=" + ttTrailer.hitchAngle.ToString("0") + " v=" + ttTruck.Speed.ToString("0.0") + " tv=" + ttTrailer.Speed.ToString("0.0") + " err=" + err.ToString("0.00") + " up=" + ttTrailer.uprightNow.ToString("0.00") + " ramp=" + ttTrailer.rampK.ToString("0.00") + " stowed=" + ttTrailer.Stowed + " bike=" + (ttBike != null ? (ttBike.stowed ? "in" : ttTrailer.InBay(ttBike.transform.position) ? "bay" : "out") + (ttBike.driver != null ? "+rider" : "") : "-") + " prompt='" + f.prompt + "' det=" + Trailer.Detaches + "/" + Trailer.SafeDetaches + " last=" + ttTrailer.lastDetach + " tpos=" + tr.position.ToString("0.0") + (ttBike != null ? " bpos=" + ttBike.transform.position.ToString("0.0") + " bv=" + ttBike.Speed.ToString("0.0") + " bkin=" + ttBike.rb.isKinematic : "") + " " + ttNote);
        }
        return true;
    }
}
