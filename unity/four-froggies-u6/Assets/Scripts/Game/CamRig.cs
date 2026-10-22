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
    public void Bump(float deg) { kick = Mathf.Max(kick, deg); }   // ffu47: a little nudge (a cow's lick)
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
    // (dt capped at 0.05: a slow frame (phone hiccup, 3 fps SwiftShader probe) must not swallow the whole quake in one step)
    public void TickQuake(float dt) { quake = Mathf.MoveTowards(quake, 0f, Mathf.Min(dt, 0.05f) * (1.4f + quake * 1.6f)); }
    // ffu15: recoil kick (degrees, view pitches up then settles) + over-the-shoulder aim state
    float kick, baseFov = -1f;
    public void Kick(float deg) { kick = Mathf.Min(kick + deg, 9f); }

    public void Snap() { init = false; }
    // ffu65 (Bill 2026-10-20 Pixel 9: "inside it doesn't let you look up"): indoors (James's house, 2nd floor, camper rooms) the
    // pitch goes up to -65 (65 deg above horizontal) with touch drag / right stick / mouse; as the view tilts up the camera slides in
    // close behind + under the froggy's head and the pivot rises over it, so it looks at the ceiling / lamps instead of clamping.
    public static float DemoLookY;   // probe only: added to the look input (same path as the stick / drag)
    public static bool Indoor(Frog f) { return f != null && f.world == WorldId.House; }
    public float UpK { get; private set; }

    public void SetYaw(float y) { yaw = yawT = y; }
    public void SetPitch(float p) { pitch = pitchT = p; }

    public void ResetView(float behindYaw)
    {
        yawT = behindYaw; pitchT = Mathf.Clamp(16f, minPitch, maxPitch); zoomMul = 1f; manualT = 10f;
    }

    // ffu67h RC DRIVER'S-STAND VIEW (Bill Oct 20 16:37): per player (each slot has its own rig, so split = own choice, remembered while
    // the game runs). Y on the remote / F toggles chase <-> stand: the camera stays at the froggy's own spot under the stand canopy (eye
    // height, behind + over the right shoulder), pans / tilts smoothly to keep the car in view and zooms the FOV gently; it never moves
    // with the car. Far away (> 55 m, free roam) it falls back to the chase view until the car comes back. Controls stay car-relative.
    public bool rcStand; public bool RcStandActive { get; private set; }
    Vector3 standLook; float standFov = -1f, standBase = -1f; bool standInit; Renderer[] standR;
    public bool StandBlockedNow { get; private set; }
    // true when the eye->car segment passes through the froggy's bounds (sphere of the horizontal half-size, +5 cm)
    static bool StandBlocked(Vector3 eye, Vector3 car, Bounds hb)
    {
        Vector3 d = car - eye; float L = d.magnitude; if (L < 0.01f) return false; d /= L;
        float r = Mathf.Max(hb.extents.x, hb.extents.z) * 0.85f + 0.05f;
        // the head part only: centre at the upper third of the bounds
        Vector3 c = new Vector3(hb.center.x, hb.max.y - Mathf.Min(r, hb.extents.y), hb.center.z);
        float t = Mathf.Clamp(Vector3.Dot(c - eye, d), 0f, L);
        return (eye + d * t - c).sqrMagnitude < r * r;
    }
    public void ToggleRcStand(Frog f) { rcStand = !rcStand; standInit = false; if (f != null && f.human) f.Toast(rcStand ? "Stand view (Y: chase view)" : "Chase view (Y: stand view)", 1.4f); }
    void RcStand(Frog f, PIn i, float dt)
    {
        RcCar rc = f.vehicle as RcCar;
        if (rc != null && i.land) ToggleRcStand(f);
        Vector3 me = f.transform.position;
        float d = rc != null ? Vector2.Distance(new Vector2(me.x, me.z), new Vector2(rc.transform.position.x, rc.transform.position.z)) : 0f;
        bool on = rc != null && rcStand && d < 55f;
        if (!on)
        {
            if (RcStandActive) { if (standBase > 0f) cam.fieldOfView = standBase; standBase = -1f; standInit = false; }
            RcStandActive = false; return;
        }
        if (!RcStandActive) { standBase = cam.fieldOfView; standFov = cam.fieldOfView; }
        RcStandActive = true;
        Vector3 car = rc.transform.position + Vector3.up * 0.12f;
        Vector3 dir = car - me; dir.y = 0f; dir = dir.sqrMagnitude > 0.01f ? dir.normalized : f.transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, dir);
        // ffu67h fix 2: the froggy's own head sat on the eye->car line (H1 stand shot: the head filled the middle of the frame
        // and hid the car, worse as the FOV zooms in). Now the eye is over the right shoulder ABOVE the head top (renderer bounds),
        // and if the eye->car ray still passes through the froggy's bounds sphere it climbs/steps right until it clears.
        if (!standInit || standR == null || standR.Length == 0) standR = f.GetComponentsInChildren<Renderer>();
        Bounds hb = new Bounds(me + Vector3.up * 0.8f, Vector3.one * 0.6f); bool hbOk = false;
        foreach (var r in standR) if (r != null && r.enabled && r.gameObject.activeInHierarchy && !(r is ParticleSystemRenderer)) { if (!hbOk) { hb = r.bounds; hbOk = true; } else hb.Encapsulate(r.bounds); }
        float top = Mathf.Clamp(hb.max.y - me.y, 0.6f, 2.4f);
        Vector3 eye = me + Vector3.up * (top + 0.32f) - dir * 0.95f + right * 0.7f;
        for (int k = 0; k < 6; k++) { if (!StandBlocked(eye, car, hb)) break; eye += Vector3.up * 0.18f + right * 0.12f; }
        StandBlockedNow = StandBlocked(eye, car, hb);
        if (!standInit) { standLook = car; standInit = true; }
        standLook = Vector3.Lerp(standLook, car, Mathf.Min(1f, dt * 6f));   // smooth pan / tilt
        cam.transform.position = eye;
        cam.transform.rotation = Quaternion.LookRotation((standLook - eye).normalized);
        // fix 2: portrait screens (Pixel H2: car in frame only 78%) - the zoom framed 3.2 m VERTICALLY, so a tall narrow view lost
        // the car off the sides. Frame 3.2 m across the NARROWER axis (vertical FOV from aspect), and the 22 deg floor applies to it too.
        float asp = Mathf.Min(1f, cam.aspect > 0.05f ? cam.aspect : 1f);
        float vMin = Mathf.Min(standBase, 2f * Mathf.Atan(Mathf.Tan(11f * Mathf.Deg2Rad) / asp) * Mathf.Rad2Deg);
        float fw = Mathf.Clamp(2f * Mathf.Atan(3.2f / (Mathf.Max(1f, d) * asp)) * Mathf.Rad2Deg, vMin, standBase);   // gentle auto-zoom
        standFov = Mathf.Lerp(standFov, fw, Mathf.Min(1f, dt * 1.5f));
        cam.fieldOfView = standFov;
    }
    public void Update(Frog f, PIn i, float dt)
    {
        if (f == null) return;
        Vehicle v = f.vehicle != null ? f.vehicle : f.passengerOf;
        bool tank = v != null && !float.IsNaN(v.AimYaw);
        if (!init) { yawT = yaw; pitchT = pitch; }
        if (i.camReset && Bridge73.Toggle(this, f)) i.camReset = false;   // ffu73: R3 / 0 / double-tap = bridge view aboard the trillion-story mech
        if (i.camReset) ResetView(v != null ? v.transform.eulerAngles.y : f.transform.eulerAngles.y);
        StoryMech sm = f.vehicle as StoryMech;
        float ak = sm != null ? sm.aimK : 0f;
        float bk = sm == null && v == null ? Bow61.AimK(f) : 0f;   // ffu61 froggy bow aim
        if (!tank) yawT += i.look.x * (ak > 0.5f ? 0.7f : 1f);
        if (DemoLookY != 0f) { pitchT -= DemoLookY; manualT = 0f; }
        pitchT = Mathf.Clamp(pitchT - i.look.y * (tank ? 0.3f : 1f) * (bk > 0.5f ? 0.7f : 1f), Mathf.Lerp(minPitch, -38f, Mathf.Max(ak, bk * 0.6f)), maxPitch);
        bool touching = i.lookHeld || i.look.sqrMagnitude > 0.0001f;
        if (touching || ak > 0.05f || bk > 0.05f) { manualT = 0f; followW = 0f; } else manualT += dt;
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
        if (v == null && f.GlideCamK > 0.001f)
        {
            // ffu47 paraglider: pull back + up so the wing (4.5 m over the harness) and the froggy are both in frame, and
            // ease in behind the flight direction when the player isn't looking around
            float gk = Mathf.SmoothStep(0f, 1f, f.GlideCamK);
            dist = Mathf.Lerp(dist, 14.5f, gk); height = Mathf.Lerp(height, 5.4f, gk);
            if (f.GlideFlying && manualT > 1.5f) yawT = Mathf.LerpAngle(yawT, f.glideYaw, Mathf.Min(1f, dt * 1.4f));
        }
        dist = (dist + extraDistance) * zoomMul;
        bool camper64 = v == null && Camper64.In(f);   // ffu64: tight camper interior - close, low, never through the walls
        if (camper64) { dist = Mathf.Min(dist, 2.2f); height = Mathf.Min(height, 1.0f); }
        bool indoor = v == null && Indoor(f);

        // smooth the player's own input so a finger landing / lifting never jumps the view
        yaw = Mathf.SmoothDampAngle(yaw, yawT, ref yawVel, tank ? 0.05f : 0.09f, Mathf.Infinity, dt);
        pitch = Mathf.SmoothDamp(pitch, pitchT, ref pitchVel, 0.09f, Mathf.Infinity, dt);

        if (!init) { focus = target; init = true; yaw = yawT; pitch = pitchT; curDist = dist; }
        focus = Vector3.Lerp(focus, target, Mathf.Min(1f, dt * 10f));
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = focus + Vector3.up * height * 0.5f;
        UpK = indoor ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(8f, -50f, pitch)) : 0f;
        if (UpK > 0.001f)
        {
            dist = Mathf.Lerp(dist, 1.25f, UpK); pivot += Vector3.up * Mathf.Lerp(0f, 0.85f, UpK);
            // never lift the pivot through a low ceiling (camper rooms are 1.65 .. 2.15 m tall)
            RaycastHit up; Vector3 fo = focus + Vector3.up * 0.3f;
            if (Physics.Raycast(fo, Vector3.up, out up, 6f, (1 << 0) | (1 << HouseWorld.WallLayer), QueryTriggerInteraction.Ignore) && pivot.y > up.point.y - 0.3f) pivot.y = Mathf.Max(fo.y, up.point.y - 0.3f);
        }
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
        else if (bk > 0.001f)
        {
            // ffu61: froggy bow - over the right shoulder, close, a little zoom (same idea as the mech arm cannon)
            Vector3 rightV = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            // ffu61b: higher + further right + back (the 1.2 m froggy filled the middle of the view at 1.15 / 0.55 / 2.6 m)
            Vector3 sh = f.transform.position + Vector3.up * 1.5f + rightV * 0.9f;
            float e = Mathf.SmoothStep(0f, 1f, bk);
            pivot = Vector3.Lerp(pivot, sh, e);
            dist = Mathf.Lerp(dist, 3.3f, e);
            if (baseFov < 0f) baseFov = cam.fieldOfView;
            cam.fieldOfView = Mathf.Lerp(baseFov, baseFov * 0.82f, e);
        }
        else if (baseFov > 0f) { cam.fieldOfView = baseFov; baseFov = -1f; }
        kick = Mathf.MoveTowards(kick, 0f, dt * Mathf.Max(6f, kick * 7f));
        rot = Quaternion.Euler(pitch - kick, yaw, 0f);
        Vector3 back = rot * Vector3.back;
        float allowed = dist;
        RaycastHit hit;
        if (Physics.SphereCast(pivot, 0.35f, back, out hit, dist, (1 << 0) | (1 << 12) | (1 << 13) | ((Light55.On && f.world == WorldId.House) || camper64 ? 1 << HouseWorld.WallLayer : 0), QueryTriggerInteraction.Ignore))
            allowed = Mathf.Max(camper64 ? 0.35f : 1.2f, hit.distance - 0.1f);
        // pull in fast (no clipping), ease back out slowly (no popping)
        if (curDist < 0f) curDist = allowed;
        curDist = allowed < curDist ? Mathf.Lerp(curDist, allowed, Mathf.Min(1f, dt * 18f)) : Mathf.MoveTowards(curDist, allowed, dt * Mathf.Max(2f, dist * 0.35f));
        Vector3 want = pivot + back * curDist;
        float gy = Worlds.FloorUnder(want) + 0.5f;
        if (want.y < gy) want.y = gy;
        if (UpK > 0.001f) { float fy = f.transform.position.y + 0.35f; if (want.y < fy) want.y = Mathf.Lerp(want.y, fy, UpK); }

        trauma = Mathf.MoveTowards(trauma, 0f, dt * 1.6f);
        Vector3 shake = trauma > 0f ? Random.insideUnitSphere * trauma * trauma * 0.6f : Vector3.zero;
        TickQuake(dt);
        cam.transform.position = want + shake + QuakePos(curDist);
        cam.transform.rotation = Quaternion.LookRotation((pivot - want).normalized + shake * 0.02f) * QuakeRot();
        if (UpK > 0.001f) cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, rot, UpK);   // indoors looking up: the view follows the pitch, not the (floor-clamped) pivot
        if (kick > 0.01f) cam.transform.rotation = cam.transform.rotation * Quaternion.Euler(-kick * 0.6f, 0f, 0f);
        RcStand(f, i, dt);
        Bridge73.Apply(this, f, i);   // ffu73: bridge view (behind the captain's chair, through the viewscreen)
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
