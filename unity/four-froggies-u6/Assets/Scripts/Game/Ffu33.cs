using UnityEngine;

// ffu33 hook-up + demo shots, kept out of the shared Bootstrap / Game files (other releases edit those in parallel):
//  * a host object made after the scene loads builds the Enchanted Forest, its orbs and James's jet base once the ranch
//    and Game exist (Bootstrap.Awake has run by then);
//  * demo shots for the probe (?ffdemo=1&ffshot=...): the normal demo code doesn't know these names, so it leaves P1 on
//    the ranch with the follow camera; this script (runs after Game's LateUpdate) scripts P1 and pins the camera:
//      forestorbs  James walks into the forest collecting orbs (follow cam / canopy wide shot / fairy-ring golden orb)
//      jetflight   James in the jet on the VTOL pad: vertical lift-off, transition, climb + afterburner (Mach 1 boom),
//                  HYPER around the earth
//    &orbreset=1 clears the saved orb progress first; &jetat=hyper skips straight to the high-speed part.
[DefaultExecutionOrder(100)]
public class Ffu33 : MonoBehaviour
{
    public static Ffu33 I;
    bool built;
    string shot = "";
    float t0 = -1f, logT;
    int phase;
    Vector3 camPos, camLook, camFw; bool camInit;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        I = new GameObject("ffu33").AddComponent<Ffu33>();
        DontDestroyOnLoad(I.gameObject);
    }

    public static string Url(string key)
    {
        string u = FFPlatform.PageUrl;
        int q = u.IndexOf('?'); if (q < 0) return null;
        foreach (string kv in u.Substring(q + 1).Split('&', '#'))
        {
            int e = kv.IndexOf('=');
            string k = e >= 0 ? kv.Substring(0, e) : kv;
            if (k == key) return e >= 0 ? kv.Substring(e + 1) : "";
        }
        return null;
    }

    void Update()
    {
        if (!built)
        {
            if (Bootstrap.I == null || Game.I == null) return;
            built = true;
            try { Forest.Build(); } catch (System.Exception e) { Debug.LogError("ffu33 forest: " + e); }
            try { ForestOuter.Build(); } catch (System.Exception e) { Debug.LogError("ffw forest outer: " + e); }
            try { Forest56.Build(); } catch (System.Exception e) { Debug.LogError("ffu56 tree fort / bugs: " + e); }   // ffu56
            try { Orbs.Create(); } catch (System.Exception e) { Debug.LogError("ffu33 orbs: " + e); }
            try { JetBase.Create(); } catch (System.Exception e) { Debug.LogError("ffu33 jet base: " + e); }
            shot = Url("ffdemo") != null ? (Url("ffshot") ?? "") : "";
            if (shot != "forestorbs" && shot != "jetflight") shot = "";
            if (shot.Length > 0) Debug.Log("FFDEMO ffu33 demo " + shot);
        }
        if (shot.Length == 0 || Game.I.state != Game.State.Play || Game.I.slots.Count == 0) return;
        Frog f = Game.I.frogs[Game.I.slots[0].frog];
        if (t0 < 0f) { t0 = Time.time; Begin(f); }
        float t = Time.time - t0;
        if (shot == "forestorbs") OrbsTick(f, t);
        else JetTick(f, t);
    }

    void LateUpdate()
    {
        if (shot.Length == 0 || t0 < 0f || Game.I == null || Game.I.slots.Count == 0) return;
        Camera c = Game.I.slots[0].cam;
        if (c == null || !camInit) return;
        c.transform.position = camPos;
        c.transform.LookAt(camLook);
    }

    void Cam(Vector3 p, Vector3 look, float ease)
    {
        if (!camInit || ease <= 0f) { camPos = p; camLook = look; camInit = true; return; }
        float k = Mathf.Min(1f, Time.deltaTime * ease);
        camPos = Vector3.Lerp(camPos, p, k); camLook = Vector3.Lerp(camLook, look, k);
    }

    void Begin(Frog f)
    {
        Game.I.Demo33Chars();
        if (f.vehicle != null) f.ExitVehicle();
        Vector2 mn = JetLayout.ForestMin;
        if (shot == "forestorbs")
        {
            // 8 m in from the south-west corner, so the follow cam starts clear of the entrance sign (mn + (3, -1.5))
            Vector3 p = new Vector3(mn.x + 8f, 0f, mn.y + 8f);
            f.DemoPose(new Vector3(p.x, Ranch.GY(p.x, p.z) + 0.2f, p.z), 30f);
            Game.I.Demo33Hook(OrbWalk);
        }
        else
        {
            Jet j = Jet.I;
            if (j == null) return;
            Vector2 pc = JetLayout.PadC;
            j.homePos = new Vector3(pc.x, Ranch.GY(pc.x, pc.y) + 0.1f, pc.y);
            Vector3 hp = j.homePos;
            j.homeYaw = 0f;   // demo: face north, out over the pond
            j.ResetToPad();
            j.homePos = hp;
            f.DemoPose(hp + new Vector3(-3f, 0.3f, -4f), 0f);
            f.EnterVehicle(j);
            j.autopilot = JetAuto;
            if (Url("jetat") == "hyper")
            {
                j.DemoAir(hp + Vector3.up * 620f, 330f);
            }
        }
        Debug.Log("FFDEMO ffu33 begin " + shot + " t=" + Time.realtimeSinceStartup.ToString("0.0"));
    }

    // ---------------- forestorbs ----------------
    Vector3 orbGoal; bool goldLeg;
    PIn OrbWalk(PIn i)
    {
        var o = new PIn(); o.look = i.look;
        Frog f = Game.I.frogs[Game.I.slots[0].frog];
        if (f == null || Orbs.I == null) return o;
        Vector3 p = f.transform.position;
        // nearest glowing orb (golden leg: the fairy-ring one)
        Orbs.Orb best = null; float bd = 1e9f;
        foreach (var ob in Orbs.I.orbs)
        {
            if (ob.taken) continue;
            if (goldLeg != ob.IsGold) continue;
            float d = (new Vector2(ob.basePos.x - p.x, ob.basePos.z - p.z)).sqrMagnitude;
            if (d < bd) { bd = d; best = ob; }
        }
        if (best == null) return o;
        orbGoal = best.basePos;
        Vector3 to = orbGoal - p; to.y = 0f;
        Vector3 m = Quaternion.Euler(0f, -f.CamYaw, 0f) * to.normalized;
        o.move = new Vector2(m.x, m.z) * 0.9f;
        if (to.magnitude < 1.6f && orbGoal.y - p.y > 1.4f) o.hop = Random.value < 0.08f;
        return o;
    }

    void OrbsTick(Frog f, float t)
    {
        Vector2 mn = JetLayout.ForestMin, mx = JetLayout.ForestMax;
        int ph = t < 18f ? 0 : t < 25f ? 1 : 2;   // short phases: SwiftShader runs the demo at ~0.12 s per real s
        if (ph != phase)
        {
            phase = ph; camInit = false;
            Debug.Log("FFDEMO forestorbs phase " + ph + " collected " + Orbs.Collected + " t=" + Time.realtimeSinceStartup.ToString("0.0"));
            if (ph == 2)
            {
                // the golden orb in the fairy ring: start 7 m away and walk in
                goldLeg = true;
                Vector3 fr = Forest.FairyRing;
                Vector3 s = fr + new Vector3(-7f, 0f, -3f);
                f.DemoPose(new Vector3(s.x, Ranch.GY(s.x, s.z) + 0.2f, s.z), 60f);
            }
        }
        Vector3 fp = f.transform.position;
        if (ph == 0)
        {
            // low chase from behind-right, looking where the froggy walks
            Vector3 fw = orbGoal - fp; fw.y = 0f; if (fw.sqrMagnitude < 0.01f) fw = f.transform.forward; fw.Normalize();
            Vector3 rt = new Vector3(fw.z, 0f, -fw.x);
            Cam(fp - fw * 4.6f + rt * 1.6f + Vector3.up * 1.9f, fp + fw * 4f + Vector3.up * 0.9f, 3f);
        }
        else if (ph == 1)
        {
            // wide low shot across the forest floor towards a light shaft, canopy above
            Vector3 g = Forest.Gaps.Count > 0 ? Forest.Gaps[0] : new Vector3((mn.x + mx.x) * 0.5f, 0f, (mn.y + mx.y) * 0.5f);
            Vector3 from = new Vector3(g.x - 11f, 0f, g.z - 9f);
            from.y = Ranch.GY(from.x, from.z) + 1.5f;
            Cam(from, new Vector3(g.x, Ranch.GY(g.x, g.z) + 4.5f, g.z), 0f);
        }
        else
        {
            Vector3 fr = Forest.FairyRing;
            Vector3 c = fr + new Vector3(-6.5f, 0f, 3.5f); c.y = Ranch.GY(c.x, c.z) + 2.1f;
            Cam(c, fr + Vector3.up * 1.1f, 0f);
        }
        logT -= Time.deltaTime;
        if (logT <= 0f) { logT = 2f; Debug.Log("FFDEMO forestorbs t=" + t.ToString("0") + " pos " + fp.ToString("0.0") + " under canopy " + Forest.UnderCanopy(fp) + " collected " + Orbs.Collected + " remaining " + Orbs.I.Remaining); }
    }

    // ---------------- jetflight ----------------
    Jet.JetIn JetAuto(Jet j)
    {
        var o = new Jet.JetIn();
        float t = Time.time - t0;
        bool skip = Url("jetat") == "hyper";
        if (!skip && t < 9f) { o.climb = j.agl < 22f ? 1f : 0f; return o; }               // vertical lift-off from the pad
        if (j.vtol || j.nz > 0.5f) { o.fwd = 1f; o.climb = j.agl < 28f ? 0.5f : 0f; return o; }   // push forward -> transition
        // jet: accelerate level, then climb at ~18 deg to 600 m on the afterburner (Mach 1 on the way), then HYPER
        float s = j.vel.magnitude;
        float pitch = -Mathf.DeltaAngle(0f, j.transform.eulerAngles.x);
        float wantPitch = s < 95f ? 4f : j.transform.position.y < 600f ? 18f : 1f;
        o.pitch = Mathf.Clamp((wantPitch - pitch) / 12f, -1f, 1f);
        o.roll = Mathf.Clamp(Mathf.DeltaAngle(0f, j.transform.eulerAngles.z) / 30f, -1f, 1f);
        o.thr = 1f;
        o.burn = true;
        if (j.mode != Jet.Mode.Hyper && j.transform.position.y > 550f && s > 300f && (skip || t > 30f)) o.hyper = true;
        return o;
    }

    void JetTick(Frog f, float t)
    {
        Jet j = Jet.I;
        if (j == null) return;
        Vector3 jp = j.transform.position;
        Vector3 fw0 = j.vel.sqrMagnitude > 4f ? j.vel : j.transform.forward; fw0.y = 0f; if (fw0.sqrMagnitude < 0.01f) fw0 = Vector3.forward; fw0.Normalize();
        // the heading is eased (not the position) so the camera keeps up with a 1200 m/s jet
        camFw = camFw == Vector3.zero ? fw0 : Vector3.Slerp(camFw, fw0, Mathf.Min(1f, Time.deltaTime * 2.5f));
        Vector3 fw = camFw; fw.y = 0f; fw.Normalize();
        Vector3 rt = new Vector3(fw.z, 0f, -fw.x);
        bool skip = Url("jetat") == "hyper";
        int ph = skip ? 3 : t < 9f ? 0 : t < 17f ? 1 : j.mode == Jet.Mode.Hyper ? 3 : 2;
        if (ph != phase) { phase = ph; camInit = false; Debug.Log("FFDEMO jetflight phase " + ph + " t=" + Time.realtimeSinceStartup.ToString("0.0")); }
        if (ph == 0)
        {
            // side view of the pad: the jet lifting straight up, dust ring, hangar behind
            Vector2 pc = JetLayout.PadC;
            Vector3 c = new Vector3(pc.x - 13f, 0f, pc.y - 19f); c.y = Ranch.GY(c.x, c.z) + 3.2f;
            Cam(c, jp + Vector3.up * 2.5f, 0f);
        }
        else if (ph == 1)
        {
            // 3/4 rear-side: nozzles swinging aft during the transition
            Cam(jp - fw * 14f + rt * 11f + Vector3.up * 3.5f, jp + fw * 2f + Vector3.up * 2f, 0f);
        }
        else if (ph == 2)
        {
            Cam(jp - fw * 26f + Vector3.up * 7f + rt * 6f, jp + fw * 25f, 0f);
        }
        else
        {
            // high chase: horizon + the earth curving away below
            Cam(jp - fw * 34f + Vector3.up * 9f, jp + fw * 220f - Vector3.up * 70f, 0f);
        }
        logT -= Time.deltaTime;
        if (logT <= 0f)
        {
            logT = 2f;
            Debug.Log("FFDEMO jetflight t=" + t.ToString("0") + " mode " + j.mode + " nz " + j.nz.ToString("0.00") + " v " + j.vel.magnitude.ToString("0") + " mach " + j.Mach.ToString("0.00") + " alt " + jp.y.ToString("0") + " agl " + j.agl.ToString("0") + " pos " + jp.ToString("0") + " lap " + j.laps + " " + (j.LapFrac * 100f).ToString("0") + "%");
        }
    }
}

public partial class Game
{
    // ffu33 demo access (Ffu33.cs): P1's input hook + keep James in seat 1
    public void Demo33Hook(System.Func<PIn, PIn> h) { demoHook = h; }
    public void Demo33Chars()
    {
        if (slots.Count == 0) return;
        Frog f = frogs[slots[0].frog];
        if (f.charId != 0) SetSeatChar(f.id, 0, false);
        demoKeepChars = true;
    }
}
