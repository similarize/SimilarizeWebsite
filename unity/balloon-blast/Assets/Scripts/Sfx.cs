using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Sound design v2 (sound pass, 2026-10-09). Two sources of sound:
//  1. Synthesised at start-up (no files): layered airsoft shots (spring thump + gas pop + piston clack + air tail, 4 near
//     variants + 2 distant ones), balloon pops (5 variants: latex crack + rubber slap + flutter), "your balloon popped"
//     wah-wah, hit-confirm ding, reload slide, jump boing, countdown beeps + GO, and a seamless wind loop.
//  2. Recorded CC0 sounds and music (Snd.cs, Resources/Audio/*.bytes from work/lb-gfx/bb/build_audio.py): footsteps on
//     grass / wood, BB impacts on wood / metal / hay / bodies, mag out / in + strap, UI clicks, Kenney jingles as round start
//     / win / lose stingers, kids' cheers + applause, birds ambience, and two kid-friendly music loops (lobby + match).
// Browsers only allow WebGL audio after the first tap / click / key / pad press, so music starts on that gesture.
// Volume ON / LOW / OFF: M key or the SOUND button (top-right, left of the page's own buttons), saved in PlayerPrefs bb.sound.
public static class Sfx
{
    public const int Rate = 22050;
    static AudioClip[] shots, shotsFar, pops, stepsGrass, stepsWood, hitWood, hitMetal, hitSoft;
    static AudioClip MyPop, Ding, Slide, Jump, Beep, Go, Click, Wind;
    static AudioClip hitBody, land, magOut, magIn, strap, uiClick, uiJoin, uiStart, uiBack, uiSwitch, uiSelect, uiOpen, uiClose;
    static AudioClip jGo, jWin, jMatch, jLose, cheer1, cheer2, cheerClap, cheerYay, amb;

    static GameObject host;
    static AudioSource[] pool;
    static int next;
    static AudioSource musA, musB, ambSrc, windSrc;
    static bool matchScene;
    static float duck = 1f, duckT;
    public static bool Unlocked { get; private set; }
    public static int Level { get; private set; }     // 0 = on, 1 = low, 2 = off
    static readonly float[] Levels = { 1f, 0.35f, 0f };
    public static string LevelName { get { return Level == 0 ? "ON" : Level == 1 ? "LOW" : "OFF"; } }

    static Canvas canvas;
    static Image button;
    static Text buttonText;

    struct Delayed { public float t; public AudioClip c; public float vol, pitch; }
    static readonly List<Delayed> delayed = new List<Delayed>();

    static System.Random rnd = new System.Random(7);
    static float N() { return (float)(rnd.NextDouble() * 2.0 - 1.0); }
    static float R01() { return (float)rnd.NextDouble(); }

    public static void Init()
    {
        if (host != null) return;
        host = new GameObject("Audio");
        Object.DontDestroyOnLoad(host);
        if (Object.FindObjectOfType<AudioListener>() == null) host.AddComponent<AudioListener>();
        pool = new AudioSource[20];
        for (int i = 0; i < pool.Length; i++) { pool[i] = host.AddComponent<AudioSource>(); pool[i].playOnAwake = false; pool[i].spatialBlend = 0f; }
        musA = Loop(); musB = Loop(); ambSrc = Loop(); windSrc = Loop();
        Level = Mathf.Clamp(PlayerPrefs.GetInt("bb.sound", 0), 0, 2);
        AudioListener.volume = Levels[Level];
        float t0 = Time.realtimeSinceStartup;
        Build();
        LoadRecorded();
        Debug.Log("SOUND: built in " + ((Time.realtimeSinceStartup - t0) * 1000f).ToString("0") + " ms, recorded clips " + Snd.Loaded + " loaded / " + Snd.Missing + " missing");
        BuildButton();
        host.AddComponent<SfxDriver>();
    }

    static AudioSource Loop()
    {
        AudioSource a = host.AddComponent<AudioSource>();
        a.loop = true; a.playOnAwake = false; a.spatialBlend = 0f; a.volume = 0f;
        return a;
    }

    static void LoadRecorded()
    {
        stepsGrass = Snd.LoadSet("step_grass", 5);
        stepsWood = Snd.LoadSet("step_wood", 5);
        hitWood = Snd.LoadSet("hit_wood", 3);
        hitMetal = Snd.LoadSet("hit_metal", 2);
        hitSoft = Snd.LoadSet("hit_soft", 2);
        hitBody = Snd.Load("hit_body"); land = Snd.Load("land");
        magOut = Snd.Load("mag_out"); magIn = Snd.Load("mag_in"); strap = Snd.Load("strap");
        uiClick = Snd.Load("ui_click"); uiJoin = Snd.Load("ui_join"); uiStart = Snd.Load("ui_start"); uiBack = Snd.Load("ui_back");
        uiSwitch = Snd.Load("ui_switch"); uiSelect = Snd.Load("ui_select"); uiOpen = Snd.Load("ui_open"); uiClose = Snd.Load("ui_close");
        jGo = Snd.Load("j_go"); jWin = Snd.Load("j_win"); jMatch = Snd.Load("j_match"); jLose = Snd.Load("j_lose");
        cheer1 = Snd.Load("cheer_teens1"); cheer2 = Snd.Load("cheer_teens2"); cheerClap = Snd.Load("cheer_clap"); cheerYay = Snd.Load("cheer_yay");
        amb = Snd.Load("amb_birds");
        // music is NOT decoded here: MusicClip() decodes only the track that is about to play (mono 24 kHz) and
        // Tick() destroys the faded-out one, so at most one decoded music track is resident (two during a crossfade)
    }

    // ---------------- volume + button ----------------
    static void BuildButton()
    {
        canvas = UIK.MakeCanvas("SoundButton", null, 200, false);
        Object.DontDestroyOnLoad(canvas.gameObject);
        button = UIK.Img(canvas.transform, null, new Color(0f, 0f, 0f, 0.5f), new Vector2(1, 1), new Vector2(-235, -28), new Vector2(150, 44));
        buttonText = UIK.Label(button.transform, "", 20, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 42), Color.white);
        RefreshButton();
        PlaceButton();
    }

    // pixel placement: left of the page's own toolbar (fullscreen + Arcade), inside the safe area, any orientation
    public static void PlaceButton()
    {
        if (canvas == null) return;
        Page.Refresh();
        float ui = Page.Ui;
        var sc = canvas.GetComponent<CanvasScaler>();
        if (sc != null) sc.scaleFactor = ui;
        float rightPx = Page.TbW > 0 ? Page.TbW + 8f * ui : Page.R + 10f * ui;
        float cyPx = Page.TbW > 0 && Page.TbH > 0 ? Mathf.Max(Page.TbH * 0.5f + 3f * ui, Page.T + 26f * ui) : Page.T + 32f * ui;
        button.rectTransform.anchoredPosition = new Vector2(-(rightPx / ui + 75f), -(cyPx / ui));
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
        Play(uiClick != null ? uiClick : Click, 0.7f);
    }

    public static void Unlock()
    {
        if (Unlocked) return;
        Unlocked = true;
        StartLoop(musA, MusicClip(matchScene));
        StartLoop(ambSrc, amb);
        StartLoop(windSrc, Wind);
    }

    static AudioClip MusicClip(bool match)
    {
        string want = match ? "mus_match" : "mus_lobby";
        if (musB != null && musB.clip != null && musB.clip.name == want) return musB.clip;   // fading back to it
        if (musA != null && musA.clip != null && musA.clip.name == want) return musA.clip;
        AudioClip c = Snd.Load(want);
        if (c == null) c = Snd.Load(match ? "mus_lobby" : "mus_match");
        return c;
    }

    static void StartLoop(AudioSource s, AudioClip c)
    {
        if (s == null || c == null) return;
        if (s.clip == c && s.isPlaying) return;
        s.clip = c; s.volume = 0f; s.Play();
    }

    // lobby <-> match: the music crossfades between the two loops, the field ambience comes up in a match
    public static void SetScene(bool match)
    {
        if (matchScene == match) return;
        matchScene = match;
        if (!Unlocked) return;
        AudioSource from = musA; musA = musB; musB = from;     // musA = the one fading in
        StartLoop(musA, MusicClip(match));
    }

    public static void Tick(float dt)
    {
        if (musA == null) return;
        duckT -= dt;
        duck = Mathf.MoveTowards(duck, duckT > 0f ? 0.28f : 1f, dt * (duckT > 0f ? 4f : 0.6f));
        float musVol = Unlocked ? (matchScene ? 0.2f : 0.3f) * duck : 0f;
        musA.volume = Mathf.MoveTowards(musA.volume, musVol, dt * 0.4f);
        musB.volume = Mathf.MoveTowards(musB.volume, 0f, dt * 0.4f);
        if (musB.isPlaying && musB.volume <= 0.001f) musB.Stop();
        if (!musB.isPlaying && musB.clip != null && musB.clip != musA.clip)
        {
            AudioClip old = musB.clip; musB.clip = null; Object.Destroy(old);   // keep one decoded music track
        }
        float ambVol = Unlocked ? (matchScene ? 0.32f : 0.12f) : 0f;
        ambSrc.volume = Mathf.MoveTowards(ambSrc.volume, ambVol, dt * 0.3f);
        windSrc.volume = Mathf.MoveTowards(windSrc.volume, Unlocked ? (matchScene ? 0.16f : 0.07f) : 0f, dt * 0.3f);
        float now = Time.unscaledTime;
        for (int i = delayed.Count - 1; i >= 0; i--)
            if (now >= delayed[i].t) { Delayed d = delayed[i]; delayed.RemoveAt(i); Play(d.c, d.vol, d.pitch); }
    }

    static void Duck(float secs) { duckT = Mathf.Max(duckT, secs); }

    // ---------------- playback ----------------
    public static void Play(AudioClip c, float vol, float pitch = 1f)
    {
        if (c == null || pool == null || !Unlocked || vol <= 0.005f) return;
        AudioSource s = pool[next];
        next = (next + 1) % pool.Length;
        s.pitch = pitch;
        s.PlayOneShot(c, vol);
    }

    static void Later(float secs, AudioClip c, float vol, float pitch = 1f)
    {
        if (c == null || !Unlocked) return;
        delayed.Add(new Delayed { t = Time.unscaledTime + secs, c = c, vol = vol, pitch = pitch });
    }

    static AudioClip Pick(AudioClip[] set) { return set == null || set.Length == 0 ? null : set[Random.Range(0, set.Length)]; }

    // distance to the nearest human player's eye (2D sound, distance-scaled)
    static float Nearest(Vector3 p)
    {
        float d = 1e9f;
        if (Game.I != null)
            foreach (var sl in Game.I.slots)
                if (sl.soldier != null) d = Mathf.Min(d, (sl.soldier.eye.position - p).magnitude);
        return d > 1e8f ? 25f : d;
    }

    static float Falloff(Vector3 p, float range) { float k = Mathf.Clamp01(1f - Nearest(p) / range); return k * k; }

    public static void PlayAt(AudioClip c, Vector3 p, float vol, float range, float pitch = 1f)
    {
        float k = Falloff(p, range);
        if (k > 0.002f) Play(c, vol * k, pitch);
    }

    static float Vary(float a) { return 1f + Random.Range(-a, a); }

    // ---------------- game events ----------------
    public static void OnShot(Soldier s)
    {
        if (s.human) { Play(Pick(shots), 0.42f, Vary(0.04f)); return; }
        float d = Nearest(s.eye.position);
        float k = Mathf.Clamp01(1f - d / 70f); k *= k;
        if (k < 0.003f) return;
        Play(d > 14f ? Pick(shotsFar) : Pick(shots), 0.38f * k, Vary(0.06f));
    }

    public static void OnPop(Vector3 p, Soldier victim, Soldier by)
    {
        AudioClip c = Pick(pops);
        if ((by != null && by.human) || victim.human) Play(c, 0.85f, Vary(0.07f));
        else PlayAt(c, p, 0.85f, 90f, Vary(0.08f));
        if (victim.human) Play(MyPop, 0.7f);
        else if (by != null && by.human) Later(0.06f, Ding, 0.35f, Vary(0.03f));   // hit confirm for the shooter
    }

    public static void OnOut(Soldier victim, Soldier by)
    {
        if (by != null && by.human && !victim.human) Later(0.25f, cheerYay, 0.42f);
    }

    // a BB hitting something that is not a balloon: wood / metal / hay / ground / a figure
    public static void OnImpact(Vector3 p, Collider col)
    {
        float k = Falloff(p, 22f);
        if (k < 0.01f) return;
        AudioClip c; float vol = 0.5f;
        switch (Surface(col))
        {
            case 1: c = Pick(hitMetal); vol = 0.32f; break;
            case 2: c = Pick(hitSoft); vol = 0.4f; break;
            case 3: c = hitBody; vol = 0.55f; break;
            case 4: c = Pick(hitSoft); vol = 0.18f; break;   // ground
            default: c = Pick(hitWood); vol = 0.42f; break;
        }
        Play(c, vol * k, Vary(0.12f));
    }

    // 0 wood, 1 metal, 2 hay / sandbags, 3 a figure, 4 ground
    static readonly Dictionary<int, int> surfaceCache = new Dictionary<int, int>();
    public static int Surface(Collider col)
    {
        if (col == null) return 4;
        int id = col.GetInstanceID(), v;
        if (surfaceCache.TryGetValue(id, out v)) return v;
        v = 0;
        if (col is TerrainCollider) v = 4;
        else if (col.GetComponentInParent<Soldier>() != null) v = 3;
        else
        {
            Transform t = col.transform;
            for (int i = 0; i < 3 && t != null; i++, t = t.parent)
            {
                string n = t.name;
                if (n.Contains("Bale") || n.Contains("Sandbag") || n.Contains("Hay") || n.Contains("Bush")) { v = 2; break; }
                if (n.Contains("Barrel") || n.Contains("Roof") || n.Contains("Tyre") || n.Contains("Metal") || n.Contains("Rock")) { v = 1; break; }
            }
        }
        surfaceCache[id] = v;
        return v;
    }

    public static void OnStep(Soldier s, int surface, bool landing)
    {
        if (!s.human && Nearest(s.transform.position) > 16f) return;
        AudioClip c = surface == 0 || surface == 1 ? Pick(stepsWood) : Pick(stepsGrass);
        float vol = s.human ? 0.26f : 0.3f;
        if (landing) { vol *= 1.5f; if (s.human) Play(land, 0.22f, Vary(0.05f)); }
        if (s.human) Play(c, vol, Vary(0.08f));
        else PlayAt(c, s.transform.position, vol, 16f, Vary(0.08f));
    }

    public static void OnReload(Soldier s, bool done)
    {
        if (s.human)
        {
            if (done) Play(Slide, 0.55f);
            else { Play(magOut, 0.5f, Vary(0.04f)); Play(strap, 0.25f); Later(0.72f, magIn, 0.55f, Vary(0.03f)); }
        }
        else if (!done) PlayAt(magOut, s.eye.position, 0.4f, 25f);
        else PlayAt(Slide, s.eye.position, 0.4f, 25f);
    }

    public static void OnJump(Soldier s)
    {
        if (s.human) Play(Jump, 0.38f, Vary(0.04f));
        else PlayAt(Jump, s.eye.position, 0.3f, 25f, Vary(0.06f));
    }

    public static void CountBeep() { Play(Beep, 0.5f); }
    public static void RoundGo() { Play(Go, 0.55f); Play(jGo, 0.5f); }
    public static void RoundEnd(bool humanWon, bool match)
    {
        Duck(match ? 4.5f : 3f);
        if (humanWon)
        {
            Play(match ? jMatch : jWin, 0.7f);
            Later(0.15f, match ? cheer2 : cheer1, 0.55f);
            if (match) Later(0.5f, cheerClap, 0.5f);
        }
        else
        {
            Play(jLose, 0.6f);
            if (match) Later(0.9f, cheerClap, 0.3f);
        }
    }

    // UI
    public static void UiTap() { Play(uiClick, 0.5f); }
    public static void UiJoin() { Play(uiJoin, 0.55f); }
    public static void UiBack() { Play(uiBack, 0.55f); }
    public static void UiSwitch() { Play(uiSwitch, 0.55f); }
    public static void UiSelect() { Play(uiSelect, 0.55f); }
    public static void UiPanel(bool open) { Play(open ? uiOpen : uiClose, 0.5f); }
    public static void UiStart() { Play(uiStart, 0.6f); }

    // ---------------- synthesis helpers ----------------
    static AudioClip Clip(string name, float[] d)
    {
        var c = AudioClip.Create(name, d.Length, 1, Rate, false);
        c.SetData(d, 0);
        return c;
    }

    static float[] Buf(float secs) { return new float[Mathf.Max(1, Mathf.RoundToInt(secs * Rate))]; }
    static float Env(float t, float a, float dur) { return t < 0f ? 0f : t < a ? t / a : Mathf.Clamp01(1f - (t - a) / Mathf.Max(0.0001f, dur - a)); }
    static float Sq(float x) { return (x - Mathf.Floor(x)) < 0.5f ? 1f : -1f; }
    static float Saw(float x) { return 2f * (x - Mathf.Floor(x + 0.5f)); }
    static float Tri(float x) { return 1f - 4f * Mathf.Abs(x - Mathf.Floor(x + 0.5f)); }
    static float Sin(float t, float f) { return Mathf.Sin(t * 6.2832f * f); }

    // RBJ band-pass (0 dB peak gain) over a buffer, in place
    static void Reso(float[] x, float f, float q)
    {
        float w = 2f * Mathf.PI * f / Rate, al = Mathf.Sin(w) / (2f * q), a0 = 1f + al;
        float b0 = al / a0, b2 = -al / a0, a1 = -2f * Mathf.Cos(w) / a0, a2 = (1f - al) / a0;
        float x1 = 0f, x2 = 0f, y1 = 0f, y2 = 0f;
        for (int i = 0; i < x.Length; i++)
        {
            float y = b0 * x[i] + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = x[i]; y2 = y1; y1 = y; x[i] = y;
        }
    }

    static void Normalize(float[] d, float peak)
    {
        float m = 0f;
        for (int i = 0; i < d.Length; i++) m = Mathf.Max(m, Mathf.Abs(d[i]));
        if (m < 1e-6f) return;
        float k = peak / m;
        for (int i = 0; i < d.Length; i++) d[i] *= k;
    }

    // airsoft AEG / spring shot: low "thup" of the piston + band-passed gas pop + plastic clack + piston return + air tail
    static float[] ShotV(int v, bool far)
    {
        float len = far ? 0.22f : 0.18f;
        var d = Buf(len);
        float thumpF = 150f + v * 14f, popF = 1800f + v * 350f, clackF = 2600f + v * 260f;
        var pop = new float[d.Length]; var clack = new float[d.Length]; var tail = new float[d.Length];
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            pop[i] = N() * Mathf.Exp(-t * (far ? 70f : 110f));
            clack[i] = (t < 0.003f || (t > 0.042f + v * 0.004f && t < 0.045f + v * 0.004f)) ? N() : 0f;
            tail[i] = N() * Env(t - 0.004f, 0.01f, 0.12f) * 0.25f;
        }
        Reso(pop, popF, 1.4f); Reso(clack, clackF, 6f); Reso(tail, 5200f, 0.7f);
        float lp = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float thump = Mathf.Sin(6.2832f * (thumpF * t - 260f * t * t)) * Mathf.Exp(-t * 55f) * Mathf.Clamp01(t * 3000f);
            float x = thump * (far ? 1.1f : 0.7f) + pop[i] * 3.4f + clack[i] * (far ? 0.4f : 3f) + tail[i] * (far ? 0.3f : 1f);
            if (far) { lp += (x - lp) * 0.35f; x = lp; }
            d[i] = x;
        }
        Normalize(d, far ? 0.6f : 0.85f);
        return d;
    }

    // balloon pop: very fast broadband crack, a rubbery slap that drops in pitch, and the latex flutter of the shreds
    static float[] PopV(int v)
    {
        var d = Buf(0.3f);
        var crack = new float[d.Length]; var flut = new float[d.Length];
        float slapF = 140f + v * 28f, flutHz = 34f + v * 7f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            crack[i] = N() * (Mathf.Exp(-t * (180f + v * 25f)) + 0.25f * Mathf.Exp(-t * 35f));
            flut[i] = N() * Env(t - 0.012f, 0.008f, 0.09f + v * 0.012f) * (0.5f + 0.5f * Mathf.Sin(t * 6.2832f * flutHz));
        }
        Reso(flut, 900f + v * 160f, 1.2f);
        float hp = 0f, prev = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            hp = 0.9f * (hp + crack[i] - prev); prev = crack[i];
            float slap = Mathf.Sin(6.2832f * (slapF * t - 160f * t * t)) * Mathf.Exp(-t * 30f) * 0.75f;
            d[i] = (hp * 1.6f + crack[i] * 0.5f + slap + flut[i] * 1.6f) * Mathf.Clamp01(t * 4000f);
        }
        Normalize(d, 0.95f);
        return d;
    }

    static void Build()
    {
        shots = new AudioClip[4];
        for (int v = 0; v < 4; v++) shots[v] = Clip("shot" + v, ShotV(v, false));
        shotsFar = new AudioClip[2];
        for (int v = 0; v < 2; v++) shotsFar[v] = Clip("shotfar" + v, ShotV(v + 1, true));
        pops = new AudioClip[5];
        for (int v = 0; v < 5; v++) pops[v] = Clip("pop" + v, PopV(v));

        // "your balloon popped": pop + sad descending "wah-wah"
        float[] d = Buf(0.85f); float lp = 0f;
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

        // hit confirm: two bright bell partials (E6 then B6)
        d = Buf(0.32f);
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate, t2 = t - 0.06f;
            float a = (Sin(t, 1318.5f) + 0.35f * Sin(t, 3955f)) * Mathf.Exp(-t * 18f);
            float b = t2 > 0f ? (Sin(t2, 1975.5f) + 0.3f * Sin(t2, 5926f)) * Mathf.Exp(-t2 * 14f) : 0f;
            d[i] = (a + b) * Mathf.Clamp01(t * 2000f) * 0.42f;
        }
        Ding = Clip("ding", d);

        // reload done: charging-handle slide "shk-chk"
        d = Buf(0.26f); var nz = new float[d.Length];
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            nz[i] = N() * (Env(t, 0.01f, 0.07f) * 0.6f + (t > 0.11f ? Mathf.Exp(-(t - 0.11f) * 90f) : 0f));
        }
        Reso(nz, 3200f, 2.5f);
        for (int i = 0; i < d.Length; i++) { float t = i / (float)Rate; d[i] = nz[i] * 2f + (t > 0.11f ? Sin(t, 950f) * Mathf.Exp(-(t - 0.11f) * 120f) * 0.4f : 0f); }
        Normalize(d, 0.8f);
        Slide = Clip("slide", d);

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

        // UI click fallback (the recorded Kenney click is used when present)
        d = Buf(0.05f);
        for (int i = 0; i < d.Length; i++) { float t = i / (float)Rate; d[i] = Sin(t, 1800f) * Env(t, 0.001f, 0.05f) * 0.5f; }
        Click = Clip("click", d);

        Wind = Clip("wind", WindLoop());
    }

    // 12 s seamless wind: low-passed noise with slow gusts (whole-number cycles per loop) and a soft whistle band,
    // crossfaded over its last second so the loop point is inaudible
    static float[] WindLoop()
    {
        float L = 12f, X = 1f;
        int n = Mathf.RoundToInt((L + X) * Rate), loopN = Mathf.RoundToInt(L * Rate), xN = Mathf.RoundToInt(X * Rate);
        var raw = new float[n]; var whistle = new float[n];
        float lp1 = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float g = 0.55f + 0.25f * Mathf.Sin(6.2832f * t * 2f / L) + 0.15f * Mathf.Sin(6.2832f * t * 5f / L + 1.3f) + 0.05f * Mathf.Sin(6.2832f * t * 11f / L);
            float w = N();
            lp1 += (w - lp1) * (0.02f + 0.03f * g);
            lp2 += (lp1 - lp2) * 0.08f;
            raw[i] = lp2 * g * 3f;
            whistle[i] = w * g * g * 0.05f;
        }
        Reso(whistle, 700f, 8f);
        var d = new float[loopN];
        for (int i = 0; i < loopN; i++) d[i] = raw[i] + whistle[i];
        for (int i = 0; i < xN; i++)
        {
            float k = i / (float)xN;
            d[i] = (raw[i] + whistle[i]) * Mathf.Sqrt(k) + (raw[loopN + i] + whistle[loopN + i]) * Mathf.Sqrt(1f - k);
        }
        Normalize(d, 0.7f);
        return d;
    }
}

public class SfxDriver : MonoBehaviour
{
    float placeT;
    void Update()
    {
        if (!Sfx.Unlocked && Kb.AnyGesture()) Sfx.Unlock();
        Sfx.Tick(Time.unscaledDeltaTime);
        placeT -= Time.unscaledDeltaTime;
        if (placeT <= 0f) { placeT = 0.5f; Sfx.PlaceButton(); }
        if (Kb.MDown()) Sfx.CycleVolume();
        foreach (Vector2 p in Kb.TouchesBegan()) if (Sfx.ButtonHit(p)) Sfx.CycleVolume();
        if (Kb.MouseLeftDown() && Cursor.lockState != CursorLockMode.Locked && Sfx.ButtonHit(Kb.MousePos())) Sfx.CycleVolume();
    }
}
