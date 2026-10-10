using UnityEngine;

// ffu22 story mode screenshot demos for work/webgl-probe: ?ffdemo=1&ffshot=storyintro | storyparts | storystorm |
// storyfail | storyrelaunch | storyend (optionally &storych=N). The lobby joins keyboard P1 and presses STORY; lines
// auto-advance; each demo scripts the moment worth photographing. Logs "FFSTORY demo ..." lines to match shots to.
public partial class Story
{
    float demoT; int demoPhase;

    static int DemoChapter(string shot)
    {
        switch (shot)
        {
            case "storyintro": return 1;
            case "storyparts": return 2;
            case "storystorm": return 4;
            case "storyfail": return 5;
            case "storyrelaunch": return 6;
            case "storyend": return 7;
        }
        return 0;
    }
    static int DemoStep(string shot)
    {
        switch (shot)
        {
            case "storystorm": return 1;
            case "storyrelaunch": return 2;
        }
        return 0;
    }

    void DemoAfterStart()
    {
        demoT = 0f; demoPhase = 0;
        Frog f = Lead;
        switch (demoShot)
        {
            case "storyparts":
                ResetParts(1 | 16, true);
                ApplyRocket();
                {
                    Vector3 tp = Layout.TrackPoint(1.0f); tp.y = Ranch.GY(tp.x, tp.z) + 0.3f;
                    f.SendTo(WorldId.Ranch, tp, 60f);
                    G.StoryFaceCam(60f);
                }
                break;
            case "storystorm":
                {
                    Vector3 p = P + new Vector3(-6f, 0f, 14f); p.y = Ranch.GY(p.x, p.z) + 0.3f;
                    f.SendTo(WorldId.Ranch, p, 160f);
                    G.StoryFaceCam(160f);
                }
                break;
            case "storyrelaunch":
                fixedFins = fixedTank = fixedChip = true; ApplyRocket();
                break;
            case "storyend":
                rocksLeft = 0;
                foreach (var r in S.rocks) r.gameObject.SetActive(false);
                break;
        }
        // &storyspeed=N (demo only): run the story N x faster so a whole scene fits in one SwiftShader probe
        string sp = Param("storyspeed"); float spd;
        if (sp != null && float.TryParse(sp, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out spd)) Time.timeScale = Mathf.Clamp(spd, 0.25f, 4f);
        Debug.Log("FFSTORY demo start " + demoShot + " ch " + ch + " t=" + Time.realtimeSinceStartup.ToString("0.0"));
    }

    void DemoTick(float dt)
    {
        demoT += dt;
        Frog f = Lead;
        int ph = demoPhase;
        switch (demoShot)
        {
            case "storyparts":
                if (demoPhase == 0 && demoT > 14f)
                {
                    demoPhase = 1;   // grab a fin
                    foreach (var it in S.fins) if (!it.taken) { f.SendTo(WorldId.Ranch, it.basePos - Vector3.up * 1.2f, 60f); break; }
                }
                if (demoPhase == 1 && demoT > 22f)
                {
                    demoPhase = 2;   // all fins + the nose cone in hand, walk up to the pad: watch them fly into place
                    foreach (var it in S.fins) it.taken = true;
                    foreach (var it in S.fins) it.t.gameObject.SetActive(false);
                    Carry(3, f);
                    noseRevealed = true; Carry(2, f);
                    Vector3 p = P + new Vector3(4f, 0f, 22f); p.y = Ranch.GY(p.x, p.z) + 0.3f;
                    f.SendTo(WorldId.Ranch, p, 180f);
                    G.StoryFaceCam(185f);
                }
                if (demoPhase == 2 && demoT > 25f) { demoPhase = 3; Vector3 p = P + new Vector3(3f, 0f, 12f); p.y = Ranch.GY(p.x, p.z) + 0.3f; f.SendTo(WorldId.Ranch, p, 180f); G.StoryFaceCam(185f); }
                break;
            case "storystorm":
                if (demoPhase == 0 && demoT > 6f) { demoPhase = 1; Vector3 q = P + new Vector3(-20f, 0f, 4f); q.y = Ranch.GY(q.x, q.z); S.Strike(q); }
                if (demoPhase == 1 && demoT > 16f) { demoPhase = 2; Vector3 q = P + new Vector3(18f, 0f, -10f); q.y = Ranch.GY(q.x, q.z); S.Strike(q); }
                if (demoPhase == 2 && demoT > 26f) { demoPhase = 3; Vector3 q = P + new Vector3(-12f, 0f, 20f); q.y = Ranch.GY(q.x, q.z); S.Strike(q); }
                if (demoPhase >= 1 && Mathf.Repeat(demoT, 4f) < dt) { Vector3 p = P + new Vector3(-6f, 0f, 14f); p.y = Ranch.GY(p.x, p.z) + 0.3f; if ((f.transform.position - p).magnitude > 4f) f.SendTo(WorldId.Ranch, p, 160f); }
                break;
            case "storyfail":
                if (demoPhase == 0 && demoT > 2f && !runner.Busy) { demoPhase = 1; Play(FailCo()); }
                break;
            case "storyrelaunch":
                if (demoPhase == 0 && demoT > 2f && !runner.Busy && ch == 6) { demoPhase = 1; step = 2; Play(RelaunchCo()); }
                break;
            case "storyend":
                if (demoPhase == 0 && demoT > 1.5f && ch == 7)
                {
                    demoPhase = 1;
                    for (int k = 0; k < 4; k++) { pupFound[k] = true; S.pups[k].follow = f.transform; S.pups[k].transform.position = S.MarsRocketPos + new Vector3(-2f + k, 0.2f, 6f); S.pups[k].area = new Rect(Worlds.MarsO.x - 95f, Worlds.MarsO.z - 95f, 190f, 190f); }
                    Vector3 p = S.MarsRocketPos + new Vector3(0f, 0.5f, 6f);
                    f.SendTo(WorldId.Mars, p, 180f);
                    step = 1;
                }
                break;
        }
        if (ph != demoPhase) Debug.Log("FFSTORY demo phase " + demoPhase + " t=" + Time.realtimeSinceStartup.ToString("0.0"));
    }
}
