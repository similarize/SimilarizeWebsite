using System.Collections.Generic;
using UnityEngine;

// All sound is synthesised at start-up (no audio assets): one-shot effects, engine loops and a
// bouncy music loop per world mood. Browsers only start WebGL audio after the first tap / click / key,
// which Unity handles; until then everything is silent.
public static class Sfx
{
    public const int Rate = 22050;
    public static AudioClip Hop, Splash, Door, Shell, Missile, Boom, Pickup, Win, Click, Step, Thud;
    public static AudioClip EngineCar, EngineTank, Rotor, DroneWhine, BoatMotor, Bubbles;

    static GameObject host;
    static AudioSource[] pool;
    static int next;
    static AudioSource musicA, musicB;
    static string mood = "";
    static readonly Dictionary<string, AudioClip> songs = new Dictionary<string, AudioClip>();
    static float fade = 1f;
    public static int Level { get; private set; }     // 0 = full, 1 = low, 2 = off
    static readonly float[] Levels = { 1f, 0.35f, 0f };
    public static string LevelName { get { return Level == 0 ? "ON" : Level == 1 ? "LOW" : "OFF"; } }

    static System.Random rnd = new System.Random(3);
    static float N() { return (float)(rnd.NextDouble() * 2.0 - 1.0); }

    public static void Init()
    {
        if (host != null) return;
        host = new GameObject("Audio");
        Object.DontDestroyOnLoad(host);
        host.AddComponent<AudioListener>();
        pool = new AudioSource[10];
        for (int i = 0; i < pool.Length; i++) { pool[i] = host.AddComponent<AudioSource>(); pool[i].playOnAwake = false; pool[i].spatialBlend = 0f; }
        musicA = host.AddComponent<AudioSource>(); musicA.loop = true; musicA.playOnAwake = false;
        musicB = host.AddComponent<AudioSource>(); musicB.loop = true; musicB.playOnAwake = false;
        host.AddComponent<SfxDriver>();
        Level = Mathf.Clamp(PlayerPrefs.GetInt("ff.sound", 0), 0, 2);
        AudioListener.volume = Levels[Level];
        Build();
    }

    public static void CycleVolume()
    {
        Level = (Level + 1) % 3;
        AudioListener.volume = Levels[Level];
        PlayerPrefs.SetInt("ff.sound", Level);
        Play(Click, 1f);
    }

    public static void Play(AudioClip c, float vol, float pitch = 1f)
    {
        if (c == null || pool == null) return;
        AudioSource s = pool[next];
        next = (next + 1) % pool.Length;
        s.pitch = pitch;
        s.PlayOneShot(c, vol);
    }

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

    public static AudioSource Loop(GameObject owner, AudioClip c)
    {
        var s = owner.AddComponent<AudioSource>();
        s.clip = c; s.loop = true; s.playOnAwake = false; s.volume = 0f; s.spatialBlend = 0f;
        return s;
    }

    // ---------------- music ----------------
    public static void Music(string m)
    {
        if (pool == null || m == mood) return;
        mood = m;
        AudioClip c;
        if (!songs.TryGetValue(m, out c)) { c = Song(m); songs[m] = c; }
        // swap sources, crossfade in SfxDriver
        var t = musicA; musicA = musicB; musicB = t;
        musicA.clip = c; musicA.volume = 0f; musicA.Play();
        fade = 0f;
    }

    public static void TickMusic(float dt)
    {
        if (musicA == null) return;
        fade = Mathf.MoveTowards(fade, 1f, dt * 0.6f);
        musicA.volume = fade * 0.34f;
        if (musicB.isPlaying) { musicB.volume = (1f - fade) * 0.34f; if (fade >= 1f) musicB.Stop(); }
    }

    // ---------------- synthesis helpers ----------------
    static AudioClip Clip(string name, float[] d, bool loop = false)
    {
        var c = AudioClip.Create(name, d.Length, 1, Rate, false);
        c.SetData(d, 0);
        return c;
    }

    static float[] Buf(float secs) { return new float[Mathf.Max(1, Mathf.RoundToInt(secs * Rate))]; }
    static float Env(float t, float a, float dur) { return t < a ? t / a : Mathf.Clamp01(1f - (t - a) / Mathf.Max(0.0001f, dur - a)); }

    static void Build()
    {
        // hop: springy "boing" sweep
        var d = Buf(0.16f); float ph = 0f;
        for (int i = 0; i < d.Length; i++) { float t = i / (float)Rate; float f = 280f + 520f * Mathf.Sqrt(t / 0.16f); ph += f / Rate; d[i] = Mathf.Sin(ph * 6.2832f) * Env(t, 0.005f, 0.16f) * 0.6f; }
        Hop = Clip("hop", d);
        // splash: filtered noise
        d = Buf(0.45f); float lp = 0f;
        for (int i = 0; i < d.Length; i++) { float t = i / (float)Rate; lp += (N() - lp) * 0.35f; d[i] = lp * Mathf.Pow(Env(t, 0.01f, 0.45f), 2f) * 0.9f; }
        Splash = Clip("splash", d);
        // door: two thunks
        d = Buf(0.22f);
        for (int i = 0; i < d.Length; i++) { float t = i / (float)Rate; float a = t < 0.09f ? Env(t, 0.003f, 0.09f) : Env(t - 0.11f, 0.003f, 0.11f); d[i] = (Mathf.Sin(t * 6.2832f * 140f) * 0.6f + N() * 0.25f) * a * 0.7f; }
        Door = Clip("door", d);
        // click
        d = Buf(0.04f);
        for (int i = 0; i < d.Length; i++) { float t = i / (float)Rate; d[i] = Mathf.Sin(t * 6.2832f * 1800f) * Env(t, 0.001f, 0.04f) * 0.5f; }
        Click = Clip("click", d);
        // tank shell: crack + low thump
        d = Buf(0.5f); lp = 0f;
        for (int i = 0; i < d.Length; i++) { float t = i / (float)Rate; lp += (N() - lp) * 0.5f; d[i] = (lp * Mathf.Exp(-t * 18f) + Mathf.Sin(t * 6.2832f * (90f - t * 80f)) * Mathf.Exp(-t * 7f)) * 0.8f; }
        Shell = Clip("shell", d);
        // missile: rising whoosh
        d = Buf(0.8f); lp = 0f;
        for (int i = 0; i < d.Length; i++) { float t = i / (float)Rate; float k = 0.05f + t * 0.5f; lp += (N() - lp) * k; d[i] = lp * Env(t, 0.05f, 0.8f) * 0.8f; }
        Missile = Clip("missile", d);
        // explosion: long low rumble
        d = Buf(1.5f); lp = 0f; float lp2 = 0f;
        for (int i = 0; i < d.Length; i++) { float t = i / (float)Rate; lp += (N() - lp) * 0.08f; lp2 += (lp - lp2) * 0.2f; d[i] = Mathf.Clamp(lp2 * 3.2f * Mathf.Exp(-t * 2.2f) + Mathf.Sin(t * 6.2832f * 48f) * Mathf.Exp(-t * 5f) * 0.6f, -1f, 1f); }
        Boom = Clip("boom", d);
        // pickup: quick up-arpeggio
        d = Buf(0.3f);
        float[] pn = { 784f, 988f, 1319f };
        for (int i = 0; i < d.Length; i++) { float t = i / (float)Rate; int k = Mathf.Min(2, (int)(t / 0.07f)); float tt = t - k * 0.07f; d[i] = Sq(t * pn[k]) * Env(tt, 0.004f, k == 2 ? 0.16f : 0.07f) * 0.3f; }
        Pickup = Clip("pickup", d);
        // win fanfare
        d = Buf(1.1f);
        float[] wn = { 523f, 659f, 784f, 659f, 1047f };
        float[] wt = { 0f, 0.12f, 0.24f, 0.36f, 0.5f };
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate; float v = 0f;
            for (int k = 0; k < wn.Length; k++) { float tt = t - wt[k]; if (tt < 0f) continue; float dur = k == 4 ? 0.6f : 0.13f; if (tt > dur) continue; v += (Sq(t * wn[k]) * 0.5f + Mathf.Sin(t * 6.2832f * wn[k] * 0.5f) * 0.5f) * Env(tt, 0.005f, dur); }
            d[i] = v * 0.3f;
        }
        Win = Clip("win", d);
        // mech footstep: heavy thud
        d = Buf(0.5f); lp = 0f;
        for (int i = 0; i < d.Length; i++) { float t = i / (float)Rate; lp += (N() - lp) * 0.06f; d[i] = (Mathf.Sin(t * 6.2832f * (55f - t * 40f)) * 0.9f + lp * 2f) * Mathf.Exp(-t * 8f) * 0.9f; }
        Step = Clip("step", d);
        Thud = Step;
        // loops (integer cycles per second so they loop cleanly)
        EngineCar = LoopClip("car", (t) => Saw(t * 55f) * 0.5f + Saw(t * 110f) * 0.25f + Mathf.Sin(t * 6.2832f * 27f) * 0.25f, 0.45f);
        EngineTank = LoopClip("tank", (t) => Saw(t * 38f) * 0.5f + Sq(t * 19f) * 0.3f + N() * 0.15f, 0.5f);
        Rotor = LoopClip("rotor", (t) => { float chop = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * 6.2832f * 14f), 4f); return N() * 0.6f * chop + Mathf.Sin(t * 6.2832f * 70f) * 0.2f; }, 0.5f, true);
        DroneWhine = LoopClip("drone", (t) => Saw(t * 210f) * 0.25f + Saw(t * 213f) * 0.25f + Mathf.Sin(t * 6.2832f * 420f) * 0.15f, 0.35f);
        BoatMotor = LoopClip("boat", (t) => Sq(t * 72f) * 0.35f + Saw(t * 36f) * 0.3f + N() * 0.12f, 0.45f, true);
        Bubbles = LoopClip("bubbles", (t) => { float b = Mathf.Sin(t * 6.2832f * (400f + 300f * Mathf.Sin(t * 6.2832f * 3f))) * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 6.2832f * 6f)), 6f); return b * 0.5f + N() * 0.04f; }, 0.4f);
    }

    static float Saw(float x) { return 2f * (x - Mathf.Floor(x + 0.5f)); }
    static float Sq(float x) { return (x - Mathf.Floor(x)) < 0.5f ? 1f : -1f; }
    static float Tri(float x) { return 1f - 4f * Mathf.Abs(x - Mathf.Floor(x + 0.5f)); }

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
        return Clip(name, d, true);
    }

    // ---------------- songs ----------------
    // A short looping tune per mood: kick + hat, bass on the chord roots, a bouncy pentatonic melody.
    static AudioClip Song(string m)
    {
        float bpm; int[] prog; int[] scale; int seed; float leadType; bool drums; float root;
        switch (m)
        {
            case "house": bpm = 104f; prog = new[] { 0, 5, 3, 4 }; scale = new[] { 0, 2, 4, 7, 9, 11 }; seed = 21; leadType = 1f; drums = true; root = 196f; break;      // cosy music box
            case "underwater": bpm = 76f; prog = new[] { 0, 3, 5, 3 }; scale = new[] { 0, 2, 4, 7, 9 }; seed = 33; leadType = 2f; drums = false; root = 174.6f; break;  // floaty arps
            case "space": bpm = 68f; prog = new[] { 0, 6, 5, 4 }; scale = new[] { 0, 2, 4, 6, 7, 11 }; seed = 45; leadType = 3f; drums = false; root = 146.8f; break;   // lydian bleeps
            case "lobby": bpm = 116f; prog = new[] { 0, 4, 5, 3 }; scale = new[] { 0, 2, 4, 7, 9 }; seed = 9; leadType = 0f; drums = true; root = 220f; break;
            default: bpm = 126f; prog = new[] { 0, 4, 5, 3 }; scale = new[] { 0, 2, 4, 7, 9 }; seed = 5; leadType = 0f; drums = true; root = 261.6f; break;          // ranch: bouncy froggy hop
        }
        var r = new System.Random(seed);
        float beat = 60f / bpm;
        int bars = 8;
        float len = bars * 4 * beat;
        var d = new float[Mathf.RoundToInt(len * Rate)];
        int[] major = { 0, 2, 4, 5, 7, 9, 11 };
        // melody: 8th notes, rests sprinkled, two-bar motif repeated with variation
        int steps = bars * 8;
        var mel = new int[steps];
        for (int i = 0; i < 16; i++) mel[i] = r.NextDouble() < 0.22 ? -99 : scale[r.Next(scale.Length)] + (r.NextDouble() < 0.3 ? 12 : 0);
        for (int i = 16; i < steps; i++) mel[i] = (r.NextDouble() < 0.7) ? mel[i % 16] : (r.NextDouble() < 0.25 ? -99 : scale[r.Next(scale.Length)] + 12 * r.Next(2));
        float lp = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float b = t / beat;
            int bar = (int)(b / 4f);
            int chord = prog[(bar / 2) % prog.Length];
            float inBeat = b - Mathf.Floor(b);
            float v = 0f;
            // bass: root on each beat, octave bounce on the off-beat
            float bassF = root * 0.25f * Mathf.Pow(2f, major[chord % 7] / 12f) * (inBeat >= 0.5f ? 2f : 1f);
            float bt = (inBeat % 0.5f) * beat;
            v += Tri(t * bassF) * Mathf.Exp(-bt * (leadType >= 2f ? 2f : 6f)) * 0.45f;
            // pad (soft chord) for the slow moods
            if (leadType >= 2f)
            {
                for (int k = 0; k < 3; k++) v += Mathf.Sin(t * 6.2832f * root * 0.5f * Mathf.Pow(2f, (major[(chord + k * 2) % 7] + (chord + k * 2 >= 7 ? 12 : 0)) / 12f)) * 0.07f;
            }
            // melody
            float s8 = b * 2f;
            int si = (int)s8 % steps;
            float st = (s8 - Mathf.Floor(s8)) * beat * 0.5f;
            if (mel[si] > -50)
            {
                float f = root * Mathf.Pow(2f, (mel[si] + major[chord % 7] * 0) / 12f);
                float env = Mathf.Exp(-st * (leadType == 1f ? 7f : leadType >= 2f ? 3f : 9f)) * Mathf.Clamp01(st * 300f);
                float tone;
                if (leadType == 0f) tone = Sq(t * f) * 0.45f + Tri(t * f * 2f) * 0.2f;            // chirpy square
                else if (leadType == 1f) tone = Mathf.Sin(t * 6.2832f * f * 2f) * 0.7f + Mathf.Sin(t * 6.2832f * f * 4f) * 0.15f;   // music box
                else if (leadType == 2f) tone = Mathf.Sin(t * 6.2832f * f) * 0.6f + Mathf.Sin(t * 6.2832f * f * 1.5f) * 0.15f;     // bubbly
                else tone = Tri(t * f * 2f) * 0.5f;                                                   // space bleep
                v += tone * env * 0.32f;
            }
            if (drums)
            {
                float kt = inBeat * beat;
                v += Mathf.Sin(6.2832f * (60f * kt - 40f * kt * kt)) * Mathf.Exp(-kt * 22f) * 0.55f;   // kick
                float ht = ((b * 2f) - Mathf.Floor(b * 2f)) * beat * 0.5f;
                if (((int)(b * 2f)) % 2 == 1) v += N() * Mathf.Exp(-ht * 60f) * 0.12f;             // off-beat hat
                if (((int)b) % 2 == 1) v += N() * Mathf.Exp(-kt * 18f) * 0.16f;                       // snare-ish on 2 and 4
            }
            if (leadType == 2f) { lp += (v - lp) * 0.25f; v = lp; }   // underwater = muffled
            d[i] = Mathf.Clamp(v * 0.8f, -1f, 1f);
        }
        // short fade at the loop seam
        int fadeN = 400;
        for (int i = 0; i < fadeN; i++) { float k = i / (float)fadeN; d[i] *= k; d[d.Length - 1 - i] *= k; }
        return Clip("song_" + m, d, true);
    }
}

public class SfxDriver : MonoBehaviour
{
    void Update()
    {
        Sfx.TickMusic(Time.unscaledDeltaTime);
        if (Kb.MDown()) Sfx.CycleVolume();
    }
}
