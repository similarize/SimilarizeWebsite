using UnityEngine;

// ffu62 (Bill: "with big wheels the trailer is connected to an empty space"): the hitch on every tow vehicle is LIVE.
// Before, TrailerBarn built the receiver once at stock height while Trailer.HitchLocal (= the joint anchor) drops by
// bigLift and moves back by bigBack with big wheels -> the trailer hung off a point in the air below / behind the bumper.
// Now the receiver socket stays on the bumper, a drop shank (any length) + ball mount reach down / back to EXACTLY
// Trailer.HitchLocal(v) (the joint's connectedAnchor), and while towing two safety chains + a wiring cable sag between
// the truck and the trailer's coupler. Re-laid out whenever the wheel size / lift changes.
public class HitchRig62 : MonoBehaviour
{
    Vehicle v;
    Transform root, socket, post, bar, ballMount, ball, plate;
    Transform[] links;
    float lastLift = -99f, lastBack = -99f;
    const int ChainN = 8, CableN = 12;

    public static void Add(Vehicle v)
    {
        if (v == null || v.GetComponent<HitchRig62>() != null) return;
        var r = v.gameObject.AddComponent<HitchRig62>();
        r.v = v; r.Build();
    }

    public static Vector3 BallTopLocal(Vehicle v) { return Trailer.HitchLocal(v) + new Vector3(0f, 0.04f, 0f); }

    void Build()
    {
        root = Mats.Node(v.transform, "HitchReceiver", Vector3.zero);
        Material dark = Mats.Lit(new Color(0.1f, 0.1f, 0.11f)), steel = Mats.Steel(new Color(0.7f, 0.71f, 0.73f));
        Material chain = Mats.Steel(new Color(0.55f, 0.56f, 0.58f)), cable = Mats.Lit(new Color(0.05f, 0.05f, 0.05f));
        socket = Mats.Prim(PrimitiveType.Cube, root, Vector3.zero, new Vector3(0.16f, 0.16f, 0.4f), dark).transform;
        plate = Mats.Prim(PrimitiveType.Cube, root, Vector3.zero, new Vector3(0.5f, 0.06f, 0.08f), dark).transform;   // receiver cross-member
        post = Mats.Prim(PrimitiveType.Cube, root, Vector3.zero, Vector3.one * 0.1f, dark).transform;              // drop / rise shank
        bar = Mats.Prim(PrimitiveType.Cube, root, Vector3.zero, Vector3.one * 0.1f, dark).transform;               // ball-mount bar
        ballMount = Mats.Prim(PrimitiveType.Cube, root, Vector3.zero, new Vector3(0.2f, 0.05f, 0.2f), steel).transform;
        ball = Mats.Prim(PrimitiveType.Sphere, root, Vector3.zero, Vector3.one * 0.1f, steel).transform;
        links = new Transform[ChainN * 2 + CableN];
        for (int i = 0; i < links.Length; i++)
        {
            bool c = i >= ChainN * 2;
            var g = Mats.Prim(PrimitiveType.Cube, root, Vector3.zero, Vector3.one * 0.03f, c ? cable : chain);
            links[i] = g.transform; g.SetActive(false);
        }
        Mats.SetLayer(root.gameObject, Vehicle.VehicleLayer);
        foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Layout();
    }

    static void Seg(Transform t, Vector3 a, Vector3 b, float thick)
    {
        Vector3 d = b - a; float L = Mathf.Max(d.magnitude, 0.02f);
        t.localPosition = (a + b) * 0.5f;
        t.localRotation = Quaternion.LookRotation(d.sqrMagnitude > 1e-6f ? d : Vector3.forward, Mathf.Abs(d.normalized.y) > 0.95f ? Vector3.forward : Vector3.up);
        t.localScale = new Vector3(thick, thick, L + thick);
    }

    void Layout()
    {
        lastLift = v.bigLift; lastBack = v.bigBack;
        Vector3 h = Trailer.HitchLocal(v);
        BoxCollider b = v.body;
        Bike bk = v as Bike;
        float bumperY = b != null ? Mathf.Max(0.55f, b.center.y - b.size.y * 0.5f + 0.1f) : 0.6f;
        float sockY = Mathf.Max(bumperY, h.y + v.bigLift);            // the receiver rides with the body
        float sockZ = h.z + v.bigBack + 0.62f;                        // at the stock bumper (big tyres push the ball back)
        if (bk != null && bk.spotty) { sockY = h.y + 0.25f; sockZ = h.z + 0.5f; }
        socket.localPosition = new Vector3(0f, sockY, sockZ + 0.1f);
        plate.localPosition = new Vector3(0f, sockY + 0.02f, sockZ + 0.32f);
        float barY = h.y - 0.03f;
        Seg(post, new Vector3(0f, sockY, sockZ - 0.05f), new Vector3(0f, barY, sockZ - 0.05f), 0.1f);
        post.gameObject.SetActive(Mathf.Abs(sockY - barY) > 0.08f);
        Seg(bar, new Vector3(0f, barY, sockZ - 0.05f), new Vector3(0f, barY, h.z), 0.09f);
        ballMount.localPosition = new Vector3(0f, barY + 0.02f, h.z);
        ball.localPosition = h + new Vector3(0f, 0.04f, 0f);
    }

    void LateUpdate()
    {
        if (v == null) { Destroy(this); return; }
        if (Mathf.Abs(v.bigLift - lastLift) > 0.005f || Mathf.Abs(v.bigBack - lastBack) > 0.005f) Layout();
        Trailer t = TrailerTow.TowedBy(v);
        bool on = t != null;
        if (links[0].gameObject.activeSelf != on) foreach (var l in links) l.gameObject.SetActive(on);
        if (!on) return;
        // chains: from the receiver's sides to the trailer tongue just behind the coupler; cable: from the bumper plug
        Vector3 h = Trailer.HitchLocal(v);
        Vector3 tc = v.transform.InverseTransformPoint(t.BallWorld);
        Vector3 tf = v.transform.InverseTransformDirection(t.transform.forward);   // trailer forward (toward the truck)
        Vector3 tr = Vector3.Cross(Vector3.up, tf).normalized;
        for (int s = 0; s < 2; s++)
        {
            float sx = s == 0 ? -1f : 1f;
            Vector3 A = new Vector3(sx * 0.14f, h.y - 0.06f, h.z + 0.3f);
            Vector3 B = tc - tf * 0.35f + tr * sx * 0.16f + Vector3.down * 0.08f;
            Sag(ChainN * s, ChainN, A, B, 0.16f, 0.035f);
        }
        Vector3 CA = new Vector3(0.22f, socket.localPosition.y + 0.05f, socket.localPosition.z + 0.15f);
        Vector3 CB = tc - tf * 0.55f + tr * 0.1f + Vector3.up * 0.05f;
        Sag(ChainN * 2, CableN, CA, CB, 0.22f, 0.025f);
    }

    void Sag(int i0, int n, Vector3 A, Vector3 B, float sag, float thick)
    {
        Vector3 prev = A;
        for (int k = 1; k <= n; k++)
        {
            float u = k / (float)n;
            Vector3 p = Vector3.Lerp(A, B, u) + Vector3.down * (sag * 4f * u * (1f - u));
            Seg(links[i0 + k - 1], prev, p, thick);
            prev = p;
        }
    }
}
