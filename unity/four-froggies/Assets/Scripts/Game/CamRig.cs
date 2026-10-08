using UnityEngine;

// Third-person orbit camera for one froggy (split-screen) with zoom, collision, auto-follow in vehicles and shake.
public class CamRig
{
    public Camera cam;
    public float yaw, pitch = 16f, zoomMul = 1f;
    Vector3 focus;
    float manualT = 10f, trauma;
    bool init;

    public CamRig(Camera c) { cam = c; }

    public void AddShake(float t) { trauma = Mathf.Min(1.2f, trauma + t); }

    public void Snap() { init = false; }

    public void Update(Frog f, PIn i, float dt)
    {
        if (f == null) return;
        Vehicle v = f.vehicle;
        bool tank = v != null && !float.IsNaN(v.AimYaw);
        if (!tank) yaw += i.look.x;
        pitch = Mathf.Clamp(pitch - i.look.y * (tank ? 0.3f : 1f), -5f, 70f);
        if (i.look.sqrMagnitude > 0.01f) manualT = 0f; else manualT += dt;
        zoomMul = Mathf.Clamp(zoomMul * (1f + i.zoom * 1.4f * dt), 0.45f, 3.2f);

        float dist = 8f, height = 1.6f;
        Vector3 target = f.FocusPoint;
        if (v != null)
        {
            dist = v.camDistance;
            height = v.camHeight;
            if (tank) yaw = Mathf.LerpAngle(yaw, v.AimYaw, Mathf.Min(1f, dt * 8f));
            else if (manualT > 1.2f && v.Speed > 3f)
            {
                Vector3 vel = v.Velocity; vel.y = 0f;
                float heading = v.flyer ? v.transform.eulerAngles.y : Mathf.Atan2(vel.x, vel.z) * Mathf.Rad2Deg;
                if (!v.flyer && v.ForwardSpeed < -1f) heading = v.transform.eulerAngles.y;
                yaw = Mathf.LerpAngle(yaw, heading, Mathf.Min(1f, dt * 1.6f));
            }
        }
        dist *= zoomMul;

        if (!init) { focus = target; init = true; }
        focus = Vector3.Lerp(focus, target, Mathf.Min(1f, dt * 10f));
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = focus + Vector3.up * height * 0.5f;
        Vector3 want = pivot + rot * new Vector3(0f, 0f, -dist);
        RaycastHit hit;
        Vector3 d = want - pivot;
        if (Physics.SphereCast(pivot, 0.35f, d.normalized, out hit, d.magnitude, 1 << 0, QueryTriggerInteraction.Ignore))
            want = pivot + d.normalized * Mathf.Max(1.2f, hit.distance - 0.1f);
        float gy = Ranch.GY(want.x, want.z) + 0.5f;
        if (want.y < gy) want.y = gy;

        trauma = Mathf.MoveTowards(trauma, 0f, dt * 1.6f);
        Vector3 shake = trauma > 0f ? Random.insideUnitSphere * trauma * trauma * 0.6f : Vector3.zero;
        cam.transform.position = want + shake;
        cam.transform.rotation = Quaternion.LookRotation((pivot - want).normalized + shake * 0.02f);
    }
}
