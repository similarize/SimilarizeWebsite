using System.Collections.Generic;
using UnityEngine;

// ffarc66 ARCADE SIGNS (Ben 2026-10-24): one post-and-board sign per live arcade game, scattered on open ranch ground
// (away from the house, garage, track, loop, pad, Monster Park, pool and pond; seeded = the same spots every time).
// Walk up to one: the prompt says "play <game>" (A on a pad / the touch A button / E); pressing it opens that game on
// similarize.com in the same tab (FFNav.jslib; Application.OpenURL off the web). The list is baked from the live cabinets
// on gh-pages games/index.html (2026-10-24; Four Froggies itself and the Archive shelf left out) - re-bake when cabinets
// change. Resume66 saves the players' spots first (back within 2 h = resume). &ffarc0=1 / &ff77old=1 = no signs.
public static class ArcadeSigns66
{
    static int old = -1;
    public static bool Old { get { if (old < 0) old = (Ffu33.Url("ffarc0") == "1" || Ffu33.Url("ff77old") == "1") ? 1 : 0; return old == 1; } }
    public const string Site = "https://www.similarize.com";
    public static readonly string[,] Games =
    {
        { "Lambo Blast 6.6", "/games/lambo-blast-u6/" },
        { "Balloon Blast Airsoft", "/games/balloon-blast-babylon/" },
        { "Balloon Blast 6.6", "/games/balloon-blast-u6/" },
        { "RC Rally Jump", "/games/RCRallyJump/index.html" },
        { "Dirt Track RC Rally", "/games/dirt-track-rc-rally/" },
        { "RC Dune Buggy", "/games/rc-dune-buggy/" },
        { "Froggy Hop Racing", "/games/froggy-hop-racing/" },
        { "Drop Lab", "/games/drop-lab/" },
        { "Asteroids", "/games/asteroids.html" },
        { "Soccer RC", "/games/basketball.html" },
        { "Soccer RC 3D", "/games/soccer-rc-3d/" },
    };
    public static readonly List<Vector3> Spots = new List<Vector3>();
    public static int Opens;

#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")] static extern void FFNavGo(string url);
#else
    static void FFNavGo(string url) { Application.OpenURL(url); }
#endif

    static float Rect(float x, float z, Vector2 c, Vector2 size)
    {
        float dx = Mathf.Max(0f, Mathf.Abs(x - c.x) - size.x * 0.5f), dz = Mathf.Max(0f, Mathf.Abs(z - c.y) - size.y * 0.5f);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }
    static bool Open(float x, float z)
    {
        if (Mathf.Abs(x) > Layout.Half - 25f || Mathf.Abs(z) > Layout.Half - 25f) return false;
        if (Layout.Flatness(x, z) < 0.99f) return false;                       // house / garage / roads / track / loop / pad / Monster Park
        if (Layout.PondQ(x, z) < 1.3f) return false;                           // pond + shore
        if (Rect(x, z, Layout.HouseC, Layout.HouseSize) < 22f || Rect(x, z, Layout.PoolC, Layout.PoolSize) < 14f) return false;
        foreach (var s in Spots) if (new Vector2(s.x - x, s.z - z).magnitude < 24f) return false;
        return true;
    }

    public static void Build()
    {
        if (Old || Spots.Count > 0) return;
        var rnd = new System.Random(6606);
        int n = Games.GetLength(0), tries = 0;
        for (int i = 0; i < n && tries < 4000; tries++)
        {
            float x = (float)(rnd.NextDouble() * 2.0 - 1.0) * (Layout.Half - 25f), z = (float)(rnd.NextDouble() * 2.0 - 1.0) * (Layout.Half - 25f);
            if (!Open(x, z)) continue;
            float gy = Ranch.GY(x, z);
            var p = new Vector3(x, gy, z);
            Spots.Add(p);
            Make(i, p);
            i++;
        }
        Debug.Log("ARC66 arcade signs: " + Spots.Count + "/" + n + " placed (" + tries + " tries)");
    }

    static void Make(int i, Vector3 p)
    {
        string title = Games[i, 0], url = Site + Games[i, 1];
        float yaw = Mathf.Atan2(-p.x, -p.z) * Mathf.Rad2Deg;   // faces the middle of the ranch
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        var root = new GameObject("ArcadeSign " + title).transform;
        root.SetPositionAndRotation(p, q);
        Material wood = Mats.Lit(new Color(0.42f, 0.28f, 0.16f));
        for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, root, new Vector3(1.9f * s, 1.2f, 0f), new Vector3(0.16f, 2.4f, 0.16f), wood);
        Color bg = Color.HSVToRGB((i * 0.13f) % 1f, 0.65f, 0.55f);
        Mats.Prim(PrimitiveType.Cube, root, new Vector3(0f, 1.62f, 0f), new Vector3(4.1f, 0.06f, 0.08f), Mats.Unlit(Color.Lerp(bg, Color.white, 0.55f)));   // small glow strip
        Ranch.Sign(p + q * new Vector3(0f, 2.55f, 0f), yaw, title + "\n<size=22>Play here</size>", bg, 4.2f, 1.7f);
        var h = Interact.Add(p + q * new Vector3(0f, 0f, 1.2f), 3.5f, "play " + title + "!", f => { Resume66.Save(p, yaw); Go(f, title, url); });
        h.enabled = f => f != null && f.world == WorldId.Ranch && f.vehicle == null;
    }

    static void Go(Frog f, string title, string url)
    {
        Opens++;
        Debug.Log("ARC66 open " + title + " -> " + url);
        if (f != null) f.Toast("Off to " + title + "!", 1.5f);
        FFNavGo(url);
    }
}
