using System.Collections.Generic;
using UnityEngine;

// ffu32 GARDEN RETREAT: a hammock (lie in it sprawled out, it swings side to side) or a porch swing bench by the campfire
// (sit, it swings back and forth facing the fire). Both are Vehicles so the usual A / E / tap enters and the usual camera
// follows; the root is a kinematic, static body (the swinging parts are children). Jump (SPACE / A / tap A) or E gets out.
// Stick / WASD pushes the swing; it settles back to a slow idle rock. hasWorldBounds = true keeps it out of the vehicle
// wreck system (Combat.Eligible), and robots never pick it (RobotDrive.List skips GardenSeat).
public class GardenSeat : Vehicle
{
    public enum Kind { Hammock, Swing }
    public Kind kind;
    public Transform pivot;            // hammock: rotates about local z (its long axis); swing: about local x (the top beam)
    public Vector3 standPoint;         // where an AI froggy walks to before getting in (world)
    public static readonly List<GardenSeat> Seats = new List<GardenSeat>();
    float phase, amp, enteredT, creakCool;
    float period, idleAmp, maxAmp;
    public float aiPush;               // AI riders: a gentle self-push amount (0..1)
    public float occupiedT;            // seconds the current rider has been in
    public void Push(float k) { amp = Mathf.Min(maxAmp, amp + k * 6f); }   // ffu54: a robot pushes the swing

    public override string HelpLine
    {
        get
        {
            InputKind k = driver != null ? driver.inputKind : InputKind.Keyboard;
            string push = k == InputKind.Gamepad ? "L-stick" : k == InputKind.Touch ? "stick" : "WASD";
            string outK = k == InputKind.Gamepad ? "A" : k == InputKind.Touch ? "A" : "SPACE / E";
            return kind == Kind.Hammock ? "Hammock - " + push + ": rock  |  " + outK + ": hop out"
                                        : "Porch swing - " + push + ": swing  |  " + outK + ": hop off";
        }
    }
    public override string HudStatus { get { return kind == Kind.Hammock ? "Hammock" : "Porch swing" + (Campfire.I != null && Campfire.I.Lit ? "  -  the fire is crackling" : ""); } }
    public override string[] TouchSet { get { return new[] { "A", null, null, null, null }; } }
    public override bool CanEnter(Frog f) { return f != null && f.world == WorldId.Ranch; }

    public static GardenSeat Create(Kind k, Transform parent, Vector3 pos, float yaw, int index)
    {
        var go = new GameObject(k == Kind.Hammock ? "Hammock " + index : "Porch swing " + index);
        go.transform.SetParent(parent, true);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        var s = go.AddComponent<GardenSeat>();
        s.kind = k;
        s.Title = k == Kind.Hammock ? "Hammock" : "Porch swing";
        s.EnterVerb = k == Kind.Hammock ? "lie in the hammock" : "sit on the swing";
        s.showDriver = true;
        if (k == Kind.Hammock) s.BuildHammock(index); else s.BuildSwing(index);
        // the static body: a box around the bed / bench (frogs walk up to it; vehicles bump it)
        if (k == Kind.Hammock) s.SetupBody(60f, new Vector3(0f, 0.75f, 0f), new Vector3(1.15f, 0.55f, 2.9f), new Vector3(0f, 0.7f, 0f));
        else s.SetupBody(60f, new Vector3(0f, 0.52f, 0f), new Vector3(1.75f, 0.5f, 0.75f), new Vector3(0f, 0.5f, 0f));
        s.rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        s.rb.isKinematic = true;
        s.rb.interpolation = RigidbodyInterpolation.None;
        s.hasWorldBounds = true; s.worldCenter = pos; s.worldRadius = 1e6f;
        s.camDistance = k == Kind.Hammock ? 5.2f : 5.6f;
        s.camHeight = k == Kind.Hammock ? 2.0f : 1.7f;
        s.period = k == Kind.Hammock ? 2.7f : 2.3f;
        s.idleAmp = k == Kind.Hammock ? 4.5f : 6f;
        s.maxAmp = k == Kind.Hammock ? 19f : 24f;
        s.amp = 1.5f;
        s.phase = index * 1.7f;
        Vector3 side = go.transform.right;
        s.standPoint = k == Kind.Hammock ? pos + side * 1.3f : pos + go.transform.forward * 1.2f;
        Seats.Add(s);
        return s;
    }

    protected new void OnDestroy() { Seats.Remove(this); Vehicle.All.Remove(this); }

    // ---------------- geometry ----------------
    static Texture2D stripeTex;
    static Material HammockMat()
    {
        if (stripeTex == null)
        {
            // woven stripes across the bed (cream / sunset orange / teal / cream / coral), 64 x 4
            Color[] band = { new Color(0.95f, 0.9f, 0.78f), new Color(0.96f, 0.55f, 0.22f), new Color(0.15f, 0.55f, 0.6f), new Color(0.95f, 0.9f, 0.78f), new Color(0.9f, 0.36f, 0.33f), new Color(0.98f, 0.8f, 0.3f) };
            stripeTex = new Texture2D(64, 4, TextureFormat.RGBA32, true);
            stripeTex.wrapMode = TextureWrapMode.Repeat;
            for (int x = 0; x < 64; x++)
            {
                Color c = band[(x / 6) % band.Length];
                if (x % 6 == 0) c *= 0.82f;
                for (int y = 0; y < 4; y++) stripeTex.SetPixel(x, y, c * (0.96f + 0.04f * ((x + y) % 2)));
            }
            stripeTex.Apply();
        }
        var m = Mats.Tex(stripeTex, 0.05f);
        return m;
    }

    // a curved fabric bed hanging under the pivot: length L (z), width W (x), ends at y yEnd, centre sag down to yMid,
    // sides curl up by curl. Two-sided (the underside is seen from the camera too).
    static Mesh BedMesh(float L, float W, float yEnd, float yMid, float curl)
    {
        int nz = 18, nx = 8;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        for (int side = 0; side < 2; side++)
        {
            int b = v.Count;
            for (int iz = 0; iz <= nz; iz++)
                for (int ix = 0; ix <= nx; ix++)
                {
                    float u = iz / (float)nz, w = ix / (float)nx;
                    float z = (u - 0.5f) * L, s = (w - 0.5f) * 2f;
                    float zz = (u - 0.5f) * 2f;
                    float y = Mathf.Lerp(yMid, yEnd, zz * zz);
                    float widthK = Mathf.Lerp(1f, 0.22f, Mathf.Pow(Mathf.Abs(zz), 3f));   // gathered at the ends
                    float x = s * W * 0.5f * widthK;
                    y += curl * s * s * (1f - zz * zz) - (side == 1 ? 0.012f : 0f);
                    v.Add(new Vector3(x, y, z));
                    uv.Add(new Vector2(u * 3f, w));
                }
            for (int iz = 0; iz < nz; iz++)
                for (int ix = 0; ix < nx; ix++)
                {
                    int a = b + iz * (nx + 1) + ix, c = a + nx + 1;
                    if (side == 0) { tri.Add(a); tri.Add(c); tri.Add(a + 1); tri.Add(a + 1); tri.Add(c); tri.Add(c + 1); }
                    else { tri.Add(a); tri.Add(a + 1); tri.Add(c); tri.Add(a + 1); tri.Add(c + 1); tri.Add(c); }
                }
        }
        var m = new Mesh();
        m.name = "HammockBed";
        m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    static void Rope(Transform parent, Vector3 a, Vector3 b, float thick, Material m)
    {
        var g = Mats.Prim(PrimitiveType.Cylinder, parent, (a + b) * 0.5f, new Vector3(thick, (b - a).magnitude * 0.5f, thick), m);
        g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
        g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    public const float HamHalf = 2.15f, HamTop = 1.68f;
    void BuildHammock(int index)
    {
        Transform t = transform;
        Material wood = Mats.TexTint("LB/wood", new Color(0.62f, 0.45f, 0.3f), 0.1f), rope = Mats.Lit(new Color(0.86f, 0.8f, 0.66f));
        Material woodD = Mats.TexTint("LB/wood", new Color(0.46f, 0.33f, 0.22f), 0.1f);
        // two stout posts with a little cross brace + a cap each end (posts lean out a touch, like a hammock stand)
        for (int e = -1; e <= 1; e += 2)
        {
            var post = Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0.95f, e * HamHalf), new Vector3(0.2f, 0.95f, 0.2f), new Vector3(e * 6f, 0f, 0f), wood, true);
            Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 1.92f, e * (HamHalf + 0.1f)), new Vector3(0.24f, 0.04f, 0.24f), woodD);
            // foot braces (visual)
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.09f, e * (HamHalf + 0.25f)), new Vector3(0.9f, 0.14f, 0.16f), woodD);
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.09f, e * (HamHalf + 0.25f)), new Vector3(0.16f, 0.14f, 0.9f), woodD);
            post.name = "Post";
        }
        pivot = Mats.Node(t, "Swing", new Vector3(0f, HamTop, 0f));
        float yEnd = -0.62f, yMid = -1.0f;
        var bed = new GameObject("Bed");
        bed.transform.SetParent(pivot, false);
        bed.AddComponent<MeshFilter>().sharedMesh = BedMesh(2.95f, 1.15f, yEnd, yMid, 0.2f);
        var mr = bed.AddComponent<MeshRenderer>(); mr.sharedMaterial = HammockMat();
        // spreader bars at both ends + the ropes up to the posts (fanned strings)
        for (int e = -1; e <= 1; e += 2)
        {
            float ze = e * 1.5f;
            Mats.Prim(PrimitiveType.Cylinder, pivot, new Vector3(0f, yEnd + 0.03f, ze), new Vector3(0.05f, 0.34f, 0.05f), new Vector3(0f, 0f, 90f), woodD);
            Vector3 top = new Vector3(0f, 0.02f, e * (HamHalf - 0.1f));
            for (int k = -2; k <= 2; k++) Rope(pivot, new Vector3(k * 0.075f, yEnd + 0.05f, ze), top, 0.018f, rope);
        }
        // a little pillow at the head end (+z)
        Mats.Prim(PrimitiveType.Sphere, pivot, new Vector3(0f, yMid + 0.16f + 0.17f, 0.95f), new Vector3(0.55f, 0.16f, 0.32f), Mats.Lit(index % 2 == 0 ? new Color(0.97f, 0.93f, 0.85f) : new Color(0.55f, 0.75f, 0.85f)));
        // rider: lying on the back, head toward +z, feet toward -z, sunk into the middle
        seat = Mats.Node(pivot, "Seat", new Vector3(0f, yMid + 0.36f, -0.5f));
        seat.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward) * Quaternion.Euler(-8f, 0f, 0f);
        seatScale = 0.86f;
    }

    void BuildSwing(int index)
    {
        Transform t = transform;
        Material wood = Mats.TexTint("LB/wood", new Color(0.6f, 0.43f, 0.28f), 0.1f), chain = Mats.Steel(new Color(0.55f, 0.55f, 0.57f));
        Color[] cush = { new Color(0.92f, 0.45f, 0.25f), new Color(0.25f, 0.55f, 0.62f), new Color(0.95f, 0.78f, 0.3f), new Color(0.8f, 0.35f, 0.4f) };
        // A-frame swing stand: two A legs each side + a top beam along x
        float H = 2.35f, hw = 1.25f;
        for (int s = -1; s <= 1; s += 2)
            for (int f = -1; f <= 1; f += 2)
            {
                Vector3 foot = new Vector3(s * (hw + 0.12f), 0f, f * 0.75f), top = new Vector3(s * hw, H, 0f);
                var leg = Mats.Prim(PrimitiveType.Cube, t, (foot + top) * 0.5f, new Vector3(0.13f, (top - foot).magnitude, 0.13f), wood, true);
                leg.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (top - foot).normalized);
            }
        for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, t, new Vector3(s * (hw + 0.06f), 0.85f, 0f), new Vector3(0.1f, 0.1f, 1.0f), wood);   // A cross bars
        Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, H + 0.04f, 0f), new Vector3(2.75f, 0.16f, 0.16f), wood, true);
        // a little roof canopy over the beam (striped awning)
        var roof = Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, H + 0.32f, 0f), new Vector3(2.9f, 0.05f, 1.2f), Mats.Lit(cush[index % cush.Length] * 0.95f));
        roof.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        for (int f = -1; f <= 1; f += 2) Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, H + 0.18f, f * 0.55f), new Vector3(2.9f, 0.26f, 0.04f), Mats.Lit(new Color(0.97f, 0.95f, 0.9f)));
        pivot = Mats.Node(t, "Swing", new Vector3(0f, H, 0f));
        float sy = -H + 0.5f;   // seat top 0.5 m above the ground
        Material plank = Mats.TexTint("LB/wood", new Color(0.72f, 0.55f, 0.38f), 0.12f);
        Mats.Prim(PrimitiveType.Cube, pivot, new Vector3(0f, sy - 0.04f, 0f), new Vector3(1.6f, 0.07f, 0.55f), plank);
        var back = Mats.Prim(PrimitiveType.Cube, pivot, new Vector3(0f, sy + 0.3f, -0.3f), new Vector3(1.6f, 0.55f, 0.06f), plank);
        back.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
        for (int s = -1; s <= 1; s += 2)
        {
            Mats.Prim(PrimitiveType.Cube, pivot, new Vector3(s * 0.8f, sy + 0.2f, 0f), new Vector3(0.06f, 0.06f, 0.58f), plank);   // arm rests
            Mats.Prim(PrimitiveType.Cube, pivot, new Vector3(s * 0.8f, sy + 0.08f, 0.24f), new Vector3(0.06f, 0.24f, 0.06f), plank);
            Rope(pivot, new Vector3(s * 0.8f, 0f, 0f), new Vector3(s * 0.8f, sy + 0.2f, 0.25f), 0.03f, chain);
            Rope(pivot, new Vector3(s * 0.8f, 0f, 0f), new Vector3(s * 0.8f, sy + 0.58f, -0.33f), 0.03f, chain);
        }
        // cushion + a throw pillow
        Mats.Prim(PrimitiveType.Cube, pivot, new Vector3(0f, sy + 0.02f, 0.02f), new Vector3(1.5f, 0.07f, 0.5f), Mats.Lit(cush[index % cush.Length]));
        var pil = Mats.Prim(PrimitiveType.Cube, pivot, new Vector3(-0.52f, sy + 0.22f, -0.18f), new Vector3(0.36f, 0.32f, 0.12f), Mats.Lit(cush[(index + 2) % cush.Length]));
        pil.transform.localRotation = Quaternion.Euler(-14f, 12f, 8f);
        seat = Mats.Node(pivot, "Seat", new Vector3(0.12f, sy + 0.06f, 0.02f));
        seatScale = 0.8f;
    }

    // ---------------- behaviour ----------------
    public override void OnEnter() { enteredT = 0f; occupiedT = 0f; amp = Mathf.Max(amp, idleAmp * 0.8f); Sfx.Play(GardenAudio.Creak, 0.25f, 1.1f); }
    public override void OnExit() { base.OnExit(); aiPush = 0f; }

    public override void Drive(PIn i, float camYaw, float dt)
    {
        base.Drive(i, camYaw, dt);
        enteredT += dt;
        if (driver != null && inp.hop && enteredT > 0.5f) { driver.ExitVehicle(); return; }
        float push = kind == Kind.Hammock ? Mathf.Max(Mathf.Abs(inp.move.x), Mathf.Abs(inp.move.y)) : Mathf.Max(Mathf.Abs(inp.move.y), inp.gas, Mathf.Abs(inp.move.x) * 0.5f);
        push = Mathf.Max(push, aiPush);
        if (push > 0.05f) amp = Mathf.Min(maxAmp, amp + push * 7f * dt);
    }

    protected override void FixedUpdate() { }   // static kinematic root: no flipping / out-of-world logic

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (driver != null) occupiedT += dt;
        float idle = driver != null ? idleAmp : 1.2f;
        amp = Mathf.MoveTowards(amp, idle, (amp > idle ? 1.6f : 3f) * dt);
        float prevV = Mathf.Cos(phase);
        phase += dt * 6.2832f / period;
        float a = amp * Mathf.Sin(phase);
        if (pivot != null) pivot.localRotation = kind == Kind.Hammock ? Quaternion.Euler(0f, 0f, a) : Quaternion.Euler(a, 0f, 0f);
        creakCool -= dt;
        if (driver != null && amp > 8f && creakCool <= 0f && Mathf.Sign(prevV) != Mathf.Sign(Mathf.Cos(phase)))
        {
            creakCool = period * 0.45f;
            if (Random.value < 0.6f) Sfx.PlayAt(GardenAudio.Creak, transform.position + Vector3.up, 0.16f + amp * 0.006f, 22f, Random.Range(0.85f, 1.15f));
        }
    }

    // pose the rider after Frog.Update's seated pose (LateUpdate wins): hammock = sprawled on the back, arms behind the
    // head, legs apart; swing = sitting back, hands up on the chains
    void LateUpdate()
    {
        if (driver == null || driver.model == null || driver.model.pet || driver.model.bob == null) return;
        FrogModel m = driver.model;
        float t = Time.time + driver.id;
        if (kind == Kind.Hammock)
        {
            m.bob.localPosition = Vector3.zero;
            m.bob.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.8f) * 1.5f, 0f, 0f);
            m.bob.localScale = new Vector3(1f, 1f + Mathf.Sin(t * 1.6f) * 0.015f, 1f);   // slow breathing
            if (m.legL != null) m.legL.localRotation = Quaternion.Euler(-14f, 0f, -24f);
            if (m.legR != null) m.legR.localRotation = Quaternion.Euler(-14f, 0f, 24f);
            if (m.armL != null) m.armL.localRotation = Quaternion.Euler(-165f, 0f, 30f);
            if (m.armR != null) m.armR.localRotation = Quaternion.Euler(-165f, 0f, -30f);
            if (m.head != null) m.head.localRotation = Quaternion.Euler(-10f + Mathf.Sin(t * 0.4f) * 4f, Mathf.Sin(t * 0.3f) * 16f, 0f);
        }
        else
        {
            if (m.armL != null) m.armL.localRotation = Quaternion.Euler(-118f, 0f, 22f);
            if (m.armR != null) m.armR.localRotation = Quaternion.Euler(-118f, 0f, -22f);
            float a = amp * Mathf.Sin(phase);
            if (m.legL != null) m.legL.localRotation = Quaternion.Euler(-62f + a * 0.8f, 0f, 0f);   // legs kick with the swing
            if (m.legR != null) m.legR.localRotation = Quaternion.Euler(-62f + a * 0.8f, 0f, 0f);
            if (m.head != null && Campfire.I != null && Campfire.I.Lit) m.head.localRotation = Quaternion.Euler(8f + Mathf.Sin(t * 0.5f) * 3f, Mathf.Sin(t * 0.35f) * 10f, 0f);
        }
    }

    public static GardenSeat FreeNear(Vector3 p, float maxDist, Kind? k = null)
    {
        GardenSeat best = null; float bd = maxDist * maxDist;
        foreach (var s in Seats)
        {
            if (s == null || s.driver != null || Garden.Claimed(s)) continue;
            if (k.HasValue && s.kind != k.Value) continue;
            float d = (s.transform.position - p).sqrMagnitude;
            if (d < bd) { bd = d; best = s; }
        }
        return best;
    }
}
