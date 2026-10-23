using System.Collections.Generic;
using UnityEngine;

// ffu76 (Bill, Oct 23 13:07): "Let the pilot steer where it touches down and put the default so the ramp doesn't end in the water."
//  * Default spot (every world, every mech size, several mechs landing at once): MechLand75's old spot (ranch: beside the Starship
//    pad, facing east; elsewhere the world's landing site) is checked first. A spot is good when the mech's footprint, the whole
//    ramp line (hinge -> foot), the foot and the walk-off fan are on dry ground (not the pond / pool - the same pond test the
//    swim code uses, Layout.InPond + water level), inside the world, clear of the house / garage / pads / Starship / track / forest
//    (known footprints with heights, plus a raycast against real colliders at the ramp's height), and clear of other landing
//    mechs (their footprint + ramp) and of the parked ranch mechs. If not, it turns the mech (12 headings) and then searches
//    rings around the old spot until one passes. (B9: the trillion-story mech's 174 m bridge ramp faced the pond.)
//  * Pilot steering: while the mech comes down, its pilot (a local human) moves the touchdown point with the left stick / WASD /
//    touch joystick (camera-relative; the landing camera holds still during the descent). A ring on the ground shows where it
//    will stand, a smaller ring + dashes show where the ramp's foot will reach. Green = good. Red = the ramp would end in water
//    (or hit something): touchdown is refused - it hovers and nudges itself to the nearest good spot. Holding the stick slows the
//    descent a little so there is time to steer. AI pilots use the default spot. Split screen: each pilot steers their own mech.
// &ff76old=1 = the ffu75 placement (no checks, no steering). &ff75old=1 still turns off MechLand75 entirely.
public static class F76
{
    static int old = -1;
    public static bool Old { get { if (old < 0) old = Ffu33.Url("ff76old") == "1" ? 1 : 0; return old == 1; } }
}

public partial class Frog
{
    // ffu76: the held pilot's left stick steers the landing mech (Frog.Update, land75 branch)
    void Steer76() { if (land75 != null && human && !netPuppet) land75.SteerIn76(this, input.move); }
}

public partial class MechLand75
{
    public Vector3 C0; public float yaw0;      // the old (ffu75) spot, for the probe / log
    public float reach76;
    public bool steer76, spotOk = true, hovering;
    public float steerDist, slowT;
    public int nudges, badEvals;
    public string spotWhy = "";
    public bool everBad;                       // probe: the marker went red at least once
    public static int Steered, Nudged, Moved76;
    public static readonly List<string> Log76 = new List<string>();
    float targetYaw;
    Vector2 steerIn; float steerAge = 9f;
    Vector3 camF76; bool camHave76;
    Vector3 descSide;
    Vector3 nudgeTo; bool haveNudge;
    float evalT; Vector3 evalAt = new Vector3(1e9f, 0f, 0f);
    Transform marker76; Transform[] ring76, foot76, dash76; Material okMat76, badMat76; bool markBad;
    static readonly float[] YawSteps = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 120f, -120f, 150f, -150f, 180f };
    static readonly RaycastHit[] hits76 = new RaycastHit[8];

    public float FootR { get { return Mathf.Max(3f, 0.13f * H); } }
    public bool MarkerOn76 { get { return marker76 != null && marker76.gameObject.activeSelf; } }
    public bool MarkerRed76 { get { return markBad; } }
    public Vector3 CamFwd76 { get { return camHave76 ? camF76 : -descSide; } }
    float hoverT;

    // ---------------- the checks ----------------
    static float Half(WorldId w) { return w == WorldId.Ranch ? Layout.Half : w == WorldId.Callisto ? 85f : 95f; }
    bool Inside(Vector3 p, float margin)
    {
        Vector3 o = Origin(world); float h = Half(world) - margin;
        return Mathf.Abs(p.x - o.x) <= h && Mathf.Abs(p.z - o.z) <= h;
    }
    public static bool Wet(WorldId w, Vector3 p)
    {
        if (w != WorldId.Ranch) return false;   // Moon, Mars, Callisto: no open water
        if (Layout.PondQ(p.x, p.z) < 1.12f) return true;                                   // the pond (+ its muddy shore)
        if (Layout.InPond(p.x, p.z) || Ranch.GY(p.x, p.z) < Layout.WaterY + 0.3f) return true;   // the swim test's water + anything below the water line
        Vector2 pc = Layout.PoolC, ps = Layout.PoolSize * 0.5f;
        return Mathf.Abs(p.x - pc.x) < ps.x + 1.5f && Mathf.Abs(p.z - pc.y) < ps.y + 1.5f;  // the pool
    }
    // known ranch structures: centre, half size, height (m above the ground)
    struct Box76 { public Vector2 c, h; public float top; public string name; public Box76(Vector2 c, Vector2 size, float top, string n) { this.c = c; h = size * 0.5f; this.top = top; name = n; } }
    static Box76[] boxes;
    static Box76[] Boxes
    {
        get
        {
            if (boxes != null) return boxes;
            var l = new List<Box76>
            {
                new Box76(Layout.HouseC, Layout.HouseSize + new Vector2(4f, 4f), Layout.HouseH + 5f, "the house"),
                new Box76(Layout.GarageC, Layout.GarageSize + new Vector2(4f, 4f), Layout.GarageH + 3f, "the garage"),
                new Box76(Layout.PadC, new Vector2(30f, 30f), 8f, "the Starship pad"),
                new Box76(Layout.PadC, new Vector2(16f, 16f), 80f, "the Starship"),
                new Box76(MissionSite.PadC, new Vector2(16f, 16f), 30f, "the robots' rocket pad"),
                new Box76(StorySet.PadXZ, new Vector2(20f, 20f), 40f, "the story rocket"),
                new Box76(JetLayout.HangarC, new Vector2(JetLayout.HangarW + 6f, JetLayout.HangarD + 6f), JetLayout.HangarH + 3f, "the jet hangar"),
                new Box76(JetLayout.PadC, new Vector2(JetLayout.PadR * 2f + 4f, JetLayout.PadR * 2f + 4f), 4f, "the VTOL pad"),
                new Box76((JetLayout.ForestMin + JetLayout.ForestMax) * 0.5f, JetLayout.ForestMax - JetLayout.ForestMin + new Vector2(8f, 8f), 30f, "the forest"),
                new Box76(RcTrack.C, new Vector2(44f, 34f), 4f, "the RC track"),
                new Box76(Layout.LoopC + new Vector2(-12f, 4f), new Vector2(70f, 34f), Layout.LoopR * 2f + 4f, "the loop"),
            };
            return boxes = l.ToArray();
        }
    }
    // something at p reaching up to 'clear' m above the ground (ramp height there; 1e4 = the mech's own footprint)
    bool Blocked(Vector3 p, float clear, out string why)
    {
        why = "";
        if (world == WorldId.Ranch)
        {
            foreach (var b in Boxes)
                if (Mathf.Abs(p.x - b.c.x) < b.h.x && Mathf.Abs(p.z - b.c.y) < b.h.y && b.top > clear - 1f) { why = b.name; return true; }
            if (clear < 6f && (Layout.RoadDist(p.x, p.z) < Layout.TrackW * 0.5f + 2f || Layout.LoopLinkDist(p.x, p.z) < Layout.LoopW * 0.5f + 2f)) { why = "the track"; return true; }
        }
        // real colliders (buildings, rocks, props) reaching up to the ramp / mech
        float g = Ground(world, p.x, p.z);
        float top = 150f, low = clear >= 1000f ? 0.8f : Mathf.Max(0.8f, clear - 1f);
        if (top <= low) return false;
        int n = Physics.RaycastNonAlloc(new Vector3(p.x, g + top, p.z), Vector3.down, hits76, top - low, Vehicle.GroundMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider c = hits76[i].collider;
            if (c == null || c is TerrainCollider) continue;
            Vector3 bs = c.bounds.size;
            if (bs.x > 150f || bs.z > 150f) continue;   // ground meshes / big planes
            if (hits76[i].point.y < Ground(world, hits76[i].point.x, hits76[i].point.z) + 0.8f) continue;
            why = c.name; return true;
        }
        return false;
    }
    static float SegD(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector2 P = new Vector2(p.x, p.z), A = new Vector2(a.x, a.z), B = new Vector2(b.x, b.z), ab = B - A;
        float u = Mathf.Clamp01(Vector2.Dot(P - A, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
        return (P - (A + ab * u)).magnitude;
    }
    static float D2(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return (a - b).magnitude; }
    bool Crowded(Vector3 p, bool body, out string why)
    {
        why = "";
        foreach (var o in All)
        {
            if (o == null || o == this || o.world != world || o.phase == Ph.Done) continue;
            Vector3 hx = o.C + o.fwd * o.hingeL.z;
            if (body)
            {
                if (D2(p, o.C) < FootR + o.FootR + 6f) { why = "another mech (" + o.mech.Title + ")"; return true; }
                if (SegD(p, hx, o.F) < FootR + 5f) { why = o.mech.Title + "'s ramp"; return true; }
            }
            else if (D2(p, o.C) < o.FootR + 3f || SegD(p, hx, o.F) < 5f || D2(p, o.F) < 9f) { why = o.mech.Title + (D2(p, o.C) < o.FootR + 3f ? "" : "'s ramp"); return true; }
        }
        if (world == WorldId.Ranch)
            foreach (var m in StoryMech.AllMechs)
            {
                if (m == null || m == mech || m.Away72 || m.wrecked || !m.gameObject.activeInHierarchy) continue;
                float r = Mathf.Max(3f, 0.13f * m.height) + (body ? FootR + 6f : 3f);
                if (D2(p, m.transform.position) < r) { why = m.Title; return true; }
            }
        return false;
    }
    // can the mech stand at c facing yaw, with its whole ramp and walk-off on dry, clear ground?
    public bool SpotOk(Vector3 c, float yaw, out string why)
    {
        Vector3 fw = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward, rt = new Vector3(fw.z, 0f, -fw.x);
        Vector3 f = c + fw * reach76, hx = c + fw * hingeL.z;
        float fr = FootR;
        var body = new List<Vector3>(9) { c };
        for (int i = 0; i < 8; i++) body.Add(c + Quaternion.Euler(0f, i * 45f, 0f) * fw * fr);
        int ns = Mathf.Clamp(Mathf.CeilToInt((f - hx).magnitude / 3f), 2, 60);
        var ramp = new List<Vector3>(ns + 1); var rampH = new List<float>(ns + 1);
        float hy = hingeL.y;
        for (int i = 0; i <= ns; i++) { float u = i / (float)ns; ramp.Add(Vector3.Lerp(hx, f, u)); rampH.Add(Mathf.Lerp(hy, 0f, u)); }
        var fan = new List<Vector3> { f, f + fw * 3.5f, f + fw * 3.5f + rt * 6f, f + fw * 3.5f - rt * 6f, f + fw * 7f };
        // cheap tests first: edges, water, crowding
        foreach (var p in body) { if (!Inside(p, 2f)) { why = "the mech would stand off the edge"; return false; } if (Wet(world, p)) { why = "the mech would stand in water"; return false; } }
        foreach (var p in fan) { if (!Inside(p, 3f)) { why = "the ramp would end off the edge"; return false; } if (Wet(world, p)) { why = "the ramp would end in water"; return false; } }
        foreach (var p in ramp) { if (!Inside(p, 2f)) { why = "the ramp would reach off the edge"; return false; } if (Wet(world, p)) { why = "the ramp would cross water"; return false; } }
        foreach (var p in body) if (Crowded(p, p == c, out why)) { why = "too close to " + why; return false; }
        foreach (var p in fan) if (Crowded(p, false, out why)) { why = "the ramp would end on " + why; return false; }
        for (int i = 0; i < ramp.Count; i += 2) if (Crowded(ramp[i], false, out why)) { why = "the ramp would cross " + why; return false; }
        // a walk-off that doesn't drop into a crater / up a cliff
        float gf = Ground(world, f.x, f.z);
        for (int i = 1; i < fan.Count; i++) if (Mathf.Abs(Ground(world, fan[i].x, fan[i].z) - gf) > 2.5f) { why = "the ramp foot would be on a steep slope"; return false; }
        // structures / colliders
        foreach (var p in body) if (Blocked(p, 1e4f, out why)) { why = "the mech would stand on " + why; return false; }
        foreach (var p in fan) if (Blocked(p, 2.5f, out why)) { why = "the ramp would end on " + why; return false; }
        for (int i = 0; i < ramp.Count; i++) if (Blocked(ramp[i], rampH[i], out why)) { why = "the ramp would hit " + why; return false; }
        why = "dry and clear";
        return true;
    }
    bool YawAt(Vector3 c, float pref, out float yaw, out string why)
    {
        why = "";
        foreach (float d in YawSteps) { yaw = pref + d; if (SpotOk(c, yaw, out why)) return true; }
        yaw = pref; SpotOk(c, pref, out why); return false;
    }
    // nearest good spot to 'from' (rings outwards), preferring the heading 'pref'
    public bool FindSpot(Vector3 from, float pref, float maxR, out Vector3 at, out float yaw)
    {
        float step = Mathf.Max(6f, 0.12f * H);
        string why;
        for (float r = 0f; r <= maxR + 0.01f; r += step)
        {
            int n = r < 0.01f ? 1 : Mathf.Max(6, Mathf.RoundToInt(2f * Mathf.PI * r / step));
            float a0 = Random.value * 360f;
            for (int k = 0; k < n; k++)
            {
                Vector3 p = from + Quaternion.Euler(0f, a0 + k * 360f / n, 0f) * Vector3.forward * r;
                p.y = Ground(world, p.x, p.z);
                if (!Inside(p, FootR + 2f)) continue;
                if (YawAt(p, pref, out yaw, out why)) { at = p; return true; }
            }
        }
        at = from; yaw = pref; return false;
    }

    // ---------------- Setup: the default spot ----------------
    void Spot76(float reach)
    {
        reach76 = reach;
        C0 = C; yaw0 = yawDeg; targetYaw = yawDeg;
        descSide = (Quaternion.Euler(0f, 62f, 0f) * fwd).normalized;
        if (F76.Old) return;
        string why;
        Vector3 c = C; c.y = Ground(world, c.x, c.z);
        if (SpotOk(c, yawDeg, out why)) { spotWhy = why; }
        else
        {
            string was = why; float yw; Vector3 at;
            float maxR = world == WorldId.Ranch ? 260f : 120f;
            if (FindSpot(c, yawDeg, maxR, out at, out yw)) { PlaceAt(at, yw); spotOk = true; spotWhy = "moved from the old spot (" + was + ")"; Moved76++; }
            else { spotOk = false; spotWhy = "no dry spot found (" + was + ") - old spot kept"; }
        }
        targetYaw = yawDeg;
        descSide = (Quaternion.Euler(0f, 62f, 0f) * fwd).normalized;
        steer76 = pilot != null && pilot.human && !pilot.netPuppet;
        string s = "FF76 spot for " + mech.Title + " (H " + H + ") on " + bodyId + ": " + C.ToString("0") + " yaw " + yawDeg.ToString("0") + " ramp foot " + F.ToString("0") + " | " + spotWhy + " | old spot " + C0.ToString("0") + " yaw " + yaw0.ToString("0") + (steer76 ? " | " + pilot.nick + " can steer" : " | AI pilot: default spot");
        Log76.Add(s); Debug.Log(s);
        if (steer76) BuildMarker76();
    }
    // put the planned mech at c facing yaw (the body keeps its altitude)
    void PlaceAt(Vector3 c, float yaw)
    {
        yawDeg = yaw;
        fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        right = new Vector3(fwd.z, 0f, -fwd.x);
        C = c;
        float gy = Ground(world, C.x, C.z);
        foreach (float sx in new[] { -1f, 1f }) { Vector3 fp = C + right * (sx * 0.1f * H); gy = Mathf.Max(gy, Ground(world, fp.x, fp.z)); }
        C.y = gy;
        F = C + fwd * reach76; F.y = Ground(world, F.x, F.z);
        root.position = C; root.rotation = Quaternion.Euler(0f, yawDeg, 0f);
    }

    // ---------------- steering (Descend) ----------------
    public void SteerIn76(Frog f, Vector2 mv) { if (f == pilot && steer76) { steerIn = mv; steerAge = 0f; } }
    public void Cam76(Camera cam) { if (cam != null) { camF76 = cam.transform.forward; camHave76 = true; } }
    float SteerSpeed { get { return Mathf.Clamp(10f + H * 0.3f, 12f, 60f); } }
    float MaxSteerR { get { return world == WorldId.Ranch ? 400f : 60f + H; } }

    // called every Descend frame on the new path; returns true while touchdown must wait (hovering to a good spot)
    bool Descend76(float dt, bool skip)
    {
        if (F76.Old) return false;
        steerAge += dt;
        Vector2 v = steerAge < 0.3f ? steerIn : Vector2.zero;
        bool pushing = steer76 && v.magnitude > 0.15f;
        Vector3 c = C;
        if (pushing)
        {
            Vector3 cf = camHave76 ? camF76 : -descSide; cf.y = 0f; if (cf.sqrMagnitude < 1e-4f) cf = -descSide; cf.Normalize();
            Vector3 cr = new Vector3(cf.z, 0f, -cf.x);
            Vector2 vv = v.magnitude > 1f ? v.normalized : v;
            Vector3 mv = (cr * vv.x + cf * vv.y) * SteerSpeed * dt;
            Vector3 n = c + mv;
            Vector3 off = n - C0; off.y = 0f;
            if (off.magnitude > MaxSteerR) n = C0 + off.normalized * MaxSteerR;
            Vector3 o = Origin(world); float h = Half(world) - FootR - 3f;
            n.x = Mathf.Clamp(n.x, o.x - h, o.x + h); n.z = Mathf.Clamp(n.z, o.z - h, o.z + h);
            steerDist += D2(n, c);
            c = n; haveNudge = false;
            // a little more time to steer while the stick is held (at most 8 s more in all)
            if (slowT < 8f && !skip) { t -= dt * 0.5f; slowT += dt * 0.5f; }
        }
        // is the spot under the mech good (any heading)? re-check when it moved or every 0.3 s
        evalT += dt;
        if (D2(c, evalAt) > 0.5f || evalT > 0.3f)
        {
            evalT = 0f; evalAt = c;
            float yw; string why;
            Vector3 cg = c; cg.y = Ground(world, c.x, c.z);
            bool ok = YawAt(cg, targetYaw, out yw, out why);
            if (ok) targetYaw = yw; else { badEvals++; everBad = true; }
            if (ok != spotOk) Debug.Log("FF76 " + mech.Title + " landing spot " + (ok ? "OK" : "refused: " + why) + " at " + c.ToString("0"));
            spotOk = ok; spotWhy = why;
        }
        // red and nobody steering: nudge to the nearest good spot
        if (!spotOk && !pushing)
        {
            if (!haveNudge)
            {
                float yw; Vector3 at;
                if (FindSpot(c, targetYaw, 80f + H, out at, out yw)) { nudgeTo = at; haveNudge = true; nudges++; Nudged++; Debug.Log("FF76 nudging " + mech.Title + " from " + c.ToString("0") + " to dry ground at " + at.ToString("0")); }
                else { nudgeTo = C0; haveNudge = true; }
            }
            Vector3 d = nudgeTo - c; d.y = 0f;
            float stp = SteerSpeed * 1.2f * dt;
            c = d.magnitude <= stp ? new Vector3(nudgeTo.x, c.y, nudgeTo.z) : c + d.normalized * stp;
        }
        if (D2(c, C) > 1e-3f || Mathf.Abs(Mathf.DeltaAngle(yawDeg, targetYaw)) > 0.01f)
        {
            float ny = Mathf.MoveTowardsAngle(yawDeg, targetYaw, 120f * dt);
            Vector3 keep = c;
            PlaceAt(keep, ny);
        }
        Marker76();
        // touchdown waits for a good spot (and the turn to finish); hover just above the ground, give up after 6 s
        bool due = t >= DescentT || skip;
        bool ready = spotOk && Mathf.Abs(Mathf.DeltaAngle(yawDeg, targetYaw)) < 1f;
        if (due && !ready)
        {
            hovering = true;
            if (!pushing) hoverT += dt;
            if (hoverT > 8f) { hovering = false; Debug.Log("FF76 " + mech.Title + " gave up waiting for a dry spot at " + C.ToString("0")); return false; }
            if (t > DescentT - 0.5f) t = DescentT - 0.5f;
            return true;
        }
        hovering = false;
        return false;
    }

    // final spot at touchdown: ramp aim, crew walk-off targets, the boarding spot
    void Finalize76()
    {
        if (F76.Old) return;
        PlaceAt(C, Mathf.Abs(Mathf.DeltaAngle(yawDeg, targetYaw)) < 1.5f ? targetYaw : yawDeg);
        AimRamp(); SetRamp(0f); rampHinge.gameObject.SetActive(false);
        var keys = new List<Frog>(targets.Keys); int n = keys.Count;
        for (int k = 0; k < n; k++)
        {
            Frog f = keys[k];
            Vector3 tp = F + fwd * 3.5f + right * ((k - (n - 1) * 0.5f) * 1.9f);
            tp.y = Ground(world, tp.x, tp.z) + 0.05f;
            targets[f] = tp;
            if (f != null && f.netPuppet) f.SendTo(world, tp + Vector3.up * 0.4f, yawDeg);
        }
        if (spot != null) spot.pos = F + fwd * 1.5f;
        float moved = D2(C, C0);
        if (steer76 && steerDist > 3f) Steered++;
        string why; bool ok = SpotOk(C, yawDeg, out why);
        string s = "FF76 touchdown spot " + mech.Title + " on " + bodyId + ": " + C.ToString("0") + " yaw " + yawDeg.ToString("0") + " ramp foot " + F.ToString("0") + " " + (ok ? "dry" : "NOT OK (" + why + ")") + " | " + moved.ToString("0") + " m from the old spot, steered " + steerDist.ToString("0") + " m, nudges " + nudges;
        Log76.Add(s); Debug.Log(s);
        if (marker76 != null) marker76.gameObject.SetActive(false);
    }

    // ---------------- the ground marker ----------------
    void BuildMarker76()
    {
        okMat76 = Mats.Unlit(new Color(0.35f, 1f, 0.45f));
        badMat76 = Mats.Unlit(new Color(1f, 0.22f, 0.18f));
        marker76 = new GameObject("LandMarker76").transform;
        marker76.SetParent(transform, false);
        ring76 = new Transform[32]; foot76 = new Transform[16]; dash76 = new Transform[10];
        for (int i = 0; i < ring76.Length; i++) ring76[i] = Mats.Prim(PrimitiveType.Cube, marker76, Vector3.zero, Vector3.one, okMat76).transform;
        for (int i = 0; i < foot76.Length; i++) foot76[i] = Mats.Prim(PrimitiveType.Cube, marker76, Vector3.zero, Vector3.one, okMat76).transform;
        for (int i = 0; i < dash76.Length; i++) dash76[i] = Mats.Prim(PrimitiveType.Cube, marker76, Vector3.zero, Vector3.one, okMat76).transform;
        Mats.NoShadows(marker76.gameObject);
        markBad = false;
        Marker76();
    }
    void Ring76(Transform[] segs, Vector3 c, float r, float w)
    {
        int n = segs.Length;
        float len = 2f * Mathf.PI * r / n * 0.72f;
        for (int i = 0; i < n; i++)
        {
            float a = (i + 0.5f) * 360f / n;
            Vector3 d = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
            Vector3 p = c + d * r; p.y = Ground(world, p.x, p.z) + 0.25f + w * 0.1f;
            segs[i].position = p;
            segs[i].rotation = Quaternion.LookRotation(new Vector3(d.z, 0f, -d.x));
            segs[i].localScale = new Vector3(w, 0.12f + w * 0.05f, len);
        }
    }
    void Marker76()
    {
        if (marker76 == null) return;
        bool show = phase == Ph.Descend;
        if (marker76.gameObject.activeSelf != show) marker76.gameObject.SetActive(show);
        if (!show) return;
        bool bad = !spotOk;
        if (bad != markBad)
        {
            markBad = bad;
            Material m = bad ? badMat76 : okMat76;
            foreach (var s in ring76) s.GetComponent<Renderer>().sharedMaterial = m;
            foreach (var s in foot76) s.GetComponent<Renderer>().sharedMaterial = m;
            foreach (var s in dash76) s.GetComponent<Renderer>().sharedMaterial = m;
        }
        float w = Mathf.Clamp(H * 0.012f, 0.35f, 2.2f);
        Ring76(ring76, C, FootR + 1.5f, w);
        Ring76(foot76, F, Mathf.Max(2.5f, H * 0.03f), w * 0.8f);
        Vector3 a = C + fwd * (FootR + 1.5f), b = F - fwd * Mathf.Max(2.5f, H * 0.03f);
        Vector3 ab = b - a; ab.y = 0f;
        float L = ab.magnitude; int n = dash76.Length;
        for (int i = 0; i < n; i++)
        {
            bool on = L > 2f;
            if (dash76[i].gameObject.activeSelf != on) dash76[i].gameObject.SetActive(on);
            if (!on) continue;
            Vector3 p = a + ab * ((i + 0.5f) / n); p.y = Ground(world, p.x, p.z) + 0.25f + w * 0.1f;
            dash76[i].position = p;
            dash76[i].rotation = Quaternion.LookRotation(ab.normalized);
            dash76[i].localScale = new Vector3(w * 0.7f, 0.12f + w * 0.05f, L / n * 0.55f);
        }
    }

    // HUD line for the pilot during the descent (plain text, no rich-text tags)
    string Prompt76(Frog f)
    {
        if (F76.Old || f != pilot || !steer76) return null;
        if (!spotOk) return hovering ? "Ramp would end in water - moving to dry ground..." : "Red: the ramp would end in water - steer to dry ground";
        string st = f.inputKind == InputKind.Gamepad ? "Left stick" : f.inputKind == InputKind.Touch ? "Joystick" : "WASD";
        string go = f.inputKind == InputKind.Gamepad ? "A" : f.inputKind == InputKind.Touch ? "A" : "Space";
        return st + ": steer where it lands  |  " + go + ": land now";
    }
}
