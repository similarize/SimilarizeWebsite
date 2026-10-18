using UnityEngine;

// ffu33 synthesised sounds (no assets, same approach as Sfx / MechStomp): orb chimes (one per colour, pentatonic bells),
// the golden-orb fanfare, and James's jet: turbine whine loop, jet roar loop, afterburner rumble loop, sonic boom,
// VTOL lift-fan roar loop. Built lazily on first use.
public static class Synth33
{
    static AudioClip[] chimes;
    static AudioClip fanfare, whine, roar, burner, boom, fan;
    static int Rate { get { return Sfx.Rate; } }

    static AudioClip Make(string name, float[] d) { var c = AudioClip.Create(name, d.Length, 1, Rate, false); c.SetData(d, 0); return c; }
    static void Norm(float[] d, float g)
    {
        float pk = 0.0001f; for (int i = 0; i < d.Length; i++) pk = Mathf.Max(pk, Mathf.Abs(d[i]));
        float k = g / pk; for (int i = 0; i < d.Length; i++) d[i] *= k;
    }

    // bell: inharmonic partials with fast-decaying highs + a soft shimmer tail
    static float[] Bell(float f0, float secs, float bright)
    {
        int n = Mathf.RoundToInt(secs * Rate);
        var d = new float[n];
        float[] ratio = { 1f, 2.01f, 2.76f, 4.07f, 5.43f };
        float[] amp = { 1f, 0.55f, 0.35f * bright, 0.22f * bright, 0.12f * bright };
        float[] dec = { 2.2f, 3.4f, 5f, 7f, 9f };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate, s = 0f;
            for (int k = 0; k < ratio.Length; k++) if (f0 * ratio[k] < Rate * 0.45f) s += Mathf.Sin(6.2832f * f0 * ratio[k] * t) * amp[k] * Mathf.Exp(-t * dec[k]);
            s *= Mathf.Clamp01(t / 0.002f);
            d[i] = s;
        }
        return d;
    }

    public static AudioClip Chime(int colour)
    {
        if (chimes == null)
        {
            // C major pentatonic, two octaves: every colour its own note, so a run of orbs plays a little tune
            float[] notes = { 784f, 880f, 1047f, 1175f, 1319f, 1568f, 1760f, 2093f };
            chimes = new AudioClip[notes.Length];
            for (int i = 0; i < notes.Length; i++)
            {
                float[] a = Bell(notes[i], 1.3f, 1f), b = Bell(notes[i] * 2f, 1.3f, 0.6f);
                for (int k = 0; k < a.Length; k++) a[k] = a[k] + b[k] * 0.35f * Mathf.Clamp01((k / (float)Rate - 0.06f) * 30f);   // sparkle grace note
                Norm(a, 0.8f);
                chimes[i] = Make("orb chime " + i, a);
            }
        }
        return chimes[Mathf.Abs(colour) % chimes.Length];
    }

    // golden orb: bright brass-ish arpeggio C-E-G-C' + held chord with bells on top
    public static AudioClip Fanfare
    {
        get
        {
            if (fanfare != null) return fanfare;
            float secs = 2.6f; int n = Mathf.RoundToInt(secs * Rate);
            var d = new float[n];
            float[] f = { 523.25f, 659.25f, 783.99f, 1046.5f };
            float[] at = { 0f, 0.13f, 0.26f, 0.42f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, s = 0f;
                for (int k = 0; k < 4; k++)
                {
                    float tt = t - at[k];
                    if (tt < 0f) continue;
                    float env = Mathf.Clamp01(tt / 0.02f) * (k == 3 ? Mathf.Exp(-tt * 0.9f) : Mathf.Exp(-tt * 1.4f));
                    float ph = 6.2832f * f[k] * tt;
                    // brass: a few odd/even harmonics with a slight vibrato
                    float vib = 1f + 0.004f * Mathf.Sin(6.2832f * 5.5f * tt);
                    float b = Mathf.Sin(ph * vib) + 0.5f * Mathf.Sin(2f * ph * vib) + 0.3f * Mathf.Sin(3f * ph * vib) + 0.15f * Mathf.Sin(4f * ph * vib);
                    s += b * env * 0.5f;
                    s += Mathf.Sin(6.2832f * f[k] * 2f * tt) * Mathf.Exp(-tt * 4f) * 0.3f;   // bell on each note
                }
                d[i] = (float)System.Math.Tanh(s * 0.9f) * Mathf.Clamp01((secs - t) / 0.3f);
            }
            Norm(d, 0.9f);
            return fanfare = Make("golden fanfare", d);
        }
    }

    // seamless loops: built from sines with whole-number cycles + looped filtered noise
    static float[] NoiseLoop(int n, float lpA, float lpB, int seed)
    {
        var r = new System.Random(seed);
        var d = new float[n];
        float a = 0f, b = 0f;
        // run twice so the filter state at the start matches the end (no click at the loop point)
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < n; i++)
            {
                float x = (float)(r.NextDouble() * 2 - 1);
                a += (x - a) * lpA; b += (a - b) * lpB;
                if (pass == 1) d[i] = a - b * 0.6f;
            }
        return d;
    }

    public static AudioClip Whine
    {
        get
        {
            if (whine != null) return whine;
            float secs = 1f; int n = Mathf.RoundToInt(secs * Rate);
            var d = new float[n];
            var nz = NoiseLoop(n, 0.5f, 0.05f, 33);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                // turbine: 1200 / 2400 / 3650 Hz lines (whole cycles per second -> seamless) + hiss
                d[i] = Mathf.Sin(6.2832f * 1200f * t) * 0.35f + Mathf.Sin(6.2832f * 2400f * t) * 0.18f + Mathf.Sin(6.2832f * 3650f * t) * 0.08f + nz[i] * 0.5f;
            }
            Norm(d, 0.6f);
            return whine = Make("jet whine", d);
        }
    }

    public static AudioClip Roar
    {
        get
        {
            if (roar != null) return roar;
            int n = Rate * 2;
            var d = NoiseLoop(n, 0.12f, 0.01f, 34);
            for (int i = 0; i < n; i++) { float t = i / (float)Rate; d[i] = (float)System.Math.Tanh(d[i] * 3f + Mathf.Sin(6.2832f * 55f * t) * 0.15f); }
            Norm(d, 0.75f);
            return roar = Make("jet roar", d);
        }
    }

    public static AudioClip Burner
    {
        get
        {
            if (burner != null) return burner;
            int n = Rate * 2;
            var d = NoiseLoop(n, 0.035f, 0.004f, 35);
            var cr = NoiseLoop(n, 0.6f, 0.2f, 36);
            for (int i = 0; i < n; i++) { float t = i / (float)Rate; d[i] = (float)System.Math.Tanh(d[i] * 6f + Mathf.Sin(6.2832f * 38f * t) * 0.3f) + cr[i] * 0.25f * (0.6f + 0.4f * Mathf.Sin(6.2832f * 9f * t)); }
            Norm(d, 0.85f);
            return burner = Make("afterburner", d);
        }
    }

    public static AudioClip Fan
    {
        get
        {
            if (fan != null) return fan;
            int n = Rate;
            var d = NoiseLoop(n, 0.25f, 0.02f, 37);
            for (int i = 0; i < n; i++) { float t = i / (float)Rate; d[i] = d[i] * (0.75f + 0.25f * Mathf.Sin(6.2832f * 24f * t)) + Mathf.Sin(6.2832f * 96f * t) * 0.18f + Mathf.Sin(6.2832f * 480f * t) * 0.06f; }
            Norm(d, 0.7f);
            return fan = Make("lift fan", d);
        }
    }

    // sonic boom: double crack (N-wave) + low rumble tail
    public static AudioClip Boom
    {
        get
        {
            if (boom != null) return boom;
            float secs = 2f; int n = Mathf.RoundToInt(secs * Rate);
            var d = new float[n];
            var r = new System.Random(38);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float nw = 0f;
                for (int k = 0; k < 2; k++)
                {
                    float tt = t - k * 0.12f;
                    if (tt >= 0f && tt < 0.09f) nw += (1f - tt / 0.045f) * (k == 0 ? 1f : 0.85f);   // N-wave ramp
                }
                float x = (float)(r.NextDouble() * 2 - 1);
                lp += (x - lp) * 0.02f;
                float rum = lp * 7f * Mathf.Exp(-t * 1.6f) * Mathf.Clamp01(t / 0.05f);
                float thump = Mathf.Sin(6.2832f * (48f + 30f * Mathf.Exp(-t * 10f)) * t) * Mathf.Exp(-t * 3f);
                d[i] = (float)System.Math.Tanh(nw * 2.2f + rum + thump * 0.8f) * Mathf.Clamp01((secs - t) / 0.3f);
            }
            Norm(d, 0.95f);
            return boom = Make("sonic boom", d);
        }
    }
}
