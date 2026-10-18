using UnityEngine;

// ffu37: THE BREACH (Hunt for Red October style). The underwater world IS the ranch pond seen from below (its seabed
// maps 1:1 onto the pond bed, Layout.SubSeaY / PondBedY), so a sub that comes up through the surface fast enough
// (Submarine.BreachMin, only reachable with the emergency blow from deep down) keeps its momentum: it is carried over
// to the same spot of the ranch pond, launches out of the water (height = v^2 / 2g, so it scales with the ascent speed:
// ~2 m clear at 6 m/s, ~6-7 m at full blow), big splash + spray column, arcs with its nose following the path, falls
// back (second, wider splash, camera shake + pad rumble), sinks to ~3.5 m and goes back down to the underwater world
// at that depth with normal controls. Gentle surfacing (< 6 m/s) works exactly as before.
public partial class Submarine
{
    public const float BreachMin = 6f;          // m/s straight up at the surface
    float blowT, blowK;
    bool breach, bOut, bAbove, bReentered;
    float bT, bAfterT, bV0, bMaxH, bMaxBottom;
    public bool Breaching { get { return breach; } }
    public float BlowK { get { return blowK; } }
    // demo / log
    public static int Breaches;
    public static float LastMaxH = -99f, LastMaxBottom = -99f, LastV0;
    public static Vector3 LastSpot;

    const float HalfH = 1.3f;   // hull half height (capsule 2.6 m)

    void StartBreach()
    {
        Vector3 rel = transform.position - Worlds.UnderO;
        bool ok;
        Vector3 spot = UnderwaterWorld.PondSpot(rel, 7f, out ok);
        spot.y = Layout.WaterY + rel.y;
        Vector3 v = rb.velocity;
        breach = true; bOut = bAbove = bReentered = false; bT = bAfterT = 0f;
        bV0 = v.y; bMaxH = bMaxBottom = -99f;
        surfaceT = 0f;
        if (driver != null) driver.world = WorldId.Ranch;   // the camera / fog / music follow (Game.TrackWorlds)
        rb.position = spot; transform.position = spot;
        rb.velocity = v;
        LastSpot = spot; LastV0 = v.y; Breaches++;
        Debug.Log("Submarine: BREACH v=" + v.y.ToString("0.0") + " m/s from the reef (" + rel.x.ToString("0") + ", " + rel.z.ToString("0") + ") -> pond (" + spot.x.ToString("0") + ", " + spot.z.ToString("0") + ")" + (ok ? "" : " (fallback spot)") + ", expect ~" + (v.y * v.y / 19.62f).ToString("0.0") + " m");
    }

    void BreachStep(float dt)
    {
        bT += dt;
        Vector3 p = rb.position, v = rb.velocity;
        float wy = Layout.WaterY;
        float k = Mathf.Clamp01((wy - (p.y - HalfH)) / (2f * HalfH));   // submerged fraction of the hull
        // out of the water: gravity; in it: neutrally buoyant + drag (stronger once it's back in, so it settles)
        v.y -= 9.81f * (1f - k) * dt;
        if (k > 0f)
        {
            float sp = v.magnitude;
            float drag = (bReentered ? 0.9f + 0.1f * sp : 0.12f + 0.02f * sp) * k;
            v -= v * Mathf.Clamp01(drag * dt);
        }
        bool on = driver != null;
        if (on) yaw += inp.move.x * 30f * dt;
        if (on && k > 0.5f)
        {
            Vector3 fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 hv = Vector3.MoveTowards(new Vector3(v.x, 0f, v.z), fwd * inp.move.y * 8f, 3f * dt);
            v.x = hv.x; v.z = hv.z;
        }
        rb.velocity = v;
        // nose follows the path: up on the way out, bow drops as it falls back
        float hs = new Vector2(v.x, v.z).magnitude;
        float pitchT = Mathf.Clamp(-Mathf.Atan2(v.y, Mathf.Max(hs, 3f)) * Mathf.Rad2Deg, -62f, 38f);
        if (bReentered) pitchT *= 0.4f;
        pitchVis = Mathf.MoveTowardsAngle(pitchVis, pitchT, 70f * dt);
        rb.MoveRotation(Quaternion.Euler(pitchVis, yaw, 0f));

        Vector3 wl = new Vector3(p.x, wy + 0.1f, p.z);
        // 1st splash: the hull bursts through the surface
        if (!bOut && p.y > wy - 0.3f)
        {
            bOut = true;
            float e = Mathf.Clamp01((v.y - 4f) / 7f);
            SplashBurst(wl, e, false);
            if (driver != null && driver.human) driver.Toast("BREACH!", 2f);
        }
        if (p.y - HalfH > wy) bAbove = true;
        bMaxH = Mathf.Max(bMaxH, p.y - wy);
        bMaxBottom = Mathf.Max(bMaxBottom, p.y - HalfH - wy);
        // 2nd splash: falling back in
        if (bOut && !bReentered && v.y < -1f && p.y - HalfH < wy + 0.2f && bT > 0.25f)
        {
            bReentered = true;
            float e = Mathf.Clamp01((-v.y - 2f) / 9f);
            SplashBurst(wl, Mathf.Max(e, 0.35f), true);
            if (Game.I != null)
            {
                Game.I.Quake(p, 0.35f + e * 0.45f, 70f);
                if (driver != null) Game.I.PilotKick(driver, 0.6f + e * 0.8f, true);
            }
            Debug.Log("Submarine: re-entry v=" + v.y.ToString("0.0") + " m/s, max height above water: centre " + bMaxH.ToString("0.00") + " m, hull bottom " + bMaxBottom.ToString("0.00") + " m");
        }
        // trailing spray while flying, wake foam while wallowing
        if (bOut && !bReentered && p.y - HalfH > wy && Random.value < 0.7f)
            FX.Spray(transform.TransformPoint(new Vector3(Random.Range(-1f, 1f), -1.1f, Random.Range(-3f, 3f))), v * 0.4f + Random.insideUnitSphere * 1.5f, Random.Range(0.25f, 0.5f), Random.Range(0.5f, 0.9f), new Color(0.9f, 0.96f, 1f, 0.7f));
        if (k > 0.05f && k < 0.95f && Random.value < 0.4f) FX.Splash(wl + Random.insideUnitSphere * 2f, 3);
        if (k > 0.6f && Random.value < 0.5f) FX.Bubble(p + Random.insideUnitSphere * 1.5f, 3);
        if (bReentered) bAfterT += dt;
        if ((bReentered && (p.y < wy - 3.5f || bAfterT > 3f)) || bT > 10f) EndBreach();
    }

    void SplashBurst(Vector3 wl, float e, bool reentry)
    {
        float big = reentry ? 1.35f : 1f;
        FX.Splash(wl, 30);
        FX.Splash(wl + transform.forward * 2.5f, 26);
        FX.Splash(wl - transform.forward * 2.5f, 26);
        int col = (int)((40 + e * 70) * big);
        for (int i = 0; i < col; i++)
        {
            // a column of spray straight up around the hull + an outward ring
            Vector2 r = Random.insideUnitCircle;
            Vector3 o = transform.forward * Random.Range(-3.4f, 3.4f) + new Vector3(r.x, 0f, r.y) * 1.6f;
            bool ring = i % 3 == 0;
            Vector3 vel = ring ? new Vector3(r.x, 0f, r.y).normalized * Random.Range(4f, 8f + e * 6f) * big + Vector3.up * Random.Range(2f, 5f)
                               : new Vector3(r.x * 1.5f, Random.Range(5f, 9f + e * 9f) * big, r.y * 1.5f);
            FX.Spray(wl + o, vel, Random.Range(0.45f, 1.1f + e * 0.6f) * big, Random.Range(1.0f, 1.9f), new Color(0.92f, 0.97f, 1f, 0.85f));
        }
        if (Sfx.SplashBig != null)
        {
            if (driver != null && driver.human) Sfx.Play(Sfx.SplashBig, 1f, reentry ? 0.62f : 0.78f);
            else Sfx.PlayAt(Sfx.SplashBig, wl, 1f, 140f, reentry ? 0.62f : 0.78f);
        }
    }

    void EndBreach()
    {
        Vector3 p = rb.position, v = rb.velocity;
        float x = Mathf.Clamp(p.x - Layout.PondC.x, -85f, 85f), z = Mathf.Clamp(p.z - Layout.PondC.y, -85f, 85f);
        float y = Mathf.Clamp(p.y - Layout.WaterY, Layout.SubSeaY(x, z) + 2.5f, -2.8f);
        Vector3 under = Worlds.UnderO + new Vector3(x, y, z);
        LastMaxH = bMaxH; LastMaxBottom = bMaxBottom;
        breach = false; blowK = 0f; blowT = 0f; surfaceT = 0f;
        if (driver != null) driver.world = WorldId.Underwater;
        rb.position = under; transform.position = under;
        rb.velocity = new Vector3(v.x * 0.5f, Mathf.Min(v.y, 0f) * 0.6f, v.z * 0.5f);
        pitchVis *= 0.5f;
        Debug.Log("Submarine: breach over after " + bT.ToString("0.0") + " s - back under at depth " + (-y).ToString("0.0") + " m; max height above water: centre " + bMaxH.ToString("0.00") + " m, hull bottom " + bMaxBottom.ToString("0.00") + " m (v0 " + bV0.ToString("0.0") + " m/s)");
    }
}
