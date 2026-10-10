using System.Collections.Generic;
using UnityEngine;

// ffu20 (Bill: "we want the robots to be able to do what the froggies can do ... tell them to drive the trucks or the
// monster truck"): a robot ordered from the phone walks to a vehicle, climbs in and drives it with the SAME vehicle
// code the froggies use. The vehicle's driver is a hidden "pilot" Frog owned by the robot (Frog.robotPilot), fed a PIn
// every frame by the AI below, so every Vehicle subclass (trucks, Cyberboat, monster truck, tanks, Ripsaws, Optimus
// suit, story mechs, helicopter, drone, boats) runs its normal physics / sounds / effects. The robot itself sits in
// the seat (scaled to the seat like a froggy). Modes: wander, follow me, race the track (pond loop for boats, track at
// 15 m for flyers), go to my spot (park there). "Get out": flyers land first, boats head back to their mooring.
// Local per device (robots are not synced online).
public class RobotDriver
{
    public readonly Robot r;
    public Vehicle v;
    public string mode;              // wander, follow, race, spot
    public Frog user;                // who gave the order (follow me / my spot)
    public int phase;                // 0 walking to it, 1 driving, 2 getting out
    Vector3 spot, wanderGoal;
    float wanderT, stuckT, revT, raceT, walkT, exitT;
    bool raceInit;
    public static readonly string[] Modes = { "wander", "follow", "race", "spot" };
    public static readonly string[] ModeNames = { "wandering", "following", "racing", "going to the spot" };

    public RobotDriver(Robot robot, Vehicle veh, string m, Frog from)
    {
        r = robot; v = veh; user = from; SetMode(m);
    }

    public void SetMode(string m)
    {
        mode = m; raceInit = false; wanderT = 0f;
        if (user != null) spot = user.vehicle != null ? user.vehicle.transform.position : user.transform.position;
    }

    public string Status
    {
        get
        {
            string t = v != null ? v.Title : "vehicle";
            if (phase == 0) return "walking to the " + t;
            if (phase == 2) return "parking the " + t;
            int k = System.Array.IndexOf(Modes, mode);
            return (k >= 0 ? ModeNames[k] : mode) + " in the " + t;
        }
    }

    // ---------- which vehicles a robot can take (every ranch vehicle a froggy can drive) ----------
    public static bool Drivable(Vehicle v)
    {
        if (v == null || v.body == null || v is Starship) return false;
        Vector3 p = v.transform.position;
        return Mathf.Abs(p.x) < Layout.Half + 40f && Mathf.Abs(p.z) < Layout.Half + 40f && p.y > -60f && p.y < 900f;   // the ranch only (not the sub / Curiosity / space)
    }

    public static Frog MakePilot(Robot r)
    {
        var go = new GameObject("RobotPilot " + r.robotName);
        var f = go.AddComponent<Frog>();
        f.Build(0, r.transform.position + Vector3.down * 40f, 0f);
        f.nick = r.robotName;
        f.charId = 4;              // counts as a pet: may borrow any story mech (robots belong to the whole ranch)
        f.human = false;
        f.robotPilot = r;
        f.model.gameObject.SetActive(false);
        f.cc.enabled = false;
        return f;
    }

    // ---------- per frame (from Robot.Update) ----------
    public void Tick(float dt)
    {
        Frog pilot = r.Pilot;
        if (v == null || v.Wrecked && phase == 0) { r.EndDrive("my ride is wrecked"); return; }
        if (phase == 0)
        {
            if (v.driver != null && v.driver != pilot) { r.EndDrive(v.Title + " is taken (" + v.driver.nick + ")"); return; }
            walkT += dt;
            Vector3 goal = BoardSpot();
            bool there = r.ExtWalk(goal, r.RunSpeed, 0.6f, dt);
            Vector3 cp = v.body.ClosestPoint(r.transform.position + Vector3.up);
            float d = new Vector2(cp.x - r.transform.position.x, cp.z - r.transform.position.z).magnitude;
            if (there || d < 2.6f || walkT > 60f)
            {
                if (!v.CanEnter(pilot)) { r.EndDrive("can't get into the " + v.Title); return; }
                pilot.world = WorldId.Ranch;
                pilot.EnterVehicle(v);
                if (pilot.vehicle != v) { r.EndDrive(v.Title + " is taken"); return; }
                r.SitIn(v);
                phase = 1;
                Sfx.PlayAt(Sfx.Door, v.transform.position, 0.6f, 40f);
            }
            return;
        }
        // seated: the pilot left (wrecked / knocked out / taken over)
        if (pilot.vehicle != v) { r.Unseat(pilot.transform.position); r.EndDrive(v.Wrecked ? "my ride got wrecked" : "out of the " + v.Title); return; }
        float camYaw;
        PIn i = phase == 2 ? ExitInput(dt, out camYaw) : AiInput(dt, out camYaw);
        if (phase == 2 && exitT < 0f)
        {
            Vector3 at = v.ExitPoint();
            if (v is Boat || (v is GroundVehicle && ((GroundVehicle)v).amph != null && Layout.InPond(at.x, at.z))) at = ShoreNear(v.transform.position);
            pilot.ExitVehicle();
            pilot.cc.enabled = false;
            r.Unseat(at);
            r.EndDrive(null);
            return;
        }
        pilot.SetInput(i, camYaw);
    }

    Vector3 BoardSpot()
    {
        Vector3 vp = v.transform.position;
        if (v is Boat) return ShoreNear(vp);
        if (v is Flyer && vp.y > Ranch.GY(vp.x, vp.z) + 4f) return RobotNav.G(-44f, 21.5f);   // roof pads: the robot jets up from the porch (see below)
        Vector3 cp = v.body.ClosestPoint(r.transform.position + Vector3.up);
        Vector3 d = r.transform.position - cp; d.y = 0f;
        if (d.sqrMagnitude < 0.01f) d = -v.transform.right;
        return cp + d.normalized * 1.6f;
    }

    public static Vector3 ShoreNear(Vector3 p)
    {
        Vector2 c = Layout.PondC, R = Layout.PondR;
        Vector2 d = new Vector2((p.x - c.x) / R.x, (p.z - c.y) / R.y);
        if (d.sqrMagnitude < 0.0001f) d = Vector2.left;
        d.Normalize();
        float q = 1.16f;
        float x = c.x + d.x * R.x * q, z = c.y + d.y * R.y * q;
        return RobotNav.G(x, z);
    }

    public void BoardNow() { walkT = 999f; }   // demo: skip the walk
    public void GetOut() { if (phase == 1) { phase = 2; exitT = 6f; } else if (phase == 0) r.EndDrive(null); }

    // ---------- the AI ----------
    enum Kind { Ground, Mech, Heli, Drone, Boat }
    Kind KindOf()
    {
        if (v is StoryMech) return Kind.Mech;
        var fl = v as Flyer;
        if (fl != null) return fl.isDrone ? Kind.Drone : Kind.Heli;
        if (v is Boat) return Kind.Boat;
        return Kind.Ground;
    }
    float Scale { get { var sm = v as StoryMech; return sm != null ? Mathf.Max(1f, sm.height / 6f) : 1f; } }

    PIn ExitInput(float dt, out float camYaw)
    {
        exitT -= dt;
        Kind k = KindOf();
        camYaw = v.transform.eulerAngles.y;
        var i = new PIn();
        if (k == Kind.Heli || k == Kind.Drone)
        {
            float h = ((Flyer)v).AltitudeAboveGround;
            i.climb = h > 3f ? -1f : -0.5f;
            exitT = h < 0.6f && v.Speed < 1.5f ? -1f : 1f;   // land first, then climb out
            return i;
        }
        else if (k == Kind.Boat)
        {
            Vector3 home = v.HomePos;
            if ((home - v.transform.position).sqrMagnitude > 36f && exitT > -20f) { exitT = Mathf.Max(exitT, 0.1f); return Steer(home, 8f, 4f, out camYaw); }
            exitT = -1f;
        }
        else
        {
            float fs = v.ForwardSpeed;
            i.move.y = fs > 0.6f ? -1f : fs < -0.6f ? 1f : 0f;
            if (Mathf.Abs(fs) < 0.8f) exitT = -1f;
        }
        return i;
    }

    PIn AiInput(float dt, out float camYaw)
    {
        Kind k = KindOf();
        Vector3 p = v.transform.position;
        float S = Scale;
        Vector3 goal; float maxSp, arrive;
        switch (mode)
        {
            case "follow":
                {
                    Frog f = user;
                    Vector3 fp = f != null ? (f.vehicle != null ? f.vehicle.transform.position : f.transform.position) : p;
                    if (f != null && f.world != WorldId.Ranch) fp = p;
                    goal = fp; arrive = 9f * S + (k == Kind.Heli || k == Kind.Drone ? 6f : 0f); maxSp = 18f * Mathf.Sqrt(S);
                    break;
                }
            case "spot":
                goal = spot; arrive = 4f * S; maxSp = 14f * Mathf.Sqrt(S); break;
            case "race":
                goal = RaceGoal(k, p, S); arrive = 0f; maxSp = 40f * Mathf.Sqrt(S); break;
            default:
                {
                    wanderT -= dt;
                    if (wanderT <= 0f || Flat(wanderGoal - p) < 7f * S) { wanderGoal = PickWander(k); wanderT = 35f; }
                    goal = wanderGoal; arrive = 0f; maxSp = 16f * Mathf.Sqrt(S); break;
                }
        }
        return Steer(goal, maxSp, arrive, out camYaw);
    }

    static float Flat(Vector3 d) { d.y = 0f; return d.magnitude; }

    PIn Steer(Vector3 goal, float maxSp, float arrive, out float camYaw)
    {
        Kind k = KindOf();
        var i = new PIn();
        Vector3 p = v.transform.position;
        Vector3 fwd = v.transform.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        fwd.Normalize();
        Vector3 to = goal - p; to.y = 0f;
        float d = to.magnitude;
        float ang = d > 0.01f ? Vector3.SignedAngle(fwd, to, Vector3.up) : 0f;
        camYaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
        if (k == Kind.Heli || k == Kind.Drone)
        {
            var fl = (Flyer)v;
            float want = (mode == "race" ? 15f : 20f) + (mode == "follow" ? 0f : 0f);
            float ground = Ranch.GY(p.x, p.z);
            if (p.x > -70f && p.x < -15f && p.z > -25f && p.z < 15f) ground = Mathf.Max(ground, Layout.HouseH);
            float alt = p.y - ground;
            i.climb = Mathf.Clamp((want - alt) * 0.25f, -1f, 1f);
            bool parked = arrive > 0f && d < arrive;
            if (k == Kind.Drone)
            {
                camYaw = 0f;
                if (!parked && d > 0.5f) { float m = Mathf.Clamp01(d / 25f); i.move = new Vector2(to.x / d, to.z / d) * m; }
            }
            else
            {
                i.move.x = Mathf.Clamp(ang / 35f, -1f, 1f);
                if (!parked) i.move.y = Mathf.Clamp01(d / 30f) * Mathf.Clamp01(1f - Mathf.Abs(ang) / 120f);
            }
            if (alt < 4f && i.move.sqrMagnitude > 0f && i.climb > 0f) i.move *= 0.3f;   // climb first, then cruise
            return i;
        }
        // ground, mech, boat
        float fs = v.ForwardSpeed;
        if (arrive > 0f && d < arrive)
        {
            i.move.y = fs > 0.8f ? -1f : 0f;
            stuckT = 0f;
            return i;
        }
        if (revT > 0f)
        {
            revT -= Time.deltaTime;
            i.move.y = k == Kind.Mech ? -0.6f : -0.8f;
            i.move.x = -Mathf.Sign(ang == 0f ? 1f : ang);
            return i;
        }
        float steer = Mathf.Clamp(ang / (k == Kind.Mech ? 45f : 28f), -1f, 1f);
        // obstacle feeler (walls / buildings / trees, not the ground)
        if (k != Kind.Mech)
        {
            RaycastHit hit;
            float look = 5f + Mathf.Max(0f, fs) * 0.8f;
            Vector3 o = p + Vector3.up * 1.2f + fwd * (v.body.size.z * 0.5f);
            if (Physics.SphereCast(o, 0.9f, fwd, out hit, look, Vehicle.GroundMask, QueryTriggerInteraction.Ignore) && hit.normal.y < 0.5f)
            {
                float side = Vector3.Dot(hit.normal, new Vector3(fwd.z, 0f, -fwd.x));
                steer = Mathf.Clamp(steer + (side >= 0f ? 1f : -1f) * 1.2f, -1f, 1f);
            }
        }
        float corner = Mathf.Clamp01(Mathf.Abs(ang) / 110f);
        float want2 = Mathf.Min(maxSp, d * 0.7f + 4f) * (1f - corner * 0.65f);
        if (arrive > 0f) want2 = Mathf.Min(want2, (d - arrive) * 0.8f + 2f);
        float thr = Mathf.Clamp((want2 - fs) * 0.35f, -1f, 1f);
        if (k == Kind.Mech) thr = Mathf.Clamp(thr, -0.6f, 1f);
        if (Mathf.Abs(ang) > 120f && d < 18f) { thr = -0.7f; steer = -Mathf.Sign(ang); }   // tight spot: back round
        i.move.x = steer;
        i.move.y = thr;
        // stuck: pushing but not moving -> back up with the wheel turned the other way
        if (thr > 0.3f && Mathf.Abs(fs) < 0.7f) stuckT += Time.deltaTime; else stuckT = Mathf.Max(0f, stuckT - Time.deltaTime);
        if (stuckT > 2.2f) { stuckT = 0f; revT = 1.6f; }
        return i;
    }

    Vector3 RaceGoal(Kind k, Vector3 p, float S)
    {
        if (k == Kind.Boat)
        {
            // laps of the pond, outside the islands (q 0.86 ring), anticlockwise
            Vector2 c = Layout.PondC, R = Layout.PondR;
            float a = Mathf.Atan2((p.z - c.y) / R.y, (p.x - c.x) / R.x);
            a += 0.45f;
            return new Vector3(c.x + Mathf.Cos(a) * R.x * 0.86f, p.y, c.y + Mathf.Sin(a) * R.y * 0.86f);
        }
        if (!raceInit)
        {
            raceInit = true;
            float best = 1e9f;
            for (int n = 0; n < 160; n++)
            {
                float t = n * Mathf.PI * 2f / 160f;
                float dd = Flat(Layout.TrackPoint(t) - p);
                if (dd < best) { best = dd; raceT = t; }
            }
        }
        for (int g = 0; g < 8 && Flat(Layout.TrackPoint(raceT) - p) < 13f * Mathf.Min(S, 3f); g++) raceT += 0.06f;
        if (raceT > Mathf.PI * 2f) raceT -= Mathf.PI * 2f;
        return Layout.TrackPoint(raceT + 0.12f * Mathf.Min(S, 2.5f));
    }

    Vector3 PickWander(Kind k)
    {
        for (int tries = 0; tries < 30; tries++)
        {
            float x, z;
            switch (k)
            {
                case Kind.Boat:
                    {
                        float a = Random.value * 6.283f, q = Random.Range(0.3f, 0.85f);
                        x = Layout.PondC.x + Mathf.Cos(a) * Layout.PondR.x * q; z = Layout.PondC.y + Mathf.Sin(a) * Layout.PondR.y * q;
                        int w; if (Layout.IslandK(x, z, out w) > 0f) continue;
                        return new Vector3(x, Layout.WaterY, z);
                    }
                case Kind.Mech: x = Random.Range(-160f, 160f); z = Random.Range(-160f, 160f); break;
                case Kind.Heli: case Kind.Drone: x = Random.Range(-150f, 150f); z = Random.Range(-150f, 150f); return new Vector3(x, 0f, z);
                default:
                    if (Random.value < 0.55f) return Layout.TrackPoint(Random.value * 6.283f);
                    x = Random.Range(-20f, 125f); z = Random.Range(-25f, 75f); break;
            }
            if (Layout.PondQ(x, z) < 1.25f) continue;
            if (x > -70f && x < -14f && z > -40f && z < 16f) continue;     // house + pool
            if (x > -32f && x < 28f && z > 10f && z < 33f) continue;       // garage
            return new Vector3(x, Ranch.GY(x, z), z);
        }
        return Layout.TrackPoint(Random.value * 6.283f);
    }

    // phone list: every drivable ranch vehicle (story mechs grouped by band)
    public static List<Vehicle> List()
    {
        var l = new List<Vehicle>();
        foreach (var v in Vehicle.All) if (Drivable(v) && !(v is StoryMech)) l.Add(v);
        return l;
    }
    // the free story mech of a band nearest the robot
    public static StoryMech MechOfBand(int band, Vector3 near)
    {
        StoryMech best = null; float bd = 1e18f;
        foreach (var m in StoryMech.AllMechs)
        {
            if (m == null || m.band != band || m.driver != null || m.wrecked) continue;
            float d = (m.transform.position - near).sqrMagnitude;
            if (d < bd) { bd = d; best = m; }
        }
        return best;
    }
}
