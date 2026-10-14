using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

// lambo8 ONLINE HOST / JOIN over the internet (Bill), reusing Four Froggies' online stack (ffu13): PeerJS over WebRTC data
// channels with the free 0.peerjs.com signalling server (Plugins/WebGL/LBNet.jslib, a copy of FFNet.jslib with its own
// "lamboblast-" peer ids), star topology (the host relays), a 4-character room code from ABCDEFGHJKLMNPQRSTUVWXYZ23456789.
//
// Roles / authority:
//  * one player per device online (local split-screen seats are refused while a room is open);
//  * the HOST owns the race: track, line-up (who drives which kart, car + robot), the start (countdown), every AI kart, the
//    finishing order / places; it relays each guest's kart to the other guests;
//  * each device simulates its own kart; karts owned by another device are puppets (Kart.netPuppet): no local physics,
//    drawn ~0.1 s in the past, interpolated between 15 Hz snapshots (position, yaw, speed, lateral slip, vertical speed,
//    lap, track distance, drift / boost / spin / shield / respawn flags, finish time);
//  * items: the firing device sends F (kart, item) and every other device replays Items.Use on that kart (same shots /
//    hazards everywhere); a hit is decided by the SHOOTER's device - if the victim lives elsewhere it gets an H message
//    and applies the spin-out itself (Kart.Hit routing). Item boxes are local; puppets never pick them up.
// Messages ('|' fields): guest->host H(ello)|name|car|robot, C|car|robot, S|state, P (keep-alive);
// host->guests L|lineup (lobby, on change + every 2 s), G|track|lineup (race start), S|state..., O|kart|place|time
// (finish order), X|full, B (back to the lobby). Both ways: F|kart|item|pos|yaw, H... see Hit below (V|victim|item|shooter).
// Disconnects: a guest gone / silent 20 s -> its kart turns into an AI racer on the host; the host gone -> guests carry on
// offline (every puppet back to local AI).
[DefaultExecutionOrder(-45)]
public class LBNet : MonoBehaviour
{
    public static LBNet I;
    public enum Role { Off, Host, Guest }
    public Role role = Role.Off;
    public bool connected;
    public string code = "", myId = "", info = "";
    public float infoT;
    public const string CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    const float SendDt = 1f / 15f, Delay = 0.1f, Silence = 20f;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public bool Online { get { return role != Role.Off; } }
    public bool IsHost { get { return role == Role.Host; } }
    public bool IsGuest { get { return role == Role.Guest; } }
    public static bool On { get { return I != null && I.role != Role.Off; } }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void LBNet_Init();
    [DllImport("__Internal")] static extern void LBNet_Host(string code);
    [DllImport("__Internal")] static extern void LBNet_Join(string code);
    [DllImport("__Internal")] static extern void LBNet_Send(string to, string msg);
    [DllImport("__Internal")] static extern void LBNet_Drop(string id);
    [DllImport("__Internal")] static extern void LBNet_Leave();
    [DllImport("__Internal")] static extern string LBNet_Poll();
#else
    static void LBNet_Init() { }
    static void LBNet_Host(string code) { Debug.Log("LBNet (editor): no network"); }
    static void LBNet_Join(string code) { Debug.Log("LBNet (editor): no network"); }
    static void LBNet_Send(string to, string msg) { }
    static void LBNet_Drop(string id) { }
    static void LBNet_Leave() { }
    static string LBNet_Poll() { return null; }
#endif

    // players in the room (host's view; guests get it in L): owner "host" or a guest peer id
    public class Player { public string id = "", name = ""; public int car, robot, kart = -1; public float lastRx; }
    public readonly List<Player> players = new List<Player>();
    // per kart: "" = AI on the host, "host", or a guest id
    public readonly string[] kartOwner = new string[Game.Racers];
    public int myKart = -1;
    public bool raceOn;
    float hostLastRx, sendT, lobbyT, logT, joinStartT;
    int rxCount, txCount;

    struct Snap { public float t, yaw, spd, lat, vy, s, fin; public Vector3 pos; public int lap, flags; }
    class Remote { public readonly List<Snap> buf = new List<Snap>(); public float offset = float.NaN; public float lastRx; }
    readonly Remote[] rem = new Remote[Game.Racers];

    void Awake()
    {
        I = this;
        for (int i = 0; i < rem.Length; i++) { rem[i] = new Remote(); kartOwner[i] = ""; }
        LBNet_Init();
    }

    // ---------------- helpers ----------------
    static string F(float v) { return v.ToString("0.###", Inv); }
    static float PF(string s) { float v; return float.TryParse(s, NumberStyles.Float, Inv, out v) ? v : 0f; }
    static int PI(string s) { int v; return int.TryParse(s, NumberStyles.Integer, Inv, out v) ? v : 0; }
    void Say(string s) { info = s; infoT = Time.unscaledTime; Debug.Log("NET: " + s); }
    void Send(string to, string m) { txCount++; LBNet_Send(to, m); }
    public static string CleanCode(string s)
    {
        if (s == null) return "";
        var sb = new StringBuilder();
        foreach (char c in s.ToUpperInvariant()) if (CodeChars.IndexOf(c) >= 0) sb.Append(c);
        return sb.Length > 6 ? sb.ToString().Substring(0, 6) : sb.ToString();
    }
    public static string CleanName(string s)
    {
        if (s == null) return null;
        s = s.Trim().ToUpperInvariant();
        if (s.Length > 4) return null;
        foreach (char c in s) if (c < 'A' || c > 'Z') return null;
        return s;
    }
    public bool OwnsKart(int k)
    {
        if (role == Role.Off || k < 0 || k >= kartOwner.Length) return true;
        string o = kartOwner[k];
        if (role == Role.Host) return o.Length == 0 || o == "host";
        return o == myId;
    }
    public string OwnerName(int k)
    {
        if (role == Role.Off || k < 0 || k >= kartOwner.Length || kartOwner[k].Length == 0) return null;
        foreach (var p in players) if (p.id == kartOwner[k]) return p.name.Length > 0 ? p.name : (p.id == "host" ? "HOST" : "ONLINE");
        return "ONLINE";
    }

    // ---------------- public API (Game) ----------------
    public void Host(string want)
    {
        Leave(null);
        role = Role.Host; connected = false; raceOn = false;
        code = CleanCode(want);
        if (code.Length < 4) { code = ""; for (int i = 0; i < 4; i++) code += CodeChars[Random.Range(0, CodeChars.Length)]; }
        players.Clear();
        players.Add(new Player { id = "host", name = "HOST", lastRx = Time.unscaledTime });
        Say("Opening room " + code + "...");
        LBNet_Host(code);
    }

    public void Join(string c)
    {
        Leave(null);
        c = CleanCode(c);
        if (c.Length < 3) { Say("Type the 4-character room code from the host's screen"); return; }
        role = Role.Guest; connected = false; code = c; raceOn = false; joinStartT = Time.unscaledTime;
        Say("Joining room " + code + "...");
        LBNet_Join(code);
    }

    public void Leave(string why)
    {
        if (role != Role.Off) { LBNet_Leave(); Debug.Log("NET leave: " + (why ?? "")); }
        role = Role.Off; connected = false; raceOn = false; myId = ""; myKart = -1;
        players.Clear();
        for (int i = 0; i < kartOwner.Length; i++) kartOwner[i] = "";
        if (Game.I != null) foreach (var k in Game.I.karts) { k.netPuppet = false; k.netName = null; }
        if (why != null) { Say(why); if (Game.I != null) Game.I.NetToast(why); }
    }

    // local picks changed (guest asks the host; host updates its own entry)
    public void PicksChanged(int car, int robot)
    {
        if (role == Role.Guest && connected) Send("host", "C|" + car + "|" + robot);
        if (role == Role.Host && players.Count > 0) { players[0].car = car; players[0].robot = robot; BroadcastLineup(); }
    }

    // ---------------- host: line-up ----------------
    // players' karts: host = kart 0, guests 1.. in join order; a clash of car / robot is resolved to a free one (host wins)
    void ResolveLineup()
    {
        var usedC = new bool[8]; var usedR = new bool[Robots.Count];
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            p.kart = i;
            if (usedC[p.car]) { for (int c = 0; c < 8; c++) if (!usedC[c]) { p.car = c; break; } }
            usedC[p.car] = true;
            if (usedR[p.robot]) { for (int r = 0; r < Robots.Count; r++) if (!usedR[r]) { p.robot = r; break; } }
            usedR[p.robot] = true;
        }
        for (int k = 0; k < kartOwner.Length; k++) kartOwner[k] = "";
        foreach (var p in players) if (p.kart >= 0 && p.kart < kartOwner.Length) kartOwner[p.kart] = p.id;
    }

    string LineupStr()
    {
        var sb = new StringBuilder();
        sb.Append(players.Count);
        foreach (var p in players) sb.Append('|').Append(p.id).Append('~').Append(p.name).Append('~').Append(p.car).Append('~').Append(p.robot).Append('~').Append(p.kart);
        return sb.ToString();
    }

    void ParseLineup(string[] f, int at)
    {
        players.Clear();
        int n = PI(f[at]);
        for (int i = 0; i < n && at + 1 + i < f.Length; i++)
        {
            var q = f[at + 1 + i].Split('~');
            if (q.Length < 5) continue;
            players.Add(new Player { id = q[0], name = q[1], car = PI(q[2]), robot = PI(q[3]), kart = PI(q[4]) });
        }
        for (int k = 0; k < kartOwner.Length; k++) kartOwner[k] = "";
        foreach (var p in players) if (p.kart >= 0 && p.kart < kartOwner.Length) kartOwner[p.kart] = p.id;
        myKart = -1;
        foreach (var p in players) if (p.id == myId) myKart = p.kart;
    }

    public void BroadcastLineup()
    {
        if (role != Role.Host) return;
        ResolveLineup();
        if (Game.I != null) Game.I.NetLineup();
        Send("*", "L|" + (raceOn ? 1 : 0) + "|" + Game.CurrentTrack + "|" + LineupStr());
    }

    // host pressed RACE
    public void HostStart(int track)
    {
        if (role != Role.Host) return;
        ResolveLineup();
        raceOn = true;
        for (int i = 0; i < rem.Length; i++) { rem[i].buf.Clear(); rem[i].offset = float.NaN; }
        Send("*", "G|" + track + "|" + LineupStr());
        Debug.Log("NET race start track " + track + " | " + LineupStr());
    }
    public void HostBackToLobby() { if (role == Role.Host) { raceOn = false; Send("*", "B"); } }
    public void HostFinish(int k, int place, float t) { if (role == Role.Host) Send("*", "O|" + k + "|" + place + "|" + F(t)); }

    // ---------------- items / hits ----------------
    public void SendFire(Kart k, Item it)
    {
        if (role == Role.Off) return;
        string m = "F|" + k.id + "|" + (int)it + "|" + F(k.transform.position.x) + "|" + F(k.transform.position.y) + "|" + F(k.transform.position.z) + "|" + F(k.yaw) + "|" + F(k.speed);
        if (role == Role.Host) Send("*", m); else if (connected) Send("host", m);
    }
    public void SendHit(Kart victim, Item it, Kart from)
    {
        if (role == Role.Off) return;
        string m = "V|" + victim.id + "|" + (int)it + "|" + (from != null ? from.id : -1);
        if (role == Role.Host) { string o = kartOwner[victim.id]; if (o.Length > 0 && o != "host") Send(o, m); }
        else if (connected) Send("host", m);
        Debug.Log("NET hit k" + victim.id + " by " + (from != null ? "k" + from.id : "-") + " " + it);
    }

    // ---------------- per frame ----------------
    void Update()
    {
        Poll();
        if (role == Role.Off || Game.I == null) return;
        float now = Time.unscaledTime;
        if (role == Role.Host)
        {
            for (int i = players.Count - 1; i >= 1; i--)
                if (now - players[i].lastRx > Silence) DropGuest(players[i].id, players[i].name + " timed out");
            lobbyT += Time.unscaledDeltaTime;
            if (lobbyT > 2f) { lobbyT = 0f; Send("*", "L|" + (raceOn ? 1 : 0) + "|" + Game.CurrentTrack + "|" + LineupStr()); }
        }
        else
        {
            if (!connected && now - joinStartT > 45f) { Leave("No answer from room " + code); return; }
            if (connected && now - hostLastRx > Silence) { Leave("Lost the host - playing offline"); return; }
            lobbyT += Time.unscaledDeltaTime;
            if (connected && lobbyT > 2f) { lobbyT = 0f; Send("host", "P"); }
        }
        // states
        sendT += Time.unscaledDeltaTime;
        if (raceOn && sendT >= SendDt)
        {
            sendT = 0f;
            var sb = new StringBuilder("S");
            int n = 0;
            foreach (var k in Game.I.karts) if (OwnsKart(k.id)) { sb.Append('|').Append(StateOf(k)); n++; }
            if (n > 0) { if (role == Role.Host) Send("*", sb.ToString()); else if (connected) Send("host", sb.ToString()); }
        }
        if (raceOn) PosePuppets();
        if (now - logT > 5f && (raceOn || Kart.Autopilot))
        {
            logT = now;
            var sb = new StringBuilder();
            foreach (var k in Game.I.karts) if (!OwnsKart(k.id)) sb.Append(" k").Append(k.id).Append(" lap ").Append(k.lap).Append(" s ").Append(k.s.ToString("F0")).Append(" buf ").Append(rem[k.id].buf.Count);
            Debug.Log("NET state " + role + " room " + code + " players " + players.Count + " myKart " + myKart + " rx " + rxCount + " tx " + txCount + " race " + raceOn + " | puppets:" + sb);
        }
    }

    string StateOf(Kart k)
    {
        int fl = (k.drifting ? 1 : 0) | (k.boostT > 0f ? 2 : 0) | (k.spinT > 0f ? 4 : 0) | (k.shieldT > 0f ? 8 : 0) | (k.respawning ? 16 : 0) | (k.grounded ? 32 : 0) | (k.finished ? 64 : 0);
        Vector3 p = k.transform.position;
        return k.id + "~" + F(p.x) + "~" + F(p.y) + "~" + F(p.z) + "~" + F(k.yaw) + "~" + F(k.speed) + "~" + F(k.lat) + "~" + F(k.vy) + "~" + k.lap + "~" + F(k.s) + "~" + fl + "~" + F(k.finished ? k.finishTime : 0f) + "~" + F(Time.unscaledTime);
    }

    void OnStates(string[] f, string from)
    {
        for (int i = 1; i < f.Length; i++)
        {
            var q = f[i].Split('~');
            if (q.Length < 13) continue;
            int k = PI(q[0]);
            if (k < 0 || k >= rem.Length || OwnsKart(k)) continue;
            if (role == Role.Host && kartOwner[k] != from) continue;      // a guest may only move its own kart
            var s = new Snap { pos = new Vector3(PF(q[1]), PF(q[2]), PF(q[3])), yaw = PF(q[4]), spd = PF(q[5]), lat = PF(q[6]), vy = PF(q[7]), lap = PI(q[8]), s = PF(q[9]), flags = PI(q[10]), fin = PF(q[11]), t = PF(q[12]) };
            Remote r = rem[k];
            float off = Time.unscaledTime - s.t;
            r.offset = float.IsNaN(r.offset) ? off : Mathf.Min(r.offset + 0.002f, Mathf.Max(off, r.offset - 0.05f));
            r.lastRx = Time.unscaledTime;
            if (r.buf.Count > 0 && s.t <= r.buf[r.buf.Count - 1].t) continue;
            r.buf.Add(s);
            if (r.buf.Count > 30) r.buf.RemoveAt(0);
            // host: the finish of a guest's kart -> the host assigns the place
            if (role == Role.Host && (s.flags & 64) != 0 && Game.I != null) Game.I.NetKartFinished(k, s.fin);
        }
    }

    void PosePuppets()
    {
        float now = Time.unscaledTime;
        foreach (var k in Game.I.karts)
        {
            bool pup = !OwnsKart(k.id);
            k.netPuppet = pup;
            k.netName = OwnerName(k.id);
            if (!pup) continue;
            Remote r = rem[k.id];
            if (r.buf.Count == 0 || float.IsNaN(r.offset)) continue;
            float t = now - r.offset - Delay;
            Snap a = r.buf[0], b = a;
            for (int i = 0; i < r.buf.Count; i++)
            {
                if (r.buf[i].t <= t) a = r.buf[i];
                if (r.buf[i].t >= t) { b = r.buf[i]; break; }
                b = r.buf[i];
            }
            float u = b.t > a.t ? Mathf.Clamp01((t - a.t) / (b.t - a.t)) : 1f;
            Vector3 pos = (b.pos - a.pos).sqrMagnitude > 144f ? b.pos : Vector3.Lerp(a.pos, b.pos, u);
            // a short extrapolation when the stream is late (keeps puppets moving smoothly over a dropped packet)
            if (t > b.t && t - b.t < 0.25f) pos += new Vector3(Mathf.Sin(b.yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(b.yaw * Mathf.Deg2Rad)) * b.spd * (t - b.t);
            k.NetPose(pos, Mathf.LerpAngle(a.yaw, b.yaw, u), Mathf.Lerp(a.spd, b.spd, u), b.lat, b.vy, b.lap, b.s, b.flags, b.fin);
        }
    }

    void DropGuest(string id, string why)
    {
        int i = players.FindIndex(p => p.id == id);
        if (i < 1) return;
        int k = players[i].kart;
        players.RemoveAt(i);
        LBNet_Drop(id);
        if (k >= 0 && k < kartOwner.Length) kartOwner[k] = "";
        if (Game.I != null && k >= 0 && k < Game.I.karts.Count) { var kt = Game.I.karts[k]; kt.netPuppet = false; kt.netName = null; kt.human = false; }
        if (Game.I != null) Game.I.NetToast(why);
        Debug.Log("NET drop " + id + " (" + why + ")");
        if (!raceOn) BroadcastLineup();
        else Send("*", "L|1|" + Game.CurrentTrack + "|" + LineupStr());
    }

    // ---------------- inbox ----------------
    void Poll()
    {
        string all = null;
        try { all = LBNet_Poll(); } catch { }
        if (string.IsNullOrEmpty(all)) return;
        foreach (string line in all.Split('\n'))
        {
            if (line.Length == 0) continue;
            var f = line.Split(new[] { '|' }, 3);
            switch (f[0])
            {
                case "open":
                    if (role == Role.Host) { connected = true; if (f.Length > 1) code = f[1]; Say("Room " + code + " is open - share the code"); BroadcastLineup(); }
                    break;
                case "me": if (f.Length > 1) myId = f[1]; break;
                case "conn":
                    if (role == Role.Guest)
                    {
                        connected = true; hostLastRx = Time.unscaledTime;
                        int car = 0, robot = 0;
                        if (Game.I != null) Game.I.MyPicks(out car, out robot);
                        Send("host", "H|ONLINE|" + car + "|" + robot);
                        Say("Joined room " + code + " - the host starts the race");
                    }
                    break;
                case "gone":
                    if (role == Role.Host && f.Length > 1) DropGuest(f[1], "A player left");
                    else if (role == Role.Guest) Leave("The host closed the room - playing offline");
                    break;
                case "err":
                    {
                        string kind = f.Length > 1 ? f[1] : "";
                        if (kind == "noroom") Leave("No room " + code);
                        else if (kind == "timeout") Leave("Room " + code + " did not answer");
                        else if (kind == "load") Leave("Online play could not load (PeerJS) - check the connection");
                        else if (!connected) Leave("Online error: " + kind);
                        else Debug.Log("NET err " + line);
                        break;
                    }
                case "msg":
                    if (f.Length >= 3)
                    {
                        var g = f[2].Split('|');
                        int sep = f[2].IndexOf('|');
                        string from = f[1];
                        rxCount++;
                        OnMsg(from, g);
                        _ = sep;
                    }
                    break;
            }
        }
    }

    void OnMsg(string from, string[] p)
    {
        if (p.Length == 0) return;
        if (role == Role.Host)
        {
            Player pl = players.Find(x => x.id == from);
            if (pl != null) pl.lastRx = Time.unscaledTime;
            switch (p[0])
            {
                case "H":
                    if (pl != null) break;
                    if (players.Count >= 4 || raceOn) { Send(from, "X|" + (raceOn ? "race" : "full")); LBNet_Drop(from); Debug.Log("NET refused " + from + (raceOn ? " (race on)" : " (full)")); break; }
                    pl = new Player { id = from, name = "P" + (players.Count + 1), car = p.Length > 2 ? PI(p[2]) : 1, robot = p.Length > 3 ? PI(p[3]) : 1, lastRx = Time.unscaledTime };
                    players.Add(pl);
                    Debug.Log("NET guest joined " + from + " as " + pl.name);
                    if (Game.I != null) Game.I.NetToast(pl.name + " joined online");
                    BroadcastLineup();
                    break;
                case "C":
                    if (pl != null && p.Length >= 3 && !raceOn) { pl.car = Mathf.Clamp(PI(p[1]), 0, 7); pl.robot = Mathf.Clamp(PI(p[2]), 0, Robots.Count - 1); BroadcastLineup(); }
                    break;
                case "S":
                    if (pl == null) break;
                    OnStates(p, from);
                    Send("*!" + from, string.Join("|", p));      // relay
                    break;
                case "F":
                    if (pl == null) break;
                    OnFire(p);
                    Send("*!" + from, string.Join("|", p));
                    break;
                case "V":
                    if (pl == null || p.Length < 4) break;
                    {
                        int v = PI(p[1]);
                        if (v < 0 || v >= kartOwner.Length) break;
                        if (OwnsKart(v)) ApplyHit(p);
                        else { string o = kartOwner[v]; if (o.Length > 0) Send(o, string.Join("|", p)); }
                    }
                    break;
            }
            return;
        }
        // guest
        hostLastRx = Time.unscaledTime;
        switch (p[0])
        {
            case "L":
                if (p.Length >= 4)
                {
                    ParseLineup(p, 3);
                    if (Game.I != null) { Game.I.NetLineup(); if (p[1] == "0") Game.I.NetTrack(PI(p[2])); }
                }
                break;
            case "G":
                if (p.Length >= 3)
                {
                    ParseLineup(p, 2);
                    raceOn = true;
                    for (int i = 0; i < rem.Length; i++) { rem[i].buf.Clear(); rem[i].offset = float.NaN; }
                    Debug.Log("NET race start (guest) track " + p[1] + " myKart " + myKart);
                    if (Game.I != null) Game.I.NetStart(PI(p[1]));
                }
                break;
            case "S": OnStates(p, "host"); break;
            case "F": OnFire(p); break;
            case "V": ApplyHit(p); break;
            case "O":
                if (p.Length >= 4 && Game.I != null) Game.I.NetPlace(PI(p[1]), PI(p[2]), PF(p[3]));
                break;
            case "B":
                raceOn = false;
                if (Game.I != null) Game.I.NetBackToLobby();
                break;
            case "X": Leave(p.Length > 1 && p[1] == "race" ? "That room is already racing - try again after the race" : "That room is full (4 players)"); break;
        }
    }

    void OnFire(string[] p)
    {
        if (p.Length < 8 || Game.I == null) return;
        int k = PI(p[1]);
        if (k < 0 || k >= Game.I.karts.Count || OwnsKart(k)) return;
        Kart kt = Game.I.karts[k];
        kt.NetPose(new Vector3(PF(p[3]), PF(p[4]), PF(p[5])), PF(p[6]), PF(p[7]), kt.lat, kt.vy, kt.lap, kt.s, -1, 0f);
        Items.Use(kt, (Item)PI(p[2]));
    }

    void ApplyHit(string[] p)
    {
        if (p.Length < 4 || Game.I == null) return;
        int v = PI(p[1]), fromK = PI(p[3]);
        if (v < 0 || v >= Game.I.karts.Count || !OwnsKart(v)) return;
        Kart from = fromK >= 0 && fromK < Game.I.karts.Count ? Game.I.karts[fromK] : null;
        Game.I.karts[v].NetHit((Item)PI(p[2]), from);
    }
}
