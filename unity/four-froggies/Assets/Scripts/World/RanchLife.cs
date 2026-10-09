using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Stage E: things that live on the ranch.
//  * James's robots (three.js "bible", ff3d112 robot table): Optimus (Bubbles's), Unitree, Figure 03, Figure 02,
//    Big Figure Two, Atlas HD, Atlas electric - parked by the garage with the same heights / speeds.
//  * ffu12: the robots are always working - auto chores + wall charging jacks (World/RobotChores.cs), and the phone can
//    give any robot a chore, send it to charge, or hand a froggy direct control of it ("Drive it!").
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
        RanchJobs.Create();     // ffu12: charging jacks, chore props, litter
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
        // lined up on the open lawn west of the garage (x < -29) and north of the house porch + flower bed (z > 20), facing the house;
        // the old spots (x -36..-20, z 14..22) were inside / against the garage's west bays
        Add("optimus", "Optimus", "Bubbles", 0xE1E1E1, 2.35f, 0.7f, 7.5f, -36f, 28f);
        Add("unitree", "Unitree", "", 0x2C2F38, 1.72f, 0.58f, 12f, -38.5f, 28f);
        Add("figure03", "Figure 03", "", 0xB3B3B3, 2.2f, 0.65f, 9f, -41f, 28f);
        Add("figure02", "Figure 02", "", 0x999999, 2.05f, 0.6f, 8.5f, -43.5f, 28f);
        Add("figure02big", "Big Figure Two", "", 0x888888, 3.4f, 1.35f, 6.5f, -46.3f, 28.6f);
        Add("atlas_hd", "Atlas HD", "", 0xD2D2D2, 2.45f, 0.72f, 10f, -49.1f, 28f);
        Add("atlas_el", "Atlas electric", "", 0xBCBCBC, 2.4f, 0.7f, 9.5f, -51.6f, 28f);

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
        var r = Robot.Build(id, name, owner, c, h, bulk, maxSp, new Vector3(x, Ranch.GY(x, z), z));
        // ffu12: mixed starting charge so a couple head straight for the jacks while the rest pick chores
        float[] start = { 0.2f, 0.85f, 0.62f, 0.95f, 0.22f, 0.74f, 0.5f };
        r.battery = start[robots.Count % start.Length];
        robots.Add(r);
    }
}

// One of James's robots: kinematic humanoid that walks on the terrain. ffu12: always working - an auto brain runs chores
// and charging (World/RobotChores.cs); phone orders interrupt it and it resumes on its own; a froggy can drive it by hand.
public class Robot : MonoBehaviour
{
    public string id, robotName, owner;
    public float height, maxSpeed, bulk;
    float sndStep, sndServo = 1f;   // ffu10 footsteps + servo whirs
    public string cmd = "auto", status = "starting up";
    public Transform follow;
    Vector3 target;
    float yaw, phase, speed, wave, alt, danceT, resumeT, followT;
    bool flying, landAuto;
    Transform legL, legR, armL, armR, body, head, jets, carryNode;
    public Transform eye;    // phone POV
    public Transform Body { get { return body; } }
    public Transform CarryNode { get { return carryNode; } }

    // ---- ffu12 brain state ----
    public float battery = 1f;
    public Frog manual;                  // froggy driving this robot from the phone
    public int forcedChore = -1;
    public bool forcedCharge;
    public int chore = -1;
    public ChargeJack jack;
    public bool Charging { get { return charging; } }
    bool charging;
    PIn mIn; float mYaw;
    class Step { public int k; public Vector3 p; public float t, spd = 1f, face = float.NaN, timeout; public int pose; public System.Action done; public System.Action<float> tick; }
    readonly List<Step> steps = new List<Step>();
    int stepIdx; float stepT; bool routed;
    readonly List<Vector3> route = new List<Vector3>();
    int routeIdx;
    Transform tool, toolHead;
    int toolKind = -1;
    readonly Dictionary<int, Transform> tools = new Dictionary<int, Transform>();
    readonly Dictionary<int, Transform> toolHeads = new Dictionary<int, Transform>();
    GameObject cargo; int cargoKind = -1;
    readonly List<LitterItem> bag = new List<LitterItem>();
    LeafPile rakePile;
    int lastChore = -1;
    Vector3 goalDir; float goalSpeed, faceYaw = float.NaN;
    int pose, workPose;                  // 0 walk, 1 hold tool, 2 carry, 3 bend, 4 plugged, 5 sweep, 6 rake, 7 water, 8 push
    float bend, fxT, sndT, tagT;
    Material lampM;
    TextMesh tagMesh;
    string tagLine = "";
    AudioSource loop;
    public const float LowBattery = 0.25f;
    float WalkSpeed { get { return Mathf.Min(maxSpeed * 0.32f, 2.8f); } }

    public static Robot Build(string id, string name, string owner, Color c, float h, float bulk, float maxSp, Vector3 pos)
    {
        var go = new GameObject("Robot " + name);
        go.transform.position = pos;
        var r = go.AddComponent<Robot>();
        r.id = id; r.robotName = name; r.owner = owner; r.height = h; r.maxSpeed = maxSp; r.bulk = bulk;
        r.yaw = 180f; r.target = pos;
        Material m = Mats.Shiny(c), dark = Mats.Lit(new Color(0.1f, 0.1f, 0.12f)), visor = Mats.Unlit(id.StartsWith("atlas") ? new Color(1f, 0.85f, 0.4f) : new Color(0.4f, 0.85f, 1f));
        float w = bulk * 0.6f;
        Transform t = go.transform;
        r.body = Mats.Node(t, "Body", Vector3.zero);
        // stage A standing robot meshes (Resources/LB/sr_*.bytes); the old box robot only if a pack is missing
        LBPack pk = LBPack.Get(PackFor(id));
        bool packed = pk != null && pk.Has("body") && pk.Has("thighL") && pk.Has("shinL") && pk.Has("uarmL") && pk.Has("farmL") && pk.Has("head");
        if (packed)
        {
            float s = h / Mathf.Max(0.5f, pk.Height());
            pk.Spawn("body", r.body, Vector3.zero, s, c);
            r.legL = Limb(pk, r.body, "thighL", "shinL", s, c);
            r.legR = Limb(pk, r.body, "thighR", "shinR", s, c);
            r.armL = Limb(pk, r.body, "uarmL", "farmL", s, c);
            r.armR = Limb(pk, r.body, "uarmR", "farmR", s, c);
            r.head = pk.Spawn("head", r.body, Vector3.zero, s, c);
            r.eye = Mats.Node(r.head, "Eye", new Vector3(0f, 0.08f, 0.14f));
        }
        else
        {
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
        }
        r.jets = Mats.Node(r.body, "Jets", new Vector3(0f, h * 0.55f, -w * 0.32f));
        var jm = new Material(Mats.Fx); jm.color = new Color(0.4f, 0.8f, 1f, 0.7f);
        for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Sphere, r.jets, new Vector3(w * 0.2f * s, -h * 0.25f, 0f), new Vector3(w * 0.15f, h * 0.25f, w * 0.15f), jm);
        r.jets.gameObject.SetActive(false);
        // ffu12: status lamp on the back (green pulse = charging, blue = working, amber = low, red = flat) + carry point
        r.lampM = new Material(Mats.Unlit(Color.white));
        r.lampM.color = new Color(0.3f, 0.7f, 1f);
        Mats.Prim(PrimitiveType.Cube, r.body, new Vector3(0f, h * 0.7f, -(bulk * 0.32f + 0.04f)), new Vector3(0.16f, 0.16f, 0.05f), r.lampM);
        Mats.Prim(PrimitiveType.Sphere, r.body, new Vector3(0f, h * 1.02f, 0f), Vector3.one * 0.09f, r.lampM);
        r.carryNode = Mats.Node(r.body, "Carry", new Vector3(0f, h * 0.55f, h * 0.27f + 0.25f));
        if (!packed || Look.Mobile) Mats.NoShadows(go);   // the mesh robots cast shadows on desktop (grounds them)
        // name tag (+ status line, refreshed twice a second)
        var tag = new GameObject("Tag");
        tag.transform.SetParent(t, false);
        tag.transform.localPosition = Vector3.up * (h + 0.55f);
        var tm = tag.AddComponent<TextMesh>();
        tm.text = name + (owner.Length > 0 ? "\n<size=34>(" + owner + "'s)</size>" : "");
        tm.font = UIK.Font; tm.fontSize = 48; tm.characterSize = 0.035f; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.richText = true;
        tag.GetComponent<MeshRenderer>().sharedMaterial = UIK.Font != null ? UIK.Font.material : null;
        tag.AddComponent<Billboard>();
        r.tagMesh = tm;
        r.loop = Sfx.EngEV != null ? Sfx.Loop(go, Sfx.EngEV) : null;
        return r;
    }

    static string PackFor(string id)
    {
        switch (id)
        {
            case "figure02big": return "sr_figure02";
            case "atlas_hd": return "sr_atlashd";
            case "atlas_el": return "sr_atlase";
            default: return "sr_" + id;
        }
    }

    // upper part at its pivot under the body + lower part under it (so the whole limb swings about the hip / shoulder)
    static Transform Limb(LBPack pk, Transform body, string upper, string lower, float s, Color c)
    {
        Transform u = pk.Spawn(upper, body, Vector3.zero, s, c);
        if (u == null) return Mats.Node(body, upper, Vector3.zero);
        pk.Spawn(lower, u, -pk.parts[upper].pivot, 1f, c);
        return u;
    }

    public Vector3 PlugPoint { get { return transform.position + Vector3.up * height * 0.48f - transform.forward * (bulk * 0.3f + 0.05f); } }
    public string Pct { get { return Mathf.RoundToInt(battery * 100f) + "%"; } }

    // ---------------- phone orders ----------------
    // 3D: come / go / stop / wave / roofHeli / roofDrone; v2 extras: follow / dance; ffu12: auto / charge / chore:<n>.
    // Everything except auto ends by itself and the robot goes back to its chores.
    public string Order(string c, Frog from)
    {
        if (manual != null) ReleaseManual(false);
        AbortPlan();
        follow = null; danceT = 0f; landAuto = false;
        bool aloft = transform.position.y > Ranch.GY(transform.position.x, transform.position.z) + 1.5f;
        if (!c.StartsWith("roof")) flying = aloft;
        if (c.StartsWith("chore:"))
        {
            int k = int.Parse(c.Substring(6));
            forcedChore = k; forcedCharge = false; cmd = "auto";
            if (aloft) Resume();
            return robotName + ": on it - " + Chores.Doing[k];
        }
        switch (c)
        {
            case "auto": cmd = "auto"; forcedChore = -1; forcedCharge = false; if (aloft) Resume(); return robotName + ": back to chores (auto)";
            case "charge": cmd = "auto"; forcedChore = -1; forcedCharge = true; if (aloft) Resume(); return robotName + ": going to a charging jack (" + Pct + ")";
            case "come": cmd = "come"; target = from.transform.position; status = "coming"; return robotName + ": coming to you";
            case "go": cmd = "go"; target = transform.position + transform.forward * 8f; status = "moving"; return robotName + ": moving out";
            case "stop": cmd = "idle"; speed = 0f; status = "standby"; resumeT = 15f; return robotName + ": stopped (chores again in 15 s)";
            case "wave": cmd = "wave"; wave = 2.2f; speed = 0f; status = "waving"; return robotName + ": waving";
            case "roofHeli": cmd = "roof"; target = new Vector3(-52f, Layout.HouseH + 0.1f + Ranch.PadTop, -6f) + new Vector3(2.5f, 0f, 2.5f); status = "heli pad"; flying = true; return robotName + ": flying to roof helipad (H)";
            case "roofDrone": cmd = "roof"; target = new Vector3(-30f, Layout.HouseH + 0.1f + Ranch.PadTop, -6f) + new Vector3(2.2f, 0f, 2.2f); status = "drone pad"; flying = true; return robotName + ": flying to passenger drone pad";
            case "follow": cmd = "follow"; follow = from.transform; followT = 45f; status = "following"; return robotName + ": following " + from.nick + " (45 s)";
            case "dance": cmd = "dance"; danceT = 8f; speed = 0f; status = "dancing"; return robotName + ": dance party!";
        }
        return "";
    }

    void Resume()
    {
        Vector3 p = transform.position;
        if (cmd == "parked" || p.y > Ranch.GY(p.x, p.z) + 1.5f)
        {
            cmd = "roof"; flying = true; landAuto = true; status = "flying down";
            target = RobotNav.G(-38f + Random.Range(-4f, 4f), 24f + Random.Range(-2f, 2f));
            return;
        }
        cmd = "auto"; flying = false;
    }

    // ---------------- manual control from the phone ----------------
    public void TakeManual(Frog f)
    {
        if (f == null) return;
        if (manual != null && manual != f) manual.remote = null;
        if (RanchLife.I != null) foreach (var o in RanchLife.I.robots) if (o != this && o.manual == f) o.ReleaseManual(false);
        if (cmd != "auto") Resume();
        if (cmd == "roof") { cmd = "auto"; flying = false; landAuto = false; }   // dropped from the roof by the driver - land in place
        AbortPlan();
        manual = f; f.remote = this;
        mIn = new PIn();
        Sfx.Play(Sfx.Confirm != null ? Sfx.Confirm : Sfx.Pickup, 0.6f);
        f.Toast("Driving " + robotName + ": stick walk, RT run, A wave. LB / P / PHONE hands it back to auto.", 4f);
    }

    public void ReleaseManual(bool say)
    {
        Frog f = manual;
        manual = null;
        if (f != null && f.remote == this) f.remote = null;
        cmd = "auto";
        steps.Clear(); stepIdx = 0;
        if (say && f != null) { f.Toast(robotName + " is back on auto chores (" + Pct + ")", 2.5f); Sfx.Play(Sfx.Click, 0.6f); }
    }

    public void Manual(PIn i, float camYaw) { mIn = i; mYaw = camYaw; }

    void ManualTick()
    {
        Vector3 m = new Vector3(mIn.move.x, 0f, mIn.move.y);
        if (m.sqrMagnitude > 0.01f)
        {
            float mag = Mathf.Min(1f, m.magnitude);
            goalDir = Quaternion.Euler(0f, mYaw, 0f) * m.normalized;
            goalSpeed = mag * Mathf.Lerp(Mathf.Min(maxSpeed, 4.2f), maxSpeed, mIn.gas);
        }
        if (mIn.hop || mIn.use) { wave = 1.6f; Sfx.PlayAt(Sfx.Pick(Sfx.Servo), transform.position + Vector3.up * height * 0.7f, 0.35f, 24f); }
        status = "driven by " + manual.nick;
        pose = 0;
        mIn = new PIn();
    }

    // ---------------- auto brain ----------------
    Step Add(int k, Vector3 p, int pose = 0, float t = 0f, float spd = 1f) { var s = new Step { k = k, p = p, pose = pose, t = t, spd = spd }; steps.Add(s); return s; }
    Step Walk(Vector3 p) { return Add(0, p); }
    Step Work(Vector3 p, int ps, float spd) { return Add(1, p, ps, 0f, spd); }
    Step Act(float t, int ps, float face, System.Action done = null) { var s = Add(2, Vector3.zero, ps, t); s.face = face; s.done = done; return s; }
    static float YawTo(Vector3 from, Vector3 to) { return Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg; }

    void AbortPlan()
    {
        var J = RanchJobs.I;
        if (chore >= 0 && J != null) J.busy[chore] = Mathf.Max(0, J.busy[chore] - 1);
        chore = -1;
        if (J != null)
        {
            foreach (var it in bag) J.Dropped(it, transform.position + Random.insideUnitSphere * 0.6f);
            foreach (var it in J.litter) if (it.claim == this && it.t.parent == J.transform) it.claim = null;
            foreach (var lp in J.leaves) if (lp.claim == this) lp.claim = null;
        }
        bag.Clear(); rakePile = null;
        if (jack != null) { if (jack.user == this) jack.user = null; jack = null; }
        charging = false;
        if (cargo != null) cargo.SetActive(false);
        SetTool(-1);
        steps.Clear(); stepIdx = 0; stepT = 0f; routed = false;
        pose = 0;
    }

    void SetTool(int k)
    {
        if (k == toolKind) return;
        if (tool != null) tool.gameObject.SetActive(false);
        toolKind = k; tool = null; toolHead = null;
        if (k < 0 || k == Chores.Crates || k == Chores.Hay || k == Chores.Litter) return;
        if (!tools.TryGetValue(k, out tool))
        {
            Transform hd;
            tool = RanchJobs.MakeTool(this, k, out hd);
            tools[k] = tool; toolHeads[k] = hd;
        }
        toolHead = toolHeads[k];
        tool.gameObject.SetActive(true);
    }

    void Cargo(int k, bool on)
    {
        if (on && (cargo == null || cargoKind != k)) { if (cargo != null) Destroy(cargo); cargo = RanchJobs.MakeCargo(this, k); cargoKind = k; }
        if (cargo != null) cargo.SetActive(on);
    }

    void PickPlan()
    {
        AbortPlan();
        var J = RanchJobs.I;
        if (J == null) return;
        if ((forcedCharge || battery < LowBattery) && PlanCharge()) return;
        int c = forcedChore >= 0 ? forcedChore : J.PickChore(this, lastChore);
        forcedChore = -1;
        if (c < 0)
        {
            // nothing free: top up at a jack, else stroll back towards the robot line
            if (battery < 0.97f && PlanCharge()) return;
            Walk(RobotNav.G(-44f + Random.Range(-8f, 8f), 30f + Random.Range(-1.5f, 3f)));
            Act(Random.Range(2f, 4f), 0, float.NaN);
            status = "looking for work";
            return;
        }
        chore = c; lastChore = c; J.busy[c]++;
        status = Chores.Doing[c];
        BuildChore(c);
        if (steps.Count == 0) { J.busy[c]--; chore = -1; Walk(transform.position); }
    }

    bool PlanCharge()
    {
        var J = RanchJobs.I;
        ChargeJack j = J.ClaimJack(this);
        if (j == null) return false;
        jack = j; forcedCharge = false;
        status = "to charger";
        Walk(j.stand);
        Act(0.6f, 0, j.faceYaw, () => { charging = true; status = "charging"; Sfx.PlayAt(Sfx.Confirm != null ? Sfx.Confirm : Sfx.Click, PlugPoint, 0.45f, 30f, 0.9f); FX.Sparkle(J.JackPlug(j), new Color(0.5f, 1f, 0.6f), 8); });
        var s = Add(3, j.stand, 4);
        s.face = j.faceYaw;
        s.done = () => { charging = false; Sfx.PlayAt(Sfx.Click, PlugPoint, 0.4f, 30f, 1.2f); if (jack != null && jack.user == this) jack.user = null; jack = null; status = "charged"; };
        return true;
    }

    void BuildChore(int c)
    {
        var J = RanchJobs.I;
        Vector3 p = transform.position;
        switch (c)
        {
            case Chores.Sweep:
                {
                    SetTool(c);
                    float z1 = 15.2f, z2 = 13.4f;
                    Walk(RobotNav.G(-51.6f, z1));
                    Act(0.4f, 1, 90f);
                    Work(RobotNav.G(-32.4f, z1), 5, 1f);
                    Work(RobotNav.G(-32.4f, z2), 5, 0.8f);
                    Work(RobotNav.G(-51.6f, z2), 5, 1f);
                    break;
                }
            case Chores.Vacuum:
                {
                    SetTool(c);
                    float z1 = 17.0f, z2 = 15.6f;
                    Walk(RobotNav.G(-25.2f, z1));
                    Act(0.4f, 1, 90f);
                    Work(RobotNav.G(22.5f, z1), 8, 1.1f);
                    Work(RobotNav.G(22.5f, z2), 8, 0.8f);
                    Work(RobotNav.G(-25.2f, z2), 8, 1.1f);
                    break;
                }
            case Chores.Crates:
            case Chores.Hay:
                {
                    Vector2 a = c == Chores.Crates ? RanchJobs.CratePile : RanchJobs.HayPile, b = c == Chores.Crates ? RanchJobs.CrateDrop : RanchJobs.HayDrop;
                    Vector3 A = RobotNav.G(a.x, a.y), B = RobotNav.G(b.x, b.y);
                    Vector3 pickAt = A + (B - A).normalized * 1.6f, dropAt = B + (A - B).normalized * 1.7f;
                    pickAt = RobotNav.G(pickAt.x, pickAt.z); dropAt = RobotNav.G(dropAt.x, dropAt.z);
                    for (int trip = 0; trip < 2; trip++)
                    {
                        Walk(pickAt);
                        Act(1.0f, 3, YawTo(pickAt, A), () => { Cargo(c, true); Sfx.PlayAt(Sfx.BumpSoft != null ? Sfx.BumpSoft : Sfx.Thud, A, 0.4f, 30f); });
                        Walk(dropAt);
                        Act(0.9f, 3, YawTo(dropAt, B), () => { Cargo(c, false); if (c == Chores.Crates) J.AddCrate(); else J.AddHay(); Sfx.PlayAt(c == Chores.Hay ? Sfx.Thud : (Sfx.Clank != null ? Sfx.Clank : Sfx.Thud), B, 0.45f, 30f); FX.Dust(B + Vector3.up * 0.3f, 1f); });
                    }
                    break;
                }
            case Chores.Litter:
                {
                    Vector3 from = p;
                    for (int n = 0; n < 3; n++)
                    {
                        LitterItem it = J.ClaimLitter(this, from);
                        if (it == null) break;
                        Vector3 ip = it.t.position, d = from - ip; d.y = 0f;
                        Vector3 at = ip + (d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward) * (0.7f + bulk * 0.3f);
                        at = RobotNav.G(at.x, at.z);
                        Walk(at);
                        LitterItem cap = it;
                        Act(0.9f, 3, YawTo(at, ip), () => Grab(cap));
                        from = ip;
                    }
                    if (steps.Count == 0) return;
                    Vector3 bin = RobotNav.G(RanchJobs.BinP.x, RanchJobs.BinP.y), front = RobotNav.G(RanchJobs.BinP.x - 1.4f, RanchJobs.BinP.y + 0.4f);
                    Walk(front);
                    Act(1.0f, 2, YawTo(front, bin), () => { foreach (var it in bag) J.Binned(it); bag.Clear(); });
                    break;
                }
            case Chores.Mow:
                {
                    SetTool(c);
                    Rect m = RanchJobs.MowArea;
                    int lanes = Mathf.FloorToInt(m.width / 2.2f);
                    for (int i = 0; i < 3; i++)
                    {
                        int lane = J.mowLane++ % lanes;
                        float x = m.xMin + 1.1f + lane * 2.2f;
                        bool up = lane % 2 == 0;
                        Vector3 s0 = RobotNav.G(x, up ? m.yMin : m.yMax), s1 = RobotNav.G(x, up ? m.yMax : m.yMin);
                        if (i == 0) { Walk(s0); Act(0.4f, 1, YawTo(s0, s1)); }
                        else Work(s0, 8, 0.9f);
                        Work(s1, 8, 1.35f);
                    }
                    break;
                }
            case Chores.Rake:
                {
                    SetTool(c);
                    Vector3 from = p;
                    for (int n = 0; n < 2; n++)
                    {
                        LeafPile lp = J.ClaimLeaves(this, from);
                        if (lp == null) break;
                        Vector3 lpp = lp.t.position, d = from - lpp; d.y = 0f;
                        Vector3 at = lpp + (d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward) * (1.5f + bulk * 0.3f);
                        at = RobotNav.G(at.x, at.z);
                        Walk(at);
                        LeafPile cap = lp;
                        var s = Act(6.5f, 6, YawTo(at, lpp), () => { if (cap.active) J.Raked(cap, 1f); cap.claim = null; });
                        s.tick = dt => { if (cap.active) J.Raked(cap, dt / 6.5f); };
                        from = lpp;
                    }
                    if (steps.Count == 0) SetTool(-1);
                    break;
                }
            case Chores.Water:
                {
                    SetTool(c);
                    float[] xs = { -50f, -42.8f, -35f };
                    int dir = Random.value < 0.5f ? 1 : -1;
                    for (int i = 0; i < 3; i++)
                    {
                        float x = xs[dir > 0 ? i : 2 - i];
                        Walk(RobotNav.G(x, 21.7f));
                        Act(2.6f, 7, 180f);
                    }
                    break;
                }
        }
    }

    void Grab(LitterItem it)
    {
        if (it == null || !it.active) return;
        it.t.SetParent(carryNode, true);
        it.t.localPosition = new Vector3(0f, -0.05f + bag.Count * 0.22f, 0.05f);
        bag.Add(it);
        Sfx.PlayAt(Sfx.Pick(Sfx.Servo), transform.position, 0.3f, 24f, 1.3f);
    }

    void NextStep()
    {
        var s = steps[stepIdx];
        stepIdx++; stepT = 0f; routed = false;
        if (s.done != null) s.done();
    }

    void Brain(float dt)
    {
        if (stepIdx >= steps.Count)
        {
            // chore finished
            var J = RanchJobs.I;
            if (chore >= 0 && J != null) J.busy[chore] = Mathf.Max(0, J.busy[chore] - 1);
            chore = -1;
            PickPlan();
            if (steps.Count == 0) return;
        }
        // nearly flat in the middle of a chore: drop it and go charge
        if (!charging && jack == null && battery < 0.1f && chore >= 0) { AbortPlan(); if (!PlanCharge()) { Walk(transform.position); } }
        if (stepIdx >= steps.Count) return;
        var st = steps[stepIdx];
        stepT += dt;
        Vector3 p = transform.position;
        int carryPose = bag.Count > 0 || (cargo != null && cargo.activeSelf) ? 2 : tool != null ? 1 : 0;
        switch (st.k)
        {
            case 0:
                {
                    if (!routed)
                    {
                        RobotNav.Route(p, st.p, route);
                        routeIdx = 0; routed = true;
                        float len = 0f; Vector3 a = p;
                        foreach (var w in route) { len += RobotNav.Flat(w - a); a = w; }
                        st.timeout = len / WalkSpeed * 2.2f + 6f;
                    }
                    pose = carryPose;
                    Vector3 wp = route[routeIdx];
                    bool last = routeIdx == route.Count - 1;
                    float d = RobotNav.Flat(wp - p);
                    if (d < (last ? 0.3f : 0.9f)) { routeIdx++; if (routeIdx >= route.Count) { NextStep(); return; } wp = route[routeIdx]; }
                    Vector3 to = wp - p; to.y = 0f;
                    goalDir = to.normalized;
                    goalSpeed = WalkSpeed * (carryPose == 2 ? 0.8f : 1f);
                    if (last) goalSpeed = Mathf.Min(goalSpeed, d * 1.6f + 0.25f);
                    if (stepT > st.timeout) { transform.position = RobotNav.G(st.p.x, st.p.z); NextStep(); }
                    break;
                }
            case 1:
                {
                    pose = st.pose;
                    Vector3 to = st.p - p; to.y = 0f;
                    float d = to.magnitude;
                    if (d < 0.25f || stepT > d / Mathf.Max(0.3f, st.spd) * 3f + 8f) { NextStep(); return; }
                    goalDir = to / d;
                    goalSpeed = Mathf.Min(st.spd, d * 2f + 0.2f);
                    WorkFx(dt);
                    break;
                }
            case 2:
                pose = st.pose == 0 ? carryPose : st.pose;
                faceYaw = st.face;
                if (st.tick != null) st.tick(dt);
                WorkFx(dt);
                if (stepT >= st.t) NextStep();
                break;
            case 3:
                pose = 4;
                faceYaw = st.face;
                status = "charging";
                if (battery >= 0.995f && stepT > 3f) NextStep();
                break;
        }
    }

    // particles + sounds while working (throttled)
    void WorkFx(float dt)
    {
        fxT -= dt; sndT -= dt;
        Vector3 hp = toolHead != null ? toolHead.position : transform.position + transform.forward;
        switch (pose)
        {
            case 5:
                if (fxT <= 0f) { fxT = 0.18f; FX.Dust(hp, 0.7f); }
                if (sndT <= 0f) { sndT = 0.45f; Sfx.PlayAt(Sfx.Pick(Sfx.Foot), hp, 0.22f, 22f, 1.6f); }
                break;
            case 6:
                if (fxT <= 0f) { fxT = 0.22f; FX.Smoke(hp, 0.35f, new Color(0.75f, 0.4f, 0.12f, 0.8f)); }
                if (sndT <= 0f) { sndT = 0.7f; Sfx.PlayAt(Sfx.Pick(Sfx.Foot), hp, 0.25f, 22f, 0.8f); }
                break;
            case 7:
                {
                    Vector3 v = transform.forward * 1.2f + Vector3.down * 0.6f;
                    int n = Look.Mobile ? 1 : 2;
                    for (int i = 0; i < n; i++) FX.Spray(hp, v + Random.insideUnitSphere * 0.25f, 0.09f, 0.55f, new Color(0.7f, 0.85f, 1f, 0.75f));
                    if (sndT <= 0f) { sndT = 0.9f; Sfx.PlayAt(Sfx.Splash, hp, 0.12f, 20f, 1.8f); }
                    break;
                }
            case 8:
                if (toolKind == Chores.Mow && fxT <= 0f && speed > 0.3f) { fxT = 0.12f; FX.Smoke(hp - transform.forward * 0.4f + Vector3.up * 0.1f, 0.3f, new Color(0.35f, 0.65f, 0.2f, 0.85f)); }
                if (toolKind == Chores.Vacuum && fxT <= 0f && speed > 0.3f) { fxT = 0.5f; FX.Sparkle(hp, new Color(0.5f, 1f, 1f), 2); }
                break;
        }
    }

    // ---------------- old phone orders (come / go / roof / follow / dance / wave / stop) ----------------
    void OrderTick(float dt, Vector3 p)
    {
        pose = 0;
        if (cmd == "follow" && follow != null) target = follow.position;
        if (cmd == "come" || cmd == "go" || cmd == "roof" || cmd == "follow")
        {
            Vector3 to = target - p; to.y = 0f;
            float stopAt = cmd == "follow" ? 2.5f : cmd == "come" ? 2f : 0.3f;
            if (to.magnitude > stopAt) { goalDir = to.normalized; goalSpeed = Mathf.Min(maxSpeed, to.magnitude * 1.5f); }
            else if (cmd != "follow")
            {
                if (cmd == "roof")
                {
                    if (landAuto)
                    {
                        if (p.y > RobotNav.Floor(p.x, p.z) + 0.25f) return;    // still descending onto the lawn
                        landAuto = false; flying = false; cmd = "auto"; return;
                    }
                    status = status + " (landed)"; cmd = "parked"; resumeT = 20f;
                }
                else { resumeT = cmd == "come" ? 10f : 5f; cmd = "idle"; status = "standby"; flying = false; }
            }
        }
        if (cmd == "follow") { followT -= dt; if (followT <= 0f || follow == null) { cmd = "idle"; resumeT = 0.1f; } }
        if (cmd == "dance") { danceT -= dt; if (danceT <= 0f) { cmd = "idle"; status = "standby"; resumeT = 2f; } }
        if (cmd == "wave" && wave <= 0f) { cmd = "idle"; status = "standby"; resumeT = 2f; }
        if (cmd == "idle" || cmd == "parked") { resumeT -= dt; if (resumeT <= 0f) Resume(); }
    }

    // keeps moving robots apart from each other, frogs and vehicles (cheap: flat distance checks)
    Vector3 Avoid(Vector3 p, Vector3 dir, ref float spd)
    {
        Vector3 push = Vector3.zero;
        var all = RanchLife.I.robots;
        for (int i = 0; i < all.Count; i++)
        {
            var o = all[i];
            if (o == this) continue;
            Vector3 d = p - o.transform.position; d.y = 0f;
            float m = d.magnitude, R = 0.8f + (bulk + o.bulk) * 0.65f;
            if (m >= R || m < 0.001f) continue;
            push += d / m * (R - m) / R * 1.6f;
            if (Vector3.Dot(dir, -d / m) > 0.6f) push += new Vector3(dir.z, 0f, -dir.x) * 0.7f;   // sidestep a robot in the way
        }
        if (Game.I != null)
            foreach (var f in Game.I.frogs)
            {
                if (f == null || f.world != WorldId.Ranch || f.vehicle != null) continue;
                Vector3 d = p - f.transform.position; d.y = 0f;
                float m = d.magnitude;
                if (m >= 1.8f || m < 0.001f) continue;
                push += d / m * (1.8f - m) * 0.9f;
                if (Vector3.Dot(dir, -d / m) > 0.5f && m < 1.4f) spd *= 0.4f;   // yield to a frog right ahead
            }
        foreach (var v in Vehicle.All)
        {
            if (v == null || !v.isActiveAndEnabled) continue;
            Vector3 d = p - v.transform.position;
            if (Mathf.Abs(d.x) > 12f || Mathf.Abs(d.z) > 12f || Mathf.Abs(d.y) > 6f) continue;
            d.y = 0f;
            float m = d.magnitude, vs = v.Speed, R = vs > 1f ? 4f + vs * 0.3f : 2.4f;
            if (m >= R || m < 0.001f) continue;
            push += d / m * (R - m) / R * (vs > 1f ? 2.6f : 1.1f);
            if (vs > 1f && Vector3.Dot(dir, -d / m) > 0.3f) spd *= 0.5f;
        }
        Vector3 nd = dir + push;
        nd.y = 0f;
        return nd.sqrMagnitude > 0.0001f ? nd.normalized : dir;
    }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        Vector3 p = transform.position;
        wave = Mathf.Max(0f, wave - dt);
        goalDir = Vector3.zero; goalSpeed = 0f; faceYaw = float.NaN;
        if (manual != null && (manual.remote != this || !manual.human || manual.world != WorldId.Ranch || manual.vehicle != null)) ReleaseManual(false);
        if (manual != null) ManualTick();
        else if (cmd == "auto") Brain(dt);
        else OrderTick(dt, p);

        // battery: charges at a jack (~22 s from empty), drains slowly while working (~2.5-3 min of chores)
        if (charging) battery = Mathf.Min(1f, battery + dt / 22f);
        else battery = Mathf.Max(0f, battery - dt * (1f / 420f + (speed > 0.3f ? 1f / 300f : 0f) + (pose >= 5 ? 1f / 320f : 0f) + (flying ? 1f / 60f : 0f)));
        float spd = goalSpeed;
        if (battery <= 0.01f) spd = Mathf.Min(spd, 0.8f);

        Vector3 dir = goalDir;
        if (spd > 0.05f && manual == null && !flying) dir = Avoid(p, dir, ref spd);
        speed = Mathf.MoveTowards(speed, spd, (manual != null ? 14f : 8f) * dt);
        if (dir.sqrMagnitude > 0.01f && spd > 0.05f) yaw = Mathf.MoveTowardsAngle(yaw, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, (manual != null ? 480f : 240f) * dt);
        else if (!float.IsNaN(faceYaw)) yaw = Mathf.MoveTowardsAngle(yaw, faceYaw, 200f * dt);
        if (cmd == "dance" && manual == null) yaw += 220f * dt;
        Vector3 np = p + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * speed * dt;
        if (!flying && RobotNav.Blocked(np.x, np.z) && !RobotNav.Blocked(p.x, p.z))
        {
            if (!RobotNav.Blocked(np.x, p.z)) np.z = p.z;
            else if (!RobotNav.Blocked(p.x, np.z)) np.x = p.x;
            else { np.x = p.x; np.z = p.z; }
        }
        p.x = np.x; p.z = np.z;
        // ffu10: metal footsteps while walking, servo whirs when moving / waving / dancing (positional)
        if (!flying && speed > 0.3f) { sndStep += speed * dt; if (sndStep > height * 0.38f) { sndStep = 0f; Sfx.PlayAt(Sfx.StepMetal, p, 0.32f, 28f, Random.Range(0.9f, 1.15f) * Mathf.Clamp(1.8f / height, 0.6f, 1.4f)); } }
        sndServo -= dt;
        if (sndServo <= 0f && (speed > 0.3f || wave > 0f || cmd == "dance" || pose >= 5))
        {
            sndServo = Random.Range(1.2f, 3.2f);
            Sfx.PlayAt(Sfx.Pick(Sfx.Servo), p + Vector3.up * height * 0.7f, 0.24f, 24f, Random.Range(0.85f, 1.2f));
        }
        // flying to / from a roof pad: jets on, climb over the house, settle on the pad
        float ground = RobotNav.Floor(p.x, p.z);
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
            if (jets.gameObject.activeSelf) jets.gameObject.SetActive(false);
        }
        transform.position = p;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        Animate(dt);
        Lamp();
        Hum();
        tagT -= dt;
        if (tagT <= 0f) { tagT = 0.5f; UpdateTag(); }
    }

    void Animate(float dt)
    {
        phase += speed * 2.2f / Mathf.Max(0.5f, height) * dt * 3f;
        float sw = Mathf.Sin(phase) * Mathf.Clamp01(speed / 2f) * 30f;
        legL.localRotation = Quaternion.Euler(sw, 0f, 0f);
        legR.localRotation = Quaternion.Euler(-sw, 0f, 0f);
        float t = Time.time;
        bend = Mathf.MoveTowards(bend, pose == 3 ? 20f : pose == 6 ? 10f : pose == 5 ? 8f : 0f, 70f * dt);
        Quaternion aL, aR;
        float hx = 0f;
        if (cmd == "dance" && manual == null)
        {
            aL = Quaternion.Euler(-150f + Mathf.Sin(t * 8f) * 30f, 0f, 0f);
            aR = Quaternion.Euler(-150f - Mathf.Sin(t * 8f) * 30f, 0f, 0f);
            body.localPosition = Vector3.up * Mathf.Abs(Mathf.Sin(t * 8f)) * 0.15f;
        }
        else
        {
            body.localPosition = Vector3.zero;
            switch (pose)
            {
                case 1: case 8: aL = aR = Quaternion.Euler(-45f + sw * 0.15f, 0f, 0f); break;
                case 2: aL = Quaternion.Euler(-78f, 0f, -8f); aR = Quaternion.Euler(-78f, 0f, 8f); break;
                case 3: { float k = Mathf.Sin(t * 3f) * 6f; aL = Quaternion.Euler(-62f + k, 0f, 0f); aR = Quaternion.Euler(-62f - k, 0f, 0f); hx = 20f; break; }
                case 4: aL = Quaternion.Euler(-6f, 0f, -4f); aR = Quaternion.Euler(-6f, 0f, 4f); hx = 14f + Mathf.Sin(t * 1.2f) * 3f; break;
                case 5: { float k = Mathf.Sin(t * 5f); aL = Quaternion.Euler(-42f + k * 8f, k * 14f, 0f); aR = Quaternion.Euler(-42f - k * 8f, k * 14f, 0f); break; }
                case 6: { float k = Mathf.Sin(t * 3.2f); aL = aR = Quaternion.Euler(-50f + k * 16f, 0f, 0f); break; }
                case 7: aL = Quaternion.Euler(-10f, 0f, 0f); aR = Quaternion.Euler(-70f, 0f, 0f); break;
                default: aL = Quaternion.Euler(-sw * 0.8f, 0f, 0f); aR = Quaternion.Euler(sw * 0.8f, 0f, 0f); break;
            }
            if (wave > 0f) aR = Quaternion.Euler(0f, 0f, 160f + Mathf.Sin(t * 12f) * 20f);
        }
        armL.localRotation = aL;
        armR.localRotation = aR;
        body.localRotation = Quaternion.Euler(bend, 0f, 0f);
        head.localRotation = Quaternion.Euler(hx, pose >= 1 && pose != 4 ? 0f : Mathf.Sin(t * 0.6f + height) * 20f, 0f);
        // tool motion
        if (tool != null)
        {
            Vector3 basePos = new Vector3(0f, height * 0.53f, height * 0.27f + 0.12f);
            if (pose == 5) { tool.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 5f) * 26f, 0f); tool.localPosition = basePos; }
            else if (pose == 6) { tool.localRotation = Quaternion.identity; tool.localPosition = basePos + Vector3.forward * Mathf.Sin(t * 3.2f) * 0.3f; }
            else if (pose == 7) { tool.localRotation = Quaternion.Euler(40f, 0f, 0f); tool.localPosition = basePos; }
            else if (pose == 8) { tool.localRotation = Quaternion.identity; tool.localPosition = basePos; }
            else { tool.localRotation = Quaternion.Euler(-25f, 0f, 0f); tool.localPosition = basePos + new Vector3(0.1f, 0.05f, -0.05f); }
        }
    }

    void Lamp()
    {
        Color c;
        if (charging) c = Color.Lerp(new Color(0.1f, 0.5f, 0.15f), new Color(0.5f, 1f, 0.55f), 0.5f + 0.5f * Mathf.Sin(Time.time * 4.5f));
        else if (manual != null) c = new Color(1f, 0.45f, 0.9f);
        else if (battery < 0.05f) c = new Color(1f, 0.15f, 0.1f);
        else if (battery < LowBattery) c = new Color(1f, 0.7f, 0.15f);
        else c = new Color(0.3f, 0.7f, 1f);
        if (lampM.color != c) lampM.color = c;
    }

    // charging hum + mower / vacuum motor: one looping source per robot, 2D, volume by distance to the nearest froggy
    void Hum()
    {
        if (loop == null) return;
        float baseV = 0f, pitch = 1f;
        if (charging) { baseV = 0.14f; pitch = 0.42f; }
        else if (pose == 8 && toolKind == Chores.Mow) { baseV = 0.2f; pitch = 1.05f; }
        else if (pose == 8 && toolKind == Chores.Vacuum) { baseV = 0.14f; pitch = 1.9f; }
        float v = 0f;
        if (baseV > 0f) { float k = Mathf.Clamp01(1f - Sfx.Near(transform.position) / 26f); v = baseV * k * k; }
        if (v < 0.004f) { if (loop.isPlaying) loop.Stop(); return; }
        loop.pitch = pitch; loop.volume = v;
        if (!loop.isPlaying) loop.Play();
    }

    public string StatusLine
    {
        get
        {
            if (manual != null) return "driven by " + manual.nick;
            if (cmd != "auto") return status;
            if (charging) return "charging";
            if (jack != null) return "to charger";
            if (chore >= 0) return Chores.Doing[chore];
            return status;
        }
    }

    void UpdateTag()
    {
        if (tagMesh == null) return;
        string col = charging ? "#8cff8c" : battery < LowBattery ? "#ffc040" : "#9fd8ff";
        string line = robotName + (owner.Length > 0 ? " <size=30>(" + owner + "'s)</size>" : "") + "\n<size=30><color=" + col + ">" + StatusLine + "  " + Pct + "</color></size>";
        if (line != tagLine) { tagLine = line; tagMesh.text = line; }
    }

    // demo / screenshot mode: put the robot somewhere and start a given chore (-2 = charge, jack index in arg)
    public void DemoStart(Vector3 pos, float yawDeg, int what, float batt, int jackIdx = -1)
    {
        if (manual != null) ReleaseManual(false);
        AbortPlan();
        cmd = "auto"; flying = false;
        battery = batt;
        transform.position = RobotNav.G(pos.x, pos.z);
        yaw = yawDeg;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        var J = RanchJobs.I;
        if (what == -2 && J != null && jackIdx >= 0 && jackIdx < J.jacks.Count)
        {
            var j = J.jacks[jackIdx];
            if (j.user != null && j.user != this) j.user.AbortPlan();
            j.user = this; jack = j;
            transform.position = j.stand; yaw = j.faceYaw;
            Act(0.3f, 0, j.faceYaw, () => { charging = true; status = "charging"; });
            var s = Add(3, j.stand, 4); s.face = j.faceYaw;
            s.done = () => { charging = false; if (jack != null && jack.user == this) jack.user = null; jack = null; };
            return;
        }
        if (what >= 0) forcedChore = what;
    }
}

// James's robot phone: LB (pad) / P (keys) / PHONE (touch) opens it. Up/Down picks a robot, Left/Right an order,
// A / E / Enter sends it (touch: tap a robot row, then an order). Each row shows what the robot is doing + its battery.
// ffu12 orders: Auto chores, Go charge, Drive it! (manual control with the normal controls + follow camera; LB / P / PHONE
// hands it back), the 8 chores, then the 3D orders. A live POV picture-in-picture follows the selected robot.
public class RobotPhone : MonoBehaviour
{
    public static RobotPhone I;
    public bool open;
    public Frog user;
    int sel = 1, cmdSel;     // Unitree is the default link, like the 3D phone
    static readonly string[] Cmds = { "auto", "charge", "drive", "chore:0", "chore:1", "chore:2", "chore:3", "chore:4", "chore:5", "chore:6", "chore:7",
                                      "come", "go", "stop", "wave", "roofHeli", "roofDrone", "follow", "dance" };
    static readonly string[] CmdLabels = { "Auto chores", "Go charge", "DRIVE IT!", "Sweep porch", "Vacuum garage", "Haul crates", "Haul hay", "Pick up litter", "Mow lawn", "Rake leaves", "Water flowers",
                                           "Come here", "Go (8 m)", "Stop", "Wave", "Roof helipad", "Drone pad", "Follow me", "Dance" };
    Canvas canvas;
    Image panel, btn;
    readonly List<Image> rows = new List<Image>(), cmdBtns = new List<Image>();
    readonly List<Text> rowNames = new List<Text>(), rowStats = new List<Text>();
    Text msg, hint;
    Camera pov;
    float stickCool;
    const float PW = 430f, PH = 650f;

    static Color CmdColor(int i, bool on)
    {
        if (on) return new Color(1f, 0.75f, 0.2f, 0.9f);
        return i < 3 ? new Color(0.15f, 0.55f, 0.3f, 0.75f) : i < 11 ? new Color(0.12f, 0.45f, 0.5f, 0.7f) : new Color(0.2f, 0.35f, 0.7f, 0.6f);
    }

    void Awake()
    {
        I = this;
        canvas = UIK.MakeCanvas("Phone", null, 70, true);
        Transform r = canvas.transform;
        panel = UIK.Img(r, null, new Color(0.04f, 0.05f, 0.07f, 0.93f), new Vector2(1f, 0.5f), new Vector2(-PW * 0.5f - 12f, 0f), new Vector2(PW, PH));
        Transform p = panel.transform;
        UIK.Label(p, "JAMES'S ROBOT PHONE", 21, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(PW - 20f, 28f), new Color(0.55f, 1f, 0.45f));
        var robots = RanchLife.I.robots;
        for (int i = 0; i < robots.Count; i++)
        {
            var row = UIK.Img(p, null, new Color(1f, 1f, 1f, 0.08f), new Vector2(0.5f, 1f), new Vector2(0f, -54f - i * 31f), new Vector2(PW - 20f, 28f));
            rows.Add(row);
            var n = UIK.Label(row.transform, robots[i].robotName, 16, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(76f, 0f), new Vector2(140f, 26f), Color.white);
            rowNames.Add(n);
            var st = UIK.Label(row.transform, "", 15, TextAnchor.MiddleRight, new Vector2(1f, 0.5f), new Vector2(-128f, 0f), new Vector2(244f, 26f), Color.white);
            st.supportRichText = true;
            rowStats.Add(st);
        }
        UIK.Label(p, "ORDERS", 15, TextAnchor.MiddleLeft, new Vector2(0.5f, 1f), new Vector2(0f, -54f - robots.Count * 31f - 6f), new Vector2(PW - 30f, 20f), new Color(0.7f, 0.85f, 0.7f));
        float gy = -54f - robots.Count * 31f - 32f;
        for (int i = 0; i < Cmds.Length; i++)
        {
            int col = i % 3, rr = i / 3;
            var b = UIK.Img(p, null, CmdColor(i, false), new Vector2(0.5f, 1f), new Vector2(-136f + col * 136f, gy - rr * 33f), new Vector2(130f, 29f));
            UIK.Label(b.transform, CmdLabels[i], 14, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(128f, 28f), Color.white);
            cmdBtns.Add(b);
        }
        msg = UIK.Label(p, "Pick a robot, then order it.", 15, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 42f), new Vector2(PW - 20f, 26f), new Color(1f, 0.95f, 0.6f));
        hint = UIK.Label(p, "Up/Down robot   Left/Right order   A / E / Enter send   LB / P close", 13, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(PW - 16f, 22f), new Color(0.75f, 0.8f, 0.85f));
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
        if (f == null) return;
        // driving a robot by hand: the phone button hands it back to the auto brain
        if (f.remote != null) { f.remote.ReleaseManual(true); return; }
        if (f.world != WorldId.Ranch) return;
        open = !open || user != f;
        user = f;
        msg.text = open ? "Live POV from the selected robot" : "";
        Sfx.Play(Sfx.Click, 0.7f);
        if (open) f.Toast("Robot phone: Up/Down robot, Left/Right order, A send, LB / P close", 3f);
    }

    // demo / screenshot mode
    public void DemoOpen(Frog f, int robot, string cmd)
    {
        user = f; open = true; sel = robot;
        int k = System.Array.IndexOf(Cmds, cmd);
        if (k >= 0) cmdSel = k;
    }
    public void DemoSend() { if (user != null) Send(user); }

    // called by Game with the phone user's input; returns true when the phone ate the input
    public bool Handle(Frog f, PIn i)
    {
        if (!open || f != user) return false;
        if (f.world != WorldId.Ranch || f.vehicle != null || f.remote != null) { open = false; return false; }
        stickCool -= Time.unscaledDeltaTime;
        var robots = RanchLife.I.robots;
        float y = i.move.y, x = i.move.x;
        if (stickCool <= 0f)
        {
            if (y > 0.6f) { sel = (sel + robots.Count - 1) % robots.Count; stickCool = 0.22f; Sfx.Play(Sfx.Click, 0.4f); }
            else if (y < -0.6f) { sel = (sel + 1) % robots.Count; stickCool = 0.22f; Sfx.Play(Sfx.Click, 0.4f); }
            else if (x > 0.6f) { cmdSel = (cmdSel + 1) % Cmds.Length; stickCool = 0.18f; Sfx.Play(Sfx.Click, 0.4f); }
            else if (x < -0.6f) { cmdSel = (cmdSel + Cmds.Length - 1) % Cmds.Length; stickCool = 0.18f; Sfx.Play(Sfx.Click, 0.4f); }
        }
        if (Mathf.Abs(x) < 0.3f && Mathf.Abs(y) < 0.3f) stickCool = 0f;
        if (i.use || i.hop || Kb.EnterDown()) Send(f);
        return true;
    }

    void Send(Frog f)
    {
        var r = RanchLife.I.robots[sel];
        string c = Cmds[cmdSel];
        if (c == "drive")
        {
            open = false;
            r.TakeManual(f);
            msg.text = "Driving " + r.robotName;
            return;
        }
        msg.text = r.Order(c, f);
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
            string bc = r.Charging ? "#8cff8c" : r.battery < Robot.LowBattery ? "#ffb030" : "#ffffff";
            rowStats[i].text = "<color=#9fd8ff>" + r.StatusLine + "</color>  <color=" + bc + ">" + (r.Charging ? "+" : "") + r.Pct + "</color>";
        }
        for (int i = 0; i < cmdBtns.Count; i++) cmdBtns[i].color = CmdColor(i, i == cmdSel);
        // live POV from the selected robot, beside the panel (left of it in landscape, above it in portrait)
        var rb = robots[sel];
        pov.transform.position = rb.eye.position;
        pov.transform.rotation = rb.eye.rotation;
        var corners = new Vector3[4];
        panel.rectTransform.GetWorldCorners(corners);   // overlay canvas: world = screen pixels
        float sw = Screen.width, sh = Screen.height;
        float left = corners[0].x / sw, top = corners[1].y / sh, bottom = corners[0].y / sh;
        float w = 0.24f, h = w * sw / sh * 0.62f;
        if (left > w + 0.03f) pov.rect = new Rect(left - w - 0.01f, Mathf.Clamp(top - h, 0f, 1f - h), w, h);
        else { w = Mathf.Min(0.9f, left + (1f - left) * 0.9f); h = Mathf.Min(1f - top - 0.01f, w * sw / sh * 0.56f); pov.rect = h > 0.06f ? new Rect(1f - w - 0.02f, top + 0.005f, w, h) : new Rect(0.02f, 0.72f, 0.3f, 0.25f); }
    }
}
