using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// ffu67 probe: ?ffdemo=1&ffshot=wheels67 - PER-PLAYER wheel size with the gamepad (Bill Oct 20: "when you're in the cyber truck
// you should be able to change the size of the wheels using the game pad ... as a user, not as a whole game").
// Two synthetic pads join (split-screen), P1 drives James's Cybertruck, P2 drives Dad's Cybertruck. P1 D-pad up x4 (2x),
// P2 D-pad up x7 + down x1 (2.5x): each changes only its own truck; View + D-pad = zoom (no wheel change, no view toggle);
// the help menu opened by P1: P2's D-pad > changes only P2's truck. Tyres settle, trucks sit on the ground (wheels grounded,
// upright, body above the terrain) and drive. Swap: P1 in Dad's truck keeps the truck's size until P1 changes it; P2 back
// in -> P2's own size for it again. Logs "WHEELS67 PASS|FAIL ...", "WHEELS67 RESULT n/m", "FFDEMO wheels67 ph N <name>", "FFDEMO wheels67 done".
public class Ffu67 : MonoBehaviour
{
    public static Ffu67 I;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        I = new GameObject("ffu67").AddComponent<Ffu67>();
        DontDestroyOnLoad(I.gameObject);
    }
    bool started;
    void Update()
    {
        if (started) return;
        string s = Ffu33.Url("ffdemo") != null ? Ffu33.Url("ffshot") : null;
        if (s != "wheels67") { enabled = false; return; }
        if (Game.I == null || Game.I.state != Game.State.Play || Game.I.slots.Count == 0) return;
        started = true;
        Game.I.StartCoroutine(Game.I.W67Run());
    }
}

public partial class Game
{
    int w67Pass, w67Total, w67Ph;
    void W67Check(string what, bool ok, string detail) { w67Total++; if (ok) w67Pass++; Debug.Log("WHEELS67 " + (ok ? "PASS" : "FAIL") + " " + what + " | " + detail); }
    void W67Next(string name) { Debug.Log("FFDEMO wheels67 ph " + (w67Ph++) + " " + name + " t " + Time.time.ToString("0.0")); }
    IEnumerator W67F(int n) { for (int k = 0; k < n; k++) yield return null; }
    IEnumerator W67Wait(float s) { yield return new WaitForSecondsRealtime(s); yield return W67F(2); }
    // press + release one button (other state held: trigger)
    IEnumerator W67Press(Gamepad p, GamepadButton b, float rt = 0f)
    {
        var st = new GamepadState().WithButton(b); st.rightTrigger = rt;
        InputSystem.QueueStateEvent(p, st);
        yield return W67F(2);
        var up = new GamepadState(); up.rightTrigger = rt;
        InputSystem.QueueStateEvent(p, up);
        yield return W67F(2);
    }
    static void W67Park(Vehicle v, float x, float z)
    {
        if (v == null) return;
        if (v.driver != null) v.driver.ExitVehicle();
        Vector3 p = new Vector3(x, Ranch.GY(x, z) + 0.6f + v.bigLift, z);
        Quaternion q = Quaternion.Euler(0f, 90f, 0f);
        v.rb.velocity = Vector3.zero; v.rb.angularVelocity = Vector3.zero;
        v.rb.position = p; v.rb.rotation = q; v.transform.SetPositionAndRotation(p, q);
    }
    static string W67Sit(GroundVehicle v, out bool ok)
    {
        int g = 0; foreach (var w in v.wheels) if (w.grounded) g++;
        Vector3 p = v.transform.position; float gy = Ranch.GY(p.x, p.z);
        float bodyBot = v.body != null ? v.body.bounds.min.y : p.y;
        ok = g >= v.wheels.Count - 1 && v.transform.up.y > 0.9f && bodyBot > gy + 0.05f;
        return v.Title + " k " + v.bigK.ToString("0.00") + " want " + BigWheels.Want(v).ToString("0.00") + " (" + BigWheels.InchLabel(v) + ") grounded " + g + "/" + v.wheels.Count +
               " up " + v.transform.up.y.ToString("0.00") + " body bottom " + (bodyBot - gy).ToString("0.00") + " m above ground, lift " + v.bigLift.ToString("0.00");
    }

    public IEnumerator W67Run()
    {
        Debug.Log("FFDEMO wheels67 start slots " + slots.Count + " pads " + Gamepad.all.Count);
        yield return W67Wait(2f);
        Gamepad A = InputSystem.AddDevice<Gamepad>("FFTestPad67");
        yield return W67Wait(1.2f);
        yield return W67Press(A, GamepadButton.South);
        yield return W67Wait(1f);
        Gamepad B = InputSystem.AddDevice<Gamepad>("FFTestPad67");
        yield return W67Wait(1.2f);
        yield return W67Press(B, GamepadButton.South);
        yield return W67Wait(1.5f);
        Slot sa = FindPad(A), sb = FindPad(B);
        W67Check("two pads seated (split-screen)", sa != null && sb != null && slots.Count == 2, SeatList() + " shared " + shared);
        if (sa == null || sb == null) { W67Done(); yield break; }
        if (shared) { ToggleView(); yield return W67Wait(0.5f); }
        Frog fa = frogs[sa.frog], fb = frogs[sb.frog];
        GroundVehicle ja = null, da = null;
        foreach (var v in Vehicle.All) { var gv = v as GroundVehicle; if (gv == null) continue; if (v.ownerName == "James") ja = gv; else if (v.ownerName == "Dad") da = gv; }
        W67Check("James's + Dad's Cybertrucks found", ja != null && da != null, (ja != null ? ja.Title : "no James truck") + " / " + (da != null ? da.Title : "no Dad truck"));
        if (ja == null || da == null) { W67Done(); yield break; }
        if (DadFroggy.F != null && DadFroggy.F.vehicle != null) DadFroggy.F.ExitVehicle();
        W67Park(ja, 6f, -8f); W67Park(da, 6f, 0f);
        // start both trucks at stock (as the vehicle), no player picks
        BigWheels.SetFor(ja, 1f, "demo reset", false); BigWheels.SetFor(da, 1f, "demo reset", false);
        yield return W67Wait(0.5f);
        fa.SendTo(WorldId.Ranch, ja.transform.position + Vector3.right * 4f, 0f); fb.SendTo(WorldId.Ranch, da.transform.position + Vector3.right * 4f, 0f);
        yield return W67F(2);
        fa.EnterVehicle(ja); fb.EnterVehicle(da);
        yield return W67Wait(2f);
        W67Check("P1 drives James's truck, P2 drives Dad's truck", fa.vehicle == ja && ja.driver == fa && fb.vehicle == da && da.driver == fb, fa.nick + " -> " + (fa.vehicle != null ? fa.vehicle.Title : "-") + ", " + fb.nick + " -> " + (fb.vehicle != null ? fb.vehicle.Title : "-"));
        W67Next("stock");
        yield return W67Wait(3f);
        bool sitA, sitB; string s1 = W67Sit(ja, out sitA), s2 = W67Sit(da, out sitB);
        W67Check("both trucks sit on the ground at stock", sitA && sitB, s1 + " || " + s2);

        // P1: D-pad up x4 -> 2x
        int n0 = WheelPadNudges; float zA0 = sa.rig != null ? sa.rig.zoomMul : 1f; bool sh0 = shared;
        for (int k = 0; k < 4; k++) yield return W67Press(A, GamepadButton.DpadUp);
        W67Check("P1 D-pad up x4: P1's truck 2x, P2's truck untouched", Mathf.Abs(BigWheels.Want(ja) - 2f) < 0.01f && Mathf.Abs(BigWheels.Want(da) - 1f) < 0.01f,
            "James " + BigWheels.Want(ja).ToString("0.00") + " Dad " + BigWheels.Want(da).ToString("0.00") + " nudges " + (WheelPadNudges - n0) + " | P1 toast '" + fa.toast + "'");
        W67Check("toast reads in inches on P1's view", fa.toast.StartsWith("Wheels: ") && fa.toast.Contains(" in"), "'" + fa.toast + "' | P2 toast '" + fb.toast + "'");
        W67Check("D-pad in the truck does not zoom or toggle the view", (sa.rig == null || Mathf.Abs(sa.rig.zoomMul - zA0) < 0.02f) && shared == sh0, "zoom " + zA0.ToString("0.00") + " -> " + (sa.rig != null ? sa.rig.zoomMul.ToString("0.00") : "-") + " shared " + shared);
        // P2: up x7 then down x1 -> 2.5x (the 4x clamp: SetFor clamps Min..Max, BigWheels.MaxR caps the tyre radius)
        for (int k = 0; k < 7; k++) yield return W67Press(B, GamepadButton.DpadUp);
        float topB = BigWheels.Want(da);
        yield return W67Press(B, GamepadButton.DpadDown);
        BigWheels.SetFor(da, 9f, "clamp probe", false, fb); float clampK = BigWheels.Want(da); BigWheels.SetFor(da, 2.5f, "clamp probe", false, fb);
        W67Check("P2 D-pad up x7 + down x1: P2's truck 2.5x, P1's stays 2x; sizes clamp at 4x", Mathf.Abs(topB - 2.75f) < 0.01f && Mathf.Abs(BigWheels.Want(da) - 2.5f) < 0.01f && Mathf.Abs(BigWheels.Want(ja) - 2f) < 0.01f && Mathf.Abs(clampK - BigWheels.Max) < 0.01f,
            "top " + topB.ToString("0.00") + " clamp " + clampK.ToString("0.00") + " Dad " + BigWheels.Want(da).ToString("0.00") + " James " + BigWheels.Want(ja).ToString("0.00") + " | P2 toast '" + fb.toast + "'");
        W67Next("sized");
        // View + D-pad up = zoom, wheels unchanged, view not toggled
        float wA = BigWheels.Want(ja); zA0 = sa.rig != null ? sa.rig.zoomMul : 1f; sh0 = shared;
        InputSystem.QueueStateEvent(A, new GamepadState().WithButton(GamepadButton.Select));
        yield return W67F(3);
        InputSystem.QueueStateEvent(A, new GamepadState().WithButton(GamepadButton.Select).WithButton(GamepadButton.DpadDown));
        yield return W67Wait(0.8f);
        InputSystem.QueueStateEvent(A, new GamepadState().WithButton(GamepadButton.Select));
        yield return W67F(3);
        InputSystem.QueueStateEvent(A, new GamepadState());
        yield return W67Wait(0.6f);
        W67Check("View + D-pad = camera zoom (wheels same, no split/shared toggle)", Mathf.Abs(BigWheels.Want(ja) - wA) < 0.01f && shared == sh0 && (sa.rig == null || sa.rig.zoomMul > zA0 + 0.05f),
            "wheels " + wA.ToString("0.00") + " -> " + BigWheels.Want(ja).ToString("0.00") + " zoom " + zA0.ToString("0.00") + " -> " + (sa.rig != null ? sa.rig.zoomMul.ToString("0.00") : "-") + " shared " + sh0 + " -> " + shared);
        // help menu opened by P1, P2's pad D-pad > : P2's own truck only
        yield return W67Press(A, GamepadButton.Start);
        yield return W67Wait(0.8f);
        bool helpOpen = help;
        W67Next("help");
        yield return W67Wait(1.5f);
        yield return W67Press(B, GamepadButton.DpadRight);
        yield return W67Wait(0.5f);
        W67Check("help menu (opened by P1): P2's D-pad > changes only P2's truck", helpOpen && Mathf.Abs(BigWheels.Want(da) - 2.75f) < 0.01f && Mathf.Abs(BigWheels.Want(ja) - 2f) < 0.01f,
            "help " + helpOpen + " Dad " + BigWheels.Want(da).ToString("0.00") + " James " + BigWheels.Want(ja).ToString("0.00") + " | row '" + (wheelLabel != null ? wheelLabel.text : "") + "'");
        yield return W67Wait(1.5f);
        yield return W67Press(A, GamepadButton.Start);
        yield return W67Wait(1f);
        // settle: tyres ease 1.1 /s of GAME time (wait for it; the probe runs at < 1 fps)
        { float gt = 0f, rt0 = Time.realtimeSinceStartup; while ((gt < 4f || Mathf.Abs(ja.bigK - BigWheels.Want(ja)) > 0.02f || Mathf.Abs(da.bigK - BigWheels.Want(da)) > 0.02f) && gt < 12f && Time.realtimeSinceStartup - rt0 < 240f) { yield return null; gt += Time.deltaTime; } }
        W67Next("settled");
        s1 = W67Sit(ja, out sitA); s2 = W67Sit(da, out sitB);
        W67Check("both trucks settled at their own sizes, on the ground (no clipping)", sitA && sitB && Mathf.Abs(ja.bigK - 2f) < 0.05f && Mathf.Abs(da.bigK - 2.75f) < 0.3f && da.bigK > ja.bigK + 0.5f, s1 + " || " + s2);
        yield return W67Wait(2f);
        // drive both (RT 0.45) ~5 s
        Vector3 pa = ja.transform.position, pb = da.transform.position; float minUpA = 1f, minUpB = 1f, minClr = 9f;
        var go = new GamepadState(); go.rightTrigger = 0.45f;
        InputSystem.QueueStateEvent(A, go); InputSystem.QueueStateEvent(B, go);
        W67Next("drive");
        float t = 0f;
        float rt1 = Time.realtimeSinceStartup;
        while (t < 5f && Time.realtimeSinceStartup - rt1 < 200f)
        {
            yield return null; t += Time.deltaTime;
            minUpA = Mathf.Min(minUpA, ja.transform.up.y); minUpB = Mathf.Min(minUpB, da.transform.up.y);
            foreach (var v in new[] { ja, da }) { Vector3 q = v.transform.position; float bb = v.body != null ? v.body.bounds.min.y : q.y; minClr = Mathf.Min(minClr, bb - Ranch.GY(q.x, q.z)); }
        }
        InputSystem.QueueStateEvent(A, new GamepadState()); InputSystem.QueueStateEvent(B, new GamepadState());
        float da1 = Vector3.Distance(pa, ja.transform.position), db1 = Vector3.Distance(pb, da.transform.position);
        W67Check("both drive on their own wheel sizes, upright, body never below the ground", da1 > 3f && db1 > 3f && minUpA > 0.75f && minUpB > 0.75f && minClr > -0.05f,
            "moved P1 " + da1.ToString("0.0") + " m, P2 " + db1.ToString("0.0") + " m | min up " + minUpA.ToString("0.00") + " / " + minUpB.ToString("0.00") + " | min body clearance " + minClr.ToString("0.00") + " m");
        yield return W67Wait(3f);
        // swap: P1 into Dad's truck (no P1 pick for it -> keeps the truck's 2.75x), P1 D-pad down x1 -> 2.5 (P1's pick);
        // P2 back in Dad's truck -> P2's own 2.75x
        fa.ExitVehicle(); fb.ExitVehicle();
        yield return W67Wait(1f);
        fa.EnterVehicle(da);
        yield return W67Wait(1f);
        float keep = BigWheels.Want(da);
        yield return W67Press(A, GamepadButton.DpadDown);
        yield return W67Wait(0.5f);
        float p1pick = BigWheels.Want(da);
        fa.ExitVehicle();
        yield return W67Wait(1f);
        fb.EnterVehicle(da);
        yield return W67Wait(1f);
        float p2back = BigWheels.Want(da);
        W67Check("per player per truck: P1 in Dad's truck keeps 2.75x, sets own 2.5x; P2 back in -> P2's 2.75x", Mathf.Abs(keep - 2.75f) < 0.01f && Mathf.Abs(p1pick - 2.5f) < 0.01f && Mathf.Abs(p2back - 2.75f) < 0.01f,
            "keep " + keep.ToString("0.00") + " P1 pick " + p1pick.ToString("0.00") + " P2 back " + p2back.ToString("0.00"));
        fa.EnterVehicle(ja);
        yield return W67Wait(4f);
        W67Next("final");
        yield return W67Wait(3f);
        W67Done();
    }
    void W67Done() { Debug.Log("WHEELS67 RESULT " + w67Pass + "/" + w67Total); Debug.Log("FFDEMO wheels67 done"); }
}
