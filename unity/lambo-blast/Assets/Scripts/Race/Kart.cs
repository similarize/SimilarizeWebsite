using UnityEngine;

// One racer: arcade car physics (no rigidbody: ray-cast ground following, jumps off ramps, soft walls),
// drift + mini-turbo, items, spin-outs, lap counting, and the AI driver.
public class Kart : MonoBehaviour
{
    public int id;
    public int car, robot;
    public bool human;
    // debug / screenshot mode (?lbdemo in the URL): human karts drive themselves
    public static bool Autopilot;
    public int slot = -1;              // index into Game.slots for humans
    public CarSpec spec;
    public KIn input;

    // physics state
    public float yaw, speed, lat, vy;
    public bool grounded = true;
    Vector3 gNormal = Vector3.up;
    float lastClimb;
    public bool onRoad = true, inWater;

    // drift / boost / hits
    public bool drifting;
    int driftDir;
    public float driftCharge;
    float steerVisual, spinAngle, wheelRot, headYaw, bounce;
    public float boostT, boostPower = 1.35f;
    public float spinT, spinTotal = 1f, shrinkT, shieldT;
    public bool respawning;
    float respawnT;
    float lastGoodS;
    int lastGoodHint;
    float gasHeldT;

    // items
    public Item item = Item.None;
    public float rollT;
    Item rollTarget;
    public Item rollShow;
    float rollTick;

    // race
    public int lap = -1;
    bool cp = true;
    public float s;
    int hint = -1;
    public bool finished;
    public float finishTime;
    public int place = 8;
    public float wrongT;
    // lambo7 free roam (human drivers only; AI keep the old 9 m soft walls): drive up to OffMax m off the road into the
    // terrain, slower grip there; far off course (OffFar m) for OffTowT s, or the wrong way for WrongTowT s, warns and then
    // fades + tows you back to the nearest track point facing the right way. Water, lava and big falls respawn as before.
    public const float OffMax = 55f, OffFar = 14f, OffWarnT = 3f, OffTowT = 5.5f, WrongWarnT = 2.5f, WrongTowT = 5.5f;
    public float offT;                 // seconds far off course
    public bool towing;                // current respawn is a tow (fade, no sink)
    float towS = -1f, respawnTotal = 1f, fadeInT;
    public float FadeA { get { if (respawning && towing) return Mathf.Clamp01(1f - respawnT / Mathf.Max(0.01f, respawnTotal)) ; return Mathf.Clamp01(fadeInT / 0.45f); } }
    // lambo8 online: a kart driven by another device is a puppet (posed from the network, no local physics)
    public bool netPuppet;
    public string netName;            // online player's name for tags / results (null = local or AI)
    bool netApplyHit;
    public bool hostPlaced;          // host: this kart's finishing place has been given out
    public static bool OffDemo;        // &lboff=1: P1 autopilot drives off the track (probe of the tow-back)
    bool FreeRoam { get { return human && (!Autopilot || OffDemo); } }
    public float RaceDist { get { return finished ? Track.Laps + 10f - place * 0.01f : lap + s / Track.Length; } }

    // messages for this car's player
    public string toast = "";
    public float toastT;

    // AI
    float aiLane, aiLaneTarget, aiLaneT, aiItemT, stuckT;
    bool aiShortcut, aiLikesShortcut, aiJump;
    // boost pads, big-air stunts
    float padCool, airT, trickT, trickRoll;
    bool bigAir;
    float lastScS = -1f;
    public float aiTopMul = 1f;
    float aiSkill = 1f;

    // visuals
    Transform body, head, wheelT;
    Transform[] steerPivots, spinners;
    GameObject shield;
    AudioSource eng, skid;

    public Vector3 Forward { get { return new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad)); } }
    public Vector3 Right { get { Vector3 f = Forward; return new Vector3(f.z, 0f, -f.x); } }
    public Vector3 Velocity { get { return Forward * speed + Right * lat + Vector3.up * vy; } }
    public string Nick { get { return Robots.Names[robot] + " · " + Cars.All[car].name; } }
    public Color Color { get { return Cars.All[car].color; } }

    public static Kart Create(int id, int car, int robot)
    {
        var go = new GameObject("Kart " + id);
        var k = go.AddComponent<Kart>();
        k.id = id;
        k.aiSkill = 0.95f + 0.05f * Random.value;
        k.aiLikesShortcut = Random.value < 0.4f;
        k.aiLane = k.aiLaneTarget = Random.Range(-3.5f, 3.5f);
        k.SetLook(car, robot);
        return k;
    }

    public void SetLook(int c, int r)
    {
        if (body != null && c == car && r == robot) return;
        car = c; robot = r;
        spec = Cars.All[c];
        if (body != null) MeshMerge.DestroyWithMeshes(body.gameObject);
        body = new GameObject("Body").transform;
        body.SetParent(transform, false);
        KartModel.Build(body, car, robot, out steerPivots, out spinners, out head, out wheelT);
        // lambo9: everything but the wheels rides on a sprung node (body roll / pitch over the planted wheels)
        sprung = new GameObject("Sprung").transform;
        sprung.SetParent(body, false);
        var kids = new System.Collections.Generic.List<Transform>();
        foreach (Transform ch in body) if (ch != sprung && ch.name != "Steer" && ch.name != "Axle") kids.Add(ch);
        foreach (var ch in kids) ch.SetParent(sprung, false);
        if (dress != null) dress.Destroy();
        dress = CarDress.Attach(transform, sprung, Cars.All[c].color);
        shield = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(shield.GetComponent<Collider>());
        shield.name = "Shield";
        shield.transform.SetParent(body, false);
        shield.transform.localPosition = new Vector3(0f, 0.75f, 0f);
        shield.transform.localScale = new Vector3(3.2f, 2.4f, 5.6f);
        var mr = shield.GetComponent<MeshRenderer>();
        mr.sharedMaterial = Mats.GlassTint(new Color(0.35f, 0.85f, 1f, 0.35f));
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        shield.SetActive(false);
    }

    public void SetHuman(bool h)
    {
        human = h;
        if (h && eng == null) { eng = Sfx.Loop(gameObject, Sfx.Engine); skid = Sfx.Loop(gameObject, Sfx.Skid); }
        if (!h && eng != null) { eng.Stop(); skid.Stop(); }
    }

    // reset onto the start grid
    public void Place(Vector3 pos, float yawDeg)
    {
        transform.position = pos;
        yaw = yawDeg;
        speed = lat = vy = 0f;
        grounded = true; gNormal = Vector3.up; lastClimb = 0f;
        drifting = false; driftCharge = 0f; boostT = 0f; spinT = 0f; shrinkT = 0f; shieldT = 0f; ResetDrive();
        respawning = false; item = Item.None; rollT = 0f;
        lap = -1; cp = true; finished = false; finishTime = 0f; wrongT = 0f; toastT = 0f; offT = 0f; towing = false; towS = -1f; fadeInT = 0f; hostPlaced = false;
        Track.Proj p = Track.Nearest(pos, -1, 0);
        hint = p.i; s = p.s; lastGoodS = s; lastGoodHint = hint;
        aiShortcut = false; aiItemT = 0f; stuckT = 0f; gasHeldT = 0f;
        aiLikesShortcut = Random.value < (Track.Def != null ? Track.Def.aiShortcut : 0.4f);
        aiJump = false; padCool = 0f; airT = 0f; trickT = 0f; trickRoll = 0f; bigAir = false; lastScS = -1f;
        ApplyTransform(0f);
    }

    public void Toast(string msg, float secs = 2.2f) { toast = msg; toastT = secs; }

    public void StartBoost(float secs, float power)
    {
        boostT = Mathf.Max(boostT, secs);
        boostPower = Mathf.Max(power, boostT > secs ? boostPower : 0f);
        if (speed < spec.top) speed = Mathf.Min(spec.top * power, speed + 8f);
    }

    public void GiveItem()
    {
        if (item != Item.None || rollT > 0f || finished) return;
        int n = Game.I != null ? Game.I.karts.Count : 8;
        rollTarget = Items.Roll(place, n);
        rollT = 1.0f;
        rollTick = 0f;
        if (!human) aiItemT = Random.Range(1.2f, 5f);
    }

    // online: the hit goes through the shooter's device; this device applies it only for karts it owns
    public void NetHit(Item type, Kart from) { netApplyHit = true; try { Hit(type, from); } finally { netApplyHit = false; } }

    public void Hit(Item type, Kart from)
    {
        if (LBNet.On && !netApplyHit)
        {
            bool shooterLocal = from == null ? !netPuppet : !from.netPuppet;
            if (!shooterLocal) return;                                   // the shooter's device decides
            if (netPuppet) { if (!finished && !respawning && shieldT <= 0f) LBNet.I.SendHit(this, type, from); return; }   // the victim lives on another device
        }
        if (finished || respawning) return;
        if (shieldT > 0f)
        {
            shieldT = 0f;
            Sfx.PlayAt(Sfx.ShieldPop, transform.position, 0.8f);
            FX.Sparkle(transform.position + Vector3.up, new Color(0.4f, 0.9f, 1f), 18);
            if (human) Toast("Shield blocked the " + Items.Hit(type) + "!");
            return;
        }
        float spin = 1.1f, launch = 0f;
        switch (type)
        {
            case Item.Rocket: case Item.Homing: spin = 1.3f; launch = 6f; break;
            case Item.Fireball: spin = 1.0f; break;
            case Item.Oil: spin = 1.0f; break;
            case Item.Bowling: spin = 1.5f; launch = 5f; break;
            case Item.Mine: spin = 1.4f; launch = 8f; break;
            case Item.Lightning: spin = 0.9f; shrinkT = 3f; break;
        }
        spin /= Mathf.Sqrt(spec.weight);
        spinT = spinTotal = spin;
        drifting = false; driftCharge = 0f; boostT = 0f;
        speed *= type == Item.Oil ? 0.55f : 0.3f;
        yawRate *= 0.3f; lat *= 0.3f;
        if (launch > 0f) { vy = launch; grounded = false; }
        if (type != Item.Lightning && type != Item.Oil && type != Item.Fireball) FX.Boom(transform.position + Vector3.up * 0.6f, 0.6f);
        Sfx.PlayAt(Sfx.Spin, transform.position, 0.7f);
        if (type == Item.Rocket || type == Item.Homing || type == Item.Mine || type == Item.Bowling) Sfx.PlayAt(Sfx.Boom, transform.position, 0.6f);
        if (human) Toast(from != null && from != this ? "Hit by " + Robots.Names[from.robot] + "'s " + Items.Hit(type) + "!" : "Spun out on the " + Items.Hit(type) + "!");
        if (from != null && from != this && from.human) from.Toast("You hit " + Robots.Names[robot] + "!", 1.6f);
    }

    void Fire()
    {
        if (item == Item.None || rollT > 0f || spinT > 0f || respawning) return;
        Item it = item;
        item = Item.None;
        Items.Use(this, it);
        if (LBNet.On) LBNet.I.SendFire(this, it);
    }

    // ------------------------------------------------------------------
    // online puppet: pose from the network (LBNet.PosePuppets, ~0.1 s behind, interpolated)
    public void NetPose(Vector3 p, float yawDeg, float spd, float latV, float vyV, int lapN, float sN, int flags, float fin)
    {
        transform.position = p; yaw = yawDeg; speed = spd; lat = latV; vy = vyV; lap = lapN; s = sN;
        if (flags < 0) return;
        drifting = (flags & 1) != 0;
        if ((flags & 2) != 0) boostT = Mathf.Max(boostT, 0.1f);
        if ((flags & 4) != 0 && spinT <= 0f) { spinT = spinTotal = 1f; }
        shieldT = (flags & 8) != 0 ? Mathf.Max(shieldT, 0.5f) : 0f;
        respawning = (flags & 16) != 0;
        grounded = (flags & 32) != 0;
        if ((flags & 64) != 0 && !finished) { finished = true; finishTime = fin; }
    }

    void TickPuppet(float dt)
    {
        if (toastT > 0f) toastT -= dt;
        if (spinT > 0f) spinT -= dt;
        if (boostT > 0f) boostT -= dt;
        if (shieldT > 0f) shieldT -= dt;
        Vector3 pos = transform.position;
        float gy; Vector3 gn;
        Track.Ground(pos, out gy, out gn);
        gNormal = Vector3.Slerp(gNormal, grounded && pos.y - gy < 0.6f ? gn : Vector3.up, Mathf.Min(1f, dt * 8f));
        Vector3 fwd = Forward;
        if (boostT > 0f) FX.Flame(transform.TransformPoint(new Vector3(0f, 0.47f, -2.4f)), -fwd);
        if (drifting && grounded) FX.Spark(transform.TransformPoint(new Vector3(0.95f, 0.1f, -1.5f)), -fwd, new Color(0.35f, 0.65f, 1f));
        ApplyTransform(dt);
        lastSteer = Mathf.Lerp(lastSteer, 0f, 0.3f);
    }

    public void Tick(float dt, bool racing, bool countdown)
    {
        if (netPuppet) { TickPuppet(dt); return; }
        if (toastT > 0f) toastT -= dt;
        if (fadeInT > 0f) fadeInT -= dt;
        if (rollT > 0f)
        {
            rollT -= dt;
            rollTick -= dt;
            if (rollTick <= 0f) { rollTick = 0.07f; rollShow = Items.All[Random.Range(0, Items.All.Length)]; if (human) Sfx.Play(Sfx.Roll, 0.5f); }
            if (rollT <= 0f) { item = rollTarget; rollShow = item; if (human) Sfx.Play(Sfx.ItemGet, 0.7f); }
        }
        if (shieldT > 0f) shieldT -= dt;
        if (shrinkT > 0f) shrinkT -= dt;
        if (boostT > 0f) boostT -= dt;

        KIn i = new KIn();
        if (racing && !finished) i = human && !Autopilot ? input : AIInput(dt);
        if (OffDemo && human && racing && !finished && Game.I != null) i = OffDemoInput(i, Game.I.RaceTime);
        else if (finished) i = AIInput(dt);          // cruise after the line
        if (countdown)
        {
            // start boost: hold gas during the last second of the countdown
            KIn ci = human ? input : new KIn { gas = Random.value < 0.5f ? 1f : 0f };
            gasHeldT = ci.gas > 0.5f ? gasHeldT + dt : 0f;
            i = new KIn();
        }
        if (spinT > 0f) { spinT -= dt; i = new KIn(); }
        if (respawning) { TickRespawn(dt); ApplyTransform(dt); return; }
        if (i.fire) Fire();

        Vector3 pos = transform.position;
        bool farOff = FreeRoam && lastOffDist > Track.HalfW + OffFar;
        // AI ignores the rubber band on a jump approach so it always reaches full boost speed at the lip
        float top = spec.top * (boostT > 0f ? boostPower : 1f) * (shrinkT > 0f ? 0.72f : 1f) * (onRoad ? 1f : farOff ? 0.62f : 0.78f) * (inWater ? 0.85f : 1f) * (human || aiJump ? 1f : aiTopMul * aiSkill);
        if (padCool > 0f) padCool -= dt;
        float accel = spec.accel * (boostT > 0f ? 2.4f : 1f);

        // ---- lambo9 drift: a clutch kick (yaw kick + the rear lets go while held), held with counter-steer + throttle ----
        float beta = Mathf.Atan2(lat, Mathf.Max(1f, Mathf.Abs(speed)));
        if (!drifting && i.drift && grounded && Mathf.Abs(i.steer) > 0.3f && speed > 12f && spinT <= 0f)
        {
            drifting = true; driftDir = i.steer > 0f ? 1 : -1; driftCharge = 0f;
            yawRate += driftDir * 0.6f;
        }
        if (drifting)
        {
            if (!i.drift || speed < 9f || spinT > 0f || Mathf.Abs(beta) > 1.25f)
            {
                drifting = false;
                if (driftCharge >= 2.3f) { StartBoost(1.3f, 1.3f); Sfx.PlayAt(Sfx.DriftPop, transform.position, 0.8f, 60f, 1.15f); }
                else if (driftCharge >= 1.1f) { StartBoost(0.7f, 1.22f); Sfx.PlayAt(Sfx.DriftPop, transform.position, 0.7f); }
                driftCharge = 0f;
            }
            else if (grounded && Mathf.Abs(beta) > 0.08f) driftCharge += dt * (0.75f + 0.55f * Mathf.Abs(i.steer)) * Mathf.Clamp01(Mathf.Abs(beta) / 0.22f);
        }

        // ---- lambo9 vehicle model (longitudinal + lateral + yaw) ----
        Drive(i, dt, top, accel, farOff);
        if (grounded && speed > top) speed = Mathf.MoveTowards(speed, top, (boostT > 0f ? 4f : 10f) * dt);
        if (!onRoad && FreeRoam && grounded && Mathf.Abs(speed) > 2f) speed = Mathf.MoveTowards(speed, 0f, (farOff ? 2.2f : 1.2f) * dt);   // rough ground drags

        // ---- move ----
        Vector3 fwd = Forward, right = Right;
        pos += driveDisp;

        // ---- where are we on the track ----
        Track.Proj pm = Track.Nearest(pos, hint, 24);
        // off the main road (a long shortcut skips far more than the search window): full search so s, laps,
        // the AI target and the soft walls follow the kart to where it rejoins
        if (pm.dist > Track.HalfW + Track.Wall) pm = Track.Nearest(pos, -1, 0);
        lastOffDist = pm.dist;
        hint = pm.i;
        Track.Proj ps = Track.NearestShortcut(pos);
        bool scGap = ps.s > Track.ScGapA && ps.s < Track.ScGapB;
        bool onMain = (pm.dist < Track.HalfW + 0.8f || (pm.dist < Track.HalfW + 5f && Track.InPipe(pm.s))) && !Track.InGap(pm.s, 0f);
        bool onSc = ps.dist < Track.ScHalf + 0.8f && !scGap && ps.s > 0.5f && ps.s < Track.ScLength - 0.5f;
        onRoad = onMain || onSc;

        // soft walls
        float wallM = FreeRoam ? OffMax : Track.Wall, wallS = FreeRoam ? OffMax : 5.5f;
        float overM = pm.dist - (Track.HalfW + wallM);
        float overS = ps.dist - (Track.ScHalf + wallS);
        if (overM > 0f && overS > 0f)
        {
            bool toMain = overM < overS;
            Vector3 nrm = toMain ? Track.Flat(pos - pm.point).normalized : Track.Flat(pos - ps.point).normalized;
            float over = toMain ? overM : overS;
            pos -= nrm * over;
            Vector3 v = fwd * speed + right * lat;
            float o = Vector3.Dot(v, nrm);
            if (o > 0f)
            {
                v -= nrm * o * 1.2f;
                speed = Vector3.Dot(v, fwd) * 0.97f;
                lat = Vector3.Dot(v, right);
                yawRate *= 0.5f;
                if (o > 7f) { Sfx.PlayAt(Sfx.Bump, pos, 0.6f); if (human) Game.Shake(this, 0.3f); }
            }
        }

        // free roam: the edge of the terrain square is a soft wall too
        if (FreeRoam)
        {
            Rect tr = Track.TerrainRect;
            float m = 18f;
            Vector3 cl = new Vector3(Mathf.Clamp(pos.x, tr.xMin + m, tr.xMax - m), pos.y, Mathf.Clamp(pos.z, tr.yMin + m, tr.yMax - m));
            if ((cl - pos).sqrMagnitude > 0.0001f) { pos = cl; speed *= 0.95f; }
        }
        BarrierCheck(ref pos, pm);
        // ---- vertical ----
        float gy; Vector3 gn;
        Track.Ground(pos, out gy, out gn);
        // free roam off the road: a cliff face (> 1.6 m up in one step or a very steep wall) stops you like a natural wall
        if (FreeRoam && !onRoad && grounded && (gy - transform.position.y > 1.6f || (gn.y < 0.5f && gy > transform.position.y + 0.2f)))
        {
            Vector3 back = transform.position;
            pos = new Vector3(back.x, pos.y, back.z);
            if (speed > 4f) { Sfx.PlayAt(Sfx.Bump, pos, 0.5f); Game.Shake(this, 0.25f); }
            speed = -Mathf.Sign(speed) * Mathf.Min(3f, Mathf.Abs(speed) * 0.2f); lat = 0f;
            Track.Ground(pos, out gy, out gn);
        }
        // generated tracks: crossing a ramp lip launches at the ramp's real angle (frame-rate independent)
        if (grounded && Track.Def != null && speed > 5f)
        {
            float yTop, slope; string gname;
            if (Track.LipCross(s, pm.s, pm.lat, lastScS, ps.s, ps.dist, out yTop, out slope, out gname))
            {
                grounded = false;
                vy = Mathf.Min(14f, speed * slope);
                pos.y = Mathf.Max(pos.y, yTop);
                airT = 0f; trickT = 0f; bigAir = vy > 6f;
                if (human && gname != null) Toast(gname.ToUpper() + "!", 1.4f);
                if (Autopilot) Debug.Log("LB launch k" + id + " " + gname + " v " + speed.ToString("F1") + " vy " + vy.ToString("F1"));
            }
        }
        if (grounded)
        {
            float predicted = pos.y + lastClimb * dt;
            if (gy < predicted - 0.3f)
            {
                grounded = false;
                vy = Mathf.Clamp(lastClimb, -3f, 14f);
                airT = 0f; trickT = 0f;
                bigAir = vy > 6f;
                if (bigAir && human && Track.Def != null)
                {
                    string gname = Track.GapNameNear(pm.s, onSc ? ps.s : -1f);
                    if (gname != null) Toast(gname.ToUpper() + "!", 1.4f);
                }
            }
            else
            {
                float climb = (gy - pos.y) / Mathf.Max(dt, 0.001f);
                lastClimb = Mathf.Lerp(lastClimb, Mathf.Clamp(climb, -15f, 15f), 0.5f);
                pos.y = gy;
                gNormal = Vector3.Slerp(gNormal, gn, Mathf.Min(1f, dt * 10f));
            }
        }
        if (!grounded)
        {
            pos.y += vy * dt - 12f * dt * dt;      // exact ballistic step: same arc at 20 fps as at 60
            vy -= 24f * dt;
            gNormal = Vector3.Slerp(gNormal, Vector3.up, Mathf.Min(1f, dt * 2f));
            airT += dt;
            // big air off a ramp: a barrel roll, and a stunt boost when it lands
            if (bigAir && airT > 0.25f && spinT <= 0f) trickT += dt;
            trickRoll = trickT > 0f ? 360f * Smooth01(trickT / 0.7f) : 0f;
            if (pos.y <= gy)
            {
                if (vy < -7f) { Sfx.PlayAt(Sfx.Land, pos, Mathf.Clamp01(-vy / 18f)); bounce = Mathf.Clamp(-vy * 0.012f, 0f, 0.18f); for (int d = 0; d < 8; d++) FX.ThemeDust(pos + Random.insideUnitSphere * 1.2f, Forward, Track.Theme, 1f); }
                pos.y = gy; vy = 0f; grounded = true; lastClimb = 0f;
                if (bigAir && trickT >= 0.7f && spinT <= 0f && gy > Track.WaterY - 0.5f)
                {
                    StartBoost(0.9f, 1.25f);
                    Sfx.PlayAt(Sfx.DriftPop, pos, 0.8f, 60f, 1.3f);
                    if (human) Toast("STUNT BOOST!", 1.2f);
                }
                bigAir = false; trickT = 0f; trickRoll = 0f;
            }
        }
        else { trickRoll = 0f; }
        inWater = grounded && gy < Track.WaterY - 0.05f;
        transform.position = pos;

        // fell into the sea / the lagoon gap
        if (grounded && gy < Track.WaterY - 1.0f)
        {
            respawning = true; respawnT = respawnTotal = 1.3f; towing = false;
            FX.Splash(new Vector3(pos.x, Track.WaterY, pos.z), 30);
            Sfx.PlayAt(Sfx.Splash, pos, 0.9f);
            if (human) Toast("Splash! Back on the track...", 1.5f);
        }
        // generated tracks: fell into a chasm / off the skyway / into the lava
        if (Track.Def != null && !respawning)
        {
            bool useSc = ps.dist < pm.dist && ps.s > 0.5f && ps.s < Track.ScLength - 0.5f;
            float refY = useSc ? ps.point.y : pm.point.y;
            bool lava = Track.Lava && pos.y < Track.LavaY + 0.6f;
            // free roam far from the road: natural terrain below the road is fine, only a real drop (off the skyway, into a
            // chasm) counts; near the road + over the gaps the old rules stand
            bool near = !FreeRoam || Mathf.Min(pm.dist, useSc ? ps.dist : 1e9f) < Track.HalfW + Track.Wall + 2f || Track.InGap(pm.s, 30f);
            bool fell = near ? ((!grounded && pos.y < refY - 14f) || (grounded && gy < refY - 5f)) : ((!grounded && pos.y < refY - 30f) || (grounded && gy < refY - 28f));
            if (lava || fell)
            {
                respawning = true; respawnT = respawnTotal = 1.1f; towing = false;
                if (Autopilot) Debug.Log("LB fell k" + id + " s " + pm.s.ToString("F0") + (useSc ? " (shortcut " + ps.s.ToString("F0") + ")" : "") + " v " + speed.ToString("F1") + (lava ? " lava" : ""));
                if (lava) { FX.Boom(new Vector3(pos.x, Track.LavaY + 0.5f, pos.z), 0.8f); Sfx.PlayAt(Sfx.Boom, pos, 0.7f); }
                else Sfx.PlayAt(Sfx.Spin, pos, 0.6f);
                if (human) Toast(lava ? "Toasted in the lava! Back on the track..." : "Too short! Hit the boost pad and try again...", 1.8f);
            }
        }
        // boost pads (full width before every gap)
        if ((grounded || pos.y - gy < 2.5f) && padCool <= 0f && Track.Pads.Count > 0 && Track.PadAt(s, pm.s, pm.lat, lastScS, ps.s, ps.lat, ps.dist < pm.dist && ps.dist < Track.ScHalf + 1f))
        {
            if (Autopilot) Debug.Log("LB pad k" + id + " s " + pm.s.ToString("F0") + " v " + speed.ToString("F1"));
            StartBoost(1.7f, 1.35f);
            padCool = 1f;
            Sfx.PlayAt(Sfx.Boost, pos, 0.7f);
            if (human) Game.Shake(this, 0.15f);
        }
        if (onMain && !inWater && grounded && !Track.InGap(pm.s, 3f)) { lastGoodS = pm.s; lastGoodHint = pm.i; }
        lastScS = ps.s;

        // ---- laps ----
        float prevS = s;
        s = pm.s;
        float L = Track.Length;
        if (prevS > 0.75f * L && s < 0.25f * L)
        {
            if (cp) { cp = false; lap++; if (racing && !finished) OnLap(); }
        }
        else if (prevS < 0.25f * L && s > 0.75f * L) { lap--; cp = true; }
        if (s > 0.45f * L && s < 0.75f * L) cp = true;

        // wrong way (ignored on the shortcut)
        Vector3 tan = onSc && ps.dist < pm.dist ? ps.tan : pm.tan;
        if (racing && !finished && Vector3.Dot(fwd, tan) < -0.3f && Mathf.Abs(speed) > 4f && speed > 0f) wrongT += dt;
        else if (FreeRoam && racing && !finished && Vector3.Dot(fwd, tan) < -0.3f && Mathf.Abs(speed) <= 4f) { }   // stopped / turning: hold the timer
        else wrongT = 0f;
        if (FreeRoam && racing && !finished)
        {
            bool far = pm.dist > Track.HalfW + OffFar && !(onSc || ps.dist < Track.ScHalf + OffFar);
            offT = far ? offT + dt : Mathf.MoveTowards(offT, 0f, dt * 2f);
            if (OffDemo && (int)(Game.I.RaceTime) != offLogSec) { offLogSec = (int)Game.I.RaceTime; Debug.Log("LB off k" + id + " t " + offLogSec + " dist " + pm.dist.ToString("F0") + " onRoad " + onRoad + " offT " + offT.ToString("F1") + " wrongT " + wrongT.ToString("F1") + " v " + speed.ToString("F1") + " y " + pos.y.ToString("F1")); }
            if (!respawning && (offT > OffTowT || wrongT > WrongTowT)) StartTow(pm.s, offT > OffTowT ? "Back to the track..." : "Turned around - back on track...");
        }
        else offT = 0f;

        // AI stuck -> respawn
        if (!human && racing && Mathf.Abs(speed) < 2f && spinT <= 0f) { stuckT += dt; if (stuckT > 3f) { respawning = true; respawnT = respawnTotal = 0.2f; towing = false; stuckT = 0f; } } else stuckT = 0f;

        // effects
        if (grounded && Mathf.Abs(speed) > 8f && !onRoad && !inWater) { float amt = Mathf.Clamp01(Mathf.Abs(speed) / 30f) * (Look.Mobile ? 0.4f : 0.75f); FX.ThemeDust(pos - fwd * 2f + Right * 0.9f, fwd, Track.Theme, amt); FX.ThemeDust(pos - fwd * 2f - Right * 0.9f, fwd, Track.Theme, amt); }
        else if (grounded && Track.Theme == 3 && Mathf.Abs(speed) > 18f && !inWater) FX.ThemeDust(pos - fwd * 2.2f, fwd, 3, 0.15f);   // snow spray even on the road
        if (inWater && Mathf.Abs(speed) > 4f && Random.value < 0.6f) FX.Splash(new Vector3(pos.x, Track.WaterY + 0.1f, pos.z) - fwd * 1.5f + right * Random.Range(-1f, 1f), 2);
        if (boostT > 0f) FX.Flame(transform.TransformPoint(new Vector3(0f, 0.47f, -2.4f)), -fwd);
        if (drifting && grounded && driftCharge > 1.1f)
        {
            Color sc = driftCharge >= 2.3f ? new Color(1f, 0.55f, 0.1f) : new Color(0.35f, 0.65f, 1f);
            FX.Spark(transform.TransformPoint(new Vector3(-0.95f, 0.1f, -1.5f)), -fwd, sc);
            FX.Spark(transform.TransformPoint(new Vector3(0.95f, 0.1f, -1.5f)), -fwd, sc);
        }
        ApplyTransform(dt);
        TickAudio(i);
    }

    // ------------------------------------------------------------------ lambo9 vehicle model
    // Planar two-axle ("bicycle") car: front / rear slip angles through a Pacejka-style grip curve (peak ~11 deg, falls off
    // past it), static + dynamic load transfer (braking loads the nose -> trail-brake oversteer, throttle loads the tail),
    // a friction circle per axle (power / brake eat into cornering grip, wheel spin on loose ground), speed-sensitive
    // steering lock, a little downforce, grip from the road's banking, surface grip (tarmac / dirt / snow / water),
    // input ramps (keys + touch buttons are on/off) and a 6-speed box (rpm drives the engine sound + torque ripple).
    // Integrated in <= 1/120 s substeps so it behaves the same at 15 fps as at 60. No rigidbody: the vertical
    // (ground following, ramps, ballistic jumps) stays as it was, so every gap still flies exactly as designed.
    public const float WB = 2.84f, CgA = 1.30f, CgB = 1.54f, CgH = 0.5f, Grav = 9.81f, Redline = 8400f, Idle = 1050f;
    public float yawRate;                  // rad/s, + = turning right (yaw grows)
    public float rpm = Idle, accLong, slipRear;
    public int gear = 1;
    public bool wheelSpin, braking;
    public float gasS, brakeS, steerS;     // ramped pedals / steering
    float shiftT;
    Vector3 driveDisp;
    static readonly float[] GearTop = { 0.26f, 0.4f, 0.54f, 0.68f, 0.84f, 1.0f };   // share of (top x 1.4) each gear reaches at the redline

    float SurfaceMu(bool farOff)
    {
        int th = Track.Theme;
        if (inWater) return 0.6f;
        if (onRoad) return th == 3 ? 1.14f : 1.3f;
        float m = th == 3 ? 0.52f : th == 0 ? 0.66f : th == 5 ? 0.92f : th == 2 ? 0.7f : 0.75f;
        return farOff ? m * 0.92f : m;
    }

    public static float SteerLock(float u) { return 0.52f / (1f + u * u / 240f); }   // rad: ~30 deg parked, ~7 deg at 30 m/s

    static float Pac(float a)
    {
        float x = 11f * a;
        return Mathf.Sin(1.45f * Mathf.Atan(x - 0.3f * (x - Mathf.Atan(x))));
    }

    static float PacR(float a) { return Mathf.Abs(a) > 0.2f ? Mathf.Sign(a) * Mathf.Max(Mathf.Abs(Pac(a)), 0.96f) : Pac(a); }   // rear: no fall-off past the peak (no snap spins)

    float TorqueK()
    {
        if (shiftT > 0f) return 0.2f;
        float n = Mathf.Clamp01((rpm - Idle) / (Redline - Idle));
        return 0.86f + 0.24f * Mathf.Sin(n * Mathf.PI);
    }

    void Drive(KIn i, float dt, float top, float accel, bool farOff)
    {
        // pedal / steering ramps (a key press is a gradual squeeze, not a step)
        gasS = Mathf.MoveTowards(gasS, Mathf.Clamp01(i.gas), dt * (i.gas > gasS ? 3.4f : 6f));
        brakeS = Mathf.MoveTowards(brakeS, Mathf.Clamp01(i.brake), dt * (i.brake > brakeS ? 5f : 8f));
        bool outwards = Mathf.Abs(i.steer) > Mathf.Abs(steerS) && (steerS == 0f || Mathf.Sign(i.steer) == Mathf.Sign(steerS));
        steerS = Mathf.MoveTowards(steerS, Mathf.Clamp(i.steer, -1f, 1f), dt * (outwards ? 3.8f : 6.5f));
        braking = brakeS > 0.1f && speed > 1f;
        float mu = SurfaceMu(farOff);
        float bankB = 0f;
        if (onRoad && Track.Bank != null && hint >= 0 && hint < Track.Bank.Length) bankB = Mathf.Abs(Track.Bank[hint]) * 0.9f;
        float m = 1450f * spec.weight, Iz = m * 1.15f * CgA * CgB;
        float assist = human && !Autopilot ? 0.35f : 1.1f;          // gentle stability assist (AI: firmer)
        int n = Mathf.Clamp(Mathf.CeilToInt(dt * 120f), 1, 40);
        float h = dt / n;
        driveDisp = Vector3.zero;
        bool spinAny = false; float slipR = 0f;
        for (int k = 0; k < n; k++)
        {
            float u = speed, v = lat, r = yawRate, au = Mathf.Abs(u);
            if (grounded)
            {
                float df = Mathf.Clamp01(au / 40f);
                float muE = mu * (1f + 0.22f * df * df) + bankB;
                float fzF = Mathf.Max(0.2f * m * Grav, m * Grav * CgB / WB - m * accLong * CgH / WB);
                float fzR = Mathf.Max(0.2f * m * Grav, m * Grav * CgA / WB + m * accLong * CgH / WB);
                // longitudinal
                float drive = 0f, brk = 0f;
                if (gasS > 0.02f && u > -0.5f && u < top) drive = accel * gasS * (1.2f - 0.6f * Mathf.Clamp01(u / top)) * TorqueK();
                float tracMax = muE * Grav;
                if (drive > tracMax) { spinAny = true; drive = tracMax; }
                if (brakeS > 0.02f && u > 0.5f) brk = Mathf.Min(13.5f * brakeS, muE * Grav * 1.05f);
                // friction circles: what the tyres spend on drive / braking they can't spend on cornering
                float fxR = (drive * 0.6f + brk * 0.4f) * m, fxF = (drive * 0.4f + brk * 0.6f) * m;
                float muR = muE * (drifting ? 0.7f : 1.12f);           // wider rear tyres: a stable, slightly understeering car
                float capF = muE * 0.95f * fzF, capR = muR * fzR;
                float fyFmax = Mathf.Sqrt(Mathf.Max(capF * capF * 0.0625f, capF * capF - fxF * fxF));
                float fyRmax = Mathf.Sqrt(Mathf.Max(capR * capR * 0.0625f, capR * capR - fxR * fxR));
                if (spinAny) fyRmax *= 0.8f;
                float delta = steerS * SteerLock(au);
                float uA = Mathf.Max(au, 2.5f);
                float aF = Mathf.Atan2(v + CgA * r, uA) - delta;
                float aR = Mathf.Atan2(v - CgB * r, uA);
                float fyF = -fyFmax * Pac(aF), fyR = -fyRmax * (drifting ? Pac(aR) : PacR(aR));
                float cd = Mathf.Cos(delta), sd = Mathf.Sin(delta);
                float vdot = (fyF * cd + fyR) / m - u * r;
                float rdot = (CgA * fyF * cd - CgB * fyR) / Iz;
                float bb = Mathf.Atan2(v, Mathf.Max(au, 1f));
                if (!drifting && au > 4f)
                {
                    float rl = muE * Grav / Mathf.Max(au, 5f) * 1.05f, rt = Mathf.Clamp(u * Mathf.Tan(delta) / WB, -rl, rl);
                    rdot -= assist * (r - rt) * Mathf.Clamp01(Mathf.Abs(bb) / 0.12f);
                }
                if (drifting && Mathf.Abs(bb) > 0.42f) rdot += 9f * (Mathf.Abs(bb) - 0.42f) * Mathf.Sign(bb);   // drift assist: holds ~20-35 deg
                float udot = drive - brk * Mathf.Sign(u) - fyF * sd / m + v * r;
                udot -= 0.00042f * u * au;                                   // aero drag
                float kin = u < 0f ? 0f : Mathf.Clamp01((au - 2f) / 4f);                 // parking speeds: plain kinematic steering
                float rK = u * Mathf.Tan(steerS * 0.52f) / WB;
                r = Mathf.Lerp(rK, r + rdot * h, kin);
                v = Mathf.Lerp(v * Mathf.Max(0f, 1f - 10f * h), v + vdot * h, kin);
                u += udot * h;
                // rolling resistance + engine braking off the throttle, reverse, gas while rolling back, spin-out scrub
                bool coast = gasS <= 0.05f && brakeS <= 0.05f;
                u = Mathf.MoveTowards(u, 0f, (0.35f + (coast ? 2.3f : 0f)) * h);
                if (brakeS > 0.02f && u <= 0.5f) u = Mathf.Max(-10f, u - 9f * brakeS * h);
                if (gasS > 0.05f && u < -0.5f) u = Mathf.MoveTowards(u, 0f, 16f * h);
                if (spinT > 0f) { u = Mathf.MoveTowards(u, 0f, 20f * h); r *= Mathf.Max(0f, 1f - 6f * h); v *= Mathf.Max(0f, 1f - 6f * h); }
                accLong = Mathf.Lerp(accLong, Mathf.Clamp(udot - v * r, -6f, 6f), Mathf.Min(1f, 18f * h));   // tyre force / m (not the v*r kinematic term)
                slipR = aR;
            }
            else
            {
                u = Mathf.MoveTowards(u, 0f, 1f * h);
                v *= Mathf.Max(0f, 1f - 0.6f * h);
                r = Mathf.Lerp(r, steerS * 0.9f, Mathf.Min(1f, 2f * h));    // a little air control
                accLong = Mathf.Lerp(accLong, 0f, Mathf.Min(1f, 6f * h));
            }
            speed = u; lat = v; yawRate = Mathf.Clamp(r, -4f, 4f);
            yaw += yawRate * h * Mathf.Rad2Deg;
            driveDisp += (Forward * speed + Right * lat) * h;
        }
        wheelSpin = spinAny && grounded;
        slipRear = slipR;
        // 6-speed box: rpm from road speed in the current gear (free revs in the air / spinning wheels)
        float gTop = spec.top * 1.4f * GearTop[gear - 1];
        float rpmT = Idle + (Redline - Idle) * Mathf.Clamp01(Mathf.Abs(speed) / gTop);
        if (!grounded || wheelSpin) rpmT = Mathf.Max(rpmT, Mathf.Lerp(rpmT, Redline * 0.97f, gasS * 0.6f));
        if (Mathf.Abs(speed) < 1f) rpmT = Idle + gasS * 3800f;
        rpm = Mathf.Lerp(rpm, rpmT, Mathf.Min(1f, dt * (shiftT > 0f ? 9f : 14f)));
        if (shiftT > 0f) shiftT -= dt;
        else if (gear < GearTop.Length && rpm > 8000f && gasS > 0.3f && grounded)
        {
            gear++; shiftT = 0.15f;
            if (human && gasS > 0.8f && Random.value < 0.6f) { FX.Flame(transform.TransformPoint(new Vector3(0f, 0.47f, -2.4f)), -Forward); Sfx.PlayAt(Sfx.Bump, transform.position, 0.14f, 40f, 0.55f); }
        }
        else if (gear > 1 && rpm < 4300f) { gear--; shiftT = 0.09f; }
        if (Mathf.Abs(speed) < 0.5f && gear > 1 && shiftT <= 0f) gear = 1;
    }

    // lambo9 solid armco (Roadside): a kart crossing a barrier's line from either side is pushed back and bounces off
    // (restitution 0.3, scrub, sparks), unless it is flying over it
    float barLat = 0f; bool barInit;
    void BarrierCheck(ref Vector3 pos, Track.Proj pm)
    {
        float prev = barLat;
        barLat = pm.lat;
        if (!barInit) { barInit = true; return; }
        if (Track.Barriers.Count == 0 || pos.y - pm.point.y > 1.6f || pm.dist > Track.HalfW + 8f) return;
        foreach (var b in Track.Barriers)
        {
            float d = Track.WrapDist(pm.s - b.a);
            if (d < 0f || d > b.b - b.a) continue;
            float L = b.lat, sideLat = pm.lat * b.side, prevSide = prev * b.side;
            const float half = 1.05f;
            bool fromIn = prevSide <= L - half + 0.05f && sideLat > L - half, fromOut = prevSide >= L + half - 0.05f && sideLat < L + half;
            if (!fromIn && !fromOut) continue;
            float target = fromIn ? L - half : L + half;
            Vector3 nrm = pm.right * b.side * (fromIn ? 1f : -1f);     // direction into the barrier
            pos += pm.right * b.side * (target - sideLat);
            Vector3 v = Forward * speed + Right * lat;
            float o = Vector3.Dot(v, nrm);
            if (o > 0f)
            {
                v -= nrm * o * 1.3f;
                Vector3 tv = v - nrm * Vector3.Dot(v, nrm);
                v -= tv * Mathf.Clamp01(0.012f * o);
                speed = Vector3.Dot(v, Forward); lat = Vector3.Dot(v, Right);
                yawRate *= 0.55f;
                if (o > 3f)
                {
                    Vector3 cp = pos + nrm * half;
                    for (int q = 0; q < 6; q++) FX.Spark(cp + Vector3.up * 0.5f, -nrm + Random.insideUnitSphere * 0.5f, new Color(1f, 0.75f, 0.35f));
                    Sfx.PlayAt(Sfx.Bump, pos, Mathf.Clamp01(o / 14f));
                    if (human) Game.Shake(this, Mathf.Clamp(o * 0.03f, 0.1f, 0.4f));
                }
            }
            barLat = Vector3.Dot(Track.Flat(pos - pm.point), pm.right);
            break;
        }
    }

    void ResetDrive()
    {
        yawRate = 0f; accLong = 0f; slipRear = 0f; gear = 1; rpm = Idle; shiftT = 0f; gasS = brakeS = steerS = 0f; wheelSpin = false; braking = false;
        driveDisp = Vector3.zero; barInit = false;
    }

    // called at GO
    public void LaunchStart()
    {
        if (gasHeldT > 0.05f && gasHeldT < 1.1f) { StartBoost(1.2f, 1.3f); if (human) Toast("Rocket start!", 1.2f); }
        gasHeldT = 0f;
    }

    void OnLap()
    {
        if (Autopilot && Game.I != null) Debug.Log("LB lap k" + id + " lap " + lap + " t " + Game.I.RaceTime.ToString("F1") + (human ? " P1" : ""));
        if (lap >= Track.Laps)
        {
            finished = true;
            finishTime = Game.I != null ? Game.I.RaceTime : 0f;
            if (Game.I != null) Game.I.OnFinish(this);
        }
        else if (lap >= 1 && human)
        {
            Toast(lap == Track.Laps - 1 ? "FINAL LAP!" : "LAP " + (lap + 1) + " / " + Track.Laps, 2f);
            Sfx.Play(Sfx.Finish, 0.6f);
        }
    }

    // smooth fade + tow to the nearest track point (free roam)
    void StartTow(float nearS, string msg)
    {
        respawning = true; towing = true; respawnT = respawnTotal = 0.75f; towS = nearS;
        if (Autopilot || OffDemo) Debug.Log("LB tow k" + id + " off " + offT.ToString("F1") + " s wrong " + wrongT.ToString("F1") + " s -> s " + nearS.ToString("F0"));
        offT = 0f; wrongT = 0f;
        Toast(msg, 1.6f);
    }

    void TickRespawn(float dt)
    {
        respawnT -= dt;
        if (towing) speed = Mathf.MoveTowards(speed, 0f, 40f * dt);
        else { transform.position += Vector3.down * dt * 1.5f; speed = 0f; }
        lat = 0f;
        if (towing) { transform.position += Forward * speed * dt; }
        if (respawnT > 0f) return;
        respawning = false;
        float rs = Track.SafeRespawnS(towing && towS >= 0f ? towS : lastGoodS);
        if (towing) { fadeInT = 0.45f; towing = false; towS = -1f; }
        Vector3 p = Track.PointAt(rs, 0f);
        float y; Vector3 n;
        Track.Ground(p, out y, out n);
        transform.position = new Vector3(p.x, y + 0.5f, p.z);
        Vector3 t = Track.TangentAt(rs);
        yaw = Mathf.Atan2(t.x, t.z) * Mathf.Rad2Deg;
        hint = Track.IndexAt(rs);
        vy = 0f; grounded = false; drifting = false; spinT = 0f; ResetDrive(); gear = 2;
        speed = Track.Def != null ? 12f : 6f;
        bigAir = false; trickT = 0f; trickRoll = 0f;
        aiShortcut = false;
    }

    // lambo9 visuals: measured accelerations drive a sprung body (roll / pitch / dive on a damped spring), the steered
    // wheels show the real lock (counter-steer in a slide), skids + tyre smoke from the real rear slip / wheel spin,
    // brake lights, a soft contact shadow under the car
    float visYaw, visSpeed, visAx, visAy, rollA, rollV, pitchA, pitchV;
    Transform sprung;
    CarDress dress;
    void ApplyTransform(float dt)
    {
        transform.rotation = Quaternion.FromToRotation(Vector3.up, gNormal) * Quaternion.Euler(0f, yaw, 0f);
        if (dt > 0f)
        {
            float yr = Mathf.DeltaAngle(visYaw, yaw) * Mathf.Deg2Rad / dt, al = (speed - visSpeed) / dt;
            if (respawning || Mathf.Abs(yr) > 8f) { yr = 0f; al = 0f; }
            visAy = Mathf.Lerp(visAy, Mathf.Clamp(speed * yr, -20f, 20f), Mathf.Min(1f, dt * 8f));
            visAx = Mathf.Lerp(visAx, Mathf.Clamp(al, -16f, 16f), Mathf.Min(1f, dt * 8f));
        }
        visYaw = yaw; visSpeed = speed;
        float beta = Mathf.Atan2(lat, Mathf.Max(1f, Mathf.Abs(speed)));
        float str = 0f;
        if (grounded && !respawning && Mathf.Abs(speed) > 5f)
        {
            str = drifting ? 0.85f : Mathf.Max(Mathf.Clamp01((Mathf.Abs(beta) - 0.1f) / 0.2f), Mathf.Clamp01((Mathf.Abs(lat) - 4.5f) / 4f));
            if (wheelSpin) str = Mathf.Max(str, 0.6f);
        }
        if (str > 0.05f)
        {
            Vector3 rt = transform.right;
            Vector3 wl = transform.TransformPoint(new Vector3(-0.85f, 0f, -1.35f)), wr = transform.TransformPoint(new Vector3(0.85f, 0f, -1.35f));
            Skids.Mark(id * 2, wl, gNormal, rt, str);
            Skids.Mark(id * 2 + 1, wr, gNormal, rt, str);
            if (onRoad && !inWater && str > 0.25f && Random.value < str * (Look.Mobile ? 0.35f : 0.7f) && !Gfx.Low)
                FX.TyreSmoke(Random.value < 0.5f ? wl : wr, Velocity * 0.25f, str);
        }
        if (body == null) return;
        steerVisual = Mathf.Lerp(steerVisual, input.steer * (human ? 1f : 0.6f), Mathf.Min(1f, dt * 10f));
        if (spinT > 0f) { float k = 1f - spinT / spinTotal; spinAngle = 720f * (1f - (1f - k) * (1f - k)); } else spinAngle = 0f;
        bounce = Mathf.MoveTowards(bounce, 0f, dt * 0.8f);
        // sprung body: + roll = leans to the outside of a right turn, + pitch = nose dives (braking), - = squat (throttle)
        float rollT = grounded ? Mathf.Clamp(visAy * 0.36f, -5f, 5f) : 0f, pitchT = grounded ? Mathf.Clamp(-visAx * 0.28f, -3.2f, 3.6f) : 0f;
        int ns = Mathf.Clamp(Mathf.CeilToInt(dt / 0.01f), 1, 12);
        float hs = dt / ns;
        for (int q = 0; q < ns; q++)
        {
            rollV += (95f * (rollT - rollA) - 10f * rollV) * hs; rollA += rollV * hs;
            pitchV += (110f * (pitchT - pitchA) - 11f * pitchV) * hs; pitchA += pitchV * hs;
        }
        rollA = Mathf.Clamp(rollA, -7f, 7f); pitchA = Mathf.Clamp(pitchA, -5f, 5f);
        body.localRotation = Quaternion.Euler(0f, spinAngle, trickRoll);
        body.localPosition = new Vector3(0f, -bounce, 0f);
        if (sprung != null)
        {
            sprung.localRotation = Quaternion.Euler(pitchA, 0f, rollA);
            sprung.localPosition = new Vector3(0f, -bounce * 0.6f - Mathf.Abs(rollA) * 0.004f, 0f);
        }
        float sc = shrinkT > 0f ? 0.7f : 1f;
        body.localScale = Vector3.MoveTowards(body.localScale, Vector3.one * sc, dt * 2f);
        wheelRot += speed * dt / 0.37f * Mathf.Rad2Deg * (wheelSpin ? 1.6f : 1f);
        if (spinners != null) foreach (var w in spinners) if (w != null) w.localRotation = Quaternion.Euler(wheelRot, 0f, 0f);
        float steerAng = netPuppet ? LastSteer * 20f : steerS * SteerLock(Mathf.Abs(speed)) * Mathf.Rad2Deg * 1.15f;
        if (steerPivots != null) foreach (var p in steerPivots) if (p != null) p.localRotation = Quaternion.Euler(0f, steerAng, 0f);
        headYaw = Mathf.Lerp(headYaw, LastSteer * 28f + (drifting ? driftDir * 18f : 0f), Mathf.Min(1f, dt * 5f));
        if (head != null) head.localRotation = Quaternion.Euler(0f, headYaw, 0f);
        if (wheelT != null) wheelT.localRotation = Quaternion.AngleAxis(-(netPuppet ? steerAng : steerS * 30f) * 2.2f, RobotModel.WheelAxis);
        if (dress != null) dress.Tick(this, braking || (netPuppet && visAx < -4f), boostT > 0f);
        if (shield != null)
        {
            bool on = shieldT > 0f;
            if (shield.activeSelf != on) shield.SetActive(on);
            if (on) shield.transform.localScale = new Vector3(3.2f, 2.4f, 5.6f) * (1f + 0.04f * Mathf.Sin(Time.time * 8f)) * (shieldT < 2f && ((int)(shieldT * 6f)) % 2 == 0 ? 0.92f : 1f);
        }
    }

    float lastSteer, lastOffDist;
    static float Smooth01(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    float LastSteer { get { return lastSteer; } }

    void TickAudio(KIn i)
    {
        lastSteer = Mathf.Lerp(lastSteer, i.steer, 0.3f);
        if (eng == null) return;
        if (!Sfx.Unlocked) return;
        if (!eng.isPlaying) eng.Play();
        float hs = Game.I != null ? Game.I.HumanVolumeScale : 1f;
        float sp = Mathf.Abs(speed) / spec.top;
        // lambo9: pitch follows the gearbox rpm (drops on every upshift), louder + harsher under load
        float rn = Mathf.Clamp01((rpm - Idle) / (Redline - Idle));
        eng.pitch = Mathf.Lerp(eng.pitch, 0.6f + rn * 1.7f + (boostT > 0f ? 0.08f : 0f), 0.5f);
        eng.volume = (0.13f + 0.2f * gasS + 0.1f * rn + 0.05f * sp) * hs;
        float bt = Mathf.Abs(Mathf.Atan2(lat, Mathf.Max(1f, Mathf.Abs(speed))));
        bool sk = grounded && Mathf.Abs(speed) > 5f && (drifting || bt > 0.13f || wheelSpin) && !inWater;
        if (sk && !skid.isPlaying) skid.Play();
        skid.volume = Mathf.MoveTowards(skid.volume, sk ? 0.22f * hs : 0f, Time.deltaTime * 2f);
        if (!sk && skid.volume < 0.01f && skid.isPlaying) skid.Stop();
    }

    // &lboff=1 probe: P1 leaves the road at race t 5 s and drives straight into the terrain (expect a tow at ~far + 5.5 s),
    // then at t 26 s turns round and drives the wrong way along the road (expect WRONG WAY, then a tow)
    int offLogSec = -1;
    bool aiBackwards; float offDemoYaw = 999f;
    KIn OffDemoInput(KIn ai, float t)
    {
        aiBackwards = t > 26f && t < 45f;
        if (t > 5f && t < 22f)
        {
            if (offDemoYaw > 900f) offDemoYaw = yaw + 70f;
            float d = Mathf.DeltaAngle(yaw, offDemoYaw);
            return new KIn { gas = 1f, steer = Mathf.Clamp(d / 20f, -1f, 1f) };
        }
        if (t >= 26f && t < 27.5f && !aiBackwards) return ai;
        return ai;
    }

    // ------------------------------------------------------------------ AI
    KIn AIInput(float dt)
    {
        var i = new KIn();
        Vector3 pos = transform.position, fwd = Forward;
        float look = 9f + Mathf.Abs(speed) * 0.5f;
        aiLaneT -= dt;
        if (aiLaneT <= 0f) { aiLaneT = Random.Range(2.5f, 6f); aiLaneTarget = Random.Range(-4f, 4f); }
        aiLane = Mathf.MoveTowards(aiLane, aiLaneTarget, dt * 1.5f);

        // shortcut decision when approaching it
        if (!finished && aiLikesShortcut && !aiShortcut)
        {
            float ds = Track.ScEnterS - s;
            if (ds > 0f && ds < 30f) aiShortcut = true;
        }
        Vector3 target;
        if (aiShortcut)
        {
            Track.Proj ps = Track.NearestShortcut(pos);
            if (ps.s + look >= Track.ScLength - 1f || ps.dist > 20f) { aiShortcut = false; target = Track.PointAt(s + look, aiLane); }
            else target = Track.ShortcutPoint(ps.s + look);
        }
        else target = Track.PointAt(s + look, aiLane);
        if (aiBackwards) target = Track.PointAt(s - look, 0f);
        // jump ahead (generated tracks): line up on the centre, full gas, no brakes, no rubber band
        float jz = -1f;
        if (Track.Def != null && !finished)
        {
            float scS = -1f;
            if (aiShortcut) scS = Track.NearestShortcut(pos).s;
            jz = Track.JumpAhead(s, scS, 130f);
        }
        aiJump = jz >= 0f;
        if (aiJump)
        {
            aiLaneTarget = 0f; aiLane = Mathf.MoveTowards(aiLane, 0f, dt * 5f);
            if (!aiShortcut) target = Track.PointAt(s + look, aiLane);
        }
        Vector3 to = Track.Flat(target - pos);
        float ang = Vector3.SignedAngle(fwd, to, Vector3.up);
        // lambo9: pure-pursuit steering through the real steering lock (the car model has a speed-sensitive lock + grip limits)
        float au = Mathf.Abs(speed);
        float kap = 2f * Mathf.Sin(ang * Mathf.Deg2Rad) / Mathf.Max(5f, to.magnitude);
        float dReq = Mathf.Atan(WB * kap) * 1.1f - lat * 0.012f;
        i.steer = au < 6f ? Mathf.Clamp(ang / 26f, -1f, 1f) : Mathf.Clamp(dReq / SteerLock(au), -1f, 1f);
        float bend = aiShortcut ? 0f : Track.Bend(s, 20f + au * 0.9f);
        // corner speed from the road's curvature ahead (grip-limited) and a braking distance at ~1 g
        float muA = (Track.Theme == 3 ? 1.14f : 1.3f) * 0.86f * aiSkill;
        float want = spec.top * 1.05f;
        if (!aiShortcut)
            for (float d = 6f; d <= 120f; d += 16f)
            {
                float c = Track.Bend(s + d - 8f, 16f) * Mathf.Deg2Rad / 16f;
                if (c < 0.002f) continue;
                int bi = Track.IndexAt(s + d);
                float bk = Track.Bank != null && bi < Track.Bank.Length ? Mathf.Abs(Track.Bank[bi]) * 0.9f : 0f;
                float vs = Mathf.Sqrt((muA + bk) * Grav / c);
                want = Mathf.Min(want, Mathf.Sqrt(vs * vs + 2f * 10f * Mathf.Max(0f, d - au * 0.3f)));
            }
        if (boostT > 0f) want = Mathf.Max(want, Mathf.Min(want * boostPower, spec.top * boostPower));
        if (Mathf.Abs(ang) > 60f) want = Mathf.Min(want, 12f);
        i.gas = speed < want - 0.5f ? 1f : speed < want + 1f ? 0.35f : 0f;
        i.brake = speed > want + 1.5f ? Mathf.Clamp((speed - want) / 5f, 0.3f, 1f) : 0f;
        if (aiJump && (jz < 75f || speed < want + 3f)) { i.gas = 1f; i.brake = 0f; }   // last 75 m before a lip: flat out, no brakes
        if (finished) { i.gas = speed < 14f ? 0.6f : 0f; return i; }

        // items
        if (item != Item.None && rollT <= 0f)
        {
            aiItemT -= dt;
            bool use = aiItemT <= 0f;
            if (Game.I != null)
            {
                if (Items.IsForward(item))
                {
                    foreach (Kart k in Game.I.karts)
                    {
                        if (k == this) continue;
                        Vector3 d = Track.Flat(k.transform.position - pos);
                        if (d.magnitude < 55f && Vector3.Angle(fwd, d) < 14f) { use = true; break; }
                    }
                }
                else if (Items.IsDrop(item))
                {
                    foreach (Kart k in Game.I.karts)
                    {
                        if (k == this) continue;
                        Vector3 d = Track.Flat(k.transform.position - pos);
                        if (d.magnitude < 22f && Vector3.Angle(-fwd, d) < 30f) { use = true; break; }
                    }
                }
                else if (item == Item.Boost && bend > 30f) use = false;
                else if (item == Item.Boost && jz > 20f && jz < 70f) use = true;      // save the boost for a jump
                else if (item == Item.Boost && Track.Def != null && jz < 0f && Track.Gaps.Count > 0 && aiItemT > -4f) use = false;
                else if (item == Item.Lightning && place <= 1) use = false;
            }
            if (use) i.fire = true;
        }
        return i;
    }
}
