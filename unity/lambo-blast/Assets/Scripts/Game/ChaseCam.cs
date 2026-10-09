using UnityEngine;

// Smooth chase camera: follows the car's heading with a critically damped yaw (no snapping), ignores
// the visual spin of a spun-out car, softens jumps, never dips under the ground, widens the FOV with speed.
public class ChaseCam
{
    public Camera cam;
    float yaw, yawVel, height, heightVel, fov = 62f, trauma, dist = 7f;
    Vector3 look;
    bool init;

    public ChaseCam(Camera c) { cam = c; }

    public void Reset() { init = false; }
    public void AddShake(float t) { trauma = Mathf.Min(1f, trauma + t); }

    public void Update(Kart k, float dt, float baseFov)
    {
        if (k == null || dt <= 0f) return;
        Vector3 kp = k.transform.position;
        // follow the facing, eased towards the drift direction a little (a drifting car slides sideways)
        float want = k.yaw + (k.drifting ? Mathf.Clamp(k.lat * 1.2f, -14f, 14f) : 0f);
        if (k.speed < -2f) want = k.yaw;     // reversing: stay behind the nose's direction, no flip
        if (!init)
        {
            yaw = want; yawVel = 0f; height = kp.y; heightVel = 0f; init = true;
            look = kp;
        }
        yaw = Mathf.SmoothDampAngle(yaw, want, ref yawVel, k.respawning ? 0.6f : 0.22f, 400f, dt);
        height = Mathf.SmoothDamp(height, kp.y, ref heightVel, k.grounded ? 0.12f : 0.35f, Mathf.Infinity, dt);
        float sp = Mathf.Clamp01(Mathf.Abs(k.speed) / 34f);
        float wantDist = 6.8f + sp * 1.6f + (k.boostT > 0f ? 0.8f : 0f);
        dist = Mathf.Lerp(dist, wantDist, Mathf.Min(1f, dt * 2f));
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 basePos = new Vector3(kp.x, height, kp.z);
        Vector3 pos = basePos + rot * new Vector3(0f, 2.7f, -dist);
        // keep above the ground / water
        float gy; Vector3 n;
        Track.Ground(pos, out gy, out n);
        float floor = Mathf.Max(gy, Track.WaterY) + 1.0f;
        if (pos.y < floor) pos.y = floor;
        Vector3 lookAt = basePos + Vector3.up * 1.2f + rot * Vector3.forward * 3f;
        look = Vector3.Lerp(look, lookAt, Mathf.Min(1f, dt * 14f));
        trauma = Mathf.MoveTowards(trauma, 0f, dt * 1.8f);
        Vector3 shake = trauma > 0f ? Random.insideUnitSphere * trauma * trauma * 0.35f : Vector3.zero;
        cam.transform.position = pos + shake;
        cam.transform.rotation = Quaternion.LookRotation(look - pos);
        float wantFov = baseFov + sp * 6f + (k.boostT > 0f ? 6f : 0f);
        fov = Mathf.Lerp(fov, wantFov, Mathf.Min(1f, dt * 3f));
        cam.fieldOfView = fov;
    }
}
