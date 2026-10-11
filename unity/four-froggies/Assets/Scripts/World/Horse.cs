using System.Collections.Generic;
using UnityEngine;

// ffu32 HORSE RIDING (Bill: "You should be able to ride the horses, get on them and ride them around."). The three pasture
// horses (Animal.Horse, Quaternius mesh with leg / head parts) get a saddle, bridle + reins and this Vehicle component on
// the same object. Walk up and press A / E / tap to mount; the froggy sits in the saddle holding the reins.
//  * Unmounted: the Animal component runs as before (wander / graze / flee) and this body is kinematic. A horse left
//    outside the pasture grazes a while, then walks itself home through the gate.
//  * Mounted: Animal is off (so it never flees from its own rider), a gravity-free rigidbody whose velocity we set every
//    step (the box collider stops it at walls / fences) + our own terrain follow and jump arc.
//  * Gaits from speed: walk (4-beat lateral) / trot (diagonal pairs) / canter (3-beat) / gallop (4-beat rotary), each with
//    its own phase offsets, duty factor, swing amplitude and stride rate, blended across the change; legs fold (shorten)
//    in the swing phase; body bob / pitch rock and head nod per gait; hoof strikes play a clop per foot plant.
//  * Stick / WASD: forward urges it on (keyboard holds ramp walk -> trot -> canter), SHIFT / LB / RT hard = gallop, back =
//    slow / back up, left-right = rein. JUMP (SPACE, pad X / Y / RB, touch JUMP) = jump (clears the 1.2 m pasture fence and
//    berms) when moving, rear up + whinny when standing. E / A / tap A gets off.
public class Horse : Vehicle
{
    public static readonly List<Horse> Herd = new List<Horse>();
    public Animal anim;
    public int index;
    Transform bodyT, headT, tailT;
    readonly List<Transform> legs = new List<Transform>();
    float backY = 1.35f, saddleZ = 0f, hipZ = -0.7f, legLen = 0.9f;
    Vector3 bitLocal;                 // in head space
    Transform reinL, reinR;
    float yaw, speed, rampT, vy, phase, airT, rearT = -1f, jumpCool, blockedT, idleT, lastGait = -1f, neighCool, aiNoMount;
    bool air, homeWalk;
    float[] lastU = new float[4];
    Vector3 lastPos;
    Rect home;
    public bool Ridden { get { return driver != null; } }
    public string GaitName { get { return Gait(speed); } }

    public const float WalkV = 1.8f, TrotV = 4.2f, CanterV = 7.5f, GallopV = 12.5f;
    public override string HelpLine
    {
        get
        {
            InputKind k = driver != null ? driver.inputKind : InputKind.Keyboard;
            if (k == InputKind.Gamepad) return GaitName.ToUpper() + "  |  L-stick ride  RT / LB gallop  |  X / Y jump  |  A get off";
            if (k == InputKind.Touch) return GaitName.ToUpper() + "  |  stick ride (push far = faster)  |  JUMP  |  A get off";
            return GaitName.ToUpper() + "  |  W ride  SHIFT gallop  A / D rein  |  SPACE jump  |  E off";   // ffu32c: one line at 1280 px
        }
    }
    public override string HudStatus { get { return "Horse - " + GaitName + "  " + Mathf.RoundToInt(Speed * 3.6f) + " km/h"; } }
    public override string[] TouchSet { get { return new[] { "A", "JUMP", null, null, null }; } }
    public override bool CanEnter(Frog f) { return f != null && f.world == WorldId.Ranch && rearT < 0f; }

    static string Gait(float v) { return v < 0.25f ? "Standing" : v < 2.9f ? "Walk" : v < 5.8f ? "Trot" : v < 9.6f ? "Canter" : "Gallop"; }

    public static Horse Make(Animal a, int index)
    {
        if (a == null) return null;
        var h = a.gameObject.AddComponent<Horse>();
        h.anim = a; h.index = index;
        h.Title = "Horse";
        h.EnterVerb = "ride the horse";
        h.home = a.area;
        h.Setup();
        Herd.Add(h);
        return h;
    }

    void Setup()
    {
        bodyT = anim.body; headT = anim.head; tailT = anim.tail;
        legs.AddRange(anim.legs);
        Measure();
        BuildTack();
        SetupBody(450f, new Vector3(0f, backY - 0.32f, saddleZ * 0.3f), new Vector3(0.62f, 0.62f, 1.7f), new Vector3(0f, backY - 0.4f, 0f));
        rb.useGravity = false;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;   // valid kinematic or not
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.None;
        hasWorldBounds = true; worldCenter = transform.position; worldRadius = 1e6f;   // out of the wreck system (Combat.Eligible)
        camDistance = 6.8f; camHeight = 2.4f;
        seatScale = 0.7f;
        showDriver = true;
    }

    // back height + saddle spot from the actual body mesh (vertices on the spine between the front and back legs)
    void Measure()
    {
        float zf = 0.6f, zb = -0.6f;
        if (legs.Count == 4)
        {
            zf = (legs[0].localPosition.z + legs[1].localPosition.z) * 0.5f;
            zb = (legs[2].localPosition.z + legs[3].localPosition.z) * 0.5f;
            legLen = Mathf.Max(0.5f, legs[0].localPosition.y);
        }
        hipZ = zb;
        saddleZ = Mathf.Lerp(zb, zf, 0.58f);
        float best = -1f;
        foreach (var mf in bodyT.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
            bool skip = headT != null && mf.transform.IsChildOf(headT);
            foreach (var l in legs) if (mf.transform.IsChildOf(l)) skip = true;
            if (skip) continue;
            var vs = mf.sharedMesh.vertices;
            for (int i = 0; i < vs.Length; i += 1)
            {
                Vector3 p = bodyT.InverseTransformPoint(mf.transform.TransformPoint(vs[i]));
                if (Mathf.Abs(p.x) < 0.14f && Mathf.Abs(p.z - saddleZ) < 0.16f && p.y > best) best = p.y;
            }
        }
        if (best > 0.6f && best < 2.5f) backY = best;
        // bit: the front-most, lower part of the head
        bitLocal = new Vector3(0f, 0.4f, 0.4f);
        if (headT != null)
        {
            float bz = -9f; Vector3 bp = Vector3.zero; bool any = false;
            foreach (var mf in headT.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                foreach (var v in mf.sharedMesh.vertices)
                {
                    Vector3 p = headT.InverseTransformPoint(mf.transform.TransformPoint(v));
                    if (p.z > bz) { bz = p.z; bp = p; any = true; }
                }
            }
            if (any) bitLocal = bp + new Vector3(0f, -0.06f, -0.12f);
        }
        Debug.Log("Horse " + index + ": back " + backY.ToString("0.00") + " m, saddle z " + saddleZ.ToString("0.00") + ", leg " + legLen.ToString("0.00") + ", bit " + bitLocal.ToString("0.00"));
    }

    void BuildTack()
    {
        Color[] saddleC = { new Color(0.42f, 0.22f, 0.1f), new Color(0.2f, 0.12f, 0.08f), new Color(0.55f, 0.32f, 0.14f) };
        Color[] padC = { new Color(0.75f, 0.15f, 0.15f), new Color(0.15f, 0.35f, 0.7f), new Color(0.2f, 0.55f, 0.3f) };
        Material leather = Mats.Shiny(saddleC[index % 3]), pad = Mats.Lit(padC[index % 3]), strap = Mats.Lit(new Color(0.12f, 0.08f, 0.05f)), steel = Mats.Steel(new Color(0.72f, 0.72f, 0.74f));
        Transform b = bodyT;
        float y = backY, z = saddleZ;
        // saddle blanket (draped down both flanks), seat, pommel + horn, cantle, skirts, girth, stirrups
        Mats.Prim(PrimitiveType.Cube, b, new Vector3(0f, y - 0.02f, z), new Vector3(0.66f, 0.05f, 0.78f), pad);
        for (int s = -1; s <= 1; s += 2)
        {
            var fl = Mats.Prim(PrimitiveType.Cube, b, new Vector3(s * 0.31f, y - 0.2f, z), new Vector3(0.04f, 0.36f, 0.74f), pad);
            fl.transform.localRotation = Quaternion.Euler(0f, 0f, s * 8f);
            var sk = Mats.Prim(PrimitiveType.Cube, b, new Vector3(s * 0.33f, y - 0.2f, z - 0.02f), new Vector3(0.035f, 0.3f, 0.55f), leather);
            sk.transform.localRotation = Quaternion.Euler(0f, 0f, s * 8f);
            // stirrup leather + iron
            Mats.Prim(PrimitiveType.Cube, b, new Vector3(s * 0.36f, y - 0.42f, z + 0.05f), new Vector3(0.03f, 0.5f, 0.05f), strap);
            Mats.Prim(PrimitiveType.Cube, b, new Vector3(s * 0.37f, y - 0.7f, z + 0.05f), new Vector3(0.05f, 0.04f, 0.13f), steel);
        }
        Mats.Prim(PrimitiveType.Cube, b, new Vector3(0f, y + 0.05f, z), new Vector3(0.44f, 0.09f, 0.56f), leather);
        Mats.Prim(PrimitiveType.Cube, b, new Vector3(0f, y + 0.13f, z + 0.27f), new Vector3(0.34f, 0.14f, 0.1f), leather);
        Mats.Prim(PrimitiveType.Cylinder, b, new Vector3(0f, y + 0.25f, z + 0.29f), new Vector3(0.06f, 0.06f, 0.06f), leather);
        Mats.Prim(PrimitiveType.Sphere, b, new Vector3(0f, y + 0.31f, z + 0.29f), new Vector3(0.1f, 0.04f, 0.1f), leather);
        var cantle = Mats.Prim(PrimitiveType.Cube, b, new Vector3(0f, y + 0.17f, z - 0.27f), new Vector3(0.4f, 0.2f, 0.07f), leather);
        cantle.transform.localRotation = Quaternion.Euler(-14f, 0f, 0f);
        // girth band round the belly
        Mats.Prim(PrimitiveType.Cube, b, new Vector3(0f, y - 0.62f, z + 0.12f), new Vector3(0.56f, 0.05f, 0.1f), strap);
        for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, b, new Vector3(s * 0.3f, y - 0.42f, z + 0.12f), new Vector3(0.03f, 0.42f, 0.09f), strap);
        // bridle on the head: nose band + cheek straps + bit rings
        if (headT != null)
        {
            Vector3 bt = bitLocal;
            Mats.Prim(PrimitiveType.Cube, headT, bt + new Vector3(0f, 0.12f, -0.12f), new Vector3(0.3f, 0.05f, 0.05f), strap);
            for (int s = -1; s <= 1; s += 2)
            {
                Mats.Prim(PrimitiveType.Cube, headT, bt + new Vector3(s * 0.13f, 0.3f, -0.3f), new Vector3(0.03f, 0.5f, 0.04f), strap, false).transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
                Mats.Prim(PrimitiveType.Cylinder, headT, bt + new Vector3(s * 0.14f, 0f, 0f), new Vector3(0.07f, 0.01f, 0.07f), steel).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
        }
        // reins (stretched each frame between the bit rings and the rider's hands)
        reinL = Mats.Prim(PrimitiveType.Cube, transform, Vector3.zero, Vector3.one * 0.02f, strap).transform;
        reinR = Mats.Prim(PrimitiveType.Cube, transform, Vector3.zero, Vector3.one * 0.02f, strap).transform;
        seat = Mats.Node(b, "Seat", new Vector3(0f, y + 0.09f, z - 0.04f));
        foreach (var r in GetComponentsInChildren<Renderer>()) r.receiveShadows = true;
    }

    // ---------------- mount / dismount ----------------
    public override void OnEnter()
    {
        homeWalk = false;
        anim.enabled = false;
        yaw = transform.eulerAngles.y;
        speed = 0f; vy = 0f; air = false; rampT = 0f; idleT = 0f; rearT = -1f;
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.position = transform.position; rb.rotation = transform.rotation;
        rb.velocity = Vector3.zero;
        lastPos = transform.position;
        if (driver != null && driver.human) Sfx.PlayAt(GardenAudio.Snort, transform.position + Vector3.up * 1.5f, 0.55f, 30f, Random.Range(0.9f, 1.1f));
        Debug.Log("Horse " + index + ": mounted by " + (driver != null ? driver.nick : "?") + " at " + transform.position.ToString("0"));
    }

    public override void OnExit()
    {
        base.OnExit();
        speed = 0f; rearT = -1f;
        rb.velocity = Vector3.zero;
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.None;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 p = transform.position; p.y = Ground(p); transform.position = p;
        ResetPose();
        reinL.gameObject.SetActive(false); reinR.gameObject.SetActive(false);
        idleT = 0f;
        Resume();
        aiNoMount = 6f;
    }

    // hand back to the Animal wander: inside the pasture as before; outside, graze where it stands for now
    void Resume()
    {
        Vector3 p = transform.position;
        bool inside = home.Contains(new Vector2(p.x, p.z));
        anim.area = inside ? home : new Rect(p.x - 3f, p.z - 3f, 6f, 6f);
        anim.SyncYaw();
        anim.enabled = true;
        homeWalk = false;
    }

    public bool InPasture { get { Vector3 p = transform.position; return home.Contains(new Vector2(p.x, p.z)); } }

    float Ground(Vector3 p)
    {
        RaycastHit h;
        float gy = Ranch.GY(p.x, p.z);
        if (Physics.Raycast(p + Vector3.up * 1.6f, Vector3.down, out h, 4f, Vehicle.GroundMask, QueryTriggerInteraction.Ignore) && h.point.y > gy - 0.5f) gy = Mathf.Max(gy, h.point.y);
        return gy;
    }

    // ---------------- riding ----------------
    bool jumpLatch;   // the jump press is a one-frame flag in Update; FixedUpdate may not run that frame
    public override void Drive(PIn i, float camYaw, float dt)
    {
        base.Drive(i, camYaw, dt);
        if ((inp.hop && !inp.use) || inp.alt || inp.gunFire || (inp.fire && !(inp.gas > 0.05f))) jumpLatch = true;
    }

    protected override void FixedUpdate()
    {
        if (driver == null && !homeWalk) return;
        float dt = Time.fixedDeltaTime;
        PIn i = driver != null ? inp : HomeInput();
        jumpCool -= dt;
        bool jumpBtn = driver != null && jumpLatch;
        jumpLatch = false;
        float fwd = Mathf.Clamp01(Mathf.Max(i.move.y, i.gas));
        float back = Mathf.Clamp01(Mathf.Max(-i.move.y, i.brake));
        bool gallop = i.boostHeld || i.gas > 0.85f;
        // desired speed: analog stick partly forward = proportional; full forward ramps walk -> trot -> canter over ~2.5 s
        float want;
        if (rearT >= 0f) want = 0f;
        else if (fwd > 0.05f)
        {
            if (fwd < 0.92f && !gallop) { want = Mathf.Lerp(WalkV * 0.6f, CanterV, Mathf.InverseLerp(0.05f, 0.92f, fwd)); rampT = 0f; }
            else { rampT += dt; want = rampT < 0.9f ? WalkV : rampT < 2.4f ? TrotV : CanterV; }
            if (gallop) want = GallopV;
        }
        else { rampT = 0f; want = back > 0.1f ? -1.1f * back : 0f; }
        if (air) want = speed;
        float acc = want > speed ? (speed < TrotV ? 3.2f : 4.2f) : (want < 0f ? 3f : 6.5f);
        speed = Mathf.MoveTowards(speed, want, acc * dt);
        // reins: turn rate slows at speed; can pivot on the spot
        float turnIn = Mathf.Clamp(i.move.x, -1f, 1f);
        float rate = Mathf.Lerp(95f, 55f, Mathf.InverseLerp(0f, GallopV, Mathf.Abs(speed)));
        if (!air && rearT < 0f) yaw += turnIn * rate * dt * (speed < -0.1f ? -1f : 1f);
        // refuse deep water ahead
        Vector3 fw = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 pos = rb.position;
        Vector3 ahead = pos + fw * (1.6f + Mathf.Max(0f, speed) * 0.25f);
        if (speed > 0f && Layout.InPond(ahead.x, ahead.z) && Ranch.GY(ahead.x, ahead.z) < Layout.WaterY - 0.8f)
        {
            speed = Mathf.MoveTowards(speed, 0f, 14f * dt);
            if (neighCool <= 0f && driver != null && driver.human) { neighCool = 3f; driver.Toast("The horse won't wade into deep water", 2f); Sfx.PlayAt(GardenAudio.Snort, pos + Vector3.up * 1.5f, 0.6f, 30f, 1f); }
        }
        neighCool -= dt;
        // edge of the ranch
        if (Mathf.Abs(ahead.x) > Layout.Half - 8f || Mathf.Abs(ahead.z) > Layout.Half - 8f) speed = Mathf.Min(speed, 0.5f);
        // jump / rear
        if (jumpBtn && jumpCool <= 0f && !air && rearT < 0f)
        {
            jumpCool = 0.9f;
            if (speed > 2.4f)
            {
                air = true; airT = 0f; vy = 5.4f + Mathf.Min(speed, 10f) * 0.06f;
                Sfx.PlayAt(GardenAudio.Snort, pos + Vector3.up * 1.4f, 0.5f, 30f, 1.15f);
                Debug.Log("Horse " + index + ": jump at " + speed.ToString("0.0") + " m/s");
            }
            else
            {
                rearT = 0f; speed = 0f;
                if (Sfx.Neigh != null) Sfx.PlayAt(Sfx.Neigh, pos + Vector3.up * 1.8f, 0.9f, 60f, Random.Range(1.0f, 1.12f));
                Debug.Log("Horse " + index + ": rears up");
            }
        }
        if (rearT >= 0f) { rearT += dt; if (rearT > 1.7f) { rearT = -1f; HoofFall(pos, 1, 0.9f); HoofFall(pos, 1, 0.7f); } }
        // vertical: terrain follow, airborne arc
        float gy = Ground(pos);
        float nvy;
        if (air)
        {
            airT += dt;
            vy -= 15.5f * dt;
            nvy = vy;
            if (pos.y + vy * dt <= gy && vy < 0f)
            {
                air = false; nvy = (gy - pos.y) / dt;
                float hard = Mathf.Clamp01(-vy / 9f);
                for (int k = 0; k < 4; k++) HoofFall(pos, 2, 0.6f + hard * 0.5f);
                for (int k = 0; k < 6; k++) FX.Dust(pos + Random.insideUnitSphere * 0.6f, 1f);
                Debug.Log("Horse " + index + ": landed after " + airT.ToString("0.00") + " s");
            }
        }
        else
        {
            if (pos.y - gy > 0.7f && speed > 1f) { air = true; airT = 0f; vy = 0f; nvy = 0f; }   // ran off a ledge
            else nvy = Mathf.Clamp((gy - pos.y) / dt * 0.5f, -7f, 9f);
        }
        Vector3 v = fw * speed; v.y = nvy;
        rb.velocity = v;
        rb.MoveRotation(Quaternion.Euler(0f, yaw, 0f));
        kinVel = v;
        // pushed into a wall / fence: stop pushing (the box keeps it out; no endless shove)
        Vector3 moved = pos - lastPos; moved.y = 0f;
        lastPos = pos;
        if (!air && Mathf.Abs(speed) > 1.5f && moved.magnitude < Mathf.Abs(speed) * dt * 0.25f) { blockedT += dt; if (blockedT > 0.35f) { speed *= 0.3f; blockedT = 0f; } }
        else blockedT = 0f;
        // keep on the map
        if (pos.y < -30f || Mathf.Abs(pos.x) > Layout.Half + 10f || Mathf.Abs(pos.z) > Layout.Half + 10f) { rb.position = HomePos + Vector3.up * 0.5f; rb.velocity = Vector3.zero; speed = 0f; }
    }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (driver == null)
        {
            aiNoMount -= dt;
            // a horse left out on the ranch grazes a while, then walks itself home through the gate
            if (!homeWalk && !InPasture && anim.enabled) { idleT += dt; if (idleT > 25f) StartHomeWalk(); }
            if (!homeWalk) return;
        }
        Animate(dt);
        UpdateReins();
    }

    // ---------------- walk home (unridden, outside the pasture) ----------------
    int homeLeg;
    void StartHomeWalk()
    {
        homeWalk = true; homeLeg = 0; anim.enabled = false;
        yaw = transform.eulerAngles.y;
        rb.isKinematic = false; rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.position = transform.position; lastPos = transform.position; speed = 0f; air = false;
        Debug.Log("Horse " + index + ": walking home to the pasture from " + transform.position.ToString("0"));
    }

    PIn HomeInput()
    {
        var o = new PIn();
        Vector3 p = transform.position;
        Vector3 gateOut = PastureGate.Outside, gateIn = PastureGate.Inside;
        Vector3 goal = homeLeg == 0 ? gateOut : gateIn;
        Vector3 to = goal - p; to.y = 0f;
        if (homeLeg == 0 && to.magnitude < 2.5f) homeLeg = 1;
        if (homeLeg == 1 && (to.magnitude < 2f || InPasture && (p.x < home.xMax - 4f)))
        {
            homeWalk = false;
            rb.velocity = Vector3.zero; rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.None;
            ResetPose();
            idleT = 0f; anim.area = home; anim.SyncYaw(); anim.enabled = true;
            Debug.Log("Horse " + index + ": back in the pasture");
            return o;
        }
        float want = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
        float da = Mathf.DeltaAngle(yaw, want);
        o.move = new Vector2(Mathf.Clamp(da / 35f, -1f, 1f), Mathf.Abs(da) > 60f ? 0.08f : 0.16f);
        return o;
    }

    // AI rider steering helper: head for `goal`, faster when far
    public PIn SteerTo(Vector3 goal, float stopDist, bool hurry)
    {
        var o = new PIn();
        Vector3 to = goal - transform.position; to.y = 0f;
        float d = to.magnitude;
        if (d < stopDist) return o;
        float da = Mathf.DeltaAngle(yaw, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
        o.move.x = Mathf.Clamp(da / 30f, -1f, 1f);
        float f = d > 30f ? 0.9f : d > 14f ? 0.7f : d > 7f ? 0.45f : 0.2f;
        if (Mathf.Abs(da) > 70f) f = 0.12f;
        o.move.y = f;
        o.boostHeld = hurry && d > 35f && Mathf.Abs(da) < 25f;
        return o;
    }

    // ---------------- procedural gaits ----------------
    // leg order FL, FR, BL, BR (Animal.cs / build_animals.py); phase offsets per gait
    static readonly float[][] Off = {
        new[] { 0.25f, 0.75f, 0.0f, 0.5f },    // walk: LH, LF, RH, RF
        new[] { 0.0f, 0.5f, 0.5f, 0.0f },      // trot: diagonal pairs
        new[] { 0.6f, 0.32f, 0.32f, 0.0f },    // canter (left lead): RH, LH+RF, LF
        new[] { 0.45f, 0.57f, 0.12f, 0.0f } }; // gallop (rotary)
    static readonly float[] Duty = { 0.64f, 0.46f, 0.4f, 0.32f }, Amp = { 19f, 27f, 33f, 42f }, Freq = { 0.95f, 1.45f, 1.75f, 2.25f };
    static readonly float[] Nom = { WalkV, TrotV, CanterV, GallopV };

    static float LegAngle(float u, float duty, float A, out float fold)
    {
        u = u - Mathf.Floor(u);
        if (u < duty) { fold = 0f; return Mathf.Lerp(-A, A, u / duty); }        // stance: hoof sweeps back under the body
        float s = (u - duty) / (1f - duty);
        fold = Mathf.Sin(s * Mathf.PI);
        return Mathf.Lerp(A, -A, 0.5f - 0.5f * Mathf.Cos(s * Mathf.PI));        // swing: lifts and reaches forward
    }

    void Animate(float dt)
    {
        float v = Mathf.Abs(speed);
        // gait blend: g in [0,3]
        float g = v < 2.9f ? 0f : v < 5.8f ? 1f : v < 9.6f ? 2f : 3f;
        float edge = v < 2.9f ? 2.9f : v < 5.8f ? 5.8f : v < 9.6f ? 9.6f : 99f;
        int lo = (int)g, hi = Mathf.Min(3, lo + 1);
        float w = lo == hi ? 0f : Mathf.Clamp01((v - (edge - 0.5f)) / 0.5f);
        float freq = Mathf.Lerp(Freq[lo], Freq[hi], w) * Mathf.Lerp(0.75f, 1.12f, Mathf.InverseLerp(Nom[lo] * 0.5f, Nom[lo] * 1.25f, v));
        bool moving = v > 0.2f && rearT < 0f;
        if (moving) phase += freq * dt * (speed < 0f ? -1f : 1f);
        int gi = w < 0.5f ? lo : hi;
        if (gi != (int)lastGait && moving && driver != null) { lastGait = gi; }
        float A = moving ? Mathf.Lerp(Amp[lo], Amp[hi], w) * Mathf.Clamp01(v / 1.2f) : 0f;
        float duty = Mathf.Lerp(Duty[lo], Duty[hi], w);
        for (int k = 0; k < legs.Count && k < 4; k++)
        {
            float fold, fold2;
            float a1 = LegAngle(phase + Off[lo][k], Duty[lo], A, out fold);
            float a2 = LegAngle(phase + Off[hi][k], Duty[hi], A, out fold2);
            float a = Mathf.Lerp(a1, a2, w);
            float fo = Mathf.Lerp(fold, fold2, w) * Mathf.Clamp01(v / 1.5f);
            if (air)
            {
                // tucked over a jump: fronts fold up forward, hinds trail
                float tk = Mathf.Clamp01(airT / 0.15f);
                a = Mathf.Lerp(a, k < 2 ? -55f : 40f, tk); fo = Mathf.Lerp(fo, 0.8f, tk);
            }
            if (rearT >= 0f && k < 2)
            {
                float paw = Mathf.Sin(rearT * 9f + k * 1.6f);
                a = -40f + paw * 30f; fo = 0.5f + paw * 0.3f;
            }
            legs[k].localRotation = Quaternion.Euler(a, 0f, 0f);
            legs[k].localScale = new Vector3(1f, 1f - 0.28f * Mathf.Clamp01(fo), 1f);
            // hoof plant = start of stance (u wraps past 0)
            float u = phase + (w < 0.5f ? Off[lo][k] : Off[hi][k]);
            u -= Mathf.Floor(u);
            if (moving && !air && lastU[k] > 0.7f && u < 0.3f) HoofFall(legs[k].position, gi);
            lastU[k] = u;
        }
        // body: bob / pitch per gait; rearing pivots on the hind hips; jump pitch follows the arc
        float tph = phase * Mathf.PI * 2f;
        float bob, pitch;
        switch (gi)
        {
            case 0: bob = Mathf.Cos(tph * 2f) * 0.025f; pitch = Mathf.Sin(tph * 2f) * 0.8f; break;
            case 1: bob = Mathf.Abs(Mathf.Cos(tph)) * 0.07f - 0.035f; pitch = 0f; break;
            case 2: bob = Mathf.Sin(tph) * 0.06f; pitch = Mathf.Sin(tph + 0.6f) * 4f; break;
            default: bob = Mathf.Sin(tph) * 0.08f; pitch = Mathf.Sin(tph + 0.8f) * 5f; break;
        }
        if (!moving) { bob = Mathf.Sin(Time.time * 1.6f + index) * 0.008f; pitch = 0f; }
        // rigid legs: the hooves lift by L(1-cos a) at the stride extremes, so drop the body a touch with the stride
        bob -= legLen * (1f - Mathf.Cos(A * Mathf.Deg2Rad)) * 0.35f;
        if (air) pitch = Mathf.Clamp(-vy * 3.2f, -16f, 18f);
        if (rearT >= 0f)
        {
            float e = rearT < 0.35f ? Mathf.SmoothStep(0f, 1f, rearT / 0.35f) : rearT > 1.3f ? Mathf.SmoothStep(1f, 0f, (rearT - 1.3f) / 0.4f) : 1f;
            pitch = -38f * e;
        }
        Vector3 piv = new Vector3(0f, legLen, hipZ);
        Quaternion q = Quaternion.Euler(pitch, 0f, 0f);
        bodyT.localRotation = q;
        bodyT.localPosition = piv - q * piv + new Vector3(0f, bob, 0f);
        // head: nod with the stride (walk / canter / gallop), still at the trot; up when rearing, stretched over a jump
        if (headT != null)
        {
            float nod;
            switch (gi)
            {
                case 0: nod = Mathf.Sin(tph * 2f + 0.5f) * 6f; break;
                case 1: nod = Mathf.Sin(tph * 2f) * 1.5f; break;
                case 2: nod = Mathf.Sin(tph + 1.2f) * 9f; break;
                default: nod = Mathf.Sin(tph + 1.4f) * 11f + 6f; break;
            }
            if (!moving) nod = Mathf.Sin(Time.time * 0.9f + index) * 3f;
            if (rearT >= 0f) nod = -22f + Mathf.Sin(rearT * 8f) * 8f;
            if (air) nod = 12f;
            float look = driver != null ? Mathf.Clamp(inp.move.x * 14f, -16f, 16f) : 0f;
            headT.localRotation = Quaternion.Euler(nod, look, 0f);
        }
        if (tailT != null) tailT.localRotation = Quaternion.Euler(20f + v * 3.5f, Mathf.Sin(Time.time * (3f + v * 0.5f)) * (25f - Mathf.Min(v, 12f)), 0f);
    }

    // one hoof strike: gait 0..3 sets loudness / pitch; vol overrides (landings, rear-down)
    void HoofFall(Vector3 p, int g, float vol = -1f)
    {
        if (GardenAudio.Clop == null) return;
        g = Mathf.Clamp(g, 0, 3);
        if (vol < 0f) vol = 0.32f + g * 0.12f;
        float pitch = (g == 3 ? 0.88f : g == 2 ? 0.95f : 1.03f) * Random.Range(0.93f, 1.07f);
        Vector3 q = transform.position;
        // softer on grass, crisper on the dirt roads / yard
        bool dirt = Layout.RoadDist(q.x, q.z) < Layout.TrackW * 0.6f || Layout.Flatness(q.x, q.z) < 0.3f;
        if (!dirt) { vol *= 0.8f; pitch *= 0.93f; }
        bool mine = driver != null && driver.human;
        AudioClip c = GardenAudio.Clop[Random.Range(0, 4)];
        if (mine) Sfx.Play(c, Mathf.Min(1f, vol), pitch); else Sfx.PlayAt(c, q, vol, 45f, pitch);
        if (g >= 2 && Random.value < 0.5f) FX.Dust(p, 0.6f);
        HoofCount++;
    }
    public static int HoofCount;

    void ResetPose()
    {
        bodyT.localPosition = Vector3.zero; bodyT.localRotation = Quaternion.identity;
        foreach (var l in legs) { l.localScale = Vector3.one; l.localRotation = Quaternion.identity; }
        if (headT != null) headT.localRotation = Quaternion.identity;
    }

    void UpdateReins()
    {
        bool on = driver != null && driver.model != null && driver.model.gameObject.activeSelf && headT != null;
        if (reinL.gameObject.activeSelf != on) { reinL.gameObject.SetActive(on); reinR.gameObject.SetActive(on); }
        if (!on) return;
        // hands: in front of the rider's chest; bit rings on the head
        for (int s = 0; s < 2; s++)
        {
            float sx = s == 0 ? -1f : 1f;
            Vector3 hand = seat.TransformPoint(new Vector3(sx * 0.15f, 0.42f, 0.36f) * (seatScale / 0.7f));
            Vector3 bit = headT.TransformPoint(bitLocal + new Vector3(sx * 0.14f, 0f, 0f));
            Transform r = s == 0 ? reinL : reinR;
            Vector3 d = bit - hand;
            r.position = (bit + hand) * 0.5f - Vector3.up * 0.04f;
            r.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            r.localScale = new Vector3(0.025f / transform.lossyScale.x, 0.012f / transform.lossyScale.y, d.magnitude / transform.lossyScale.z);
        }
    }

    // rider pose: straddling the saddle, hands forward on the reins, a little bounce with the gait
    void LateUpdate()
    {
        if (driver == null || driver.model == null || driver.model.pet || driver.model.bob == null) return;
        FrogModel m = driver.model;
        float v = Mathf.Abs(speed);
        float bounce = v > 0.3f ? Mathf.Abs(Mathf.Sin(phase * Mathf.PI * 2f)) * Mathf.Clamp01(v / 6f) * 0.06f : 0f;
        m.bob.localPosition = new Vector3(0f, bounce, 0f);
        m.bob.localRotation = Quaternion.Euler(Mathf.Clamp(v * 1.2f, 0f, 14f) + (air ? 10f : 0f), 0f, 0f);   // lean forward at speed
        if (m.legL != null) m.legL.localRotation = Quaternion.Euler(-35f, 0f, -38f);
        if (m.legR != null) m.legR.localRotation = Quaternion.Euler(-35f, 0f, 38f);
        if (m.armL != null) m.armL.localRotation = Quaternion.Euler(-72f, 0f, 8f);
        if (m.armR != null) m.armR.localRotation = Quaternion.Euler(-72f, 0f, -8f);
        if (rearT >= 0f && m.armL != null) { m.armL.localRotation = Quaternion.Euler(-150f, 0f, 30f); }   // one arm up "yee-haw"
    }

    public static Horse FreeNear(Vector3 p, float maxDist)
    {
        Horse best = null; float bd = maxDist * maxDist;
        foreach (var h in Herd)
        {
            if (h == null || h.driver != null || h.rearT >= 0f || h.aiNoMount > 0f) continue;
            float d = (h.transform.position - p).sqrMagnitude;
            if (d < bd) { bd = d; best = h; }
        }
        return best;
    }
}

// ffu32: a two-leaf swinging gate in the pasture's east gap (z = centre +-5). Opens for a horse (ridden or walking home)
// or a froggy on foot that comes close; closes behind. Its collider only exists while it is (nearly) shut.
public class PastureGate : MonoBehaviour
{
    public static PastureGate I;
    public static Vector3 Outside, Inside;
    Transform[] leaves = new Transform[2];
    BoxCollider[] cols = new BoxCollider[2];
    float open, sndCool;
    Vector3 c;

    public static void Build(Rect P)
    {
        var go = new GameObject("Pasture gate");
        I = go.AddComponent<PastureGate>();
        float x = P.xMax, cz = P.center.y;
        I.c = new Vector3(x, Ranch.GY(x, cz), cz);
        Outside = new Vector3(x + 7f, 0f, cz); Outside.y = Ranch.GY(Outside.x, Outside.z);
        Inside = new Vector3(x - 7f, 0f, cz); Inside.y = Ranch.GY(Inside.x, Inside.z);
        Material wood = Mats.TexTint("LB/wood", new Color(0.86f, 0.8f, 0.7f), 0.08f), dark = Mats.Lit(new Color(0.2f, 0.16f, 0.12f));
        for (int s = 0; s < 2; s++)
        {
            float hz = cz + (s == 0 ? -5f : 5f);
            float gy = Ranch.GY(x, hz);
            Mats.Prim(PrimitiveType.Cube, go.transform, new Vector3(x, gy + 0.8f, hz), new Vector3(0.3f, 1.6f, 0.3f), wood, true);   // hinge post
            var hinge = new GameObject("Leaf").transform;
            hinge.SetParent(go.transform, false);
            hinge.position = new Vector3(x, gy, hz);
            hinge.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, s == 0 ? 1f : -1f));   // leaf runs along +local z toward the middle
            for (int k = 0; k < 4; k++) Mats.Prim(PrimitiveType.Cube, hinge, new Vector3(0f, 0.35f + k * 0.3f, 2.45f), new Vector3(0.08f, 0.12f, 4.8f), wood);
            for (int k = 0; k < 3; k++) Mats.Prim(PrimitiveType.Cube, hinge, new Vector3(0f, 0.8f, 0.15f + k * 2.3f), new Vector3(0.09f, 1.15f, 0.1f), wood);
            var br = Mats.Prim(PrimitiveType.Cube, hinge, new Vector3(0f, 0.8f, 2.45f), new Vector3(0.07f, 0.1f, 5.1f), wood);
            br.transform.localRotation = Quaternion.Euler(-12.5f, 0f, 0f);
            Mats.Prim(PrimitiveType.Cube, hinge, new Vector3(0f, 1.05f, 4.85f), new Vector3(0.12f, 0.12f, 0.12f), dark);   // latch
            var bc = hinge.gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 0.75f, 2.45f); bc.size = new Vector3(0.15f, 1.3f, 4.9f);
            I.leaves[s] = hinge; I.cols[s] = bc;
        }
        Debug.Log("PastureGate: built at " + I.c.ToString("0"));
    }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        bool want = false;
        foreach (var h in Horse.Herd)
            if (h != null && (h.Ridden || !h.InPasture || h.GetComponent<Rigidbody>() != null && !h.GetComponent<Rigidbody>().isKinematic) && (h.transform.position - c).sqrMagnitude < 13f * 13f) want = true;
        if (!want && Game.I != null)
            foreach (var f in Game.I.frogs)
                if (f != null && f.world == WorldId.Ranch && (f.transform.position - c).sqrMagnitude < (f.vehicle != null ? 13f * 13f : 4.5f * 4.5f)) want = true;
        float prev = open;
        open = Mathf.MoveTowards(open, want ? 1f : 0f, dt * (want ? 1.1f : 0.6f));
        if ((prev == 0f && open > 0f) || (prev > 0.02f && open == 0f))
        {
            if (sndCool <= 0f) { Sfx.PlayAt(GardenAudio.Creak, c + Vector3.up, 0.45f, 35f, open > 0f ? 0.8f : 0.7f); sndCool = 0.6f; }
            if (prev > 0.02f && open == 0f) Sfx.PlayAt(Sfx.Clank != null ? Sfx.Clank : Sfx.Click, c + Vector3.up, 0.35f, 35f, 1.2f);
        }
        sndCool -= dt;
        float ang = Mathf.SmoothStep(0f, 1f, open) * 105f;
        for (int s = 0; s < 2; s++)
        {
            if (leaves[s] == null) continue;
            // both leaves swing inward (west, into the pasture; the PASTURE sign stands just outside)
            float baseYaw = s == 0 ? 0f : 180f;
            leaves[s].rotation = Quaternion.Euler(0f, baseYaw + (s == 0 ? -ang : ang), 0f);
            if (cols[s] != null) cols[s].enabled = open < 0.08f;
        }
    }
}
