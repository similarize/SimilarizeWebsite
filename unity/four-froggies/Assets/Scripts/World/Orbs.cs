using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ffu33 glowing orbs in the Enchanted Forest (Bill: "They're really cool, and they're all different colors. The golden one
// is maybe one of the biggest and most precious."). 28 coloured orbs (7 colours x 4) floating and bobbing under the canopy,
// plus 2 big rare GOLDEN orbs hidden deep in the forest (one in the fairy ring of glowing mushrooms) worth 10 each.
//  * look: HDR-bright unlit core (blooms through LBPost / RealPost) + a soft camera-facing halo + a faint outer halo that
//    pulses; only the golden orbs carry a real point light (range 7);
//  * a human froggy (on foot or in a vehicle) touching one collects it: chime (each colour its own pentatonic note),
//    coloured sparkle burst, per-colour counter on that player's HUD (a row of coloured dots under the name);
//    golden = fanfare + big gold burst + toast;
//  * progress saved in PlayerPrefs (ff.orbs.n = lifetime count per colour, ff.orbs.t = which orbs are gone and when they
//    come back, wall-clock), orbs respawn after 150 s (golden 420 s);
//  * cheap: no physics, distance checks only while a human froggy is on the ranch, animation only within 90 m.
public class Orbs : MonoBehaviour
{
    public static Orbs I;
    public static readonly string[] ColourNames = { "red", "orange", "green", "cyan", "blue", "purple", "pink", "GOLDEN" };
    public static readonly Color[] Colours =
    {
        new Color(1f, 0.22f, 0.2f), new Color(1f, 0.55f, 0.12f), new Color(0.35f, 1f, 0.3f), new Color(0.2f, 0.95f, 1f),
        new Color(0.25f, 0.45f, 1f), new Color(0.72f, 0.3f, 1f), new Color(1f, 0.38f, 0.78f), new Color(1f, 0.8f, 0.22f)
    };
    public const int Gold = 7;
    const float Respawn = 150f, RespawnGold = 420f;

    public class Orb
    {
        public int colour; public Vector3 basePos; public Transform root, core, halo, halo2; public bool taken; public double back; public float phase;
        public Light light;
        public bool IsGold { get { return colour == Gold; } }
    }
    public readonly List<Orb> orbs = new List<Orb>();
    public readonly int[] counts = new int[8];
    public int lastColour = -1; public float lastT = -99f;   // HUD flash
    public static int Collected;     // this session (demo log)
    Material haloMat;
    readonly Dictionary<Slot, OrbRow> rows = new Dictionary<Slot, OrbRow>();

    public static void Create()
    {
        if (I != null) return;
        I = new GameObject("Forest orbs").AddComponent<Orbs>();
        I.Build();
    }

    static float GY(float x, float z) { return Ranch.GY(x, z); }

    void Build()
    {
        var r = new System.Random(3304);
        System.Func<float> R = () => (float)r.NextDouble();
        Vector2 mn = JetLayout.ForestMin, mx = JetLayout.ForestMax;
        haloMat = new Material(FX.SoftMat()) { name = "Orb halo" };
        var spots = new List<Vector3>();
        // coloured orbs: spread over the forest, not too close to each other or to a trunk
        for (int c = 0; c < 7; c++)
            for (int k = 0; k < 4; k++)
            {
                Vector3 best = Vector3.zero;
                for (int tries = 0; tries < 60; tries++)
                {
                    float x = Mathf.Lerp(mn.x + 4f, mx.x - 4f, R()), z = Mathf.Lerp(mn.y + 4f, mx.y - 4f, R());
                    bool ok = true;
                    foreach (Vector3 s in spots) if ((new Vector2(s.x - x, s.z - z)).sqrMagnitude < 36f) { ok = false; break; }
                    if (ok) foreach (Vector3 t in Forest.TreeBases) if ((new Vector2(t.x - x, t.z - z)).sqrMagnitude < 4f) { ok = false; break; }
                    if (!ok) continue;
                    best = new Vector3(x, GY(x, z) + 0.95f + R() * 0.7f, z);
                    break;
                }
                if (best == Vector3.zero) { float x = Mathf.Lerp(mn.x + 4f, mx.x - 4f, R()), z = Mathf.Lerp(mn.y + 4f, mx.y - 4f, R()); best = new Vector3(x, GY(x, z) + 1.2f, z); }
                spots.Add(best);
                Add(c, best);
            }
        // golden: the fairy ring (deep north-east) and the far north-west corner behind the logs
        Vector3 fr = Forest.FairyRing;
        Add(Gold, new Vector3(fr.x, fr.y + 1.5f, fr.z));
        float gx = mn.x + 7f, gz = mx.y - 9f;
        Add(Gold, new Vector3(gx, GY(gx, gz) + 1.6f, gz));
        Load();
        Camera.onPreCull += Billboard;
        Debug.Log("Orbs: " + orbs.Count + " orbs (" + Remaining + " out), collected so far " + string.Join(",", System.Array.ConvertAll(counts, x => x.ToString())));
    }

    void Add(int colour, Vector3 p)
    {
        bool gold = colour == Gold;
        var o = new Orb { colour = colour, basePos = p, phase = Random.Range(0f, 6.28f) };
        o.root = new GameObject(gold ? "Golden orb" : "Orb " + ColourNames[colour]).transform;
        o.root.SetParent(transform, false);
        o.root.position = p;
        Color c = Colours[colour];
        var coreMat = new Material(Mats.Unlit(Color.white)) { name = "Orb core " + ColourNames[colour] };
        coreMat.color = c * (gold ? 2.6f : 2.2f);
        float rad = gold ? 0.44f : 0.22f;
        o.core = Mats.Prim(PrimitiveType.Sphere, o.root, Vector3.zero, Vector3.one * rad * 2f, coreMat, false).transform;
        Mats.NoShadows(o.core.gameObject);
        var hot = new Material(Mats.Unlit(Color.white)) { name = "Orb heart" };
        hot.color = Color.Lerp(c, Color.white, 0.75f) * 2.8f;
        Mats.NoShadows(Mats.Prim(PrimitiveType.Sphere, o.core, Vector3.zero, Vector3.one * 0.5f, hot, false));
        o.halo = Quad(o.root, gold ? 2.8f : 1.5f, new Color(c.r, c.g, c.b, 0.62f));
        o.halo2 = Quad(o.root, gold ? 6f : 3f, new Color(c.r, c.g, c.b, gold ? 0.22f : 0.16f));
        if (gold)
        {
            o.light = new GameObject("Golden orb light").AddComponent<Light>();
            o.light.transform.SetParent(o.root, false);
            o.light.type = LightType.Point; o.light.color = new Color(1f, 0.82f, 0.4f); o.light.range = 7f; o.light.intensity = 1.6f;
            o.light.shadows = LightShadows.None; o.light.renderMode = LightRenderMode.Auto;
        }
        orbs.Add(o);
    }

    Transform Quad(Transform parent, float size, Color c)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(parent, false);
        q.transform.localScale = Vector3.one * size;
        var mr = q.GetComponent<MeshRenderer>();
        var m = new Material(haloMat); m.color = c;
        mr.sharedMaterial = m;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        return q.transform;
    }

    public int Remaining { get { int n = 0; foreach (var o in orbs) if (!o.taken) n++; return n; } }
    public int Score { get { int s = 0; for (int i = 0; i < counts.Length; i++) s += counts[i] * (i == Gold ? 10 : 1); return s; } }

    static double Now { get { return (System.DateTime.UtcNow - new System.DateTime(1970, 1, 1)).TotalSeconds; } }

    void Load()
    {
        try
        {
            string n = PlayerPrefs.GetString("ff.orbs.n", "");
            if (n.Length > 0) { var p = n.Split(','); for (int i = 0; i < counts.Length && i < p.Length; i++) int.TryParse(p[i], out counts[i]); }
            string t = PlayerPrefs.GetString("ff.orbs.t", "");
            double now = Now;
            foreach (string e in t.Split(';'))
            {
                var kv = e.Split(':'); int i; double back;
                if (kv.Length != 2 || !int.TryParse(kv[0], out i) || !double.TryParse(kv[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out back)) continue;
                if (i < 0 || i >= orbs.Count || back <= now) continue;
                orbs[i].taken = true; orbs[i].back = back; orbs[i].root.gameObject.SetActive(false);
            }
        }
        catch (System.Exception ex) { Debug.Log("Orbs: load failed " + ex.Message); }
        if (Ffu33.Url("orbreset") != null) { for (int i = 0; i < counts.Length; i++) counts[i] = 0; foreach (var o in orbs) { o.taken = false; o.root.gameObject.SetActive(true); } Save(); }
    }

    void Save()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < orbs.Count; i++) if (orbs[i].taken) sb.Append(i).Append(':').Append(orbs[i].back.ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Append(';');
        PlayerPrefs.SetString("ff.orbs.t", sb.ToString());
        PlayerPrefs.SetString("ff.orbs.n", string.Join(",", System.Array.ConvertAll(counts, x => x.ToString())));
        PlayerPrefs.Save();
    }

    void Update()
    {
        if (Game.I == null) return;
        float t = Time.time, dt = Time.deltaTime;
        double now = Now;
        bool any = false; Vector3 near = Vector3.zero; float nd = 1e9f;
        foreach (Frog f in Game.I.frogs) if (f != null && f.human && f.world == WorldId.Ranch) { any = true; Vector3 p = f.Center; Vector2 fc = JetLayout.ForestC; float d = (new Vector2(p.x - fc.x, p.z - fc.y)).sqrMagnitude; if (d < nd) { nd = d; near = p; } }
        for (int i = 0; i < orbs.Count; i++)
        {
            Orb o = orbs[i];
            if (o.taken)
            {
                if (now >= o.back) { o.taken = false; o.root.gameObject.SetActive(true); FX.Sparkle(o.basePos, Colours[o.colour], 10); Save(); }
                continue;
            }
            if (!any) continue;
            // animate only near a player
            float camD = (near - o.basePos).sqrMagnitude;
            if (camD < 90f * 90f)
            {
                float b = Mathf.Sin(t * 1.6f + o.phase) * 0.16f;
                Vector3 drift = new Vector3(Mathf.Sin(t * 0.5f + o.phase) * 0.18f, b, Mathf.Cos(t * 0.43f + o.phase) * 0.18f);
                o.root.position = o.basePos + drift;
                float pulse = 1f + Mathf.Sin(t * 2.4f + o.phase) * 0.12f;
                o.halo2.localScale = Vector3.one * (o.IsGold ? 6f : 3f) * pulse;
                o.core.localScale = Vector3.one * (o.IsGold ? 0.88f : 0.44f) * (1f + Mathf.Sin(t * 3.1f + o.phase) * 0.05f);
                if (o.light != null) o.light.intensity = 1.4f + 0.4f * Mathf.Sin(t * 2.4f + o.phase);
            }
            // collect: any human froggy (or its vehicle) touching it
            foreach (Frog f in Game.I.frogs)
            {
                if (f == null || !f.human || f.world != WorldId.Ranch || f.launching) continue;
                Vector3 p = f.vehicle != null ? f.vehicle.transform.position : f.passengerOf != null ? Vector3.one * 1e6f : f.Center;
                float rr = (o.IsGold ? 1.5f : 1.15f) + (f.vehicle != null ? 1.6f : 0f);
                if ((p - o.root.position).sqrMagnitude < rr * rr) { Collect(i, f); break; }
            }
        }
        // per-player HUD row
        foreach (Slot s in Game.I.slots)
        {
            if (s == null || s.hud == null) continue;
            OrbRow row;
            if (!rows.TryGetValue(s, out row)) { row = new OrbRow(s.hud.panel); rows[s] = row; }
            Frog f = s.frog >= 0 && s.frog < Game.I.frogs.Count ? Game.I.frogs[s.frog] : null;
            bool show = f != null && f.world == WorldId.Ranch && Game.I.state == Game.State.Play && (JetLayout.InForest(f.transform.position.x, f.transform.position.z, 22f) || Time.time - lastT < 8f);
            row.Tick(show, this, dt);
        }
    }

    public void Collect(int i, Frog f)
    {
        Orb o = orbs[i];
        if (o.taken) return;
        o.taken = true;
        o.back = Now + (o.IsGold ? RespawnGold : Respawn);
        o.root.gameObject.SetActive(false);
        counts[o.colour]++;
        Collected++;
        lastColour = o.colour; lastT = Time.time;
        Vector3 p = o.root.position;
        Color c = Colours[o.colour];
        if (o.IsGold)
        {
            Sfx.Play(Synth33.Fanfare, 0.9f);
            Sfx.Play(Synth33.Chime(7), 0.6f, 0.5f);
            FX.Sparkle(p, c, 60);
            FX.Sparkle(p, Color.white, 30);
            FX.Ring(p - Vector3.up * 0.8f, 2.5f, 24, 6f, new Color(1f, 0.85f, 0.35f, 0.7f));
            f.Toast("GOLDEN ORB! +10  (score " + Score + ")", 4f);
        }
        else
        {
            Sfx.Play(Synth33.Chime(o.colour), 0.75f, Random.Range(0.99f, 1.01f));
            FX.Sparkle(p, c, 28);
            FX.Sparkle(p, Color.Lerp(c, Color.white, 0.6f), 10);
            f.Toast(Cap(ColourNames[o.colour]) + " orb!  " + counts[o.colour] + " " + ColourNames[o.colour] + "  |  " + Remaining + " still glowing in the forest", 2.4f);
        }
        Debug.Log("FFDEMO orb collected " + ColourNames[o.colour] + " by " + f.nick + " total " + Collected + " score " + Score);
        Save();
    }
    static string Cap(string s) { return s.Length > 0 ? char.ToUpper(s[0]) + s.Substring(1) : s; }

    // halos face each camera (split-screen safe: done per camera just before it renders)
    void Billboard(Camera cam)
    {
        if (cam == null || cam.cullingMask == (1 << 20)) return;
        Vector3 cp = cam.transform.position;
        if (!JetLayout.InForest(cp.x, cp.z, 120f)) return;
        Quaternion q = cam.transform.rotation;
        foreach (Orb o in orbs)
        {
            if (o.taken) continue;
            o.halo.rotation = q; o.halo2.rotation = q * Quaternion.Euler(0f, 0f, Time.time * 8f + o.phase * 30f);
        }
    }

    void OnDestroy() { Camera.onPreCull -= Billboard; }

    // a row of coloured dots + counts under the player's name (top-left of their view)
    class OrbRow
    {
        readonly RectTransform root;
        readonly Text[] num = new Text[8];
        readonly Image[] dot = new Image[8];
        readonly Text head;
        float vis;
        readonly CanvasGroup cg;
        public OrbRow(RectTransform panel)
        {
            root = UIK.Rect(panel, "Orb row", new Vector2(0f, 1f), new Vector2(200f, -66f), new Vector2(380f, 30f));
            cg = root.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false;
            var bg = UIK.Img(root, UIK.SoftRect, new Color(0f, 0f, 0f, 0.35f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(390f, 34f));
            head = UIK.Label(root, "ORBS", 16, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), new Vector2(-160f, 0f), new Vector2(60f, 30f), new Color(0.85f, 1f, 0.85f));
            for (int i = 0; i < 8; i++)
            {
                float x = -122f + i * 38f + (i == Gold ? 6f : 0f);
                float s = i == Gold ? 20f : 14f;
                dot[i] = UIK.Img(root, UIK.Circle, Colours[i], new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(s, s));
                if (i == Gold) UIK.Img(root, UIK.Ring, new Color(1f, 0.95f, 0.7f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(s + 6f, s + 6f));
                num[i] = UIK.Label(root, "0", 16, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), new Vector2(x + 23f, 0f), new Vector2(30f, 30f), Color.white);
            }
        }
        public void Tick(bool show, Orbs o, float dt)
        {
            vis = Mathf.MoveTowards(vis, show ? 1f : 0f, dt * 3f);
            cg.alpha = vis;
            if (vis <= 0.001f) { if (root.gameObject.activeSelf) root.gameObject.SetActive(false); return; }
            if (!root.gameObject.activeSelf) root.gameObject.SetActive(true);
            for (int i = 0; i < 8; i++)
            {
                num[i].text = o.counts[i].ToString();
                float pop = i == o.lastColour ? Mathf.Clamp01(1f - (Time.time - o.lastT) / 0.6f) : 0f;
                float s = (i == Gold ? 20f : 14f) * (1f + pop * 0.8f);
                dot[i].rectTransform.sizeDelta = new Vector2(s, s);
            }
        }
    }
}
