using System.Collections.Generic;
using UnityEngine;

// ffu72 (Ben's 6.6 work list, Batch C) item 6 - WORLD-STATE: a vehicle that leaves Earth is not also on the ranch, and the
// Starship exists only where it is.
//  * The Starship is ONE thing with one place (StarAt): on the ranch pad, in space (someone aboard), or standing on Mars /
//    Callisto / the Moon where its crew landed. Away from the pad the ranch stack is hidden (the launch / landing sequence
//    shows it while it climbs / drops in); nobody can board an empty pad ("The Starship is on Mars right now").
//  * No permanent Earth-orbit copy: a space ship with nobody aboard (and no mech waiting for its crew) draws nothing.
//  * A mech that flies to space hides on the ranch (stowed, inactive) until it lands back at the ranch. A mech that drops its
//    crew on Mars / Callisto / the Moon waits in that body's orbit; "board" at the landing pad beams them back up to it.
//  * Safety net (Watch72): a mech no space ship carries comes home to its spot; a Starship whose crew all left another way
//    (lobby, mode pick) flies itself home to the pad.
//  * Space ships are a small pool (ships[0] = the old single `ship`): a ship is reused only when idle, never stolen from
//    a mech waiting in orbit. Time warp = the fastest ship someone is flying.
// F72.Old (&ff72old=1) = the pre-ffu72 code paths, untouched.
public enum StarLoc { Pad, Space, Mars, Callisto, Moon }

public partial class SpaceWorld
{
    public readonly List<Starship> ships = new List<Starship>();
    readonly Dictionary<Starship, LineRenderer[]> guides72 = new Dictionary<Starship, LineRenderer[]>();
    public static StarLoc StarAt = StarLoc.Pad;
    float starIdleT;
    public static int ShipsBuilt72;

    public static bool StarHome { get { return F72.Old || StarAt == StarLoc.Pad; } }
    public static string StarWhere
    {
        get
        {
            switch (StarAt)
            {
                case StarLoc.Space: return "up in space";
                case StarLoc.Mars: return "on Mars";
                case StarLoc.Callisto: return "on Callisto";
                case StarLoc.Moon: return "on the Moon";
                default: return "on the pad";
            }
        }
    }
    static StarLoc LocOf(string id) { return id == "mars" ? StarLoc.Mars : id == "callisto" ? StarLoc.Callisto : id == "moon" ? StarLoc.Moon : id == "earth" ? StarLoc.Pad : StarLoc.Space; }
    static WorldId WorldOfLoc(StarLoc l) { return l == StarLoc.Mars ? WorldId.Mars : l == StarLoc.Callisto ? WorldId.Callisto : l == StarLoc.Moon ? WorldId.Moon : WorldId.Space; }
    public static bool OffEarth(WorldId w) { return w == WorldId.Space || w == WorldId.Mars || w == WorldId.Callisto || w == WorldId.Moon; }

    public void SetStar(StarLoc l, string why)
    {
        if (StarAt != l) Debug.Log("FF72 starship " + StarAt + " -> " + l + " (" + why + ")");
        StarAt = l; starIdleT = 0f;
        ApplyPad72();
    }

    // ranch pad stack: shown only while the Starship is home (or climbing / dropping in through the launch sequence)
    public static void ApplyPad72()
    {
        if (F72.Old || Ranch.ShipStack == null) return;
        bool on = StarAt == StarLoc.Pad || LaunchSeq.Busy;
        if (Ranch.ShipStack.gameObject.activeSelf != on) Ranch.ShipStack.gameObject.SetActive(on);
    }
    public static bool PadShown { get { return Ranch.ShipStack != null && Ranch.ShipStack.gameObject.activeSelf; } }

    public static int Riders(Starship s) { int n = 0; if (Game.I != null) foreach (Frog f in Game.I.frogs) if (f != null && (f.passengerOf == s || f.jetOf == s)) n++; return n; }
    public static bool InUse(Starship s) { return s != null && (s.driver != null || s.parkedFor != null || Riders(s) > 0); }
    public int ShipsInUse { get { int n = 0; foreach (var s in ships) if (InUse(s)) n++; return n; } }
    public int ShipsShown { get { int n = 0; foreach (var s in ships) if (s != null && s.Shown72) n++; return n; } }
    public int StarshipsShown { get { int n = 0; foreach (var s in ships) if (s != null && s.Shown72 && s.mechForm == null) n++; return n; } }

    // the first ship someone is flying (the old "ship.driver != null" ride-along test)
    public Starship DrivenShip() { foreach (var s in ships) if (s != null && s.driver != null) return s; return null; }
    public Starship ShipOf(Frog f)
    {
        if (f == null) return ship;
        Starship s = f.vehicle as Starship; if (s != null) return s;
        s = f.passengerOf as Starship; if (s != null) return s;
        if (f.jetOf != null) return f.jetOf;
        return ship;
    }
    // an AI froggy in space with no ride joins whoever is flying (never takes the controls of a mech waiting for its crew)
    public Starship StrayShip() { if (F72.Old) return ship; Starship d = DrivenShip(); if (d != null) return d; return InUse(ship) ? null : ship; }

    // an idle ship from the pool (never one a mech is waiting in), or a new one
    public Starship FreeShip()
    {
        foreach (var s in ships) if (s != null && !InUse(s)) return s;
        var n = Starship.Build(this);
        n.gameObject.name = "Starship (space " + (ships.Count + 1) + ")";
        ships.Add(n);
        ShipsBuilt72++;
        guides72[n] = new[] { Line(new Color(0.55f, 0.9f, 1f, 0.5f), 0.45f, 90, false), Line(new Color(1f, 0.85f, 0.3f, 0.6f), 0.5f, 64, true) };
        int e = Find("earth");
        n.EnterOrbit(e, bodies[e].cap, 0f);
        Debug.Log("FF72 new space ship #" + ships.Count);
        return n;
    }

    float Warp72()
    {
        float w = 1f; bool any = false;
        foreach (var s in ships) if (s != null && s.driver != null) { w = any ? Mathf.Max(w, s.SimRate) : s.SimRate; any = true; }
        return any ? w : (ship != null ? ship.SimRate : 1f);
    }

    void Sim72(float dt)
    {
        foreach (var s in ships)
        {
            if (s == null) continue;
            bool use = InUse(s);
            if (use || s == ship) s.Sim(dt);
            LineRenderer p = s == ship ? path : null, r = s == ship ? targetRing : null;
            LineRenderer[] g;
            if (s != ship && guides72.TryGetValue(s, out g)) { p = g[0]; r = g[1]; }
            if (p == null) continue;
            bool show = s.driver != null;
            if (p.enabled != show) p.enabled = show;
            if (!show) { if (r != null) r.enabled = false; continue; }
            var pts = s.Predict(p.positionCount);
            for (int k = 0; k < pts.Length; k++) p.SetPosition(k, pts[k]);
            int ti = s.target;
            if (r != null && ti >= 0 && !(s.mode == Starship.Mode.Orbit && s.OrbitBody == ti))
            {
                Body b = bodies[ti];
                float rr = Mathf.Max(b.r * 1.6f, b.cap > 0 ? b.cap : b.r * 2f, 8f);
                r.enabled = true;
                for (int k = 0; k < r.positionCount; k++) { float a = k * Mathf.PI * 2f / r.positionCount; r.SetPosition(k, b.pos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rr); }
            }
            else if (r != null) r.enabled = false;
        }
    }

    // ---------------- launch / orbit / land (ffu72 paths) ----------------
    void Fly72(Frog f, Starship s, string why)
    {
        f.SendTo(WorldId.Space, s.transform.position, 0f);
        f.EnterVehicle(s);
        Debug.Log("FF72 " + f.nick + " flies " + s.gameObject.name + " (" + (s.mechForm != null ? s.mechForm.Title : "Starship") + ") - " + why);
    }
    void Ride72(Frog f, Starship s, string why)
    {
        f.SendTo(WorldId.Space, s.transform.position, 0f);
        f.BoardAsPassenger(s);
        Debug.Log("FF72 " + f.nick + " rides " + s.gameObject.name + " with " + (s.driver != null ? s.driver.nick : "-") + " - " + why);
    }

    public static bool Shared72 { get { return Game.I != null && Game.I.SharedScreen; } }
    // the flying Starship (not a mech) - its countdown crew rides inside it
    public Starship StarDriven() { foreach (var s in ships) if (s != null && s.driver != null && s.mechForm == null) return s; return null; }
    public int Driven72 { get { int n = 0; foreach (var s in ships) if (s != null && s.driver != null) n++; return n; } }
    // ffu72 item 7: humans riding a mech's shoulders (only AI / uncontrolled froggies may, except the shared-screen group trip)
    public int HumanShoulderRiders { get { int n = 0; if (Game.I != null) foreach (Frog f in Game.I.frogs) { var s = f != null ? f.passengerOf as Starship : null; if (s != null && s.mechForm != null && f.human) n++; } return n; } }

    // the Starship from the ranch pad (launch sequence, shared-screen ride-alongs)
    // ffu72 item 7: ONE shared view = one group trip (everyone rides whoever is flying, mech or Starship, as before).
    // Split screen = every human is independent: they only ever ride INSIDE the Starship they joined at the countdown,
    // never on another player's mech; otherwise they fly their own ship.
    void Launch72(Frog f)
    {
        Starship g = DrivenShip(), star = StarDriven();
        Starship ride = Shared72 ? g : star != null ? star : (!f.human ? g : null);
        if (ride != null)
        {
            Ride72(f, ride, Shared72 ? "shared screen: the group trip" : ride.mechForm == null ? "Starship crew" : "AI rides the mech's shoulders");
            f.Toast("Liftoff! You're riding with " + ride.driver.nick, 3f);
            return;
        }
        Starship s = FreeShip();
        s.SetMechForm(null);
        int earth = Find("earth"), st = Find("station");
        float sAng = Mathf.Atan2((PosAt(st, simT) - PosAt(earth, simT)).z, (PosAt(st, simT) - PosAt(earth, simT)).x);
        s.EnterOrbit(earth, bodies[st].a, sAng - 0.18f);
        SetStar(StarLoc.Space, f.nick + " launched");
        Fly72(f, s, "pad launch");
        f.Toast("LIFTOFF! Parked in sunlit Earth orbit beside the Station.  D-pad < > target, X auto-transfer, Y land", 5f);
    }

    // a story mech rocketed past 2.4 km: it flies on in space as itself
    bool LaunchMech72(Frog f, StoryMech m)
    {
        Starship g = DrivenShip();
        // ffu72 item 7: one shared view = one group trip (whoever is already flying takes this player along; their mech parks
        // at its spot). Split screen: this pilot's mech flies as its OWN ship (never merged onto another mech).
        if (g != null && (Shared72 || !f.human || f.netPuppet)) { Launch72(f); return false; }
        int others = Driven72;
        Starship s = FreeShip();
        int earth = Find("earth"), st = Find("station");
        float sAng = Mathf.Atan2((PosAt(st, simT) - PosAt(earth, simT)).z, (PosAt(st, simT) - PosAt(earth, simT)).x);
        s.EnterOrbit(earth, bodies[st].a, sAng - 0.18f - 0.6f * others);   // its own spot in the parking orbit, ~30 m from the next
        s.SetTarget(Find("moon"));
        s.SetMechForm(m);
        Fly72(f, s, "mech reached space");
        return true;
    }

    // a mech waiting in this body's orbit for this froggy (it dropped them off)
    public Starship ParkedFor(Frog f, int body)
    {
        foreach (var s in ships) if (s != null && s.parkedFor == f && s.mechForm != null && (body < 0 || s.parkedBody == body)) return s;
        return null;
    }

    void ToOrbit72(Frog f, string bodyId)
    {
        int b = Find(bodyId);
        Starship pk = ParkedFor(f, b);
        // ffu75: the mech stands on this world - walk up its ramp and fly it (never beamed up past it)
        MechLand75 ml75 = pk != null && !F75.Old ? MechLand75.For(pk) : null;
        if (ml75 != null)
        {
            if (!ml75.Board(f)) f.Toast(pk.mechForm != null ? pk.mechForm.Title + " is right here - A at the foot of its ramp" : "Your mech is right here", 3f);
            return;
        }
        if (pk != null)
        {
            pk.parkedFor = null; pk.parkedBody = -1;
            Fly72(f, pk, "beamed back up to the waiting mech");
            f.Toast("Beamed back up to " + pk.mechForm.Title + " in " + bodies[b].name + " orbit", 3.5f);
            Sfx.Play(Sfx.Boom, 0.7f, 0.7f);
            return;
        }
        Starship g = DrivenShip();
        if (g != null && g.mechForm == null)
        {
            Ride72(f, g, "beamed up to the flying Starship");
            f.Toast("Beamed up to the Starship with " + g.driver.nick, 3f);
        }
        else if (g != null && g.mechForm != null && (!f.human || (Game.I != null && Game.I.SharedScreen)))
        {
            Ride72(f, g, "beamed up to the flying mech");
            f.Toast("Beamed up to " + g.mechForm.Title + " with " + g.driver.nick, 3f);
        }
        else
        {
            Starship s = FreeShip();
            s.SetMechForm(null);
            s.EnterOrbit(b, bodies[b].cap, Random.value * 6.28f);
            SetStar(StarLoc.Space, f.nick + " back to " + bodies[b].name + " orbit");
            Fly72(f, s, "to " + bodies[b].name + " orbit");
            f.Toast("Back in " + bodies[b].name + " orbit. Earth is the next target.", 3.5f);
            s.SetTarget(Find("earth"));
        }
        Sfx.Play(Sfx.Boom, 0.7f, 0.7f);
    }

    void Land72(Starship s, int bodyIdx)
    {
        Body b = bodies[bodyIdx];
        if (!b.landable)
        {
            if (s.driver != null) s.driver.Toast("Can't land on " + b.name + " - Earth, the Moon, Mars and Callisto are landable", 3f);
            return;
        }
        s.ReboardAll();   // jetpacking froggies come back in before touchdown
        StoryMech mech = s.mechForm;
        Frog pilot = s.driver;
        var crew = new List<Frog>();
        if (s.driver != null) crew.Add(s.driver);
        if (Game.I != null) foreach (Frog f in Game.I.frogs) if (f != null && f.passengerOf == s) crew.Add(f);
        bool earth = b.id == "earth";
        if (mech == null) SetStar(earth ? StarLoc.Pad : LocOf(b.id), "landed on " + b.name);
        // ffu75 (Bill): a mech lands ITSELF (on its flames, then a ramp) - no Starship stand-in, it parks on the surface (MechLand75.cs)
        if (!F75.Old && mech != null && crew.Count > 0) { Land75(s, bodyIdx, crew, pilot); Sfx.Play(Sfx.Boom, 0.8f, 0.5f); return; }
        if (earth && mech == null && crew.Count > 0)
        {
            var spots = new List<Vector3>();
            for (int j = 0; j < crew.Count; j++)
            {
                Vector2 pc = Layout.PadC;
                float x = pc.x + 16f, z = pc.y - 4.5f + j * 3f;
                spots.Add(new Vector3(x, Ranch.GY(x, z) + 1.2f, z));
            }
            if (LaunchSeq.BeginLanding(crew, spots)) { Sfx.Play(Sfx.Boom, 0.8f, 0.5f); return; }
        }
        // a mech that drops its crew on another world waits in that orbit for its pilot (it is not back on the ranch)
        if (mech != null && !earth && pilot != null) { s.parkedFor = pilot; s.parkedBody = bodyIdx; Debug.Log("FF72 " + mech.Title + " waits in " + b.name + " orbit for " + pilot.nick); }
        int k = 0;
        foreach (Frog f in crew)
        {
            if (earth)
            {
                Vector2 pc = Layout.PadC;
                float x = pc.x + 16f, z = pc.y - 4.5f + k * 3f;
                f.SendTo(WorldId.Ranch, new Vector3(x, Ranch.GY(x, z) + 1.2f, z), 90f);
                f.Toast("Touchdown at the ranch!", 3f);
            }
            else if (b.id == "mars") SurfaceWorlds.LandMars(f, k);
            else if (b.id == "moon") MoonWorld.Land(f, k);
            else SurfaceWorlds.LandCallisto(f, k);
            if (mech != null && !earth && f == pilot) f.Toast(mech.Title + " waits in orbit - board at the landing pad to beam back up to it", 4f);
            k++;
        }
        if (mech != null && earth)
        {
            s.SetMechForm(null);
            mech.ReturnFromSpace(pilot);
        }
        Sfx.Play(Sfx.Boom, 0.8f, 0.5f);
    }

    // ---------------- per frame: keep the world consistent ----------------
    void Watch72(float dt)
    {
        foreach (var s in ships)
        {
            if (s == null) continue;
            if (s.parkedFor != null && !OffEarth(s.parkedFor.world) && MechLand75.For(s) == null) { Debug.Log("FF72 " + s.gameObject.name + ": " + s.parkedFor.nick + " left the off-Earth worlds - the waiting mech flies home"); s.parkedFor = null; s.parkedBody = -1; }
            bool use = InUse(s);
            if (!use && s.mechForm != null) { Debug.Log("FF72 " + s.gameObject.name + ": nobody aboard " + s.mechForm.Title + " - it flies home"); s.SetMechForm(null); }
            s.Show72(use && !MechLand75.Hides(s));   // ffu75: not drawn in orbit while its mech stands on a world
        }
        foreach (var m in StoryMech.AllMechs)
        {
            if (m == null || !m.Away72) continue;
            bool carried = false;
            foreach (var s in ships) if (s != null && s.mechForm == m) carried = true;
            if (!carried) m.Home72("no space ship carries it");
        }
        bool starAboard = false;
        foreach (var s in ships) if (s != null && s.mechForm == null && InUse(s)) starAboard = true;
        if (StarAt == StarLoc.Space)
        {
            if (starAboard || LaunchSeq.Busy) starIdleT = 0f;
            else if ((starIdleT += dt) > 1.5f) SetStar(StarLoc.Pad, "nobody aboard in space - autopilot home");
        }
        else if (StarAt != StarLoc.Pad)
        {
            WorldId w = WorldOfLoc(StarAt); bool there = starAboard;
            if (Game.I != null) foreach (Frog f in Game.I.frogs) if (f != null && f.world == w) there = true;
            if (there) starIdleT = 0f;
            else if ((starIdleT += dt) > 1.5f) SetStar(StarLoc.Pad, "its crew left " + w + " - autopilot home");
        }
        else starIdleT = 0f;
        ApplyPad72();
        MechLand75.StaticTick();
    }
}

public partial class Starship
{
    public Frog parkedFor;           // ffu72: a mech waiting in orbit for the froggy it dropped on Mars / Callisto / the Moon
    public int parkedBody = -1;
    bool shown72 = true;
    public bool Shown72 { get { return shown72; } }
    // ffu72: nobody aboard = nothing drawn (no permanent Earth-orbit copy)
    public void Show72(bool on)
    {
        if (shipVis == null) shipVis = transform.Find("Vis");
        shown72 = on;
        bool sv = on && mechForm == null, mv = on && mechForm != null;
        if (shipVis != null && shipVis.gameObject.activeSelf != sv) shipVis.gameObject.SetActive(sv);
        if (mechVis != null && mechVis.gameObject.activeSelf != mv) mechVis.gameObject.SetActive(mv);
    }
}
