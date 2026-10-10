using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// ffu22 STORY MODE - "The Big Launch" (Bill, Oct 2026). Picked with the lobby STORY button; free play is untouched.
// Arc: the Mars pups call for help -> the froggies build a rocket from parts all over the ranch -> the test fire bursts a
// fuel tank (quick repair) -> a storm batters the pad (hold out) -> the first launch fails and falls back ("It's over...?")
// -> a friend says keep going / have faith -> the spare fins, the patched tank and the rescued chip turn out to be the key
// -> the relaunch climbs into sunshine -> Mars: clear the rockfall, rescue the pups -> home: fireworks, credits, all is well.
// Seven chapters, each a checkpoint saved to localStorage (FFStory.jslib, try/catch). Progress is shared by the whole
// team (shared screen or split-screen); cutscenes use one full-screen story camera with letterbox bars + dialogue
// (skippable). Speakers are the seats' roster characters (never invented names), plus the Mars pups on the phone.
// URL: &storych=N jumps to chapter N (tests); ?ffdemo=1&ffshot=storyintro|storyparts|storystorm|storyfail|storyrelaunch|storyend.
public partial class Story : MonoBehaviour
{
    public static Story I;
    public static bool Active;
    public const string SaveKey = "ff.story.biglaunch";
    public static readonly string[] ChTitle = { "", "A Call From Mars", "Gathering the Parts", "The Test Fire", "The Storm", "Liftoff?", "Keep Going", "To Mars and Home" };
    public static readonly string[] PartName = { "ENGINE", "FUEL TANKS", "NOSE CONE", "FINS", "COMPUTER CHIP" };

    public int ch = 1, step;
    public int parts;                          // delivered mask (bit per part)
    readonly int[] state = new int[5];         // 0 todo, 1 carried / loaded, 2 delivered
    readonly Frog[] carrier = new Frog[5];
    readonly Transform[] icon = new Transform[5];
    Vehicle engineOn;
    public StoryUI ui;
    StorySet S { get { return StorySet.I; } }
    Game G { get { return Game.I; } }
    bool demo; string demoShot = "";
    float hintT, chTime;

    // ---------------- lifecycle ----------------
    public static void Begin()
    {
        if (I == null) I = new GameObject("Story").AddComponent<Story>();
        I.Begin1();
    }

    void Begin1()
    {
        StorySet.Ensure();
        if (ui == null) ui = new StoryUI();
        ui.SetVisible(true);
        Active = true;
        HookOnce();
        string u = Application.absoluteURL ?? "";
        demo = u.Contains("ffdemo");
        int k = u.IndexOf("ffshot=");
        demoShot = "";
        if (k >= 0) { demoShot = u.Substring(k + 7); int e = demoShot.IndexOfAny(new[] { '&', '#' }); if (e >= 0) demoShot = demoShot.Substring(0, e); }
        int jump = DemoChapter(demoShot);
        string jc = Param("storych");
        int jn;
        if (jc != null && int.TryParse(jc, out jn)) jump = Mathf.Clamp(jn, 1, 7);
        Debug.Log("FFSTORY begin demo=" + demo + " shot=" + demoShot + " jump=" + jump);
        if (jump > 0) { StartChapter(jump, DemoStep(demoShot)); return; }
        int sch, sst, sp;
        if (Load(out sch, out sst, out sp) && sch >= 1 && sch <= 8 && !(sch == 1 && sst == 0 && sp == 0))
        {
            Play(ChoiceCo(sch, sst, sp));
            return;
        }
        StartChapter(1, 0);
    }

    public void Stop()
    {
        if (!Active) return;
        Active = false;
        runner.Clear(); cine = false; modal = false;
        Sfx.Override = null;
        Worlds.StormK = 0f; Worlds.Flash = 0f; Worlds.StormReset();
        SunK(1f);
        if (S != null) { S.ClearStorm(); S.Beams(new List<Vector3>(), WorldId.Ranch); S.Rain(new List<Camera>(), 0f, Vector3.zero); S.root.gameObject.SetActive(false); S.MarsActive(false); if (S.spaceRoot != null) S.spaceRoot.gameObject.SetActive(false); }
        foreach (var f in G.frogs) Slump(f, false);
        for (int i = 0; i < 5; i++) DropIcon(i);
        if (S != null)
        {
            // story pickups stay inert in free play (Pickups would still "collect" hidden ones)
            foreach (var it in S.fuel) { it.taken = true; it.t.gameObject.SetActive(false); }
            foreach (var it in S.fins) { it.taken = true; it.t.gameObject.SetActive(false); }
            S.noseItem.taken = true; S.noseItem.t.gameObject.SetActive(false);
            S.chipItem.taken = true; S.chipItem.t.gameObject.SetActive(false);
            if (engineOn != null) { S.engineCrate.SetParent(S.root, true); engineOn = null; }
            if (ui != null) ui.letterbox = ui.dimK = ui.fadeK = ui.bigK = ui.titleK = ui.creditsK = 0f;
            fireworks = false; poses.Clear();
            if (S.rocket != null) { S.rocket.root.gameObject.SetActive(true); RocketHome(); }
        }
        if (ui != null) { ui.SetVisible(false); ui.HideMarkers(); }
        Debug.Log("FFSTORY stop");
    }

    static string Param(string key)
    {
        string u = Application.absoluteURL ?? "";
        int q = u.IndexOf('?');
        if (q < 0) return null;
        foreach (string kv in u.Substring(q + 1).Split('&', '#'))
        {
            int e = kv.IndexOf('=');
            if (e > 0 && kv.Substring(0, e) == key) return System.Uri.UnescapeDataString(kv.Substring(e + 1));
        }
        return null;
    }

    // ---------------- save (localStorage) ----------------
#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")] static extern int FFStorySave(string k, string v);
    [System.Runtime.InteropServices.DllImport("__Internal")] static extern string FFStoryLoad(string k);
#else
    static int FFStorySave(string k, string v) { PlayerPrefs.SetString(k, v); return 1; }
    static string FFStoryLoad(string k) { return PlayerPrefs.GetString(k, ""); }
#endif
    void Save()
    {
        if (demo) return;
        string v = "bl1|" + ch + "|" + step + "|" + parts;
        try { FFStorySave(SaveKey, v); Debug.Log("FFSTORY saved " + v); } catch (System.Exception e) { Debug.LogWarning("FFSTORY save failed: " + e.Message); }
    }
    bool Load(out int c, out int s, out int p)
    {
        c = 1; s = 0; p = 0;
        if (demo) return false;
        string v = "";
        try { v = FFStoryLoad(SaveKey) ?? ""; } catch (System.Exception e) { Debug.LogWarning("FFSTORY load failed: " + e.Message); return false; }
        var a = v.Split('|');
        if (a.Length < 4 || a[0] != "bl1") return false;
        return int.TryParse(a[1], out c) && int.TryParse(a[2], out s) && int.TryParse(a[3], out p);
    }

    // ---------------- cast ----------------
    // cast 0 = P1's character (the lead), then the other seats in order; Roster names only
    Frog Cast(int k)
    {
        var list = new List<Frog>();
        if (G.slots.Count > 0) list.Add(G.frogs[G.slots[0].frog]);
        foreach (var f in G.frogs) if (!list.Contains(f)) list.Add(f);
        return list[Mathf.Clamp(k, 0, list.Count - 1)];
    }
    string CastName(int k) { return Roster.Name(Cast(k).charId); }
    List<Frog> Humans() { var l = new List<Frog>(); foreach (var s in G.slots) l.Add(G.frogs[s.frog]); return l; }
    Frog Lead { get { return Cast(0); } }

    // ---------------- chapters ----------------
    void StartChapter(int c, int s)
    {
        ch = c; step = s; chTime = 0f;
        runner.Clear(); cine = false; modal = false;
        ResetParts(c >= 3 ? 31 : (c == 2 ? parts : 0), c == 2);
        Debug.Log("FFSTORY chapter " + c + " step " + s + " t=" + Time.realtimeSinceStartup.ToString("0.0"));
        S.root.gameObject.SetActive(true);
        S.ClearStorm();
        Worlds.StormK = c == 4 && s >= 1 ? 1f : c == 5 ? 0.55f : c == 6 && s < 3 ? 0.45f : 0f;
        SunK(1f - Worlds.StormK * 0.6f);
        S.benchFins.gameObject.SetActive(c == 6 && s == 1);
        fixedFins = fixedTank = fixedChip = c > 6 || (c == 6 && s >= 2);
        if (c == 6 && s >= 2) S.benchFins.gameObject.SetActive(false);
        RocketHome();
        ApplyRocket();
        S.ShowPickups(c == 2, parts);
        Sfx.Override = c == 4 ? "story_storm" : c == 5 ? "story_storm" : c == 6 ? "story_faith" : "story";
        if (c == 7 && s < 2) MarsArrive(false);
        if (c != 7) foreach (var f in Humans()) if (f.world == WorldId.Mars) f.SendTo(WorldId.Ranch, S.GatherPos + Vector3.right * f.id * 1.5f + Vector3.up * 0.4f, 180f);
        if (c >= 3 && c != 7) PlaceTeamAtPad();
        switch (c)
        {
            case 1: if (s == 0) Play(IntroCo()); break;
            case 2: if (s == 0) Save(); break;
            case 3: Save(); break;
            case 4: if (s == 0) { Save(); Play(StormRollCo()); } else StormBegin(); break;
            case 5: Save(); break;
            case 6: Save(); if (s == 0) Play(EncourageCo()); break;
            case 7: Save(); break;
            case 8: Save(); break;
        }
        if (demo) DemoAfterStart();
    }

    void ResetParts(int mask, bool live)
    {
        parts = mask;
        for (int i = 0; i < 5; i++) { state[i] = (mask & (1 << i)) != 0 ? 2 : 0; carrier[i] = null; DropIcon(i); }
        engineOn = null;
        if (S.engineCrate.parent != S.root) { S.engineCrate.SetParent(S.root, true); }
        S.engineCrate.position = S.EnginePos; S.engineCrate.rotation = Quaternion.identity;
        Pickups.Reset("st_fuel"); Pickups.Reset("st_fin");
        fuelRunT = -1f; fuelFails = 0;
        S.fuel[1].basePos = S.fuel[1].t.position = FuelTopPos();
        simonState = 0; simonRound = 0; noseRevealed = false;
        S.noseItem.taken = true; S.noseItem.t.gameObject.SetActive(false);
        S.noseChestLid.localRotation = Quaternion.identity;
        chipOpen = false; chipHelpers.Clear(); chipWait = 0f;
        S.chipItem.taken = true; S.chipItem.t.gameObject.SetActive(false);
        S.chipVaultLid.localPosition = new Vector3(0f, 1.4f, 0f); S.chipVaultLid.localRotation = Quaternion.identity;
        if (state[0] == 2 || !live) S.engineCrate.gameObject.SetActive(false);
        if (state[1] == 2 || !live) foreach (var it in S.fuel) { it.taken = true; it.t.gameObject.SetActive(false); }
        if (state[3] == 2 || !live) foreach (var it in S.fins) { it.taken = true; it.t.gameObject.SetActive(false); }
    }
    Vector3 FuelTopPos() { if (RallyTrack.BranchArcEnd > 1f) { Vector3 f, u; return RallyTrack.BranchFrameAt((RallyTrack.LoopArcA + RallyTrack.LoopArcB) * 0.5f, out f, out u) + u * 1.3f; } float lgy = Ranch.GY(Layout.LoopC.x, Layout.LoopC.y); return new Vector3(Layout.LoopC.x, lgy + 2f * Layout.LoopR - 1.6f, Layout.LoopC.y + Layout.LoopShift * 0.5f); }

    bool tankBurst, finsSnapped, fixedFins, fixedTank, fixedChip;
    void ApplyRocket()
    {
        tankBurst = ch == 3 && step == 1;
        finsSnapped = (ch == 5) || (ch == 6 && !fixedFins);
        S.rocket.Show(parts, ch <= 2, tankBurst, ch > 3 || (ch == 3 && step >= 2), finsSnapped ? 2 : 0);
        foreach (var a in S.anchors) a.gameObject.SetActive(ch == 4 && step == 1);
    }

    void RocketHome()
    {
        S.rocket.root.position = StorySet.Pad + Vector3.up * StorySet.CradleH;
        S.rocket.root.rotation = Quaternion.identity;
        StorySet.Flame(S.rocket, 0f, 0f, 0f);
    }

    void PlaceTeamAtPad()
    {
        int k = 0;
        foreach (var f in Humans())
        {
            if (f.world != WorldId.Ranch || (f.FocusPoint - StorySet.Pad).magnitude > 60f)
            {
                if (f.vehicle != null) f.ExitVehicle();
                Vector3 p = StorySet.Pad + new Vector3(-3f + k * 2f, 0f, 14f); p.y = Ranch.GY(p.x, p.z) + 0.3f;
                f.SendTo(WorldId.Ranch, p, 180f);
            }
            k++;
        }
    }

    // ---------------- per frame ----------------
    void Update()
    {
        if (!Active || G == null || G.state != Game.State.Play) return;
        float dt = Time.deltaTime;
        chTime += dt;
        hintT -= dt;
        PollRawInput();
        runner.Tick();
        if (!Active) return;   // ffu22 fix: the ending (Stop2) ran inside the tick
        if (!runner.Busy && pendCh > 0) { int pc = pendCh, ps = pendStep; pendCh = -1; StartChapter(pc, ps); }
        if (!runner.Busy && ch <= 7)
        {
            switch (ch)
            {
                case 1: Ch1(dt); break;
                case 2: Ch2(dt); break;
                case 3: Ch3(dt); break;
                case 4: Ch4(dt); break;
                case 5: Ch5(dt); break;
                case 6: Ch6(dt); break;
                case 7: Ch7(dt); break;
            }
        }
        if (ch == 4 && step == 1 && !runner.Busy) StormTick(dt);
        Weather(dt);
        if (demo) DemoTick(dt);
        UpdateIcons();
        ui.Tick(cine, cine && skippable, SkipHint());
        // objective visibility: gameplay only, dimmed at the low point
        ui.ObjectivesVisible(!cine && !modalHidesObj, ch == 6 && step == 0 ? 0.35f : 1f);
        S.Beams(BeamSpots(), WorldId.Ranch);
    }

    // ---------------- chapter 1: a call from Mars ----------------
    void Ch1(float dt)
    {
        if (step == 0) { step = 1; }
        SetObj("CHAPTER 1 · " + ChTitle[1].ToUpper(), "Go to the launch pad south of the house", null);
        foreach (var f in Humans())
            if (f.world == WorldId.Ranch && Flat(f.FocusPoint - StorySet.Pad) < 14f) { Play(BlueprintCo()); return; }
    }

    // ---------------- chapter 2: gathering the parts ----------------
    float fuelRunT = -1f; int fuelFails;
    int simonState, simonRound, simonIdx, simonLastPad = -1; float simonT; readonly List<int> simonSeq = new List<int>(); bool noseRevealed;
    bool chipOpen, chipNear; float chipWait, chipCall; readonly HashSet<Robot> chipHelpers = new HashSet<Robot>();

    void Ch2(float dt)
    {
        // engine: loaded onto any vehicle (not a mech / Starship) that drives up to it
        if (state[0] == 0)
        {
            foreach (var f in Humans())
            {
                if (f.world != WorldId.Ranch) continue;
                float d = Flat(f.FocusPoint - S.EnginePos);
                if (f.vehicle != null && !(f.vehicle is StoryMech) && !(f.vehicle is Starship) && !(f.vehicle is Boat) && d < 7.5f) { LoadEngine(f, f.vehicle); break; }
                if (f.vehicle == null && d < 5f && hintT <= 0f) { f.Toast("Too heavy to carry! Bring a truck - the Monster Truck is in the garage", 3.5f); hintT = 5f; }
            }
        }
        else if (state[0] == 1 && engineOn != null)
        {
            if (engineOn.driver != null) carrier[0] = engineOn.driver;
            if (Flat(engineOn.transform.position - StorySet.Pad) < 15f) Deliver(0, S.engineCrate.position);
        }
        // fuel run timer: 25 s from the first canister
        int fuelLeft = 0; foreach (var it in S.fuel) if (!it.taken) fuelLeft++;
        if (state[1] == 0 && fuelRunT >= 0f)
        {
            fuelRunT -= dt;
            if (fuelLeft == 0) { fuelRunT = -1f; }
            else if (fuelRunT < 0f)
            {
                fuelFails++;
                Pickups.Reset("st_fuel");
                if (fuelFails >= 3) { Vector3 g = S.fuel[1].basePos; g.y = Ranch.GY(g.x, g.z) + 1.3f; S.fuel[1].basePos = g; S.fuel[1].t.position = g; }
                ToastAll(fuelFails >= 3 ? "Too slow - but the top canister fell down to the ground. Grab all three again!" : "Too slow! The canisters rolled back. Try again - drive fast through the loop!", 4f);
                Sfx.Play(Sfx.Bonk != null ? Sfx.Bonk : Sfx.Click, 0.7f, 0.8f);
            }
        }
        SimonTick(dt);
        ChipTick(dt);
        // carried parts are delivered at the pad
        for (int i = 1; i < 5; i++)
            if (state[i] == 1 && carrier[i] != null && carrier[i].world == WorldId.Ranch && Flat(carrier[i].FocusPoint - StorySet.Pad) < 14f)
                Deliver(i, carrier[i].FocusPoint + Vector3.up * 2.4f);
        // objective text + checklist
        int fins = 6 - Pickups.Remaining("st_fin"), fuelGot = 3 - fuelLeft;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 5; i++)
        {
            string n = PartName[i];
            if (i == 1 && state[i] == 0) n += " " + fuelGot + "/3";
            if (i == 3 && state[i] == 0) n += " " + fins + "/6";
            string col = state[i] == 2 ? "#7dff8a" : state[i] == 1 ? "#ffd84a" : "#ffffffaa";
            sb.Append("<color=" + col + ">" + (state[i] == 2 ? "+ " : state[i] == 1 ? "> " : "") + n + "</color>");
            if (i < 4) sb.Append("   ");
        }
        string txt = AnyCarried() ? "Bring the part" + (CarriedCount() > 1 ? "s" : "") + " to the launch pad!" : "Find the rocket parts around the ranch, house and track";
        if (fuelRunT >= 0f) SetStatus("<b>FUEL RUN</b>  " + Mathf.CeilToInt(fuelRunT) + " s  ·  " + fuelGot + " / 3 canisters"); else if (simonState > 0 && simonState < 4) SetStatus(simonState == 1 ? "Watch the floor lights..." : "Hop on the lights in the same order!  " + simonIdx + " / " + simonSeq.Count); else if (chipNear && !chipOpen && state[4] == 0) SetStatus("<b>CHIP VAULT</b>  robots here: " + Mathf.Min(2, chipHelpers.Count) + " / 2  ·  phone: Come here"); else SetStatus("");
        SetObj("CHAPTER 2 · " + ChTitle[2].ToUpper() + "   " + Bits(parts) + " / 5", txt, sb.ToString());
        if (parts == 31) { SetStatus(""); Play(AssemblyCo()); }
    }

    int Bits(int m) { int n = 0; for (int i = 0; i < 5; i++) if ((m & (1 << i)) != 0) n++; return n; }
    bool AnyCarried() { foreach (var f in Humans()) for (int i = 0; i < 5; i++) if (state[i] == 1 && carrier[i] == f) return true; return false; }
    int CarriedCount() { int n = 0; for (int i = 0; i < 5; i++) if (state[i] == 1) n++; return n; }

    void LoadEngine(Frog f, Vehicle v)
    {
        state[0] = 1; carrier[0] = f; engineOn = v;
        S.engineCrate.SetParent(v.transform, true);
        float top = 1.8f;
        foreach (var r in v.GetComponentsInChildren<Renderer>()) if (r.enabled && !(r is ParticleSystemRenderer)) top = Mathf.Max(top, r.bounds.max.y - v.transform.position.y);
        S.engineCrate.localPosition = v.transform.InverseTransformVector(Vector3.up * (top + 0.05f));
        S.engineCrate.localRotation = Quaternion.identity;
        FX.Sparkle(S.engineCrate.position + Vector3.up, new Color(1f, 0.85f, 0.4f), 24);
        Sfx.Play(Sfx.Clank != null ? Sfx.Clank : Sfx.Pickup, 0.9f);
        ToastAll("Engine loaded on the " + v.Title + "! Drive it to the launch pad", 3.5f);
        Debug.Log("FFSTORY engine loaded on " + v.Title);
    }

    // a part arrives at the pad: it flies from the carrier into its slot on the rocket
    void Deliver(int i, Vector3 from)
    {
        state[i] = 2; parts |= 1 << i;
        if (i == 0) { S.engineCrate.SetParent(S.root, true); S.engineCrate.gameObject.SetActive(false); S.engineSpot.gameObject.SetActive(false); engineOn = null; }
        DropIcon(i);
        carrier[i] = null;
        StartCoroutine(FlyIn(i, from));
        ToastAll(PartName[i] + " delivered!  " + Bits(parts) + " / 5", 3f);
        Debug.Log("FFSTORY delivered " + PartName[i] + " mask=" + parts);
        Save();
    }

    IEnumerator FlyIn(int i, Vector3 from)
    {
        S.rocket.Show(parts & ~(1 << i), true, false, false, 0);
        Vector3 to = S.rocket.SlotWorld(i);
        var g = new GameObject("FlyIn").transform;
        g.position = from;
        var vis = PartIcon(i, g, 1.4f);
        float t = 0f, dur = 1.5f;
        Vector3 mid = (from + to) * 0.5f + Vector3.up * 8f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / dur);
            g.position = Vector3.Lerp(Vector3.Lerp(from, mid, k), Vector3.Lerp(mid, to, k), k);
            g.Rotate(0f, 360f * Time.deltaTime, 0f);
            if (Random.value < 0.5f) FX.Sparkle(g.position, new Color(0.5f, 0.9f, 1f), 1);
            yield return null;
        }
        Destroy(g.gameObject);
        ApplyRocket();
        FX.Sparkle(to, new Color(1f, 0.9f, 0.4f), 30);
        Sfx.Play(Sfx.Clank != null ? Sfx.Clank : Sfx.Pickup, 1f, 1.1f);
        Sfx.Play(Sfx.Win, 0.35f, 1.4f);
    }

    Transform PartIcon(int i, Transform parent, float s)
    {
        Transform t;
        switch (i)
        {
            case 0: t = StorySet.EngineModel(parent, new Vector3(0f, -0.8f * s, 0f), 0.3f * s).transform; break;
            default:
                {
                    var src = i == 1 ? S.fuel[0].t : i == 2 ? S.noseItem.t : i == 3 ? S.fins[0].t : S.chipItem.t;
                    var c = Instantiate(src.gameObject, parent);
                    c.SetActive(true);
                    foreach (var rr in c.GetComponentsInChildren<Renderer>()) if (rr.sharedMaterial == StorySet.GlowMat()) Destroy(rr.gameObject);
                    c.transform.localPosition = Vector3.zero; c.transform.localRotation = Quaternion.identity; c.transform.localScale = Vector3.one * s;
                    foreach (var col in c.GetComponentsInChildren<Collider>()) Destroy(col);
                    t = c.transform; break;
                }
        }
        return t;
    }

    void Carry(int i, Frog f)
    {
        state[i] = 1; carrier[i] = f;
        DropIcon(i);
        var holder = new GameObject("Carry" + i).transform;
        holder.SetParent(f.transform, false);
        PartIcon(i, holder, 0.75f);
        icon[i] = holder;
        Debug.Log("FFSTORY carrying " + PartName[i] + " by " + f.nick);
    }
    void DropIcon(int i) { if (icon[i] != null) { Destroy(icon[i].gameObject); icon[i] = null; } }
    void UpdateIcons()
    {
        int n = 0;
        for (int i = 0; i < 5; i++)
        {
            if (icon[i] == null) continue;
            icon[i].localPosition = new Vector3((n - 0.5f) * 0.8f, 2.3f + Mathf.Sin(Time.time * 3f + i) * 0.12f, 0f);
            icon[i].Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
            n++;
        }
    }

    void OnPickup(Pickups.Item it, Frog f)
    {
        if (!Active || ch != 2) return;
        switch (it.group)
        {
            case "st_fuel":
                {
                    int left = Pickups.Remaining("st_fuel");
                    if (left == 2 && fuelRunT < 0f) { fuelRunT = 25f; ToastAll("FUEL RUN! 25 seconds for all three canisters - one is at the top of the loop!", 3.5f); }
                    else if (left > 0) f.Toast("Fuel canister! " + (3 - left) + " / 3", 2f);
                    if (left == 0) { fuelRunT = -1f; Carry(1, f); ToastAll("All three fuel tanks! Bring them to the launch pad", 3.5f); Sfx.Play(Sfx.Win, 0.6f, 1.2f); }
                    break;
                }
            case "st_fin":
                {
                    int left = Pickups.Remaining("st_fin");
                    if (left > 0) f.Toast("A fin! " + (6 - left) + " / 6" + (left <= 2 ? "  (spares never hurt!)" : ""), 2.2f);
                    else { Carry(3, f); ToastAll("All six fins - four for the rocket and two spares! Bring them to the pad", 3.8f); Sfx.Play(Sfx.Win, 0.6f, 1.2f); }
                    break;
                }
            case "st_nose": Carry(2, f); f.Toast("The nose cone! Bring it to the launch pad", 3f); break;
            case "st_chip": Carry(4, f); f.Toast("The computer chip! Bring it to the launch pad", 3f); break;
        }
    }

    // house puzzle: four floor lights flash a sequence, hop on them in the same order (3, then 4 long)
    void SimonTick(float dt)
    {
        if (state[2] != 0 || noseRevealed) return;
        Frog inHouse = null;
        foreach (var f in Humans()) if (f.world == WorldId.House && Flat(f.transform.position - S.SimonC) < 9f) { inHouse = f; break; }
        if (inHouse == null) { if (simonState != 0) { simonState = 0; GlowAll(false); } return; }
        simonT += dt;
        if (simonState == 0)
        {
            simonState = 1; simonRound = Mathf.Max(simonRound, 0); simonT = -1.2f;
            simonSeq.Clear(); int len = 3 + simonRound; for (int k = 0; k < len; k++) simonSeq.Add(Random.Range(0, 4));
            inHouse.Toast("The nose cone is locked in the chest! Watch the floor lights, then hop on them in the same order", 4.5f);
        }
        if (simonState == 1)
        {
            int idx = Mathf.FloorToInt(simonT / 0.75f);
            bool lit = simonT >= 0f && Mathf.Repeat(simonT, 0.75f) < 0.5f && idx < simonSeq.Count;
            for (int k = 0; k < 4; k++) S.simonGlow[k].enabled = lit && simonSeq[Mathf.Clamp(idx, 0, simonSeq.Count - 1)] == k;
            if (lit && Mathf.Repeat(simonT, 0.75f) < dt * 1.5f) Sfx.Play(Sfx.Select != null ? Sfx.Select : Sfx.Click, 0.6f, 1f + simonSeq[idx] * 0.15f);
            if (idx >= simonSeq.Count) { simonState = 2; simonIdx = 0; simonLastPad = PadUnder(); GlowAll(false); }
            return;
        }
        if (simonState == 2)
        {
            int pad = PadUnder();
            if (pad != simonLastPad && pad >= 0)
            {
                bool ok = simonSeq[simonIdx] == pad;
                for (int k = 0; k < 4; k++) S.simonGlow[k].enabled = k == pad;
                if (ok)
                {
                    simonIdx++;
                    Sfx.Play(Sfx.Select != null ? Sfx.Select : Sfx.Click, 0.7f, 1f + pad * 0.15f);
                    if (simonIdx >= simonSeq.Count)
                    {
                        simonRound++;
                        if (simonRound >= 2) { simonState = 4; StartCoroutine(OpenChest()); }
                        else { simonState = 0; ToastAll("Nice! One more - a longer one", 2.5f); Sfx.Play(Sfx.Win, 0.4f, 1.3f); }
                    }
                }
                else
                {
                    simonState = 1; simonT = -1.2f;
                    Sfx.Play(Sfx.Bonk != null ? Sfx.Bonk : Sfx.Click, 0.7f, 0.7f);
                    ToastAll("Oops! Watch again...", 2f);
                }
            }
            if (pad < 0 && simonState == 2) GlowAll(false);
            simonLastPad = pad;
        }
    }
    int PadUnder()
    {
        foreach (var f in Humans())
        {
            if (f.world != WorldId.House) continue;
            for (int k = 0; k < 4; k++) if (Flat(f.transform.position - S.simonPads[k]) < 0.95f && f.transform.position.y < S.simonPads[k].y + 0.6f) return k;
        }
        return -1;
    }
    void GlowAll(bool on) { for (int k = 0; k < 4; k++) S.simonGlow[k].enabled = on; }
    IEnumerator OpenChest()
    {
        GlowAll(true);
        Sfx.Play(Sfx.Win, 0.7f, 1.1f);
        ToastAll("Click! The chest is open!", 2.5f);
        float t = 0f;
        while (t < 1f) { t += Time.deltaTime; S.noseChestLid.localRotation = Quaternion.Euler(-100f * Mathf.SmoothStep(0f, 1f, t), 0f, 0f); yield return null; }
        GlowAll(false);
        noseRevealed = true;
        S.noseItem.taken = false; S.noseItem.t.gameObject.SetActive(true);
        FX.Sparkle(S.noseItem.basePos, new Color(1f, 0.4f, 0.3f), 30);
    }

    // chip vault: two robots must come and lift the lid (robot phone "Come here", or drive one there)
    void ChipTick(float dt)
    {
        if (state[4] != 0 || chipOpen || RanchLife.I == null) return;
        Frog near = null;
        foreach (var f in Humans()) if (f.world == WorldId.Ranch && Flat(f.FocusPoint - S.ChipVaultPos) < 16f) { near = f; break; }
        // only robots that came because they were called (phone "Come here" / "Follow me") or are driven count
        foreach (var r in RanchLife.I.robots) if (r != null && (r.cmd == "come" || r.cmd == "follow" || r.cmd == "idle" || r.manual != null) && Flat(r.transform.position - S.ChipVaultPos) < 5.5f) chipHelpers.Add(r);
        chipNear = near != null;
        if (near != null)
        {
            if (chipWait <= 0f) near.Toast("The chip vault lid is too heavy! Open the robot phone (LB / P / PHONE) and order two robots: Come here", 5f);
            chipWait += dt;
            chipCall -= dt;
            if (chipWait > 40f && chipCall <= 0f)
            {
                chipCall = 9f;
                var list = new List<Robot>(RanchLife.I.robots);
                list.Sort((a, b) => Flat(a.transform.position - S.ChipVaultPos).CompareTo(Flat(b.transform.position - S.ChipVaultPos)));
                int sent = 0;
                foreach (var r in list) { if (chipHelpers.Contains(r) || r.manual != null) continue; r.Order("follow", near); r.follow = S.chipVault; if (++sent >= 2 - chipHelpers.Count) break; }
                if (chipWait < 50f) ToastAll("The robots heard you calling - help is on the way!", 3f);
            }
        }
        if (chipHelpers.Count >= 2) StartCoroutine(OpenVault());
    }
    void SetStatusIfFree(string s) { if (s != null && fuelRunT < 0f && (simonState == 0 || simonState >= 4)) SetStatus(s); }
    IEnumerator OpenVault()
    {
        chipOpen = true;
        foreach (var r in chipHelpers) if (r != null && r.manual == null) r.Order("wave", null);
        ToastAll("Heave-ho! The robots lift the lid!", 2.5f);
        Sfx.Play(Sfx.Clank != null ? Sfx.Clank : Sfx.Pickup, 0.9f, 0.8f);
        float t = 0f;
        while (t < 1.4f) { t += Time.deltaTime; float k = Mathf.SmoothStep(0f, 1f, t / 1.4f); S.chipVaultLid.localPosition = new Vector3(0f, 1.4f + 1.4f * k, -0.6f * k); S.chipVaultLid.localRotation = Quaternion.Euler(-35f * k, 0f, 0f); yield return null; }
        S.chipItem.taken = false; S.chipItem.t.gameObject.SetActive(true);
        FX.Sparkle(S.chipItem.basePos, new Color(0.4f, 1f, 0.95f), 30);
        Sfx.Play(Sfx.Win, 0.5f, 1.3f);
    }

    // ---------------- chapter 3: test fire + repair ----------------
    void Ch3(float dt)
    {
        if (step == 0) SetObj("CHAPTER 3 · " + ChTitle[3].ToUpper(), "Start the test fire at the control panel", null);
        else if (step == 1) SetObj("CHAPTER 3 · " + ChTitle[3].ToUpper(), "Repair the burst fuel tank (A at the rocket)", null);
        if (step == 1 && Random.value < 0.3f) FX.Smoke(S.rocket.root.TransformPoint(new Vector3(2.1f, 3.6f, 0.3f)), Random.Range(1f, 2f), new Color(0.3f, 0.3f, 0.32f, 0.6f));
        SetStatus("");
    }

    // ---------------- chapter 4: the storm ----------------
    float stormT, debrisT, anchorT, boltT2; int saved, bumps; bool fenceDone;
    readonly float[] looseT = new float[3];
    readonly bool[] loose = new bool[3];
    readonly HashSet<Rigidbody> deflected = new HashSet<Rigidbody>();
    const float StormLen = 70f;
    Vector3 Wind { get { return new Vector3(1f, 0f, 0.3f).normalized; } }

    void Ch4(float dt)
    {
        if (step != 1) return;
        SetObj("CHAPTER 4 · " + ChTitle[4].ToUpper(), "Protect the rocket! Bump away flying junk and re-tie loose ropes (A)", null);
    }

    void StormBegin()
    {
        step = 1; stormT = 0f; debrisT = 2f; anchorT = 8f; boltT2 = 3f; saved = 0; bumps = 0; fenceDone = false;
        for (int i = 0; i < 3; i++) { loose[i] = false; looseT[i] = 0f; }
        deflected.Clear();
        Worlds.StormK = 1f;
        ApplyRocket();
        S.BuildStormFence();
        if (RanchLife.I != null) { int n = 0; foreach (var r in RanchLife.I.robots) { if (r.manual != null) continue; r.Order("come", Lead); if (++n >= 3) break; } }
        Sfx.Override = "story_storm";
    }

    void StormTick(float dt)
    {
        stormT += dt;
        Vector3 W = Wind, P = StorySet.Pad;
        debrisT -= dt;
        if (debrisT <= 0f && stormT < StormLen - 4f)
        {
            debrisT = Random.Range(2.6f, 4.2f) * (Look.Mobile ? 1.2f : 1f);
            Vector3 side = new Vector3(-W.z, 0f, W.x);
            Vector3 s = P - W * 36f + side * Random.Range(-12f, 12f);
            s.y = Ranch.GY(s.x, s.z) + 1.2f;
            S.SpawnDebris(s);
        }
        for (int i = S.debris.Count - 1; i >= 0; i--)
        {
            var rb = S.debris[i];
            if (rb == null) { S.debris.RemoveAt(i); continue; }
            Vector3 p = rb.position;
            if (!deflected.Contains(rb))
            {
                Vector3 to = P - p; to.y = 0f;
                Vector3 want = (W * 0.45f + to.normalized * 0.8f).normalized * 6.5f, v = rb.velocity; v.y = 0f;
                rb.AddForce((want - v) * 2.5f, ForceMode.Acceleration);
                if (rb.angularVelocity.sqrMagnitude < 4f) rb.AddTorque(Vector3.Cross(Vector3.up, want) * 0.6f, ForceMode.Acceleration);
                // froggies, their vehicles and the robots bump junk away
                bool hit = false; Vector3 from = p;
                foreach (var f in G.frogs)
                {
                    if (f.world != WorldId.Ranch) continue;
                    float r = f.vehicle != null ? 4f : 2.4f;
                    if (Flat(f.FocusPoint - p) < r) { hit = true; from = f.FocusPoint; if (f.human) f.Toast("Bonk! Junk saved", 1.2f); break; }
                }
                if (!hit && RanchLife.I != null) foreach (var r in RanchLife.I.robots) if (r != null && Flat(r.transform.position - p) < 2.2f) { hit = true; from = r.transform.position; break; }
                if (hit)
                {
                    deflected.Add(rb); saved++;
                    Vector3 away = p - from; away.y = 0f; if (away.sqrMagnitude < 0.01f) away = -W;
                    rb.velocity = away.normalized * 13f + Vector3.up * 7f;
                    rb.AddTorque(Random.insideUnitSphere * 30f, ForceMode.VelocityChange);
                    Sfx.PlayAt(Sfx.Bonk != null ? Sfx.Bonk : Sfx.Thud, p, 0.8f, 60f, Random.Range(0.9f, 1.2f));
                    FX.Dust(p, 1f); FX.Sparkle(p, Color.white, 6);
                }
                else if (Flat(p - P) < 4.5f)
                {
                    bumps++;
                    FX.Boom(p, 0.4f); Game.Shake(p, 0.35f);
                    Sfx.PlayAt(Sfx.Thud != null ? Sfx.Thud : Sfx.Boom, p, 1f, 80f, 0.8f);
                    S.debris.RemoveAt(i); Destroy(rb.gameObject); continue;
                }
            }
            if (Flat(p - P) > 90f || p.y < -20f) { S.debris.RemoveAt(i); Destroy(rb.gameObject); }
        }
        // ropes come loose; a loose rope for 10 s costs a bump
        anchorT -= dt;
        if (anchorT <= 0f && stormT < StormLen - 6f)
        {
            anchorT = Random.Range(8f, 12f);
            int k = Random.Range(0, 3);
            if (!loose[k]) { loose[k] = true; looseT[k] = 0f; ToastAll("A rope came loose! Re-tie it (A)", 2.5f); Sfx.Play(Sfx.Clank != null ? Sfx.Clank : Sfx.Click, 0.7f, 0.7f); }
        }
        for (int k = 0; k < 3; k++)
        {
            S.anchorLights[k].sharedMaterial = loose[k] ? Mats.Unlit(new Color(1f, 0.2f, 0.15f)) : Mats.Unlit(new Color(0.35f, 1f, 0.4f));
            var lr = S.anchors[k].GetComponent<LineRenderer>();
            if (lr != null) lr.enabled = !loose[k] || Mathf.Repeat(Time.time * 6f, 1f) < 0.5f;
            if (!loose[k]) continue;
            looseT[k] += dt;
            if (looseT[k] > 10f) { looseT[k] = 0f; bumps++; Game.Shake(StorySet.Pad, 0.3f); }
        }
        // lightning all around; one strike hits the fence by the pad
        boltT2 -= dt;
        if (boltT2 <= 0f)
        {
            boltT2 = Random.Range(3.5f, 6.5f);
            float a = Random.value * Mathf.PI * 2f, d = Random.Range(35f, 90f);
            Vector3 q = P + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d); q.y = Ranch.GY(q.x, q.z);
            S.Strike(q);
        }
        if (!fenceDone && stormT > 28f)
        {
            fenceDone = true;
            Vector3 fp = StorySet.Pad + new Vector3(-15f, 0f, -6f); fp.y = Ranch.GY(fp.x, fp.z);
            S.Strike(fp); S.SmashFence(fp + Vector3.up * 0.5f);
            ToastAll("Lightning! The old fence is smashed - keep that junk away from the rocket!", 3f);
        }
        if (RanchLife.I != null && Mathf.Repeat(stormT, 10f) < dt) { int n = 0; foreach (var r in RanchLife.I.robots) { if (r.manual != null) continue; r.Order("come", Lead); if (++n >= 3) break; } }
        int left = Mathf.Max(0, Mathf.CeilToInt(StormLen - stormT));
        SetStatus("<b>HOLD OUT</b>  " + (left / 60) + ":" + (left % 60).ToString("00") + "   ·   junk saved " + saved + "   ·   bumps " + bumps);
        if (stormT >= StormLen) { SetStatus(""); Play(StormDamageCo()); }
    }

    public void TieRope(int k) { if (k >= 0 && k < 3 && loose[k]) { loose[k] = false; looseT[k] = 0f; saved++; Sfx.Play(Sfx.Confirm != null ? Sfx.Confirm : Sfx.Pickup, 0.8f); FX.Sparkle(S.anchors[k].position + Vector3.up, new Color(0.4f, 1f, 0.5f), 12); } }
    public bool RopeLoose(int k) { return Active && ch == 4 && step == 1 && !runner.Busy && k >= 0 && k < 3 && loose[k]; }

    // ---------------- chapter 5 / 6 ----------------
    void Ch5(float dt) { SetObj("CHAPTER 5 · " + ChTitle[5].ToUpper(), "Launch the rocket at the control panel!", null); SetStatus(""); }

    void Ch6(float dt)
    {
        if (step == 1)
        {
            string l = (fixedFins ? "<color=#7dff8a>+ SPARE FINS</color>" : "SPARE FINS") + "   " + (fixedTank ? "<color=#7dff8a>+ PATCHED TANK</color>" : "PATCHED TANK") + "   " + (fixedChip ? "<color=#7dff8a>+ CHIP REBOOT</color>" : "CHIP REBOOT");
            SetObj("CHAPTER 6 · " + ChTitle[6].ToUpper(), "Everything you worked for is still here - fix her up!", l);
            if (fixedFins && fixedTank && fixedChip) { step = 2; Save(); ToastAll("She's ready. One more try!", 3f); }
        }
        else if (step == 2) SetObj("CHAPTER 6 · " + ChTitle[6].ToUpper(), "Relaunch at the control panel - have faith!", null);
        SetStatus("");
    }

    // ---------------- chapter 7: Mars ----------------
    int rocksLeft = 3; readonly bool[] pupFound = new bool[4];
    void Ch7(float dt)
    {
        // somebody flew off in the Mars Starship: the story's rocket is right here, the pups are waiting
        foreach (var f in Humans())
            if (f.world == WorldId.Space || f.world == WorldId.Callisto || f.world == WorldId.Ranch)
            {
                if (f.vehicle != null) f.ExitVehicle();
                f.LeavePassenger();
                Vector3 p = S.MarsRocketPos + new Vector3(4f, 0.5f, 5f);
                p.y = Worlds.MarsO.y + SurfaceWorlds.MarsY(p.x - Worlds.MarsO.x, p.z - Worlds.MarsO.z) + 0.4f;
                f.SendTo(WorldId.Mars, p, 0f);
                f.Toast("Our own rocket is right here - the pups are waiting in the cave!", 3.5f);
            }
        if (step == 0)
        {
            SetObj("CHAPTER 7 · " + ChTitle[7].ToUpper(), rocksLeft > 0 ? "Clear the rockfall at the cave mouth (A)  " + (3 - rocksLeft) + " / 3" : "Find the Mars pups deep in the cave", null);
            for (int k = 0; k < S.pups.Count; k++)
            {
                var p = S.pups[k];
                if (pupFound[k] || rocksLeft > 0) continue;
                foreach (var f in Humans())
                    if (f.world == WorldId.Mars && Flat(f.transform.position - p.transform.position) < 3.2f)
                    {
                        pupFound[k] = true; p.follow = f.transform; p.followDist = 1.5f + k * 0.5f;
                        p.area = new Rect(Worlds.MarsO.x - 95f, Worlds.MarsO.z - 95f, 190f, 190f);
                        FX.Sparkle(p.transform.position + Vector3.up, new Color(1f, 0.9f, 0.4f), 16);
                        Sfx.Play(Sfx.Bark != null ? Sfx.Bark : Sfx.Pickup, 0.8f, 1.5f);
                        int n = 0; foreach (bool b in pupFound) if (b) n++;
                        ToastAll(n < 4 ? "A pup! " + n + " / 4 - they're so happy to see you!" : "All four pups! Lead them back to the rocket", 3f);
                        break;
                    }
            }
            int found = 0; foreach (bool b in pupFound) if (b) found++;
            if (found >= 4) { step = 1; }
            SetStatus(rocksLeft == 0 ? "<b>PUPS</b>  " + found + " / 4" : "");
        }
        else if (step == 1)
        {
            SetObj("CHAPTER 7 · " + ChTitle[7].ToUpper(), "Lead the pups back to the rocket", null);
            bool all = true;
            foreach (var p in S.pups) if (Flat(p.transform.position - S.MarsRocketPos) > 11f) all = false;
            bool frog = false; foreach (var f in Humans()) if (f.world == WorldId.Mars && Flat(f.transform.position - S.MarsRocketPos) < 10f) frog = true;
            SetStatus("");
            if (all && frog) Play(HomeCo());
        }
    }

    public void ClearRock()
    {
        if (rocksLeft <= 0) return;
        rocksLeft--;
        Transform r = S.rocks[rocksLeft];
        Vector3 c = r.position;
        r.gameObject.SetActive(false);
        // destruction: the boulder breaks into tumbling chunks
        for (int i = 0; i < 6; i++)
        {
            var g = Mats.Prim(PrimitiveType.Sphere, null, c + Random.insideUnitSphere * 0.8f, Vector3.one * Random.Range(0.5f, 1f), Mats.Lit(new Color(0.48f, 0.26f, 0.16f)), true);
            var rb = g.AddComponent<Rigidbody>(); rb.mass = 5f;
            rb.velocity = (Random.insideUnitSphere + Vector3.up) * 5f;
            Destroy(g, 6f);
        }
        FX.Boom(c, 0.6f); FX.Dust(c, 1f); Game.Shake(c, 0.4f);
        Sfx.Play(Sfx.Boom, 0.7f, 0.8f);
        if (rocksLeft == 0) ToastAll("The way is clear! Find the pups at the back of the cave", 3f);
    }

    void MarsArrive(bool landedNow)
    {
        S.BuildMars();
        S.MarsActive(true);
        rocksLeft = 3; for (int k = 0; k < 4; k++) pupFound[k] = false;
        foreach (var r in S.rocks) r.gameObject.SetActive(true);
        int i = 0;
        foreach (var f in Humans())
        {
            if (f.vehicle != null) f.ExitVehicle();
            SurfaceWorlds.LandMars(f, i);
            Vector3 p = S.MarsRocketPos + new Vector3(4f + i * 1.6f, 0.5f, 5f);
            p.y = Worlds.MarsO.y + SurfaceWorlds.MarsY(p.x - Worlds.MarsO.x, p.z - Worlds.MarsO.z) + 0.4f;
            f.SendTo(WorldId.Mars, p, 0f);
            f.Toast("", 0.01f);
            i++;
        }
        for (int k = 0; k < S.pups.Count; k++)
        {
            var p = S.pups[k]; p.follow = null;
            Vector3[] spots = { new Vector3(-4f, 0f, 76f), new Vector3(-1.5f, 0f, 78f), new Vector3(1.5f, 0f, 78f), new Vector3(4f, 0f, 76f) };
            p.transform.position = SurfaceWorlds.M(spots[k].x, SurfaceWorlds.MarsY(spots[k].x, spots[k].z), spots[k].z);
            p.area = new Rect(Worlds.MarsO.x + spots[k].x - 1.5f, Worlds.MarsO.z + spots[k].z - 1.5f, 3f, 3f);
        }
    }

    // ---------------- interactions (hotspots) ----------------
    bool hooked;
    void HookOnce()
    {
        if (hooked) return;
        hooked = true;
        Pickups.Listen(OnPickup);
        var panel = Interact.Add(S.PanelPos, 2.8f, "", f => PanelPress(f));
        panel.pos.y = Ranch.GY(S.PanelPos.x, S.PanelPos.z) + 0.2f;
        panel.enabled = f => Active && f.world == WorldId.Ranch && !runner.Busy && ((ch == 3 && step == 0) || ch == 5 || (ch == 6 && (step == 1 && !fixedChip || step == 2)));
        panel.dynLabel = f => ch == 3 ? "start the engine test fire" : ch == 5 ? "LAUNCH!" : ch == 6 && step == 1 ? "reboot the chip (backup flight plan)" : "RELAUNCH - have faith!";
        var tank = Interact.Add(new Vector3(S.TankFixPos.x, Ranch.GY(S.TankFixPos.x, S.TankFixPos.z) + 0.2f, S.TankFixPos.z), 3.2f, "", f => TankPress(f));
        tank.enabled = f => Active && f.world == WorldId.Ranch && !runner.Busy && ((ch == 3 && step == 1) || (ch == 6 && step == 1 && !fixedTank));
        tank.dynLabel = f => ch == 3 ? "repair the fuel tank" : "check your patch on the tank";
        var bench = Interact.Add(new Vector3(S.BenchPos.x, Ranch.GY(S.BenchPos.x, S.BenchPos.z) + 0.2f, S.BenchPos.z), 2.8f, "fit the two spare fins you found", f => FitFins(f));
        bench.enabled = f => Active && f.world == WorldId.Ranch && !runner.Busy && ch == 6 && step == 1 && !fixedFins;
        for (int k = 0; k < 3; k++)
        {
            int kk = k;
            var a = Interact.Add(S.anchors[k].position + Vector3.up * 0.2f, 2.6f, "re-tie the rope", f => TieRope(kk));
            a.enabled = f => RopeLoose(kk);
        }
        S.BuildMars(); S.MarsActive(false);
        var rf = Interact.Add(S.RockfallPos + Vector3.up * 0.3f, 4.5f, "clear the rocks", f => ClearRock());
        rf.enabled = f => Active && ch == 7 && step == 0 && f.world == WorldId.Mars && rocksLeft > 0 && !runner.Busy;
        rf.dynLabel = f => "clear the rocks (" + rocksLeft + " left)";
    }

    void PanelPress(Frog f)
    {
        if (ch == 3 && step == 0) Play(TestFireCo());
        else if (ch == 5) Play(FailCo());
        else if (ch == 6 && step == 1 && !fixedChip) { fixedChip = true; StartCoroutine(Glow(S.rocket.SlotWorld(4), new Color(0.4f, 1f, 0.95f))); ToastAll("Chip rebooted - the backup flight plan you rescued is loaded!", 3f); foreach (var l in S.rocket.chipLights) l.sharedMaterial = Mats.Unlit(new Color(0.6f, 1f, 1f)); }
        else if (ch == 6 && step == 2) Play(RelaunchCo());
    }
    void TankPress(Frog f)
    {
        if (ch == 3 && step == 1) Play(RepairCo());
        else if (ch == 6 && !fixedTank) { fixedTank = true; StartCoroutine(Glow(S.rocket.SlotWorld(1), new Color(1f, 0.8f, 0.3f))); ToastAll("Your patch held strong through the fall and the storm!", 3f); }
    }
    void FitFins(Frog f)
    {
        if (fixedFins) return;
        fixedFins = true;
        S.benchFins.gameObject.SetActive(false);
        StartCoroutine(FinFly());
        ToastAll("The two spare fins fit perfectly. Good thing you grabbed all six!", 3.2f);
    }
    IEnumerator FinFly()
    {
        Vector3 from = S.BenchPos + Vector3.up * 1.4f;
        for (int k = 0; k < 2; k++)
        {
            var g = new GameObject("FinFly").transform; g.position = from;
            var m = Instantiate(S.fins[0].t.gameObject, g); m.SetActive(true); m.transform.localPosition = Vector3.zero;
            Vector3 to = S.rocket.root.TransformPoint(Quaternion.Euler(0f, -(45f + (k == 0 ? 1 : 3) * 90f), 0f) * new Vector3(2f, 0.6f, 0f));
            float t = 0f;
            while (t < 1f) { t += Time.deltaTime / 1.1f; g.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t)) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 4f; yield return null; }
            Destroy(g.gameObject);
            FX.Sparkle(to, new Color(1f, 0.4f, 0.3f), 20);
            Sfx.Play(Sfx.Clank != null ? Sfx.Clank : Sfx.Pickup, 0.9f, 1.1f);
        }
        ApplyRocket();
    }
    IEnumerator Glow(Vector3 at, Color c)
    {
        for (int i = 0; i < 6; i++) { FX.Sparkle(at, c, 10); yield return new WaitForSeconds(0.12f); }
        Sfx.Play(Sfx.Win, 0.4f, 1.3f);
    }

    // ---------------- helpers ----------------
    static float Flat(Vector3 d) { d.y = 0f; return d.magnitude; }
    void ToastAll(string s, float t) { foreach (var f in Humans()) f.Toast(s, t); }
    void SetObj(string k, string t, string l) { ui.SetObjective(k, t, l); }
    void SetStatus(string s) { ui.SetStatus(s); }

    float sun0 = -1f;
    void SunK(float k)
    {
        var sun = RenderSettings.sun;
        if (sun == null) return;
        if (sun0 < 0f) sun0 = sun.intensity;
        sun.intensity = sun0 * k;
    }

    Vector3 windNow;
    void Weather(float dt)
    {
        var cams = G.StoryViewCams();
        float rainK = Worlds.StormK > 0.3f ? Mathf.Clamp01((Worlds.StormK - 0.3f) / 0.7f) : 0f;
        bool ranchView = false; foreach (var c in cams) if (c != null && G.CamWorldIsRanch(c)) ranchView = true;
        windNow = Vector3.Lerp(windNow, Wind * (4f + 6f * Worlds.StormK), dt);
        S.Rain(cams, ranchView ? rainK : 0f, windNow);
        if (!cine) SunK(1f - Worlds.StormK * 0.6f);
        if (ch == 5 && !cine && Random.value < dt * 0.15f) { float a = Random.value * 6.28f; Vector3 q = StorySet.Pad + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 120f; q.y = Ranch.GY(q.x, q.z); S.Strike(q); }
    }

    // spots for the yellow light beams (ranch / house): where the team should go next
    List<Vector3> BeamSpots()
    {
        var l = new List<Vector3>();
        if (cine || G.slots.Count == 0) return l;
        if (ch == 2)
        {
            if (state[0] == 0) l.Add(S.EnginePos);
            if (state[1] == 0) foreach (var it in S.fuel) if (!it.taken) { l.Add(new Vector3(it.basePos.x, Ranch.GY(it.basePos.x, it.basePos.z), it.basePos.z)); break; }
            if (state[3] == 0) { Pickups.Item best = null; float bd = 1e9f; Vector3 me = Lead.FocusPoint; foreach (var it in S.fins) if (!it.taken && Flat(it.basePos - me) < bd) { bd = Flat(it.basePos - me); best = it; } if (best != null) l.Add(new Vector3(best.basePos.x, Ranch.GY(best.basePos.x, best.basePos.z), best.basePos.z)); }
            if (state[4] == 0) l.Add(S.ChipVaultPos);
            if (state[2] == 0) l.Add(new Vector3(Layout.HouseC.x, Ranch.GY(Layout.HouseC.x, 12f), 12.2f));
            if (AnyCarried()) l.Add(StorySet.Pad);
        }
        else if (ch == 1 && step == 1) l.Add(StorySet.Pad);
        return l;
    }

    // the marker target for one froggy (null = none)
    public Vector3? Target(Frog f)
    {
        if (!Active || cine || f == null) return null;
        Vector3 me = f.FocusPoint;
        switch (ch)
        {
            case 1: return Portal(f, StorySet.Pad, WorldId.Ranch);
            case 2:
                {
                    for (int i = 0; i < 5; i++) if (state[i] == 1 && carrier[i] == f) return Portal(f, StorySet.Pad, WorldId.Ranch);
                    if (engineOn != null && engineOn.driver == f) return Portal(f, StorySet.Pad, WorldId.Ranch);
                    var c = new List<KeyValuePair<Vector3, WorldId>>();
                    if (state[0] == 0) c.Add(new KeyValuePair<Vector3, WorldId>(S.EnginePos, WorldId.Ranch));
                    else if (state[0] == 1 && engineOn != null && engineOn.driver == null) c.Add(new KeyValuePair<Vector3, WorldId>(engineOn.transform.position, WorldId.Ranch));
                    if (state[1] == 0)
                    {
                        Pickups.Item next = null;
                        foreach (var it in S.fuel) if (!it.taken) { next = it; break; }
                        if (next != null) c.Add(new KeyValuePair<Vector3, WorldId>(next.basePos, WorldId.Ranch));
                    }
                    if (state[3] == 0) foreach (var it in S.fins) if (!it.taken) c.Add(new KeyValuePair<Vector3, WorldId>(it.basePos, WorldId.Ranch));
                    if (state[2] == 0) c.Add(new KeyValuePair<Vector3, WorldId>(noseRevealed ? S.noseItem.basePos : S.SimonC, WorldId.House));
                    if (state[4] == 0) c.Add(new KeyValuePair<Vector3, WorldId>(chipOpen ? S.chipItem.basePos : S.ChipVaultPos, WorldId.Ranch));
                    Vector3? best = null; float bd = 1e9f;
                    foreach (var kv in c)
                    {
                        Vector3 p = Portal(f, kv.Key, kv.Value);
                        float d = (p - me).magnitude + (kv.Value != f.world ? 40f : 0f);
                        if (d < bd) { bd = d; best = p; }
                    }
                    if (best == null && AnyCarried()) return Portal(f, StorySet.Pad, WorldId.Ranch);
                    return best;
                }
            case 3: return Portal(f, step == 0 ? S.PanelPos : S.TankFixPos, WorldId.Ranch);
            case 4:
                {
                    if (step != 1) return null;
                    for (int k = 0; k < 3; k++) if (loose[k]) return Portal(f, S.anchors[k].position, WorldId.Ranch);
                    Rigidbody near = null; float bd = 1e9f;
                    foreach (var rb in S.debris) if (rb != null && !deflected.Contains(rb)) { float d = Flat(rb.position - StorySet.Pad); if (d < bd) { bd = d; near = rb; } }
                    return near != null ? near.position : (Vector3?)Portal(f, StorySet.Pad, WorldId.Ranch);
                }
            case 5: return Portal(f, S.PanelPos, WorldId.Ranch);
            case 6:
                if (step == 1) { if (!fixedFins) return Portal(f, S.BenchPos, WorldId.Ranch); if (!fixedTank) return Portal(f, S.TankFixPos, WorldId.Ranch); if (!fixedChip) return Portal(f, S.PanelPos, WorldId.Ranch); }
                return step >= 1 ? Portal(f, S.PanelPos, WorldId.Ranch) : (Vector3?)null;
            case 7:
                if (f.world != WorldId.Mars) return null;
                if (step == 0)
                {
                    if (rocksLeft > 0) return S.RockfallPos;
                    for (int k = 0; k < S.pups.Count; k++) if (!pupFound[k]) return S.pups[k].transform.position;
                }
                return S.MarsRocketPos;
        }
        return null;
    }

    // a target in another world -> the doorway that leads there (ranch front door <-> house front door)
    Vector3 Portal(Frog f, Vector3 p, WorldId w)
    {
        if (f.world == w) return p;
        if (f.world == WorldId.Ranch && w == WorldId.House) { float z1 = Layout.HouseC.y + Layout.HouseSize.y * 0.5f; return new Vector3(Layout.HouseC.x, Ranch.GY(Layout.HouseC.x, z1 + 0.7f), z1 + 0.7f); }
        if (f.world == WorldId.House && w == WorldId.Ranch) return HouseWorld.L(0f, 0f, 20.6f);
        return p;
    }
}
