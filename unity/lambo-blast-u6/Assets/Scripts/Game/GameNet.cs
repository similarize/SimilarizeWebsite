using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// lambo8 online lobby + race glue (LBNet does the networking). Lobby: HOST (pad X / key H / tap) opens a room and shows
// its 4-character code, JOIN (pad Y / key J / tap) opens the keypad for a code; while online they become CLOSE ROOM /
// LEAVE (key L). One player per device online. Invite link: ?room=CODE. Test: &lbhost=CODE hosts with that code.
public partial class Game
{
    Image hostBtn, joinBtn;
    Text hostLbl, joinLbl;
    Keypad keypad;
    Canvas keypadCanvas;
    float netToastT = -10f, netAutoT;
    string netToast = "";
    bool netUrlDone;
    int hostFinishCount;

    public static int CurrentTrack { get { return I == null ? Track.Id : (I.pendingTrack >= 0 ? I.pendingTrack : Track.Id); } }
    int HumanKarts { get { return LBNet.On && LBNet.I.players.Count > 0 && (LBNet.I.IsHost || LBNet.I.myKart >= 0) ? LBNet.I.players.Count : slots.Count; } }

    void BuildNetUI()
    {
        gameObject.AddComponent<LBNet>();
        Transform r = lobbyRoot != null ? (Transform)lobbyRoot : lobbyCanvas.transform;     // u6h: inside the safe-area root
        hostBtn = Glass(r, new Color(0.12f, 0.15f, 0.2f, 0.9f), new Vector2(0.5f, 0.5f));
        hostLbl = UIK.Label(hostBtn.transform, "HOST", 13, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110, 30), new Color(0.75f, 0.92f, 1f));
        UIK.Stretch(hostLbl.rectTransform);
        joinBtn = Glass(r, new Color(0.12f, 0.15f, 0.2f, 0.9f), new Vector2(0.5f, 0.5f));
        joinLbl = UIK.Label(joinBtn.transform, "JOIN", 13, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110, 30), new Color(0.75f, 0.92f, 1f));
        UIK.Stretch(joinLbl.rectTransform);
        keypadCanvas = UIK.MakeCanvas("Keypad", null, 170, true);
        keypad = new Keypad(keypadCanvas.transform);
    }

    void LayoutNetButtons(bool portrait)
    {
        if (hostBtn == null) return;
        if (portrait)
        {
            // u6h: 44 tall (was 26), clear of MODE (GameLobbyUI.LayoutLobby)
            hostBtn.rectTransform.anchoredPosition = new Vector2(-292, -568); hostBtn.rectTransform.sizeDelta = new Vector2(124, 44);
            joinBtn.rectTransform.anchoredPosition = new Vector2(-160, -568); joinBtn.rectTransform.sizeDelta = new Vector2(124, 44);
        }
        else
        {
            // u6h: u6g's MODE (398, 150 wide) sat on top of HOST + JOIN -> MODE now 137..307, HOST 320..424, JOIN 432..536
            hostBtn.rectTransform.anchoredPosition = new Vector2(372, -288); hostBtn.rectTransform.sizeDelta = new Vector2(104, 44);
            joinBtn.rectTransform.anchoredPosition = new Vector2(484, -288); joinBtn.rectTransform.sizeDelta = new Vector2(104, 44);
        }
        var cs = keypadCanvas.GetComponent<CanvasScaler>();
        float aspect = Screen.width / (float)Mathf.Max(1, Screen.height);
        cs.referenceResolution = portrait ? new Vector2(720, 1280) : new Vector2(1280, 720);
        cs.matchWidthOrHeight = portrait ? (1f / aspect >= 1.75f ? 0f : 1f) : (aspect >= 1.78f ? 1f : 0f);
    }

    public void NetToast(string s)
    {
        netToast = s; netToastT = Time.unscaledTime;
        foreach (var sl in slots) if (sl.kart != null) sl.kart.Toast(s, 3f);
    }

    public void MyPicks(out int car, out int robot)
    {
        car = slots.Count > 0 ? slots[0].car : 1; robot = slots.Count > 0 ? slots[0].robot : 1;
    }

    // the line-up changed (host: after a join / pick; guest: L from the host)
    public void NetLineup()
    {
        var n = LBNet.I;
        if (n == null) return;
        if (n.IsGuest && slots.Count > 0 && n.myKart >= 0)
        {
            foreach (var p in n.players) if (p.id == n.myId) { slots[0].car = p.car; slots[0].robot = p.robot; }
        }
        if (state == State.Lobby) { Assign(); PlaceGrid(); }
    }

    public void NetTrack(int id)
    {
        if (state != State.Lobby || id < 0 || id >= Track.Count) return;
        if (Track.Id != id && pendingTrack != id) { pendingTrack = -1; RebuildTrack(id); }
    }

    public void NetStart(int track)
    {
        if (slots.Count == 0) Join(Application.isMobilePlatform ? InputKind.Touch : InputKind.Keyboard, null);
        if (state == State.Results || state != State.Lobby) { foreach (var s in slots) if (s.hud != null) s.hud.SetActive(false); }
        if (track >= 0 && track < Track.Count && Track.Id != track) { pendingTrack = -1; RebuildTrack(track); }
        StartRace(true);
    }

    public void NetBackToLobby() { if (state != State.Lobby) ToLobby(); }

    // host: a guest's kart crossed the line (its state says finished) -> the host gives it its place
    public void NetKartFinished(int k, float t)
    {
        if (k < 0 || k >= karts.Count) return;
        Kart kt = karts[k];
        if (kt.hostPlaced) return;
        kt.hostPlaced = true;
        hostFinishCount = Mathf.Max(hostFinishCount, finishCount) + 1;
        finishCount = hostFinishCount;
        kt.finished = true; kt.finishTime = t; kt.place = finishCount;
        LBNet.I.HostFinish(k, kt.place, t);
        Debug.Log("NET finish k" + k + " place " + kt.place + " t " + t.ToString("F1"));
    }

    // guest: the host's finishing order
    public void NetPlace(int k, int place, float t)
    {
        if (k < 0 || k >= karts.Count) return;
        Kart kt = karts[k];
        kt.finished = true; kt.finishTime = t; kt.place = place;
        finishCount = Mathf.Max(finishCount, place);
    }

    // line-up from the room: the players' karts (host 0, guests 1..), this device's seat on its own kart, AI for the rest
    bool AssignOnline()
    {
        var n = LBNet.I;
        if (n == null || !n.Online || n.players.Count == 0) return false;
        int mine = n.IsHost ? 0 : n.myKart;
        if (n.IsGuest && mine < 0) return false;
        var usedCar = new bool[Cars.All.Length];
        var robotUses = new int[Robots.Count];
        var isPlayer = new bool[karts.Count];
        foreach (var p in n.players)
        {
            if (p.kart < 0 || p.kart >= karts.Count) continue;
            isPlayer[p.kart] = true;
            usedCar[p.car] = true; robotUses[p.robot]++;
            karts[p.kart].SetLook(p.car, p.robot);
            karts[p.kart].slot = -1;
            karts[p.kart].SetHuman(false);
        }
        if (slots.Count > 0 && mine >= 0 && mine < karts.Count)
        {
            slots[0].kart = karts[mine];
            karts[mine].slot = 0;
            karts[mine].SetHuman(true);
            slots[0].car = karts[mine].car; slots[0].robot = karts[mine].robot;
        }
        int nextCar = 0, rOff = 0;
        for (int i = 0; i < karts.Count; i++)
        {
            if (isPlayer[i]) continue;
            while (nextCar < usedCar.Length && usedCar[nextCar]) nextCar++;
            int c = nextCar < usedCar.Length ? nextCar : i % Cars.All.Length;
            if (nextCar < usedCar.Length) usedCar[nextCar] = true;
            int pick = -1, min = 99;
            for (int k = 0; k < Robots.Count; k++) { int r = (k + rOff) % Robots.Count; if (robotUses[r] < min) { min = robotUses[r]; pick = r; } }
            robotUses[pick]++;
            rOff = pick + 1;
            karts[i].slot = -1;
            karts[i].SetHuman(false);
            karts[i].SetLook(c, pick);
        }
        return true;
    }

    // URL: ?room=CODE joins (invite link), &lbhost=CODE hosts with that code (tests)
    void NetUrl()
    {
        if (netUrlDone || Time.unscaledTime < 3f) return;
        netUrlDone = true;
        string u = Application.absoluteURL ?? "";
        string room = Param(u, "room="), host = Param(u, "lbhost=");
        if (host.Length >= 4) { if (slots.Count == 0) Join(InputKind.Keyboard, null); LBNet.I.Host(host); }
        else if (room.Length >= 3) { if (slots.Count == 0) Join(Application.isMobilePlatform ? InputKind.Touch : InputKind.Keyboard, null); LBNet.I.Join(room); }
    }
    static string Param(string u, string key)
    {
        int i = u.IndexOf(key);
        if (i < 0) return "";
        int j = i + key.Length, e = j;
        while (e < u.Length && char.IsLetterOrDigit(u[e])) e++;
        return LBNet.CleanCode(u.Substring(j, e - j));
    }

    void HostToggle()
    {
        var n = LBNet.I;
        if (n.Online) { n.Leave(n.IsHost ? "Room closed" : "You left the room"); return; }
        while (slots.Count > 1) Leave(slots[slots.Count - 1]);      // online = one player per device
        if (slots.Count == 0) Join(Application.isMobilePlatform ? InputKind.Touch : InputKind.Keyboard, null);
        n.Host(null);
    }

    void JoinToggle()
    {
        var n = LBNet.I;
        if (n.Online) { n.Leave(n.IsHost ? "Room closed" : "You left the room"); return; }
        keypad.Open(false, "", c =>
        {
            while (slots.Count > 1) Leave(slots[slots.Count - 1]);
            if (slots.Count == 0) Join(Application.isMobilePlatform ? InputKind.Touch : InputKind.Keyboard, null);
            LBNet.I.Join(c);
        }, null);
    }

    // returns true when the lobby should skip its own input this frame (keypad open)
    bool NetLobbyInput()
    {
        var n = LBNet.I;
        if (n == null) return false;
        NetUrl();
        LayoutNetButtons(lobbyLayout == 1);
        bool portrait = Screen.height > Screen.width;
        if (keypad.Update(portrait)) return true;
        if (!Kb.typing)
        {
            if (Kb.HDown()) HostToggle();
            else if (Kb.JDown() && !n.Online) JoinToggle();
            else if (Kb.LDown() && n.Online) n.Leave(n.IsHost ? "Room closed" : "You left the room");
        }
        foreach (Gamepad pad in Gamepad.all)
        {
            if (IsGhostDev(pad) || FindPad(pad) == null) continue;
            if (pad.buttonWest.wasPressedThisFrame) HostToggle();
            else if (pad.buttonNorth.wasPressedThisFrame) JoinToggle();
        }
        var taps = Kb.TouchesBegan();
        if (Kb.MouseLeftDown() && Kb.TouchCount() == 0) taps.Add(Kb.MousePos());
        foreach (var tp in taps)
        {
            if (Hit(hostBtn, tp)) { HostToggle(); return true; }
            if (Hit(joinBtn, tp)) { JoinToggle(); return true; }
        }
        hostLbl.text = n.IsHost ? "CLOSE ROOM" : "HOST  H";
        joinLbl.text = n.IsGuest ? "LEAVE  L" : "JOIN  J";
        hostBtn.gameObject.SetActive(!n.IsGuest);
        joinBtn.gameObject.SetActive(!n.IsHost);
        return false;
    }

    // lobby status line while online
    string NetStatus()
    {
        var n = LBNet.I;
        if (n == null) return null;
        if (Time.unscaledTime - netToastT < 4f && netToast.Length > 0) return "<color=#9fe8ff>" + netToast + "</color>";
        if (!n.Online) return Time.unscaledTime - n.infoT < 5f && n.info.Length > 0 ? "<color=#ffb4a8>" + n.info + "</color>" : null;
        var sb = new System.Text.StringBuilder();
        sb.Append("<color=#9fe8ff>ONLINE  ·  ROOM <b>").Append(n.code).Append("</b></color>");
        if (!n.connected) sb.Append("  ·  ").Append(n.info);
        else
        {
            sb.Append("  ·  ");
            for (int i = 0; i < n.players.Count; i++)
            {
                var p = n.players[i];
                Color c = Cars.All[Mathf.Clamp(p.car, 0, 7)].color;
                sb.Append(i > 0 ? ", " : "").Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(Color.Lerp(c, Color.white, 0.25f))).Append(">")
                  .Append(p.id == "host" ? "HOST" : p.name).Append(p.id == n.myId || (n.IsHost && p.id == "host") ? " (you)" : "").Append("</color>");
            }
            sb.Append(n.IsHost ? "   ·   RACE starts for everyone" : "   ·   the host starts the race");
        }
        return sb.ToString();
    }
}
