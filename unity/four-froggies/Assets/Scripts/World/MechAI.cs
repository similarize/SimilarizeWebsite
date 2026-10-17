using System.Collections.Generic;
using UnityEngine;

// ffu47 MECH AI (Bill: "AI froggies never get into the mechs, they just sit there"). Like BikeAI / SkateAI: idle AI froggies
// (James / Jimmy / Bubbles / Rexy only) now and then walk to THEIR OWN mech (CANON.md: mechs stay owner-locked - every froggy
// has a 10-story + a 100-story, so those are the ones the AI takes; the 1000-story / trillion-story stay for the players),
// climb in and pilot it with the player's own mech code (StoryMech.Drive): walk around the mech yard field west of the house,
// stomp, square up and fire the arm cannon into the sky (never at anything), walk alongside / spar with a player's mech, and
// after 40-90 s walk back to the spot and climb out.
//  * Interest: a human froggy within ~75 m of the mech yard, or in / beside a mech (60 m) -> every ~2.5 s each idle AI froggy
//    within 170 m of its own mech has a 35% chance to turn, walk over and board; up to 2 AI pilots at once. Player far away
//    -> one pilot now and then (every 90-180 s, 35%) so the ranch stays calm.
//  * Safety: never closer than (H x 0.6 + 9) m to a froggy on foot (turns away and stops), stays out of the house rectangle
//    + H/2 + 12 m margin (walks round it), keeps to the field x -195..-95, z -65..78 unless following the player's mech, and
//    spar shots go up at 55 deg, 25 deg off the player's line. No jumps / rockets / missiles (no landings = no Destruct
//    stomps from jumps; footfall crushing is the normal MechStomp + Destruct regen).
//  * MechAI.Paused / &mechai=0 turn it off (demos); logs "MechAI: ...".
public class MechAI : MonoBehaviour
{
    public static MechAI I;
    public static bool Paused;
    public static int Boardings, Shots, Exits;
    public class Job
    {
        public Frog f; public StoryMech m; public int phase;   // 0 walking over, 1 piloting, 2 heading back, 3 done
        public float t, dur, modeT, fireT, stuckT, burstT; public int mode, burst; public Vector3 goal; public float fireYaw;
        public float walked; public Vector3 lastP; public int shots;
    }
    public readonly List<Job> jobs = new List<Job>();
    readonly Dictionary<Frog, float> cool = new Dictionary<Frog, float>();
    readonly List<Frog> coolKeys = new List<Frog>();
    float scanT = 3f, farT = 75f;
    public static readonly Rect Field = new Rect(-195f, -65f, 100f, 143f);
    public static void Ensure() { if (I == null) new GameObject("Mech AI").AddComponent<MechAI>(); }
    void Awake() { I = this; if (Game.UrlParamS("mechai") == "0") Paused = true; }

    public static bool Pilots(Frog f) { return I != null && I.JobOf(f) != null && f.vehicle is StoryMech; }
    Job JobOf(Frog f) { foreach (var j in jobs) if (j.f == f) return j; return null; }
    static float Flat(Vector3 d) { d.y = 0f; return d.magnitude; }

    static Frog HumanNear(Vector3 p, float r, bool onFootOnly)
    {
        if (Game.I == null) return null;
        Frog best = null; float bd = r;
        foreach (Frog f in Game.I.frogs)
        {
            if (f == null || !f.human || f.world != WorldId.Ranch) continue;
            if (onFootOnly && f.vehicle != null) continue;
            Vector3 q = f.vehicle != null ? f.vehicle.transform.position : f.transform.position;
            float d = Flat(q - p);
            if (d < bd) { bd = d; best = f; }
        }
        return best;
    }
    static StoryMech PlayerMech()
    {
        if (Game.I == null) return null;
        foreach (Frog f in Game.I.frogs) if (f != null && f.human && f.vehicle is StoryMech) return (StoryMech)f.vehicle;
        return null;
    }
    public static bool PlayerInterested(out string why)
    {
        why = "";
        if (Game.I == null) return false;
        Vector3 yard = new Vector3(-145f, 0f, -10f);
        foreach (Frog f in Game.I.frogs)
        {
            if (f == null || !f.human || f.world != WorldId.Ranch) continue;
            if (f.vehicle is StoryMech) { why = f.nick + " is in a mech"; return true; }
            Vector3 p = f.transform.position;
            if (Flat(p - yard) < 75f) { why = f.nick + " is " + Flat(p - yard).ToString("0") + " m from the mech yard"; return true; }
            foreach (var m in StoryMech.AllMechs) if (m != null && m.band <= 1 && Flat(m.transform.position - p) < 60f) { why = f.nick + " is near " + m.Title; return true; }
        }
        return false;
    }

    StoryMech MechFor(Frog f)
    {
        StoryMech pick = null; float bd = 1e9f;
        bool want100 = Random.value < 0.25f;
        foreach (var m in StoryMech.AllMechs)
        {
            if (m == null || m.owner != f.charId || m.band > 1 || m.driver != null || m.scripted || !m.CanEnter(f)) continue;
            bool busy = false; foreach (var j in jobs) if (j.m == m) busy = true;
            if (busy) continue;
            float d = Flat(m.transform.position - f.transform.position) + ((m.band == 1) != want100 ? 40f : 0f);
            if (d < bd) { bd = d; pick = m; }
        }
        return pick;
    }

    // send this froggy (or a random idle one) to its own mech; returns the job or null
    public Job Send(Frog who, string why)
    {
        if (who == null || !Roster.IsFrog(who.charId) || who.charId > 3 || !who.AiIdleOnRanch || who.GlideOn) return null;
        StoryMech m = MechFor(who);
        if (m == null) return null;
        var j = new Job { f = who, m = m, phase = 0, t = 0f, dur = Random.Range(40f, 90f), modeT = 0f, lastP = who.transform.position };
        jobs.Add(j);
        Debug.Log("MechAI: " + who.nick + " heads for " + m.Title + " (" + Flat(m.transform.position - who.transform.position).ToString("0") + " m) - " + why);
        return j;
    }

    void Update()
    {
        var g = Game.I;
        if (g == null || g.state != Game.State.Play) return;
        float dt = Mathf.Min(Time.deltaTime, Mathf.Max(0.05f, StoryMech.DtCap));   // the mechs' own clock (demos raise it)
        coolKeys.Clear(); coolKeys.AddRange(cool.Keys);   // ffu60: reused key list (was a new list every frame)
        foreach (var k in coolKeys) cool[k] -= dt;
        for (int i = jobs.Count - 1; i >= 0; i--)
        {
            var j = jobs[i];
            j.t += dt;
            if (j.f == null || j.m == null) { jobs.RemoveAt(i); continue; }
            if (j.phase == 0)
            {
                if (!j.f.AiIdleOnRanch || j.m.driver != null || j.m.wrecked || j.t > 75f || Story.Active) { Debug.Log("MechAI: " + (j.f != null ? j.f.nick : "?") + " gave up walking to " + j.m.Title); jobs.RemoveAt(i); continue; }
                float H = j.m.height;
                Vector3 mp = j.m.transform.position;
                Vector3 board = mp + j.m.transform.forward * (H * 0.22f + 2f);
                j.f.RcInvite(board);
                Vector3 fp = j.f.transform.position + Vector3.up * 0.6f;
                bool atBody = j.m.body != null && (j.m.body.ClosestPoint(fp) - fp).magnitude < 3f;
                if (atBody || Flat(j.f.transform.position - mp) < H * 0.25f + 3.5f || Flat(j.f.transform.position - board) < 2.5f)
                {
                    j.f.EnterVehicle(j.m);
                    if (j.f.vehicle != j.m) { jobs.RemoveAt(i); continue; }
                    j.phase = 1; j.t = 0f; j.modeT = 0f; j.mode = 0; j.fireT = Random.Range(4f, 9f); j.goal = WanderGoal(j);
                    Boardings++;
                    FX.Sparkle(mp + Vector3.up * H * 0.85f, Froggies.Color(j.m.owner), 18);
                    Sfx.PlayAt(Sfx.Door, mp + Vector3.up * H * 0.8f, 0.8f, 120f, 0.7f);
                    Debug.Log("MechAI: " + j.f.nick + " climbs into " + j.m.Title + " (pilots " + Pilots() + ", for " + j.dur.ToString("0") + " s)");
                }
            }
            else if (j.f.vehicle != j.m)
            {
                Debug.Log("MechAI: " + j.f.nick + " is out of " + j.m.Title + " after " + j.t.ToString("0") + " s (" + j.shots + " shots)");
                cool[j.f] = Random.Range(50f, 110f);
                jobs.RemoveAt(i);
            }
        }
        if (Paused || Story.Active) return;
        scanT -= dt; farT -= dt;
        if (scanT > 0f) return;
        scanT = 2.5f;
        string why;
        bool near = PlayerInterested(out why);
        int max = near ? 2 : 1;
        if (Pilots() >= max) return;
        if (!near && farT > 0f) return;
        if (!near) { farT = Random.Range(90f, 180f); if (Random.value > 0.35f) return; }
        var cands = new List<Frog>();
        foreach (Frog f in g.frogs)
        {
            if (f == null || f.human || !f.AiIdleOnRanch || f.GlideOn || f.charId > 3 || JobOf(f) != null) continue;
            float c; if (cool.TryGetValue(f, out c) && c > 0f) continue;
            StoryMech m = MechFor(f);
            if (m == null || Flat(m.transform.position - f.transform.position) > 170f) continue;
            cands.Add(f);
        }
        if (cands.Count == 0) return;
        Frog pick = cands[Random.Range(0, cands.Count)];
        if (near && Random.value > 0.35f) return;
        Send(pick, near ? why : "now and then (player far away)");
    }

    public int Pilots() { int n = 0; foreach (var j in jobs) if (j.phase >= 1 || j.phase == 0) n++; return n; }

    // ---------------- piloting (called from Frog.Think while the froggy sits in the mech) ----------------
    public static PIn Pilot(Frog f, float dt, ref float camYaw)
    {
        var o = new PIn();
        Job j = I != null ? I.JobOf(f) : null;
        StoryMech m = f.vehicle as StoryMech;
        if (j == null || m == null) return o;
        dt = Mathf.Min(Time.deltaTime, StoryMech.DtCap);
        float H = m.height;
        Vector3 p = m.transform.position;
        float yaw = m.transform.eulerAngles.y;
        StoryMech pm = PlayerMech();
        j.modeT -= dt;
        if (j.phase == 1 && j.t > j.dur) { j.phase = 2; j.goal = m.SpawnPoint + m.transform.forward * 0f; Debug.Log("MechAI: " + f.nick + " walks " + m.Title + " back to its spot"); }
        if (j.phase == 1 && j.modeT <= 0f)
        {
            j.modeT = Random.Range(8f, 14f);
            int prev = j.mode;
            if (pm != null && pm != m) j.mode = Random.value < 0.55f ? 1 : (Random.value < 0.6f ? 2 : 0);
            else j.mode = Random.value < 0.75f ? 0 : 3;
            j.goal = WanderGoal(j);
            if (j.mode != prev) Debug.Log("MechAI: " + f.nick + " mode " + ModeName(j.mode) + (pm != null ? " (player mech " + pm.Title + ")" : ""));
        }
        Vector3 goal = j.goal;
        bool hold = false;
        if (j.phase == 1 && j.mode == 1 && pm != null)
        {
            // walk alongside the player's mech: off its right / left shoulder, a little behind
            float side = (j.f.id % 2 == 0 ? 1f : -1f);
            goal = pm.transform.position + pm.transform.right * side * (pm.height * 0.5f + H * 0.5f + 12f) - pm.transform.forward * (H * 0.3f);
        }
        else if (j.phase == 1 && j.mode == 2 && pm != null)
        {
            // spar: face the player's mech from a good distance, side-step, shots up into the sky over it
            Vector3 d = p - pm.transform.position; d.y = 0f;
            float want = pm.height * 0.5f + H * 0.5f + 26f;
            goal = pm.transform.position + (d.sqrMagnitude > 1f ? d.normalized : Vector3.forward) * want;
            if (Flat(goal - p) < 6f) hold = true;
        }
        else if (j.phase == 1 && j.mode == 3) hold = true;   // show off: stomp in place + fire
        if (j.phase == 1 && j.mode == 0 && Flat(goal - p) < 6f) j.goal = goal = WanderGoal(j);
        goal = SafeGoal(goal, H);
        // a froggy on foot too close: turn away and stop (never stomp the player)
        Frog close = HumanNear(p, H * 0.6f + 9f, true);
        if (close != null) { Vector3 away = p - close.transform.position; away.y = 0f; goal = p + away.normalized * 30f; hold = Flat(away) < H * 0.45f + 6f; }
        // ---- fire bursts into the sky ----
        j.fireT -= dt;
        if (j.burst > 0)
        {
            o.aimHeld = true;
            camYaw = j.fireYaw;
            Vector3 dir = Quaternion.Euler(-(j.mode == 2 ? 55f : 40f), j.fireYaw, 0f) * Vector3.forward;
            m.aimPoint = m.MuzzlePoint + dir * 320f; m.aimPointValid = true;
            j.burstT -= dt;
            if (j.burstT <= 0f && m.aimK > 0.6f && Mathf.Abs(Mathf.DeltaAngle(yaw, j.fireYaw)) < 12f)
            {
                o.gunFire = true; j.burst--; j.burstT = 0.55f; j.shots++; Shots++;
                if (j.burst == 0) j.fireT = Random.Range(5f, 11f);
            }
            if (j.burstT < -4f) { j.burst = 0; j.fireT = 6f; }   // couldn't square up: give up this burst
            return o;
        }
        if (j.phase == 1 && j.fireT <= 0f && Flat(p - Vector3.zero) < 400f)
        {
            j.burst = 3; j.burstT = 0.8f;
            j.fireYaw = SafeFireYaw(p, pm != null && j.mode == 2 ? pm.transform.position : (Vector3?)null);
            return o;
        }
        // ---- walk ----
        Vector3 to = goal - p; to.y = 0f;
        float dist = to.magnitude;
        if (j.phase == 2 && (dist < 5f || j.t > j.dur + 35f))
        {
            f.ExitVehicle();
            Exits++;
            return new PIn();
        }
        if (hold || dist < 3f)
        {
            // stomp in place now and then (tiny forward pulses + turns read as stomping)
            o.move = new Vector2(Mathf.Sin(j.t * 0.9f) * 0.35f, 0f);
            return o;
        }
        float ang = Mathf.DeltaAngle(yaw, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
        o.move.x = Mathf.Clamp(ang / 45f, -1f, 1f);
        o.move.y = Mathf.Abs(ang) > 70f ? 0.12f : Mathf.Clamp(dist / 25f, 0.3f, j.mode == 1 && pm != null ? 1f : 0.75f);
        camYaw = yaw;
        // stuck (wedged against something / walking in place): new goal
        if (Flat(p - j.lastP) < 0.4f * dt * 10f && o.move.y > 0.25f) j.stuckT += dt; else j.stuckT = 0f;
        j.lastP = p;
        if (j.stuckT > 6f) { j.stuckT = 0f; j.goal = WanderGoal(j); }
        return o;
    }

    static string ModeName(int m) { return m == 0 ? "wander" : m == 1 ? "walk alongside" : m == 2 ? "spar" : "show off"; }

    static Vector3 WanderGoal(Job j)
    {
        Vector3 p = j.m.transform.position;
        for (int k = 0; k < 12; k++)
        {
            Vector3 q = new Vector3(Random.Range(Field.xMin + 12f, Field.xMax - 8f), 0f, Random.Range(Field.yMin + 8f, Field.yMax - 8f));
            if (Flat(q - p) < 25f || Flat(q - p) > 90f) continue;
            if (InHouse(q, j.m.height)) continue;
            return q;
        }
        return new Vector3(-150f, 0f, 30f);
    }

    static bool InHouse(Vector3 q, float H)
    {
        Vector2 c = Layout.HouseC, s = Layout.HouseSize * 0.5f;
        float mg = H * 0.5f + 12f;
        return Mathf.Abs(q.x - c.x) < s.x + mg && Mathf.Abs(q.z - c.y) < s.y + mg;
    }

    // keep goals off the house (+ margin), inside the ranch, out of the pasture
    static Vector3 SafeGoal(Vector3 g, float H)
    {
        Vector2 c = Layout.HouseC, s = Layout.HouseSize * 0.5f;
        float mg = H * 0.5f + 12f;
        if (InHouse(g, H))
        {
            // push out of the nearer side (west, mostly - the mech field is west of the house)
            float dxW = (g.x - (c.x - s.x - mg)), dzN = ((c.y + s.y + mg) - g.z), dzS = (g.z - (c.y - s.y - mg));
            if (dxW <= dzN && dxW <= dzS) g.x = c.x - s.x - mg - 1f;
            else if (dzN < dzS) g.z = c.y + s.y + mg + 1f;
            else g.z = c.y - s.y - mg - 1f;
        }
        Rect P = RanchLife.Pasture;
        if (g.x > P.xMin - H * 0.4f && g.x < P.xMax + H * 0.4f && g.z > P.yMin - H * 0.4f) g.z = P.yMin - H * 0.4f - 2f;
        g.x = Mathf.Clamp(g.x, -Layout.Half + H * 0.4f, Layout.Half - H * 0.4f);
        g.z = Mathf.Clamp(g.z, -Layout.Half + H * 0.4f, Layout.Half - H * 0.4f);
        return g;
    }

    // a firing direction away from the house and from every froggy (shots climb, then curve down far away)
    static float SafeFireYaw(Vector3 p, Vector3? spar)
    {
        float best = 270f, bs = -1e9f;
        for (int k = 0; k < 16; k++)
        {
            float y = k * 22.5f;
            if (spar.HasValue)
            {
                Vector3 d0 = spar.Value - p; float sy = Mathf.Atan2(d0.x, d0.z) * Mathf.Rad2Deg;
                y = sy + (k % 2 == 0 ? 25f : -25f) + (k / 2) * 3f;
            }
            Vector3 dir = Quaternion.Euler(0f, y, 0f) * Vector3.forward;
            Vector3 land = p + dir * 420f;   // roughly where a bolt comes down
            float score = 0f;
            Vector3 hc = new Vector3(Layout.HouseC.x, 0f, Layout.HouseC.y);
            score += Vector3.Dot(dir, (p - hc).normalized) * 2f;                        // away from the house
            if (Mathf.Abs(land.x) > Layout.Half || Mathf.Abs(land.z) > Layout.Half) score += 1.5f;   // lands off the ranch
            if (Game.I != null)
                foreach (Frog f in Game.I.frogs)
                {
                    if (f == null || f.world != WorldId.Ranch) continue;
                    Vector3 fd = f.transform.position - p; fd.y = 0f;
                    if (fd.sqrMagnitude < 1f) continue;
                    score -= Mathf.Max(0f, Vector3.Dot(dir, fd.normalized)) * (f.human ? 2.5f : 1f);
                }
            if (spar.HasValue) score += 1f;
            if (score > bs) { bs = score; best = y; }
            if (spar.HasValue && k > 3) break;
        }
        return best;
    }
}
