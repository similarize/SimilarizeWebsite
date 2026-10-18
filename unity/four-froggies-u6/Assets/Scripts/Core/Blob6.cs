using UnityEngine;

// ffu6k: soft contact shadow ("blob AO") under froggies and animals, the same trick HeroCars52 uses under the cars: a multiply quad
// (FF/RRBlob) laid on the ground below the character, sized from its renderers, fading + spreading as it hops / jumps off the ground.
// Grounds characters in shade, in the house and on SwiftShader-class GPUs where the soft shadow map is too coarse to show feet.
// Cost: one short raycast per character every 2nd frame + one tiny transparent quad. Off on the lowest Perf60 tier and with ?u6blob=0.
public class Blob6 : MonoBehaviour
{
    public static bool Off;
    static Shader sh; static bool shTried; static int count;
    Transform blob; Material mat; float k, size = -1f; int mask, frame; Frog frog;
    void Start() { frog = GetComponent<Frog>(); }

    public static void Attach(GameObject g, int ignoreLayer)
    {
        if (Off || g == null || g.GetComponent<Blob6>() != null) return;
        if (!shTried) { shTried = true; sh = Shader.Find("FF/RRBlob"); Off = sh == null || FFQ("u6blob") == "0"; if (Off) return; }
        if (sh == null) return;
        var b = g.AddComponent<Blob6>();
        b.mask = ~((1 << ignoreLayer) | (1 << 2));   // not the character itself, not Ignore Raycast
        if (++count == 1) Debug.Log("U6: contact shadows (Blob6) on");
    }
    static string FFQ(string key)
    {
        string u = Application.absoluteURL ?? ""; int q = u.IndexOf('?'); if (q < 0) return null;
        foreach (var kv in u.Substring(q + 1).Split('&')) { int e = kv.IndexOf('='); if ((e >= 0 ? kv.Substring(0, e) : kv) == key) return e >= 0 ? kv.Substring(e + 1) : "1"; }
        return null;
    }

    void Make()
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(g.GetComponent<Collider>());
        g.name = "ContactShadow6";
        mat = new Material(sh);
        var r = g.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        blob = g.transform;
    }

    void MeasureSize()
    {
        Bounds bb = new Bounds(transform.position, Vector3.zero); bool any = false;
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r.transform == blob) continue;
            if (!any) { bb = r.bounds; any = true; } else bb.Encapsulate(r.bounds);
        }
        size = any ? Mathf.Clamp(Mathf.Max(bb.size.x, bb.size.z) * 1.15f, 0.25f, 3.5f) : 0.6f;
    }

    void OnDisable() { if (blob != null) blob.gameObject.SetActive(false); }
    void OnDestroy() { if (blob != null) Destroy(blob.gameObject); }

    void LateUpdate()
    {
        if (Off || (Perf60.On && Perf60.Tier < 1)) { if (blob != null && blob.gameObject.activeSelf) blob.gameObject.SetActive(false); return; }
        if (blob == null) Make();
        if (size < 0f || (frame++ & 63) == 0) MeasureSize();   // grows with hats / suits / scale changes
        RaycastHit hit;
        Vector3 p = transform.position;
        bool ok = Physics.Raycast(p + Vector3.up * 0.3f, Vector3.down, out hit, 4f, mask, QueryTriggerInteraction.Ignore);
        float h = ok ? Mathf.Max(0f, p.y - hit.point.y) : 99f;
        bool seated = frog != null && (frog.vehicle != null || frog.passengerOf != null || frog.InScuba) || transform.parent != null && transform.parent.GetComponentInParent<Vehicle>() != null;
        float target = ok && !seated ? Mathf.Clamp01(1f - h / (1.2f + size)) : 0f;
        k = Mathf.MoveTowards(k, target, Time.deltaTime * 5f);
        bool vis = k > 0.02f;
        if (blob.gameObject.activeSelf != vis) blob.gameObject.SetActive(vis);
        if (!vis) return;
        blob.position = hit.point + hit.normal * 0.015f;
        blob.rotation = Quaternion.LookRotation(-hit.normal, Vector3.ProjectOnPlane(transform.forward, hit.normal).normalized + Vector3.forward * 1e-4f);
        float s = size * (1f + h * 0.35f);
        blob.localScale = new Vector3(s, s, 1f);
        mat.SetFloat("_Str", 0.5f * k);
    }
}
