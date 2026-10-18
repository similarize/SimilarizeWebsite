using UnityEngine;

// ffu56 robot PLAY verbs for the forest toys (World/Forest56.cs), robot parity with James:
//   fort  = walk to the tree fort, hammer planks on until it's finished (shared progress with James), climb the rope ladder,
//           keep lookout on the deck for a while, climb down again;
//   bugs  = catch 3 bugs / fireflies in the robot's own jar, bring them to James, let them go together.
public partial class Robot
{
    string StartFort56()
    {
        var v = visit;
        if (world != WorldId.Ranch) return Say("the tree fort is in the forest at the ranch - come home and we'll build it!");
        Fort56.Build();
        v.verb = "fort"; v.vs = 0; v.vt = 0f; v.count = 0;
        return Say(Fort56.Done ? "race you up to the tree fort!" : "let's build the tree fort - I'll bring the hammer!");
    }

    void FortTick56(float dt, Vector3 lp)
    {
        var v = visit; Frog f = v.lead;
        Vector3 at = Fort56.Ladder + new Vector3(0.9f, 0f, -0.3f);
        switch (v.vs)
        {
            case 0:   // walk over
                {
                    float d = WalkTo(at, RunSpeed, 0.4f, dt);
                    if (d < 0.9f || v.vt > 90f) { v.vs = Fort56.Done ? 2 : 1; v.vt = 0f; Debug.Log("ROBOTS56 fort " + robotName + " at the tree fort, built " + Fort56.Built + " / " + Fort56.Total); }
                    break;
                }
            case 1:   // hammer planks on
                pose = 3; Face(Fort56.Base);
                if (v.vt > 0.7f)
                {
                    v.vt = 0f;
                    string s = Fort56.AddPlanks(1, robotName);
                    v.count++;
                    if (v.count % 6 == 1 || Fort56.Done) f.Toast(robotName + ": " + s, 1.8f);
                    if (Fort56.Done) { wave = 2f; v.vs = 2; v.vt = 0f; v.line = "the tree fort is finished!"; }
                }
                break;
            case 2:   // climb the rope ladder
                {
                    Vector3 l = Fort56.Ladder;
                    float y = Mathf.Min(Fort56.DeckY, l.y + v.vt * 0.9f);
                    transform.position = new Vector3(l.x, y, l.z + 0.35f); yOver = y; Face(Fort56.Base); pose = 13;
                    if (y >= Fort56.DeckY - 0.01f)
                    {
                        v.vs = 3; v.vt = 0f;
                        Vector3 ds = Fort56.RobotDeckSpot(RanchLife.I != null ? RanchLife.I.robots.IndexOf(this) : 0);
                        transform.position = ds; yOver = ds.y; wave = 2.4f;
                        f.Toast(robotName + ": lookout! I can see the pond from up in the tree fort!", 2.6f);
                        Debug.Log("ROBOTS56 fort " + robotName + " up on the deck at " + ds.ToString("0.0"));
                    }
                    break;
                }
            case 3:   // on the deck: lookout
                yOver = transform.position.y; pose = v.vt < 3f ? 0 : 8; Face(lp);
                if (v.vt > 25f) { v.vs = 4; v.vt = 0f; transform.position = new Vector3(Fort56.Ladder.x, Fort56.DeckY, Fort56.Ladder.z + 0.35f); }
                break;
            default:  // climb down
                {
                    Vector3 l = Fort56.Ladder;
                    float y = Mathf.Max(l.y, Fort56.DeckY - v.vt * 1.4f);
                    transform.position = new Vector3(l.x, y, l.z + 0.35f); yOver = y; pose = 13;
                    if (y <= l.y + 0.01f) { yOver = float.NaN; EndVerb(); }
                    break;
                }
        }
    }

    string StartBugs56()
    {
        var v = visit;
        if (world != WorldId.Ranch) return Say("the fireflies live in the forest at the ranch!");
        Forest56.Build();
        v.verb = "bugs"; v.vs = 0; v.vt = 0f; v.count = 0;
        return Say("bug hunt! I'll fill my jar with fireflies for you!");
    }

    Bug56 bugTarget;
    void BugsTick56(float dt, Vector3 lp)
    {
        var v = visit; Frog f = v.lead;
        if (v.vs == 0)
        {
            if (bugTarget == null || bugTarget.caught) bugTarget = Bug56.NearestFree(transform.position, 400f);
            if (bugTarget == null) { v.vs = 1; v.vt = 0f; return; }
            Vector3 bp = bugTarget.transform.position;
            float d = WalkTo(new Vector3(bp.x, transform.position.y, bp.z), RunSpeed, 0.5f, dt);
            if (d < 1.2f)
            {
                pose = 8;
                string s = Jar56.Put(this, carryNode != null ? carryNode : transform, Vector3.zero, bugTarget);
                bugTarget = null; v.count++;
                Debug.Log("ROBOTS56 bugs " + robotName + " caught #" + v.count + ": " + s);
                if (v.count >= 3) { v.vs = 1; v.vt = 0f; f.Toast(robotName + ": my jar is glowing - coming to show you!", 2.2f); }
            }
            if (v.vt > 120f) { v.vs = 1; v.vt = 0f; }
            return;
        }
        if (v.vs == 1)
        {
            float d = WalkTo(lp, RunSpeed, 1.6f, dt);
            if (d < 2.4f || v.vt > 90f) { v.vs = 2; v.vt = 0f; Face(lp); wave = 2f; f.Toast(robotName + ": look - " + Jar56.Count(this) + " glowing bugs! Let's let them go...", 2.4f); }
            return;
        }
        pose = 8; Face(lp);
        if (v.vt > 3f)
        {
            string s = Jar56.Release(this, transform.position + Vector3.up * 1.3f);
            f.Toast(robotName + ": " + s, 2.4f);
            Debug.Log("ROBOTS56 bugs " + robotName + " " + s);
            EndVerb();
        }
    }
}
