using UnityEngine;
using UnityEngine.InputSystem;

// Keyboard + mouse helpers: new Input System first, legacy Input Manager as a fallback.
public static class Kb
{
    public static Vector2 Move()
    {
        Vector2 v = Vector2.zero;
        Keyboard k = Keyboard.current;
        if (k != null)
        {
            if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y += 1;
            if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y -= 1;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1;
            if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1;
        }
        else
        {
            try
            {
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v.y += 1;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v.y -= 1;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) v.x += 1;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) v.x -= 1;
            }
            catch { }
        }
        return Vector2.ClampMagnitude(v, 1f);
    }

    public static bool EnterDown()
    {
        Keyboard k = Keyboard.current;
        if (k != null) return k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame;
        try { return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter); } catch { return false; }
    }

    public static bool EscDown()
    {
        Keyboard k = Keyboard.current;
        if (k != null) return k.escapeKey.wasPressedThisFrame || k.backspaceKey.wasPressedThisFrame;
        try { return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace); } catch { return false; }
    }

    public static bool JumpDown()
    {
        Keyboard k = Keyboard.current;
        if (k != null) return k.spaceKey.wasPressedThisFrame;
        try { return Input.GetKeyDown(KeyCode.Space); } catch { return false; }
    }

    public static bool ReloadDown()
    {
        Keyboard k = Keyboard.current;
        if (k != null) return k.rKey.wasPressedThisFrame;
        try { return Input.GetKeyDown(KeyCode.R); } catch { return false; }
    }

    public static bool FDown()
    {
        Keyboard k = Keyboard.current;
        if (k != null) return k.fKey.wasPressedThisFrame;
        try { return Input.GetKeyDown(KeyCode.F); } catch { return false; }
    }

    public static bool LeftDown()
    {
        Keyboard k = Keyboard.current;
        if (k != null) return k.leftArrowKey.wasPressedThisFrame;
        try { return Input.GetKeyDown(KeyCode.LeftArrow); } catch { return false; }
    }

    public static bool RightDown()
    {
        Keyboard k = Keyboard.current;
        if (k != null) return k.rightArrowKey.wasPressedThisFrame;
        try { return Input.GetKeyDown(KeyCode.RightArrow); } catch { return false; }
    }

    // screen positions of touches that began this frame
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

    public static Vector2 MouseDelta()
    {
        Mouse m = Mouse.current;
        if (m != null) return m.delta.ReadValue();
        try { return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f; } catch { return Vector2.zero; }
    }

    public static int TouchCount()
    {
        try { return Input.touchCount; } catch { return 0; }
    }

    public static bool AnyTouchBegan()
    {
        try
        {
            for (int i = 0; i < Input.touchCount; i++)
                if (Input.GetTouch(i).phase == UnityEngine.TouchPhase.Began) return true;
        }
        catch { }
        return false;
    }
}
