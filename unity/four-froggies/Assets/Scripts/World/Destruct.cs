using System.Collections.Generic;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

// ffu21 DESTRUCTION: every weapon blows things apart and everything comes back.
// - Breakable: one piece of the world (a house wall panel, a window, a roof section, furniture, a track segment / ramp / rail /
//   tyre / pillar / loop section, a tree, a rock, a flag). It has HP; when it breaks it hides, throws pooled physics chunks
//   in its own materials, smokes / burns for a while and after ~20-40 s it re-assembles (grows back from its pivot with
//   sparkles) once nobody is standing in it.
// - Intact static pieces are drawn through BreakBatch: merged per material per 60 m cell (like MeshMerge), so the
//   world costs the same draw calls as before; a break / rebuild re-merges just the touched cells.
// - Robots blow apart and respawn (RobotBreak), animals go POOF cartoon-style and pop back (AnimalBreak), vehicles use the
//   existing VehicleWreck, story mechs lose limbs and topple (StoryMech MechParts.cs).
// - Online: a break is sent as D|b|<id> (ids from kind + position, the same on every device); breaks are idempotent and the
//   regen timers run locally. Robots by name (D|r|name), mech limbs from the mech's authority device (D|m|hash|part).
public enum BreakKind { Wall, Window, Roof, Furniture, Track, TrackSurface, Tree, Rock, Flag }

public class Breakable : MonoBehaviour
{
    public BreakKind kind;
    public float maxHp = 20f, hp = 20f;
    public float regenMin = 22f, regenMax = 34f;
    public string id;
    public Bounds bounds;
    public int state;                 // 0 intact, 1 broken, 2 rebuilding
    public float timer, rebuildK;
    public readonly List<Breakable> linked = new List<Breakable>();   // break with this one (windows in a wall panel)
    public Breakable host;            // a window waits for its wall
    public readonly List<MeshRenderer> solo = new List<MeshRenderer>(), batched = new List<MeshRenderer>();
    public Collider[] cols;
    public Vector3 baseScale;
    public Material[] debrisMats;
    public bool smokes = true;
    public System.Action<Breakable> onState;   // track cells: re-cook the shared continuous collider
}

public class Destruct : MonoBehaviour
{
    public static Destruct I;
    public static readonly List<Breakable> All = new List<Breakable>();
    static readonly Dictionary<string, Breakable> byId = new Dictionary<string, Breakable>();
    static readonly List<Breakable> active = new List<Breakable>();
    public static int MaxChunks = 140;
    public static bool Low;                 // phones + Tesla browser
    public static float RegenScale = 1f;    // demos shorten the wait
    public static float RebuildTime = 1.6f;
    public static bool On = true;
    static bool homesDone;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern string FFRealUA();
#else
    static string FFRealUA() { return ""; }
#endif

    public static void Init()
    {
        if (I != null) return;
        var go = new GameObject("Destruct");
        I = go.AddComponent<Destruct>();
        string ua = "";
        try { ua = FFRealUA() ?? ""; } catch (System.Exception) { }
        bool tesla = ua.Contains("Tesla") || ua.Contains("QtCarBrowser");
        Low = Look.Mobile || tesla;
        MaxChunks = Look.Mobile ? 44 : tesla ? 70 : 150;
        Physics.IgnoreLayerCollision(DebrisLayer, DebrisLayer, true);
        Physics.IgnoreLayerCollision(DebrisLayer, Vehicle.VehicleLayer, true);
        Physics.IgnoreLayerCollision(DebrisLayer, Vehicle.FrogLayer, true);
        Physics.IgnoreLayerCollision(DebrisLayer, Vehicle.ProjectileLayer, true);
        Physics.IgnoreLayerCollision(DebrisLayer, Vehicle.PropLayer, true);
        Debug.Log("Destruct: on, chunk cap " + MaxChunks + (tesla ? " (Tesla UA)" : Look.Mobile ? " (mobile)" : ""));
    }

    // debris lives on Ignore Raycast: not ground for wheels / frogs / shots (Vehicle.GroundMask and MechShot skip layer 2),
    // and it only collides with the ground, track and buildings
    public const int DebrisLayer = 2;

    // ---------------- registration ----------------
    static readonly Dictionary<BreakKind, float[]> Tune = new Dictionary<BreakKind, float[]>
    {   // hp, regenMin, regenMax
        { BreakKind.Wall,         new[] { 24f, 24f, 36f } },
        { BreakKind.Window,       new[] { 4f,  20f, 28f } },
        { BreakKind.Roof,         new[] { 30f, 26f, 38f } },
        { BreakKind.Furniture,    new[] { 8f,  20f, 30f } },
        { BreakKind.Track,        new[] { 14f, 20f, 30f } },
        { BreakKind.TrackSurface, new[] { 30f, 24f, 34f } },
        { BreakKind.Tree,         new[] { 14f, 26f, 40f } },
        { BreakKind.Rock,         new[] { 28f, 24f, 36f } },
        { BreakKind.Flag,         new[] { 5f,  18f, 26f } },
    };

    public static Breakable Register(GameObject go, BreakKind kind, bool batch = true, float hpMul = 1f)
    {
        if (go == null) return null;
        var b = go.GetComponent<Breakable>();
        if (b == null) b = go.AddComponent<Breakable>();
        b.kind = kind;
        float[] t = Tune[kind];
        b.maxHp = b.hp = t[0] * hpMul; b.regenMin = t[1]; b.regenMax = t[2];
        b.baseScale = go.transform.localScale;
        b.cols = go.GetComponentsInChildren<Collider>(true);
        var rs = go.GetComponentsInChildren<MeshRenderer>(true);
        bool first = true;
        var mats = new List<Material>();
        foreach (var r in rs)
        {
            if (r == null) continue;
            if (first) { b.bounds = r.bounds; first = false; } else b.bounds.Encapsulate(r.bounds);
            if (r.sharedMaterial != null && !mats.Contains(r.sharedMaterial) && r.sharedMaterial != Mats.Glass) mats.Add(r.sharedMaterial);
        }
        foreach (var c in b.cols) if (c != null) { if (first) { b.bounds = c.bounds; first = false; } else b.bounds.Encapsulate(c.bounds); }
        if (first) b.bounds = new Bounds(go.transform.position, Vector3.one);
        b.debrisMats = mats.Count > 0 ? mats.ToArray() : new[] { Mats.Lit(Color.gray) };
        foreach (var r in rs)
        {
            if (r == null) continue;
            if (batch && BreakBatch.Add(b, r)) b.batched.Add(r); else b.solo.Add(r);
        }
        b.smokes = kind != BreakKind.Window && kind != BreakKind.Flag;
        // stable id: kind + position (decimetres), so the same piece has the same id on every device
        Vector3 c0 = b.bounds.center;
        string id = (int)kind + ":" + Mathf.RoundToInt(c0.x * 10f) + ":" + Mathf.RoundToInt(c0.y * 10f) + ":" + Mathf.RoundToInt(c0.z * 10f);
        string id0 = id; int dup = 1;
        while (byId.ContainsKey(id)) id = id0 + "#" + (dup++);
        b.id = id;
        byId[id] = b;
        All.Add(b);
        return b;
    }

    // ---------------- damage ----------------
    // every explosion (Boom.At) and stomp calls this: breaks pieces, robots and animals inside the radius
    public static void Blast(Vector3 p, float radius, float damage, Vehicle by)
    {
        if (!On || damage <= 0f) return;
        float r2 = radius * radius;
        for (int i = 0; i < All.Count; i++)
        {
            Breakable b = All[i];
            if (b == null || b.state != 0) continue;
            float d2 = b.bounds.SqrDistance(p);
            if (d2 > r2) continue;
            float k = 1f - Mathf.Sqrt(d2) / radius;
            float d = damage * (0.4f + 0.6f * k);
            b.hp -= d;
            if (b.hp <= 0f) Break(b, p, d, false);
            else if (b.kind != BreakKind.Window) FX.Smoke(b.bounds.ClosestPoint(p), 0.8f + d * 0.04f, new Color(0.35f, 0.33f, 0.3f, 0.6f));
        }
        if (RanchLife.I != null)
            foreach (var rb in RanchLife.I.robots)
            {
                if (rb == null || !rb.gameObject.activeInHierarchy) continue;
                // ffu20: never the phone's display robot, robots inside the mission ship / off on Mars or Callisto;
                // a robot driving a vehicle (hidden in a closed cab) still counts
                if (rb.displayOnly || rb.world != WorldId.Ranch || ((rb.frozen || rb.hidden) && rb.seatedIn == null)) continue;
                Vector3 c = rb.transform.position + Vector3.up * rb.height * 0.5f;
                float dist = Mathf.Max(0f, (c - p).magnitude - rb.height * 0.4f);
                if (dist > radius) continue;
                RobotBreak.Hit(rb, p, damage * (0.4f + 0.6f * (1f - dist / radius)), false);
            }
        for (int i = Animal.All.Count - 1; i >= 0; i--)
        {
            Animal a = Animal.All[i];
            if (a == null || !a.isActiveAndEnabled) continue;
            if ((a.Pos - p).sqrMagnitude > r2 * 1.1f) continue;
            AnimalBreak.Poof(a, p);
        }
    }

    public static void Break(Breakable b, Vector3 from, float power, bool fromNet)
    {
        if (b == null || b.state == 1) return;
        if (b.state == 2) { b.transform.localScale = b.baseScale; }
        b.state = 1;
        b.hp = 0f;
        b.timer = Random.Range(b.regenMin, b.regenMax) * RegenScale;
        foreach (var r in b.solo) if (r != null) r.enabled = false;
        foreach (var r in b.batched) if (r != null) r.enabled = false;
        if (b.batched.Count > 0) BreakBatch.Dirty(b);
        foreach (var c in b.cols) if (c != null) c.enabled = false;
        if (!active.Contains(b)) active.Add(b);
        if (b.onState != null) b.onState(b);
        BreakFx(b, from, power);
        foreach (var l in b.linked) if (l != null && l.state != 1) Break(l, from, power, true);
        if (!fromNet) SendNet("b|" + b.id);
    }

    static void BreakFx(Breakable b, Vector3 from, float power)
    {
        Vector3 c = b.bounds.center, s = b.bounds.size;
        float size = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
        float camD = NearestCamDist(c);
        bool far = camD > 160f;
        int n;
        switch (b.kind)
        {
            case BreakKind.Window: n = 7; break;
            case BreakKind.Flag: n = 3; break;
            case BreakKind.Furniture: n = 5; break;
            case BreakKind.Tree: n = 7; break;
            case BreakKind.TrackSurface: n = 12; break;
            default: n = Mathf.Clamp(Mathf.RoundToInt(size * 1.6f), 4, 10); break;
        }
        if (Low) n = Mathf.Max(2, n / 2);
        if (far) n = Mathf.Min(n, 2);
        float chunk = Mathf.Clamp(Mathf.Pow(s.x * s.y * s.z / Mathf.Max(1, n), 1f / 3f) * 0.75f, 0.12f, 2.6f);
        for (int i = 0; i < n; i++)
        {
            Vector3 q = new Vector3(c.x + Random.Range(-0.45f, 0.45f) * s.x, c.y + Random.Range(-0.45f, 0.45f) * s.y, c.z + Random.Range(-0.45f, 0.45f) * s.z);
            Vector3 dir = (q - from); dir.y = Mathf.Abs(dir.y) + 0.4f;
            Vector3 v = dir.normalized * Random.Range(5f, 11f) + Random.insideUnitSphere * 3f + Vector3.up * Random.Range(2f, 6f);
            Material m = b.debrisMats[Random.Range(0, b.debrisMats.Length)];
            Vector3 sz = b.kind == BreakKind.Window ? new Vector3(chunk * 1.3f, chunk * 1.1f, 0.05f)
                       : new Vector3(chunk * Random.Range(0.6f, 1.2f), chunk * Random.Range(0.5f, 1.1f), chunk * Random.Range(0.6f, 1.2f));
            Chunks.Spawn(q, v, sz, b.kind == BreakKind.Window ? GlassShard : m, far ? 3f : Random.Range(5f, 7.5f));
        }
        if (far) { FX.Smoke(c, size * 0.5f, new Color(0.4f, 0.38f, 0.35f, 0.7f)); return; }
        switch (b.kind)
        {
            case BreakKind.Window:
                FX.Sparkle(c, new Color(0.8f, 0.95f, 1f), 14);
                Sfx.PlayAt(Sfx.Pick(Sfx.Crash) ?? Sfx.Clank, c, 0.6f, 70f, 1.7f);
                break;
            case BreakKind.Tree:
                FX.Cloud(c + Vector3.up * s.y * 0.2f, Mathf.Clamp(size * 0.35f, 1f, 3f), 10, new Color(0.35f, 0.55f, 0.22f, 0.8f));
                FX.Cloud(b.bounds.min + new Vector3(s.x * 0.5f, 0.5f, s.z * 0.5f), 1.2f, 6, new Color(0.45f, 0.38f, 0.3f, 0.7f));
                Sfx.PlayAt(Sfx.Bonk ?? Sfx.Thud, c, 0.8f, 90f, 0.6f);
                break;
            case BreakKind.Flag:
                FX.Sparkle(c, Color.white, 8);
                break;
            default:
                FX.Cloud(c, Mathf.Clamp(size * 0.3f, 0.8f, 4f), Low ? 6 : 12, new Color(0.62f, 0.58f, 0.52f, 0.75f));
                Sfx.PlayAt(Sfx.Pick(Sfx.Crash) ?? Sfx.Boom, c, 0.85f, 120f, Random.Range(0.7f, 0.9f));
                break;
        }
    }
    static Material glassShard;
    static Material GlassShard { get { if (glassShard == null) glassShard = Mats.Shiny(new Color(0.7f, 0.85f, 0.95f)); return glassShard; } }

    // mech feet / a toppling mech crush what they land on
    public static void Stomp(Vector3 p, float radius, float damage, Vehicle by) { Blast(p, radius, damage, by); }

    // ---------------- per frame: burning wrecks, regeneration ----------------
    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.1f);
        if (!homesDone && RanchLife.I != null && RanchLife.I.robots.Count > 0) { homesDone = true; RobotBreak.RememberHomes(); }
        Chunks.Tick(dt);
        int smokeBudget = Low ? 2 : 5;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            Breakable b = active[i];
            if (b == null) { active.RemoveAt(i); continue; }
            if (b.state == 1)
            {
                b.timer -= dt;
                float age = (b.regenMin * RegenScale) - b.timer;
                if (b.smokes && smokeBudget > 0 && Random.value < dt * 3f && NearestCamDist(b.bounds.center) < 140f)
                {
                    smokeBudget--;
                    Vector3 sp = new Vector3(b.bounds.center.x, b.bounds.min.y + 0.4f, b.bounds.center.z) + Random.insideUnitSphere * 0.6f;
                    FX.Smoke(sp, 1.2f + b.bounds.size.magnitude * 0.08f, new Color(0.2f, 0.2f, 0.2f, 0.55f));
                    if (b.timer > 6f && b.kind != BreakKind.Rock && Random.value < 0.7f) FX.Flame(sp, Vector3.up);
                }
                if (b.timer > 0f) continue;
                if (b.host != null && b.host.state == 1) { b.timer = 1f; continue; }   // a window waits for its wall
                if (Occupied(b)) { b.timer = 1.2f; continue; }
                StartRebuild(b);
                foreach (var l in b.linked) if (l != null && l.state == 1) StartRebuild(l);
            }
            else if (b.state == 2)
            {
                b.rebuildK += dt / RebuildTime;
                float k = Mathf.Clamp01(b.rebuildK);
                float e = EaseOutBack(k);
                b.transform.localScale = b.baseScale * Mathf.Max(0.01f, e);
                if (Random.value < dt * 14f)
                {
                    Vector3 sp = new Vector3(Random.Range(b.bounds.min.x, b.bounds.max.x), Random.Range(b.bounds.min.y, b.bounds.max.y), Random.Range(b.bounds.min.z, b.bounds.max.z));
                    FX.Sparkle(sp, new Color(0.6f, 0.95f, 1f), 2);
                }
                if (k >= 1f) FinishRebuild(b, i);
            }
        }
    }

    static void StartRebuild(Breakable b)
    {
        b.state = 2; b.rebuildK = 0f;
        b.transform.localScale = b.baseScale * 0.01f;
        foreach (var r in b.solo) if (r != null) r.enabled = true;
        foreach (var r in b.batched) if (r != null) r.enabled = true;
        if (NearestCamDist(b.bounds.center) < 120f)
        {
            FX.Sparkle(b.bounds.center, new Color(0.55f, 0.95f, 1f), 12);
            Sfx.PlayAt(Sfx.Pick(Sfx.Servo) ?? Sfx.Pickup, b.bounds.center, 0.45f, 60f, 1.3f);
        }
    }

    static void FinishRebuild(Breakable b, int idx)
    {
        b.state = 0; b.hp = b.maxHp;
        b.transform.localScale = b.baseScale;
        foreach (var c in b.cols) if (c != null) c.enabled = true;
        foreach (var r in b.batched) if (r != null) r.enabled = false;
        if (b.batched.Count > 0) BreakBatch.Dirty(b);
        if (b.onState != null) b.onState(b);
        if (idx >= 0 && idx < active.Count && active[idx] == b) active.RemoveAt(idx); else active.Remove(b);
        if (NearestCamDist(b.bounds.center) < 120f) FX.Sparkle(b.bounds.center, new Color(1f, 1f, 0.75f), 8);
    }

    // don't rebuild a wall around a froggy / vehicle / robot standing in its space
    static readonly Collider[] occ = new Collider[8];
    static bool Occupied(Breakable b)
    {
        if (b.cols == null || b.cols.Length == 0) return false;
        int n = Physics.OverlapBoxNonAlloc(b.bounds.center, b.bounds.extents * 0.95f, occ, Quaternion.identity, (1 << Vehicle.FrogLayer) | (1 << Vehicle.VehicleLayer), QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            if (occ[i] == null) continue;
            var sm = occ[i].GetComponentInParent<StoryMech>();
            if (sm != null) continue;      // giant mechs stand over everything
            return true;
        }
        return false;
    }

    void LateUpdate() { BreakBatch.Flush(); TrackColliders.Flush(); }

    static float EaseOutBack(float x) { const float c1 = 1.4f, c3 = c1 + 1f; float t = x - 1f; return 1f + c3 * t * t * t + c1 * t * t; }

    // ---------------- helpers ----------------
    public static float NearestCamDist(Vector3 p)
    {
        float best = 1e9f;
        if (Game.I != null)
            foreach (var s in Game.I.slots)
                if (s != null && s.cam != null && s.cam.isActiveAndEnabled) best = Mathf.Min(best, (s.cam.transform.position - p).magnitude);
        if (best > 1e8f && Camera.main != null) best = (Camera.main.transform.position - p).magnitude;
        return best > 1e8f ? 0f : best;
    }

    // camera shake that reaches far (a 160 m mech hitting the ground)
    public static void BigShake(Vector3 p, float power, float range)
    {
        if (Game.I == null) return;
        foreach (var s in Game.I.slots)
        {
            if (s == null || s.rig == null || s.cam == null) continue;
            float d = (s.cam.transform.position - p).magnitude;
            s.rig.AddShake(power * Mathf.Clamp01(1f - d / range));
        }
    }

    // ---------------- net ----------------
    public static void SendNet(string payload)
    {
        if (Net.I == null || !Net.I.Online) return;
        Net.I.SendDestruct(payload);
    }

    public static void OnNet(string[] p)
    {
        if (p.Length < 3) return;
        switch (p[1])
        {
            case "b":
                {
                    Breakable b;
                    string id = p[2];
                    for (int i = 3; i < p.Length; i++) id += "|" + p[i];
                    if (byId.TryGetValue(id, out b) && b.state != 1) Break(b, b.bounds.center + Vector3.up, 20f, true);
                    break;
                }
            case "r":
                if (RanchLife.I != null)
                    foreach (var r in RanchLife.I.robots)
                        if (r != null && r.robotName == p[2]) { RobotBreak.Hit(r, r.transform.position + Vector3.up, 999f, true); break; }
                break;
            case "m":
                if (p.Length >= 4)
                {
                    int h; int part;
                    if (!int.TryParse(p[2], out h) || !int.TryParse(p[3], out part)) break;
                    foreach (var m in StoryMech.AllMechs) if (m != null && m.Title.GetHashCode() == h) { m.NetLoseLimb(part); break; }
                }
                break;
        }
    }

    public static int Count(int state)
    {
        int n = 0;
        foreach (var b in All) if (b != null && b.state == state) n++;
        return n;
    }
}

// ---------------- pooled physics debris ----------------
public static class Chunks
{
    class C { public GameObject go; public Transform t; public Rigidbody rb; public MeshRenderer mr; public float life, life0; public Vector3 size; }
    static readonly List<C> pool = new List<C>();
    static readonly List<C> live = new List<C>();
    static Transform root;
    public static int Live { get { return live.Count; } }

    public static void Spawn(Vector3 p, Vector3 v, Vector3 size, Material m, float life)
    {
        if (Destruct.MaxChunks <= 0) return;
        C c;
        if (live.Count >= Destruct.MaxChunks) { c = live[0]; live.RemoveAt(0); }
        else if (pool.Count > 0) { c = pool[pool.Count - 1]; pool.RemoveAt(pool.Count - 1); }
        else c = Make();
        c.t.position = p;
        c.t.rotation = Random.rotation;
        c.t.localScale = size;
        c.size = size;
        c.mr.sharedMaterial = m;
        c.go.SetActive(true);
        c.rb.mass = Mathf.Clamp(size.x * size.y * size.z * 400f, 2f, 3000f);
        c.rb.velocity = v;
        c.rb.angularVelocity = Random.insideUnitSphere * 9f;
        c.life = c.life0 = life;
        live.Add(c);
    }

    static C Make()
    {
        if (root == null) root = new GameObject("Debris").transform;
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = "Chunk";
        g.layer = Destruct.DebrisLayer;
        g.transform.SetParent(root, false);
        var c = new C { go = g, t = g.transform, mr = g.GetComponent<MeshRenderer>() };
        c.mr.shadowCastingMode = Look.Mobile ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
        c.mr.receiveShadows = !Look.Mobile;
        c.rb = g.AddComponent<Rigidbody>();
        c.rb.drag = 0.15f; c.rb.angularDrag = 0.4f;
        c.rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        c.rb.interpolation = RigidbodyInterpolation.None;
        return c;
    }

    public static void Tick(float dt)
    {
        for (int i = live.Count - 1; i >= 0; i--)
        {
            C c = live[i];
            c.life -= dt;
            if (c.life < 0.7f) c.t.localScale = c.size * Mathf.Max(0.01f, c.life / 0.7f);
            if (c.life <= 0f || c.t.position.y < -200f)
            {
                c.go.SetActive(false);
                live.RemoveAt(i);
                pool.Add(c);
            }
        }
    }
}

// ---------------- batching of intact static pieces ----------------
// intact breakable pieces are merged per material per 60 m cell; a piece that breaks / rebuilds dirties its cells, which
// are re-merged at the end of the frame (LateUpdate), so the world draws like the old MeshMerge result
public static class BreakBatch
{
    class Cell
    {
        public Material mat; public bool shadows;
        public readonly List<MeshFilter> mfs = new List<MeshFilter>();
        public readonly List<Breakable> owners = new List<Breakable>();
        public GameObject go; public Mesh mesh; public bool dirty; public int layer;
    }
    static readonly Dictionary<Material, Dictionary<int, Cell>> cells = new Dictionary<Material, Dictionary<int, Cell>>();
    static readonly Dictionary<Breakable, List<Cell>> ofPiece = new Dictionary<Breakable, List<Cell>>();
    static readonly List<Cell> dirty = new List<Cell>();
    static Transform root;
    const float CellSize = 60f;

    public static bool Add(Breakable b, MeshRenderer r)
    {
        var mf = r.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable || r.sharedMaterial == null || r.sharedMaterials.Length > 1) return false;
        if (mf.sharedMesh.subMeshCount > 1) return false;
        Material m = r.sharedMaterial;
        Vector3 wp = b.bounds.center;
        int key = Mathf.FloorToInt((wp.x + 30000f) / CellSize) * 100000 + Mathf.FloorToInt((wp.z + 30000f) / CellSize);
        Dictionary<int, Cell> d;
        if (!cells.TryGetValue(m, out d)) { d = new Dictionary<int, Cell>(); cells[m] = d; }
        Cell c;
        if (!d.TryGetValue(key, out c)) { c = new Cell { mat = m, shadows = r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off, layer = r.gameObject.layer }; d[key] = c; }
        c.mfs.Add(mf); c.owners.Add(b);
        List<Cell> l;
        if (!ofPiece.TryGetValue(b, out l)) { l = new List<Cell>(); ofPiece[b] = l; }
        if (!l.Contains(c)) l.Add(c);
        r.enabled = false;
        if (!c.dirty) { c.dirty = true; dirty.Add(c); }
        return true;
    }

    public static void Dirty(Breakable b)
    {
        List<Cell> l;
        if (!ofPiece.TryGetValue(b, out l)) return;
        foreach (var c in l) if (!c.dirty) { c.dirty = true; dirty.Add(c); }
    }

    static readonly List<CombineInstance> tmp = new List<CombineInstance>();
    public static void Flush()
    {
        if (dirty.Count == 0) return;
        if (root == null) root = new GameObject("BreakBatch").transform;
        foreach (var c in dirty)
        {
            c.dirty = false;
            tmp.Clear();
            int verts = 0;
            for (int i = 0; i < c.mfs.Count; i++)
            {
                if (c.owners[i] == null || c.owners[i].state != 0 || c.mfs[i] == null) continue;
                var mesh = c.mfs[i].sharedMesh;
                tmp.Add(new CombineInstance { mesh = mesh, transform = c.mfs[i].transform.localToWorldMatrix });
                verts += mesh.vertexCount;
            }
            if (c.go == null)
            {
                c.go = new GameObject("Batch " + c.mat.name);
                c.go.layer = c.layer;
                c.go.transform.SetParent(root, false);
                c.go.AddComponent<MeshFilter>();
                var mr = c.go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = c.mat;
                mr.shadowCastingMode = c.shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                c.mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, name = "BreakBatch" };
                c.mesh.MarkDynamic();
                c.go.GetComponent<MeshFilter>().sharedMesh = c.mesh;
            }
            c.mesh.Clear();
            if (tmp.Count > 0) { c.mesh.CombineMeshes(tmp.ToArray(), true, true); c.mesh.RecalculateBounds(); }
            c.go.SetActive(tmp.Count > 0);
        }
        dirty.Clear();
    }

    public static int CellCount { get { int n = 0; foreach (var d in cells.Values) n += d.Count; return n; } }
}

// ---------------- robots: blow apart, rebuild at their spot ----------------
public class RobotBreak : MonoBehaviour
{
    public Robot r;
    public float hp = 22f;
    float downT = -1f, buildK = -1f;
    Vector3 home; float homeYaw;
    Vector3 baseScale;
    public static readonly Dictionary<Robot, RobotBreak> Of = new Dictionary<Robot, RobotBreak>();

    public static RobotBreak Get(Robot r)
    {
        RobotBreak b;
        if (Of.TryGetValue(r, out b) && b != null) return b;
        // the state lives on its own object: the robot itself is switched off while it is in pieces
        var go = new GameObject("RobotBreak " + r.robotName);
        b = go.AddComponent<RobotBreak>();
        b.r = r; b.home = r.transform.position; b.homeYaw = r.transform.eulerAngles.y; b.baseScale = r.transform.localScale;
        Of[r] = b;
        return b;
    }

    // call once the robots exist so each remembers where it started (respawn spot)
    public static void RememberHomes()
    {
        if (RanchLife.I == null) return;
        foreach (var r in RanchLife.I.robots) if (r != null) Get(r);
    }

    public static void Hit(Robot r, Vector3 from, float dmg, bool fromNet)
    {
        var b = Get(r);
        if (b.downT >= 0f || b.buildK >= 0f) return;
        b.hp -= dmg;
        if (b.hp > 0f && !fromNet) { FX.Sparkle(r.transform.position + Vector3.up * r.height * 0.6f, new Color(1f, 0.8f, 0.4f), 6); return; }
        b.BlowUp(from, fromNet);
    }

    void BlowUp(Vector3 from, bool fromNet)
    {
        Vector3 c = r.transform.position + Vector3.up * r.height * 0.5f;
        Frog drv = r.manual;
        // ffu20: out of the vehicle it was driving, and its space mission is scrubbed (before launch) or cut short (it
        // rebuilds at its spot and carries on home with what it has)
        if (r.seatedIn != null || r.drv != null) r.EndDrive("got blown up! Rebuilding...");
        if (r.mission != null) { string m = r.mission.Abort(); if (RobotPhone.I != null) RobotPhone.I.Say(m); }
        // release jacks / chores / the phone driver cleanly, then switch the robot off while it is in pieces
        r.DemoStart(r.transform.position, r.transform.eulerAngles.y, -1, r.battery);
        r.status = "blown up - rebuilding";
        if (drv != null) drv.Toast(r.robotName + " got blown to bits! It rebuilds itself in about 25 s", 3.5f);
        Material[] mats = Mats0(r);
        bool far = Destruct.NearestCamDist(c) > 160f;
        int n = far ? 2 : Destruct.Low ? 5 : 10;
        for (int i = 0; i < n; i++)
        {
            Vector3 q = c + Random.insideUnitSphere * r.height * 0.35f;
            Vector3 v = (q - from).normalized * Random.Range(6f, 12f) + Vector3.up * Random.Range(4f, 9f);
            float s = r.height * Random.Range(0.06f, 0.14f);
            Chunks.Spawn(q, v, new Vector3(s, s * Random.Range(0.8f, 2f), s), mats[Random.Range(0, mats.Length)], Random.Range(5f, 7f));
        }
        FX.Boom(c, Mathf.Clamp(r.height * 0.45f, 0.8f, 1.6f));
        FX.Sparkle(c, new Color(0.5f, 0.85f, 1f), 18);
        Sfx.PlayAt(Sfx.Pick(Sfx.Crash) ?? Sfx.Clank, c, 0.9f, 110f, 1.1f);
        r.gameObject.SetActive(false);
        downT = Random.Range(22f, 30f) * Destruct.RegenScale;
        if (!fromNet) Destruct.SendNet("r|" + r.robotName);
        Debug.Log("Destruct: robot " + r.robotName + " blown up");
    }

    static Material[] Mats0(Robot r)
    {
        var l = new List<Material>();
        foreach (var mr in r.GetComponentsInChildren<MeshRenderer>())
            if (mr.sharedMaterial != null && !l.Contains(mr.sharedMaterial)) { l.Add(mr.sharedMaterial); if (l.Count >= 4) break; }
        if (l.Count == 0) l.Add(Mats.Lit(new Color(0.7f, 0.7f, 0.72f)));
        return l.ToArray();
    }

    void Update()
    {
        if (r == null) { Destroy(gameObject); return; }
        float dt = Mathf.Min(Time.deltaTime, 0.1f);
        if (downT >= 0f)
        {
            downT -= dt;
            if (downT > 0f) return;
            downT = -1f;
            // rebuild at the spot it started from (its line on the lawn / its Mars post), assembling with sparkles
            Vector3 p = home;
            r.gameObject.SetActive(true);
            r.DemoStart(p, homeYaw, -1, Mathf.Max(r.battery, 0.5f));
            r.status = "rebuilt";
            buildK = 0f;
            r.transform.localScale = baseScale * 0.01f;
            FX.Sparkle(p + Vector3.up * r.height * 0.5f, new Color(0.55f, 0.95f, 1f), 20);
            Sfx.PlayAt(Sfx.Confirm ?? Sfx.Pickup, p, 0.6f, 70f, 1.1f);
        }
        if (buildK >= 0f)
        {
            buildK += dt / 1.8f;
            float k = Mathf.Clamp01(buildK);
            r.transform.localScale = baseScale * Mathf.Max(0.01f, 1f + 2.4f * Mathf.Pow(k - 1f, 3f) + 1.4f * Mathf.Pow(k - 1f, 2f));
            if (Random.value < dt * 12f) FX.Sparkle(r.transform.position + Vector3.up * Random.Range(0f, r.height), new Color(0.6f, 0.95f, 1f), 2);
            if (k >= 1f) { r.transform.localScale = baseScale; buildK = -1f; hp = 22f; }
        }
    }

    public bool Down { get { return downT >= 0f; } }
}

// ---------------- animals: cartoon POOF, pop back somewhere in their pen ----------------
public class AnimalBreak : MonoBehaviour
{
    Animal a;
    float downT = -1f, popK = -1f;
    Vector3 baseScale;
    Renderer[] rends;
    static readonly Color[] Confetti = { new Color(1f, 0.85f, 0.2f), new Color(1f, 0.45f, 0.6f), new Color(0.5f, 0.85f, 1f), Color.white };

    public static void Poof(Animal a, Vector3 from)
    {
        var b = a.GetComponent<AnimalBreak>();
        if (b == null) { b = a.gameObject.AddComponent<AnimalBreak>(); b.a = a; b.baseScale = a.transform.localScale; }
        if (b.downT >= 0f || b.popK >= 0f) return;
        Vector3 c = a.Pos + Vector3.up * 0.6f;
        // no gore: a white puff ball, stars and confetti, a squeaky voice
        FX.Cloud(c, 1.1f, Destruct.Low ? 8 : 14, new Color(1f, 1f, 1f, 0.9f));
        for (int i = 0; i < 4; i++) FX.Sparkle(c + Vector3.up * 0.4f, Confetti[i], 5);
        AudioClip v = Sfx.AnimalVoice(string.IsNullOrEmpty(a.voice) ? a.kind.ToString() : a.voice);
        Sfx.PlayAt(v ?? Sfx.Bonk ?? Sfx.Pickup, c, 0.8f, 60f, 1.6f);
        Sfx.PlayAt(Sfx.Pickup, c, 0.4f, 50f, 0.7f);
        b.rends = a.GetComponentsInChildren<Renderer>();
        foreach (var r in b.rends) if (r != null) r.enabled = false;
        a.enabled = false;
        b.downT = Random.Range(18f, 26f) * Destruct.RegenScale;
    }

    void Update()
    {
        if (a == null) return;
        float dt = Mathf.Min(Time.deltaTime, 0.1f);
        if (downT >= 0f)
        {
            downT -= dt;
            if (downT > 0f) return;
            downT = -1f;
            Rect r = a.area;
            float x = Random.Range(r.xMin + 0.6f, r.xMax - 0.6f), z = Random.Range(r.yMin + 0.6f, r.yMax - 0.6f);
            float y = a.groundFn != null ? a.groundFn(x, z) : a.floorY;
            if (r.width > 0.1f) a.transform.position = new Vector3(x, y, z);
            foreach (var rr in rends) if (rr != null) rr.enabled = true;
            a.enabled = true;
            popK = 0f;
            a.transform.localScale = baseScale * 0.01f;
            FX.Sparkle(a.Pos + Vector3.up * 0.5f, Color.white, 10);
            Sfx.PlayAt(Sfx.Pickup, a.Pos, 0.4f, 40f, 1.4f);
        }
        if (popK >= 0f)
        {
            popK += dt / 0.6f;
            float k = Mathf.Clamp01(popK);
            a.transform.localScale = baseScale * Mathf.Max(0.01f, 1f + 2.6f * Mathf.Pow(k - 1f, 3f) + 1.6f * Mathf.Pow(k - 1f, 2f));
            if (k >= 1f) { a.transform.localScale = baseScale; popK = -1f; }
        }
    }
}
