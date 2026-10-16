using UnityEngine;
using UnityEngine.UI;

// u6g: DRIVING MODE per player. KID (default) = the lambo9b handling, unchanged. EXPERT = Kart.expert (realistic throttle,
// rear wheelspin, no assists, more power / top speed). Saved per controller kind in PlayerPrefs ("lbMode.Keyboard" etc).
// ?lbexpert=1 forces EXPERT (probes), ?lbexpert=0 forces KID. AI karts are never expert.
public partial class Game
{
    Image modeBtn;
    Text modeLbl;

    static int ForcedMode()
    {
        string u = Application.absoluteURL ?? "";
        if (u.Contains("lbexpert=1")) return 1;
        if (u.Contains("lbexpert=0")) return 0;
        return -1;
    }

    static bool DriveModeSaved(InputKind kind)
    {
        int f = ForcedMode();
        if (f >= 0) return f == 1;
        return PlayerPrefs.GetInt("lbMode." + kind, 0) == 1;
    }

    void ApplyDriveModes()
    {
        foreach (var k in karts) k.expert = false;
        foreach (var s in slots) if (s.kart != null) s.kart.expert = s.expert;
        string m = "";
        foreach (var s in slots) m += " P" + (slots.IndexOf(s) + 1) + "=" + (s.expert ? "EXPERT" : "KID");
        Debug.Log("U6G: driving modes" + m);
    }

    void ToggleDriveMode(Slot s)
    {
        if (s == null) return;
        s.expert = !s.expert;
        PlayerPrefs.SetInt("lbMode." + s.kind, s.expert ? 1 : 0);
        PlayerPrefs.Save();
        if (s.kart != null) s.kart.expert = s.expert;
        Sfx.Play(Sfx.Click, 0.5f);
        Debug.Log("U6G: P" + (slots.IndexOf(s) + 1) + " driving mode " + (s.expert ? "EXPERT" : "KID"));
    }

    // u6h probe knob: ?lbseats=N (2-4) seats N controller players with no device, in the lobby only, so headless probes can
    // check the split-screen lobby (cards, per-player MODE chips). SeatTick leaves those device-less seats alone. Not for racing.
    static bool probeSeats; bool probeSeatsDone;
    void ProbeSeats()
    {
        if (probeSeatsDone || state != State.Lobby) return;
        probeSeatsDone = true;
        string u = Application.absoluteURL ?? "";
        int i = u.IndexOf("lbseats=");
        if (i < 0 || i + 8 >= u.Length || !char.IsDigit(u[i + 8])) return;
        int n = Mathf.Clamp(u[i + 8] - '0', 2, 4);
        probeSeats = true;
        while (slots.Count > 0) Leave(slots[slots.Count - 1]);
        for (int k = 0; k < n; k++)
        {
            int car = 0; while (!CarFree(car, null)) car++;
            int robot = slots.Count % Robots.Count; while (!RobotFree(robot, null)) robot = (robot + 1) % Robots.Count;
            slots.Add(new Slot { kind = InputKind.Gamepad, pad = null, joinTime = Time.unscaledTime, car = car, robot = robot, expert = false });
        }
        Assign(); PlaceGrid();
        Debug.Log("U6H: probe seats " + n + " (device-less controller players, lobby only)");
    }

    string DriveModesNow()
    {
        if (slots.Count == 0) return "";
        string m = "\n";
        for (int i = 0; i < slots.Count; i++) m += (i > 0 ? "   ·   " : "") + "P" + (i + 1) + " " + (slots[i].expert ? "<color=#ff8a5a>EXPERT</color>" : "<color=#7dff9a>KID</color>");
        return m;
    }

    void MakeModeButton(Transform r)
    {
        modeBtn = Glass(r, new Color(0.12f, 0.15f, 0.2f, 0.92f), new Vector2(0.5f, 0.5f));
        modeLbl = UIK.Label(modeBtn.transform, "MODE  KID", 13, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160, 40), Color.white);
        modeLbl.supportRichText = true;
        UIK.Stretch(modeLbl.rectTransform);
    }

    void RefreshModeButton()
    {
        if (modeLbl == null) return;
        string t;
        if (slots.Count <= 1)
        {
            bool ex = slots.Count == 1 ? slots[0].expert : DriveModeSaved(Application.isMobilePlatform ? InputKind.Touch : InputKind.Keyboard);
            t = "MODE  " + (ex ? "<color=#ff8a5a><b>EXPERT</b></color>" : "<color=#7dff9a><b>KID</b></color>") + "\n<size=9>tap · G · pad Back</size>";
        }
        else
        {
            t = "";
            for (int i = 0; i < slots.Count; i++) t += (i > 0 ? (i == 2 ? "\n" : "  ") : "") + "P" + (i + 1) + " " + (slots[i].expert ? "<color=#ff8a5a>EXP</color>" : "<color=#7dff9a>KID</color>");
        }
        modeLbl.text = t;
    }
}
