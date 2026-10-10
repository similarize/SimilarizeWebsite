using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// ffu24 STORY EPISODE 2 - "Catch Jimmy!" (Bill, Oct 2026). Picked from the lobby STORY picker (StoryPicker.cs).
// James, Bubbles and Rexy take the Starship to Mars with Jimmy - and Jimmy flies off with his new long-range jetpack.
// The chase: Callisto -> back to Mars -> Mercury -> Saturn's moon Enceladus -> three chambers deep under Mars (he gets away
// every time) -> the low point ("We'll never catch him...") -> hope (his jetpack is coughing, he's heading home) -> the
// final chase in Earth orbit, caught with the cargo net -> everyone lands home together, fireworks, credits.
// Same machinery as episode 1 (chapters + checkpoints in localStorage, cutscene runner, letterbox, typewriter dialogue,
// SKIP, objective card + markers, split-screen co-op). Jimmy is a story-only runaway (JimmyNpc); if a player picked Jimmy
// they play another free character for this episode, and an AI Jimmy seat is parked out of sight until the end.
// Each find is a playful chase: follow him on the radar / objective marker / his jet trail, get close to make him burn
// a boost (he hops 20-45 m away), and once his fuel is gone he can only hop - run into him to TAG him. He escapes anyway
// in the cutscene, and the next destination is revealed.
// URL: ?story=2&storych=N (tests); ?ffdemo=1&ffshot=ep2intro|ep2callisto|ep2mars|ep2mercury|ep2saturn|ep2chamber3|ep2orbit|ep2end.
public partial class Story
{
    public int episode = 1;
    public static int Episode = 1, PickMode = 0;        // set by the picker: 1 continue, 2 start over (0 = old behaviour)
    public const string SaveKey2 = "ff.story.catchjimmy";
    public const string Ep2Name = "Catch Jimmy!";
    public static readonly string[] ChTitle2 = { "", "Mars Day Out", "Callisto Hide-and-Seek", "Back to Mars", "Too Hot on Mercury", "Saturn's Icy Moon", "The Deep Chambers", "Catch Him Over Earth" };

    enum Pl { None, Mars, Callisto, Mercury, Enceladus, Deep, Orbit, Ranch }
    Pl place = Pl.None;
    int pickMode;                                        // this run's picker choice (copied from PickMode in Begin1)
    StoryEp2Set E { get { return StoryEp2Set.I; } }
    JimmyNpc jim;
    Frog e2Hidden;
    readonly Dictionary<int, int> e2Swap = new Dictionary<int, int>();

    // ---------------- start / stop ----------------
    void Begin2()
    {
        episode = 2;
        StoryEp2Set.Ensure();
        StoryEp2Set.Install(true);
        SurfaceWorlds.Quiet = true;
        Frog.AiGoalHook = E2AiGoal;
        if (jim == null) jim = JimmyNpc.Make();
        jim.gameObject.SetActive(false);
        E2Cast();
        int jump = DemoChapter2(demoShot);
        string jc = Param("storych"); int jn;
        if (jc != null && int.TryParse(jc, out jn)) jump = Mathf.Clamp(jn, 1, 7);
        Debug.Log("FFSTORY ep2 begin demo=" + demo + " shot=" + demoShot + " jump=" + jump + " mode=" + pickMode);
        if (jump > 0) { StartChapter(jump, DemoStep2(demoShot)); return; }
        int c, s;
        bool has = Load2(out c, out s) && c >= 1 && c <= 8 && !(c == 1 && s == 0);
        if (has && pickMode != 2) { StartChapter(c >= 8 ? 7 : c, 0); return; }
        StartChapter(1, 0);
    }

    void E2Stop()
    {
        StoryEp2Set.Install(false);
        SurfaceWorlds.Quiet = false;
        Frog.AiGoalHook = null;
        if (E != null) { E.HideAll(); E.ventsOn = false; }
        Vector3? jpos = jim != null && jim.gameObject.activeSelf && place == Pl.Ranch ? jim.transform.position : (Vector3?)null;
        if (jim != null) { jim.gameObject.SetActive(false); jim.transform.localScale = Vector3.one; }
        foreach (var kv in e2Swap) G.SetSeatChar(kv.Key, kv.Value, false);
        e2Swap.Clear();
        if (e2Hidden != null)
        {
            HideSeat(e2Hidden, false);
            if (jpos.HasValue) e2Hidden.SendTo(WorldId.Ranch, jpos.Value + Vector3.up * 0.3f, 180f);
            e2Hidden = null;
        }
        G.FixAiChars();
        foreach (var f in G.frogs)
            if (f != null && !f.netPuppet && (f.world == WorldId.Mars || f.world == WorldId.Callisto || f.world == WorldId.Space))
                f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f);
        fireworks = false;
        place = Pl.None;
        if (ui != null) { ui.Radar(false, Vector2.zero, ""); ui.CineHud(""); }
    }

    // Jimmy is the runaway: a player who picked Jimmy plays a free character this episode; the AI Jimmy seat is parked
    void E2Cast()
    {
        e2Swap.Clear(); e2Hidden = null;
        var used = new HashSet<int>();
        foreach (var s in G.slots) used.Add(G.frogs[s.frog].charId);
        foreach (var s in G.slots)
        {
            Frog f = G.frogs[s.frog];
            if (f.charId != 1) continue;
            int rep = -1;
            foreach (int c in new[] { 0, 2, 3, 4, 5, 6, 7, 8, 9 }) if (!used.Contains(c)) { rep = c; break; }
            if (rep < 0) continue;
            used.Add(rep); e2Swap[s.frog] = 1;
            G.SetSeatChar(s.frog, rep, false);
            f.Toast("Jimmy is the runaway in this story - you play " + Roster.Name(rep) + "!", 6f);
            Debug.Log("FFSTORY ep2: seat " + s.frog + " plays " + Roster.Name(rep) + " (Jimmy is the runaway)");
        }
        G.FixAiChars();
        foreach (var f in G.frogs) if (!f.human && f.charId == 1) { e2Hidden = f; HideSeat(f, true); break; }
    }
    void HideSeat(Frog f, bool hide)
    {
        if (hide)
        {
            if (f.vehicle != null) f.ExitVehicle();
            f.SendTo(WorldId.RealRoom, new Vector3(0f, -900f, 0f), 0f);
            f.netPuppet = true;
            if (f.model != null) f.model.gameObject.SetActive(false);
        }
        else
        {
            f.netPuppet = false;
            if (f.model != null) f.model.gameObject.SetActive(true);
            f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f);
        }
    }

    List<Frog> Team()
    {
        var l = new List<Frog>();
        for (int k = 0; k < 4; k++) { Frog f = Cast(k); if (!l.Contains(f)) l.Add(f); }
        return l;
    }

    // AI crew members follow the lead (instead of wandering towards ranch spots)
    Vector3? E2AiGoal(Frog f)
    {
        if (!Active || episode != 2 || f == null || f.human || f == e2Hidden || G.slots.Count == 0) return null;
        Frog l = Lead;
        if (l == null || l == f || l.world != f.world) return null;
        float a = f.id * 2.1f;
        Vector3 t = l.transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 3f;
        if (jState == 2 && jim != null && jim.gameObject.activeSelf && (jim.transform.position - l.transform.position).magnitude < 30f) t = jim.transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 4f;
        return t;
    }

    // ---------------- save ----------------
    void Save2()
    {
        if (demo) return;
        string v = "cj1|" + ch + "|" + step;
        try { FFStorySave(SaveKey2, v); Debug.Log("FFSTORY ep2 saved " + v); } catch (System.Exception e) { Debug.LogWarning("FFSTORY save failed: " + e.Message); }
    }
    bool Load2(out int c, out int s)
    {
        c = 1; s = 0;
        if (demo) return false;
        var a = ReadSave(SaveKey2).Split('|');
        if (a.Length < 3 || a[0] != "cj1") return false;
        return int.TryParse(a[1], out c) && int.TryParse(a[2], out s);
    }
    public static string ReadSave(string key)
    {
        try { return FFStoryLoad(key) ?? ""; } catch (System.Exception e) { Debug.LogWarning("FFSTORY load failed: " + e.Message); return ""; }
    }

    // ---------------- places ----------------
    static WorldId WorldOf(Pl p)
    {
        switch (p)
        {
            case Pl.Mars: case Pl.Deep: return WorldId.Mars;
            case Pl.Callisto: case Pl.Mercury: case Pl.Enceladus: return WorldId.Callisto;
            case Pl.Orbit: return WorldId.Space;
            default: return WorldId.Ranch;
        }
    }
    static float GroundY(Pl p, float x, float z)
    {
        switch (p)
        {
            case Pl.Mars: return Worlds.MarsO.y + SurfaceWorlds.MarsY(x - Worlds.MarsO.x, z - Worlds.MarsO.z);
            case Pl.Callisto: return Worlds.CallistoO.y + SurfaceWorlds.CalY(x - Worlds.CallistoO.x, z - Worlds.CallistoO.z);
            case Pl.Mercury: return StoryEp2Set.MercO.y + StoryEp2Set.MercY(x - StoryEp2Set.MercO.x, z - StoryEp2Set.MercO.z);
            case Pl.Enceladus: return StoryEp2Set.EncO.y + StoryEp2Set.EncY(x - StoryEp2Set.EncO.x, z - StoryEp2Set.EncO.z);
            case Pl.Deep: return StoryEp2Set.DeepO.y + StoryEp2Set.DeepY(x - StoryEp2Set.DeepO.x, z - StoryEp2Set.DeepO.z);
            case Pl.Ranch: return Ranch.GY(x, z);
        }
        return StoryEp2Set.OrbitV.y;
    }
    Vector3 OnGround(Vector3 p, float up = 0f) { p.y = GroundY(place, p.x, p.z) + up; return p; }
    static Vector3 PlaceO(Pl p)
    {
        switch (p)
        {
            case Pl.Mars: return Worlds.MarsO;
            case Pl.Callisto: return Worlds.CallistoO;
            case Pl.Mercury: return StoryEp2Set.MercO;
            case Pl.Enceladus: return StoryEp2Set.EncO;
            case Pl.Deep: return StoryEp2Set.DeepO;
            case Pl.Orbit: return StoryEp2Set.OrbitV;
        }
        return Vector3.zero;
    }
    Transform ShipOf(Pl p)
    {
        switch (p)
        {
            case Pl.Mars: return E.marsShip;
            case Pl.Callisto: return E.calShip;
            case Pl.Mercury: return E.mercShip;
            case Pl.Enceladus: return E.encShip;
            case Pl.Ranch: return E.ranchShip;
        }
        return null;
    }
    Vector3 Spawn(Pl p, int i, int level = 0)
    {
        Vector3 o = PlaceO(p);
        Vector3 l;
        switch (p)
        {
            case Pl.Mars: l = new Vector3(-9f + i * 2.2f, 0f, -31f); break;
            case Pl.Callisto: l = new Vector3(3f + i * 2.2f, 0f, -14f); break;
            case Pl.Deep:
                {
                    Vector4 c = StoryEp2Set.Chambers[level];
                    l = new Vector3(c.x - 2.2f + i * 2.2f, 0f, c.y - c.z + (level == 0 ? 7f : 3f)); break;
                }
            case Pl.Ranch: { Vector3 r = StoryEp2Set.RanchLanding + new Vector3(-4f + i * 2.4f, 0f, 12f); return OnGroundP(p, r, 0.4f); }
            default: l = new Vector3(-2f + i * 2.2f, 0f, -14f); break;
        }
        return OnGroundP(p, o + l, 0.4f);
    }
    static Vector3 OnGroundP(Pl p, Vector3 w, float up) { w.y = GroundY(p, w.x, w.z) + up; return w; }

    // move the crew (and the set pieces) to a place
    void E2Goto(Pl p, bool sendTeam = true, int level = 0)
    {
        place = p;
        E.HideAll();
        E.ventsOn = p == Pl.Enceladus;
        switch (p)
        {
            case Pl.Mercury: StoryEp2Set.Show(E.mercRoot, true); break;
            case Pl.Enceladus: StoryEp2Set.Show(E.encRoot, true); break;
            case Pl.Deep: StoryEp2Set.Show(E.deepRoot, true); break;
            case Pl.Orbit: StoryEp2Set.Show(E.orbitRoot, true); break;
        }
        StoryEp2Set.Show(ShipOf(p), true);
        if (SurfaceWorlds.I != null) { if (p == Pl.Mars || p == Pl.Deep) SurfaceWorlds.I.EnsureMars(); if (p == Pl.Callisto) SurfaceWorlds.I.EnsureCallisto(); }
        if (jim != null) { jim.transform.localScale = Vector3.one; jim.ShowTag(true); }
        if (!sendTeam) return;
        int i = 0;
        foreach (var f in Team())
        {
            if (f.vehicle != null) f.ExitVehicle();
            f.SendTo(WorldOf(p), Spawn(p, i, level), 0f);
            i++;
        }
        G.StoryFaceCam(0f);
        Debug.Log("FFSTORY ep2 goto " + p + " level " + level);
    }

    // cast poses for cutscenes (any place)
    void E2Pose(Frog f, Vector3 p, float yaw)
    {
        if (f.vehicle != null) f.ExitVehicle();
        if (f.remote != null) f.remote.ReleaseManual(false);
        p.y = GroundY(place, p.x, p.z) + 0.05f;
        poses[f] = new Vector4(p.x, p.y, p.z, yaw);
        f.Teleport(p);
    }
    void E2PoseLine(Vector3 c, float yaw, float spacing = 2.3f)
    {
        var t = Team();
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        for (int k = 0; k < t.Count; k++) E2Pose(t[k], c + q * Vector3.right * (k - (t.Count - 1) * 0.5f) * spacing - q * Vector3.forward * Mathf.Abs(k - 1) * 0.6f, yaw);
    }
    // the crew around a point, all facing it (tag scenes)
    void E2PoseRing(Vector3 c, float r, float baseYaw)
    {
        var t = Team();
        for (int k = 0; k < t.Count; k++)
        {
            float a = (baseYaw + (k - (t.Count - 1) * 0.5f) * 38f) * Mathf.Deg2Rad;
            Vector3 p = c + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * r;
            Vector3 d = c - p;
            E2Pose(t[k], p, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
        }
    }
    static float YawTo(Vector3 from, Vector3 to) { Vector3 d = to - from; return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }

    void JimAt(Vector3 p, float yaw)
    {
        jim.gameObject.SetActive(true);
        p = OnGround(p);
        jim.transform.position = p;
        jim.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        jim.jet = 0f; jim.sputter = 0f; jim.air = false; jim.speed = 0f;
        if (jim.trail != null) jim.trail.Clear();
    }

    // ---------------- chapters ----------------
    static Pl ChPlace(int c) { return c == 2 ? Pl.Callisto : c == 3 ? Pl.Mars : c == 4 ? Pl.Mercury : c == 5 ? Pl.Enceladus : c == 6 ? Pl.Deep : c == 7 ? Pl.Orbit : Pl.Mars; }

    void E2StartChapter(int c, int s)
    {
        ch = c; step = s; chTime = 0f;
        runner.Clear(); cine = false; modal = false; modalHidesObj = false; poses.Clear();
        foreach (var f in G.frogs) Slump(f, false);
        ui.dimK = 0f; ui.bigK = 0f; ui.titleK = 0f; ui.creditsK = 0f;
        SetStatus(""); ui.Radar(false, Vector2.zero, ""); ui.CineHud("");
        Sfx.Override = null; fireworks = false; jState = 0;
        Debug.Log("FFSTORY ep2 chapter " + c + " step " + s + " t=" + Time.realtimeSinceStartup.ToString("0.0"));
        if (s == 0) Save2();
        switch (c)
        {
            case 1:
                if (s == 0) Play(E2IntroCo());
                else { E2Goto(Pl.Mars); step = Mathf.Max(1, s); JimAt(Worlds.MarsO + new Vector3(-4f, 0f, -12f), 180f); }
                break;
            case 2: case 3: case 4: case 5:
                if (s == 0) Play(ArriveCo(c));
                else { E2Goto(ChPlace(c)); step = 1; StartChaseFor(c); }
                break;
            case 6:
                if (s == 0) Play(DeepArriveCo());
                else if (s >= 3) { E2Goto(Pl.Deep, true, 2); step = 3; StartChaseFor(6); }
                else { E2Goto(Pl.Deep, true, s == 2 ? 1 : 0); JimAt(StoryEp2Set.DeepC(2) + new Vector3(0f, 0f, 4f), 180f); }
                break;
            case 7:
                if (demoShot == "ep2end") Play(CaptureCo());
                else Play(OrbitIntroCo());
                break;
        }
        if (demo) DemoAfterStart2();
    }

    // ---------------- per frame ----------------
    float aiT, fwT2;
    void E2Update(float dt)
    {
        if (!runner.Busy)
        {
            switch (ch)
            {
                case 1: Ch1E2(dt); break;
                case 2: case 3: case 4: case 5: ChaseTick(dt); ChaseObj(); break;
                case 6: Ch6E2(dt); break;
            }
            E2Keep(dt);
        }
        if (jim != null && jim.gameObject.activeSelf && jState == 3) { jim.sputter = 0f; }
        E2Radar();
        if (demo) DemoTick2(dt);
        ui.Tick(cine, cine && skippable, SkipHint());
        ui.ObjectivesVisible(!cine && !modalHidesObj, 1f);
        if (fireworks)
        {
            fwT2 -= dt;
            if (fwT2 <= 0f) { fwT2 = Random.Range(0.35f, 0.8f); StoryEp2Set.Firework(StoryEp2Set.RanchLanding + new Vector3(Random.Range(-30f, 30f), Random.Range(28f, 48f), Random.Range(-10f, 25f))); }
        }
    }

    // keep the AI crew with the lead; bring lost humans back
    void E2Keep(float dt)
    {
        aiT -= dt;
        if (aiT > 0f || G.slots.Count == 0 || place == Pl.Orbit || place == Pl.None) return;
        aiT = 1f;
        Frog l = Lead;
        WorldId w = WorldOf(place);
        Vector3 o = PlaceO(place);
        int i = 0;
        foreach (var f in Team())
        {
            i++;
            if (f.human)
            {
                Vector3 p = f.transform.position;
                bool lost = f.world != w || p.y < GroundY(place, p.x, p.z) - 8f || (place != Pl.Deep && place != Pl.Ranch && (new Vector2(p.x - o.x, p.z - o.z)).magnitude > 120f);
                if (lost && f.vehicle == null) { f.SendTo(w, Spawn(place, i, place == Pl.Deep ? StoryEp2Set.DeepLevel(p) : 0), 0f); f.Toast("Back to the others!", 2f); }
                continue;
            }
            if (l == null || f == l) continue;
            if (f.world != l.world || (f.transform.position - l.transform.position).magnitude > 30f)
            {
                Vector3 p = l.transform.position + new Vector3(Mathf.Cos(i * 2f), 0f, Mathf.Sin(i * 2f)) * 3f;
                f.SendTo(l.world, OnGround(p, 0.4f), 0f);
            }
        }
    }

    // ---------------- chapter 1: Mars day out ----------------
    static Vector3 M(float x, float z) { return SurfaceWorlds.M(x, SurfaceWorlds.MarsY(x, z), z); }
    void Ch1E2(float dt)
    {
        if (step < 1) return;
        // Jimmy shows off: little jet hops by the rover
        if (jim.gameObject.activeSelf && jState == 0)
        {
            Vector3 b = OnGround(Worlds.MarsO + new Vector3(-4f, 0f, -12f));
            float h = Mathf.Repeat(chTime, 4f);
            float up = h < 1.2f ? Mathf.Sin(h / 1.2f * Mathf.PI) * 3.2f : 0f;
            jim.transform.position = b + Vector3.up * up;
            jim.jet = h < 0.9f ? 0.7f : 0f; jim.air = up > 0.05f;
            Frog l = Lead;
            if (l != null) jim.transform.rotation = Quaternion.Euler(0f, YawTo(jim.transform.position, l.transform.position), 0f);
        }
        if (step == 1)
        {
            SetObj("CHAPTER 1 · " + ChTitle2[1].ToUpper(), "Explore Mars! Hop over to the big crater", null);
            foreach (var f in Humans()) if (f.world == WorldId.Mars && Flat(f.transform.position - M(9f, -6f)) < 9f) { step = 2; ToastAll("What a crater! Hey... where's Jimmy?", 3f); break; }
        }
        else if (step == 2)
        {
            SetObj("CHAPTER 1 · " + ChTitle2[1].ToUpper(), "Jimmy is showing off by the Curiosity rover - go and see", null);
            foreach (var f in Humans()) if (f.world == WorldId.Mars && Flat(f.transform.position - jim.transform.position) < 9f) { Play(BoltCo()); break; }
        }
        SetStatus("");
    }

    // ---------------- the chase ----------------
    int dodgesLeft, dodgesAll; int jState;            // 0 on the ground, 1 boosting, 2 out of fuel (hopping), 3 caught (scene)
    float cAlert, cTired, cHopMin, cHopMax, cR, jT, jDur, jArc, tauntT;
    Vector3 cC, jFrom, jTo;
    static readonly string[] Taunts = { "Too slow!", "Nope!", "Catch me if you can!", "Wheee!", "Missed me!", "Over here!", "Ha ha!", "Can't catch me!", "Zoom!" };

    void StartChaseFor(int c)
    {
        Pl p = c == 6 ? Pl.Deep : ChPlace(c);
        Vector3 o = PlaceO(p);
        switch (c)
        {
            case 2: Chase(3, 9f, 2.6f, 18f, 40f, o, 56f); break;
            case 3: Chase(4, 10f, 2.8f, 20f, 44f, o + new Vector3(0f, 0f, -6f), 62f); break;
            case 4: Chase(5, 10.5f, 3.0f, 20f, 45f, o, 64f); break;
            case 5: Chase(6, 11f, 3.2f, 22f, 48f, o, 64f); break;
            default: Chase(4, 7.5f, 2.8f, 9f, 22f, StoryEp2Set.DeepC(2), 16.5f); break;
        }
    }
    void Chase(int dodges, float alert, float tired, float hmin, float hmax, Vector3 centre, float r)
    {
        dodgesLeft = dodgesAll = dodges; cAlert = alert; cTired = tired; cHopMin = hmin; cHopMax = hmax; cC = centre; cR = r;
        jState = 0; tauntT = 0f;
        Frog l = Lead;
        Vector3 from = l != null ? l.transform.position : centre;
        Vector3 s = jim.transform.position;
        if (!jim.gameObject.activeSelf || Flat(s - from) < 18f || Flat(s - centre) > r) { s = PickSpot(from, Mathf.Max(hmin, 22f), hmax + 10f); JimAt(s, YawTo(s, from)); }   // else keep him where the cutscene showed him
        Debug.Log("FFSTORY ep2 chase ch " + ch + " dodges " + dodges + " jimmy at " + (s - PlaceO(place)).ToString("0"));
    }

    bool SpotOk(Vector3 w)
    {
        Vector3 l = w - PlaceO(place);
        switch (place)
        {
            case Pl.Mars:
                if (Mathf.Abs(l.x) < 11f && l.z > 26f) return false;                  // the cave
                if (new Vector2(l.x, l.z + 30f).magnitude < 10f) return false;         // the old rocket
                if (new Vector2(l.x - 10f, l.z + 30f).magnitude < 7f) return false;    // dog pen
                if (new Vector2(l.x + 16f, l.z + 40f).magnitude < 9f) return false;    // our Starship
                break;
            case Pl.Callisto:
                if (new Vector2(l.x, l.z).magnitude < 9f) return false;
                if (new Vector2(l.x - 12f, l.z + 9f).magnitude < 9f) return false;
                break;
            case Pl.Mercury: case Pl.Enceladus:
                if (new Vector2(l.x + 6f, l.z + 26f).magnitude < 9f) return false;
                break;
            case Pl.Deep:
                if (new Vector2(l.x + 9f, l.z - 128f).magnitude < 4f) return false;    // crystal cluster
                break;
        }
        return true;
    }
    Vector3 PickSpot(Vector3 from, float hmin, float hmax)
    {
        Vector3 best = cC; float bs = -1f;
        var hs = Humans();
        for (int k = 0; k < 40; k++)
        {
            Vector2 r = Random.insideUnitCircle * cR;
            Vector3 p = OnGround(cC + new Vector3(r.x, 0f, r.y));
            float hop = Flat(p - from);
            if (hop < hmin || hop > hmax || !SpotOk(p)) continue;
            float near = 60f;
            foreach (var f in hs) near = Mathf.Min(near, Flat(f.transform.position - p));
            float sc = Mathf.Min(near, 40f) + Random.value * 6f;
            if (sc > bs) { bs = sc; best = p; }
        }
        if (bs < 0f) { Vector2 r = Random.insideUnitCircle * cR * 0.6f; best = OnGround(cC + new Vector3(r.x, 0f, r.y)); }
        return best;
    }

    Frog NearestHuman(Vector3 p, out float d, out float dy)
    {
        Frog best = null; d = 1e9f; dy = 0f;
        WorldId w = WorldOf(place);
        foreach (var f in Humans())
        {
            if (f.world != w) continue;
            Vector3 q = f.transform.position;
            float dd = Flat(q - p);
            if (dd < d) { d = dd; best = f; dy = q.y - p.y; }
        }
        return best;
    }

    bool InChase { get { return (ch >= 2 && ch <= 5 && step == 1) || (ch == 6 && step == 3); } }

    void ChaseTick(float dt)
    {
        if (!InChase || jim == null || !jim.gameObject.activeSelf) return;
        Transform jt = jim.transform;
        Vector3 jp = jt.position;
        float nd, dy;
        Frog near = NearestHuman(jp, out nd, out dy);
        tauntT -= dt;
        switch (jState)
        {
            case 0:
                {
                    jim.jet = 0f; jim.air = false; jim.speed = 0f; jim.sputter = 0f;
                    jt.position = OnGround(jp) + Vector3.up * Mathf.Abs(Mathf.Sin(Time.time * 3f)) * 0.25f;
                    if (near != null) jt.rotation = Quaternion.Slerp(jt.rotation, Quaternion.Euler(0f, YawTo(jp, near.transform.position), 0f), dt * 4f);
                    if (dodgesLeft > 0 && near != null && nd < cAlert) Launch(near);
                    else if (dodgesLeft <= 0) OutOfFuel();
                    else if (near != null && nd < cAlert * 2.2f && tauntT <= 0f) { tauntT = 6f; Say2Toast("Come on, catch me!"); }
                    break;
                }
            case 1:
                {
                    jT += dt;
                    float k = Mathf.Clamp01(jT / jDur);
                    Vector3 p = Vector3.Lerp(jFrom, jTo, k) + Vector3.up * jArc * 4f * k * (1f - k);
                    Vector3 v = p - jp;
                    jt.position = p;
                    Vector3 flat = new Vector3(v.x, 0f, v.z);
                    if (flat.sqrMagnitude > 1e-4f) jt.rotation = Quaternion.LookRotation(flat) * Quaternion.Euler(Mathf.Lerp(28f, -10f, k), 0f, 0f);
                    jim.jet = k < 0.75f ? 1f : 0.35f; jim.air = true; jim.speed = 3f;
                    if (k >= 1f)
                    {
                        jt.position = jTo; jt.rotation = Quaternion.Euler(0f, jt.eulerAngles.y, 0f);
                        FX.Dust(jTo, 0.8f);
                        if (dodgesLeft <= 0) OutOfFuel(); else jState = 0;
                    }
                    break;
                }
            case 2:
                {
                    // out of fuel: he hops away on foot (slower than any froggy), the jetpack coughing smoke
                    jim.jet = 0f; jim.sputter = 1f; jim.air = false;
                    Vector3 dir = near != null ? jp - near.transform.position : Vector3.forward;
                    dir.y = 0f; dir = dir.sqrMagnitude > 1e-3f ? dir.normalized : Vector3.forward;
                    Vector3 tc = cC - jp; tc.y = 0f;
                    float edge = Mathf.Clamp01((tc.magnitude - cR * 0.75f) / (cR * 0.25f));
                    dir = (dir * (1f - edge) + tc.normalized * edge * 1.4f + Quaternion.Euler(0f, 90f, 0f) * dir * Mathf.Sin(Time.time * 0.9f) * 0.5f).normalized;
                    float spd = near != null && nd < 14f ? cTired : cTired * 0.4f;
                    Vector3 np = jp + dir * spd * dt;
                    if (!SpotOk(np)) np = jp + Quaternion.Euler(0f, 90f, 0f) * dir * spd * dt;
                    np = OnGround(np);
                    float hop = Mathf.Abs(Mathf.Sin(Time.time * 6f)) * 0.35f;
                    jt.position = np + Vector3.up * hop;
                    jt.rotation = Quaternion.Slerp(jt.rotation, Quaternion.LookRotation(dir), dt * 5f);
                    jim.speed = spd;
                    if (tauntT <= 0f && near != null && nd < 10f) { tauntT = 4.5f; Say2Toast(Random.value < 0.5f ? "Uh oh... my jetpack!" : "No fair - I'm out of fuel!"); }
                    if (near != null && nd < 2.4f && Mathf.Abs(dy) < 3f) { Tagged(near); }
                    break;
                }
        }
    }

    void Launch(Frog from)
    {
        dodgesLeft--;
        jFrom = jim.transform.position;
        jTo = PickSpot(from.transform.position, cHopMin, cHopMax);
        float d = Flat(jTo - jFrom);
        if (Flat(jTo - jFrom) < cHopMin * 0.5f) jTo = PickSpot(jFrom, cHopMin * 0.6f, cHopMax * 1.4f);
        d = Flat(jTo - jFrom);
        jDur = 1.2f + d / 24f; jArc = 5f + d * 0.2f; jT = 0f; jState = 1;
        Say2Toast(Taunts[Random.Range(0, Taunts.Length)]);
        Sfx.PlayAt(Sfx.Missile != null ? Sfx.Missile : Sfx.Boom, jFrom, 0.75f, 90f, 1.35f);
        Sfx.Ribbiting(jFrom, 0.7f);
        FX.Dust(jFrom, 0.9f);
        Debug.Log("FFSTORY ep2 dodge, left " + dodgesLeft + " hop " + d.ToString("0") + " m");
    }
    void OutOfFuel()
    {
        jState = 2;
        Sfx.Play(Sfx.Bonk != null ? Sfx.Bonk : Sfx.Click, 0.7f, 0.7f);
        ToastAll("Jimmy's jetpack is OUT OF FUEL! Run into him to TAG him!", 4f);
        Debug.Log("FFSTORY ep2 out of fuel");
    }
    void Say2Toast(string s) { foreach (var f in Humans()) if (f.world == WorldOf(place)) f.Toast("JIMMY:  " + s, 2.2f); }

    void Tagged(Frog by)
    {
        jState = 3;
        Debug.Log("FFSTORY ep2 tagged by " + by.nick + " ch " + ch);
        if (ch == 6) Play(VentCo(by)); else Play(EscapeCo(ch, by));
    }

    string FuelBars()
    {
        var sb = new System.Text.StringBuilder("<color=#b6ff5a>");
        for (int i = 0; i < dodgesLeft; i++) sb.Append("|");
        sb.Append("</color><color=#ffffff40>");
        for (int i = dodgesLeft; i < dodgesAll; i++) sb.Append("|");
        sb.Append("</color>");
        return sb.ToString();
    }
    void ChaseObj()
    {
        if (!InChase) return;
        int c = ch;
        string k = "CHAPTER " + c + " · " + ChTitle2[c].ToUpper();
        if (jState == 2) { SetObj(k, "Jimmy's out of fuel - TAG HIM!", null); SetStatus("<b>OUT OF FUEL!</b>   run into Jimmy to tag him"); }
        else
        {
            SetObj(k, "Catch Jimmy! Get close to make him burn his jetpack fuel", null);
            SetStatus("<b>JIMMY'S FUEL</b>  <size=26>" + FuelBars() + "</size>   ·   boosts left " + dodgesLeft);
        }
    }

    // radar: Jimmy relative to P1's camera
    void E2Radar()
    {
        bool on = !cine && jim != null && jim.gameObject.activeSelf && G.slots.Count > 0 && place != Pl.Orbit && (InChase || ch == 1 || ch == 6);
        if (!on) { ui.Radar(false, Vector2.zero, ""); return; }
        Frog me = Lead;
        if (me == null || me.world != WorldOf(place)) { ui.Radar(false, Vector2.zero, ""); return; }
        Vector3 d = jim.transform.position - me.transform.position; d.y = 0f;
        Vector3 l = Quaternion.Euler(0f, -G.StoryCamYaw(), 0f) * d;
        float dist = d.magnitude, r = Mathf.Sqrt(Mathf.Clamp01(dist / 90f));
        Vector2 rel = new Vector2(l.x, l.z);
        rel = rel.sqrMagnitude > 1e-4f ? rel.normalized * r : Vector2.zero;
        ui.Radar(true, rel, "JIMMY  " + Mathf.RoundToInt(dist) + " m" + (jState == 2 ? "  ·  NO FUEL" : ""));
    }

    // ---------------- chapter 6: the deep chambers ----------------
    void Ch6E2(float dt)
    {
        string k = "CHAPTER 6 · " + ChTitle2[6].ToUpper();
        if (step == 1 || step == 2)
        {
            if (jim.gameObject.activeSelf && jState == 0) { Vector3 b = OnGround(StoryEp2Set.DeepC(2) + new Vector3(0f, 0f, 4f)); jim.transform.position = b + Vector3.up * Mathf.Abs(Mathf.Sin(Time.time * 3f)) * 0.25f; }
            int want = step;   // 1 -> reach level 2, 2 -> reach level 3
            SetObj(k, step == 1 ? "Follow Jimmy's jet trail down to Level 2" : "Down again - to Level 3, the Deep Den", null);
            SetStatus("<b>LEVEL</b>  " + step + " / 3");
            foreach (var f in Humans())
            {
                if (f.world != WorldId.Mars) continue;
                Vector3 l = f.transform.position - StoryEp2Set.DeepO;
                if (step == 1 && l.z > 44f && l.y < -10f) { step = 2; ToastAll("A MARS DOGGY:  Woof! He zoomed right past us - all the way down to the Deep Den!", 4.5f); Sfx.Play(Sfx.Bark != null ? Sfx.Bark : Sfx.Pickup, 0.7f, 1.3f); break; }
                if (step == 2 && l.z > 106f && l.y < -24f) { Play(DenFoundCo()); break; }
            }
            return;
        }
        if (step == 3) { ChaseTick(dt); ChaseObj(); }
    }

    // the objective marker target
    Vector3? E2Target(Frog f)
    {
        if (!Active || cine || f == null || place == Pl.Orbit) return null;
        if (f.world != WorldOf(place)) return null;
        if (InChase && jim != null && jim.gameObject.activeSelf) return jim.transform.position;
        if (ch == 1) return step == 1 ? M(9f, -6f) : step == 2 ? jim.transform.position : (Vector3?)null;
        if (ch == 6)
        {
            if (step == 1) return StoryEp2Set.DeepC(1, 0.5f);
            if (step == 2) return StoryEp2Set.DeepC(2, 0.5f);
        }
        return null;
    }

    // ================= cutscenes =================
    IEnumerator TravelCo(string dest, string line, string kick = null, string main = null)
    {
        CamWorld = WorldId.Space;
        StoryEp2Set.Show(E.travelRoot, true);
        E.ShowDest(dest);
        Transform ship = E.travelShip;
        Vector3 V = StoryEp2Set.TravelV;
        Vector3 a = V + new Vector3(0f, 0f, -260f), b = V + new Vector3(0f, 0f, 560f);
        Vector3 look = E.travelDest[dest].childCount > 0 ? E.travelDest[dest].GetChild(0).position : V + Vector3.forward * 1500f;
        ship.rotation = Quaternion.identity; ship.position = a;
        Transform tj = E.travelJim;
        tj.position = a + new Vector3(0f, 6f, 170f);
        var tr = tj.GetComponent<TrailRenderer>(); if (tr != null) tr.Clear();
        Track(() => ship.position + new Vector3(11f, 5f, -28f), () => Vector3.Lerp(ship.position, look, 0.22f), 50f);
        bool title = main != null;
        if (title) { ui.titleKick = kick ?? ""; ui.titleText = main; } else ui.bigText = line ?? "";
        yield return FadeTo(0f, 0.7f);
        yield return Anim(6.5f, k =>
        {
            ship.position = Vector3.Lerp(a, b, k);
            tj.position = Vector3.Lerp(a + new Vector3(0f, 6f, 170f), b + new Vector3(30f, 20f, 520f), k * k);
            StoryEp2Set.ShipFlame(ship, 0.85f);
            if (E.travelStars != null) E.travelStars.position = CamPos;
            float vis = k < 0.15f ? k / 0.15f : k > 0.82f ? (1f - k) / 0.18f : 1f;
            if (title) ui.titleK = vis; else ui.bigK = vis * 0.95f;
        });
        ui.titleK = 0f; ui.bigK = 0f;
        yield return FadeTo(1f, 0.6f);
        StoryEp2Set.ShipFlame(ship, 0f);
        StoryEp2Set.Show(E.travelRoot, false);
    }

    // the Starship comes down on its spot at a place (crew aboard, hidden)
    IEnumerator LandCo(Pl p)
    {
        Transform ship = ShipOf(p);
        CamWorld = WorldOf(p);
        if (ship == null) { yield return FadeTo(0f, 0.6f); yield break; }
        Vector3 home = ship.position;
        float gy = GroundY(p, home.x, home.z);
        Vector3 basePt = new Vector3(home.x, gy, home.z);
        var team = Team();
        foreach (var f in team) if (f.model != null) f.model.gameObject.SetActive(false);
        bool jimOn = jim.gameObject.activeSelf; jim.gameObject.SetActive(false);
        Vector3 camP = basePt + new Vector3(30f, 7f, 34f);
        if (p == Pl.Ranch) camP = basePt + new Vector3(26f, 6f, 38f);
        Track(() => camP, () => ship.position + Vector3.down * 4f, 52f);
        ship.position = home + Vector3.up * 80f;
        StoryEp2Set.ShipFlame(ship, 1f);
        yield return FadeTo(0f, 0.6f);
        var hum = Sfx.Loop(gameObject, Sfx.EngRocket != null ? Sfx.EngRocket : Sfx.Rotor);
        if (hum != null && !skipping) { hum.volume = 0.6f; hum.Play(); }
        yield return Anim(3.4f, k =>
        {
            float e = 1f - (1f - k) * (1f - k);
            ship.position = home + Vector3.up * 80f * (1f - e);
            StoryEp2Set.ShipFlame(ship, 1f - k * 0.4f);
            if (k > 0.65f && Random.value < 0.5f) FX.Dust(basePt + Random.insideUnitSphere * 7f, 1f);
            camShake = 0.05f * k;
        });
        if (hum != null) { hum.Stop(); Destroy(hum); }
        ship.position = home;
        StoryEp2Set.ShipFlame(ship, 0f);
        if (!skipping) { for (int i = 0; i < 8; i++) FX.Dust(basePt + Random.insideUnitSphere * 6f, 1f); Sfx.Play(Sfx.Thud != null ? Sfx.Thud : Sfx.Boom, 0.6f, 0.7f); }
        foreach (var f in team) if (f.model != null) f.model.gameObject.SetActive(true);
        jim.gameObject.SetActive(jimOn);
    }

    // ---------------- 1. prologue: Mars ----------------
    IEnumerator E2IntroCo()
    {
        ui.fadeK = 1f;
        yield return CineOn();
        Sfx.Override = "story";
        yield return TravelCo("mars", null, "FOUR FROGGIES PRESENT", "CATCH JIMMY!");
        E2Goto(Pl.Mars);
        JimAt(Worlds.MarsO + new Vector3(-4f, 0f, -27f), 200f);
        yield return LandCo(Pl.Mars);
        Vector3 c = OnGround(Worlds.MarsO + new Vector3(-8f, 0f, -30f));
        E2PoseLine(c, 20f);
        JimAt(c + new Vector3(4.5f, 0f, 1.5f), 250f);
        Shot(c + new Vector3(8f, 3f, 12f), c + Vector3.up * 1.2f, c + new Vector3(6f, 2.4f, 9f), c + Vector3.up * 1f, 6f, 48f);
        yield return Title("CHAPTER 1", ChTitle2[1], 1.4f);
        Close(0);
        yield return Say(0, "Mars! Everybody out - let's go exploring!");
        Close(1);
        yield return Say(1, "Look at all that red dust... and there's a giant crater over there!");
        JimClose();
        yield return Say(-3, "Hey everyone, check out my new jetpack! It can go really, REALLY far.");
        Close(2);
        yield return Say(2, "Cool! Just... don't fly off anywhere, okay?");
        JimClose();
        yield return Say(-3, "Who, me? Never!");
        yield return CineOff();
        Sfx.Override = null;
        JimAt(Worlds.MarsO + new Vector3(-4f, 0f, -12f), 180f);
        step = 1;
    }
    void JimClose()
    {
        Transform t = jim.transform;
        Vector3 fw = t.forward; fw.y = 0f; fw = fw.sqrMagnitude > 1e-3f ? fw.normalized : Vector3.forward;
        Vector3 rt = new Vector3(fw.z, 0f, -fw.x);
        Vector3 p = t.position + Vector3.up * 0.75f;
        Shot(p + fw * 3.4f + rt * 0.8f + Vector3.up * 0.55f, p, p + fw * 2.9f + rt * 0.8f + Vector3.up * 0.5f, p, 3.5f, 42f);
    }

    // end of chapter 1: Jimmy blasts off
    IEnumerator BoltCo()
    {
        Vector3 J = OnGround(jim.transform.position);
        Frog l = Lead;
        float yaw = YawTo(J, l.transform.position);
        JimAt(J, yaw);
        E2PoseRing(J, 4.5f, yaw);
        yield return CineOn();
        JimClose();
        yield return Say(-3, "Watch this! Three... two... one...");
        var hum = Sfx.Loop(gameObject, Sfx.EngRocket != null ? Sfx.EngRocket : Sfx.Rotor);
        if (hum != null && !skipping) { hum.volume = 0.6f; hum.pitch = 1.5f; hum.Play(); }
        Vector3 away = Quaternion.Euler(0f, yaw + 180f, 0f) * Vector3.forward;
        Vector3 cam = J + Quaternion.Euler(0f, yaw, 0f) * new Vector3(4f, 1.5f, 9f);
        Track(() => cam, () => jim.transform.position + Vector3.up * 0.8f, 50f);
        FX.Dust(J, 1f);
        yield return Anim(3f, k => { jim.transform.position = J + Vector3.up * 120f * k * k + away * 40f * k; jim.transform.rotation = Quaternion.LookRotation(away) * Quaternion.Euler(-60f * k, 0f, 0f); jim.jet = 1f; jim.air = true; camShake = 0.06f; });
        if (hum != null) { hum.Stop(); Destroy(hum); }
        Close(0);
        yield return Say(0, "JIMMY! Come back!");
        yield return Say(-3, "(far away)  Catch me if you can!", 2.4f);
        jim.gameObject.SetActive(false);
        Close(1);
        yield return Say(1, "The radar says he's flying to Jupiter... to its moon Callisto!");
        Close(0, -0.8f);
        yield return Say(0, "Back to the Starship, everyone. We're going after him!");
        yield return FadeTo(1f, 0.7f);
        Next(2, 0);
    }

    // ---------------- arrival at a chase place (chapters 2 - 5) ----------------
    static readonly string[] DestId = { "", "", "callisto", "mars", "mercury", "saturn" };
    static readonly string[] DestLine = { "", "", "NEXT STOP: CALLISTO", "NEXT STOP: MARS (AGAIN!)", "NEXT STOP: MERCURY", "NEXT STOP: SATURN'S MOON ENCELADUS" };
    IEnumerator ArriveCo(int c)
    {
        ui.fadeK = 1f;
        yield return CineOn();
        Pl p = ChPlace(c);
        yield return TravelCo(DestId[c], DestLine[c]);
        E2Goto(p);
        jim.gameObject.SetActive(false);
        yield return LandCo(p);
        Vector3 c0 = Spawn(p, 1);
        E2PoseLine(c0, 0f);
        cC = PlaceO(p); cR = 56f; cHopMin = 26f; cHopMax = 44f;
        Vector3 js = OnGround(c0 + new Vector3(10f, 0f, 30f));
        JimAt(js, 180f);
        Shot(c0 + new Vector3(-6f, 3.5f, -9f), c0 + Vector3.forward * 12f + Vector3.up * 2f, c0 + new Vector3(-4f, 3f, -7f), c0 + Vector3.forward * 14f + Vector3.up * 1.5f, 6f, 50f);
        yield return Title("CHAPTER " + c, ChTitle2[c], 1.4f);
        switch (c)
        {
            case 2:
                Close(0); yield return Say(0, "Callisto! Whoa - the gravity is so low, every hop is a giant hop!");
                Close(1); yield return Say(1, "There he is! JIMMY, come back!");
                break;
            case 3:
                Close(2); yield return Say(2, "Back on Mars... I can see his jet trail over the dunes!");
                Close(0); yield return Say(0, "Spread out, everyone. He can't boost forever!");
                break;
            case 4:
                Close(0); yield return Say(0, "Whoa... look at the size of that Sun!");
                Close(1); yield return Say(1, "It's SO hot here. Let's catch him fast!");
                break;
            case 5:
                Close(0); yield return Say(0, "Saturn's icy moon... look at those rings!");
                Close(2); yield return Say(2, "Careful - those ice geysers will launch you sky-high.");
                Close(1); yield return Say(1, "We're getting really tired of chasing, Jimmy...");
                break;
        }
        Shot(js + new Vector3(4f, 2.2f, -7f), js + Vector3.up, js + new Vector3(3f, 1.8f, -5.5f), js + Vector3.up * 0.9f, 3.5f, 45f);
        string[] jl = { "", "", "Come and get me!", "Took you long enough!", "Hot hot hot! Catch me if you can!", "Wheee! Over here!" };
        yield return Say(-3, jl[c]);
        yield return CineOff();
        step = 1;
        StartChaseFor(c);
        ToastAll(c == 2 ? "Get close to Jimmy to make him burn a boost. When his fuel runs out, TAG him!" : "Follow the radar and his jet trail - wear down his fuel, then tag him!", 5f);
    }

    // ---------------- caught... and away again ----------------
    static readonly string[] GotLine = { "", "", "Got you! Tag - you're caught!", "Gotcha this time! Right, Jimmy?", "Phew, it's hot... Okay Jimmy, time to go home.", "Brrr... Got you! Now can we PLEASE go home?" };
    static readonly string[] JimLine = { "", "", "Ha! Nice try! This jetpack goes WAY farther than Callisto. See ya!", "Almost! But I've always wanted to see Mercury. See ya!", "Too hot, too hot! I'm off to cool down by Saturn. See ya!", "Brrr is right! I'm going somewhere cozy... underground! See ya!" };
    static readonly string[] Crew1 = { "", "", "He's getting away again!", "Mercury?! That's right next to the Sun!", "Saturn?! He's flying all the way out there?", "Underground? The radar shows him diving back to Mars..." };
    static readonly string[] Crew2 = { "", "", "The radar says... he's heading back to Mars!", "Then we'd better hurry before he gets a sunburn.", "This is getting ridiculous... but we're not giving up.", "...and going DOWN. Into the ground!" };
    IEnumerator EscapeCo(int c, Frog by)
    {
        Vector3 J = OnGround(jim.transform.position);
        float yaw = YawTo(J, by.transform.position);
        JimAt(J, yaw);
        E2PoseRing(J, 2.6f, yaw);
        SetStatus(""); ui.Radar(false, Vector2.zero, "");
        yield return CineOn();
        Sfx.Play(Sfx.Win, 0.6f, 1.3f);
        ui.bigText = "TAGGED!"; ui.bigK = 1f;
        Vector3 fw = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward, rt = new Vector3(fw.z, 0f, -fw.x);
        Shot(J + fw * 7.5f + rt * 3f + Vector3.up * 3f, J + Vector3.up * 0.8f, J + fw * 6f + rt * 2.2f + Vector3.up * 2.4f, J + Vector3.up * 0.8f, 4f, 48f);
        yield return Wait(1f);
        yield return Anim(0.5f, k => ui.bigK = 1f - k);
        yield return Say(0, GotLine[c]);
        JimClose();
        yield return Say(-3, JimLine[c]);
        // a fresh fuel cell clicks in... and he's gone
        var hum = Sfx.Loop(gameObject, Sfx.EngRocket != null ? Sfx.EngRocket : Sfx.Rotor);
        if (hum != null && !skipping) { hum.volume = 0.6f; hum.pitch = 1.5f; hum.Play(); }
        Vector3 cam = J + fw * 9f + rt * 4f + Vector3.up * 1.5f;
        Track(() => cam, () => jim.transform.position + Vector3.up * 0.8f, 52f);
        FX.Dust(J, 1f);
        Vector3 away = -fw;
        yield return Anim(2.4f, k => { jim.transform.position = J + Vector3.up * 110f * k * k + away * 30f * k; jim.transform.rotation = Quaternion.LookRotation(away) * Quaternion.Euler(-55f * k, 0f, 0f); jim.jet = 1f; jim.air = true; camShake = 0.06f; });
        if (hum != null) { hum.Stop(); Destroy(hum); }
        jim.gameObject.SetActive(false);
        Close(1);
        yield return Say(1, Crew1[c]);
        Close(2);
        yield return Say(2, Crew2[c]);
        yield return FadeTo(1f, 0.7f);
        Next(c + 1, 0);
    }

    // ---------------- chapter 6: under Mars ----------------
    IEnumerator DeepArriveCo()
    {
        ui.fadeK = 1f;
        yield return CineOn();
        yield return TravelCo("mars", "NEXT STOP: MARS... AGAIN?!");
        E2Goto(Pl.Mars);
        jim.gameObject.SetActive(false);
        yield return LandCo(Pl.Mars);
        Vector3 cave = OnGround(M(0f, 24f));
        E2PoseLine(cave, 0f);
        Shot(cave + new Vector3(5f, 3f, -9f), cave + new Vector3(0f, 3f, 12f), cave + new Vector3(3f, 2.4f, -7f), cave + new Vector3(0f, 2.5f, 12f), 6f, 50f);
        Sfx.Override = "story_tense";
        yield return Say(1, "His jet trail goes right into that old cave...");
        yield return Say(2, "...and DOWN. I didn't know the cave went down!");
        yield return Say(-4, "Woof! Hello, froggies! The Mars froggies and doggies live down there - three levels deep. Your friend zoomed all the way to the bottom!");
        Close(0);
        yield return Say(0, "Then that's where we're going. Down we go!");
        yield return FadeTo(1f, 0.6f);
        E2Goto(Pl.Deep, true, 0);
        CamWorld = WorldId.Mars;
        JimAt(StoryEp2Set.DeepC(2) + new Vector3(0f, 0f, 4f), 180f);
        Vector3 c1 = Spawn(Pl.Deep, 1, 0);
        E2PoseLine(c1, 0f);
        Shot(c1 + new Vector3(6f, 3.2f, -5f), c1 + new Vector3(0f, 1.5f, 14f), c1 + new Vector3(4f, 2.6f, -3f), c1 + new Vector3(0f, 1.5f, 16f), 7f, 52f);
        yield return FadeTo(0f, 0.8f);
        yield return Title("CHAPTER 6", ChTitle2[6], 1.4f);
        Close(1);
        yield return Say(1, "Wow... a whole town of froggies and doggies under Mars!");
        Close(0);
        yield return Say(0, "Look - glowing bits of his jet trail. Follow it down!");
        yield return CineOff();
        Sfx.Override = null;
        step = 1;
    }

    IEnumerator DenFoundCo()
    {
        Vector3 e = OnGround(StoryEp2Set.DeepO + new Vector3(0f, -28f, 106f));
        Vector3 J = OnGround(StoryEp2Set.DeepC(2) + new Vector3(0f, 0f, 2f));
        E2PoseLine(e, 0f);
        JimAt(J, 180f);
        yield return CineOn();
        Shot(e + new Vector3(4f, 2.8f, -6f), J + Vector3.up, e + new Vector3(3f, 2.4f, -4.5f), J + Vector3.up, 5f, 48f);
        yield return Say(-3, "You found my secret hideout?! Nobody finds my secret hideout!");
        Close(0);
        yield return Say(0, "There's nowhere left to fly, Jimmy!");
        JimClose();
        yield return Say(-3, "Wanna bet?");
        yield return CineOff();
        step = 3;
        StartChaseFor(6);
        JimAt(J, 180f);
    }

    // tagged in the Deep Den: up the vent shaft... the low point... and hope
    IEnumerator VentCo(Frog by)
    {
        Vector3 J = OnGround(jim.transform.position);
        float yaw = YawTo(J, by.transform.position);
        JimAt(J, yaw);
        E2PoseRing(J, 2.6f, yaw);
        SetStatus(""); ui.Radar(false, Vector2.zero, "");
        yield return CineOn();
        Sfx.Play(Sfx.Win, 0.6f, 1.3f);
        ui.bigText = "TAGGED!"; ui.bigK = 1f;
        Vector3 fw = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward, rt = new Vector3(fw.z, 0f, -fw.x);
        Shot(J + fw * 7f + rt * 3f + Vector3.up * 3f, J + Vector3.up * 0.8f, J + fw * 5.5f + rt * 2f + Vector3.up * 2.4f, J + Vector3.up * 0.8f, 4f, 48f);
        yield return Wait(1f);
        yield return Anim(0.5f, k => ui.bigK = 1f - k);
        yield return Say(0, "Got you! For real this time!");
        JimClose();
        yield return Say(-3, "Not yet! Sorry, froggies - gotta fly!");
        Vector3 vent = OnGround(StoryEp2Set.DeepO + new Vector3(0f, -28f, 134f));
        Vector3 cam = OnGround(StoryEp2Set.DeepC(2) + new Vector3(8f, 0f, -4f)) + Vector3.up * 2f;
        Track(() => cam, () => jim.transform.position + Vector3.up, 55f);
        var hum = Sfx.Loop(gameObject, Sfx.EngRocket != null ? Sfx.EngRocket : Sfx.Rotor);
        if (hum != null && !skipping) { hum.volume = 0.5f; hum.pitch = 1.6f; hum.Play(); }
        yield return Anim(1.2f, k => { jim.transform.position = Vector3.Lerp(J, vent, k) + Vector3.up * 3f * Mathf.Sin(k * Mathf.PI); jim.jet = 1f; jim.air = true; });
        yield return Anim(2.2f, k => { jim.transform.position = vent + Vector3.up * 40f * k * k; jim.transform.rotation = Quaternion.Euler(-80f * k, yaw, 0f); jim.jet = 1f; camShake = 0.05f; });
        if (hum != null) { hum.Stop(); Destroy(hum); }
        jim.gameObject.SetActive(false);
        Close(1);
        yield return Say(1, "He went up the vent shaft... he's gone. Again.");
        // the low point
        Vector3 c = OnGround(StoryEp2Set.DeepC(2) + new Vector3(0f, 0f, -6f));
        E2PoseLine(c, 180f);
        foreach (var f in Team()) Slump(f, true);
        Sfx.Override = "story_somber";
        Shot(c + new Vector3(2.5f, 1.4f, -6f), c + Vector3.up * 0.6f, c + new Vector3(1.5f, 1.2f, -4.5f), c + Vector3.up * 0.5f, 9f, 46f);
        yield return Anim(2f, k => ui.dimK = 0.55f * k);
        yield return Say(2, "Callisto. Mars. Mercury. Saturn. Under Mars... and he got away every single time.");
        yield return Say(1, "We're so tired...");
        yield return Say(0, "Maybe... maybe we'll never catch him.");
        ui.bigText = "We'll never catch him...";
        Shot(c + new Vector3(0f, 5f, -14f), c + Vector3.up, c + new Vector3(0f, 7f, -18f), c + Vector3.up, 7f, 50f);
        yield return Anim(1.5f, k => ui.bigK = k);
        yield return Wait(3.5f);
        yield return Anim(1.5f, k => ui.bigK = 1f - k);
        // hope: a Mars doggy heard something
        Sfx.Override = "story_faith";
        if (E.marsDogs.Count > 0)
        {
            Animal d = E.marsDogs[E.marsDogs.Count - 1];
            Vector3 dp = OnGround(c + new Vector3(3.5f, 0f, -2.5f));
            d.transform.position = dp; d.area = new Rect(dp.x - 1f, dp.z - 1f, 2f, 2f);
        }
        Shot(c + new Vector3(5f, 2.2f, -6f), c + new Vector3(1.5f, 0.7f, -1f), c + new Vector3(4f, 2f, -5f), c + new Vector3(1.5f, 0.7f, -1f), 6f, 46f);
        yield return Say(-4, "Woof... excuse me, froggies? I heard his jetpack when he flew past. It was coughing and sputtering!");
        Slump(Cast(1), false);
        yield return Anim(1.5f, k => ui.dimK = Mathf.Lerp(0.55f, 0.3f, k));
        Close(1);
        yield return Say(1, "Coughing... that means his jetpack is almost out of fuel!");
        Slump(Cast(2), false);
        Close(2);
        yield return Say(2, "And look at the radar - he's flying home. To Earth!");
        Slump(Cast(0), false);
        Close(0);
        yield return Say(0, "Then this is our chance. One last try - all together. Let's bring Jimmy home!");
        foreach (var f in G.frogs) Slump(f, false);
        yield return Anim(1.2f, k => ui.dimK = Mathf.Lerp(0.3f, 0f, k));
        yield return FadeTo(1f, 0.7f);
        Next(7, 0);
    }

    // ---------------- chapter 7: the chase over Earth ----------------
    IEnumerator OrbitIntroCo()
    {
        ui.fadeK = 1f;
        yield return CineOn();
        Sfx.Override = "story_tense";
        yield return TravelCo("earth", "NEXT STOP: EARTH ORBIT");
        E2Goto(Pl.Orbit, false);
        CamWorld = WorldId.Space;
        Transform ship = E.orbitShip;
        Vector3 V = StoryEp2Set.OrbitV;
        ship.position = V + new Vector3(0f, 0f, -150f); ship.rotation = Quaternion.identity;
        jim.gameObject.SetActive(true); jim.transform.localScale = Vector3.one * 2.4f; jim.ShowTag(true);
        jim.transform.position = V + new Vector3(8f, 2f, -40f); jim.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
        if (jim.trail != null) jim.trail.Clear();
        Track(() => ship.position + new Vector3(-12f, 6f, -26f), () => Vector3.Lerp(ship.position, jim.transform.position, 0.6f), 50f);
        yield return FadeTo(0f, 0.8f);
        yield return Anim(3f, k => { ship.position = V + new Vector3(0f, 0f, -150f + 40f * k); jim.transform.position = V + new Vector3(8f, 2f, -40f + 60f * k); jim.jet = 0.6f; jim.sputter = 0.5f; StoryEp2Set.ShipFlame(ship, 0.6f); if (E.orbitStars != null) E.orbitStars.position = CamPos; });
        yield return Title("CHAPTER 7", ChTitle2[7], 1.3f);
        yield return Say(1, "There he is - right above Earth! And his jetpack is sputtering!");
        yield return Say(0, "This is it! Steer the Starship right up to him and catch him with the cargo net!");
        step = 1;
        yield return OrbitGameCo();
    }

    Vector2 chaseMove; bool chaseBoost;
    string OrbitHint()
    {
        InputKind k = G.slots.Count > 0 ? G.slots[0].kind : InputKind.Keyboard;
        if (k == InputKind.Gamepad) return "LEFT STICK steer  ·  UP / RT boost  ·  A cargo net";
        if (k == InputKind.Touch) return "hold the LEFT / RIGHT side to steer  ·  top of the screen = boost  ·  tap = cargo net";
        return "A / D or ARROWS steer  ·  W boost  ·  SPACE / ENTER / CLICK cargo net";
    }
    IEnumerator OrbitGameCo()
    {
        skipping = false;   // a SKIP of the intro must not leak into the game / the capture scene
        CamWorld = WorldId.Space;
        cine = true; skippable = false; modal = true; modalHidesObj = true;
        float lb0 = ui.letterbox;
        yield return Anim(0.5f, k => ui.letterbox = Mathf.Lerp(lb0, 0.35f, k));
        Transform ship = E.orbitShip;
        Vector3 V = StoryEp2Set.OrbitV;
        Vector3 sp = ship.position; float yaw = 0f, spd = 20f, roll = 0f;
        Vector3 jp = jim.transform.position, jv = Vector3.forward * 10f;
        int jinks = 3; float jinkT = 0f, t = 0f, netCool = 0f, msgT = 0f; string msg = "";
        bool caught = false, fuelOut = false;
        advance = false; chaseMove = Vector2.zero; chaseBoost = false;
        Track(() => ship.position - Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * 26f + Vector3.up * 8f, () => ship.position + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * 24f - Vector3.up * 3f, 55f);
        Sfx.Override = "story_triumph";
        Debug.Log("FFSTORY ep2 orbit chase start");
        while (!caught)
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            t += dt;
            Vector2 mv = chaseMove; chaseMove = Vector2.zero;
            bool boost = chaseBoost; chaseBoost = false;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) mv.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) mv.x += 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) mv.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) mv.y -= 1f;
            }
            try
            {
                if (Input.touchCount > 0)
                {
                    Vector2 tp = Input.GetTouch(0).position;
                    mv.x += Mathf.Clamp((tp.x - Screen.width * 0.5f) / (Screen.width * 0.25f), -1f, 1f);
                    if (tp.y > Screen.height * 0.6f) mv.y += 1f;
                }
            }
            catch { }
            Vector3 toJ = jp - sp; toJ.y = 0f;
            float dj = toJ.magnitude;
            if (demo)
            {
                float da = Mathf.DeltaAngle(yaw, Mathf.Atan2(toJ.x, toJ.z) * Mathf.Rad2Deg);
                mv.x = Mathf.Clamp(da / 25f, -1f, 1f); mv.y = 1f;
            }
            mv.x = Mathf.Clamp(mv.x, -1f, 1f);
            yaw += mv.x * 80f * dt;
            float want = mv.y > 0.3f || boost ? 32f : mv.y < -0.3f ? 12f : 21f;
            spd = Mathf.MoveTowards(spd, want, 14f * dt);
            Vector3 fw = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 off = sp - V; off.y = 0f;
            if (off.magnitude > 330f) yaw = Mathf.MoveTowardsAngle(yaw, Mathf.Atan2(-off.x, -off.z) * Mathf.Rad2Deg, 50f * dt);
            sp += fw * spd * dt;
            roll = Mathf.Lerp(roll, -mv.x * 28f, dt * 4f);
            ship.position = sp;
            ship.rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, roll);
            StoryEp2Set.ShipFlame(ship, 0.4f + (spd - 12f) / 20f * 0.7f);
            // Jimmy: flees, jinks sideways three times, then his jetpack gives out
            if (!fuelOut)
            {
                Vector3 away = dj > 0.1f ? toJ / dj : Vector3.forward;
                Vector3 wander = Quaternion.Euler(0f, Mathf.Sin(t * 0.45f) * 70f, 0f) * away;
                Vector3 wantV = wander * (dj < 70f ? 17f : 10f);
                Vector3 cc = jp - V; cc.y = 0f;
                if (cc.magnitude > 240f) wantV += -cc.normalized * 16f;
                jv = Vector3.MoveTowards(jv, wantV, 9f * dt);
                jinkT -= dt;
                if (jinks > 0 && dj < 28f && jinkT <= 0f)
                {
                    jinks--; jinkT = 2.4f;
                    Vector3 side = Vector3.Cross(Vector3.up, fw) * (Random.value < 0.5f ? -1f : 1f);
                    jv = side * 40f + away * 12f;
                    msg = "JIMMY:  " + Taunts[Random.Range(0, Taunts.Length)]; msgT = 2f;
                    Sfx.Play(Sfx.Missile != null ? Sfx.Missile : Sfx.Boom, 0.5f, 1.4f);
                    Debug.Log("FFSTORY ep2 orbit jink, left " + jinks);
                }
                if ((jinks == 0 && jinkT <= 0.6f) || t > 75f)
                {
                    fuelOut = true; jinks = 0;
                    msg = "His jetpack is OUT OF FUEL - fly right up to him!"; msgT = 3f;
                    Sfx.Play(Sfx.Bonk != null ? Sfx.Bonk : Sfx.Click, 0.7f, 0.7f);
                    Debug.Log("FFSTORY ep2 orbit out of fuel t=" + t.ToString("0"));
                }
            }
            else jv = Vector3.MoveTowards(jv, (dj > 0.1f ? toJ / dj : Vector3.forward) * 3.5f, 8f * dt);
            jp += jv * dt;
            jp.y = V.y + 2f + Mathf.Sin(t * 1.3f) * 1.5f;
            jim.transform.position = jp;
            Vector3 jf = new Vector3(jv.x, 0f, jv.z);
            if (jf.sqrMagnitude > 0.1f) jim.transform.rotation = Quaternion.LookRotation(jf) * Quaternion.Euler(fuelOut ? 10f : 35f, 0f, 0f);
            jim.jet = fuelOut ? 0f : 0.85f; jim.sputter = fuelOut ? 1f : 0.3f; jim.air = true; jim.speed = 2f;
            // the cargo net: A when he is close in front of the nose; or simply fly into him once he's out of fuel
            toJ = jp - sp; toJ.y = 0f; dj = toJ.magnitude;
            float front = dj > 0.1f ? Vector3.Dot(fw, toJ / dj) : 1f;
            netCool -= dt;
            if (demo && fuelOut && dj < 18f) advance = true;
            if (advance)
            {
                advance = false;
                if (netCool <= 0f)
                {
                    netCool = 0.8f;
                    if (dj < 24f && front > 0.7f)
                    {
                        if (fuelOut) caught = true;
                        else { msg = "He dodged the net - wear him out first!"; msgT = 2f; jinkT = 0f; }
                    }
                    else { msg = dj >= 24f ? "Too far for the net - get closer!" : "Point the nose at him!"; msgT = 1.6f; }
                }
            }
            if (fuelOut && dj < 9f && front > 0.2f) caught = true;
            if (E.orbitStars != null) E.orbitStars.position = CamPos;
            if (E.orbitEarth != null) E.orbitEarth.Rotate(0f, dt * 0.6f, 0f, Space.World);
            msgT -= dt;
            string bars = fuelOut ? "<color=#b6ff5a><b>OUT OF FUEL!</b></color>" : "fuel  <color=#b6ff5a>" + new string('|', jinks + 1) + "</color>";
            ui.CineHud("<b>CATCH JIMMY!</b>   JIMMY " + Mathf.RoundToInt(dj) + " m   ·   " + bars + "\n<size=17>" + (msgT > 0f ? "<color=#ffd84a>" + msg + "</color>" : OrbitHint()) + "</size>");
            yield return null;
        }
        ui.CineHud("");
        modal = false; modalHidesObj = false;
        Debug.Log("FFSTORY ep2 orbit caught t=" + t.ToString("0"));
        yield return CaptureCo();
    }

    // ---------------- the capture, home, celebration, credits ----------------
    IEnumerator CaptureCo()
    {
        Transform ship = E.orbitShip;
        Vector3 V = StoryEp2Set.OrbitV;
        if (place != Pl.Orbit)
        {
            // demo / replay entry straight into the capture
            E2Goto(Pl.Orbit, false);
            ship.position = V + new Vector3(0f, 0f, -20f); ship.rotation = Quaternion.identity;
            jim.gameObject.SetActive(true); jim.transform.localScale = Vector3.one * 2.4f;
            jim.transform.position = ship.position + Vector3.forward * 16f + Vector3.up * 2f;
            ui.fadeK = 0f;
        }
        CamWorld = WorldId.Space;
        cine = true; skippable = true;
        float lb0 = ui.letterbox;
        yield return Anim(0.4f, k => ui.letterbox = Mathf.Lerp(lb0, 1f, k));
        Sfx.Override = "story_triumph";
        Vector3 sp = ship.position, jp0 = jim.transform.position;
        Vector3 bay = sp + ship.up * 2.5f - ship.forward * 1f;
        Vector3 side = ship.right;
        Track(() => sp + side * 16f + Vector3.up * 5f + ship.forward * 6f, () => jim.transform.position, 46f);
        Sfx.Play(Sfx.Confirm != null ? Sfx.Confirm : Sfx.Pickup, 0.8f, 1.1f);
        yield return Anim(1.8f, k => { jim.transform.position = Vector3.Lerp(jp0, bay, Mathf.SmoothStep(0f, 1f, k)); jim.jet = 0f; jim.sputter = 0.6f; if (Random.value < 0.4f) FX.Sparkle(jim.transform.position, new Color(0.7f, 1f, 0.5f), 4); if (E.orbitStars != null) E.orbitStars.position = CamPos; });
        Sfx.Play(Sfx.Win, 0.9f, 1f);
        ui.bigText = "CAUGHT!"; 
        yield return Anim(0.5f, k => ui.bigK = k);
        yield return Wait(1.4f);
        yield return Anim(0.5f, k => ui.bigK = 1f - k);
        yield return Say(-3, "Okay, okay... you got me! My jetpack is totally out of fuel anyway.");
        yield return Say(0, "Jimmy! We chased you across the WHOLE solar system!");
        yield return Say(-3, "I know... I'm sorry I ran off. I just wanted to see how far it could go.");
        yield return Say(1, "Well, now we know - REALLY far. Come on, let's go home. Together.");
        yield return FadeTo(1f, 0.7f);
        StoryEp2Set.ShipFlame(ship, 0f);
        yield return HomeCo2();
    }

    IEnumerator HomeCo2()
    {
        E2Goto(Pl.Ranch);
        jim.transform.localScale = Vector3.one; jim.jet = 0f; jim.sputter = 0f; jim.air = false;
        jim.gameObject.SetActive(false);
        Sfx.Override = "story_triumph";
        yield return LandCo(Pl.Ranch);
        Vector3 c = OnGround(StoryEp2Set.RanchLanding + new Vector3(0f, 0f, -13f));
        E2PoseLine(c, 180f, 2.4f);
        var t = Team();
        JimAt(c + new Vector3(t.Count * 1.25f + 0.8f, 0f, 0.4f), 180f);
        jim.ShowTag(false);
        FaceAll(c + new Vector3(0f, 0f, -12f));
        if (RanchLife.I != null) foreach (var r in RanchLife.I.robots) if (r.manual == null) r.Order("dance", Lead);
        fireworks = true;
        Shot(c + new Vector3(0f, 2.2f, -10f), c + Vector3.up * 1.2f, c + new Vector3(0f, 2f, -8f), c + Vector3.up, 6f, 50f);
        yield return Say(1, "Home sweet home!");
        JimClose();
        yield return Say(-3, "Sorry for all the running, everyone. That was the best game of tag EVER.");
        Close(0);
        yield return Say(0, "Next time, let's play tag... WITHOUT the jetpack.");
        JimClose();
        yield return Say(-3, "Deal!");
        Close(2);
        yield return Say(2, "Welcome home, Jimmy.");
        Shot(c + new Vector3(24f, 8f, -30f), StoryEp2Set.RanchLanding + Vector3.up * 18f, c + new Vector3(16f, 4f, -22f), StoryEp2Set.RanchLanding + Vector3.up * 22f, 18f, 55f);
        ui.SetCredits(Credits2());
        yield return Anim(1.5f, k => ui.creditsK = k);
        yield return Wait(skipping ? 0f : 9f);
        yield return Anim(1f, k => ui.creditsK = 1f - k);
        ui.bigText = "They finally got him back.";
        yield return Anim(1.5f, k => ui.bigK = k);
        yield return Wait(3.5f);
        yield return Anim(1.5f, k => ui.bigK = 1f - k);
        fireworks = false;
        yield return CineOff();
        ch = 8; step = 0; Save2();
        ToastAll("THE END - thanks for playing Catch Jimmy! Free play continues.", 6f);
        Debug.Log("FFSTORY ep2 complete");
        Sfx.Override = null;
        Active = true; Stop();
    }

    string Credits2()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<size=40><b>CATCH JIMMY!</b></size>\n<size=20>a Four Froggies story · episode 2</size>\n\n");
        sb.Append("<size=22><color=#ffd27a>STARRING</color></size>\n");
        foreach (var f in Team()) sb.Append(Roster.Name(f.charId) + "\n");
        sb.Append("and Jimmy (and his jetpack)\n\nthe Mars froggies and doggies\n\n");
        sb.Append("<size=22><color=#ffd27a>FILMED ON LOCATION</color></size>\nMars · Callisto · Mercury · Enceladus · Earth orbit\n\n");
        sb.Append("<size=20>Thanks for never giving up on a friend.</size>");
        return sb.ToString();
    }

    // ================= demos =================
    static int DemoChapter2(string shot)
    {
        switch (shot)
        {
            case "ep2intro": return 1;
            case "ep2callisto": return 2;
            case "ep2mars": return 3;
            case "ep2mercury": return 4;
            case "ep2saturn": return 5;
            case "ep2chamber3": return 6;
            case "ep2orbit": return 7;
            case "ep2end": return 7;
        }
        return 0;
    }
    static int DemoStep2(string shot) { return shot == "ep2chamber3" ? 3 : 0; }

    float demoMoveT;
    void DemoAfterStart2()
    {
        demoT = 0f; demoPhase = 0; demoMoveT = 0f;
        string spd = Param("storyspeed"); float sp;
        if (spd != null && float.TryParse(spd, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out sp)) Time.timeScale = Mathf.Clamp(sp, 0.25f, 4f);
        Debug.Log("FFSTORY demo start " + demoShot + " ch " + ch + " t=" + Time.realtimeSinceStartup.ToString("0.0"));
    }
    // the demo player walks up to Jimmy so the probe sees dodges, the out-of-fuel hop and the tag
    void DemoTick2(float dt)
    {
        demoT += dt;
        if (runner.Busy || G.slots.Count == 0) return;
        Frog f = Lead;
        demoMoveT -= dt;
        if (demoMoveT > 0f) return;
        if (InChase && jim != null && jim.gameObject.activeSelf && (jState == 0 || jState == 2))
        {
            demoMoveT = jState == 2 ? 3.5f : 3f;
            Vector3 jp = jim.transform.position;
            Vector3 dir = f.transform.position - jp; dir.y = 0f;
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.back;
            float d = jState == 2 ? 1.6f : cAlert - 2f;
            Vector3 p = OnGround(jp + dir * d, 0.3f);
            float yaw = YawTo(p, jp);
            f.SendTo(f.world, p, yaw);
            G.StoryFaceCam(yaw);
            Debug.Log("FFSTORY demo step to jimmy (state " + jState + ", fuel " + dodgesLeft + ")");
            return;
        }
        if (ch == 1 && step >= 1)
        {
            demoMoveT = 3f;
            Vector3 tgt = step == 1 ? M(9f, -6f) : jim.transform.position;
            Vector3 p = OnGround(tgt + new Vector3(-3f, 0f, -3f), 0.3f);
            f.SendTo(WorldId.Mars, p, YawTo(p, tgt)); G.StoryFaceCam(YawTo(p, tgt));
            return;
        }
        if (ch == 6 && (step == 1 || step == 2))
        {
            demoMoveT = 4f;
            Vector3 p = step == 1 ? StoryEp2Set.DeepC(1) + new Vector3(0f, 0.4f, -14f) : StoryEp2Set.DeepC(2) + new Vector3(0f, 0.4f, -14f);
            p = OnGround(p, 0.4f);
            f.SendTo(WorldId.Mars, p, 0f); G.StoryFaceCam(0f);
        }
    }
}
