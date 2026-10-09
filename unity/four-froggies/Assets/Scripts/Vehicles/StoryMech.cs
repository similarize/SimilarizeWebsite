using System.Collections.Generic;
using UnityEngine;

// Giant "story mechs" from the 2.5D Four Froggies (games/four-froggies canon.js: 10 / 100 / 1000 / trillion-story
// bands, colours a5b4fc / 67e8f9 / fcd34d / ffd700). Lineup (built in RanchLife, 11 mechs): every froggy has a 10-story and
// a 100-story mech, James and Bubbles each have a 1000-story, and only James has the trillion-story. Trimmed in the
// frog's colour; frogs pilot their own, the cats and dogs may borrow any (ffu14).
// Scale is stylised: 10-story 12 m, 100-story 30 m, 1000-story 60 m, trillion-story 160 m.
// ffu14: quick and responsive for little kids (weight from camera shake, footfall thuds, dust); JUMP (tap UP), back
// ROCKETS (hold UP) with visible flames + AFTERBURNER (BOOST), climb high enough and it reaches space; CHEST cannon +
// chest missiles that do real damage; health bar, knock-out (pilot pops out unhurt, mech respawns at its spot with a
// 20 s spawn shield that starts when the pilot climbs back in and holds while it waits there empty).
public class StoryMech : Vehicle
{
    public int owner;            // froggy character index (0..3)
    public int band;             // 0..3
    public float height;
    Transform hips, legL, legR, armL, armR, torso, head;
    float yaw, phase, speed, stepSide, fireCool, missileCool, turnVel;
    public static readonly string[] BandName = { "10-story", "100-story", "1000-story", "trillion-story" };
    public static readonly float[] BandH = { 12f, 30f, 60f, 160f };
    static readonly string[] BandHex = { "#a5b4fc", "#67e8f9", "#fcd34d", "#ffd700" };
    public static readonly float[] BandHP = { 100f, 160f, 240f, 400f };
    public static readonly List<StoryMech> AllMechs = new List<StoryMech>();

    // flight
    float vy, upHeldT, airT, flameK, boostK, landCool;
    bool grounded = true, rocketing;
    Vector3 airVel;
    public float Altitude { get; private set; }
    public const float SpaceAlt = 2400f;          // reach this above the ranch with the rockets -> space
    public float SpaceK { get { return Mathf.Clamp01((Altitude - 300f) / (SpaceAlt - 300f)); } }

    // combat
    public float hp, maxHp;
    public bool wrecked;
    float wreckT, shieldT = -1f;   // shieldT: >0 counting down (pilot aboard), -1 = parked-at-spawn shield, 0 = none
    bool atSpawn = true;
    float netSendT, lastSentHp = -1f; bool lastSentWreck; int lastSentShield = -99;
    Renderer[] rends; Collider[] cols;
    Transform shieldGo, barRoot, barFill; TextMesh shieldText, barText;
    Material shieldMat;
    readonly Transform[] flames = new Transform[2];
    Light jetLight;
    AudioSource jetLoop;

    public override string HelpLine
    {
        get
        {
            return "L-stick walk | UP tap jump, hold ROCKETS | BOOST afterburner | FIRE chest cannon | MSL chest missiles | A climb out";
        }
    }
    public override string[] TouchSet { get { return new[] { "A", "FIRE", "MSL", "JUMP", "BOOST" }; } }

    public override bool CanEnter(Frog f) { return !wrecked && (f.IsPet || f.charId == owner); }
    public override string DeniedLine
    {
        get
        {
            if (wrecked) return Title + " - knocked out, back at its spot in " + Mathf.CeilToInt(wreckT) + " s";
            return Froggies.Names[owner] + "'s " + BandName[band] + " mech - only " + Froggies.Names[owner] + " (or a cat or dog) can pilot it";
        }
    }
    public bool Shielded { get { return wrecked || shieldT > 0f || (shieldT < 0f && driver == null && atSpawn); } }
    public string StatusLine
    {
        get
        {
            string s = "HP " + Mathf.CeilToInt(hp) + "/" + Mathf.RoundToInt(maxHp);
            if (shieldT > 0f) s += "  SHIELD " + Mathf.CeilToInt(shieldT) + "s";
            if (Altitude > 5f) s += "  alt " + Mathf.RoundToInt(Altitude) + " m" + (Altitude > 300f ? "  (space at " + Mathf.RoundToInt(SpaceAlt) + ")" : "");
            return s;
        }
    }

    protected override bool OutOfWorld(Vector3 p) { return false; }

    // demo / probe helpers
    public static float DemoClimb = 1f;
    public static float DtCap = 0.05f;   // demo / probe: SwiftShader runs ~2 fps, so the mech demos raise this
    public Vector3 HomeOrNow { get { return transform.position; } }
    public void DemoPlace(Vector3 p, float yawDeg)
    {
        p.y = Ranch.GY(p.x, p.z);
        transform.position = p; yaw = yawDeg; transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);
        if (rb != null) { rb.position = p; rb.rotation = transform.rotation; }
        atSpawn = false;
    }

    public static StoryMech Build(int owner, int band, Vector3 groundPos, float yawDeg)
    {
        float H = BandH[band];
        var go = new GameObject(Froggies.Names[owner] + " " + BandName[band] + " mech");
        go.transform.position = groundPos;
        go.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);
        var m = go.AddComponent<StoryMech>();
        m.owner = owner; m.band = band; m.height = H; m.yaw = yawDeg;
        m.Title = Froggies.Names[owner] + "'s " + BandName[band] + " mech";
        m.EnterVerb = "pilot the " + BandName[band] + " mech";
        m.engineKind = 6;
        m.flyer = true;   // no flip rescue; camera follows the body heading
        m.SetupBodyPublic(1000f, new Vector3(0f, H * 0.32f, 0f), new Vector3(H * 0.42f, H * 0.64f, H * 0.26f), Vector3.zero);
        m.rb.isKinematic = true; m.rb.useGravity = false;
        m.rb.interpolation = RigidbodyInterpolation.None;
        m.rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        m.camDistance = H * 1.15f + 6f; m.camHeight = H * 0.75f;
        m.showDriver = false;
        m.maxHp = m.hp = BandHP[band];

        Material body = Mats.Shiny(Mats.Hex(BandHex[band]));
        Material trim = Mats.Shiny(Froggies.Color(owner));
        Material dark = Mats.Lit(new Color(0.15f, 0.16f, 0.18f));
        Material glow = Mats.Unlit(Color.Lerp(Froggies.Color(owner), Color.white, 0.5f));
        Transform t = go.transform;
        LBPack pk = LBPack.Get("storymech");
        bool packed = pk != null && pk.Has("hips") && pk.Has("torso") && pk.Has("legL") && pk.Has("armL") && pk.Has("head");
        if (packed)
        {
            // armoured froggy mech mesh (work/lb-gfx/ff/build_storymech.py; ffu14: solid collar + neck, caps face out)
            Color bandC = Mats.Hex(BandHex[band]);
            System.Func<string, Transform, Transform> P = (part, node) => pk.Spawn(part, node, -pk.parts[part].pivot * H, H, bandC);
            Vector3 hipsP = pk.parts["hips"].pivot, torsoP = pk.parts["torso"].pivot;
            m.hips = Mats.Node(t, "Hips", hipsP * H); P("hips", m.hips);
            m.legL = Mats.Node(m.hips, "LegL", (pk.parts["legL"].pivot - hipsP) * H); P("legL", m.legL);
            m.legR = Mats.Node(m.hips, "LegR", (pk.parts["legR"].pivot - hipsP) * H); P("legR", m.legR);
            m.torso = Mats.Node(m.hips, "Torso", (torsoP - hipsP) * H); P("torso", m.torso);
            m.armL = Mats.Node(m.torso, "ArmL", (pk.parts["armL"].pivot - torsoP) * H); P("armL", m.armL);
            m.armR = Mats.Node(m.torso, "ArmR", (pk.parts["armR"].pivot - torsoP) * H); P("armR", m.armR);
            m.head = Mats.Node(m.torso, "Head", (pk.parts["head"].pivot - torsoP) * H); P("head", m.head);
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                if (r.gameObject.name == "trim") r.sharedMaterial = trim;
                else if (r.gameObject.name == "glow") r.sharedMaterial = glow;
            }
            if (band < 2) foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        else
        {
            m.hips = Mats.Node(t, "Hips", new Vector3(0f, H * 0.42f, 0f));
            for (int s = -1; s <= 1; s += 2)
            {
                Transform leg = Mats.Node(m.hips, s < 0 ? "LegL" : "LegR", new Vector3(H * 0.1f * s, 0f, 0f));
                Mats.Prim(PrimitiveType.Cube, leg, new Vector3(0f, -H * 0.11f, 0f), new Vector3(H * 0.11f, H * 0.22f, H * 0.12f), body);
                Mats.Prim(PrimitiveType.Cube, leg, new Vector3(0f, -H * 0.33f, 0f), new Vector3(H * 0.1f, H * 0.18f, H * 0.11f), trim);
                Mats.Prim(PrimitiveType.Cube, leg, new Vector3(0f, -H * 0.41f, H * 0.04f), new Vector3(H * 0.14f, H * 0.03f, H * 0.22f), dark);
                if (s < 0) m.legL = leg; else m.legR = leg;
            }
            m.torso = Mats.Node(m.hips, "Torso", new Vector3(0f, H * 0.04f, 0f));
            Mats.Prim(PrimitiveType.Cube, m.torso, new Vector3(0f, H * 0.2f, 0f), new Vector3(H * 0.38f, H * 0.32f, H * 0.22f), body);
            for (int s = -1; s <= 1; s += 2)
            {
                Transform arm = Mats.Node(m.torso, s < 0 ? "ArmL" : "ArmR", new Vector3(H * 0.24f * s, H * 0.3f, 0f));
                Mats.Prim(PrimitiveType.Cube, arm, new Vector3(0f, -H * 0.15f, 0f), new Vector3(H * 0.08f, H * 0.3f, H * 0.09f), body);
                if (s < 0) m.armL = arm; else m.armR = arm;
            }
            m.head = Mats.Node(m.torso, "Head", new Vector3(0f, H * 0.4f, 0f));
            Mats.Prim(PrimitiveType.Cube, m.head, new Vector3(0f, H * 0.04f, 0f), new Vector3(H * 0.16f, H * 0.12f, H * 0.14f), body);
        }
        m.seat = Mats.Node(m.head, "Seat", Vector3.zero);
        m.seatScale = 0.5f;
        Mats.SetLayer(go, VehicleLayer);
        // name plate on the chest
        var tag = new GameObject("Plate");
        tag.transform.SetParent(m.torso, false);
        tag.transform.localPosition = packed ? new Vector3(0f, H * 0.115f, H * 0.088f) : new Vector3(0f, H * 0.12f, H * 0.115f);
        var tm = tag.AddComponent<TextMesh>();
        tm.text = Froggies.Names[owner].ToUpper() + "\n" + BandName[band].ToUpper();
        tm.font = UIK.Font; tm.fontSize = 64; tm.characterSize = H * 0.004f; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
        tag.GetComponent<MeshRenderer>().sharedMaterial = UIK.Font != null ? UIK.Font.material : null;
        m.BuildJets(packed);
        m.BuildShieldAndBar();
        m.rends = go.GetComponentsInChildren<Renderer>(true);
        m.cols = go.GetComponentsInChildren<Collider>(true);
        AllMechs.Add(m);
        return m;
    }

    // ---------- back rockets: two nozzles on the back pack, flame cones (outer orange, inner white-yellow core) ----------
    void BuildJets(bool packed)
    {
        float H = height;
        Material outer = Mats.Unlit(new Color(1f, 0.55f, 0.12f)), core = Mats.Unlit(new Color(1f, 0.95f, 0.7f));
        Material nozzle = Mats.Steel(new Color(0.35f, 0.36f, 0.4f));
        for (int s = 0; s < 2; s++)
        {
            float x = (s == 0 ? -1f : 1f) * 0.055f * H;
            Vector3 np = packed ? new Vector3(x, (0.565f - 0.46f) * H, -0.12f * H) : new Vector3(x, H * 0.12f, -H * 0.12f);
            var nz = Mats.Prim(PrimitiveType.Cylinder, torso, np + new Vector3(0f, -0.01f * H, 0f), new Vector3(0.05f * H, 0.012f * H, 0.05f * H), nozzle);
            nz.name = "Nozzle";
            Transform f = Mats.Node(torso, "Flame", np + new Vector3(0f, -0.02f * H, 0f));
            var o = Mats.Prim(PrimitiveType.Capsule, f, new Vector3(0f, -0.5f, 0f), new Vector3(0.6f, 0.55f, 0.6f), outer);
            var c = Mats.Prim(PrimitiveType.Capsule, f, new Vector3(0f, -0.42f, 0f), new Vector3(0.32f, 0.45f, 0.32f), core);
            o.GetComponent<Renderer>().shadowCastingMode = c.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            f.localScale = Vector3.zero;
            flames[s] = f;
        }
        var lg = new GameObject("JetLight");
        lg.transform.SetParent(torso, false);
        lg.transform.localPosition = new Vector3(0f, 0f, -0.2f * H);
        jetLight = lg.AddComponent<Light>();
        jetLight.type = LightType.Point; jetLight.color = new Color(1f, 0.6f, 0.25f); jetLight.range = H * 1.2f; jetLight.intensity = 0f; jetLight.shadows = LightShadows.None;
        jetLight.enabled = false;
    }

    void BuildShieldAndBar()
    {
        float H = height;
        shieldMat = new Material(Mats.GlassTint(new Color(0.35f, 0.85f, 1f, 0.28f)));
        var sg = Mats.Prim(PrimitiveType.Sphere, transform, new Vector3(0f, H * 0.5f, 0f), new Vector3(H * 0.75f, H * 1.12f, H * 0.75f), shieldMat);
        sg.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        shieldGo = sg.transform; shieldGo.gameObject.SetActive(false);
        // health bar + shield countdown float over the head and face each camera (Camera.onPreCull)
        barRoot = Mats.Node(transform, "HealthBar", new Vector3(0f, H * 1.08f + 1.5f, 0f));
        float w = Mathf.Max(4f, H * 0.36f), h = Mathf.Max(0.45f, H * 0.026f);
        Mats.Prim(PrimitiveType.Cube, barRoot, Vector3.zero, new Vector3(w + h * 0.4f, h * 1.4f, 0.05f), Mats.Unlit(new Color(0.05f, 0.05f, 0.06f)));
        var fp = Mats.Node(barRoot, "FillPivot", new Vector3(-w * 0.5f, 0f, -0.06f));
        var fill = Mats.Prim(PrimitiveType.Cube, fp, new Vector3(w * 0.5f, 0f, 0f), new Vector3(w, h, 0.05f), new Material(Mats.Unlit(new Color(0.3f, 1f, 0.35f))));
        barFill = fp;
        barFill.GetChild(0).name = "Fill";
        var tg = new GameObject("BarText"); tg.transform.SetParent(barRoot, false); tg.transform.localPosition = new Vector3(0f, h * 1.6f, -0.06f);
        barText = tg.AddComponent<TextMesh>(); barText.font = UIK.Font; barText.fontSize = 64; barText.characterSize = h * 0.08f; barText.anchor = TextAnchor.LowerCenter; barText.alignment = TextAlignment.Center;
        tg.GetComponent<MeshRenderer>().sharedMaterial = UIK.Font != null ? UIK.Font.material : null;
        shieldText = barText;
        foreach (var r in barRoot.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (!hooked) { hooked = true; Camera.onPreCull += FaceBars; }
    }
    static bool hooked;
    static void FaceBars(Camera c)
    {
        if (c == null || (c.cullingMask & (1 << VehicleLayer)) == 0) return;
        Vector3 cp = c.transform.position;
        foreach (var m in AllMechs)
        {
            if (m == null || m.barRoot == null) continue;
            Vector3 d = m.barRoot.position - cp;
            // per camera: hidden when this camera is right on top of the mech (its own pilot's view) - the HUD shows it
            bool show = m.barWanted && d.sqrMagnitude > m.height * m.height * 4.5f;
            if (m.barRoot.gameObject.activeSelf != show) m.barRoot.gameObject.SetActive(show);
            if (show && d.sqrMagnitude > 1e-4f) m.barRoot.rotation = Quaternion.LookRotation(d, Vector3.up);
        }
    }

    void Start()
    {
        // cull the small mechs when they are tiny on screen
        var lod = gameObject.AddComponent<LODGroup>();
        var rs = new List<Renderer>();
        foreach (var r in GetComponentsInChildren<Renderer>())
            if (r != null && r.GetComponent<TextMesh>() == null && !r.transform.IsChildOf(barRoot) && r.transform != shieldGo && !r.transform.IsChildOf(flames[0]) && !r.transform.IsChildOf(flames[1])) rs.Add(r);
        lod.SetLODs(new[] { new LOD(band == 0 ? 0.02f : 0.004f, rs.ToArray()) });
        spawnPos = transform.position; spawnRot = transform.rotation;
    }

    protected new void OnDestroy() { AllMechs.Remove(this); Vehicle.All.Remove(this); }

    public override void OnEnter()
    {
        base.OnEnter();
        yaw = transform.eulerAngles.y;
        if (shieldT < 0f && atSpawn && hpWasRespawned) shieldT = 20f;   // spawn protection counts from boarding
        hpWasRespawned = false;
        if (driver != null) driver.Toast(Title + "!  UP = jump / hold rockets, BOOST = afterburner, FIRE / MSL from the chest", 3.5f);
    }
    bool hpWasRespawned;

    public override void OnExit()
    {
        base.OnExit();
        if (jetLoop != null) jetLoop.volume = 0f;
    }

    // ---------------- damage ----------------
    // authority: the pilot's device; an empty mech belongs to the host (offline: this device)
    public bool Authority
    {
        get
        {
            if (Net.I == null || !Net.I.Online) return true;
            if (driver != null) return !driver.netPuppet;
            return Net.I.IsHost;
        }
    }

    public void Damage(float d, Vector3 at, Vehicle by)
    {
        if (wrecked || d <= 0f || by == this) return;
        if (Shielded) { FX.Sparkle(at, new Color(0.5f, 0.9f, 1f), 8); shieldFlash = 1f; return; }
        if (!Authority) return;
        hp = Mathf.Max(0f, hp - d);
        hitFlash = 1f;
        if (driver != null && driver.human) Game.Shake(transform.position + Vector3.up * height * 0.5f, 0.35f);
        if (hp <= 0f) KnockOut(by);
        SendNet(true);
    }
    float hitFlash, shieldFlash;
    bool barWanted;

    void KnockOut(Vehicle by)
    {
        if (wrecked) return;
        wrecked = true; wreckT = 12f; hp = 0f; atSpawn = false;
        Frog pilot = driver;
        Vector3 c = transform.position + Vector3.up * height * 0.5f;
        // big kid-friendly kaboom: fireballs, sparks, smoke, debris; the pilot pops out unhurt
        Boom.At(c, height * 0.35f + 4f, Mathf.Clamp(height / 20f, 1f, 3f), 0f, this);
        for (int i = 0; i < 4; i++) FX.Boom(c + Random.insideUnitSphere * height * 0.3f, Mathf.Clamp(height / 15f, 1f, 4f));
        Sfx.PlayAt(Sfx.Boom, c, 1f, 200f + height * 2f, 0.6f);
        Debris(c);
        if (pilot != null)
        {
            pilot.ExitVehicle();
            Vector3 p = transform.position + transform.right * (height * 0.25f + 3f);
            p.y = Ranch.GY(p.x, p.z) + 0.5f;
            pilot.Teleport(p);
            pilot.Knock(Vector3.up * 9f + transform.right * 4f);
            pilot.Toast("Your mech got knocked out! You're fine - walk back to its spot, it's back in 12 s", 4.5f);
        }
        if (by != null && by.driver != null && by.driver != pilot) by.driver.Toast("You knocked out " + Title + "!", 3f);
        foreach (var r in rends) if (r != null && !r.transform.IsChildOf(barRoot)) r.enabled = false;
        foreach (var col in cols) if (col != null) col.enabled = false;
        if (jetLight != null) jetLight.enabled = false;
        speed = 0f; vy = 0f; airVel = Vector3.zero;
        Debug.Log("Mech KO: " + Title);
    }

    void Debris(Vector3 c)
    {
        Material m = Mats.Lit(Color.Lerp(Mats.Hex(BandHex[band]), Color.black, 0.45f));
        int n = Look.Mobile ? 6 : 10;
        for (int i = 0; i < n; i++)
        {
            var g = Mats.Prim(PrimitiveType.Cube, null, c + Random.insideUnitSphere * height * 0.25f, Vector3.one * height * Random.Range(0.05f, 0.11f), m, true);
            g.layer = PropLayer;
            var rb2 = g.AddComponent<Rigidbody>();
            rb2.mass = 50f;
            rb2.velocity = Random.onUnitSphere * height * 0.4f + Vector3.up * height * 0.5f;
            rb2.angularVelocity = Random.insideUnitSphere * 6f;
            Destroy(g, 9f);
        }
    }

    void Respawn()
    {
        wrecked = false; hp = maxHp; shieldT = -1f; atSpawn = true; hpWasRespawned = true;
        transform.position = spawnPos; transform.rotation = spawnRot; yaw = spawnRot.eulerAngles.y;
        rb.position = spawnPos; rb.rotation = spawnRot;
        foreach (var r in rends) if (r != null) r.enabled = true;
        foreach (var col in cols) if (col != null) col.enabled = true;
        foreach (var f in flames) if (f != null) f.localScale = Vector3.zero;
        vy = 0f; airVel = Vector3.zero; grounded = true; Altitude = 0f;
        FX.Sparkle(transform.position + Vector3.up * height * 0.5f, new Color(0.5f, 0.9f, 1f), 30);
        Sfx.PlayAt(Sfx.Confirm != null ? Sfx.Confirm : Sfx.Pickup, transform.position, 0.8f, 120f, 0.7f);
        SendNet(true);
    }

    // ---------------- net (ffu14): hp / knocked out / shield from the authority device ----------------
    void SendNet(bool now)
    {
        if (Net.I == null || !Net.I.Online || !Authority) return;
        int sh = shieldT > 0f ? Mathf.CeilToInt(shieldT) : shieldT < 0f ? -1 : 0;
        if (!now && Mathf.Abs(hp - lastSentHp) < 0.5f && wrecked == lastSentWreck && sh == lastSentShield) return;
        if (!now && Time.unscaledTime - netSendT < 0.2f) return;
        netSendT = Time.unscaledTime; lastSentHp = hp; lastSentWreck = wrecked; lastSentShield = sh;
        Net.I.SendMech(this, hp, wrecked, sh);
    }
    public void NetState(float h, bool wr, int sh)
    {
        if (Authority) return;
        if (wr && !wrecked) { hp = 0f; KnockOut(null); return; }
        if (!wr && wrecked) Respawn();
        if (h < hp - 0.5f) hitFlash = 1f;
        hp = h;
        shieldT = sh;
    }

    // ---------------- per frame ----------------
    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, DtCap);
        float H = height;
        TickBar(dt);
        if (wrecked)
        {
            wreckT -= dt;
            if (Random.value < dt * 6f) FX.Smoke(transform.position + new Vector3(Random.Range(-1f, 1f) * H * 0.15f, 1f, Random.Range(-1f, 1f) * H * 0.15f), H * 0.12f + 2f, new Color(0.2f, 0.2f, 0.2f, 0.7f));
            if (Random.value < dt * 10f) FX.Flame(transform.position + Random.insideUnitSphere * H * 0.12f + Vector3.up * 1.5f, Vector3.up);
            if (wreckT <= 0f && Authority) Respawn();
            return;
        }
        if (shieldT > 0f) { shieldT -= dt; if (shieldT <= 0f) { shieldT = 0f; if (driver != null) driver.Toast("Spawn shield off - you can be hit now!", 2.5f); } }
        SendNet(false);

        bool on = driver != null;
        Vector3 pos = transform.position;
        float ground = Ranch.GY(pos.x, pos.z);
        // walk / turn: quick for kids (~8 m/s for the 10-story up to ~30 m/s for the trillion-story)
        float maxSp = 8f + H * 0.14f;
        float stick = on ? Mathf.Clamp(inp.move.y, -0.6f, 1f) : 0f;
        bool boost = on && inp.boostHeld;
        boostK = Mathf.MoveTowards(boostK, boost ? 1f : 0f, dt * 4f);
        float wantSp = stick * maxSp * (1f + boostK * (grounded ? 0.6f : 1.6f));
        float acc = (Mathf.Abs(wantSp) > Mathf.Abs(speed) ? 2.2f : 3.2f) * maxSp;
        speed = Mathf.MoveTowards(speed, wantSp, acc * dt);
        float turnRate = (95f - band * 12f) * (grounded ? 1f : 0.8f);
        turnVel = Mathf.MoveTowards(turnVel, on ? inp.move.x * turnRate : 0f, turnRate * 6f * dt);
        yaw += turnVel * dt;
        Vector3 fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        // jump (tap UP on the ground) / rockets (hold UP) / afterburner (BOOST)
        float g = 20f * Mathf.Sqrt(H / 12f);
        bool up = on && inp.upHeld;
        upHeldT = up ? upHeldT + dt : 0f;
        if (grounded && up && upHeldT <= dt + 1e-4f && landCool <= 0f)
        {
            vy = Mathf.Sqrt(2f * g * H * 0.35f);
            grounded = false;
            Sfx.PlayAt(Sfx.Pick(Sfx.Servo), pos, 0.6f, 120f, 0.6f);
            for (int i = 0; i < 8 + band * 4; i++) FX.Dust(pos + Random.insideUnitSphere * H * 0.2f, 1f);
        }
        rocketing = !grounded && up && (upHeldT > 0.18f || vy < 0f);
        float climbMax = (18f + H * 0.25f) * (1f + boostK) * (1f + Mathf.Max(0f, Altitude - 300f) / 400f) * DemoClimb;
        if (rocketing) vy = Mathf.MoveTowards(vy, climbMax, g * 2.2f * dt);
        else if (!grounded)
        {
            vy -= g * dt;
            if (boost) vy = Mathf.Max(vy, -H * 0.4f);   // afterburner alone glides
            vy = Mathf.Max(vy, -(30f + H * 0.6f));
        }
        flameK = Mathf.MoveTowards(flameK, rocketing ? 1f + boostK : (boost && !grounded ? 0.6f + boostK * 0.4f : boost ? 0.35f * boostK : 0f), dt * 6f);

        Vector3 p = pos + fwd * speed * dt;
        p.y += vy * dt;
        p.x = Mathf.Clamp(p.x, -Layout.Half + H * 0.2f, Layout.Half - H * 0.2f);
        p.z = Mathf.Clamp(p.z, -Layout.Half + H * 0.2f, Layout.Half - H * 0.2f);
        ground = Ranch.GY(p.x, p.z);
        landCool -= dt;
        if (!grounded && p.y <= ground && vy <= 0f)
        {
            // touchdown: a big thud, dust ring, shake
            p.y = ground; grounded = true; landCool = 0.25f;
            float hard = Mathf.Clamp01(-vy / (20f + H * 0.4f));
            Game.Shake(p, Mathf.Clamp(0.5f + band * 0.3f, 0.4f, 1.6f) * (0.4f + hard));
            Sfx.PlayAt(Sfx.Land != null ? Sfx.Land : Sfx.Thud, p, 0.9f, 120f + H * 2f, Mathf.Lerp(0.9f, 0.4f, band / 3f));
            Sfx.PlayAt(Sfx.Step, p, 0.8f, 120f + H * 2f, 0.5f);
            for (int i = 0; i < 16 + band * 6; i++) { Vector2 r = Random.insideUnitCircle.normalized * H * Random.Range(0.15f, 0.4f); FX.Dust(p + new Vector3(r.x, 0.5f, r.y), 1f); }
            foreach (Collider c in Physics.OverlapSphere(p, H * 0.3f + 2f, 1 << PropLayer))
                if (c.attachedRigidbody != null) c.attachedRigidbody.AddExplosionForce(500f + band * 800f, p, H * 0.4f + 3f, 1.5f);
            vy = 0f;
        }
        if (grounded) { p.y = ground; vy = 0f; }
        Altitude = Mathf.Max(0f, p.y - ground);
        if (Altitude > 1f) atSpawn = false;
        if ((new Vector2(p.x, p.z) - new Vector2(spawnPos.x, spawnPos.z)).sqrMagnitude > 4f) atSpawn = false;
        kinVel = (p - pos) / Mathf.Max(dt, 1e-4f);
        rb.MovePosition(p);
        rb.MoveRotation(Quaternion.Euler(0f, yaw, 0f));
        transform.position = p;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        Animate(dt, maxSp);
        Jets(dt);
        Weapons(dt, on, fwd);
        if (on && Altitude >= SpaceAlt && SpaceWorld.I != null) ReachSpace();
    }

    void Animate(float dt, float maxSp)
    {
        float H = height;
        if (grounded)
        {
            // stride cadence: brisk, with a minimum so even the trillion-story steps lively (feet slide a little; fine)
            float frac = Mathf.Clamp01(Mathf.Abs(speed) / Mathf.Max(0.1f, maxSp));
            float turning = Mathf.Clamp01(Mathf.Abs(turnVel) / 60f);
            float cad = Mathf.Max(Mathf.Abs(speed) / (H * 0.42f), (frac + turning * 0.6f) * 1.4f);
            phase += cad * Mathf.PI * dt;
            float sw = Mathf.Sin(phase);
            float amp = Mathf.Max(frac, turning * 0.5f) * 30f;
            legL.localRotation = Quaternion.Euler(sw * amp, 0f, 0f);
            legR.localRotation = Quaternion.Euler(-sw * amp, 0f, 0f);
            armL.localRotation = Quaternion.Euler(-sw * amp * 0.7f + (fireCool > 0.15f ? -25f : 0f), 0f, 0f);
            armR.localRotation = Quaternion.Euler(sw * amp * 0.7f + (fireCool > 0.15f ? -25f : 0f), 0f, 0f);
            hips.localPosition = new Vector3(hips.localPosition.x * 0f + Mathf.Sin(phase) * H * 0.01f * amp / 30f, HipY - Mathf.Abs(Mathf.Cos(phase)) * H * 0.02f * amp / 30f, hips.localPosition.z);
            torso.localRotation = Quaternion.Euler(Mathf.Clamp(speed / maxSp, -1f, 1f) * 6f, 0f, Mathf.Sin(phase) * 3f * amp / 30f - turnVel * 0.05f);
            float side = Mathf.Sign(Mathf.Cos(phase));
            if (amp > 4f && side != stepSide)
            {
                stepSide = side;
                Vector3 foot = transform.TransformPoint(new Vector3(H * 0.1f * side, 0f, H * 0.05f));
                Game.Shake(foot, Mathf.Clamp(0.2f + band * 0.22f, 0.2f, 0.9f));
                Sfx.PlayAt(Sfx.Step, foot, 0.5f + band * 0.15f, 80f + H * 2f, Mathf.Lerp(1.1f, 0.45f, band / 3f));
                for (int i = 0; i < 3 + band * 3; i++) FX.Dust(foot + Random.insideUnitSphere * H * 0.05f, 1f);
                foreach (Collider c in Physics.OverlapSphere(foot, H * 0.12f + 1f, 1 << PropLayer))
                    if (c.attachedRigidbody != null) c.attachedRigidbody.AddExplosionForce(400f + band * 600f, foot, H * 0.2f + 2f, 1f);
            }
        }
        else
        {
            // in the air: legs tuck a little, arms out for balance, body leans into the flight
            Quaternion lt = Quaternion.Euler(rocketing ? 12f : -18f, 0f, 0f);
            legL.localRotation = Quaternion.Slerp(legL.localRotation, lt, dt * 6f);
            legR.localRotation = Quaternion.Slerp(legR.localRotation, Quaternion.Euler(rocketing ? 18f : -10f, 0f, 0f), dt * 6f);
            armL.localRotation = Quaternion.Slerp(armL.localRotation, Quaternion.Euler(-20f, 0f, -25f), dt * 6f);
            armR.localRotation = Quaternion.Slerp(armR.localRotation, Quaternion.Euler(-20f, 0f, 25f), dt * 6f);
            torso.localRotation = Quaternion.Slerp(torso.localRotation, Quaternion.Euler(Mathf.Clamp(speed / Mathf.Max(1f, maxSp), -1f, 1.5f) * 14f, 0f, -turnVel * 0.12f), dt * 4f);
            hips.localPosition = new Vector3(0f, HipY, hips.localPosition.z);
        }
        if (head != null) head.localRotation = Quaternion.Euler(0f, Mathf.Clamp(Mathf.DeltaAngle(yaw, camYawIn), -40f, 40f) * (driver != null ? 0.5f : 0f), 0f);
    }
    float hipY0 = -1f;
    float HipY { get { if (hipY0 < 0f) hipY0 = hips.localPosition.y; return hipY0; } }

    void Jets(float dt)
    {
        float H = height;
        float k = flameK;
        for (int i = 0; i < 2; i++)
        {
            if (flames[i] == null) continue;
            float fl = 1f + Mathf.Sin(Time.time * (41f + i * 7f)) * 0.1f + Random.Range(-0.06f, 0.06f);
            float len = H * (0.12f + 0.16f * k) * fl * (k > 0.01f ? 1f : 0f);
            float wid = H * (0.05f + 0.02f * k) * (k > 0.01f ? 1f : 0f);
            flames[i].localScale = new Vector3(wid, len, wid);
            if (k > 0.05f && Random.value < k * dt * 30f)
            {
                Vector3 fp = flames[i].position - Vector3.up * len * 0.8f;
                FX.Flame(fp, Vector3.down);
                if (Random.value < 0.4f) FX.Smoke(fp - Vector3.up * len * 0.3f, H * 0.05f + 1f, new Color(0.75f, 0.75f, 0.75f, 0.45f));
            }
        }
        if (jetLight != null)
        {
            jetLight.enabled = k > 0.02f && !Look.Mobile;
            jetLight.intensity = k * 2.2f * (0.9f + Random.value * 0.2f);
        }
        // rocket roar: the synthesised launch rumble, pitched by the afterburner
        bool heard = driver != null && driver.human;
        if (jetLoop == null && heard && k > 0.02f) jetLoop = Sfx.Loop(gameObject, Sfx.EngRocket != null ? Sfx.EngRocket : Sfx.Rotor);
        if (jetLoop != null)
        {
            jetLoop.volume = Mathf.MoveTowards(jetLoop.volume, heard ? Mathf.Clamp01(k) * 0.75f : 0f, dt * 3f);
            jetLoop.pitch = 0.65f + 0.25f * boostK + 0.1f * Mathf.Clamp01(k);
            if (!jetLoop.isPlaying && jetLoop.volume > 0f) jetLoop.Play();
        }
        // ground blast under the rockets near the ground
        if (k > 0.3f && Altitude < H * 1.2f && Random.value < dt * 20f)
        {
            Vector3 gp = transform.position; gp.y = Ranch.GY(gp.x, gp.z) + 0.5f;
            FX.Dust(gp + Random.insideUnitSphere * H * 0.25f, 1f);
        }
    }

    // ---------------- weapons: everything leaves from the chest ----------------
    public Vector3 ChestPoint { get { return torso.TransformPoint(new Vector3(0f, 0.22f * height, 0.14f * height)); } }

    Vehicle AimTarget(Vector3 from, Vector3 dir, float cone)
    {
        Vehicle best = null; float bs = cone;
        foreach (var v in Vehicle.All)
        {
            if (v == null || v == this || v.body == null || !v.body.enabled) continue;
            StoryMech sm = v as StoryMech;
            if (sm != null && sm.wrecked) continue;
            if (sm == null && v.driver == null) continue;          // only aim-assist at driven vehicles / mechs
            Vector3 to = v.body.bounds.center - from;
            float d = to.magnitude; if (d > 400f + height * 3f || d < 1f) continue;
            float ang = Vector3.Angle(dir, to);
            float score = ang + d * 0.02f;
            if (ang < cone && score < bs) { bs = score; best = v; }
        }
        return best;
    }

    void Weapons(float dt, bool on, Vector3 fwd)
    {
        fireCool -= dt; missileCool -= dt;
        if (!on || driver.netPuppet) return;
        float H = height;
        Vector3 aimDir = Quaternion.Euler(0f, camYawIn, 0f) * Vector3.forward;
        Vector3 from = ChestPoint;
        if ((inp.gunFire || inp.gunHeld) && fireCool <= 0f)
        {
            fireCool = 0.32f;
            Vehicle t = AimTarget(from, aimDir, 22f);
            Vector3 dir;
            if (t != null) dir = (t.body.bounds.center - from).normalized;
            else
            {
                // no target: aim at the ground ~3 heights ahead so shots land where the kid is looking
                Vector3 aimAt = transform.position + aimDir * (H * 3f + 15f);
                aimAt.y = Ranch.GY(aimAt.x, aimAt.z) + 1f;
                if (Altitude > H) aimAt = from + aimDir * 60f + Vector3.down * 20f;
                dir = (aimAt - from).normalized;
            }
            Vector3 vel = dir * (90f + H * 0.4f);
            MechShot.Spawn(this, 1, from + dir * H * 0.05f, vel, null);
            if (Net.I != null && Net.I.Online) Net.I.SendFire(this, 1, from + dir * H * 0.05f, vel);
        }
        if (inp.alt && missileCool <= 0f)
        {
            missileCool = 1.4f;
            Vehicle t = AimTarget(from, aimDir, 40f);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 side = torso.right * s * H * 0.06f;
                Vector3 dir = (aimDir * 2f + Vector3.up * 0.35f + torso.right * s * 0.25f).normalized;
                Vector3 vel = dir * (35f + H * 0.2f);
                MechShot.Spawn(this, 2, from + side, vel, t);
                if (Net.I != null && Net.I.Online) Net.I.SendFire(this, 2, from + side, vel);
            }
        }
    }

    // ---------------- space ----------------
    void ReachSpace()
    {
        Frog pilot = driver;
        if (pilot == null) return;
        bool takeAll = Game.I != null && Game.I.SharedScreen;
        string who = pilot.nick;
        pilot.SendTo(WorldId.Ranch, pilot.transform.position, 0f);   // out of the mech first
        SpaceWorld.I.Launch(pilot);
        pilot.Toast("Your mech broke into SPACE! The Starship docked and picked you up.", 4.5f);
        if (takeAll)
            foreach (Frog f in Game.I.frogs)
                if (f != null && f != pilot && f.human && !f.netPuppet && f.world != WorldId.Space)
                {
                    SpaceWorld.I.Launch(f);
                    f.Toast(who + " is taking everyone to space!", 4f);
                }
        if (takeAll) pilot.Toast(who + " is taking everyone to space!", 4f);
        Debug.Log("Mech reached space: " + Title + (takeAll ? " (shared screen: everyone along)" : ""));
        // the mech flies itself home and waits at its spot
        transform.position = spawnPos; transform.rotation = spawnRot; yaw = spawnRot.eulerAngles.y;
        vy = 0f; grounded = true; Altitude = 0f; speed = 0f; flameK = 0f;
    }

    // ---------------- health bar / shield visuals ----------------
    void TickBar(float dt)
    {
        hitFlash = Mathf.Max(0f, hitFlash - dt * 3f);
        shieldFlash = Mathf.Max(0f, shieldFlash - dt * 2f);
        bool shield = !wrecked && Shielded;
        bool showShield = shield && !wrecked && (driver != null || shieldFlash > 0f || shieldT > 0f || atSpawn && hpWasRespawned);
        if (shieldGo.gameObject.activeSelf != showShield) shieldGo.gameObject.SetActive(showShield);
        if (showShield)
        {
            float flick = shieldT > 0f && shieldT < 4f ? (Mathf.Sin(Time.time * 18f) > 0f ? 1f : 0.3f) : 1f;
            shieldMat.color = new Color(0.35f, 0.85f, 1f, (0.16f + 0.1f * Mathf.Sin(Time.time * 5f) + shieldFlash * 0.25f) * flick);
            shieldGo.localScale = new Vector3(height * 0.75f, height * 1.12f, height * 0.75f) * (1f + Mathf.Sin(Time.time * 3f) * 0.015f);
        }
        bool show = wrecked || driver != null || hp < maxHp - 0.5f || shieldT > 0f || shieldFlash > 0f;
        barWanted = show;
        if (!show) return;
        float k = Mathf.Clamp01(hp / maxHp);
        barFill.localScale = new Vector3(Mathf.Max(0.001f, k), 1f, 1f);
        var fr = barFill.GetChild(0).GetComponent<Renderer>();
        Color c = Color.Lerp(new Color(1f, 0.25f, 0.2f), new Color(0.3f, 1f, 0.35f), k);
        if (hitFlash > 0f) c = Color.Lerp(c, Color.white, hitFlash);
        fr.sharedMaterial.color = c;
        string label = wrecked ? "BACK IN " + Mathf.CeilToInt(Mathf.Max(0f, wreckT)) : shieldT > 0f ? "SHIELD " + Mathf.CeilToInt(shieldT) : shield && driver == null && shieldT < 0f ? "SPAWN SHIELD" : "";
        if (driver != null && label.Length == 0) label = driver.nick;
        if (barText.text != label) barText.text = label;
        barText.color = shieldT > 0f || (shield && !wrecked) ? new Color(0.55f, 0.92f, 1f) : Color.white;
    }
}
