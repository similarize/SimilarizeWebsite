using UnityEngine;

// ffu57 robot PLAY verbs for the animals (Core/Animals57.cs), robot parity with James:
//   pets    = walk to the nearest animal in this world, pet it, feed it a treat that fits it (and a dog gets a game of fetch)
//   ridepet = climb on the nearest cow / sheep / pig / big dog and ride it in a slow circle (ranch horses: PLAY > Ride a horse)
//   fetch   = play fetch with the nearest dog (Germy, Daisy or any ranch dog): three throws
// Same rules as froggies: an order from James always wins, the animal's own state machine does the walking.
public partial class Robot
{
    Animal pet57; int pet57N;

    Animal NearAnimal57(System.Func<Animal, bool> ok)
    {
        Animal best = null; float bd = 120f * 120f;
        Vector3 p = transform.position;
        foreach (var a in Animal.All)
        {
            if (a == null || !a.isActiveAndEnabled || a.frozen || !ok(a)) continue;
            if (Animals57.I != null && Animals57.I.Busy(a)) continue;
            if (CritterPals.I != null && CritterPals.I.pals.Exists(q => q.a == a)) continue;
            float d = (a.transform.position - p).sqrMagnitude;
            if (Mathf.Abs(a.transform.position.y - p.y) > 15f) continue;
            if (d < bd) { bd = d; best = a; }
        }
        return best;
    }

    string StartPets57(string verb)
    {
        var v = visit;
        if (!Animals57.On || Animals57.I == null) return Say("the animals are napping right now");
        System.Func<Animal, bool> ok = a => true;
        if (verb == "fetch") ok = a => a.kind == Animal.Kind.Dog;
        if (verb == "ridepet") ok = a => a.GetComponent<Horse>() == null && (a.kind == Animal.Kind.Cow || a.kind == Animal.Kind.Goat || a.kind == Animal.Kind.Dog && a.legs.Count > 0 && a.legs[0].localPosition.y > 0.3f);
        pet57 = NearAnimal57(ok);
        if (pet57 == null)
        {
            if (verb == "fetch") return Say("no dog here - ask Germy or Daisy to come along, then we can play fetch!");
            if (verb == "ridepet") return Say(world == WorldId.Ranch ? "no cow free right now - try the pasture!" : "nothing big enough to ride here - the cows are on the ranch!");
            return Say("no animals here - ask a pet to come along with you!");
        }
        pet57N = 0;
        v.verb = verb; v.vs = 0; v.vt = 0f; v.count = 0;
        string n = pet57.petName.Length > 0 ? pet57.petName : "the " + pet57.kind.ToString().ToLower();
        Debug.Log("ROBOTS57 " + verb + " " + robotName + " -> " + n + " at " + pet57.transform.position.ToString("0.0"));
        return Say(verb == "fetch" ? "fetch time with " + n + "!" : verb == "ridepet" ? "giddy-up! I'm going to ride " + n + "!" : "I'll go say hi to " + n + " and give it a treat!");
    }

    void PetsTick57(float dt, Vector3 lp)
    {
        var v = visit; Frog f = v.lead;
        if (pet57 == null || Animals57.I == null) { EndVerb(); return; }
        var s = Animals57.I.Of(pet57);
        Vector3 ap = pet57.transform.position;
        switch (v.vs)
        {
            case 0:   // walk over
                {
                    Vector3 side = (transform.position - ap); side.y = 0f; side = side.sqrMagnitude > 0.01f ? side.normalized : Vector3.back;
                    float d = WalkTo(ap + side * (Animals57.BodyLen(pet57) * 0.5f + 0.7f), RunSpeed, 0.35f, dt);
                    if (d < 0.6f || v.vt > 60f)
                    {
                        v.vt = 0f;
                        if (v.verb == "ridepet") { v.vs = 10; Animals57.I.RobotRide(s, this); f.Toast(robotName + ": yee-haw! I'm riding the " + pet57.kind.ToString().ToLower() + "!", 2.2f); }
                        else if (v.verb == "fetch") { v.vs = 20; }
                        else { v.vs = 1; Animals57.I.StartPet(s, null, this); }
                    }
                    break;
                }
            case 1:   // petting
                pose = 3; Face(ap);
                if (v.vt > 3.4f) { v.vs = 2; v.vt = 0f; Animals57.I.StartFeed(s, null, this); }
                break;
            case 2:   // feeding
                pose = 8; Face(ap);
                if (v.vt > 5f) { if (pet57.kind == Animal.Kind.Dog) { v.vs = 20; v.vt = 0f; } else { wave = 2f; EndVerb(); } }
                break;
            case 10:  // riding: sit on its back
                {
                    if (s.mode != Animals57.Mode.Ridden || s.r != this) { yOver = float.NaN; Vector3 off = ap + pet57.transform.right * 1.2f; transform.position = new Vector3(off.x, ap.y, off.z); EndVerb(); break; }
                    Vector3 seat = pet57.transform.TransformPoint(s.seatLocal);
                    transform.position = seat; yOver = seat.y;
                    transform.rotation = pet57.transform.rotation;
                    pose = 9;
                    break;
                }
            case 20:  // fetch: throw (wind-up / follow-through), wait for the dog to bring it back
                {
                    if (s.mode == Animals57.Mode.None || s.mode == Animals57.Mode.Follow)
                    {
                        if (pet57N >= 3) { f.Toast(robotName + ": good dog! That was fun, " + (pet57.petName.Length > 0 ? pet57.petName : "buddy") + "!", 2f); EndVerb(); break; }
                        Face(ap + (ap - transform.position)); pose = v.vt < 0.5f ? 16 : 17;
                        if (v.vt > 0.7f) { pet57N++; v.vt = 0f; Animals57.I.StartFetch(s, null, this); }
                    }
                    else { pose = 0; Face(ap); v.vt = 0f; }
                    break;
                }
        }
    }

    void EndPets57()
    {
        if (pet57 != null && Animals57.I != null)
        {
            var s = Animals57.I.Of(pet57);
            if (s.r == this && s.mode == Animals57.Mode.Ridden) Animals57.I.EndRide(s);
        }
        if (visit != null && visit.verb == "ridepet") yOver = float.NaN;
        pet57 = null;
    }
}
