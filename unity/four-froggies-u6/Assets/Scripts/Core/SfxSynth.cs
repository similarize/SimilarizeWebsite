using UnityEngine;

// ffu30 synthesised engine voices (no assets, built once on first use, 22.05 kHz mono loops, every partial a whole number
// of cycles per loop so the loops are seamless at any AudioSource.pitch):
//  kind 12 Cybertruck EV. ffu39 rework (Bill: "way too loud and doesn't sound like a Cybertruck"), modelled on Tesla's own
//          "Normal Operating Sounds" clips + owner reports (a faint high-pitched whine while accelerating, the pedestrian
//          warning whir in Drive at low speed, tyres + wind dominating at speed): six quiet layers, nearly silent overall
//          (peak sum ~0.1, far under music / frogs / effects): CtWhine smooth high motor whine (pitch ~ speed, louder on
//          throttle), CtAvas soft synthetic pedestrian-warning tone (moving under ~20 mph, fades out 7-9.5 m/s), CtRegen
//          subtle regen whine while slowing, Road tyre roll (rises with speed), CtWind (above ~12 m/s), CtCrunch gravel /
//          grass grit (SynthVoice.surface: 0 pavement .. 1 gravel). No hum chord, no combustion rumble, no buzz. No CC0
//          Cybertruck recording was found (the only free drive-by, Wikimedia Commons "Tesla cybertruck - yurakucho", is a
//          CC BY-SA street video), so this stays synthesised.
//  kind 13 RC car: high brushless motor whine + small-tyre gravel hiss (RcCar drives its own voice, distance-scaled).
//  kind 14 2-stroke dirt bike (ring-ding brap), kind 16 4-stroke dirt bike (thump brap): RPM model with gear bands, so
//          the note rises with throttle, drops at each shift and free-revs in the air.
//  kind 15 electric motorbike (Varg / Surron / street e-moto): the EV voice, higher (pitchMul), plus belt whir + regen.
public static class SfxSynth
{
    const int R = 22050;
    public static AudioClip SkateRoll, SkateHum;   // ffu46
    public static AudioClip CtWhine, CtAvas, CtRegen, CtWind, CtCrunch, EvWhine, EvHum, Road, Regen, RcWhine, RcGravel, Bike2s, Bike4s, Intake, Belt;
    static bool built;
    static System.Random rnd = new System.Random(30);
    static float N() { return (float)(rnd.NextDouble() * 2.0 - 1.0); }
    static float S(float hz, float t) { return Mathf.Sin(t * hz * 6.2831853f); }

    static AudioClip Mk(string name, float[] d) { var c = AudioClip.Create(name, d.Length, 1, R, false); c.SetData(d, 0); return c; }

    // noise loops: generate 2 s + 0.25 s, then crossfade the tail into the head (seamless)
    static float[] Seamless(float[] raw, int n)
    {
        int x = raw.Length - n;
        var d = new float[n];
        for (int i = 0; i < n; i++) d[i] = raw[i];
        for (int i = 0; i < x; i++) { float k = i / (float)x; d[i] = raw[n + i] * (1f - k) + raw[i] * k; }
        return d;
    }
    static void Norm(float[] d, float peak)
    {
        float m = 1e-6f; foreach (float v in d) m = Mathf.Max(m, Mathf.Abs(v));
        for (int i = 0; i < d.Length; i++) d[i] = d[i] / m * peak;
    }

    public static void Build()
    {
        if (built) return;
        built = true;
        int n = R * 2;                       // 2 s loops: partials at multiples of 0.5 Hz are seamless
        float[] d;
        // EV motor / inverter whine: f0 420 Hz with motor orders 1, 2, 3, 6 + a faint 9x inverter edge
        d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)R;
            float vib = 1f + 0.06f * S(3f, t);
            d[i] = (S(420f, t) * 0.42f + S(840f, t) * 0.16f + S(1260f, t) * 0.22f * vib + S(2520f, t) * 0.07f + S(3780f, t) * 0.035f) ;
        }
        Norm(d, 0.8f); EvWhine = Mk("syn_ev_whine", d);
        // futuristic hum: low A chord with a slow 0.5 Hz beat + shimmering high partial swell
        d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)R;
            float sw = 0.5f + 0.5f * S(0.5f, t);
            d[i] = S(110f, t) * 0.34f + S(110.5f, t) * 0.22f + S(165f, t) * 0.2f + S(220f, t) * 0.14f + S(277.5f, t) * 0.08f
                 + S(1760f, t) * 0.035f * sw + S(2640.5f, t) * 0.02f * (1f - sw);
        }
        Norm(d, 0.75f); EvHum = Mk("syn_ev_hum", d);
        // tyre / road: soft rumbling noise (two one-pole low-passes) with a light hiss
        {
            int m = n + R / 4; var raw = new float[m]; float a = 0f, b = 0f, h = 0f;
            for (int i = 0; i < m; i++) { float w = N(); a += (w - a) * 0.09f; b += (a - b) * 0.2f; h += (w - h) * 0.6f; raw[i] = b * 2.2f + (w - h) * 0.12f; }
            d = Seamless(raw, n); Norm(d, 0.7f); Road = Mk("syn_road", d);
        }
        // regen whir: 520 Hz + 2nd/3rd with a 6 Hz flutter
        d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)R;
            float fl = 0.75f + 0.25f * S(6f, t);
            d[i] = (S(520f, t) * 0.5f + S(1040f, t) * 0.18f + S(1560f, t) * 0.1f) * fl + S(260f, t) * 0.12f;
        }
        Norm(d, 0.7f); Regen = Mk("syn_regen", d);
        // RC brushless: 900 Hz squarish motor + 2.5x gear mesh + a little commutation buzz
        d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)R;
            d[i] = S(900f, t) * 0.36f + S(1800f, t) * 0.12f + S(2700f, t) * 0.16f + S(4500f, t) * 0.05f + S(2250f, t) * 0.14f + N() * 0.03f;
        }
        Norm(d, 0.75f); RcWhine = Mk("syn_rc_whine", d);
        {
            int m = n + R / 4; var raw = new float[m]; float lo = 0f;
            for (int i = 0; i < m; i++)
            {
                float w = N(); lo += (w - lo) * 0.3f;
                float crack = rnd.NextDouble() < 0.004 ? N() * 2.2f : 0f;
                raw[i] = (w - lo) * 0.5f + crack;
            }
            d = Seamless(raw, n); Norm(d, 0.6f); RcGravel = Mk("syn_rc_gravel", d);
        }
        // 2-stroke: 60 Hz firing; each cycle = sharp ringing pulse + expansion-chamber buzz + per-cycle jitter
        d = new float[n];
        {
            float f = 60f; float lp = 0f; int cyc = -1; float amp = 1f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)R; float ph = t * f; int c = (int)ph; float p = ph - c;
                if (c != cyc) { cyc = c; amp = 0.8f + 0.2f * (float)rnd.NextDouble(); }
                float pulse = Mathf.Exp(-p * 7f) * Mathf.Sin(p * 6.2831853f * 3f);
                float buzz = (2f * (ph * 4f - Mathf.Floor(ph * 4f + 0.5f))) * 0.25f;   // 240 Hz saw (whole cycles)
                lp += (N() - lp) * 0.35f;
                float v = (pulse * 0.9f + buzz * (0.6f + 0.4f * Mathf.Exp(-p * 3f)) + lp * Mathf.Exp(-p * 9f) * 0.6f) * amp;
                d[i] = (float)System.Math.Tanh(v * 1.6f);
            }
        }
        Norm(d, 0.8f); Bike2s = Mk("syn_bike2s", d);
        // 4-stroke thumper: 30 Hz firing; low thump + exhaust bark, alternate cycles slightly different
        d = new float[n];
        {
            float f = 30f; float lp = 0f, lp2 = 0f; int cyc = -1; float amp = 1f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)R; float ph = t * f; int c = (int)ph; float p = ph - c;
                if (c != cyc) { cyc = c; amp = 0.85f + 0.15f * (float)rnd.NextDouble(); }
                float thump = Mathf.Exp(-p * 5f) * (Mathf.Sin(p * 6.2831853f * 2f) + 0.45f * Mathf.Sin(p * 6.2831853f * 5f));
                lp += (N() - lp) * 0.25f; lp2 += (lp - lp2) * 0.3f;
                float bark = lp2 * Mathf.Exp(-p * 8f) * 2.4f;
                float v = (thump + bark) * amp + Mathf.Sin(ph * 6.2831853f * 0.5f) * 0.12f;   // 15 Hz sub (whole cycles)
                d[i] = (float)System.Math.Tanh(v * 1.4f);
            }
        }
        Norm(d, 0.85f); Bike4s = Mk("syn_bike4s", d);
        // intake / mechanical layer (band noise) for the gas bikes
        {
            int m = n + R / 4; var raw = new float[m]; float a = 0f, b = 0f;
            for (int i = 0; i < m; i++) { float w = N(); a += (w - a) * 0.5f; b += (a - b) * 0.12f; raw[i] = a - b; }
            d = Seamless(raw, n); Norm(d, 0.5f); Intake = Mk("syn_intake", d);
        }
        // belt / chain whir for the e-motos: band noise with a 1100 Hz tone riding on it
        {
            int m = n + R / 4; var raw = new float[m]; float a = 0f, b = 0f;
            for (int i = 0; i < m; i++) { float t = i / (float)R; float w = N(); a += (w - a) * 0.45f; b += (a - b) * 0.08f; raw[i] = (a - b) * 0.8f + S(1100f, t) * 0.12f; }
            d = Seamless(raw, n); Norm(d, 0.55f); Belt = Mk("syn_belt", d);
        }
        // ---- ffu39 Cybertruck layers ----
        // motor whine: 1 kHz fundamental (pitch 0.3..2.4 -> 300 Hz..2.4 kHz), gentle 2nd + a faint gear-mesh 3rd, no vibrato
        d = new float[n];
        for (int i = 0; i < n; i++) { float t = i / (float)R; d[i] = S(1000f, t) * 0.8f + S(2000f, t) * 0.12f + S(3000f, t) * 0.05f; }
        Norm(d, 0.6f); CtWhine = Mk("syn_ct_whine", d);
        // pedestrian warning (AVAS): soft open fifth + octave with a slow breathing swell and a soft band-noise whir
        {
            int m = n + R / 4; var raw = new float[m]; float a = 0f, b = 0f;
            for (int i = 0; i < m; i++)
            {
                float t = i / (float)R; float sw = 0.75f + 0.25f * S(0.5f, t);
                float w = N(); a += (w - a) * 0.12f; b += (a - b) * 0.12f;
                raw[i] = (S(330f, t) * 0.4f + S(495f, t) * 0.24f + S(660f, t) * 0.12f + S(990f, t) * 0.04f) * sw + (a - b) * 0.5f;
            }
            d = Seamless(raw, n); Norm(d, 0.55f); CtAvas = Mk("syn_ct_avas", d);
        }
        // regen whine: a slightly lower, purer tone than the drive whine (720 Hz + soft 3rd)
        d = new float[n];
        for (int i = 0; i < n; i++) { float t = i / (float)R; d[i] = S(720f, t) * 0.85f + S(2160f, t) * 0.07f; }
        Norm(d, 0.55f); CtRegen = Mk("syn_ct_regen", d);
        // wind: broadband rush (band-passed noise) with slow gusts (0.5 / 1 Hz, whole cycles)
        {
            int m = n + R / 4; var raw = new float[m]; float a = 0f, b = 0f;
            for (int i = 0; i < m; i++)
            {
                float t = i / (float)R; float w = N(); a += (w - a) * 0.35f; b += (a - b) * 0.04f;
                raw[i] = (a - b) * (0.8f + 0.12f * S(0.5f, t) + 0.08f * S(1f, t));
            }
            d = Seamless(raw, n); Norm(d, 0.55f); CtWind = Mk("syn_ct_wind", d);
        }
        // gravel / grass grit: crackle grains (short decaying clicks) over a soft hiss
        {
            int m = n + R / 4; var raw = new float[m]; float lo = 0f, g = 0f, gd = 0f;
            for (int i = 0; i < m; i++)
            {
                float w = N(); lo += (w - lo) * 0.25f;
                if (rnd.NextDouble() < 0.0025) { g = N() * (0.6f + 0.4f * (float)rnd.NextDouble()); gd = 0.82f + 0.12f * (float)rnd.NextDouble(); }
                g *= gd;
                raw[i] = (w - lo) * 0.18f + g * w * 1.6f;
            }
            d = Seamless(raw, n); Norm(d, 0.55f); CtCrunch = Mk("syn_ct_crunch", d);
        }
        // ---- ffu46 skateboard: urethane roll (low, hollow rumble with a deck resonance ~190 Hz and a faint seam clack twice a
        // loop) + bearing hum (a pure 880 Hz + 1760 Hz whir, higher and thinner than any engine voice) ----
        {
            int m = n + R / 4; var raw = new float[m]; float a = 0f, b = 0f, rz = 0f, rv = 0f;
            for (int i = 0; i < m; i++)
            {
                float t = i / (float)R; float w = N(); a += (w - a) * 0.14f; b += (a - b) * 0.18f;
                // a resonator at 190 Hz driven by the rumble (the deck / hollow trucks)
                float f0 = 190f * 6.2831853f / R; rv += (b * 0.6f - rz * f0 * f0 - rv * 0.02f); rz += rv;
                float ph = Mathf.Repeat(t, 1f); float clack = ph < 0.012f ? Mathf.Exp(-ph * 420f) * Mathf.Sin(ph * 6.2831853f * 1400f) * 0.8f : 0f;
                raw[i] = b * 1.4f + rz * 0.35f + clack;
            }
            d = Seamless(raw, n); Norm(d, 0.7f); SkateRoll = Mk("syn_skate_roll", d);
        }
        d = new float[n];
        for (int i = 0; i < n; i++) { float t = i / (float)R; d[i] = S(880f, t) * 0.6f + S(1760f, t) * 0.18f + S(2640f, t) * 0.05f; }
        Norm(d, 0.5f); SkateHum = Mk("syn_skate_hum", d);
        Debug.Log("SfxSynth: built EV / RC / bike loops + ffu39 Cybertruck layers + ffu46 skateboard");
    }
}

// One synthesised voice: four looping sources (a, b, c, d) levelled + pitched every tick.
public class SynthVoice
{
    public AudioSource a, b, c, d, e, f;   // e, f: ffu39 Cybertruck wind + crunch
    public readonly int kind;
    readonly float pm;
    float lastSpd = -1f, dvS, rpm = 0.2f, gearLo, gearHi = 7f;
    public float gain = 1f;
    public int gear;
    public readonly float[] vol = new float[6], pit = new float[6];
    public float surface = 0.6f;   // ffu39 Cybertruck: 0 pavement / concrete .. 0.6 grass .. 1 gravel / dirt (Vehicle sets it)
    public float extCap = 1f;      // ffu39: cap for e / f (robot-driven distance fade, Vehicle.EngineSound)
    public float decel;
    public static SynthVoice LastCyber, LastBike;

    public SynthVoice(GameObject owner, int kind, float pitchMul)
    {
        SfxSynth.Build();
        this.kind = kind; pm = pitchMul;
        AudioClip ca, cb, cc, cd;
        switch (kind)
        {
            case 13: ca = SfxSynth.RcWhine; cb = SfxSynth.RcGravel; cc = null; cd = null; break;
            case 14: ca = SfxSynth.Bike2s; cb = SfxSynth.Intake; cc = SfxSynth.RcGravel; cd = null; break;
            case 16: ca = SfxSynth.Bike4s; cb = SfxSynth.Intake; cc = SfxSynth.RcGravel; cd = null; break;
            case 15: ca = SfxSynth.EvWhine; cb = SfxSynth.Belt; cc = SfxSynth.Regen; cd = SfxSynth.Road; break;
            case 17: ca = SfxSynth.SkateRoll; cb = SfxSynth.SkateHum; cc = SfxSynth.RcGravel; cd = null; break;   // ffu46 skateboard
            default: ca = SfxSynth.CtWhine; cb = SfxSynth.CtAvas; cc = SfxSynth.CtRegen; cd = SfxSynth.Road; break;   // ffu39
        }
        a = Src(owner, ca); b = Src(owner, cb); c = Src(owner, cc); d = Src(owner, cd);
        if (kind == 12) { e = Src(owner, SfxSynth.CtWind); f = Src(owner, SfxSynth.CtCrunch); }
        if (kind == 12) LastCyber = this;
        if (kind == 14 || kind == 15 || kind == 16) LastBike = this;
    }
    static AudioSource Src(GameObject o, AudioClip clip)
    {
        if (clip == null) return null;
        var s = Sfx.Loop(o, clip); s.Play(); s.Pause();
        return s;
    }
    public void Kill() { foreach (var s in new[] { a, b, c, d, e, f }) if (s != null) Object.Destroy(s); a = b = c = d = e = f = null; }

    void Set(int i, AudioSource s, float v, float p, float dt, float rate = 1.2f)
    {
        vol[i] = v * gain; pit[i] = p;
        if (s == null) return;
        s.volume = Mathf.MoveTowards(s.volume, v * gain, dt * rate);
        s.pitch = Mathf.Lerp(s.pitch, Mathf.Max(0.05f, p), Mathf.Min(1f, dt * 6f));
        if (s.volume <= 0.001f) { if (s.isPlaying) s.Pause(); }
        else if (!s.isPlaying) s.UnPause();
    }

    // on: someone aboard; spd m/s; load 0..1 (throttle); slip m/s sideways; ground 0..1 (wheels down)
    public void Tick(bool on, float spd, float load, float slip, float ground, float dt)
    {
        if (dt <= 0f) return;
        if (lastSpd < 0f) lastSpd = spd;
        float dv = (spd - lastSpd) / dt; lastSpd = spd;
        dvS = Mathf.Lerp(dvS, dv, Mathf.Min(1f, dt * 5f));
        decel = (load < 0.25f && spd > 1.5f) ? Mathf.Clamp01(-dvS / 3.5f) : 0f;
        float k = on ? 1f : 0f;
        switch (kind)
        {
            case 13:   // RC car
            {
                float sp = Mathf.Clamp01(spd / 13f);
                Set(0, a, k * (0.05f + sp * 0.3f + load * 0.12f), (0.42f + spd / 8.5f + load * 0.15f) * pm, dt, 2.5f);
                Set(1, b, k * Mathf.Clamp01(spd / 8f) * 0.16f * ground, 0.9f + spd / 25f, dt, 2.5f);
                break;
            }
            case 14: case 16:   // gas dirt bikes: RPM with gear bands
            {
                float[] bands = { 0f, 7f, 12f, 17f, 22f, 28f, 99f };
                int g = 0; while (g < bands.Length - 2 && spd > bands[g + 1]) g++;
                gear = g + 1; gearLo = bands[g]; gearHi = bands[g + 1];
                float frac = Mathf.Clamp01((spd - gearLo) / Mathf.Max(1f, gearHi - gearLo));
                float target = ground > 0.2f ? (load > 0.1f ? 0.28f + 0.55f * frac + 0.22f * load : 0.12f + 0.3f * frac) : 0.15f + 0.85f * load;
                if (spd < 0.6f && load < 0.1f) target = 0.06f;   // idle
                rpm = Mathf.MoveTowards(rpm, Mathf.Clamp01(target), dt * (target > rpm ? 3.5f : 2.2f));
                float p = 0.42f + rpm * 1.85f;
                bool two = kind == 14;
                Set(0, a, k * (0.24f + load * 0.16f + rpm * 0.1f), p * pm * (two ? 1f : 1f), dt, 3f);
                Set(1, b, k * (0.04f + load * 0.1f) , 0.6f + rpm * 1.3f, dt, 3f);
                Set(2, c, k * Mathf.Clamp01(spd / 10f) * 0.12f * ground, 0.55f + spd / 40f, dt, 2f);
                break;
            }
            case 17:   // ffu46 skateboard: roll (speed, wheels down; ground = 1 off the half-pipe concrete = a little dirt grit) + bearing hum
            {
                float sp = Mathf.Clamp01(spd / 9f);
                Set(0, a, k * ground * Mathf.Clamp01(spd / 0.6f) * (0.06f + 0.22f * sp), 0.55f + spd / 10f, dt, 4f);
                Set(1, b, k * Mathf.Clamp01(spd / 2f) * (0.025f + 0.04f * sp) * (ground > 0.5f ? 1f : 0.6f), (0.62f + spd / 16f) * pm, dt, 2f);
                Set(2, c, k * ground * Mathf.Clamp01(spd / 3f) * 0.05f * Mathf.Clamp01(slip), 0.8f + spd / 30f, dt, 2f);
                break;
            }
            case 15:   // electric motorbikes: the EV voice, higher
            {
                float sp = Mathf.Clamp01(spd / 14f);
                Set(0, a, k * (0.04f + sp * 0.22f + load * 0.12f), (0.36f + spd / 11f + load * 0.1f) * pm, dt, 2f);
                Set(1, b, k * Mathf.Clamp01(spd / 10f) * (0.08f + load * 0.06f), 0.8f + spd / 30f, dt, 2f);
                Set(2, c, k * decel * Mathf.Clamp01(spd / 5f) * 0.2f, (0.4f + spd / 16f) * pm, dt, 2f);
                Set(3, d, k * Mathf.Clamp01(spd / 18f) * 0.12f * ground, 0.85f + spd / 40f, dt);
                break;
            }
            default:   // 12 Cybertruck EV (ffu39: nearly silent - every layer well under music / frogs / effects)
            {
                float mph = spd * 2.237f;
                float sp = Mathf.Clamp01(spd / 30f);
                // motor whine: faint, smooth, high; pitch 300 Hz .. ~2.4 kHz with speed, a little louder on throttle
                Set(0, a, k * (0.004f + 0.014f * sp + 0.018f * load * Mathf.Clamp01(spd / 4f)), (0.3f + spd / 14f + load * 0.04f) * pm, dt, 0.5f);
                // pedestrian warning tone: on while rolling under ~20 mph (fades in from walking pace, out 15-21 mph)
                float avas = Mathf.Clamp01((spd - 0.15f) / 0.6f) * (1f - Mathf.Clamp01((mph - 15f) / 6f));
                Set(1, b, k * avas * 0.03f, (0.92f + spd / 40f) * pm, dt, 0.4f);
                // regen whine while slowing (no throttle, decelerating)
                Set(2, c, k * decel * Mathf.Clamp01(spd / 5f) * 0.02f, (0.4f + spd / 15f) * pm, dt, 0.8f);
                // tyre roll + wind + surface grit
                float gr = Mathf.Clamp01(ground);
                Set(3, d, k * gr * (0.008f + 0.05f * Mathf.Pow(sp, 0.8f)) * Mathf.Clamp01(spd / 1.5f) * (1f - 0.35f * surface), 0.55f + spd / 45f + surface * 0.1f, dt, 0.6f);
                float wind = Mathf.Clamp01((spd - 11f) / 22f);
                Set(4, e, Mathf.Min(extCap, k * wind * wind * 0.06f), 0.8f + spd / 60f, dt, 0.4f);
                Set(5, f, Mathf.Min(extCap, k * gr * surface * Mathf.Clamp01(spd / 3f) * (0.012f + 0.035f * Mathf.Sqrt(sp))), 0.8f + spd / 35f, dt, 0.8f);
                break;
            }
        }
    }

    public string Levels()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        string[] nm = kind == 13 ? new[] { "whine", "gravel", "-", "-", "-", "-" } : (kind == 14 || kind == 16) ? new[] { "engine", "intake", "dirt", "-", "-", "-" } : kind == 15 ? new[] { "whine", "belt", "regen", "road", "-", "-" } : kind == 17 ? new[] { "roll", "bearings", "grit", "-", "-", "-" } : new[] { "whine", "avas", "regen", "road", "wind", "crunch" };
        for (int i = 0; i < 6; i++) if (nm[i] != "-") sb.Append(nm[i]).Append('=').Append(vol[i].ToString("0.00")).Append('@').Append(pit[i].ToString("0.00")).Append(' ');
        if (kind == 12) { float sum = 0f; for (int i = 0; i < 6; i++) sum += vol[i]; sb.Append("sum=").Append(sum.ToString("0.000")).Append(" surf=").Append(surface.ToString("0.0")).Append(' '); }
        if (kind == 14 || kind == 16) sb.Append("rpm=").Append(rpm.ToString("0.00")).Append(" gear=").Append(gear);
        return sb.ToString();
    }
}
