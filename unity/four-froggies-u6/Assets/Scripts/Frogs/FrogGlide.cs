using System.Collections.Generic;
using UnityEngine;

// ffu47 PARAGLIDER (Bill: "when you jump out of any aircraft in the air you should get a parachute that deploys and carries
// you down ... steerable like a paraglider"). Bailing out of a helicopter / drone / James's VTOL jet / a mech flying high
// (and the jet's passengers) = a short freefall with the wing pack on the back, then a ram-air wing pops out (auto after
// 0.6 s, or A / Space / tap A earlier): 11 cells in an arc, A + C lines to the risers, brake lines to the hands. Flight
// model: trim 9.2 m/s forward, 1.75 m/s sink; stick / A D / touch stick turns (the wing banks, the froggy swings out);
// stick forward / W = speed bar (faster, more sink; with a hard turn = spiral dive to lose height); LT / S / stick back =
// brakes - pulling them fast near the ground trades speed for lift (the FLARE: sink goes to ~0 for a soft landing).
// Touchdown = the wing drops behind, gets packed up into the backpack and the froggy walks on; landing in the pond =
// cut away. AI froggies (jet passengers, demo) steer to a spot near the player, S-turn to lose height and flare.
// The canopy shows over the network through the existing 'chute' state flag (8). &chuteold=1 = the ffu46 behaviour
// (round sphere chute + its first-frame cut) for before / after shots.
// Root cause of "no chute": ExitVehicle armed the old chute, but the next Walk() read cc.isGrounded BEFORE moving - that
// flag was still the one from the froggy's last walk before getting in (standing on the ground), so the very first frame
// cut the chute again (SetChute(false)) and the froggy just fell. The jet never allowed getting out in the air at all.
public partial class Frog
{
    GameObject chuteGo;           // the wing rig (pivot at the harness)
    Transform wingRoot, packT;
    readonly List<Transform> flapL = new List<Transform>(), flapR = new List<Transform>();
    Transform brakeLineL, brakeLineR; Vector3 brakeTipL, brakeTipR;
    public int glidePh;           // 0 off, 1 freefall, 2 opening, 3 flying, 4 landed (packing up)
    public float glideT, glidePhT, glideYaw, glideFwd, glideBrake, glideTurn, glideBar, glideAgl, GlideCamK;
    public Vector3? glideGoal;    // AI: where to land
    public float HVy() { return vel.y; }
    public bool GlideOn { get { return glidePh > 0; } }
    public bool GlideFlying { get { return glidePh == 2 || glidePh == 3; } }
    public static float GlideDtCap = 0.05f;
    public static bool ChuteOld;  // &chuteold=1
    public int glideFlares, glideLands; public float glideLandVy, glideLandFwd, glideMinVy = -99f; public bool glideSoft; public string glideFrom = "";
    float glideFlareT; bool glideFlareSaid;
    const float WingH = 4.5f;     // canopy height over the harness

    // called by ExitVehicle (and the jet for its passengers): out at spot with velocity v0, freefall, then the wing
    public void BeginBail(string from, Vector3 spot, Vector3 v0, bool popUp)
    {
        if (cc != null && !cc.enabled && vehicle == null && passengerOf == null) cc.enabled = true;
        bool stale = cc != null && cc.isGrounded;
        Teleport(spot);
        Vector3 h = new Vector3(v0.x, 0f, v0.z);
        if (h.magnitude > 14f) h = h.normalized * 14f;
        planar = h;
        vel = new Vector3(0f, popUp ? Mathf.Max(v0.y, 6f) : Mathf.Min(v0.y, 0f), 0f);
        glideFrom = from;
        if (ChuteOld)
        {
            SetChute(true);
            Toast("Parachute! The " + from + " flies itself home.", 3f);
            Debug.Log("FFDEMO chute OLD bail " + nick + " from " + from + " cc.isGrounded(stale)=" + stale);
            return;
        }
        glidePh = 1; glidePhT = 0f; glideT = 0f; glideYaw = h.sqrMagnitude > 1f ? Mathf.Atan2(h.x, h.z) * Mathf.Rad2Deg : yaw;
        glideFwd = Mathf.Min(h.magnitude, 9f); glideBrake = 0f; glideTurn = 0f; glideBar = 0f; glideMinVy = -99f; glideFlareT = 0f; glideFlareSaid = false;
        BuildWing();
        chuteGo.SetActive(false);
        packT.gameObject.SetActive(true); packT.localScale = new Vector3(0.48f, 0.5f, 0.24f);
        if (human) Toast(popUp ? "EJECT!  The wing opens in a moment" : "Bail out!  The wing opens in a moment", 2.2f);
        Debug.Log("FFDEMO chute bail " + nick + " from " + from + " at " + spot.ToString("0.0") + " v0 " + v0.ToString("0.0") + " cc.isGrounded(stale)=" + stale + " human " + human);
    }

    // canopy on / off (net puppets + the old path); the new flight sets it through glidePh
    public void SetChute(bool on)
    {
        chute = on;
        if (ChuteOld) { OldChute(on); return; }
        if (on) { BuildWing(); chuteGo.SetActive(true); wingRoot.localScale = Vector3.one; wingRoot.localRotation = Quaternion.identity; chuteGo.transform.localRotation = Quaternion.identity; }
        else
        {
            if (chuteGo != null) chuteGo.SetActive(false);
            if (packT != null) packT.gameObject.SetActive(false);
            if (glidePh > 0) { glidePh = 0; if (model != null) model.transform.localRotation = Quaternion.identity; }
            GlideCamK = 0f;
        }
    }

    // stop gliding at once (entering a vehicle, world change, water)
    public void GlideStop() { SetChute(false); glidePh = 0; GlideCamK = 0f; }

    // ---------------- the ffu46 chute (before shots only) ----------------
    GameObject oldChuteGo;
    void OldChute(bool on)
    {
        if (on && oldChuteGo == null)
        {
            oldChuteGo = new GameObject("Chute");
            oldChuteGo.transform.SetParent(transform, false);
            oldChuteGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            Color c = Color.Lerp(color, Color.white, 0.35f);
            Mats.Prim(PrimitiveType.Sphere, oldChuteGo.transform, new Vector3(0f, 2.4f, 0f), new Vector3(3.2f, 1.2f, 3.2f), Mats.Lit(c));
            Mats.Prim(PrimitiveType.Sphere, oldChuteGo.transform, new Vector3(0f, 2.15f, 0f), new Vector3(3.0f, 0.9f, 3.0f), Mats.Lit(Color.white));
            Mats.SetLayer(oldChuteGo, 9);
        }
        if (oldChuteGo != null) oldChuteGo.SetActive(on);
    }

    // ---------------- the wing model ----------------
    void BuildWing()
    {
        if (chuteGo != null) return;
        chuteGo = new GameObject("Paraglider");
        chuteGo.transform.SetParent(transform, false);
        chuteGo.transform.localPosition = new Vector3(0f, 0.95f, 0f);
        wingRoot = Mats.Node(chuteGo.transform, "Wing", Vector3.zero);
        Color main = Color.Lerp(color, Color.white, 0.12f), alt = new Color(0.97f, 0.97f, 0.95f), accent = new Color(1f, 0.78f, 0.12f);
        Material mMain = Mats.Lit(main), mAlt = Mats.Lit(alt), mAcc = Mats.Lit(accent), mDark = Mats.Lit(new Color(0.08f, 0.09f, 0.1f));
        Material mLine = Mats.Unlit(new Color(0.93f, 0.93f, 0.9f)), mBrake = Mats.Unlit(new Color(1f, 0.45f, 0.2f));
        const int N = 11; const float R = 5.4f, Arc = 80f;
        Vector3 arcC = new Vector3(0f, WingH - R, 0.35f);
        Vector3 rL = new Vector3(-0.24f, 0.28f, 0f), rR = new Vector3(0.24f, 0.28f, 0f);   // riser tops (harness carabiners)
        System.Action<Vector3, Vector3, float, Material> Line = (a, b, w, m) =>
        {
            Vector3 d = b - a;
            var g = Mats.Prim(PrimitiveType.Cube, wingRoot, (a + b) * 0.5f, new Vector3(w, d.magnitude, w), m);
            g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
        };
        for (int i = 0; i < N; i++)
        {
            float u = i / (float)(N - 1), a = Mathf.Lerp(-Arc * 0.5f, Arc * 0.5f, u), ar = a * Mathf.Deg2Rad;
            float mid = 1f - Mathf.Abs(u - 0.5f) * 2f;
            float chord = Mathf.Lerp(1.55f, 2.35f, mid), thick = Mathf.Lerp(0.2f, 0.42f, mid), w = 2f * Mathf.PI * R * (Arc / 360f) / N + 0.03f;
            Vector3 c = arcC + new Vector3(Mathf.Sin(ar) * R, Mathf.Cos(ar) * R, 0f);
            Quaternion q = Quaternion.Euler(0f, 0f, -a);
            Material m = (i % 2 == 0) ? mMain : mAlt;
            if (i == N / 2) m = mAcc;
            var cell = Mats.Prim(PrimitiveType.Cube, wingRoot, c, new Vector3(w, thick, chord), m);
            cell.transform.localRotation = q;
            // rounded nose with the dark cell openings, the top skin bulge
            var nose = Mats.Prim(PrimitiveType.Cube, wingRoot, c + q * new Vector3(0f, -thick * 0.08f, chord * 0.5f + 0.02f), new Vector3(w * 0.86f, thick * 0.62f, 0.06f), mDark);
            nose.transform.localRotation = q;
            var bulge = Mats.Prim(PrimitiveType.Capsule, wingRoot, c + q * new Vector3(0f, thick * 0.38f, 0.12f), new Vector3(w * 0.95f, chord * 0.42f, thick * 0.5f), m);
            bulge.transform.localRotation = q * Quaternion.Euler(90f, 0f, 0f);
            // trailing-edge flap (brakes): hinged at the trailing edge
            Transform hinge = Mats.Node(wingRoot, "Flap", c + q * new Vector3(0f, 0f, -chord * 0.5f));
            hinge.localRotation = q;
            Mats.Prim(PrimitiveType.Cube, hinge, new Vector3(0f, 0f, -0.22f), new Vector3(w, thick * 0.35f, 0.44f), m);
            (a < 0f ? flapL : flapR).Add(hinge);
            // A lines (front) + C lines (back), each to the riser on its side
            Vector3 under = c + q * new Vector3(0f, -thick * 0.5f, 0f);
            Vector3 rs = a < 0f ? rL : a > 0f ? rR : (i % 2 == 0 ? rL : rR);
            Line(rs, under + q * new Vector3(0f, 0f, chord * 0.32f), 0.03f, mLine);
            Line(rs, under + q * new Vector3(0f, 0f, -chord * 0.22f), 0.03f, mLine);
            if (i == 0) brakeTipL = c + q * new Vector3(0f, 0f, -chord * 0.5f - 0.3f);
            if (i == N - 1) brakeTipR = c + q * new Vector3(0f, 0f, -chord * 0.5f - 0.3f);
        }
        // risers + brake lines to the froggy's hands (toggles)
        Line(new Vector3(-0.2f, -0.2f, 0f), rL, 0.06f, mDark);
        Line(new Vector3(0.2f, -0.2f, 0f), rR, 0.06f, mDark);
        var bl = Mats.Prim(PrimitiveType.Cube, wingRoot, Vector3.zero, Vector3.one * 0.03f, mBrake); brakeLineL = bl.transform;
        var br = Mats.Prim(PrimitiveType.Cube, wingRoot, Vector3.zero, Vector3.one * 0.03f, mBrake); brakeLineR = br.transform;
        // harness seat under the froggy
        Mats.Prim(PrimitiveType.Cube, chuteGo.transform, new Vector3(0f, -0.62f, -0.05f), new Vector3(0.66f, 0.08f, 0.56f), mDark);
        Mats.Prim(PrimitiveType.Cube, chuteGo.transform, new Vector3(0f, -0.3f, -0.4f), new Vector3(0.62f, 0.62f, 0.1f), Mats.Lit(new Color(0.18f, 0.2f, 0.24f)));
        Mats.SetLayer(chuteGo, 9);
        // the wing pack on the back (freefall + packing up)
        var pk = new GameObject("WingPack"); packT = pk.transform; packT.SetParent(transform, false);
        packT.localPosition = new Vector3(0f, 0.72f, -0.48f);
        Mats.Prim(PrimitiveType.Cube, packT, Vector3.zero, Vector3.one, Mats.Lit(Color.Lerp(color, Color.black, 0.35f)));
        Mats.Prim(PrimitiveType.Cube, packT, new Vector3(0f, 0.2f, -0.52f), new Vector3(0.9f, 0.12f, 0.08f), mAcc);
        Mats.SetLayer(pk, 9);
        pk.SetActive(false);
    }

    void PoseLine(Transform t, Vector3 a, Vector3 b)
    {
        Vector3 d = b - a;
        t.localPosition = (a + b) * 0.5f;
        t.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
        t.localScale = new Vector3(0.03f, d.magnitude, 0.03f);
    }

    void WingPose(float open, float dt)
    {
        if (chuteGo == null) return;
        float bl = Mathf.Clamp01(glideBrake + Mathf.Max(0f, -glideTurn) * 0.8f), br = Mathf.Clamp01(glideBrake + Mathf.Max(0f, glideTurn) * 0.8f);
        foreach (var f in flapL) f.localRotation = FlapBase(f) * Quaternion.Euler(-bl * 38f, 0f, 0f);
        foreach (var f in flapR) f.localRotation = FlapBase(f) * Quaternion.Euler(-br * 38f, 0f, 0f);
        // the wing opens (cells fill from the middle) and rises out of the pack
        float s = open < 1f ? Mathf.SmoothStep(0.12f, 1f, open) * (1f + Mathf.Sin(open * Mathf.PI) * 0.08f) : 1f;
        wingRoot.localScale = new Vector3(Mathf.Lerp(0.25f, 1f, Mathf.Pow(open, 0.7f)), s, Mathf.Lerp(0.5f, 1f, open));
        float sway = Mathf.Sin(Time.time * 1.7f + id) * 1.2f;
        wingRoot.localRotation = Quaternion.Euler(-glideBrake * 9f + glideBar * 5f + sway * 0.4f, 0f, -glideTurn * 15f + sway);
        // hands pull the brake toggles
        Vector3 hl = new Vector3(-0.32f, -0.05f - bl * 0.3f, 0.12f), hr = new Vector3(0.32f, -0.05f - br * 0.3f, 0.12f);
        PoseLine(brakeLineL, hl, brakeTipL); PoseLine(brakeLineR, hr, brakeTipR);
    }
    readonly Dictionary<Transform, Quaternion> flapBase = new Dictionary<Transform, Quaternion>();
    Quaternion FlapBase(Transform f) { Quaternion q; if (!flapBase.TryGetValue(f, out q)) { q = f.localRotation; flapBase[f] = q; } return q; }

    float GroundBelow(Vector3 p)
    {
        RaycastHit hit;
        float g = -1e9f;
        if (Physics.Raycast(p + Vector3.up * 0.3f, Vector3.down, out hit, 900f, Vehicle.GroundMask, QueryTriggerInteraction.Ignore)) g = hit.point.y;
        else if (world == WorldId.Ranch) g = Mathf.Abs(p.x) < Layout.Half && Mathf.Abs(p.z) < Layout.Half ? Ranch.GY(p.x, p.z) : Countryside.Height(p.x, p.z, false);
        if (world == WorldId.Ranch && Layout.InPond(p.x, p.z)) g = Mathf.Max(g, Layout.WaterY);
        return g;
    }

    // per frame while gliding (Update); true = handled (no walking)
    bool GlideTick(float dt0)
    {
        float dt = Mathf.Min(Time.deltaTime, GlideDtCap);
        glideT += dt; glidePhT += dt;
        Vector3 p = transform.position;
        float gy = GroundBelow(p);
        glideAgl = p.y - gy;
        bool pond = world == WorldId.Ranch && Layout.InPond(p.x, p.z) && Ranch.GY(p.x, p.z) < Layout.WaterY - 0.6f;
        if (glidePh == 4) return PackTick(dt);
        GlideCamK = Mathf.MoveTowards(GlideCamK, 1f, dt * 1.5f);
        InputKind k = Game.HintKind(this);
        if (glidePh == 1)
        {
            // freefall with the pack on: exit momentum fades, auto-open after 0.6 s (or A / Space / tap now)
            vel.y = Mathf.Max(vel.y - Gravity * dt, -38f);
            planar = Vector3.MoveTowards(planar, Vector3.zero, 2.5f * dt);
            bool pull = human && input.hop && glidePhT > 0.12f;
            if (glidePhT >= 0.6f || pull || glideAgl < 7f)
            {
                glidePh = 2; glidePhT = 0f; chute = true;
                chuteGo.SetActive(true); packT.gameObject.SetActive(false);
                glideFwd = Mathf.Max(glideFwd, new Vector2(planar.x, planar.z).magnitude * 0.6f);
                Vector3 pc = transform.position + Vector3.up * 2f;
                FX.Puff(pc, Vector3.up * 1.5f, 1.4f, 0.9f, new Color(1f, 1f, 1f, 0.5f));
                if (human) Sfx.Play(Sfx.Door, 0.7f, 0.7f); else Sfx.PlayAt(Sfx.Door, pc, 0.6f, 60f, 0.7f);
                Debug.Log("FFDEMO chute deploy " + nick + " after " + glideT.ToString("0.00") + " s (" + (pull ? "pulled" : glideAgl < 7f ? "low" : "auto") + ") agl " + glideAgl.ToString("0.0") + " vy " + vel.y.ToString("0.0"));
            }
            prompt = human ? (k == InputKind.Gamepad ? "A" : k == InputKind.Touch ? "A" : "SPACE") + ": open the wing now" : "";
            cc.Move((planar + Vector3.up * vel.y) * dt);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            model.Animate(0.6f, true, false, false, dt);
            if (glideAgl < 0.3f || (cc.collisionFlags & CollisionFlags.Below) != 0) GlideLand(gy, pond);
            return true;
        }
        // ---- opening + flying ----
        float turn, bar, brake;
        if (human)
        {
            turn = input.move.x;
            bar = Mathf.Max(0f, input.move.y);
            brake = Mathf.Clamp01(Mathf.Max(input.brake, -input.move.y));
        }
        else AiGlide(p, out turn, out bar, out brake);
        // keep over the ranch: past the edge the wind turns you back
        if (world == WorldId.Ranch && (Mathf.Abs(p.x) > Layout.Half - 12f || Mathf.Abs(p.z) > Layout.Half - 12f))
        {
            float home = Mathf.Atan2(-p.x, -p.z) * Mathf.Rad2Deg;
            turn = Mathf.Clamp(Mathf.DeltaAngle(glideYaw, home) / 30f, -1f, 1f);
            if (human && prompt.Length == 0) Toast("The wind turns you back over the ranch", 1.2f);
        }
        if (glideBrake < 0.6f && brake >= 0.6f && glideFwd > 6.5f) { glideFlares++; glideFlareT = 0f; }
        glideTurn = Mathf.MoveTowards(glideTurn, Mathf.Clamp(turn, -1f, 1f), 3f * dt);
        glideBrake = Mathf.MoveTowards(glideBrake, brake, 4f * dt);
        glideBar = Mathf.MoveTowards(glideBar, bar * (1f - glideBrake), 2f * dt);
        glideYaw += glideTurn * (52f + 26f * glideBrake) * dt;
        float vT = Mathf.Lerp(9.2f, 4.2f, glideBrake) + glideBar * 2.3f;
        float open = glidePh == 2 ? Mathf.Clamp01(glidePhT / 0.75f) : 1f;
        glideFwd = Mathf.MoveTowards(glideFwd, vT, (glidePh == 2 ? (glideFwd > vT ? 5f : 8.4f) : glideFwd > vT ? 2.5f : 2.4f) * dt);
        float sink = Mathf.Lerp(1.75f, 1.05f, Mathf.Clamp01(glideBrake * 1.7f)) + glideBar * 0.9f
                   + Mathf.Abs(glideTurn) * (0.45f + 8f * glideBar);   // hard turn + speed bar = spiral dive
        // the flare: while the brakes bleed off speed (forward speed above the braked trim) the wing turns it into lift,
        // so the sink goes to ~0 for a moment; flared ~1-2 m up = touchdown at ~1 m/s and ~4 m/s instead of 1.75 / 9
        if (glidePh == 3) sink = Mathf.Max(0f, sink - Mathf.Max(0f, glideFwd - vT) * 0.7f);
        float vy = Mathf.MoveTowards(vel.y, -sink, (glidePh == 2 ? 26f : 4f) * dt);
        vel.y = Mathf.Min(vy, 0.6f);
        planar = Vector3.MoveTowards(planar, Vector3.zero, 3f * dt);
        if (glidePh == 2 && glidePhT > 0.75f) { glidePh = 3; glidePhT = 0f; Debug.Log("FFDEMO chute open " + nick + " fwd " + glideFwd.ToString("0.0") + " vy " + vel.y.ToString("0.0") + " agl " + glideAgl.ToString("0.0")); }
        if (glidePh == 3) glideMinVy = glideMinVy < -98f ? vel.y : Mathf.Max(glideMinVy, vel.y);
        Vector3 dir = Quaternion.Euler(0f, glideYaw, 0f) * Vector3.forward;
        cc.Move((dir * glideFwd + planar + Vector3.up * vel.y) * dt);
        yaw = glideYaw;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        model.Animate(0f, false, true, false, dt);
        model.transform.localRotation = Quaternion.Euler(-glideBrake * 6f, 0f, -glideTurn * 20f);   // swings out under the wing in a turn
        WingPose(open, dt);
        // wind rush puffs at speed
        if (Random.value < dt * (1f + glideBar * 4f)) FX.Puff(p + dir * 0.8f + Vector3.up * 1.4f, -dir * glideFwd * 0.5f, 0.3f, 0.6f, new Color(1f, 1f, 1f, 0.12f));
        // prompts (device glyphs via Hints46 GlideHint)
        if (human)
        {
            prompt = "glide";
            if (glideAgl < 4.5f && glideBrake < 0.5f && !glideFlareSaid) { glideFlareSaid = true; Toast("FLARE!  " + (k == InputKind.Gamepad ? "Pull LT" : k == InputKind.Touch ? "Pull the stick back" : "Hold S") + " just before touchdown", 1.6f); }
        }
        else prompt = "";
        Vector3 np = transform.position;
        if ((cc.collisionFlags & CollisionFlags.Below) != 0 || np.y - GroundBelow(np) < 0.06f || (pond && np.y < Layout.WaterY + 0.25f)) GlideLand(gy, pond);
        return true;
    }

    void GlideLand(float gy, bool pond)
    {
        glideLandVy = vel.y; glideLandFwd = glideFwd; glideLands++;
        glideSoft = glideLandVy > -1.6f && glideLandFwd < 6.8f;
        Vector3 p = transform.position;
        Debug.Log("FFDEMO chute land " + nick + " vy " + glideLandVy.ToString("0.00") + " fwd " + glideLandFwd.ToString("0.0") + " soft " + glideSoft + " flares " + glideFlares + " pond " + pond + " at " + p.ToString("0.0") + " after " + glideT.ToString("0.0") + " s");
        if (pond)
        {
            FX.Splash(p + Vector3.up * 0.5f, 20);
            if (human) { Sfx.Play(Sfx.Splash, 0.9f); Toast("Splash! Wing cut away - swim to shore", 2.5f); }
            GlideStop();
            return;
        }
        for (int i = 0; i < 6; i++) FX.Dust(p + Random.insideUnitSphere * 0.6f, 0.6f);
        if (human)
        {
            Sfx.Play(Sfx.Land, glideSoft ? 0.25f : 0.6f, glideSoft ? 1.2f : 0.9f);
            Toast(glideSoft ? "Nice flare - soft landing!" : "Bump! Flare (" + (inputKind == InputKind.Gamepad ? "LT" : inputKind == InputKind.Touch ? "stick back" : "S") + ") just before touchdown for a soft landing", 2.6f);
        }
        Vector3 dir = Quaternion.Euler(0f, glideYaw, 0f) * Vector3.forward;
        planar = dir * Mathf.Min(glideLandFwd, 6f) * 0.5f;   // a couple of run-out steps
        vel = Vector3.zero;
        if (glidePh == 1) { glidePh = 4; glidePhT = 0.9f; chute = false; return; }   // landed before it opened: just pack
        glidePh = 4; glidePhT = 0f;
    }

    // touchdown: the wing drops behind the froggy, then gets bundled into the pack and the pack is put away
    bool PackTick(float dt)
    {
        GlideCamK = Mathf.MoveTowards(GlideCamK, 0f, dt * 0.8f);
        float t = glidePhT;
        planar = Vector3.MoveTowards(planar, Vector3.zero, 6f * dt);
        vel.y -= Gravity * dt; if ((cc.collisionFlags & CollisionFlags.Below) != 0 && vel.y < -2f) vel.y = -2f;
        cc.Move((planar + vel) * dt);
        model.transform.localRotation = Quaternion.identity;
        model.Animate(planar.magnitude, false, false, false, dt);
        if (chuteGo != null && chuteGo.activeSelf)
        {
            float k = Mathf.Clamp01(t / 0.9f);
            chuteGo.transform.localRotation = Quaternion.Euler(-100f * Mathf.SmoothStep(0f, 1f, k), 0f, 0f);
            wingRoot.localScale = new Vector3(1f - 0.3f * k, Mathf.Lerp(1f, 0.25f, k), 1f);
            if (t > 0.9f)
            {
                float k2 = Mathf.Clamp01((t - 0.9f) / 0.5f);
                chuteGo.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.05f, k2);
                if (packT != null) { packT.gameObject.SetActive(true); packT.localScale = new Vector3(0.48f, 0.5f, 0.24f) * Mathf.Lerp(0.4f, 1f, k2); }
            }
        }
        prompt = human ? "Packing up the wing..." : "";
        if (t > 1.6f)
        {
            if (chuteGo != null) { chuteGo.transform.localScale = Vector3.one; chuteGo.transform.localRotation = Quaternion.identity; }
            GlideStop();
            if (human) Toast("Wing packed - back on your feet", 1.6f);
            Debug.Log("FFDEMO chute packed " + nick);
            return false;
        }
        return true;
    }

    // AI froggies: head for a spot (beside the player), S-turn to lose height over it, flare at ~3 m
    void AiGlide(Vector3 p, out float turn, out float bar, out float brake)
    {
        bar = 0f; brake = 0f;
        Vector3 goal;
        if (glideGoal.HasValue) goal = glideGoal.Value;
        else
        {
            Frog lead = null;
            if (Game.I != null) foreach (Frog f in Game.I.frogs) if (f != null && f.human && f.world == world) { lead = f; break; }
            goal = lead != null ? lead.transform.position + new Vector3(6f, 0f, 4f) : p + Quaternion.Euler(0f, glideYaw, 0f) * Vector3.forward * 30f;
        }
        Vector3 to = goal - p; to.y = 0f;
        float d = to.magnitude;
        float want = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
        // too high for the distance left: circle (S-turns) to burn height
        if (d < glideAgl * 3.2f && glideAgl > 6f) want = glideYaw + 60f * Mathf.Sign(Mathf.Sin(glideT * 0.35f + id));
        turn = Mathf.Clamp(Mathf.DeltaAngle(glideYaw, want) / 35f, -1f, 1f) * 0.8f;
        if (glideAgl < 2.2f) { brake = 1f; turn *= 0.2f; }
        else if (glideAgl < 6f) turn *= 0.4f;
    }

    // net puppets: the shown wing sways a little (no flight model on this device)
    void GlidePuppetTick()
    {
        if (!chute || chuteGo == null || wingRoot == null) return;
        float sway = Mathf.Sin(Time.time * 1.7f + id) * 1.2f;
        wingRoot.localRotation = Quaternion.Euler(sway * 0.4f, 0f, sway);
    }
}
