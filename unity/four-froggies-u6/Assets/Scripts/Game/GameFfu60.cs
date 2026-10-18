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
    float PhaseS = 28f;   // ffu6l: &p60s=<seconds> (probe: long pond phase so a slow SwiftShader shot lands in it)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        if (Ffu33.Url("ffdemo") == null || Ffu33.Url("ffshot") != "perf60") return;
        I = new GameObject("ffu60").AddComponent<Ffu60>();
        string o = Ffu33.Url("p60");
        if (!string.IsNullOrEmpty(o)) I.order = o.Split(',');
        float ps; if (float.TryParse(Ffu33.Url("p60s") ?? "", System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out ps) && ps > 5f) I.PhaseS = ps;
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
            case "pond": if (step == 0) { step = 1; Diag(); } Cam(new Vector3(10f, 9f, -40f), new Vector3(70f, -1f, -80f)); break;
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

    // ffu6m diag (6.6 probe findings): the pond surface draws nothing from the shore cam (R-pond) and frogs / the lander cast no
    // sun shadows (Q-moon, O-light). One line with the water renderer + material state, the pond globals and the shadow chain.
    void Diag()
    {
        string s = "FF6 diag pond: ";
        var go = GameObject.Find("PondWater");
        if (go == null) s += "PondWater MISSING";
        else
        {
            var r = go.GetComponent<Renderer>(); var m = r != null ? r.sharedMaterial : null;
            s += "active " + go.activeInHierarchy + " en " + (r != null && r.enabled) + " forceOff " + (r != null && r.forceRenderingOff) + " vis " + (r != null && r.isVisible) +
                 " layer " + go.layer + " rlm " + (r != null ? r.renderingLayerMask : 0u) + " bounds " + (r != null ? r.bounds.center.ToString("0.0") + "/" + r.bounds.size.ToString("0") : "-") +
                 " shader " + (m != null && m.shader != null ? m.shader.name + " sup " + m.shader.isSupported + " passes " + m.passCount + " q " + m.renderQueue : "none") +
                 " seamap " + (m != null && m.HasProperty("_SeaMap") && m.GetTexture("_SeaMap") != null) + " patch " + (m != null && m.HasProperty("_Patch") ? m.GetFloat("_Patch") : -1f);
        }
        s += " | FFPond " + Shader.GetGlobalVector("_FFPond") + " Q " + Shader.GetGlobalVector("_FFPondQ") + " RipP " + Shader.GetGlobalVector("_FFRipP") + " RipRect " + Shader.GetGlobalVector("_FFRipRect");
        // shadow chain: URP asset, the main light, the slot camera, the player's frog renderers
        var a = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        s += " | shadows asset " + (a != null ? "dist " + a.shadowDistance.ToString("0") + " casc " + a.shadowCascadeCount + " res " + a.mainLightShadowmapResolution : "none");
        var sun = RenderSettings.sun;
        if (sun != null)
        {
            var ld = sun.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
            s += " sun " + sun.name + " " + sun.shadows + " str " + sun.shadowStrength.ToString("0.00") + " en " + sun.isActiveAndEnabled + " cm " + sun.cullingMask + " rlm " + sun.renderingLayerMask +
                 (ld != null ? " srl " + ld.shadowRenderingLayers.value + " csl " + ld.customShadowLayers + " usePipe " + ld.usePipelineSettings : " noLData") + " bias " + sun.shadowBias.ToString("0.00") + "/" + sun.shadowNormalBias.ToString("0.00") +
                 " near " + sun.shadowNearPlane.ToString("0.00") + (a != null ? " abias " + a.shadowDepthBias.ToString("0.00") + "/" + a.shadowNormalBias.ToString("0.00") + " soft " + a.supportsSoftShadows + " mls " + a.supportsMainLightShadows : "");   // ffu6n
        }
        var cam = Game.I.slots[0].cam;
        var cd = cam != null ? cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() : null;
        s += " cam " + (cd != null ? "renderShadows " + cd.renderShadows : "noCData") + " qs " + QualitySettings.shadows + " " + QualitySettings.shadowDistance.ToString("0");
        if (f != null)
        {
            int n = 0, on = 0, sc = 0; string sh = "";
            foreach (var r in f.GetComponentsInChildren<Renderer>())
            {
                n++; if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) on++;
                var m = r.sharedMaterial; if (m != null && m.FindPass("ShadowCaster") >= 0) sc++;
                if (sh.Length == 0 && m != null && m.shader != null) sh = m.shader.name + " rlm " + r.renderingLayerMask + " fro " + r.forceRenderingOff;
            }
            s += " | frog rend " + n + " casting " + on + " withSCpass " + sc + " (" + sh + ")";
        }
        Debug.Log(s);
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
