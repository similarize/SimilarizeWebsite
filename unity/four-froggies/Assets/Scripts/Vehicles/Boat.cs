using UnityEngine;

// Pond boat: four-point buoyancy, thrust only while the hull is wet, wake spray.
public class Boat : Vehicle
{
    public float accel = 9f, maxSpeed = 18f, turnRate = 1.4f;
    public Transform prop;
    readonly Vector3[] floats = { new Vector3(-0.9f, 0f, 1.8f), new Vector3(0.9f, 0f, 1.8f), new Vector3(-0.9f, 0f, -1.9f), new Vector3(0.9f, 0f, -1.9f) };
    float wet;

    public override string HelpLine { get { return "L-stick steer | RT gas | LT reverse | A get out"; } }

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
