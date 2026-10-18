using UnityEngine;

// ffu33: where the Enchanted Forest and James's jet base sit on the ranch. Pure maths (no scene objects) so the editor build
// script can flatten the terrain under the runway + hangar (Layout.GroundY x Flat) and Ranch.MeshTrees can keep its random
// trees / bushes / rocks off them (Reserved).
//  * jet base: the open strip south of the pond (clear of the pond basin, the 1000-story mech row west of x -30, the story
//    pad, the ffu30 RC raceway + MX line in the east at z 0..45, the ffu31 trailer barn north of the track, the ffu32 garden):
//    a short east-west runway (x 6..128, z -161), the apron with the VTOL pad, and the hangar at the east end, doors west.
//  * forest: the north-east corner behind the rally track's east lobe (x 130..177, z 84..177).
public static class JetLayout
{
    // short runway along x (west-east)
    public const float RunwayX0 = 6f, RunwayX1 = 128f, RunwayZ = -161f, RunwayW = 14f;
    // hangar (doors on the WEST face, towards the apron + runway). W = size along x, D = along z
    public static readonly Vector2 HangarC = new Vector2(156f, -153f);
    public const float HangarW = 22f, HangarD = 26f, HangarH = 9.5f;
    public static float DoorX { get { return HangarC.x - HangarW * 0.5f; } }
    // apron (runway end -> hangar doors) with the VTOL pad in the middle
    public static readonly Vector2 PadC = new Vector2(136.5f, -153f);
    public const float PadR = 7f;
    public const float ApronX0 = 126f;

    // the enchanted forest (north-east corner, behind the track's east lobe)
    public static readonly Vector2 ForestMin = new Vector2(130f, 84f), ForestMax = new Vector2(177f, 177f);
    public static Vector2 ForestC { get { return (ForestMin + ForestMax) * 0.5f; } }

    static bool InRect(float x, float z, float x0, float x1, float z0, float z1, float m) { return x > x0 - m && x < x1 + m && z > z0 - m && z < z1 + m; }

    // hangar + apron + runway (with a margin)
    public static bool InBase(float x, float z, float margin)
    {
        if (InRect(x, z, ApronX0, HangarC.x + HangarW * 0.5f, HangarC.y - HangarD * 0.5f, HangarC.y + HangarD * 0.5f, margin)) return true;
        return InRect(x, z, RunwayX0, RunwayX1, RunwayZ - RunwayW * 0.5f, RunwayZ + RunwayW * 0.5f, margin);
    }

    public static bool InForest(float x, float z, float margin = 0f)
    {
        return x > ForestMin.x - margin && x < ForestMax.x + margin && z > ForestMin.y - margin && z < ForestMax.y + margin;
    }

    // ranch random trees / bushes / rocks keep off the base (the forest grows its own trees)
    public static bool Reserved(float x, float z) { return InBase(x, z, 7f); }

    // terrain noise multiplier: 0 on the base (flat), eased back to 1 over 16 m
    public static float Flat(float x, float z)
    {
        float d = Mathf.Min(
            RectDist(x, z, ApronX0 - 2f, HangarC.x + HangarW * 0.5f + 2f, HangarC.y - HangarD * 0.5f - 2f, HangarC.y + HangarD * 0.5f + 2f),
            RectDist(x, z, RunwayX0 - 4f, RunwayX1 + 2f, RunwayZ - RunwayW * 0.5f - 3f, RunwayZ + RunwayW * 0.5f + 3f));
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d / 16f));
    }

    static float RectDist(float x, float z, float x0, float x1, float z0, float z1)
    {
        float dx = Mathf.Max(0f, Mathf.Max(x0 - x, x - x1));
        float dz = Mathf.Max(0f, Mathf.Max(z0 - z, z - z1));
        return Mathf.Sqrt(dx * dx + dz * dz);
    }
}
