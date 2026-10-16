using UnityEngine;

// u6i: the 2026 Tesla Roadster's tricks (only when this kart drives car Cars.Roadster).
//  * Thruster hop (keys J / E, pad Y / R3, touch JUMP): cold-gas thrusters at the four corners fire down -> a controlled hop
//    high enough to clear another car (kart-kart contact is skipped above 1.6 m), a short hover at the top, then a thrusted,
//    soft landing (white vapour puffs the whole way). Costs compressed air from a gauge that refills on the ground.
//      KID:    tap = full hop (2.6 m), automatic hover + soft landing, 2 hops in a row, refills in ~2 s.
//      EXPERT: shorter kick (2 m); HOLD the button to keep the thrusters lit (hover, longer flight, soft landing) - it drains
//              the air fast; let go too early and it drops hard (thump, scrub). Smaller tank refill.
//  * Rear main thrusters: a launch kick from a standstill (race GO or a stop) while the air lasts (real car: 0-60 mph ~0.8 s).
//  * Suction fans: extra tyre grip on the ground (FanMu, more in EXPERT, where the Roadster is also all-wheel drive).
//  * Retractable rear wing: folds into the tail at low speed, deploys at speed / under braking, pitches up as an air brake in a hop.
// The AI (and the ?lbdemo autopilot) hops a car it is about to rear-end. Probe log: 'U6I: hop k.. cleared k.. dy ..'.
public partial class Kart
{
    public float thrustAir = 1f;          // 0..1 compressed-air gauge (HUD)
    public bool hopping;
    float hopT, hoverT, hopCool, launchT, standT, hopLogT;
    bool launchArmed, hopSoft;
    int hopCleared;
    Transform wingT; Vector3 wingBase; float wingK = 1f;
    static readonly Vector3[] Corner = { new Vector3(-0.62f, 0.2f, 2.02f), new Vector3(0.62f, 0.2f, 2.02f), new Vector3(-0.86f, 0.2f, 0.75f), new Vector3(0.86f, 0.2f, 0.75f),
                                         new Vector3(-0.86f, 0.2f, -0.6f), new Vector3(0.86f, 0.2f, -0.6f), new Vector3(-0.83f, 0.24f, -1.98f), new Vector3(0.83f, 0.24f, -1.98f) };
    static readonly Vector3[] Main = { new Vector3(-0.57f, 0.41f, -2.22f), new Vector3(-0.19f, 0.41f, -2.22f), new Vector3(0.19f, 0.41f, -2.22f), new Vector3(0.57f, 0.41f, -2.22f) };

    public bool IsRoadster { get { return Cars.IsRoadster(car); } }
    bool HopExpert { get { return expert && human; } }
    float HopCost { get { return HopExpert ? 0.42f : 0.48f; } }
    public bool HopReady { get { return IsRoadster && thrustAir >= HopCost && hopCool <= 0f && !respawning; } }
    static int hopLogS = -1;
    static bool hopLog { get { if (hopLogS < 0) hopLogS = (Application.absoluteURL ?? "").Contains("lbhop") ? 1 : 0; return hopLogS == 1; } }

    void HopSetup()
    {
        wingT = sprung != null ? sprung.Find("Wing") : null;
        if (wingT != null) wingBase = wingT.localPosition;
        wingK = 1f;
    }

    void HopReset()
    {
        hopping = false; hopT = hoverT = hopCool = launchT = 0f; thrustAir = 1f; standT = 0f; launchArmed = false;
    }

    // per frame, before the car model: refill, launch kick timer
    void HopTick(float dt, KIn i)
    {
        if (!IsRoadster) return;
        if (hopCool > 0f) hopCool -= dt;
        if (grounded && !hopping) thrustAir = Mathf.Min(1f, thrustAir + dt * (HopExpert ? 0.17f : 0.26f));
        if (grounded && Mathf.Abs(speed) < 1.5f) { standT += dt; if (standT > 0.6f) launchArmed = true; } else standT = 0f;
    }

    public void HopArmLaunch() { if (IsRoadster) launchArmed = true; }

    // rear main thrusters off the line: extra push while the air lasts (call after Drive)
    void HopLaunch(KIn i, float dt)
    {
        if (!IsRoadster) return;
        if (launchArmed && grounded && i.gas > 0.8f && speed > -0.5f && thrustAir > 0.2f && spinT <= 0f)
        {
            launchArmed = false; launchT = 0.85f;
            if (human) { Sfx.PlayAt(Sfx.Boost, transform.position, 0.55f, 60f, 0.7f); Toast("THRUSTER LAUNCH!", 1.1f); }
        }
        if (launchT <= 0f) return;
        launchT -= dt;
        if (!grounded || i.gas < 0.3f || speed > 27f) { launchT = 0f; return; }
        speed += (HopExpert ? 20f : 16f) * dt;           // with the tyres + fans: ~0-27 m/s in about a second
        thrustAir = Mathf.Max(0f, thrustAir - dt * 0.35f);
        Vector3 back = -Forward, v = Velocity;
        foreach (var m in Main) if (Random.value < Mathf.Min(1f, 18f * dt)) FX.ColdGas(transform.TransformPoint(m), back, v, 1f);   // u6i2: rate per second, not per frame
    }

    bool HopWants(KIn i) { return IsRoadster && i.jump && HopReady && (grounded || airT < 0.12f) && !hopping; }

    void StartHop(ref Vector3 pos)
    {
        hopping = true; hopT = 0f; hoverT = 0f; hopCleared = 0; hopSoft = true;
        thrustAir = Mathf.Max(0f, thrustAir - HopCost);
        hopCool = 0.35f;
        grounded = false; airT = 0f; trickT = 0f; trickRoll = 0f; bigAir = false;
        vy = Mathf.Max(vy, 0f) + (HopExpert ? 9.8f : 11.2f);
        pos.y += 0.06f;
        float top = spec.top * 1.12f;
        if (speed > 3f) speed = Mathf.Min(top, speed + (HopExpert ? 2.5f : 3.5f));   // rear thrusters push a little too
        if (Autopilot || hopLog) Debug.Log("U6I: hop k" + id + " v " + speed.ToString("F1") + " air " + thrustAir.ToString("F2") + (HopExpert ? " EXPERT" : " KID"));
        Sfx.PlayAt(Sfx.Boost, pos, human ? 0.75f : 0.45f, 60f, 1.45f);
        Sfx.PlayAt(Sfx.Splash, pos, human ? 0.35f : 0.2f, 50f, 1.8f);     // the hiss
        Vector3 v = Velocity;
        for (int k = 0; k < 2; k++) foreach (var c in Corner) FX.ColdGas(transform.TransformPoint(c), Vector3.down, v, 1f);
        if (human) Game.Shake(this, 0.12f);
    }

    // airborne step while hopping (replaces the plain ballistic step)
    void HopAir(ref Vector3 pos, float gy, KIn i, float dt)
    {
        hopT += dt;
        float h = pos.y - gy;
        float g; bool burn = false;
        if (vy > 0f) g = 24f;                                             // up: the kick is over, plain gravity
        else if (!HopExpert && hoverT < 0.4f) { g = 0f; vy = Mathf.MoveTowards(vy, 0f, 40f * dt); hoverT += dt; burn = true; }   // KID: auto hover at the top
        else if (HopExpert && i.jumpHeld && thrustAir > 0.02f && hoverT < 0.9f) { g = 4f; hoverT += dt; burn = true; thrustAir = Mathf.Max(0f, thrustAir - dt * 0.55f); }
        else g = HopExpert ? 20f : 11f;
        float ny = vy - g * dt;
        float cap = HopExpert ? (i.jumpHeld ? -4f : -14f) : -5.5f;
        if (ny < cap) { ny = Mathf.MoveTowards(vy, cap, 30f * dt); burn = true; }
        // landing burn: the corner thrusters brake the last metre (KID always, EXPERT only while held)
        if (ny < -2.2f && h < 1.3f && (!HopExpert || i.jumpHeld)) { ny = Mathf.MoveTowards(vy, -2.2f, 34f * dt); burn = true; }
        hopSoft = !HopExpert || i.jumpHeld;
        pos.y += (vy + ny) * 0.5f * dt;
        vy = ny;
        if (burn || hopT < 0.18f)
        {
            Vector3 v = Velocity; float st = hopT < 0.18f ? 1f : 0.6f;
            for (int k = 0; k < Corner.Length; k++) if (Random.value < Mathf.Min(1f, 14f * dt)) FX.ColdGas(transform.TransformPoint(Corner[k]), Vector3.down, v, st);
        }
        // probe evidence: passed over another car
        if ((Autopilot || hopLog) && Game.I != null)
        {
            foreach (Kart o in Game.I.karts)
            {
                if (o == this || (hopCleared & (1 << o.id)) != 0) continue;
                Vector3 d = Track.Flat(o.transform.position - pos);
                float dy = pos.y - o.transform.position.y;
                if (d.magnitude < 2.5f && dy > 1.6f)
                {
                    hopCleared |= 1 << o.id;
                    Debug.Log("U6I: hop k" + id + " cleared k" + o.id + " (" + o.Nick + ") dy " + dy.ToString("F2") + " m dist " + d.magnitude.ToString("F2") + " t " + hopT.ToString("F2") + " s");
                }
            }
        }
    }

    void HopLand(Vector3 pos)
    {
        hopping = false;
        Vector3 v = Velocity;
        for (int k = 0; k < 3; k++) foreach (var c in Corner) FX.ColdGas(transform.TransformPoint(c), (Vector3.down + Random.insideUnitSphere * 0.6f).normalized, v, 0.9f);
        if (!hopSoft && vy < -8f) { speed *= 0.85f; if (human) Toast("HARD LANDING - hold the hop button to brake the fall", 1.6f); }
        else if (human && hopCleared != 0) Toast("THRUSTER HOP!", 1.0f);
        if (Autopilot || hopLog) Debug.Log("U6I: hop k" + id + " landed vy " + vy.ToString("F1") + " flight " + hopT.ToString("F2") + " s soft " + hopSoft + " cleared " + CountBits(hopCleared));
    }

    static int CountBits(int m) { int c = 0; while (m != 0) { c += m & 1; m >>= 1; } return c; }

    // suction fans: extra grip on the ground (fraction of the surface mu)
    float FanMu(float mu) { return IsRoadster && grounded ? mu * (expert && human ? 0.42f : 0.28f) : 0f; }

    // AI / autopilot: hop a car it is about to rear-end
    bool AIHop()
    {
        if (!HopReady || !grounded || Game.I == null || speed < 8f) return false;
        Vector3 pos = transform.position, fwd = Forward;
        foreach (Kart o in Game.I.karts)
        {
            if (o == this || o.respawning) continue;
            Vector3 d = Track.Flat(o.transform.position - pos);
            float ahead = Vector3.Dot(d, fwd), side = Mathf.Abs(Vector3.Dot(d, Right));
            if (side > 2.2f || ahead < 2.5f) continue;
            float vrel = speed - Vector3.Dot(o.Velocity, fwd);
            if (vrel < 2f) continue;
            float gap = ahead - 2.5f;                 // contact starts at 2.5 m
            if (gap > vrel * 0.16f && gap < vrel * 0.5f + 1.5f) return true;
        }
        return false;
    }

    // visuals: wing, idle vapour
    void HopVisual(float dt)
    {
        if (wingT == null) return;
        float want = (Mathf.Abs(speed) > 16f || (braking && speed > 10f) || hopping) ? 1f : 0f;
        wingK = Mathf.MoveTowards(wingK, want, dt * 1.8f);
        float k = wingK * wingK * (3f - 2f * wingK);
        wingT.localPosition = wingBase + new Vector3(0f, -0.25f * (1f - k), 0.02f * (1f - k));
        wingT.localScale = new Vector3(Mathf.Lerp(0.9f, 1f, k), Mathf.Lerp(0.28f, 1f, k), 1f);
        float pitch = hopping && vy < 0f ? -16f : braking && speed > 10f ? -10f : 0f;      // air brake
        wingT.localRotation = Quaternion.Slerp(wingT.localRotation, Quaternion.Euler(pitch * k, 0f, 0f), Mathf.Min(1f, dt * 6f));
    }
}
