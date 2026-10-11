using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ffu33 James's jet base on the east side of the ranch (JetLayout): a hangar (big sliding doors facing the runway, a roof
// whose middle slides open so the jet can lift straight out), the apron with the VTOL pad in front of the doors, a taxi line
// and a short runway (piano keys, centre line, 18 / 36, edge lights, windsock). The terrain under it is flat (Layout.GroundY
// x JetLayout.Flat). Also runs: door / roof motors, passenger boarding (A / E next to the jet while James flies it; gamepad Y
// boards too), and the jet's HUD readout for its riders.
public class JetBase : MonoBehaviour
{
    public static JetBase I;
    Transform root, hroot, doorA, doorB, roofA, roofB;
    float doorK, roofK;
    Vector3 doorA0, doorB0, roofA0, roofB0;   // local (hangar frame)
    public Jet jet;
    Hotspot rideSpot;
    readonly Dictionary<Frog, float> leftAt = new Dictionary<Frog, float>();
    readonly Dictionary<Slot, JetHud> huds = new Dictionary<Slot, JetHud>();
    public float DoorK { get { return doorK; } }
    public static float FloorY = -99f;   // hangar floor top (world y)
    public float RoofK { get { return roofK; } }

    static float GY(float x, float z) { return Ranch.GY(x, z); }
    static float Top(float x0, float x1, float z0, float z1)
    {
        float y = -99f;
        for (int i = 0; i <= 4; i++) for (int j = 0; j <= 4; j++) y = Mathf.Max(y, GY(Mathf.Lerp(x0, x1, i / 4f), Mathf.Lerp(z0, z1, j / 4f)));
        return y;
    }

    public static void Create()
    {
        if (I != null) return;
        I = new GameObject("James's jet base").AddComponent<JetBase>();
        I.Build();
    }

    GameObject Box(Transform p, Vector3 pos, Vector3 size, Material m, bool col = true, float yaw = 0f)
    {
        return Mats.Prim(PrimitiveType.Cube, p, pos, size, new Vector3(0f, yaw, 0f), m, col);
    }

    void Build()
    {
        root = new GameObject("Jet base static").transform;
        root.SetParent(transform, false);
        Material conc = Mats.Lit(new Color(0.6f, 0.6f, 0.58f));
        Material asph = Mats.Lit(new Color(0.17f, 0.17f, 0.19f));
        Material white = Mats.Lit(new Color(0.94f, 0.94f, 0.92f));
        Material yellow = Mats.Lit(new Color(1f, 0.8f, 0.12f));
        Material wall = Mats.TexTint("LB/roofmetal", new Color(0.72f, 0.75f, 0.78f), 0.35f, 2.5f);
        Material wallIn = Mats.Lit(new Color(0.82f, 0.83f, 0.84f));
        Material roofM = Mats.TexTint("LB/roofmetal", new Color(0.5f, 0.52f, 0.56f), 0.4f, 3f);
        Material frame = Mats.Paint(new Color(0.2f, 0.22f, 0.25f), 0.6f);
        Material green = Mats.Paint(Roster.Color(0), 0.8f);
        Material floorM = Mats.Lit(new Color(0.42f, 0.43f, 0.45f));

        Vector2 hcW = JetLayout.HangarC;
        float W = JetLayout.HangarW, D = JetLayout.HangarD, H = JetLayout.HangarH;
        float fy = Top(hcW.x - W * 0.5f, hcW.x + W * 0.5f, hcW.y - D * 0.5f, hcW.y + D * 0.5f) + 0.12f;   // floor top
        FloorY = fy;
        // the hangar is built in its own frame (doors on local +x) and turned 180 deg so the doors face west (apron / runway)
        hroot = new GameObject("Hangar").transform;
        hroot.SetParent(transform, false);
        hroot.position = new Vector3(hcW.x, 0f, hcW.y);
        hroot.rotation = Quaternion.Euler(0f, 180f, 0f);
        Transform hs = new GameObject("Hangar static").transform; hs.SetParent(hroot, false);
        Vector2 hc = Vector2.zero;
        float x0 = -W * 0.5f, x1 = W * 0.5f, z0 = -D * 0.5f, z1 = D * 0.5f;
        // ---- hangar floor, walls, roof ----
        Box(hs, new Vector3(hc.x, fy - 0.6f, hc.y), new Vector3(W, 1.2f, D), floorM);
        Box(hs, new Vector3(x1 - 0.6f, fy + 0.005f, hc.y), new Vector3(1.0f, 0.02f, D - 2f), yellow, false);    // door line
        for (int k = -5; k <= 5; k++) Box(hs, new Vector3(x1 - 0.6f, fy + 0.012f, hc.y + k * 2.2f), new Vector3(1.0f, 0.02f, 0.9f), Mats.Lit(new Color(0.1f, 0.1f, 0.1f)), false, 35f);
        Box(hs, new Vector3(x0, fy + H * 0.5f, hc.y), new Vector3(0.4f, H, D), wall);                         // back wall
        Box(hs, new Vector3(x0 + 0.25f, fy + H * 0.5f, hc.y), new Vector3(0.1f, H - 0.2f, D - 0.6f), wallIn, false);
        for (int s = -1; s <= 1; s += 2)
        {
            Box(hs, new Vector3(hc.x, fy + H * 0.5f, hc.y + s * D * 0.5f), new Vector3(W, H, 0.4f), wall);   // side walls
            Box(hs, new Vector3(hc.x, fy + H * 0.5f, hc.y + s * (D * 0.5f - 0.25f)), new Vector3(W - 0.6f, H - 0.2f, 0.1f), wallIn, false);
            Box(hs, new Vector3(x1, fy + H * 0.5f, hc.y + s * (D * 0.5f - 0.5f)), new Vector3(0.7f, H, 1.0f), frame);   // door posts
            for (int r = 0; r < 5; r++) Box(hs, new Vector3(x0 + 2f + r * 4.4f, fy + H * 0.5f, hc.y + s * (D * 0.5f + 0.22f)), new Vector3(0.25f, H, 0.12f), frame, false);
        }
        Box(hs, new Vector3(x1, fy + H - 0.4f, hc.y), new Vector3(0.7f, 0.8f, D), frame);                     // header beam over the doors
        // roof: fixed outer strips, two middle panels slide out over them
        float ry = fy + H + 0.15f;
        for (int s = -1; s <= 1; s += 2)
            Box(hs, new Vector3(hc.x, ry, hc.y + s * (D * 0.5f - 3.75f)), new Vector3(W + 0.6f, 0.3f, 7.5f + 0.4f), roofM);
        roofA = Box(hroot, new Vector3(hc.x, ry + 0.3f, hc.y - 2.75f), new Vector3(W + 0.4f, 0.28f, 5.6f), roofM).transform;
        roofB = Box(hroot, new Vector3(hc.x, ry + 0.3f, hc.y + 2.75f), new Vector3(W + 0.4f, 0.28f, 5.6f), roofM).transform;
        roofA0 = roofA.localPosition; roofB0 = roofB.localPosition;
        Box(hs, new Vector3(hc.x, ry + 0.25f, hc.y), new Vector3(W + 0.6f, 0.2f, 0.25f), frame, false);
        // ceiling lights (unlit panels) + big J-1 on the back wall
        Material lamp = new Material(Mats.Unlit(Color.white)) { name = "Hangar lamp" }; lamp.color = new Color(1.4f, 1.38f, 1.3f);
        for (int i = 0; i < 3; i++) for (int s = -1; s <= 1; s += 2) Box(hs, new Vector3(x0 + 4f + i * 7f, fy + H - 0.3f, hc.y + s * 8.5f), new Vector3(3.2f, 0.08f, 0.7f), lamp, false);
        Box(hs, new Vector3(x0 + 0.35f, fy + 5.5f, hc.y), new Vector3(0.06f, 2.6f, 9f), green, false);
        WorldLabel(hroot.TransformPoint(new Vector3(x0 + 0.42f, fy + 5.5f, hc.y)), hroot.rotation * Quaternion.Euler(0f, 270f, 0f), "JAMES  J-1", Color.white, 0.12f);
        // props inside: tool chest, crates, tyre stack
        Material red = Mats.Paint(new Color(0.75f, 0.12f, 0.1f), 0.7f);
        Box(hs, new Vector3(x0 + 1.2f, fy + 0.6f, z0 + 2.5f), new Vector3(1.2f, 1.2f, 2.6f), red);
        Box(hs, new Vector3(x0 + 1.4f, fy + 1.45f, z0 + 2.5f), new Vector3(1.1f, 0.5f, 2.4f), red);
        Material crate = Mats.TexTint("LB/wood", new Color(0.8f, 0.65f, 0.45f), 0.1f, 1.2f);
        Box(hs, new Vector3(x0 + 2f, fy + 0.6f, z1 - 2.2f), new Vector3(1.2f, 1.2f, 1.2f), crate);
        Box(hs, new Vector3(x0 + 3.4f, fy + 0.5f, z1 - 2f), new Vector3(1f, 1f, 1f), crate);
        Box(hs, new Vector3(x0 + 2.2f, fy + 1.6f, z1 - 2.1f), new Vector3(0.9f, 0.9f, 0.9f), crate, true, 20f);
        Material tyre = Mats.Lit(new Color(0.08f, 0.08f, 0.08f));
        for (int i = 0; i < 4; i++) Mats.Prim(PrimitiveType.Cylinder, hs, new Vector3(x0 + 5.5f, fy + 0.15f + i * 0.3f, z1 - 1.6f), new Vector3(1f, 0.14f, 1f), tyre, i == 0);
        // ---- sliding doors (two panels slide sideways past the side walls) ----
        doorA = Box(hroot, new Vector3(x1 + 0.55f, fy + (H - 0.8f) * 0.5f, hc.y - D * 0.25f + 0.25f), new Vector3(0.3f, H - 0.8f, D * 0.5f - 0.5f), wall).transform;
        doorB = Box(hroot, new Vector3(x1 + 0.55f, fy + (H - 0.8f) * 0.5f, hc.y + D * 0.25f - 0.25f), new Vector3(0.3f, H - 0.8f, D * 0.5f - 0.5f), wall).transform;
        doorA0 = doorA.localPosition; doorB0 = doorB.localPosition;
        foreach (Transform m in new[] { doorA, doorB, roofA, roofB }) { var kb = m.gameObject.AddComponent<Rigidbody>(); kb.isKinematic = true; kb.useGravity = false; }
        Box(doorA, new Vector3(1.2f, 0f, 0f), new Vector3(0.2f, 0.06f, 0.9f), green, false);
        Box(doorB, new Vector3(1.2f, 0f, 0f), new Vector3(0.2f, 0.06f, 0.9f), green, false);
        float doorX = JetLayout.DoorX;
        try { Ranch.Sign(new Vector3(doorX - 0.9f, fy + H + 1.6f, hcW.y), 270f, "JAMES'S HANGAR", new Color(0.12f, 0.36f, 0.14f), 8f, 1.6f); } catch (System.Exception e) { Debug.Log("JetBase sign: " + e.Message); }
        MeshMerge.Merge(hs, true);

        // ---- apron + VTOL pad + taxi line (world frame) ----
        float ax0 = JetLayout.ApronX0, ax1 = doorX, az0 = hcW.y - 13f, az1 = hcW.y + 13f;
        float ay = Top(ax0, ax1, az0, az1) + 0.06f;
        Box(root, new Vector3((ax0 + ax1) * 0.5f, ay - 0.4f, hcW.y), new Vector3(ax1 - ax0, 0.8f, az1 - az0), conc);
        Vector2 pc = JetLayout.PadC;
        Mats.Prim(PrimitiveType.Cylinder, root, new Vector3(pc.x, ay + 0.005f, pc.y), new Vector3(JetLayout.PadR * 2f, 0.01f, JetLayout.PadR * 2f), Mats.Lit(new Color(0.3f, 0.32f, 0.34f)), false);
        Mats.Prim(PrimitiveType.Cylinder, root, new Vector3(pc.x, ay + 0.01f, pc.y), new Vector3(JetLayout.PadR * 2f - 0.3f, 0.01f, JetLayout.PadR * 2f - 0.3f), yellow, false);
        Mats.Prim(PrimitiveType.Cylinder, root, new Vector3(pc.x, ay + 0.015f, pc.y), new Vector3(JetLayout.PadR * 2f - 1.3f, 0.01f, JetLayout.PadR * 2f - 1.3f), Mats.Lit(new Color(0.3f, 0.32f, 0.34f)), false);
        Box(root, new Vector3(pc.x, ay + 0.02f, pc.y - 1.6f), new Vector3(5f, 0.02f, 0.8f), white, false);
        Box(root, new Vector3(pc.x, ay + 0.02f, pc.y + 1.6f), new Vector3(5f, 0.02f, 0.8f), white, false);
        Box(root, new Vector3(pc.x, ay + 0.02f, pc.y), new Vector3(0.8f, 0.02f, 2.4f), white, false);
        WorldLabel(new Vector3(pc.x + 4.4f, ay + 0.03f, pc.y), Quaternion.Euler(90f, 270f, 0f), "VTOL", new Color(1f, 0.82f, 0.15f), 0.09f);
        // taxi line: hangar doors -> pad -> runway centre line
        Box(root, new Vector3((pc.x + ax1) * 0.5f, ay + 0.012f, pc.y), new Vector3(ax1 - pc.x, 0.02f, 0.35f), yellow, false);
        Box(root, new Vector3(ax0 + 3f, ay + 0.012f, (pc.y + JetLayout.RunwayZ) * 0.5f), new Vector3(0.35f, 0.02f, Mathf.Abs(pc.y - JetLayout.RunwayZ)), yellow, false);
        Material lg = new Material(Mats.Unlit(Color.white)) { name = "Pad light" }; lg.color = new Color(0.4f, 1.6f, 0.5f);
        for (int i = 0; i < 12; i++) { float a = i / 12f * Mathf.PI * 2f; Mats.Prim(PrimitiveType.Sphere, root, new Vector3(pc.x + Mathf.Cos(a) * (JetLayout.PadR + 0.4f), ay + 0.1f, pc.y + Mathf.Sin(a) * (JetLayout.PadR + 0.4f)), Vector3.one * 0.22f, lg, false); }

        // ---- runway (along x) ----
        float rz = JetLayout.RunwayZ, rw = JetLayout.RunwayW, r0 = JetLayout.RunwayX0, r1 = JetLayout.RunwayX1;
        float rY = Top(r0, r1, rz - rw * 0.5f, rz + rw * 0.5f) + 0.05f;
        Box(root, new Vector3((r0 + r1) * 0.5f, rY - 0.4f, rz), new Vector3(r1 - r0, 0.8f, rw), asph);
        for (float x = r0 + 14f; x < r1 - 14f; x += 9f) Box(root, new Vector3(x, rY + 0.008f, rz), new Vector3(4.5f, 0.02f, 0.45f), white, false);
        for (int e = 0; e < 2; e++)
        {
            float ex = e == 0 ? r0 + 2.5f : r1 - 2.5f;
            for (int k = -3; k <= 3; k++) if (k != 0) Box(root, new Vector3(ex, rY + 0.008f, rz + k * 1.7f), new Vector3(4f, 0.02f, 0.9f), white, false);
            WorldLabel(new Vector3(e == 0 ? r0 + 9f : r1 - 9f, rY + 0.03f, rz), Quaternion.Euler(90f, e == 0 ? 90f : 270f, 0f), e == 0 ? "09" : "27", Color.white, 0.16f);
        }
        Box(root, new Vector3((r0 + r1) * 0.5f, rY + 0.008f, rz - rw * 0.5f + 0.3f), new Vector3(r1 - r0 - 2f, 0.02f, 0.25f), white, false);
        Box(root, new Vector3((r0 + r1) * 0.5f, rY + 0.008f, rz + rw * 0.5f - 0.3f), new Vector3(r1 - r0 - 2f, 0.02f, 0.25f), white, false);
        Material el = new Material(Mats.Unlit(Color.white)) { name = "Runway light" }; el.color = new Color(1.5f, 1.45f, 1.2f);
        Material elg = new Material(Mats.Unlit(Color.white)) { name = "Runway end light" }; elg.color = new Color(0.3f, 1.6f, 0.4f);
        for (float x = r0; x <= r1 + 0.1f; x += 8f)
            for (int s = -1; s <= 1; s += 2)
                Mats.Prim(PrimitiveType.Sphere, root, new Vector3(x, rY + 0.12f, rz + s * (rw * 0.5f + 0.5f)), Vector3.one * 0.25f, (x - r0 < 1f || r1 - x < 8f) ? elg : el, false);
        // windsock by the west end
        float wx = r0 + 10f, wz = rz + rw * 0.5f + 6f, wy = GY(wx, wz);
        Mats.Prim(PrimitiveType.Cylinder, root, new Vector3(wx, wy + 3f, wz), new Vector3(0.12f, 3f, 0.12f), white, true);
        var sock = new GameObject("Windsock").transform; sock.SetParent(root, false); sock.position = new Vector3(wx, wy + 5.7f, wz); sock.rotation = Quaternion.Euler(8f, 60f, 0f);
        Material orange = Mats.Lit(new Color(1f, 0.45f, 0.1f));
        for (int k = 0; k < 4; k++) Mats.Prim(PrimitiveType.Cylinder, sock, new Vector3(0f, 0f, 0.45f + k * 0.55f), new Vector3(0.55f - k * 0.08f, 0.27f, 0.55f - k * 0.08f), new Vector3(90f, 0f, 0f), k % 2 == 0 ? orange : white, false);

        MeshMerge.Merge(root, true);

        // ---- the jet, parked inside facing the doors ----
        Vector3 home = new Vector3(hcW.x - 1.5f, fy, hcW.y);
        jet = Jet.Spawn(home, 270f);
        // passengers: A / E next to the jet while James is flying it (it has to be on the ground or hovering low)
        rideSpot = Interact.Add(home, 7f, "ride along in James's jet", f => Board(f));
        rideSpot.enabled = f => jet != null && jet.driver != null && jet.driver != f && f.vehicle == null && f.passengerOf == null && jet.agl < 3f && jet.Passengers().Count < 1 && (!leftAt.ContainsKey(f) || Time.time - leftAt[f] > 1.2f);
        Debug.Log("JetBase: hangar floor y " + fy.ToString("0.00") + " runway y " + rY.ToString("0.00") + " jet at " + home.ToString("0.0"));
    }

    void Board(Frog f)
    {
        if (jet == null || jet.driver == null) return;
        f.BoardAsPassenger(jet);
        f.Toast("Riding along with " + jet.driver.nick + "! (A / E to hop out when it's landed)", 3f);
        Sfx.Play(Sfx.Door, 0.6f);
    }

    static TextMesh WorldLabel(Vector3 p, Quaternion q, string text, Color c, float size)
    {
        var g = new GameObject("Label " + text);
        g.transform.position = p; g.transform.rotation = q;
        var tm = g.AddComponent<TextMesh>();
        tm.text = text; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = c;
        tm.font = UIK.WorldFont; tm.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
        UIK.WorldText(tm, 64, size);
        return tm;
    }

    void Update()
    {
        if (Game.I == null || jet == null) return;
        float dt = Time.deltaTime;
        Vector2 hc = JetLayout.HangarC;
        Vector3 doorC = new Vector3(JetLayout.DoorX, 0f, hc.y);
        Vector3 jp = jet.transform.position;
        bool near = (new Vector2(jp.x - doorC.x, jp.z - doorC.z)).magnitude < 34f && jp.y < 30f;
        foreach (Frog f in Game.I.frogs) if (f != null && f.world == WorldId.Ranch && (new Vector2(f.transform.position.x - doorC.x, f.transform.position.z - doorC.z)).magnitude < 22f) near = true;
        doorK = Mathf.MoveTowards(doorK, near ? 1f : 0f, dt * 0.35f);
        float e = doorK * doorK * (3f - 2f * doorK);
        float slide = JetLayout.HangarD * 0.5f - 0.6f;
        // (hangar frame: doors on local +x, panels slide along local z)
        doorA.localPosition = doorA0 + new Vector3(0.4f * Mathf.Clamp01(e * 6f), 0f, -slide * e);
        doorB.localPosition = doorB0 + new Vector3(0.4f * Mathf.Clamp01(e * 6f), 0f, slide * e);
        bool inside = Mathf.Abs(jp.x - hc.x) < JetLayout.HangarW * 0.5f && Mathf.Abs(jp.z - hc.y) < JetLayout.HangarD * 0.5f;
        bool roofOpen = inside && jet.driver != null && (jet.vtol || jet.nz > 0.5f);
        if (!inside && jet.driver != null && (new Vector2(jp.x - hc.x, jp.z - hc.y)).magnitude < 30f && jp.y > JetLayout.HangarH + 2f && (jet.vtol || jet.nz > 0.5f)) roofOpen = true;   // coming down to land inside
        roofK = Mathf.MoveTowards(roofK, roofOpen ? 1f : 0f, dt * 0.45f);
        float re = roofK * roofK * (3f - 2f * roofK);
        roofA.localPosition = roofA0 + new Vector3(0f, 0f, -5.4f * re);
        roofB.localPosition = roofB0 + new Vector3(0f, 0f, 5.4f * re);
        // passenger hotspot follows the jet; gamepad Y next to it boards (pilot or passenger)
        rideSpot.pos = jp;
        foreach (Slot s in Game.I.slots)
        {
            if (s == null || s.frog < 0 || s.frog >= Game.I.frogs.Count) continue;
            Frog f = Game.I.frogs[s.frog];
            if (f == null) continue;
            if (f.passengerOf == jet)
            {
                if (s.last.use && jet.agl < 2.6f && jet.vel.magnitude < 7f)
                {
                    f.LeavePassenger(); f.Teleport(jet.ExitPoint()); leftAt[f] = Time.time;
                    f.Toast("Hopped out of James's jet", 2f);
                }
                continue;
            }
            if (s.kind == InputKind.Gamepad && s.pad != null && s.pad.buttonNorth.wasPressedThisFrame && f.vehicle == null && f.passengerOf == null && f.remote == null && f.world == WorldId.Ranch)
            {
                Vector3 cp = jet.body != null ? jet.body.ClosestPoint(f.transform.position + Vector3.up * 0.6f) : jp;
                if ((cp - (f.transform.position + Vector3.up * 0.6f)).magnitude < 2.8f)
                {
                    if (jet.driver == null && jet.CanEnter(f)) f.EnterVehicle(jet);
                    else if (rideSpot.enabled(f)) Board(f);
                }
            }
        }
        // the jet HUD for every rider's view
        foreach (Slot s in Game.I.slots)
        {
            if (s == null || s.hud == null) continue;
            JetHud h;
            if (!huds.TryGetValue(s, out h)) { h = new JetHud(s.hud.panel); huds[s] = h; }
            Frog f = s.frog >= 0 && s.frog < Game.I.frogs.Count ? Game.I.frogs[s.frog] : null;
            bool riding = f != null && (f.vehicle == jet || f.passengerOf == jet) && Game.I.state == Game.State.Play;
            h.Tick(riding ? jet : null, dt);
        }
    }

    // top-centre readout: mode, speed (km/h + Mach), altitude, throttle; HYPER adds the earth-lap progress + home distance
    class JetHud
    {
        readonly RectTransform root; readonly Text line1, line2; readonly Image bar, barBg;
        public JetHud(RectTransform panel)
        {
            root = UIK.Rect(panel, "Jet HUD", new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(640f, 86f));
            UIK.Img(root, UIK.SoftRect, new Color(0f, 0.05f, 0.02f, 0.4f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 80f));
            line1 = UIK.Label(root, "", 26, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(620f, 36f), new Color(0.6f, 1f, 0.6f));
            line2 = UIK.Label(root, "", 18, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(620f, 28f), Color.white);
            line1.supportRichText = line2.supportRichText = true;
            // the HYPER line (speed, Mach, alt, earth lap, km, laps) wrapped at 1280 px in ffu33a: shrink to fit on one line
            line2.resizeTextForBestFit = true; line2.resizeTextMinSize = 11; line2.resizeTextMaxSize = 18; line2.horizontalOverflow = HorizontalWrapMode.Wrap; line2.verticalOverflow = VerticalWrapMode.Truncate;
            barBg = UIK.Img(root, null, new Color(1f, 1f, 1f, 0.18f), new Vector2(0.5f, 0.5f), new Vector2(0f, -32f), new Vector2(300f, 6f));
            bar = UIK.Img(root, null, new Color(0.5f, 1f, 0.5f, 0.9f), new Vector2(0.5f, 0.5f), new Vector2(-150f, -32f), new Vector2(0f, 6f));
            bar.rectTransform.pivot = new Vector2(0f, 0.5f);
            root.gameObject.SetActive(false);
        }
        public void Tick(Jet j, float dt)
        {
            bool on = j != null;
            if (root.gameObject.activeSelf != on) root.gameObject.SetActive(on);
            if (!on) return;
            float kmh = j.vel.magnitude * 3.6f;
            string mode;
            Color mc;
            if (j.mode == Jet.Mode.Hyper) { mode = "HYPER  -  AROUND THE EARTH"; mc = new Color(1f, 0.75f, 0.3f); }
            else if (j.nz > 0.05f && j.nz < 0.95f) { mode = "TRANSITION  -  NOZZLES " + (j.vtol ? "DOWN" : "AFT") + " " + Mathf.RoundToInt((1f - j.nz) * 100f) + "%"; mc = new Color(0.9f, 0.9f, 0.5f); }
            else if (j.vtol) { mode = j.onGround ? "VTOL  -  READY (lift off: up)" : "VTOL HOVER"; mc = new Color(0.55f, 0.85f, 1f); }
            else { mode = j.onGround ? "JET  -  ON THE GROUND" : (j.afterburner ? "JET  -  AFTERBURNER" : "JET"); mc = new Color(0.6f, 1f, 0.6f); }
            line1.text = mode; line1.color = mc;
            string l2 = Mathf.RoundToInt(kmh) + " km/h";
            if (j.vel.magnitude > 120f) l2 += "   MACH " + j.Mach.ToString("0.00");
            l2 += "   ALT " + Mathf.RoundToInt(Mathf.Max(0f, j.transform.position.y)) + " m";
            if (j.agl < 300f && j.agl > 0.5f) l2 += " (" + Mathf.RoundToInt(j.agl) + " up)";
            if (j.mode == Jet.Mode.Hyper)
                l2 += "   |   EARTH LAP " + Mathf.RoundToInt(j.LapFrac * 100f) + "%  (" + Mathf.RoundToInt(j.LapFrac * 40075f).ToString("N0") + " km)" + (j.laps > 0 ? "  x" + j.laps : "");
            else
            {
                float d = new Vector2(j.transform.position.x, j.transform.position.z).magnitude;
                if (d > 600f) l2 += "   |   RANCH " + (d / 1000f).ToString("0.0") + " km " + Bearing(j);
            }
            line2.text = l2;
            float thr = j.vtol || j.nz > 0.5f ? 0f : j.throttle;
            bar.rectTransform.sizeDelta = new Vector2(300f * thr, 6f);
            bar.color = j.afterburner ? new Color(1f, 0.6f, 0.2f, 0.95f) : new Color(0.5f, 1f, 0.5f, 0.9f);
            barBg.enabled = !(j.vtol || j.nz > 0.5f);
        }
        static string Bearing(Jet j)
        {
            Vector3 to = -j.transform.position; to.y = 0f;
            Vector3 f = j.transform.forward; f.y = 0f;
            float a = Vector3.SignedAngle(f, to, Vector3.up);
            return Mathf.Abs(a) < 25f ? "AHEAD" : Mathf.Abs(a) > 150f ? "BEHIND" : a > 0f ? "TO THE RIGHT" : "TO THE LEFT";
        }
    }
}
