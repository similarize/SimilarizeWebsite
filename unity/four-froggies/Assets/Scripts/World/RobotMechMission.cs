using System.Collections.Generic;
using UnityEngine;

// ffu26 robot MECH missions (Bill: "robots can get inside the mechs and pilot them, including taking mechs to space ...
// fly to Mars or Callisto in the mech, collect rocks with the mech's hands or the cargo bay, and return"). Fills the
// ffu20 gap ("mission via a story mech to space: NOT done").
//   walk to the mech -> climb into its glass cockpit (StoryMechRobot.cs canopy) -> crouch, rockets -> scripted climb of
//   the REAL ranch mech -> in space it stays a mech: a space-scale copy in the ffu15 Superman pose with back rockets and
//   the cargo bay (MechCraft below) flies the live Earth -> Mars / Callisto transfer -> stands up and lands on the
//   surface -> walks to each rock, bends down, grabs it with the hand, swings it over the shoulder into the open cargo
//   bay -> back to space -> transfer home -> the real mech lands beside the ROBOT MISSION PAD (10 / 100-story; the
//   giants land at their own spot) -> rocks are tossed onto the display pile -> the robot climbs out.
//   "Mech space patrol": climb, orbit Earth for a while, land. "Recall mech": cuts any of it short and heads home.
// While the copy is in space the real mech waits hidden under its spot with the robot's pilot still in it, so nobody
// can take it (and a mech a player is in can't be taken by a robot). On the ranch the real mech stays a normal
// target: player weapons damage it, a lost limb topples it / a knock-out ejects the robot and ends the mission (ffu21).
// One robot mission at a time (mission ship or mech). Local per device, like ffu20.
public static class MechPilot
{
    // the robot sits in the mech's glass cockpit, sized to the canopy
    public static void Seat(Robot r, StoryMech m) { SeatOn(r, m, m.CanopySeat, m.CanopyRobotH); }

    public static void SeatOn(Robot r, Vehicle v, Transform seat, float worldH)
    {
        r.seatedIn = v;
        r.transform.SetParent(seat, false);
        float ls = Mathf.Max(1e-4f, seat.lossyScale.x);
        r.transform.localScale = Vector3.one * (worldH / Mathf.Max(0.3f, r.height) / ls);
        r.transform.localPosition = new Vector3(0f, -0.4f * worldH / ls, 0f);
        r.transform.localRotation = Quaternion.identity;
        r.ExtSpeed(0f); r.ExtPose(9);
        r.SetHidden(false);
    }
}

// the mech as it flies in space / walks on Mars: same armoured body (StoryMech.BuildBodyCopy), drawn 16 m tall in space
// (planets are 10x scaled, like ffu15's mech-in-space) and at mech size (12..28 m) on the surface
public class MechCraft : MonoBehaviour
{
    public const float VisH = 16f;
    public Transform pose, hips, legL, legR, torso, armL, armR, head, hand, bayPoint, seat;
    public float robotH, reachX, reachZ, reachY;
    readonly Transform[] flames = new Transform[2];
    readonly Transform[] doors = new Transform[2];
    GameObject bayGlow;
    Light jetLight;
    AudioSource roar;
    public float flame, stand, bay, scale = 1f;
    public int owner, band;

    public static MechCraft Build(StoryMech m)
    {
        var go = new GameObject("Robot mech craft (" + m.Title + ")");
        var c = go.AddComponent<MechCraft>();
        c.owner = m.owner; c.band = m.band;
        float H = VisH;
        c.pose = Mats.Node(go.transform, "Pose", Vector3.zero);
        var parts = StoryMech.BuildBodyCopy(c.pose, m.owner, m.band, H);
        c.hips = parts[0]; c.legL = parts[1]; c.legR = parts[2]; c.torso = parts[3]; c.armL = parts[4]; c.armR = parts[5]; c.head = parts[6];
        Material gun = Mats.Steel(new Color(0.22f, 0.23f, 0.26f));
        Mats.Prim(PrimitiveType.Cylinder, c.armR, new Vector3(0f, -0.33f * H, 0.005f * H), new Vector3(0.05f * H, 0.07f * H, 0.05f * H), gun);
        c.hand = Mats.Node(c.armL, "Hand", new Vector3(0f, -0.36f * H, 0.02f * H));   // the free (left) hand grabs; the right arm is the cannon
        // back rockets
        Material nozzle = Mats.Steel(new Color(0.35f, 0.36f, 0.4f));
        Material outer = new Material(Mats.Fx); outer.color = new Color(1f, 0.55f, 0.18f, 0.55f);
        Material core = Mats.Unlit(new Color(1f, 0.95f, 0.75f));
        for (int s = 0; s < 2; s++)
        {
            float x = (s == 0 ? -1f : 1f) * 0.055f * H;
            Vector3 np = new Vector3(x, 0.105f * H, -0.12f * H);
            Mats.Prim(PrimitiveType.Cylinder, c.torso, np + new Vector3(0f, -0.01f * H, 0f), new Vector3(0.05f * H, 0.014f * H, 0.05f * H), nozzle);
            Transform f = Mats.Node(c.torso, "Flame", np + new Vector3(0f, -0.025f * H, 0f));
            f.localRotation = Quaternion.Euler(35f, 0f, 0f);
            Mats.Prim(PrimitiveType.Capsule, f, new Vector3(0f, -0.5f, 0f), new Vector3(0.62f, 0.55f, 0.62f), outer);
            Mats.Prim(PrimitiveType.Capsule, f, new Vector3(0f, -0.4f, 0f), new Vector3(0.3f, 0.45f, 0.3f), core);
            f.localScale = Vector3.zero;
            c.flames[s] = f;
        }
        var lg = new GameObject("JetLight"); lg.transform.SetParent(c.torso, false); lg.transform.localPosition = new Vector3(0f, 0f, -0.3f * H);
        c.jetLight = lg.AddComponent<Light>(); c.jetLight.type = LightType.Point; c.jetLight.color = new Color(1f, 0.6f, 0.3f); c.jetLight.range = H * 1.4f; c.jetLight.shadows = LightShadows.None; c.jetLight.enabled = false;
        // cargo bay on the back (ffu15 look): lit interior + two doors hinged at the outer edges
        Material door = Mats.Shiny(Color.Lerp(Froggies.Color(m.owner), Color.white, 0.25f)), dark = Mats.Lit(new Color(0.08f, 0.09f, 0.1f));
        Vector3 bc = new Vector3(0f, 0.2f * H, -0.15f * H);
        c.bayGlow = new GameObject("BayInside"); c.bayGlow.transform.SetParent(c.torso, false);
        Mats.Prim(PrimitiveType.Cube, c.bayGlow.transform, bc + new Vector3(0f, 0f, 0.006f * H), new Vector3(0.17f * H, 0.15f * H, 0.004f * H), dark);
        Mats.Prim(PrimitiveType.Cube, c.bayGlow.transform, bc + new Vector3(0f, 0.065f * H, 0.002f * H), new Vector3(0.15f * H, 0.008f * H, 0.004f * H), Mats.Unlit(new Color(0.55f, 0.95f, 1f)));
        c.bayGlow.SetActive(false);
        for (int s = 0; s < 2; s++)
        {
            float sx = s == 0 ? -1f : 1f;
            Transform hinge = Mats.Node(c.torso, s == 0 ? "BayDoorL" : "BayDoorR", bc + new Vector3(sx * 0.09f * H, 0f, -0.004f * H));
            Mats.Prim(PrimitiveType.Cube, hinge, new Vector3(-sx * 0.045f * H, 0f, 0f), new Vector3(0.09f * H, 0.16f * H, 0.012f * H), door);
            Mats.Prim(PrimitiveType.Cube, hinge, new Vector3(-sx * 0.045f * H, 0.07f * H, -0.007f * H), new Vector3(0.085f * H, 0.006f * H, 0.003f * H), Mats.Unlit(new Color(1f, 0.75f, 0.2f)));
            c.doors[s] = hinge;
        }
        c.bayPoint = Mats.Node(c.torso, "BayPoint", bc + new Vector3(0f, -0.02f * H, -0.01f * H));
        // the robot's glass cockpit on the head
        float R;
        StoryMech.AddCanopy(c.head, H, out c.seat, out R);
        c.robotH = R * 1.55f;
        Mats.SetLayer(go, Vehicle.VehicleLayer);
        Mats.NoShadows(go);
        c.roar = Sfx.Loop(go, Sfx.EngRocket != null ? Sfx.EngRocket : Sfx.Rotor);
        // how far ahead / how low the hand reaches when bending down (for picking rocks)
        c.SetStand(1f);
        c.torso.localRotation = Quaternion.Euler(48f, 0f, 0f);
        c.armL.localRotation = Quaternion.Euler(-40f, 0f, 8f);
        Vector3 hp = go.transform.InverseTransformPoint(c.hand.position);
        c.reachX = hp.x; c.reachZ = hp.z; c.reachY = hp.y;
        c.torso.localRotation = Quaternion.identity; c.armL.localRotation = Quaternion.identity;
        return c;
    }

    // 0 = Superman flight pose (body along +z, head first), 1 = standing on its feet
    public void SetStand(float k)
    {
        stand = k;
        float H = VisH;
        pose.localPosition = Vector3.Lerp(new Vector3(0f, 0f, -H * 0.5f), Vector3.zero, k);
        pose.localRotation = Quaternion.Slerp(Quaternion.Euler(90f, 0f, 0f), Quaternion.identity, k);
    }

    public void SetScale(float s) { scale = s; transform.localScale = Vector3.one * s; }
    public float WorldH { get { return VisH * scale; } }

    // flight look: arms forward (above the head), legs straight back with a flutter
    public void FlyPose(float dt)
    {
        float t = Time.time;
        armR.localRotation = Quaternion.Slerp(armR.localRotation, Quaternion.Euler(172f, 0f, -6f), dt * 3f);
        armL.localRotation = Quaternion.Slerp(armL.localRotation, Quaternion.Euler(165f, 0f, 9f), dt * 3f);
        legL.localRotation = Quaternion.Euler(Mathf.Sin(t * 2.2f) * 4f, 0f, 0f);
        legR.localRotation = Quaternion.Euler(-Mathf.Sin(t * 2.2f) * 4f, 0f, 0f);
        torso.localRotation = Quaternion.Slerp(torso.localRotation, Quaternion.identity, dt * 3f);
    }

    public void Tick(float dt, bool heard)
    {
        float H = VisH;
        for (int i = 0; i < 2; i++)
        {
            float fl = 1f + Mathf.Sin(Time.time * (41f + i * 7f)) * 0.1f;
            float len = H * (0.1f + 0.22f * flame) * fl, wid = H * (0.05f + 0.025f * flame);
            flames[i].localScale = flame > 0.02f ? new Vector3(wid, len, wid) : Vector3.zero;
        }
        jetLight.enabled = flame > 0.05f && !Look.Mobile; jetLight.intensity = flame * 2.5f;
        float a = bay * 110f;
        if (doors[0] != null) doors[0].localRotation = Quaternion.Euler(0f, a, 0f);
        if (doors[1] != null) doors[1].localRotation = Quaternion.Euler(0f, -a, 0f);
        bool glow = bay > 0.05f;
        if (bayGlow.activeSelf != glow) bayGlow.SetActive(glow);
        if (roar != null)
        {
            float k = Mathf.Clamp01(1f - Sfx.Near(transform.position) / 500f);
            float vol = flame > 0.05f ? Mathf.Max(k * k * 0.7f, heard ? 0.22f : 0f) * Mathf.Clamp01(flame + 0.2f) : 0f;
            roar.volume = Mathf.MoveTowards(roar.volume, vol, dt * 1.5f);
            roar.pitch = 0.7f + flame * 0.25f;
            if (roar.volume > 0.004f) { if (!roar.isPlaying) roar.Play(); } else if (roar.isPlaying) roar.Stop();
        }
    }
}

public class RobotMechMission
{
    public static RobotMechMission Active;
    public readonly Robot r;
    public readonly StoryMech m;
    public readonly int kind;            // 0 Mars rocks, 1 Callisto ice, 2 space patrol (orbit Earth)
    public readonly int want;
    public int got, phase;
    public float fuel = 1f;
    float t;
    Frog user;
    MechCraft craft;
    Vector3 launchP, landP, site;
    float launchYaw, landYaw, descH0 = 360f, orbitA0, distLine = -1f;
    bool aborted, seatedAtStart;
    readonly List<Transform> rocks = new List<Transform>();
    readonly List<Transform> carried = new List<Transform>();
    int pickIdx = -1, cs; float csT, walkPh;
    Vector3 camPos; Quaternion camRot; bool camSnap = true;
    Transform flying; Vector3 flyFrom; int unloadN;
    float unloadT;

    public const int Walk = 0, Board = 1, Prep = 2, Ascent = 3, TransitOut = 4, Orbit = 5, Descent = 6, Collect = 7, Ascent2 = 8,
                     TransitBack = 9, Descent2 = 10, Unload = 11, Exit = 12;
    const float AscentT = 10f, TransitT = 22f, OrbitT = 30f, DescentT = 9f, Ascent2T = 7f, Descent2T = 10f;

    public string Name { get { return kind == 0 ? "MECH TO MARS" : kind == 1 ? "MECH TO CALLISTO" : "MECH SPACE PATROL"; } }
    public string Dest { get { return kind == 0 ? "Mars" : kind == 1 ? "Callisto" : "Earth orbit"; } }
    public string Unit { get { return kind == 0 ? "rocks" : "ice chunks"; } }
    WorldId DestWorld { get { return kind == 0 ? WorldId.Mars : WorldId.Callisto; } }
    float H { get { return m.height; } }
    float SurfH { get { return Mathf.Clamp(m.height, 12f, 28f); } }

    public static RobotMechMission For(Robot r) { return Active != null && Active.r == r ? Active : null; }
    public static bool TickFor(Robot r, float dt) { if (Active == null || Active.r != r) return false; Active.Tick(dt); return true; }

    RobotMechMission(Robot robot, StoryMech mech, int k, int n, Frog from) { r = robot; m = mech; kind = k; want = n; user = from; }

    // phone: MISSION > Mech to Mars rocks / Mech to Callisto / Mech space patrol (mech = the one picked in DRIVE, the one the
    // robot is already piloting, or the nearest free 10-story)
    public static string Start(Robot r, StoryMech mech, int kind, int n, Frog from)
    {
        if (SpaceWorld.I == null || SurfaceWorlds.I == null) return "No space here";
        if (Active != null) return "A robot is already on a mech mission (" + Active.r.robotName + " - " + Active.PhaseLine + ")";
        if (RobotMission.Active != null) return "One mission at a time - " + RobotMission.Active.r.robotName + " is on the " + RobotMission.Active.Name.ToLower();
        if (r.mission != null) return r.robotName + " is on a mission";
        if (r.battery < 0.35f) return r.robotName + " needs a charge first (" + r.Pct + ")";
        if (mech == null) return "No free mech for " + r.robotName;
        if (mech.wrecked || mech.PartsBusy) return mech.Title + " is knocked down - try another";
        Frog pl = r.Pilot;
        if (mech.driver != null && mech.driver != pl) return mech.Title + " is taken (" + mech.driver.nick + ")";
        if (r.manual != null) r.ReleaseManual(false);
        bool seated = r.drv != null && r.drv.v == mech && r.drv.phase == 1 && pl.vehicle == mech;
        if (seated) r.drv = null;                       // already in the cockpit: the mission takes over the controls
        else { if (r.drv != null) r.EndDrive(null); r.ResetForMission(); }
        var mm = new RobotMechMission(r, mech, kind, n, from);
        mm.seatedAtStart = seated;
        Active = mm;
        r.cmd = "mechmission";
        mm.phase = seated ? Prep : Walk;
        Sfx.Play(Sfx.Confirm != null ? Sfx.Confirm : Sfx.Pickup, 0.6f);
        return r.robotName + ": " + mm.Name + " in " + mech.Title + (kind == 2 ? " - orbiting Earth!" : " - bringing back " + n + " " + mm.Unit + "!");
    }

    public string Recall()
    {
        if (phase <= Prep) { End(false, true); return r.robotName + ": mech mission scrubbed"; }
        if (aborted || phase >= TransitBack) return r.robotName + ": already heading home";
        aborted = true;
        if (phase == Ascent)
        {
            // fly straight back down where it took off
            float h = m.transform.position.y - Ranch.GY(launchP.x, launchP.z);
            landP = launchP; landYaw = launchYaw; descH0 = Mathf.Max(5f, h);
            Go(Descent2);
        }
        else if (phase == TransitOut) { float done = Mathf.Clamp01(t / TransitT); Go(TransitBack); t = (1f - done) * TransitT; }
        else if (phase == Orbit) BeginDescent2();
        return r.robotName + ": recalling the mech - heading home" + (got > 0 ? " with " + got + " " + Unit : "");
    }

    // thrown out of the mech (knocked out / lost a leg / the robot got blown up): mission over, the mech stays where it is
    public void Eject(string why)
    {
        Frog pl = Pilot;
        bool inSpace = !OnRanchMech && phase > Prep;
        Vector3 at = pl.vehicle != m ? pl.transform.position : m.transform.position + m.transform.right * (H * 0.3f + 3f);
        if (inSpace) { Vector3 sp = m.SpawnPoint; at = sp + Vector3.right * (H * 0.3f + 3f); }
        if (pl.vehicle == m) pl.ExitVehicle();
        pl.cc.enabled = false;
        r.world = WorldId.Ranch;
        if (m.scripted && !inSpace) m.ScriptEnd(false);
        r.Unseat(at);
        FX.Sparkle(r.transform.position + Vector3.up, new Color(1f, 0.8f, 0.3f), 14);
        ToastAll(r.robotName + (why != null ? " " + why + " - " : " ejected from " + m.Title + "! ") + "Mech mission over" + (got > 0 ? " (" + got + " " + Unit + " lost)" : ""));
        End(true, false);
    }

    void Go(int ph) { phase = ph; t = 0f; cs = 0; csT = 0f; r.ExtReset(); }
    Frog Pilot { get { return r.Pilot; } }

    static void ToastAll(string s, float d = 3.5f)
    {
        if (Game.I == null) return;
        foreach (Frog f in Game.I.frogs) if (f != null && f.human) f.Toast(s, d);
    }

    public string PhaseLine
    {
        get
        {
            switch (phase)
            {
                case Walk: return "walking to " + m.Title;
                case Board: return "climbing into the cockpit";
                case Prep: return "mech launch T-" + Mathf.Max(0, Mathf.CeilToInt(2.5f - t));
                case Ascent: return "mech climbing to space";
                case TransitOut: return "mech flying to " + Dest + " " + Mathf.RoundToInt(Mathf.Clamp01(t / TransitT) * 100f) + "%";
                case Orbit: return "mech patrolling Earth orbit";
                case Descent: return "mech landing on " + Dest;
                case Collect: return "mech collecting " + got + "/" + want;
                case Ascent2: return "mech lifting off " + Dest;
                case TransitBack: return "mech flying home " + Mathf.RoundToInt(Mathf.Clamp01(t / TransitT) * 100f) + "%";
                case Descent2: return "mech landing at the ranch";
                case Unload: return "unloading " + got + " " + Unit;
                case Exit: return "climbing out";
            }
            return "done";
        }
    }
    public string DistLine { get { return distLine < 0f ? "" : distLine >= 1000f ? (distLine / 1000f).ToString("0.0") + " km" : Mathf.RoundToInt(distLine) + " m"; } }
    public string DistWhat
    {
        get
        {
            switch (phase)
            {
                case Walk: return "to mech";
                case Ascent: case Descent: case Descent2: case Ascent2: return "altitude";
                case TransitOut: return "to " + Dest;
                case TransitBack: return "to Earth";
                case Orbit: return "above Earth";
                case Collect: return "to next " + (kind == 0 ? "rock" : "ice");
            }
            return "";
        }
    }
    public bool OnRanchMech { get { return phase >= Board && phase <= Ascent || phase >= Descent2; } }

    // ---------- per frame (from Robot.Update) ----------
    public void Tick(float dt)
    {
        t += dt;
        distLine = -1f;
        Frog pl = Pilot;
        bool heard = RobotFeed.Watching(r);
        // ffu21 interplay: on the ranch the mech can be shot; a knock-out or a lost leg throws the robot out
        if (phase >= Prep && OnRanchMech && phase != Exit && (m.wrecked || m.PartsBusy || pl.vehicle != m)) { Eject(null); return; }
        if (craft != null) craft.Tick(dt, heard);
        switch (phase)
        {
            case Walk:
                {
                    r.world = WorldId.Ranch;
                    if (m.driver != null && m.driver != pl) { Fail(m.Title + " got taken (" + m.driver.nick + ")"); return; }
                    if (m.wrecked || m.PartsBusy) { Fail(m.Title + " is knocked down"); return; }
                    Vector3 cp = m.body.ClosestPoint(r.transform.position + Vector3.up);
                    Vector3 d = r.transform.position - cp; d.y = 0f;
                    if (d.sqrMagnitude < 0.01f) d = -m.transform.right;
                    Vector3 goal = cp + d.normalized * 1.6f;
                    distLine = RobotNav.Flat(goal - r.transform.position);
                    bool there = r.ExtWalk(goal, r.RunSpeed, 0.6f, dt);
                    if (there || distLine < 1.2f || t > 60f) Go(Board);
                    break;
                }
            case Board:
                {
                    if (!m.CanEnter(pl)) { Fail("can't get into " + m.Title); return; }
                    pl.world = WorldId.Ranch;
                    pl.EnterVehicle(m);
                    if (pl.vehicle != m) { Fail(m.Title + " is taken"); return; }
                    MechPilot.Seat(r, m);
                    FX.Sparkle(m.CanopySeat.position, new Color(0.5f, 1f, 0.6f), 16);
                    Sfx.PlayAt(Sfx.Door, m.CanopySeat.position, 0.7f, 80f);
                    Go(Prep);
                    break;
                }
            case Prep:
                {
                    if (t < dt * 1.5f)
                    {
                        if (seatedAtStart) MechPilot.Seat(r, m);
                        m.scripted = true; m.scriptAir = 0f;
                        launchP = m.transform.position; launchP.y = Ranch.GY(launchP.x, launchP.z);
                        launchYaw = m.transform.eulerAngles.y;
                        ToastAll(r.robotName + " is taking " + m.Title + " to " + (kind == 2 ? "space" : Dest) + "!");
                    }
                    m.scriptFlame = t > 1.4f ? 0.5f : 0f;
                    if (Random.value < 0.5f) FX.Dust(launchP + new Vector3(Random.Range(-1f, 1f) * H * 0.2f, 0.4f, Random.Range(-1f, 1f) * H * 0.2f), 1f);
                    if (t >= 2.5f) { Go(Ascent); Sfx.PlayAt(Sfx.Boom, launchP, 1f, 400f + H * 3f, 0.6f); }
                    break;
                }
            case Ascent:
                {
                    float h = 3.6f * t * t + 3f * t;
                    m.scriptAir = 1f; m.scriptFlame = 1.5f;
                    m.ScriptPlace(launchP + Vector3.up * h, launchYaw);
                    fuel = Mathf.Max(0f, fuel - dt * 0.012f);
                    distLine = h;
                    if (h < H * 1.5f && Random.value < 0.8f) FX.Dust(launchP + new Vector3(Random.Range(-1f, 1f) * H * 0.3f, 0.5f, Random.Range(-1f, 1f) * H * 0.3f), 1f);
                    if (Random.value < 0.25f) Game.Shake(m.transform.position, 0.1f + m.band * 0.05f);
                    if (t >= AscentT) ToSpace();
                    break;
                }
            case TransitOut:
            case TransitBack:
                {
                    var W = SpaceWorld.I;
                    int e = W.Find("earth"), d = W.Find(kind == 0 ? "mars" : "callisto");
                    int from = phase == TransitOut ? e : d, to = phase == TransitOut ? d : e;
                    Vector3 A = W.PosAt(from, W.simT), B = W.PosAt(to, W.simT);
                    Vector3 dir = B - A; dir.y = 0f; float span = dir.magnitude; dir /= Mathf.Max(1f, span);
                    Vector3 side = Vector3.Cross(Vector3.up, dir);
                    Vector3 p0 = A + dir * W.bodies[from].cap, p3 = B - dir * W.bodies[to].cap;
                    Vector3 p1 = p0 + dir * span * 0.3f - side * span * 0.18f, p2 = p3 - dir * span * 0.3f - side * span * 0.1f;
                    float u = Mathf.Clamp01(t / TransitT), s = u * u * (3f - 2f * u);
                    Vector3 pos = Bez(p0, p1, p2, p3, s), ahead = Bez(p0, p1, p2, p3, Mathf.Min(1f, s + 0.01f)) - pos;
                    craft.transform.position = pos;
                    if (ahead.sqrMagnitude > 1e-6f) craft.transform.rotation = Quaternion.Slerp(craft.transform.rotation, Quaternion.LookRotation(ahead.normalized, Vector3.up), Mathf.Min(1f, dt * 3f));
                    craft.SetStand(0f); craft.FlyPose(dt);
                    craft.flame = u < 0.12f || u > 0.88f ? 1.4f : 0.45f;
                    fuel = Mathf.Max(0f, fuel - dt * craft.flame * 0.004f);
                    distLine = (p3 - pos).magnitude * 10f;
                    if (u >= 1f)
                    {
                        if (phase == TransitOut)
                        {
                            if (kind == 0) SurfaceWorlds.I.EnsureMars(); else SurfaceWorlds.I.EnsureCallisto();
                            site = kind == 0 ? Worlds.MarsO + new Vector3(-34f, 0f, -52f) : Worlds.CallistoO + new Vector3(30f, 0f, -28f);
                            site.y = GroundAt(site.x, site.z);
                            r.world = DestWorld;
                            craft.SetScale(SurfH / MechCraft.VisH);
                            Go(Descent);
                        }
                        else BeginDescent2();
                    }
                    break;
                }
            case Orbit:
                {
                    var W = SpaceWorld.I;
                    int e = W.Find("earth");
                    Vector3 E = W.PosAt(e, W.simT);
                    float R = Mathf.Max(W.bodies[e].cap * 1.15f, W.bodies[e].r * 1.6f);
                    float a = orbitA0 + t * 0.21f;
                    Vector3 pos = E + new Vector3(Mathf.Cos(a), Mathf.Sin(a * 0.5f) * 0.12f, Mathf.Sin(a)) * R;
                    Vector3 tan = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                    craft.transform.position = pos;
                    craft.transform.rotation = Quaternion.Slerp(craft.transform.rotation, Quaternion.LookRotation(tan, (pos - E).normalized), Mathf.Min(1f, dt * 3f));
                    craft.SetStand(0f); craft.FlyPose(dt);
                    craft.flame = 0.35f + 0.15f * Mathf.Sin(t * 0.7f);
                    fuel = Mathf.Max(0f, fuel - dt * 0.002f);
                    distLine = (R - W.bodies[e].r) * 10f;
                    if (t >= OrbitT) BeginDescent2();
                    break;
                }
            case Descent:
                {
                    float u = Mathf.Clamp01(t / DescentT), k = 1f - u;
                    float h = 160f * k * k * k;
                    craft.transform.position = site + Vector3.up * h;
                    craft.transform.rotation = Quaternion.Euler(0f, 150f, 0f);
                    craft.SetStand(Mathf.Clamp01(t / 3f));
                    if (craft.stand < 1f) craft.FlyPose(dt); else StandPose(dt, 0f);
                    craft.flame = h > 0.1f ? 0.6f + 0.6f * (1f - Mathf.Clamp01(h / 40f)) : 0f;
                    fuel = Mathf.Max(0f, fuel - dt * 0.006f);
                    distLine = h;
                    if (h < 25f && Random.value < 0.7f) FX.Dust(site + new Vector3(Random.Range(-1f, 1f) * SurfH * 0.4f, 0.3f, Random.Range(-1f, 1f) * SurfH * 0.4f), 2.5f);
                    if (u >= 1f)
                    {
                        craft.flame = 0f;
                        Sfx.PlayAt(Sfx.Thud, site, 1f, 300f, 0.5f);
                        for (int i = 0; i < 14; i++) FX.Dust(site + Random.insideUnitSphere * SurfH * 0.4f, 2f);
                        SpawnRocks();
                        Go(aborted ? Ascent2 : Collect);
                    }
                    break;
                }
            case Collect: CollectTick(dt); break;
            case Ascent2:
                {
                    craft.bay = Mathf.MoveTowards(craft.bay, 0f, dt * 1.2f);
                    float h = t < 1.2f ? 0f : 3.2f * (t - 1.2f) * (t - 1.2f) + 2f * (t - 1.2f);
                    craft.transform.position = site + Vector3.up * h;
                    craft.SetStand(1f - Mathf.Clamp01((t - 2.5f) / 2.5f));
                    if (craft.stand < 1f) craft.FlyPose(dt); else StandPose(dt, 0f);
                    craft.flame = t < 0.8f ? 0.3f : 1.4f;
                    fuel = Mathf.Max(0f, fuel - dt * 0.01f);
                    distLine = h;
                    if (h < 20f && Random.value < 0.7f) FX.Dust(site + new Vector3(Random.Range(-1f, 1f) * SurfH * 0.4f, 0.3f, Random.Range(-1f, 1f) * SurfH * 0.4f), 2.5f);
                    if (t >= Ascent2T)
                    {
                        foreach (var rk in rocks) if (rk != null) Object.Destroy(rk.gameObject);
                        rocks.Clear();
                        r.world = WorldId.Space;
                        craft.SetScale(1f);
                        Go(TransitBack);
                    }
                    break;
                }
            case Descent2:
                {
                    float u = Mathf.Clamp01(t / Descent2T), k = 1f - u;
                    float h = descH0 * k * k * k;
                    m.scriptAir = h > 0.4f ? 1f : 0f;
                    m.scriptFlame = h > 0.2f ? 0.7f + 0.7f * (1f - Mathf.Clamp01(h / 60f)) : 0f;
                    m.ScriptPlace(landP + Vector3.up * h, landYaw);
                    fuel = Mathf.Max(0f, fuel - dt * 0.006f);
                    distLine = h;
                    if (h < H && Random.value < 0.8f) FX.Dust(landP + new Vector3(Random.Range(-1f, 1f) * H * 0.3f, 0.5f, Random.Range(-1f, 1f) * H * 0.3f), 1f);
                    if (u >= 1f)
                    {
                        m.ScriptEnd(true);
                        Game.Shake(landP, 0.5f + m.band * 0.3f);
                        Sfx.PlayAt(Sfx.Land != null ? Sfx.Land : Sfx.Thud, landP, 1f, 200f + H * 2f, 0.6f);
                        for (int i = 0; i < 16; i++) { Vector2 q = Random.insideUnitCircle.normalized * H * Random.Range(0.15f, 0.4f); FX.Dust(landP + new Vector3(q.x, 0.5f, q.y), 1f); }
                        Go(kind == 2 || got == 0 ? Exit : Unload);
                        unloadN = 0; unloadT = 0.6f;
                    }
                    break;
                }
            case Unload: UnloadTick(dt); break;
            case Exit:
                {
                    if (t < 1.0f) break;
                    string msg = kind == 2 ? r.robotName + " is back from the mech space patrol in " + m.Title + "!"
                        : r.robotName + " is back from " + Dest + " in " + m.Title + " with " + got + " " + Unit + "! (" + MissionSite.count[kind] + " on display)";
                    ToastAll(msg);
                    Sfx.Play(Sfx.Win, 0.5f);
                    End(false, true);
                    break;
                }
        }
    }

    void ToSpace()
    {
        SpaceWorld.I.EnsureBuilt();
        if (craft == null) craft = MechCraft.Build(m);
        craft.gameObject.SetActive(true);
        craft.SetScale(1f); craft.SetStand(0f); craft.flame = 1.4f; craft.bay = 0f;
        var W = SpaceWorld.I;
        int e = W.Find("earth");
        Vector3 E = W.PosAt(e, W.simT);
        craft.transform.position = E + Vector3.up * W.bodies[e].cap;
        craft.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
        orbitA0 = Random.value * 6.28f;
        // the real mech waits hidden under its spot (pilot still aboard: nobody else can take it)
        Vector3 sp = m.SpawnPoint;
        m.scriptFlame = 0f; m.scriptAir = 0f;
        m.ScriptPlace(new Vector3(sp.x, Ranch.GY(sp.x, sp.z) - 1500f - H, sp.z), launchYaw);
        r.world = WorldId.Space;
        MechPilot.SeatOn(r, m, craft.seat, craft.robotH);
        camSnap = true;
        Go(kind == 2 ? Orbit : TransitOut);
    }

    void BeginDescent2()
    {
        // back to the ranch: the 10 / 100-story mechs land beside the ROBOT MISSION PAD, the giants on their own spot
        if (!aborted || landP == Vector3.zero)
        {
            Vector2 pc = MissionSite.PadC;
            if (m.band <= 1) { float x = pc.x + 14f + H * 0.32f, z = pc.y - 2f; landP = new Vector3(x, Ranch.GY(x, z), z); landYaw = -90f; }   // east of the pad (the Starship pad is to the west)
            else { Vector3 sp = m.SpawnPoint; landP = new Vector3(sp.x, Ranch.GY(sp.x, sp.z), sp.z); landYaw = launchYaw; }
        }
        descH0 = 360f;
        if (craft != null) craft.gameObject.SetActive(false);
        r.world = WorldId.Ranch;
        m.ScriptPlace(landP + Vector3.up * descH0, landYaw);
        MechPilot.Seat(r, m);
        camSnap = true;
        Go(Descent2);
    }

    // ---------- on the surface: walk, bend, grab with the hand, swing it over the shoulder into the cargo bay ----------
    void SpawnRocks()
    {
        foreach (var rk in rocks) if (rk != null) Object.Destroy(rk.gameObject);
        rocks.Clear();
        int n = want + 1;
        float S = SurfH;
        Vector3 O = kind == 0 ? Worlds.MarsO : Worlds.CallistoO;
        for (int i = 0; i < n; i++)
        {
            float a = 2.2f + i * (2.4f / Mathf.Max(1, n - 1)) + Random.Range(-0.15f, 0.15f), d = S * Random.Range(1.5f, 2.6f);
            Vector3 p = site + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
            Vector3 l = p - O;
            l.z = Mathf.Min(l.z, kind == 0 ? 12f : 80f);                       // Mars: stay south of the cave
            Vector2 lf = new Vector2(l.x, l.z); if (lf.magnitude > 68f) { lf = lf.normalized * 68f; l.x = lf.x; l.z = lf.y; }   // inside the rim
            p = O + l;
            p.y = GroundAt(p.x, p.z) + 0.05f;
            var s = MissionSite.Sample(kind, null, Random.Range(0, 9999));
            s.position = p;
            s.localScale = Vector3.one * S * 0.3f;
            var bm = new Material(Mats.Fx); bm.color = kind == 0 ? new Color(1f, 0.55f, 0.2f, 0.28f) : new Color(0.5f, 0.9f, 1f, 0.3f);
            var beam = Mats.Prim(PrimitiveType.Cylinder, s, new Vector3(0f, 2.2f, 0f), new Vector3(0.06f, 2.2f, 0.06f), bm);
            beam.name = "Beam"; Mats.NoShadows(beam);
            rocks.Add(s);
        }
    }

    void StandPose(float dt, float walkAmp)
    {
        walkPh += dt * (walkAmp > 0f ? 3.2f : 0f);
        float sw = Mathf.Sin(walkPh) * walkAmp;
        craft.legL.localRotation = Quaternion.Euler(sw, 0f, 0f);
        craft.legR.localRotation = Quaternion.Euler(-sw, 0f, 0f);
        craft.armR.localRotation = Quaternion.Slerp(craft.armR.localRotation, Quaternion.Euler(-sw * 0.6f - 10f, 0f, 4f), dt * 5f);
        craft.armL.localRotation = Quaternion.Slerp(craft.armL.localRotation, Quaternion.Euler(sw * 0.6f, 0f, -4f), dt * 5f);
        craft.torso.localRotation = Quaternion.Slerp(craft.torso.localRotation, Quaternion.Euler(walkAmp > 0f ? 5f : 0f, 0f, Mathf.Sin(walkPh) * 2f), dt * 5f);
    }

    void CollectTick(float dt)
    {
        craft.bay = Mathf.MoveTowards(craft.bay, 1f, dt * 1.2f);
        csT += dt;
        if (aborted || got >= want) { if (cs == 0) { Go(Ascent2); return; } }
        float S = SurfH, sc = craft.scale;
        Transform root = craft.transform;
        if (cs == 0)
        {
            // pick the nearest rock left and walk to where the hand reaches it
            if (pickIdx < 0 || pickIdx >= rocks.Count || rocks[pickIdx] == null)
            {
                pickIdx = -1; float bd = 1e9f;
                for (int i = 0; i < rocks.Count; i++) if (rocks[i] != null) { float dd = RobotNav.Flat(rocks[i].position - root.position); if (dd < bd) { bd = dd; pickIdx = i; } }
                if (pickIdx < 0) { Go(Ascent2); return; }
            }
            Transform rk = rocks[pickIdx];
            Vector3 to = rk.position - root.position; to.y = 0f;
            float want2 = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            float yaw = Mathf.MoveTowardsAngle(root.eulerAngles.y, want2, 70f * dt);
            root.rotation = Quaternion.Euler(0f, yaw, 0f);
            // the hand is on the LEFT: stand so the rock sits just ahead of the left hand
            Vector3 handOff = root.rotation * new Vector3(craft.reachX, 0f, craft.reachZ) * sc;
            Vector3 stand = rk.position - handOff; stand.y = 0f;
            Vector3 ds = stand - new Vector3(root.position.x, 0f, root.position.z);
            float dist = ds.magnitude;
            distLine = RobotNav.Flat(rk.position - root.position);
            bool facing = Mathf.Abs(Mathf.DeltaAngle(yaw, want2)) < 25f;
            if (dist > 0.4f * sc && csT < 25f)
            {
                float v = Mathf.Min(S * 0.32f, dist * 2f + 0.5f) * (facing ? 1f : 0.25f);
                Vector3 np = root.position + ds / Mathf.Max(dist, 1e-4f) * v * dt;
                np.y = GroundAt(np.x, np.z);
                root.position = np;
                StandPose(dt, 22f);
                if (Mathf.Sin(walkPh) * Mathf.Sin(walkPh - dt * 3.2f) < 0f) { Sfx.PlayAt(Sfx.Step, root.position, 0.6f, 120f, 0.6f); FX.Dust(root.position + root.right * (Random.value - 0.5f) * S * 0.2f, 1.5f); }
            }
            else { cs = 1; csT = 0f; }
            return;
        }
        Transform rock = pickIdx >= 0 && pickIdx < rocks.Count ? rocks[pickIdx] : null;
        if (cs == 1)
        {
            // bend down and reach
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(csT / 1.1f));
            craft.torso.localRotation = Quaternion.Euler(48f * k, 0f, 0f);
            craft.armL.localRotation = Quaternion.Euler(-40f * k, 0f, 8f * k);
            craft.legL.localRotation = Quaternion.Euler(-14f * k, 0f, 0f); craft.legR.localRotation = Quaternion.Euler(-14f * k, 0f, 0f);
            if (rock != null) distLine = RobotNav.Flat(rock.position - root.position);
            if (csT >= 1.1f)
            {
                if (rock != null)
                {
                    Transform beam = rock.Find("Beam"); if (beam != null) Object.Destroy(beam.gameObject);
                    rock.SetParent(craft.hand, true);
                    FX.Dust(rock.position, 1.5f);
                    Sfx.PlayAt(Sfx.Pickup, rock.position, 0.7f, 80f, 0.8f);
                }
                cs = 2; csT = 0f;
            }
            return;
        }
        if (cs == 2)
        {
            // stand up and swing the arm up and back over the shoulder, above the open bay
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(csT / 1.3f));
            craft.torso.localRotation = Quaternion.Euler(48f * (1f - k) - 8f * k, 0f, 0f);
            craft.armL.localRotation = Quaternion.Euler(Mathf.Lerp(-40f, -195f, k), 0f, Mathf.Lerp(8f, 14f, k));
            craft.legL.localRotation = Quaternion.Euler(-14f * (1f - k), 0f, 0f); craft.legR.localRotation = Quaternion.Euler(-14f * (1f - k), 0f, 0f);
            if (rock != null) rock.localPosition = Vector3.Lerp(rock.localPosition, Vector3.zero, dt * 6f);
            if (csT >= 1.3f)
            {
                if (rock != null) { flying = rock; flyFrom = rock.position; rock.SetParent(null, true); }
                cs = 3; csT = 0f;
            }
            return;
        }
        if (cs == 3)
        {
            // let go: the rock drops into the bay
            float k = Mathf.Clamp01(csT / 0.55f);
            if (flying != null)
            {
                Vector3 bp = craft.bayPoint.position;
                flying.position = Vector3.Lerp(flyFrom, bp, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * S * 0.08f;
            }
            craft.armL.localRotation = Quaternion.Slerp(craft.armL.localRotation, Quaternion.Euler(-10f, 0f, 4f), dt * 3f);
            craft.torso.localRotation = Quaternion.Slerp(craft.torso.localRotation, Quaternion.identity, dt * 4f);
            if (k >= 1f)
            {
                if (flying != null)
                {
                    flying.SetParent(craft.bayPoint, true);
                    int n = carried.Count;
                    flying.localPosition = new Vector3(((n % 3) - 1) * 0.035f * MechCraft.VisH, (n / 3) * 0.02f * MechCraft.VisH, 0f);
                    flying.localScale = flying.localScale * 0.75f;
                    carried.Add(flying);
                    FX.Sparkle(flying.position, kind == 0 ? new Color(1f, 0.55f, 0.3f) : new Color(0.6f, 0.95f, 1f), 8);
                    Sfx.PlayAt(Sfx.Clank != null ? Sfx.Clank : Sfx.Thud, flying.position, 0.6f, 80f, 0.9f + got * 0.05f);
                    got++;
                    flying = null;
                }
                if (pickIdx >= 0 && pickIdx < rocks.Count) rocks[pickIdx] = null;
                pickIdx = -1; cs = 0; csT = 0f;
            }
        }
    }

    // back at the ranch: the mech tosses the samples one by one from its back onto the display pile (if it landed by
    // the pad; the giants far away just add them to the count)
    void UnloadTick(float dt)
    {
        Vector3 table = MissionSite.TableFront(kind) + new Vector3(0f, 1f, -1.6f);
        bool near = RobotNav.Flat(table - m.transform.position) < 50f + H;
        unloadT -= dt;
        if (flying != null)
        {
            float k = Mathf.Clamp01(1f - unloadT / 0.7f);
            Vector3 p = Vector3.Lerp(flyFrom, table, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * (6f + H * 0.2f);
            flying.position = p;
            if (k >= 1f)
            {
                MissionSite.AddToPile(kind, flying, true);
                MissionSite.Deposit(kind);
                MissionSite.Trim(kind);
                Sfx.PlayAt(Sfx.Thud, table, 0.5f, 40f, 1.4f);
                flying = null; unloadT = 0.25f;
            }
            return;
        }
        if (unloadT > 0f) return;
        if (unloadN >= got) { Go(Exit); return; }
        unloadN++;
        if (near)
        {
            flying = MissionSite.Sample(kind, null, 300 + unloadN);
            flyFrom = m.transform.TransformPoint(new Vector3(0f, H * 0.62f, -H * 0.12f));
            flying.position = flyFrom;
            unloadT = 0.7f;
            Sfx.PlayAt(Sfx.Pick(Sfx.Servo), flyFrom, 0.5f, 80f, 0.8f);
        }
        else
        {
            MissionSite.Deposit(kind);
            FX.Sparkle(m.transform.position + Vector3.up * H * 0.6f, kind == 0 ? new Color(1f, 0.55f, 0.3f) : new Color(0.6f, 0.95f, 1f), 8);
            unloadT = 0.3f;
        }
    }

    float GroundAt(float x, float z)
    {
        if (kind == 0) return Worlds.MarsO.y + SurfaceWorlds.MarsY(x - Worlds.MarsO.x, z - Worlds.MarsO.z);
        return Worlds.CallistoO.y + SurfaceWorlds.CalY(x - Worlds.CallistoO.x, z - Worlds.CallistoO.z);
    }

    void Fail(string why)
    {
        ToastAll(r.robotName + ": " + why, 3f);
        if (RobotPhone.I != null) RobotPhone.I.Say(r.robotName + ": " + why);
        End(false, true);
    }

    void End(bool ejected, bool climbOut)
    {
        Frog pl = r.Pilot;
        r.world = WorldId.Ranch;
        // the real mech: back above ground if it was waiting under its spot
        if (m != null)
        {
            if (m.transform.position.y < Ranch.GY(m.transform.position.x, m.transform.position.z) - 50f) { Vector3 sp = m.SpawnPoint; m.ScriptPlace(new Vector3(sp.x, Ranch.GY(sp.x, sp.z), sp.z), launchYaw); }
            if (m.scripted) m.ScriptEnd(!m.PartsBusy);
        }
        // never leave the robot inside the craft we are about to delete
        if (craft != null && r.transform.IsChildOf(craft.transform))
        {
            if (pl.vehicle == m && m != null && !climbOut) MechPilot.Seat(r, m);
            else r.Unseat(m != null ? m.ExitPoint() : r.transform.position);
        }
        if (craft != null) { foreach (var c in carried) if (c != null) Object.Destroy(c.gameObject); Object.Destroy(craft.gameObject); craft = null; }
        foreach (var rk in rocks) if (rk != null) Object.Destroy(rk.gameObject);
        rocks.Clear(); carried.Clear();
        if (flying != null) { Object.Destroy(flying.gameObject); flying = null; }
        if (!ejected && climbOut && m != null && pl.vehicle == m)
        {
            pl.ExitVehicle();
            pl.cc.enabled = false;
            r.Unseat(m.ExitPoint());
        }
        else if (r.seatedIn != null && pl.vehicle != r.seatedIn) r.Unseat(r.transform.position);
        r.frozen = false; r.SetHidden(false);
        r.cmd = "auto"; r.ExtReset();
        if (Active == this) Active = null;
    }

    // ---------- the phone video feed (drone = chase cam, eye = the robot's cockpit view) ----------
    public bool Cam(bool eye, out Vector3 pos, out Quaternion rot)
    {
        pos = Vector3.zero; rot = Quaternion.identity;
        if (phase == Walk || phase == Board) return false;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        Vector3 wantP; Quaternion wantR;
        bool space = phase == TransitOut || phase == TransitBack || phase == Orbit;
        bool surface = phase == Descent || phase == Collect || phase == Ascent2;
        if (eye)
        {
            Transform seat = surface || space ? craft.seat : m.CanopySeat;
            Transform body = surface || space ? craft.transform : m.transform;
            float R = (surface || space ? craft.robotH * craft.scale : m.CanopyRobotH);
            wantP = seat.position + body.up * R * 0.55f + seat.forward * R * 0.45f;
            Vector3 fw = seat.forward;
            if (space) fw = craft.transform.forward;
            wantR = Quaternion.LookRotation(fw * 0.8f + Vector3.down * (phase == Ascent || phase == Descent2 || phase == Descent ? 0.9f : 0.25f), space ? craft.transform.up : Vector3.up);
            pos = wantP; rot = wantR; camSnap = true;
            return true;
        }
        if (space)
        {
            Transform c = craft.transform;
            Vector3 fw = c.forward, up = c.up, rt = c.right;
            wantP = c.position - fw * 38f + up * 12f + rt * 14f;
            wantR = Quaternion.LookRotation((c.position + fw * 18f) - wantP, up);
        }
        else if (surface)
        {
            Transform c = craft.transform;
            float S = craft.WorldH;
            Vector3 fw = c.forward; fw.y = 0f; fw.Normalize();
            Vector3 rt = new Vector3(fw.z, 0f, -fw.x);
            Vector3 at = c.position + Vector3.up * S * 0.5f;
            wantP = at - fw * S * 1.2f - rt * S * 1.15f + Vector3.up * S * 0.35f;
            float gy = GroundAt(wantP.x, wantP.z) + 1.5f; if (wantP.y < gy) wantP.y = gy;
            wantR = Quaternion.LookRotation(at - wantP, Vector3.up);
        }
        else
        {
            Vector3 fw = m.transform.forward; fw.y = 0f; fw.Normalize();
            Vector3 rt = new Vector3(fw.z, 0f, -fw.x);
            Vector3 at = m.transform.position + Vector3.up * H * 0.6f;
            if (phase == Ascent || phase == Descent2)
            {
                // a ground-side camera that tilts up after the climbing mech (then follows it up)
                Vector3 b = phase == Ascent ? launchP : landP;
                float h = m.transform.position.y - b.y;
                wantP = b - fw * (H * 1.6f + 14f) + rt * (H * 1.2f + 10f) + Vector3.up * (H * 0.4f + Mathf.Max(0f, h - H * 2f) * 0.85f);
            }
            else wantP = at - fw * (H * 1.3f + 8f) + rt * (H * 0.9f + 4f) + Vector3.up * H * 0.25f;
            float gy = Ranch.GY(wantP.x, wantP.z) + 1.5f; if (wantP.y < gy) wantP.y = gy;
            wantR = Quaternion.LookRotation(at - wantP, Vector3.up);
        }
        if (camSnap || (camPos - wantP).sqrMagnitude > 250000f) { camPos = wantP; camRot = wantR; camSnap = false; }
        else { camPos = Vector3.Lerp(camPos, wantP, dt * 5f); camRot = Quaternion.Slerp(camRot, wantR, dt * 6f); }
        pos = camPos; rot = camRot;
        return true;
    }

    // ---------- demo / screenshot mode ----------
    public static RobotMechMission Demo(Robot r, StoryMech mech, int kind, int n, string at, Frog f)
    {
        if (Active != null) Active.End(false, true);
        r.battery = Mathf.Max(r.battery, 0.9f);
        string s = Start(r, mech, kind, n, f);
        Debug.Log("FFDEMO mechmission start: " + s);
        var mm = Active;
        if (mm == null) return null;
        if (at == "board") return mm;
        // jump straight into the cockpit
        Frog pl = r.Pilot; pl.world = WorldId.Ranch;
        if (pl.vehicle != mech) pl.EnterVehicle(mech);
        MechPilot.Seat(r, mech);
        mm.seatedAtStart = true;
        mm.Go(Prep);
        if (at == "space" || at == "collect")
        {
            mm.launchP = mech.transform.position; mm.launchYaw = mech.transform.eulerAngles.y;
            mech.scripted = true;
            mm.ToSpace();
            if (at == "collect")
            {
                if (kind == 0) SurfaceWorlds.I.EnsureMars(); else SurfaceWorlds.I.EnsureCallisto();
                mm.site = kind == 0 ? Worlds.MarsO + new Vector3(-34f, 0f, -52f) : Worlds.CallistoO + new Vector3(30f, 0f, -28f);
                mm.site.y = mm.GroundAt(mm.site.x, mm.site.z);
                r.world = mm.DestWorld;
                mm.craft.SetScale(mm.SurfH / MechCraft.VisH);
                mm.Go(Descent);
                mm.t = DescentT - 2.2f;   // the last seconds of the landing, then the rock pick-up
            }
            else mm.t = TransitT * 0.25f;
        }
        return mm;
    }

    static Vector3 Bez(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
    {
        float u = 1f - t;
        return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
    }
}
