using System.Collections.Generic;
using UnityEngine;

// ffu46 demo shots for the probe:
//  ?ffdemo=1&ffshot=skate46 : the half-pipe skateboard rack (5 s), James gets on a board and pushes (kick) south into the
//                              half-pipe with an ollie on the way, pumps it side to side (airs out of the coping, 180s), then
//                              under-rotates one air on purpose (90 deg) -> BAIL: tumbles off, the board rolls away; then an
//                              AI froggy walks to the rack, grabs a board and rides the pipe. Logs "FFDEMO skate46 ..." every
//                              0.5 s + "FFDEMO skate46 summary: ...". &skonly=ai skips to the AI part.
//  ?ffdemo=1&ffshot=hints46 : helper hints - first-time QUICK TIPS card + help chip on foot next to a rack board (the "ride"
//                              prompt), gets on (the timed in-vehicle hint line), opens the help screen (NOW RIDING ·
//                              SKATEBOARD + SETTINGS HINTS row), hints Off / On, closes. &hintdev=pad|keys|touch forces the
//                              device shown, &ffjoin=touch makes P1 a touch player (on-screen controls).
public partial class Game
{
    static bool Is46(string sc) { return sc == "skate46" || sc == "hints46"; }
    Frog d46F, d46Ai; Skateboard d46B, d46AiB; float d46T, d46PhT, d46Log, d46RunT, d46BailT = -1f; int d46Ph = -1; string d46Sc = "";
    readonly List<Skateboard> d46Rack = new List<Skateboard>();
    readonly HashSet<string> d46Surf = new HashSet<string>();
    float d46MaxOver, d46MinNy = 1f, d46LastBailT = -99f; int d46BailsSeen; int d46Bails0, d46Kicks0, d46Ollies0, d46Airs0, d46PipeAirs0, d46AiAirs; bool d46AiRode;

    void Demo46Start(Frog f, string sc)
    {
        d46F = f; d46T = 0f; d46PhT = 0f; d46Log = 0f; d46Sc = sc; d46Ph = 0;
        if (f.vehicle != null) f.ExitVehicle();
        SkateAI.Paused = true;
        var bai = FindObjectOfType<BikeAI>(); if (bai != null) bai.enabled = false;
        d46Rack.Clear(); foreach (var b in Skateboard.Boards) if (b.rack) d46Rack.Add(b);
        string hd = UrlParam("hintdev");
        HintForce = hd == "pad" ? (int)InputKind.Gamepad : hd == "touch" ? (int)InputKind.Touch : hd == "keys" ? (int)InputKind.Keyboard : -1;
        hpForce = HintForce;
        if (UrlParam("ffjoin") == "touch" && slots.Count > 0 && slots[0].kind != InputKind.Touch) { Kb.TestMobile = true; slots[0].kind = InputKind.Touch; Debug.Log("FFDEMO " + sc + ": P1 switched to touch"); }
        Vector3 rc = Skateboard.RackC;
        if (sc == "hints46")
        {
            SetHintsOn(true);
            foreach (var k in new[] { "Skateboard", "Bike", "GroundVehicle" }) SaveStore.SetInt("ff.used." + k, 0);
            d46B = d46Rack.Count > 3 ? d46Rack[3] : null;
            Vector3 p = d46B != null ? d46B.transform.position + new Vector3(0f, 0f, 1.7f) : rc + new Vector3(0f, 0f, 2f);
            f.SendTo(WorldId.Ranch, p, 180f);
            Debug.Log("FFDEMO hints46 start: device " + (HintForce >= 0 ? ((InputKind)HintForce).ToString() : slots[0].kind.ToString()) + " hints " + HintsOn + " board " + (d46B != null ? d46B.name : "none"));
        }
        else
        {
            f.SendTo(WorldId.Ranch, rc + new Vector3(0.5f, 0f, 3f), 180f);
            Debug.Log("FFDEMO skate46 start: " + Skateboard.Boards.Count + " boards, rack " + d46Rack.Count + " at " + rc.ToString("0.0") + " | half-pipe x " + MonsterPark.HpX + " z " + MonsterPark.HpZ0 + ".." + MonsterPark.HpZ1 + " floor y " + MonsterPark.FloorY.ToString("0.00") + " coping y " + (MonsterPark.FloorY + MonsterPark.HpR + MonsterPark.HpVert).ToString("0.00"));
            foreach (var b in Skateboard.Boards) Debug.Log("FFDEMO skate46 board " + b.name + " at " + b.transform.position.ToString("0.00") + " up " + b.transform.up.y.ToString("0.00") + " rack " + b.rack + " resizable " + BigWheels.Resizable(b) + " | " + b.HelpLine);
            if (UrlParam("skonly") == "ai") { d46Ph = 4; d46PhT = 0f; }
        }
    }

    void D46Ride(Skateboard b)
    {
        d46B = b;
        d46Bails0 = b.bails; d46Kicks0 = b.pushes; d46Ollies0 = b.ollies; d46Airs0 = b.airs; d46PipeAirs0 = b.pipeAirs; b.maxAirH = 0f;
        d46F.EnterVehicle(b);
        b.aiRider = d46F; b.aiPhase = 0; b.aiT = 0f; b.aiSpinGoal = 180f; b.aiOllie = true; b.aiOllied = false; b.aiRunSecs = 999f;
        Debug.Log("FFDEMO skate46 ride " + b.name + " | touch set " + string.Join(",", b.TouchSet) + " | wheels resizable " + BigWheels.Resizable(b));
    }

    bool Demo46Cam(Frog f, string sc, out Vector3 pos, out Vector3 look)
    {
        pos = look = Vector3.zero;
        if (d46Ph < 0) return false;
        float dt = Mathf.Min(Time.deltaTime, 0.4f);
        d46T += dt; d46PhT += dt; d46Log -= dt;
        if (sc == "hints46") return Hints46Tick(f, out pos, out look);
        Vector3 rc = Skateboard.RackC; float gy = MonsterPark.FloorY;
        // ---- phases ----
        if (d46Ph == 0 && d46PhT > 5f && d46Rack.Count > 1) { d46Ph = 1; d46PhT = 0f; D46Ride(d46Rack[1]); Debug.Log("FFDEMO skate46 phase 1 push t=" + d46T.ToString("0.0")); }
        else if (d46Ph == 1 && d46B != null && (d46B.aiPhase >= 1 || d46PhT > 20f)) { d46Ph = 2; d46PhT = 0f; Debug.Log("FFDEMO skate46 phase 2 half-pipe runs t=" + d46T.ToString("0.0") + " pushes " + (d46B.pushes - d46Kicks0) + " ollies " + (d46B.ollies - d46Ollies0)); }
        else if (d46Ph == 2 && d46PhT > 36f) { d46Ph = 3; d46PhT = 0f; if (d46B != null) d46B.aiSpinGoal = 90f; Debug.Log("FFDEMO skate46 phase 3 bail run (spin goal 90) t=" + d46T.ToString("0.0")); }
        else if (d46Ph == 3 && d46B != null && d46B.bails > d46Bails0 && d46BailT < 0f) { d46BailT = d46T; Debug.Log("FFDEMO skate46 bail seen t=" + d46T.ToString("0.0") + " (" + d46B.lastBail + ")"); }
        else if (d46Ph == 3 && ((d46BailT > 0f && d46T - d46BailT > 7f) || d46PhT > 26f))
        {
            D46Summary("rider");
            if (d46F.vehicle != null) d46F.ExitVehicle();
            d46Ph = 4; d46PhT = 0f;
        }
        else if (d46Ph == 4 && d46PhT < 0.05f) { }
        if (d46Ph == 4 && d46Ai == null && d46PhT > 0.1f)
        {
            foreach (Frog o in frogs) if (o != null && o != d46F && !o.human) { d46Ai = o; break; }
            if (d46Ai != null)
            {
                if (d46Ai.vehicle != null) d46Ai.ExitVehicle();
                d46Ai.SendTo(WorldId.Ranch, rc + new Vector3(5f, 0f, 9f), 200f);
                Frog sent = SkateAI.I != null ? SkateAI.I.Send(d46Ai) : null;
                Debug.Log("FFDEMO skate46 phase 4 AI t=" + d46T.ToString("0.0") + " " + d46Ai.nick + " sent " + (sent != null) + " idle " + d46Ai.AiIdleOnRanch);
            }
        }
        if (d46Ph == 4 && d46Ai != null && d46Ai.vehicle is Skateboard && !d46AiRode) { d46AiRode = true; d46AiB = (Skateboard)d46Ai.vehicle; d46AiAirs = d46AiB.airs; d46AiB.maxAirH = 0f; Debug.Log("FFDEMO skate46 AI " + d46Ai.nick + " is on " + d46AiB.name + " t=" + d46T.ToString("0.0")); }
        if (d46Ph == 4 && (d46PhT > 62f || (d46AiRode && d46AiB != null && d46AiB.driver == null && d46PhT > 20f)))
        {
            d46Ph = 5;
            Debug.Log("FFDEMO skate46 summary ai: " + (d46Ai != null ? d46Ai.nick : "-") + " rode " + d46AiRode + (d46AiB != null ? " | airs " + (d46AiB.airs - d46AiAirs) + " | max over coping " + d46AiB.maxAirH.ToString("0.00") + " m | bails " + d46AiB.bails + " | on board " + (d46AiB.driver != null) : "") + " | SkateAI rides " + SkateAI.Rides);
            Debug.Log("FFDEMO skate46 done");
        }
        // ---- stats + log ----
        Skateboard lb = d46Ph == 4 || d46Ph == 5 ? d46AiB : d46B;
        if (d46B != null && d46B.bails != d46BailsSeen) { d46BailsSeen = d46B.bails; d46LastBailT = d46T; }
        if (lb != null && lb.driver != null)
        {
            d46Surf.Add(lb.lastSurface);
            d46MaxOver = Mathf.Max(d46MaxOver, lb.AirOverCoping);
            if (lb.mode == Skateboard.Mode.Ground) d46MinNy = Mathf.Min(d46MinNy, lb.Normal.y);
        }
        if (d46Log <= 0f && lb != null)
        {
            d46Log = 0.5f;
            Vector3 p = lb.transform.position;
            Debug.Log("FFDEMO skate46 t=" + d46T.ToString("0.0") + " ph=" + d46Ph + " " + (lb.driver != null ? lb.driver.nick : "nobody") + " mode=" + lb.mode + " v=" + lb.SignedSpeed.ToString("0.0") + " u=" + (p.x - MonsterPark.HpX).ToString("0.00") + " y=" + (p.y - gy).ToString("0.00") + " z=" + p.z.ToString("0.0") + " n.y=" + lb.Normal.y.ToString("0.00") + " over=" + lb.AirOverCoping.ToString("0.00") +
                      " spin=" + Mathf.RoundToInt(lb.SpinDeg) + " kicks=" + lb.pushes + " ollies=" + lb.ollies + " airs=" + lb.airs + " pipeAirs=" + lb.pipeAirs + " bails=" + lb.bails + " rolling=" + lb.rolling + " surf=" + lb.lastSurface + " hud='" + (lb.driver != null ? lb.HudStatus : "") + "' " + lb.VoiceLevels +
                      (d46F != null && d46F.GetComponent<FrogTumble>() != null && d46F.GetComponent<FrogTumble>().Active ? " TUMBLE frog y " + (d46F.transform.position.y - gy).ToString("0.00") : ""));
        }
        // ---- camera ----
        if (d46Ph == 0) { pos = rc + new Vector3(-2.6f, 1.7f, 3.6f); look = rc + new Vector3(0.4f, 0.1f, -0.3f); return true; }
        Skateboard cb = lb != null ? lb : d46B;
        if (d46Ph == 4 && d46Ai != null && !d46AiRode) { Vector3 ap = d46Ai.transform.position; pos = ap + new Vector3(4f, 3f, 6f); look = ap + Vector3.up * 0.6f; return true; }
        if (cb == null) { pos = rc + new Vector3(0f, 5f, 12f); look = rc; return true; }
        // ffu46c: right after a bail, frame the tumbling froggy + the loose board (the end-on pipe view lost them)
        if ((d46Ph == 2 || d46Ph == 3) && d46F.vehicle == null && d46B != null && d46B.bails > d46Bails0 && d46T - d46LastBailT < 6f)
        {
            Vector3 fp = d46F.transform.position, mid = (fp + d46B.transform.position) * 0.5f;
            pos = new Vector3(MonsterPark.HpX + Mathf.Sign(MonsterPark.HpX - mid.x + 0.01f) * 1.5f, MonsterPark.FloorY + 3.6f, mid.z + 7.5f); look = mid + Vector3.up * 0.4f;
            return true;
        }
        Vector3 bp = cb.transform.position;
        bool inPipe = bp.z < MonsterPark.HpZ1 - 0.5f;
        if (d46Ph == 1 && !inPipe)
        {
            Vector3 fw = cb.transform.forward; fw.y = 0f; fw = fw.sqrMagnitude > 0.01f ? fw.normalized : Vector3.back;
            Vector3 rt = Vector3.Cross(Vector3.up, fw);
            pos = bp - fw * 3.6f + rt * 2.2f + Vector3.up * 1.6f; look = bp + fw * 1.5f + Vector3.up * 0.5f;
            return true;
        }
        // the classic end-on half-pipe view, following the rider down the pipe
        float cz = Mathf.Clamp(bp.z, MonsterPark.HpZ0 + 2f, MonsterPark.HpZ1) + 10f;
        pos = new Vector3(MonsterPark.HpX + 1.2f, gy + 4.3f, cz); look = new Vector3(MonsterPark.HpX, gy + 3.1f, cz - 10f);   // ffu46b: under the HALF-PIPE board (5.1 m+)
        return true;
    }

    void D46Summary(string who)
    {
        var b = d46B; if (b == null) return;
        Debug.Log("FFDEMO skate46 summary " + who + ": kicks " + (b.pushes - d46Kicks0) + " | ollies " + (b.ollies - d46Ollies0) + " | airs (all) " + (b.airs - d46Airs0) + " | out of the coping " + (b.pipeAirs - d46PipeAirs0) + " | max over coping " + b.maxAirH.ToString("0.00") + " m (seen " + d46MaxOver.ToString("0.00") + ") | max speed " + b.maxSpeed.ToString("0.0") + " m/s | min ground n.y " + d46MinNy.ToString("0.00") +
                  " | bails " + (b.bails - d46Bails0) + " (" + b.lastBail + ") | board after bail: rolling " + b.rolling + " at " + b.transform.position.ToString("0.0") + " | surfaces " + string.Join(",", new List<string>(d46Surf).ToArray()) + " | James on board " + (d46F.vehicle == b));
    }

    bool Hints46Tick(Frog f, out Vector3 pos, out Vector3 look)
    {
        Skateboard b = d46B;
        Vector3 fp = f.vehicle != null ? f.vehicle.transform.position : f.transform.position;
        pos = fp + new Vector3(2.8f, 1.9f, 4.2f); look = fp + Vector3.up * 0.5f;
        if (d46Ph == 0 && d46PhT > 7f && b != null) { d46Ph = 1; d46PhT = 0f; f.EnterVehicle(b); Debug.Log("FFDEMO hints46 on the board t=" + d46T.ToString("0.0") + " first use " + f.vehFirst); }
        else if (d46Ph == 1 && d46PhT > 7f) { d46Ph = 2; d46PhT = 0f; help = true; helpBy = slots.Count > 0 ? slots[0] : null; Debug.Log("FFDEMO hints46 help open t=" + d46T.ToString("0.0")); }
        else if (d46Ph == 2 && d46PhT > 7f) { d46Ph = 3; d46PhT = 0f; SetHintsOn(false); Debug.Log("FFDEMO hints46 hints off t=" + d46T.ToString("0.0")); }
        else if (d46Ph == 3 && d46PhT > 4f) { d46Ph = 4; d46PhT = 0f; SetHintsOn(true); help = false; Debug.Log("FFDEMO hints46 hints on + help closed t=" + d46T.ToString("0.0")); }
        else if (d46Ph == 4 && d46PhT > 5f) { d46Ph = 5; Debug.Log("FFDEMO hints46 done " + HelpLayoutInfo); }
        if (d46Log <= 0f)
        {
            d46Log = 1f;
            string pr = HintPrompt(f).Replace("\n", " / ");
            Debug.Log("FFDEMO hints46 t=" + d46T.ToString("0.0") + " ph=" + d46Ph + " device " + HintKind(f) + " chip '" + (chipLabel != null ? chipLabel.text : "") + "' " + (hpMenuChip != null && hpMenuChip.gameObject.activeSelf ? "shown " + hpMenuChip.anchoredPosition.ToString("0") + " " + hpMenuChip.sizeDelta.ToString("0") : "hidden") +
                      " | tips " + (tipsCard != null && tipsCard.gameObject.activeSelf ? HintInfo : "hidden") + " | prompt '" + pr + "' | vehT " + f.vehT.ToString("0.0") + " | help " + help + (help ? " " + HelpLayoutInfo + " hints row '" + (hpHints != null ? hpHints.text : "") + "'" : "") + " | touch " + (touch != null && touch.active));
        }
        return true;
    }
}
