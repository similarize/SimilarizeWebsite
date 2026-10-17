using System.Collections.Generic;
using UnityEngine;

// ffu50 (Bill, 2026-10-16): "Let's make it so Figure 03 has its very own rocket. And only he can get in." + the same release:
// space materials -> arrowheads -> arrows -> Figure 03's bow -> a cartoon cow hunt -> cook + eat at the garden campfire.
//  * FIGURE 03'S ROCKET: our own model in his style (cream knit body with ribs, dark grey nose / fins / legs / skirt, a dark
//    visor band with a glowing ring, a glowing strip up the side, "FIGURE 03" name plates) on its own small octagonal pad
//    near the ROBOT MISSION PAD (site picker, first clear spot). It flies the ffu20 mission flight unchanged
//    (RobotMission with ship = this rocket, own = true): Figure 03 always uses it (phone ORDERS "Fly your rocket", any MISSION
//    tab mission, or A / E at the ramp while you DRIVE IT! him). OWNER-LOCKED: any frog / player / pet / other robot at the
//    ramp gets "Only Figure 03 can fly this rocket" (HUD toast for players + a bubble over the ramp for everyone); the phone
//    order for any other robot is refused the same way.
//  * MATERIALS: every robot mission sample (Mars rock / Callisto ice) also goes into the shared stash (Stash50), plus one
//    chunk of meteorite metal per trip. Figure 03 tips his into the MATERIALS bin at his pad; a second bin sits between the
//    display tables at the robot mission pad. Bins show the count; the phone MISSION tab has a live "Stash" item.
//  * WORKBENCH (garden, near the campfire): bring materials (A / E at a bin takes up to 4) -> arrowheads (metal = 2) -> arrows
//    (arrowhead + a stick from the bundle) -> hand them to Figure 03 (A / E next to him) or drop them in the shared quiver.
//  * BOW: Figure 03 carries a bow on his back. Phone ORDERS "Hunt (bow)" = he walks to a ranch cow, draws, shoots; while you
//    DRIVE IT! him, A / E shoots (aim assist to a cow ahead). Kid-friendly: a hit tips the cow over with stars and a POOF
//    (Destruct's AnimalBreak: it pops back in its pen later) and leaves a cartoon DRUMSTICK pickup. No blood, ever.
//  * CAMPFIRE: carry the drumstick to the garden campfire -> cook it (lights the fire if needed, 5 s) -> eat it (happy hop +
//    hearts + a toast).
// Local per device (not synced), saved through SaveStore (ff.stash.*).

public partial class MissionShip
{
    // Figure 03's rocket: same proportions / hatch / ramp / plume as the shared ship so the mission flight is reused as is
    public static MissionShip BuildFig03()
    {
        var go = new GameObject("Figure 03 rocket");
        var s = go.AddComponent<MissionShip>();
        Transform t = go.transform;
        s.vis = Mats.Node(t, "Vis", Vector3.zero);
        Transform v = s.vis;
        Material knit = Mats.Lit(new Color(0.93f, 0.9f, 0.82f));   // matte cream (his knit suit); the ribs below give the knit texture
        Material rib = Mats.Lit(new Color(0.84f, 0.81f, 0.73f));
        Material grey = Mats.Paint(new Color(0.24f, 0.25f, 0.27f), 0.5f), greyD = Mats.Lit(new Color(0.14f, 0.15f, 0.16f));
        Material visor = Mats.Paint(new Color(0.05f, 0.05f, 0.07f), 0.95f);
        Material glowM = Mats.Unlit(new Color(0.62f, 0.92f, 1f)), cream = Mats.Lit(new Color(0.96f, 0.94f, 0.88f));
        const float R = 2.0f;
        // knit body + ribs (horizontal welts read like a knitted sleeve)
        Mats.Prim(PrimitiveType.Cylinder, v, new Vector3(0f, 10f, 0f), new Vector3(R * 2f, 7f, R * 2f), knit);
        for (int k = 0; k < 12; k++) Mats.Prim(PrimitiveType.Cylinder, v, new Vector3(0f, 4.0f + k * 1.08f, 0f), new Vector3(R * 2f + 0.05f, 0.07f, R * 2f + 0.05f), rib);
        // dark grey skirt
        Mats.Prim(PrimitiveType.Cylinder, v, new Vector3(0f, 3.1f, 0f), new Vector3(R * 2f + 0.1f, 0.9f, R * 2f + 0.1f), grey);
        // nose: cream ogive, a dark glossy visor band with a glowing ring (his face), dark grey tip
        Mats.Prim(PrimitiveType.Sphere, v, new Vector3(0f, 17f, 0f), new Vector3(R * 2f, 6.6f, R * 2f), knit);
        Mats.Prim(PrimitiveType.Sphere, v, new Vector3(0f, 17.5f, 0f), new Vector3(R * 2f + 0.08f, 2.3f, R * 2f + 0.08f), visor);
        Mats.Prim(PrimitiveType.Cylinder, v, new Vector3(0f, 17.5f, 0f), new Vector3(R * 2f + 0.12f, 0.05f, R * 2f + 0.12f), glowM);
        Mats.Prim(PrimitiveType.Sphere, v, new Vector3(0f, 19.6f, 0f), new Vector3(1.3f, 2.6f, 1.3f), grey);
        // the glowing strip up the side (beside the hatch) and a second one on the back
        Mats.Prim(PrimitiveType.Cube, v, new Vector3(1.05f, 10.4f, R * 0.86f), new Vector3(0.16f, 11f, 0.08f), glowM).transform.localRotation = Quaternion.Euler(0f, 30f, 0f);
        Mats.Prim(PrimitiveType.Cube, v, new Vector3(0f, 10.4f, -R - 0.01f), new Vector3(0.16f, 11f, 0.06f), glowM);
        // name plates (front above the hatch + back)
        for (int side = 0; side < 2; side++)
        {
            float zz = side == 0 ? R + 0.02f : -R - 0.02f;
            Transform pl = Mats.Node(v, "NamePlate", new Vector3(0f, 7.6f, zz));
            pl.localRotation = Quaternion.Euler(0f, side == 0 ? 0f : 180f, 0f);
            Mats.Prim(PrimitiveType.Cube, pl, Vector3.zero, new Vector3(2.5f, 0.62f, 0.05f), greyD);
            Mats.Prim(PrimitiveType.Cube, pl, new Vector3(0f, -0.36f, 0f), new Vector3(2.5f, 0.05f, 0.055f), glowM);
            var tg = new GameObject("Name"); tg.transform.SetParent(pl, false);
            tg.transform.localPosition = new Vector3(0f, 0f, 0.035f); tg.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var tm = tg.AddComponent<TextMesh>(); UIK.WorldText(tm, 64, 0.042f);
            tm.text = "FIGURE 03"; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = new Color(0.96f, 0.94f, 0.88f);
        }
        // three swept dark grey fins
        for (int k = 0; k < 3; k++)
        {
            Quaternion q = Quaternion.Euler(0f, 60f + k * 120f, 0f);
            var fin = Mats.Prim(PrimitiveType.Cube, v, q * new Vector3(0f, 4.4f, R + 0.55f), new Vector3(0.14f, 3.2f, 1.3f), grey);
            fin.transform.localRotation = q * Quaternion.Euler(18f, 0f, 0f);
        }
        // four landing legs + pads (dark grey)
        for (int k = 0; k < 4; k++)
        {
            Quaternion q = Quaternion.Euler(0f, 45f + k * 90f, 0f);
            var leg = Mats.Prim(PrimitiveType.Cube, v, q * new Vector3(0f, 1.7f, R + 0.7f), new Vector3(0.22f, 3.6f, 0.22f), greyD);
            leg.transform.localRotation = q * Quaternion.Euler(-22f, 0f, 0f);
            Mats.Prim(PrimitiveType.Cylinder, v, q * new Vector3(0f, 0.08f, R + 1.35f), new Vector3(0.9f, 0.08f, 0.9f), greyD);
        }
        Material bell = Mats.Steel(new Color(0.36f, 0.36f, 0.38f));
        Mats.Prim(PrimitiveType.Cylinder, v, new Vector3(0f, 2.2f, 0f), new Vector3(R * 2f - 0.1f, 0.05f, R * 2f - 0.1f), greyD);
        for (int k = 0; k < 3; k++) Mats.Prim(PrimitiveType.Cylinder, v, Quaternion.Euler(0f, k * 120f, 0f) * new Vector3(0f, 1.9f, 0.75f), new Vector3(0.8f, 0.4f, 0.8f), bell);
        // hatch (soft cool light inside) + ramp, same hinge as the shared ship
        Mats.Prim(PrimitiveType.Cube, v, new Vector3(0f, 4.4f, R - 0.02f), new Vector3(1.3f, 2.0f, 0.08f), Mats.Unlit(new Color(0.75f, 0.92f, 1f)));
        Mats.Prim(PrimitiveType.Cube, v, new Vector3(0f, 5.45f, R + 0.02f), new Vector3(1.5f, 0.08f, 0.06f), greyD);
        s.ramp = Mats.Node(v, "Ramp", new Vector3(0f, 3.45f, R));
        Mats.Prim(PrimitiveType.Cube, s.ramp, new Vector3(0f, 0f, 2.3f), new Vector3(1.3f, 0.08f, 4.6f), greyD);
        Mats.Prim(PrimitiveType.Cube, s.ramp, new Vector3(0f, 0.06f, 2.3f), new Vector3(1.1f, 0.03f, 4.4f), cream);
        Mats.Prim(PrimitiveType.Cube, s.ramp, new Vector3(0.5f, 0.08f, 2.3f), new Vector3(0.06f, 0.03f, 4.4f), glowM);
        Mats.Prim(PrimitiveType.Cube, s.ramp, new Vector3(-0.5f, 0.08f, 2.3f), new Vector3(0.06f, 0.03f, 4.4f), glowM);
        // plume (cooler, bluish-white core so it reads as his)
        s.plume = Mats.Node(t, "Plume", new Vector3(0f, 1.6f, 0f));
        var outer = Mats.Prim(PrimitiveType.Sphere, s.plume, new Vector3(0f, -4.5f, 0f), new Vector3(3.0f, 9f, 3.0f), Mats.Unlit(new Color(1f, 0.6f, 0.25f)));
        var inner = Mats.Prim(PrimitiveType.Sphere, s.plume, new Vector3(0f, -2.8f, 0f), new Vector3(1.6f, 5.6f, 1.6f), Mats.Unlit(new Color(0.85f, 0.95f, 1f)));
        Mats.NoShadows(outer); Mats.NoShadows(inner);
        s.plume.gameObject.SetActive(false);
        var gl = new GameObject("Glow"); gl.transform.SetParent(t, false); gl.transform.localPosition = new Vector3(0f, -2f, 0f);
        s.glow = gl.AddComponent<Light>(); s.glow.type = LightType.Point; s.glow.range = 30f; s.glow.color = new Color(0.9f, 0.8f, 0.7f); s.glow.shadows = LightShadows.None; s.glow.enabled = false;
        s.roar = Sfx.Loop(go, Sfx.EngRocket != null ? Sfx.EngRocket : Sfx.Rotor);
        // a body collider so nobody walks through it (nobody else can get in anyway)
        var cc = go.AddComponent<CapsuleCollider>(); cc.center = new Vector3(0f, 11f, 0f); cc.radius = R; cc.height = 18f; cc.direction = 1;
        if (Look.Mobile) Mats.NoShadows(go);
        return s;
    }
}

public static class Stash50
{
    public static int rock, ice, metal, arrows, figArrows, ver;
    static bool loaded;
    public static void L()
    {
        if (loaded) return; loaded = true;
        rock = SaveStore.GetInt("ff.stash.rock", 0); ice = SaveStore.GetInt("ff.stash.ice", 0); metal = SaveStore.GetInt("ff.stash.metal", 0);
        arrows = SaveStore.GetInt("ff.stash.arrows", 0); figArrows = SaveStore.GetInt("ff.stash.figArrows", 0);
    }
    public static void Save()
    {
        SaveStore.SetInt("ff.stash.rock", rock); SaveStore.SetInt("ff.stash.ice", ice); SaveStore.SetInt("ff.stash.metal", metal);
        SaveStore.SetInt("ff.stash.arrows", arrows); SaveStore.SetInt("ff.stash.figArrows", figArrows);
        ver++;
    }
    public static int Materials { get { L(); return rock + ice + metal; } }
    public static void Add(int kind, int n, string why)
    {
        L();
        if (kind == 0) rock += n; else if (kind == 1) ice += n; else metal += n;
        Save();
        Debug.Log("STASH50: +" + n + " " + (kind == 0 ? "Mars rock" : kind == 1 ? "Callisto ice" : "meteorite metal") + " (" + why + ") -> " + Line);
    }
    public static string Line { get { L(); return "Mars rock " + rock + " · Callisto ice " + ice + " · meteorite metal " + metal + " · arrows " + arrows + " (+" + figArrows + " with Figure 03)"; } }
    public static string Short { get { L(); return "Stash R" + rock + " I" + ice + " M" + metal + " · " + (arrows + figArrows) + " arrows"; } }
    public static string BinText { get { L(); return "MATERIALS\n<size=40>Mars rock " + rock + "  ·  Callisto ice " + ice + "  ·  metal " + metal + "</size>"; } }
}

public static class Fig03Rocket
{
    public const string Owner = "figure03";
    public static MissionShip ship;
    public static Vector2 PadC;
    public static float PadTop, Yaw;
    public static bool Built;
    public const float PadR = 5.5f;
    static Transform root, binPile;
    static TextMesh binText, bubble;
    public static float bubbleT;
    static readonly List<Transform> binItems = new List<Transform>();
    public static Vector3 Base { get { return new Vector3(PadC.x, PadTop, PadC.y); } }
    static Quaternion Q { get { return Quaternion.Euler(0f, Yaw, 0f); } }
    public static Vector3 BinPos { get { Vector3 p = Base + Q * new Vector3(7.6f, 0f, 2.6f); p.y = Ranch.GY(p.x, p.z); return p; } }
    public static Vector3 BinFront { get { Vector3 p = BinPos + Q * new Vector3(0f, 0f, 1.35f); return RobotNav.G(p.x, p.z); } }
    public static float BinFace { get { return Yaw + 180f; } }
    public static Vector3 RampFoot { get { return ship != null ? ship.RampFoot : Base; } }
    public static bool OnSlab(float x, float z) { return Built && (new Vector2(x, z) - PadC).sqrMagnitude < PadR * PadR; }
    public static bool Parked { get { return ship != null && RobotMission.FigActive == null; } }

    // first clear spot: a ring round the robot mission pad, then next to his charging pad by the garage
    static List<Vector2> Candidates()
    {
        var l = new List<Vector2> { new Vector2(-80f, -84f), new Vector2(-58f, -62f), new Vector2(-58f, -82f), new Vector2(-80f, -36f), new Vector2(-58f, -40f) };
        for (int ring = 0; ring < 2; ring++)
            for (int a = 0; a < 12; a++)
            {
                float ang = a * 30f * Mathf.Deg2Rad, rad = ring == 0 ? 27f : 36f;
                l.Add(MissionSite.PadC + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad);
            }
        l.Add(new Vector2(-44f, 42f)); l.Add(new Vector2(-56f, 40f)); l.Add(new Vector2(-30f, 46f)); l.Add(new Vector2(-60f, 30f));
        return l;
    }
    static int why0, whyR, whyS, whyB;
    static bool Clear(Vector2 c, out int blockers)
    {
        blockers = 0;
        float lo = 1e9f, hi = -1e9f;
        for (float u = -9f; u <= 9.01f; u += 3f)
            for (float w = -9f; w <= 9.01f; w += 3f)
            {
                if (u * u + w * w > 81f) continue;
                float x = c.x + u, z = c.y + w;
                if (Mathf.Abs(x) > 165f || Mathf.Abs(z) > 165f) { why0++; return false; }
                if (Layout.RoadDist(x, z) < Layout.TrackW * 0.5f + 1.5f || Layout.PondQ(x, z) < 1.25f) { whyR++; return false; }
                if (JetLayout.Reserved(x, z) || RcTrack.Reserved(x, z) || MonsterPark.Reserved(x, z) || CarrierLayout.Reserved(x, z) || MissionSite.Reserved(x, z) || RanchLife.Reserved(x, z)) { whyR++; return false; }
                if ((new Vector2(x, z) - StorySet.PadXZ).magnitude < 25f || (new Vector2(x, z) - Layout.PadC).magnitude < 28f) { whyR++; return false; }
                if (Mathf.Abs(x - Layout.HouseC.x) < Layout.HouseSize.x * 0.5f + 6f && Mathf.Abs(z - Layout.HouseC.y) < Layout.HouseSize.y * 0.5f + 6f) { whyB++; return false; }
                if (Mathf.Abs(x - Layout.GarageC.x) < Layout.GarageSize.x * 0.5f + 6f && Mathf.Abs(z - Layout.GarageC.y) < Layout.GarageSize.y * 0.5f + 8f) { whyB++; return false; }
                float g = Ranch.GY(x, z); lo = Mathf.Min(lo, g); hi = Mathf.Max(hi, g);
            }
        if (hi - lo > 1.4f) { whyS++; return false; }
        var hits = Physics.OverlapBox(new Vector3(c.x, lo + 12.3f, c.y), new Vector3(9f, 12f, 9f), Quaternion.identity, ~((1 << 2) | (1 << Vehicle.FrogLayer)), QueryTriggerInteraction.Ignore);
        foreach (var h in hits) if (!(h is TerrainCollider) && h.GetComponentInParent<Robot>() == null) blockers++;
        if (blockers > 0) { whyB++; return false; }
        return true;
    }

    public static void Build()
    {
        if (Built || !Worlds.SpaceOn || MissionSite.ship == null) return;
        Physics.SyncTransforms();
        Vector2 site = Vector2.zero; bool ok = false; int bl;
        foreach (var c in Candidates()) if (Clear(c, out bl)) { site = c; ok = true; break; }
        if (!ok) { site = new Vector2(-80f, -84f); Debug.Log("FIG03ROCKET: no fully clear site - using " + site.ToString("0")); }
        Debug.Log("FIG03ROCKET: site " + site.ToString("0.0") + " (rejected: bounds " + why0 + " reserved/road " + whyR + " slope " + whyS + " built " + whyB + ")");
        PadC = site;
        // ramp faces the house (where Figure 03 comes from)
        Vector2 to = Layout.HouseC - site;
        Yaw = Mathf.Round(Mathf.Atan2(to.x, to.y) * Mathf.Rad2Deg / 15f) * 15f;
        root = new GameObject("Figure 03 rocket pad").transform;
        float top = -99f, lo = 99f;
        for (int a = 0; a < 8; a++) for (int k = 0; k <= 2; k++)
            {
                float ang = a * 45f * Mathf.Deg2Rad, rr = k * PadR * 0.5f;
                float g = Ranch.GY(site.x + Mathf.Cos(ang) * rr, site.y + Mathf.Sin(ang) * rr);
                top = Mathf.Max(top, g); lo = Mathf.Min(lo, g);
            }
        PadTop = top + 0.2f;
        float bottom = lo - 1.2f, hgt = PadTop - bottom;
        Material conc = Mats.Lit(new Color(0.3f, 0.31f, 0.33f)), creamM = Mats.Lit(new Color(0.93f, 0.9f, 0.82f)), glowM = Mats.Unlit(new Color(0.55f, 0.88f, 1f));
        float side = PadR * 2f * 0.924f;
        for (int k = 0; k < 2; k++)
        {
            var sl = Mats.Prim(PrimitiveType.Cube, root, new Vector3(site.x, bottom + hgt * 0.5f, site.y), new Vector3(side, hgt, side), conc, true);
            sl.transform.rotation = Quaternion.Euler(0f, Yaw + 22.5f + k * 45f, 0f);
        }
        Mats.Prim(PrimitiveType.Cylinder, root, new Vector3(site.x, PadTop + 0.01f, site.y), new Vector3(8.8f, 0.01f, 8.8f), creamM);
        Mats.Prim(PrimitiveType.Cylinder, root, new Vector3(site.x, PadTop + 0.015f, site.y), new Vector3(8.2f, 0.01f, 8.2f), conc);
        Mats.Prim(PrimitiveType.Cylinder, root, new Vector3(site.x, PadTop + 0.02f, site.y), new Vector3(6.6f, 0.01f, 6.6f), glowM);
        Mats.Prim(PrimitiveType.Cylinder, root, new Vector3(site.x, PadTop + 0.025f, site.y), new Vector3(6.3f, 0.01f, 6.3f), conc);
        // "FIGURE 03" painted on the pad in front of the ramp
        var pt = new GameObject("PadName"); pt.transform.SetParent(root, false);
        pt.transform.position = Base + Q * new Vector3(0f, 0.04f, -3.6f);
        pt.transform.rotation = Q * Quaternion.Euler(90f, 180f, 0f);
        var ptm = pt.AddComponent<TextMesh>(); UIK.WorldText(ptm, 64, 0.06f);
        ptm.text = "FIGURE 03"; ptm.anchor = TextAnchor.MiddleCenter; ptm.alignment = TextAlignment.Center; ptm.color = new Color(0.93f, 0.9f, 0.82f);
        Vector3 sg = Base + Q * new Vector3(-7.2f, 0f, 4.5f);
        Ranch.Sign(new Vector3(sg.x, Ranch.GY(sg.x, sg.z) + 2.4f, sg.z), Yaw, "FIGURE 03'S ROCKET\n<size=24>only Figure 03 can fly it</size>", new Color(0.18f, 0.19f, 0.21f), 6.5f, 2f);
        BuildBin();
        Mats.NoShadows(root.gameObject);
        ship = MissionShip.BuildFig03();
        Park();
        // the polite "no" bubble over the ramp
        var bg = new GameObject("RocketBubble"); bg.transform.SetParent(root, false);
        bg.transform.position = RampFoot + Vector3.up * 3.4f;
        bubble = bg.AddComponent<TextMesh>(); UIK.WorldText(bubble, 64, 0.045f);
        bubble.text = "Only Figure 03 can fly this rocket"; bubble.anchor = TextAnchor.MiddleCenter; bubble.alignment = TextAlignment.Center;
        bubble.color = new Color(1f, 0.92f, 0.5f);
        bg.AddComponent<Billboard>();
        bg.SetActive(false);
        // players: A / E at the ramp = the polite no
        var h = Interact.Add(RampFoot + Vector3.up * 0.2f, 2.4f, "board Figure 03's rocket", f => Refuse(f.nick, f, "player"));
        h.enabled = f => Parked && f.vehicle == null && f.world == WorldId.Ranch;
        Built = true;
        Debug.Log("FIG03ROCKET: built pad at " + Base.ToString("0.0") + " yaw " + Yaw + ", ramp foot " + RampFoot.ToString("0.0") + ", bin " + BinPos.ToString("0.0"));
    }

    static void BuildBin()
    {
        Transform b = MakeBin(root, BinPos, Yaw, out binText, out binPile);
        b.name = "Figure 03 materials bin";
        Fig50.AddBinSpot(BinPos, BinPos + Q * new Vector3(0f, 0f, 1.2f));
    }

    // a dark grey materials crate with a cream rim, open top, label + live count
    public static Transform MakeBin(Transform parent, Vector3 pos, float yaw, out TextMesh text, out Transform pile)
    {
        Transform b = new GameObject("Materials bin").transform; b.SetParent(parent, false);
        b.position = pos; b.rotation = Quaternion.Euler(0f, yaw, 0f);
        Material dk = Mats.Lit(new Color(0.22f, 0.23f, 0.25f)), rim = Mats.Lit(new Color(0.93f, 0.9f, 0.82f)), inside = Mats.Lit(new Color(0.1f, 0.1f, 0.11f));
        Mats.Prim(PrimitiveType.Cube, b, new Vector3(0f, 0.08f, 0f), new Vector3(1.5f, 0.16f, 1.0f), dk, true);
        Mats.Prim(PrimitiveType.Cube, b, new Vector3(0f, 0.45f, 0.48f), new Vector3(1.5f, 0.8f, 0.06f), dk, true);
        Mats.Prim(PrimitiveType.Cube, b, new Vector3(0f, 0.45f, -0.48f), new Vector3(1.5f, 0.8f, 0.06f), dk, true);
        Mats.Prim(PrimitiveType.Cube, b, new Vector3(0.72f, 0.45f, 0f), new Vector3(0.06f, 0.8f, 1.0f), dk, true);
        Mats.Prim(PrimitiveType.Cube, b, new Vector3(-0.72f, 0.45f, 0f), new Vector3(0.06f, 0.8f, 1.0f), dk, true);
        Mats.Prim(PrimitiveType.Cube, b, new Vector3(0f, 0.17f, 0f), new Vector3(1.38f, 0.02f, 0.9f), inside);
        Mats.Prim(PrimitiveType.Cube, b, new Vector3(0f, 0.86f, 0.48f), new Vector3(1.56f, 0.05f, 0.09f), rim);
        Mats.Prim(PrimitiveType.Cube, b, new Vector3(0f, 0.86f, -0.48f), new Vector3(1.56f, 0.05f, 0.09f), rim);
        Mats.Prim(PrimitiveType.Cube, b, new Vector3(0.72f, 0.86f, 0f), new Vector3(0.09f, 0.05f, 1.06f), rim);
        Mats.Prim(PrimitiveType.Cube, b, new Vector3(-0.72f, 0.86f, 0f), new Vector3(0.09f, 0.05f, 1.06f), rim);
        pile = Mats.Node(b, "Pile", new Vector3(0f, 0.2f, 0f));
        var tg = new GameObject("BinCount"); tg.transform.SetParent(b, false); tg.transform.localPosition = new Vector3(0f, 1.75f, 0f);
        text = tg.AddComponent<TextMesh>(); UIK.WorldText(text, 64, 0.032f);
        text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center; text.richText = true; text.color = new Color(0.8f, 0.95f, 1f);
        tg.AddComponent<Billboard>();
        Fig50.bins.Add(text);
        return b;
    }

    public static void Park()
    {
        if (ship == null) return;
        ship.transform.position = Base;
        ship.transform.rotation = Q;
        ship.flame = 0f; ship.SetRamp(1f);
        if (ship.plume != null) ship.plume.gameObject.SetActive(false);
    }

    public static void IntoBin(int kind, Transform s)
    {
        if (binPile == null) { Object.Destroy(s.gameObject); return; }
        s.SetParent(binPile, false);
        s.localPosition = new Vector3(Random.Range(-0.55f, 0.55f), Random.Range(0f, 0.12f), Random.Range(-0.32f, 0.32f));
        s.localRotation = Quaternion.Euler(0f, Random.value * 360f, 0f);
        s.localScale = Vector3.one * 0.9f;
        binItems.Add(s);
        while (binItems.Count > 14) { if (binItems[0] != null) Object.Destroy(binItems[0].gameObject); binItems.RemoveAt(0); }
        FX.Sparkle(s.position + Vector3.up * 0.2f, kind == 0 ? new Color(1f, 0.6f, 0.3f) : new Color(0.6f, 0.95f, 1f), 6);
    }
    public static void BinTaken(int n) { for (int i = 0; i < n && binItems.Count > 0; i++) { if (binItems[0] != null) Object.Destroy(binItems[0].gameObject); binItems.RemoveAt(0); } }

    // the polite no: a HUD toast for the player it concerns + the bubble over the ramp for everybody
    static readonly Dictionary<Object, float> cool = new Dictionary<Object, float>();
    public static void Refuse(string who, Frog toast, string what)
    {
        bubbleT = 3.2f;
        if (bubble != null) bubble.gameObject.SetActive(true);
        if (toast != null && toast.human) toast.Toast("Only Figure 03 can fly this rocket", 2.8f);
        Sfx.PlayAt(Sfx.Click, RampFoot, 0.5f, 30f, 0.8f);
        Debug.Log("FIG03ROCKET refuse: " + who + " (" + what + ") - \"Only Figure 03 can fly this rocket\"");
    }
    static bool Cool(Object o, float secs)
    {
        float t; if (cool.TryGetValue(o, out t) && Time.time < t) return false;
        cool[o] = Time.time + secs; return true;
    }

    static float scanT;
    public static void Tick(float dt)
    {
        if (!Built) return;
        if (bubbleT > 0f) { bubbleT -= dt; if (bubbleT <= 0f && bubble != null) bubble.gameObject.SetActive(false); }
        scanT -= dt;
        if (scanT > 0f || !Parked) return;
        scanT = 0.25f;
        Vector3 rf = RampFoot;
        const float Rr = 3.0f;
        if (Game.I != null)
            foreach (Frog f in Game.I.frogs)
            {
                if (f == null || f.world != WorldId.Ranch || f.vehicle != null || f.remote != null) continue;
                if (RobotNav.Flat(f.transform.position - rf) < Rr && Mathf.Abs(f.transform.position.y - rf.y) < 3f && Cool(f, 5f))
                    Refuse(f.nick, f, f.human ? "player" : f.charId >= 4 ? "pet" : "froggy");
            }
        if (RanchLife.I != null)
            foreach (var r in RanchLife.I.robots)
            {
                if (r == null || r.id == Owner || r.hidden || r.displayOnly || r.world != WorldId.Ranch || r.seatedIn != null) continue;
                if (RobotNav.Flat(r.transform.position - rf) < Rr && Cool(r, 5f)) Refuse(r.robotName, r.manual, "robot" + (r.manual != null ? ", driven by " + r.manual.nick : ""));
            }
        foreach (var a in Animal.All)
        {
            if (a == null || a.petName.Length == 0) continue;
            if (RobotNav.Flat(a.Pos - rf) < Rr && Cool(a, 6f)) Refuse(a.petName, null, "pet");
        }
    }
}

// ---------------- Figure 03 hunting with his bow ----------------
public class FigHunt50
{
    readonly Robot r; readonly Frog by;
    int ph, shots; float t, total;
    Animal cow; Arrow50 arrow;
    string line = "looking for a cow";
    public string Line { get { return "hunting: " + line; } }
    public FigHunt50(Robot robot, Frog from) { r = robot; by = from; }

    public void Stop(string say)
    {
        if (r.hunt == this) r.hunt = null;
        r.cmd = "auto"; r.ExtReset(); r.ExtPose(0);
        Fig50.Bow(r, false);
        if (Fig50.bowBorrower == r) { Fig50.bowBorrower = null; Fig50.ReturnBow(r); }   // ffu54: hand the bow back
        if (say != null)
        {
            Debug.Log("FIGHUNT50 end: " + say);
            if (Game.I != null) foreach (Frog f in Game.I.frogs) if (f != null && f.human) f.Toast(say, 3.2f);
        }
    }

    public void Tick(float dt)
    {
        t += dt; total += dt;
        if (total > 150f) { Stop(r.robotName + ": the cows are too quick today - back to chores"); return; }
        Vector3 p = r.transform.position;
        switch (ph)
        {
            case 0:
                cow = Fig50.NearestCow(p);
                if (cow == null) { Stop(r.robotName + ": no cows to hunt right now"); return; }
                ph = 1; t = 0f; r.ExtReset(); line = "stalking a cow";
                Debug.Log("FIGHUNT50: target cow at " + cow.Pos.ToString("0.0") + " dist " + RobotNav.Flat(cow.Pos - p).ToString("0.0") + " arrows " + Fig50.ArrowsFor(r));
                break;
            case 1:
                {
                    if (!Fig50.Huntable(cow)) { ph = 0; break; }
                    Vector3 cp = cow.Pos;
                    float d = RobotNav.Flat(cp - p);
                    if (d < 14f) { ph = 2; t = 0f; Fig50.Bow(r, true); line = "drawing the bow"; Sfx.PlayAt(Sfx.Pick(Sfx.Servo), p + Vector3.up * 1.4f, 0.4f, 30f, 0.8f); break; }
                    Vector3 away = p - cp; away.y = 0f; if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
                    Vector3 goal = cp + away.normalized * 11.5f;
                    goal = RobotNav.G(goal.x, goal.z);
                    r.ExtPose(0);
                    r.ExtWalk(goal, r.RunSpeed * 0.75f, 0.6f, dt);
                    line = "stalking a cow (" + Mathf.RoundToInt(d) + " m)";
                    if (t > 75f) { Stop(r.robotName + ": couldn't get close to a cow"); return; }
                    break;
                }
            case 2:
                if (!Fig50.Huntable(cow)) { ph = 0; Fig50.Bow(r, false); break; }
                r.ExtPose(1);
                r.ExtFace(Mathf.Atan2(cow.Pos.x - p.x, cow.Pos.z - p.z) * Mathf.Rad2Deg);
                Fig50.Bow(r, true);
                if (t > 1.2f)
                {
                    arrow = Fig50.Shoot(r, cow.Pos + Vector3.up * 0.85f);
                    shots++; ph = 3; t = 0f; line = "arrow away!";
                }
                break;
            case 3:
                r.ExtPose(1);
                if (arrow == null || arrow.done || t > 4f)
                {
                    if (arrow != null && arrow.hit) { ph = 4; t = 0f; line = "got one!"; r.ExtWave(1.6f); Fig50.Bow(r, false); }
                    else if (shots < 3 && Fig50.ArrowsFor(r) > 0) { ph = cow != null && Fig50.Huntable(cow) ? 1 : 0; t = 0f; line = "missed - trying again"; Debug.Log("FIGHUNT50: miss, shot " + shots); }
                    else { Stop(Fig50.ArrowsFor(r) > 0 ? r.robotName + ": missed three times - back to chores" : r.robotName + ": out of arrows - craft more at the garden workbench"); return; }
                }
                break;
            case 4:
                if (t > 1.6f) { Stop(r.robotName + ": got one! A drumstick is waiting by the cows - cook it on the garden campfire"); return; }
                break;
        }
    }
}

// a flying arrow (ballistic, swept hit test against the ranch cows)
public class Arrow50 : MonoBehaviour
{
    public bool done, hit;
    Vector3 v; float life, stuckT = -1f;
    public static Arrow50 Fire(Vector3 from, Vector3 target)
    {
        var go = new GameObject("Arrow");
        go.transform.position = from;
        Material shaft = Mats.Lit(new Color(0.62f, 0.45f, 0.26f)), head = Mats.Steel(new Color(0.55f, 0.56f, 0.6f)), fl = Mats.Lit(new Color(0.93f, 0.9f, 0.82f));
        Mats.Prim(PrimitiveType.Cylinder, go.transform, Vector3.zero, new Vector3(0.03f, 0.42f, 0.03f), Vector3.right * 90f, shaft);
        Mats.Prim(PrimitiveType.Cube, go.transform, new Vector3(0f, 0f, 0.45f), new Vector3(0.06f, 0.06f, 0.1f), new Vector3(0f, 0f, 45f), head);
        Mats.Prim(PrimitiveType.Cube, go.transform, new Vector3(0f, 0f, -0.36f), new Vector3(0.12f, 0.01f, 0.12f), fl);
        Mats.Prim(PrimitiveType.Cube, go.transform, new Vector3(0f, 0f, -0.36f), new Vector3(0.01f, 0.12f, 0.12f), fl);
        Mats.NoShadows(go);
        var a = go.AddComponent<Arrow50>();
        const float speed = 30f;
        Vector3 d = target - from; float hd = new Vector2(d.x, d.z).magnitude;
        float T = Mathf.Max(0.12f, hd / speed);
        a.v = d / T + Vector3.up * (0.5f * 9.81f * T);
        go.transform.rotation = Quaternion.LookRotation(a.v);
        Sfx.PlayAt(Sfx.Pick(Sfx.Servo), from, 0.5f, 40f, 1.8f);
        return a;
    }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.25f);
        if (stuckT >= 0f) { stuckT += dt; if (stuckT > 6f) Destroy(gameObject); return; }
        life += dt;
        Vector3 p = transform.position;
        int n = Mathf.CeilToInt(dt / 0.02f);
        float h = dt / Mathf.Max(1, n);
        for (int i = 0; i < n; i++)
        {
            Vector3 a = p;
            v.y -= 9.81f * h;
            p += v * h;
            Animal got = null;
            foreach (var c in Animal.All)
            {
                if (!Fig50.Huntable(c)) continue;
                Vector3 cc = c.Pos + Vector3.up * 0.85f;
                Vector3 ab = p - a; float k = Mathf.Clamp01(Vector3.Dot(cc - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
                if ((a + ab * k - cc).sqrMagnitude < 1.15f * 1.15f) { got = c; break; }
            }
            if (got != null)
            {
                done = hit = true;
                Debug.Log("ARROW50: HIT cow at " + got.Pos.ToString("0.0") + " after " + life.ToString("0.00") + " s");
                FX.Sparkle(p, new Color(1f, 0.9f, 0.4f), 8);
                TipOver50.Tip(got);
                foreach (var rr in GetComponentsInChildren<Renderer>()) rr.enabled = false;   // ffu50b: hide, keep the object (the hunt reads .hit)
                stuckT = 4f;   // gone in ~2 s
                return;
            }
            if (p.y < Ranch.GY(p.x, p.z) + 0.05f || life > 4f)
            {
                done = true; stuckT = 0f;
                p.y = Mathf.Max(p.y, Ranch.GY(p.x, p.z) + 0.05f);
                transform.position = p;
                FX.Dust(p, 0.6f);
                Debug.Log("ARROW50: miss, landed at " + p.ToString("0.0"));
                return;
            }
        }
        transform.position = p;
        if (v.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(v);
    }
}

// the cartoon tip-over: the cow rolls onto its side with circling stars, then POOFs (AnimalBreak: back in its pen later)
// and leaves a drumstick. No blood, ever.
public class TipOver50 : MonoBehaviour
{
    Animal a; float t, starT; Quaternion q0; Vector3 p0;
    public static readonly List<Animal> Respawning = new List<Animal>();
    public static void Tip(Animal a)
    {
        if (a == null || a.GetComponent<TipOver50>() != null) return;
        var c = a.gameObject.AddComponent<TipOver50>();
        c.a = a; c.q0 = a.transform.rotation; c.p0 = a.Pos;
        a.enabled = false;   // the animal stops moving (its Update would undo the roll)
        Sfx.PlayAt(Sfx.Bonk != null ? Sfx.Bonk : Sfx.Thud, a.Pos, 0.8f, 60f, 1.2f);
        AudioClip m = Sfx.AnimalVoice(string.IsNullOrEmpty(a.voice) ? "Cow" : a.voice);
        if (m != null) Sfx.PlayAt(m, a.Pos, 0.6f, 60f, 1.5f);
        Debug.Log("TIPOVER50: cow tipped over at " + a.Pos.ToString("0.0"));
    }
    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.1f);
        t += dt;
        float k = Mathf.Clamp01(t / 0.55f);
        float ang = 90f * (1f - Mathf.Pow(1f - k, 3f)) + (k >= 1f ? Mathf.Sin((t - 0.55f) * 14f) * 6f * Mathf.Max(0f, 1f - (t - 0.55f) * 2f) : 0f);
        transform.rotation = q0 * Quaternion.Euler(0f, 0f, ang);
        transform.position = p0 + Vector3.up * 0.25f * Mathf.Sin(k * Mathf.PI);
        starT -= dt;
        if (starT <= 0f)
        {
            starT = 0.12f;
            float sa = t * 9f;
            Vector3 c = p0 + Vector3.up * 1.3f + new Vector3(Mathf.Cos(sa), 0f, Mathf.Sin(sa)) * 0.6f;
            FX.Sparkle(c, new Color(1f, 0.92f, 0.3f), 3);
        }
        if (t > 1.4f)
        {
            AnimalBreak.Poof(a, p0);
            transform.rotation = q0; transform.position = p0;
            Meat50.Spawn(p0);
            Respawning.Add(a);
            Debug.Log("TIPOVER50: POOF -> drumstick at " + p0.ToString("0.0") + " (the cow pops back in its pen later)");
            Destroy(this);
        }
    }
}

// a cartoon drumstick lying in the grass (A / E: pick it up)
public class Meat50 : MonoBehaviour
{
    public static readonly List<Meat50> All = new List<Meat50>();
    Hotspot spot; float t, baseY;
    public static Transform Drumstick(Transform parent, bool cooked)
    {
        var g = new GameObject(cooked ? "Cooked drumstick" : "Drumstick").transform;
        if (parent != null) g.SetParent(parent, false);
        Material meat = Mats.Lit(cooked ? new Color(0.62f, 0.36f, 0.16f) : new Color(0.86f, 0.52f, 0.46f)), bone = Mats.Lit(new Color(0.97f, 0.95f, 0.88f));
        Mats.Prim(PrimitiveType.Sphere, g, new Vector3(0f, 0f, 0.07f), new Vector3(0.22f, 0.2f, 0.3f), meat).name = "Meat";
        Mats.Prim(PrimitiveType.Cylinder, g, new Vector3(0f, 0f, -0.13f), new Vector3(0.05f, 0.09f, 0.05f), Vector3.right * 90f, bone);
        Mats.Prim(PrimitiveType.Sphere, g, new Vector3(0.03f, 0f, -0.23f), Vector3.one * 0.065f, bone);
        Mats.Prim(PrimitiveType.Sphere, g, new Vector3(-0.03f, 0f, -0.23f), Vector3.one * 0.065f, bone);
        Mats.NoShadows(g.gameObject);
        return g;
    }
    public static void Spawn(Vector3 p)
    {
        while (All.Count >= 6) All[0].Remove();
        var g = Drumstick(null, false);
        g.localScale = Vector3.one * 2.2f;
        g.position = new Vector3(p.x, Ranch.GY(p.x, p.z) + 0.35f, p.z);
        var m = g.gameObject.AddComponent<Meat50>();
        m.baseY = g.position.y;
        m.spot = Interact.Add(g.position, 2.2f, "pick up the drumstick", f => { if (Fig50.Hold(f, 4, 1)) { Debug.Log("MEAT50: " + f.nick + " picked up the drumstick"); m.Remove(); } });
        m.spot.enabled = f => Fig50.HeldKind(f) == 0 && f.vehicle == null;
        All.Add(m);
        FX.Sparkle(g.position, new Color(1f, 0.9f, 0.5f), 10);
    }
    public void Remove() { All.Remove(this); Interact.All.Remove(spot); Destroy(gameObject); }
    void Update()
    {
        t += Time.deltaTime;
        transform.position = new Vector3(transform.position.x, baseY + Mathf.Sin(t * 2.2f) * 0.08f, transform.position.z);
        transform.rotation = Quaternion.Euler(0f, t * 60f, 0f);
        if (t > 400f) Remove();
    }
}

// the happy eat: hop + arms up, the drumstick at the mouth shrinking, crumbs, then hearts + a toast
public class Eat50 : MonoBehaviour
{
    Frog f; Transform food; float t;
    public static void Begin(Frog f)
    {
        var e = f.gameObject.AddComponent<Eat50>(); e.f = f;
        Transform at = f.model != null && f.model.head != null ? f.model.head : f.transform;
        e.food = Meat50.Drumstick(at, true);
        e.food.localPosition = new Vector3(0f, -0.08f, 0.38f); e.food.localRotation = Quaternion.Euler(-30f, 0f, 0f); e.food.localScale = Vector3.one * 1.3f;
        Sfx.PlayAt(Sfx.Pickup, f.transform.position, 0.6f, 30f, 1.2f);
        Debug.Log("EAT50: " + f.nick + " starts eating the cooked drumstick");
    }
    void LateUpdate()
    {
        float dt = Time.deltaTime; t += dt;
        if (f == null || f.model == null) { Destroy(this); return; }
        var m = f.model;
        float hop = Mathf.Abs(Mathf.Sin(t * 6.5f));
        if (m.bob != null) { m.bob.localPosition = new Vector3(0f, hop * 0.22f, 0f); m.bob.localScale = new Vector3(1f + (1f - hop) * 0.05f, 1f - (1f - hop) * 0.06f + hop * 0.04f, 1f); }
        if (m.armL != null) m.armL.localRotation = Quaternion.Euler(-120f + Mathf.Sin(t * 13f) * 20f, 0f, -15f);
        if (m.armR != null) m.armR.localRotation = Quaternion.Euler(-120f - Mathf.Sin(t * 13f) * 20f, 0f, 15f);
        if (food != null)
        {
            float k = Mathf.Clamp01(1f - t / 1.9f);
            food.localScale = Vector3.one * 1.3f * Mathf.Max(0.05f, k);
            if (Random.value < dt * 10f) FX.Puff(food.position, Random.insideUnitSphere * 0.6f + Vector3.up * 0.4f, 0.05f, 0.5f, new Color(0.8f, 0.55f, 0.3f));
            if (Random.value < dt * 5f) Sfx.PlayAt(Sfx.Pickup, f.transform.position, 0.25f, 20f, 1.6f + Random.value * 0.3f);
        }
        if (t > 2.0f)
        {
            if (food != null) Destroy(food.gameObject);
            for (int i = 0; i < 3; i++) FX.Sparkle(f.transform.position + Vector3.up * (1.2f + i * 0.2f), new Color(1f, 0.45f, 0.65f), 6);
            Sfx.Play(Sfx.Win, 0.4f, 1.2f);
            f.Toast("Yum! " + f.nick + " ate a campfire drumstick - full of energy!", 3.2f);
            Debug.Log("EAT50: " + f.nick + " ate the campfire drumstick (happy hop + hearts + toast)");
            Destroy(this);
        }
    }
}

// ---------------- the glue: held items, bins, workbench, campfire, Figure 03's bow + phone / DRIVE IT! hooks ----------------
public class Fig50 : MonoBehaviour
{
    public static Fig50 I;
    class Held { public int kind, n, rock, ice, metal; public Transform vis; }   // kind 1 materials, 2 arrowheads, 3 arrows, 4 drumstick
    static readonly Dictionary<Frog, Held> held = new Dictionary<Frog, Held>();
    public static readonly List<TextMesh> bins = new List<TextMesh>();
    static readonly List<Vector3> binPos = new List<Vector3>();
    static Transform benchRoot, quiverVis, spit, spitMeat;
    public static Vector3 BenchFront, BenchPos, FireFront;
    static int binVer = -1;
    // workbench craft in progress
    static Frog crafter; static float craftT; static int craftTo;
    // campfire cooking
    static Frog cookFor; static float cookT = -1f; static bool cookReady;
    static Hotspot figSpot;
    static Robot fig;
    static readonly Dictionary<Robot, Transform> bows = new Dictionary<Robot, Transform>();

    public static void Create()
    {
        if (I != null) return;
        I = new GameObject("ffu50 glue").AddComponent<Fig50>();
        Stash50.L();
        if (RanchLife.I != null) foreach (var r in RanchLife.I.robots) if (r != null && r.id == Fig03Rocket.Owner && !r.displayOnly) fig = r;
        if (fig != null) Bow(fig, false);
        if (MissionSite.ship != null)
        {
            Vector3 bp = MissionSite.ShipBase + new Vector3(0f, 0f, 12f); bp.y = Ranch.GY(bp.x, bp.z);
            TextMesh t; Transform pile;
            Fig03Rocket.MakeBin(null, bp, 0f, out t, out pile).name = "Mission pad materials bin";
            AddBinSpot(bp, bp + new Vector3(0f, 0f, 1.2f));
        }
        BuildBench();
        // A / E next to Figure 03 with arrows in hand = hand them over
        if (fig != null)
        {
            figSpot = Interact.Add(fig.transform.position, 2.6f, "hand Figure 03 your arrows", f => GiveFig(f));
            figSpot.enabled = f => HeldKind(f) == 3 && fig.world == WorldId.Ranch && !fig.hidden && fig.mission == null && f.world == WorldId.Ranch;
            figSpot.dynLabel = f => { Held h; return held.TryGetValue(f, out h) ? "hand Figure 03 your " + h.n + " arrow" + (h.n == 1 ? "" : "s") : "hand Figure 03 your arrows"; };
        }
        Debug.Log("FFU50: glue ready - fig " + (fig != null) + ", bins " + bins.Count + ", bench " + (benchRoot != null ? BenchPos.ToString("0.0") : "none") + ", campfire " + (Campfire.I != null ? Campfire.I.Pos.ToString("0.0") : "none") + ", " + Stash50.Line);
    }

    public static void AddBinSpot(Vector3 pos, Vector3 front)
    {
        binPos.Add(pos);
        var h = Interact.Add(front, 2.3f, "take materials", f => TakeFromBin(f));
        h.enabled = f => f.vehicle == null && f.world == WorldId.Ranch;
        h.dynLabel = f => HeldKind(f) != 0 ? "materials bin (" + Stash50.Short + ") - hands full" : Stash50.Materials > 0 ? "take space materials (" + Mathf.Min(4, Stash50.Materials) + " of " + Stash50.Materials + ")" : "materials bin is empty - send a robot on a mission";
    }

    // ---- held items ----
    public static int HeldKind(Frog f) { Held h; return f != null && held.TryGetValue(f, out h) ? h.kind : 0; }
    public static bool Hold(Frog f, int kind, int n, int rock = 0, int ice = 0, int metal = 0)
    {
        if (f == null) return false;
        if (HeldKind(f) != 0) { f.Toast("Your hands are full", 1.8f); return false; }
        var h = new Held { kind = kind, n = n, rock = rock, ice = ice, metal = metal };
        held[f] = h; MakeVis(f, h);
        return true;
    }
    static void Drop(Frog f) { Held h; if (held.TryGetValue(f, out h)) { if (h.vis != null) Destroy(h.vis.gameObject); held.Remove(f); } }
    static void MakeVis(Frog f, Held h)
    {
        if (h.vis != null) Destroy(h.vis.gameObject);
        Transform at = f.model != null ? (f.model.bob != null ? f.model.bob : f.model.transform) : f.transform;
        Transform g = new GameObject("Held").transform; g.SetParent(at, false);
        g.localPosition = new Vector3(0f, 0.5f, 0.48f);
        if (h.kind == 1)
        {
            int i = 0;
            for (int k = 0; k < h.rock; k++, i++) { var s = MissionSite.Sample(0, g, 40 + i); s.localPosition = new Vector3((i % 2 - 0.5f) * 0.18f, (i / 2) * 0.1f, 0f); }
            for (int k = 0; k < h.ice; k++, i++) { var s = MissionSite.Sample(1, g, 40 + i); s.localPosition = new Vector3((i % 2 - 0.5f) * 0.18f, (i / 2) * 0.1f, 0f); }
            for (int k = 0; k < h.metal; k++, i++) { var s = Metal(g); s.localPosition = new Vector3((i % 2 - 0.5f) * 0.18f, (i / 2) * 0.1f, 0f); }
        }
        else if (h.kind == 2) { for (int i = 0; i < Mathf.Min(h.n, 8); i++) { var a = Head(g); a.localPosition = new Vector3((i % 4 - 1.5f) * 0.07f, (i / 4) * 0.07f, 0f); } }
        else if (h.kind == 3) { for (int i = 0; i < Mathf.Min(h.n, 8); i++) { var a = ArrowVis(g); a.localPosition = new Vector3((i - 3.5f) * 0.025f, 0.05f, 0f); a.localRotation = Quaternion.Euler(-70f, 0f, 0f); } }
        else if (h.kind == 4) { var d = Meat50.Drumstick(g, false); d.localScale = Vector3.one * 1.4f; d.localRotation = Quaternion.Euler(-60f, 0f, 0f); }
        Mats.NoShadows(g.gameObject);
        h.vis = g;
    }
    static Transform Metal(Transform parent)
    {
        var g = new GameObject("Meteorite metal").transform; g.SetParent(parent, false);
        Material m = Mats.Steel(new Color(0.5f, 0.5f, 0.54f));
        Mats.Prim(PrimitiveType.Sphere, g, Vector3.zero, new Vector3(0.18f, 0.13f, 0.16f), m);
        Mats.Prim(PrimitiveType.Sphere, g, new Vector3(0.05f, 0.04f, 0f), new Vector3(0.1f, 0.08f, 0.1f), m);
        return g;
    }
    static Transform Head(Transform parent)
    {
        var g = new GameObject("Arrowhead").transform; g.SetParent(parent, false);
        Mats.Prim(PrimitiveType.Cube, g, Vector3.zero, new Vector3(0.05f, 0.09f, 0.015f), new Vector3(0f, 0f, 45f), Mats.Steel(new Color(0.55f, 0.5f, 0.52f)));
        return g;
    }
    static Transform ArrowVis(Transform parent)
    {
        var g = new GameObject("Arrow").transform; g.SetParent(parent, false);
        Mats.Prim(PrimitiveType.Cylinder, g, Vector3.zero, new Vector3(0.02f, 0.32f, 0.02f), Mats.Lit(new Color(0.62f, 0.45f, 0.26f)));
        Mats.Prim(PrimitiveType.Cube, g, new Vector3(0f, 0.34f, 0f), new Vector3(0.04f, 0.06f, 0.012f), new Vector3(0f, 0f, 45f), Mats.Steel(new Color(0.55f, 0.56f, 0.6f)));
        Mats.Prim(PrimitiveType.Cube, g, new Vector3(0f, -0.28f, 0f), new Vector3(0.08f, 0.1f, 0.008f), Mats.Lit(new Color(0.93f, 0.9f, 0.82f)));
        return g;
    }

    static void TakeFromBin(Frog f)
    {
        if (HeldKind(f) != 0) { f.Toast("Your hands are full", 1.8f); return; }
        if (Stash50.Materials <= 0) { f.Toast("The bin is empty - send a robot on a Mars / Callisto mission (phone > MISSION)", 3f); return; }
        int left = 4, r = Mathf.Min(left, Stash50.rock); left -= r; int i = Mathf.Min(left, Stash50.ice); left -= i; int m = Mathf.Min(left, Stash50.metal);
        Stash50.rock -= r; Stash50.ice -= i; Stash50.metal -= m; Stash50.Save();
        Hold(f, 1, r + i + m, r, i, m);
        Fig03Rocket.BinTaken(r + i + m);
        Sfx.PlayAt(Sfx.Pickup, f.transform.position, 0.5f, 30f, 1.1f);
        string what = (r > 0 ? r + " Mars rock" : "") + (i > 0 ? (r > 0 ? ", " : "") + i + " Callisto ice" : "") + (m > 0 ? (r + i > 0 ? ", " : "") + m + " meteorite metal" : "");
        f.Toast("Took " + what + " - craft arrowheads at the garden workbench", 3f);
        Debug.Log("FFU50 bin: " + f.nick + " took " + what + " -> stash " + Stash50.Line);
    }

    // ---- workbench (garden, near the campfire) ----
    static void BuildBench()
    {
        var G = Garden.I;
        Vector3 pos; float yaw;
        if (G != null)
        {
            Vector2[] cand = { new Vector2(12f, -5.5f), new Vector2(12.5f, 7f), new Vector2(-1f, -8.5f), new Vector2(13.5f, -1.5f), new Vector2(1f, 9f) };
            pos = G.WG(cand[0].x, cand[0].y);
            foreach (var c in cand)
            {
                Vector3 p = G.WG(c.x, c.y);
                var hits = Physics.OverlapBox(p + Vector3.up * 1.1f, new Vector3(1.6f, 0.9f, 1.4f), Quaternion.Euler(0f, G.Yaw, 0f), ~((1 << 2) | (1 << Vehicle.FrogLayer)), QueryTriggerInteraction.Ignore);
                int n = 0; foreach (var hc in hits) if (!(hc is TerrainCollider)) n++;
                if (n == 0) { pos = p; break; }
            }
            Vector3 to = G.FirePos - pos; to.y = 0f;
            yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;   // the bench's front faces the fire
        }
        else { pos = RobotNav.G(-31f, 33f); yaw = 0f; }
        BenchPos = pos;
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        benchRoot = new GameObject("Workbench").transform; benchRoot.position = pos; benchRoot.rotation = q;
        Material wood = Mats.TexTint("LB/wood", new Color(0.55f, 0.38f, 0.22f), 0.08f), woodD = Mats.Lit(new Color(0.36f, 0.24f, 0.14f));
        Material iron = Mats.Steel(new Color(0.3f, 0.31f, 0.33f)), stone = Mats.Lit(new Color(0.5f, 0.49f, 0.47f)), stick = Mats.Lit(new Color(0.66f, 0.5f, 0.3f));
        Transform b = benchRoot;
        Mats.Prim(PrimitiveType.Cube, b, new Vector3(0f, 0.9f, 0f), new Vector3(2.0f, 0.1f, 0.9f), wood, true);
        for (int a = -1; a <= 1; a += 2) for (int c = -1; c <= 1; c += 2) Mats.Prim(PrimitiveType.Cube, b, new Vector3(a * 0.9f, 0.43f, c * 0.37f), new Vector3(0.1f, 0.86f, 0.1f), woodD);
        Mats.Prim(PrimitiveType.Cube, b, new Vector3(0f, 0.25f, 0f), new Vector3(1.8f, 0.05f, 0.75f), woodD);
        // vise + knapping stone + hammer
        Mats.Prim(PrimitiveType.Cube, b, new Vector3(-0.7f, 1.05f, 0.1f), new Vector3(0.24f, 0.2f, 0.18f), iron);
        Mats.Prim(PrimitiveType.Cube, b, new Vector3(-0.7f, 1.05f, 0.28f), new Vector3(0.06f, 0.06f, 0.2f), iron);
        Mats.Prim(PrimitiveType.Sphere, b, new Vector3(0.05f, 1.0f, 0.05f), new Vector3(0.36f, 0.14f, 0.3f), stone);
        Mats.Prim(PrimitiveType.Cylinder, b, new Vector3(0.35f, 0.98f, 0.15f), new Vector3(0.04f, 0.14f, 0.04f), new Vector3(0f, 0f, 90f), woodD);
        Mats.Prim(PrimitiveType.Cube, b, new Vector3(0.22f, 0.98f, 0.15f), new Vector3(0.08f, 0.08f, 0.14f), iron);
        // stick bundle (endless sticks) on the right end
        for (int i = 0; i < 7; i++) Mats.Prim(PrimitiveType.Cylinder, b, new Vector3(0.7f + (i % 3 - 1) * 0.05f, 0.99f + (i / 3) * 0.04f, -0.1f), new Vector3(0.025f, 0.4f, 0.025f), new Vector3(90f, 0f, 0f), stick);
        Mats.Prim(PrimitiveType.Cylinder, b, new Vector3(0.7f, 1.01f, -0.1f), new Vector3(0.16f, 0.02f, 0.16f), new Vector3(90f, 0f, 0f), woodD);
        // the shared quiver (a little barrel) beside the bench
        Mats.Prim(PrimitiveType.Cylinder, b, new Vector3(1.35f, 0.38f, 0.1f), new Vector3(0.5f, 0.38f, 0.5f), woodD, true);
        Mats.Prim(PrimitiveType.Cylinder, b, new Vector3(1.35f, 0.62f, 0.1f), new Vector3(0.52f, 0.02f, 0.52f), iron);
        quiverVis = Mats.Node(b, "QuiverArrows", new Vector3(1.35f, 0.76f, 0.1f));
        Ranch.Sign(pos + q * new Vector3(0f, 1.75f, -0.55f), yaw, "WORKBENCH\n<size=20>rocks - arrowheads - arrows</size>", new Color(0.32f, 0.2f, 0.1f), 2.6f, 0.8f);   // ffu50b: faces the bench front, smaller
        Mats.NoShadows(b.gameObject);
        BenchFront = RobotNav.G((pos + q * new Vector3(0f, 0f, 1.25f)).x, (pos + q * new Vector3(0f, 0f, 1.25f)).z);
        var h = Interact.Add(BenchFront, 2.4f, "workbench", f => BenchAct(f));
        h.enabled = f => f.vehicle == null && f.world == WorldId.Ranch && !(HeldKind(f) == 3 && NearFig(f));   // ffu50b: arrows in hand + Figure 03 right here = hand them to him
        h.dynLabel = f =>
        {
            if (crafter != null) return crafter == f ? "crafting..." : "workbench busy (" + crafter.nick + ")";
            Held hh; held.TryGetValue(f, out hh);
            int k = hh != null ? hh.kind : 0;
            if (k == 1) return "chip " + (hh.rock + hh.ice + hh.metal * 2) + " arrowheads from your space materials";
            if (k == 2) return "make " + hh.n + " arrow" + (hh.n == 1 ? "" : "s") + " (arrowhead + stick)";
            if (k == 3) return "put " + hh.n + " arrow" + (hh.n == 1 ? "" : "s") + " in the shared quiver (" + Stash50.arrows + ")";
            if (k == 4) return "workbench (cook the drumstick on the campfire)";
            return "workbench - bring space materials from a mission bin (quiver: " + Stash50.arrows + " arrows)";
        };
        // the campfire: cook / eat (the light / douse hotspot steps aside while you use the fire for food, Campfire.cs)
        if (Campfire.I != null)
        {
            Vector3 fp = Campfire.I.Pos;
            spit = Mats.Node(Campfire.I.transform, "Spit", Vector3.zero);
            for (int sd = -1; sd <= 1; sd += 2)
            {
                Mats.Prim(PrimitiveType.Cylinder, spit, new Vector3(sd * 0.7f, 0.5f, 0f), new Vector3(0.04f, 0.5f, 0.04f), woodD);
                Mats.Prim(PrimitiveType.Cylinder, spit, new Vector3(sd * 0.7f, 1.0f, 0f), new Vector3(0.03f, 0.12f, 0.03f), new Vector3(0f, 0f, sd * 30f), woodD);
            }
            Mats.Prim(PrimitiveType.Cylinder, spit, new Vector3(0f, 1.02f, 0f), new Vector3(0.025f, 0.78f, 0.025f), new Vector3(0f, 0f, 90f), woodD);
            Mats.NoShadows(spit.gameObject);
            var fh = Interact.Add(fp + Vector3.up * 0.2f, 2.6f, "cook", f => FireAct(f));
            fh.enabled = f => UsesFire(f);
            fh.dynLabel = f => HeldKind(f) == 4 ? (cookT >= 0f && cookFor != f ? "the spit is busy (" + cookFor.nick + "'s drumstick)" : "cook the drumstick on the campfire") : cookReady ? "eat the cooked drumstick" : "cooking... " + Mathf.CeilToInt(Mathf.Max(0f, 5f - cookT)) + " s";
            Vector3 to = Campfire.I.Pos - BenchPos; to.y = 0f;
            FireFront = RobotNav.G(fp.x - to.normalized.x * 1.9f, fp.z - to.normalized.z * 1.9f);
        }
    }

    static void BenchAct(Frog f)
    {
        if (crafter != null) { if (crafter != f) f.Toast("The workbench is busy - " + crafter.nick + " is crafting", 2f); return; }
        Held h; held.TryGetValue(f, out h);
        int k = h != null ? h.kind : 0;
        if (k == 1 || k == 2) { crafter = f; craftT = 0f; craftTo = k + 1; Sfx.PlayAt(Sfx.Clank, BenchPos, 0.6f, 40f, 1.3f); Debug.Log("FFU50 bench: " + f.nick + " crafting " + (k == 1 ? "arrowheads" : "arrows")); return; }
        if (k == 3)
        {
            Stash50.arrows += h.n; Stash50.Save();
            f.Toast(h.n + " arrows in the shared quiver (" + Stash50.arrows + ") - Figure 03 uses them for hunting", 3f);
            Debug.Log("FFU50 bench: " + f.nick + " put " + h.n + " arrows in the shared quiver -> " + Stash50.Line);
            Drop(f); Sfx.PlayAt(Sfx.Thud, BenchPos, 0.4f, 30f, 1.5f);
            return;
        }
        f.Toast(k == 4 ? "Take the drumstick to the campfire to cook it" : "Bring space materials from a mission bin (Mars rock, Callisto ice, meteorite metal)", 2.6f);
    }

    static void GiveFig(Frog f)
    {
        Held h; if (!held.TryGetValue(f, out h) || h.kind != 3 || fig == null) return;
        Stash50.figArrows += h.n; Stash50.Save();
        f.Toast("You handed Figure 03 " + h.n + " arrows (he has " + Stash50.figArrows + ") - phone ORDERS > Hunt", 3f);
        Debug.Log("FFU50: " + f.nick + " handed Figure 03 " + h.n + " arrows -> " + Stash50.Line);
        Drop(f);
        fig.ExtWave(1.4f);
        Sfx.PlayAt(Sfx.Pickup, fig.transform.position, 0.5f, 30f, 1.3f);
    }

    static bool NearFig(Frog f) { return fig != null && !fig.hidden && fig.mission == null && fig.world == WorldId.Ranch && RobotNav.Flat(fig.transform.position - f.transform.position) < 2.6f; }
    // ffu53 (audit C5): food on the spit (anyone's) or this froggy eating = the campfire is in use, no light / put-out prompt
    public static bool FireBusy(Frog f) { return cookT >= 0f || spitMeat != null || (f != null && f.GetComponent<Eat50>() != null); }
    public static bool UsesFire(Frog f) { return f != null && (HeldKind(f) == 4 || (cookFor == f && cookT >= 0f)); }
    static void FireAct(Frog f)
    {
        if (HeldKind(f) == 4)
        {
            if (cookT >= 0f && cookFor != f) { f.Toast("The spit is busy - wait for " + cookFor.nick + "'s drumstick", 2f); return; }
            if (!Campfire.I.Lit) Campfire.I.Light(f);
            Drop(f);
            cookFor = f; cookT = 0f; cookReady = false;
            if (spitMeat != null) Destroy(spitMeat.gameObject);
            spitMeat = Meat50.Drumstick(spit, false);
            spitMeat.localPosition = new Vector3(0f, 1.02f, 0f); spitMeat.localScale = Vector3.one * 2.2f; spitMeat.localRotation = Quaternion.Euler(0f, 90f, 0f);
            f.Toast("Cooking the drumstick... (5 s)", 2.5f);
            Debug.Log("FFU50 campfire: " + f.nick + " cooking a drumstick (fire lit " + Campfire.I.Lit + ")");
            return;
        }
        if (cookFor == f && cookReady)
        {
            cookFor = null; cookT = -1f; cookReady = false;
            if (spitMeat != null) Destroy(spitMeat.gameObject);
            Eat50.Begin(f);
            return;
        }
        if (cookFor == f) f.Toast("Still cooking - " + Mathf.CeilToInt(5f - cookT) + " s", 1.5f);
    }

    public static string HeldHint(Frog f)
    {
        Held h;
        if (cookFor == f && cookT >= 0f) return cookReady ? "Your drumstick is ready - eat it at the campfire" : "Your drumstick is cooking on the campfire";
        if (!held.TryGetValue(f, out h)) return null;
        switch (h.kind)
        {
            case 1: return "Carrying " + h.n + " space materials - chip arrowheads at the garden workbench (by the campfire)";
            case 2: return h.n + " arrowheads - add sticks at the workbench to make arrows";
            case 3: return h.n + " arrows - hand them to Figure 03, or drop them in the workbench quiver";
            case 4: return "A drumstick! Cook it on the garden campfire";
        }
        return null;
    }

    // ---- Figure 03: bow, arrows, hunting, rocket ----
    public static int ArrowsFor(Robot r) { return r != null && (r.id == Fig03Rocket.Owner || r == bowBorrower) ? Stash50.figArrows + Stash50.arrows : 0; }
    public static Robot bowBorrower;   // ffu54: another robot borrowed Figure 03's bow (asked first)
    public static bool Bowed(Robot r) { Transform b; return r != null && bows.TryGetValue(r, out b) && b != null && b.gameObject.activeSelf && r.id != Fig03Rocket.Owner; }
    static void UseArrow() { if (Stash50.figArrows > 0) Stash50.figArrows--; else if (Stash50.arrows > 0) Stash50.arrows--; Stash50.Save(); }

    public static void ReturnBow(Robot r) { Transform b; if (r != null && r.id != Fig03Rocket.Owner && bows.TryGetValue(r, out b) && b != null) b.gameObject.SetActive(false); }
    public static void Bow(Robot r, bool inHand)
    {
        if (r == null || r.Body == null) return;
        Transform b;
        if (bows.TryGetValue(r, out b) && b != null && !b.gameObject.activeSelf) b.gameObject.SetActive(true);
        if (!bows.TryGetValue(r, out b) || b == null)
        {
            b = new GameObject("Bow").transform; b.SetParent(r.Body, false);
            Material limb = Mats.Paint(new Color(0.22f, 0.23f, 0.25f), 0.5f), grip = Mats.Lit(new Color(0.93f, 0.9f, 0.82f)), str = Mats.Unlit(new Color(0.9f, 0.9f, 0.85f));
            for (int k = -3; k <= 3; k++)
            {
                if (k == 0) continue;
                float y = k * 0.17f, z = 0.13f * (1f - (k / 3.2f) * (k / 3.2f));
                float slope = -2f * 0.13f * k / (3.2f * 3.2f) / 0.17f;   // dz/dy
                var seg = Mats.Prim(PrimitiveType.Cube, b, new Vector3(0f, y, z), new Vector3(0.045f, 0.19f, 0.05f), limb);
                seg.transform.localRotation = Quaternion.Euler(-Mathf.Atan(slope) * Mathf.Rad2Deg, 0f, 0f);
            }
            Mats.Prim(PrimitiveType.Cube, b, new Vector3(0f, 0f, 0.13f), new Vector3(0.06f, 0.2f, 0.065f), grip);
            Mats.Prim(PrimitiveType.Cube, b, new Vector3(0f, 0f, -0.02f), new Vector3(0.012f, 1.04f, 0.012f), str).name = "String";
            var nock = Mats.Prim(PrimitiveType.Cylinder, b, new Vector3(0.03f, 0f, 0.2f), new Vector3(0.02f, 0.38f, 0.02f), new Vector3(90f, 0f, 0f), Mats.Lit(new Color(0.62f, 0.45f, 0.26f)));
            nock.name = "Nocked";
            Mats.NoShadows(b.gameObject);
            bows[r] = b;
        }
        float h = r.height;
        if (inHand) { b.localPosition = new Vector3(-0.16f, h * 0.6f, h * 0.2f + 0.12f); b.localRotation = Quaternion.identity; }
        else { b.localPosition = new Vector3(0f, h * 0.58f, -(r.bulk * 0.32f + 0.12f)); b.localRotation = Quaternion.Euler(0f, 180f, 35f); }
        Transform n = b.Find("Nocked");
        if (n != null) n.gameObject.SetActive(inHand && ArrowsFor(r) > 0);
    }

    public static Arrow50 Shoot(Robot r, Vector3 target)
    {
        Bow(r, true);
        Transform b; bows.TryGetValue(r, out b);
        Vector3 from = b != null ? b.position + r.transform.forward * 0.3f : r.transform.position + Vector3.up * r.height * 0.6f + r.transform.forward * 0.6f;
        UseArrow();
        Debug.Log("FIG03 BOW: shot from " + from.ToString("0.0") + " at " + target.ToString("0.0") + " (" + RobotNav.Flat(target - from).ToString("0.0") + " m), arrows left " + ArrowsFor(r));
        return Arrow50.Fire(from, target);
    }

    public static bool Huntable(Animal a)
    {
        return a != null && a.enabled && a.kind == Animal.Kind.Cow && a.petName.Length == 0 && !a.pal && a.GetComponent<TipOver50>() == null
            && (a.transform.parent == null || a.transform.parent.GetComponentInParent<LivestockTrailer>() == null);
    }
    public static Animal NearestCow(Vector3 p, float maxD = 400f)
    {
        Animal best = null; float bd = maxD;
        foreach (var a in Animal.All) if (Huntable(a)) { float d = RobotNav.Flat(a.Pos - p); if (d < bd) { bd = d; best = a; } }
        return best;
    }

    public static string PhoneRocket(Robot r, Frog f)
    {
        if (Fig03Rocket.ship == null) return "No rocket here";
        if (r.id != Fig03Rocket.Owner) { Fig03Rocket.Refuse(r.robotName, f, "robot, phone order"); return "Only Figure 03 can fly this rocket"; }
        string s = RobotMission.Start(r, 0, 3);
        if (r.mission != null && RobotFeed.I != null) RobotFeed.I.Show(r, 1);
        return s;
    }
    public static string PhoneHunt(Robot r, Frog f)
    {
        string ask = "";
        if (r.id != Fig03Rocket.Owner)
        {
            // ffu54 (robot parity): the bow is Figure 03's - others ask him first; he says yes unless he is using it
            Robot fig = null;
            if (RanchLife.I != null) foreach (var o in RanchLife.I.robots) if (o.id == Fig03Rocket.Owner) fig = o;
            if (fig != null && (fig.hunt != null || fig.mission != null)) return r.robotName + " asked Figure 03 to borrow his bow - \"Sorry, I'm using it right now!\"";
            if (bowBorrower != null && bowBorrower != r && bowBorrower.hunt != null) return bowBorrower.robotName + " has Figure 03's bow right now";
            bowBorrower = r;
            ask = r.robotName + " asked Figure 03 to borrow his bow - \"Sure! Be gentle with the cows.\" ";
            Debug.Log("FIGHUNT50: " + ask);
        }
        if (r.mission != null) return r.robotName + " is on a mission";
        if (ArrowsFor(r) <= 0) { if (bowBorrower == r) bowBorrower = null; return ask + r.robotName + ": no arrows yet - craft some at the garden workbench (space materials from a mission bin)"; }
        if (r.drv != null) r.EndDrive(null);
        if (r.manual != null) r.ReleaseManual(false);
        r.ResetForMission();
        r.cmd = "hunt";
        r.hunt = new FigHunt50(r, f);
        Debug.Log("FIGHUNT50: phone order Hunt from " + (f != null ? f.nick : "-") + ", arrows " + ArrowsFor(r));
        return ask + r.robotName + ": going hunting (" + ArrowsFor(r) + " arrows) - kid-friendly: a hit makes a cow go POOF";
    }

    static bool NearRamp(Robot r) { return Fig03Rocket.Parked && RobotNav.Flat(r.transform.position - Fig03Rocket.RampFoot) < 3.4f; }
    public static string ManualPrompt(Robot r)
    {
        if (r == null) return "A wave";
        if (r.id == Fig03Rocket.Owner)
        {
            if (NearRamp(r)) return "A board your rocket (Mars)";
            return ArrowsFor(r) > 0 ? "A shoot bow (" + ArrowsFor(r) + " arrows)" : "A wave";
        }
        return NearRamp(r) ? "A wave (only Figure 03 can fly this rocket)" : "A wave";
    }
    // DRIVE IT!: A / E for Figure 03 = board his rocket at the ramp, else shoot an arrow (aim assist to a cow ahead)
    public static bool ManualUse(Robot r, ref PIn i)
    {
        if (!i.use || r == null) return false;
        if (r.id != Fig03Rocket.Owner)
        {
            if (NearRamp(r)) { Fig03Rocket.Refuse(r.robotName, r.manual, "robot, DRIVE IT!"); i.use = false; i.hop = false; return true; }
            return false;
        }
        if (NearRamp(r))
        {
            Frog f = r.manual;
            string s = RobotMission.Start(r, 0, 3);
            Debug.Log("FIG03ROCKET: DRIVE IT! board -> " + s);
            if (f != null) f.Toast(s, 3f);
            if (r.mission != null && RobotFeed.I != null) RobotFeed.I.Show(r, 1);
            i.use = false; i.hop = false;
            return true;
        }
        if (ArrowsFor(r) > 0)
        {
            Vector3 fw = r.transform.forward; fw.y = 0f; fw.Normalize();
            Vector3 p = r.transform.position;
            Animal best = null; float bd = 32f;
            foreach (var a in Animal.All)
            {
                if (!Huntable(a)) continue;
                Vector3 d = a.Pos - p; d.y = 0f; float m = d.magnitude;
                if (m < bd && m > 1f && Vector3.Angle(fw, d) < 35f) { bd = m; best = a; }
            }
            Vector3 target = best != null ? best.Pos + Vector3.up * 0.85f : p + fw * 24f + Vector3.up * 1.2f;
            Shoot(r, target);
            if (r.manual != null) r.manual.Toast(best != null ? "Arrow away! (" + ArrowsFor(r) + " left)" : "Arrow away! No cow ahead (" + ArrowsFor(r) + " left)", 1.6f);
            I.manualBowT = 3f;
            i.use = false; i.hop = false;
            return true;
        }
        if (r.manual != null) r.manual.Toast("No arrows - craft some at the garden workbench", 2f);
        return false;
    }
    float manualBowT;

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.25f);
        Fig03Rocket.Tick(dt);
        // bow placement: in hand while hunting / driven / just shot, else on his back
        if (fig != null)
        {
            manualBowT -= dt;
            bool inHand = fig.hunt != null || fig.manual != null || manualBowT > 0f;
            if (fig.hunt == null) Bow(fig, inHand);
            if (figSpot != null) figSpot.pos = fig.transform.position;
        }
        // bins: live counts
        if (binVer != Stash50.ver) { binVer = Stash50.ver; foreach (var t in bins) if (t != null) t.text = Stash50.BinText; if (quiverVis != null) Quiver(); }
        // crafting
        if (crafter != null)
        {
            craftT += dt;
            if (Random.value < dt * 6f) { FX.Sparkle(BenchPos + Vector3.up * 1.05f, new Color(1f, 0.85f, 0.5f), 3); Sfx.PlayAt(Sfx.Clank, BenchPos, 0.35f, 30f, 1.2f + Random.value * 0.4f); }
            if (craftT > 1.4f || crafter == null)
            {
                Frog f = crafter; crafter = null;
                Held h; if (f != null && held.TryGetValue(f, out h))
                {
                    if (craftTo == 2 && h.kind == 1)
                    {
                        int n = h.rock + h.ice + h.metal * 2;
                        Drop(f); Hold(f, 2, n);
                        f.Toast("Chipped " + n + " arrowheads! Now add sticks (A / E again)", 2.8f);
                        Debug.Log("FFU50 bench: " + f.nick + " crafted " + n + " arrowheads");
                    }
                    else if (craftTo == 3 && h.kind == 2)
                    {
                        int n = h.n;
                        Drop(f); Hold(f, 3, n);
                        f.Toast("Made " + n + " arrows (arrowhead + stick)! Hand them to Figure 03 or the quiver", 3f);
                        Debug.Log("FFU50 bench: " + f.nick + " made " + n + " arrows");
                    }
                    Sfx.PlayAt(Sfx.Pickup, BenchPos, 0.5f, 30f, 1.3f);
                }
            }
        }
        // cooking
        if (cookT >= 0f && !cookReady)
        {
            cookT += dt;
            if (spitMeat != null)
            {
                spitMeat.localRotation = Quaternion.Euler(cookT * 120f, 90f, 0f);
                var mr = spitMeat.Find("Meat"); if (mr != null) mr.GetComponent<Renderer>().material.color = Color.Lerp(new Color(0.86f, 0.52f, 0.46f), new Color(0.62f, 0.36f, 0.16f), Mathf.Clamp01(cookT / 5f));
                if (Random.value < dt * 5f) FX.Smoke(spitMeat.position + Vector3.up * 0.2f, 0.5f, new Color(0.9f, 0.9f, 0.9f, 0.4f));
            }
            if (cookT >= 5f)
            {
                cookReady = true;
                if (cookFor != null) cookFor.Toast("Drumstick cooked! A / E at the campfire to eat it", 3f);
                Sfx.PlayAt(Sfx.Pickup, Campfire.I != null ? Campfire.I.Pos : Vector3.zero, 0.6f, 30f, 1.5f);
                Debug.Log("FFU50 campfire: drumstick cooked for " + (cookFor != null ? cookFor.nick : "-"));
            }
        }
        // held items: drop on entering a vehicle / leaving the ranch
        if (held.Count > 0)
        {
            List<Frog> gone = null;
            foreach (var kv in held) if (kv.Key == null || kv.Key.world != WorldId.Ranch) { if (gone == null) gone = new List<Frog>(); gone.Add(kv.Key); }
            if (gone != null) foreach (var f in gone) { if (f != null) Drop(f); else held.Remove(f); }
            foreach (var kv in held) if (kv.Value.vis != null) kv.Value.vis.gameObject.SetActive(kv.Key.vehicle == null);
        }
        // cows coming back after a POOF
        for (int i = TipOver50.Respawning.Count - 1; i >= 0; i--)
        {
            var a = TipOver50.Respawning[i];
            if (a == null) { TipOver50.Respawning.RemoveAt(i); continue; }
            if (a.enabled) { Debug.Log("TIPOVER50: the cow popped back at " + a.Pos.ToString("0.0")); TipOver50.Respawning.RemoveAt(i); }
        }
    }

    static void Quiver()
    {
        for (int i = quiverVis.childCount - 1; i >= 0; i--) Destroy(quiverVis.GetChild(i).gameObject);
        int n = Mathf.Min(Stash50.arrows, 10);
        for (int i = 0; i < n; i++)
        {
            var a = ArrowVis(quiverVis);
            float ang = i * 2.4f;
            a.localPosition = new Vector3(Mathf.Cos(ang) * 0.12f, 0.1f, Mathf.Sin(ang) * 0.12f);
            a.localRotation = Quaternion.Euler(Mathf.Sin(ang) * 10f, 0f, Mathf.Cos(ang) * 10f);
        }
    }
}
