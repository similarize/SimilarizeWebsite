using UnityEngine;

// ffu65 demos for the probe.
// ?ffdemo=1&ffshot=house65 : come in the front door (stairs hint toast), walk up the MIDDLE of the stairs to the 2nd floor and back
//   down, then up the open SIDE (where ffu64 bumped its head at step 11) and down, the phone's "Me: go upstairs (house)" shortcut,
//   then look UP (look input, the same path as touch drag / right stick / mouse) on the ground floor and on the 2nd floor.
// ?ffdemo=1&ffshot=dad65 : Dad the froggy daddy cooking at the stove, walking round the house, then into a camper and look up there.
// Logs "HOUSE65T|DAD65T PASS|FAIL ...", "... RESULT n/m", "FFDEMO house65|dad65 ph N <name>", "FFDEMO <shot> done".
[DefaultExecutionOrder(30002)]
public class Ffu65 : MonoBehaviour
{
    public static Ffu65 I;
    string shot = ""; bool done; float t0 = -1f, phT; int ph = -1, pass, total;
    Frog f; float hiY, loY; Vector3 dadP0; float dadMoved; bool camOn; Vector3 camPos, camLook;
    Trailer tr; Camper64.Door door; Camper64.Room room;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        I = new GameObject("ffu65").AddComponent<Ffu65>();
        DontDestroyOnLoad(I.gameObject);
    }

    string Tag { get { return shot == "dad65" ? "DAD65T" : "HOUSE65T"; } }
    void Update()
    {
        if (shot.Length == 0) { string s = Ffu33.Url("ffdemo") != null ? Ffu33.Url("ffshot") : null; if (s != "house65" && s != "dad65") { enabled = false; return; } shot = s; }
        if (done || Game.I == null || Game.I.state != Game.State.Play || Game.I.slots.Count == 0) return;
        if (t0 < 0f) { t0 = Time.time; f = Game.I.frogs[Game.I.slots[0].frog]; Debug.Log("FFDEMO " + shot + " start p1 " + f.nick); }
        phT += Time.deltaTime;
        try { if (shot == "dad65") DadDemo(); else House(); } catch (System.Exception e) { Debug.LogError("FFDEMO " + shot + " error " + e); Done(); }
    }
    void Next(int p, string name) { ph = p; phT = 0f; Debug.Log("FFDEMO " + shot + " ph " + p + " " + name + " t " + (Time.time - t0).ToString("0.0")); }
    void Check(string what, bool ok, string detail) { total++; if (ok) pass++; Debug.Log(Tag + " " + (ok ? "PASS" : "FAIL") + " " + what + " | " + detail); }
    void Done() { if (done) return; done = true; camOn = false; CamRig.DemoLookY = 0f; Debug.Log(Tag + " RESULT " + pass + "/" + total); Debug.Log("FFDEMO " + shot + " done"); }
    Vector3 HL(float x, float y, float z) { return HouseWorld.L(x, y, z); }
    float LY { get { return f.transform.position.y - Worlds.HouseO.y; } }
    void Walk(Vector3 dir) { if (f.cc != null && f.cc.enabled && f.world == WorldId.House) f.cc.Move(dir * Mathf.Min(0.25f, Time.deltaTime * 3f) + Vector3.down * 0.02f); }
    CamRig Rig { get { return Game.I.slots[0].rig; } }
    Camera Cam { get { return Game.I.slots[0].cam; } }
    float UpDeg { get { return Mathf.Asin(Mathf.Clamp(Cam.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg; } }

    // look up through the look input until 60+ deg (or 8 s), then check the view angle and that the camera is inside the room
    bool LookUp(string where, float ceilY)
    {
        CamRig.DemoLookY = UpDeg < 62f ? 2.5f : 0f;
        if (phT < 25f && UpDeg < 62f) return false;   // 65a: 8 s ran out at probe fps (pitch only -20 in the living room)
        CamRig.DemoLookY = 0f;
        Vector3 cp = Cam.transform.position;
        bool inWall = Physics.CheckSphere(cp, 0.05f, 1 << HouseWorld.WallLayer, QueryTriggerInteraction.Ignore);
        Check("look up " + where + " (>= 60 deg)", UpDeg >= 59f, "view " + UpDeg.ToString("0") + " deg up, rig pitch " + Rig.pitch.ToString("0") + " min " + Rig.minPitch.ToString("0") + " upK " + Rig.UpK.ToString("0.00"));
        Check("camera under the ceiling " + where, cp.y < ceilY - 0.05f && !inWall, "cam y " + cp.y.ToString("0.00") + " ceiling " + ceilY.ToString("0.00") + " inWall " + inWall);
        return true;
    }

    void House()
    {
        switch (ph)
        {
            case -1:
                if (phT > 4f && HouseWorld.I != null)
                {
                    if (f.vehicle != null) f.ExitVehicle();
                    HouseWorld.I.Enter(f);
                    Next(0, "enter");
                }
                break;
            case 0:
                if (phT > 1.5f)
                {
                    Check("stairs hint on first entry", HouseUp64.Hints > 0 && f.toast.Contains("Stairs to 2nd floor"), "toast '" + f.toast + "' hints " + HouseUp64.Hints);
                    camOn = true; camPos = HL(-2f, 2.2f, 17.5f); camLook = HL(-12f, 1.2f, 7f);   // what the player sees from the door: the arrows + sign
                    Next(1, "findstairs");
                }
                break;
            case 1:
                if (phT > 3f) { camOn = false; f.SendTo(WorldId.House, HL(-21.0f, 0.2f, 5.4f), 270f); hiY = 0f; Next(2, "upmiddle"); }
                break;
            case 2:
            case 4:
                Walk(Vector3.left); hiY = Mathf.Max(hiY, LY);
                camOn = true; camPos = HL(-17f, 3.2f, 13f); camLook = f.transform.position + Vector3.up * 0.6f;
                if (phT > 40f || hiY > 4.75f)
                {
                    Check("walk up the stairs (" + (ph == 2 ? "middle" : "open side") + ") to the 2nd floor", hiY > 4.6f, "highest local y " + hiY.ToString("0.00") + " at x " + (f.transform.position.x - Worlds.HouseO.x).ToString("0.0") + " z " + (f.transform.position.z - Worlds.HouseO.z).ToString("0.00"));
                    if (hiY < 4.6f) f.SendTo(WorldId.House, HL(-29f, 4.95f, ph == 2 ? 5.4f : 6.1f), 90f);
                    loY = 99f; Next(ph + 1, ph == 2 ? "downmiddle" : "downside");
                }
                break;
            case 3:
            case 5:
                Walk(Vector3.right); loY = Mathf.Min(loY, LY);
                camOn = true; camPos = HL(-17f, 3.2f, 13f); camLook = f.transform.position + Vector3.up * 0.6f;
                if (phT > 40f || (loY < 0.3f && phT > 2f))
                {
                    Check("walk back down the stairs (" + (ph == 3 ? "middle" : "side") + ")", loY < 0.4f, "lowest local y " + loY.ToString("0.00"));
                    if (ph == 3) { f.SendTo(WorldId.House, HL(-21.0f, 0.2f, 6.1f), 270f); hiY = 0f; Next(4, "upside"); }
                    else { camOn = false; f.SendTo(WorldId.Ranch, new Vector3(Layout.HouseC.x, 0.6f, Layout.HouseC.y + Layout.HouseSize.y * 0.5f + 4f), 0f); Next(6, "shortcut"); }
                }
                break;
            case 6:
                if (phT > 2f && phT - Time.deltaTime <= 2f)
                {
                    string say = HouseUp64.GoUpstairs(f);
                    Check("phone 'Me: go upstairs (house)' shortcut", f.world == WorldId.House && LY > 4.5f, "say '" + say + "' local y " + LY.ToString("0.00"));
                }
                if (phT > 4f) { f.SendTo(WorldId.House, HL(-14f, 0.2f, 12f), 0f); Rig.Snap(); Next(7, "lookup1"); }
                break;
            case 7:
                if (phT > 1.5f && LookUp("in the living room", Worlds.HouseO.y + Light55.HouseH)) Next(8, "lookup1hold");
                break;
            case 8:
                CamRig.DemoLookY = 0f;
                if (phT > 3f) { f.SendTo(WorldId.House, HL(-1f, 4.95f, -12f), 0f); Rig.Snap(); Next(9, "lookup2"); }
                break;
            case 9:
                if (phT > 1.5f && LookUp("on the 2nd floor", Worlds.HouseO.y + HouseUp64.F2 + HouseUp64.WH)) Next(10, "lookup2hold");
                break;
            case 10:
                if (phT > 3f) Done();
                break;
        }
    }

    void DadDemo()
    {
        Dad d = Dad.I;
        switch (ph)
        {
            case -1:
                if (phT > 4f && HouseWorld.I != null)
                {
                    if (f.vehicle != null) f.ExitVehicle();
                    HouseWorld.I.Enter(f);
                    Next(0, "enter");
                }
                break;
            case 0:
                if (phT > 1f)
                {
                    d = Dad.I;
                    Check("Dad is a froggy daddy (froggy model, 1.5x)", d != null && d.model != null && d.model.bob != null && d.model.transform.localScale.x > 1.3f, d == null ? "no Dad" : "model " + (d.model != null) + " scale " + (d.model != null ? d.model.transform.localScale.x.ToString("0.0") : "-"));
                    if (d == null) { Done(); return; }
                    f.SendTo(WorldId.House, HL(15.5f, 0.2f, 14.5f), 315f);
                    d.DemoPlan(0);
                    Next(1, "dadcook");
                }
                break;
            case 1:
                camOn = true; camPos = d.transform.position + new Vector3(-1.9f, 2.1f, -2.3f); camLook = d.transform.position + Vector3.up * 1.0f;   // 65a: fixed spots were behind a wall
                if (d.act == "cook" && phT > 2f || phT > 30f)
                {
                    Check("Dad cooks at the stove", d.act == "cook" && Vector3.Distance(d.transform.position, HL(13f, 0f, 17.6f)) < 0.6f, "act " + d.act + " at " + (d.transform.position - Worlds.HouseO).ToString("0.0"));
                    Next(2, "dadcookhold");
                }
                break;
            case 2:
                camOn = true; camPos = d.transform.position + new Vector3(-1.9f, 2.1f, -2.3f); camLook = d.transform.position + Vector3.up * 1.0f;   // 65a: fixed spots were behind a wall
                if (phT > 3f) { d.DemoPlan(4); dadP0 = d.transform.position; dadMoved = 0f; Next(3, "dadwalk"); }
                break;
            case 3:
                dadMoved = Mathf.Max(dadMoved, Vector3.Distance(d.transform.position, dadP0));
                camOn = true; camPos = d.transform.position - d.transform.forward * 3.2f + Vector3.up * 2.2f; camLook = d.transform.position + Vector3.up * 1.0f;   // follow Dad
                if (phT > 12f)
                {
                    Check("Dad walks round the house", dadMoved > 4f && d.model != null, "moved " + dadMoved.ToString("0.0") + " m, act " + d.act + " at " + (d.transform.position - Worlds.HouseO).ToString("0.0"));
                    camOn = false; Next(4, "tocamper");
                }
                break;
            case 4:
                {
                    var C = Camper64.I;
                    if (C == null || !TrailerBarn.Built || TrailerBarn.Trailers.Count == 0) { if (phT > 10f) { Check("camper available", false, "no camper"); Done(); } return; }
                    foreach (var x in TrailerBarn.Trailers) if (x != null && x.spec.style == 0) { tr = x; break; }
                    if (tr == null) tr = TrailerBarn.Trailers[0];
                    door = C.DoorOf(tr); if (door == null) return;
                    Vector3 df = C.DemoDoorFront(door);
                    f.SendTo(WorldId.Ranch, new Vector3(df.x, Ranch.GY(df.x, df.z) + 0.3f, df.z), tr.transform.eulerAngles.y - 90f);
                    Next(5, "camperdoor");
                }
                break;
            case 5:
                if (phT > 2f && phT - Time.deltaTime <= 2f) Camper64.I.DemoUse(f, door);
                if (Camper64.In(f) && phT > 4f) { room = Camper64.I.RoomOf(tr.spec.style); Rig.Snap(); Next(6, "lookup3"); }
                else if (phT > 30f) { Check("inside the camper", false, "world " + f.world); Done(); }
                break;
            case 6:
                if (phT > 1.5f && LookUp("in the camper", room != null ? room.o.y + room.H : Worlds.HouseO.y + 3f)) Next(7, "lookup3hold");
                break;
            case 7:
                if (phT > 3f) Done();
                break;
        }
    }

    void LateUpdate()
    {
        if (!camOn || done || Game.I == null || Game.I.slots.Count == 0) return;
        Camera c = Game.I.slots[0].cam; if (c == null) return;
        c.transform.position = camPos; c.transform.LookAt(camLook);
    }
}
