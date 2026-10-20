using UnityEngine;

// ffu64 demos for the probe. ?ffdemo=1&ffshot=camper64 : walk up to the Silver Bullet, E -> door swings open, inside the furnished
// interior (camera inside the room, walls solid), lamp off / on, go outside next to the trailer, then hitch it to a truck and
// tow it (still tows with the door parts on). Logs "CAMPER64T PASS|FAIL ...", "CAMPER64T RESULT n/m", "FFDEMO camper64 ph N <name>".
[DefaultExecutionOrder(30001)]
public class Ffu64 : MonoBehaviour
{
    public static Ffu64 I;
    string shot = ""; bool done; float t0 = -1f, phT; int ph = -1, pass, total;
    Frog f; Trailer tr; Camper64.Door door; Camper64.Room room; Vehicle v; Vector3 trStart; bool camOn; float maxAng, inT = -1f; Vector3 camPos, camLook;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        I = new GameObject("ffu64").AddComponent<Ffu64>();
        DontDestroyOnLoad(I.gameObject);
    }

    void Update()
    {
        if (shot.Length == 0) { string s = Ffu33.Url("ffdemo") != null ? Ffu33.Url("ffshot") : null; if (s != "camper64" && s != "mech64" && s != "house64") { enabled = false; return; } shot = s; }
        if (done || Game.I == null || Game.I.state != Game.State.Play || Game.I.slots.Count == 0) return;
        if (t0 < 0f) { t0 = Time.time; f = Game.I.frogs[Game.I.slots[0].frog]; Debug.Log("FFDEMO " + shot + " start p1 " + f.nick); }
        phT += Time.deltaTime;
        try { if (shot == "mech64") Mech(); else if (shot == "house64") House(); else Camper(); } catch (System.Exception e) { Debug.LogError("FFDEMO " + shot + " error " + e); Done(); }
    }

    void Next(int p, string name) { ph = p; phT = 0f; Debug.Log("FFDEMO " + shot + " ph " + p + " " + name + " t " + (Time.time - t0).ToString("0.0")); }
    void Check(string what, bool ok, string detail) { total++; if (ok) pass++; Debug.Log((shot == "mech64" ? "MECH64T " : shot == "house64" ? "HOUSE64T " : "CAMPER64T ") + (ok ? "PASS" : "FAIL") + " " + what + " | " + detail); }
    void Done() { if (done) return; done = true; camOn = false; Debug.Log((shot == "mech64" ? "MECH64T" : shot == "house64" ? "HOUSE64T" : "CAMPER64T") + " RESULT " + pass + "/" + total); Debug.Log("FFDEMO " + shot + " done"); }

    static void Place(Transform t, Rigidbody rb, Vector3 p, Quaternion q)
    {
        t.SetPositionAndRotation(p, q);
        if (rb != null) { rb.position = p; rb.rotation = q; rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        Physics.SyncTransforms();
    }

    void Camper()
    {
        var C = Camper64.I;
        switch (ph)
        {
            case -1:
                if (phT > 4f && TrailerBarn.Built && TrailerBarn.Trailers.Count > 0 && C != null)
                {
                    foreach (var x in TrailerBarn.Trailers) if (x != null && x.spec.style == 0) { tr = x; break; }
                    if (tr == null) tr = TrailerBarn.Trailers[0];
                    door = C.DoorOf(tr);
                    if (door == null) { Debug.Log("FFDEMO camper64 waiting for the door"); return; }
                    // open ground next to the barn (as hitch62)
                    Vector3 o = TrailerBarn.Centre, spot = o + new Vector3(0f, 0f, -38f);
                    for (int k = 0; k < 16; k++)
                    {
                        float a = k * 22.5f * Mathf.Deg2Rad; Vector3 p = o + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 38f;
                        if (BigWheels.Building(p) == null && BigWheels.Building(p + Vector3.forward * 8f) == null && BigWheels.Building(p - Vector3.forward * 8f) == null) { spot = p; break; }
                    }
                    float ty = tr.transform.position.y - Ranch.GY(tr.transform.position.x, tr.transform.position.z);
                    Place(tr.transform, tr.GetComponent<Rigidbody>(), new Vector3(spot.x, Ranch.GY(spot.x, spot.z) + ty, spot.z), Quaternion.identity);
                    if (f.vehicle != null) f.ExitVehicle();
                    Vector3 df = C.DemoDoorFront(door);
                    f.SendTo(WorldId.Ranch, new Vector3(df.x, Ranch.GY(df.x, df.z) + 0.3f, df.z), tr.transform.eulerAngles.y - 90f);
                    camOn = true; camPos = df + tr.transform.right * 3.2f + tr.transform.forward * 1.6f + Vector3.up * 1.4f; camLook = tr.transform.TransformPoint(new Vector3(tr.spec.wid * 0.5f, 1.2f, door.d.z));
                    Debug.Log("CAMPER64T trailer " + tr.spec.shortName + " at " + tr.transform.position.ToString("0.0") + " door front " + df.ToString("0.0"));
                    Next(0, "outside");
                }
                break;
            case 0:
                if (phT > 3f)
                {
                    float d; var h = Interact.Nearest(f.transform.position, f, out d);
                    Check("door hotspot", h == door.hs, "nearest " + (h != null ? h.Label(f) : "none") + " d " + d.ToString("0.00"));
                    C.DemoUse(f, door);
                    Next(1, "open");
                }
                break;
            case 1:
                { float ang = door.hinge.localEulerAngles.y; ang = ang > 180f ? ang - 360f : ang; if (Mathf.Abs(ang) > Mathf.Abs(maxAng)) maxAng = ang; }
                if (Camper64.In(f) && inT < 0f) inT = phT;
                if ((inT >= 0f && phT > inT + 1.5f) || phT > 25f)
                {
                    Check("door swings open", Mathf.Abs(maxAng) > 60f, "max hinge " + maxAng.ToString("0") + " deg");
                    room = C.RoomOf(tr.spec.style);
                    Check("walked inside", room != null && Camper64.In(f), "world " + f.world + " pos " + (room != null ? (f.transform.position - room.o).ToString("0.00") : "-") + " hud " + Worlds.NameAt(f.world, f.transform.position));
                    camOn = false;
                    if (room == null) { Done(); return; }
                    Next(2, "inside");
                }
                break;
            case 2:
                if (phT > 3f)
                {
                    var cam = Game.I.slots[0].cam;
                    Check("camera inside the room", cam != null && room.box.Contains(cam.transform.position), "cam " + (cam != null ? (cam.transform.position - room.o).ToString("0.00") : "-") + " half " + (room.W * 0.5f).ToString("0.00") + " H " + room.H.ToString("0.00"));
                    Vector3 a = f.transform.position;
                    if (f.cc != null && f.cc.enabled) { f.cc.Move(Vector3.left * 3f); f.cc.Move(Vector3.forward * 9f); }
                    Vector3 b = f.transform.position - room.o;
                    Check("walls + furniture solid", b.x > -room.W * 0.5f && b.z < room.L * 0.5f && b.y > -0.2f, "pushed 3 m left + 9 m forward -> local " + b.ToString("0.00") + " (inside +-" + (room.W * 0.5f).ToString("0.00") + " / " + (room.L * 0.5f).ToString("0.00") + ")");
                    f.SendTo(WorldId.House, room.o + new Vector3(room.W * 0.5f - 0.5f, 0.12f, room.d.z), 0f);
                    Next(3, "lamp");
                }
                break;
            case 3:
                if (phT > 2.5f) { C.DemoLamp(room); Check("lamp off", room.lamp != null && !room.lamp.enabled && !room.ceil.enabled, "lamp " + room.lamp.enabled); Next(4, "lampoff"); }
                break;
            case 4:
                if (phT > 2.5f) { C.DemoLamp(room); Check("lamp on", room.lamp.enabled, "lamp " + room.lamp.enabled); f.SendTo(WorldId.House, f.transform.position, 180f); Next(5, "bedview"); }
                break;
            case 5:
                if (phT > 3f)
                {
                    float d; var h = Interact.Nearest(f.transform.position, f, out d);
                    f.Teleport(room.o + new Vector3(room.W * 0.5f - 0.45f, 0.12f, room.d.z));
                    C.DemoExit(f, room);
                    Vector3 df = C.DemoDoorFront(door);
                    Check("back outside by the trailer", f.world == WorldId.Ranch && (Flat(f.transform.position - df) < 2.5f), "pos " + f.transform.position.ToString("0.0") + " door " + df.ToString("0.0"));
                    Next(6, "out");
                }
                break;
            case 6:
                if (phT > 2.5f)
                {
                    foreach (var x in Vehicle.All) if (x != null && TrailerTow.CanTow(x) && x.Title != null && x.Title.Contains("Cyber")) { v = x; break; }
                    if (v == null) foreach (var x in Vehicle.All) if (x != null && TrailerTow.CanTow(x)) { v = x; break; }
                    if (v == null) { Check("tow", false, "no tow vehicle"); Done(); return; }
                    Quaternion rot = tr.transform.rotation; Vector3 ball = tr.BallWorld;
                    float vy = v.transform.position.y - Ranch.GY(v.transform.position.x, v.transform.position.z);
                    Vector3 hl = Trailer.HitchLocal(v);
                    Vector3 vp = ball - rot * new Vector3(hl.x, 0f, hl.z); vp.y = Ranch.GY(vp.x, vp.z) + vy;
                    Place(v.transform, v.rb, vp, rot);
                    f.Teleport(tr.transform.position + tr.transform.right * 6f + Vector3.up * 0.5f);
                    Next(7, "settle");
                }
                break;
            case 7:
                if (phT > 2f) { tr.Hitch(v); trStart = tr.transform.position; Next(8, "tow"); }
                break;
            case 8:
                {
                    Vector3 vel = v.transform.forward * 6f; vel.y = v.rb.velocity.y; v.rb.velocity = vel;
                    camOn = true; camPos = tr.transform.position + tr.transform.right * 9f + Vector3.up * 3f; camLook = tr.transform.position + Vector3.up;
                    if (phT > 1.5f && phT - Time.deltaTime <= 1.5f) { float d; var h = Interact.Nearest(f.transform.position, f, out d); Debug.Log("CAMPER64T label while towing: " + (door.hs.dynLabel != null ? door.hs.dynLabel(f) : "")); }
                    if (phT > 5f)
                    {
                        float moved = Flat(tr.transform.position - trStart), gap = (Trailer.HitchWorld(v) - tr.BallWorld).magnitude;
                        float up = Vector3.Dot(tr.transform.up, Vector3.up);
                        Check("still tows", tr.Hitched && moved > 10f && gap < 0.35f && up > 0.9f, "moved " + moved.ToString("0.0") + " m, hitched " + tr.Hitched + " ball gap " + gap.ToString("0.00") + " up " + up.ToString("0.00") + " door parts " + (door.hinge != null));
                        v.rb.velocity = Vector3.zero;
                        Done();
                    }
                }
                break;
        }
    }


    // ---------------- mech64: drone into the torso, land on the shoulder, ride along, land on the head, helicopter too ----------------
    StoryMech mech; Flyer fly, heli; Mech64.Pad torsoP, headP; Vector3 mStart; float mYaw, minIn; bool wasInside;
    Vector3 PadTop(Mech64.Pad p, float side)
    {
        Transform t = p.col.transform; Vector3 sz = p.col.size;
        return t.TransformPoint(new Vector3(sz.x * 0.32f * side, sz.y * 0.5f, 0f));
    }
    void Drop(Flyer fl, Vector3 p, Vector3 vel)
    {
        fl.returning = false;
        Place(fl.transform, fl.rb, p, Quaternion.Euler(0f, mech.transform.eulerAngles.y, 0f));
        fl.rb.velocity = vel;
    }
    void Mech()
    {
        switch (ph)
        {
            case -1:
                if (phT > 5f && Mech64.I != null)
                {
                    foreach (var m in StoryMech.AllMechs) if (m != null && m.band == 1 && m.driver == null && !m.wrecked) { mech = m; break; }
                    if (mech == null) foreach (var m in StoryMech.AllMechs) if (m != null && m.driver == null) { mech = m; break; }
                    foreach (var v in Vehicle.All) { var fl = v as Flyer; if (fl == null || fl.driver != null) continue; if (fl.isDrone && fly == null) fly = fl; if (!fl.isDrone && heli == null) heli = fl; }
                    if (mech == null || fly == null) { Debug.Log("FFDEMO mech64 waiting mech " + (mech != null) + " drone " + (fly != null)); if (phT > 30f) { Check("setup", false, "no mech / drone"); Done(); } return; }
                    var l = Mech64.I.PadsOf(mech);
                    if (!Mech64.On) { Debug.Log("MECH64T knob off (before)"); }
                    foreach (var p in l) { if (p.name.EndsWith("shoulders")) torsoP = p; if (p.name.EndsWith("head")) headP = p; }
                    if (Mech64.On && (torsoP == null || headP == null)) { if (phT > 30f) { Check("pads", false, "pads " + l.Count); Done(); } return; }
                    mStart = mech.transform.position; mYaw = mech.transform.eulerAngles.y;
                    Debug.Log("MECH64T mech " + mech.Title + " H " + mech.height + " at " + mStart.ToString("0") + " drone " + fly.Title + " heli " + (heli != null ? heli.Title : "none") + " pads " + l.Count);
                    Next(0, "collide");
                    Vector3 c = torsoP != null ? torsoP.col.transform.position : mech.transform.position + Vector3.up * mech.height * 0.66f;
                    Drop(fly, c + mech.transform.forward * (mech.height * 0.5f + 6f), -mech.transform.forward * 16f + Vector3.up * 6f);
                    minIn = 1e9f; wasInside = false;
                }
                break;
            case 0:
                {
                    Vector3 c = torsoP != null ? torsoP.col.transform.position : mech.transform.position + Vector3.up * mech.height * 0.66f;
                    float depth = mech.height * 0.11f;
                    float fwd = Vector3.Dot(fly.transform.position - c, mech.transform.forward);
                    minIn = Mathf.Min(minIn, fwd);
                    if (Mathf.Abs(fwd) < depth * 0.5f && Mathf.Abs(Vector3.Dot(fly.transform.position - c, mech.transform.right)) < mech.height * 0.15f) wasInside = true;
                    camOn = true; camPos = c + mech.transform.right * (mech.height * 0.9f + 8f) + mech.transform.forward * 6f + Vector3.up * 2f; camLook = c + mech.transform.forward * 3f;
                    if (phT > 2.5f)
                    {
                        Check("drone bumps the torso (no fly-through)", !wasInside && minIn > -depth * 0.5f, "closest forward offset " + minIn.ToString("0.0") + " m (torso half depth " + (depth * 0.5f).ToString("0.0") + "), inside " + wasInside + " drone v " + fly.rb.velocity.magnitude.ToString("0.0"));
                        if (torsoP == null) { Done(); return; }
                        Drop(fly, PadTop(torsoP, -1f) + Vector3.up * 3.5f, Vector3.zero);
                        Next(1, "shoulder");
                    }
                }
                break;
            case 1:
                camOn = true; camPos = PadTop(torsoP, -1f) - mech.transform.right * 12f + mech.transform.forward * 9f + Vector3.up * 4f; camLook = PadTop(torsoP, -1f);
                if (phT > 4.5f)
                {
                    float h = fly.AltitudeAboveGround;
                    Check("lands on the shoulder", Mech64.IsPad(fly.GroundCol64) && h < 0.6f, "h " + h.ToString("0.00") + " under " + (fly.GroundCol64 != null ? fly.GroundCol64.name : "none") + " riding " + fly.ride64);
                    Next(2, "ride");
                }
                break;
            case 2:
                {
                    float k = Mathf.Min(phT, 6f);
                    Vector3 fw = Quaternion.Euler(0f, mYaw, 0f) * Vector3.forward;
                    mech.DemoPlace(mStart + fw * k * 3f, mYaw + k * 6f);
                    camOn = true; camPos = PadTop(torsoP, -1f) - mech.transform.right * 14f + mech.transform.forward * 10f + Vector3.up * 5f; camLook = PadTop(torsoP, -1f);
                    if (phT > 6.5f)
                    {
                        float d = Flat(fly.transform.position - PadTop(torsoP, -1f)), h = fly.AltitudeAboveGround;
                        Check("rides along on the moving mech", d < 1.6f && h < 0.7f && Mech64.IsPad(fly.GroundCol64), "mech moved 18 m + turned 36 deg; drone off the pad spot " + d.ToString("0.00") + " m, h " + h.ToString("0.00"));
                        Drop(fly, headP.col.transform.TransformPoint(new Vector3(0f, headP.col.size.y * 0.5f, 0f)) + Vector3.up * 3f, Vector3.zero);
                        Next(3, "head");
                    }
                }
                break;
            case 3:
                {
                    Vector3 ht = headP.col.transform.TransformPoint(new Vector3(0f, headP.col.size.y * 0.5f, 0f));
                    camOn = true; camPos = ht + mech.transform.forward * 12f + mech.transform.right * 6f + Vector3.up * 3f; camLook = ht;
                    if (phT > 4.5f)
                    {
                        float h = fly.AltitudeAboveGround;
                        Check("lands on the head", fly.GroundCol64 == headP.col && h < 0.6f, "h " + h.ToString("0.00") + " under " + (fly.GroundCol64 != null ? fly.GroundCol64.name : "none"));
                        if (heli != null) { Drop(heli, PadTop(torsoP, 1f) + Vector3.up * 4f, Vector3.zero); Next(4, "heli"); }
                        else Done();
                    }
                }
                break;
            case 4:
                camOn = true; camPos = PadTop(torsoP, 1f) + mech.transform.right * 16f + mech.transform.forward * 10f + Vector3.up * 5f; camLook = PadTop(torsoP, 1f);
                if (phT > 5f)
                {
                    float h = heli.AltitudeAboveGround;
                    Check("helicopter lands on the other shoulder", Mech64.IsPad(heli.GroundCol64) && h < 0.8f, "h " + h.ToString("0.00") + " under " + (heli.GroundCol64 != null ? heli.GroundCol64.name : "none"));
                    Done();
                }
                break;
        }
    }


    // ---------------- house64: garage back door <-> hallway, ladder + porch roof, upstairs window in, hop-in through a window,
    // walk up the stairs, upstairs views, climb out a window onto the porch roof ----------------
    Hotspot Near(out float d) { return Interact.Nearest(f.transform.position, f, out d); }
    void Use(string what)
    {
        float d; var h = Near(out d);
        Debug.Log("HOUSE64T use '" + what + "' nearest " + (h != null ? h.Label(f) : "none") + " d " + d.ToString("0.00"));
        if (h != null && h.Label(f).Contains(what)) h.act(f);
    }
    Vector3 HL(float x, float y, float z) { return HouseWorld.L(x, y, z); }
    float startY;
    void House()
    {
        Vector3 g = HouseUp64.GarageDoor;
        Vector2 c = Layout.HouseC; float z1 = c.y + Layout.HouseSize.y * 0.5f;
        switch (ph)
        {
            case -1:
                if (phT > 4f && HouseWorld.I != null)
                {
                    if (f.vehicle != null) f.ExitVehicle();
                    f.SendTo(WorldId.Ranch, new Vector3(g.x, Ranch.GY(g.x, g.z + 1.25f) + 0.3f, g.z + 1.25f), 180f);
                    camOn = true; camPos = new Vector3(g.x + 3.5f, 3.2f, g.z + 7f); camLook = new Vector3(g.x, 1.2f, g.z);
                    Next(0, "garage");
                }
                break;
            case 0:
                if (phT > 3f) { Use("back door"); Next(1, "hallway"); camOn = false; }
                break;
            case 1:
                if (phT > 1f && phT - Time.deltaTime <= 1f)
                {
                    Vector3 lp = f.transform.position - Worlds.HouseO;
                    Check("garage back door -> house hallway", f.world == WorldId.House && lp.x > 30f && lp.x < 44.5f && Mathf.Abs(lp.z - HouseUp64.HallZ) < 2f, "world " + f.world + " local " + lp.ToString("0.0"));
                }
                if (phT > 4f)
                {
                    // walk west through the hallway door into the kitchen (cc moves), then come back and take the garage door
                    if (f.cc != null && f.cc.enabled) for (int i = 0; i < 30; i++) f.cc.Move(Vector3.left * 0.5f);
                    Vector3 lp = f.transform.position - Worlds.HouseO;
                    Check("hallway opens into the kitchen", lp.x < 29.5f, "after 15 m west local " + lp.ToString("0.0"));
                    f.SendTo(WorldId.House, HL(43.2f, 0.15f, HouseUp64.HallZ), 90f);
                    Use("garage");
                    Next(2, "backgarage");
                }
                break;
            case 2:
                if (phT > 2f)
                {
                    Check("hallway -> garage", f.world == WorldId.Ranch && RobotNav.InGarage(f.transform.position), "pos " + f.transform.position.ToString("0.0"));
                    float lz = z1 + 4.5f; f.SendTo(WorldId.Ranch, new Vector3(-54.85f, Ranch.GY(-54.85f, lz) + 0.3f, lz), 90f);
                    Next(3, "ladder");
                }
                break;
            case 3:
                if (phT > 2f) { Use("ladder"); Next(4, "porchroof"); }
                break;
            case 4:
                camOn = true; camPos = new Vector3(-48f, 7.5f, z1 + 13f); camLook = new Vector3(-50f, 4f, z1 + 3f);
                if (phT > 3f)
                {
                    if (f.cc != null && f.cc.enabled) for (int i = 0; i < 16; i++) f.cc.Move(Vector3.right * 0.5f + Vector3.down * 0.3f);
                    Check("porch roof is walkable", f.world == WorldId.Ranch && f.transform.position.y > 3.6f, "after walking 8 m along it y " + f.transform.position.y.ToString("0.00") + " x " + f.transform.position.x.ToString("0.0"));
                    float wx = Layout.HouseC.x; f.Teleport(new Vector3(wx, 4.2f, z1 + 0.9f));
                    Next(5, "window");
                }
                break;
            case 5:
                if (phT > 2f)
                {
                    Use("upstairs window");
                    Next(6, "upstairs");
                    camOn = false;
                }
                break;
            case 6:
                if (phT > 1f && phT - Time.deltaTime <= 1f) Check("in through the upstairs window -> 2nd floor", f.world == WorldId.House && f.transform.position.y > Worlds.HouseO.y + 4.5f, "world " + f.world + " local " + (f.transform.position - Worlds.HouseO).ToString("0.0"));
                if (phT > 4f)
                {
                    // hop in through another upstairs window from outside (side wall): lands in the hollow shell at 6.5 m
                    f.SendTo(WorldId.Ranch, new Vector3(Layout.HouseC.x - Layout.HouseSize.x * 0.5f + 1.5f, 6.4f, Layout.HouseC.y - 3f), 90f);
                    Next(7, "hopin");
                }
                break;
            case 7:
                if (phT > 2f)
                {
                    Check("hopping in an upstairs window is NOT a kick-out", f.world == WorldId.House && f.transform.position.y > Worlds.HouseO.y + 4.5f, "world " + f.world + " local " + (f.world == WorldId.House ? (f.transform.position - Worlds.HouseO).ToString("0.0") : f.transform.position.ToString("0.0")) + " ups " + HouseUp64.UpEntries);
                    f.SendTo(WorldId.House, HL(-21.0f, 0.2f, 6.1f), 270f);
                    startY = 0f;
                    Next(8, "stairs");
                }
                break;
            case 8:
                if (f.cc != null && f.cc.enabled && f.world == WorldId.House) f.cc.Move(Vector3.left * Mathf.Min(0.25f, Time.deltaTime * 3f) + Vector3.down * 0.02f);
                startY = Mathf.Max(startY, f.transform.position.y - Worlds.HouseO.y);
                camOn = true; camPos = HL(-17f, 3.2f, 13f); camLook = f.transform.position + Vector3.up * 0.6f;
                if (phT > 9f || startY > 4.7f)
                {
                    Check("walk up the stairs", startY > 4.6f, "highest local y " + startY.ToString("0.00") + " at x " + (f.transform.position.x - Worlds.HouseO.x).ToString("0.0"));
                    if (startY < 4.6f) f.SendTo(WorldId.House, HL(-29f, 4.95f, 5.4f), 0f);
                    else f.SendTo(WorldId.House, f.transform.position + Vector3.forward * 2.5f, 0f);
                    Next(9, "landing");
                }
                break;
            case 9:
                camOn = true; camPos = HL(-12f, 8.0f, 16f); camLook = HL(-22f, 4.8f, 6f);
                if (phT > 3f) { camPos = HL(-20f, 8.2f, -3.5f); camLook = HL(-14f, 4.8f, -12f); Next(10, "jamesroom"); }
                break;
            case 10:
                camOn = true; camPos = HL(-20f, 8.2f, -3.5f); camLook = HL(-14f, 4.8f, -12f);
                if (phT > 3f) { Next(11, "bunkroom"); }
                break;
            case 11:
                camOn = true; camPos = HL(-3f, 8.2f, 3.5f); camLook = HL(6f, 5.5f, 9f);
                if (phT > 3f)
                {
                    camOn = false;
                    f.SendTo(WorldId.House, HL(-6.7f, 4.95f, 18.6f), 0f);
                    Use("climb out");
                    Next(12, "outroof");
                }
                break;
            case 12:
                if (phT > 2f)
                {
                    Check("climb out an upstairs window onto the porch roof", f.world == WorldId.Ranch && f.transform.position.y > 3.6f, "pos " + f.transform.position.ToString("0.0"));
                    Done();
                }
                break;
        }
    }

    static float Flat(Vector3 d) { d.y = 0f; return d.magnitude; }

    void LateUpdate()
    {
        if (!camOn || done || Game.I == null || Game.I.slots.Count == 0) return;
        Camera c = Game.I.slots[0].cam; if (c == null) return;
        c.transform.position = camPos; c.transform.LookAt(camLook);
    }
}
