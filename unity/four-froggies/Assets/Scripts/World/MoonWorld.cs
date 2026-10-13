using System.Collections.Generic;
using UnityEngine;

// ffu41: the MOON. The Starship already orbits it in space (the Moon sits 180 m from Earth in the SpaceWorld model, orbit
// capture radius 26 m); it is now landable: Y in Moon orbit lands here, like Mars and Callisto.
//  * Surface (Worlds.MoonO, local coords): grey regolith heightfield (190 m, NASA Apollo 11 photo AS11-40-5877 regolith
//    crop, public domain - Resources/LB/moonground.jpg), raised-rim craters, scattered rocks + boulders, a black sky with
//    ~350 stars and the Earth hanging in it (NASA Earth map already used in space, LB/space_earth).
//  * Moon gravity 1.62 m/s^2 (~1/6 g): frogs (Frog.spaceGravity = G, hop speed x HopK -> ~2.2x higher, ~3.6x longer
//    floaty hops), the buggy (own rigidbody gravity) and the golf ball. Dust is its own particle system at 1/6 g with no
//    drag (no air): long slow ballistic arcs - landing puffs under hopping frogs, rooster tails behind the buggy.
//  * Moon buggy: an open-frame two-seat lunar roving vehicle (Apollo LRV style, built in code, no markings) with two
//    rear jump seats - up to 4 frogs (driver + 3 riders: walk up to the buggy while someone drives, A = ride along,
//    A again = hop off). Soft, bouncy low-g suspension. Drive it onto the BUGGY BAY pad beside the Starship and stop:
//    it is stowed in the cargo bay and everyone aboard flies back up to Moon orbit.
//  * Things to do: plant the froggy flag (A at the flag kit by the lander), an Apollo-style lander (descent + ascent
//    stage prop), moon golf (A at the tee - the ball flies at 1/6 g), 8 lunar samples to collect -> "MOON SURVEY COMPLETE".
public class MoonWorld : MonoBehaviour
{
    public static MoonWorld I;
    public const float Gms = 1.62f, G = 1.62f / 9.81f, HopK = 0.62f;
    public static bool Ready { get { return I != null && I.built; } }
    Transform root;
    bool built, flagUp, golfDone, surveyDone;
    public MoonBuggy buggy;
    readonly Transform[] paxSeat = new Transform[3];
    readonly Frog[] riders = new Frog[3];
    Hotspot rideSpot, golfSpot;
    Rigidbody golf; Vector3 golfTee; float golfFlight; Vector3 golfFrom;
    Transform flag, flagCloth; float flagT = -1f;
    ParticleSystem dust;
    readonly Dictionary<Frog, float> air = new Dictionary<Frog, float>();
    float bayT;
    public static readonly Vector2 PadP = new Vector2(0f, -30f), BayP = new Vector2(9f, -25f), BuggyHome = new Vector2(-8f, -17f),
        LanderP = new Vector2(18f, 8f), FlagP = new Vector2(12.5f, 4.5f), TeeP = new Vector2(-14f, -6f);
    static readonly Vector3[] Craters =
    {
        new Vector3(26f, -12f, 12f), new Vector3(-30f, 22f, 9f), new Vector3(6f, 42f, 15f), new Vector3(-46f, -30f, 8f),
        new Vector3(46f, 36f, 10f), new Vector3(-16f, -58f, 7f), new Vector3(-58f, 50f, 11f), new Vector3(58f, -44f, 9f),
        new Vector3(-8f, 20f, 4f), new Vector3(36f, 14f, 3.5f), new Vector3(-36f, -6f, 4.5f), new Vector3(20f, -48f, 5f),
    };

    public static Vector3 L(float x, float y, float z) { return Worlds.MoonO + new Vector3(x, y, z); }

    public static float Y(float x, float z)
    {
        float y = (Mathf.PerlinNoise(x * 0.022f + 13f, z * 0.022f + 3f) - 0.5f) * 4f + (Mathf.PerlinNoise(x * 0.11f + 4f, z * 0.11f) - 0.5f) * 0.6f;
        foreach (Vector3 c in Craters)
        {
            float d = new Vector2(x - c.x, z - c.y).magnitude, r = c.z;
            y += Mathf.Exp(-Mathf.Pow((d - r) / (r * 0.22f + 0.6f), 2f)) * r * 0.16f       // raised rim
               - Mathf.Clamp01(1f - d / r) * Mathf.Clamp01(1f - d / r) * r * 0.42f;       // bowl
        }
        // flat landing / base area round the pad, bay, lander and tee
        float flat = Mathf.Clamp01(new Vector2(x - 2f, z + 22f).magnitude / 18f);
        y *= Mathf.SmoothStep(0f, 1f, flat);
        float edge = new Vector2(x, z).magnitude;
        y += Mathf.SmoothStep(0f, 1f, (edge - 80f) / 14f) * 20f;
        return y;
    }
    public static float GY(Vector3 p) { return Worlds.MoonO.y + Y(p.x - Worlds.MoonO.x, p.z - Worlds.MoonO.z); }

    public static void Create()
    {
        if (I != null) return;
        I = new GameObject("MoonWorld").AddComponent<MoonWorld>();
        Pickups.Listen((it, f) => { if (it.group == "moonsamples") I.Check(f, "Lunar sample! " + (8 - Pickups.Remaining("moonsamples")) + " / 8"); });
    }

    public static void Land(Frog f, int k)
    {
        Create();
        if (!I.built) I.Build();
        float x = -4f + k * 2.2f, z = -21f;
        f.SendTo(WorldId.Moon, L(x, Y(x, z) + 0.4f, z), 0f);
        if (!I.surveyDone) f.Toast("THE MOON: 1/6 gravity - floaty hops! Moon buggy by the Starship (4 seats), plant the flag at the lander, moon golf at the tee, 8 lunar samples", 6f);
    }

    // ---------------- build ----------------
    void Build()
    {
        built = true;
        root = new GameObject("Moon").transform;
        Transform st = new GameObject("Static").transform; st.SetParent(root, false);
        BuildGround();
        // Starship standing on its pad (A = back to Moon orbit)
        Rocket(st, L(PadP.x, Y(PadP.x, PadP.y), PadP.y));
        var ret = Interact.Add(L(PadP.x, Y(PadP.x, PadP.y + 3f), PadP.y + 3f), 5f, "board the Starship (back to Moon orbit)", f => ToOrbitAll(f, null));
        ret.enabled = f => f.world == WorldId.Moon && f.vehicle == null && f.passengerOf == null;
        // buggy bay pad (yellow ring + sign)
        Vector3 bp = L(BayP.x, Y(BayP.x, BayP.y), BayP.y);
        Mats.Prim(PrimitiveType.Cylinder, st, bp + Vector3.up * 0.03f, new Vector3(7f, 0.03f, 7f), Mats.Unlit(new Color(1f, 0.82f, 0.2f)));
        Mats.Prim(PrimitiveType.Cylinder, st, bp + Vector3.up * 0.05f, new Vector3(6.3f, 0.03f, 6.3f), Mats.Lit(new Color(0.3f, 0.3f, 0.32f)));
        Ranch.Sign(bp + new Vector3(0f, 2.4f, 3.8f), 180f, "BUGGY BAY", new Color(0.35f, 0.3f, 0.1f), 2.8f, 0.7f);
        Lander(st, L(LanderP.x, Y(LanderP.x, LanderP.y), LanderP.y));
        Rocks(st);
        Sky();
        foreach (var rr in root.GetComponentsInChildren<Renderer>()) rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        MeshMerge.Merge(st, false);
        // dust: own particle system, 1/6 g, no air drag
        var dgo = new GameObject("Moon dust"); dgo.transform.SetParent(root, false);
        dust = dgo.AddComponent<ParticleSystem>();
        dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = dust.main; m.loop = true; m.playOnAwake = false; m.simulationSpace = ParticleSystemSimulationSpace.World;
        m.maxParticles = 1400; m.gravityModifier = G; m.startSpeed = 0f; m.startLifetime = 3f;
        var em = dust.emission; em.enabled = false; var sh = dust.shape; sh.enabled = false;
        var col = dust.colorOverLifetime; col.enabled = true;
        var gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0.6f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = gr;
        var dr = dgo.GetComponent<ParticleSystemRenderer>();
        dr.sharedMaterial = FX.SoftMat(); dr.renderMode = ParticleSystemRenderMode.Billboard;
        dr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; dr.receiveShadows = false;
        dust.Play();
        BuildBuggy(L(BuggyHome.x, Y(BuggyHome.x, BuggyHome.y) + 1.2f, BuggyHome.y));
        // flag kit by the lander
        var fk = Interact.Add(L(FlagP.x, Y(FlagP.x, FlagP.y), FlagP.y), 3f, "plant the froggy flag", f => PlantFlag(f));
        fk.enabled = f => f.world == WorldId.Moon && !flagUp && f.vehicle == null && f.passengerOf == null;
        BuildFlag(L(FlagP.x, Y(FlagP.x, FlagP.y), FlagP.y));
        Golf();
        // 8 lunar samples (spinning rocks with a little marker light)
        var holder = new GameObject("Moon samples").transform; holder.SetParent(root, false);
        Vector2[] sp = { new Vector2(30f, -2f), new Vector2(-34f, 30f), new Vector2(8f, 50f), new Vector2(-50f, -22f), new Vector2(48f, 44f), new Vector2(-18f, -50f), new Vector2(60f, -30f), new Vector2(-10f, 22f) };
        Material sm = Mats.Shiny(new Color(0.48f, 0.46f, 0.44f)), mk = Mats.Unlit(new Color(0.45f, 0.95f, 1f));
        for (int k = 0; k < sp.Length; k++)
        {
            var t = new GameObject("Sample").transform; t.SetParent(holder, false);
            t.position = L(sp[k].x, Y(sp[k].x, sp[k].y) + 0.9f, sp[k].y);
            Mats.Prim(PrimitiveType.Sphere, t, Vector3.zero, new Vector3(0.7f, 0.5f, 0.6f), sm);
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.55f, 0f), new Vector3(0.12f, 0.45f, 0.12f), mk);
            Pickups.Add(t, WorldId.Moon, "moonsamples", 1.8f);
        }
        Debug.Log("MoonWorld: built at " + Worlds.MoonO + " - " + Craters.Length + " craters, buggy, lander, flag kit, golf tee, " + sp.Length + " samples, gravity " + Gms + " m/s2 (x" + G.ToString("0.000") + ")");
    }

    void BuildGround()
    {
        const float size = 190f; const int n = 120;
        var v = new Vector3[(n + 1) * (n + 1)]; var uv = new Vector2[v.Length];
        for (int z = 0; z <= n; z++)
            for (int x = 0; x <= n; x++)
            {
                float lx = -size * 0.5f + x * size / n, lz = -size * 0.5f + z * size / n;
                v[z * (n + 1) + x] = Worlds.MoonO + new Vector3(lx, Y(lx, lz), lz);
                uv[z * (n + 1) + x] = new Vector2(lx / 7f, lz / 7f);
            }
        var tri = new int[n * n * 6]; int k = 0;
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++) { int a = z * (n + 1) + x, b = a + 1, c = a + n + 1, d = c + 1; tri[k++] = a; tri[k++] = c; tri[k++] = b; tri[k++] = b; tri[k++] = c; tri[k++] = d; }
        var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.vertices = v; mesh.uv = uv; mesh.triangles = tri; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var go = new GameObject("Moon ground");
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var gt = Resources.Load<Texture2D>("LB/moonground");
        Material gm;
        if (gt != null) { gt.wrapMode = TextureWrapMode.Repeat; gm = Mats.Tex(gt, 0.04f); gm.color = new Color(1.25f, 1.23f, 1.2f); }
        else gm = Mats.Lit(new Color(0.5f, 0.5f, 0.5f));
        go.AddComponent<MeshRenderer>().sharedMaterial = gm;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
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

    // Apollo-style lunar lander prop: gold-foil octagonal descent stage on 4 splayed legs + footpads + ladder, angular
    // grey ascent stage with dark triangular windows, a docking hatch and a dish. No markings.
    static void Lander(Transform st, Vector3 p)
    {
        Material gold = Mats.Shiny(new Color(0.85f, 0.66f, 0.22f)), foil = Mats.Shiny(new Color(0.62f, 0.5f, 0.24f)), grey = Mats.Paint(new Color(0.72f, 0.72f, 0.7f), 0.5f),
            dark = Mats.Lit(new Color(0.08f, 0.08f, 0.1f)), leg = Mats.Steel(new Color(0.75f, 0.72f, 0.6f)), black = Mats.Lit(new Color(0.15f, 0.15f, 0.16f));
        float h = 1.5f;   // underside of the descent stage
        Mats.Prim(PrimitiveType.Cube, st, p + new Vector3(0f, h + 0.85f, 0f), new Vector3(3.2f, 1.7f, 3.2f), gold, true);
        Mats.Prim(PrimitiveType.Cube, st, p + new Vector3(0f, h + 0.85f, 0f), new Vector3(3.2f, 1.68f, 3.2f), new Vector3(0f, 45f, 0f), foil);
        Mats.Prim(PrimitiveType.Cylinder, st, p + new Vector3(0f, h - 0.25f, 0f), new Vector3(1.1f, 0.35f, 1.1f), black);   // descent engine bell
        for (int k = 0; k < 4; k++)
        {
            Quaternion q = Quaternion.Euler(0f, 45f + k * 90f, 0f);
            Vector3 top = p + q * new Vector3(0f, h + 1.2f, 1.7f), foot = p + q * new Vector3(0f, 0.12f, 3.3f);
            var g = Mats.Prim(PrimitiveType.Cylinder, st, (top + foot) * 0.5f, new Vector3(0.14f, (top - foot).magnitude * 0.5f, 0.14f), leg);
            g.transform.rotation = Quaternion.FromToRotation(Vector3.up, (top - foot).normalized);
            Vector3 brace = p + q * new Vector3(0f, h + 0.2f, 1.6f);
            var b2 = Mats.Prim(PrimitiveType.Cylinder, st, (brace + foot) * 0.5f + Vector3.up * 0.3f, new Vector3(0.07f, (brace - foot).magnitude * 0.45f, 0.07f), leg);
            b2.transform.rotation = Quaternion.FromToRotation(Vector3.up, (brace - foot + Vector3.up * 0.6f).normalized);
            Mats.Prim(PrimitiveType.Cylinder, st, foot + Vector3.up * 0.04f, new Vector3(0.8f, 0.05f, 0.8f), leg);
        }
        // ladder down the front (+x) leg
        Quaternion lq = Quaternion.Euler(0f, 90f, 0f);
        for (int r = 0; r < 7; r++) Mats.Prim(PrimitiveType.Cube, st, p + lq * new Vector3(0f, 0.45f + r * 0.36f, 2.15f - r * 0.07f), new Vector3(0.5f, 0.04f, 0.05f), leg);
        Mats.Prim(PrimitiveType.Cube, st, p + lq * new Vector3(0f, h + 1.85f, 1.75f), new Vector3(0.9f, 0.08f, 0.6f), leg);   // porch
        // ascent stage
        Vector3 a = p + Vector3.up * (h + 1.7f);
        Mats.Prim(PrimitiveType.Cube, st, a + new Vector3(0f, 0.85f, 0f), new Vector3(2.3f, 1.7f, 2.1f), grey, true);
        Mats.Prim(PrimitiveType.Cube, st, a + new Vector3(0.75f, 0.95f, 0f), new Vector3(1.1f, 1.5f, 1.9f), new Vector3(0f, 0f, -12f), grey);
        Mats.Prim(PrimitiveType.Cube, st, a + new Vector3(-0.6f, 0.6f, 0f), new Vector3(1.0f, 1.1f, 2.4f), Mats.Paint(new Color(0.55f, 0.55f, 0.54f), 0.4f));
        for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, st, a + new Vector3(1.32f, 1.25f, s * 0.45f), new Vector3(0.04f, 0.42f, 0.38f), new Vector3(s * 20f, 0f, -12f), dark);   // windows
        Mats.Prim(PrimitiveType.Cube, st, a + new Vector3(1.22f, 0.4f, 0f), new Vector3(0.05f, 0.75f, 0.75f), dark);   // hatch
        Mats.Prim(PrimitiveType.Cylinder, st, a + new Vector3(0f, 1.82f, 0f), new Vector3(0.8f, 0.12f, 0.8f), grey);  // docking tunnel
        Mats.Prim(PrimitiveType.Cylinder, st, a + new Vector3(-0.7f, 2.2f, 0.6f), new Vector3(0.05f, 0.4f, 0.05f), leg);
        var dish = Mats.Prim(PrimitiveType.Cylinder, st, a + new Vector3(-0.7f, 2.65f, 0.6f), new Vector3(0.7f, 0.03f, 0.7f), new Vector3(30f, 0f, 20f), Mats.Paint(new Color(0.9f, 0.9f, 0.88f), 0.4f));
        for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, st, a + new Vector3(0.2f, 1.2f, s * 1.13f), new Vector3(0.3f, 0.3f, 0.12f), black);   // RCS quads
    }

    static void Rocks(Transform st)
    {
        var r = new System.Random(41);
        Material[] rm = { Mats.Lit(new Color(0.42f, 0.41f, 0.4f)), Mats.Lit(new Color(0.34f, 0.33f, 0.32f)), Mats.Lit(new Color(0.5f, 0.49f, 0.47f)) };
        for (int i = 0; i < 70; i++)
        {
            float x = ((float)r.NextDouble() - 0.5f) * 160f, z = ((float)r.NextDouble() - 0.5f) * 160f;
            if (new Vector2(x - 2f, z + 22f).magnitude < 19f) continue;   // keep the base clear
            bool big = i % 9 == 0;
            float s = big ? 1.6f + (float)r.NextDouble() * 2.2f : 0.25f + (float)r.NextDouble() * 0.7f;
            Mats.Prim(PrimitiveType.Sphere, st, L(x, Y(x, z) + s * 0.25f, z), new Vector3(s * (0.9f + (float)r.NextDouble() * 0.5f), s * 0.7f, s), new Vector3((float)r.NextDouble() * 30f, (float)r.NextDouble() * 180f, (float)r.NextDouble() * 30f), rm[i % 3], big);
        }
    }

    void Sky()
    {
        // stars (tiny unlit cubes on a far dome, merged)
        var sroot = new GameObject("Moon stars").transform; sroot.SetParent(root, false);
        var r = new System.Random(7);
        Material sw = Mats.Unlit(new Color(0.95f, 0.95f, 1f)), sb = Mats.Unlit(new Color(0.7f, 0.8f, 1f));
        for (int i = 0; i < 350; i++)
        {
            Vector3 d = new Vector3((float)r.NextDouble() * 2f - 1f, (float)r.NextDouble() * 0.95f + 0.04f, (float)r.NextDouble() * 2f - 1f).normalized;
            float s = 0.9f + (float)r.NextDouble() * 1.9f;
            Mats.Prim(PrimitiveType.Cube, sroot, Worlds.MoonO + d * 760f, Vector3.one * s, new Vector3(i * 17f, i * 31f, 0f), i % 4 == 0 ? sb : sw);
        }
        foreach (var rr in sroot.GetComponentsInChildren<Renderer>()) rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        MeshMerge.Merge(sroot, false);
        // the Earth hanging in the black sky (NASA map)
        var et = Resources.Load<Texture2D>("LB/space_earth");
        Material em = et != null && Mats.UnlitTexBase != null ? Mats.UnlitTex(et) : Mats.Unlit(new Color(0.3f, 0.5f, 0.9f));
        var earth = Mats.Prim(PrimitiveType.Sphere, root, Worlds.MoonO + new Vector3(-0.32f, 0.36f, 0.88f).normalized * 640f, Vector3.one * 92f, em);
        earth.transform.rotation = Quaternion.Euler(-20f, 150f, 23f);
        var halo = new Material(Mats.Fx); halo.color = new Color(0.45f, 0.65f, 1f, 0.16f);
        Mats.Prim(PrimitiveType.Sphere, root, earth.transform.position, Vector3.one * 98f, halo);
    }

    // ---------------- buggy ----------------
    void BuildBuggy(Vector3 pos)
    {
        var go = new GameObject("Moon buggy");
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, 20f, 0f);
        var v = go.AddComponent<MoonBuggy>();
        buggy = v;
        v.engineKind = 7; v.enginePitch = 1.6f;   // electric whine
        v.Title = "Moon buggy";
        v.EnterVerb = "drive the moon buggy";
        v.hasWorldBounds = true; v.worldCenter = Worlds.MoonO; v.worldRadius = 90f;
        v.rest = 0.5f;          // long-travel low-g suspension
        v.SetupBodyPublic(450f, new Vector3(0f, 0.95f, 0f), new Vector3(1.9f, 0.5f, 3.0f), new Vector3(0f, 0.55f, 0f));
        v.maxSpeed = 13f; v.accel = 6.5f; v.turnRate = 1.4f; v.grip = 3.2f; v.dustAmount = 0f;
        Transform t = go.transform;
        Material tube = Mats.Steel(new Color(0.78f, 0.78f, 0.76f)), dark = Mats.Lit(new Color(0.12f, 0.12f, 0.13f)), mesh = Mats.Steel(new Color(0.55f, 0.55f, 0.52f)),
            fender = Mats.Paint(new Color(0.62f, 0.58f, 0.5f), 0.5f), seatM = Mats.Paint(new Color(0.3f, 0.32f, 0.36f), 0.3f), gold = Mats.Shiny(new Color(0.8f, 0.64f, 0.25f)), white = Mats.Paint(new Color(0.9f, 0.9f, 0.88f), 0.4f);
        // chassis: open tube frame + floor pan
        Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.86f, 0f), new Vector3(1.5f, 0.06f, 2.9f), mesh);
        for (int s = -1; s <= 1; s += 2)
        {
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(s * 0.78f, 0.9f, 0f), new Vector3(0.07f, 0.07f, 3.1f), tube);
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(s * 0.78f, 1.38f, -0.15f), new Vector3(0.05f, 0.05f, 1.9f), tube);   // seat-side rail
        }
        foreach (float z in new[] { -1.5f, -0.4f, 0.6f, 1.5f }) Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.9f, z), new Vector3(1.6f, 0.06f, 0.06f), tube);
        // two front seats + two rear jump seats (folding frame seats)
        Vector3[] sp = { new Vector3(-0.42f, 1.08f, 0.35f), new Vector3(0.42f, 1.08f, 0.35f), new Vector3(-0.42f, 1.08f, -0.8f), new Vector3(0.42f, 1.08f, -0.8f) };
        for (int k = 0; k < 4; k++)
        {
            Mats.Prim(PrimitiveType.Cube, t, sp[k] + new Vector3(0f, -0.12f, 0f), new Vector3(0.62f, 0.05f, 0.55f), seatM);
            Mats.Prim(PrimitiveType.Cube, t, sp[k] + new Vector3(0f, 0.2f, -0.3f), new Vector3(0.62f, 0.5f, 0.05f), new Vector3(-12f, 0f, 0f), seatM);
            Mats.Prim(PrimitiveType.Cube, t, sp[k] + new Vector3(0f, -0.06f, 0f), new Vector3(0.66f, 0.04f, 0.04f), tube);
        }
        // control console between the front seats (hand controller T-bar), TV camera + high-gain umbrella dish at the front
        Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 1.3f, 0.95f), new Vector3(0.42f, 0.5f, 0.12f), new Vector3(-20f, 0f, 0f), white);
        Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 1.22f, 0.55f), new Vector3(0.05f, 0.25f, 0.05f), dark);
        Mats.Prim(PrimitiveType.Cube, t, new Vector3(0.0f, 1.36f, 0.55f), new Vector3(0.18f, 0.05f, 0.05f), dark);
        Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(0.55f, 1.5f, 1.35f), new Vector3(0.04f, 0.55f, 0.04f), tube);
        Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(0.55f, 2.1f, 1.35f), new Vector3(0.95f, 0.025f, 0.95f), new Vector3(-15f, 0f, 0f), white);   // umbrella dish
        Mats.Prim(PrimitiveType.Cube, t, new Vector3(-0.5f, 1.35f, 1.4f), new Vector3(0.22f, 0.18f, 0.32f), white);   // TV camera
        Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(-0.5f, 1.65f, 1.4f), new Vector3(0.03f, 0.3f, 0.03f), tube);
        Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 1.05f, -1.45f), new Vector3(1.2f, 0.35f, 0.3f), gold);       // rear tool / sample pallet
        // 4 wire-mesh wheels under arched fenders
        float r = 0.41f;
        foreach (var w in new[] { new Vector3(-0.95f, r, 1.15f), new Vector3(0.95f, r, 1.15f), new Vector3(-0.95f, r, -1.15f), new Vector3(0.95f, r, -1.15f) })
        {
            Transform n = Mats.Node(t, "Wheel", w);
            Mats.Prim(PrimitiveType.Cylinder, n, Vector3.zero, new Vector3(r * 2f, 0.12f, r * 2f), new Vector3(0f, 0f, 90f), mesh);
            Mats.Prim(PrimitiveType.Cylinder, n, Vector3.zero, new Vector3(r * 1.5f, 0.14f, r * 1.5f), new Vector3(0f, 0f, 90f), dark);
            for (int c = 0; c < 6; c++) Mats.Prim(PrimitiveType.Cube, n, Vector3.zero, new Vector3(0.24f, 0.05f, r * 1.96f), new Vector3(c * 30f, 0f, 0f), tube);   // chevron treads read as spokes
            v.AddWheel(w, r, n, w.z > 0f);
            Mats.Prim(PrimitiveType.Cube, t, w + new Vector3(0f, r + 0.42f, 0f), new Vector3(0.36f, 0.04f, 1.0f), fender);
            for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, t, w + new Vector3(0f, r + 0.3f, s * 0.52f), new Vector3(0.36f, 0.04f, 0.3f), new Vector3(s * 45f, 0f, 0f), fender);
        }
        v.seat = Mats.Node(t, "Seat", sp[0]);
        for (int k = 0; k < 3; k++) paxSeat[k] = Mats.Node(t, "Rider seat " + (k + 1), sp[k + 1]);
        v.seatScale = 0.62f;
        v.FinishSetup();
        v.LowG();
        Mats.SetLayer(go, Vehicle.VehicleLayer);
        foreach (var rr in go.GetComponentsInChildren<Renderer>()) rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rideSpot = Interact.Add(pos, 3.6f, "ride along in the moon buggy", f => Ride(f));
        rideSpot.enabled = f => f.world == WorldId.Moon && f.vehicle == null && f.passengerOf == null && buggy.driver != null && buggy.driver != f && FreeSeat() >= 0;
    }

    int FreeSeat() { for (int k = 0; k < 3; k++) if (riders[k] == null) return k; return -1; }
    public int Riders { get { int n = 0; foreach (var f in riders) if (f != null) n++; return n; } }
    public int Aboard { get { return Riders + (buggy != null && buggy.driver != null ? 1 : 0); } }

    public void Ride(Frog f)
    {
        int k = FreeSeat();
        if (k < 0 || buggy == null) return;
        riders[k] = f;
        f.BoardAsPassenger(buggy);
        f.RideSeat(paxSeat[k], buggy.seatScale);
        f.Toast("Riding the moon buggy" + (buggy.driver != null ? " with " + buggy.driver.nick : "") + " - A to hop off", 2.5f);
        Sfx.Play(Sfx.Click, 0.5f, 1.2f);
    }

    public static void HopOff(Frog f)
    {
        if (I == null || I.buggy == null) { f.LeavePassenger(); return; }
        for (int k = 0; k < 3; k++) if (I.riders[k] == f) I.riders[k] = null;
        f.LeavePassenger();
        Vector3 p = I.buggy.ExitPoint();
        f.SendTo(WorldId.Moon, p, I.buggy.transform.eulerAngles.y);
    }

    // everyone aboard the buggy (driver + riders) or one froggy on foot -> Starship -> Moon orbit
    void ToOrbitAll(Frog first, List<Frog> crew)
    {
        if (SpaceWorld.I == null) return;
        if (crew == null) { SpaceWorld.I.ToOrbit(first, "moon"); return; }
        foreach (var f in crew)
        {
            if (f.vehicle != null) f.ExitVehicle();
            if (f.passengerOf != null) f.LeavePassenger();
        }
        foreach (var f in crew) SpaceWorld.I.ToOrbit(f, "moon");
    }

    void Golf()
    {
        golfTee = L(TeeP.x, Y(TeeP.x, TeeP.y), TeeP.y);
        var root2 = new GameObject("Golf tee").transform; root2.SetParent(root, false);
        Mats.Prim(PrimitiveType.Cube, root2, golfTee + new Vector3(0f, 0.02f, 0f), new Vector3(2.2f, 0.04f, 2.2f), Mats.Lit(new Color(0.3f, 0.45f, 0.3f)));
        Mats.Prim(PrimitiveType.Cylinder, root2, golfTee + new Vector3(0f, 0.08f, 0f), new Vector3(0.05f, 0.05f, 0.05f), Mats.Unlit(new Color(1f, 0.9f, 0.3f)));
        Mats.Prim(PrimitiveType.Cube, root2, golfTee + new Vector3(-1.4f, 0.45f, 0f), new Vector3(0.05f, 0.9f, 0.05f), new Vector3(0f, 0f, 18f), Mats.Steel(new Color(0.75f, 0.75f, 0.75f)));   // the club, leaning
        Ranch.Sign(golfTee + new Vector3(0f, 1.6f, 1.5f), 180f, "MOON GOLF", new Color(0.15f, 0.35f, 0.2f), 2.4f, 0.6f);
        var b = Mats.Prim(PrimitiveType.Sphere, root, golfTee + Vector3.up * 0.2f, Vector3.one * 0.22f, Mats.Shiny(Color.white), true);
        golf = b.AddComponent<Rigidbody>();
        golf.mass = 0.05f; golf.useGravity = false; golf.drag = 0f; golf.angularDrag = 0.4f; golf.interpolation = RigidbodyInterpolation.Interpolate;
        golf.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        b.AddComponent<MoonGravity>();
        golf.isKinematic = true;
        golfSpot = Interact.Add(golfTee, 2.6f, "tee off (moon golf - 1/6 g!)", f => TeeOff(f));
        golfSpot.enabled = f => f.world == WorldId.Moon && f.vehicle == null && f.passengerOf == null && golfFlight <= 0f;
    }

    public void TeeOff(Frog f)
    {
        golf.isKinematic = false;
        golf.position = golfTee + Vector3.up * 0.2f;
        Vector3 fw = f.transform.forward; fw.y = 0f; if (fw.sqrMagnitude < 0.01f) fw = Vector3.forward; fw.Normalize();
        golf.velocity = fw * 9.5f + Vector3.up * 6.5f;
        golfFlight = 0.01f; golfFrom = golfTee;
        Sfx.Play(Sfx.Click, 0.8f, 1.6f);
        f.Toast("FORE! At 1/6 g it flies for miles and miles...", 2.5f);
        Debug.Log("MoonWorld: tee off v " + golf.velocity);
    }

    void BuildFlag(Vector3 p)
    {
        flag = new GameObject("Froggy flag").transform; flag.SetParent(root, false); flag.position = p;
        Material pole = Mats.Steel(new Color(0.85f, 0.85f, 0.85f));
        Mats.Prim(PrimitiveType.Cylinder, flag, new Vector3(0f, 1.3f, 0f), new Vector3(0.05f, 1.3f, 0.05f), pole);
        Mats.Prim(PrimitiveType.Cylinder, flag, new Vector3(0.6f, 2.55f, 0f), new Vector3(0.03f, 0.6f, 0.03f), new Vector3(0f, 0f, 90f), pole);   // the stiffening rod (no air)
        flagCloth = new GameObject("Cloth").transform; flagCloth.SetParent(flag, false); flagCloth.localPosition = new Vector3(0.62f, 2.15f, 0f);
        Mats.Prim(PrimitiveType.Cube, flagCloth, Vector3.zero, new Vector3(1.2f, 0.78f, 0.02f), Mats.Paint(new Color(0.18f, 0.62f, 0.3f), 0.3f));
        Mats.Prim(PrimitiveType.Sphere, flagCloth, new Vector3(0f, 0f, 0.012f), new Vector3(0.42f, 0.34f, 0.01f), Mats.Paint(new Color(1f, 0.85f, 0.25f), 0.3f));   // froggy face
        for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Sphere, flagCloth, new Vector3(s * 0.11f, 0.12f, 0.02f), new Vector3(0.1f, 0.1f, 0.01f), Mats.Lit(new Color(0.05f, 0.05f, 0.05f)));
        for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Sphere, flagCloth, new Vector3(s * 0.11f, 0.12f, -0.012f), new Vector3(0.1f, 0.1f, 0.01f), Mats.Lit(new Color(0.05f, 0.05f, 0.05f)));
        Mats.Prim(PrimitiveType.Sphere, flagCloth, new Vector3(0f, 0f, -0.012f), new Vector3(0.42f, 0.34f, 0.01f), Mats.Paint(new Color(1f, 0.85f, 0.25f), 0.3f));
        // the folded kit (shown until it's planted)
        flag.gameObject.SetActive(false);
        kit = Mats.Prim(PrimitiveType.Cube, root, p + new Vector3(0f, 0.15f, 0f), new Vector3(0.25f, 0.3f, 1.4f), new Vector3(0f, 30f, 0f), Mats.Paint(new Color(0.85f, 0.85f, 0.82f), 0.4f));
    }
    GameObject kit;

    public void PlantFlag(Frog f)
    {
        if (flagUp) return;
        flagUp = true; flagT = 0f;
        flag.gameObject.SetActive(true);
        if (kit != null) kit.SetActive(false);
        Vector3 to = f.transform.position - flag.position; to.y = 0f;
        flag.rotation = Quaternion.Euler(0f, to.sqrMagnitude > 0.01f ? Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg + 90f : 0f, 0f);
        for (int i = 0; i < 14; i++) Kick(flag.position + Random.insideUnitSphere * 0.3f, Random.insideUnitSphere * 1.2f + Vector3.up * 1.4f, 0.25f);
        Sfx.Play(Sfx.Pickup, 0.8f, 0.9f);
        Check(f, "Froggy flag planted on the Moon!");
    }

    void Check(Frog f, string msg)
    {
        int got = 8 - Pickups.Remaining("moonsamples");
        if (!surveyDone && got >= 8 && flagUp && golfDone)
        {
            surveyDone = true;
            if (Game.I != null) foreach (Frog g in Game.I.frogs) if (g != null && g.human && g.world == WorldId.Moon) g.Toast("MOON SURVEY COMPLETE! Flag planted, moon golf played, all 8 samples.", 5f);
            Sfx.Play(Sfx.Win, 1f);
        }
        else if (f != null && msg != null) f.Toast(msg + "   (samples " + got + " / 8, flag " + (flagUp ? "up" : "-") + ", golf " + (golfDone ? "done" : "-") + ")", 2.8f);
    }

    public void Kick(Vector3 p, Vector3 v, float size)
    {
        if (dust == null) return;
        var ep = new ParticleSystem.EmitParams();
        ep.position = p; ep.velocity = v; ep.startSize = size * Random.Range(0.8f, 1.3f);
        ep.startLifetime = Random.Range(2.2f, 3.6f);
        float g = Random.Range(0.5f, 0.62f);
        ep.startColor = new Color(g, g * 0.98f, g * 0.95f, 0.75f);
        ep.rotation = Random.Range(0f, 360f);
        dust.Emit(ep, 1);
    }

    // ---------------- per frame ----------------
    void Update()
    {
        if (Game.I == null || !built) return;
        bool on = false;
        foreach (Frog f in Game.I.frogs) if (f != null && f.world == WorldId.Moon) on = true;
        if (RobotFeed.WatchingWorld(WorldId.Moon)) on = true;
        if (root.gameObject.activeSelf != on) root.gameObject.SetActive(on);
        if (buggy != null && buggy.gameObject.activeSelf != on) buggy.gameObject.SetActive(on);
        if (golf != null && golf.gameObject.activeSelf != on) golf.gameObject.SetActive(on);
        if (!on) return;
        float dt = Time.deltaTime;
        // riders sit in the jump / passenger seats (visible)
        for (int k = 0; k < 3; k++)
        {
            Frog r = riders[k];
            if (r == null) continue;
            if (r.passengerOf != buggy || r.world != WorldId.Moon) { riders[k] = null; continue; }
            r.RideSeat(paxSeat[k], buggy.seatScale);
        }
        if (rideSpot != null && buggy != null) rideSpot.pos = buggy.transform.position;
        // hop landing puffs (slow, no air)
        foreach (Frog f in Game.I.frogs)
        {
            if (f == null || f.world != WorldId.Moon || f.vehicle != null || f.passengerOf != null) continue;
            var cc = f.GetComponent<CharacterController>();
            if (cc == null) continue;
            float a; air.TryGetValue(f, out a);
            if (!cc.isGrounded) air[f] = a + dt;
            else
            {
                if (a > 0.35f)
                {
                    Vector3 fp = f.transform.position;
                    int n = Mathf.Clamp(Mathf.RoundToInt(a * 6f), 4, 16);
                    for (int i = 0; i < n; i++) { float an = i * 6.283f / n + Random.value * 0.4f; Kick(fp + new Vector3(Mathf.Cos(an), 0.05f, Mathf.Sin(an)) * 0.3f, new Vector3(Mathf.Cos(an), 0f, Mathf.Sin(an)) * Random.Range(0.8f, 1.8f) + Vector3.up * Random.Range(0.6f, 1.4f), 0.22f); }
                }
                air[f] = 0f;
            }
        }
        // buggy: rooster tails from the rear wheels, bay stow
        if (buggy != null && buggy.rb != null)
        {
            float sp = buggy.Speed;
            if (sp > 2f && buggy.Grounded >= 2)
            {
                int n = Mathf.Clamp(Mathf.RoundToInt(sp * dt * 9f + Random.value), 0, 6);
                Vector3 fw = buggy.transform.forward, rt = buggy.transform.right;
                for (int i = 0; i < n; i++)
                {
                    float s = i % 2 == 0 ? -1f : 1f;
                    Vector3 wp = buggy.transform.position + rt * (s * 0.95f) - fw * 1.4f;
                    wp.y = GY(wp) + 0.15f;
                    Kick(wp, -fw * sp * Random.Range(0.25f, 0.5f) + Vector3.up * Random.Range(2.2f, 4.2f) + rt * s * Random.Range(0f, 0.8f) + buggy.rb.velocity * 0.4f, 0.3f);
                }
            }
            Vector3 bp = buggy.transform.position;
            bool inBay = new Vector2(bp.x - (Worlds.MoonO.x + BayP.x), bp.z - (Worlds.MoonO.z + BayP.y)).magnitude < 3.2f;
            if (inBay && buggy.driver != null && sp < 1.5f)
            {
                bayT += dt;
                buggy.driver.prompt = "BUGGY BAY - hold still: stowing the buggy in the Starship (" + Mathf.Max(0f, 1.5f - bayT).ToString("0.0") + " s)";
                if (bayT > 1.5f) StowBuggy();
            }
            else bayT = 0f;
        }
        // golf ball flight
        if (golf != null && golfFlight > 0f)
        {
            golfFlight += dt;
            Vector3 gp = golf.position;
            if (Random.value < 0.3f && golf.velocity.magnitude > 2f) { }
            bool stopped = golfFlight > 1.5f && golf.velocity.magnitude < 0.4f;
            bool gone = (gp - Worlds.MoonO).magnitude > 95f || gp.y < Worlds.MoonO.y - 30f || golfFlight > 40f;
            if (stopped || gone)
            {
                float d = new Vector2(gp.x - golfFrom.x, gp.z - golfFrom.z).magnitude;
                Debug.Log("MoonWorld: golf ball " + (gone ? "out of bounds" : "stopped") + " " + d.ToString("0") + " m from the tee after " + golfFlight.ToString("0.0") + " s");
                foreach (Frog g in Game.I.frogs) if (g != null && g.human && g.world == WorldId.Moon) g.Toast("Moon golf: " + d.ToString("0") + " m" + (gone ? " - lost over the hills!" : "!"), 3f);
                golfDone = true;
                golf.velocity = Vector3.zero; golf.isKinematic = true; golf.position = golfTee + Vector3.up * 0.2f; golf.transform.position = golf.position;
                golfFlight = 0f;
                Check(null, null);
            }
            else if (golfFlight > 0.2f && Physics.Raycast(gp, Vector3.down, 0.3f) && golf.velocity.y < -1f) Kick(gp, Vector3.up * 1.2f + Random.insideUnitSphere * 0.5f, 0.15f);
        }
        // flag: planted with a little wobble that dies out (no air: it does not flap)
        if (flagT >= 0f && flagT < 3f)
        {
            flagT += dt;
            float k = Mathf.Clamp01(flagT / 0.6f);
            flag.localScale = new Vector3(1f, Mathf.Lerp(0.2f, 1f, k), 1f);
            flagCloth.localRotation = Quaternion.Euler(0f, Mathf.Sin(flagT * 9f) * 12f * Mathf.Exp(-flagT * 1.4f), 0f);
        }
    }

    void StowBuggy()
    {
        bayT = 0f;
        var crew = new List<Frog>();
        if (buggy.driver != null) crew.Add(buggy.driver);
        foreach (var r in riders) if (r != null) crew.Add(r);
        for (int k = 0; k < 3; k++) riders[k] = null;
        Debug.Log("MoonWorld: buggy stowed in the Starship bay with " + crew.Count + " aboard");
        Vector3 bp = buggy.transform.position;
        for (int i = 0; i < 20; i++) Kick(bp + Random.insideUnitSphere * 1.5f, Random.insideUnitSphere * 2f + Vector3.up * 1.5f, 0.35f);
        ToOrbitAll(crew.Count > 0 ? crew[0] : null, crew);
        foreach (var f in crew) f.Toast("Buggy stowed in the Starship cargo bay - back in Moon orbit (" + crew.Count + " aboard)", 3.5f);
        buggy.ResetHome();
    }

    public void DemoBoardDriver(Frog f)
    {
        if (buggy == null) return;
        f.SendTo(WorldId.Moon, buggy.transform.position + buggy.transform.right * -2f, 0f);
        f.EnterVehicle(buggy);
    }
    public void DemoParkInBay()
    {
        if (buggy == null) return;
        Vector3 p = L(BayP.x, Y(BayP.x, BayP.y) + 0.9f, BayP.y);
        buggy.rb.velocity = Vector3.zero; buggy.rb.angularVelocity = Vector3.zero;
        buggy.rb.position = p; buggy.transform.position = p;
    }
}

// the buggy: a GroundVehicle with its own 1/6 g and soft, long-travel springs sized for lunar weight (bouncy)
public class MoonBuggy : GroundVehicle
{
    public int Grounded { get { return groundedCount; } }
    public void LowG()
    {
        rb.useGravity = false;
        int n = Mathf.Max(1, wheels.Count);
        float perWheel = rb.mass * MoonWorld.Gms / n;
        spring = perWheel / (rest * 0.5f);                                   // sag ~half the travel under moon weight
        damper = 2f * Mathf.Sqrt(spring * rb.mass / n) * 0.16f;              // under-damped: it keeps bouncing
    }
    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        if (rb != null && !rb.isKinematic) rb.AddForce(Vector3.down * MoonWorld.Gms, ForceMode.Acceleration);
    }
    public override string HelpLine { get { return "MOON BUGGY: L-stick steer | RT gas | LT brake / reverse | A get out | friends: walk up + A to ride (4 seats) | park on the BUGGY BAY to fly back up"; } }
}

// 1/6 g for loose rigidbodies on the Moon (golf ball); the global Physics.gravity stays Earth's
public class MoonGravity : MonoBehaviour
{
    Rigidbody rb;
    void Awake() { rb = GetComponent<Rigidbody>(); }
    void FixedUpdate() { if (rb != null && !rb.isKinematic) rb.AddForce(Vector3.down * MoonWorld.Gms, ForceMode.Acceleration); }
}

public partial class Starship
{
    // ffu41 demo: target a body and start the auto-transfer (X) as the pilot would
    public void DemoAutoTransfer(int body) { SetTarget(body); StartTransfer(); }
    public bool InOrbitOf(int body) { return mode == Mode.Orbit && orbitBody == body; }
}
