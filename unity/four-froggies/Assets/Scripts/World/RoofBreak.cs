using System.Collections.Generic;
using UnityEngine;

// ffu39 MECHS BREAK ROOFS (Bill: a big mech landing / jumping onto the garage - especially the 1000-story - had its feet go
// straight through the roof). Root cause: story mechs walk on the terrain height (Ranch.GY) and only crushed what sat at
// ground level round a planted foot (Destruct.Stomp at the foot, radius H*0.09+1.2 m), so a roof 5-12 m up was never
// touched; the garage, trailer barn, hangar and treehouse roofs were not breakable at all (only the house's north roof
// strip was).
//
// Now every roof is breakable ffu21 destruction (BreakKind.Roof: debris chunks in its own material, smoke, regrows after
// 26-38 s with sparkles): the garage roof is 6 x 2 panels, the house's solid south roof 4 panels (plus the existing 4 north
// strips + chimney), each trailer-barn slope 4 panels, the hangar's fixed strips 3 panels each + both sliding panels, each
// treehouse roof slab one panel. RoofBreak.Add splits a roof slab (any rotation) into nx x nz world-space panels under
// RanchBreak.Root, then registers them.
//
// Mechs (StoryMech.RoofTick each frame):
//  * legs / body overlapping an intact roof from below or the side (walking into a building, a landing that comes down on
//    it) -> damage scaled by mech size x speed: anything bigger than the 10-story punches straight through (a hole the size
//    of its footprint, 1-2 panels for the 100-story, more for the 1000-story, the whole garage for the trillion-story);
//  * landing on a roof: impact = size x fall speed. The 10-story (the small one) can STAND on a sturdy roof after a soft
//    landing: each step on it cracks it (smoke, chips, a creak) and after a few steps / a hard landing it gives way;
//  * every break: debris + dust, MechStomp crunch boom + Game.Quake at the roof, and the mech drops through the hole under
//    gravity onto the floor / ground (StoryMech: grounded = false when the support goes).
// Holes regrow with the rest of the destruction (Destruct keeps the timers; Occupied ignores story mechs, so a regrown panel
// just reappears around a mech that is still standing in the hole).
public static class RoofBreak
{
    public static readonly List<Breakable> Pieces = new List<Breakable>();
    public static int Breaks, Cracks;
    static float fxT;

    // split a roof slab (a scaled cube primitive) into nx x nz panels along its local x / z; returns the panels
    public static List<Breakable> Add(GameObject slab, int nx, int nz, bool split = true, bool batch = true, float hpMul = 1f)
    {
        var outL = new List<Breakable>();
        if (slab == null) return outL;
        if (!split)
        {
            var b0 = Destruct.Register(slab, BreakKind.Roof, batch, hpMul);
            if (b0 != null) { Pieces.Add(b0); outL.Add(b0); }
            return outL;
        }
        var t = slab.transform;
        var mr = slab.GetComponent<MeshRenderer>();
        Material m = mr != null ? mr.sharedMaterial : Mats.Lit(Color.gray);
        bool col = slab.GetComponent<Collider>() != null && slab.GetComponent<Collider>().enabled;
        Vector3 ls = t.lossyScale;
        Quaternion q = t.rotation;
        for (int i = 0; i < nx; i++)
            for (int k = 0; k < nz; k++)
            {
                Vector3 local = new Vector3((i + 0.5f) / nx - 0.5f, 0f, (k + 0.5f) / nz - 0.5f);   // unit-cube coords
                Vector3 wp = t.TransformPoint(local);
                Vector3 size = new Vector3(ls.x / nx - 0.01f, ls.y, ls.z / nz - 0.01f);
                Transform g = RanchBreak.Group(slab.name + "Panel", wp);
                g.rotation = q;
                var p = Mats.Prim(PrimitiveType.Cube, g, Vector3.zero, size, m, col);
                p.transform.localPosition = Vector3.zero; p.transform.localRotation = Quaternion.identity;
                var r = p.GetComponent<MeshRenderer>();
                if (r != null && mr != null) { r.shadowCastingMode = mr.shadowCastingMode; r.receiveShadows = mr.receiveShadows; }
                var b = Destruct.Register(g.gameObject, BreakKind.Roof, batch, hpMul);
                if (b != null) { Pieces.Add(b); outL.Add(b); }
            }
        Object.Destroy(slab);
        slab.SetActive(false);   // Destroy is end of frame; keep it out of MeshMerge / colliders now
        return outL;
    }

    // register already-separate roof pieces (the house's north strips, chimney)
    public static void Track(Breakable b) { if (b != null && b.kind == BreakKind.Roof && !Pieces.Contains(b)) Pieces.Add(b); }

    static bool OverXZ(Bounds b, Vector3 p, float r)
    {
        float dx = Mathf.Max(0f, Mathf.Abs(p.x - b.center.x) - b.extents.x), dz = Mathf.Max(0f, Mathf.Abs(p.z - b.center.z) - b.extents.z);
        return dx * dx + dz * dz <= r * r;
    }

    // top of the roof surface under (x, z) (bounds-based, good enough for the shallow roofs here)
    static float TopAt(Breakable b, Vector3 p) { return b.bounds.max.y; }

    // the small mech (10-story) can stand on an intact roof it is already above; -inf otherwise
    public static float SupportY(Vector3 p, float H, int band, float curY)
    {
        if (band > 0 || Pieces.Count == 0) return float.NegativeInfinity;
        float best = float.NegativeInfinity, r = H * 0.08f;
        foreach (var b in Pieces)
        {
            if (b == null || b.state != 0) continue;
            if (!OverXZ(b.bounds, p, r)) continue;
            float top = TopAt(b, p);
            if (curY >= top - 0.35f && top > best) best = top;
        }
        return best;
    }

    // per frame: break roofs the mech's legs / body pass through. p = mech root (feet), vel = its velocity
    // prevY: the mech's height last frame - a fast fall (or a low frame rate) can carry the feet from above a roof to the
    // ground in one step, so the test is swept over [p.y, prevY]
    public static void MechTick(StoryMech m, Vector3 p, Vector3 vel, float H, int band, bool standingOnRoof, float prevY)
    {
        if (Pieces.Count == 0 || m == null) return;
        float r = H * 0.14f;
        float sp = new Vector2(vel.x, vel.z).magnitude, fall = Mathf.Max(0f, -vel.y);
        for (int i = 0; i < Pieces.Count; i++)
        {
            Breakable b = Pieces[i];
            if (b == null || b.state != 0) continue;
            Bounds bb = b.bounds;
            float lo = Mathf.Min(p.y, prevY);
            if (lo + H < bb.min.y || lo > bb.max.y - 0.3f) continue;   // all above it / below it (swept)
            if (!OverXZ(bb, p, r)) continue;
            if (standingOnRoof && band == 0 && p.y >= bb.max.y - 0.5f) continue;   // the small mech on top of it
            // legs / body through the roof: big mechs always punch through; the 10-story too unless it stands on top
            float dmg = band > 0 ? 999f : 12f + (sp + fall) * 4f * Time.deltaTime * 30f;
            Hit(b, new Vector3(p.x, bb.max.y, p.z), dmg, H, Mathf.Clamp(0.8f + (sp + fall) / 20f, 0.8f, 2f), m);
        }
    }

    // a landing or a footfall ON a roof (power: 1 = a step, 1.3-2.5 = a landing)
    public static bool Impact(StoryMech m, Vector3 p, float H, int band, float fallSpeed, float power)
    {
        bool broke = false;
        float r = H * (power > 1.2f ? 0.16f : 0.09f) + 0.8f;
        float e = (band == 0 ? 0.6f : 6f + band * 6f) * (4f + fallSpeed * 1.4f) * power;   // size x fall speed (the 10-story: a soft landing / steps crack, a hop or a hard landing breaks)
        for (int i = 0; i < Pieces.Count; i++)
        {
            Breakable b = Pieces[i];
            if (b == null || b.state != 0) continue;
            if (!OverXZ(b.bounds, p, r) || Mathf.Abs(p.y - b.bounds.max.y) > 1.2f + H * 0.05f) continue;
            if (Hit(b, new Vector3(p.x, b.bounds.max.y, p.z), e, H, power, m)) broke = true;
        }
        return broke;
    }

    static bool Hit(Breakable b, Vector3 at, float dmg, float H, float power, StoryMech by)
    {
        b.hp -= dmg;
        bool fx = Time.unscaledTime > fxT;
        if (b.hp > 0f)
        {
            // dent / crack: smoke, chips, a creak - the roof holds for now
            Cracks++;
            if (fx)
            {
                fxT = Time.unscaledTime + 0.2f;
                FX.Smoke(at, 1f + H * 0.05f, new Color(0.45f, 0.42f, 0.38f, 0.6f));
                for (int k = 0; k < 4; k++) Chunks.Spawn(at + Random.insideUnitSphere * 0.6f, Random.insideUnitSphere * 3f + Vector3.up * 3f, Vector3.one * Random.Range(0.12f, 0.3f), b.debrisMats[0], 2.5f);
                if (Sfx.Crash != null && Sfx.Crash.Length > 0) Sfx.PlayAt(Sfx.Pick(Sfx.Crash), at, 0.25f, 60f, Random.Range(1.2f, 1.4f));
                if (Game.I != null) Game.I.Quake(at, 0.18f, 30f);
            }
            if (PadGuardDemoLog) Debug.Log("ROOF crack " + b.name + " hp " + b.hp.ToString("0") + "/" + b.maxHp.ToString("0"));
            return false;
        }
        Breaks++;
        Destruct.Break(b, at + Vector3.up * 2f, Mathf.Max(30f, dmg * 0.2f), false);
        if (fx)
        {
            fxT = Time.unscaledTime + 0.15f;
            MechStomp.Foot(at, H, Mathf.Clamp(power, 1f, 2.2f), false);   // crunch boom + ground quake + dust cloud at the roof
            for (int k = 0; k < 10; k++) FX.Dust(at + Random.insideUnitSphere * Mathf.Max(1.5f, H * 0.06f), 1f);
        }
        Debug.Log("ROOF BREAK " + b.name + " at " + at.ToString("0") + " by " + (by != null ? by.Title : "?") + " (H " + H.ToString("0") + ")");
        return true;
    }
    public static bool PadGuardDemoLog;
}
