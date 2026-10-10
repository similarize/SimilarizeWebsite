using UnityEngine;
using UnityEngine.InputSystem;

public enum InputKind { Gamepad, Keyboard, Touch }

// One frame of player intent, the same shape for every device.
public struct PIn
{
    public Vector2 move;      // left stick / WASD / touch stick
    public Vector2 look;      // degrees this frame (right stick, mouse, touch drag)
    public bool hop;          // A / Space / touch A
    public bool use;          // A / E / touch A  (enter / exit vehicle)
    public float gas, brake;  // RT / LT (0..1)
    public float climb;       // flyers: +up / -down
    public bool fire, fireHeld;   // RT (tank shell)
    public bool alt;          // RB / right mouse (tank missile)
    public float zoom;        // + out / - in (per second units)
    public bool view;         // Back/View / V / touch VIEW
    public bool help;         // Start / H
    public bool lookHeld;     // a look finger / right stick is held (suppresses camera auto-follow)
    public bool camReset;     // R3 / 0: camera back behind, default pitch + zoom
    public bool phone;        // James's phone (stage E)
    public bool hopHeld;      // A held (swim up)
    public bool downHeld;     // B held (swim down)
    public bool target;       // space: pick next target (D-pad right / T)
    public bool targetPrev;   // D-pad left / Shift+T
    public bool auto;         // space: auto-transfer (X / G)
    public bool land;         // space: land (Y / F)
    public bool warpUp, warpDown;   // space: RB / LB, C / Z
    // ffu14 mechs: UP (pad RT / Space / touch JUMP) = tap jump, hold rockets; BOOST (pad LT / Shift / touch BOOST) =
    // afterburner; chest cannon on pad X / mouse left / touch FIRE (pad RT is the rocket there, not the gun)
    public bool upHeld, boostHeld, gunFire, gunHeld;
    // ffu15: mech arm cannon. AIM (pad LT / hold right mouse / touch AIM toggle) = over-the-shoulder aim; missiles
    // (pad RB or Y / key F / touch MSL); BOOST moved to pad LB. cargo = mech-in-space bay doors (pad B / key B / touch BAY);
    // warpHome = space: target Earth + auto-transfer (pad L3 / key R / tap the radar)
    public bool aimHeld, mslFire, cargo, warpHome;
}

public static class Kb
{
    public static bool typing;     // ffu13: the on-screen keypad is open (letter keys type, they don't toggle things)
    static bool Key(System.Func<Keyboard, bool> f, KeyCode legacy, bool down)
    {
        Keyboard k = Keyboard.current;
        if (k != null) return f(k);
        try { return down ? Input.GetKeyDown(legacy) : Input.GetKey(legacy); } catch { return false; }
    }

    public static Vector2 Move()
    {
        Vector2 v = Vector2.zero;
        if (Key(k => k.wKey.isPressed || k.upArrowKey.isPressed, KeyCode.W, false)) v.y += 1;
        if (Key(k => k.sKey.isPressed || k.downArrowKey.isPressed, KeyCode.S, false)) v.y -= 1;
        if (Key(k => k.dKey.isPressed || k.rightArrowKey.isPressed, KeyCode.D, false)) v.x += 1;
        if (Key(k => k.aKey.isPressed || k.leftArrowKey.isPressed, KeyCode.A, false)) v.x -= 1;
        return Vector2.ClampMagnitude(v, 1f);
    }

    public static bool EnterDown() { return Key(k => k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame, KeyCode.Return, true); }
    public static bool EscDown() { return Key(k => k.escapeKey.wasPressedThisFrame, KeyCode.Escape, true); }
    public static bool BackDown() { return Key(k => k.backspaceKey.wasPressedThisFrame, KeyCode.Backspace, true); }
    public static bool SpaceDown() { return Key(k => k.spaceKey.wasPressedThisFrame, KeyCode.Space, true); }
    public static bool Space() { return Key(k => k.spaceKey.isPressed, KeyCode.Space, false); }
    public static bool Shift() { return Key(k => k.leftShiftKey.isPressed || k.leftCtrlKey.isPressed, KeyCode.LeftShift, false); }
    public static bool EDown() { return Key(k => k.eKey.wasPressedThisFrame, KeyCode.E, true); }
    public static bool ZeroDown() { return Key(k => k.digit0Key.wasPressedThisFrame, KeyCode.Alpha0, true); }
    public static bool TDown() { return Key(k => k.tKey.wasPressedThisFrame, KeyCode.T, true); }
    public static bool GDown() { return Key(k => k.gKey.wasPressedThisFrame, KeyCode.G, true); }
    public static bool FDown() { return Key(k => k.fKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame, KeyCode.F, true); }
    public static bool CDown() { return Key(k => k.cKey.wasPressedThisFrame, KeyCode.C, true); }
    public static bool ZDown() { return Key(k => k.zKey.wasPressedThisFrame, KeyCode.Z, true); }
    public static bool PDown() { return Key(k => k.pKey.wasPressedThisFrame, KeyCode.P, true); }
    public static bool MDown() { return Key(k => k.mKey.wasPressedThisFrame, KeyCode.M, true); }
    public static bool VDown() { return Key(k => k.vKey.wasPressedThisFrame, KeyCode.V, true); }
    public static bool HDown() { return Key(k => k.hKey.wasPressedThisFrame || k.f1Key.wasPressedThisFrame, KeyCode.H, true); }
    public static bool LeftDown() { return Key(k => k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame, KeyCode.LeftArrow, true); }
    public static bool RightDown() { return Key(k => k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame, KeyCode.RightArrow, true); }
    public static bool Q() { return Key(k => k.qKey.isPressed || k.zKey.isPressed, KeyCode.Q, false); }
    public static bool Z() { return Key(k => k.xKey.isPressed || k.cKey.isPressed, KeyCode.X, false); }

    public static bool MouseLeft()
    {
        Mouse m = Mouse.current;
        if (m != null) return m.leftButton.isPressed;
        try { return Input.GetMouseButton(0); } catch { return false; }
    }

    public static bool MouseLeftDown()
    {
        Mouse m = Mouse.current;
        if (m != null) return m.leftButton.wasPressedThisFrame;
        try { return Input.GetMouseButtonDown(0); } catch { return false; }
    }

    public static bool MouseRight()
    {
        Mouse m = Mouse.current;
        if (m != null) return m.rightButton.isPressed;
        try { return Input.GetMouseButton(1); } catch { return false; }
    }

    public static bool BDown() { return Key(k => k.bKey.wasPressedThisFrame, KeyCode.B, true); }
    public static bool RDown() { return Key(k => k.rKey.wasPressedThisFrame, KeyCode.R, true); }
    public static bool FKeyDown() { return Key(k => k.fKey.wasPressedThisFrame, KeyCode.F, true); }

    public static bool MouseRightDown()
    {
        Mouse m = Mouse.current;
        if (m != null) return m.rightButton.wasPressedThisFrame;
        try { return Input.GetMouseButtonDown(1); } catch { return false; }
    }

    public static Vector2 MouseDelta()
    {
        Mouse m = Mouse.current;
        if (m != null) return m.delta.ReadValue();
        try { return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f; } catch { return Vector2.zero; }
    }

    public static float Scroll()
    {
        Mouse m = Mouse.current;
        if (m != null) return m.scroll.ReadValue().y;
        try { return Input.mouseScrollDelta.y * 120f; } catch { return 0f; }
    }

    public static int TouchCount()
    {
        try { return Input.touchCount; } catch { return 0; }
    }

    public static System.Collections.Generic.List<Vector2> TouchesBegan()
    {
        var l = new System.Collections.Generic.List<Vector2>();
        try
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                UnityEngine.Touch t = Input.GetTouch(i);
                if (t.phase == UnityEngine.TouchPhase.Began) l.Add(t.position);
            }
        }
        catch { }
        return l;
    }
}

public static class Pads
{
    public static Vector2 Dead(Vector2 v, float dz)
    {
        float m = v.magnitude;
        if (m < dz) return Vector2.zero;
        return v.normalized * Mathf.Clamp01((m - dz) / (1f - dz));
    }

    public static PIn Read(Gamepad p, float dt)
    {
        var i = new PIn();
        if (p == null || !p.added) return i;
        i.move = Dead(p.leftStick.ReadValue(), 0.18f);
        Vector2 r = Dead(p.rightStick.ReadValue(), 0.14f);
        i.look = new Vector2(r.x, r.y * 0.7f) * r.magnitude * 160f * dt;
        i.lookHeld = r.sqrMagnitude > 0.0001f;
        i.hopHeld = p.buttonSouth.isPressed;
        i.downHeld = p.buttonEast.isPressed;
        i.target = p.dpad.right.wasPressedThisFrame;
        i.targetPrev = p.dpad.left.wasPressedThisFrame;
        i.auto = p.buttonWest.wasPressedThisFrame;
        i.land = p.buttonNorth.wasPressedThisFrame;
        i.warpUp = p.rightShoulder.wasPressedThisFrame;
        i.warpDown = p.leftShoulder.wasPressedThisFrame;
        i.phone = p.leftShoulder.wasPressedThisFrame;
        i.camReset = p.rightStickButton.wasPressedThisFrame;
        i.hop = p.buttonSouth.wasPressedThisFrame;
        i.use = i.hop;
        i.gas = p.rightTrigger.ReadValue();
        i.brake = p.leftTrigger.ReadValue();
        i.climb = i.gas - i.brake;
        i.fire = p.rightTrigger.wasPressedThisFrame || p.buttonWest.wasPressedThisFrame;
        i.fireHeld = p.rightTrigger.isPressed || p.buttonWest.isPressed;
        i.alt = p.rightShoulder.wasPressedThisFrame || p.buttonNorth.wasPressedThisFrame;
        float z = 0f;
        if (p.dpad.down.isPressed) z += 1f;
        if (p.dpad.up.isPressed) z -= 1f;
        i.zoom = z;
        i.view = p.selectButton.wasPressedThisFrame;
        i.help = p.startButton.wasPressedThisFrame;
        // ffu15 mech mapping: LT aim (RT fires while aiming), RT jump / rockets otherwise, LB afterburner, X fire, RB / Y missiles
        i.aimHeld = i.brake > 0.4f;
        i.upHeld = i.gas > 0.3f && !i.aimHeld;
        i.boostHeld = p.leftShoulder.isPressed;
        i.gunFire = p.buttonWest.wasPressedThisFrame || (i.aimHeld && p.rightTrigger.wasPressedThisFrame);
        i.gunHeld = p.buttonWest.isPressed || (i.aimHeld && i.gas > 0.5f);
        i.mslFire = p.rightShoulder.wasPressedThisFrame || p.buttonNorth.wasPressedThisFrame;
        i.cargo = p.buttonEast.wasPressedThisFrame;
        i.warpHome = p.leftStickButton.wasPressedThisFrame;
        return i;
    }

    public static PIn ReadKeyboard(float dt)
    {
        var i = new PIn();
        i.move = Kb.Move();
        bool locked = Cursor.lockState == CursorLockMode.Locked;
        Vector2 md = Kb.MouseDelta();
        i.look = locked ? md * 0.12f : (Kb.MouseLeft() ? md * 0.25f : Vector2.zero);
        i.hop = Kb.SpaceDown();
        i.use = Kb.EDown();
        float w = Kb.Move().y;
        i.gas = Mathf.Max(0f, w);
        i.brake = Mathf.Max(0f, -w);
        i.climb = (Kb.Space() ? 1f : 0f) - (Kb.Shift() ? 1f : 0f);
        i.fire = locked && Kb.MouseLeftDown();
        i.fireHeld = locked && Kb.MouseLeft();
        i.alt = Kb.MouseRightDown();
        float z = -Kb.Scroll() / 120f * 12f;
        if (Kb.Q()) z -= 1f;
        if (Kb.Z()) z += 1f;
        i.zoom = z;
        i.view = Kb.VDown();
        i.help = Kb.HDown();
        i.camReset = Kb.ZeroDown();
        i.hopHeld = Kb.Space();
        i.downHeld = Kb.Shift();
        i.target = Kb.TDown() && !Kb.Shift();
        i.targetPrev = Kb.TDown() && Kb.Shift();
        i.auto = Kb.GDown();
        i.land = Kb.FDown();
        i.warpUp = Kb.CDown();
        i.warpDown = Kb.ZDown();
        i.phone = Kb.PDown();
        i.lookHeld = locked ? md.sqrMagnitude > 0.01f : Kb.MouseLeft();
        i.upHeld = Kb.Space();
        i.boostHeld = Kb.Shift();
        i.gunFire = i.fire;
        i.gunHeld = i.fireHeld;
        // ffu15: hold right mouse = mech aim (right click is still the tank missile / boat push elsewhere); F = mech missiles
        i.aimHeld = Kb.MouseRight();
        if (i.aimHeld && !locked) i.look = md * 0.12f;
        i.mslFire = Kb.FKeyDown();
        i.cargo = Kb.BDown();
        i.warpHome = Kb.RDown();
        return i;
    }

    // the raw signature used to spot a "ghost" pad that mirrors another one
    public static string Signature(Gamepad p)
    {
        Vector2 a = p.leftStick.ReadValue(), b = p.rightStick.ReadValue();
        return Mathf.RoundToInt(a.x * 20) + "," + Mathf.RoundToInt(a.y * 20) + "," + Mathf.RoundToInt(b.x * 20) + "," + Mathf.RoundToInt(b.y * 20)
            + (p.buttonSouth.isPressed ? "A" : "") + (p.buttonEast.isPressed ? "B" : "") + (p.buttonWest.isPressed ? "X" : "") + (p.buttonNorth.isPressed ? "Y" : "")
            + (p.leftShoulder.isPressed ? "L" : "") + (p.rightShoulder.isPressed ? "R" : "") + Mathf.RoundToInt(p.leftTrigger.ReadValue() * 10) + Mathf.RoundToInt(p.rightTrigger.ReadValue() * 10);
    }
}
