using UnityEngine;

// Each froggy lives in one "world" at a time. The ranch is the terrain at the origin; the other worlds
// (house interior, underwater, space, Mars, Callisto) are hidden roots built on demand far away from it,
// so every split-screen view simply follows its own frog wherever it is.
public enum WorldId { Ranch, House, Underwater, Space, Mars, Callisto }

public static class Worlds
{
    // ground height for camera clamps etc.; outside the ranch square there is no terrain
    public static float FloorUnder(Vector3 p)
    {
        if (Mathf.Abs(p.x) <= Layout.Half && Mathf.Abs(p.z) <= Layout.Half && p.y > -60f && p.y < 600f) return Ranch.GY(p.x, p.z);
        return -1e5f;
    }

    public static WorldId At(Vector3 p)
    {
        if (Mathf.Abs(p.x) <= Layout.Half + 60f && Mathf.Abs(p.z) <= Layout.Half + 60f && p.y > -100f && p.y < 900f) return WorldId.Ranch;
        return WorldId.Ranch;
    }

    public static string Mood(WorldId w)
    {
        switch (w)
        {
            case WorldId.House: return "house";
            case WorldId.Underwater: return "underwater";
            case WorldId.Space: case WorldId.Mars: case WorldId.Callisto: return "space";
            default: return "ranch";
        }
    }
}
