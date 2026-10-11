using UnityEngine;

// ffu28: giant footfalls. Every mech foot plant (story mechs 12 / 30 / 60 / 160 m and the 3.3 m Optimus suit) and every
// landing from a jump / rockets calls MechStomp.Foot(): a synthesised BOOM (sub-bass sweep + mid thump, soft-clipped so
// the harmonics still carry on phone / car speakers, plus a gravel crunch and a short rumble tail), pitched down and
// louder with mech size and faded by distance to the nearest human froggy; a ground QUAKE on the cameras of players in
// range (Game.Quake: short, angular + vertical, never in menus / cutscenes); a dust cloud at the foot; loose props jump.
// Called from the walk cycle at the foot plant (StoryMech.Animate / the Optimus suit's animate), never from a timer.
public static class MechStomp
{
    static AudioClip[] clips;
    static float budgetT;
    static int budget;
    public static int Count;          // footfalls so far (demo log)
    public static float LastVol, LastShake;
    static readonly Color DustC = new Color(0.62f, 0.53f, 0.40f, 0.5f);

    // 0 = Optimus-suit size (3.3 m), 1 = trillion-story (160 m)
    public static float SizeK(float size) { return Mathf.Clamp01(Mathf.Log(Mathf.Max(size, 3f) / 3f) / Mathf.Log(160f / 3f)); }

    static void Build()
    {
        clips = new AudioClip[3];
        var rnd = new System.Random(28);
        for (int v = 0; v < clips.Length; v++)
        {
            int rate = Sfx.Rate;
            float secs = 1.7f;
            var d = new float[Mathf.RoundToInt(secs * rate)];
            float ph = 0f, ph2 = 0f, lp = 0f, lpl = 0f, hp, lpR = 0f, grit = 0f, peak = 0.001f;
            float f0 = 74f + v * 7f, f1 = 27f + v * 2f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)rate;
                float n = (float)(rnd.NextDouble() * 2.0 - 1.0);
                // sub-bass: fast sweep down (the "boom")
                float f = f1 + (f0 - f1) * Mathf.Exp(-t * 9f);
                ph += f / rate;
                float atk = Mathf.Clamp01(t / 0.004f);
                float sub = Mathf.Sin(ph * 6.2832f) * Mathf.Exp(-t * 3.0f) * atk;
                // mid thump (the "thud" a phone speaker can play)
                ph2 += (62f + 70f * Mathf.Exp(-t * 22f)) / rate;
                float thump = Mathf.Sin(ph2 * 6.2832f) * Mathf.Exp(-t * 13f) * atk;
                float body = Tanh(1.9f * (sub * 0.95f + thump * 0.65f));
                // crunch: band-passed noise burst + sparse gravel crackles over the first ~0.35 s
                lp += (n - lp) * 0.45f; lpl += (lp - lpl) * 0.08f; hp = lp - lpl;
                float crunch = hp * Mathf.Exp(-t * 26f) * 0.9f;
                if (t < 0.4f && rnd.NextDouble() < 0.0045 * (1f - t / 0.4f)) grit = (float)(rnd.NextDouble() * 0.8 + 0.4) * (rnd.NextDouble() < 0.5 ? -1f : 1f);
                grit *= 0.93f;
                // rumble tail: low-passed noise
                lpR += (n - lpR) * 0.012f;
                float rumble = lpR * 6f * Mathf.Exp(-t * 2.4f) * Mathf.Clamp01(t / 0.03f);
                float s = body * 0.78f + crunch * 0.32f + grit * 0.22f + rumble * 0.3f;
                // gentle fade at the very end
                s *= Mathf.Clamp01((secs - t) / 0.15f);
                d[i] = s;
                peak = Mathf.Max(peak, Mathf.Abs(s));
            }
            float g = 0.95f / peak;
            for (int i = 0; i < d.Length; i++) d[i] *= g;
            var c = AudioClip.Create("mechstomp" + v, d.Length, 1, rate, false);
            c.SetData(d, 0);
            clips[v] = c;
        }
    }
    static float Tanh(float x) { float e = Mathf.Exp(2f * Mathf.Clamp(x, -9f, 9f)); return (e - 1f) / (e + 1f); }

    // p = the planted foot on the ground; size = mech height in metres; power 1 = a walking step, ~1.5-2.6 = a landing
    public static void Foot(Vector3 p, float size, float power = 1f, bool props = true)
    {
        if (clips == null) Build();
        Count++;
        float k = SizeK(size);
        // FX budget: 16 mechs + robots could all step at once; sound + quake always, dust capped
        if (Time.unscaledTime > budgetT) { budgetT = Time.unscaledTime + 0.1f; budget = Look.Mobile ? 3 : 6; }
        // --- sound: nearest human froggy (the pilot of a 160 m mech sits ~160 m above the foot, so ranges grow with size)
        float d = Sfx.Near(p);
        if (d > 1e8f) d = 30f;
        float range = 70f + size * 4f;                              // Optimus 83 m, 10-story 118 m, trillion 710 m
        float a = Mathf.Clamp01(1f - d / range);
        float vol = Mathf.Lerp(0.45f, 1f, k) * Mathf.Clamp(power, 0.5f, 1.6f) * a * Mathf.Sqrt(a);
        float pitch = Mathf.Lerp(1.45f, 0.58f, k) * Random.Range(0.94f, 1.06f) * (power > 1.3f ? 0.9f : 1f);
        LastVol = vol;
        if (vol > 0.02f) Sfx.Play(clips[Random.Range(0, clips.Length)], Mathf.Min(1f, vol), pitch);
        // --- quake: mild for the Optimus suit, strong for the story mechs, biggest for the biggest
        float q = (size < 6f ? 0.16f : Mathf.Lerp(0.55f, 0.95f, (k - 0.37f) / 0.63f)) * Mathf.Clamp(power, 0.5f, 2f);
        float qRange = 28f + size * 2.6f;                           // Optimus 37 m, 10-story 59 m, trillion 444 m
        LastShake = q;
        if (Game.I != null) Game.I.Quake(p, q, qRange);
        // --- dust cloud at the foot + loose props jump
        if (budget-- > 0)
        {
            float r = size < 6f ? Mathf.Max(0.6f, size * 0.07f) : size * 0.15f;   // suit ~0.6 m puffs, 10-story ~1.8 m, trillion ~24 m
            int n = Mathf.RoundToInt((Look.Mobile ? 5f : 9f) * Mathf.Clamp(power, 0.6f, 2f));
            for (int i = 0; i < n; i++)
            {
                Vector2 u = Random.insideUnitCircle.normalized;
                Vector3 dir = new Vector3(u.x, 0f, u.y);
                FX.Puff(p + dir * r * Random.Range(0.3f, 0.9f) + Vector3.up * r * 0.15f,
                        dir * r * Random.Range(1.2f, 2.4f) + Vector3.up * r * Random.Range(0.3f, 0.9f),
                        r * Random.Range(0.7f, 1.3f), Random.Range(0.8f, 1.5f), DustC);
            }
            if (props) foreach (Collider c in Physics.OverlapSphere(p, size * 0.15f + 1.5f, 1 << Vehicle.PropLayer))
                if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic)
                    c.attachedRigidbody.AddExplosionForce(150f + size * 40f * power, p - Vector3.up * 0.5f, size * 0.25f + 3f, 0.8f);
        }
    }
}
