using UnityEngine;

// ffu37: water physics for anything with a rigidbody that ends up in the ranch pond (trucks, the tank, the Ripsaw, the
// monster truck, the Optimus mech suit, motorbikes, RC cars, a Cybertruck that is NOT in Cyberboat mode, the riding
// mower). Before ffu37 nothing here knew about water: the terrain under the pond was a 2-4 m deep dish (a different,
// shallow "pond floor" from the submarine's 15-27 m seabed), so vehicles drove along it at full speed and full gravity.
// Now the bed is the submarine's seabed (Layout.PondBedY) and, while submerged:
//   * buoyancy carries 72% of the weight (Buoy) -> they sink, but slowly (terminal ~2.9 m/s with the drag below)
//   * linear + quadratic water drag on every axis (a hard entry is braked in about a second) + angular drag
//   * GroundVehicle drive force x0.5 on the bed -> ~5 m/s crawl, enough to climb the 30-45 deg drop-off back up
//     (buoyancy makes the slope ~4x easier than on land)
//   * splash + spray on entry / exit, bubbles while under, a toast for a human driver
//   * recovery: an EMPTY vehicle left under water for 45 s, or a driven one that can't make progress for 12 s with the
//     throttle on, is towed back to its parking spot (Vehicle.ResetHome) with a toast.
public static class PondPhysics
{
    public const float Buoy = 0.72f;          // fraction of the weight the water carries when fully submerged
    public const float DragLin = 0.6f, DragQuad = 0.12f;

    // true for vehicles this applies to (others handle water themselves or never touch it)
    static bool Applies(Vehicle v)
    {
        if (v.rb == null || v.rb.isKinematic || v.flyer || v.stowed) return false;
        if (v is Boat || v is Horse || v is StoryMech || v is Jet) return false;
        return true;
    }

    // submerged fraction 0..1 of the body box in the ranch pond (0 anywhere else)
    public static float Submerged(Vehicle v, out float top, out float bottom)
    {
        Vector3 p = v.rb.position;
        top = p.y + 1f; bottom = p.y - 1f;
        if (Mathf.Abs(p.x) > Layout.Half || Mathf.Abs(p.z) > Layout.Half || !Layout.InPond(p.x, p.z)) return 0f;
        if (v.body != null) { Bounds b = v.body.bounds; top = b.max.y; bottom = b.min.y; }
        float h = Mathf.Max(0.5f, top - bottom);
        return Mathf.Clamp01((Ripples.SurfaceY - bottom) / h);
    }

    // ffu38: anything with a rigidbody that floats / wades / sinks through the ranch pond surface presses the wave sim
    // (Core/WaveSim.cs): one press for a round body, two (bow + stern) for a long one, so it leaves a bow wave and a V
    // wake; strength ~ speed x how much of the hull is at the waterline, and it fades once the whole thing is under.
    static void Wake(Vehicle v)
    {
        if (v.rb == null || v.stowed || v is StoryMech || !WaveSim.Active) return;
        Vector3 p = v.rb.position;
        if (Mathf.Abs(p.x) > Layout.Half || Mathf.Abs(p.z) > Layout.Half || !Layout.InPond(p.x, p.z)) return;
        float wy = Ripples.SurfaceY;
        Bounds b = v.body != null ? v.body.bounds : new Bounds(p, new Vector3(2f, 2f, 2f));
        if (b.min.y > wy + 0.05f || b.max.y < wy - 0.25f) return;   // clear of the water / all under
        if (v.flyer && b.min.y > wy - 0.3f && v.rb.velocity.y > 1f) return;
        float hk = Mathf.Clamp01((wy - b.min.y) / 0.35f) * Mathf.Clamp01((b.max.y - wy) / 0.3f + 0.25f);
        Vector3 vel = v.rb.velocity; vel.y = 0f;
        float sp = Mathf.Min(vel.magnitude, 25f);
        Vector3 f = v.transform.forward; f.y = 0f; if (f.sqrMagnitude < 1e-4f) f = Vector3.forward; f.Normalize();
        Vector3 rt = new Vector3(f.z, 0f, -f.x);
        float halfL = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(f.x), 0f, Mathf.Abs(f.z))));
        float halfW = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(rt.x), 0f, Mathf.Abs(rt.z))));
        float r = Mathf.Clamp(halfW, 0.35f, 4f);
        float depth = Mathf.Min((0.04f + 0.03f * sp) * hk * Mathf.Sqrt(r), 0.5f);
        float foam = Mathf.Clamp01(0.35f + sp * 0.06f) * hk;
        int id = v.GetInstanceID() * 2;
        Vector3 c = new Vector3(b.center.x, wy, b.center.z);
        if (halfL > halfW * 1.4f)
        {
            float o = halfL - r * 0.8f;
            Ripples.Press(id, c + f * o, vel, r, depth, foam, r * 1.15f, 0.5f);
            Ripples.Press(id + 1, c - f * o, vel, r, depth * 0.8f, foam * 0.9f, r * 1.15f, 0.5f);
        }
        else Ripples.Press(id, c, vel, r, depth, foam, r * 1.15f, 0.5f);
    }

    public static void Step(Vehicle v, float dt)
    {
        Wake(v);   // ffu38: hulls / bodies crossing the surface push real waves + foam (boats and the Cyberboat too)
        if (!Applies(v)) { v.pondSub = 0f; return; }
        var gv = v as GroundVehicle;
        bool boatMode = gv != null && gv.amph != null && gv.amph.k > 0.02f;
        float top, bottom;
        float sub = Submerged(v, out top, out bottom);
        float was = v.pondSub;
        v.pondSub = sub;
        Rigidbody rb = v.rb;
        Vector3 vel = rb.velocity;
        Vector3 p = rb.position;
        if (sub > 0f)
        {
            if (!boatMode)
            {
                rb.AddForce(Vector3.up * 9.81f * Buoy * sub, ForceMode.Acceleration);
                float sp = vel.magnitude;
                rb.AddForce(-vel * (DragLin + DragQuad * sp) * sub, ForceMode.Acceleration);
                rb.angularVelocity *= Mathf.Clamp01(1f - 2.2f * sub * dt);
            }
            else if (top < Layout.WaterY)
            {
                // Cyberboat rising from the bed: its own floats lift it; water drag keeps the climb believable
                float sp = vel.magnitude;
                rb.AddForce(-vel * (DragLin + DragQuad * sp), ForceMode.Acceleration);
            }
        }
        // splash in / out
        Vector3 wl = new Vector3(p.x, Layout.WaterY + 0.1f, p.z);
        if (was < 0.05f && sub >= 0.05f && (vel.y < -1f || vel.magnitude > 3f))
        {
            float e = Mathf.Clamp01((-vel.y + vel.magnitude * 0.5f) / 14f);
            FX.Splash(wl, 14 + e * 16f);
            for (int i = 0; i < 10 + (int)(e * 24f); i++)
            {
                Vector2 r = Random.insideUnitCircle * 1.8f;
                FX.Spray(wl + new Vector3(r.x, 0f, r.y), new Vector3(r.x * 1.8f, Random.Range(3f, 6f + e * 8f), r.y * 1.8f), Random.Range(0.3f, 0.7f), Random.Range(0.8f, 1.5f), new Color(0.9f, 0.96f, 1f, 0.75f));
            }
            Sfx.PlayAt(Sfx.SplashBig, p, 0.5f + e * 0.5f, 90f, Random.Range(0.75f, 0.9f));
            Debug.Log("PondPhysics: " + v.Title + " into the pond at (" + p.x.ToString("0") + ", " + p.z.ToString("0") + ") v " + vel.magnitude.ToString("0.0") + ", bed " + (Ranch.GY(p.x, p.z) - Layout.WaterY).ToString("0.0") + " m");
        }
        else if (was > 0.05f && sub <= 0.05f && vel.magnitude > 2f) FX.Splash(wl, 10);
        else if (sub > 0.05f && sub < 0.95f && vel.magnitude > 1.5f && Random.value < dt * 10f) FX.Splash(wl + Random.insideUnitSphere * 1.2f, 3);
        // bubbles while under (the air coming out of the cab), more while it's still sinking
        if (sub > 0.6f && top < Layout.WaterY + 0.2f && Random.value < dt * (5f + Mathf.Max(0f, -vel.y) * 3f))
            FX.Bubble(new Vector3(p.x, top, p.z) + Random.insideUnitSphere * 0.6f, 2 + Random.Range(0, 3));
        // toast + recovery
        if (sub > 0.6f && !boatMode)
        {
            v.pondUnderT += dt;
            Frog d = v.driver;
            if (d != null && d.human && !v.pondToasted)
            {
                v.pondToasted = true;
                string boat = gv != null && gv.amph != null ? "  (" + (d.inputKind == InputKind.Gamepad ? "Y" : d.inputKind == InputKind.Touch ? "BOAT" : "F") + " = Cyberboat mode)" : "";
                d.Toast("Glug glug - sinking to the pond bed! Crawl out up the bank" + boat, 4f);
            }
            bool tryingToMove = d != null && gv != null && Mathf.Abs(gv.ThrottleRaw) > 0.3f;
            v.pondStuckT = tryingToMove && new Vector2(vel.x, vel.z).magnitude < 0.6f ? v.pondStuckT + dt : Mathf.Max(0f, v.pondStuckT - dt * 2f);
            bool abandoned = d == null && v.pondUnderT > 45f;
            if (abandoned || v.pondStuckT > 12f)
            {
                Debug.Log("PondPhysics: " + v.Title + (abandoned ? " left under water 45 s" : " stuck on the pond bed 12 s") + " - towed back to its spot");
                if (d != null && d.human) d.Toast("Stuck on the pond bed - the tow winch pulls the " + v.Title + " back to its spot", 3.5f);
                FX.Splash(wl, 20);
                v.pondUnderT = 0f; v.pondStuckT = 0f; v.pondSub = 0f;
                v.ResetHome();
            }
        }
        else { v.pondUnderT = 0f; v.pondStuckT = 0f; if (sub <= 0.01f) v.pondToasted = false; }
    }
}
