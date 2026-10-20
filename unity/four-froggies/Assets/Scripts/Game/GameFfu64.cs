using UnityEngine;

// ffu64 demos for the probe. ?ffdemo=1&ffshot=camper64 : walk up to the Silver Bullet, E -> door swings open, inside the furnished
// interior (camera inside the room, walls solid), lamp off / on, go outside next to the trailer, then hitch it to a truck and
// tow it (still tows with the door parts on). Logs "CAMPER64T PASS|FAIL ...", "CAMPER64T RESULT n/m", "FFDEMO camper64 ph N <name>".
[DefaultExecutionOrder(30001)]
public class Ffu64 : MonoBehaviour
{
    public static Ffu64 I;
    string shot = ""; bool done; float t0 = -1f, phT; int ph = -1, pass, total;
    Frog f; Trailer tr; Camper64.Door door; Camper64.Room room; Vehicle v; Vector3 trStart; bool camOn; Vector3 camPos, camLook;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        I = new GameObject("ffu64").AddComponent<Ffu64>();
        DontDestroyOnLoad(I.gameObject);
    }

    void Update()
    {
        if (shot.Length == 0) { string s = Ffu33.Url("ffdemo") != null ? Ffu33.Url("ffshot") : null; if (s != "camper64") { enabled = false; return; } shot = s; }
        if (done || Game.I == null || Game.I.state != Game.State.Play || Game.I.slots.Count == 0) return;
        if (t0 < 0f) { t0 = Time.time; f = Game.I.frogs[Game.I.slots[0].frog]; Debug.Log("FFDEMO " + shot + " start p1 " + f.nick); }
        phT += Time.deltaTime;
        try { Camper(); } catch (System.Exception e) { Debug.LogError("FFDEMO " + shot + " error " + e); Done(); }
    }

    void Next(int p, string name) { ph = p; phT = 0f; Debug.Log("FFDEMO " + shot + " ph " + p + " " + name + " t " + (Time.time - t0).ToString("0.0")); }
    void Check(string what, bool ok, string detail) { total++; if (ok) pass++; Debug.Log("CAMPER64T " + (ok ? "PASS" : "FAIL") + " " + what + " | " + detail); }
    void Done() { if (done) return; done = true; camOn = false; Debug.Log("CAMPER64T RESULT " + pass + "/" + total); Debug.Log("FFDEMO " + shot + " done"); }

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
                if (phT > 0.35f && phT - Time.deltaTime <= 0.35f) { float ang = door.hinge.localEulerAngles.y; ang = ang > 180f ? ang - 360f : ang; Check("door swings open", Mathf.Abs(ang) > 25f, "hinge " + ang.ToString("0")); }
                if (phT > 2.2f)
                {
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

    static float Flat(Vector3 d) { d.y = 0f; return d.magnitude; }

    void LateUpdate()
    {
        if (!camOn || done || Game.I == null || Game.I.slots.Count == 0) return;
        Camera c = Game.I.slots[0].cam; if (c == null) return;
        c.transform.position = camPos; c.transform.LookAt(camLook);
    }
}
