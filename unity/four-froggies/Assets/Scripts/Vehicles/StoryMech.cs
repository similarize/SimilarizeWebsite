using UnityEngine;

// Giant "story mechs" from the 2.5D Four Froggies (games/four-froggies canon.js: 10 / 100 / 1000 / trillion-story
// bands, colours a5b4fc / 67e8f9 / fcd34d / ffd700, omnigun FIRE). In the 2.5D canon each band belongs to one frog
// (Bubbles 10, Jimmy 100, Rexy 1000, James trillion); per Bill's v2 request every froggy has all four here,
// trimmed in the frog's colour, and only the owner can pilot their own.
// Scale is stylised so it works with the camera and far plane: 10-story 12 m, 100-story 30 m, 1000-story 60 m,
// trillion-story 160 m (the Starship stack is ~73 m). Kinematic: walks on the terrain with a slow heavy gait,
// footstep shake + thud, camera pulled back with size. FIRE (RT / X / click / FIRE) = omnigun blast.
public class StoryMech : Vehicle
{
    public int owner;            // frog index
    public int band;             // 0..3
    public float height;
    Transform hips, legL, legR, armL, armR, torso, head;
    float yaw, phase, speed, stepSide, fireCool;
    Vector3 lastFoot;
    public static readonly string[] BandName = { "10-story", "100-story", "1000-story", "trillion-story" };
    public static readonly float[] BandH = { 12f, 30f, 60f, 160f };
    static readonly string[] BandHex = { "#a5b4fc", "#67e8f9", "#fcd34d", "#ffd700" };

    public override string HelpLine { get { return "L-stick walk + turn (slow and heavy) | RT / X / click / FIRE omnigun | A climb out"; } }

    public override bool CanEnter(Frog f) { return f.id == owner; }
    public override string DeniedLine { get { return Froggies.Names[owner] + "'s " + BandName[band] + " mech - only " + Froggies.Names[owner] + " can pilot it"; } }

    protected override bool OutOfWorld(Vector3 p) { return false; }

    public static StoryMech Build(int owner, int band, Vector3 groundPos, float yawDeg)
    {
        float H = BandH[band];
        var go = new GameObject(Froggies.Names[owner] + " " + BandName[band] + " mech");
        go.transform.position = groundPos;
        go.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);
        var m = go.AddComponent<StoryMech>();
        m.owner = owner; m.band = band; m.height = H; m.yaw = yawDeg;
        m.Title = Froggies.Names[owner] + "'s " + BandName[band] + " mech";
        m.EnterVerb = "pilot your " + BandName[band] + " mech";
        m.engineKind = 6;
        m.flyer = true;   // no flip rescue; camera follows the body heading
        m.SetupBodyPublic(1000f, new Vector3(0f, H * 0.32f, 0f), new Vector3(H * 0.42f, H * 0.64f, H * 0.26f), Vector3.zero);
        m.rb.isKinematic = true; m.rb.useGravity = false;
        m.rb.interpolation = RigidbodyInterpolation.None;
        m.rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        m.camDistance = H * 1.15f + 6f; m.camHeight = H * 0.75f;
        m.showDriver = false;

        Material body = Mats.Shiny(Mats.Hex(BandHex[band]));
        Material trim = Mats.Shiny(Froggies.Color(owner));
        Material dark = Mats.Lit(new Color(0.15f, 0.16f, 0.18f));
        Material glow = Mats.Unlit(Color.Lerp(Froggies.Color(owner), Color.white, 0.5f));
        Transform t = go.transform;
        m.hips = Mats.Node(t, "Hips", new Vector3(0f, H * 0.42f, 0f));
        for (int s = -1; s <= 1; s += 2)
        {
            Transform leg = Mats.Node(m.hips, s < 0 ? "LegL" : "LegR", new Vector3(H * 0.1f * s, 0f, 0f));
            Mats.Prim(PrimitiveType.Cube, leg, new Vector3(0f, -H * 0.11f, 0f), new Vector3(H * 0.11f, H * 0.22f, H * 0.12f), body);
            Mats.Prim(PrimitiveType.Cube, leg, new Vector3(0f, -H * 0.23f, H * 0.02f), new Vector3(H * 0.12f, H * 0.05f, H * 0.12f), dark);    // knee
            Mats.Prim(PrimitiveType.Cube, leg, new Vector3(0f, -H * 0.33f, 0f), new Vector3(H * 0.1f, H * 0.18f, H * 0.11f), trim);
            Mats.Prim(PrimitiveType.Cube, leg, new Vector3(0f, -H * 0.41f, H * 0.04f), new Vector3(H * 0.14f, H * 0.03f, H * 0.22f), dark);   // foot
            if (s < 0) m.legL = leg; else m.legR = leg;
        }
        m.torso = Mats.Node(m.hips, "Torso", new Vector3(0f, H * 0.04f, 0f));
        Mats.Prim(PrimitiveType.Cube, m.torso, new Vector3(0f, H * 0.03f, 0f), new Vector3(H * 0.3f, H * 0.08f, H * 0.18f), dark);
        Mats.Prim(PrimitiveType.Cube, m.torso, new Vector3(0f, H * 0.2f, 0f), new Vector3(H * 0.38f, H * 0.28f, H * 0.22f), body);
        Mats.Prim(PrimitiveType.Cube, m.torso, new Vector3(0f, H * 0.22f, H * 0.112f), new Vector3(H * 0.2f, H * 0.12f, H * 0.01f), trim);   // chest plate in the frog colour
        Mats.Prim(PrimitiveType.Sphere, m.torso, new Vector3(0f, H * 0.22f, H * 0.12f), Vector3.one * H * 0.06f, glow);                      // reactor
        for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, m.torso, new Vector3(H * 0.22f * s, H * 0.33f, 0f), new Vector3(H * 0.14f, H * 0.08f, H * 0.18f), trim);   // shoulders
        MeshMerge.Merge(m.torso, band >= 2);   // before the arms / head are parented under it, so they stay animated
        for (int s = -1; s <= 1; s += 2)
        {
            Transform arm = Mats.Node(m.torso, s < 0 ? "ArmL" : "ArmR", new Vector3(H * 0.24f * s, H * 0.3f, 0f));
            Mats.Prim(PrimitiveType.Cube, arm, new Vector3(0f, -H * 0.12f, 0f), new Vector3(H * 0.08f, H * 0.22f, H * 0.09f), body);
            Mats.Prim(PrimitiveType.Cube, arm, new Vector3(0f, -H * 0.27f, 0f), new Vector3(H * 0.1f, H * 0.1f, H * 0.11f), dark);   // fist
            if (s < 0) m.armL = arm; else m.armR = arm;
        }
        m.head = Mats.Node(m.torso, "Head", new Vector3(0f, H * 0.4f, 0f));
        Mats.Prim(PrimitiveType.Cube, m.head, new Vector3(0f, H * 0.04f, 0f), new Vector3(H * 0.16f, H * 0.1f, H * 0.14f), body);
        Mats.Prim(PrimitiveType.Cube, m.head, new Vector3(0f, H * 0.05f, H * 0.071f), new Vector3(H * 0.13f, H * 0.03f, H * 0.005f), glow);   // visor
        // frog eyes on top (it's a froggy mech)
        for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Sphere, m.head, new Vector3(H * 0.05f * s, H * 0.1f, H * 0.02f), Vector3.one * H * 0.05f, trim);
        m.seat = Mats.Node(m.head, "Seat", Vector3.zero);
        m.seatScale = 0.5f;
        // fewer draw calls: merge each moving part, and cull the small ones when they are tiny on screen
        foreach (Transform part in new[] { m.legL, m.legR, m.armL, m.armR, m.head }) MeshMerge.Merge(part, band >= 2);
        Mats.SetLayer(go, VehicleLayer);
        // name plate on the chest
        var tag = new GameObject("Plate");
        tag.transform.SetParent(m.torso, false);
        tag.transform.localPosition = new Vector3(0f, H * 0.12f, H * 0.115f);
        var tm = tag.AddComponent<TextMesh>();
        tm.text = Froggies.Names[owner].ToUpper() + "\n" + BandName[band].ToUpper();
        tm.font = UIK.Font; tm.fontSize = 64; tm.characterSize = H * 0.004f; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
        tag.GetComponent<MeshRenderer>().sharedMaterial = UIK.Font != null ? UIK.Font.material : null;
        return m;
    }

    void Start()
    {
        // cull the small mechs when they are tiny on screen (renderers settle after the merge)
        var lod = gameObject.AddComponent<LODGroup>();
        var rs = new System.Collections.Generic.List<Renderer>();
        foreach (var r in GetComponentsInChildren<Renderer>()) if (r != null && !(r is MeshRenderer && r.GetComponent<TextMesh>() != null)) rs.Add(r);
        lod.SetLODs(new[] { new LOD(band == 0 ? 0.02f : 0.004f, rs.ToArray()) });
    }

    public override void OnEnter()
    {
        base.OnEnter();
        yaw = transform.eulerAngles.y;
        if (driver != null) driver.Toast(Title + ": slow, heavy, enormous. RT / X fires the omnigun.", 3f);
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        bool on = driver != null;
        float H = height;
        float maxSp = H * 0.075f + 1.2f;                  // 12 m mech ~2 m/s, trillion ~13 m/s
        float wantSp = on ? Mathf.Clamp(inp.move.y + inp.gas - inp.brake, -0.5f, 1f) * maxSp : 0f;
        speed = Mathf.MoveTowards(speed, wantSp, maxSp * 0.6f * dt);   // heavy: slow to start and stop
        if (on) yaw += inp.move.x * (32f - band * 5f) * dt;
        Vector3 fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 p = transform.position + fwd * speed * dt;
        // stay on the ranch, ride the terrain
        p.x = Mathf.Clamp(p.x, -Layout.Half + H * 0.2f, Layout.Half - H * 0.2f);
        p.z = Mathf.Clamp(p.z, -Layout.Half + H * 0.2f, Layout.Half - H * 0.2f);
        p.y = Ranch.GY(p.x, p.z);
        kinVel = (p - transform.position) / Mathf.Max(dt, 1e-4f);
        rb.MovePosition(p);
        rb.MoveRotation(Quaternion.Euler(0f, yaw, 0f));
        transform.position = p;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        // gait: big slow strides, body sway, arm swing, footstep shake + thud
        float stride = Mathf.Abs(speed) / (H * 0.35f);
        phase += stride * Mathf.PI * dt;
        float sw = Mathf.Sin(phase);
        float amp = Mathf.Clamp01(Mathf.Abs(speed) / Mathf.Max(0.1f, maxSp)) * 26f;
        legL.localRotation = Quaternion.Euler(sw * amp, 0f, 0f);
        legR.localRotation = Quaternion.Euler(-sw * amp, 0f, 0f);
        armL.localRotation = Quaternion.Euler(-sw * amp * 0.7f + (fireCool > 0.6f ? -80f : 0f), 0f, 0f);
        armR.localRotation = Quaternion.Euler(sw * amp * 0.7f + (fireCool > 0.6f ? -80f : 0f), 0f, 0f);
        hips.localPosition = new Vector3(Mathf.Sin(phase) * H * 0.01f * amp / 26f, H * 0.42f - Mathf.Abs(Mathf.Cos(phase)) * H * 0.02f * amp / 26f, 0f);
        torso.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(phase) * 3f * amp / 26f);
        float side = Mathf.Sign(Mathf.Cos(phase));
        if (amp > 3f && side != stepSide)
        {
            stepSide = side;
            Vector3 foot = transform.TransformPoint(new Vector3(H * 0.1f * side, 0f, H * 0.05f));
            Game.Shake(foot, Mathf.Clamp(0.25f + band * 0.25f, 0.2f, 1.1f));
            Sfx.PlayAt(Sfx.Step, foot, 0.5f + band * 0.15f, 80f + H * 2f, Mathf.Lerp(1.1f, 0.45f, band / 3f));
            for (int i = 0; i < 4 + band * 3; i++) FX.Dust(foot + Random.insideUnitSphere * H * 0.05f, 1f);
            // knock loose props and frogs near the foot
            foreach (Collider c in Physics.OverlapSphere(foot, H * 0.12f + 1f, 1 << PropLayer))
                if (c.attachedRigidbody != null) c.attachedRigidbody.AddExplosionForce(400f + band * 600f, foot, H * 0.2f + 2f, 1f);
        }

        // omnigun
        fireCool -= dt;
        if (on && (inp.fire || inp.fireHeld && fireCool < -0.3f) && fireCool <= 0f)
        {
            fireCool = 0.9f;
            Vector3 from = head.position + fwd * H * 0.12f;
            Vector3 dir = Quaternion.Euler(0f, camYawIn, 0f) * Vector3.forward;
            dir = (dir * 3f + Vector3.down * 0.6f).normalized;
            Projectile.Spawn(true, from + dir * 2f, dir * (40f + H * 0.4f), this);
            FX.Muzzle(from, dir);
            Sfx.PlayAt(Sfx.Missile, from, 0.9f, 150f, 0.7f);
        }
    }
}
