using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ffu77d (Ben 10-23: "the grass is just growing right in that pathway after it mows"): mowed grass STAYS mowed. A 0.5 m bit
// mask over the ranch (800 x 800 bits = 80 KB) is set under every mower deck (riding mowers, robots' push mowers: everything
// that lays MowStripes). Grass56 rebuilds the touched 10 m cells (a few per frame) with stubble-height clumps where the mask
// is cut. No regrowth. Saved (run-length, a few KB) in SaveStore "ff.mow77" every 20 s while it changes, loaded at start.
// Local to each device (online: each device sees the mowing it simulates). &ff77old=1 = before.
public static class MowMask
{
    const float Res = 0.5f;
    const string Key = "ff.mow77";
    static int N;
    static BitArray bits;
    public static readonly HashSet<int> Dirty = new HashSet<int>();
    static bool saveDirty; static float saveT;
    public static int Marked;
    static readonly Dictionary<object, Vector3> last = new Dictionary<object, Vector3>();

    static bool Ensure()
    {
        if (F77.Old) return false;
        if (bits != null) return true;
        N = Mathf.CeilToInt(Layout.Half * 2f / Res);
        bits = new BitArray(N * N);
        Load();
        return true;
    }
    static int Idx(float x, float z)
    {
        int ix = Mathf.FloorToInt((x + Layout.Half) / Res), iz = Mathf.FloorToInt((z + Layout.Half) / Res);
        if (ix < 0 || iz < 0 || ix >= N || iz >= N) return -1;
        return iz * N + ix;
    }
    public static bool Cut(float x, float z) { if (!Ensure()) return false; int i = Idx(x, z); return i >= 0 && bits[i]; }

    // a deck at 'at' (width w) this frame; key = the mower (joins the gap since its last frame so fast passes leave no holes)
    public static void Deck(object key, Vector3 at, float w)
    {
        if (!Ensure()) return;
        Vector3 from;
        if (!last.TryGetValue(key, out from) || (from - at).sqrMagnitude > 9f) from = at;
        last[key] = at;
        float d = Vector2.Distance(new Vector2(from.x, from.z), new Vector2(at.x, at.z));
        int steps = Mathf.Max(1, Mathf.CeilToInt(d / 0.25f));
        for (int s = 0; s <= steps; s++) Disc(Vector3.Lerp(from, at, s / (float)steps), w * 0.5f);
    }
    public static void Lift(object key) { last.Remove(key); }

    static void Disc(Vector3 p, float r)
    {
        int ix0 = Mathf.FloorToInt((p.x - r + Layout.Half) / Res), ix1 = Mathf.FloorToInt((p.x + r + Layout.Half) / Res);
        int iz0 = Mathf.FloorToInt((p.z - r + Layout.Half) / Res), iz1 = Mathf.FloorToInt((p.z + r + Layout.Half) / Res);
        float r2 = (r + Res * 0.35f) * (r + Res * 0.35f);
        for (int iz = Mathf.Max(0, iz0); iz <= Mathf.Min(N - 1, iz1); iz++)
            for (int ix = Mathf.Max(0, ix0); ix <= Mathf.Min(N - 1, ix1); ix++)
            {
                int i = iz * N + ix;
                if (bits[i]) continue;
                float cx = (ix + 0.5f) * Res - Layout.Half, cz = (iz + 0.5f) * Res - Layout.Half;
                if ((cx - p.x) * (cx - p.x) + (cz - p.z) * (cz - p.z) > r2) continue;
                bits[i] = true; Marked++; saveDirty = true;
                Dirty.Add(Grass56.KeyAt(cx, cz));
            }
    }

    // called every frame by Grass56
    public static void Tick()
    {
        if (!saveDirty || bits == null) return;
        saveT += Time.unscaledDeltaTime;
        if (saveT < 20f) return;
        saveT = 0f; saveDirty = false;
        Save();
    }

    // run lengths of alternating bits (starting with uncut), 7-bit varints, base64
    static void Save()
    {
        var o = new List<byte>();
        bool cur = false; int run = 0;
        for (int i = 0; i < bits.Length; i++)
        {
            if (bits[i] == cur) { run++; continue; }
            Var(o, run); cur = !cur; run = 1;
        }
        Var(o, run);
        try { SaveStore.SetString(Key, N + ":" + System.Convert.ToBase64String(o.ToArray())); SaveStore.Flush(); } catch (System.Exception e) { Debug.LogWarning("FF77 mow save: " + e.Message); }
        Debug.Log("FF77 mowed lawn saved (" + o.Count + " bytes)");
    }
    static void Var(List<byte> o, int v) { while (v >= 0x80) { o.Add((byte)(v & 0x7f | 0x80)); v >>= 7; } o.Add((byte)v); }

    static void Load()
    {
        string s = "";
        try { s = SaveStore.GetString(Key, ""); } catch { }
        int c = s.IndexOf(':');
        if (c <= 0 || s.Substring(0, c) != N.ToString()) return;
        try
        {
            byte[] b = System.Convert.FromBase64String(s.Substring(c + 1));
            int i = 0, p = 0; bool cur = false;
            while (p < b.Length && i < bits.Length)
            {
                int v = 0, sh = 0;
                while (p < b.Length) { byte x = b[p++]; v |= (x & 0x7f) << sh; sh += 7; if ((x & 0x80) == 0) break; }
                if (cur) for (int k = 0; k < v && i + k < bits.Length; k++) bits[i + k] = true;
                i += v; cur = !cur;
            }
            Debug.Log("FF77 mowed lawn loaded");
        }
        catch (System.Exception e) { Debug.LogWarning("FF77 mow load: " + e.Message); }
    }
}
