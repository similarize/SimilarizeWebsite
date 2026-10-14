using System.Collections.Generic;
using UnityEngine;

// ffu45 demo shots for the probe:
//  ?ffdemo=1&ffshot=motos    : the 5 trailer-barn bikes parked (two camera angles, 8 s; logs parking clearances), then P1
//                               rides each one out of the barn (down the lane between the trailers, out of the doors, a
//                               straight + a turn); trials = wheelies + hops, sidecar = an AI froggy in the chair.
//                               Logs "FFDEMO motos ..." every 0.5 s + "FFDEMO motos summary <bike>: ..." per bike.
//  ?ffdemo=1&ffshot=wheels45 : per-vehicle wheel size - P1 in the Monster Truck in its garage bay, sets ITS wheels to 4x
//                               (held at stock inside the garage), drives out north, the tyres grow outside; logs the
//                               Monster Truck and Dad's Cybertruck (must stay 1x). "FFDEMO wheels45 summary: ..."
//  ?ffdemo=1&ffshot=help45   : the redesigned help screen: open on foot (4 s), in James's Cybertruck (10 s, 2x wheels),
//                               scrolled to the end (20 s). &helpdev=pad|keys|touch forces the listed device.
public partial class Game
{
    static bool Is45(string sc) { return sc == "motos" || sc == "wheels45" || sc == "help45"; }
    Frog d45F; float d45T, d45PhT, d45Log; int d45Ph = -1, d45Wp; string d45Sc = "";
    Bike d45Bike; Vehicle d45Mt, d45Ct;
    float d45MaxV, d45MaxLean, d45MaxWh, d45MaxAir, d45HopT; int d45Hops0, d45Wh0, d45Cr0; bool d45Pax, d45Out;
    float d45MtInMax, d45MtMax, d45CtMax, d45MtOutT = -1f, d45MtGrowT = -1f;

    void Demo45Start(Frog f, string sc)
    {
        d45F = f; d45T = 0f; d45PhT = 0f; d45Log = 0f; d45Sc = sc; d45Ph = 0;
        if (f.vehicle != null) f.ExitVehicle();
        if (f.world != WorldId.Ranch) f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f);
        var ai = FindObjectOfType<BikeAI>(); if (ai != null) ai.enabled = false;
        if (sc == "motos")
        {
            int n = 0; foreach (var b in Bike.AllBikes) if (b.type >= 4) n++;
            Debug.Log("FFDEMO motos start: barn " + TrailerBarn.Built + " at " + TrailerBarn.Centre.ToString("0.0") + ", barn bikes " + n);
            foreach (var b in Bike.AllBikes)
            {
                if (b.type < 4) continue;
                float clr = 1e9f, ramp = 1e9f;
                foreach (var t in TrailerBarn.Trailers)
                {
                    foreach (var c in t.GetComponentsInChildren<Collider>()) { Vector3 q = c.bounds.ClosestPoint(b.transform.position + Vector3.up * 0.6f); clr = Mathf.Min(clr, Vector3.Distance(q, b.transform.position + Vector3.up * 0.6f)); }
                    if (t.spec.hauler) ramp = Mathf.Min(ramp, Flat45(t.RampFoot(0.8f) - b.transform.position));
                }
                Vector3 l = Quaternion.Euler(0f, -TrailerBarn.Yaw, 0f) * (b.transform.position - TrailerBarn.Centre);
                Debug.Log("FFDEMO motos parked " + b.Title + " barn-local (" + l.x.ToString("0.0") + ", " + l.z.ToString("0.0") + ") up " + b.transform.up.y.ToString("0.00") + " moved " + Flat45(b.transform.position - b.HomePos).ToString("0.00") +
                          " m | nearest trailer " + clr.ToString("0.0") + " m | nearest ramp foot " + ramp.ToString("0.0") + " m | in toy hauler: " + Trailer.Loadable(b) + " | seats " + (b.type == 8 ? 2 : 1));
            }
        }
        else if (sc == "wheels45")
        {
            foreach (var v in Vehicle.All) { if (v == null) continue; if (v.Title == "Monster Truck") d45Mt = v; if (v.Title == "Cybertruck") d45Ct = v; }
            Debug.Log("FFDEMO wheels45 start: monster " + (d45Mt != null) + " cybertruck " + (d45Ct != null) + " | old global ff.wheels now " + SaveStore.GetInt("ff.wheels", 4) + " (4 = 1x) | monster saved " + BigWheels.LabelFor(d45Mt) + " cybertruck saved " + BigWheels.LabelFor(d45Ct));
            if (d45Mt == null) return;
            BigWheels.SetFor(d45Ct, 1f, "demo", false);
            f.EnterVehicle(d45Mt);
            BigWheels.SetFor(d45Mt, BigWheels.Max, "demo (P1 in the Monster Truck)", false);
        }
        else
        {
            string hd = UrlParam("helpdev");
            hpForce = hd == "pad" ? (int)InputKind.Gamepad : hd == "touch" ? (int)InputKind.Touch : hd == "keys" ? (int)InputKind.Keyboard : -1;
        }
        demoHook = Demo45Drive;
    }
    static float Flat45(Vector3 d) { d.y = 0f; return d.magnitude; }

    Vector2[] D45Path()
    {
        Vector3 a = TrailerBarn.W(-6.9f, 0f, -2.5f), b = TrailerBarn.W(-6.9f, 0f, 9f), c = TrailerBarn.W(-6.9f, 0f, 16f), d = TrailerBarn.W(-6.9f, 0f, 32f), e = TrailerBarn.W(9f, 0f, 40f);
        return new[] { new Vector2(a.x, a.z), new Vector2(b.x, b.z), new Vector2(c.x, c.z), new Vector2(d.x, d.z), new Vector2(e.x, e.z) };
    }

    void D45Phase(int ph)
    {
        if (d45Ph >= 1 && d45Ph <= 5 && d45Bike != null)
        {
            Debug.Log("FFDEMO motos summary " + d45Bike.Title + ": max " + d45MaxV.ToString("0.0") + " m/s | max lean " + d45MaxLean.ToString("0") + " deg | max wheelie " + d45MaxWh.ToString("0") + " deg, wheelies " + (d45Bike.wheelies - d45Wh0) +
                      " | hops " + (Bike.Hops - d45Hops0) + " | max air " + d45MaxAir.ToString("0.00") + " s | crashes " + (d45Bike.crashes - d45Cr0) + " | waypoint " + d45Wp + "/5 | out of the barn " + d45Out + (d45Bike.type == 8 ? " | sidecar passenger seen " + d45Pax : "") + " | wheels " + d45Bike.bigK.ToString("0.00"));
            if (d45Bike.passenger != null) d45Bike.SidecarOut(d45Bike.passenger);
            if (d45F.vehicle != null) d45F.ExitVehicle();
        }
        d45Ph = ph; d45PhT = 0f; d45Wp = 0; d45MaxV = 0f; d45MaxLean = 0f; d45MaxWh = 0f; d45MaxAir = 0f; d45Pax = false; d45Out = false; d45HopT = 0f;
        Debug.Log("FFDEMO motos phase " + ph + " t=" + d45T.ToString("0.0"));
        if (ph >= 1 && ph <= 5)
        {
            d45Bike = null;
            foreach (var b in Bike.AllBikes) if (b.type == 3 + ph) d45Bike = b;
            if (d45Bike == null) return;
            d45Wh0 = d45Bike.wheelies; d45Hops0 = Bike.Hops; d45Cr0 = d45Bike.crashes;
            d45F.EnterVehicle(d45Bike);
            if (d45Bike.type == 8)
            {
                Frog pax = null;
                foreach (Frog o in frogs) if (o != null && o != d45F && !o.human && o.vehicle == null) { pax = o; break; }
                if (pax != null) { if (pax.world != WorldId.Ranch) pax.SendTo(WorldId.Ranch, d45Bike.transform.position + d45Bike.transform.right * 2f, 0f); d45Bike.SidecarIn(pax); }
            }
            Debug.Log("FFDEMO motos ride " + d45Bike.Title + " (" + d45Bike.HelpLine + ")");
        }
    }

    PIn Demo45Drive(PIn i)
    {
        var o = new PIn();
        if (d45Sc == "wheels45")
        {
            if (d45Mt == null || d45F.vehicle != d45Mt || d45PhT < 5f) return o;
            Vector3 p = d45Mt.transform.position;
            float want = Mathf.Clamp((Layout.BayX(2) - p.x) * 4f, -25f, 25f);   // keep heading north (+z) on the bay line
            o.move.x = Mathf.Clamp(Mathf.DeltaAngle(d45Mt.transform.eulerAngles.y, want) / 25f, -1f, 1f);
            if (p.z < 62f) o.gas = d45Mt.Speed < 5f ? 0.55f : 0.2f; else o.brake = 0.6f;
            return o;
        }
        if (d45Sc != "motos" || d45Ph < 1 || d45Ph > 5 || d45Bike == null || d45F.vehicle != d45Bike) return o;
        var path = D45Path();
        Vector3 bp = d45Bike.transform.position; Vector2 p2 = new Vector2(bp.x, bp.z);
        int k = Mathf.Min(d45Wp, path.Length - 1);
        Vector2 tgt = path[k];
        if ((tgt - p2).magnitude < (k < 2 ? 1.8f : 3.5f) && d45Wp < path.Length) d45Wp++;
        float vt = k < 2 ? 4.5f : k == 2 ? 7f : d45Bike.type == 4 ? 24f : d45Bike.type == 5 ? 15f : d45Bike.type == 6 ? 15f : d45Bike.type == 7 ? 8f : 12f;
        if (d45Wp >= path.Length) vt = 0f;
        Vector2 fw = new Vector2(d45Bike.transform.forward.x, d45Bike.transform.forward.z).normalized, to = (tgt - p2).normalized;
        float ang = Mathf.Atan2(fw.x * to.y - fw.y * to.x, Vector2.Dot(fw, to)) * Mathf.Rad2Deg;
        o.move = new Vector2(Mathf.Clamp(-ang / 30f, -1f, 1f), 0f);
        float fs = d45Bike.ForwardSpeed;
        if (vt > fs) o.gas = Mathf.Clamp01((vt - fs) * 0.3f + 0.2f); else o.brake = Mathf.Clamp01((fs - vt) * 0.4f);
        if (d45Bike.type == 7 && k >= 3)
        {
            // trials tricks: full throttle from slow = wheelie, hop every 1.6 s
            if (fs < 7f) o.gas = 1f;
            d45HopT += Time.fixedDeltaTime;
            if (d45HopT > 1.6f) { d45HopT = 0f; o.fire = true; }
        }
        if (d45Bike.type == 4 && k >= 3) o.gas = Mathf.Max(o.gas, 0.9f);
        return o;
    }

    bool Demo45Cam(Frog f, string sc, out Vector3 pos, out Vector3 look)
    {
        pos = look = Vector3.zero;
        if (d45Ph < 0) return false;
        float dt = Mathf.Min(Time.deltaTime, 0.4f);
        d45T += dt; d45PhT += dt; d45Log -= dt;
        if (sc == "help45") return Help45Tick(f, out pos, out look);
        if (sc == "wheels45") return Wheels45Tick(f, out pos, out look);
        // ---- motos ----
        if (d45Ph == 0 && d45PhT > 8f) D45Phase(1);
        else if (d45Ph >= 1 && d45Ph <= 5 && (d45PhT > 15f || d45Bike == null || (d45Wp >= 5 && d45Bike.Speed < 0.6f))) D45Phase(d45Ph + 1);
        if (d45Ph == 0)
        {
            if (d45PhT < 4f) { pos = TrailerBarn.W(2.2f, 2.6f, -8.6f); look = TrailerBarn.W(-8.5f, 0.6f, -4.8f); }
            else { pos = TrailerBarn.W(-14.2f, 2.7f, -9.3f); look = TrailerBarn.W(-3.5f, 0.5f, -4.4f); }
        }
        else if (d45Ph <= 5 && d45Bike != null)
        {
            Transform t = d45Bike.transform;
            Vector3 fwd = t.forward; fwd.y = 0f; fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            bool inside = TrailerBarn.Inside(t.position);
            pos = t.position - fwd * (inside ? 4.2f : 5.5f) + right * (inside ? 0.8f : 2.4f) + Vector3.up * (2.0f + d45Bike.bigLift);
            look = t.position + fwd * 2f + Vector3.up * (0.7f + d45Bike.bigLift * 0.5f);
            d45MaxV = Mathf.Max(d45MaxV, d45Bike.Speed); d45MaxLean = Mathf.Max(d45MaxLean, Mathf.Abs(d45Bike.Lean)); d45MaxWh = Mathf.Max(d45MaxWh, d45Bike.WheelieDeg);
            d45MaxAir = Mathf.Max(d45MaxAir, d45Bike.airT); if (d45Bike.passenger != null) d45Pax = true;
            if (!inside && d45Wp >= 3) d45Out = true;
            if (d45Log <= 0f)
            {
                d45Log = 0.5f;
                Debug.Log("FFDEMO motos t=" + d45T.ToString("0.0") + " " + d45Bike.Title + " v=" + d45Bike.Speed.ToString("0.0") + " lean=" + d45Bike.Lean.ToString("0") + " wheelie=" + d45Bike.WheelieDeg.ToString("0") + (d45Bike.airborne ? " AIR " + d45Bike.airT.ToString("0.00") : "") +
                          " wp=" + d45Wp + " inBarn=" + inside + " up=" + t.up.y.ToString("0.00") + " crashes=" + (d45Bike.crashes - d45Cr0) + " hops=" + (Bike.Hops - d45Hops0) + (d45Bike.type == 8 ? " pax=" + (d45Bike.passenger != null ? d45Bike.passenger.nick : "-") : "") + " pos=" + t.position.ToString("0.0") + " " + d45Bike.VoiceLevels);
            }
        }
        else
        {
            pos = TrailerBarn.W(-6f, 4f, 30f); look = TrailerBarn.W(-6f, 1f, 5f);
            if (d45PhT < 0.05f) Debug.Log("FFDEMO motos done");
        }
        return true;
    }

    bool Wheels45Tick(Frog f, out Vector3 pos, out Vector3 look)
    {
        pos = look = Vector3.zero;
        if (d45Mt == null) return false;
        var mt = d45Mt as GroundVehicle; var ct = d45Ct as GroundVehicle;
        float mk = mt != null ? mt.bigK : 1f, ck = ct != null ? ct.bigK : 1f;
        string bld = BigWheels.Building(d45Mt.transform.position);
        if (bld != null) d45MtInMax = Mathf.Max(d45MtInMax, mk);
        else if (d45MtOutT < 0f) d45MtOutT = d45T;
        d45MtMax = Mathf.Max(d45MtMax, mk); d45CtMax = Mathf.Max(d45CtMax, ck);
        if (d45MtGrowT < 0f && mk > 3.9f) d45MtGrowT = d45T;
        Vector3 p = d45Mt.transform.position;
        if (d45Log <= 0f)
        {
            d45Log = 0.5f;
            Debug.Log("FFDEMO wheels45 t=" + d45T.ToString("0.0") + " monster z " + p.z.ToString("0.0") + " k " + mk.ToString("0.00") + " want " + BigWheels.Want(d45Mt).ToString("0.00") + " in " + (bld ?? "open air") + (BigWheels.Blocked(d45Mt) ? " BLOCKED" : "") + " lift " + d45Mt.bigLift.ToString("0.00") + " v " + d45Mt.Speed.ToString("0.0") + " up " + d45Mt.transform.up.y.ToString("0.00") +
                      " | cybertruck k " + ck.ToString("0.00") + " want " + BigWheels.Want(d45Ct).ToString("0.00") + " | readout '" + BigWheels.Readout(d45Mt) + "'");
        }
        if (d45PhT > 42f && d45Ph == 0)
        {
            d45Ph = 1;
            Debug.Log("FFDEMO wheels45 summary: monster max k inside the garage " + d45MtInMax.ToString("0.00") + " | left the garage zone at t " + d45MtOutT.ToString("0.0") + " (z " + p.z.ToString("0.0") + " now) | reached 4x at t " + d45MtGrowT.ToString("0.0") + " | max k " + d45MtMax.ToString("0.00") +
                      " | cybertruck max k " + d45CtMax.ToString("0.00") + " (want " + BigWheels.Want(d45Ct).ToString("0.00") + ") | drove out " + (p.z > Layout.GarageC.y + Layout.GarageSize.y * 0.5f + 5f));
        }
        float gy = Ranch.GY(p.x, 50f);
        if (p.z < 36f) { pos = new Vector3(Layout.BayX(2) + 9f, gy + 7f, 50f); look = new Vector3(Layout.BayX(2) - 2f, Ranch.GY(p.x, 30f) + 2f, 26f); }
        else { pos = new Vector3(p.x + 13f, Ranch.GY(p.x, p.z) + 6f + d45Mt.bigLift, p.z + 12f); look = p + Vector3.up * (1.2f + d45Mt.bigLift * 0.5f); }
        return true;
    }

    bool Help45Tick(Frog f, out Vector3 pos, out Vector3 look)
    {
        pos = f.transform.position + new Vector3(-6f, 4f, -8f); look = f.transform.position + Vector3.up;
        if (d45Ph == 0 && d45PhT > 4f) { d45Ph = 1; help = true; helpBy = slots.Count > 0 ? slots[0] : null; Debug.Log("FFDEMO help45 open on foot t=" + d45T.ToString("0.0")); }
        else if (d45Ph == 1 && d45PhT > 10f)
        {
            d45Ph = 2;
            Vehicle ct = null; foreach (var v in Vehicle.All) if (v != null && v.Title == "James's Cybertruck") ct = v;
            if (ct != null) { f.EnterVehicle(ct); BigWheels.SetFor(ct, 2f, "demo", false); }
            Debug.Log("FFDEMO help45 in the vehicle t=" + d45T.ToString("0.0") + " " + (ct != null ? ct.Title : "none"));
        }
        else if (d45Ph == 2 && d45PhT > 20f) { d45Ph = 3; hpScroll = 1e6f; Debug.Log("FFDEMO help45 scrolled to the end t=" + d45T.ToString("0.0") + " max scroll " + hpMaxScroll.ToString("0")); }
        else if (d45Ph == 3 && d45PhT > 28f) { d45Ph = 4; Debug.Log("FFDEMO help45 done " + HelpLayoutInfo); }
        if (help && d45Log <= 0f) { d45Log = 2f; Debug.Log("FFDEMO help45 t=" + d45T.ToString("0.0") + " " + HelpLayoutInfo + " | wheel row '" + (wheelLabel != null ? wheelLabel.text : "") + "' / '" + (hpWheelHint != null ? hpWheelHint.text : "") + "' scroll " + hpScroll.ToString("0")); }
        if (f.vehicle != null) { Transform t = f.vehicle.transform; pos = t.position - t.forward * 9f + Vector3.up * 4f; look = t.position + Vector3.up; }
        return true;
    }
}
