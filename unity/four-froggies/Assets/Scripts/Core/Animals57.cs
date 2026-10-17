using System.Collections.Generic;
using UnityEngine;

// ffu57 ANIMALS + FROG SKIN + MOTION (Bill: make everything look and behave real; six-year-old ideas; consistency).
//  * ONE skin shader for every animal and froggy (Shaders/FFSkin57.shader, Bootstrap.skin57Mat): fur shading (strand
//    texture made here, anisotropic sheen, fuzzy rim) for the cats Spotty / Tigy / Kitty / Little White Socks, the dogs
//    Germy (German shepherd) / Daisy (dachshund), horses, cows, rabbits, sheep, chickens; WET SKIN for the frogs (tight wet
//    highlight, fresnel sky reflection, warm sub-surface tint, faint mottle). Same profile per species everywhere.
//  * CONSISTENCY: the named house pets take their coat from the Roster (the same colours / markings as the playable
//    Spotty ... Daisy), so the same animal looks the same in the house, on the ranch, on Mars and as a player.
//  * GAITS (Animal.Gait57 below): 4-beat walk (lateral sequence), diagonal trot, rotary gallop (horses) / bound (cats,
//    dogs), rabbit hops; feet planted during stance (the leg sweeps back at body speed), quick lifted swing; body bob /
//    pitch per gait; stride length and cadence from leg length + speed. IDLE: breathing, tail swish / wag, look around,
//    cats + dogs sit / lie down / groom, horses rest a hind hoof, cows chew + lie down.
//  * SIX-YEAR-OLD: E next to ANY animal cycles PET -> FEED (a treat that fits it) -> RIDE (it carries you, stick steers)
//    -> PLAY FETCH (dogs) -> COME ALONG / STAY (named pets). Named pets that come along follow James into every world
//    (house, ranch, Mars, Callisto, Moon, space, the Real Room ...) like froggies and robots. Robots: PLAY > "Pet + feed
//    animals", "Ride a cow", "Fetch with the dogs" (World/RobotVisit57 part below).
//  * Mechs leave footprints (Tracks56.MechFoot, from MechStomp.Foot).
//  * ?animals=old = the ffu56 look + motion (no dressing, old leg swing, no new E actions).
// Logs "ANIMALS57 ..." (probe).
public class Animals57 : MonoBehaviour
{
    public static Animals57 I;
    public static Material Base;
    public static Vector2? DemoSteer;     // demo / probe: steers a ridden animal (GameFfu57)          // FF/Skin57 (Bootstrap.skin57Mat); null = fall back to Standard tweaks
    static int onState = -1;
    public static bool On { get { if (onState < 0) onState = Ffu33.Url("animals") == "old" ? 0 : 1; return onState == 1; } }
    public static int Dressed, FrogsDressed, Pets, Feeds, Rides, Fetches, Follows, Arrivals, RobotPets, RobotRides, RobotFetches;

    public enum Mode { None, Pet, Eat, Ridden, Fetch, Return, Follow }
    public class St
    {
        public Animal a; public Mode mode; public float t; public Frog f; public Robot r; public int step;
        public GameObject prop; public Vector3 ballAt, home; public int fstage; public PIn rin; public float ryaw; public bool rrun;
        public float circle; public Vector3 seatLocal; public float riderScale = 1f;
    }
    readonly Dictionary<Animal, St> st = new Dictionary<Animal, St>();
    readonly Dictionary<Animal, Hotspot> hs = new Dictionary<Animal, Hotspot>();
    readonly HashSet<int> dressed = new HashSet<int>();
    readonly Dictionary<Frog, WorldId> lastWorld = new Dictionary<Frog, WorldId>();
    float scanT, frogScanT;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null || !On) return;
        I = new GameObject("Animals57").AddComponent<Animals57>();
        DontDestroyOnLoad(I.gameObject);
        Debug.Log("ANIMALS57 on (skin shader " + (Base != null ? Base.shader.name : "none - Standard fallback") + ")");
    }

    public St Of(Animal a) { St s; if (!st.TryGetValue(a, out s)) { s = new St { a = a }; st[a] = s; } return s; }
    public bool Busy(Animal a) { St s; return st.TryGetValue(a, out s) && s.mode != Mode.None && s.mode != Mode.Follow; }
    public static bool Named(Animal a) { return a != null && RosterIndex(a.petName) >= 0; }
    public static int RosterIndex(string n) { if (string.IsNullOrEmpty(n)) return -1; for (int i = 4; i < Roster.Count; i++) if (Roster.All[i].name == n) return i; return -1; }

    // ------------------------------------------------------------------ materials
    static Texture2D furTex, mottleTex;
    static float Hash(int x, int y) { unchecked { int h = x * 374761393 + y * 668265263; h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0xffff) / 65535f; } }
    static float VNoise(float x, float y, int per)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y); float fx = x - xi, fy = y - yi;
        fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
        int x0 = ((xi % per) + per) % per, x1 = (x0 + 1) % per, y0 = ((yi % per) + per) % per, y1 = (y0 + 1) % per;
        return Mathf.Lerp(Mathf.Lerp(Hash(x0, y0), Hash(x1, y0), fx), Mathf.Lerp(Hash(x0, y1), Hash(x1, y1), fx), fy);
    }
    static Texture2D MakeTex(bool fur)
    {
        const int S = 128;
        var t = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 2, name = fur ? "fur57" : "mottle57" };
        var px = new Color32[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = x / (float)S, v = y / (float)S, n;
                if (fur) n = 0.55f * VNoise(u * 48f, v * 5f, 48) + 0.3f * VNoise(u * 96f, v * 9f, 96) + 0.15f * VNoise(u * 12f, v * 12f, 12);   // long strands along v
                else n = 0.6f * VNoise(u * 6f, v * 6f, 6) + 0.4f * VNoise(u * 18f, v * 18f, 18);                                           // soft blotches
                byte b = (byte)Mathf.Clamp(n * 255f, 0f, 255f);
                px[y * S + x] = new Color32(b, b, b, 255);
            }
        t.SetPixels32(px); t.Apply(true, true);
        return t;
    }

    public struct Prof { public float fur, wet, gloss, wrap, rim, scale; public Color sss; }
    public static Prof ProfFor(Animal.Kind k, bool dachshund)
    {
        var p = new Prof { fur = 1f, wet = 0f, gloss = 0.3f, wrap = 0.5f, rim = 0.2f, scale = 0.07f, sss = new Color(1f, 0.6f, 0.45f) };
        switch (k)
        {
            case Animal.Kind.Cat: p.gloss = 0.35f; p.scale = 0.05f; break;
            case Animal.Kind.Dog: if (dachshund) { p.fur = 0.7f; p.gloss = 0.5f; p.scale = 0.05f; } else { p.gloss = 0.3f; p.scale = 0.08f; } break;
            case Animal.Kind.Horse: p.fur = 0.55f; p.gloss = 0.62f; p.scale = 0.12f; break;
            case Animal.Kind.Cow: p.fur = 0.8f; p.gloss = 0.22f; p.scale = 0.1f; break;
            case Animal.Kind.Rabbit: p.gloss = 0.2f; p.scale = 0.035f; break;
            case Animal.Kind.Goat: p.gloss = 0.1f; p.scale = 0.05f; p.wrap = 0.7f; break;
            case Animal.Kind.Chicken: p.fur = 0.9f; p.gloss = 0.25f; p.scale = 0.04f; break;
            case Animal.Kind.Lizard: p.fur = 0f; p.wet = 0.3f; p.gloss = 0.6f; p.scale = 0.03f; break;
        }
        return p;
    }
    public static Prof FrogProf() { return new Prof { fur = 0f, wet = 1f, gloss = 0.78f, wrap = 0.55f, rim = 0.25f, scale = 0.2f, sss = new Color(1f, 0.62f, 0.4f) }; }

    static readonly Dictionary<long, Material> cache = new Dictionary<long, Material>();
    public static Material Mat(Color c, Prof p, bool vc)
    {
        Color32 c3 = c;
        long key = ((long)c3.r << 32) | ((long)c3.g << 24) | ((long)c3.b << 16) | ((long)Mathf.RoundToInt(p.fur * 10f) << 12) | ((long)Mathf.RoundToInt(p.wet * 10f) << 8) | ((long)Mathf.RoundToInt(p.gloss * 20f) << 2) | (vc ? 1L : 0L);
        key ^= (long)Mathf.RoundToInt(p.scale * 1000f) << 40;
        Material m;
        if (cache.TryGetValue(key, out m) && m != null) return m;
        if (Base != null)
        {
            if (furTex == null) { furTex = MakeTex(true); mottleTex = MakeTex(false); }
            m = new Material(Base) { name = "Skin57" };
            m.SetColor("_Color", c);
            m.SetTexture("_MainTex", p.fur > 0f ? furTex : mottleTex);
            m.SetFloat("_Scale", p.scale); m.SetFloat("_Gloss", p.gloss); m.SetFloat("_Wrap", p.wrap); m.SetFloat("_Rim", p.rim);
            m.SetColor("_SSS", p.sss); m.SetFloat("_Fur", p.fur); m.SetFloat("_Wet", p.wet); m.SetFloat("_VC", vc ? 1f : 0f);
        }
        else m = p.wet > 0.5f ? Mats.Skin(c, Mathf.Min(1f, p.gloss + 0.1f)) : Mats.PBR(c, p.gloss * 0.5f, 0f);
        cache[key] = m;
        return m;
    }

    static bool HasVC(MeshFilter mf)
    {
        if (mf == null || mf.sharedMesh == null) return false;
        var m = mf.sharedMesh;
        try { return m.isReadable && m.colors32 != null && m.colors32.Length > 0; } catch { return false; }
    }

    // a renderer that is an eye / nose / tongue / claw stays as it is
    static bool Detail(Renderer r, Material m)
    {
        if (m == null) return true;
        string sn = m.shader != null ? m.shader.name : "";
        if (sn.Contains("Unlit") || sn.Contains("Particles") || sn.Contains("Transparent") || sn == "FF/Skin57") return true;
        if (m.HasProperty("_MainTex") && m.mainTexture != null) return true;   // textured pack parts keep their painted texture
        Vector3 s = r.bounds.size;
        return Mathf.Max(s.x, Mathf.Max(s.y, s.z)) < 0.075f;
    }

    public static void DressAnimal(Animal a)
    {
        if (a == null) return;
        bool dachs = a.kind == Animal.Kind.Dog && a.legs.Count > 0 && a.legs[0].localPosition.y < 0.25f;   // short-legged dog
        Prof p = ProfFor(a.kind, dachs);
        int n = 0;
        foreach (var r in a.GetComponentsInChildren<MeshRenderer>(true))
        {
            Material m = r.sharedMaterial;
            if (Detail(r, m)) continue;
            Color c = m.HasProperty("_Color") ? m.color : Color.white;
            // near-black eyes / pupils are glossy little spheres; leave pure black small parts alone
            if (c.r + c.g + c.b < 0.06f && r.bounds.size.magnitude < 0.2f) continue;
            r.sharedMaterial = Mat(c, p, HasVC(r.GetComponent<MeshFilter>()));
            n++;
        }
        Dressed++;
        if (Named(a)) Debug.Log("ANIMALS57 dressed " + a.petName + " (" + a.kind + ") parts " + n);
    }

    public static void DressFrog(FrogModel fm, bool pet, Roster.Kind kind, Roster.Look look)
    {
        Prof p = pet ? ProfFor(kind == Roster.Kind.Cat ? Animal.Kind.Cat : Animal.Kind.Dog, look == Roster.Look.Dachshund) : FrogProf();
        int n = 0;
        foreach (var r in fm.GetComponentsInChildren<MeshRenderer>(true))
        {
            Material m = r.sharedMaterial;
            if (m == null || m.shader == null || m.shader.name != "FF/Skin") continue;   // body skin only (eyes are paint / unlit)
            Vector3 s = r.bounds.size;
            if (Mathf.Max(s.x, Mathf.Max(s.y, s.z)) < 0.06f * Mathf.Max(0.3f, fm.transform.lossyScale.y)) continue;   // blush / tiny bits
            r.sharedMaterial = Mat(m.color, p, HasVC(r.GetComponent<MeshFilter>()));
            n++;
        }
        FrogsDressed++;
    }

    // ------------------------------------------------------------------ main loop
    void Update()
    {
        if (Game.I == null) return;
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        scanT -= Time.unscaledDeltaTime;
        if (scanT <= 0f)
        {
            scanT = 0.5f;
            foreach (var a in Animal.All)
            {
                if (a == null) continue;
                int id = a.GetInstanceID();
                if (!dressed.Contains(id)) { dressed.Add(id); DressAnimal(a); }
                if (!hs.ContainsKey(a)) AddSpot(a);
            }
        }
        frogScanT -= Time.unscaledDeltaTime;
        if (frogScanT <= 0f)
        {
            frogScanT = 2f;
            foreach (var fm in Object.FindObjectsOfType<FrogModel>())
            {
                int id = fm.GetInstanceID();
                if (dressed.Contains(id) || fm.transform.childCount == 0) continue;
                dressed.Add(id);
                var d = Roster.Valid(fm.charId) ? Roster.All[fm.charId] : Roster.All[0];
                DressFrog(fm, fm.pet, d.kind, d.look);
            }
        }
        // hotspots ride along with their animals
        // (a rideable ranch horse is a vehicle too: its E spot sits at its head, mounting stays at its side)
        foreach (var kv in hs) if (kv.Key != null) kv.Value.pos = kv.Key.transform.position + (kv.Key.kind == Animal.Kind.Horse && kv.Key.GetComponent<Horse>() != null ? kv.Key.transform.forward * 1.5f : Vector3.zero);
        // world changes: named pets that come along follow James into the next world
        foreach (var f in Game.I.frogs)
        {
            if (f == null) continue;
            WorldId w; bool had = lastWorld.TryGetValue(f, out w);
            lastWorld[f] = f.world;
            if (had && w != f.world) foreach (var s in st.Values) if (s.mode == Mode.Follow && s.f == f && s.a != null) Arrive(s, true);
        }
        var list = new List<St>(st.Values);
        foreach (var s in list)
        {
            if (s.a == null) { st.Remove(s.a); continue; }
            if (s.mode == Mode.None) continue;
            s.t += dt;
            Tick(s, dt);
        }
    }

    void AddSpot(Animal a)
    {
        float r = a.kind == Animal.Kind.Horse && a.GetComponent<Horse>() != null ? 1.3f : a.kind == Animal.Kind.Cow || a.kind == Animal.Kind.Horse ? 2.6f : a.kind == Animal.Kind.Dog || a.kind == Animal.Kind.Goat ? 1.9f : 1.5f;
        var h = Interact.Add(a.transform.position, r, "pet", f => Act(f, a));
        h.enabled = f => a != null && a.isActiveAndEnabled && !Busy(a) && !a.frozen && f.rideAnimal == null && !(CritterPals.I != null && CritterPals.I.pals.Exists(p => p.a == a));
        h.dynLabel = f => Label(f, a);
        hs[a] = h;
    }

    static string Nm(Animal a)
    {
        if (Named(a)) return a.petName;
        switch (a.kind) { case Animal.Kind.Goat: return a.voice == "pig" ? "the pig" : "the sheep"; default: return "the " + a.kind.ToString().ToLower(); }
    }
    static string Treat(Animal a)
    {
        switch (a.kind)
        {
            case Animal.Kind.Cat: return "a fish treat"; case Animal.Kind.Dog: return "a bone";
            case Animal.Kind.Horse: return "a carrot"; case Animal.Kind.Cow: return "some hay"; case Animal.Kind.Rabbit: return "a carrot";
            case Animal.Kind.Chicken: return "some seeds"; case Animal.Kind.Goat: return a.voice == "pig" ? "an apple" : "some hay"; default: return "a bug";
        }
    }
    bool CanRide(Animal a) { return a.GetComponent<Horse>() == null && a.kind != Animal.Kind.Lizard; }   // ranch horses are rideable vehicles already (Horse.cs)
    string[] Steps(Animal a)
    {
        var l = new List<string> { "pet", "feed" };
        if (CanRide(a)) l.Add("ride");
        if (a.kind == Animal.Kind.Dog) l.Add("fetch");
        if (Named(a)) l.Add("come");
        return l.ToArray();
    }
    string Label(Frog f, Animal a)
    {
        var s = Of(a); var k = Steps(a); string v = k[s.step % k.Length]; string n = Nm(a);
        switch (v)
        {
            case "pet": return "pet " + n;
            case "feed": return "feed " + n + " " + Treat(a);
            case "ride": return "ride " + n + "!";
            case "fetch": return "play fetch with " + n;
            default: return s.mode == Mode.Follow ? "tell " + n + " to stay here" : "ask " + n + " to come along";
        }
    }
    void Act(Frog f, Animal a)
    {
        var s = Of(a); var k = Steps(a); string v = k[s.step % k.Length]; s.step++;
        Act(f, a, v);
    }
    public void Act(Frog f, Animal a, string v)
    {
        var s = Of(a);
        if (v == "come" && s.mode == Mode.Follow) { Stay(s); f.Toast(Nm(a) + " sits and stays here", 2f); return; }
        switch (v)
        {
            case "pet": StartPet(s, f, null); break;
            case "feed": StartFeed(s, f, null); break;
            case "ride": StartRide(s, f); break;
            case "fetch": StartFetch(s, f, null); break;
            default: StartFollow(s, f); break;
        }
    }

    // ------------------------------------------------------------------ actions (frogs AND robots)
    static Vector3 Who(St s) { return s.r != null ? s.r.transform.position : s.f != null ? s.f.transform.position : s.a.transform.position; }
    // ffu58: a line that already starts with the robot's name ("Unitree gives the cow some hay") is not prefixed again
    static void Say(St s, string line, float secs = 2.2f)
    {
        if (s.r != null)
        {
            string t = Behave58.On && line.StartsWith(s.r.robotName) ? line : s.r.robotName + ": " + line;
            if (s.r.visit != null && s.r.visit.lead != null) s.r.visit.lead.Toast(t, secs);
            if (Behave58.On) Debug.Log("ANIMALS57 robot says '" + t + "'");
        }
        else if (s.f != null) s.f.Toast(line, secs);
    }
    void Take(St s) { s.a.pal = true; if (s.mode == Mode.None || s.mode == Mode.Follow) s.home = s.a.transform.position; }
    // who acts: a froggy (f) or a robot (r); a pet that was following keeps its lead froggy in f and goes back to following
    bool Begin(St s, Frog f, Robot r, Mode m)
    {
        bool wasFollow = s.mode == Mode.Follow && s.f != null;
        if (r != null) { s.r = r; if (!wasFollow) s.f = null; } else { s.r = null; s.f = f; }
        Take(s); s.mode = m; s.t = 0f; s.fstage = wasFollow ? 1 : 0;
        return wasFollow;
    }

    public void StartPet(St s, Frog f, Robot r)
    {
        Begin(s, f, r, Mode.Pet);
        Pets++; if (r != null) RobotPets++;
        string n = Nm(s.a), who = r != null ? r.robotName : f != null ? f.nick : "";
        string line;
        switch (s.a.kind)
        {
            case Animal.Kind.Cat: line = n + " purrs and rubs against " + who + " - prrrr!"; break;
            case Animal.Kind.Dog: line = n + " wags like crazy and licks " + who + "'s face!"; break;
            case Animal.Kind.Cow: line = n + " leans in for a scratch - mooo!"; break;
            case Animal.Kind.Horse: line = n + " nuzzles " + who + " and snorts happily"; break;
            case Animal.Kind.Rabbit: line = n + " wiggles its nose - so soft!"; break;
            default: line = n + " loves that!"; break;
        }
        Say(s, line);
        var vc = Sfx.AnimalVoice(s.a.voice ?? s.a.kind.ToString());
        if (vc != null) Sfx.PlayAt(vc, s.a.transform.position + Vector3.up, 0.6f, 30f, 1.05f);
        Debug.Log("ANIMALS57 pet " + n + " by " + who);
    }
    public void StartFeed(St s, Frog f, Robot r)
    {
        Begin(s, f, r, Mode.Eat);
        Feeds++;
        Vector3 me = Who(s), ap = s.a.transform.position, d = ap - me; d.y = 0f; d = d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward;
        Vector3 at = ap - d * (BodyLen(s.a) * 0.5f + 0.25f); at.y = s.a.Ground(at.x, at.z);
        s.prop = FoodProp(s.a, at);
        Say(s, (r != null ? r.robotName : f.nick) + " gives " + Nm(s.a) + " " + Treat(s.a) + " - munch munch!");
        Debug.Log("ANIMALS57 feed " + Nm(s.a) + " " + Treat(s.a));
    }
    public void StartFetch(St s, Frog f, Robot r)
    {
        bool wasFollow = Begin(s, f, r, Mode.Fetch); s.fstage = 0; s.circle = wasFollow ? 1f : 0f;
        Vector3 me = Who(s);
        Vector3 fw = r != null ? r.transform.forward : f.transform.forward; fw.y = 0f; fw = fw.sqrMagnitude > 0.01f ? fw.normalized : Vector3.forward;
        s.ballAt = me + fw * Random.Range(8f, 12f); s.ballAt.y = G57(s.ballAt.x, s.ballAt.z, me.y);
        if (s.prop != null) Destroy(s.prop);
        s.prop = Mats.Prim(PrimitiveType.Sphere, null, me + Vector3.up * 1.0f, Vector3.one * 0.14f, Mats.Lit(new Color(0.85f, 0.95f, 0.2f)));   // tennis ball
        s.prop.name = "Fetch57 ball";
        Fetches++; if (r != null) RobotFetches++;
        Say(s, (r != null ? r.robotName : f.nick) + " throws the ball - go get it, " + Nm(s.a) + "!");
        Debug.Log("ANIMALS57 fetch throw for " + Nm(s.a) + " to " + s.ballAt.ToString("0.0"));
    }
    void StartRide(St s, Frog f)
    {
        if (f.vehicle != null || f.passengerOf != null || f.piggyOf != null) { f.Toast("get out first - then hop on " + Nm(s.a) + "!", 2f); return; }
        Begin(s, f, null, Mode.Ridden);
        float h = TopY(s.a) - s.a.transform.position.y;
        float fh = 1.0f;   // a froggy is about a metre tall; small animals carry a shrunk froggy (fantasy: James rides the cat)
        s.riderScale = Mathf.Clamp(h * 1.25f / fh, 0.35f, 1f);
        s.seatLocal = new Vector3(0f, h - 0.04f, -BodyLen(s.a) * 0.05f);
        f.StartAnimalRide(s.a, s);
        Rides++;
        f.Toast("hold on! " + Nm(s.a) + " carries " + f.nick + " - stick steers, RT runs, " + (f.inputKind == InputKind.Touch || f.inputKind == InputKind.Gamepad ? "A" : "SPACE / E") + " hops off", 3f);
        Debug.Log("ANIMALS57 ride start " + Nm(s.a) + " carries " + f.nick + " seat " + h.ToString("0.00") + " rider scale " + s.riderScale.ToString("0.00"));
    }
    public void RobotRide(St s, Robot r)
    {
        Begin(s, null, r, Mode.Ridden); s.circle = Random.value * 6.28f;
        s.seatLocal = new Vector3(0f, TopY(s.a) - s.a.transform.position.y - 0.05f, 0f);
        RobotRides++; Rides++;
        Debug.Log("ANIMALS57 robot ride " + r.robotName + " on " + Nm(s.a));
    }
    void StartFollow(St s, Frog f)
    {
        s.f = f; s.r = null; s.mode = Mode.Follow; s.t = 0f; s.a.pal = true;
        Follows++;
        f.Toast(Nm(s.a) + " will come along with " + f.nick + " - everywhere!", 2.4f);
        Debug.Log("ANIMALS57 follow " + Nm(s.a) + " -> " + f.nick + " in " + f.world);
    }
    void Stay(St s)
    {
        Animal a = s.a; Vector3 p = a.transform.position;
        s.mode = Mode.None; a.pal = false; s.f = null;
        a.area = new Rect(p.x - 3f, p.z - 3f, 6f, 6f); a.floorY = p.y;
        Vector3 pp = p;
        a.groundFn = (x, z) => G57(x, z, pp.y);
        a.Init(a.area, p.y); a.SyncYaw();
        Debug.Log("ANIMALS57 stay " + Nm(a) + " at " + p.ToString("0.0"));
    }
    public void EndRide(St s)
    {
        if (s.f != null && s.f.rideAnimal == s.a) s.f.EndAnimalRide(true);
        Finish(s);
    }
    void Finish(St s)
    {
        if (s.prop != null) Destroy(s.prop);
        s.prop = null;
        if (s.fstage == 1 && s.f != null && s.mode != Mode.Follow) { s.mode = Mode.Follow; s.r = null; s.t = 0f; return; }   // a follower goes back to following
        Vector3 p = s.a.transform.position;
        if (s.a.area.Contains(new Vector2(p.x, p.z)) || s.a.area.width < 0.01f) { s.mode = Mode.None; s.a.pal = false; s.r = null; s.f = null; s.a.SyncYaw(); return; }
        s.mode = Mode.Return; s.t = 0f;   // walk back into its own pen / room first (Animal.Update clamps to the area)
    }

    // ------------------------------------------------------------------ per-frame
    void Tick(St s, float dt)
    {
        Animal a = s.a; Vector3 p = a.transform.position; Vector3 me = Who(s);
        switch (s.mode)
        {
            case Mode.Pet:
                {
                    Face(a, me, dt); a.happy57 = 1f; a.lookAt57 = me + Vector3.up * 0.6f;
                    if (s.t < 1.2f && (me - p).sqrMagnitude > Sq(BodyLen(a) * 0.5f + 0.7f)) Move(a, me, a.speed, dt, BodyLen(a) * 0.5f + 0.6f);
                    else a.PalAnimate(0f, dt);
                    if (s.t > 3.2f) { a.lookAt57 = null; Finish(s); }
                    break;
                }
            case Mode.Eat:
                {
                    if (s.prop == null) { Finish(s); break; }
                    Vector3 fp = s.prop.transform.position;
                    float dd = Flat(fp - p);
                    if (dd > BodyLen(a) * 0.5f + 0.3f && s.t < 6f) Move(a, fp, a.speed * 1.4f, dt, BodyLen(a) * 0.5f + 0.25f);
                    else
                    {
                        Face(a, fp, dt); a.eat57 = true; a.happy57 = 0.6f; a.PalAnimate(0f, dt);
                        float k = Mathf.Clamp01(1f - (s.t - 1f) / 3f);
                        s.prop.transform.localScale = s.prop.transform.localScale.normalized * Mathf.Max(0.02f, k) * PropSize(a);
                        if (Random.value < dt * 2f) FX.Sparkle(fp + Vector3.up * 0.2f, new Color(1f, 0.6f, 0.75f), 2);
                    }
                    if (s.t > 4.5f) { a.eat57 = false; Say(s, Nm(a) + " licks its lips - yum!", 1.6f); Finish(s); }
                    break;
                }
            case Mode.Fetch: FetchTick(s, dt, me); break;
            case Mode.Ridden: RideTick(s, dt); break;
            case Mode.Return:
                {
                    Vector3 c = new Vector3(a.area.center.x, p.y, a.area.center.y);
                    float d = Move(a, c, a.speed * 1.5f, dt, 0.5f);
                    Vector2 q = new Vector2(a.transform.position.x, a.transform.position.z);
                    if (a.area.Contains(q) && (d < 2f || s.t > 2f) || s.t > 40f) { if (!a.area.Contains(q)) a.transform.position = new Vector3(c.x, a.Ground(c.x, c.z), c.z); s.mode = Mode.None; a.pal = false; s.f = null; s.r = null; a.SyncYaw(); }
                    break;
                }
            case Mode.Follow:
                {
                    Frog f = s.f;
                    if (f == null || !f.isActiveAndEnabled) { Stay(s); break; }
                    if (Behave58.On && Behave58.FollowTick(this, s, f, dt)) break;   // ffu58: truck-bed ride / waits on the shore / own slot
                    Vector3 lp = f.transform.position;
                    float d = Flat(lp - p);
                    if (d > 40f || Mathf.Abs(lp.y - p.y) > 12f && f.vehicle == null) { Arrive(s, false); break; }
                    float want = BodyLen(a) * 0.6f + 1.4f;
                    if (d > want) Move(a, lp, Mathf.Min(a.runSpeed * 1.3f, (d - want) * 1.6f + a.speed), dt, want, f.world == WorldId.Space ? lp.y : float.NaN);
                    else { a.PalAnimate(0f, dt); a.lookAt57 = lp + Vector3.up * 0.5f; }
                    break;
                }
        }
    }

    void FetchTick(St s, float dt, Vector3 me)
    {
        Animal a = s.a; Vector3 p = a.transform.position;
        if (s.prop == null) { Finish(s); return; }
        Transform b = s.prop.transform;
        switch (s.fstage)
        {
            case 0:   // ball in the air (1 s arc), dog already sprinting
                {
                    float k = Mathf.Clamp01(s.t / 1.0f);
                    Vector3 from = me + Vector3.up * 1.0f;
                    b.position = Vector3.Lerp(from, s.ballAt + Vector3.up * 0.07f, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 3.2f;
                    a.happy57 = 1f;
                    Move(a, s.ballAt, a.runSpeed, dt, 0.2f);
                    if (k >= 1f) { s.fstage = 1; FX.Dust(s.ballAt, 0.3f); }
                    break;
                }
            case 1:   // run to the ball, grab it
                {
                    float d = Move(a, s.ballAt, a.runSpeed, dt, 0.25f);
                    if (d < BodyLen(a) * 0.5f + 0.15f || s.t > 12f)
                    {
                        s.fstage = 2; s.t = 0f;
                        Transform hd = a.head != null ? a.head : a.transform;
                        b.SetParent(hd, true); b.localPosition = new Vector3(0f, -0.05f, 0.18f / Mathf.Max(0.2f, hd.lossyScale.z));
                        Debug.Log("ANIMALS57 fetch " + Nm(a) + " got the ball");
                    }
                    break;
                }
            case 2:   // bring it back
                {
                    float want = BodyLen(a) * 0.5f + 0.7f;
                    float d = Move(a, me, a.runSpeed * 0.8f, dt, want);
                    a.happy57 = 1f;
                    if (d <= want + 0.1f || s.t > 15f)
                    {
                        s.fstage = 3; s.t = 0f;
                        b.SetParent(null, true); Vector3 at = p + a.transform.forward * (BodyLen(a) * 0.5f + 0.2f); at.y = G57(at.x, at.z, p.y) + 0.07f; b.position = at;
                        Say(s, Nm(a) + " drops the ball and wags - again! again!", 2f);
                        Debug.Log("ANIMALS57 fetch " + Nm(a) + " brought it back");
                    }
                    break;
                }
            default:  // wait, wag
                Face(a, me, dt); a.happy57 = 1f; a.PalAnimate(0f, dt);
                if (s.t > 1.6f) { if (s.circle > 0.5f) s.fstage = 1; Finish(s); }
                break;
        }
    }

    void RideTick(St s, float dt)
    {
        Animal a = s.a;
        if (s.f == null && s.r == null) { Finish(s); return; }
        if (s.r == null)
        {
            if (s.f.rideAnimal != a) { Finish(s); return; }
            if (DemoSteer.HasValue) { s.rin.move = DemoSteer.Value; s.ryaw = 0f; }
            Vector3 m = new Vector3(s.rin.move.x, 0f, s.rin.move.y);
            if (m.sqrMagnitude > 0.01f)
            {
                Vector3 dir = Quaternion.Euler(0f, s.ryaw, 0f) * m.normalized;
                float sp = Mathf.Min(1f, m.magnitude) * (s.rrun ? a.runSpeed : Mathf.Max(1.4f, a.speed * 1.8f));
                Move(a, a.transform.position + dir * 3f, sp, dt, 0f);
            }
            else a.PalAnimate(0f, dt);
        }
        else
        {
            // a robot rides: the animal walks a slow circle near home, 20 s, then the robot hops off
            s.circle += dt * 0.25f;
            Vector3 c = s.home + new Vector3(Mathf.Cos(s.circle), 0f, Mathf.Sin(s.circle)) * 5f;
            Move(a, c, Mathf.Max(1.2f, a.speed * 1.6f), dt, 0.3f);
            if (s.t > 20f) { Say(s, "thanks for the ride, " + Nm(a) + "!", 2f); Finish(s); }
        }
    }

    // a named pet appears next to its froggy (new world or fell far behind)
    void Arrive(St s, bool newWorld)
    {
        Frog f = s.f; Animal a = s.a; if (f == null || a == null) return;
        if (a.transform.parent != null) a.transform.SetParent(null, true);
        Vector3 fw = f.transform.forward; fw.y = 0f; fw = fw.sqrMagnitude > 0.01f ? fw.normalized : Vector3.forward;
        Vector3 at = f.transform.position - fw * 1.6f + Vector3.Cross(Vector3.up, fw) * 0.9f;
        if (Behave58.On) at = Behave58.FollowSlot(f, a, FollowIndex(s), fw);   // ffu58: each pet its own spot behind James (no pile-up)
        at.y = f.world == WorldId.Space ? f.transform.position.y : G57(at.x, at.z, f.transform.position.y);
        a.transform.position = at;
        a.transform.rotation = Quaternion.LookRotation(fw);
        FX.Dust(at, 0.6f);
        Arrivals++;
        if (newWorld) f.Toast(Nm(a) + " came along to " + Robot.WorldName(Robot.TargetWorld(f)) + "!", 2.2f);
        Debug.Log("ANIMALS57 arrive " + Nm(a) + " with " + f.nick + " in " + f.world + (newWorld ? " (new world)" : " (caught up)") + " at " + at.ToString("0.0"));
    }

    // ffu58: which follower this is (0, 1, 2 ...) among the pets following the same froggy, and the followers list
    public int FollowIndex(St me)
    {
        int i = 0;
        foreach (var s in st.Values) { if (s == me) return i; if (s.a != null && s.f == me.f && s.mode == Mode.Follow) i++; }
        return i;
    }
    public List<St> Followers(Frog f) { var l = new List<St>(); foreach (var s in st.Values) if (s.a != null && s.f == f && s.mode == Mode.Follow) l.Add(s); return l; }
    public void ArrivePublic(St s, bool newWorld) { Arrive(s, newWorld); }

    // ------------------------------------------------------------------ helpers
    static float Sq(float x) { return x * x; }
    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }
    public static float G57(float x, float z, float nearY)
    {
        RaycastHit h;
        if (Physics.Raycast(new Vector3(x, nearY + 2.5f, z), Vector3.down, out h, 6f, Vehicle.GroundMask, QueryTriggerInteraction.Ignore)) return h.point.y;
        return nearY;
    }
    public static float BodyLen(Animal a)
    {
        switch (a.kind) { case Animal.Kind.Cow: return 2f; case Animal.Kind.Horse: return 2f; case Animal.Kind.Dog: return 0.9f; case Animal.Kind.Goat: return 1f; case Animal.Kind.Cat: return 0.6f; default: return 0.35f; }
    }
    static float PropSize(Animal a) { return a.kind == Animal.Kind.Cow || a.kind == Animal.Kind.Horse ? 0.5f : 0.22f; }
    static float TopY(Animal a)
    {
        float y = a.transform.position.y + 0.3f; bool any = false;
        foreach (var r in a.GetComponentsInChildren<Renderer>())
        {
            if (a.head != null && r.transform.IsChildOf(a.head)) continue;
            if (a.tail != null && r.transform.IsChildOf(a.tail)) continue;
            if (r.GetComponentInParent<Frog>() != null) continue;
            y = any ? Mathf.Max(y, r.bounds.max.y) : r.bounds.max.y; any = true;
        }
        return y;
    }
    static GameObject FoodProp(Animal a, Vector3 at)
    {
        var g = new GameObject("Food57 " + Treat(a));
        g.transform.position = at;
        float s = PropSize(a);
        switch (a.kind)
        {
            case Animal.Kind.Cat: Mats.Prim(PrimitiveType.Capsule, g.transform, Vector3.up * 0.03f, new Vector3(0.05f, 0.08f, 0.03f), new Vector3(0f, 0f, 90f), Mats.Lit(new Color(0.95f, 0.55f, 0.3f))); break;
            case Animal.Kind.Dog: Mats.Prim(PrimitiveType.Capsule, g.transform, Vector3.up * 0.03f, new Vector3(0.05f, 0.11f, 0.05f), new Vector3(0f, 0f, 90f), Mats.Lit(new Color(0.95f, 0.92f, 0.85f))); break;
            case Animal.Kind.Cow:
            case Animal.Kind.Goat: Mats.Prim(PrimitiveType.Cube, g.transform, Vector3.up * 0.15f, new Vector3(0.6f, 0.3f, 0.4f), Mats.Lit(new Color(0.85f, 0.75f, 0.4f))); break;
            case Animal.Kind.Chicken: Mats.Prim(PrimitiveType.Cylinder, g.transform, Vector3.up * 0.01f, new Vector3(0.25f, 0.01f, 0.25f), Mats.Lit(new Color(0.8f, 0.7f, 0.45f))); break;
            default: Mats.Prim(PrimitiveType.Capsule, g.transform, Vector3.up * 0.04f, new Vector3(0.05f, 0.12f, 0.05f), new Vector3(0f, 0f, 90f), Mats.Lit(new Color(0.98f, 0.5f, 0.1f))); break;   // carrot
        }
        g.transform.localScale = Vector3.one * (s / 0.22f) * (a.kind == Animal.Kind.Horse ? 0.5f : 1f);
        return g;
    }
    static void Face(Animal a, Vector3 at, float dt)
    {
        Vector3 d = at - a.transform.position; d.y = 0f;
        if (d.sqrMagnitude < 0.01f) return;
        a.transform.rotation = Quaternion.RotateTowards(a.transform.rotation, Quaternion.LookRotation(d), 220f * dt);
    }
    // walk / run towards 'to' until 'stop' metres away; returns the remaining flat distance
    public static float Move(Animal a, Vector3 to, float speed, float dt, float stop, float fixY = float.NaN)
    {
        Vector3 p = a.transform.position, d = to - p; d.y = 0f;
        float dist = d.magnitude;
        if (dist <= stop + 0.02f) { a.PalAnimate(0f, dt); return dist; }
        Quaternion want = Quaternion.LookRotation(d / dist);
        a.transform.rotation = Quaternion.RotateTowards(a.transform.rotation, want, (a.kind == Animal.Kind.Cow || a.kind == Animal.Kind.Horse ? 120f : 240f) * dt);
        float sp = speed * Mathf.Clamp01(Vector3.Dot(a.transform.forward, d / dist) * 0.7f + 0.45f);
        Vector3 step = a.transform.forward * Mathf.Min(sp * dt, dist - stop);
        p += step;
        p.y = float.IsNaN(fixY) ? G57(p.x, p.z, p.y) : fixY;
        a.transform.position = p;
        a.PalAnimate(sp, dt);
        return Flat(to - p);
    }
}

// ---------------------------------------------------------------------------------------------- gaits + idle (Animal)
public partial class Animal
{
    [System.NonSerialized] public float happy57;          // 0..1 tail wag / purr (set while petted, fetched, fed)
    [System.NonSerialized] public bool eat57;             // head down munching
    [System.NonSerialized] public Vector3? lookAt57;      // head turns to it
    float g57c, g57IdleT, g57Pitch, g57Low, g57Speed; int g57Idle; Vector3[] g57Leg;
    // footfall phase per leg (0 front-left, 1 front-right, 2 hind-left, 3 hind-right)
    static readonly float[] OffWalk = { 0.25f, 0.75f, 0f, 0.5f }, OffTrot = { 0f, 0.5f, 0.5f, 0f }, OffGallop = { 0.55f, 0.65f, 0f, 0.1f },
        OffBound = { 0.5f, 0.6f, 0f, 0.06f }, OffHop = { 0.45f, 0.5f, 0f, 0f }, OffBiped = { 0f, 0.5f, 0f, 0.5f };

    float LegLen57()
    {
        if (g57Leg == null || g57Leg.Length == 0) return 0.3f;
        float y = 0f; foreach (var l in g57Leg) y += l.y; y /= g57Leg.Length;
        if (kind == Kind.Cow) return Mathf.Max(0.6f, y); if (kind == Kind.Horse) return Mathf.Max(0.85f, y);
        return Mathf.Max(0.05f, y);
    }

    // true = animated here (Animals57.On)
    bool Gait57(float s, float dt)
    {
        if (!Animals57.On) return false;
        if (g57Leg == null) { g57Leg = new Vector3[legs.Count]; for (int i = 0; i < legs.Count; i++) g57Leg[i] = legs[i].localPosition; g57IdleT = Random.Range(3f, 9f); }
        float t = Time.time + phase;
        g57Speed = Mathf.Lerp(g57Speed, s, Mathf.Clamp01(dt * 8f));
        float v = g57Speed;
        float L = LegLen57();
        float ratio = v / Mathf.Max(0.3f, runSpeed);
        bool quad = legs.Count >= 4;
        int gait = v < 0.08f ? 0 : kind == Kind.Rabbit ? 4 : !quad ? 5 : ratio < 0.38f ? 1 : ratio < 0.72f || kind == Kind.Cow && ratio < 0.9f ? 2 : 3;
        float strideK = gait == 1 ? 2.0f : gait == 2 ? 2.7f : gait == 3 ? 3.8f : gait == 4 ? 3f : 2.2f;
        float stride = L * strideK + 0.06f;
        float freq = Mathf.Clamp(v / stride, 0.35f, 4.5f);
        if (gait > 0) g57c = (g57c + freq * dt) % 1f;
        float duty = gait == 1 ? 0.62f : gait == 2 ? 0.46f : gait == 3 ? 0.36f : gait == 4 ? 0.3f : 0.55f;
        // leg order: 0 front-left, 1 front-right, 2 hind-left, 3 hind-right
        float[] off;
        switch (gait)
        {
            case 1: off = OffWalk; break;                                                                    // lateral-sequence walk
            case 2: off = OffTrot; break;                                                                    // diagonal trot
            case 3: off = kind == Kind.Horse || kind == Kind.Cow ? OffGallop : OffBound; break;              // rotary gallop / bound
            case 4: off = OffHop; break;                                                                     // rabbit hop
            default: off = OffBiped; break;                                                                  // biped (chicken) / idle
        }
        float A = Mathf.Clamp(Mathf.Atan2(stride * 0.5f, Mathf.Max(0.05f, L) * 1.6f) * Mathf.Rad2Deg, 10f, 42f);

        // ---- idle behaviour (standing still)
        if (gait == 0)
        {
            g57IdleT -= dt;
            if (g57IdleT <= 0f)
            {
                g57IdleT = Random.Range(4f, 11f);
                int n = kind == Kind.Cat || kind == Kind.Dog ? 5 : kind == Kind.Horse || kind == Kind.Cow ? 4 : 2;
                g57Idle = Random.Range(0, n);
                if (kind == Kind.Cat && petName.Length > 0 && Random.value < 0.3f) g57Idle = 2;   // cats love lying down
            }
        }
        else g57Idle = 0;
        int idle = gait == 0 ? g57Idle : 0;
        // cats / dogs: 1 sit, 2 lie down, 3 look around, 4 groom / scratch;  horses: 1 rest a hind hoof, 2 head toss, 3 lie (cows) / look
        bool sit = (kind == Kind.Cat || kind == Kind.Dog) && idle == 1 && happy57 < 0.5f && !eat57;
        bool lie = ((kind == Kind.Cat || kind == Kind.Dog) && idle == 2 || kind == Kind.Cow && idle == 3) && happy57 < 0.5f && !eat57;
        float lowW = lie ? 1f : sit ? 0.45f : 0f;
        g57Low = Mathf.MoveTowards(g57Low, lowW, dt * 1.4f);
        float pitchW = sit ? -18f : 0f;

        for (int i = 0; i < legs.Count && i < g57Leg.Length; i++)
        {
            float ang = 0f, lift = 0f;
            if (gait > 0)
            {
                float u = (g57c + off[i % off.Length]) % 1f;
                if (u < duty) ang = Mathf.Lerp(-A, A, u / duty);                       // stance: foot planted, leg sweeps back
                else { float w = (u - duty) / (1f - duty); w = w * w * (3f - 2f * w); ang = Mathf.Lerp(A, -A, w); lift = Mathf.Sin(w * Mathf.PI) * L * 0.1f; }
                if (gait == 4 && i < 2) ang *= 0.6f;
            }
            else
            {
                bool hind = i >= 2;
                if (kind == Kind.Horse && idle == 1 && i == 3) { ang = 10f; lift = L * 0.06f; }   // resting hind hoof
                if (sit && hind) ang = -75f * Mathf.Clamp01(g57Low / 0.45f);
                if (g57Low > 0.01f && (lie || !hind)) ang = Mathf.Lerp(ang, hind ? -80f : 80f, lie ? g57Low : 0f);
                if (sit && !hind) ang = 6f;
            }
            legs[i].localPosition = g57Leg[i] + Vector3.up * lift;
            if (Behave58.On && gait == 0 && lie && g57Low > 0.01f)
            {
                // ffu58: lying = legs folded UNDER the body (a cat's loaf, a dog's sphinx front paws), not stuck out flat
                // front / back (that read as an animal tipped over on its side, Tigy in the ffu57 shots)
                bool hindL = i >= 2; float k = g57Low;
                float fold = kind == Kind.Dog && !hindL ? 70f : hindL ? -30f : 30f;
                legs[i].localRotation = Quaternion.Euler(fold * k, 0f, 0f);
                legs[i].localScale = new Vector3(1f, Mathf.Lerp(1f, kind == Kind.Dog && !hindL ? 0.85f : 0.3f, k), 1f);
                continue;
            }
            if (Behave58.On && legs[i].localScale.y != 1f) legs[i].localScale = Vector3.one;
            legs[i].localRotation = Quaternion.Euler(ang, 0f, 0f);
        }
        if (body != null)
        {
            float bob, pitch;
            if (gait == 1) { bob = -Mathf.Abs(Mathf.Sin(g57c * Mathf.PI * 2f)) * L * 0.03f; pitch = Mathf.Sin(g57c * Mathf.PI * 4f) * 1.2f; }
            else if (gait == 2) { bob = Mathf.Abs(Mathf.Sin(g57c * Mathf.PI * 2f)) * L * 0.07f; pitch = 0f; }
            else if (gait == 3) { bob = Mathf.Max(0f, Mathf.Sin(g57c * Mathf.PI * 2f)) * L * 0.14f; pitch = Mathf.Sin(g57c * Mathf.PI * 2f + 0.8f) * 7f; }
            else if (gait == 4) { float u = g57c; bob = u > 0.3f ? Mathf.Sin((u - 0.3f) / 0.7f * Mathf.PI) * L * 1.2f : 0f; pitch = (u > 0.3f ? -8f : 6f); }
            else if (gait == 5) { bob = Mathf.Abs(Mathf.Sin(g57c * Mathf.PI * 2f)) * 0.02f; pitch = Mathf.Sin(g57c * Mathf.PI * 2f) * 3f; }
            else { bob = 0f; pitch = 0f; }
            g57Pitch = Mathf.Lerp(g57Pitch, pitch + pitchW, Mathf.Clamp01(dt * 10f));
            float breathe = 1f + Mathf.Sin(t * (kind == Kind.Cow || kind == Kind.Horse ? 1.6f : kind == Kind.Rabbit || kind == Kind.Chicken ? 5f : 2.6f) * (happy57 > 0.5f && kind == Kind.Dog ? 3f : 1f)) * 0.014f;
            body.localPosition = new Vector3(0f, bob - g57Low * L * 0.75f, 0f);
            body.localRotation = Quaternion.Euler(g57Pitch, 0f, 0f);
            body.localScale = frozen ? new Vector3(1f, 0.8f, 1f) : new Vector3(1f + (breathe - 1f) * 0.6f, breathe, 1f);
        }
        if (head != null)
        {
            bool grazing = v < 0.05f && (grazeT > 0f || eat57) && (kind == Kind.Cow || kind == Kind.Horse || kind == Kind.Goat || kind == Kind.Rabbit || kind == Kind.Chicken || eat57);
            float nod, yawH = Mathf.Sin(t * 0.7f) * 12f, roll = 0f;
            if (grazing) nod = 40f + Mathf.Sin(t * (eat57 ? 9f : 3f)) * (eat57 ? 4f : 6f);                           // head down, chewing
            else if (gait == 1 && (kind == Kind.Horse || kind == Kind.Cow)) nod = Mathf.Sin(g57c * Mathf.PI * 4f) * 6f;   // the walk's head nod
            else if (gait >= 2) nod = Mathf.Sin(g57c * Mathf.PI * 2f) * 4f - 4f;
            else nod = Mathf.Sin(t * 1.3f) * 4f;
            if (gait == 0 && idle == 3) yawH = Mathf.Sin(t * 0.9f) * 50f;                                             // look around
            if (gait == 0 && kind == Kind.Horse && idle == 2) nod = -18f + Mathf.Sin(t * 6f) * 12f;                   // head toss
            if (gait == 0 && kind == Kind.Cat && idle == 4) { nod = 35f; yawH = 70f; roll = Mathf.Sin(t * 8f) * 6f; }  // grooming
            if (gait == 0 && kind == Kind.Dog && idle == 4) { roll = Mathf.Sin(t * 14f) * 9f; }                      // ear scratch
            if (gait == 0 && kind == Kind.Cow && idle < 3 && !grazing) roll = Mathf.Sin(t * 4f) * 2f;               // chewing the cud
            if (lookAt57.HasValue && !grazing)
            {
                Vector3 d = transform.InverseTransformPoint(lookAt57.Value); d.y = 0f;
                if (d.sqrMagnitude > 0.01f) yawH = Mathf.Clamp(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -60f, 60f);
                nod = -8f;
            }
            if (happy57 > 0.5f && kind == Kind.Cat) roll = Mathf.Sin(t * 2f) * 10f;                                 // cheek rub
            head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(nod, yawH, roll), Mathf.Clamp01(dt * 6f));
        }
        if (tail != null)
        {
            float tx, ty;
            switch (kind)
            {
                case Kind.Dog: tx = -35f - happy57 * 15f; ty = Mathf.Sin(t * (6f + happy57 * 16f)) * (12f + happy57 * 28f); break;
                case Kind.Cat: tx = v > 0.1f || happy57 > 0.5f ? -70f : -25f; ty = Mathf.Sin(t * (happy57 > 0.5f ? 1.2f : 1.8f)) * 20f; break;
                case Kind.Horse:
                case Kind.Cow: { float flick = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 0.55f)), 8f); tx = 12f + flick * 20f; ty = Mathf.Sin(t * 9f) * 30f * flick + Mathf.Sin(t * 0.8f) * 4f; break; }
                default: tx = 20f; ty = Mathf.Sin(t * 3f) * 20f; break;
            }
            tail.localRotation = Quaternion.Euler(tx, ty, 0f);
        }
        if (happy57 > 0f) happy57 = Mathf.MoveTowards(happy57, 0f, dt * 0.4f);
        return true;
    }
}

// ---------------------------------------------------------------------------------------------- frogs ride animals
public partial class Frog
{
    [System.NonSerialized] public Animal rideAnimal;
    [System.NonSerialized] public Animals57.St rideSt;
    public void StartAnimalRide(Animal a, Animals57.St s)
    {
        if (vehicle != null) ExitVehicle();
        LeavePassenger();
        rideAnimal = a; rideSt = s;
        cc.enabled = false;
        transform.SetParent(a.transform, true);
        transform.localScale = Vector3.one * s.riderScale;
        AnimalRidePlace();
    }
    void AnimalRidePlace()
    {
        transform.localPosition = rideSt.seatLocal;
        transform.localRotation = Quaternion.identity;
    }
    public void EndAnimalRide(bool say)
    {
        Animal a = rideAnimal;
        rideAnimal = null; rideSt = null;
        transform.SetParent(null, true);
        transform.localScale = Vector3.one;
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        Vector3 at = a != null ? a.transform.position + a.transform.right * (Animals57.BodyLen(a) * 0.4f + 0.8f) + Vector3.up * 0.4f : transform.position;
        cc.enabled = true;
        Teleport(at);
        if (say && a != null) Toast("thanks for the ride, " + (a.petName.Length > 0 ? a.petName : "buddy") + "!", 1.8f);
    }
    // from Frog.Update: true = riding (input goes to the animal)
    bool AnimalRideTick(float dt)
    {
        if (rideAnimal == null) return false;
        if (rideSt == null || rideSt.mode != Animals57.Mode.Ridden || rideSt.f != this || !rideAnimal.isActiveAndEnabled) { EndAnimalRide(false); return false; }
        bool off = human && exitCool <= 0f && (input.hop || input.use);
        rideSt.rin = input; rideSt.ryaw = camYaw; rideSt.rrun = input.gas > 0.5f || input.brake > 0.5f || Sprinting;
        AnimalRidePlace();
        model.Animate(0f, false, true, false, dt);
        prompt = "Riding " + (rideAnimal.petName.Length > 0 ? rideAnimal.petName : "the " + rideAnimal.kind.ToString().ToLower()) + " - stick steers, RT runs, " + (inputKind == InputKind.Gamepad || inputKind == InputKind.Touch ? "A" : "SPACE / E") + " hops off";
        if (off) { var s = rideSt; EndAnimalRide(true); exitCool = 0.4f; if (Animals57.I != null && s != null) Animals57.I.EndRide(s); }
        input = new PIn();
        return true;
    }
}
