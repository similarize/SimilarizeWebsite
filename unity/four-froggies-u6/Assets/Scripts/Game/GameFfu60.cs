using UnityEngine;

// ffu60 demo / probe hook (Core/Perf60.cs). Only with ?ffdemo=1&ffshot=perf60 [&p60=<phases>] (+ &quality=high to pin the
// tier for before / after numbers, &perf=old = the same tour without ffu60). Wall-clock phases (28 s each):
//   ranch  the porch overview (same view as ffshot=ranch)     lawn   low over the lawn (grass, frog, parked truck)
//   pond   the pond shore view (ffshot=pond)                  under  James dives with the sub (sea gate on)
//   yard   the normal follow camera at the spawn (gameplay view)
// Logs "FFDEMO perf60 ph N <phase>" and, per phase, "FFDEMO perf60 <phase> avg X fps" from the game's own clock.
[DefaultExecutionOrder(100)]
public class Ffu60 : MonoBehaviour
{
    public static Ffu60 I;
    bool started, allDone; int ph = -1, step; float phW, acc; int frames;
    Vector3 camPos, camLook; bool camOn;
    Frog f;
    string[] order = { "ranch", "lawn", "pond", "under", "yard" };
    const float PhaseS = 28f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        if (Ffu33.Url("ffdemo") == null || Ffu33.Url("ffshot") != "perf60") return;
        I = new GameObject("ffu60").AddComponent<Ffu60>();
        string o = Ffu33.Url("p60");
        if (!string.IsNullOrEmpty(o)) I.order = o.Split(',');
        DontDestroyOnLoad(I.gameObject);
    }

    static Vector3 G(float x, float z) { return new Vector3(x, Ranch.GY(x, z), z); }
    void Cam(Vector3 p, Vector3 look) { camPos = p; camLook = look; camOn = true; }

    void Next()
    {
        if (ph >= 0 && ph < order.Length)
            Debug.Log("FFDEMO perf60 " + order[ph] + " avg " + (frames / Mathf.Max(0.01f, acc)).ToString("0.00") + " fps over " + frames + " frames, tier " + (Perf60.On ? Perf60.TierName : "OLD") + " dpr " + FFDisplay.CurrentDpr.ToString("0.0#") + " culled " + Cull60.Culled + "/" + Cull60.Tracked + " sea gate " + UnderwaterWorld.GateSea + "+" + UnderwaterWorld.GateLife + (Grass56.I != null ? " grass " + Grass56.I.DrawnInfo : ""));
        ph++; step = 0; phW = Time.realtimeSinceStartup; camOn = false; acc = 0f; frames = 0;
        Debug.Log("FFDEMO perf60 ph " + ph + " " + (ph < order.Length ? order[ph] : "end") + (Perf60.On ? " (ffu60 perf, tier " + Perf60.TierName + ")" : " (OLD perf)"));
    }

    void Update()
    {
        if (Game.I == null || Game.I.state != Game.State.Play || Game.I.slots.Count == 0) return;
        if (!started)
        {
            if (Realism.On && !Realism.Ready && Time.realtimeSinceStartup < 150f) return;   // the streamed scans (the real look)
            if (Terrain56.On && !Terrain56.Ready && Time.realtimeSinceStartup < 150f) return;
            started = true;
            f = Game.I.frogs[Game.I.slots[0].frog];
            Debug.Log("FFDEMO perf60 start order " + string.Join(",", order));
            Next();
            return;
        }
        if (ph >= order.Length) { if (!allDone) { allDone = true; Debug.Log("FFDEMO perf60 done"); } return; }
        float w = Time.realtimeSinceStartup - phW;
        if (w > 4f) { acc += Mathf.Min(Time.unscaledDeltaTime, 5f); frames++; }   // skip the first seconds (camera cut / world hop)
        if (step == 1 && w > 12f) { step = 2; Perf60.Census(Game.I.slots[0].cam); }
        switch (order[ph])
        {
            case "ranch": if (step == 0) step = 1; Cam(new Vector3(45f, 26f, 95f), new Vector3(-20f, 0f, 0f)); break;
            case "pond": if (step == 0) step = 1; Cam(new Vector3(10f, 9f, -40f), new Vector3(70f, -1f, -80f)); break;
            case "lawn":
                if (step == 0) { f.SendTo(WorldId.Ranch, G(22f, -18f) + Vector3.up * 0.3f, 90f); step = 1; }
                Cam(G(14f, -24f) + Vector3.up * 1.6f, G(30f, -12f) + Vector3.up * 0.6f);
                break;
            case "under":
                if (step == 0) { if (UnderwaterWorld.I != null) { f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f); UnderwaterWorld.I.Dive(f); } step = 1; }
                break;   // the normal (sub) follow camera
            case "yard":
                if (step == 0) { if (f.vehicle != null) f.ExitVehicle(); f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f); step = 1; }
                break;   // the normal follow camera
        }
        if (w > PhaseS) Next();
    }

    void LateUpdate()
    {
        if (!camOn || ph < 0 || ph >= order.Length || Game.I == null || Game.I.slots.Count == 0) return;
        Camera c = Game.I.slots[0].cam;
        if (c == null) return;
        c.transform.position = camPos;
        c.transform.LookAt(camLook);
    }
}
