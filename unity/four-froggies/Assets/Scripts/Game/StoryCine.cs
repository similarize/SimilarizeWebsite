using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// ffu22 story mode: the cutscenes. Each one is an IEnumerator run by a tiny stack runner (nested IEnumerators run in the
// same frame), so SKIP simply makes every wait return at once: the scene's actions still happen in order and the state
// always ends where the scene would have left it. In-engine camera shots (eased moves, tracking the rocket), letterbox,
// dialogue with portraits, title cards, the low-point dim and the quick-repair meter live here too.
public partial class Story
{
    class Runner
    {
        readonly Stack<IEnumerator> st = new Stack<IEnumerator>();
        public bool Busy { get { return st.Count > 0; } }
        public void Start(IEnumerator e) { st.Clear(); st.Push(e); }
        public void Clear() { st.Clear(); }
        public System.Action onError;
        public void Tick()
        {
            int guard = 0;
            while (st.Count > 0 && guard++ < 200000)
            {
                var top = st.Peek();
                bool more;
                try { more = top.MoveNext(); }
                catch (System.Exception ex) { Debug.LogError("FFSTORY cutscene error: " + ex); st.Clear(); if (onError != null) onError(); return; }
                if (st.Count == 0 || st.Peek() != top) return;   // the scene ended the story / started another one
                if (!more) { st.Pop(); continue; }
                var y = top.Current as IEnumerator;
                if (y != null) { st.Push(y); continue; }
                return;
            }
        }
    }

    readonly Runner runner = new Runner();
    bool cine, modal, skippable = true, skipping, advance, modalHidesObj;
    int choice;
    int pendCh = -1, pendStep;

    public bool Cine { get { return Active && cine; } }
    public bool BlocksInput { get { return Active && (cine || modal); } }

    void Play(IEnumerator co)
    {
        runner.onError = () => { cine = false; modal = false; skipping = false; ui.letterbox = 0f; ui.fadeK = 0f; ui.Minigame(false, 0, 0, 0, ""); ui.Dialogue(null, null, null, Color.white, ""); ui.ShowChoice(null, null, null); camP = null; poses.Clear(); };
        runner.Start(Wrap(co));
    }
    IEnumerator Wrap(IEnumerator co)
    {
        skipping = false; advance = false;
        yield return co;
        skipping = false;
    }
    void Next(int c, int s) { pendCh = c; pendStep = s; }

    // ---------------- input ----------------
    // called by Game for every slot while a cutscene / modal is up (pad A / Space / E = next, Start / H = skip)
    public void SlotPress(PIn i)
    {
        if (i.hop || i.use || i.fire || i.gunFire) advance = true;
        if (i.help && cine && skippable) skipping = true;
    }

    void PollRawInput()
    {
        if (!(cine || modal)) return;
        if (Kb.EnterDown() || Kb.SpaceDown()) advance = true;
        if (Kb.EscDown() && cine && skippable) skipping = true;
        var k = Keyboard.current;
        if (choice == 0 && ui != null)
        {
            if (k != null && (k.nKey.wasPressedThisFrame || k.yKey.wasPressedThisFrame)) choice = 2;
            foreach (var pad in Gamepad.all) if (pad != null && (pad.buttonNorth.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame)) choice = 2;
        }
        var taps = Kb.TouchesBegan();
        foreach (var p in taps)
        {
            if (ui.HitSkip(p) && skippable && cine) { skipping = true; continue; }
            if (ui.HitChoiceB(p)) { choice = 2; continue; }
            if (ui.HitChoiceA(p)) { choice = 1; continue; }
            advance = true;
        }
        if (Kb.MouseLeftDown() && Kb.TouchCount() == 0 && Mouse.current != null)
        {
            Vector2 mp = Mouse.current.position.ReadValue();
            if (ui.HitSkip(mp) && skippable && cine) skipping = true;
            else if (ui.HitChoiceB(mp)) choice = 2;
            else if (ui.HitChoiceA(mp)) choice = 1;
            else advance = true;
        }
    }

    string SkipHint()
    {
        if (G.slots.Count == 0) return "SKIP";
        switch (G.slots[0].kind) { case InputKind.Gamepad: return "SKIP  · START"; case InputKind.Keyboard: return "SKIP  · ESC"; default: return "SKIP"; }
    }
    string NextHint()
    {
        if (G.slots.Count == 0) return "";
        switch (G.slots[0].kind) { case InputKind.Gamepad: return "A  next"; case InputKind.Keyboard: return "ENTER / SPACE / CLICK  next"; default: return "tap to continue"; }
    }

    // ---------------- primitives ----------------
    IEnumerator Wait(float s) { float t = 0f; while (t < s && !skipping) { t += Time.deltaTime; yield return null; } }
    IEnumerator Anim(float dur, System.Action<float> f)
    {
        float t = 0f;
        while (t < dur && !skipping) { t += Time.deltaTime; f(Mathf.Clamp01(t / dur)); yield return null; }
        f(1f);
    }

    IEnumerator Say(int who, string text, float hold = 0f)
    {
        string name; Texture tex = null; Color col;
        Vector3 vp = Vector3.zero;
        if (who >= 0)
        {
            Frog f = Cast(who);
            name = Roster.Name(f.charId); tex = G.StoryPortrait(f.charId); col = Roster.UiColor(f.charId); vp = f.transform.position;
            if (!skipping)
            {
                if (Roster.IsFrog(f.charId)) Sfx.Ribbiting(vp, 0.6f);
                else { var c = Sfx.AnimalVoice(Roster.KindOf(f.charId) == Roster.Kind.Cat ? "Cat" : "Dog"); if (c != null) Sfx.Play(c, 0.5f, 1.1f); }
                talkFrog = f; talkT = 0.5f;
            }
        }
        else if (who == -1) { name = "THE MARS PUPS  (on the robot phone)"; col = new Color(1f, 0.75f, 0.4f); if (!skipping && Sfx.Bark != null) Sfx.Play(Sfx.Bark, 0.5f, 1.6f); }
        else { name = ""; col = Color.white; }
        Debug.Log("FFSTORY say " + name + ": " + text);
        advance = false;
        float t = 0f;
        while (!skipping)
        {
            t += Time.deltaTime;
            int n = Mathf.Min(text.Length, Mathf.FloorToInt(t * 48f));
            ui.Dialogue(name, tex, text.Substring(0, n), col, n >= text.Length ? NextHint() : "");
            if (advance) { advance = false; if (n < text.Length) t = 999f; else break; }
            if (demo && t > 1.6f + text.Length * 0.035f) break;
            if (hold > 0f && t > hold) break;
            yield return null;
        }
        ui.Dialogue(null, null, null, Color.white, "");
    }
    Frog talkFrog; float talkT;

    IEnumerator CineOn(bool canSkip = true)
    {
        cine = true; skippable = canSkip;
        if (RobotPhone.I != null) RobotPhone.I.open = false;
        float a0 = ui.letterbox;
        yield return Anim(0.45f, k => ui.letterbox = Mathf.Lerp(a0, 1f, k));
    }
    IEnumerator CineOff()
    {
        float a0 = ui.letterbox;
        yield return Anim(0.4f, k => ui.letterbox = Mathf.Lerp(a0, 0f, k));
        cine = false; camP = null; poses.Clear();
        ui.Dialogue(null, null, null, Color.white, "");
    }
    IEnumerator Title(string kick, string main, float hold)
    {
        ui.titleKick = kick; ui.titleText = main;
        Debug.Log("FFSTORY title " + kick + " " + main);
        yield return Anim(0.6f, k => ui.titleK = k);
        yield return Wait(hold);
        yield return Anim(0.6f, k => ui.titleK = 1f - k);
        ui.titleK = 0f;
    }
    IEnumerator FadeTo(float to, float dur) { float a0 = ui.fadeK; yield return Anim(dur, k => ui.fadeK = Mathf.Lerp(a0, to, k)); }

    // ---------------- camera ----------------
    System.Func<float, Vector3> camP, camL;
    float camT0, camDur = 1f, camShake;
    public Vector3 CamPos, CamLook; public float CamFov = 50f; public WorldId CamWorld = WorldId.Ranch;
    public bool CamOn { get { return Active && cine && camP != null; } }

    void Shot(Vector3 p0, Vector3 l0, Vector3 p1, Vector3 l1, float dur, float fov = 50f)
    {
        camP = k => Vector3.Lerp(p0, p1, Ease(k)); camL = k => Vector3.Lerp(l0, l1, Ease(k));
        camT0 = Time.time; camDur = Mathf.Max(0.01f, dur); CamFov = fov;
    }
    void Track(System.Func<Vector3> pos, System.Func<Vector3> look, float fov = 50f)
    {
        camP = k => pos(); camL = k => look(); camT0 = Time.time; camDur = 1f; CamFov = fov;
    }
    static float Ease(float k) { return k * k * (3f - 2f * k); }

    // Game calls this every LateUpdate before it places the story camera
    public void CamUpdate()
    {
        ApplyPoses();
        if (camP == null) return;
        float k = Mathf.Clamp01((Time.time - camT0) / camDur);
        CamPos = camP(k); CamLook = camL(k);
        if (camShake > 0f) { CamPos += Random.insideUnitSphere * camShake; camShake = Mathf.MoveTowards(camShake, 0f, Time.deltaTime * 0.8f); }
    }

    // a close shot on one cast member (in front of them, slightly to the side)
    void Close(int who, float side = 0.8f, float dist = 3.4f)
    {
        Frog f = Cast(who);
        Vector3 fw = PoseFwd(f);
        Vector3 rt = new Vector3(fw.z, 0f, -fw.x);
        Vector3 p = f.transform.position + Vector3.up * 0.75f;
        Shot(p + fw * dist + rt * side + Vector3.up * 0.55f, p, p + fw * (dist - 0.5f) + rt * side + Vector3.up * 0.5f, p, 3.5f, 42f);
    }

    // the facing the frog is SHOWN with: its cast pose when posed (applied in CamUpdate), else its transform
    Vector3 PoseFwd(Frog f)
    {
        Vector4 v;
        Vector3 fw = poses.TryGetValue(f, out v) ? Quaternion.Euler(0f, v.w, 0f) * Vector3.forward : f.transform.forward;
        fw.y = 0f; return fw.sqrMagnitude > 1e-4f ? fw.normalized : Vector3.forward;
    }

    // ---------------- cast poses ----------------
    readonly Dictionary<Frog, Vector4> poses = new Dictionary<Frog, Vector4>();
    readonly HashSet<Frog> slumped = new HashSet<Frog>();
    void Pose(Frog f, Vector3 p, float yaw)
    {
        if (f.vehicle != null) f.ExitVehicle();
        if (f.remote != null) f.remote.ReleaseManual(false);
        if (f.world == WorldId.Ranch) p.y = Ranch.GY(p.x, p.z) + 0.05f;
        else if (f.world == WorldId.Mars) p.y = Worlds.MarsO.y + SurfaceWorlds.MarsY(p.x - Worlds.MarsO.x, p.z - Worlds.MarsO.z) + 0.05f;
        poses[f] = new Vector4(p.x, p.y, p.z, yaw);
        f.Teleport(p);
    }
    void PoseCast(Vector3 c, float yaw, float spacing = 2.6f)
    {
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        Vector3 right = q * Vector3.right;
        for (int k = 0; k < 4; k++)
        {
            Frog f = Cast(k);
            if (f.world != WorldId.Ranch) { if (f.netPuppet) continue; f.SendTo(WorldId.Ranch, c, yaw); }
            float off = (k == 0 ? -0.5f : k == 1 ? -1.5f : k == 2 ? 0.5f : 1.5f) * spacing;
            Vector3 p = c + right * off - q * Vector3.forward * Mathf.Abs(off) * 0.25f;
            Pose(f, p, yaw);
        }
    }
    void ApplyPoses()
    {
        foreach (var kv in poses)
        {
            Frog f = kv.Key; Vector4 v = kv.Value;
            if (f == null) continue;
            Vector3 p = new Vector3(v.x, v.y, v.z);
            if ((f.transform.position - p).sqrMagnitude > 0.0025f) f.Teleport(p);
            float yaw = v.w;
            f.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
        if (talkFrog != null && talkT > 0f)
        {
            talkT -= Time.deltaTime;
            if (talkFrog.model != null) talkFrog.model.transform.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(talkT * 12f)) * 0.12f, 0f);
            if (talkT <= 0f && talkFrog.model != null) talkFrog.model.transform.localPosition = Vector3.zero;
        }
        foreach (var f in G.frogs)
        {
            if (f.model == null) continue;
            bool s = slumped.Contains(f);
            Quaternion want = s ? Quaternion.Euler(20f, 0f, 0f) : Quaternion.identity;
            f.model.transform.localRotation = Quaternion.Slerp(f.model.transform.localRotation, want, Mathf.Min(1f, Time.deltaTime * 3f));
            if (s && f != talkFrog) f.model.transform.localPosition = Vector3.Lerp(f.model.transform.localPosition, new Vector3(0f, -0.08f, 0f), Time.deltaTime * 3f);
        }
    }
    void Slump(Frog f, bool on)
    {
        if (on) slumped.Add(f); else { slumped.Remove(f); if (f.model != null) { f.model.transform.localRotation = Quaternion.identity; f.model.transform.localPosition = Vector3.zero; } }
    }
    void FaceAll(Vector3 at) { foreach (var f in new List<Frog>(poses.Keys)) { Vector4 v = poses[f]; Vector3 d = at - new Vector3(v.x, v.y, v.z); d.y = 0f; if (d.sqrMagnitude > 0.01f) { v.w = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; poses[f] = v; } } }

    Vector3 P { get { return StorySet.Pad; } }
    Vector3 RocketTop { get { return S.rocket.root.position + Vector3.up * 7f; } }
    Vector3 PadCast { get { return P + new Vector3(0f, 0f, 11f); } }

    // ---------------- the continue / start-over choice ----------------
    IEnumerator ChoiceCo(int c, int s, int p)
    {
        S.root.gameObject.SetActive(true);
        S.rocket.Show(c >= 3 ? 31 : p, c <= 2, false, c > 3, 0);
        S.ShowPickups(false, p);
        Sfx.Override = "story_faith";
        cine = true; skippable = false; choice = 0;
        ui.letterbox = 1f;
        Shot(P + new Vector3(18f, 6f, 30f), RocketTop, P + new Vector3(10f, 5f, 26f), RocketTop, 8f);
        bool done = c >= 8;
        ui.ShowChoice(done ? "You finished The Big Launch! Play it again?" : "Welcome back! Pick up where you left off?",
                      done ? "PLAY AGAIN FROM THE START" : "CONTINUE  ·  Chapter " + c + ": " + ChTitle[Mathf.Clamp(c, 1, 7)],
                      done ? "REPLAY CHAPTER 7  <size=16>(Y · N)</size>" : "START OVER  <size=16>(Y · N)</size>");
        advance = false;
        while (choice == 0) { if (advance) choice = 1; if (demo) choice = 2; yield return null; }
        ui.ShowChoice(null, null, null);
        cine = false; camP = null; ui.letterbox = 0f;
        Debug.Log("FFSTORY choice " + choice + " (saved ch " + c + ")");
        if (done) { if (choice == 1) Next(1, 0); else { parts = 31; Next(7, 0); } }
        else if (choice == 1) { parts = p; Next(c, c == 2 ? 0 : s); }
        else { parts = 0; Next(1, 0); }
    }

    // ---------------- 1. prologue ----------------
    IEnumerator IntroCo()
    {
        Vector3 g = S.GatherPos;
        PoseCast(g, 200f);
        yield return CineOn();
        Sfx.Override = "story_faith";
        ui.fadeK = 1f;
        Shot(g + new Vector3(30f, 18f, 40f), g + Vector3.up * 2f, g + new Vector3(12f, 6f, 16f), g + Vector3.up * 1.2f, 7f, 48f);
        yield return FadeTo(0f, 1.2f);
        yield return Title("FOUR FROGGIES PRESENT", "THE BIG LAUNCH", 2.2f);
        yield return Title("CHAPTER 1", ChTitle[1], 1.6f);
        // the robot phone rings
        for (int i = 0; i < 3 && !skipping; i++) { Sfx.Play(Sfx.Select != null ? Sfx.Select : Sfx.Click, 0.8f, 1.6f); yield return Wait(0.18f); Sfx.Play(Sfx.Select != null ? Sfx.Select : Sfx.Click, 0.8f, 1.9f); yield return Wait(0.5f); }
        Close(1);
        yield return Say(1, "Hey! The robot phone is ringing!");
        Close(0);
        yield return Say(0, "Who would call us all the way out here? ...Hello?");
        // hologram of the pups above the phone
        var holo = Hologram(Cast(0).transform.position + PoseFwd(Cast(0)) * 1.3f + Vector3.up * 1.4f);
        Shot(g + new Vector3(4f, 2.6f, 5.5f), holo.position, g + new Vector3(3f, 2.3f, 4.5f), holo.position, 5f, 45f);
        yield return Say(-1, "Woof! Hello? Is this the froggies' ranch? It's us - the Mars pups!");
        yield return Say(-1, "A big dust storm hit, and we're stuck in a cave on Mars. Our ride home blew away!");
        Close(2);
        yield return Say(2, "Stuck on Mars? Oh no!");
        Close(0, -0.8f);
        yield return Say(0, "Don't worry, pups. We'll come and get you!");
        Close(3);
        yield return Say(3, "But how? We'd need a rocket of our own...");
        Close(0);
        yield return Say(0, "Then we'll build one! Everybody to the launch pad!");
        Shot(g + new Vector3(4f, 2.6f, 5.5f), holo.position, g + new Vector3(5f, 3f, 7f), holo.position, 4f, 45f);
        yield return Say(-1, "Thank you! We'll be waiting... and hoping!");
        if (holo != null) Destroy(holo.gameObject);
        Sfx.Override = "story";
        Shot(g + new Vector3(-6f, 5f, 8f), g, g + new Vector3(-10f, 14f, 4f), P + Vector3.up * 4f, 3.5f, 50f);
        yield return Wait(2.2f);
        yield return CineOff();
        step = 1;
    }

    Transform Hologram(Vector3 at)
    {
        var root = new GameObject("PupHologram").transform;
        root.position = at;
        var d = Animal.Dog("", at, false);
        d.transform.SetParent(root, true);
        d.transform.localScale = Vector3.one * 0.8f;
        d.frozen = true;
        var m = new Material(Mats.Fx); m.color = new Color(0.35f, 0.85f, 1f, 0.55f);
        foreach (var r in d.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
        var disc = Mats.Prim(PrimitiveType.Cylinder, root, new Vector3(0f, -0.15f, 0f), new Vector3(0.9f, 0.01f, 0.9f), m);
        Mats.NoShadows(disc);
        root.gameObject.AddComponent<HoloSpin>();
        Sfx.Play(Sfx.Confirm != null ? Sfx.Confirm : Sfx.Pickup, 0.6f, 1.3f);
        return root;
    }

    // ---------------- blueprint (end of chapter 1) ----------------
    IEnumerator BlueprintCo()
    {
        PoseCast(PadCast, 180f);
        yield return CineOn();
        Shot(P + new Vector3(16f, 3f, 24f), P + Vector3.up * 6f, P + new Vector3(8f, 4.5f, 19f), P + Vector3.up * 7f, 5f, 50f);
        yield return Say(2, "Here's the rocket frame we started last summer. It just needs the important parts.");
        Shot(S.BoardPos + new Vector3(2.5f, 2.2f, 5f), S.BoardPos + Vector3.up * 2.4f, S.BoardPos + new Vector3(1f, 2.4f, 4.2f), S.BoardPos + Vector3.up * 2.5f, 4f, 45f);
        yield return Say(0, "The blueprint says: an engine, fuel tanks, a nose cone, fins and a computer chip.");
        Close(1);
        yield return Say(1, "The parts are all over the ranch, the house and the track. Let's split up!");
        Close(3);
        yield return Say(3, "And grab every fin you can find. Spares never hurt!");
        Shot(P + new Vector3(0f, 30f, 40f), P, P + new Vector3(-40f, 50f, 80f), P + new Vector3(0f, 0f, 60f), 4f, 55f);
        yield return Title("CHAPTER 2", ChTitle[2], 1.6f);
        yield return CineOff();
        Next(2, 0);
    }

    // ---------------- assembly (end of chapter 2) ----------------
    IEnumerator AssemblyCo()
    {
        if (RanchLife.I != null) { int n = 0; foreach (var r in RanchLife.I.robots) { if (r.manual != null) continue; r.Order("come", Lead); if (++n >= 3) break; } }
        PoseCast(PadCast, 180f);
        yield return CineOn();
        float a0 = Time.time;
        Track(() => { float a = (Time.time - a0) * 0.35f + 0.6f; return P + new Vector3(Mathf.Sin(a) * 17f, 5f + Mathf.Sin(a * 0.5f) * 2f, Mathf.Cos(a) * 17f); }, () => P + Vector3.up * 8f, 50f);
        for (int i = 0; i < 4 && !skipping; i++) { FX.Sparkle(S.rocket.SlotWorld(i), new Color(1f, 0.9f, 0.4f), 20); Sfx.Play(Sfx.Clank != null ? Sfx.Clank : Sfx.Click, 0.8f, 0.9f + i * 0.1f); yield return Wait(0.6f); }
        yield return Say(1, "Bolt it, wire it, tighten it... done!");
        yield return Say(0, "Look at her, everyone. Our very own rocket!");
        Close(2);
        yield return Say(2, "Let's test the engine on the ground before we fly. Safety first!");
        yield return Title("CHAPTER 3", ChTitle[3], 1.5f);
        yield return CineOff();
        Next(3, 0);
    }

    // ---------------- test fire -> burst tank ----------------
    IEnumerator TestFireCo()
    {
        PoseCast(S.PanelPos + new Vector3(-1f, 0f, 3f), 200f, 1.8f);
        yield return CineOn();
        Shot(P + new Vector3(20f, 2.5f, 22f), P + Vector3.up * 3f, P + new Vector3(16f, 2.2f, 18f), P + Vector3.up * 3f, 6f, 48f);
        for (int n = 3; n >= 1; n--) { ui.bigText = n.ToString(); ui.bigK = 1f; Sfx.Play(Sfx.Click, 0.9f, 1.5f); yield return Wait(0.8f); }
        ui.bigText = "IGNITION"; 
        var hum = Sfx.Loop(gameObject, Sfx.EngRocket != null ? Sfx.EngRocket : Sfx.Rotor);
        if (hum != null && !skipping) { hum.volume = 0.7f; hum.Play(); }
        yield return Anim(2.4f, k => { ui.bigK = 1f - k; StorySet.Flame(S.rocket, Mathf.Clamp01(k * 2f) * 0.7f, k > 0.6f ? (k - 0.6f) * 2f : 0f, Time.deltaTime); camShake = 0.12f * k; });
        // BANG: the right fuel tank bursts
        Vector3 tp = S.rocket.root.TransformPoint(new Vector3(1.95f, 3.5f, 0f));
        if (!skipping) { FX.Boom(tp, 1.2f); FX.Boom(tp + Vector3.up * 2f, 0.8f); Game.Shake(tp, 1f); Sfx.Play(Sfx.Boom, 1f, 0.8f); camShake = 0.6f; }
        if (hum != null) { hum.Stop(); Destroy(hum); }
        StorySet.Flame(S.rocket, 0f, 0f, 0f);
        step = 1; ApplyRocket();
        Sfx.Override = "story_tense";
        ui.bigK = 0f;
        yield return Wait(1.2f);
        Close(3);
        yield return Say(3, "Whoa! The fuel tank burst!");
        Close(1);
        yield return Say(1, "Is everybody okay?");
        Close(0);
        yield return Say(0, "We're fine. But the tank... all that work.");
        Close(2);
        yield return Say(2, "Hey, don't give up yet. We can patch it! Grab the tools.");
        yield return CineOff();
        Sfx.Override = "story";
    }

    // ---------------- quick repair (modal mini-game) ----------------
    IEnumerator RepairCo()
    {
        PoseCast(S.TankFixPos + new Vector3(1.5f, 0f, 1.5f), 230f, 1.6f);
        yield return CineOn(false);
        modal = true; modalHidesObj = true;
        Vector3 tp = S.rocket.root.TransformPoint(new Vector3(1.95f, 3f, 0f));
        Shot(tp + new Vector3(6f, 0.8f, 5f), tp, tp + new Vector3(5f, 0.6f, 4f), tp, 6f, 45f);
        int hits = 0; float t = 0f, zc = Random.Range(0.3f, 0.7f), zw = 0.2f, flash = 0f; string msg = "";
        advance = false;
        while (hits < 4)
        {
            t += Time.deltaTime;
            float spd = 1.4f + hits * 0.25f;
            float needle = 0.5f + 0.5f * Mathf.Sin(t * spd * 2f);
            bool inZone = Mathf.Abs(needle - zc) < zw * 0.5f;
            if (demo && inZone && Random.value < 0.25f) advance = true;
            if (advance)
            {
                advance = false;
                if (inZone)
                {
                    hits++; msg = "<color=#7dff8a>Nice weld!</color>";
                    FX.Sparkle(tp + Random.insideUnitSphere * 0.6f, new Color(1f, 0.8f, 0.3f), 24);
                    Sfx.Play(Sfx.Clank != null ? Sfx.Clank : Sfx.Click, 1f, 1f + hits * 0.1f);
                    zc = Random.Range(0.25f, 0.75f); zw = Mathf.Max(0.12f, zw - 0.02f);
                }
                else { msg = "<color=#ffb070>Missed - try again!</color>"; flash = 0.3f; Sfx.Play(Sfx.Bonk != null ? Sfx.Bonk : Sfx.Click, 0.6f, 0.8f); camShake = 0.08f; }
            }
            flash -= Time.deltaTime;
            ui.Minigame(true, needle, zc, zw, msg + "\nPress <b>A</b> (or tap) when the needle is in the green  ·  welds " + hits + " / 4");
            if (Random.value < 0.3f) FX.Smoke(tp, 1.2f, new Color(0.3f, 0.3f, 0.32f, 0.5f));
            yield return null;
        }
        ui.Minigame(false, 0, 0, 0, "");
        modal = false; modalHidesObj = false;
        step = 2; ApplyRocket();
        FX.Sparkle(tp, new Color(1f, 0.9f, 0.5f), 40);
        Sfx.Play(Sfx.Win, 0.8f, 1.1f);
        skippable = true;
        Close(2);
        yield return Say(2, "Patched and sealed! That's even stronger than before.");
        Close(0);
        yield return Say(0, "See? One step at a time. Now nothing can stop us!");
        yield return Title("CHAPTER 4", ChTitle[4], 1.4f);
        yield return CineOff();
        Next(4, 0);
    }

    // ---------------- the storm rolls in ----------------
    IEnumerator StormRollCo()
    {
        PoseCast(PadCast, 180f);
        yield return CineOn();
        Shot(P + new Vector3(10f, 3f, 22f), P + new Vector3(-60f, 40f, -20f), P + new Vector3(6f, 2.5f, 18f), P + new Vector3(-80f, 50f, -40f), 6f, 55f);
        Sfx.Override = "story_storm";
        yield return Anim(4f, k => { Worlds.StormK = k; SunK(1f - k * 0.6f); });
        if (!skipping) { Vector3 q = P + new Vector3(-50f, 0f, -30f); q.y = Ranch.GY(q.x, q.z); S.Strike(q); }
        Close(1);
        yield return Say(1, "Uh oh. Look at those clouds!");
        Close(0);
        yield return Say(0, "A storm! Quick - tie down the rocket and keep the flying junk away from it!");
        Close(3);
        yield return Say(3, "The robots are coming to help!");
        yield return CineOff();
        StormBegin();
    }

    // ---------------- storm damage ----------------
    IEnumerator StormDamageCo()
    {
        PoseCast(PadCast, 180f);
        yield return CineOn();
        Shot(P + new Vector3(-12f, 3f, 16f), P + Vector3.up * 3f, P + new Vector3(-9f, 2.6f, 13f), P + Vector3.up * 2.5f, 5f, 48f);
        if (!skipping) { Vector3 q = P + new Vector3(-6f, 0f, -8f); q.y = Ranch.GY(q.x, q.z); S.Strike(q); camShake = 0.5f; }
        yield return Wait(0.4f);
        // two fins snap off and tumble away
        for (int k = 0; k < 2 && !skipping; k++)
        {
            int fi = k == 0 ? 1 : 3;
            Vector3 fp = S.rocket.fins[fi].transform.position + Vector3.up;
            var g = Instantiate(S.fins[0].t.gameObject); g.SetActive(true); g.transform.position = fp;
            var rb = g.AddComponent<Rigidbody>(); rb.velocity = (fp - P).normalized * 6f + Vector3.up * 5f + Wind * 4f; rb.angularVelocity = Random.insideUnitSphere * 8f;
            Destroy(g, 5f);
            FX.Sparkle(fp, Color.white, 16);
            Sfx.Play(Sfx.Clank != null ? Sfx.Clank : Sfx.Thud, 0.9f, 0.7f);
            if (k == 0) { finsSnappedNow = true; S.rocket.Show(31, false, false, true, 1); } else S.rocket.Show(31, false, false, true, 2);
            yield return Wait(0.5f);
        }
        S.ClearStorm();
        yield return Anim(3f, k => Worlds.StormK = Mathf.Lerp(1f, 0.55f, k));
        Close(2);
        yield return Say(2, "We held on... but two fins snapped clean off!");
        Close(0);
        yield return Say(0, "The storm's easing up. Maybe she can still fly with two?");
        Close(1);
        yield return Say(1, "Let's try. The pups are counting on us!");
        yield return Title("CHAPTER 5", ChTitle[5], 1.4f);
        yield return CineOff();
        Next(5, 0);
    }
    bool finsSnappedNow;

    // ---------------- the first launch fails: the low point ----------------
    IEnumerator FailCo()
    {
        PoseCast(S.PanelPos + new Vector3(-2f, 0f, 4f), 200f, 1.8f);
        yield return CineOn();
        Shot(P + new Vector3(24f, 2f, 30f), P + Vector3.up * 6f, P + new Vector3(20f, 2f, 26f), P + Vector3.up * 8f, 5f, 50f);
        for (int n = 3; n >= 1; n--) { ui.bigText = n.ToString(); ui.bigK = 1f; Sfx.Play(Sfx.Click, 0.9f, 1.5f); yield return Wait(0.8f); }
        ui.bigText = "LIFTOFF!";
        var hum = Sfx.Loop(gameObject, Sfx.EngRocket != null ? Sfx.EngRocket : Sfx.Rotor);
        if (hum != null && !skipping) { hum.volume = 0.8f; hum.Play(); }
        Vector3 home = P + Vector3.up * StorySet.CradleH;
        Track(() => P + new Vector3(24f, 2f, 30f), () => S.rocket.root.position + Vector3.up * 6f, 50f);
        // up ~32 m, wobbling - then the engine coughs
        yield return Anim(3.6f, k => { ui.bigK = 1f - k; float h = 32f * k * k; S.rocket.root.position = home + Vector3.up * h; S.rocket.root.rotation = Quaternion.Euler(Mathf.Sin(k * 18f) * 4f * k, 0f, Mathf.Sin(k * 13f) * 6f * k); StorySet.Flame(S.rocket, 1f, k > 0.5f ? (k - 0.5f) * 1.6f : 0f, Time.deltaTime); camShake = 0.15f; });
        if (hum != null) hum.pitch = 0.7f;
        yield return Anim(1.2f, k => { S.rocket.root.position = home + Vector3.up * (32f + 2f * Mathf.Sin(k * 3.14f)); S.rocket.root.rotation = Quaternion.Euler(8f * k, 0f, -14f * k); StorySet.Flame(S.rocket, 1f - k, 0.9f, Time.deltaTime); if (hum != null) hum.volume = 0.8f * (1f - k); });
        if (hum != null) { hum.Stop(); Destroy(hum); }
        StorySet.Flame(S.rocket, 0f, 0f, 0f);
        Sfx.Override = "story_somber";
        // ...and falls back onto the pad
        Quaternion r0 = S.rocket.root.rotation;
        yield return Anim(1.5f, k => { float e = k * k; S.rocket.root.position = home + Vector3.up * 34f * (1f - e); S.rocket.root.rotation = Quaternion.Slerp(r0, Quaternion.Euler(0f, 0f, -4f), e); if (Random.value < 0.6f) FX.Smoke(S.rocket.root.position, Random.Range(1.5f, 3f), new Color(0.3f, 0.3f, 0.32f, 0.7f)); });
        S.rocket.root.position = home; S.rocket.root.rotation = Quaternion.Euler(0f, 0f, -4f);
        if (!skipping) { FX.Boom(home, 1f); for (int i = 0; i < 12; i++) FX.Smoke(home + Random.insideUnitSphere * 4f, Random.Range(2.5f, 4.5f), new Color(0.35f, 0.35f, 0.37f, 0.8f)); Game.Shake(home, 0.9f); Sfx.Play(Sfx.Thud != null ? Sfx.Thud : Sfx.Boom, 1f, 0.6f); Sfx.Play(Sfx.Boom, 0.6f, 0.5f); camShake = 0.5f; }
        ui.bigK = 0f;
        foreach (var f in G.frogs) Slump(f, true);
        yield return Anim(2f, k => ui.dimK = 0.55f * k);
        Shot(PadCast + new Vector3(3f, 1.6f, 6f), PadCast + Vector3.up * 0.6f, PadCast + new Vector3(1.5f, 1.3f, 4.5f), PadCast + Vector3.up * 0.5f, 9f, 45f);
        yield return Wait(1.5f);
        yield return Say(1, "It... it fell back down.");
        yield return Say(3, "We tried so hard.");
        yield return Say(0, "Maybe the pups will have to wait... forever.");
        ui.bigText = "It's over…?";
        Shot(P + new Vector3(0f, 2f, 36f), P + Vector3.up * 5f, P + new Vector3(0f, 6f, 44f), P + Vector3.up * 4f, 7f, 50f);
        yield return Anim(1.5f, k => ui.bigK = k);
        yield return Wait(4f);   // let it sit
        yield return Anim(1.5f, k => ui.bigK = 1f - k);
        S.rocket.root.rotation = Quaternion.identity;
        yield return CineOff();
        Next(6, 0);
    }

    // ---------------- the turn: keep going, have faith ----------------
    IEnumerator EncourageCo()
    {
        foreach (var f in G.frogs) Slump(f, true);
        ui.dimK = 0.55f;
        PoseCast(PadCast, 180f);
        yield return CineOn();
        Sfx.Override = "story_faith";
        Close(2, 0.6f, 3.6f);
        yield return Wait(1.2f);
        yield return Say(2, "Hey. Look at me, everyone.");
        Slump(Cast(2), false);
        yield return Anim(2f, k => ui.dimK = Mathf.Lerp(0.55f, 0.3f, k));
        yield return Say(2, "When the tank burst, we patched it. When the storm came, we held on.");
        yield return Say(2, "Nothing we did today was wasted. Keep going - and have a little faith.");
        Close(0);
        Slump(Cast(0), false);
        yield return Say(0, "...You're right. And wait - we still have the two spare fins we found!");
        Slump(Cast(1), false);
        Close(1);
        yield return Say(1, "And your patch held strong. And the chip we rescued has a backup flight plan!");
        Slump(Cast(3), false);
        Close(3);
        yield return Say(3, "Then let's fix her up. One more try!");
        foreach (var f in G.frogs) Slump(f, false);
        yield return Anim(1.5f, k => ui.dimK = Mathf.Lerp(0.3f, 0f, k));
        S.benchFins.gameObject.SetActive(true);
        Shot(S.BenchPos + new Vector3(3f, 2.5f, 5f), S.BenchPos + Vector3.up, S.BenchPos + new Vector3(2f, 2f, 4f), S.BenchPos + Vector3.up, 3f, 45f);
        yield return Wait(1.6f);
        yield return CineOff();
        step = 1; Save();
    }

    // ---------------- relaunch: the work pays off ----------------
    readonly List<Transform> clouds = new List<Transform>();
    IEnumerator RelaunchCo()
    {
        PoseCast(S.PanelPos + new Vector3(-2f, 0f, 4f), 200f, 1.8f);
        yield return CineOn();
        Shot(P + new Vector3(22f, 2f, 28f), P + Vector3.up * 6f, P + new Vector3(18f, 2f, 24f), P + Vector3.up * 8f, 5f, 50f);
        yield return Say(0, "Everyone ready? Here we go... with a little faith!");
        for (int n = 3; n >= 1; n--) { ui.bigText = n.ToString(); ui.bigK = 1f; Sfx.Play(Sfx.Click, 0.9f, 1.5f); yield return Wait(0.8f); }
        ui.bigText = "LIFTOFF!";
        Sfx.Override = "story_triumph";
        var hum = Sfx.Loop(gameObject, Sfx.EngRocket != null ? Sfx.EngRocket : Sfx.Rotor);
        if (hum != null && !skipping) { hum.volume = 0.9f; hum.Play(); }
        MakeClouds();
        Vector3 home = P + Vector3.up * StorySet.CradleH;
        float h = 0f;
        Track(() => P + new Vector3(24f, 2f + h * 0.15f, 30f), () => S.rocket.root.position + Vector3.up * 6f, 50f);
        yield return Anim(4f, k => { ui.bigK = 1f - k; h = 30f * k * k; S.rocket.root.position = home + Vector3.up * h; StorySet.Flame(S.rocket, 1f, 0f, Time.deltaTime); camShake = 0.12f * (1f - k); for (int i = 0; i < 2; i++) FX.Smoke(P + Random.insideUnitSphere * 6f, Random.Range(2f, 4f), new Color(0.8f, 0.8f, 0.82f, 0.6f)); });
        ui.bigK = 0f;
        // chase it up through the clouds into the sunshine
        Track(() => S.rocket.root.position + new Vector3(10f, -7f, 16f), () => S.rocket.root.position + Vector3.up * 9f, 55f);
        float sky0 = Worlds.StormK;
        yield return Anim(9f, k => { h = 30f + 380f * k * k + 60f * k; S.rocket.root.position = home + Vector3.up * h; StorySet.Flame(S.rocket, 1f, 0f, Time.deltaTime); Worlds.StormK = Mathf.Lerp(sky0, 0f, Mathf.Clamp01(k * 1.6f)); SunK(Mathf.Lerp(0.7f, 1.25f, Mathf.Clamp01(k * 1.6f))); foreach (var c in clouds) if (c != null) c.position += Vector3.right * Time.deltaTime * 6f * (c.position.x > P.x ? 1f : -1f); });
        if (!skipping) yield return Say(1, "She's flying! She's really flying!", 2.6f);
        if (!skipping) yield return Say(3, "Hang on, pups - we're coming!", 2.6f);
        yield return FadeTo(1f, 0.8f);
        if (hum != null) { hum.Stop(); Destroy(hum); }
        StorySet.Flame(S.rocket, 0f, 0f, 0f);
        foreach (var c in clouds) if (c != null) Destroy(c.gameObject);
        clouds.Clear();
        SunK(1f); Worlds.StormK = 0f;
        RocketHome(); S.rocket.root.gameObject.SetActive(false);
        // deep space: past Earth towards Mars
        yield return SpaceTrip(true);
        // Mars landing
        MarsArrive(true);
        CamWorld = WorldId.Mars;
        Vector3 mh = S.MarsRocketPos + Vector3.up * StorySet.CradleH;
        foreach (var f in Humans()) { f.model.gameObject.SetActive(false); }
        Track(() => S.MarsRocketPos + new Vector3(18f, 4f, 22f), () => S.marsRocket.root.position + Vector3.up * 5f, 50f);
        yield return FadeTo(0f, 0.8f);
        yield return Anim(4f, k => { float e = 1f - (1f - k) * (1f - k); S.marsRocket.root.position = mh + Vector3.up * 70f * (1f - e); StorySet.Flame(S.marsRocket, 0.7f, 0f, Time.deltaTime); if (k > 0.7f) FX.Dust(S.MarsRocketPos + Random.insideUnitSphere * 5f, 1f); });
        S.marsRocket.root.position = mh;
        StorySet.Flame(S.marsRocket, 0f, 0f, 0f);
        foreach (var f in Humans()) f.model.gameObject.SetActive(true);
        Sfx.Override = "story";
        yield return Title("CHAPTER 7", ChTitle[7], 1.6f);
        yield return Say(0, "Mars! The pups' cave is just north of here. Let's go!");
        yield return CineOff();
        CamWorld = WorldId.Ranch;
        S.rocket.root.gameObject.SetActive(true);
        Next(7, 0);
    }

    void MakeClouds()
    {
        foreach (var c in clouds) if (c != null) Destroy(c.gameObject);
        clouds.Clear();
        var m = new Material(Mats.Fx); m.color = new Color(0.86f, 0.88f, 0.92f, 0.75f);
        var rnd = new System.Random(4);
        int n = Look.Mobile ? 18 : 34;
        for (int i = 0; i < n; i++)
        {
            float a = (float)rnd.NextDouble() * 6.28f, d = 6f + (float)rnd.NextDouble() * 60f;
            Vector3 p = P + new Vector3(Mathf.Cos(a) * d, 150f + (float)rnd.NextDouble() * 40f, Mathf.Sin(a) * d);
            var q = Mats.Prim(PrimitiveType.Quad, null, p, Vector3.one * (30f + (float)rnd.NextDouble() * 30f), m);
            Mats.NoShadows(q);
            q.AddComponent<Billboard>();
            clouds.Add(q.transform);
        }
    }

    IEnumerator SpaceTrip(bool outbound)
    {
        S.BuildSpace();
        S.spaceRoot.gameObject.SetActive(true);
        CamWorld = WorldId.Space;
        Vector3 V = StorySet.SpaceV;
        Vector3 a = outbound ? V + new Vector3(0f, 0f, -400f) : V + new Vector3(80f, -20f, 900f);
        Vector3 b = outbound ? V + new Vector3(60f, -20f, 700f) : V + new Vector3(-150f, 40f, -500f);
        Quaternion face = Quaternion.LookRotation((b - a).normalized) * Quaternion.Euler(90f, 0f, 0f);
        S.spaceRocket.rotation = face;
        S.spaceRocket.position = a;
        Vector3 look = outbound ? S.spaceMars.position : S.spaceEarth.position;
        Track(() => S.spaceRocket.position - (b - a).normalized * 26f + Vector3.up * 6f + Vector3.right * 9f, () => Vector3.Lerp(S.spaceRocket.position, look, 0.25f), 50f);
        yield return FadeTo(0f, 0.8f);
        yield return Anim(7f, k => { S.spaceRocket.position = Vector3.Lerp(a, b, k); StorySet.Flame(S.spaceRocketRig, 0.8f, 0f, Time.deltaTime); if (S.spaceStars != null) S.spaceStars.position = CamPos; });
        if (!skipping) yield return Say(-2, outbound ? "Next stop: Mars!" : "Next stop: home!", 2.2f);
        yield return FadeTo(1f, 0.8f);
        StorySet.Flame(S.spaceRocketRig, 0f, 0f, 0f);
        S.spaceRoot.gameObject.SetActive(false);
    }

    // ---------------- finale: rescue, home, celebration, credits ----------------
    IEnumerator HomeCo()
    {
        CamWorld = WorldId.Mars;
        var hs = Humans();
        Vector3 mr = S.MarsRocketPos;
        for (int k = 0; k < hs.Count; k++) Pose(hs[k], mr + new Vector3(-3f + k * 2f, 0f, 8f), 180f);
        yield return CineOn();
        Shot(mr + new Vector3(10f, 2.5f, 16f), mr + Vector3.up * 1.5f, mr + new Vector3(7f, 2f, 12f), mr + Vector3.up * 1.2f, 5f, 48f);
        yield return Say(-1, "You came! You really came all the way to Mars!");
        yield return Say(0, "We promised, didn't we? Everybody aboard - we're going home!");
        yield return FadeTo(1f, 0.8f);
        foreach (var p in S.pups) p.gameObject.SetActive(false);
        foreach (var f in hs) f.model.gameObject.SetActive(false);
        Vector3 mh = mr + Vector3.up * StorySet.CradleH;
        Track(() => mr + new Vector3(20f, 3f, 24f), () => S.marsRocket.root.position + Vector3.up * 5f, 50f);
        yield return FadeTo(0f, 0.6f);
        Sfx.Override = "story_triumph";
        yield return Anim(4f, k => { S.marsRocket.root.position = mh + Vector3.up * 120f * k * k; StorySet.Flame(S.marsRocket, 1f, 0f, Time.deltaTime); if (k < 0.3f) FX.Dust(mr + Random.insideUnitSphere * 6f, 1f); });
        yield return FadeTo(1f, 0.6f);
        StorySet.Flame(S.marsRocket, 0f, 0f, 0f);
        S.marsRocket.root.position = mh;
        yield return SpaceTrip(false);
        // home: the ranch rocket comes down on its pad, everyone (and four new friends) step out
        CamWorld = WorldId.Ranch;
        int i = 0;
        foreach (var f in hs) { f.SendTo(WorldId.Ranch, PadCast + new Vector3(-3f + i * 2f, 0.4f, 0f), 180f); f.model.gameObject.SetActive(true); i++; }
        foreach (var p in S.pups) p.gameObject.SetActive(true);
        S.MarsActive(false);
        S.rocket.root.gameObject.SetActive(true);
        S.rocket.Show(31, false, false, true, 0);
        Vector3 home = P + Vector3.up * StorySet.CradleH;
        Track(() => P + new Vector3(22f, 4f, 30f), () => S.rocket.root.position + Vector3.up * 6f, 50f);
        Worlds.StormK = 0f; SunK(1.1f);
        foreach (var f in G.frogs) f.model.gameObject.SetActive(false);
        yield return FadeTo(0f, 0.8f);
        yield return Anim(4f, k => { float e = 1f - (1f - k) * (1f - k); S.rocket.root.position = home + Vector3.up * 90f * (1f - e); StorySet.Flame(S.rocket, 0.7f, 0f, Time.deltaTime); if (k > 0.7f) FX.Dust(P + Random.insideUnitSphere * 6f, 1f); });
        RocketHome();
        foreach (var f in G.frogs) f.model.gameObject.SetActive(true);
        PoseCast(PadCast, 180f);
        S.HomePups(PadCast + new Vector3(0f, 0f, 3f));
        FaceAll(PadCast + new Vector3(0f, 0f, 12f));
        if (RanchLife.I != null) foreach (var r in RanchLife.I.robots) if (r.manual == null) r.Order("dance", Lead);
        // celebration with fireworks
        fireworks = true;
        Shot(PadCast + new Vector3(0f, 2.2f, 10f), PadCast + Vector3.up * 1.2f, PadCast + new Vector3(0f, 2f, 8f), PadCast + Vector3.up, 6f, 50f);
        yield return Say(2, "Welcome to the ranch, pups!");
        yield return Say(-1, "Woof! Best. Rescue. Ever!");
        Close(0);
        yield return Say(0, "We almost gave up today... but we kept going.");
        Close(1);
        yield return Say(1, "And everything we worked for came together in the end.");
        Shot(P + new Vector3(30f, 8f, 40f), P + Vector3.up * 20f, P + new Vector3(20f, 4f, 28f), P + Vector3.up * 25f, 18f, 55f);
        // credits
        ui.SetCredits(Credits());
        yield return Anim(1.5f, k => ui.creditsK = k);
        yield return Wait(skipping ? 0f : 9f);
        yield return Anim(1f, k => ui.creditsK = 1f - k);
        ui.bigText = "All is well.";
        yield return Anim(1.5f, k => ui.bigK = k);
        yield return Wait(3.5f);
        yield return Anim(1.5f, k => ui.bigK = 1f - k);
        fireworks = false;
        yield return CineOff();
        ch = 8; step = 0; Save();
        ToastAll("THE END - thanks for playing! The ranch is yours: free play continues.", 6f);
        Debug.Log("FFSTORY complete");
        Sfx.Override = null;
        Active = false;
        Stop2();
    }

    // leave the finished story: the rocket stays on its pad as a trophy, weather and music back to normal
    void Stop2()
    {
        Active = true; Stop();
        S.root.gameObject.SetActive(true);
        S.ShowPickups(false, 31);
        foreach (var a in S.anchors) a.gameObject.SetActive(false);
        S.rocket.Show(31, false, false, true, 0);
        for (int k = 0; k < 4; k++) S.simonGlow[k].transform.parent.gameObject.SetActive(true);
    }

    string Credits()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<size=40><b>THE BIG LAUNCH</b></size>\n<size=20>a Four Froggies story</size>\n\n");
        sb.Append("<size=22><color=#ffd27a>STARRING</color></size>\n");
        for (int k = 0; k < 4; k++) sb.Append(Roster.Name(Cast(k).charId) + "\n");
        sb.Append("the Mars pups\nand the ranch robots\n\n");
        sb.Append("<size=22><color=#ffd27a>STORY, SCORE &amp; ROCKET</color></size>\nmade for this game\n\n");
        sb.Append("<size=20>Thanks for never giving up.</size>");
        return sb.ToString().Replace("&amp;", "&");
    }

    bool fireworks; float fwT;
    void LateUpdate()
    {
        if (!fireworks || S == null) return;
        fwT -= Time.deltaTime;
        if (fwT <= 0f) { fwT = Random.Range(0.35f, 0.8f); S.Firework(P + new Vector3(Random.Range(-30f, 30f), Random.Range(28f, 48f), Random.Range(-20f, 10f))); }
    }
}

// slow spin + flicker for the pups' phone hologram
public class HoloSpin : MonoBehaviour
{
    void Update()
    {
        transform.Rotate(0f, 40f * Time.deltaTime, 0f);
        float s = 1f + (Random.value < 0.06f ? -0.12f : 0f);
        transform.localScale = new Vector3(1f, s, 1f);
    }
}
