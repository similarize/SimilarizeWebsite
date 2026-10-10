using System.Collections.Generic;
using UnityEngine;

// James's house, inside. Walk into the front door on the ranch to come in; the foyer door takes you back out.
// Built on first entry as a hidden root at Worlds.HouseO (open "dollhouse" rooms, no ceiling, so the camera
// can look down over the low walls). Rooms follow the three.js house: Foyer, Living room, Kitchen (Dad),
// Pet parlor (cats Spotty, Tigy, Kitty, Little White Socks; dogs Germy and Daisy; bunny nook),
// Fish gallery (the five tanks from the 3D house) and a Reptile room (snake + lizard terrariums).
// Mini-games: feed every fish tank, fetch with Germy and Daisy, hide-and-seek with the cats.
public class HouseWorld : MonoBehaviour
{
    public static HouseWorld I;
    public const int WallLayer = 14;
    Transform root;
    bool built;
    readonly List<System.Action> afterMerge = new List<System.Action>();
    int lizN;

    static readonly Color Floor = new Color(0.66f, 0.5f, 0.34f), WallC = new Color(0.93f, 0.9f, 0.82f), Trim = new Color(0.86f, 0.84f, 0.8f);   // was 0.98 white: sunlit wall caps hit 232 grey-white in the probe
    static readonly Color Wood = new Color(0.5f, 0.34f, 0.2f), Sofa = new Color(0.32f, 0.45f, 0.62f), Rug = new Color(0.75f, 0.3f, 0.28f);
    static readonly Rect Parlor = new Rect(-30f, -20f, 20f, 24f), Fish = new Rect(-10f, -20f, 20f, 24f), Reptile = new Rect(10f, -20f, 20f, 24f);
    static readonly Rect Living = new Rect(-30f, 4f, 24f, 16f), Kitchen = new Rect(6f, 4f, 24f, 16f), Foyer = new Rect(-6f, 4f, 12f, 16f);

    public static Vector3 L(float x, float y, float z) { return Worlds.HouseO + new Vector3(x, y, z); }
    public static Vector3 Spawn(int id) { return L(-3f + id * 2f, 0.15f, 15.5f); }

    // ---------- tanks / fish ----------
    class Tank { public string name; public Vector3 c; public Vector3 half; public bool fed; public float fedT; public readonly List<Transform> flakes = new List<Transform>(); public readonly List<FishT> fish = new List<FishT>(); }
    class FishT { public Transform t; public float a, sp, rx, rz, y, size; public Vector3 pos; }
    readonly List<Tank> tanks = new List<Tank>();
    int fedCount;

    // ---------- pets ----------
    readonly List<Animal> cats = new List<Animal>();
    Animal germy, daisy;
    Dad dad;

    // ---------- fetch ----------
    Rigidbody ball;
    Animal carrier;
    Frog thrower;
    int fetches;
    float ballIdle;
    Vector3 toyBox;

    // ---------- hide-and-seek ----------
    bool seeking;
    float seekT;
    int found;
    readonly bool[] catFound = new bool[4];
    static readonly Vector3[] HideSpots = {
        new Vector3(-27f, 0f, 18.2f), new Vector3(-15f, 0f, 18.4f), new Vector3(-8.2f, 0f, 6f), new Vector3(28.4f, 0f, 18.5f),
        new Vector3(14f, 0f, 9f), new Vector3(-8.5f, 0f, -18.6f), new Vector3(8.5f, 0f, -12f), new Vector3(28.4f, 0f, -18.6f),
        new Vector3(12f, 0f, -1.6f), new Vector3(-28.4f, 0f, -18.6f), new Vector3(-28.5f, 0f, 1.5f), new Vector3(3.5f, 0f, 18.6f) };

    public static void Create()
    {
        var go = new GameObject("HouseWorld");
        I = go.AddComponent<HouseWorld>();
        // the ranch front door (walk into it) -> inside
        Vector2 c = Layout.HouseC;
        float z1 = c.y + Layout.HouseSize.y * 0.5f;
        Interact.Add(new Vector3(c.x, 0.45f, z1 + 0.7f), 1.4f, "go inside", f => I.Enter(f), true);
    }

    public void Enter(Frog f)
    {
        if (!built) Build();
        f.SendTo(WorldId.House, Spawn(f.id), 180f);
        f.Toast("James's house: feed the fish, play fetch, find the cats", 3.5f);
        Sfx.Play(Sfx.Door, 0.9f);
    }

    void Exit(Frog f)
    {
        Vector2 c = Layout.HouseC;
        float z1 = c.y + Layout.HouseSize.y * 0.5f;
        f.SendTo(WorldId.Ranch, new Vector3(c.x + (f.id - 1.5f) * 1.5f, 0.6f, z1 + 3.4f), 0f);
        Sfx.Play(Sfx.Door, 0.9f);
    }

    // ---------------- build ----------------
    GameObject Box(Vector3 lp, Vector3 size, Color c, bool col = true, int layer = 0, Vector3 euler = default(Vector3))
    {
        var g = Mats.Prim(PrimitiveType.Cube, root, L(lp.x, lp.y, lp.z), size, euler, Mats.Lit(c), col);
        if (layer != 0) g.layer = layer;
        return g;
    }

    // graphics overhaul stage B: textured walls / floors (Poly Haven CC0 plaster, wood floor, laminate, planks)
    GameObject BoxM(Vector3 lp, Vector3 size, Material m, bool col = true, int layer = 0)
    {
        var g = Mats.Prim(PrimitiveType.Cube, root, L(lp.x, lp.y, lp.z), size, Vector3.zero, m, col);
        if (layer != 0) g.layer = layer;
        return g;
    }
    // tint 0.9/0.86/0.78 (was 1/0.97/0.9: sunlit wall tops read near-white ~232 in the probe)
    static Material WallMat { get { return Mats.TexTint("LB/plaster", new Color(0.9f, 0.86f, 0.78f), 0.05f, 3f); } }
    static Material BaseMat { get { return Mats.TexTint("LB/wood", new Color(0.62f, 0.45f, 0.3f), 0.15f, 1.5f); } }

    GameObject Prim(PrimitiveType t, Vector3 lp, Vector3 size, Color c, bool col = false)
    {
        return Mats.Prim(t, root, L(lp.x, lp.y, lp.z), size, Mats.Lit(c), col);
    }

    // wall from a to b (axis aligned) with door gaps (centre positions along the wall, 3.4 m wide)
    void Wall(float x0, float z0, float x1, float z1, params float[] doors)
    {
        const float H = 3.2f, T = 0.3f, D = 3.4f;
        bool alongX = Mathf.Abs(z1 - z0) < 0.01f;
        float a = alongX ? Mathf.Min(x0, x1) : Mathf.Min(z0, z1), b = alongX ? Mathf.Max(x0, x1) : Mathf.Max(z0, z1);
        var cuts = new List<float>(doors);
        cuts.Sort();
        float cur = a;
        foreach (float dc in cuts)
        {
            float e = dc - D * 0.5f;
            if (e > cur) Seg(alongX, x0, z0, cur, e, H, T);
            // lintel trim over the door
            float mid = dc;
            if (alongX) Box(new Vector3(mid, H - 0.2f, z0), new Vector3(D, 0.4f, T + 0.04f), Trim, true, WallLayer);
            else Box(new Vector3(x0, H - 0.2f, mid), new Vector3(T + 0.04f, 0.4f, D), Trim, true, WallLayer);
            cur = dc + D * 0.5f;
        }
        if (b > cur) Seg(alongX, x0, z0, cur, b, H, T);
    }

    void Seg(bool alongX, float x0, float z0, float from, float to, float H, float T)
    {
        float m = (from + to) * 0.5f, len = to - from;
        if (alongX)
        {
            BoxM(new Vector3(m, H * 0.5f, z0), new Vector3(len, H, T), WallMat, true, WallLayer);
            Box(new Vector3(m, H + 0.04f, z0), new Vector3(len + 0.02f, 0.08f, T + 0.08f), Trim, false, WallLayer);
            BoxM(new Vector3(m, 0.12f, z0), new Vector3(len, 0.24f, T + 0.06f), BaseMat, false, WallLayer);
        }
        else
        {
            BoxM(new Vector3(x0, H * 0.5f, m), new Vector3(T, H, len), WallMat, true, WallLayer);
            Box(new Vector3(x0, H + 0.04f, m), new Vector3(T + 0.08f, 0.08f, len + 0.02f), Trim, false, WallLayer);
            BoxM(new Vector3(x0, 0.12f, m), new Vector3(T + 0.06f, 0.24f, len), BaseMat, false, WallLayer);
        }
    }

    void Build()
    {
        built = true;
        root = new GameObject("HouseInterior").transform;
        root.position = Vector3.zero;
        // floor slab + room floors in different tones (no coplanar overlaps: each room tile is its own area)
        Box(new Vector3(0f, -0.3f, 0f), new Vector3(62f, 0.6f, 42f), new Color(0.4f, 0.32f, 0.25f), true);
        FloorTile(Living, new Color(0.62f, 0.46f, 0.3f));
        FloorTile(Kitchen, new Color(0.86f, 0.86f, 0.82f));
        FloorTile(Foyer, new Color(0.7f, 0.66f, 0.6f));
        FloorTile(Parlor, new Color(0.55f, 0.62f, 0.45f));
        FloorTile(Fish, new Color(0.35f, 0.5f, 0.62f));
        FloorTile(Reptile, new Color(0.62f, 0.55f, 0.38f));
        // perimeter (front door gap at x=0 on z=20) + inner walls
        Wall(-30f, 20f, 30f, 20f, 0f);
        Wall(-30f, -20f, 30f, -20f);
        Wall(-30f, -20f, -30f, 20f);
        Wall(30f, -20f, 30f, 20f);
        Wall(-30f, 4f, 30f, 4f, -18f, 0f, 18f);
        Wall(-6f, 4f, -6f, 20f, 10f);
        Wall(6f, 4f, 6f, 20f, 10f);
        Wall(-10f, -20f, -10f, 4f, -8f);
        Wall(10f, -20f, 10f, 4f, -8f);
        // outside: a bit of lawn and the porch beyond the front door so the doorway looks out onto something
        Box(new Vector3(0f, -0.35f, 30f), new Vector3(80f, 0.6f, 20f), new Color(0.32f, 0.5f, 0.2f), true);
        Box(new Vector3(0f, 0.1f, 22.5f), new Vector3(10f, 0.2f, 5f), Wood, true);
        // front door frame (open) + exit hotspot
        Box(new Vector3(-1.85f, 1.6f, 20.1f), new Vector3(0.3f, 3.2f, 0.5f), new Color(0.35f, 0.22f, 0.12f), true, WallLayer);
        Box(new Vector3(1.85f, 1.6f, 20.1f), new Vector3(0.3f, 3.2f, 0.5f), new Color(0.35f, 0.22f, 0.12f), true, WallLayer);
        var exit = Interact.Add(L(0f, 0.1f, 20.6f), 1.5f, "back outside", f => Exit(f), true);
        exit.enabled = f => f.world == WorldId.House;
        Ranch.Sign(L(0f, 4.0f, 19.6f), 180f, "FRONT DOOR\n<size=17>back to the ranch</size>", new Color(0.2f, 0.35f, 0.15f), 5f, 1.3f);

        LivingRoom();
        KitchenRoom();
        FoyerRoom();
        ParlorRoom();
        FishRoom();
        ReptileRoom();
        RoomSign(-18f, 4f, "PET PARLOR", true); RoomSign(0f, 4f, "FISH GALLERY", true); RoomSign(18f, 4f, "REPTILE ROOM", true);
        RoomSign(-6f, 10f, "LIVING ROOM", false); RoomSign(6f, 10f, "KITCHEN", false);

        foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        MeshMerge.Merge(root, false);
        foreach (var a in afterMerge) a();
        BuildLiving();
    }

    void FloorTile(Rect r, Color c)
    {
        // wood planks in the living rooms, laminate in the kitchen / fish gallery; the room tone tints the texture
        bool lam = r == Kitchen || r == Fish;
        Material m = Mats.TexTint(lam ? "LB/laminate" : "LB/woodfloor", Color.Lerp(c, Color.white, lam ? 0.55f : 0.45f), lam ? 0.35f : 0.25f, lam ? 3f : 2.5f);
        BoxM(new Vector3(r.center.x, 0.01f, r.center.y), new Vector3(r.width - 0.02f, 0.02f, r.height - 0.02f), m, false);
    }

    void RoomSign(float x, float z, string text, bool onXWall)
    {
        Ranch.Sign(L(x, 3.75f, z + (onXWall ? 0.2f : 0f)), onXWall ? 180f : 90f, text, new Color(0.15f, 0.2f, 0.35f), 4.2f, 0.7f);
        Ranch.Sign(L(x + (onXWall ? 0f : 0.2f), 3.75f, z - (onXWall ? 0.2f : 0f)), onXWall ? 0f : 270f, text, new Color(0.15f, 0.2f, 0.35f), 4.2f, 0.7f);
    }

    void LivingRoom()
    {
        Box(new Vector3(-18f, 0.03f, 12f), new Vector3(10f, 0.02f, 7f), Rug, false);
        // L sofa facing the TV wall
        Box(new Vector3(-18f, 0.45f, 16.5f), new Vector3(8f, 0.9f, 1.6f), Sofa);
        Box(new Vector3(-18f, 1.0f, 17.4f), new Vector3(8f, 1.1f, 0.4f), Sofa);
        Box(new Vector3(-23.2f, 0.45f, 13.5f), new Vector3(1.6f, 0.9f, 5f), Sofa);
        Box(new Vector3(-18f, 0.3f, 12f), new Vector3(3.2f, 0.6f, 1.8f), Wood);   // coffee table
        Box(new Vector3(-18f, 0.6f, 5.2f), new Vector3(6f, 1.2f, 0.9f), Wood);     // TV stand
        Box(new Vector3(-18f, 2.0f, 4.9f), new Vector3(5f, 2.6f, 0.15f), new Color(0.05f, 0.05f, 0.07f));
        Box(new Vector3(-18f, 2.0f, 5.0f), new Vector3(4.7f, 2.3f, 0.02f), new Color(0.15f, 0.45f, 0.3f), false);  // screen glow (Four Froggies on TV)
        Box(new Vector3(-29f, 1.5f, 10f), new Vector3(1.2f, 3f, 6f), Wood);   // bookshelf
        for (int i = 0; i < 12; i++) Box(new Vector3(-28.35f, 0.5f + (i / 4) * 0.9f, 7.6f + (i % 4) * 1.4f), new Vector3(0.1f, 0.6f, 1.1f), Color.HSVToRGB((i * 0.13f) % 1f, 0.6f, 0.8f), false);
        Plant(-28f, 18.5f); Plant(-8f, 18.5f);
    }

    void KitchenRoom()
    {
        Box(new Vector3(18f, 0.5f, 19f), new Vector3(20f, 1f, 1.4f), new Color(0.95f, 0.95f, 0.94f));
        Box(new Vector3(18f, 1.03f, 19f), new Vector3(20.1f, 0.06f, 1.5f), new Color(0.25f, 0.25f, 0.28f), false);
        Box(new Vector3(29f, 1.2f, 10f), new Vector3(1.4f, 2.4f, 1.8f), new Color(0.8f, 0.82f, 0.85f));   // fridge
        Box(new Vector3(18f, 0.5f, 12f), new Vector3(6f, 1f, 2.4f), new Color(0.95f, 0.95f, 0.94f));   // island
        Box(new Vector3(18f, 1.03f, 12f), new Vector3(6.1f, 0.06f, 2.5f), new Color(0.25f, 0.25f, 0.28f), false);
        Box(new Vector3(12f, 0.4f, 7f), new Vector3(3f, 0.8f, 2f), Wood);   // table
        for (int i = 0; i < 4; i++) Box(new Vector3(10.6f + (i % 2) * 2.8f, 0.25f, 5.6f + (i / 2) * 2.8f), new Vector3(0.6f, 0.5f, 0.6f), Wood);
        Prim(PrimitiveType.Sphere, new Vector3(17f, 1.2f, 12f), new Vector3(0.6f, 0.3f, 0.6f), new Color(0.9f, 0.6f, 0.2f));   // fruit bowl
    }

    void FoyerRoom()
    {
        Box(new Vector3(0f, 0.03f, 13f), new Vector3(4f, 0.02f, 9f), new Color(0.5f, 0.2f, 0.25f), false);
        Plant(-4.8f, 18.8f); Plant(4.8f, 18.8f);
        Box(new Vector3(-5.2f, 0.9f, 15f), new Vector3(0.8f, 1.8f, 2.4f), Wood);   // shoe bench + coats
        // ffu14: off to the side and lower, so it no longer overlaps the FISH GALLERY room sign above the doorway
        Ranch.Sign(L(3.4f, 1.9f, 5.6f), 0f, "<size=24>FOUR FROGGIES</size>\n<size=17>James - Jimmy - Bubbles - Rexy</size>", new Color(0.12f, 0.35f, 0.15f), 4.6f, 1.2f);   // ffu15: wider + sized so the title never wraps "FROGGIE/S"
    }

    void Plant(float x, float z)
    {
        Prim(PrimitiveType.Cylinder, new Vector3(x, 0.35f, z), new Vector3(0.7f, 0.35f, 0.7f), new Color(0.7f, 0.4f, 0.25f), true);
        Prim(PrimitiveType.Sphere, new Vector3(x, 1.2f, z), new Vector3(1.2f, 1.4f, 1.2f), new Color(0.2f, 0.5f, 0.22f));
    }

    void ParlorRoom()
    {
        // cat tree, dog beds, toy box, cat bed, bunny nook
        Box(new Vector3(-27.5f, 0.9f, -2f), new Vector3(0.4f, 1.8f, 0.4f), new Color(0.8f, 0.7f, 0.55f));
        Box(new Vector3(-27.5f, 1.85f, -2f), new Vector3(1.6f, 0.15f, 1.6f), new Color(0.6f, 0.45f, 0.65f));
        Box(new Vector3(-27.5f, 0.9f, -2f), new Vector3(1.2f, 0.12f, 1.2f), new Color(0.6f, 0.45f, 0.65f));
        Prim(PrimitiveType.Cylinder, new Vector3(-14f, 0.12f, -17f), new Vector3(2f, 0.12f, 1.4f), new Color(0.35f, 0.25f, 0.5f));
        Prim(PrimitiveType.Cylinder, new Vector3(-17f, 0.12f, -17.5f), new Vector3(1.4f, 0.12f, 1.1f), new Color(0.5f, 0.25f, 0.3f));
        toyBox = new Vector3(-20f, 0f, 2.2f);
        Box(toyBox + Vector3.up * 0.45f, new Vector3(1.8f, 0.9f, 1.1f), new Color(0.9f, 0.3f, 0.25f));
        Ranch.Sign(L(toyBox.x, 1.6f, toyBox.z + 0.6f), 180f, "TOY BOX\n<size=20>fetch!</size>", new Color(0.6f, 0.15f, 0.12f), 1.8f, 0.8f);
        Prim(PrimitiveType.Cylinder, new Vector3(-27.5f, 0.1f, -8f), new Vector3(1.6f, 0.1f, 1.6f), new Color(0.95f, 0.75f, 0.85f));
        Ranch.Sign(L(-27.5f, 1.3f, -9.2f), 0f, "CAT BED\n<size=20>hide-and-seek</size>", new Color(0.5f, 0.2f, 0.45f), 1.8f, 0.8f);
        // bunny nook pen (low fence)
        Box(new Vector3(-22f, 0.3f, -12f), new Vector3(6f, 0.6f, 0.12f), Trim);
        Box(new Vector3(-22f, 0.3f, -16f), new Vector3(6f, 0.6f, 0.12f), Trim);
        Box(new Vector3(-25f, 0.3f, -14f), new Vector3(0.12f, 0.6f, 4f), Trim);
        Box(new Vector3(-19f, 0.3f, -14f), new Vector3(0.12f, 0.6f, 4f), Trim);
        Box(new Vector3(-22f, 0.02f, -14f), new Vector3(5.8f, 0.02f, 3.8f), new Color(0.75f, 0.68f, 0.35f), false);
    }

    void FishRoom()
    {
        AddTank("Big reef tank", new Vector3(0f, 0f, -9f), new Vector3(5f, 1.9f, 2.2f), true);
        AddTank("Rainbow fish tank", new Vector3(-5.5f, 0f, -18.7f), new Vector3(4f, 1.5f, 1.3f), false);
        AddTank("Coral party tank", new Vector3(5.5f, 0f, -18.7f), new Vector3(4f, 1.5f, 1.3f), false);
        AddTank("Upstairs bubbler tank", new Vector3(-8.7f, 0f, -3f), new Vector3(1.3f, 1.4f, 3.6f), false);
        AddTank("Loft aquarium", new Vector3(8.7f, 0f, -3f), new Vector3(1.3f, 1.4f, 3.6f), false);
        Box(new Vector3(0f, 0.25f, -4f), new Vector3(3f, 0.5f, 0.8f), Wood);   // viewing bench
    }

    void AddTank(string name, Vector3 lp, Vector3 size, bool big)
    {
        float standH = 0.9f;
        Box(lp + Vector3.up * standH * 0.5f, new Vector3(size.x + 0.2f, standH, size.z + 0.2f), Wood);
        Vector3 c = lp + Vector3.up * (standH + size.y * 0.5f);
        var tint = new Material(Mats.Glass);
        tint.color = new Color(0.45f, 0.8f, 1f, 0.22f);
        var glass = Mats.Prim(PrimitiveType.Cube, root, L(c.x, c.y, c.z), size, tint, true);
        glass.layer = WallLayer;
        Box(c + Vector3.up * (size.y * 0.5f + 0.06f), new Vector3(size.x + 0.08f, 0.12f, size.z + 0.08f), new Color(0.1f, 0.1f, 0.12f), false);
        Box(c - Vector3.up * (size.y * 0.5f - 0.08f), new Vector3(size.x - 0.06f, 0.14f, size.z - 0.06f), new Color(0.85f, 0.78f, 0.55f), false);   // sand
        var rnd = new System.Random(name.Length * 7);
        for (int i = 0; i < (big ? 8 : 4); i++)
        {
            Vector3 p = c + new Vector3(((float)rnd.NextDouble() - 0.5f) * size.x * 0.8f, -size.y * 0.5f + 0.25f, ((float)rnd.NextDouble() - 0.5f) * size.z * 0.6f);
            Color cc = Color.HSVToRGB((float)rnd.NextDouble(), 0.7f, 0.95f);
            if (i % 2 == 0) Prim(PrimitiveType.Sphere, p, new Vector3(0.35f, 0.4f, 0.35f), cc);   // coral
            else Prim(PrimitiveType.Cylinder, p + Vector3.up * 0.2f, new Vector3(0.06f, 0.45f, 0.06f), new Color(0.2f, 0.65f, 0.3f));   // weed
        }
        var t = new Tank { name = name, c = L(c.x, c.y, c.z), half = size * 0.5f };
        int n = big ? 18 : 11;
        Color[] cols = { new Color(1f, 0.55f, 0.1f), new Color(0.1f, 0.6f, 1f), new Color(1f, 0.85f, 0.1f), new Color(0.9f, 0.2f, 0.5f), new Color(0.3f, 0.95f, 0.8f), Color.white };
        for (int i = 0; i < n; i++)
        {
            bool large = i < (big ? 3 : 1);
            float s = large ? Random.Range(0.32f, 0.45f) : Random.Range(0.1f, 0.18f);
            var fg = new GameObject("Fish");
            fg.transform.SetParent(transform, false);
            Color fc = cols[(i + name.Length) % cols.Length];
            Mats.Prim(PrimitiveType.Sphere, fg.transform, Vector3.zero, new Vector3(s * 0.45f, s * 0.6f, s), Mats.Lit(fc));
            Mats.Prim(PrimitiveType.Cube, fg.transform, new Vector3(0f, 0f, -s * 0.6f), new Vector3(s * 0.05f, s * 0.5f, s * 0.35f), Mats.Lit(Color.Lerp(fc, Color.black, 0.2f)));
            if (large) Mats.Prim(PrimitiveType.Cube, fg.transform, new Vector3(0f, s * 0.32f, 0f), new Vector3(s * 0.04f, s * 0.3f, s * 0.4f), Mats.Lit(fc));
            Mats.NoShadows(fg);
            var f = new FishT { t = fg.transform, a = Random.value * 6.28f, sp = Random.Range(0.5f, 1.1f) * (large ? 0.5f : 1f), rx = Random.Range(0.4f, 0.85f), rz = Random.Range(0.3f, 0.8f), y = Random.Range(-0.3f, 0.35f), size = s };
            f.pos = t.c;
            t.fish.Add(f);
        }
        tanks.Add(t);
        var hs = Interact.Add(t.c + Vector3.down * (size.y * 0.5f + standH) + new Vector3(0f, 0f, 0f), Mathf.Max(size.x, size.z) * 0.5f + 1.8f, "feed the fish", f => Feed(t, f));
        hs.enabled = f => f.world == WorldId.House;
        hs.dynLabel = f => t.fed ? t.name + " (already fed - come back later)" : "sprinkle food in the " + t.name;
    }

    void ReptileRoom()
    {
        Color[] snakeC = { new Color(0.95f, 0.75f, 0.2f), new Color(0.25f, 0.55f, 0.2f), new Color(0.9f, 0.45f, 0.15f), new Color(0.35f, 0.3f, 0.25f) };
        Color[] bandC = { new Color(1f, 1f, 0.9f), new Color(0.1f, 0.25f, 0.1f), new Color(0.1f, 0.1f, 0.1f), new Color(0.75f, 0.6f, 0.35f) };
        Vector3[] spots = { new Vector3(14f, 0f, -18.6f), new Vector3(20f, 0f, -18.6f), new Vector3(26f, 0f, -18.6f), new Vector3(28.6f, 0f, -10f) };
        for (int i = 0; i < 4; i++)
        {
            Vector3 size = i == 3 ? new Vector3(1.6f, 1.3f, 4f) : new Vector3(4f, 1.3f, 1.6f);
            Vector3 c = Terrarium(spots[i], size);
            int k = i;
            Vector3 sz = size;
            afterMerge.Add(() => Animal.Snake(root, L(c.x, c.y - sz.y * 0.5f + 0.15f, c.z), sz * 0.45f, snakeC[k], bandC[k]));
        }
        // lizard terrarium (open top) + basking rock
        Vector3 lc = Terrarium(new Vector3(20f, 0f, -6f), new Vector3(5f, 1.1f, 3f));
        for (int j = 0; j < 3; j++)
            afterMerge.Add(() => {
            int i = lizN++;
            var a = Animal.Lizard(L(lc.x + i - 1f, lc.y - 0.45f, lc.z), i == 1 ? new Color(0.35f, 0.6f, 0.25f) : new Color(0.6f, 0.55f, 0.3f));
            a.transform.SetParent(root, true);
            a.Init(new Rect(L(lc.x, 0, lc.z).x - 2.2f, L(lc.x, 0, lc.z).z - 1.2f, 4.4f, 2.4f), L(0, lc.y - 0.5f, 0).y);
            });
        Ranch.Sign(L(20f, 2.2f, -1.2f), 180f, "SNAKES + LIZARDS\n<size=20>look, don't tap the glass</size>", new Color(0.3f, 0.4f, 0.15f), 3.2f, 1f);
    }

    Vector3 Terrarium(Vector3 lp, Vector3 size)
    {
        float standH = 0.9f;
        Box(lp + Vector3.up * standH * 0.5f, new Vector3(size.x + 0.2f, standH, size.z + 0.2f), Wood);
        Vector3 c = lp + Vector3.up * (standH + size.y * 0.5f);
        var tint = new Material(Mats.Glass);
        tint.color = new Color(0.85f, 0.95f, 0.85f, 0.15f);
        var glass = Mats.Prim(PrimitiveType.Cube, root, L(c.x, c.y, c.z), size, tint, true);
        glass.layer = WallLayer;
        Box(c - Vector3.up * (size.y * 0.5f - 0.06f), new Vector3(size.x - 0.06f, 0.1f, size.z - 0.06f), new Color(0.55f, 0.4f, 0.25f), false);
        Box(c + new Vector3(size.x * 0.2f, -size.y * 0.5f + 0.25f, 0f), new Vector3(size.x * 0.5f, 0.08f, 0.1f), new Color(0.4f, 0.28f, 0.15f), false, 0, new Vector3(0f, 25f, 12f));
        Prim(PrimitiveType.Sphere, c + new Vector3(-size.x * 0.25f, -size.y * 0.5f + 0.15f, 0f), new Vector3(0.6f, 0.3f, 0.5f), new Color(0.5f, 0.5f, 0.48f));
        Mats.Prim(PrimitiveType.Cylinder, root, L(c.x, c.y + size.y * 0.5f + 0.25f, c.z), new Vector3(0.3f, 0.12f, 0.3f), Mats.Unlit(new Color(1f, 0.6f, 0.3f)), false);   // heat lamp
        return c;
    }

    // ---------------- living things ----------------
    void BuildLiving()
    {
        // cats (roster names + coats)
        string[] names = { "Spotty", "Tigy", "Kitty", "Little White Socks" };
        Color[] fur = { new Color(0.95f, 0.93f, 0.88f), new Color(0.9f, 0.55f, 0.2f), new Color(0.35f, 0.35f, 0.38f), new Color(0.12f, 0.12f, 0.13f) };
        Color[] patch = { new Color(0.15f, 0.12f, 0.1f), new Color(0.5f, 0.25f, 0.08f), new Color(0.6f, 0.6f, 0.62f), new Color(0.12f, 0.12f, 0.13f) };
        for (int i = 0; i < 4; i++)
        {
            var c = Animal.Cat(names[i], L(-24f + i * 3f, 0f, -6f), fur[i], patch[i], i == 3);
            c.transform.SetParent(root, true);
            c.Init(WorldRect(Parlor, 1f), Worlds.HouseO.y);
            cats.Add(c);
        }
        germy = Animal.Dog("Germy", L(-15f, 0f, -14f), true);
        daisy = Animal.Dog("Daisy", L(-17f, 0f, -15f), false);
        foreach (var d in new[] { germy, daisy }) { d.transform.SetParent(root, true); d.Init(WorldRect(Parlor, 1f), Worlds.HouseO.y); }
        for (int i = 0; i < 3; i++)
        {
            var r = Animal.Rabbit(L(-23f + i * 1.2f, 0f, -14f), i == 1 ? new Color(0.55f, 0.42f, 0.3f) : new Color(0.95f, 0.95f, 0.95f));
            r.transform.SetParent(root, true);
            r.Init(new Rect(L(-24.6f, 0, 0).x, L(0, 0, -15.6f).z, 5.2f, 3.2f), Worlds.HouseO.y);
        }
        dad = Dad.Create(root, L(18f, 0f, 9f));
        var dh = Interact.Add(dad.transform.position, 2.6f, "talk to Dad", f => dad.Talk(f));
        dh.enabled = f => f.world == WorldId.House;
        dad.hotspot = dh;
        var tb = Interact.Add(L(toyBox.x, 0f, toyBox.z), 2.2f, "throw the ball for Germy and Daisy", f => Throw(f));
        tb.enabled = f => f.world == WorldId.House;
        var cb = Interact.Add(L(-27.5f, 0f, -8f), 2.2f, "play hide-and-seek with the cats", f => StartSeek(f));
        cb.enabled = f => f.world == WorldId.House && !seeking;
        // name tags float over the named pets
        foreach (var a in new List<Animal>(cats) { germy, daisy }) NameTag(a);
    }

    static Rect WorldRect(Rect local, float inset)
    {
        return new Rect(Worlds.HouseO.x + local.xMin + inset, Worlds.HouseO.z + local.yMin + inset, local.width - inset * 2f, local.height - inset * 2f);
    }

    void NameTag(Animal a)
    {
        var go = new GameObject("Tag");
        go.transform.SetParent(a.transform, false);
        go.transform.localPosition = Vector3.up * (a.kind == Animal.Kind.Dog && a.petName == "Germy" ? 1.3f : 0.95f);
        var tm = go.AddComponent<TextMesh>();
        tm.text = a.petName;
        tm.font = UIK.Font;
        go.GetComponent<MeshRenderer>().sharedMaterial = UIK.Font != null ? UIK.Font.material : null;
        tm.fontSize = 48;
        tm.characterSize = 0.035f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = Color.white;
        go.AddComponent<Billboard>();
    }

    // ---------------- mini-games ----------------
    void Feed(Tank t, Frog f)
    {
        if (t.fed) { f.Toast(t.name + " is full. Try another tank.", 2f); return; }
        if (t.flakes.Count > 0) return;
        for (int i = 0; i < 10; i++)
        {
            var fl = Mats.Prim(PrimitiveType.Cube, transform, t.c + new Vector3(Random.Range(-t.half.x, t.half.x) * 0.8f, t.half.y * 0.9f, Random.Range(-t.half.z, t.half.z) * 0.7f), Vector3.one * 0.06f, Mats.Lit(new Color(0.9f, 0.6f, 0.3f)));
            t.flakes.Add(fl.transform);
        }
        f.Toast("Sprinkle! Watch them gobble it up.", 2f);
        Sfx.Play(Sfx.Splash, 0.4f, 1.6f);
    }

    void Throw(Frog f)
    {
        if (ball == null)
        {
            var g = Mats.Prim(PrimitiveType.Sphere, transform, Vector3.zero, Vector3.one * 0.36f, Mats.Shiny(new Color(0.75f, 1f, 0.2f)), true);
            g.layer = Vehicle.PropLayer;
            ball = g.AddComponent<Rigidbody>();
            ball.mass = 0.3f;
            ball.drag = 0.3f;
            var pm = new PhysicMaterial("Ball") { bounciness = 0.6f, bounceCombine = PhysicMaterialCombine.Maximum, dynamicFriction = 0.4f };
            g.GetComponent<Collider>().sharedMaterial = pm;
        }
        if (carrier != null) return;
        thrower = f;
        ball.isKinematic = false;
        ball.transform.SetParent(transform, true);
        Vector3 fwd = f.transform.forward;
        ball.position = f.transform.position + fwd * 0.9f + Vector3.up * 1.2f;
        ball.velocity = fwd * 9f + Vector3.up * 3.5f;
        ballIdle = 0f;
        germy.follow = ball.transform; germy.followDist = 0.35f;
        daisy.follow = ball.transform; daisy.followDist = 0.35f;
        f.Toast("Fetch, Germy! Fetch, Daisy!", 2f);
        Sfx.Play(Sfx.Hop, 0.6f, 0.7f);
    }

    void StartSeek(Frog f)
    {
        seeking = true; seekT = 90f; found = 0;
        var spots = new List<Vector3>(HideSpots);
        for (int i = 0; i < 4; i++)
        {
            int k = Random.Range(0, spots.Count);
            Vector3 s = spots[k]; spots.RemoveAt(k);
            Animal c = cats[i];
            catFound[i] = false;
            c.transform.position = L(s.x, 0f, s.z);
            c.area = new Rect(L(s.x, 0, s.z).x - 0.2f, L(s.x, 0, s.z).z - 0.2f, 0.4f, 0.4f);
            c.frozen = true;
            c.skittish = false;
            if (!c.gameObject.activeSelf) { catFound[i] = true; found++; }   // ffu14: a player is that cat right now
        }
        Toast("The cats are hiding all over the house! Find all " + (4 - found) + " in 90 s");
        Sfx.Play(Sfx.Click, 0.8f);
    }

    void Toast(string s, float t = 3f)
    {
        if (Game.I == null) return;
        foreach (Frog f in Game.I.frogs) if (f != null && f.human && f.world == WorldId.House) f.Toast(s, t);
    }

    void EndSeek(bool won)
    {
        seeking = false;
        for (int i = 0; i < 4; i++) { cats[i].frozen = false; cats[i].skittish = true; cats[i].area = WorldRect(Parlor, 1f); cats[i].target = cats[i].Pos; }
        if (won) { Toast("Found every cat with " + Mathf.CeilToInt(seekT) + " s to spare!", 4f); Sfx.Play(Sfx.Win, 0.9f); }
        else Toast("Time's up - the cats win this round. Try again at the cat bed!", 4f);
    }

    // ---------------- update ----------------
    void Update()
    {
        if (!built) return;
        bool anyone = false;
        if (Game.I != null) foreach (Frog f in Game.I.frogs) if (f != null && f.world == WorldId.House) anyone = true;
        if (root.gameObject.activeSelf != anyone) { root.gameObject.SetActive(anyone); foreach (var t in tanks) foreach (var fi in t.fish) fi.t.gameObject.SetActive(anyone); }
        if (!anyone) return;
        HidePlayedPets();
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        UpdateFish(dt);
        UpdateFetch(dt);
        if (seeking) UpdateSeek(dt);
    }

    void UpdateFish(float dt)
    {
        foreach (Tank t in tanks)
        {
            if (t.fed && Time.time - t.fedT > 90f) t.fed = false;
            // flakes sink to the sand
            for (int i = t.flakes.Count - 1; i >= 0; i--)
            {
                Transform fl = t.flakes[i];
                Vector3 p = fl.position;
                if (p.y > t.c.y - t.half.y + 0.2f) p.y -= dt * 0.22f;
                p.x += Mathf.Sin(Time.time * 2f + i) * dt * 0.05f;
                fl.position = p;
            }
            foreach (FishT f in t.fish)
            {
                Vector3 want;
                Transform food = null; float fd = 1e9f;
                foreach (Transform fl in t.flakes) { float d = (fl.position - f.pos).sqrMagnitude; if (d < fd) { fd = d; food = fl; } }
                if (food != null) want = food.position;
                else
                {
                    f.a += f.sp * dt * 0.8f;
                    want = t.c + new Vector3(Mathf.Cos(f.a) * t.half.x * f.rx, f.y * t.half.y + Mathf.Sin(f.a * 2.3f) * 0.08f, Mathf.Sin(f.a) * t.half.z * f.rz);
                }
                Vector3 to = want - f.pos;
                float sp = food != null ? 1.2f : f.sp * 0.6f;
                Vector3 step = Vector3.ClampMagnitude(to, sp * dt);
                f.pos += step;
                Vector3 lim = t.half - Vector3.one * (f.size * 0.6f);
                Vector3 rel = f.pos - t.c;
                rel = new Vector3(Mathf.Clamp(rel.x, -lim.x, lim.x), Mathf.Clamp(rel.y, -lim.y, lim.y), Mathf.Clamp(rel.z, -lim.z, lim.z));
                f.pos = t.c + rel;
                f.t.position = f.pos;
                if (step.sqrMagnitude > 1e-8f) f.t.rotation = Quaternion.Slerp(f.t.rotation, Quaternion.LookRotation(step.normalized), dt * 6f);
                if (food != null && fd < 0.02f) { t.flakes.Remove(food); Destroy(food.gameObject); }
            }
        }
        // a feeding finishes when the last flake is gone
        foreach (Tank t in tanks)
        {
            if (t.fed || t.flakes.Count > 0) continue;
            if (t.fedT < 0f)
            {
                t.fed = true; t.fedT = Time.time;
                fedCount = 0;
                foreach (Tank u in tanks) if (u.fed) fedCount++;
                if (fedCount >= tanks.Count) { Toast("All five tanks fed! The fish are doing happy laps.", 4f); Sfx.Play(Sfx.Win, 0.9f); }
                else { Toast("Fed the " + t.name + " (" + fedCount + " / " + tanks.Count + ")", 2.5f); Sfx.Play(Sfx.Pickup, 0.7f); }
            }
        }
        foreach (Tank t in tanks) if (t.flakes.Count > 0) t.fedT = -1f;
    }

    // ffu14: a pet a player is playing as is not also sitting in the parlor
    void HidePlayedPets()
    {
        if (Game.I == null || Time.frameCount % 15 != 0) return;
        foreach (var a in new List<Animal>(cats) { germy, daisy })
        {
            if (a == null) continue;
            bool played = false;
            for (int i = 0; i < 4; i++) if (Roster.Name(Game.I.charOf[i]) == a.petName) played = true;
            if (a.gameObject.activeSelf == played) a.gameObject.SetActive(!played);
        }
    }

    void UpdateFetch(float dt)
    {
        if (ball == null || ball.isKinematic && carrier == null) return;
        Vector3 bp = ball.transform.position;
        if (carrier == null)
        {
            if (ball.velocity.magnitude < 0.6f) ballIdle += dt;
            foreach (Animal d in new[] { germy, daisy })
            {
                if (!d.gameObject.activeSelf) continue;
                Vector3 dd = d.Pos - bp; dd.y = 0f;
                if (dd.magnitude < 0.6f && ball.velocity.magnitude < 4f)
                {
                    carrier = d;
                    ball.isKinematic = true;
                    ball.transform.SetParent(d.head, false);
                    ball.transform.localPosition = new Vector3(0f, -0.05f, 0.32f);
                    Animal other = d == germy ? daisy : germy;
                    other.follow = null;
                    d.follow = thrower != null ? thrower.transform : null;
                    d.followDist = 1.3f;
                    break;
                }
            }
            if (ballIdle > 8f) { ResetBall(); }
        }
        else
        {
            if (thrower == null || thrower.world != WorldId.House) { ResetBall(); return; }
            Vector3 d = carrier.Pos - thrower.transform.position; d.y = 0f;
            if (d.magnitude < 1.9f)
            {
                fetches++;
                thrower.Toast(carrier.petName + " brought it back! (" + fetches + (fetches >= 5 ? ")  Good dogs!" : " / 5)"), 2.5f);
                if (fetches % 5 == 0) Sfx.Play(Sfx.Win, 0.8f); else Sfx.Play(Sfx.Pickup, 0.7f);
                ball.transform.SetParent(transform, true);
                ball.transform.position = carrier.Pos + carrier.transform.forward * 0.5f + Vector3.up * 0.3f;
                ball.isKinematic = false;
                ball.velocity = Vector3.zero;
                carrier.follow = null;
                carrier = null;
                ballIdle = 0f;
            }
        }
    }

    void ResetBall()
    {
        if (carrier != null) { carrier.follow = null; carrier = null; }
        germy.follow = null; daisy.follow = null;
        ball.transform.SetParent(transform, true);
        ball.isKinematic = true;
        ball.transform.position = L(toyBox.x, 1.2f, toyBox.z);
    }

    void UpdateSeek(float dt)
    {
        seekT -= dt;
        for (int i = 0; i < 4; i++)
        {
            if (catFound[i]) continue;
            Animal c = cats[i];
            foreach (Frog f in Game.I.frogs)
            {
                if (f == null || !f.human || f.world != WorldId.House) continue;
                Vector3 d = f.transform.position - c.Pos; d.y = 0f;
                if (d.magnitude < 1.9f)
                {
                    catFound[i] = true; found++;
                    c.frozen = false; c.area = WorldRect(Parlor, 1f); c.target = c.Pos; c.skittish = true;
                    f.Toast("Found " + c.petName + "! (" + found + " / 4)", 2.5f);
                    Sfx.Play(Sfx.Pickup, 0.8f, 1.2f);
                    break;
                }
            }
        }
        if (found >= 4) EndSeek(true);
        else if (seekT <= 0f) EndSeek(false);
        else if (Mathf.FloorToInt(seekT + dt) != Mathf.FloorToInt(seekT) && Mathf.FloorToInt(seekT) % 15 == 0) Toast("Hide-and-seek: " + found + " / 4 found, " + Mathf.CeilToInt(seekT) + " s left", 2f);
    }
}

// James's dad (named "Dad" in the three.js house). Stands in the kitchen, turns to whoever comes close.
public class Dad : MonoBehaviour
{
    public Hotspot hotspot;
    Transform armR, head;
    int line;
    float wave;
    static readonly string[] Lines = {
        "Dad: Good to see you, James.",
        "Dad: The fish are hungry - give every tank a sprinkle.",
        "Dad: Germy and Daisy want to play fetch. The toy box is in the pet parlor.",
        "Dad: The cats love hide-and-seek. Start it at the cat bed.",
        "James's dad waves.",
    };

    public static Dad Create(Transform parent, Vector3 pos)
    {
        var go = new GameObject("Dad");
        go.transform.SetParent(parent, true);
        go.transform.position = pos;
        var d = go.AddComponent<Dad>();
        Color skin = new Color(0.93f, 0.76f, 0.6f), shirt = new Color(0.25f, 0.4f, 0.65f), jeans = new Color(0.2f, 0.25f, 0.38f);
        Transform t = go.transform;
        for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, t, new Vector3(0.14f * s, 0.45f, 0f), new Vector3(0.2f, 0.9f, 0.24f), Mats.Lit(jeans));
        Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 1.25f, 0f), new Vector3(0.55f, 0.75f, 0.3f), Mats.Lit(shirt));
        d.head = Mats.Node(t, "Head", new Vector3(0f, 1.82f, 0f));
        Mats.Prim(PrimitiveType.Sphere, d.head, Vector3.zero, new Vector3(0.32f, 0.36f, 0.32f), Mats.Lit(skin));
        Mats.Prim(PrimitiveType.Sphere, d.head, new Vector3(0f, 0.12f, -0.02f), new Vector3(0.34f, 0.2f, 0.34f), Mats.Lit(new Color(0.35f, 0.25f, 0.15f)));
        for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Sphere, d.head, new Vector3(0.07f * s, 0.02f, 0.15f), Vector3.one * 0.04f, Mats.Lit(Color.black));
        Mats.Prim(PrimitiveType.Cube, t, new Vector3(-0.36f, 1.2f, 0f), new Vector3(0.14f, 0.7f, 0.14f), Mats.Lit(shirt));
        d.armR = Mats.Node(t, "ArmR", new Vector3(0.36f, 1.55f, 0f));
        Mats.Prim(PrimitiveType.Cube, d.armR, new Vector3(0f, -0.35f, 0f), new Vector3(0.14f, 0.7f, 0.14f), Mats.Lit(shirt));
        Mats.Prim(PrimitiveType.Sphere, d.armR, new Vector3(0f, -0.74f, 0f), Vector3.one * 0.13f, Mats.Lit(skin));
        var col = go.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 0.95f, 0f); col.height = 1.9f; col.radius = 0.35f;
        var tag = new GameObject("Tag");
        tag.transform.SetParent(t, false);
        tag.transform.localPosition = Vector3.up * 2.35f;
        var tm = tag.AddComponent<TextMesh>();
        tm.text = "Dad"; tm.font = UIK.Font; tm.fontSize = 48; tm.characterSize = 0.04f; tm.anchor = TextAnchor.MiddleCenter;
        tag.GetComponent<MeshRenderer>().sharedMaterial = UIK.Font != null ? UIK.Font.material : null;
        tag.AddComponent<Billboard>();
        return d;
    }

    public void Talk(Frog f)
    {
        string s = Lines[line % Lines.Length];
        if (line % Lines.Length == 0 && f.nick != "James") s = "Dad: Hi " + f.nick + "! Is James with you?";
        line++;
        wave = 1.8f;
        f.Toast(s, 3.5f);
        Sfx.Play(Sfx.Click, 0.5f, 0.7f);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        wave = Mathf.Max(0f, wave - dt);
        Frog near = null; float nd = 6f;
        if (Game.I != null) foreach (Frog f in Game.I.frogs) if (f != null && f.world == WorldId.House) { float d = (f.transform.position - transform.position).magnitude; if (d < nd) { nd = d; near = f; } }
        if (near != null)
        {
            Vector3 to = near.transform.position - transform.position; to.y = 0f;
            if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), dt * 3f);
        }
        armR.localRotation = Quaternion.Euler(0f, 0f, wave > 0f ? 150f + Mathf.Sin(Time.time * 12f) * 20f : Mathf.Sin(Time.time) * 3f);
        head.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 0.8f) * 3f, 0f, 0f);
    }
}

// keeps a world-space label facing the camera that is rendering it
public class Billboard : MonoBehaviour
{
    void OnWillRenderObject()
    {
        Camera c = Camera.current;
        if (c != null) transform.rotation = Quaternion.LookRotation(transform.position - c.transform.position);
    }
}
