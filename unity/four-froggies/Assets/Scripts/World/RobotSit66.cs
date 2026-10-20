using UnityEngine;

// ffu6r (Bill 2026-10-19: "We can order the robots to come sit by us on the swing. And to come to the hammocks as well.
// Swing on the hammock."): PLAY verbs "swingsit" (Swing with me) + "hamwith" (Hammock with me), on the phone's ORDERS tab
// and by voice. The robot comes to the caller, takes the free porch swing / hammock NEAREST the caller (the one next to
// theirs when they are already in one; else it sits and the froggy can join on the next one), and while both are seated
// its seat swings / rocks in step with the caller's (GardenSeat.SyncTo). Portable: same file in 2022.3 and 6.6.
public partial class Robot
{
    [System.NonSerialized] public Frog sitWith;          // ffu6r: swing / rock in step with this froggy's seat

    string SitWith66(GardenSeat.Kind k, Frog f, bool ranch)
    {
        bool sw = k == GardenSeat.Kind.Swing;
        if (!ranch || Garden.I == null) return Say(sw ? "the swings are in the garden at the ranch" : "the hammocks are in the garden at the ranch");
        var mine = f.vehicle as GardenSeat;
        Vector3 near = mine != null ? mine.transform.position : f.transform.position;
        GardenSeat best = null; float bd = 1e9f;
        foreach (var s in GardenSeat.Seats)
        {
            if (s == null || s.kind != k || s.driver != null || s == mine) continue;
            float d = (s.transform.position - near).sqrMagnitude;
            if (d < bd) { bd = d; best = s; }
        }
        if (best == null) return Say(sw ? "all the swings are taken" : "all the hammocks are taken");
        string r = DriveOrder(best, "hold", f);
        if (drv == null) return r;
        sitWith = f; RobotSit66.Ensure();
        Debug.Log("SIT66 " + robotName + " -> " + best.name + " for " + f.nick + (mine != null ? " (next to " + mine.name + ")" : " (caller not seated)"));
        return Say(sw ? (mine != null ? "scooting onto the swing next to you - let's swing together!" : "I'll sit on a swing - come sit on the one next to me!")
                      : (mine != null ? "taking the hammock next to yours - let's rock together!" : "I'll take a hammock - hop in the one next to me!"));
    }
}

public class RobotSit66 : MonoBehaviour
{
    static RobotSit66 I;
    float logT;
    public static void Ensure() { if (I == null) { I = new GameObject("RobotSit66").AddComponent<RobotSit66>(); DontDestroyOnLoad(I.gameObject); } }
    public static GardenSeat SeatOf(Robot r) { return r != null && r.drv != null ? r.drv.v as GardenSeat : null; }

    void Update()
    {
        if (RanchLife.I == null) return;
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        logT -= Time.unscaledDeltaTime;
        foreach (var r in RanchLife.I.robots)
        {
            if (r == null || r.sitWith == null) continue;
            if (r.drv == null) { r.sitWith = null; continue; }     // another order / got out
            var gs = SeatOf(r);
            if (gs == null) { r.sitWith = null; continue; }
            if (gs.driver == null) continue;                        // still walking over
            var p = r.sitWith.vehicle as GardenSeat;
            if (p != null && p.kind == gs.kind && p != gs) { gs.SyncTo(p, dt); gs.aiPush = 0f; }
            else gs.aiPush = 0.22f;                                 // waiting for the froggy: a gentle rock
            if (logT <= 0f) { logT = 5f; Debug.Log("SIT66 " + r.robotName + " seated on " + gs.name + " angle " + gs.SwingAngle.ToString("0.0") + (p != null ? " | " + r.sitWith.nick + " on " + p.name + " angle " + p.SwingAngle.ToString("0.0") : " | " + r.sitWith.nick + " not seated")); }
        }
    }
}
