using UnityEngine;
using System.Collections.Generic;

// ffu63 PLAY CATCH, BOTH WAYS (Bill Oct 19 16:27 MDT): the robot throws the froggy plushie (PLAY > Play catch, or say "play catch");
// the froggy CATCHES it (it lands on / next to you = caught; or walk up to it / E / A), an arc preview shows the throw
// (aimed at the robot's hands; hold LT / right mouse to aim it yourself, like the bow), RT / click / F (touch: FIRE) THROWS it back.
// The robot runs under it and catches it (or fetches it when it lands) and throws again. Up to 12 throws, then "keep the plushie".
public static class Catch63
{
    class Held { public Robot r; public Transform ball; public Transform[] dots; public float t; }
    static readonly Dictionary<Frog, Held> held = new Dictionary<Frog, Held>();
    public static bool DemoFire;           // probe: throw now
    public static int Thrown, Caught, RobotCaught;      // probe counters (froggy throws, froggy catches)

    public static bool Holding(Frog f) { return f != null && held.ContainsKey(f); }

    public static void Give(Frog f, Robot r, Transform ball)
    {
        if (f == null || ball == null || Holding(f)) return;
        foreach (var c in ball.GetComponentsInChildren<Collider>()) c.enabled = false;
        ball.SetParent(null, true);
        var h = new Held { r = r, ball = ball };
        h.dots = new Transform[26];
        Material dm = Mats.Unlit(Color.white);
        for (int i = 0; i < h.dots.Length; i++)
        {
            var d = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(d.GetComponent<Collider>());
            d.GetComponent<Renderer>().sharedMaterial = dm; d.name = "Arc63"; d.transform.localScale = Vector3.one * 0.08f;
            Mats.NoShadows(d); d.SetActive(false);
            h.dots[i] = d.transform;
        }
        held[f] = h; Caught++;
        Sfx.PlayAt(Sfx.Pickup, f.transform.position, 0.5f, 30f, 1.3f);
        string k = f.inputKind == InputKind.Gamepad ? "RT throw (hold LT to aim)" : f.inputKind == InputKind.Touch ? "FIRE throw" : "click / F throw (hold right mouse to aim)";
        f.Toast("Caught it! Throw it back to " + r.robotName + ": " + k, 3.5f);
        Debug.Log("CATCH63 froggy caught " + f.nick + " from " + r.robotName + " (" + Caught + ")");
    }

    static void Drop(Frog f, Held h, bool toGround)
    {
        held.Remove(f);
        if (h.dots != null) foreach (var d in h.dots) if (d != null) Object.Destroy(d.gameObject);
        if (toGround && h.ball != null) { Vector3 p = f.transform.position + f.transform.forward * 0.6f; p.y = f.transform.position.y + 0.12f; h.ball.position = p; }
    }

    static Vector3 Hand(Frog f) { return f.transform.position + Vector3.up * 0.8f + f.transform.forward * 0.38f; }

    // a lob from -> target: flight time from the flat distance, gravity compensated
    public static Vector3 Lob(Vector3 from, Vector3 target)
    {
        Vector3 d = target - from; float hd = new Vector2(d.x, d.z).magnitude;
        float T = Mathf.Clamp(hd / 8f, 0.55f, 1.6f);
        return d / T + Vector3.up * (0.5f * 9.81f * T);
    }

    // per froggy, from Frog.Tick (after the bow)
    public static void Tick(Frog f, PIn i, float dt)
    {
        Held h;
        if (!held.TryGetValue(f, out h))
        {
            // pick up the plushie lying next to you (walk onto it, or E / A within reach)
            if (f.vehicle != null || RanchLife.I == null || Bow61.Has(f)) return;
            foreach (var r in RanchLife.I.robots)
            {
                Transform b = r != null ? r.CatchBallFor63(f) : null;
                if (b == null) continue;
                Vector3 d = b.position - f.transform.position; d.y = 0f;
                if (d.magnitude < 1.1f || (i.use && d.magnitude < 2f)) { Give(f, r, b); r.Catch63Picked(); }
                break;
            }
            return;
        }
        if (h.ball == null || h.r == null || h.r.visit == null || h.r.visit.verb != "catch" || f.vehicle != null || f.world != h.r.world) { Drop(f, h, h.ball != null); return; }
        h.t += dt;
        Vector3 from = Hand(f);
        h.ball.position = from; h.ball.rotation = f.transform.rotation;
        // aim: the robot's hands, or (LT / right mouse held) where the camera's centre looks
        Vector3 tgt = h.r.CatchHands63();
        bool aimed = i.aimHeld && f.human;
        Camera cam = Game.I != null ? Game.I.CamFor(f) : null;
        if (aimed && cam != null)
        {
            Vector3 o = cam.transform.position, cd = cam.transform.forward;
            tgt = o + cd * 25f;
            RaycastHit hit;
            if (Physics.Raycast(o + cd * 2.5f, cd, out hit, 60f, ~((1 << 2) | (1 << 9) | (1 << 10)), QueryTriggerInteraction.Ignore)) tgt = hit.point;
            if (Vector3.Distance(tgt, h.r.CatchHands63()) < 2.2f) tgt = h.r.CatchHands63();   // snaps onto the robot
        }
        Vector3 v = Lob(from, tgt);
        Arc(h, from, v, cam, Vector3.Distance(tgt, h.r.CatchHands63()) < 0.1f);
        if (!aimed) { Vector3 to = tgt - f.transform.position; to.y = 0f; }
        bool fire = (i.bowFire && f.human) || (DemoFire && h.t > 1.5f) || (!f.human && h.t > 2.5f);
        if (fire && h.t > 0.35f)
        {
            DemoFire = false; Thrown++;
            Drop(f, h, false);
            h.r.Catch63Thrown(f, h.ball, from, v);
            Sfx.PlayAt(Sfx.Pick(Sfx.Servo), from, 0.3f, 25f, 1.6f);
            Debug.Log("CATCH63 froggy throw " + f.nick + " -> " + h.r.robotName + " v " + v.ToString("0.0") + (aimed ? " aimed" : " at the hands") + " (" + Thrown + ")");
        }
    }

    static void Arc(Held h, Vector3 from, Vector3 v, Camera cam, bool onRobot)
    {
        Color c = onRobot ? new Color(1f, 0.85f, 0.25f) : Color.white;
        Vector3 p = from, vv = v; bool stop = false; const float tStep = 0.06f;
        for (int k = 0; k < h.dots.Length; k++)
        {
            Transform d = h.dots[k]; if (d == null) continue;
            if (!stop) { for (int s = 0; s < 3; s++) { vv.y -= 9.81f * tStep / 3f; p += vv * tStep / 3f; } if (p.y < from.y - 2.5f) stop = true; }
            bool show = !stop && k > 0;
            if (d.gameObject.activeSelf != show) d.gameObject.SetActive(show);
            if (show)
            {
                d.position = p; float cd = cam != null ? Vector3.Distance(cam.transform.position, p) : 8f; d.localScale = Vector3.one * Mathf.Clamp(cd * 0.01f, 0.07f, 0.35f);
                var r = d.GetComponent<Renderer>(); if (r.sharedMaterial.color != c) r.sharedMaterial = Mats.Unlit(c);
            }
        }
    }
}

public partial class Robot
{
    // the catch plushie lying on the ground waiting for this froggy (state 4), else null
    public Transform CatchBallFor63(Frog f)
    {
        var v = visit;
        if (v == null || v.verb != "catch" || v.lead != f || v.vs != 4 || v.plush == null || world == WorldId.RealRoom) return null;
        return v.plush.transform;
    }
    public void Catch63Picked() { if (visit != null) { visit.vs = 5; visit.vt = 0f; } }
    public Vector3 CatchHands63() { return transform.position + Vector3.up * height * 0.62f + transform.forward * (0.35f + bulk * 0.2f); }

    public void Catch63Thrown(Frog f, Transform ball, Vector3 from, Vector3 vel)
    {
        var v = visit; if (v == null || v.verb != "catch") return;
        if (v.plush == null) v.plush = ball.gameObject;
        v.vs = 6; v.vt = 0f; v.vp = from; v.vp2 = vel;
    }

    // states 4..6 of CatchTick (ffu63): 4 on the ground by the froggy, 5 the froggy holds it, 6 flying to the robot
    bool CatchTurn63(float dt, Vector3 lp, float d, Transform pl)
    {
        var v = visit; Frog f = v.lead;
        if (world == WorldId.RealRoom || v.plush == null) return false;
        switch (v.vs)
        {
            case 4:
                if (v.count >= 12) { f.Toast(robotName + ": that was SO fun! Keep the plushie!", 2.5f); v.plush = null; EndVerb(); return true; }
                if (Catch63.Holding(f)) { v.vs = 5; v.vt = 0f; return true; }
                Face(lp); pose = 16;
                if (v.vt > 14f) { Say("I'll get it!"); v.vs = 0; v.vt = 0f; }
                return true;
            case 5:
                if (!Catch63.Holding(f)) { v.vs = 4; v.vt = 0f; return true; }
                if (d > 10f) WalkTo(lp, RunSpeed, 6f, dt); else if (d < 3.5f) { Vector3 away = transform.position + (transform.position - lp).normalized * 3f; WalkTo(away, RunSpeed * 0.6f, 0.3f, dt); }
                Face(lp); pose = 16;   // hands up, ready
                if (v.vt > 40f) { v.line = "waiting for your throw"; }
                return true;
            case 6:
                {
                    // fly the plushie, run under its landing spot, catch it near the hands
                    Vector3 p = v.vp, vel = v.vp2;
                    int sub = Mathf.Clamp(Mathf.CeilToInt(dt / 0.03f), 1, 12); float sdt = dt / sub; bool gotIt = false;
                    for (int q = 0; q < sub && !gotIt; q++) { vel.y -= 9.81f * sdt; p += vel * sdt; gotIt = Vector3.Distance(p, CatchHands63()) < 0.85f + bulk * 0.45f; }
                    v.vp = p; v.vp2 = vel;
                    pl.position = p; pl.Rotate(380f * dt, 260f * dt, 0f, Space.Self);
                    Vector3 land = p, lv = vel;
                    float hy = transform.position.y + height * 0.62f;
                    for (int k = 0; k < 60 && (lv.y > 0f || land.y > hy); k++) { lv.y -= 9.81f * 0.05f; land += lv * 0.05f; }
                    Vector3 stand = land - transform.forward * (0.35f + bulk * 0.2f);
                    float dl = RobotNav.Flat(stand - transform.position);
                    if (dl > 0.25f && dl < 14f) WalkTo(stand, RunSpeed * 1.25f, 0.2f, dt);
                    Face(new Vector3(p.x, 0f, p.z)); pose = 16;
                    float reach = 0.85f + bulk * 0.45f;
                    if (gotIt || Vector3.Distance(p, CatchHands63()) < reach)
                    {
                        pl.SetParent(carryNode, true); pl.localPosition = Vector3.zero;
                        v.count++; v.vs = 1; v.vt = 0f; Catch63.RobotCaught++;
                        Sfx.PlayAt(Sfx.Pick(Sfx.Servo), p, 0.45f, 25f, 1.2f);
                        FX.Sparkle(p, new Color(1f, 0.9f, 0.4f), 6);
                        f.Toast(robotName + ": got it! Nice throw, " + f.nick + "! Here it comes...", 2.2f);
                        Debug.Log("CATCH63 robot caught " + robotName + " from " + f.nick + " (" + v.count + ")");
                        return true;
                    }
                    float gy = Ground(p);
                    if (p.y <= gy + 0.12f || v.vt > 6f)
                    {
                        p.y = gy + 0.12f; pl.position = p; pl.rotation = Quaternion.identity;
                        FX.Sparkle(p, new Color(0.6f, 1f, 0.6f), 4);
                        f.Toast(robotName + ": whoops - missed! I'll grab it.", 2f);
                        Debug.Log("CATCH63 robot missed " + robotName + " landed " + RobotNav.Flat(p - transform.position).ToString("0.0") + " m away");
                        v.vs = 0; v.vt = 0f;
                    }
                    return true;
                }
        }
        return false;
    }
}
