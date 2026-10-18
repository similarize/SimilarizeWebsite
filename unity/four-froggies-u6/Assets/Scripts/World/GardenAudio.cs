using UnityEngine;

// ffu32: synthesised sounds for the garden retreat + horse riding (no audio assets, like Sfx / MechStomp):
//  Birds    - 12 s loop of garden birdsong (warbles, chirp runs, a far cuckoo-ish call), soft
//  Crickets - 6 s loop of crickets (4-5 kHz pulse trains, two voices)
//  Crackle  - 5 s loop of campfire crackle (low roar + pops + snaps)
//  Ignite   - match strike + whoomp as the kindling catches
//  Hiss     - douse: water hiss + sizzle
//  Clop[4]  - hoof strikes (dull thud + woody knock), pitched / mixed per gait in Horse.cs
//  Snort    - horse blow / snort
public static class GardenAudio
{
    public static AudioClip Birds, Crickets, Crackle, Ignite, Hiss, Snort, Creak;
    public static AudioClip[] Clop;
    static bool built;
    static System.Random rnd = new System.Random(32);
    static float N() { return (float)(rnd.NextDouble() * 2.0 - 1.0); }
    static float R() { return (float)rnd.NextDouble(); }

    public static void Ensure()
    {
        if (built) return;
        built = true;
        int rate = Sfx.Rate;
        Birds = Make("g_birds", BuildBirds(rate, 12f), rate);
        Crickets = Make("g_crickets", BuildCrickets(rate, 6f), rate);
        Crackle = Make("g_crackle", BuildCrackle(rate, 5f), rate);
        Ignite = Make("g_ignite", BuildIgnite(rate), rate);
        Hiss = Make("g_hiss", BuildHiss(rate), rate);
        Snort = Make("g_snort", BuildSnort(rate), rate);
        Creak = Make("g_creak", BuildCreak(rate), rate);
        Clop = new AudioClip[4];
        for (int i = 0; i < 4; i++) Clop[i] = Make("g_clop" + i, BuildClop(rate, i), rate);
        Debug.Log("GardenAudio: synthesised birds, crickets, crackle, ignite, hiss, snort, creak, 4 hoof clops");
    }

    static AudioClip Make(string name, float[] d, int rate)
    {
        float peak = 0.001f;
        for (int i = 0; i < d.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(d[i]));
        float g = 0.92f / peak;
        for (int i = 0; i < d.Length; i++) d[i] *= g;
        var c = AudioClip.Create(name, d.Length, 1, rate, false);
        c.SetData(d, 0);
        return c;
    }

    // loop-safe: everything that starts near the end wraps round to the start
    static void AddWrap(float[] d, int at, float v) { d[((at % d.Length) + d.Length) % d.Length] += v; }

    static float[] BuildBirds(int rate, float secs)
    {
        int n = Mathf.RoundToInt(secs * rate);
        var d = new float[n];
        // a quiet leafy bed (very low filtered noise) so it never drops to silence
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++) { lp += (N() - lp) * 0.02f; lp2 += (lp - lp2) * 0.02f; d[i] = (lp - lp2) * 0.05f; }
        // songs: each bird = a run of chirps with its own pitch and shape
        int songs = 15;
        for (int s = 0; s < songs; s++)
        {
            int start = (int)(R() * n);
            int kind = rnd.Next(4);
            float baseF = 2400f + R() * 2600f;
            float vol = 0.25f + R() * 0.45f;
            int notes = kind == 3 ? 2 : 3 + rnd.Next(6);
            float t0 = 0f;
            for (int k = 0; k < notes; k++)
            {
                float len = kind == 0 ? 0.05f + R() * 0.04f : kind == 1 ? 0.11f + R() * 0.08f : kind == 2 ? 0.035f : 0.32f;
                float f0 = baseF * (kind == 3 ? 0.32f : 1f) * (1f + N() * 0.12f);
                float f1 = kind == 0 ? f0 * (1.3f + R() * 0.4f) : kind == 1 ? f0 * (0.7f + R() * 0.2f) : kind == 2 ? f0 * 1.05f : f0 * 0.8f;
                int ln = Mathf.RoundToInt(len * rate);
                float ph = 0f, vib = 18f + R() * 30f;
                for (int i = 0; i < ln; i++)
                {
                    float u = i / (float)ln;
                    float f = Mathf.Lerp(f0, f1, kind == 1 ? u * u : u) * (1f + Mathf.Sin(i / (float)rate * vib * 6.283f) * (kind == 1 ? 0.06f : 0.015f));
                    ph += f / rate;
                    float env = Mathf.Sin(u * Mathf.PI); env *= env;
                    float v = (Mathf.Sin(ph * 6.283f) + 0.18f * Mathf.Sin(ph * 12.566f)) * env * vol;
                    AddWrap(d, start + Mathf.RoundToInt(t0 * rate) + i, v);
                }
                t0 += len + (kind == 2 ? 0.03f : kind == 3 ? 0.18f : 0.04f + R() * 0.09f);
            }
        }
        return d;
    }

    static float[] BuildCrickets(int rate, float secs)
    {
        int n = Mathf.RoundToInt(secs * rate);
        var d = new float[n];
        // two cricket voices with different chirp rates, plus a far chorus (amplitude-modulated hiss at 4.3 kHz)
        float[] fc = { 4600f, 4150f, 5000f };
        float[] chirpHz = { 2.6f, 1.9f, 3.3f };
        float[] vols = { 0.55f, 0.4f, 0.22f };
        for (int v = 0; v < 3; v++)
        {
            float ph = 0f, off = R();
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                // loop-safe: chirp rate x secs is rounded to whole cycles
                float cr = Mathf.Round(chirpHz[v] * secs) / secs;
                float c = (t * cr + off) % 1f;
                float gate = c < 0.28f ? 1f : 0f;
                // each chirp = 4-5 pulses at ~32 Hz
                float pulse = Mathf.Max(0f, Mathf.Sin(t * (32f + v * 5f) * 6.283f));
                ph += fc[v] / rate;
                d[i] += Mathf.Sin(ph * 6.283f) * pulse * pulse * gate * vols[v] * Mathf.Sin(Mathf.Min(1f, c / 0.28f) * Mathf.PI);
            }
        }
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            lp += (N() - lp) * 0.6f;
            float t = i / (float)rate;
            d[i] += lp * 0.025f * (0.6f + 0.4f * Mathf.Sin(t * 6.283f / secs * 3f));
        }
        return d;
    }

    static float[] BuildCrackle(int rate, float secs)
    {
        int n = Mathf.RoundToInt(secs * rate);
        var d = new float[n];
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            // soft roar: low-passed noise with a slow wobble (loop-safe wobble: whole cycles)
            lp += (N() - lp) * 0.04f; lp2 += (lp - lp2) * 0.05f;
            float t = i / (float)rate;
            d[i] = lp2 * 1.8f * (0.75f + 0.25f * Mathf.Sin(t * 6.283f * 2f / secs));
        }
        // pops and snaps
        int pops = Mathf.RoundToInt(secs * 14f);
        for (int p = 0; p < pops; p++)
        {
            int at = (int)(R() * n);
            bool snap = R() < 0.18f;
            float a = snap ? 0.9f + R() * 0.5f : 0.15f + R() * 0.45f;
            int len = Mathf.RoundToInt((snap ? 0.03f : 0.008f + R() * 0.012f) * rate);
            float hp = 0f, prev = 0f;
            for (int i = 0; i < len; i++)
            {
                float x = N();
                hp = 0.6f * (hp + x - prev); prev = x;   // crude high-pass: crisp
                float env = Mathf.Exp(-i / (float)len * 5f);
                AddWrap(d, at + i, hp * env * a);
            }
        }
        return d;
    }

    static float[] BuildIgnite(int rate)
    {
        float secs = 1.6f;
        int n = Mathf.RoundToInt(secs * rate);
        var d = new float[n];
        float hp = 0f, prev = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float x = N();
            hp = 0.7f * (hp + x - prev); prev = x;
            // match scratch 0..0.12 s
            float scratch = t < 0.14f ? hp * (0.5f + 0.5f * Mathf.Sin(t * 180f)) * 0.7f : 0f;
            // whoomp: low-passed noise swelling from 0.2 s, decaying
            lp += (x - lp) * Mathf.Lerp(0.02f, 0.12f, Mathf.Clamp01((t - 0.2f) / 0.3f));
            float sw = t > 0.18f ? Mathf.Clamp01((t - 0.18f) / 0.18f) * Mathf.Exp(-(t - 0.36f) * 2.4f) : 0f;
            d[i] = scratch + lp * sw * 2.4f;
            d[i] *= Mathf.Clamp01((secs - t) / 0.2f);
        }
        return d;
    }

    static float[] BuildHiss(int rate)
    {
        float secs = 1.9f;
        int n = Mathf.RoundToInt(secs * rate);
        var d = new float[n];
        float hp = 0f, prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float x = N();
            hp = 0.85f * (hp + x - prev); prev = x;
            float env = Mathf.Clamp01(t / 0.05f) * Mathf.Exp(-t * 1.6f);
            float sizzle = R() < 0.004f ? N() * 2f : 0f;
            d[i] = hp * env * 0.8f + sizzle * env;
        }
        return d;
    }

    static float[] BuildSnort(int rate)
    {
        float secs = 0.7f;
        int n = Mathf.RoundToInt(secs * rate);
        var d = new float[n];
        float lp = 0f, ph = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            lp += (N() - lp) * 0.25f;
            ph += (38f + 20f * Mathf.Sin(t * 50f)) / rate;
            float flutter = 0.6f + 0.4f * Mathf.Sin(ph * 6.283f);
            float env = Mathf.Clamp01(t / 0.03f) * Mathf.Exp(-t * 4.5f);
            d[i] = lp * flutter * env;
        }
        return d;
    }

    static float[] BuildCreak(int rate)
    {
        float secs = 0.6f;
        int n = Mathf.RoundToInt(secs * rate);
        var d = new float[n];
        float ph = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            // rope / wood creak: a buzzy pulse train with a wandering rate
            float f = 90f + 40f * Mathf.Sin(t * 7f) + 25f * Mathf.Sin(t * 23f);
            ph += f / rate;
            float saw = (ph % 1f) * 2f - 1f;
            float env = Mathf.Sin(Mathf.Clamp01(t / secs) * Mathf.PI);
            d[i] = saw * env * 0.5f + N() * env * 0.05f;
        }
        return d;
    }

    // v 0..3: four slightly different strikes
    static float[] BuildClop(int rate, int v)
    {
        float secs = 0.22f;
        int n = Mathf.RoundToInt(secs * rate);
        var d = new float[n];
        float ph = 0f, ph2 = 0f, lp = 0f, bp = 0f, prev = 0f;
        float f0 = 150f + v * 12f, f1 = 820f + v * 70f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float x = N();
            // dull thud (hoof into turf)
            ph += (f0 * (1f + 0.6f * Mathf.Exp(-t * 60f))) / rate;
            float thud = Mathf.Sin(ph * 6.283f) * Mathf.Exp(-t * 28f);
            // woody knock (the "clop")
            ph2 += f1 / rate;
            float knock = Mathf.Sin(ph2 * 6.283f) * Mathf.Exp(-t * 70f);
            // grit
            lp += (x - lp) * 0.3f; bp = lp - prev; prev = lp;
            float grit = bp * Mathf.Exp(-t * 45f) * 2.2f;
            d[i] = thud * 0.9f + knock * 0.55f + grit * 0.35f;
        }
        return d;
    }
}
