using System.Collections.Generic;
using UnityEngine;

// ffu75 (Bill, Oct 23 09:49): "When you're in space in the mech, it should be the mech that lands on the planet, not a
// starship or a rocket ... The mechs should be the thing they see landing with its rockets, and then them exiting down a ramp."
//  * Every land path (pad Y, key F, touch LAND, auto) ends in SpaceWorld.Land72; for a ship in MECH form it now hands over
//    here (SpaceWorld.Land75) instead of dropping the crew at the Starship pad.
//  * The crew are on the target world at once (world look, music, cameras) but held inside the mech. The mech ITSELF - a
//    full-size copy of that mech (same body, same band height as on the ranch: 12 / 30 / 60 / 160 m; the trillion-story one
//    carries its bridge) - drops in from high up on its foot + back thruster flames, legs down, slows, kicks up dust and
//    touches down with a thud. Then a ramp (a gangway with a moving walkway) swings down from the chest (10 / 100 / 1000) or
//    out of the bridge viewscreen (trillion) and the froggies walk down it onto the surface, one after another.
//  * The mech stays parked there (no Starship / rocket stand-in: the world's Starship prop is hidden while a mech stands on
//    that world and the Starship itself is not there). A at the foot of the ramp: the pilot (the owner - mechs are
//    owner-locked) walks up with the crew, the ramp folds up and the mech lifts off on its flames back to orbit. On a shared
//    screen the other players can walk up and ride along; in a split view humans only ride their own mech (ffu72 item 7).
//  * Landing at the ranch (Earth) plays the same descent + ramp, then the real ranch mech stands where the copy landed
//    (empty, the pilot climbs back in as usual).
//  * Each crew member's own camera (or the shared camera) watches from the ground: descent, touchdown, the ramp, the walk.
//    No new camera (the player's camera is posed after the normal cameras, like LaunchSeq). A / FIRE skips a step.
// &ff75old=1 = the pre-ffu75 paths (crew dropped at the Starship pad, mech waits in orbit). Probe ?ffdemo=1&ffshot=ffu75.
public static class F75
{
    static int old = -1;
    public static bool Old { get { if (old < 0) old = Ffu33.Url("ff75old") == "1" ? 1 : 0; return old == 1; } }
}

public partial class Frog
{
    public MechLand75 land75;   // ffu75: held by a landing / taking-off mech (scripted: inside it, or walking its ramp)
}

public partial class MechLand75 : MonoBehaviour
{
    public static readonly List<MechLand75> All = new List<MechLand75>();
    public const float DtCap75 = 0.2f;                       // SwiftShader / slow phones: the cinematic runs on game time, capped
    public const float DescentT = 7f, TouchHold = 1.2f, RampT = 2.6f, RampUpT = 2f, IgniteT = 1.1f, ClimbT = 5f, FadeT = 0.6f;
    static readonly float[] RampDeg = { 30f, 34f, 40f, 60f };   // 60 deg: the trillion-story gangway is a steep moving stair

    public enum Ph { Descend, Touch, Ramp, WalkDown, Parked, WalkUp, RampUp, Ignite, Climb, Done }
    public Ph phase;
    public float t;
    public Starship ship;
    public StoryMech mech;
    public int body = -1;
    public string bodyId;
    public WorldId world;
    public Frog pilot;
    public float H, h0, alt;
    public readonly List<Frog> crew = new List<Frog>();       // held right now (inside or on the ramp)
    public readonly List<Frog> walkers = new List<Frog>();
    readonly Dictionary<Frog, float> walkT0 = new Dictionary<Frog, float>();
    readonly Dictionary<Frog, Vector3> targets = new Dictionary<Frog, Vector3>();
    public Transform root, bodyT, rampHinge, rampBox;
    public Bridge73 bridge;
    Transform legL, legR, torso, armL, armR;
    readonly List<Transform> flames = new List<Transform>();
    readonly List<Transform> chevrons = new List<Transform>();
    Light jetLight;
    AudioSource rumble;
    public Vector3 C, F, fwd, right;          // mech centre on the ground, ramp foot, facing, right
    public Vector3 hingeL, deckL;             // body-local hinge (top of the ramp) and the deck point inside
    public float rampLen, rampK;              // 0 stowed .. 1 down
    Quaternion rampUpQ, rampDownQ;
    Hotspot spot;
    public float yawDeg;
    public int dustPuffs, touchdowns;         // probe
    public bool skipNext;
    public float fade;
    float burnK;
    bool earth;
    public static int Begun, Landed, RampsDown, Walked, Takeoffs, Swaps;   // probe counters

    public bool Earth { get { return earth; } }
    public bool OnSurface { get { return phase != Ph.Done; } }
    public bool WantsCam { get { return phase == Ph.Descend || phase == Ph.Touch || phase == Ph.Ramp || phase == Ph.WalkDown || phase == Ph.RampUp || phase == Ph.Ignite || phase == Ph.Climb || (phase == Ph.WalkUp && walkers.Count > 0); } }
    public static MechLand75 Of(Frog f) { foreach (var m in All) if (m != null && m.crew.Contains(f)) return m; return null; }
    public static MechLand75 For(Starship s) { foreach (var m in All) if (m != null && m.ship == s) return m; return null; }
    public static MechLand75 InWorld(WorldId w) { foreach (var m in All) if (m != null && m.world == w) return m; return null; }
    // the space copy is not drawn in orbit while its mech stands on a world
    public static bool Hides(Starship s) { return !F75.Old && For(s) != null; }

    // ---------------- ground ----------------
    public static float Ground(WorldId w, float x, float z)
    {
        switch (w)
        {
            case WorldId.Moon: return MoonWorld.GY(new Vector3(x, 0f, z));
            case WorldId.Mars: return Worlds.MarsO.y + SurfaceWorlds.MarsY(x - Worlds.MarsO.x, z - Worlds.MarsO.z);
            case WorldId.Callisto: return Worlds.CallistoO.y + SurfaceWorlds.CalY(x - Worlds.CallistoO.x, z - Worlds.CallistoO.z);
            default: return Ranch.GY(x, z);
        }
    }
    static Vector3 Origin(WorldId w) { return w == WorldId.Moon ? Worlds.MoonO : w == WorldId.Mars ? Worlds.MarsO : w == WorldId.Callisto ? Worlds.CallistoO : Vector3.zero; }
    // where the ramp comes down (world-local, beside the landing pad) and which way the mech stands from it (free ground)
    static void Site(WorldId w, out Vector2 foot, out Vector2 away)
    {
        switch (w)
        {
            case WorldId.Moon: foot = new Vector2(0f, -14f); away = new Vector2(-0.6f, 0.8f); break;       // NW: clear of the bay, lander, flag, Apollo 11
            case WorldId.Mars: foot = new Vector2(-2f, -18f); away = new Vector2(-0.8f, 0.6f); break;      // NW: clear of the crater, pen and cave
            default: foot = new Vector2(-2f, 12f); away = new Vector2(-0.7f, -0.7f); break;                // Callisto SW
        }
    }

    // ---------------- begin (SpaceWorld.Land75) ----------------
    public static MechLand75 Begin(Starship s, StoryMech m, string bodyId, int bodyIdx, WorldId w, List<Frog> who, Frog pilot)
    {
        var go = new GameObject("MechLand75 " + m.Title + " @" + bodyId);
        var ml = go.AddComponent<MechLand75>();
        ml.Setup(s, m, bodyId, bodyIdx, w, who, pilot);
        return ml;
    }

    void Setup(Starship s, StoryMech m, string id, int bodyIdx, WorldId w, List<Frog> who, Frog p)
    {
        ship = s; mech = m; bodyId = id; body = bodyIdx; world = w; pilot = p; earth = w == WorldId.Ranch;
        H = m.height;
        root = transform;
        bodyT = Mats.Node(root, "Body", Vector3.zero);
        var parts = StoryMech.BuildBodyCopy(bodyT, m.owner, m.band, H);
        legL = parts[1]; legR = parts[2]; torso = parts[3]; armL = parts[4]; armR = parts[5];
        if (armL != null) armL.localRotation = Quaternion.Euler(-10f, 0f, -22f);
        if (armR != null) armR.localRotation = Quaternion.Euler(-10f, 0f, 22f);
        if (F73.Has(m.band) && parts.Length > 6 && parts[6] != null)
        {
            bridge = Bridge73.ForHead(parts[6], H, m.owner, Froggies.Names[m.owner].ToUpper() + "'S BRIDGE");
            if (bridge != null) Mats.SetLayer(bridge.gameObject, Vehicle.VehicleLayer);
        }
        // the ramp top: out of the bridge viewscreen (over its sill), or the front of the chest
        if (bridge != null)
        {
            hingeL = bodyT.InverseTransformPoint(bridge.room.TransformPoint(new Vector3(0f, 1.02f, 3.35f)));
            deckL = bodyT.InverseTransformPoint(bridge.room.TransformPoint(new Vector3(0f, 0.02f, 1.9f)));
        }
        else
        {
            Vector3 mn, mx;
            if (!ChestBounds(out mn, out mx)) { mn = new Vector3(-0.12f, 0.45f, -0.1f) * H; mx = new Vector3(0.12f, 0.8f, 0.1f) * H; }
            float y = Mathf.Lerp(mn.y, mx.y, 0.55f);
            hingeL = new Vector3(0f, y, mx.z + 0.01f * H);
            deckL = new Vector3(0f, y, mx.z - 0.06f * H);
        }
        BuildFlames();
        BuildRamp();
        if (!Look.Mobile)
        {
            var lg = new GameObject("MechLandLight75"); lg.transform.SetParent(bodyT, false); lg.transform.localPosition = new Vector3(0f, -0.05f * H, 0f);
            jetLight = lg.AddComponent<Light>(); jetLight.type = LightType.Point; jetLight.color = new Color(1f, 0.62f, 0.3f); jetLight.range = H * 1.3f + 10f; jetLight.shadows = LightShadows.None; jetLight.intensity = 0f;
        }
        Mats.SetLayer(bodyT.gameObject, Vehicle.VehicleLayer);
        foreach (var t2 in flames) Mats.NoShadows(t2.gameObject);
        // where: the ranch spot beside the Starship pad (where ReturnFromSpace parks it), else the world's landing site
        float ang = RampDeg[Mathf.Clamp(m.band, 0, 3)] * Mathf.Deg2Rad;
        float reach = hingeL.z + hingeL.y / Mathf.Tan(ang);
        int others = 0; foreach (var o in All) if (o != null && o != this && o.world == w) others++;
        if (earth)
        {
            Vector2 pc = Layout.PadC;
            C = new Vector3(pc.x + 30f + H * 0.3f, 0f, pc.y + 10f + others * (H * 0.5f + 30f));
            yawDeg = 90f;
            fwd = Vector3.right;
            F = C + fwd * reach;
        }
        else
        {
            Vector2 fl, aw; Site(w, out fl, out aw);
            Vector3 o = Origin(w);
            Vector3 awW = new Vector3(aw.x, 0f, aw.y).normalized;
            Vector3 sideW = new Vector3(awW.z, 0f, -awW.x);
            F = o + new Vector3(fl.x, 0f, fl.y) + sideW * (others * (H * 0.4f + 14f));
            fwd = -awW;
            C = F + awW * reach;
            yawDeg = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
        }
        right = new Vector3(fwd.z, 0f, -fwd.x);
        // stand on the higher of the centre / feet (never sink a foot into a slope)
        float gy = Ground(w, C.x, C.z);
        foreach (float sx in new[] { -1f, 1f }) { Vector3 fp = C + right * (sx * 0.1f * H); gy = Mathf.Max(gy, Ground(w, fp.x, fp.z)); }
        C.y = gy;
        F.y = Ground(w, F.x, F.z);
        root.position = C; root.rotation = Quaternion.Euler(0f, yawDeg, 0f);
        Spot76(reach);   // ffu76: a spot whose whole ramp + foot are on dry, clear ground (MechSpot76.cs); the pilot may steer it
        h0 = Mathf.Max(220f, H * 2.6f);
        alt = h0; bodyT.localPosition = new Vector3(0f, alt, 0f);
        AimRamp();
        SetRamp(0f); rampHinge.gameObject.SetActive(false);
        // crew: held inside the mech; each one's walk-off spot fans out past the ramp foot
        int n = who.Count;
        for (int k = 0; k < n; k++)
        {
            Frog f = who[k];
            if (f == null) continue;
            Vector3 tp = F + fwd * 3.5f + right * ((k - (n - 1) * 0.5f) * 1.9f);
            tp.y = Ground(w, tp.x, tp.z) + 0.05f;
            targets[f] = tp;
            if (f.netPuppet) { f.SendTo(w, tp + Vector3.up * 0.4f, yawDeg); continue; }   // online froggies: their own device poses them
            Hold(f);
        }
        rumble = Sfx.EngRocket != null ? Sfx.Loop(gameObject, Sfx.EngRocket) : null;
        if (rumble != null) { rumble.volume = 0.6f; rumble.pitch = 0.75f; rumble.Play(); }
        phase = Ph.Descend; t = 0f; burnK = 1f;
        spot = Interact.Add(F + fwd * 1.5f, 5f, "board the mech", BoardSpot);
        spot.dynLabel = fr => fr == pilot || (pilot != null && !pilot.human) ? "board " + mech.Title + " (take off)" : "board " + mech.Title + " (ride along)";
        spot.enabled = fr => phase == Ph.Parked && !earth && fr.world == world && fr.vehicle == null && fr.passengerOf == null && fr.land75 == null && CanRide(fr);
        All.Add(this);
        Begun++;
        Debug.Log("FF75 " + m.Title + " (H " + H + ") lands itself on " + id + " at " + C.ToString("0") + " yaw " + yawDeg.ToString("0") + ", ramp " + RampDeg[Mathf.Clamp(m.band, 0, 3)] + " deg " + rampLen.ToString("0.0") + " m from " + (bridge != null ? "the bridge" : "the chest") + " to " + F.ToString("0") + ", crew " + crew.Count + (pilot != null ? " pilot " + pilot.nick : ""));
    }

    bool ChestBounds(out Vector3 mn, out Vector3 mx)
    {
        mn = new Vector3(1e9f, 1e9f, 1e9f); mx = -mn; bool any = false;
        if (torso == null) return false;
        foreach (var mf in torso.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Transform tr = mf.transform; bool skip = false;
            for (Transform q = tr; q != null && q != torso; q = q.parent) if (q == armL || q == armR || q.name == "Head") { skip = true; break; }
            if (skip) continue;
            Bounds bb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = bb.center + Vector3.Scale(bb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 l = bodyT.InverseTransformPoint(tr.TransformPoint(c));
                mn = Vector3.Min(mn, l); mx = Vector3.Max(mx, l); any = true;
            }
        }
        return any && mx.y > mn.y;
    }

    void BuildFlames()
    {
        Material outer = new Material(Mats.Fx); outer.color = new Color(1f, 0.55f, 0.18f, 0.55f);
        Material core = Mats.Unlit(new Color(1f, 0.95f, 0.75f));
        Material nozzle = Mats.Steel(new Color(0.3f, 0.31f, 0.34f));
        float W = 0.065f * H;
        // a thruster under each foot (points straight down)
        foreach (var leg in new[] { legL, legR })
        {
            if (leg == null) continue;
            Vector3 lp = bodyT.InverseTransformPoint(leg.position);
            Vector3 fp = new Vector3(lp.x, 0.012f * H, lp.z + 0.01f * H);
            Mats.Prim(PrimitiveType.Cylinder, bodyT, fp + Vector3.up * 0.004f * H, new Vector3(W * 0.8f, 0.008f * H, W * 0.8f), nozzle);
            Transform f = Mats.Node(bodyT, "FootFlame75", fp);
            Mats.Prim(PrimitiveType.Capsule, f, new Vector3(0f, -0.5f, 0f), new Vector3(0.62f, 0.55f, 0.62f), outer);
            Mats.Prim(PrimitiveType.Capsule, f, new Vector3(0f, -0.4f, 0f), new Vector3(0.3f, 0.45f, 0.3f), core);
            flames.Add(f);
        }
        // two back thrusters (down and a little back), the main engines of the space copy
        if (torso != null)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 np = bodyT.InverseTransformPoint(torso.position) + new Vector3(s * 0.06f * H, 0.08f * H, -0.12f * H);
                Mats.Prim(PrimitiveType.Cylinder, bodyT, np, new Vector3(W * 0.7f, 0.012f * H, W * 0.7f), nozzle);
                Transform f = Mats.Node(bodyT, "BackFlame75", np - Vector3.up * 0.01f * H);
                f.localRotation = Quaternion.Euler(-20f, 0f, 0f);
                Mats.Prim(PrimitiveType.Capsule, f, new Vector3(0f, -0.5f, 0f), new Vector3(0.62f, 0.55f, 0.62f), outer);
                Mats.Prim(PrimitiveType.Capsule, f, new Vector3(0f, -0.4f, 0f), new Vector3(0.3f, 0.45f, 0.3f), core);
                flames.Add(f);
            }
        }
    }

    void BuildRamp()
    {
        Color oc = Froggies.Color(mech.owner);
        Material deck = Mats.PBR(new Color(0.32f, 0.33f, 0.36f), 0.35f, 0.7f);
        Material rail = Mats.Steel(new Color(0.6f, 0.62f, 0.65f));
        Material belt = Mats.Lit(new Color(0.08f, 0.09f, 0.1f));
        Material stripe = Mats.Unlit(new Color(1f, 0.78f, 0.2f));
        Material trim = Mats.Unlit(Color.Lerp(oc, Color.white, 0.4f));
        rampHinge = Mats.Node(bodyT, "Ramp75", hingeL);
        rampBox = Mats.Node(rampHinge, "Run", Vector3.zero);   // +z down the ramp, length set in AimRamp (unit length, scaled)
        float w = Mathf.Clamp(0.035f * H, 2.4f, 6f);
        // unit-length parts (the Run node is scaled along z to the ramp length)
        Mats.Prim(PrimitiveType.Cube, rampBox, new Vector3(0f, -0.12f, 0.5f), new Vector3(w, 0.22f, 1f), deck);
        Mats.Prim(PrimitiveType.Cube, rampBox, new Vector3(0f, 0.0f, 0.5f), new Vector3(w * 0.72f, 0.03f, 1f), belt);
        for (int s = -1; s <= 1; s += 2)
        {
            Mats.Prim(PrimitiveType.Cube, rampBox, new Vector3(s * w * 0.5f, 0.55f, 0.5f), new Vector3(0.1f, 0.08f, 1f), rail);
            Mats.Prim(PrimitiveType.Cube, rampBox, new Vector3(s * w * 0.5f, 0.12f, 0.5f), new Vector3(0.16f, 0.26f, 1f), trim);
        }
        // moving-walkway chevrons (children of the hinge, not the scaled run, so they keep their shape)
        for (int k = 0; k < 10; k++)
        {
            var c = Mats.Node(rampHinge, "Chevron", Vector3.zero);
            Mats.Prim(PrimitiveType.Cube, c, new Vector3(0f, 0.03f, 0f), new Vector3(w * 0.6f, 0.02f, 0.25f), stripe);
            chevrons.Add(c);
        }
        Mats.NoShadows(rampHinge.gameObject);
    }

    // point the ramp from its hinge to the foot on the ground (the real ground under the foot, not a nominal angle)
    void AimRamp()
    {
        Vector3 hingeW = root.TransformPoint(hingeL);   // body at rest (alt 0)
        Vector3 d = F - hingeW;
        rampLen = d.magnitude;
        Quaternion inv = Quaternion.Inverse(root.rotation);
        Vector3 dn = d.normalized;
        rampDownQ = inv * Quaternion.LookRotation(dn, Vector3.Cross(dn, right));
        rampUpQ = inv * Quaternion.LookRotation(fwd, Vector3.up);   // stowed: retracted into the chest / under the viewscreen sill
        SetRamp(0f);
    }
    // k 0 = retracted, 1 = down on the ground: the gangway slides out level first, then swings down while it extends
    void SetRamp(float k)
    {
        rampK = k;
        rampHinge.localRotation = Quaternion.Slerp(rampUpQ, rampDownQ, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - 0.15f) / 0.85f)));
        rampBox.localScale = new Vector3(1f, 1f, Mathf.Max(0.05f, rampLen * Mathf.Lerp(0.03f, 1f, Mathf.Clamp01(k * 1.25f))));
    }
    public float RampDegNow { get { if (rampHinge == null) return 0f; Vector3 dz = rampHinge.forward; return Mathf.Asin(Mathf.Clamp(-dz.y, -1f, 1f)) * Mathf.Rad2Deg; } }
    public Vector3 HingeW { get { return bodyT.TransformPoint(hingeL); } }
    public Vector3 DeckW { get { return bodyT.TransformPoint(deckL); } }

    bool CanRide(Frog f)
    {
        if (f == pilot) return true;
        if (!f.human) return false;
        return Game.I != null && Game.I.SharedScreen;   // ffu72 item 7: split-screen humans ride only their own mech
    }

    // ---------------- holding froggies ----------------
    void Hold(Frog f)
    {
        if (f.vehicle != null) f.ExitVehicle();
        f.LeavePassenger();
        if (f.jetOf != null) { f.jetOf = null; }
        f.transform.SetParent(null, true);
        f.transform.localScale = Vector3.one;
        f.SetSuit(false);
        f.land75 = this;
        if (f.cc != null) f.cc.enabled = false;
        f.model.gameObject.SetActive(false);
        f.transform.position = DeckW;
        if (!crew.Contains(f)) crew.Add(f);
    }
    void Release(Frog f, Vector3 at, float yaw)
    {
        f.land75 = null;
        crew.Remove(f); walkers.Remove(f);
        f.model.gameObject.SetActive(true);
        f.SendTo(world, at, yaw);
        if (f.cc != null && !f.cc.enabled) f.cc.enabled = true;
    }
    public string Prompt(Frog f)
    {
        switch (phase)
        {
            case Ph.Descend: { string p76 = Prompt76(f); return p76 ?? mech.Title + " is landing - A / FIRE skips"; }
            case Ph.WalkDown: return "Walking down the ramp - A / FIRE skips";
            case Ph.WalkUp: case Ph.RampUp: return f == pilot || walkers.Count > 0 ? "Boarding - A / FIRE skips" : "Aboard " + mech.Title + " - waiting for " + (pilot != null ? pilot.nick : "the pilot") + " to fly";
            case Ph.Ignite: case Ph.Climb: return "Liftoff! - A / FIRE skips";
            case Ph.Parked: return "Aboard " + mech.Title + " - waiting for " + (pilot != null ? pilot.nick : "the pilot") + " to fly";
            default: return mech.Title;
        }
    }
    public void Skip(Frog f) { if (t > 0.3f) skipNext = true; }

    // ---------------- boarding (A at the foot of the ramp) ----------------
    void BoardSpot(Frog f) { Board(f); }
    public bool Board(Frog f)
    {
        if (f == null || phase != Ph.Parked || earth || !CanRide(f)) return false;
        bool go = f == pilot || (pilot != null && !pilot.human) || pilot == null;
        if (!go)
        {
            // a rider walks up and waits aboard for the pilot
            StartWalkUp(f, 0f);
            f.Toast("Aboard " + mech.Title + " - " + pilot.nick + " flies it (it's " + pilot.nick + "'s mech)", 3f);
            Debug.Log("FF75 " + f.nick + " walks up the ramp of " + mech.Title + " to ride along");
            return true;
        }
        // the pilot (or the first rider when the pilot is an AI froggy): everyone of the crew still on this world goes too
        float d0 = 0f;
        StartWalkUp(f, d0);
        if (pilot != null && pilot != f && pilot.land75 == null && pilot.world == world) { d0 += 0.6f; StartWalkUp(pilot, d0); }
        if (Game.I != null)
            foreach (Frog o in Game.I.frogs)
            {
                if (o == null || o == f || o == pilot || o.land75 != null || o.world != world || o.netPuppet) continue;
                if (o.vehicle != null || o.passengerOf != null) continue;
                bool came = targets.ContainsKey(o);
                if (o.human ? (Game.I.SharedScreen) : came) { d0 += 0.6f; StartWalkUp(o, d0); }
            }
        phase = Ph.WalkUp; t = 0f;
        Sfx.Play(Sfx.Door, 0.7f, 0.9f);
        Debug.Log("FF75 " + f.nick + " boards " + mech.Title + " on " + bodyId + " - " + walkers.Count + " walking up, " + (crew.Count - walkers.Count) + " already aboard");
        return true;
    }
    void StartWalkUp(Frog f, float delay)
    {
        // AI froggies wandering off hop back to the foot of the ramp first
        Vector3 d = f.transform.position - F; d.y = 0f;
        if (d.magnitude > 14f) { Vector3 p = F + fwd * 2f; p.y = Ground(world, p.x, p.z) + 0.05f; f.Teleport(p); }
        Hold(f);
        f.model.gameObject.SetActive(true);
        Vector3 at = F + fwd * 1.5f; at.y = Ground(world, at.x, at.z) + 0.05f;
        f.transform.position = at;
        walkT0[f] = t + delay;
        if (!walkers.Contains(f)) walkers.Add(f);
    }

    // ---------------- per frame ----------------
    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, DtCap75);
        if (ship == null || mech == null) { End("ship or mech gone"); return; }
        // the mech flew home another way (the safety net) or its pilot left the off-Earth worlds
        if (!earth && phase == Ph.Parked && (ship.mechForm != mech || ship.parkedFor == null)) { End("the mech left (" + (ship.mechForm != mech ? "no longer this ship's form" : "nobody it waits for") + ")"); return; }
        // anyone taken away by something else (lobby, a world change) is no longer ours
        crew.RemoveAll(f => f == null || f.land75 != this); walkers.RemoveAll(f => f == null || f.land75 != this);
        if (phase != Ph.Parked && crew.Count == 0 && walkers.Count == 0 && (pilot == null || pilot.world != world)) { End("nobody left with it"); return; }
        t += dt;
        bool skip = skipNext; skipNext = false;
        switch (phase)
        {
            case Ph.Descend:
                {
                    if (skip) t = DescentT;
                    bool wait76 = Descend76(dt, skip);   // ffu76: pilot steering + no touchdown on a wet / blocked spot
                    float u = Mathf.Clamp01(t / DescentT);
                    alt = h0 * Mathf.Pow(1f - u, 2.4f);
                    bodyT.localPosition = new Vector3(0f, alt, 0f);
                    // legs hang straight down, a slow sway on the way in
                    if (legL != null) legL.localRotation = Quaternion.Slerp(legL.localRotation, Quaternion.Euler(4f, 0f, 0f), dt * 3f);
                    if (legR != null) legR.localRotation = Quaternion.Slerp(legR.localRotation, Quaternion.Euler(-3f, 0f, 0f), dt * 3f);
                    bodyT.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.9f) * 1.5f * (1f - u), 0f, Mathf.Sin(t * 0.7f) * 1.2f * (1f - u));
                    burnK = 1f;
                    Dust(dt, alt);
                    if (t >= DescentT && !wait76) Touchdown();
                    break;
                }
            case Ph.Touch:
                {
                    float k = Mathf.Clamp01(t / 0.35f);
                    bodyT.localPosition = new Vector3(0f, -Mathf.Sin(k * Mathf.PI) * 0.015f * H, 0f);   // the legs take the weight
                    bodyT.localRotation = Quaternion.identity;
                    if (legL != null) legL.localRotation = Quaternion.Slerp(legL.localRotation, Quaternion.identity, dt * 6f);
                    if (legR != null) legR.localRotation = Quaternion.Slerp(legR.localRotation, Quaternion.identity, dt * 6f);
                    burnK = Mathf.MoveTowards(burnK, 0f, dt * 2.5f);
                    if (t >= TouchHold || skip) { phase = Ph.Ramp; t = 0f; rampHinge.gameObject.SetActive(true); Sfx.Play(Sfx.Servo != null && Sfx.Servo.Length > 0 ? Sfx.Pick(Sfx.Servo) : Sfx.Door, 0.8f, 0.7f); if (bridge != null && bridge.visorR != null) bridge.visorR.enabled = false; }
                    break;
                }
            case Ph.Ramp:
                {
                    if (skip) t = RampT;
                    SetRamp(Mathf.Clamp01(t / RampT));
                    if (t >= RampT)
                    {
                        SetRamp(1f);
                        FX.Dust(F, 1.2f); Sfx.Play(Sfx.Clank != null ? Sfx.Clank : Sfx.Door, 0.8f, 0.8f);
                        RampsDown++;
                        Debug.Log("FF75 ramp down on " + mech.Title + " (" + RampDegNow.ToString("0") + " deg, " + rampLen.ToString("0.0") + " m) - " + crew.Count + " walk down");
                        phase = Ph.WalkDown; t = 0f;
                        int k = 0;
                        foreach (Frog f in crew) { walkT0[f] = k * 0.7f; f.transform.position = DeckW; if (!walkers.Contains(f)) walkers.Add(f); k++; }
                    }
                    break;
                }
            case Ph.WalkDown:
                {
                    burnK = 0f;
                    var done = new List<Frog>();
                    foreach (Frog f in walkers)
                    {
                        float s = t - walkT0[f];
                        if (skip) s = 1e6f;
                        Vector3 p, dir; bool end = PathDown(s, out p, out dir);
                        if (s < 0f) { f.transform.position = DeckW; continue; }
                        if (!f.model.gameObject.activeSelf) f.model.gameObject.SetActive(true);
                        Vector3 tp; if (!targets.TryGetValue(f, out tp)) tp = F + fwd * 3f;
                        if (end)
                        {
                            Vector3 d2 = tp - p; d2.y = 0f;
                            float s2 = s - DownTime;
                            if (d2.magnitude < 0.2f || s2 * 3f >= d2.magnitude || skip) { done.Add(f); continue; }
                            p += d2.normalized * s2 * 3f; p.y = Ground(world, p.x, p.z) + 0.05f; dir = d2.normalized;
                        }
                        f.transform.position = p;
                        Vector3 fl = dir; fl.y = 0f;
                        if (fl.sqrMagnitude > 1e-4f) f.transform.rotation = Quaternion.LookRotation(fl.normalized);
                        f.model.Animate(3f, false, false, false, dt);
                    }
                    foreach (Frog f in done)
                    {
                        Vector3 tp; if (!targets.TryGetValue(f, out tp)) tp = F + fwd * 3f;
                        Release(f, tp + Vector3.up * 0.05f, yawDeg);
                        FX.Dust(tp, 0.5f);
                        f.Toast(earth ? "Touchdown at the ranch!" : "You're on " + SpaceWorld.I.bodies[body].name + "! " + mech.Title + " waits here - A at the foot of its ramp to fly again", 4.5f);
                    }
                    if (walkers.Count == 0)
                    {
                        Walked++;
                        Debug.Log("FF75 crew down the ramp of " + mech.Title + " on " + bodyId + " - the mech stays parked");
                        if (earth) { SwapToRanchMech(); return; }
                        phase = Ph.Parked; t = 0f;
                    }
                    break;
                }
            case Ph.Parked:
                burnK = 0f;
                // riders waiting aboard stay hidden on the deck
                foreach (Frog f in crew) { f.transform.position = DeckW; if (f.model.gameObject.activeSelf) f.model.gameObject.SetActive(false); }
                break;
            case Ph.WalkUp:
                {
                    var done = new List<Frog>();
                    foreach (Frog f in walkers)
                    {
                        float s = t - walkT0[f];
                        if (skip) s = 1e6f;
                        if (s < 0f) continue;
                        Vector3 p, dir; bool end = PathUp(s, out p, out dir);
                        if (end) { done.Add(f); continue; }
                        f.transform.position = p;
                        Vector3 fl = dir; fl.y = 0f;
                        if (fl.sqrMagnitude > 1e-4f) f.transform.rotation = Quaternion.LookRotation(fl.normalized);
                        f.model.Animate(3f, false, false, false, dt);
                    }
                    foreach (Frog f in done) { walkers.Remove(f); f.model.gameObject.SetActive(false); f.transform.position = DeckW; }
                    bool pilotIn = pilot == null || crew.Contains(pilot);
                    if (walkers.Count == 0)
                    {
                        if (pilotIn) { phase = Ph.RampUp; t = 0f; Sfx.Play(Sfx.Servo != null && Sfx.Servo.Length > 0 ? Sfx.Pick(Sfx.Servo) : Sfx.Door, 0.8f, 0.7f); }
                        else { phase = Ph.Parked; t = 0f; }   // a rider is aboard; the pilot is still out
                    }
                    break;
                }
            case Ph.RampUp:
                {
                    if (skip) t = RampUpT;
                    SetRamp(1f - Mathf.Clamp01(t / RampUpT));
                    if (t >= RampUpT) { rampHinge.gameObject.SetActive(false); if (bridge != null && bridge.visorR != null) bridge.visorR.enabled = true; phase = Ph.Ignite; t = 0f; if (rumble != null) { rumble.volume = 0.3f; rumble.Play(); } }
                    break;
                }
            case Ph.Ignite:
                burnK = Mathf.Clamp01(t / IgniteT);
                Dust(dt, 0f);
                if (t >= IgniteT || skip) { phase = Ph.Climb; t = 0f; Takeoffs++; Debug.Log("FF75 " + mech.Title + " lifts off " + bodyId + " with " + crew.Count + " aboard"); }
                break;
            case Ph.Climb:
                {
                    burnK = 1f;
                    alt = 2f * t * t + 1.5f * t * t * t + t * H * 0.08f;
                    bodyT.localPosition = new Vector3(0f, alt, 0f);
                    Dust(dt, alt);
                    if (skip && fade < 0.01f) t = Mathf.Max(t, ClimbT - FadeT);
                    fade = Mathf.Clamp01((t - (ClimbT - FadeT)) / FadeT);
                    if (t >= ClimbT) { ToOrbit(); return; }
                    break;
                }
        }
        foreach (Frog f in crew) if (!walkers.Contains(f)) f.transform.position = DeckW;   // aboard: rides inside the mech
        Flames(dt);
        Chevrons(dt);
        if (rumble != null)
        {
            float want = burnK > 0.02f ? Mathf.Lerp(0.35f, 1f, burnK) * (phase == Ph.Climb ? Mathf.Lerp(1f, 0.4f, t / ClimbT) : 1f) : 0f;
            rumble.volume = Mathf.MoveTowards(rumble.volume, want, dt * 1.5f);
            rumble.pitch = 0.7f + 0.25f * burnK;
            if (rumble.volume <= 0f && rumble.isPlaying) rumble.Stop();
        }
    }

    // path down: deck -> hinge (a few steps) -> along the ramp -> foot; walking speed on deck, the walkway on the ramp
    float DeckSpeed { get { return 3f; } }
    float RampSpeed { get { return Mathf.Clamp(rampLen / 7f, 3f, 22f); } }
    float DownTime { get { return (HingeW - DeckW).magnitude / DeckSpeed + rampLen / RampSpeed; } }
    bool PathDown(float s, out Vector3 p, out Vector3 dir)
    {
        Vector3 a = DeckW, b = HingeW, c = F;
        float t1 = (b - a).magnitude / DeckSpeed, t2 = rampLen / RampSpeed;
        if (s <= 0f) { p = a; dir = fwd; return false; }
        if (s < t1) { p = Vector3.Lerp(a, b, s / t1); dir = b - a; return false; }
        if (s < t1 + t2) { p = Vector3.Lerp(b, c, (s - t1) / t2) + Vector3.up * 0.25f; dir = c - b; return false; }
        p = c + Vector3.up * 0.05f; dir = fwd; return true;
    }
    bool PathUp(float s, out Vector3 p, out Vector3 dir)
    {
        Vector3 a = F + fwd * 1.5f, b = F, c = HingeW, d = DeckW;
        a.y = Ground(world, a.x, a.z) + 0.05f;
        float t0 = (b - a).magnitude / DeckSpeed, t1 = rampLen / RampSpeed, t2 = (d - c).magnitude / DeckSpeed;
        if (s < t0) { p = Vector3.Lerp(a, b, s / t0); dir = b - a; return false; }
        if (s < t0 + t1) { p = Vector3.Lerp(b, c, (s - t0) / t1) + Vector3.up * 0.25f; dir = c - b; return false; }
        if (s < t0 + t1 + t2) { p = Vector3.Lerp(c, d, (s - t0 - t1) / t2); dir = d - c; return false; }
        p = d; dir = -fwd; return true;
    }

    void Touchdown()
    {
        Finalize76();
        phase = Ph.Touch; t = 0f; alt = 0f; bodyT.localPosition = Vector3.zero;
        float sc = Mathf.Clamp(H / 25f, 0.6f, 6f);
        foreach (float sx in new[] { -1f, 1f }) { Vector3 fp = root.TransformPoint(new Vector3(sx * 0.1f * H, 0f, 0f)); FX.Dust(fp, 2f + sc); }
        FX.Boom(C + Vector3.up * 0.5f, Mathf.Clamp(H / 60f, 0.4f, 2.5f));
        Game.Shake(C, Mathf.Clamp(H / 80f, 0.3f, 1.2f));
        Sfx.Play(Sfx.Boom, 0.8f, Mathf.Lerp(0.8f, 0.45f, Mathf.Clamp01(H / 160f)));
        touchdowns++; Landed++;
        Debug.Log("FF75 touchdown: " + mech.Title + " stands on " + bodyId + " at " + C.ToString("0.0"));
    }

    void Dust(float dt, float a)
    {
        float reach = H * 1.2f + 25f;
        if (a > reach) return;
        float k = Mathf.Clamp01(1f - a / reach) * burnK;
        int n = Mathf.RoundToInt(k * 3f);
        float rmax = Mathf.Max(6f, H * 0.45f);
        for (int i = 0; i < n; i++)
        {
            float ang = Random.value * 6.2832f, r = Random.Range(0.15f, 1f) * rmax;
            Vector3 p = C + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
            p.y = Ground(world, p.x, p.z) + 0.3f;
            float g = world == WorldId.Mars ? 0.75f : world == WorldId.Ranch ? 0.7f : 0.68f;
            Color c = world == WorldId.Mars ? new Color(0.8f, 0.5f, 0.32f, 0.7f) : new Color(g, g * 0.97f, g * 0.93f, 0.7f);
            FX.Smoke(p, Mathf.Clamp(H * 0.05f, 2f, 10f) * Random.Range(0.7f, 1.3f), c);
            dustPuffs++;
        }
        if (Random.value < k) { FX.Dust(C + new Vector3(Random.Range(-1f, 1f) * rmax, 0.2f, Random.Range(-1f, 1f) * rmax), 1.5f); dustPuffs++; }
        if (k > 0.05f) Game.Shake(C, k * dt * 0.8f);
    }

    void Flames(float dt)
    {
        float tm = Time.time;
        bool on = burnK > 0.02f;
        for (int i = 0; i < flames.Count; i++)
        {
            Transform f = flames[i];
            if (f == null) continue;
            if (f.gameObject.activeSelf != on) f.gameObject.SetActive(on);
            if (!on) continue;
            float fl = 1f + Mathf.Sin(tm * (43f + i * 6f)) * 0.08f + Random.Range(-0.05f, 0.05f);
            bool foot = i < 2;
            // longer near the ground in the final powered metres (and while climbing off the pad)
            float near = phase == Ph.Descend ? Mathf.Clamp01(1f - alt / (H * 1.5f + 30f)) : 1f;
            float len = H * (foot ? 0.16f + 0.14f * near : 0.12f + 0.06f * near) * burnK * fl;
            float wid = H * (foot ? 0.055f : 0.045f) * (0.8f + 0.2f * burnK);
            f.localScale = new Vector3(wid, Mathf.Max(0.01f, len), wid);
        }
        if (jetLight != null) { bool l = on; if (jetLight.enabled != l) jetLight.enabled = l; if (l) jetLight.intensity = burnK * 2.4f * (0.9f + Random.value * 0.2f); }
    }

    void Chevrons(float dt)
    {
        if (!rampHinge.gameObject.activeSelf || chevrons.Count == 0) return;
        float sp = phase == Ph.WalkUp ? -RampSpeed : RampSpeed;
        float ph = (Time.time * sp / Mathf.Max(1f, rampLen)) % 1f; if (ph < 0f) ph += 1f;
        for (int k = 0; k < chevrons.Count; k++)
        {
            float u = (ph + k / (float)chevrons.Count) % 1f;
            chevrons[k].localPosition = new Vector3(0f, 0f, u * rampBox.localScale.z);
            chevrons[k].localRotation = Quaternion.identity;
        }
    }

    // ---------------- endings ----------------
    void ToOrbit()
    {
        phase = Ph.Done;
        var go = new List<Frog>(crew);
        foreach (Frog f in go) { f.land75 = null; f.model.gameObject.SetActive(true); }
        crew.Clear(); walkers.Clear();
        if (SpaceWorld.I != null) SpaceWorld.I.Takeoff75(this, go);
        End("lifted off to orbit");
    }

    void SwapToRanchMech()
    {
        phase = Ph.Done;
        Swaps++;
        if (ship != null) { if (ship.parkedFor != null) ship.parkedFor = null; ship.parkedBody = -1; if (ship.mechForm == mech) ship.SetMechForm(null); }
        mech.Land75Place(C, yawDeg);
        Debug.Log("FF75 the ranch " + mech.Title + " stands where it landed (" + C.ToString("0") + ")");
        End("landed at the ranch");
    }

    void End(string why)
    {
        if (phase != Ph.Done) phase = Ph.Done;
        foreach (Frog f in new List<Frog>(crew))
        {
            Vector3 tp; if (!targets.TryGetValue(f, out tp)) { tp = F + fwd * 3f; tp.y = Ground(world, tp.x, tp.z) + 0.05f; }
            Release(f, tp + Vector3.up * 0.05f, yawDeg);
        }
        if (spot != null) { Interact.All.Remove(spot); spot = null; }
        All.Remove(this);
        Debug.Log("FF75 " + (mech != null ? mech.Title : "mech") + " landing on " + bodyId + " ends: " + why);
        Destroy(gameObject);
    }
    void OnDestroy() { All.Remove(this); if (spot != null) Interact.All.Remove(spot); foreach (var f in crew) if (f != null && f.land75 == this) { f.land75 = null; if (f.model != null) f.model.gameObject.SetActive(true); if (f.cc != null) f.cc.enabled = true; } }

    // ---------------- cameras (Game.Land75Views, after the normal cameras) ----------------
    public static bool View(Camera cam, ViewHud hud, Frog f)
    {
        if (cam == null || f == null) return false;
        MechLand75 m = Of(f);
        if (m == null || !m.WantsCam) return false;
        m.PoseCam(cam);
        if (f == m.pilot) m.Cam76(cam);
        if (hud != null)
        {
            string s = "";
            string nm = SpaceWorld.I != null && m.body >= 0 ? SpaceWorld.I.bodies[m.body].name.ToUpper() : m.bodyId.ToUpper();
            if (m.phase == Ph.Descend && m.t < 2.6f) s = "<size=56>" + (m.earth ? "LANDING AT THE RANCH" : "LANDING ON " + nm) + "</size>\n<size=24>" + m.mech.Title + "</size>";
            else if (m.phase == Ph.Touch) s = "<size=80>TOUCHDOWN!</size>";
            else if (m.phase == Ph.Ignite || (m.phase == Ph.Climb && m.t < 1.6f)) s = "<size=80>LIFTOFF!</size>";
            bool canSkip = m.phase == Ph.Descend || m.phase == Ph.WalkDown || m.phase == Ph.Ramp || m.phase == Ph.Climb || m.phase == Ph.WalkUp;
            hud.SetCenter(s + (canSkip ? "\n<size=20>A / FIRE skips</size>" : ""));
            hud.SetFade(m.fade);
        }
        return true;
    }

    void PoseCam(Camera cam)
    {
        Vector3 pos, look;
        Vector3 side = (Quaternion.Euler(0f, 62f, 0f) * fwd).normalized;
        bool rampShot = phase == Ph.Ramp || phase == Ph.RampUp;
        bool walkShot = (phase == Ph.WalkDown || phase == Ph.WalkUp) && walkers.Count > 0;
        if (walkShot)
        {
            // beside the lead walker, a little behind and above, so the froggy and the ramp under it read
            Frog lead = null; float best = -1e9f;
            foreach (Frog f in walkers) { float s = t - walkT0[f]; if (s >= 0f && s > best) { best = s; lead = f; } }
            if (lead == null) lead = walkers[0];
            Vector3 rd = (F - HingeW).normalized; if (phase == Ph.WalkUp) rd = -rd;
            Vector3 rgt = Vector3.Cross(Vector3.up, rd).normalized;
            Vector3 lp = lead.transform.position + Vector3.up * 0.8f;
            pos = lp + rgt * 6.5f + Vector3.up * 2.6f - rd * 5f;
            float gy = Ground(world, pos.x, pos.z) + 1f; if (pos.y < gy) pos.y = gy;
            look = lp + rd * 2.5f;
        }
        else if (rampShot)
        {
            Vector3 mid = Vector3.Lerp(HingeW, F, 0.5f);
            float d = rampLen * 0.75f + H * 0.35f + 10f;
            Vector3 sd = (Quaternion.Euler(0f, 80f, 0f) * fwd).normalized;
            pos = mid + sd * d + fwd * (d * 0.35f);
            pos.y = Ground(world, pos.x, pos.z) + Mathf.Max(3f, (HingeW.y - C.y) * 0.35f);
            look = Vector3.Lerp(HingeW, F, 0.45f);
        }
        else
        {
            float d = H * 1.7f + 34f;
            if (!F76.Old && descSide.sqrMagnitude > 0.5f && (phase == Ph.Descend || phase == Ph.Touch)) side = descSide;   // ffu76: holds still while the pilot steers
            if (Steer76Cam)
            {
                // ffu76b: the steering pilot sees the ground marker AND the mech: higher, looking down, pulled back with altitude
                float d2 = Mathf.Max(d, alt + H * 1.7f + 20f);
                look = C + Vector3.up * (alt * 0.45f + H * 0.3f);
                pos = C + side * (d2 * 0.9f);
                pos.y = look.y + d2 * 0.45f;
                float gy2 = Ground(world, pos.x, pos.z) + 3f; if (pos.y < gy2) pos.y = gy2;
            }
            else
            {
            pos = C + side * d;
            pos.y = Ground(world, pos.x, pos.z) + H * 0.12f + 3f;
            look = C + Vector3.up * (alt + H * 0.42f);
            }
        }
        float shake = (phase == Ph.Touch && t < 0.5f ? 0.25f : 0f) + (burnK > 0.1f && alt < H + 30f ? 0.08f * burnK : 0f);
        pos += Random.insideUnitSphere * shake;
        cam.transform.position = pos;
        cam.transform.rotation = Quaternion.LookRotation((look - pos).normalized);
        float need = (look - pos).magnitude + H * 2f + 50f;
        if (cam.farClipPlane < need) cam.farClipPlane = need;
    }

    // ---------------- world's Starship prop: hidden while a mech stands on that world and the Starship isn't there ----------------
    static readonly Dictionary<WorldId, Transform> props = new Dictionary<WorldId, Transform>();
    public static void RegisterStarProp(WorldId w, Transform prop) { if (prop != null) props[w] = prop; }
    public static bool StarPropShown(WorldId w)
    {
        if (F75.Old) return true;
        if (InWorld(w) == null) return true;
        StarLoc here = w == WorldId.Moon ? StarLoc.Moon : w == WorldId.Mars ? StarLoc.Mars : w == WorldId.Callisto ? StarLoc.Callisto : StarLoc.Pad;
        return SpaceWorld.StarAt == here;
    }
    public static void StaticTick()
    {
        foreach (var kv in props)
        {
            if (kv.Value == null) continue;
            bool on = StarPropShown(kv.Key);
            if (kv.Value.gameObject.activeSelf != on) { kv.Value.gameObject.SetActive(on); Debug.Log("FF75 " + kv.Key + " Starship prop " + (on ? "shown" : "hidden (a mech stands here, the Starship is " + SpaceWorld.StarWhere + ")")); }
        }
    }
    public static bool PropActive(WorldId w) { Transform p; return props.TryGetValue(w, out p) && p != null && p.gameObject.activeInHierarchy; }
    // build the prop group, kept out of the world's mesh merge so it can be hidden
    public static Transform PropGroup(Transform parent, WorldId w)
    {
        var g = Mats.Node(parent, "StarshipProp75", Vector3.zero);
        RegisterStarProp(w, g);
        return g;
    }
    public static void BeforeMerge(Transform g, bool on) { if (g == null) return; foreach (var r in g.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = on; }
}

public partial class SpaceWorld
{
    // a ship in mech form lands on a world: the mech itself lands (MechLand75)
    void Land75(Starship s, int bodyIdx, List<Frog> crew, Frog pilot)
    {
        Body b = bodies[bodyIdx];
        StoryMech mech = s.mechForm;
        bool earth = b.id == "earth";
        if (pilot == null && crew.Count > 0) pilot = crew[0];
        s.parkedFor = pilot; s.parkedBody = bodyIdx;   // keeps the ship in use: its mech stands on the world, not in orbit
        WorldId w = earth ? WorldId.Ranch : WorldOfLoc(LocOf(b.id));
        int k = 0;
        foreach (Frog f in crew)
        {
            if (earth)
            {
                Vector2 pc = Layout.PadC;
                float x = pc.x + 16f, z = pc.y - 4.5f + k * 3f;
                f.SendTo(WorldId.Ranch, new Vector3(x, Ranch.GY(x, z) + 1.2f, z), 90f);
            }
            else if (b.id == "mars") SurfaceWorlds.LandMars(f, k);
            else if (b.id == "moon") MoonWorld.Land(f, k);
            else SurfaceWorlds.LandCallisto(f, k);
            k++;
        }
        MechLand75.Begin(s, mech, b.id, bodyIdx, w, crew, pilot);
        Debug.Log("FF75 " + mech.Title + " lands on " + b.name + " itself (no Starship) with " + crew.Count + " aboard");
    }

    // the mech lifted off a world: back in that body's orbit, the pilot flying it, the rest riding
    public void Takeoff75(MechLand75 ml, List<Frog> go)
    {
        Starship s = ml.ship;
        if (s == null) return;
        Body b = bodies[ml.body];
        s.parkedFor = null; s.parkedBody = -1;
        if (s.mechForm != ml.mech) s.SetMechForm(ml.mech);
        s.EnterOrbit(ml.body, b.cap, Random.value * 6.28f);
        Frog p = ml.pilot != null && go.Contains(ml.pilot) ? ml.pilot : (go.Count > 0 ? go[0] : null);
        if (p != null)
        {
            Fly72(p, s, "took off from " + b.name + " in the mech");
            p.Toast("Back in " + b.name + " orbit in " + ml.mech.Title + ". Earth is the next target.", 3.5f);
        }
        foreach (Frog f in go) if (f != null && f != p) { Ride72(f, s, "rode the mech up from " + b.name); f.Toast("Back in " + b.name + " orbit aboard " + ml.mech.Title, 3f); }
        if (s.bridge73 != null) s.bridge73.SeatRiders(s, null);
        s.SetTarget(Find("earth"));
        Sfx.Play(Sfx.Boom, 0.7f, 0.7f);
    }
}

public partial class StoryMech
{
    // ffu75: the mech landed at the ranch by itself - the real one stands where the copy touched down (empty)
    public void Land75Place(Vector3 p, float yawDeg)
    {
        Back72();
        if (wrecked) return;
        DemoPlace(p, yawDeg);
        vy = 0f; grounded = true; Altitude = 0f; speed = 0f; flameK = 0f; aimK = 0f;
    }
}

public partial class Game
{
    // after LaunchSeq.View: the landing / takeoff camera for froggies held by a mech (their own view, or the shared view)
    void Land75Views()
    {
        if (MechLand75.All.Count == 0) return;
        foreach (var s in slots) if (s.cam != null && s.cam.enabled) MechLand75.View(s.cam, s.hud, frogs[s.frog]);
        if (sharedCam != null && sharedCam.enabled)
        {
            foreach (var s in slots) if (MechLand75.View(sharedCam, sharedHud, frogs[s.frog])) break;
        }
    }
}
