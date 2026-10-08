using UnityEngine;

// Third-person orbit camera for one froggy (split-screen) with zoom, collision, auto-follow in vehicles and shake.
// Feel rules (v2): the player's yaw / pitch / zoom are kept until they change or reset them; input is
// smoothed (no jumps when a finger lands or lifts); auto-follow behind a moving vehicle only starts after
// 4 s without camera input (never while a look finger / stick is held) and eases in over ~2 s;
// wall collision pulls in quickly but lets the camera back out slowly so it never pops.
public class CamRig
{
    public Camera cam;
    public float yaw, pitch = 16f, zoomMul = 1f;
    public float extraDistance;          // worlds / big mechs push the camera further out
    float yawT, pitchT = 16f, followW, curDist = -1f, yawVel, pitchVel;
    Vector3 focus;
    float manualT = 10f, trauma;
    bool init;

    public CamRig(Camera c) { cam = c; }

    public void AddShake(float t) { trauma = Mathf.Min(1.2f, trauma + t); }

    public void Snap() { init = false; }

    public void SetYaw(float y) { yaw = yawT = y; }

    public void ResetView(float behindYaw)
    {
        yawT = behindYaw; pitchT = 16f; zoomMul = 1f; manualT = 10f;
    }

    public void Update(Frog f, PIn i, float dt)
    {
        if (f == null) return;
        Vehicle v = f.vehicle;
        bool tank = v != null && !float.IsNaN(v.AimYaw);
        if (!init) { yawT = yaw; pitchT = pitch; }
        if (i.camReset) ResetView(v != null ? v.transform.eulerAngles.y : f.transform.eulerAngles.y);
        if (!tank) yawT += i.look.x;
        pitchT = Mathf.Clamp(pitchT - i.look.y * (tank ? 0.3f : 1f), -5f, 70f);
        bool touching = i.lookHeld || i.look.sqrMagnitude > 0.0001f;
        if (touching) { manualT = 0f; followW = 0f; } else manualT += dt;
        zoomMul = Mathf.Clamp(zoomMul * (1f + i.zoom * 1.4f * dt), 0.45f, 3.2f);

        float dist = 8f, height = 1.6f;
        Vector3 target = f.FocusPoint;
        if (v != null)
        {
            dist = v.camDistance;
            height = v.camHeight;
            if (tank) yawT = Mathf.LerpAngle(yawT, v.AimYaw, Mathf.Min(1f, dt * 8f));
            else if (manualT > 4f && v.Speed > 3f)
            {
                // gentle auto-follow: weight ramps up over ~2 s, and the turn rate itself is slow
                followW = Mathf.MoveTowards(followW, 1f, dt * 0.5f);
                Vector3 vel = v.Velocity; vel.y = 0f;
                float heading = v.flyer ? v.transform.eulerAngles.y : Mathf.Atan2(vel.x, vel.z) * Mathf.Rad2Deg;
                if (!v.flyer && v.ForwardSpeed < -1f) heading = v.transform.eulerAngles.y;
                yawT = Mathf.LerpAngle(yawT, heading, Mathf.Min(1f, dt * 1.2f * followW * followW));
            }
            else followW = Mathf.MoveTowards(followW, 0f, dt);
        }
        dist = (dist + extraDistance) * zoomMul;

        // smooth the player's own input so a finger landing / lifting never jumps the view
        yaw = Mathf.SmoothDampAngle(yaw, yawT, ref yawVel, tank ? 0.05f : 0.09f, Mathf.Infinity, dt);
        pitch = Mathf.SmoothDamp(pitch, pitchT, ref pitchVel, 0.09f, Mathf.Infinity, dt);

        if (!init) { focus = target; init = true; yaw = yawT; pitch = pitchT; curDist = dist; }
        focus = Vector3.Lerp(focus, target, Mathf.Min(1f, dt * 10f));
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = focus + Vector3.up * height * 0.5f;
        Vector3 back = rot * Vector3.back;
        float allowed = dist;
        RaycastHit hit;
        if (Physics.SphereCast(pivot, 0.35f, back, out hit, dist, (1 << 0) | (1 << 12) | (1 << 13), QueryTriggerInteraction.Ignore))
            allowed = Mathf.Max(1.2f, hit.distance - 0.1f);
        // pull in fast (no clipping), ease back out slowly (no popping)
        if (curDist < 0f) curDist = allowed;
        curDist = allowed < curDist ? Mathf.Lerp(curDist, allowed, Mathf.Min(1f, dt * 18f)) : Mathf.MoveTowards(curDist, allowed, dt * Mathf.Max(2f, dist * 0.35f));
        Vector3 want = pivot + back * curDist;
        float gy = Worlds.FloorUnder(want) + 0.5f;
        if (want.y < gy) want.y = gy;

        trauma = Mathf.MoveTowards(trauma, 0f, dt * 1.6f);
        Vector3 shake = trauma > 0f ? Random.insideUnitSphere * trauma * trauma * 0.6f : Vector3.zero;
        cam.transform.position = want + shake;
        cam.transform.rotation = Quaternion.LookRotation((pivot - want).normalized + shake * 0.02f);
    }
}
