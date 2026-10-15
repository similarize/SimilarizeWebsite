using System.Collections.Generic;
using UnityEngine;

// ffu47 demo shots for the probe:
//  ?ffdemo=1&ffshot=chute47 : James hovers his VTOL jet ~28 m over the open ranch east of the garage with Jimmy riding along,
//                             EJECTS (both pop up out of the cockpit), 0.6 s freefall with the wing pack, the paraglider opens;
//                             James's camera is the real in-game glide camera (pulls back + up to frame the wing): he turns
//                             left, glides, turns right, speed bar, then FLARES at ~3 m and lands soft; the wing drops and is
//                             packed up. Then the camera frames Jimmy's (AI) canopy: he S-turns down beside James and flares.
//                             The jet flies itself home to its VTOL pad. &chuteold=1 = the ffu46 code path (helicopter bail,
//                             sphere chute, first-frame cut) for the BEFORE shot. Logs "FFDEMO chute47 ..." + summary + done.
//  ?ffdemo=1&ffshot=mechai47 : James walks west toward the mech yard; MechAI notices (player near the yard) and sends AI froggies
//                             to their own 10 / 100-story mechs; they board, walk the field, square up and fire the arm cannon
//                             into the sky, keep away from James on foot, and one walks back and climbs out. Logs "FFDEMO mechai47".
//  ?ffdemo=1&ffshot=critters47 : animal pens - James stands in the Lizard terrace (lizards climb to his shoulders), the Backyard
//                             animal yard (bunnies climb his legs, cats his back), then sprints (they hop off), the pasture
//                             (cows + horses walk up, nuzzle and lick), the house reptile room's terrarium (a lizard hops out
//                             over the glass onto him), and an AI froggy (Jimmy) in the Lizard terrace gets climbed too.
public partial class Game
{
    public static string UrlParamS(string k) { return UrlParam(k); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Ffu47Boot()
    {
        Frog.ChuteOld = UrlParam("chuteold") == "1";
        MechAI.Ensure();
        CritterPals.Ensure();
        Debug.Log("ffu47: paraglider" + (Frog.ChuteOld ? " (OLD ffu46 chute)" : "") + ", MechAI, CritterPals ready");
    }

    static bool Is47(string sc) { return sc == "chute47" || sc == "mechai47" || sc == "critters47"; }
    Frog d47F, d47Ai; float d47T, d47PhT, d47Log; int d47Ph = -1; string d47Sc = "";
    Vehicle d47V; Vector3 d47P0; float d47MinD = 1e9f, d47MinHouse = 1e9f; int d47Ph0Boards;
    readonly List<Frog> d47Ais = new List<Frog>();

    Frog D47Ai(int i)
    {
        int k = 0;
        foreach (Frog o in frogs) if (o != null && !o.human) { if (k == i) return o; k++; }
        return null;
    }

    void Demo47Start(Frog f, string sc)
    {
        d47F = f; d47T = 0f; d47PhT = 0f; d47Log = 0f; d47Sc = sc; d47Ph = 0;
        if (f.vehicle != null) f.ExitVehicle();
        SkateAI.Paused = true;
        var bai = FindObjectOfType<BikeAI>(); if (bai != null) bai.enabled = false;
        Frog.DtCap = 0.15f; Frog.GlideDtCap = 0.3f; CritterPals.DtCap = 0.3f;
        demoHook = Demo47Input;
        if (sc == "chute47")
        {
            MechAI.Paused = true; CritterPals.Paused = true;
            d47Ai = D47Ai(0);
            float alt; if (!float.TryParse(UrlParam("chutealt") ?? "", out alt)) alt = 20f;   // &chutealt=28 = higher
            Vector3 p0 = new Vector3(10f, 0f, -4f); p0.y = Ranch.GY(p0.x, p0.z) + alt; d47P0 = p0;
            if (Frog.ChuteOld)
            {
                Flyer heli = null;
                foreach (var v in Vehicle.All) { var fl = v as Flyer; if (fl != null && !fl.isDrone) { heli = fl; break; } }
                d47V = heli;
                if (heli != null)
                {
                    heli.transform.position = p0; heli.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                    if (heli.rb != null) { heli.rb.position = p0; heli.rb.rotation = heli.transform.rotation; heli.rb.velocity = Vector3.zero; }
                    f.SendTo(WorldId.Ranch, new Vector3(p0.x - 3f, Ranch.GY(p0.x - 3f, p0.z) + 0.1f, p0.z), 90f);   // stand on the ground first (walks a second, then gets in: the stale isGrounded comes from here)
                }
                Debug.Log("FFDEMO chute47 start OLD (ffu46 path): helicopter " + (heli != null ? heli.Title : "none") + " at " + p0.ToString("0"));
            }
            else
            {
                Jet j = Jet.I; d47V = j;
                if (j != null)
                {
                    f.SendTo(WorldId.Ranch, new Vector3(p0.x, Ranch.GY(p0.x, p0.z) + 0.1f, p0.z), 90f);   // stood on the ground first, like a real flight
                    f.EnterVehicle(j);
                    j.DemoHover(p0, 90f, 6f);
                    Jet.DemoDt = 0.2f;
                    if (d47Ai != null) { if (d47Ai.vehicle != null) d47Ai.ExitVehicle(); d47Ai.BoardAsPassenger(j); }
                }
                Debug.Log("FFDEMO chute47 start: James in " + (j != null ? j.Title : "NO JET") + " hovering at " + p0.ToString("0") + " agl " + (j != null ? j.agl.ToString("0") : "?") + ", passenger " + (d47Ai != null ? d47Ai.nick : "-"));
            }
        }
        else if (sc == "mechai47")
        {
            CritterPals.Paused = true;
            StoryMech.DtCap = 0.3f;
            MechAI.Paused = false;
            f.SendTo(WorldId.Ranch, new Vector3(-92f, 0f, 14f) + Vector3.up * Ranch.GY(-92f, 14f), 270f);
            if (slots.Count > 0 && slots[0].rig != null) slots[0].rig.SetYaw(270f);
            d47Ais.Clear();
            Vector3[] at = { new Vector3(-112f, 0f, -2f), new Vector3(-114f, 0f, 14f), new Vector3(-110f, 0f, 24f) };
            for (int i = 0; i < 3; i++)
            {
                Frog a = D47Ai(i); if (a == null) continue;
                if (a.vehicle != null) a.ExitVehicle();
                a.SendTo(WorldId.Ranch, at[i] + Vector3.up * Ranch.GY(at[i].x, at[i].z), 90f);
                d47Ais.Add(a);
            }
            foreach (var m in StoryMech.AllMechs) if (m != null && m.band <= 1) Debug.Log("FFDEMO mechai47 mech " + m.Title + " at " + m.transform.position.ToString("0") + " owner " + m.owner + " free " + (m.driver == null));
            Debug.Log("FFDEMO mechai47 start: James at " + f.transform.position.ToString("0") + ", AI " + d47Ais.Count);
        }
        else
        {
            MechAI.Paused = true; CritterPals.Paused = false;
            D47Critters(0);
        }
    }

    // critters47 phases: 0 Lizard terrace, 1 Backyard animal yard, 2 sprint, 3 pasture, 4 terrarium (house), 5 AI froggy, 6 done
    void D47Critters(int ph)
    {
        d47Ph = ph; d47PhT = 0f;
        Frog f = d47F;
        if (ph == 0) f.SendTo(WorldId.Ranch, Gp(-70.5f, -11.6f), 180f);
        else if (ph == 1) f.SendTo(WorldId.Ranch, Gp(-69f, -32.5f), 0f);
        else if (ph == 3) f.SendTo(WorldId.Ranch, Gp(-148f, 109f), 30f);   // beside (not on) the trough
        else if (ph == 4)
        {
            if (HouseWorld.I != null) HouseWorld.I.Enter(f);
            f.SendTo(WorldId.House, HouseWorld.L(19.4f, 0.05f, -3.6f), 180f);
        }
        else if (ph == 5)
        {
            f.SendTo(WorldId.Ranch, Gp(-60f, -6f), 270f);
            d47Ai = D47Ai(0);
            if (d47Ai != null) { if (d47Ai.vehicle != null) d47Ai.ExitVehicle(); d47Ai.SendTo(WorldId.Ranch, Gp(-70.5f, -11.2f), 90f); }
        }
        Debug.Log("FFDEMO critters47 phase " + ph + " t=" + d47T.ToString("0.0") + " at " + f.transform.position.ToString("0.0") + " world " + f.world + " | climbs " + CritterPals.Climbs + " rides " + CritterPals.Rides + " licks " + CritterPals.Licks + " hopoffs " + CritterPals.HopOffs);
    }
    static Vector3 Gp(float x, float z) { return new Vector3(x, Ranch.GY(x, z) + 0.05f, z); }

    PIn Demo47Input(PIn i)
    {
        var o = new PIn();
        Frog f = d47F;
        if (f == null) return i;
        if (d47Sc == "chute47")
        {
            if (f.GlideFlying)
            {
                float t = f.glideT;
                if (t > 1.6f && t < 3.2f) o.move.x = -1f;
                else if (t > 4.4f && t < 6.2f) o.move.x = 1f;
                else if (t > 6.6f && t < 7.8f) o.move.y = 1f;   // speed bar
                if (f.glideAgl < 3.3f) o.move = new Vector2(0f, -1f);   // FLARE
                o.brake = f.glideAgl < 3.3f ? 1f : 0f;
            }
            return o;
        }
        if (d47Sc == "mechai47")
        {
            if (d47Ph == 0 && f.transform.position.x > -112f) o.move = new Vector2(0f, 1f);
            return o;
        }
        if (d47Sc == "critters47" && d47Ph == 2 && d47PhT < 3f) { o.move = new Vector2(0f, 1f); o.downHeld = true; }   // sprint
        return o;
    }

    bool Demo47Cam(Frog f, string sc, out Vector3 pos, out Vector3 look)
    {
        pos = look = Vector3.zero;
        if (d47Ph < 0) return false;
        float dt = Mathf.Min(Time.deltaTime, 0.4f);
        d47T += dt; d47PhT += dt; d47Log -= dt;
        if (sc == "chute47") return Chute47(f, out pos, out look);
        if (sc == "mechai47") return MechAi47(f, out pos, out look);
        return Critters47(f, out pos, out look);
    }

    bool Chute47(Frog f, out Vector3 pos, out Vector3 look)
    {
        pos = look = Vector3.zero;
        Frog ai = d47Ai;
        if (d47Ph == 0 && Frog.ChuteOld && f.vehicle == null && d47PhT > 1.2f && d47V != null)
        {
            Flyer h = (Flyer)d47V;
            h.transform.position = d47P0; if (h.rb != null) { h.rb.position = d47P0; h.rb.velocity = Vector3.zero; }
            f.EnterVehicle(h);
            Debug.Log("FFDEMO chute47 OLD: James got in the helicopter after standing " + d47PhT.ToString("0.0") + " s, grounded " + f.cc.isGrounded);
        }
        if (d47Ph == 0 && d47PhT > (Frog.ChuteOld ? 3.5f : 3f) && f.vehicle != null)
        {
            d47Ph = 1; d47PhT = 0f;
            Debug.Log("FFDEMO chute47 EJECT t=" + d47T.ToString("0.0") + " canExit " + f.vehicle.CanExit(f) + " agl " + (f.vehicle is Jet ? ((Jet)f.vehicle).agl.ToString("0.0") : (f.vehicle is Flyer ? ((Flyer)f.vehicle).AltitudeAboveGround.ToString("0.0") : "?")));
            f.ExitVehicle();
        }
        if (d47Ph == 1 && !f.GlideOn && (!Frog.ChuteOld || d47PhT > 12f) && d47PhT > 1f)
        {
            d47Ph = 2; d47PhT = 0f;
            Debug.Log("FFDEMO chute47 player down t=" + d47T.ToString("0.0") + " landVy " + f.glideLandVy.ToString("0.00") + " fwd " + f.glideLandFwd.ToString("0.0") + " soft " + f.glideSoft + " flares " + f.glideFlares);
        }
        if (d47Ph == 2 && ((ai != null && !ai.GlideOn && d47PhT > 2f) || d47PhT > 30f || ai == null || Frog.ChuteOld))
        {
            d47Ph = 3;
            Jet j = Jet.I;
            Debug.Log("FFDEMO chute47 summary: James lands " + f.glideLands + " soft " + f.glideSoft + " vy " + f.glideLandVy.ToString("0.00") + " fwd " + f.glideLandFwd.ToString("0.0") + " flares " + f.glideFlares + " | " + (ai != null ? ai.nick + " lands " + ai.glideLands + " soft " + ai.glideSoft + " vy " + ai.glideLandVy.ToString("0.00") + " fwd " + ai.glideLandFwd.ToString("0.0") + " flares " + ai.glideFlares : "no AI") + " | jet homing " + (j != null && j.homing) + " jet at " + (j != null ? j.transform.position.ToString("0") : "-") + " chute flag now " + f.chute);
            Debug.Log("FFDEMO chute47 done");
        }
        if (d47Log <= 0f)
        {
            d47Log = 0.5f;
            Debug.Log("FFDEMO chute47 t=" + d47T.ToString("0.0") + " ph=" + d47Ph + " James gph " + f.glidePh + " agl " + f.glideAgl.ToString("0.0") + " fwd " + f.glideFwd.ToString("0.0") + " vy " + f.HVy().ToString("0.00") + " brake " + f.glideBrake.ToString("0.00") + " turn " + f.glideTurn.ToString("0.00") + " yaw " + f.glideYaw.ToString("0") + " camK " + f.GlideCamK.ToString("0.00") + " chute " + f.chute + " pos " + f.transform.position.ToString("0.0") + " hint '" + HintPrompt(f) + "'" +
                      (ai != null ? " | " + ai.nick + " gph " + ai.glidePh + " agl " + ai.glideAgl.ToString("0.0") + " fwd " + ai.glideFwd.ToString("0.0") + " brake " + ai.glideBrake.ToString("0.00") + " pos " + ai.transform.position.ToString("0.0") : "") +
                      (Jet.I != null ? " | jet " + Jet.I.mode + " homing " + Jet.I.homing + " at " + Jet.I.transform.position.ToString("0") : ""));
        }
        // camera: the aircraft from the side before the bail; then the real in-game glide camera; then the AI's canopy
        if (d47Ph == 0 && d47V != null)
        {
            Vector3 vp = d47V.transform.position;
            pos = vp + new Vector3(-6f, 3f, -16f); look = vp + Vector3.up * 1f; return true;
        }
        if (d47Ph == 1 && f.glidePh == 1) { pos = f.transform.position + new Vector3(-5f, 2f, -12f); look = f.transform.position + Vector3.up * 1f; return true; }
        if (d47Ph == 1 || (d47Ph >= 2 && ai == null)) return false;   // the game's own glide camera
        if (ai != null)
        {
            Vector3 ap = ai.transform.position;
            Vector3 fw = Quaternion.Euler(0f, ai.GlideOn ? ai.glideYaw : ai.transform.eulerAngles.y, 0f) * Vector3.forward;
            Vector3 rt = Vector3.Cross(Vector3.up, fw);
            pos = ap - fw * 11f + rt * 5f + Vector3.up * 3.5f; look = ap + Vector3.up * (ai.GlideOn ? 2.8f : 0.8f);
            float gy = Ranch.GY(pos.x, pos.z) + 1f; if (pos.y < gy) pos.y = gy;
            return true;
        }
        return false;
    }

    bool MechAi47(Frog f, out Vector3 pos, out Vector3 look)
    {
        pos = look = Vector3.zero;
        var mai = MechAI.I;
        if (d47Ph == 0 && d47PhT > 4f && mai != null)
        {
            // the natural scan may already have sent someone; make sure two go for the probe (logged)
            int sent = 0;
            foreach (var a in d47Ais) { bool has = false; foreach (var j in mai.jobs) if (j.f == a) has = true; if (has) sent++; }
            for (int i = 0; i < d47Ais.Count && sent < 2; i++)
            {
                bool has = false; foreach (var j in mai.jobs) if (j.f == d47Ais[i]) has = true;
                if (!has && mai.Send(d47Ais[i], "demo nudge (player walking to the yard)") != null) sent++;
            }
            d47Ph = 1; d47PhT = 0f;
            string why; bool near = MechAI.PlayerInterested(out why);
            Debug.Log("FFDEMO mechai47 phase 1 t=" + d47T.ToString("0.0") + " interested " + near + " (" + why + ") jobs " + mai.jobs.Count);
        }
        if (d47Ph == 1 && mai != null)
        {
            foreach (var j in mai.jobs) if (j.phase >= 1 && j.dur > 36f) { j.dur = j.f == d47Ais[0] ? 24f : 60f; if (j.fireT > 3f) j.fireT = 3f; }
            if (MechAI.Boardings > 0 && d47Ph0Boards == 0) { d47Ph0Boards = MechAI.Boardings; Debug.Log("FFDEMO mechai47 first boarding t=" + d47T.ToString("0.0")); }
            if (d47PhT > 120f || (MechAI.Exits > 0 && d47PhT > 70f)) { d47Ph = 2; Debug.Log("FFDEMO mechai47 summary: boardings " + MechAI.Boardings + " shots " + MechAI.Shots + " exits " + MechAI.Exits + " min mech-James " + d47MinD.ToString("0.0") + " m min mech-house-edge " + d47MinHouse.ToString("0.0") + " m jobs " + mai.jobs.Count); Debug.Log("FFDEMO mechai47 done"); }
        }
        // stats
        Vector3 c = Vector3.zero; int n = 0;
        if (mai != null)
            foreach (var j in mai.jobs)
            {
                Vector3 q = j.phase >= 1 && j.m != null ? j.m.transform.position : j.f.transform.position;
                c += q; n++;
                if (j.phase >= 1 && j.m != null)
                {
                    Vector3 d = j.m.transform.position - f.transform.position; d.y = 0f; d47MinD = Mathf.Min(d47MinD, d.magnitude);
                    Vector2 hc = Layout.HouseC, hs = Layout.HouseSize * 0.5f;
                    float dx = Mathf.Max(Mathf.Abs(q.x - hc.x) - hs.x, 0f), dz = Mathf.Max(Mathf.Abs(q.z - hc.y) - hs.y, 0f);
                    d47MinHouse = Mathf.Min(d47MinHouse, Mathf.Sqrt(dx * dx + dz * dz));
                }
            }
        if (d47Log <= 0f && mai != null)
        {
            d47Log = 0.5f;
            var sb = new System.Text.StringBuilder("FFDEMO mechai47 t=" + d47T.ToString("0.0") + " ph=" + d47Ph + " James " + f.transform.position.ToString("0") + " boardings " + MechAI.Boardings + " shots " + MechAI.Shots + " exits " + MechAI.Exits);
            foreach (var j in mai.jobs) sb.Append(" | ").Append(j.f.nick).Append(" ").Append(j.phase == 0 ? "walking" : j.phase == 1 ? "piloting" : "home").Append(" ").Append(j.m.Title).Append(" @").Append((j.phase >= 1 ? j.m.transform.position : j.f.transform.position).ToString("0")).Append(" mode ").Append(j.mode).Append(" t ").Append(j.t.ToString("0")).Append(" shots ").Append(j.shots).Append(" aimK ").Append(j.m.aimK.ToString("0.0"));
            Debug.Log(sb.ToString());
        }
        // camera: behind James looking west at the mech rows; then a wide view of the AI mechs
        Vector3 fp = f.transform.position;
        bool piloting = false; if (mai != null) foreach (var j in mai.jobs) if (j.phase >= 1) piloting = true;
        if (!piloting)
        {
            Vector3 tgt = n > 0 ? c / n : new Vector3(-145f, 0f, 6f);
            pos = fp + new Vector3(9f, 4.5f, 6f); look = new Vector3(tgt.x, Ranch.GY(tgt.x, tgt.z) + 2f, tgt.z);
            return true;
        }
        Vector3 mc = n > 0 ? c / n : new Vector3(-145f, 0f, 6f);
        Vector3 dir = mc - fp; dir.y = 0f; dir = dir.sqrMagnitude > 1f ? dir.normalized : Vector3.left;
        pos = fp - dir * 14f + Vector3.up * 9f + Vector3.Cross(Vector3.up, dir) * 8f; look = new Vector3(mc.x, Ranch.GY(mc.x, mc.z) + 9f, mc.z);
        return true;
    }

    bool Critters47(Frog f, out Vector3 pos, out Vector3 look)
    {
        pos = look = Vector3.zero;
        float[] dur = { 20f, 18f, 5f, 20f, 22f, 22f };
        if (d47Ph >= 0 && d47Ph <= 5 && d47PhT > dur[d47Ph])
        {
            Debug.Log("FFDEMO critters47 end of phase " + d47Ph + " riders on James " + CritterPals.Riders(f) + " (" + CritterPals.RiderLabel(f) + ")" + (d47Ai != null ? " riders on " + d47Ai.nick + " " + CritterPals.Riders(d47Ai) + " (" + CritterPals.RiderLabel(d47Ai) + ")" : "") + " | climbs " + CritterPals.Climbs + " rides " + CritterPals.Rides + " nuzzles " + CritterPals.Nuzzles + " licks " + CritterPals.Licks + " hopoffs " + CritterPals.HopOffs);
            if (d47Ph < 5) D47Critters(d47Ph + 1);
            else
            {
                d47Ph = 6;
                var sb = new System.Text.StringBuilder();
                foreach (var kv in CritterPals.OffWhy) sb.Append(kv.Key).Append(" ").Append(kv.Value).Append(", ");
                Debug.Log("FFDEMO critters47 summary: climbs " + CritterPals.Climbs + " rides " + CritterPals.Rides + " nuzzles " + CritterPals.Nuzzles + " licks " + CritterPals.Licks + " hopoffs " + CritterPals.HopOffs + " (why: " + sb + ")");
                Debug.Log("FFDEMO critters47 done");
            }
        }
        Frog who = d47Ph == 5 && d47Ai != null ? d47Ai : f;
        if (d47Log <= 0f && CritterPals.I != null)
        {
            d47Log = 0.5f;
            var sb = new System.Text.StringBuilder("FFDEMO critters47 t=" + d47T.ToString("0.0") + " ph=" + d47Ph + " " + who.nick + " riders " + CritterPals.Riders(who) + " '" + HintPrompt(who) + "'");
            foreach (var p in CritterPals.I.pals) sb.Append(" | ").Append(p.a.kind).Append(">").Append(p.f.nick).Append(" ").Append(p.st).Append(" slot ").Append(p.slot).Append(" y+").Append((p.a.transform.position.y - p.f.transform.position.y).ToString("0.00"));
            Debug.Log(sb.ToString());
        }
        Vector3 fp = who.transform.position;
        if (d47Ph == 3)
        {
            // pasture: a little wider so the cow / horse heads and the froggy share the frame
            Vector3 c = fp; int n = 1;
            foreach (var p in CritterPals.I.pals) if (p.f == who && p.st == CritterPals.St.Nuzzle) { c += p.a.transform.position; n++; }
            c /= n;
            pos = c + new Vector3(4.5f, 2.6f, -5.5f); look = c + Vector3.up * 0.9f; return true;
        }
        Vector3 fw = who.transform.forward, rt = who.transform.right;
        pos = fp + fw * 2.3f + rt * 1.5f + Vector3.up * 1.45f; look = fp + Vector3.up * 0.7f;
        if (d47Ph == 4) { pos = fp + fw * 2.0f - rt * 1.6f + Vector3.up * 1.5f; look = fp + Vector3.up * 0.8f + rt * 0.3f; }
        return true;
    }
}
