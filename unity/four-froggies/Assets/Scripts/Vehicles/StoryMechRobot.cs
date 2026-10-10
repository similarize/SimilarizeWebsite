using UnityEngine;

// ffu26 (Bill: "robots can get inside the mechs and pilot them, including taking mechs to space"):
//  * a robot pilot sits VISIBLY in a glass cockpit canopy on top of the mech's head (built on first use, shown only
//    while a robot is the driver) - RobotDriver / RobotMechMission put the robot on CanopySeat;
//  * a robot mech mission can fly the real ranch mech on a script (launch climb, landing back at the ranch):
//    `scripted` skips the stick logic in Update, the mission places the mech with ScriptPlace and sets the rocket
//    flame / airborne pose; damage, limb loss and toppling still run first (ffu21), and they eject the robot.
public partial class StoryMech
{
    public bool scripted;                 // a robot mech mission is flying this mech
    public float scriptFlame, scriptAir;  // rocket flame 0..2, 0 = standing pose, 1 = flight pose
    Transform canopy, canopySeat;
    float canopyR;

    public bool RobotAboard { get { return driver != null && driver.robotPilot != null; } }
    public float CanopyRobotH { get { BuildCanopy(); return canopyR * 1.55f; } }
    public Vector3 SpawnPoint { get { return spawnPos; } }

    // the cockpit seat (robot origin goes a little below it so its hips sit on the seat)
    public Transform CanopySeat { get { BuildCanopy(); return canopySeat; } }
    public Vector3 CockpitEye { get { BuildCanopy(); return canopySeat.TransformPoint(new Vector3(0f, canopyR * 0.75f, canopyR * 0.55f)); } }

    void BuildCanopy()
    {
        if (canopy != null || head == null) return;
        canopy = AddCanopy(head, height, out canopySeat, out canopyR);
        Mats.SetLayer(canopy.gameObject, VehicleLayer);
        canopy.gameObject.SetActive(false);
    }

    // glass cockpit bubble on top-front of a mech head (the ranch mech and the robot mission's space / surface copy)
    public static Transform AddCanopy(Transform head, float H, out Transform seat, out float R)
    {
        // head bounds in head space (pack meshes or the fallback cube)
        Vector3 mn = new Vector3(1e9f, 1e9f, 1e9f), mx = -mn;
        bool any = false;
        foreach (var mf in head.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Bounds b = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 l = head.InverseTransformPoint(mf.transform.TransformPoint(c));
                mn = Vector3.Min(mn, l); mx = Vector3.Max(mx, l); any = true;
            }
        }
        float hs = head.lossyScale.x > 1e-4f ? head.lossyScale.x : 1f;
        if (!any) { mn = new Vector3(-0.08f, 0f, -0.07f) * H / hs; mx = new Vector3(0.08f, 0.12f, 0.07f) * H / hs; }
        Vector3 size = mx - mn;
        R = Mathf.Clamp(Mathf.Min(size.x, size.z) * 0.42f, H * 0.035f / hs, H * 0.075f / hs);
        Vector3 top = new Vector3((mn.x + mx.x) * 0.5f, mx.y, (mn.z + mx.z) * 0.5f + size.z * 0.12f);
        Transform canopy = Mats.Node(head, "RobotCanopy", top);
        Material frame = Mats.Steel(new Color(0.2f, 0.21f, 0.24f)), glassM = Mats.GlassTint(new Color(0.55f, 0.85f, 1f, 0.32f));
        Material lamp = Mats.Unlit(new Color(0.5f, 1f, 0.6f));
        Mats.Prim(PrimitiveType.Cylinder, canopy, new Vector3(0f, R * 0.06f, 0f), new Vector3(R * 2.25f, R * 0.08f, R * 2.4f), frame);      // collar ring
        Mats.Prim(PrimitiveType.Cylinder, canopy, new Vector3(0f, R * 0.13f, 0f), new Vector3(R * 2.05f, R * 0.03f, R * 2.2f), Mats.Lit(new Color(0.07f, 0.08f, 0.1f)));   // floor
        var dome = Mats.Prim(PrimitiveType.Sphere, canopy, new Vector3(0f, R * 0.55f, 0f), new Vector3(R * 2.1f, R * 1.9f, R * 2.3f), glassM);
        dome.name = "Glass";
        Mats.Prim(PrimitiveType.Cube, canopy, new Vector3(0f, R * 1.0f, 0f), new Vector3(R * 0.08f, R * 0.08f, R * 2.2f), frame);         // roof bar
        for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, canopy, new Vector3(s * R * 0.75f, R * 0.25f, R * 0.8f), new Vector3(R * 0.18f, R * 0.06f, R * 0.06f), lamp);   // nav lamps
        // seat + console
        Mats.Prim(PrimitiveType.Cube, canopy, new Vector3(0f, R * 0.25f, -R * 0.25f), new Vector3(R * 0.7f, R * 0.18f, R * 0.6f), frame);
        Mats.Prim(PrimitiveType.Cube, canopy, new Vector3(0f, R * 0.6f, -R * 0.6f), new Vector3(R * 0.7f, R * 0.7f, R * 0.12f), frame);
        Mats.Prim(PrimitiveType.Cube, canopy, new Vector3(0f, R * 0.42f, R * 0.62f), new Vector3(R * 0.9f, R * 0.22f, R * 0.25f), Mats.Lit(new Color(0.1f, 0.11f, 0.13f)));
        Mats.Prim(PrimitiveType.Cube, canopy, new Vector3(0f, R * 0.54f, R * 0.6f), new Vector3(R * 0.7f, R * 0.02f, R * 0.18f), Mats.Unlit(new Color(0.45f, 0.85f, 1f)));
        seat = Mats.Node(canopy, "Seat", new Vector3(0f, R * 0.34f, -R * 0.2f));
        foreach (var r in canopy.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        R *= hs;   // world size
        return canopy;
    }

    void CanopyTick()
    {
        bool want = RobotAboard && !wrecked;
        if (!want && canopy == null) return;
        BuildCanopy();
        if (canopy != null && canopy.gameObject.activeSelf != want) canopy.gameObject.SetActive(want);
    }

    // ---------- scripted flight (robot mech missions) ----------
    public void ScriptPlace(Vector3 p, float yawDeg)
    {
        yaw = yawDeg;
        Quaternion q = Quaternion.Euler(0f, yawDeg, 0f);
        transform.position = p; transform.rotation = q;
        if (rb != null) { rb.position = p; rb.rotation = q; }
        float g = Ranch.GY(p.x, p.z);
        Altitude = Mathf.Max(0f, p.y - g);
        speed = 0f; vy = 0f; turnVel = 0f;
        atSpawn = false;
    }

    public void ScriptEnd(bool onGround)
    {
        scripted = false; scriptFlame = 0f; scriptAir = 0f; flameK = 0f;
        if (onGround) { grounded = true; vy = 0f; Altitude = 0f; Vector3 p = transform.position; p.y = Ranch.GY(p.x, p.z); ScriptPlace(p, yaw); }
        for (int i = 0; i < 2; i++) if (flames[i] != null) flames[i].localScale = Vector3.zero;
    }

    void ScriptTick(float dt)
    {
        flameK = Mathf.MoveTowards(flameK, scriptFlame, dt * 6f);
        grounded = scriptAir < 0.5f;
        rocketing = !grounded && scriptFlame > 0.3f;
        Animate(dt, 8f + height * 0.14f);
        if (!grounded)
        {
            // arms up for the climb (a superhero launch), legs together
            armL.localRotation = Quaternion.Slerp(armL.localRotation, Quaternion.Euler(-165f, 0f, -12f), dt * 3f);
            armR.localRotation = Quaternion.Slerp(armR.localRotation, Quaternion.Euler(-160f, 0f, 12f), dt * 3f);
        }
        Jets(dt);
        kinVel = Vector3.zero;
    }
}
