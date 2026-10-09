using UnityEngine;

// Loads the recorded CC0 sounds and music packed by work/lb-gfx/bb/build_audio.py as Resources/Audio/<name>.bytes
// ("BBA1": IMA ADPCM, 4 bits per sample, blocks of 4096 frames that each restart the predictor) and turns them into
// PCM AudioClips. Own format instead of Unity's importer so music / ambience loop sample-exact in WebGL (AAC priming
// would leave a gap at the loop point) and the build size stays predictable (~1.8 MB for all of it).
public static class Snd
{
    static readonly int[] Step =
    {
        7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31, 34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97, 107, 118, 130, 143,
        157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449, 494, 544, 598, 658, 724, 796, 876, 963, 1060, 1166, 1282, 1411, 1552,
        1707, 1878, 2066, 2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428, 4871, 5358, 5894, 6484, 7132, 7845, 8630, 9493, 10442, 11487,
        12635, 13899, 15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767
    };
    static readonly int[] IdxAdj = { -1, -1, -1, -1, 2, 4, 6, 8 };

    public static int Loaded, Missing;

    public static AudioClip Load(string name)
    {
        TextAsset ta = null;
        try { ta = Resources.Load<TextAsset>("Audio/" + name); } catch { }
        if (ta == null) { Missing++; return null; }
        try
        {
            AudioClip c = Decode(name, ta.bytes);
            if (c != null) Loaded++; else Missing++;
            return c;
        }
        catch (System.Exception e) { Debug.LogWarning("Snd: " + name + " " + e.Message); Missing++; return null; }
    }

    public static AudioClip[] LoadSet(string prefix, int n)
    {
        var l = new System.Collections.Generic.List<AudioClip>();
        for (int i = 0; i < n; i++) { AudioClip c = Load(prefix + i); if (c != null) l.Add(c); }
        return l.ToArray();
    }

    static int I32(byte[] b, int o) { return b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24); }
    static int I16(byte[] b, int o) { return (short)(b[o] | (b[o + 1] << 8)); }

    static AudioClip Decode(string name, byte[] b)
    {
        if (b.Length < 20 || b[0] != 'B' || b[1] != 'B' || b[2] != 'A' || b[3] != '1') return null;
        int rate = I32(b, 4), ch = I16(b, 8), frames = I32(b, 12), block = I32(b, 16);
        if (ch < 1 || ch > 2 || frames <= 0 || block <= 0) return null;
        var data = new float[frames * ch];
        int o = 20;
        const float k = 1f / 32767f;
        for (int b0 = 0; b0 < frames; b0 += block)
        {
            int n = Mathf.Min(block, frames - b0);
            for (int c = 0; c < ch; c++)
            {
                int pred = I16(b, o), idx = b[o + 2];
                o += 4;
                for (int i = 0; i < n; i++)
                {
                    int by = b[o + (i >> 1)];
                    int code = (i & 1) != 0 ? (by >> 4) : (by & 15);
                    int step = Step[idx];
                    int diff = step >> 3;
                    if ((code & 4) != 0) diff += step;
                    if ((code & 2) != 0) diff += step >> 1;
                    if ((code & 1) != 0) diff += step >> 2;
                    pred = (code & 8) != 0 ? pred - diff : pred + diff;
                    if (pred > 32767) pred = 32767; else if (pred < -32768) pred = -32768;
                    idx += IdxAdj[code & 7];
                    if (idx < 0) idx = 0; else if (idx > 88) idx = 88;
                    data[(b0 + i) * ch + c] = pred * k;
                }
                o += (n + 1) >> 1;
            }
        }
        AudioClip clip = AudioClip.Create(name, frames, ch, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
