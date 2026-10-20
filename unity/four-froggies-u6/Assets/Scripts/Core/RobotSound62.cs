using UnityEngine;

// ffu62 (Bill: "robots should make a robot sound, not a footsteps sound - like a motor winding"): procedural robot audio.
// Each robot step = a short servo / motor whirr (pitch rises with speed, lower for big robots) + a soft mechanical clunk,
// instead of the footstep clip (StepMetal fell back to the froggy Step when step_metal was missing). Played through
// Sfx.PlayAt, so it is positional / distance-scaled and follows the game's sound setting like everything else.
public static class RobotSound62
{
    static AudioClip[] whirr; static AudioClip clunk;
    const int SR = 22050;

    static void Build()
    {
        whirr = new AudioClip[3];
        for (int k = 0; k < 3; k++)
        {
            float dur = 0.26f + k * 0.05f; int n = (int)(SR * dur); var d = new float[n];
            float f0 = 520f + k * 90f, f1 = f0 * (1.9f + k * 0.15f), ph = 0f, ph2 = 0f;
            var rng = new System.Random(62 + k);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = Mathf.Lerp(f0, f1, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t * 1.6f))) * (1f - 0.12f * Mathf.Max(0f, t - 0.7f) / 0.3f);
                ph += f / SR; ph2 += f * 2.02f / SR;
                float env = Mathf.Clamp01(t * 25f) * Mathf.Clamp01((1f - t) * 6f);
                float s = Mathf.Sin(ph * 6.2832f) * 0.55f + Mathf.Sin(ph2 * 6.2832f) * 0.22f + ((ph * 3f) % 1f - 0.5f) * 0.18f;   // whine + gear buzz
                s += ((float)rng.NextDouble() - 0.5f) * 0.06f;
                d[i] = s * env * 0.5f;
            }
            whirr[k] = AudioClip.Create("robot_whirr" + k, n, 1, SR, false); whirr[k].SetData(d, 0);
        }
        {
            int n = (int)(SR * 0.16f); var d = new float[n]; var rng = new System.Random(7);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float thump = Mathf.Sin(6.2832f * (70f + 40f * Mathf.Exp(-t * 40f)) * t) * Mathf.Exp(-t * 28f);
                float click = ((float)rng.NextDouble() - 0.5f) * Mathf.Exp(-t * 300f) * 0.8f;
                float ring = Mathf.Sin(6.2832f * 1750f * t) * Mathf.Exp(-t * 60f) * 0.12f;
                d[i] = (thump * 0.7f + click + ring) * 0.55f;
            }
            clunk = AudioClip.Create("robot_clunk", n, 1, SR, false); clunk.SetData(d, 0);
        }
    }

    public static AudioClip Whirr { get { if (whirr == null) Build(); return whirr[Random.Range(0, whirr.Length)]; } }
    public static AudioClip Clunk { get { if (clunk == null) Build(); return clunk; } }

    // one robot step: whirr (pitch with speed, lower for tall robots) + clunk at the feet
    public static void Step(Vector3 feet, float speed, float height)
    {
        float big = Mathf.Clamp(1.8f / Mathf.Max(0.5f, height), 0.6f, 1.4f);
        float sp = Mathf.Clamp(0.85f + speed * 0.09f, 0.85f, 1.6f);
        Sfx.PlayAt(Whirr, feet + Vector3.up * height * 0.45f, 0.2f, 26f, sp * big * Random.Range(0.95f, 1.05f));
        Sfx.PlayAt(Clunk, feet, 0.16f, 20f, big * Random.Range(0.9f, 1.1f));
    }
}
