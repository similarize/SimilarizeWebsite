using System.Collections.Generic;
using UnityEngine;

// ffu35 STOCK TRAILER (Bill: a horse / cattle trailer). A bumper-pull aluminium livestock trailer parked just outside the
// pasture's east fence, ramp-gate toward the pasture. It is an ffu31 Trailer (same hitch, tow physics and watchdogs: back a
// truck up to its tongue, "E / Y / tap: Hitch"), plus this component:
//  * slatted sides + a slatted roof, so you can see who is riding inside; a rear gate that swings down into a ramp;
//  * loading: walk up to the gate - "A / E: Open the gate", then "A / E: Load the nearest cow / horse" (it walks itself up
//    the ramp), or "A / E: Lead the cow" next to a cow (it follows you; walk it to the ramp and it goes in), or ride a
//    horse to the gate - "A / E: Load the horse" (you hop off, the horse walks in). Room: 3 rows - a horse takes a row, two
//    cows share one (4 cows, or 3 horses, or a mix). "A / E: Close the gate" at the latch on the left rear corner; the
//    gate also closes itself when the truck pulls away;
//  * tow it anywhere on the ranch; open the gate and the animals walk down and stay there: their wander area (and a horse's
//    home) moves to the drop spot - unless you drop them back in the pasture.
// Demo: ?ffdemo=1&ffshot=stocktrailer (GameFfu35.cs).
public class LivestockTrailer : MonoBehaviour
{
    public static LivestockTrailer I;
    public static readonly Trailer.Spec Spec = new Trailer.Spec
    {
        name = "Stock Trailer (cows + horses)", shortName = "Stock Trailer", style = 4, len = 7.4f, wid = 2.35f, hgt = 2.45f, floorY = 0.66f,
        mass = 1900f, wheelR = 0.33f, axleZ = -0.5f, tongue = 1.7f, tandem = true,
        main = new Color(0.8f, 0.82f, 0.84f), accent = new Color(0.55f, 0.1f, 0.1f)
    };
    public const float RampLen = 2.1f;
    public const int Rows = 3;

    public Trailer tr;
    public float gateK;               // 0 closed (standing up), 1 down (ramp)
    float gateWant, gateAng = 20f, deck;
    Transform hinge;
    Hotspot gateSpot, latchSpot;

    public class Rider { public Animal a; public Horse h; public int row; public float x; public bool inside; }
    public readonly List<Rider> riders = new List<Rider>();     // aboard or walking in
    class Walker
    {
        public Rider r; public Animal a; public Horse h;
        public readonly List<Vector3> pts = new List<Vector3>();
        public readonly List<bool> ground = new List<bool>();
        public int seg; public float u, wait, speed, phase, yaw, totalT;
        public bool unload; public Vector3 drop; public int waitGateAt = -1;
    }
    readonly List<Walker> walkers = new List<Walker>();
    readonly Dictionary<Animal, Hotspot> leadSpots = new Dictionary<Animal, Hotspot>();
    readonly Dictionary<Animal, Frog> leading = new Dictionary<Animal, Frog>();
    readonly Dictionary<Animal, float> leadT = new Dictionary<Animal, float>();
    readonly Dictionary<Horse, List<Collider>> horseCols = new Dictionary<Horse, List<Collider>>();
    Frog pendRider; Horse pendHorse;
    bool pendUnload;
    public static int Loads, Unloads, Leads;
    public Vector3 lastDrop;
    public int Aboard { get { int n = 0; foreach (var r in riders) if (r.inside) n++; return n; } }
    public bool Busy { get { return walkers.Count > 0; } }
    public bool GateOpen { get { return gateK > 0.97f; } }
    public bool GateClosed { get { return gateK < 0.02f && gateWant < 0.5f; } }

    // ---------------- boot + build ----------------
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var go = new GameObject("ffu35 stock trailer boot");
        go.AddComponent<Boot35>();
        DontDestroyOnLoad(go);
    }
    class Boot35 : MonoBehaviour
    {
        float t;
        void Update()
        {
            t += Time.unscaledDeltaTime;
            if (I != null) { Destroy(gameObject); return; }
            if (Bootstrap.I == null || Game.I == null || RanchLife.I == null || Animal.All.Count == 0) return;
            if (!TrailerBarn.Built && t < 20f) return;
            try { Build(); } catch (System.Exception e) { Debug.LogError("ffu35 stock trailer: " + e); }
            Destroy(gameObject);
        }
    }

    public static void Build()
    {
        if (I != null) return;
        Rect P = RanchLife.Pasture;
        // just outside the east fence, south of the gate (the horses' walk-home path through the gate stays clear),
        // nose east so a truck backs up from the open ground; ramp-gate west toward the fence
        Vector2[] cands = { new Vector2(P.xMax + 13f, P.center.y - 14f), new Vector2(P.xMax + 13f, P.center.y + 15f), new Vector2(P.xMax + 17f, P.center.y - 20f) };
        Vector2 c = cands[0];
        foreach (var cc in cands) { if (SiteClear(cc)) { c = cc; break; } }
        ClearTrees(new Vector3(c.x + 1f, 0f, c.y));
        Vector3 pos = new Vector3(c.x, Ranch.GY(c.x, c.y) + 0.05f, c.y);
        Trailer t = BuildModel(Spec, pos, 90f);
        I = t.gameObject.AddComponent<LivestockTrailer>();
        I.tr = t;
        I.deck = Spec.floorY + 0.1f;
        I.hinge = t.transform.Find("GateHinge");
        I.Spots();
        Debug.Log("StockTrailer: built at " + pos.ToString("0.0") + " (pasture east fence x " + P.xMax + "), room " + Rows + " rows: 4 cows / 3 horses");
    }

    static bool SiteClear(Vector2 c)
    {
        foreach (var col in Physics.OverlapBox(new Vector3(c.x + 1f, Ranch.GY(c.x, c.y) + 2f, c.y), new Vector3(9f, 1.6f, 2.5f)))
        {
            if (col is TerrainCollider || col.isTrigger) continue;
            string n = col.transform.root.name;
            if (n.StartsWith("Tree") || n.StartsWith("Rock") || col.name.StartsWith("TreeCol") || col.name.StartsWith("RockCol") || n == "RanchStatic") continue;
            return false;
        }
        return true;
    }

    // same as Garden.ClearTrees (ffu32c): trees / rocks are Breakables drawn through BreakBatch
    static void ClearTrees(Vector3 c)
    {
        int n = 0;
        foreach (var col in Physics.OverlapBox(c + Vector3.up * 3f, new Vector3(12f, 6f, 4.5f)))
        {
            if (col is TerrainCollider || col.isTrigger || col.attachedRigidbody != null) continue;
            Transform t = col.transform;
            while (t.parent != null && t.parent.name != "RanchStatic" && t.parent.parent != null) t = t.parent;
            if (t.name.StartsWith("Tree") || t.name.StartsWith("Rock") || col.name.StartsWith("TreeCol") || col.name.StartsWith("RockCol"))
            {
                if (!t.gameObject.activeSelf) continue;
                var b = t.GetComponent<Breakable>();
                if (b != null) { b.state = 1; b.hp = 0f; BreakBatch.Dirty(b); }
                t.gameObject.SetActive(false); n++;
            }
        }
        if (n > 0) Debug.Log("StockTrailer: cleared " + n + " trees / rocks at the site");
    }

    static GameObject Box(Transform p, Vector3 pos, Vector3 size, Material m) { return Mats.Prim(PrimitiveType.Cube, p, pos, size, m); }

    public static Trailer BuildModel(Trailer.Spec s, Vector3 groundPos, float yaw)
    {
        var go = new GameObject("Trailer " + s.shortName);
        go.transform.SetPositionAndRotation(groundPos, Quaternion.Euler(0f, yaw, 0f));
        Transform t = go.transform;
        Transform sh = Mats.Node(t, "ShellParts", Vector3.zero);
        Trailer tr = go.AddComponent<Trailer>();
        float L = s.len, W = s.wid, H = s.hgt, F = s.floorY, hz = L * 0.5f, hw = W * 0.5f;
        float deck = F + 0.1f;
        tr.deck = deck;
        Vector3 ball = new Vector3(0f, 0.5f, hz + s.tongue);
        Material alu = Mats.PBR(s.main, 0.7f, 0.8f), red = Mats.Paint(s.accent, 0.6f), dark = Mats.Lit(new Color(0.12f, 0.12f, 0.13f));
        Material floorM = Mats.Lit(new Color(0.28f, 0.24f, 0.2f)), steel = Mats.Steel(new Color(0.65f, 0.66f, 0.68f));
        // chassis + floor (rubber-mat brown), lower skirt panels in maroon, white pinstripe
        Box(sh, new Vector3(0f, F - 0.08f, 0f), new Vector3(W - 0.1f, 0.16f, L), dark);
        Box(sh, new Vector3(0f, F + 0.05f, 0f), new Vector3(W - 0.06f, 0.1f, L - 0.05f), floorM);
        for (int k = -1; k <= 1; k += 2)
        {
            Box(sh, new Vector3((hw - 0.02f) * k, F + 0.32f, 0.05f), new Vector3(0.04f, 0.56f, L - 0.1f), red);
            Box(sh, new Vector3((hw - 0.005f) * k, F + 0.56f, 0.05f), new Vector3(0.03f, 0.04f, L - 0.1f), Mats.Unlit(new Color(0.95f, 0.95f, 0.92f)));
            // slats: horizontal aluminium bars with open gaps (you can see the animals)
            for (float y = F + 0.78f; y < F + H - 0.05f; y += 0.3f)
                Box(sh, new Vector3((hw - 0.02f) * k, y, 0.05f), new Vector3(0.035f, 0.1f, L - 0.1f), alu);
            Box(sh, new Vector3((hw - 0.02f) * k, F + H, 0.05f), new Vector3(0.06f, 0.08f, L - 0.1f), alu);          // top rail
            for (float z = -hz + 0.06f; z <= hz - 0.05f; z += L / 6f)
                Box(sh, new Vector3((hw - 0.01f) * k, F + H * 0.5f + 0.05f, z), new Vector3(0.07f, H - 0.1f, 0.08f), alu);   // posts
            // rear red lights on the corner posts
            Box(sh, new Vector3((hw - 0.02f) * k, F + 0.4f, -hz - 0.02f), new Vector3(0.12f, 0.2f, 0.04f), Mats.Unlit(new Color(1f, 0.12f, 0.1f)));
            Box(sh, new Vector3((hw - 0.02f) * k, F + H - 0.1f, -hz - 0.02f), new Vector3(0.06f, 0.06f, 0.04f), Mats.Unlit(new Color(1f, 0.6f, 0.1f)));
        }
        // front: solid maroon nose with a slanted top cap + a small vent window; the roof is slatted too (light + a view in)
        Box(sh, new Vector3(0f, F + H * 0.5f + 0.05f, hz - 0.03f), new Vector3(W - 0.02f, H - 0.1f, 0.06f), red);
        var cap = Box(sh, new Vector3(0f, F + H + 0.02f, hz - 0.25f), new Vector3(W, 0.06f, 0.6f), red);
        cap.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
        Box(sh, new Vector3(0f, F + H - 0.55f, hz + 0.005f), new Vector3(1.1f, 0.35f, 0.02f), Mats.GlassTint(new Color(0.08f, 0.12f, 0.16f)));
        for (int i = -3; i <= 3; i++) Box(sh, new Vector3(i * 0.33f, F + H + 0.04f, -0.25f), new Vector3(0.17f, 0.04f, L - 0.6f), alu);
        for (float z = -hz + 0.1f; z <= hz - 0.5f; z += 1.2f) Box(sh, new Vector3(0f, F + H + 0.07f, z), new Vector3(W, 0.04f, 0.06f), alu);
        // rear frame around the gate opening
        Box(sh, new Vector3(0f, F + H - 0.04f, -hz + 0.02f), new Vector3(W, 0.1f, 0.06f), alu);
        // tongue: A-frame, coupler, ball, chain hint, propane-style tack box
        float fz = hz - 0.1f;
        for (int k = -1; k <= 1; k += 2)
        {
            Vector3 a = new Vector3(0.62f * k, 0.5f, fz), b = new Vector3(0.06f * k, 0.5f, ball.z - 0.35f);
            Vector3 mid = (a + b) * 0.5f, d = b - a;
            var arm = Box(sh, mid, new Vector3(0.1f, 0.12f, d.magnitude), dark);
            arm.transform.localRotation = Quaternion.Euler(0f, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 0f);
        }
        Box(sh, new Vector3(0f, 0.5f, ball.z - 0.18f), new Vector3(0.16f, 0.14f, 0.42f), dark);
        Mats.Prim(PrimitiveType.Sphere, sh, ball + new Vector3(0f, 0.06f, 0f), Vector3.one * 0.13f, steel);
        Box(sh, new Vector3(0f, 0.36f, ball.z - 0.3f), new Vector3(0.22f, 0.02f, 0.02f), steel);
        Box(sh, new Vector3(0f, 0.85f, hz + 0.55f), new Vector3(0.9f, 0.45f, 0.45f), red);
        Transform jk = Mats.Node(t, "Jack", ball + new Vector3(0.12f, 0.35f, -0.25f));
        Mats.Prim(PrimitiveType.Cylinder, jk, new Vector3(0f, -0.2f, 0f), new Vector3(0.07f, 0.2f, 0.07f), dark);
        Box(jk, new Vector3(0f, -0.4f, 0f), new Vector3(0.18f, 0.03f, 0.18f), dark);
        tr.jack = jk;
        // tandem wheels under the floor
        float wx = hw - 0.16f;
        foreach (float az in new[] { s.axleZ + 0.5f, s.axleZ - 0.5f })
            for (int k = -1; k <= 1; k += 2)
            {
                Vector3 cpos = new Vector3(wx * k, s.wheelR, az);
                Transform wn = Mats.Node(t, "TWheel", cpos);
                Mats.Prim(PrimitiveType.Cylinder, wn, Vector3.zero, new Vector3(s.wheelR * 2f, 0.12f, s.wheelR * 2f), new Vector3(0f, 0f, 90f), Mats.Lit(new Color(0.07f, 0.07f, 0.08f)));
                Mats.Prim(PrimitiveType.Cylinder, wn, Vector3.zero, new Vector3(s.wheelR * 1.2f, 0.125f, s.wheelR * 1.2f), new Vector3(0f, 0f, 90f), steel);
                tr.AddWheel(cpos, s.wheelR, wn);
            }
        // the gate / ramp: hinged at the rear floor edge; its mesh runs along hinge -z (+90 = standing up = closed)
        Transform hg = Mats.Node(t, "GateHinge", new Vector3(0f, deck, -hz));
        hg.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Box(hg, new Vector3(0f, 0.03f, -RampLen * 0.5f), new Vector3(W - 0.14f, 0.06f, RampLen), alu);
        for (float z = 0.25f; z < RampLen - 0.05f; z += 0.3f) Box(hg, new Vector3(0f, 0.075f, -z), new Vector3(W - 0.3f, 0.03f, 0.05f), dark);   // cleats
        for (int k = -1; k <= 1; k += 2) Box(hg, new Vector3((hw - 0.1f) * k, 0.07f, -RampLen * 0.5f), new Vector3(0.06f, 0.1f, RampLen), red);
        Box(hg, new Vector3(0f, -0.02f, -RampLen * 0.62f), new Vector3(W - 0.4f, 0.03f, 0.7f), red);                       // outside panel stripe
        Box(hg, new Vector3(hw - 0.3f, -0.06f, -RampLen * 0.55f), new Vector3(0.1f, 0.06f, 0.18f), steel);               // latch
        // body collider (the gate has none: animals and froggies walk the ramp kinematically)
        var col = new GameObject("TCol").AddComponent<BoxCollider>();
        col.transform.SetParent(t, false);
        col.center = new Vector3(0f, F + H * 0.5f, 0.05f); col.size = new Vector3(W, H, L - 0.1f);
        MeshMerge.Merge(sh, true);
        tr.Init(s, ball);
        return tr;
    }

    // ---------------- hotspots ----------------
    void Spots()
    {
        gateSpot = new Hotspot { pos = RampFoot(1.0f), radius = 3f, label = "" };
        gateSpot.enabled = f => f.vehicle == null && f.world == WorldId.Ranch && Upright && !Busy;
        gateSpot.dynLabel = GateLabel;
        gateSpot.act = GateAct;
        Interact.All.Add(gateSpot);
        latchSpot = new Hotspot { pos = LatchPos(), radius = 1.5f, label = "Close the gate" };
        latchSpot.enabled = f => f.vehicle == null && f.world == WorldId.Ranch && gateWant > 0.5f && !Busy;
        latchSpot.dynLabel = f => "Close the gate" + (Aboard > 0 ? " (" + Aboard + " aboard)" : "");
        latchSpot.act = f => CloseGate(f);
        Interact.All.Add(latchSpot);
        foreach (var a in Animal.All)
        {
            if (a == null || a.kind != Animal.Kind.Cow) continue;
            Animal cow = a;
            var h = new Hotspot { pos = cow.Pos, radius = 2.4f, label = "" };
            h.enabled = f => f.vehicle == null && f.world == WorldId.Ranch && cow.enabled && !IsAboard(cow) && (leading.ContainsKey(cow) || RoomFor(false));
            h.dynLabel = f => leading.ContainsKey(cow) ? "Stop leading the cow" : "Lead the cow to the Stock Trailer";
            h.act = f => { if (leading.ContainsKey(cow)) StopLead(cow, "let go"); else StartLead(cow, f); };
            Interact.All.Add(h);
            leadSpots[cow] = h;
        }
    }

    public bool Upright { get { return tr != null && tr.transform.up.y > 0.8f; } }
    public bool Parked { get { return tr.Speed < 1.2f && (!tr.Hitched || tr.tower == null || tr.tower.Speed < 1f); } }
    bool IsAboard(Animal a) { foreach (var r in riders) if (r.a == a) return true; return false; }

    string GateLabel(Frog f)
    {
        if (!Parked) return "Stop the truck to open the gate";
        if (gateWant < 0.5f) return Aboard > 0 ? "Open the gate - let the " + Aboard + " animal" + (Aboard > 1 ? "s" : "") + " out" : "Open the Stock Trailer gate";
        if (!GateOpen) return "Gate moving...";
        Animal n = NearestFree(f.transform.position, 40f);
        if (n != null && RoomFor(n.kind == Animal.Kind.Horse)) return "Load the nearest " + (n.kind == Animal.Kind.Horse ? "horse" : "cow") + " (" + Aboard + " aboard)";
        return "Close the gate" + (Aboard > 0 ? " (" + Aboard + " aboard)" : "") + (RoomFor(false) ? "" : " - full!");
    }

    void GateAct(Frog f)
    {
        if (!Parked) return;
        if (gateWant < 0.5f)
        {
            gateWant = 1f; pendUnload = Aboard > 0;
            Sfx.PlayAt(Sfx.Clank != null ? Sfx.Clank : Sfx.Thud, LatchPos(), 0.6f, 30f, 1.2f);
            if (f != null && f.human) f.Toast(pendUnload ? "Gate down - out you come!" : "Gate open! Bring cows or horses up the ramp", 2.6f);
            return;
        }
        if (!GateOpen) return;
        Animal n = NearestFree(f.transform.position, 40f);
        if (n != null && RoomFor(n.kind == Animal.Kind.Horse)) { StartLoad(n, f); return; }
        CloseGate(f);
    }

    public bool OpenGate(Frog f) { if (!Parked) return false; if (gateWant < 0.5f) GateAct(f); return true; }
    public void CloseGate(Frog f)
    {
        if (Busy) return;
        gateWant = 0f; pendUnload = false;
        Sfx.PlayAt(Sfx.Clank != null ? Sfx.Clank : Sfx.Thud, LatchPos(), 0.7f, 30f, 0.9f);
        if (f != null && f.human) f.Toast(Aboard > 0 ? "Gate shut! Hitch up a truck and take them anywhere" : "Gate shut", 2.6f);
        Debug.Log("StockTrailer: gate closed, " + Aboard + " aboard");
    }

    // ---------------- room ----------------
    // a horse takes a whole row, two cows share one
    public bool RoomFor(bool horse) { int dummyRow; float dummyX; return FindSlot(horse, out dummyRow, out dummyX); }
    bool FindSlot(bool horse, out int row, out float x)
    {
        row = -1; x = 0f;
        var used = new int[Rows]; var horseRow = new bool[Rows]; var cowX = new float[Rows];
        int cows = 0;
        foreach (var r in riders) { used[r.row]++; if (r.h != null) horseRow[r.row] = true; else { cowX[r.row] = r.x; cows++; } }
        if (!horse)
        {
            if (cows >= 4) return false;
            for (int i = 0; i < Rows; i++) if (used[i] == 1 && !horseRow[i]) { row = i; x = -cowX[i]; return true; }
        }
        for (int i = 0; i < Rows; i++) if (used[i] == 0) { row = i; x = horse ? 0f : 0.5f; return true; }
        return false;
    }
    Vector3 SlotLocal(int row, float x) { float zf = Spec.len * 0.5f - 0.2f; return new Vector3(x, deck, zf - 2.33f * (row + 0.5f)); }

    public Animal NearestFree(Vector3 p, float maxD)
    {
        Animal best = null; float bd = maxD * maxD;
        foreach (var a in Animal.All)
        {
            if (a == null || !a.enabled || (a.kind != Animal.Kind.Cow && a.kind != Animal.Kind.Horse) || IsAboard(a)) continue;
            Horse h = a.GetComponent<Horse>();
            if (h != null && (h.driver != null || h.stowed)) continue;
            float d = (a.Pos - p).sqrMagnitude;
            if (d < bd) { bd = d; best = a; }
        }
        return best;
    }

    // ---------------- positions ----------------
    public Vector3 RampFoot(float extra)
    {
        Vector3 back = -tr.transform.forward; back.y = 0f; back.Normalize();
        Vector3 p = tr.transform.TransformPoint(new Vector3(0f, 0f, -Spec.len * 0.5f)) + back * (RampLen * Mathf.Cos(gateAng * Mathf.Deg2Rad) + extra);
        p.y = Ground(p);
        return p;
    }
    Vector3 LatchPos()
    {
        Vector3 p = tr.transform.TransformPoint(new Vector3(-Spec.wid * 0.5f - 0.9f, 0f, -Spec.len * 0.5f + 0.4f));
        p.y = Ground(p);
        return p;
    }
    static float Ground(Vector3 p)
    {
        RaycastHit h;
        float gy = Ranch.GY(p.x, p.z);
        if (Physics.Raycast(new Vector3(p.x, gy + 2.5f, p.z), Vector3.down, out h, 5f, Trailer.WheelMask, QueryTriggerInteraction.Ignore)) gy = Mathf.Max(gy, h.point.y);
        return gy;
    }

    // ---------------- loading / unloading ----------------
    public bool StartLoad(Animal a, Frog by)
    {
        if (a == null || IsAboard(a) || !Upright) return false;
        Horse h = a.GetComponent<Horse>();
        if (h != null && (h.driver != null || h.stowed)) return false;
        int row; float x;
        if (!FindSlot(h != null, out row, out x)) { if (by != null && by.human) by.Toast("The Stock Trailer is full!", 2f); return false; }
        if (gateWant < 0.5f) { if (!Parked) return false; gateWant = 1f; }
        if (leading.ContainsKey(a)) StopLead(a, "loading");
        var r = new Rider { a = a, h = h, row = row, x = x };
        riders.Add(r);
        a.enabled = false;
        if (h != null) { h.stowed = true; HorseCols(h, false); }
        var w = new Walker { r = r, a = a, h = h, unload = false, speed = 1.6f, phase = Random.value * 6f, yaw = a.transform.eulerAngles.y };
        Vector3 foot = RampFoot(0.35f), approach = RampFoot(2.6f);
        Vector3 top = tr.transform.TransformPoint(new Vector3(0f, deck, -Spec.len * 0.5f + 0.5f));
        Vector3 slot = tr.transform.TransformPoint(SlotLocal(row, x));
        float far = (approach - a.Pos).magnitude;
        if (far > 14f) w.speed = 2.6f;
        w.pts.Add(a.Pos); w.ground.Add(true);
        if (far > 1.2f) { w.pts.Add(approach); w.ground.Add(true); }
        w.pts.Add(foot); w.ground.Add(false);
        w.waitGateAt = w.pts.Count - 1;      // waits at the ramp foot until the gate is down
        w.pts.Add(top); w.ground.Add(false);
        w.pts.Add(slot); w.ground.Add(false);
        walkers.Add(w);
        Sfx.PlayAt(Sfx.AnimalVoice(h != null ? "Horse" : (a.voice ?? "Cow")), a.Pos + Vector3.up, 0.7f, 40f, 1f);
        if (by != null && by.human) by.Toast(h != null ? "Walk on! The horse heads up the ramp" : "Moo! The cow heads up the ramp", 2.2f);
        Debug.Log("StockTrailer: loading " + (h != null ? "horse " + h.index : "cow") + " from " + a.Pos.ToString("0") + " -> row " + row + " x " + x.ToString("0.0") + " (" + riders.Count + " riders)");
        return true;
    }

    void StartUnloadAll()
    {
        Vector3 back = -tr.transform.forward; back.y = 0f; back.Normalize();
        Vector3 side = new Vector3(back.z, 0f, -back.x);
        Vector3 drop = RampFoot(6f);
        lastDrop = drop;
        Vector3 top = tr.transform.TransformPoint(new Vector3(0f, deck, -Spec.len * 0.5f + 0.5f));
        int k = 0;
        // rear rows walk out first
        var order = new List<Rider>(riders);
        order.Sort((p, q) => q.row.CompareTo(p.row));
        foreach (var r in order)
        {
            if (!r.inside) continue;
            r.inside = false;
            r.a.transform.SetParent(null, true);
            var w = new Walker { r = r, a = r.a, h = r.h, unload = true, speed = 1.5f, wait = k * 1.1f, phase = Random.value * 6f, yaw = r.a.transform.eulerAngles.y, drop = drop };
            Vector3 spread = drop + side * ((k % 3) - 1) * 3.2f + back * ((k / 3) * 2.8f + Random.Range(-0.6f, 0.6f));
            spread.y = Ground(spread);
            w.pts.Add(r.a.transform.position); w.ground.Add(false);
            w.pts.Add(top); w.ground.Add(false);
            w.pts.Add(RampFoot(0.35f)); w.ground.Add(true);
            w.pts.Add(spread); w.ground.Add(true);
            walkers.Add(w);
            k++;
        }
        Debug.Log("StockTrailer: unloading " + k + " animals at " + drop.ToString("0"));
    }

    void HorseCols(Horse h, bool on)
    {
        if (!on)
        {
            var list = new List<Collider>();
            foreach (var c in h.GetComponentsInChildren<Collider>()) if (c.enabled && !(c is CharacterController) && c.GetComponentInParent<Frog>() == null) { c.enabled = false; list.Add(c); }
            horseCols[h] = list;
        }
        else
        {
            List<Collider> list;
            if (horseCols.TryGetValue(h, out list)) { foreach (var c in list) if (c != null) c.enabled = true; horseCols.Remove(h); }
        }
    }

    void Finish(Walker w)
    {
        if (!w.unload)
        {
            w.r.inside = true;
            w.a.transform.SetParent(tr.transform, true);
            w.a.transform.localPosition = SlotLocal(w.r.row, w.r.x);
            w.a.transform.localRotation = Quaternion.identity;
            Loads++;
            int n = Aboard;
            Frog near = NearestHuman(w.a.Pos, 30f);
            if (near != null) near.Toast(RoomFor(false) || RoomFor(true) ? n + " aboard - load more, or close the gate" : "All aboard! Close the gate and hitch up a truck", 2.6f);
            Debug.Log("StockTrailer: " + (w.h != null ? "horse " + w.h.index : "cow") + " aboard (row " + w.r.row + "), " + n + " aboard");
            return;
        }
        Release(w.r, w.drop);
    }

    void Release(Rider r, Vector3 drop)
    {
        riders.Remove(r);
        Animal a = r.a;
        if (a.transform.parent != null) a.transform.SetParent(null, true);
        Vector3 p = a.transform.position; p.y = Ground(p); a.transform.position = p;
        a.transform.rotation = Quaternion.Euler(0f, a.transform.eulerAngles.y, 0f);
        Rect P = RanchLife.Pasture;
        bool inPasture = P.Contains(new Vector2(drop.x, drop.z));
        Rect area;
        if (inPasture) area = new Rect(P.xMin + 1.5f, P.yMin + 1.5f, P.width - 3f, P.height - 3f);
        else
        {
            float lim = Layout.Half - 6f;
            float x0 = Mathf.Clamp(drop.x - 9f, -lim, lim - 18f), z0 = Mathf.Clamp(drop.z - 9f, -lim, lim - 18f);
            area = new Rect(x0, z0, 18f, 18f);
        }
        p.x = Mathf.Clamp(p.x, area.xMin + 0.4f, area.xMax - 0.4f); p.z = Mathf.Clamp(p.z, area.yMin + 0.4f, area.yMax - 0.4f);
        p.y = Ground(p); a.transform.position = p;
        a.groundFn = Ranch.GY;
        a.area = area; a.follow = null; a.holdPosition = false; a.frozen = false; a.skittish = true;
        if (r.h != null) { r.h.stowed = false; HorseCols(r.h, true); r.h.SetHome(area, inPasture); }
        a.SyncYaw();
        a.enabled = true;
        Unloads++;
        Debug.Log("StockTrailer: " + (r.h != null ? "horse " + r.h.index : "cow") + " out at " + p.ToString("0") + " - grazes around " + area.center.ToString("0") + (inPasture ? " (pasture)" : " (new home)"));
    }

    static Frog NearestHuman(Vector3 p, float maxD)
    {
        if (Game.I == null) return null;
        Frog best = null; float bd = maxD * maxD;
        foreach (var f in Game.I.frogs) if (f != null && f.human && (f.transform.position - p).sqrMagnitude < bd) { bd = (f.transform.position - p).sqrMagnitude; best = f; }
        return best;
    }

    // ---------------- leading a cow ----------------
    public void StartLead(Animal cow, Frog f)
    {
        if (cow == null || f == null) return;
        foreach (var kv in new List<KeyValuePair<Animal, Frog>>(leading)) if (kv.Value == f) StopLead(kv.Key, "switched");
        leading[cow] = f; leadT[cow] = 0f;
        float lim = Layout.Half - 3f;
        cow.area = new Rect(-lim, -lim, lim * 2f, lim * 2f);
        cow.skittish = false; cow.follow = f.transform; cow.followDist = 2.4f; cow.holdPosition = false;
        Leads++;
        if (f.human) f.Toast("The cow follows you - walk it to the Stock Trailer gate", 2.8f);
        Sfx.PlayAt(Sfx.AnimalVoice(cow.voice ?? "Cow"), cow.Pos + Vector3.up, 0.6f, 40f, 1.05f);
        Debug.Log("StockTrailer: " + f.nick + " leads a cow from " + cow.Pos.ToString("0"));
    }

    public void StopLead(Animal cow, string why)
    {
        Frog f; if (!leading.TryGetValue(cow, out f)) return;
        leading.Remove(cow); leadT.Remove(cow);
        cow.follow = null;
        if (why != "loading")
        {
            Vector3 p = cow.Pos;
            Rect P = RanchLife.Pasture;
            cow.area = P.Contains(new Vector2(p.x, p.z)) ? new Rect(P.xMin + 1.5f, P.yMin + 1.5f, P.width - 3f, P.height - 3f) : new Rect(p.x - 8f, p.z - 8f, 16f, 16f);
            cow.skittish = true;
            cow.SyncYaw();
        }
        Debug.Log("StockTrailer: stopped leading a cow (" + why + ")");
    }

    // ---------------- ridden horse at the gate (TrailerTow.DriverTick hook) ----------------
    public static bool DriverTick(Frog f, PIn input)
    {
        if (I == null || f == null) return false;
        Horse h = f.vehicle as Horse;
        if (h == null) return false;
        return I.RiderTick(f, h, input);
    }

    bool RiderTick(Frog f, Horse h, PIn input)
    {
        Vector3 foot = RampFoot(1f);
        Vector3 d = h.transform.position - foot; d.y = 0f;
        float dist = d.magnitude;
        if (dist > 10f || pendRider != null) return false;
        string A = f.inputKind == InputKind.Keyboard ? "E" : "A";
        if (!Upright || !Parked) { f.prompt = "Stock Trailer - stop the truck first  |  " + h.HelpLine; return false; }
        if (!RoomFor(true)) { f.prompt = "The Stock Trailer is full  |  " + h.HelpLine; return false; }
        if (dist > 4.5f) { f.prompt = "Ride to the Stock Trailer gate to load your horse  |  " + h.HelpLine; return false; }
        f.prompt = A + ": Load the horse into the Stock Trailer";
        if (!input.use) return false;
        pendRider = f; pendHorse = h;   // the dismount happens next frame (Frog is mid-vehicle-update now)
        return true;
    }

    // ---------------- per frame ----------------
    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.1f);
        if (tr == null) return;
        // gate: closes itself when the truck pulls away (animals still walking are put where they were going)
        if (gateWant > 0.5f && !Parked && (tr.Speed > 1.5f || (tr.Hitched && tr.tower != null && tr.tower.Speed > 1.5f)))
        {
            for (int i = walkers.Count - 1; i >= 0; i--) { var w = walkers[i]; walkers.RemoveAt(i); Vector3 e = w.pts[w.pts.Count - 1]; if (w.unload) w.a.transform.position = e; Finish(w); }
            gateWant = 0f; pendUnload = false;
            Frog dr = tr.tower != null ? tr.tower.driver : null;
            if (dr != null && dr.human) dr.Toast("Stock Trailer gate closed - off we go!", 2.2f);
            Debug.Log("StockTrailer: gate auto-closed (moving), " + Aboard + " aboard");
        }
        float before = gateK;
        gateK = Mathf.MoveTowards(gateK, gateWant, dt / 1.3f);
        if (gateK != before && Random.value < 0.06f) Sfx.PlayAt(Sfx.Pick(Sfx.Servo), LatchPos(), 0.2f, 25f, 0.7f);
        // the ramp tip rests on whatever is under it
        if (hinge != null)
        {
            Vector3 hp = hinge.position, back = -tr.transform.forward;
            RaycastHit hh;
            Vector3 probe = hp + back * RampLen * 0.95f + Vector3.up * 1f;
            float gy = Physics.Raycast(probe, Vector3.down, out hh, 4f, Trailer.WheelMask, QueryTriggerInteraction.Ignore) ? hh.point.y : hp.y - deck;
            float drop = Mathf.Clamp(hp.y - gy - 0.03f, 0.05f, RampLen * 0.7f);
            gateAng = Mathf.Lerp(gateAng, Mathf.Asin(drop / RampLen) * Mathf.Rad2Deg, dt * 4f);
            hinge.localRotation = Quaternion.Euler(Mathf.Lerp(90f, -gateAng, Mathf.SmoothStep(0f, 1f, gateK)), 0f, 0f);
        }
        if (pendUnload && GateOpen) { pendUnload = false; StartUnloadAll(); }
        // hotspots follow the trailer
        if (gateSpot != null) gateSpot.pos = RampFoot(1.0f);
        if (latchSpot != null) latchSpot.pos = LatchPos();
        foreach (var kv in leadSpots) if (kv.Key != null) kv.Value.pos = kv.Key.Pos;
        // rolled over: everybody out, right here
        if (!Upright && tr.transform.up.y < 0.3f && riders.Count > 0)
        {
            walkers.Clear();
            foreach (var r in new List<Rider>(riders)) Release(r, r.a.transform.position);
            Debug.Log("StockTrailer: tipped over - animals let out");
        }
        // a ridden horse at the gate: hop off, the horse walks in
        if (pendRider != null)
        {
            Frog f = pendRider; Horse h = pendHorse; pendRider = null; pendHorse = null;
            if (f.vehicle == h) f.ExitVehicle();
            if (f.vehicle == null)
            {
                Vector3 side = tr.transform.TransformPoint(new Vector3(Spec.wid * 0.5f + 1.6f, 0f, -Spec.len * 0.5f - 1f));
                side.y = Ground(side) + 0.1f;
                f.Teleport(side);
                StartLoad(h.anim, f);
            }
        }
        Leads35(dt);
        Walk(dt);
        IdleInside();
    }

    void Leads35(float dt)
    {
        if (leading.Count == 0) return;
        Vector3 foot = RampFoot(0.5f);
        foreach (var kv in new List<KeyValuePair<Animal, Frog>>(leading))
        {
            Animal cow = kv.Key; Frog f = kv.Value;
            leadT[cow] += dt;
            Vector3 d = cow.Pos - foot; d.y = 0f;
            if (d.magnitude < 6f && RoomFor(false) && Parked && Upright) { StartLoad(cow, f); continue; }
            if (f == null || f.world != WorldId.Ranch || (f.transform.position - cow.Pos).magnitude > 60f || leadT[cow] > 240f) StopLead(cow, "too far / timed out");
        }
    }

    void Walk(float dt)
    {
        for (int i = walkers.Count - 1; i >= 0; i--)
        {
            Walker w = walkers[i];
            w.totalT += dt;
            if (w.wait > 0f) { w.wait -= dt; Legs(w, 0f, dt); continue; }
            if (w.unload && !GateOpen) { Legs(w, 0f, dt); continue; }
            if (!w.unload && w.seg >= w.waitGateAt && !GateOpen) { Legs(w, 0f, dt); continue; }
            Vector3 a = w.pts[w.seg], b = w.pts[w.seg + 1];
            Vector3 flat = b - a; flat.y = 0f;
            float len = Mathf.Max(0.05f, (b - a).magnitude);
            bool onRamp = !w.ground[w.seg] || !w.ground[w.seg + 1];
            float sp = onRamp ? 1.1f : w.speed;
            w.u += sp * dt / len;
            if (w.u >= 1f)
            {
                w.u = 0f; w.seg++;
                if (w.seg >= w.pts.Count - 1 || w.totalT > 45f)
                {
                    walkers.RemoveAt(i);
                    w.a.transform.position = w.pts[w.pts.Count - 1];
                    Finish(w);
                    continue;
                }
                a = w.pts[w.seg]; b = w.pts[w.seg + 1]; flat = b - a; flat.y = 0f;
            }
            Vector3 p = Vector3.Lerp(a, b, w.u);
            if (w.ground[w.seg] && w.ground[w.seg + 1]) p.y = Ground(p);
            if (flat.sqrMagnitude > 0.0004f)
            {
                float ty = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
                w.yaw = Mathf.MoveTowardsAngle(w.yaw, ty, 220f * dt);
            }
            float pitch = 0f;
            if (onRamp && flat.sqrMagnitude > 0.01f) pitch = -Mathf.Atan2(b.y - a.y, flat.magnitude) * Mathf.Rad2Deg;
            w.a.transform.SetPositionAndRotation(p, Quaternion.Euler(pitch * 0.8f, w.yaw, 0f));
            Legs(w, sp, dt);
        }
    }

    void Legs(Walker w, float s, float dt)
    {
        w.phase += dt * (w.h != null ? 6f : 4.5f) * Mathf.Clamp(s, 0.3f, 2.2f);
        var legs = w.a.legs;
        for (int i = 0; i < legs.Count; i++)
        {
            float ang = s > 0.05f ? Mathf.Sin(w.phase + (i % 2 == (i / 2) % 2 ? 0f : Mathf.PI)) * 28f : 0f;
            if (legs[i] != null) legs[i].localRotation = Quaternion.Euler(ang, 0f, 0f);
        }
        if (w.a.head != null) w.a.head.localRotation = Quaternion.Euler(Mathf.Sin(w.phase * 0.5f) * 6f + 4f, 0f, 0f);
    }

    // riders inside: stand still, the odd head turn / tail flick, swaying with the trailer
    void IdleInside()
    {
        float t = Time.time;
        foreach (var r in riders)
        {
            if (!r.inside || r.a == null) continue;
            float ph = r.row * 1.7f + r.x;
            foreach (var l in r.a.legs) if (l != null) l.localRotation = Quaternion.identity;
            if (r.a.head != null) r.a.head.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.8f + ph) * 8f + 6f, Mathf.Sin(t * 0.5f + ph) * 22f, 0f);
            if (r.a.tail != null) r.a.tail.localRotation = Quaternion.Euler(20f, Mathf.Sin(t * 3f + ph) * 25f, 0f);
            if (r.a.transform.parent != tr.transform) { r.a.transform.SetParent(tr.transform, true); r.a.transform.localPosition = SlotLocal(r.row, r.x); r.a.transform.localRotation = Quaternion.identity; }
        }
    }

    // demo / probe log line
    public string LogLine()
    {
        int cows = 0, horses = 0; foreach (var r in riders) if (r.inside) { if (r.h != null) horses++; else cows++; }
        return "gate " + gateK.ToString("0.00") + " aboard " + Aboard + " (cows " + cows + " horses " + horses + ") walking " + walkers.Count + " hitched " + tr.Hitched + " v " + tr.Speed.ToString("0.0") + " pos " + tr.transform.position.ToString("0") + " up " + tr.transform.up.y.ToString("0.00");
    }
}
