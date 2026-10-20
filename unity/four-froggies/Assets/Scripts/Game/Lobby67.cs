using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ffu67c BACK TO LOBBY (Bill Oct 20: "once in the game there's no visible way to return to the lobby"). A "BACK TO LOBBY" button
// in the pause / help screen header (next to CLOSE), with a confirm:
//   pad: View (while the menu is open) asks, A confirms; keys: Backspace asks, Enter confirms; touch / mouse: tap it, tap again.
//   Anything else (or 5 s) cancels. Online rooms keep their own LEAVE in the lobby (the button explains that).
// ReturnToLobby: every local player out of vehicles / gliders / robot control, rumble zeroed, phones closed, story stopped,
// split / shared views and their cameras + HUDs removed, players stay seated (same characters) in the lobby. Logs "LOBBY67 ...".
public partial class Game
{
    RectTransform hpLobby; Text hpLobbyT; Image hpLobbyImg;
    float lobbyAskT = -1f;
    public static int LobbyReturns;
    bool LobbyAsking { get { return lobbyAskT > 0f && Time.unscaledTime < lobbyAskT; } }

    void LobbyButtonBuild()
    {
        var b = UIK.Glass(hpCard, new Color(0.2f, 0.45f, 0.9f, 0.92f), Vector2.zero, new Vector2(220f, 44f));
        hpLobbyImg = b; hpLobby = b.rectTransform; hpLobby.anchorMin = hpLobby.anchorMax = new Vector2(1f, 1f); hpLobby.pivot = new Vector2(1f, 1f);
        hpLobbyT = UIK.Label(hpLobby, "BACK TO LOBBY", 17, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 44f), Color.white);
        UIK.Stretch(hpLobbyT.rectTransform); UIK.Modernize(hpLobbyT, true, 0.3f);
    }
    void LobbyButtonLayout(bool narrow, float pad, InputKind kind, float closeW)
    {
        if (hpLobby == null) return;
        float w = narrow ? 168f : 250f;
        hpLobby.sizeDelta = new Vector2(w, narrow ? 44f : 46f);
        hpLobby.anchoredPosition = new Vector2(-pad + 6f - closeW - 10f, -pad + 6f);
        hpLobbyT.fontSize = narrow ? 14 : 16;
    }
    void LobbyAsk(string why)
    {
        bool online = Net.I != null && Net.I.Online;
        if (online) { if (helpBy != null) frogs[helpBy.frog].Toast("Online room: close help, then use LEAVE / CLOSE ROOM in the lobby", 2.5f); return; }
        if (LobbyAsking) return;
        lobbyAskT = Time.unscaledTime + 5f; lobbyAskBy = why; Sfx.Play(Sfx.Select, 0.6f, 0.9f);
        Debug.Log("LOBBY67 ask: back to the lobby? (" + why + ")");
    }
    string lobbyAskBy = "";
    // per frame while the help screen is open (HelpTick)
    void LobbyButtonTick(InputKind kind)
    {
        if (hpLobby == null) return;
        bool online = Net.I != null && Net.I.Online;
        bool ask = LobbyAsking;
        string pk = kind == InputKind.Gamepad ? (ask ? G("A") + " = YES  ·  back = stay" : "View") : kind == InputKind.Touch ? (ask ? "TAP AGAIN = YES" : "") : (ask ? "Enter = YES  ·  Esc = stay" : "Backspace");
        string t = online ? "LOBBY: use LEAVE" : ask ? "LOBBY?  " + pk : "BACK TO LOBBY" + (pk.Length > 0 ? "  ·  " + pk : "");
        if (hpLobbyT.text != t) hpLobbyT.text = t;
        hpLobbyImg.color = online ? new Color(0.3f, 0.32f, 0.36f, 0.85f) : ask ? new Color(0.95f, 0.62f, 0.12f, 0.95f) : new Color(0.2f, 0.45f, 0.9f, 0.92f);
        if (online) return;
        // touch / mouse
        bool hit = false;
        foreach (Vector2 tp in Kb.TouchesBegan()) if (Hit(hpLobby, tp, 10f)) hit = true;
        var m = Mouse.current; if (m != null && Kb.MouseLeftDown() && Kb.TouchCount() == 0 && Hit(hpLobby, m.position.ReadValue(), 4f)) hit = true;
        if (hit) { if (ask) { ReturnToLobby("tap / click confirmed"); return; } LobbyAsk("tap / click"); return; }
        // keys
        var k = Keyboard.current;
        if (k != null && !Kb.typing)
        {
            if (k.backspaceKey.wasPressedThisFrame && !ask) { LobbyAsk("Backspace"); return; }
            if (ask && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame)) { ReturnToLobby("Enter confirmed"); return; }
        }
        // pads: A confirms while asking (any seated pad)
        if (ask)
            foreach (var s in slots)
                if (s.kind == InputKind.Gamepad && s.pad != null && s.pad.added && !IsGhostDev(s.pad) && s.pad.buttonSouth.wasPressedThisFrame) { ReturnToLobby("pad A confirmed (P" + (slots.IndexOf(s) + 1) + ")"); return; }
    }
    void LobbyAskCancel(string why) { if (lobbyAskT > 0f) { lobbyAskT = -1f; Debug.Log("LOBBY67 ask cancelled (" + why + ")"); } }

    public void ReturnToLobby(string why)
    {
        if (state != State.Play) return;
        LobbyReturns++; lobbyAskT = -1f;
        RumbleStopAll("back to lobby");
        if (RobotPhone.I != null) RobotPhone.CloseAll();
        foreach (var s in slots)
        {
            Frog f = frogs[s.frog]; if (f == null) continue;
            if (f.remote != null) f.remote.ReleaseManual(false);
            if (f.vehicle != null) f.ExitVehicle();
            f.GlideStop();
            f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(s.frog), 0f);
            f.Toast("", 0f);
            if (s.cam != null) Destroy(s.cam.gameObject);
            if (s.hud != null && s.hud.panel != null) Destroy(s.hud.panel.gameObject);
            s.cam = null; s.rig = null; s.hud = null; s.world = WorldId.Ranch; s.remote = null; s.ltHold = false;
        }
        help = false; hpRoot.gameObject.SetActive(false); hpMenuChip.gameObject.SetActive(false);
        EnterLobby();   // story stop, shared cam off, cursor free, state = Lobby (sets every frog human = false)
        foreach (var s in slots) frogs[s.frog].human = true;   // the players stay seated in the lobby
        lobbyLook = false; autoStartT = -1f; sharedInit = false;
        lobbyCanvas.enabled = true; hudCanvas.enabled = false;
        fadeT = 1f;
        Sfx.Play(Sfx.Click, 0.8f);
        Debug.Log("LOBBY67 back to the lobby (" + why + ") | " + SeatList() + " | rumble pads " + RumbleActive + " | cams " + Camera.allCamerasCount + " | state " + state);
    }
}
