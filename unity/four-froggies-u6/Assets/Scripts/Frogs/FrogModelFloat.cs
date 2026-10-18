using UnityEngine;

// ffu29 pool float pose (Frog.PoolTick lays the whole model on its back at the water surface; this poses the limbs):
// arms sprawled out to the sides with a lazy alternating backstroke windmill that grows with drift speed, legs splayed
// with a slow flutter kick, head looking about. Works for the frog mesh, the primitive frog and the cats / dogs.
public partial class FrogModel
{
    public void AnimateFloat(float strokePhase, float move, float t)
    {
        if (bob == null) return;
        bob.localPosition = Vector3.zero;
        bob.localScale = new Vector3(1f, 1f + Mathf.Sin(t * 1.1f) * 0.015f, 1f);   // slow breathing
        bob.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.7f) * 1.5f, 0f, 0f);
        float k = Mathf.Lerp(0.25f, 1f, move);   // even idle, the hands paddle a little
        float a = strokePhase * 360f;
        if (armL != null && armR != null)
        {
            Quaternion idleL = Quaternion.Euler(-25f + Mathf.Sin(t * 1.4f) * 12f, 0f, -72f);
            Quaternion idleR = Quaternion.Euler(-25f + Mathf.Sin(t * 1.4f + 2f) * 12f, 0f, 72f);
            Quaternion strL = Quaternion.Euler(0f, 0f, -38f) * Quaternion.Euler(a, 0f, 0f);
            Quaternion strR = Quaternion.Euler(0f, 0f, 38f) * Quaternion.Euler(a + 180f, 0f, 0f);
            armL.localRotation = Quaternion.Slerp(idleL, strL, k * 0.85f);
            armR.localRotation = Quaternion.Slerp(idleR, strR, k * 0.85f);
        }
        if (legL != null && legR != null)
        {
            float kick = Mathf.Sin(t * (2.2f + move * 2.5f)) * (8f + move * 10f);
            legL.localRotation = Quaternion.Euler(12f + kick, 0f, -28f);
            legR.localRotation = Quaternion.Euler(12f - kick, 0f, 28f);
        }
        if (head != null) head.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.5f) * 6f - 8f, Mathf.Sin(t * 0.33f) * 14f, 0f);
    }
}
