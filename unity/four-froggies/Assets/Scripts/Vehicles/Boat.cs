using UnityEngine;

// Pond boat: four-point buoyancy, thrust only while the hull is wet, wake spray.
public class Boat : Vehicle
{
    public override string[] TouchSet { get { return new[] { "A", null, "PUSH", "GAS", "BRAKE" }; } }
    public float accel = 9f, maxSpeed = 18f, turnRate = 1.4f;
    public Transform prop;
    readonly Vector3[] floats = { new Vector3(-0.9f, 0f, 1.8f), new Vector3(0.9f, 0f, 1.8f), new Vector3(-0.9f, 0f, -1.9f), new Vector3(0.9f, 0f, -1.9f) };
    float wet, beachedT, pushCool;
    public bool Beached { get { return beachedT > 0.6f; } }

    public override string HelpLine
    {
        get
        {
            return Beached ? "Beached! RB / Y / right-click / MSL = push off (it also drifts back on its own)"
                           : "L-stick steer | RT gas | LT reverse | A get out";
        }
    }

    // direction from here to deep water: away from an island we are stuck on, else towards the pond centre
    Vector3 DeepDir(Vector3 p)
    {
        float best = 1e9f; Vector3 dir = Vector3.zero;
        foreach (Vector3 isl in Layout.Islands)
        {
            Vector2 d = new Vector2(p.x - isl.x, p.z - isl.y);
            float k = d.magnitude / isl.z;
            if (k < 1.9f && k < best) { best = k; dir = new Vector3(d.x, 0f, d.y).normalized; }
        }
        if (dir == Vector3.zero) dir = new Vector3(Layout.PondC.x - p.x, 0f, Layout.PondC.y - p.z).normalized;
        return dir;
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        float dt = Time.fixedDeltaTime;
        int inWater = 0;
        float perPoint = rb.mass * 9.81f / floats.Length;
        foreach (Vector3 f in floats)
        {
            Vector3 p = transform.TransformPoint(f);
            float depth = Layout.WaterY - p.y;
            if (depth > 0f && Layout.InPond(p.x, p.z))
            {
                inWater++;
                float k = Mathf.Min(depth / 0.35f, 2.5f);
                Vector3 pv = rb.GetPointVelocity(p);
                rb.AddForceAtPosition(Vector3.up * (perPoint * k - pv.y * rb.mass * 0.45f), p);
            }
        }
        wet = inWater / (float)floats.Length;
        // beached on a shore or an island: drift (and on request shove) back into deep water
        Vector3 bp = rb.position;
        bool nearPond = Layout.PondQ(bp.x, bp.z) < 1.45f;
        if (nearPond) { if (wet < 0.75f && rb.velocity.magnitude < 2.5f) beachedT += dt; else beachedT = Mathf.Max(0f, beachedT - dt * 2f); }
        pushCool -= dt;
        if (beachedT > 0.6f)
        {
            Vector3 deep = DeepDir(bp);
            if (driver != null && inp.alt && pushCool <= 0f)
            {
                pushCool = 0.8f;
                rb.AddForce((deep * 7f + Vector3.up * 2.5f), ForceMode.VelocityChange);
                FX.Splash(bp + Vector3.up * 0.3f, 12);
                Sfx.PlayAt(Sfx.Splash, bp, 0.9f);
            }
            if (beachedT > 2.0f)
            {
                // a little wave keeps nudging it out
                rb.AddForce(deep * 7f + Vector3.up * (wet < 0.25f ? 3f : 0f), ForceMode.Acceleration);
                float yaw = Mathf.Atan2(deep.x, deep.z) * Mathf.Rad2Deg;
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, Quaternion.Euler(0f, yaw, 0f), dt * 0.6f));
                if (Random.value < 0.08f) FX.Splash(bp + deep * 2f, 3);
            }
            // way up on land with nobody aboard for a while: back to its mooring
            if (driver == null && beachedT > 12f && Layout.PondQ(bp.x, bp.z) > 1.15f)
            {
                beachedT = 0f;
                rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
                rb.position = spawnPos + Vector3.up * 0.5f; rb.rotation = spawnRot;
            }
        }
        else if (!nearPond && driver == null)
        {
            beachedT += dt;
            if (beachedT > 12f) { beachedT = 0f; rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; rb.position = spawnPos + Vector3.up * 0.5f; rb.rotation = spawnRot; }
        }
        Vector3 v = rb.velocity;
        Vector3 fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 right = transform.right; right.y = 0f; right.Normalize();
        float fs = Vector3.Dot(v, fwd), ls = Vector3.Dot(v, right);
        float throttle = Mathf.Clamp(inp.move.y + inp.gas - inp.brake, -1f, 1f);
        if (driver == null) throttle = 0f;
        float push = wet > 0f ? 1f : 0.12f;   // can shuffle back into the water if beached
        if (throttle > 0f && fs < maxSpeed) rb.AddForce(fwd * accel * throttle * push * rb.mass);
        else if (throttle < 0f && fs > -maxSpeed * 0.35f) rb.AddForce(fwd * accel * 0.5f * throttle * push * rb.mass);
        if (wet > 0f)
        {
            rb.AddForce(-right * ls * 2.2f * rb.mass * wet);
            rb.AddForce(-fwd * fs * 0.25f * rb.mass * wet);
            float target = (driver != null ? inp.move.x : 0f) * turnRate * Mathf.Clamp(0.35f + Mathf.Abs(fs) / 8f, 0f, 1f) * (fs < -0.5f ? -1f : 1f);
            float yr = YawRate(rb, transform);
            rb.AddTorque(Vector3.up * (target - yr) * Mathf.Min(1f, 4f * dt), ForceMode.VelocityChange);
            rb.angularDrag = 2.5f;
        }
        else rb.angularDrag = 0.8f;
        if (wet > 0f && Mathf.Abs(fs) > 3f && Random.value < 0.6f)
        {
            Vector3 stern = transform.TransformPoint(new Vector3(0f, 0f, -2.6f));
            stern.y = Layout.WaterY + 0.1f;
            FX.Splash(stern + Random.insideUnitSphere * 0.4f, 1 + Mathf.Abs(fs) * 0.1f);
        }
    }

    void Update()
    {
        if (prop != null) prop.Rotate(Vector3.forward, (driver != null ? 900f + Speed * 80f : 0f) * Time.deltaTime, Space.Self);
    }
}
