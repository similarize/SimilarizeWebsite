using UnityEngine;

// ffu32: AI froggies (open seats) now and then go and relax in the garden retreat - lie in a hammock or swing by the
// campfire (and sometimes light it) - and ride a horse: they mount a free horse and ride alongside when player 1 is out on
// horseback, or take a short ride round the pasture when they happen to be near. Called first thing in Frog.Think;
// true = it set this AI froggy's input this frame. Off in story mode, online puppets, robot pilots and other worlds.
public partial class Frog
{
    public GardenSeat gardenSeat;          // the seat this AI froggy is heading to / sitting in
    public Horse aiHorse;                  // the horse this AI froggy is heading to / riding
    float gardenT, gardenCool = -1f, gardenWalkT, horseT, horseCool = -1f, horseWalkT, horseGoalT, gardenCheckT;
    Vector3 horseGoal;

    Frog GardenLead()
    {
        if (Game.I == null) return null;
        foreach (var f in Game.I.frogs) if (f != null && f.human && f != this) return f;
        return null;
    }

    // demo / scripted: put this AI froggy in a seat (or on a horse) for `secs`
    public void AiSit(GardenSeat s, float secs)
    {
        if (s == null || s.driver != null) return;
        if (vehicle != null) ExitVehicle();
        gardenSeat = s; Garden.Claim(s, this);
        EnterVehicle(s); gardenT = secs;
    }
    public void AiRide(Horse h, float secs)
    {
        if (h == null || h.driver != null) return;
        if (vehicle != null) ExitVehicle();
        aiHorse = h; EnterVehicle(h); horseT = secs;
    }

    void GardenRelease()
    {
        if (vehicle != null && (vehicle == gardenSeat || vehicle == aiHorse)) ExitVehicle();
        gardenSeat = null; aiHorse = null;
    }

    bool GardenThink(float dt)
    {
        if (human || netPuppet || robotPilot != null) return false;
        if (world != WorldId.Ranch || Story.Active || launching || passengerOf != null || remote != null)
        {
            if (gardenSeat != null || aiHorse != null) GardenRelease();
            return false;
        }
        if (gardenCool < 0f) { gardenCool = Random.Range(25f, 60f); horseCool = Random.Range(20f, 50f); }
        gardenCool -= dt; horseCool -= dt;
        Frog lead = GardenLead();
        var i = new PIn();

        // ---- in a hammock / on a swing ----
        GardenSeat gs = vehicle as GardenSeat;
        if (gs != null)
        {
            gs.aiPush = Mathf.PerlinNoise(Time.time * 0.2f, id) > 0.62f ? 0.35f : 0f;
            gardenT -= dt;
            bool leadGone = lead != null && lead.world != WorldId.Ranch;
            if (gardenT <= 0f || leadGone)
            {
                ExitVehicle(); gardenSeat = null; gardenCool = Random.Range(45f, 90f);
                i.hop = true;
            }
            input = i; camYaw = 0f;
            return true;
        }
        // ---- on a horse ----
        Horse rh = vehicle as Horse;
        if (rh != null)
        {
            horseT -= dt;
            bool leadRiding = lead != null && lead.vehicle is Horse && lead.world == WorldId.Ranch;
            if ((horseT <= 0f && !leadRiding) || (lead != null && lead.world != WorldId.Ranch))
            {
                ExitVehicle(); aiHorse = null; horseCool = Random.Range(60f, 120f);
                input = i; return true;
            }
            if (leadRiding)
            {
                // ride alongside player 1: beside and a little behind, spread out by seat
                Transform lt = lead.vehicle.transform;
                float side = (id % 2 == 0 ? -1f : 1f) * (3.2f + (id / 2) * 2.6f);
                Vector3 goal = lt.position + lt.right * side - lt.forward * 3f;
                i = rh.SteerTo(goal, 1.5f, true);
                // match a fast player rather than idling right beside them
                if ((goal - transform.position).magnitude < 6f && lead.vehicle.Speed > 3f) { i.move.y = Mathf.Clamp(lead.vehicle.Speed / 8f, 0.2f, 0.95f); i.boostHeld = lead.vehicle.Speed > 10f; }
            }
            else
            {
                horseGoalT -= dt;
                if (horseGoalT <= 0f || (horseGoal - transform.position).sqrMagnitude < 9f)
                {
                    horseGoalT = Random.Range(6f, 12f);
                    Rect P = RanchLife.Pasture;
                    horseGoal = new Vector3(Random.Range(P.xMin + 6f, P.xMax - 6f), 0f, Random.Range(P.yMin + 6f, P.yMax - 6f));
                }
                i = rh.SteerTo(horseGoal, 2f, false);
                if (i.move.y > 0.75f) i.move.y = 0.75f;
            }
            input = i; camYaw = 0f;
            return true;
        }
        if (vehicle != null) return false;

        // ---- walking to a seat ----
        if (gardenSeat != null)
        {
            if (gardenSeat.driver != null || Garden.I == null) { gardenSeat = null; return false; }
            gardenWalkT += dt;
            Vector3 sp = gardenSeat.standPoint;
            Vector3 to = sp - transform.position; to.y = 0f;
            float d = gardenSeat.body != null ? (gardenSeat.body.ClosestPoint(transform.position + Vector3.up * 0.6f) - (transform.position + Vector3.up * 0.6f)).magnitude : to.magnitude;
            if (d < 1.4f || to.magnitude < 0.8f)
            {
                if (gardenSeat.kind == GardenSeat.Kind.Swing && Campfire.I != null && !Campfire.I.Lit && Random.value < 0.5f) Campfire.I.Light(this);
                EnterVehicle(gardenSeat);
                gardenT = Random.Range(22f, 45f);
                Debug.Log("FFGARDEN " + nick + " relaxing in the " + gardenSeat.Title + " t=" + Time.realtimeSinceStartup.ToString("0.0"));
                input = new PIn(); return true;
            }
            if (gardenWalkT > 50f)
            {
                // taking forever (blocked somewhere): a little hop-over puff, like the tag-along
                FX.Smoke(transform.position + Vector3.up * 0.4f, 1.4f, new Color(0.88f, 0.88f, 0.88f, 0.7f));
                Teleport(sp + Vector3.up * 0.3f);
                gardenWalkT = 0f;
            }
            WalkInput(ref i, to, dt);
            input = i; camYaw = 0f;
            return true;
        }
        // ---- walking to a horse ----
        if (aiHorse != null)
        {
            if (aiHorse.driver != null) { aiHorse = null; return false; }
            horseWalkT += dt;
            Vector3 to = aiHorse.transform.position - transform.position; to.y = 0f;
            float d = aiHorse.body != null ? (aiHorse.body.ClosestPoint(transform.position + Vector3.up * 0.6f) - (transform.position + Vector3.up * 0.6f)).magnitude : to.magnitude;
            if (d < 1.8f)
            {
                EnterVehicle(aiHorse);
                horseT = Random.Range(30f, 55f);
                Debug.Log("FFHORSE " + nick + " mounted horse " + aiHorse.index + " t=" + Time.realtimeSinceStartup.ToString("0.0"));
                input = new PIn(); return true;
            }
            if (horseWalkT > 45f) { aiHorse = null; horseCool = 40f; return false; }
            WalkInput(ref i, to, dt);
            input = i; camYaw = 0f;
            return true;
        }

        // ---- decide ----
        gardenCheckT -= dt;
        if (gardenCheckT > 0f) return false;
        gardenCheckT = Random.Range(6f, 11f);
        // horse: player 1 is out riding -> grab a free horse nearby and ride along; else a rare ride when near the pasture
        if (horseCool <= 0f && Horse.Herd.Count > 0)
        {
            bool leadRiding = lead != null && lead.vehicle is Horse && lead.world == WorldId.Ranch;
            Horse h = Horse.FreeNear(transform.position, leadRiding ? 90f : 45f);
            if (h != null && (leadRiding || Random.value < 0.25f)) { aiHorse = h; horseWalkT = 0f; return true; }
        }
        if (Garden.I != null && gardenCool <= 0f)
        {
            bool leadThere = lead != null && lead.world == WorldId.Ranch && (lead.transform.position - Garden.I.C).sqrMagnitude < 38f * 38f;
            if (Random.value < (leadThere ? 0.35f : 0.06f))
            {
                GardenSeat.Kind? k = null;
                if (lead != null && lead.vehicle is GardenSeat) k = ((GardenSeat)lead.vehicle).kind;
                else if (Random.value < 0.5f) k = Random.value < 0.5f ? GardenSeat.Kind.Hammock : GardenSeat.Kind.Swing;
                GardenSeat s = GardenSeat.FreeNear(transform.position, 600f, k) ?? GardenSeat.FreeNear(transform.position, 600f);
                if (s != null) { gardenSeat = s; Garden.Claim(s, this); gardenWalkT = 0f; Debug.Log("FFGARDEN " + nick + " heading to the " + s.Title); return true; }
            }
        }
        return false;
    }

    void WalkInput(ref PIn i, Vector3 to, float dt)
    {
        float m = to.magnitude;
        if (m > 0.3f) { Vector3 d = to / m; i.move = new Vector2(d.x, d.z) * (m > 4f ? 1f : 0.6f); }
        aiHopT -= dt;
        if (aiHopT <= 0f) { aiHopT = Random.Range(2.5f, 5f); i.hop = m > 6f && Random.value < 0.35f; }
        if (cc != null && cc.isGrounded && m > 1.5f && planar.magnitude < 0.6f && Random.value < dt * 2.5f) i.hop = true;   // blocked (a fence rail): hop
    }
}
