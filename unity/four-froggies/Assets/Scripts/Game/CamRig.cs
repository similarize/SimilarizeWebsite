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
    public float extraDistance;
    public float minPitch = -5f, maxPitch = 70f;          // worlds / big mechs push the camera further out
    float yawT, pitchT = 16f, followW, curDist = -1f, yawVel, pitchVel;
    Vector3 focus;
    float manualT = 10f, trauma;
    bool init;

    public CamRig(Camera c) { cam = c; }

    public void AddShake(float t) { trauma = Mathf.Min(1.2f, trauma + t); }
    // ffu28: ground quake from giant footfalls - a short low-frequency vertical bounce + a little pitch / roll, sized by
    // camera distance so it reads the same for a 3 m suit and a 160 m mech (positional jitter alone vanishes at 190 m)
    float quake, quakeSeed;
    public float QuakeK { get { return quake; } }
    public void AddQuake(float q) { quake = Mathf.Min(1f, Mathf.Max(quake, q) + q * 0.25f); quakeSeed += 13.7f; }
    public Vector3 QuakePos(float camDist)
    {
        if (quake <= 0.001f) return Vector3.zero;
        float t = Time.time * 17f + quakeSeed, q2 = quake * quake;
        float amp = q2 * (0.12f + camDist * 0.012f);
        return new Vector3((Mathf.PerlinNoise(t, 1.3f) - 0.5f) * amp * 0.6f, (Mathf.PerlinNoise(2.7f, t) - 0.5f) * amp * 2f, 0f);
    }
    public Quaternion QuakeRot()
    {
        if (quake <= 0.001f) return Quaternion.identity;
        float t = Time.time * 15f + quakeSeed, q2 = quake * quake;
        return Quaternion.Euler((Mathf.PerlinNoise(t, 5.1f) - 0.5f) * q2 * 3.2f, 0f, (Mathf.PerlinNoise(7.3f, t) - 0.5f) * q2 * 2.4f);
    }
    public void TickQuake(float dt) { quake = Mathf.MoveTowards(quake, 0f, dt * (1.4f + quake * 1.6f)); }
    // ffu15: recoil kick (degrees, view pitches up then settles) + over-the-shoulder aim state
    float kick, baseFov = -1f;
    public void Kick(float deg) { kick = Mathf.Min(kick + deg, 9f); }

    public void Snap() { init = false; }

    public void SetYaw(float y) { yaw = yawT = y; }
    public void SetPitch(float p) { pitch = pitchT = p; }

    public void ResetView(float behindYaw)
    {
        yawT = behindYaw; pitchT = Mathf.Clamp(16f, minPitch, maxPitch); zoomMul = 1f; manualT = 10f;
    }

    public void Update(Frog f, PIn i, float dt)
    {
        if (f == null) return;
        Vehicle v = f.vehicle != null ? f.vehicle : f.passengerOf;
        bool tank = v != null && !float.IsNaN(v.AimYaw);
        if (!init) { yawT = yaw; pitchT = pitch; }
        if (i.camReset) ResetView(v != null ? v.transform.eulerAngles.y : f.transform.eulerAngles.y);
        StoryMech sm = f.vehicle as StoryMech;
        float ak = sm != null ? sm.aimK : 0f;
        if (!tank) yawT += i.look.x * (ak > 0.5f ? 0.7f : 1f);
        pitchT = Mathf.Clamp(pitchT - i.look.y * (tank ? 0.3f : 1f), Mathf.Lerp(minPitch, -38f, ak), maxPitch);
        bool touching = i.lookHeld || i.look.sqrMagnitude > 0.0001f;
        if (touching || ak > 0.05f) { manualT = 0f; followW = 0f; } else manualT += dt;
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
        if (v == null && f.remote != null) { dist = 3.4f + f.remote.height * 1.8f; height = 1.4f; }   // ffu12: driving a robot
        dist = (dist + extraDistance) * zoomMul;

        // smooth the player's own input so a finger landing / lifting never jumps the view
        yaw = Mathf.SmoothDampAngle(yaw, yawT, ref yawVel, tank ? 0.05f : 0.09f, Mathf.Infinity, dt);
        pitch = Mathf.SmoothDamp(pitch, pitchT, ref pitchVel, 0.09f, Mathf.Infinity, dt);

        if (!init) { focus = target; init = true; yaw = yawT; pitch = pitchT; curDist = dist; }
        focus = Vector3.Lerp(focus, target, Mathf.Min(1f, dt * 10f));
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = focus + Vector3.up * height * 0.5f;
        if (ak > 0.001f)
        {
            // over the right shoulder, tight, aiming where the screen centre points
            float H = sm.height;
            Vector3 rightV = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            Vector3 sh = sm.transform.position + Vector3.up * H * 1.0f + rightV * H * 0.42f;
            float e = Mathf.SmoothStep(0f, 1f, ak);
            pivot = Vector3.Lerp(pivot, sh, e);
            dist = Mathf.Lerp(dist, H * 0.8f + 4f, e);
            if (baseFov < 0f) baseFov = cam.fieldOfView;
            cam.fieldOfView = Mathf.Lerp(baseFov, baseFov * 0.78f, e);
        }
        else if (baseFov > 0f) { cam.fieldOfView = baseFov; baseFov = -1f; }
        kick = Mathf.MoveTowards(kick, 0f, dt * Mathf.Max(6f, kick * 7f));
        rot = Quaternion.Euler(pitch - kick, yaw, 0f);
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
        TickQuake(dt);
        cam.transform.position = want + shake + QuakePos(curDist);
        cam.transform.rotation = Quaternion.LookRotation((pivot - want).normalized + shake * 0.02f) * QuakeRot();
        if (kick > 0.01f) cam.transform.rotation = cam.transform.rotation * Quaternion.Euler(-kick * 0.6f, 0f, 0f);
        // the pilot's aim point: what the screen centre looks at (skipping the mech itself)
        if (sm != null && ak > 0.2f && sm.driver == f)
        {
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            Vector3 best = ray.origin + ray.direction * 800f; float bd = 1e9f; bool onT = false;
            foreach (RaycastHit h in Physics.RaycastAll(ray, 1500f, ~((1 << 9) | (1 << 10)), QueryTriggerInteraction.Ignore))
            {
                if (h.collider.transform.IsChildOf(sm.transform)) continue;
                if (h.distance < sm.height * 0.4f) continue;
                if (h.distance < bd) { bd = h.distance; best = h.point; onT = h.collider.GetComponentInParent<Vehicle>() != null; }
            }
            sm.aimPoint = best; sm.aimPointValid = true; sm.aimOnTarget = onT;
        }
    }
}
