using UnityEngine;

// ffu39 demo shots for the probe (?ffdemo=1&ffshot=...):
//   roofstomp  a story mech (&rsband=0..3, default 2 = the 1000-story) stands east of the garage (roof intact, ~8 s), then
//              comes down on the garage roof from the top of a jump: panels break (debris, dust, crunch, quake), the mech
//              drops through the hole onto the garage floor; it stands there; the holes regrow ~26-38 s later.
//              &rsband=0 (the 10-story) lands softly: it stands ON the roof, its steps crack it, a hop then breaks through.
//              Logs "FFDEMO roofstomp t phase mech y grounded | roof breaks / cracks / broken panels".
//   pondtop    straight down on the pond from 230 m (shoreline, bank, water edge) - ffu39 pond rim check.
//   (seattest  runs in the lobby - Game/GameSeats.cs)
public partial class Game
{
    static bool Is39(string sc) { return sc == "roofstomp" || sc == "pondtop" || sc == "seattest"; }
    StoryMech r9Mech; Frog r9Frog; int r9Band, r9Phase; float r9T, r9PhT, r9Log;

    void Demo39Start(Frog f, string sc)
    {
        if (sc != "roofstomp") return;
        if (f.vehicle != null) f.ExitVehicle();
        if (f.world != WorldId.Ranch) f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f);
        r9Band = 2; string b = UrlParam("rsband"); if (b != null) int.TryParse(b, out r9Band); r9Band = Mathf.Clamp(r9Band, 0, 3);
        r9Mech = FindMech(0, r9Band); r9Frog = f;
        r9Phase = 0; r9T = 0f; r9PhT = 0f; r9Log = 0f;
        if (r9Mech == null) { Debug.Log("FFDEMO roofstomp: no mech band " + r9Band); return; }
        StoryMech.DtCap = 0.3f;
        Destruct.DtCap = 0.3f;
        float H = r9Mech.height;
        r9Mech.DemoPlace(new Vector3(Layout.GarageC.x + Layout.GarageSize.x * 0.5f + 6f + H * 0.25f, 0f, Layout.GarageC.y), 270f);
        f.EnterVehicle(r9Mech);
        demoHook = RoofInput;
        Debug.Log("FFDEMO roofstomp start: " + r9Mech.Title + " H " + H + " roof panels " + RoofBreak.Pieces.Count);
    }

    PIn RoofInput(PIn i)
    {
        var o = new PIn();
        if (r9Band == 0 && r9Phase == 2 && r9T - r9PhT > 2f && r9T - r9PhT < 9f) o.move = new Vector2(0.6f, 0.45f);   // small mech: steps on the roof
        if (r9Band == 0 && r9Phase == 2 && r9T - r9PhT > 10f && r9T - r9PhT < 10.4f) o.upHeld = true;          // ... then a hop
        return o;
    }

    int BrokenRoofs() { int n = 0; foreach (var b in RoofBreak.Pieces) if (b != null && b.state != 0) n++; return n; }

    bool Demo39Cam(Frog f, string sc, out Vector3 pos, out Vector3 look)
    {
        pos = look = Vector3.zero;
        if (sc == "pondtop")
        {
            Vector2 c = Layout.PondC;
            pos = new Vector3(c.x, 230f, c.y - 0.5f); look = new Vector3(c.x, Layout.WaterY, c.y);
            if (Time.frameCount % 150 == 0) Debug.Log("FFDEMO pondtop realism=" + Realism.Ready + " waveSim=" + WaveSim.Active);
            return true;
        }
        if (sc != "roofstomp" || r9Mech == null) return false;
        float dt = Mathf.Min(Time.deltaTime, 0.34f);
        r9T += dt; r9Log -= Time.unscaledDeltaTime;
        float H = r9Mech.height;
        Vector2 gc = Layout.GarageC;
        if (r9Phase == 0 && r9T > 8f)
        {
            // the top of a jump over the garage roof, falling onto it
            float top = Layout.GarageH + 0.4f;
            r9Mech.DemoDrop(new Vector3(gc.x + 9f, top + (r9Band == 0 ? 2.5f : H * 0.35f), gc.y), 270f, r9Band == 0 ? -1f : -4f);
            r9Phase = 1; r9PhT = r9T;
            Debug.Log("FFDEMO roofstomp -> phase 1 (jump onto the roof) t=" + r9T.ToString("0.0"));
        }
        else if (r9Phase == 1 && r9Mech.DemoGrounded && r9T - r9PhT > 0.5f) { r9Phase = 2; r9PhT = r9T; Debug.Log("FFDEMO roofstomp -> phase 2 (landed) t=" + r9T.ToString("0.0") + " y=" + r9Mech.transform.position.y.ToString("0.00")); }
        // raised view from the south-east, framing the garage roof + the mech
        float d = 34f + H * 0.9f;
        pos = new Vector3(gc.x + d * 0.55f, Layout.GarageH + 10f + H * 0.45f, gc.y - d * 0.85f);
        look = new Vector3(gc.x + 6f, Layout.GarageH * 0.6f + H * 0.12f, gc.y);
        if (r9Log <= 0f)
        {
            r9Log = 0.5f;
            Vector3 mp = r9Mech.transform.position;
            Debug.Log("FFDEMO roofstomp t=" + r9T.ToString("0.0") + " phase=" + r9Phase + " mech y=" + mp.y.ToString("0.00") + " ground=" + Ranch.GY(mp.x, mp.z).ToString("0.00") + " grounded=" + r9Mech.DemoGrounded + " | roof breaks " + RoofBreak.Breaks + " cracks " + RoofBreak.Cracks + " broken panels " + BrokenRoofs() + "/" + RoofBreak.Pieces.Count);
        }
        return true;
    }
}
