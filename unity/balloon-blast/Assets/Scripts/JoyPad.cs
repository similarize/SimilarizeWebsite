// xbm1 (Games SME, 2026-10-20): Joystick -> Gamepad fallback for WebGL.
// A browser pad without the "standard" mapping (Gamepad API mapping == "") reaches the Input System as a
// WebGLJoystick, not a Gamepad, so code that reads Gamepad.all ignores it. For every such Joystick this adds a
// virtual Gamepad ("JoyPad") and copies the joystick's sticks/buttons into it each update, assuming the usual
// standard button/axis order (0 A, 1 B, 2 X, 3 Y, 4 LB, 5 RB, 6 LT, 7 RT, 8 View, 9 Menu, 10 LS, 11 RS,
// 12-15 d-pad; axes 0/1 left stick, 2/3 right stick). Every Gamepad.all reader then works unchanged.
// One frame of latency (state is queued after an update and processed in the next). No gameplay changes.
// ?paddebug=1 logs every pad / joystick (layout, buttons, axes) once every 2 s.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

public static class JoyPad
{
    static readonly Dictionary<int, Gamepad> map = new Dictionary<int, Gamepad>();   // joystick deviceId -> virtual pad
    static readonly Dictionary<int, GamepadState> last = new Dictionary<int, GamepadState>();
    static bool debug;
    static float nextLog;

    static readonly GamepadButton[] order = {
        GamepadButton.South, GamepadButton.East, GamepadButton.West, GamepadButton.North,
        GamepadButton.LeftShoulder, GamepadButton.RightShoulder, GamepadButton.LeftTrigger, GamepadButton.RightTrigger,
        GamepadButton.Select, GamepadButton.Start, GamepadButton.LeftStick, GamepadButton.RightStick,
        GamepadButton.DpadUp, GamepadButton.DpadDown, GamepadButton.DpadLeft, GamepadButton.DpadRight };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        string url = "";
        try { url = Application.absoluteURL ?? ""; } catch { }
        debug = url.Contains("paddebug=1");
        foreach (var d in InputSystem.devices) if (d is Joystick j) Attach(j);
        InputSystem.onDeviceChange += (d, ch) =>
        {
            if (!(d is Joystick j)) return;
            if (ch == InputDeviceChange.Added || ch == InputDeviceChange.Reconnected) Attach(j);
            else if (ch == InputDeviceChange.Removed || ch == InputDeviceChange.Disconnected) Detach(j);
        };
        InputSystem.onAfterUpdate += Pump;
    }

    static void Attach(Joystick j)
    {
        if (map.ContainsKey(j.deviceId) || Btn(j, 0) == null) return;
        var pad = InputSystem.AddDevice<Gamepad>("JoyPad");
        map[j.deviceId] = pad;
        Debug.Log("JoyPad: non-standard pad '" + j.displayName + "' #" + j.deviceId + " mapped to Gamepad #" + pad.deviceId);
    }

    static void Detach(Joystick j)
    {
        Gamepad pad;
        if (!map.TryGetValue(j.deviceId, out pad)) return;
        map.Remove(j.deviceId); last.Remove(j.deviceId);
        if (pad.added) InputSystem.RemoveDevice(pad);
    }

    static ButtonControl Btn(Joystick j, int i) { return j.TryGetChildControl<ButtonControl>("Button " + (i + 1)); }
    static float Ax(Joystick j, int n) { var a = j.TryGetChildControl<AxisControl>("Axis " + n); return a != null ? a.ReadValue() : 0f; }

    static void Pump()
    {
        foreach (var kv in map)
        {
            var j = InputSystem.GetDeviceById(kv.Key) as Joystick;
            if (j == null || !kv.Value.added) continue;
            var s = new GamepadState();
            if (j.stick != null) s.leftStick = j.stick.ReadValue();
            s.rightStick = new Vector2(Ax(j, 1), -Ax(j, 2));   // raw axes 2/3 (browser +y = down)
            for (int i = 0; i < order.Length; i++)
            {
                var b = Btn(j, i); if (b == null) continue;
                float v = b.ReadValue();
                if (i == 6) s.leftTrigger = v;
                if (i == 7) s.rightTrigger = v;
                if (b.isPressed) s = s.WithButton(order[i], true);
            }
            GamepadState prev;
            if (last.TryGetValue(kv.Key, out prev) && Same(prev, s)) continue;
            last[kv.Key] = s;
            InputSystem.QueueStateEvent(kv.Value, s);
        }
        if (debug && Time.unscaledTime >= nextLog) { nextLog = Time.unscaledTime + 2f; Log(); }
    }

    static bool Same(GamepadState a, GamepadState b)
    {
        return a.buttons == b.buttons && a.leftStick == b.leftStick && a.rightStick == b.rightStick &&
               a.leftTrigger == b.leftTrigger && a.rightTrigger == b.rightTrigger;
    }

    static void Log()
    {
        var sb = new System.Text.StringBuilder("paddebug:");
        foreach (var d in InputSystem.devices)
        {
            if (!(d is Gamepad) && !(d is Joystick)) continue;
            sb.Append(" | #").Append(d.deviceId).Append(' ').Append(d.layout).Append(" '").Append(d.displayName).Append("' pressed:");
            foreach (var c in d.allControls) if (c is ButtonControl b && b.isPressed && !b.synthetic) sb.Append(' ').Append(c.name);
            if (d is Gamepad g) sb.Append(" L").Append(g.leftStick.ReadValue()).Append(" R").Append(g.rightStick.ReadValue());
            if (d is Joystick jj && jj.stick != null) sb.Append(" stick").Append(jj.stick.ReadValue());
        }
        Debug.Log(sb.ToString());
    }
}
