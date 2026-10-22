using UnityEngine;

// Tracked tank (the Ripsaw M5): ffu70 (Ben item 11) RT drives forward / LT brakes + reverses like every other vehicle (stick up /
// down still drives too), right stick / mouse aims the turret, B fires a shell (hold = repeat; X too), RB / Y missile.
// Keyboard: W / S drive, Space or B or click = shell, right-click = missile. Touch: GAS / BRAKE, FIRE, MSL. Tracked = no drift.
// &ff70old=1 = the old mapping (stick drive, RT / X fire).
public class Tank : GroundVehicle
{
    public Transform turret, barrel, muzzle;
    public float recoilDist = 0.6f;                              // ffu9: the Ripsaw M5's 30 mm gun recoils less
    public Vector3 missileOffset = new Vector3(0.9f, 1.1f, 0f);  // missile pod (turret space)
    float aimYaw, aimPitch = 4f, shellCool, missileCool, recoil;
    bool aimInit;

    public override string[] TouchSet { get { return new[] { "A", "FIRE", "MSL", usesTriggers ? "GAS" : null, usesTriggers ? "BRAKE" : null }; } }
    public override string HelpLine { get { return F70.Old ? "L-stick drive | R-stick aim turret | RT fire shell | RB/LT missile | A get out" : "L-stick steer | RT drive | LT brake/reverse | R-stick aim turret | B fire shell | RB/Y missile | A get out"; } }
    public override string HelpFor(InputKind k)
    {
        if (F70.Old) return null;
        if (k == InputKind.Gamepad) return "RT  /  LT::drive / brake + reverse|L-stick::steer (tracks turn on the spot)|R-stick::aim the turret|B::fire a shell (hold = repeat)|RB  /  Y::missile|A::get out";
        if (k == InputKind.Keyboard) return "W  /  S::drive / brake + reverse|A  /  D::steer|Mouse::aim the turret|Space  /  B  /  click::fire a shell|Right-click::missile|E::get out";
        return null;
    }
    bool fireWas; [System.NonSerialized] public int shells;   // ffu70: B / Space edge + probe counter
    public override float AimYaw { get { return turret != null ? turret.eulerAngles.y : float.NaN; } }

    public override void OnEnter()
    {
        base.OnEnter();
        if (!aimInit) { aimYaw = transform.eulerAngles.y; aimInit = true; }
    }

    public override void Drive(PIn i, float camYaw, float dt)
    {
        base.Drive(i, camYaw, dt);
        if (!aimInit) { aimYaw = transform.eulerAngles.y; aimInit = true; }
        aimYaw += i.look.x;
        aimPitch = Mathf.Clamp(aimPitch + i.look.y * 0.6f, -6f, 24f);
        shellCool -= dt;
        missileCool -= dt;
        bool fire, held;
        if (F70.Old) { fire = i.fire; held = i.fireHeld; }
        else
        {
            // ffu70: RT is the throttle now -> the shell is on B (pad) / Space or B (keys) = PIn.handbrake (held); pad X and mouse / touch
            // FIRE still shoot. Pad RT also sets i.fire, so i.fire only counts off the pad.
            InputKind ik = driver != null ? driver.inputKind : InputKind.Keyboard;
            bool bNow = i.handbrake && !fireWas; fireWas = i.handbrake;
            fire = bNow || (ik == InputKind.Gamepad ? i.auto : i.fire);
            held = i.handbrake || (ik != InputKind.Gamepad && i.fireHeld);
        }
        if ((fire || (held && shellCool < -0.3f)) && shellCool <= 0f) { FireShell(); shellCool = 0.85f; shells++; }
        if (i.alt && missileCool <= 0f) { FireMissile(); missileCool = 1.6f; }
    }

    protected override void Update()
    {
        base.Update();
        float dt = Time.deltaTime;
        if (turret == null) return;
        if (driver != null)
        {
            float local = Mathf.DeltaAngle(transform.eulerAngles.y, aimYaw);
            float cur = turret.localEulerAngles.y;
            float next = Mathf.MoveTowardsAngle(cur, local, 110f * dt);
            turret.localRotation = Quaternion.Euler(0f, next, 0f);
        }
        recoil = Mathf.MoveTowards(recoil, 0f, dt * 2.5f);
        if (barrel != null)
        {
            barrel.localRotation = Quaternion.Euler(-aimPitch, 0f, 0f);
            barrel.GetChild(0).localPosition = new Vector3(0f, 0f, 2.1f - recoil * recoilDist);
        }
    }

    void FireShell()
    {
        if (muzzle == null) return;
        Vector3 dir = muzzle.forward;
        Projectile.Spawn(false, muzzle.position + dir * 0.3f, dir * 75f + rb.velocity, this);
        FX.Muzzle(muzzle.position, dir);
        Sfx.PlayAt(Sfx.Shell, muzzle.position, 0.9f);
        rb.AddForceAtPosition(-dir * rb.mass * 2.5f, turret.position, ForceMode.Impulse);
        recoil = 1f;
        Game.Shake(muzzle.position, 0.35f);
    }

    void FireMissile()
    {
        if (muzzle == null) return;
        Vector3 dir = Quaternion.AngleAxis(-2f, turret.right) * muzzle.forward;
        Vector3 from = turret.position + turret.up * missileOffset.y + turret.right * missileOffset.x + turret.forward * missileOffset.z;
        Projectile.Spawn(true, from + dir * 0.5f, dir * 38f + rb.velocity, this);
        FX.Muzzle(from, dir);
        Sfx.PlayAt(Sfx.Missile, from, 0.8f);
    }
}
