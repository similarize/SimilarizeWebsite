using UnityEngine;

// One racer: arcade car physics (no rigidbody: ray-cast ground following, jumps off ramps, soft walls),
// drift + mini-turbo, items, spin-outs, lap counting, and the AI driver.
public class Kart : MonoBehaviour
{
    public int id;
    public int car, robot;
    public bool human;
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
    float driftVisual, steerVisual, spinAngle, wheelRot, headYaw, bounce;
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
    public float RaceDist { get { return finished ? Track.Laps + 10f - place * 0.01f : lap + s / Track.Length; } }

    // messages for this car's player
    public string toast = "";
    public float toastT;

    // AI
    float aiLane, aiLaneTarget, aiLaneT, aiItemT, stuckT;
    bool aiShortcut, aiLikesShortcut;
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
        drifting = false; driftCharge = 0f; boostT = 0f; spinT = 0f; shrinkT = 0f; shieldT = 0f;
        respawning = false; item = Item.None; rollT = 0f;
        lap = -1; cp = true; finished = false; finishTime = 0f; wrongT = 0f; toastT = 0f;
        Track.Proj p = Track.Nearest(pos, -1, 0);
        hint = p.i; s = p.s; lastGoodS = s; lastGoodHint = hint;
        aiShortcut = false; aiItemT = 0f; stuckT = 0f; gasHeldT = 0f;
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

    public void Hit(Item type, Kart from)
    {
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
    }

    // ------------------------------------------------------------------
    public void Tick(float dt, bool racing, bool countdown)
    {
        if (toastT > 0f) toastT -= dt;
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
        if (racing && !finished) i = human ? input : AIInput(dt);
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
        float top = spec.top * (boostT > 0f ? boostPower : 1f) * (shrinkT > 0f ? 0.72f : 1f) * (onRoad ? 1f : 0.78f) * (inWater ? 0.85f : 1f) * (human ? 1f : aiTopMul * aiSkill);
        float accel = spec.accel * (boostT > 0f ? 2.4f : 1f);

        // ---- longitudinal ----
        if (grounded)
        {
            if (i.gas > 0.05f && speed > -0.5f)
            {
                if (speed < top) speed = Mathf.Min(top, speed + accel * i.gas * dt * (1.2f - 0.6f * Mathf.Clamp01(speed / top)));
            }
            if (speed > top) speed = Mathf.MoveTowards(speed, top, (boostT > 0f ? 4f : 10f) * dt);
            if (i.brake > 0.05f)
            {
                if (speed > 0.5f) speed -= 30f * i.brake * dt;
                else speed = Mathf.Max(-10f, speed - 12f * i.brake * dt);
            }
            if (i.gas <= 0.05f && i.brake <= 0.05f) speed = Mathf.MoveTowards(speed, 0f, 4.5f * dt);
            if (i.gas > 0.05f && speed < -0.5f) speed = Mathf.MoveTowards(speed, 0f, 25f * dt);
            if (spinT > 0f) speed = Mathf.MoveTowards(speed, 0f, 20f * dt);
        }
        else speed = Mathf.MoveTowards(speed, 0f, 1f * dt);

        // ---- steering + drift ----
        float speedF = Mathf.Clamp01(Mathf.Abs(speed) / 7f);
        float turn = i.steer * spec.handling * speedF * (1f - 0.22f * Mathf.Clamp01(speed / spec.top));
        if (speed < 0f) turn = -turn;
        if (!drifting && i.drift && grounded && Mathf.Abs(i.steer) > 0.3f && speed > 12f && spinT <= 0f)
        {
            drifting = true; driftDir = i.steer > 0f ? 1 : -1; driftCharge = 0f;
            vy = 2.6f; grounded = false;    // little hop into the drift
        }
        if (drifting)
        {
            if (!i.drift || speed < 9f || spinT > 0f)
            {
                drifting = false;
                if (driftCharge >= 2.3f) { StartBoost(1.3f, 1.3f); Sfx.PlayAt(Sfx.DriftPop, transform.position, 0.8f, 60f, 1.15f); }
                else if (driftCharge >= 1.1f) { StartBoost(0.7f, 1.22f); Sfx.PlayAt(Sfx.DriftPop, transform.position, 0.7f); }
                driftCharge = 0f;
            }
            else
            {
                float k = 0.78f + 0.48f * i.steer * driftDir;      // steer into the drift = tighter, against = wider
                turn = driftDir * spec.handling * Mathf.Clamp(k, 0.3f, 1.3f);
                if (grounded) driftCharge += dt * (0.75f + 0.55f * Mathf.Abs(i.steer));
            }
        }
        yaw += turn * dt * (grounded ? 1f : 0.5f);

        // lateral slip (drift slides outwards, normal driving grips)
        float latT = drifting ? -driftDir * Mathf.Abs(speed) * 0.17f : 0f;
        lat = Mathf.Lerp(lat, latT, Mathf.Min(1f, dt * (drifting ? 4f : grounded ? 9f : 1f)));

        // ---- move ----
        Vector3 fwd = Forward, right = Right;
        pos += (fwd * speed + right * lat) * dt;

        // ---- where are we on the track ----
        Track.Proj pm = Track.Nearest(pos, hint, 24);
        hint = pm.i;
        Track.Proj ps = Track.NearestShortcut(pos);
        bool scGap = ps.s > Track.ScGapA && ps.s < Track.ScGapB;
        bool onMain = pm.dist < Track.HalfW + 0.8f;
        bool onSc = ps.dist < Track.ScHalf + 0.8f && !scGap && ps.s > 0.5f && ps.s < Track.ScLength - 0.5f;
        onRoad = onMain || onSc;

        // soft walls
        float overM = pm.dist - (Track.HalfW + Track.Wall);
        float overS = ps.dist - (Track.ScHalf + 5.5f);
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
                if (o > 7f) { Sfx.PlayAt(Sfx.Bump, pos, 0.6f); if (human) Game.Shake(this, 0.3f); }
            }
        }

        // ---- vertical ----
        float gy; Vector3 gn;
        Track.Ground(pos, out gy, out gn);
        if (grounded)
        {
            float predicted = pos.y + lastClimb * dt;
            if (gy < predicted - 0.3f)
            {
                grounded = false;
                vy = Mathf.Clamp(lastClimb, -3f, 14f);
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
            vy -= 24f * dt;
            pos.y += vy * dt;
            gNormal = Vector3.Slerp(gNormal, Vector3.up, Mathf.Min(1f, dt * 2f));
            if (pos.y <= gy)
            {
                if (vy < -7f) { Sfx.PlayAt(Sfx.Land, pos, Mathf.Clamp01(-vy / 18f)); bounce = Mathf.Clamp(-vy * 0.012f, 0f, 0.18f); for (int d = 0; d < 6; d++) FX.Dust(pos, 1f); }
                pos.y = gy; vy = 0f; grounded = true; lastClimb = 0f;
            }
        }
        inWater = grounded && gy < Track.WaterY - 0.05f;
        transform.position = pos;

        // fell into the sea / the lagoon gap
        if (grounded && gy < Track.WaterY - 1.0f)
        {
            respawning = true; respawnT = 1.3f;
            FX.Splash(new Vector3(pos.x, Track.WaterY, pos.z), 30);
            Sfx.PlayAt(Sfx.Splash, pos, 0.9f);
            if (human) Toast("Splash! Back on the track...", 1.5f);
        }
        if (onMain && !inWater) { lastGoodS = pm.s; lastGoodHint = pm.i; }

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
        if (racing && !finished && Vector3.Dot(fwd, tan) < -0.3f && Mathf.Abs(speed) > 4f && speed > 0f) wrongT += dt; else wrongT = 0f;

        // AI stuck -> respawn
        if (!human && racing && Mathf.Abs(speed) < 2f && spinT <= 0f) { stuckT += dt; if (stuckT > 3f) { respawning = true; respawnT = 0.2f; stuckT = 0f; } } else stuckT = 0f;

        // effects
        if (grounded && Mathf.Abs(speed) > 10f && !onRoad && !inWater) FX.Dust(pos - fwd * 2f, 0.35f);
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

    // called at GO
    public void LaunchStart()
    {
        if (gasHeldT > 0.05f && gasHeldT < 1.1f) { StartBoost(1.2f, 1.3f); if (human) Toast("Rocket start!", 1.2f); }
        gasHeldT = 0f;
    }

    void OnLap()
    {
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

    void TickRespawn(float dt)
    {
        respawnT -= dt;
        transform.position += Vector3.down * dt * 1.5f;
        speed = 0f; lat = 0f;
        if (respawnT > 0f) return;
        respawning = false;
        float rs = lastGoodS;
        Vector3 p = Track.PointAt(rs, 0f);
        float y; Vector3 n;
        Track.Ground(p, out y, out n);
        transform.position = new Vector3(p.x, y + 0.5f, p.z);
        Vector3 t = Track.TangentAt(rs);
        yaw = Mathf.Atan2(t.x, t.z) * Mathf.Rad2Deg;
        hint = lastGoodHint;
        vy = 0f; grounded = false; drifting = false; spinT = 0f;
        speed = 6f;
        aiShortcut = false;
    }

    void ApplyTransform(float dt)
    {
        transform.rotation = Quaternion.FromToRotation(Vector3.up, gNormal) * Quaternion.Euler(0f, yaw, 0f);
        if (body == null) return;
        float wantDrift = drifting ? driftDir * 24f : 0f;
        driftVisual = Mathf.Lerp(driftVisual, wantDrift, Mathf.Min(1f, dt * 8f));
        steerVisual = Mathf.Lerp(steerVisual, input.steer * (human ? 1f : 0.6f), Mathf.Min(1f, dt * 10f));
        if (spinT > 0f) { float k = 1f - spinT / spinTotal; spinAngle = 720f * (1f - (1f - k) * (1f - k)); } else spinAngle = 0f;
        bounce = Mathf.MoveTowards(bounce, 0f, dt * 0.8f);
        float roll = -steerVisual * Mathf.Clamp01(Mathf.Abs(speed) / 20f) * 3f - (drifting ? driftDir * 3f : 0f);
        body.localRotation = Quaternion.Euler(0f, driftVisual + spinAngle, roll);
        body.localPosition = new Vector3(0f, -bounce, 0f);
        float sc = shrinkT > 0f ? 0.7f : 1f;
        body.localScale = Vector3.MoveTowards(body.localScale, Vector3.one * sc, dt * 2f);
        wheelRot += speed * dt / 0.37f * Mathf.Rad2Deg;
        if (spinners != null) foreach (var w in spinners) if (w != null) w.localRotation = Quaternion.Euler(wheelRot, 0f, 0f);
        float steerAng = (drifting ? -driftDir * 10f : 0f) + LastSteer * 26f;
        if (steerPivots != null) foreach (var p in steerPivots) if (p != null) p.localRotation = Quaternion.Euler(0f, steerAng, 0f);
        headYaw = Mathf.Lerp(headYaw, LastSteer * 28f + (drifting ? driftDir * 18f : 0f), Mathf.Min(1f, dt * 5f));
        if (head != null) head.localRotation = Quaternion.Euler(0f, headYaw, 0f);
        if (wheelT != null) wheelT.localRotation = Quaternion.AngleAxis(-steerAng * 2.2f, RobotModel.WheelAxis);
        if (shield != null)
        {
            bool on = shieldT > 0f;
            if (shield.activeSelf != on) shield.SetActive(on);
            if (on) shield.transform.localScale = new Vector3(3.2f, 2.4f, 5.6f) * (1f + 0.04f * Mathf.Sin(Time.time * 8f)) * (shieldT < 2f && ((int)(shieldT * 6f)) % 2 == 0 ? 0.92f : 1f);
        }
    }

    float lastSteer;
    float LastSteer { get { return lastSteer; } }

    void TickAudio(KIn i)
    {
        lastSteer = Mathf.Lerp(lastSteer, i.steer, 0.3f);
        if (eng == null) return;
        if (!Sfx.Unlocked) return;
        if (!eng.isPlaying) eng.Play();
        float hs = Game.I != null ? Game.I.HumanVolumeScale : 1f;
        float sp = Mathf.Abs(speed) / spec.top;
        eng.pitch = 0.55f + sp * 1.15f + i.gas * 0.12f + (boostT > 0f ? 0.15f : 0f) + (grounded ? 0f : 0.15f * i.gas);
        eng.volume = (0.16f + 0.22f * i.gas + 0.12f * sp) * hs;
        bool sk = drifting && grounded;
        if (sk && !skid.isPlaying) skid.Play();
        skid.volume = Mathf.MoveTowards(skid.volume, sk ? 0.22f * hs : 0f, Time.deltaTime * 2f);
        if (!sk && skid.volume < 0.01f && skid.isPlaying) skid.Stop();
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
        Vector3 to = Track.Flat(target - pos);
        float ang = Vector3.SignedAngle(fwd, to, Vector3.up);
        i.steer = Mathf.Clamp(ang / 26f, -1f, 1f);
        float bend = aiShortcut ? 0f : Track.Bend(s, 20f + Mathf.Abs(speed) * 0.9f);
        float want = spec.top * (bend > 70f ? 0.6f : bend > 50f ? 0.72f : bend > 32f ? 0.86f : 1.05f);
        if (Mathf.Abs(ang) > 60f) want = Mathf.Min(want, 12f);
        i.gas = speed < want ? 1f : 0f;
        i.brake = speed > want + 4f ? 0.7f : 0f;
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
                else if (item == Item.Lightning && place <= 1) use = false;
            }
            if (use) i.fire = true;
        }
        return i;
    }
}
