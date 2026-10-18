using UnityEngine;

// ffu38: a wading mech really disturbs the pond (Core/WaveSim.cs through Ripples.Press / Swell):
//  * each leg where it crosses the surface presses the water (foam contact line round the leg, a little bow wave when it
//    swings forward, spray flicked off a fast-moving leg)
//  * once the water is over the hips the body itself ploughs: a ring of waves round it, a bow wave + V wake when it walks
//    (strength ~ speed x how much of it is in the water), only a gentle breathing ring when it stands still
//  * big slow swell rings from the body while it trudges (and from every footfall, Core/Ripples.Footfall), sized by H
// Scales with the mech's height, so the 10-story (12 m) to the trillion-story behave alike, bigger and slower.
public partial class StoryMech
{
    Vector3 mwLeg0, mwLeg1; bool mwLegOk0, mwLegOk1;
    void MechWater(Vector3 to, Vector3 vel, float H, float dt)
    {
        if (Mathf.Abs(to.x) > Layout.Half || Mathf.Abs(to.z) > Layout.Half || !Layout.InPond(to.x, to.z) || dt <= 0f) { mwLegOk0 = mwLegOk1 = false; return; }
        float wy = Ripples.SurfaceY;
        if (to.y > wy + 0.05f || to.y + H * 0.95f < wy) { mwLegOk0 = mwLegOk1 = false; return; }   // dry, or all under
        int id = GetHashCode() * 4;
        vel.y = 0f;
        if (vel.sqrMagnitude > 900f) vel = vel.normalized * 30f;
        float sp = vel.magnitude;
        float scale = Mathf.Sqrt(H / 12f);
        float breathe = 0.5f + 0.5f * Mathf.Sin(Time.time * (2.4f / scale) + id * 0.37f);
        float legRad = H * 0.055f;
        float footDeep = Mathf.Clamp01((wy - to.y) / (H * 0.25f));
        for (int i = 0; i < 2; i++)
        {
            Transform leg = i == 0 ? legL : legR;
            if (leg == null) continue;
            Vector3 hip = leg.position, dir = -leg.up;
            bool okLeg = hip.y > wy && dir.y < -0.2f;
            Vector3 c = Vector3.zero;
            if (okLeg)
            {
                float tt = (hip.y - wy) / -dir.y;
                okLeg = tt < HipY * 1.08f;   // the foot itself is under the surface
                c = hip + dir * tt; c.y = wy;
            }
            bool had = i == 0 ? mwLegOk0 : mwLegOk1;
            Vector3 prev = i == 0 ? mwLeg0 : mwLeg1;
            if (i == 0) { mwLegOk0 = okLeg; mwLeg0 = c; } else { mwLegOk1 = okLeg; mwLeg1 = c; }
            if (!okLeg) continue;
            Vector3 lv = had ? (c - prev) / dt : vel;
            if (lv.sqrMagnitude > 400f) lv = lv.normalized * 20f;
            float ls = lv.magnitude;
            float depth = Mathf.Min((0.02f + 0.016f * ls) * Mathf.Lerp(0.55f, 1f, footDeep) * scale * (0.8f + 0.2f * breathe), 0.3f);
            float foam = Mathf.Clamp01(0.4f + ls * 0.08f) * Mathf.Lerp(0.7f, 1f, footDeep);
            Ripples.Press(id + i, c, lv, legRad * 1.1f, depth, foam, legRad * 1.3f, 0.55f, 1f);   // ffu39: legs on the bed churn silt
            // spray flicked off a leg swinging through the water
            if (ls > 1.2f && Random.value < dt * Mathf.Min(ls * 3f, 14f) * (Look.Mobile ? 0.5f : 1f))
            {
                Vector3 f = ls > 0.01f ? lv / ls : Vector3.zero;
                Vector3 o = c + f * legRad * 1.1f + Vector3.up * 0.05f;
                FX.Spray(o, f * ls * 0.45f + Vector3.up * (1.2f + ls * 0.55f) * Mathf.Min(scale, 2.5f) + Random.insideUnitSphere * 0.6f,
                         Random.Range(0.18f, 0.35f) * scale, Random.Range(0.6f, 1f), new Color(0.88f, 0.94f, 1f, 0.75f));
            }
        }
        // the body ploughing once the water is over the hips (and the head is still out)
        float hipsY = hips != null ? hips.position.y : to.y + H * 0.42f;
        if (wy > hipsY - H * 0.06f && to.y + H * 0.9f > wy)
        {
            float bodyR = H * 0.17f;
            float subK = Mathf.Clamp01((wy - hipsY) / (H * 0.2f) + 0.35f);
            float depth = Mathf.Min((0.025f + 0.02f * sp) * subK * scale * (0.7f + 0.3f * breathe), 0.6f);
            Vector3 c = new Vector3(to.x, wy, to.z);
            Ripples.Press(id + 2, c, vel, bodyR, depth, Mathf.Clamp01(0.45f + sp * 0.08f) * subK, bodyR * 1.08f, 0.45f, 0.35f);
            if (sp > 0.8f) Ripples.Swell(c, Mathf.Clamp(H * 0.9f, 7f, 70f), Mathf.Clamp(0.03f + H * 0.004f, 0.06f, 0.5f) * Mathf.Clamp01(sp / 4f) * subK, bodyR);
            // bow spray at the chest
            if (sp > 1.6f && Random.value < dt * Mathf.Min(sp * 2.5f, 12f) * (Look.Mobile ? 0.5f : 1f))
            {
                Vector3 f = vel / sp, side = new Vector3(f.z, 0f, -f.x) * Random.Range(-1f, 1f);
                FX.Spray(c + f * bodyR * 1.05f + side * bodyR * 0.6f + Vector3.up * 0.1f, (f * 0.6f + side * 0.5f) * sp + Vector3.up * (1.5f + sp * 0.5f) * Mathf.Min(scale, 2.5f),
                         Random.Range(0.3f, 0.6f) * scale, Random.Range(0.7f, 1.2f), new Color(0.88f, 0.94f, 1f, 0.7f));
            }
        }
    }
}
