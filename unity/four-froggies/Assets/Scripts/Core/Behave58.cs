using System.Collections.Generic;
using UnityEngine;

// ffu58 BEHAVIOUR REALISM + CONSISTENCY II (Bill's standing realism brief + six-year-old pass). ?behave=old = the ffu57 behaviour.
//  * Pets that come along with a froggy each get their own spot behind it (fanned out, no pile-up on James after a world hop),
//    and keep that spot while walking.
//  * Six-year-old ideas: pets ride along in the Cybertruck's back seats (hop in when James drives off, hop out when he gets out);
//    dogs swim with you in the pond (dog paddle at the surface, even when you dive); cats wait on the shore and watch.
//  * Reactions: animals turn their heads to watch a car going past; idle robots wave at a car that drives by close.
//  * Parked cars hold on a slope (GroundVehicle), the robot jet-pack approach flies a glide path (RobotVisit54),
//    lying pets fold their legs under them (Animals57), Tigy's stripes hug the back (Animals.cs).
//  * C8 scale table: ScaleReport() measures frogs, robots, pets, farm animals, vehicles, doors and furniture against a
//    real-world reference scaled to a froggy = a ~6-year-old (1.15 m) and flags the outliers ("SCALE58 ..." log lines).
public static class Behave58
{
    static int on = -1;
    public static bool On { get { if (on < 0) on = Ffu33.Url("behave") == "old" ? 0 : 1; return on == 1; } }
    public static int BedRides, BedHops, Swims, ShoreWaits, CarWatches, RobotWaves;

    // ------------------------------------------------------------------ C8: real-size pets (the ffu58a scale table: cats 0.62-0.70 m to
    // the ear tips vs ~0.38 real, Germy 1.08 vs 0.8, Daisy 0.50 vs 0.35, rabbits 0.51 vs 0.3; the froggy = a 6-year-old)
    public static float FitK(Animal a)
    {
        if (!On || a == null) return 1f;
        switch (a.kind)
        {
            case Animal.Kind.Cat: return 0.6f;
            case Animal.Kind.Rabbit: return 0.6f;
            case Animal.Kind.Dog: return a.petName == "Germy" ? 0.74f : a.petName == "Daisy" ? 0.7f : 1f;
            default: return 1f;
        }
    }
    public static void Fit(Animal a) { float k = FitK(a); if (k < 0.999f) a.transform.localScale = a.transform.localScale * k; }
    // C8: real heights for the real-world humanoid robots (the fantasy 'Big Figure Two' stays a giant; Unitree H1 is already 1.72-1.8)
    public static float RobotH(string id, float h)
    {
        if (!On) return h;
        switch (id) { case "optimus": return 1.73f; case "figure03": return 1.68f; case "figure02": return 1.68f; case "atlas_hd": return 1.5f; case "atlas_el": return 1.5f; default: return h; }
    }

    // ------------------------------------------------------------------ follow slots
    public static Vector3 FollowSlot(Frog f, Animal a, int idx, Vector3 fw)
    {
        fw.y = 0f; fw = fw.sqrMagnitude > 0.01f ? fw.normalized : Vector3.forward;
        float side = idx % 2 == 0 ? 1f : -1f;
        float ang = (idx < 2 ? 38f : idx < 4 ? 70f : 0f) * side;
        float dist = 1.5f + Animals57.BodyLen(a) * 0.6f + (idx / 2) * 0.8f;
        Vector3 dir = Quaternion.Euler(0f, 180f + ang, 0f) * fw;
        Vector3 at = f.transform.position + dir * dist;
        at.y = f.world == WorldId.Space ? f.transform.position.y : Animals57.G57(at.x, at.z, f.transform.position.y);
        return at;
    }

    static bool Pond(Vector3 p) { return Layout.PondQ(p.x, p.z) < 1.0f && Ranch.GY(p.x, p.z) < Layout.WaterY - 0.3f; }
    static bool InRanchWater(Frog f) { return (f.world == WorldId.Ranch || f.world == WorldId.Underwater) && Pond(f.transform.position) && f.transform.position.y < Layout.WaterY + 0.4f; }

    // in-car seats for pets (Cybertruck ct52: rear bench z -0.85, then the front passenger seat); local to the vehicle
    static readonly Vector3[] CtSeats = { new Vector3(0.45f, 1.2f, -0.85f), new Vector3(-0.45f, 1.2f, -0.85f), new Vector3(0f, 1.2f, -0.9f), new Vector3(0.45f, 1.2f, -0.02f) };
    static bool HasPetSeats(Vehicle v) { return v is GroundVehicle && v.Title.Contains("Cybertruck") && v.transform.Find("Sprung") != null; }
    static readonly Dictionary<Animal, Vector3> carScale = new Dictionary<Animal, Vector3>();
    static readonly HashSet<Animal> told = new HashSet<Animal>();
    static readonly Dictionary<Animal, float> seatY = new Dictionary<Animal, float>();

    // returns true = handled this frame (Animals57 Follow tick)
    public static bool FollowTick(Animals57 m, Animals57.St s, Frog f, float dt)
    {
        Animal a = s.a; Vector3 p = a.transform.position, lp = f.transform.position;
        int idx = m.FollowIndex(s);
        // 1) riding along in the car
        if (f.vehicle != null && HasPetSeats(f.vehicle) && (a.kind == Animal.Kind.Cat || a.kind == Animal.Kind.Dog) && idx < CtSeats.Length)
        {
            Transform vt = f.vehicle.transform;
            Transform sprung = vt.Find("Sprung") ?? vt;
            if (a.transform.parent != sprung)
            {
                if (!carScale.ContainsKey(a)) carScale[a] = a.transform.localScale;
                FX.Dust(p, 0.3f);
                a.transform.SetParent(sprung, true);
                // fit under the roof: the rear headroom is ~0.42 m above the cushion (a froggy shrinks to its seat the same way)
                // cats sit on the rear cushion (top 1.21); a big dog sits down in the rear footwell (0.85) with its head up
                // under the roof (~1.62 inside); a froggy shrinks to its seat the same way when nothing else fits
                float h = Mathf.Max(0.2f, TopH(a));
                seatY[a] = Mathf.Clamp(1.62f - h, 0.85f, 1.2f);
                a.transform.localScale = carScale[a] * Mathf.Min(1f, (1.62f - seatY[a]) / h);
                BedRides++;
                f.Toast(Nm(a) + " hops in the back seat - road trip!", 2f);
                Debug.Log("BEHAVE58 pet in car " + Nm(a) + " seat " + idx + " in " + f.vehicle.Title + " h " + h.ToString("0.00"));
            }
            Vector3 cs = CtSeats[idx]; float sy; if (seatY.TryGetValue(a, out sy)) cs.y = sy;
            a.transform.localPosition = cs;
            a.transform.localRotation = Quaternion.identity;
            a.happy57 = Mathf.Max(a.happy57, f.vehicle.Speed > 3f ? 0.6f : 0f);   // tail wag at speed (head out of the window, in spirit)
            a.PalAnimate(0f, dt);
            return true;
        }
        if (carScale.ContainsKey(a))
        {
            // James got out (or a world hop already pulled the pet out): hop out next to him at full size
            if (a.transform.parent != null) a.transform.SetParent(null, true);
            a.transform.localScale = carScale[a]; carScale.Remove(a);
            Vector3 fw0 = f.transform.forward; Vector3 at = FollowSlot(f, a, idx, fw0);
            a.transform.position = at; a.transform.rotation = Quaternion.LookRotation(new Vector3(fw0.x, 0f, fw0.z).sqrMagnitude > 0.01f ? new Vector3(fw0.x, 0f, fw0.z) : Vector3.forward);
            FX.Dust(at, 0.4f); BedHops++;
            Debug.Log("BEHAVE58 pet out of car " + Nm(a) + " at " + at.ToString("0.0"));
            return true;
        }
        if (f.vehicle != null) return false;   // other vehicles: run behind / catch up as before
        // 2) the pond: dogs paddle along at the surface, cats wait on the shore
        if (InRanchWater(f))
        {
            if (a.kind == Animal.Kind.Dog)
            {
                Vector3 tgt = lp - f.transform.forward * 1.6f; tgt.y = Layout.WaterY - 0.32f;
                Vector3 d = tgt - p; d.y = 0f;
                bool wet = Pond(p);
                float sp = wet ? 1.3f : a.runSpeed;
                if (d.magnitude > 0.8f)
                {
                    a.transform.rotation = Quaternion.RotateTowards(a.transform.rotation, Quaternion.LookRotation(d.normalized), 180f * dt);
                    Vector3 np = p + a.transform.forward * Mathf.Min(d.magnitude, sp * dt);
                    np.y = Pond(np) ? Mathf.MoveTowards(p.y, Layout.WaterY - 0.32f, 2f * dt) : Animals57.G57(np.x, np.z, p.y);
                    a.transform.position = np;
                    a.PalAnimate(wet ? 2.2f : sp, dt);   // paddling legs
                    if (wet && Random.value < dt * 3f) FX.Splash(new Vector3(np.x, Layout.WaterY + 0.05f, np.z), 0.4f);
                }
                else a.PalAnimate(0.8f, dt);
                if (!told.Contains(a) && Pond(p)) { told.Add(a); Swims++; f.Toast(Nm(a) + " jumps in - dog paddle!", 2f); Debug.Log("BEHAVE58 swim " + Nm(a) + " paddles with " + f.nick); }
                return true;
            }
            else
            {
                Vector3 shore = RobotDriver.ShoreNear(lp);
                Vector3 d = shore - p; d.y = 0f;
                if (d.magnitude > 0.6f && !Pond(p + d.normalized * 0.5f)) Animals57.Move(a, shore, a.runSpeed, dt, 0.4f);
                else { a.PalAnimate(0f, dt); a.lookAt57 = lp; }
                if (!told.Contains(a)) { told.Add(a); ShoreWaits++; f.Toast(Nm(a) + " doesn't like water - waiting on the shore", 2f); Debug.Log("BEHAVE58 shore " + Nm(a) + " waits for " + f.nick + " at " + shore.ToString("0.0")); }
                return true;
            }
        }
        if (told.Contains(a)) told.Remove(a);
        if (Pond(p) && a.kind == Animal.Kind.Dog)
        {   // climb out of the pond and shake off
            Vector3 sh = RobotDriver.ShoreNear(p); Animals57.Move(a, sh, 1.3f, dt, 0.3f);
            if (!Pond(a.transform.position)) { a.transform.position = new Vector3(a.transform.position.x, Animals57.G57(a.transform.position.x, a.transform.position.z, Layout.WaterY + 1f), a.transform.position.z); FX.Splash(a.transform.position + Vector3.up * 0.4f, 0.8f); }
            return true;
        }
        // 3) normal follow, but to this pet's own slot behind James
        float dl = Flat(lp - p);
        if (dl > 40f || Mathf.Abs(lp.y - p.y) > 12f) { m.ArrivePublic(s, false); return true; }
        Vector3 slot = FollowSlot(f, a, idx, f.transform.forward);
        float ds = Flat(slot - p);
        if (ds > 0.5f) Animals57.Move(a, slot, Mathf.Min(a.runSpeed * 1.3f, ds * 1.4f + a.speed), dt, 0.3f, f.world == WorldId.Space ? lp.y : float.NaN);
        else { a.PalAnimate(0f, dt); a.lookAt57 = lp + Vector3.up * 0.5f; }
        return true;
    }

    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }
    static string Nm(Animal a) { return a.petName.Length > 0 ? a.petName : "the " + a.kind.ToString().ToLower(); }
    static float TopH(Animal a)
    {
        float y0 = a.transform.position.y, top = y0 + 0.3f; bool any = false;
        foreach (var r in a.GetComponentsInChildren<Renderer>()) { if (r.GetComponentInParent<Frog>() != null || r.GetComponent<TextMesh>() != null || r is ParticleSystemRenderer) continue; top = any ? Mathf.Max(top, r.bounds.max.y) : r.bounds.max.y; any = true; }
        return top - y0;
    }

    // ------------------------------------------------------------------ C8 scale table
    struct Ref { public string what; public float real; public Ref(string w, float r) { what = w; real = r; } }
    public static float Height(GameObject g, out Vector3 size)
    {
        Bounds b = new Bounds(g.transform.position, Vector3.zero); bool any = false;
        foreach (var r in g.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || r is ParticleSystemRenderer || r.GetComponent<TextMesh>() != null) continue;
            if (g.GetComponent<Frog>() == null && r.GetComponentInParent<Frog>() != null) continue;   // a frog riding / sitting in it
            if (any) b.Encapsulate(r.bounds); else { b = r.bounds; any = true; }
        }
        size = b.size;
        return any ? b.max.y - g.transform.position.y : 0f;
    }
    public static string ScaleReport()
    {
        // real-world heights (m, top of head / back / roof) with the froggy = a 6-year-old (1.15 m): a 6-year-old's world
        var rows = new List<string>();
        Frog j = Game.I != null && Game.I.frogs.Count > 0 ? Game.I.frogs[0] : null;
        Vector3 sz;
        float fh = j != null ? Height(j.gameObject, out sz) : 1f; if (fh < 0.3f) fh = 1f;
        float k = fh / 1.15f;   // game metres per real metre at the froggy's scale
        rows.Add("SCALE58 ref froggy " + (j != null ? j.nick : "?") + " h " + fh.ToString("0.00") + " m = a 6-year-old 1.15 m -> x" + k.ToString("0.00"));
        int outl = 0;
        System.Action<string, float, float> row = (n, h, real) =>
        {
            float want = real * k, r = want > 0.01f ? h / want : 0f;
            bool o = r < 0.7f || r > 1.43f; if (o) outl++;
            rows.Add("SCALE58 " + n + " h " + h.ToString("0.00") + " want " + want.ToString("0.00") + " (real " + real.ToString("0.00") + ") ratio " + r.ToString("0.00") + (o ? " OUTLIER" : " ok"));
        };
        if (Game.I != null) foreach (var f in Game.I.frogs) if (f != null && f != j && f.vehicle == null && f.rideAnimal == null) { float h = Height(f.gameObject, out sz); rows.Add("SCALE58 frog " + f.nick + " h " + h.ToString("0.00") + " vs James " + (h / fh).ToString("0.00")); }
        if (RanchLife.I != null) foreach (var r in RanchLife.I.robots) if (r != null)
            {   // real-world humanoids at their published heights (Big Figure Two = the fantasy giant, compared with its own field)
                float real = r.id == "optimus" ? 1.73f : r.id == "unitree" ? 1.8f : r.id == "figure03" || r.id == "figure02" ? 1.68f : r.id == "atlas_hd" || r.id == "atlas_el" ? 1.5f : r.height;
                float hh = Height(r.gameObject, out sz);
                float want = real * k, rr = hh / want; bool o = rr < 0.7f || rr > 1.3f; if (o) outl++;
                rows.Add("SCALE58 robot " + r.robotName + " h " + hh.ToString("0.00") + " (field " + r.height.ToString("0.00") + ") real " + real.ToString("0.00") + " ratio " + rr.ToString("0.00") + (o ? " OUTLIER" : " ok"));
            }
        var seen = new HashSet<string>();
        foreach (var a in Resources.FindObjectsOfTypeAll<Animal>())
        {
            if (a == null || !a.gameObject.scene.IsValid() || a.transform.parent != null && a.transform.parent.GetComponentInParent<Vehicle>() != null) continue;
            string key = a.petName.Length > 0 ? a.petName : a.kind.ToString();
            if (seen.Contains(key)) continue; seen.Add(key);
            float real;
            switch (a.kind)
            {
                case Animal.Kind.Cat: real = 0.38f; break;                                         // to the ear tips
                case Animal.Kind.Dog: real = a.petName == "Daisy" ? 0.35f : a.petName == "Germy" ? 0.80f : 0.55f; break;   // dachshund / German shepherd (ear tips)
                case Animal.Kind.Cow: real = 1.5f; break; case Animal.Kind.Horse: real = 1.75f; break;   // withers 1.4 / 1.6 + head
                case Animal.Kind.Goat: real = a.voice == "pig" ? 0.9f : 0.95f; break; case Animal.Kind.Rabbit: real = 0.30f; break;
                case Animal.Kind.Chicken: real = 0.45f; break; default: real = 0.15f; break;
            }
            bool act = a.gameObject.activeInHierarchy;
            float h = act ? Height(a.gameObject, out sz) : -1f;
            if (act) row("animal " + key, h, real); else rows.Add("SCALE58 animal " + key + " (inactive, not measured)");
        }
        seen.Clear();
        foreach (var v in Vehicle.All)
        {
            if (v == null || !v.isActiveAndEnabled) continue;
            string t = v.Title; float real = -1f;
            if (t.Contains("Cybertruck")) real = 1.79f; else if (v is Roadster49) real = 1.21f; else if (v is Bike) real = 1.25f; else if (t.Contains("Tractor") || t.Contains("tractor")) real = 2.7f;
            if (real < 0f || seen.Contains(t)) continue; seen.Add(t);
            float h = Height(v.gameObject, out sz);
            // vehicles are real-size (the froggies are the kids): compare in REAL metres too
            rows.Add("SCALE58 vehicle " + t + " h " + h.ToString("0.00") + " l " + sz.z.ToString("0.00") + " real " + real.ToString("0.00") + " ratio " + (h / real).ToString("0.00") + " | seat scale " + v.seatScale.ToString("0.00") + (v.seat != null ? " seat y " + v.transform.InverseTransformPoint(v.seat.position).y.ToString("0.00") : ""));
        }
        // doors + furniture by name (house, garage, barn, Real Room)
        int nd = 0;
        foreach (var tr in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (tr == null || !tr.gameObject.scene.IsValid() || !tr.gameObject.activeInHierarchy) continue;
            string n = tr.name.ToLower(); float real = -1f; string what = null;
            if (n.Contains("door") && !n.Contains("doorway") && !n.Contains("sign")) { real = 2.03f; what = "door"; }
            else if (n == "bed" || n.StartsWith("bed ")) { real = 0.6f; what = "bed"; }
            else if (n.Contains("chair")) { real = 0.9f; what = "chair"; }
            else if (n.Contains("table") && !n.Contains("turntable")) { real = 0.75f; what = "table"; }
            else if (n.Contains("sofa") || n.Contains("couch")) { real = 0.85f; what = "sofa"; }
            if (what == null || nd > 40) continue;
            float h = Height(tr.gameObject, out sz);
            if (h < 0.05f) continue;
            nd++;
            rows.Add("SCALE58 " + what + " '" + tr.name + "' at " + tr.position.ToString("0") + " h " + h.ToString("0.00") + " w " + Mathf.Max(sz.x, sz.z).ToString("0.00") + " real " + real.ToString("0.00") + " (x" + (h / real).ToString("0.00") + " real, x" + (h / (real * k)).ToString("0.00") + " froggy scale)");
        }
        rows.Add("SCALE58 outliers " + outl);
        foreach (var s in rows) Debug.Log(s);
        return string.Join("\n", rows.ToArray());
    }
}

// reactions: animals watch passing cars, idle robots wave at them
public class Behave58Runner : MonoBehaviour
{
    public static Behave58Runner I;
    float t; readonly HashSet<Animal> watching = new HashSet<Animal>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { if (I != null || !Behave58.On) return; I = new GameObject("Behave58").AddComponent<Behave58Runner>(); DontDestroyOnLoad(I.gameObject); }
    void Update()
    {
        t -= Time.unscaledDeltaTime; if (t > 0f || Game.I == null) return; t = 0.35f;
        var fast = new List<Vehicle>();
        foreach (var v in Vehicle.All) if (v != null && v.isActiveAndEnabled && v.Speed > 2.5f && !(v is Horse)) fast.Add(v);
        foreach (var a in watching) if (a != null && a.lookAt57.HasValue && !a.pal) a.lookAt57 = null;
        watching.Clear();
        if (fast.Count == 0) return;
        foreach (var a in Animal.All)
        {
            if (a == null || a.pal || a.frozen || !a.isActiveAndEnabled) continue;
            Vector3 p = a.transform.position;
            foreach (var v in fast)
                if ((v.transform.position - p).sqrMagnitude < 16f * 16f) { if (!a.lookAt57.HasValue) { a.lookAt57 = v.transform.position + Vector3.up; watching.Add(a); Behave58.CarWatches++; } break; }
        }
        if (RanchLife.I != null)
            foreach (var r in RanchLife.I.robots)
            {
                if (r == null || !r.isActiveAndEnabled) continue;
                foreach (var v in fast)
                    if ((v.transform.position - r.transform.position).sqrMagnitude < 7f * 7f && r.WaveAtCar58(v)) { Behave58.RobotWaves++; break; }
            }
    }
}

public partial class Robot
{
    float wave58T;
    // an idle robot (no visit / order) waves at a car going past, at most every 12 s
    public bool WaveAtCar58(Vehicle v)
    {
        if (visit != null || manual != null || Time.time < wave58T || v.driver == null) return false;
        wave58T = Time.time + 12f; wave = Mathf.Max(wave, 1.6f);
        return true;
    }
}

public partial class Animal
{
    // demo: lie down now (Gait57 idle 2) for the lying-pose shot
    public void DemoLie58() { g57Idle = 2; g57IdleT = 30f; happy57 = 0f; }
}
