using UnityEngine;

// ffu52 HERO VEHICLES (Bill 2026-10-16: "make the Roadster and the Cybertruck look true to life from real photos").
// Both are our own photo-matched meshes (work/lb-gfx/ff/build_roadster52.py + build_cyber52.py -> Resources/LB/roadster52 +
// cybertruck52 .bytes), built from Tesla's published dimensions + public photos (no logos, badges or third-party meshes):
//  * Cybertruck (VehicleFactory.Cybertruck = James's, Dad's and the yard truck): L 5.68 W 2.03 H 1.79 m, wheelbase 3.635,
//    track 1.772, LT285/65R20 (r 0.439), ~3010 kg; brushed stainless (ct52_brushed texture, metallic), black flares / sills.
//  * Roadster (Roadster49): wheelbase 2.825 (game 2.80), 295/30R20 front + 335/30R21 rear, ~1700 kg, Ano White, gold glass.
//  * CarFeel52: visual weight transfer (the sprung body squats / dives / rolls on its springs with the car's acceleration)
//    + a soft contact shadow under the car.
//  ?ffcars=old : the pre-ffu52 cars (old meshes, masses, no CarFeel) for before / after shots.
public static class HeroCars52
{
    static int old = -1;
    public static bool Old { get { if (old < 0) old = Ffu33.Url("ffcars") == "old" ? 1 : 0; return old == 1; } }
    public static LBPack Pack(string n) { return Old ? null : LBPack.Get(n); }
    public static readonly Color AnoWhite = new Color(0.93f, 0.93f, 0.915f);
}

[DefaultExecutionOrder(50)]
public class CarFeel52 : MonoBehaviour
{
    public static bool Log;                    // demo: log pitch / roll
    Rigidbody rb; Transform sprung; Vector3 pivot;
    float kP, kR, maxDeg, w0, zeta;
    Vector3 lastV, aS; bool init;
    float pitch, pitchV, roll, rollV;
    public float Pitch { get { return pitch; } }
    public float Roll { get { return roll; } }
    public float MaxPitch, MinPitch, MaxRoll;
    Transform blob; Material blobMat; float blobW, blobL, blobK = 1f;

    // kPitch / kRoll: degrees per m/s^2 of acceleration; hz: body natural frequency (real cars ~1.2-1.8 Hz)
    public static CarFeel52 Attach(Vehicle v, Transform sprung, Vector3 pivot, float kPitch, float kRoll, float hz, float len, float wid)
    {
        var c = v.gameObject.AddComponent<CarFeel52>();
        c.rb = v.rb; c.sprung = sprung; c.pivot = pivot; c.kP = kPitch; c.kR = kRoll; c.maxDeg = 4f;
        c.w0 = 2f * Mathf.PI * hz; c.zeta = 0.55f;
        c.blobW = wid * 1.22f; c.blobL = len * 1.1f;
        c.MakeBlob();
        return c;
    }

    void MakeBlob()
    {
        try
        {
            var sh = Shader.Find("FF/RRBlob");
            if (sh == null) return;
            var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(g.GetComponent<Collider>());
            g.name = "ContactShadow52";
            blobMat = new Material(sh); blobMat.SetFloat("_Str", 0.62f);
            g.GetComponent<MeshRenderer>().sharedMaterial = blobMat;
            g.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            g.GetComponent<MeshRenderer>().receiveShadows = false;
            blob = g.transform;
        }
        catch (System.Exception e) { Debug.LogWarning("CarFeel52 blob: " + e.Message); }
    }

    void OnDestroy() { if (blob != null) Destroy(blob.gameObject); }
    void OnDisable() { if (blob != null) blob.gameObject.SetActive(false); }
    void OnEnable() { if (blob != null) blob.gameObject.SetActive(true); }

    void FixedUpdate()
    {
        if (rb == null || sprung == null) return;
        float dt = Time.fixedDeltaTime;
        Vector3 v = rb.isKinematic ? Vector3.zero : rb.velocity;
        if (!init) { lastV = v; init = true; }
        Vector3 a = (v - lastV) / dt; lastV = v;
        if (a.sqrMagnitude > 2500f) a = a.normalized * 50f;          // collisions / teleports: no huge kick
        aS = Vector3.Lerp(aS, a, 1f - Mathf.Exp(-dt / 0.06f));
        Vector3 la = transform.InverseTransformDirection(aS);
        // accelerating = squat (nose up), braking = dive; cornering = roll to the outside
        float tp = Mathf.Clamp(-la.z * kP, -maxDeg, maxDeg);
        float tr = Mathf.Clamp(la.x * kR, -maxDeg, maxDeg);
        float k = w0 * w0, c = 2f * zeta * w0;
        pitchV += (k * (tp - pitch) - c * pitchV) * dt; pitch += pitchV * dt;
        rollV += (k * (tr - roll) - c * rollV) * dt; roll += rollV * dt;
        pitch = Mathf.Clamp(pitch, -maxDeg * 1.2f, maxDeg * 1.2f); roll = Mathf.Clamp(roll, -maxDeg * 1.2f, maxDeg * 1.2f);
        MaxPitch = Mathf.Max(MaxPitch, pitch); MinPitch = Mathf.Min(MinPitch, pitch); MaxRoll = Mathf.Max(MaxRoll, Mathf.Abs(roll));
    }

    void LateUpdate()
    {
        if (sprung == null) return;
        Quaternion q = Quaternion.Euler(pitch, 0f, roll);
        sprung.localRotation = q;
        sprung.localPosition = pivot - q * pivot;
        if (blob == null) return;
        // soft contact shadow: on the ground under the car, fades as it lifts off (jumps, thrusters, walls)
        RaycastHit hit;
        Vector3 c0 = transform.position + transform.up * 0.6f;
        bool ok = Physics.Raycast(c0, Vector3.down, out hit, 8f, ~(1 << Vehicle.VehicleLayer), QueryTriggerInteraction.Ignore);
        float h = ok ? Mathf.Max(0f, transform.position.y - hit.point.y) : 99f;
        float up = transform.up.y;
        float target = ok && up > 0.5f ? Mathf.Clamp01(1f - h / 2.5f) * Mathf.Clamp01((up - 0.5f) * 3f) : 0f;
        blobK = Mathf.MoveTowards(blobK, target, Time.deltaTime * 4f);
        bool vis = blobK > 0.02f;
        if (blob.gameObject.activeSelf != vis) blob.gameObject.SetActive(vis);
        if (!vis) return;
        Vector3 fw = Vector3.ProjectOnPlane(transform.forward, hit.normal).normalized;
        blob.position = hit.point + hit.normal * 0.02f;
        blob.rotation = Quaternion.LookRotation(-hit.normal, fw);
        float grow = 1f + h * 0.25f;
        blob.localScale = new Vector3(blobW * grow, blobL * grow, 1f);
        blobMat.SetFloat("_Str", 0.62f * blobK);
    }
}
