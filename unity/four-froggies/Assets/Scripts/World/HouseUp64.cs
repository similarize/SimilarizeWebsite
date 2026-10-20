using System.Collections.Generic;
using UnityEngine;

// ffu64 JAMES'S HOUSE, UPSTAIRS + GARAGE BACK DOOR + PORCH ROOF.
// House world (WorldId.House, built by HouseWorld.Build when Light55 is on):
//   * a stair run in the living room along the fish-gallery wall (16 steps x 0.3 m, bottom at x -21.2, top at x -28.4),
//     a stairwell hole in the ground-floor ceiling above it (ceiling mesh + colliders in 4 pieces), railings;
//   * a real 2nd floor (floor 4.8 m, ceiling 8.8 m): hallway, James's room, guest room, bathroom, landing / reading nook,
//     bunk room, craft room - beds, desks, tub, toilet, sink, rugs, lamps; windows on every outer wall at the shell's
//     upstairs window height (6.8 m) - "climb out the window" puts you outside that window on the ranch (onto the porch
//     roof over the porch, else down on the ground);
//   * a hallway off the kitchen's east wall (door gap at z 13.3) ending at the GARAGE door -> James's garage on the ranch.
//   ffu67b (Bill Oct 20: "the garage back door, entered from outside, must lead INTO THE GARAGE ... garage and house stay
//   separate spaces, joined by an inside hallway / mudroom with two doors"): the hallway is now the MUDROOM, the same footprint
//   inside and out - ranch x -19 .. -14.5, z 4 .. 13 (a wing on the house's east wall that meets the garage's back wall) =
//   house-local x 30 .. 36, z 10.67 .. 22.67 (local = (ranch - HouseC) x 60/46, 40/30). Door 1: kitchen east wall (z 13.3).
//   Door 2: north end (local x 33) = the garage's back wall at ranch x -16.7 (inside the garage, sign "MUDROOM - HOUSE").
//   The OUTSIDE back door is a real opening in the garage's back wall at ranch x -9 (Ranch.Garage splits the wall), facing
//   the open lawn; walk through it (auto door) or E / A -> straight INTO the garage; from inside, E / A -> back outside.
//   The old (-27.2, 13.25) portal door into the hallway is gone (it would sit in a dead-end nook behind the wing).
// Ranch side: the porch cover gets a top collider (walkable), a ladder at its west end (E / A climb up / down), "climb in the
// upstairs window" spots on the porch roof, a back door in the garage's south wall (-27.2, 13.25) into the house hallway, and
// a watcher: a froggy that hops into the hollow shell through an upstairs window (4.3 .. 8.7 m up, walls intact) is taken
// into the 2nd floor at the matching spot instead of being dropped into the empty shell and walked out onto the porch.
public static class HouseUp64
{
    public const float F2 = 4.8f, WH = 4.0f;                                   // 2nd floor top, its wall height
    public const float HX0 = -28.4f, HX1 = -21.2f, HZ0 = 4.15f, HZ1 = 6.6f;    // stairwell hole (house-local)
    public const float HallZ = 13.3f;                                          // kitchen east wall door -> hallway
    public static readonly Vector3 GarageDoor = new Vector3(-16.7f, 0f, 13.25f);   // ffu67b: the garage's door into the MUDROOM (inside the garage)
    public static readonly Vector3 BackDoor = new Vector3(-9f, 0f, 13.25f);        // ffu67b: the garage's OUTSIDE back door (real opening)
    public const float BackDoorW = 1.6f, BackDoorH = 2.5f;
    // mudroom: ranch footprint + the matching house-local box
    public const float MRx0 = -19f, MRx1 = -14.5f, MRz0 = 4f, MRz1 = 13f, MRH = 4.2f;
    public const float MLx0 = 30.15f, MLx1 = 36f, MLz0 = 10.67f, MLz1 = 22.67f, MLdoorX = 33f;
    public static int BackDoorIn, BackDoorOut, MudTrips;
    public static bool Built;
    public static int UpEntries, WindowExits, GarageTrips, LadderClimbs;
    static Transform up;
    static Material wallM, floorM, trimM, woodM;

    static Vector3 L(float x, float y, float z) { return HouseWorld.L(x, y, z); }
    static GameObject Bx(Vector3 lp, Vector3 size, Material m, bool col = true)
    {
        var g = Mats.Prim(PrimitiveType.Cube, up, L(lp.x, lp.y, lp.z), size, Vector3.zero, m, col);
        g.layer = HouseWorld.WallLayer;
        return g;
    }
    static Material C(Color c) { return Mats.Lit(c); }

    // ---------------- ground-floor ceiling with the stairwell hole ----------------
    public static Mesh CeilingMesh(Vector3 o, float H)
    {
        var rs = Pieces();
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        foreach (var r in rs)
        {
            int b = v.Count;
            v.Add(o + new Vector3(r.xMin, H, r.yMin)); v.Add(o + new Vector3(r.xMax, H, r.yMin)); v.Add(o + new Vector3(r.xMax, H, r.yMax)); v.Add(o + new Vector3(r.xMin, H, r.yMax));
            foreach (var p in new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax) }) uv.Add(new Vector2((p.x + 30.2f) / 3f, (p.y + 20.2f) / 3f));
            t.Add(b); t.Add(b + 1); t.Add(b + 2); t.Add(b); t.Add(b + 2); t.Add(b + 3);   // same winding as the old quad (faces down)
        }
        var m = new Mesh(); m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }
    static Rect[] Pieces()
    {
        return new[] {
            Rect.MinMaxRect(-30.2f, -20.2f, 30.2f, HZ0), Rect.MinMaxRect(-30.2f, HZ1, 30.2f, 20.2f),
            Rect.MinMaxRect(-30.2f, HZ0, HX0, HZ1), Rect.MinMaxRect(HX1, HZ0, 30.2f, HZ1) };
    }
    public static void CeilingCols(Transform root, float H)
    {
        foreach (var r in Pieces())
        {
            var cg = new GameObject("Ceiling64Col"); cg.transform.SetParent(root, false); cg.layer = HouseWorld.WallLayer;
            // pad only the OUTER edges by 0.1 m (ffu64 C3 / B3: padding every edge pushed 0.1 m slivers into the stairwell -> a froggy
            // hugging the open side of the stairs bumped its head at step 11)
            float x0 = r.xMin - (r.xMin < -30f ? 0.1f : 0f), x1 = r.xMax + (r.xMax > 30f ? 0.1f : 0f), z0 = r.yMin - (r.yMin < -20f ? 0.1f : 0f), z1 = r.yMax + (r.yMax > 20f ? 0.1f : 0f);
            cg.transform.position = L((x0 + x1) * 0.5f, H + 0.15f, (z0 + z1) * 0.5f);
            cg.AddComponent<BoxCollider>().size = new Vector3(x1 - x0, 0.3f, z1 - z0);
        }
    }

    // ---------------- 2nd floor, stairs, hallway ----------------
    public static void Build(Transform root)
    {
        if (Built) return;
        Built = true;
        up = Mats.Node(root, "Up64", Vector3.zero);
        wallM = Mats.TexTint("LB/plaster", new Color(0.9f, 0.86f, 0.8f), 0.05f, 3f);
        floorM = Mats.TexTint("LB/wood", new Color(0.66f, 0.5f, 0.34f), 0.15f, 1.5f);
        trimM = C(new Color(0.86f, 0.84f, 0.8f)); woodM = C(new Color(0.5f, 0.34f, 0.2f));
        float H = Light55.HouseH;
        // stairs: solid blocks, 0.3 rise / 0.45 run, climbing toward -x
        for (int i = 0; i < 16; i++)
        {
            float xa = HX1 - (i + 1) * 0.45f, xb = HX1 - i * 0.45f, top = (i + 1) * 0.3f;
            Bx(new Vector3((xa + xb) * 0.5f, top * 0.5f, (HZ0 + HZ1 - 0.05f) * 0.5f), new Vector3(0.45f, top, HZ1 - HZ0 - 0.05f), i % 2 == 0 ? woodM : C(new Color(0.55f, 0.38f, 0.23f)));
        }
        for (int i = 0; i < 17; i += 2) Bx(new Vector3(HX1 - i * 0.45f, (i * 0.3f) + 0.55f, HZ1 - 0.04f), new Vector3(0.06f, 1.1f, 0.06f), trimM, false);
        float railLen = Mathf.Sqrt(7.2f * 7.2f + 4.8f * 4.8f);
        var rail = Mats.Prim(PrimitiveType.Cube, up, L((HX0 + HX1) * 0.5f, 2.4f + 1.05f, HZ1 - 0.04f), new Vector3(railLen, 0.08f, 0.08f), new Vector3(0f, 0f, -Mathf.Atan2(4.8f, 7.2f) * Mathf.Rad2Deg), trimM, false);
        rail.layer = HouseWorld.WallLayer;
        Ranch.Sign(L(-24.8f, 3.6f, 6.75f), 0f, "UPSTAIRS", new Color(0.25f, 0.35f, 0.5f), 3.2f, 0.8f);
        Findable();   // ffu65: big signs, floor arrows, lit + contrasting stairs
        // 2nd-floor floor boards around the hole (the ground-floor ceiling colliders carry them)
        foreach (var r in Pieces())
        {
            float x0 = Mathf.Max(r.xMin, -29.85f), x1 = Mathf.Min(r.xMax, 29.85f), z0 = Mathf.Max(r.yMin, -19.85f), z1 = Mathf.Min(r.yMax, 19.85f);
            Bx(new Vector3((x0 + x1) * 0.5f, F2 - 0.02f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.04f, z1 - z0), floorM, false);
        }
        // railing round the hole upstairs (solid: no falling back down except by the stairs)
        Bx(new Vector3((HX0 + HX1) * 0.5f, F2 + 0.55f, HZ1 + 0.05f), new Vector3(HX1 - HX0, 1.1f, 0.1f), woodM, true);
        Bx(new Vector3((HX0 + HX1) * 0.5f, F2 + 0.55f, HZ0 - 0.05f), new Vector3(HX1 - HX0, 1.1f, 0.1f), woodM, true);
        Bx(new Vector3(HX1 + 0.05f, F2 + 0.55f, (HZ0 + HZ1) * 0.5f), new Vector3(0.1f, 1.1f, HZ1 - HZ0 + 0.2f), woodM, true);
        // outer walls + inner walls (door gaps 3.4 m, 3.2 m tall)
        Wall2(true, 20f, -30f, 30f);
        Wall2(true, -20f, -30f, 30f);
        Wall2(false, -30f, -20f, 20f);
        Wall2(false, 30f, -20f, 20f);
        Wall2(true, -2f, -30f, 30f, -20f, 0f, 20f);
        Wall2(true, 2f, -30f, 30f, -14f, 2f, 20f);
        Wall2(false, -10f, -20f, -2f);
        Wall2(false, 10f, -20f, -2f);
        Wall2(false, -6f, 2f, 20f);
        Wall2(false, 10f, 2f, 20f);
        // ceiling (faces down) + collider + room tones on the floor
        var cg = Bx(new Vector3(0f, F2 + WH + 0.15f, 0f), new Vector3(60.6f, 0.3f, 40.6f), Mats.TexTint("LB/plaster", new Color(0.95f, 0.94f, 0.9f), 0.05f, 3f), true);
        Bx(new Vector3(0f, F2 + 0.005f, 0f), new Vector3(59.6f, 0.01f, 3.7f), C(new Color(0.55f, 0.3f, 0.26f)), false);   // hallway runner
        Furnish();
        UpWindows();
        Lamps();
        Hallway(root);
        foreach (var r in up.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        MeshMerge.Merge(up, false);
        Debug.Log("HOUSE64 upstairs built (stairs 16 x 0.3 m, floor " + F2 + " m, hallway to the garage)");
    }

    static void Wall2(bool alongX, float fixedC, float from, float to, params float[] doors)
    {
        const float T = 0.3f, D = 3.4f, DH = 3.2f;
        var cuts = new List<float>(doors); cuts.Sort();
        float a = from;
        foreach (float d in cuts)
        {
            Seg2(alongX, fixedC, a, d - D * 0.5f, 0f, WH, T);
            Seg2(alongX, fixedC, d - D * 0.5f, d + D * 0.5f, DH, WH, T);   // header
            a = d + D * 0.5f;
        }
        Seg2(alongX, fixedC, a, to, 0f, WH, T);
    }
    static void Seg2(bool alongX, float c, float a, float b, float y0, float y1, float T)
    {
        if (b - a < 0.05f) return;
        float m = (a + b) * 0.5f, len = b - a, h = y1 - y0, y = F2 + (y0 + y1) * 0.5f;
        if (alongX) Bx(new Vector3(m, y, c), new Vector3(len, h, T), wallM); else Bx(new Vector3(c, y, m), new Vector3(T, h, len), wallM);
        if (y0 < 0.01f) { if (alongX) Bx(new Vector3(m, F2 + 0.12f, c), new Vector3(len, 0.24f, T + 0.06f), woodM, false); else Bx(new Vector3(c, F2 + 0.12f, m), new Vector3(T + 0.06f, 0.24f, len), woodM, false); }
    }

    static void Bed(float x, float z, bool alongZ, Color blanket, float w = 2.2f, float l = 3.0f)
    {
        Vector3 s = alongZ ? new Vector3(w, 0.55f, l) : new Vector3(l, 0.55f, w);
        Bx(new Vector3(x, F2 + 0.275f, z), s, woodM);
        Bx(new Vector3(x, F2 + 0.65f, z), new Vector3(s.x - 0.1f, 0.2f, s.z - 0.1f), C(new Color(0.96f, 0.95f, 0.92f)), false);
        Vector3 bl = alongZ ? new Vector3(w - 0.05f, 0.08f, l * 0.62f) : new Vector3(l * 0.62f, 0.08f, w - 0.05f);
        Vector3 bo = alongZ ? new Vector3(0f, 0f, l * 0.17f) : new Vector3(-l * 0.17f, 0f, 0f);
        Bx(new Vector3(x, F2 + 0.78f, z) + bo, bl, C(blanket), false);
        Vector3 po = alongZ ? new Vector3(0f, 0f, -l * 0.38f) : new Vector3(l * 0.38f, 0f, 0f);
        Bx(new Vector3(x, F2 + 0.86f, z) + po, alongZ ? new Vector3(w * 0.7f, 0.16f, 0.45f) : new Vector3(0.45f, 0.16f, w * 0.7f), C(Color.white), false);
        Vector3 ho = alongZ ? new Vector3(0f, 0f, -l * 0.5f - 0.05f) : new Vector3(l * 0.5f + 0.05f, 0f, 0f);
        Bx(new Vector3(x, F2 + 0.75f, z) + ho, alongZ ? new Vector3(w, 1.5f, 0.12f) : new Vector3(0.12f, 1.5f, w), woodM);
    }
    static void Rug(float x, float z, float sx, float sz, Color c) { Bx(new Vector3(x, F2 + 0.012f, z), new Vector3(sx, 0.01f, sz), C(c), false); }

    static void Furnish()
    {
        Color white = new Color(0.97f, 0.97f, 0.96f);
        // James's room (x -30..-10, z -20..-2): bed against the x -10 wall, desk + chair, toy chest, rug, poster
        Bed(-11.8f, -11f, false, new Color(0.25f, 0.55f, 0.3f));
        Rug(-20f, -11f, 7f, 5f, new Color(0.3f, 0.45f, 0.7f));
        Bx(new Vector3(-26.5f, F2 + 0.5f, -3.0f), new Vector3(3.2f, 1.0f, 1.3f), woodM);
        Bx(new Vector3(-26.5f, F2 + 0.3f, -4.6f), new Vector3(0.9f, 0.6f, 0.9f), C(new Color(0.85f, 0.3f, 0.25f)));
        Bx(new Vector3(-26.5f, F2 + 1.25f, -2.6f), new Vector3(1.2f, 0.5f, 0.06f), C(new Color(0.08f, 0.08f, 0.1f)), false);   // monitor
        Bx(new Vector3(-12.5f, F2 + 0.45f, -18.6f), new Vector3(2.4f, 0.9f, 1.2f), C(new Color(0.85f, 0.6f, 0.25f)));   // toy chest
        Bx(new Vector3(-10.17f, F2 + 2.4f, -16f), new Vector3(0.03f, 1.4f, 1.0f), C(new Color(0.95f, 0.75f, 0.2f)), false);
        Ranch.Sign(L(-20f, F2 + 3.55f, -2.2f), 0f, "JAMES'S ROOM", new Color(0.2f, 0.45f, 0.3f), 3.2f, 0.6f);
        // guest room (x -10..10): double bed, nightstands, wardrobe
        Bed(0f, -12.5f, true, new Color(0.7f, 0.35f, 0.5f), 3.2f, 3.2f);
        Bx(new Vector3(-2.4f, F2 + 0.35f, -14f), new Vector3(0.9f, 0.7f, 0.9f), woodM); Bx(new Vector3(2.4f, F2 + 0.35f, -14f), new Vector3(0.9f, 0.7f, 0.9f), woodM);
        Bx(new Vector3(8.6f, F2 + 1.4f, -6f), new Vector3(2.2f, 2.8f, 1.2f), woodM);
        Rug(0f, -9.5f, 5f, 3f, new Color(0.8f, 0.7f, 0.5f));
        Ranch.Sign(L(0f, F2 + 3.55f, -2.2f), 0f, "GUEST ROOM", new Color(0.5f, 0.3f, 0.45f), 3.2f, 0.6f);
        // bathroom (x 10..30): tub with water, toilet, vanity + mirror, bath mat
        Bx(new Vector3(24.5f, F2 + 0.5f, -17.4f), new Vector3(5f, 1.0f, 2.6f), C(white));
        Bx(new Vector3(24.5f, F2 + 0.85f, -17.4f), new Vector3(4.4f, 0.04f, 2.0f), Mats.Unlit(new Color(0.55f, 0.8f, 0.95f)), false);
        Bx(new Vector3(12.2f, F2 + 0.35f, -18.6f), new Vector3(0.9f, 0.7f, 1.0f), C(white));
        Bx(new Vector3(12.2f, F2 + 0.95f, -19.4f), new Vector3(0.9f, 0.6f, 0.35f), C(white));
        Bx(new Vector3(26.0f, F2 + 0.5f, -3.0f), new Vector3(3.0f, 1.0f, 1.2f), woodM);
        Bx(new Vector3(26.0f, F2 + 1.03f, -3.0f), new Vector3(3.05f, 0.06f, 1.25f), C(new Color(0.9f, 0.9f, 0.88f)), false);
        Bx(new Vector3(26.0f, F2 + 2.1f, -2.17f), new Vector3(2.2f, 1.4f, 0.03f), Mats.GlassTint(new Color(0.7f, 0.8f, 0.85f)), false);
        Rug(21f, -14f, 3f, 2f, new Color(0.4f, 0.7f, 0.75f));
        Ranch.Sign(L(20f, F2 + 3.55f, -2.2f), 0f, "BATHROOM", new Color(0.25f, 0.5f, 0.6f), 3.2f, 0.6f);
        // landing / reading nook (x -30..-6, z 2..20): beanbags, bookshelf, telescope at the window
        Bx(new Vector3(-29.2f, F2 + 1.4f, 14f), new Vector3(1.2f, 2.8f, 5f), woodM);
        for (int i = 0; i < 9; i++) Bx(new Vector3(-28.55f, F2 + 0.5f + (i / 3) * 0.9f, 12.4f + (i % 3) * 1.4f), new Vector3(0.1f, 0.6f, 1.1f), C(Color.HSVToRGB((i * 0.17f) % 1f, 0.6f, 0.8f)), false);
        Mats.Prim(PrimitiveType.Sphere, up, L(-20f, F2 + 0.45f, 13f), new Vector3(1.6f, 0.9f, 1.6f), C(new Color(0.9f, 0.45f, 0.2f)), true).layer = HouseWorld.WallLayer;
        Mats.Prim(PrimitiveType.Sphere, up, L(-16.8f, F2 + 0.45f, 12f), new Vector3(1.6f, 0.9f, 1.6f), C(new Color(0.3f, 0.6f, 0.85f)), true).layer = HouseWorld.WallLayer;
        Rug(-18f, 12.5f, 7f, 5f, new Color(0.75f, 0.65f, 0.4f));
        Ranch.Sign(L(-14f, F2 + 3.55f, 2.2f), 180f, "LANDING", new Color(0.4f, 0.35f, 0.25f), 3.0f, 0.6f);
        // bunk room (x -6..10): bunk bed against the x 10 wall, ladder
        Bx(new Vector3(8.6f, F2 + 0.3f, 8f), new Vector3(2.4f, 0.6f, 3.4f), woodM);
        Bx(new Vector3(8.6f, F2 + 0.7f, 8f), new Vector3(2.3f, 0.2f, 3.3f), C(new Color(0.95f, 0.94f, 0.9f)), false);
        Bx(new Vector3(8.6f, F2 + 2.1f, 8f), new Vector3(2.4f, 0.2f, 3.4f), woodM);
        Bx(new Vector3(8.6f, F2 + 2.3f, 8f), new Vector3(2.3f, 0.2f, 3.3f), C(new Color(0.3f, 0.5f, 0.85f)), false);
        foreach (float zz in new[] { 6.4f, 9.6f }) Bx(new Vector3(7.5f, F2 + 1.3f, zz), new Vector3(0.12f, 2.6f, 0.12f), woodM);
        Rug(2f, 9f, 5f, 4f, new Color(0.6f, 0.75f, 0.35f));
        Ranch.Sign(L(2f, F2 + 3.55f, 2.2f), 180f, "BUNK ROOM", new Color(0.3f, 0.45f, 0.65f), 3.0f, 0.6f);
        // craft room (x 10..30): big table, stools, shelves of supplies
        Bx(new Vector3(20f, F2 + 0.8f, 9f), new Vector3(6f, 0.1f, 3f), woodM);
        foreach (var lp in new[] { new Vector2(17.3f, 7.8f), new Vector2(22.7f, 7.8f), new Vector2(17.3f, 10.2f), new Vector2(22.7f, 10.2f) }) Bx(new Vector3(lp.x, F2 + 0.4f, lp.y), new Vector3(0.15f, 0.8f, 0.15f), woodM);
        for (int i = 0; i < 4; i++) Bx(new Vector3(17.5f + i * 1.7f, F2 + 0.3f, 11.6f), new Vector3(0.6f, 0.6f, 0.6f), C(Color.HSVToRGB(i * 0.22f, 0.5f, 0.85f)));
        Bx(new Vector3(29.3f, F2 + 1.2f, 9f), new Vector3(1.0f, 2.4f, 4f), woodM);
        for (int i = 0; i < 6; i++) Bx(new Vector3(28.7f, F2 + 0.6f + (i / 3) * 1.0f, 7.8f + (i % 3) * 1.2f), new Vector3(0.3f, 0.4f, 0.8f), C(Color.HSVToRGB((0.1f + i * 0.15f) % 1f, 0.7f, 0.9f)), false);
        Ranch.Sign(L(20f, F2 + 3.55f, 2.2f), 180f, "CRAFT ROOM", new Color(0.55f, 0.35f, 0.2f), 3.0f, 0.6f);
    }

    // windows on every outer wall at the shell's upstairs height; each is a way out onto the ranch
    static void UpWindows()
    {
        Material pane = Mats.Unlit(new Color(0.80f, 0.88f, 0.97f));
        for (int face = 0; face < 4; face++)
        {
            bool alongX = face < 2; int cols = alongX ? 9 : 6;
            float half = alongX ? 30f : 20f, w = 2f * half / cols;
            for (int i = 0; i < cols; i++)
            {
                float a = -half + w * (i + 0.5f);
                Vector3 n, p;
                switch (face)
                {
                    case 0: n = Vector3.back; p = new Vector3(a, F2 + 2.0f, 20f - 0.15f); break;
                    case 1: n = Vector3.forward; p = new Vector3(a, F2 + 2.0f, -20f + 0.15f); break;
                    case 2: n = Vector3.right; p = new Vector3(-30f + 0.15f, F2 + 2.0f, a); break;
                    default: n = Vector3.left; p = new Vector3(30f - 0.15f, F2 + 2.0f, a); break;
                }
                Bx(p + n * 0.03f, alongX ? new Vector3(2.5f, 2.0f, 0.06f) : new Vector3(0.06f, 2.0f, 2.5f), trimM, false);
                Bx(p + n * 0.06f, alongX ? new Vector3(2.2f, 1.7f, 0.04f) : new Vector3(0.04f, 1.7f, 2.2f), pane, false);
                Bx(p + n * 0.12f + Vector3.down * 0.95f, alongX ? new Vector3(2.6f, 0.08f, 0.22f) : new Vector3(0.22f, 0.08f, 2.6f), trimM, false);
                Vector3 inner = p + n * 0.9f; inner.y = F2 + 0.1f;
                Vector3 hx = L(inner.x, inner.y, inner.z), outN = -n, lp = p;
                var hs = Interact.Add(hx, 1.4f, "climb out the window", f => WindowOut(f, lp, outN));
                hs.enabled = f => f.world == WorldId.House && f.transform.position.y > Worlds.HouseO.y + F2 - 0.6f;
            }
        }
    }

    // ---------------- ffu65 (Bill 2026-10-20: "I don't see the stairs"): make the stairs easy to find from the front door ----------------
    // a bright orange sign over the foyer -> living room door ("STAIRS TO 2ND FLOOR ->"), a big sign + an arrow board at the foot of
    // the stairs, glowing yellow floor arrows from the foyer to the bottom step, cream step nosings, two warm lights over the stair run,
    // and a "STAIRS DOWN" sign upstairs. The first time each froggy comes in, a hint toast points the way; the robot phone's PLAY tab
    // has "Me: go upstairs (house)".
    public static int Hints, Shortcuts;
    static readonly bool[] hinted = new bool[8];
    static void Findable()
    {
        Color orange = new Color(0.9f, 0.42f, 0.08f);
        Ranch.Sign(L(-5.75f, 3.05f, 10f), 90f, "STAIRS TO 2ND FLOOR  >", orange, 3.6f, 0.75f);    // foyer side of the living-room door (yaw 90 = reads from the foyer; 65a had it backwards)
        Ranch.Sign(L(-24.8f, 2.55f, 6.85f), 0f, "STAIRS TO 2ND FLOOR", orange, 4.6f, 0.85f);
        Ranch.Sign(L(-20.75f, 1.55f, 5.4f), 90f, "STAIRS UP  ^", orange, 2.2f, 0.6f);
        Ranch.Sign(L(-24.8f, F2 + 2.6f, 6.62f), 0f, "STAIRS DOWN", new Color(0.25f, 0.35f, 0.5f), 2.6f, 0.6f);
        var glow = Mats.Unlit(new Color(1f, 0.85f, 0.15f)); var cream = Mats.Unlit(new Color(0.98f, 0.93f, 0.78f));
        for (int i = 0; i < 16; i++) { float xe = HX1 - i * 0.45f; Bx(new Vector3(xe - 0.03f, (i + 1) * 0.3f + 0.006f, (HZ0 + HZ1 - 0.05f) * 0.5f), new Vector3(0.06f, 0.012f, HZ1 - HZ0 - 0.1f), cream, false); }
        // floor arrows: foyer (spawn) -> living-room door -> foot of the stairs
        Vector2[] path = { new Vector2(-3f, 13.5f), new Vector2(-4.6f, 10.4f), new Vector2(-8.5f, 10f), new Vector2(-12.5f, 8.6f), new Vector2(-16.5f, 6.9f), new Vector2(-19.6f, 5.6f) };
        for (int k = 0; k + 1 < path.Length; k++)
        {
            Vector2 a = path[k], b = path[k + 1], d = (b - a).normalized; float yaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
            for (int s = -1; s <= 1; s += 2)
            {
                var g = Mats.Prim(PrimitiveType.Cube, up, L(a.x + d.x * 0.6f - d.y * 0.18f * s, 0.03f, a.y + d.y * 0.6f + d.x * 0.18f * s), new Vector3(0.12f, 0.02f, 0.55f), new Vector3(0f, yaw - 35f * s, 0f), glow, false);
                g.layer = HouseWorld.WallLayer;
            }
        }
        foreach (var lp in new[] { L(-22.5f, 3.6f, 5.4f), L(-27f, F2 + 2.8f, 5.4f) })
        {
            var g = new GameObject("StairLamp65"); g.transform.SetParent(up, false); g.transform.position = lp;
            var l = g.AddComponent<Light>(); l.type = LightType.Point; l.range = 8f; l.intensity = 1.5f; l.color = new Color(1f, 0.9f, 0.72f); l.shadows = LightShadows.None;
        }
    }
    public static void EnterHint(Frog f)
    {
        if (f == null || f.id < 0 || f.id >= hinted.Length || hinted[f.id]) return;
        hinted[f.id] = true; Hints++;
        f.Toast("Stairs to 2nd floor \u2192  through the door on your right, follow the yellow arrows", 6f);
        Debug.Log("HOUSE65 stairs hint " + f.nick);
    }
    // robot phone PLAY tab "Me: go upstairs (house)": straight to the 2nd-floor landing at the top of the stairs, from any world
    public static string GoUpstairs(Frog f)
    {
        if (f == null || HouseWorld.I == null) return "";
        if (f.vehicle != null) f.ExitVehicle();
        HouseWorld.I.EnsureBuilt();
        if (!Built) return "James's house upstairs isn't built in this mode";
        f.SendTo(WorldId.House, L(-29.0f, F2 + 0.15f, 5.4f), 0f);
        Shortcuts++; Debug.Log("HOUSE65 go upstairs " + f.nick);
        return "Upstairs at James's house! The stairs back down are right here by the railing.";
    }

    static void Lamps()
    {
        foreach (var c in new[] { new Vector2(-20f, -11f), new Vector2(0f, -11f), new Vector2(20f, -11f), new Vector2(-18f, 11f), new Vector2(2f, 11f), new Vector2(20f, 11f), new Vector2(0f, 0f) })
        {
            var g = new GameObject("UpLamp64"); g.transform.SetParent(up, false); g.transform.position = L(c.x, F2 + WH - 0.5f, c.y);
            var l = g.AddComponent<Light>(); l.type = LightType.Point; l.range = 13f; l.intensity = 0.9f; l.color = new Color(1f, 0.92f, 0.8f); l.shadows = LightShadows.None;
            Mats.Prim(PrimitiveType.Cylinder, up, L(c.x, F2 + WH - 0.03f, c.y), new Vector3(0.9f, 0.03f, 0.9f), Mats.Unlit(new Color(1f, 0.95f, 0.85f)), false).layer = HouseWorld.WallLayer;
        }
    }

    // ffu67b: the MUDROOM (was the ffu64 hallway): kitchen east-wall door (z HallZ) + the garage door at the north end
    static void Hallway(Transform root)
    {
        float x0 = MLx0, x1 = MLx1, zs = MLz0, zn = MLz1, zc = (zs + zn) * 0.5f, H = Light55.HouseH;
        Bx(new Vector3((x0 + x1) * 0.5f, -0.3f, zc), new Vector3(x1 - x0 + 0.3f, 0.6f, zn - zs + 0.6f), floorM);
        Bx(new Vector3((x0 + x1) * 0.5f, 0.006f, zc), new Vector3(1.4f, 0.012f, zn - zs - 1.2f), C(new Color(0.6f, 0.32f, 0.25f)), false);   // runner
        Bx(new Vector3((x0 + x1) * 0.5f, H * 0.5f, zs), new Vector3(x1 - x0 + 0.3f, H, 0.3f), wallM);              // south wall
        Bx(new Vector3(x1 + 0.15f, H * 0.5f, zc), new Vector3(0.3f, H, zn - zs + 0.3f), wallM);                     // east wall
        Bx(new Vector3(30f, H * 0.5f, (20f + zn) * 0.5f), new Vector3(0.3f, H, zn - 20f + 0.3f), wallM);           // west wall past the house corner
        // north wall with the garage door gap (1.8 m) at MLdoorX
        float dg0 = MLdoorX - 0.9f, dg1 = MLdoorX + 0.9f;
        Bx(new Vector3((x0 - 0.15f + dg0) * 0.5f, H * 0.5f, zn), new Vector3(dg0 - x0 + 0.15f, H, 0.3f), wallM);
        Bx(new Vector3((dg1 + x1 + 0.3f) * 0.5f, H * 0.5f, zn), new Vector3(x1 + 0.3f - dg1, H, 0.3f), wallM);
        Bx(new Vector3(MLdoorX, (3.2f + H) * 0.5f, zn), new Vector3(1.8f, H - 3.2f, 0.3f), wallM);
        Bx(new Vector3(MLdoorX, 1.6f, zn + 0.1f), new Vector3(1.7f, 3.2f, 0.06f), C(new Color(0.42f, 0.27f, 0.15f)));   // the garage door (closed panel, solid)
        Bx(new Vector3(MLdoorX + 0.6f, 1.5f, zn - 0.02f), new Vector3(0.12f, 0.1f, 0.08f), Mats.Steel(new Color(0.75f, 0.7f, 0.5f)), false);
        Bx(new Vector3(MLdoorX, 3.3f, zn - 0.08f), new Vector3(2.1f, 0.2f, 0.1f), trimM, false);
        Bx(new Vector3((x0 + x1) * 0.5f, H + 0.15f, zc), new Vector3(x1 - x0 + 0.6f, 0.3f, zn - zs + 0.6f), Mats.TexTint("LB/plaster", new Color(0.95f, 0.94f, 0.9f), 0.05f, 3f));
        Ranch.Sign(L(MLdoorX, 3.85f, zn - 0.2f), 180f, "GARAGE", new Color(0.35f, 0.35f, 0.4f), 2.6f, 0.6f);
        Ranch.Sign(L(x0 + 0.25f, 3.6f, HallZ), 90f, "MUDROOM", new Color(0.3f, 0.25f, 0.2f), 2.6f, 0.55f);
        // bench, coat hooks, shoe rack, boots
        Bx(new Vector3(x1 - 0.5f, 0.3f, 17.5f), new Vector3(0.7f, 0.6f, 3f), woodM);
        for (int i = 0; i < 4; i++) Bx(new Vector3(x1 - 0.12f, 2.2f, 16.3f + i * 0.8f), new Vector3(0.25f, 0.1f, 0.1f), Mats.Steel(new Color(0.6f, 0.6f, 0.62f)), false);
        Bx(new Vector3(x1 - 0.45f, 0.25f, 12.2f), new Vector3(0.6f, 0.5f, 2.2f), woodM);
        var g = new GameObject("HallLamp64"); g.transform.SetParent(up, false); g.transform.position = L((x0 + x1) * 0.5f, H - 0.4f, zc);
        var l = g.AddComponent<Light>(); l.type = LightType.Point; l.range = 12f; l.intensity = 1f; l.color = new Color(1f, 0.92f, 0.8f); l.shadows = LightShadows.None;
        var hs = Interact.Add(L(MLdoorX, 0f, zn - 0.9f), 1.3f, "go into the garage", f => ToGarage(f));
        hs.enabled = f => f.world == WorldId.House;
    }

    // ---------------- portals ----------------
    static Vector3 ToRanch(Vector3 local) { Vector2 c = Layout.HouseC, s = Layout.HouseSize; return new Vector3(c.x + local.x * s.x / 60f, local.y, c.y + local.z * s.y / 40f); }
    static bool OverPorch(Vector3 r) { return r.x > -53.4f && r.x < -30.6f && r.z > Layout.HouseC.y + Layout.HouseSize.y * 0.5f - 0.5f; }

    static void WindowOut(Frog f, Vector3 local, Vector3 outN)
    {
        Vector3 r = ToRanch(local) + outN * 1.6f;
        if (OverPorch(r)) { r.y = 3.9f + 0.3f; r.z = Mathf.Min(r.z, Layout.HouseC.y + Layout.HouseSize.y * 0.5f + 2.4f); }
        else r.y = Ranch.GY(r.x, r.z) + 0.3f;
        f.SendTo(WorldId.Ranch, r, Mathf.Atan2(outN.x, outN.z) * Mathf.Rad2Deg);
        f.Toast(OverPorch(r) ? "Out the window onto the porch roof" : "Out the upstairs window - wheee!", 2.2f);
        Sfx.Play(Sfx.Door, 0.6f);
        WindowExits++;
        Debug.Log("HOUSE64 window out " + f.nick + " -> " + r.ToString("0.0") + " porch " + OverPorch(r));
    }

    public static bool EnterUpstairs(Frog f, Vector3 ranchP)
    {
        if (HouseWorld.I == null || !Light55.On) return false;
        HouseWorld.I.EnsureBuilt();
        if (!Built) return false;
        Vector2 c = Layout.HouseC, s = Layout.HouseSize;
        float hx = Mathf.Clamp((ranchP.x - c.x) * 60f / s.x, -27.8f, 27.8f), hz = Mathf.Clamp((ranchP.z - c.y) * 40f / s.y, -17.8f, 17.8f);
        Vector3 p = L(hx, F2 + 0.15f, hz);
        Vector3 toC = new Vector3(-hx, 0f, -hz).normalized;
        for (int k = 0; k < 12; k++)
        {
            if (!Physics.CheckCapsule(p + Vector3.up * 0.45f, p + Vector3.up * 0.75f, 0.45f, 1 << HouseWorld.WallLayer, QueryTriggerInteraction.Ignore)) break;
            p += toC * 0.8f;
        }
        f.SendTo(WorldId.House, p, Mathf.Atan2(toC.x, toC.z) * Mathf.Rad2Deg);
        f.Toast("In through the upstairs window - James's 2nd floor! (stairs down by the landing)", 3f);
        Sfx.Play(Sfx.Door, 0.6f);
        UpEntries++;
        Debug.Log("HOUSE64 upstairs in " + f.nick + " from ranch " + ranchP.ToString("0.0") + " -> local " + (p - Worlds.HouseO).ToString("0.0"));
        return true;
    }

    public static void FromGarage(Frog f)
    {
        if (HouseWorld.I == null) return;
        HouseWorld.I.EnsureBuilt();
        if (!Built) { HouseWorld.I.Enter(f); return; }
        f.SendTo(WorldId.House, L(MLdoorX, 0.15f, MLz1 - 1.4f), 180f);
        f.Toast("The mudroom - the kitchen door is on your right", 2.4f);
        Sfx.Play(Sfx.Door, 0.9f);
        GarageTrips++; MudTrips++;
        Debug.Log("HOUSE64 garage -> mudroom " + f.nick + " local " + (f.transform.position - Worlds.HouseO).ToString("0.0"));
    }
    static void ToGarage(Frog f)
    {
        Vector3 p = new Vector3(GarageDoor.x, 0f, GarageDoor.z + 1.6f); p.y = Ranch.GY(p.x, p.z) + 0.3f;
        f.SendTo(WorldId.Ranch, p, 0f);
        f.Toast("James's garage", 1.8f);
        Sfx.Play(Sfx.Door, 0.9f);
        GarageTrips++; MudTrips++;
        Debug.Log("HOUSE64 mudroom -> garage " + f.nick + " at " + p.ToString("0.0"));
    }
    // ffu67b: the outside back door (a real opening): E / A takes you across it
    public static void BackIn(Frog f)
    {
        Vector3 p = new Vector3(BackDoor.x, 0f, BackDoor.z + 1.5f); p.y = Ranch.GY(p.x, p.z) + 0.3f;
        f.SendTo(WorldId.Ranch, p, 0f);
        f.Toast("In the back door - James's garage", 2f);
        Sfx.Play(Sfx.Door, 0.9f); BackDoorIn++;
        Debug.Log("HOUSE67 back door in -> garage " + f.nick + " at " + p.ToString("0.0") + " inGarage " + RobotNav.InGarage(p));
    }
    public static void BackOut(Frog f)
    {
        Vector3 p = new Vector3(BackDoor.x, 0f, BackDoor.z - 1.6f); p.y = Ranch.GY(p.x, p.z) + 0.3f;
        f.SendTo(WorldId.Ranch, p, 180f);
        f.Toast("Out the garage back door", 1.8f);
        Sfx.Play(Sfx.Door, 0.9f); BackDoorOut++;
        Debug.Log("HOUSE67 back door out " + f.nick + " at " + p.ToString("0.0"));
    }

    // ---------------- ranch side ----------------
    public static void RanchSide()
    {
        var root = new GameObject("Ranch64").transform;
        Vector2 c = Layout.HouseC, s = Layout.HouseSize;
        float x0 = c.x - s.x * 0.5f, z1 = c.y + s.y * 0.5f;
        float px0 = -53.6f, px1 = -30.4f, pz0 = z1, pz1 = z1 + 7f;
        // porch cover top: walkable
        var pc = new GameObject("PorchRoof64Col"); pc.transform.SetParent(root, false);
        pc.transform.position = new Vector3((px0 + px1) * 0.5f, 3.75f, (pz0 + pz1) * 0.5f);
        pc.AddComponent<BoxCollider>().size = new Vector3(px1 - px0 + 0.6f, 0.25f, pz1 - pz0 + 0.6f);
        // ladder at the porch's west end
        float lx = px0 - 0.55f, lz = (pz0 + pz1) * 0.5f + 1f;
        Material lw = Mats.Lit(new Color(0.55f, 0.4f, 0.25f));
        foreach (float dz in new[] { -0.4f, 0.4f }) Mats.Prim(PrimitiveType.Cube, root, new Vector3(lx, 2.1f, lz + dz), new Vector3(0.08f, 4.2f, 0.08f), lw, false);
        for (float y = 0.35f; y < 4.1f; y += 0.35f) Mats.Prim(PrimitiveType.Cube, root, new Vector3(lx, y, lz), new Vector3(0.06f, 0.06f, 0.8f), lw, false);
        var lb = Interact.Add(new Vector3(lx - 0.7f, 0.4f, lz), 1.4f, "climb the ladder onto the porch roof", f => { f.Teleport(new Vector3(px0 + 1.4f, 4.2f, lz)); f.Toast("On the porch roof! Upstairs windows: E / A", 2.5f); LadderClimbs++; Debug.Log("HOUSE64 ladder up " + f.nick); });
        lb.enabled = f => f.world == WorldId.Ranch && f.vehicle == null;
        var lt = Interact.Add(new Vector3(px0 + 1.1f, 3.95f, lz), 1.2f, "climb down the ladder", f => { f.Teleport(new Vector3(lx - 1.0f, Ranch.GY(lx - 1f, lz) + 0.3f, lz)); LadderClimbs++; });
        lt.enabled = f => f.world == WorldId.Ranch && f.vehicle == null;
        // upstairs windows reachable from the porch roof
        for (int i = 0; i < 9; i++)
        {
            float x = x0 + s.x / 9f * (i + 0.5f);
            if (x < px0 + 0.6f || x > px1 - 0.6f) continue;
            Vector3 wp = new Vector3(x, 6.8f, z1);
            var hw = Interact.Add(new Vector3(x, 3.95f, z1 + 0.9f), 1.5f, "climb in the upstairs window", f => EnterUpstairs(f, wp));
            hw.enabled = f => f.world == WorldId.Ranch && f.vehicle == null && Light55.On;
        }
        // ffu67b: the MUDROOM wing (closed shell, its inside is the house world's mudroom) between the house's east wall and
        // the garage's back wall, and the garage's door into it (inside the garage only)
        Material wm = Mats.Lit(new Color(0.9f, 0.87f, 0.8f)), rm = Mats.Lit(new Color(0.36f, 0.36f, 0.4f));
        Vector3 wc = new Vector3((MRx0 + MRx1) * 0.5f, MRH * 0.5f, (MRz0 + MRz1) * 0.5f);
        Mats.Prim(PrimitiveType.Cube, root, wc, new Vector3(MRx1 - MRx0, MRH, MRz1 - MRz0 - 0.02f), wm, true).name = "Mudroom67";
        Mats.Prim(PrimitiveType.Cube, root, new Vector3(wc.x + 0.1f, MRH + 0.12f, wc.z - 0.1f), new Vector3(MRx1 - MRx0 + 0.4f, 0.24f, MRz1 - MRz0 + 0.2f), rm, true);
        for (int i = 0; i < 2; i++)   // two small windows on the east face (lit), like the mudroom inside
            Mats.Prim(PrimitiveType.Cube, root, new Vector3(MRx1 + 0.02f, 2.2f, 6.5f + i * 3.2f), new Vector3(0.04f, 1.1f, 1.2f), Mats.Unlit(new Color(0.95f, 0.85f, 0.6f)), false);
        Ranch.Sign(new Vector3(MRx1 + 0.06f, 3.5f, (MRz0 + MRz1) * 0.5f), 90f, "MUDROOM", new Color(0.3f, 0.25f, 0.2f), 2.6f, 0.5f);
        Material dm = Mats.Lit(new Color(0.42f, 0.27f, 0.15f)), tr = Mats.Lit(new Color(0.86f, 0.84f, 0.8f));
        Vector3 g = GarageDoor;
        float gz = Layout.GarageC.y - Layout.GarageSize.y * 0.5f + 0.52f;   // inner face of the back wall
        Mats.Prim(PrimitiveType.Cube, root, new Vector3(g.x, 1.2f, gz), new Vector3(1.6f, 2.4f, 0.05f), dm, false);
        Mats.Prim(PrimitiveType.Cube, root, new Vector3(g.x, 2.45f, gz + 0.01f), new Vector3(1.9f, 0.12f, 0.06f), tr, false);
        Mats.Prim(PrimitiveType.Cube, root, new Vector3(g.x - 0.85f, 1.2f, gz + 0.01f), new Vector3(0.12f, 2.5f, 0.06f), tr, false);
        Mats.Prim(PrimitiveType.Cube, root, new Vector3(g.x + 0.85f, 1.2f, gz + 0.01f), new Vector3(0.12f, 2.5f, 0.06f), tr, false);
        Mats.Prim(PrimitiveType.Sphere, root, new Vector3(g.x + 0.55f, 1.1f, gz + 0.05f), Vector3.one * 0.1f, Mats.Steel(new Color(0.75f, 0.7f, 0.5f)), false);
        Ranch.Sign(new Vector3(g.x, 2.95f, gz + 0.06f), 180f, "MUDROOM - HOUSE", new Color(0.3f, 0.25f, 0.2f), 2.6f, 0.5f);
        var gi = Interact.Add(new Vector3(g.x, 0.3f, g.z + 1.25f), 1.3f, "go through the mudroom door into the house", f => FromGarage(f));
        gi.enabled = f => f.world == WorldId.Ranch && f.vehicle == null && RobotNav.InGarage(f.transform.position);
        // the OUTSIDE back door: frame + auto door in the opening Ranch.Garage leaves at BackDoor.x
        Vector3 b = BackDoor; float oz = Layout.GarageC.y - Layout.GarageSize.y * 0.5f;
        foreach (float side in new[] { -0.03f, 0.53f })
        {
            Mats.Prim(PrimitiveType.Cube, root, new Vector3(b.x, BackDoorH + 0.06f, oz + side), new Vector3(BackDoorW + 0.3f, 0.12f, 0.06f), tr, false);
            Mats.Prim(PrimitiveType.Cube, root, new Vector3(b.x - BackDoorW * 0.5f - 0.06f, BackDoorH * 0.5f, oz + side), new Vector3(0.12f, BackDoorH, 0.06f), tr, false);
            Mats.Prim(PrimitiveType.Cube, root, new Vector3(b.x + BackDoorW * 0.5f + 0.06f, BackDoorH * 0.5f, oz + side), new Vector3(0.12f, BackDoorH, 0.06f), tr, false);
        }
        var hinge = new GameObject("BackDoor67"); hinge.transform.SetParent(root, false); hinge.transform.position = new Vector3(b.x - BackDoorW * 0.5f, 0f, oz + 0.25f);
        var panel = Mats.Prim(PrimitiveType.Cube, hinge.transform, new Vector3(BackDoorW * 0.5f, BackDoorH * 0.5f - 0.02f, 0f), new Vector3(BackDoorW - 0.04f, BackDoorH - 0.06f, 0.06f), dm, true);
        foreach (float kz in new[] { -0.06f, 0.06f }) Mats.Prim(PrimitiveType.Sphere, hinge.transform, new Vector3(BackDoorW - 0.3f, 1.1f, kz), Vector3.one * 0.1f, Mats.Steel(new Color(0.75f, 0.7f, 0.5f)), false);
        hinge.AddComponent<BackDoor67>().panel = panel.GetComponent<Collider>();
        Ranch.Sign(new Vector3(b.x, BackDoorH + 0.55f, oz - 0.08f), 0f, "GARAGE - BACK DOOR", new Color(0.3f, 0.25f, 0.2f), 2.6f, 0.5f);
        var bi = Interact.Add(new Vector3(b.x, 0.3f, oz - 0.9f), 1.3f, "go in the back door into the garage", f => BackIn(f));
        bi.enabled = f => f.world == WorldId.Ranch && f.vehicle == null && !RobotNav.InGarage(f.transform.position);
        var bo = Interact.Add(new Vector3(b.x, 0.3f, oz + 1.4f), 1.2f, "go out the back door", f => BackOut(f));
        bo.enabled = f => f.world == WorldId.Ranch && f.vehicle == null && RobotNav.InGarage(f.transform.position);
        // anything else sitting where the mudroom wing now stands (for the probe)
        var hits = Physics.OverlapBox(wc, new Vector3((MRx1 - MRx0) * 0.5f - 0.3f, MRH * 0.5f - 0.2f, (MRz1 - MRz0) * 0.5f - 0.3f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        var names = new List<string>(); foreach (var h in hits) if (h != null && h.gameObject.name != "Mudroom67") names.Add(h.gameObject.name);
        Debug.Log("HOUSE67 mudroom wing x " + MRx0 + ".." + MRx1 + " z " + MRz0 + ".." + MRz1 + " (house-local x " + MLx0 + ".." + MLx1 + " z " + MLz0 + ".." + MLz1 + "), garage mudroom door x " + g.x + ", outside back door x " + b.x + " | other colliders in the footprint: " + (names.Count == 0 ? "none" : string.Join(", ", names.ToArray())));
        root.gameObject.AddComponent<HouseUp64Watch>();
        Debug.Log("HOUSE64 ranch side: porch roof collider, ladder, " + "upstairs window spots, garage back door at " + g.ToString("0.0"));
    }
}

// a froggy hopping into the hollow shell through an upstairs window goes to the matching spot on the 2nd floor
public class HouseUp64Watch : MonoBehaviour
{
    float wallT; bool open;
    void Update()
    {
        if (Game.I == null || !Light55.On) return;
        wallT -= Time.deltaTime;
        if (wallT <= 0f)
        {
            wallT = 0.5f; open = false;
            foreach (var b in RanchBreak.HousePieces) if (b != null && b.state != 0 && b.kind == BreakKind.Wall) { open = true; break; }
        }
        if (open) return;
        Rect r = RanchBreak.HouseInner;
        foreach (var f in Game.I.frogs)
        {
            if (f == null || f.world != WorldId.Ranch || f.vehicle != null || f.netPuppet) continue;
            Vector3 p = f.transform.position;
            if (p.y > Layout.HouseH * 0.5f - 0.2f && p.y < Layout.HouseH - 0.3f && r.Contains(new Vector2(p.x, p.z))) HouseUp64.EnterUpstairs(f, p);
        }
    }
}

// ffu67b: the garage's outside back door swings open (inward) when a froggy is near and closes behind them; closed = solid
public class BackDoor67 : MonoBehaviour
{
    public Collider panel;
    float a;
    public static bool Open;
    void Update()
    {
        bool near = false;
        Vector3 c = transform.position + new Vector3(HouseUp64.BackDoorW * 0.5f, 0f, 0f);
        if (Game.I != null) foreach (var f in Game.I.frogs) if (f != null && f.world == WorldId.Ranch && f.vehicle == null && (f.transform.position - c).sqrMagnitude < 6.5f) { near = true; break; }
        var d = DadFroggy.F; if (!near && d != null && d.world == WorldId.Ranch && d.vehicle == null && (d.transform.position - c).sqrMagnitude < 6.5f) near = true;
        float t = near ? 95f : 0f;
        a = Mathf.MoveTowards(a, t, 240f * Time.deltaTime);
        transform.localRotation = Quaternion.Euler(0f, -a, 0f);   // swings into the garage (+z)
        bool open = a > 60f;
        if (panel != null && panel.enabled == open) panel.enabled = !open;
        if (open != Open) { Open = open; if (open) Sfx.Play(Sfx.Door, 0.5f); }
    }
}
