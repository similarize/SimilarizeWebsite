using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;

// ffu35 GHOST / DUPLICATE CONTROLLER GUARD (Bill, Tesla Cybertruck browser: one controller plugged in, the lobby looks
// fine, then in game a second froggy suddenly joins, driven by the same physical pad).
//
// Root cause (ffu1-34): the browser exposes the one pad twice (two Gamepad entries, or a re-connect under a new index /
// id / mapping string). The old ghost check (Game.IsGhostPress) only called a press a ghost when ANOTHER, already-joined
// pad had recorded an identical press within 150 ms. In the lobby both entries are read in the same loop, so the second
// one is caught. In play, the mid-game join loop ran BEFORE the joined slots recorded their press for the frame, so the
// first in-game A (= hop) on the mirror entry found no matching recent press -> Join() -> a second froggy on the same pad.
// A re-connect under a new device id did the same thing (the old froggy idled, a new one joined).
//
// Now (order independent, ported from games/shared/gamepad.js padmerge1):
//  * every pad's button-downs are timestamped once per frame (Tick, before anybody reads input);
//  * press coincidence: the same button going down on two devices within 120 ms twice - or an A / Start press once while
//    either device is unassigned (= a join attempt) - makes the later device an ALIAS of the other. Aliases never join and
//    never drive a froggy; a pair that has each pressed alone is known-distinct and never merged (two real pads of the
//    same model are fine); an alias that presses alone twice is released (a false merge heals itself);
//  * joins are only ever an explicit A / Start press, accepted 150 ms later (after the coincidence window), from a device
//    that is not assigned and not a mirror of an assigned one - never just because a device connected (presses in the
//    first 0.6 s after a device appears are ignored too);
//  * reconnects re-bind the existing froggy: a removed root's alias takes over its slot, a join press from a new device
//    while a froggy has lost its pad re-binds that froggy (same model first), and a new same-model device that shows up
//    while the bound pad has gone silent re-binds too;
//  * every device event (added / removed / reconnected / alias / release / join / rebind / blocked join) is logged to the
//    console with a "PADS:" prefix so a case like Bill's can be read from the browser console. &padlog=1 logs each press.
public static class PadGuard
{
    public class Info
    {
        public Gamepad pad;
        public int id;
        public string key = "";
        public float addedT, lastActT = -100f, lastPressT = -100f;
        public readonly float[] down = new float[NB];
        public readonly int[] downF = new int[NB];
        public int aliasOf = -1, soloSinceAlias;
        public bool removed;
        public string sig = "";
        public readonly HashSet<int> soloWith = new HashSet<int>();
    }
    class Ev { public int id, b, f; public float t; public bool coincident; }

    public const int NB = 14;
    public const float Win = 0.12f, Grace = 0.6f, JoinDelay = 0.15f;
    static readonly string[] BName = { "A", "B", "X", "Y", "LB", "RB", "LT", "RT", "Start", "View", "L3", "R3", "DUp", "DDown" };
    static readonly Dictionary<int, Info> infos = new Dictionary<int, Info>();
    static readonly Dictionary<long, int> coinc = new Dictionary<long, int>();
    static readonly HashSet<long> distinct = new HashSet<long>();
    static readonly List<Ev> pending = new List<Ev>();
    static readonly List<int> joinReady = new List<int>();
    static readonly List<float> joinReadyT = new List<float>();
    public static bool GraceOn = true;   // Game: on in play (a pad that just appeared can't join with that press), off in the lobby
    static readonly List<KeyValuePair<int, int>> promoted = new List<KeyValuePair<int, int>>();   // (new root, removed old root)
    static readonly List<KeyValuePair<int, int>> merges = new List<KeyValuePair<int, int>>();     // (alias, root)
    static readonly List<Ev> evs = new List<Ev>();
    static int tickFrame = -1;
    static bool hooked;
    public static bool Verbose;
    public static int Aliases, Releases, BlockedJoins, Joins;
    public static System.Func<Gamepad, bool> Assigned = p => false;

    static long Pair(int a, int b) { return a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a; }
    public static void Log(string s) { Debug.Log("PADS: " + s); }

    static void Hook()
    {
        if (hooked) return;
        hooked = true;
        try { InputSystem.onDeviceChange += OnChange; } catch (System.Exception e) { Log("no device-change hook: " + e.Message); }
        string u = FFPlatform.PageUrl ?? "";
        Verbose = u.Contains("padlog");
        Log("guard on (ffu35), " + Gamepad.all.Count + " pad(s) " + List());
    }

    public static string Describe(Gamepad p)
    {
        if (p == null) return "null";
        var d = p.description;
        return "#" + p.deviceId + " '" + p.displayName + "' product='" + d.product + "' maker='" + d.manufacturer + "' if=" + d.interfaceName + " layout=" + p.layout + " key=" + KeyOf(p) + (p.added ? "" : " (not added)");
    }

    public static string List()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var p in Gamepad.all) { if (p == null) continue; Info i; infos.TryGetValue(p.deviceId, out i); sb.Append("[").Append(Describe(p)); if (i != null && i.aliasOf >= 0) sb.Append(" ALIAS of #").Append(i.aliasOf); sb.Append("] "); }
        return sb.ToString();
    }

    // model key: USB vendor:product from the browser's id string when present ("Vendor: 045e Product: 02e0" = Chrome,
    // "045e-02e0-Xbox ..." = Firefox), else the product / display name without the mapping / API decorations
    public static string KeyOf(Gamepad p)
    {
        if (p == null) return "";
        var d = p.description;
        string s = ((d.product ?? "") + " " + (d.manufacturer ?? "")).Trim();
        if (s.Length == 0) s = p.displayName ?? p.name ?? "";
        var m = Regex.Match(s, @"Vendor:\s*([0-9a-fA-F]{4}).*?Product:\s*([0-9a-fA-F]{4})");
        if (m.Success) return (m.Groups[1].Value + ":" + m.Groups[2].Value).ToLowerInvariant();
        m = Regex.Match(s, @"\b([0-9a-fA-F]{4})-([0-9a-fA-F]{4})-");
        if (m.Success) return (m.Groups[1].Value + ":" + m.Groups[2].Value).ToLowerInvariant();
        string n = Regex.Replace(s.ToLowerInvariant(), @"\(.*?\)|standard gamepad|xinput|gamepad|controller|[^a-z]", "");
        return n.Length > 0 ? n : (p.layout ?? "pad");
    }

    static UnityEngine.InputSystem.Controls.ButtonControl Btn(Gamepad p, int b)
    {
        switch (b)
        {
            case 0: return p.buttonSouth; case 1: return p.buttonEast; case 2: return p.buttonWest; case 3: return p.buttonNorth;
            case 4: return p.leftShoulder; case 5: return p.rightShoulder; case 6: return p.leftTrigger; case 7: return p.rightTrigger;
            case 8: return p.startButton; case 9: return p.selectButton; case 10: return p.leftStickButton; case 11: return p.rightStickButton;
            case 12: return p.dpad.up; default: return p.dpad.down;
        }
    }

    static void OnChange(InputDevice dev, InputDeviceChange ch)
    {
        Gamepad p = dev as Gamepad;
        if (p == null) return;
        Log("device " + ch + " " + Describe(p) + " | now " + Gamepad.all.Count + " pad(s)");
        Info inf;
        if (ch == InputDeviceChange.Removed || ch == InputDeviceChange.Disconnected)
        {
            if (infos.TryGetValue(p.deviceId, out inf)) inf.removed = true;
            // the same physical pad lives on as one of its aliases: promote it, Game re-binds the froggy to it
            foreach (var o in infos.Values)
                if (o.aliasOf == p.deviceId && !o.removed)
                {
                    o.aliasOf = -1;
                    foreach (var o2 in infos.Values) if (o2 != o && o2.aliasOf == p.deviceId) o2.aliasOf = o.id;
                    promoted.Add(new KeyValuePair<int, int>(o.id, p.deviceId));
                    Log("#" + o.id + " takes over from removed #" + p.deviceId + " (it was its mirror)");
                    break;
                }
        }
        else if (ch == InputDeviceChange.Added || ch == InputDeviceChange.Reconnected)
        {
            if (infos.TryGetValue(p.deviceId, out inf)) { inf.removed = false; inf.addedT = Time.unscaledTime; }
        }
    }

    public static Info Get(Gamepad p) { Info i; return p != null && infos.TryGetValue(p.deviceId, out i) ? i : null; }
    public static bool IsAlias(Gamepad p) { var i = Get(p); return i != null && i.aliasOf >= 0; }
    public static Gamepad RootOf(Gamepad p) { var i = Get(p); if (i == null || i.aliasOf < 0) return p; Info r; return infos.TryGetValue(i.aliasOf, out r) ? r.pad : p; }
    public static bool IsRemoved(Gamepad p) { if (p == null || !p.added) return true; var i = Get(p); return i != null && i.removed; }
    public static bool SameModel(Gamepad a, Gamepad b) { return a != null && b != null && KeyOf(a) == KeyOf(b); }

    // a pad that has been silent since `since` (no stick / button change), for at least `quiet` seconds
    public static bool SilentSince(Gamepad p, float since, float quiet)
    {
        var i = Get(p);
        return i != null && i.lastActT < since && Time.unscaledTime - i.lastActT > quiet;
    }
    public static float AddedAt(Gamepad p) { var i = Get(p); return i != null ? i.addedT : 0f; }

    public static Gamepad TakeJoin()
    {
        while (joinReady.Count > 0)
        {
            int id = joinReady[0]; float jt = joinReadyT[0]; joinReady.RemoveAt(0); joinReadyT.RemoveAt(0);
            if (Time.unscaledTime - jt > 0.6f) continue;   // a menu had the press (keypad, story picker...) - stale
            Info i;
            if (infos.TryGetValue(id, out i) && i.pad != null && i.pad.added && i.aliasOf < 0) return i.pad;
        }
        return null;
    }
    public static bool TakePromotion(out Gamepad now, out Gamepad old)
    {
        now = old = null;
        while (promoted.Count > 0)
        {
            var kv = promoted[0]; promoted.RemoveAt(0);
            Info a, b;
            if (infos.TryGetValue(kv.Key, out a) && infos.TryGetValue(kv.Value, out b)) { now = a.pad; old = b.pad; return true; }
        }
        return false;
    }
    public static bool TakeMerge(out Gamepad alias, out Gamepad root)
    {
        alias = root = null;
        while (merges.Count > 0)
        {
            var kv = merges[0]; merges.RemoveAt(0);
            Info a, b;
            if (infos.TryGetValue(kv.Key, out a) && infos.TryGetValue(kv.Value, out b)) { alias = a.pad; root = b.pad; return true; }
        }
        return false;
    }

    // a device is part of an assigned device's mirror group (itself assigned, its root assigned, or one of its aliases)
    static bool GroupAssigned(Info i)
    {
        if (Assigned(i.pad)) return true;
        int root = i.aliasOf >= 0 ? i.aliasOf : i.id;
        foreach (var o in infos.Values)
            if (!o.removed && o.pad != null && (o.id == root || o.aliasOf == root) && Assigned(o.pad)) return true;
        return false;
    }

    // once per frame, before any input is read (Game.Update calls it first)
    public static void Tick()
    {
        if (Time.frameCount == tickFrame) return;
        tickFrame = Time.frameCount;
        Hook();
        float now = Time.unscaledTime;
        foreach (var p in Gamepad.all)
        {
            if (p == null) continue;
            Info inf;
            if (!infos.TryGetValue(p.deviceId, out inf))
            {
                inf = new Info { pad = p, id = p.deviceId, key = KeyOf(p), addedT = now };
                infos[p.deviceId] = inf;
                Log("seen " + Describe(p) + " | " + Gamepad.all.Count + " pad(s) now");
            }
            inf.pad = p; inf.removed = false;
            string sg = Pads.Signature(p) + (p.startButton.isPressed ? "S" : "") + (p.selectButton.isPressed ? "V" : "") + (p.dpad.up.isPressed ? "u" : "") + (p.dpad.down.isPressed ? "d" : "") + (p.dpad.left.isPressed ? "l" : "") + (p.dpad.right.isPressed ? "r" : "");
            if (sg != inf.sig) { inf.sig = sg; inf.lastActT = now; }
        }
        evs.Clear();
        foreach (var p in Gamepad.all)
        {
            if (p == null) continue;
            Info inf = infos[p.deviceId];
            for (int b = 0; b < NB; b++)
            {
                var c = Btn(p, b);
                if (c == null || !c.wasPressedThisFrame) continue;
                if (GraceOn && now - inf.addedT < Grace) { if (Verbose) Log("#" + inf.id + " " + BName[b] + " ignored (device just appeared)"); continue; }
                inf.down[b] = now; inf.downF[b] = Time.frameCount; inf.lastPressT = now;
                var e = new Ev { id = inf.id, b = b, t = now, f = Time.frameCount };
                evs.Add(e); pending.Add(e);
                if (Verbose) Log("#" + inf.id + " " + BName[b] + " down" + (Assigned(p) ? " (assigned)" : "") + (inf.aliasOf >= 0 ? " (alias of #" + inf.aliasOf + ")" : ""));
            }
        }
        // coincidences: the same button down on another device within the window (both directions; same-frame pairs once)
        foreach (var e in evs)
            foreach (var o in infos.Values)
            {
                if (o.id == e.id || o.removed || o.pad == null) continue;
                float dt = Mathf.Abs(o.down[e.b] - e.t);
                if (dt > Win && !(o.down[e.b] > 0f && Time.frameCount - o.downF[e.b] <= 2)) continue;   // 120 ms, or 2 frames at a low frame rate
                e.coincident = true;
                foreach (var pe in pending) if (pe.id == o.id && pe.b == e.b && (Mathf.Abs(pe.t - e.t) <= Win || Time.frameCount - o.downF[e.b] <= 2)) pe.coincident = true;
                if (dt == 0f && o.id > e.id && evs.Exists(x => x.id == o.id && x.b == e.b)) continue;   // counted from the other side
                Coincide(infos[e.id], o, e.b);
            }
        // a join press (A / Start on an unassigned device) that lands with ANY button-down on an assigned device: a
        // mirror whose buttons are numbered differently still can't sneak in a second froggy
        foreach (var e in evs)
        {
            if (e.coincident || (e.b != 0 && e.b != 8)) continue;
            Info me = infos[e.id];
            if (Assigned(me.pad)) continue;
            foreach (var o in infos.Values)
            {
                if (o.id == e.id || o.removed || o.pad == null || !Assigned(o.pad)) continue;
                float last = -100f; foreach (float d in o.down) last = Mathf.Max(last, d);
                if (Mathf.Abs(last - e.t) > Win) continue;
                e.coincident = true;
                Coincide(me, o, e.b);
            }
        }
        // matured presses: solo presses (known-distinct / alias release) and clean join presses
        for (int k = pending.Count - 1; k >= 0; k--)
        {
            Ev e = pending[k];
            if (now - e.t < JoinDelay || Time.frameCount - e.f < 3) continue;
            pending.RemoveAt(k);
            Info inf;
            if (!infos.TryGetValue(e.id, out inf) || inf.pad == null || !inf.pad.added) continue;
            if (!e.coincident)
            {
                foreach (var o in infos.Values) if (o.id != inf.id && !o.removed) inf.soloWith.Add(o.id);
                foreach (var o in infos.Values)
                    if (o.id != inf.id && !o.removed && o.soloWith.Contains(inf.id) && inf.soloWith.Contains(o.id) && distinct.Add(Pair(o.id, inf.id)))
                        Log("#" + inf.id + " and #" + o.id + " are two real controllers (each pressed alone)");
                if (inf.aliasOf >= 0 && ++inf.soloSinceAlias >= 2)
                {
                    Log("#" + inf.id + " released (pressed alone twice; not a mirror of #" + inf.aliasOf + ")");
                    distinct.Add(Pair(inf.id, inf.aliasOf));
                    inf.aliasOf = -1; Releases++;
                }
            }
            if (e.b != 0 && e.b != 8) continue;   // joins: A / Start only
            if (Assigned(inf.pad)) continue;
            if (inf.aliasOf >= 0) { BlockedJoins++; Log("blocked join from #" + inf.id + " (mirror of #" + inf.aliasOf + ")"); continue; }
            if (GroupAssigned(inf)) { BlockedJoins++; Log("blocked join from #" + inf.id + " (its mirror is already playing)"); continue; }
            if (!joinReady.Contains(inf.id)) { joinReady.Add(inf.id); joinReadyT.Add(now); Joins++; }
        }
    }

    static void Coincide(Info a, Info b, int button)
    {
        long k = Pair(a.id, b.id);
        if (distinct.Contains(k)) { if (Verbose) Log("#" + a.id + "/#" + b.id + " same " + BName[button] + " but known distinct"); return; }
        int n; coinc.TryGetValue(k, out n); coinc[k] = ++n;
        bool aAs = Assigned(a.pad), bAs = Assigned(b.pad);
        bool join = (button == 0 || button == 8) && (!aAs || !bAs);
        if (Verbose) Log("#" + a.id + "/#" + b.id + " pressed " + BName[button] + " together (" + n + ")");
        if (n < 2 && !join) return;
        // two froggies already playing: only identical full pad state, three times, merges them (real players press
        // something alone within seconds and become known-distinct long before that)
        if (aAs && bAs && (n < 3 || a.sig != b.sig)) return;
        // the alias: the unassigned one; both / neither assigned -> the newer device (higher id)
        Info alias, root;
        if (aAs != bAs) { alias = aAs ? b : a; root = aAs ? a : b; }
        else { alias = a.id > b.id ? a : b; root = alias == a ? b : a; }
        int rootId = root.aliasOf >= 0 ? root.aliasOf : root.id;
        if (rootId == alias.id) return;
        if (alias.aliasOf == rootId) return;
        // anything that was an alias of the new alias follows it to the root
        foreach (var o in infos.Values) if (o.aliasOf == alias.id) o.aliasOf = rootId;
        alias.aliasOf = rootId; alias.soloSinceAlias = 0; Aliases++;
        int ji = joinReady.IndexOf(alias.id); if (ji >= 0) { joinReady.RemoveAt(ji); joinReadyT.RemoveAt(ji); }
        Log("#" + alias.id + " is a MIRROR of #" + rootId + " (" + BName[button] + " pressed together " + n + "x) - it can't join or drive a froggy");
        merges.Add(new KeyValuePair<int, int>(alias.id, rootId));
    }

    // ---- synthetic test support (probe ?ffshot=padtest) ----
    public static void ForgetDevice(int id) { infos.Remove(id); }
}
