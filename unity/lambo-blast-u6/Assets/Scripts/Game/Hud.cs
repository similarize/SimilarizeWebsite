using UnityEngine;
using UnityEngine.UI;

// One HUD per viewport: position, lap, held item (with roulette), speed + drift charge, minimap,
// countdown / wrong way / finish text, hit messages and name tags over the other cars.
// lambo7: modern restyle - glass panels (rounded, translucent graphite, thin light edge), Montserrat numerals / Inter
// labels with soft shadows (no hard outlines), a speed gauge bar, an item slot ring, a framed minimap, the
// WRONG WAY / RETURN TO TRACK warnings with a countdown, and a fade-to-black overlay for the tow back to the track.
public class ViewHud
{
    public RectTransform panel;
    readonly RectTransform inner;
    readonly Text pos, posOf, lapT, timeT, speedT, speedU, center, toast, itemName, itemHint, driftT, warnT;
    readonly Image itemBg, itemRing, itemIcon, speedBar, speedBarBg, posPanel, speedPanel, fade, warnBg, toastBg;
    readonly RawImage map;
    readonly Image[] dots = new Image[8];
    readonly Text[] tags = new Text[8];
    float scale = 1f, speedShown, centerPop;
    string gearShown = "";
    string lastCenter = "";
    Rect vp;
    public float bottomInset;

    public void SetMap(Texture2D t) { if (map != null) map.texture = t; }

    static Image Glass(Transform p, Vector2 anchor, Vector2 pos, Vector2 size, float a = 0.62f)
    {
        var g = UIK.RoundImg(p, UIK.Round2, new Color(0.05f, 0.06f, 0.085f, a), anchor, pos, size, 2f);
        var e = UIK.RoundImg(g.transform, UIK.Edge2, new Color(1f, 1f, 1f, 0.12f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, 2f);
        UIK.Stretch(e.rectTransform);
        return g;
    }

    public ViewHud(Transform canvas, string name, Texture2D mapTex)
    {
        panel = UIK.Rect(canvas, "View " + name, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        UIK.Stretch(panel);
        inner = UIK.Rect(panel, "Inner", Vector2.zero, Vector2.zero, new Vector2(1280, 720));
        inner.pivot = Vector2.zero;
        inner.anchoredPosition = Vector2.zero;
        Transform r = inner;
        // position + lap (top left)
        posPanel = Glass(r, new Vector2(0f, 1f), new Vector2(146f, -78f), new Vector2(236f, 120f));
        pos = UIK.Label(posPanel.transform, "", 58, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(84f, -40f), new Vector2(140f, 66f), Color.white);
        UIK.Modernize(pos, true, 0.45f);
        posOf = UIK.Label(posPanel.transform, "", 20, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(178f, -48f), new Vector2(90f, 30f), new Color(0.8f, 0.86f, 0.95f, 0.85f));
        lapT = UIK.Label(posPanel.transform, "", 16, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(84f, -88f), new Vector2(140f, 22f), new Color(1f, 0.9f, 0.62f));
        timeT = UIK.Label(posPanel.transform, "", 14, TextAnchor.MiddleRight, new Vector2(1f, 1f), new Vector2(-60f, -88f), new Vector2(110f, 22f), new Color(0.82f, 0.88f, 0.96f, 0.85f));
        // speed (bottom left)
        speedPanel = Glass(r, new Vector2(0f, 0f), new Vector2(146f, 62f), new Vector2(236f, 92f));
        speedT = UIK.Label(speedPanel.transform, "", 40, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(82f, 12f), new Vector2(130f, 50f), Color.white);
        UIK.Modernize(speedT, true, 0.45f);
        speedU = UIK.Label(speedPanel.transform, "KM/H", 12, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(178f, 4f), new Vector2(80f, 20f), new Color(0.75f, 0.82f, 0.92f, 0.85f));
        speedBarBg = UIK.RoundImg(speedPanel.transform, UIK.Round2, new Color(1f, 1f, 1f, 0.1f), new Vector2(0f, 0.5f), new Vector2(118f, -24f), new Vector2(204f, 6f), 10f);
        speedBar = UIK.RoundImg(speedPanel.transform, UIK.Round2, Color.white, new Vector2(0f, 0.5f), new Vector2(16f, -24f), new Vector2(10f, 6f), 10f);
        speedBar.rectTransform.pivot = new Vector2(0f, 0.5f);
        var sg = speedBar.gameObject.AddComponent<UIGradient>(); sg.top = new Color(0.5f, 0.95f, 1f); sg.bottom = new Color(0.2f, 0.6f, 1f);
        driftT = UIK.Label(r, "", 20, TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(150f, 126f), new Vector2(260f, 32f), Color.white);
        UIK.Modernize(driftT, true, 0.5f);
        // centre / warnings / toast
        center = UIK.Label(r, "", 90, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1000f, 220f), Color.white);
        UIK.Modernize(center, true, 0.55f);
        warnBg = UIK.RoundImg(r, UIK.Round2, new Color(0.45f, 0.04f, 0.06f, 0.0f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(520f, 70f), 2f);
        warnT = UIK.Label(warnBg.transform, "", 28, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 70f), Color.white);
        UIK.Modernize(warnT, true, 0.4f);
        warnT.supportRichText = true;
        toastBg = UIK.RoundImg(r, UIK.Round2, new Color(0.05f, 0.06f, 0.085f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 136f), new Vector2(560f, 46f), 2f);
        toast = UIK.Label(r, "", 24, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 136f), new Vector2(900f, 46f), new Color(1f, 0.93f, 0.62f));
        // item slot (top right)
        itemBg = Glass(r, new Vector2(1f, 1f), new Vector2(-84f, -84f), new Vector2(120f, 120f));
        itemRing = UIK.Img(itemBg.transform, UIK.Ring, new Color(1f, 1f, 1f, 0.18f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(86f, 86f));
        itemIcon = UIK.Img(itemBg.transform, UIK.Circle, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(64f, 64f));
        itemName = UIK.Label(itemBg.transform, "", 13, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 13f), new Vector2(150f, 22f), Color.white);
        itemHint = UIK.Label(r, "", 12, TextAnchor.UpperCenter, new Vector2(1f, 1f), new Vector2(-84f, -156f), new Vector2(200f, 22f), new Color(0.8f, 0.86f, 0.95f, 0.8f));
        // minimap (framed circle)
        UIK.Img(r, UIK.SoftDot, new Color(0f, 0f, 0f, 0.35f), new Vector2(1f, 1f), new Vector2(-92f, -268f), new Vector2(220f, 220f));
        UIK.Img(r, UIK.Circle, new Color(0.05f, 0.06f, 0.085f, 0.55f), new Vector2(1f, 1f), new Vector2(-92f, -268f), new Vector2(178f, 178f));
        UIK.Img(r, UIK.Ring, new Color(1f, 1f, 1f, 0.16f), new Vector2(1f, 1f), new Vector2(-92f, -268f), new Vector2(180f, 180f));
        var mrt = UIK.Rect(r, "Map", new Vector2(1f, 1f), new Vector2(-92f, -268f), new Vector2(158f, 158f));
        map = mrt.gameObject.AddComponent<RawImage>();
        map.texture = mapTex;
        map.raycastTarget = false;
        for (int i = 0; i < 8; i++)
        {
            dots[i] = UIK.Img(mrt, UIK.Circle, Color.white, new Vector2(0f, 0f), Vector2.zero, new Vector2(10f, 10f));
            tags[i] = UIK.Label(r, "", 16, TextAnchor.MiddleCenter, new Vector2(0f, 0f), Vector2.zero, new Vector2(240f, 26f), Color.white);
        }
        fade = UIK.Img(panel, null, new Color(0f, 0f, 0f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        UIK.Stretch(fade.rectTransform);
    }

    public void SetActive(bool on) { panel.gameObject.SetActive(on); }

    // viewport rect (0..1) + how compact the contents should be
    public void SetRect(Rect r, float k)
    {
        vp = r;
        scale = k;
        panel.anchorMin = new Vector2(r.xMin, r.yMin);
        panel.anchorMax = new Vector2(r.xMax, r.yMax);
        panel.offsetMin = Vector2.zero;
        panel.offsetMax = Vector2.zero;
    }

    static string Ord(int p)
    {
        if (p == 1) return "1<size=26>ST</size>";
        if (p == 2) return "2<size=26>ND</size>";
        if (p == 3) return "3<size=26>RD</size>";
        return p + "<size=26>TH</size>";
    }
    static string OrdPlain(int p) { return p == 1 ? "1st" : p == 2 ? "2nd" : p == 3 ? "3rd" : p + "th"; }

    public void SetCenter(string s, Color c) { center.text = s; center.color = c; }

    static string Clock(float t)
    {
        if (t < 0f) t = 0f;
        int m = (int)(t / 60f);
        float s = t - m * 60f;
        return m + ":" + s.ToString("00.0");
    }

    public void Tick(Camera cam, Kart me, Game g, string centerOverride)
    {
        float dt = Time.unscaledDeltaTime;
        // fit the 1280x720 layout into this viewport
        Rect pr = panel.rect;
        if (pr.width > 1f)
        {
            inner.localScale = Vector3.one * scale;
            inner.sizeDelta = new Vector2(pr.width / scale, pr.height / scale);
        }
        int n = g.karts.Count;
        pos.supportRichText = true;
        pos.text = Ord(me.place);
        pos.color = me.place == 1 ? new Color(1f, 0.86f, 0.3f) : Color.white;
        posOf.text = "/ " + n;
        int lapShow = Mathf.Clamp(me.lap + 1, 1, Track.Laps);
        lapT.text = me.finished ? "FINISHED" : "LAP " + lapShow + " / " + Track.Laps;
        timeT.text = Clock(me.finished ? me.finishTime : g.RaceTime);
        float kmh = Mathf.Abs(me.speed) * 3.6f;
        speedShown = Mathf.Lerp(speedShown, kmh, Mathf.Min(1f, dt * 12f));
        speedT.text = Mathf.RoundToInt(speedShown).ToString();
        // lambo9: the bar is a rev counter (gearbox rpm, red at the shift light) + the gear beside KM/H
        float frac = me.netPuppet ? Mathf.Clamp01(speedShown / (me.spec.top * 3.6f * 1.35f)) : Mathf.Clamp01((me.rpm - Kart.Idle) / (Kart.Redline - Kart.Idle));
        speedBar.rectTransform.sizeDelta = new Vector2(Mathf.Max(6f, 204f * frac), 6f);
        speedBar.color = me.boostT > 0f ? new Color(0.5f, 1f, 0.6f) : frac > 0.9f ? new Color(1f, 0.32f, 0.25f) : Color.white;
        string gu = Mathf.Abs(me.speed) < 0.3f && me.gasS < 0.05f ? "N" : me.speed < -0.5f ? "R" : me.gear.ToString();
        if (gu != gearShown) { gearShown = gu; speedU.text = "KM/H   <b>" + gu + "</b>"; }
        if (me.drifting) { driftT.text = me.driftCharge >= 2.3f ? "DRIFT  <color=#ff9a1a>MAX</color>" : me.driftCharge >= 1.1f ? "DRIFT  <color=#5aa8ff>BOOST</color>" : "DRIFT"; }
        else driftT.text = me.boostT > 0f ? "<color=#5dff9a>BOOST</color>" : (me.shieldT > 0f ? "<color=#6fe0ff>SHIELD</color>" : "");
        // item slot
        Item show = me.rollT > 0f ? me.rollShow : me.item;
        if (show == Item.None) { itemIcon.enabled = false; itemName.text = ""; itemHint.text = ""; itemRing.color = new Color(1f, 1f, 1f, 0.12f); }
        else
        {
            itemIcon.enabled = true;
            itemIcon.color = Items.Col(show);
            itemName.text = Items.Name(show).ToUpper();
            itemHint.text = me.rollT > 0f ? "" : g.FireHint(me);
            Color ic = Items.Col(show);
            itemRing.color = me.rollT > 0f ? new Color(1f, 1f, 1f, 0.3f + 0.3f * Mathf.Sin(Time.time * 30f)) : new Color(ic.r, ic.g, ic.b, 0.85f);
        }
        // warnings: wrong way / off course, with the seconds left before the tow back
        string warn = "";
        if (!me.finished && !me.respawning && g.state == Game.State.Race)
        {
            if (me.offT > Kart.OffWarnT) warn = "RETURN TO TRACK   <color=#ffd0d0>" + Mathf.CeilToInt(Mathf.Max(0f, Kart.OffTowT - me.offT)) + "</color>";
            else if (me.wrongT > Kart.WrongWarnT) warn = "WRONG WAY   <color=#ffd0d0>" + Mathf.CeilToInt(Mathf.Max(0f, Kart.WrongTowT - me.wrongT)) + "</color>";
        }
        warnT.text = warn;
        float wa = warn.Length > 0 ? 0.78f + 0.12f * Mathf.Sin(Time.time * 8f) : 0f;
        warnBg.color = new Color(0.55f, 0.05f, 0.08f, Mathf.MoveTowards(warnBg.color.a, wa, dt * 4f));
        // centre text (pops in when it changes)
        string c;
        Color cc = Color.white;
        if (!string.IsNullOrEmpty(centerOverride)) c = centerOverride;
        else if (me.finished) { c = "<size=60>" + OrdPlain(me.place).ToUpper() + " PLACE</size>"; cc = me.place <= 3 ? new Color(1f, 0.86f, 0.3f) : Color.white; }
        else c = "";
        if (c != lastCenter) { lastCenter = c; centerPop = 1f; }
        centerPop = Mathf.MoveTowards(centerPop, 0f, dt * 4f);
        center.text = c; center.color = cc;
        center.rectTransform.localScale = Vector3.one * (1f + 0.18f * centerPop * centerPop);
        toast.text = me.toastT > 0f ? me.toast : "";
        float ta = me.toastT > 0f ? Mathf.Clamp01(me.toastT * 3f) * 0.55f : 0f;
        toastBg.color = new Color(0.05f, 0.06f, 0.085f, ta);
        toastBg.rectTransform.sizeDelta = new Vector2(Mathf.Min(900f, 40f + toast.preferredWidth), 46f);
        float lift = bottomInset / Mathf.Max(0.3f, scale);
        toast.rectTransform.anchoredPosition = new Vector2(0f, 136f + lift);
        toastBg.rectTransform.anchoredPosition = new Vector2(0f, 136f + lift);
        speedPanel.rectTransform.anchoredPosition = new Vector2(146f, 62f + lift);
        driftT.rectTransform.anchoredPosition = new Vector2(150f, 126f + lift);
        fade.color = new Color(0f, 0f, 0f, me.FadeA);

        // minimap dots (leader drawn last = on top)
        RectTransform mrt = map.rectTransform;
        for (int i = 0; i < dots.Length; i++)
        {
            if (i >= n) { dots[i].enabled = false; continue; }
            Kart k = g.karts[i];
            Vector2 uv = Track.MapUV(k.transform.position);
            dots[i].enabled = true;
            dots[i].color = k.Color;
            dots[i].rectTransform.anchoredPosition = new Vector2(Mathf.Clamp01(uv.x) * mrt.sizeDelta.x, Mathf.Clamp01(uv.y) * mrt.sizeDelta.y);
            float sz = k == me ? 16f : k.human ? 13f : 9f;
            dots[i].rectTransform.sizeDelta = Vector2.one * sz;
            if (k == me) dots[i].transform.SetAsLastSibling();
        }
        // name tags
        for (int i = 0; i < tags.Length; i++)
        {
            Text t = tags[i];
            if (cam == null || i >= n || g.karts[i] == me) { t.enabled = false; continue; }
            Kart k = g.karts[i];
            Vector3 wp = k.transform.position + Vector3.up * 2.4f;
            Vector3 sp = cam.WorldToScreenPoint(wp);
            float d = sp.z;
            if (d < 1f || d > 90f || !cam.pixelRect.Contains(new Vector2(sp.x, sp.y))) { t.enabled = false; continue; }
            Vector2 lp;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(inner, sp, null, out lp);
            t.enabled = true;
            t.text = (k.netName != null ? k.netName + " " : k.human ? "P" + (k.slot + 1) + " " : "") + Robots.Names[k.robot];
            Color kc = k.Color == Cars.All[7].color ? new Color(0.78f, 0.8f, 0.86f) : Color.Lerp(k.Color, Color.white, 0.2f);
            t.color = new Color(kc.r, kc.g, kc.b, Mathf.Clamp01(1.4f - d / 90f));
            t.fontSize = d > 45f ? 13 : 16;
            t.rectTransform.anchoredPosition = lp;
        }
    }
}
