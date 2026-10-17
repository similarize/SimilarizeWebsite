using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// ffu56 FOOTPRINTS + TYRE TRACKS, the same for everyone (consistency, Bill): every frog / froggy, every robot, every animal
// and every ground vehicle (each grounded wheel / track roller) presses prints into soft ground - on the ranch by surface
// (wet mud at the pond rim = deep + long-lived, dirt / paths / litter = clear, grass = faint), on Mars / the Moon /
// Callisto in the dust. None on roads, floors, decks or under water. One ring-buffer mesh (one draw call), prints fade out.
// Atlas (made here, no texture files): 0 frog foot, 1 robot boot, 2 tyre tread, 3 hoof.
public class Tracks56 : MonoBehaviour
{
    public static Tracks56 I;
    public static int Prints, TyreMarks, FrogPrints, RobotPrints, AnimalPrints;
    int N;
    Vector3[] v; Vector2[] uv; Color32[] col; float[] born, life, a0;
    int head; bool dirtyV, dirtyC; float fadeT;
    Mesh mesh; Material mat;
    class S { public Vector3 last; public bool left; }
    readonly Dictionary<int, S> st = new Dictionary<int, S>();
    Frog[] frogs = new Frog[0]; float frogT;

    void Awake()
    {
        I = this;
        N = Terrain56.Lite ? 900 : 2600;
        v = new Vector3[N * 4]; uv = new Vector2[N * 4]; col = new Color32[N * 4]; born = new float[N]; life = new float[N]; a0 = new float[N];
        var tri = new int[N * 6];
        for (int i = 0; i < N; i++) { int b = i * 4, t = i * 6; tri[t] = b; tri[t + 1] = b + 2; tri[t + 2] = b + 1; tri[t + 3] = b; tri[t + 4] = b + 3; tri[t + 5] = b + 2; }
        mesh = new Mesh { name = "Tracks56", indexFormat = IndexFormat.UInt32 };
        mesh.MarkDynamic();
        mesh.vertices = v; mesh.uv = uv; mesh.colors32 = col; mesh.triangles = tri;
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
        mat = new Material(Mats.Fx) { name = "Tracks56" };
        mat.mainTexture = Atlas();
        mat.renderQueue = 2460;
        var go = new GameObject("Tracks56Mesh");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
        Debug.Log("TERRAIN56 tracks: " + N + " print slots, atlas frog / boot / tyre / hoof");
    }

    static Texture2D Atlas()
    {
        const int S = 128, W = S * 4;
        var t = new Texture2D(W, S, TextureFormat.RGBA32, true) { name = "TracksAtlas56", wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[W * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < W; x++)
            {
                int cell = x / S; float u = (x % S + 0.5f) / S, w = (y + 0.5f) / S;   // u across, w along (toes at w = 1)
                float a = 0f;
                if (cell == 0)
                {   // frog: round palm + 4 long toes with round tips
                    a = Blob(u, w, 0.5f, 0.3f, 0.2f, 0.17f);
                    for (int k = 0; k < 4; k++)
                    {
                        float ang = (-0.75f + k * 0.5f), tx = 0.5f + Mathf.Sin(ang) * 0.36f, tz = 0.3f + Mathf.Cos(ang) * 0.55f;
                        a = Mathf.Max(a, Line(u, w, 0.5f, 0.35f, tx, tz, 0.045f));
                        a = Mathf.Max(a, Blob(u, w, tx, tz, 0.08f, 0.08f));
                    }
                }
                else if (cell == 1)
                {   // robot boot: rounded sole with heel gap and tread bars
                    float sole = Mathf.Max(Blob(u, w, 0.5f, 0.68f, 0.3f, 0.28f), Blob(u, w, 0.5f, 0.22f, 0.24f, 0.17f));
                    float bars = (Mathf.Repeat(w * 9f, 1f) < 0.55f) ? 1f : 0.45f;
                    a = sole * bars;
                }
                else if (cell == 2)
                {   // tyre tread: full width band with chevrons, tiles along w
                    float edge = Mathf.Clamp01((0.47f - Mathf.Abs(u - 0.5f)) * 30f);
                    float chev = Mathf.Repeat(w * 4f + Mathf.Abs(u - 0.5f) * 1.6f, 1f) < 0.5f ? 1f : 0.5f;
                    a = edge * chev;
                }
                else
                {   // hoof: U shape
                    float o = Blob(u, w, 0.5f, 0.5f, 0.36f, 0.4f), i = Blob(u, w, 0.5f, 0.36f, 0.18f, 0.3f);
                    a = Mathf.Clamp01(o - i * 0.85f);
                }
                px[y * W + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
            }
        t.SetPixels32(px); t.Apply(true, true);
        return t;
    }
    static float Blob(float u, float w, float cx, float cz, float rx, float rz)
    {
        float dx = (u - cx) / rx, dz = (w - cz) / rz; float d = dx * dx + dz * dz;
        return Mathf.Clamp01((1f - d) * 4f);
    }
    static float Line(float u, float w, float ax, float az, float bx, float bz, float r)
    {
        Vector2 p = new Vector2(u, w), a = new Vector2(ax, az), ab = new Vector2(bx - ax, bz - az);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Mathf.Clamp01((r - (a + ab * t - p).magnitude) / r * 3f);
    }

    // how soft / which colour the ground is at a contact (0 = no print)
    static float Soft(Vector3 p, RaycastHit h, out Color c, out float lifeK)
    {
        c = new Color(0.17f, 0.11f, 0.06f); lifeK = 1f;
        WorldId w = Terrain56.WorldAt(p);
        if (w == WorldId.Ranch)
        {
            if (!(h.collider is TerrainCollider)) return 0f;
            if (Layout.PondQ(p.x, p.z) < 1.0f && p.y < Layout.WaterY) return 0f;   // under water
            if (Layout.RoadDist(p.x, p.z) < Layout.TrackW * 0.5f) return 0f;   // asphalt
            var s = Terrain56.Weights(p.x, p.z);
            lifeK = 0.5f + s.m * 2.5f + s.s * 1.2f;
            if (s.m > 0.4f) c = new Color(0.12f, 0.08f, 0.04f);
            return Mathf.Clamp01(s.m * 1.1f + s.s * 0.8f + (s.d + s.p) * 0.7f + s.l * 0.55f + s.g * 0.28f);
        }
        if (w == WorldId.Mars) { c = new Color(0.28f, 0.1f, 0.04f); lifeK = 3f; return 0.8f; }
        if (w == WorldId.Moon) { c = new Color(0.1f, 0.1f, 0.1f); lifeK = 4f; return 0.85f; }
        if (w == WorldId.Callisto) { c = new Color(0.18f, 0.18f, 0.2f); lifeK = 3f; return 0.7f; }
        return 0f;
    }

    bool Ground(Vector3 p, float up, out RaycastHit h)
    {
        return Physics.Raycast(p + Vector3.up * up, Vector3.down, out h, up + 0.45f, Vehicle.GroundMask, QueryTriggerInteraction.Ignore);
    }

    void Put(Vector3 p, Vector3 n, Vector3 fwd, float wid, float len, int cell, Color c, float alpha, float secs)
    {
        if (alpha < 0.03f) return;
        fwd = Vector3.ProjectOnPlane(fwd, n); if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward; fwd.Normalize();
        Vector3 rt = Vector3.Cross(n, fwd).normalized * wid * 0.5f, fl = fwd * len * 0.5f, lift = n * 0.025f;
        int i = head; head = (head + 1) % N; int b = i * 4;
        v[b] = p - rt - fl + lift; v[b + 1] = p + rt - fl + lift; v[b + 2] = p + rt + fl + lift; v[b + 3] = p - rt + fl + lift;
        float u0 = cell * 0.25f + 0.004f, u1 = cell * 0.25f + 0.246f;
        float vl = cell == 2 ? len / Mathf.Max(0.1f, wid) * 0.5f : 1f;
        uv[b] = new Vector2(u0, 0f); uv[b + 1] = new Vector2(u1, 0f); uv[b + 2] = new Vector2(u1, Mathf.Min(1f, vl)); uv[b + 3] = new Vector2(u0, Mathf.Min(1f, vl));
        born[i] = Time.time; life[i] = secs; a0[i] = alpha;
        var cc = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), (byte)(alpha * 255f));
        col[b] = col[b + 1] = col[b + 2] = col[b + 3] = cc;
        dirtyV = true; dirtyC = true; Prints++;
    }

    // one footstep for a walker: strides of 'stride' metres, left / right offset 'gap', print size w x l
    void Step(int id, Vector3 pos, Vector3 fwd, float stride, float gap, float w, float l, int cell, float up, ref int counter)
    {
        S s; if (!st.TryGetValue(id, out s)) { s = new S { last = pos }; st[id] = s; return; }
        Vector3 d = pos - s.last; d.y = 0f;
        if (d.sqrMagnitude > 25f) { s.last = pos; return; }   // teleported
        if (d.sqrMagnitude < stride * stride) return;
        s.last = pos; s.left = !s.left;
        Vector3 dir = d.normalized; if (fwd.sqrMagnitude > 0.01f && Vector3.Dot(fwd, dir) > 0.2f) dir = (fwd + dir).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, dir).normalized * (s.left ? -gap : gap);
        RaycastHit h; if (!Ground(pos + side, up, out h)) return;
        Color c; float lk; float soft = Soft(h.point, h, out c, out lk); if (soft <= 0f) return;
        Put(h.point, h.normal, dir, w, l, cell, c, 0.62f * soft, 25f * lk);
        counter++;
    }

    // vehicles with their own wheel physics (the Roadster, bikes, skateboards, RC cars): wheel spots from their real track /
    // wheelbase, contact from a ray under the body
    void OtherWheels(Vehicle veh)
    {
        if (veh == null || !veh.isActiveAndEnabled || veh.stowed || veh.rb == null || veh.rb.isKinematic) return;
        float track, base_, tyre; bool single = false;
        if (veh is Roadster49) { track = 1.62f; base_ = 2.6f; tyre = 0.3f; }
        else if (veh is Bike) { track = 0f; base_ = 1.45f; tyre = 0.16f; single = true; }
        else if (veh is Skateboard) { track = 0.22f; base_ = 0.5f; tyre = 0.05f; }
        else if (veh is RcCar) { track = 0.26f; base_ = 0.34f; tyre = 0.07f; }
        else return;
        if (veh.rb.velocity.sqrMagnitude < 0.25f) return;
        Transform t = veh.transform;
        RaycastHit h0;
        if (!Physics.Raycast(t.position + Vector3.up * 1f, Vector3.down, out h0, 3f, Vehicle.GroundMask, QueryTriggerInteraction.Ignore) || t.position.y - h0.point.y > 1.3f) { for (int k = 0; k < 4; k++) st.Remove(veh.GetInstanceID() * 31 + k); return; }
        int n = single ? 1 : 4;
        for (int k = 0; k < n; k++)
        {
            float sx = single ? 0f : (k % 2 == 0 ? -0.5f : 0.5f) * track, sz = single ? -0.3f * base_ : (k < 2 ? -0.5f : 0.5f) * base_;
            Vector3 cp = t.position + t.right * sx + t.forward * sz; cp.y = h0.point.y;
            int id = veh.GetInstanceID() * 31 + k;
            S s; if (!st.TryGetValue(id, out s)) { st[id] = new S { last = cp }; continue; }
            Vector3 d = cp - s.last;
            if (d.sqrMagnitude > 36f) { s.last = cp; continue; }
            if (d.sqrMagnitude < 0.35f * 0.35f) continue;
            RaycastHit h; if (!Ground((cp + s.last) * 0.5f, 0.6f, out h)) { s.last = cp; continue; }
            Color c; float lk; float soft = Soft(h.point, h, out c, out lk);
            if (soft > 0f) { Put(h.point, h.normal, d, Mathf.Max(tyre, 0.05f) * 1.1f, d.magnitude + 0.04f, 2, c, 0.55f * Mathf.Clamp01(soft + 0.15f), 22f * lk); TyreMarks++; }
            s.last = cp;
        }
    }

    void Update()
    {
        if (Game.I == null) return;
        frogT -= Time.deltaTime;
        if (frogT <= 0f) { frogT = 2f; frogs = Object.FindObjectsOfType<Frog>(); }
        foreach (var f in frogs)
        {
            if (f == null || !f.isActiveAndEnabled || f.vehicle != null || f.passengerOf != null || f.Swimming) continue;
            if (f.world != WorldId.Ranch && f.world != WorldId.Mars && f.world != WorldId.Moon && f.world != WorldId.Callisto) continue;
            float sc = Mathf.Clamp(f.transform.lossyScale.y, 0.5f, 3f);
            Step(f.GetInstanceID(), f.transform.position, f.transform.forward, 0.42f * sc, 0.11f * sc, 0.17f * sc, 0.2f * sc, 0, 0.35f, ref FrogPrints);
        }
        if (RanchLife.I != null)
            foreach (var r in RanchLife.I.robots)
            {
                if (r == null || !r.isActiveAndEnabled || r.rideOf != null || !float.IsNaN(r.yOver) || r.transform.parent != null) continue;
                if (r.world != WorldId.Ranch && r.world != WorldId.Mars && r.world != WorldId.Moon && r.world != WorldId.Callisto) continue;
                Step(r.GetInstanceID(), r.transform.position, r.transform.forward, 0.68f, 0.14f, 0.15f, 0.3f, 1, 0.35f, ref RobotPrints);
            }
        foreach (var a in Animal.All)
        {
            if (a == null || !a.isActiveAndEnabled) continue;
            Step(a.GetInstanceID(), a.transform.position, a.transform.forward, 0.5f, 0.2f, 0.13f, 0.14f, 3, 0.4f, ref AnimalPrints);
        }
        foreach (var veh in Vehicle.All)
        {
            var gv = veh as GroundVehicle;
            if (gv == null) { OtherWheels(veh); continue; }
            if (!gv.isActiveAndEnabled || gv.stowed || gv.rb == null || gv.rb.isKinematic) continue;
            if (gv.rb.velocity.sqrMagnitude < 0.25f) continue;
            for (int k = 0; k < gv.wheels.Count; k++)
            {
                var w = gv.wheels[k];
                int id = gv.GetInstanceID() * 31 + k;
                if (!w.grounded) { st.Remove(id); continue; }
                Vector3 up = gv.transform.up;
                Vector3 cp = gv.transform.TransformPoint(w.mount) - up * w.dist;
                S s; if (!st.TryGetValue(id, out s)) { st[id] = new S { last = cp }; continue; }
                Vector3 d = cp - s.last;
                if (d.sqrMagnitude > 36f) { s.last = cp; continue; }
                float seg = Mathf.Max(0.35f, w.radius * 0.9f);
                if (d.sqrMagnitude < seg * seg) continue;
                RaycastHit h; if (!Ground((cp + s.last) * 0.5f, 0.5f, out h)) { s.last = cp; continue; }
                Color c; float lk; float soft = Soft(h.point, h, out c, out lk);
                if (soft > 0f) { Put((cp + s.last) * 0.5f, h.normal, d, Mathf.Clamp(w.radius * 0.62f, 0.18f, 1.2f), d.magnitude + 0.04f, 2, c, 0.55f * Mathf.Clamp01(soft + 0.15f), 22f * lk); TyreMarks++; }
                s.last = cp;
            }
        }
        // fade
        fadeT -= Time.deltaTime;
        if (fadeT <= 0f)
        {
            fadeT = 0.4f; float now = Time.time;
            for (int i = 0; i < N; i++)
            {
                if (life[i] <= 0f) continue;
                float age = now - born[i], k = Mathf.Clamp01((life[i] - age) / (life[i] * 0.35f));
                byte a = (byte)(a0[i] * k * 255f);
                int b = i * 4;
                if (col[b].a != a) { col[b].a = col[b + 1].a = col[b + 2].a = col[b + 3].a = a; dirtyC = true; }
                if (k <= 0f) life[i] = 0f;
            }
        }
        if (dirtyV) { mesh.vertices = v; mesh.uv = uv; dirtyV = false; }
        if (dirtyC) { mesh.colors32 = col; dirtyC = false; }
    }
}
