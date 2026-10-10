using UnityEngine;

// ffu21: per-part damage for the story mechs (10 / 100 / 1000 / trillion-story, all scaled by height).
// Parts: 0 left leg, 1 right leg, 2 left arm, 3 right arm (the arm cannon + missile pod), 4 head (the cockpit).
// - Each hit damages the part nearest the hit point (on top of the mech's main HP; main HP 0 = the old full knock-out).
// - A leg blown off: the mech loses its footing and topples toward that side in a slow, heavy fall, hits the ground with a
//   dust shockwave, a far-reaching camera shake and a crush blast; the pilot ejects as before. It lies there smoking,
//   the missing parts re-assemble, and it stands back up (10 s shield).
// - Right arm blown off: the cannon + missiles are offline until it grows back. Left arm: just gone for a while.
// - Head blown off: the pilot's view turns to static (Hud) until it grows back.
// - Lost limbs fly off as physics copies of the limb mesh. Everything grows back after ~24-32 s.
// - Authority: the mech's authority device (pilot / host) decides limb loss and sends D|m|hash|part; others mirror it.
public partial class StoryMech
{
    public const int LegL = 0, LegR = 1, ArmL = 2, ArmR = 3, Head = 4;
    float[] partHp, partRegen, partGrow;
    bool[] partLost;
    Renderer[][] partRends;
    int toppleState;            // 0 none, 1 falling, 2 lying, 3 regrowing, 4 standing up
    float toppleT, toppleDur, downT, fallAng;
    Vector3 fallDir, fallAxis, fallPivot, fallBasePos;
    Quaternion fallBaseRot;
    float impactBounce;

    public bool PartsBusy { get { return toppleState != 0; } }
    public bool HeadLost { get { return partLost != null && partLost[Head]; } }
    public bool Toppled { get { return toppleState != 0; } }
    public bool ArmOk(int i) { return partLost == null || !partLost[i]; }
    public int PartRegenLeft(int i) { return partRegen == null ? 0 : Mathf.CeilToInt(Mathf.Max(0f, partRegen[i])); }
    public string PartsLine
    {
        get
        {
            switch (toppleState)
            {
                case 1: return "TIMBER! it's falling over";
                case 2: return "down - lost a leg, back on its feet in " + Mathf.CeilToInt(downT + 4f) + " s";
                case 3: return "re-assembling...";
                default: return "standing back up";
            }
        }
    }
    static readonly string[] PartName = { "left leg", "right leg", "left arm", "cannon arm", "head" };

    Transform PartT(int i)
    {
        switch (i) { case LegL: return legL; case LegR: return legR; case ArmL: return armL; case ArmR: return armR; default: return head; }
    }

    void InitParts()
    {
        if (partHp != null) return;
        partHp = new float[5]; partRegen = new float[5]; partGrow = new float[5]; partLost = new bool[5];
        partRends = new Renderer[5][];
        for (int i = 0; i < 5; i++)
        {
            partHp[i] = PartMax(i); partGrow[i] = -1f;
            Transform t = PartT(i);
            partRends[i] = t != null ? t.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
        }
    }
    float PartMax(int i) { return maxHp * (i <= LegR ? 0.28f : i == Head ? 0.2f : 0.25f); }

    // the part a hit lands on: nearest limb segment (or the torso = none)
    int NearestPart(Vector3 at)
    {
        float H = height;
        int best = -1;
        float bd = (at - torso.TransformPoint(new Vector3(0f, 0.2f * H, 0f))).magnitude - H * 0.12f;   // torso bias
        for (int i = 0; i < 5; i++)
        {
            if (partLost[i]) continue;
            Transform t = PartT(i);
            if (t == null) continue;
            float d;
            if (i == Head) d = (at - t.TransformPoint(new Vector3(0f, 0.05f * H, 0f))).magnitude;
            else
            {
                float len = i <= LegR ? 0.42f * H : 0.3f * H;
                Vector3 a = t.position, b = t.position - t.up * len;
                Vector3 ab = b - a;
                float u = Mathf.Clamp01(Vector3.Dot(at - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                d = (at - (a + ab * u)).magnitude;
            }
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    void PartHit(float d, Vector3 at, Vehicle by)
    {
        InitParts();
        if (toppleState == 1) return;
        int i = NearestPart(at);
        if (i < 0) return;
        partHp[i] -= d * 1.3f;
        if (partHp[i] <= 0f) LoseLimb(i, at, by, false);
    }

    public void NetLoseLimb(int i)
    {
        if (Authority || i < 0 || i > 4) return;
        InitParts();
        if (!partLost[i]) LoseLimb(i, PartT(i) != null ? PartT(i).position : transform.position, null, true);
    }

    void LoseLimb(int i, Vector3 at, Vehicle by, bool fromNet)
    {
        InitParts();
        if (partLost[i] || wrecked) return;
        partLost[i] = true; partHp[i] = 0f;
        partRegen[i] = Random.Range(24f, 32f) * Destruct.RegenScale;
        partGrow[i] = -1f;
        Transform t = PartT(i);
        float H = height;
        Vector3 joint = t != null ? t.position : at;
        // a physics copy of the limb flies off, the real one hides
        if (t != null) ThrowLimbCopy(t, at);
        foreach (var r in partRends[i]) if (r != null) r.enabled = false;
        float sc = Mathf.Clamp(H / 14f, 1f, 6f);
        FX.Boom(joint, sc);
        FX.Boom(joint + Random.insideUnitSphere * H * 0.04f, sc * 0.7f);
        FX.Sparkle(joint, new Color(1f, 0.8f, 0.4f), 24);
        FX.Cloud(joint, H * 0.06f + 1f, 10, new Color(0.3f, 0.3f, 0.3f, 0.75f));
        Sfx.PlayAt(Sfx.Pick(Sfx.Crash) ?? Sfx.Boom, joint, 1f, 160f + H * 2f, Mathf.Lerp(0.9f, 0.45f, band / 3f));
        Sfx.PlayAt(Sfx.Boom, joint, 0.9f, 160f + H * 2f, 0.7f);
        Destruct.BigShake(joint, 0.5f + band * 0.15f, 60f + H * 1.5f);
        if (driver != null && driver.human) driver.Toast("Your mech's " + PartName[i] + " got blown off!" + (i == ArmR ? " Weapons offline until it grows back." : i == Head ? " Cockpit cameras down!" : ""), 3f);
        if (by != null && by.driver != null && by.driver != driver) by.driver.Toast("You blew off " + Title + "'s " + PartName[i] + "!", 2.5f);
        Debug.Log("Mech part lost: " + Title + " " + PartName[i]);
        if (!fromNet && Authority) Destruct.SendNet("m|" + Title.GetHashCode() + "|" + i);
        if (i <= LegR) StartTopple(i == LegL ? -1f : 1f);
    }

    void ThrowLimbCopy(Transform limb, Vector3 hitFrom)
    {
        GameObject copy = Instantiate(limb.gameObject, limb.position, limb.rotation);
        copy.name = "LostLimb";
        copy.transform.localScale = limb.lossyScale;
        foreach (var c in copy.GetComponentsInChildren<Collider>(true)) Destroy(c);
        foreach (var l in copy.GetComponentsInChildren<Light>(true)) Destroy(l);
        foreach (var tm in copy.GetComponentsInChildren<TextMesh>(true)) Destroy(tm.gameObject);
        var rs = copy.GetComponentsInChildren<Renderer>(true);
        Bounds lb = new Bounds(copy.transform.position, Vector3.one);
        bool first = true;
        foreach (var r in rs) { r.enabled = true; if (first) { lb = r.bounds; first = false; } else lb.Encapsulate(r.bounds); }
        Mats.SetLayer(copy, Destruct.DebrisLayer);
        var box = copy.AddComponent<BoxCollider>();
        box.center = copy.transform.InverseTransformPoint(lb.center);
        Vector3 ls = copy.transform.lossyScale;
        box.size = new Vector3(lb.size.x / Mathf.Max(1e-3f, ls.x), lb.size.y / Mathf.Max(1e-3f, ls.y), lb.size.z / Mathf.Max(1e-3f, ls.z)) * 0.8f;
        var rb2 = copy.AddComponent<Rigidbody>();
        rb2.mass = 200f * height;
        Vector3 away = limb.position - transform.position; away.y = 0f;
        away = away.sqrMagnitude > 0.01f ? away.normalized : transform.right;
        rb2.velocity = away * height * 0.35f + Vector3.up * height * 0.3f + kinVel * 0.5f;
        rb2.angularVelocity = Random.insideUnitSphere * 2.5f;
        copy.AddComponent<ShrinkAway>().life = 9f;
    }

    // ---------------- toppling ----------------
    void StartTopple(float side)
    {
        if (toppleState != 0 || wrecked) return;
        float H = height;
        // the pilot ejects (same as a knock-out: pops out unhurt beside the mech, on the side it is NOT falling to)
        Frog pilot = driver;
        if (pilot != null && !pilot.netPuppet)
        {
            pilot.ExitVehicle();
            Vector3 p = transform.position - transform.right * side * (H * 0.3f + 3f);
            p.y = Ranch.GY(p.x, p.z) + 0.5f;
            pilot.Teleport(p);
            pilot.Knock(Vector3.up * 9f - transform.right * side * 4f);
            pilot.Toast("Your mech lost its footing and is going DOWN! You bailed out fine - it gets back up in about 30 s", 4.5f);
        }
        toppleState = 1; toppleT = 0f;
        toppleDur = 1.7f + H * 0.022f;           // 10-story ~2 s ... trillion-story ~5.2 s
        Vector3 f = transform.right * side * 0.85f + transform.forward * 0.3f; f.y = 0f;
        fallDir = f.normalized;
        fallAxis = Vector3.Cross(Vector3.up, fallDir).normalized;
        Vector3 bp = transform.position; bp.y = Ranch.GY(bp.x, bp.z);
        fallBasePos = bp;
        fallBaseRot = Quaternion.Euler(0f, yaw, 0f);
        fallPivot = bp + fallDir * H * 0.1f;
        fallAng = 0f; impactBounce = 0f;
        speed = 0f; vy = 0f; flameK = 0f; aimK = 0f; grounded = true; Altitude = 0f;
        Sfx.PlayAt(Sfx.Pick(Sfx.Servo) ?? Sfx.Clank, transform.position, 1f, 150f + H * 2f, 0.5f);
        Debug.Log("Mech topple: " + Title + " falling " + toppleDur.ToString("0.0") + " s");
    }

    void PoseFall(float ang)
    {
        Quaternion q = Quaternion.AngleAxis(ang, fallAxis);
        Vector3 p = fallPivot + q * (fallBasePos - fallPivot);
        Quaternion r = q * fallBaseRot;
        transform.position = p; transform.rotation = r;
        if (rb != null) { rb.position = p; rb.rotation = r; }
    }

    // returns true while the mech is toppling / down / getting up (normal walking + weapons are skipped)
    bool TickParts(float dt)
    {
        if (partHp == null) return false;
        float H = height;
        // limb regrowth when not toppled (arms, head; legs regrow while lying down)
        if (toppleState == 0)
        {
            for (int i = ArmL; i <= Head; i++)
            {
                if (partLost[i] && partGrow[i] < 0f) { partRegen[i] -= dt; if (partRegen[i] <= 0f) StartGrow(i); }
                TickGrow(i, dt);
            }
            return false;
        }
        switch (toppleState)
        {
            case 1:
                {
                    toppleT += dt;
                    float u = Mathf.Clamp01(toppleT / toppleDur);
                    // teeters first (a small wobble the "wrong" way), then goes over faster and faster
                    float wob = u < 0.18f ? -Mathf.Sin(u / 0.18f * Mathf.PI) * 3f : 0f;
                    fallAng = 87f * Mathf.Pow(u, 2.3f) + wob;
                    PoseFall(fallAng);
                    // arms flail up, the remaining leg buckles
                    if (armL != null) armL.localRotation = Quaternion.Slerp(armL.localRotation, Quaternion.Euler(-150f, 0f, -30f), dt * 2f);
                    if (armR != null) armR.localRotation = Quaternion.Slerp(armR.localRotation, Quaternion.Euler(-130f, 0f, 30f), dt * 2f);
                    if (legL != null && !partLost[LegL]) legL.localRotation = Quaternion.Slerp(legL.localRotation, Quaternion.Euler(-25f, 0f, 0f), dt * 2f);
                    if (legR != null && !partLost[LegR]) legR.localRotation = Quaternion.Slerp(legR.localRotation, Quaternion.Euler(-25f, 0f, 0f), dt * 2f);
                    if (Random.value < dt * 8f) FX.Smoke(transform.position + Vector3.up * H * 0.3f, H * 0.05f + 1f, new Color(0.25f, 0.25f, 0.25f, 0.6f));
                    if (u >= 1f) { Impact(); toppleState = 2; downT = Random.Range(18f, 24f) * Destruct.RegenScale; impactBounce = 1f; }
                    return true;
                }
            case 2:
                {
                    // a small bounce right after the impact, then it lies still, smoking
                    impactBounce = Mathf.Max(0f, impactBounce - dt * 1.8f);
                    PoseFall(87f - Mathf.Sin(impactBounce * Mathf.PI) * 4f);
                    downT -= dt;
                    if (Random.value < dt * 5f) FX.Smoke(transform.TransformPoint(new Vector3(Random.Range(-0.1f, 0.1f) * H, H * 0.3f, 0f)), H * 0.06f + 1.5f, new Color(0.22f, 0.22f, 0.22f, 0.6f));
                    if (Random.value < dt * 6f) FX.Flame(transform.TransformPoint(new Vector3(0f, H * 0.38f, 0f)), Vector3.up);
                    if (downT <= 0f) { toppleState = 3; toppleT = 0f; for (int i = 0; i < 5; i++) if (partLost[i]) StartGrow(i); }
                    return true;
                }
            case 3:
                {
                    bool any = false;
                    for (int i = 0; i < 5; i++) { TickGrow(i, dt); if (partLost[i] || partGrow[i] >= 0f) any = true; }
                    PoseFall(87f);
                    if (!any) { toppleState = 4; toppleT = 0f; Sfx.PlayAt(Sfx.Pick(Sfx.Servo) ?? Sfx.Clank, transform.position, 1f, 150f + H * 2f, 0.55f); }
                    return true;
                }
            default:
                {
                    // stand back up: push up on the arms, swing upright, settle with a thud + dust
                    toppleT += dt;
                    float upDur = 2.2f + H * 0.012f;
                    float u = Mathf.Clamp01(toppleT / upDur);
                    float e = u * u * (3f - 2f * u);
                    PoseFall(87f * (1f - e));
                    if (armL != null) armL.localRotation = Quaternion.Slerp(armL.localRotation, Quaternion.identity, dt * 3f);
                    if (armR != null) armR.localRotation = Quaternion.Slerp(armR.localRotation, Quaternion.identity, dt * 3f);
                    if (legL != null) legL.localRotation = Quaternion.Slerp(legL.localRotation, Quaternion.identity, dt * 3f);
                    if (legR != null) legR.localRotation = Quaternion.Slerp(legR.localRotation, Quaternion.identity, dt * 3f);
                    if (Random.value < dt * 10f) FX.Dust(fallBasePos + Random.insideUnitSphere * H * 0.25f, 1f);
                    if (u >= 1f)
                    {
                        toppleState = 0;
                        transform.position = fallBasePos; transform.rotation = fallBaseRot;
                        if (rb != null) { rb.position = fallBasePos; rb.rotation = fallBaseRot; }
                        hp = Mathf.Max(hp, maxHp * 0.75f);
                        for (int i = 0; i < 5; i++) partHp[i] = PartMax(i);
                        shieldT = 10f;
                        Game.Shake(fallBasePos, 0.5f + band * 0.2f);
                        Sfx.PlayAt(Sfx.Land ?? Sfx.Thud, fallBasePos, 0.9f, 140f + H * 2f, 0.5f);
                        FX.Ring(fallBasePos, H * 0.4f, 24 + band * 8, H * 0.25f, new Color(0.62f, 0.54f, 0.42f, 0.6f));
                        SendNet(true);
                        Debug.Log("Mech back up: " + Title);
                    }
                    return true;
                }
        }
    }

    void Impact()
    {
        float H = height;
        Vector3 p = fallPivot + fallDir * H * 0.62f;
        p.y = Ranch.GY(p.x, p.z) + 0.5f;
        float pw = Mathf.Clamp(H / 16f, 1f, 6f);
        // dust shockwave rolling out along the ground + dust cloud along the body
        FX.Ring(p, H * 0.7f, Destruct.Low ? 40 : 70 + band * 20, H * 0.45f + 8f, new Color(0.66f, 0.58f, 0.45f, 0.7f));
        FX.Ring(p, H * 0.35f, Destruct.Low ? 20 : 40, H * 0.25f + 5f, new Color(0.5f, 0.45f, 0.38f, 0.75f));
        for (int i = 0; i < 6; i++) FX.Cloud(fallPivot + fallDir * H * (0.15f + i * 0.14f) + Vector3.up * 1f, H * 0.08f + 1.5f, Destruct.Low ? 3 : 6, new Color(0.62f, 0.55f, 0.44f, 0.75f));
        Destruct.BigShake(p, 1.1f, 80f + H * 3f);
        Game.Shake(p, 1.2f);
        Sfx.PlayAt(Sfx.Boom, p, 1f, 250f + H * 3f, 0.35f);
        Sfx.PlayAt(Sfx.Land ?? Sfx.Thud, p, 1f, 250f + H * 3f, 0.3f);
        Sfx.PlayAt(Sfx.Pick(Sfx.Crash) ?? Sfx.Clank, p, 0.9f, 200f + H * 2f, 0.5f);
        // knocks froggies / props about (no damage from Boom), and crushes what it lands on
        Boom.At(p, H * 0.35f + 4f, pw, 0f, this);
        Destruct.Stomp(p, H * 0.3f + 3f, 60f, this);
        Destruct.Stomp(fallPivot + fallDir * H * 0.3f, H * 0.2f + 2f, 60f, this);
        Debug.Log("Mech impact: " + Title + " at " + p.ToString("0"));
    }

    void StartGrow(int i)
    {
        if (!partLost[i]) return;
        partGrow[i] = 0f;
        Transform t = PartT(i);
        if (t == null) { partLost[i] = false; partGrow[i] = -1f; return; }
        t.localScale = Vector3.one * 0.01f;
        foreach (var r in partRends[i]) if (r != null) r.enabled = true;
        FX.Sparkle(t.position, new Color(0.55f, 0.95f, 1f), 20);
        Sfx.PlayAt(Sfx.Confirm ?? Sfx.Pickup, t.position, 0.7f, 120f + height, 0.8f);
    }

    void TickGrow(int i, float dt)
    {
        if (partGrow[i] < 0f) return;
        Transform t = PartT(i);
        partGrow[i] += dt / 1.8f;
        float k = Mathf.Clamp01(partGrow[i]);
        float e = 1f + 2.4f * Mathf.Pow(k - 1f, 3f) + 1.4f * Mathf.Pow(k - 1f, 2f);
        if (t != null)
        {
            t.localScale = Vector3.one * Mathf.Max(0.01f, e);
            if (Random.value < dt * 16f) FX.Sparkle(t.position - t.up * height * 0.15f * Random.value, new Color(0.6f, 0.95f, 1f), 3);
        }
        if (k >= 1f)
        {
            if (t != null) t.localScale = Vector3.one;
            partGrow[i] = -1f; partLost[i] = false; partHp[i] = PartMax(i);
            if (driver != null && driver.human) driver.Toast("Your mech's " + PartName[i] + " grew back!", 2f);
        }
    }

    void ResetParts()
    {
        if (partHp == null) return;
        toppleState = 0;
        for (int i = 0; i < 5; i++)
        {
            partLost[i] = false; partGrow[i] = -1f; partHp[i] = PartMax(i);
            Transform t = PartT(i);
            if (t != null) { t.localScale = Vector3.one; t.localRotation = Quaternion.identity; }
        }
    }

    // demo: hit a part directly (probe shots)
    public void DemoBlowPart(int i) { InitParts(); atSpawn = false; LoseLimb(i, PartT(i) != null ? PartT(i).position : transform.position, null, false); }
    public Vector3 PartPoint(int i)
    {
        Transform t = PartT(i);
        if (t == null) return transform.position;
        return i == Head ? t.position : t.position - t.up * height * (i <= LegR ? 0.25f : 0.15f);
    }
}

// a thrown-off limb / chunk shrinks away and is removed
public class ShrinkAway : MonoBehaviour
{
    public float life = 8f;
    float t; Vector3 s0;
    void Start() { s0 = transform.localScale; }
    void Update()
    {
        t += Time.deltaTime;
        if (t > life - 1f) transform.localScale = s0 * Mathf.Max(0.01f, life - t);
        if (t >= life) Destroy(gameObject);
    }
}
