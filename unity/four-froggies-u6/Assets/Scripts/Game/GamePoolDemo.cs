using UnityEngine;

// ffu29 demo shot ?ffdemo=1&ffshot=poolswim: P1 (James) starts on the west deck of the backyard pool, walks over the coping
// into the water and floats on his back; Jimmy is put floating in the east half (stays); P1 drifts head-first round the
// pool with lazy backstrokes, idles sprawled out, then climbs out (hop) at demo t ~44 s. Camera: 3/4 view from the south
// deck following P1. Logs "FFDEMO pool t floating pos" every 2 s plus the FFPOOL enter / climb-out lines.
public partial class Game
{
    public static bool IsPoolDemo(string sc) { return sc == "poolswim"; }
    float poolT, poolLogT;

    void PoolDemoStart(Frog f, string sc)
    {
        if (f.world != WorldId.Ranch) f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f);
        if (f.vehicle != null) f.ExitVehicle();
        if (!Roster.IsFrog(f.charId) || f.charId != 0) SetSeatChar(f.id, 0, false);
        demoKeepChars = true;
        Frog.TagOn = false;
        poolT = 0f; poolLogT = 0f;
        Vector2 c = Layout.PoolC, s = Layout.PoolSize;
        f.DemoPose(new Vector3(c.x - s.x * 0.5f - 2.2f, 0f, c.y - 0.5f), 90f);
        foreach (Frog o in frogs)
            if (o != null && !o.human && o.charId == 1)
            {
                if (o.vehicle != null) o.ExitVehicle();
                if (o.world != WorldId.Ranch) o.SendTo(WorldId.Ranch, Ranch.FrogSpawn(o.id), 0f);
                o.demoFloatStay = true;
                o.Teleport(new Vector3(c.x + 4f, Ranch.GY(c.x + 4f, c.y + 1.5f) + 0.05f, c.y + 1.5f));
                o.StartFloat();
            }
        demoHook = PoolDemoInput;
        Debug.Log("FFDEMO pool start t=" + Time.realtimeSinceStartup.ToString("0.0"));
    }

    PIn PoolDemoInput(PIn i)
    {
        Frog f = P1Frog; var o = new PIn(); o.look = i.look;
        if (f == null) return o;
        Vector2 c = Layout.PoolC;
        Vector3 p = f.transform.position;
        float t = poolT;
        if (!f.Floating)
        {
            if (t < 30f) o.move = StickTo(f, new Vector3(c.x - 3f, 0f, c.y) - p, 0.7f);   // walk in from the west deck
            return o;
        }
        if (t < 24f) o.move = StickTo(f, new Vector3(Mathf.Sin(t * 0.25f), 0f, Mathf.Cos(t * 0.25f) * 0.6f), 0.8f);   // lazy loop
        else if (t < 40f) o.move = Vector2.zero;                                                                        // sprawled, drifting to a stop
        else if (t < 40.3f) o.hop = true;                                                                               // climb out
        return o;
    }

    bool PoolDemoCam(Frog f, string sc, out Vector3 pos, out Vector3 look)
    {
        poolT += Time.deltaTime;
        Vector3 p = f.transform.position;
        look = new Vector3(p.x, Ranch.GY(p.x, p.z) + 0.25f, p.z);
        pos = look + new Vector3(3.2f, 3.0f, -5.2f);
        poolLogT -= Time.unscaledDeltaTime;
        if (poolLogT <= 0f) { poolLogT = 2f; Debug.Log("FFDEMO pool t=" + poolT.ToString("0.0") + " floating=" + f.Floating + " pos=" + p.ToString("0.0") + " prompt=" + f.prompt); }
        return true;
    }
}
