using System.Collections.Generic;
using UnityEngine;

// Starship blast-off from the ranch pad (Ben, Oct 2026). A at the pad starts it:
//   ~3 s countdown on the HUD -> engines light (emissive flame plume, pad smoke / steam, camera shake, rumble)
//   -> liftoff: the stack rises slowly, then accelerates and climbs on its own for ~5 s while a low-angle chase
//   camera watches it go and the sky darkens -> fade to black -> SpaceWorld.Launch for everyone (the existing
//   flow: first froggy flies, the rest ride along) -> fade back in, in Earth orbit.
// Every human froggy on the ranch (plus any AI froggy at the pad) goes together; anyone can still join with
// A at the pad during the countdown. A / FIRE (or hop) skips straight to the fade.
// Light on purpose (Tesla browser / WebGL): two unlit plume meshes, a few shared-pool particles a frame,
// one procedural looping rumble clip, a HUD fade image; no new cameras or post effects.
public class LaunchSeq : MonoBehaviour
{
    public static LaunchSeq I;
    const float CountT = 3f, IgniteLead = 0.9f, ClimbT = 5f, FadeOutT = 0.6f, SkipFadeT = 0.35f, FadeInT = 0.8f;
    const float GatherR = 45f;

    public readonly List<Frog> crew = new List<Frog>();
    int phase;                 // 0 idle, 1 countdown + climb, 2 fading in (in space)
    float t, fadeIn, skipAt = -1f, ign, h, dark, fade;
    int lastCount = 99;
    AudioSource rumble;
    static readonly Dictionary<Camera, float> camDark = new Dictionary<Camera, float>();



    static void Ensure()
    {
        if (I != null) return;
        var go = new GameObject("LaunchSeq");
        I = go.AddComponent<LaunchSeq>();
        I.rumble = Sfx.Loop(go, MakeRumble());
    }

    // the pad hotspot calls this
    public static void Begin(Frog f)
    {
        if (SpaceWorld.I == null || f == null) return;
        if (Ranch.ShipStack == null) { SpaceWorld.I.Launch(f); return; }
        Ensure();
        I.Start1(f);
    }

    void Start1(Frog f)
    {
        if (phase == 1)
        {
            if (t < CountT) { Add(f); f.Toast("Aboard! Liftoff in " + Mathf.CeilToInt(CountT - t), 2f); }
            else f.Toast("Liftoff already underway - catch the next one", 2.5f);
            return;
        }
        if (phase == 2) { SpaceWorld.I.Launch(f); return; }   // previous crew still fading in: just ride up
        crew.Clear();
        Add(f);
        if (Game.I != null)
            foreach (Frog o in Game.I.frogs)
            {
                if (o == null || o == f || o.world != WorldId.Ranch || o.netPuppet) continue;   // ffu13: online froggies launch on their own device
                bool near = (o.transform.position - Ranch.ShipBase).sqrMagnitude < GatherR * GatherR;
                // ffu14 (Bill): split-screen players are independent (only AI froggies at the pad hop on); on a shared
                // screen whoever launches takes every player along
                if (o.human ? Game.I.SharedScreen : near) Add(o);
            }
        phase = 1; t = 0f; skipAt = -1f; ign = 0f; h = 0f; dark = 0f; fade = 0f; lastCount = 99;
        if (rumble != null) { rumble.volume = 0f; rumble.pitch = 0.8f; rumble.Play(); }
        foreach (Frog c in crew) c.Toast(Game.I != null && Game.I.SharedScreen && crew.Count > 1 ? f.nick + " is taking everyone to space!  A / FIRE skips" : "Starship countdown!  A / FIRE skips", 3f);
    }

    void Add(Frog f)
    {
        if (crew.Contains(f)) return;
        if (f.vehicle != null) f.ExitVehicle();
        f.LeavePassenger();
        f.launching = true;
        f.cc.enabled = false;
        f.model.gameObject.SetActive(false);
        f.transform.SetParent(Ranch.ShipStack, true);
        f.transform.position = Ranch.ShipStack.position + Ranch.ShipBase + Vector3.up * 48f;
        crew.Add(f);
    }

    public static void Skip(Frog f)
    {
        if (I == null || I.phase != 1 || I.skipAt >= 0f || I.t < 0.3f) return;
        I.skipAt = I.t;
    }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (phase == 2)
        {
            fadeIn -= dt;
            fade = Mathf.Clamp01(fadeIn / FadeInT);
            if (rumble != null) { rumble.volume = Mathf.MoveTowards(rumble.volume, 0f, dt * 0.8f); if (rumble.volume <= 0f) rumble.Stop(); }
            if (fadeIn <= 0f) { phase = 0; crew.Clear(); fade = 0f; }
            return;
        }
        if (phase != 1) return;
        t += dt;

        // countdown beeps
        int n = Mathf.CeilToInt(CountT - t);
        if (n != lastCount && n >= 1 && n <= 3) { Sfx.Play(Sfx.Click, 0.9f, 1.5f); }
        if (n != lastCount && n == 0) Sfx.Play(Sfx.Boom, 0.7f, 0.5f);
        lastCount = n;

        float tau = t - CountT;                                      // seconds since liftoff
        ign = Mathf.Clamp01((t - (CountT - IgniteLead)) / IgniteLead);
        h = tau > 0f ? 2f * tau * tau + 1.2f * tau * tau * tau : 0f;  // slow off the pad, then accelerating (~200 m at 5 s)
        Ranch.ShipStack.position = new Vector3(0f, h, 0f);
        dark = Mathf.Clamp01(tau / ClimbT);

        // plume: grows at ignition, flickers, stretches as it climbs
        if (Ranch.ShipFlames != null)
        {
            bool on = ign > 0f;
            if (Ranch.ShipFlames.gameObject.activeSelf != on) Ranch.ShipFlames.gameObject.SetActive(on);
            float fl = 1f + Mathf.Sin(t * 47f) * 0.08f + Random.Range(-0.06f, 0.06f);
            float len = (0.25f + 0.75f * ign) * (1f + Mathf.Clamp01(tau / 3f) * 0.8f) * fl;
            float wid = (0.6f + 0.4f * ign) * (1f + Random.Range(-0.04f, 0.04f));
            Ranch.ShipFlames.localScale = new Vector3(wid, len, wid);
        }

        // pad smoke / steam, a few particles a frame from the shared FX pool
        Vector3 b = Ranch.ShipBase;
        float smokeK = ign * Mathf.Clamp01(1f - Mathf.Max(0f, tau) / 3f);
        int puffs = Mathf.RoundToInt(smokeK * 3f);
        for (int i = 0; i < puffs; i++)
        {
            float a = Random.value * 6.2832f, r = Random.Range(5f, 15f);
            float g = Random.Range(0.62f, 0.95f);
            FX.Smoke(b + new Vector3(Mathf.Cos(a) * r, Random.Range(-4f, 0f), Mathf.Sin(a) * r), Random.Range(2.5f, 5f), new Color(g, g, g, 0.7f));
        }
        if (ign > 0f) for (int i = 0; i < 2; i++) FX.Flame(b + new Vector3(Random.Range(-2.5f, 2.5f), h - 1f, Random.Range(-2.5f, 2.5f)), Vector3.down);
        if (ign > 0f) Game.Shake(b, ign * dt * 1.2f * Mathf.Clamp01(1f - Mathf.Max(0f, tau) / 4f));   // nearby onlookers feel it too

        // rumble builds to liftoff, then fades as it climbs away
        if (rumble != null)
        {
            float build = Mathf.Clamp01((t - (CountT - IgniteLead - 0.4f)) / (IgniteLead + 0.4f));
            float away = tau > 0f ? Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(tau / ClimbT)) : 1f;
            rumble.volume = build * away;
            rumble.pitch = 0.75f + 0.25f * build + 0.1f * Mathf.Clamp01(tau / ClimbT);
        }

        // fade out at the end of the climb, or quickly after a skip
        if (skipAt >= 0f) fade = Mathf.Max(fade, Mathf.Clamp01((t - skipAt) / SkipFadeT));
        else fade = Mathf.Clamp01((tau - (ClimbT - FadeOutT)) / FadeOutT);
        if (fade >= 1f) Finish();
    }

    void Finish()
    {
        Ranch.ShipStack.position = Vector3.zero;
        if (Ranch.ShipFlames != null) Ranch.ShipFlames.gameObject.SetActive(false);
        var go = new List<Frog>(crew);
        foreach (Frog f in go)
        {
            f.transform.SetParent(null, true);
            f.transform.localScale = Vector3.one;
            f.model.gameObject.SetActive(true);
            f.cc.enabled = true;
            f.launching = false;
        }
        phase = 2; fadeIn = FadeInT; fade = 1f;
        foreach (Frog f in go) SpaceWorld.I.Launch(f);   // existing flow: first flies, the rest ride along
    }

    // ---------------- views (called from Game.LateUpdate after the normal cameras) ----------------
    public static void View(Camera cam, ViewHud hud, Frog f)
    {
        if (cam == null) return;
        bool mine = I != null && f != null && I.crew.Contains(f);
        camDark[cam] = 0f;
        if (!mine || I.phase == 0) { if (hud != null) hud.SetFade(0f); return; }
        if (hud != null) hud.SetFade(I.fade);
        if (I.phase != 1) return;
        camDark[cam] = I.dark;
        I.PoseCam(cam);
        if (hud != null)
        {
            float tau = I.t - CountT;
            string s = tau < 0f ? "<size=110>" + Mathf.CeilToInt(CountT - I.t) + "</size>\n<size=26>STARSHIP LAUNCH</size>"
                     : tau < 1.4f ? "<size=80>LIFTOFF!</size>" : "";
            hud.SetCenter(s + (I.skipAt < 0f ? "\n<size=20>A / FIRE skips</size>" : ""));
        }
    }

    void PoseCam(Camera cam)
    {
        Vector3 b = Ranch.ShipBase;
        Vector3 dir = new Vector3(0.94f, 0f, -0.34f);              // sign side of the pad, away from the tower
        float dist = 72f + h * 0.35f;
        float x = b.x + dir.x * dist, z = b.z + dir.z * dist;
        Vector3 pos = new Vector3(x, Ranch.GY(x, z) + 3f + h * 0.55f, z);
        Vector3 look = b + Vector3.up * (30f + h);
        float tau = t - CountT;
        float shake = ign * (tau < 1.5f ? 0.35f : 0.35f * Mathf.Clamp01(1f - (tau - 1.5f) / 3f));
        pos += Random.insideUnitSphere * shake;
        cam.transform.position = pos;
        cam.transform.rotation = Quaternion.LookRotation(look - pos + Random.insideUnitSphere * shake * 0.8f);
    }

    // Worlds.PreCull asks how dark the ranch sky should be for this camera (0 = normal)
    public static float SkyDark(Camera c) { float k; float a = Ascent.Get(c); return Mathf.Max(a, camDark.TryGetValue(c, out k) ? k : 0f); }

    // low rocket rumble: brown noise + a couple of sub tones, 1 s seamless loop
    static AudioClip MakeRumble()
    {
        int rate = Sfx.Rate, n = rate, x = rate / 8;
        var raw = new float[n + x];
        var rnd = new System.Random(7);
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < raw.Length; i++)
        {
            float tt = i / (float)rate;
            float w = (float)(rnd.NextDouble() * 2.0 - 1.0);
            lp += (w - lp) * 0.05f; lp2 += (lp - lp2) * 0.15f;
            float crackle = (rnd.NextDouble() < 0.004) ? (float)(rnd.NextDouble() - 0.5) * 0.6f : 0f;
            raw[i] = lp2 * 3.2f + Mathf.Sin(tt * 6.2832f * 31f) * 0.18f + Mathf.Sin(tt * 6.2832f * 47f) * 0.1f + crackle;
        }
        var d = new float[n];
        for (int i = 0; i < n; i++) d[i] = raw[i];
        for (int i = 0; i < x; i++) { float k = i / (float)x; d[i] = raw[i] * k + raw[n + i] * (1f - k); }
        for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(d[i] * 0.8f, -1f, 1f);
        var c = AudioClip.Create("rumble", n, 1, rate, false);
        c.SetData(d, 0);
        return c;
    }
}

// ffu14: per-camera "how far up towards space" (mech rocket climb) -> Worlds.PreCull darkens the ranch sky
public static class Ascent
{
    static readonly System.Collections.Generic.Dictionary<Camera, float> k = new System.Collections.Generic.Dictionary<Camera, float>();
    public static void Set(Camera c, float v) { if (c != null) k[c] = v; }
    public static float Get(Camera c) { float v; return c != null && k.TryGetValue(c, out v) ? v : 0f; }
}
