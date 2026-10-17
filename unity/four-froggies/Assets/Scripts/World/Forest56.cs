using System.Collections.Generic;
using UnityEngine;

// ffu56 SIX-YEAR-OLD FOREST IDEAS (Bill's standing checklist A):
//  * TREE FORT at the forest's south-west edge (Terrain56.FortSpot, a path leads there): starts as a bare tree with a pile of
//    planks. Build it plank by plank - James (E: hammer 4 planks) and / or a robot (phone PLAY > "Build the tree fort") - posts,
//    deck, railings, walls with a window, roof, rope ladder, lantern, flag. When it's done: climb up / down the rope ladder,
//    robots climb up too and keep lookout ("I can see the pond from up here!").
//  * BUG HUNT WITH A JAR: glowing fireflies drift under the forest canopy, butterflies + beetles in the meadow by the forest.
//    E: catch one in your jar (the jar glows on James), let them all go from the tree fort deck or its ladder. Robots too:
//    PLAY > "Bug hunt" = the robot catches 3 and brings its jar over, then lets them go with you.
//  * Footprints in the mud (Core/Tracks56.cs) - everyone leaves them, the pond rim keeps them longest.
// Built in every load (not only with ?terrain): these are toys, not looks. Robot verbs: World/RobotVisit54.cs "fort" / "bugs".
public static class Fort56
{
    public static Vector3 Base, Deck, Ladder;
    public static float DeckY;
    public static int Built, Total;
    public static bool Done { get { return Total > 0 && Built >= Total; } }
    public static string LastBuilder = "";
    static readonly List<GameObject> parts = new List<GameObject>();
    static readonly List<Vector3> scales = new List<Vector3>();
    static GameObject pile, deckCol, flag;
    static Transform root;
    static float popT;

    public static void Build()
    {
        if (root != null) return;
        Vector2 s = Terrain56.FortSpot;
        // a forest tree near the spot holds the fort (its trunk goes through the deck), else a stand-alone trunk
        Vector3 tb = new Vector3(s.x, 0f, s.y); float bd = 1e9f;
        foreach (var t in Forest.TreeBases) { float d = (new Vector2(t.x, t.z) - s).sqrMagnitude; if (d < bd && d < 12f * 12f) { bd = d; tb = t; } }
        bool own = bd > 1e8f;
        Base = new Vector3(tb.x, Ranch.GY(tb.x, tb.z), tb.z);
        Terrain56.FortSpot = new Vector2(Base.x, Base.z);
        DeckY = Base.y + 3.3f;
        root = new GameObject("TreeFort56").transform; root.position = Base;
        Material plank = Mats.Lit(new Color(0.62f, 0.45f, 0.28f)), dark = Mats.Lit(new Color(0.42f, 0.3f, 0.19f)), rope = Mats.Lit(new Color(0.78f, 0.68f, 0.45f));
        Material roof = Mats.Lit(new Color(0.35f, 0.42f, 0.25f)), flagM = Mats.Lit(new Color(0.15f, 0.75f, 0.3f));
        if (own)
        {
            var tr = Mats.Prim(PrimitiveType.Cylinder, root, Vector3.up * 4f, new Vector3(0.9f, 4f, 0.9f), Mats.Lit(new Color(0.36f, 0.26f, 0.17f)), true);
            tr.name = "FortTrunk56";
        }
        float H = 3.6f, D = 3.3f;
        // 4 corner posts
        for (int i = 0; i < 4; i++) { float x = (i % 2 == 0 ? -1 : 1) * H * 0.5f, z = (i < 2 ? -1 : 1) * H * 0.5f; P(new Vector3(x, D * 0.5f, z), new Vector3(0.22f, D, 0.22f), Vector3.zero, dark); }
        // deck planks
        for (int i = 0; i < 9; i++) P(new Vector3(-H * 0.5f + (i + 0.5f) * H / 9f, D, 0f), new Vector3(H / 9f - 0.03f, 0.1f, H + 0.2f), Vector3.zero, plank);
        // railings (east + west + north low rails)
        for (int k = 0; k < 2; k++)
        {
            float y = D + 0.45f + k * 0.45f;
            P(new Vector3(-H * 0.5f, y, 0f), new Vector3(0.08f, 0.08f, H), Vector3.zero, dark);
            P(new Vector3(H * 0.5f, y, 0f), new Vector3(0.08f, 0.08f, H), Vector3.zero, dark);
            P(new Vector3(0f, y, H * 0.5f), new Vector3(H, 0.08f, 0.08f), Vector3.zero, dark);
        }
        // little hut walls on the north half (window in the east wall)
        float wy = D + 0.85f;
        P(new Vector3(0f, wy, H * 0.5f - 0.05f), new Vector3(H - 0.2f, 1.6f, 0.08f), Vector3.zero, plank);
        P(new Vector3(-H * 0.5f + 0.05f, wy, H * 0.25f), new Vector3(0.08f, 1.6f, H * 0.5f), Vector3.zero, plank);
        P(new Vector3(H * 0.5f - 0.05f, wy + 0.55f, H * 0.25f), new Vector3(0.08f, 0.5f, H * 0.5f), Vector3.zero, plank);
        P(new Vector3(H * 0.5f - 0.05f, wy - 0.55f, H * 0.25f), new Vector3(0.08f, 0.5f, H * 0.5f), Vector3.zero, plank);
        // roof (two slopes over the hut)
        P(new Vector3(-H * 0.27f, D + 2.0f, H * 0.25f), new Vector3(H * 0.6f, 0.08f, H * 0.6f), new Vector3(0f, 0f, 28f), roof);
        P(new Vector3(H * 0.27f, D + 2.0f, H * 0.25f), new Vector3(H * 0.6f, 0.08f, H * 0.6f), new Vector3(0f, 0f, -28f), roof);
        // rope ladder on the south side: 2 ropes + 7 rungs
        P(new Vector3(-0.3f, D * 0.5f, -H * 0.5f - 0.15f), new Vector3(0.04f, D, 0.04f), Vector3.zero, rope);
        P(new Vector3(0.3f, D * 0.5f, -H * 0.5f - 0.15f), new Vector3(0.04f, D, 0.04f), Vector3.zero, rope);
        for (int i = 0; i < 7; i++) P(new Vector3(0f, 0.35f + i * 0.45f, -H * 0.5f - 0.15f), new Vector3(0.64f, 0.06f, 0.08f), Vector3.zero, plank);
        // lantern + flag
        P(new Vector3(H * 0.5f - 0.3f, D + 1.5f, H * 0.5f - 0.3f), Vector3.one * 0.22f, Vector3.zero, Mats.Unlit(new Color(1f, 0.85f, 0.45f)));
        P(new Vector3(H * 0.25f, D + 2.9f, H * 0.25f), new Vector3(0.05f, 1.4f, 0.05f), Vector3.zero, dark);
        flag = P(new Vector3(H * 0.25f + 0.35f, D + 3.35f, H * 0.25f), new Vector3(0.7f, 0.42f, 0.03f), Vector3.zero, flagM);
        Total = parts.Count;
        // the plank pile at the foot of the tree (shrinks as it's used)
        pile = new GameObject("FortPile56"); pile.transform.SetParent(root, false);
        for (int i = 0; i < 6; i++) { var g = Mats.Prim(PrimitiveType.Cube, pile.transform, new Vector3(2.6f, 0.07f + i * 0.12f, -2.4f + (i % 2) * 0.1f), new Vector3(1.8f, 0.1f, 0.3f), plank, false); g.transform.rotation = Quaternion.Euler(0f, 20f + i * 9f, 0f); }
        // deck collider (only once the deck is built)
        deckCol = new GameObject("FortDeck56"); deckCol.transform.SetParent(root, false);
        deckCol.transform.position = Base + new Vector3(0f, D - 0.05f, 0f);
        var bc = deckCol.AddComponent<BoxCollider>(); bc.size = new Vector3(H, 0.2f, H); deckCol.SetActive(false);
        Deck = Base + new Vector3(0.6f, D + 0.06f, -0.8f);
        Ladder = Base + new Vector3(0f, 0f, -H * 0.5f - 0.9f);
        Ladder.y = Ranch.GY(Ladder.x, Ladder.z);
        Show();
        var hb = Interact.Add(Ladder, 2.2f, "", f => UseBase(f));
        hb.dynLabel = f => !Done ? "hammer planks onto the tree fort (" + Built + " / " + Total + ")" : Jar56.Count(f) > 0 ? "let your bugs go at the tree fort" : "climb the rope ladder up to the tree fort";
        hb.enabled = f => f.world == WorldId.Ranch && f.vehicle == null;
        var hd = Interact.Add(Deck, 2.4f, "", f => UseDeck(f));
        hd.dynLabel = f => Jar56.Count(f) > 0 ? "let your bugs go from the tree fort" : "climb down the rope ladder";
        hd.enabled = f => f.world == WorldId.Ranch && f.vehicle == null && Done && f.transform.position.y > DeckY - 0.8f;
        Debug.Log("FOREST56 tree fort at " + Base.ToString("0.0") + (own ? " (own trunk)" : " (forest tree)") + " deck y " + DeckY.ToString("0.00") + ", " + Total + " planks to build");
    }

    static GameObject P(Vector3 local, Vector3 size, Vector3 euler, Material m)
    {
        var g = Mats.Prim(PrimitiveType.Cube, root, local, size, m, false);
        g.transform.localRotation = Quaternion.Euler(euler);
        g.SetActive(false);
        parts.Add(g); scales.Add(size);
        return g;
    }

    static void Show()
    {
        for (int i = 0; i < parts.Count; i++) if (parts[i] != null && parts[i].activeSelf != (i < Built)) parts[i].SetActive(i < Built);
        if (deckCol != null) deckCol.SetActive(Built >= 13);
        if (pile != null) pile.transform.localScale = new Vector3(1f, Mathf.Max(0.05f, 1f - Built / (float)Mathf.Max(1, Total)), 1f);
    }

    // adds n planks (James's hammer or a robot); returns the toast line
    public static string AddPlanks(int n, string who)
    {
        if (root == null) Build();
        if (Done) return "the tree fort is finished!";
        int b0 = Built;
        Built = Mathf.Min(Total, Built + n);
        LastBuilder = who;
        for (int i = b0; i < Built; i++) { var g = parts[i]; if (g != null) { g.SetActive(true); FX.Dust(g.transform.position, 0.3f); } }
        Show();
        Sfx.PlayAt(Sfx.BumpSoft != null ? Sfx.BumpSoft : Sfx.Thud, Base + Vector3.up * 2f, 0.45f, 30f, 1.5f);
        if (Done)
        {
            if (flag != null) flag.AddComponent<Flag56>();
            Debug.Log("FOREST56 tree fort DONE (last plank by " + who + ")");
            return "TA-DA! The tree fort is finished - climb the rope ladder!";
        }
        Debug.Log("FOREST56 fort planks " + Built + " / " + Total + " by " + who);
        return "bang bang bang! " + Built + " / " + Total + " planks";
    }

    static void UseBase(Frog f)
    {
        if (!Done) { f.Toast(AddPlanks(4, f.nick), 2f); return; }
        if (Jar56.Count(f) > 0) { f.Toast(Jar56.Release(f, Base + Vector3.up * 2f), 2.5f); return; }
        f.SendTo(WorldId.Ranch, Deck, 200f);
        f.Toast("Up in the tree fort! You can see the whole forest from here!", 2.6f);
        Debug.Log("FOREST56 " + f.nick + " climbed up the tree fort");
    }
    static void UseDeck(Frog f)
    {
        if (Jar56.Count(f) > 0) { f.Toast(Jar56.Release(f, Deck + Vector3.up), 2.5f); return; }
        f.SendTo(WorldId.Ranch, Ladder + Vector3.up * 0.1f, 180f);
        f.Toast("down the rope ladder!", 1.6f);
    }

    public static void DemoClimb(Frog f) { if (Done) UseBase(f); }
    // demo / robot helpers
    public static Vector3 RobotDeckSpot(int i) { return Base + new Vector3(-0.7f + (i % 3) * 0.7f, DeckY - Base.y + 0.06f, -0.9f); }
}

public class Flag56 : MonoBehaviour
{
    Vector3 p0; void Start() { p0 = transform.localPosition; }
    void Update() { transform.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 3.1f) * 14f, 0f); }
}

// ---------------------------------------------------------------- the bug jar (per frog / robot)
public static class Jar56
{
    static readonly Dictionary<int, List<Bug56>> jars = new Dictionary<int, List<Bug56>>();
    static readonly Dictionary<int, GameObject> props = new Dictionary<int, GameObject>();
    public static int Caught, Released;
    public static int Count(Component who) { List<Bug56> l; return who != null && jars.TryGetValue(who.GetInstanceID(), out l) ? l.Count : 0; }
    public static string Put(Component who, Transform holder, Vector3 local, Bug56 b)
    {
        int id = who.GetInstanceID();
        List<Bug56> l; if (!jars.TryGetValue(id, out l)) { l = new List<Bug56>(); jars[id] = l; }
        l.Add(b); b.Catch(); Caught++;
        GameObject jar;
        if (!props.TryGetValue(id, out jar) || jar == null)
        {
            jar = new GameObject("BugJar56"); jar.transform.SetParent(holder, false); jar.transform.localPosition = local;
            Mats.Prim(PrimitiveType.Cylinder, jar.transform, Vector3.zero, new Vector3(0.16f, 0.11f, 0.16f), Mats.Glass, false).transform.localPosition = Vector3.zero;
            Mats.Prim(PrimitiveType.Cylinder, jar.transform, Vector3.zero, new Vector3(0.17f, 0.02f, 0.17f), Mats.Lit(new Color(0.6f, 0.6f, 0.62f)), false).transform.localPosition = new Vector3(0f, 0.12f, 0f);
            props[id] = jar;
        }
        var dot = Mats.Prim(PrimitiveType.Sphere, jar.transform, Vector3.zero, Vector3.one * 0.035f, Mats.Unlit(b.firefly ? new Color(0.85f, 1f, 0.35f) : b.col), false);
        dot.transform.localPosition = new Vector3(Random.Range(-0.05f, 0.05f), Random.Range(-0.07f, 0.07f), Random.Range(-0.05f, 0.05f));
        int fl = 0, ot = 0; foreach (var x in l) { if (x.firefly) fl++; else ot++; }
        Debug.Log("FOREST56 jar " + id + " caught " + (b.firefly ? "firefly" : b.kindName) + " -> " + fl + " fireflies, " + ot + " bugs");
        return "Got it! Jar: " + fl + " firefl" + (fl == 1 ? "y" : "ies") + (ot > 0 ? ", " + ot + " bug" + (ot == 1 ? "" : "s") : "");
    }
    public static string Release(Component who, Vector3 at)
    {
        int id = who.GetInstanceID();
        List<Bug56> l; if (!jars.TryGetValue(id, out l) || l.Count == 0) return "the jar is empty";
        int n = l.Count;
        foreach (var b in l) b.Free(at);
        Released += n; l.Clear();
        GameObject jar; if (props.TryGetValue(id, out jar) && jar != null) Object.Destroy(jar);
        props.Remove(id);
        Debug.Log("FOREST56 jar " + id + " released " + n + " at " + at.ToString("0.0"));
        return "Bye bye bugs! " + n + " glowing friends fly off into the forest";
    }
}

// ---------------------------------------------------------------- the bugs
public class Bug56 : MonoBehaviour
{
    public static readonly List<Bug56> All = new List<Bug56>();
    public bool firefly, caught;
    public string kindName = "firefly";
    public Color col = Color.white;
    Vector3 home, vel; float ph, hgt; Hotspot hot; Transform wingL, wingR, glow;

    public static void SpawnAll()
    {
        if (All.Count > 0) return;
        var r = new System.Random(5656);
        int nf = Look.Mobile ? 9 : 16, nb = Look.Mobile ? 5 : 9;
        Vector2 a = JetLayout.ForestMin, b = JetLayout.ForestMax;
        for (int i = 0; i < nf; i++)
        {
            // the fort side of the forest first (half of them within ~25 m of the fort)
            float x = i < nf / 2 ? Terrain56.FortSpot.x + (float)(r.NextDouble() * 2 - 1) * 18f : Mathf.Lerp(a.x + 4f, b.x - 4f, (float)r.NextDouble());
            float z = i < nf / 2 ? Terrain56.FortSpot.y + (float)(r.NextDouble() * 2 - 1) * 18f : Mathf.Lerp(a.y + 4f, b.y - 4f, (float)r.NextDouble());
            x = Mathf.Clamp(x, a.x + 2f, b.x - 2f); z = Mathf.Clamp(z, a.y + 2f, b.y - 2f);
            Make(true, new Vector3(x, Ranch.GY(x, z), z), "firefly", new Color(0.85f, 1f, 0.35f), r);
        }
        string[] kinds = { "butterfly", "beetle", "butterfly", "ladybug" };
        Color[] cs = { new Color(1f, 0.55f, 0.1f), new Color(0.15f, 0.35f, 0.55f), new Color(0.55f, 0.75f, 1f), new Color(0.9f, 0.1f, 0.1f) };
        for (int i = 0; i < nb; i++)
        {
            float x = a.x - 4f - (float)r.NextDouble() * 22f, z = a.y + (float)r.NextDouble() * 30f - 8f;
            if (Layout.RoadDist(x, z) < Layout.TrackW * 0.5f + 2f) { x -= 14f; }
            Make(false, new Vector3(x, Ranch.GY(x, z), z), kinds[i % 4], cs[i % 4], r);
        }
        Debug.Log("FOREST56 bugs: " + nf + " fireflies in the forest, " + nb + " butterflies / beetles in the meadow");
    }

    static void Make(bool ff, Vector3 p, string kind, Color c, System.Random r)
    {
        var go = new GameObject(ff ? "Firefly56" : "Bug56 " + kind);
        var bg = go.AddComponent<Bug56>();
        bg.firefly = ff; bg.kindName = kind; bg.col = c; bg.home = p; bg.ph = (float)r.NextDouble() * 10f;
        bg.hgt = ff ? 0.7f + (float)r.NextDouble() * 1.6f : (kind == "butterfly" ? 0.5f + (float)r.NextDouble() * 0.8f : 0.08f);
        go.transform.position = p + Vector3.up * bg.hgt;
        if (ff)
        {
            bg.glow = Mats.Prim(PrimitiveType.Sphere, go.transform, Vector3.zero, Vector3.one * 0.09f, Mats.Unlit(c), false).transform;
            Mats.Prim(PrimitiveType.Sphere, go.transform, Vector3.back * 0.06f, Vector3.one * 0.06f, Mats.Lit(new Color(0.15f, 0.12f, 0.08f)), false);
        }
        else if (kind == "butterfly")
        {
            Mats.Prim(PrimitiveType.Cube, go.transform, Vector3.zero, new Vector3(0.025f, 0.025f, 0.12f), Mats.Lit(new Color(0.1f, 0.08f, 0.06f)), false);
            var wl = new GameObject("WL").transform; wl.SetParent(go.transform, false);
            var wr = new GameObject("WR").transform; wr.SetParent(go.transform, false);
            Mats.Prim(PrimitiveType.Cube, wl, Vector3.left * 0.07f, new Vector3(0.13f, 0.005f, 0.11f), Mats.Lit(c), false);
            Mats.Prim(PrimitiveType.Cube, wr, Vector3.right * 0.07f, new Vector3(0.13f, 0.005f, 0.11f), Mats.Lit(c), false);
            bg.wingL = wl; bg.wingR = wr;
        }
        else Mats.Prim(PrimitiveType.Sphere, go.transform, Vector3.zero, new Vector3(0.09f, 0.06f, 0.12f), Mats.Shiny(c), false);
        bg.hot = Interact.Add(go.transform.position, 1.7f, "catch the " + kind + " in your jar", f => bg.ByFrog(f));
        bg.hot.enabled = f => !bg.caught && f.world == WorldId.Ranch && f.vehicle == null;
        All.Add(bg);
    }

    void ByFrog(Frog f)
    {
        if (caught) return;
        f.Toast(Jar56.Put(f, f.transform, new Vector3(0.28f, 0.45f, 0.18f), this), 2.2f);
        Sfx.PlayAt(Sfx.Pick(Sfx.Servo), transform.position, 0.25f, 20f, 2f);
    }
    public void Catch() { caught = true; gameObject.SetActive(false); }
    public void Free(Vector3 at) { caught = false; transform.position = at; vel = Random.onUnitSphere * 1.5f + Vector3.up * 1.2f; gameObject.SetActive(true); }

    void Update()
    {
        float t = Time.time + ph;
        Vector3 p = transform.position;
        Vector3 want = home + new Vector3(Mathf.Sin(t * 0.31f) * 3.5f + Mathf.Sin(t * 0.83f) * 1.2f, hgt + Mathf.Sin(t * 0.7f) * (firefly ? 0.5f : kindName == "butterfly" ? 0.35f : 0f), Mathf.Cos(t * 0.27f) * 3.5f + Mathf.Sin(t * 0.61f) * 1.1f);
        if (!firefly && kindName != "butterfly") want.y = Ranch.GY(want.x, want.z) + 0.06f;
        vel = Vector3.Lerp(vel, (want - p) * 0.8f, Time.deltaTime * 1.5f);
        p += vel * Time.deltaTime;
        transform.position = p;
        if (vel.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(new Vector3(vel.x, 0f, vel.z) + Vector3.forward * 0.001f);
        if (glow != null) { float bl = Mathf.Repeat(t * 0.55f, 1f); glow.localScale = Vector3.one * (bl < 0.45f ? 0.1f + Mathf.Sin(bl / 0.45f * Mathf.PI) * 0.09f : 0.035f); }
        if (wingL != null) { float fl = Mathf.Sin(t * 22f) * 55f; wingL.localRotation = Quaternion.Euler(0f, 0f, fl); wingR.localRotation = Quaternion.Euler(0f, 0f, -fl); }
        if (hot != null) hot.pos = p;
    }

    public static Bug56 NearestFree(Vector3 p, float max)
    {
        Bug56 best = null; float bd = max * max;
        foreach (var b in All) { if (b == null || b.caught) continue; float d = (b.transform.position - p).sqrMagnitude; if (d < bd) { bd = d; best = b; } }
        return best;
    }
}

public static class Forest56
{
    static bool built;
    // Bootstrap order: after Ranch + Forest are built (Terrain56.Begin time is fine: Forest.TreeBases is filled)
    public static void Build()
    {
        if (built) return; built = true;
        try { Fort56.Build(); } catch (System.Exception e) { Debug.LogWarning("FOREST56 fort " + e); }
        try { Bug56.SpawnAll(); } catch (System.Exception e) { Debug.LogWarning("FOREST56 bugs " + e); }
    }
}
