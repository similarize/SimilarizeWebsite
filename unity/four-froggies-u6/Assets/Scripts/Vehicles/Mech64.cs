using System.Collections.Generic;
using UnityEngine;

// ffu64 AIRCRAFT vs GIANT MECHS / ROBOTS. The story mechs' only collider is the body box over the legs + hips (0..0.64 H)
// and it sits on the vehicle layer, which the drone / helicopter ground check (Vehicle.GroundMask) ignores - so aircraft
// flew through the upper body and could never land on one. Mech64 adds solid kinematic "pads" that follow the bones:
// torso (its top = the shoulders) + head (StoryMech), and shoulders + head on the big ranch robots (height >= 4 m,
// e.g. Big Figure Two). Pads are on the vehicle layer (aircraft bump into them; froggies ignore the vehicle layer as before),
// Flyer.NearGround also looks for pads under it, so the hover / landing logic treats a shoulder or the head top as ground,
// and once landed (low above a pad, not climbing) the aircraft RIDES ALONG: each fixed step it takes the pad's move + turn.
// Knob &mech64=0 = old behaviour. Logs "MECH64 ...".
[DefaultExecutionOrder(-50)]
public class Mech64 : MonoBehaviour
{
    public static Mech64 I;
    public static bool On = true;
    public static int Landings;
    public class Pad { public string name; public Transform follow; public Vector3 c; public Rigidbody rb; public BoxCollider col; public Matrix4x4 last, now; public bool child; public Component owner; }
    readonly List<Pad> pads = new List<Pad>();
    readonly Dictionary<Collider, Pad> byCol = new Dictionary<Collider, Pad>();
    readonly HashSet<Component> known = new HashSet<Component>();
    float scanT;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        On = Ffu33.Url("mech64") != "0";
        I = new GameObject("mech64").AddComponent<Mech64>();
        DontDestroyOnLoad(I.gameObject);
    }

    public static bool IsPad(Collider c) { return I != null && c != null && I.byCol.ContainsKey(c); }
    public List<Pad> PadsOf(Component owner) { var l = new List<Pad>(); foreach (var p in pads) if (p.owner == owner) l.Add(p); return l; }

    // local AABB (bone space) of the renderers under `bone`, skipping the sub-trees in `skip`
    static bool LocalBounds(Transform bone, Transform[] skip, out Bounds b)
    {
        b = new Bounds(); bool any = false;
        foreach (var mf in bone.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            bool sk = false; foreach (var s in skip) if (s != null && s != bone && mf.transform.IsChildOf(s)) sk = true;
            if (sk) continue;
            Bounds mb = mf.sharedMesh.bounds;
            Matrix4x4 m = bone.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = m.MultiplyPoint3x4(c);
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
            }
        }
        return any && b.size.x > 0.05f && b.size.y > 0.05f;
    }

    Pad Add(Component owner, string name, Transform follow, Vector3 c, Vector3 size, bool child)
    {
        var go = new GameObject("Pad64 " + name);
        go.layer = Vehicle.VehicleLayer;
        var p = new Pad { name = name, follow = follow, c = c, owner = owner, child = child };
        Vector3 ls = follow.lossyScale;
        if (child) { go.transform.SetParent(follow, false); go.transform.localPosition = c; }
        else { go.transform.SetPositionAndRotation(follow.TransformPoint(c), follow.rotation); }
        p.col = go.AddComponent<BoxCollider>();
        p.col.size = child ? size : Vector3.Scale(size, ls);
        p.rb = go.AddComponent<Rigidbody>(); p.rb.isKinematic = true; p.rb.useGravity = false; p.rb.interpolation = RigidbodyInterpolation.None;
        p.now = p.last = Matrix4x4.TRS(go.transform.position, go.transform.rotation, Vector3.one);
        pads.Add(p); byCol[p.col] = p;
        return p;
    }

    void AddMech(StoryMech m)
    {
        float H = m.height;
        Transform torso = m.Bone64(0), head = m.Bone64(1), aL = m.Bone64(2), aR = m.Bone64(3);
        var skip = new[] { head, aL, aR };
        Bounds b; int n = 0;
        if (torso != null && LocalBounds(torso, skip, out b)) { Add(m, m.Title + " torso/shoulders", torso, b.center, b.size, false); n++; }
        else Add(m, m.Title + " torso/shoulders", m.transform, new Vector3(0f, 0.66f * H, 0f), new Vector3(0.38f, 0.32f, 0.22f) * H, false);
        if (head != null && LocalBounds(head, new Transform[0], out b)) { Add(m, m.Title + " head", head, b.center, b.size, false); n++; }
        else Add(m, m.Title + " head", m.transform, new Vector3(0f, 0.9f * H, 0f), new Vector3(0.16f, 0.12f, 0.14f) * H, false);
        // (arms stay pad-free: swinging kinematic arm boxes would shove parked cars around)
        Debug.Log("MECH64 pads for " + m.Title + " H " + H + " from mesh bounds " + n + "/2");
    }

    void AddRobot(Robot r)
    {
        float h = r.height, w = Mathf.Max(0.42f * h, 0.6f);
        Add(r, r.robotName + " shoulders", r.transform, new Vector3(0f, 0.8f * h, 0f), new Vector3(w, 0.12f * h, 0.24f * h), true);
        Add(r, r.robotName + " head", r.transform, new Vector3(0f, 0.93f * h, 0f), new Vector3(0.17f * h, 0.13f * h, 0.17f * h), true);
        Debug.Log("MECH64 pads for robot " + r.robotName + " h " + h.ToString("0.0"));
    }

    void FixedUpdate()
    {
        if (!On) return;
        scanT -= Time.fixedDeltaTime;
        if (scanT <= 0f)
        {
            scanT = 1f;
            foreach (var m in StoryMech.AllMechs) if (m != null && !known.Contains(m)) { known.Add(m); AddMech(m); }
            if (RanchLife.I != null) foreach (var r in RanchLife.I.robots) if (r != null && r.height >= 4f && !known.Contains(r)) { known.Add(r); AddRobot(r); }
            for (int i = pads.Count - 1; i >= 0; i--) if (pads[i].owner == null || pads[i].follow == null) { if (pads[i].col != null) { byCol.Remove(pads[i].col); Destroy(pads[i].col.gameObject); } pads.RemoveAt(i); }
        }
        foreach (var p in pads)
        {
            if (p.follow == null) continue;
            bool act = p.follow.gameObject.activeInHierarchy;
            if (p.col.enabled != act) p.col.enabled = act;
            p.last = p.now;
            if (p.child) { p.now = Matrix4x4.TRS(p.col.transform.position, p.col.transform.rotation, Vector3.one); continue; }
            Vector3 pos = p.follow.TransformPoint(p.c); Quaternion rot = p.follow.rotation;
            p.rb.MovePosition(pos); p.rb.MoveRotation(rot);
            p.now = Matrix4x4.TRS(pos, rot, Vector3.one);
        }
    }

    // Flyer ground check: the nearest pad straight below (vehicle layer, so GroundMask never sees it)
    public static bool PadBelow(Vector3 o, float maxD, out RaycastHit best)
    {
        best = default(RaycastHit);
        if (!On || I == null || I.pads.Count == 0) return false;
        bool any = false;
        foreach (var h in Physics.RaycastAll(o, Vector3.down, maxD, 1 << Vehicle.VehicleLayer, QueryTriggerInteraction.Ignore))
            if (I.byCol.ContainsKey(h.collider) && (!any || h.distance < best.distance)) { best = h; any = true; }
        return any;
    }

    // landed on a pad (low, not climbing) -> carry the aircraft with the pad's move + turn since the last fixed step
    public static bool Ride(Flyer fl, Collider gc, float h, float climb, ref float yaw)
    {
        Pad p;
        if (!On || I == null || gc == null || !I.byCol.TryGetValue(gc, out p)) { if (fl != null) fl.ride64 = null; return false; }
        if (h > 0.6f || climb > 0.05f) { fl.ride64 = null; return false; }
        if (fl.ride64 != p.name) { fl.ride64 = p.name; Landings++; Debug.Log("MECH64 landed " + fl.Title + " on " + p.name + " h " + h.ToString("0.00")); }
        Matrix4x4 d = p.now * p.last.inverse;
        Vector3 np = d.MultiplyPoint3x4(fl.rb.position);
        if ((np - fl.rb.position).sqrMagnitude > 1e-8f) fl.rb.position = np;
        Vector3 f0 = d.MultiplyVector(Quaternion.Euler(0f, yaw, 0f) * Vector3.forward); f0.y = 0f;
        if (f0.sqrMagnitude > 1e-6f) yaw = Mathf.Atan2(f0.x, f0.z) * Mathf.Rad2Deg;
        return true;
    }
}
