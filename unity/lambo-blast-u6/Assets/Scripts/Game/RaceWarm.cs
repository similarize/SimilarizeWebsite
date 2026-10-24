using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

// u6g: RACE -> "LOADING TRACK" overlay until the race view is really on screen, then the countdown.
// Why: on WebGPU the first race frames create every new render pipeline (track / scans / instanced grass + foliage / shadows /
// reflection probe / post) in the GPU process. The JS main loop keeps ticking, so the countdown beeps played while the
// canvas still showed the lobby for seconds (Bill, 2026-10-15). Now:
//   phase 0: race cameras off, a cheap UI-only camera + opaque overlay -> the overlay is presented at once;
//   phase 1: race cameras on behind the overlay (real targets / formats -> this IS the pipeline prewarm); a 1x1
//            AsyncGPUReadback each frame acts as a GPU fence (WebGPU queues are in order: it returns only when the frames
//            before it are done). Ready = >= 4 race frames, >= 0.6 s, fence round trip < 0.35 s and 2 calm frames (dt < 0.2 s),
//            or 30 s at most. Then the overlay goes, the music starts and the 3-2-1 countdown runs as before.
// u6g fix 3 (2026-10-24, Ben: desktop WebGPU sat on LOADING TRACK with a slow bar while the race audio ran): on current
// desktop Chrome/Edge the WebGPU AsyncGPUReadback callback never comes back (console: "A valid external Instance reference
// no longer exists" / "Instance dropped in popErrorScope"; headless repro: 1700 calm frames, fence rtt never set), so the
// gate always waited for the 30 s cap. Now if the first fence has not returned within FenceGiveUp, the fence is dead for the
// session (frame-time gate from then on), the hard cap is 12 s, and the finish path always switches the race cameras on and
// the overlay / load camera off (also if the state leaves the countdown some other way).
public partial class Game
{
    bool warmWait;
    int warmPhase, warmFrames, calmFrames, fenceFrames;
    float warmT0, phaseT0, fenceSentT = -1f, fenceRtt = 99f, calmDt = 0.2f;
    bool fencePending;
    RenderTexture fenceRT;
    Canvas loadCanvas;
    Text loadTitle, loadText;
    Image loadBar, loadBarBg;
    Camera loadCam;
    // u6g fix 2: fence on WebGPU only. WebGL compiles on the main thread at first draw (frame time already shows the stall), and
    // the u6g desktop WebGL probe lost its context right after the readbacks started (A-D-gl) -> no AsyncGPUReadback there.
    static bool FenceOk { get { return SystemInfo.supportsAsyncGPUReadback && U6.WebGPU && !fenceDead; } }
    static bool fenceDead;              // u6g fix 3: a readback never returned -> no fence for the rest of the session
    static bool fenceEverOk;            // u6g fix 3: at least one readback came back (then slow ones are just a busy GPU)
    const float FenceGiveUp = 2.0f;     // u6g fix 3: s the first fence may stay pending before the fence counts as dead
    const float WarmCap = 12f;          // u6g fix 3: hard cap on the overlay (was 30 s)
    static bool warmedOnce;
    float lobbyDt = 0.033f;     // u6g: lobby frame time (EMA, unscaled), sampled in UpdateLobby

    void WarmBegin()
    {
        string u = Application.absoluteURL ?? "";
        if (u.Contains("u6warm=0")) { warmWait = false; SetRaceCams(true); Sfx.Music("beach"); return; }
        warmWait = true; warmPhase = 0; warmFrames = 0; calmFrames = 0; fenceFrames = 0;
        warmT0 = phaseT0 = Time.realtimeSinceStartup; fencePending = false; fenceRtt = 99f;
        // u6g fix 2: "calm" = no slower than the lobby ran (a slow phone / software GPU never gets under a fixed 0.2 s)
        calmDt = Mathf.Max(0.2f, 1.6f * lobbyDt);
        if (loadCanvas == null) MakeLoadUI();
        if (loadCam == null)
        {
            var g = new GameObject("U6G load cam");
            loadCam = g.AddComponent<Camera>();
            loadCam.clearFlags = CameraClearFlags.SolidColor;
            loadCam.backgroundColor = new Color(0.03f, 0.04f, 0.06f);
            loadCam.cullingMask = 0;
            loadCam.depth = 90;
            loadCam.allowHDR = false; loadCam.allowMSAA = false;
        }
        loadCam.enabled = true;
        loadCanvas.enabled = true;
        loadTitle.text = "LOADING TRACK";
        SetLoad(0.04f, Track.Name.ToUpper());
        SetRaceCams(false);
        Debug.Log("U6G: RACE -> loading overlay (fence " + (FenceOk ? "AsyncGPUReadback" : "frame-time only") + ", " + (warmedOnce ? "warm" : "first race") + ")");
    }

    void SetRaceCams(bool on)
    {
        foreach (var s in slots) if (s.cam != null) s.cam.enabled = on;
        // u6g fix 3: switching back on restores the 3-player overview quarter too (ApplyLayout: overview only for 3 views)
        if (overview != null) overview.enabled = on && slots.Count == 3;
    }

    // true = ready (countdown may run)
    bool WarmTick()
    {
        if (!warmWait) return true;
        float now = Time.realtimeSinceStartup, el = now - warmT0, pe = now - phaseT0;
        bool calm = Time.unscaledDeltaTime < calmDt;
        Fence(now);
        if (warmPhase == 0)
        {
            SetRaceCams(false);
            warmFrames++;
            // the overlay frame is on screen once a fence sent after it came back (or 1.5 s)
            if ((warmFrames >= 3 && (!FenceOk || (fenceFrames >= 1 && fenceRtt < 0.5f))) || pe > 1.5f)
            {
                warmPhase = 1; phaseT0 = now; warmFrames = 0; calmFrames = 0; fenceFrames = 0; fenceRtt = 99f;
                SetRaceCams(true);
                if (loadCam != null) loadCam.enabled = false;
                Debug.Log("U6G: overlay up after " + el.ToString("F2") + " s -> race cameras on behind it");
            }
            SetLoad(0.08f, Track.Name.ToUpper());
            return false;
        }
        warmFrames++;
        calmFrames = calm ? calmFrames + 1 : 0;
        bool gpuDone = !FenceOk || (fenceFrames >= 2 && fenceRtt < Mathf.Max(0.35f, 1.5f * calmDt));
        bool ready = (warmFrames >= 4 && pe >= 0.6f && gpuDone && calmFrames >= 2) || pe > WarmCap;
        float p = 0.12f + 0.83f * (1f - Mathf.Exp(-pe / 6f));
        SetLoad(ready ? 1f : p, Track.Name.ToUpper() + "   ·   preparing graphics " + Mathf.FloorToInt(pe) + " s");
        if (!ready) return false;
        WarmFinish();
        Sfx.Music("beach");
        Debug.Log("U6G: first race frame presented " + el.ToString("F2") + " s after RACE (warm " + pe.ToString("F2") + " s, " + warmFrames + " frames, calm < " + calmDt.ToString("F2") + " s, fence rtt " +
            (FenceOk ? (fenceRtt * 1000f).ToString("F0") + " ms" : "n/a") + (pe > WarmCap ? ", TIMEOUT" : "") + ") -> countdown");
        return true;
    }

    // u6g fix 3: one place that ends the warm-up: race cameras on, overlay + load camera off
    void WarmFinish()
    {
        warmWait = false; warmedOnce = true;
        SetRaceCams(true);
        if (loadCam != null) loadCam.enabled = false;
        if (loadCanvas != null) loadCanvas.enabled = false;
    }

    // u6g fix 3: called by UpdateRace when the state is no longer Countdown; if the overlay is somehow still up, end it
    void WarmGuard()
    {
        if (!warmWait) return;
        Debug.LogWarning("U6G: state left the countdown with the loading overlay still up -> overlay off, race cameras on");
        WarmFinish();
    }

    void Fence(float now)
    {
        if (fencePending && !fenceDead && !fenceEverOk && now - fenceSentT > FenceGiveUp)
        {
            fenceDead = true; fencePending = false;
            Debug.LogWarning("U6G: GPU fence (AsyncGPUReadback) did not return within " + FenceGiveUp.ToString("F1") + " s -> frame-time gate for this session");
        }
        if (!FenceOk || fencePending) return;
        if (fenceRT == null) { fenceRT = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGB32) { name = "U6G fence" }; fenceRT.Create(); }
        fencePending = true; fenceSentT = now;
        try
        {
            AsyncGPUReadback.Request(fenceRT, 0, r =>
            {
                if (fenceDead) return;     // u6g fix 3: a late callback after the give-up changes nothing
                fenceEverOk = true;
                fencePending = false;
                fenceRtt = Time.realtimeSinceStartup - fenceSentT;
                fenceFrames++;
            });
        }
        catch (System.Exception e) { fencePending = false; Debug.LogWarning("U6G: fence failed " + e.Message); }
    }

    void SetLoad(float f, string line)
    {
        if (loadBar == null) return;
        loadBar.rectTransform.sizeDelta = new Vector2(Mathf.Max(8f, 520f * Mathf.Clamp01(f)), 8f);
        loadText.text = line;
    }

    void MakeLoadUI()
    {
        loadCanvas = UIK.MakeCanvas("Loading", null, 190, true);
        var bg = UIK.Img(loadCanvas.transform, null, new Color(0.03f, 0.04f, 0.06f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10, 10));
        UIK.Stretch(bg.rectTransform);
        Transform r = loadCanvas.transform;
        loadTitle = UIK.Label(r, "LOADING TRACK", 46, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 46), new Vector2(1000, 64), Color.white);
        UIK.Modernize(loadTitle, true, 0.3f);
        loadText = UIK.Label(r, "", 18, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -4), new Vector2(1000, 30), new Color(0.75f, 0.85f, 0.95f));
        loadBarBg = UIK.Img(r, null, new Color(1f, 1f, 1f, 0.12f), new Vector2(0.5f, 0.5f), new Vector2(0, -44), new Vector2(520, 8));
        loadBar = UIK.Img(r, null, new Color(0.36f, 0.98f, 0.62f), new Vector2(0.5f, 0.5f), new Vector2(-260, -44), new Vector2(8, 8));
        loadBar.rectTransform.pivot = new Vector2(0f, 0.5f);
        var hint = UIK.Label(r, "first race on this device warms up the graphics - later races start faster", 13, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -78), new Vector2(1000, 24), new Color(0.6f, 0.68f, 0.78f, 0.8f));
        loadCanvas.enabled = false;
    }

    void WarmCancel()
    {
        if (!warmWait) return;
        warmWait = false;
        if (loadCanvas != null) loadCanvas.enabled = false;
        if (loadCam != null) loadCam.enabled = false;
    }
}
