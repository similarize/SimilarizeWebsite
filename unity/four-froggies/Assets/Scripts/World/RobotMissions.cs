using System.Collections.Generic;
using UnityEngine;

// ffu20 robot space missions (Bill: "tell them to go to space and have a mission ... like collect Mars rocks and bring
// them back, and we should be able to watch their video feed").
// The robots fly their OWN ship from the ROBOT MISSION PAD (south-west of the house, beside the big Starship pad), so the
// froggies' Starship stays free. Mission = one robot at a time:
//   walk to the pad -> up the ramp -> countdown -> ascent over the ranch -> auto-transfer through the real space world
//   (Earth -> Mars / Callisto, live planet positions) -> powered landing beside the Mars / Callisto Starship -> walk out,
//   collect N visible samples into a sample case -> back aboard -> launch -> transfer home -> land on the pad -> carry the
//   case to the display tables and tip the samples onto the pile (counter, saved in PlayerPrefs).
// Runs in the background with the same robot / ship logic whether anyone watches or not; worlds are only rendered (kept
// active) while a player is there or the phone video feed (RobotFeed) is looking at them. Local per device (not synced).
public class MissionShip : MonoBehaviour
{
    public Transform plume, ramp, vis;
    Light glow;
    AudioSource roar;
    public float flame;
    public const float H = 21f;
    public static readonly Vector3 HatchLocal = new Vector3(0f, 3.5f, 1.95f);

    public static MissionShip Build()
    {
        var go = new GameObject("Robot mission ship");
        var s = go.AddComponent<MissionShip>();
        Transform t = go.transform;
        s.vis = Mats.Node(t, "Vis", Vector3.zero);
        Transform v = s.vis;
        Material steel = Mats.Paint(new Color(0.83f, 0.84f, 0.86f), 0.72f), weld = Mats.Paint(new Color(0.62f, 0.63f, 0.66f), 0.6f);
        Material tile = Mats.Lit(new Color(0.1f, 0.1f, 0.11f)), dark = Mats.Lit(new Color(0.16f, 0.16f, 0.18f));
        Material orange = Mats.Shiny(new Color(1f, 0.5f, 0.1f)), glass = Mats.Unlit(new Color(0.55f, 0.85f, 1f));
        const float R = 2.0f;
        Mats.Prim(PrimitiveType.Cylinder, v, new Vector3(0f, 10f, 0f), new Vector3(R * 2f, 7f, R * 2f), steel);
        for (int k = 0; k < 5; k++) Mats.Prim(PrimitiveType.Cylinder, v, new Vector3(0f, 4.4f + k * 2.8f, 0f), new Vector3(R * 2f + 0.03f, 0.03f, R * 2f + 0.03f), weld);
        Mats.Prim(PrimitiveType.Cylinder, v, new Vector3(-0.35f, 10f, 0f), new Vector3(R * 2f - 0.5f, 6.95f, R * 2f - 0.02f), tile);   // heat-shield side
        Mats.Prim(PrimitiveType.Sphere, v, new Vector3(0f, 17f, 0f), new Vector3(R * 2f, 6.4f, R * 2f), steel);
        Mats.Prim(PrimitiveType.Sphere, v, new Vector3(0f, 19.2f, 0f), new Vector3(R * 1.25f, 3.6f, R * 1.25f), steel);
        for (int sd = -1; sd <= 1; sd += 2)
        {
            Mats.Prim(PrimitiveType.Cube, v, new Vector3(sd * (R + 0.45f), 15.6f, 0f), new Vector3(0.9f, 2.2f, 0.12f), dark);   // forward flaps
            Mats.Prim(PrimitiveType.Cube, v, new Vector3(sd * (R + 0.6f), 5.2f, 0f), new Vector3(1.3f, 3.2f, 0.14f), dark);     // aft flaps
        }
        for (int k = 0; k < 4; k++) Mats.Prim(PrimitiveType.Cube, v, new Vector3(-0.6f + k * 0.4f, 16.2f, R - 0.02f), new Vector3(0.26f, 0.22f, 0.06f), glass);   // windows
        // four landing legs + pads
        for (int k = 0; k < 4; k++)
        {
            Quaternion q = Quaternion.Euler(0f, 45f + k * 90f, 0f);
            var leg = Mats.Prim(PrimitiveType.Cube, v, q * new Vector3(0f, 1.7f, R + 0.7f), new Vector3(0.22f, 3.6f, 0.22f), dark);
            leg.transform.localRotation = q * Quaternion.Euler(-22f, 0f, 0f);
            Mats.Prim(PrimitiveType.Cylinder, v, q * new Vector3(0f, 0.08f, R + 1.35f), new Vector3(0.9f, 0.08f, 0.9f), dark);
        }
        // engines in the skirt
        Mats.Prim(PrimitiveType.Cylinder, v, new Vector3(0f, 3.0f, 0f), new Vector3(R * 2f - 0.1f, 0.05f, R * 2f - 0.1f), tile);
        Material bell = Mats.Steel(new Color(0.42f, 0.38f, 0.34f));
        for (int k = 0; k < 3; k++) Mats.Prim(PrimitiveType.Cylinder, v, Quaternion.Euler(0f, k * 120f, 0f) * new Vector3(0f, 2.6f, 0.75f), new Vector3(0.8f, 0.4f, 0.8f), bell);
        // hatch + ramp (hinged at the hatch sill, lowered to the ground when on a surface)
        Mats.Prim(PrimitiveType.Cube, v, new Vector3(0f, 4.4f, R - 0.02f), new Vector3(1.3f, 2.0f, 0.08f), Mats.Unlit(new Color(0.95f, 0.8f, 0.45f)));
        s.ramp = Mats.Node(v, "Ramp", new Vector3(0f, 3.45f, R));
        Mats.Prim(PrimitiveType.Cube, s.ramp, new Vector3(0f, 0f, 2.3f), new Vector3(1.3f, 0.08f, 4.6f), dark);
        Mats.Prim(PrimitiveType.Cube, s.ramp, new Vector3(0f, 0.06f, 2.3f), new Vector3(1.1f, 0.03f, 4.4f), orange);
        // "ROBOTS" band so it never reads as the froggies' Starship
        Mats.Prim(PrimitiveType.Cylinder, v, new Vector3(0f, 12.6f, 0f), new Vector3(R * 2f + 0.04f, 0.35f, R * 2f + 0.04f), orange);
        // plume (pivot at the bells, stretches downward)
        s.plume = Mats.Node(t, "Plume", new Vector3(0f, 2.4f, 0f));
        var outer = Mats.Prim(PrimitiveType.Sphere, s.plume, new Vector3(0f, -4.5f, 0f), new Vector3(3.0f, 9f, 3.0f), Mats.Unlit(new Color(1f, 0.55f, 0.15f)));
        var inner = Mats.Prim(PrimitiveType.Sphere, s.plume, new Vector3(0f, -2.8f, 0f), new Vector3(1.6f, 5.6f, 1.6f), Mats.Unlit(new Color(1f, 0.95f, 0.75f)));
        Mats.NoShadows(outer); Mats.NoShadows(inner);
        s.plume.gameObject.SetActive(false);
        var gl = new GameObject("Glow"); gl.transform.SetParent(t, false); gl.transform.localPosition = new Vector3(0f, -2f, 0f);
        s.glow = gl.AddComponent<Light>(); s.glow.type = LightType.Point; s.glow.range = 30f; s.glow.color = new Color(1f, 0.7f, 0.4f); s.glow.shadows = LightShadows.None; s.glow.enabled = false;
        s.roar = Sfx.Loop(go, Sfx.EngRocket != null ? Sfx.EngRocket : Sfx.Rotor);
        if (Look.Mobile) Mats.NoShadows(go);
        return s;
    }

    public void SetRamp(float k) { if (ramp != null) ramp.localRotation = Quaternion.Euler(Mathf.Lerp(-90f, 48f, k), 0f, 0f); }
    public Vector3 RampFoot { get { return transform.TransformPoint(new Vector3(0f, 0f, 2.0f + 4.6f * 0.67f)); } }
    public Vector3 Hatch { get { return transform.TransformPoint(HatchLocal); } }

    public void Tick(float dt, bool heardBoost)
    {
        bool on = flame > 0.02f;
        if (plume.gameObject.activeSelf != on) plume.gameObject.SetActive(on);
        if (on) plume.localScale = new Vector3(0.7f + flame * 0.4f, flame * (0.85f + Random.value * 0.3f), 0.7f + flame * 0.4f);
        glow.enabled = on && !Look.Mobile; glow.intensity = flame * 3f;
        if (roar != null)
        {
            float k = Mathf.Clamp01(1f - Sfx.Near(transform.position) / 500f);
            float vol = on ? Mathf.Max(k * k * 0.8f, heardBoost ? 0.25f : 0f) * Mathf.Clamp01(flame + 0.2f) : 0f;
            roar.volume = Mathf.MoveTowards(roar.volume, vol, dt * 1.5f);
            roar.pitch = 0.7f + flame * 0.3f;
            if (roar.volume > 0.004f) { if (!roar.isPlaying) roar.Play(); } else if (roar.isPlaying) roar.Stop();
        }
    }
}

public static class MissionSite
{
    public static readonly Vector2 PadC = new Vector2(-80f, -60f);
    public static float PadTop;
    public static MissionShip ship;
    static Transform root;
    static readonly Transform[] pileRoot = new Transform[2];
    static readonly TextMesh[] counter = new TextMesh[2];
    public static readonly int[] count = new int[2];
    static readonly List<Transform>[] pile = { new List<Transform>(), new List<Transform>() };
    public static Vector3 ShipBase { get { return new Vector3(PadC.x, PadTop, PadC.y); } }
    public static Vector3 TableFront(int kind) { Vector3 c = TableC(kind); return RobotNav.G(c.x, c.z + 1.6f); }
    static Vector3 TableC(int kind) { float x = PadC.x + (kind == 0 ? -2.8f : 2.8f), z = PadC.y + 12f; return new Vector3(x, Ranch.GY(x, z), z); }
    public static bool Reserved(float x, float z) { return Mathf.Abs(x - PadC.x) < 14f && Mathf.Abs(z - PadC.y) < 18f; }
    public static bool OnSlab(float x, float z) { return root != null && Mathf.Abs(x - PadC.x) < 7f && Mathf.Abs(z - PadC.y) < 7f; }

    public static void Build()
    {
        root = new GameObject("Robot mission pad").transform;
        float top = -99f;
        for (int i = -1; i <= 1; i++) for (int j = -1; j <= 1; j++) top = Mathf.Max(top, Ranch.GY(PadC.x + i * 7f, PadC.y + j * 7f));
        PadTop = top + 0.25f;
        float lo = Mathf.Min(Ranch.GY(PadC.x - 7f, PadC.y - 7f), Ranch.GY(PadC.x + 7f, PadC.y + 7f)) - 1.5f;
        Material conc = Mats.Lit(new Color(0.55f, 0.55f, 0.57f));
        Mats.Prim(PrimitiveType.Cube, root, new Vector3(PadC.x, (PadTop + lo) * 0.5f, PadC.y), new Vector3(14f, PadTop - lo, 14f), conc, true);
        Mats.Prim(PrimitiveType.Cylinder, root, new Vector3(PadC.x, PadTop + 0.01f, PadC.y), new Vector3(9f, 0.01f, 9f), Mats.Unlit(new Color(1f, 0.78f, 0.15f)));
        Mats.Prim(PrimitiveType.Cylinder, root, new Vector3(PadC.x, PadTop + 0.015f, PadC.y), new Vector3(8.3f, 0.01f, 8.3f), conc);
        Ranch.Sign(new Vector3(PadC.x + 9.5f, Ranch.GY(PadC.x + 9.5f, PadC.y + 9f) + 2.6f, PadC.y + 9f), 0f, "ROBOT MISSION PAD\n<size=24>phone > MISSION tab</size>", new Color(0.35f, 0.15f, 0.05f), 7f, 2.2f);
        // sample display tables (Mars rocks left, Callisto ice right) + live counters
        Material wood = Mats.Lit(new Color(0.42f, 0.28f, 0.16f));
        for (int k = 0; k < 2; k++)
        {
            Vector3 c = TableC(k);
            Mats.Prim(PrimitiveType.Cube, root, c + Vector3.up * 0.85f, new Vector3(2.2f, 0.1f, 1.4f), wood, true);
            for (int a = -1; a <= 1; a += 2) for (int b = -1; b <= 1; b += 2) Mats.Prim(PrimitiveType.Cube, root, c + new Vector3(a * 1f, 0.42f, b * 0.6f), new Vector3(0.1f, 0.84f, 0.1f), wood);
            Mats.Prim(PrimitiveType.Cube, root, c + Vector3.up * 0.93f, new Vector3(2.0f, 0.06f, 1.2f), Mats.Lit(k == 0 ? new Color(0.25f, 0.12f, 0.08f) : new Color(0.12f, 0.2f, 0.28f)));
            pileRoot[k] = Mats.Node(root, k == 0 ? "MarsPile" : "IcePile", c + Vector3.up * 0.96f);
            var tg = new GameObject("Counter");
            tg.transform.SetParent(root, false);
            tg.transform.position = c + Vector3.up * 2.6f;
            var tm = tg.AddComponent<TextMesh>();
            UIK.WorldText(tm, 64, 0.05f);
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.richText = true;
            tm.color = k == 0 ? new Color(1f, 0.62f, 0.42f) : new Color(0.6f, 0.92f, 1f);
            tg.AddComponent<Billboard>();
            counter[k] = tm;
            count[k] = PlayerPrefs.GetInt(k == 0 ? "ff.marsRocks" : "ff.callistoIce", 0);
            for (int i = 0; i < Mathf.Min(count[k], 60); i++) AddToPile(k, Sample(k, null, i), false);
            UpdateCounter(k);
        }
        Mats.NoShadows(root.gameObject);
        ship = MissionShip.Build();
        ParkOnPad();
    }

    public static void ParkOnPad()
    {
        ship.transform.position = ShipBase;
        ship.transform.rotation = Quaternion.Euler(0f, 0f, 0f);   // ramp faces +z (north, towards the house / robots)
        ship.flame = 0f; ship.SetRamp(1f);
        if (ship.plume != null) ship.plume.gameObject.SetActive(false);
    }

    static void UpdateCounter(int k)
    {
        counter[k].text = (k == 0 ? "MARS ROCKS" : "CALLISTO ICE") + "\n<size=192>" + count[k] + "</size>";
    }

    // a sample: Mars rock (lumpy dark-red stone) or Callisto ice (glassy cyan crystal)
    public static Transform Sample(int kind, Transform parent, int seed)
    {
        var g = new GameObject(kind == 0 ? "Mars rock" : "Callisto ice").transform;
        if (parent != null) g.SetParent(parent, false);
        var r = new System.Random(seed * 31 + kind * 7);
        float f() { return (float)r.NextDouble(); }
        if (kind == 0)
        {
            Color c = Color.Lerp(new Color(0.48f, 0.2f, 0.12f), new Color(0.3f, 0.14f, 0.1f), f());
            Material m = Mats.Lit(c);
            for (int i = 0; i < 3; i++)
            {
                var p = Mats.Prim(PrimitiveType.Sphere, g, new Vector3(f() - 0.5f, f() * 0.5f, f() - 0.5f) * 0.16f, new Vector3(0.22f + f() * 0.12f, 0.15f + f() * 0.08f, 0.2f + f() * 0.1f), m);
                p.transform.localRotation = Quaternion.Euler(f() * 40f, f() * 360f, f() * 40f);
            }
        }
        else
        {
            Material m = Mats.GlassTint(new Color(0.62f, 0.9f, 1f, 0.7f)), core = Mats.Unlit(new Color(0.75f, 0.95f, 1f));
            for (int i = 0; i < 3; i++)
            {
                var p = Mats.Prim(PrimitiveType.Cube, g, new Vector3(f() - 0.5f, 0.08f + f() * 0.06f, f() - 0.5f) * 0.18f, new Vector3(0.09f, 0.26f + f() * 0.12f, 0.09f), m);
                p.transform.localRotation = Quaternion.Euler(f() * 50f - 25f, f() * 360f, f() * 50f - 25f);
            }
            Mats.Prim(PrimitiveType.Sphere, g, new Vector3(0f, 0.06f, 0f), Vector3.one * 0.07f, core);
        }
        Mats.NoShadows(g.gameObject);
        return g;
    }

    public static void AddToPile(int k, Transform s, bool fx)
    {
        int i = pile[k].Count;
        s.SetParent(pileRoot[k], false);
        float a = i * 2.39996f, rad = Mathf.Min(0.85f, 0.11f * Mathf.Sqrt(i + 0.5f) * 1.6f);
        float y = Mathf.Max(0f, 0.32f - rad * 0.3f) * Mathf.Clamp01(i / 8f) + (i >= 40 ? 0.12f : 0f);
        s.localPosition = new Vector3(Mathf.Cos(a) * rad, y, Mathf.Sin(a) * rad * 0.65f);
        s.localRotation = Quaternion.Euler(0f, i * 47f, 0f);
        s.localScale = Vector3.one;
        pile[k].Add(s);
        if (fx) FX.Sparkle(s.position + Vector3.up * 0.2f, k == 0 ? new Color(1f, 0.6f, 0.3f) : new Color(0.6f, 0.95f, 1f), 6);
    }

    public static void Deposit(int k)
    {
        count[k]++;
        PlayerPrefs.SetInt(k == 0 ? "ff.marsRocks" : "ff.callistoIce", count[k]);
        UpdateCounter(k);
    }
    public static int PileCount(int k) { return pile[k].Count; }
    public static void Trim(int k) { while (pile[k].Count > 60) { Object.Destroy(pile[k][0].gameObject); pile[k].RemoveAt(0); } }
}

public class RobotMission
{
    public static RobotMission Active;
    public readonly Robot r;
    public readonly int kind;           // 0 Mars rocks, 1 Callisto ice
    public readonly int want;
    public int got;
    public int phase;
    float t, pickT;
    int pickIdx = -1;
    Transform crate;
    readonly List<Transform> samples = new List<Transform>();
    readonly List<Transform> carried = new List<Transform>();
    Vector3 site;                       // landing spot on Mars / Callisto
    float distLine;
    bool aborted;
    MissionShip S { get { return MissionSite.ship; } }
    public string Name { get { return kind == 0 ? "MARS ROCK RUN" : "CALLISTO ICE RUN"; } }
    public string Dest { get { return kind == 0 ? "Mars" : "Callisto"; } }
    WorldId DestWorld { get { return kind == 0 ? WorldId.Mars : WorldId.Callisto; } }
    public string Unit { get { return kind == 0 ? "rocks" : "ice chunks"; } }

    const int WalkToShip = 0, Board = 1, Count = 2, Ascent = 3, TransitOut = 4, Descent = 5, Exit = 6, Collect = 7, BackToShip = 8,
              Board2 = 9, Count2 = 10, Ascent2 = 11, TransitBack = 12, Descent2 = 13, Exit2 = 14, Deliver = 15;
    const float TransitT = 22f, AscentT = 12f, Ascent2T = 8f, DescentT = 9f, Descent2T = 10f;

    RobotMission(Robot robot, int k, int n) { r = robot; kind = k; want = n; }

    public static string Start(Robot r, int kind, int n)
    {
        if (MissionSite.ship == null || SpaceWorld.I == null || SurfaceWorlds.I == null) return "No mission ship here";
        if (Active != null) return "The mission ship is busy (" + Active.r.robotName + " - " + Active.PhaseLine + ")";
        if (r.battery < 0.35f) return r.robotName + " needs a charge first (" + r.Pct + ")";
        if (r.manual != null) r.ReleaseManual(false);
        if (r.drv != null) r.EndDrive(null);
        r.ResetForMission();
        var m = new RobotMission(r, kind, n);
        Active = m; r.mission = m; r.cmd = "mission";
        Sfx.Play(Sfx.Confirm != null ? Sfx.Confirm : Sfx.Pickup, 0.6f);
        return r.robotName + ": " + m.Name + " - bringing back " + n + " " + m.Unit + "!";
    }

    public string Abort()
    {
        if (phase <= Count) { End(true); return r.robotName + ": mission scrubbed"; }
        if (!aborted) { aborted = true; return r.robotName + ": cutting it short - heading home with " + got + " " + Unit; }
        return r.robotName + ": already heading home";
    }

    void Go(int ph) { phase = ph; t = 0f; r.ExtReset(); }

    static void ToastRanch(string s)
    {
        if (Game.I == null) return;
        foreach (Frog f in Game.I.frogs) if (f != null && f.human) f.Toast(s, 3.5f);
    }

    public string PhaseLine
    {
        get
        {
            switch (phase)
            {
                case WalkToShip: return "walking to the mission ship";
                case Board: return "boarding";
                case Count: return "countdown T-" + Mathf.CeilToInt(3f - t);
                case Ascent: return "launching";
                case TransitOut: return "in transit to " + Dest + " " + Mathf.RoundToInt(Mathf.Clamp01(t / TransitT) * 100f) + "%";
                case Descent: return "landing on " + Dest;
                case Exit: return "stepping out on " + Dest;
                case Collect: return "collecting " + got + "/" + want;
                case BackToShip: return "back to the ship (" + got + "/" + want + ")";
                case Board2: case Count2: return "boarding for home";
                case Ascent2: return "launching from " + Dest;
                case TransitBack: return "returning to Earth " + Mathf.RoundToInt(Mathf.Clamp01(t / TransitT) * 100f) + "%";
                case Descent2: return "landing at the ranch";
                case Exit2: case Deliver: return "delivering " + got + " " + Unit;
            }
            return "done";
        }
    }
    public string DistLine { get { return distLine < 0f ? "" : distLine >= 1000f ? (distLine / 1000f).ToString("0.0") + " km" : Mathf.RoundToInt(distLine) + " m"; } }
    public string DistWhat
    {
        get
        {
            switch (phase)
            {
                case WalkToShip: case BackToShip: return "to ship";
                case Ascent: case Ascent2: case Descent: case Descent2: return "altitude";
                case TransitOut: return "to " + Dest;
                case TransitBack: return "to Earth";
                case Collect: return "to next " + (kind == 0 ? "rock" : "ice");
                case Deliver: return "to display";
            }
            return "";
        }
    }

    void HideIn()
    {
        r.frozen = true; r.SetHidden(true);
        r.transform.position = S.Hatch;
    }

    public void Tick(float dt)
    {
        t += dt;
        distLine = -1f;
        S.Tick(dt, RobotFeed.Watching(r));
        switch (phase)
        {
            case WalkToShip:
                {
                    r.world = WorldId.Ranch;
                    Vector3 foot = S.RampFoot;
                    distLine = RobotNav.Flat(foot - r.transform.position);
                    if (r.ExtWalk(foot, r.RunSpeed, 0.5f, dt)) Go(Board);
                    break;
                }
            case Board:
            case Board2:
                {
                    r.frozen = true;
                    float k = Mathf.Clamp01(t / 2f);
                    r.transform.position = Vector3.Lerp(S.RampFoot, S.Hatch, k);
                    r.ExtYaw(S.transform.eulerAngles.y + 180f);
                    r.ExtSpeed(1.4f);
                    if (k >= 1f)
                    {
                        HideIn();
                        Go(phase == Board ? Count : Count2);
                        Sfx.PlayAt(Sfx.Door, S.Hatch, 0.6f, 60f);
                    }
                    break;
                }
            case Count:
            case Count2:
                {
                    HideIn();
                    float cd = phase == Count ? 3f : 2f;
                    S.SetRamp(1f - Mathf.Clamp01(t / 1.2f));
                    if (Random.value < 0.4f) FX.Smoke(S.transform.position + Random.insideUnitSphere * 3f, 2.2f, new Color(0.92f, 0.92f, 0.95f, 0.55f));
                    S.flame = t > cd - 0.6f ? 0.4f : 0f;
                    if (t >= cd)
                    {
                        Go(phase == Count ? Ascent : Ascent2);
                        Sfx.PlayAt(Sfx.Boom, S.transform.position, 1f, 600f, 0.55f);
                        if (phase == Ascent) ToastRanch(r.robotName + " launched on the " + Name.ToLower() + "!");
                    }
                    break;
                }
            case Ascent:
            case Ascent2:
                {
                    HideIn();
                    float T = phase == Ascent ? AscentT : Ascent2T;
                    Vector3 b = phase == Ascent ? MissionSite.ShipBase : site;
                    float h = 3.2f * t * t + 1.5f * t;
                    S.transform.position = b + Vector3.up * h + new Vector3(0.012f, 0f, 0.006f) * h * h / 40f;
                    S.transform.rotation = Quaternion.Euler(Mathf.Clamp(h / 60f, 0f, 12f), 0f, 0f);
                    S.flame = 1f;
                    distLine = h;
                    if (h < 40f && Random.value < 0.7f) FX.Smoke(b + new Vector3(Random.Range(-6f, 6f), 0.5f, Random.Range(-6f, 6f)), 3.5f, new Color(0.85f, 0.83f, 0.8f, 0.6f));
                    if (Random.value < 0.25f) Game.Shake(S.transform.position, 0.08f);
                    if (t >= T)
                    {
                        SpaceWorld.I.EnsureBuilt();
                        r.world = WorldId.Space;
                        Go(phase == Ascent ? TransitOut : TransitBack);
                    }
                    break;
                }
            case TransitOut:
            case TransitBack:
                {
                    HideIn();
                    var W = SpaceWorld.I;
                    int e = W.Find("earth"), d = W.Find(kind == 0 ? "mars" : "callisto");
                    int from = phase == TransitOut ? e : d, to = phase == TransitOut ? d : e;
                    Vector3 A = W.PosAt(from, W.simT), B = W.PosAt(to, W.simT);
                    Vector3 dir = (B - A); dir.y = 0f; float span = dir.magnitude; dir /= Mathf.Max(1f, span);
                    Vector3 side = Vector3.Cross(Vector3.up, dir);
                    Vector3 p0 = A + dir * W.bodies[from].cap, p3 = B - dir * W.bodies[to].cap;
                    Vector3 p1 = p0 + dir * span * 0.3f + side * span * 0.22f, p2 = p3 - dir * span * 0.3f + side * span * 0.12f;
                    float u = Mathf.Clamp01(t / TransitT), s = u * u * (3f - 2f * u);
                    Vector3 pos = Bez(p0, p1, p2, p3, s), ahead = Bez(p0, p1, p2, p3, Mathf.Min(1f, s + 0.01f)) - pos;
                    S.transform.position = pos;
                    if (ahead.sqrMagnitude > 0.0001f) S.transform.rotation = Quaternion.LookRotation(ahead.normalized) * Quaternion.Euler(90f, 0f, 0f);
                    S.flame = u < 0.12f || u > 0.88f ? 1f : 0.15f;
                    distLine = (p3 - pos).magnitude * 10f;   // 10 m per space unit (SpaceWorld.K) -> km read like the 3D version
                    if (u >= 1f)
                    {
                        if (phase == TransitOut)
                        {
                            if (kind == 0) SurfaceWorlds.I.EnsureMars(); else SurfaceWorlds.I.EnsureCallisto();
                            site = kind == 0 ? Worlds.MarsO + new Vector3(-13f, 0f, -38f) : Worlds.CallistoO + new Vector3(-9f, 0f, 8f);
                            site.y = GroundAt(site.x, site.z);
                            r.world = DestWorld;
                            Go(Descent);
                        }
                        else
                        {
                            r.world = WorldId.Ranch;
                            Go(Descent2);
                        }
                    }
                    break;
                }
            case Descent:
            case Descent2:
                {
                    HideIn();
                    float T = phase == Descent ? DescentT : Descent2T, H0 = phase == Descent ? 160f : 420f;
                    Vector3 b = phase == Descent ? site : MissionSite.ShipBase;
                    float u = Mathf.Clamp01(t / T), k = 1f - u;
                    float h = H0 * k * k * k;
                    S.transform.position = b + Vector3.up * h;
                    S.transform.rotation = Quaternion.Euler(0f, phase == Descent ? 150f : 0f, 0f);
                    S.flame = h > 0.05f ? 0.55f + 0.45f * (1f - Mathf.Clamp01(h / 40f)) : 0f;
                    distLine = h;
                    if (h < 25f && Random.value < 0.6f) FX.Dust(b + new Vector3(Random.Range(-5f, 5f), 0.3f, Random.Range(-5f, 5f)), 2.5f);
                    if (u >= 1f)
                    {
                        S.flame = 0f;
                        Sfx.PlayAt(Sfx.Thud, b, 0.8f, 200f, 0.6f);
                        if (phase == Descent) { SpawnSamples(); Go(Exit); }
                        else Go(Exit2);
                    }
                    break;
                }
            case Exit:
            case Exit2:
                {
                    r.SetHidden(false);
                    S.SetRamp(Mathf.Clamp01(t / 1.2f));
                    float k = Mathf.Clamp01((t - 1.2f) / 1.8f);
                    r.frozen = true;
                    r.transform.position = Vector3.Lerp(S.Hatch, S.RampFoot, k);
                    r.ExtYaw(S.transform.eulerAngles.y);
                    r.ExtSpeed(k > 0f && k < 1f ? 1.4f : 0f);
                    if (k >= 1f)
                    {
                        r.frozen = false;
                        if (phase == Exit) { Go(aborted ? BackToShip : Collect); MakeCrate(); }
                        else Go(Deliver);
                    }
                    break;
                }
            case Collect:
                {
                    r.frozen = false;
                    if (aborted || got >= want) { Go(BackToShip); break; }
                    if (pickIdx < 0 || pickIdx >= samples.Count || samples[pickIdx] == null)
                    {
                        pickIdx = -1; float bd = 1e9f;
                        for (int i = 0; i < samples.Count; i++) if (samples[i] != null) { float dd = RobotNav.Flat(samples[i].position - r.transform.position); if (dd < bd) { bd = dd; pickIdx = i; } }
                        if (pickIdx < 0) { Go(BackToShip); break; }
                        pickT = 0f;
                    }
                    Transform smp = samples[pickIdx];
                    distLine = RobotNav.Flat(smp.position - r.transform.position);
                    if (pickT <= 0f)
                    {
                        Vector3 to = smp.position - r.transform.position; to.y = 0f;
                        Vector3 stand = smp.position - to.normalized * 0.75f;
                        r.ExtPose(2);
                        if (r.ExtWalk(stand, r.RunSpeed * 0.8f, 0.3f, dt) || distLine < 0.9f) pickT = 0.001f;
                    }
                    else
                    {
                        pickT += dt;
                        r.ExtPose(3);
                        r.ExtFace(Mathf.Atan2(smp.position.x - r.transform.position.x, smp.position.z - r.transform.position.z) * Mathf.Rad2Deg);
                        if (pickT > 0.6f && smp.parent != crate)
                        {
                            // into the sample case on the robot's chest
                            Transform beam = smp.Find("Beam");
                            if (beam != null) Object.Destroy(beam.gameObject);
                            smp.SetParent(crate, false);
                            int n = carried.Count;
                            smp.localPosition = new Vector3(((n % 3) - 1) * 0.17f, 0.12f + (n / 6) * 0.1f, ((n / 3) % 2 - 0.5f) * 0.16f);
                            smp.localScale = Vector3.one * 0.8f;
                            carried.Add(smp);
                            got++;
                            FX.Sparkle(smp.position, kind == 0 ? new Color(1f, 0.55f, 0.3f) : new Color(0.6f, 0.95f, 1f), 6);
                            Sfx.PlayAt(Sfx.Pickup, smp.position, 0.6f, 40f, 1.1f + got * 0.05f);
                        }
                        if (pickT > 1.3f) { samples[pickIdx] = null; pickIdx = -1; pickT = 0f; }
                    }
                    break;
                }
            case BackToShip:
                {
                    r.frozen = false;
                    r.ExtPose(2);
                    Vector3 foot = S.RampFoot;
                    distLine = RobotNav.Flat(foot - r.transform.position);
                    if (r.ExtWalk(foot, r.RunSpeed * 0.8f, 0.5f, dt))
                    {
                        foreach (var s in samples) if (s != null) Object.Destroy(s.gameObject);
                        samples.Clear();
                        Go(Board2);
                    }
                    break;
                }
            case Deliver:
                {
                    r.frozen = false;
                    r.world = WorldId.Ranch;
                    Vector3 tf = MissionSite.TableFront(kind);
                    distLine = RobotNav.Flat(tf - r.transform.position);
                    if (pickT <= 0f)
                    {
                        r.ExtPose(2);
                        if (r.ExtWalk(tf, r.RunSpeed * 0.8f, 0.3f, dt)) pickT = 0.001f;
                    }
                    else
                    {
                        pickT += dt;
                        r.ExtPose(3);
                        r.ExtFace(180f);
                        if (carried.Count > 0 && pickT > 0.45f)
                        {
                            pickT = 0.001f;
                            Transform s = carried[0]; carried.RemoveAt(0);
                            MissionSite.AddToPile(kind, s, true);
                            MissionSite.Deposit(kind);
                            MissionSite.Trim(kind);
                            Sfx.PlayAt(Sfx.Thud, s.position, 0.4f, 40f, 1.4f);
                        }
                        if (carried.Count == 0 && pickT > 0.6f)
                        {
                            ToastRanch(r.robotName + " is back from " + Dest + " with " + got + " " + Unit + "! (" + MissionSite.count[kind] + " on display)");
                            Sfx.Play(Sfx.Win, 0.5f);
                            End(false);
                        }
                    }
                    break;
                }
        }
    }

    float GroundAt(float x, float z)
    {
        if (kind == 0) return Worlds.MarsO.y + SurfaceWorlds.MarsY(x - Worlds.MarsO.x, z - Worlds.MarsO.z);
        return Worlds.CallistoO.y + SurfaceWorlds.CalY(x - Worlds.CallistoO.x, z - Worlds.CallistoO.z);
    }

    void SpawnSamples()
    {
        samples.Clear();
        int n = want + 2;
        for (int i = 0; i < n; i++)
        {
            float a = i * 2.4f + Random.Range(-0.4f, 0.4f) + 1.2f, d = Random.Range(9f, 24f);
            Vector3 p = site + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
            Vector3 l = p - (kind == 0 ? Worlds.MarsO : Worlds.CallistoO);
            if (kind == 0 && (l.z > 28f || new Vector2(l.x - 10f, l.z + 30f).magnitude < 6f || new Vector2(l.x, l.z + 30f).magnitude < 6f)) { p = site + new Vector3(-Mathf.Abs(Mathf.Cos(a)) * d, 0f, -Mathf.Abs(Mathf.Sin(a)) * d * 0.5f - 4f); }
            if (kind == 1 && new Vector2(l.x, l.z).magnitude < 6f) p += (p - site).normalized * 8f;
            p.y = GroundAt(p.x, p.z) + 0.02f;
            var s = MissionSite.Sample(kind, null, Random.Range(0, 9999));
            s.position = p;
            s.localScale = Vector3.one * 1.25f;
            // a faint marker beam so the samples read from the feed camera
            var bm = new Material(Mats.Fx); bm.color = kind == 0 ? new Color(1f, 0.55f, 0.2f, 0.28f) : new Color(0.5f, 0.9f, 1f, 0.3f);
            var beam = Mats.Prim(PrimitiveType.Cylinder, s, new Vector3(0f, 2.2f, 0f), new Vector3(0.12f, 2.2f, 0.12f), bm);
            beam.name = "Beam"; Mats.NoShadows(beam);
            samples.Add(s);
        }
    }

    void MakeCrate()
    {
        if (crate != null) return;
        Transform cn = r.CarryNode;
        crate = Mats.Node(cn, "SampleCase", new Vector3(0f, -0.08f, -0.05f));
        float k = Mathf.Clamp(r.height / 2.2f, 0.7f, 1.4f);
        crate.localScale = Vector3.one * k;
        Material shell = Mats.Shiny(new Color(0.92f, 0.93f, 0.95f)), band = Mats.Shiny(new Color(1f, 0.5f, 0.1f));
        Mats.Prim(PrimitiveType.Cube, crate, new Vector3(0f, 0f, 0f), new Vector3(0.62f, 0.06f, 0.44f), shell);
        Mats.Prim(PrimitiveType.Cube, crate, new Vector3(0f, 0.09f, 0.22f), new Vector3(0.62f, 0.18f, 0.03f), shell);
        Mats.Prim(PrimitiveType.Cube, crate, new Vector3(0f, 0.09f, -0.22f), new Vector3(0.62f, 0.18f, 0.03f), shell);
        Mats.Prim(PrimitiveType.Cube, crate, new Vector3(0.31f, 0.09f, 0f), new Vector3(0.03f, 0.18f, 0.44f), band);
        Mats.Prim(PrimitiveType.Cube, crate, new Vector3(-0.31f, 0.09f, 0f), new Vector3(0.03f, 0.18f, 0.44f), band);
        Mats.NoShadows(crate.gameObject);
    }

    void End(bool scrubbed)
    {
        if (crate != null) { foreach (var c in carried) if (c != null) Object.Destroy(c.gameObject); Object.Destroy(crate.gameObject); }
        foreach (var s in samples) if (s != null) Object.Destroy(s.gameObject);
        samples.Clear(); carried.Clear();
        MissionSite.ParkOnPad();
        r.frozen = false; r.SetHidden(false); r.world = WorldId.Ranch;
        if (scrubbed) r.transform.position = RobotNav.G(MissionSite.ship.RampFoot.x, MissionSite.ship.RampFoot.z);
        r.mission = null; r.cmd = "auto"; r.ExtReset();
        if (Active == this) Active = null;
    }

    // demo / screenshot mode: start a mission and jump straight to a phase ("collect" on the surface, "return" =
    // descending onto the ranch pad with a full sample case)
    public static void Demo(Robot r, int kind, int n, string at)
    {
        if (Active != null) Active.End(true);
        Start(r, kind, n);
        var m = r.mission;
        if (m == null) { Debug.Log("FFDEMO mission failed to start: battery " + r.Pct); r.battery = 1f; Start(r, kind, n); m = r.mission; if (m == null) return; }
        if (at == "collect")
        {
            if (kind == 0) SurfaceWorlds.I.EnsureMars(); else SurfaceWorlds.I.EnsureCallisto();
            SpaceWorld.I.EnsureBuilt();
            m.site = kind == 0 ? Worlds.MarsO + new Vector3(-13f, 0f, -38f) : Worlds.CallistoO + new Vector3(-9f, 0f, 8f);
            m.site.y = m.GroundAt(m.site.x, m.site.z);
            m.S.transform.position = m.site; m.S.transform.rotation = Quaternion.Euler(0f, 150f, 0f); m.S.SetRamp(1f); m.S.flame = 0f;
            r.world = m.DestWorld;
            m.SpawnSamples();
            m.MakeCrate();
            Vector3 f = m.S.RampFoot;
            r.transform.position = new Vector3(f.x, r.GroundAt(f.x, f.z), f.z);
            r.frozen = false; r.SetHidden(false);
            m.Go(Collect);
        }
        else if (at == "return")
        {
            m.MakeCrate();
            for (int i = 0; i < n; i++)
            {
                var smp = MissionSite.Sample(kind, m.crate, 100 + i);
                smp.localPosition = new Vector3(((i % 3) - 1) * 0.17f, 0.12f + (i / 6) * 0.1f, ((i / 3) % 2 - 0.5f) * 0.16f);
                smp.localScale = Vector3.one * 0.8f;
                m.carried.Add(smp);
            }
            m.got = n;
            r.world = WorldId.Ranch;
            m.HideIn();
            m.Go(Descent2);
        }
    }

    static Vector3 Bez(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
    {
        float u = 1f - t;
        return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
    }

    // ---------- the phone video feed ----------
    // drone cam = a chase camera that keeps the robot (or the ship) in frame; eye cam = the robot's head / the cockpit window
    public bool Cam(bool eye, out Vector3 pos, out Quaternion rot)
    {
        pos = Vector3.zero; rot = Quaternion.identity;
        Transform s = S.transform;
        bool inShip = phase >= Board && phase <= Descent || phase >= Board2 && phase <= Descent2;
        if (phase == Board || phase == Board2) inShip = false;
        if (!inShip) return false;
        if (eye)
        {
            pos = s.TransformPoint(new Vector3(0f, 16.2f, 2.3f));
            rot = Quaternion.LookRotation(s.up, -s.forward);
            if (phase == Descent || phase == Descent2 || phase == Count || phase == Count2) rot = Quaternion.LookRotation(s.forward * 0.7f - s.up, s.up);
            return true;
        }
        if (phase == TransitOut || phase == TransitBack)
        {
            Vector3 fw = s.up, rt = s.right;
            pos = s.position - fw * 26f + Vector3.up * 9f + rt * 10f;
            rot = Quaternion.LookRotation((s.position + fw * 60f) - pos, Vector3.up);
            return true;
        }
        // hull camera looking down the side of the ship (plume + ground falling away / rushing up)
        Vector3 side = s.right;
        pos = s.TransformPoint(new Vector3(3.6f, 11f, 1.2f));
        Vector3 look = -s.up * 1f + side * 0.22f;
        if (phase == Count || phase == Count2) look = -s.up + side * 0.45f;
        rot = Quaternion.LookRotation(look, s.forward);
        return true;
    }
}
