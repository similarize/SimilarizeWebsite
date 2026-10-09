using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Stage E: things that live on the ranch.
//  * James's robots (three.js "bible", ff3d112 robot table): Optimus (Bubbles's), Unitree, Figure 03, Figure 02,
//    Big Figure Two, Atlas HD, Atlas electric - parked by the garage with the same heights / speeds.
//  * James's robot phone: pick a robot, order it. The 3D phone's orders are come / go / stop / wave / roof helipad /
//    drone pad, plus a live POV cam of the linked robot; Follow and Dance are extra orders added for Bill.
//  * Ranch animals (unnamed): cows + horses in a fenced pasture, goats and chickens, and the 3D yard pens
//    (Yard dog run, Backyard animal yard, Lizard terrace) - they wander, graze and bolt from frogs and vehicles.
//  * The giant story mechs: every froggy's 10 + 100-story, James's + Bubbles's 1000-story, James's trillion-story.
public class RanchLife : MonoBehaviour
{
    public static RanchLife I;
    public readonly List<Robot> robots = new List<Robot>();
    public static readonly Rect Pasture = new Rect(-170f, 86f, 56f, 60f);

    // keep trees out of the pasture and the mech parking spots
    public static bool Reserved(float x, float z)
    {
        if (new Rect(Pasture.xMin - 4f, Pasture.yMin - 4f, Pasture.width + 8f, Pasture.height + 8f).Contains(new Vector2(x, z))) return true;
        if (x < -125f && z > -50f && z < 20f) return true;          // 10 / 100-story rows
        if (z < -145f && x < -30f) return true;                      // 1000-story row
        if (z > 150f && x < -20f) return true;                       // James's trillion-story mech
        return false;
    }

    public static void Create()
    {
        var go = new GameObject("RanchLife");
        I = go.AddComponent<RanchLife>();
        I.Build();
        go.AddComponent<RobotPhone>();
    }

    static Animal Ground(Animal a, Rect r)
    {
        a.groundFn = Ranch.GY;
        a.Init(r, 0f);
        return a;
    }

    void Build()
    {
        // ---- robots (bible table) ----
        Add("optimus", "Optimus", "Bubbles", 0xE1E1E1, 2.35f, 0.7f, 7.5f, -30f, 16f);
        Add("unitree", "Unitree", "", 0x2C2F38, 1.72f, 0.58f, 12f, -28f, 20f);
        Add("figure03", "Figure 03", "", 0xB3B3B3, 2.2f, 0.65f, 9f, -32f, 18f);
        Add("figure02", "Figure 02", "", 0x999999, 2.05f, 0.6f, 8.5f, -34f, 14f);
        Add("figure02big", "Big Figure Two", "", 0x888888, 3.4f, 1.35f, 6.5f, -36f, 22f);
        Add("atlas_hd", "Atlas HD", "", 0xD2D2D2, 2.45f, 0.72f, 10f, -22f, 16f);
        Add("atlas_el", "Atlas electric", "", 0xBCBCBC, 2.4f, 0.7f, 9.5f, -20f, 14f);

        // ---- pasture: fence + cows + horses ----
        var fence = new GameObject("Pasture fence").transform;
        Color wood = new Color(0.55f, 0.4f, 0.25f);
        Rect P = Pasture;
        for (float x = P.xMin; x <= P.xMax + 0.1f; x += 4f) { Post(fence, x, P.yMin, wood); Post(fence, x, P.yMax, wood); }
        for (float z = P.yMin + 4f; z < P.yMax; z += 4f) { Post(fence, P.xMin, z, wood); if (Mathf.Abs(z - P.center.y) > 5f) Post(fence, P.xMax, z, wood); }   // gate on the east side
        Rail(fence, new Vector2(P.xMin, P.yMin), new Vector2(P.xMax, P.yMin), wood);
        Rail(fence, new Vector2(P.xMin, P.yMax), new Vector2(P.xMax, P.yMax), wood);
        Rail(fence, new Vector2(P.xMin, P.yMin), new Vector2(P.xMin, P.yMax), wood);
        Rail(fence, new Vector2(P.xMax, P.yMin), new Vector2(P.xMax, P.center.y - 5f), wood);
        Rail(fence, new Vector2(P.xMax, P.center.y + 5f), new Vector2(P.xMax, P.yMax), wood);
        // a barn + trough (graphics overhaul: Poly Haven CC0 plank siding painted barn red, grey metal gable roof, white trim)
        Vector3 barn = new Vector3(P.xMin + 10f, 0f, P.yMax - 10f);
        float by = Ranch.GY(barn.x, barn.z);
        Material siding = Mats.TexTint("LB/barnred", Color.white, 0.08f), roof = Mats.TexTint("LB/roofmetal", Color.white, 0.35f), trimM = Mats.Lit(new Color(0.95f, 0.94f, 0.9f));
        Mats.Prim(PrimitiveType.Cube, fence, new Vector3(barn.x, by + 3f, barn.z), new Vector3(12f, 6f, 9f), siding, true);
        for (int sd = -1; sd <= 1; sd += 2)   // gable roof: two slabs meeting over the ridge
            Mats.Prim(PrimitiveType.Cube, fence, new Vector3(barn.x + sd * 3.2f, by + 7.35f, barn.z), new Vector3(7.4f, 0.25f, 9.8f), new Vector3(0f, 0f, -sd * 32f), roof, false);
        for (int ez = -1; ez <= 1; ez += 2)   // gable ends (triangle-ish: stacked boxes)
            for (int k = 0; k < 4; k++)
                Mats.Prim(PrimitiveType.Cube, fence, new Vector3(barn.x, by + 6.35f + k * 0.55f, barn.z + ez * 4.48f), new Vector3(9.2f - k * 1.8f, 0.56f, 0.06f), siding, false);
        Mats.Prim(PrimitiveType.Cube, fence, new Vector3(barn.x, by + 2.2f, barn.z + 4.55f), new Vector3(4f, 4.4f, 0.1f), siding, false);
        foreach (float a in new[] { 45f, -45f })   // white X braces on the big door
            Mats.Prim(PrimitiveType.Cube, fence, new Vector3(barn.x, by + 2.2f, barn.z + 4.62f), new Vector3(5.6f, 0.22f, 0.06f), new Vector3(0f, 0f, a), trimM, false);
        foreach (Vector3 tp in new[] { new Vector3(0f, 4.45f, 0f), new Vector3(0f, 0.05f, 0f), new Vector3(-2.05f, 2.2f, 0f), new Vector3(2.05f, 2.2f, 0f) })
            Mats.Prim(PrimitiveType.Cube, fence, new Vector3(barn.x, by, barn.z + 4.62f) + tp, tp.x == 0f ? new Vector3(4.3f, 0.2f, 0.07f) : new Vector3(0.2f, 4.5f, 0.07f), trimM, false);
        for (int cx = -1; cx <= 1; cx += 2)   // corner trim
            for (int cz = -1; cz <= 1; cz += 2)
                Mats.Prim(PrimitiveType.Cube, fence, new Vector3(barn.x + cx * 6f, by + 3f, barn.z + cz * 4.5f), new Vector3(0.25f, 6.02f, 0.25f), trimM, false);
        Mats.Prim(PrimitiveType.Cube, fence, new Vector3(barn.x, by + 4.9f, barn.z + 4.58f), new Vector3(1.6f, 1.2f, 0.08f), Mats.Lit(new Color(0.15f, 0.15f, 0.17f)), false);   // hay loft window
        Mats.Prim(PrimitiveType.Cube, fence, new Vector3(P.center.x, Ranch.GY(P.center.x, P.center.y) + 0.4f, P.center.y), new Vector3(4f, 0.8f, 1.2f), Mats.Lit(new Color(0.45f, 0.45f, 0.5f)), true);
        Ranch.Sign(new Vector3(P.xMax + 1f, Ranch.GY(P.xMax, P.center.y) + 3f, P.center.y + 6f), 90f, "PASTURE", new Color(0.3f, 0.45f, 0.15f), 3.6f, 1f);
        MeshMerge.Merge(fence, true);
        Rect inner = new Rect(P.xMin + 1.5f, P.yMin + 1.5f, P.width - 3f, P.height - 3f);
        for (int i = 0; i < 6; i++) Ground(Animal.Cow(new Vector3(P.xMin + 12f + i * 7f, 0f, P.yMin + 20f + (i % 3) * 9f), i % 2 == 0), inner);
        Color[] hc = { new Color(0.45f, 0.28f, 0.15f), new Color(0.95f, 0.93f, 0.9f), new Color(0.15f, 0.12f, 0.1f) };
        for (int i = 0; i < 3; i++) Ground(Animal.Horse(new Vector3(P.xMin + 20f + i * 10f, 0f, P.yMax - 22f), hc[i], i == 1 ? new Color(0.8f, 0.8f, 0.78f) : new Color(0.1f, 0.08f, 0.06f)), inner);
        for (int i = 0; i < 3; i++) Ground(Animal.Goat(new Vector3(P.xMax - 10f, 0f, P.yMin + 10f + i * 4f)), inner);
        // chickens by a coop near the barn
        Rect coop = new Rect(P.xMin + 2f, P.yMax - 24f, 14f, 10f);
        for (int i = 0; i < 7; i++) Ground(Animal.Chicken(new Vector3(coop.center.x + i * 0.6f, 0f, coop.center.y)), coop);

        // ---- yard pens from the 3D ranch (unnamed animals) ----
        Pen(new Rect(-62f, 18f, 9f, 7f), "Yard dog run", a => Animal.Dog("", a, true), 2, k => Animal.Dog("", k, false), 1);
        Pen(new Rect(-74f, -36f, 10f, 8f), "Backyard animal yard", a => Animal.Rabbit(a, new Color(0.95f, 0.95f, 0.95f)), 3, k => Animal.Cat("", k, new Color(0.85f, 0.6f, 0.35f), new Color(0.6f, 0.35f, 0.15f), false), 2);
        Pen(new Rect(-74f, -14f, 7f, 6f), "Lizard terrace", a => Animal.Lizard(a, new Color(0.45f, 0.6f, 0.25f)), 3, null, 0);

        // ---- the story mechs ----
        // mech yard west of the house: every froggy's 10-story and 100-story rows; James's + Bubbles's 1000-story along
        // the south; James's trillion-story alone to the north-west (Ben's lineup, 11 mechs; owner-only piloting)
        for (int f = 0; f < 4; f++)
        {
            float x10 = -160f + f * 9f, z10 = 6f;
            StoryMech.Build(f, 0, new Vector3(x10, Ranch.GY(x10, z10), z10), 90f);
            float x100 = -165f + f * 16f, z100 = -28f;
            StoryMech.Build(f, 1, new Vector3(x100, Ranch.GY(x100, z100), z100), 90f);
        }
        int[] owners1k = { 0, 2 };   // James, Bubbles (Froggies.Names order: James, Jimmy, Bubbles, Rexy)
        for (int i = 0; i < owners1k.Length; i++)
        {
            float x1k = -140f + i * 50f, z1k = -160f;
            StoryMech.Build(owners1k[i], 2, new Vector3(x1k, Ranch.GY(x1k, z1k), z1k), 0f);
        }
        float xt = -108f, zt = 176f;
        StoryMech.Build(0, 3, new Vector3(xt, Ranch.GY(xt, zt), zt), 180f);   // James only
        Ranch.Sign(new Vector3(-128f, Ranch.GY(-128f, 14f) + 3f, 14f), 90f, "MECH YARD\n<size=22>every froggy: 10 + 100-story mech\nJames & Bubbles: 1000-story\nJames: trillion-story</size>", new Color(0.25f, 0.25f, 0.45f), 8f, 3.2f);
    }

    static void Post(Transform p, float x, float z, Color c)
    {
        Mats.Prim(PrimitiveType.Cube, p, new Vector3(x, Ranch.GY(x, z) + 0.7f, z), new Vector3(0.25f, 1.4f, 0.25f), Mats.TexTint("LB/wood", Color.Lerp(c, Color.white, 0.55f), 0.08f), true);
    }

    static void Rail(Transform p, Vector2 a, Vector2 b, Color c)
    {
        int n = Mathf.CeilToInt((b - a).magnitude / 4f);
        for (int i = 0; i < n; i++)
        {
            Vector2 s = Vector2.Lerp(a, b, i / (float)n), e = Vector2.Lerp(a, b, (i + 1) / (float)n), m = (s + e) * 0.5f;
            float y = Ranch.GY(m.x, m.y);
            foreach (float h in new[] { 0.6f, 1.15f })
            {
                var g = Mats.Prim(PrimitiveType.Cube, p, new Vector3(m.x, y + h, m.y), new Vector3(0.1f, 0.12f, (e - s).magnitude + 0.1f), Mats.TexTint("LB/wood", Color.Lerp(c, Color.white, 0.55f), 0.08f), true);
                g.transform.rotation = Quaternion.LookRotation(new Vector3(e.x - s.x, 0f, e.y - s.y));
            }
        }
    }

    void Pen(Rect r, string label, System.Func<Vector3, Animal> make, int n, System.Func<Vector3, Animal> make2, int n2)
    {
        var p = new GameObject("Pen " + label).transform;
        Color c = new Color(0.92f, 0.92f, 0.9f);
        Rail(p, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), c);
        Rail(p, new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), c);
        Rail(p, new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), c);
        Rail(p, new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), c);
        Ranch.Sign(new Vector3(r.center.x, Ranch.GY(r.center.x, r.yMax) + 2.2f, r.yMax + 0.2f), 0f, label, new Color(0.25f, 0.35f, 0.2f), 3.4f, 0.7f);
        MeshMerge.Merge(p, true);
        Rect inner = new Rect(r.xMin + 0.6f, r.yMin + 0.6f, r.width - 1.2f, r.height - 1.2f);
        for (int i = 0; i < n; i++) Ground(make(new Vector3(inner.center.x + i, 0f, inner.center.y)), inner);
        for (int i = 0; i < n2; i++) Ground(make2(new Vector3(inner.center.x - i, 0f, inner.center.y + 1f)), inner);
    }

    void Add(string id, string name, string owner, int hex, float h, float bulk, float maxSp, float x, float z)
    {
        Color c = new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);
        robots.Add(Robot.Build(id, name, owner, c, h, bulk, maxSp, new Vector3(x, Ranch.GY(x, z), z)));
    }
}

// One of James's robots: kinematic humanoid that walks on the terrain and follows phone orders.
public class Robot : MonoBehaviour
{
    public string id, robotName, owner;
    public float height, maxSpeed;
    public string cmd = "idle", status = "standby";
    public Transform follow;
    Vector3 target;
    float yaw, phase, speed, wave, alt, danceT;
    bool flying;
    Transform legL, legR, armL, armR, body, head, jets;
    public Transform eye;    // phone POV

    public static Robot Build(string id, string name, string owner, Color c, float h, float bulk, float maxSp, Vector3 pos)
    {
        var go = new GameObject("Robot " + name);
        go.transform.position = pos;
        var r = go.AddComponent<Robot>();
        r.id = id; r.robotName = name; r.owner = owner; r.height = h; r.maxSpeed = maxSp;
        r.yaw = 180f; r.target = pos;
        Material m = Mats.Shiny(c), dark = Mats.Lit(new Color(0.1f, 0.1f, 0.12f)), visor = Mats.Unlit(id.StartsWith("atlas") ? new Color(1f, 0.85f, 0.4f) : new Color(0.4f, 0.85f, 1f));
        float w = bulk * 0.6f;
        Transform t = go.transform;
        r.body = Mats.Node(t, "Body", Vector3.zero);
        for (int s = -1; s <= 1; s += 2)
        {
            Transform leg = Mats.Node(r.body, "Leg", new Vector3(w * 0.32f * s, h * 0.47f, 0f));
            Mats.Prim(PrimitiveType.Cube, leg, new Vector3(0f, -h * 0.235f, 0f), new Vector3(w * 0.28f, h * 0.47f, w * 0.3f), m);
            Mats.Prim(PrimitiveType.Cube, leg, new Vector3(0f, -h * 0.46f, w * 0.08f), new Vector3(w * 0.3f, h * 0.03f, w * 0.5f), dark);
            if (s < 0) r.legL = leg; else r.legR = leg;
            Transform arm = Mats.Node(r.body, "Arm", new Vector3(w * 0.62f * s, h * 0.8f, 0f));
            Mats.Prim(PrimitiveType.Cube, arm, new Vector3(0f, -h * 0.17f, 0f), new Vector3(w * 0.2f, h * 0.34f, w * 0.22f), m);
            Mats.Prim(PrimitiveType.Sphere, arm, new Vector3(0f, -h * 0.36f, 0f), Vector3.one * w * 0.24f, dark);
            if (s < 0) r.armL = arm; else r.armR = arm;
        }
        Mats.Prim(PrimitiveType.Cube, r.body, new Vector3(0f, h * 0.66f, 0f), new Vector3(w, h * 0.34f, w * 0.55f), m);
        Mats.Prim(PrimitiveType.Cube, r.body, new Vector3(0f, h * 0.5f, 0f), new Vector3(w * 0.7f, h * 0.06f, w * 0.45f), dark);
        r.head = Mats.Node(r.body, "Head", new Vector3(0f, h * 0.9f, 0f));
        Mats.Prim(PrimitiveType.Sphere, r.head, Vector3.zero, new Vector3(w * 0.5f, h * 0.13f, w * 0.48f), dark);
        Mats.Prim(PrimitiveType.Cube, r.head, new Vector3(0f, 0f, w * 0.22f), new Vector3(w * 0.4f, h * 0.04f, 0.02f), visor);
        r.eye = Mats.Node(r.head, "Eye", new Vector3(0f, 0.05f, w * 0.3f));
        r.jets = Mats.Node(r.body, "Jets", new Vector3(0f, h * 0.55f, -w * 0.32f));
        var jm = new Material(Mats.Fx); jm.color = new Color(0.4f, 0.8f, 1f, 0.7f);
        for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Sphere, r.jets, new Vector3(w * 0.2f * s, -h * 0.25f, 0f), new Vector3(w * 0.15f, h * 0.25f, w * 0.15f), jm);
        r.jets.gameObject.SetActive(false);
        Mats.NoShadows(go);
        // name tag
        var tag = new GameObject("Tag");
        tag.transform.SetParent(t, false);
        tag.transform.localPosition = Vector3.up * (h + 0.4f);
        var tm = tag.AddComponent<TextMesh>();
        tm.text = name + (owner.Length > 0 ? "\n<size=34>(" + owner + "'s)</size>" : "");
        tm.font = UIK.Font; tm.fontSize = 48; tm.characterSize = 0.035f; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.richText = true;
        tag.GetComponent<MeshRenderer>().sharedMaterial = UIK.Font != null ? UIK.Font.material : null;
        tag.AddComponent<Billboard>();
        return r;
    }

    // phone orders (3D: come / go / stop / wave / roofHeli / roofDrone; v2 extras: follow / dance)
    public string Order(string c, Frog from)
    {
        follow = null; flying = false; danceT = 0f;
        switch (c)
        {
            case "come": cmd = "come"; target = from.transform.position; status = "coming"; return robotName + ": coming to you";
            case "go": cmd = "go"; target = transform.position + transform.forward * 8f; status = "moving"; return robotName + ": moving out";
            case "stop": cmd = "idle"; speed = 0f; status = "standby"; alt = 0f; return robotName + ": stopped";
            case "wave": cmd = "wave"; wave = 2.2f; speed = 0f; status = "waving"; return robotName + ": waving";
            case "roofHeli": cmd = "roof"; target = new Vector3(-52f, Layout.HouseH + 0.1f + Ranch.PadTop, -6f) + new Vector3(2.5f, 0f, 2.5f); status = "heli pad"; flying = true; return robotName + ": flying to roof helipad (H)";
            case "roofDrone": cmd = "roof"; target = new Vector3(-30f, Layout.HouseH + 0.1f + Ranch.PadTop, -6f) + new Vector3(2.2f, 0f, 2.2f); status = "drone pad"; flying = true; return robotName + ": flying to passenger drone pad";
            case "follow": cmd = "follow"; follow = from.transform; status = "following"; return robotName + ": following " + from.nick;
            case "dance": cmd = "dance"; danceT = 8f; speed = 0f; status = "dancing"; return robotName + ": dance party!";
        }
        return "";
    }

    void Update()
    {
        float dt = Time.deltaTime;
        Vector3 p = transform.position;
        wave = Mathf.Max(0f, wave - dt);
        float want = 0f;
        Vector3 to = Vector3.zero;
        if (cmd == "follow" && follow != null) { target = follow.position; }
        if (cmd == "come" || cmd == "go" || cmd == "roof" || cmd == "follow")
        {
            to = target - p; to.y = 0f;
            float stopAt = cmd == "follow" ? 2.5f : cmd == "come" ? 2f : 0.3f;
            if (to.magnitude > stopAt) want = Mathf.Min(maxSpeed, to.magnitude * 1.5f);
            else if (cmd != "follow")
            {
                if (cmd == "roof") status = status + " (landed)";
                cmd = cmd == "roof" ? "parked" : "idle";
                if (status == "coming" || status == "moving") status = "standby";
                if (cmd == "idle") flying = false;
            }
        }
        if (cmd == "dance") { danceT -= dt; if (danceT <= 0f) { cmd = "idle"; status = "standby"; } }
        speed = Mathf.MoveTowards(speed, want, 12f * dt);
        if (to.sqrMagnitude > 0.01f && speed > 0.1f) yaw = Mathf.MoveTowardsAngle(yaw, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 220f * dt);
        if (cmd == "dance") yaw += 220f * dt;
        p += Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * speed * dt;
        // flying to a roof pad: jets on, climb over the house, settle on the pad
        float ground = Ranch.GY(p.x, p.z);
        RaycastHit hit;
        if ((flying || cmd == "parked") && Physics.Raycast(new Vector3(p.x, 60f, p.z), Vector3.down, out hit, 80f, Vehicle.GroundMask, QueryTriggerInteraction.Ignore)) ground = Mathf.Max(ground, hit.point.y);
        if (flying || cmd == "parked")
        {
            float d = new Vector2(target.x - p.x, target.z - p.z).magnitude;
            float cruise = d > 1f ? Mathf.Max(Layout.HouseH + 4f, ground + 2f) : target.y;
            alt = Mathf.MoveTowards(alt, cruise, 5f * dt);
            p.y = Mathf.Max(ground, alt);
            jets.gameObject.SetActive(p.y > ground + 0.2f);
            if (p.y > ground + 0.2f && Random.value < 0.4f) FX.Smoke(p + Vector3.up * height * 0.3f, 0.3f, new Color(0.7f, 0.9f, 1f, 0.6f));
        }
        else
        {
            p.y = ground;
            alt = p.y;
            jets.gameObject.SetActive(false);
        }
        transform.position = p;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        // animation
        phase += speed * 2.2f / Mathf.Max(0.5f, height) * dt * 3f;
        float sw = Mathf.Sin(phase) * Mathf.Clamp01(speed / 2f) * 30f;
        legL.localRotation = Quaternion.Euler(sw, 0f, 0f);
        legR.localRotation = Quaternion.Euler(-sw, 0f, 0f);
        float t = Time.time;
        if (cmd == "dance")
        {
            armL.localRotation = Quaternion.Euler(-150f + Mathf.Sin(t * 8f) * 30f, 0f, 0f);
            armR.localRotation = Quaternion.Euler(-150f - Mathf.Sin(t * 8f) * 30f, 0f, 0f);
            body.localPosition = Vector3.up * Mathf.Abs(Mathf.Sin(t * 8f)) * 0.15f;
        }
        else
        {
            armL.localRotation = Quaternion.Euler(-sw * 0.8f, 0f, 0f);
            armR.localRotation = wave > 0f ? Quaternion.Euler(0f, 0f, 160f + Mathf.Sin(t * 12f) * 20f) : Quaternion.Euler(sw * 0.8f, 0f, 0f);
            body.localPosition = Vector3.zero;
        }
        head.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 0.6f + height) * 20f, 0f);
    }
}

// James's robot phone: LB (pad) / P (keys) / PHONE (touch) opens it. Up/Down picks a robot, Left/Right an order,
// A / E / Enter sends it. A live POV picture-in-picture follows the linked robot.
public class RobotPhone : MonoBehaviour
{
    public static RobotPhone I;
    public bool open;
    public Frog user;
    int sel = 1, cmdSel;     // Unitree is the default link, like the 3D phone
    static readonly string[] Cmds = { "come", "go", "stop", "wave", "roofHeli", "roofDrone", "follow", "dance" };
    static readonly string[] CmdLabels = { "Come here", "Go (8 m)", "Stop", "Wave", "Roof helipad", "Drone pad", "Follow me", "Dance" };
    Canvas canvas;
    Image panel, btn;
    readonly List<Image> rows = new List<Image>(), cmdBtns = new List<Image>();
    readonly List<Text> rowTexts = new List<Text>();
    Text msg;
    Camera pov;
    float stickCool;

    void Awake()
    {
        I = this;
        canvas = UIK.MakeCanvas("Phone", null, 70, true);
        Transform r = canvas.transform;
        panel = UIK.Img(r, null, new Color(0.05f, 0.06f, 0.08f, 0.92f), new Vector2(1f, 0.5f), new Vector2(-200f, -40f), new Vector2(380f, 560f));
        Transform p = panel.transform;
        UIK.Label(p, "JAMES'S ROBOT PHONE", 22, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(360f, 30f), new Color(0.55f, 1f, 0.45f));
        var robots = RanchLife.I.robots;
        for (int i = 0; i < robots.Count; i++)
        {
            var row = UIK.Img(p, null, new Color(1f, 1f, 1f, 0.08f), new Vector2(0.5f, 1f), new Vector2(0f, -60f - i * 34f), new Vector2(350f, 30f));
            rows.Add(row);
            rowTexts.Add(UIK.Label(row.transform, "", 17, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), new Vector2(6f, 0f), new Vector2(340f, 28f), Color.white));
        }
        for (int i = 0; i < Cmds.Length; i++)
        {
            var b = UIK.Img(p, null, new Color(0.2f, 0.45f, 0.8f, 0.6f), new Vector2(0.5f, 0f), new Vector2(-88f + (i % 2) * 176f, 190f - (i / 2) * 40f), new Vector2(168f, 34f));
            UIK.Label(b.transform, CmdLabels[i], 17, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(166f, 32f), Color.white);
            cmdBtns.Add(b);
        }
        msg = UIK.Label(p, "Pick a robot, then order it.", 16, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(360f, 30f), new Color(1f, 0.95f, 0.6f));
        btn = UIK.Img(r, null, new Color(0.1f, 0.12f, 0.15f, 0.75f), new Vector2(0f, 1f), new Vector2(150f, -150f), new Vector2(110f, 44f));
        UIK.Label(btn.transform, "PHONE", 20, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 42f), Color.white);
        panel.gameObject.SetActive(false);
        btn.gameObject.SetActive(false);
        var cg = new GameObject("Phone POV");
        pov = cg.AddComponent<Camera>();
        pov.depth = 40; pov.fieldOfView = 70f; pov.nearClipPlane = 0.1f; pov.farClipPlane = 500f;
        pov.enabled = false;
    }

    public void Toggle(Frog f)
    {
        if (f == null || f.world != WorldId.Ranch) return;
        open = !open || user != f;
        user = f;
        msg.text = open ? "Robot phone - live POV" : "";
        Sfx.Play(Sfx.Click, 0.7f);
        if (open) f.Toast("Robot phone: Up/Down robot, Left/Right order, A send, LB / P close", 3f);
    }

    // called by Game with the phone user's input; returns true when the phone ate the input
    public bool Handle(Frog f, PIn i)
    {
        if (!open || f != user) return false;
        if (f.world != WorldId.Ranch || f.vehicle != null) { open = false; return false; }
        stickCool -= Time.unscaledDeltaTime;
        var robots = RanchLife.I.robots;
        float y = i.move.y, x = i.move.x;
        if (stickCool <= 0f)
        {
            if (y > 0.6f) { sel = (sel + robots.Count - 1) % robots.Count; stickCool = 0.22f; Sfx.Play(Sfx.Click, 0.4f); }
            else if (y < -0.6f) { sel = (sel + 1) % robots.Count; stickCool = 0.22f; Sfx.Play(Sfx.Click, 0.4f); }
            else if (x > 0.6f) { cmdSel = (cmdSel + 1) % Cmds.Length; stickCool = 0.22f; Sfx.Play(Sfx.Click, 0.4f); }
            else if (x < -0.6f) { cmdSel = (cmdSel + Cmds.Length - 1) % Cmds.Length; stickCool = 0.22f; Sfx.Play(Sfx.Click, 0.4f); }
        }
        if (Mathf.Abs(x) < 0.3f && Mathf.Abs(y) < 0.3f) stickCool = 0f;
        if (i.use || Kb.EnterDown()) Send(f);
        return true;
    }

    void Send(Frog f)
    {
        var r = RanchLife.I.robots[sel];
        msg.text = r.Order(Cmds[cmdSel], f);
        f.Toast(msg.text, 2f);
        Sfx.Play(Sfx.Pickup, 0.5f, 1.3f);
    }

    void Update()
    {
        bool showBtn = Game.I != null && Game.I.state == Game.State.Play && Application.isMobilePlatform;
        if (showBtn) foreach (var s in Game.I.slots) if (s.kind == InputKind.Touch) { Frog f = Game.I.frogs[s.frog]; showBtn = f.world == WorldId.Ranch && f.vehicle == null; }
        btn.gameObject.SetActive(showBtn);
        if (showBtn)
            foreach (Vector2 tp in Kb.TouchesBegan())
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(btn.rectTransform, tp, null))
                    foreach (var s in Game.I.slots) if (s.kind == InputKind.Touch) Toggle(Game.I.frogs[s.frog]);
                if (open)
                {
                    for (int i = 0; i < rows.Count; i++) if (RectTransformUtility.RectangleContainsScreenPoint(rows[i].rectTransform, tp, null)) { sel = i; Sfx.Play(Sfx.Click, 0.4f); }
                    for (int i = 0; i < cmdBtns.Count; i++) if (RectTransformUtility.RectangleContainsScreenPoint(cmdBtns[i].rectTransform, tp, null)) { cmdSel = i; if (user != null) Send(user); }
                }
            }
        if (user != null && (user.world != WorldId.Ranch || Game.I == null || Game.I.state != Game.State.Play)) open = false;
        panel.gameObject.SetActive(open);
        pov.enabled = open;
        if (!open) return;
        var robots = RanchLife.I.robots;
        for (int i = 0; i < robots.Count; i++)
        {
            var r = robots[i];
            rows[i].color = i == sel ? new Color(0.3f, 0.8f, 0.35f, 0.5f) : new Color(1f, 1f, 1f, 0.08f);
            rowTexts[i].text = r.robotName + (r.owner.Length > 0 ? " (" + r.owner + ")" : "") + "  <color=#9fd8ff>" + r.status + "</color>";
            rowTexts[i].supportRichText = true;
        }
        for (int i = 0; i < cmdBtns.Count; i++) cmdBtns[i].color = i == cmdSel ? new Color(1f, 0.75f, 0.2f, 0.85f) : new Color(0.2f, 0.45f, 0.8f, 0.6f);
        // live POV from the linked robot, picture-in-picture top-right
        var rb = robots[sel];
        pov.transform.position = rb.eye.position;
        pov.transform.rotation = rb.eye.rotation;
        pov.rect = new Rect(0.71f, 0.72f, 0.27f, 0.25f);
    }
}
