using System.Collections.Generic;
using UnityEngine;

// ffu47 CRITTER PALS (Bill: "animals in pens should interact with you like real animals would"). Uses the existing
// Animal code and species only, in every pen / enclosure that has them: the reptile room's lizard terrarium (heat lamp +
// basking rock, the house), the yard's Lizard terrace, Backyard animal yard (rabbits + cats), Yard dog run, the pasture
// (cows, horses, sheep / pig "goats"), the chicken coop, the house pet parlor's bunny nook. Named pets (Spotty, Tigy, Kitty,
// Little White Socks, Germy, Daisy) keep their own games (fetch / hide-and-seek) and are left out.
//  * Small, climb-inclined animals come over when a froggy (player OR AI) is in their pen or close (6 m), then climb on
//    along leg / back / shoulder points of the froggy's own body: LIZARDS run up the leg and side to a SHOULDER and sit
//    there (and play: scurry across the back to the other shoulder, head bobs, tail flicks); RABBITS crawl up a LEG and
//    hug it; CATS climb up the back and drape over the shoulders; CHICKENS flap-hop onto the back, then the HEAD. A lizard
//    in the raised terrarium hops out over the glass onto the froggy's side. Up to 6 riders' spots per froggy.
//  * Big animals never climb: COWS, HORSES, SHEEP / PIG and the dog-run DOGS walk up, face the froggy, nuzzle (head rub)
//    and LICK (a pink tongue flicks out at the froggy, a slurp, a little camera bump for players, a moo / neigh / baa / woof).
//  * Riders hop off (arc back into their pen) when the froggy sprints (SHIFT / pad B), jumps, gets into a vehicle, glides,
//    swims, leaves the pen (3 m past its fence), or after 20-60 s. A / E / tap A = shake them off. Then a 25-70 s cooldown.
//  * AI froggies get the same (they walk gently and don't hop while carrying). CritterPals.Paused / &pals=0 turn it off.
public class CritterPals : MonoBehaviour
{
    public static CritterPals I;
    public static bool Paused;
    public static float DtCap = 0.05f;
    public static int Climbs, Rides, Licks, Nuzzles, HopOffs;
    public static readonly Dictionary<string, int> OffWhy = new Dictionary<string, int>();
    public enum St { Approach, Climb, Ride, Hop, Nuzzle }
    public class Pal
    {
        public Animal a; public Frog f; public St st; public float t, dur, speed; public int slot;
        public List<Vector3> path = new List<Vector3>(); public List<bool> hop = new List<bool>(); public int seg; public float segT;
        public Vector3 from, to; public float hopDur, hopPeak; public string why = "";
        public Vector3 scale0; public float lickT, playT, nod; public Transform tongue; public bool raised; public float faceK;
    }
    public readonly List<Pal> pals = new List<Pal>();
    readonly Dictionary<Animal, float> cool = new Dictionary<Animal, float>();
    readonly Dictionary<Frog, float> lastY = new Dictionary<Frog, float>(), heightK = new Dictionary<Frog, float>();
    readonly Dictionary<Frog, Vector3> lastP = new Dictionary<Frog, Vector3>();
    static AudioClip slurp;

    public static void Ensure() { if (I == null) new GameObject("Critter Pals").AddComponent<CritterPals>(); }
    void Awake() { I = this; if (Game.UrlParamS("pals") == "0") Paused = true; }

    // ---------------- queries used by Frog ----------------
    public static int Riders(Frog f)
    {
        if (I == null || f == null) return 0;
        int n = 0;
        foreach (var p in I.pals) if (p.f == f && (p.st == St.Climb || p.st == St.Ride)) n++;
        return n;
    }
    public static string RiderLabel(Frog f)
    {
        var names = new List<string>();
        if (I != null) foreach (var p in I.pals) if (p.f == f && (p.st == St.Climb || p.st == St.Ride)) { string k = KindName(p.a.kind); if (!names.Contains(k)) names.Add(k); }
        if (names.Count == 0) return "";
        return "the " + string.Join(" + ", names.ToArray());
    }
    public static void ShakeOff(Frog f, string why)
    {
        if (I == null || f == null) return;
        foreach (var p in I.pals) if (p.f == f && (p.st == St.Climb || p.st == St.Ride || p.st == St.Approach || p.st == St.Nuzzle)) I.HopOff(p, why);
    }
    static string KindName(Animal.Kind k) { return k == Animal.Kind.Goat ? "sheep" : k.ToString().ToLowerInvariant(); }
    static bool Small(Animal.Kind k) { return k == Animal.Kind.Lizard || k == Animal.Kind.Rabbit || k == Animal.Kind.Cat || k == Animal.Kind.Chicken; }
    static bool Big(Animal.Kind k) { return k == Animal.Kind.Cow || k == Animal.Kind.Horse || k == Animal.Kind.Goat || k == Animal.Kind.Dog; }

    static bool FrogOk(Frog f)
    {
        return f != null && f.vehicle == null && f.passengerOf == null && f.remote == null && f.robotPilot == null && !f.netPuppet && !f.GlideOn
            && !f.Swimming && !f.launching && f.jetOf == null && !f.Climbing && f.model != null && f.model.gameObject.activeInHierarchy && f.gameObject.activeInHierarchy;
    }
    static bool AnimalOk(Animal a)
    {
        return a != null && a.enabled && a.gameObject.activeInHierarchy && a.petName.Length == 0 && !a.frozen && a.follow == null && !a.holdPosition && (Small(a.kind) || Big(a.kind));
    }
    static float Flat(Vector3 d) { d.y = 0f; return d.magnitude; }
    static float OutOfArea(Rect r, Vector3 p)
    {
        float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax), dz = Mathf.Max(r.yMin - p.z, 0f, p.z - r.yMax);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }
    Pal PalOf(Animal a) { foreach (var p in pals) if (p.a == a) return p; return null; }
    bool SlotFree(Frog f, int s) { foreach (var p in pals) if (p.f == f && p.slot == s && p.st != St.Hop) return false; return true; }
    int Count(Frog f, bool big) { int n = 0; foreach (var p in pals) if (p.f == f && p.st != St.Hop && Big(p.a.kind) == big) n++; return n; }

    // froggy body size (frogs ~1.15 m, the cats / dogs differ) - attach points scale with it
    float HK(Frog f)
    {
        float k;
        if (heightK.TryGetValue(f, out k)) return k;
        k = 1f;
        var rs = f.model.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0)
        {
            Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            k = Mathf.Clamp((b.max.y - f.transform.position.y) / 1.15f, 0.6f, 1.6f);
        }
        heightK[f] = k;
        return k;
    }

    // slots: 0 left shoulder, 1 right shoulder, 2 head, 3 back, 4 left leg, 5 right leg
    int PickSlot(Frog f, Animal.Kind k)
    {
        int[] order = k == Animal.Kind.Lizard ? new[] { 1, 0, 3 } : k == Animal.Kind.Rabbit ? new[] { 4, 5 } : k == Animal.Kind.Cat ? new[] { 3, 1, 0 } : new[] { 2, 3 };
        foreach (int s in order) if (SlotFree(f, s)) return s;
        return -1;
    }
    static Vector3 SlotPoint(int s, float hk)
    {
        Vector3 v;
        switch (s)
        {
            case 0: v = new Vector3(-0.43f, 0.95f, -0.08f); break;
            case 1: v = new Vector3(0.43f, 0.95f, -0.08f); break;
            case 2: v = new Vector3(0f, 1.2f, 0.02f); break;
            case 3: v = new Vector3(0f, 0.86f, -0.36f); break;
            case 4: v = new Vector3(-0.4f, 0.42f, 0.12f); break;
            default: v = new Vector3(0.4f, 0.42f, 0.12f); break;
        }
        return v * hk;
    }

    // the climb route over the froggy's body (froggy-local points) for this animal + slot
    void BuildPath(Pal p, float hk)
    {
        p.path.Clear(); p.hop.Clear(); p.seg = 0; p.segT = 0f;
        float s = p.slot == 0 || p.slot == 4 ? -1f : 1f;
        if (p.slot == 2 || p.slot == 3) s = Vector3.Dot(p.a.transform.position - p.f.transform.position, p.f.transform.right) < 0f ? -1f : 1f;
        Vector3 foot = new Vector3(0.34f * s, 0.04f, 0.34f), knee = new Vector3(0.38f * s, 0.32f, 0.2f), hip = new Vector3(0.42f * s, 0.56f, 0.02f);
        Vector3 side = new Vector3(0.44f * s, 0.74f, -0.14f), back = new Vector3(0.12f * s, 0.74f, -0.46f);
        Vector3 goal = SlotPoint(p.slot, 1f);
        System.Action<Vector3, bool> Add = (v, h) => { p.path.Add(v * hk); p.hop.Add(h); };
        Vector3 start = p.f.transform.InverseTransformPoint(p.a.transform.position);
        Add(start, false);
        switch (p.a.kind)
        {
            case Animal.Kind.Lizard:
                if (p.raised) { Add(hip, true); } else { Add(foot, false); Add(knee, false); Add(hip, false); }
                Add(side, false); if (p.slot == 3) Add(back, false); Add(goal, false); break;
            case Animal.Kind.Rabbit:
                Add(foot, false); Add(knee, false); Add(goal, false); break;
            case Animal.Kind.Cat:
                Add(new Vector3(0.3f * s, 0.05f, -0.5f), false); Add(new Vector3(0.3f * s, 0.42f, -0.48f), true); Add(back, false); Add(goal, false); break;
            default:   // chicken: flap-hop onto the back, then the head
                Add(back + new Vector3(0f, 0.08f, 0f), true); if (p.slot == 2) Add(goal, true); else Add(goal, false); break;
        }
        p.speed = p.a.kind == Animal.Kind.Lizard ? 0.9f : p.a.kind == Animal.Kind.Rabbit ? 0.55f : p.a.kind == Animal.Kind.Cat ? 1.0f : 0.8f;
    }

    void LateUpdate()   // after the froggies moved: riders stay glued to them
    {
        var g = Game.I;
        if (g == null || g.state != Game.State.Play) return;
        float dt = Mathf.Min(Time.deltaTime, DtCap);
        foreach (var k in new List<Animal>(cool.Keys)) cool[k] -= dt;
        // ---- tick pals ----
        for (int i = pals.Count - 1; i >= 0; i--)
        {
            var p = pals[i];
            if (p.a == null || !p.a.enabled || p.f == null) { if (p.a != null) Release(p, false); pals.RemoveAt(i); continue; }
            p.t += dt;
            if (p.st == St.Hop) { if (HopTick(p, dt)) { Release(p, true); pals.RemoveAt(i); } continue; }
            string why = Trigger(p);
            if (why != null) { HopOff(p, why); continue; }
            if (p.st == St.Approach) ApproachTick(p, dt);
            else if (p.st == St.Climb) ClimbTick(p, dt);
            else if (p.st == St.Ride) RideTick(p, dt);
            else if (p.st == St.Nuzzle) NuzzleTick(p, dt);
        }
        foreach (Frog f in g.frogs) if (f != null) { lastY[f] = f.transform.position.y; lastP[f] = f.transform.position; }
        if (Paused) return;
        // ---- scan a few animals per frame for a nearby froggy ----
        var all = Animal.All;
        for (int k = 0; k < all.Count; k++)
        {
            Animal a = all[k];
            if (!AnimalOk(a) || a.pal || a.Fleeing || PalOf(a) != null) continue;
            float c; if (cool.TryGetValue(a, out c) && c > 0f) continue;
            cool[a] = 0.8f;   // look around about once a second
            bool small = Small(a.kind);
            Vector3 ap = a.transform.position;
            Frog best = null; float bd = 1e9f;
            foreach (Frog f in g.frogs)
            {
                if (!FrogOk(f) || f.Sprinting || f.HSpeed > 5f) continue;
                Vector3 fp = f.transform.position;
                if (Mathf.Abs(fp.y - ap.y) > 3f) continue;
                float d = Flat(fp - ap), oa = OutOfArea(a.area, fp);
                bool interested = small ? (oa < 1.5f && d < 14f) || d < 6f : (oa < 2f && d < 16f) || d < 9f;
                if (interested && d < bd) { bd = d; best = f; }
            }
            if (best == null) continue;
            if (Random.value > (small ? 0.45f : 0.3f)) { cool[a] = Random.Range(1f, 3f); continue; }
            if (small)
            {
                int slot = PickSlot(best, a.kind);
                if (slot < 0) continue;
                Begin(a, best, St.Approach, slot);
            }
            else
            {
                if (Count(best, true) >= 2) continue;
                Begin(a, best, St.Approach, -1);
            }
        }
    }

    public Pal Begin(Animal a, Frog f, St st, int slot)
    {
        var p = new Pal { a = a, f = f, st = st, slot = slot, t = 0f, scale0 = a.transform.localScale };
        p.raised = a.Ground(a.transform.position.x, a.transform.position.z) > f.transform.position.y + 0.45f;
        a.pal = true;
        pals.Add(p);
        Debug.Log("CritterPals: a " + KindName(a.kind) + " notices " + f.nick + (f.human ? " (player)" : " (AI)") + " - " + (slot >= 0 ? "comes to climb on (slot " + slot + ")" : "comes over to say hello") + (p.raised ? " from its raised enclosure" : ""));
        return p;
    }

    // why this pal has to let go now (null = stays)
    string Trigger(Pal p)
    {
        Frog f = p.f;
        if (!f.gameObject.activeInHierarchy || f.vehicle != null || f.passengerOf != null) return "vehicle";
        if (f.GlideOn) return "glide";
        if (f.Swimming) return "swim";
        Vector3 fp = f.transform.position, lp;
        if (lastP.TryGetValue(f, out lp) && (fp - lp).magnitude > 15f) return "world";
        bool on = p.st == St.Climb || p.st == St.Ride;
        if (f.Sprinting && (on || p.st == St.Nuzzle)) return "sprint";
        float ly; if (lastY.TryGetValue(f, out ly) && on && (fp.y - ly) / Mathf.Max(Time.deltaTime, 0.001f) > 2.2f && !f.cc.isGrounded) return "jump";
        float oa = OutOfArea(p.a.area, fp);
        if (on && oa > (p.raised ? 3.5f : 3f)) return "left the pen";
        if (p.st == St.Ride && p.t > p.dur) return "time";
        if (p.st == St.Approach && (Flat(fp - p.a.transform.position) > 18f || p.t > 25f || f.Climbing)) return "lost interest";
        return null;
    }

    void ApproachTick(Pal p, float dt)
    {
        Animal a = p.a; Frog f = p.f;
        Vector3 ap = a.transform.position, fp = f.transform.position;
        bool big = Big(a.kind);
        Vector3 target;
        float stand = a.kind == Animal.Kind.Cow || a.kind == Animal.Kind.Horse ? 2.05f : a.kind == Animal.Kind.Goat ? 1.15f : 1.05f;
        if (big) { Vector3 d = ap - fp; d.y = 0f; if (d.sqrMagnitude < 0.01f) d = f.transform.forward; target = fp + d.normalized * stand; }
        else target = f.transform.TransformPoint(new Vector3((p.slot == 0 || p.slot == 4 ? -0.34f : 0.34f), 0f, 0.34f) * HK(f));
        // stay inside the pen (they come to the fence / glass if the froggy is outside it)
        Rect r = a.area;
        Vector3 tc = new Vector3(Mathf.Clamp(target.x, r.xMin + 0.2f, r.xMax - 0.2f), 0f, Mathf.Clamp(target.z, r.yMin + 0.2f, r.yMax - 0.2f));
        Vector3 to = tc - ap; to.y = 0f;
        float spd = big ? a.speed * 1.4f : Mathf.Max(a.runSpeed * 0.65f, 0.7f);
        float step = Mathf.Min(to.magnitude, spd * dt);
        if (to.sqrMagnitude > 0.0004f)
        {
            Vector3 np = ap + to.normalized * step;
            np.y = a.Ground(np.x, np.z);
            a.transform.position = np;
            a.transform.rotation = Quaternion.RotateTowards(a.transform.rotation, Quaternion.LookRotation(to.normalized, Vector3.up), 300f * dt);
        }
        a.PalAnimate(step / Mathf.Max(dt, 0.001f), dt);
        Vector3 cur = a.transform.position;
        if (big)
        {
            if (Flat(cur - target) < 0.35f || (Flat(tc - target) > 0.3f && Flat(cur - tc) < 0.3f && Flat(cur - fp) < stand + 1.2f))
            {
                p.st = St.Nuzzle; p.t = 0f; p.dur = Random.Range(4f, 7f); p.lickT = 0.6f; Nuzzles++;
                AudioClip c = Sfx.AnimalVoice(a.voice ?? a.kind.ToString());
                if (c != null) Sfx.PlayAt(c, cur + Vector3.up, 0.7f, 40f, Random.Range(0.95f, 1.05f));
                if (f.human) f.Toast("The " + KindName(a.kind) + " wants to say hello!", 2f);
                Debug.Log("CritterPals: the " + KindName(a.kind) + " nuzzles " + f.nick);
            }
            return;
        }
        // small: at the froggy's foot (or close enough to hop from a raised enclosure) -> climb
        Vector3 hipW = f.transform.TransformPoint(new Vector3(0.42f, 0.56f, 0f) * HK(f));
        bool reach = p.raised ? Flat(cur - fp) < 2.7f : Flat(cur - target) < 0.45f;
        if (reach)
        {
            p.st = St.Climb; p.t = 0f;
            p.a.transform.localScale = p.scale0 * (a.kind == Animal.Kind.Cat ? 0.7f : a.kind == Animal.Kind.Chicken ? 0.85f : 1f);
            BuildPath(p, HK(f)); Climbs++;
            Debug.Log("CritterPals: the " + KindName(a.kind) + " starts climbing " + f.nick + " -> slot " + p.slot + " (" + p.path.Count + " points" + (p.raised ? ", hops over the glass" : "") + ")");
            if (f.human && a.kind == Animal.Kind.Lizard) f.Toast("A lizard is climbing up you!", 2f);
            else if (f.human && a.kind == Animal.Kind.Rabbit) f.Toast("A bunny is climbing your leg!", 2f);
        }
    }

    void Pose(Pal p, Vector3 local, Vector3 dirLocal, float dt, bool snap)
    {
        Transform ft = p.f.transform;
        Vector3 w = ft.TransformPoint(local);
        Vector3 axis = ft.position + Vector3.up * local.y;
        Vector3 outW = w - axis; outW.y = 0f;
        float topK = Mathf.Clamp01((local.y / HK(p.f) - 0.8f) / 0.3f);
        if (p.slot == 3 || p.slot == 2) topK = Mathf.Max(topK, 0.7f);
        Vector3 up = Vector3.Slerp(outW.sqrMagnitude > 1e-4f ? outW.normalized : Vector3.up, Vector3.up, Mathf.Max(topK, local.y < 0.12f ? 1f : 0f));
        Vector3 fw = ft.TransformDirection(dirLocal);
        if (Vector3.Cross(fw, up).sqrMagnitude < 1e-4f) fw = ft.forward;
        Quaternion q = Quaternion.LookRotation(fw, up);
        p.a.transform.position = w;
        p.a.transform.rotation = snap ? q : Quaternion.Slerp(p.a.transform.rotation, q, Mathf.Min(1f, dt * 10f));
    }

    void ClimbTick(Pal p, float dt)
    {
        if (p.seg >= p.path.Count - 1) { p.st = St.Ride; p.t = 0f; p.dur = Random.Range(20f, 60f); p.playT = Random.Range(2f, 5f); Rides++; Debug.Log("CritterPals: the " + KindName(p.a.kind) + " is riding on " + p.f.nick + " (slot " + p.slot + ", " + p.dur.ToString("0") + " s)"); return; }
        Vector3 a0 = p.path[p.seg], a1 = p.path[p.seg + 1];
        // the start point follows the animal's spot in the world (the froggy moves)
        float len = Mathf.Max(0.05f, (a1 - a0).magnitude);
        bool hop = p.hop[p.seg + 1];
        float dur = hop ? 0.45f + len * 0.15f : len / p.speed;
        p.segT += dt / dur;
        float k = Mathf.Clamp01(p.segT);
        Vector3 pos = Vector3.Lerp(a0, a1, hop ? Mathf.SmoothStep(0f, 1f, k) : k);
        if (hop) pos.y += Mathf.Sin(k * Mathf.PI) * (0.35f + (p.raised && p.seg == 0 ? 1.3f : 0f));
        Vector3 dir = a1 - a0; if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
        Pose(p, pos, dir.normalized, dt, false);
        p.a.PalAnimate(hop ? 0f : p.speed * 1.5f, dt);
        if (p.a.kind == Animal.Kind.Chicken && hop) p.a.body.localScale = new Vector3(1f + Mathf.Sin(k * 20f) * 0.15f, 1f, 1f);   // flapping
        if (p.segT >= 1f) { p.seg++; p.segT = 0f; if (p.a.body != null) p.a.body.localScale = Vector3.one; }
    }

    void RideTick(Pal p, float dt)
    {
        float hk = HK(p.f);
        Vector3 pt = SlotPoint(p.slot, hk);
        Vector3 face;
        switch (p.slot)
        {
            case 4: case 5: face = new Vector3(p.slot == 4 ? 1f : -1f, 0.15f, 0f); break;   // hugging the leg, facing in
            case 3: face = new Vector3(1f, 0f, 0.2f); break;                                 // draped over the shoulders / back
            default: face = Vector3.forward; break;
        }
        // play: lizards scurry to the other shoulder now and then, everyone fidgets
        p.playT -= Time.deltaTime;
        float bob = 0f;
        if (p.playT <= 0f)
        {
            p.playT = Random.Range(3f, 7f);
            if (p.a.kind == Animal.Kind.Lizard && (p.slot == 0 || p.slot == 1) && SlotFree(p.f, 1 - p.slot) && Random.value < 0.6f)
            {
                int to = 1 - p.slot;
                p.path.Clear(); p.hop.Clear(); p.seg = 0; p.segT = 0f;
                p.path.Add(pt); p.hop.Add(false);
                p.path.Add(new Vector3(0f, 0.9f, -0.4f) * hk); p.hop.Add(false);
                p.path.Add(SlotPoint(to, hk)); p.hop.Add(false);
                p.slot = to; p.st = St.Climb; p.speed = 0.7f;
                Debug.Log("CritterPals: the lizard scurries across " + p.f.nick + "'s back to the other shoulder");
                return;
            }
        }
        float t = Time.time * 3f + p.a.GetInstanceID();
        if (p.a.kind == Animal.Kind.Lizard) bob = Mathf.Max(0f, Mathf.Sin(t * 1.7f)) * 0.03f;   // push-up head bobs
        else if (p.a.kind == Animal.Kind.Rabbit) bob = Mathf.Sin(t * 0.8f) * 0.01f;
        else if (p.a.kind == Animal.Kind.Chicken) bob = Mathf.Abs(Mathf.Sin(t)) * 0.02f;
        Pose(p, pt + Vector3.up * bob, face, Time.deltaTime, false);
        if (p.a.head != null) p.a.head.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.6f) * 12f, Mathf.Sin(t * 0.37f) * 30f, 0f);
        if (p.a.tail != null) p.a.tail.localRotation = Quaternion.Euler(10f, Mathf.Sin(t * 1.3f) * 30f, 0f);
    }

    void NuzzleTick(Pal p, float dt)
    {
        Animal a = p.a; Frog f = p.f;
        Vector3 fp = f.transform.position, ap = a.transform.position;
        Vector3 to = fp - ap; to.y = 0f;
        if (to.sqrMagnitude > 0.01f) a.transform.rotation = Quaternion.RotateTowards(a.transform.rotation, Quaternion.LookRotation(to.normalized, Vector3.up), 200f * dt);
        float stand = a.kind == Animal.Kind.Cow || a.kind == Animal.Kind.Horse ? 2.05f : a.kind == Animal.Kind.Goat ? 1.15f : 1.05f;
        if (to.magnitude > stand + 1.4f && to.magnitude < 9f && OutOfArea(a.area, fp) < 2f)
        {
            // the froggy stepped back: follow a step
            Vector3 np = ap + to.normalized * Mathf.Min(to.magnitude - stand, a.speed * 1.4f * dt);
            np.x = Mathf.Clamp(np.x, a.area.xMin + 0.2f, a.area.xMax - 0.2f); np.z = Mathf.Clamp(np.z, a.area.yMin + 0.2f, a.area.yMax - 0.2f);
            np.y = a.Ground(np.x, np.z); a.transform.position = np;
        }
        a.PalAnimate(0f, dt);
        float t = p.t;
        if (a.head != null) a.head.localRotation = Quaternion.Euler(18f + Mathf.Sin(t * 5f) * 8f, Mathf.Sin(t * 3.1f) * 14f, Mathf.Sin(t * 2.3f) * 6f);   // nuzzle rub
        p.lickT -= dt;
        if (p.lickT <= 0f) { p.lickT = Random.Range(0.9f, 1.6f); Lick(p); }
        if (p.tongue != null)
        {
            float k = Mathf.Clamp01(1f - (p.lickT - (p.lickT > 0.5f ? p.lickT - 0.35f : 0f)));
            float age = Time.time - p.faceK;
            float s = age < 0.35f ? Mathf.Sin(age / 0.35f * Mathf.PI) : 0f;
            p.tongue.gameObject.SetActive(s > 0.02f);
            Vector3 dirF = (f.Center - ap); dirF.y = 0f; dirF = dirF.sqrMagnitude > 0.01f ? dirF.normalized : a.transform.forward;
            Vector3 lp = f.Center + Vector3.up * 0.2f * HK(f) - dirF * (0.45f * HK(f) + 0.12f);
            p.tongue.position = lp - dirF * 0.12f * (1f - s);
            p.tongue.rotation = Quaternion.LookRotation(dirF, Vector3.up) * Quaternion.Euler(-20f + s * 35f, 0f, 0f);
            p.tongue.localScale = new Vector3(0.16f, 0.05f, 0.05f + s * 0.32f);
        }
        if (p.t > p.dur) { Debug.Log("CritterPals: the " + KindName(a.kind) + " wanders off after nuzzling " + f.nick); HopOff(p, "done"); }
    }

    void Lick(Pal p)
    {
        if (p.tongue == null)
        {
            var g = Mats.Prim(PrimitiveType.Sphere, null, Vector3.zero, Vector3.one * 0.1f, Mats.Lit(new Color(1f, 0.45f, 0.55f)));
            g.name = "Tongue"; p.tongue = g.transform;
        }
        p.faceK = Time.time;   // lick start
        Licks++;
        Frog f = p.f;
        if (slurp == null) slurp = MakeSlurp();
        Vector3 at = f.Center + Vector3.up * 0.25f;
        if (f.human) Sfx.Play(slurp, 0.55f, Random.Range(0.9f, 1.15f)); else Sfx.PlayAt(slurp, at, 0.5f, 25f, Random.Range(0.9f, 1.15f));
        FX.Sparkle(at, new Color(1f, 0.6f, 0.75f), 3);
        if (f.human && Game.I != null)
            foreach (Slot s in Game.I.slots)
                if (s != null && s.frog >= 0 && s.frog < Game.I.frogs.Count && Game.I.frogs[s.frog] == f && s.rig != null) { s.rig.Bump(2.2f); s.rig.AddShake(0.12f); }
        if (f.human && Licks % 4 == 1) f.Toast("Slurp! The " + KindName(p.a.kind) + " licks " + f.nick, 1.6f);
        if (Licks <= 3 || Licks % 10 == 0) Debug.Log("CritterPals: lick #" + Licks + " by a " + KindName(p.a.kind) + " on " + f.nick);
    }

    static AudioClip MakeSlurp()
    {
        int sr = 22050, n = (int)(sr * 0.32f);
        var d = new float[n];
        float lp = 0f, ph = 0f; var rnd = new System.Random(47);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr, k = t / 0.32f;
            float env = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * (1f - k * 0.4f);
            float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
            float cut = Mathf.Lerp(0.08f, 0.35f, Mathf.Sin(k * Mathf.PI * 2f) * 0.5f + 0.5f);   // wet "shlp" sweep
            lp += (noise - lp) * cut;
            ph += 2f * Mathf.PI * Mathf.Lerp(420f, 180f, k) / sr;
            d[i] = (lp * 0.8f + Mathf.Sin(ph) * 0.25f * env) * env;
        }
        var c = AudioClip.Create("slurp", n, 1, sr, false);
        c.SetData(d, 0);
        return c;
    }

    // let go: riders arc back down into their pen; walkers just turn away
    public void HopOff(Pal p, string why)
    {
        if (p.st == St.Hop) return;
        string k = why; int c; OffWhy.TryGetValue(k, out c); OffWhy[k] = c + 1;
        bool on = p.st == St.Climb || p.st == St.Ride;
        if (p.tongue != null) { Destroy(p.tongue.gameObject); p.tongue = null; }
        if (!on || why == "world")
        {
            cool[p.a] = Random.Range(25f, 50f);
            if (on) { HopOffs++; Debug.Log("CritterPals: the " + KindName(p.a.kind) + " stays home - " + p.f.nick + " left for another place"); }
            Release(p, true); pals.Remove(p);
            return;
        }
        HopOffs++;
        Debug.Log("CritterPals: the " + KindName(p.a.kind) + " hops off " + p.f.nick + " (" + why + ") after " + p.t.ToString("0") + " s");
        p.st = St.Hop; p.t = 0f; p.why = why;
        p.from = p.a.transform.position;
        Rect r = p.a.area;
        Vector3 fp = p.f.transform.position, outD = p.from - fp; outD.y = 0f; outD = outD.sqrMagnitude > 0.001f ? outD.normalized : -p.f.transform.forward;
        Vector3 land = fp + outD * (why == "shake" ? 1.6f : 1f);
        land.x = Mathf.Clamp(land.x, r.xMin + 0.3f, r.xMax - 0.3f); land.z = Mathf.Clamp(land.z, r.yMin + 0.3f, r.yMax - 0.3f);
        land.y = p.a.Ground(land.x, land.z);
        p.to = land;
        float dist = (land - p.from).magnitude;
        p.hopDur = Mathf.Clamp(0.4f + dist * 0.12f, 0.45f, 1.2f);
        p.hopPeak = 0.4f + Mathf.Max(0f, land.y - p.from.y) + (p.raised ? 0.8f : 0f);
        cool[p.a] = why == "shake" ? Random.Range(40f, 70f) : Random.Range(25f, 50f);
        if (p.f.human && why == "shake") p.f.Toast("Shook off the " + KindName(p.a.kind) + "!", 1.4f);
    }

    bool HopTick(Pal p, float dt)
    {
        float k = Mathf.Clamp01(p.t / p.hopDur);
        Vector3 pos = Vector3.Lerp(p.from, p.to, k); pos.y += Mathf.Sin(k * Mathf.PI) * p.hopPeak;
        p.a.transform.position = pos;
        Vector3 d = p.to - p.from; d.y = 0f;
        if (d.sqrMagnitude > 1e-4f) p.a.transform.rotation = Quaternion.Slerp(p.a.transform.rotation, Quaternion.LookRotation(d.normalized, Vector3.up), Mathf.Min(1f, dt * 8f));
        p.a.PalAnimate(2f, dt);
        return k >= 1f;
    }

    void Release(Pal p, bool place)
    {
        if (p.a == null) return;
        if (p.tongue != null) { Destroy(p.tongue.gameObject); p.tongue = null; }
        p.a.transform.localScale = p.scale0;
        if (p.a.body != null) p.a.body.localScale = Vector3.one;
        if (place)
        {
            Vector3 q = p.a.transform.position; Rect r = p.a.area;
            q.x = Mathf.Clamp(q.x, r.xMin + 0.3f, r.xMax - 0.3f); q.z = Mathf.Clamp(q.z, r.yMin + 0.3f, r.yMax - 0.3f); q.y = p.a.Ground(q.x, q.z);
            p.a.transform.position = q;
            Vector3 e = p.a.transform.eulerAngles; p.a.transform.rotation = Quaternion.Euler(0f, e.y, 0f);
        }
        p.a.pal = false;
        p.a.SyncYaw();
    }
}
