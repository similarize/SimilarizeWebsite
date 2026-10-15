using UnityEngine;

// Chase camera. lambo9: a sprung rig like the racing games - the camera position trails the car on a damped spring
// (hard acceleration pulls the car away, braking lets it close in), the heading swings part-way towards the direction
// of travel when the car slides (you see the drift angle), FOV widens with speed, a light speed / rough-ground shake.
// Still ignores the visual spin of a spun-out car, softens jumps and never dips under the ground.
public class ChaseCam
{
    public Camera cam;
    float yaw, yawVel, height, heightVel, fov = 62f, trauma, dist = 7f, accK;
    Vector3 look, rigPos, rigVel;
    bool init;

    public ChaseCam(Camera c) { cam = c; }

    public void Reset() { init = false; }
    public void AddShake(float t) { trauma = Mathf.Min(1f, trauma + t); }

    public void Update(Kart k, float dt, float baseFov)
    {
        if (k == null || dt <= 0f) return;
        Vector3 kp = k.transform.position;
        // heading: the car's yaw, swung ~45 % towards the velocity when it slides (capped at 22 deg)
        float slip = Mathf.Atan2(k.lat, Mathf.Max(3f, Mathf.Abs(k.speed))) * Mathf.Rad2Deg;
        float want = k.yaw + Mathf.Clamp(slip * 0.45f, -22f, 22f);
        if (k.speed < -2f) want = k.yaw;     // reversing: stay behind the nose's direction, no flip
        if (!init)
        {
            yaw = want; yawVel = 0f; height = kp.y; heightVel = 0f; init = true;
            look = kp; rigVel = Vector3.zero; accK = 0f;
            rigPos = kp + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 2.7f, -7f);
        }
        yaw = Mathf.SmoothDampAngle(yaw, want, ref yawVel, k.respawning ? 0.6f : 0.2f, 400f, dt);
        height = Mathf.SmoothDamp(height, kp.y, ref heightVel, k.grounded ? 0.12f : 0.35f, Mathf.Infinity, dt);
        float sp = Mathf.Clamp01(Mathf.Abs(k.speed) / 34f);
        accK = Mathf.Lerp(accK, Mathf.Clamp(k.accLong, -12f, 12f), Mathf.Min(1f, dt * 3f));
        float wantDist = 6.7f + sp * 1.5f - Mathf.Abs(k.speed) * 0.06f + (k.boostT > 0f ? 0.9f : 0f) + accK * 0.06f;
        dist = Mathf.Lerp(dist, wantDist, Mathf.Min(1f, dt * 2.5f));
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 basePos = new Vector3(kp.x, height, kp.z);
        Vector3 target = basePos + rot * new Vector3(0f, 2.55f + sp * 0.1f, -dist);
        // spring lag on the rig itself (critically damped-ish, ~0.07 s) - snaps after a respawn / tow
        if ((target - rigPos).sqrMagnitude > 900f || k.respawning) { rigPos = target; rigVel = Vector3.zero; }
        else rigPos = Vector3.SmoothDamp(rigPos, target, ref rigVel, 0.07f, Mathf.Infinity, dt);
        Vector3 pos = rigPos;
        // keep above the ground / water
        float gy; Vector3 n;
        Track.Ground(pos, out gy, out n);
        float floor = Mathf.Max(gy, Track.SurfaceFloor) + 1.0f;
        if (pos.y < floor) { pos.y = floor; rigPos.y = floor; }
        Vector3 lookAt = basePos + Vector3.up * 1.15f + rot * Vector3.forward * 3.2f;
        look = Vector3.Lerp(look, lookAt, Mathf.Min(1f, dt * 14f));
        trauma = Mathf.MoveTowards(trauma, 0f, dt * 1.8f);
        Vector3 shake = trauma > 0f ? Random.insideUnitSphere * trauma * trauma * 0.35f : Vector3.zero;
        // high-speed / rough-ground buzz (smooth noise, not random jitter)
        float t = Time.time;
        float buzz = (k.grounded ? 1f : 0.2f) * (0.012f * sp * sp + (k.onRoad ? 0f : 0.03f * sp));
        shake += new Vector3(Mathf.PerlinNoise(t * 9f, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t * 11f) - 0.5f, 0f) * buzz * 2f;
        cam.transform.position = pos + shake;
        Quaternion lr = Quaternion.LookRotation(look - pos);
        float rollC = Mathf.Clamp(-k.yawRate * Mathf.Abs(k.speed) * 0.05f, -2.2f, 2.2f) * (k.grounded ? 1f : 0f);   // slight lean into the turn
        cam.transform.rotation = lr * Quaternion.Euler(0f, 0f, rollC);
        float wantFov = baseFov + sp * sp * 10f + (k.boostT > 0f ? 6f : 0f);
        fov = Mathf.Lerp(fov, wantFov, Mathf.Min(1f, dt * 3f));
        cam.fieldOfView = fov;
    }
}
