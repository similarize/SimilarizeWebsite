using System.Collections.Generic;
using UnityEngine;

// ffu53 ONE SEA: the underwater world IS the ranch pond. Worlds.UnderO now sits at the pond centre on the water line
// (was a separate hidden copy at (1400, 0, 0)), so the reef, kelp, wreck, cave, fish, turtles, rays, jellies, the whale,
// the pearls and the submarine are built INTO the pond basin, on the ranch pond bed itself (SeaY = the ranch ground).
// A truck that drives in, the Optimus mech, a mech wading, a swimming / scuba froggy and the sub all see the same things
// in the same places. WorldId.Underwater is only the "under water" camera / controls mode now, not another place.
// Underwater world under the pond. Board the submarine at the pond dock and it dives straight down into the
// pond (ffu53; before: a separate hidden root): coral reef, kelp forest, a shipwreck, a rock cave, fish schools,
// turtles, rays, jellies and a whale (the 3D version's sea life). Goal loop: find all the pearls.
// A in the sub = swim out in scuba gear (3D swim: stick moves, RT / A rises, LT / B sinks, gentle buoyancy,
// bubbles); A next to the sub = climb back in; surface the sub (or swim to the top) + keep rising = ranch.
public class UnderwaterWorld : MonoBehaviour
{
    public static UnderwaterWorld I;
    Transform root;
    bool built;
    // ffu60: the sea (reef / wreck / cave / sand / light shafts) only renders for a camera near or under the pond, the
    // fish / turtles / rays / jellies / whale / kelp with the same gate since ffu60d (they show through the water from shore) - per camera, so in
    // split-screen a diver sees it while the player on the porch doesn't pay for it. Their animation pauses meanwhile.
    Renderer[] gateSea, gateLife; bool seaShown = true, lifeShown = true; int lifeFrame = -100;
    static readonly HashSet<int> owned = new HashSet<int>();
    public static bool Owns(Renderer r) { return r != null && owned.Contains(r.GetHashCode()); }
    public static int GateSea, GateLife;   // probe counters: renderers per group
    public Submarine sub;
    float best = -1f, roundStart = -1f;
    const string Group = "pearls";

    class School { public Vector3 c, vel; public float t, seed; public readonly List<Transform> fish = new List<Transform>(); public readonly List<Vector3> off = new List<Vector3>(); }
    readonly List<School> schools = new List<School>();
    class Swimmer { public Transform t; public float a, r, sp, y, zs = 1f; public Vector3 c; public bool bob; }
    readonly List<Swimmer> swimmers = new List<Swimmer>();
    readonly List<Transform> kelp = new List<Transform>();

    public static Vector3 L(float x, float y, float z) { return Worlds.UnderO + new Vector3(x, y, z); }

    // seabed height (local). ffu37: the shared pond-bed truth lives in Layout.SubSeaY (the ranch pond bed uses it too)
    // ffu53: the real ranch pond bed under this spot (the same ground every vehicle / mech / froggy stands on); Layout.SubSeaY
    // before the terrain exists
    public static float SeaY(float x, float z)
    {
        float g = Ranch.GY(Worlds.UnderO.x + x, Worlds.UnderO.z + z);
        if (g == 0f) return Layout.SubSeaY(x, z);
        return g - Worlds.UnderO.y;
    }
    // ffu53: squeeze a local (x, z) into the pond ellipse (q <= qMax) so swimmers never leave the water
    public static Vector3 InPond(Vector3 local, float qMax)
    {
        Vector2 r = Layout.PondR;
        float q = (local.x / r.x) * (local.x / r.x) + (local.z / r.y) * (local.z / r.y);
        if (q <= qMax * qMax || q < 1e-6f) return local;
        float k = qMax / Mathf.Sqrt(q);
        return new Vector3(local.x * k, local.y, local.z * k);
    }
    float caveX0 = -64f;
    Transform chestLid, motes; Vector3 chestPos; bool chestOpen; float chestShut;
    public static Vector3 Mooring { get { return Ranch.SubMooring != Vector3.zero ? Ranch.SubMooring : L(-74f, -0.4f, -3.9f); } }

    public static void Create()
    {
        var go = new GameObject("UnderwaterWorld");
        I = go.AddComponent<UnderwaterWorld>();
        Pickups.Listen((it, f) => { if (it.group == Group) I.OnPearl(f); });
    }

    public Vector3 DockSpot { get { Vector2 c = Layout.PondC, r = Layout.PondR; return new Vector3(c.x - r.x + 2f, 0.7f, c.y); } }

    // ranch side: board the parked sub at the dock -> dive
    public void Dive(Frog f)
    {
        if (!built) Build();
        if (sub.driver == null && (sub.transform.position - Mooring).magnitude > 6f) { sub.rb.velocity = Vector3.zero; sub.rb.position = Mooring; sub.transform.SetPositionAndRotation(Mooring, Quaternion.Euler(0f, 90f, 0f)); }   // ffu53: the empty sub is fetched back to the dock
        if (sub.driver == null)
        {
            f.SendTo(WorldId.Underwater, sub.transform.position + Vector3.up * 2f, sub.transform.eulerAngles.y);
            f.EnterVehicle(sub);
            f.Toast("Diving! L-stick drive, RT up, LT down, A = swim out in scuba gear", 4f);
        }
        else
        {
            f.SendTo(WorldId.Underwater, sub.transform.position + sub.transform.right * 3.5f, sub.transform.eulerAngles.y);
            f.Toast(sub.driver.nick + " has the sub - you're in scuba gear. Find the pearls!", 4f);
        }
        if (roundStart < 0f) roundStart = Time.time;
        FX.Splash(DockSpot, 20);
        Sfx.Play(Sfx.SplashBig, 1f, 0.8f);
    }

    public void Surface(Frog f)
    {
        if (f.vehicle != null) f.ExitVehicle();
        Vector3 d = DockSpot + new Vector3((f.id - 1.5f) * 1.6f, 0f, 0f);
        f.SendTo(WorldId.Ranch, d, 270f);
        f.Toast("Back at the pond dock", 2.5f);
        Sfx.Play(Sfx.SplashBig, 0.9f, 1.2f);
    }

    // ffu15: a scuba froggy swims up through the surface -> floats in the ranch pond above the same spot of the reef
    // (the reef maps onto the pond 1:1 around its centre); hold DOWN / DIVE there to come back down here
    public void SurfaceSwim(Frog f)
    {
        Vector3 rel = f.transform.position - Worlds.UnderO;
        Vector2 c = Layout.PondC, r = Layout.PondR;
        Vector3 spot = Vector3.zero; bool ok = false;
        for (int k = 0; k < 12 && !ok; k++)
        {
            float shrink = 1f - k * 0.08f;
            float x = c.x + Mathf.Clamp(rel.x, -r.x * 0.8f, r.x * 0.8f) * shrink, z = c.y + Mathf.Clamp(rel.z, -r.y * 0.8f, r.y * 0.8f) * shrink;
            float surf, depth;
            if (Water.At(x, z, out surf, out depth) && depth > 1.6f) { spot = new Vector3(x, Layout.WaterY - 0.6f, z); ok = true; }
        }
        if (!ok) spot = DockSpot + new Vector3(6f, Layout.WaterY - 0.6f - DockSpot.y, (f.id - 1.5f) * 1.6f);
        f.SendTo(WorldId.Ranch, spot, f.transform.eulerAngles.y);
        FX.Splash(spot + Vector3.up * 0.6f, 18);
        Sfx.Play(Sfx.SplashBig, 0.8f, 1.2f);
        f.Toast("Surfaced in the pond!  Hold " + (f.inputKind == InputKind.Gamepad ? "B" : f.inputKind == InputKind.Touch ? "DIVE" : "SHIFT") + " to dive back down, or swim to shore", 4f);
        Debug.Log("Scuba: " + f.nick + " surfaced in the pond at (" + spot.x.ToString("0") + ", " + spot.z.ToString("0") + ")");
    }

    // ffu37: the ranch pond spot above a reef-local position (1:1 around the pond centre), pulled in towards the centre
    // until the pond there is at least minDepth deep (same mapping as SurfaceSwim)
    public static Vector3 PondSpot(Vector3 rel, float minDepth, out bool ok)
    {
        Vector2 c = Layout.PondC, r = Layout.PondR;
        for (int k = 0; k < 14; k++)
        {
            float shrink = 1f - k * 0.07f;
            float x = c.x + Mathf.Clamp(rel.x, -r.x * 0.85f, r.x * 0.85f) * shrink, z = c.y + Mathf.Clamp(rel.z, -r.y * 0.85f, r.y * 0.85f) * shrink;
            if (Layout.WaterY - Ranch.GY(x, z) > minDepth) { ok = true; return new Vector3(x, Layout.WaterY, z); }
        }
        ok = false;
        return new Vector3(c.x - 20f, Layout.WaterY, c.y);
    }

    public void DiveFromPond(Frog f)
    {
        if (!built) Build();
        Vector2 c = Layout.PondC;
        Vector3 p = f.transform.position;
        float x = Mathf.Clamp(p.x - c.x, -85f, 85f), z = Mathf.Clamp(p.z - c.y, -85f, 85f);
        // ffu37 merge: one depth truth - the reef is the pond seen from below, so the froggy arrives at the depth it dived
        // to in the pond (ffw pond dive: hold on the floor), just above the seabed there (was: at most 6 m down)
        float y = Mathf.Clamp(p.y - Layout.WaterY, SeaY(x, z) + 1.2f, -2.2f);
        f.SendTo(WorldId.Underwater, Worlds.UnderO + new Vector3(x, y, z), f.transform.eulerAngles.y);
        if (roundStart < 0f) roundStart = Time.time;
        FX.Splash(p + Vector3.up * 0.5f, 16);
        Sfx.Play(Sfx.SplashBig, 0.8f, 0.9f);
        f.Toast("Dive! Back down to the reef - swim up to the top to surface again", 3f);
        Debug.Log("Scuba: " + f.nick + " dived from the pond");
    }

    void OnPearl(Frog f)
    {
        int left = Pickups.Remaining(Group), total = Pickups.Total(Group);
        if (left == 0)
        {
            float t = Time.time - roundStart;
            bool rec = best < 0f || t < best;
            if (rec) best = t;
            Toast("OCEAN EXPLORER! All " + total + " pearls in " + Mathf.RoundToInt(t) + " s" + (rec ? " - new best!" : "") + "  They'll reappear soon.", 5f);
            Sfx.Play(Sfx.Win, 1f);
            Invoke("NewRound", 12f);
        }
        else f.Toast("Pearl! " + (total - left) + " / " + total, 1.8f);
    }

    void NewRound() { Pickups.Reset(Group); roundStart = Time.time; Toast("The pearls are back - go again!", 3f); }

    void Toast(string s, float t)
    {
        if (Game.I == null) return;
        foreach (Frog f in Game.I.frogs) if (f != null && f.human && f.world == WorldId.Underwater) f.Toast(s, t);
    }

    // ---------------- build ----------------
    GameObject P(PrimitiveType t, Vector3 lp, Vector3 s, Color c, bool col = false, Vector3 euler = default(Vector3))
    {
        return Mats.Prim(t, root, L(lp.x, lp.y, lp.z), s, euler, Mats.Lit(c), col);
    }

    void Build()
    {
        built = true;
        root = new GameObject("Underwater").transform;
        Debug.Log("Underwater: ONE SEA - built in the ranch pond at " + Worlds.UnderO + " (ffu53)");
        Seabed();
        Surface();
        var rnd = new System.Random(8);
        Reef(rnd);
        Kelp(rnd);
        Wreck();
        Cave();
        Rocks(rnd);
        foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // graphics overhaul stage B: animated caustics on every lit surface (FF/Underwater), merged afterwards
        int conv = 0;
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) { var nm = Mats.Underwater(mats[i], Worlds.UnderO.y); if (nm != mats[i]) { mats[i] = nm; conv++; } }
            r.sharedMaterials = mats;
        }
        Debug.Log("Underwater: caustics on " + conv + " surfaces");
        MeshMerge.Merge(root, false);
        Motes();
        Shafts();
        Life(rnd);
        Pearls(rnd);
        sub = Submarine.Build(Mooring, 90f);   // ffu53: the real sub is moored at the pond dock (it was a separate copy at the reef)
        if (Perf60.On) GateInit();
        Ranch.Sign(L(-58f, -3f, -14f), 270f, "UNDERWATER\n<size=22>find all the pearls - reef, kelp, shipwreck, cave</size>", new Color(0.05f, 0.3f, 0.55f), 11f, 2.6f);   // 7 m wrapped "UNDERWAT/ER"
    }

    void GateInit()
    {
        var sea = new List<Renderer>(); var life = new List<Renderer>();
        foreach (var r in root.GetComponentsInChildren<Renderer>(true)) if (!(r is ParticleSystemRenderer) && !shafts.Contains(r)) sea.Add(r);
        foreach (var s in schools) foreach (var t in s.fish) if (t != null) life.AddRange(t.GetComponentsInChildren<Renderer>(true));
        foreach (var w in swimmers) if (w.t != null) life.AddRange(w.t.GetComponentsInChildren<Renderer>(true));
        foreach (var k in kelp) if (k != null) life.AddRange(k.GetComponentsInChildren<Renderer>(true));
        gateSea = sea.ToArray(); gateLife = life.ToArray();
        foreach (var r in gateSea) owned.Add(r.GetHashCode());
        foreach (var r in gateLife) owned.Add(r.GetHashCode());
        GateSea = gateSea.Length; GateLife = gateLife.Length;
        U6Cam.PreCull += Gate;
        Debug.Log("PERF60 sea gate: " + gateSea.Length + " sea + " + gateLife.Length + " sea-life renderers draw only for cameras near / under the pond");
    }

    static void Gate(Camera c)
    {
        var u = I;
        if (u == null || u.gateSea == null || c == null) return;
        bool sea = false, life = false;
        if (c.cameraType == CameraType.Game && (c.cullingMask & 1) != 0)
        {
            Vector3 p = c.transform.position;
            float q = Layout.PondQ(p.x, p.z), h = p.y - Layout.WaterY;
            bool under = h < 0.35f && q < 1.12f;
            sea = under || (q < 1.5f && h < 24f);
            life = sea;   // ffu60d: the jellies / fish show through the water from the shore too (60a hid them above 9 m)
        }
        if (life) u.lifeFrame = Time.frameCount;
        if (sea != u.seaShown) { u.seaShown = sea; foreach (var r in u.gateSea) if (r != null) r.forceRenderingOff = !sea; }
        if (life != u.lifeShown) { u.lifeShown = life; foreach (var r in u.gateLife) if (r != null) r.forceRenderingOff = !life; }
    }

    // ffu53: the sand lies ON the ranch pond bed (0.12 m up, no collider: the terrain is the floor), deep part of the pond only
    void Seabed()
    {
        Vector2 R = Layout.PondR; const int nx = 80, nz = 56;
        var v = new Vector3[(nx + 1) * (nz + 1)]; var uv = new Vector2[v.Length]; var ok = new bool[v.Length];
        for (int z = 0; z <= nz; z++)
            for (int x = 0; x <= nx; x++)
            {
                float lx = -R.x + x * 2f * R.x / nx, lz = -R.y + z * 2f * R.y / nz;
                float q = (lx / R.x) * (lx / R.x) + (lz / R.y) * (lz / R.y), y = SeaY(lx, lz);
                int i = z * (nx + 1) + x;
                ok[i] = q < 0.8f && y < -1.5f;
                v[i] = L(lx, y + 0.12f, lz); uv[i] = new Vector2(lx / 6f, lz / 6f);
            }
        var tri = new List<int>();
        for (int z = 0; z < nz; z++)
            for (int x = 0; x < nx; x++)
            {
                int a = z * (nx + 1) + x, b = a + 1, c = a + nx + 1, d = c + 1;
                if (!(ok[a] && ok[b] && ok[c] && ok[d])) continue;
                tri.Add(a); tri.Add(c); tri.Add(b); tri.Add(b); tri.Add(c); tri.Add(d);
            }
        var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.vertices = v; mesh.uv = uv; mesh.triangles = tri.ToArray();
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var go = new GameObject("Seabed");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var sb = Resources.Load<Texture2D>("LB/seabed");
        Material sm;
        if (sb != null) { sm = Mats.Tex(sb, 0.05f); sm.color = new Color(0.95f, 0.92f, 0.82f); }
        else sm = Mats.Lit(new Color(0.78f, 0.72f, 0.55f));
        var smr = go.AddComponent<MeshRenderer>(); smr.sharedMaterial = sm;
        smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.transform.SetParent(root, true);
        Debug.Log("Underwater: sand on the ranch pond bed, " + tri.Count / 3 + " tris");
    }

    // ffu53: the water surface seen from below = an ellipse the size of the pond, facing down, just under the ranch water
    void Surface()
    {
        var m = new Material(Mats.Water);
        m.color = new Color(0.55f, 0.85f, 0.95f, 0.55f);
        const int n = 48; Vector2 R = Layout.PondR * 0.99f;
        var v = new Vector3[n + 1]; var tri = new int[n * 3];
        v[0] = L(0f, -0.04f, 0f);
        for (int i = 0; i < n; i++) { float a = i * Mathf.PI * 2f / n; v[i + 1] = L(Mathf.Cos(a) * R.x, -0.04f, Mathf.Sin(a) * R.y); }
        for (int i = 0; i < n; i++) { tri[i * 3] = 0; tri[i * 3 + 1] = i + 1; tri[i * 3 + 2] = (i + 1) % n + 1; }
        var mesh = new Mesh(); mesh.vertices = v; mesh.triangles = tri; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var s = new GameObject("SurfaceFromBelow");
        s.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = s.AddComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        s.transform.SetParent(root, true);
    }

    void Reef(System.Random r)
    {
        Color[] cols = { new Color(1f, 0.45f, 0.5f), new Color(1f, 0.6f, 0.2f), new Color(0.6f, 0.35f, 0.9f), new Color(0.95f, 0.85f, 0.3f), new Color(0.3f, 0.85f, 0.75f), new Color(0.95f, 0.3f, 0.3f), new Color(0.4f, 0.6f, 1f) };
        for (int i = 0; i < 170; i++)
        {
            float a = (float)r.NextDouble() * 6.283f, d = Mathf.Sqrt((float)r.NextDouble()) * 40f;
            float x = Mathf.Cos(a) * d, z = Mathf.Sin(a) * d, y = SeaY(x, z);
            if (y > -4.5f) continue;   // ffu37: not on the island tops (they reach 2.5 m under the surface)
            Color c = cols[r.Next(cols.Length)];
            float s = 0.6f + (float)r.NextDouble() * 1.8f;
            switch (i % 5)
            {
                case 0: P(PrimitiveType.Sphere, new Vector3(x, y + s * 0.3f, z), new Vector3(s * 1.4f, s * 0.8f, s * 1.3f), c, true); break;      // brain coral
                case 1:   // branching coral
                    for (int b = 0; b < 4; b++) P(PrimitiveType.Cylinder, new Vector3(x + (b - 1.5f) * 0.3f * s, y + s * 0.6f, z + (b % 2) * 0.3f), new Vector3(0.22f * s, s * 0.7f, 0.22f * s), c, false, new Vector3((b - 1.5f) * 18f, 0f, (b % 2 - 0.5f) * 30f));
                    break;
                case 2: P(PrimitiveType.Cube, new Vector3(x, y + s, z), new Vector3(s * 1.8f, s * 2f, 0.08f), c, false, new Vector3(0f, a * 57f, 0f)); break;   // sea fan
                case 3:   // tube sponge cluster
                    for (int b = 0; b < 3; b++) P(PrimitiveType.Cylinder, new Vector3(x + b * 0.5f, y + s * 0.5f, z + (b % 2) * 0.4f), new Vector3(0.45f, s * 0.5f + b * 0.2f, 0.45f), c);
                    break;
                default:  // anemone
                    P(PrimitiveType.Sphere, new Vector3(x, y + 0.25f, z), new Vector3(0.9f, 0.5f, 0.9f), c);
                    for (int b = 0; b < 6; b++) P(PrimitiveType.Cylinder, new Vector3(x + Mathf.Cos(b) * 0.3f, y + 0.6f, z + Mathf.Sin(b) * 0.3f), new Vector3(0.08f, 0.3f, 0.08f), Color.Lerp(c, Color.white, 0.4f));
                    break;
            }
        }
        // starfish + shells on the sand
        for (int i = 0; i < 40; i++)
        {
            float x = ((float)r.NextDouble() - 0.5f) * 150f, z = ((float)r.NextDouble() - 0.5f) * 105f, y = SeaY(x, z);
            if (y > -2f) continue;   // ffu53: in the pond only
            Color c = i % 2 == 0 ? new Color(1f, 0.45f, 0.2f) : new Color(0.95f, 0.85f, 0.7f);
            for (int k = 0; k < 5; k++) P(PrimitiveType.Cube, new Vector3(x, y + 0.05f, z) + Quaternion.Euler(0f, k * 72f, 0f) * Vector3.forward * 0.18f, new Vector3(0.12f, 0.06f, 0.36f), c, false, new Vector3(0f, k * 72f, 0f));
        }
    }

    void Kelp(System.Random r)
    {
        var km = new Material(Mats.Lit(new Color(0.2f, 0.5f, 0.2f)));
        for (int i = 0; i < 46; i++)
        {
            float x = -40f + ((float)r.NextDouble() - 0.5f) * 36f, z = -35f + ((float)r.NextDouble() - 0.5f) * 36f, y = SeaY(x, z);
            float h = 6f + (float)r.NextDouble() * 10f;
            h = Mathf.Min(h, -1.5f - y);   // ffu37: kelp on an island cone stays under the surface
            if (h < 2f) continue;
            var k = new GameObject("Kelp").transform;
            k.position = L(x, y, z);
            for (int s = 0; s < 5; s++)
            {
                var seg = Mats.Prim(PrimitiveType.Cube, k, new Vector3(0f, s * h / 5f + h / 10f, 0f), new Vector3(0.5f - s * 0.05f, h / 5f, 0.08f), km);
                seg.transform.localRotation = Quaternion.Euler(0f, s * 25f, 0f);
            }
            Mats.NoShadows(k.gameObject);
            k.SetParent(transform, true);
            kelp.Add(k);
        }
    }

    void Wreck()
    {
        Vector3 c = new Vector3(-30f, 0f, 30f);   // ffu53: inside the pond basin (was (55, 40) = under the ranch bank)
        float y = SeaY(c.x, c.z);
        Color wood = new Color(0.36f, 0.26f, 0.16f), dark = new Color(0.22f, 0.16f, 0.1f);
        Vector3 e = new Vector3(0f, 25f, 14f);   // tilted on its side a bit
        var hull = new GameObject("Wreck").transform;
        hull.SetParent(root, false);
        hull.position = L(c.x, y + 2f, c.z);
        hull.rotation = Quaternion.Euler(e);
        System.Action<Vector3, Vector3, Color, bool> B = (p, s, col, collide) => Mats.Prim(PrimitiveType.Cube, hull, p, s, Mats.Lit(col), collide);
        B(new Vector3(0f, -1.2f, 0f), new Vector3(5f, 0.6f, 20f), dark, true);           // keel / floor
        B(new Vector3(-2.6f, 0.6f, 0f), new Vector3(0.4f, 3.6f, 20f), wood, true);        // port side
        B(new Vector3(2.6f, 0.6f, -4f), new Vector3(0.4f, 3.6f, 12f), wood, true);        // starboard (broken: a hole to swim in)
        B(new Vector3(2.6f, -0.4f, 7f), new Vector3(0.4f, 1.6f, 6f), wood, true);
        B(new Vector3(0f, 0.6f, 10.2f), new Vector3(5.2f, 3.6f, 0.4f), wood, true);       // bow
        B(new Vector3(0f, 0.6f, -10.2f), new Vector3(5.2f, 3.6f, 0.4f), wood, true);      // stern
        B(new Vector3(0f, 2.5f, -6f), new Vector3(5f, 0.3f, 8f), wood, true);             // half deck
        B(new Vector3(0f, 3.8f, -7f), new Vector3(3f, 2.4f, 3.5f), dark, true);           // cabin
        Mats.Prim(PrimitiveType.Cylinder, hull, new Vector3(0f, 4.5f, 1f), new Vector3(0.4f, 2.5f, 0.4f), Mats.Lit(dark), true);   // broken mast (53b: shorter - it stays under the pond surface)
        Mats.Prim(PrimitiveType.Cylinder, hull, new Vector3(4f, -0.6f, 3f), new Vector3(0.35f, 3f, 0.35f), Vector3.zero + new Vector3(0f, 0f, 80f), Mats.Lit(dark), false);
        // treasure chest inside
        B(new Vector3(0f, -0.4f, 4f), new Vector3(1.4f, 0.9f, 0.9f), new Color(0.55f, 0.35f, 0.15f), true);
        // ffu53 six-year-old idea: the treasure chest OPENS when anyone (scuba, sub, a sunk truck, a mech) comes close
        chestPos = hull.TransformPoint(new Vector3(0f, -0.4f, 4f));
        var hinge = new GameObject("ChestLid").transform; hinge.SetParent(transform, false);
        hinge.position = hull.TransformPoint(new Vector3(0f, 0.05f, 3.55f)); hinge.rotation = hull.rotation;
        Mats.Prim(PrimitiveType.Cube, hinge, new Vector3(0f, 0.15f, 0.45f), new Vector3(1.45f, 0.3f, 0.95f), Mats.Lit(new Color(0.85f, 0.7f, 0.2f)), false);
        var gold = Mats.Unlit(new Color(1f, 0.82f, 0.25f));
        for (int k = 0; k < 7; k++) { var coin = Mats.Prim(PrimitiveType.Cylinder, transform, hull.TransformPoint(new Vector3(-0.45f + k * 0.15f, 0.08f + (k % 2) * 0.06f, 4f + (k % 3 - 1) * 0.2f)), new Vector3(0.22f, 0.02f, 0.22f), gold); coin.transform.rotation = hull.rotation * Quaternion.Euler(10f * k, 0f, 8f); }
        chestLid = hinge; baseLid = hinge.localRotation;
        Ranch.Sign(L(c.x - 8f, y + 6f, c.z - 12f), 220f, "SHIPWRECK", new Color(0.35f, 0.25f, 0.12f), 4f, 1f);
    }

    void Cave()
    {
        // rock tunnel along x at (-58, z 30): rings of boulders, open at both ends, glowing crystals inside
        Color rock = new Color(0.32f, 0.32f, 0.35f);
        float cz = -2f;   // ffu53: inside the pond (was x -72..-43, z 30 = under the bank), mouth towards the dock
        // 53b: on the real pond bed the west end is a shallow shelf - start the tunnel where all of it stays under water
        float x0 = -64f;
        for (; x0 < -30f; x0 += 2f)
        {
            bool deep = true;
            for (int k = 0; k <= 12 && deep; k++) if (SeaY(x0 + k * 2.4f, cz) + 8.3f > -2f) deep = false;
            if (deep) break;
        }
        caveX0 = x0;
        Debug.Log("Underwater: cave tunnel x " + x0.ToString("0") + ".." + (x0 + 28.8f).ToString("0") + " (pond-local, z " + cz + ")");
        for (int k = 0; k <= 12; k++)
        {
            float x = x0 + k * 2.4f;
            float y0 = SeaY(x, cz);
            for (int j = 0; j < 9; j++)
            {
                float a = Mathf.PI * (j / 8f);   // half ring: left wall, roof, right wall
                Vector3 p = new Vector3(x, y0 + Mathf.Sin(a) * 6.5f, cz + Mathf.Cos(a) * 6.5f);
                P(PrimitiveType.Sphere, p, new Vector3(3.2f, 3f, 3.2f), Color.Lerp(rock, Color.black, (k + j) % 3 * 0.12f), true);
            }
            if (k % 3 == 1)
            {
                Color glow = k % 2 == 0 ? new Color(0.3f, 1f, 0.9f) : new Color(0.75f, 0.4f, 1f);
                Mats.Prim(PrimitiveType.Cube, root, L(x, y0 + 0.6f, cz + 3.5f), new Vector3(0.4f, 1.2f, 0.4f), new Vector3(10f, 30f, 15f), Mats.Unlit(glow), false);
                Mats.Prim(PrimitiveType.Cube, root, L(x + 0.4f, y0 + 0.4f, cz - 3.6f), new Vector3(0.3f, 0.8f, 0.3f), new Vector3(-12f, 10f, -18f), Mats.Unlit(glow), false);
            }
        }
        Ranch.Sign(L(x0 - 2f, Mathf.Min(SeaY(x0 - 2f, cz) + 8.5f, -2f), cz), 270f, "CAVE", new Color(0.2f, 0.2f, 0.3f), 3f, 1f);
    }

    void Rocks(System.Random r)
    {
        for (int i = 0; i < 40; i++)
        {
            float x = ((float)r.NextDouble() - 0.5f) * 180f, z = ((float)r.NextDouble() - 0.5f) * 180f;
            if (new Vector2(x, z).magnitude < 30f) continue;
            float s = 1.2f + (float)r.NextDouble() * 3f;
            if (SeaY(x, z) > -6f) continue;   // ffu37: not poking out of an island top
            P(PrimitiveType.Sphere, new Vector3(x, SeaY(x, z) + s * 0.2f, z), new Vector3(s * 1.6f, s, s * 1.3f), new Color(0.35f, 0.36f, 0.38f), true, new Vector3(0f, (float)r.NextDouble() * 180f, 0f));
        }
    }

    // drifting marine snow + tiny bubbles around the reef (one particle system, fewer on phones)
    void Motes()
    {
        var go = new GameObject("Motes");
        go.transform.SetParent(root, false);
        go.transform.position = L(0f, -12f, 0f); motes = go.transform;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 18f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.95f, 1f, 0.55f), new Color(1f, 1f, 0.9f, 0.25f));
        main.maxParticles = Look.Mobile ? 500 : 1400;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.002f;
        main.prewarm = true;
        var em = ps.emission; em.rateOverTime = Look.Mobile ? 35f : 95f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(130f, 16f, 90f);
        var nz = ps.noise; nz.enabled = true; nz.strength = 0.15f; nz.frequency = 0.25f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var pr = go.GetComponent<ParticleSystemRenderer>();
        var m = new Material(Mats.Fx);
        m.mainTexture = SoftDot();
        pr.sharedMaterial = m;
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
    }

    static Texture2D SoftDot()
    {
        const int n = 32;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = new Vector2(x - n * 0.5f + 0.5f, y - n * 0.5f + 0.5f).magnitude / (n * 0.5f);
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d));
            }
        t.SetPixels(px); t.Apply(true);
        return t;
    }

    void Shafts()
    {
        var m = new Material(Mats.Fx);
        m.color = new Color(0.75f, 0.95f, 1f, 0.07f);
        for (int i = 0; i < 9; i++)
        {
            var s = Mats.Prim(PrimitiveType.Cube, root, L((i - 4) * 14f, -9f, (i % 2 == 0 ? 14f : -12f)), new Vector3(2.2f, 16f, 2.2f), new Vector3(0f, i * 20f, (i - 4) * 2.5f), m, false);
            var sr = s.GetComponent<Renderer>();
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shafts.Add(sr);
        }
        U6Cam.PreCull += ShaftGate;
    }

    // ffu6e: the light shafts are only for a diver - seen from above the surface (shore, boats, the pond race) they were pale
    // glass slabs standing in the water (2022.3 too). Per camera, like the sea gate.
    readonly List<Renderer> shafts = new List<Renderer>(); bool shaftsShown = true;
    static void ShaftGate(Camera c)
    {
        var u = I;
        if (u == null || u.shafts.Count == 0 || c == null || c.cameraType != CameraType.Game || (c.cullingMask & 1) == 0) return;
        Vector3 p = c.transform.position;
        bool under = p.y - Layout.WaterY < 0.0f && Layout.PondQ(p.x, p.z) < 1.12f;
        if (under != u.shaftsShown) { u.shaftsShown = under; foreach (var r in u.shafts) if (r != null) r.forceRenderingOff = !under; }
    }

    void Life(System.Random r)
    {
        Color[] fc = { new Color(1f, 0.8f, 0.1f), new Color(0.1f, 0.65f, 1f), new Color(1f, 0.45f, 0.1f), new Color(0.6f, 0.95f, 0.6f), new Color(1f, 0.5f, 0.6f), new Color(0.7f, 0.7f, 1f) };
        for (int s = 0; s < 6; s++)
        {
            var sc = new School { seed = s * 13.7f, c = L(((float)r.NextDouble() - 0.5f) * 80f, -10f, ((float)r.NextDouble() - 0.5f) * 50f) };
            Material m = Mats.Shiny(fc[s]);
            for (int i = 0; i < 16; i++)
            {
                float sz = 0.25f + (float)r.NextDouble() * 0.15f;
                var g = Mats.Prim(PrimitiveType.Sphere, transform, sc.c, new Vector3(sz * 0.45f, sz * 0.6f, sz * 1.4f), m);
                g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sc.fish.Add(g.transform);
                sc.off.Add(new Vector3(((float)r.NextDouble() - 0.5f) * 5f, ((float)r.NextDouble() - 0.5f) * 2.4f, ((float)r.NextDouble() - 0.5f) * 5f));
            }
            schools.Add(sc);
        }
        // turtles
        for (int i = 0; i < 4; i++)
        {
            var t = new GameObject("Turtle").transform; t.SetParent(transform, false);
            Mats.Prim(PrimitiveType.Sphere, t, Vector3.zero, new Vector3(1.4f, 0.55f, 1.7f), Mats.Lit(new Color(0.25f, 0.45f, 0.25f)));
            Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 0f, 1f), new Vector3(0.45f, 0.4f, 0.5f), Mats.Lit(new Color(0.55f, 0.6f, 0.4f)));
            for (int f = 0; f < 4; f++) Mats.Prim(PrimitiveType.Cube, t, new Vector3(f % 2 == 0 ? -0.8f : 0.8f, -0.05f, f < 2 ? 0.5f : -0.6f), new Vector3(0.7f, 0.08f, 0.35f), Mats.Lit(new Color(0.55f, 0.6f, 0.4f)));
            Mats.NoShadows(t.gameObject);
            swimmers.Add(new Swimmer { t = t, a = i * 1.6f, r = 20f + i * 8f, sp = 0.12f, y = -9f - i, c = L(0f, 0f, 0f) });
        }
        // rays
        for (int i = 0; i < 3; i++)
        {
            var t = new GameObject("Ray").transform; t.SetParent(transform, false);
            Mats.Prim(PrimitiveType.Sphere, t, Vector3.zero, new Vector3(3f, 0.25f, 2f), Mats.Lit(new Color(0.35f, 0.38f, 0.45f)));
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0f, -1.8f), new Vector3(0.08f, 0.08f, 2f), Mats.Lit(new Color(0.3f, 0.3f, 0.35f)));
            Mats.NoShadows(t.gameObject);
            swimmers.Add(new Swimmer { t = t, a = i * 2.1f, r = 18f + i * 8f, sp = 0.16f, y = -7f - i * 2f, c = L(10f, 0f, -10f) });
        }
        // jellies
        var jm = new Material(Mats.Glass); jm.color = new Color(1f, 0.6f, 0.85f, 0.45f);
        for (int i = 0; i < 10; i++)
        {
            var t = new GameObject("Jelly").transform; t.SetParent(transform, false);
            Mats.Prim(PrimitiveType.Sphere, t, Vector3.zero, new Vector3(0.9f, 0.5f, 0.9f), jm);
            for (int k = 0; k < 5; k++) Mats.Prim(PrimitiveType.Cube, t, new Vector3(Mathf.Cos(k) * 0.2f, -0.55f, Mathf.Sin(k) * 0.2f), new Vector3(0.03f, 0.9f, 0.03f), jm);
            swimmers.Add(new Swimmer { t = t, a = i * 0.7f, r = 6f + i * 4f, sp = 0.05f, y = -4f - (i % 4) * 2.5f, c = L(-20f + i * 5f, 0f, 20f - i * 3f), bob = true });
        }
        // a whale far out, slowly circling
        {
            var t = new GameObject("Whale").transform; t.SetParent(transform, false);
            Mats.Prim(PrimitiveType.Capsule, t, Vector3.zero, new Vector3(4f, 7f, 3.6f), new Vector3(90f, 0f, 0f), Mats.Lit(new Color(0.25f, 0.32f, 0.45f)));
            Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0f, -0.9f, 1f), new Vector3(3.4f, 1.6f, 9f), Mats.Lit(new Color(0.85f, 0.85f, 0.88f)));
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0f, -7.4f), new Vector3(5f, 0.3f, 1.6f), Mats.Lit(new Color(0.25f, 0.32f, 0.45f)));
            for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, t, new Vector3(2.4f * s, -1f, 2f), new Vector3(2.8f, 0.2f, 1.2f), new Vector3(0f, 20f * s, -25f * s), Mats.Lit(new Color(0.25f, 0.32f, 0.45f)));
            Mats.NoShadows(t.gameObject);
            swimmers.Add(new Swimmer { t = t, a = 0f, r = 52f, zs = 0.62f, sp = 0.035f, y = -12f, c = L(0f, 0f, 0f) });
        }
    }

    void Pearls(System.Random r)
    {
        var spots = new List<Vector3>();
        for (int i = 0; i < 8; i++) { float a = i * 0.785f; float x = Mathf.Cos(a) * (12f + i * 3f), z = Mathf.Sin(a) * (12f + i * 3f); spots.Add(new Vector3(x, SeaY(x, z) + 1.5f, z)); }
        for (int i = 0; i < 5; i++) { float x = -40f + (i - 2) * 6f, z = -35f + (i % 2) * 8f; spots.Add(new Vector3(x, SeaY(x, z) + 3f + i, z)); }
        // ffu53: wreck / cave / deep pearls moved with them into the pond basin
        spots.Add(new Vector3(-30f, SeaY(-30f, 34f) + 2.2f, 34f));
        spots.Add(new Vector3(-30f, SeaY(-30f, 26f) + 4f, 26f));
        spots.Add(new Vector3(-35f, SeaY(-35f, 20f) + 6f, 20f));
        spots.Add(new Vector3(-23f, SeaY(-23f, 36f) + 3f, 36f));
        for (int i = 0; i < 5; i++) { float x = caveX0 + 2f + i * 5f; spots.Add(new Vector3(x, SeaY(x, -2f) + 1.6f, -2f)); }
        spots.Add(new Vector3(0f, SeaY(0f, -40f) + 2f, -40f));
        spots.Add(new Vector3(30f, SeaY(30f, -38f) + 2f, -38f));
        var holder = new GameObject("Pearls").transform; holder.SetParent(transform, false);
        var pm = Mats.Shiny(new Color(0.97f, 0.95f, 1f));
        var gm = Mats.Unlit(new Color(1f, 0.85f, 0.3f));
        for (int i = 0; i < spots.Count; i++)
        {
            Vector3 s = spots[i];
            var g = Mats.Prim(PrimitiveType.Sphere, holder, L(s.x, s.y, s.z), Vector3.one * (i % 6 == 0 ? 0.7f : 0.45f), i % 6 == 0 ? gm : pm);
            // open clam shell under each pearl
            var shell = Mats.Prim(PrimitiveType.Sphere, g.transform, new Vector3(0f, -0.8f, 0f), new Vector3(2.4f, 0.6f, 2f), Mats.Lit(new Color(0.75f, 0.55f, 0.65f)));
            Pickups.Add(g.transform, WorldId.Underwater, Group, 1.8f);
        }
    }

    // ---------------- update ----------------
    // ffu53: under the pond surface, whatever the mode (scuba / sub = Underwater; truck, mech, swimmer = Ranch)
    public static bool UnderPond(Frog f, out Vector3 p)
    {
        p = f.vehicle != null ? f.vehicle.transform.position : f.Center;
        if (f.world == WorldId.Underwater) return true;
        return f.world == WorldId.Ranch && p.y < Layout.WaterY - 0.4f && Layout.InPond(p.x, p.z);
    }

    void Update()
    {
        if (!built)
        {
            // ffu53: one sea - built once the ranch terrain exists (it is the pond bed), always there for every entry
            if (Game.I != null && Time.timeSinceLevelLoad > 1.5f && Ranch.GY(Worlds.UnderO.x, Worlds.UnderO.z) < -3f) Build();
            return;
        }
        bool anyone = false;
        Vector3 fp;
        if (Game.I != null) foreach (Frog f in Game.I.frogs) if (f != null && UnderPond(f, out fp)) anyone = true;
        if (motes != null && motes.gameObject.activeSelf != anyone) motes.gameObject.SetActive(anyone);
        if (sub != null && sub.driver == null && (sub.transform.position - Mooring).magnitude < 4f) { sub.rb.velocity = Vector3.zero; sub.rb.position = Mooring; }   // moored: stays put at the dock
        ChestTick();
        float dt = Mathf.Min(Time.deltaTime, 0.05f), t = Time.time;
        if (gateLife != null && Time.frameCount - lifeFrame > 2) return;   // ffu60: no camera sees the sea life - its animation waits
        foreach (School s in schools)
        {
            s.t += dt;
            Vector3 home = L(Mathf.Sin(s.t * 0.05f + s.seed) * 55f, -9f + Mathf.Sin(s.t * 0.13f + s.seed) * 4f, Mathf.Sin(s.t * 0.037f + s.seed * 2f) * 35f);
            Vector3 want = (home - s.c).normalized * 3.2f;
            // scatter from frogs and the sub
            foreach (Frog f in Game.I.frogs)
                if (f != null && UnderPond(f, out fp)) { Vector3 d = s.c - fp; if (d.magnitude < 7f) want += d.normalized * 8f; }
            s.vel = Vector3.MoveTowards(s.vel, want, dt * 4f);
            s.c += s.vel * dt;
            float floor = Worlds.UnderO.y + SeaY(s.c.x - Worlds.UnderO.x, s.c.z - Worlds.UnderO.z) + 2.5f;
            if (s.c.y < floor) s.c.y = floor;
            if (s.c.y > Worlds.UnderO.y - 1.5f) s.c.y = Worlds.UnderO.y - 1.5f;
            s.c = Worlds.UnderO + InPond(s.c - Worlds.UnderO, 0.85f);
            Quaternion rot = s.vel.sqrMagnitude > 0.01f ? Quaternion.LookRotation(s.vel) : Quaternion.identity;
            for (int i = 0; i < s.fish.Count; i++)
            {
                Vector3 o = s.off[i] + new Vector3(Mathf.Sin(t * 1.3f + i), Mathf.Sin(t * 0.9f + i * 2f) * 0.4f, Mathf.Cos(t * 1.1f + i)) * 0.5f;
                s.fish[i].position = s.c + rot * o;
                s.fish[i].rotation = rot * Quaternion.Euler(0f, Mathf.Sin(t * 8f + i) * 12f, 0f);
            }
        }
        foreach (Swimmer w in swimmers)
        {
            w.a += w.sp * dt;
            Vector3 p = w.c + new Vector3(Mathf.Cos(w.a) * w.r, w.y + (w.bob ? Mathf.Sin(t * 0.8f + w.r) * 1.5f : Mathf.Sin(t * 0.3f + w.r) * 0.6f), Mathf.Sin(w.a) * w.r * w.zs);
            p = Worlds.UnderO + InPond(p - Worlds.UnderO, 0.85f);   // ffu53: stay in the pond
            float fl = Worlds.UnderO.y + SeaY(p.x - Worlds.UnderO.x, p.z - Worlds.UnderO.z) + 1.5f;
            p.y = Mathf.Min(Mathf.Max(p.y, fl), Worlds.UnderO.y - 1.2f);
            Vector3 d = p - w.t.position;
            w.t.position = p;
            if (!w.bob && d.sqrMagnitude > 1e-6f) w.t.rotation = Quaternion.LookRotation(d) * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.5f + w.r) * 8f);
            if (w.bob) w.t.localScale = new Vector3(1f + Mathf.Sin(t * 3f + w.r) * 0.12f, 1f - Mathf.Sin(t * 3f + w.r) * 0.12f, 1f + Mathf.Sin(t * 3f + w.r) * 0.12f);
        }
        for (int i = 0; i < kelp.Count; i++) kelp[i].rotation = Quaternion.Euler(Mathf.Sin(t * 0.7f + i) * 6f, i * 37f, Mathf.Cos(t * 0.5f + i) * 6f);
    }

    public static int ChestOpens; public static string ChestBy = "";
    public static Vector3 ChestPos { get { return I != null ? I.chestPos : Vector3.zero; } }
    void ChestTick()
    {
        if (chestLid == null || Game.I == null) return;
        Frog by = null;
        foreach (Frog f in Game.I.frogs)
        {
            if (f == null) continue;
            Vector3 p = f.vehicle != null ? f.vehicle.transform.position : f.Center;
            float r = f.vehicle != null ? 6.5f : 4.5f;
            if ((p - chestPos).sqrMagnitude < r * r) { by = f; break; }
        }
        if (by != null) chestShut = Time.time + 20f;
        bool want = by != null || Time.time < chestShut;
        if (want && !chestOpen)
        {
            chestOpen = true; ChestOpens++; ChestBy = by != null ? by.nick + (by.vehicle != null ? " (" + by.vehicle.Title + ")" : "") : "";
            FX.Sparkle(chestPos + Vector3.up * 0.6f, new Color(1f, 0.85f, 0.3f), 24);
            if (by != null && by.human) { by.Toast("TREASURE! The old chest in the shipwreck is full of gold coins!", 3.5f); Sfx.Play(Sfx.Pickup, 0.8f, 0.8f); }
            Debug.Log("Underwater: the wreck's treasure chest opens for " + ChestBy + " (world " + (by != null ? by.world.ToString() : "-") + ")");
        }
        if (!want && chestOpen) chestOpen = false;
        float ang = chestOpen ? -105f : 0f;
        chestLid.localRotation = Quaternion.Slerp(chestLid.localRotation, baseLid * Quaternion.Euler(ang, 0f, 0f), 1f - Mathf.Exp(-Time.deltaTime * 3f));
    }
    Quaternion baseLid = Quaternion.identity;
}

// The submarine: hovers neutrally buoyant, left stick drives + turns, RT rises, LT sinks, A = swim out.
// At the surface keep rising to pop up at the pond dock (back to the ranch).
public partial class Submarine : Vehicle
{
    float yaw, pitchVis, surfaceT;
    Transform prop;

    public override string[] TouchSet { get { return new[] { "A", null, null, "UP", "DOWN" }; } }
    public override string HelpLine { get { return "L-stick drive + turn | RT / Space up | LT / Shift down | A swim out (scuba) | surface + keep rising = ranch | from deep, hold UP all the way = BREACH!"; } }

    public static Submarine Build(Vector3 pos, float yawDeg)
    {
        var go = new GameObject("Submarine");
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);
        var v = go.AddComponent<Submarine>();
        v.Title = "Submarine";
        v.EnterVerb = "climb into the Submarine";
        v.flyer = true;
        v.engineKind = 9;    // submarine hum
        v.SetupBodyPublic(3000f, new Vector3(0f, 0.2f, 0f), new Vector3(2.6f, 2.6f, 7f), Vector3.zero);
        v.rb.useGravity = false; v.rb.freezeRotation = true; v.rb.drag = 0f;
        v.camDistance = 13f; v.camHeight = 3f;
        v.yaw = yawDeg;
        Transform t = go.transform;
        Material hull = Mats.Shiny(Mats.Hex("#00a5ff")), dark = Mats.Lit(new Color(0.12f, 0.13f, 0.15f));
        Mats.Prim(PrimitiveType.Capsule, t, Vector3.zero, new Vector3(2.6f, 3.6f, 2.6f), new Vector3(90f, 0f, 0f), hull);
        Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 1.5f, -0.6f), new Vector3(1.2f, 1.1f, 2.2f), hull);
        Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 1.1f, 1.5f), new Vector3(1.5f, 1.2f, 1.8f), Mats.Glass);
        Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(0.3f, 2.4f, -0.9f), new Vector3(0.12f, 0.5f, 0.12f), dark);
        for (int s = -1; s <= 1; s += 2)
        {
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(1.6f * s, 0f, -2.6f), new Vector3(1.2f, 0.12f, 0.8f), dark);
            Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0.75f * s, -0.3f, 3.35f), Vector3.one * 0.35f, Mats.Unlit(new Color(1f, 0.97f, 0.8f)));
            for (int k = 0; k < 3; k++) Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(1.28f * s, 0.25f, -1f + k * 1.1f), new Vector3(0.45f, 0.03f, 0.45f), new Vector3(0f, 0f, 90f), Mats.Steel(new Color(0.7f, 0.7f, 0.72f)));
        }
        Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.9f, -3.2f), new Vector3(0.12f, 1.2f, 0.8f), dark);
        v.prop = Mats.Node(t, "Prop", new Vector3(0f, 0f, -3.75f));
        Mats.Prim(PrimitiveType.Cube, v.prop, Vector3.zero, new Vector3(1.4f, 0.12f, 0.05f), Mats.Steel(new Color(0.8f, 0.7f, 0.3f)));
        Mats.Prim(PrimitiveType.Cube, v.prop, Vector3.zero, new Vector3(0.12f, 1.4f, 0.05f), Mats.Steel(new Color(0.8f, 0.7f, 0.3f)));
        v.seat = Mats.Node(t, "Seat", new Vector3(0f, 0.55f, 1.4f));
        v.seatScale = 0.8f;
        Mats.SetLayer(go, VehicleLayer);
        Mats.NoShadows(go);
        return v;
    }

    protected override bool OutOfWorld(Vector3 p)
    {
        if (breach) return false;   // ffu37: in the air over the ranch pond
        Vector3 l = p - Worlds.UnderO;
        return Mathf.Abs(l.x) > 130f || Mathf.Abs(l.z) > 130f || l.y < -60f || l.y > 20f;
    }

    public override void OnEnter()
    {
        base.OnEnter(); yaw = transform.eulerAngles.y; surfaceT = 0f;
        if (driver != null && driver.world == WorldId.Ranch) driver.world = WorldId.Underwater;   // ffu53: one sea - climbing in anywhere in the pond = dive mode
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        float dt = Time.fixedDeltaTime;
        if (breach) { BreachStep(dt); return; }   // ffu37
        bool on = driver != null;
        Vector3 v = rb.velocity;
        Vector3 local = transform.position - Worlds.UnderO;
        float climb = on ? Mathf.Clamp(inp.climb + (inp.hopHeld ? 1f : 0f) - (inp.downHeld ? 1f : 0f), -1f, 1f) : 0f;
        if (on) yaw += inp.move.x * 55f * dt;
        Vector3 fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 wishH = on ? fwd * inp.move.y * 11f : Vector3.zero;
        // ffu37: EMERGENCY BLOW - full UP held (RT / Space / touch UP) for 0.6 s (or with full throttle) blows the ballast:
        // the climb builds from 5 up to ~11.5 m/s, so from deep down the sub hits the surface fast enough to BREACH
        bool full = on && climb > 0.85f && local.y < -2.6f;
        blowT = full ? blowT + dt : 0f;
        float blowWant = full && (blowT > 0.6f || inp.move.y > 0.6f) ? 1f : 0f;
        blowK = Mathf.MoveTowards(blowK, blowWant, dt * (blowWant > blowK ? 0.8f : 2f));
        float wishV = climb * 5f + blowK * 6.5f + (on ? 0f : Mathf.Sin(Time.time * 0.6f) * 0.2f);
        Vector3 hv = Vector3.MoveTowards(new Vector3(v.x, 0f, v.z), wishH, 6f * dt);
        float vy = Mathf.MoveTowards(v.y, wishV, (5f + blowK * 2f) * dt);
        if (on && local.y > -2.4f && vy > BreachMin && UnderwaterWorld.I != null)
        {
            rb.velocity = new Vector3(hv.x, vy, hv.z);
            StartBreach();
            return;
        }
        if (local.y > -1.6f && vy > 0f) { vy = 0f; blowK = 0f; }
        rb.velocity = new Vector3(hv.x, vy, hv.z);
        pitchVis = Mathf.Lerp(pitchVis, Mathf.Clamp(-vy * (3f + blowK * 2.5f), -55f, 30f), dt * 3f);
        rb.MoveRotation(Quaternion.Euler(pitchVis, yaw, -inp.move.x * 5f));
        if (blowK > 0.05f && Random.value < blowK * 0.8f) FX.Bubble(transform.TransformPoint(new Vector3(Random.Range(-1.3f, 1.3f), 0.6f, Random.Range(-2.5f, 2.5f))), 3);
        if (on && driver.human && blowK > 0.02f && blowT - dt < 0.6f && blowT >= 0.6f) driver.Toast("BLOW THE BALLAST! Keep holding UP...", 1.6f);
        // surfacing: at the top and still rising -> pop up at the pond dock (back to the ranch)
        if (on && local.y > -2.2f && climb > 0.3f) surfaceT += dt; else surfaceT = 0f;
        if (on && driver.human && surfaceT > 0.25f && surfaceT - dt <= 0.25f) driver.Toast("Surfacing... keep rising to head back to the ranch", 1.5f);
        if (on && surfaceT > 1.2f && UnderwaterWorld.I != null)
        {
            surfaceT = 0f;
            Frog d = driver;
            UnderwaterWorld.I.Surface(d);
        }
    }

    void Update()
    {
        float sp = Speed + Mathf.Abs(rb.velocity.y);
        if (prop != null) prop.Rotate(Vector3.forward, (driver != null ? 300f + sp * 120f : 0f) * Time.deltaTime, Space.Self);
        if (driver != null && Random.value < 0.15f + sp * 0.05f) FX.Bubble(transform.TransformPoint(new Vector3(0f, 0f, -4f)), 2);
    }
}
