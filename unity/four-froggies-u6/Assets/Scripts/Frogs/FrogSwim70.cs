using UnityEngine;

// ffu70 (Ben item 2) swim boost pose: the froggy lies FLAT on the water (belly down, head forward and up) and does a breaststroke
// kick cycle. ph 0..1 with 0 = the kick: legs snap from drawn-up + splayed to straight back together while the arms shoot forward
// (0..0.25), glide (0.25..0.5), arms sweep out and back (0.5..0.8), knees draw up + hands tuck for the next kick (0.8..1).
// k blends from whatever Animate() set (the upright swim) to this pose. Same rig names on the frog mesh, the primitive frog and the
// pets. No allocations.
public partial class FrogModel
{
    static float Ease(float a, float b, float t) { t = Mathf.Clamp01(t); t = t * t * (3f - 2f * t); return a + (b - a) * t; }
    public void AnimateBreast(float ph, float k, float t)
    {
        if (bob == null) return;
        float bodyPitch = 76f + Mathf.Sin(ph * 6.2831853f) * 3f;
        // keep the body centred over the froggy (the pivot is at the feet): back a little, up a little so the back breaks the surface
        bob.localPosition = Vector3.Lerp(bob.localPosition, new Vector3(0f, 0.12f + (ph < 0.25f ? 0.03f : 0f), -0.55f), k);
        bob.localRotation = Quaternion.Slerp(bob.localRotation, Quaternion.Euler(bodyPitch, 0f, 0f), k);
        bob.localScale = Vector3.Lerp(bob.localScale, Vector3.one, k);
        // legs: angle about x (drawn up = -70) and splay about z
        float lx, lz;
        if (ph < 0.25f) { float u = ph / 0.25f; lx = Ease(-70f, 6f, u); lz = Ease(38f, 6f, u); }       // the kick
        else if (ph < 0.8f) { lx = 6f; lz = 6f; }                                                        // straight back, together
        else { float u = (ph - 0.8f) / 0.2f; lx = Ease(6f, -70f, u); lz = Ease(6f, 38f, u); }           // knees up, feet out
        if (legL != null && legR != null)
        {
            legL.localRotation = Quaternion.Slerp(legL.localRotation, Quaternion.Euler(lx, 0f, -lz), k);
            legR.localRotation = Quaternion.Slerp(legR.localRotation, Quaternion.Euler(lx, 0f, lz), k);
        }
        // arms: forward = -160 about x, swept out / back = -40 with a wide splay, tucked = -95
        float ax, az;
        if (ph < 0.25f) { float u = ph / 0.25f; ax = Ease(-95f, -160f, u); az = Ease(25f, 8f, u); }
        else if (ph < 0.5f) { ax = -160f; az = 8f; }
        else if (ph < 0.8f) { float u = (ph - 0.5f) / 0.3f; ax = Ease(-160f, -45f, u); az = Ease(8f, 70f, u); }
        else { float u = (ph - 0.8f) / 0.2f; ax = Ease(-45f, -95f, u); az = Ease(70f, 25f, u); }
        if (armL != null && armR != null)
        {
            armL.localRotation = Quaternion.Slerp(armL.localRotation, Quaternion.Euler(ax, 0f, -az), k);
            armR.localRotation = Quaternion.Slerp(armR.localRotation, Quaternion.Euler(ax, 0f, az), k);
        }
        if (head != null) head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(-48f, 0f, 0f), k);   // look ahead, not at the bottom
    }
}
