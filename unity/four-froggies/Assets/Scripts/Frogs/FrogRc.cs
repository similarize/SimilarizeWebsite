using UnityEngine;

// ffu30: AI hooks for the RC raceway (RcTrack invites AI froggies to the driver's stand) and the garage bikes
// (BikeAI walks an AI froggy to a free bike now and then).
public partial class Frog
{
    // walk (AI wander target) towards p for the next few seconds
    public void RcInvite(Vector3 p)
    {
        if (human) return;
        aiTarget = p;
        if (aiTimer < 6f) aiTimer = 8f;
    }
    public bool AiIdleOnRanch
    {
        get
        {
            return !human && !netPuppet && robotPilot == null && vehicle == null && remote == null && passengerOf == null && jetOf == null
                && world == WorldId.Ranch && callT <= 0f && !launching && gameObject.activeInHierarchy && !Climbing;
        }
    }
}
