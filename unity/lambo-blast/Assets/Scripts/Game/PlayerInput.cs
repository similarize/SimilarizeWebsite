using UnityEngine;
using UnityEngine.InputSystem;

public enum InputKind { Gamepad, Keyboard, Touch }

// One frame of driver intent, the same shape for every device.
public struct KIn
{
    public float steer;      // -1 left .. +1 right
    public float gas;        // 0..1
    public float brake;      // 0..1 (brake, then reverse)
    public bool fire;        // fire / drop the held item (pressed this frame)
    public bool drift;       // drift held
    public bool pause;       // Start / Esc
}

public static class Kb
{
    static bool Key(System.Func<Keyboard, bool> f, KeyCode legacy, bool down)
    {
        Keyboard k = Keyboard.current;
        if (k != null) return f(k);
        try { return down ? Input.GetKeyDown(legacy) : Input.GetKey(legacy); } catch { return false; }
    }

    public static bool Left() { return Key(k => k.aKey.isPressed || k.leftArrowKey.isPressed, KeyCode.A, false); }
    public static bool Right() { return Key(k => k.dKey.isPressed || k.rightArrowKey.isPressed, KeyCode.D, false); }
    public static bool Up() { return Key(k => k.wKey.isPressed || k.upArrowKey.isPressed, KeyCode.W, false); }
    public static bool Down() { return Key(k => k.sKey.isPressed || k.downArrowKey.isPressed, KeyCode.S, false); }
    public static bool LeftDown() { return Key(k => k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame, KeyCode.LeftArrow, true); }
    public static bool RightDown() { return Key(k => k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame, KeyCode.RightArrow, true); }
    public static bool UpDown() { return Key(k => k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame, KeyCode.UpArrow, true); }
    public static bool DownDown() { return Key(k => k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame, KeyCode.DownArrow, true); }
    public static bool EnterDown() { return Key(k => k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame, KeyCode.Return, true); }
    public static bool EscDown() { return Key(k => k.escapeKey.wasPressedThisFrame, KeyCode.Escape, true); }
    public static bool BackDown() { return Key(k => k.backspaceKey.wasPressedThisFrame, KeyCode.Backspace, true); }
    public static bool SpaceDown() { return Key(k => k.spaceKey.wasPressedThisFrame, KeyCode.Space, true); }
    public static bool Shift() { return Key(k => k.leftShiftKey.isPressed || k.rightShiftKey.isPressed, KeyCode.LeftShift, false); }
    public static bool MDown() { return Key(k => k.mKey.wasPressedThisFrame, KeyCode.M, true); }
    public static bool CDown() { return Key(k => k.cKey.wasPressedThisFrame, KeyCode.C, true); }
    public static bool PDown() { return Key(k => k.pKey.wasPressedThisFrame, KeyCode.P, true); }
    public static bool RDown() { return Key(k => k.rKey.wasPressedThisFrame, KeyCode.R, true); }
    public static bool QDown() { return Key(k => k.qKey.wasPressedThisFrame, KeyCode.Q, true); }
    public static bool AnyKeyDown() { return Key(k => k.anyKey.wasPressedThisFrame, KeyCode.Space, true); }

    public static bool MouseLeftDown()
    {
        Mouse m = Mouse.current;
        if (m != null) return m.leftButton.wasPressedThisFrame;
        try { return Input.GetMouseButtonDown(0); } catch { return false; }
    }

    public static Vector2 MousePos()
    {
        Mouse m = Mouse.current;
        if (m != null) return m.position.ReadValue();
        try { return Input.mousePosition; } catch { return Vector2.zero; }
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

    public static KIn Read()
    {
        var i = new KIn();
        i.steer = (Right() ? 1f : 0f) - (Left() ? 1f : 0f);
        i.gas = Up() ? 1f : 0f;
        i.brake = Down() ? 1f : 0f;
        i.fire = SpaceDown();
        i.drift = Shift();
        i.pause = EscDown() || PDown();
        return i;
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

    // Xbox layout: RT gas, LT brake / reverse, left stick steer, A also gas, B brake, X / RB fire, LB drift, Start pause
    public static KIn Read(Gamepad p)
    {
        var i = new KIn();
        if (p == null || !p.added) return i;
        Vector2 ls = Dead(p.leftStick.ReadValue(), 0.15f);
        i.steer = ls.x;
        if (p.dpad.left.isPressed) i.steer = -1f;
        if (p.dpad.right.isPressed) i.steer = 1f;
        i.gas = Mathf.Max(p.rightTrigger.ReadValue(), p.buttonSouth.isPressed ? 1f : 0f);
        i.brake = Mathf.Max(p.leftTrigger.ReadValue(), p.buttonEast.isPressed ? 1f : 0f);
        i.fire = p.buttonWest.wasPressedThisFrame || p.rightShoulder.wasPressedThisFrame;
        i.drift = p.leftShoulder.isPressed;
        i.pause = p.startButton.wasPressedThisFrame;
        return i;
    }

    public static bool AnyButtonDown(Gamepad p)
    {
        return p.buttonSouth.wasPressedThisFrame || p.buttonEast.wasPressedThisFrame || p.buttonWest.wasPressedThisFrame || p.buttonNorth.wasPressedThisFrame
            || p.startButton.wasPressedThisFrame || p.rightShoulder.wasPressedThisFrame || p.leftShoulder.wasPressedThisFrame
            || p.rightTrigger.wasPressedThisFrame || p.leftTrigger.wasPressedThisFrame;
    }

    // the raw signature used to spot a "ghost" pad that mirrors another one (same as the other Unity games)
    public static string Signature(Gamepad p)
    {
        Vector2 a = p.leftStick.ReadValue(), b = p.rightStick.ReadValue();
        return Mathf.RoundToInt(a.x * 20) + "," + Mathf.RoundToInt(a.y * 20) + "," + Mathf.RoundToInt(b.x * 20) + "," + Mathf.RoundToInt(b.y * 20)
            + (p.buttonSouth.isPressed ? "A" : "") + (p.buttonEast.isPressed ? "B" : "") + (p.buttonWest.isPressed ? "X" : "") + (p.buttonNorth.isPressed ? "Y" : "")
            + (p.leftShoulder.isPressed ? "L" : "") + (p.rightShoulder.isPressed ? "R" : "") + Mathf.RoundToInt(p.leftTrigger.ReadValue() * 10) + Mathf.RoundToInt(p.rightTrigger.ReadValue() * 10);
    }
}
