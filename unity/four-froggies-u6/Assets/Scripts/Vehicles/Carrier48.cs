using System.Collections.Generic;
using UnityEngine;

// ffu48 SPOTTY'S CARRIER CAMPER (Bill: "spotty has a camper trailer that transforms into a camper aircraft carrier kinda
// [like the] Cybertruck transforms. Spotty's motorcycle can tow the trailer and when it gets on the water it goes on its
// side and becomes part of the aircraft carrier.")
//  * Spotty's Spot: a gravel pad on the pond's north shore (CarrierLayout picks it from pure Layout maths, so Ranch keeps
//    trees off it) with Spotty's Tourer motorcycle (BikeSpotty48.cs, ffu45 adventure-bike physics, no wheelies, hitch arm)
//    hitched to the Carrier Camper (a ffu31 Trailer, style 4: white with black spots, pink stripe, 2 solar panels).
//    CANON: both are Spotty's - others ask to borrow (ownerName / ownerChar 4), like the Cybertrucks.
//  * The bike tows the camper with the ffu31 hitch (TrailerTow prompt E / Y / tap, joint roll free for a bike).
//  * Ride the rig into the pond (bike front > 1 m deep): ONE progress value k (0 rig .. 1 carrier, reversible):
//      k .00-.12  the bike tips onto its side, Spotty (the rider) hops up onto the camper roof
//      k .12-.30  the camper SINKS (water up to the roof), bubbles, groan          <- the story holds here (holdAt)
//      k .30-.72  under water: hull halves unfold, bow + stern slide out, the bike slides back into the stern pod,
//                 deck rises off the roof, panels telescope + fold out, the island tower climbs (lifting the rider
//                 to the flying bridge)
//      k .72-.95  the whole carrier RISES out of the water (waterline 2.45 -> 0.4 m), water cascades off the deck
//      k .95-1    deck lights, horn, "CARRIER MODE!"
//    Servo / clank / groan / rumble cues at the steps, both directions. Drive back to the shore -> reverses (3.5 s): bike
//    rights itself at the bow, re-hitched, rider back on the bike.
//  * Carrier: 28 x 9 m hull, flight deck 3 m above the water (solid, walkable: frogs ride along via CarrierRig.Carry,
//    James's VTOL jet can land on it and rides along too - Jet.cs), island + flying bridge (the helm = CarrierHelm, a
//    Vehicle), float points on Ripples.SurfaceY + Water.Wave, presses the ffu38 wave sim (bow + stern), wake spray.
//    Swim up to the hull: "A / E: Climb aboard". Helm: A / E at the island door.
public static class CarrierLayout
{
    public const float DeckY = 3.4f;          // deck top, camper-local (camper local y 0 = ground under the wheels)
    public const float HalfW = 4.5f, Z0 = -12.5f, Z1 = 14f;   // deck extents (local)
    static bool picked;
    static Vector2 site; static float yaw;
    public static Vector2 Site { get { Pick(); return site; } }
    public static float Yaw { get { Pick(); return yaw; } }
    public static Vector2 Fwd { get { float a = Yaw * Mathf.Deg2Rad; return new Vector2(Mathf.Sin(a), Mathf.Cos(a)); } }

    static void Pick()
    {
        if (picked) return;
        picked = true;
        float[] angs = { 80f, 90f, 70f, 100f, 60f, 110f, 120f, 50f, 130f };
        site = Vector2.zero; bool ok = false;
        foreach (float a in angs)
        {
            float r = a * Mathf.Deg2Rad;
            Vector2 c = Layout.PondC + new Vector2(Mathf.Cos(r) * Layout.PondR.x, Mathf.Sin(r) * Layout.PondR.y) * 1.45f;
            Vector2 d = (Layout.PondC - c).normalized;
            if (Clear(c, d)) { site = c; ok = true; break; }
        }
        if (!ok) { float r = 80f * Mathf.Deg2Rad; site = Layout.PondC + new Vector2(Mathf.Cos(r) * Layout.PondR.x, Mathf.Sin(r) * Layout.PondR.y) * 1.45f; }
        Vector2 f = (Layout.PondC - site).normalized;
        yaw = Mathf.Atan2(f.x, f.y) * Mathf.Rad2Deg;
        Debug.Log("CarrierLayout: Spotty's Spot at " + site.ToString("0") + " yaw " + yaw.ToString("0") + (ok ? "" : " (fallback)"));
    }

    static bool Clear(Vector2 c, Vector2 d)
    {
        Vector2 r = new Vector2(d.y, -d.x);
        float lo = 1e9f, hi = -1e9f;
        for (float u = -9f; u <= 9f; u += 3f)
            for (float v = -8f; v <= 10f; v += 3f)
            {
                Vector2 p = c + r * u + d * v;
                if (Mathf.Abs(p.x) > 165f || Mathf.Abs(p.y) > 165f) return false;
                if (Layout.RoadDist(p.x, p.y) < Layout.TrackW * 0.5f + 3f) return false;
                if (Layout.PondQ(p.x, p.y) < 1.2f) return false;
                if (JetLayout.Reserved(p.x, p.y) || RcTrack.Reserved(p.x, p.y) || MonsterPark.Reserved(p.x, p.y)) return false;
                if ((p - StorySet.PadXZ).magnitude < 25f) return false;
                if ((p - Layout.HouseC).magnitude < 40f || (p - Layout.GarageC).magnitude < 40f) return false;
                float g = Layout.GroundY(p.x, p.y); lo = Mathf.Min(lo, g); hi = Mathf.Max(hi, g);
            }
        return hi - lo < 1.6f;
    }

    // Ranch keeps trees off the spot and the drive down to the water
    public static bool Reserved(float x, float z)
    {
        Vector2 p = new Vector2(x, z), c = Site, f = Fwd;
        if ((p - c).magnitude < 14f) return true;
        Vector2 q = p - c;
        float along = Vector2.Dot(q, f), side = Mathf.Abs(Vector2.Dot(q, new Vector2(f.y, -f.x)));
        return along > 0f && along < 40f && side < 6f;
    }
}

// the helm on the flying bridge: a Vehicle (camera, HUD, help, hints, enter / exit, borrow canon) sharing the camper's
// rigidbody. Its own small trigger box sits at deck level by the island door, so you take the helm from the deck and step
// off onto the deck; the seat (the driver's model) is up on the bridge.
public class CarrierHelm : Vehicle
{
    public CarrierRig rig;
    public PIn In { get { return inp; } }
    public System.Func<PIn, PIn> demoHook;   // probe autopilot for a frog at the helm
    public override string[] TouchSet { get { return new[] { "A", null, null, "GAS", "BRAKE" }; } }
    public override string HelpLine { get { return "Spotty's Carrier: L-stick steer | RT ahead | LT slow / astern | drive to the shore = back to bike + camper | A leave the helm"; } }
    public override string HelpFor(InputKind k)
    {
        if (k == InputKind.Gamepad) return "L-stick::steer the carrier|RT::ahead|LT::slow down / astern|Drive to the shore::back to bike + camper|Jet on the deck::James's VTOL lands + rides along|A::leave the helm (onto the deck)";
        if (k == InputKind.Touch) return "Stick::steer the carrier|GAS::ahead|BRAKE::slow down / astern|Drive to the shore::back to bike + camper|Jet on the deck::James's VTOL lands + rides along|A::leave the helm (onto the deck)";
        return "A  /  D::steer the carrier|W::ahead|S::slow down / astern|Drive to the shore::back to bike + camper|Jet on the deck::James's VTOL lands + rides along|E::leave the helm (onto the deck)";
    }
    public override string HudStatus { get { return "SPOTTY'S CARRIER  " + Mathf.RoundToInt(Speed * 3.6f) + " km/h" + (rig != null && rig.k < 0.999f ? "  ·  transforming" : ""); } }
    public override bool CanEnter(Frog f) { return rig != null && rig.k >= 0.999f && rig.toCarrier; }
    public override bool CanExit(Frog f) { return rig == null || rig.k >= 0.999f || rig.k <= 0.001f; }
    public override string DeniedLine { get { return "Spotty's Carrier - wait for it to finish transforming"; } }
    public void Setup(Vector3 home) { spawnPos = home; spawnRot = transform.rotation; }
    protected override void FixedUpdate()
    {
        if (driver == null) inp = new PIn();
        EngineSound(Time.fixedDeltaTime);
    }
}

public class CarrierRig : MonoBehaviour
{
    public static CarrierRig I;
    public Trailer trailer;
    public Bike bike;
    public CarrierHelm helm;
    public float k;                 // 0 bike + camper .. 1 carrier
    public bool toCarrier;
    public float holdAt = -1f;      // the story pauses k here (the sinking beat)
    public float speedK = 1f;       // the story slows the sequence
    public static float Tin = 6.5f, Tout = 3.5f;
    public System.Func<PIn, PIn> ghost;   // story / demo pilot (no Frog at the helm)
    public FrogModel npc;           // story: Spotty
    public static int Transforms;
    public bool Carrier { get { return toCarrier && k >= 0.999f; } }
    public bool Active { get { return k > 0f || toCarrier; } }
    public Transform seatNode;
    Vector3 homePos; Quaternion homeRot;
    Vector3 bikeStartL; Quaternion bikeStartR;
    Vector3 seatStartL;
    float mass0, prevK, cueCool, sayCool, idleT, bubT, sprayT, hornCool;
    Vector3 com0, inert0; Quaternion inertR0;
    bool built;
    GameObject parts;
    Transform hullL, hullR, bow, stern, deckC, deckF, deckA, island, radar, podRing, landRing;
    Material ledMat, deckLightMat, glowMat;
    readonly List<Collider> deckCols = new List<Collider>();
    readonly List<Hotspot> spots = new List<Hotspot>();
    readonly List<Collider> bikeCols = new List<Collider>();
    static AudioClip rumble, horn;

    static readonly Vector3 PodL = new Vector3(-0.75f, 0.55f, -13.2f);
    static readonly Vector3 RoofSpot = new Vector3(0f, 2.68f, 1.6f);
    static readonly Vector3 BridgeSpot = new Vector3(3.3f, CarrierLayout.DeckY + 4.05f, 0.7f);
    static readonly Vector3 HelmL = new Vector3(1.75f, CarrierLayout.DeckY + 0.75f, 0.7f);
    static readonly float[] FloatX = { -2.4f, 2.4f };
    static readonly float[] FZ = { -10f, -3.5f, 3.5f, 10.5f };

    // ---------------- spawn ----------------
    public static readonly Trailer.Spec Spec = new Trailer.Spec { name = "Spotty's Carrier Camper", shortName = "Carrier Camper", style = 4, len = 5.2f, wid = 2.2f, hgt = 2.1f, floorY = 0.55f, mass = 380f, wheelR = 0.36f, axleZ = -0.3f, tongue = 1.4f, main = new Color(0.97f, 0.97f, 0.95f), accent = new Color(0.98f, 0.45f, 0.68f) };

    public static void Spawn()
    {
        if (I != null) return;
        Vector2 s = CarrierLayout.Site; float yaw = CarrierLayout.Yaw;
        Vector2 f = CarrierLayout.Fwd;
        BuildSpot(s, yaw);
        Vector3 cp = new Vector3(s.x, Ranch.GY(s.x, s.y) + 0.05f, s.y);
        Trailer tr = TrailerModels.Build(Spec, cp, yaw);
        tr.gameObject.name = "Spotty's Carrier Camper";
        var rig = tr.gameObject.AddComponent<CarrierRig>();
        rig.trailer = tr;
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        // the bike in front of the tongue, hitch ball on the coupler
        Vector3 ball = tr.BallWorld;
        Bike b = Bike.SpawnSpotty(Vector3.zero, yaw);
        Vector3 bp = ball - q * b.HitchPoint; bp.y = Ranch.GY(bp.x, bp.z) + 0.15f;
        b.transform.position = bp; b.rb.position = bp;
        b.Rehome();
        rig.bike = b;
        rig.homePos = tr.transform.position; rig.homeRot = tr.transform.rotation;
        rig.MakeHelm();
        I = rig;
        Debug.Log("Carrier48: Spotty's Carrier Camper + Tourer at " + cp.ToString("0.0") + " yaw " + yaw.ToString("0") + " (forward " + f.ToString("0.00") + ")");
    }

    static void BuildSpot(Vector2 s, float yaw)
    {
        var root = new GameObject("Spotty's Spot").transform;
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        Vector3 c = new Vector3(s.x, Ranch.GY(s.x, s.y), s.y);
        var pad = Mats.Prim(PrimitiveType.Cube, root, c + Vector3.up * 0.03f, new Vector3(9f, 0.06f, 15f), Mats.Lit(new Color(0.62f, 0.58f, 0.52f)));
        pad.transform.rotation = q;
        // sign: post + board with a big paw print (pink on white) and black spots
        Vector3 sp = c + q * new Vector3(-5.6f, 0f, -3f); sp.y = Ranch.GY(sp.x, sp.z);
        Transform sign = Mats.Node(root, "Sign", sp); sign.rotation = q * Quaternion.Euler(0f, 90f, 0f);
        Mats.Prim(PrimitiveType.Cube, sign, new Vector3(0f, 0.9f, 0f), new Vector3(0.12f, 1.8f, 0.12f), Mats.Lit(new Color(0.35f, 0.25f, 0.16f)));
        Mats.Prim(PrimitiveType.Cube, sign, new Vector3(0f, 2.1f, 0f), new Vector3(1.7f, 1.1f, 0.08f), Mats.Paint(new Color(0.97f, 0.97f, 0.95f), 0.4f));
        Material pink = Mats.Paint(new Color(0.98f, 0.45f, 0.68f), 0.5f), blk = Mats.Lit(new Color(0.08f, 0.08f, 0.09f));
        Mats.Prim(PrimitiveType.Cylinder, sign, new Vector3(0f, 1.98f, 0.05f), new Vector3(0.5f, 0.02f, 0.42f), new Vector3(90f, 0f, 0f), pink);
        for (int i = 0; i < 4; i++) { float a = (-55f + i * 36.7f) * Mathf.Deg2Rad; Mats.Prim(PrimitiveType.Cylinder, sign, new Vector3(Mathf.Sin(a) * 0.36f, 1.98f + Mathf.Cos(a) * 0.34f, 0.05f), new Vector3(0.17f, 0.02f, 0.17f), new Vector3(90f, 0f, 0f), pink); }
        Mats.Prim(PrimitiveType.Cylinder, sign, new Vector3(-0.62f, 2.45f, 0.05f), new Vector3(0.18f, 0.02f, 0.14f), new Vector3(90f, 0f, 0f), blk);
        Mats.Prim(PrimitiveType.Cylinder, sign, new Vector3(0.66f, 1.72f, 0.05f), new Vector3(0.14f, 0.02f, 0.12f), new Vector3(90f, 0f, 0f), blk);
        // a food bowl + a fish-shaped doormat by the camper door
        Vector3 bw = c + q * new Vector3(2.3f, 0f, -1.2f); bw.y = Ranch.GY(bw.x, bw.z) + 0.06f;
        Mats.Prim(PrimitiveType.Cylinder, root, bw, new Vector3(0.36f, 0.06f, 0.36f), pink);
        foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void MakeHelm()
    {
        var go = new GameObject("Spotty's Carrier helm");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = HelmL;
        seatNode = Mats.Node(transform, "CarrierSeat", RoofSpot);
        helm = go.AddComponent<CarrierHelm>();
        helm.rig = this;
        helm.Title = "Carrier Camper"; helm.EnterVerb = "take the helm of Spotty's Carrier";
        helm.ownerName = "Spotty"; helm.ownerChar = 4;
        helm.rb = trailer.rb;
        go.layer = Vehicle.VehicleLayer;
        var bc = go.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(1.2f, 1.4f, 1.2f);
        helm.body = bc;
        helm.seat = seatNode; helm.seatScale = 0.75f; helm.showDriver = true;
        helm.camDistance = 24f; helm.camHeight = 8f; helm.engineKind = 11; helm.enginePitch = 0.6f;
        helm.stowed = true;
        helm.Setup(transform.position);
        Vehicle.All.Add(helm);
    }

    public static bool IsSpottyBike(Vehicle v) { Bike b = v as Bike; return b != null && b.spotty; }
    public static bool CanPair(Vehicle v, Trailer t) { if (IsSpottyBike(v)) return I != null && t == I.trailer; return true; }
    public static string TowExtra(Vehicle v) { return IsSpottyBike(v) ? "  |  ride into the pond = CARRIER MODE" : ""; }

    // a frog / the jet standing on the moving deck rides along (translation of the deck point this step)
    public static Vector3 Carry(Vector3 p, float dt)
    {
        if (I == null || !I.toCarrier || I.k < 0.9f || I.trailer == null || I.trailer.rb == null) return Vector3.zero;
        Vector3 l = I.transform.InverseTransformPoint(p);
        bool deck = Mathf.Abs(l.x) < CarrierLayout.HalfW + 0.3f && l.z > CarrierLayout.Z0 - 0.3f && l.z < CarrierLayout.Z1 + 2.5f && l.y > CarrierLayout.DeckY - 0.4f && l.y < CarrierLayout.DeckY + 1.2f;
        bool bridge = Mathf.Abs(l.x - BridgeSpot.x) < 1.6f && Mathf.Abs(l.z - (BridgeSpot.z - 1.2f)) < 2.2f && l.y > BridgeSpot.y - 0.5f && l.y < BridgeSpot.y + 1.2f;
        if (!deck && !bridge) return Vector3.zero;
        return I.trailer.rb.GetPointVelocity(p) * dt;
    }
    // ffu50 (Bill: after E off the carrier the frog kept getting sent one way): a froggy on the deck follows the deck's real
    // motion since the last frame (transform delta of the deck point), not rb velocity x a capped frame dt - at low frame rates
    // (dt above Frog.DtCap) the old way fell behind the moving deck and dragged the froggy towards the stern.
    Matrix4x4 prevW2L; bool prevOk;
    public static Vector3 CarryFrame(Vector3 p)
    {
        if (I == null || !I.toCarrier || I.k < 0.9f || !I.prevOk) return Vector3.zero;
        Vector3 l = I.prevW2L.MultiplyPoint3x4(p);
        bool deck = Mathf.Abs(l.x) < CarrierLayout.HalfW + 0.3f && l.z > CarrierLayout.Z0 - 0.3f && l.z < CarrierLayout.Z1 + 2.5f && l.y > CarrierLayout.DeckY - 0.4f && l.y < CarrierLayout.DeckY + 1.2f;
        bool bridge = Mathf.Abs(l.x - BridgeSpot.x) < 1.6f && Mathf.Abs(l.z - (BridgeSpot.z - 1.2f)) < 2.2f && l.y > BridgeSpot.y - 0.5f && l.y < BridgeSpot.y + 1.2f;
        if (!deck && !bridge) return Vector3.zero;
        Vector3 d = I.transform.TransformPoint(l) - p;
        return d.sqrMagnitude > 25f ? Vector3.zero : d;   // a teleport / reset of the carrier is not a carry
    }
    public static bool OnDeck(Vector3 p)
    {
        if (I == null || !I.Carrier) return false;
        Vector3 l = I.transform.InverseTransformPoint(p);
        return Mathf.Abs(l.x) < CarrierLayout.HalfW + 0.2f && l.z > CarrierLayout.Z0 - 0.2f && l.z < CarrierLayout.Z1 + 2f && l.y > CarrierLayout.DeckY - 0.6f && l.y < CarrierLayout.DeckY + 6f;
    }
    public Vector3 DeckPoint(float x, float z) { return transform.TransformPoint(new Vector3(x, CarrierLayout.DeckY + 0.15f, z)); }
    public Vector3 LandingSpot { get { return DeckPoint(-0.6f, 6.5f); } }
    public Vector3 BridgeWorld { get { return transform.TransformPoint(BridgeSpot); } }

    // ---------------- per step ----------------
    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        if (trailer == null || bike == null) return;
        if (!toCarrier && k <= 0f) { WatchEntry(dt); return; }
        float rate = (toCarrier ? 1f / Tin : 1f / Tout) * speedK;
        float want = toCarrier ? 1f : 0f;
        float nk = Mathf.MoveTowards(k, want, rate * dt);
        if (toCarrier && holdAt >= 0f && k <= holdAt) nk = Mathf.Min(nk, holdAt);
        // the hull grows to 28 m: keep sinking + gliding out until the stern + bow have deep water under them (ffu48b: hold
        // at the end of the sinking beat, before anything unfolds; max 16 s; a last check before the colliders come on)
        if (toCarrier && nk > 0.3f && k <= 0.3f && clearWait < 16f && !RoomToGrow()) { nk = 0.3f; clearWait += dt; holding = true; }
        else if (toCarrier && nk > 0.66f && k <= 0.66f && clearWait < 22f && !RoomToGrow()) { nk = 0.66f; clearWait += dt; holding = true; }
        else holding = false;
        if (!toCarrier) clearWait = 0f;
        k = nk;
        Physics_(dt);
        if (!toCarrier && k <= 0f) Finish();
    }

    void WatchEntry(float dt)
    {
        bike.maxSpeed = trailer.Hitched && trailer.tower == bike ? 16f : 31f;   // towing: a calmer top speed
        // abandoned away from home: back to Spotty's Spot after a minute with nobody near
        idleT += dt;
        if (idleT > 2f)
        {
            float dHome = (trailer.transform.position - homePos).magnitude;
            bool away = dHome > 6f || (bike.transform.position - trailer.transform.position).magnitude > 9f;
            bool empty = bike.driver == null && bike.ghostHook == null && ghost == null;
            float near = Mathf.Min(Sfx.Near(trailer.transform.position), Sfx.Near(bike.transform.position));
            abandonT = away && empty && near > 45f ? abandonT + idleT : 0f;
            idleT = 0f;
            if (abandonT > 60f) { abandonT = 0f; ResetHome(); Debug.Log("Carrier48: rig sent home (abandoned)"); }
        }
        if (!(trailer.Hitched && trailer.tower == bike)) return;
        if (bike.driver == null && bike.ghostHook == null) return;
        if (bike.driver != null && bike.driver.world != WorldId.Ranch) return;
        Vector3 bf = bike.transform.position + bike.transform.forward * 0.8f;
        Vector3 tp = trailer.transform.position;
        float s1, d1, s2, d2;
        if (!Water.At(bf.x, bf.z, out s1, out d1) || !Water.At(tp.x, tp.z, out s2, out d2)) { rearm = true; return; }
        if (!rearm) { if (d1 < 0.5f) rearm = true; return; }   // ffu48d: after a shore revert in deep-ish water, touch the shallows first (no instant re-transform)
        if (d1 > 1.0f && d2 > 0.45f) Begin(true);
    }
    float abandonT, clearWait, stuckT;
    bool rearm = true;
    public bool AfterRevert { get { return !rearm && !toCarrier && k <= 0f; } }   // ffu48d: back to bike + camper in the water, not re-armed yet (a normal sinking bike again)
    bool holding;
    bool RoomToGrow()
    {
        foreach (float z in new[] { -13.5f, -6f, 8f, 16.5f })
            foreach (float x in new[] { -4f, 4f })
            {
                Vector3 p = transform.TransformPoint(new Vector3(x, 0f, z));
                float s, d;
                if (!Water.At(p.x, p.z, out s, out d) || d < 1.8f) return false;
            }
        return true;
    }

    void Physics_(float dt)
    {
        Rigidbody rb = trailer.rb;
        Vector3 pos = rb.position;
        float surf, depth;
        bool water = Water.At(pos.x, pos.z, out surf, out depth);
        surf = Ripples.SurfaceY;
        float b = toCarrier ? Mathf.Clamp01(k / 0.05f) : Mathf.Clamp01(k / 0.2f);
        float WL = WaterLine(k, toCarrier);
        float per = rb.mass * 9.81f / 8f;
        float t = Time.time;
        int wet = 0;
        foreach (float x in FloatX)
            foreach (float z in FZ)
            {
                Vector3 p = transform.TransformPoint(new Vector3(x, 0f, z));
                // a virtual hull: every float point uses the pond surface, also the ones still over the bank (the carrier's
                // colliders stay off until it has risen, so nothing digs into the shore)
                float wy = surf + Water.Wave(p.x, p.z, t);
                float d = wy - p.y;
                if (d <= -0.5f) continue;
                wet++;
                float f = per * Mathf.Clamp(1f + (d - WL) / 0.55f, 0f, 3f);
                Vector3 pv = rb.GetPointVelocity(p);
                rb.AddForceAtPosition(Vector3.up * (f - pv.y * per / 9.81f * 1.8f) * b, p);
            }
        // roll / pitch damping, the deck keeps level
        Vector3 av = rb.angularVelocity;
        Vector3 yawPart = Vector3.up * Vector3.Dot(av, Vector3.up);
        rb.AddTorque(-(av - yawPart) * Mathf.Min(1f, 2.5f * dt) * b, ForceMode.VelocityChange);
        Vector3 up = transform.up;
        Vector3 tilt = Vector3.Cross(up, Vector3.up);
        rb.AddTorque(tilt * 3f * b * (k > 0.5f ? 1f : 0.5f), ForceMode.Acceleration);
        if (wet == 0 && toCarrier && k > 0.3f && !water) { /* beached: nothing to float on */ }

        Vector3 fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
        Vector3 vel = rb.velocity;
        float fs = Vector3.Dot(vel, fwd), ls = Vector3.Dot(vel, right);
        PIn ip = PilotIn();
        bool piloted = helm.driver != null || ghost != null;
        float thr = Mathf.Clamp(ip.move.y + ip.gas - ip.brake, -1f, 1f);
        float steer = Mathf.Clamp(ip.move.x, -1f, 1f);
        if (wet > 0)
        {
            // water drag
            rb.AddForce(-right * ls * 1.6f * b, ForceMode.Acceleration);
            if (Carrier && piloted)
            {
                const float maxS = 12f;
                if (thr > 0.02f) rb.AddForce(fwd * 4.2f * thr * Mathf.Clamp01(1.1f - fs / maxS), ForceMode.Acceleration);
                else if (thr < -0.02f) rb.AddForce(fwd * (fs > 0.5f ? 4f : (fs > -4f ? 2f : 0f)) * thr, ForceMode.Acceleration);
                rb.AddForce(-fwd * fs * (thr > 0.02f ? 0.05f : 0.25f), ForceMode.Acceleration);
                float target = steer * 0.42f * Mathf.Clamp(0.35f + Mathf.Abs(fs) / 5f, 0f, 1f) * (fs < -0.5f ? -1f : 1f);
                float yr = Vector3.Dot(rb.angularVelocity, Vector3.up);
                rb.AddTorque(Vector3.up * (target - yr) * Mathf.Min(1f, 3f * dt), ForceMode.VelocityChange);
                // ffu48b: aground with the throttle on -> push off toward open water (and give up into bike + camper if
                // it is really high and dry)
                Vector3 hv = vel; hv.y = 0f;
                stuckT = Mathf.Abs(thr) > 0.3f && hv.magnitude < 0.5f ? stuckT + dt : 0f;
                if (stuckT > 1.2f)
                {
                    Vector3 open = new Vector3(Layout.PondC.x - pos.x, 0f, Layout.PondC.y - pos.z).normalized;
                    rb.AddForce((open * 2f - hv) * Mathf.Min(1f, 3f * dt) + Vector3.up * 0.05f, ForceMode.VelocityChange);
                }
                if (stuckT > 6f && depth < 1.6f && !JetParked()) { stuckT = 0f; Begin(false); }
            }
            else if (Carrier)
            {
                // nobody at the helm: it drifts to a stop (sea anchor)
                rb.AddForce(-fwd * fs * 0.6f, ForceMode.Acceleration);
                float yr = Vector3.Dot(rb.angularVelocity, Vector3.up);
                rb.AddTorque(-Vector3.up * yr * Mathf.Min(1f, 2f * dt), ForceMode.VelocityChange);
            }
            else
            {
                // transforming: the rig keeps gliding gently out (into) the water it was driven into
                // ffu48b: a velocity target (VelocityChange) - the ffu48a acceleration lost to the camper's friction on the bed
                float want = toCarrier ? (k < 0.75f ? (holding || !RoomToGrow() ? 3.2f : 1.6f) : 0f) : 2.2f;
                Vector3 hv = vel; hv.y = 0f;
                rb.AddForce((fwd * want - hv) * Mathf.Min(1f, 6f * dt), ForceMode.VelocityChange);
                float yr = Vector3.Dot(rb.angularVelocity, Vector3.up);
                rb.AddTorque(-Vector3.up * yr * Mathf.Min(1f, 2f * dt), ForceMode.VelocityChange);
            }
        }
        // shore ahead -> back to bike + camper
        if (Carrier && piloted && fs > 0.8f)
        {
            Vector3 ahead = pos + fwd * 17f;
            float s3, d3;
            bool wa = Water.At(ahead.x, ahead.z, out s3, out d3);
            if (!wa || d3 < 1.4f)
            {
                if (JetParked()) { if (sayCool <= 0f && helm.driver != null && helm.driver.human) { helm.driver.Toast("James's jet is parked on the deck - fly it off first!", 2.5f); sayCool = 4f; } }
                else Begin(false);
            }
        }
        if (!water && Carrier && depth <= 0f && !JetParked()) Begin(false);
        sayCool -= dt;
        Wake(fs, vel);
    }

    PIn PilotIn()
    {
        if (helm.driver != null) return helm.demoHook != null ? helm.demoHook(helm.In) : helm.In;
        if (ghost != null) return ghost(new PIn());
        return new PIn();
    }

    // camper-local waterline height (how deep the float points sit)
    static float WaterLine(float k, bool fwd)
    {
        if (fwd)
        {
            if (k < 0.3f) return Mathf.Lerp(1.1f, 2.45f, S(0.0f, 0.3f, k));
            if (k < 0.72f) return 2.45f;
            return Mathf.Lerp(2.45f, 0.4f, S(0.72f, 0.95f, k));
        }
        return Mathf.Lerp(1.3f, 0.4f, S(0.45f, 1f, k));
    }
    static float S(float a, float b, float x) { return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((x - a) / (b - a))); }

    void Wake(float fs, Vector3 vel)
    {
        if (!WaveSim.Active || k < 0.05f) return;
        float sp = Mathf.Min(Mathf.Abs(fs), 15f);
        Vector3 f = transform.forward; f.y = 0f; f.Normalize();
        float r = 1.2f + 3.2f * S(0.3f, 0.75f, k);
        float depth = Mathf.Min((0.03f + 0.02f * sp) * Mathf.Sqrt(r), 0.32f);
        float foam = Mathf.Clamp01(0.35f + sp * 0.05f);
        int id = GetHashCode() * 2 + 7;
        float wy = Ripples.SurfaceY;
        Vector3 c = transform.position; c.y = wy;
        float half = 2.5f + 9f * S(0.3f, 0.75f, k);
        Ripples.Press(id, c + f * half, vel, r, depth, foam, r * 1.15f, 0.5f);
        Ripples.Press(id + 1, c - f * half, vel, r, depth * 0.8f, foam, r * 1.15f, 0.5f);
    }

    public bool JetParked()
    {
        Jet j = Jet.I;
        if (j == null || !j.onGround) return false;
        Vector3 l = transform.InverseTransformPoint(j.transform.position);
        return Mathf.Abs(l.x) < CarrierLayout.HalfW + 2f && l.z > CarrierLayout.Z0 - 2f && l.z < CarrierLayout.Z1 + 3f && l.y > CarrierLayout.DeckY - 1f && l.y < CarrierLayout.DeckY + 2f;
    }

    // ---------------- begin / finish ----------------
    public void Begin(bool to)
    {
        if (to == toCarrier) return;
        if (!built) Build();
        Rigidbody rb = trailer.rb;
        if (to)
        {
            Frog pilot = bike.driver;
            if (trailer.Hitched) trailer.Detach("carrier mode", false);
            trailer.carrierMode = true;
            mass0 = rb.mass; com0 = rb.centerOfMass; inert0 = rb.inertiaTensor; inertR0 = rb.inertiaTensorRotation;
            rb.mass = 5000f;
            rb.centerOfMass = new Vector3(0f, 0.6f, 0.5f);
            float L = 28f, W = 9f, H = 5f;
            rb.inertiaTensor = new Vector3(rb.mass / 12f * (H * H + L * L), rb.mass / 12f * (W * W + L * L), rb.mass / 12f * (W * W + H * H));
            rb.inertiaTensorRotation = Quaternion.identity;
            rb.angularDrag = 1.5f;
            foreach (var w in trailer.wheels) if (w.vis != null) w.vis.gameObject.SetActive(false);
            Vector3 seatW = bike.seat != null ? bike.seat.position : bike.transform.position + Vector3.up;
            if (pilot != null) pilot.ExitVehicle();
            FreezeBike(true);
            bikeStartL = transform.InverseTransformPoint(bike.transform.position);
            bikeStartR = Quaternion.Inverse(transform.rotation) * bike.transform.rotation;
            seatStartL = transform.InverseTransformPoint(seatW);
            seatNode.localPosition = seatStartL; seatNode.localRotation = Quaternion.identity;
            if (ghost == null && bike.ghostHook != null) ghost = bike.ghostHook;
            helm.stowed = false;
            if (pilot != null) { string o = helm.ownerName; helm.ownerName = null; pilot.EnterVehicle(helm); helm.ownerName = o; }
            if (npc != null) { npc.transform.SetParent(seatNode, false); npc.transform.localPosition = Vector3.zero; npc.transform.localRotation = Quaternion.identity; npc.transform.localScale = Vector3.one * 0.75f; }
            Transforms++;
            Sfx.PlayAt(Sfx.SplashBig != null ? Sfx.SplashBig : Sfx.Splash, transform.position, 0.8f, 80f, 0.9f);
            FX.Splash(bike.transform.position + Vector3.up * 0.3f, 18);
        }
        else
        {
            if (helm.driver != null && helm.driver.human) helm.driver.Toast("Shore ahead - back to bike + camper!", 2f);
        }
        toCarrier = to;
        Debug.Log("Carrier48: " + (to ? "-> CARRIER" : "-> bike + camper") + " at " + transform.position.ToString("0") + " k=" + k.ToString("0.00") + " t=" + Time.realtimeSinceStartup.ToString("0.0"));
    }

    void Finish()
    {
        Rigidbody rb = trailer.rb;
        Vector3 v = rb.velocity;
        trailer.carrierMode = false;
        rb.mass = mass0 > 0f ? mass0 : Spec.mass; rb.centerOfMass = com0; rb.inertiaTensor = inert0; rb.inertiaTensorRotation = inertR0; rb.angularDrag = 0.6f;
        foreach (var w in trailer.wheels) if (w.vis != null) w.vis.gameObject.SetActive(true);
        foreach (var c in deckCols) if (c != null) c.enabled = false;
        // level the camper, bike in front of the tongue on the bed / bank
        float yaw = transform.eulerAngles.y;
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        rb.rotation = q; transform.rotation = q;
        rb.angularVelocity = Vector3.zero;
        Vector3 ball = trailer.BallWorld;
        Vector3 bp = ball - q * bike.HitchPoint;
        RaycastHit h;
        if (Physics.Raycast(bp + Vector3.up * 6f, Vector3.down, out h, 40f, Trailer.WheelMask, QueryTriggerInteraction.Ignore)) bp.y = h.point.y + 0.12f; else bp.y = Ranch.GY(bp.x, bp.z) + 0.12f;
        FreezeBike(false);
        bike.transform.SetPositionAndRotation(bp, q); bike.rb.position = bp; bike.rb.rotation = q;
        Vector3 hv = v; hv.y = 0f;
        bike.rb.velocity = hv; rb.velocity = hv;
        Physics.SyncTransforms();
        trailer.Hitch(bike);
        Frog pilot = helm.driver;
        if (pilot != null) { pilot.ExitVehicle(); string o = bike.ownerName; bike.ownerName = null; pilot.EnterVehicle(bike); bike.ownerName = o; }
        if (npc != null) { npc.transform.SetParent(bike.seat, false); npc.transform.localPosition = Vector3.zero; npc.transform.localRotation = Quaternion.identity; npc.transform.localScale = Vector3.one * 0.66f; }
        if (ghost != null && bike.ghostHook == null) bike.ghostHook = ghost;
        helm.stowed = true;
        seatNode.localPosition = RoofSpot;
        if (parts != null) parts.SetActive(false);
        rearm = false;
        Debug.Log("Carrier48: back to bike + camper at " + transform.position.ToString("0"));
    }

    void FreezeBike(bool on)
    {
        if (on)
        {
            bike.carried = true;
            if (!bike.rb.isKinematic) { bike.rb.velocity = Vector3.zero; bike.rb.angularVelocity = Vector3.zero; }
            bike.rb.interpolation = RigidbodyInterpolation.None;
            bike.rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            bike.rb.isKinematic = true;
            bikeCols.Clear();
            foreach (var c in bike.GetComponentsInChildren<Collider>())
                if (c != null && c.enabled && !(c is CharacterController) && c.GetComponentInParent<Frog>() == null) { c.enabled = false; bikeCols.Add(c); }
            bike.transform.SetParent(transform, true);
        }
        else
        {
            bike.transform.SetParent(null, true);
            foreach (var c in bikeCols) if (c != null) c.enabled = true;
            bikeCols.Clear();
            bike.rb.isKinematic = false;
            bike.rb.interpolation = RigidbodyInterpolation.Interpolate;
            bike.rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            bike.carried = false;
        }
    }

    // story / skip: snap straight to the finished carrier floating at p (world, y ignored), heading yaw
    public void ForceCarrier(Vector3 p, float yaw)
    {
        if (!toCarrier) Begin(true);
        holdAt = -1f; k = 1f;
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        p.y = Ripples.SurfaceY - 0.4f;
        Rigidbody rb = trailer.rb;
        rb.position = p; rb.rotation = q; transform.SetPositionAndRotation(p, q);
        rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
        Physics.SyncTransforms();
        Debug.Log("Carrier48: forced carrier at " + p.ToString("0"));
    }

    // everything back on Spotty's Spot: camper parked, bike hitched in front
    public void ResetHome()
    {
        if (toCarrier || k > 0f)
        {
            Frog pl = helm.driver; if (pl != null) pl.ExitVehicle();
            toCarrier = false; k = 0f; holdAt = -1f;
            Finish();
        }
        if (bike.driver != null) bike.driver.ExitVehicle();
        if (trailer.Hitched) trailer.Detach("sent home", false);
        trailer.GoHome();
        Quaternion q = homeRot;
        Vector3 bp = trailer.BallWorld - q * bike.HitchPoint; bp.y = Ranch.GY(bp.x, bp.z) + 0.15f;
        bike.rb.velocity = Vector3.zero; bike.rb.angularVelocity = Vector3.zero;
        bike.rb.position = bp; bike.rb.rotation = q; bike.transform.SetPositionAndRotation(bp, q);
        Physics.SyncTransforms();
        trailer.Hitch(bike);
        if (npc != null) { npc.transform.SetParent(bike.seat, false); npc.transform.localPosition = Vector3.zero; npc.transform.localRotation = Quaternion.identity; npc.transform.localScale = Vector3.one * 0.66f; }
        rearm = true;   // ffu48d: parked on Spotty's Spot (dry) = armed
    }

    // ---------------- visuals ----------------
    void Update()
    {
        if (trailer == null) return;
        // the very first hitch (both have settled on the ground by then)
        if (!firstHitch) { firstT += Time.deltaTime; if (firstT > 1.5f) { firstHitch = true; if (!trailer.Hitched && bike != null) { trailer.Hitch(bike); Debug.Log("Carrier48: rig hitched at Spotty's Spot"); } } }
        float dt = Time.deltaTime;
        if (npc != null) NpcPose(dt);
        if (!built) return;
        bool show = k > 0.001f;
        if (parts.activeSelf != show) parts.SetActive(show);
        Cue(prevK, k);
        prevK = k;
        foreach (var c in deckCols) if (c != null) c.enabled = toCarrier ? k > 0.9f : k > 0.95f;
        if (!show) return;
        if (bike != null && bike.carried) PoseBike();
        PoseSeat();
        // parts
        float h = S(0.30f, 0.65f, k);
        for (int i = 0; i < 2; i++)
        {
            Transform t = i == 0 ? hullL : hullR; float sx = i == 0 ? -1f : 1f;
            t.localPosition = new Vector3(sx * Mathf.Lerp(0.5f, 2.9f, h), Mathf.Lerp(0.9f, 1.0f, h), Mathf.Lerp(0f, 0.75f, h));
            t.localScale = new Vector3(Mathf.Lerp(0.2f, 1f, h), Mathf.Lerp(0.25f, 1f, h), Mathf.Lerp(0.18f, 1f, h));
        }
        float bs = S(0.40f, 0.72f, k);
        bow.localPosition = new Vector3(0f, 1.0f, Mathf.Lerp(2.4f, 14.6f, bs)); bow.localScale = new Vector3(Mathf.Lerp(0.3f, 1f, bs), Mathf.Lerp(0.3f, 1f, bs), 1f);
        stern.localPosition = new Vector3(0f, 1.0f, Mathf.Lerp(-2.4f, -12.9f, bs)); stern.localScale = new Vector3(Mathf.Lerp(0.3f, 1f, bs), Mathf.Lerp(0.3f, 1f, bs), 1f);
        float dr = S(0.45f, 0.70f, k), dt2 = S(0.50f, 0.80f, k), dw = S(0.55f, 0.85f, k);
        float dy = Mathf.Lerp(2.7f, CarrierLayout.DeckY, dr);
        deckC.localPosition = new Vector3(0f, dy, 0f); deckC.localScale = new Vector3(Mathf.Lerp(0.24f, 1f, dw), 1f, Mathf.Lerp(0.6f, 1f, dr));
        deckF.localPosition = new Vector3(0f, dy, Mathf.Lerp(0.5f, 4f, dt2)); deckF.localScale = new Vector3(Mathf.Lerp(0.24f, 1f, dw), 1f, Mathf.Lerp(0.05f, 1f, dt2));
        deckA.localPosition = new Vector3(0f, dy, Mathf.Lerp(-0.5f, -4f, dt2)); deckA.localScale = new Vector3(Mathf.Lerp(0.24f, 1f, dw), 1f, Mathf.Lerp(0.05f, 1f, dt2));
        float il = S(0.58f, 0.86f, k);
        island.localPosition = new Vector3(Mathf.Lerp(1.2f, 3.3f, S(0.5f, 0.62f, k)), Mathf.Lerp(CarrierLayout.DeckY - 4.1f, CarrierLayout.DeckY, il), -0.5f);
        island.localScale = new Vector3(1f, Mathf.Lerp(0.15f, 1f, il), 1f);
        if (radar != null) radar.localRotation = Quaternion.Euler(0f, Time.time * 90f, 0f);
        if (podRing != null) podRing.localRotation = Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(0f, Time.time * (Carrier ? 400f + trailer.rb.velocity.magnitude * 80f : 120f), 0f);
        // lights: cyan strobe while transforming, steady pink-white deck lights in carrier mode
        bool moving = k > 0.001f && k < 0.999f;
        float led = moving ? (Mathf.Repeat(Time.time * 6f, 1f) < 0.5f ? 1f : 0.2f) : 0.6f + 0.3f * Mathf.Sin(Time.time * 2.5f);
        Color lc = new Color(0.35f, 0.95f, 1f) * led; lc.a = 1f; ledMat.color = lc;
        Color dc = Color.Lerp(new Color(0.15f, 0.15f, 0.12f), new Color(1f, 0.95f, 0.75f), S(0.9f, 1f, k) * (0.75f + 0.25f * Mathf.Sin(Time.time * 4f))); dc.a = 1f; deckLightMat.color = dc;
        if (landRing != null) landRing.gameObject.SetActive(k > 0.8f);
        Effects(dt);
    }
    bool firstHitch; float firstT;

    void NpcPose(float dt)
    {
        if (npc == null) return;
        bool onBike = bike != null && npc.transform.parent == bike.seat;
        npc.Animate(0f, false, onBike, false, dt);
        if (onBike)
        {
            if (npc.legL != null) npc.legL.localRotation = Quaternion.Euler(-38f, 0f, -8f);
            if (npc.legR != null) npc.legR.localRotation = Quaternion.Euler(-38f, 0f, 8f);
            if (npc.armL != null) npc.armL.localRotation = Quaternion.Euler(-72f, 0f, 0f);
            if (npc.armR != null) npc.armR.localRotation = Quaternion.Euler(-72f, 0f, 0f);
        }
        else if (Carrier || npcWave)
        {
            if (npc.armR != null) npc.armR.localRotation = Quaternion.Euler(-160f + Mathf.Sin(Time.time * 7f) * 22f, 0f, 0f);
        }
    }
    public bool npcWave;

    void PoseBike()
    {
        float tip = S(0.0f, 0.12f, k), slide = S(0.40f, 0.72f, k);
        Quaternion side = bikeStartR * Quaternion.Euler(0f, 0f, 90f * tip);
        Vector3 p = bikeStartL + Vector3.down * 0.3f * tip;
        Vector3 lp = Vector3.Lerp(p, PodL, slide);
        Quaternion lr = Quaternion.Slerp(side, Quaternion.Euler(0f, 0f, 90f), slide);
        bike.transform.localPosition = lp; bike.transform.localRotation = lr;
    }

    void PoseSeat()
    {
        float hop = S(0.0f, 0.12f, k), up = S(0.58f, 0.86f, k);
        Vector3 a = Vector3.Lerp(seatStartL, RoofSpot, hop) + Vector3.up * Mathf.Sin(hop * Mathf.PI) * 1.2f;
        Vector3 p = toCarrier ? Vector3.Lerp(a, BridgeSpot, up) : Vector3.Lerp(Vector3.Lerp(seatStartL, RoofSpot, S(0.0f, 0.15f, k)), BridgeSpot, S(0.5f, 0.86f, k));
        seatNode.localPosition = p;
        seatNode.localRotation = Quaternion.identity;
    }

    void Effects(float dt)
    {
        Vector3 c = transform.position;
        float wy = Ripples.SurfaceY;
        // bubbles while it sinks / transforms under water
        if (toCarrier && k > 0.08f && k < 0.75f)
        {
            bubT += dt * (k < 0.3f ? 26f : 40f) * (Look.Mobile ? 0.5f : 1f);
            while (bubT >= 1f)
            {
                bubT -= 1f;
                Vector3 p = transform.TransformPoint(new Vector3(Random.Range(-3f, 3f), Random.Range(0.2f, 2f), Random.Range(-6f, 6f)));
                if (p.y > wy) p.y = wy - 0.2f;
                FX.Bubble(p, Random.Range(2, 5));
                if (Random.value < 0.35f) { Vector3 s = p; s.y = wy + 0.05f; FX.Spray(s, Vector3.up * Random.Range(0.8f, 2.2f) + Random.insideUnitSphere * 0.6f, Random.Range(0.15f, 0.35f), 0.6f, new Color(0.88f, 0.96f, 1f, 0.8f)); }
            }
        }
        // the rise: water pours off the deck edges + a ring of spray
        if (toCarrier && k > 0.72f && k < 0.99f)
        {
            sprayT += dt * 60f * (Look.Mobile ? 0.5f : 1f);
            while (sprayT >= 1f)
            {
                sprayT -= 1f;
                float sx = Random.value < 0.5f ? -1f : 1f;
                Vector3 p = transform.TransformPoint(new Vector3(sx * CarrierLayout.HalfW, CarrierLayout.DeckY - 0.1f, Random.Range(CarrierLayout.Z0, CarrierLayout.Z1)));
                Vector3 v = transform.right * sx * Random.Range(0.6f, 1.6f) + Vector3.down * Random.Range(0.5f, 1.5f);
                FX.Spray(p, v, Random.Range(0.25f, 0.5f), Random.Range(0.6f, 1f), new Color(0.9f, 0.97f, 1f, 0.85f));
                if (Random.value < 0.25f) { Vector3 q = p; q.y = wy + 0.1f; FX.Spray(q, Vector3.up * Random.Range(2f, 4.5f) + transform.right * sx * 2f, Random.Range(0.3f, 0.6f), 0.8f, new Color(0.92f, 0.98f, 1f, 0.8f)); }
            }
        }
        // carrier underway: bow spray + the stern pod's churn
        if (Carrier)
        {
            float sp = trailer.rb.velocity.magnitude;
            if (sp > 1.5f && Random.value < dt * (6f + sp * 2f))
            {
                Vector3 bp = transform.TransformPoint(new Vector3(Random.Range(-1.5f, 1.5f), 0f, 16f)); bp.y = wy + 0.1f;
                FX.Spray(bp, transform.right * Random.Range(-3f, 3f) + Vector3.up * Random.Range(1.5f, 3f), Random.Range(0.25f, 0.5f), 0.7f, new Color(0.9f, 0.97f, 1f, 0.8f));
            }
            PIn ip = PilotIn();
            float thr = Mathf.Abs(ip.move.y + ip.gas - ip.brake);
            if (thr > 0.05f && Random.value < dt * 20f)
            {
                Vector3 jp = transform.TransformPoint(new Vector3(Random.Range(-2.6f, 2.6f), 0f, -13.6f)); jp.y = wy + 0.05f;
                FX.Spray(jp, -transform.forward * Random.Range(3f, 6f) + Vector3.up * Random.Range(1f, 2.5f), Random.Range(0.3f, 0.55f), 0.8f, new Color(0.93f, 0.98f, 1f, 0.75f));
            }
        }
    }

    void Cue(float a, float b)
    {
        if (a == b) return;
        cueCool -= Time.deltaTime;
        System.Func<float, bool> crossed = x => (a < x && b >= x) || (a > x && b <= x);
        Vector3 p = transform.position;
        if (Sfx.Near(p) > 120f) return;
        if (crossed(0.04f)) Sfx.PlayAt(Sfx.Pick(Sfx.Servo), p, 0.55f, 70f, 0.8f);
        if (crossed(0.12f) && Sfx.Clank != null) Sfx.PlayAt(Sfx.Clank, p, 0.6f, 70f, 0.75f);
        if (crossed(0.2f)) { Sfx.PlayAt(Sfx.Pick(Sfx.Servo), p, 0.6f, 80f, 0.45f); if (Sfx.Bubbles != null) Sfx.PlayAt(Sfx.Bubbles, p, 0.7f, 70f, 0.8f); }
        if (crossed(0.35f)) Sfx.PlayAt(Rumble(), p, 0.8f, 90f, 1f);
        if (crossed(0.45f)) Sfx.PlayAt(Sfx.Pick(Sfx.Servo), p, 0.55f, 70f, 1.1f);
        if (crossed(0.6f) && Sfx.Clank != null) Sfx.PlayAt(Sfx.Clank, p, 0.65f, 70f, 0.9f);
        if (crossed(0.7f)) Sfx.PlayAt(Sfx.Pick(Sfx.Servo), p, 0.6f, 70f, 1.25f);
        if (crossed(0.74f) && toCarrier)
        {
            Sfx.PlayAt(Sfx.SplashBig != null ? Sfx.SplashBig : Sfx.Splash, p, 1f, 100f, 0.8f);
            Sfx.PlayAt(Rumble(), p, 0.9f, 100f, 1.2f);
            for (int i = 0; i < 6; i++) global::FX.Splash(transform.TransformPoint(new Vector3(Random.Range(-4f, 4f), 0f, Random.Range(-12f, 14f))) + Vector3.up * 0.2f, 26);
            Ripples.Swell(p, 14f, 0.12f, 6f);
        }
        if (crossed(0.86f) && Sfx.Clank != null) Sfx.PlayAt(Sfx.Clank, p, 0.65f, 70f, 1.05f);
        if (b >= 0.999f && a < 0.999f)
        {
            if (Sfx.Confirm != null) Sfx.PlayAt(Sfx.Confirm, p, 0.4f, 70f, 1f);
            Sfx.PlayAt(Horn(), p, 0.9f, 140f, 1f);
            if (helm.driver != null && helm.driver.human) helm.driver.Toast("CARRIER MODE!", 2.4f);
            Debug.Log("Carrier48: CARRIER MODE at " + p.ToString("0") + " t=" + Time.realtimeSinceStartup.ToString("0.0"));
        }
    }

    static AudioClip Rumble()
    {
        if (rumble != null) return rumble;
        const int rate = 22050; int n = (int)(rate * 1.6f);
        var d = new float[n]; var r = new System.Random(9); float lp = 0f, lp2 = 0f, ph = 0f;
        for (int i = 0; i < n; i++)
        {
            float x = i / (float)n, env = Mathf.Sin(Mathf.PI * Mathf.Pow(x, 0.5f)) * (1f - x * 0.5f);
            float nz = (float)(r.NextDouble() * 2.0 - 1.0);
            lp += (nz - lp) * 0.05f; lp2 += (lp - lp2) * 0.05f;
            ph += (38f + 18f * x) / rate;
            d[i] = Mathf.Clamp((lp2 * 4f + Mathf.Sin(ph * 6.2832f) * 0.4f) * env, -1f, 1f) * 0.85f;
        }
        rumble = AudioClip.Create("carrier_rumble", n, 1, rate, false); rumble.SetData(d, 0);
        return rumble;
    }
    static AudioClip Horn()
    {
        if (horn != null) return horn;
        const int rate = 22050; int n = (int)(rate * 1.5f);
        var d = new float[n]; float p1 = 0f, p2 = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float x = i / (float)n, env = Mathf.Clamp01(x / 0.06f) * Mathf.Clamp01((1f - x) / 0.25f);
            p1 += 110f / rate; p2 += 165f / rate;
            float s = (Mathf.Repeat(p1, 1f) * 2f - 1f) * 0.5f + (Mathf.Repeat(p2, 1f) * 2f - 1f) * 0.4f;
            lp += (s - lp) * 0.12f;
            d[i] = Mathf.Clamp(lp * 1.6f * env, -1f, 1f) * 0.8f;
        }
        horn = AudioClip.Create("carrier_horn", n, 1, rate, false); horn.SetData(d, 0);
        return horn;
    }

    // ---------------- the carrier parts (built on the first transform) ----------------
    void Build()
    {
        built = true;
        parts = new GameObject("CarrierParts");
        parts.transform.SetParent(transform, false);
        Transform R = parts.transform;
        Material white = Mats.Paint(new Color(0.96f, 0.96f, 0.94f), 0.55f), navy = Mats.PBR(new Color(0.12f, 0.16f, 0.24f), 0.45f, 0.2f);
        Material spot = Mats.Lit(new Color(0.07f, 0.07f, 0.08f)), pink = Mats.Paint(new Color(0.98f, 0.45f, 0.68f), 0.6f), grey = Mats.PBR(new Color(0.34f, 0.36f, 0.38f), 0.25f, 0.1f);
        Material deckM = Mats.PBR(new Color(0.24f, 0.26f, 0.28f), 0.15f, 0.05f), yellow = Mats.Unlit(new Color(1f, 0.82f, 0.15f)), lineW = Mats.Unlit(new Color(0.95f, 0.95f, 0.92f));
        Material steel = Mats.Steel(new Color(0.75f, 0.77f, 0.8f)), glass = Mats.GlassTint(new Color(0.1f, 0.2f, 0.3f)), warm = Mats.Unlit(new Color(1f, 0.85f, 0.55f));
        ledMat = new Material(Mats.Unlit(new Color(0.35f, 0.95f, 1f)));
        deckLightMat = new Material(Mats.Unlit(new Color(1f, 0.95f, 0.75f)));
        glowMat = new Material(Mats.Unlit(new Color(0.4f, 0.9f, 1f)));
        float DY = CarrierLayout.DeckY;
        // hull halves (built at final size around their node; node at (+-2.9, 1.0, 0.75))
        for (int i = 0; i < 2; i++)
        {
            float sx = i == 0 ? -1f : 1f;
            Transform n = Mats.Node(R, i == 0 ? "HullL" : "HullR", new Vector3(sx * 2.9f, 1f, 0.75f));
            Mats.Prim(PrimitiveType.Cube, n, new Vector3(0f, -1.1f, 0f), new Vector3(3.2f, 1.6f, 25.5f), navy);                 // below the waterline
            Mats.Prim(PrimitiveType.Cube, n, new Vector3(sx * 0.15f, 1.05f, 0f), new Vector3(2.9f, 2.7f, 25.5f), white);         // topsides
            Mats.Prim(PrimitiveType.Cube, n, new Vector3(sx * 1.47f, -0.3f, 0f), new Vector3(0.06f, 0.18f, 25.6f), pink);        // boot stripe
            Mats.Prim(PrimitiveType.Cube, n, new Vector3(sx * 1.48f, 2.25f, 0f), new Vector3(0.05f, 0.08f, 25.6f), ledMat);      // LED strip
            // Spotty's spots + warm portholes (the camper cabin inside)
            var rnd = new System.Random(11 + i);
            for (int s = 0; s < 9; s++)
            {
                float z = -11f + s * 2.8f + (float)rnd.NextDouble() * 1.2f, y = 0.4f + (float)rnd.NextDouble() * 1.6f, r = 0.45f + (float)rnd.NextDouble() * 0.6f;
                Mats.Prim(PrimitiveType.Cylinder, n, new Vector3(sx * 1.47f, y, z), new Vector3(r, 0.02f, r * 0.8f), new Vector3(0f, 0f, 90f), spot);
            }
            for (int s = 0; s < 6; s++) Mats.Prim(PrimitiveType.Cylinder, n, new Vector3(sx * 1.49f, 1.55f, -7.5f + s * 3f), new Vector3(0.45f, 0.02f, 0.45f), new Vector3(0f, 0f, 90f), warm);
            if (i == 0) hullL = n; else hullR = n;
        }
        // bow (a wedge) + stern transom with the bike pod
        bow = Mats.Node(R, "Bow", new Vector3(0f, 1f, 14.6f));
        var bm = Wedge(9f, 4.3f, 3.6f);
        var bg = new GameObject("BowMesh"); bg.transform.SetParent(bow, false); bg.transform.localPosition = new Vector3(0f, -1.9f, -0.4f);
        bg.AddComponent<MeshFilter>().sharedMesh = bm; bg.AddComponent<MeshRenderer>().sharedMaterial = white;
        Mats.Prim(PrimitiveType.Cube, bow, new Vector3(0f, -1.55f, 0.4f), new Vector3(1.2f, 1.0f, 2.2f), navy);
        for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Sphere, bow, new Vector3(s * 2.2f, 1.9f, 0.3f), Vector3.one * 0.22f, Mats.Unlit(s < 0 ? new Color(1f, 0.15f, 0.1f) : new Color(0.15f, 1f, 0.3f)));
        Mats.Prim(PrimitiveType.Cylinder, bow, new Vector3(1.3f, 0.9f, 0.9f), new Vector3(0.5f, 0.06f, 0.5f), new Vector3(0f, 0f, 90f), steel);   // anchor hawse
        stern = Mats.Node(R, "Stern", new Vector3(0f, 1f, -12.9f));
        Mats.Prim(PrimitiveType.Cube, stern, new Vector3(0f, 0.9f, 0f), new Vector3(9f, 2.8f, 0.5f), white);
        Mats.Prim(PrimitiveType.Cube, stern, new Vector3(0f, -1.1f, 0f), new Vector3(9f, 1.5f, 0.5f), navy);
        for (int s = -1; s <= 1; s += 2)
        {
            Mats.Prim(PrimitiveType.Cylinder, stern, new Vector3(s * 2.6f, -0.85f, -0.35f), new Vector3(0.8f, 0.3f, 0.8f), new Vector3(90f, 0f, 0f), grey);
            Mats.Prim(PrimitiveType.Cylinder, stern, new Vector3(s * 2.6f, -0.85f, -0.52f), new Vector3(0.55f, 0.02f, 0.55f), new Vector3(90f, 0f, 0f), glowMat);
        }
        Mats.Prim(PrimitiveType.Cube, stern, new Vector3(0f, 1.9f, -0.27f), new Vector3(3f, 0.5f, 0.03f), pink);   // name board
        // the bike pod: a cradle + a spinning turbine ring round the bike's rear wheel (the bike lies here on its side)
        Mats.Prim(PrimitiveType.Cube, R, PodL + new Vector3(0.75f, -0.45f, 0.1f), new Vector3(2.2f, 0.15f, 2.6f), grey);
        podRing = Mats.Node(R, "PodRing", PodL + new Vector3(0.68f, 0f, -0.75f));
        for (int s = 0; s < 6; s++) { var bl = Mats.Prim(PrimitiveType.Cube, podRing, Quaternion.Euler(0f, s * 60f, 0f) * new Vector3(0f, 0f, 0.48f), new Vector3(0.08f, 0.04f, 0.4f), glowMat); bl.transform.localRotation = Quaternion.Euler(0f, s * 60f, 25f); }
        // flight deck: centre (over the camper), fore + aft panels
        deckC = DeckPanel(R, "DeckC", -4f, 4f, deckM, lineW, yellow, false);
        deckF = DeckPanel(R, "DeckF", 0f, 10f, deckM, lineW, yellow, true);
        deckA = DeckPanel(R, "DeckA", -8.5f, 0f, deckM, lineW, yellow, false);
        // landing ring + a pink paw print (Spotty's mark) on the fore deck
        landRing = Mats.Node(deckF, "LandRing", new Vector3(-0.6f, 0.02f, 2.5f));
        for (int s = 0; s < 20; s++) { float a = s * 18f * Mathf.Deg2Rad; var seg = Mats.Prim(PrimitiveType.Cube, landRing, new Vector3(Mathf.Sin(a) * 3.2f, 0f, Mathf.Cos(a) * 3.2f), new Vector3(0.22f, 0.02f, 0.9f), yellow); seg.transform.localRotation = Quaternion.Euler(0f, s * 18f, 0f); }
        Mats.Prim(PrimitiveType.Cylinder, landRing, Vector3.zero, new Vector3(1.6f, 0.012f, 1.35f), pink);
        for (int s = 0; s < 4; s++) { float a = (-55f + s * 36.7f) * Mathf.Deg2Rad; Mats.Prim(PrimitiveType.Cylinder, landRing, new Vector3(Mathf.Sin(a) * 1.15f, 0f, Mathf.Cos(a) * 1.1f), new Vector3(0.55f, 0.012f, 0.55f), pink); }
        // island tower + flying bridge (starboard)
        island = Mats.Node(R, "Island", new Vector3(3.3f, DY, -0.5f));
        Mats.Prim(PrimitiveType.Cube, island, new Vector3(0f, 2f, 0f), new Vector3(2f, 4f, 5f), white);
        Mats.Prim(PrimitiveType.Cube, island, new Vector3(0f, 3.25f, 0.2f), new Vector3(2.06f, 0.75f, 4.7f), glass);   // bridge windows
        Mats.Prim(PrimitiveType.Cube, island, new Vector3(-1.02f, 1.0f, 1.2f), new Vector3(0.05f, 1.9f, 0.9f), pink);    // door
        for (int s = 0; s < 3; s++) Mats.Prim(PrimitiveType.Cylinder, island, new Vector3(-1.02f, 1.6f + s * 0.0f, -1.6f + s * 1.0f), new Vector3(0.35f, 0.02f, 0.35f), new Vector3(0f, 0f, 90f), spot);
        Mats.Prim(PrimitiveType.Cube, island, new Vector3(0f, 4.02f, 0.2f), new Vector3(2.6f, 0.08f, 3.4f), deckM);     // bridge floor
        for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, island, new Vector3(s * 1.28f, 4.45f, 0.2f), new Vector3(0.05f, 0.8f, 3.4f), steel);
        Mats.Prim(PrimitiveType.Cube, island, new Vector3(0f, 4.45f, 1.88f), new Vector3(2.6f, 0.8f, 0.05f), steel);
        Mats.Prim(PrimitiveType.Cube, island, new Vector3(0f, 4.4f, 1.55f), new Vector3(0.9f, 0.7f, 0.35f), navy);       // helm console
        Mats.Prim(PrimitiveType.Cylinder, island, new Vector3(0f, 4.9f, 1.35f), new Vector3(0.55f, 0.03f, 0.55f), new Vector3(70f, 0f, 0f), steel);   // wheel
        Mats.Prim(PrimitiveType.Cylinder, island, new Vector3(0.5f, 5.6f, -1.2f), new Vector3(0.12f, 1.6f, 0.12f), steel);   // mast
        radar = Mats.Node(island, "Radar", new Vector3(0.5f, 7.1f, -1.2f));
        Mats.Prim(PrimitiveType.Cube, radar, Vector3.zero, new Vector3(1.6f, 0.25f, 0.12f), steel);
        Mats.Prim(PrimitiveType.Cube, island, new Vector3(0.95f, 6.6f, -1.2f), new Vector3(0.7f, 0.45f, 0.02f), pink);       // pennant
        Mats.Prim(PrimitiveType.Sphere, island, new Vector3(0.5f, 7.45f, -1.2f), Vector3.one * 0.2f, deckLightMat);
        // rails + deck edge lights
        for (int s = -1; s <= 1; s += 2)
            for (int i = 0; i < 9; i++) Mats.Prim(PrimitiveType.Sphere, R, new Vector3(s * (CarrierLayout.HalfW - 0.15f), DY + 0.06f, -11.5f + i * 3.1f), Vector3.one * 0.16f, deckLightMat);
        foreach (var r in parts.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = Look.Mobile ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
        foreach (var c in parts.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
        Mats.SetLayer(parts, 0);
        // solid colliders (camper-local; enabled in carrier mode): deck, hull, island, bridge floor, rails
        var cg = new GameObject("CarrierCols"); cg.transform.SetParent(transform, false); cg.layer = 0;
        System.Action<Vector3, Vector3> box = (c, s) => { var b = cg.AddComponent<BoxCollider>(); b.center = c; b.size = s; b.enabled = false; deckCols.Add(b); };
        float zc = (CarrierLayout.Z0 + CarrierLayout.Z1) * 0.5f, zl = CarrierLayout.Z1 - CarrierLayout.Z0;
        box(new Vector3(0f, DY - 0.2f, zc), new Vector3(CarrierLayout.HalfW * 2f, 0.4f, zl));
        box(new Vector3(0f, 0.9f, zc), new Vector3(CarrierLayout.HalfW * 2f - 0.4f, 3.4f, zl - 1f));
        box(new Vector3(0f, DY - 0.2f, CarrierLayout.Z1 + 1.4f), new Vector3(5f, 0.4f, 2.8f));                       // bow tip deck
        box(new Vector3(3.3f, DY + 2f, -0.5f), new Vector3(2f, 4f, 5f));                                                 // island
        box(new Vector3(3.3f, DY + 4f, -0.3f), new Vector3(2.6f, 0.1f, 3.4f));                                           // bridge floor
        for (int s = -1; s <= 1; s += 2) box(new Vector3(s * (CarrierLayout.HalfW - 0.05f), DY + 0.45f, zc), new Vector3(0.1f, 0.9f, zl));
        box(new Vector3(0f, DY + 0.45f, CarrierLayout.Z0 + 0.05f), new Vector3(CarrierLayout.HalfW * 2f, 0.9f, 0.1f));
        // James's VTOL jet lands on the deck but never shoves the carrier (raycasts still see the deck)
        if (Jet.I != null) foreach (var jc in Jet.I.GetComponentsInChildren<Collider>(true)) foreach (var dc in deckCols) if (jc != null && dc != null) Physics.IgnoreCollision(jc, dc, true);
        // boarding: swim up to the hull, "A / E: Climb aboard"
        foreach (float sx in new[] { -1f, 1f })
            foreach (float z in new[] { -8f, 0f, 8f })
            {
                float zz = z, xx = sx;
                var h = new Hotspot { radius = 4.2f, label = "Climb aboard Spotty's Carrier", humansOnly = false };
                h.enabled = f => Carrier && !OnDeck(f.transform.position);
                h.act = f => Board(f, xx, zz);
                Interact.All.Add(h);
                spots.Add(h);
                hotZ.Add(new Vector2(xx, zz));
            }
        Debug.Log("Carrier48: carrier parts built (" + deckCols.Count + " colliders)");
    }
    readonly List<Vector2> hotZ = new List<Vector2>();

    void LateUpdate()
    {
        prevW2L = transform.worldToLocalMatrix; prevOk = true;   // ffu50 CarryFrame
        if (!built) return;
        float wy = Ripples.SurfaceY;
        for (int i = 0; i < spots.Count; i++)
        {
            Vector3 p = transform.TransformPoint(new Vector3(hotZ[i].x * (CarrierLayout.HalfW + 0.8f), 0f, hotZ[i].y));
            p.y = wy;
            spots[i].pos = p;
        }
    }

    public void Board(Frog f, float side, float z)
    {
        if (f == null || !Carrier) return;
        if (f.vehicle != null) f.ExitVehicle();
        float x = side < 0f ? -2.2f : 1.4f;
        f.Teleport(DeckPoint(x, z) + Vector3.up * 0.2f);
        if (f.human) f.Toast("Aboard Spotty's Carrier!", 2f);
        FX.Spray(f.transform.position, Vector3.up * 2f, 0.3f, 0.6f, new Color(0.9f, 0.97f, 1f, 0.8f));
        Debug.Log("Carrier48: " + f.nick + " climbed aboard");
    }

    Transform DeckPanel(Transform R, string name, float z0, float z1, Material deckM, Material lineW, Material yellow, bool fore)
    {
        float DY = CarrierLayout.DeckY, W = CarrierLayout.HalfW * 2f;
        Transform n = Mats.Node(R, name, new Vector3(0f, DY, 0f));
        float zc = (z0 + z1) * 0.5f, zl = z1 - z0;
        Mats.Prim(PrimitiveType.Cube, n, new Vector3(0f, -0.18f, zc), new Vector3(W, 0.36f, zl), deckM);
        Mats.Prim(PrimitiveType.Cube, n, new Vector3(0f, -0.5f, zc), new Vector3(W, 0.3f, zl), Mats.Paint(new Color(0.96f, 0.96f, 0.94f), 0.55f));
        // centre-line dashes + yellow edge lines
        for (float z = z0 + 0.6f; z < z1 - 0.5f; z += 2.2f) Mats.Prim(PrimitiveType.Cube, n, new Vector3(-0.6f, 0.005f, z + 0.5f), new Vector3(0.25f, 0.02f, 1.1f), lineW);
        for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, n, new Vector3(s * (W * 0.5f - 0.45f), 0.006f, zc), new Vector3(0.14f, 0.02f, zl - 0.1f), yellow);
        if (fore)
        {
            // bow tip deck + the "deck arrows" toward the bow
            for (int i = 0; i < 3; i++) { var ar = Mats.Prim(PrimitiveType.Cube, n, new Vector3(-0.6f, 0.007f, z1 - 1.5f - i * 1.2f), new Vector3(1.2f, 0.02f, 0.18f), lineW); ar.transform.localRotation = Quaternion.Euler(0f, 0f, 0f); }
            Mats.Prim(PrimitiveType.Cube, n, new Vector3(0f, -0.18f, z1 + 1.4f), new Vector3(5f, 0.36f, 2.8f), deckM);
        }
        return n;
    }

    // a bow wedge: base width w, height h, length l (points along +z)
    static Mesh Wedge(float w, float h, float l)
    {
        float hw = w * 0.5f;
        Vector3[] v = { new Vector3(-hw, 0f, 0f), new Vector3(hw, 0f, 0f), new Vector3(hw, h, 0f), new Vector3(-hw, h, 0f), new Vector3(0f, h * 0.15f, l), new Vector3(0f, h, l * 0.9f) };
        int[] t = { 0, 1, 4, 1, 2, 5, 1, 5, 4, 0, 4, 5, 0, 5, 3, 3, 5, 2, 0, 3, 2, 0, 2, 1 };
        var vv = new List<Vector3>(); var tt = new List<int>();
        Vector3 cen = Vector3.zero; foreach (var p in v) cen += p; cen /= v.Length;
        for (int i = 0; i < t.Length; i += 3)
        {
            Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f - cen) < 0f) { var x = b; b = c; c = x; }
            int s = vv.Count; vv.Add(a); vv.Add(b); vv.Add(c); tt.Add(s); tt.Add(s + 1); tt.Add(s + 2);
        }
        var m = new Mesh(); m.SetVertices(vv); m.SetTriangles(tt, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }
}

// the camper itself (TrailerModels style 4): a rounded white "squaredrop" with Spotty's black spots, a pink stripe, two
// solar panels, windows, a door and a little roof deck rail (that is where Spotty stands while it sinks)
public static class CarrierModels
{
    public static void Camper(Trailer tr, Transform p, Trailer.Spec s)
    {
        float L = s.len, W = s.wid, H = s.hgt, F = s.floorY, hz = L * 0.5f;
        var pts = new List<Vector2>();
        float r = 0.55f;
        pts.Add(new Vector2(-hz, 0f)); pts.Add(new Vector2(hz, 0f));
        for (int i = 0; i <= 6; i++) { float a = i / 6f * Mathf.PI * 0.5f; pts.Add(new Vector2(hz - r + r * Mathf.Cos(a), H - r + r * Mathf.Sin(a))); }
        for (int i = 0; i <= 6; i++) { float a = Mathf.PI * 0.5f + i / 6f * Mathf.PI * 0.5f; pts.Add(new Vector2(-hz + r + r * Mathf.Cos(a), H - r + r * Mathf.Sin(a))); }
        var sh = TrailerModels.Extrude(p, pts.ToArray(), W * 0.5f, Mats.Paint(s.main, 0.55f));
        sh.transform.localPosition = new Vector3(0f, F, 0f);
        Material blk = Mats.Lit(new Color(0.07f, 0.07f, 0.08f)), pink = Mats.Paint(s.accent, 0.6f), glass = Mats.GlassTint(new Color(0.08f, 0.12f, 0.16f));
        Mats.Prim(PrimitiveType.Cube, p, new Vector3(0f, F + 0.1f, 0f), new Vector3(W + 0.02f, 0.2f, L - 0.1f), blk);
        var rnd = new System.Random(4);
        for (int k = -1; k <= 1; k += 2)
        {
            Mats.Prim(PrimitiveType.Cube, p, new Vector3((W * 0.5f + 0.006f) * k, F + 0.55f, 0f), new Vector3(0.01f, 0.12f, L * 0.86f), pink);
            for (int i = 0; i < 7; i++)
            {
                float z = -hz + 0.5f + i * (L - 1f) / 6f + (float)rnd.NextDouble() * 0.3f, y = F + 0.9f + (float)rnd.NextDouble() * (H - 1.4f), sz = 0.22f + (float)rnd.NextDouble() * 0.28f;
                if (Mathf.Abs(z - 0.9f) < 0.55f && y > F + 1.1f) continue;   // keep the window clear
                Mats.Prim(PrimitiveType.Cylinder, p, new Vector3((W * 0.5f + 0.012f) * k, y, z), new Vector3(sz, 0.01f, sz * 0.8f), new Vector3(0f, 0f, 90f), blk);
            }
            Mats.Prim(PrimitiveType.Cube, p, new Vector3((W * 0.5f + 0.01f) * k, F + 1.45f, 0.9f), new Vector3(0.03f, 0.55f, 0.95f), glass);
        }
        // door (curb side) + step, front + rear windows, lights
        Mats.Prim(PrimitiveType.Cube, p, new Vector3(W * 0.5f + 0.015f, F + 0.95f, -1.2f), new Vector3(0.03f, 1.7f, 0.7f), Mats.Paint(new Color(0.9f, 0.9f, 0.88f), 0.5f));
        Mats.Prim(PrimitiveType.Cylinder, p, new Vector3(W * 0.5f + 0.035f, F + 1.4f, -1.2f), new Vector3(0.3f, 0.01f, 0.3f), new Vector3(0f, 0f, 90f), glass);
        Mats.Prim(PrimitiveType.Cube, p, new Vector3(W * 0.5f + 0.16f, F - 0.2f, -1.2f), new Vector3(0.3f, 0.05f, 0.6f), blk);
        Mats.Prim(PrimitiveType.Cube, p, new Vector3(0f, F + H * 0.6f, hz + 0.01f), new Vector3(W * 0.6f, 0.45f, 0.04f), glass);
        Mats.Prim(PrimitiveType.Cube, p, new Vector3(0f, F + H * 0.62f, -hz - 0.01f), new Vector3(W * 0.4f, 0.35f, 0.04f), glass);
        for (int k = -1; k <= 1; k += 2) Mats.Prim(PrimitiveType.Cube, p, new Vector3(W * 0.36f * k, F + 0.35f, -hz - 0.03f), new Vector3(0.26f, 0.1f, 0.05f), Mats.Unlit(new Color(1f, 0.12f, 0.1f)));
        // a pink paw on the nose
        Mats.Prim(PrimitiveType.Cylinder, p, new Vector3(0f, F + 0.75f, hz + 0.02f), new Vector3(0.36f, 0.01f, 0.3f), new Vector3(90f, 0f, 0f), pink);
        // roof: two solar panels + a slim rail
        TrailerModels.Solar(p, new Vector3(0f, F + H + 0.04f, -1.05f), 1.2f, 1.45f, Vector3.zero);
        TrailerModels.Solar(p, new Vector3(0f, F + H + 0.04f, 0.6f), 1.2f, 1.45f, Vector3.zero);
        for (int k = -1; k <= 1; k += 2) Mats.Prim(PrimitiveType.Cube, p, new Vector3(0.82f * k, F + H + 0.1f, -0.2f), new Vector3(0.04f, 0.04f, 3.6f), Mats.Steel(new Color(0.78f, 0.8f, 0.82f)));
        var col = new GameObject("TCol").AddComponent<BoxCollider>();
        col.transform.SetParent(tr.transform, false);
        col.center = new Vector3(0f, F + H * 0.5f, 0f); col.size = new Vector3(W, H, L - 0.2f);
    }
}
