using UnityEngine;
using UnityEngine.UI;

// lambo7 MODERN SHOWROOM LOBBY (Bill: "the lobby looks like an old arcade game ... better fidelity, better resolution,
// better colors, better everything"). Same controls / seats / picks as before; the look is new:
//  * dark showroom: the island orbit is dimmed under a deep graphite gradient with two soft colour glows;
//  * glass cards (rounded translucent panels, thin edge in the player's colour, soft glow behind) holding the 1:1 sized
//    turntable view (Showroom.Stand, masked to a rounded window), car + robot names, and slim gradient stat bars;
//  * car / robot tiles = rounded glass tiles with 2x thumbnails, a name band, a colour frame for the picking player and a
//    white frame for the tap / mouse player's own pick, eased scale + colour on every pick;
//  * track card with a live preview thumbnail of the selected track (rendered once per track build), < > buttons;
//  * RACE = gradient pill with a breathing glow; Montserrat / Inter text everywhere (UIK), no hard outlines.
public partial class Game
{
    Image lobbyGlowA, lobbyGlowB, playGlow, trackThumbWin;
    RawImage trackThumb;
    readonly Image[] cardEdge = new Image[4], cardGlow = new Image[4];
    readonly Image[] carWin = new Image[8], robotWin = new Image[7], carFrames = new Image[8], robotFrames = new Image[7];
    readonly Color[] carTileCol = new Color[8], robotTileCol = new Color[7];
    readonly float[] cardFade = new float[4];
    int lastVisible = -1;
    RenderTexture trackRT;
    Camera trackCam;
    float lobbyT;

    static readonly Color GlassCol = new Color(0.07f, 0.085f, 0.115f, 0.86f), TileCol = new Color(0.09f, 0.105f, 0.14f, 0.9f);

    static Image Glass(Transform p, Color c, Vector2 anchor) { return UIK.RoundImg(p, UIK.Round2, c, anchor, Vector2.zero, new Vector2(10, 10), 2f); }

    void BuildLobbyUI()
    {
        lobbyCanvas = UIK.MakeCanvas("Lobby", null, 100, true);
        Transform r = lobbyCanvas.transform;
        lobbyBg = UIK.Img(r, UIK.Gradient(new Color(0.035f, 0.045f, 0.07f, 0.9f), new Color(0.02f, 0.025f, 0.04f, 0.8f), new Color(0.01f, 0.012f, 0.02f, 0.96f)), Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        UIK.Stretch(lobbyBg.rectTransform); lobbyBg.rectTransform.offsetMin = new Vector2(-60, -60); lobbyBg.rectTransform.offsetMax = new Vector2(60, 60);
        lobbyGlowA = UIK.Img(r, UIK.SoftDot, new Color(0.2f, 0.65f, 1f, 0.16f), new Vector2(0f, 1f), new Vector2(160, -120), new Vector2(900, 700));
        lobbyGlowB = UIK.Img(r, UIK.SoftDot, new Color(1f, 0.25f, 0.6f, 0.11f), new Vector2(1f, 0f), new Vector2(-140, 120), new Vector2(900, 700));
        lobbyTitle = UIK.Label(r, "LAMBO BLAST", 46, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 60), Color.white);
        UIK.Modernize(lobbyTitle, true, 0.55f);
        var tg = lobbyTitle.gameObject.AddComponent<UIGradient>(); tg.top = Color.white; tg.bottom = new Color(0.55f, 0.88f, 1f);
        lobbySub = UIK.Label(r, "", 15, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180, 26), new Color(0.82f, 0.88f, 0.96f, 0.85f));
        string[] lab = { "SPEED", "ACCEL", "HANDLING", "WEIGHT" };
        for (int i = 0; i < 4; i++) cardGlow[i] = UIK.RoundImg(r, UIK.SoftRect, new Color(1f, 1f, 1f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10, 10), 1f);   // all glows under all cards
        for (int i = 0; i < 4; i++)
        {
            cards[i] = Glass(r, GlassCol, new Vector2(0.5f, 0.5f));
            Transform c = cards[i].transform;
            cardEdge[i] = UIK.RoundImg(c, UIK.Edge2, new Color(1f, 1f, 1f, 0.1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, 2f);
            UIK.Stretch(cardEdge[i].rectTransform);
            cardSwatch[i] = UIK.RoundImg(c, UIK.Round2, Color.white, new Vector2(0f, 1f), Vector2.zero, new Vector2(10, 6), 8f);
            cardHead[i] = UIK.Label(c, "", 14, TextAnchor.MiddleLeft, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(280, 22), new Color(0.85f, 0.9f, 1f, 0.9f));
            cardHead[i].supportRichText = true;
            cardPreviewFrame[i] = UIK.RoundImg(c, UIK.Round2, Color.white, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(10, 10), 2.4f);
            cardPreviewFrame[i].gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var pr = UIK.Rect(cardPreviewFrame[i].transform, "Preview", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIK.Stretch(pr);
            cardPreview[i] = pr.gameObject.AddComponent<RawImage>();
            cardPreview[i].texture = stands[i].rt;
            cardPreview[i].raycastTarget = false;
            cardTexts[i] = UIK.Label(c, "", 20, TextAnchor.UpperLeft, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(280, 60), Color.white);
            cardTexts[i].supportRichText = true;
            bars[i] = new Image[4]; barLabs[i] = new Text[4]; barBgs[i] = new Image[4];
            for (int b = 0; b < 4; b++)
            {
                barLabs[i][b] = UIK.Label(c, lab[b], 11, TextAnchor.MiddleLeft, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(110, 16), new Color(0.75f, 0.82f, 0.92f, 0.85f));
                barBgs[i][b] = UIK.RoundImg(c, UIK.Round2, new Color(1f, 1f, 1f, 0.08f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(140, 6), 10f);
                bars[i][b] = UIK.RoundImg(c, UIK.Round2, Color.white, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(140, 6), 10f);
                var bgr = bars[i][b].gameObject.AddComponent<UIGradient>(); bgr.top = Color.white; bgr.bottom = new Color(0.75f, 0.75f, 0.75f);
                bars[i][b].rectTransform.pivot = new Vector2(0f, 0.5f);
                barBgs[i][b].rectTransform.pivot = new Vector2(0f, 0.5f);
            }
        }
        for (int i = 0; i < 8; i++)
        {
            carTiles[i] = Glass(r, TileCol, new Vector2(0.5f, 0.5f));
            Transform t = carTiles[i].transform;
            carWin[i] = UIK.RoundImg(t, UIK.Round2, Color.white, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(10, 10), 2.6f);
            carWin[i].gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var th = UIK.Rect(carWin[i].transform, "Thumb", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIK.Stretch(th);
            carThumbs[i] = th.gameObject.AddComponent<RawImage>();
            carThumbs[i].raycastTarget = false;
            carThumbs[i].color = new Color(1f, 1f, 1f, 0f);
            carStrips[i] = UIK.RoundImg(t, UIK.Round2, Cars.All[i].color, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(10, 3), 12f);
            carNames[i] = UIK.Label(t, Cars.All[i].name.ToUpper(), 12, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(118, 16), new Color(0.92f, 0.95f, 1f));
            carBadges[i] = UIK.Label(t, "", 12, TextAnchor.UpperLeft, new Vector2(0f, 1f), Vector2.zero, new Vector2(118, 18), Color.white);
            carBadges[i].supportRichText = true;
            carFrames[i] = UIK.RoundImg(t, UIK.Edge2, new Color(1f, 1f, 1f, 0.08f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, 2f);
            UIK.Stretch(carFrames[i].rectTransform);
            carTileCol[i] = TileCol;
        }
        for (int i = 0; i < 7; i++)
        {
            robotTiles[i] = Glass(r, TileCol, new Vector2(0.5f, 0.5f));
            Transform t = robotTiles[i].transform;
            robotWin[i] = UIK.RoundImg(t, UIK.Round2, Color.white, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(10, 10), 2.6f);
            robotWin[i].gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var th = UIK.Rect(robotWin[i].transform, "Thumb", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIK.Stretch(th);
            robotThumbs[i] = th.gameObject.AddComponent<RawImage>();
            robotThumbs[i].raycastTarget = false;
            robotThumbs[i].color = new Color(1f, 1f, 1f, 0f);
            robotNames[i] = UIK.Label(t, Robots.Short[i], 12, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(126, 16), new Color(0.92f, 0.95f, 1f));
            robotBadges[i] = UIK.Label(t, "", 12, TextAnchor.UpperLeft, new Vector2(0f, 1f), Vector2.zero, new Vector2(126, 18), Color.white);
            robotBadges[i].supportRichText = true;
            robotFrames[i] = UIK.RoundImg(t, UIK.Edge2, new Color(1f, 1f, 1f, 0.08f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, 2f);
            UIK.Stretch(robotFrames[i].rectTransform);
            robotTileCol[i] = TileCol;
        }
        trackBox = Glass(r, GlassCol, new Vector2(0.5f, 0.5f));
        trackThumbWin = UIK.RoundImg(trackBox.transform, UIK.Round2, Color.white, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(10, 10), 3f);
        trackThumbWin.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        var ttr = UIK.Rect(trackThumbWin.transform, "TrackThumb", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        UIK.Stretch(ttr);
        trackThumb = ttr.gameObject.AddComponent<RawImage>();
        trackThumb.raycastTarget = false;
        trackThumb.color = new Color(1f, 1f, 1f, 0f);
        trackName = UIK.Label(trackBox.transform, "", 18, TextAnchor.UpperLeft, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(200, 26), Color.white);
        UIK.Modernize(trackName, true, 0.4f);
        trackInfo = UIK.Label(trackBox.transform, "", 11, TextAnchor.UpperLeft, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(200, 34), new Color(0.75f, 0.82f, 0.92f, 0.85f));
        trackPrev = Glass(r, new Color(0.12f, 0.15f, 0.2f, 0.92f), new Vector2(0.5f, 0.5f));
        var tpl = UIK.Label(trackPrev.transform, "<", 26, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46, 46), Color.white); UIK.Stretch(tpl.rectTransform);
        trackNext = Glass(r, new Color(0.12f, 0.15f, 0.2f, 0.92f), new Vector2(0.5f, 0.5f));
        var tnl = UIK.Label(trackNext.transform, ">", 26, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46, 46), Color.white); UIK.Stretch(tnl.rectTransform);
        playGlow = UIK.RoundImg(r, UIK.SoftRect, new Color(0.2f, 1f, 0.6f, 0.35f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10, 10), 1f);
        playBtn = UIK.RoundImg(r, UIK.Round2, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260, 46), 1f);
        var pg = playBtn.gameObject.AddComponent<UIGradient>(); pg.top = new Color(0.36f, 0.98f, 0.62f); pg.bottom = new Color(0.05f, 0.62f, 0.55f);
        var pl = UIK.Label(playBtn.transform, "RACE", 26, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260, 46), new Color(0.02f, 0.12f, 0.1f));
        UIK.Modernize(pl, true, 0f);
        UIK.Stretch(pl.rectTransform);
        lobbyStatus = UIK.Label(r, "", 14, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180, 24), new Color(0.82f, 0.95f, 0.88f, 0.95f));
        lobbyHelp = UIK.Label(r, "", 11, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 34), new Color(0.7f, 0.76f, 0.86f, 0.75f));
        lobbyHelp.supportRichText = true;
        creditsBtn = Glass(r, new Color(0.12f, 0.15f, 0.2f, 0.85f), new Vector2(0.5f, 0.5f));
        var cl = UIK.Label(creditsBtn.transform, "CREDITS  C", 11, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(118, 26), new Color(0.8f, 0.9f, 1f));
        UIK.Stretch(cl.rectTransform);
        creditsPanel = Glass(r, new Color(0.03f, 0.04f, 0.06f, 0.96f), new Vector2(0.5f, 0.5f));
        // u6d: + the scanned CC0 surfaces line, licence link to this build's LICENSES.txt
        string credits = CreditsText.Replace("<b>Made for this game</b>", Gfx6.CreditLine + "<b>Made for this game</b>").Replace("games/lambo-blast/LICENSES.txt", "games/lambo-blast-u6/LICENSES.txt");
        var ct = UIK.Label(creditsPanel.transform, credits, 14, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980, 440), Color.white);
        ct.supportRichText = true;
        ct.horizontalOverflow = HorizontalWrapMode.Wrap;
        UIK.Stretch(ct.rectTransform);
        ct.rectTransform.offsetMin = new Vector2(30, 14); ct.rectTransform.offsetMax = new Vector2(-30, -14);
        creditsPanel.gameObject.SetActive(false);
    }

    // ---------------- layout ----------------
    // Landscape ref 1280x720 (match height when aspect >= 1.78, else width); portrait ref 720x1280. Card size follows the
    // number of visible cards (1-2 big cinematic windows, 3-4 compact), re-laid out on resize / join / leave.
    float cardTop;
    void LayoutLobby()
    {
        var now = new Vector2Int(Screen.width, Screen.height);
        int visible = Mathf.Max(1, slots.Count);
        if (now == lastScreen && visible == lastVisible) return;
        lastScreen = now; lastVisible = visible;
        bool portrait = Screen.height > Screen.width;
        lobbyLayout = portrait ? 1 : 0;
        float aspect = Screen.width / (float)Mathf.Max(1, Screen.height);
        var sc = lobbyCanvas.GetComponent<CanvasScaler>();
        sc.referenceResolution = portrait ? new Vector2(720, 1280) : new Vector2(1280, 720);
        sc.matchWidthOrHeight = portrait ? (1f / aspect >= 1.75f ? 0f : 1f) : (aspect >= 1.78f ? 1f : 0f);
        System.Action<Graphic, Vector2, Vector2> put = (g, p, size) => { g.rectTransform.anchoredPosition = p; g.rectTransform.sizeDelta = size; };
        if (portrait)
        {
            put(lobbyTitle, new Vector2(0, 598), new Vector2(680, 52)); lobbyTitle.fontSize = 42;
            put(lobbySub, new Vector2(0, 558), new Vector2(690, 28)); lobbySub.fontSize = 14;
            if (visible == 1) { cardSize = new Vector2(680, 0); previewH = 370f; } else { cardSize = new Vector2(346, 0); previewH = 168f; }
            cardTop = 534f;
            tileCarSize = new Vector2(166, 78); tileRobotSize = new Vector2(166, 88);
            for (int i = 0; i < 8; i++) put(carTiles[i], new Vector2(-258 + (i % 4) * 172, -158 - (i / 4) * 84), tileCarSize);
            for (int i = 0; i < 7; i++) put(robotTiles[i], new Vector2(i < 4 ? -258 + i * 172 : -172 + (i - 4) * 172, -334 - (i / 4) * 94), tileRobotSize);
            put(trackBox, new Vector2(-118, -518), new Vector2(330, 60));
            put(trackPrev, new Vector2(-312, -518), new Vector2(46, 60));
            put(trackNext, new Vector2(76, -518), new Vector2(46, 60));
            put(playBtn, new Vector2(214, -518), new Vector2(240, 56));
            put(creditsBtn, new Vector2(266, -568), new Vector2(120, 26));
            put(lobbyStatus, new Vector2(0, -598), new Vector2(690, 24)); lobbyStatus.fontSize = 13;
            put(lobbyHelp, new Vector2(0, -622), new Vector2(700, 28)); lobbyHelp.fontSize = 10;
            put(creditsPanel, Vector2.zero, new Vector2(700, 940));
        }
        else
        {
            put(lobbyTitle, new Vector2(0, 322), new Vector2(1100, 56)); lobbyTitle.fontSize = 46;
            put(lobbySub, new Vector2(0, 288), new Vector2(1180, 24)); lobbySub.fontSize = 15;
            if (visible == 1) { cardSize = new Vector2(560, 0); previewH = 196f; }
            else if (visible == 2) { cardSize = new Vector2(470, 0); previewH = 190f; }
            else if (visible == 3) { cardSize = new Vector2(392, 0); previewH = 176f; }
            else { cardSize = new Vector2(302, 0); previewH = 150f; }
            cardTop = 270f;
            tileCarSize = new Vector2(126, 76); tileRobotSize = new Vector2(136, 88);
            for (int i = 0; i < 8; i++) put(carTiles[i], new Vector2(-483 + i * 138, -122), tileCarSize);
            for (int i = 0; i < 7; i++) put(robotTiles[i], new Vector2(-474 + i * 158, -210), tileRobotSize);
            put(trackBox, new Vector2(-388, -286), new Vector2(330, 62));
            put(trackPrev, new Vector2(-584, -286), new Vector2(44, 62));
            put(trackNext, new Vector2(-192, -286), new Vector2(44, 62));
            put(playBtn, new Vector2(0, -286), new Vector2(250, 52));
            put(creditsBtn, new Vector2(548, -286), new Vector2(112, 28));
            put(lobbyStatus, new Vector2(0, -324), new Vector2(1180, 22)); lobbyStatus.fontSize = 14;
            put(lobbyHelp, new Vector2(0, -346), new Vector2(1220, 26)); lobbyHelp.fontSize = 10;
            put(creditsPanel, Vector2.zero, new Vector2(1060, 500));
        }
        playGlow.rectTransform.anchoredPosition = playBtn.rectTransform.anchoredPosition;
        playGlow.rectTransform.sizeDelta = playBtn.rectTransform.sizeDelta + new Vector2(70, 60);
        // track card insides: thumbnail on the left, name + info on the right
        Vector2 tb = trackBox.rectTransform.sizeDelta;
        float thH = tb.y - 12f, thW = thH * 16f / 9f;
        put(trackThumbWin, new Vector2(6 + thW * 0.5f, 0), new Vector2(thW, thH));
        put(trackName, new Vector2(14 + thW + (tb.x - thW - 20) * 0.5f, 10), new Vector2(tb.x - thW - 20, 24));
        put(trackInfo, new Vector2(14 + thW + (tb.x - thW - 20) * 0.5f, -14), new Vector2(tb.x - thW - 20, 30));
        trackName.fontSize = 16;
        trackName.horizontalOverflow = HorizontalWrapMode.Overflow;
        trackInfo.fontSize = lobbyLayout == 1 ? 10 : 11;
        // card insides (anchored to the card's top edge)
        float w = cardSize.x, pw = w - 20f, y = -10f;
        for (int i = 0; i < 4; i++)
        {
            cardSwatch[i].rectTransform.pivot = new Vector2(0f, 0.5f);
            put(cardSwatch[i], new Vector2(14, -17), new Vector2(26, 5));
            put(cardHead[i], new Vector2(0, -17), new Vector2(w - 92, 20));
            put(cardPreviewFrame[i], new Vector2(0, -30 - previewH * 0.5f), new Vector2(pw, previewH));
            put(cardTexts[i], new Vector2(0, -30 - previewH - 8 - 22), new Vector2(w - 28, 46));
            for (int b = 0; b < 4; b++)
            {
                int col = b % 2, row = b / 2;
                float colW = (w - 40f) * 0.5f, x0 = -w * 0.5f + 14f + col * (colW + 12f);
                float by = -30 - previewH - 62 - row * 20;
                barLabs[i][b].rectTransform.pivot = new Vector2(0f, 0.5f);
                put(barLabs[i][b], new Vector2(x0, by), new Vector2(72, 16));
                barBgs[i][b].rectTransform.anchoredPosition = new Vector2(x0 + 74, by);
                barBgs[i][b].rectTransform.sizeDelta = new Vector2(colW - 76, 6);
                bars[i][b].rectTransform.anchoredPosition = new Vector2(x0 + 74, by);
            }
        }
        cardSize.y = 30 + previewH + 62 + 20 + 22;
        for (int i = 0; i < 4; i++) { cards[i].rectTransform.sizeDelta = cardSize; cardGlow[i].rectTransform.sizeDelta = cardSize + new Vector2(70, 70); }
        // tile insides
        for (int i = 0; i < 8; i++)
        {
            Vector2 ts = tileCarSize;
            put(carWin[i], new Vector2(0, -4 - (ts.y - 24) * 0.5f), new Vector2(ts.x - 8, ts.y - 24));
            CropTo(carThumbs[i], ts.x - 8, ts.y - 24, Showroom.CarThumbW, Showroom.CarThumbH);
            put(carStrips[i], new Vector2(0, 4), new Vector2(ts.x * 0.4f, 3));
            put(carNames[i], new Vector2(0, 12), new Vector2(ts.x - 6, 16));
            put(carBadges[i], new Vector2(6 + (ts.x - 8) * 0.5f, -12), new Vector2(ts.x - 8, 18));
        }
        for (int i = 0; i < 7; i++)
        {
            Vector2 ts = tileRobotSize;
            put(robotWin[i], new Vector2(0, -4 - (ts.y - 24) * 0.5f), new Vector2(ts.x - 8, ts.y - 24));
            CropTo(robotThumbs[i], ts.x - 8, ts.y - 24, Showroom.RobotThumbW, Showroom.RobotThumbH);
            put(robotNames[i], new Vector2(0, 11), new Vector2(ts.x - 6, 16));
            put(robotBadges[i], new Vector2(6 + (ts.x - 8) * 0.5f, -12), new Vector2(ts.x - 8, 18));
        }
        SeatLog("lobby layout " + (portrait ? "portrait" : "landscape") + " " + Screen.width + "x" + Screen.height + " cards " + visible);
    }

    static void CropTo(RawImage img, float w, float h, int tw, int th)
    {
        float ra = tw / (float)th, pa = w / Mathf.Max(1f, h);
        img.uvRect = pa > ra ? new Rect(0f, (1f - ra / pa) * 0.5f, 1f, ra / pa) : new Rect((1f - pa / ra) * 0.5f, 0f, pa / ra, 1f);
    }

    // visible cards are centred (1 in the middle, 2-4 in a row; portrait 1 big, 2-4 in a 2x2 grid), top edge at cardTop
    Vector2 CardPos(int i, int visible)
    {
        float hy = cardSize.y * 0.5f;
        if (lobbyLayout == 1)
        {
            if (visible == 1) return new Vector2(0, cardTop - hy);
            return new Vector2(i % 2 == 0 ? -178 : 178, i < 2 ? cardTop - hy : cardTop - hy * 3f - 10f);
        }
        float gap = 14f;
        return new Vector2((i - (visible - 1) * 0.5f) * (cardSize.x + gap), cardTop - hy);
    }

    // turntable targets follow their windows' on-screen pixel size (1:1, no upscaling)
    void SyncStands()
    {
        float k = lobbyCanvas != null ? lobbyCanvas.scaleFactor : 1f;
        for (int i = 0; i < 4; i++)
        {
            if (!cards[i].gameObject.activeSelf) continue;
            Vector2 sz = cardPreviewFrame[i].rectTransform.rect.size * k;
            if (stands[i].Resize(Mathf.RoundToInt(sz.x), Mathf.RoundToInt(sz.y)) || cardPreview[i].texture != stands[i].rt) cardPreview[i].texture = stands[i].rt;
            cardPreview[i].uvRect = new Rect(0f, 0f, 1f, 1f);
        }
    }

    // live preview of the selected track (rendered once per build from high above the start straight)
    void RenderTrackThumb()
    {
        if (trackRT == null) { trackRT = new RenderTexture(512, 288, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }; trackRT.Create(); }
        if (trackCam == null)
        {
            trackCam = new GameObject("TrackThumbCam").AddComponent<Camera>();
            trackCam.enabled = false;
            trackCam.cullingMask = ~Showroom.Mask;
            trackCam.clearFlags = CameraClearFlags.Skybox;
            trackCam.fieldOfView = 50f;
            trackCam.nearClipPlane = 1f; trackCam.farClipPlane = 2500f;
            trackCam.allowHDR = false;
            LBPost.Add(trackCam);
        }
        Vector2 mn = Track.Min, mx = Track.Max;
        Vector3 c = new Vector3((mn.x + mx.x) * 0.5f, 0f, (mn.y + mx.y) * 0.5f);
        float size = Mathf.Max(mx.x - mn.x, mx.y - mn.y);
        Vector3 st = Track.PointAt(0f, 0f);
        Vector3 dir = Track.Flat(c - st).normalized; if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
        trackCam.transform.position = c - dir * size * 0.62f + Vector3.up * size * 0.52f;
        trackCam.transform.LookAt(c + Vector3.up * 5f);
        trackCam.targetTexture = trackRT;
        if (!trackRT.IsCreated()) trackRT.Create();
        trackCam.Render();
        trackCam.targetTexture = null;
        trackThumb.texture = trackRT;
        trackThumb.color = Color.white;
        CropTo(trackThumb, trackThumbWin.rectTransform.sizeDelta.x, trackThumbWin.rectTransform.sizeDelta.y, 512, 288);
    }

    // ---------------- per-frame lobby look ----------------
    void RefreshLobby(float dt)
    {
        int visible = Mathf.Max(1, slots.Count);
        attractT += dt;
        lobbyT += dt;
        float breathe = 0.5f + 0.5f * Mathf.Sin(lobbyT * 2.2f);
        lobbyGlowA.color = new Color(0.2f, 0.65f, 1f, 0.12f + 0.05f * Mathf.Sin(lobbyT * 0.6f));
        lobbyGlowB.color = new Color(1f, 0.25f, 0.6f, 0.08f + 0.04f * Mathf.Sin(lobbyT * 0.5f + 1.7f));
        for (int i = 0; i < 4; i++)
        {
            bool on = i < slots.Count;
            bool show = on || (i == 0 && slots.Count == 0);
            if (cards[i].gameObject.activeSelf != show) { cards[i].gameObject.SetActive(show); cardGlow[i].gameObject.SetActive(show); cardFade[i] = 0f; }
            stands[i].Tick(dt, show && state == State.Lobby);
            if (!show) continue;
            // entrance: fade + slide up
            cardFade[i] = Mathf.MoveTowards(cardFade[i], 1f, dt * 3f);
            float e = 1f - (1f - cardFade[i]) * (1f - cardFade[i]);
            Vector2 target = CardPos(i, visible) + new Vector2(0f, -18f * (1f - e));
            var crt = cards[i].rectTransform;
            crt.anchoredPosition = Vector2.Lerp(crt.anchoredPosition, target, cardFade[i] < 1f ? 1f : Mathf.Min(1f, dt * 10f));
            cardGlow[i].rectTransform.anchoredPosition = crt.anchoredPosition;
            cards[i].color = new Color(GlassCol.r, GlassCol.g, GlassCol.b, GlassCol.a * e);
            if (!on)
            {
                // nobody in yet: the showroom cycles through every car and robot
                int k = (int)(attractT / 2.6f);
                stands[i].Set(k % 8, k % 7, this, false);
                cardSwatch[i].color = new Color(1f, 1f, 1f, 0.35f);
                cardEdge[i].color = new Color(1f, 1f, 1f, 0.1f);
                cardGlow[i].color = new Color(0.4f, 0.7f, 1f, 0.12f * e);
                cardHead[i].text = "SHOWROOM  ·  <color=#9fe8ff>JOIN TO PICK YOUR CAR</color>";
                cardTexts[i].text = "<size=19>" + Cars.All[stands[i].car < 0 ? 0 : stands[i].car].name.ToUpper() + "</size>  <size=14><color=#9fb4cc>· " + Robots.Names[stands[i].robot < 0 ? 0 : stands[i].robot] + "</color></size>\n" +
                    "<size=12><color=#9fb4cc>Press A on a gamepad, Enter on the keyboard, or tap / click a car or a robot below</color></size>";
                foreach (var b in bars[i]) b.enabled = false;
                foreach (var b in barBgs[i]) b.enabled = false;
                foreach (var b in barLabs[i]) b.enabled = false;
                continue;
            }
            Slot s = slots[i];
            if (stands[i].owner == s)
            {
                if (stands[i].car != s.car) carPop[s.car] = 1f;
                if (stands[i].robot != s.robot) robotPop[s.robot] = 1f;
            }
            stands[i].Set(s.car, s.robot, s, true);
            CarSpec cs = Cars.All[s.car];
            string dev = s.kind == InputKind.Gamepad ? "CONTROLLER" : s.kind == InputKind.Keyboard ? "KEYBOARD" : "TOUCH";
            Color pc = PlayerCol[i];
            cardSwatch[i].color = pc;
            cardEdge[i].color = Color.Lerp(new Color(pc.r, pc.g, pc.b, 0.35f), new Color(pc.r, pc.g, pc.b, 0.75f), breathe);
            cardGlow[i].color = new Color(pc.r, pc.g, pc.b, (0.14f + 0.1f * breathe) * e);
            string nav = s.kind == InputKind.Gamepad ? "D-pad < > car  ·  ^ v robot" : s.kind == InputKind.Keyboard ? "arrows < > car  ·  ^ v robot  ·  or click" : "tap a car / robot below";
            cardHead[i].text = "<color=#" + ColorUtility.ToHtmlStringRGB(pc) + ">P" + (i + 1) + "</color>  ·  " + dev + "   <color=#7f93ab>" + nav + "</color>";
            Color nameCol = s.car == 7 ? new Color(0.78f, 0.8f, 0.86f) : Color.Lerp(cs.color, Color.white, 0.15f);
            cardTexts[i].text = "<size=19><color=#" + ColorUtility.ToHtmlStringRGB(nameCol) + ">" + cs.name.ToUpper() + "</color></size>  <size=14><color=#c9d6e6>· " + Robots.Names[s.robot] + "</color></size>\n" +
                "<size=11><color=#8fa3ba>" + Robots.Looks[s.robot] + "</color></size>";
            float[] v = { Mathf.InverseLerp(27f, 34f, cs.top), Mathf.InverseLerp(9f, 14f, cs.accel), Mathf.InverseLerp(78f, 106f, cs.handling), Mathf.InverseLerp(0.8f, 1.4f, cs.weight) };
            for (int b = 0; b < 4; b++)
            {
                bars[i][b].enabled = true; barBgs[i][b].enabled = true; barLabs[i][b].enabled = true;
                float bw = barBgs[i][b].rectTransform.sizeDelta.x;
                var bs = bars[i][b].rectTransform.sizeDelta;
                bars[i][b].rectTransform.sizeDelta = new Vector2(Mathf.Lerp(bs.x, bw * Mathf.Max(0.08f, v[b]), Mathf.Min(1f, dt * 8f)), 6f);
                bars[i][b].color = b == 0 && s.car == 0 ? new Color(0.35f, 1f, 0.55f) : Color.Lerp(new Color(0.35f, 0.8f, 1f), pc, 0.35f);
            }
        }
        SyncStands();
        // thumbnails: textures, highlight, badges, pop
        if (Showroom.ThumbsReady)
        {
            thumbCheckT += dt;
            if (thumbCheckT > 2f) { thumbCheckT = 0f; if (Showroom.ThumbsLost()) Showroom.RenderThumbs(); }
        }
        if (trackThumb.texture == null || (trackRT != null && !trackRT.IsCreated())) RenderTrackThumb();
        Slot ps = FindSlot(InputKind.Touch) ?? FindSlot(InputKind.Keyboard) ?? (slots.Count == 1 ? slots[0] : null);
        for (int i = 0; i < 8; i++)
        {
            if (Showroom.ThumbsReady && carThumbs[i].texture == null) { carThumbs[i].texture = Showroom.CarThumbs[i]; carThumbs[i].color = Color.white; }
            bool taken = false; string badge = ""; int firstPick = -1;
            for (int k = 0; k < slots.Count; k++)
                if (slots[k].car == i)
                {
                    if (slots[k] != ps) taken = true;
                    if (firstPick < 0) firstPick = k;
                    badge += "<color=#" + ColorUtility.ToHtmlStringRGB(PlayerCol[k]) + ">P" + (k + 1) + "</color> ";
                }
            carBadges[i].text = badge;
            TileStyle(carTiles[i], carFrames[i], carThumbs[i], firstPick, ps != null && ps.car == i, taken, ref carPop[i], ref carTileCol[i], dt);
        }
        for (int i = 0; i < 7; i++)
        {
            if (Showroom.ThumbsReady && robotThumbs[i].texture == null) { robotThumbs[i].texture = Showroom.RobotThumbs[i]; robotThumbs[i].color = Color.white; }
            bool taken = false; string badge = ""; int firstPick = -1;
            for (int k = 0; k < slots.Count; k++)
                if (slots[k].robot == i)
                {
                    if (slots[k] != ps) taken = true;
                    if (firstPick < 0) firstPick = k;
                    badge += "<color=#" + ColorUtility.ToHtmlStringRGB(PlayerCol[k]) + ">P" + (k + 1) + "</color> ";
                }
            robotBadges[i].text = badge;
            TileStyle(robotTiles[i], robotFrames[i], robotThumbs[i], firstPick, ps != null && ps.robot == i, taken, ref robotPop[i], ref robotTileCol[i], dt);
        }
        int tshow = pendingTrack >= 0 ? pendingTrack : Track.Id;
        trackPop = Mathf.Max(0f, trackPop - dt * 3.5f);
        trackBox.rectTransform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(trackPop * Mathf.PI));
        trackName.text = Track.NameOf(tshow).ToUpper();
        trackInfo.text = "TRACK " + (tshow + 1) + " / " + Track.Count + "\n" + (pendingTrack >= 0 ? "building track..." : (LBNet.On && LBNet.I.IsGuest ? "the host picks the track" : lobbyLayout == 1 ? "tap < >  ·  T  ·  LB / RB" : "tap < >  ·  T  ·  pad LB / RB"));
        trackThumb.color = Color.Lerp(trackThumb.color, pendingTrack >= 0 ? new Color(1f, 1f, 1f, 0.35f) : Color.white, Mathf.Min(1f, dt * 8f));
        lobbySub.text = Track.NameOf(tshow).ToUpper() + "   ·   " + Track.BlurbOf(tshow) + "   ·   8 cars  ·  3 laps  ·  grab the ? boxes";
        // RACE: breathing glow
        playGlow.color = new Color(0.25f, 1f, 0.65f, 0.18f + 0.22f * breathe);
        playBtn.rectTransform.localScale = Vector3.one * (1f + 0.015f * breathe);
        string top = "<color=#7dff9a>GREEN is the fastest car</color> (" + Cars.All[0].Kmh + " km/h)   ·   ";
        if (slots.Count == 0) lobbyStatus.text = top + "join to pick your car + robot";
        else lobbyStatus.text = top + slots.Count + " player" + (slots.Count > 1 ? "s" : "") + " in   ·   A / Start / Enter / RACE to go";
        string ns = NetStatus();
        if (ns != null) lobbyStatus.text = ns;
        lobbyHelp.text = HelpLines;
    }

    // tile look: glass tile, frame in the colour of the (first) player who picked it, white frame for the tap / mouse
    // player's own pick, dimmed when another player holds it, eased colour + a short pop when it is picked
    static void TileStyle(Image tile, Image frame, RawImage thumb, int pickedBy, bool mine, bool taken, ref float pop, ref Color col, float dt)
    {
        pop = Mathf.Max(0f, pop - dt * 3.5f);
        float s = 1f + 0.1f * Mathf.Sin(pop * Mathf.PI);
        if (pickedBy >= 0) s += 0.035f;
        tile.rectTransform.localScale = Vector3.Lerp(tile.rectTransform.localScale, Vector3.one * s, Mathf.Min(1f, dt * 14f));
        Color pc = pickedBy >= 0 ? PlayerCol[pickedBy] : Color.white;
        Color want = pickedBy >= 0 ? new Color(0.09f + pc.r * 0.16f, 0.105f + pc.g * 0.16f, 0.14f + pc.b * 0.16f, 0.95f) : TileCol;
        col = Color.Lerp(col, want, Mathf.Min(1f, dt * 10f));
        tile.color = col;
        Color fw = mine ? new Color(1f, 1f, 1f, 0.95f) : pickedBy >= 0 ? new Color(pc.r, pc.g, pc.b, 0.9f) : new Color(1f, 1f, 1f, 0.08f);
        frame.color = Color.Lerp(frame.color, fw, Mathf.Min(1f, dt * 12f));
        if (thumb.texture != null) thumb.color = taken && !mine ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
    }
}
