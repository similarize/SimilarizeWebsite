using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ffu22 story mode UI. Two overlay canvases:
//   objectives (order 60, under the robot phone): chapter + objective pill at the top centre, team checklist, storm timer,
//     and per view an objective marker (diamond over the target with its distance, or an edge arrow when it is off-screen)
//   cinema (order 85, over everything but the fade): letterbox bars, dialogue box (portrait from the lobby thumbnails,
//     speaker name in the character's colour, typewriter text), chapter title cards, the big centre line ("It's over...?"),
//     a dim layer for the low point, SKIP, the quick-repair meter and the continue / start-over choice.
// Modern faces (UIK.Display / UIK.Body) and the ffu17 glass sprites, laid out on a 1280x720 (landscape) or 720x1280
// (portrait) reference so phones get the same proportions.
public class StoryUI
{
    Canvas objCanvas, cineCanvas;
    RectTransform objRoot, cineRoot;
    Image objPanel; Text objKicker, objText, objList, statusLine;
    readonly RectTransform[] mk = new RectTransform[4];
    readonly Image[] mkArrow = new Image[4], mkDot = new Image[4];
    readonly Text[] mkDist = new Text[4];
    Image barTop, barBot, dim, fade, dlg, skipBtn, portraitFrame;
    RawImage portrait;
    Text dlgName, dlgText, dlgHint, skipText, bigCenter, titleKicker, titleMain, credits;
    Image mgPanel, mgBar, mgZone, mgNeedle; Text mgTitle, mgInfo;
    Image choiceA, choiceB; Text choiceAT, choiceBT, choiceQ;
    int layout = -1;
    // ffu24 (episode 2): Jimmy radar (objectives canvas) + a cinema HUD line (orbit chase)
    RectTransform radar; Image radarBlip, radarMe; Text radarLabel, cineHud;
    public float letterbox, dimK, fadeK, titleK, bigK, creditsK;
    public string bigText = "", titleKick = "", titleText = "";
    public bool portraitMode;

    static Sprite arrowSprite, diamondSprite;

    public StoryUI()
    {
        objCanvas = UIK.MakeCanvas("StoryObjectives", null, 60, true);
        cineCanvas = UIK.MakeCanvas("StoryCinema", null, 85, true);
        objRoot = (RectTransform)objCanvas.transform;
        cineRoot = (RectTransform)cineCanvas.transform;
        Transform o = objRoot, c = cineRoot;

        objPanel = UIK.Glass(o, new Color(0.03f, 0.07f, 0.08f, 0.72f), Vector2.zero, new Vector2(620, 92));
        objPanel.rectTransform.anchorMin = objPanel.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        objPanel.rectTransform.anchoredPosition = new Vector2(0, -62);
        objKicker = L(objPanel.transform, "", 15, TextAnchor.UpperCenter, new Color(1f, 0.82f, 0.35f), true);
        UIK.Anchor(objKicker.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -26), new Vector2(-12, -6));
        objText = L(objPanel.transform, "", 22, TextAnchor.MiddleCenter, Color.white, false);
        UIK.Anchor(objText.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(14, 24), new Vector2(-14, -24));
        objText.resizeTextForBestFit = true; objText.resizeTextMinSize = 12; objText.resizeTextMaxSize = 22;
        objList = L(objPanel.transform, "", 15, TextAnchor.LowerCenter, new Color(1f, 1f, 1f, 0.85f), false);
        UIK.Anchor(objList.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(10, 5), new Vector2(-10, 26));
        objList.supportRichText = true; objText.supportRichText = true;
        statusLine = L(o, "", 20, TextAnchor.MiddleCenter, new Color(0.75f, 0.95f, 1f), true);
        statusLine.rectTransform.anchorMin = statusLine.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        statusLine.rectTransform.sizeDelta = new Vector2(700, 30);
        statusLine.rectTransform.anchoredPosition = new Vector2(0, -126);
        statusLine.supportRichText = true;
        for (int i = 0; i < 4; i++)
        {
            mk[i] = UIK.Rect(o, "Marker" + i, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60));
            mk[i].anchorMin = mk[i].anchorMax = Vector2.zero;
            mkDot[i] = UIK.Img(mk[i], Diamond(), new Color(1f, 0.85f, 0.25f, 0.95f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));
            mkArrow[i] = UIK.Img(mk[i], Arrow(), new Color(1f, 0.85f, 0.25f, 0.95f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44, 44));
            var sh = mkArrow[i].gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.6f); sh.effectDistance = new Vector2(1.5f, -1.5f);
            mkDist[i] = L(mk[i], "", 17, TextAnchor.MiddleCenter, Color.white, true);
            mkDist[i].rectTransform.sizeDelta = new Vector2(160, 26);
            mkDist[i].rectTransform.anchoredPosition = new Vector2(0, -30);
            mk[i].gameObject.SetActive(false);
        }

        radar = UIK.Rect(o, "Radar", new Vector2(1f, 0.5f), new Vector2(-100, 60), new Vector2(150, 150));
        UIK.Img(radar, UIK.Circle, new Color(0.02f, 0.06f, 0.08f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 150));
        UIK.Img(radar, UIK.Ring, new Color(0.7f, 1f, 0.45f, 0.6f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 150));
        UIK.Img(radar, UIK.Ring, new Color(0.7f, 1f, 0.45f, 0.22f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(78, 78));
        radarMe = UIK.Img(radar, Arrow(), Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18, 18));
        radarBlip = UIK.Img(radar, Diamond(), new Color(0.75f, 1f, 0.35f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24, 24));
        radarLabel = L(radar, "", 15, TextAnchor.MiddleCenter, Color.white, true);
        radarLabel.rectTransform.sizeDelta = new Vector2(240, 24); radarLabel.rectTransform.anchoredPosition = new Vector2(0, -90);
        var rt0 = L(radar, "RADAR", 12, TextAnchor.MiddleCenter, new Color(0.75f, 1f, 0.45f, 0.8f), true);
        rt0.rectTransform.sizeDelta = new Vector2(150, 18); rt0.rectTransform.anchoredPosition = new Vector2(0, 86);
        radar.gameObject.SetActive(false);

        dim = UIK.Img(c, UIK.Gradient(new Color(0f, 0f, 0.02f, 0.75f), new Color(0f, 0f, 0.02f, 0.45f), new Color(0f, 0f, 0.02f, 0.8f)), new Color(1, 1, 1, 0), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        UIK.Stretch(dim.rectTransform);
        barTop = UIK.Img(c, null, Color.black, new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
        barTop.rectTransform.anchorMin = new Vector2(0, 1); barTop.rectTransform.anchorMax = new Vector2(1, 1); barTop.rectTransform.pivot = new Vector2(0.5f, 1f);
        barBot = UIK.Img(c, null, Color.black, new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
        barBot.rectTransform.anchorMin = new Vector2(0, 0); barBot.rectTransform.anchorMax = new Vector2(1, 0); barBot.rectTransform.pivot = new Vector2(0.5f, 0f);
        titleKicker = L(c, "", 22, TextAnchor.MiddleCenter, new Color(1f, 0.82f, 0.35f), true);
        titleMain = L(c, "", 54, TextAnchor.MiddleCenter, Color.white, true);
        var tg = titleMain.gameObject.AddComponent<UIGradient>(); tg.top = new Color(0.75f, 1f, 0.7f); tg.bottom = new Color(0.35f, 0.9f, 0.85f);
        bigCenter = L(c, "", 52, TextAnchor.MiddleCenter, new Color(0.85f, 0.88f, 0.95f), true);
        credits = L(c, "", 26, TextAnchor.MiddleCenter, Color.white, false);
        credits.supportRichText = true;
        credits.verticalOverflow = VerticalWrapMode.Overflow;

        dlg = UIK.Glass(c, new Color(0.02f, 0.05f, 0.07f, 0.86f), Vector2.zero, new Vector2(960, 150));
        dlg.rectTransform.anchorMin = dlg.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        portraitFrame = UIK.Sliced(dlg.transform, UIK.Round2, Color.white, 2.4f);
        portraitFrame.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        portrait = new GameObject("Portrait", typeof(RectTransform)).AddComponent<RawImage>();
        portrait.rectTransform.SetParent(portraitFrame.transform, false);
        UIK.Stretch(portrait.rectTransform); portrait.raycastTarget = false;
        dlgName = L(dlg.transform, "", 24, TextAnchor.UpperLeft, Color.white, true);
        dlgText = L(dlg.transform, "", 24, TextAnchor.UpperLeft, Color.white, false);
        dlgText.supportRichText = false;
        dlgHint = L(dlg.transform, "", 14, TextAnchor.LowerRight, new Color(1f, 1f, 1f, 0.6f), false);
        skipBtn = UIK.Glass(c, new Color(0f, 0f, 0f, 0.45f), Vector2.zero, new Vector2(150, 40));
        skipBtn.rectTransform.anchorMin = skipBtn.rectTransform.anchorMax = new Vector2(1f, 1f);
        skipText = L(skipBtn.transform, "SKIP", 17, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.85f), true);
        UIK.Stretch(skipText.rectTransform);

        mgPanel = UIK.Glass(c, new Color(0.02f, 0.05f, 0.07f, 0.88f), Vector2.zero, new Vector2(720, 210));
        mgTitle = L(mgPanel.transform, "QUICK REPAIR", 30, TextAnchor.UpperCenter, new Color(1f, 0.82f, 0.35f), true);
        UIK.Anchor(mgTitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -52), new Vector2(-10, -12));
        mgBar = UIK.Glass(mgPanel.transform, new Color(1f, 1f, 1f, 0.12f), new Vector2(0, -4), new Vector2(600, 40));
        mgZone = UIK.Img(mgBar.transform, null, new Color(0.3f, 1f, 0.45f, 0.75f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110, 34));
        mgNeedle = UIK.Img(mgBar.transform, null, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8, 56));
        mgInfo = L(mgPanel.transform, "", 20, TextAnchor.LowerCenter, Color.white, false);
        UIK.Anchor(mgInfo.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(10, 14), new Vector2(-10, 60));
        mgInfo.supportRichText = true;
        mgPanel.gameObject.SetActive(false);

        choiceQ = L(c, "", 30, TextAnchor.MiddleCenter, Color.white, true);
        choiceA = UIK.Glass(c, new Color(0.18f, 0.66f, 0.3f, 0.95f), Vector2.zero, new Vector2(520, 72));
        choiceAT = L(choiceA.transform, "", 22, TextAnchor.MiddleCenter, Color.white, true); UIK.Stretch(choiceAT.rectTransform);
        choiceB = UIK.Glass(c, new Color(0.25f, 0.3f, 0.4f, 0.95f), Vector2.zero, new Vector2(520, 72));
        choiceBT = L(choiceB.transform, "", 22, TextAnchor.MiddleCenter, Color.white, true); UIK.Stretch(choiceBT.rectTransform);
        choiceAT.supportRichText = choiceBT.supportRichText = true;
        ShowChoice(null, null, null);

        cineHud = L(c, "", 22, TextAnchor.UpperCenter, Color.white, false);
        cineHud.rectTransform.anchorMin = cineHud.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        cineHud.rectTransform.sizeDelta = new Vector2(1100, 90); cineHud.rectTransform.anchoredPosition = new Vector2(0, -95);
        cineHud.supportRichText = true;

        fade = UIK.Img(c, null, new Color(0, 0, 0, 0), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        UIK.Stretch(fade.rectTransform);
        Dialogue(null, null, null, Color.white, "");
        SetObjective(null, null, null);
    }

    static Text L(Transform p, string s, int size, TextAnchor a, Color col, bool display)
    {
        var t = UIK.Label(p, s, size, a, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 40), col);
        UIK.Modernize(t, display, 0.5f);
        return t;
    }

    static Sprite Arrow()
    {
        if (arrowSprite != null) return arrowSprite;
        const int n = 64;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;   // points up (+v)
                float edge = Mathf.Abs(u) - (0.8f - v) * 0.55f;                        // triangle sides
                float d = Mathf.Max(edge, Mathf.Max(-v - 0.6f, v - 0.85f));
                float notch = (v < -0.15f && Mathf.Abs(u) < (-0.15f - v) * 0.9f) ? 1f : 0f;
                float a = Mathf.Clamp01(0.5f - d * n * 0.5f) * (1f - notch);
                t.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        t.Apply();
        arrowSprite = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return arrowSprite;
    }

    static Sprite Diamond()
    {
        if (diamondSprite != null) return diamondSprite;
        const int n = 64;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Abs(u) + Mathf.Abs(v);
                float a = Mathf.Clamp01((0.92f - d) * n * 0.35f);
                float hole = Mathf.Clamp01((0.42f - d) * n * 0.35f);
                t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Max(a - hole, 0f) + hole * 0.35f));
            }
        t.Apply();
        diamondSprite = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return diamondSprite;
    }

    void Layout()
    {
        bool p = Screen.height > Screen.width;
        int want = p ? 1 : 0;
        if (want == layout) return;
        layout = want; portraitMode = p;
        foreach (var cv in new[] { objCanvas, cineCanvas })
        {
            var sc = cv.GetComponent<CanvasScaler>();
            sc.referenceResolution = p ? new Vector2(720, 1280) : new Vector2(1280, 720);
            sc.matchWidthOrHeight = p ? 0f : 0.5f;
        }
        objPanel.rectTransform.sizeDelta = p ? new Vector2(560, 104) : new Vector2(620, 92);
        objPanel.rectTransform.anchoredPosition = p ? new Vector2(0, -200) : new Vector2(0, -62);
        statusLine.rectTransform.anchoredPosition = p ? new Vector2(0, -222) : new Vector2(0, -126);
        dlg.rectTransform.sizeDelta = p ? new Vector2(690, 230) : new Vector2(960, 150);
        float pw = p ? 120 : 118;
        UIK.Anchor(portraitFrame.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -16 - pw), new Vector2(16 + pw, -16));
        UIK.Anchor(dlgName.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(32 + pw, -48), new Vector2(-20, -12));
        UIK.Anchor(dlgText.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(32 + pw, 20), new Vector2(-24, -50));
        if (p) UIK.Anchor(dlgText.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(20, 24), new Vector2(-20, -150));
        dlgText.fontSize = p ? 25 : 24;
        UIK.Anchor(dlgHint.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(20, 6), new Vector2(-16, 26));
        skipBtn.rectTransform.anchoredPosition = p ? new Vector2(-95, -84) : new Vector2(-95, -52);
        titleKicker.rectTransform.sizeDelta = new Vector2(p ? 680 : 1100, 34);
        titleMain.rectTransform.sizeDelta = new Vector2(p ? 700 : 1200, 80);
        titleMain.fontSize = p ? 44 : 56;
        titleKicker.rectTransform.anchoredPosition = new Vector2(0, 52);
        titleMain.rectTransform.anchoredPosition = new Vector2(0, 0);
        bigCenter.rectTransform.sizeDelta = new Vector2(p ? 680 : 1100, 120);
        credits.rectTransform.sizeDelta = new Vector2(p ? 680 : 1000, 900);
        mgPanel.rectTransform.sizeDelta = p ? new Vector2(680, 230) : new Vector2(720, 210);
        mgBar.rectTransform.sizeDelta = new Vector2(p ? 560 : 600, 40);
        choiceQ.rectTransform.sizeDelta = new Vector2(p ? 680 : 1100, 90);
        choiceQ.rectTransform.anchoredPosition = new Vector2(0, 120);
        choiceA.rectTransform.anchoredPosition = new Vector2(0, 20);
        choiceB.rectTransform.anchoredPosition = new Vector2(0, -70);
        choiceA.rectTransform.sizeDelta = choiceB.rectTransform.sizeDelta = new Vector2(p ? 600 : 560, 72);
        radar.anchoredPosition = p ? new Vector2(-92, 230) : new Vector2(-100, 60);
        radar.localScale = Vector3.one * (p ? 0.9f : 1f);
        cineHud.rectTransform.sizeDelta = new Vector2(p ? 680 : 1100, p ? 130 : 90);
        cineHud.rectTransform.anchoredPosition = new Vector2(0, p ? -150 : -95);
    }

    // ffu24: Jimmy on the radar (rel = -1..1 camera-relative, x right / y ahead)
    public void Radar(bool on, Vector2 rel, string label)
    {
        if (radar.gameObject.activeSelf != on) radar.gameObject.SetActive(on);
        if (!on) return;
        Layout();
        radarBlip.rectTransform.anchoredPosition = rel * 66f;
        float pulse = 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 6f);
        radarBlip.color = new Color(0.75f, 1f, 0.35f, pulse);
        if (radarLabel.text != label) radarLabel.text = label;
    }
    public void CineHud(string s) { s = s ?? ""; if (cineHud.text != s) cineHud.text = s; cineHud.enabled = s.Length > 0; }

    // ---------------- objectives ----------------
    public void SetObjective(string kicker, string text, string list)
    {
        bool on = !string.IsNullOrEmpty(text);
        if (objPanel.gameObject.activeSelf != on) objPanel.gameObject.SetActive(on);
        if (!on) return;
        if (objKicker.text != kicker) objKicker.text = kicker ?? "";
        if (objText.text != text) objText.text = text;
        string l = list ?? "";
        if (objList.text != l) objList.text = l;
        float h = l.Length > 0 ? (portraitMode ? 116 : 104) : (portraitMode ? 96 : 84);
        objPanel.rectTransform.sizeDelta = new Vector2(portraitMode ? 560 : 640, h);
        UIK.Anchor(objText.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(14, l.Length > 0 ? 28 : 8), new Vector2(-14, -26));
    }

    public void SetStatus(string s) { s = s ?? ""; if (statusLine.text != s) statusLine.text = s; }

    public void ObjectivesVisible(bool on, float alpha)
    {
        if (objCanvas.enabled != on) objCanvas.enabled = on;
        var cg = objRoot.GetComponent<CanvasGroup>();
        if (cg == null) cg = objRoot.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = alpha;
    }

    // views: (camera, target world point or null, label); hides unused markers
    public void Markers(List<Camera> cams, List<Vector3?> targets, List<Vector3> from)
    {
        Layout();
        float k = objCanvas.scaleFactor > 0f ? objCanvas.scaleFactor : 1f;
        for (int i = 0; i < 4; i++)
        {
            bool on = i < cams.Count && cams[i] != null && cams[i].enabled && targets[i].HasValue;
            if (mk[i].gameObject.activeSelf != on) mk[i].gameObject.SetActive(on);
            if (!on) continue;
            Camera c = cams[i];
            Vector3 tp = targets[i].Value;
            Rect r = c.pixelRect;
            Vector3 sp = c.WorldToScreenPoint(tp + Vector3.up * 1.5f);
            float dist = Vector3.Distance(from[i], tp);
            bool behind = sp.z < 0f;
            Vector2 s2 = new Vector2(sp.x, sp.y);
            if (behind) s2 = new Vector2(r.center.x * 2f - s2.x, r.center.y * 2f - s2.y);
            float m = 46f * k, top = 150f * k;
            bool inside = !behind && s2.x > r.xMin + m && s2.x < r.xMax - m && s2.y > r.yMin + m && s2.y < r.yMax - Mathf.Min(top, r.height * 0.3f);
            Vector2 pos;
            if (inside)
            {
                pos = s2;
                mkArrow[i].enabled = false; mkDot[i].enabled = true;
                float bob = Mathf.Sin(Time.unscaledTime * 4f) * 4f;
                mkDot[i].rectTransform.anchoredPosition = new Vector2(0, 10 + bob);
                mkDist[i].rectTransform.anchoredPosition = new Vector2(0, -18);
            }
            else
            {
                Vector2 cc = r.center, d = s2 - cc;
                if (d.sqrMagnitude < 1f) d = Vector2.up;
                float hx = r.width * 0.5f - m, hy = r.height * 0.5f - m - Mathf.Min(top * 0.5f, r.height * 0.15f);
                float sc = Mathf.Min(Mathf.Abs(hx / Mathf.Max(0.001f, Mathf.Abs(d.x))), Mathf.Abs(hy / Mathf.Max(0.001f, Mathf.Abs(d.y))));
                pos = cc + d * sc;
                mkArrow[i].enabled = true; mkDot[i].enabled = false;
                float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f;
                mkArrow[i].rectTransform.localRotation = Quaternion.Euler(0, 0, ang);
                mkArrow[i].rectTransform.anchoredPosition = Vector2.zero;
                mkDist[i].rectTransform.anchoredPosition = -d.normalized * 40f;
            }
            mk[i].anchoredPosition = pos / k;
            mkDist[i].text = dist < 1000f ? Mathf.RoundToInt(dist) + " m" : (dist / 1000f).ToString("0.0") + " km";
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f);
            Color mc = new Color(1f, 0.85f, 0.25f, 0.95f * pulse);
            mkArrow[i].color = mc; mkDot[i].color = mc;
        }
    }

    public void HideMarkers() { for (int i = 0; i < 4; i++) if (mk[i].gameObject.activeSelf) mk[i].gameObject.SetActive(false); }

    // ---------------- cinema ----------------
    public void Dialogue(string name, Texture portraitTex, string text, Color nameCol, string hint)
    {
        bool on = text != null;
        if (dlg.gameObject.activeSelf != on) dlg.gameObject.SetActive(on);
        if (!on) return;
        dlgName.text = name ?? "";
        dlgName.color = Color.Lerp(nameCol, Color.white, 0.25f);
        bool hasP = portraitTex != null;
        portraitFrame.gameObject.SetActive(hasP);
        if (hasP) portrait.texture = portraitTex;
        float pw = portraitMode ? 120 : 118;
        UIK.Anchor(dlgName.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(hasP ? 32 + pw : 24, -48), new Vector2(-20, -12));
        if (!portraitMode) UIK.Anchor(dlgText.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(hasP ? 32 + pw : 24, 20), new Vector2(-24, -50));
        else UIK.Anchor(dlgText.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(20, 26), new Vector2(-20, hasP ? -150 : -52));
        dlgText.text = text;
        dlgHint.text = hint ?? "";
    }

    public void ShowChoice(string q, string a, string b)
    {
        bool on = q != null;
        choiceQ.gameObject.SetActive(on); choiceA.gameObject.SetActive(on); choiceB.gameObject.SetActive(on);
        if (!on) return;
        choiceQ.text = q; choiceAT.text = a; choiceBT.text = b;
    }
    public bool HitChoiceA(Vector2 sp) { return Hit(choiceA, sp); }
    public bool HitChoiceB(Vector2 sp) { return Hit(choiceB, sp); }
    public bool HitSkip(Vector2 sp) { return skipBtn.gameObject.activeInHierarchy && Hit(skipBtn, sp); }
    static bool Hit(Image img, Vector2 sp) { return img != null && img.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(img.rectTransform, sp, null); }

    public void Minigame(bool on, float needle01, float zoneC, float zoneW, string info)
    {
        if (mgPanel.gameObject.activeSelf != on) mgPanel.gameObject.SetActive(on);
        if (!on) return;
        float w = mgBar.rectTransform.sizeDelta.x;
        mgZone.rectTransform.sizeDelta = new Vector2(w * zoneW, 34);
        mgZone.rectTransform.anchoredPosition = new Vector2((zoneC - 0.5f) * w, 0);
        mgNeedle.rectTransform.anchoredPosition = new Vector2((needle01 - 0.5f) * w, 0);
        mgInfo.text = info;
        mgPanel.rectTransform.anchoredPosition = new Vector2(0, portraitMode ? -60 : -40);
    }

    public void SetCredits(string s) { credits.text = s ?? ""; }

    // per frame: animate bars / fades / titles; skip button only during cutscenes
    public void Tick(bool cine, bool skippable, string skipHint)
    {
        Layout();
        float H = portraitMode ? 1280f : 720f;
        float bar = letterbox * (portraitMode ? 0.1f : 0.11f) * H;
        barTop.rectTransform.sizeDelta = new Vector2(0, bar);
        barBot.rectTransform.sizeDelta = new Vector2(0, bar);
        barTop.enabled = barBot.enabled = letterbox > 0.001f;
        dlg.rectTransform.anchoredPosition = new Vector2(0, Mathf.Max(bar + 14f, portraitMode ? 150f : 20f) + dlg.rectTransform.sizeDelta.y * 0.5f);
        dim.color = new Color(1, 1, 1, dimK);
        dim.enabled = dimK > 0.001f;
        fade.color = new Color(0, 0, 0, fadeK);
        fade.enabled = fadeK > 0.001f;
        bool sk = cine && skippable;
        if (skipBtn.gameObject.activeSelf != sk) skipBtn.gameObject.SetActive(sk);
        if (sk) skipText.text = skipHint;
        float ta = Mathf.Clamp01(titleK);
        titleKicker.text = titleKick; titleMain.text = titleText;
        titleKicker.color = new Color(1f, 0.82f, 0.35f, ta);
        titleMain.color = new Color(1f, 1f, 1f, ta);
        titleMain.enabled = titleKicker.enabled = ta > 0.001f;
        bigCenter.text = bigText;
        bigCenter.color = new Color(0.86f, 0.88f, 0.96f, Mathf.Clamp01(bigK));
        bigCenter.enabled = bigK > 0.001f;
        credits.color = new Color(1, 1, 1, Mathf.Clamp01(creditsK));
        credits.enabled = creditsK > 0.001f;
    }

    public void SetVisible(bool on)
    {
        objCanvas.enabled = on; cineCanvas.enabled = on;
    }
}
