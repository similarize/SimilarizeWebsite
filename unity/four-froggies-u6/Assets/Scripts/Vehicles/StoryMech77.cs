using UnityEngine;

// ffu77 (Ben item 4, 2026-10-21 list): Superman flight on Earth. Pad B / key B toggles it in any story mech: the mech lifts
// off, lies level head first (the ffu15 space pose: arms forward, legs back, back rockets as the main engines) and flies
// fast. Stick: turn + speed, RT / Space climb, LT / Shift dive, LB / Shift afterburner (jet-class speed). Like the jets'
// hyper flight it goes AROUND THE WORLD: past Jet.WrapR it comes back round the other side of the planet. B again = land:
// (from beyond the ranch it heads home first), it descends, stands up in the air and touches down on its feet.
// Per mech, driven by its own pilot's pad, so split screen works. &ff77old=1 = before.
public static class F77
{
    static int old = -1;
    public static bool Old { get { if (old < 0) old = Ffu33.Url("ff77old") == "1" ? 1 : 0; return old == 1; } }
}

public partial class StoryMech
{
    public bool SuperOn77 { get; private set; }
    float superK, superAlt, superBank;
    bool superLandQ;
    Vector3 superC;   // body centre (the visual root swings round it)
    int superLaps;

    float SuperHalf { get { return height * 0.5f; } }
    static bool OnRanch77(Vector3 p, float m) { return Mathf.Abs(p.x) < Layout.Half - m && Mathf.Abs(p.z) < Layout.Half - m; }

    // called from Update after SendNet; true = this frame is a Superman frame (the normal walk / rocket code is skipped)
    bool SuperTick77(float dt)
    {
        if (F77.Old) return false;
        bool on = driver != null && !driver.netPuppet;
        if (!SuperOn77)
        {
            if (on && inp.cargo && !Away72) SuperStart77();
            if (!SuperOn77) return false;
        }
        else if (!on) { SuperEnd77(false); return false; }
        else if (inp.cargo) { superLandQ = !superLandQ; if (superLandQ) driver.Toast(OnRanch77(superC, height) ? "Landing..." : "Heading home to land on the ranch...", 2f); }

        float H = height;
        Vector3 c = superC;
        bool home = OnRanch77(c, H * 0.3f);
        float ground = home ? MechGround(c, c.y) : Ranch.GY(c.x, c.z);
        boostK = Mathf.MoveTowards(boostK, inp.boostHeld ? 1f : 0f, dt * 3f);
        float cruise = 120f + H * 1.2f;
        float top = Mathf.Lerp(cruise * 2.5f, 900f + H * 2f, boostK);
        float stick = Mathf.Clamp(inp.move.y, -1f, 1f);
        float want = stick >= 0f ? Mathf.Lerp(cruise, top, stick) : Mathf.Lerp(cruise, cruise * 0.3f, -stick);
        if (superLandQ) want = Mathf.Min(want, cruise * (home ? 0.4f : 1.5f));
        if (superK < 0.99f && !superLandQ) want *= superK;   // ease into the dive forward
        speed = Mathf.MoveTowards(speed, want, (want > speed ? 160f + 300f * boostK : 320f) * dt);
        turnVel = Mathf.MoveTowards(turnVel, inp.move.x * 55f, 220f * dt);
        if (superLandQ && !home)
        {
            float wantYaw = Mathf.Atan2(-c.x, -c.z) * Mathf.Rad2Deg;
            yaw = Mathf.MoveTowardsAngle(yaw, wantYaw, 45f * dt);
        }
        yaw += turnVel * dt;
        superBank = Mathf.MoveTowards(superBank, -turnVel * 0.7f, 90f * dt);

        // altitude: held like the jets' hyper flight; RT / Space climbs, LT / Shift dives; never below the treetops
        float minY = ground + (home ? H * 0.6f + 8f : 300f);
        superAlt += inp.climb * (40f + H * 0.4f + speed * 0.3f) * dt;
        if (superLandQ) superAlt = home ? ground + H * 0.5f + 2f : Mathf.Max(minY, superAlt - 80f * dt);
        else superAlt = Mathf.Max(superAlt, minY);
        float vyC = Mathf.Clamp((superAlt - c.y) * 1.5f, -(120f + H), 250f + H);
        vy = Mathf.MoveTowards(vy, vyC, (200f + H) * dt);
        Vector3 fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 nc = c + fwd * speed * dt + Vector3.up * vy * dt;
        // around the world (Jet.Hyper): out past WrapR the mech comes back round the other side of the planet
        Vector2 xz = new Vector2(nc.x, nc.z);
        if (xz.magnitude > Jet.WrapR && Vector2.Dot(xz, new Vector2(fwd.x, fwd.z)) > 0f)
        {
            Vector2 n = -xz.normalized * (Jet.WrapR - 400f);
            nc = new Vector3(n.x, nc.y, n.y);
            superLaps++;
            driver.Toast(superLaps == 1 ? "AROUND THE WORLD! The ranch is coming up ahead" : "AROUND THE WORLD x" + superLaps + "!", 3f);
            FX.Sparkle(nc + fwd * H, Color.white, 40);
            if (Game.I != null) foreach (Slot s in Game.I.slots) if (s != null && s.rig != null && s.frog >= 0 && s.frog < Game.I.frogs.Count && Game.I.frogs[s.frog] == driver) s.rig.Snap();
            Debug.Log("FF77 " + Title + " Superman lap " + superLaps);
        }
        kinVel = (nc - c) / Mathf.Max(dt, 1e-4f);
        superC = nc;
        Altitude = Mathf.Max(0f, nc.y - SuperHalf - ground);
        atSpawn = false;

        // pose: level (90 deg) when flying, nose up a little in a climb; back upright while it settles in to land
        bool settle = superLandQ && home && nc.y - superAlt < H * 0.3f + 4f && speed < cruise * 0.5f;
        superK = Mathf.MoveTowards(superK, settle ? 0f : 1f, dt * (settle ? 1.4f : 2.2f));
        float climbDeg = Mathf.Atan2(vy, Mathf.Max(20f, speed)) * Mathf.Rad2Deg;
        SuperPose77(dt);
        flameK = Mathf.MoveTowards(flameK, 1f + boostK, dt * 6f);
        Jets(dt);
        Quaternion r = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, superBank * superK) * Quaternion.Euler(Mathf.Clamp(90f - climbDeg * 0.6f, 40f, 100f) * superK, 0f, 0f);
        Vector3 root = nc - r * (Vector3.up * SuperHalf);
        rb.MovePosition(root); rb.MoveRotation(r);
        transform.position = root; transform.rotation = r;
        // ffbr66: a mech with a bridge lifts its head to look where it flies (level, tilted a little down), so the bridge, its
        // seated crew and the bridge camera face the flight instead of the ground; eases back with superK when it lands
        if (bridge73 != null && head != null && !F73.Old66) head.rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(F73.SuperTilt66 * superK, 0f, 0f);

        if (settle && superK <= 0.001f) { SuperEnd77(true); return true; }
        if (Altitude >= SpaceAlt && SpaceWorld.I != null) { SuperEnd77(false); ReachSpace(); }
        return true;
    }

    void SuperStart77()
    {
        Vector3 p = transform.position;
        SuperOn77 = true; superLandQ = false; superK = 0f; superBank = 0f; superLaps = 0;
        aimK = 0f; aimPointValid = false;
        superC = p + Vector3.up * SuperHalf;
        superAlt = superC.y + height * 0.8f + 20f;
        grounded = false; vy = Mathf.Max(vy, 10f);
        Sfx.PlayAt(Sfx.Pick(Sfx.Servo), p, 0.6f, 120f, 0.6f);
        for (int i = 0; i < 8 + band * 4; i++) FX.Dust(p + Random.insideUnitSphere * height * 0.2f, 1f);
        if (driver != null) driver.Toast("SUPERMAN FLIGHT!  Stick fly, " + (driver.inputKind == InputKind.Gamepad ? "RT up / LT down, LB faster, B land" : "Space up / Shift down + faster, B land") + " - go around the world!", 4f);
        Debug.Log("FF77 " + Title + " Superman flight on");
    }

    // landed = touched down from a landing; otherwise (pilot out / to space) the mech just stands up where it is and falls
    void SuperEnd77(bool landed)
    {
        if (!SuperOn77) return;
        SuperOn77 = false; superLandQ = false; superK = 0f;
        Vector3 feet = superC - Vector3.up * SuperHalf;
        if (!OnRanch77(feet, height * 0.2f))
        {
            // beyond the ranch: park it just inside the edge on the way home (the walk code keeps a mech on the ranch)
            float m = Layout.Half - height * 0.3f;
            feet.x = Mathf.Clamp(feet.x, -m, m); feet.z = Mathf.Clamp(feet.z, -m, m);
        }
        Quaternion r = Quaternion.Euler(0f, yaw, 0f);
        rb.position = feet; rb.rotation = r;
        transform.position = feet; transform.rotation = r;
        speed = landed ? 0f : speed * 0.2f; turnVel = 0f; vy = landed ? -4f : Mathf.Min(vy, 0f);
        grounded = false; flameK = landed ? 0f : flameK;
        Debug.Log("FF77 " + Title + " Superman flight off (" + (landed ? "landed" : "ended") + ")");
    }

    // arms reach forward (the space copy's pose), legs straight back, slight kick
    void SuperPose77(float dt)
    {
        float k = superK, s = Mathf.Sin(Time.time * 3f) * 4f;
        float t = dt * 5f;
        armR.localRotation = Quaternion.Slerp(armR.localRotation, Quaternion.Slerp(Quaternion.Euler(-20f, 0f, 25f), Quaternion.Euler(172f, 0f, -6f), k), t);
        armL.localRotation = Quaternion.Slerp(armL.localRotation, Quaternion.Slerp(Quaternion.Euler(-20f, 0f, -25f), Quaternion.Euler(165f, 0f, 9f), k), t);
        legL.localRotation = Quaternion.Slerp(legL.localRotation, Quaternion.Euler(s * k, 0f, 0f), t);
        legR.localRotation = Quaternion.Slerp(legR.localRotation, Quaternion.Euler(-s * k, 0f, 0f), t);
        torso.localRotation = Quaternion.Slerp(torso.localRotation, Quaternion.identity, t);
        hips.localPosition = new Vector3(0f, HipY, hips.localPosition.z);
    }
}
