using System.Collections.Generic;
using UnityEngine;

// ffu11: amphibious Cybertruck. Drive into the pond and it transforms into a "Cyberboat" (~2 s): the wheels lift and
// fold flat into the wells, stainless shutters close the arches, a faceted V-hull drops out of the belly, two
// sponsons slide out and down, a water-jet swings out of the tail, a bow deflector + rear wing flip up, cyan light
// strips strobe, splash + spray + servo / whoosh sound. Boat mode: buoyancy on 4 points with little waves, jet thrust,
// planing lift (bow rises with speed), banking into turns, three foam wake trails, bow spray + rooster tail, jet-boat
// engine voice (kind 11). Reaching shallow water / a shore slope plays the same sequence backwards and the wheels take
// over again. The sequence is one progress value k (0 truck .. 1 boat) so it reverses smoothly from any point.
// Water is found by Water.Depth (the ranch pond today; other surfaces can register there).
public static class Water
{
    public struct Body { public System.Func<float, float, bool> inside; public float y; public System.Func<float, float, float> ground; }
    public static readonly List<Body> Bodies = new List<Body>();
    static bool init;
    static void Init()
    {
        if (init) return;
        init = true;
        // the ranch pond (islands rise out of it through the terrain)
        Bodies.Add(new Body { inside = (x, z) => Layout.InPond(x, z), y = Layout.WaterY, ground = (x, z) => Ranch.GY(x, z) });
    }
    // water surface height + depth to the ground at (x, z); false when there is no water there
    public static bool At(float x, float z, out float surface, out float depth)
    {
        Init();
        foreach (Body b in Bodies)
            if (b.inside(x, z)) { surface = b.y; depth = b.y - b.ground(x, z); return true; }
        surface = 0f; depth = 0f;
        return false;
    }
    // small rolling waves (the buoy gates bob with the same rhythm)
    public static float Wave(float x, float z, float t)
    {
        return Mathf.Sin(t * 1.55f + x * 0.31f) * 0.07f + Mathf.Sin(t * 1.12f + z * 0.43f + 1.3f) * 0.05f;
    }
}

public class CyberBoat : MonoBehaviour
{
    public const float EnterDepth = 0.8f, ExitDepth = 0.5f, Tin = 2.0f, Tout = 1.3f;
    public GroundVehicle v;
    public float k;                 // 0 truck .. 1 boat
    public bool boatTarget;
    public bool Boat { get { return k > 0.5f; } }
    public float WheelScale { get { return Mathf.Clamp01((0.4f - k) / 0.2f); } }
    public float Wet { get; private set; }
    public static int Transforms;   // demo / log counter
    bool built;
    float prevK, strobeT, airT, sprayT, toggleCool;
    Transform hull, sponL, sponR, jet, deflector, wing;
    readonly List<Transform> shutters = new List<Transform>();
    Material ledMat, glowMat;
    readonly List<Renderer> leds = new List<Renderer>();
    TrailRenderer[] trails;
    Transform[] trailPts;
    GameObject partsRoot;
    BoxCollider hullCol;
    static AudioClip whoosh;
    static Material foamMat;

    // float points (local): bow pair, stern pair
    static readonly Vector3[] Floats = { new Vector3(-1.05f, 0.08f, 2.1f), new Vector3(1.05f, 0.08f, 2.1f), new Vector3(-1.1f, 0.08f, -2.3f), new Vector3(1.1f, 0.08f, -2.3f) };
    public float maxBoatSpeed = 27f, boatAccel = 15f, boatTurn = 1.55f;

    public static CyberBoat Attach(GroundVehicle gv)
    {
        var c = gv.gameObject.AddComponent<CyberBoat>();
        c.v = gv;
        gv.amph = c;
        return c;
    }

    // ---------------- per physics step (called by GroundVehicle.FixedUpdate) ----------------
    public void Step(float dt, float steer, float throttle)
    {
        Rigidbody rb = v.rb;
        Vector3 pos = rb.position;
        float surf = 0f, depth = 0f;
        bool water = (v.driver == null || v.driver.world == WorldId.Ranch) && Water.At(pos.x, pos.z, out surf, out depth);
        // look ahead under the bow (in the direction of travel) so the wheels come down before the hull grounds
        Vector3 fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
        float fsp = Vector3.Dot(rb.velocity, fwd);
        Vector3 ahead = pos + fwd * Mathf.Sign(fsp + 0.01f) * (3.2f + Mathf.Abs(fsp) * 0.45f);
        float s2, depthAhead;
        bool waterAhead = Water.At(ahead.x, ahead.z, out s2, out depthAhead);
        if (!waterAhead) depthAhead = -1f;
        bool shoreAhead = fsp > 1f && depthAhead < 0.3f;
        toggleCool -= dt;
        if (toggleCool <= 0f)
        {
            if (!boatTarget && water && depth > EnterDepth && !shoreAhead) Begin(true);
            else if (boatTarget && (!water || depth < ExitDepth || (shoreAhead && depth < EnterDepth + 0.3f))) Begin(false);
        }

        float speedK = boatTarget ? 1f / Tin : 1f / Tout;
        k = Mathf.MoveTowards(k, boatTarget ? 1f : 0f, speedK * dt);
        if (k > 0f && !built) Build();
        if (k <= 0f) { Wet = 0f; if (hullCol != null) hullCol.enabled = false; return; }
        if (hullCol != null) hullCol.enabled = k > 0.5f;

        // buoyancy (fades in / out with the sequence), planing lift, banking
        float buoy = boatTarget ? Mathf.Clamp01(k / 0.06f) : Mathf.Clamp01(k / 0.3f);   // floats as soon as it starts
        float jetK = Mathf.Clamp01((k - 0.3f) / 0.4f);
        float plane = Mathf.Clamp01((Mathf.Abs(fsp) - 5f) / 11f) * jetK;
        float perPoint = rb.mass * 9.81f / Floats.Length;
        int wetN = 0;
        float t = Time.time;
        for (int i = 0; i < Floats.Length; i++)
        {
            Vector3 lp = Floats[i];
            Vector3 p = transform.TransformPoint(lp);
            float wy = surf + Water.Wave(p.x, p.z, t) * (1f - plane * 0.6f);
            float d = wy - p.y;
            if (!water || d <= 0f) continue;
            wetN++;
            float kd = Mathf.Min(d / 0.26f, 3f);   // rests with the waterline ~0.34 m up the hull
            // bow points lift more as the hull planes, stern less: the nose rises with speed;
            // the inside of a turn sinks a little so the hull banks into it
            float mul = (lp.z > 0f ? 1f + 0.55f * plane : 1f - 0.25f * plane);
            float side = Mathf.Sign(lp.x);
            mul *= 1f - 0.2f * steer * side * Mathf.Clamp01(Mathf.Abs(fsp) / 10f) * jetK;
            Vector3 pv = rb.GetPointVelocity(p);
            rb.AddForceAtPosition(Vector3.up * (perPoint * kd * mul - pv.y * rb.mass * 0.45f) * buoy, p);
        }
        Wet = wetN / (float)Floats.Length;
        if (Wet > 0f && plane > 0f) rb.AddForce(Vector3.up * rb.mass * 9.81f * 0.18f * plane * Wet * buoy);

        // splash-down after a jump (boat ramp)
        if (Wet <= 0f) airT += dt;
        else
        {
            if (airT > 0.35f && rb.velocity.y < -3f)
            {
                FX.Splash(pos + Vector3.up * 0.3f, 30);
                Sfx.PlayAt(Sfx.SplashBig != null ? Sfx.SplashBig : Sfx.Splash, pos, 0.9f, 70f, Random.Range(0.9f, 1.05f));
            }
            airT = 0f;
        }

        // jet thrust + hull drag (only while the hull is wet)
        if (Wet > 0f && jetK > 0f)
        {
            Vector3 right = transform.right; right.y = 0f; right.Normalize();
            Vector3 vel = rb.velocity;
            float fs = Vector3.Dot(vel, fwd), ls = Vector3.Dot(vel, right);
            bool driven = v.driver != null;
            float th = driven ? throttle : 0f;
            float push = Wet * jetK;
            if (th > 0.02f && fs < maxBoatSpeed) rb.AddForce(fwd * boatAccel * th * Mathf.Clamp01(1.12f - fs / maxBoatSpeed) * push * rb.mass);
            else if (th < -0.02f) rb.AddForce(fwd * (fs > 1f ? boatAccel * 0.9f : (fs > -8f ? boatAccel * 0.45f : 0f)) * th * push * rb.mass);
            rb.AddForce(-right * ls * 2.0f * rb.mass * Wet * jetK);
            rb.AddForce(-fwd * fs * (driven && th > 0.02f ? 0.1f : 0.3f) * rb.mass * Wet * jetK);
            float target = (driven ? steer : 0f) * boatTurn * Mathf.Clamp(0.35f + Mathf.Abs(fs) / 7f, 0f, 1f) * (fs < -0.5f ? -1f : 1f);
            float yr = Vector3.Dot(rb.angularVelocity, transform.up);
            rb.AddTorque(transform.up * (target - yr) * Mathf.Min(1f, 5f * dt) * jetK, ForceMode.VelocityChange);
            // keep roll / pitch from wandering off (buoyancy rights it, this damps the wobble)
            Vector3 av = rb.angularVelocity;
            Vector3 yawPart = transform.up * Vector3.Dot(av, transform.up);
            rb.AddTorque(-(av - yawPart) * Mathf.Min(1f, 2.5f * dt) * jetK, ForceMode.VelocityChange);
        }
        if (k >= 0.5f) { v.slipSpeed = 0f; v.groundFrac = 0f; }
    }

    void Begin(bool toBoat)
    {
        boatTarget = toBoat;
        toggleCool = 0.8f;
        Transforms++;
        Vector3 p = transform.position;
        Debug.Log("CyberBoat: " + v.Title + (toBoat ? " -> boat" : " -> truck") + " at (" + p.x.ToString("0") + ", " + p.z.ToString("0") + ") t=" + Time.realtimeSinceStartup.ToString("0.0"));
        bool heard = Heard();
        if (toBoat)
        {
            FX.Splash(p + Vector3.up * 0.4f, 30);
            FX.Splash(p + transform.forward * 2.5f + Vector3.up * 0.3f, 24);
            for (int i = 0; i < 26; i++)
            {
                Vector3 d = Random.onUnitSphere; d.y = Mathf.Abs(d.y) * 1.6f + 0.8f;
                FX.Spray(p + new Vector3(Random.Range(-1.2f, 1.2f), 0.3f, Random.Range(-2.6f, 2.6f)), d * Random.Range(3f, 7f), Random.Range(0.2f, 0.45f), Random.Range(0.6f, 1.2f), new Color(0.88f, 0.96f, 1f, 0.85f));
            }
            if (heard) Sfx.PlayAt(Sfx.SplashBig != null ? Sfx.SplashBig : Sfx.Splash, p, 0.9f, 80f, 0.95f);
            if (v.driver != null && v.driver.human) v.driver.Toast("CYBERBOAT MODE!", 2f);
        }
        else if (v.driver != null && v.driver.human) v.driver.Toast("Shore ahead - back to truck mode", 1.6f);
        if (heard) Sfx.PlayAt(Whoosh(), p, 0.75f, 70f, toBoat ? 1f : 0.85f);
    }

    bool Heard() { return Sfx.Near(transform.position) < 70f; }

    // synthesised mechanical whoosh: band-passed noise sweeping up then down, with a servo buzz
    static AudioClip Whoosh()
    {
        if (whoosh != null) return whoosh;
        const int rate = 22050; int n = (int)(rate * 1.1f);
        var d = new float[n];
        var r = new System.Random(5);
        float lp = 0f, lp2 = 0f, ph = 0f;
        for (int i = 0; i < n; i++)
        {
            float x = i / (float)n;
            float env = Mathf.Sin(Mathf.PI * Mathf.Pow(x, 0.7f)) * (1f - x * 0.4f);
            float cut = 0.04f + 0.35f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(x * 1.15f));
            float nz = (float)(r.NextDouble() * 2.0 - 1.0);
            lp += (nz - lp) * cut; lp2 += (lp - lp2) * cut;
            ph += (90f + 260f * x) / rate;
            float buzz = (Mathf.Repeat(ph, 1f) < 0.5f ? 1f : -1f) * 0.12f * Mathf.Clamp01(1f - Mathf.Abs(x - 0.45f) * 2.2f);
            d[i] = Mathf.Clamp((lp - lp2 * 0.6f) * 2.2f * env + buzz * env, -1f, 1f) * 0.8f;
        }
        whoosh = AudioClip.Create("cyber_whoosh", n, 1, rate, false);
        whoosh.SetData(d, 0);
        return whoosh;
    }

    // ---------------- visuals ----------------
    static float S(float a, float b, float x) { return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((x - a) / (b - a))); }

    // GroundVehicle.Update hands each wheel's normal pose here; folded wheels lie flat inside the wells
    public void WheelPose(Vector3 mount, ref Vector3 p, ref Quaternion rot)
    {
        if (k <= 0f) return;
        float a = S(0.05f, 0.4f, k);
        float sx = Mathf.Sign(mount.x);
        Vector3 tuck = new Vector3(sx * 0.55f, 0.8f, mount.z);
        p = Vector3.Lerp(p, tuck, a);
        Quaternion flat = Quaternion.Euler(0f, 0f, -sx * 90f);
        rot = Quaternion.Slerp(rot, flat, a);
    }

    void Update()
    {
        if (!built) return;
        float dt = Time.deltaTime;
        bool show = k > 0.001f;
        if (partsRoot.activeSelf != show) partsRoot.SetActive(show);
        // sounds at the steps of the sequence (both directions)
        if (Heard()) Cue(prevK, k);
        if (k > 0.5f != prevK > 0.5f) v.SetEngineKind(k > 0.5f ? 11 : 7);
        prevK = k;
        if (!show) { foreach (var tr in trails) tr.emitting = false; return; }

        // V-hull drops out of the belly (pivot at the belly, grows down)
        float h = S(0.25f, 0.65f, k);
        hull.localScale = new Vector3(1f, Mathf.Lerp(0.04f, 1f, h), Mathf.Lerp(0.9f, 1f, h));
        // arch shutters roll down
        float sh = S(0.3f, 0.55f, k);
        foreach (var s in shutters) s.localScale = new Vector3(1f, Mathf.Lerp(0.02f, 1f, sh), 1f);
        // sponsons slide out, then drop
        float so = S(0.4f, 0.7f, k), sd = S(0.62f, 0.85f, k);
        for (int i = 0; i < 2; i++)
        {
            Transform sp = i == 0 ? sponL : sponR;
            float sx = i == 0 ? -1f : 1f;
            sp.localPosition = new Vector3(sx * Mathf.Lerp(0.5f, 1.2f, so), Mathf.Lerp(0.42f, 0.14f, sd), Mathf.Lerp(-0.2f, 0f, so));
            sp.localScale = new Vector3(Mathf.Lerp(0.6f, 1f, so), 1f, Mathf.Lerp(0.85f, 1f, so));
        }
        // water-jet swings out of the tail (180 deg about its hinge)
        float j = S(0.55f, 0.85f, k);
        jet.localRotation = Quaternion.Euler(Mathf.Lerp(180f, 0f, j), 0f, 0f);
        // bow deflector + rear wing
        float f = S(0.7f, 0.95f, k);
        deflector.localRotation = Quaternion.Euler(Mathf.Lerp(0f, 40f, f), 0f, 0f);
        deflector.localScale = new Vector3(1f, 1f, Mathf.Lerp(0.2f, 1f, f));
        wing.localPosition = new Vector3(0f, Mathf.Lerp(1.42f, 2.0f, f), -2.45f);
        wing.localScale = new Vector3(Mathf.Lerp(0.5f, 1f, f), 1f, 1f);

        // light strips: strobe while transforming, steady glow (pulse with throttle) in boat mode
        bool moving = k > 0.001f && k < 0.999f;
        strobeT += dt;
        float led;
        if (moving) led = Mathf.Repeat(strobeT * 7f, 1f) < 0.5f ? 1f : 0.15f;
        else led = 0.55f + 0.25f * Mathf.Sin(strobeT * 3f);
        Color lc = new Color(0.35f, 0.95f, 1f) * led;
        lc.a = 1f;
        ledMat.color = lc;
        float thr = v.Throttle01;
        Color gc = Color.Lerp(new Color(0.2f, 0.5f, 0.7f), new Color(0.6f, 1f, 1f), thr) * (0.4f + 0.6f * j);
        gc.a = 1f;
        glowMat.color = gc;

        // wake trails pinned to the water surface + spray
        Vector3 pos = transform.position;
        float spd = v.Speed;
        float surf, depth;
        bool inWater = Water.At(pos.x, pos.z, out surf, out depth);
        bool wake = inWater && Wet > 0f && k > 0.6f && spd > 2f;
        for (int i = 0; i < trails.Length; i++)
        {
            Vector3 wp = trailPts[i].position;
            wp.y = surf + 0.04f;
            trails[i].transform.position = wp;
            trails[i].transform.rotation = Quaternion.LookRotation(Vector3.up, transform.forward);
            trails[i].emitting = wake;
        }
        if (wake)
        {
            sprayT += dt * (6f + spd * 1.6f) * (Look.Mobile ? 0.5f : 1f);
            Vector3 fwd = transform.forward, right = transform.right;
            while (sprayT >= 1f)
            {
                sprayT -= 1f;
                float sk = Mathf.Clamp01(spd / 20f);
                // bow spray thrown out sideways
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 bp = pos + fwd * Random.Range(1.2f, 2.4f) + right * s * 1.45f; bp.y = surf + 0.1f;
                    FX.Spray(bp, right * s * Random.Range(2f, 4.5f) * (0.5f + sk) + Vector3.up * Random.Range(1.5f, 3.5f) * (0.4f + sk) - fwd * spd * 0.3f,
                        Random.Range(0.12f, 0.3f), Random.Range(0.35f, 0.7f), new Color(0.9f, 0.97f, 1f, 0.8f));
                }
                // rooster tail from the jet
                if (v.Throttle01 > 0.1f)
                {
                    Vector3 jp = jet.TransformPoint(new Vector3(0f, -0.05f, -0.55f)); jp.y = Mathf.Max(jp.y, surf + 0.05f);
                    FX.Spray(jp, -fwd * Random.Range(4f, 8f) * (0.4f + sk) + Vector3.up * Random.Range(2.5f, 5f) * (0.3f + sk) + Random.insideUnitSphere,
                        Random.Range(0.18f, 0.45f), Random.Range(0.5f, 0.9f), new Color(0.92f, 0.98f, 1f, 0.75f));
                }
            }
        }
        else if (moving && inWater && Random.value < 0.5f)
        {
            Vector3 sp = pos + new Vector3(Random.Range(-1.3f, 1.3f), 0f, Random.Range(-2.6f, 2.6f)); sp.y = surf + 0.1f;
            FX.Spray(sp, Random.insideUnitSphere * 2f + Vector3.up * 2.5f, Random.Range(0.15f, 0.35f), Random.Range(0.4f, 0.8f), new Color(0.88f, 0.96f, 1f, 0.8f));
        }
    }

    void Cue(float a, float b)
    {
        if (a == b) return;
        System.Func<float, bool> crossed = x => (a < x && b >= x) || (a > x && b <= x);
        Vector3 p = transform.position;
        if (crossed(0.05f)) Sfx.PlayAt(Sfx.Pick(Sfx.Servo), p, 0.55f, 50f, 0.9f);
        if (crossed(0.3f)) Sfx.PlayAt(Sfx.Pick(Sfx.Servo), p, 0.5f, 50f, 1.1f);
        if (crossed(0.45f) && Sfx.Clank != null) Sfx.PlayAt(Sfx.Clank, p, 0.5f, 50f, 0.8f);
        if (crossed(0.6f)) Sfx.PlayAt(Sfx.Pick(Sfx.Servo), p, 0.5f, 50f, 1.25f);
        if (crossed(0.85f) && Sfx.Clank != null) Sfx.PlayAt(Sfx.Clank, p, 0.55f, 50f, 1.05f);
        if (b >= 0.999f && a < 0.999f && Sfx.Confirm != null) Sfx.PlayAt(Sfx.Confirm, p, 0.35f, 50f, 1.1f);
    }

    // ---------------- build the boat parts (first transform only) ----------------
    void Build()
    {
        built = true;
        Transform t = transform;
        partsRoot = new GameObject("CyberboatParts");
        partsRoot.transform.SetParent(t, false);
        Transform R = partsRoot.transform;
        Material steel = Mats.PBR(Mats.Hex("#B8BCC0"), 0.62f, 0.9f);
        Material dark = Mats.PBR(Mats.Hex("#151617"), 0.3f, 0.1f);
        ledMat = new Material(Mats.Unlit(new Color(0.35f, 0.95f, 1f)));
        glowMat = new Material(Mats.Unlit(new Color(0.4f, 0.9f, 1f)));

        // V-hull: pivot at the belly (y 0.56), grows downward
        hull = Mats.Node(R, "Hull", new Vector3(0f, 0.56f, 0f));
        var hullGo = new GameObject("HullMesh");
        hullGo.transform.SetParent(hull, false);
        hullGo.transform.localPosition = new Vector3(0f, -0.56f, 0f);
        hullGo.AddComponent<MeshFilter>().sharedMesh = HullMesh();
        hullGo.AddComponent<MeshRenderer>().sharedMaterial = steel;
        // black chine strakes along the hull
        for (int s = -1; s <= 1; s += 2)
            Mats.Prim(PrimitiveType.Cube, hullGo.transform, new Vector3(s * 0.99f, 0.3f, -0.2f), new Vector3(0.06f, 0.05f, 4.9f), dark);

        // arch shutters: stainless plates that roll down over the wheel openings (pivot at the arch top)
        foreach (float z in new[] { 1.85f, -1.75f })
            for (int s = -1; s <= 1; s += 2)
            {
                Transform n = Mats.Node(R, "Shutter", new Vector3(s * 1.03f, 0.985f, z));
                Mats.Prim(PrimitiveType.Cube, n, new Vector3(0f, -0.24f, 0f), new Vector3(0.03f, 0.48f, 1.5f), steel);
                Mats.Prim(PrimitiveType.Cube, n, new Vector3(s * 0.012f, -0.46f, 0f), new Vector3(0.02f, 0.035f, 1.5f), dark);
                shutters.Add(n);
            }

        // sponsons
        Mesh spm = SponsonMesh();
        for (int i = 0; i < 2; i++)
        {
            float sx = i == 0 ? -1f : 1f;
            Transform n = Mats.Node(R, i == 0 ? "SponsonL" : "SponsonR", new Vector3(sx * 0.5f, 0.42f, 0f));
            var g = new GameObject("SponsonMesh");
            g.transform.SetParent(n, false);
            g.AddComponent<MeshFilter>().sharedMesh = spm;
            g.AddComponent<MeshRenderer>().sharedMaterial = steel;
            Mats.Prim(PrimitiveType.Cube, n, new Vector3(sx * 0.255f, 0.3f, -0.1f), new Vector3(0.02f, 0.05f, 4.2f), dark);   // rub strake
            var led = Mats.Prim(PrimitiveType.Cube, n, new Vector3(sx * 0.262f, 0.2f, 0.0f), new Vector3(0.012f, 0.035f, 3.6f), ledMat);
            leds.Add(led.GetComponent<Renderer>());
            if (i == 0) sponL = n; else sponR = n;
        }
        // side light strips on the body (flash while transforming)
        for (int s = -1; s <= 1; s += 2)
        {
            var led = Mats.Prim(PrimitiveType.Cube, R, new Vector3(s * 1.032f, 0.66f, 0.05f), new Vector3(0.01f, 0.03f, 2.0f), ledMat);
            leds.Add(led.GetComponent<Renderer>());
        }
        var front = Mats.Prim(PrimitiveType.Cube, R, new Vector3(0f, 1.06f, 2.885f), new Vector3(1.6f, 0.025f, 0.01f), ledMat);
        leds.Add(front.GetComponent<Renderer>());

        // water-jet unit hinged under the tail: stowed pointing forward under the body, swings 180 deg to point aft
        jet = Mats.Node(R, "Jet", new Vector3(0f, 0.5f, -2.62f));
        Mats.Prim(PrimitiveType.Cube, jet, new Vector3(0f, -0.06f, -0.22f), new Vector3(0.95f, 0.3f, 0.5f), steel);
        for (int s = -1; s <= 1; s += 2)
        {
            Mats.Prim(PrimitiveType.Cylinder, jet, new Vector3(s * 0.25f, -0.08f, -0.5f), new Vector3(0.3f, 0.2f, 0.3f), new Vector3(90f, 0f, 0f), dark);
            Mats.Prim(PrimitiveType.Cylinder, jet, new Vector3(s * 0.25f, -0.08f, -0.7f), new Vector3(0.22f, 0.012f, 0.22f), new Vector3(90f, 0f, 0f), glowMat);
        }
        Mats.Prim(PrimitiveType.Cube, jet, new Vector3(0f, 0.04f, -0.36f), new Vector3(0.9f, 0.03f, 0.16f), dark);   // trim flap

        // bow deflector: hinged at the front of the hood, lies flat until it flips up
        deflector = Mats.Node(R, "Deflector", new Vector3(0f, 1.23f, 2.6f));
        Mats.Prim(PrimitiveType.Cube, deflector, new Vector3(0f, 0.01f, -0.3f), new Vector3(1.7f, 0.035f, 0.6f), steel);
        Mats.Prim(PrimitiveType.Cube, deflector, new Vector3(0f, 0.035f, -0.32f), new Vector3(1.4f, 0.02f, 0.42f), Mats.Glass);
        // rear wing rising out of the tonneau
        wing = Mats.Node(R, "Wing", new Vector3(0f, 1.42f, -2.45f));
        Mats.Prim(PrimitiveType.Cube, wing, new Vector3(0f, 0f, 0f), new Vector3(1.95f, 0.05f, 0.42f), new Vector3(-8f, 0f, 0f), steel);
        for (int s = -1; s <= 1; s += 2)
        {
            Mats.Prim(PrimitiveType.Cube, wing, new Vector3(s * 0.62f, -0.22f, 0.02f), new Vector3(0.05f, 0.45f, 0.22f), dark);
            Mats.Prim(PrimitiveType.Cube, wing, new Vector3(s * 0.98f, 0.06f, 0f), new Vector3(0.03f, 0.16f, 0.46f), steel);
        }
        Mats.Prim(PrimitiveType.Cube, wing, new Vector3(0f, 0.032f, -0.2f), new Vector3(1.9f, 0.012f, 0.015f), ledMat);

        foreach (var r in partsRoot.GetComponentsInChildren<Renderer>())
            r.shadowCastingMode = Look.Mobile ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
        Mats.SetLayer(partsRoot, Vehicle.VehicleLayer);
        // Prim() only queues its collider for Destroy; drop them now so they never join the truck's compound collider
        foreach (var c in partsRoot.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);

        // hull collider (only while in boat mode) so the hull sits on ramps / the dock instead of the body box
        hullCol = v.ExtraBoxPublic(new Vector3(0f, 0.38f, 0f), new Vector3(2.4f, 0.5f, 5.4f));
        hullCol.enabled = false;

        // wake trails: two from the sponson sterns, one from the jet
        trailPts = new[] { Mats.Node(t, "WakeL", new Vector3(-1.2f, 0f, -2.4f)), Mats.Node(t, "WakeR", new Vector3(1.2f, 0f, -2.4f)), Mats.Node(t, "WakeJ", new Vector3(0f, 0f, -3.1f)) };
        trails = new TrailRenderer[3];
        for (int i = 0; i < 3; i++)
        {
            var g = new GameObject("Wake" + i);   // not parented: pinned to the water each frame
            var tr = g.AddComponent<TrailRenderer>();
            tr.time = i == 2 ? 1.6f : 2.4f;
            tr.minVertexDistance = 0.6f;
            tr.alignment = LineAlignment.TransformZ;
            tr.widthMultiplier = i == 2 ? 1.6f : 2.6f;
            tr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(1f, 1f));
            var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.85f, 0.95f, 1f), 1f) },
                       new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.6f, 0.35f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = gr;
            tr.sharedMaterial = FoamMat();
            tr.textureMode = LineTextureMode.Stretch;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.emitting = false;
            trails[i] = tr;
        }
        Debug.Log("CyberBoat: parts built for " + v.Title);
    }

    void OnDestroy()
    {
        if (trails != null) foreach (var tr in trails) if (tr != null) Destroy(tr.gameObject);
    }

    // foam texture across the trail width: bright frothy edges, lighter middle, soft border
    static Material FoamMat()
    {
        if (foamMat != null) return foamMat;
        const int w = 8, h = 64;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var r = new System.Random(3);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float v = (y + 0.5f) / h;            // across the width
            float e = Mathf.Abs(v - 0.5f) * 2f;  // 0 centre .. 1 edge
            float a = 0.55f + 0.45f * Mathf.Exp(-Mathf.Pow((e - 0.72f) / 0.2f, 2f));
            a *= Mathf.Clamp01((1f - e) / 0.08f);
            for (int x = 0; x < w; x++)
            {
                float n = 0.8f + 0.2f * (float)r.NextDouble();
                px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.Clamp(a * n * 255f, 0f, 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        foamMat = new Material(Mats.Fx);
        foamMat.mainTexture = tex;
        return foamMat;
    }

    // ---------------- procedural faceted meshes (flat shaded) ----------------
    // loft through closed or open polylines (same vertex count) at increasing z; caps optional
    static Mesh Loft(List<Vector3[]> rings, bool closed, bool capFirst, bool capLast, System.Func<float, Vector3> inside)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        System.Action<Vector3, Vector3, Vector3> tri = (a, b, c) =>
        {
            Vector3 nrm = Vector3.Cross(b - a, c - a);
            if (nrm.sqrMagnitude < 1e-10f) return;
            Vector3 cen = (a + b + c) / 3f;
            if (Vector3.Dot(nrm, cen - inside(cen.z)) < 0f) { Vector3 tmp = b; b = c; c = tmp; }
            int i0 = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2);
        };
        for (int r = 0; r < rings.Count - 1; r++)
        {
            Vector3[] A = rings[r], B = rings[r + 1];
            int n = A.Length, segs = closed ? n : n - 1;
            for (int i = 0; i < segs; i++)
            {
                int j = (i + 1) % n;
                tri(A[i], A[j], B[j]);
                tri(A[i], B[j], B[i]);
            }
        }
        if (capFirst) { Vector3[] A = rings[0]; for (int i = 1; i < A.Length - 1; i++) tri(A[0], A[i], A[i + 1]); }
        if (capLast) { Vector3[] A = rings[rings.Count - 1]; for (int i = 1; i < A.Length - 1; i++) tri(A[0], A[i], A[i + 1]); }
        var m = new Mesh();
        m.SetVertices(verts);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // V-hull under the body: top edge at the belly (y 0.56), chines at y 0.28, keel at y 0.06, raked bow
    static Mesh HullMesh()
    {
        var rings = new List<Vector3[]>();
        System.Func<float, float, float, float, float, Vector3[]> ring = (z, top, wc, yc, yk) => new[]
        {
            new Vector3(-1.0f, top, z), new Vector3(-wc, yc, z), new Vector3(-wc * 0.45f, (yc + yk) * 0.5f - 0.02f, z), new Vector3(0f, yk, z),
            new Vector3(wc * 0.45f, (yc + yk) * 0.5f - 0.02f, z), new Vector3(wc, yc, z), new Vector3(1.0f, top, z)
        };
        rings.Add(ring(-2.82f, 0.62f, 1.0f, 0.3f, 0.1f));
        rings.Add(ring(-1.2f, 0.58f, 1.0f, 0.28f, 0.06f));
        rings.Add(ring(1.0f, 0.58f, 1.0f, 0.28f, 0.06f));
        rings.Add(ring(2.15f, 0.6f, 0.92f, 0.36f, 0.2f));
        rings.Add(ring(2.75f, 0.75f, 0.7f, 0.58f, 0.5f));
        rings.Add(ring(2.98f, 0.95f, 0.4f, 0.88f, 0.84f));
        return Loft(rings, false, true, false, z => new Vector3(0f, 0.7f, Mathf.Clamp(z, -2.5f, 2.5f)));
    }

    // sponson: faceted float, 0.5 wide, 0.42 tall (local y 0..0.42), pointed raked bow, flat transom
    static Mesh SponsonMesh()
    {
        var rings = new List<Vector3[]>();
        System.Func<float, float, float, Vector3[]> ring = (z, s, lift) => new[]
        {
            new Vector3(-0.23f * s, 0.42f, z), new Vector3(0.23f * s, 0.42f, z), new Vector3(0.26f * s, lift + (0.22f - lift) * s, z),
            new Vector3(0.1f * s, lift + (0f - lift) * s, z), new Vector3(-0.1f * s, lift + (0f - lift) * s, z), new Vector3(-0.26f * s, lift + (0.22f - lift) * s, z)
        };
        rings.Add(ring(-2.55f, 1f, 0.3f));
        rings.Add(ring(1.4f, 1f, 0.3f));
        rings.Add(ring(2.25f, 0.62f, 0.34f));
        rings.Add(ring(2.75f, 0.12f, 0.38f));
        return Loft(rings, true, true, true, z => new Vector3(0f, 0.25f, Mathf.Clamp(z, -2.3f, 2.4f)));
    }
}
