using UnityEngine;

// Tracked tank: left stick drives, right stick / mouse aims the turret, RT / click fires a shell,
// RB / LT / right-click launches a missile.
public class Tank : GroundVehicle
{
    public Transform turret, barrel, muzzle;
    float aimYaw, aimPitch = 4f, shellCool, missileCool, recoil;
    bool aimInit;

    public override string HelpLine { get { return "L-stick drive | R-stick aim turret | RT fire shell | RB/LT missile | A get out"; } }
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
        if ((i.fire || (i.fireHeld && shellCool < -0.3f)) && shellCool <= 0f) { FireShell(); shellCool = 0.85f; }
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
            barrel.GetChild(0).localPosition = new Vector3(0f, 0f, 2.1f - recoil * 0.6f);
        }
    }

    void FireShell()
    {
        if (muzzle == null) return;
        Vector3 dir = muzzle.forward;
        Projectile.Spawn(false, muzzle.position + dir * 0.3f, dir * 75f + rb.velocity, this);
        FX.Muzzle(muzzle.position, dir);
        rb.AddForceAtPosition(-dir * rb.mass * 2.5f, turret.position, ForceMode.Impulse);
        recoil = 1f;
        Game.Shake(muzzle.position, 0.35f);
    }

    void FireMissile()
    {
        if (muzzle == null) return;
        Vector3 dir = Quaternion.AngleAxis(-2f, turret.right) * muzzle.forward;
        Vector3 from = turret.position + turret.up * 1.1f + turret.right * 0.9f;
        Projectile.Spawn(true, from + dir * 0.5f, dir * 38f + rb.velocity, this);
        FX.Muzzle(from, dir);
    }
}
