using System.Collections.Generic;
using UnityEngine;

// ffu64 CAMPER WALK-IN. Every camper trailer (Silver Bullet, Teardrop, Toy Hauler, Cyber Camper) gets a real curb-side door
// that swings open on E / A (door sound) and a furnished interior: bed, dinette table + bench, tiny kitchen (counter, sink,
// two-burner stove, fridge, cabinets), a table lamp + ceiling light you can switch, windows with daylight, rug.
// The interior is a small room built on first use inside the indoor world (WorldId.House space, far from James's house:
// Worlds.HouseO + (170 + style * 30, 0, 0)), like the house itself, so the trailer's physics / towing / hitch never change.
// Walls, floor, ceiling and furniture are real colliders (HouseWorld.WallLayer) - the froggy bumps into them and the camera
// (CamRig: tight-space rule while In(f)) pulls in close under the ceiling. "go outside" by the door puts you back next to
// the trailer wherever it is now (it may have been towed away meanwhile). Knob &camper64=0 = no doors.
[DefaultExecutionOrder(60)]
public class Camper64 : MonoBehaviour
{
    public static Camper64 I;
    public static bool On = true;
    public const int Layer = HouseWorld.WallLayer;
    public static int Enters, Exits, LampFlips;

    public class DoorSpec { public float z, y, h, w; }
    public class Door { public Trailer tr; public Transform hinge; public float k, want, hold; public DoorSpec d; public Hotspot hs; }
    public class Room
    {
        public int style; public string name; public Vector3 o; public Transform root; public float L, W, H; public DoorSpec d;
        public Light lamp, ceil; public bool lampOn = true; public Renderer shade; public Transform hinge; public float k, want, hold;
        public Trailer last; public Bounds box;
    }
    readonly List<Door> doors = new List<Door>();
    readonly Dictionary<int, Room> rooms = new Dictionary<int, Room>();
    readonly Dictionary<Frog, Trailer> inside = new Dictionary<Frog, Trailer>();
    class Pending { public Frog f; public Door door; public float t; }
    readonly List<Pending> pend = new List<Pending>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        On = Ffu33.Url("camper64") != "0";
        I = new GameObject("camper64").AddComponent<Camper64>();
        DontDestroyOnLoad(I.gameObject);
    }

    public static DoorSpec Spec(Trailer.Spec s)
    {
        float F = s.floorY;
        switch (s.style)
        {
            case 0: return new DoorSpec { z = -0.65f, y = F + 1.0f, h = 1.85f, w = 0.72f };
            case 1: return new DoorSpec { z = 0.55f, y = F + 0.72f, h = 1.0f, w = 0.85f };
            case 2: return new DoorSpec { z = -0.75f, y = F + 1.05f, h = 1.95f, w = 0.75f };
            case 3: return new DoorSpec { z = 0.15f, y = F + 0.98f, h = 1.6f, w = 0.8f };
        }
        return null;
    }

    // ---------------- queries ----------------
    public Room RoomAt(Vector3 p)
    {
        foreach (var r in rooms.Values) if (r.box.Contains(p)) return r;
        return null;
    }
    public static bool In(Frog f) { return I != null && f != null && f.world == WorldId.House && I.RoomAt(f.transform.position) != null; }
    public static string NameAt(Vector3 p) { if (I == null) return null; var r = I.RoomAt(p); return r != null ? r.name + " camper" : null; }
    public Door DoorOf(Trailer t) { foreach (var d in doors) if (d.tr == t) return d; return null; }
    public Room RoomOf(int style) { Room r; rooms.TryGetValue(style, out r); return r; }

    static bool Moving(Trailer t) { return t.Speed > 1.2f || (t.Hitched && t.tower != null && t.tower.Speed > 1f); }

    // ---------------- exterior door ----------------
    void Register(Trailer t)
    {
        var d = Spec(t.spec); if (d == null) return;
        var door = new Door { tr = t, d = d };
        float W = t.spec.wid;
        Transform tt = t.transform;
        // dark doorway behind the slab (seen when it swings open) + the slab on a hinge at its front edge
        var hole = Mats.Prim(PrimitiveType.Cube, tt, new Vector3(W * 0.5f + 0.03f, d.y, d.z), new Vector3(0.006f, d.h - 0.06f, d.w - 0.06f), Mats.Unlit(new Color(0.16f, 0.12f, 0.09f)));
        hole.name = "Doorway64";
        door.hinge = Mats.Node(tt, "Door64", new Vector3(W * 0.5f + 0.055f, d.y, d.z + d.w * 0.5f));
        Color c = t.spec.style == 1 ? new Color(0.18f, 0.18f, 0.19f) : t.spec.main * 0.93f;
        Mats.Prim(PrimitiveType.Cube, door.hinge, new Vector3(0f, 0f, -d.w * 0.5f), new Vector3(0.04f, d.h, d.w), t.spec.style == 0 || t.spec.style == 3 ? Mats.PBR(c, 0.8f, 0.85f) : Mats.Paint(c, 0.5f));
        Mats.Prim(PrimitiveType.Cube, door.hinge, new Vector3(0.03f, d.h * 0.22f, -d.w * 0.5f), new Vector3(0.02f, Mathf.Min(0.4f, d.h * 0.3f), d.w * 0.6f), Mats.GlassTint(new Color(0.08f, 0.12f, 0.16f)));
        Mats.Prim(PrimitiveType.Cube, door.hinge, new Vector3(0.05f, -d.h * 0.05f, -d.w * 0.86f), new Vector3(0.04f, 0.04f, 0.1f), Mats.Steel(new Color(0.6f, 0.61f, 0.63f)));
        foreach (var r in door.hinge.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        door.hs = Interact.Add(DoorFront(door, 0.85f), 1.5f, "", f => Use(f, door));
        door.hs.enabled = f => On && f.world == WorldId.Ranch && f.vehicle == null && door.tr != null;
        door.hs.dynLabel = f => Moving(door.tr) ? "wait - the camper is moving" : "go inside the " + door.tr.spec.shortName;
        doors.Add(door);
    }

    static Vector3 DoorFront(Door d, float out_)
    {
        Vector3 p = d.tr.transform.TransformPoint(new Vector3(d.tr.spec.wid * 0.5f + out_, 0f, d.d.z));
        p.y = d.tr.transform.position.y + 0.1f;
        return p;
    }

    void Use(Frog f, Door door)
    {
        if (Moving(door.tr)) { f.Toast("Wait until the camper stops", 2f); return; }
        foreach (var p in pend) if (p.f == f) return;
        door.want = 1f; door.hold = 1.6f;
        Sfx.Play(Sfx.Door, 0.9f);
        pend.Add(new Pending { f = f, door = door, t = 0.55f });
    }

    // demo hook: use the door from where the froggy stands
    public void DemoUse(Frog f, Door d) { Use(f, d); }
    public Vector3 DemoDoorFront(Door d) { return DoorFront(d, 0.85f); }

    // ---------------- inside ----------------
    void Enter(Frog f, Door door)
    {
        var r = Build(door.tr.spec);
        r.last = door.tr;
        inside[f] = door.tr;
        Vector3 p = r.o + new Vector3(r.W * 0.5f - 0.75f, 0.12f, r.d.z);
        f.SendTo(WorldId.House, p, 270f);
        r.want = 1f; r.hold = 0.9f;
        f.Toast(r.name + ": bed, kitchen, table + lamp. E / A by the door = go outside", 3.5f);
        Enters++;
        Debug.Log("CAMPER64 enter " + f.nick + " " + r.name + " room " + (p - r.o).ToString("0.00"));
    }

    void Exit(Frog f, Room r)
    {
        Trailer t; inside.TryGetValue(f, out t);
        if (t == null) t = r.last;
        inside.Remove(f);
        Sfx.Play(Sfx.Door, 0.9f);
        r.want = 1f; r.hold = 0.8f;
        Door door = t != null ? DoorOf(t) : null;
        if (door == null) { f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f); return; }
        door.want = 1f; door.hold = 1.4f;
        Vector3 p = DoorFront(door, 1.25f);
        if (Mathf.Abs(p.x) < Layout.Half && Mathf.Abs(p.z) < Layout.Half) p.y = Mathf.Max(p.y, Ranch.GY(p.x, p.z)) + 0.25f;
        f.SendTo(WorldId.Ranch, p, t.transform.eulerAngles.y + 90f);
        Exits++;
        Debug.Log("CAMPER64 exit " + f.nick + " " + r.name + " at " + p.ToString("0.0") + " trailer " + t.transform.position.ToString("0.0"));
    }

    void Lamp(Frog f, Room r)
    {
        r.lampOn = !r.lampOn; LampFlips++;
        if (r.lamp != null) r.lamp.enabled = r.lampOn;
        if (r.ceil != null) r.ceil.enabled = r.lampOn;
        if (r.shade != null) r.shade.sharedMaterial = r.lampOn ? Mats.Unlit(new Color(1f, 0.86f, 0.55f)) : Mats.Lit(new Color(0.85f, 0.8f, 0.7f));
        Sfx.Play(Sfx.Click, 0.7f);
        if (f != null) f.Toast(r.lampOn ? "Lamp on" : "Lamp off", 1.5f);
        Debug.Log("CAMPER64 lamp " + (r.lampOn ? "on" : "off") + " " + r.name);
    }
    public void DemoLamp(Room r) { Lamp(null, r); }
    public void DemoExit(Frog f, Room r) { Exit(f, r); }

    GameObject B(Room r, Vector3 lp, Vector3 size, Material m, bool col, Vector3 euler = default(Vector3))
    {
        var g = Mats.Prim(PrimitiveType.Cube, r.root, lp, size, euler, m, col);
        g.layer = Layer;
        return g;
    }
    GameObject C(Room r, Vector3 lp, Vector3 size, Material m, Vector3 euler = default(Vector3))
    {
        var g = Mats.Prim(PrimitiveType.Cylinder, r.root, lp, size, euler, m, false);
        g.layer = Layer;
        return g;
    }

    Room Build(Trailer.Spec s)
    {
        Room r;
        if (rooms.TryGetValue(s.style, out r)) return r;
        r = new Room { style = s.style, name = s.shortName, d = Spec(s) };
        r.o = Worlds.HouseO + new Vector3(170f + s.style * 30f, 0f, 0f);
        r.L = Mathf.Max(3.6f, s.len - 0.4f); r.W = s.wid - 0.16f; r.H = Mathf.Clamp(s.hgt - 0.25f, 1.65f, 2.15f);
        float L = r.L, W = r.W, H = r.H, hz = L * 0.5f, hx = W * 0.5f, T = 0.12f;
        var go = new GameObject("Camper64 " + s.shortName);
        r.root = go.transform; r.root.position = r.o;
        r.box = new Bounds(r.o + new Vector3(0f, H * 0.5f, 0f), new Vector3(W + 0.4f, H + 1.2f, L + 0.4f));
        Material wall = Mats.TexTint("LB/plaster", new Color(0.93f, 0.9f, 0.84f), 0.05f, 2f);
        Material floor = Mats.TexTint("LB/wood", new Color(0.66f, 0.5f, 0.34f), 0.15f, 1.2f);
        Material trim = Mats.Lit(new Color(0.5f, 0.36f, 0.24f)), wood = Mats.Lit(new Color(0.58f, 0.42f, 0.27f));
        Material accent = Mats.Lit(Color.Lerp(s.accent, Color.white, 0.15f));
        Material counter = Mats.Lit(new Color(0.86f, 0.85f, 0.8f)), dark = Mats.Lit(new Color(0.12f, 0.12f, 0.13f)), metal = Mats.Steel(new Color(0.75f, 0.76f, 0.78f));
        // shell: floor, ceiling, end walls, street-side wall, curb-side wall with the door gap
        B(r, new Vector3(0f, -0.1f, 0f), new Vector3(W + 0.6f, 0.2f, L + 0.6f), floor, true);
        B(r, new Vector3(0f, H + 0.06f, 0f), new Vector3(W + 0.6f, T, L + 0.6f), wall, true);
        B(r, new Vector3(0f, H * 0.5f, hz + T * 0.5f), new Vector3(W + 0.4f, H, T), wall, true);
        B(r, new Vector3(0f, H * 0.5f, -hz - T * 0.5f), new Vector3(W + 0.4f, H, T), wall, true);
        B(r, new Vector3(-hx - T * 0.5f, H * 0.5f, 0f), new Vector3(T, H, L), wall, true);
        float dz = r.d.z, dw = r.d.w + 0.12f, dh = Mathf.Min(r.d.h, H - 0.08f);
        float z0 = dz - dw * 0.5f, z1 = dz + dw * 0.5f;
        B(r, new Vector3(hx + T * 0.5f, H * 0.5f, (-hz + z0) * 0.5f), new Vector3(T, H, z0 + hz), wall, true);
        B(r, new Vector3(hx + T * 0.5f, H * 0.5f, (z1 + hz) * 0.5f), new Vector3(T, H, hz - z1), wall, true);
        if (H - dh > 0.02f) B(r, new Vector3(hx + T * 0.5f, dh + (H - dh) * 0.5f, dz), new Vector3(T, H - dh, dw), wall, true);
        // door frame + the inside of the door on its hinge + daylight beyond the doorway (and a step outside so it reads)
        B(r, new Vector3(hx + 0.02f, dh * 0.5f, z0 + 0.03f), new Vector3(0.18f, dh, 0.06f), trim, false);
        B(r, new Vector3(hx + 0.02f, dh * 0.5f, z1 - 0.03f), new Vector3(0.18f, dh, 0.06f), trim, false);
        B(r, new Vector3(hx + 0.02f, dh + 0.03f, dz), new Vector3(0.18f, 0.06f, dw), trim, false);
        B(r, new Vector3(hx + 0.4f, -0.12f, dz), new Vector3(0.6f, 0.06f, dw), dark, true);
        B(r, new Vector3(hx + 0.75f, dh * 0.5f, dz), new Vector3(0.05f, dh + 0.2f, dw + 0.3f), Mats.Unlit(new Color(0.72f, 0.86f, 0.98f)), true);   // daylight outside; keeps a froggy in
        r.hinge = Mats.Node(r.root, "InDoor64", new Vector3(hx + 0.05f, dh * 0.5f, z1 - 0.06f));
        var slab = Mats.Prim(PrimitiveType.Cube, r.hinge, new Vector3(0f, 0f, -(dw - 0.12f) * 0.5f), new Vector3(0.04f, dh - 0.02f, dw - 0.12f), Mats.Paint(new Color(0.9f, 0.9f, 0.88f), 0.5f));
        slab.layer = Layer;
        Mats.Prim(PrimitiveType.Cube, r.hinge, new Vector3(-0.04f, -dh * 0.05f, -(dw - 0.12f) * 0.86f), new Vector3(0.04f, 0.04f, 0.1f), metal).layer = Layer;
        // trim: skirting + cornice
        B(r, new Vector3(0f, H - 0.04f, hz - 0.02f), new Vector3(W, 0.08f, 0.05f), trim, false);
        B(r, new Vector3(0f, H - 0.04f, -hz + 0.02f), new Vector3(W, 0.08f, 0.05f), trim, false);
        B(r, new Vector3(-hx + 0.02f, H - 0.04f, 0f), new Vector3(0.05f, 0.08f, L), trim, false);
        B(r, new Vector3(-hx + 0.02f, 0.05f, 0f), new Vector3(0.05f, 0.1f, L), trim, false);
        // windows: daylight panes in frames (front end, rear end, both long walls)
        Material sky = Mats.Unlit(new Color(0.78f, 0.9f, 1f));
        float wy = Mathf.Min(1.3f, H - 0.55f), wh = Mathf.Min(0.5f, H - wy - 0.15f) * 2f * 0.5f + 0.2f;
        foreach (float wz in new[] { -hz + 1.0f, hz - 1.0f })
        {
            B(r, new Vector3(-hx + 0.005f, wy, wz), new Vector3(0.02f, wh, 0.75f), sky, false);
            B(r, new Vector3(-hx + 0.02f, wy, wz), new Vector3(0.03f, wh + 0.08f, 0.83f), trim, false);
        }
        B(r, new Vector3(0f, wy, hz - 0.005f), new Vector3(W * 0.5f, wh, 0.02f), sky, false);
        B(r, new Vector3(0f, wy, hz - 0.02f), new Vector3(W * 0.5f + 0.08f, wh + 0.08f, 0.03f), trim, false);
        if (hz - z1 > 1.3f) { B(r, new Vector3(hx - 0.005f, wy, (z1 + hz) * 0.5f), new Vector3(0.02f, wh, 0.7f), sky, false); B(r, new Vector3(hx - 0.02f, wy, (z1 + hz) * 0.5f), new Vector3(0.03f, wh + 0.08f, 0.78f), trim, false); }
        // rug down the aisle
        B(r, new Vector3(hx * 0.3f, 0.005f, 0f), new Vector3(W * 0.42f, 0.01f, L * 0.45f), accent, false);

        // bed across the rear (full width)
        float bz = -hz + 0.78f;
        B(r, new Vector3(0f, 0.22f, bz), new Vector3(W - 0.04f, 0.44f, 1.5f), wood, true);
        B(r, new Vector3(0f, 0.52f, bz), new Vector3(W - 0.14f, 0.18f, 1.4f), Mats.Lit(new Color(0.95f, 0.94f, 0.9f)), false);
        B(r, new Vector3(0f, 0.63f, bz + 0.2f), new Vector3(W - 0.18f, 0.06f, 0.95f), accent, false);
        B(r, new Vector3(-hx * 0.45f, 0.67f, bz - 0.5f), new Vector3(0.55f, 0.12f, 0.32f), Mats.Lit(Color.white), false);
        B(r, new Vector3(hx * 0.45f, 0.67f, bz - 0.5f), new Vector3(0.55f, 0.12f, 0.32f), Mats.Lit(Color.white), false);
        // dinette at the front on the street side (+x aisle stays clear): bench, table, lamp
        float tz = hz - 0.85f, tx = -hx + W * 0.27f;
        B(r, new Vector3(tx, 0.22f, hz - 0.25f), new Vector3(W * 0.52f, 0.44f, 0.45f), wood, true);
        B(r, new Vector3(tx, 0.47f, hz - 0.25f), new Vector3(W * 0.5f, 0.08f, 0.42f), accent, false);
        B(r, new Vector3(tx, 0.72f, tz), new Vector3(W * 0.5f, 0.05f, 0.62f), wood, true);
        C(r, new Vector3(tx, 0.36f, tz), new Vector3(0.08f, 0.35f, 0.08f), metal);
        C(r, new Vector3(tx - 0.12f, 0.77f, tz + 0.12f), new Vector3(0.14f, 0.03f, 0.14f), dark);
        C(r, new Vector3(tx - 0.12f, 0.92f, tz + 0.12f), new Vector3(0.03f, 0.14f, 0.03f), metal);
        var shade = C(r, new Vector3(tx - 0.12f, 1.1f, tz + 0.12f), new Vector3(0.26f, 0.09f, 0.26f), Mats.Unlit(new Color(1f, 0.86f, 0.55f)));
        r.shade = shade.GetComponent<Renderer>();
        var lg = new GameObject("Lamp64"); lg.transform.SetParent(r.root, false); lg.transform.localPosition = new Vector3(tx - 0.12f, 1.05f, tz + 0.12f);
        r.lamp = lg.AddComponent<Light>(); r.lamp.type = LightType.Point; r.lamp.range = 3.6f; r.lamp.intensity = 1.5f; r.lamp.color = new Color(1f, 0.82f, 0.58f); r.lamp.shadows = LightShadows.None;
        var cg = new GameObject("Ceil64"); cg.transform.SetParent(r.root, false); cg.transform.localPosition = new Vector3(0f, H - 0.12f, 0f);
        r.ceil = cg.AddComponent<Light>(); r.ceil.type = LightType.Point; r.ceil.range = L * 0.75f; r.ceil.intensity = 0.9f; r.ceil.color = new Color(1f, 0.93f, 0.82f); r.ceil.shadows = LightShadows.None;
        C(r, new Vector3(0f, H - 0.03f, 0f), new Vector3(0.36f, 0.03f, 0.36f), Mats.Unlit(new Color(1f, 0.95f, 0.85f)));
        // tiny kitchen on the street side between the bed and the dinette: counter, sink, two-burner stove, fridge, cabinets
        float k0 = bz + 0.75f + 0.05f, k1 = tz - 0.4f, kl = Mathf.Max(0.6f, Mathf.Min(2.4f, k1 - k0));
        bool fridge = kl >= 1.3f; float fl = fridge ? 0.56f : 0f; kl -= fridge ? fl + 0.02f : 0f;
        float kz = k0 + kl * 0.5f, kx = -hx + 0.29f;
        B(r, new Vector3(kx, 0.42f, kz), new Vector3(0.56f, 0.84f, kl), wood, true);
        B(r, new Vector3(kx, 0.86f, kz), new Vector3(0.6f, 0.04f, kl + 0.02f), counter, false);
        B(r, new Vector3(kx, 0.86f, kz - kl * 0.25f), new Vector3(0.36f, 0.05f, Mathf.Min(0.4f, kl * 0.35f)), metal, false);
        C(r, new Vector3(kx - 0.18f, 0.98f, kz - kl * 0.25f), new Vector3(0.03f, 0.12f, 0.03f), metal);
        for (int b = 0; b < 2; b++)
        {
            float zz = kz + kl * 0.12f + b * Mathf.Min(0.24f, kl * 0.2f);
            C(r, new Vector3(kx, 0.885f, zz), new Vector3(0.18f, 0.01f, 0.18f), dark);
            C(r, new Vector3(kx, 0.89f, zz), new Vector3(0.12f, 0.005f, 0.12f), Mats.Unlit(new Color(0.9f, 0.3f, 0.08f)));
        }
        for (int dd = 0; dd < 3; dd++) B(r, new Vector3(kx + 0.285f, 0.45f, kz - kl * 0.33f + dd * kl * 0.33f), new Vector3(0.01f, 0.6f, kl * 0.3f), trim, false);
        if (H > 1.95f) B(r, new Vector3(-hx + 0.18f, H - 0.3f, kz), new Vector3(0.34f, 0.42f, kl), wood, true);
        if (fridge)
        {
            float fh = Mathf.Min(1.4f, H - 0.15f);
            B(r, new Vector3(-hx + 0.3f, fh * 0.5f, kz + kl * 0.5f + 0.02f + fl * 0.5f), new Vector3(0.56f, fh, fl), Mats.Lit(new Color(0.9f, 0.9f, 0.88f)), true);
            B(r, new Vector3(-hx + 0.585f, fh * 0.6f, kz + kl * 0.5f + 0.02f + fl * 0.85f), new Vector3(0.03f, 0.3f, 0.03f), metal, false);
        }
        // a plant + a picture so it feels lived in
        C(r, new Vector3(hx - 0.25f, 0.15f, hz - 0.3f), new Vector3(0.22f, 0.15f, 0.22f), Mats.Lit(new Color(0.7f, 0.35f, 0.2f)));
        Mats.Prim(PrimitiveType.Sphere, r.root, new Vector3(hx - 0.25f, 0.45f, hz - 0.3f), new Vector3(0.38f, 0.42f, 0.38f), Mats.Lit(new Color(0.25f, 0.55f, 0.25f))).layer = Layer;
        B(r, new Vector3(-hx + 0.01f, Mathf.Min(1.45f, H - 0.4f), bz + 0.1f), new Vector3(0.02f, 0.36f, 0.5f), Mats.Lit(new Color(0.3f, 0.5f, 0.75f)), false);
        foreach (var rr in r.root.GetComponentsInChildren<Renderer>()) rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // hotspots (world positions; only for froggies standing in this room)
        Room room = r;
        System.Func<Frog, bool> here = f => f.world == WorldId.House && room.box.Contains(f.transform.position);
        var hx1 = Interact.Add(r.o + new Vector3(hx - 0.45f, 0f, dz), 0.95f, "go outside", f => Exit(f, room)); hx1.enabled = here;
        var hl = Interact.Add(r.o + new Vector3(tx + 0.35f, 0f, tz - 0.55f), 1.2f, "", f => Lamp(f, room)); hl.enabled = here;
        hl.dynLabel = f => room.lampOn ? "turn the lamp off" : "turn the lamp on";
        var hb = Interact.Add(r.o + new Vector3(hx * 0.4f, 0f, bz + 0.95f), 0.9f, "take a nap", f => { f.Toast("Zzz... the camper bed is so cozy", 2.5f); Sfx.Play(Sfx.Click, 0.4f); }); hb.enabled = here;
        var hk = Interact.Add(r.o + new Vector3(kx + 0.75f, 0f, kz), 0.8f, "make hot cocoa", f => { f.Toast("Hot cocoa on the little stove - yum!", 2.5f); Sfx.Play(Sfx.Pickup, 0.6f); }); hk.enabled = here;
        rooms[s.style] = r;
        Debug.Log("CAMPER64 built " + r.name + " L " + L.ToString("0.0") + " W " + W.ToString("0.00") + " H " + H.ToString("0.00") + " at " + r.o.ToString("0"));
        return r;
    }

    // ---------------- per frame ----------------
    void Update()
    {
        if (!On) return;
        for (int i = doors.Count - 1; i >= 0; i--) if (doors[i].tr == null) { Interact.All.Remove(doors[i].hs); doors.RemoveAt(i); }
        foreach (var t in Trailer.All) if (t != null && t.spec != null && t.spec.style >= 0 && t.spec.style <= 3 && DoorOf(t) == null) Register(t);
        float dt = Mathf.Min(Time.deltaTime, 0.1f);   // low-fps devices still swing the door in time
        foreach (var d in doors)
        {
            d.hs.pos = DoorFront(d, 0.85f);
            if (d.hold > 0f) d.hold -= dt; else d.want = 0f;
            float nk = Mathf.MoveTowards(d.k, d.want, dt * 2.6f);
            if (nk != d.k || d.hinge.localEulerAngles.y != 0f) { d.k = nk; d.hinge.localRotation = Quaternion.Euler(0f, -105f * Mathf.SmoothStep(0f, 1f, d.k), 0f); }
        }
        foreach (var r in rooms.Values)
        {
            if (r.hold > 0f) r.hold -= dt; else r.want = 0f;
            r.k = Mathf.MoveTowards(r.k, r.want, dt * 2.6f);
            r.hinge.localRotation = Quaternion.Euler(0f, -75f * Mathf.SmoothStep(0f, 1f, r.k), 0f);
        }
        for (int i = pend.Count - 1; i >= 0; i--)
        {
            var p = pend[i]; p.t -= dt;
            if (p.t > 0f) continue;
            pend.RemoveAt(i);
            if (p.f != null && p.door.tr != null && p.f.world == WorldId.Ranch && p.f.vehicle == null) Enter(p.f, p.door);
        }
    }
}
