using UnityEngine;

// ffu31: the driver's side of towing + bike loading. Frog.Update calls DriverTick for a human driver every frame
// (before the exit check): near a hitch it shows "E / Y / tap: Hitch" and the press hitches instead of getting out;
// while towing the same button unhitches; on a bike at a toy hauler's ramp it loads / stows.
public static class TrailerTow
{
    // trucks only: the Cybertrucks, the Monster Truck, and any later "semi" / "pickup" / "truck" ground vehicle
    public static bool CanTow(Vehicle v)
    {
        GroundVehicle gv = v as GroundVehicle;
        if (gv == null || gv is StoryMech || gv is Tank || gv is RidingMower || gv.walker || gv.tracked || v.body == null) return false;
        if (Trailer.Loadable(v)) return false;
        string t = v.Title.ToLowerInvariant();
        return t.Contains("truck") || t.Contains("semi") || t.Contains("pickup") || t.Contains("hauler") || t.Contains("tesla");
    }

    public static Trailer TowedBy(Vehicle v)
    {
        foreach (var t in Trailer.All) if (t != null && t.tower == v && t.Hitched) return t;
        return null;
    }

    public const string Btn = "E / Y / tap";

    public static bool DriverTick(Frog f, PIn input)
    {
        Vehicle v = f.vehicle;
        if (v == null || Trailer.All.Count == 0 || f.world != WorldId.Ranch) return false;
        bool press = input.use || input.land;
        Trailer towing = TowedBy(v);
        if (towing != null)
        {
            f.prompt = "Towing the " + towing.spec.shortName + "  |  " + Btn + ": Unhitch  |  RT gas  LT brake/reverse";
            if (press)
            {
                towing.Detach("unhitched", false);
                f.Toast(towing.spec.shortName + " unhitched - press again to get out", 2.2f);
                return true;
            }
            return false;
        }
        if (CanTow(v))
        {
            Trailer best = null; float bd = 1e9f, d;
            Trailer near = null; float nd = 6f;
            foreach (var t in Trailer.All)
            {
                if (t == null) continue;
                if (t.CanHitch(v, out d)) { if (d < bd) { bd = d; best = t; } }
                else if (!t.Hitched)
                {
                    Vector3 dd = Trailer.HitchWorld(v) - t.BallWorld; dd.y = 0f;
                    if (dd.magnitude < nd) { nd = dd.magnitude; near = t; }
                }
            }
            if (best != null)
            {
                f.prompt = Btn + ": Hitch the " + best.spec.shortName;
                if (press)
                {
                    best.Hitch(v);
                    f.Toast("Hitched! Towing the " + best.spec.shortName + " - " + Btn + " unhitches", 2.6f);
                    return true;
                }
                return false;
            }
            if (near != null && f.prompt == v.HelpLine) f.prompt = "Back the hitch up to the " + near.spec.shortName + "'s tongue (" + nd.ToString("0.0") + " m)  |  " + v.HelpLine;
            return false;
        }
        if (Trailer.Loadable(v) && !v.stowed)
        {
            foreach (var t in Trailer.All)
            {
                if (t == null || !t.spec.hauler || t.move != null) continue;
                if (t.InBay(v.transform.position))
                {
                    if (!t.HasRoomFor(v)) { f.prompt = "The " + t.spec.shortName + " is full"; return false; }
                    if (v.Speed > 1f) { f.prompt = "Stop inside to lock the " + v.Title + " in  |  " + Btn + ": Stow"; return false; }
                    f.prompt = Btn + ": Stow the " + v.Title + " (locks it in)";
                    if (press) { t.StowParked(v); return true; }
                    return false;
                }
                if (t.AtRampFoot(v) && t.HasRoomFor(v) && (t.RampDown || t.CanMoveRamp))
                {
                    f.prompt = Btn + ": Load into the " + t.spec.shortName + (t.RampDown ? "  (or ride up the ramp)" : "");
                    if (press && t.StartLoad(v, f)) return true;
                    return false;
                }
            }
        }
        return false;
    }
}
