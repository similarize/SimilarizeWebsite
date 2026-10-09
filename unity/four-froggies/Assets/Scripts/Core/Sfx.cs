using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

// Sound for Four Froggies (ffu10 sound-design pass).
// - Effects + engine loops: 16-bit mono PCM TextAssets in Resources/Audio/<name>.bytes ("FFP1" + rate + samples),
//   built offline by work/audio/ff/build_audio.py from CC0 recordings (BigSoundBank.com, Kenney) and synthesis.
//   PCM (AudioClip.Create) keeps one-shots tight and engine loops sample-exact in WebGL (no AAC encoder padding).
// - Music + ambience: seamless Ogg loops in web/audio/, streamed per stage by Plugins/WebGL/FFAudio.jslib (Web Audio).
// - Volume ON / LOW / OFF (M key, SOUND button, touch SND, help Y) drives both AudioListener.volume and the JS bus.
// Anything missing falls back to the old synthesised sounds, so the game never goes silent on a bad asset.
public static class Sfx
{
    public const int Rate = 22050;      // fallback synthesis rate (LaunchSeq builds its rumble with it)
    public static AudioClip Hop, Splash, Door, Shell, Missile, Boom, Pickup, Win, Click, Step, Thud;
    public static AudioClip EngineCar, EngineTank, Rotor, DroneWhine, BoatMotor, Bubbles;
    // ffu10 sound set
    public static AudioClip SplashBig, Select, Confirm, Toggle, StepMetal, Land, BumpSoft, Bonk, Skid, Clank, Tracks, Wash;
    public static AudioClip EngEV, EngV8, EngDiesel, EngSub, EngRocket, EngServo;
    public static AudioClip[] Ribbit = new AudioClip[0], Foot = new AudioClip[0], Crash = new AudioClip[0], Servo = new AudioClip[0];
    public static AudioClip Moo, Neigh, Bleat, Baa, Cluck, Rooster, Bark, Meow, Oink;

    static GameObject host;
    static AudioSource[] pool;
    static int next;
    static string mood = "", amb = "";
    public static int Level { get; private set; }     // 0 = full, 1 = low, 2 = off
    static readonly float[] Levels = { 1f, 0.35f, 0f };
    public static string LevelName { get { return Level == 0 ? "ON" : Level == 1 ? "LOW" : "OFF"; } }
    public const string AudioV = "a1";                // cache-buster for web/audio/*.ogg
    public static Vector3 ListenerPos;                 // P1's frog (set by Game) - picks the pond ambience
    static int loaded, fallback;

    static System.Random rnd = new System.Random(3);
    static float N() { return (float)(rnd.NextDouble() * 2.0 - 1.0); }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void FFAudio_Init();
    [DllImport("__Internal")] static extern void FFAudio_Play(int chan, string url, float vol, float fade);
    [DllImport("__Internal")] static extern void FFAudio_Master(float v);
    [DllImport("__Internal")] static extern void FFAudio_Lowpass(float hz);
#else
    static void FFAudio_Init() { }
    static void FFAudio_Play(int chan, string url, float vol, float fade) { }
    static void FFAudio_Master(float v) { }
    static void FFAudio_Lowpass(float hz) { }
#endif

    public static void Init()
    {
        if (host != null) return;
        host = new GameObject("Audio");
        Object.DontDestroyOnLoad(host);
        host.AddComponent<AudioListener>();
        pool = new AudioSource[16];
        for (int i = 0; i < pool.Length; i++) { pool[i] = host.AddComponent<AudioSource>(); pool[i].playOnAwake = false; pool[i].spatialBlend = 0f; }
        host.AddComponent<SfxDriver>();
        Level = Mathf.Clamp(PlayerPrefs.GetInt("ff.sound", 0), 0, 2);
        AudioListener.volume = Levels[Level];
        try { FFAudio_Init(); FFAudio_Master(Levels[Level]); } catch (System.Exception e) { Debug.LogWarning("FFAudio init failed: " + e.Message); }
        LoadAll();
        BuildFallback();
        Debug.Log("Sfx: " + loaded + " PCM clips loaded, " + fallback + " synthesised fallbacks");
    }

    public static void CycleVolume()
    {
        Level = (Level + 1) % 3;
        AudioListener.volume = Levels[Level];
        PlayerPrefs.SetInt("ff.sound", Level);
        try { FFAudio_Master(Levels[Level]); } catch { }
        Play(Toggle != null ? Toggle : Click, 1f);
    }

    public static void Play(AudioClip c, float vol, float pitch = 1f)
    {
        if (c == null || pool == null) return;
        AudioSource s = pool[next];
        next = (next + 1) % pool.Length;
        s.pitch = pitch;
        s.PlayOneShot(c, vol);
    }

    public static AudioClip Pick(AudioClip[] set) { return set == null || set.Length == 0 ? null : set[rnd.Next(set.Length)]; }

    // positional-ish: louder the closer it is to the nearest human froggy
    public static void PlayAt(AudioClip c, Vector3 p, float vol, float range = 90f, float pitch = 1f)
    {
        float d = 1e9f;
        if (Game.I != null)
            foreach (var f in Game.I.frogs) if (f != null && f.human) d = Mathf.Min(d, (f.FocusPoint - p).magnitude);
        if (d > 1e8f) d = 30f;
        float k = Mathf.Clamp01(1f - d / range);
        if (k > 0.02f) Play(c, vol * k * k, pitch);
    }

    public static float Near(Vector3 p)
    {
        float d = 1e9f;
        if (Game.I != null)
            foreach (var f in Game.I.frogs) if (f != null && f.human) d = Mathf.Min(d, (f.FocusPoint - p).magnitude);
        return d;
    }

    public static AudioSource Loop(GameObject owner, AudioClip c)
    {
        var s = owner.AddComponent<AudioSource>();
        s.clip = c; s.loop = true; s.playOnAwake = false; s.volume = 0f; s.spatialBlend = 0f;
        return s;
    }

    // ---------------- music + ambience (streamed) ----------------
    static string Track(string m)
    {
        switch (m)
        {
            case "lobby": return "mus_lobby";
            case "house": return "mus_house";
            case "underwater": return "mus_under";
            case "space": return "mus_space";
            case "mars": return "mus_mars";
            case "callisto": return "mus_callisto";
            default: return "mus_ranch";
        }
    }
    static string Bed(string m)
    {
        switch (m)
        {
            case "house": return "amb_house";
            case "underwater": return "amb_under";
            case "space": case "callisto": return "amb_space";
            case "mars": return "amb_mars";
            case "lobby": return "amb_ranch";
            default: return Layout.PondQ(ListenerPos.x, ListenerPos.z) < 1.25f ? "amb_pond" : "amb_ranch";
        }
    }
    static float MusicVol(string m) { return m == "lobby" ? 0.42f : m == "space" ? 0.34f : 0.36f; }
    static float BedVol(string m) { return m == "lobby" ? 0.25f : m == "underwater" ? 0.55f : m == "space" || m == "callisto" ? 0.45f : 0.5f; }

    public static void Music(string m)
    {
        if (pool == null) return;
        string b = Bed(m);
        if (m == mood && b == amb) return;
        try
        {
            if (m != mood)
            {
                FFAudio_Play(0, "audio/" + Track(m) + ".ogg?v=" + AudioV, MusicVol(m), mood.Length == 0 ? 0.8f : 1.6f);
                FFAudio_Lowpass(m == "underwater" ? 2600f : 20000f);
            }
            if (b != amb) FFAudio_Play(1, "audio/" + b + ".ogg?v=" + AudioV, BedVol(m), 2.5f);
        }
        catch (System.Exception e) { Debug.LogWarning("FFAudio play failed: " + e.Message); }
        mood = m; amb = b;
    }

    public static void TickMusic(float dt) { }

    // ---------------- engines ----------------
    // One voice per vehicle: the main loop pitched / levelled from speed + throttle, plus an optional second layer
    // (track rattle, boat wash) and a tyre-skid layer for wheeled vehicles. Kinds: 0 petrol car, 1 tracked diesel
    // (Ripsaw EV2 / M5), 2 helicopter, 3 passenger drone, 4 boat, 5 mech suit (servo hum; steps elsewhere), 7 electric
    // (Cybertruck, rover), 8 monster V8, 9 submarine, 10 Starship rocket.
    public class EngineVoice
    {
        public AudioSource main, layer, skid, extra;
        readonly int kind;
        readonly float pitchMul;
        float clankT;
        public EngineVoice(GameObject owner, int kind, float pitchMul)
        {
            this.kind = kind; this.pitchMul = pitchMul;
            AudioClip c = ClipFor(kind);
            if (c != null) { main = Loop(owner, c); main.Play(); main.Pause(); }
            AudioClip l = kind == 1 ? Tracks : kind == 4 ? Wash : null;
            if (l != null) { layer = Loop(owner, l); layer.Play(); layer.Pause(); }
            if ((kind == 0 || kind == 7 || kind == 8) && Skid != null) { skid = Loop(owner, Skid); skid.Play(); skid.Pause(); }
            if (kind == 11)
            {
                // ffu11 Cyberboat: electric jet-pump whine (main) + water wash (layer) + hull slap / impeller rumble (extra)
                if (Wash != null) { layer = Loop(owner, Wash); layer.Play(); layer.Pause(); }
                AudioClip ex = BoatMotor != null ? BoatMotor : EngineCar;
                if (ex != null) { extra = Loop(owner, ex); extra.Play(); extra.Pause(); }
            }
        }
        public void Kill()
        {
            foreach (var s in new[] { main, layer, skid, extra }) if (s != null) Object.Destroy(s);
            main = layer = skid = extra = null;
        }
        static AudioClip ClipFor(int k)
        {
            switch (k)
            {
                case 1: return EngDiesel != null ? EngDiesel : EngineTank;
                case 2: return Rotor;
                case 3: return DroneWhine;
                case 4: return BoatMotor;
                case 5: return EngServo;
                case 7: return EngEV != null ? EngEV : EngineCar;
                case 8: return EngV8 != null ? EngV8 : EngineCar;
                case 9: return EngSub != null ? EngSub : BoatMotor;
                case 10: return EngRocket != null ? EngRocket : Rotor;
                case 11: return EngEV != null ? EngEV : (DroneWhine != null ? DroneWhine : EngineCar);
                default: return EngineCar;
            }
        }
        static void Set(AudioSource s, float vol, float pitch, float dt)
        {
            if (s == null) return;
            s.volume = Mathf.MoveTowards(s.volume, vol, dt * 1.2f);
            s.pitch = Mathf.Lerp(s.pitch, Mathf.Max(0.05f, pitch), Mathf.Min(1f, dt * 4f));
            if (s.volume <= 0.001f) { if (s.isPlaying) s.Pause(); }
            else if (!s.isPlaying) s.UnPause();
        }
        // on: someone aboard (or an autopilot); spd m/s; load 0..1 throttle / climb; slip m/s sideways; ground 0..1
        public void Tick(bool on, float spd, float load, float slip, float ground, Vector3 pos, float dt)
        {
            float v = 0f, p = 1f, lv = 0f, lp = 1f;
            switch (kind)
            {
                case 1: v = 0.32f + load * 0.12f; p = 0.72f + spd / 30f + load * 0.18f; lv = Mathf.Clamp01(spd / 8f) * 0.32f * ground; lp = 0.75f + spd / 22f; break;
                case 2: v = 0.36f; p = 0.86f + spd / 70f + load * 0.1f; break;
                case 3: v = 0.3f; p = 0.82f + spd / 45f + load * 0.18f; break;
                case 4: v = 0.26f + load * 0.1f; p = 0.7f + spd / 20f + load * 0.25f; lv = Mathf.Clamp01(spd / 10f) * 0.4f; lp = 0.8f + spd / 35f; break;
                case 5: v = Mathf.Clamp01(spd / 4f) * 0.12f; p = 0.9f + spd / 10f; break;
                case 7: v = 0.06f + Mathf.Clamp01(spd / 12f) * 0.22f + load * 0.06f; p = 0.45f + spd / 15f + load * 0.08f; break;
                case 8: v = 0.36f + load * 0.12f; p = 0.78f + spd / 24f + load * 0.25f; break;
                case 9: v = 0.25f + load * 0.1f; p = 0.8f + spd / 12f; break;
                case 10: v = 0.22f + load * 0.35f; p = 0.85f + load * 0.25f; break;
                case 11: v = 0.1f + Mathf.Clamp01(spd / 22f) * 0.26f + load * 0.1f; p = 0.62f + spd / 13f + load * 0.15f; lv = Mathf.Clamp01(spd / 8f) * 0.42f + 0.06f; lp = 0.85f + spd / 40f; break;
                default: v = 0.28f + load * 0.1f; p = 0.8f + spd / 28f + load * 0.2f; break;
            }
            if (!on) { v = 0f; lv = 0f; }
            Set(main, v, p * pitchMul, dt);
            Set(layer, lv, lp, dt);
            float sk = on ? Mathf.Clamp01((Mathf.Abs(slip) - 3.5f) / 6f) * ground * Mathf.Clamp01(spd / 5f) * 0.35f : 0f;
            Set(skid, sk, 0.9f + Mathf.Abs(slip) / 30f, dt * 2f);
            if (extra != null) Set(extra, on ? 0.1f + load * 0.14f : 0f, 0.95f + spd / 18f + load * 0.15f, dt);
            // tracked: metal clanks from the links, faster with speed
            if (kind == 1 && on && spd > 1.2f && ground > 0.3f && Clank != null)
            {
                clankT -= dt * spd;
                if (clankT <= 0f) { clankT = Random.Range(0.6f, 1.6f); Play(Clank, Random.Range(0.12f, 0.22f) * Mathf.Clamp01(spd / 6f), Random.Range(0.8f, 1.15f)); }
            }
        }
    }

    // ---------------- loading ----------------
    static AudioClip L(string name)
    {
        var ta = Resources.Load<TextAsset>("Audio/" + name);
        if (ta == null) return null;
        try
        {
            byte[] b = ta.bytes;
            if (b.Length < 12 || b[0] != 'F' || b[1] != 'F' || b[2] != 'P' || b[3] != '1') return null;
            int rate = System.BitConverter.ToInt32(b, 4), n = System.BitConverter.ToInt32(b, 8);
            n = Mathf.Min(n, (b.Length - 12) / 2);
            if (n <= 0) return null;
            var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = (short)(b[12 + 2 * i] | (b[13 + 2 * i] << 8)) / 32768f;
            var c = AudioClip.Create(name, n, 1, rate, false);
            c.SetData(d, 0);
            loaded++;
            return c;
        }
        catch (System.Exception e) { Debug.LogWarning("Sfx " + name + ": " + e.Message); return null; }
    }
    static AudioClip[] Set(params string[] names)
    {
        var l = new List<AudioClip>();
        foreach (var n in names) { var c = L(n); if (c != null) l.Add(c); }
        return l.ToArray();
    }

    static void LoadAll()
    {
        Hop = L("hop"); Splash = L("splash"); SplashBig = L("splash_big"); Door = L("door");
        Click = L("click"); Select = L("select"); Confirm = L("confirm"); Toggle = L("toggle");
        Pickup = L("pickup"); Win = L("win"); Shell = L("cannon"); Boom = L("boom"); Missile = L("missile");
        Step = L("step"); Thud = Step; StepMetal = L("step_metal"); Land = L("land"); BumpSoft = L("bump_soft"); Bonk = L("bonk");
        Ribbit = Set("ribbit", "ribbit2"); Foot = Set("foot0", "foot1", "foot2", "foot3"); Crash = Set("crash_metal", "crash_metal2");
        Servo = Set("servo0", "servo1", "servo2");
        Moo = L("moo"); Neigh = L("neigh"); Bleat = L("bleat"); Baa = L("baa"); Cluck = L("cluck"); Rooster = L("rooster");
        Bark = L("bark"); Meow = L("meow"); Oink = L("oink");
        Skid = L("skid"); Clank = L("clank"); Tracks = L("tracks"); Wash = L("wash");
        EngineCar = L("eng_car"); EngEV = L("eng_ev"); EngV8 = L("eng_v8"); EngDiesel = L("eng_diesel"); EngineTank = EngDiesel;
        Rotor = L("eng_rotor"); DroneWhine = L("eng_drone"); BoatMotor = L("eng_boat"); EngSub = L("eng_sub");
        EngRocket = L("eng_rocket"); EngServo = L("eng_servo");
    }

    // ---------------- synthesised fallbacks (the pre-ffu10 sounds) ----------------
    static AudioClip Clip(string name, float[] d)
    {
        var c = AudioClip.Create(name, d.Length, 1, Rate, false);
        c.SetData(d, 0);
        fallback++;
        return c;
    }
    static float[] Buf(float secs) { return new float[Mathf.Max(1, Mathf.RoundToInt(secs * Rate))]; }
    static float Env(float t, float a, float dur) { return t < a ? t / a : Mathf.Clamp01(1f - (t - a) / Mathf.Max(0.0001f, dur - a)); }
    static float Saw(float x) { return 2f * (x - Mathf.Floor(x + 0.5f)); }
    static float Sq(float x) { return (x - Mathf.Floor(x)) < 0.5f ? 1f : -1f; }

    static void BuildFallback()
    {
        float[] d; float lp;
        if (Hop == null) { d = Buf(0.16f); float ph = 0f; for (int i = 0; i < d.Length; i++) { float t = i / (float)Rate; float f = 280f + 520f * Mathf.Sqrt(t / 0.16f); ph += f / Rate; d[i] = Mathf.Sin(ph * 6.2832f) * Env(t, 0.005f, 0.16f) * 0.6f; } Hop = Clip("hop", d); }
        if (Splash == null) { d = Buf(0.45f); lp = 0f; for (int i = 0; i < d.Length; i++) { float t = i / (float)Rate; lp += (N() - lp) * 0.35f; d[i] = lp * Mathf.Pow(Env(t, 0.01f, 0.45f), 2f) * 0.9f; } Splash = Clip("splash", d); }
        if (SplashBig == null) SplashBig = Splash;
        if (Door == null) { d = Buf(0.22f); for (int i = 0; i < d.Length; i++) { float t = i / (float)Rate; float a = t < 0.09f ? Env(t, 0.003f, 0.09f) : Env(t - 0.11f, 0.003f, 0.11f); d[i] = (Mathf.Sin(t * 6.2832f * 140f) * 0.6f + N() * 0.25f) * a * 0.7f; } Door = Clip("door", d); }
        if (Click == null) { d = Buf(0.04f); for (int i = 0; i < d.Length; i++) { float t = i / (float)Rate; d[i] = Mathf.Sin(t * 6.2832f * 1800f) * Env(t, 0.001f, 0.04f) * 0.5f; } Click = Clip("click", d); }
        if (Shell == null) { d = Buf(0.5f); lp = 0f; for (int i = 0; i < d.Length; i++) { float t = i / (float)Rate; lp += (N() - lp) * 0.5f; d[i] = (lp * Mathf.Exp(-t * 18f) + Mathf.Sin(t * 6.2832f * (90f - t * 80f)) * Mathf.Exp(-t * 7f)) * 0.8f; } Shell = Clip("shell", d); }
        if (Missile == null) { d = Buf(0.8f); lp = 0f; for (int i = 0; i < d.Length; i++) { float t = i / (float)Rate; float k = 0.05f + t * 0.5f; lp += (N() - lp) * k; d[i] = lp * Env(t, 0.05f, 0.8f) * 0.8f; } Missile = Clip("missile", d); }
        if (Boom == null) { d = Buf(1.5f); lp = 0f; float lp2 = 0f; for (int i = 0; i < d.Length; i++) { float t = i / (float)Rate; lp += (N() - lp) * 0.08f; lp2 += (lp - lp2) * 0.2f; d[i] = Mathf.Clamp(lp2 * 3.2f * Mathf.Exp(-t * 2.2f) + Mathf.Sin(t * 6.2832f * 48f) * Mathf.Exp(-t * 5f) * 0.6f, -1f, 1f); } Boom = Clip("boom", d); }
        if (Pickup == null) { d = Buf(0.3f); float[] pn = { 784f, 988f, 1319f }; for (int i = 0; i < d.Length; i++) { float t = i / (float)Rate; int k = Mathf.Min(2, (int)(t / 0.07f)); float tt = t - k * 0.07f; d[i] = Sq(t * pn[k]) * Env(tt, 0.004f, k == 2 ? 0.16f : 0.07f) * 0.3f; } Pickup = Clip("pickup", d); }
        if (Win == null) Win = Pickup;
        if (Step == null) { d = Buf(0.5f); lp = 0f; for (int i = 0; i < d.Length; i++) { float t = i / (float)Rate; lp += (N() - lp) * 0.06f; d[i] = (Mathf.Sin(t * 6.2832f * (55f - t * 40f)) * 0.9f + lp * 2f) * Mathf.Exp(-t * 8f) * 0.9f; } Step = Clip("step", d); Thud = Step; }
        if (StepMetal == null) StepMetal = Step;
        if (Toggle == null) Toggle = Click;
        if (Select == null) Select = Click;
        if (Confirm == null) Confirm = Pickup;
        if (EngineCar == null) EngineCar = LoopClip("car", (t) => Saw(t * 55f) * 0.5f + Saw(t * 110f) * 0.25f + Mathf.Sin(t * 6.2832f * 27f) * 0.25f, 0.45f);
        if (EngineTank == null) { EngineTank = LoopClip("tank", (t) => Saw(t * 38f) * 0.5f + Sq(t * 19f) * 0.3f + N() * 0.15f, 0.5f); }
        if (EngDiesel == null) EngDiesel = EngineTank;
        if (Rotor == null) Rotor = LoopClip("rotor", (t) => { float chop = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * 6.2832f * 14f), 4f); return N() * 0.6f * chop + Mathf.Sin(t * 6.2832f * 70f) * 0.2f; }, 0.5f, true);
        if (DroneWhine == null) DroneWhine = LoopClip("drone", (t) => Saw(t * 210f) * 0.25f + Saw(t * 213f) * 0.25f + Mathf.Sin(t * 6.2832f * 420f) * 0.15f, 0.35f);
        if (BoatMotor == null) BoatMotor = LoopClip("boat", (t) => Sq(t * 72f) * 0.35f + Saw(t * 36f) * 0.3f + N() * 0.12f, 0.45f, true);
        if (Bubbles == null) Bubbles = LoopClip("bubbles", (t) => { float b = Mathf.Sin(t * 6.2832f * (400f + 300f * Mathf.Sin(t * 6.2832f * 3f))) * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 6.2832f * 6f)), 6f); return b * 0.5f + N() * 0.04f; }, 0.4f);
        if (EngServo == null) EngServo = DroneWhine;
    }

    static AudioClip LoopClip(string name, System.Func<float, float> f, float vol, bool smooth = false)
    {
        var d = Buf(1f);
        float lp = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float v = f(i / (float)Rate);
            if (smooth) { lp += (v - lp) * 0.3f; v = lp; }
            d[i] = Mathf.Clamp(v * vol, -1f, 1f);
        }
        return Clip(name, d);
    }

    // ---------------- little helpers used around the game ----------------
    public static void Ribbiting(Vector3 p, float vol = 0.7f) { PlayAt(Pick(Ribbit), p, vol, 40f, Random.Range(0.9f, 1.15f)); }

    public static AudioClip AnimalVoice(string kind)
    {
        switch (kind)
        {
            case "Cow": return Moo;
            case "Horse": return Neigh;
            case "Chicken": return Random.value < 0.15f && Rooster != null ? Rooster : Cluck;
            case "Goat": return Bleat;
            case "sheep": return Baa;
            case "pig": return Oink;
            case "Dog": return Bark;
            case "Cat": return Meow;
            default: return null;
        }
    }
}

public class SfxDriver : MonoBehaviour
{
    void Update()
    {
        if (Kb.MDown() && !Kb.typing) Sfx.CycleVolume();   // ffu13: not while typing a name / room code
    }
}
