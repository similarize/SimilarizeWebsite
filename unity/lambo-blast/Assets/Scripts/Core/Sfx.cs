using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// All sound is synthesised at start-up (no audio assets), same approach as Four Froggies Core/Sfx.cs:
// a steel-drum beach-party loop for the race, a chill lobby loop, engines tied to throttle (one loop
// per human car + one shared loop for nearby AI), drift skid, item / hit / pickup sounds, countdown
// beeps + GO, win / lose jingles. Nothing plays until the first key / click / tap / pad press.
// Volume ON / LOW / OFF: M key or the SOUND button (top centre), saved in PlayerPrefs lb.sound.
public static class Sfx
{
    public const int Rate = 22050;
    public static AudioClip Beep, Go, Roll, ItemGet, Rocket, Fireball, Oil, Zap, Bowl, Mine, ShieldUp, ShieldPop, Boost, Homing,
        Boom, Spin, Splash, Land, Bump, DriftPop, Win, Lose, Finish, Click, Pop;
    public static AudioClip Engine, Skid, EngineAI;

    static GameObject host;
    static AudioSource[] pool;
    static int next;
    static AudioSource music, aiEngine;
    static string mood = "", playing = "";
    static readonly Dictionary<string, AudioClip> songs = new Dictionary<string, AudioClip>();
    static float duck = 1f, duckT;
    public static bool Unlocked { get; private set; }
    public static int Level { get; private set; }     // 0 = on, 1 = low, 2 = off
    static readonly float[] Levels = { 1f, 0.35f, 0f };
    public static string LevelName { get { return Level == 0 ? "ON" : Level == 1 ? "LOW" : "OFF"; } }

    static Canvas canvas;
    static Image button;
    static Text buttonText;

    static System.Random rnd = new System.Random(11);
    static float N() { return (float)(rnd.NextDouble() * 2.0 - 1.0); }

    public static void Init()
    {
        if (host != null) return;
        host = new GameObject("Audio");
        Object.DontDestroyOnLoad(host);
        host.AddComponent<AudioListener>();
        pool = new AudioSource[16];
        for (int i = 0; i < pool.Length; i++) { pool[i] = host.AddComponent<AudioSource>(); pool[i].playOnAwake = false; pool[i].spatialBlend = 0f; }
        music = host.AddComponent<AudioSource>(); music.loop = true; music.playOnAwake = false; music.volume = 0f;
        Level = Mathf.Clamp(PlayerPrefs.GetInt("lb.sound", 0), 0, 2);
        AudioListener.volume = Levels[Level];
        Build();
        aiEngine = host.AddComponent<AudioSource>(); aiEngine.clip = EngineAI; aiEngine.loop = true; aiEngine.playOnAwake = false; aiEngine.volume = 0f;
        BuildButton();
        host.AddComponent<SfxDriver>();
    }

    // ---------------- volume + button ----------------
    static void BuildButton()
    {
        canvas = UIK.MakeCanvas("SoundButton", null, 200, true);
        Object.DontDestroyOnLoad(canvas.gameObject);
        button = UIK.Img(canvas.transform, null, new Color(0f, 0f, 0f, 0.5f), new Vector2(0.5f, 1f), new Vector2(70, -26), new Vector2(136, 40));
        buttonText = UIK.Label(button.transform, "", 19, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(136, 38), Color.white);
        RefreshButton();
    }

    public static RectTransform ButtonRect { get { return button != null ? button.rectTransform : null; } }
    public static Canvas ButtonCanvas { get { return canvas; } }

    static void RefreshButton()
    {
        if (buttonText == null) return;
        buttonText.text = "SOUND: " + LevelName + " <size=13>(M)</size>";
        buttonText.color = Level == 2 ? new Color(1f, 0.55f, 0.5f) : Level == 1 ? new Color(1f, 0.9f, 0.5f) : Color.white;
    }

    public static bool ButtonHit(Vector2 screenPos)
    {
        return button != null && canvas != null && canvas.enabled &&
               RectTransformUtility.RectangleContainsScreenPoint(button.rectTransform, screenPos, null);
    }

    public static void CycleVolume()
    {
        Level = (Level + 1) % 3;
        AudioListener.volume = Levels[Level];
        PlayerPrefs.SetInt("lb.sound", Level);
        PlayerPrefs.Save();
        RefreshButton();
        Play(Click, 0.8f);
    }

    // first user gesture: browsers only let WebGL audio start after one
    public static void Unlock()
    {
        if (Unlocked) return;
        Unlocked = true;
        if (mood.Length > 0) StartSong(mood);
    }

    // ---------------- playback ----------------
    public static void Play(AudioClip c, float vol, float pitch = 1f)
    {
        if (c == null || pool == null || !Unlocked) return;
        AudioSource s = pool[next];
        next = (next + 1) % pool.Length;
        s.pitch = pitch;
        s.PlayOneShot(c, vol);
    }

    // louder the closer it is to the nearest human car
    public static void PlayAt(AudioClip c, Vector3 p, float vol, float range = 70f, float pitch = 1f)
    {
        float d = Game.I != null ? Game.I.NearestHumanDistance(p) : 0f;
        float k = Mathf.Clamp01(1f - d / range);
        if (k > 0.03f) Play(c, vol * (0.25f + 0.75f * k * k), pitch);
    }

    public static AudioSource Loop(GameObject owner, AudioClip c)
    {
        var s = owner.AddComponent<AudioSource>();
        s.clip = c; s.loop = true; s.playOnAwake = false; s.volume = 0f; s.spatialBlend = 0f;
        return s;
    }

    // shared AI engine drone: volume from the closest AI car to any human
    public static void SetAIEngine(float vol, float pitch)
    {
        if (aiEngine == null) return;
        if (!Unlocked || vol < 0.005f) { if (aiEngine.isPlaying) aiEngine.Stop(); return; }
        if (!aiEngine.isPlaying) aiEngine.Play();
        aiEngine.volume = Mathf.MoveTowards(aiEngine.volume, vol, Time.deltaTime);
        aiEngine.pitch = pitch;
    }

    public static void Music(string m)
    {
        if (m == mood) return;
        mood = m;
        if (Unlocked) StartSong(m);
    }

    static void StartSong(string m)
    {
        if (music == null || playing == m) return;
        AudioClip c;
        if (!songs.TryGetValue(m, out c)) { c = Song(m); songs[m] = c; }
        music.clip = c;
        music.volume = 0f;
        music.Play();
        playing = m;
    }

    // duck the music under a jingle
    public static void Duck(float secs) { duckT = secs; }

    public static void Tick(float dt)
    {
        if (music == null) return;
        if (duckT > 0f) duckT -= dt;
        duck = Mathf.MoveTowards(duck, duckT > 0f ? 0.25f : 1f, dt * 1.5f);
        if (music.isPlaying) music.volume = Mathf.MoveTowards(music.volume, 0.3f * duck, dt * 0.5f);
    }

    // ---------------- synthesis ----------------
    static AudioClip Clip(string name, float[] d)
    {
        var c = AudioClip.Create(name, d.Length, 1, Rate, false);
        c.SetData(d, 0);
        return c;
    }

    static float[] Buf(float secs) { return new float[Mathf.Max(1, Mathf.RoundToInt(secs * Rate))]; }
    static float Env(float t, float a, float dur) { return t < a ? t / a : Mathf.Clamp01(1f - (t - a) / Mathf.Max(0.0001f, dur - a)); }
    static float Saw(float x) { return 2f * (x - Mathf.Floor(x + 0.5f)); }
    static float Sq(float x) { return (x - Mathf.Floor(x)) < 0.5f ? 1f : -1f; }
    static float Tri(float x) { return 1f - 4f * Mathf.Abs(x - Mathf.Floor(x + 0.5f)); }
    static float Sin(float x) { return Mathf.Sin(x * 6.2832f); }

    static AudioClip Tone(string name, float secs, System.Func<float, float> f)
    {
        var d = Buf(secs);
        for (int i = 0; i < d.Length; i++) d[i] = Mathf.Clamp(f(i / (float)Rate), -1f, 1f);
        return Clip(name, d);
    }

    // noise through a one-pole low-pass whose cutoff follows k(t)
    static AudioClip Noise(string name, float secs, System.Func<float, float> k, System.Func<float, float> env, float gain)
    {
        var d = Buf(secs); float lp = 0f;
        for (int i = 0; i < d.Length; i++) { float t = i / (float)Rate; lp += (N() - lp) * k(t); d[i] = Mathf.Clamp(lp * env(t) * gain, -1f, 1f); }
        return Clip(name, d);
    }

    static AudioClip LoopClip(string name, System.Func<float, float> f, float vol)
    {
        var d = Buf(1f);
        for (int i = 0; i < d.Length; i++) d[i] = Mathf.Clamp(f(i / (float)Rate) * vol, -1f, 1f);
        return Clip(name, d);
    }

    static float[] notesUp = { 523f, 659f, 784f, 1047f };

    static void Build()
    {
        Beep = Tone("beep", 0.22f, t => Sq(t * 660f) * 0.35f * Env(t, 0.005f, 0.22f));
        Go = Tone("go", 0.6f, t => (Sq(t * 990f) * 0.3f + Sin(t * 1980f) * 0.15f) * Env(t, 0.005f, 0.6f));
        Pop = Tone("pop", 0.2f, t => (Sin(t * (480f + t * 2600f)) * 0.45f + Sin(t * (960f + t * 5200f)) * 0.12f) * Env(t, 0.004f, 0.2f));
        Click = Tone("click", 0.04f, t => Sin(t * 1800f) * Env(t, 0.001f, 0.04f) * 0.5f);
        // item roulette tick + "got it" chime
        Roll = Tone("roll", 0.05f, t => Sq(t * 1400f) * 0.18f * Env(t, 0.002f, 0.05f));
        ItemGet = Tone("itemget", 0.35f, t => { int k = Mathf.Min(3, (int)(t / 0.07f)); return (Sin(t * notesUp[k]) * 0.4f + Sq(t * notesUp[k]) * 0.12f) * Env(t - k * 0.07f, 0.003f, k == 3 ? 0.14f : 0.07f); });
        Rocket = Noise("rocket", 0.9f, t => 0.08f + t * 0.4f, t => Env(t, 0.03f, 0.9f), 1.6f);
        Fireball = Noise("fireball", 0.6f, t => 0.25f - t * 0.2f, t => Env(t, 0.02f, 0.6f) * (0.7f + 0.3f * Sin(t * 30f)), 1.4f);
        Oil = Tone("oil", 0.4f, t => { float f = 180f - t * 260f; return (Sin(t * f) * 0.6f + N() * 0.15f) * Env(t, 0.01f, 0.4f); });
        Zap = Noise("zap", 0.7f, t => 0.9f, t => Env(t, 0.002f, 0.7f) * (Sin(t * 37f) > 0f ? 1f : 0.3f), 0.9f);
        Bowl = Tone("bowl", 0.9f, t => (Sin(t * (70f + 10f * Sin(t * 8f))) * 0.6f + N() * 0.1f) * Env(t, 0.05f, 0.9f));
        Mine = Tone("mine", 0.25f, t => (Sin(t * 220f) * 0.5f + N() * 0.3f) * Mathf.Exp(-t * 18f));
        ShieldUp = Tone("shield", 0.5f, t => { float f = 400f + t * 900f; return (Sin(t * f) * 0.3f + Sin(t * f * 1.5f) * 0.15f) * Env(t, 0.02f, 0.5f); });
        ShieldPop = Tone("shieldpop", 0.3f, t => (Sin(t * (1200f - t * 2500f)) * 0.35f + N() * 0.2f) * Mathf.Exp(-t * 10f));
        Boost = Noise("boost", 1.0f, t => 0.05f + Mathf.Min(t, 0.4f) * 0.9f, t => Env(t, 0.08f, 1.0f), 1.5f);
        Homing = Tone("homing", 0.7f, t => (Sq(t * 880f) * 0.18f * (((int)(t * 12f)) % 2 == 0 ? 1f : 0f)) + N() * 0.2f * Env(t, 0.05f, 0.7f));
        // explosion: long low rumble
        {
            var d = Buf(1.3f); float lp = 0f, lp2 = 0f;
            for (int i = 0; i < d.Length; i++) { float t = i / (float)Rate; lp += (N() - lp) * 0.09f; lp2 += (lp - lp2) * 0.2f; d[i] = Mathf.Clamp(lp2 * 3.2f * Mathf.Exp(-t * 2.6f) + Sin(t * 50f) * Mathf.Exp(-t * 5f) * 0.6f, -1f, 1f); }
            Boom = Clip("boom", d);
        }
        Spin = Tone("spin", 0.9f, t => { float f = 520f * (1f - t * 0.6f) * (1f + 0.15f * Sin(t * 9f)); return Sq(t * f) * 0.16f * Env(t, 0.01f, 0.9f); });
        Splash = Noise("splash", 0.5f, t => 0.35f, t => Mathf.Pow(Env(t, 0.01f, 0.5f), 2f), 0.9f);
        Land = Tone("land", 0.2f, t => (Sin(t * (90f - t * 120f)) * 0.7f + N() * 0.2f) * Mathf.Exp(-t * 16f));
        Bump = Tone("bump", 0.18f, t => (Sin(t * 140f) * 0.5f + N() * 0.35f) * Mathf.Exp(-t * 22f));
        DriftPop = Tone("driftpop", 0.3f, t => (Saw(t * (300f + t * 1500f)) * 0.25f + N() * 0.15f) * Env(t, 0.01f, 0.3f));
        // jingles
        {
            float[] wn = { 523f, 659f, 784f, 1047f, 784f, 1047f, 1319f };
            float[] wt = { 0f, 0.13f, 0.26f, 0.39f, 0.6f, 0.73f, 0.86f };
            var d = Buf(1.8f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate, v = 0f;
                for (int k = 0; k < wn.Length; k++) { float tt = t - wt[k]; float dur = k == wn.Length - 1 ? 0.9f : 0.15f; if (tt < 0f || tt > dur) continue; v += (Sq(t * wn[k]) * 0.4f + Sin(t * wn[k] * 0.5f) * 0.5f) * Env(tt, 0.005f, dur); }
                d[i] = Mathf.Clamp(v * 0.3f, -1f, 1f);
            }
            Win = Clip("win", d);
            float[] ln = { 392f, 370f, 349f, 330f };
            d = Buf(1.6f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate; int k = Mathf.Min(3, (int)(t / 0.3f)); float tt = t - k * 0.3f; float dur = k == 3 ? 0.7f : 0.3f;
                float f = ln[k] * (k == 3 ? 1f + 0.02f * Sin(t * 6f) : 1f);
                d[i] = (Saw(t * f) * 0.35f + Sin(t * f) * 0.3f) * Env(tt, 0.01f, dur) * 0.5f;
            }
            Lose = Clip("lose", d);
            Finish = Tone("finish", 0.7f, t => { int k = Mathf.Min(2, (int)(t / 0.12f)); float f = k == 0 ? 784f : k == 1 ? 988f : 1175f; return Sq(t * f) * 0.22f * Env(t - k * 0.12f, 0.004f, k == 2 ? 0.45f : 0.12f); });
        }
        // loops (integer cycles per second so they loop cleanly)
        Engine = LoopClip("engine", t => Saw(t * 60f) * 0.45f + Saw(t * 120f) * 0.25f + Sq(t * 30f) * 0.15f + Sin(t * 240f) * 0.1f, 0.5f);
        EngineAI = LoopClip("engineai", t => Saw(t * 70f) * 0.35f + Saw(t * 73f) * 0.35f + Saw(t * 140f) * 0.15f, 0.45f);
        {
            var d = Buf(1f); float lp = 0f;
            for (int i = 0; i < d.Length; i++) { lp += (N() - lp) * 0.6f; float t = i / (float)Rate; d[i] = (lp * 0.5f + Sin(t * 900f) * 0.06f) * 0.6f; }
            Skid = Clip("skid", d);
        }
    }

    // ---------------- music ----------------
    // Beach party: steel-drum lead, marimba bass, shaker + kick + clave, I-IV-V-IV in F (race) / chill (lobby).
    static AudioClip Song(string m)
    {
        bool lobby = m == "lobby";
        float bpm = lobby ? 104f : 124f;
        float root = 174.6f;   // F3
        int[] prog = lobby ? new[] { 0, 5, 3, 4 } : new[] { 0, 3, 4, 3 };
        int[] major = { 0, 2, 4, 5, 7, 9, 11 };
        int[] pent = { 0, 2, 4, 7, 9 };
        var r = new System.Random(lobby ? 77 : 31);
        float beat = 60f / bpm;
        int bars = 8;
        float len = bars * 4 * beat;
        var d = new float[Mathf.RoundToInt(len * Rate)];
        int steps = bars * 8;
        var mel = new int[steps];
        for (int i = 0; i < 16; i++) mel[i] = r.NextDouble() < (lobby ? 0.4 : 0.25) ? -99 : pent[r.Next(pent.Length)] + (r.NextDouble() < 0.35 ? 12 : 0);
        for (int i = 16; i < steps; i++) mel[i] = r.NextDouble() < 0.65 ? mel[i % 16] : (r.NextDouble() < 0.3 ? -99 : pent[r.Next(pent.Length)] + 12 * r.Next(2));
        // calypso bass pattern (in 8ths): root, -, 5th, root, -, root, 5th, -
        int[] bassPat = { 0, -1, 7, 0, -1, 0, 7, -1 };
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float b = t / beat;
            int bar = (int)(b / 4f);
            int chord = prog[(bar / 2) % prog.Length];
            float chordShift = major[chord % 7];
            float v = 0f;
            float s8 = b * 2f;
            int i8 = (int)s8;
            float st = (s8 - i8) * beat * 0.5f;
            // marimba bass
            int bp = bassPat[i8 % 8];
            if (bp >= 0)
            {
                float f = root * 0.5f * Mathf.Pow(2f, (chordShift + bp) / 12f);
                v += (Sin(t * f) * 0.8f + Sin(t * f * 4f) * 0.15f * Mathf.Exp(-st * 30f)) * Mathf.Exp(-st * 7f) * 0.42f;
            }
            // off-beat chord stabs (guitar-ish)
            if (i8 % 2 == 1 && !lobby)
            {
                for (int k = 0; k < 3; k++)
                {
                    float f = root * Mathf.Pow(2f, (major[(chord + k * 2) % 7] + ((chord + k * 2) >= 7 ? 12 : 0)) / 12f);
                    v += Tri(t * f) * Mathf.Exp(-st * 16f) * 0.07f;
                }
            }
            // pad for the lobby
            if (lobby)
                for (int k = 0; k < 3; k++)
                    v += Sin(t * root * Mathf.Pow(2f, (major[(chord + k * 2) % 7] + ((chord + k * 2) >= 7 ? 12 : 0)) / 12f)) * 0.05f;
            // steel drum lead: inharmonic partials, fast decay
            int mi = i8 % steps;
            if (mel[mi] > -50)
            {
                float f = root * 2f * Mathf.Pow(2f, (mel[mi] + chordShift * 0f) / 12f);
                float env = Mathf.Exp(-st * (lobby ? 5f : 7f)) * Mathf.Clamp01(st * 400f);
                float tone = Sin(t * f) * 0.6f + Sin(t * f * 2f) * 0.25f * Mathf.Exp(-st * 10f) + Sin(t * f * 2.76f) * 0.12f * Mathf.Exp(-st * 18f);
                v += tone * env * (lobby ? 0.24f : 0.3f);
            }
            float kt = (b - Mathf.Floor(b)) * beat;
            if (!lobby)
            {
                if (((int)b) % 2 == 0) v += Sin(55f * kt - 35f * kt * kt) * Mathf.Exp(-kt * 20f) * 0.55f;   // kick on 1 + 3
                else v += Sin(48f * kt) * Mathf.Exp(-kt * 22f) * 0.35f;                                             // softer on 2 + 4
            }
            // shaker on every 16th, accented off-beats
            float s16 = b * 4f; float t16 = (s16 - Mathf.Floor(s16)) * beat * 0.25f;
            v += N() * Mathf.Exp(-t16 * 70f) * (((int)s16) % 2 == 1 ? 0.07f : 0.035f) * (lobby ? 0.6f : 1f);
            // clave 3-2
            int c16 = ((int)s16) % 16;
            if (!lobby && (c16 == 0 || c16 == 3 || c16 == 6 || c16 == 10 || c16 == 12)) v += Sin(t * 2500f) * Mathf.Exp(-t16 * 90f) * 0.12f;
            d[i] = Mathf.Clamp(v * 0.85f, -1f, 1f);
        }
        int fadeN = 300;
        for (int i = 0; i < fadeN; i++) { float k = i / (float)fadeN; d[i] *= k; d[d.Length - 1 - i] *= k; }
        return Clip("song_" + m, d);
    }
}

public class SfxDriver : MonoBehaviour
{
    void Update()
    {
        Sfx.Tick(Time.unscaledDeltaTime);
        bool gesture = Kb.AnyKeyDown() || Kb.MouseLeftDown() || Kb.TouchesBegan().Count > 0;
        if (!gesture) foreach (Gamepad p in Gamepad.all) if (Pads.AnyButtonDown(p)) { gesture = true; break; }
        if (gesture) Sfx.Unlock();
        if (Kb.MDown()) Sfx.CycleVolume();
        if (Kb.MouseLeftDown() && Kb.TouchCount() == 0 && Sfx.ButtonHit(Kb.MousePos())) Sfx.CycleVolume();
        foreach (Vector2 p in Kb.TouchesBegan()) if (Sfx.ButtonHit(p)) Sfx.CycleVolume();
    }
}
