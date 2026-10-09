using System.Collections.Generic;
using UnityEngine;

// Each froggy lives in one "world" at a time. The ranch is the terrain at the origin; the other worlds
// (house interior, underwater, space, Mars, Callisto) are hidden roots built on demand far away from it,
// so every split-screen view simply follows its own frog wherever it is. Each camera gets its world's
// sky / fog / ambient just before it renders (Camera.onPreCull).
public enum WorldId { Ranch, House, Underwater, Space, Mars, Callisto }

public static class Worlds
{
    // staged rollout switches (stage B = house, C = underwater, D = space, E = mechs/robots/animals)
    public static bool UnderwaterOn = true, SpaceOn = true, StageEOn = true;

    public static readonly Vector3 HouseO = new Vector3(0f, 0f, 1400f);
    public static readonly Vector3 UnderO = new Vector3(1400f, 0f, 0f);
    public static readonly Vector3 SpaceO = new Vector3(0f, 0f, -30000f);
    public static readonly Vector3 MarsO = new Vector3(-1600f, 0f, 0f);
    public static readonly Vector3 CallistoO = new Vector3(-1600f, 0f, 1600f);

    static readonly Dictionary<Camera, WorldId> camWorld = new Dictionary<Camera, WorldId>();
    static bool hooked;
    static Material ranchSky;
    public static Material spaceSky;    // set by the space world (null = solid black)
    static Color fog0, amb0, amb1, amb2;
    static float fogD0, exp0 = -1f;

    public static void Init()
    {
        if (hooked) return;
        hooked = true;
        ranchSky = RenderSettings.skybox;
        fog0 = RenderSettings.fogColor; fogD0 = RenderSettings.fogDensity;
        amb0 = RenderSettings.ambientSkyColor; amb1 = RenderSettings.ambientEquatorColor; amb2 = RenderSettings.ambientGroundColor;
        if (RenderSettings.sun != null) sunRot0 = RenderSettings.sun.transform.rotation;
        if (ranchSky != null && ranchSky.HasProperty("_Exposure")) exp0 = ranchSky.GetFloat("_Exposure");
        Camera.onPreCull += PreCull;
    }

    public static void SetCamera(Camera c, WorldId w)
    {
        if (c == null) return;
        camWorld[c] = w;
        switch (w)
        {
            case WorldId.Space:
                c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = Color.black; c.farClipPlane = 12000f; c.nearClipPlane = 0.5f; break;
            case WorldId.Underwater:
                c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.05f, 0.28f, 0.38f); c.farClipPlane = 260f; c.nearClipPlane = 0.2f; break;
            case WorldId.House:
                c.clearFlags = CameraClearFlags.Skybox; c.farClipPlane = 400f; c.nearClipPlane = 0.15f; break;
            case WorldId.Mars:
            case WorldId.Callisto:
                c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = w == WorldId.Mars ? new Color(0.55f, 0.36f, 0.25f) : new Color(0.02f, 0.02f, 0.04f); c.farClipPlane = 900f; c.nearClipPlane = 0.25f; break;
            default:
                c.clearFlags = CameraClearFlags.Skybox; c.farClipPlane = 900f; c.nearClipPlane = 0.3f; break;
        }
    }

    static Quaternion sunRot0;
    static void PreCull(Camera c)
    {
        WorldId w;
        if (!camWorld.TryGetValue(c, out w)) w = WorldId.Ranch;
        if (w != WorldId.Space && RenderSettings.sun != null) RenderSettings.sun.transform.rotation = sunRot0;
        float launchDark = w == WorldId.Ranch ? LaunchSeq.SkyDark(c) : 0f;   // Starship climb: the sky darkens
        // ffu14: altitude (mech rockets, Starship climb, a high drone): thinner haze, far plane opens up to the horizon,
        // the sky fades to black towards the edge of space (~2.4 km here); the lobby turntables (layer 20) are skipped
        float altFog = 1f;
        if (w == WorldId.Ranch && c.cullingMask != (1 << 20))
        {
            float alt = Mathf.Max(0f, c.transform.position.y - 20f);
            launchDark = Mathf.Max(launchDark, Mathf.Clamp01((alt - 400f) / 2000f));
            altFog = 1f / (1f + alt / 220f);
            c.farClipPlane = Mathf.Min(26000f, 900f + alt * 9f);
            c.nearClipPlane = 0.3f + alt * 0.002f;
        }
        if (exp0 >= 0f && ranchSky != null) ranchSky.SetFloat("_Exposure", Mathf.Lerp(exp0, exp0 * 0.06f, launchDark));
        switch (w)
        {
            case WorldId.Underwater:
                RenderSettings.fog = true; RenderSettings.fogColor = new Color(0.06f, 0.32f, 0.42f); RenderSettings.fogDensity = 0.022f;
                RenderSettings.ambientSkyColor = new Color(0.35f, 0.6f, 0.75f); RenderSettings.ambientEquatorColor = new Color(0.2f, 0.42f, 0.5f); RenderSettings.ambientGroundColor = new Color(0.1f, 0.2f, 0.25f);
                break;
            case WorldId.Space:
                if (SpaceWorld.I != null) SpaceWorld.I.PreCull(c);
                RenderSettings.fog = false;
                // ffu10: brighter fill so the Starship hull / night sides never go pure black when backlit by the Sun
                RenderSettings.ambientSkyColor = new Color(0.26f, 0.27f, 0.32f); RenderSettings.ambientEquatorColor = new Color(0.2f, 0.2f, 0.24f); RenderSettings.ambientGroundColor = new Color(0.1f, 0.1f, 0.12f);
                break;
            case WorldId.Mars:
                if (SurfaceWorlds.InCave(c.transform.position))
                {
                    // inside the cave: dark, teal / violet glow
                    RenderSettings.fog = true; RenderSettings.fogColor = new Color(0.05f, 0.04f, 0.06f); RenderSettings.fogDensity = 0.03f;
                    RenderSettings.ambientSkyColor = new Color(0.12f, 0.3f, 0.32f); RenderSettings.ambientEquatorColor = new Color(0.2f, 0.12f, 0.3f); RenderSettings.ambientGroundColor = new Color(0.05f, 0.05f, 0.06f);
                    break;
                }
                RenderSettings.fog = true; RenderSettings.fogColor = new Color(0.62f, 0.42f, 0.3f); RenderSettings.fogDensity = MarsFog;
                RenderSettings.ambientSkyColor = new Color(0.7f, 0.5f, 0.38f); RenderSettings.ambientEquatorColor = new Color(0.5f, 0.34f, 0.25f); RenderSettings.ambientGroundColor = new Color(0.25f, 0.15f, 0.1f);
                break;
            case WorldId.Callisto:
                RenderSettings.fog = false;
                RenderSettings.ambientSkyColor = new Color(0.3f, 0.32f, 0.38f); RenderSettings.ambientEquatorColor = new Color(0.22f, 0.22f, 0.26f); RenderSettings.ambientGroundColor = new Color(0.1f, 0.1f, 0.12f);
                break;
            case WorldId.House:
                RenderSettings.fog = false;
                RenderSettings.ambientSkyColor = new Color(0.78f, 0.74f, 0.68f); RenderSettings.ambientEquatorColor = new Color(0.62f, 0.56f, 0.5f); RenderSettings.ambientGroundColor = new Color(0.35f, 0.3f, 0.25f);
                break;
            default:
                RenderSettings.fog = true; RenderSettings.fogColor = Color.Lerp(fog0, new Color(0.02f, 0.03f, 0.08f), launchDark); RenderSettings.fogDensity = fogD0 * (1f - 0.7f * launchDark) * altFog;
                RenderSettings.ambientSkyColor = amb0; RenderSettings.ambientEquatorColor = amb1; RenderSettings.ambientGroundColor = amb2;
                break;
        }
    }

    public static float MarsFog = 0.006f;

    // ground height for camera clamps etc.; outside the ranch square there is no terrain
    public static float FloorUnder(Vector3 p)
    {
        if (Mathf.Abs(p.x) <= Layout.Half && Mathf.Abs(p.z) <= Layout.Half && p.y > -60f && p.y < 600f) return Ranch.GY(p.x, p.z);
        return -1e5f;
    }

    public static float KillY(WorldId w)
    {
        switch (w)
        {
            case WorldId.House: return HouseO.y - 10f;
            case WorldId.Underwater: return UnderO.y - 80f;
            case WorldId.Space: return -1e9f;
            case WorldId.Mars: return MarsO.y - 60f;
            case WorldId.Callisto: return CallistoO.y - 60f;
            default: return -30f;
        }
    }

    public static void Respawn(Frog f)
    {
        switch (f.world)
        {
            case WorldId.House: f.SendTo(WorldId.House, HouseWorld.Spawn(f.id), 180f); break;
            case WorldId.Mars: SurfaceWorlds.LandMars(f, f.id); break;
            case WorldId.Callisto: SurfaceWorlds.LandCallisto(f, f.id); break;
            case WorldId.Underwater: f.SendTo(WorldId.Underwater, UnderwaterWorld.I.sub.transform.position + Vector3.up * 3f, 0f); break;
            default: f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f); break;
        }
    }

    public static string Mood(WorldId w)
    {
        switch (w)
        {
            case WorldId.House: return "house";
            case WorldId.Underwater: return "underwater";
            case WorldId.Space: return "space";
            case WorldId.Mars: return "mars";
            case WorldId.Callisto: return "callisto";
            default: return "ranch";
        }
    }

    public static string Name(WorldId w)
    {
        switch (w)
        {
            case WorldId.House: return "James's house";
            case WorldId.Underwater: return "Underwater";
            case WorldId.Space: return "Space";
            case WorldId.Mars: return "Mars";
            case WorldId.Callisto: return "Callisto";
            default: return "Ranch";
        }
    }
}
