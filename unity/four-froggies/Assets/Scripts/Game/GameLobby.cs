using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ffu14 lobby: character showroom. Four seat cards (P1..P4 or AI) each with a live 3D turntable of the seat's character,
// a 10-tile roster picker (portrait thumbnails rendered once), title / buttons / status in one consistent style.
// Seats stay the identity used everywhere (slots, net owners, spawn points); the character is per seat (charOf).
public partial class Game
{
    public readonly int[] charOf = { 0, 1, 2, 3 };
    public static readonly Color[] PCol = { Mats.Hex("#ffd84a"), Mats.Hex("#4ad8ff"), Mats.Hex("#ff6fb0"), Mats.Hex("#9dff5a") };
    static string PHex(int i) { return "#" + ColorUtility.ToHtmlStringRGB(PCol[i & 3]); }

    Canvas lobbyCanvas, fadeCanvas;
    RectTransform lobbyRoot;
    CanvasGroup lobbyGroup;
    ModernButton playB, hostB, joinB;
    Image lobbyBg, lobbyRule, fader, viewBar, playBtn, soundBtn, creditsBtn, creditsPanel, hostBtn, joinBtn, nameBtn;
    Text lobbyTitle, lobbyKicker, lobbySub, lobbyHelp, lobbyStatus, soundText, viewText, hostText, joinBtnText, nameText, netText, playText;
    readonly Image[] cards = new Image[4], cardInner = new Image[4], cardChip = new Image[4];
    readonly RawImage[] cardRT = new RawImage[4];
    readonly Text[] cardName = new Text[4], cardKind = new Text[4], cardWho = new Text[4], cardChipText = new Text[4];
    readonly Image[] tiles = new Image[Roster.Count], tileBadge = new Image[Roster.Count];
    readonly RawImage[] tileRT = new RawImage[Roster.Count];
    readonly Text[] tileName = new Text[Roster.Count], tileBadgeText = new Text[Roster.Count];
    readonly float[] cardPop = new float[4], tilePop = new float[Roster.Count];
    Showroom.Stand[] stands;
    int lobbyLayout = -1;      // 0 landscape, 1 portrait
    float lobbyAge, fadeT, thumbWait = 2f;

    static readonly Color Ink = new Color(0.03f, 0.06f, 0.05f, 0.94f), Edge = new Color(1f, 1f, 1f, 0.16f), Mint = new Color(0.55f, 1f, 0.5f);

    void BuildLobbyUI()
    {
        lobbyCanvas = UIK.MakeCanvas("Lobby", null, 100, true);
        var sc = lobbyCanvas.GetComponent<CanvasScaler>();
        // ffu14e: Expand (= min of the width / height ratios) so the whole design always fits, centred. Shrink is the
        // max ratio and cropped the sides on a 412x915 phone (HOST / JOIN and the card edges were cut off)
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        Transform cr = lobbyCanvas.transform;
        // full-screen backdrop: the ranch keeps turning behind a dark green gradient
        lobbyBg = UIK.Img(cr, UIK.Gradient(new Color(0.01f, 0.05f, 0.03f, 0.9f), new Color(0.02f, 0.07f, 0.05f, 0.6f), new Color(0.01f, 0.04f, 0.03f, 0.94f)), Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        UIK.Stretch(lobbyBg.rectTransform);
        lobbyRoot = UIK.Rect(cr, "LobbyRoot", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1280, 720));
        lobbyGroup = lobbyRoot.gameObject.AddComponent<CanvasGroup>();
        Transform r = lobbyRoot;

        lobbyKicker = UIK.Label(r, "JAMES'S RANCH", 18, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600, 26), new Color(1f, 0.85f, 0.4f));
        lobbyTitle = UIK.Label(r, "FOUR FROGGIES", 64, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 80), Mint);
        var sh = lobbyTitle.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0f, 0.25f, 0.05f, 0.9f); sh.effectDistance = new Vector2(0, -5);
        lobbySub = UIK.Label(r, "Pick your character  ·  hop in any vehicle  ·  1-4 players, split-screen or online", 19, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 30), new Color(1f, 1f, 1f, 0.82f));
        lobbyRule = UIK.Img(r, null, new Color(0.55f, 1f, 0.5f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320, 3));

        for (int i = 0; i < 4; i++)
        {
            cards[i] = UIK.Panel(r, Edge, Vector2.zero, new Vector2(270, 300));
            Transform c = cards[i].transform;
            cardInner[i] = UIK.Panel(c, Ink, Vector2.zero, Vector2.zero);
            UIK.Anchor(cardInner[i].rectTransform, Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4));
            cardRT[i] = new GameObject("Turntable", typeof(RectTransform)).AddComponent<RawImage>();
            cardRT[i].rectTransform.SetParent(c, false);
            cardRT[i].raycastTarget = false;
            UIK.Anchor(cardRT[i].rectTransform, new Vector2(0f, 0.33f), Vector2.one, new Vector2(9, 0), new Vector2(-9, -9));
            cardName[i] = UIK.Label(c, "", 26, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Color.white);
            UIK.Anchor(cardName[i].rectTransform, new Vector2(0f, 0.2f), new Vector2(1f, 0.33f), new Vector2(6, 0), new Vector2(-6, 0));
            cardName[i].resizeTextForBestFit = true; cardName[i].resizeTextMinSize = 14; cardName[i].resizeTextMaxSize = 28;
            cardKind[i] = UIK.Label(c, "", 15, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0.7f));
            UIK.Anchor(cardKind[i].rectTransform, new Vector2(0f, 0.12f), new Vector2(1f, 0.21f), new Vector2(6, 0), new Vector2(-6, 0));
            cardWho[i] = UIK.Label(c, "", 17, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Color.white);
            UIK.Anchor(cardWho[i].rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.12f), new Vector2(6, 4), new Vector2(-6, 0));
            cardWho[i].supportRichText = true;
            cardChip[i] = UIK.Panel(c, PCol[i], Vector2.zero, new Vector2(66, 30));
            UIK.Anchor(cardChip[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14, -42), new Vector2(84, -14));
            cardChipText[i] = UIK.Label(cardChip[i].transform, "", 17, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.05f, 0.05f, 0.05f));
            UIK.Stretch(cardChipText[i].rectTransform);
            Object.Destroy(cardChipText[i].GetComponent<Outline>());
        }
        for (int c = 0; c < Roster.Count; c++)
        {
            tiles[c] = UIK.Panel(r, Edge, Vector2.zero, new Vector2(100, 100));
            Transform t = tiles[c].transform;
            var inner = UIK.Panel(t, Ink, Vector2.zero, Vector2.zero);
            UIK.Anchor(inner.rectTransform, Vector2.zero, Vector2.one, new Vector2(3, 3), new Vector2(-3, -3));
            tileRT[c] = new GameObject("Thumb", typeof(RectTransform)).AddComponent<RawImage>();
            tileRT[c].rectTransform.SetParent(t, false);
            tileRT[c].raycastTarget = false;
            UIK.Anchor(tileRT[c].rectTransform, Vector2.zero, Vector2.one, new Vector2(6, 6), new Vector2(-6, -6));
            tileRT[c].color = new Color(0.1f, 0.12f, 0.12f);
            var band = UIK.Img(t, null, new Color(0f, 0f, 0f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIK.Anchor(band.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.25f), new Vector2(6, 6), new Vector2(-6, 0));
            tileName[c] = UIK.Label(t, Roster.Name(c), 14, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Color.white);
            UIK.Anchor(tileName[c].rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.25f), new Vector2(7, 6), new Vector2(-7, 0));
            tileName[c].resizeTextForBestFit = true; tileName[c].resizeTextMinSize = 8; tileName[c].resizeTextMaxSize = 15;
            tileName[c].horizontalOverflow = HorizontalWrapMode.Wrap;
            tileBadge[c] = UIK.Panel(t, PCol[0], Vector2.zero, Vector2.zero);
            UIK.Anchor(tileBadge[c].rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-46, -30), new Vector2(-4, -4));
            tileBadgeText[c] = UIK.Label(tileBadge[c].transform, "", 15, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.05f, 0.05f, 0.05f));
            UIK.Stretch(tileBadgeText[c].rectTransform);
            Object.Destroy(tileBadgeText[c].GetComponent<Outline>());
        }

        // ffu15: modern pill buttons (gradient, glow, shadow, icon, key badges, hover / press / focus) - see ModernButton
        playB = new ModernButton(r, "PLAY", 0, new Color(0.18f, 0.72f, 0.32f, 1f));
        playBtn = playB.root; playText = playB.label;
        hostB = new ModernButton(r, "HOST", 1, new Color(0.2f, 0.48f, 0.92f, 1f));
        hostBtn = hostB.root; hostText = hostB.label;
        joinB = new ModernButton(r, "JOIN", 2, new Color(0.56f, 0.34f, 0.92f, 1f));
        joinBtn = joinB.root; joinBtnText = joinB.label;
        nameBtn = UIK.Panel(r, new Color(1f, 1f, 1f, 0.1f), Vector2.zero, new Vector2(320, 42));
        nameText = UIK.Label(nameBtn.transform, "", 20, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Color.white);
        UIK.Stretch(nameText.rectTransform); nameText.supportRichText = true;
        viewBar = UIK.Panel(r, new Color(1f, 1f, 1f, 0.1f), Vector2.zero, new Vector2(460, 42));
        viewText = UIK.Label(viewBar.transform, "", 19, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Color.white);
        UIK.Stretch(viewText.rectTransform); viewText.supportRichText = true;
        netText = UIK.Label(r, "", 24, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1150, 46), new Color(1f, 0.9f, 0.4f));
        netText.supportRichText = true;
        lobbyStatus = UIK.Label(r, "", 20, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1150, 30), new Color(0.75f, 1f, 0.72f));
        lobbyHelp = UIK.Label(r, "", 15, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 44), new Color(1, 1, 1, 0.7f));

        // corner buttons are anchored to the screen corners, outside the centred design
        soundBtn = UIK.Panel(cr, new Color(0f, 0f, 0f, 0.5f), Vector2.zero, new Vector2(176, 38));
        soundBtn.rectTransform.anchorMin = soundBtn.rectTransform.anchorMax = new Vector2(0f, 1f);   // top-left: the arcade page has its own button top-right
        soundBtn.rectTransform.anchoredPosition = new Vector2(260, -28);
        soundText = UIK.Label(soundBtn.transform, "", 17, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Color.white);
        UIK.Stretch(soundText.rectTransform); soundText.supportRichText = true;
        creditsBtn = UIK.Panel(cr, new Color(0f, 0f, 0f, 0.5f), Vector2.zero, new Vector2(150, 38));
        creditsBtn.rectTransform.anchorMin = creditsBtn.rectTransform.anchorMax = new Vector2(0f, 1f);
        creditsBtn.rectTransform.anchoredPosition = new Vector2(87, -28);
        var cl = UIK.Label(creditsBtn.transform, "CREDITS (C)", 16, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.8f, 0.95f, 1f));
        UIK.Stretch(cl.rectTransform);
        creditsPanel = UIK.Panel(cr, new Color(0.01f, 0.05f, 0.03f, 0.97f), Vector2.zero, new Vector2(1180, 640));
        var ct = UIK.Label(creditsPanel.transform, CreditsText, 17, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1120, 610), Color.white);
        ct.supportRichText = true;
        ct.horizontalOverflow = HorizontalWrapMode.Wrap;
        ct.resizeTextForBestFit = true; ct.resizeTextMinSize = 9; ct.resizeTextMaxSize = 17;
        UIK.Stretch(ct.rectTransform);
        ct.rectTransform.offsetMin = new Vector2(30, 14); ct.rectTransform.offsetMax = new Vector2(-30, -14);
        creditsPanel.gameObject.SetActive(false);

        // fade-to-play overlay (above everything, both lobby and HUD)
        fadeCanvas = UIK.MakeCanvas("Fade", null, 900, true);
        fader = UIK.Img(fadeCanvas.transform, null, new Color(0.01f, 0.03f, 0.02f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        UIK.Stretch(fader.rectTransform);
        fadeCanvas.enabled = false;

        stands = new Showroom.Stand[4];
        for (int i = 0; i < 4; i++) { stands[i] = new Showroom.Stand(i); stands[i].Set(charOf[i], false); cardRT[i].texture = stands[i].rt; }
    }

    // ---------------- layout ----------------
    void PlaceCards(Vector2[] pos, Vector2 size, int nameSize)
    {
        for (int i = 0; i < 4; i++)
        {
            cards[i].rectTransform.anchoredPosition = pos[i];
            cards[i].rectTransform.sizeDelta = size;
            cardName[i].resizeTextMaxSize = nameSize;
            // crop the turntable texture to the visible area's aspect (no stretching)
            float w = size.x - 18f, h = size.y * 0.67f - 9f;
            float ta = stands[i].rt.width / (float)stands[i].rt.height, va = w / Mathf.Max(1f, h);
            cardRT[i].uvRect = va >= ta ? new Rect(0f, (1f - ta / va) * 0.5f, 1f, ta / va) : new Rect((1f - va / ta) * 0.5f, 0f, va / ta, 1f);
        }
    }

    void LayoutLobby()
    {
        bool portrait = Screen.height > Screen.width;
        int want = portrait ? 1 : 0;
        var sc = lobbyCanvas.GetComponent<CanvasScaler>();
        Vector2 design = portrait ? new Vector2(780, 1540) : new Vector2(1300, 730);
        if (want == lobbyLayout) return;
        lobbyLayout = want;
        sc.referenceResolution = design;
        lobbyRoot.sizeDelta = design;
        System.Action<Graphic, float, float, float, float> put = (g, x, y, w, h) => { g.rectTransform.anchoredPosition = new Vector2(x, y); g.rectTransform.sizeDelta = new Vector2(w, h); };
        if (portrait)
        {
            put(lobbyKicker, 0, 712, 600, 26); lobbyKicker.fontSize = 18;
            put(lobbyTitle, 0, 664, 700, 74); lobbyTitle.fontSize = 58;
            put(lobbySub, 0, 606, 680, 50); lobbySub.fontSize = 18;
            put(lobbyRule, 0, 578, 260, 3);
            PlaceCards(new[] { new Vector2(-176, 398), new Vector2(176, 398), new Vector2(-176, 66), new Vector2(176, 66) }, new Vector2(336, 320), 30);
            for (int c = 0; c < Roster.Count; c++) put(tiles[c], -270 + (c % 5) * 135, -180 - (c / 5) * 135, 126, 126);
            put(hostBtn, -250, -462, 224, 96);
            put(playBtn, 0, -462, 252, 96);
            put(joinBtn, 250, -462, 224, 96);
            put(netText, 0, -548, 700, 70); netText.fontSize = 24;
            put(lobbyStatus, 0, -604, 700, 50); lobbyStatus.fontSize = 21;
            put(lobbyHelp, 0, -730, 690, 80); lobbyHelp.fontSize = 17;

        }
        else
        {
            put(lobbyKicker, 0, 330, 600, 24); lobbyKicker.fontSize = 17;
            put(lobbyTitle, 0, 290, 1100, 70); lobbyTitle.fontSize = 60;
            put(lobbySub, 0, 249, 1100, 28); lobbySub.fontSize = 18;
            put(lobbyRule, 0, 229, 320, 3);
            PlaceCards(new[] { new Vector2(-441, 74), new Vector2(-147, 74), new Vector2(147, 74), new Vector2(441, 74) }, new Vector2(278, 290), 28);
            for (int c = 0; c < Roster.Count; c++) put(tiles[c], -495 + c * 110, -132, 102, 102);
            put(hostBtn, -330, -224, 270, 64);
            put(playBtn, 0, -224, 330, 64);
            put(joinBtn, 330, -224, 270, 64);
            put(lobbyStatus, 0, -322, 1150, 28); lobbyStatus.fontSize = 19;
            put(lobbyHelp, 0, -350, 1200, 24); lobbyHelp.fontSize = 14;

        }
        lastNetRow = -1;
    }

    int lastNetRow = -1;
    // NAME / VIEW / room code row: depends on what is visible
    void LayoutNetRow(bool viewOn, bool online)
    {
        int key = (lobbyLayout == 1 ? 4 : 0) + (viewOn ? 2 : 0) + (online ? 1 : 0);
        if (key == lastNetRow) return;
        lastNetRow = key;
        System.Action<Graphic, float, float, float, float> put = (g, x, y, w, h) => { g.rectTransform.anchoredPosition = new Vector2(x, y); g.rectTransform.sizeDelta = new Vector2(w, h); };
        if (lobbyLayout == 1)
        {
            if (viewOn) { put(viewBar, -170, -664, 340, 52); put(nameBtn, 180, -664, 320, 52); }
            else put(nameBtn, 0, -664, 420, 52);
            put(netText, 0, -548, 700, 70);
        }
        else
        {
            if (viewOn) { put(viewBar, -210, -284, 500, 40); put(nameBtn, 250, -284, 340, 40); put(netText, 0, -284, 0, 0); }
            else if (online) { put(nameBtn, 410, -284, 300, 40); put(netText, -150, -284, 860, 44); }
            else { put(nameBtn, 0, -284, 380, 40); put(netText, 0, -322, 1150, 30); }
        }
    }

    // ---------------- characters ----------------
    bool HumanSeat(int seat) { return SlotForFrog(seat) != null || (Net.I != null && Net.I.TakenByRemote(seat)); }
    int SeatOfChar(int c) { for (int i = 0; i < 4; i++) if (charOf[i] == c) return i; return -1; }
    bool CharHeldByHuman(int c, int exceptSeat)
    {
        for (int j = 0; j < 4; j++) if (j != exceptSeat && charOf[j] == c && HumanSeat(j)) return true;
        return false;
    }

    public bool CharHeldByOther(int c, int seat) { return CharHeldByHuman(c, seat); }

    public void SetSeatChar(int seat, int c, bool fx)
    {
        if (seat < 0 || seat > 3 || !Roster.Valid(c) || charOf[seat] == c) return;
        charOf[seat] = c;
        frogs[seat].SetChar(c);
        if (stands != null) stands[seat].Set(c, fx && state == State.Lobby);
        if (fx) { cardPop[seat] = 1f; tilePop[c] = 1f; }
    }

    // AI seats use froggies nobody plays (their own froggy first); never a cat or dog, never a duplicate
    public void FixAiChars()
    {
        if (Net.I != null && Net.I.IsGuest) return;   // the host decides
        if (demoKeepChars) return;
        bool[] used = new bool[Roster.Count];
        for (int j = 0; j < 4; j++) if (HumanSeat(j)) used[charOf[j]] = true;
        for (int j = 0; j < 4; j++)
        {
            if (HumanSeat(j)) continue;
            int c = charOf[j];
            if (used[c] || !Roster.IsFrog(c))
            {
                c = !used[j] ? j : -1;
                for (int f = 0; f < 4 && c < 0; f++) if (!used[f]) c = f;
                for (int f = 4; f < Roster.Count && c < 0; f++) if (!used[f]) c = f;
                bool was = Net.I != null && Net.I.IsHost;
                SetSeatChar(j, c, false);
                if (was) Net.I.CharsChanged();
            }
            used[c] = true;
        }
    }

    void PickChar(Slot s, int c)
    {
        if (s == null || !Roster.Valid(c) || charOf[s.frog] == c) return;
        if (CharHeldByHuman(c, s.frog)) { Sfx.Play(Sfx.BumpSoft, 0.5f); return; }
        if (Net.I != null && Net.I.IsGuest) { Net.I.RequestChar(c); Sfx.Play(Sfx.Select, 0.5f); return; }
        if (state == State.Play) return;
        SetSeatChar(s.frog, c, true);
        Sfx.Play(Sfx.Select, 0.6f, 0.9f + c * 0.03f);
        if (slots.IndexOf(s) == 0) PlayerPrefs.SetInt("ff.char", c);
        FixAiChars();
        if (Net.I != null && Net.I.IsHost) Net.I.CharsChanged();
    }

    void Cycle(Slot s, int dir)
    {
        if (s == null) return;
        int c = charOf[s.frog];
        int tries = Mathf.Abs(dir) == 5 ? 1 : Roster.Count - 1;
        for (int k = 1; k <= tries; k++)
        {
            int n = ((c + dir * k) % Roster.Count + Roster.Count) % Roster.Count;
            if (n == c) break;
            if (!CharHeldByHuman(n, s.frog)) { PickChar(s, n); return; }
        }
    }

    // ---------------- per-frame lobby visuals ----------------
    void TickShowroom(float dt)
    {
        lobbyAge += dt;
        lobbyGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(lobbyAge / 0.6f));
        lobbyRoot.anchoredPosition = new Vector2(0f, (1f - Mathf.Clamp01(lobbyAge / 0.6f)) * -18f);
        if (thumbWait > 0f) { thumbWait -= 1f; if (thumbWait <= 0f) Showroom.RenderThumbs(); }
        else if (Showroom.ThumbsLost()) Showroom.RenderThumbs();
        for (int c = 0; c < Roster.Count; c++)
        {
            if (Showroom.ThumbsReady && tileRT[c].texture != Showroom.Thumbs[c]) { tileRT[c].texture = Showroom.Thumbs[c]; tileRT[c].color = Color.white; }
            tilePop[c] = Mathf.Max(0f, tilePop[c] - dt * 3f);
            tiles[c].rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(tilePop[c] * Mathf.PI) * 0.12f);
        }
        for (int i = 0; i < 4; i++)
        {
            if (stands[i].ch != charOf[i]) stands[i].Set(charOf[i], true);
            stands[i].Tick(dt, true);
            cardPop[i] = Mathf.Max(0f, cardPop[i] - dt * 3f);
            cards[i].rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(cardPop[i] * Mathf.PI) * 0.05f);
        }
    }

    void StandsOff() { if (stands != null) foreach (var s in stands) s.Tick(0f, false); }

    void TickFade(float dt)
    {
        if (fadeT <= 0f) { if (fadeCanvas.enabled) fadeCanvas.enabled = false; return; }
        fadeT = Mathf.Max(0f, fadeT - dt * 2.2f);
        fadeCanvas.enabled = true;
        fader.color = new Color(0.01f, 0.03f, 0.02f, Mathf.SmoothStep(0f, 1f, fadeT));
    }

    // a player's seat colour (local: P1..P4 by join order; online: by seat)
    Color SeatColor(int seat, out string tag)
    {
        Slot s = SlotForFrog(seat);
        Net net = Net.I;
        bool online = net != null && net.Online;
        if (s != null) { int pn = slots.IndexOf(s); tag = online ? "YOU" : "P" + (pn + 1); return PCol[online ? seat : pn]; }
        if (online && net.TakenByRemote(seat)) { tag = net.OwnerLabel(seat).StartsWith("HOST") ? "HOST" : "ONLINE"; return PCol[seat]; }
        tag = "AI";
        return new Color(0.55f, 0.58f, 0.6f);
    }

    void RefreshLobby()
    {
        Net net = Net.I;
        bool online = net != null && net.Online;
        bool touchOnly = TouchOnly;
        for (int i = 0; i < 4; i++)
        {
            int ch = charOf[i];
            string tag;
            Color pc = SeatColor(i, out tag);
            bool human = tag != "AI";
            Slot s = SlotForFrog(i);
            cards[i].color = human ? new Color(pc.r, pc.g, pc.b, 0.95f) : Edge;
            cardChip[i].color = human ? pc : new Color(0.32f, 0.35f, 0.37f, 0.95f);
            cardChipText[i].text = tag;
            cardChipText[i].color = human ? new Color(0.05f, 0.05f, 0.05f) : new Color(0.9f, 0.92f, 0.92f);
            cardName[i].text = Roster.Name(ch);
            cardName[i].color = Color.Lerp(Roster.UiColor(ch), Color.white, 0.35f);
            cardKind[i].text = Roster.Species(ch).ToUpper() + "  ·  " + Roster.Ability(ch);
            string who;
            if (s != null)
            {
                string dev = s.kind == InputKind.Gamepad ? "Gamepad" : s.kind == InputKind.Keyboard ? "Keyboard" : "Touch";
                who = (s.name.Length > 0 ? "<color=#ffe680>" + s.name + "</color>  " : "") + "<size=14>" + dev + (online ? " · " + net.OwnerLabel(i) : " · READY") + "</size>";
            }
            else if (human) who = (net.names[i].Length > 0 ? "<color=#ffe680>" + net.names[i] + "</color>  " : "") + "<size=14>" + net.OwnerLabel(i) + "</size>";
            else who = "<size=14><color=#b8c2c0>" + (touchOnly ? "AI froggy" : "open seat · AI froggy") + "</color></size>";
            cardWho[i].text = who;
        }
        for (int c = 0; c < Roster.Count; c++)
        {
            int seat = SeatOfChar(c);
            string tag = "AI";
            Color pc = seat >= 0 ? SeatColor(seat, out tag) : Color.gray;
            bool held = seat >= 0 && tag != "AI";
            tiles[c].color = held ? pc : Edge;
            tileBadge[c].gameObject.SetActive(held);
            if (held) { tileBadge[c].color = pc; tileBadgeText[c].text = tag == "YOU" ? "YOU" : tag == "ONLINE" || tag == "HOST" ? "ON" : tag; }
            tileName[c].color = held ? Color.Lerp(pc, Color.white, 0.5f) : Color.white;
        }
        soundText.text = "SOUND: " + Sfx.LevelName + (touchOnly ? "" : " <size=13>(M)</size>");
        bool viewOn = !touchOnly && !online;
        viewBar.gameObject.SetActive(viewOn);
        LayoutNetRow(viewOn, online);
        hostText.text = online ? (net.IsHost ? "CLOSE ROOM" : "LEAVE") : "HOST";
        hostB.SetBadge(touchOnly ? "" : online ? "X · L" : "X · H");
        hostB.SetTint(online ? new Color(0.85f, 0.32f, 0.26f, 1f) : new Color(0.2f, 0.48f, 0.92f, 1f));
        joinBtnText.text = "JOIN";
        joinB.SetBadge(touchOnly ? "" : "Y · J");
        joinBtn.gameObject.SetActive(!online);
        bool waitHost = online && net.IsGuest;
        playText.text = waitHost ? "WAITING FOR HOST" : "PLAY";
        playB.SetBadge(touchOnly || waitHost ? "" : "A · ENTER");
        playB.SetTint(waitHost ? new Color(0.25f, 0.35f, 0.28f, 0.95f) : new Color(0.18f, 0.72f, 0.32f, 1f));
        // gamepad focus: PLAY is the default action once someone has joined; X / Y held highlight HOST / JOIN
        bool xHeld = false, yHeld = false;
        foreach (var gp in UnityEngine.InputSystem.Gamepad.all) if (gp != null && gp.added) { xHeld |= gp.buttonWest.isPressed; yHeld |= gp.buttonNorth.isPressed; }
        playB.focus = slots.Count > 0 && !waitHost; hostB.focus = xHeld; joinB.focus = yHeld;
        bool mouseOk = !touchOnly && Kb.TouchCount() == 0;
        playB.Tick(mouseOk); hostB.Tick(mouseOk); joinB.Tick(mouseOk);
        Slot me = slots.Count > 0 ? slots[0] : null;
        nameText.text = "NAME  <color=#ffe680>" + (me != null ? (me.name.Length > 0 ? me.name : Roster.Name(charOf[me.frog])) : "-") + "</color>  <size=13>" + (touchOnly ? "tap to change" : "RB / N / click") + "</size>";

        string nt = "";
        if (online)
        {
            if (net.IsHost && net.connected)
                nt = "ROOM CODE  <size=34><color=#ffffff>" + net.code + "</color></size>  <size=17>friends press JOIN and type it</size>" + (net.PlayersLine().Length > 0 && lobbyLayout == 1 ? "\n<size=18>" + net.PlayersLine() + "</size>" : "");
            else if (net.IsGuest && net.connected)
                nt = "ROOM <color=#ffffff>" + net.code + "</color>" + (net.PlayersLine().Length > 0 ? "   <size=18>" + net.PlayersLine() + "</size>" : "");
            else nt = net.info;
        }
        string err = "";
        if (!online && net != null && net.info.Length > 0 && Time.unscaledTime - net.infoT < 8f) err = "<color=#ff8a80>" + net.info + "</color>";
        netText.text = nt;
        lobbyStatus.supportRichText = true;

        if (online)
        {
            lobbyHelp.text = "Online: one player per device. Pick any free character (tap it / D-pad / arrows). NAME is optional.  " +
                (net.IsHost ? "PLAY starts it for everyone; friends can join later too." : "The host starts the game.");
            if (slots.Count == 0) lobbyStatus.text = "Press A / Enter or tap a character to take a seat";
            else lobbyStatus.text = net.IsGuest ? (net.connected ? net.info : "") : (net.connected ? "Ready - PLAY when your friends are in" : "");
            return;
        }
        if (touchOnly)
        {
            lobbyHelp.text = "Tap a character, then PLAY. Empty seats are AI froggies.\nWith friends: HOST shows a room code, they tap JOIN and type it.";
            Slot ts = FindSlot(InputKind.Touch);
            lobbyStatus.text = ts == null ? "Tap a character to pick it" : "You are <color=" + Roster.UiHex(charOf[ts.frog]) + ">" + (ts.name.Length > 0 ? ts.name + " (" + Roster.Name(charOf[ts.frog]) + ")" : Roster.Name(charOf[ts.frog])) + "</color> - tap PLAY";
            if (err.Length > 0) lobbyStatus.text = err;
            return;
        }
        lobbyHelp.text = "Gamepad: A join · D-pad pick (left/right, up/down) · B leave · Start play     Keyboard: Enter join/play · arrows pick · Esc leave     Mouse/touch: click a character";
        viewText.text = shared ? "VIEW   Split   <color=#ffd84a>[ SHARED ]</color>  <size=13>(Back / V)</size>"
                               : "VIEW   <color=#ffd84a>[ SPLIT ]</color>   Shared  <size=13>(Back / V)</size>";
        if (slots.Count == 0) lobbyStatus.text = "Press A on a gamepad, Enter on the keyboard, or click a character to join";
        else lobbyStatus.text = slots.Count + " player" + (slots.Count > 1 ? "s" : "") + " ready - Start / A again / Enter / PLAY to begin" + (autoStartT > 0f ? "  (auto in " + Mathf.CeilToInt(autoStartT) + ")" : "");
        if (err.Length > 0) lobbyStatus.text = err;
    }

    // tile taps / clicks: pick that character for this device's seat (joining first if needed)
    bool LobbyTilePick(InputKind kind, Vector2 pos)
    {
        for (int c = 0; c < Roster.Count; c++)
        {
            if (!Hit(tiles[c], pos)) continue;
            Slot s = FindSlot(kind);
            if (s == null) s = Join(kind, null);
            if (s != null) PickChar(s, c);
            return true;
        }
        return false;
    }
}
