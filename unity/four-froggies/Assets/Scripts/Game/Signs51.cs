using System.Collections.Generic;
using UnityEngine;

// ffu51 PLAIN-ENGLISH SIGNS (Bill: "weird words" on the signs - the launch area, the jump, other placards).
// Root cause: every world sign is a world-space uGUI canvas rasterising Montserrat at fontSize x dynamicPixelsPerUnit 6
// (up to ~324 px glyphs, more during best-fit), so the shared dynamic font atlas overflowed its 4096 max ("Failed to update
// dynamic font (FFDisplay) texture" ~1100 times per run) and glyphs were drawn from the wrong atlas cells = scrambled
// letters. Fix: sign glyphs are rasterised at ~88 px (Dppu), world TextMesh glyphs capped at 160 px (TmCap), and the
// few cryptic / dev-style labels get plain kid-friendly wording (Plain).
//  ?ffsigns=old  : the pre-ffu51 behaviour (px 6, cap 256, old wording) for before / after shots
//  ?ffdemo=1&ffshot=signs[&ffsignf=LOOP|GARAGE] : tour - the camera visits every ranch sign / label in turn (3.5 s each),
//      logs "FFDEMO sign i/N 'text' at (x,y,z)" + "FFDEMO signs done", and the font-atlas health.
[DefaultExecutionOrder(200)]
public class Signs51 : MonoBehaviour
{
    static int old = -1;
    public static bool Old { get { if (old < 0) old = Ffu33.Url("ffsigns") == "old" ? 1 : 0; return old == 1; } }
    public const float GlyphPx = 88f;
    public static float Dppu(int fontSize) { return Old ? 6f : Mathf.Clamp(GlyphPx / Mathf.Max(1, fontSize), 1f, 4f); }
    public static int TmCap { get { return Old ? 256 : 160; } }

    static readonly Dictionary<string, string> map = new Dictionary<string, string>
    {
        { "STARSHIP\n<size=28>board it to launch!</size>", "ROCKET LAUNCH PAD\n<size=28>climb into the Starship to fly to space!</size>" },
        { "C-RAMP\n<size=26>Roadster: fans ON</size>", "WALL RIDE RAMP\n<size=26>drive the Roadster up and over!</size>" },
        { "JAMES  J-1", "JAMES'S JET" },
        { "VTOL", "JET PAD" },
        { "09", "RUNWAY" }, { "27", "RUNWAY" },
        { "MECH YARD\n<size=22>every froggy: 10 + 100-story mech\nJames & Bubbles: 1000-story\nJames: trillion-story</size>", "MECH YARD\n<size=22>giant robot suits -\nwalk up and climb in!</size>" },
        { "MONSTER PARK\n<size=26>crusher - step-ups - gap - tabletop - whoops - rocks - teeter - WALL OF DEATH - half-pipe - bridge - hill</size>", "MONSTER TRUCK PARK\n<size=26>jumps, ramps, a half-pipe and the WALL OF DEATH!</size>" },
        { "STEP-UPS + DROP", "STEP-UP JUMPS" },
        { "TABLETOP", "TABLETOP JUMP" },
        { "WHOOPS", "BUMPY HILLS" },
        { "ROCK GARDEN\n<size=20>hard - tow truck on call</size>", "ROCK CRAWL\n<size=20>big bumpy rocks - hard!</size>" },
        { "TEETER-TOTTER\n<size=20>hard</size>", "SEESAW\n<size=20>tip it over - hard!</size>" },
        { "NARROW BRIDGE\n<size=20>hard</size>", "NARROW BRIDGE\n<size=20>drive slowly - hard!</size>" },
        { "HILL CLIMB\n<size=20>hard - 30 deg</size>", "HILL CLIMB\n<size=20>very steep - hard!</size>" },
        { "Yard dog run", "DOG RUN" },
        { "Backyard animal yard", "PET YARD" },
        { "Lizard terrace", "LIZARD PEN" },
        { "ROBOT MISSION PAD\n<size=24>phone > MISSION tab</size>", "ROBOT MISSION PAD\n<size=24>open the phone, then MISSION</size>" },
        { "LOOP  >\n<size=26>this way</size>", "LOOP THIS WAY  >" },
        { "<  LOOP\n<size=26>this way too</size>", "<  LOOP THIS WAY" },
        { "RALLY TRACK\n<size=24>figure 8 - bridge - jumps</size>", "RALLY TRACK\n<size=24>figure 8, bridge and jumps</size>" },
        { "FROGGY RC RACEWAY\n<size=22>walk up to the stand: A / E / tap = take a remote</size>", "FROGGY RC RACEWAY\n<size=22>walk up to the stand to grab a remote</size>" },
    };
    public static readonly List<string> Changed = new List<string>();
    public static string Plain(string s)
    {
        if (s == null || Old) return s;
        string o;
        if (map.TryGetValue(s, out o)) { if (!Changed.Contains(s)) { Changed.Add(s); Debug.Log("Signs51: '" + s.Replace("\n", " / ") + "' -> '" + o.Replace("\n", " / ") + "'"); } return o; }
        return s;
    }

    // ---------------- tour demo
    static Signs51 I;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { if (I != null) return; I = new GameObject("signs51").AddComponent<Signs51>(); DontDestroyOnLoad(I.gameObject); }

    string shot = ""; bool init; float t0 = -1f, logT; int cur = -1;
    readonly List<Transform> stops = new List<Transform>(); readonly List<string> texts = new List<string>(); readonly List<float> widths = new List<float>();
    static int rebuilds;

    void Update()
    {
        if (!init)
        {
            if (Bootstrap.I == null || Game.I == null || Time.timeSinceLevelLoad < 0.5f) return;
            init = true;
            shot = Ffu33.Url("ffdemo") != null ? (Ffu33.Url("ffshot") ?? "") : "";
            if (shot != "signs") shot = "";
            Font.textureRebuilt += f => { rebuilds++; };
            Debug.Log("Signs51: glyph px " + (Old ? "6x (old)" : GlyphPx.ToString("0")) + ", TextMesh cap " + TmCap + (shot.Length > 0 ? " - FFDEMO signs tour" : ""));
        }
        if (shot.Length == 0 || Game.I.state != Game.State.Play || Game.I.slots.Count == 0) return;
        if (t0 < 0f)
        {
            if (Time.timeSinceLevelLoad < 1f) return;
            t0 = Time.time;
            string flt = Ffu33.Url("ffsignf") ?? "";
            string[] keys = flt.Length > 0 ? flt.Split('|') : new string[0];
            foreach (var go in Object.FindObjectsOfType<Canvas>())
            {
                if (go.renderMode != RenderMode.WorldSpace || go.name != "Sign") continue;
                Vector3 p = go.transform.position;
                if (Mathf.Abs(p.x) > 700f || Mathf.Abs(p.z) > 700f) continue;           // ranch only (other worlds sit far away)
                var tx = go.GetComponentInChildren<UnityEngine.UI.Text>();
                string s = tx != null ? tx.text : "?";
                if (!Match(s, keys)) continue;
                stops.Add(go.transform); texts.Add(s); widths.Add(((RectTransform)go.transform).sizeDelta.x * go.transform.lossyScale.x);
            }
            foreach (var tm in Object.FindObjectsOfType<TextMesh>())
            {
                if (!tm.gameObject.name.StartsWith("Label ")) continue;
                Vector3 p = tm.transform.position;
                if (Mathf.Abs(p.x) > 700f || Mathf.Abs(p.z) > 700f || !Match(tm.text, keys)) continue;
                stops.Add(tm.transform); texts.Add(tm.text); widths.Add(Mathf.Max(3f, tm.text.Length * tm.characterSize * tm.fontSize * 0.08f));
            }
            Debug.Log("FFDEMO signs tour: " + stops.Count + " stops");
        }
        float t = Time.time - t0;
        int i = Mathf.FloorToInt(t / 3.5f);
        if (i != cur)
        {
            cur = i;
            if (i < stops.Count) Debug.Log("FFDEMO sign " + (i + 1) + "/" + stops.Count + " '" + texts[i].Replace("\n", " / ") + "' at " + stops[i].position.ToString("0.0") + " t=" + t.ToString("0.0") + " atlas rebuilds " + rebuilds);
            else if (i == stops.Count) Debug.Log("FFDEMO signs done, atlas rebuilds " + rebuilds);
        }
    }
    static bool Match(string s, string[] keys)
    {
        if (keys.Length == 0) return true;
        foreach (var k in keys) if (s.ToUpperInvariant().Contains(k.ToUpperInvariant())) return true;
        return false;
    }

    void LateUpdate()
    {
        if (shot.Length == 0 || t0 < 0f || cur < 0 || cur >= stops.Count || Game.I == null || Game.I.slots.Count == 0) return;
        Camera c = Game.I.slots[0].cam; if (c == null) return;
        Transform s = stops[cur];
        float d = Mathf.Clamp(widths[cur] * 0.95f, 2.6f, 16f);
        Vector3 f = s.forward; f.y = 0f; if (f.sqrMagnitude < 1e-4f) f = Vector3.forward; f.Normalize();
        if (s.forward.y < -0.7f) f = s.up;   // ground labels (runway / pad) lie flat: look down from above
        Vector3 p = s.position - f * d + Vector3.up * (s.forward.y < -0.7f ? d * 0.8f : 0.4f);
        c.transform.position = p; c.transform.LookAt(s.position);
    }
}
