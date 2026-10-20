using UnityEngine;

// ffu67b probe: ?ffdemo=1&ffshot=house67 - the garage / mudroom / house walk-through (Bill Oct 20: "the garage back door,
// entered from outside, must lead INTO THE GARAGE ... joined by an inside hallway / mudroom with two doors").
// Walks (CharacterController, no teleport) from the lawn through the garage's OUTSIDE back door into the garage, takes the
// garage's mudroom door into the house world's mudroom, walks through the kitchen door into the kitchen, walks back, takes the
// mudroom's garage door back into the garage and walks out of the back door; then checks the other ways in (big bay door,
// front door in / out, phone "go upstairs"), that the mudroom wing is closed on the ranch and its inside walls are solid.
// Logs "HOUSE67T PASS|FAIL ...", "HOUSE67T RESULT n/m", "FFDEMO house67 ph N <name>", "FFDEMO house67 done".
[DefaultExecutionOrder(30004)]
public class Ffu67b : MonoBehaviour
{
    public static Ffu67b I;
    string shot = ""; bool done; float t0 = -1f, phT; int ph = -1, pass, total;
    Frog f; bool camOn; Vector3 camPos, camLook; bool sawOpen, leftRanch; float maxZ, minZ;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        I = new GameObject("ffu67b").AddComponent<Ffu67b>();
        DontDestroyOnLoad(I.gameObject);
    }

    void Update()
    {
        if (shot.Length == 0) { string s = Ffu33.Url("ffdemo") != null ? Ffu33.Url("ffshot") : null; if (s != "house67") { enabled = false; return; } shot = s; }
        if (done || Game.I == null || Game.I.state != Game.State.Play || Game.I.slots.Count == 0) return;
        if (t0 < 0f) { t0 = Time.time; f = Game.I.frogs[Game.I.slots[0].frog]; Debug.Log("FFDEMO house67 start p1 " + f.nick); }
        phT += Time.deltaTime;
        try { Run(); } catch (System.Exception e) { Debug.LogError("FFDEMO house67 error " + e); Done(); }
    }
    void Next(int p, string name) { ph = p; phT = 0f; Debug.Log("FFDEMO house67 ph " + p + " " + name + " t " + (Time.time - t0).ToString("0.0")); }
    void Check(string what, bool ok, string detail) { total++; if (ok) pass++; Debug.Log("HOUSE67T " + (ok ? "PASS" : "FAIL") + " " + what + " | " + detail); }
    void Done() { if (done) return; done = true; camOn = false; Debug.Log("HOUSE67T RESULT " + pass + "/" + total); Debug.Log("FFDEMO house67 done"); }
    Hotspot Near(out float d) { return Interact.Nearest(f.transform.position, f, out d); }
    bool Use(string what)
    {
        float d; var h = Near(out d);
        Debug.Log("HOUSE67T use '" + what + "' nearest " + (h != null ? h.Label(f) : "none") + " d " + d.ToString("0.00"));
        if (h != null && h.Label(f).Contains(what)) { h.act(f); return true; }
        return false;
    }
    Vector3 HL(float x, float y, float z) { return HouseWorld.L(x, y, z); }
    Vector3 LP { get { return f.transform.position - Worlds.HouseO; } }
    void Walk(Vector3 dir, float speed = 2.6f) { if (f.cc != null && f.cc.enabled) f.cc.Move((dir.normalized * speed + Vector3.down * 2f) * Mathf.Min(Time.deltaTime, 0.34f)); }   // fix 1: the window is game time, so the step must follow it at 0.3 fps too (0.1 cap walked 2.6 of 5 m)
    void PutR(Vector3 p, float yaw) { if (f.vehicle != null) f.ExitVehicle(); f.SendTo(WorldId.Ranch, new Vector3(p.x, Ranch.GY(p.x, p.z) + 0.3f, p.z), yaw); }
    static float OZ { get { return Layout.GarageC.y - Layout.GarageSize.y * 0.5f; } }   // garage back wall (outer face)
    bool InMud { get { Vector3 l = LP; return f.world == WorldId.House && l.x > HouseUp64.MLx0 && l.x < HouseUp64.MLx1 && l.z > HouseUp64.MLz0 && l.z < HouseUp64.MLz1; } }

    void Run()
    {
        Vector3 b = HouseUp64.BackDoor, g = HouseUp64.GarageDoor;
        switch (ph)
        {
            case -1:
                if (phT > 4f && HouseWorld.I != null)
                {
                    HouseWorld.I.EnsureBuilt();
                    PutR(new Vector3(b.x, 0f, OZ - 5f), 0f);
                    camOn = true; camPos = new Vector3(b.x + 6f, 3.4f, OZ - 9f); camLook = new Vector3(b.x - 2f, 1.4f, OZ);
                    sawOpen = false; maxZ = -1e9f;
                    Next(0, "outback");
                }
                break;
            case 0:   // outside, walk north through the back door
                if (phT > 2f && phT < 5.5f) Walk(Vector3.forward);
                if (BackDoor67.Open) sawOpen = true;
                maxZ = Mathf.Max(maxZ, f.transform.position.z);
                if (phT > 9f)
                {
                    bool inG = f.world == WorldId.Ranch && RobotNav.InGarage(f.transform.position);
                    Check("outside BACK DOOR -> walks straight INTO THE GARAGE (no teleport)", inG && sawOpen, "world " + f.world + " pos " + f.transform.position.ToString("0.0") + " door opened " + sawOpen + " max z " + maxZ.ToString("0.0"));
                    if (!inG) { PutR(new Vector3(b.x, 0f, OZ - 0.9f), 0f); bool used = Use("back door into the garage"); Check("back door hotspot (E / A) -> into the garage", used && RobotNav.InGarage(f.transform.position), "pos " + f.transform.position.ToString("0.0")); }
                    camPos = new Vector3(b.x + 4f, 3.6f, OZ + 7.5f); camLook = new Vector3(b.x, 1.3f, OZ + 0.5f);
                    Next(1, "garage");
                }
                break;
            case 1:   // in the garage: to the mudroom door
                if (phT > 3f && phT - Time.deltaTime <= 3f) { PutR(new Vector3(g.x, 0f, g.z + 1.25f), 180f); camPos = new Vector3(g.x + 3.5f, 3.2f, g.z + 7f); camLook = new Vector3(g.x, 1.2f, g.z); }
                if (phT > 6f)
                {
                    bool used = Use("mudroom door");
                    camOn = false;
                    Check("garage's MUDROOM door -> the house mudroom", used && InMud, "world " + f.world + " local " + LP.ToString("0.0"));
                    Next(2, "mudroom");
                }
                break;
            case 2:   // mudroom: walls solid all round, then walk to the kitchen door level and west into the kitchen
                if (phT > 0.5f && phT - Time.deltaTime <= 0.5f)
                {
                    Vector3 o = HL(33f, 1.5f, 17f); string hits = ""; int ok = 0;
                    foreach (var d in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left })
                    {
                        RaycastHit h; bool hit = Physics.Raycast(o, d, out h, 12f, 1 << HouseWorld.WallLayer, QueryTriggerInteraction.Ignore);
                        float lim = d == Vector3.forward ? 22.67f - 17f + 0.4f : d == Vector3.back ? 17f - 10.67f + 0.4f : d == Vector3.right ? 36f - 33f + 0.5f : 33f - 30f + 4f;   // west: the kitchen door gap is lower down
                        if (hit && h.distance <= lim) ok++;
                        hits += d + " " + (hit ? h.distance.ToString("0.0") : "none") + "; ";
                    }
                    Check("mudroom walls solid (no seeing / walking out except the 2 doors)", ok >= 3, hits);
                }
                if (phT > 3f && phT < 5f) Walk(Vector3.back);
                if (phT > 5f && phT - Time.deltaTime <= 5f) f.SendTo(WorldId.House, HL(33f, 0.15f, HouseUp64.HallZ), 270f);
                if (phT > 5.5f && phT < 10f) Walk(Vector3.left);
                if (phT > 10f)
                {
                    Vector3 l = LP;
                    Check("mudroom KITCHEN door -> walks into the kitchen", f.world == WorldId.House && l.x < 29.5f && l.x > 6f && l.z > 4f && l.z < 20f, "local " + l.ToString("0.0"));
                    Next(3, "kitchen");
                }
                break;
            case 3:   // kitchen -> back into the mudroom -> its garage door
                if (phT > 3f && phT < 7.5f) Walk(Vector3.right);
                if (phT > 7.5f && phT - Time.deltaTime <= 7.5f) Check("kitchen -> back into the mudroom", InMud, "local " + LP.ToString("0.0"));
                if (phT > 8f && phT - Time.deltaTime <= 8f) f.SendTo(WorldId.House, HL(HouseUp64.MLdoorX, 0.15f, HouseUp64.MLz1 - 1.1f), 0f);
                if (phT > 9f)
                {
                    bool used = Use("go into the garage");
                    Check("mudroom GARAGE door -> James's garage (inside, by the mudroom door)", used && f.world == WorldId.Ranch && RobotNav.InGarage(f.transform.position) && Mathf.Abs(f.transform.position.x - g.x) < 2f, "pos " + f.transform.position.ToString("0.0"));
                    Next(4, "backgarage");
                }
                break;
            case 4:   // garage -> walk out of the back door
                if (phT > 1f && phT - Time.deltaTime <= 1f) { PutR(new Vector3(b.x, 0f, OZ + 3.5f), 180f); camOn = true; camPos = new Vector3(b.x + 4f, 3.6f, OZ + 7.5f); camLook = new Vector3(b.x, 1.3f, OZ); minZ = 1e9f; sawOpen = false; }
                if (phT > 2.5f && phT < 8f) Walk(Vector3.back);
                if (BackDoor67.Open) sawOpen = true;
                minZ = Mathf.Min(minZ, f.transform.position.z);
                if (phT > 8f)
                {
                    bool outside = f.world == WorldId.Ranch && !RobotNav.InGarage(f.transform.position) && f.transform.position.z < OZ - 0.5f;
                    Check("garage -> walks OUT of the back door onto the lawn", outside && sawOpen, "pos " + f.transform.position.ToString("0.0") + " min z " + minZ.ToString("0.0") + " door opened " + sawOpen);
                    camPos = new Vector3(-6f, 6.5f, -4f); camLook = new Vector3(-16f, 2f, 10f);
                    Next(5, "out");
                }
                break;
            case 5:   // outside: wing closed, no old portal, other entries
                if (phT > 3f && phT - Time.deltaTime <= 3f)
                {
                    RaycastHit h; bool hitE = Physics.Raycast(new Vector3(-10f, 1.5f, 8f), Vector3.left, out h, 8f, ~0, QueryTriggerInteraction.Ignore);
                    string wn = hitE ? h.collider.gameObject.name + " at x " + h.point.x.ToString("0.0") : "none";
                    Check("mudroom wing outside = solid shell joined to the house + garage (x -19..-14.5, z 4..13)", hitE && Mathf.Abs(h.point.x - HouseUp64.MRx1) < 0.3f, "ray from the lawn hits " + wn);
                    // nothing on the ranch still says "hallway" (the old back-door portal is gone)
                    int old = 0; foreach (var hs in Interact.All) if (hs != null && hs.label != null && hs.label.Contains("hallway")) old++;
                    Check("old back-door portal into the house is gone", old == 0, old + " 'hallway' hotspots");
                }
                if (phT > 6f)
                {
                    // big bay door: walk in from the drive
                    float bx = Layout.BayX(3) + 3.2f;   // beside the tank, clear of the bay pillar
                    PutR(new Vector3(bx, 0f, OZ + Layout.GarageSize.y + 4f), 180f);
                    camOn = false;
                    Next(6, "bigdoor");
                }
                break;
            case 6:
                if (phT > 1f && phT < 5f) Walk(Vector3.back);
                if (phT > 5f)
                {
                    Check("garage BIG DOOR (bays) -> into the garage", RobotNav.InGarage(f.transform.position), "pos " + f.transform.position.ToString("0.0"));
                    // front door in
                    Vector2 c = Layout.HouseC; float z1 = c.y + Layout.HouseSize.y * 0.5f;
                    PutR(new Vector3(c.x, 0f, z1 + 0.7f), 180f);
                    bool used = Use("go inside");
                    Vector3 l = LP;
                    Check("house FRONT DOOR -> the foyer", used && f.world == WorldId.House && Mathf.Abs(l.x) < 6f && l.z > 4f && l.z < 20f, "local " + l.ToString("0.0"));
                    Next(7, "foyer");
                }
                break;
            case 7:
                if (phT > 3f)
                {
                    f.SendTo(WorldId.House, HL(0f, 0.15f, 20.4f), 0f);
                    bool used = Use("back outside");
                    Vector2 c = Layout.HouseC; float z1 = c.y + Layout.HouseSize.y * 0.5f;
                    Check("foyer front door -> out on the porch", used && f.world == WorldId.Ranch && f.transform.position.z > z1 && Mathf.Abs(f.transform.position.x - c.x) < 6f, "pos " + f.transform.position.ToString("0.0"));
                    string r = HouseUp64.GoUpstairs(f);
                    Check("phone 'Me: go upstairs (house)' -> the 2nd floor landing", f.world == WorldId.House && LP.y > HouseUp64.F2 - 0.2f, "'" + r + "' local " + LP.ToString("0.0"));
                    Next(8, "upstairs");
                }
                break;
            case 8:
                if (phT > 4f) Done();
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
