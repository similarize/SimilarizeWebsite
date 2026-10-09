using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

// ffu13: online Host / Join (PeerJS via Plugins/WebGL/FFNet.jslib), modelled on the three.js 3D version's party lobby.
// - One player per device online. The host's device owns its own froggy plus every AI froggy; each guest owns its froggy.
// - Every owner sends its froggies' state ~12 times a second (position / yaw / animation flags / world, plus the pose,
//   velocity and Cyberboat transform of a vehicle it is driving). The host relays guest states to the other guests.
// - Froggies owned by another device are "puppets": their own simulation is off (Frog.netPuppet) and they are drawn
//   ~0.1 s in the past, interpolated between snapshots. A vehicle a puppet drives is made kinematic and posed the same way;
//   when the puppet gets out it goes back to local physics with the last velocity.
// - Robots, animals, pickups, projectiles, the space Starship and the worlds themselves stay local on every device.
// - Stage changes follow the host: when the host's froggy enters the house / dives / launches / lands, every guest follows.
// - Disconnects: a guest who leaves (or goes silent 20 s) turns back into an AI froggy on the host; if the host goes away
//   the guests carry on offline with the other froggies on local AI.
[DefaultExecutionOrder(-40)]
public class Net : MonoBehaviour
{
    public static Net I;
    public enum Role { Off, Host, Guest }
    public Role role = Role.Off;
    public bool connected;          // host: room open; guest: connected to the host
    public string code = "";
    public string myId = "";
    public string info = "";        // last status / error line for the lobby
    public float infoT;
    public bool playing;            // host has started the game
    public WorldId hostWorld = WorldId.Ranch;

    // per froggy: "" = AI (host simulates), "host", or a guest's peer id; chosen name ("" = the froggy's own name)
    public readonly string[] owner = { "", "", "", "" };
    public readonly string[] names = { "", "", "", "" };
    public readonly bool[] remoteHuman = new bool[4];

    public const string CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    const float SendDt = 1f / 12f, Delay = 0.11f, Silence = 20f;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public bool Online { get { return role != Role.Off; } }
    public bool IsHost { get { return role == Role.Host; } }
    public bool IsGuest { get { return role == Role.Guest; } }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void FFNet_Init();
    [DllImport("__Internal")] static extern void FFNet_Host(string code);
    [DllImport("__Internal")] static extern void FFNet_Join(string code);
    [DllImport("__Internal")] static extern void FFNet_Send(string to, string msg);
    [DllImport("__Internal")] static extern void FFNet_Drop(string id);
    [DllImport("__Internal")] static extern void FFNet_Leave();
    [DllImport("__Internal")] static extern string FFNet_Poll();
#else
    static void FFNet_Init() { }
    static void FFNet_Host(string code) { Debug.Log("FFNet (editor): no network"); }
    static void FFNet_Join(string code) { Debug.Log("FFNet (editor): no network"); }
    static void FFNet_Send(string to, string msg) { }
    static void FFNet_Drop(string id) { }
    static void FFNet_Leave() { }
    static string FFNet_Poll() { return null; }
#endif

    class Guest { public string id; public int frog = -1; public string name = ""; public float lastRx; }
    readonly Dictionary<string, Guest> guests = new Dictionary<string, Guest>();
    float hostLastRx, sendT, lobbyT, logT, joinStartT;
    int rxCount, txCount;

    struct Snap
    {
        public float t, yaw, spd, aux;
        public WorldId world;
        public Vector3 pos, vpos, vvel;
        public Quaternion vrot;
        public int flags, veh;
    }
    class Remote { public readonly List<Snap> buf = new List<Snap>(); public float offset = float.NaN; public float lastRx; }
    readonly Remote[] rem = { new Remote(), new Remote(), new Remote(), new Remote() };

    class PupVeh { public bool kin; public RigidbodyInterpolation interp; public Vector3 vel; }
    readonly Dictionary<Vehicle, PupVeh> pupVeh = new Dictionary<Vehicle, PupVeh>();
    readonly Dictionary<int, Vehicle> vehByKey = new Dictionary<int, Vehicle>();
    int vehCount = -1;

    void Awake() { I = this; FFNet_Init(); }

    // ---------------- public API (Game) ----------------
    public static string CleanName(string s)
    {
        // letters A-Z only, max 4; anything else is rejected (null). "" = use the froggy's own name
        if (s == null) return null;
        s = s.Trim().ToUpperInvariant();
        if (s.Length > 4) return null;
        foreach (char c in s) if (c < 'A' || c > 'Z') return null;
        return s;
    }

    public static string CleanCode(string s)
    {
        if (s == null) return "";
        var sb = new StringBuilder();
        foreach (char c in s.ToUpperInvariant()) if (CodeChars.IndexOf(c) >= 0) sb.Append(c);
        return sb.Length > 6 ? sb.ToString().Substring(0, 6) : sb.ToString();
    }

    public void Host(string wantCode)
    {
        Leave(null);
        role = Role.Host; connected = false; playing = Game.I != null && Game.I.state == Game.State.Play;
        code = CleanCode(wantCode);
        if (code.Length < 4) { code = ""; for (int i = 0; i < 4; i++) code += CodeChars[Random.Range(0, CodeChars.Length)]; }
        Say("Opening room " + code + "...");
        FFNet_Host(code);
        Debug.Log("NET host requested " + code);
    }

    public void Join(string c)
    {
        Leave(null);
        c = CleanCode(c);
        if (c.Length < 3) { Say("Type the 4-character room code from the host's screen"); return; }
        role = Role.Guest; connected = false; code = c; playing = false; joinStartT = Time.unscaledTime;
        Say("Joining room " + code + "...");
        FFNet_Join(code);
        Debug.Log("NET join requested " + code);
    }

    // why != null: shown in the lobby / toasted
    public void Leave(string why)
    {
        if (role != Role.Off) { FFNet_Leave(); Debug.Log("NET leave: " + (why ?? "")); }
        role = Role.Off; connected = false; playing = false; myId = ""; guests.Clear();
        for (int i = 0; i < 4; i++) { owner[i] = ""; names[i] = ""; remoteHuman[i] = false; }
        if (Game.I != null) foreach (var f in Game.I.frogs) SetPuppet(f, false);
        if (why != null) { Say(why); if (Game.I != null) Game.I.ToastLocal(why, 4f); }
    }

    public bool TakenByRemote(int f)
    {
        if (role == Role.Off || f < 0 || f > 3) return false;
        if (role == Role.Host) return owner[f].Length > 0 && owner[f] != "host";
        return owner[f].Length > 0 && owner[f] != myId;
    }

    public string OwnerLabel(int f)
    {
        if (role == Role.Off) return "";
        string o = owner[f];
        if (o.Length == 0) return "AI froggy";
        bool mine = role == Role.Host ? o == "host" : o == myId;
        string who = o == "host" ? "HOST" : "ONLINE";
        return mine ? (role == Role.Host ? "YOU - HOST" : "YOU - ONLINE") : who;
    }

    // guest: ask the host for a froggy (the lobby update moves the slot when granted)
    public void RequestFrog(int f)
    {
        if (role == Role.Guest && connected) Send("host", "C|" + f);
    }

    // ffu14: characters. Guests ask the host (K|char); the host owns Game.charOf and sends it in every L message.
    public void RequestChar(int c)
    {
        if (role == Role.Guest && connected) Send("host", "K|" + c);
    }
    public void CharsChanged()
    {
        if (role == Role.Host && connected && lobbyT > 0.05f) BroadcastLobby();
        else if (role == Role.Host) charsDirty = true;
    }
    bool charsDirty;

    public void LocalChanged()
    {
        // host: its own seat / name changed -> everyone's lobby; guest: tell the host the new name
        if (role == Role.Host) { SyncHostSeat(); BroadcastLobby(); }
        else if (role == Role.Guest && connected) Send("host", "N|" + LocalName());
    }

    public void OnStartPlay()
    {
        if (role != Role.Host) return;
        playing = true;
        BroadcastLobby();
    }

    // shown on the lobby: who is in the room
    public string PlayersLine()
    {
        if (role == Role.Off) return "";
        var sb = new StringBuilder();
        for (int i = 0; i < 4; i++)
        {
            if (owner[i].Length == 0) continue;
            if (sb.Length > 0) sb.Append("   ");
            int ch = Game.I != null ? Game.I.charOf[i] : i;
            string n = names[i].Length > 0 ? names[i] : Roster.Name(ch);
            sb.Append("<color=" + Roster.UiHex(ch) + ">" + n + "</color>" + (owner[i] == "host" ? " (host)" : ""));
        }
        return sb.ToString();
    }

    void Say(string s) { info = s; infoT = Time.unscaledTime; }

    // ---------------- helpers ----------------
    Slot LocalSlot { get { return Game.I != null && Game.I.slots.Count > 0 ? Game.I.slots[0] : null; } }
    string LocalName() { var s = LocalSlot; return s != null ? s.name : ""; }

    void Send(string to, string msg) { FFNet_Send(to, msg); txCount++; }

    void SyncHostSeat()
    {
        // the host's own froggy = its local slot (a change goes straight out to the guests)
        int before = -1; string beforeName = "";
        for (int i = 0; i < 4; i++) if (owner[i] == "host") { before = i; beforeName = names[i]; owner[i] = ""; names[i] = ""; }
        var s = LocalSlot;
        int now = -1;
        if (s != null && s.frog >= 0 && owner[s.frog].Length == 0) { owner[s.frog] = "host"; names[s.frog] = s.name; now = s.frog; }
        if ((now != before || (now >= 0 && names[now] != beforeName)) && connected && lobbyT > 0.05f) BroadcastLobby();
    }

    string LobbyMsg()
    {
        var sb = new StringBuilder("L|");
        sb.Append(playing ? 1 : 0).Append('|').Append((int)hostWorld);
        for (int i = 0; i < 4; i++) sb.Append('|').Append(owner[i]).Append('|').Append(names[i]);
        for (int i = 0; i < 4; i++) sb.Append('|').Append(Game.I != null ? Game.I.charOf[i] : i);   // ffu14 characters
        charsDirty = false;
        return sb.ToString();
    }

    void BroadcastLobby() { if (role == Role.Host && connected) Send("*", LobbyMsg()); lobbyT = 0f; }

    int FirstFreeFrog(int want)
    {
        if (want >= 0 && want < 4 && owner[want].Length == 0) return want;
        for (int i = 0; i < 4; i++) if (owner[i].Length == 0) return i;
        return -1;
    }

    // ---------------- main loop ----------------
    void Update()
    {
        if (Game.I == null) return;
        Poll();
        float now = Time.unscaledTime;
        if (role == Role.Host)
        {
            SyncHostSeat();
            var s = LocalSlot;
            if (s != null && Game.I.state == Game.State.Play)
            {
                WorldId w = Game.I.frogs[s.frog].world;
                if (w != hostWorld) { hostWorld = w; if (connected) Send("*", "W|" + (int)w); }
            }
            lobbyT += Time.unscaledDeltaTime;
            if (lobbyT > 2f || (charsDirty && lobbyT > 0.05f)) BroadcastLobby();
            // silent guests (closed tab without a close event, phone asleep) -> drop
            var drop = new List<string>();
            foreach (var g in guests.Values) if (now - g.lastRx > Silence) drop.Add(g.id);
            foreach (var id in drop) { FFNet_Drop(id); GuestGone(id, " lost connection"); }
        }
        else if (role == Role.Guest)
        {
            if (connected && now - hostLastRx > Silence) Leave("Lost the host - you're playing offline now");
            else if (!connected && now - joinStartT > 60f) Leave("Could not reach room " + code + " - check the code and try again");
        }
        // which froggies are puppets of another device
        for (int i = 0; i < 4; i++)
        {
            Frog f = Game.I.frogs[i];
            bool pup = role != Role.Off && (role == Role.Host ? TakenByRemote(i) : (owner[i] != myId || myId.Length == 0) && !IsLocalFrog(i));
            if (role == Role.Guest && !connected) pup = false;
            SetPuppet(f, pup);
            if (pup) ApplyRemote(i, f);
        }
        if (role != Role.Off && connected)
        {
            sendT += Time.unscaledDeltaTime;
            if (sendT >= SendDt) { sendT = 0f; SendStates(); }
        }
        logT += Time.unscaledDeltaTime;
        if (role != Role.Off && logT > 4f)
        {
            logT = 0f;
            var sb = new StringBuilder("NET " + role + (connected ? " connected" : " connecting") + " room=" + code + " playing=" + playing + " tx=" + txCount + " rx=" + rxCount + " |");
            for (int i = 0; i < 4; i++)
            {
                Frog f = Game.I.frogs[i];
                Vector3 p = f.transform.position;
                sb.Append(" " + Froggies.Names[i] + "=" + f.nick + (f.netPuppet ? "(puppet" + (remoteHuman[i] ? ",human" : ",ai") + ")" : f.human ? "(local)" : "(ai)") + "@" + p.x.ToString("0.0", Inv) + "," + p.z.ToString("0.0", Inv) + (f.vehicle != null ? "[" + f.vehicle.Title + "]" : ""));
            }
            Debug.Log(sb.ToString());
        }
    }

    bool IsLocalFrog(int i) { foreach (var s in Game.I.slots) if (s.frog == i) return true; return false; }

    void Poll()
    {
        string all = FFNet_Poll();
        if (string.IsNullOrEmpty(all)) return;
        foreach (string line in all.Split('\n'))
        {
            if (line.Length == 0) continue;
            int a = line.IndexOf('|');
            string kind = a < 0 ? line : line.Substring(0, a);
            string rest = a < 0 ? "" : line.Substring(a + 1);
            switch (kind)
            {
                case "open":
                    connected = true; code = rest; Say("Room " + code + " is open");
                    Debug.Log("NET room open " + code);
                    BroadcastLobby();
                    break;
                case "me": myId = rest; break;
                case "conn":
                    if (role == Role.Host)
                    {
                        guests[rest] = new Guest { id = rest, lastRx = Time.unscaledTime };
                        Debug.Log("NET guest connected " + rest);
                        Send(rest, LobbyMsg());
                    }
                    else if (role == Role.Guest)
                    {
                        connected = true; hostLastRx = Time.unscaledTime;
                        var s = LocalSlot;
                        Send("host", "H|" + LocalName() + "|" + (s != null ? s.frog : -1) + "|" + (s != null ? Game.I.charOf[s.frog] : -1));
                        Say("Connected to room " + code + " - waiting for the host");
                        Debug.Log("NET connected to host, room " + code);
                    }
                    break;
                case "gone":
                    if (role == Role.Host) GuestGone(rest, " left");
                    else if (role == Role.Guest) Leave("The host left - you're playing offline now");
                    break;
                case "err":
                    {
                        string[] e = rest.Split('|');
                        string k = e[0];
                        Debug.Log("NET error " + rest);
                        if (k == "noroom") Leave("No room " + code + " - check the code on the host's screen");
                        else if (k == "load") Leave("Couldn't load the online service - offline play still works");
                        else if (k == "timeout") Leave("Room " + code + " didn't answer - check the code / Wi-Fi");
                        else if (k == "network" || k == "server-error" || k == "socket-error" || k == "socket-closed") { if (!connected) Leave("Can't reach the online service right now - offline play still works"); }
                        else if (!connected) Leave("Online error (" + k + ") - offline play still works");
                        break;
                    }
                case "msg":
                    {
                        int b = rest.IndexOf('|');
                        if (b < 0) break;
                        rxCount++;
                        OnMsg(rest.Substring(0, b), rest.Substring(b + 1));
                        break;
                    }
            }
        }
    }

    void GuestGone(string id, string how)
    {
        Guest g;
        if (!guests.TryGetValue(id, out g)) return;
        guests.Remove(id);
        string n = "A player";
        for (int i = 0; i < 4; i++)
            if (owner[i] == id)
            {
                n = names[i].Length > 0 ? names[i] : Roster.Name(Game.I.charOf[i]);
                owner[i] = ""; names[i] = ""; remoteHuman[i] = false;
                SetPuppet(Game.I.frogs[i], false);
            }
        Debug.Log("NET guest gone " + id + how);
        Game.I.ToastLocal(n + how + " - their froggy is on AI now", 3.5f);
        Send("*", "T|" + n + how);
        BroadcastLobby();
    }

    void OnMsg(string from, string m)
    {
        string[] p = m.Split('|');
        if (p.Length == 0) return;
        if (role == Role.Host)
        {
            Guest g;
            if (!guests.TryGetValue(from, out g)) { g = new Guest { id = from }; guests[from] = g; }
            g.lastRx = Time.unscaledTime;
            switch (p[0])
            {
                case "H":
                    {
                        string nm = CleanName(p.Length > 1 ? p[1] : "") ?? "";
                        int want = p.Length > 2 ? ParseI(p[2]) : -1;
                        for (int i = 0; i < 4; i++) if (owner[i] == from) { owner[i] = ""; names[i] = ""; }
                        int f = FirstFreeFrog(want);
                        if (f < 0) { Send(from, "X|full"); FFNet_Drop(from); guests.Remove(from); Debug.Log("NET room full, refused " + from); return; }
                        owner[f] = from; names[f] = nm; remoteHuman[f] = true; g.frog = f; g.name = nm;
                        rem[f].buf.Clear(); rem[f].offset = float.NaN;
                        int wc = p.Length > 3 ? ParseI(p[3]) : -1;   // ffu14: the character they picked before joining
                        if (Roster.Valid(wc) && !Game.I.CharHeldByOther(wc, f)) Game.I.SetSeatChar(f, wc, false);
                        Game.I.FixAiChars();
                        string cn = Roster.Name(Game.I.charOf[f]);
                        string shown = nm.Length > 0 ? nm : cn;
                        Debug.Log("NET guest " + from + " joined as " + cn + " (seat " + f + ") name='" + nm + "'");
                        Game.I.ToastLocal(shown + " joined online as " + cn + "!", 3.5f);
                        Send("*!" + from, "T|" + shown + " joined as " + cn);
                        BroadcastLobby();
                        break;
                    }
                case "C":
                    {
                        int f = p.Length > 1 ? ParseI(p[1]) : -1;
                        if (f < 0 || f > 3 || owner[f].Length > 0) { Send(from, LobbyMsg()); break; }
                        if (Game.I.state == Game.State.Play) { Send(from, LobbyMsg()); break; }   // froggies are fixed once playing
                        string nm = "";
                        for (int i = 0; i < 4; i++) if (owner[i] == from) { nm = names[i]; owner[i] = ""; names[i] = ""; remoteHuman[i] = false; }
                        owner[f] = from; names[f] = nm; remoteHuman[f] = true;
                        rem[f].buf.Clear(); rem[f].offset = float.NaN;
                        BroadcastLobby();
                        break;
                    }
                case "K":
                    {
                        int c = p.Length > 1 ? ParseI(p[1]) : -1;
                        int seat = -1;
                        for (int i = 0; i < 4; i++) if (owner[i] == from) seat = i;
                        if (seat >= 0 && Roster.Valid(c) && Game.I.state == Game.State.Lobby && !Game.I.CharHeldByOther(c, seat))
                        {
                            Game.I.SetSeatChar(seat, c, true);
                            Game.I.FixAiChars();
                        }
                        BroadcastLobby();
                        break;
                    }
                case "N":
                    {
                        string nm = CleanName(p.Length > 1 ? p[1] : "");
                        if (nm == null) break;
                        for (int i = 0; i < 4; i++) if (owner[i] == from) names[i] = nm;
                        BroadcastLobby();
                        break;
                    }
                case "S":
                    {
                        // apply the guest's own froggy, relay to everybody else
                        bool ok = false;
                        for (int r = 1; r < p.Length; r++)
                        {
                            Snap s; int fid;
                            if (!ParseState(p[r], out fid, out s)) continue;
                            if (fid < 0 || fid > 3 || owner[fid] != from) continue;
                            Push(fid, s); ok = true;
                        }
                        if (ok) Send("*!" + from, m);
                        break;
                    }
                case "P": break;   // keep-alive
            }
            return;
        }
        if (role != Role.Guest) return;
        hostLastRx = Time.unscaledTime;
        switch (p[0])
        {
            case "L":
                {
                    if (p.Length < 11) break;
                    bool wasPlaying = playing;
                    playing = p[1] == "1";
                    hostWorld = (WorldId)ParseI(p[2]);
                    for (int i = 0; i < 4; i++)
                    {
                        string o = p[3 + i * 2];
                        if (o != owner[i]) { rem[i].buf.Clear(); rem[i].offset = float.NaN; }
                        owner[i] = o;
                        names[i] = CleanName(p[4 + i * 2]) ?? "";
                        remoteHuman[i] = o.Length > 0;
                    }
                    if (p.Length >= 15)
                        for (int i = 0; i < 4; i++) { int c = ParseI(p[11 + i]); if (Roster.Valid(c)) Game.I.SetSeatChar(i, c, Game.I.state == Game.State.Lobby); }
                    int mine = -1;
                    for (int i = 0; i < 4; i++) if (owner[i] == myId && myId.Length > 0) mine = i;
                    var s = LocalSlot;
                    if (mine >= 0 && s != null && s.frog != mine) Game.I.MoveSlot(s, mine);
                    if (mine >= 0) Say("In room " + code + " as " + Roster.Name(Game.I.charOf[mine]) + (playing ? "" : " - waiting for the host to start"));
                    if (playing && mine >= 0 && Game.I.state == Game.State.Lobby)
                    {
                        Debug.Log("NET host is playing -> start");
                        Game.I.StartPlay(true);
                        if (hostWorld != WorldId.Ranch) Follow(hostWorld);
                    }
                    else if (!wasPlaying && playing) Debug.Log("NET host started");
                    break;
                }
            case "W":
                {
                    hostWorld = (WorldId)ParseI(p.Length > 1 ? p[1] : "0");
                    if (Game.I.state == Game.State.Play) Follow(hostWorld);
                    break;
                }
            case "S":
                for (int r = 1; r < p.Length; r++)
                {
                    Snap s; int fid;
                    if (!ParseState(p[r], out fid, out s)) continue;
                    if (fid < 0 || fid > 3 || (owner[fid] == myId && myId.Length > 0) || IsLocalFrog(fid)) continue;
                    Push(fid, s);
                }
                break;
            case "T":
                Game.I.ToastLocal(p.Length > 1 ? p[1] : "", 3f);
                break;
            case "X":
                Leave("That room is full (4 froggies) - play offline or host your own");
                break;
        }
    }

    static int ParseI(string s) { int v; return int.TryParse(s, NumberStyles.Integer, Inv, out v) ? v : -1; }
    static float ParseF(string s) { float v; return float.TryParse(s, NumberStyles.Float, Inv, out v) ? v : 0f; }

    // ---------------- stage follow (guest) ----------------
    void Follow(WorldId w)
    {
        var s = LocalSlot;
        if (s == null) return;
        Frog f = Game.I.frogs[s.frog];
        if (f.world == w || f.launching) return;
        Debug.Log("NET follow host -> " + w);
        if (f.world == WorldId.Underwater && UnderwaterWorld.I != null && w == WorldId.Ranch) { UnderwaterWorld.I.Surface(f); return; }
        switch (w)
        {
            case WorldId.House: if (HouseWorld.I != null) HouseWorld.I.Enter(f); break;
            case WorldId.Underwater: if (UnderwaterWorld.I != null) UnderwaterWorld.I.Dive(f); break;
            case WorldId.Space: if (SpaceWorld.I != null) SpaceWorld.I.Launch(f); break;
            case WorldId.Mars: if (SurfaceWorlds.I != null) SurfaceWorlds.LandMars(f, f.id); break;
            case WorldId.Callisto: if (SurfaceWorlds.I != null) SurfaceWorlds.LandCallisto(f, f.id); break;
            default: f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f); break;
        }
        f.Toast("Following the host: " + Worlds.Name(w), 3f);
    }

    // ---------------- state out ----------------
    static int VehKey(Vehicle v)
    {
        if (v == null || v is Starship) return 0;
        Vector3 h = v.HomePos;
        string k = v.Title + "@" + Mathf.RoundToInt(h.x) + "," + Mathf.RoundToInt(h.z);
        unchecked
        {
            int x = (int)2166136261;
            foreach (char c in k) { x ^= c; x *= 16777619; }
            return x == 0 ? 1 : x;
        }
    }

    Vehicle FindVeh(int key)
    {
        if (Vehicle.All.Count != vehCount)
        {
            vehCount = Vehicle.All.Count;
            vehByKey.Clear();
            foreach (var v in Vehicle.All) { int k = VehKey(v); if (k != 0 && !vehByKey.ContainsKey(k)) vehByKey[k] = v; }
        }
        Vehicle r;
        return vehByKey.TryGetValue(key, out r) && r != null ? r : null;
    }

    static string F(float v) { return v.ToString("0.##", Inv); }

    void SendStates()
    {
        var sb = new StringBuilder("S");
        int n = 0;
        for (int i = 0; i < 4; i++)
        {
            Frog f = Game.I.frogs[i];
            bool mine = role == Role.Host ? !TakenByRemote(i) : IsLocalFrog(i) && owner[i] == myId;
            if (!mine || f.netPuppet) continue;
            AppendState(sb, f);
            n++;
        }
        if (n == 0) { if (role == Role.Guest) Send("host", "P"); return; }
        Send(role == Role.Host ? "*" : "host", sb.ToString());
    }

    void AppendState(StringBuilder sb, Frog f)
    {
        Vector3 p = f.transform.position;
        int flags = 0;
        if (f.cc != null && f.cc.enabled && !f.cc.isGrounded && f.vehicle == null) flags |= 1;
        if (f.Swimming) flags |= 2;
        if (f.InScuba) flags |= 4;
        if (f.chute) flags |= 8;
        if (!f.model.gameObject.activeSelf) flags |= 16;
        if (f.human) flags |= 32;
        Vehicle v = f.vehicle;
        int key = VehKey(v);
        sb.Append('|').Append(f.id).Append('~').Append((int)f.world).Append('~').Append(F(p.x)).Append('~').Append(F(p.y)).Append('~').Append(F(p.z))
          .Append('~').Append(F(f.transform.eulerAngles.y)).Append('~').Append(F(f.HSpeed)).Append('~').Append(flags)
          .Append('~').Append(Time.realtimeSinceStartup.ToString("0.###", Inv)).Append('~').Append(key);
        if (key != 0)
        {
            Vector3 vp = v.transform.position, vv = v.Velocity;
            Quaternion q = v.transform.rotation;
            var gv = v as GroundVehicle;
            float aux = gv != null && gv.amph != null ? gv.amph.k : 0f;
            sb.Append('~').Append(F(vp.x)).Append('~').Append(F(vp.y)).Append('~').Append(F(vp.z))
              .Append('~').Append(q.x.ToString("0.####", Inv)).Append('~').Append(q.y.ToString("0.####", Inv)).Append('~').Append(q.z.ToString("0.####", Inv)).Append('~').Append(q.w.ToString("0.####", Inv))
              .Append('~').Append(F(vv.x)).Append('~').Append(F(vv.y)).Append('~').Append(F(vv.z)).Append('~').Append(F(aux));
        }
    }

    static bool ParseState(string r, out int fid, out Snap s)
    {
        s = new Snap(); fid = -1;
        string[] a = r.Split('~');
        if (a.Length < 10) return false;
        fid = ParseI(a[0]);
        s.world = (WorldId)Mathf.Clamp(ParseI(a[1]), 0, 5);
        s.pos = new Vector3(ParseF(a[2]), ParseF(a[3]), ParseF(a[4]));
        s.yaw = ParseF(a[5]); s.spd = ParseF(a[6]); s.flags = ParseI(a[7]); s.t = ParseF(a[8]); s.veh = ParseI(a[9]);
        if (s.veh == -1) s.veh = 0;
        if (s.veh != 0)
        {
            if (a.Length < 21) { s.veh = 0; return true; }
            s.vpos = new Vector3(ParseF(a[10]), ParseF(a[11]), ParseF(a[12]));
            s.vrot = new Quaternion(ParseF(a[13]), ParseF(a[14]), ParseF(a[15]), ParseF(a[16]));
            if (s.vrot.x == 0f && s.vrot.y == 0f && s.vrot.z == 0f && s.vrot.w == 0f) s.vrot = Quaternion.identity;
            s.vrot = Quaternion.Normalize(s.vrot);
            s.vvel = new Vector3(ParseF(a[17]), ParseF(a[18]), ParseF(a[19]));
            s.aux = ParseF(a[20]);
        }
        return true;
    }

    void Push(int fid, Snap s)
    {
        Remote r = rem[fid];
        float now = Time.realtimeSinceStartup;
        float off = now - s.t;
        // clock offset = smallest seen (least network delay), drifting up slowly so a long stall doesn't lock it low
        if (float.IsNaN(r.offset) || off < r.offset || Mathf.Abs(off - r.offset) > 5f) r.offset = off;
        else r.offset += (off - r.offset) * 0.02f;
        r.lastRx = now;
        if (r.buf.Count > 0 && s.t <= r.buf[r.buf.Count - 1].t) return;   // out of order / duplicate
        r.buf.Add(s);
        if (r.buf.Count > 12) r.buf.RemoveAt(0);
        remoteHuman[fid] = (s.flags & 32) != 0 || (role == Role.Host && owner[fid].Length > 0);
    }

    // ---------------- puppets ----------------
    void SetPuppet(Frog f, bool on)
    {
        if (f == null || f.netPuppet == on) return;
        if (on)
        {
            if (f.vehicle != null) f.ExitVehicle();
            f.LeavePassenger();
            f.human = false;
            f.netPuppet = true;
            if (f.cc != null) f.cc.enabled = false;
            rem[f.id].buf.Clear(); rem[f.id].offset = float.NaN;
        }
        else
        {
            f.netPuppet = false;     // first, so ExitVehicle hands the vehicle back to physics properly
            Vehicle v = f.vehicle;
            if (v != null) { ReleaseVeh(v); f.ExitVehicle(); }
            if (f.cc != null && f.vehicle == null && f.passengerOf == null && !f.launching) f.cc.enabled = true;
            if (f.model != null) f.model.gameObject.SetActive(true);
            f.SetScubaNet(false);
            f.SetChuteNet(false);
        }
    }

    void MakeVehPuppet(Vehicle v)
    {
        if (v == null || pupVeh.ContainsKey(v)) return;
        var pv = new PupVeh();
        if (v.rb != null) { pv.kin = v.rb.isKinematic; pv.interp = v.rb.interpolation; v.rb.isKinematic = true; v.rb.interpolation = RigidbodyInterpolation.None; }
        pupVeh[v] = pv;
        v.enabled = false;
    }

    public void ReleaseVeh(Vehicle v)
    {
        PupVeh pv;
        if (v == null || !pupVeh.TryGetValue(v, out pv)) return;
        pupVeh.Remove(v);
        v.enabled = true;
        v.NetVel(Vector3.zero);
        if (v.rb != null)
        {
            v.rb.isKinematic = pv.kin;
            v.rb.interpolation = pv.interp;
            if (!pv.kin) { v.rb.velocity = pv.vel; v.rb.angularVelocity = Vector3.zero; v.rb.WakeUp(); }
        }
    }

    void ApplyRemote(int i, Frog f)
    {
        Remote r = rem[i];
        if (r.buf.Count == 0) return;
        float rt = Time.realtimeSinceStartup - r.offset - Delay;
        Snap a = r.buf[0], b = a;
        float k = 0f;
        if (rt <= r.buf[0].t) { a = b = r.buf[0]; }
        else if (rt >= r.buf[r.buf.Count - 1].t) { a = b = r.buf[r.buf.Count - 1]; k = Mathf.Min(rt - b.t, 0.25f); }
        else
        {
            for (int j = 0; j + 1 < r.buf.Count; j++)
                if (r.buf[j].t <= rt && rt <= r.buf[j + 1].t)
                {
                    a = r.buf[j]; b = r.buf[j + 1];
                    k = (rt - a.t) / Mathf.Max(1e-4f, b.t - a.t);
                    break;
                }
        }
        bool sameLeg = a.world == b.world && a.veh == b.veh && (a.pos - b.pos).sqrMagnitude < 144f;
        float dt = Time.deltaTime;
        f.world = b.world;
        int flags = b.flags;
        if (b.veh != 0)
        {
            Vehicle v = FindVeh(b.veh);
            if (v != null && v.gameObject.activeInHierarchy)
            {
                if (f.vehicle != v)
                {
                    if (f.vehicle != null) f.ExitVehicle();
                    if (v.driver != null && v.driver != f)
                    {
                        Frog d = v.driver;
                        // two froggies took the same seat at once: the lower froggy number keeps it (both sides agree)
                        if (!d.netPuppet && d.id < f.id) { PlaceFoot(f, a, b, k, sameLeg, dt, flags); return; }
                        d.ExitVehicle();
                        if (d.human) d.Toast(f.nick + " took the " + v.Title, 2.5f);
                    }
                    MakeVehPuppet(v);
                    f.EnterVehicle(v);
                    if (f.cc != null) f.cc.enabled = false;
                }
                Vector3 vp; Quaternion vq;
                if (b.t == a.t && k > 0f) { vp = b.vpos + b.vvel * k; vq = b.vrot; }   // short extrapolation past the newest
                else if (sameLeg) { vp = Vector3.Lerp(a.vpos, b.vpos, k); vq = Quaternion.Slerp(a.vrot, b.vrot, k); }
                else { vp = b.vpos; vq = b.vrot; }
                v.transform.SetPositionAndRotation(vp, vq);
                if (v.rb != null) { v.rb.position = vp; v.rb.rotation = vq; }
                v.NetVel(b.vvel);
                PupVeh pv;
                if (pupVeh.TryGetValue(v, out pv)) pv.vel = b.vvel;
                var gv = v as GroundVehicle;
                if (gv != null && gv.amph != null) gv.amph.NetSetK(Mathf.Lerp(a.aux, b.aux, sameLeg ? k : 1f));
                bool show = v.showDriver && (flags & 16) == 0;
                if (f.model.gameObject.activeSelf != show) f.model.gameObject.SetActive(show);
                f.model.Animate(0f, false, true, false, dt);
                return;
            }
        }
        PlaceFoot(f, a, b, k, sameLeg, dt, flags);
    }

    void PlaceFoot(Frog f, Snap a, Snap b, float k, bool sameLeg, float dt, int flags)
    {
        if (f.vehicle != null) f.ExitVehicle();
        if (f.cc != null && f.cc.enabled) f.cc.enabled = false;
        Vector3 p; float yaw;
        if (b.t == a.t) { p = b.pos; yaw = b.yaw; }
        else if (sameLeg) { p = Vector3.Lerp(a.pos, b.pos, k); yaw = Mathf.LerpAngle(a.yaw, b.yaw, k); }
        else { p = b.pos; yaw = b.yaw; }
        f.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
        bool show = (flags & 16) == 0;
        if (f.model.gameObject.activeSelf != show) f.model.gameObject.SetActive(show);
        f.SetScubaNet((flags & 4) != 0);
        f.SetChuteNet((flags & 8) != 0);
        f.model.Animate(Mathf.Lerp(a.spd, b.spd, k), (flags & 1) != 0, false, (flags & 2) != 0 || (flags & 4) != 0, dt);
    }
}
