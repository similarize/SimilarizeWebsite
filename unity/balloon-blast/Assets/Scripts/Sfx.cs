using UnityEngine;
using UnityEngine.UI;

// All sound is synthesised at start-up (no audio assets): airsoft shots, balloon pops, the
// "your balloon popped" sting, reload, jump, countdown beeps + GO, win / lose jingles and an
// upbeat party music loop. Browsers only allow WebGL audio after the first tap / click / key /
// pad press, so the music starts on that first gesture. Volume ON / LOW / OFF: M key or the
// SOUND button (top-right), saved in PlayerPrefs bb.sound.
public static class Sfx
{
    public const int Rate = 22050;
    public static AudioClip Shot, Pop, MyPop, Reload, ReloadDone, Jump, Beep, Go, RoundWin, MatchWin, Lose, Click, Song;

    static GameObject host;
    static AudioSource[] pool;
    static int next;
    static AudioSource music;
    static float duck = 1f, duckT;
    public static bool Unlocked { get; private set; }
    public static int Level { get; private set; }     // 0 = on, 1 = low, 2 = off
    static readonly float[] Levels = { 1f, 0.35f, 0f };
    public static string LevelName { get { return Level == 0 ? "ON" : Level == 1 ? "LOW" : "OFF"; } }

    static Canvas canvas;
    static Image button;
    static Text buttonText;

    static System.Random rnd = new System.Random(7);
    static float N() { return (float)(rnd.NextDouble() * 2.0 - 1.0); }

    public static void Init()
    {
        if (host != null) return;
        host = new GameObject("Audio");
        Object.DontDestroyOnLoad(host);
        if (Object.FindObjectOfType<AudioListener>() == null) host.AddComponent<AudioListener>();
        pool = new AudioSource[16];
        for (int i = 0; i < pool.Length; i++) { pool[i] = host.AddComponent<AudioSource>(); pool[i].playOnAwake = false; pool[i].spatialBlend = 0f; }
        music = host.AddComponent<AudioSource>(); music.loop = true; music.playOnAwake = false; music.spatialBlend = 0f; music.volume = 0f;
        Level = Mathf.Clamp(PlayerPrefs.GetInt("bb.sound", 0), 0, 2);
        AudioListener.volume = Levels[Level];
        Build();
        BuildButton();
        host.AddComponent<SfxDriver>();
    }

    // ---------------- volume + button ----------------
    static void BuildButton()
    {
        canvas = UIK.MakeCanvas("SoundButton", null, 200, true);
        Object.DontDestroyOnLoad(canvas.gameObject);
        button = UIK.Img(canvas.transform, null, new Color(0f, 0f, 0f, 0.5f), new Vector2(1, 1), new Vector2(-78, -28), new Vector2(140, 42));
        buttonText = UIK.Label(button.transform, "", 20, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140, 40), Color.white);
        RefreshButton();
    }

    static void RefreshButton()
    {
        if (buttonText == null) return;
        buttonText.text = "SOUND: " + LevelName + "  <size=14>(M)</size>";
        buttonText.color = Level == 2 ? new Color(1f, 0.55f, 0.5f) : Level == 1 ? new Color(1f, 0.9f, 0.5f) : Color.white;
    }

    // true when a screen position (touch / mouse) lands on the SOUND button
    public static bool ButtonHit(Vector2 screenPos)
    {
        return button != null && canvas != null && canvas.enabled &&
               RectTransformUtility.RectangleContainsScreenPoint(button.rectTransform, screenPos, null);
    }

    public static void CycleVolume()
    {
        Level = (Level + 1) % 3;
        AudioListener.volume = Levels[Level];
        PlayerPrefs.SetInt("bb.sound", Level);
        PlayerPrefs.Save();
        RefreshButton();
        Play(Click, 0.8f);
    }

    public static void Unlock()
    {
        if (Unlocked) return;
        Unlocked = true;
        if (music != null && Song != null) { music.clip = Song; music.volume = 0f; music.Play(); }
    }

    public static void Tick(float dt)
    {
        if (music == null) return;
        duckT -= dt;
        duck = Mathf.MoveTowards(duck, duckT > 0f ? 0.3f : 1f, dt * (duckT > 0f ? 4f : 0.7f));
        float target = Unlocked ? 0.3f * duck : 0f;
        music.volume = Mathf.MoveTowards(music.volume, target, dt * 0.5f);
    }

    static void Duck(float secs) { duckT = Mathf.Max(duckT, secs); }

    // ---------------- playback ----------------
    public static void Play(AudioClip c, float vol, float pitch = 1f)
    {
        if (c == null || pool == null || !Unlocked) return;
        AudioSource s = pool[next];
        next = (next + 1) % pool.Length;
        s.pitch = pitch;
        s.PlayOneShot(c, vol);
    }

    // distance to the nearest human player's eye (2D sound, distance-scaled)
    static float Nearest(Vector3 p)
    {
        float d = 1e9f;
        if (Game.I != null)
            foreach (var sl in Game.I.slots)
                if (sl.soldier != null) d = Mathf.Min(d, (sl.soldier.eye.position - p).magnitude);
        return d > 1e8f ? 25f : d;
    }

    public static void PlayAt(AudioClip c, Vector3 p, float vol, float range, float pitch = 1f)
    {
        float k = Mathf.Clamp01(1f - Nearest(p) / range);
        if (k > 0.03f) Play(c, vol * k * k, pitch);
    }

    static float Vary(float a) { return 1f + Random.Range(-a, a); }

    // ---------------- game events ----------------
    public static void OnShot(Soldier s)
    {
        if (s.human) Play(Shot, 0.32f, Vary(0.05f));
        else PlayAt(Shot, s.eye.position, 0.3f, 55f, Vary(0.07f));
    }

    public static void OnPop(Vector3 p, Soldier victim, Soldier by)
    {
        if ((by != null && by.human) || victim.human) Play(Pop, 0.8f, Vary(0.08f));
        else PlayAt(Pop, p, 0.8f, 90f, Vary(0.08f));
        if (victim.human) Play(MyPop, 0.75f);
    }

    public static void OnReload(Soldier s, bool done)
    {
        AudioClip c = done ? ReloadDone : Reload;
        if (s.human) Play(c, 0.6f);
        else PlayAt(c, s.eye.position, 0.5f, 25f);
    }

    public static void OnJump(Soldier s)
    {
        if (s.human) Play(Jump, 0.45f, Vary(0.04f));
        else PlayAt(Jump, s.eye.position, 0.35f, 25f, Vary(0.06f));
    }

    public static void CountBeep() { Play(Beep, 0.55f); }
    public static void RoundGo() { Play(Go, 0.65f); }
    public static void RoundEnd(bool humanWon, bool match)
    {
        Duck(match ? 3.2f : 1.8f);
        if (humanWon) Play(match ? MatchWin : RoundWin, 0.7f);
        else Play(Lose, 0.65f);
    }

    // ---------------- synthesis helpers ----------------
    static AudioClip Clip(string name, float[] d)
    {
        var c = AudioClip.Create(name, d.Length, 1, Rate, false);
        c.SetData(d, 0);
        return c;
    }

    static float[] Buf(float secs) { return new float[Mathf.Max(1, Mathf.RoundToInt(secs * Rate))]; }
    static float Env(float t, float a, float dur) { return t < 0f ? 0f : t < a ? t / a : Mathf.Clamp01(1f - (t - a) / Mathf.Max(0.0001f, dur - a)); }
    static float Saw(float x) { return 2f * (x - Mathf.Floor(x + 0.5f)); }
    static float Sq(float x) { return (x - Mathf.Floor(x)) < 0.5f ? 1f : -1f; }
    static float Tri(float x) { return 1f - 4f * Mathf.Abs(x - Mathf.Floor(x + 0.5f)); }
    static float Sin(float t, float f) { return Mathf.Sin(t * 6.2832f * f); }

    // a sequence of notes (Hz, start s, length s) with a square + sine voice
    static float[] Jingle(float[] f, float[] at, float[] len, float total, float sqMix, float vib)
    {
        var d = Buf(total);
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate, v = 0f;
            for (int k = 0; k < f.Length; k++)
            {
                float tt = t - at[k];
                if (tt < 0f || tt > len[k]) continue;
                float fr = f[k] * (1f + vib * Mathf.Sin(tt * 6.2832f * 6f) * Mathf.Clamp01(tt * 4f));
                float e = Env(tt, 0.006f, len[k]);
                e = Mathf.Sqrt(e);
                v += (Sq(t * fr) * sqMix + Sin(t, fr) * (1f - sqMix) + Sin(t, fr * 2f) * 0.15f) * e;
            }
            d[i] = Mathf.Clamp(v * 0.28f, -1f, 1f);
        }
        return d;
    }

    static void Build()
    {
        // airsoft shot: soft "pff" of air + a tiny plastic tick
        var d = Buf(0.1f); float lp = 0f, hp = 0f, prev = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate; float n = N();
            hp = 0.85f * (hp + n - prev); prev = n;                 // high-passed noise
            lp += (hp - lp) * 0.55f;
            float tick = Sin(t, 2400f) * Mathf.Exp(-t * 400f);
            d[i] = (lp * Mathf.Exp(-t * 45f) * 0.8f + tick * 0.5f);
        }
        Shot = Clip("shot", d);

        // balloon pop: sharp crack + rubbery low thump
        d = Buf(0.22f); lp = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            lp += (N() - lp) * 0.7f;
            float crack = lp * Mathf.Exp(-t * 60f);
            float thump = Sin(t, 180f - t * 500f) * Mathf.Exp(-t * 28f) * 0.6f;
            d[i] = Mathf.Clamp((crack * 1.3f + thump) * Mathf.Clamp01(t * 2000f), -1f, 1f);
        }
        Pop = Clip("pop", d);

        // "your balloon popped": pop + sad descending "wah-wah"
        d = Buf(0.85f); lp = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate; float v = 0f;
            if (t < 0.15f) { lp += (N() - lp) * 0.6f; v += lp * Mathf.Exp(-t * 50f) * 1.1f; }
            float w1 = t - 0.12f, w2 = t - 0.42f;
            if (w1 >= 0f && w1 < 0.3f) { float f = 392f - w1 * 60f; v += (Saw(t * f) * 0.5f + Sin(t, f) * 0.4f) * Env(w1, 0.02f, 0.3f) * (0.7f + 0.3f * Sin(w1, 7f)); }
            if (w2 >= 0f && w2 < 0.42f) { float f = 330f - w2 * 120f; v += (Saw(t * f) * 0.5f + Sin(t, f) * 0.4f) * Env(w2, 0.02f, 0.42f) * (0.7f + 0.3f * Sin(w2, 6f)); }
            d[i] = Mathf.Clamp(v * 0.55f, -1f, 1f);
        }
        MyPop = Clip("mypop", d);

        // reload: mag out click, slide rattle, mag in clack
        d = Buf(0.5f); lp = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate; float v = 0f;
            v += Sin(t, 1500f) * Mathf.Exp(-t * 160f) * 0.6f;                                     // click
            float s = t - 0.12f; if (s > 0f && s < 0.16f) { lp += (N() - lp) * 0.3f; v += lp * 0.35f * Env(s, 0.02f, 0.16f); }
            float c = t - 0.34f; if (c > 0f) v += (Sin(t, 900f) * 0.5f + N() * 0.4f) * Mathf.Exp(-c * 90f);   // clack
            d[i] = Mathf.Clamp(v * 0.8f, -1f, 1f);
        }
        Reload = Clip("reload", d);
        d = Buf(0.12f);
        for (int i = 0; i < d.Length; i++) { float t = i / (float)Rate; d[i] = (Sin(t, 1100f) * 0.5f + N() * 0.35f) * Mathf.Exp(-t * 70f) * 0.8f; }
        ReloadDone = Clip("reloaddone", d);

        // jump: springy boing
        d = Buf(0.18f); float ph = 0f;
        for (int i = 0; i < d.Length; i++) { float t = i / (float)Rate; float f = 260f + 600f * Mathf.Sqrt(t / 0.18f); ph += f / Rate; d[i] = (Mathf.Sin(ph * 6.2832f) * 0.7f + Tri(ph * 2f) * 0.2f) * Env(t, 0.005f, 0.18f) * 0.6f; }
        Jump = Clip("jump", d);

        // countdown beep + GO
        d = Buf(0.16f);
        for (int i = 0; i < d.Length; i++) { float t = i / (float)Rate; d[i] = (Sq(t * 660f) * 0.35f + Sin(t, 660f) * 0.4f) * Env(t, 0.004f, 0.16f) * 0.6f; }
        Beep = Clip("beep", d);
        d = Buf(0.6f);
        for (int i = 0; i < d.Length; i++) { float t = i / (float)Rate; float e = Env(t, 0.005f, 0.6f); d[i] = (Sq(t * 1046.5f) * 0.25f + Sin(t, 1318.5f) * 0.3f + Sin(t, 784f) * 0.3f) * Mathf.Sqrt(e) * 0.55f; }
        Go = Clip("go", d);

        // jingles
        RoundWin = Clip("roundwin", Jingle(
            new[] { 523.3f, 659.3f, 784f, 1046.5f, 784f, 1046.5f },
            new[] { 0f, 0.1f, 0.2f, 0.3f, 0.45f, 0.55f },
            new[] { 0.1f, 0.1f, 0.1f, 0.15f, 0.1f, 0.5f }, 1.1f, 0.5f, 0f));
        MatchWin = Clip("matchwin", Jingle(
            new[] { 392f, 523.3f, 659.3f, 784f, 659.3f, 784f, 1046.5f, 1318.5f, 1046.5f, 1318.5f, 1568f },
            new[] { 0f, 0.12f, 0.24f, 0.36f, 0.6f, 0.72f, 0.84f, 1.08f, 1.2f, 1.32f, 1.5f },
            new[] { 0.12f, 0.12f, 0.12f, 0.22f, 0.12f, 0.12f, 0.22f, 0.12f, 0.12f, 0.16f, 0.9f }, 2.6f, 0.45f, 0.004f));
        Lose = Clip("lose", Jingle(
            new[] { 392f, 370f, 349.2f, 329.6f },
            new[] { 0f, 0.3f, 0.6f, 0.9f },
            new[] { 0.28f, 0.28f, 0.28f, 0.8f }, 1.8f, 0.3f, 0.012f));

        // UI click
        d = Buf(0.05f);
        for (int i = 0; i < d.Length; i++) { float t = i / (float)Rate; d[i] = Sin(t, 1800f) * Env(t, 0.001f, 0.05f) * 0.5f; }
        Click = Clip("click", d);

        Song = PartySong();
    }

    // ---------------- music ----------------
    // Upbeat party loop: 128 bpm, I-V-vi-IV in C, four-on-the-floor kick, claps on 2 + 4, off-beat hats,
    // bouncy octave bass, off-beat chord stabs and a chirpy pentatonic hook (second half varies it).
    static AudioClip PartySong()
    {
        float bpm = 128f, beat = 60f / bpm;
        int bars = 16;
        float len = bars * 4 * beat;
        var d = new float[Mathf.RoundToInt(len * Rate)];
        float root = 261.63f;   // C4
        int[] prog = { 0, 7, 9, 5 };                          // C, G, Am, F (semitones above C)
        bool[] minor = { false, false, true, false };
        // hook: 16 eighth-notes per 2 bars, semitones over C (-99 = rest)
        int[] hookA = { 12, -99, 16, 19, -99, 16, 12, 14, 16, -99, 14, 12, 9, -99, 12, -99 };
        int[] hookB = { 19, -99, 21, 19, 16, -99, 19, 21, 24, -99, 21, 19, 16, 14, 12, -99 };
        float hpPrev = 0f, hp = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float b = t / beat;
            int beatI = (int)b;
            int bar = beatI / 4;
            int ci = bar % 4;
            float inBeat = b - beatI;
            float bt = inBeat * beat;                         // seconds since this beat
            float v = 0f;
            float chordRoot = root * Mathf.Pow(2f, prog[ci] / 12f);

            // kick (four on the floor)
            v += Mathf.Sin(6.2832f * (55f * bt - 60f * bt * bt + 2.2f * (1f - Mathf.Exp(-bt * 40f)))) * Mathf.Exp(-bt * 14f) * 0.6f;
            // clap on 2 and 4 (bars 0-1 of each 8 lighter)
            if (beatI % 2 == 1)
            {
                float n = N();
                float clap = n * (Mathf.Exp(-bt * 30f) + 0.6f * Mathf.Exp(-Mathf.Abs(bt - 0.012f) * 300f));
                v += clap * 0.22f;
            }
            // off-beat open-ish hat
            float s8 = b * 2f; int e8 = (int)s8; float et = (s8 - e8) * beat * 0.5f;
            {
                float n = N(); hp = 0.9f * (hp + n - hpPrev); hpPrev = n;
                if (e8 % 2 == 1) v += hp * Mathf.Exp(-et * 35f) * 0.12f;
                else v += hp * Mathf.Exp(-et * 120f) * 0.04f;
            }
            // bass: root then octave on the off-beat eighth
            float bassF = chordRoot * 0.25f * (e8 % 2 == 1 ? 2f : 1f);
            v += (Tri(t * bassF) * 0.7f + Sq(t * bassF) * 0.12f) * Mathf.Exp(-et * 5f) * Mathf.Clamp01(et * 400f) * 0.42f;
            // chord stabs on the off-beats
            if (e8 % 2 == 1)
            {
                float st = et;
                float th = minor[ci] ? 3f : 4f;
                float stab = Saw(t * chordRoot) + Saw(t * chordRoot * Mathf.Pow(2f, th / 12f)) + Saw(t * chordRoot * 1.4983f);
                v += stab * Mathf.Exp(-st * 16f) * Mathf.Clamp01(st * 600f) * 0.07f;
            }
            // hook (lead), enters from bar 2; second half alternates A/B
            if (bar >= 2)
            {
                int[] hook = (bar >= 8 && (bar / 2) % 2 == 1) ? hookB : hookA;
                int idx = e8 % 16;
                int note = hook[idx];
                if (note > -50)
                {
                    float f = root * Mathf.Pow(2f, note / 12f);
                    float env = Mathf.Exp(-et * 9f) * Mathf.Clamp01(et * 500f);
                    v += (Sq(t * f) * 0.35f + Tri(t * f * 2f) * 0.2f + Sin(t, f) * 0.25f) * env * 0.3f;
                }
            }
            // little riser sparkle on the last beat of every 4th bar
            if (bar % 4 == 3 && beatI % 4 == 3)
            {
                float f = 1046.5f * Mathf.Pow(2f, (int)(inBeat * 4f) * 4f / 12f);
                v += Sin(t, f) * Mathf.Exp(-((inBeat * 4f) % 1f) * beat * 0.25f * 20f) * 0.08f;
            }
            d[i] = Mathf.Clamp(v * 0.8f, -1f, 1f);
        }
        int fadeN = 300;
        for (int i = 0; i < fadeN; i++) { float k = i / (float)fadeN; d[i] *= k; d[d.Length - 1 - i] *= k; }
        return Clip("party", d);
    }
}

public class SfxDriver : MonoBehaviour
{
    void Update()
    {
        if (!Sfx.Unlocked && Kb.AnyGesture()) Sfx.Unlock();
        Sfx.Tick(Time.unscaledDeltaTime);
        if (Kb.MDown()) Sfx.CycleVolume();
        foreach (Vector2 p in Kb.TouchesBegan()) if (Sfx.ButtonHit(p)) Sfx.CycleVolume();
        if (Kb.MouseLeftDown() && Cursor.lockState != CursorLockMode.Locked && Sfx.ButtonHit(Kb.MousePos())) Sfx.CycleVolume();
    }
}
