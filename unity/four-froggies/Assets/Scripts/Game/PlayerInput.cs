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
}

public static class Kb
{
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
    public static bool EDown() { return Key(k => k.eKey.wasPressedThisFrame || k.fKey.wasPressedThisFrame, KeyCode.E, true); }
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
