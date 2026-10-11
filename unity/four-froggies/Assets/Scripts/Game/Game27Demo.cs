using UnityEngine;

// ffu27 demo / screenshot shots (?ffdemo=1&ffshot=...):
//   marscave   P1 walks from the Mars cave mouth (z 30) up the corridor into the domed chamber to King Germy (z ~71),
//              picking up the cave crystals on the way. &cavedome=old rebuilds the ffu25 dome collider (the bug).
//   phonemars  P1 on Mars with the phone open on the CALL tab
//   callall    tag-along off; P1 on Mars, the AI froggies on the ranch; P1 phones "Call everyone" -> they jetpack over
//   tagmars    P1 boards the Starship in Mars orbit -> the AI froggies ride along -> landing on Mars, crew around P1
//   landhome   P1 + crew from Mars orbit back to Earth -> the ranch pad landing sequence (LaunchSeq phase 3)
//   treehouse  P1 climbs a treehouse trunk, steps onto the deck, walks inside, then up on the roof
// (treeclimb lives in DestructDemo; DemoClimbWalk below steers P1's stick into the trunk for it)
public partial class Game
{
    static bool Is27(string sc) { return sc == "marscave" || sc == "phonemars" || sc == "callall" || sc == "tagmars" || sc == "landhome" || sc == "treehouse"; }
    float d27T, d27Log; int d27Phase, d27Wp;

    static Vector3 MarsLocal(float x, float z) { return SurfaceWorlds.M(x, SurfaceWorlds.MarsY(x, z) + 0.5f, z); }

    void Demo27Start(Frog f, string sc)
    {
        d27T = 0f; d27Log = 0f; d27Phase = 0; d27Wp = 0; demoHook = null;
        if (f.vehicle != null) f.ExitVehicle();
        if (!Roster.IsFrog(f.charId) || f.charId != 0) SetSeatChar(f.id, 0, false);
        demoKeepChars = true;
        Frog.TagOn = sc != "callall";
        var sw = SurfaceWorlds.I;
        if (sc == "marscave" || sc == "phonemars" || sc == "callall")
        {
            if (sw != null) sw.EnsureMars();
            if (sc == "marscave") f.SendTo(WorldId.Mars, MarsLocal(0f, 29f), 0f);
            else f.SendTo(WorldId.Mars, MarsLocal(3f, -20f), 180f);
            if (sc == "callall")
                foreach (Frog o in frogs) if (o != null && o != f && !o.human) o.SendTo(WorldId.Ranch, Ranch.FrogSpawn(o.id), 0f);
            if (sc == "phonemars" && RobotPhone.I != null) RobotPhone.I.DemoOpen(f, 1, "CALL");
            if (sc == "marscave") demoHook = DemoCaveWalk;
        }
        else if (sc == "tagmars" || sc == "landhome")
        {
            if (f.world != WorldId.Ranch) f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f);
            if (SpaceWorld.I != null) SpaceWorld.I.EnsureBuilt();
        }
        else if (sc == "treehouse")
        {
            if (f.world != WorldId.Ranch) f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f);
            if (Treehouse.Spots.Count > 0)
            {
                Vector3 b = Treehouse.Spots[0];
                f.DemoPose(b + new Vector3(-2.6f, 0f, 0f), 90f);
                demoHook = DemoTreehouseWalk;
            }
        }
        Debug.Log("FFDEMO 27 start " + sc + " t=" + Time.realtimeSinceStartup.ToString("0.0"));
    }

    Frog P1Frog { get { return slots.Count > 0 ? frogs[slots[0].frog] : null; } }

    static Vector2 StickTo(Frog f, Vector3 worldDir, float k)
    {
        worldDir.y = 0f;
        if (worldDir.sqrMagnitude < 1e-6f) return Vector2.zero;
        Vector3 m = Quaternion.Euler(0f, -f.CamYaw, 0f) * worldDir.normalized;
        return new Vector2(m.x, m.z) * k;
    }

    // marscave: walk the corridor waypoints into the chamber
    PIn DemoCaveWalk(PIn i)
    {
        Frog f = P1Frog; var o = new PIn(); o.look = i.look;
        if (f == null || f.world != WorldId.Mars) return o;
        float[] wz = { 36f, 50f, 60f, 70.5f };
        if (d27Wp >= wz.Length) return o;
        Vector3 goal = MarsLocal(0f, wz[d27Wp]);
        Vector3 to = goal - f.transform.position; to.y = 0f;
        if (to.magnitude < 1.4f) { d27Wp++; Debug.Log("FFDEMO marscave waypoint " + d27Wp + " z=" + (f.transform.position.z - Worlds.MarsO.z).ToString("0.0")); return o; }
        o.move = StickTo(f, to, 0.85f);
        return o;
    }

    // treeclimb (DestructDemo): walk 3 m into the trunk, 12 degrees off-centre (the glancing contact that used to slide)
    PIn DemoClimbWalk(PIn i)
    {
        Frog f = P1Frog; var t = DestructDemo.DemoTree;
        if (f == null || t == null || f.Climbing || f.demoTop) return i;
        var o = new PIn(); o.look = i.look;
        Vector3 to = t.basePos - f.transform.position;
        o.move = StickTo(f, Quaternion.Euler(0f, 12f, 0f) * to, 0.8f);
        return o;
    }

    // treehouse: into the trunk -> climb (demoClimbMove up) -> step out on the deck -> walk inside -> roof
    PIn DemoTreehouseWalk(PIn i)
    {
        Frog f = P1Frog; var o = new PIn(); o.look = i.look;
        if (f == null || Treehouse.Spots.Count == 0) return o;
        Vector3 b = Treehouse.Spots[0], p = f.transform.position;
        float up = p.y - b.y;
        if (d27Phase == 0)
        {
            if (f.Climbing) { d27Phase = 1; Debug.Log("FFDEMO treehouse climbing t=" + Time.realtimeSinceStartup.ToString("0.0")); }
            else o.move = StickTo(f, b - p, 0.8f);
        }
        if (d27Phase == 1)
        {
            f.demoClimbMove = new Vector2(0f, 1f);
            if (!f.Climbing && up > Treehouse.DeckY - 0.5f) { f.demoClimbMove = Vector2.zero; d27Phase = 2; d27T = 0f; Debug.Log("FFDEMO treehouse on the deck y=" + up.ToString("0.0") + " t=" + Time.realtimeSinceStartup.ToString("0.0")); }
            else if (!f.Climbing && d27T > 25f) { f.demoClimbMove = Vector2.zero; d27Phase = 9; Debug.Log("FFDEMO treehouse FELL OFF y=" + up.ToString("0.0")); }
        }
        if (d27Phase == 2)
        {
            float sd = p.z >= b.z ? 1f : -1f;
            Vector3[] wp = { b + new Vector3(1.05f, 0f, 1.15f * sd), b + new Vector3(1.4f, 0f, 0f), b + new Vector3(4.1f, 0f, 0.2f) };
            Vector3 g = wp[Mathf.Min(d27Wp, 2)]; Vector3 to = g - p; to.y = 0f;
            if (to.magnitude < 0.5f || (d27T > 40f && d27Wp < 2)) { if (d27Wp < 2) { d27Wp++; if (d27T > 40f) d27T = 0f; } else { d27Phase = 3; d27T = 0f; Debug.Log("FFDEMO treehouse inside y=" + up.ToString("0.0") + " t=" + Time.realtimeSinceStartup.ToString("0.0")); } }
            else o.move = StickTo(f, to, 0.55f);
        }
        if (d27Phase == 3 && d27T > 12f)
        {
            // demo shortcut to the roof (players hop crate -> crate -> roof)
            f.Teleport(b + new Vector3(3.6f, Treehouse.DeckY + 3.9f, 0.4f));
            d27Phase = 4; d27T = 0f; Debug.Log("FFDEMO treehouse roof (teleport) t=" + Time.realtimeSinceStartup.ToString("0.0"));
        }
        if (d27Phase == 4)
        {
            o.move = StickTo(f, new Vector3(Mathf.Sin(d27T * 0.6f), 0f, 0f), 0.35f);
            o.hop = Mathf.Repeat(d27T, 2.5f) < 0.1f;
        }
        return o;
    }

    bool Demo27Cam(Frog f, string sc, out Vector3 pos, out Vector3 look)
    {
        pos = look = Vector3.zero;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.3f);
        d27T += dt;
        bool log = (d27Log -= Time.unscaledDeltaTime) <= 0f;
        if (log) d27Log = 1.5f;
        Vector3 fp = f.transform.position;
        string crew = "";
        if (log) foreach (Frog o in frogs) if (o != null && o != f) crew += " " + o.nick + ":" + o.world + (o.world == f.world ? "@" + Vector3.Distance(o.transform.position, fp).ToString("0.0") + "m" : "") + (o.passengerOf != null ? "(ship)" : "");
        switch (sc)
        {
            case "marscave":
                {
                    if (log) Debug.Log("FFDEMO marscave z=" + (fp.z - Worlds.MarsO.z).ToString("0.0") + " x=" + (fp.x - Worlds.MarsO.x).ToString("0.0") + " y=" + (fp.y - Worlds.MarsO.y).ToString("0.00") + " crystals left " + Pickups.Remaining("crystals") + " wp " + d27Wp + " t=" + Time.realtimeSinceStartup.ToString("0.0"));
                    if (d27Wp >= 4) { pos = fp + new Vector3(2.6f, 1.6f, 3.6f); look = fp + new Vector3(0f, 0.6f, 1.5f); }
                    else { pos = fp + new Vector3(0.6f, 1.9f, -4.2f); look = fp + new Vector3(0f, 0.7f, 5f); }
                    return true;
                }
            case "phonemars":
                if (log) Debug.Log("FFDEMO phonemars open=" + (RobotPhone.I != null && RobotPhone.I.open) + " world=" + f.world);
                if (d27T > 7f && d27Phase == 0 && RobotPhone.I != null) { d27Phase = 1; RobotPhone.I.DemoOpen(f, 1, "call:all"); }
                pos = fp + new Vector3(-1.2f, 1.5f, -4.4f); look = fp + new Vector3(0.6f, 0.7f, 0f);
                return true;
            case "callall":
                if (log) Debug.Log("FFDEMO callall" + crew + " t=" + Time.realtimeSinceStartup.ToString("0.0"));
                if (d27T > 4f && d27Phase == 0 && RobotPhone.I != null) { d27Phase = 1; RobotPhone.I.DemoOpen(f, 1, "call:all"); RobotPhone.I.DemoSend(); Debug.Log("FFDEMO callall sent"); }
                if (d27T > 9f && d27Phase == 1 && RobotPhone.I != null) { d27Phase = 2; RobotPhone.I.open = false; }
                pos = fp + new Vector3(5f, 3.2f, -8f); look = fp + new Vector3(0f, 0.6f, 0f);
                return true;
            case "tagmars":
                {
                    if (log) Debug.Log("FFDEMO tagmars P1:" + f.world + crew + " t=" + Time.realtimeSinceStartup.ToString("0.0"));
                    var sw = SpaceWorld.I;
                    if (sw == null) return false;
                    if (d27T > 2f && d27Phase == 0) { d27Phase = 1; sw.ToOrbit(f, "mars"); Debug.Log("FFDEMO tagmars aboard in Mars orbit"); }
                    if (d27T > 9f && d27Phase == 1) { d27Phase = 2; sw.Land(sw.Find("mars")); Debug.Log("FFDEMO tagmars landing on Mars"); }
                    if (f.world != WorldId.Mars) return false;   // the normal space / ranch camera
                    pos = fp + new Vector3(4f, 2.4f, 6.5f); look = fp + new Vector3(0f, 0.4f, -1f);
                    return true;
                }
            case "landhome":
                {
                    if (log) Debug.Log("FFDEMO landhome P1:" + f.world + (f.launching ? "(landing)" : "") + crew + " t=" + Time.realtimeSinceStartup.ToString("0.0"));
                    var sw = SpaceWorld.I;
                    if (sw == null) return false;
                    if (d27T > 2f && d27Phase == 0) { d27Phase = 1; sw.ToOrbit(f, "mars"); }
                    if (d27T > 8f && d27Phase == 1) { d27Phase = 2; sw.Land(sw.Find("earth")); Debug.Log("FFDEMO landhome: land at Earth"); }
                    return false;   // LaunchSeq.View drives the landing camera; follow cam otherwise
                }
            case "treehouse":
                {
                    if (Treehouse.Spots.Count == 0) return false;
                    Vector3 b = Treehouse.Spots[0];
                    if (log) Debug.Log("FFDEMO treehouse phase " + d27Phase + " h=" + (fp.y - b.y).ToString("0.00") + " climbing=" + f.Climbing + " t=" + Time.realtimeSinceStartup.ToString("0.0"));
                    if (d27Phase <= 1) { pos = b + new Vector3(-5.5f, Mathf.Max(1.6f, fp.y - b.y + 1.2f), -4.5f); look = fp + Vector3.up * 0.3f; }
                    else if (d27Phase == 2) { pos = b + new Vector3(-4.5f, Treehouse.DeckY + 2.4f, -5f); look = fp; }
                    else if (d27Phase == 3) { pos = b + new Vector3(4.0f, Treehouse.DeckY + 1.35f, -4.6f); look = b + new Vector3(4.1f, Treehouse.DeckY + 0.6f, 0.2f); }
                    else { pos = b + new Vector3(11f, Treehouse.DeckY + 5f, -8f); look = b + new Vector3(3.6f, Treehouse.DeckY + 2.4f, 0f); }
                    return true;
                }
        }
        return false;
    }
}
