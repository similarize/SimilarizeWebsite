using System.Collections.Generic;
using UnityEngine;

// ffu6r canon (Bill 2026-10-19 16:41 / 16:42): "Figure 03 is the only robot that, if he doesn't wanna do chores, he doesn't
// have to when we tell him to." + "Figure 03 is different than all the other robots. He can play and do everything that
// the froggies can do, and not just do chores."
//  * Declines: a CHORE order (auto chores, any chore, mow, go home to chores) to Figure 03 is declined by mood - 50 %, +25 %
//    if he did one in the last 2 minutes, -30 % when asked with "please" (voice), clamped 15..85 % - with a cheeky line and a
//    fun offer ("Nah, I'd rather play catch! Want to?"); saying "yes" / "okay" by voice takes the offer. Play / come / stop /
//    charge / mission orders are never declined. Every other robot always obeys (James's orders still override their chores).
//  * Free play: when nobody has given him anything to do (auto, no visit / mission / drive / hunt, battery fine) he picks his
//    own fun with a froggy nearby every 10-30 s - the same PLAY verbs a player can ask for (tag, hop, swing, swim, race,
//    catch, dance, build, bugs, pets, fetch, climb, cook, collect, ride along, hunt with his own bow) - instead of chores.
//    He also takes cars out for a spin himself (any vehicle the usual ownership / ask-to-share rules let him use, back out
//    after ~90 s), tags along with a froggy wherever they go (follow = drop pod / door / jets to other worlds), and now and
//    then flies his own rocket on a space trip (Fig50). Bill 16:43: "He can drive cars, get on a Starship and go, and do
//    everything a froggy can."
//    Off in probe demos unless &fig03free=1. Knob &fig03roll=a,b,.. forces the decline dice (probes).
// Portable (2022.3 + 6.6): plain Unity + game classes only.
public class Fig03Free : MonoBehaviour
{
    static Fig03Free I;
    public static int Declined, Accepted, FreePicks;
    public static string Offer = ""; public static Robot OfferBy; public static float OfferT = -99f;
    public static bool AskedNicely;        // set by the voice parser for the next order ("please")
    public static bool LastDeclined;
    static float lastChoreT = -999f, choreHoldUntil = -999f, nextPick = 20f, driveT = -1f;
    static readonly Queue<float> rolls = new Queue<float>();
    static bool rollsRead;
    static readonly string[] Fun = { "catch", "tag", "race", "swing", "swim", "hop", "dance", "build", "bugs", "fetch", "climb", "pets", "cook", "collect" };
    static readonly string[] FunName = { "play catch", "play tag", "have a race", "go on the swings", "go swimming", "do some hopping", "have a dance party", "build a sandcastle", "go on a bug hunt", "play fetch with the dogs", "climb a tree", "pet the animals", "have a cookout", "go treasure hunting" };

    public static bool Is(Robot r) { return r != null && r.id == "figure03"; }
    static bool IsChore(string a) { return a == "auto" || a.StartsWith("chore:") || a.StartsWith("mow:") || a == "play:home"; }

    static float Roll()
    {
        if (!rollsRead)
        {
            rollsRead = true; string k = Ffu33.Url("fig03roll");
            if (!string.IsNullOrEmpty(k)) foreach (var x in k.Split(',')) { float v; if (float.TryParse(x, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) rolls.Enqueue(v); }
        }
        return rolls.Count > 0 ? rolls.Dequeue() : Random.value;
    }

    // RobotPhone.Do: true = Figure 03 said no (say = his line)
    public static bool Declines(Robot r, string a, Frog f, ref string say)
    {
        bool nice = AskedNicely; AskedNicely = false; LastDeclined = false;
        if (!Is(r) || a == null || !IsChore(a)) return false;
        float now = Time.realtimeSinceStartup;
        float p = 0.5f + (now - lastChoreT < 120f ? 0.25f : 0f) - (nice ? 0.3f : 0f);
        p = Mathf.Clamp(p, 0.15f, 0.85f);
        float roll = Roll();
        bool no = roll < p;
        Debug.Log("FIG03 chore order '" + a + "' p(decline) " + p.ToString("0.00") + " roll " + roll.ToString("0.00") + (nice ? " (please)" : "") + " -> " + (no ? "DECLINED" : "accepted"));
        if (!no) { Accepted++; lastChoreT = now; choreHoldUntil = now + 300f; return false; }
        Declined++; LastDeclined = true;
        int k = Random.Range(0, Fun.Length);
        Offer = Fun[k]; OfferBy = r; OfferT = now;
        string[] nah = { "Nah, I'd rather " + FunName[k] + "! Want to?", "Chores? Ribbit-nope! Let's " + FunName[k] + " instead!", "Hmm... no thanks! " + Cap(FunName[k]) + " sounds way more fun. Coming?", "Not right now - I'm in a " + FunName[k] + " mood! You in?" };
        say = r.robotName + ": " + nah[Random.Range(0, nah.Length)] + (nice ? "" : " (Maybe if you ask nicely...)");
        return true;
    }
    static string Cap(string s) { return s.Length > 0 ? char.ToUpperInvariant(s[0]) + s.Substring(1) : s; }

    // the voice "yes" to his offer
    public static string TakeOffer(Robot r) { return r == OfferBy && Offer.Length > 0 && Time.realtimeSinceStartup - OfferT < 40f ? "play:" + Offer : null; }
    public static bool FreeVisit(Robot r) { return Is(r) && free.Contains(r); }
    static readonly HashSet<Robot> free = new HashSet<Robot>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        if (Ffu33.Url("ffdemo") != null && Ffu33.Url("fig03free") != "1") return;   // demos keep Figure 03 where they put him
        I = new GameObject("Fig03Free").AddComponent<Fig03Free>(); DontDestroyOnLoad(I.gameObject);
    }

    void Update()
    {
        if (Game.I == null || Game.I.state != Game.State.Play || RanchLife.I == null) return;
        float now = Time.realtimeSinceStartup;
        if (now < nextPick) return;
        nextPick = now + Random.Range(10f, 30f);
        Robot r = RanchLife.I.robots.Find(x => Is(x));
        if (r == null) return;
        if (r.visit == null && r.drv == null) free.Remove(r);
        if (driveT > 0f && r.drv != null && free.Contains(r) && now - driveT > 90f) { r.drv.GetOut(); driveT = -1f; Debug.Log("FIG03 free play: parks the car"); return; }
        if (r.drv == null) driveT = -1f;
        if (now < choreHoldUntil && r.forcedChore >= 0) return;                         // he agreed to a chore: let him finish
        if (r.cmd != "auto" || r.forcedChore >= 0 || r.visit != null || r.mission != null || r.drv != null || r.hunt != null || r.manual != null) return;
        if (r.frozen || r.hidden || r.displayOnly || r.Charging || r.battery < r.lowAt + 0.05f || RobotMechMission.For(r) != null) return;
        if (now < choreHoldUntil) return;
        // a froggy nearby to play with (players or AI froggies)
        var near = new List<Frog>();
        foreach (var f in Game.I.frogs)
            if (f != null && f.gameObject.activeInHierarchy && Robot.TargetWorld(f) == (r.world == WorldId.Underwater ? WorldId.Ranch : r.world) && f.world != WorldId.RealRoom && !f.launching && (f.transform.position - r.transform.position).sqrMagnitude < 70f * 70f) near.Add(f);
        if (near.Count == 0) return;
        Frog pal = near[Random.Range(0, near.Count)];
        string verb = pal.vehicle != null && !(pal.vehicle is GardenSeat) ? "ridewith" : pal.vehicle is GardenSeat ? (((GardenSeat)pal.vehicle).kind == GardenSeat.Kind.Swing ? "swingsit" : "hamwith") : Fun[Random.Range(0, Fun.Length)];
        float dice = Random.value; string s;
        if (verb != "ridewith" && dice < 0.15f && r.world == WorldId.Ranch)
        {
            // take a car out himself (RobotDriver.ListFor = what is around; DriveOrder applies the ownership / ask-to-share rules)
            var vs = RobotDriver.ListFor(pal);
            Vehicle v = vs.Count > 0 ? vs[Random.Range(0, Mathf.Min(vs.Count, 6))] : null;
            if (v == null) return;
            s = r.DriveOrder(v, Random.value < 0.5f ? "wander" : "follow", pal); verb = "drive " + v.Title;
            if (r.drv != null) driveT = now;
        }
        else if (dice < 0.2f && r.world == WorldId.Ranch && r.battery > 0.6f) { s = Fig50.PhoneRocket(r, pal); verb = "own rocket"; }
        else if (dice < 0.35f) { verb = "follow"; s = r.Play(verb, pal); }   // tags along, into other worlds too
        else s = r.Play(verb, pal);
        free.Add(r); FreePicks++;
        Debug.Log("FIG03 free play: " + verb + " with " + pal.nick + " -> " + s);
    }
}
