using System.Collections.Generic;
using UnityEngine;

// Helicopter and passenger drone. Velocity-target flight: left stick flies forward/back and turns
// (drone strafes with the stick and turns with the camera), RT/Space climbs, LT/Shift descends.
// With nobody aboard the rotors spool down and it settles under gravity.
public class Flyer : Vehicle
{
    public bool isDrone;
    public float maxSpeed = 22f, climbSpeed = 8f, turnRate = 85f;
    public readonly List<Transform> rotors = new List<Transform>();
    public readonly List<Vector3> rotorAxes = new List<Vector3>();
    public Transform tailRotor;
    float spool, yaw, pitchVis, rollVis;

    public override string HelpLine
    {
        get
        {
            return isDrone ? "L-stick fly (camera-relative) | RT/Space up | LT/Shift down | A get out"
                           : "L-stick fly + turn | RT/Space up | LT/Shift down | A get out";
        }
    }

    public void InitFlyer(float mass, Vector3 center, Vector3 size)
    {
        flyer = true;
        SetupBody(mass, center, size, new Vector3(0f, center.y - size.y * 0.3f, 0f));
        rb.useGravity = false;
        rb.freezeRotation = true;
        rb.drag = 0f;
        yaw = transform.eulerAngles.y;
    }

    public override void OnEnter()
    {
        base.OnEnter();
        yaw = transform.eulerAngles.y;
    }

    bool NearGround(out float h)
    {
        RaycastHit hit;
        Vector3 o = transform.position + Vector3.up * 0.5f;
        if (Physics.Raycast(o, Vector3.down, out hit, 50f, GroundMask, QueryTriggerInteraction.Ignore)) { h = hit.distance - 0.5f; return true; }
        h = 50f;
        return false;
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        float dt = Time.fixedDeltaTime;
        bool on = driver != null;
        spool = Mathf.MoveTowards(spool, on ? 1f : 0f, dt * (on ? 0.8f : 0.35f));
        float h;
        NearGround(out h);
        Vector3 v = rb.velocity;

        if (on && spool > 0.6f)
        {
            Vector3 wishH;
            if (isDrone)
            {
                Quaternion cy = Quaternion.Euler(0f, camYawIn, 0f);
                wishH = cy * new Vector3(inp.move.x, 0f, inp.move.y) * maxSpeed;
                if (wishH.sqrMagnitude > 1f) yaw = Mathf.MoveTowardsAngle(yaw, Mathf.Atan2(wishH.x, wishH.z) * Mathf.Rad2Deg, turnRate * 1.4f * dt);
            }
            else
            {
                yaw += inp.move.x * turnRate * dt;
                Vector3 f = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                wishH = f * inp.move.y * maxSpeed;
            }
            float climb = inp.climb;
            float wishV = climb * climbSpeed;
            if (Mathf.Abs(climb) < 0.05f) wishV = -v.y * 0.5f;   // hold altitude
            if (transform.position.y > 120f && wishV > 0f) wishV = 0f;
            Vector3 hv = new Vector3(v.x, 0f, v.z);
            hv = Vector3.MoveTowards(hv, wishH, (isDrone ? 18f : 10f) * dt);
            float vy = Mathf.MoveTowards(v.y, wishV, 14f * dt);
            if (h < 0.3f && vy < 0f) vy = 0f;
            rb.velocity = new Vector3(hv.x, vy, hv.z);
        }
        else
        {
            // gravity + gentle lift while the rotors still turn
            v.y -= 9.81f * (1f - spool * 0.85f) * dt;
            float fr = h < 0.4f ? 6f : 0.4f;
            v.x = Mathf.MoveTowards(v.x, 0f, fr * dt);
            v.z = Mathf.MoveTowards(v.z, 0f, fr * dt);
            rb.velocity = v;
        }

        // visual tilt from velocity
        Vector3 local = Quaternion.Euler(0f, -yaw, 0f) * rb.velocity;
        float airborne = Mathf.Clamp01(h / 1.5f);
        pitchVis = Mathf.Lerp(pitchVis, Mathf.Clamp(local.z * 0.9f, -16f, 16f) * airborne, dt * 3f);
        rollVis = Mathf.Lerp(rollVis, Mathf.Clamp(-local.x * 0.9f - (isDrone ? 0f : inp.move.x * 6f), -18f, 18f) * airborne, dt * 3f);
        rb.MoveRotation(Quaternion.Euler(pitchVis, yaw, rollVis));
    }

    void Update()
    {
        float dt = Time.deltaTime;
        float rpm = spool * (isDrone ? 1500f : 900f);
        for (int i = 0; i < rotors.Count; i++)
        {
            Vector3 ax = i < rotorAxes.Count ? rotorAxes[i] : Vector3.up;
            rotors[i].Rotate(ax, rpm * dt * (i % 2 == 0 ? 1f : -1f), Space.Self);
        }
        if (tailRotor != null) tailRotor.Rotate(Vector3.right, rpm * 1.6f * dt, Space.Self);
        if (spool > 0.5f && driver != null)
        {
            float h;
            if (NearGround(out h) && h < 6f) FX.Dust(transform.position - Vector3.up * h, (1f - h / 6f) * 0.5f);
        }
    }
}
