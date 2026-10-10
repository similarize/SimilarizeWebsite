using System.Collections.Generic;
using UnityEngine;

// Landable surfaces from the three.js space update:
// MARS: red ground, a crater, a walk-in cave to the north (corridor + chamber, glow crystals, cave dogs that
//   play and King Germy), 6 lost pups (light beams) that follow whoever finds them - lead them to the Dog pen
//   by the Starship - 10 glow crystals, Curiosity (drivable) with jump ramps east and west, and a dust storm
//   every ~100 s (shelter in the cave). Both done = "Mars mission complete!".
// CALLISTO: icy cratered ground, Jupiter filling the sky, low-gravity floaty hops, 12 ice shards and 3 old
//   beacons (A to switch on) -> "Callisto survey complete!".
// The Starship stands at each landing pad: A there flies you back up to orbit.
public class SurfaceWorlds : MonoBehaviour
{
    public static SurfaceWorlds I;
    public static bool Quiet;   // ffu24: story episode 2 is visiting (no Starship-to-orbit prompts, no dust storms)
    Transform marsRoot, calRoot;
    bool marsBuilt, calBuilt, marsDone, calDone;
    readonly List<Animal> pups = new List<Animal>();
    readonly List<Transform> beams = new List<Transform>();
    readonly List<Animal> caveDogs = new List<Animal>();
    readonly bool[] pupSafe = new bool[6];
    Vector3 penC;
    float stormT = 60f, storm;
    readonly bool[] beaconOn = new bool[3];
    readonly List<Renderer> beaconLights = new List<Renderer>();
    Material beaconOff, beaconGreen;

    public static Vector3 M(float x, float y, float z) { return Worlds.MarsO + new Vector3(x, y, z); }
    public static Vector3 C(float x, float y, float z) { return Worlds.CallistoO + new Vector3(x, y, z); }

    public static float MarsY(float x, float z)
    {
        float y = (Mathf.PerlinNoise(x * 0.03f + 2f, z * 0.03f + 5f) - 0.5f) * 5f + (Mathf.PerlinNoise(x * 0.12f, z * 0.12f) - 0.5f) * 0.8f;
        float d = new Vector2(x - 22f, z + 8f).magnitude;               // crater east of the pad
        y += Mathf.Exp(-Mathf.Pow((d - 14f) / 3f, 2f)) * 2.5f - Mathf.Clamp01(1f - d / 13f) * 3.5f;
        float flat = Mathf.Clamp01(new Vector2(x, z + 30f).magnitude / 14f);  // landing pad area
        y *= flat;
        float cave = Mathf.Clamp01(1f - Mathf.Abs(x) / 16f) * Mathf.Clamp01((z - 30f) / 6f);   // flatten the cave floor
        y *= 1f - cave;
        float edge = new Vector2(x, z).magnitude;
        y += Mathf.SmoothStep(0f, 1f, (edge - 80f) / 14f) * 18f;
        return y;
    }

    public static float CalY(float x, float z)
    {
        float y = (Mathf.PerlinNoise(x * 0.025f + 9f, z * 0.025f + 1f) - 0.5f) * 4f;
        Vector3[] craters = { new Vector3(20f, 10f, 9f), new Vector3(-25f, 20f, 7f), new Vector3(-10f, -30f, 11f), new Vector3(35f, -25f, 6f) };
        foreach (Vector3 c in craters)
        {
            float d = new Vector2(x - c.x, z - c.y).magnitude;
            y += Mathf.Exp(-Mathf.Pow((d - c.z) / 2.2f, 2f)) * 1.6f - Mathf.Clamp01(1f - d / c.z) * 2.6f;
        }
        y *= Mathf.Clamp01(new Vector2(x, z - 0f).magnitude / 10f);
        float edge = new Vector2(x, z).magnitude;
        y += Mathf.SmoothStep(0f, 1f, (edge - 70f) / 12f) * 16f;
        return y;
    }

    public static void Create()
    {
        var go = new GameObject("SurfaceWorlds");
        I = go.AddComponent<SurfaceWorlds>();
        Pickups.Listen((it, f) =>
        {
            if (it.group == "crystals") I.CheckMars(f, "Glow crystal! " + (10 - Pickups.Remaining("crystals")) + " / 10");
            if (it.group == "shards") I.CheckCal(f, "Ice shard! " + (12 - Pickups.Remaining("shards")) + " / 12");
        });
    }

    public static void LandMars(Frog f, int k)
    {
        if (!I.marsBuilt) I.BuildMars();
        f.SendTo(WorldId.Mars, M(-4f + k * 2.2f, MarsY(-4f + k * 2.2f, -24f) + 0.4f, -24f), 0f);
        if (!I.marsDone) f.Toast("MARS: 6 lost pups (follow the light beams) need the Dog pen by the Starship - 10 glow crystals - Curiosity has jump ramps - the cave is north", 6f);
    }

    public static void LandCallisto(Frog f, int k)
    {
        if (!I.calBuilt) I.BuildCallisto();
        f.SendTo(WorldId.Callisto, C(-4f + k * 2.2f, CalY(-4f + k * 2.2f, 6f) + 0.4f, 6f), 0f);
        if (!I.calDone) f.Toast("CALLISTO: low gravity - 12 ice shards - switch on the 3 old beacons (A)", 5f);
    }

    // ---------------- shared ----------------
    static GameObject Ground(string name, System.Func<float, float, float> h, Vector3 o, float size, int n, Color c1, Color c2, int seed)
    {
        var v = new Vector3[(n + 1) * (n + 1)]; var uv = new Vector2[v.Length];
        for (int z = 0; z <= n; z++)
            for (int x = 0; x <= n; x++)
            {
                float lx = -size * 0.5f + x * size / n, lz = -size * 0.5f + z * size / n;
                v[z * (n + 1) + x] = o + new Vector3(lx, h(lx, lz), lz);
                uv[z * (n + 1) + x] = new Vector2(lx / 8f, lz / 8f);
            }
        var tri = new int[n * n * 6]; int k = 0;
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++) { int a = z * (n + 1) + x, b = a + 1, cc = a + n + 1, d = cc + 1; tri[k++] = a; tri[k++] = cc; tri[k++] = b; tri[k++] = b; tri[k++] = cc; tri[k++] = d; }
        var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.vertices = v; mesh.uv = uv; mesh.triangles = tri; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        // stage B: Poly Haven CC0 ground textures (red_sand on Mars, moon_01 on Callisto), tinted to the old palette
        var gt = Resources.Load<Texture2D>(name.StartsWith("Mars") ? "LB/marsground" : "LB/calground");
        if (gt != null)
        {
            var gm = Mats.Tex(gt, 0.06f); gm.color = Color.Lerp(Color.Lerp(c1, c2, 0.4f) * 1.6f, Color.white, 0.45f);
            go.AddComponent<MeshRenderer>().sharedMaterial = gm;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }
        var tex = new Texture2D(64, 64, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
        var r = new System.Random(seed); var px = new Color32[64 * 64];
        for (int i = 0; i < px.Length; i++) px[i] = Color.Lerp(c1, c2, (float)r.NextDouble() * 0.6f + Mathf.PerlinNoise((i % 64) * 0.1f, (i / 64) * 0.1f) * 0.4f);
        tex.SetPixels32(px); tex.Apply(true);
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = new Material(Mats.Lit(Color.white)) { mainTexture = tex };
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        return go;
    }

    static void Rocket(Transform parent, Vector3 p)
    {
        Material steel = Mats.Steel(new Color(0.78f, 0.79f, 0.8f)), tile = Mats.Lit(new Color(0.12f, 0.12f, 0.13f));
        Mats.Prim(PrimitiveType.Cylinder, parent, p + Vector3.up * 8f, new Vector3(3.6f, 6f, 3.6f), steel, true);
        Mats.Prim(PrimitiveType.Cylinder, parent, p + new Vector3(0.25f, 8f, 0f), new Vector3(3.3f, 5.9f, 3.3f), tile);
        Mats.Prim(PrimitiveType.Sphere, parent, p + Vector3.up * 14.3f, new Vector3(3.6f, 5.4f, 3.6f), steel);
        for (int k = 0; k < 3; k++) Mats.Prim(PrimitiveType.Cube, parent, p + Quaternion.Euler(0f, k * 120f, 0f) * new Vector3(0f, 1.2f, 2.3f), new Vector3(0.3f, 2.4f, 1.4f), tile);
        Mats.Prim(PrimitiveType.Cylinder, parent, p + Vector3.up * 0.05f, new Vector3(9f, 0.05f, 9f), Mats.Lit(new Color(0.4f, 0.4f, 0.42f)));
    }

    static Transform Beam(Transform parent, Vector3 p, Color c)
    {
        var m = new Material(Mats.Fx); m.color = c;
        var g = Mats.Prim(PrimitiveType.Cylinder, parent, p + Vector3.up * 15f, new Vector3(0.9f, 15f, 0.9f), m);
        g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g.transform;
    }

    void Toast(WorldId w, string s, float t)
    {
        if (Game.I == null) return;
        foreach (Frog f in Game.I.frogs) if (f != null && f.human && f.world == w) f.Toast(s, t);
    }

    public void EnsureMars() { if (!marsBuilt) BuildMars(); }           // ffu20: robot missions land here
    public void EnsureCallisto() { if (!calBuilt) BuildCallisto(); }

    // ---------------- Mars ----------------
    public static bool InCave(Vector3 p)
    {
        Vector3 l = p - Worlds.MarsO;
        return (Mathf.Abs(l.x) < 4f && l.z > 36f && l.z < 58f) || new Vector2(l.x, l.z - 68f).magnitude < 13f;
    }

    void BuildMars()
    {
        marsBuilt = true;
        marsRoot = new GameObject("Mars").transform;
        var g = Ground("Mars ground", MarsY, Worlds.MarsO, 190f, 110, new Color(0.72f, 0.36f, 0.2f), new Color(0.55f, 0.27f, 0.15f), 21);
        g.transform.SetParent(marsRoot, true);
        Rocket(marsRoot, M(0f, MarsY(0f, -30f), -30f));
        var ret = Interact.Add(M(0f, MarsY(0f, -30f), -27f), 5f, "board the Starship (back to Mars orbit)", f => SpaceWorld.I.ToOrbit(f, "mars"));
        ret.enabled = f => f.world == WorldId.Mars && !Quiet;
        // dog pen
        penC = M(10f, MarsY(10f, -30f), -30f);
        for (int k = 0; k < 14; k++)
        {
            float a = k * Mathf.PI * 2f / 14f;
            if (k == 3) continue;   // gate gap
            Vector3 p = penC + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 4.5f;
            Mats.Prim(PrimitiveType.Cube, marsRoot, p + Vector3.up * 0.5f, new Vector3(0.15f, 1f, 0.15f), Mats.Lit(new Color(0.9f, 0.85f, 0.7f)), true);
        }
        Mats.Prim(PrimitiveType.Cylinder, marsRoot, penC + Vector3.up * 0.04f, new Vector3(9f, 0.02f, 9f), Mats.Unlit(new Color(1f, 0.85f, 0.25f)));
        Ranch.Sign(penC + new Vector3(0f, 2.6f, 4.6f), 180f, "DOG PEN", new Color(0.5f, 0.3f, 0.1f), 3f, 0.8f);
        // cave: corridor of rock + domed chamber
        Color rock = new Color(0.42f, 0.22f, 0.14f);
        for (float z = 36f; z <= 58f; z += 2.2f)
        {
            for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, marsRoot, M(3.6f * s, MarsY(3.6f * s, z) + 2f, z), new Vector3(2.4f, 5f, 2.6f), Mats.Lit(rock), true);
            Mats.Prim(PrimitiveType.Cube, marsRoot, M(0f, MarsY(0f, z) + 4.6f, z), new Vector3(9f, 1.4f, 2.6f), Mats.Lit(Color.Lerp(rock, Color.black, 0.2f)), true);
        }
        for (int k = 0; k < 22; k++)
        {
            float a = k * Mathf.PI * 2f / 22f;
            Vector3 p = new Vector3(Mathf.Cos(a) * 13f, 0f, 68f + Mathf.Sin(a) * 13f);
            if (Mathf.Abs(p.x) < 4f && p.z < 60f) continue;   // the corridor opening
            Mats.Prim(PrimitiveType.Sphere, marsRoot, M(p.x, MarsY(p.x, p.z) + 2.5f, p.z), new Vector3(5f, 7f, 5f), Mats.Lit(rock), true);
        }
        Mats.Prim(PrimitiveType.Cylinder, marsRoot, M(0f, MarsY(0f, 68f) + 6.5f, 68f), new Vector3(30f, 0.8f, 30f), Mats.Lit(Color.Lerp(rock, Color.black, 0.3f)), true);
        Color[] glow = { new Color(0.3f, 1f, 0.9f), new Color(0.75f, 0.45f, 1f) };
        for (int k = 0; k < 14; k++)
        {
            float a = k * 0.45f, d = 6f + (k % 3) * 2f;
            Mats.Prim(PrimitiveType.Cube, marsRoot, M(Mathf.Cos(a) * d, MarsY(0f, 68f) + 0.6f, 68f + Mathf.Sin(a) * d), new Vector3(0.4f, 1.3f, 0.4f), new Vector3(15f, k * 40f, 10f), Mats.Unlit(glow[k % 2]), false);
        }
        Ranch.Sign(M(0f, MarsY(0f, 34f) + 6f, 34f), 180f, "CAVE", new Color(0.35f, 0.15f, 0.1f), 3f, 1f);
        // Curiosity jump ramps east and west
        foreach (float x in new[] { -30f, 34f })
        {
            float z = -2f;
            var r = Mats.Prim(PrimitiveType.Cube, marsRoot, M(x, MarsY(x, z) + 0.6f, z), new Vector3(5f, 0.4f, 7f), new Vector3(-15f, x < 0 ? 270f : 90f, 0f), Mats.Lit(new Color(0.85f, 0.65f, 0.25f)), true);
        }
        foreach (var rr in marsRoot.GetComponentsInChildren<Renderer>()) rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        MeshMerge.Merge(marsRoot, false);

        // cave dogs (unnamed) + King Germy
        for (int k = 0; k < 16; k++)
        {
            var d = Animal.Dog("", M(Random.Range(-6f, 6f), 0f, 68f + Random.Range(-6f, 6f)), k % 3 == 0);
            d.transform.localScale = Vector3.one * 0.7f;
            d.transform.SetParent(marsRoot, true);
            d.Init(new Rect(Worlds.MarsO.x - 9f, Worlds.MarsO.z + 59f, 18f, 18f), Worlds.MarsO.y + MarsY(0f, 68f));
            d.skittish = false;
            caveDogs.Add(d);
        }
        var king = Animal.Dog("King Germy", M(0f, 0f, 74f), true);
        king.transform.localScale = Vector3.one * 1.5f;
        king.transform.SetParent(marsRoot, true);
        king.Init(new Rect(Worlds.MarsO.x - 3f, Worlds.MarsO.z + 71f, 6f, 6f), Worlds.MarsO.y + MarsY(0f, 74f));
        Mats.Prim(PrimitiveType.Cylinder, king.head, new Vector3(0f, 0.2f, 0f), new Vector3(0.22f, 0.06f, 0.22f), Mats.Unlit(new Color(1f, 0.8f, 0.2f)));
        Label(king.transform, "King Germy", 1.2f);
        // lost pups with light beams
        Vector3[] spots = { new Vector3(-6f, 0f, -62f), new Vector3(6.5f, 0f, -53f), new Vector3(-21f, 0f, 7f), new Vector3(23f, 0f, 13f), new Vector3(-44f, 0f, -20f), new Vector3(46f, 0f, 30f) };
        for (int k = 0; k < spots.Length; k++)
        {
            Vector3 s = spots[k];
            var p = Animal.Dog("", M(s.x, MarsY(s.x, s.z), s.z), k % 2 == 0);
            p.transform.localScale = Vector3.one * 0.5f;
            p.transform.SetParent(marsRoot, true);
            p.Init(new Rect(Worlds.MarsO.x + s.x - 2f, Worlds.MarsO.z + s.z - 2f, 4f, 4f), 0f);
            p.skittish = false;
            p.groundFn = (x, z) => Worlds.MarsO.y + MarsY(x - Worlds.MarsO.x, z - Worlds.MarsO.z);
            pups.Add(p);
            beams.Add(Beam(marsRoot, M(s.x, MarsY(s.x, s.z), s.z), new Color(1f, 0.95f, 0.5f, 0.25f)));
        }
        // glow crystals (collectible)
        var holder = new GameObject("Mars crystals").transform;
        Vector3[] cs = { new Vector3(-30f, 0f, 30f), new Vector3(40f, 0f, -40f), new Vector3(22f, 0f, -8f), new Vector3(-50f, 0f, 40f), new Vector3(0f, 0f, 68f), new Vector3(5f, 0f, 72f), new Vector3(-5f, 0f, 64f), new Vector3(60f, 0f, 0f), new Vector3(-60f, 0f, -45f), new Vector3(0f, 0f, 47f) };
        for (int k = 0; k < cs.Length; k++)
        {
            Vector3 c = cs[k];
            var g2 = Mats.Prim(PrimitiveType.Cube, holder, M(c.x, MarsY(c.x, c.z) + 1.1f, c.z), new Vector3(0.6f, 1.2f, 0.6f), new Vector3(0f, 0f, 45f), Mats.Unlit(glow[k % 2]), false);
            Pickups.Add(g2.transform, WorldId.Mars, "crystals", 1.8f);
        }
        // Curiosity rover
        Curiosity(M(-8f, MarsY(-8f, -14f) + 1f, -14f));
    }

    void Curiosity(Vector3 pos)
    {
        var go = new GameObject("Curiosity");
        go.transform.position = pos;
        var v = go.AddComponent<GroundVehicle>();
        v.engineKind = 7; v.enginePitch = 1.25f;   // electric rover whine
        v.Title = "Curiosity";
        v.EnterVerb = "drive Curiosity";
        v.hasWorldBounds = true; v.worldCenter = Worlds.MarsO; v.worldRadius = 92f;
        v.SetupBodyPublic(900f, new Vector3(0f, 1.1f, 0f), new Vector3(2.2f, 0.8f, 3f), new Vector3(0f, 0.4f, 0f));
        v.maxSpeed = 16f; v.accel = 10f; v.turnRate = 1.6f; v.grip = 6f;
        Transform t = go.transform;
        Material white = Mats.Lit(new Color(0.9f, 0.88f, 0.82f)), dark = Mats.Lit(new Color(0.2f, 0.2f, 0.22f));
        Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 1.2f, 0f), new Vector3(2f, 0.6f, 2.8f), white);
        Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(0.5f, 2.2f, 0.9f), new Vector3(0.12f, 0.7f, 0.12f), dark);
        Mats.Prim(PrimitiveType.Cube, t, new Vector3(0.5f, 2.95f, 0.95f), new Vector3(0.6f, 0.3f, 0.35f), white);
        Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(-0.6f, 1.75f, -1.2f), new Vector3(0.5f, 0.35f, 0.5f), new Vector3(30f, 0f, 0f), dark);
        float r = 0.42f;
        foreach (var w in new[] { new Vector3(-1.1f, r, 1.2f), new Vector3(1.1f, r, 1.2f), new Vector3(-1.1f, r, 0f), new Vector3(1.1f, r, 0f), new Vector3(-1.1f, r, -1.2f), new Vector3(1.1f, r, -1.2f) })
        {
            Transform n = Mats.Node(t, "Wheel", w);
            Mats.Prim(PrimitiveType.Cylinder, n, Vector3.zero, new Vector3(r * 2f, 0.18f, r * 2f), new Vector3(0f, 0f, 90f), dark);
            v.AddWheel(w, r, n, Mathf.Abs(w.z) > 1f);
        }
        v.seat = Mats.Node(t, "Seat", new Vector3(0f, 1.55f, 0f));
        v.seatScale = 0.6f;
        v.FinishSetup();
        Mats.SetLayer(go, Vehicle.VehicleLayer);
    }

    void Label(Transform t, string s, float h)
    {
        var lab = new GameObject("Tag");
        lab.transform.SetParent(t, false);
        lab.transform.localPosition = Vector3.up * h;
        var tm = lab.AddComponent<TextMesh>();
        tm.text = s; UIK.WorldText(tm, 48, 0.035f); tm.anchor = TextAnchor.MiddleCenter;
        lab.AddComponent<Billboard>();
    }

    void CheckMars(Frog f, string msg)
    {
        int safe = 0; foreach (bool b in pupSafe) if (b) safe++;
        int crystals = 10 - Pickups.Remaining("crystals");
        if (!marsDone && safe >= pups.Count && crystals >= 10)
        {
            marsDone = true;
            Toast(WorldId.Mars, "MARS MISSION COMPLETE! All pups home and every crystal found.", 5f);
            Sfx.Play(Sfx.Win, 1f);
        }
        else if (f != null && msg != null) f.Toast(msg + "   (pups home " + safe + " / " + pups.Count + ")", 2.5f);
    }

    // ---------------- Callisto ----------------
    void BuildCallisto()
    {
        calBuilt = true;
        calRoot = new GameObject("Callisto").transform;
        var g = Ground("Callisto ground", CalY, Worlds.CallistoO, 170f, 100, new Color(0.62f, 0.62f, 0.68f), new Color(0.42f, 0.4f, 0.42f), 31);
        g.transform.SetParent(calRoot, true);
        Rocket(calRoot, C(0f, CalY(0f, 0f), 0f));
        var ret = Interact.Add(C(0f, CalY(0f, 3f), 3f), 5f, "board the Starship (back to Callisto orbit)", f => SpaceWorld.I.ToOrbit(f, "callisto"));
        ret.enabled = f => f.world == WorldId.Callisto && !Quiet;
        // Jupiter fills the sky
        var jtex = Resources.Load<Texture2D>("LB/space_jupiter");   // NASA map (stage B)
        Material jm;
        if (jtex != null && Mats.UnlitTexBase != null) jm = Mats.UnlitTex(jtex);
        else
        {
        jm = new Material(Mats.Unlit(Color.white));
        var jt = new Texture2D(8, 128, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 128; y++) { Color c = Color.Lerp(new Color(0.85f, 0.7f, 0.5f), new Color(0.6f, 0.38f, 0.25f), Mathf.PerlinNoise(y * 0.09f, 7f) * 1.2f); for (int x = 0; x < 8; x++) jt.SetPixel(x, y, c); }
        jt.Apply(true);
        jm.mainTexture = jt;
        }
        var jup = Mats.Prim(PrimitiveType.Sphere, calRoot, C(150f, 260f, 520f), Vector3.one * 520f, jm);
        jup.transform.rotation = Quaternion.Euler(12f, 0f, 20f);
        // ice spikes for flavour
        var r = new System.Random(5);
        for (int i = 0; i < 40; i++)
        {
            float x = ((float)r.NextDouble() - 0.5f) * 140f, z = ((float)r.NextDouble() - 0.5f) * 140f;
            if (new Vector2(x, z).magnitude < 12f) continue;
            float s = 0.6f + (float)r.NextDouble() * 1.8f;
            Mats.Prim(PrimitiveType.Cube, calRoot, C(x, CalY(x, z) + s * 0.6f, z), new Vector3(s * 0.6f, s * 1.6f, s * 0.6f), new Vector3((float)r.NextDouble() * 20f, (float)r.NextDouble() * 90f, (float)r.NextDouble() * 20f), Mats.Shiny(new Color(0.75f, 0.88f, 0.95f)), true);
        }
        foreach (var rr in calRoot.GetComponentsInChildren<Renderer>()) rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        MeshMerge.Merge(calRoot, false);
        // beacons
        beaconOff = Mats.Unlit(new Color(0.4f, 0.12f, 0.1f));
        beaconGreen = Mats.Unlit(new Color(0.3f, 1f, 0.4f));
        Vector3[] bs = { new Vector3(-40f, 0f, 30f), new Vector3(45f, 0f, 20f), new Vector3(5f, 0f, -50f) };
        for (int k = 0; k < 3; k++)
        {
            Vector3 b = bs[k];
            float y = CalY(b.x, b.z);
            Mats.Prim(PrimitiveType.Cube, calRoot, C(b.x, y + 1.6f, b.z), new Vector3(0.5f, 3.2f, 0.5f), Mats.Steel(new Color(0.5f, 0.5f, 0.55f)), true);
            for (int s = 0; s < 3; s++) Mats.Prim(PrimitiveType.Cube, calRoot, C(b.x, y + 0.3f, b.z) + Quaternion.Euler(0f, s * 120f, 0f) * Vector3.forward * 0.8f, new Vector3(0.15f, 0.6f, 1.6f), Mats.Steel(new Color(0.4f, 0.4f, 0.45f)));
            var l = Mats.Prim(PrimitiveType.Sphere, calRoot, C(b.x, y + 3.5f, b.z), Vector3.one * 0.9f, beaconOff);
            beaconLights.Add(l.GetComponent<Renderer>());
            int kk = k;
            var h = Interact.Add(C(b.x, y, b.z), 2.5f, "switch on the old beacon", f => SwitchBeacon(kk, f));
            h.enabled = f => f.world == WorldId.Callisto && !beaconOn[kk];
        }
        var holder = new GameObject("Callisto shards").transform;
        for (int k = 0; k < 12; k++)
        {
            float a = k * 0.52f + 0.3f, d = 18f + (k % 4) * 12f;
            float x = Mathf.Cos(a) * d, z = Mathf.Sin(a) * d;
            var sh = Mats.Prim(PrimitiveType.Cube, holder, C(x, CalY(x, z) + 1.1f, z), new Vector3(0.45f, 1.1f, 0.45f), new Vector3(0f, 0f, 30f), Mats.Unlit(new Color(0.6f, 0.95f, 1f)), false);
            Pickups.Add(sh.transform, WorldId.Callisto, "shards", 1.8f);
        }
    }

    void SwitchBeacon(int k, Frog f)
    {
        beaconOn[k] = true;
        beaconLights[k].sharedMaterial = beaconGreen;
        FX.Sparkle(beaconLights[k].transform.position, new Color(0.4f, 1f, 0.5f), 20);
        Sfx.Play(Sfx.Pickup, 0.8f, 0.8f);
        CheckCal(f, "Beacon online!");
    }

    void CheckCal(Frog f, string msg)
    {
        int on = 0; foreach (bool b in beaconOn) if (b) on++;
        int shards = 12 - Pickups.Remaining("shards");
        if (!calDone && on >= 3 && shards >= 12)
        {
            calDone = true;
            Toast(WorldId.Callisto, "CALLISTO SURVEY COMPLETE!", 5f);
            Sfx.Play(Sfx.Win, 1f);
        }
        else if (f != null) f.Toast(msg + "   (beacons " + on + " / 3, shards " + shards + " / 12)", 2.5f);
    }

    // ---------------- update ----------------
    void Update()
    {
        if (Game.I == null) return;
        bool onMars = false, onCal = false;
        foreach (Frog f in Game.I.frogs) if (f != null) { if (f.world == WorldId.Mars) onMars = true; if (f.world == WorldId.Callisto) onCal = true; }
        if (RobotFeed.WatchingWorld(WorldId.Mars)) onMars = true;           // ffu20: robot video feed
        if (RobotFeed.WatchingWorld(WorldId.Callisto)) onCal = true;
        if (marsBuilt && marsRoot.gameObject.activeSelf != onMars) marsRoot.gameObject.SetActive(onMars);
        if (calBuilt && calRoot.gameObject.activeSelf != onCal) calRoot.gameObject.SetActive(onCal);
        float dt = Time.deltaTime;
        if (onMars) UpdateMars(dt);
        if (onCal && beaconLights.Count > 0)
            for (int k = 0; k < beaconLights.Count; k++) if (beaconOn[k]) beaconLights[k].transform.localScale = Vector3.one * (0.9f + Mathf.Sin(Time.time * 6f + k) * 0.15f);
    }

    void UpdateMars(float dt)
    {
        // pups: found -> follow the finder; inside the pen -> safe
        for (int k = 0; k < pups.Count; k++)
        {
            Animal p = pups[k];
            Vector3 pp = p.transform.position;
            if (pupSafe[k]) continue;
            if (p.follow == null)
            {
                foreach (Frog f in Game.I.frogs)
                    if (f != null && f.human && f.world == WorldId.Mars && (f.transform.position - pp).magnitude < 3f)
                    {
                        p.follow = f.transform; p.followDist = 1.6f + k * 0.4f;
                        p.area = new Rect(Worlds.MarsO.x - 95f, Worlds.MarsO.z - 95f, 190f, 190f);
                        if (beams[k] != null) beams[k].gameObject.SetActive(false);
                        f.Toast("A lost pup! It's following you - lead it to the Dog pen by the Starship", 3f);
                        Sfx.Play(Sfx.Pickup, 0.6f, 1.4f);
                        break;
                    }
            }
            else if ((new Vector2(pp.x - penC.x, pp.z - penC.z)).magnitude < 4f)
            {
                pupSafe[k] = true;
                p.follow = null;
                p.area = new Rect(penC.x - 3f, penC.z - 3f, 6f, 6f);
                FX.Sparkle(pp + Vector3.up, new Color(1f, 0.9f, 0.4f), 16);
                Sfx.Play(Sfx.Pickup, 0.8f, 1.2f);
                CheckMars(null, null);
                int safe = 0; foreach (bool b in pupSafe) if (b) safe++;
                Toast(WorldId.Mars, "Pup safe in the pen! " + safe + " / " + pups.Count, 2.5f);
            }
        }
        // cave dogs hop when a frog is near
        foreach (Animal d in caveDogs)
        {
            bool near = false;
            foreach (Frog f in Game.I.frogs) if (f != null && f.world == WorldId.Mars && (f.transform.position - d.Pos).magnitude < 4f) near = true;
            if (near && d.body != null) d.body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(Time.time * 7f + d.GetInstanceID())) * 0.4f, 0f);
        }
        // dust storm every ~100 s, 20 s long; shelter in the cave
        stormT -= dt;
        if (stormT <= 0f && storm <= 0f && !Quiet) { storm = 20f; stormT = 100f; Toast(WorldId.Mars, "DUST STORM! Shelter in the cave to the north", 3f); }
        if (storm > 0f) storm -= dt;
        float want = storm > 0f ? 0.05f : 0.006f;
        Worlds.MarsFog = Mathf.MoveTowards(Worlds.MarsFog, want, dt * 0.02f);
    }
}
