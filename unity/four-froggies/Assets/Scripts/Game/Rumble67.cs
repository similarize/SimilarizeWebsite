using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// ffu67c RUMBLE (Bill Oct 20 13:52: "when one player gets in a mech, ALL controllers start rumbling constantly, and it KEEPS rumbling
// after getting out"). Causes: Quake() pulsed EVERY nearby player's pad on each mech footfall, and a single rumblePad / rumbleEnd only
// remembered the LAST pad, so the others were never set back to zero. Now:
//   * a pulse goes only to the pad of the player who caused it (the pilot: PilotKick, the mech's own footfalls via MechStomp.Foot pilot);
//   * every pulsed pad has its own end time (safety cap 0.4 s - a pulse can never stick), checked every frame in every state;
//   * zero on vehicle exit / bail out / deploy (Frog.ExitVehicle), world change (Frog.SendTo), pause (help opens), app focus loss /
//     pause, leaving the seat; only pads we pulsed are touched (no Gamepad.current, no all-pads calls).
// Log: "RUMBLE67 ..." per pulse / stop; RumbleActive = pads currently non-zero (probe).
public partial class Game
{
    const float RumbleMax = 0.4f;
    readonly Dictionary<Gamepad, float> rumbleEnds = new Dictionary<Gamepad, float>();
    public static int RumblePulses, RumbleStops;
    public int RumbleActive { get { return rumbleEnds.Count; } }
    // per-pad tallies for the probe (each player's own rumble state)
    public readonly Dictionary<Gamepad, int> PadPulses = new Dictionary<Gamepad, int>();
    public readonly Dictionary<Gamepad, float> PadMaxAmp = new Dictionary<Gamepad, float>();
    public int PulsesOf(Gamepad p) { int n; return p != null && PadPulses.TryGetValue(p, out n) ? n : 0; }
    public float MaxAmpOf(Gamepad p) { float a; return p != null && PadMaxAmp.TryGetValue(p, out a) ? a : 0f; }
    Slot SlotOfFrog(Frog f) { if (f == null) return null; foreach (var s in slots) if (frogs[s.frog] == f) return s; return null; }

    void Pulse(Slot s, float lo, float hi, float secs, string why)
    {
        if (s == null || s.kind != InputKind.Gamepad || s.pad == null || !s.pad.added || help) return;
        try { s.pad.SetMotorSpeeds(Mathf.Clamp01(lo), Mathf.Clamp01(hi)); } catch { return; }
        float end = Time.unscaledTime + Mathf.Min(secs, RumbleMax);
        float old; if (!rumbleEnds.TryGetValue(s.pad, out old) || old < end) rumbleEnds[s.pad] = end;
        RumblePulses++;
        int pn; PadPulses.TryGetValue(s.pad, out pn); PadPulses[s.pad] = pn + 1;
        float pa; PadMaxAmp.TryGetValue(s.pad, out pa); PadMaxAmp[s.pad] = Mathf.Max(pa, Mathf.Max(lo, hi));
        if (RumblePulses <= 40 || RumblePulses % 50 == 0) Debug.Log("RUMBLE67 pulse P" + (slots.IndexOf(s) + 1) + " " + why + " lo " + lo.ToString("0.00") + " hi " + hi.ToString("0.00") + " " + secs.ToString("0.00") + " s | active pads " + rumbleEnds.Count);
    }
    void StopPad(Gamepad p, string why)
    {
        if (p == null) return;
        try { if (p.added) p.SetMotorSpeeds(0f, 0f); } catch { }
        if (rumbleEnds.Remove(p)) { RumbleStops++; Debug.Log("RUMBLE67 zero " + p.displayName + " #" + p.deviceId + " (" + why + ") | active pads " + rumbleEnds.Count); }
    }
    public void RumbleStop(Frog f, string why) { Slot s = SlotOfFrog(f); if (s != null && s.pad != null) StopPad(s.pad, why); }
    public void RumbleStopAll(string why)
    {
        if (rumbleEnds.Count == 0) return;
        foreach (var p in new List<Gamepad>(rumbleEnds.Keys)) StopPad(p, why);
    }
    // every frame (Update, any state): expire pulses; a pad whose player left / pad gone is zeroed too
    void TickRumble()
    {
        if (rumbleEnds.Count == 0) return;
        float now = Time.unscaledTime;
        List<Gamepad> done = null;
        foreach (var kv in rumbleEnds)
        {
            bool seated = false; foreach (var s in slots) if (s.pad == kv.Key) { seated = true; break; }
            if (now > kv.Value || !seated || help || state != State.Play) (done ?? (done = new List<Gamepad>())).Add(kv.Key);
        }
        if (done != null) foreach (var p in done) StopPad(p, now > rumbleEnds[p] ? "pulse over" : help ? "pause" : state != State.Play ? "not playing" : "player left");
    }
    void OnApplicationFocus(bool on) { if (!on) RumbleStopAll("app focus lost"); }
    void OnApplicationPause(bool paused) { if (paused) RumbleStopAll("app paused"); }
    void OnDisable() { RumbleStopAll("disabled"); }
}
