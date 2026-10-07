using UnityEngine;

// Simple airsoft bot: wanders between cover, spots enemies with line of sight, shoots at balloons with aim error.
public class BotBrain : MonoBehaviour
{
    Soldier s;
    Soldier target;
    Vector3 goal;
    bool hasGoal;
    float scanT, waitT, errT, strafeT, stuckT, unseenT, reactT, burstT, pauseT, lookT;
    Vector3 aimErr;
    float strafe = 1f;
    Vector3 stuckPos;
    float skill;
    bool wantsMove;

    void Start()
    {
        s = GetComponent<Soldier>();
        skill = Random.Range(0.75f, 1.2f);
        scanT = Random.value * 0.3f;
    }

    void Update()
    {
        if (s == null) return;
        Game g = Game.I;
        s.inMove = Vector2.zero; s.inFire = false; s.inAds = false;
        wantsMove = false;
        if (g == null || g.state != Game.State.Playing || !s.alive) { target = null; hasGoal = false; return; }
        float dt = Time.deltaTime;

        scanT -= dt;
        if (scanT <= 0f) { scanT = 0.25f; Scan(); }
        if (target != null && !target.alive) target = null;

        if (target != null) Engage(dt); else Roam(dt);
        Stuck(dt);
        if (s.ammo < 6 && target == null && !s.Reloading) s.inReload = true;
    }

    void Scan()
    {
        Soldier best = null; float bd = float.MaxValue;
        foreach (var o in Game.I.soldiers)
        {
            if (o == s || o == null || !o.alive) continue;
            Vector3 d = o.transform.position - s.transform.position;
            float dist = d.magnitude;
            if (dist > 80f) continue;
            float ang = Vector3.Angle(s.transform.forward, new Vector3(d.x, 0f, d.z));
            if (ang > 80f && dist > 12f && o != target) continue;
            if (!CanSee(o)) continue;
            if (dist < bd) { bd = dist; best = o; }
        }
        if (best != null)
        {
            if (best != target) { target = best; reactT = Random.Range(0.35f, 0.8f) / skill; NewErr(); }
            unseenT = 0f;
        }
        else if (target != null)
        {
            unseenT += 0.25f;
            if (unseenT > 2.5f) { target = null; hasGoal = false; }
        }
    }

    bool CanSee(Soldier o)
    {
        Vector3 a = s.eye.position;
        Vector3 b = o.AimPoint();
        RaycastHit h;
        if (Physics.Linecast(a, b, out h, ~0, QueryTriggerInteraction.Ignore))
        {
            Soldier hs = h.collider.GetComponentInParent<Soldier>();
            return hs == o || hs == s;
        }
        return true;
    }

    void NewErr()
    {
        float dist = target != null ? Vector3.Distance(target.transform.position, s.transform.position) : 20f;
        aimErr = Random.insideUnitSphere * (0.25f + dist * 0.03f) / skill;
        errT = Random.Range(0.4f, 0.9f);
    }

    void LookToward(Vector3 worldPoint, float turnSpeed, float dt, out float dy, out float dp)
    {
        Vector3 d = worldPoint - s.eye.position;
        float flat = new Vector2(d.x, d.z).magnitude;
        float wantYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        float wantPitch = -Mathf.Atan2(d.y, Mathf.Max(flat, 0.01f)) * Mathf.Rad2Deg;
        dy = Mathf.DeltaAngle(s.yaw, wantYaw);
        dp = wantPitch - s.pitch;
        float turn = turnSpeed * dt;
        s.inLook = new Vector2(Mathf.Clamp(dy, -turn, turn), -Mathf.Clamp(dp, -turn, turn));
    }

    void Engage(float dt)
    {
        Vector3 aim = target.AimPoint() + aimErr;
        float dist = Vector3.Distance(aim, s.eye.position);
        float tof = dist / BBs.Speed;
        aim.y += 0.5f * BBs.Drop * tof * tof;
        float dy, dp;
        LookToward(aim, 220f * skill, dt, out dy, out dp);

        errT -= dt;
        if (errT <= 0f) NewErr();
        aimErr = Vector3.Lerp(aimErr, Vector3.zero, dt * 0.5f * skill);
        reactT -= dt;

        bool onTarget = Mathf.Abs(dy) < 4f && Mathf.Abs(dp) < 4f;
        if (reactT <= 0f && onTarget && unseenT < 0.3f && dist < 70f)
        {
            if (burstT > 0f)
            {
                burstT -= dt;
                s.inFire = true;
                if (burstT <= 0f) pauseT = Random.Range(0.25f, 0.8f);
            }
            else
            {
                pauseT -= dt;
                if (pauseT <= 0f) burstT = Random.Range(0.4f, 1.1f);
            }
        }
        s.inAds = dist > 30f;

        Vector3 to = target.transform.position - s.transform.position; to.y = 0f;
        Vector3 flat = to.sqrMagnitude > 0.01f ? to.normalized : s.transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, flat);
        strafeT -= dt;
        if (strafeT <= 0f) { strafeT = Random.Range(0.8f, 2.2f); strafe = Random.value < 0.5f ? -1f : 1f; }
        Vector3 want = right * strafe * 0.8f;
        if (dist > 34f) want += flat;
        else if (dist < 12f) want -= flat * 0.8f;
        want = Avoid(want);
        SetMoveWorld(want);
    }

    void Roam(float dt)
    {
        Vector3 pos = s.transform.position;
        if (hasGoal)
        {
            Vector3 dd = goal - pos; dd.y = 0f;
            if (dd.magnitude < 2.2f) { hasGoal = false; waitT = Random.Range(1f, 3.5f); }
        }
        if (waitT > 0f)
        {
            waitT -= dt;
            lookT += dt;
            s.inLook = new Vector2(Mathf.Sin(lookT * 0.9f) * 70f * dt, s.pitch * Mathf.Min(1f, 3f * dt));
            if (waitT <= 0f) PickGoal();
            return;
        }
        if (!hasGoal) PickGoal();
        Vector3 d = goal - pos; d.y = 0f;
        Vector3 dir = Avoid(d.normalized);
        float wantYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        float dy = Mathf.DeltaAngle(s.yaw, wantYaw);
        float turn = 200f * dt;
        s.inLook = new Vector2(Mathf.Clamp(dy, -turn, turn), s.pitch * Mathf.Min(1f, 3f * dt));
        SetMoveWorld(dir * (Mathf.Abs(dy) < 60f ? 1f : 0.3f));
    }

    void PickGoal()
    {
        Game g = Game.I;
        int alive = 0;
        foreach (var o in g.soldiers) if (o != null && o.alive) alive++;
        float hunt = 0.3f + (8 - alive) * 0.08f;
        if (Random.value < hunt)
        {
            Soldier pick = null; int n = 0;
            foreach (var o in g.soldiers)
            {
                if (o == null || o == s || !o.alive) continue;
                n++;
                if (Random.Range(0, n) == 0) pick = o;
            }
            if (pick != null)
            {
                Vector2 off = Random.insideUnitCircle * 8f;
                goal = pick.transform.position + new Vector3(off.x, 0f, off.y);
                hasGoal = true;
                return;
            }
        }
        if (World.cover.Count > 0)
        {
            for (int i = 0; i < 6; i++)
            {
                Vector3 c = World.cover[Random.Range(0, World.cover.Count)];
                if (Vector3.Distance(c, s.transform.position) < 70f || i == 5) { goal = c; hasGoal = true; return; }
            }
        }
        goal = World.center + new Vector3(Random.Range(-60f, 60f), 0f, Random.Range(-60f, 60f));
        hasGoal = true;
    }

    Vector3 Avoid(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.01f) return dir;
        Vector3 o = s.transform.position + Vector3.up * 0.9f;
        if (Free(o, dir)) return dir;
        float[] angles = { 45f, -45f, 90f, -90f, 135f, -135f };
        foreach (float a in angles)
        {
            Vector3 d2 = Quaternion.Euler(0f, a, 0f) * dir;
            if (Free(o, d2)) return d2;
        }
        return -dir;
    }

    bool Free(Vector3 o, Vector3 d)
    {
        RaycastHit h;
        if (Physics.SphereCast(o, 0.3f, d.normalized, out h, 1.8f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider is TerrainCollider) return true;
            Soldier hs = h.collider.GetComponentInParent<Soldier>();
            if (hs == s) return true;
            return false;
        }
        return true;
    }

    void SetMoveWorld(Vector3 w)
    {
        Vector3 local = s.transform.InverseTransformDirection(w);
        s.inMove = Vector2.ClampMagnitude(new Vector2(local.x, local.z), 1f);
        wantsMove = s.inMove.sqrMagnitude > 0.05f;
    }

    void Stuck(float dt)
    {
        stuckT += dt;
        if (stuckT < 2f) return;
        Vector3 p = s.transform.position;
        if (wantsMove && Vector3.Distance(p, stuckPos) < 0.7f)
        {
            PickGoal();
            s.inJump = true;
        }
        stuckT = 0f;
        stuckPos = p;
    }
}
