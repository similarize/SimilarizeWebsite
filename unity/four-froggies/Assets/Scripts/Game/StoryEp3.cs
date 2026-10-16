using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ffu48 STORY EPISODE 3 - "Spotty's Surprise" (Bill, Oct 2026). Picked from the lobby STORY picker (third card).
// Opening cinematic: the froggies are hanging out on the pond shore when Spotty rides up on her Tourer, towing her Carrier
// Camper - straight into the pond. The bike tips onto its side, the camper sinks (bubbles, a held beat of silence)... then
// it TRANSFORMS and the whole aircraft carrier rises out of the water. Big surprised reactions (James the biggest: a
// backflip-hop and the close-up), then awe, and the reveal of Spotty up on the flying bridge.
//  Chapter 2 "All Aboard!": swim out and climb aboard (A / E at the hull); everyone on deck -> cheers.
//  Chapter 3 "Deck Landing": James brings his VTOL jet and lands it on the deck (a human James flies it himself;
//  otherwise it is a cutscene), credits, "To be continued...", free play (the carrier stays out on the pond).
// Spotty is a story NPC (the fixed-roster cat, never a new name): a player who picked Spotty plays another free character
// for this episode (toast), restored at the end. Same machinery as episodes 1 + 2 (checkpoints in localStorage
// "ff.story.carrier" = "sc1|ch|step", cutscene runner, letterbox, dialogue, SKIP, objective card + markers).
// URL: ?story=3&storych=N; demos ?ffdemo=1&ffshot=ep3intro|ep3board|ep3land.
public partial class Story
{
    public const string SaveKey3 = "ff.story.carrier";
    public const string Ep3Name = "Spotty's Surprise";
    public static readonly string[] ChTitle3 = { "", "Spotty's Surprise", "All Aboard!", "Deck Landing" };
    public static readonly int[] EpChapters = { 0, 7, 7, 3 };
    readonly Dictionary<int, int> e3Swap = new Dictionary<int, int>();
    FrogModel spottyNpc;
    CarrierRig Rig { get { return CarrierRig.I; } }

    // ---------------- start / stop ----------------
    void Begin3()
    {
        episode = 3;
        E3Cast();
        Frog.AiGoalHook = E3AiGoal;
        if (Rig == null) { try { CarrierRig.Spawn(); } catch (System.Exception e) { Debug.LogError("FFSTORY ep3: carrier spawn " + e); } }   // ffu48b: the story can start before Ffu48's first Update
        int jump = demoShot == "ep3intro" ? 1 : demoShot == "ep3board" ? 2 : demoShot == "ep3land" ? 3 : 0;
        string jc = Param("storych"); int jn;
        if (jc != null && int.TryParse(jc, out jn)) jump = Mathf.Clamp(jn, 1, 3);
        Debug.Log("FFSTORY ep3 begin demo=" + demo + " shot=" + demoShot + " jump=" + jump + " mode=" + pickMode + " rig=" + (Rig != null));
        if (Rig == null) { ToastAll("Spotty's Carrier isn't ready yet - try again in a moment", 4f); Active = true; Stop(); return; }
        if (jump > 0) { StartChapter(jump, 0); return; }
        int c, s;
        bool has = Load3(out c, out s) && c >= 1 && c <= 4 && !(c == 1 && s == 0);
        if (has && pickMode != 2) { StartChapter(c >= 4 ? 3 : c, 0); return; }
        StartChapter(1, 0);
    }

    void E3Stop()
    {
        Frog.AiGoalHook = null;
        foreach (var kv in e3Swap) G.SetSeatChar(kv.Key, kv.Value, false);
        e3Swap.Clear();
        G.FixAiChars();
        E3ReactClear();
        if (Rig != null)
        {
            Rig.ghost = null; Rig.speedK = 1f; Rig.holdAt = -1f; Rig.npcWave = false;
            if (Rig.bike != null) Rig.bike.ghostHook = null;
            Rig.npc = null;
            if (Rig.helm != null) Rig.helm.demoHook = null;
        }
        if (spottyNpc != null) { Destroy(spottyNpc.gameObject); spottyNpc = null; }
        fireworks = false;
        if (ui != null) ui.CineHud("");
    }

    void E3Cast()
    {
        e3Swap.Clear();
        var used = new HashSet<int>();
        foreach (var s in G.slots) used.Add(G.frogs[s.frog].charId);
        foreach (var s in G.slots)
        {
            Frog f = G.frogs[s.frog];
            if (f.charId != 4) continue;
            int rep = -1;
            foreach (int c in new[] { 0, 1, 2, 3, 5, 6, 7, 8, 9 }) if (!used.Contains(c)) { rep = c; break; }
            if (rep < 0) continue;
            used.Add(rep); e3Swap[s.frog] = 4;
            G.SetSeatChar(s.frog, rep, false);
            f.Toast("Spotty is the surprise in this story - you play " + Roster.Name(rep) + "!", 6f);
            Debug.Log("FFSTORY ep3: seat " + s.frog + " plays " + Roster.Name(rep) + " (Spotty is the story's surprise)");
        }
        G.FixAiChars();
    }

    void Save3()
    {
        if (demo) return;
        string v = "sc1|" + ch + "|" + step;
        try { FFStorySave(SaveKey3, v); Debug.Log("FFSTORY ep3 saved " + v); } catch (System.Exception e) { Debug.LogWarning("FFSTORY save failed: " + e.Message); }
    }
    bool Load3(out int c, out int s)
    {
        c = 1; s = 0;
        if (demo) return false;
        var a = ReadSave(SaveKey3).Split('|');
        if (a.Length < 3 || a[0] != "sc1") return false;
        return int.TryParse(a[1], out c) && int.TryParse(a[2], out s);
    }

    // ---------------- places ----------------
    Vector3 Site3 { get { Vector2 s = CarrierLayout.Site; return new Vector3(s.x, Ranch.GY(s.x, s.y), s.y); } }
    Vector3 Fwd3 { get { Vector2 f = CarrierLayout.Fwd; return new Vector3(f.x, 0f, f.y); } }
    Vector3 Rt3 { get { Vector3 f = Fwd3; return new Vector3(f.z, 0f, -f.x); } }
    Vector3 Anchor3 { get { Vector3 a = Site3 + Fwd3 * 52f; a.y = Ripples.SurfaceY; return a; } }
    Vector3 Watch3 { get { Vector3 w = Site3 + Fwd3 * 15f - Rt3 * 15f; w.y = Ranch.GY(w.x, w.z); return w; } }
    float WatchYaw { get { Vector3 d = (Site3 + Fwd3 * 34f) - Watch3; return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; } }
    Frog JamesF { get { foreach (var f in G.frogs) if (f != null && f.charId == 0 && !f.netPuppet) return f; return null; } }
    int JamesCast { get { for (int k = 0; k < 4; k++) if (Cast(k).charId == 0) return k; return 0; } }
    int CastOf(int charId, int fallback) { for (int k = 0; k < 4; k++) if (Cast(k).charId == charId) return k; return fallback; }

    void SpottyOnBike()
    {
        if (Rig == null) return;
        if (spottyNpc == null)
        {
            var go = new GameObject("Spotty (story)");
            spottyNpc = go.AddComponent<FrogModel>();
            spottyNpc.BuildChar(4);
            Mats.SetLayer(go, 9);
        }
        Rig.npc = spottyNpc;
        Transform st = Rig.bike.seat;
        spottyNpc.transform.SetParent(st, false);
        spottyNpc.transform.localPosition = Vector3.zero; spottyNpc.transform.localRotation = Quaternion.identity; spottyNpc.transform.localScale = Vector3.one * 0.66f;
    }
    void SpottyOnBridge()
    {
        if (Rig == null) return;
        SpottyOnBike();
        Rig.npc = spottyNpc;
        spottyNpc.transform.SetParent(Rig.seatNode, false);
        spottyNpc.transform.localPosition = Vector3.zero; spottyNpc.transform.localRotation = Quaternion.identity; spottyNpc.transform.localScale = Vector3.one * 0.75f;
    }
    // the carrier floating at the anchor spot, Spotty on the bridge, holding position
    void CarrierAtAnchor()
    {
        if (Rig == null) return;
        if (Rig.bike.driver != null) Rig.bike.driver.ExitVehicle();
        if (Rig.helm.driver != null) Rig.helm.driver.ExitVehicle();
        if (!Rig.Carrier || Flat(Rig.transform.position - Anchor3) > 30f) Rig.ForceCarrier(Anchor3, CarrierLayout.Yaw);
        Rig.ghost = i => new PIn();
        Rig.bike.ghostHook = null;
        SpottyOnBridge();
    }

    static PIn E3Drive(Bike b, Vector3 tgt, float vt)
    {
        var o = new PIn();
        Vector3 d = tgt - b.transform.position; d.y = 0f;
        Vector3 fw = b.transform.forward; fw.y = 0f;
        float ang = Vector3.SignedAngle(fw, d, Vector3.up);
        o.move = new Vector2(Mathf.Clamp(ang / 30f, -1f, 1f), 0f);
        float fs = b.ForwardSpeed;
        if (fs < vt) o.gas = Mathf.Clamp01((vt - fs) * 0.3f + 0.25f); else o.brake = Mathf.Clamp01((fs - vt) * 0.3f);
        return o;
    }

    // pose on the deck (no ground snap: the deck is above the water)
    void PoseDeck(Frog f, Vector3 p, float yaw)
    {
        if (f.vehicle != null) f.ExitVehicle();
        poses[f] = new Vector4(p.x, p.y, p.z, yaw);
        f.Teleport(p);
    }

    // ---------------- chapters ----------------
    void E3StartChapter(int c, int s)
    {
        ch = c; step = s; chTime = 0f;
        runner.Clear(); cine = false; modal = false; modalHidesObj = false; poses.Clear();
        foreach (var f in G.frogs) Slump(f, false);
        E3ReactClear();
        ui.dimK = 0f; ui.bigK = 0f; ui.titleK = 0f; ui.creditsK = 0f;
        SetStatus(""); ui.CineHud("");
        Sfx.Override = "story"; fireworks = false;
        Debug.Log("FFSTORY ep3 chapter " + c + " step " + s + " t=" + Time.realtimeSinceStartup.ToString("0.0"));
        if (s == 0) Save3();
        switch (c)
        {
            case 1: Play(E3IntroCo()); break;
            case 2: CarrierAtAnchor(); step = 1; boardT = 0f; break;
            case 3: CarrierAtAnchor(); TeamAboard(); Play(E3LandCo()); break;
        }
    }

    float boardT, aiBoardT;
    void E3Update(float dt)
    {
        if (!runner.Busy)
        {
            if (ch == 2 && step == 1) Ch2E3(dt);
            if (ch == 3 && step == 1) Ch3E3(dt);
        }
        ui.Tick(cine, cine && skippable, SkipHint());
        ui.ObjectivesVisible(!cine && !modalHidesObj, 1f);
    }

    void Ch2E3(float dt)
    {
        boardT += dt;
        if (Rig == null) return;
        if (!Rig.Carrier) CarrierAtAnchor();
        string how = G.slots.Count > 0 && G.slots[0].kind == InputKind.Gamepad ? "A" : G.slots.Count > 0 && G.slots[0].kind == InputKind.Touch ? "A" : "E";
        int on = 0, n = 0;
        foreach (var f in Humans()) { n++; if (CarrierRig.OnDeck(f.transform.position)) on++; }
        SetObj("CHAPTER 2 · " + ChTitle3[2].ToUpper(), "Swim out to Spotty's carrier and climb aboard (" + how + " at the hull)", n > 1 ? "On deck: " + on + " / " + n : null);
        if (demo && boardT > 4f) foreach (var f in Humans()) if (!CarrierRig.OnDeck(f.transform.position)) Rig.Board(f, -1f, 0f);
        // AI crew climb aboard once a player is up there
        aiBoardT -= dt;
        if (on > 0 && aiBoardT <= 0f)
        {
            aiBoardT = 1.5f;
            foreach (var f in Team()) if (!f.human && !CarrierRig.OnDeck(f.transform.position)) { Rig.Board(f, 1f, -6f + f.id * 3f); break; }
        }
        if (n > 0 && on == n) { step = 2; Play(E3AboardCo()); }
    }

    void TeamAboard()
    {
        int i = 0;
        foreach (var f in Team())
        {
            if (!CarrierRig.OnDeck(f.transform.position)) Rig.Board(f, -1f, -6f + i * 3f);
            i++;
        }
    }

    void Ch3E3(float dt)
    {
        Jet j = Jet.I;
        if (j == null || Rig == null) { step = 2; Play(E3EndCo()); return; }
        SetObj("CHAPTER 3 · " + ChTitle3[3].ToUpper(), "Land your VTOL on the carrier: hover over the pink paw, then gently down", null);
        Frog jf = JamesF;
        if (j.onGround && Rig.JetParked()) { step = 2; if (jf != null && jf.vehicle == j) jf.ExitVehicle(); Play(E3EndCo()); return; }
        if (jf == null || jf.vehicle != j)
        {
            // he hopped out somewhere: back into the jet, hovering near the carrier
            if (jf != null && chTime > 2f) { chTime = 0f; JetNear(jf); }
        }
    }

    void JetNear(Frog jf)
    {
        Jet j = Jet.I; if (j == null || jf == null) return;
        Vector3 p = Rig.transform.position - Rig.transform.forward * 60f + Rig.transform.right * 30f; p.y = Ripples.SurfaceY + 24f;
        Vector3 d = Rig.LandingSpot - p; d.y = 0f;
        if (jf.vehicle != null) jf.ExitVehicle();
        jf.SendTo(WorldId.Ranch, p, 0f);
        jf.EnterVehicle(j);
        j.DemoHover(p, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 0f);
    }

    Vector3? E3Target(Frog f)
    {
        if (!Active || cine || f == null || Rig == null) return null;
        if (ch == 2 && step == 1 && !CarrierRig.OnDeck(f.transform.position))
        {
            Vector3 l = Rig.transform.InverseTransformPoint(f.transform.position);
            Vector3 p = Rig.transform.TransformPoint(new Vector3(Mathf.Sign(l.x) * (CarrierLayout.HalfW + 0.8f), 0f, Mathf.Clamp(l.z, -8f, 8f)));
            p.y = Ripples.SurfaceY + 0.5f;
            return p;
        }
        if (ch == 3 && step == 1 && Jet.I != null && f.vehicle == Jet.I) return Rig.LandingSpot;
        return null;
    }

    Vector3? E3AiGoal(Frog f)
    {
        if (!Active || episode != 3 || f == null || f.human || G.slots.Count == 0) return null;
        if (CarrierRig.OnDeck(f.transform.position) && Rig != null)
        {
            float a = f.id * 1.7f;
            return Rig.DeckPoint(-1.5f + Mathf.Cos(a) * 2f, -4f + Mathf.Sin(a) * 4f);
        }
        return null;
    }

    // ---------------- reactions ----------------
    class Rx { public float t0, big; public Vector3 s0; }
    readonly Dictionary<Frog, Rx> reacts = new Dictionary<Frog, Rx>();
    void React(Frog f, float big)
    {
        if (f == null || f.model == null) return;
        Rx r;
        if (!reacts.TryGetValue(f, out r)) { r = new Rx { s0 = f.model.transform.localScale }; reacts[f] = r; }
        r.t0 = Time.time; r.big = big;
        if (!skipping)
        {
            if (Roster.IsFrog(f.charId)) Sfx.Ribbiting(f.transform.position, 0.5f + 0.4f * big);
            else { var c = Sfx.AnimalVoice(Roster.KindOf(f.charId) == Roster.Kind.Cat ? "Cat" : "Dog"); if (c != null) Sfx.Play(c, 0.5f, 1.2f); }
            if (big > 0.5f && Sfx.Hop != null) Sfx.Play(Sfx.Hop, 0.7f, 0.8f);
        }
    }
    void ReactAll(float big) { foreach (var f in Team()) React(f, big); }
    void E3ReactClear()
    {
        foreach (var kv in reacts) if (kv.Key != null && kv.Key.model != null) { var m = kv.Key.model.transform; m.localPosition = Vector3.zero; m.localRotation = Quaternion.identity; m.localScale = kv.Value.s0; }
        reacts.Clear();
    }
    // called at the end of ApplyPoses (after the walk animation and the slump / talk bob): hop, stretch, arms up; the big
    // one (James) is a high surprised hop with a backflip and a long squash-and-stretch
    void E3ReactApply()
    {
        if (reacts.Count == 0) return;
        var done = new List<Frog>();
        foreach (var kv in reacts)
        {
            Frog f = kv.Key; Rx r = kv.Value;
            if (f == null || f.model == null) { done.Add(f); continue; }
            float dur = 1.3f + r.big * 1.4f, t = Time.time - r.t0, u = t / dur;
            Transform m = f.model.transform;
            if (u >= 1f) { m.localPosition = Vector3.zero; m.localRotation = Quaternion.identity; m.localScale = r.s0; done.Add(f); continue; }
            float hop = Mathf.Sin(Mathf.PI * Mathf.Clamp01(u * 1.15f)) * (0.55f + 2.4f * r.big);
            m.localPosition = Vector3.up * Mathf.Max(0f, hop);
            float flip = r.big > 0.5f ? 360f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.12f) / 0.6f)) : 0f;
            float lean = r.big > 0.5f ? 0f : -12f * Mathf.Sin(Mathf.PI * u);
            m.localRotation = Quaternion.Euler(-flip + lean, 0f, 0f);
            float st = 1f + Mathf.Sin(Mathf.PI * Mathf.Clamp01(u * 2.2f)) * (0.18f + 0.3f * r.big) - (u > 0.85f ? Mathf.Sin(Mathf.PI * (u - 0.85f) / 0.15f) * 0.15f : 0f);
            float sq = 1f / Mathf.Sqrt(Mathf.Max(0.3f, st));
            m.localScale = new Vector3(r.s0.x * sq, r.s0.y * st, r.s0.z * sq);
            var fm = f.model;
            float w = Mathf.Sin(t * (14f + 8f * r.big)) * 24f;
            if (fm.armL != null) fm.armL.localRotation = Quaternion.Euler(-160f + w, 0f, -25f);
            if (fm.armR != null) fm.armR.localRotation = Quaternion.Euler(-160f - w, 0f, 25f);
            if (r.big > 0.5f && fm.legL != null) { fm.legL.localRotation = Quaternion.Euler(-40f * Mathf.Sin(Mathf.PI * u), 0f, -20f); fm.legR.localRotation = Quaternion.Euler(-40f * Mathf.Sin(Mathf.PI * u), 0f, 20f); }
        }
        foreach (var f in done) reacts.Remove(f);
    }

    // close shot on one cast member, tighter + lower for a reaction (fov punch for the big one)
    void CloseR(int who, float dist, float fov)
    {
        Frog f = Cast(who);
        Vector3 fw = PoseFwd(f), rt = new Vector3(fw.z, 0f, -fw.x);
        Vector3 p = f.transform.position + Vector3.up * 0.8f;
        Shot(p + fw * (dist + 0.8f) + rt * 0.5f + Vector3.up * 0.25f, p + Vector3.up * 0.3f, p + fw * dist + rt * 0.35f + Vector3.up * 0.15f, p + Vector3.up * 0.6f, 2.5f, fov);
    }

    // ---------------- cutscenes ----------------
    IEnumerator E3IntroCo()
    {
        var rig = Rig;
        yield return CineOn();
        Sfx.Override = "story";
        rig.ResetHome();
        SpottyOnBike();
        rig.ghost = null;
        rig.holdAt = 0.3f; rig.speedK = 0.75f;
        Vector3 S0 = Site3, fw = Fwd3, rt = Rt3, W = Watch3, up = Vector3.up;
        Vector3 pondLook = S0 + fw * 40f; pondLook.y = Ripples.SurfaceY + 1f;
        PoseCast(W, WatchYaw, 2.3f);
        int jc = JamesCast;
        // 1 - a calm morning at the pond
        Shot(W - fw * 14f - rt * 10f + up * 9f, pondLook, W - fw * 9f - rt * 6f + up * 5f, pondLook, 7f, 55f);
        yield return Title("EPISODE 3", Ep3Name, 2.6f);
        Vector3 wf = Quaternion.Euler(0f, WatchYaw, 0f) * Vector3.forward;
        Shot(W + wf * 7f + up * 1.8f - rt * 2f, W + up * 0.9f, W + wf * 6f + up * 1.6f, W + up * 0.9f, 5f, 48f);
        yield return Say(jc, "What a perfect day at the pond!");
        yield return Say(CastOf(1, 1) == jc ? 1 : CastOf(1, 1), "Hey... is that Spotty? What is she TOWING?");
        // 2 - Spotty's rig rolls up and heads for the water
        Bike b = rig.bike;
        Vector3 tgt = S0 + fw * 70f;
        b.ghostHook = i => E3Drive(b, tgt, 6.5f);
        Track(() => b.transform.position - b.transform.forward * 8f + rt * 5f + up * 3.2f, () => (b.transform.position + rig.transform.position) * 0.5f + up * 1.2f, 50f);
        yield return Say(-5, "Hi everyone! Watch THIS!");
        FaceAll(b.transform.position);
        CloseR(jc, 2.6f, 40f);
        React(Cast(jc), 0.2f);
        yield return Say(jc, "Spotty, STOP! That's the POND!");
        // 3 - into the water
        Vector3 side = S0 + fw * 26f + rt * 16f; side.y = Ripples.SurfaceY + 1.6f;
        Track(() => side, () => b.transform.position + up * 0.8f, 46f);
        float w = 0f;
        while (rig.k <= 0f && w < 18f && !skipping) { w += Time.deltaTime; yield return null; }
        if (rig.k <= 0f) { Debug.Log("FFSTORY ep3: rig never reached deep water (w " + w.ToString("0.0") + ") - forcing"); rig.ForceCarrier(Anchor3, CarrierLayout.Yaw); }
        // 4 - it tips over... and sinks
        Track(() => rig.transform.position + rt * 11f + fw * 6f + Vector3.up * (Ripples.SurfaceY - rig.transform.position.y + 1.1f), () => rig.transform.position + up * 1.6f, 44f);
        yield return Wait(2.6f);
        FaceAll(rig.transform.position);
        ReactAll(0.15f);
        CloseR(CastOf(2, 2), 2.4f, 40f);
        yield return Say(CastOf(2, 2), "Oh no... she's SINKING!");
        CloseR(jc, 2.0f, 34f);
        React(Cast(jc), 0.3f);
        yield return Say(jc, "SPOTTY!!");
        // the held beat: just bubbles
        Track(() => rig.transform.position + rt * 8f + fw * 9f + Vector3.up * (Ripples.SurfaceY - rig.transform.position.y + 0.5f), () => rig.transform.position + up * 2.2f, 40f);
        ui.CineHud("");
        yield return Wait(2.4f);
        // 5 - rumble... and it RISES
        camShake = 0.3f;
        rig.holdAt = -1f;
        Vector3 low = rig.transform.position + rt * 30f + fw * 10f; low.y = Ripples.SurfaceY + 0.7f;
        Track(() => low, () => rig.transform.position + up * (1.5f + 4f * Mathf.Clamp01((rig.k - 0.6f) / 0.4f)), 52f);
        w = 0f;
        while (!rig.Carrier && w < 22f && !skipping) { w += Time.deltaTime; if (rig.k > 0.72f && rig.k < 0.9f) camShake = Mathf.Max(camShake, 0.18f); yield return null; }
        if (!rig.Carrier) { float ws, wd; bool wet = Water.At(rig.transform.position.x, rig.transform.position.z, out ws, out wd) && wd > 2.5f; rig.ForceCarrier(wet ? rig.transform.position : Anchor3, wet ? rig.transform.eulerAngles.y : CarrierLayout.Yaw); }
        rig.ghost = i => new PIn();
        SpottyOnBridge();
        ui.bigText = "CARRIER MODE!";
        yield return Anim(0.4f, k => ui.bigK = k);
        yield return Wait(1.4f);
        yield return Anim(0.4f, k => ui.bigK = 1f - k);
        // 6 - everyone is super surprised (James the most)
        FaceAll(rig.transform.position);
        Shot(W + wf * 9f + up * 2.2f, W + up * 1.2f, W + wf * 7.5f + up * 1.8f, W + up * 1.4f, 3f, 52f);
        ReactAll(0.35f);
        React(Cast(jc), 1f);
        yield return Wait(1.6f);
        int rx = CastOf(3, 3), bb = CastOf(2, 2), jm = CastOf(1, 1);
        if (rx != jc) { CloseR(rx, 2.2f, 38f); React(Cast(rx), 0.5f); yield return Say(rx, "WHOA!"); }
        if (bb != jc && bb != rx) { CloseR(bb, 2.2f, 38f); React(Cast(bb), 0.5f); yield return Say(bb, "No WAY!"); }
        if (jm != jc && jm != rx && jm != bb) { CloseR(jm, 2.2f, 38f); React(Cast(jm), 0.5f); yield return Say(jm, "It's a boat! No - it's a SHIP!"); }
        // James: the biggest one - a super close-up with a fov punch and a backflip-hop
        CloseR(jc, 1.7f, 30f);
        React(Cast(jc), 1f);
        camShake = 0.12f;
        yield return Wait(0.8f);
        yield return Say(jc, "IT'S... AN... AIRCRAFT CARRIER!!!");
        React(Cast(jc), 0.7f);
        // 7 - awe: they all look out at it, then... who's that up on the bridge?
        FaceAll(rig.transform.position);
        Vector3 behind = W - wf * 6f + up * 2.4f;
        Shot(behind, rig.transform.position + up * 4f, behind + wf * 3f + up * 0.4f, rig.transform.position + up * 5f, 5f, 50f);
        yield return Wait(3.2f);
        rig.npcWave = true;
        Vector3 br = rig.BridgeWorld;
        Vector3 rfw = rig.transform.forward; rfw.y = 0f; rfw.Normalize();
        Vector3 rrt = new Vector3(rfw.z, 0f, -rfw.x);
        Shot(br + rfw * 9f - rrt * 3f - up * 2.5f, br + up * 0.6f, br + rfw * 5.5f - rrt * 1.5f - up * 0.6f, br + up * 0.7f, 5f, 44f);
        if (Sfx.Confirm != null && !skipping) Sfx.Play(Sfx.Confirm, 0.5f, 0.9f);
        yield return Say(-5, "Surprise! Meet my Carrier Camper. Want to come aboard?");
        CloseR(jc, 2.2f, 38f);
        React(Cast(jc), 0.4f);
        yield return Say(jc, "Does it have room for my JET?!");
        Shot(br + rfw * 6f - rrt * 2f - up * 1f, br + up * 0.6f, br + rfw * 5f - rrt * 1.5f - up * 0.8f, br + up * 0.7f, 4f, 42f);
        yield return Say(-5, "That's what the flight deck is for! Swim on out!");
        rig.npcWave = false;
        rig.speedK = 1f;
        yield return CineOff();
        Next(2, 0);
    }

    IEnumerator E3AboardCo()
    {
        yield return CineOn();
        var rig = Rig;
        TeamAboard();
        int i = 0;
        Vector3 rfw = rig.transform.forward; rfw.y = 0f; rfw.Normalize();
        float yaw = Mathf.Atan2(rfw.x, rfw.z) * Mathf.Rad2Deg;
        foreach (var f in Team()) { PoseDeck(f, rig.DeckPoint(-2.2f + i * 1.3f, 1.5f - i * 0.6f), yaw + 180f); i++; }
        Vector3 c = rig.DeckPoint(-1.5f, 1f);
        Shot(c + rfw * -9f + Vector3.up * 5f, c + Vector3.up, c + rfw * -7f + Vector3.up * 3.5f, c + Vector3.up, 6f, 52f);
        ReactAll(0.4f);
        if (!skipping && Sfx.Win != null) Sfx.Play(Sfx.Win, 0.5f);
        yield return Say(CastOf(3, 3), "Everybody's aboard!");
        rig.npcWave = true;
        Vector3 br = rig.BridgeWorld;
        Shot(br + rfw * 7f - Vector3.up * 2f, br + Vector3.up * 0.6f, br + rfw * 5f - Vector3.up * 1f, br + Vector3.up * 0.6f, 4f, 44f);
        yield return Say(-5, "Welcome aboard! Next: a real flight-deck landing. James, where's your jet?");
        CloseR(JamesCast, 2.3f, 40f);
        yield return Say(JamesCast, "At the hangar! Hold on - I'll fly it over!");
        rig.npcWave = false;
        yield return CineOff();
        Next(3, 0);
    }

    IEnumerator E3LandCo()
    {
        var rig = Rig;
        Jet j = Jet.I;
        Frog jf = JamesF;
        bool fly = j != null && jf != null && jf.human && !demo;
        Debug.Log("FFSTORY ep3 chapter 3: " + (j == null ? "no jet" : fly ? "James (player) flies the VTOL" : "cutscene landing"));
        if (j == null) { step = 2; Play(E3EndCo()); yield break; }
        if (fly)
        {
            JetNear(jf);
            step = 1;
            chTime = 0f;
            yield break;
        }
        // cutscene: the jet comes in, hovers over the pink paw and settles onto the deck
        yield return CineOn();
        if (jf != null && jf.vehicle != null) jf.ExitVehicle();
        Vector3 spot = rig.LandingSpot;
        Vector3 rfw = rig.transform.forward; rfw.y = 0f; rfw.Normalize();
        Vector3 rrt = new Vector3(rfw.z, 0f, -rfw.x);
        float yaw = Mathf.Atan2(rfw.x, rfw.z) * Mathf.Rad2Deg;
        Vector3 a = spot - rfw * 90f + rrt * 30f + Vector3.up * 26f, h = spot + Vector3.up * 9f;
        j.DemoHover(a, yaw, 0f);
        Vector3 cam = rig.transform.position + rrt * -26f + rfw * 18f + Vector3.up * 10f;
        Track(() => cam, () => j.transform.position, 50f);
        if (!skipping && Sfx.Confirm != null) Sfx.Play(Sfx.Confirm, 0.4f, 1.2f);
        yield return Anim(7f, k => { float e = k * k * (3f - 2f * k); j.DemoHover(Vector3.Lerp(a, h, e), yaw, 0f); });
        yield return Anim(4f, k => j.DemoHover(Vector3.Lerp(h, spot + Vector3.up * 0.6f, k), yaw, 0f));
        float w = 0f;
        while (!j.onGround && w < 6f && !skipping) { w += Time.deltaTime; yield return null; }
        if (!j.onGround) j.DemoHover(spot + Vector3.up * 0.05f, yaw, 0f);
        Debug.Log("FFSTORY ep3 jet on the deck: onGround " + j.onGround + " parked " + rig.JetParked());
        if (jf != null) PoseDeck(jf, spot + rrt * -5.5f + Vector3.up * 0.1f, yaw + 90f);
        yield return Wait(1f);
        yield return CineOff();
        step = 2;
        Play(E3EndCo());
    }

    IEnumerator E3EndCo()
    {
        var rig = Rig;
        yield return CineOn();
        Sfx.Override = "story_triumph";
        Vector3 rfw = rig.transform.forward; rfw.y = 0f; rfw.Normalize();
        Vector3 rrt = new Vector3(rfw.z, 0f, -rfw.x);
        float yaw = Mathf.Atan2(rfw.x, rfw.z) * Mathf.Rad2Deg;
        int i = 0;
        foreach (var f in Team()) { PoseDeck(f, rig.DeckPoint(-3.2f + i * 1.4f, -2.5f), yaw + 180f); i++; }
        Vector3 c = rig.transform.position + Vector3.up * 4f;
        Shot(c + rrt * 34f - rfw * 20f + Vector3.up * 14f, c, c + rrt * 26f - rfw * 12f + Vector3.up * 9f, c + rfw * 3f, 7f, 52f);
        ReactAll(0.6f);
        rig.npcWave = true;
        yield return Say(-5, "Perfect landing, James! Welcome to the Carrier Camper.");
        CloseR(JamesCast, 2.2f, 38f);
        React(Cast(JamesCast), 0.8f);
        yield return Say(JamesCast, "Best. Day. EVER!");
        Shot(c + rrt * 40f + rfw * 30f + Vector3.up * 22f, c, c + rrt * 30f + rfw * 24f + Vector3.up * 16f, c, 14f, 55f);
        ui.SetCredits(Credits3());
        yield return Anim(1.5f, k => { ui.creditsK = k; ui.dimK = 0.45f * k; });
        yield return Wait(skipping ? 0f : 7f);
        yield return Anim(1f, k => { ui.creditsK = 1f - k; ui.dimK = 0.45f * (1f - k); });
        ui.bigText = "To be continued...";
        yield return Anim(1.2f, k => ui.bigK = k);
        yield return Wait(2.5f);
        yield return Anim(1.2f, k => ui.bigK = 1f - k);
        rig.npcWave = false;
        yield return CineOff();
        ch = 4; step = 0; Save3();
        ToastAll("THE END - Spotty's Carrier is out on the pond. Take the helm (ask Spotty) or swim aboard!", 6f);
        Debug.Log("FFSTORY ep3 complete");
        Sfx.Override = null;
        Active = true; Stop();
    }

    string Credits3()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<size=40><b>SPOTTY'S SURPRISE</b></size>\n<size=20>a Four Froggies story · episode 3</size>\n\n");
        sb.Append("<size=22><color=#ffd27a>STARRING</color></size>\nSpotty (and her Carrier Camper)\n");
        foreach (var f in Team()) sb.Append(Roster.Name(f.charId) + "\n");
        sb.Append("\n<size=22><color=#ffd27a>ALSO STARRING</color></size>\nthe Tourer motorcycle\nJames's VTOL jet\n\n");
        sb.Append("<size=20>Some surprises are worth the wait.</size>");
        return sb.ToString();
    }
}
